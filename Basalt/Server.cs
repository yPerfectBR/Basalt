namespace Basalt.Server;

using System.Collections.Concurrent;
using System.Diagnostics;
using Basalt.Server.Commands;
using Basalt.Server.Network;
using Basalt.Server.Plugins;
using Basalt.Protocol.Enums;
using Basalt.Protocol.Packets;
using Basalt.RakNet;
using Basalt.Server.Events;
using Basalt.Server.Scheduling;
using Basalt.Server.World;
using Basalt.Server.World.Dimension.Generation;
using Basalt.Server.World.Dimension.Provider;

using Basalt.Server.Player;
using PlayerInstance = Basalt.Server.Player.Player;
using WorldInstance = Basalt.Server.World.World;

public sealed class Server
{
    /// <summary>
    /// TODO! Adjust cause of faking windows
    /// </summary>
    private const ulong TpsUpdateIntervalTicks = 20;
    private const double TickIntervalMs = 50.0;
    private const double SpinThresholdMs = 16.0;

    /// <summary>
    /// Raknet server
    /// </summary>
    private readonly NetworkServer _raknet;
    /// <summary>
    /// Registry for dimension generators
    /// </summary>
    private readonly Dictionary<string, Type> _generatorRegistry = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Registry for world providers
    /// </summary>
    private readonly Dictionary<string, Type> _providerRegistry = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WorldInstance> _worlds = new(StringComparer.OrdinalIgnoreCase);
    private readonly IWorldScheduler _scheduler;
    /// <summary>
    /// Cancellation source for the main network loop
    /// </summary>
    private CancellationTokenSource? _runCancellation;
    /// <summary>
    /// Task for the main network loop
    /// </summary>
    private Task? _networkLoopTask;
    /// <summary>
    /// Cancellation source for the tick loop
    /// </summary>
    private CancellationTokenSource? _tickCancellation;
    /// <summary>
    /// Task for the tick loop
    /// </summary>
    private Task? _tickLoopTask;
    private long _lastTpsTimestamp;
    private ulong _lastTpsTick;
    private ulong _serverTickValue;
    private readonly Dictionary<ServerEvent, List<Delegate>> _signalHandlers = [];
    /// <summary>
    /// Registry for players (legacy; use <see cref="Sessions"/>).
    /// </summary>
    [Obsolete("Use Sessions and session.ActiveEntity")]
    public IReadOnlyDictionary<NetworkConnection, PlayerInstance> Players { get; }

    /// <summary>
    /// Central registry of connected player sessions.
    /// </summary>
    public ConcurrentDictionary<NetworkConnection, PlayerSession> Sessions { get; } = new();
    /// <summary>
    /// Registry for commands
    /// </summary>
    public CommandRegistry Commands = new();
    public PluginManager Plugins { get; }
    /// <summary>
    /// Network handler for processing minecraft packets and packet handlers
    /// </summary>
    public NetworkHandler Network { get; }
    public Properties Properties { get; }
    /// <summary>
    /// World simulation scheduler (worker pool in Phase 3+).
    /// </summary>
    public IWorldScheduler Scheduler => _scheduler;
    public IEnumerable<WorldInstance> Worlds => _worlds.Values;

    public string DefaultWorldIdentifier { get; }

    /// <summary>
    /// Ticks per second on average
    /// </summary>
    public double Tps { get; private set; } = 20.0;

    public Server(Properties? properties = null)
    {
        Properties = properties ?? new Properties();
        _raknet = new NetworkServer(new RaknetServerOptions(MaxMtu: Properties.Mtu, Port: Properties.Port));
        Network = new NetworkHandler(this);
        Plugins = new PluginManager(this);
        if (Properties.WorldThreadCount < 1)
        {
            throw new InvalidOperationException("world-thread-count must be >= 1.");
        }

        _scheduler = Properties.WorldSchedulerEnabled
            ? new WorldScheduler(this)
            : new SingleThreadScheduler(this);
        Players = new LegacyPlayersAdapter(this);

        RegisterProvider<LevelDbProvider>("leveldb");
        RegisterProvider<InMemoryProvider>("memory");
        RegisterGenerator<VoidGenerator>("void");
        RegisterGenerator<SuperFlatGenerator>("superflat");

        
        Plugins.LoadAll(Properties.PluginsDirectory);

        DefaultWorldIdentifier = Properties.DefaultWorldIdentifier;
        WorldInstance defaultWorld = Properties.WorldProvider.Equals("memory", StringComparison.OrdinalIgnoreCase)
            ? LoadWorld(DefaultWorldIdentifier, Properties.WorldProvider) ?? CreateWorld(DefaultWorldIdentifier, Properties.WorldProvider)
            : LoadWorld(DefaultWorldIdentifier, Properties.WorldProvider, Properties.WorldPath) ?? CreateWorld(DefaultWorldIdentifier, Properties.WorldProvider, Properties.WorldPath);

        if (!_generatorRegistry.TryGetValue("superflat", out Type? generatorType))
        {
            throw new KeyNotFoundException("No generator registered with identifier 'superflat'.");
        }

        if (defaultWorld.GetDimension("overworld") is null)
        {
            defaultWorld.CreateDimension("overworld", DimensionType.Overworld, generatorType);
        }
        defaultWorld.ConfigurePersistence(Properties.WorldPath);

        LoadAdditionalWorlds();

        Commands.RegisterDefaultCommands();
    }

    void LoadAdditionalWorlds()
    {
        if (string.IsNullOrWhiteSpace(Properties.AdditionalWorlds))
        {
            return;
        }

        if (!_generatorRegistry.TryGetValue("superflat", out Type? generatorType))
        {
            throw new KeyNotFoundException("No generator registered with identifier 'superflat'.");
        }

        string[] worldIds = Properties.AdditionalWorlds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = 0; i < worldIds.Length; i++)
        {
            string worldId = worldIds[i];
            if (worldId.Equals(DefaultWorldIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string worldPath = Path.Combine("worlds", worldId);
            WorldInstance? world = Properties.WorldProvider.Equals("memory", StringComparison.OrdinalIgnoreCase)
                ? LoadWorld(worldId, Properties.WorldProvider)
                : LoadWorld(worldId, Properties.WorldProvider, worldPath)
                  ?? CreateWorld(worldId, Properties.WorldProvider, worldPath);

            if (world is null)
            {
                continue;
            }

            if (world.GetDimension("overworld") is null)
            {
                world.CreateDimension("overworld", DimensionType.Overworld, generatorType);
            }
        }
    }

    public void Start()
    {
        _scheduler.Start();
        Plugins.StartAll();
        Commands.CacheAvailableCommands(this);
        _lastTpsTimestamp = Stopwatch.GetTimestamp();
        _lastTpsTick = GetWorld().TickValue;

        _runCancellation = new CancellationTokenSource();
        _networkLoopTask = Task.Run(async () =>
        {
            await _raknet.Start();
        }, _runCancellation.Token);

        CancellationTokenSource tickCancellation = new();
        _tickCancellation = tickCancellation;
        _tickLoopTask = Task.Run(() =>
        {
            CancellationToken token = tickCancellation.Token;
            while (!token.IsCancellationRequested)
            {
                long tickStartTimestamp = Stopwatch.GetTimestamp();
                Tick();

                long tickDeadlineTimestamp = tickStartTimestamp + (long)(TickIntervalMs * Stopwatch.Frequency / 1000.0);
                double remainingMs = GRTM(tickDeadlineTimestamp, Stopwatch.GetTimestamp());
                if (remainingMs <= 0)
                {
                    continue;
                }

                while (remainingMs > SpinThresholdMs)
                {
                    Thread.Sleep(1);
                    remainingMs = GRTM(tickDeadlineTimestamp, Stopwatch.GetTimestamp());
                    if (remainingMs <= 0)
                    {
                        break;
                    }
                }

                while (Stopwatch.GetTimestamp() < tickDeadlineTimestamp)
                {
                    Thread.SpinWait(1);
                }
            }
        }, _tickCancellation.Token);

        _raknet.OnMessage += Network.HandlePacket;
        _raknet.OnDisconnected += connection =>
        {
            try
            {
                Network.HandleDisconnected(connection);
            }
            catch (Exception exception)
            {
                Logger.Warn($"Unhandled disconnect error: {exception}");
            }
        };

        Emit(new ServerStartSignal());
        Logger.Info($"Basalt listening on 0.0.0.0:{Properties.Port}");
    }

    public void On<TSignal>(ServerEvent @event, Action<TSignal> handler) where TSignal : ISignal
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (!_signalHandlers.TryGetValue(@event, out List<Delegate>? handlers))
        {
            handlers = [];
            _signalHandlers[@event] = handlers;
        }

        handlers.Add(handler);
    }

    public void Emit(ServerEvent @event, ISignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        if (!_signalHandlers.TryGetValue(@event, out List<Delegate>? handlers))
        {
            return;
        }

        for (int i = 0; i < handlers.Count; i++)
        {
            Delegate handler = handlers[i];
            Type? signalType = handler.Method.GetParameters().FirstOrDefault()?.ParameterType;
            if (signalType is null || !signalType.IsInstanceOfType(signal))
            {
                continue;
            }

            handler.DynamicInvoke(signal);
        }
    }

    public void Emit(ISignal signal)
    {
        Emit(signal.Event, signal);
    }

    public void Stop()
    {
        CancellationTokenSource? runCancellation = _runCancellation;
        Task? networkLoopTask = _networkLoopTask;
        _runCancellation = null;
        _networkLoopTask = null;

        CancellationTokenSource? cancellation = _tickCancellation;
        Task? tickLoopTask = _tickLoopTask;
        _tickCancellation = null;
        _tickLoopTask = null;

        if (runCancellation is null && cancellation is null)
        {
            return;
        }

        foreach (PlayerSession session in Sessions.Values.ToArray())
        {
            try
            {
                session.Disconnect("Server closed.");
            }
            catch (Exception exception)
            {
                Logger.Warn($"Unhandled disconnect error during shutdown: {exception}");
            }
        }

        runCancellation?.Cancel();
        cancellation?.Cancel();

        try
        {
            networkLoopTask?.Wait(250);
            tickLoopTask?.Wait();
        }
        catch (AggregateException exception) when (exception.InnerExceptions.All(static inner => inner is TaskCanceledException))
        { }
        finally
        {
            runCancellation?.Dispose();
            cancellation?.Dispose();
        }

        Plugins.DisableAll();
        _scheduler.Stop();
        Logger.Info("Basalt successfully stopped.");
    }

    public WorldInstance CreateWorld(string name, string providerIdentifier, params object[] providerArgs)
    {
        return CreateWorld(name, providerIdentifier, registration: null, providerArgs);
    }

    public WorldInstance CreateWorld(
        string name,
        string providerIdentifier,
        WorldRegistration? registration,
        params object[] providerArgs)
    {
        if (_worlds.ContainsKey(name))
        {
            throw new InvalidOperationException($"World '{name}' already exists.");
        }

        if (string.IsNullOrWhiteSpace(providerIdentifier))
        {
            throw new ArgumentException("Provider identifier cannot be empty.", nameof(providerIdentifier));
        }

        if (!_providerRegistry.TryGetValue(providerIdentifier, out Type? providerType))
        {
            throw new KeyNotFoundException($"Unknown provider identifier '{providerIdentifier}'.");
        }

        if (providerArgs.Length == 0 && providerIdentifier.Equals("leveldb", StringComparison.OrdinalIgnoreCase))
        {
            providerArgs = [Path.Combine("worlds", name)];
        }

        object? providerInstance = Activator.CreateInstance(providerType, providerArgs);
        if (providerInstance is not WorldProvider provider)
        {
            throw new InvalidOperationException($"Could not construct provider '{providerType.FullName}'.");
        }

        WorldInstance world = new(name, provider);
        world.Server = this;
        ApplyWorldRegistration(world, registration, ResolveWorldDataPath(providerIdentifier, providerArgs));
        _worlds[name] = world;
        return world;
    }

    public WorldInstance? LoadWorld(string name, string providerIdentifier, params object[] providerArgs)
    {
        return LoadWorld(name, providerIdentifier, registration: null, providerArgs);
    }

    public WorldInstance? LoadWorld(
        string name,
        string providerIdentifier,
        WorldRegistration? registration,
        params object[] providerArgs)
    {
        if (string.IsNullOrWhiteSpace(providerIdentifier))
        {
            throw new ArgumentException("Provider identifier cannot be empty.", nameof(providerIdentifier));
        }

        if (!_providerRegistry.TryGetValue(providerIdentifier, out Type? providerType))
        {
            throw new KeyNotFoundException($"Unknown provider identifier '{providerIdentifier}'.");
        }

        if (providerIdentifier.Equals("memory", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (providerArgs.Length == 0 && providerIdentifier.Equals("leveldb", StringComparison.OrdinalIgnoreCase))
        {
            providerArgs = [Path.Combine("worlds", name)];
        }

        if (providerIdentifier.Equals("leveldb", StringComparison.OrdinalIgnoreCase))
        {
            string path = providerArgs.Length > 0 ? providerArgs[0] as string ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path) || !Directory.EnumerateFileSystemEntries(path).Any())
            {
                return null;
            }
        }

        object? providerInstance = Activator.CreateInstance(providerType, providerArgs);
        if (providerInstance is not WorldProvider provider)
        {
            throw new InvalidOperationException($"Could not construct provider '{providerType.FullName}'.");
        }

        WorldInstance world = new(name, provider);
        world.Server = this;
        ApplyWorldRegistration(world, registration, ResolveWorldDataPath(providerIdentifier, providerArgs));
        _worlds[name] = world;
        return world;
    }

    public bool UnloadWorld(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("World identifier cannot be empty.", nameof(identifier));
        }

        if (identifier.Equals(DefaultWorldIdentifier, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot unload the default world.");
        }

        if (!_worlds.Remove(identifier, out WorldInstance? world))
        {
            return false;
        }

        world.Server = null;
        world.Dispose();
        return true;
    }

    public WorldInstance GetWorld()
    {
        return GetWorld(DefaultWorldIdentifier);
    }

    public WorldInstance GetWorld(string identifier)
    {
        if (_worlds.TryGetValue(identifier, out WorldInstance? world))
        {
            return world;
        }

        throw new KeyNotFoundException($"World '{identifier}' was not found.");
    }

    public bool TryGetWorld(string identifier, out WorldInstance? world)
    {
        return _worlds.TryGetValue(identifier, out world);
    }

    public void RegisterProvider<TProvider>(string identifier) where TProvider : WorldProvider
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Provider identifier cannot be empty.", nameof(identifier));
        }

        _providerRegistry[identifier] = typeof(TProvider);
    }

    public void RegisterGenerator<TGenerator>(string identifier) where TGenerator : Generator
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Generator identifier cannot be empty.", nameof(identifier));
        }

        _generatorRegistry[identifier] = typeof(TGenerator);
    }

    public void Tick()
    {
        if (!Properties.WorldSchedulerEnabled)
        {
            _scheduler.DrainMainQueue();
        }

        _serverTickValue++;
        long startTimestamp = Stopwatch.GetTimestamp();
        _raknet.Tick();

        if (!Properties.WorldSchedulerEnabled)
        {
            foreach (WorldInstance world in _worlds.Values.ToArray())
            {
                if (world.PresentPlayerCount <= 0)
                {
                    continue;
                }

                long worldStartTimestamp = Stopwatch.GetTimestamp();
                world.Tick();
                long worldEndTimestamp = Stopwatch.GetTimestamp();
                ((Tickable)world).TickWork = (worldEndTimestamp - worldStartTimestamp) * 1000.0 / Stopwatch.Frequency;
            }
        }

        long endTimestamp = Stopwatch.GetTimestamp();
        UpdateTps(endTimestamp);
    }

    public void UpdateTps(long timestamp)
    {
        if (_lastTpsTimestamp == 0)
        {
            _lastTpsTimestamp = timestamp;
            _lastTpsTick = _serverTickValue;
            return;
        }

        ulong tickDelta = _serverTickValue - _lastTpsTick;
        if (tickDelta < TpsUpdateIntervalTicks)
        {
            return;
        }

        long timestampDelta = timestamp - _lastTpsTimestamp;
        if (timestampDelta <= 0)
        {
            return;
        }

        double elapsedSeconds = (double)timestampDelta / Stopwatch.Frequency;
        double currentTps;
        if (Properties.WorldSchedulerEnabled)
        {
            IReadOnlyList<WorkerLoadMetrics> metrics = _scheduler.GetMetrics();
            currentTps = metrics.Count == 0 ? 20.0 : metrics.Average(static metric => metric.Tps);
        }
        else
        {
            currentTps = tickDelta / elapsedSeconds;
        }

        Tps = Tps == 0 ? currentTps : Tps + ((currentTps - Tps) * 0.2);
        _lastTpsTimestamp = timestamp;
        _lastTpsTick = _serverTickValue;
    }

    private static double GRTM(long deadlineTimestamp, long timestamp)
    {
        return (deadlineTimestamp - timestamp) * 1000.0 / Stopwatch.Frequency;
    }

    void ApplyWorldRegistration(WorldInstance world, WorldRegistration? registration, string? worldDataPath = null)
    {
        world.Registration = registration
            ?? WorldRegistrationLoader.TryLoad(world.Name, worldDataPath)
            ?? WorldRegistrationDefaults.ForWorld(world.Name, Properties);

        WorldRegistration.Validate(world.Registration, Properties.WorldThreadCount);
    }

    static string? ResolveWorldDataPath(string providerIdentifier, object[] providerArgs)
    {
        if (!providerIdentifier.Equals("leveldb", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (providerArgs.Length == 0 || providerArgs[0] is not string path || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return path;
    }

    public void Broadcast(DataPacket packet, params PlayerInstance[]? exclude)
    {
        foreach (PlayerSession session in Sessions.Values)
        {
            if (session.ActiveEntity is not PlayerInstance player)
            {
                continue;
            }

            if (exclude is not null)
            {
                bool skipped = false;
                for (int i = 0; i < exclude.Length; i++)
                {
                    if (ReferenceEquals(exclude[i], player))
                    {
                        skipped = true;
                        break;
                    }
                }

                if (skipped)
                {
                    continue;
                }
            }

            session.Send(packet);
        }
    }
}








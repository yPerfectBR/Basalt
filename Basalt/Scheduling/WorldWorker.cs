namespace Basalt.Server.Scheduling;

using System.Collections.Concurrent;
using System.Diagnostics;
using Basalt.Server.Scheduling.Messages;
using WorldInstance = Basalt.Server.World.World;

/// <summary>
/// Simulation thread that drains its inbox and ticks attached worlds.
/// </summary>
public sealed class WorldWorker
{
    private const int MaxMessagesPerDrain = 256;
    private const double TickIntervalMs = 50.0;
    private const double SpinThresholdMs = 16.0;
    private const ulong TpsUpdateIntervalTicks = 20;

    private readonly Server _server;
    private readonly ConcurrentQueue<IWorldMessage> _inbox = new();
    private readonly Dictionary<string, WorldInstance> _attachedWorlds = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _runCancellation;
    private Task? _loopTask;
    private long _lastTpsTimestamp;
    private ulong _lastTpsTick;
    private ulong _tickValue;

    public int WorkerId { get; }
    public WorkerLoadMetrics Metrics { get; }

    internal int PendingMessageCount => _inbox.Count;

    public WorldWorker(int workerId, Server server)
    {
        WorkerId = workerId;
        _server = server;
        Metrics = new WorkerLoadMetrics { WorkerId = workerId };
    }

    public void Start()
    {
        if (_loopTask is not null)
        {
            return;
        }

        _runCancellation = new CancellationTokenSource();
        CancellationToken token = _runCancellation.Token;
        _lastTpsTimestamp = Stopwatch.GetTimestamp();
        _lastTpsTick = 0;

        _loopTask = Task.Run(() => WorkerLoop(token), token);
    }

    public void Stop()
    {
        CancellationTokenSource? cancellation = _runCancellation;
        Task? loopTask = _loopTask;
        _runCancellation = null;
        _loopTask = null;

        cancellation?.Cancel();
        try
        {
            loopTask?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException exception) when (exception.InnerExceptions.All(static inner => inner is TaskCanceledException))
        { }
        finally
        {
            cancellation?.Dispose();
            DrainInbox(int.MaxValue);
        }
    }

    public void Enqueue(IWorldMessage message)
    {
        _inbox.Enqueue(message);
    }

    void WorkerLoop(CancellationToken token)
    {
        Thread.CurrentThread.Name = $"world-worker-{WorkerId}";

        while (!token.IsCancellationRequested)
        {
            long tickStartTimestamp = Stopwatch.GetTimestamp();
#if DEBUG
            ThreadGuard.CurrentWorkerId = WorkerId;
#endif

            DrainInbox(MaxMessagesPerDrain);
            TickAttachedWorlds(tickStartTimestamp);

            long tickEndTimestamp = Stopwatch.GetTimestamp();
            Metrics.LastTickWorkMs = (tickEndTimestamp - tickStartTimestamp) * 1000.0 / Stopwatch.Frequency;
            UpdateMetrics(tickEndTimestamp);

            long tickDeadlineTimestamp = tickStartTimestamp + (long)(TickIntervalMs * Stopwatch.Frequency / 1000.0);
            SleepUntilDeadline(tickDeadlineTimestamp);
        }
    }

    void TickAttachedWorlds(long tickStartTimestamp)
    {
        foreach (WorldInstance world in _attachedWorlds.Values.ToArray())
        {
            if (world.PresentPlayerCount <= 0)
            {
                continue;
            }

            long worldStartTimestamp = Stopwatch.GetTimestamp();
            world.Tick();
            long worldEndTimestamp = Stopwatch.GetTimestamp();
            world.TickWork = (worldEndTimestamp - worldStartTimestamp) * 1000.0 / Stopwatch.Frequency;
        }

        _tickValue++;
        Metrics.TickLagMs = Math.Max(0, (Stopwatch.GetTimestamp() - tickStartTimestamp) * 1000.0 / Stopwatch.Frequency - TickIntervalMs);
    }

    void UpdateMetrics(long timestamp)
    {
        Metrics.ActiveWorldCount = _attachedWorlds.Count;
        Metrics.TotalPresentPlayers = 0;
        foreach (WorldInstance world in _attachedWorlds.Values)
        {
            Metrics.TotalPresentPlayers += world.PresentPlayerCount;
        }

        if (_lastTpsTimestamp == 0)
        {
            _lastTpsTimestamp = timestamp;
            _lastTpsTick = _tickValue;
            return;
        }

        ulong tickDelta = _tickValue - _lastTpsTick;
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
        double currentTps = Math.Min(20.0, tickDelta / elapsedSeconds);
        Metrics.Tps = Metrics.Tps == 0 ? currentTps : Metrics.Tps + ((currentTps - Metrics.Tps) * 0.2);
        _lastTpsTimestamp = timestamp;
        _lastTpsTick = _tickValue;
    }

    static void SleepUntilDeadline(long deadlineTimestamp)
    {
        double remainingMs = (deadlineTimestamp - Stopwatch.GetTimestamp()) * 1000.0 / Stopwatch.Frequency;
        if (remainingMs <= 0)
        {
            return;
        }

        while (remainingMs > SpinThresholdMs)
        {
            Thread.Sleep(1);
            remainingMs = (deadlineTimestamp - Stopwatch.GetTimestamp()) * 1000.0 / Stopwatch.Frequency;
            if (remainingMs <= 0)
            {
                return;
            }
        }

        while (Stopwatch.GetTimestamp() < deadlineTimestamp)
        {
            Thread.SpinWait(1);
        }
    }

    internal void DrainInbox(int maxMessages)
    {
        List<IWorldMessage> batch = [];
        while (batch.Count < maxMessages && _inbox.TryDequeue(out IWorldMessage? message))
        {
            batch.Add(message);
        }

        batch.Sort(static (a, b) => GetMessagePriority(a).CompareTo(GetMessagePriority(b)));

        foreach (IWorldMessage message in batch)
        {
            try
            {
                ProcessMessage(message);
            }
            catch (Exception exception)
            {
                Logger.Warn($"Worker {WorkerId} message error: {exception.Message}");
            }
        }
    }

    static int GetMessagePriority(IWorldMessage message)
    {
        return message switch
        {
            DetachWorldMessage => 0,
            AttachWorldMessage => 1,
            ProcessDisconnectMessage => 2,
            ProcessPacketMessage => 3,
            _ => 4
        };
    }

    void ProcessMessage(IWorldMessage message)
    {
        switch (message)
        {
            case AttachWorldMessage attachMessage:
                HandleAttach(attachMessage);
                break;

            case DetachWorldMessage detachMessage:
                HandleDetach(detachMessage);
                break;

            case ProcessPacketMessage packetMessage:
                HandleProcessPacket(packetMessage);
                break;

            case ProcessDisconnectMessage disconnectMessage:
                _server.Network.ProcessDisconnectOnWorker(disconnectMessage.Connection);
                break;
        }
    }

    void HandleAttach(AttachWorldMessage message)
    {
        WorldInstance world = message.World;
        if (_attachedWorlds.ContainsKey(world.Name))
        {
            return;
        }

        _attachedWorlds[world.Name] = world;

        if (_server.Properties.WorldSchedulerDebug)
        {
            Logger.Debug("[Attach] world={0} worker={1}", world.Name, WorkerId);
        }
    }

    void HandleDetach(DetachWorldMessage message)
    {
        WorldInstance world = message.World;
        if (world.PresentPlayerCount > 0)
        {
            Logger.Warn($"Detach skipped for {world.Name}: PresentPlayerCount={world.PresentPlayerCount}");
            return;
        }

        _attachedWorlds.Remove(world.Name);
        world.AttachedWorkerId = null;

        if (_server.Properties.WorldSchedulerDebug)
        {
            Logger.Debug("[Detach] world={0} worker={1}", world.Name, WorkerId);
        }
    }

    void HandleProcessPacket(ProcessPacketMessage packetMessage)
    {
        long startTimestamp = Stopwatch.GetTimestamp();

        _server.Network.HandleGamePacketOnWorker(
            packetMessage.Connection,
            packetMessage.PacketId,
            packetMessage.Payload);

        if (_server.Properties.WorldSchedulerDebug)
        {
            double durationMs = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
            Logger.Debug(
                "[Worker:{0}] ProcessPacketMessage packet={1} durationMs={2:0.###}",
                WorkerId,
                packetMessage.PacketId,
                durationMs);
        }
    }
}

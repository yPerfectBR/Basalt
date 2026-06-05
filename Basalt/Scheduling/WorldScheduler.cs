namespace Basalt.Server.Scheduling;

using Basalt.Protocol.Enums;
using Basalt.Protocol.Types;
using Basalt.RakNet;
using Basalt.Server.Player;
using Basalt.Server.Scheduling.Messages;
using Basalt.Server.World.Dimension;
using WorldInstance = Basalt.Server.World.World;

/// <summary>
/// Multi-worker scheduler: PickWorker, attach/detach, and packet routing to worker inboxes.
/// </summary>
public sealed class WorldScheduler : IWorldScheduler
{
    private const double ScoreEpsilon = 0.01;

    private readonly Server _server;
    private readonly WorldWorkerPool _pool;

    public WorldScheduler(Server server)
    {
        _server = server;
        _pool = new WorldWorkerPool(server.Properties.WorldThreadCount, server);
    }

    internal WorldWorkerPool Pool => _pool;

    public void Start()
    {
        _pool.Start();
    }

    public void Stop()
    {
        _pool.Stop();
    }

    public void RequestAttach(WorldInstance world)
    {
        if (world.IsAttached)
        {
            return;
        }

        int workerId = PickWorker(world.Registration);
        world.AttachedWorkerId = workerId;
        _pool.GetWorker(workerId).Enqueue(new AttachWorldMessage { World = world });
    }

    public void RequestDetach(WorldInstance world)
    {
        if (world.PresentPlayerCount > 0 || !world.AttachedWorkerId.HasValue)
        {
            return;
        }

        int workerId = world.AttachedWorkerId.Value;
        _pool.GetWorker(workerId).Enqueue(new DetachWorldMessage { World = world });
    }

    public IReadOnlyList<WorkerLoadMetrics> GetMetrics()
    {
        return _pool.GetAllMetrics();
    }

    public void EnqueueGamePacket(NetworkConnection connection, PacketId packetId, ReadOnlySpan<byte> payload)
    {
        int? workerId = ResolveWorkerForPacket(connection, packetId);
        if (!workerId.HasValue)
        {
            return;
        }

        if (_server.Properties.WorldSchedulerDebug)
        {
            Logger.Debug("[PacketIngress] enqueue worker={0} packet={1}", workerId.Value, packetId);
        }

        _pool.GetWorker(workerId.Value).Enqueue(new ProcessPacketMessage
        {
            Connection = connection,
            PacketId = packetId,
            Payload = payload.ToArray()
        });
    }

    public void EnqueueDisconnect(NetworkConnection connection)
    {
        int workerId = ResolveWorkerForDisconnect(connection) ?? 0;

        if (_server.Properties.WorldSchedulerDebug)
        {
            Logger.Debug("[PacketIngress] enqueue disconnect worker={0}", workerId);
        }

        _pool.GetWorker(workerId).Enqueue(new ProcessDisconnectMessage
        {
            Connection = connection
        });
    }

    public void DrainMainQueue()
    {
    }

    internal int PickWorker(WorldRegistration registration)
    {
        if (registration.PreferredWorker is int preferred)
        {
            return preferred;
        }

        int bestWorker = registration.AllowedWorkers[0];
        double bestScore = double.MaxValue;
        int bestWorldCount = int.MaxValue;

        foreach (int workerId in registration.AllowedWorkers)
        {
            WorldWorker worker = _pool.GetWorker(workerId);
            WorkerLoadMetrics metrics = GetWorkerLoad(worker);
            double score = ComputeScore(metrics);
            if (score + ScoreEpsilon < bestScore
                || (Math.Abs(score - bestScore) <= ScoreEpsilon && metrics.ActiveWorldCount < bestWorldCount))
            {
                bestScore = score;
                bestWorker = workerId;
                bestWorldCount = metrics.ActiveWorldCount;
            }
        }

        return bestWorker;
    }

    WorkerLoadMetrics GetWorkerLoad(WorldWorker worker)
    {
        int presetWorldCount = worker.Metrics.ActiveWorldCount;
        int presetPlayerCount = worker.Metrics.TotalPresentPlayers;
        double lastWorkMs = worker.Metrics.LastTickWorkMs;
        double tickLagMs = worker.Metrics.TickLagMs;
        double tps = worker.Metrics.Tps;

        worker.RefreshMetrics();
        int worldCount = Math.Max(presetWorldCount, worker.Metrics.ActiveWorldCount);
        int playerCount = Math.Max(presetPlayerCount, worker.Metrics.TotalPresentPlayers);

        foreach (WorldInstance world in _server.Worlds)
        {
            if (world.AttachedWorkerId == worker.WorkerId && !worker.HasAttachedWorld(world.Name))
            {
                worldCount++;
                playerCount += world.PresentPlayerCount;
            }
        }

        return new WorkerLoadMetrics
        {
            WorkerId = worker.WorkerId,
            ActiveWorldCount = worldCount,
            TotalPresentPlayers = playerCount,
            LastTickWorkMs = lastWorkMs,
            TickLagMs = tickLagMs,
            Tps = tps
        };
    }

    internal static double ComputeScore(WorkerLoadMetrics metrics)
    {
        return metrics.ActiveWorldCount
            + metrics.TotalPresentPlayers * 0.5
            + metrics.LastTickWorkMs
            + metrics.TickLagMs * 2.0;
    }

    int? ResolveWorkerForPacket(NetworkConnection connection, PacketId packetId)
    {
        if (packetId == PacketId.ResourcePackClientResponse)
        {
            WorldInstance world = _server.GetWorld();
            EnsureWorldRouted(world);
            return world.AttachedWorkerId;
        }

        if (!_server.Sessions.TryGetValue(connection, out PlayerSession? session))
        {
            return null;
        }

        if (session.TransferState == TransferState.Transferring)
        {
            return null;
        }

        if (session.ActiveEntity?.Dimension?.World is WorldInstance entityWorld)
        {
            EnsureWorldRouted(entityWorld);
            return entityWorld.AttachedWorkerId;
        }

        return null;
    }

    int? ResolveWorkerForDisconnect(NetworkConnection connection)
    {
        if (!_server.Sessions.TryGetValue(connection, out PlayerSession? session))
        {
            return null;
        }

        if (session.ActiveEntity?.Dimension?.World is WorldInstance world)
        {
            return world.AttachedWorkerId ?? PickWorker(world.Registration);
        }

        return 0;
    }

    void EnsureWorldRouted(WorldInstance world)
    {
        if (world.IsAttached)
        {
            return;
        }

        RequestAttach(world);
    }

    internal void SetWorkerMetrics(int workerId, WorkerLoadMetrics metrics)
    {
        WorkerLoadMetrics target = _pool.GetWorker(workerId).Metrics;
        target.ActiveWorldCount = metrics.ActiveWorldCount;
        target.TotalPresentPlayers = metrics.TotalPresentPlayers;
        target.LastTickWorkMs = metrics.LastTickWorkMs;
        target.TickLagMs = metrics.TickLagMs;
        target.Tps = metrics.Tps;
    }

    public void BeginCrossWorldTransfer(
        PlayerSession session,
        WorldInstance targetWorld,
        Dimension targetDimension,
        Vec3f position)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(targetWorld);
        ArgumentNullException.ThrowIfNull(targetDimension);

        if (session.TransferState != TransferState.Idle)
        {
            throw new InvalidOperationException("Transfer already in progress.");
        }

        if (session.ActiveEntity?.Dimension?.World is not WorldInstance sourceWorld)
        {
            throw new InvalidOperationException("Player has no active world.");
        }

        if (!sourceWorld.AttachedWorkerId.HasValue)
        {
            throw new InvalidOperationException($"Source world '{sourceWorld.Name}' is not attached to a worker.");
        }

        session.TransferState = TransferState.Transferring;

        _pool.GetWorker(sourceWorld.AttachedWorkerId.Value).Enqueue(new PrepareTransferMessage
        {
            Session = session,
            TargetWorld = targetWorld,
            TargetDimensionId = targetDimension.Identifier,
            Position = position
        });
    }
}

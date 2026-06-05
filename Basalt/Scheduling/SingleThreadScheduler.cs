namespace Basalt.Server.Scheduling;

using Basalt.Protocol.Enums;
using Basalt.RakNet;
using WorldInstance = Basalt.Server.World.World;

/// <summary>
/// Single-thread scheduler stub (Phase 0): noop until message queue is added in Phase 1.
/// </summary>
public sealed class SingleThreadScheduler : IWorldScheduler
{
    private readonly Server _server;

    public SingleThreadScheduler(Server server)
    {
        _server = server;
    }

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public void RequestAttach(WorldInstance world)
    {
    }

    public void RequestDetach(WorldInstance world)
    {
    }

    public IReadOnlyList<WorkerLoadMetrics> GetMetrics()
    {
        return
        [
            new WorkerLoadMetrics
            {
                WorkerId = 0,
                ActiveWorldCount = 0,
                TotalPresentPlayers = _server.Players.Count,
                Tps = _server.Tps
            }
        ];
    }

    public void EnqueueGamePacket(NetworkConnection connection, PacketId packetId, ReadOnlySpan<byte> payload)
    {
    }

    public void EnqueueDisconnect(NetworkConnection connection)
    {
    }

    public void DrainMainQueue()
    {
    }
}

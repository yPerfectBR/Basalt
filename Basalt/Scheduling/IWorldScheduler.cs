namespace Basalt.Server.Scheduling;

using Basalt.Protocol.Enums;
using Basalt.RakNet;
using WorldInstance = Basalt.Server.World.World;

/// <summary>
/// Coordinates world attach/detach, worker assignment, and packet routing to simulation threads.
/// </summary>
public interface IWorldScheduler
{
    /// <summary>Starts the scheduler (worker pool in Phase 3+).</summary>
    void Start();

    /// <summary>Stops the scheduler and drains pending work.</summary>
    void Stop();

    /// <summary>Attaches a world to a worker when the first player enters (Phase 3+).</summary>
    void RequestAttach(WorldInstance world);

    /// <summary>Detaches a world when the last player leaves (Phase 3+).</summary>
    void RequestDetach(WorldInstance world);

    /// <summary>Per-worker load metrics for balancing and debug.</summary>
    IReadOnlyList<WorkerLoadMetrics> GetMetrics();

    /// <summary>
    /// Enqueues a game packet for processing on the simulation thread.
    /// </summary>
    void EnqueueGamePacket(NetworkConnection connection, PacketId packetId, ReadOnlySpan<byte> payload);

    /// <summary>
    /// Enqueues a disconnect for processing on the simulation thread.
    /// </summary>
    void EnqueueDisconnect(NetworkConnection connection);

    /// <summary>
    /// Processes queued messages on the simulation thread. Called from <see cref="Server.Tick"/>.
    /// </summary>
    void DrainMainQueue();
}

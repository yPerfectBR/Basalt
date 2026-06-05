namespace Basalt.Server.Scheduling;

using System.Collections.Concurrent;
using Basalt.Protocol.Enums;
using Basalt.RakNet;
using Basalt.Server.Scheduling.Messages;
using WorldInstance = Basalt.Server.World.World;

/// <summary>
/// Single-thread scheduler: marshals network work to the tick thread via a main queue.
/// </summary>
public sealed class SingleThreadScheduler : IWorldScheduler
{
    private const int MaxMessagesPerDrain = 256;

    private readonly Server _server;
    private readonly ConcurrentQueue<IWorldMessage> _mainQueue = new();

    public SingleThreadScheduler(Server server)
    {
        _server = server;
    }

    public void Start()
    {
    }

    public void Stop()
    {
        DrainMainQueue(int.MaxValue);
    }

    public void RequestAttach(WorldInstance world)
    {
        world.AttachedWorkerId = 0;
    }

    public void RequestDetach(WorldInstance world)
    {
        world.AttachedWorkerId = null;
    }

    public IReadOnlyList<WorkerLoadMetrics> GetMetrics()
    {
        int activeWorldCount = 0;
        foreach (WorldInstance world in _server.Worlds)
        {
            if (world.PresentPlayerCount > 0)
            {
                activeWorldCount++;
            }
        }

        return
        [
            new WorkerLoadMetrics
            {
                WorkerId = 0,
                ActiveWorldCount = activeWorldCount,
                TotalPresentPlayers = _server.Players.Count,
                Tps = _server.Tps
            }
        ];
    }

    public void EnqueueGamePacket(NetworkConnection connection, PacketId packetId, ReadOnlySpan<byte> payload)
    {
        _mainQueue.Enqueue(new ProcessPacketMessage
        {
            Connection = connection,
            PacketId = packetId,
            Payload = payload.ToArray()
        });
    }

    public void EnqueueDisconnect(NetworkConnection connection)
    {
        _mainQueue.Enqueue(new ProcessDisconnectMessage
        {
            Connection = connection
        });
    }

    public void DrainMainQueue()
    {
        DrainMainQueue(MaxMessagesPerDrain);
    }

    void DrainMainQueue(int maxMessages)
    {
        int processed = 0;
        while (processed < maxMessages && _mainQueue.TryDequeue(out IWorldMessage? message))
        {
            processed++;
            try
            {
                switch (message)
                {
                    case ProcessPacketMessage packetMessage:
                        _server.Network.HandleGamePacketOnWorker(
                            packetMessage.Connection,
                            packetMessage.PacketId,
                            packetMessage.Payload);
                        break;

                    case ProcessDisconnectMessage disconnectMessage:
                        _server.Network.ProcessDisconnectOnWorker(disconnectMessage.Connection);
                        break;
                }
            }
            catch (Exception exception)
            {
                Logger.Warn($"Scheduler message error: {exception.Message}");
            }
        }
    }
}

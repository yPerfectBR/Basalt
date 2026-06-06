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
    private int _simulationThreadId;

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

    /// <summary>Pending messages in the main queue (for tests).</summary>
    internal int PendingMessageCount => _mainQueue.Count;

    /// <summary>Thread that last processed a queued message (for tests).</summary>
    internal Thread? LastMessageProcessedOnThread { get; private set; }

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
                TotalPresentPlayers = _server.Sessions.Count,
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

    public void RunOnWorldThread(WorldInstance world, Action action)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(action);

        if (world.AttachedWorkerId is null)
        {
            world.AttachedWorkerId = 0;
        }

        if (IsSimulationThread())
        {
            action();
            return;
        }

        TaskCompletionSource<object?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _mainQueue.Enqueue(new RunOnWorldThreadMessage
        {
            Action = action,
            Completion = completion
        });

        while (!completion.Task.IsCompleted)
        {
            DrainMainQueue(int.MaxValue);
            if (!completion.Task.IsCompleted && _mainQueue.IsEmpty)
            {
                Thread.Sleep(1);
            }
        }

        completion.Task.GetAwaiter().GetResult();
    }

    public void DrainMainQueue()
    {
        DrainMainQueue(MaxMessagesPerDrain);
    }

    bool IsSimulationThread()
    {
#if DEBUG
        if (ThreadGuard.CurrentWorkerId == 0)
        {
            return true;
        }
#endif

        return _simulationThreadId != 0
            && Thread.CurrentThread.ManagedThreadId == _simulationThreadId;
    }

    void DrainMainQueue(int maxMessages)
    {
#if DEBUG
        ThreadGuard.CurrentWorkerId = 0;
#endif

        _simulationThreadId = Thread.CurrentThread.ManagedThreadId;
        int processed = 0;
        while (processed < maxMessages && _mainQueue.TryDequeue(out IWorldMessage? message))
        {
            processed++;
            try
            {
                LastMessageProcessedOnThread = Thread.CurrentThread;

                switch (message)
                {
                    case RunOnWorldThreadMessage runMessage:
                        HandleRunOnWorldThread(runMessage);
                        break;

                    case ProcessPacketMessage packetMessage:
                        if (_server.Properties.WorldSchedulerDebug)
                        {
                            Logger.Debug(
                                "[Scheduler] ProcessPacketMessage packet={0} thread={1}",
                                packetMessage.PacketId,
                                Thread.CurrentThread.Name ?? Thread.CurrentThread.ManagedThreadId.ToString());
                        }

                        _server.Network.HandleGamePacketOnWorker(
                            packetMessage.Connection,
                            packetMessage.PacketId,
                            packetMessage.Payload);
                        break;

                    case ProcessDisconnectMessage disconnectMessage:
                        if (_server.Properties.WorldSchedulerDebug)
                        {
                            Logger.Debug(
                                "[Scheduler] ProcessDisconnectMessage thread={0}",
                                Thread.CurrentThread.Name ?? Thread.CurrentThread.ManagedThreadId.ToString());
                        }

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

    static void HandleRunOnWorldThread(RunOnWorldThreadMessage message)
    {
        try
        {
            message.Action();
            message.Completion?.TrySetResult(null);
        }
        catch (Exception exception)
        {
            message.Completion?.TrySetException(exception);
            throw;
        }
    }
}

namespace Basalt.Tests.Scheduling;

using Basalt.Protocol.Enums;
using Basalt.RakNet;
using Basalt.Server;
using Basalt.Server.Entity.Traits.Types;
using Basalt.Server.Player;
using Basalt.Server.Scheduling;
using Basalt.Server.Scheduling.Messages;
using Basalt.Server.World;
using Basalt.Server.World.Dimension;
using Basalt.Tests.Support;
using Vec3f = Basalt.Protocol.Types.Vec3f;

public sealed class CrossWorkerTransferTests
{
    [Fact]
    public void CrossWorkerTransfer_PickWorkerChoosesFreerThread()
    {
        Server server = TestServerFactory.CreateMultiWorkerServer();
        WorldScheduler scheduler = TestServerFactory.RequireWorldScheduler(server);

        scheduler.SetWorkerMetrics(0, new WorkerLoadMetrics
        {
            WorkerId = 0,
            ActiveWorldCount = 1,
            TotalPresentPlayers = 1,
            LastTickWorkMs = 20,
            TickLagMs = 5
        });
        scheduler.SetWorkerMetrics(1, new WorkerLoadMetrics
        {
            WorkerId = 1,
            ActiveWorldCount = 0,
            TotalPresentPlayers = 0,
            LastTickWorkMs = 0,
            TickLagMs = 0
        });

        WorldRegistration registration = new()
        {
            Identifier = "world_copy",
            AllowedWorkers = [0, 1]
        };

        Assert.Equal(1, scheduler.PickWorker(registration));
    }

    [Fact]
    public void CrossWorkerTransfer_TargetWorldAttachIfDormant()
    {
        Server server = TestServerFactory.CreateMultiWorkerServer();
        WorldScheduler scheduler = TestServerFactory.RequireWorldScheduler(server);
        World target = TestServerFactory.CreateRegisteredWorld(server, "target", [0, 1]);

        scheduler.RequestAttach(target);

        Assert.NotNull(target.AttachedWorkerId);
        Assert.Contains(target.AttachedWorkerId.Value, target.Registration.AllowedWorkers);
        TestServerFactory.DrainAllWorkers(scheduler, rounds: 5);
        Assert.True(target.IsAttached);
    }

    [Fact]
    public void CrossWorkerTransfer_SessionHasOneEntityAfterComplete()
    {
        Server server = TestServerFactory.CreateMultiWorkerServer();
        WorldScheduler scheduler = TestServerFactory.RequireWorldScheduler(server);

        World source = TestServerFactory.CreateRegisteredWorld(server, "source", [0]);
        World target = TestServerFactory.CreateRegisteredWorld(server, "target", [0, 1]);

        TestNetworkConnection connection = new();
        Player player = new("Steve", "12345", Guid.NewGuid());
        PlayerSession session = new()
        {
            Connection = connection,
            Network = server.Network,
            Username = "Steve",
            Xuid = "12345",
            Uuid = player.Uuid,
            ActiveEntity = player
        };
        player.Session = session;
        server.Sessions[connection] = session;

        Dimension sourceDimension = source.GetDimension(DimensionType.Overworld)!;
        WorldPlayerPresence.OnPlayerEnteredWorld(server, source);
        player.Spawn(sourceDimension, new EntitySpawnOptions(InitialSpawn: true));
        TestServerFactory.DrainAllWorkers(scheduler, rounds: 5);

        Dimension targetDimension = target.GetDimension(DimensionType.Overworld)!;
        PlayerWorldTransfer.PlayerTransform transform = new(new Vec3f { X = 8, Y = 64, Z = 8 }, player.Pitch, player.Yaw, player.HeadYaw);

        scheduler.BeginCrossWorldTransfer(session, target, targetDimension, transform, TransferCarryFlags.None);
        TestServerFactory.DrainAllWorkers(scheduler);

        Assert.Equal(TransferState.Idle, session.TransferState);
        Assert.NotNull(session.ActiveEntity);
        Assert.Same(target, session.ActiveEntity!.Dimension?.World);
        Assert.Equal(1, target.PresentPlayerCount);
        Assert.Equal(0, source.PresentPlayerCount);
        Assert.Equal(1, target.AttachedWorkerId);
    }

    [Fact]
    public void SameWorkerCrossWorld_UsesPerWorldTransferWithoutSnapshotProtocol()
    {
        Server server = TestServerFactory.CreateMultiWorkerServer();
        WorldScheduler scheduler = TestServerFactory.RequireWorldScheduler(server);

        World source = TestServerFactory.CreateRegisteredWorld(server, "source_a", [0]);
        World target = TestServerFactory.CreateRegisteredWorld(server, "source_b", [0]);

        TestNetworkConnection connection = new();
        Player player = new("Alex", "67890", Guid.NewGuid());
        PlayerSession session = new()
        {
            Connection = connection,
            Network = server.Network,
            Username = "Alex",
            Xuid = "67890",
            Uuid = player.Uuid,
            ActiveEntity = player
        };
        player.Session = session;

        Dimension sourceDimension = source.GetDimension(DimensionType.Overworld)!;
        WorldPlayerPresence.OnPlayerEnteredWorld(server, source);
        player.Spawn(sourceDimension, new EntitySpawnOptions(InitialSpawn: true));

        Dimension targetDimension = target.GetDimension(DimensionType.Overworld)!;
        PlayerWorldTransfer.PlayerTransform transform = new(new Vec3f { X = 1, Y = 64, Z = 1 }, player.Pitch, player.Yaw, player.HeadYaw);
        PlayerWorldTransfer.ApplySameWorker(server, player, target, targetDimension, transform, TransferCarryFlags.None);

        Assert.Same(target, player.Dimension?.World);
        Assert.Equal(TransferState.Idle, session.TransferState);
        Assert.Same(player, session.ActiveEntity);
        Assert.Equal(0, source.AttachedWorkerId);
        Assert.Equal(0, target.AttachedWorkerId);
    }

    [Fact]
    public void TransferFailure_SessionNotStuckTransferring()
    {
        Server server = TestServerFactory.CreateMultiWorkerServer();
        WorldScheduler scheduler = TestServerFactory.RequireWorldScheduler(server);
        WorldWorker worker = scheduler.Pool.GetWorker(0);

        TestNetworkConnection connection = new();
        PlayerSession session = new()
        {
            Connection = connection,
            Network = server.Network,
            Username = "Fail",
            Xuid = "000",
            Uuid = Guid.NewGuid(),
            TransferState = TransferState.Transferring
        };
        server.Sessions[connection] = session;

        worker.Enqueue(new CompleteTransferMessage
        {
            Session = session,
            Snapshot = new PlayerEntitySnapshot
            {
                Username = "Fail",
                Xuid = "000",
                Uuid = session.Uuid,
                Position = new Vec3f(),
                SourceWorldId = "missing",
                TargetWorldId = "missing_world",
                TargetDimensionId = "overworld",
                SourceEntityNbt = new Basalt.Protocol.Nbt.CompoundTag()
            }
        });

        worker.DrainInbox(int.MaxValue);

        Assert.Equal(TransferState.Idle, session.TransferState);
        Assert.Null(session.ActiveEntity);
    }
}

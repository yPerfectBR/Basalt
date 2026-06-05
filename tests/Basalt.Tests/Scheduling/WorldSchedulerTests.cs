namespace Basalt.Tests.Scheduling;

using Basalt.Protocol.Enums;
using Basalt.RakNet;
using Basalt.Server;
using Basalt.Server.Player;
using Basalt.Server.Scheduling;
using Basalt.Server.World;
using Basalt.Tests.Support;

public sealed class WorldSchedulerTests
{
    [Fact]
    public void PickWorker_RespectsAllowedWorkers()
    {
        Server server = TestServerFactory.CreateMultiWorkerServer();
        WorldScheduler scheduler = TestServerFactory.RequireWorldScheduler(server);

        scheduler.SetWorkerMetrics(2, new WorkerLoadMetrics
        {
            WorkerId = 2,
            ActiveWorldCount = 5,
            TotalPresentPlayers = 10,
            LastTickWorkMs = 40,
            TickLagMs = 5
        });
        scheduler.SetWorkerMetrics(3, new WorkerLoadMetrics
        {
            WorkerId = 3,
            ActiveWorldCount = 0,
            TotalPresentPlayers = 0,
            LastTickWorkMs = 0,
            TickLagMs = 0
        });

        WorldRegistration registration = new()
        {
            Identifier = "dungeon",
            AllowedWorkers = [2, 3]
        };

        int picked = scheduler.PickWorker(registration);

        Assert.Equal(3, picked);
        Assert.Contains(picked, registration.AllowedWorkers);
    }

    [Fact]
    public void PickWorker_ChoosesLowerLoadScore()
    {
        Server server = TestServerFactory.CreateMultiWorkerServer();
        WorldScheduler scheduler = TestServerFactory.RequireWorldScheduler(server);

        scheduler.SetWorkerMetrics(1, new WorkerLoadMetrics
        {
            WorkerId = 1,
            ActiveWorldCount = 8,
            TotalPresentPlayers = 16,
            LastTickWorkMs = 30,
            TickLagMs = 10
        });
        scheduler.SetWorkerMetrics(2, new WorkerLoadMetrics
        {
            WorkerId = 2,
            ActiveWorldCount = 1,
            TotalPresentPlayers = 2,
            LastTickWorkMs = 5,
            TickLagMs = 0
        });

        WorldRegistration registration = new()
        {
            Identifier = "island",
            AllowedWorkers = [1, 2]
        };

        Assert.Equal(2, scheduler.PickWorker(registration));
    }

    [Fact]
    public void PickWorker_UsesPreferredWorkerWhenSet()
    {
        Server server = TestServerFactory.CreateMultiWorkerServer();
        WorldScheduler scheduler = TestServerFactory.RequireWorldScheduler(server);

        scheduler.SetWorkerMetrics(1, new WorkerLoadMetrics { WorkerId = 1, ActiveWorldCount = 0 });
        scheduler.SetWorkerMetrics(2, new WorkerLoadMetrics { WorkerId = 2, ActiveWorldCount = 99 });

        WorldRegistration registration = new()
        {
            Identifier = "preferred",
            AllowedWorkers = [1, 2],
            PreferredWorker = 2
        };

        Assert.Equal(2, scheduler.PickWorker(registration));
    }

    [Fact]
    public void PickWorker_TieBreaksByFewerWorlds()
    {
        Server server = TestServerFactory.CreateMultiWorkerServer();
        WorldScheduler scheduler = TestServerFactory.RequireWorldScheduler(server);

        scheduler.SetWorkerMetrics(1, new WorkerLoadMetrics
        {
            WorkerId = 1,
            ActiveWorldCount = 3,
            TotalPresentPlayers = 0,
            LastTickWorkMs = 0,
            TickLagMs = 0
        });
        scheduler.SetWorkerMetrics(2, new WorkerLoadMetrics
        {
            WorkerId = 2,
            ActiveWorldCount = 1,
            TotalPresentPlayers = 0,
            LastTickWorkMs = 0,
            TickLagMs = 0
        });

        WorldRegistration registration = new()
        {
            Identifier = "tie",
            AllowedWorkers = [1, 2]
        };

        Assert.Equal(2, scheduler.PickWorker(registration));
    }

    [Fact]
    public void Attach_OnFirstPlayer_SetsAttachedWorkerId()
    {
        Server server = TestServerFactory.CreateMultiWorkerServer();
        World world = server.GetWorld();
        Assert.False(world.IsAttached);

        WorldPlayerPresence.OnPlayerEnteredWorld(server, world);

        Assert.Equal(1, world.PresentPlayerCount);
        Assert.True(world.IsAttached);
        Assert.NotNull(world.AttachedWorkerId);
        Assert.Equal(0, world.AttachedWorkerId);
    }

    [Fact]
    public void Detach_OnLastPlayer_ClearsAttachedWorkerId()
    {
        Server server = TestServerFactory.CreateMultiWorkerServer();
        World world = server.GetWorld();

        WorldPlayerPresence.OnPlayerEnteredWorld(server, world);
        WorldPlayerPresence.OnPlayerLeftWorld(server, world);

        for (int i = 0; i < 50 && world.IsAttached; i++)
        {
            Thread.Sleep(10);
        }

        Assert.Equal(0, world.PresentPlayerCount);
        Assert.False(world.IsAttached);
        Assert.Null(world.AttachedWorkerId);
    }

    [Fact]
    public void EnqueuePacket_RoutesToCorrectWorker()
    {
        Server server = TestServerFactory.CreateMultiWorkerServer();
        WorldScheduler scheduler = TestServerFactory.RequireWorldScheduler(server);
        World world = server.GetWorld();
        WorldPlayerPresence.OnPlayerEnteredWorld(server, world);

        int workerId = world.AttachedWorkerId!.Value;
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

        var dimension = world.GetDimension(DimensionType.Overworld);
        Assert.NotNull(dimension);
        player.Spawn(dimension, new Basalt.Server.Entity.Traits.Types.EntitySpawnOptions(InitialSpawn: true));

        scheduler.EnqueueGamePacket(connection, PacketId.PlayerAuthInput, []);

        Assert.Equal(1, scheduler.Pool.GetWorker(workerId).PendingMessageCount);
        for (int i = 0; i < scheduler.Pool.WorkerCount; i++)
        {
            if (i == workerId)
            {
                continue;
            }

            Assert.Equal(0, scheduler.Pool.GetWorker(i).PendingMessageCount);
        }
    }

    [Fact]
    public void WorldSchedulerDisabled_UsesSingleThreadPath()
    {
        Server server = TestServerFactory.CreateMemoryServer();
        SingleThreadScheduler scheduler = Assert.IsType<SingleThreadScheduler>(server.Scheduler);
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

        server.Scheduler.EnqueueGamePacket(connection, PacketId.PlayerAuthInput, []);

        Assert.Equal(1, scheduler.PendingMessageCount);
    }
}

namespace Basalt.Tests.Support;

using Basalt.Protocol.Enums;
using Basalt.Server;
using Basalt.Server.Scheduling;
using Basalt.Server.World;

internal static class TestServerFactory
{
    public static Server CreateMemoryServer()
    {
        return new Server(new Properties
        {
            WorldProvider = "memory",
            Port = 0,
            AdditionalWorlds = "",
            PluginsDirectory = Path.Combine(Path.GetTempPath(), "basalt-tests", Guid.NewGuid().ToString("N"))
        });
    }

    public static Server CreateMultiWorkerServer(int workerCount = 4)
    {
        Server server = new(new Properties
        {
            WorldProvider = "memory",
            Port = 0,
            AdditionalWorlds = "",
            WorldSchedulerEnabled = true,
            WorldThreadCount = workerCount,
            PluginsDirectory = Path.Combine(Path.GetTempPath(), "basalt-tests", Guid.NewGuid().ToString("N"))
        });
        server.Scheduler.Start();
        return server;
    }

    public static WorldScheduler RequireWorldScheduler(Server server)
    {
        return Assert.IsType<WorldScheduler>(server.Scheduler);
    }

    /// <summary>
    /// Mirrors active-only world ticking from <see cref="Server.Tick"/> when scheduler is disabled.
    /// </summary>
    public static void SimulateActiveWorldTicks(params World[] worlds)
    {
        foreach (World world in worlds)
        {
            if (world.PresentPlayerCount <= 0)
            {
                continue;
            }

            world.TickValue++;
        }
    }

    public static World EnsureOverworld(Server server, World world)
    {
        if (world.GetDimension(DimensionType.Overworld) is not null)
        {
            return world;
        }

        world.CreateDimension(
            "overworld",
            DimensionType.Overworld,
            typeof(Basalt.Server.World.Dimension.Generation.SuperFlatGenerator));
        return world;
    }

    public static World CreateRegisteredWorld(Server server, string name, int[] allowedWorkers)
    {
        WorldRegistration registration = new()
        {
            Identifier = name,
            AllowedWorkers = allowedWorkers
        };
        World world = server.CreateWorld(name, "memory", registration);
        return EnsureOverworld(server, world);
    }

    public static void DrainAllWorkers(WorldScheduler scheduler, int rounds = 30)
    {
        for (int round = 0; round < rounds; round++)
        {
            for (int workerId = 0; workerId < scheduler.Pool.WorkerCount; workerId++)
            {
                scheduler.Pool.GetWorker(workerId).DrainInbox(int.MaxValue);
            }

            Thread.Sleep(5);
        }
    }
}

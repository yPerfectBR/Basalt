namespace Basalt.Tests.Support;

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
            PluginsDirectory = Path.Combine(Path.GetTempPath(), "basalt-tests", Guid.NewGuid().ToString("N"))
        });
    }

    public static Server CreateMultiWorkerServer(int workerCount = 4)
    {
        Server server = new(new Properties
        {
            WorldProvider = "memory",
            Port = 0,
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
}

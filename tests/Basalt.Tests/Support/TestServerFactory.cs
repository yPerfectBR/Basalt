namespace Basalt.Tests.Support;

using Basalt.Server;
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

    /// <summary>
    /// Mirrors active-only world ticking from <see cref="Server.Tick"/>.
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

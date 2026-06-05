namespace Basalt.Tests.Scheduling;

using Basalt.Server;
using Basalt.Server.Scheduling;
using Basalt.Server.World;
using Basalt.Tests.Support;

public sealed class ActiveWorldTickTests
{
    [Fact]
    public void DormantWorld_DoesNotIncrementTickValue()
    {
        World world = new("dormant");

        TestServerFactory.SimulateActiveWorldTicks(world);

        Assert.Equal(0UL, world.TickValue);
    }

    [Fact]
    public void ActiveWorld_IncrementsTickValue()
    {
        World world = new("active")
        {
            PresentPlayerCount = 1
        };

        TestServerFactory.SimulateActiveWorldTicks(world);

        Assert.Equal(1UL, world.TickValue);
    }

    [Fact]
    public void WorldPlayerPresence_AttachOnFirstPlayer_SetsAttachedWorkerId()
    {
        Server server = TestServerFactory.CreateMemoryServer();
        World world = server.GetWorld();
        Assert.False(world.IsAttached);

        WorldPlayerPresence.OnPlayerEnteredWorld(server, world);

        Assert.Equal(1, world.PresentPlayerCount);
        Assert.True(world.IsAttached);
        Assert.Equal(0, world.AttachedWorkerId);
    }

    [Fact]
    public void WorldPlayerPresence_DetachOnLastPlayer_ClearsAttachedWorkerId()
    {
        Server server = TestServerFactory.CreateMemoryServer();
        World world = server.GetWorld();
        WorldPlayerPresence.OnPlayerEnteredWorld(server, world);

        WorldPlayerPresence.OnPlayerLeftWorld(server, world);

        Assert.Equal(0, world.PresentPlayerCount);
        Assert.False(world.IsAttached);
        Assert.Null(world.AttachedWorkerId);
    }

    [Fact]
    public void ServerTick_UpdatesTpsWhenDefaultWorldIsDormant()
    {
        Server server = TestServerFactory.CreateMemoryServer();
        Assert.Equal(0, server.GetWorld().PresentPlayerCount);

        for (int i = 0; i < 25; i++)
        {
            server.Tick();
        }

        Assert.True(server.Tps > 0);
        Assert.Equal(0UL, server.GetWorld().TickValue);
    }
}

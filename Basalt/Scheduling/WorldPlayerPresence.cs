namespace Basalt.Server.Scheduling;

using WorldInstance = Basalt.Server.World.World;

/// <summary>
/// Maintains per-world player counts for active-only ticking and attach/detach (Phase 3+).
/// </summary>
public static class WorldPlayerPresence
{
    public static void OnPlayerEnteredWorld(Server server, WorldInstance world)
    {
        if (world.PresentPlayerCount == 0)
        {
            server.Scheduler.RequestAttach(world);
        }

        world.PresentPlayerCount++;
    }

    public static void OnPlayerLeftWorld(Server server, WorldInstance world)
    {
        if (world.PresentPlayerCount <= 0)
        {
            return;
        }

        world.PresentPlayerCount--;

        if (world.PresentPlayerCount == 0)
        {
            server.Scheduler.RequestDetach(world);
        }
    }
}

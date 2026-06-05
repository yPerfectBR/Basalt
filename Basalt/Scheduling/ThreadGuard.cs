namespace Basalt.Server.Scheduling;

using System.Diagnostics;
using WorldInstance = Basalt.Server.World.World;

#if DEBUG
/// <summary>
/// Debug-only assertions that world mutation runs on the expected worker thread.
/// </summary>
public static class ThreadGuard
{
    [ThreadStatic]
    public static int? CurrentWorkerId;

    public static void AssertWorldThread(WorldInstance world)
    {
        Debug.Assert(world.AttachedWorkerId == CurrentWorkerId);
    }
}
#endif

using Basalt.Server.Events;
using Basalt.Server.Plugins;
using WorldInstance = Basalt.Server.World.World;

[assembly: Plugin("SchedulerSnapshot", "1.0.0", Authors = ["Basalt"])]

namespace Basalt.Samples.Plugins.SchedulerSnapshot;

/// <summary>
/// Global-thread example that inspects worlds via RunOnWorldThread.
/// </summary>
public sealed class SchedulerSnapshotPlugin : Plugin
{
    public override void OnStart()
    {
        Listen<ServerStartSignal>(ServerEvent.ServerStart, HandleServerStart);
    }

    void HandleServerStart(ServerStartSignal _)
    {
        bool schedulerEnabled = Server.Properties.WorldSchedulerEnabled;
        int workerCount = Server.Properties.WorldThreadCount;
        Log(
            $"server start schedulerEnabled={schedulerEnabled} workerCount={workerCount} thread={Environment.CurrentManagedThreadId}");

        foreach (WorldInstance world in Server.Worlds)
        {
            if (world.PresentPlayerCount <= 0 && !string.Equals(world.Name, Server.DefaultWorldIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            RunOnWorld(world, () => LogWorldSnapshot(world));
        }
    }

    void LogWorldSnapshot(WorldInstance world)
    {
        Log(
            $"world={world.Name} present={world.PresentPlayerCount} attachedWorker={world.AttachedWorkerId?.ToString() ?? "none"} thread={Environment.CurrentManagedThreadId}");
    }
}

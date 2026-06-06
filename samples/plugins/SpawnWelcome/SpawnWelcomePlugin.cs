using Basalt.Server.Events;
using Basalt.Server.Plugins;
using Player = Basalt.Server.Player.Player;

[assembly: Plugin("SpawnWelcome", "1.0.0", Authors = ["Basalt"])]

namespace Basalt.Samples.Plugins.SpawnWelcome;

/// <summary>
/// World-bound example: handler runs on the owning worker via Server.Emit affinity.
/// </summary>
public sealed class SpawnWelcomePlugin : Plugin
{
    public override void OnStart()
    {
        Listen<PlayerSpawnSignal>(ServerEvent.PlayerSpawn, HandleSpawn);
    }

    void HandleSpawn(PlayerSpawnSignal signal)
    {
        Player player = signal.Player;
        string worldName = player.Dimension?.World?.Name ?? "unknown";
        int? workerId = player.Dimension?.World?.AttachedWorkerId;

        player.SendMessage(
            $"§7[SpawnWelcome] world=§a{worldName}§7 worker=§a{workerId?.ToString() ?? "n/a"}");

        Log(
            $"spawn user={player.Username} world={worldName} worker={workerId?.ToString() ?? "n/a"} thread={Environment.CurrentManagedThreadId}");
    }
}

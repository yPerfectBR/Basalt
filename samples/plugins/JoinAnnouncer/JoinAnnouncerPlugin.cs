using Basalt.Server.Events;
using Basalt.Server.Plugins;

[assembly: Plugin("JoinAnnouncer", "1.0.0", Authors = ["Basalt"])]

namespace Basalt.Samples.Plugins.JoinAnnouncer;

/// <summary>
/// Global-thread example: logs joins without touching world simulation.
/// </summary>
public sealed class JoinAnnouncerPlugin : Plugin
{
    public override void OnStart()
    {
        Listen<PlayerJoinSignal>(ServerEvent.PlayerJoin, HandleJoin);
    }

    void HandleJoin(PlayerJoinSignal signal)
    {
        string dimensionState = signal.Player.Dimension is null ? "none" : "set";
        Log(
            $"join user={signal.Player.Username} thread={Environment.CurrentManagedThreadId} dimension={dimensionState}");
    }
}

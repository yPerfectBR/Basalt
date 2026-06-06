using Basalt.Protocol.Types;
using Basalt.Server.Block;
using Basalt.Server.Events;
using Basalt.Server.Plugins;
using Basalt.Server.World.Dimension;
using Player = Basalt.Server.Player.Player;

[assembly: Plugin("BreakGuard", "1.0.0", Authors = ["Basalt"])]

namespace Basalt.Samples.Plugins.BreakGuard;

/// <summary>
/// World-bound example: cancels block breaks synchronously on the worker thread.
/// </summary>
public sealed class BreakGuardPlugin : Plugin
{
    public override void OnStart()
    {
        Listen<PlayerBreakBlockSignal>(ServerEvent.PlayerBreakBlock, HandleBreak);
    }

    void HandleBreak(PlayerBreakBlockSignal signal)
    {
        Player player = signal.Player;
        Dimension? dimension = player.Dimension;
        if (dimension is null)
        {
            return;
        }
        BlockPos position = signal.BlockPosition;
        string worldName = dimension.World?.Name ?? "unknown";
        BlockPermutation permutation = dimension.GetPermutation(position.X, position.Y, position.Z);

        if (position.Y < -60 || string.Equals(permutation.Type.Identifier, "minecraft:bedrock", StringComparison.Ordinal))
        {
            signal.Cancel();
            player.SendMessage("§c[BreakGuard] This block is protected.");
            Log(
                $"cancelled break user={player.Username} world={worldName} block={permutation.Type.Identifier} pos={position.X},{position.Y},{position.Z} thread={Environment.CurrentManagedThreadId}");
        }
    }
}

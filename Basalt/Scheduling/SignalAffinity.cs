namespace Basalt.Server.Scheduling;

using Basalt.Server.Events;
using WorldInstance = Basalt.Server.World.World;

/// <summary>
/// Resolves thread affinity for server events (Phase 5).
/// </summary>
internal static class SignalAffinity
{
    public static bool IsGlobalEvent(ISignal signal)
    {
        return signal.Event is ServerEvent.ServerStart or ServerEvent.PlayerJoin;
    }

    public static WorldInstance? TryResolveWorld(Server server, ISignal signal)
    {
        switch (signal)
        {
            case PlayerSignal playerSignal:
                if (playerSignal.Player.Dimension?.World is WorldInstance playerWorld)
                {
                    return playerWorld;
                }

                return playerSignal.Event == ServerEvent.PlayerSpawn ? server.GetWorld() : null;

            case EntityHurtSignal hurtSignal:
                return hurtSignal.Entity.Dimension?.World as WorldInstance;

            case EntitySpawnSignal spawnSignal:
                return spawnSignal.Entity.Dimension?.World as WorldInstance;

            case EntityDieSignal dieSignal:
                return dieSignal.Entity.Dimension?.World as WorldInstance;

            default:
                return null;
        }
    }
}

namespace Basalt.Server.Player;

using Basalt.Protocol.Enums;
using Basalt.Protocol.Nbt;
using Basalt.Protocol.Types;
using Basalt.Server.Scheduling;
using Basalt.Server.World.Dimension;
using WorldInstance = Basalt.Server.World.World;

/// <summary>
/// Per-world player persistence and cross-world transfer state assembly.
/// Game state lives in each world's LevelDB; only connection data stays on <see cref="PlayerSession"/>.
/// </summary>
public static class PlayerWorldTransfer
{
    public static readonly Vec3f DefaultSpawn = new() { X = 0f, Y = -57f, Z = 0f };

    public readonly record struct PlayerTransform(Vec3f Position, float Pitch, float Yaw, float HeadYaw);

    public static void SaveToWorld(Player player, WorldInstance world)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(world);
        world.Provider.SavePlayerData(player.Xuid, player.WriteToNbt());
    }

    public static CompoundTag? LoadFromWorld(string xuid, WorldInstance world)
    {
        ArgumentException.ThrowIfNullOrEmpty(xuid);
        ArgumentNullException.ThrowIfNull(world);
        return world.Provider.LoadPlayerData(xuid);
    }

    public static bool IsCrossWorld(WorldInstance sourceWorld, WorldInstance targetWorld)
    {
        ArgumentNullException.ThrowIfNull(sourceWorld);
        ArgumentNullException.ThrowIfNull(targetWorld);

        return !ReferenceEquals(sourceWorld, targetWorld)
            && !string.Equals(sourceWorld.Name, targetWorld.Name, StringComparison.OrdinalIgnoreCase);
    }

    public static PlayerTransform ResolveDestinationTransform(
        Player source,
        WorldInstance targetWorld,
        Vec3f? explicitCoords,
        TransferCarryFlags carryFlags)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(targetWorld);

        if (explicitCoords.HasValue)
        {
            return new PlayerTransform(explicitCoords.Value, source.Pitch, source.Yaw, source.HeadYaw);
        }

        if (carryFlags.HasFlag(TransferCarryFlags.Position))
        {
            return new PlayerTransform(source.Position, source.Pitch, source.Yaw, source.HeadYaw);
        }

        CompoundTag? saved = LoadFromWorld(source.Xuid, targetWorld);
        if (saved is not null)
        {
            Vec3f position = new()
            {
                X = saved.Get<FloatTag>("x")?.Value ?? DefaultSpawn.X,
                Y = saved.Get<FloatTag>("y")?.Value ?? DefaultSpawn.Y,
                Z = saved.Get<FloatTag>("z")?.Value ?? DefaultSpawn.Z
            };

            return new PlayerTransform(position, 0f, 0f, 0f);
        }

        return new PlayerTransform(DefaultSpawn, 0f, 0f, 0f);
    }

    public static CompoundTag BuildEntityNbt(
        Player sourcePlayer,
        WorldInstance sourceWorld,
        WorldInstance targetWorld,
        TransferCarryFlags carryFlags)
    {
        SaveToWorld(sourcePlayer, sourceWorld);
        return BuildEntityNbtFromSnapshot(sourcePlayer.WriteToNbt(), targetWorld, sourcePlayer.Xuid, carryFlags);
    }

    public static CompoundTag BuildEntityNbtFromSnapshot(
        CompoundTag sourceEntityNbt,
        WorldInstance targetWorld,
        string xuid,
        TransferCarryFlags carryFlags)
    {
        ArgumentNullException.ThrowIfNull(sourceEntityNbt);
        ArgumentNullException.ThrowIfNull(targetWorld);
        ArgumentException.ThrowIfNullOrEmpty(xuid);

        CompoundTag baseNbt = LoadFromWorld(xuid, targetWorld) ?? CreateMinimalFromSourceIdentity(sourceEntityNbt);

        if (carryFlags.HasFlag(TransferCarryFlags.Inventory))
        {
            PlayerNbtMerge.ApplyInventory(baseNbt, sourceEntityNbt);
        }

        return baseNbt;
    }

    public static void ApplySameWorker(
        global::Basalt.Server.Server server,
        Player player,
        WorldInstance targetWorld,
        Dimension targetDimension,
        PlayerTransform transform,
        TransferCarryFlags carryFlags)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(targetWorld);
        ArgumentNullException.ThrowIfNull(targetDimension);

        if (player.Dimension?.World is not WorldInstance sourceWorld)
        {
            throw new InvalidOperationException("Player has no active world.");
        }

        if (!IsCrossWorld(sourceWorld, targetWorld))
        {
            player.Teleport(transform.Position, targetDimension);
            return;
        }

        CompoundTag entityNbt = BuildEntityNbt(player, sourceWorld, targetWorld, carryFlags);
        DimensionType sourceDimensionType = player.Dimension.Type;
        bool useDimensionChange = sourceDimensionType != targetDimension.Type;

        WorldPlayerPresence.OnPlayerLeftWorld(server, sourceWorld);

        player.FromNBT(entityNbt);
        player.Position = transform.Position;
        player.Pitch = transform.Pitch;
        player.Yaw = transform.Yaw;
        player.HeadYaw = transform.HeadYaw;

        player.Teleport(transform.Position, targetDimension, forceDimensionChange: false);
        WorldPlayerPresence.OnPlayerEnteredWorld(server, targetWorld);
        player.ResyncAfterWorldTransfer(useDimensionChange);
    }

    static CompoundTag CreateMinimalFromSourceIdentity(CompoundTag sourceEntityNbt)
    {
        string username = sourceEntityNbt.Get<StringTag>("username")?.Value ?? "Player";
        string xuid = sourceEntityNbt.Get<StringTag>("xuid")?.Value ?? string.Empty;
        string uuidText = sourceEntityNbt.Get<StringTag>("uuid")?.Value ?? Guid.Empty.ToString();
        Guid uuid = Guid.TryParse(uuidText, out Guid parsed) ? parsed : Guid.Empty;

        Player player = new(username, xuid, uuid);
        return player.WriteToNbt();
    }
}

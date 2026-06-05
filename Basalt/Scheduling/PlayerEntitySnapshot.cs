namespace Basalt.Server.Scheduling;

using Basalt.Protocol.Enums;
using Basalt.Protocol.Nbt;
using Basalt.Protocol.Types;

/// <summary>
/// Serializable player state for cross-worker transfer.
/// </summary>
public sealed class PlayerEntitySnapshot
{
    public required string Username { get; init; }
    public required string Xuid { get; init; }
    public required Guid Uuid { get; init; }
    public required Vec3f Position { get; init; }
    public required string SourceWorldId { get; init; }
    public required string TargetWorldId { get; init; }
    public required string TargetDimensionId { get; init; }
    public float Pitch { get; init; }
    public float Yaw { get; init; }
    public float HeadYaw { get; init; }
    public Gamemode Gamemode { get; init; }
    public required CompoundTag EntityNbt { get; init; }
}

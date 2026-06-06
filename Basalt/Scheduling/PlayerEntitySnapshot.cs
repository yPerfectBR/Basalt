namespace Basalt.Server.Scheduling;

using Basalt.Protocol.Enums;
using Basalt.Protocol.Nbt;
using Basalt.Protocol.Types;
using Basalt.Server.Player;

/// <summary>
/// Serializable player state for cross-worker transfer.
/// Game state is loaded from the target world's LevelDB; <see cref="SourceEntityNbt"/> is used only for optional carry merges.
/// </summary>
public sealed class PlayerEntitySnapshot
{
    public required string Username { get; init; }
    public required string Xuid { get; init; }
    public required Guid Uuid { get; init; }
    public required Vec3f Position { get; init; }
    public required string SourceWorldId { get; init; }
    public DimensionType SourceDimensionType { get; init; }
    public ulong RuntimeId { get; init; }
    public required string TargetWorldId { get; init; }
    public required string TargetDimensionId { get; init; }
    public float Pitch { get; init; }
    public float Yaw { get; init; }
    public float HeadYaw { get; init; }
    public TransferCarryFlags CarryFlags { get; init; }
    public required CompoundTag SourceEntityNbt { get; init; }
}

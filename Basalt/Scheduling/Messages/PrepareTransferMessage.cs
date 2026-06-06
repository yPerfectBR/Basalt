namespace Basalt.Server.Scheduling.Messages;

using Basalt.Protocol.Types;
using Basalt.Server.Player;
using WorldInstance = Basalt.Server.World.World;

/// <summary>
/// Captures entity state on the source worker and hands off to the target worker.
/// </summary>
public sealed class PrepareTransferMessage : IWorldMessage
{
    public required PlayerSession Session { get; init; }
    public required WorldInstance TargetWorld { get; init; }
    public required string TargetDimensionId { get; init; }
    public required Vec3f Position { get; init; }
    public float Pitch { get; init; }
    public float Yaw { get; init; }
    public float HeadYaw { get; init; }
    public TransferCarryFlags CarryFlags { get; init; }
}

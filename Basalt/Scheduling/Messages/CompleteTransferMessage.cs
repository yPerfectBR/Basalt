namespace Basalt.Server.Scheduling.Messages;

using Basalt.Server.Player;

/// <summary>
/// Spawns the player from a snapshot on the target worker.
/// </summary>
public sealed class CompleteTransferMessage : IWorldMessage
{
    public required PlayerSession Session { get; init; }
    public required PlayerEntitySnapshot Snapshot { get; init; }
}

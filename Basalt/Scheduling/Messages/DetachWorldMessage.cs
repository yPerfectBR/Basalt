namespace Basalt.Server.Scheduling.Messages;

using WorldInstance = Basalt.Server.World.World;

/// <summary>
/// Detaches a world from a worker when the last player leaves.
/// </summary>
public sealed class DetachWorldMessage : IWorldMessage
{
    public required WorldInstance World { get; init; }
}

namespace Basalt.Server.Scheduling.Messages;

using WorldInstance = Basalt.Server.World.World;

/// <summary>
/// Attaches a world to a worker when the first player enters.
/// </summary>
public sealed class AttachWorldMessage : IWorldMessage
{
    public required WorldInstance World { get; init; }
}

namespace Basalt.Server.Scheduling.Messages;

using Basalt.RakNet;

/// <summary>
/// Client disconnect deferred from the network thread to the simulation thread.
/// </summary>
public sealed class ProcessDisconnectMessage : IWorldMessage
{
    public required NetworkConnection Connection { get; init; }
}

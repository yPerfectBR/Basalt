namespace Basalt.Server.Scheduling.Messages;

using Basalt.Protocol.Enums;
using Basalt.RakNet;

/// <summary>
/// Game packet deferred from the network thread to the simulation thread.
/// </summary>
public sealed class ProcessPacketMessage : IWorldMessage
{
    public required NetworkConnection Connection { get; init; }
    public required PacketId PacketId { get; init; }
    public required byte[] Payload { get; init; }
}

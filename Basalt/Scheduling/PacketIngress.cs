namespace Basalt.Server.Scheduling;

using Basalt.Protocol.Enums;
using Basalt.RakNet;

/// <summary>
/// Routes incoming game packets to the network thread (global) or simulation queue (world-bound).
/// </summary>
public sealed class PacketIngress
{
    private readonly Server _server;

    public PacketIngress(Server server)
    {
        _server = server;
    }

    public void Route(NetworkConnection connection, PacketId packetId, ReadOnlySpan<byte> payload)
    {
        if (IsGlobalPacket(packetId))
        {
            if (_server.Properties.WorldSchedulerDebug)
            {
                Logger.Debug("[PacketIngress] inline packet={0}", packetId);
            }

            _server.Network.HandleGamePacketOnWorker(connection, packetId, payload);
            return;
        }

        if (!_server.Sessions.ContainsKey(connection))
        {
            return;
        }

        if (_server.Properties.WorldSchedulerDebug)
        {
            Logger.Debug("[PacketIngress] enqueue packet={0}", packetId);
        }

        _server.Scheduler.EnqueueGamePacket(connection, packetId, payload);
    }

    internal static bool IsGlobalPacket(PacketId packetId)
    {
        return packetId is PacketId.Login or PacketId.RequestNetworkSettings;
    }
}

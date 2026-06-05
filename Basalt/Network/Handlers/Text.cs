namespace Basalt.Server.Network.Handlers;

using Basalt.Server;
using Basalt.Server.Events;
using Basalt.Server.Player;
using Basalt.Protocol.Packets;
using Basalt.RakNet;


public static class Text
{
    public static void Handle(Server server, NetworkConnection connection, ReadOnlySpan<byte> packetBuffer)
    {
        TextPacket packet = new();
        int offset = 0;
        Binary.BinaryReader reader = new(packetBuffer, ref offset);
        packet = (TextPacket)Protocol.Io.Packet.Deserialize(reader);

        if (!SessionLookup.TryGetPlayer(server, connection, out global::Basalt.Server.Player.Player? sender))
        {
            Logger.Warn("Text received for unknown player session.");
            return;
        }

        string rawMessage = packet.Variant.Message;
        string message = $"<{sender.Username}> {rawMessage}";
        PlayerChatSignal signal = new(sender, rawMessage, message);
        server.Emit(signal);
        if (!signal.Emit())
        {
            return;
        }

        foreach (PlayerSession session in server.Sessions.Values)
        {
            session.SendMessage(signal.Message);
        }
    }
}










namespace Basalt.Server.Player;

using Basalt.Protocol.Enums;
using Basalt.Protocol.Packets;
using Basalt.Protocol.Types;
using Basalt.RakNet;
using Basalt.Server.Network;

/// <summary>
/// Stable server-level player session (connection, identity, permissions).
/// Survives entity despawn during cross-worker transfers.
/// </summary>
public sealed class PlayerSession
{
    public required NetworkConnection Connection { get; init; }
    public required NetworkHandler Network { get; init; }

    public required string Username { get; init; }
    public required string Xuid { get; init; }
    public required Guid Uuid { get; init; }

    public DeviceOS DeviceOS { get; set; }
    public Skin Skin { get; set; } = new();

    /// <summary>World entity when spawned; null during login or transfer.</summary>
    public Player? ActiveEntity { get; set; }

    public TransferState TransferState { get; set; } = TransferState.Idle;

    public HashSet<string> Permissions { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool IsOperator { get; private set; }

    public void SetOperator(bool isOperator)
    {
        IsOperator = isOperator;
        if (isOperator)
        {
            Permissions.Add("basalt.op");
        }
        else
        {
            Permissions.Remove("basalt.op");
        }
    }

    public bool HasPermission(string permission)
    {
        return Permissions.Contains(permission);
    }

    public void Send(DataPacket packet)
    {
        Network.SendPacket(Connection, packet);
    }

    public void Send(params DataPacket[] packets)
    {
        if (packets.Length == 0)
        {
            return;
        }

        Network.SendPackets(Connection, packets);
    }

    public void SendMessage(string message)
    {
        Send(new TextPacket
        {
            VariantType = TextVariantType.MessageOnly,
            FilteredMessage = null,
            NeedsTranslation = false,
            Xuid = string.Empty,
            PlatformChatId = string.Empty,
            Variant = new TextVariant
            {
                Message = message,
                Parameters = [],
                Source = string.Empty,
                Type = TextType.Raw
            }
        });
    }

    public void Disconnect(string reason = "")
    {
        DisconnectPacket disconnect = new()
        {
            Reason = string.IsNullOrEmpty(reason) ? DisconnectReason.Disconnected : DisconnectReason.NetherNetSignalingSigninFailed,
            HideDisconnectionScreen = string.IsNullOrEmpty(reason),
            Message = reason,
            FilteredMessage = string.Empty
        };

        Network.SendPacket(Connection, disconnect, immediate: true);
        Connection.Disconnect();
    }
}

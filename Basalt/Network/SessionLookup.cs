namespace Basalt.Server.Network.Handlers;

using Basalt.RakNet;
using Basalt.Server.Player;

/// <summary>
/// Resolves online player entities from sessions for network handlers.
/// </summary>
internal static class SessionLookup
{
    public static bool TryGetPlayer(Server server, NetworkConnection connection, out Player player)
    {
        player = null!;
        if (!server.Sessions.TryGetValue(connection, out PlayerSession? session))
        {
            return false;
        }

        if (session.TransferState == TransferState.Transferring)
        {
            return false;
        }

        if (session.ActiveEntity is not Player entity)
        {
            return false;
        }

        player = entity;
        return true;
    }

    public static bool TryGetSession(Server server, NetworkConnection connection, out PlayerSession session)
    {
        return server.Sessions.TryGetValue(connection, out session!);
    }
}

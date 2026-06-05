namespace Basalt.Server.Player;

using System.Collections;
using Basalt.RakNet;

/// <summary>
/// Read-only view of spawned player entities keyed by connection (Phase 2 compatibility).
/// </summary>
public sealed class LegacyPlayersAdapter : IReadOnlyDictionary<NetworkConnection, Player>
{
    private readonly Server _server;

    internal LegacyPlayersAdapter(Server server)
    {
        _server = server;
    }

    public Player this[NetworkConnection key]
    {
        get
        {
            if (!TryGetValue(key, out Player? player))
            {
                throw new KeyNotFoundException();
            }

            return player;
        }
    }

    public IEnumerable<NetworkConnection> Keys =>
        _server.Sessions.Values
            .Where(static session => session.ActiveEntity is not null)
            .Select(static session => session.Connection);

    public IEnumerable<Player> Values =>
        _server.Sessions.Values
            .Select(static session => session.ActiveEntity)
            .OfType<Player>();

    public int Count =>
        _server.Sessions.Values.Count(static session => session.ActiveEntity is not null);

    public bool ContainsKey(NetworkConnection key) => TryGetValue(key, out _);

    public bool TryGetValue(NetworkConnection key, out Player value)
    {
        value = null!;
        if (!_server.Sessions.TryGetValue(key, out PlayerSession? session) || session.ActiveEntity is not Player player)
        {
            return false;
        }

        value = player;
        return true;
    }

    public IEnumerator<KeyValuePair<NetworkConnection, Player>> GetEnumerator()
    {
        foreach (PlayerSession session in _server.Sessions.Values)
        {
            if (session.ActiveEntity is not Player player)
            {
                continue;
            }

            yield return new KeyValuePair<NetworkConnection, Player>(session.Connection, player);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

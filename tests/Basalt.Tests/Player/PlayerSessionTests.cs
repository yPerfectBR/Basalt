namespace Basalt.Tests.Player;

using Basalt.RakNet;
using Basalt.Server;
using Basalt.Server.Player;
using Basalt.Tests.Support;

public sealed class PlayerSessionTests
{
    [Fact]
    public void Session_SurvivesEntityDespawn()
    {
        Server server = TestServerFactory.CreateMemoryServer();
        TestNetworkConnection connection = new();
        Player player = new("Steve", "12345", Guid.NewGuid());
        PlayerSession session = new()
        {
            Connection = connection,
            Network = server.Network,
            Username = "Steve",
            Xuid = "12345",
            Uuid = player.Uuid,
            ActiveEntity = player
        };
        player.Session = session;
        server.Sessions[connection] = session;

        session.ActiveEntity = null;

        Assert.True(server.Sessions.ContainsKey(connection));
        Assert.Null(session.ActiveEntity);
    }

    [Fact]
    public void NpcPlayer_HasNoSession()
    {
        Player npc = new("Guard", string.Empty, Guid.NewGuid());

        Assert.Null(npc.Session);
        Assert.False(npc.IsOnline);
    }

    [Fact]
    public void LegacyPlayersAdapter_MatchesSessions()
    {
        Server server = TestServerFactory.CreateMemoryServer();
        TestNetworkConnection connection = new();
        Player player = new("Alex", "67890", Guid.NewGuid());
        PlayerSession session = new()
        {
            Connection = connection,
            Network = server.Network,
            Username = "Alex",
            Xuid = "67890",
            Uuid = player.Uuid,
            ActiveEntity = player
        };
        player.Session = session;
        server.Sessions[connection] = session;

#pragma warning disable CS0618
        IReadOnlyDictionary<NetworkConnection, Player> legacyPlayers = server.Players;
#pragma warning restore CS0618

        Assert.Equal(1, legacyPlayers.Count);
        Assert.True(legacyPlayers.TryGetValue(connection, out Player? resolved));
        Assert.Same(player, resolved);

        session.ActiveEntity = null;

        Assert.Equal(0, legacyPlayers.Count);
        Assert.False(legacyPlayers.ContainsKey(connection));
    }
}

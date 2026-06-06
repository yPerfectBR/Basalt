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
}

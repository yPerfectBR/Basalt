namespace Basalt.Tests.Scheduling;

using Basalt.Protocol.Enums;
using Basalt.Server;
using Basalt.Server.Player;
using Basalt.Server.Scheduling;
using Basalt.Tests.Support;

public sealed class PacketIngressTests
{
    [Fact]
    public void PacketIngress_GlobalHandler_DoesNotRequireEntity()
    {
        Assert.True(PacketIngress.IsGlobalPacket(PacketId.Login));
        Assert.True(PacketIngress.IsGlobalPacket(PacketId.RequestNetworkSettings));
        Assert.False(PacketIngress.IsGlobalPacket(PacketId.PlayerAuthInput));

        Server server = TestServerFactory.CreateMemoryServer();
        SingleThreadScheduler scheduler = Assert.IsType<SingleThreadScheduler>(server.Scheduler);
        PacketIngress ingress = new(server);
        TestNetworkConnection connection = new();

        ingress.Route(connection, PacketId.PlayerAuthInput, []);

        Assert.Equal(0, scheduler.PendingMessageCount);
    }

    [Fact]
    public void PacketIngress_WorldBoundHandler_EnqueuesWhenPlayerPresent()
    {
        Server server = TestServerFactory.CreateMemoryServer();
        SingleThreadScheduler scheduler = Assert.IsType<SingleThreadScheduler>(server.Scheduler);
        PacketIngress ingress = new(server);
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

        ingress.Route(connection, PacketId.PlayerAuthInput, []);

        Assert.Equal(1, scheduler.PendingMessageCount);
    }

    [Fact]
    public void ProcessPacketMessage_ProcessedOnMainThread()
    {
        Server server = TestServerFactory.CreateMemoryServer();
        SingleThreadScheduler scheduler = Assert.IsType<SingleThreadScheduler>(server.Scheduler);
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

        Thread drainThread = new(() => scheduler.DrainMainQueue())
        {
            Name = "test-drain-thread"
        };

        scheduler.EnqueueDisconnect(connection);
        drainThread.Start();
        drainThread.Join();

        Assert.Same(drainThread, scheduler.LastMessageProcessedOnThread);
    }
}

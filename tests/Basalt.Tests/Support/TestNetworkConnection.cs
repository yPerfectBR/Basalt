namespace Basalt.Tests.Support;

using Basalt.RakNet;

internal sealed class TestNetworkConnection : NetworkConnection
{
    protected override void SendMessage(ReadOnlySpan<byte> raw)
    {
    }
}

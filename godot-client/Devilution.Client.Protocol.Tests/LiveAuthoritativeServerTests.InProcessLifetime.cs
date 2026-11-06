using System.Net;
using System.Net.Sockets;
using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed partial class LiveAuthoritativeServerTests
{
    [Fact]
    public async Task DefaultLiveHostUsesTcpWithoutAChildProcessAndReleasesItsResources()
    {
        using var server = await StartedServer.StartAsync(TestContext.Current.CancellationToken);
        Assert.False(server.IsProcess);
        Assert.Throws<InvalidOperationException>(() => server.ProcessId);
        var root = server.SaveRoot;
        var port = server.Port;
        Assert.True(Directory.Exists(root));
        await using (var client = new AuthoritativeClient(CreateOptions(server, "in-process-lifetime", 1))) {
            await client.ConnectAsync(TestContext.Current.CancellationToken);
            var model = new AuthoritativeClientModel();
            model.Apply(await WaitForMessageAsync(client, message => message.Snapshot is not null));
            Assert.Single(model.Snapshot!.Players);
        }
        server.Dispose();
        Assert.True(server.HasExited);
        Assert.False(Directory.Exists(root));
        using var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        Assert.Equal(port, ((IPEndPoint)listener.LocalEndpoint).Port);
    }
}

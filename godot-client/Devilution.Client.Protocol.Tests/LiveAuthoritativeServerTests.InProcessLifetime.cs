using Devilution.Protocol.V1;
using System.Net;
using System.Net.Sockets;
using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class LiveAuthoritativeInProcessLifetimeTests : LiveAuthoritativeTestBase
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
    [Fact]
    public async Task ControlledCombatClockDoesNotAdvanceDuringConnectionSetup()
    {
        using var server = await StartedServer.StartAsync(
            TestContext.Current.CancellationToken, startingLevelOverride: 0, controlledClock: true);
        Assert.True(server.IsClockHeld);
        await using var first = new AuthoritativeClient(CreateOptions(server, "controlled-first", 0));
        await first.ConnectAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0UL, (await WaitForMessageAsync(first, message => message.Snapshot is not null)).Snapshot!.Tick);
        await using var second = new AuthoritativeClient(CreateOptions(server, "controlled-second", 1));
        await second.ConnectAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0UL, (await WaitForMessageAsync(second, message => message.Snapshot is { Players.Count: 2 })).Snapshot!.Tick);
        Assert.Equal(0UL, server.CurrentTick);
        server.AdvanceObservedClock(42);
        var advanced = await WaitForMessageAsync(first, message => message.Snapshot?.Tick == 42);
        Assert.Equal(2, advanced.Snapshot!.Players.Count);
        Assert.Equal(42UL, server.CurrentTick);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LiveHostsUseTwoHundredTicksByDefault(bool useProcess)
    {
        using var server = await StartedServer.StartAsync(TestContext.Current.CancellationToken, useProcess: useProcess);
        var options = CreateOptions(server, "live-default-rate", 1);
        Assert.Equal(TimeSpan.FromSeconds(2), options.MaximumRetryTimeout);
        Assert.Equal(TimeSpan.FromSeconds(2), new ClientConnectionOptions("127.0.0.1", 1, "defaults", "0.1.0", "content").MaximumRetryTimeout);
        await using var client = new AuthoritativeClient(options);
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        Assert.Equal(200U, client.ServerTickRateHz);
        Assert.Equal(200U, client.ServerIdentity!.TickRateHz);
    }

    [Fact]
    public async Task ProcessHostHonorsTheWarmupOptOutBeforePublishingItsIdentity()
    {
        using var server = await StartedServer.StartAsync(TestContext.Current.CancellationToken, useProcess: true);
        Assert.Contains("Floor entry warm-up skipped by host option.", server.ProcessDiagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SimultaneousHostsKeepEntityAllocationAndWorldMutationsIndependent()
    {
        using var first = await StartedServer.StartAsync(TestContext.Current.CancellationToken, startingLevelOverride: 0, randomSeed: 1);
        using var second = await StartedServer.StartAsync(TestContext.Current.CancellationToken, startingLevelOverride: 0, randomSeed: 1);
        Assert.NotEqual(first.Port, second.Port);
        Assert.NotEqual(first.SaveRoot, second.SaveRoot);
        await using var firstClient = new AuthoritativeClient(CreateOptions(first, "isolated-first", 1));
        await using var secondClient = new AuthoritativeClient(CreateOptions(second, "isolated-second", 1));
        await firstClient.ConnectAsync(TestContext.Current.CancellationToken);
        await secondClient.ConnectAsync(TestContext.Current.CancellationToken);
        var firstInitial = (await WaitForMessageAsync(firstClient, message => message.Snapshot is not null)).Snapshot!;
        var secondInitial = (await WaitForMessageAsync(secondClient, message => message.Snapshot is not null)).Snapshot!;
        var firstPlayer = Assert.Single(firstInitial.Players);
        var secondPlayer = Assert.Single(secondInitial.Players);
        Assert.Equal(firstPlayer.EntityId, secondPlayer.EntityId);
        Assert.NotEqual(firstClient.HostInstanceId, secondClient.HostInstanceId);
        var sequence = firstClient.Queue(new Command {
            MoveRequested = new MoveRequested { DirectionY = 1 },
        }, firstClient.SuggestedCommandTick(firstInitial.Tick));
        await firstClient.PollAsync(TestContext.Current.CancellationToken);
        Assert.Equal(CommandStatus.Accepted, (await WaitForAcknowledgementAsync(firstClient, sequence)).Status);
        await WaitForMessageAsync(firstClient, message => message.Snapshot?.Players.Single().PositionY == firstPlayer.PositionY + 1);
        var untouched = (await WaitForMessageAsync(secondClient, message => message.Snapshot is { } current
            && current.Tick > secondInitial.Tick)).Snapshot!.Players.Single();
        Assert.Equal((secondPlayer.PositionX, secondPlayer.PositionY), (untouched.PositionX, untouched.PositionY));
        Assert.Equal(secondPlayer.Belt, untouched.Belt);
        Assert.Equal(secondPlayer.Gold, untouched.Gold);
    }

}

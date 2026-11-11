using System.Net;
using System.Net.Sockets;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class TooLateLiveHostTests : LiveAuthoritativeTestBase
{
    [Theory]
    [InlineData(20U)] [InlineData(200U)]
    [Trait("Category", "Slow")]
    public async Task LiveClockGatedHostAcceptsTheFreshRetryAndKeepsItsReceiptForDuplicateDelivery(uint rate)
    {
        using var server = await StartedServer.StartAsync(TestContext.Current.CancellationToken,
            startingLevelOverride: 0, controlledClock: true, tickRateHz: rate, commandSchedulingMode: CommandSchedulingMode.ClockGated);
        var tick = server.CurrentTick + 100; server.AdvanceObservedClock(tick);
        var options = CreateOptions(server, "too-late-live", 0) with { TimeProvider = new ManualTimeProvider() };
        var timing = new AuthoritativeClientCommandTimingRecorder();
        await using var client = new AuthoritativeClient(options) { CommandTimingRecorder = timing };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        await client.ConnectAsync(deadline.Token);
        var model = new AuthoritativeClientModel();
        var messages = new List<AuthoritativeClientMessage>();
        async Task Until(Func<bool> ready)
        {
            while (true) {
                deadline.Token.ThrowIfCancellationRequested();
                foreach (var message in client.DrainMessages()) {
                    Assert.Null(message.Error); messages.Add(message); client.ApplyMessageToModel(model, message);
                }
                if (ready()) return;
                await client.PollAsync(deadline.Token);
                await Task.Delay(1, deadline.Token);
            }
        }
        await Until(() => model.Snapshot is not null);
        var original = new Command { CloseStoreRequested = new() };
        var root = client.Queue(original, tick - 6); // Beyond the real host's two-tick UI window.
        await Until(() => timing.CaptureReport().Commands.Count == 2 && server.PendingCommandCount == 1);
        var sends = timing.CaptureReport().Commands;
        Assert.Equal(root, sends[0].ClientSequence); Assert.Equal(tick - 6, sends[0].RequestedTick);
        var retry = sends[1];
        Assert.True(retry.ClientSequence > root); Assert.True(retry.RequestedTick > tick);
        Assert.Equal(tick - 6, original.RequestedTick);
        var refused = Assert.Single(messages.SelectMany(message => message.Acknowledgement?.Results ?? [])
            .Where(result => result.ClientSequence == root));
        Assert.Equal(CommandStatus.Rejected, refused.Status); Assert.Equal(CommandRejectReason.TooLate, refused.RejectReason);
        Assert.True(refused.ServerReceiptSequence > 0);
        Assert.Null(model.LastCommandResult);
        server.AdvanceObservedClock(retry.RequestedTick);
        await Until(() => model.LastCommandWasApplied && client.PendingCommandCount == 0
            && model.Snapshot!.Tick >= model.LastCommandResult!.AppliedTick);
        Assert.Equal(CommandStatus.Accepted, model.LastCommandResult!.Status);
        Assert.Equal(root, model.LastCommandIntentSequence);
        Assert.Equal(retry.ClientSequence, model.LastCommandResult.ClientSequence);
        var accepted = model.LastCommandResult.Clone();
        Assert.Single(messages.Where(message => message.CommandRetry.HasValue));
        Assert.Null(model.Snapshot!.ActiveStore);
        var token = client.SessionToken;
        await client.DisconnectAsync();
        using var duplicate = new TcpClient();
        await duplicate.ConnectAsync(IPAddress.Loopback, server.Port, deadline.Token);
        await using var stream = duplicate.GetStream();
        await TooLateRetryPeer.WriteAsync(stream, new() { ClientHello = new() {
            ClientBuildId = options.ClientBuildId, ProtocolSchemaVersion = options.ProtocolSchemaVersion,
            ContentManifestHash = options.ContentManifestHash, RulesetIdentityHash = options.RulesetIdentityHash,
            ResumeToken = token, SupportsClockGatedCommands = true,
        } }, deadline.Token);
        Assert.NotNull((await TooLateRetryPeer.ReadAsync(stream, deadline.Token)).ServerHello);
        Assert.NotNull((await TooLateRetryPeer.ReadAsync(stream, deadline.Token)).Snapshot);
        await TooLateRetryPeer.WriteAsync(stream, new() { CommandBatch = new() { Commands = {
            new Command { ClientSequence = retry.ClientSequence, RequestedTick = retry.RequestedTick, CloseStoreRequested = new() },
        } } }, deadline.Token);
        var replayed = Assert.Single((await TooLateRetryPeer.ReadAsync(stream, deadline.Token)).CommandAck.Results);
        Assert.Equal(CommandStatus.Duplicate, replayed.Status);
        Assert.Equal(accepted.ServerReceiptSequence, replayed.ServerReceiptSequence);
        Assert.Equal(accepted.AppliedTick, replayed.AppliedTick);
        Assert.Equal(CommandRejectReason.Unspecified, replayed.RejectReason);
    }
}

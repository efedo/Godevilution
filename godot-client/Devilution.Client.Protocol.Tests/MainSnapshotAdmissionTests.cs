using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed partial class MainSnapshotAdmissionTests
{
    [Theory]
    [InlineData(CommandStatus.Accepted)] [InlineData(CommandStatus.Rescheduled)]
    public async Task RejectedSnapshotDoesNotLoseReceiptsAlreadyRemovedFromThePendingLedger(CommandStatus status)
    {
        await using var peer = new RecoveryPeer(rejectRecovery: false, batchReceipt: status);
        peer.Start();
        var time = new ManualTimeProvider();
        await using var scene = new Main();
        scene.SetReconnectTime(time);
        var client = new AuthoritativeClient(peer.Options);
        await scene.ConnectAdmissionSceneAsync(client, peer.Options);
        Assert.True(scene.PumpAdmissionScene());
        var results = new List<CommandResult>();
        scene.AcceptanceAcknowledgementObserver = ack => results.AddRange(ack.Results);
        var sequence = client.Queue(new Command { OpenStoreRequested = new() { StoreId = 1 } }, requestedTick: 12);
        await client.PollAsync(TestContext.Current.CancellationToken);
        var batch = new List<AuthoritativeClientMessage>();
        await UntilAsync(() => { batch.AddRange(client.DrainMessages()); return batch.Any(message => message.Acknowledgement is not null); });
        Assert.Equal(0, client.PendingCommandCount);
        Assert.Equal(2, batch.Count(message => message.Snapshot is not null));
        Assert.False(scene.PumpAdmissionScene(batch));
        Assert.False(client.IsConnected);
        Assert.Equal(1UL, scene.AdmissionPublishedSnapshotCount); // Only the initial, valid publication was exposed.
        var received = Assert.IsType<CommandResult>(scene.AdmissionModel.LastCommandResult);
        Assert.Equal(sequence, received.ClientSequence);
        Assert.Equal(status, received.Status);
        Assert.True(scene.AdmissionModel.LastCommandWasApplied); // Includes frozen Rescheduled command context.
        Assert.Equal(sequence, Assert.Single(results).ClientSequence);

        Assert.Equal(1, peer.Handshakes);
        Assert.Equal(TimeSpan.FromMilliseconds(250), time.NextTimerDelay);
        time.AdvanceMilliseconds(249);
        Assert.Equal(1, peer.Handshakes);
        time.AdvanceMilliseconds(1);
        await UntilAsync(() => peer.Handshakes == 2 && client.IsTransportConnected);
        Assert.False(client.IsConnected);
        await UntilAsync(() => { scene.PumpAdmissionScene(); return client.IsConnected; });
        await scene.AdmissionReconnectTask!;
        Assert.Equal(sequence, scene.AdmissionModel.LastCommandResult!.ClientSequence);
        Assert.True(scene.AdmissionModel.CurrentTick >= scene.AdmissionModel.LastCommandResult.AppliedTick);
        Assert.Equal(0, client.PendingCommandCount);
        Assert.Null(scene.AdmissionStatus);
    }

    [Theory]
    [InlineData("geometry")] [InlineData("state")] [InlineData("inline-body")]
    public async Task RealReconnectKeepsControlsFencedUntilTheCompleteBaselineIsAdmitted(string corruption)
    {
        await using var peer = new RecoveryPeer(rejectRecovery: false);
        peer.Start();
        var time = new ManualTimeProvider();
        await using var scene = new Main();
        scene.SetReconnectTime(time);
        var client = new AuthoritativeClient(peer.Options);
        await scene.ConnectAdmissionSceneAsync(client, peer.Options);
        var first = Assert.Single(client.DrainMessages(), message => message.Snapshot is not null);
        scene.PumpAdmissionScene([first]);
        var bad = BadReference(first.Snapshot!);
        if (corruption != "geometry") bad.GeometryRevision = 1;
        if (corruption == "state") bad.StateRevision = 2;
        if (corruption == "inline-body") bad.Players.Add(first.Snapshot!.Players[0].Clone());
        Assert.False(scene.PumpAdmissionScene([new(Snapshot: bad)]));
        Assert.True(client.RequiresCompleteSnapshotAdmission);
        Assert.False(client.IsConnected);
        Assert.False(scene.PumpAdmissionScene([first])); // The rejected connection cannot reopen itself.
        time.AdvanceMilliseconds(250);
        await UntilAsync(() => peer.Handshakes == 2 && client.IsTransportConnected);
        Assert.False(client.IsConnected);
        Assert.False(scene.NeedsTransportReconnectForTest(client));
        await UntilAsync(() => { scene.PumpAdmissionScene(); return client.IsConnected; });
        await scene.AdmissionReconnectTask!;
        Assert.False(client.RequiresCompleteSnapshotAdmission);
        Assert.Null(scene.AdmissionStatus);
        Assert.Equal(2UL, scene.AdmissionModel.AcceptedSnapshotRevision);
    }

    [Fact]
    public async Task RepeatedCompleteRejectionsBackOffAndReturnThroughRealSessionCleanupAfterThreeAttempts()
    {
        await using var peer = new RecoveryPeer(rejectRecovery: true);
        peer.Start();
        var time = new ManualTimeProvider();
        await using var scene = new Main();
        scene.SetReconnectTime(time);
        var client = new AuthoritativeClient(peer.Options);
        await scene.ConnectAdmissionSceneAsync(client, peer.Options);
        var first = Assert.Single(client.DrainMessages(), message => message.Snapshot is not null);
        Assert.True(scene.PumpAdmissionScene([first]));
        Assert.False(scene.PumpAdmissionScene([new(Snapshot: BadReference(first.Snapshot!))]));
        Assert.False(client.IsConnected);
        client.Queue(new Command { OpenStoreRequested = new() { StoreId = 1 } }, requestedTick: 12);
        scene.SetAdmissionBookmarkWarning("The local bookmark cannot be written.");
        for (var attempt = 1; attempt <= Main.MaximumAdmissionRecoveryAttempts; attempt++) {
            var delay = 250 * (1 << (attempt - 1));
            Assert.Equal(TimeSpan.FromMilliseconds(delay), time.NextTimerDelay);
            time.AdvanceMilliseconds(delay - 1);
            Assert.Equal(attempt, peer.Handshakes);
            time.AdvanceMilliseconds(1);
            await UntilAsync(() => peer.Handshakes == attempt + 1 && client.IsTransportConnected);
            Assert.False(client.IsConnected);
            var rejected = new List<AuthoritativeClientMessage>();
            await UntilAsync(() => { rejected.AddRange(client.DrainMessages()); return rejected.Any(message => message.Snapshot is not null); });
            Assert.False(scene.PumpAdmissionScene(rejected));
            if (attempt < Main.MaximumAdmissionRecoveryAttempts)
                await UntilAsync(() => time.NextTimerDelay == TimeSpan.FromMilliseconds(delay * 2));
        }
        await UntilAsync(() => !client.IsTransportConnected || time.NextTimerDelay.HasValue);
        Assert.Null(time.NextTimerDelay); // Exhaustion closes instead of scheduling an unbounded fourth retry.
        await UntilAsync(() => { scene.PumpAdmissionScene(); return scene.AdmissionMenuVisible; });
        await scene.AdmissionReturnTask!;
        Assert.Equal(4, peer.Handshakes); // One original connection plus the three bounded retries.
        Assert.False(client.IsTransportConnected);
        Assert.Null(scene.AdmissionModel.Snapshot);
        Assert.Contains("failed after 3 attempts", scene.AdmissionMenuError);
        Assert.True(scene.ObservedReturnCleanupSucceeded);
        time.Advance(TimeSpan.FromMinutes(1));
        Assert.False(scene.PumpAdmissionScene());
        Assert.Equal(4, peer.Handshakes);
    }

    [Fact]
    public async Task OrdinaryTransportRecoveryKeepsItsImmediateFirstAttempt()
    {
        await using var peer = new RecoveryPeer(rejectRecovery: false);
        peer.Start();
        var time = new ManualTimeProvider();
        await using var scene = new Main();
        scene.SetReconnectTime(time);
        var client = new AuthoritativeClient(peer.Options);
        await scene.ConnectAdmissionSceneAsync(client, peer.Options);
        scene.PumpAdmissionScene();
        await client.DisconnectAsync();
        scene.PumpAdmissionScene();
        await UntilAsync(() => peer.Handshakes == 2 && scene.AdmissionReconnectTask?.IsCompleted == true);
        Assert.Null(time.NextTimerDelay);
        scene.PumpAdmissionScene();
        Assert.True(client.IsConnected);
        Assert.Equal(2UL, scene.AdmissionModel.AcceptedSnapshotRevision);
    }

    [Fact]
    public async Task OldConnectionSnapshotsCannotRejectAQueuedRecoveryBaseline()
    {
        await using var peer = new RecoveryPeer(rejectRecovery: false);
        peer.Start();
        var time = new ManualTimeProvider();
        await using var scene = new Main();
        scene.SetReconnectTime(time);
        var client = new AuthoritativeClient(peer.Options);
        await scene.ConnectAdmissionSceneAsync(client, peer.Options);
        var first = Assert.Single(client.DrainMessages(), message => message.Snapshot is not null);
        scene.PumpAdmissionScene([first]);
        scene.PumpAdmissionScene([new(Snapshot: BadReference(first.Snapshot!))]);
        time.AdvanceMilliseconds(250);
        await UntilAsync(() => peer.Handshakes == 2 && client.IsTransportConnected);
        var current = new List<AuthoritativeClientMessage>();
        await UntilAsync(() => { current.AddRange(client.DrainMessages()); return current.Any(message => message.Snapshot is not null); });
        Assert.True(scene.PumpAdmissionScene([first with { Snapshot = BadReference(first.Snapshot!) }, .. current]));
        await scene.AdmissionReconnectTask!;
        Assert.True(client.IsConnected);
        Assert.Equal(2, peer.Handshakes);
    }
}

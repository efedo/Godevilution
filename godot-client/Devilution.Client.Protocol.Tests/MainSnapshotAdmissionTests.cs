using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class MainSnapshotAdmissionTests : LiveAuthoritativeTestBase
{
    [Theory]
    [InlineData("geometry")] [InlineData("state")] [InlineData("inline-body")]
    public async Task RejectedReferencesFenceMainControlsUntilANewCompleteSnapshotIsAdmitted(string corruption)
    {
        var cancellation = TestContext.Current.CancellationToken;
        using var server = await StartedServer.StartAsync(cancellation, startingLevelOverride: 0, controlledClock: true,
            commandSchedulingMode: CommandSchedulingMode.Immediate, persistenceMode: "cohort");
        await using var client = new AuthoritativeClient(CreateOptions(server, "admission-recovery", requestedClassId: 0) with {
            UseSnapshotReferences = true, SnapshotPollInterval = TimeSpan.Zero,
        }) { CommandTimingRecorder = new AuthoritativeClientCommandTimingRecorder() };
        var scene = new Main();
        await client.ConnectAsync(cancellation);
        var first = Assert.Single(client.DrainMessages(), message => message.Snapshot is not null);
        scene.AdmitForTest(client, first);
        Assert.True(client.IsConnected);
        var initial = first.Snapshot!;
        var rejected = new Snapshot {
            Tick = initial.Tick + 1, StateSha256 = initial.StateSha256,
            ContentManifestHash = initial.ContentManifestHash, RulesetIdentityHash = initial.RulesetIdentityHash,
            GeometryUnchanged = true, GeometryRevision = initial.GeometryRevision,
            StateUnchanged = true, StateRevision = initial.StateRevision,
        };
        switch (corruption) {
        case "geometry": rejected.GeometryRevision++; break;
        case "state": rejected.StateRevision++; break;
        case "inline-body": rejected.Players.Add(initial.Players[0].Clone()); break;
        }
        var admittedTick = scene.AdmissionModel.CurrentTick;
        Assert.Throws<InvalidDataException>(() => scene.AdmitForTest(client, new(Snapshot: rejected)));
        Assert.True(scene.AdmissionReconnectRequested);
        Assert.Contains("gameplay is disabled", scene.AdmissionStatus);
        Assert.True(client.RequiresCompleteSnapshotAdmission);
        Assert.False(client.IsConnected);
        Assert.Equal(admittedTick, scene.AdmissionModel.CurrentTick);

        // Even a complete old snapshot cannot reopen the same rejected connection.
        Assert.Throws<InvalidDataException>(() => client.ApplyMessageToModel(scene.AdmissionModel, first));
        Assert.False(client.IsConnected);
        var sequence = client.Queue(new Command { MoveRequested = new() { DirectionX = 1 } }, admittedTick + 1);
        await client.PollAsync(cancellation);
        Assert.Empty(client.CommandTimingRecorder!.CaptureReport().Commands);

        await client.ReconnectAsync(cancellation);
        Assert.False(client.IsConnected); // A successful TCP/protocol handshake does not admit its model state.
        Assert.True(client.IsTransportConnected);
        Assert.False(scene.NeedsTransportReconnectForTest(client)); // Do not replace an unconsumed recovery baseline.
        var recovered = Assert.Single(client.DrainMessages(), message => message.Snapshot is not null);
        Assert.False(recovered.Snapshot!.GeometryUnchanged);
        Assert.False(recovered.Snapshot.StateUnchanged);
        scene.AdmitForTest(client, recovered);
        Assert.True(client.IsConnected);
        Assert.False(client.RequiresCompleteSnapshotAdmission);
        Assert.Null(scene.AdmissionStatus);
        Assert.NotEmpty(scene.AdmissionModel.Snapshot!.Players);
        Assert.Equal(2UL, scene.AdmissionModel.AcceptedSnapshotRevision);
        // The pending intent retains its identity and can be dispatched only after full admission.
        Assert.Equal(1, client.PendingCommandCount);
        await client.PollAsync(cancellation);
        Assert.Single(client.CommandTimingRecorder.CaptureReport().Commands, command => command.ClientSequence == sequence);
    }

    [Fact]
    public async Task InvalidCompleteRecoveryDoesNotUnfenceTheConnection()
    {
        var cancellation = TestContext.Current.CancellationToken;
        using var server = await StartedServer.StartAsync(cancellation, startingLevelOverride: 0, controlledClock: true);
        await using var client = new AuthoritativeClient(CreateOptions(server, "invalid-recovery", requestedClassId: 0) with {
            UseSnapshotReferences = true,
        });
        var scene = new Main();
        await client.ConnectAsync(cancellation);
        var first = Assert.Single(client.DrainMessages(), message => message.Snapshot is not null);
        scene.AdmitForTest(client, first);
        var invalid = first.Snapshot!.Clone();
        invalid.Players[0].Life = invalid.Players[0].LifeMaximum + 1;
        Assert.Throws<InvalidDataException>(() => scene.AdmitForTest(client, new(Snapshot: invalid)));
        await client.ReconnectAsync(cancellation);
        var fresh = Assert.Single(client.DrainMessages(), message => message.Snapshot is not null);
        var badRecovery = fresh.Snapshot!.Clone();
        badRecovery.Players[0].Life = badRecovery.Players[0].LifeMaximum + 1;
        Assert.Throws<InvalidDataException>(() => scene.AdmitForTest(client, new(Snapshot: badRecovery)));
        Assert.False(client.IsConnected);
        Assert.True(client.RequiresCompleteSnapshotAdmission);
        Assert.Throws<InvalidDataException>(() => scene.AdmitForTest(client, fresh));
        Assert.False(client.IsConnected);
        await client.ReconnectAsync(cancellation);
        var valid = Assert.Single(client.DrainMessages(), message => message.Snapshot is not null);
        scene.AdmitForTest(client, valid);
        Assert.True(client.IsConnected);
        Assert.False(client.RequiresCompleteSnapshotAdmission);
    }
}

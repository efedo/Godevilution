using Devilution.Diablo;
using Devilution.Protocol.CommandLog;
using Devilution.Protocol.V1;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class NativeTrapHostPublicationTests
{
    [Fact]
    public async Task SavedLaterFightPublishesEveryPendingMissileAtTheFailedBoundary()
    {
        var session = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_MISSILE_FAILURE_SESSION");
        var profile = Environment.GetEnvironmentVariable("GODEVILUTION_JOURNEY_REGRESSION_PROFILE");
        if (string.IsNullOrWhiteSpace(session) || string.IsNullOrWhiteSpace(profile))
            Assert.Skip("Select the private later-fight session and profile for its failing host boundary.");
        var log = Directory.GetFiles(Path.Combine(session!, "save-command-logs"), HostCommandLog.LogFileName,
            SearchOption.AllDirectories).Single();
        var directory = Path.GetDirectoryName(log)!;
        var records = CommandLogReader.ReadAll(log);
        var boundary = records.Last(value => value.ParticipantLeft is not null).ParticipantLeft.Tick;
        var checkpoint = records.Select((record, index) => (record, index)).Last(entry =>
            entry.record.Checkpoint is { } marker && marker.Tick < boundary
            && File.Exists(Path.Combine(directory, marker.FileName)));
        var document = AuthoritativeCohortStore.DeserializeDocument(File.ReadAllBytes(
            Path.Combine(directory, checkpoint.record.Checkpoint.FileName)));
        var executor = Restore(document, profile!);
        var participant = Assert.Single(document.Participants);
        foreach (var record in records.Skip(checkpoint.index + 1)) {
            if (record.ServerCommand is not { } logged) continue;
            executor.AdvanceTo(logged.CurrentTick);
            var actual = executor.Execute(logged.SessionId, logged.Command.Clone(), logged.Result.AppliedTick);
            Assert.True((logged.Result.Status == CommandStatus.Accepted) == actual.Succeeded, $"Sequence {logged.Command.ClientSequence}, tick {logged.CurrentTick}: expected {logged.Result}, actual {actual}.");
        }
        executor.AdvanceTo(boundary);
        var captured = new DiabloCohortCodec().Deserialize(executor.CaptureCohortState().SerializedState);
        var pending = Assert.Single(captured.Projectiles);
        Assert.Equal((2147483667U, 151392341U, 0U), (pending.EntityId, pending.SourceEntityId, pending.SpellId));
        Assert.Equal((7U, 51, 53, 9, 4U), (pending.LevelId, pending.PositionX, pending.PositionY, pending.Damage, pending.RemainingTicks));
        Assert.Equal("Lightning", pending.NativeMissileId);
        var encoded = executor.CaptureCohortState();
        var cold = Restore(document with { Tick = boundary, SerializedModuleState = encoded.SerializedState }, profile!);
        Assert.Equal(encoded.SerializedState, cold.CaptureCohortState().SerializedState);
        output.WriteLine($"Publishing failed live tick {boundary} with {captured.Projectiles.Count} missiles.");
        await using var host = new SnapshotHost(boundary,
            tick => cold.CreateSnapshot(participant.SessionId, participant.EntityId, tick), cold.AdvanceTo);
        await host.ConnectAsync();
        await host.PollAsync(boundary + 1);
    }
}

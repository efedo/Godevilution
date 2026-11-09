using System.Diagnostics;
using Devilution.Client.Protocol;
using Devilution.Data;
using Devilution.Diablo;
using Devilution.Protocol;
using Devilution.Protocol.CommandLog;
using Devilution.Protocol.V1;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Google.Protobuf;
using Xunit;

namespace Devilution.Server.Tests;

/** Private captures stay outside the repository; the deterministic clock regression needs no capture. */
public sealed class SavedTownWalkReplayTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Slow")]
    public void RecordedTownWalkKeepsAdmittingAndRecoversItsPresentationClock()
    {
        var log = Environment.GetEnvironmentVariable("GODEVILUTION_TOWN_WALK_REPLAY_LOG");
        var profile = Environment.GetEnvironmentVariable("GODEVILUTION_JOURNEY_REGRESSION_PROFILE");
        if (string.IsNullOrWhiteSpace(log) || string.IsNullOrWhiteSpace(profile))
            Assert.Skip("Select a private Town command log and its native profile for the recorded-target replay.");
        var records = CommandLogReader.ReadAll(log!).ToArray();
        var rejected = Array.FindIndex(records, value => value.ServerCommand?.Result.RejectReason == CommandRejectReason.InvalidTarget);
        Assert.True(rejected >= 0);
        var directory = Path.GetDirectoryName(log)!;
        var baseline = records.Take(rejected).Last(value => value.Checkpoint is { } marker
            && File.Exists(Path.Combine(directory, marker.FileName)));
        var document = AuthoritativeCohortStore.DeserializeDocument(File.ReadAllBytes(Path.Combine(directory, baseline.Checkpoint.FileName)));
        var adapter = Restore(document, profile!);
        var participant = Assert.Single(document.Participants);
        var commands = records.Where(value => value.UnixMilliseconds >= baseline.UnixMilliseconds
            && value.ServerCommand is { Command.IntentCase: Command.IntentOneofCase.MoveRequested }).ToArray();
        Assert.NotEmpty(commands);
        var cache = new AuthoritativeSnapshotGeometryCache(levelScopedIdleReferences: true);
        var model = new AuthoritativeClientModel();
        var clock = new AuthoritativePresentationClock();
        var stages = new List<(string Stage, ulong Sequence, double Milliseconds, long Bytes)>();
        var idleReferenceCount = 0;
        var idleWireBytes = 0;
        var movingWireBytes = 0;
        var commandIndex = 0;
        var previousTick = document.Tick;
        var previousMilliseconds = baseline.UnixMilliseconds;
        var nextPoll = 0d;
        var end = (commands[^1].UnixMilliseconds - baseline.UnixMilliseconds) / 1000d + 8;
        var current = 0d;
        const double frameSeconds = 1d / 160;
        var gcBefore = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
        for (var elapsed = 0d; elapsed < end; elapsed += frameSeconds) {
            var now = baseline.UnixMilliseconds + elapsed * 1000;
            while (commandIndex < commands.Length && commands[commandIndex].UnixMilliseconds <= now) {
                var record = commands[commandIndex++];
                var command = record.ServerCommand;
                Measure("advance", command.Command.ClientSequence, () => adapter.AdvanceTo(command.CurrentTick));
                Measure("execute", command.Command.ClientSequence, () => {
                    var result = adapter.Execute(command.SessionId, command.Command.Clone(), command.Result.AppliedTick);
                    Assert.Equal(command.Result.Status == CommandStatus.Accepted, result.Succeeded);
                });
                previousTick = command.CurrentTick;
                previousMilliseconds = record.UnixMilliseconds;
            }
            // Reproduce the recorded slowing clock, including discarded wall time, between receipts.
            var observedTick = commandIndex == commands.Length
                ? previousTick + (ulong)((now - previousMilliseconds) / 50)
                : previousTick + (ulong)((commands[commandIndex].ServerCommand.CurrentTick - previousTick)
                    * Math.Clamp((now - previousMilliseconds) / Math.Max(1, commands[commandIndex].UnixMilliseconds - previousMilliseconds), 0, 1));
            if (elapsed >= nextPoll) {
                Measure("advance", 0, () => adapter.AdvanceTo(observedTick));
                Snapshot snapshot = null!;
                Measure("snapshot", 0, () => {
                    snapshot = adapter.CreateSnapshot(participant.SessionId, participant.EntityId, observedTick);
                    snapshot.ContentManifestHash = document.Identity.ContentManifestHash;
                    snapshot.RulesetIdentityHash = document.Identity.RulesetIdentityHash;
                });
                Snapshot wire = null!;
                Measure("reference", 0, () => wire = cache.Project(snapshot));
                if (wire.StateUnchanged) { idleReferenceCount++; idleWireBytes = wire.CalculateSize(); }
                else if (wire.GeometryUnchanged) movingWireBytes = Math.Max(movingWireBytes, wire.CalculateSize());
                model.Apply(new(Snapshot: Snapshot.Parser.ParseFrom(wire.ToByteArray())) { LevelScopedIdleReferences = true });
                nextPoll += 0.1;
            }
            var tick = clock.Advance(model.Snapshot!, frameSeconds, 20);
            Assert.True(tick >= current);
            current = tick;
        }
        Assert.True(model.AcceptedSnapshotRevision > 100);
        Assert.True(idleReferenceCount > 0);
        Assert.InRange(current - model.CurrentTick, 0, 3);
        var finalMarker = records.Last(value => value.Checkpoint is { } marker && marker.Tick <= model.CurrentTick
            && File.Exists(Path.Combine(directory, marker.FileName)));
        var finalDocument = AuthoritativeCohortStore.DeserializeDocument(File.ReadAllBytes(Path.Combine(directory, finalMarker.Checkpoint.FileName)));
        var resumed = Restore(finalDocument, profile!);
        resumed.AdvanceTo(model.CurrentTick);
        var expected = resumed.CreateSnapshot(participant.SessionId, participant.EntityId, model.CurrentTick);
        expected.ContentManifestHash = document.Identity.ContentManifestHash;
        expected.RulesetIdentityHash = document.Identity.RulesetIdentityHash;
        Assert.Equal(expected, model.Snapshot);
        foreach (var group in stages.GroupBy(value => value.Stage))
            output.WriteLine($"{group.Key}: count={group.Count()}, mean={group.Average(value => value.Milliseconds):F3} ms, max={group.Max(value => value.Milliseconds):F3} ms, mean allocation={group.Average(value => value.Bytes):F0} B");
        foreach (var sample in stages.Where(value => value.Stage == "execute").OrderByDescending(value => value.Milliseconds).Take(12))
            output.WriteLine($"command {sample.Sequence}: {sample.Milliseconds:F3} ms, {sample.Bytes} B");
        output.WriteLine($"GC collections: {string.Join('/', Enumerable.Range(0, 3).Select(generation => GC.CollectionCount(generation) - gcBefore[generation]))}; final admitted tick={model.CurrentTick}, lead={current - model.CurrentTick:F3}");
        output.WriteLine($"Native Town: {idleReferenceCount} idle body references, {idleWireBytes} B/reference versus {movingWireBytes} B/moving body (snapshot payloads).");

        void Measure(string stage, ulong sequence, Action action)
        {
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            action();
            stages.Add((stage, sequence, Stopwatch.GetElapsedTime(start).TotalMilliseconds,
                GC.GetAllocatedBytesForCurrentThread() - allocated));
        }
    }

    private static StoreSimulationExecutor Restore(AuthoritativeCohortDocument document, string profile)
    {
        var saved = new DiabloCohortCodec().Deserialize(document.SerializedModuleState);
        var packs = new[] { "content", "native_cathedral_population", "native_cathedral_runtime", "native_town_geometry", "native_catacombs_population" }
            .Select(name => ContentPackLoader.LoadManifestDirectory(Path.Combine(profile, name))).ToArray();
        var content = ComposedContent.Compose(new ContentManifest(packs[0].Id, packs[0].Version, packs));
        var classId = saved.Participants[0].Player.CharacterClassId;
        var activation = DiabloModule.Instance.CathedralNativeTownProgressionContentBootstrap.ActivateNativeTownClass(
            content, classId, saved.Configuration.InitialRandomSeed, saved.Configuration.GameMode);
        var template = activation.PlayerLoadouts.CreateTemplate("default", classId, null, null, null, 0, null, 75, 68);
        var adapter = new StoreSimulationExecutor(activation, (_, _) => template, null,
            AuthoritativeProtocolFeatures.TargetCellPathing | AuthoritativeProtocolFeatures.CommitBoundaryCommandSnapshots);
        using var prepared = adapter.PrepareCohortRestore(new AuthoritativeModuleCohortState(
            document.Identity.ModuleId, document.Identity.ModuleFormatVersion, document.Tick,
            document.Participants.Select(value => new AuthoritativeModuleCohortParticipant(value.SessionId, value.EntityId)).ToArray(), document.SerializedModuleState));
        adapter.CommitCohortRestore(prepared);
        return adapter;
    }
}

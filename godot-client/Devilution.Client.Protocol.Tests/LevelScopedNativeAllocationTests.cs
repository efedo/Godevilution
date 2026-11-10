using System.Diagnostics;
using Devilution.Client.Protocol;
using Devilution.Data;
using Devilution.Diablo;
using Devilution.Server.Host;
using Devilution.Protocol;
using Devilution.Protocol.CommandLog;
using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class LevelScopedNativeAllocationTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Slow")]
    public void NativeTownAndDungeonWalkingMeasureScopedBodiesThroughRealOwners()
    {
        var log = Environment.GetEnvironmentVariable("GODEVILUTION_TOWN_WALK_REPLAY_LOG");
        var profile = Environment.GetEnvironmentVariable("GODEVILUTION_JOURNEY_REGRESSION_PROFILE");
        if (string.IsNullOrWhiteSpace(log) || string.IsNullOrWhiteSpace(profile))
            Assert.Skip("Select a private native Town command log and profile for the allocation comparison.");
        var records = CommandLogReader.ReadAll(log!).ToArray();
        var directory = Path.GetDirectoryName(log)!;
        var marker = records.First(value => value.Checkpoint is { } checkpoint
            && File.Exists(Path.Combine(directory, checkpoint.FileName)));
        var document = AuthoritativeCohortStore.DeserializeDocument(File.ReadAllBytes(Path.Combine(directory, marker.Checkpoint.FileName)));
        var adapter = Restore(document, profile!);
        var participant = Assert.Single(document.Participants);
        var tick = document.Tick;
        var snapshot = Capture();
        Assert.Equal(0U, snapshot.Players[0].LevelId);
        var stairs = snapshot.LevelGeometry.Single(value => value.LevelId == 0).WarpTriggers
            .First(value => value.TargetLevelId == 1 && value.Available);
        Assert.True(adapter.Execute(participant.SessionId, new Command { ClientSequence = 1,
            MoveRequested = new MoveRequested { TargetCell = true, FollowPath = true,
                TargetX = stairs.TriggerX, TargetY = stairs.TriggerY } }, tick).Succeeded);
        adapter.AdvanceTo(++tick);
        snapshot = Capture();
        Assert.Equal(PlayerActionKind.Move, snapshot.Players[0].CurrentAction?.Kind);
        Measure(snapshot, "Town walking");
        for (var step = 0; step < 1600 && snapshot.Players[0].LevelId == 0; step++) {
            adapter.AdvanceTo(++tick);
            snapshot = Capture();
        }
        Assert.Equal(1U, snapshot.Players[0].LevelId);
        for (var step = 0; step < 32 && !AuthoritativePlayerSettlement.IsSettled(snapshot.Players[0], 0); step++) {
            adapter.AdvanceTo(++tick); snapshot = Capture();
        }
        var player = snapshot.Players[0];
        var geometry = snapshot.LevelGeometry.Single(value => value.LevelId == 1);
        var solids = geometry.BlockedCells.Concat(geometry.StaticObjectBlockedCells).ToHashSet();
        var target = new[] { (X: 1, Y: 0), (X: 0, Y: 1), (X: -1, Y: 0), (X: 0, Y: -1) }
            .Select(offset => (X: player.PositionX + offset.X, Y: player.PositionY + offset.Y))
            .First(cell => cell.X >= 0 && cell.Y >= 0 && cell.X < geometry.Width && cell.Y < geometry.Height
                && !solids.Contains(cell.Y * geometry.Width + cell.X)
                && !snapshot.Monsters.Any(value => value.LevelId == 1 && value.Alive
                    && (value.PositionX, value.PositionY) == cell));
        Assert.True(adapter.Execute(participant.SessionId, new Command { ClientSequence = 2,
            MoveRequested = new MoveRequested { TargetCell = true, FollowPath = true,
                TargetX = target.X, TargetY = target.Y } }, tick).Succeeded);
        adapter.AdvanceTo(++tick); snapshot = Capture();
        Assert.Equal(PlayerActionKind.Move, snapshot.Players[0].CurrentAction?.Kind);
        Measure(snapshot, "L1 walking");

        Snapshot Capture()
        {
            var result = adapter.CreateSnapshot(participant.SessionId, participant.EntityId, tick);
            result.ContentManifestHash = document.Identity.ContentManifestHash;
            result.RulesetIdentityHash = document.Identity.RulesetIdentityHash;
            return result;
        }
    }

    private void Measure(Snapshot snapshot, string label)
    {
        var full = Sample(false);
        var scoped = Sample(true);
        output.WriteLine($"{label}: {snapshot.Monsters.Count} monsters/{snapshot.Objects.Count} objects; "
            + $"payload {full.WireBytes} -> {scoped.WireBytes} B; client decode/admission "
            + $"{full.Allocated:F0} -> {scoped.Allocated:F0} B/snapshot, "
            + $"{full.Allocated * 10 / 1_000_000:F3} -> {scoped.Allocated * 10 / 1_000_000:F3} MB/s at 10 Hz; "
            + $"{full.Milliseconds:F3} -> {scoped.Milliseconds:F3} ms/snapshot.");

        (int WireBytes, double Allocated, double Milliseconds) Sample(bool scope)
        {
            var cache = new AuthoritativeSnapshotGeometryCache(true, scope);
            var model = new AuthoritativeClientModel();
            void Admit(byte[] bytes) => model.Apply(new(Snapshot: Envelope.Parser.ParseFrom(bytes).Snapshot) {
                LevelScopedIdleReferences = true, LevelScopedSnapshots = scope,
            });
            Admit(new Envelope { Snapshot = cache.Project(snapshot) }.ToByteArray());
            var wire = cache.Project(snapshot);
            Assert.False(wire.StateUnchanged);
            Assert.Equal(snapshot.StateSha256, wire.StateSha256);
            var payload = new Envelope { Snapshot = wire }.ToByteArray();
            for (var warm = 0; warm < 16; warm++) Admit(payload);
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            for (var sample = 0; sample < 128; sample++) Admit(payload);
            var milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds / 128;
            var bytes = (GC.GetAllocatedBytesForCurrentThread() - allocated) / 128d;
            return (payload.Length, bytes, milliseconds);
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
        // Exercise the normal native adapter's internal feature-aware constructor under the client's runtime.
        var adapter = (StoreSimulationExecutor)Activator.CreateInstance(typeof(StoreSimulationExecutor),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
            [activation, (Func<string, uint?, DiabloPlayerTemplate>)((_, _) => template), null,
                AuthoritativeProtocolFeatures.TargetCellPathing | AuthoritativeProtocolFeatures.CommitBoundaryCommandSnapshots], null)!;
        using var prepared = adapter.PrepareCohortRestore(new AuthoritativeModuleCohortState(
            document.Identity.ModuleId, document.Identity.ModuleFormatVersion, document.Tick,
            document.Participants.Select(value => new AuthoritativeModuleCohortParticipant(value.SessionId, value.EntityId)).ToArray(), document.SerializedModuleState));
        adapter.CommitCohortRestore(prepared);
        return adapter;
    }
}

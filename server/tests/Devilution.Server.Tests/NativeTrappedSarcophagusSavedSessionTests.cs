using Devilution.Client.Acceptance;
using Devilution.Client.Protocol;
using Devilution.Data;
using Devilution.Engine;
using Devilution.Protocol.CommandLog;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Google.Protobuf;
using Xunit;

namespace Devilution.Server.Tests;

/** Replays a private failed journey without writing to its profile or session. */
public sealed class NativeTrappedSarcophagusSavedSessionTests(ITestOutputHelper output)
{
    [Fact(Explicit = true)]
    [Trait("Category", "Slow")]
    public void SavedOpeningPublishesItsReservedSkeletonBeforeAndAfterItWalks()
    {
        var session = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_SARCOPHAGUS_SESSION");
        var profile = Environment.GetEnvironmentVariable("GODEVILUTION_JOURNEY_REGRESSION_PROFILE");
        if (string.IsNullOrWhiteSpace(session) || string.IsNullOrWhiteSpace(profile))
            Assert.Skip("Select the private failed sarcophagus session and profile.");
        var logDirectory = Directory.GetDirectories(Path.Combine(session!, "save-command-logs")).Single();
        var records = CommandLogReader.ReadAll(Path.Combine(logDirectory, HostCommandLog.LogFileName));
        var opening = records.Select((value, index) => (value, index)).Single(value =>
            value.value.ServerCommand?.Command.OperateObjectRequested?.ObjectEntityId == NativeTrappedSarcophagusProjectionTests.Sarcophagus);
        var marker = records.Select((value, index) => (value, index)).Last(value => value.index < opening.index && value.value.Checkpoint is not null);
        var document = AuthoritativeCohortStore.DeserializeDocument(File.ReadAllBytes(Path.Combine(logDirectory, marker.value.Checkpoint!.FileName)));
        var final = AuthoritativeCohortStore.DeserializeDocument(File.ReadAllBytes(Path.Combine(session!, "save", "authoritative-cohort.json")));
        var packs = new[] {"content", "native_cathedral_population", "native_cathedral_runtime", "native_town_geometry", "native_catacombs_population"}
            .Select(name => ContentPackLoader.LoadManifestDirectory(Path.Combine(profile!, name))).ToArray();
        var manifest = new ContentManifest(packs[0].Id, packs[0].Version, packs);
        var factory = new DiabloAuthoritativeGameHostAdapterFactory("devilution.diablo.cathedral-native-all-class-town.v1");
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !Directory.Exists(Path.Combine(repository.FullName, "legacy-native"))) repository = repository.Parent;
        var start = new AuthoritativePlayerStartConfiguration(null, null, null) {
            RandomSeed = 0, GameMode = AuthoritativeGameMode.SinglePlayer, StartingLevelId = 0,
            TestParticipantDefinition = File.ReadAllText(Path.Combine(repository!.FullName, "tools/native-test-heroes/warrior-l8.json")),
        };
        var runtime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(manifest,
            factory.CreateBuiltInModules(), new Clock(), (content, registry) => factory.Activate(registry, content, start));
        var adapter = runtime.Active.Target;
        using var prepared = ((IAuthoritativeGameCohortAdapter)adapter).PrepareCohortRestore(new(
            document.Identity.ModuleId, document.Identity.ModuleFormatVersion, document.Tick,
            document.Participants.Select(value => new AuthoritativeModuleCohortParticipant(value.SessionId, value.EntityId)).ToArray(),
            document.SerializedModuleState));
        var commands = new AuthoritativeCommandServer(adapter);
        commands.InitializeCohortState(new(document.Tick, document.NextServerReceiptSequence,
            document.Participants.Select(value => new AuthoritativeSessionCommandLedger(value.SessionId, value.CommandReceipts) {
                RetiredThroughClientSequence = value.RetiredThroughClientSequence,
            }).ToArray()));
        ((IAuthoritativeGameCohortAdapter)adapter).CommitCohortRestore(prepared);
        var participant = Assert.Single(document.Participants);
        Snapshot? before = null;
        foreach (var record in records.Skip(marker.index + 1).Take(opening.index - marker.index).Where(value => value.ServerCommand is not null)) {
            var logged = record.ServerCommand!;
            commands.AdvanceTo(logged.CurrentTick);
            if (logged.Command.OperateObjectRequested?.ObjectEntityId == NativeTrappedSarcophagusProjectionTests.Sarcophagus)
                before = adapter.CreateSnapshot(participant.SessionId, participant.EntityId, logged.CurrentTick);
            Assert.Equal(logged.Result, commands.Process(logged.SessionId, logged.Command.Clone(), logged.CurrentTick));
        }
        Assert.NotNull(before);
        var receipt = opening.value.ServerCommand!.Result;
        commands.AdvanceTo(receipt.AppliedTick + 1);
        var first = adapter.CreateSnapshot(participant.SessionId, participant.EntityId, receipt.AppliedTick + 1);
        Assert.Contains(first.Monsters, value => value.EntityId == NativeTrappedSarcophagusProjectionTests.ReservedSkeleton && value.Alive);
        commands.AdvanceTo(final.Tick);
        var after = Snapshot.Parser.ParseFrom(adapter.CreateSnapshot(participant.SessionId, participant.EntityId, final.Tick).ToByteArray());
        var model = new AuthoritativeClientModel(); model.Apply(new AuthoritativeClientMessage(Snapshot: after));
        var skeleton = Assert.Single(after.Monsters, value => value.EntityId == NativeTrappedSarcophagusProjectionTests.ReservedSkeleton);
        output.WriteLine($"Opening {receipt.AppliedTick}; firing {first.Objects.Single(value => value.EntityId == NativeTrappedSarcophagusProjectionTests.WallTrap).NativeTrapFiredTick}; "
            + $"published tick {after.Tick}: {skeleton.EntityId} {skeleton.NativeAi} alive={skeleton.Alive} at ({skeleton.PositionX},{skeleton.PositionY}).");
        Assert.Equal((32, 65), (skeleton.PositionX, skeleton.PositionY));
        var checkpoint = NativeTrappedSarcophagusJourneyEvidence.Capture(before, after, NativeTrappedSarcophagusProjectionTests.Sarcophagus,
            receipt, receipt.AppliedTick + 2000, after.Objects.Single(value => value.EntityId == NativeTrappedSarcophagusProjectionTests.Sarcophagus).AnimationFrame);
        Assert.Equal(new[] {skeleton.EntityId}, checkpoint.Skeletons);
        NativeTrappedSarcophagusJourneyEvidence.VerifyRetained(after, checkpoint);
    }

    private sealed class Clock : IAuthoritativeClock { public ulong CurrentTick => 0; }
}

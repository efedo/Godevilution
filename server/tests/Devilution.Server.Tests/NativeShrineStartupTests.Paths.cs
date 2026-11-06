using Devilution.Client.Acceptance;
using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Server.Gameplay;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class NativeShrineStartupTests
{
    [Fact(Explicit = true)]
    public void SavedShrineApproachUsesTheLiveProjectionAndIdentifiesItsObstructions()
    {
        var session = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_SHRINE_PATH_SESSION");
        var profile = Environment.GetEnvironmentVariable("GODEVILUTION_JOURNEY_REGRESSION_PROFILE");
        if (string.IsNullOrWhiteSpace(session) || string.IsNullOrWhiteSpace(profile)) Assert.Skip("Select the private failed shrine session and profile.");
        var document = AuthoritativeCohortStore.DeserializeDocument(File.ReadAllBytes(Path.Combine(session!, "save", "authoritative-cohort.json")));
        var packs = new[] { "content", "native_cathedral_population", "native_cathedral_runtime", "native_town_geometry", "native_catacombs_population" }
            .Select(name => ContentPackLoader.LoadManifestDirectory(Path.Combine(profile!, name))).ToArray();
        var manifest = new ContentManifest(packs[0].Id, packs[0].Version, packs);
        var factory = new DiabloAuthoritativeGameHostAdapterFactory("devilution.diablo.cathedral-native-all-class-town.v1");
        var start = new AuthoritativePlayerStartConfiguration(null, null, null) {
            RandomSeed = 0, GameMode = AuthoritativeGameMode.SinglePlayer, StartingLevelId = 0,
            TestParticipantDefinition = File.ReadAllText(Path.Combine(FindRepository(), "tools/native-test-heroes/warrior-l8.json")),
        };
        var runtime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(manifest,
            factory.CreateBuiltInModules(), new TestClock(), (content, registry) => factory.Activate(registry, content, start));
        var adapter = runtime.Active.Target;
        using var prepared = ((IAuthoritativeGameCohortAdapter)adapter).PrepareCohortRestore(new(
            document.Identity.ModuleId, document.Identity.ModuleFormatVersion, document.Tick,
            document.Participants.Select(value => new AuthoritativeModuleCohortParticipant(value.SessionId, value.EntityId)).ToArray(),
            document.SerializedModuleState));
        ((IAuthoritativeGameCohortAdapter)adapter).CommitCohortRestore(prepared);
        var participant = Assert.Single(document.Participants);
        var snapshot = adapter.CreateSnapshot(participant.SessionId, participant.EntityId, document.Tick);
        var player = Assert.Single(snapshot.Players);
        var shrine = Assert.Single(snapshot.Objects, value => value.LevelId == 5 && value.NativeObjectId is 59 or 60
            && value.PositionX == 81 && value.PositionY == 65);
        var geometry = Assert.Single(snapshot.LevelGeometry, value => value.LevelId == 5);
        var strict = NativeObjectApproachPath.Find(snapshot, geometry, shrine);
        var actorsOnly = NativeObjectApproachPath.Find(snapshot, geometry, shrine, ignoreActors: true);
        var doorsOnly = NativeObjectApproachPath.Find(snapshot, geometry, shrine, allowClosedDoors: true);
        var route = NativeObjectApproachPath.FindForShrine(snapshot, geometry, shrine, required: true);
        var initial = snapshot.Clone(); initial.Players[0].PositionX = 81; initial.Players[0].PositionY = 76;
        Assert.Equal(14, NativeObjectApproachPath.FindForShrine(initial, geometry, shrine, required: true)!.Count);
        output.WriteLine($"L5 player ({player.PositionX},{player.PositionY}); paths strict={strict?.Count}, actors ignored={actorsOnly?.Count}, doors openable={doorsOnly?.Count}, both={route?.Count}.");
        Assert.NotNull(route);
        foreach (var cell in route!) {
            var actors = snapshot.Monsters.Where(value => value.Alive && value.LevelId == 5
                && (value.PositionX == cell.X && value.PositionY == cell.Y || value.Moving && value.FuturePositionX == cell.X && value.FuturePositionY == cell.Y));
            foreach (var actor in actors) output.WriteLine($"Blocking monster {actor.EntityId} at route cell ({cell.X},{cell.Y}).");
        }
        var door = NativeObjectApproachPath.FirstClosedDoorOnRoute(snapshot, 5, route!);
        if (door is not null) output.WriteLine($"First closed route door {door.EntityId} at ({door.PositionX},{door.PositionY}).");
        snapshot.Monsters.Clear();
        var cleared = NativeObjectApproachPath.FindForShrine(snapshot, geometry, shrine, required: false);
        Assert.NotNull(cleared);
    }
    [Fact(Explicit = true)]
    public void SavedChestApproachReplaysTheNativeRhinoKnockback()
    {
        var session = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_CHEST_PATH_SESSION");
        var profile = Environment.GetEnvironmentVariable("GODEVILUTION_JOURNEY_REGRESSION_PROFILE");
        if (string.IsNullOrWhiteSpace(session) || string.IsNullOrWhiteSpace(profile)) Assert.Skip("Select the private failed chest session and profile.");
        var logDirectory = Directory.GetDirectories(Path.Combine(session!, "save-command-logs")).Single();
        var records = Devilution.Protocol.CommandLog.CommandLogReader.ReadAll(Path.Combine(logDirectory, HostCommandLog.LogFileName));
        var marker = records.Select((value, index) => (value, index)).Single(value => value.value.Checkpoint?.Generation == 15);
        var document = AuthoritativeCohortStore.DeserializeDocument(File.ReadAllBytes(Path.Combine(logDirectory, marker.value.Checkpoint.FileName)));
        var packs = new[] { "content", "native_cathedral_population", "native_cathedral_runtime", "native_town_geometry", "native_catacombs_population" }
            .Select(name => ContentPackLoader.LoadManifestDirectory(Path.Combine(profile!, name))).ToArray();
        var manifest = new ContentManifest(packs[0].Id, packs[0].Version, packs);
        var factory = new DiabloAuthoritativeGameHostAdapterFactory("devilution.diablo.cathedral-native-all-class-town.v1");
        var start = new AuthoritativePlayerStartConfiguration(null, null, null) {
            RandomSeed = 0, GameMode = AuthoritativeGameMode.SinglePlayer, StartingLevelId = 0,
            TestParticipantDefinition = File.ReadAllText(Path.Combine(FindRepository(), "tools/native-test-heroes/warrior-l8.json")),
        };
        var runtime = new GameplayRulesetRuntime<IAuthoritativeGameHostAdapter>(manifest,
            factory.CreateBuiltInModules(), new TestClock(), (content, registry) => factory.Activate(registry, content, start));
        var adapter = runtime.Active.Target;
        using var prepared = ((IAuthoritativeGameCohortAdapter)adapter).PrepareCohortRestore(new(
            document.Identity.ModuleId, document.Identity.ModuleFormatVersion, document.Tick,
            document.Participants.Select(value => new AuthoritativeModuleCohortParticipant(value.SessionId, value.EntityId)).ToArray(),
            document.SerializedModuleState));
        var commands = new Devilution.Server.Commands.AuthoritativeCommandServer(adapter);
        commands.InitializeCohortState(new(document.Tick, document.NextServerReceiptSequence,
            document.Participants.Select(value => new Devilution.Server.Commands.AuthoritativeSessionCommandLedger(value.SessionId, value.CommandReceipts) {
                RetiredThroughClientSequence = value.RetiredThroughClientSequence,
            }).ToArray()));
        ((IAuthoritativeGameCohortAdapter)adapter).CommitCohortRestore(prepared);
        var participant = Assert.Single(document.Participants);
        Devilution.Protocol.V1.Snapshot? before = null;
        foreach (var record in records.Skip(marker.index + 1).Where(value => value.ServerCommand is not null)) {
            var logged = record.ServerCommand;
            commands.AdvanceTo(logged.CurrentTick);
            if (logged.Command.ClientSequence == 253)
                before = adapter.CreateSnapshot(participant.SessionId, participant.EntityId, logged.CurrentTick);
            Assert.Equal(logged.Result, commands.Process(logged.SessionId, logged.Command.Clone(), logged.CurrentTick));
        }
        Assert.NotNull(before);
        Assert.Equal((83, 72), (before.Players[0].PositionX, before.Players[0].PositionY));
        commands.AdvanceTo(8131);
        var after = adapter.CreateSnapshot(participant.SessionId, participant.EntityId, 8131);
        Assert.Equal((85, 73), (after.Players[0].PositionX, after.Players[0].PositionY));
        var rhino = Assert.Single(after.Monsters, value => value.EntityId == 151457899);
        Assert.Equal(("Rhino", 83, 71), (rhino.NativeAi, rhino.PositionX, rhino.PositionY));
        Assert.True(NativeObjectApproachPath.WasCombatInterruptedMove(before, after, 84, 72));
        commands.AdvanceTo(8141);
        after = adapter.CreateSnapshot(participant.SessionId, participant.EntityId, 8141);
        var chest = Assert.Single(after.Objects, value => value.EntityId == 151461932);
        Assert.False(chest.Activated);
        var route = NativeObjectApproachPath.Find(after,
            Assert.Single(after.LevelGeometry, value => value.LevelId == 8), chest);
        Assert.NotNull(route);
        Assert.InRange(route!.Count, 1, 24);
        output.WriteLine($"Sequence 253 was knocked from requested (84,72) to (85,73) at tick 8131 by Rhino {rhino.EntityId}. "
            + $"The unchanged planner still reaches chest {chest.EntityId} in {route.Count} cardinal steps at tick 8141.");
    }

}

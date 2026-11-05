using Devilution.Data;
using Devilution.Diablo;
using Devilution.Protocol.CommandLog;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

/** Replays private journey evidence without launching either game process. */
public sealed class NativeJourneyCommandReplayTests
{
    [Fact]
    public void ChestWallArrowTemporarilyBlocksWalkingButItsDiagonalDropCanBePickedUp()
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_JOURNEY_REGRESSION_LOG");
        var profile = Environment.GetEnvironmentVariable("GODEVILUTION_JOURNEY_REGRESSION_PROFILE");
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(profile))
            Assert.Skip("Select the private normal journey log and profile to reproduce the trapped-chest interruption.");
        var records = CommandLogReader.ReadAll(Path.Combine(directory!, HostCommandLog.LogFileName));
        var marker = records.Select((record, index) => (record, index)).Single(entry =>
            entry.record.KindCase == CommandLogRecord.KindOneofCase.Checkpoint && entry.record.Checkpoint.Generation == 5);
        var document = AuthoritativeCohortStore.DeserializeDocument(File.ReadAllBytes(
            Path.Combine(directory!, marker.record.Checkpoint.FileName)));
        var saved = new DiabloCohortCodec().Deserialize(document.SerializedModuleState);
        var packs = new[] { "content", "native_cathedral_population", "native_cathedral_runtime",
            "native_town_geometry", "native_catacombs_population" }
            .Select(name => ContentPackLoader.LoadManifestDirectory(Path.Combine(profile!, name))).ToArray();
        var content = ComposedContent.Compose(new ContentManifest(packs[0].Id, packs[0].Version, packs));
        var classId = saved.Participants[0].Player.CharacterClassId;
        var activation = DiabloModule.Instance.CathedralNativeTownProgressionContentBootstrap.ActivateNativeTownClass(
            content, classId, saved.Configuration.InitialRandomSeed, saved.Configuration.GameMode);
        var template = activation.PlayerLoadouts.CreateTemplate("default", classId, null, null, null, 0, null, 75, 68);
        var executor = new StoreSimulationExecutor(activation, (_, _) => template, null,
            AuthoritativeProtocolFeatures.TargetCellPathing | AuthoritativeProtocolFeatures.QueuedRangedAttacks
                | AuthoritativeProtocolFeatures.CommitBoundaryCommandSnapshots);
        using var prepared = executor.PrepareCohortRestore(new AuthoritativeModuleCohortState(
            document.Identity.ModuleId, document.Identity.ModuleFormatVersion, document.Tick,
            document.Participants.Select(value => new AuthoritativeModuleCohortParticipant(value.SessionId, value.EntityId)).ToArray(),
            document.SerializedModuleState));
        var commands = new AuthoritativeCommandServer(executor, CommandAdmissionPolicy.ForLiveHost(20));
        commands.InitializeCohortState(new AuthoritativeCommandCohortState(document.Tick, document.NextServerReceiptSequence,
            document.Participants.Select(value => new AuthoritativeSessionCommandLedger(value.SessionId, value.CommandReceipts) {
                RetiredThroughClientSequence = value.RetiredThroughClientSequence,
            }).ToArray()));
        executor.CommitCohortRestore(prepared);
        var participant = Assert.Single(document.Participants);
        foreach (var record in records.Skip(marker.index + 1).Where(value => value.KindCase == CommandLogRecord.KindOneofCase.ServerCommand)) {
            var logged = record.ServerCommand;
            if (logged.Command.ClientSequence > 65) break;
            commands.AdvanceTo(logged.CurrentTick);
            if (logged.Command.ClientSequence == 65) {
                var before = executor.CreateSnapshot(participant.SessionId, participant.EntityId, logged.CurrentTick);
                Assert.Equal(PlayerCombatMode.Block, Assert.Single(before.Players).CombatMode);
                Assert.Equal((66, 23), (before.Players[0].PositionX, before.Players[0].PositionY));
                Assert.True(before.Players[0].CombatModeEndTick > before.Tick);
            }
            Assert.Equal(logged.Result, commands.Process(logged.SessionId, logged.Command.Clone(), logged.CurrentTick));
            if (logged.Command.ClientSequence == 64) {
                commands.AdvanceTo(2305);
                var fired = executor.CreateSnapshot(participant.SessionId, participant.EntityId, 2305);
                Assert.Contains(fired.Projectiles, value => value.SourceEntityId == 151265340
                    && value.TargetEntityId == 151265346 && value.SpellId == 0);
            }
        }
        commands.AdvanceTo(2316);
        var snapshot = executor.CreateSnapshot(participant.SessionId, participant.EntityId, 2316);
        var drop = snapshot.WorldItems.Single(value => value.EntityId == 1073741826);
        Assert.Equal((65, 24), (drop.PositionX, drop.PositionY));
        var result = commands.Process(participant.SessionId, new Command {
            ClientSequence = 66, RequestedTick = 2316,
            PickupWorldItemRequested = new PickupWorldItemRequested { ItemEntityId = drop.EntityId },
        }, 2316);
        Assert.Equal(CommandStatus.Accepted, result.Status);
        var after = executor.CreateSnapshot(participant.SessionId, participant.EntityId, 2316);
        Assert.DoesNotContain(after.WorldItems, value => value.EntityId == drop.EntityId);
        Assert.Equal((66, 23), (after.Players[0].PositionX, after.Players[0].PositionY));
        var move = commands.Process(participant.SessionId, new Command {
            ClientSequence = 67, RequestedTick = 2316, MoveRequested = new MoveRequested { DirectionY = 1 },
        }, 2316);
        Assert.Equal(CommandStatus.Accepted, move.Status); // (66,24) is walkable once Block ends.
    }
}

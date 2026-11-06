using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Client.Acceptance;
using Devilution.Data;
using Devilution.Diablo;
using Devilution.Protocol;
using Devilution.Protocol.CommandLog;
using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Host;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class NativeJourneyTrapObservationReplayTests
{
    [Fact(Explicit = true)]
    public async Task InspectRequiredDoorFiringAtEveryTick()
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_DOOR_FAILURE_LOG");
        var profile = Environment.GetEnvironmentVariable("GODEVILUTION_DOOR_FAILURE_PROFILE");
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(profile))
            Assert.Skip("Select the private 200 Hz door-firing command log and profile.");
        var records = CommandLogReader.ReadAll(Path.Combine(directory, HostCommandLog.LogFileName));
        var marker = records.Select((record, index) => (record, index)).Single(value => value.record.Checkpoint?.Generation == 5);
        var document = AuthoritativeCohortStore.DeserializeDocument(File.ReadAllBytes(Path.Combine(directory, marker.record.Checkpoint.FileName)));
        var saved = new DiabloCohortCodec().Deserialize(document.SerializedModuleState);
        var packs = new[] {"content", "native_cathedral_population", "native_cathedral_runtime", "native_town_geometry", "native_catacombs_population"}
            .Select(name => ContentPackLoader.LoadManifestDirectory(Path.Combine(profile, name))).ToArray();
        var content = ComposedContent.Compose(new ContentManifest(packs[0].Id, packs[0].Version, packs));
        var activation = DiabloModule.Instance.CathedralNativeTownProgressionContentBootstrap.ActivateNativeTownClass(
            content, saved.Participants[0].Player.CharacterClassId, saved.Configuration.InitialRandomSeed, saved.Configuration.GameMode);
        var template = activation.PlayerLoadouts.CreateTemplate("default", 0, null, null, null, 0, null, 75, 68);
        var executor = new StoreSimulationExecutor(activation, (_, _) => template, null,
            AuthoritativeProtocolFeatures.TargetCellPathing | AuthoritativeProtocolFeatures.QueuedRangedAttacks | AuthoritativeProtocolFeatures.CommitBoundaryCommandSnapshots);
        using var prepared = executor.PrepareCohortRestore(new AuthoritativeModuleCohortState(document.Identity.ModuleId,
            document.Identity.ModuleFormatVersion, document.Tick,
            document.Participants.Select(value => new AuthoritativeModuleCohortParticipant(value.SessionId, value.EntityId)).ToArray(), document.SerializedModuleState));
        var commands = new AuthoritativeCommandServer(executor, CommandAdmissionPolicy.ForLiveHost(200));
        commands.InitializeCohortState(new AuthoritativeCommandCohortState(document.Tick, document.NextServerReceiptSequence,
            document.Participants.Select(value => new AuthoritativeSessionCommandLedger(value.SessionId, value.CommandReceipts) {
                RetiredThroughClientSequence = value.RetiredThroughClientSequence,
            }).ToArray()));
        executor.CommitCohortRestore(prepared);
        var participant = Assert.Single(document.Participants);
        NativeCatacombsDoorTrapObservation.Opening? baseline = null;
        NativeCatacombsDoorTrapObservation.Opened? opening = null;
        foreach (var record in records.Skip(marker.index + 1)) {
            if (record.ServerCommand is not { } logged) continue;
            commands.AdvanceTo(logged.CurrentTick);
            if (logged.Command.ClientSequence == 257) {
                var before = executor.CreateSnapshot(participant.SessionId, participant.EntityId, logged.CurrentTick);
                Assert.False(before.Objects.Single(value => value.EntityId == 151396470).MissilePassable);
                baseline = new(before, 151396470);
            }
            Assert.True(logged.Result.Equals(commands.Process(logged.SessionId, logged.Command.Clone(), logged.CurrentTick)),
                $"Command replay differs at sequence {logged.Command.ClientSequence}.");
            if (logged.Command.ClientSequence == 257)
                opening = new(Assert.IsType<NativeCatacombsDoorTrapObservation.Opening>(baseline), logged.Result.AppliedTick,
                    logged.Result.AppliedTick + 1000);
        }
        Assert.NotNull(opening);
        var result = new List<object>();
        var client = new AuthoritativeClientModel();
        for (ulong tick = 20764; tick <= 20845; tick++) {
            commands.AdvanceTo(tick);
            var snapshot = executor.CreateSnapshot(participant.SessionId, participant.EntityId, tick);
            var door = snapshot.Objects.Single(value => value.EntityId == 151396470);
            try {
                AuthoritativeSnapshotValidator.Validate(snapshot);
                client.Apply(new AuthoritativeClientMessage(Snapshot: snapshot));
            } catch (Exception e) { result.Add(new {Tick=tick, Error=e.ToString()}); }
            Assert.Equal(tick >= 20765, opening.Find(snapshot) is not null);
            if (tick >= 20765) Assert.Equal(20765UL, opening.Find(snapshot)!.Tick);
            var missiles = snapshot.Projectiles.Where(value => value.SourceEntityId == 151396408).ToArray();
            Assert.Equal(tick is >= 20765 and < 20795, missiles.Length != 0);
            if (tick == 20765) {
                var shot = Assert.Single(missiles);
                Assert.Equal(("Firebolt", 16, 151396470U), (shot.NativeMissileId, shot.Damage, shot.TargetEntityId));
            }
            if (tick == 20786) Assert.Equal("MagmaBallExplosion", Assert.Single(missiles).NativeMissileId);
            result.Add(new {Tick=tick, Open=door.MissilePassable, Armed=door.TrapArmed, Projectiles=missiles.Select(value => new {
                value.EntityId, value.TargetEntityId, value.PositionX, value.PositionY, value.Behavior, value.NativeMissileId, value.Damage, value.RemainingTicks,
            }).ToArray()});
        }
        Assert.DoesNotContain(result, value => value.GetType().GetProperty("Error") is not null);
        // A real TCP publication after flight and explosion must retain proof of the exact link.
        await using var host = new NativeTrapHostPublicationTests.SnapshotHost(20845,
            tick => executor.CreateSnapshot(participant.SessionId, participant.EntityId, tick), executor.AdvanceTo);
        await host.ConnectAsync();
        var published = await host.PollAsync(20846);
        Assert.DoesNotContain(published.Projectiles, value => value.SourceEntityId == 151396408);
        var retained = Assert.IsType<NativeCatacombsDoorTrapObservation.Firing>(opening.Find(published));
        Assert.Equal((151396470U, 151396408U, 20765UL), (retained.DoorEntity, retained.TrapEntity, retained.Tick));
        Assert.Null(host.Server.LastClientFailure);
        if (Environment.GetEnvironmentVariable("GODEVILUTION_DOOR_FAILURE_TRACE") is { Length: > 0 } path)
            File.WriteAllText(path, JsonSerializer.Serialize(result));
    }
}

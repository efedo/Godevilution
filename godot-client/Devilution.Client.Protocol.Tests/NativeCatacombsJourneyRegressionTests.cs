using Devilution.Client.Protocol;
using Devilution.Client.Acceptance;
using Devilution.Data;
using Devilution.Diablo;
using Devilution.Protocol.V1;
using Devilution.Protocol.CommandLog;
using Google.Protobuf;
using Devilution.Server.Host;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeCatacombsJourneyRegressionTests(ITestOutputHelper output)
{
    [Fact]
    public void SavedJourneySnapshotsContinueAdvancingTheClientClock()
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_JOURNEY_REGRESSION_SESSION");
        if (string.IsNullOrWhiteSpace(directory))
            Assert.Skip("Select a private journey session to reproduce its snapshots without running the journey.");
        var path = Path.Combine(directory!, "save", AuthoritativeCohortStore.CheckpointFileName);
        var document = AuthoritativeCohortStore.DeserializeDocument(File.ReadAllBytes(path));
        var executor = Restore(document);
        var participant = Assert.Single(document.Participants);
        var snapshot = executor.CreateSnapshot(participant.SessionId, participant.EntityId, document.Tick);
        var invalid = snapshot.Objects.Where(value => value.NativeChestInteractable
            && !AuthoritativeObjectSelector.IsValidNativeChestInteraction(value)).ToArray();
        Assert.True(invalid.Length == 0, string.Join("; ", invalid.Select(value => value.ToString())));
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot));
        executor.AdvanceTo(document.Tick + 20);
        model.Apply(new AuthoritativeClientMessage(Snapshot: executor.CreateSnapshot(
            participant.SessionId, participant.EntityId, document.Tick + 20)));
        Assert.Equal(document.Tick + 20, model.Snapshot!.Tick);
    }

    [Fact]
    public void SavedRequiredDoorFiringProjectsAndAdvancesTheClientThroughMissileExpiry()
    {
        var session = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_TRAP_FIRING_SESSION");
        if (string.IsNullOrWhiteSpace(session))
            Assert.Skip("Select the private required-door session to replay its captured native firing state.");
        var log = Directory.GetFiles(Path.Combine(session!, "save-command-logs"), HostCommandLog.LogFileName,
            SearchOption.AllDirectories).Single();
        var directory = Path.GetDirectoryName(log)!;
        var records = CommandLogReader.ReadAll(log);
        var operation = records.Select((record, index) => (record, index)).Last(entry =>
            entry.record.ServerCommand?.Command.OperateObjectRequested?.ObjectEntityId == 151396470);
        var operatedTick = operation.record.ServerCommand.Result.AppliedTick;
        var checkpoint = records.Select((record, index) => (record, index)).Last(entry => entry.index < operation.index
            && entry.record.Checkpoint is { } marker && marker.Tick < operatedTick
            && File.Exists(Path.Combine(directory, marker.FileName)));
        var document = AuthoritativeCohortStore.DeserializeDocument(File.ReadAllBytes(
            Path.Combine(directory, checkpoint.record.Checkpoint.FileName)));
        var executor = Restore(document);
        var participant = Assert.Single(document.Participants);
        foreach (var record in records.Skip(checkpoint.index + 1).Take(operation.index - checkpoint.index)) {
            if (record.ServerCommand is not { } logged) continue;
            executor.AdvanceTo(logged.CurrentTick);
            var actual = executor.Execute(logged.SessionId, logged.Command.Clone(), logged.Result.AppliedTick);
            Assert.Equal(logged.Result.Status == CommandStatus.Accepted, actual.Succeeded);
        }
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: executor.CreateSnapshot(
            participant.SessionId, participant.EntityId, operatedTick)));
        ulong? firstFiring = null;
        ulong? expired = null;
        var codec = new DiabloCohortCodec();
        var firebolts = new HashSet<uint>();
        ulong? explosionTick = null;
        var damageEvents = 0;
        for (var tick = operatedTick + 1; tick <= operatedTick + 128; tick++) {
            executor.AdvanceTo(tick);
            if (firstFiring is null) {
                var state = codec.Deserialize(executor.CaptureCohortState().SerializedState);
                var trap = state.NativeTraps!.Single(value => value.Facts.TriggerEntityId == 151396470);
                Assert.Equal(1, trap.TrapVar4);
                Assert.Equal(DiabloNativeTrapDisposition.Fired, trap.Disposition);
                var missile = Assert.Single(state.Projectiles, value => value.SourceEntityId == 151396408);
                firstFiring = tick;
                Assert.Equal("Firebolt", missile.NativeMissileId);
                Assert.Equal(10, missile.Damage);
                output.WriteLine($"Trap fired at {tick}; {missile.NativeMissileId} from {missile.StartX},{missile.StartY} toward {missile.TargetX},{missile.TargetY}, damage {missile.Damage}.");
            }
            var wire = executor.CreateSnapshot(participant.SessionId, participant.EntityId, tick);
            var missiles = wire.Projectiles.Where(value => value.SourceEntityId == 151396408).ToArray();
            foreach (var missile in missiles.Where(value => value.NativeMissileId == "Firebolt")) firebolts.Add(missile.EntityId);
            if (tick == firstFiring)
                Assert.Contains(NativeCatacombsDoorTrapObservation.Find(wire), value => value.DoorEntity == 151396470);
            if (missiles.FirstOrDefault(value => value.NativeMissileId == "MagmaBallExplosion") is { } boom && explosionTick is null) {
                explosionTick = tick;
                Assert.Equal((75, 74), (boom.PositionX, boom.PositionY));
                output.WriteLine($"Explosion at {tick}: {boom.PositionX},{boom.PositionY}.");
            }
            if (missiles.Length == 0) expired ??= tick;
            var events = executor.DrainEvents(participant.SessionId, participant.EntityId, tick);
            damageEvents += events?.Events.Count(value => value.Damage?.SourceEntityId == 151396408) ?? 0;
            model.Apply(new AuthoritativeClientMessage(Snapshot: Snapshot.Parser.ParseFrom(wire.ToByteArray())));
            Assert.Equal(tick, model.Snapshot!.Tick);
            if (expired is not null && tick >= expired + 8) break;
        }
        output.WriteLine($"Firing tick {firstFiring}; explosion {explosionTick}; missile expiry {expired}; damage events {damageEvents}; client reached {model.Snapshot!.Tick}.");
        Assert.Equal(6055UL, firstFiring);
        Assert.Equal(6092UL, explosionTick);
        Assert.Equal(6101UL, expired);
        Assert.Equal(0, damageEvents);
        Assert.Single(firebolts);
        var final = codec.Deserialize(executor.CaptureCohortState().SerializedState);
        Assert.Equal(DiabloNativeTrapDisposition.Fired, final.NativeTraps!.Single(
            value => value.Facts.TriggerEntityId == 151396470).Disposition);
    }

    [Fact]
    public void SavedPickedUpGoldSlotContainsADifferentLaterItemRatherThanADuplicate()
    {
        var session = Environment.GetEnvironmentVariable("GODEVILUTION_NATIVE_ITEM_REUSE_SESSION");
        if (string.IsNullOrWhiteSpace(session))
            Assert.Skip("Select the private normal journey session to check its serialized slot reuse.");
        var codec = new DiabloCohortCodec();
        var before = codec.Deserialize(AuthoritativeCohortStore.DeserializeDocument(File.ReadAllBytes(
            Directory.GetFiles(session!, "checkpoint-6-attempt-6.json", SearchOption.AllDirectories).Single())).SerializedModuleState);
        var final = codec.Deserialize(AuthoritativeCohortStore.DeserializeDocument(File.ReadAllBytes(
            Path.Combine(session!, "save", AuthoritativeCohortStore.CheckpointFileName))).SerializedModuleState);
        var oldLevel = before.CathedralItems!.Levels.Single(value => value.LevelId == 5);
        var newLevel = final.CathedralItems!.Levels.Single(value => value.LevelId == 5);
        var slot = newLevel.SlotEntityBindings.Single(value => value.EntityId == 1073741825).Slot;
        Assert.Equal(5, slot);
        Assert.DoesNotContain(slot, oldLevel.ActiveItemIds.Take(oldLevel.ActiveItemCount));
        Assert.Equal(0U, oldLevel.SlotEntityBindings[slot].EntityId);
        Assert.Equal(231948336U, oldLevel.Items[slot].Seed);
        Assert.Equal(11, oldLevel.Items[slot].ItemType);
        var replacement = final.WorldItems.Single(value => value.EntityId == 1073741825);
        Assert.Equal(251598481U, replacement.ItemSeed);
        Assert.Equal(150U, replacement.State.StableItemId);
        Assert.Equal(newLevel.Items[slot].Seed, replacement.ItemSeed);
        var snapshot = new Snapshot { WorldItems = { new WorldItemSnapshot { EntityId = replacement.EntityId,
            LevelId = replacement.LevelId, ItemSeed = replacement.ItemSeed,
            State = new ItemStateSnapshot { StableItemId = replacement.State.StableItemId } } } };
        Assert.False(NativePickedUpItemIdentity.IsOnFloor(snapshot, 231948336, 25));
        Assert.DoesNotContain(final.WorldItems, value => value.ItemSeed == 231948336 && value.State.StableItemId == 25);
        output.WriteLine("Slot 5 was inactive after Gold pickup (seed 231948336, stable 25); entity 1073741825 later binds scroll seed 251598481, stable 150.");
    }

    private static StoreSimulationExecutor Restore(AuthoritativeCohortDocument document)
    {
        var profile = Environment.GetEnvironmentVariable("GODEVILUTION_JOURNEY_REGRESSION_PROFILE");
        Assert.False(string.IsNullOrWhiteSpace(profile));
        var saved = new DiabloCohortCodec().Deserialize(document.SerializedModuleState);
        var packs = new[] { "content", "native_cathedral_population", "native_cathedral_runtime",
            "native_town_geometry", "native_catacombs_population" }
            .Select(directory => ContentPackLoader.LoadManifestDirectory(Path.Combine(profile!, directory))).ToArray();
        var content = ComposedContent.Compose(new ContentManifest(packs[0].Id, packs[0].Version, packs));
        var classId = saved.Participants[0].Player.CharacterClassId;
        var activation = DiabloModule.Instance.CathedralNativeTownProgressionContentBootstrap.ActivateNativeTownClass(
            content, classId, saved.Configuration.InitialRandomSeed, saved.Configuration.GameMode);
        var template = activation.PlayerLoadouts.CreateTemplate("default", classId, null, null, null, 0, null, 75, 68);
        var executor = new StoreSimulationExecutor(activation, template);
        using var prepared = executor.PrepareCohortRestore(new AuthoritativeModuleCohortState(
            document.Identity.ModuleId, document.Identity.ModuleFormatVersion, document.Tick,
            document.Participants.Select(value => new AuthoritativeModuleCohortParticipant(value.SessionId, value.EntityId)).ToArray(),
            document.SerializedModuleState));
        executor.CommitCohortRestore(prepared);
        return executor;
    }
}

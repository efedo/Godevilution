using System.Text.Json;
using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Google.Protobuf;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    private const string NativeDepthEntryTownCheckpointKind = "native-depth-entry-continuation-town-v1";

    private sealed record NativeDepthEntryBaseline(
        int FormatVersion,
        string ExpectedSnapshotBase64,
        string InputCheckpointSha256,
        ulong Tick,
        ulong LastClientSequence,
        string ContentHash,
        string RulesetHash,
        uint EntityId,
        uint LevelId);

    private sealed record NativeDepthEntryTownCheckpoint(
        int FormatVersion,
        string CheckpointKind,
        string SourceEntryCheckpointSha256,
        string ExpectedSnapshotBase64,
        string InputCheckpointSha256,
        ulong Tick,
        ulong LastClientSequence,
        string ContentHash,
        string RulesetHash,
        uint EntityId,
        uint LevelId,
        uint RandomState,
        string WorldHash,
        NativeDepthProgressionGeometry[] Floors,
        NativeDepthProgressionCombat[] Combat);
    private sealed record NativeDepthEntryCheckpointInputProof(int FormatVersion, string InputCheckpointSha256);
    private readonly record struct NativeDepthEntryGearIdentity(uint StableItemId, uint ItemSeed);

    private async Task VerifyNativeDepthEntryContinuationAsync(
        Main main,
        string directory,
        string baselinePath,
        bool visible,
        Func<uint, Task> travel,
        Func<Task> townCheckpoint)
    {
        var baselineJson = JsonDocument.Parse(System.IO.File.ReadAllText(baselinePath));
        using (baselineJson) {
            if (baselineJson.RootElement.TryGetProperty("CheckpointKind", out var kind)) {
                Require(kind.GetString() == NativeDepthEntryTownCheckpointKind,
                    "The continuation baseline is not the explicit Town checkpoint produced by this route.");
                var expected = baselineJson.RootElement.Deserialize<NativeDepthEntryTownCheckpoint>()
                    ?? throw new InvalidDataException("The native depth Town continuation checkpoint is empty.");
                await ResumeNativeDepthEntryContinuationAsync(main, directory, expected, visible, travel, townCheckpoint);
                return;
            }

            var entry = baselineJson.RootElement.Deserialize<NativeDepthEntryBaseline>()
                ?? throw new InvalidDataException("The native depth entry baseline is empty.");
            await RunNativeDepthEntryContinuationAsync(main, directory, entry, visible, travel, townCheckpoint);
        }
    }

    private async Task RunNativeDepthEntryContinuationAsync(
        Main main,
        string directory,
        NativeDepthEntryBaseline baseline,
        bool visible,
        Func<uint, Task> travel,
        Func<Task> townCheckpoint)
    {
        var initial = main.ObservedSnapshot
            ?? throw new InvalidDataException("The native depth continuation has no admitted snapshot.");
        var initialPlayer = initial.Players.Single();
        var connection = main.ObservedInitialConnection
            ?? throw new InvalidDataException("The native depth continuation has no connection observation.");
        var hello = connection.ServerHello
            ?? throw new InvalidDataException("The native depth continuation has no ServerHello observation.");
        var identity = main.ObservedIdentity
            ?? throw new InvalidDataException("The native depth continuation has no admitted server identity.");
        var copiedCheckpointHash = ReadCheckpointInputProof(directory);
        var expectedSnapshot = Snapshot.Parser.ParseFrom(Convert.FromBase64String(baseline.ExpectedSnapshotBase64));
        Require(baseline.FormatVersion == 1 && baseline.EntityId != 0 && baseline.LevelId == 3
            && IsSha256(baseline.InputCheckpointSha256) && copiedCheckpointHash == baseline.InputCheckpointSha256,
            "The continuation did not start from the exact copied R18 L3 checkpoint.");
        Require(connection.AdmissionCompleted && hello.ResumedSessionMatched
            && hello.LastClientSequence == baseline.LastClientSequence
            && identity.ContentManifestHash == baseline.ContentHash
            && identity.RulesetIdentityHash == baseline.RulesetHash,
            "The continuation was not admitted against the R18 profile, content, ruleset and saved command cursor.");
        Require(initial.Tick >= baseline.Tick && initialPlayer.EntityId == baseline.EntityId
            && initialPlayer.LevelId == baseline.LevelId
            && initialPlayer.ToByteArray().AsSpan().SequenceEqual(expectedSnapshot.Players.Single().ToByteArray())
            && initial.RandomState == expectedSnapshot.RandomState
            && HashNativeProgressionWorld(initial) == HashNativeProgressionWorld(expectedSnapshot)
            && main.ObservedLastQueuedSequence == 0 && main.ObservedPendingCommandCount == 0 && Settled(main),
            "The copied L3 checkpoint changed its player, RNG, world or cursor before continuation input.");

        var timing = main.ObservedCommandTimingReport
            ?? throw new InvalidOperationException("The continuation requires sanitized command timing evidence.");
        var startingSequence = hello.LastClientSequence;
        var bow = initialPlayer.Equipment.Single(item => item.Slot == 4).Clone();
        var entryPlayer = new {
            initialPlayer.EntityId, initialPlayer.LevelId, initialPlayer.PositionX, initialPlayer.PositionY,
            initialPlayer.Life, initialPlayer.LifeMaximum, initialPlayer.Experience, initial.RandomState,
            Tick = initial.Tick, Sequence = startingSequence,
        };
        WriteEvidence(directory, "native-depth-entry-continuation-start.json", new {
            CheckpointSha256 = copiedCheckpointHash,
            BaselineTick = baseline.Tick,
            ObservedTick = initial.Tick,
            ContentHash = identity.ContentManifestHash,
            RulesetHash = identity.RulesetIdentityHash,
            Player = entryPlayer,
            WorldHash = HashNativeProgressionWorld(initial),
            BeforeAnyGameplayInput = true,
        });

        Require(IsNativeProgressionBowUsable(bow), "The copied L3 checkpoint has no usable earned Rogue bow.");
        var combat = new List<NativeDepthProgressionCombat>(capacity: 2);
        var defeatedL3 = await FightNativeProgressionCombatAsync(main, directory, 3);
        combat.Add(new(3, defeatedL3));
        await ReequipNativeBarrelWeapon(main, bow);
        await travel(4);
        Require(main.ObservedSnapshot!.Players.Single().LevelId == 4,
            "The ordinary native stair route did not enter L4 from the copied L3 checkpoint.");
        var defeatedL4 = await FightNativeProgressionCombatAsync(main, directory, 4);
        combat.Add(new(4, defeatedL4));
        await ReequipNativeBarrelWeapon(main, bow);
        for (var destination = 3; destination >= 0; destination--)
            await travel((uint)destination);
        Require(main.ObservedSnapshot!.Players.Single().LevelId == 0,
            "The L3/L4 continuation did not return to Town through ordinary routes.");
        await UseNativeTownHealer(main);
        await townCheckpoint();

        var final = main.ObservedSnapshot!;
        var finalPlayer = final.Players.Single();
        Require(finalPlayer.Life > 0 && finalPlayer.LevelId == 0 && Settled(main),
            "The L3/L4 continuation did not reach a living, settled Town checkpoint.");
        VerifyEntryContinuationRetention(final, combat);
        var report = main.ObservedCommandTimingReport ?? timing;
        var pathCommands = CaptureAcceptedNativePathCommands(report, startingSequence);
        Require(pathCommands.Length > 0,
            "The actual continuation did not record an accepted ordinary world-click destination path.");
        var sequence = main.ObservedLastQueuedSequence;
        Require(sequence > startingSequence && main.ObservedCommandResult?.ClientSequence == sequence,
            "The continuation Town checkpoint has no settled command cursor.");
        var townCheckpointState = new NativeDepthEntryTownCheckpoint(
            1,
            NativeDepthEntryTownCheckpointKind,
            copiedCheckpointHash,
            Convert.ToBase64String(final.ToByteArray()),
            string.Empty,
            final.Tick,
            sequence,
            identity.ContentManifestHash,
            identity.RulesetIdentityHash,
            finalPlayer.EntityId,
            finalPlayer.LevelId,
            final.RandomState,
            HashNativeProgressionWorld(final),
            Enumerable.Range(1, 4).Select(level => new NativeDepthProgressionGeometry((uint)level,
                HashProto(DepthGeometry(main, (uint)level)))).ToArray(),
            combat.ToArray());
        WriteEvidence(directory, "native-depth-entry-continuation.json", new {
            CheckpointKind = NativeDepthEntryTownCheckpointKind,
            FullRouteClaimed = false,
            Visible = visible,
            SourceEntryCheckpointSha256 = copiedCheckpointHash,
            BaselineTick = baseline.Tick,
            InitialObservedTick = initial.Tick,
            InitialSequence = startingSequence,
            FinalTick = final.Tick,
            FinalSequence = sequence,
            Player = finalPlayer,
            RandomState = final.RandomState,
            WorldHash = HashNativeProgressionWorld(final),
            Floors = townCheckpointState.Floors,
            Combat = combat,
            AcceptedWorldClickPaths = pathCommands,
        });
        WriteEvidence(directory, "native-depth-entry-continuation-town.json", townCheckpointState);
    }

    private async Task ResumeNativeDepthEntryContinuationAsync(
        Main main,
        string directory,
        NativeDepthEntryTownCheckpoint expected,
        bool visible,
        Func<uint, Task> travel,
        Func<Task> townCheckpoint)
    {
        var initial = main.ObservedSnapshot
            ?? throw new InvalidDataException("The native depth Town continuation has no admitted snapshot.");
        var player = initial.Players.Single();
        var connection = main.ObservedInitialConnection
            ?? throw new InvalidDataException("The fresh Town continuation has no connection observation.");
        var hello = connection.ServerHello
            ?? throw new InvalidDataException("The fresh Town continuation has no ServerHello observation.");
        var identity = main.ObservedIdentity
            ?? throw new InvalidDataException("The fresh Town continuation has no admitted server identity.");
        var copiedCheckpointHash = ReadCheckpointInputProof(directory);
        var expectedSnapshot = Snapshot.Parser.ParseFrom(Convert.FromBase64String(expected.ExpectedSnapshotBase64));
        Require(expected.FormatVersion == 1 && expected.CheckpointKind == NativeDepthEntryTownCheckpointKind
            && expected.LevelId == 0 && expected.EntityId != 0 && IsSha256(expected.InputCheckpointSha256)
            && copiedCheckpointHash == expected.InputCheckpointSha256
            && expected.Floors.Select(floor => floor.LevelId).SequenceEqual(new uint[] { 1, 2, 3, 4 })
            && expected.Combat.Select(floor => floor.LevelId).SequenceEqual(new uint[] { 3, 4 }),
            "The fresh-host continuation did not open the exact Town checkpoint produced by the L3/L4 route.");
        Require(connection.AdmissionCompleted && hello.ResumedSessionMatched
            && hello.LastClientSequence == expected.LastClientSequence
            && identity.ContentManifestHash == expected.ContentHash
            && identity.RulesetIdentityHash == expected.RulesetHash,
            "The fresh Town continuation changed the profile, content, ruleset or saved cursor.");
        Require(initial.Tick >= expected.Tick && player.EntityId == expected.EntityId && player.LevelId == 0
            && player.ToByteArray().AsSpan().SequenceEqual(expectedSnapshot.Players.Single().ToByteArray())
            && initial.RandomState == expected.RandomState
            && HashNativeProgressionWorld(initial) == expected.WorldHash
            && main.ObservedLastQueuedSequence == 0 && main.ObservedPendingCommandCount == 0 && Settled(main),
            "Fresh Town restore changed the exact saved player, RNG, world, entity or level before input.");
        VerifyEntryContinuationRetention(initial, expected, expectedSnapshot);

        var revisited = new List<uint>();
        for (uint depth = 1; depth <= 4; depth++) {
            await travel(depth);
            var floorSnapshot = main.ObservedSnapshot!;
            Require(floorSnapshot.Players.Single().LevelId == depth,
                $"Fresh Town continuation did not revisit L{depth} through its ordinary route.");
            VerifyEntryContinuationFloor(floorSnapshot, expected, expectedSnapshot, depth);
            revisited.Add(depth);
        }
        for (var destination = 3; destination >= 0; destination--) {
            await travel((uint)destination);
            VerifyEntryContinuationRetention(main.ObservedSnapshot!, expected, expectedSnapshot);
        }
        await townCheckpoint();
        var final = main.ObservedSnapshot!;
        Require(final.Players.Single().LevelId == 0 && final.Players.Single().Life > 0 && Settled(main),
            "The fresh-host continuation did not return alive and settled in Town.");
        VerifyEntryContinuationRetention(final, expected, expectedSnapshot);
        WriteEvidence(directory, "native-depth-entry-continuation-resume.json", new {
            ContinuationComplete = true,
            FullRouteClaimed = false,
            Visible = visible,
            InputCheckpointSha256 = expected.InputCheckpointSha256,
            ObservedInitialTick = initial.Tick,
            SavedTick = expected.Tick,
            SavedSequence = expected.LastClientSequence,
            FinalTick = final.Tick,
            FinalSequence = main.ObservedLastQueuedSequence,
            EntityId = final.Players.Single().EntityId,
            LevelId = final.Players.Single().LevelId,
            RevisitedLevels = revisited.ToArray(),
            FloorGeometry = expected.Floors,
            RetainedCombat = expected.Combat,
        });
    }

    private static void VerifyEntryContinuationRetention(Snapshot snapshot, IReadOnlyList<NativeDepthProgressionCombat> combat)
    {
        foreach (var floor in combat) {
            Require(floor.LevelId is 3 or 4 && floor.Combat.Kills.Length > 0
                && floor.Combat.ExperienceAfter > floor.Combat.ExperienceBefore
                && floor.Combat.WorkUsed is > 0 and <= NativeCombatWorkBudget.MaximumWork,
                $"The continuation omitted its bounded real L{floor.LevelId} combat/XP evidence.");
            VerifyEntryContinuationFloor(snapshot, floor.LevelId, floor);
        }
    }

    private static void VerifyEntryContinuationRetention(
        Snapshot snapshot,
        NativeDepthEntryTownCheckpoint expected,
        Snapshot expectedSnapshot)
    {
        foreach (var floor in expected.Floors)
            Require(HashProto(snapshot.LevelGeometry.Single(row => row.LevelId == floor.LevelId)) == floor.Hash,
                $"The Town continuation changed retained L{floor.LevelId} geometry.");
        VerifyEntryContinuationRetention(snapshot, expected.Combat);
        VerifyEntryContinuationFloorRetention(snapshot, expectedSnapshot, 1);
        VerifyEntryContinuationFloorRetention(snapshot, expectedSnapshot, 2);
        VerifyEntryContinuationGear(snapshot, expectedSnapshot);
    }

    private static void VerifyEntryContinuationFloor(
        Snapshot snapshot,
        NativeDepthEntryTownCheckpoint expected,
        Snapshot expectedSnapshot,
        uint levelId)
    {
        var floor = expected.Floors.Single(row => row.LevelId == levelId);
        Require(HashProto(snapshot.LevelGeometry.Single(row => row.LevelId == levelId)) == floor.Hash,
            $"Fresh Town continuation changed retained L{levelId} geometry.");
        if (levelId is 1 or 2)
            VerifyEntryContinuationFloorRetention(snapshot, expectedSnapshot, levelId);
        VerifyEntryContinuationGear(snapshot, expectedSnapshot);
        var combat = expected.Combat.SingleOrDefault(row => row.LevelId == levelId);
        if (combat is not null)
            VerifyEntryContinuationFloor(snapshot, levelId, combat);
    }

    private static void VerifyEntryContinuationFloorRetention(Snapshot snapshot, Snapshot expectedSnapshot, uint levelId)
    {
        foreach (var corpse in expectedSnapshot.Corpses.Where(corpse => corpse.LevelId == levelId)) {
            var retained = snapshot.Corpses.Where(row => row.EntityId == corpse.EntityId && row.LevelId == levelId).ToArray();
            Require(retained.Length == 1 && retained[0].PositionX == corpse.PositionX
                && retained[0].PositionY == corpse.PositionY,
                $"Fresh Town continuation changed or lost retained L{levelId} corpse {corpse.EntityId}.");
        }
        foreach (var monster in expectedSnapshot.Monsters.Where(monster => monster.LevelId == levelId && !monster.Alive)) {
            var retained = snapshot.Monsters.Where(row => row.EntityId == monster.EntityId && row.LevelId == levelId).ToArray();
            Require(retained.Length == 1 && !retained[0].Alive,
                $"Fresh Town continuation revived or lost retained L{levelId} dead actor {monster.EntityId}.");
        }
    }

    private static void VerifyEntryContinuationGear(Snapshot snapshot, Snapshot expectedSnapshot)
    {
        var expectedPlayer = expectedSnapshot.Players.Single();
        var expectedGear = expectedPlayer.Inventory
            .Where(item => IsContinuationGear(item.State))
            .Select(item => new NativeDepthEntryGearIdentity(item.State.StableItemId, item.ItemSeed))
            .Concat(expectedPlayer.Equipment
                .Where(item => IsContinuationGear(item.State))
                .Select(item => new NativeDepthEntryGearIdentity(item.State.StableItemId, item.ItemSeed)))
            .Concat(expectedPlayer.Belt
                .Where(item => IsContinuationGear(item.State))
                .Select(item => new NativeDepthEntryGearIdentity(item.State.StableItemId, item.ItemSeed)))
            .Where(item => item.StableItemId != 0 && item.ItemSeed != 0)
            .Distinct()
            .ToArray();
        Require(expectedGear.Length > 0, "The retained Town baseline has no stable weapon or armor identity.");

        var player = snapshot.Players.Single();
        var carriedGear = player.Inventory
            .Where(item => IsContinuationGear(item.State))
            .Select(item => new NativeDepthEntryGearIdentity(item.State.StableItemId, item.ItemSeed))
            .Concat(player.Equipment
                .Where(item => IsContinuationGear(item.State))
                .Select(item => new NativeDepthEntryGearIdentity(item.State.StableItemId, item.ItemSeed)))
            .Concat(player.Belt
                .Where(item => IsContinuationGear(item.State))
                .Select(item => new NativeDepthEntryGearIdentity(item.State.StableItemId, item.ItemSeed)))
            .ToHashSet();
        var baselineGround = expectedSnapshot.WorldItems
            .Select(item => new NativeDepthEntryGearIdentity(item.State.StableItemId, item.ItemSeed))
            .ToHashSet();
        foreach (var gear in expectedGear) {
            Require(carriedGear.Contains(gear),
                $"Fresh Town continuation lost retained gear stable ID {gear.StableItemId}/seed {gear.ItemSeed}.");
            if (!baselineGround.Contains(gear))
                Require(!snapshot.WorldItems.Any(item => item.State.StableItemId == gear.StableItemId
                    && item.ItemSeed == gear.ItemSeed),
                    $"Fresh Town continuation respawned claimed gear stable ID {gear.StableItemId}/seed {gear.ItemSeed}.");
        }
    }

    private static bool IsContinuationGear(ItemStateSnapshot? state)
        => state is not null && state.ItemClass is 1 or 2;

    private static void VerifyEntryContinuationFloor(Snapshot snapshot, uint levelId, NativeDepthProgressionCombat floor)
    {
        foreach (var kill in floor.Combat.Kills) {
            Require(kill.MonsterEntityId != 0 && kill.AcceptedShots.Length > 0
                && kill.ExperienceAfter > kill.ExperienceBefore
                && snapshot.Corpses.Count(corpse => corpse.EntityId == kill.MonsterEntityId && corpse.LevelId == levelId) == 1
                && !snapshot.Monsters.Any(monster => monster.EntityId == kill.MonsterEntityId && monster.Alive),
                $"Retained L{levelId} lost or revived continuation combat corpse {kill.MonsterEntityId}.");
        }
    }

    private static object[] CaptureAcceptedNativePathCommands(
        AuthoritativeClientTimingCaptureReport report,
        ulong afterSequence)
    {
        Require(report.DroppedRecordCount == 0,
            "Sanitized command timing dropped records during the native depth continuation.");
        return report.Commands
            .Where(command => command.ClientSequence > afterSequence
                && command.IntentKind == Command.IntentOneofCase.MoveRequested
                && command.MoveTargetCell && command.MoveFollowPath
                && command.WriteOutcome == AuthoritativeClientWriteOutcome.Completed)
            .Join(report.Acknowledgements,
                command => (command.TransportGeneration, command.ClientSequence),
                receipt => (receipt.TransportGeneration, receipt.ClientSequence),
                (command, receipt) => new { command, receipt })
            .Where(row => row.receipt.Status == CommandStatus.Accepted
                && row.receipt.RejectReason == CommandRejectReason.Unspecified
                && row.receipt.AppliedTick > 0)
            .Select(row => (object)new {
                Sequence = row.command.ClientSequence,
                row.command.RequestedTick,
                AppliedTick = row.receipt.AppliedTick,
                TargetX = row.command.MoveTargetX,
                TargetY = row.command.MoveTargetY,
                WorldClickPathRequested = true,
                Accepted = true,
            })
            .ToArray();
    }

    private static string ReadCheckpointInputProof(string directory)
    {
        var path = System.IO.Path.Combine(directory, "native-depth-entry-input-proof.json");
        var proof = JsonSerializer.Deserialize<NativeDepthEntryCheckpointInputProof>(System.IO.File.ReadAllText(path))
            ?? throw new InvalidDataException("The wrapper did not provide a copied-checkpoint hash proof.");
        Require(proof.FormatVersion == 1 && IsSha256(proof.InputCheckpointSha256),
            "The copied-checkpoint hash proof is malformed.");
        return proof.InputCheckpointSha256.ToUpperInvariant();
    }

    private static bool IsNativeDepthEntryTownCheckpointBaseline(string baselinePath)
    {
        using var baseline = JsonDocument.Parse(System.IO.File.ReadAllText(baselinePath));
        return baseline.RootElement.TryGetProperty("CheckpointKind", out var kind)
            && kind.GetString() == NativeDepthEntryTownCheckpointKind;
    }

    private static bool IsSha256(string value)
        => value.Length == 64 && value.All(character => char.IsAsciiHexDigit(character));
}

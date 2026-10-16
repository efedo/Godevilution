using Devilution.Protocol.V1;

namespace Devilution.Client.Acceptance;

public partial class SessionAcceptance
{
    private sealed record NativeProgressionL2CombatEvidence(
        uint MonsterEntityId,
        uint MonsterId,
        uint ExperienceBefore,
        uint ExperienceAfter,
        uint[] EntryGroundItemIds,
        uint[] ObservedNewDropEntityIds,
        NativeKillEvidence[] Kills,
        NativeAttackRefusalEvidence[] AttackRefusals,
        int WorkUsed);

    private async Task<NativeProgressionL2CombatEvidence> FightNativeProgressionL2CombatAsync(
        Main main,
        string directory)
    {
        var initialSnapshot = main.ObservedSnapshot
            ?? throw new InvalidOperationException("Native progression L2 combat requires an authoritative snapshot.");
        var initialPlayer = initialSnapshot.Players.Single();
        Require(IsNativeProgressionL2(main) && initialPlayer.LevelId == 2 && initialPlayer.Life > 0,
            "Native progression L2 combat requires a living Rogue on L2.");

        var experienceBefore = initialPlayer.Experience;
        var entryGroundItemIds = initialSnapshot.WorldItems
            .Where(item => item.LevelId == initialPlayer.LevelId)
            .Select(item => item.EntityId)
            .Order()
            .ToArray();
        var initialEligibleActors = initialSnapshot.Monsters
            .Where(monster => monster.LevelId == initialPlayer.LevelId && monster.Alive && !monster.IsPlayerMinion)
            .Select(monster => monster.EntityId)
            .ToHashSet();
        Require(initialEligibleActors.Count > 0,
            "Native progression L2 has no live targets for the bounded combat step.");

        var work = new NativeCombatWorkBudget(enforceCumulativeTargetLimit: true);
        var reselectionYields = 0;
        var doorApproachCommands = new Dictionary<uint, int>();
        var operatedDoors = new HashSet<uint>();
        var doorEvidence = new List<NativeDoorOperationEvidence>();

        while (work.Used < NativeCombatWorkBudget.MaximumWork) {
            var snapshot = main.ObservedSnapshot!;
            var player = snapshot.Players.Single();
            Require(player.LevelId == initialPlayer.LevelId && player.Life > 0,
                "The Rogue died or left L2 before the first native combat target was defeated.");

            var remainingTargets = snapshot.Monsters
                .Where(monster => monster.LevelId == player.LevelId
                    && monster.Alive && !monster.IsPlayerMinion
                    && initialEligibleActors.Contains(monster.EntityId))
                .OrderBy(monster => NativeMonsterDistance(player, monster))
                .ThenBy(monster => monster.EntityId)
                .ToArray();
            var target = remainingTargets.FirstOrDefault(monster =>
                HasNativeRangedLaneWithinApproach(main, snapshot, player, monster,
                    NativeMaximumRangedApproachCommands));
            if (target is null) {
                if (remainingTargets.Length > 0 && operatedDoors.Count < NativeMaximumDoorOperations
                    && await AdvanceTowardReachableNativeDoor(main, doorApproachCommands,
                        operatedDoors, doorEvidence, work))
                    continue;

                throw new InvalidOperationException(
                    "No eligible native L2 target or bounded door route remains before the first defeat: "
                    + $"initialTargets={initialEligibleActors.Count}, "
                    + $"reselectionYields={reselectionYields}/{NativeGearHuntMaximumReselectionYields}, "
                    + $"combatWork={work.Used}/{NativeCombatWorkBudget.MaximumWork}, "
                    + $"doors={operatedDoors.Count}/{NativeMaximumDoorOperations}, "
                    + $"entryItems=[{string.Join(",", entryGroundItemIds)}], "
                    + $"refusals=[{string.Join(";", work.AttackRefusals)}]; "
                    + DescribeNativeAttackSnapshot(snapshot, 0) + "; "
                    + DescribeNativeUnopenedDoors(snapshot, operatedDoors));
            }

            var defeated = await DefeatNativeTargetAsync(main, target.EntityId,
                maximumAttacks: NativeGearHuntMaximumAttacksPerTarget,
                combatWorkBudget: work);
            if (defeated is null) {
                reselectionYields++;
                work.ConsumeExploration("reselect the first native L2 combat target");
                Require(reselectionYields <= NativeGearHuntMaximumReselectionYields,
                    "Native L2 first-combat selection exceeded its bounded reselection limit: "
                    + $"yields={reselectionYields}/{NativeGearHuntMaximumReselectionYields}, "
                    + $"combatWork={work.Used}/{NativeCombatWorkBudget.MaximumWork}, "
                    + $"refusals=[{string.Join(";", work.AttackRefusals)}].");
                continue;
            }

            var finalSnapshot = main.ObservedSnapshot
                ?? throw new InvalidOperationException("Native L2 combat lost its final authoritative snapshot.");
            var finalPlayer = finalSnapshot.Players.Single();
            Require(defeated.MonsterEntityId == target.EntityId && defeated.MonsterId == target.MonsterId,
                "The first defeated native L2 target did not match the selected live target identity.");
            var targetCorpses = finalSnapshot.Corpses.Where(corpse =>
                corpse.EntityId == defeated.MonsterEntityId && corpse.LevelId == initialPlayer.LevelId).ToArray();
            Require(finalPlayer.LevelId == initialPlayer.LevelId && finalPlayer.Life > 0,
                "The Rogue died or left L2 during the first native target defeat.");
            Require(finalPlayer.Experience > experienceBefore && defeated.ExperienceAfter > experienceBefore,
                "The first native target defeat did not produce an XP increase over the L2 entry baseline.");
            Require(targetCorpses.Length == 1
                && !finalSnapshot.Monsters.Any(monster => monster.EntityId == defeated.MonsterEntityId && monster.Alive),
                "The first native target did not resolve to exactly one corpse with no living target remaining.");
            Require(defeated.AcceptedShots.Length > 0
                && defeated.AcceptedShots.All(shot => shot.TargetEntityId == defeated.MonsterEntityId
                    && shot.Sequence > 0 && shot.AppliedTick > 0),
                "The first native target defeat lacks an accepted ordinary aimed-shot ledger.");

            var entryItems = entryGroundItemIds.ToHashSet();
            var observedNewDropEntityIds = defeated.DropEntityIds
                .Where(entityId => !entryItems.Contains(entityId))
                .Distinct()
                .Order()
                .ToArray();
            var evidence = new NativeProgressionL2CombatEvidence(
                defeated.MonsterEntityId,
                defeated.MonsterId,
                experienceBefore,
                finalPlayer.Experience,
                entryGroundItemIds,
                observedNewDropEntityIds,
                [defeated],
                work.AttackRefusals.ToArray(),
                work.Used);
            WriteEvidence(directory, "native-progression-l2-combat.json", new {
                Passed = true,
                evidence.MonsterEntityId,
                evidence.MonsterId,
                evidence.ExperienceBefore,
                evidence.ExperienceAfter,
                evidence.EntryGroundItemIds,
                evidence.ObservedNewDropEntityIds,
                evidence.Kills,
                evidence.AttackRefusals,
                evidence.WorkUsed,
            });
            return evidence;
        }

        throw new InvalidOperationException(
            "Native progression L2 exhausted the shared combat-work limit before the first target defeat: "
            + $"combatWork={work.Used}/{NativeCombatWorkBudget.MaximumWork}, "
            + $"reselectionYields={reselectionYields}/{NativeGearHuntMaximumReselectionYields}, "
            + $"doors={operatedDoors.Count}/{NativeMaximumDoorOperations}, "
            + $"entryItems=[{string.Join(",", entryGroundItemIds)}], "
            + $"refusals=[{string.Join(";", work.AttackRefusals)}].");
    }
}

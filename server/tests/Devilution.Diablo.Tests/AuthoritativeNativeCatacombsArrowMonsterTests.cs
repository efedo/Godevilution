using System.Reflection;
using Devilution.Engine;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeNativeCatacombsArrowTests;

namespace Devilution.Diablo.Tests;

/** Production-leaf coverage for monster impacts from an owned Catacombs Arrow. */
public sealed class AuthoritativeNativeCatacombsArrowMonsterTests
{
    private static readonly MethodInfo ResolveMonsterArrowHitMethod = typeof(DiabloCombatCommandProcessor)
        .GetMethod("ResolveNativeCatacombsArrowMonsterHit", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("The native Catacombs Arrow monster-hit leaf is missing.");

    private static readonly MethodInfo EffectiveMonsterLevelMethod = typeof(DiabloCombatCommandProcessor)
        .GetMethod("GetEffectiveMonsterLevel", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("The native effective monster-level helper is missing.");

    private static readonly MethodInfo EffectiveMonsterArmorClassMethod = typeof(DiabloCombatCommandProcessor)
        .GetMethod("GetEffectiveMonsterArmorClass", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("The native effective monster armor helper is missing.");

    private static readonly MethodInfo NativeTrapExperienceRewardMethod = typeof(DiabloCombatCommandProcessor)
        .GetMethod("GetNativeTrapExperienceReward", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("The native trap experience helper is missing.");

    [Fact(Explicit = true)]
    public void SurvivingHardHitCancelsTheOwnedRangedActionAndValidates()
    {
        using var scenario = CreateScenario(0, 5, 2588, "SkeletonRanged");
        var missileCatalog = scenario.Fixture.Activation.MonsterMissiles
            ?? throw new InvalidOperationException("The qualified C2 fixture has no missile catalog.");
        var target = FindOtherActor(scenario, value => (value.NativeAi is "SkeletonRanged" or "GoatRanged")
            && value.DialogueMessageId == 0
            && value.DialogueState != AuthoritativeMonsterDialogueState.Inquiring
            && value.MaxHitPointsQ6 > (EffectiveMonsterLevel(scenario, value) + 3)
                * DeterministicFixedPointQ6.Scale);
        Assert.True(missileCatalog.TryGet(target.NativeAi!, target.MonsterId, out var targetMissile),
            "The separate ranged target must resolve its own native missile row.");
        target.RestoreHitPointsQ6(target.MaxHitPointsQ6, target.MaxHitPointsQ6);
        var hardHitDamage = EffectiveMonsterLevel(scenario, target) + 3;
        scenario.Source.MinDamage = hardHitDamage;
        scenario.Source.MaxDamage = hardHitDamage;
        var hardHitDamageQ6 = checked(hardHitDamage
            * DeterministicFixedPointQ6.Scale);
        Assert.True(target.HitPointsQ6 > hardHitDamageQ6,
            "The generated ranged actor must survive the prepared hard-hit input.");
        target.IsBerserked = true;
        target.IsPetrified = false;
        scenario.World.StartNativeCatacombsRangedAction(target, targetMissile,
            scenario.Player, attackSeed: 123);
        Assert.True(scenario.World.HasNativeCatacombsRangedAction(target.EntityId));
        var targetCell = scenario.FindClearAdjacentTarget();
        scenario.LaunchArrow(targetCell.X, targetCell.Y);
        var arrow = Assert.Single(scenario.World.CaptureNativeCatacombsArrows());
        var hitChance = Math.Clamp(
            90 - EffectiveMonsterArmorClass(scenario, target) - 1, 5, 95);
        scenario.Random.Restore(FindSeedWithFirstRollBelow(hitChance));

        var hit = ResolveMonsterArrowHit(scenario, arrow, target, distance: 1);

        scenario.World.ValidateNativePopulationState([scenario.Player]);
        Assert.True(hit);
        Assert.True(target.HitPointsQ6 > 0);
        Assert.Equal(target.MaxHitPointsQ6 - hardHitDamageQ6, target.HitPointsQ6);
        Assert.False(target.IsPetrified);
        Assert.True(target.NativeMeleeCursor is { HitRecovery: true });
        Assert.False(scenario.World.HasNativeCatacombsRangedAction(target.EntityId));
        Assert.DoesNotContain(scenario.World.CaptureNativeCatacombsRangedActions(),
            value => value.EntityId == target.EntityId);
    }

    [Fact(Explicit = true)]
    public void SameFactionArrowImpactIsRejectedBeforeTheHitRoll()
    {
        using var scenario = CreateScenario(0, 5, 2588, "SkeletonRanged");
        var target = FindOtherActor(scenario, value =>
            value.IsPlayerMinion == scenario.Source.IsPlayerMinion
            && !value.IsBerserked && !scenario.Source.IsBerserked
            && value.DialogueMessageId == 0
            && value.DialogueState != AuthoritativeMonsterDialogueState.Inquiring);
        Assert.Equal(scenario.Source.IsPlayerMinion, target.IsPlayerMinion);
        scenario.Source.MinDamage = 1;
        scenario.Source.MaxDamage = 1;
        var targetCell = scenario.FindClearAdjacentTarget();
        scenario.LaunchArrow(targetCell.X, targetCell.Y);
        var arrow = Assert.Single(scenario.World.CaptureNativeCatacombsArrows());
        var priorLife = target.HitPointsQ6;
        scenario.Random.Restore(0x27182818);
        var priorRandom = scenario.Random.State;

        var hit = ResolveMonsterArrowHit(scenario, arrow, target, distance: 1);

        scenario.World.ValidateNativePopulationState([scenario.Player]);
        Assert.False(hit);
        Assert.Equal(priorLife, target.HitPointsQ6);
        Assert.Equal(priorRandom, scenario.Random.State);
    }

    [Fact(Explicit = true)]
    public void OpposingLowDamageArrowHitUsesTheNativeRollsWithoutStartingHardRecovery()
    {
        using var scenario = CreateScenario(0, 5, 2588, "SkeletonRanged");
        scenario.Source.MinDamage = 1;
        scenario.Source.MaxDamage = 1;
        var target = FindOtherActor(scenario, value =>
            !value.IsUnique && !value.IsPlayerMinion && !value.BerserkImmune
            && value.DialogueMessageId == 0
            && value.DialogueState != AuthoritativeMonsterDialogueState.Inquiring
            && value.NativeAi is not ("Golem" or "Gargoyle" or "Rhino" or "Sneak"
                or "Stalker" or "Unseen" or "IllWeav"));
        target.IsBerserked = true;
        target.IsPetrified = false;
        Assert.True(target.HitPointsQ6 > DeterministicFixedPointQ6.Scale);
        var armorClass = EffectiveMonsterArmorClass(scenario, target);
        var hitChance = Math.Clamp(90 - armorClass - 1, 5, 95);
        var hitSeed = FindSeedWithFirstRollBelow(hitChance);
        var targetCell = scenario.FindClearAdjacentTarget();
        scenario.LaunchArrow(targetCell.X, targetCell.Y);
        var arrow = Assert.Single(scenario.World.CaptureNativeCatacombsArrows());
        scenario.Random.Restore(hitSeed);
        var expectedRandom = new DeterministicLegacyRandom(hitSeed);
        _ = expectedRandom.GenerateRnd(100);
        _ = expectedRandom.RandomIntBetween(1, 1);
        expectedRandom.AdvanceRndSeed();
        var lifeBefore = target.HitPointsQ6;

        var hit = ResolveMonsterArrowHit(scenario, arrow, target, distance: 1);

        scenario.World.ValidateNativePopulationState([scenario.Player]);
        Assert.True(hit);
        Assert.Equal(lifeBefore - DeterministicFixedPointQ6.Scale, target.HitPointsQ6);
        Assert.Null(target.NativeMeleeCursor);
        Assert.Equal(expectedRandom.State, scenario.Random.State);
    }

    [Fact(Explicit = true)]
    public void FractionalFatalArrowInterceptionCommitsTheSharedNativeLootTransaction()
    {
        using var scenario = CreateScenario(0, 5, 2588, "SkeletonRanged");
        // Keep the canonical Warrior identity and loadout, but make XP eligibility explicit.
        scenario.Player.CharacterLevel = 1;
        scenario.Player.Experience = 0;
        scenario.Player.Life = scenario.Player.LifeMaximum;
        Assert.Equal(1U, scenario.Player.CharacterLevel);
        Assert.True(scenario.Player.Life >= 64);
        scenario.Source.MinDamage = 1;
        scenario.Source.MaxDamage = 1;
        var (target, lootPreview) = FindVisibleDropCandidate(scenario);
        var expectedDeathTicks = target.DeathAnimationTicks;
        target.IsBerserked = true;
        target.IsPetrified = true;
        target.RestoreHitPointsQ6(127, target.MaxHitPointsQ6);
        target.RecordNativePlayerHit();
        Assert.Equal(127, target.HitPointsQ6);
        Assert.True(lootPreview.RequiresWorldItem);
        var beforeItems = Assert.IsType<DiabloCathedralItemCheckpoint>(
            scenario.World.CaptureCathedralItems());
        var itemIdsBefore = scenario.World.WorldItems.Keys.ToHashSet();
        var experienceBefore = scenario.Player.Experience;
        var targetCell = scenario.FindClearAdjacentTarget();
        scenario.LaunchArrow(targetCell.X, targetCell.Y);
        var arrow = Assert.Single(scenario.World.CaptureNativeCatacombsArrows());
        var events = new List<DiabloDirectedGameplayEvent>();
        var expectedExperience = Assert.IsType<uint>(NativeTrapExperienceRewardMethod.Invoke(
            scenario.Processor, [scenario.Player, target]));
        Assert.False(target.IsPlayerMinion);
        Assert.True(target.NativePlayerHitRecorded);
        Assert.True(scenario.Player.Life >= 64);
        Assert.Equal(1U, scenario.Player.CharacterLevel);
        Assert.True(target.ExperienceReward > 0);
        Assert.True(expectedExperience > 0,
            $"Native trap XP reward must be eligible before death: target XP={target.ExperienceReward}, "
            + $"effective level={EffectiveMonsterLevel(scenario, target)}, player level={scenario.Player.CharacterLevel}, "
            + $"player life={scenario.Player.Life}, target player-hit={target.NativePlayerHitRecorded}, "
            + $"minion={target.IsPlayerMinion}.");

        var hit = ResolveMonsterArrowHit(scenario, arrow, target, distance: 1, events);

        scenario.World.ValidateNativePopulationState([scenario.Player]);
        Assert.True(hit);
        Assert.Equal(0, target.HitPointsQ6);
        Assert.Equal(expectedDeathTicks, target.DeathTicksRemaining);
        Assert.True(target.NativePlayerHitRecorded);
        Assert.Equal(checked(experienceBefore + expectedExperience), scenario.Player.Experience);
        if (expectedDeathTicks == 0) {
            Assert.False(scenario.World.CombatTargets.ContainsKey(target.EntityId));
            var corpse = Assert.Single(scenario.World.Corpses.Values,
                value => value.EntityId == target.EntityId);
            Assert.Equal((target.LevelId, target.PositionX, target.PositionY),
                (corpse.LevelId, corpse.PositionX, corpse.PositionY));
        } else {
            Assert.Same(target, scenario.World.CombatTargets[target.EntityId]);
            Assert.False(scenario.World.Corpses.ContainsKey(target.EntityId));
        }
        Assert.Contains(events, value => value.Event.Kind == DiabloGameplayEventKind.Damage
            && value.Event.SourceEntityId == scenario.Source.EntityId
            && value.Event.TargetEntityId == target.EntityId
            && value.Event.AmountQ6 == DeterministicFixedPointQ6.Scale);
        var actualDrop = Assert.Single(scenario.World.WorldItems.Values,
            value => !itemIdsBefore.Contains(value.EntityId));
        var expectedProjection = lootPreview.Projection
            ?? throw new InvalidOperationException("The selected loot preview has no world projection.");
        Assert.Equal(expectedProjection.State, actualDrop.State);
        Assert.Equal(expectedProjection.ItemSeed, actualDrop.ItemSeed);
        Assert.Equal(expectedProjection.Price, actualDrop.Price);
        var afterItems = Assert.IsType<DiabloCathedralItemCheckpoint>(
            scenario.World.CaptureCathedralItems());
        var beforeLevel = Assert.Single(beforeItems.Levels, value => value.LevelId == target.LevelId);
        var afterLevel = Assert.Single(afterItems.Levels, value => value.LevelId == target.LevelId);
        Assert.Equal(beforeLevel.ActiveItemCount + 1, afterLevel.ActiveItemCount);
        // The floor checkpoint keeps its generation seed; MonsterDeath restores the loot stream
        // into shared RNG, then consumes the native death-sound draw.
        Assert.Equal(beforeLevel.RandomState, afterLevel.RandomState);
        var expectedSharedRandom = new DeterministicLegacyRandom(lootPreview.FinalRandomState);
        _ = expectedSharedRandom.AdvanceRndSeed();
        Assert.Equal(expectedSharedRandom.State, scenario.Random.State);
    }

    [Fact(Explicit = true)]
    public void OpposingHardArrowHitClearsTheScavengerHealingGoal()
    {
        using var scenario = CreateScenario(0, 5, 2588, "SkeletonRanged");
        var scavenger = FindOtherActor(scenario, value => value.NativeAi == "Scavenger"
            && !value.IsUnique && !value.IsPlayerMinion && !value.BerserkImmune
            && !value.StoneCurseImmune);
        var damage = Math.Max(1, EffectiveMonsterLevel(scenario, scavenger) + 3);
        scenario.Source.MinDamage = damage;
        scenario.Source.MaxDamage = damage;
        scavenger.IsBerserked = true;
        scavenger.IsPetrified = true;
        scavenger.RestoreHitPointsQ6(scavenger.MaxHitPointsQ6, scavenger.MaxHitPointsQ6);
        Assert.True(scavenger.HitPointsQ6 > damage * DeterministicFixedPointQ6.Scale,
            "The prepared Scavenger must survive the hard Arrow hit.");
        scavenger.StartScavengerHealing(7);
        scavenger.SelectScavengerCorpse(0x7F000001);
        Assert.Equal((7, 0x7F000001U),
            (scavenger.ScavengerHealingTicks, scavenger.ScavengerCorpseEntityId));
        var targetCell = scenario.FindClearAdjacentTarget();
        scenario.LaunchArrow(targetCell.X, targetCell.Y);
        var arrow = Assert.Single(scenario.World.CaptureNativeCatacombsArrows());
        var lifeBefore = scavenger.HitPointsQ6;

        var hit = ResolveMonsterArrowHit(scenario, arrow, scavenger, distance: 1);

        scenario.World.ValidateNativePopulationState([scenario.Player]);
        Assert.True(hit);
        Assert.True(scavenger.HitPointsQ6 > 0);
        Assert.Equal(lifeBefore - damage * DeterministicFixedPointQ6.Scale,
            scavenger.HitPointsQ6);
        Assert.Equal((0, 0U),
            (scavenger.ScavengerHealingTicks, scavenger.ScavengerCorpseEntityId));
    }

    private static AuthoritativeCombatTarget FindOtherActor(
        NativeCatacombsArrowTestScenario scenario,
        Func<AuthoritativeCombatTarget, bool> predicate)
        => scenario.World.CombatTargets.Values
            .Where(value => value.EntityId != scenario.Source.EntityId
                && value.LevelId == scenario.Build.Policy.LevelId
                && value.HitPointsQ6 > 0
                && value.NativeInitialization is not null
                && predicate(value))
            .OrderBy(value => value.NativeInitialization!.Binding.NativeSlot)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("The qualified C2 owner has no actor for the requested Arrow test.");

    private static (AuthoritativeCombatTarget Target, AuthoritativeCathedralItemDeathTransaction Preview)
        FindVisibleDropCandidate(NativeCatacombsArrowTestScenario scenario)
    {
        foreach (var target in scenario.World.CombatTargets.Values
            .Where(value => value.EntityId != scenario.Source.EntityId
                && value.LevelId == scenario.Build.Policy.LevelId
                && value.HitPointsQ6 > 0
                && value.MaxHitPointsQ6 >= 127
                && value.ExperienceReward > 0
                && !value.IsUnique
                && !value.IsPlayerMinion
                && !value.StoneCurseImmune
                && value.NativeAi is not ("Rhino" or "Sneak" or "Stalker" or "Unseen" or "IllWeav")
                && value.DialogueMessageId == 0
                && value.DialogueState != AuthoritativeMonsterDialogueState.Inquiring
                && value.NativeInitialization is not null)
            .OrderBy(value => value.NativeInitialization!.Binding.NativeSlot)) {
            AuthoritativeCathedralItemDeathTransaction? preview;
            try {
                preview = scenario.World.PrepareCathedralDeathLoot(
                    target, scenario.Player, [scenario.Player]);
            } catch (NotSupportedException) {
                continue;
            }
            if (preview is { RequiresWorldItem: true, Projection: not null }
                && preview.Loot.Item is { ItemType: not 11 })
                return (target, preview);
        }
        throw new InvalidOperationException("The qualified C2 owner has no ordinary actor with a visible native loot drop.");
    }

    private static bool ResolveMonsterArrowHit(
        NativeCatacombsArrowTestScenario scenario,
        AuthoritativeNativeCatacombsArrowState arrow,
        AuthoritativeCombatTarget target,
        int distance,
        ICollection<DiabloDirectedGameplayEvent>? events = null)
    {
        var result = ResolveMonsterArrowHitMethod.Invoke(scenario.Processor,
            [arrow, target, distance, scenario.Player, events ?? new List<DiabloDirectedGameplayEvent>()]);
        return Assert.IsType<bool>(result);
    }

    private static int EffectiveMonsterLevel(
        NativeCatacombsArrowTestScenario scenario,
        AuthoritativeCombatTarget target)
        => Assert.IsType<int>(EffectiveMonsterLevelMethod.Invoke(scenario.Processor, [target]));

    private static int EffectiveMonsterArmorClass(
        NativeCatacombsArrowTestScenario scenario,
        AuthoritativeCombatTarget target)
        => Assert.IsType<int>(EffectiveMonsterArmorClassMethod.Invoke(scenario.Processor, [target]));

    private static uint FindSeedWithFirstRollBelow(int threshold)
    {
        for (uint seed = 0; seed < 1_000_000; seed++) {
            var candidate = new DeterministicLegacyRandom(seed);
            if (candidate.GenerateRnd(100) < threshold)
                return seed;
        }
        throw new InvalidOperationException("Could not find a hit seed for the prepared native Arrow case.");
    }
}

using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeNativeCatacombsRangedActionTests
{
    [Fact]
    public void NormalAttackReleasesOnEachEffectFrameTickAndCompletesAtGraphicEnd()
    {
        var action = CreateClock(frameCount: 3, ticksPerFrame: 2, special: false);
        var steps = new List<AuthoritativeNativeCatacombsRangedActionStep>();
        for (var tick = 0; tick < 5; tick++) {
            var step = action.Advance();
            steps.Add(step);
            action = step.Action;
            if (step.Completed) break;
        }

        Assert.Equal(5, steps.Count);
        Assert.Equal(new[] { 4 }, steps.Select((step, index) => (step, index))
            .Where(pair => pair.step.Release).Select(pair => pair.index));
        Assert.True(steps[4].Completed);
        Assert.Equal((0, 0), (action.FrameIndex, action.TickCounter));
    }

    [Fact]
    public void SpecialAttackReleasesOnlyOnFinalMetadataFrameAtCounterZero()
    {
        var action = CreateClock(frameCount: 3, ticksPerFrame: 2, special: true);
        var steps = new List<AuthoritativeNativeCatacombsRangedActionStep>();
        for (var tick = 0; tick < 5; tick++) {
            var step = action.Advance();
            steps.Add(step);
            action = step.Action;
            if (step.Completed) break;
        }

        Assert.Equal(5, steps.Count);
        Assert.Equal(new[] { 4 }, steps.Select((step, index) => (step, index))
            .Where(pair => pair.step.Release).Select(pair => pair.index));
        Assert.True(steps[4].Completed);
    }

    [Fact]
    public void SourceEffectFrameIsIndependentFromGraphicFrameCount()
    {
        var skeleton = CreateTarget("SkeletonRanged", normalEffectFrame: 2);
        var normal = AuthoritativeNativeCatacombsRangedAction.Create(
            skeleton,
            new AuthoritativeMonsterMissileDefinition(
                "SkeletonRanged", "Arrow", AuthoritativeProjectileBehavior.MonsterArrow),
            targetEntityId: 904, targetX: 20, targetY: 10,
            attackSeed: 1, populationFingerprint: PopulationFingerprint);
        Assert.Equal((4, 1), (normal.ActionFrameCount, normal.EffectFrame));
        var normalReleases = new List<int>();
        var normalSteps = 0;
        while (true) {
            var step = normal.Advance();
            if (step.Release) normalReleases.Add(normalSteps);
            normal = step.Action;
            normalSteps++;
            if (step.Completed) break;
        }
        Assert.Equal(new[] { 2, 3 }, normalReleases);
        Assert.Equal(7, normalSteps);

        var acid = CreateTarget("Acid", specialEffectFrame: 4);
        var special = AuthoritativeNativeCatacombsRangedAction.Create(
            acid, CreateAcidMissile(), targetEntityId: 905, targetX: 21, targetY: 11, attackSeed: 2,
            populationFingerprint: PopulationFingerprint);
        Assert.Equal((5, 3), (special.ActionFrameCount, special.EffectFrame));
        var specialReleases = new List<int>();
        var specialSteps = 0;
        while (true) {
            var step = special.Advance();
            if (step.Release) specialReleases.Add(specialSteps);
            special = step.Action;
            specialSteps++;
            if (step.Completed) break;
        }
        Assert.Equal(new[] { 9 }, specialReleases);
        Assert.Equal(13, specialSteps);

        var normalMismatch = CreateTarget("SkeletonRanged", normalEffectFrame: 3);
        Assert.Throws<InvalidDataException>(() => normal.ValidateTarget(normalMismatch, PopulationFingerprint));
        Assert.Throws<InvalidDataException>(() => AuthoritativeNativeCatacombsRangedAction.Create(
            CreateTarget("SkeletonRanged", normalEffectFrame: 0),
            new AuthoritativeMonsterMissileDefinition(
                "SkeletonRanged", "Arrow", AuthoritativeProjectileBehavior.MonsterArrow),
            targetEntityId: 907, targetX: 18, targetY: 10,
            attackSeed: 4, populationFingerprint: PopulationFingerprint));
        Assert.Throws<InvalidDataException>(() => AuthoritativeNativeCatacombsRangedAction.Create(
            CreateTarget("Acid", specialEffectFrame: 0), CreateAcidMissile(), targetEntityId: 906,
            targetX: 18, targetY: 10,
            attackSeed: 3, populationFingerprint: PopulationFingerprint));
    }

    [Fact]
    public void TerminalGraphicFrameCompletesBeforeItsRemainingTicks()
    {
        var terminalNormal = CreateClock(frameCount: 4, ticksPerFrame: 3, special: false) with {
            FrameIndex = 3,
            TickCounter = 2,
        };
        var normalStep = terminalNormal.Advance();
        Assert.True(normalStep.Release);
        Assert.True(normalStep.Completed);

        var terminalSpecial = CreateClock(frameCount: 4, ticksPerFrame: 3, special: true) with {
            FrameIndex = 3,
            TickCounter = 2,
        };
        var specialStep = terminalSpecial.Advance();
        Assert.False(specialStep.Release);
        Assert.True(specialStep.Completed);
    }

    [Fact]
    public void CreationAndValidationBindTheActorPopulationAndCompleteMissileMapping()
    {
        var target = CreateTarget("Acid");
        var missile = CreateAcidMissile();
        var action = AuthoritativeNativeCatacombsRangedAction.Create(
            target, missile, targetEntityId: 900, targetX: 21, targetY: 12, attackSeed: 0x12345678,
            populationFingerprint: PopulationFingerprint);

        Assert.Equal(0, action.NativeSlot);
        Assert.Equal(target.EntityId, action.EntityId);
        Assert.Equal(target.MonsterId, action.ContentMonsterId);
        Assert.Equal(target.LevelId, action.LevelId);
        Assert.Equal(22, action.NativeTypeId);
        Assert.Equal(PopulationFingerprint, action.PopulationFingerprint);
        Assert.Equal(0x12345678U, action.AttackSeed);
        Assert.Equal((5, 3, 3), (action.ActionFrameCount, action.TicksPerFrame, action.EffectFrame));
        action.ValidateTarget(target, PopulationFingerprint);
        action.ValidateMissile(missile);

        Assert.Throws<InvalidDataException>(() => action.ValidateTarget(target, DifferentFingerprint));
        Assert.Throws<InvalidDataException>(() => action.ValidateMissile(missile with { ProjectileSpeed = 17 }));
    }

    [Fact]
    public void NormalArrowCreationUsesNormalAnimationMetadataAndKnownControllerOnly()
    {
        var target = CreateTarget("SkeletonRanged");
        var missile = new AuthoritativeMonsterMissileDefinition(
            "SkeletonRanged", "Arrow", AuthoritativeProjectileBehavior.MonsterArrow);
        var action = AuthoritativeNativeCatacombsRangedAction.Create(
            target, missile, targetEntityId: 902, targetX: 20, targetY: 10, attackSeed: 99,
            populationFingerprint: PopulationFingerprint);

        Assert.False(action.UsesSpecialAttack);
        Assert.Equal((4, 2, 1), (action.ActionFrameCount, action.TicksPerFrame, action.EffectFrame));
        action.ValidateTarget(target, PopulationFingerprint);
        action.ValidateMissile(missile);

        var wrongTarget = CreateTarget("Magma");
        Assert.Throws<InvalidDataException>(() => action.ValidateTarget(wrongTarget, PopulationFingerprint));
    }

    [Fact]
    public void NativeZeroAnimationRateClampsToOneAndLegacyInitializationCannotStartC2Action()
    {
        var target = CreateTarget("Magma", attackTicks: 0, specialTicks: 0);
        var action = AuthoritativeNativeCatacombsRangedAction.Create(
            target, CreateMagmaMissile(), targetEntityId: 901, targetX: 20, targetY: 10, attackSeed: 7,
            populationFingerprint: PopulationFingerprint);
        Assert.Equal(1, action.TicksPerFrame);

        var legacy = CreateTarget("Magma", catacombsDepth: 0);
        Assert.Throws<InvalidDataException>(() => AuthoritativeNativeCatacombsRangedAction.Create(
            legacy, CreateMagmaMissile(), targetEntityId: 901, targetX: 20, targetY: 10, attackSeed: 7,
            populationFingerprint: PopulationFingerprint));
    }

    [Fact]
    public void ControllerAdmissionKeepsStormUnsupportedAndSupportsAcidUnique()
    {
        var storm = CreateTarget("Storm");
        var stormMissile = new AuthoritativeMonsterMissileDefinition(
            "Storm", "ThinLightning", AuthoritativeProjectileBehavior.Lightning,
            AuthoritativeMonsterRangedAttackMode.Ranged);
        Assert.False(AuthoritativeNativeCatacombsRangedAction.IsSupportedOrdinaryController(storm, stormMissile));
        Assert.False(AuthoritativeNativeCatacombsRangedAction.IsSupportedSpecialController(storm, stormMissile));

        var acidUnique = CreateTarget("AcidUnique");
        var acidMissile = CreateAcidMissile() with {
            NativeAi = "AcidUnique",
            AttackMode = AuthoritativeMonsterRangedAttackMode.Ranged,
        };
        Assert.True(AuthoritativeNativeCatacombsRangedAction.IsSupportedSpecialController(acidUnique, acidMissile));
        var acidUniqueAction = AuthoritativeNativeCatacombsRangedAction.Create(
            acidUnique, acidMissile, targetEntityId: 906, targetX: 26, targetY: 14,
            attackSeed: 3, populationFingerprint: PopulationFingerprint);
        Assert.True(acidUniqueAction.UsesSpecialAttack);
        Assert.Equal(AuthoritativeMonsterRangedAttackMode.Ranged, acidUniqueAction.AttackMode);
        Assert.Equal(AuthoritativeProjectileBehavior.Acid, acidUniqueAction.ProjectileBehavior);

        var goat = CreateTarget("GoatRanged");
        var goatMissile = new AuthoritativeMonsterMissileDefinition(
            "GoatRanged", "Arrow", AuthoritativeProjectileBehavior.MonsterArrow);
        Assert.True(AuthoritativeNativeCatacombsRangedAction.IsSupportedOrdinaryController(goat, goatMissile));
        Assert.False(AuthoritativeNativeCatacombsRangedAction.IsSupportedOrdinaryController(
            goat, goatMissile with { SpellId = 1 }));
        Assert.False(AuthoritativeNativeCatacombsRangedAction.IsSupportedSpecialController(
            acidUnique, acidMissile with { SpellId = 0 }));
        var goatAction = AuthoritativeNativeCatacombsRangedAction.Create(
            goat, goatMissile, targetEntityId: 907, targetX: 24, targetY: 11,
            attackSeed: 4, populationFingerprint: PopulationFingerprint);
        Assert.False(goatAction.UsesSpecialAttack);
        Assert.Equal((24, 11), (goatAction.TargetX, goatAction.TargetY));
    }

    [Fact]
    public void EffectMetadataIsNotClampedToNativeGraphicFrameCount()
    {
        var target = CreateTarget("SkeletonRanged", normalEffectFrame: 7);
        var action = AuthoritativeNativeCatacombsRangedAction.Create(
            target,
            new AuthoritativeMonsterMissileDefinition(
                "SkeletonRanged", "Arrow", AuthoritativeProjectileBehavior.MonsterArrow),
            targetEntityId: 908, targetX: 15, targetY: 18,
            attackSeed: 5, populationFingerprint: PopulationFingerprint);

        Assert.Equal(4, action.ActionFrameCount);
        Assert.Equal(6, action.EffectFrame);
        Assert.False(action.Advance().Release);
        Assert.Throws<InvalidDataException>(() => (action with { EffectFrame = sbyte.MaxValue }).ValidateShape());
    }

    [Fact]
    public void MalformedFrameAndFingerprintStateIsRejected()
    {
        var valid = CreateClock(frameCount: 3, ticksPerFrame: 2, special: true);
        Assert.Throws<InvalidDataException>(() => (valid with { EffectFrame = -2 }).Advance());
        Assert.Throws<InvalidDataException>(() => (valid with { PopulationFingerprint = "not-a-hash" }).Advance());
        Assert.Throws<InvalidDataException>(() => (valid with { FrameIndex = 3 }).Advance());
    }

    [Fact(Explicit = true)]
    public void CatacombsActualProcessorKeepsAnOffscreenSpecialClockAndEmitsSharedDamageDrawOnce()
    {
        using var fixture = AuthoritativeCathedralNativePopulationIntegrationTests.CreateFixture(0);
        var (world, actor, victim, processor, random, missile) = CreateOwnedScenario(
            fixture, depth: 6, seed: 743271966, nativeAi: "Acid");
        actor.RestoreActiveForTicks(0);
        actor.RestoreRangedAvoidanceMovementState(idleTicks: 13, movementMode: 2);
        world.StartNativeCatacombsRangedAction(actor, missile, victim, attackSeed: 19);

        var expectedRandom = new DeterministicLegacyRandom(random.State);
        var released = 0;
        var completed = false;
        for (ulong tick = 1; tick <= 100; tick++) {
            var action = Assert.Single(world.CaptureNativeCatacombsRangedActions(6));
            var releasesNow = action.FrameIndex == action.EffectFrame
                && (!action.UsesSpecialAttack || action.TickCounter == 0);
            if (releasesNow) {
                // Move the current endpoint at the release boundary; the projectile must use it.
                victim.PositionX = actor.PositionX < 56 ? 108 : 3;
                victim.PositionY = actor.PositionY < 56 ? 108 : 3;
            }

            var beforeRandom = random.State;
            var expectedTarget = (victim.FuturePositionX, victim.FuturePositionY);
            processor.AdvanceTick(tick, [victim]);

            Assert.Equal(0, actor.ActiveForTicks);
            if (releasesNow) {
                released++;
                _ = expectedRandom.AdvanceRndSeed(); // PlaySfxLoc(SpellAcid) precedes AddAcid's damage roll.
                var expectedDamage = expectedRandom.RandomIntBetween(actor.MinDamage, actor.MaxDamage);
                Assert.Equal(expectedRandom.State, random.State);
                var projectile = Assert.Single(world.Projectiles.Values,
                    value => value.SourceEntityId == actor.EntityId);
                Assert.Equal(expectedTarget, (projectile.TargetX, projectile.TargetY));
                Assert.Equal(expectedDamage, projectile.Damage);
            } else {
                Assert.Equal(beforeRandom, random.State);
            }

            if (!world.HasNativeCatacombsRangedAction(actor.EntityId)) {
                completed = true;
                break;
            }
        }

        Assert.True(completed, "The production ranged mode must finish on its native terminal graphic frame.");
        Assert.Equal(1, released);
        Assert.Equal((0, 0), (actor.RangedAvoidanceMovementIdleTicks, actor.RangedAvoidanceMovementMode));
        Assert.Empty(world.CaptureNativeCatacombsRangedActions(6));
    }

    [Fact(Explicit = true)]
    public void CatacombsInactiveGoatCompletionDoesNotDrawDelayOrRedispatchAi()
    {
        using var fixture = AuthoritativeCathedralNativePopulationIntegrationTests.CreateFixture(0);
        var (world, actor, victim, processor, random, missile) = CreateOwnedScenario(
            fixture, depth: 6, seed: 2588, nativeAi: "GoatRanged");
        actor.RestoreActiveForTicks(0);
        actor.RestoreRangedDelay(0);
        world.StartNativeCatacombsRangedAction(actor, missile, victim, attackSeed: 23);

        var initialRandom = random.State;
        var completed = false;
        var releaseObserved = false;
        for (ulong tick = 1; tick <= 100; tick++) {
            processor.AdvanceTick(tick, [victim]);
            Assert.Equal(0, actor.ActiveForTicks);
            Assert.Equal(0, actor.RangedDelayTicks);
            releaseObserved |= world.Projectiles.Values.Any(value => value.SourceEntityId == actor.EntityId);
            if (!world.HasNativeCatacombsRangedAction(actor.EntityId)) {
                completed = true;
                break;
            }
        }

        Assert.True(completed, "The pending offscreen ranged mode must continue to its completion boundary.");
        Assert.True(releaseObserved, "The native GoatRanged effect frame must emit its Arrow even while inactive.");
        Assert.Equal(initialRandom, random.State);
    }

    [Fact(Explicit = true)]
    public void CatacombsSkeletonCompletionClearsItsNativeMovementIdleBand()
    {
        using var fixture = AuthoritativeCathedralNativePopulationIntegrationTests.CreateFixture(0);
        var (world, actor, victim, processor, random, missile) = CreateOwnedScenario(
            fixture, depth: 5, seed: 2588, nativeAi: "SkeletonRanged");
        actor.RestoreActiveForTicks(0);
        actor.RestoreSkeletonRangedMovementState(idleTicks: 13, movementMode: 2);
        world.StartNativeCatacombsRangedAction(actor, missile, victim, attackSeed: 29);
        var initialRandom = random.State;

        var completed = false;
        var releaseBoundaryObserved = false;
        for (ulong tick = 1; tick <= 100; tick++) {
            var action = Assert.Single(world.CaptureNativeCatacombsRangedActions(5));
            var releasesNow = action.FrameIndex == action.EffectFrame;
            var projectilesBefore = world.Projectiles.Count;
            processor.AdvanceTick(tick, [victim]);
            Assert.Equal(0, actor.ActiveForTicks);
            if (releasesNow)
                releaseBoundaryObserved |= world.Projectiles.Count == projectilesBefore + 1
                    && world.Projectiles.Values.Any(value => value.SourceEntityId == actor.EntityId);
            if (!world.HasNativeCatacombsRangedAction(actor.EntityId)) {
                completed = true;
                break;
            }
        }

        Assert.True(completed, "The native SkeletonBow mode must finish at the graphic boundary.");
        Assert.True(releaseBoundaryObserved, "The production clock must visit its native Arrow effect frame before Stand.");
        Assert.Equal(initialRandom, random.State);
        Assert.Equal((0, 0), (actor.SkeletonRangedMovementIdleTicks, actor.SkeletonRangedMovementMode));
    }

    [Fact(Explicit = true)]
    public void CatacombsActualProcessorAdvancesReservedBarrelEmergenceBeforeOffscreenAi()
    {
        using var fixture = AuthoritativeCathedralNativePopulationIntegrationTests.CreateFixture(2);
        var build = fixture.CreateBuild(5, 5, dungeonSeed: 2588);
        var missiles = fixture.Activation.MonsterMissiles!;
        var world = DiabloWorldState.CreateNativeCatacombsFloor(build, fixture.Items, fixture.Spells, missiles);
        var barrelState = Assert.Single(world.CaptureNativeBarrels()!, value => value.ObjectSlot == 77);
        Assert.Equal(57, barrelState.NativeTypeId);
        Assert.Equal(9, barrelState.Var2);
        Assert.Equal(4, barrelState.Var4);
        Assert.Equal(DiabloNativeBarrelActorState.Reserved, barrelState.ActorState);

        var barrel = world.Objects[barrelState.EntityId];
        var player = AuthoritativeCatacombsMonsterRuntimeAdapterTests.CreateProcessorPlayer(90, 90);
        player.EntityId = 0x70000002;
        player.LevelId = 5;
        Assert.True(world.TryGetNativeBarrelFacts(barrel, out var facts));
        Assert.Contains(true, world.GetNativeBarrelSpawnAvailability(barrel, [player]));

        var mutation = world.TryBreakNativeCathedralBarrel(
            barrel, player, [player], 0x5A17C0DE, () => 0x7F000001);
        Assert.NotNull(mutation);
        Assert.NotNull(mutation!.Operation.Spawn);
        var emergingState = Assert.Single(world.CaptureNativeBarrels()!, value => value.ObjectSlot == 77);
        Assert.Equal(DiabloNativeBarrelActorState.Emerging, emergingState.ActorState);
        var actor = world.CombatTargets[emergingState.LinkedMonsterEntityId];
        actor.RestoreActiveForTicks(0);
        foreach (var other in world.CombatTargets.Values.Where(value => value.EntityId != actor.EntityId))
            other.IsPetrified = true;
        var farCorner = new[] { (X: 0, Y: 0), (X: 0, Y: 111), (X: 111, Y: 0), (X: 111, Y: 111) }
            .OrderByDescending(value => (long)(value.X - actor.PositionX) * (value.X - actor.PositionX)
                + (long)(value.Y - actor.PositionY) * (value.Y - actor.PositionY))
            .First();
        player.PositionX = farCorner.X;
        player.PositionY = farCorner.Y;

        var random = new DeterministicLegacyRandom(mutation.Operation.FinalWorldRandomState);
        var processor = new DiabloCombatCommandProcessor(world, fixture.Spells,
            new AuthoritativeCombatRules(10, 1, 100), random,
            monsterMissiles: missiles,
            monsterTypes: fixture.Activation.TrustedOptions!.MonsterTypes);
        var before = Assert.Single(world.CaptureNativeBarrels()!, value => value.ObjectSlot == 77);
        var positionBefore = (actor.PositionX, actor.PositionY, actor.FuturePositionX, actor.FuturePositionY);

        processor.AdvanceTick(1, [player]);

        var after = Assert.Single(world.CaptureNativeBarrels()!, value => value.ObjectSlot == 77);
        Assert.Equal(DiabloNativeBarrelActorState.Emerging, after.ActorState);
        Assert.True((after.SpecialFrameIndex, after.SpecialTickCounter)
            != (before.SpecialFrameIndex, before.SpecialTickCounter));
        Assert.Equal(positionBefore, (actor.PositionX, actor.PositionY, actor.FuturePositionX, actor.FuturePositionY));
        Assert.False(actor.IsMoving);
        Assert.Equal(0, actor.ActiveForTicks);

        Assert.True(missiles.TryGet("SkeletonRanged", actor.MonsterId, out var rangedMissile));
        Assert.Throws<InvalidDataException>(() => world.StartNativeCatacombsRangedAction(
            actor, rangedMissile, player, attackSeed: 31));
        Assert.False(world.HasNativeCatacombsRangedAction(actor.EntityId));
        var endTick = checked((ulong)(facts.NativeSpecialFrameCount * Math.Max(1, facts.NativeSpecialTicksPerFrame) + 3));
        for (ulong tick = 2; tick <= endTick
            && world.GetNativeBarrelState(77)!.ActorState == DiabloNativeBarrelActorState.Emerging; tick++)
            processor.AdvanceTick(tick, [player]);
        Assert.Equal(DiabloNativeBarrelActorState.Active, world.GetNativeBarrelState(77)!.ActorState);
        var activeBarrel = Assert.Single(world.CaptureNativeBarrels()!, value => value.ObjectSlot == 77);
        Assert.Equal(DiabloNativeBarrelActorState.Active, activeBarrel.ActorState);
        Assert.Equal(actor.EntityId, activeBarrel.LinkedMonsterEntityId);
        Assert.Equal("SkeletonRanged", actor.NativeAi);
        var actorInitialization = actor.NativeInitialization
            ?? throw new InvalidDataException("The spawned reservation must retain its native initialization.");
        Assert.Equal(3, actorInitialization.InitialState.Ai);
        Assert.False(actor.IsMoving);
        Assert.Null(actor.NativeMeleeCursor);
        Assert.True(actor.HitPointsQ6 > 0);
        Assert.Equal(0, actor.ActiveForTicks);
        Assert.False(world.HasNativeCatacombsMonsterPhase(actor.EntityId));
        var initialEmergence = world.CaptureNativeInitialSpecialStand()
            .SingleOrDefault(value => value.EntityId == actor.EntityId);
        Assert.True(initialEmergence is null || initialEmergence.Completed);
        Assert.Equal(actorInitialization, build.Policy.Bindings.GetNativeBarrelReservation(77));
        Assert.True(build.Policy.Bindings.IsNativeBarrelReservationSpawned(
            actorInitialization.Binding.NativeSlot, actor.EntityId, world));

        world.StartNativeCatacombsRangedAction(actor, rangedMissile, player, attackSeed: 37);
        Assert.Equal(actor.NativeInitialization!.Binding.NativeSlot,
            Assert.Single(world.CaptureNativeCatacombsRangedActions(5)).NativeSlot);
        world.ValidateNativePopulationState([]);
    }

    [Theory(Explicit = true)]
    [InlineData("SkeletonRanged", 5, 2588U, true)]
    [InlineData("Acid", 6, 743271966U, false)]
    public void CatacombsOwnerRejectsMissileRowsOutsideTheNativeControllerSpellContract(
        string nativeAi, int depth, uint seed, bool ordinary)
    {
        using var fixture = AuthoritativeCathedralNativePopulationIntegrationTests.CreateFixture(0);
        var build = fixture.CreateBuild(depth, checked((uint)depth), dungeonSeed: seed);
        var source = fixture.Activation.MonsterMissiles!;
        var knownAcidSpellId = source.Definitions.Single(row => row.NativeAi == "Acid").SpellId;
        Assert.NotEqual(0U, knownAcidSpellId);
        var changed = new AuthoritativeMonsterMissileCatalog(source.Definitions.Select(row =>
            row.NativeAi == nativeAi ? row with { SpellId = ordinary ? knownAcidSpellId : 0U } : row));

        Assert.Throws<InvalidDataException>(() => DiabloWorldState.CreateNativeCatacombsFloor(
            build, fixture.Items, fixture.Spells, changed));
    }

    private static (
        DiabloWorldState World,
        AuthoritativeCombatTarget Actor,
        AuthoritativePlayerState Victim,
        DiabloCombatCommandProcessor Processor,
        DeterministicLegacyRandom Random,
        AuthoritativeMonsterMissileDefinition Missile) CreateOwnedScenario(
        AuthoritativeCathedralNativePopulationIntegrationTests.QualifiedCatacombsBuilderFixture fixture,
        int depth,
        uint seed,
        string nativeAi)
    {
        var build = fixture.CreateBuild(depth, checked((uint)depth), dungeonSeed: seed);
        var missiles = fixture.Activation.MonsterMissiles
            ?? throw new InvalidDataException("The Catacombs fixture requires a validated ranged catalog.");
        var world = DiabloWorldState.CreateNativeCatacombsFloor(build, fixture.Items, fixture.Spells, missiles);
        var actor = world.CombatTargets.Values.Where(value => value.LevelId == (uint)depth
                && value.NativeAi == nativeAi)
            .OrderBy(value => value.NativeInitialization!.Binding.NativeSlot)
            .First();
        foreach (var other in world.CombatTargets.Values.Where(value => value.EntityId != actor.EntityId))
            other.IsPetrified = true;
        world.ValidateNativePopulationState([]);
        if (!missiles.TryGet(nativeAi, actor.MonsterId, out var missile))
            throw new InvalidDataException("The qualified C2 actor has no exact missile binding.");

        var victim = AuthoritativeCatacombsMonsterRuntimeAdapterTests.CreateProcessorPlayer(90, 90);
        victim.EntityId = 0x70000001;
        victim.LevelId = checked((uint)depth);
        var random = new DeterministicLegacyRandom(0x12345678);
        var processor = new DiabloCombatCommandProcessor(world, fixture.Spells,
            new AuthoritativeCombatRules(10, 1, 100), random,
            monsterMissiles: missiles,
            monsterTypes: fixture.Activation.TrustedOptions!.MonsterTypes);
        return (world, actor, victim, processor, random, missile);
    }

    private const string MetadataFingerprint = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string PopulationFingerprint = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
    private const string DifferentFingerprint = "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC";
    private const string MissileFingerprint = "DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD";

    private static AuthoritativeNativeCatacombsRangedAction CreateClock(int frameCount, int ticksPerFrame, bool special)
        => new(0, 501, 1, 5, 22, special ? "Acid" : "SkeletonRanged",
            MetadataFingerprint, PopulationFingerprint, MissileFingerprint, special ? "Acid" : "Arrow",
            special ? AuthoritativeProjectileBehavior.Acid : AuthoritativeProjectileBehavior.MonsterArrow,
            special ? AuthoritativeMonsterRangedAttackMode.RangedAvoidance : AuthoritativeMonsterRangedAttackMode.Ranged,
            special ? 3U : 0U, 900, 10, 12, special, 0x12345678, frameCount, ticksPerFrame, frameCount - 1, 0, 0);

    private static AuthoritativeMonsterMissileDefinition CreateAcidMissile()
        => new("Acid", "Acid", AuthoritativeProjectileBehavior.Acid,
            AuthoritativeMonsterRangedAttackMode.RangedAvoidance, SpellId: 1);

    private static AuthoritativeMonsterMissileDefinition CreateMagmaMissile()
        => new("Magma", "MagmaBall", AuthoritativeProjectileBehavior.MonsterMagic,
            AuthoritativeMonsterRangedAttackMode.RangedAvoidance, SpellId: 1);

    private static AuthoritativeCombatTarget CreateTarget(
        string nativeAi,
        int catacombsDepth = 5,
        int attackTicks = 2,
        int specialTicks = 3,
        int normalEffectFrame = 2,
        int specialEffectFrame = 4)
    {
        var initial = new AuthoritativeCathedralPopulationMonsterState {
            Slot = 0,
            Type = 22,
            LevelType = 0,
            Mode = 0,
            Goal = 1,
            Tile = new(10, 10),
            Future = new(10, 10),
            Old = new(10, 10),
            Last = new(10, 10),
            Temp = new(10, 10),
            EnemyPosition = new(10, 10),
            Direction = 0,
            Ai = 3,
            UniqueType = byte.MaxValue,
            HitPoints = 6400,
            MaxHitPoints = 6400,
        };
        var initialization = new AuthoritativeNativeMonsterInitialization(
            MetadataFingerprint, initial, 0, 3, normalEffectFrame,
            catacombsDepth == 0 ? 0 : specialEffectFrame,
            new(0, 22, byte.MaxValue, 501, 1, 5, string.Empty), false) {
            CatacombsDepth = catacombsDepth,
            NativeAttackFrameCount = 4,
            NativeAttackTicksPerFrame = attackTicks,
            NativeSpecialAttackFrameCount = catacombsDepth == 0 ? 0 : 5,
            NativeSpecialAttackTicksPerFrame = catacombsDepth == 0 ? 0 : specialTicks,
        };
        var target = new AuthoritativeCombatTarget(501, 10, 10, 100,
            maxHitPoints: 100, levelId: 5, monsterId: 1, nativeAi: nativeAi);
        target.RestoreNativeInitialization(initialization, 0, 0);
        return target;
    }
}

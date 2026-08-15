using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class DiabloSimulationTests
{
    [Fact]
    public void CurrentContentSchemaIncludesTrapDisarmCapability()
    {
        Assert.Equal(
            new ContentSchemaIdentity("devilution.diablo", "1.194.0"),
            DiabloContentSchema.Current);
    }

    [Fact]
    public void SimulationAppliesContentMaximumAttributeToLivePlayers()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatRules = new AuthoritativeCombatRules(
                    10,
                    1,
                    100,
                    maximumAttribute: 125),
            });
        var player = CreatePlayer();
        player.Equipment.Add(new EquippedStoreItem(
            4,
            42,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                PlusStrength = 200,
            }));

        simulation.AdvanceStatusEffects(player, 1);

        Assert.Equal(125, player.Attributes.Strength.Current);
        Assert.Equal(125, player.MaximumAttribute);
    }

    [Fact]
    public void SnapshotRoundTripsMonsterOwnedCounselorChargedBolt()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    1,
                    0,
                    20,
                    monsterId: 108,
                    nativeAi: "Counselor",
                    intelligence: 1)],
                Projectiles = [new AuthoritativeProjectile(
                    50,
                    9,
                    7,
                    30,
                    0,
                    1,
                    0,
                    7,
                    0,
                    3,
                    AuthoritativeDamageType.Lightning,
                    0,
                    16) {
                    Behavior = AuthoritativeProjectileBehavior.MonsterChargedBolt,
                    OriginalDirection = 0,
                    PathIndex = 2,
                    PathTicks = 8,
                    Speed = 8,
                    ImpactLifetime = 1,
                    ResistantDamageDivisor = 1,
                }],
            });

        var snapshot = simulation.CaptureSnapshot(CreatePlayer(), 3);
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules));

        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var projectile = Assert.Single(replacement.WorldState.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterChargedBolt, projectile.Behavior);
        Assert.Equal((9U, 7U, 8), (
            projectile.SourceEntityId,
            projectile.TargetEntityId,
            projectile.Speed));
        Assert.Equal(2, projectile.PathIndex);
        Assert.Equal(8, projectile.PathTicks);
    }

    [Fact]
    public void SnapshotRoundTripsMonsterArrowWithZeroSpellIdentity()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    1,
                    0,
                    20,
                    nativeAi: "SkeletonRanged")],
                Projectiles = [new AuthoritativeProjectile(
                    51,
                    9,
                    7,
                    0,
                    0,
                    1,
                    0,
                    0,
                    0,
                    2,
                    AuthoritativeDamageType.Physical,
                    0,
                    1,
                    allowZeroSpellId: true) {
                    Behavior = AuthoritativeProjectileBehavior.MonsterArrow,
                    Speed = 1,
                    ImpactLifetime = 1,
                    ResistantDamageDivisor = 1,
                }],
            });

        var snapshot = simulation.CaptureSnapshot(CreatePlayer(), 3);
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules));

        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var projectile = Assert.Single(replacement.WorldState.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.MonsterArrow, projectile.Behavior);
        Assert.Equal((0U, 9U, 7U, 1), (
            projectile.SpellId,
            projectile.SourceEntityId,
            projectile.TargetEntityId,
            projectile.Speed));
    }

    [Fact]
    public void ModuleRegistersRulesAndSimulationFactoryCapabilities()
    {
        var module = new DiabloModule();
        var registry = new GameplayModuleRegistry();
        registry.Load([module], CreateContext());

        Assert.Same(module.Rules, registry.GetRequiredCapability<IDiabloStoreRules>());
        Assert.Same(
            DiabloSimulationFactory.Instance,
            registry.GetRequiredCapability<IDiabloSimulationFactory>());
        Assert.Same(
            module.ContentBootstrap,
            registry.GetRequiredCapability<IDiabloContentBootstrap>());
        Assert.Equal("devilution.diablo", Assert.Single(registry.Modules).Identity.Id);
        Assert.True(module.Identity.Compatibility!.Supports(GameplayEngineApiVersion.Current));
    }

    [Fact]
    public void ContentBootstrapRejectsAnIncompatibleDeclaredSchemaBeforeLoadingTables()
    {
        var manifest = new ContentManifest(
            "other",
            "1",
            [new ContentPack(
                "other",
                "1.0.0",
                [TsvTable.Parse("empty.tsv", "id\nvalue\n")],
                [],
                [],
                new ContentSchemaIdentity("other.game", "1.0.0"))]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            new DiabloModule().ContentBootstrap.Activate(
                ComposedContent.Compose(manifest)));

        Assert.Contains("other.game@1.0.0", exception.Message, StringComparison.Ordinal);
        Assert.Contains("devilution.diablo@1.194.0", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FactoryComposesStoreWorldAndCombatTransitionsOverSharedState()
    {
        var module = new DiabloModule();
        var registry = new GameplayModuleRegistry();
        registry.Load([module], CreateContext());
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [new StoreItem(0, 42, 10)]);
        var target = new AuthoritativeCombatTarget(9, 1, 0, 5);
        var simulation = registry.GetRequiredCapability<IDiabloSimulationFactory>().Create(
            new DiabloSimulationOptions(
                catalog,
                registry.GetRequiredCapability<IDiabloStoreRules>()) {
                CombatTargets = [target],
            });
        simulation.WorldState.ValidateInitialPlayerPosition(0, 0, 0);
        var player = CreatePlayer();

        Assert.True(simulation.Execute(player, new DiabloOpenStoreIntent(1), 1).Succeeded);
        Assert.True(simulation.Execute(player, new DiabloPurchaseIntent(1, 0), 2).Succeeded);
        Assert.True(simulation.Execute(player, new DiabloAttackIntent(9), 3).Succeeded);

        Assert.Equal(90U, player.Gold);
        Assert.Single(player.Inventory);
        Assert.Equal(0, target.HitPoints);
        Assert.Equal(100U, player.Experience);
        Assert.DoesNotContain(9U, simulation.WorldState.CombatTargets.Keys);
        Assert.Equal(9U, Assert.Single(simulation.WorldState.Corpses).Key);
    }

    [Fact]
    public void ProceduralEncounterPolicyMaterializesASeedStableSetWhenEnteringANewLevel()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        world.AddLevel(new AuthoritativeLevel(
            2,
            4,
            4,
            blockedCells: Enumerable.Range(3, 13)));
        var planner = new AuthoritativeMonsterSpawnPlanner(
            world,
            new AuthoritativeMonsterTypeCatalog([
                new AuthoritativeMonsterTypeDefinition(
                    2,
                    2,
                    "Generated Monster",
                    1,
                    2,
                    1,
                    10,
                    10,
                    0,
                    1,
                    2,
                    0,
                    0,
                    false,
                    false,
                    false),
            ]),
            new AuthoritativeLevelGenerationCatalog([
                new AuthoritativeLevelGenerationRule(1, 2, 2, 100, 0),
            ]));
        var portal = new AuthoritativePortal(5, 1, 0, 0, 2, 0, 0);
        var initialTargets = planner.PlanCombatTargets(1, 0x1234, false, 1);
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                Portals = [portal],
                CombatTargets = initialTargets,
                MonsterSpawnPlanner = planner,
                ProceduralMonsterSpawns = new DiabloMonsterSpawnConfiguration(1),
                RandomSeed = 0x1234,
            });
        var player = CreatePlayer();
        player.LevelId = 1;
        player.PositionX = 0;
        player.PositionY = 0;

        var result = simulation.Execute(
            player,
            new DiabloUsePortalIntent(portal.PortalId),
            1,
            [player]);

        Assert.True(result.Succeeded);
        Assert.Equal(2U, player.LevelId);
        Assert.Equal(2, simulation.WorldState.CombatTargets.Count);
        var generated = Assert.Single(
            simulation.WorldState.CombatTargets.Values,
            target => target.LevelId == 2);
        Assert.True(world.IsWalkable(2, generated.PositionX, generated.PositionY));
        Assert.NotEqual(AuthoritativeMonsterSpawnPlanner.FirstGeneratedMonsterEntityId, generated.EntityId);
    }

    [Fact]
    public void DefeatingConfiguredMonsterAdvancesAndCompletesQuest()
    {
        var target = new AuthoritativeCombatTarget(9, 1, 0, 5, monsterId: 12);
        var quest = new AuthoritativeQuestState(
            3001,
            0,
            1,
            completionMonsterId: 12);
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [target],
                Quests = [quest],
            });
        var player = CreatePlayer();

        Assert.True(simulation.Execute(player, new DiabloAttackIntent(9), 1).Succeeded);

        Assert.Equal(1U, quest.Progress);
        Assert.True(quest.Completed);
        Assert.False(quest.Active);
        Assert.True(quest.Logged);
    }

    [Fact]
    public void SnapshotRoundTripsMonsterDeathAnimationState()
    {
        var sourceTarget = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            5,
            deathAnimationTicks: 3,
            experienceReward: 77,
            toHit: 61,
            toHitSpecial: 43,
            nativeAi: "SkeletonRanged");
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules) {
                CombatTargets = [sourceTarget],
            });
        var sourcePlayer = CreatePlayer();
        Assert.True(simulation.Execute(sourcePlayer, new DiabloAttackIntent(9), 1).Succeeded);
        simulation.AdvanceTick(2, [sourcePlayer]);
        var snapshot = simulation.CaptureSnapshot(sourcePlayer, 4);

        var replacementTarget = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            5,
            deathAnimationTicks: 3,
            experienceReward: 77,
            toHit: 61,
            toHitSpecial: 43,
            nativeAi: "SkeletonRanged");
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules) {
                CombatTargets = [replacementTarget],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal(3, restored.DeathAnimationTicks);
        Assert.Equal(77U, restored.ExperienceReward);
        Assert.Equal((61, 43), (restored.ToHit, restored.ToHitSpecial));
        Assert.Equal("SkeletonRanged", restored.NativeAi);
        Assert.Equal(2, restored.DeathTicksRemaining);
        Assert.Equal(0, restored.HitPoints);
    }

    [Fact]
    public void SnapshotRoundTripsRhinoChargePositionAndPlayerKnockback()
    {
        var monster = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            20,
            attackDamage: 1,
            aggroRange: 8,
            minDamageSpecial: 5,
            maxDamageSpecial: 32,
            toHitSpecial: 100,
            nativeAi: "Rhino",
            intelligence: 3);
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [monster],
            });
        var player = CreatePlayer();
        player.PositionX = 7;
        player.PositionY = 0;

        simulation.AdvanceTick(1, [player]);
        var snapshot = simulation.CaptureSnapshot(player, 1);
        var savedMonster = Assert.Single(snapshot.Monsters);
        Assert.Equal((6, 0), (savedMonster.PositionX, savedMonster.PositionY));
        Assert.Equal(3, savedMonster.Intelligence);
        Assert.Equal((8, 0), (snapshot.Player.PositionX, snapshot.Player.PositionY));

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    1,
                    0,
                    20,
                    attackDamage: 1,
                    aggroRange: 8,
                    minDamageSpecial: 5,
                    maxDamageSpecial: 32,
                    toHitSpecial: 100,
                    nativeAi: "Rhino",
                    intelligence: 3)],
            });
        var restoredPlayerState = CreatePlayer();
        replacement.RestoreSnapshot(restoredPlayerState, snapshot);

        var restoredMonster = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal((6, 0), (restoredMonster.PositionX, restoredMonster.PositionY));
        var restoredPlayer = replacement.CaptureSnapshot(restoredPlayerState, 1).Player;
        Assert.Equal((8, 0), (restoredPlayer.PositionX, restoredPlayer.PositionY));
    }

    [Fact]
    public void SnapshotRoundTripsGargoyleSpecialLock()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    1,
                    0,
                    40,
                    maxHitPoints: 100,
                    attackDamage: 1,
                    aggroRange: 8,
                    nativeAi: "Fallen",
                    intelligence: 2,
                    gargoyleSpecialActive: true)],
            });
        var player = CreatePlayer();
        player.PositionX = 8;
        player.PositionY = 0;

        var snapshot = simulation.CaptureSnapshot(player, 1);
        Assert.True(Assert.Single(snapshot.Monsters).GargoyleSpecialActive);

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    1,
                    0,
                    40,
                    maxHitPoints: 100,
                    attackDamage: 1,
                    aggroRange: 8,
                    nativeAi: "Fallen",
                    intelligence: 2)],
            });
        var restoredPlayer = CreatePlayer();
        replacement.RestoreSnapshot(restoredPlayer, snapshot);

        Assert.True(Assert.Single(replacement.WorldState.CombatTargets).Value.GargoyleSpecialActive);
    }

    [Fact]
    public void SnapshotRoundTripsFallenFearState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    nativeAi: "Zombie",
                    monsterLevel: 3,
                    fallenFearTicks: 5,
                    fallenFearDirection: 7)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        var savedMonster = Assert.Single(snapshot.Monsters);
        Assert.Equal((5, 7), (savedMonster.FallenFearTicks, savedMonster.FallenFearDirection));

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    nativeAi: "Zombie",
                    monsterLevel: 3)],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal((5, 7), (restored.FallenFearTicks, restored.FallenFearDirection));
    }

    [Fact]
    public void SnapshotRoundTripsSneakState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    nativeAi: "Zombie",
                    sneakHidden: true,
                    sneakRetreatTicks: 5)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        var savedMonster = Assert.Single(snapshot.Monsters);
        Assert.Equal((true, 5), (savedMonster.SneakHidden, savedMonster.SneakRetreatTicks));

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    nativeAi: "Zombie")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal((true, 5), (restored.SneakHidden, restored.SneakRetreatTicks));
    }

    [Fact]
    public void SnapshotRoundTripsScavengerHealingAndCorpses()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    2,
                    0,
                    4,
                    maxHitPoints: 20,
                    nativeAi: "Zombie",
                    scavengerHealingTicks: 7,
                    scavengerCorpseEntityId: 10)],
                Corpses = [new AuthoritativeCorpse(10, 0, 4, 0)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        var savedMonster = Assert.Single(snapshot.Monsters);
        Assert.Equal((7, 10U), (savedMonster.ScavengerHealingTicks, savedMonster.ScavengerCorpseEntityId));
        Assert.Equal((10U, 4, 0), (
            Assert.Single(snapshot.Corpses).EntityId,
            Assert.Single(snapshot.Corpses).PositionX,
            Assert.Single(snapshot.Corpses).PositionY));

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    2,
                    0,
                    4,
                    maxHitPoints: 20,
                    nativeAi: "Zombie")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal((7, 10U), (restored.ScavengerHealingTicks, restored.ScavengerCorpseEntityId));
        Assert.Equal((4, 0), (
            Assert.Single(replacement.WorldState.Corpses.Values).PositionX,
            Assert.Single(replacement.WorldState.Corpses.Values).PositionY));
    }

    [Fact]
    public void SnapshotRoundTripsBatRetreatState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Zombie",
                    batRetreatTicks: 1,
                    batRetreatDirection: 7)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        var savedMonster = Assert.Single(snapshot.Monsters);
        Assert.Equal((1, 7), (savedMonster.BatRetreatTicks, savedMonster.BatRetreatDirection));

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Zombie")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal((1, 7), (restored.BatRetreatTicks, restored.BatRetreatDirection));
    }

    [Fact]
    public void SnapshotRoundTripsHellfireEquipmentResourceBasesAndEffectiveMaxima()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules));
        var attributes = new PlayerAttributesState(
            new PlayerAttributeState(10, 10),
            new PlayerAttributeState(20, 20),
            new PlayerAttributeState(30, 30),
            new PlayerAttributeState(40, 40));
        var source = new AuthoritativePlayerState(
            0,
            0,
            80 << 6,
            124 << 6,
            124 << 6,
            attributes,
            [new EquippedStoreItem(
                4,
                42,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    PlusStrength = 4,
                    PlusDexterity = 6,
                    ManaToLifePercent = 50,
                })],
            Enumerable.Repeat(-1, 40).ToArray(),
            [],
            0,
            0,
            80 << 6,
            1,
            0,
            0,
            baseLifeMaximum: 80 << 6,
            baseManaMaximum: 124 << 6);
        source.RecalculateEquipmentResources();
        var snapshot = simulation.CaptureSnapshot(source, 1);

        var destination = new AuthoritativePlayerState(
            0,
            0,
            80 << 6,
            124 << 6,
            124 << 6,
            PlayerAttributesState.Zero,
            [],
            Enumerable.Repeat(-1, 40).ToArray(),
            [],
            0,
            0,
            80 << 6,
            1,
            0,
            0,
            baseLifeMaximum: 80 << 6,
            baseManaMaximum: 124 << 6);

        simulation.RestoreSnapshot(destination, snapshot);

        Assert.Equal(80 << 6, destination.BaseLifeMaximum);
        Assert.Equal(124 << 6, destination.BaseManaMaximum);
        Assert.Equal(142 << 6, destination.LifeMaximum);
        Assert.Equal(62 << 6, destination.ManaMaximum);
        Assert.Equal(source.Life, destination.Life);
        Assert.Equal(source.Mana, destination.Mana);
        Assert.Equal(source.Equipment, destination.Equipment);
        Assert.Equal(14, destination.Attributes.Strength.Current);
        Assert.Equal(36, destination.Attributes.Dexterity.Current);
    }

    [Fact]
    public void SnapshotRoundTripsNoManaFlagAndPreservesCastAdmission()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules));
        var source = CreatePlayer();
        source.Life = 10;
        source.MemorizedSpells.Add(new MemorizedSpellState(1, 1));
        source.Equipment.Add(new EquippedStoreItem(
            4,
            42,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                Flags = AuthoritativeEquipmentManaRules.NoManaFlag,
            }));
        var snapshot = simulation.CaptureSnapshot(source, 1);
        var destination = CreatePlayer();

        simulation.RestoreSnapshot(destination, snapshot);

        Assert.Equal(
            AuthoritativeEquipmentManaRules.NoManaFlag,
            Assert.Single(destination.Equipment).State.Flags);
        Assert.Equal(new MemorizedSpellState(1, 1), Assert.Single(destination.MemorizedSpells));
        var result = simulation.Execute(
            destination,
            new DiabloCastIntent(1, 0, 0, 0, DiabloCastSourceKind.MemorizedSpell),
            2);

        Assert.False(result.Succeeded);
        Assert.Equal(DiabloRuleFailure.InsufficientResources, result.Failure);
        Assert.Equal(0, destination.Mana);
    }

    [Fact]
    public void SnapshotRoundTripsGetHitAndPreservesMonsterDamageAdjustment()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            10,
            attackDamage: 20,
            aggroRange: 5);
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules) {
                CombatTargets = [target],
            });
        var source = CreatePlayer();
        source.Equipment.Add(new EquippedStoreItem(
            4,
            42,
            AuthoritativeItemState.Empty with {
                StatFlag = true,
                PlusGetHit = -5,
            }));
        var snapshot = simulation.CaptureSnapshot(source, 1);
        var destination = CreatePlayer();

        simulation.RestoreSnapshot(destination, snapshot);
        var deliveries = simulation.AdvanceTick(2, [destination]);

        Assert.Equal(-5, Assert.Single(destination.Equipment).State.PlusGetHit);
        Assert.Equal(35, destination.Life);
        Assert.Equal(15, Assert.Single(deliveries).Event.Amount);
    }

    [Fact]
    public void SnapshotRoundTripsQuestPoolAvailability()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                Quests = [new AuthoritativeQuestState(40, 0, 2, unavailable: true)],
            });
        var snapshot = simulation.CaptureSnapshot(CreatePlayer(), 3);

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules));
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        Assert.True(Assert.Single(replacement.WorldState.Quests.Values).Unavailable);
    }

    [Fact]
    public void SnapshotRoundTripsInitialAndLoggedQuestState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                Quests = [new AuthoritativeQuestState(
                    40,
                    0,
                    2,
                    active: false,
                    logged: true)],
            });
        var snapshot = simulation.CaptureSnapshot(CreatePlayer(), 3);

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules));
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var quest = Assert.Single(replacement.WorldState.Quests.Values);
        Assert.False(quest.Active);
        Assert.True(quest.Logged);
    }

    [Fact]
    public void SnapshotRoundTripsNativeQuestVariablesAndMessage()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                Quests = [new AuthoritativeQuestState(
                    40,
                    0,
                    2,
                    variable1: 2,
                    variable2: 7,
                    messageId: 123,
                    completionMonsterId: 12)],
            });
        var snapshot = simulation.CaptureSnapshot(CreatePlayer(), 3);

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules));
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var quest = Assert.Single(replacement.WorldState.Quests.Values);
        Assert.Equal((2, 7, 123), (quest.Variable1, quest.Variable2, quest.MessageId));
        Assert.Equal(12U, quest.CompletionMonsterId);
    }

    [Fact]
    public void SnapshotRoundTripsUniqueMonsterIdentityAndQuestTrigger()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    16,
                    1,
                    0,
                    20,
                    monsterId: 126,
                    unique: true,
                    uniqueIdentity: "Defiler")],
                Quests = [new AuthoritativeQuestState(
                    3021,
                    0,
                    1,
                    completionUniqueIdentity: "Defiler")],
            });
        var snapshot = simulation.CaptureSnapshot(CreatePlayer(), 3);

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules));
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        Assert.Equal("Defiler", Assert.Single(replacement.WorldState.CombatTargets.Values).UniqueIdentity);
        Assert.Equal("Defiler", Assert.Single(replacement.WorldState.Quests.Values).CompletionUniqueIdentity);

        var legacySnapshot = snapshot with {
            Monsters = snapshot.Monsters
                .Select(monster => monster with { UniqueIdentity = string.Empty })
                .ToArray(),
            Quests = snapshot.Quests
                .Select(quest => quest with { CompletionUniqueIdentity = string.Empty })
                .ToArray(),
        };
        var contentInitialized = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    16,
                    1,
                    0,
                    20,
                    monsterId: 126,
                    unique: true,
                    uniqueIdentity: "Defiler")],
                Quests = [new AuthoritativeQuestState(
                    3021,
                    0,
                    1,
                    completionUniqueIdentity: "Defiler")],
            });
        contentInitialized.RestoreSnapshot(CreatePlayer(), legacySnapshot);

        Assert.Equal("Defiler", Assert.Single(contentInitialized.WorldState.CombatTargets.Values).UniqueIdentity);
        Assert.Equal("Defiler", Assert.Single(contentInitialized.WorldState.Quests.Values).CompletionUniqueIdentity);
    }

    [Fact]
    public void SnapshotCaptureAndRestoreOwnPlayerWorldAndActiveStoreState()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [new StoreItem(0, 42, 10)]);
        var simulation = DiabloSimulationFactory.Instance.Create(new DiabloSimulationOptions(
            catalog,
            DiabloModule.Instance.Rules) {
            CombatTargets = [new AuthoritativeCombatTarget(
                9,
                1,
                0,
                5,
                monsterId: 4,
                moving: true,
                futurePositionX: 1,
                futurePositionY: 1,
                petrified: true)],
            WorldItems = [new AuthoritativeWorldItem(
                20,
                0,
                2,
                0,
                99,
                25,
                AuthoritativeItemState.Empty)],
            Objects = [new AuthoritativeWorldObject(
                30,
                6,
                0,
                3,
                0,
                questId: 40,
                questProgressAmount: 2,
                breakable: true,
                destroyed: true)],
            Quests = [new AuthoritativeQuestState(40, 0, 2, progress: 1)],
            Projectiles = [new AuthoritativeProjectile(
                50,
                7,
                0,
                1,
                0,
                0,
                0,
                1,
                0,
                15,
                AuthoritativeDamageType.Fire,
                0,
                3) {
                Behavior = AuthoritativeProjectileBehavior.Firebolt,
                VelocityX = 1204840,
                VelocityY = 602420,
                OriginalDirection = 7,
                Speed = 26,
                ResistantDamageDivisor = 4,
                LastCollisionTargetEntityId = 9,
            }, new AuthoritativeProjectile(
                51,
                7,
                0,
                3,
                0,
                1,
                0,
                4,
                0,
                0,
                AuthoritativeDamageType.Lightning,
                0,
                254) {
                Behavior = AuthoritativeProjectileBehavior.Lightning,
                StartX = 0,
                StartY = 0,
                VelocityX = 1482880,
                VelocityY = 741440,
                OriginalDirection = 7,
                Speed = 32,
                ResistantDamageDivisor = 4,
                EffectiveSpellLevel = 3,
            }, new AuthoritativeProjectile(
                52,
                7,
                0,
                3,
                0,
                1,
                0,
                0,
                0,
                384,
                AuthoritativeDamageType.Lightning,
                0,
                6) {
                Behavior = AuthoritativeProjectileBehavior.LightningSegment,
                StartX = 0,
                StartY = 0,
                ResistantDamageDivisor = 4,
                EffectiveSpellLevel = 3,
            }, new AuthoritativeProjectile(
                53,
                7,
                0,
                4,
                0,
                0,
                0,
                0,
                0,
                253,
                AuthoritativeDamageType.Magic,
                1,
                18) {
                Behavior = AuthoritativeProjectileBehavior.FlashBottom,
                ResistantDamageDivisor = 4,
                EffectiveSpellLevel = 3,
            }, new AuthoritativeProjectile(
                54,
                7,
                0,
                4,
                0,
                0,
                0,
                0,
                0,
                294,
                AuthoritativeDamageType.Magic,
                1,
                18) {
                Behavior = AuthoritativeProjectileBehavior.FlashTop,
                ResistantDamageDivisor = 4,
                EffectiveSpellLevel = 3,
            }, new AuthoritativeProjectile(
                55,
                7,
                0,
                23,
                0,
                5,
                6,
                5,
                6,
                0,
                AuthoritativeDamageType.Magic,
                0,
                1) {
                Behavior = AuthoritativeProjectileBehavior.Teleport,
                StartX = 5,
                StartY = 6,
            }, new AuthoritativeProjectile(
                56,
                7,
                8,
                32,
                0,
                9,
                10,
                9,
                10,
                0,
                AuthoritativeDamageType.Magic,
                0,
                15) {
                Behavior = AuthoritativeProjectileBehavior.ResurrectionBeam,
            }, new AuthoritativeProjectile(
                57,
                7,
                9,
                8,
                0,
                1,
                1,
                1,
                1,
                0,
                AuthoritativeDamageType.Magic,
                0,
                112) {
                Behavior = AuthoritativeProjectileBehavior.StoneCurse,
                StartX = 1,
                StartY = 1,
                ImpactLifetime = 11,
                EffectiveSpellLevel = 1,
            }],
        });
        var player = CreatePlayer();
        Assert.True(simulation.Execute(player, new DiabloOpenStoreIntent(1), 1).Succeeded);
        Assert.True(simulation.Execute(player, new DiabloPurchaseIntent(1, 0), 2).Succeeded);
        player.AbilitySpellId = 1;
        player.CombatMode = DiabloPlayerCombatMode.Spell;
        player.CombatModeEndTick = 15;
        player.CurrentAction = new AuthoritativePlayerActionState(
            DiabloPlayerActionKind.Cast,
            10,
            12,
            12,
            15) {
            EffectApplied = true,
            TargetEntityId = 9,
            CastIntent = new DiabloCastIntent(
                1,
                9,
                1,
                0,
                DiabloCastSourceKind.Ability),
        };
        player.HeldItem = new OwnedStoreItem(
            0,
            0,
            91,
            25,
            13,
            AuthoritativeItemState.Empty with { ItemType = 1, StatFlag = true });

        var snapshot = simulation.CaptureSnapshot(player, 14);

        var replacementCatalog = new StoreCatalog();
        replacementCatalog.AddStore(1, [new StoreItem(0, 42, 10)]);
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(replacementCatalog, DiabloModule.Instance.Rules));
        var destination = CreatePlayer();
        replacement.RestoreSnapshot(destination, snapshot);

        Assert.Equal(14UL, snapshot.Tick);
        Assert.Equal(90U, destination.Gold);
        Assert.Single(destination.Inventory);
        Assert.Equal(player.HeldItem, destination.HeldItem);
        Assert.Equal(7U, destination.EntityId);
        Assert.NotSame(player.CurrentAction, destination.CurrentAction);
        Assert.Equal(player.CurrentAction, destination.CurrentAction);
        Assert.True(destination.CurrentAction!.EffectApplied);
        Assert.Equal(DiabloCastSourceKind.Ability, destination.CurrentAction.CastIntent!.Source);
        var restoredMonster = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal(4U, restoredMonster.MonsterId);
        Assert.True(restoredMonster.IsMoving);
        Assert.Equal(1, restoredMonster.FuturePositionX);
        Assert.Equal(1, restoredMonster.FuturePositionY);
        Assert.True(restoredMonster.IsPetrified);
        Assert.Equal(99U, Assert.Single(replacement.WorldState.WorldItems).Value.ItemSeed);
        var restoredObject = Assert.Single(replacement.WorldState.Objects).Value;
        Assert.Equal(40U, restoredObject.QuestId);
        Assert.Equal(2U, restoredObject.QuestProgressAmount);
        Assert.True(restoredObject.Breakable);
        Assert.True(restoredObject.Destroyed);
        Assert.Equal(1U, Assert.Single(replacement.WorldState.Quests).Value.Progress);
        var restoredProjectiles = replacement.WorldState.Projectiles.Values
            .OrderBy(projectile => projectile.EntityId)
            .ToArray();
        Assert.Equal(8, restoredProjectiles.Length);
        var restoredProjectile = restoredProjectiles[0];
        Assert.Equal(3U, restoredProjectile.RemainingTicks);
        Assert.Equal(AuthoritativeProjectileBehavior.Firebolt, restoredProjectile.Behavior);
        Assert.Equal((1204840, 602420), (
            restoredProjectile.VelocityX,
            restoredProjectile.VelocityY));
        Assert.Equal(4, restoredProjectile.ResistantDamageDivisor);
        Assert.Equal(9U, restoredProjectile.LastCollisionTargetEntityId);
        var restoredLightningController = restoredProjectiles[1];
        Assert.Equal(AuthoritativeProjectileBehavior.Lightning, restoredLightningController.Behavior);
        Assert.Equal(254U, restoredLightningController.RemainingTicks);
        Assert.Equal(3, restoredLightningController.EffectiveSpellLevel);
        Assert.Equal((1482880, 741440), (
            restoredLightningController.VelocityX,
            restoredLightningController.VelocityY));
        var restoredLightningSegment = restoredProjectiles[2];
        Assert.Equal(AuthoritativeProjectileBehavior.LightningSegment, restoredLightningSegment.Behavior);
        Assert.Equal(384, restoredLightningSegment.Damage);
        Assert.Equal(6U, restoredLightningSegment.RemainingTicks);
        Assert.Equal(3, restoredLightningSegment.EffectiveSpellLevel);
        var restoredFlashBottom = restoredProjectiles[3];
        Assert.Equal(AuthoritativeProjectileBehavior.FlashBottom, restoredFlashBottom.Behavior);
        Assert.Equal(253, restoredFlashBottom.Damage);
        Assert.Equal(18U, restoredFlashBottom.RemainingTicks);
        Assert.Equal(3, restoredFlashBottom.EffectiveSpellLevel);
        var restoredFlashTop = restoredProjectiles[4];
        Assert.Equal(AuthoritativeProjectileBehavior.FlashTop, restoredFlashTop.Behavior);
        Assert.Equal(294, restoredFlashTop.Damage);
        Assert.Equal(18U, restoredFlashTop.RemainingTicks);
        var restoredTeleport = restoredProjectiles[5];
        Assert.Equal(AuthoritativeProjectileBehavior.Teleport, restoredTeleport.Behavior);
        Assert.Equal((5, 6), (restoredTeleport.PositionX, restoredTeleport.PositionY));
        Assert.Equal((5, 6), (restoredTeleport.StartX, restoredTeleport.StartY));
        Assert.Equal(1U, restoredTeleport.RemainingTicks);
        var restoredResurrectionBeam = restoredProjectiles[6];
        Assert.Equal(
            AuthoritativeProjectileBehavior.ResurrectionBeam,
            restoredResurrectionBeam.Behavior);
        Assert.Equal(8U, restoredResurrectionBeam.TargetEntityId);
        Assert.Equal((9, 10), (
            restoredResurrectionBeam.PositionX,
            restoredResurrectionBeam.PositionY));
        Assert.Equal(15U, restoredResurrectionBeam.RemainingTicks);
        var restoredStoneCurse = restoredProjectiles[7];
        Assert.Equal(
            AuthoritativeProjectileBehavior.StoneCurse,
            restoredStoneCurse.Behavior);
        Assert.Equal(9U, restoredStoneCurse.TargetEntityId);
        Assert.Equal(112U, restoredStoneCurse.RemainingTicks);
        Assert.Equal(11, restoredStoneCurse.ImpactLifetime);
        Assert.Empty(replacementCatalog.GetItems(1));
    }

    [Fact]
    public void SnapshotRoundTripsDynamicPortalState()
    {
        var portal = new AuthoritativePortal(
            0x20000001,
            1,
            12,
            10,
            0,
            57,
            40) {
            OwnerEntityId = 7,
            Bidirectional = true,
            RemainingTicks = 88,
        };
        var simulation = DiabloSimulationFactory.Instance.Create(new DiabloSimulationOptions(
            new StoreCatalog(),
            DiabloModule.Instance.Rules) {
            Portals = [portal],
            FallbackWidth = 80,
            FallbackHeight = 80,
        });
        var sourcePlayer = CreatePlayer();
        sourcePlayer.LevelId = 1;
        var snapshot = simulation.CaptureSnapshot(sourcePlayer, 5);
        var restored = DiabloSimulationFactory.Instance.Create(new DiabloSimulationOptions(
            new StoreCatalog(),
            DiabloModule.Instance.Rules) {
            FallbackWidth = 80,
            FallbackHeight = 80,
        });

        var destinationPlayer = CreatePlayer();
        destinationPlayer.LevelId = 1;
        restored.RestoreSnapshot(destinationPlayer, snapshot);

        var restoredPortal = Assert.Single(restored.WorldState.Portals.Values);
        Assert.Equal(portal.PortalId, restoredPortal.PortalId);
        Assert.Equal(portal.RemainingTicks, restoredPortal.RemainingTicks);
        Assert.Equal(portal.OwnerEntityId, restoredPortal.OwnerEntityId);
        Assert.True(restoredPortal.Bidirectional);
    }

    [Fact]
    public void SnapshotRoundTripsFireballImpactState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules) {
                Projectiles = [new AuthoritativeProjectile(
                    0x80000000,
                    7,
                    0,
                    12,
                    0,
                    1,
                    0,
                    4,
                    0,
                    58,
                    AuthoritativeDamageType.Fire,
                    1,
                    9) {
                    Behavior = AuthoritativeProjectileBehavior.Fireball,
                    Phase = AuthoritativeProjectilePhase.Impact,
                    StartX = 0,
                    StartY = 0,
                    TraveledX = 2038960,
                    TraveledY = 1019480,
                    OriginalDirection = 7,
                    Speed = 22,
                    ImpactLifetime = 14,
                    ResistantDamageDivisor = 4,
                    EffectiveSpellLevel = 3,
                    LastCollisionTargetEntityId = 9,
                }],
            });
        var source = CreatePlayer();
        var snapshot = simulation.CaptureSnapshot(source, 5);
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules));
        var destination = CreatePlayer();

        replacement.RestoreSnapshot(destination, snapshot);

        var restored = Assert.Single(replacement.WorldState.Projectiles).Value;
        Assert.Equal(AuthoritativeProjectileBehavior.Fireball, restored.Behavior);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, restored.Phase);
        Assert.Equal((1, 0, 4, 0), (
            restored.PositionX,
            restored.PositionY,
            restored.TargetX,
            restored.TargetY));
        Assert.Equal((2038960, 1019480), (
            restored.TraveledX,
            restored.TraveledY));
        Assert.Equal((58, 22, 14, 4, 3, 9U), (
            restored.Damage,
            restored.Speed,
            restored.ImpactLifetime,
            restored.ResistantDamageDivisor,
            restored.EffectiveSpellLevel,
            restored.LastCollisionTargetEntityId));
        Assert.Equal(9U, restored.RemainingTicks);
    }

    [Fact]
    public void SnapshotRoundTripsFlameWaveControllerAndSegmentState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules) {
                Projectiles = [
                    new AuthoritativeProjectile(
                        0x80000001,
                        7,
                        0,
                        15,
                        1,
                        5,
                        5,
                        8,
                        5,
                        0,
                        AuthoritativeDamageType.Fire,
                        0,
                        1) {
                        Behavior = AuthoritativeProjectileBehavior.FlameWaveController,
                        OriginalDirection = 0,
                    },
                    new AuthoritativeProjectile(
                        0x80000002,
                        13,
                        0,
                        15,
                        1,
                        6,
                        5,
                        6,
                        6,
                        9,
                        AuthoritativeDamageType.Fire,
                        0,
                        255) {
                        Behavior = AuthoritativeProjectileBehavior.FlameWaveSegment,
                        OriginalDirection = 0,
                        Speed = 16,
                        ResistantDamageDivisor = 4,
                        EffectiveSpellLevel = 9,
                    },
                ],
            });
        var snapshot = simulation.CaptureSnapshot(CreatePlayer(), 5);
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules));

        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = replacement.WorldState.Projectiles.Values
            .OrderBy(projectile => projectile.EntityId)
            .ToArray();
        Assert.Equal(2, restored.Length);
        Assert.Equal(AuthoritativeProjectileBehavior.FlameWaveController, restored[0].Behavior);
        Assert.Equal((8, 5, 0U, 1U, 0, 0), (
            restored[0].TargetX,
            restored[0].TargetY,
            restored[0].TargetEntityId,
            restored[0].RemainingTicks,
            restored[0].Speed,
            restored[0].Damage));
        Assert.Equal(AuthoritativeProjectileBehavior.FlameWaveSegment, restored[1].Behavior);
        Assert.Equal((6, 5, 16, 4, 9, 255U, 9), (
            restored[1].PositionX,
            restored[1].PositionY,
            restored[1].Speed,
            restored[1].ResistantDamageDivisor,
            restored[1].EffectiveSpellLevel,
            restored[1].RemainingTicks,
            restored[1].Damage));
    }

    [Fact]
    public void SnapshotRoundTripsInfernoControllerAndSegmentState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules) {
                Projectiles = [
                    new AuthoritativeProjectile(
                        0x80000003,
                        7,
                        0,
                        21,
                        0,
                        5,
                        5,
                        8,
                        5,
                        0,
                        AuthoritativeDamageType.Fire,
                        0,
                        253) {
                        Behavior = AuthoritativeProjectileBehavior.InfernoController,
                        VelocityX = 2097152,
                        OriginalDirection = 2,
                        Speed = 32,
                        PathIndex = 1,
                    },
                    new AuthoritativeProjectile(
                        0x80000004,
                        7,
                        0,
                        21,
                        0,
                        6,
                        5,
                        6,
                        5,
                        36,
                        AuthoritativeDamageType.Fire,
                        0,
                        20) {
                        Behavior = AuthoritativeProjectileBehavior.InfernoSegment,
                        ResistantDamageDivisor = 1,
                    },
                ],
            });
        var snapshot = simulation.CaptureSnapshot(CreatePlayer(), 5);
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules));

        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = replacement.WorldState.Projectiles.Values
            .OrderBy(projectile => projectile.EntityId)
            .ToArray();
        Assert.Equal(2, restored.Length);
        Assert.Equal(AuthoritativeProjectileBehavior.InfernoController, restored[0].Behavior);
        Assert.Equal((32, 253U, 1, 2097152), (
            restored[0].Speed,
            restored[0].RemainingTicks,
            restored[0].PathIndex,
            restored[0].VelocityX));
        Assert.Equal(AuthoritativeProjectileBehavior.InfernoSegment, restored[1].Behavior);
        Assert.Equal((36, 20U, 1), (
            restored[1].Damage,
            restored[1].RemainingTicks,
            restored[1].ResistantDamageDivisor));
    }

    [Fact]
    public void SnapshotRoundTripsChainLightningControllerAndBoltState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules) {
                Projectiles = [
                    new AuthoritativeProjectile(
                        0x80000005,
                        7,
                        0,
                        16,
                        0,
                        5,
                        5,
                        8,
                        5,
                        0,
                        AuthoritativeDamageType.Lightning,
                        0,
                        1) {
                        Behavior = AuthoritativeProjectileBehavior.ChainLightningController,
                        OriginalDirection = 2,
                        EffectiveSpellLevel = 1,
                    },
                    new AuthoritativeProjectile(
                        0x80000006,
                        7,
                        0,
                        16,
                        0,
                        6,
                        5,
                        8,
                        5,
                        0,
                        AuthoritativeDamageType.Lightning,
                        0,
                        254) {
                        Behavior = AuthoritativeProjectileBehavior.ChainLightningBolt,
                        VelocityX = 2097152,
                        OriginalDirection = 2,
                        Speed = 32,
                        ResistantDamageDivisor = 4,
                        EffectiveSpellLevel = 1,
                    },
                ],
            });
        var snapshot = simulation.CaptureSnapshot(CreatePlayer(), 5);
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules));

        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = replacement.WorldState.Projectiles.Values
            .OrderBy(projectile => projectile.EntityId)
            .ToArray();
        Assert.Equal(2, restored.Length);
        Assert.Equal(AuthoritativeProjectileBehavior.ChainLightningController, restored[0].Behavior);
        Assert.Equal(1U, restored[0].RemainingTicks);
        Assert.Equal(AuthoritativeProjectileBehavior.ChainLightningBolt, restored[1].Behavior);
        Assert.Equal((32, 254U, 4), (
            restored[1].Speed,
            restored[1].RemainingTicks,
            restored[1].ResistantDamageDivisor));
    }

    [Fact]
    public void SnapshotRoundTripsNovaControllerAndBallState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules) {
                Projectiles = [
                    new AuthoritativeProjectile(
                        0x80000007,
                        7,
                        0,
                        18,
                        0,
                        5,
                        5,
                        9,
                        5,
                        320,
                        AuthoritativeDamageType.Lightning,
                        0,
                        1) {
                        Behavior = AuthoritativeProjectileBehavior.NovaController,
                        OriginalDirection = 2,
                        EffectiveSpellLevel = 1,
                    },
                    new AuthoritativeProjectile(
                        0x80000008,
                        7,
                        0,
                        18,
                        0,
                        6,
                        5,
                        9,
                        5,
                        320,
                        AuthoritativeDamageType.Lightning,
                        0,
                        254) {
                        Behavior = AuthoritativeProjectileBehavior.NovaBall,
                        VelocityX = 1048576,
                        VelocityY = 1048576,
                        OriginalDirection = 2,
                        Speed = 16,
                        ResistantDamageDivisor = 4,
                        EffectiveSpellLevel = 1,
                    },
                ],
            });
        var snapshot = simulation.CaptureSnapshot(CreatePlayer(), 5);
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules));

        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = replacement.WorldState.Projectiles.Values
            .OrderBy(projectile => projectile.EntityId)
            .ToArray();
        Assert.Equal(2, restored.Length);
        Assert.Equal(AuthoritativeProjectileBehavior.NovaController, restored[0].Behavior);
        Assert.Equal(1U, restored[0].RemainingTicks);
        Assert.Equal(AuthoritativeProjectileBehavior.NovaBall, restored[1].Behavior);
        Assert.Equal((16, 254U, 4), (
            restored[1].Speed,
            restored[1].RemainingTicks,
            restored[1].ResistantDamageDivisor));
    }

    [Fact]
    public void SnapshotRoundTripsApocalypseControllerAndBoomState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules) {
                Projectiles = [
                    new AuthoritativeProjectile(
                        0x80000009,
                        7,
                        0,
                        25,
                        0,
                        10,
                        10,
                        10,
                        10,
                        12,
                        AuthoritativeDamageType.Magic,
                        0,
                        255) {
                        Behavior = AuthoritativeProjectileBehavior.ApocalypseController,
                        StartX = 10,
                        StartY = 10,
                        ApocalypseScanX = 4,
                        ApocalypseScanY = 3,
                    },
                    new AuthoritativeProjectile(
                        0x8000000A,
                        7,
                        0,
                        25,
                        0,
                        4,
                        3,
                        4,
                        3,
                        12,
                        AuthoritativeDamageType.Physical,
                        0,
                        14) {
                        Behavior = AuthoritativeProjectileBehavior.ApocalypseBoom,
                        ApocalypseHit = true,
                    },
                ],
            });
        var snapshot = simulation.CaptureSnapshot(CreatePlayer(), 5);
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules));

        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = replacement.WorldState.Projectiles.Values
            .OrderBy(projectile => projectile.EntityId)
            .ToArray();
        Assert.Equal(2, restored.Length);
        Assert.Equal(AuthoritativeProjectileBehavior.ApocalypseController, restored[0].Behavior);
        Assert.Equal((4, 3), (restored[0].ApocalypseScanX, restored[0].ApocalypseScanY));
        Assert.Equal(AuthoritativeProjectileBehavior.ApocalypseBoom, restored[1].Behavior);
        Assert.True(restored[1].ApocalypseHit);
        Assert.Equal(14U, restored[1].RemainingTicks);
    }

    [Fact]
    public void SnapshotRoundTripsRageActiveAndCooldownState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules) {
                Projectiles = [
                    new AuthoritativeProjectile(
                        0x8000000B,
                        7,
                        0,
                        22,
                        0,
                        10,
                        10,
                        10,
                        10,
                        18,
                        AuthoritativeDamageType.Physical,
                        0,
                        47) {
                        Behavior = AuthoritativeProjectileBehavior.RageController,
                        EffectiveSpellLevel = 1,
                    },
                    new AuthoritativeProjectile(
                        0x8000000C,
                        7,
                        0,
                        22,
                        0,
                        10,
                        10,
                        10,
                        10,
                        18,
                        AuthoritativeDamageType.Physical,
                        0,
                        9) {
                        Behavior = AuthoritativeProjectileBehavior.RageController,
                        Phase = AuthoritativeProjectilePhase.Impact,
                    },
                ],
            });
        var snapshot = simulation.CaptureSnapshot(CreatePlayer(), 5);
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules));

        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = replacement.WorldState.Projectiles.Values
            .OrderBy(projectile => projectile.EntityId)
            .ToArray();
        Assert.Equal(2, restored.Length);
        Assert.All(
            restored,
            projectile => Assert.Equal(
                AuthoritativeProjectileBehavior.RageController,
                projectile.Behavior));
        Assert.Equal(AuthoritativeProjectilePhase.Travel, restored[0].Phase);
        Assert.Equal(47U, restored[0].RemainingTicks);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, restored[1].Phase);
        Assert.Equal(9U, restored[1].RemainingTicks);
    }

    [Fact]
    public void SnapshotRoundTripsElementalRetargetState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules) {
                Projectiles = [
                    new AuthoritativeProjectile(
                        0x8000000D,
                        7,
                        0,
                        31,
                        0,
                        12,
                        10,
                        14,
                        10,
                        18,
                        AuthoritativeDamageType.Fire,
                        1,
                        17) {
                        Behavior = AuthoritativeProjectileBehavior.Elemental,
                        OriginalDirection = 7,
                        Speed = 16,
                        ImpactLifetime = 14,
                        ResistantDamageDivisor = 4,
                        PathIndex = 1,
                        VelocityX = 123,
                        VelocityY = -456,
                        ElementalOriginX = 12,
                        ElementalOriginY = 10,
                        EffectiveSpellLevel = 2,
                    },
                ],
            });
        var snapshot = simulation.CaptureSnapshot(CreatePlayer(), 5);
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules));

        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.Elemental, restored.Behavior);
        Assert.Equal(1, restored.PathIndex);
        Assert.Equal(17U, restored.RemainingTicks);
        Assert.Equal((14, 10), (restored.TargetX, restored.TargetY));
        Assert.Equal((12, 10), (restored.ElementalOriginX, restored.ElementalOriginY));
        Assert.Equal((123, -456), (restored.VelocityX, restored.VelocityY));
    }

    [Fact]
    public void SnapshotRoundTripsGuardianControllerState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules) {
                Projectiles = [
                    new AuthoritativeProjectile(
                        0x8000000E,
                        7,
                        0,
                        13,
                        0,
                        12,
                        10,
                        12,
                        10,
                        0,
                        AuthoritativeDamageType.Fire,
                        0,
                        32) {
                        Behavior = AuthoritativeProjectileBehavior.GuardianController,
                        OriginalDirection = 3,
                        GuardianState = 2,
                        EffectiveSpellLevel = 1,
                    },
                ],
            });
        var snapshot = simulation.CaptureSnapshot(CreatePlayer(), 5);
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules));

        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.Projectiles.Values);
        Assert.Equal(AuthoritativeProjectileBehavior.GuardianController, restored.Behavior);
        Assert.Equal(2, restored.GuardianState);
        Assert.Equal(32U, restored.RemainingTicks);
        Assert.Equal((12, 10), (restored.PositionX, restored.PositionY));
    }

    [Fact]
    public void SnapshotRestoreRejectsMalformedHeldGoldBeforeMutation()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules));
        var destination = CreatePlayer();
        var snapshot = simulation.CaptureSnapshot(destination, 1) with {
            Player = simulation.CapturePlayerState(destination) with {
                HeldItem = new OwnedStoreItem(
                    0,
                    0,
                    42,
                    74,
                    0,
                    AuthoritativeItemState.Empty with {
                        ItemType = 11,
                        ItemIndex = 0,
                        Value = 75,
                        IdentifiedValue = 75,
                        StatFlag = true,
                    }),
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(destination, snapshot));
        Assert.Null(destination.HeldItem);
        Assert.Equal(100U, destination.Gold);
    }

    [Fact]
    public void SnapshotRoundTripsTypedStatusAndRejectsMalformedStatusState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules));
        var source = CreatePlayer();
        source.StatusEffects.Add(new AuthoritativeStatusEffect(9, 1584, 0) {
            Behavior = AuthoritativeStatusEffectBehavior.Infravision,
        });
        var snapshot = simulation.CaptureSnapshot(source, 1);
        var destination = CreatePlayer();

        simulation.RestoreSnapshot(destination, snapshot);

        var restored = Assert.Single(destination.StatusEffects);
        Assert.Equal(AuthoritativeStatusEffectBehavior.Infravision, restored.Behavior);
        Assert.Equal(1584U, restored.RemainingTicks);

        var malformedMagnitude = snapshot with {
            Player = snapshot.Player with {
                StatusEffects = [new AuthoritativeStatusEffect(9, 1, 1) {
                    Behavior = AuthoritativeStatusEffectBehavior.Infravision,
                }],
            },
        };
        var duplicateIds = snapshot with {
            Player = snapshot.Player with {
                StatusEffects = [
                    new AuthoritativeStatusEffect(9, 1, 0),
                    new AuthoritativeStatusEffect(9, 2, 0),
                ],
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(CreatePlayer(), malformedMagnitude));
        Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(CreatePlayer(), duplicateIds));
    }

    [Fact]
    public void SnapshotRestoreRejectsContentOwnedFlashControllerState()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(catalog, DiabloModule.Instance.Rules));
        var player = CreatePlayer();
        var snapshot = simulation.CaptureSnapshot(player, 1) with {
            Projectiles = [new DiabloProjectileState(
                50,
                player.EntityId,
                0,
                4,
                0,
                0,
                0,
                0,
                0,
                1,
                AuthoritativeDamageType.Magic,
                1,
                19) {
                Behavior = AuthoritativeProjectileBehavior.Flash,
            }],
        };
        var destination = CreatePlayer();

        Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(destination, snapshot));
    }

    [Fact]
    public void SnapshotRestoreRejectsMalformedGoldWorldItem()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules));
        var player = CreatePlayer();
        var snapshot = simulation.CaptureSnapshot(player, 1) with {
            WorldItems = [new DiabloWorldItemState(
                20,
                0,
                1,
                0,
                42,
                74,
                AuthoritativeItemState.Empty with {
                    ItemType = 11,
                    ItemIndex = 0,
                    Value = 75,
                    IdentifiedValue = 75,
                    StatFlag = true,
                })],
        };

        Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(CreatePlayer(), snapshot));
    }

    [Fact]
    public void SnapshotRestoreRejectsMalformedTeleportControllerState()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(catalog, DiabloModule.Instance.Rules));
        var player = CreatePlayer();
        var snapshot = simulation.CaptureSnapshot(player, 1) with {
            Projectiles = [new DiabloProjectileState(
                50,
                player.EntityId,
                0,
                23,
                0,
                1,
                1,
                1,
                1,
                0,
                AuthoritativeDamageType.Magic,
                0,
                3) {
                Behavior = AuthoritativeProjectileBehavior.Teleport,
                StartX = 1,
                StartY = 1,
            }],
        };
        var destination = CreatePlayer();

        Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(destination, snapshot));
    }

    [Fact]
    public void SnapshotRestoreAcceptsPhasingControllerState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules));
        var player = CreatePlayer();
        var snapshot = simulation.CaptureSnapshot(player, 1) with {
            Projectiles = [new DiabloProjectileState(
                50,
                player.EntityId,
                0,
                10,
                0,
                6,
                7,
                6,
                7,
                0,
                AuthoritativeDamageType.Magic,
                0,
                2) {
                Behavior = AuthoritativeProjectileBehavior.Phasing,
                StartX = 6,
                StartY = 7,
            }],
        };
        var restoredSimulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules));

        restoredSimulation.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(restoredSimulation.WorldState.Projectiles).Value;
        Assert.Equal(AuthoritativeProjectileBehavior.Phasing, restored.Behavior);
        Assert.Equal((6, 7, 2U), (
            restored.PositionX,
            restored.PositionY,
            restored.RemainingTicks));
    }

    [Fact]
    public void SnapshotRestoreAcceptsStoneCurseShatterAfterMovementCancellation()
    {
        var sourceSimulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(9, 1, 1, 20)],
            });
        var player = CreatePlayer();
        var captured = sourceSimulation.CaptureSnapshot(player, 1);
        var snapshot = captured with {
            Monsters = [captured.Monsters[0] with {
                HitPoints = 0,
                HitPointsQ6 = 0,
                Petrified = true,
            }],
            Projectiles = [CreateStoneCurseControllerState(
                50,
                player.EntityId,
                9,
                2,
                1,
                11,
                effectiveSpellLevel: 0) with {
                Phase = AuthoritativeProjectilePhase.Impact,
            }],
        };
        var restoredSimulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules));

        restoredSimulation.RestoreSnapshot(CreatePlayer(), snapshot);

        var restoredTarget = Assert.Single(restoredSimulation.WorldState.CombatTargets).Value;
        var restoredController = Assert.Single(restoredSimulation.WorldState.Projectiles).Value;
        Assert.Equal(0, restoredTarget.HitPoints);
        Assert.True(restoredTarget.IsPetrified);
        Assert.Equal(AuthoritativeProjectilePhase.Impact, restoredController.Phase);
        Assert.Equal((2, 1), (restoredController.PositionX, restoredController.PositionY));
    }

    [Fact]
    public void SnapshotRestoreRejectsDuplicateStoneCurseControllers()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    1,
                    1,
                    20,
                    petrified: true)],
            });
        var player = CreatePlayer();
        var snapshot = simulation.CaptureSnapshot(player, 1) with {
            Projectiles = [
                CreateStoneCurseControllerState(50, player.EntityId, 9, 1, 1, 10),
                CreateStoneCurseControllerState(51, player.EntityId, 9, 1, 1, 10),
            ],
        };

        Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(CreatePlayer(), snapshot));
    }

    [Fact]
    public void SnapshotRestoreRejectsCrossKindEntityCollisionsBeforeMutation()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, []);
        var simulation = DiabloSimulationFactory.Instance.Create(new DiabloSimulationOptions(
            catalog,
            DiabloModule.Instance.Rules) {
            CombatTargets = [new AuthoritativeCombatTarget(9, 1, 0, 5)],
            WorldItems = [new AuthoritativeWorldItem(
                20,
                0,
                2,
                0,
                99,
                25,
                AuthoritativeItemState.Empty)],
        });
        var snapshot = simulation.CaptureSnapshot(CreatePlayer(), 0);
        var invalid = snapshot with {
            Monsters = [snapshot.Monsters[0] with { EntityId = 20 }],
        };
        var destination = CreatePlayer();

        Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(destination, invalid));

        Assert.Equal(100U, destination.Gold);
        Assert.Equal(7U, destination.EntityId);
    }

    [Fact]
    public void SnapshotRestoreRetainsDeathAndLevelTransitionActions()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules));
        var deathSource = CreatePlayer();
        deathSource.Life = 0;
        deathSource.CombatMode = DiabloPlayerCombatMode.Death;
        deathSource.CombatModeEndTick = 40;
        deathSource.CurrentAction = new AuthoritativePlayerActionState(
            DiabloPlayerActionKind.Death,
            1,
            1,
            40,
            40) {
            EffectApplied = true,
            TargetEntityId = 9,
        };
        var deathSnapshot = simulation.CaptureSnapshot(deathSource, 20);
        var restoredDeath = CreatePlayer();

        simulation.RestoreSnapshot(restoredDeath, deathSnapshot);

        Assert.Equal(DiabloPlayerCombatMode.Death, restoredDeath.CombatMode);
        Assert.Equal(deathSource.CurrentAction, restoredDeath.CurrentAction);
        Assert.NotSame(deathSource.CurrentAction, restoredDeath.CurrentAction);

        var transitionSource = CreatePlayer();
        transitionSource.LevelId = 2;
        transitionSource.PositionX = 3;
        transitionSource.PositionY = 4;
        transitionSource.CombatMode = DiabloPlayerCombatMode.NewLevel;
        transitionSource.CombatModeEndTick = 22;
        transitionSource.CurrentAction = new AuthoritativePlayerActionState(
            DiabloPlayerActionKind.LevelTransition,
            21,
            21,
            22,
            22) {
            EffectApplied = true,
            TargetEntityId = 5,
        };
        var transitionSnapshot = simulation.CaptureSnapshot(transitionSource, 21);
        var restoredTransition = CreatePlayer();

        simulation.RestoreSnapshot(restoredTransition, transitionSnapshot);

        Assert.Equal(
            DiabloPlayerCombatMode.NewLevel,
            restoredTransition.CombatMode);
        Assert.Equal(
            transitionSource.CurrentAction,
            restoredTransition.CurrentAction);
        Assert.NotSame(
            transitionSource.CurrentAction,
            restoredTransition.CurrentAction);

        var restartSource = CreatePlayer();
        restartSource.Life = Math.Min(restartSource.LifeMaximum, 64);
        restartSource.Mana = 0;
        restartSource.LevelId = 0;
        restartSource.CombatMode = DiabloPlayerCombatMode.NewLevel;
        restartSource.CombatModeEndTick = 43;
        restartSource.CurrentAction = new AuthoritativePlayerActionState(
            DiabloPlayerActionKind.RestartTown,
            42,
            42,
            43,
            43) {
            EffectApplied = true,
        };
        var restartSnapshot = simulation.CaptureSnapshot(restartSource, 42);
        var restoredRestart = CreatePlayer();

        simulation.RestoreSnapshot(restoredRestart, restartSnapshot);

        Assert.Equal(DiabloPlayerCombatMode.NewLevel, restoredRestart.CombatMode);
        Assert.Equal(restartSource.CurrentAction, restoredRestart.CurrentAction);
        Assert.NotSame(restartSource.CurrentAction, restoredRestart.CurrentAction);
    }

    [Fact]
    public void RuntimeCreatesNormalizedPlayersAndExecutesThroughEngineLifecycle()
    {
        var catalog = new StoreCatalog();
        catalog.AddStore(1, [new StoreItem(0, 42, 10)]);
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(catalog, DiabloModule.Instance.Rules));
        var runtime = simulation.CreateRuntime(new DiabloPlayerTemplate(
            100,
            4,
            20,
            6,
            2,
            PlayerAttributesState.Zero,
            [],
            [],
            [],
            0,
            0,
            10,
            0,
            0,
            -3));

        var result = runtime.Execute(
            "player",
            1,
            (player, participants) => {
                Assert.Single(participants);
                return new SimulationStepResult<DiabloIntentResult, DiabloGameplayEvent>(
                    simulation.Execute(player, new DiabloOpenStoreIntent(1), 1));
            });
        var player = runtime.Read("player", state => state);

        Assert.True(result.Succeeded);
        Assert.Equal(100U, player.Gold);
        Assert.Equal(20, player.LifeMaximum);
        Assert.Equal(6, player.ManaMaximum);
        Assert.Equal(1U, player.CharacterLevel);
        Assert.Equal(0, player.ArmorClass);
        Assert.Equal(1U, player.ActiveStoreId);
    }

    private static GameplayModuleContext CreateContext()
    {
        var content = new ContentManifest(
            "base",
            "1",
            [new ContentPack("base", "1", [TsvTable.Parse("empty.tsv", "id\nvalue\n")])]);
        return new GameplayModuleContext(content, new DelegateAuthoritativeClock(() => 0));
    }

    private static AuthoritativePlayerState CreatePlayer()
    {
        return new AuthoritativePlayerState(
            100,
            0,
            50,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            Enumerable.Repeat(-1, 40).ToArray(),
            [],
            0,
            0,
            50,
            1,
            0,
            0) {
            EntityId = 7,
        };
    }

    private static DiabloProjectileState CreateStoneCurseControllerState(
        uint entityId,
        uint sourceEntityId,
        uint targetEntityId,
        int positionX,
        int positionY,
        uint remainingTicks,
        int effectiveSpellLevel = 1)
    {
        return new DiabloProjectileState(
            entityId,
            sourceEntityId,
            targetEntityId,
            8,
            0,
            positionX,
            positionY,
            positionX,
            positionY,
            0,
            AuthoritativeDamageType.Magic,
            0,
            remainingTicks) {
            Behavior = AuthoritativeProjectileBehavior.StoneCurse,
            StartX = positionX,
            StartY = positionY,
            ImpactLifetime = 11,
            EffectiveSpellLevel = effectiveSpellLevel,
        };
    }
}

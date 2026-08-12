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
            new ContentSchemaIdentity("devilution.diablo", "0.32.0"),
            DiabloContentSchema.Current);
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
        Assert.Contains("devilution.diablo@0.32.0", exception.Message, StringComparison.Ordinal);
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
        Assert.Same(target, simulation.WorldState.CombatTargets[9]);
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

using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Shared;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class DiabloSimulationTests
{
    [Fact]
    public void CurrentContentSchemaIncludesTrapDisarmCapability()
    {
        Assert.Equal(
            new ContentSchemaIdentity("devilution.diablo", "1.492.0"),
            DiabloContentSchema.Current);
    }

    [Theory]
    [InlineData("devilution.diablo", "1.417.0", false)]
    [InlineData("devilution.diablo", "1.491.0", true)]
    [InlineData("devilution.diablo", "1.441.0", false)]
    [InlineData("devilution.diablo", "1.429.0", false)]
    [InlineData("devilution.diablo", "1.415.0", false)]
    [InlineData("other.game", "1.424.0", false)]
    public void ContentSchemaCompatibilityRetainsThePreviousAdditiveContract(
        string schemaId,
        string version,
        bool expected)
    {
        Assert.Equal(
            expected,
            DiabloContentSchema.IsCompatible(new ContentSchemaIdentity(schemaId, version)));
    }

    [Fact]
    public void SimulationMaterializesRelativeSetLevelObjectsFromInitialOrigins()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(17, 32, 32));
        var catalog = new AuthoritativeSetLevelObjectCatalog([
            new AuthoritativeSetLevelObjectPlacement(1101, 6, 4, 9, 24, 0, 0, 2, 2) {
                PlacementMode = AuthoritativeSetLevelObjectPlacementMode.SetLevelOriginOffset,
            },
        ]);

        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                SetLevelObjects = catalog,
                SetLevelObjectOrigins = new Dictionary<int, AuthoritativeSetLevelObjectOrigin> {
                    [6] = new(17, 12, 7),
                },
            });

        var @object = Assert.Single(simulation.WorldState.Objects.Values);
        Assert.Equal(
            AuthoritativeSetLevelObjectCatalog.RuntimeObjectEntityIdBase + 1101,
            @object.EntityId);
        Assert.Equal((17U, 21, 31), (@object.LevelId, @object.PositionX, @object.PositionY));
        Assert.Equal((17U, 12), (
            simulation.SetLevelObjectOrigins[6].LevelId,
            simulation.SetLevelObjectOrigins[6].PositionX));
    }

    [Fact]
    public void SimulationMaterializesRelativeObjectsFromProceduralOrigins()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(17, 32, 32));
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                SetLevelObjects = new AuthoritativeSetLevelObjectCatalog([
                    new AuthoritativeSetLevelObjectPlacement(1101, 6, 4, 1, 1, 0, 0, 2, 2) {
                        PlacementMode = AuthoritativeSetLevelObjectPlacementMode.SetLevelOriginOffset,
                    },
                ]),
                ProceduralSetPieceOrigins = new AuthoritativeProceduralSetPieceOriginCatalog([
                    new AuthoritativeProceduralSetPieceOriginDefinition(6, 17, 4, 4, 1234),
                ]),
                RandomSeed = 99,
            });

        var origin = simulation.SetLevelObjectOrigins[6];
        var @object = Assert.Single(simulation.WorldState.Objects.Values);
        Assert.Equal((origin.LevelId, origin.PositionX + 1, origin.PositionY + 1),
            (@object.LevelId, @object.PositionX, @object.PositionY));
    }

    [Fact]
    public void SimulationPublishesDeferredProceduralOriginsWhenGeometryArrives()
    {
        var world = new AuthoritativeWorld();
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                SetLevelObjects = new AuthoritativeSetLevelObjectCatalog([
                    new AuthoritativeSetLevelObjectPlacement(1101, 6, 4, 1, 1, 0, 0, 1, 1) {
                        PlacementMode = AuthoritativeSetLevelObjectPlacementMode.SetLevelOriginOffset,
                    },
                ]),
                ProceduralSetPieceOrigins = new AuthoritativeProceduralSetPieceOriginCatalog([
                    new AuthoritativeProceduralSetPieceOriginDefinition(6, 17, 4, 4, 1234),
                ]),
                RandomSeed = 99,
        });

        Assert.Empty(simulation.SetLevelObjectOrigins);
        simulation.WorldState.AddLevel(new AuthoritativeLevel(17, 32, 32));
        Assert.Contains(simulation.WorldState.LevelGeometry, level => level.LevelId == 17);

        var published = simulation.PublishAvailableProceduralSetPieceOrigins();
        var origin = Assert.Single(published).Value;
        var @object = Assert.Single(simulation.WorldState.Objects.Values);
        Assert.Equal((origin.LevelId, origin.PositionX + 1, origin.PositionY + 1),
            (@object.LevelId, @object.PositionX, @object.PositionY));
        Assert.Empty(simulation.PublishAvailableProceduralSetPieceOrigins());
    }

    [Fact]
    public void SimulationPlansOrdinaryQuestPieceOriginsWithoutUsingSetLevelIds()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(17, 32, 32));
        var catalog = new AuthoritativeProceduralQuestPieceOriginCatalog([
            new AuthoritativeProceduralQuestPieceOriginDefinition(2201, 17, 4, 4, 1234),
        ]);
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                ProceduralQuestPieceOrigins = catalog,
                RandomSeed = 99,
            });

        var origin = Assert.Single(simulation.ProceduralQuestPieceOriginLocations);
        Assert.Equal(2201U, origin.Key);
        Assert.Equal(17U, origin.Value.LevelId);
        Assert.InRange(origin.Value.PositionX, 0, 28);
        Assert.InRange(origin.Value.PositionY, 0, 28);
        Assert.Empty(simulation.SetLevelObjectOrigins);
    }

    [Fact]
    public void SnapshotRoundTripRestoresProceduralQuestPieceOrigins()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(0, 32, 32));
        world.AddLevel(new AuthoritativeLevel(17, 32, 32));
        var options = new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
            World = world,
            ProceduralQuestPieceOrigins = new AuthoritativeProceduralQuestPieceOriginCatalog([
                new AuthoritativeProceduralQuestPieceOriginDefinition(2201, 17, 4, 4, 1234),
            ]),
            RandomSeed = 99,
        };
        var source = DiabloSimulationFactory.Instance.Create(options);
        var snapshot = source.CaptureSnapshot(CreatePlayer(), 3);

        var restoredWorld = new AuthoritativeWorld();
        restoredWorld.AddLevel(new AuthoritativeLevel(0, 32, 32));
        restoredWorld.AddLevel(new AuthoritativeLevel(17, 32, 32));
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = restoredWorld,
                ProceduralQuestPieceOrigins = options.ProceduralQuestPieceOrigins,
                RandomSeed = 99,
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        Assert.Equal(
            source.ProceduralQuestPieceOriginLocations,
            replacement.ProceduralQuestPieceOriginLocations);
    }

    [Fact]
    public void SimulationPublishesDeferredOrdinaryQuestPieceOriginsWhenGeometryArrives()
    {
        var world = new AuthoritativeWorld();
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                ProceduralQuestPieceOrigins = new AuthoritativeProceduralQuestPieceOriginCatalog([
                    new AuthoritativeProceduralQuestPieceOriginDefinition(2201, 17, 4, 4, 1234),
                ]),
                RandomSeed = 99,
            });

        Assert.Empty(simulation.ProceduralQuestPieceOriginLocations);
        simulation.WorldState.AddLevel(new AuthoritativeLevel(17, 32, 32));
        var published = simulation.PublishAvailableProceduralQuestPieceOrigins();

        Assert.Single(published);
        Assert.Equal(published, simulation.ProceduralQuestPieceOriginLocations);
        Assert.Empty(simulation.PublishAvailableProceduralQuestPieceOrigins());
    }

    [Fact]
    public void SimulationPublishesAndMaterializesRelativeSetLevelObjectsAfterConstruction()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(17, 32, 32));
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                SetLevelObjects = new AuthoritativeSetLevelObjectCatalog([
                    new AuthoritativeSetLevelObjectPlacement(1101, 6, 4, -2, 3, 0, 0, 2, 2) {
                        PlacementMode = AuthoritativeSetLevelObjectPlacementMode.SetLevelOriginOffset,
                    },
                ]),
            });

        var generated = simulation.PublishSetLevelObjectOrigin(
            6,
            new AuthoritativeSetLevelObjectOrigin(17, 12, 7));

        var @object = Assert.Single(generated);
        Assert.Equal((10, 10), (@object.PositionX, @object.PositionY));
        Assert.Same(@object, simulation.WorldState.Objects[@object.EntityId]);
    }

    [Fact]
    public void SnapshotRoundTripRestoresPublishedSetLevelObjectOrigins()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(0, 32, 32));
        world.AddLevel(new AuthoritativeLevel(17, 32, 32));
        var catalog = new AuthoritativeSetLevelObjectCatalog([
            new AuthoritativeSetLevelObjectPlacement(1101, 6, 4, -2, 3, 0, 0, 2, 2) {
                PlacementMode = AuthoritativeSetLevelObjectPlacementMode.SetLevelOriginOffset,
            },
        ]);
        var options = new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
            World = world,
            SetLevelObjects = catalog,
        };
        var source = DiabloSimulationFactory.Instance.Create(options);
        source.PublishSetLevelObjectOrigin(6, new AuthoritativeSetLevelObjectOrigin(17, 12, 7));
        var snapshot = source.CaptureSnapshot(CreatePlayer(), 3);

        var restoredWorld = new AuthoritativeWorld();
        restoredWorld.AddLevel(new AuthoritativeLevel(0, 32, 32));
        restoredWorld.AddLevel(new AuthoritativeLevel(17, 32, 32));
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = restoredWorld,
                SetLevelObjects = catalog,
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var origin = Assert.Single(replacement.SetLevelObjectOrigins);
        Assert.Equal((6, 17U, 12, 7), (
            origin.Key,
            origin.Value.LevelId,
            origin.Value.PositionX,
            origin.Value.PositionY));
    }

    [Fact]
    public void SnapshotRestoreRejectsMoreThanNativePortalCapacity()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(0, 16, 16));
        world.AddLevel(new AuthoritativeLevel(1, 16, 16));
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
            });
        var snapshot = simulation.CaptureSnapshot(CreatePlayer(), 1) with {
            Portals = Enumerable.Range(1, AuthoritativePortalRules.NativeMaximumPortalCount + 1)
                .Select(id => new DiabloPortalState((uint)id, 0, id, 0, 1, id, 1))
                .ToArray(),
        };

        var exception = Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(CreatePlayer(), snapshot));
        Assert.Contains("invalid or excessive portal state", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SnapshotRestoreRejectsOutOfRangeDiabloKillLevel()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(0, 16, 16));
        world.AddLevel(new AuthoritativeLevel(1, 16, 16));
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
            });
        var snapshot = simulation.CaptureSnapshot(CreatePlayer(), 1) with {
            Player = simulation.CapturePlayerState(CreatePlayer()) with {
                DiabloKillLevel = NativeCompatibilityLimits.MaximumNativeDiabloKillLevel + 1,
            },
        };

        var exception = Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(CreatePlayer(), snapshot));
        Assert.Contains("out-of-range player state", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SnapshotRoundTripPreservesSetLevelRewardOriginMetadata()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(0, 32, 32));
        world.AddLevel(new AuthoritativeLevel(1, 32, 32));
        var sourceObject = new AuthoritativeWorldObject(
            20,
            4,
            1,
            8,
            8,
            rewardKind: AuthoritativeObjectRewardKind.NormalItem,
            rewardId: 100,
            rewardOriginKind: AuthoritativeObjectRewardOriginKind.SetLevelOrigin,
            rewardSetLevelId: 6,
            rewardOffsetX: 1,
            dialogueMessageId: 255);
        var source = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                Objects = [sourceObject],
                SetLevelObjectOrigins = new Dictionary<int, AuthoritativeSetLevelObjectOrigin> {
                    [6] = new(1, 12, 13),
                },
            });
        var snapshot = source.CaptureSnapshot(CreatePlayer(), 3);

        var restoredWorld = new AuthoritativeWorld();
        restoredWorld.AddLevel(new AuthoritativeLevel(0, 32, 32));
        restoredWorld.AddLevel(new AuthoritativeLevel(1, 32, 32));
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = restoredWorld,
                Objects = [],
                SetLevelObjectOrigins = new Dictionary<int, AuthoritativeSetLevelObjectOrigin> {
                    [6] = new(1, 12, 13),
                },
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.Objects.Values);
        Assert.Equal(AuthoritativeObjectRewardOriginKind.SetLevelOrigin, restored.RewardOriginKind);
        Assert.Equal(6, restored.RewardSetLevelId);
        Assert.Equal(255, restored.DialogueMessageId);
    }

    [Fact]
    public void SnapshotRoundTripRestoresDialogueIdentityOnExistingObject()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 16, 16));
        var source = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                Objects = [new AuthoritativeWorldObject(
                    20,
                    71,
                    1,
                    8,
                    8,
                    dialogueMessageId: 412)],
            });
        var snapshot = source.CaptureSnapshot(CreatePlayer(), 3);

        var replacementWorld = new AuthoritativeWorld();
        replacementWorld.AddLevel(new AuthoritativeLevel(0, 16, 16));
        replacementWorld.AddLevel(new AuthoritativeLevel(1, 16, 16));
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = replacementWorld,
                Objects = [new AuthoritativeWorldObject(20, 71, 1, 0, 0)],
            });

        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.Objects.Values);
        Assert.Equal(412, restored.DialogueMessageId);
        Assert.Equal((8, 8), (restored.PositionX, restored.PositionY));
    }

    [Fact]
    public void SnapshotRoundTripRestoresSetLevelAnchoredMutation()
    {
        var mutationCatalog = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                7,
                1,
                geometryRectanglesAdd: [new AuthoritativeWorldMutationRectangle(0, 0, 1, 1)],
                anchorSetLevelId: 6),
        ]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(0, 16, 16));
        world.AddLevel(new AuthoritativeLevel(1, 16, 16));
        var origin = new Dictionary<int, AuthoritativeSetLevelObjectOrigin> {
            [6] = new(1, 4, 5),
        };
        var source = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                WorldMutations = mutationCatalog,
                SetLevelObjectOrigins = origin,
            });
        Assert.True(source.WorldState.ApplyWorldMutation(7));
        var snapshot = source.CaptureSnapshot(CreatePlayer(), 3);

        var restoredWorld = new AuthoritativeWorld();
        restoredWorld.AddLevel(new AuthoritativeLevel(0, 16, 16));
        restoredWorld.AddLevel(new AuthoritativeLevel(1, 16, 16));
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = restoredWorld,
                WorldMutations = mutationCatalog,
                SetLevelObjectOrigins = origin,
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        Assert.Contains(7U, replacement.WorldState.ActiveWorldMutationIds);
        Assert.False(replacement.WorldState.IsWalkable(1, 4, 5));
    }

    [Fact]
    public void SnapshotRoundTripRestoresProceduralQuestOriginAnchoredMutation()
    {
        var mutationCatalog = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                7,
                1,
                geometryRectanglesAdd: [new AuthoritativeWorldMutationRectangle(0, 0, 1, 1)],
                anchorProceduralQuestPieceOriginId: 2201),
        ]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(0, 16, 16));
        world.AddLevel(new AuthoritativeLevel(1, 16, 16));
        var origins = new Dictionary<uint, AuthoritativeProceduralQuestPieceOrigin> {
            [2201] = new(1, 4, 5),
        };
        var source = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                WorldMutations = mutationCatalog,
            });
        source.PublishProceduralQuestPieceOrigin(2201, origins[2201]);
        Assert.True(source.WorldState.ApplyWorldMutation(7));
        var snapshot = source.CaptureSnapshot(CreatePlayer(), 3);

        var restoredWorld = new AuthoritativeWorld();
        restoredWorld.AddLevel(new AuthoritativeLevel(0, 16, 16));
        restoredWorld.AddLevel(new AuthoritativeLevel(1, 16, 16));
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = restoredWorld,
                WorldMutations = mutationCatalog,
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        Assert.Contains(7U, replacement.WorldState.ActiveWorldMutationIds);
        Assert.False(replacement.WorldState.IsWalkable(1, 4, 5));
    }

    [Fact]
    public void FailedSnapshotRestoreRetainsSetLevelMutationProjection()
    {
        var mutationCatalog = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                7,
                1,
                geometryRectanglesAdd: [new AuthoritativeWorldMutationRectangle(0, 0, 1, 1)],
                anchorSetLevelId: 6),
        ]);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(0, 16, 16));
        world.AddLevel(new AuthoritativeLevel(1, 16, 16));
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                WorldMutations = mutationCatalog,
                SetLevelObjectOrigins = new Dictionary<int, AuthoritativeSetLevelObjectOrigin> {
                    [6] = new(1, 4, 5),
                },
            });
        Assert.True(simulation.WorldState.ApplyWorldMutation(7));
        var snapshot = simulation.CaptureSnapshot(CreatePlayer(), 3);
        var invalid = snapshot with {
            SetLevelObjectOrigins = [snapshot.SetLevelObjectOrigins[0] with { LevelId = 0 }],
        };

        Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(CreatePlayer(), invalid));

        Assert.Equal(new AuthoritativeSetLevelObjectOrigin(1, 4, 5), simulation.SetLevelObjectOrigins[6]);
        Assert.False(simulation.WorldState.IsWalkable(1, 4, 5));
    }

    [Fact]
    public void FailedSetLevelOriginPublicationLeavesOriginsAndObjectsUnchanged()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(17, 32, 32));
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                SetLevelObjects = new AuthoritativeSetLevelObjectCatalog([
                    new AuthoritativeSetLevelObjectPlacement(
                        1101,
                        6,
                        4,
                        1,
                        1,
                        0,
                        0,
                        1,
                        1) {
                        PlacementMode = AuthoritativeSetLevelObjectPlacementMode.SetLevelOriginOffset,
                        TemplateEntityId = 9999,
                    },
                ]),
            });

        Assert.Throws<InvalidDataException>(() => simulation.PublishSetLevelObjectOrigin(
            6,
            new AuthoritativeSetLevelObjectOrigin(17, 12, 12)));

        Assert.Empty(simulation.SetLevelObjectOrigins);
        Assert.Empty(simulation.WorldState.Objects);
    }

    [Fact]
    public void FailedProceduralOriginBatchRollsBackEarlierPublications()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = new AuthoritativeWorld(),
                SetLevelObjects = new AuthoritativeSetLevelObjectCatalog([
                    new AuthoritativeSetLevelObjectPlacement(1101, 6, 4, 1, 1, 0, 0, 1, 1) {
                        PlacementMode = AuthoritativeSetLevelObjectPlacementMode.SetLevelOriginOffset,
                    },
                    new AuthoritativeSetLevelObjectPlacement(1102, 7, 5, 1, 1, 0, 0, 1, 1) {
                        PlacementMode = AuthoritativeSetLevelObjectPlacementMode.SetLevelOriginOffset,
                        TemplateEntityId = 9999,
                    },
                ]),
                ProceduralSetPieceOrigins = new AuthoritativeProceduralSetPieceOriginCatalog([
                    new AuthoritativeProceduralSetPieceOriginDefinition(6, 17, 4, 4, 1234),
                    new AuthoritativeProceduralSetPieceOriginDefinition(7, 17, 4, 4, 1235),
                ]),
            });
        simulation.WorldState.AddLevel(new AuthoritativeLevel(17, 32, 32));

        Assert.Throws<InvalidDataException>(
            simulation.PublishAvailableProceduralSetPieceOrigins);

        Assert.Empty(simulation.SetLevelObjectOrigins);
        Assert.Empty(simulation.WorldState.Objects);
    }

    [Theory]
    [InlineData(DiabloDifficulty.Normal, DiabloGameVariant.Diablo, DiabloGameMode.SinglePlayer, 10)]
    [InlineData(DiabloDifficulty.Nightmare, DiabloGameVariant.Diablo, DiabloGameMode.SinglePlayer, 130)]
    [InlineData(DiabloDifficulty.Hell, DiabloGameVariant.Diablo, DiabloGameMode.SinglePlayer, 240)]
    [InlineData(DiabloDifficulty.Nightmare, DiabloGameVariant.Hellfire, DiabloGameMode.SinglePlayer, 80)]
    [InlineData(DiabloDifficulty.Nightmare, DiabloGameVariant.Hellfire, DiabloGameMode.Multiplayer, 130)]
    [InlineData(DiabloDifficulty.Hell, DiabloGameVariant.Hellfire, DiabloGameMode.SinglePlayer, 140)]
    [InlineData(DiabloDifficulty.Hell, DiabloGameVariant.Hellfire, DiabloGameMode.Multiplayer, 240)]
    public void FactoryAppliesNativeDifficultyHealthScaling(
        DiabloDifficulty difficulty,
        DiabloGameVariant gameVariant,
        DiabloGameMode gameMode,
        int expectedMaximumHitPoints)
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            10,
            maxHitPoints: 10);
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [target],
                CombatRules = new AuthoritativeCombatRules(
                    10,
                    1,
                    100,
                    gameVariant: gameVariant),
                Difficulty = difficulty,
                GameMode = gameMode,
            });

        Assert.Same(target, Assert.Single(simulation.WorldState.CombatTargets.Values));
        Assert.Equal(expectedMaximumHitPoints, target.MaxHitPoints);
        Assert.Equal(expectedMaximumHitPoints, target.HitPoints);
    }

    [Fact]
    public void DifficultyHealthScalingPreservesPartialMonsterHealthAndExcludesPlayerMinions()
    {
        var wounded = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            5,
            maxHitPoints: 10);
        var minion = new AuthoritativeCombatTarget(
            10,
            2,
            0,
            10,
            maxHitPoints: 10,
            playerMinion: true);
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [wounded, minion],
                Difficulty = DiabloDifficulty.Nightmare,
            });

        Assert.Equal((65, 130), (wounded.HitPoints, wounded.MaxHitPoints));
        Assert.Equal((10, 10), (minion.HitPoints, minion.MaxHitPoints));
    }

    [Fact]
    public void FactoryUsesExternalDifficultyHealthRules()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            10,
            maxHitPoints: 10);
        DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [target],
                CombatRules = new AuthoritativeCombatRules(
                    10,
                    1,
                    100,
                    difficultyRules: new DiabloDifficultyRules {
                        HellHealthMultiplier = 2,
                        HellDiabloHealthBonus = 3,
                    }),
                Difficulty = DiabloDifficulty.Hell,
            });

        Assert.Equal((23, 23), (target.HitPoints, target.MaxHitPoints));
    }

    [Fact]
    public void SnapshotRestoreRetainsEffectiveDifficultyHealthWithoutScalingAgain()
    {
        var source = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    1,
                    0,
                    10,
                    maxHitPoints: 10)],
                Difficulty = DiabloDifficulty.Hell,
            });
        var snapshot = source.CaptureSnapshot(CreatePlayer(), 3);
        var destination = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                Difficulty = DiabloDifficulty.Hell,
            });

        destination.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(destination.WorldState.CombatTargets.Values);
        Assert.Equal((240, 240), (restored.HitPoints, restored.MaxHitPoints));
    }

    [Fact]
    public void SnapshotRoundTripsMonsterDialogueIdentity()
    {
        var source = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    1,
                    0,
                    10,
                    unique: true,
                    uniqueIdentity: "Gharbad",
                    dialogueMessageId: 145,
                    monsterItemSeed: 0x12345678,
                    monsterAiSeed: 0x87654321)],
            });

        var snapshot = source.CaptureSnapshot(CreatePlayer(), 3);
        var destination = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules));

        destination.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(destination.WorldState.CombatTargets.Values);
        Assert.Equal(145, restored.DialogueMessageId);
        Assert.Equal((0x12345678U, 0x87654321U),
            (restored.MonsterItemSeed, restored.MonsterAiSeed));
    }

    [Fact]
    public void SimulationProjectsTheCurrentAuthoredDialogueMessageOnInitialTarget()
    {
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            10,
            unique: true,
            uniqueIdentity: "Warlord");
        var quest = new AuthoritativeQuestState(
            3012,
            0,
            1,
            active: true,
            completionUniqueIdentity: "Warlord");
        var stages = new AuthoritativeQuestDialogueStageCatalog([
            new AuthoritativeQuestDialogueStage(
                3012,
                0,
                -1,
                110,
                2,
                0),
        ]);

        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [target],
                Quests = [quest],
                QuestDialogueStages = stages,
            });

        Assert.Equal(110, Assert.Single(simulation.WorldState.CombatTargets.Values).DialogueMessageId);
        Assert.Equal(
            AuthoritativeMonsterDialogueState.Inquiring,
            Assert.Single(simulation.WorldState.CombatTargets.Values).DialogueState);

        var snapshot = simulation.CaptureSnapshot(CreatePlayer(), 1);
        var legacySnapshot = snapshot with {
            Monsters = [snapshot.Monsters[0] with { DialogueMessageId = 0 }],
        };
        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                QuestDialogueStages = stages,
            });
        replacement.RestoreSnapshot(CreatePlayer(), legacySnapshot);

        Assert.Equal(110, Assert.Single(replacement.WorldState.CombatTargets.Values).DialogueMessageId);
    }

    [Fact]
    public void SimulationProjectsItemAbsentDialogueBranchOnSharedInitialTarget()
    {
        var target = new AuthoritativeCombatTarget(
            20,
            20,
            20,
            100,
            levelId: 14,
            unique: true,
            uniqueIdentity: "Lachdanan");
        var quest = new AuthoritativeQuestState(
            3005,
            14,
            1,
            active: true,
            completionUniqueIdentity: "Lachdanan");
        var stages = new AuthoritativeQuestDialogueStageCatalog([
            new(
                3005,
                0,
                0,
                83,
                0,
                0,
                RequiredItemId: 2137,
                ConsumeItem: true,
                CompleteQuest: true,
                RewardKind: AuthoritativeQuestDialogueRewardKind.UniqueItem,
                RewardUniqueId: 1008),
            new(
                3005,
                0,
                0,
                81,
                0,
                0,
                RequiredItemId: 2137,
                RequireItemPresent: false),
        ]);

        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [target],
                Quests = [quest],
                QuestDialogueStages = stages,
            });

        Assert.Equal(81, target.DialogueMessageId);
    }

    [Fact]
    public void SnapshotRestoreRejectsOnlyOneMonsterRuntimeSeed()
    {
        var source = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    1,
                    0,
                    10,
                    monsterItemSeed: 0x12345678,
                    monsterAiSeed: 0x87654321)],
            });
        var snapshot = source.CaptureSnapshot(CreatePlayer(), 3);
        var malformed = snapshot with {
            Monsters = [snapshot.Monsters[0] with { MonsterAiSeed = 0 }],
        };
        var destination = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules));

        Assert.Throws<InvalidDataException>(() =>
            destination.RestoreSnapshot(CreatePlayer(), malformed));
    }

    [Fact]
    public void SnapshotRestoreAllowsPlayerToRemainOnUnactivatedOccupancyTrigger()
    {
        DiabloSimulation CreateSimulation()
        {
            var world = new AuthoritativeWorld();
            world.AddLevel(new AuthoritativeLevel(
                5,
                10,
                10,
                nativePieceLayer: Enumerable.Repeat<ushort>(0, 100)));
            return DiabloSimulationFactory.Instance.Create(
                new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                    World = world,
                    Objects = [new AuthoritativeWorldObject(
                        2040,
                        3,
                        5,
                        2,
                        2,
                        questId: 3016,
                        activateOnOccupancy: true,
                        requiredActivatedLinkedObjects: 2)],
                    Quests = [new AuthoritativeQuestState(3016, 5, 1, active: true)],
                });
        }

        var source = CreateSimulation();
        var sourcePlayer = CreatePlayer();
        sourcePlayer.LevelId = 5;
        sourcePlayer.PositionX = 2;
        sourcePlayer.PositionY = 2;
        var snapshot = source.CaptureSnapshot(sourcePlayer, 1);
        var destination = CreateSimulation();
        var restoredPlayer = CreatePlayer();
        restoredPlayer.LevelId = 5;
        restoredPlayer.PositionX = 2;
        restoredPlayer.PositionY = 2;

        destination.RestoreSnapshot(restoredPlayer, snapshot);

        var restored = Assert.Single(destination.WorldState.Objects.Values);
        Assert.True(restored.ActivateOnOccupancy);
        Assert.Equal(2, restored.RequiredActivatedLinkedObjects);
        Assert.Equal((2, 2), (restoredPlayer.PositionX, restoredPlayer.PositionY));
    }

    [Fact]
    public void HellUsesTypeSpecificResistanceMetadataWithoutChangingNightmare()
    {
        var type = new AuthoritativeMonsterTypeDefinition(
            12,
            12,
            "Hell-resistant monster",
            1,
            16,
            10,
            10,
            10,
            0,
            1,
            1,
            1,
            1,
            false,
            false,
            false) {
            FireResistance = 25,
            LightningResistance = 50,
            MagicResistance = 75,
            AcidResistance = 100,
            FireResistanceHell = 100,
            LightningResistanceHell = 0,
            MagicResistanceHell = 50,
            AcidResistanceHell = 75,
        };
        var target = new AuthoritativeCombatTarget(
            9,
            1,
            0,
            10,
            monsterId: 12,
            fireResistance: type.FireResistance,
            lightningResistance: type.LightningResistance,
            magicResistance: type.MagicResistance,
            acidResistance: type.AcidResistance);
        var monsterTypes = new AuthoritativeMonsterTypeCatalog([type]);
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [target],
                MonsterTypes = monsterTypes,
                Difficulty = DiabloDifficulty.Hell,
            });

        Assert.Equal((100, 0, 50, 75), (
            target.FireResistance,
            target.LightningResistance,
            target.MagicResistance,
            target.AcidResistance));

        var nightmareTarget = new AuthoritativeCombatTarget(
            10,
            1,
            0,
            10,
            monsterId: 12,
            fireResistance: type.FireResistance,
            lightningResistance: type.LightningResistance,
            magicResistance: type.MagicResistance,
            acidResistance: type.AcidResistance);
        var nightmare = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [nightmareTarget],
                MonsterTypes = monsterTypes,
                Difficulty = DiabloDifficulty.Nightmare,
            });

        Assert.Equal((25, 50, 75, 100), (
            nightmareTarget.FireResistance,
            nightmareTarget.LightningResistance,
            nightmareTarget.MagicResistance,
            nightmareTarget.AcidResistance));
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
                    DamageMinimum = 2,
                    DamageMaximum = 9,
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
        Assert.Equal((2, 9), (projectile.DamageMinimum, projectile.DamageMaximum));
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
        Assert.Contains("devilution.diablo@1.492.0", exception.Message, StringComparison.Ordinal);
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
        var returnPortal = new AuthoritativePortal(6, 2, 0, 0, 1, 0, 0);
        var initialTargets = planner.PlanCombatTargets(1, 0x1234, false, 1);
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                Portals = [portal, returnPortal],
                CombatTargets = initialTargets,
                MonsterSpawnPlanner = planner,
                ProceduralMonsterSpawns = new DiabloMonsterSpawnConfiguration(1),
                RandomSeed = 0x1234,
                Difficulty = DiabloDifficulty.Nightmare,
            });
        var player = CreatePlayer();
        player.LevelId = 1;
        player.PositionX = 0;
        player.PositionY = 0;
        var preEntrySnapshot = simulation.CaptureSnapshot(player, 0);

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
        Assert.Equal(115, generated.MaxHitPoints);

        simulation.RestoreSnapshot(player, preEntrySnapshot);

        var replayResult = simulation.Execute(
            player,
            new DiabloUsePortalIntent(portal.PortalId),
            2,
            [player]);

        Assert.True(replayResult.Succeeded);
        Assert.Equal(2U, player.LevelId);
        Assert.Equal(2, simulation.WorldState.CombatTargets.Count);
        var replayedGenerated = Assert.Single(
            simulation.WorldState.CombatTargets.Values,
            target => target.LevelId == 2);
        Assert.Equal(generated.EntityId, replayedGenerated.EntityId);

        simulation.WorldState.CombatTargets.Remove(generated.EntityId);
        var defeatedSnapshot = simulation.CaptureSnapshot(player, 3);

        simulation.RestoreSnapshot(player, defeatedSnapshot);
        var returnToStart = simulation.Execute(
            player,
            new DiabloUsePortalIntent(returnPortal.PortalId),
            4,
            [player]);
        Assert.True(returnToStart.Succeeded, returnToStart.Failure?.ToString());
        Assert.Equal(1U, player.LevelId);
        Assert.True(simulation.Execute(
            player,
            new DiabloUsePortalIntent(portal.PortalId),
            5,
            [player]).Succeeded);

        Assert.Equal(2U, player.LevelId);
        Assert.Single(
            simulation.WorldState.CombatTargets.Values,
            target => target.LevelId == 1);
        Assert.DoesNotContain(
            simulation.WorldState.CombatTargets.Values,
            target => target.LevelId == 2);
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
            nativeAi: "SkeletonRanged",
            unique: true,
            uniqueIdentity: "Lachdanan",
            dialogueMessageId: 83);
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
            nativeAi: "SkeletonRanged",
            unique: true,
            uniqueIdentity: "Lachdanan",
            dialogueMessageId: 83);
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
        Assert.Equal(83, restored.DialogueMessageId);
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
                    fallenFearDirection: 7,
                    initialStandAnimationFrame: 4,
                    initialStandAnimationTick: 2,
                    fallenRallyTicks: 165)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        var savedMonster = Assert.Single(snapshot.Monsters);
        Assert.Equal((5, 7), (savedMonster.FallenFearTicks, savedMonster.FallenFearDirection));
        Assert.Equal(
            (4, 2, 165),
            (savedMonster.FallenStandAnimationFrame,
                savedMonster.FallenStandAnimationTick,
                savedMonster.FallenRallyTicks));

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
        Assert.Equal(
            (4, 2, 165),
            (restored.FallenStandAnimationFrame,
                restored.FallenStandAnimationTick,
                restored.FallenRallyTicks));
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
                    batRetreatDirection: 7,
                    batMovementIdleTicks: 21,
                    batMovementMode: 1)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        var savedMonster = Assert.Single(snapshot.Monsters);
        Assert.Equal((1, 7), (savedMonster.BatRetreatTicks, savedMonster.BatRetreatDirection));
        Assert.Equal((21, 1), (savedMonster.BatMovementIdleTicks, savedMonster.BatMovementMode));

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
        Assert.Equal((21, 1), (restored.BatMovementIdleTicks, restored.BatMovementMode));
    }

    [Fact]
    public void SnapshotRoundTripsRangedAvoidanceMoveGoalState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Magma",
                    rangedAvoidanceMoveTicks: 7,
                    rangedAvoidanceTurn: 1)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        var savedMonster = Assert.Single(snapshot.Monsters);
        Assert.Equal((7, 1), (
            savedMonster.RangedAvoidanceMoveTicks,
            savedMonster.RangedAvoidanceTurn));

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Magma")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal((7, 1), (
            restored.RangedAvoidanceMoveTicks,
            restored.RangedAvoidanceTurn));
    }

    [Fact]
    public void SnapshotRoundTripsRangedAvoidanceMovementCounterState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Magma",
                    rangedAvoidanceMovementIdleTicks: 10,
                    rangedAvoidanceMovementMode: 1)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        var savedMonster = Assert.Single(snapshot.Monsters);
        Assert.Equal((10, 1), (
            savedMonster.RangedAvoidanceMovementIdleTicks,
            savedMonster.RangedAvoidanceMovementMode));

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Magma")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal((10, 1), (
            restored.RangedAvoidanceMovementIdleTicks,
            restored.RangedAvoidanceMovementMode));
    }

    [Fact]
    public void SnapshotRoundTripsRhinoMoveGoalState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Rhino",
                    rhinoMovementTicks: 7,
                    rhinoMovementTurn: 1)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        var savedMonster = Assert.Single(snapshot.Monsters);
        Assert.Equal((7, 1), (
            savedMonster.RhinoMovementTicks,
            savedMonster.RhinoMovementTurn));

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Rhino")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal((7, 1), (
            restored.RhinoMovementTicks,
            restored.RhinoMovementTurn));
    }

    [Fact]
    public void SnapshotRoundTripsAiAvoidanceMoveGoalState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "GoatMelee",
                    avoidanceMovementTicks: 7,
                    avoidanceMovementTurn: 1)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        var savedMonster = Assert.Single(snapshot.Monsters);
        Assert.Equal((7, 1), (
            savedMonster.AvoidanceMovementTicks,
            savedMonster.AvoidanceMovementTurn));

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "GoatMelee")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal((7, 1), (
            restored.AvoidanceMovementTicks,
            restored.AvoidanceMovementTurn));
    }

    [Fact]
    public void SnapshotRoundTripsAiAvoidanceMovementCounterState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "GoatMelee",
                    avoidanceMovementIdleTicks: 10,
                    avoidanceMovementMode: 1)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        var savedMonster = Assert.Single(snapshot.Monsters);
        Assert.Equal((10, 1), (
            savedMonster.AvoidanceMovementIdleTicks,
            savedMonster.AvoidanceMovementMode));

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "GoatMelee")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal((10, 1), (
            restored.AvoidanceMovementIdleTicks,
            restored.AvoidanceMovementMode));
    }

    [Fact]
    public void SnapshotRoundTripsSkeletonMeleeDelayState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "SkeletonMelee",
                    skeletonMeleeDelayTicks: 7)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        var savedMonster = Assert.Single(snapshot.Monsters);
        Assert.Equal(7, savedMonster.SkeletonMeleeDelayTicks);

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "SkeletonMelee")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal(7, restored.SkeletonMeleeDelayTicks);
    }

    [Fact]
    public void SnapshotRoundTripsSkeletonRangedMovementCounterState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "SkeletonRanged",
                    skeletonRangedMovementIdleTicks: 10,
                    skeletonRangedMovementMode: 1)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        var savedMonster = Assert.Single(snapshot.Monsters);
        Assert.Equal((10, 1), (
            savedMonster.SkeletonRangedMovementIdleTicks,
            savedMonster.SkeletonRangedMovementMode));

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "SkeletonRanged")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal((10, 1), (
            restored.SkeletonRangedMovementIdleTicks,
            restored.SkeletonRangedMovementMode));
    }

    [Fact]
    public void SnapshotRoundTripsFatMovementCounterState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Fat",
                    fatMovementIdleTicks: 10,
                    fatMovementMode: 1)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        var savedMonster = Assert.Single(snapshot.Monsters);
        Assert.Equal((10, 1), (
            savedMonster.FatMovementIdleTicks,
            savedMonster.FatMovementMode));

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Fat")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal((10, 1), (
            restored.FatMovementIdleTicks,
            restored.FatMovementMode));
    }

    [Fact]
    public void SnapshotRoundTripsRhinoMovementAndDelayState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Rhino",
                    rhinoMovementIdleTicks: 10,
                    rhinoMovementMode: 1,
                    rhinoDelayTicks: 7)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        var savedMonster = Assert.Single(snapshot.Monsters);
        Assert.Equal((10, 1, 7), (
            savedMonster.RhinoMovementIdleTicks,
            savedMonster.RhinoMovementMode,
            savedMonster.RhinoDelayTicks));

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Rhino")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal((10, 1, 7), (
            restored.RhinoMovementIdleTicks,
            restored.RhinoMovementMode,
            restored.RhinoDelayTicks));
    }

    [Fact]
    public void SnapshotRoundTripsSkeletonKingDelayState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "SkeletonKing",
                    skeletonKingDelayTicks: 7)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        Assert.Equal(7, Assert.Single(snapshot.Monsters).SkeletonKingDelayTicks);

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "SkeletonKing")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal(7, restored.SkeletonKingDelayTicks);
    }

    [Fact]
    public void SnapshotRoundTripsHorkDemonDelayState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "HorkDemon",
                    horkDemonDelayTicks: 7)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        Assert.Equal(7, Assert.Single(snapshot.Monsters).HorkDemonDelayTicks);

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "HorkDemon")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal(7, restored.HorkDemonDelayTicks);
    }

    [Fact]
    public void SnapshotRoundTripsRangedAvoidanceDelayState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Magma",
                    rangedAvoidanceDelayTicks: 7)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        Assert.Equal(7, Assert.Single(snapshot.Monsters).RangedAvoidanceDelayTicks);

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Magma")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal(7, restored.RangedAvoidanceDelayTicks);
    }

    [Fact]
    public void SnapshotRoundTripsSnakeDelayState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Snake",
                    snakeDelayTicks: 7)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        Assert.Equal(7, Assert.Single(snapshot.Monsters).SnakeDelayTicks);

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Snake")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal(7, restored.SnakeDelayTicks);
    }

    [Fact]
    public void SnapshotRoundTripsCounselorDelayState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Counselor",
                    counselorDelayTicks: 7)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        Assert.Equal(7, Assert.Single(snapshot.Monsters).CounselorDelayTicks);

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Counselor")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal(7, restored.CounselorDelayTicks);
    }

    [Fact]
    public void SnapshotRoundTripsAvoidanceDelayState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "GoatMelee",
                    avoidanceDelayTicks: 7)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        Assert.Equal(7, Assert.Single(snapshot.Monsters).AvoidanceDelayTicks);

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "GoatMelee")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal(7, restored.AvoidanceDelayTicks);
    }

    [Fact]
    public void SnapshotRoundTripsRangedDelayState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "FireBat",
                    rangedDelayTicks: 7)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        Assert.Equal(7, Assert.Single(snapshot.Monsters).RangedDelayTicks);

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "FireBat")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal(7, restored.RangedDelayTicks);
    }

    [Fact]
    public void SnapshotRoundTripsMegaDelayState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    14,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Mega",
                    megaDelayTicks: 7)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        Assert.Equal(7, Assert.Single(snapshot.Monsters).MegaDelayTicks);

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    14,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Mega")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(replacement.WorldState.CombatTargets).Value;
        Assert.Equal(7, restored.MegaDelayTicks);
    }

    [Fact]
    public void SnapshotRoundTripsCounselorRetreatState()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Counselor",
                    counselorRetreatTicks: 3)],
            });
        var player = CreatePlayer();

        var snapshot = simulation.CaptureSnapshot(player, 1);
        Assert.Equal(3, Assert.Single(snapshot.Monsters).CounselorRetreatTicks);

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [new AuthoritativeCombatTarget(
                    9,
                    4,
                    2,
                    20,
                    levelId: 0,
                    nativeAi: "Counselor")],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        Assert.Equal(
            3,
            Assert.Single(replacement.WorldState.CombatTargets).Value.CounselorRetreatTicks);
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
    public void AdvanceTickActivatesQuestWhenPlayerIsAlreadyOnActiveSetLevel()
    {
        var activeSetLevel = AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(1, activeMap: true);
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(activeSetLevel, 32, 32));
        var quest = new AuthoritativeQuestState(
            3013,
            3,
            1,
            active: false,
            routeActivationVariable1: 1);
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                Quests = [quest],
                QuestTypes = new AuthoritativeQuestTypeCatalog([
                    new AuthoritativeQuestTypeDefinition(
                        3013,
                        3,
                        "The Curse of King Leoric",
                        3,
                        -1,
                        "DTYPE_CATHEDRAL",
                        1,
                        false),
                ]),
            });
        var player = CreatePlayer();
        player.LevelId = activeSetLevel;

        simulation.AdvanceTick(1, [player]);

        Assert.True(quest.Active);
        Assert.Equal(1, quest.Variable1);
    }

    [Fact]
    public void RestoreSnapshotActivatesQuestWhenPlayerIsAlreadyOnActiveSetLevel()
    {
        var activeSetLevel = AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(1, activeMap: true);
        var questTypes = new AuthoritativeQuestTypeCatalog([
            new AuthoritativeQuestTypeDefinition(
                3013,
                3,
                "The Curse of King Leoric",
                3,
                -1,
                "DTYPE_CATHEDRAL",
                1,
                false),
        ]);
        var sourceWorld = new AuthoritativeWorld();
        sourceWorld.AddLevel(new AuthoritativeLevel(activeSetLevel, 32, 32));
        var source = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = sourceWorld,
                Quests = [new AuthoritativeQuestState(
                    3013,
                    3,
                    1,
                    active: false,
                    routeActivationVariable1: 1)],
                QuestTypes = questTypes,
            });
        var sourcePlayer = CreatePlayer();
        sourcePlayer.LevelId = activeSetLevel;
        var snapshot = source.CaptureSnapshot(sourcePlayer, 1);

        var destinationWorld = new AuthoritativeWorld();
        destinationWorld.AddLevel(new AuthoritativeLevel(activeSetLevel, 32, 32));
        var destination = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = destinationWorld,
                Quests = [new AuthoritativeQuestState(
                    3013,
                    3,
                    1,
                    active: false,
                    routeActivationVariable1: 1)],
                QuestTypes = questTypes,
            });
        var destinationPlayer = CreatePlayer();

        destination.RestoreSnapshot(destinationPlayer, snapshot);

        var quest = Assert.Single(destination.WorldState.Quests.Values);
        Assert.True(quest.Active);
        Assert.Equal(1, quest.Variable1);
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
                    completionMonsterId: 12,
                    routeActivationVariable1: 1)],
            });
        var snapshot = simulation.CaptureSnapshot(CreatePlayer(), 3);

        var replacement = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                Quests = [new AuthoritativeQuestState(
                    40,
                    0,
                    2,
                    routeActivationVariable1: 1)],
            });
        replacement.RestoreSnapshot(CreatePlayer(), snapshot);

        var quest = Assert.Single(replacement.WorldState.Quests.Values);
        Assert.Equal((2, 7, 123), (quest.Variable1, quest.Variable2, quest.MessageId));
        Assert.Equal(12U, quest.CompletionMonsterId);
        Assert.Equal(1, quest.RouteActivationVariable1);
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
                destroyed: true,
                isShrine: true)],
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
        Assert.Equal(14UL, restoredMonster.MovementStartedTick);
        Assert.Equal(15UL, restoredMonster.MovementEffectTick);
        Assert.True(restoredMonster.IsPetrified);
        Assert.Equal(99U, Assert.Single(replacement.WorldState.WorldItems).Value.ItemSeed);
        var restoredObject = Assert.Single(replacement.WorldState.Objects).Value;
        Assert.Equal(40U, restoredObject.QuestId);
        Assert.Equal(2U, restoredObject.QuestProgressAmount);
        Assert.True(restoredObject.Breakable);
        Assert.True(restoredObject.Destroyed);
        Assert.True(restoredObject.IsShrine);
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
    public void SnapshotRestoreHydratesOmittedAuthoredItemMetadataByMiscId()
    {
        var definition = new AuthoritativeItemDefinition(
            7,
            1,
            100,
            150,
            3,
            7,
            0,
            20,
            40,
            2,
            1,
            true) {
            NativeItemIndex = 101,
            MiscId = 121,
        };
        var items = new AuthoritativeItemCatalog([
            definition,
            new AuthoritativeItemDefinition(
                24,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                1,
                true) {
                NativeItemIndex = 23,
                MiscId = 44,
            },
        ]);
        var legacyState = AuthoritativeItemState.Empty with {
            ItemIndex = 999,
            MiscId = 121,
        };
        var storeCatalog = new StoreCatalog();
        storeCatalog.AddStore(1, [new StoreItem(0, 44, 25, legacyState)]);
        var options = new DiabloSimulationOptions(
            storeCatalog,
            DiabloModule.Instance.Rules) {
            Items = items,
        };
        var simulation = DiabloSimulationFactory.Instance.Create(options);
        var source = CreatePlayer();
        source.ActiveStoreId = 1;
        source.Inventory.Add(new OwnedStoreItem(0, 0, 42, 25, 0, legacyState));
        source.InventoryGrid[0] = 0;
        source.InventoryGrid[1] = 0;
        simulation.WorldState.WorldItems.Add(
            20,
            new AuthoritativeWorldItem(20, 0, 2, 0, 43, 25, legacyState));

        var captured = simulation.CaptureSnapshot(source, 1);
        Assert.NotNull(captured.ActiveStore);
        var capturedStore = captured.ActiveStore!;
        var legacySnapshot = captured with {
            Player = captured.Player with {
                Inventory = [captured.Player.Inventory[0] with {
                    State = legacyState,
                }],
            },
            WorldItems = [captured.WorldItems[0] with { State = legacyState }],
            ActiveStore = capturedStore with {
                Items = [capturedStore.Items[0] with { State = legacyState }],
            },
        };
        var destination = CreatePlayer();
        var replacement = DiabloSimulationFactory.Instance.Create(options);

        replacement.RestoreSnapshot(destination, legacySnapshot);

        var restoredInventoryItem = Assert.Single(destination.Inventory);
        Assert.Equal(2, restoredInventoryItem.State.InventoryWidth);
        Assert.Equal(1, restoredInventoryItem.State.InventoryHeight);
        Assert.Equal(100, restoredInventoryItem.State.Value);
        Assert.Equal(150, restoredInventoryItem.State.IdentifiedValue);
        Assert.Equal(3, restoredInventoryItem.State.MinDamage);
        Assert.Equal(7, restoredInventoryItem.State.MaxDamage);
        Assert.Equal(20, restoredInventoryItem.State.Durability);
        Assert.Equal(40, restoredInventoryItem.State.MaxDurability);
        Assert.Equal([0, 0], destination.InventoryGrid.Take(2));
        var restoredWorldItem = Assert.Single(replacement.WorldState.WorldItems).Value;
        Assert.Equal(2, restoredWorldItem.State.InventoryWidth);
        Assert.Equal(1, restoredWorldItem.State.InventoryHeight);
        Assert.Equal(100, restoredWorldItem.State.Value);
        Assert.Equal(150, restoredWorldItem.State.IdentifiedValue);
        Assert.Equal(3, restoredWorldItem.State.MinDamage);
        Assert.Equal(7, restoredWorldItem.State.MaxDamage);
        Assert.Equal(20, restoredWorldItem.State.Durability);
        Assert.Equal(40, restoredWorldItem.State.MaxDurability);
        var restoredSnapshot = replacement.CaptureSnapshot(destination, 2);
        Assert.NotNull(restoredSnapshot.ActiveStore);
        var restoredStore = restoredSnapshot.ActiveStore!;
        Assert.Equal(2, restoredStore.Items[0].State.InventoryWidth);
        Assert.Equal(1, restoredStore.Items[0].State.InventoryHeight);
        Assert.Equal(100, restoredStore.Items[0].State.Value);
        Assert.Equal(150, restoredStore.Items[0].State.IdentifiedValue);
        Assert.Equal(3, restoredStore.Items[0].State.MinDamage);
        Assert.Equal(7, restoredStore.Items[0].State.MaxDamage);
        Assert.Equal(20, restoredStore.Items[0].State.Durability);
        Assert.Equal(40, restoredStore.Items[0].State.MaxDurability);
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

        Assert.Contains(0U, destinationPlayer.VisitedLevelIds);
        Assert.Contains(1U, destinationPlayer.VisitedLevelIds);
        var restoredPortal = Assert.Single(restored.WorldState.Portals.Values);
        Assert.Equal(portal.PortalId, restoredPortal.PortalId);
        Assert.Equal(portal.RemainingTicks, restoredPortal.RemainingTicks);
        Assert.Equal(portal.OwnerEntityId, restoredPortal.OwnerEntityId);
        Assert.True(restoredPortal.Bidirectional);
    }

    [Fact]
    public void SnapshotRoundTripsMaterializedSetLevelWithoutRegularVisitedMarker()
    {
        var runtimeLevelId = AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(0, activeMap: true);
        var simulation = DiabloSimulationFactory.Instance.Create(new DiabloSimulationOptions(
            new StoreCatalog(),
            DiabloModule.Instance.Rules) {
            FallbackWidth = 80,
            FallbackHeight = 80,
        });
        var sourcePlayer = CreatePlayer();
        sourcePlayer.LevelId = runtimeLevelId;
        sourcePlayer.VisitedLevelIds.Clear();

        var snapshot = simulation.CaptureSnapshot(sourcePlayer, 5);

        Assert.Equal(runtimeLevelId, snapshot.Player.LevelId);
        Assert.Empty(snapshot.Player.VisitedLevelIds);

        var destination = DiabloSimulationFactory.Instance.Create(new DiabloSimulationOptions(
            new StoreCatalog(),
            DiabloModule.Instance.Rules) {
            FallbackWidth = 80,
            FallbackHeight = 80,
        });
        var destinationPlayer = CreatePlayer();
        destination.RestoreSnapshot(destinationPlayer, snapshot);

        Assert.Equal(runtimeLevelId, destinationPlayer.LevelId);
        Assert.Empty(destinationPlayer.VisitedLevelIds);
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
    public void SnapshotRestoreFailurePreservesPreviousWorldMutationProjection()
    {
        var mutations = new AuthoritativeWorldMutationCatalog([
            new AuthoritativeWorldMutation(
                7,
                0,
                geometryRectanglesAdd: [new AuthoritativeWorldMutationRectangle(1, 1, 1, 1)]),
            new AuthoritativeWorldMutation(
                8,
                0,
                geometryRectanglesAdd: [new AuthoritativeWorldMutationRectangle(2, 2, 1, 1)]),
        ]);
        var world = AuthoritativeWorld.LoadTsv(
            "levels.tsv",
            "level_id\twidth\theight\tblocked_cells\n0\t4\t4\tnone\n");
        var source = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                WorldMutations = mutations,
            });
        var destination = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
                WorldMutations = mutations,
            });
        Assert.True(source.WorldState.ApplyWorldMutation(7));
        Assert.True(destination.WorldState.ApplyWorldMutation(8));

        var sourcePlayer = CreatePlayer();
        var snapshot = source.CaptureSnapshot(sourcePlayer, 1) with {
            Player = source.CapturePlayerState(sourcePlayer) with {
                Attributes = PlayerAttributesState.Zero with {
                    Strength = new PlayerAttributeState(-1, 0),
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            destination.RestoreSnapshot(CreatePlayer(), snapshot));

        Assert.Equal([8U], destination.WorldState.ActiveWorldMutationIds);
        Assert.True(destination.WorldState.IsWalkable(0, 1, 1));
        Assert.False(destination.WorldState.IsWalkable(0, 2, 2));
    }

    [Fact]
    public void SnapshotRestoreRejectsUnrepresentablePlayerAndWorldItemPrices()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules));
        var source = CreatePlayer();
        var playerSnapshot = simulation.CaptureSnapshot(source, 1) with {
            Player = simulation.CapturePlayerState(source) with {
                Inventory = [new OwnedStoreItem(
                    0,
                    0,
                    42,
                    uint.MaxValue,
                    0,
                    AuthoritativeItemState.Empty with {
                        ItemType = 1,
                        ItemIndex = 1,
                    })],
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(CreatePlayer(), playerSnapshot));

        var worldSnapshot = simulation.CaptureSnapshot(source, 1) with {
            WorldItems = [new DiabloWorldItemState(
                20,
                0,
                1,
                0,
                42,
                uint.MaxValue,
                AuthoritativeItemState.Empty with {
                    ItemType = 1,
                    ItemIndex = 1,
                })],
        };
        Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(CreatePlayer(), worldSnapshot));

        var storeCatalog = new StoreCatalog();
        storeCatalog.AddStore(1, [new StoreItem(0, 42, 10)]);
        var storeSimulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                storeCatalog,
                DiabloModule.Instance.Rules));
        var storeSource = CreatePlayer();
        storeSource.ActiveStoreId = 1;
        var storeSnapshot = storeSimulation.CaptureSnapshot(storeSource, 1);
        var invalidStore = storeSnapshot with {
            ActiveStore = storeSnapshot.ActiveStore! with {
                Items = [storeSnapshot.ActiveStore.Items[0] with { Price = uint.MaxValue }],
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            storeSimulation.RestoreSnapshot(CreatePlayer(), invalidStore));
    }

    [Fact]
    public void SnapshotRestoreRejectsUnknownStableItemIdWhenCatalogIsActive()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, false),
            new AuthoritativeItemDefinition(24, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, false) {
                NativeItemIndex = 23,
                MiscId = 44,
            },
        ]);
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules) {
                Items = items,
            });
        var source = CreatePlayer();
        var snapshot = simulation.CaptureSnapshot(source, 1) with {
            Player = simulation.CapturePlayerState(source) with {
                HeldItem = new OwnedStoreItem(
                    0,
                    0,
                    42,
                    5,
                    0,
                    AuthoritativeItemState.Empty with {
                        StableItemId = 999,
                        ItemIndex = 7,
                        ItemType = 1,
                    }),
            },
        };
        var destination = CreatePlayer();

        Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(destination, snapshot));
        Assert.Null(destination.HeldItem);
    }

    [Fact]
    public void SnapshotRestoreAcceptsLegacyZeroStableItemIdWhenCatalogIsActive()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(7, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, false),
            new AuthoritativeItemDefinition(24, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, false) {
                NativeItemIndex = 23,
                MiscId = 44,
            },
        ]);
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules) {
                Items = items,
            });
        var source = CreatePlayer();
        var snapshot = simulation.CaptureSnapshot(source, 1) with {
            Player = simulation.CapturePlayerState(source) with {
                HeldItem = new OwnedStoreItem(
                    0,
                    0,
                    42,
                    5,
                    0,
                    AuthoritativeItemState.Empty with {
                        ItemIndex = 7,
                        ItemType = 1,
                    }),
            },
        };
        var destination = CreatePlayer();

        simulation.RestoreSnapshot(destination, snapshot);

        Assert.Equal(7, destination.HeldItem!.State.ItemIndex);
        Assert.Equal(0U, destination.HeldItem.State.StableItemId);
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
        var malformedEtherealizeMagnitude = snapshot with {
            Player = snapshot.Player with {
                StatusEffects = [new AuthoritativeStatusEffect(53, uint.MaxValue, 1) {
                    Behavior = AuthoritativeStatusEffectBehavior.Etherealize,
                }],
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(CreatePlayer(), duplicateIds));
        Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(CreatePlayer(), malformedEtherealizeMagnitude));
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
    public void SnapshotRestoreRejectsUnvalidatedMonsterMissileIdentity()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules));
        var player = CreatePlayer();
        var snapshot = simulation.CaptureSnapshot(player, 1) with {
            Projectiles = [new DiabloProjectileState(
                50, player.EntityId, 0, 4, 0, 1, 1, 1, 1, 1,
                AuthoritativeDamageType.Magic, 0, 3) {
                Behavior = AuthoritativeProjectileBehavior.MonsterMagic,
                NativeMissileId = "NotInActiveContent",
            }],
        };

        Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(CreatePlayer(), snapshot));
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
    public void SnapshotRestoreHydratesOmittedGoldValueFieldsFromWorldPrice()
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
                75,
                AuthoritativeItemState.Empty with {
                    ItemType = 11,
                    ItemIndex = 0,
                    StatFlag = true,
                })],
        };

        simulation.RestoreSnapshot(CreatePlayer(), snapshot);

        var restored = Assert.Single(simulation.WorldState.WorldItems).Value;
        Assert.Equal(75, restored.State.Value);
        Assert.Equal(75, restored.State.IdentifiedValue);
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
    public void SnapshotRestoreRollbackUsesLiveActionTickWhenIncomingSnapshotIsOlder()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
            });
        var destination = CreatePlayer();
        destination.LevelId = 1;
        destination.PositionX = 1;
        destination.PositionY = 1;
        destination.CombatMode = DiabloPlayerCombatMode.WalkSouthwards;
        destination.CombatModeEndTick = 10;
        destination.CurrentAction = new AuthoritativePlayerActionState(
            DiabloPlayerActionKind.Move,
            2,
            3,
            4,
            10) {
            DirectionY = 1,
        };

        var valid = simulation.CaptureSnapshot(destination, 2);
        var invalid = valid with {
            Tick = 1,
            Player = valid.Player with { CharacterClassId = 99 },
        };

        Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(destination, invalid));
        Assert.Equal(0U, destination.CharacterClassId);
        Assert.Equal(2UL, destination.CurrentAction!.StartedAtTick);
    }

    [Fact]
    public void SnapshotRestoreAllowsInFlightMoveToBeCancelledByNewGeometry()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 4, 4));
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                World = world,
            });
        var source = CreatePlayer();
        source.LevelId = 1;
        source.CombatMode = DiabloPlayerCombatMode.WalkSouthwards;
        source.CombatModeEndTick = 4;
        source.CurrentAction = new AuthoritativePlayerActionState(
            DiabloPlayerActionKind.Move,
            1,
            2,
            3,
            4) {
            DirectionX = 1,
        };

        var snapshot = simulation.CaptureSnapshot(source, 1);
        world.ReplaceLevel(new AuthoritativeLevel(
            1,
            4,
            4,
            blockedCells: [1]));
        var destination = CreatePlayer();
        destination.LevelId = 1;

        simulation.RestoreSnapshot(destination, snapshot);
        simulation.AdvanceTick(2, [destination]);

        Assert.Equal((0, 0), (destination.PositionX, destination.PositionY));
        Assert.True(destination.CurrentAction!.EffectApplied);
    }

    [Fact]
    public void SnapshotRestoreRejectsTileTargetedCastOutsideWorldGeometry()
    {
        var world = new AuthoritativeWorld();
        world.AddLevel(new AuthoritativeLevel(1, 10, 10));
        var options = new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
            World = world,
        };
        var simulation = DiabloSimulationFactory.Instance.Create(options);
        var player = CreatePlayer();
        player.LevelId = 1;
        player.CombatMode = DiabloPlayerCombatMode.Spell;
        player.CombatModeEndTick = 3;
        player.CurrentAction = new AuthoritativePlayerActionState(
            DiabloPlayerActionKind.Cast,
            1,
            1,
            2,
            3) {
            CastIntent = new DiabloCastIntent(
                1,
                0,
                4,
                5,
                DiabloCastSourceKind.Ability),
        };

        var snapshot = simulation.CaptureSnapshot(player, 1);
        var invalid = snapshot with {
            Player = snapshot.Player with {
                CurrentAction = snapshot.Player.CurrentAction! with {
                    CastIntent = snapshot.Player.CurrentAction.CastIntent! with { TargetX = 10 },
                },
            },
        };
        var replacement = DiabloSimulationFactory.Instance.Create(options);
        var destination = CreatePlayer();
        destination.LevelId = 1;

        Assert.Throws<InvalidDataException>(() =>
            replacement.RestoreSnapshot(destination, invalid));

        var invalidSource = snapshot with {
            Player = snapshot.Player with {
                CurrentAction = snapshot.Player.CurrentAction! with {
                    CastIntent = snapshot.Player.CurrentAction.CastIntent! with {
                        SourceItem = new DiabloPlayerItemReference(
                            DiabloPlayerItemLocation.Inventory,
                            40),
                    },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            replacement.RestoreSnapshot(destination, invalidSource));

        var invalidProgress = snapshot with {
            Player = snapshot.Player with {
                CurrentAction = snapshot.Player.CurrentAction! with {
                    EffectTick = 2,
                    ReadyTick = 2,
                    EffectApplied = true,
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            replacement.RestoreSnapshot(destination, invalidProgress));

        var invalidActionCoordinates = snapshot with {
            Player = snapshot.Player with {
                CurrentAction = snapshot.Player.CurrentAction! with {
                    TargetX = 1,
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            replacement.RestoreSnapshot(destination, invalidActionCoordinates));
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

        var invalidDeathSnapshot = deathSnapshot with {
            Player = deathSnapshot.Player with {
                CurrentAction = deathSnapshot.Player.CurrentAction! with {
                    TargetEntityId = 0,
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(CreatePlayer(), invalidDeathSnapshot));

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

    [Fact]
    public void PlayerTemplateRejectsOversizedStartupCollections()
    {
        var exception = Assert.Throws<InvalidDataException>(() => new DiabloPlayerTemplate(
            gold: 0,
            experience: 0,
            life: 1,
            mana: 0,
            manaMaximum: 0,
            attributes: PlayerAttributesState.Zero,
            equipment: [],
            inventoryGrid: Enumerable.Repeat(-1, 401).ToArray(),
            belt: [],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 1,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0));

        Assert.Contains("too many inventory-grid entries", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerTemplateRejectsDuplicateEquipmentAndBeltSlots()
    {
        var equipmentException = Assert.Throws<InvalidDataException>(() => new DiabloPlayerTemplate(
            gold: 0,
            experience: 0,
            life: 1,
            mana: 0,
            manaMaximum: 0,
            attributes: PlayerAttributesState.Zero,
            equipment: [new EquippedStoreItem(1, 1), new EquippedStoreItem(1, 2)],
            inventoryGrid: [],
            belt: [],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 1,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0));
        Assert.Contains("duplicate equipment slots", equipmentException.Message, StringComparison.Ordinal);

        var beltException = Assert.Throws<InvalidDataException>(() => new DiabloPlayerTemplate(
            gold: 0,
            experience: 0,
            life: 1,
            mana: 0,
            manaMaximum: 0,
            attributes: PlayerAttributesState.Zero,
            equipment: [],
            inventoryGrid: [],
            belt: [new BeltStoreItem(2, 1), new BeltStoreItem(2, 2)],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 1,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0));
        Assert.Contains("duplicate belt slots", beltException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerTemplateRejectsInvalidInventoryGridAndMemorizedSpells()
    {
        var gridException = Assert.Throws<InvalidDataException>(() => new DiabloPlayerTemplate(
            gold: 0,
            experience: 0,
            life: 1,
            mana: 0,
            manaMaximum: 0,
            attributes: PlayerAttributesState.Zero,
            equipment: [],
            inventoryGrid: [0],
            belt: [],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 1,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0,
            inventory: []));
        Assert.Contains("invalid inventory-grid reference", gridException.Message, StringComparison.Ordinal);

        var spellException = Assert.Throws<InvalidDataException>(() => new DiabloPlayerTemplate(
            gold: 0,
            experience: 0,
            life: 1,
            mana: 0,
            manaMaximum: 0,
            attributes: PlayerAttributesState.Zero,
            equipment: [],
            inventoryGrid: [],
            belt: [],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 1,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0,
            memorizedSpells: [new MemorizedSpellState(1, 1), new MemorizedSpellState(1, 2)]));
        Assert.Contains("invalid memorized-spell state", spellException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerTemplateRejectsInvalidItemSlotsAndInventoryFootprints()
    {
        var equipmentException = Assert.Throws<InvalidDataException>(() => new DiabloPlayerTemplate(
            gold: 0,
            experience: 0,
            life: 1,
            mana: 0,
            manaMaximum: 0,
            attributes: PlayerAttributesState.Zero,
            equipment: [new EquippedStoreItem(7, 1)],
            inventoryGrid: [],
            belt: [],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 1,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0));
        Assert.Contains("invalid equipment slot", equipmentException.Message, StringComparison.Ordinal);

        var beltException = Assert.Throws<InvalidDataException>(() => new DiabloPlayerTemplate(
            gold: 0,
            experience: 0,
            life: 1,
            mana: 0,
            manaMaximum: 0,
            attributes: PlayerAttributesState.Zero,
            equipment: [],
            inventoryGrid: [],
            belt: [new BeltStoreItem(8, 1)],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 1,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0));
        Assert.Contains("invalid belt slot", beltException.Message, StringComparison.Ordinal);

        var itemState = AuthoritativeItemState.Empty with { InventoryWidth = 2 };
        var footprintException = Assert.Throws<InvalidDataException>(() => new DiabloPlayerTemplate(
            gold: 0,
            experience: 0,
            life: 1,
            mana: 0,
            manaMaximum: 0,
            attributes: PlayerAttributesState.Zero,
            equipment: [],
            inventoryGrid: [0, -1],
            belt: [],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 1,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0,
            inventory: [new OwnedStoreItem(0, 0, 1, 0, 0, itemState)]));
        Assert.Contains("invalid inventory footprint", footprintException.Message, StringComparison.Ordinal);

        var dimensionException = Assert.Throws<InvalidDataException>(() => new DiabloPlayerTemplate(
            gold: 0,
            experience: 0,
            life: 1,
            mana: 0,
            manaMaximum: 0,
            attributes: PlayerAttributesState.Zero,
            equipment: [],
            inventoryGrid: [],
            belt: [],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 1,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0,
            inventory: [new OwnedStoreItem(
                0,
                0,
                1,
                0,
                0,
                AuthoritativeItemState.Empty with { InventoryWidth = 0 })]));
        Assert.Contains("invalid inventory item state", dimensionException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerTemplateRejectsInvalidScalarState()
    {
        var resourceException = Assert.Throws<InvalidDataException>(() => new DiabloPlayerTemplate(
            gold: 0,
            experience: 0,
            life: -1,
            mana: 0,
            manaMaximum: 0,
            attributes: PlayerAttributesState.Zero,
            equipment: [],
            inventoryGrid: [],
            belt: [],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 1,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0));
        Assert.Contains("out-of-range resource state", resourceException.Message, StringComparison.Ordinal);

        var abilityException = Assert.Throws<InvalidDataException>(() => new DiabloPlayerTemplate(
            gold: 0,
            experience: 0,
            life: 1,
            mana: 0,
            manaMaximum: 0,
            attributes: PlayerAttributesState.Zero,
            equipment: [],
            inventoryGrid: [],
            belt: [],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 1,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0,
            abilitySpellId: AuthoritativePlayerLoadoutCatalog.MaximumSpellId + 1));
        Assert.Contains("out-of-range ability spell", abilityException.Message, StringComparison.Ordinal);

        var reflectException = Assert.Throws<InvalidDataException>(() => new DiabloPlayerTemplate(
            gold: 0,
            experience: 0,
            life: 1,
            mana: 0,
            manaMaximum: 0,
            attributes: PlayerAttributesState.Zero,
            equipment: [],
            inventoryGrid: [],
            belt: [],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 1,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0,
            reflectCharges: ushort.MaxValue + 1U));
        Assert.Contains("out-of-range reflect charges", reflectException.Message, StringComparison.Ordinal);

        var timingException = Assert.Throws<InvalidDataException>(() => new DiabloPlayerTemplate(
            gold: 0,
            experience: 0,
            life: 1,
            mana: 0,
            manaMaximum: 0,
            attributes: PlayerAttributesState.Zero,
            equipment: [],
            inventoryGrid: [],
            belt: [],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 1,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0,
            combatModeEndTick: 1));
        Assert.Contains("invalid combat-mode timing", timingException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerTemplateRejectsInvalidAttributeState()
    {
        var negativeException = Assert.Throws<InvalidDataException>(() => new DiabloPlayerTemplate(
            gold: 0,
            experience: 0,
            life: 1,
            mana: 0,
            manaMaximum: 0,
            attributes: PlayerAttributesState.Zero with {
                Strength = new PlayerAttributeState(-1, 0),
            },
            equipment: [],
            inventoryGrid: [],
            belt: [],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 1,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0));
        Assert.Contains("invalid attribute state", negativeException.Message, StringComparison.Ordinal);

        var nullException = Assert.Throws<InvalidDataException>(() => new DiabloPlayerTemplate(
            gold: 0,
            experience: 0,
            life: 1,
            mana: 0,
            manaMaximum: 0,
            attributes: PlayerAttributesState.Zero with {
                Vitality = null!,
            },
            equipment: [],
            inventoryGrid: [],
            belt: [],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 1,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0));
        Assert.Contains("invalid attribute state", nullException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerTemplateRejectsDuplicateItemIdentitiesAcrossCollections()
    {
        var state = AuthoritativeItemState.Empty;
        var exception = Assert.Throws<InvalidDataException>(() => new DiabloPlayerTemplate(
            gold: 0,
            experience: 0,
            life: 1,
            mana: 0,
            manaMaximum: 0,
            attributes: PlayerAttributesState.Zero,
            equipment: [new EquippedStoreItem(0, 42, state)],
            inventoryGrid: [0],
            belt: [],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 1,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0,
            inventory: [new OwnedStoreItem(0, 0, 42, 0, 0, state)]));
        Assert.Contains("duplicate item identity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerTemplateRejectsInvalidItemIdentityMetadata()
    {
        var emptyIdentityException = Assert.Throws<InvalidDataException>(() => new DiabloPlayerTemplate(
            gold: 0,
            experience: 0,
            life: 1,
            mana: 0,
            manaMaximum: 0,
            attributes: PlayerAttributesState.Zero,
            equipment: [],
            inventoryGrid: [0],
            belt: [],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 1,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0,
            inventory: [new OwnedStoreItem(0, 0, 0, 0, 0, AuthoritativeItemState.Empty)]));
        Assert.Contains("invalid item identity metadata", emptyIdentityException.Message, StringComparison.Ordinal);

        var presentationException = Assert.Throws<InvalidDataException>(() => new DiabloPlayerTemplate(
            gold: 0,
            experience: 0,
            life: 1,
            mana: 0,
            manaMaximum: 0,
            attributes: PlayerAttributesState.Zero,
            equipment: [],
            inventoryGrid: [0],
            belt: [],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 1,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0,
            inventory: [new OwnedStoreItem(
                0,
                0,
                1,
                0,
                0,
                AuthoritativeItemState.Empty with {
                    BaseName = "Valid",
                    NativeCursorGraphic = 256,
                })]));
        Assert.Contains("invalid item identity metadata", presentationException.Message, StringComparison.Ordinal);
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

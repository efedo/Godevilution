using Devilution.Protocol.V1;
using Devilution.Protocol;
using Devilution.Server.Protocol;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeSnapshotValidatorTests
{
    [Fact]
    public void AcceptsLegacyOmittedAndCanonicalStateHashes()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot());
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            StateSha256 = new string('a', AuthoritativeSnapshotValidator.Sha256HexLength),
        });
    }

    [Fact]
    public void AcceptsDistinctEntityIdsAcrossProjectedCollections()
    {
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1 } },
            Monsters = { new MonsterSnapshot { EntityId = 2 } },
            WorldItems = {
                new WorldItemSnapshot {
                    EntityId = 3,
                    ItemSeed = 1,
                    State = new ItemStateSnapshot(),
                },
            },
            Objects = { new ObjectSnapshot { EntityId = 4, ObjectId = 1 } },
            Projectiles = {
                new ProjectileSnapshot {
                    EntityId = 5,
                    SourceEntityId = 2,
                    SpellId = 1,
                    RemainingTicks = 1,
                },
            },
            Corpses = { new CorpseSnapshot { EntityId = 6 } },
        };

        AuthoritativeSnapshotValidator.Validate(snapshot);
    }

    [Fact]
    public void RejectsSnapshotsBeyondNativePlayerCapacity()
    {
        var snapshot = new Snapshot();
        for (var index = 1; index <= 5; index++)
            snapshot.Players.Add(new PlayerSnapshot { EntityId = (uint)index });

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void RejectsSnapshotsBeyondNativeWarpTriggerCapacityPerLevel()
    {
        var snapshot = new Snapshot {
            LevelGeometry = { new LevelGeometrySnapshot { LevelId = 1 } },
        };
        for (var index = 1; index <= 8; index++)
            snapshot.LevelGeometry[0].WarpTriggers.Add(new WarpTriggerSnapshot {
                WarpId = (uint)index,
            });

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void AcceptsValidQuestAndQuestTownerInteractionProjection()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            Quests = {
                new QuestSnapshot {
                    QuestId = 1,
                    RequiredProgress = 1,
                    NativeQuestId = 1,
                },
            },
            LevelGeometry = {
                new LevelGeometrySnapshot { LevelId = 1, Width = 10, Height = 10 },
            },
            QuestTownerInteractions = {
                new QuestTownerInteractionSnapshot {
                    InteractionId = 1,
                    QuestId = 1,
                    LevelId = 1,
                    PositionX = 2,
                    PositionY = 3,
                    MessageId = 1,
                },
            },
        });
    }

    [Fact]
    public void AcceptsValidTownerGreetingProjection()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            LevelGeometry = {
                new LevelGeometrySnapshot { LevelId = 1, Width = 10, Height = 10 },
            },
            TownerGreetings = {
                new TownerGreetingSnapshot {
                    TownerId = 4,
                    LevelId = 1,
                    PositionX = 2,
                    PositionY = 3,
                    MessageId = 150,
                },
            },
        });
    }

    [Fact]
    public void RejectsMalformedTownerGreetings()
    {
        var duplicate = new Snapshot {
            TownerGreetings = {
                new TownerGreetingSnapshot { TownerId = 4, MessageId = 150 },
                new TownerGreetingSnapshot { TownerId = 4, MessageId = 151 },
            },
        };
        var invalidMessage = new Snapshot {
            TownerGreetings = {
                new TownerGreetingSnapshot { TownerId = 4, MessageId = 0 },
            },
        };
        var outsideGeometry = new Snapshot {
            LevelGeometry = {
                new LevelGeometrySnapshot { LevelId = 1, Width = 10, Height = 10 },
            },
            TownerGreetings = {
                new TownerGreetingSnapshot {
                    TownerId = 4,
                    LevelId = 1,
                    PositionX = 10,
                    MessageId = 150,
                },
            },
        };

        foreach (var snapshot in new[] { duplicate, invalidMessage, outsideGeometry })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void RejectsInvalidQuestStateAndQuestTownerInteractions()
    {
        var invalidNativeQuest = new Snapshot {
            Quests = {
                new QuestSnapshot {
                    QuestId = 1,
                    RequiredProgress = 1,
                    NativeQuestId = 24,
                },
            },
        };
        var invalidProgress = new Snapshot {
            Quests = {
                new QuestSnapshot {
                    QuestId = 1,
                    RequiredProgress = 1,
                    Progress = 2,
                },
            },
        };
        var invalidInteraction = new Snapshot {
            Quests = {
                new QuestSnapshot { QuestId = 1, RequiredProgress = 1 },
            },
            LevelGeometry = {
                new LevelGeometrySnapshot { LevelId = 1, Width = 10, Height = 10 },
            },
            QuestTownerInteractions = {
                new QuestTownerInteractionSnapshot {
                    InteractionId = 1,
                    QuestId = 2,
                    LevelId = 1,
                    PositionX = 2,
                    PositionY = 3,
                    MessageId = 1,
                },
            },
        };

        foreach (var snapshot in new[] {
            invalidNativeQuest,
            invalidProgress,
            invalidInteraction,
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void RejectsMalformedLevelGeometryAndWarpTargets()
    {
        var oversized = new Snapshot {
            LevelGeometry = {
                new LevelGeometrySnapshot { LevelId = 1, Width = 113, Height = 10 },
            },
        };
        var duplicateCell = new Snapshot {
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 10,
                    Height = 10,
                    BlockedCells = { 4, 4 },
                },
            },
        };
        var invalidLayerValue = new Snapshot {
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 1,
                    Height = 1,
                    NativeObjectLayer = { 147 },
                },
            },
        };
        var unknownWarpTarget = new Snapshot {
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 10,
                    Height = 10,
                    WarpTriggers = {
                        new WarpTriggerSnapshot {
                            WarpId = 1,
                            TargetLevelId = 2,
                            TargetX = 1,
                            TargetY = 1,
                            Available = true,
                        },
                    },
                },
            },
        };

        foreach (var snapshot in new[] {
            oversized,
            duplicateCell,
            invalidLayerValue,
            unknownWarpTarget,
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void RejectsEntityPositionsAndAuxiliaryLevelStateOutsideNativeBounds()
    {
        var invalidPlayerPosition = new Snapshot {
            LevelGeometry = {
                new LevelGeometrySnapshot { LevelId = 1, Width = 10, Height = 10 },
            },
            Players = {
                new PlayerSnapshot { EntityId = 1, LevelId = 1, PositionX = 10, PositionY = 1 },
            },
        };
        var invalidMovingMonsterPosition = new Snapshot {
            LevelGeometry = {
                new LevelGeometrySnapshot { LevelId = 1, Width = 10, Height = 10 },
            },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    LevelId = 1,
                    PositionX = 1,
                    PositionY = 1,
                    Moving = true,
                    FuturePositionX = 10,
                    FuturePositionY = 1,
                },
            },
        };
        var invalidOrigin = new Snapshot {
            LevelGeometry = {
                new LevelGeometrySnapshot { LevelId = 1, Width = 10, Height = 10 },
            },
            SetLevelObjectOrigins = {
                new SetLevelObjectOriginSnapshot {
                    SetLevelId = 1,
                    LevelId = 1,
                    PositionX = 10,
                    PositionY = 1,
                },
            },
        };
        var duplicateMaterializedLevel = new Snapshot {
            MaterializedProceduralLevelIds = { 7, 7 },
        };

        foreach (var snapshot in new[] {
            invalidPlayerPosition,
            invalidMovingMonsterPosition,
            invalidOrigin,
            duplicateMaterializedLevel,
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void AcceptsValidPlayerActionProjection()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            Tick = 1,
            LevelGeometry = {
                new LevelGeometrySnapshot { LevelId = 1, Width = 10, Height = 10 },
            },
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    LevelId = 1,
                    PositionX = 1,
                    PositionY = 1,
                    CombatMode = PlayerCombatMode.WalkNorthwards,
                    CombatModeEndTick = 3,
                    CurrentAction = new PlayerActionSnapshot {
                        Kind = PlayerActionKind.Move,
                        StartedAtTick = 1,
                        EffectTick = 1,
                        ReadyTick = 2,
                        EndTick = 3,
                        EffectApplied = true,
                        DirectionX = 1,
                        DirectionY = 0,
                    },
                },
            },
        });
    }

    [Fact]
    public void AcceptsCellTargetedAttackAndRejectsItsInvalidDestination()
    {
        var valid = new Snapshot {
            Tick = 1,
            LevelGeometry = {
                new LevelGeometrySnapshot { LevelId = 1, Width = 10, Height = 10 },
            },
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    LevelId = 1,
                    CombatMode = PlayerCombatMode.Attack,
                    CombatModeEndTick = 3,
                    CurrentAction = new PlayerActionSnapshot {
                        Kind = PlayerActionKind.Attack,
                        StartedAtTick = 1,
                        EffectTick = 1,
                        ReadyTick = 2,
                        EndTick = 3,
                        EffectApplied = true,
                        TargetCell = true,
                        TargetX = 4,
                        TargetY = 5,
                    },
                },
            },
        };

        AuthoritativeSnapshotValidator.Validate(valid);

        var invalid = valid.Clone();
        invalid.Players[0].CurrentAction.TargetX = 10;
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(invalid));
    }

    [Fact]
    public void AcceptsTileTargetedCastAndRejectsItsInvalidDestination()
    {
        var valid = new Snapshot {
            Tick = 1,
            LevelGeometry = {
                new LevelGeometrySnapshot { LevelId = 1, Width = 10, Height = 10 },
            },
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    LevelId = 1,
                    CombatMode = PlayerCombatMode.Spell,
                    CombatModeEndTick = 3,
                    CurrentAction = new PlayerActionSnapshot {
                        Kind = PlayerActionKind.Cast,
                        StartedAtTick = 1,
                        EffectTick = 1,
                        ReadyTick = 2,
                        EndTick = 3,
                        EffectApplied = true,
                        Cast = new CastRequested {
                            SpellId = 1,
                            TargetX = 4,
                            TargetY = 5,
                            Source = CastSourceKind.Ability,
                        },
                    },
                },
            },
        };

        AuthoritativeSnapshotValidator.Validate(valid);

        var invalid = valid.Clone();
        invalid.Players[0].CurrentAction.Cast.TargetX = 10;
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(invalid));
    }

    [Fact]
    public void RejectsInvalidPlayerActionTimingMovementAndCastSource()
    {
        var invalidTiming = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    CombatMode = PlayerCombatMode.WalkNorthwards,
                    CombatModeEndTick = 3,
                    CurrentAction = new PlayerActionSnapshot {
                        Kind = PlayerActionKind.Move,
                        StartedAtTick = 2,
                        EffectTick = 1,
                        ReadyTick = 2,
                        EndTick = 3,
                        DirectionX = 1,
                    },
                },
            },
        };
        var blockedMovement = new Snapshot {
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 10,
                    Height = 10,
                    BlockedCells = { 12 },
                },
            },
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    LevelId = 1,
                    PositionX = 1,
                    PositionY = 1,
                    CombatMode = PlayerCombatMode.WalkNorthwards,
                    CombatModeEndTick = 3,
                    CurrentAction = new PlayerActionSnapshot {
                        Kind = PlayerActionKind.Move,
                        StartedAtTick = 1,
                        EffectTick = 1,
                        ReadyTick = 2,
                        EndTick = 3,
                        DirectionX = 1,
                    },
                },
            },
        };
        var invalidCastSource = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    CombatMode = PlayerCombatMode.Spell,
                    CombatModeEndTick = 3,
                    CurrentAction = new PlayerActionSnapshot {
                        Kind = PlayerActionKind.Cast,
                        StartedAtTick = 1,
                        EffectTick = 1,
                        ReadyTick = 2,
                        EndTick = 3,
                        Cast = new CastRequested {
                            SpellId = 1,
                            Source = (CastSourceKind)99,
                        },
                    },
                },
            },
        };
        var invalidProgress = new Snapshot {
            Tick = 1,
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    CombatMode = PlayerCombatMode.WalkNorthwards,
                    CombatModeEndTick = 3,
                    CurrentAction = new PlayerActionSnapshot {
                        Kind = PlayerActionKind.Move,
                        StartedAtTick = 1,
                        EffectTick = 2,
                        ReadyTick = 2,
                        EndTick = 3,
                        EffectApplied = true,
                        DirectionX = 1,
                    },
                },
            },
        };
        var invalidCastSourceReference = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    CombatMode = PlayerCombatMode.Spell,
                    CombatModeEndTick = 3,
                    CurrentAction = new PlayerActionSnapshot {
                        Kind = PlayerActionKind.Cast,
                        StartedAtTick = 1,
                        EffectTick = 1,
                        ReadyTick = 2,
                        EndTick = 3,
                        Cast = new CastRequested {
                            SpellId = 1,
                            Source = CastSourceKind.Ability,
                            SourceItem = new PlayerItemReference {
                                Location = PlayerItemLocation.Inventory,
                                Slot = 40,
                            },
                        },
                    },
                },
            },
        };
        var invalidEntityAttackCoordinates = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    CombatMode = PlayerCombatMode.Attack,
                    CombatModeEndTick = 3,
                    CurrentAction = new PlayerActionSnapshot {
                        Kind = PlayerActionKind.Attack,
                        StartedAtTick = 1,
                        EffectTick = 1,
                        ReadyTick = 2,
                        EndTick = 3,
                        TargetEntityId = 2,
                        TargetX = 1,
                    },
                },
            },
        };

        foreach (var snapshot in new[] {
            invalidTiming,
            blockedMovement,
            invalidProgress,
            invalidCastSource,
            invalidCastSourceReference,
            invalidEntityAttackCoordinates,
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void AcceptsValidMonsterStateAndPackLeaderProjection()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    LevelId = 1,
                    Alive = true,
                    HitPoints = 1,
                    MaxHitPoints = 1,
                },
                new MonsterSnapshot {
                    EntityId = 2,
                    LevelId = 1,
                    LeaderRelation = 1,
                    LeaderEntityId = 1,
                },
            },
        });
    }

    [Fact]
    public void RejectsMalformedMonsterVitalityCombatAndPackState()
    {
        var impossibleHealth = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    HitPoints = 1,
                    MaxHitPoints = 1,
                    Alive = false,
                },
            },
        };
        var invalidUniqueIdentity = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    UniqueIdentity = "unique-without-flag",
                },
            },
        };
        var invalidResistance = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    FireResistance = 101,
                },
            },
        };
        var invalidPackLeader = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    LeaderRelation = 1,
                    LeaderEntityId = 99,
                },
            },
        };

        foreach (var snapshot in new[] {
            impossibleHealth,
            invalidUniqueIdentity,
            invalidResistance,
            invalidPackLeader,
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void RejectsMalformedMonsterOccupancyAndFallenState()
    {
        var invalidMovingDestination = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    Moving = true,
                    PositionX = 1,
                    PositionY = 1,
                    FuturePositionX = 3,
                    FuturePositionY = 1,
                },
            },
        };
        var invalidStationaryDestination = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    PositionX = 1,
                    PositionY = 1,
                    FuturePositionX = 2,
                    FuturePositionY = 1,
                },
            },
        };
        var invalidFallenAnimationFrame = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    FallenStandAnimationFrame = 128,
                },
            },
        };
        var invalidFallenAnimationTick = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    FallenStandAnimationTick = 128,
                },
            },
        };
        var invalidFallenRallyDuration = new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    FallenRallyTicks = 4097,
                },
            },
        };

        foreach (var snapshot in new[] {
            invalidMovingDestination,
            invalidStationaryDestination,
            invalidFallenAnimationFrame,
            invalidFallenAnimationTick,
            invalidFallenRallyDuration,
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void AcceptsValidObjectEffectProjection()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            Quests = {
                new QuestSnapshot {
                    QuestId = 7,
                    RequiredProgress = 1,
                },
            },
            Objects = {
                new ObjectSnapshot {
                    EntityId = 1,
                    ObjectId = 1,
                    PresentationKind = ObjectPresentationKind.Quest,
                    QuestId = 7,
                    EffectKind = 4,
                    QuestTransitionKind = 1,
                    QuestSequenceFollowUpKind = 1,
                    QuestSequenceFollowUpAmount = 1,
                },
                new ObjectSnapshot {
                    EntityId = 2,
                    ObjectId = 2,
                    EffectKind = 8,
                    ProjectileTargetOffsetX = 2,
                    ProjectileSourceKind = 1,
                    NativeObjectId = 108,
                    AnimationFrame = byte.MaxValue,
                    ActivatedAnimationFrame = byte.MaxValue,
                },
                new ObjectSnapshot {
                    EntityId = 3,
                    ObjectId = 3,
                    Breakable = true,
                    Destroyed = true,
                    PresentationKind = ObjectPresentationKind.Breakable,
                },
            },
        });
    }

    [Fact]
    public void RejectsMalformedObjectEffectMetadata()
    {
        var invalidObjects = new[] {
            new ObjectSnapshot { EntityId = 1 },
            new ObjectSnapshot { EntityId = 1, ObjectId = 1, EffectAmount = -1 },
            new ObjectSnapshot { EntityId = 1, ObjectId = 1, EffectKind = 10 },
            new ObjectSnapshot {
                EntityId = 1,
                ObjectId = 1,
                PresentationKind = (ObjectPresentationKind)99,
            },
            new ObjectSnapshot { EntityId = 1, ObjectId = 1, QuestId = 7 },
            new ObjectSnapshot {
                EntityId = 1,
                ObjectId = 1,
                QuestTransitionKind = 3,
                QuestTransitionAmount = 0,
            },
            new ObjectSnapshot {
                EntityId = 1,
                ObjectId = 1,
                RewardKind = 1,
                RewardId = 0,
            },
            new ObjectSnapshot {
                EntityId = 1,
                ObjectId = 1,
                ProjectileTargetOffsetX = 1,
            },
            new ObjectSnapshot {
                EntityId = 1,
                ObjectId = 1,
                QuestSequenceFollowUpKind = 1,
                QuestSequenceFollowUpAmount = 1,
            },
            new ObjectSnapshot { EntityId = 1, ObjectId = 1, NativeObjectId = 109 },
            new ObjectSnapshot { EntityId = 1, ObjectId = 1, AnimationFrame = 256 },
            new ObjectSnapshot { EntityId = 1, ObjectId = 1, ActivatedAnimationFrame = 256 },
            new ObjectSnapshot {
                EntityId = 1,
                ObjectId = 1,
                Breakable = true,
                MissilePassable = true,
            },
            new ObjectSnapshot {
                EntityId = 1,
                ObjectId = 1,
                Destroyed = true,
            },
        };

        foreach (var @object in invalidObjects)
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(new Snapshot {
                    Objects = { @object },
                }));
    }

    [Fact]
    public void AcceptsValidProjectileProjection()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            Projectiles = {
                new ProjectileSnapshot {
                    EntityId = 1,
                    SourceEntityId = 2,
                    SpellId = 1,
                    RemainingTicks = 3,
                    Behavior = 2,
                    Damage = 4,
                    PresentationKind = ProjectilePresentationKind.Fire,
                },
            },
        });
    }

    [Fact]
    public void RejectsMalformedProjectileStateAndNativeMissileIdentity()
    {
        var missingSource = new Snapshot {
            Projectiles = {
                new ProjectileSnapshot {
                    EntityId = 1,
                    SpellId = 1,
                    RemainingTicks = 1,
                },
            },
        };
        var invalidDamageRange = new Snapshot {
            Projectiles = {
                new ProjectileSnapshot {
                    EntityId = 1,
                    SourceEntityId = 2,
                    SpellId = 1,
                    RemainingTicks = 1,
                    DamageMinimum = 4,
                    DamageMaximum = 3,
                },
            },
        };
        var invalidPresentation = new Snapshot {
            Projectiles = {
                new ProjectileSnapshot {
                    EntityId = 1,
                    SourceEntityId = 2,
                    SpellId = 1,
                    RemainingTicks = 1,
                    PresentationKind = (ProjectilePresentationKind)99,
                },
            },
        };
        var invalidMissileIdentity = new Snapshot {
            Projectiles = {
                new ProjectileSnapshot {
                    EntityId = 1,
                    SourceEntityId = 2,
                    RemainingTicks = 1,
                    Behavior = 50,
                    NativeMissileId = "invalid identity",
                },
            },
        };
        var oversizedMissileIdentity = new Snapshot {
            Projectiles = {
                new ProjectileSnapshot {
                    EntityId = 1,
                    SourceEntityId = 2,
                    RemainingTicks = 1,
                    Behavior = 50,
                    NativeMissileId = new string('x', 129),
                },
            },
        };

        foreach (var snapshot in new[] {
            missingSource,
            invalidDamageRange,
            invalidPresentation,
            invalidMissileIdentity,
            oversizedMissileIdentity,
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void RejectsProjectileTargetOutsideProjectedLevelGeometry()
    {
        var valid = new Snapshot {
            LevelGeometry = {
                new LevelGeometrySnapshot { LevelId = 1, Width = 10, Height = 10 },
            },
            Projectiles = {
                new ProjectileSnapshot {
                    EntityId = 1,
                    SourceEntityId = 2,
                    SpellId = 1,
                    LevelId = 1,
                    PositionX = 2,
                    PositionY = 3,
                    TargetX = 9,
                    TargetY = 9,
                    RemainingTicks = 1,
                },
            },
        };

        AuthoritativeSnapshotValidator.Validate(valid);

        var invalid = valid.Clone();
        invalid.Projectiles[0].TargetX = 10;
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(invalid));
    }

    [Fact]
    public void AcceptsValidWorldItemIdentityAndNativeState()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            WorldItems = {
                new WorldItemSnapshot {
                    EntityId = 1,
                    ItemSeed = 17,
                    Price = int.MaxValue,
                    State = new ItemStateSnapshot {
                        ItemType = 1,
                        BaseName = "Sword",
                        IdentifiedName = "Sword",
                        NativeCursorGraphic = 1,
                    },
                },
            },
        });
    }

    [Fact]
    public void RejectsMalformedWorldItemPriceIdentityAndState()
    {
        var invalidPrice = new Snapshot {
            WorldItems = {
                new WorldItemSnapshot {
                    EntityId = 1,
                    ItemSeed = 1,
                    Price = uint.MaxValue,
                    State = new ItemStateSnapshot(),
                },
            },
        };
        var missingIdentity = new Snapshot {
            WorldItems = {
                new WorldItemSnapshot {
                    EntityId = 1,
                    State = new ItemStateSnapshot(),
                },
            },
        };
        var incompletePresentation = new Snapshot {
            WorldItems = {
                new WorldItemSnapshot {
                    EntityId = 1,
                    ItemSeed = 1,
                    State = new ItemStateSnapshot { BaseName = "Sword" },
                },
            },
        };
        var invalidState = new Snapshot {
            WorldItems = {
                new WorldItemSnapshot {
                    EntityId = 1,
                    ItemSeed = 1,
                    State = new ItemStateSnapshot { MaxDamage = -1 },
                },
            },
        };

        foreach (var snapshot in new[] {
            invalidPrice,
            missingIdentity,
            incompletePresentation,
            invalidState,
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void AcceptsValidPlayerInventoryStashHeldAndStoreItems()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    ActiveStoreId = 9,
                    Inventory = {
                        new ItemSnapshot {
                            ItemSeed = 11,
                            Price = 100,
                            State = new ItemStateSnapshot(),
                        },
                    },
                    Equipment = {
                        new EquippedItemSnapshot {
                            Slot = 0,
                            ItemSeed = 12,
                            State = new ItemStateSnapshot(),
                        },
                    },
                    Belt = {
                        new BeltItemSnapshot {
                            Slot = 0,
                            ItemSeed = 13,
                            State = new ItemStateSnapshot(),
                        },
                    },
                    Stash = {
                        new StashItemSnapshot {
                            Page = 0,
                            Cell = 0,
                            Item = new ItemSnapshot {
                                ItemSeed = 14,
                                Price = 200,
                                State = new ItemStateSnapshot(),
                            },
                        },
                    },
                    HeldItem = new ItemSnapshot {
                        ItemSeed = 15,
                        Price = 300,
                        State = new ItemStateSnapshot(),
                    },
                },
            },
            ActiveStore = new StoreSnapshot {
                StoreId = 9,
                Items = {
                    new StoreItemSnapshot {
                        StoreSlot = 0,
                        ItemSeed = 16,
                        Price = 400,
                        State = new ItemStateSnapshot(),
                    },
                },
            },
        };

        AuthoritativeSnapshotValidator.Validate(snapshot);
    }

    [Fact]
    public void RejectsMalformedPlayerItemTopologyAndStoreState()
    {
        var invalidPrice = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    Inventory = {
                        new ItemSnapshot {
                            ItemSeed = 1,
                            Price = uint.MaxValue,
                            State = new ItemStateSnapshot(),
                        },
                    },
                },
            },
        };
        var duplicateSeed = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    Inventory = {
                        new ItemSnapshot { ItemSeed = 1, State = new ItemStateSnapshot() },
                    },
                    Equipment = {
                        new EquippedItemSnapshot {
                            Slot = 0,
                            ItemSeed = 1,
                            State = new ItemStateSnapshot(),
                        },
                    },
                },
            },
        };
        var invalidFootprint = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    Inventory = {
                        new ItemSnapshot {
                            ItemSeed = 1,
                            State = new ItemStateSnapshot { InventoryWidth = 11 },
                        },
                    },
                },
            },
        };
        var invalidStore = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 1, ActiveStoreId = 9 },
            },
            ActiveStore = new StoreSnapshot {
                StoreId = 9,
                Items = {
                    new StoreItemSnapshot {
                        StoreSlot = 0,
                        ItemSeed = 1,
                        State = new ItemStateSnapshot(),
                    },
                    new StoreItemSnapshot {
                        StoreSlot = 0,
                        ItemSeed = 2,
                        State = new ItemStateSnapshot(),
                    },
                },
            },
        };

        foreach (var snapshot in new[] {
            invalidPrice,
            duplicateSeed,
            invalidFootprint,
            invalidStore,
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void AcceptsValidPlayerResourcesAttributesSpellsStatusAndStash()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    Life = 8,
                    LifeMaximum = 10,
                    Mana = 3,
                    ManaMaximum = 5,
                    BaseLifeMaximum = 10,
                    BaseManaMaximum = 5,
                    Gold = 100,
                    StashGold = 200,
                    StatPoints = 4,
                    ReflectCharges = 2,
                    LightRadius = 2,
                    CharacterLevel = 50,
                    DiabloKillLevel = byte.MaxValue,
                    Attributes = new PlayerAttributesSnapshot {
                        Strength = new AttributeSnapshot { Base = 10, Current = 12 },
                        Magic = new AttributeSnapshot { Base = 8, Current = 9 },
                        Dexterity = new AttributeSnapshot { Base = 7, Current = 8 },
                        Vitality = new AttributeSnapshot { Base = 6, Current = 7 },
                    },
                    AbilitySpellId = 1,
                    MemorizedSpells = {
                        new PlayerSpellLevelSnapshot {
                            SpellId = 1,
                            Level = 2,
                            TargetingKind = SpellTargetingKind.Player,
                        },
                    },
                    StatusEffects = {
                        new StatusEffectSnapshot {
                            EffectId = 1,
                            RemainingTicks = 3,
                            Behavior = StatusEffectBehavior.Infravision,
                        },
                    },
                    Stash = {
                        new StashItemSnapshot {
                            Page = 0,
                            Cell = 0,
                            Item = new ItemSnapshot {
                                ItemSeed = 1,
                                State = new ItemStateSnapshot(),
                            },
                        },
                    },
                },
            },
        });
    }

    [Fact]
    public void RejectsMalformedPlayerCoreState()
    {
        var invalidResources = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    Life = 11,
                    LifeMaximum = 10,
                    LightRadius = 1,
                },
            },
        };
        var incompleteAttributes = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    Attributes = new PlayerAttributesSnapshot {
                        Strength = new AttributeSnapshot { Base = 1, Current = 1 },
                    },
                },
            },
        };
        var invalidSpells = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    MemorizedSpells = {
                        new PlayerSpellLevelSnapshot { SpellId = 1, Level = 16 },
                        new PlayerSpellLevelSnapshot { SpellId = 1, Level = 1 },
                    },
                },
            },
        };
        var invalidStatus = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    StatusEffects = {
                        new StatusEffectSnapshot {
                            EffectId = 1,
                            RemainingTicks = 1,
                            Behavior = StatusEffectBehavior.Search,
                            Magnitude = 1,
                        },
                    },
                },
            },
        };
        var invalidStatusBehavior = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    StatusEffects = {
                        new StatusEffectSnapshot {
                            EffectId = 1,
                            RemainingTicks = 1,
                            Behavior = (StatusEffectBehavior)4,
                        },
                    },
                },
            },
        };
        var oversizedStatus = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                },
            },
        };
        for (var effectId = 1; effectId <= AuthoritativeProtocolLimits.MaximumStatusEffectCount + 1; effectId++)
            oversizedStatus.Players[0].StatusEffects.Add(new StatusEffectSnapshot {
                EffectId = (uint)effectId,
                RemainingTicks = 1,
            });
        var overlappingStash = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    Stash = {
                        new StashItemSnapshot {
                            Page = 0,
                            Cell = 0,
                            Item = new ItemSnapshot {
                                ItemSeed = 1,
                                State = new ItemStateSnapshot { InventoryWidth = 2 },
                            },
                        },
                        new StashItemSnapshot {
                            Page = 0,
                            Cell = 1,
                            Item = new ItemSnapshot {
                                ItemSeed = 2,
                                State = new ItemStateSnapshot(),
                            },
                        },
                    },
                },
            },
        };

        foreach (var snapshot in new[] {
            invalidResources,
            incompleteAttributes,
            invalidSpells,
            invalidStatus,
            invalidStatusBehavior,
            oversizedStatus,
            overlappingStash,
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void AcceptsDefinedDifficultyAndValidPortalState()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            Difficulty = GameDifficulty.Normal,
            Portals = {
                new PortalSnapshot {
                    PortalId = 1,
                    Bidirectional = true,
                    OwnerEntityId = 2,
                    RemainingTicks = 3,
                },
                new PortalSnapshot { PortalId = 4 },
            },
            ActiveWorldMutationIds = { 5, 6 },
        });
    }

    [Fact]
    public void RejectsSnapshotsBeyondNativePortalCapacity()
    {
        var snapshot = new Snapshot();
        for (var index = 1; index <= 5; index++)
            snapshot.Portals.Add(new PortalSnapshot { PortalId = (uint)index });

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void RejectsSnapshotsBeyondNativeMonsterCapacityPerLevel()
    {
        var snapshot = new Snapshot();
        for (var index = 1; index <= 201; index++)
            snapshot.Monsters.Add(new MonsterSnapshot {
                EntityId = (uint)index,
                LevelId = 1,
            });

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void RejectsSnapshotsBeyondNativeObjectAndItemCapacityPerLevel()
    {
        var objectSnapshot = new Snapshot();
        for (var index = 1; index <= 128; index++)
            objectSnapshot.Objects.Add(new ObjectSnapshot {
                EntityId = (uint)index,
                ObjectId = (uint)index,
                LevelId = 1,
            });

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(objectSnapshot));

        var itemSnapshot = new Snapshot();
        for (var index = 1; index <= 128; index++)
            itemSnapshot.WorldItems.Add(new WorldItemSnapshot {
                EntityId = (uint)index,
                LevelId = 1,
                ItemSeed = (uint)index,
                State = new ItemStateSnapshot(),
            });

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(itemSnapshot));
    }

    [Fact]
    public void AcceptsOmittedAndPrintablePlayerNames()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 1 },
                new PlayerSnapshot { EntityId = 2, CharacterName = "Godevilution" },
            },
        });
    }

    [Fact]
    public void RejectsMalformedNonEmptyStateHashes()
    {
        foreach (var hash in new[] {
            "not-a-hash",
            new string('a', AuthoritativeSnapshotValidator.Sha256HexLength - 1),
            new string('a', AuthoritativeSnapshotValidator.Sha256HexLength + 1),
            new string('a', AuthoritativeSnapshotValidator.Sha256HexLength - 1) + "g",
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(new Snapshot { StateSha256 = hash }));
    }

    [Fact]
    public void RejectsZeroOrDuplicateEntityIds()
    {
        var zero = new Snapshot {
            Players = { new PlayerSnapshot() },
        };
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(zero));

        var duplicate = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Monsters = { new MonsterSnapshot { EntityId = 7 } },
        };
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(duplicate));
    }

    [Fact]
    public void RejectsInvalidSnapshotMetadata()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(new Snapshot {
                Difficulty = (GameDifficulty)99,
            }));

        var tooManyMutations = new Snapshot();
        tooManyMutations.ActiveWorldMutationIds.AddRange(
            Enumerable.Range(1, 4097).Select(value => (uint)value));

        foreach (var snapshot in new[] {
            new Snapshot { Portals = { new PortalSnapshot() } },
            new Snapshot {
                Portals = {
                    new PortalSnapshot { PortalId = 1 },
                    new PortalSnapshot { PortalId = 1 },
                },
            },
            new Snapshot {
                Portals = {
                    new PortalSnapshot { PortalId = 1, Bidirectional = true },
                },
            },
            new Snapshot {
                Portals = {
                    new PortalSnapshot { PortalId = 1, OwnerEntityId = 2 },
                },
            },
            new Snapshot { ActiveWorldMutationIds = { 0 } },
            new Snapshot { ActiveWorldMutationIds = { 7, 7 } },
            tooManyMutations,
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void RejectsInvalidPlayerNameShapes()
    {
        foreach (var name in new[] {
            new string('A', 32),
            "bad\nname",
            "bad\0name",
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(new Snapshot {
                    Players = { new PlayerSnapshot { EntityId = 1, CharacterName = name } },
                }));
    }

    [Fact]
    public void RejectsDiabloKillLevelsOutsideNativeRange()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(new Snapshot {
                Players = {
                    new PlayerSnapshot {
                        EntityId = 1,
                        DiabloKillLevel = byte.MaxValue + 1U,
                    },
                },
            }));
    }

    [Fact]
    public void RejectsPlayerLevelsOutsideNativeRange()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(new Snapshot {
                Players = {
                    new PlayerSnapshot {
                        EntityId = 1,
                        LevelId = 25,
                    },
                },
            }));
    }

    [Fact]
    public void RejectsDuplicateVisitedLevels()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(new Snapshot {
                Players = {
                    new PlayerSnapshot {
                        EntityId = 1,
                        LevelId = 4,
                        VisitedLevelIds = { 0, 4, 4 },
                    },
                },
            }));
    }

    [Fact]
    public void RejectsUndefinedPlayerEnums()
    {
        foreach (var player in new[] {
            new PlayerSnapshot { EntityId = 1, FacingDirection = (PlayerDirection)99 },
            new PlayerSnapshot { EntityId = 1, CombatMode = (PlayerCombatMode)99 },
            new PlayerSnapshot { EntityId = 1, AbilityKind = (PlayerAbilityKind)99 },
            new PlayerSnapshot { EntityId = 1, AbilityTargetingKind = (SpellTargetingKind)99 },
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(new Snapshot { Players = { player } }));
    }

    [Fact]
    public void AcceptsPlayerCombatModesWithCompatibleLifetimes()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    CombatMode = PlayerCombatMode.Stand,
                },
                new PlayerSnapshot {
                    EntityId = 2,
                    CombatMode = PlayerCombatMode.Block,
                    CombatModeEndTick = 4,
                },
                new PlayerSnapshot {
                    EntityId = 3,
                    CombatMode = PlayerCombatMode.GotHit,
                    CombatModeEndTick = 5,
                },
            },
        });
    }

    [Fact]
    public void RejectsContradictoryPlayerCombatModeLifetimes()
    {
        foreach (var player in new[] {
            new PlayerSnapshot {
                EntityId = 1,
                CombatMode = PlayerCombatMode.Stand,
                CombatModeEndTick = 1,
            },
            new PlayerSnapshot {
                EntityId = 1,
                CombatMode = PlayerCombatMode.Block,
            },
            new PlayerSnapshot {
                EntityId = 1,
                CombatMode = PlayerCombatMode.GotHit,
            },
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(new Snapshot { Players = { player } }));
    }

    [Fact]
    public void AcceptsMovementTimingAndOccupancyForAnActiveMovingMonster()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    Moving = true,
                    PositionX = 1,
                    PositionY = 1,
                    FuturePositionX = 2,
                    FuturePositionY = 1,
                    MovementStartedTick = 12,
                    MovementEffectTick = 13,
                },
            },
        });
    }

    [Fact]
    public void RejectsNativeMonsterMovementModesAndTurnsOutsideSharedBounds()
    {
        var valid = new Snapshot {
            Monsters = { new MonsterSnapshot { EntityId = 1 } },
        };
        foreach (var mutate in new Action<MonsterSnapshot>[] {
            monster => monster.BatMovementMode = AuthoritativeProtocolLimits.MaximumNativeMonsterMovementMode + 1,
            monster => monster.RangedAvoidanceTurn = AuthoritativeProtocolLimits.MaximumNativeMonsterMovementTurn + 1,
            monster => monster.RangedAvoidanceMovementMode = AuthoritativeProtocolLimits.MinimumNativeMonsterMovementMode - 1,
            monster => monster.RhinoMovementTurn = AuthoritativeProtocolLimits.MinimumNativeMonsterMovementTurn - 1,
            monster => monster.AvoidanceMovementTurn = AuthoritativeProtocolLimits.MaximumNativeMonsterMovementTurn + 1,
            monster => monster.AvoidanceMovementMode = AuthoritativeProtocolLimits.MaximumNativeMonsterMovementMode + 1,
            monster => monster.SkeletonRangedMovementMode = AuthoritativeProtocolLimits.MinimumNativeMonsterMovementMode - 1,
            monster => monster.FatMovementMode = AuthoritativeProtocolLimits.MaximumNativeMonsterMovementMode + 1,
            monster => monster.RhinoMovementMode = AuthoritativeProtocolLimits.MinimumNativeMonsterMovementMode - 1,
        }) {
            var invalid = valid.Clone();
            mutate(invalid.Monsters[0]);
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(invalid));
        }
    }

    [Fact]
    public void RejectsNativeMonsterAiTimersAndCountersOutsideSharedBounds()
    {
        var valid = new Snapshot {
            Monsters = { new MonsterSnapshot { EntityId = 1 } },
        };
        foreach (var mutate in new Action<MonsterSnapshot>[] {
            monster => monster.FallenFearTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterAiTimerTicks + 1,
            monster => monster.SneakRetreatTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterShortTimerTicks + 1,
            monster => monster.ScavengerHealingTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterShortTimerTicks + 1,
            monster => monster.BatRetreatTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterRetreatTicks + 1,
            monster => monster.BatMovementIdleTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterAiTimerTicks + 1,
            monster => monster.RangedAvoidanceMoveTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterMovementTicks + 1,
            monster => monster.RangedAvoidanceMovementIdleTicks = -2,
            monster => monster.RhinoMovementTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterMovementTicks + 1,
            monster => monster.AvoidanceMovementTicks = -2,
            monster => monster.AvoidanceMovementIdleTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterAiTimerTicks + 1,
            monster => monster.SkeletonMeleeDelayTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterAiTimerTicks + 1,
            monster => monster.SkeletonRangedMovementIdleTicks = -2,
            monster => monster.FatMovementIdleTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterAiTimerTicks + 1,
            monster => monster.RhinoMovementIdleTicks = -2,
            monster => monster.RhinoDelayTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterAiTimerTicks + 1,
            monster => monster.SkeletonKingDelayTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterAiTimerTicks + 1,
            monster => monster.HorkDemonDelayTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterAiTimerTicks + 1,
            monster => monster.RangedAvoidanceDelayTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterAiTimerTicks + 1,
            monster => monster.SnakeDelayTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterAiTimerTicks + 1,
            monster => monster.CounselorDelayTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterAiTimerTicks + 1,
            monster => monster.AvoidanceDelayTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterAiTimerTicks + 1,
            monster => monster.RangedDelayTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterAiTimerTicks + 1,
            monster => monster.MegaDelayTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterAiTimerTicks + 1,
            monster => monster.CounselorRetreatTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterRetreatTicks + 1,
        }) {
            var invalid = valid.Clone();
            mutate(invalid.Monsters[0]);
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(invalid));
        }
    }

    [Fact]
    public void RejectsNativeMonsterAiIdentityDirectionsAndScavengerReferencesOutsideSharedBounds()
    {
        var valid = new Snapshot {
            Monsters = { new MonsterSnapshot { EntityId = 1, LevelId = 1 } },
        };
        foreach (var mutate in new Action<MonsterSnapshot>[] {
            monster => monster.NativeAi = new string('x', AuthoritativeProtocolLimits.MaximumIdentityBytes + 1),
            monster => monster.NativeAi = "bad\0ai",
            monster => monster.FallenFearDirection = AuthoritativeProtocolLimits.MaximumNativeMonsterDirection + 1,
            monster => monster.BatRetreatDirection = -1,
            monster => monster.ScavengerCorpseEntityId = 2,
        }) {
            var invalid = valid.Clone();
            mutate(invalid.Monsters[0]);
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(invalid));
        }

        var wrongLevelReference = valid.Clone();
        wrongLevelReference.Corpses.Add(new CorpseSnapshot { EntityId = 2, LevelId = 2 });
        wrongLevelReference.Monsters[0].ScavengerCorpseEntityId = 2;
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(wrongLevelReference));
    }

    [Fact]
    public void RejectsOneSidedNativeMonsterRuntimeSeeds()
    {
        var valid = new Snapshot {
            Monsters = { new MonsterSnapshot { EntityId = 1 } },
        };
        foreach (var mutate in new Action<MonsterSnapshot>[] {
            monster => monster.MonsterItemSeed = 1,
            monster => monster.MonsterAiSeed = 1,
        }) {
            var invalid = valid.Clone();
            mutate(invalid.Monsters[0]);
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(invalid));
        }
    }

    [Fact]
    public void RejectsPlayerMinionOwnersMissingFromTheSnapshotOrOnAnotherLevel()
    {
        var valid = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    LevelId = 1,
                    IsPlayerMinion = true,
                    OwnerPlayerId = 7,
                },
            },
        };
        AuthoritativeSnapshotValidator.Validate(valid);

        var missingOwner = valid.Clone();
        missingOwner.Monsters[0].OwnerPlayerId = 8;
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(missingOwner));

        var wrongLevel = valid.Clone();
        wrongLevel.Players[0].LevelId = 2;
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(wrongLevel));
    }

    [Fact]
    public void AcceptsMaterializedSetLevelPlayerIdentityWithoutRegularVisitedFlag()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    LevelId = AuthoritativeLevelIdRules.RuntimeSetLevelIdBase + 1,
                },
            },
        });
    }

    [Fact]
    public void RejectsMaterializedSetLevelIdentityInRegularVisitedFlags()
    {
        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeSnapshotValidator.Validate(new Snapshot {
                Players = {
                    new PlayerSnapshot {
                        EntityId = 1,
                        LevelId = 4,
                        VisitedLevelIds = {
                            AuthoritativeLevelIdRules.RuntimeSetLevelIdBase,
                        },
                    },
                },
            }));
    }

    [Fact]
    public void RejectsInvalidMonsterMovementTiming()
    {
        foreach (var monster in new[] {
            new MonsterSnapshot {
                EntityId = 1,
                Moving = true,
                MovementStartedTick = 12,
                MovementEffectTick = 11,
            },
            new MonsterSnapshot {
                EntityId = 1,
                MovementEffectTick = 13,
            },
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(new Snapshot {
                    Monsters = { monster },
                }));
    }
}

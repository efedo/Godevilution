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
            Objects = { new ObjectSnapshot { EntityId = 4 } },
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
                        DirectionX = 1,
                        DirectionY = 0,
                    },
                },
            },
        });
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

        foreach (var snapshot in new[] {
            invalidTiming,
            blockedMovement,
            invalidCastSource,
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

        foreach (var snapshot in new[] {
            missingSource,
            invalidDamageRange,
            invalidPresentation,
            invalidMissileIdentity,
        })
            Assert.Throws<InvalidDataException>(() =>
                AuthoritativeSnapshotValidator.Validate(snapshot));
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
    public void AcceptsMovementTimingOnlyForAnActiveMovingMonster()
    {
        AuthoritativeSnapshotValidator.Validate(new Snapshot {
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 1,
                    Moving = true,
                    MovementStartedTick = 12,
                    MovementEffectTick = 13,
                },
            },
        });
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

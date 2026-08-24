using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;
using ProtocolItemPresentationKind = Devilution.Protocol.V1.ItemPresentationKind;

namespace Devilution.Client.Protocol.Tests;

public sealed class ClientModelTests
{
    [Fact]
    public void ExposesOnlyActiveLevelWarpTriggersToPresentation()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
                LevelGeometry = {
                    new LevelGeometrySnapshot {
                        LevelId = 0,
                        Width = 10,
                        Height = 10,
                        WarpTriggers = {
                            new WarpTriggerSnapshot { WarpId = 1, TriggerX = 2, TriggerY = 2, Available = true },
                        },
                    },
                    new LevelGeometrySnapshot {
                        LevelId = 1,
                        Width = 10,
                        Height = 10,
                        WarpTriggers = {
                            new WarpTriggerSnapshot { WarpId = 2, TriggerX = 3, TriggerY = 4, Available = true },
                        },
                    },
                },
            }));

        var trigger = Assert.Single(model.VisibleWarpTriggers);
        Assert.Equal(2U, trigger.WarpId);
        Assert.Equal((3, 4), (trigger.TriggerX, trigger.TriggerY));
        Assert.Same(trigger, model.WarpTriggerAt(3, 4));
        Assert.Null(model.WarpTriggerAt(2, 2));
    }

    [Fact]
    public void RejectsMalformedWarpTriggerSnapshots()
    {
        var model = new AuthoritativeClientModel();
        Assert.Throws<InvalidDataException>(() => model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
                LevelGeometry = {
                    new LevelGeometrySnapshot {
                        LevelId = 1,
                        Width = 10,
                        Height = 10,
                        WarpTriggers = {
                            new WarpTriggerSnapshot { WarpId = 0, TriggerX = 3, TriggerY = 4 },
                        },
                    },
                },
        })));

        Assert.Throws<InvalidDataException>(() => model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
                LevelGeometry = {
                    new LevelGeometrySnapshot {
                        LevelId = 1,
                        Width = 10,
                        Height = 10,
                        WarpTriggers = {
                            new WarpTriggerSnapshot {
                                WarpId = 1,
                                TriggerX = 3,
                                TriggerY = 4,
                                TargetLevelId = 1,
                                TargetX = 1,
                                TargetY = 1,
                                RequiredQuestId = 9,
                                Available = true,
                            },
                        },
                    },
                },
        })));
    }

    [Fact]
    public void RejectsWarpTriggersWithUnknownOrOutOfBoundsDestinations()
    {
        var model = new AuthoritativeClientModel();
        Assert.Throws<InvalidDataException>(() => model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
                LevelGeometry = {
                    new LevelGeometrySnapshot {
                        LevelId = 1,
                        Width = 10,
                        Height = 10,
                        WarpTriggers = {
                            new WarpTriggerSnapshot {
                                WarpId = 1,
                                TriggerX = 3,
                                TriggerY = 4,
                                TargetLevelId = 2,
                                TargetX = 1,
                                TargetY = 1,
                                Available = true,
                            },
                        },
                    },
                },
            })));

        Assert.Throws<InvalidDataException>(() => model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
                LevelGeometry = {
                    new LevelGeometrySnapshot {
                        LevelId = 1,
                        Width = 10,
                        Height = 10,
                        WarpTriggers = {
                            new WarpTriggerSnapshot {
                                WarpId = 1,
                                TriggerX = 3,
                                TriggerY = 4,
                                TargetLevelId = 1,
                                TargetX = 10,
                                TargetY = 1,
                                Available = true,
                            },
                        },
                    },
                },
            })));
    }

    [Fact]
    public void RejectsOutOfRangeLevelGeometryCellsAndLayers()
    {
        var model = new AuthoritativeClientModel();
        var outOfRangeCell = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 4,
                    Height = 4,
                    BlockedCells = { 16 },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: outOfRangeCell)));

        var invalidTransparency = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 4,
                    Height = 4,
                    NativeTransparency = { 0, 1 },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidTransparency)));

        var invalidNativePresentation = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 1,
                    Height = 1,
                    NativeTransparency = { 128 },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidNativePresentation)));

        invalidNativePresentation.LevelGeometry[0].NativeTransparency.Clear();
        invalidNativePresentation.LevelGeometry[0].NativeMegaTileLayer.Add(256);
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidNativePresentation)));

        var oversizedFineGeometry = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 113,
                    Height = 1,
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: oversizedFineGeometry)));

        var oversizedCoarseGeometry = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 82,
                    Height = 82,
                    NativeMegaTileLayer = { Enumerable.Repeat<uint>(0, 41 * 41) },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: oversizedCoarseGeometry)));
    }

    [Fact]
    public void AcceptsNativeObjectLayerProjection()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
                LevelGeometry = {
                    new LevelGeometrySnapshot {
                        LevelId = 1,
                        Width = 2,
                        Height = 2,
                        NativeObjectLayer = { 0, 97, 100, 99 },
                        NativeMegaTileLayer = { 115 },
                        NativePieceLayer = { 0, 297, 300, 299 },
                    },
                },
            }));

        Assert.Equal(
            [0U, 97U, 100U, 99U],
            Assert.Single(model.Snapshot!.LevelGeometry).NativeObjectLayer);
        Assert.Equal(
            [115U],
            Assert.Single(model.Snapshot.LevelGeometry).NativeMegaTileLayer);
        Assert.Equal(
            [0U, 297U, 300U, 299U],
            Assert.Single(model.Snapshot.LevelGeometry).NativePieceLayer);
    }

    [Fact]
    public void RejectsNativeObjectLayerValuesOutsideCompatibilityRange()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 1,
                    Height = 1,
                    NativeObjectLayer = { 109 },
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public void RejectsNativeObjectSnapshotValuesOutsideCompatibilityRange()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Objects = {
                new ObjectSnapshot {
                    EntityId = 10,
                    ObjectId = 4,
                    NativeObjectId = 109,
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public void RejectsEntitiesOutsideProjectedLevelGeometry()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 4,
                    Height = 4,
                },
            },
            Objects = {
                new ObjectSnapshot {
                    EntityId = 10,
                    LevelId = 1,
                    PositionX = 4,
                    PositionY = 0,
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public void RejectsEntityCoordinatesOutsideNativeTileRangeWithoutGeometry()
    {
        var model = new AuthoritativeClientModel();
        var valid = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Monsters = { new MonsterSnapshot { EntityId = 8 } },
            WorldItems = {
                new WorldItemSnapshot {
                    EntityId = 9,
                    ItemSeed = 42,
                    State = new ItemStateSnapshot(),
                },
            },
            Objects = {
                new ObjectSnapshot { EntityId = 10, ObjectId = 4 },
            },
            Projectiles = {
                new ProjectileSnapshot {
                    EntityId = 11,
                    SourceEntityId = 7,
                    SpellId = 1,
                    RemainingTicks = 1,
                },
            },
            Corpses = { new CorpseSnapshot { EntityId = 12 } },
            Portals = { new PortalSnapshot { PortalId = 13 } },
        };

        foreach (var mutate in new Action<Snapshot>[] {
            snapshot => snapshot.Players[0].PositionX = 256,
            snapshot => snapshot.Monsters[0].PositionY = 256,
            snapshot => snapshot.WorldItems[0].PositionX = 256,
            snapshot => snapshot.Objects[0].PositionY = 256,
            snapshot => snapshot.Projectiles[0].PositionX = 256,
            snapshot => snapshot.Corpses[0].PositionY = 256,
            snapshot => snapshot.Portals[0].SourcePositionX = 256,
        }) {
            var invalid = valid.Clone();
            mutate(invalid);
            Assert.Throws<InvalidDataException>(() =>
                model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
        }
    }

    [Fact]
    public void RejectsPortalEndpointsOutsideProjectedLevelGeometry()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 4,
                    Height = 4,
                },
            },
            Portals = {
                new PortalSnapshot {
                    PortalId = 12,
                    SourceLevelId = 1,
                    SourcePositionX = 0,
                    SourcePositionY = 0,
                    DestinationLevelId = 1,
                    DestinationPositionX = 4,
                    DestinationPositionY = 0,
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public void SelectsAvailableContentOwnedQuestTownerStageByStateAndAdjacency()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    LevelId = 0,
                    PositionX = 79,
                    PositionY = 20,
                    Inventory = {
                        new ItemSnapshot {
                            ItemSeed = 41,
                            State = new ItemStateSnapshot { StableItemId = 2141 },
                        },
                    },
                },
            },
            Quests = {
                new QuestSnapshot {
                    QuestId = 3002,
                    Initial = true,
                },
            },
            QuestTownerInteractions = {
                new QuestTownerInteractionSnapshot {
                    InteractionId = 300201,
                    QuestId = 3002,
                    TownerId = 6,
                    LevelId = 0,
                    PositionX = 80,
                    PositionY = 20,
                    ItemId = 2141,
                    Kind = 0,
                    MessageId = 129,
                    ConsumeItem = true,
                },
            },
        };

        Assert.Equal(
            300201U,
            AuthoritativeQuestTownerInteractionSelector.Select(snapshot)!.InteractionId);
        Assert.Equal(
            300201U,
            AuthoritativeQuestTownerInteractionSelector.SelectAt(snapshot, 80, 20)!.InteractionId);
        Assert.Null(AuthoritativeQuestTownerInteractionSelector.SelectAt(snapshot, 79, 20));

        snapshot.Players[0].PositionX = 50;
        Assert.Null(AuthoritativeQuestTownerInteractionSelector.Select(snapshot));
    }

    [Fact]
    public void RejectsMalformedQuestTownerInteractionSnapshots()
    {
        var model = new AuthoritativeClientModel();
        Assert.Throws<InvalidDataException>(() => model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = { new PlayerSnapshot { EntityId = 7 } },
                QuestTownerInteractions = {
                    new QuestTownerInteractionSnapshot {
                        InteractionId = 0,
                        QuestId = 3002,
                        TownerId = 6,
                        MessageId = 129,
                    },
                },
            })));
    }

    [Fact]
    public void RejectsQuestTownerInteractionSnapshotsWithUnknownQuest()
    {
        var model = new AuthoritativeClientModel();
        Assert.Throws<InvalidDataException>(() => model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = { new PlayerSnapshot { EntityId = 7 } },
                QuestTownerInteractions = {
                    new QuestTownerInteractionSnapshot {
                        InteractionId = 300201,
                        QuestId = 3002,
                        TownerId = 6,
                        LevelId = 0,
                        PositionX = 80,
                        PositionY = 20,
                        Kind = 0,
                        MessageId = 129,
                    },
                },
            })));
    }

    [Fact]
    public void RejectsQuestTownerInteractionSnapshotsOutsideProjectedLevelGeometry()
    {
        var model = new AuthoritativeClientModel();
        Assert.Throws<InvalidDataException>(() => model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = { new PlayerSnapshot { EntityId = 7 } },
                Quests = { new QuestSnapshot { QuestId = 3002 } },
                LevelGeometry = {
                    new LevelGeometrySnapshot {
                        LevelId = 0,
                        Width = 10,
                        Height = 10,
                    },
                },
                QuestTownerInteractions = {
                    new QuestTownerInteractionSnapshot {
                        InteractionId = 300201,
                        QuestId = 3002,
                        TownerId = 6,
                        LevelId = 0,
                        PositionX = 10,
                        PositionY = 9,
                        Kind = 0,
                        MessageId = 129,
                    },
                },
            })));
    }

    [Fact]
    public void SelectsItemlessAuthoredDialogueAfterQuestCompletion()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    LevelId = 0,
                    PositionX = 55,
                    PositionY = 62,
                },
            },
            Quests = {
                new QuestSnapshot {
                    QuestId = 3013,
                    Completed = true,
                    Variable2 = 1,
                },
            },
            QuestTownerInteractions = {
                new QuestTownerInteractionSnapshot {
                    InteractionId = 300205,
                    QuestId = 3013,
                    TownerId = 3,
                    LevelId = 0,
                    PositionX = 55,
                    PositionY = 62,
                    Kind = 3,
                    RequiredVariable2 = 1,
                    Variable1 = 2,
                    Variable2 = 2,
                    MessageId = 3,
                    AllowCompleted = true,
                },
            },
        };

        Assert.Equal(
            300205U,
            AuthoritativeQuestTownerInteractionSelector.Select(snapshot)!.InteractionId);
    }

    [Fact]
    public void SelectsAuthoredQuestTownerActivationFromVisitedLevelOrGate()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    LevelId = 0,
                    PositionX = 55,
                    PositionY = 62,
                    VisitedLevelIds = { 0, 4 },
                },
            },
            Quests = {
                new QuestSnapshot {
                    QuestId = 3013,
                    Initial = true,
                },
            },
            QuestTownerInteractions = {
                new QuestTownerInteractionSnapshot {
                    InteractionId = 300206,
                    QuestId = 3013,
                    TownerId = 3,
                    LevelId = 0,
                    PositionX = 55,
                    PositionY = 62,
                    Kind = 0,
                    Variable1 = 1,
                    Variable2 = 1,
                    MessageId = 2,
                    RequiredVisitedLevel1 = 2,
                    RequiredVisitedLevel2 = 4,
                },
            },
        };

        Assert.Equal(
            300206U,
            AuthoritativeQuestTownerInteractionSelector.Select(snapshot)!.InteractionId);

        snapshot.Players[0].VisitedLevelIds.Clear();
        snapshot.Players[0].VisitedLevelIds.Add(0);
        Assert.Null(AuthoritativeQuestTownerInteractionSelector.Select(snapshot));
    }

    [Fact]
    public void RejectsVisitedLevelsThatOmitTheCurrentLevel()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    LevelId = 4,
                    VisitedLevelIds = { 0 },
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));

        snapshot.Players[0].VisitedLevelIds.Add(4);
        model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot));
        Assert.Contains(4U, model.Snapshot!.Players[0].VisitedLevelIds);
    }

    [Fact]
    public void RejectsMalformedPlayerAttributeProjection()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    Attributes = new PlayerAttributesSnapshot {
                        Strength = new AttributeSnapshot { Base = 15, Current = 15 },
                        Magic = new AttributeSnapshot { Base = 20, Current = 20 },
                        Dexterity = new AttributeSnapshot { Base = 10, Current = 10 },
                        Vitality = new AttributeSnapshot { Base = 25, Current = 25 },
                    },
                },
            },
        };

        model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot));

        var missing = snapshot.Clone();
        missing.Players[0].Attributes!.Vitality = null;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: missing)));

        var negative = snapshot.Clone();
        negative.Players[0].Attributes!.Strength.Current = -1;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: negative)));
    }

    [Fact]
    public void RejectsUndefinedPlayerAbilityTargetingKind()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    AbilityTargetingKind = SpellTargetingKind.Tile,
                },
            },
        };

        model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot));

        snapshot.Players[0].AbilityTargetingKind = (SpellTargetingKind)99;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public void SelectsUnloggedQuestTownerStageOnlyBeforeQuestIsLogged()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 1,
                    LevelId = 0,
                    PositionX = 55,
                    PositionY = 79,
                },
            },
            Quests = {
                new QuestSnapshot {
                    QuestId = 3014,
                    Initial = false,
                    Logged = false,
                },
            },
            QuestTownerInteractions = {
                new QuestTownerInteractionSnapshot {
                    InteractionId = 300210,
                    QuestId = 3014,
                    TownerId = 1,
                    LevelId = 0,
                    PositionX = 55,
                    PositionY = 79,
                    Kind = 3,
                    MessageId = 39,
                    RequireUnlogged = true,
                },
            },
        };

        Assert.Equal(
            300210U,
            AuthoritativeQuestTownerInteractionSelector.Select(snapshot)!.InteractionId);

        snapshot.Quests[0].Logged = true;
        Assert.Null(AuthoritativeQuestTownerInteractionSelector.Select(snapshot));
    }

    [Fact]
    public void SelectsVariableGatedActivationForAnAlreadyActiveQuest()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    LevelId = 0,
                    PositionX = 61,
                    PositionY = 63,
                    VisitedLevelIds = { 0, 4 },
                },
            },
            Quests = {
                new QuestSnapshot {
                    QuestId = 3001,
                    Variable2 = 0,
                },
            },
            QuestTownerInteractions = {
                new QuestTownerInteractionSnapshot {
                    InteractionId = 300207,
                    QuestId = 3001,
                    TownerId = 0,
                    LevelId = 0,
                    PositionX = 62,
                    PositionY = 63,
                    Kind = 0,
                    RequiredVariable2 = 0,
                    Variable2 = 1,
                    MessageId = 115,
                    RequiredVisitedLevel1 = 4,
                    RequiredVisitedLevel2 = 5,
                },
            },
        };

        Assert.Equal(
            300207U,
            AuthoritativeQuestTownerInteractionSelector.Select(snapshot)!.InteractionId);

        snapshot.Players[0].VisitedLevelIds.Clear();
        snapshot.Players[0].VisitedLevelIds.Add(0);
        Assert.Null(AuthoritativeQuestTownerInteractionSelector.Select(snapshot));
    }

    [Fact]
    public void EnumeratesAndCyclesAuthoritativeSpellSourcesWithoutChangingTheirOwnership()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    AbilitySpellId = 28,
                    AbilityKind = PlayerAbilityKind.TrapDisarm,
                    MemorizedSpells = {
                        new PlayerSpellLevelSnapshot {
                            SpellId = 3,
                            Level = 2,
                            DisplayName = "Lightning",
                            TargetingKind = SpellTargetingKind.CombatEntity,
                        },
                        new PlayerSpellLevelSnapshot {
                            SpellId = 1,
                            Level = 1,
                            DisplayName = "Firebolt",
                            TargetingKind = SpellTargetingKind.CombatEntity,
                        },
                    },
                    Equipment = {
                        new EquippedItemSnapshot {
                            Slot = 4,
                            ItemSeed = 99,
                            State = new ItemStateSnapshot {
                                SpellId = 30,
                                Charges = 3,
                                StableItemId = 700,
                                MiscId = 23,
                                SpellDisplayName = "Charged Bolt",
                                SpellSourceKind = ItemSpellSourceKind.ChargedItem,
                                SpellTargetingKind = SpellTargetingKind.CombatEntity,
                            },
                        },
                    },
                    Inventory = {
                        new ItemSnapshot {
                            ItemSeed = 100,
                            State = new ItemStateSnapshot {
                                SpellId = 32,
                                StableItemId = 701,
                                MiscId = 22,
                                SpellSourceKind = ItemSpellSourceKind.ConsumableItem,
                            },
                        },
                    },
                    Belt = {
                        new BeltItemSnapshot {
                            Slot = 2,
                            ItemSeed = 101,
                            State = new ItemStateSnapshot {
                                SpellId = 33,
                                StableItemId = 702,
                                MiscId = 22,
                                SpellDisplayName = "Telekinesis",
                                SpellSourceKind = ItemSpellSourceKind.ConsumableItem,
                                SpellTargetingKind = SpellTargetingKind.WorldEntity,
                            },
                        },
                    },
                },
            },
        };

        var sources = AuthoritativeSpellSourceSelector.GetSources(snapshot);

        Assert.Equal(
            ["memorized:1:1", "memorized:3:2", "equipment:4:99", "inventory:0", "belt:2", "ability:28"],
            sources.Select(source => source.Key));
        Assert.Equal(CastSourceKind.MemorizedSpell, sources[0].Kind);
        Assert.Equal(SpellTargetingKind.CombatEntity, sources[0].TargetingKind);
        Assert.Equal("Firebolt (level 1)", sources[0].DisplayName);
        Assert.Equal(CastSourceKind.ChargedItem, sources[2].Kind);
        Assert.Equal(SpellTargetingKind.CombatEntity, sources[2].TargetingKind);
        Assert.Equal("Charged Bolt (staff, 3 charges)", sources[2].DisplayName);
        Assert.Equal(PlayerItemLocation.Equipment, sources[2].SourceItem!.Location);
        Assert.Equal(4U, sources[2].SourceItem!.Slot);
        Assert.Equal(700U, sources[2].SourceItem!.StableItemId);
        Assert.Equal(CastSourceKind.ConsumableItem, sources[3].Kind);
        Assert.Equal(PlayerItemLocation.Inventory, sources[3].SourceItem!.Location);
        Assert.Equal(701U, sources[3].SourceItem!.StableItemId);
        Assert.Equal(702U, sources[4].SourceItem!.StableItemId);
        Assert.Equal(SpellTargetingKind.WorldEntity, sources[4].TargetingKind);
        Assert.Equal(CastSourceKind.Ability, sources[^1].Kind);
        Assert.Equal("Ability TrapDisarm", sources[^1].DisplayName);

        var selected = AuthoritativeSpellSourceSelector.Select(sources, "memorized:3:2");
        Assert.Equal("memorized:3:2", selected!.Key);
        Assert.Equal("equipment:4:99", AuthoritativeSpellSourceSelector
            .Cycle(sources, selected.Key, 1)!.Key);
        Assert.Equal("ability:28", AuthoritativeSpellSourceSelector
            .Cycle(sources, "memorized:1:1", -1)!.Key);
        Assert.Equal("memorized:1:1", AuthoritativeSpellSourceSelector
            .Select(sources, "missing")!.Key);
    }

    [Fact]
    public void ClassifiesServerOwnedConsumableActionsForThePresentationBoundary()
    {
        Assert.True(AuthoritativeItemActionRules.IsTargetlessConsumable(
            ItemActionKind.RestoreFullLifeAndMana));
        Assert.True(AuthoritativeItemActionRules.IsTargetlessConsumable(
            ItemActionKind.IncreaseAllAttributes));
        Assert.False(AuthoritativeItemActionRules.IsTargetlessConsumable(
            ItemActionKind.ApplySharpnessOil));
        Assert.True(AuthoritativeItemActionRules.IsTargetedConsumable(
            ItemActionKind.ApplySharpnessOil));
        Assert.False(AuthoritativeItemActionRules.IsTargetedConsumable(
            ItemActionKind.RestorePartialLife));
        var target = new PlayerItemReference {
            Location = PlayerItemLocation.Equipment,
            Slot = 4,
            StableItemId = 700,
        };
        Assert.True(AuthoritativeItemActionRules.AreSameReference(
            target,
            new PlayerItemReference {
                Location = PlayerItemLocation.Equipment,
                Slot = 4,
                StableItemId = 700,
            }));
        Assert.False(AuthoritativeItemActionRules.AreSameReference(
            target,
            new PlayerItemReference {
                Location = PlayerItemLocation.Inventory,
                Slot = 4,
                StableItemId = 700,
            }));
    }

    [Fact]
    public void AppliesAuthoritativeSnapshotAndEvents()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Tick = 12,
                Players = { new PlayerSnapshot { EntityId = 7, Life = 30, LifeMaximum = 40 } },
            }));
        model.Apply(new AuthoritativeClientMessage(
            Events: new EventBatch {
                Tick = 12,
                Events = {
                    new GameEvent { Damage = new DamageEvent { TargetEntityId = 7, Amount = 3 } },
                    new GameEvent {
                        Dialogue = new DialogueEvent {
                            SourceEntityId = 7,
                            TargetEntityId = 20,
                            QuestId = 3003,
                            MessageId = 77,
                        },
                    },
                    new GameEvent {
                        Victory = new VictoryEvent {
                            PlayerEntityId = 7,
                            BossEntityId = 20,
                            DiabloKillLevel = 1,
                        },
                    },
                },
            }));

        Assert.Equal(12UL, model.CurrentTick);
        Assert.Equal(7U, model.PlayerEntityId);
        Assert.Equal(3, model.RecentEvents.Count);
        Assert.Equal(3, model.RecentEvents[0].Damage.Amount);
        Assert.Equal(3003U, model.RecentEvents[1].Dialogue.QuestId);
        Assert.Equal(77, model.RecentEvents[1].Dialogue.MessageId);
        Assert.Equal(1U, model.RecentEvents[2].Victory.DiabloKillLevel);
        Assert.Equal(13UL, model.NextCommandTick);
    }

    [Fact]
    public void BoundsRecentEventHistoryAndAdvancesItsSequenceWindow()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Tick = 1,
            Players = { new PlayerSnapshot { EntityId = 7 } },
        }));
        var events = new EventBatch { Tick = 1 };
        for (var amount = 0; amount < 300; amount++)
            events.Events.Add(new GameEvent {
                Experience = new ExperienceEvent { PlayerEntityId = 7, Amount = (uint)amount },
            });

        model.Apply(new AuthoritativeClientMessage(Events: events));

        Assert.Equal(256, model.RecentEvents.Count);
        Assert.Equal(44U, model.RecentEvents[0].Experience.Amount);
        Assert.Equal(299U, model.RecentEvents[^1].Experience.Amount);
        Assert.Equal(44UL, model.RecentEventsStartSequence);
        Assert.Equal(300UL, model.RecentEventsEndSequence);

        model.ClearRecentEvents();

        Assert.Empty(model.RecentEvents);
        Assert.Equal(300UL, model.RecentEventsStartSequence);
        Assert.Equal(300UL, model.RecentEventsEndSequence);
    }

    [Fact]
    public void RejectsSnapshotsWithoutALocalPlayerOrWithInvalidEntityIds()
    {
        var model = new AuthoritativeClientModel();

        Assert.Throws<InvalidDataException>(() => model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot())));
        Assert.Throws<InvalidDataException>(() => model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = { new PlayerSnapshot() },
            })));
        Assert.Throws<InvalidDataException>(() => model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = {
                    new PlayerSnapshot { EntityId = 7 },
                    new PlayerSnapshot { EntityId = 7 },
                },
            })));
    }

    [Fact]
    public void RejectsCrossCollectionEntityIdCollisions()
    {
        var model = new AuthoritativeClientModel();
        var valid = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Monsters = {
                new MonsterSnapshot { EntityId = 8 },
            },
            WorldItems = {
                new WorldItemSnapshot {
                    EntityId = 9,
                    ItemSeed = 42,
                    State = new ItemStateSnapshot(),
                },
            },
            Objects = { new ObjectSnapshot { EntityId = 10, ObjectId = 4 } },
            Projectiles = {
                new ProjectileSnapshot {
                    EntityId = 11,
                    SourceEntityId = 13,
                    SpellId = 1,
                    RemainingTicks = 1,
                },
            },
            Corpses = { new CorpseSnapshot { EntityId = 12 } },
        };
        model.Apply(new AuthoritativeClientMessage(Snapshot: valid));

        foreach (var mutate in new Action<Snapshot>[] {
            snapshot => snapshot.Monsters[0].EntityId = 7,
            snapshot => snapshot.WorldItems[0].EntityId = 7,
            snapshot => snapshot.Objects[0].EntityId = 7,
            snapshot => snapshot.Projectiles[0].EntityId = 7,
            snapshot => snapshot.Corpses[0].EntityId = 7,
        }) {
            var invalid = valid.Clone();
            mutate(invalid);
            Assert.Throws<InvalidDataException>(() =>
                model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
        }
    }

    [Fact]
    public void RejectsMalformedAuthoredObjectMetadata()
    {
        var model = new AuthoritativeClientModel();
        var valid = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Objects = {
                new ObjectSnapshot {
                    EntityId = 10,
                    ObjectId = 4,
                },
            },
        };
        model.Apply(new AuthoritativeClientMessage(Snapshot: valid));

        var invalidTransition = valid.Clone();
        invalidTransition.Objects[0].QuestTransitionKind = 1;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidTransition)));

        var invalidReward = valid.Clone();
        invalidReward.Objects[0].RewardKind = 1;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidReward)));

        var nativeMonsterFollowUp = valid.Clone();
        nativeMonsterFollowUp.Objects[0].EffectKind = 4;
        nativeMonsterFollowUp.Objects[0].QuestId = 3002;
        nativeMonsterFollowUp.Quests.Add(new QuestSnapshot {
            QuestId = 3002,
            RequiredProgress = 1,
        });
        nativeMonsterFollowUp.Objects[0].QuestSequenceFollowUpKind = 3;
        nativeMonsterFollowUp.Objects[0].QuestSequenceFollowUpAmount = 139;
        nativeMonsterFollowUp.Objects[0].QuestSequenceFollowUpOffsetX = 1;
        model.Apply(new AuthoritativeClientMessage(Snapshot: nativeMonsterFollowUp));

        var invalidFollowUp = valid.Clone();
        invalidFollowUp.Objects[0].EffectKind = 4;
        invalidFollowUp.Objects[0].QuestSequenceFollowUpKind = 1;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidFollowUp)));

        var invalidProjectileSource = valid.Clone();
        invalidProjectileSource.Objects[0].ProjectileSourceKind = 1;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidProjectileSource)));

        var invalidPresentation = valid.Clone();
        invalidPresentation.Objects[0].PresentationKind
            = (Devilution.Protocol.V1.ObjectPresentationKind)99;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidPresentation)));

        var invalidEffect = valid.Clone();
        invalidEffect.Objects[0].EffectKind = 10;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidEffect)));

        var invalidCollision = valid.Clone();
        invalidCollision.Objects[0].Breakable = true;
        invalidCollision.Objects[0].MissilePassable = true;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidCollision)));

        var invalidDestroyedState = valid.Clone();
        invalidDestroyedState.Objects[0].Destroyed = true;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidDestroyedState)));
    }

    [Fact]
    public void RejectsAuthoredObjectWithUnknownQuestReference()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Objects = {
                new ObjectSnapshot {
                    EntityId = 10,
                    QuestId = 3002,
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public void RejectsZeroOrDuplicateQuestIdentities()
    {
        var model = new AuthoritativeClientModel();
        var zeroId = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Quests = { new QuestSnapshot { RequiredProgress = 1 } },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: zeroId)));

        var duplicateIds = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Quests = {
                new QuestSnapshot { QuestId = 3002, RequiredProgress = 1 },
                new QuestSnapshot { QuestId = 3002, RequiredProgress = 1 },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: duplicateIds)));

        var invalidNativeQuestId = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Quests = {
                new QuestSnapshot {
                    QuestId = 3002,
                    RequiredProgress = 1,
                    NativeQuestId = 24,
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidNativeQuestId)));

        var duplicateNativeQuestIds = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Quests = {
                new QuestSnapshot {
                    QuestId = 3002,
                    RequiredProgress = 1,
                    NativeQuestId = 0,
                },
                new QuestSnapshot {
                    QuestId = 3003,
                    RequiredProgress = 1,
                    NativeQuestId = 0,
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: duplicateNativeQuestIds)));

        var oversizedUtf8Identity = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Quests = {
                new QuestSnapshot {
                    QuestId = 3002,
                    RequiredProgress = 1,
                    CompletionUniqueIdentity = new string('é', 65),
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: oversizedUtf8Identity)));
    }

    [Fact]
    public void ValidatesAndRetainsTheAuthoritativeHeldItem()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    HeldItem = new ItemSnapshot {
                        ItemSeed = 42,
                        Price = 65,
                        State = new ItemStateSnapshot {
                            ItemType = 11,
                            InventoryWidth = 1,
                            InventoryHeight = 1,
                            Value = 65,
                        },
                    },
                },
            },
        };

        model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot));

        Assert.Equal(42U, model.Snapshot!.Players[0].HeldItem.ItemSeed);
        var missingSeed = snapshot.Clone();
        missingSeed.Players[0].HeldItem.ItemSeed = 0;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: missingSeed)));
        var oversized = snapshot.Clone();
        oversized.Players[0].HeldItem.State.InventoryWidth = 11;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: oversized)));
        var invalidPrice = snapshot.Clone();
        invalidPrice.Players[0].HeldItem.Price = uint.MaxValue;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidPrice)));
    }

    [Fact]
    public void RejectsOversizedPlayerItemPrices()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    Inventory = {
                        new ItemSnapshot {
                            ItemSeed = 42,
                            Price = uint.MaxValue,
                            State = new ItemStateSnapshot { InventoryWidth = 1, InventoryHeight = 1 },
                        },
                    },
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public void RejectsPlayerItemsWithIncompleteIdentity()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    Inventory = {
                        new ItemSnapshot {
                            State = new ItemStateSnapshot(),
                        },
                    },
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public void RejectsOversizedInventoryFootprintsInsteadOfClampingThem()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    Inventory = {
                        new ItemSnapshot {
                            ItemSeed = 42,
                            State = new ItemStateSnapshot {
                                InventoryWidth = 11,
                                InventoryHeight = 1,
                            },
                        },
                    },
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public void RejectsDuplicateNonzeroPlayerItemSeedsAcrossCollections()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    Inventory = {
                        new ItemSnapshot {
                            ItemSeed = 42,
                            State = new ItemStateSnapshot(),
                        },
                    },
                    Belt = {
                        new BeltItemSnapshot {
                            ItemSeed = 42,
                            State = new ItemStateSnapshot(),
                        },
                    },
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public void RejectsMalformedInventoryTopology()
    {
        var model = new AuthoritativeClientModel();
        var invalidBeltSlot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    Belt = {
                        new BeltItemSnapshot {
                            Slot = 8,
                            ItemSeed = 42,
                            State = new ItemStateSnapshot(),
                        },
                    },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidBeltSlot)));

        var duplicateEquipmentSlot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    Equipment = {
                        new EquippedItemSnapshot {
                            Slot = 4,
                            ItemSeed = 42,
                            State = new ItemStateSnapshot(),
                        },
                        new EquippedItemSnapshot {
                            Slot = 4,
                            ItemSeed = 43,
                            State = new ItemStateSnapshot(),
                        },
                    },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: duplicateEquipmentSlot)));

        var invalidGridReference = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    Inventory = {
                        new ItemSnapshot {
                            ItemSeed = 42,
                            State = new ItemStateSnapshot(),
                        },
                    },
                    InventoryGrid = { -1, 1 },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidGridReference)));

        var invalidGridFootprint = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    Inventory = {
                        new ItemSnapshot {
                            ItemSeed = 42,
                            State = new ItemStateSnapshot {
                                InventoryWidth = 2,
                            },
                        },
                    },
                },
            },
        };
        invalidGridFootprint.Players[0].InventoryGrid.Add(Enumerable.Repeat(-1, 40));
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidGridFootprint)));
    }

    [Fact]
    public void RejectsMalformedAuthoritativeSpellState()
    {
        var model = new AuthoritativeClientModel();
        var invalidAbility = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    AbilitySpellId = 256,
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidAbility)));

        var invalidLevel = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    MemorizedSpells = {
                        new PlayerSpellLevelSnapshot { SpellId = 1, Level = 16 },
                    },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidLevel)));

        var duplicateSpell = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    MemorizedSpells = {
                        new PlayerSpellLevelSnapshot { SpellId = 1, Level = 1 },
                        new PlayerSpellLevelSnapshot { SpellId = 1, Level = 2 },
                    },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: duplicateSpell)));

        var invalidTargeting = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    MemorizedSpells = {
                        new PlayerSpellLevelSnapshot {
                            SpellId = 1,
                            Level = 1,
                            TargetingKind = (SpellTargetingKind)99,
                        },
                    },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidTargeting)));
    }

    [Fact]
    public void RejectsPlayerValuesOutsideNativeRanges()
    {
        var model = new AuthoritativeClientModel();
        var invalidGold = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    Gold = uint.MaxValue,
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidGold)));

        var invalidStashGold = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    StashGold = uint.MaxValue,
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidStashGold)));

        var invalidReflectCharges = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    ReflectCharges = ushort.MaxValue + 1U,
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidReflectCharges)));

        var invalidProgression = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    CharacterLevel = 51,
                    StatPoints = uint.MaxValue,
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidProgression)));
    }

    [Fact]
    public void RejectsMalformedAuthoritativeProjectiles()
    {
        var model = new AuthoritativeClientModel();
        var invalidIdentity = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Projectiles = {
                new ProjectileSnapshot {
                    EntityId = 10,
                    RemainingTicks = 1,
                    SpellId = 1,
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidIdentity)));

        var invalidEffects = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Projectiles = {
                new ProjectileSnapshot {
                    EntityId = 10,
                    SourceEntityId = 11,
                    SpellId = 1,
                    RemainingTicks = 1,
                    Damage = -1,
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidEffects)));

        var invalidCodes = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Projectiles = {
                new ProjectileSnapshot {
                    EntityId = 10,
                    SourceEntityId = 11,
                    SpellId = 1,
                    RemainingTicks = 1,
                    DamageType = 5,
                    Behavior = 60,
                    Phase = 2,
                    PresentationKind = (Devilution.Protocol.V1.ProjectilePresentationKind)99,
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidCodes)));
    }

    [Fact]
    public void RejectsImpossibleCoreItemStateRanges()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    Inventory = {
                        new ItemSnapshot {
                            ItemSeed = 42,
                            State = new ItemStateSnapshot {
                                Value = -1,
                                MaxCharges = 1,
                                Charges = 2,
                            },
                        },
                    },
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));

        snapshot.Players[0].Inventory[0].State.Value = 0;
        snapshot.Players[0].Inventory[0].State.MaxCharges = 0;
        snapshot.Players[0].Inventory[0].State.CreateInfo = ushort.MaxValue + 1U;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));

        snapshot.Players[0].Inventory[0].State.CreateInfo = 0;
        snapshot.Players[0].Inventory[0].State.PlusDamage = short.MaxValue + 1;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));

        snapshot.Players[0].Inventory[0].State.Value = 0;
        snapshot.Players[0].Inventory[0].State.Charges = 0;
        snapshot.Players[0].Inventory[0].State.MaxCharges = 0;
        snapshot.Players[0].Inventory[0].State.MinDamage = 5;
        snapshot.Players[0].Inventory[0].State.MaxDamage = 4;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public void RejectsInconsistentOrMalformedActiveStoreSnapshots()
    {
        var model = new AuthoritativeClientModel();
        var missingStore = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, ActiveStoreId = 1 } },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: missingStore)));

        var duplicateSlot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, ActiveStoreId = 1 } },
            ActiveStore = new StoreSnapshot {
                StoreId = 1,
                Items = {
                    new StoreItemSnapshot {
                        StoreSlot = 4,
                        ItemSeed = 42,
                        State = new ItemStateSnapshot { ItemType = 1 },
                    },
                    new StoreItemSnapshot {
                        StoreSlot = 4,
                        ItemSeed = 43,
                        State = new ItemStateSnapshot { ItemType = 1 },
                    },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: duplicateSlot)));

        var unsupportedPrice = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, ActiveStoreId = 1 } },
            ActiveStore = new StoreSnapshot {
                StoreId = 1,
                Items = {
                    new StoreItemSnapshot {
                        StoreSlot = 4,
                        ItemSeed = 42,
                        Price = uint.MaxValue,
                        State = new ItemStateSnapshot { ItemType = 1 },
                    },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: unsupportedPrice)));

        var invalidIdentity = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, ActiveStoreId = 1 } },
            ActiveStore = new StoreSnapshot {
                StoreId = 1,
                Items = {
                    new StoreItemSnapshot {
                        StoreSlot = 4,
                        State = new ItemStateSnapshot { ItemType = 1 },
                    },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidIdentity)));
    }

    [Fact]
    public void AppliesMultiplayerSnapshotsWithTheReceivingParticipantFirst()
    {
        var model = new AuthoritativeClientModel();

        model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = {
                    new PlayerSnapshot { EntityId = 8, PositionX = 3, PositionY = 4 },
                    new PlayerSnapshot { EntityId = 7, PositionX = 9, PositionY = 10 },
                },
            }));

        Assert.Equal(8U, model.PlayerEntityId);
        Assert.Equal(2, model.Snapshot!.Players.Count);
        Assert.Null(model.PredictedPlayerPosition);
    }

    [Fact]
    public void ProjectsServerOwnedArmedTrapStateForPresentation()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = { new PlayerSnapshot { EntityId = 7 } },
                Objects = {
                    new ObjectSnapshot {
                        EntityId = 20,
                        ObjectId = 4,
                        TrapArmed = true,
                        PresentationKind = Devilution.Protocol.V1.ObjectPresentationKind.Trap,
                    },
                    new ObjectSnapshot { EntityId = 21, ObjectId = 5, TrapArmed = false },
                },
            }));

        var trap = Assert.Single(model.ArmedTrapObjects);
        Assert.Equal(20U, trap.EntityId);
        Assert.True(trap.TrapArmed);
        Assert.Equal(
            Devilution.Protocol.V1.ObjectPresentationKind.Trap,
            trap.PresentationKind);
    }

    [Fact]
    public void RetainsCharacterNamesAndNativeDynamicEarPresentation()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    CharacterName = "Dead-RogueDM",
                    ManaShieldActive = true,
                    StatusEffects = {
                        new StatusEffectSnapshot {
                            EffectId = 9,
                            RemainingTicks = 1584,
                            Behavior = StatusEffectBehavior.Infravision,
                        },
                        new StatusEffectSnapshot {
                            EffectId = 46,
                            RemainingTicks = 255,
                            Behavior = StatusEffectBehavior.Search,
                        },
                        new StatusEffectSnapshot {
                            EffectId = 53,
                            RemainingTicks = 100,
                            Behavior = StatusEffectBehavior.Etherealize,
                        },
                    },
                },
            },
            WorldItems = {
                new WorldItemSnapshot {
                    EntityId = 20,
                    ItemSeed = 0,
                    State = new ItemStateSnapshot {
                        CreateInfo = 0x4100,
                        ItemIndex = 23,
                        MiscId = 44,
                        BaseName = "Ear of A",
                        IdentifiedName = "A",
                        NativeCursorGraphic = 21,
                        PresentationKind = ProtocolItemPresentationKind.Ear,
                    },
                },
            },
        };

        model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot));

        Assert.Equal("Dead-RogueDM", model.Snapshot!.Players[0].CharacterName);
        Assert.True(model.Snapshot.Players[0].ManaShieldActive);
        Assert.Equal(
            [
                StatusEffectBehavior.Infravision,
                StatusEffectBehavior.Search,
                StatusEffectBehavior.Etherealize,
            ],
            model.Snapshot.Players[0].StatusEffects.Select(effect => effect.Behavior));
        var ear = Assert.Single(model.Snapshot.WorldItems);
        Assert.Equal(0U, ear.ItemSeed);
        Assert.Equal("Ear of A", ear.State.BaseName);
        Assert.Equal("A", ear.State.IdentifiedName);
        Assert.Equal(21, ear.State.NativeCursorGraphic);
        Assert.Equal(ProtocolItemPresentationKind.Ear, ear.State.PresentationKind);

        var missingDynamicName = snapshot.Clone();
        missingDynamicName.WorldItems[0].State.IdentifiedName = "";
        Assert.Throws<InvalidDataException>(() => model.Apply(
            new AuthoritativeClientMessage(Snapshot: missingDynamicName)));
        var missingCursor = snapshot.Clone();
        missingCursor.WorldItems[0].State.ClearNativeCursorGraphic();
        Assert.Throws<InvalidDataException>(() => model.Apply(
            new AuthoritativeClientMessage(Snapshot: missingCursor)));
        var invalidPrice = snapshot.Clone();
        invalidPrice.WorldItems[0].Price = uint.MaxValue;
        Assert.Throws<InvalidDataException>(() => model.Apply(
            new AuthoritativeClientMessage(Snapshot: invalidPrice)));
        var invalidPresentation = snapshot.Clone();
        invalidPresentation.WorldItems[0].State.PresentationKind
            = (Devilution.Protocol.V1.ItemPresentationKind)99;
        Assert.Throws<InvalidDataException>(() => model.Apply(
            new AuthoritativeClientMessage(Snapshot: invalidPresentation)));
        var invalidSpellSource = snapshot.Clone();
        invalidSpellSource.WorldItems[0].State.SpellSourceKind
            = (Devilution.Protocol.V1.ItemSpellSourceKind)99;
        Assert.Throws<InvalidDataException>(() => model.Apply(
            new AuthoritativeClientMessage(Snapshot: invalidSpellSource)));
        var invalidAction = snapshot.Clone();
        invalidAction.WorldItems[0].State.ItemActionKind
            = (Devilution.Protocol.V1.ItemActionKind)99;
        Assert.Throws<InvalidDataException>(() => model.Apply(
            new AuthoritativeClientMessage(Snapshot: invalidAction)));
        var invalidTargeting = snapshot.Clone();
        invalidTargeting.WorldItems[0].State.SpellTargetingKind
            = (Devilution.Protocol.V1.SpellTargetingKind)99;
        Assert.Throws<InvalidDataException>(() => model.Apply(
            new AuthoritativeClientMessage(Snapshot: invalidTargeting)));
        var invalidSpellName = snapshot.Clone();
        invalidSpellName.WorldItems[0].State.SpellDisplayName = "bad\0name";
        Assert.Throws<InvalidDataException>(() => model.Apply(
            new AuthoritativeClientMessage(Snapshot: invalidSpellName)));
        var invalidPlayerName = snapshot.Clone();
        invalidPlayerName.Players[0].CharacterName = "bad name";
        Assert.Throws<InvalidDataException>(() => model.Apply(
            new AuthoritativeClientMessage(Snapshot: invalidPlayerName)));
        var invalidStatus = snapshot.Clone();
        invalidStatus.Players[0].StatusEffects[0].Magnitude = 1;
        Assert.Throws<InvalidDataException>(() => model.Apply(
            new AuthoritativeClientMessage(Snapshot: invalidStatus)));
        var invalidSearch = snapshot.Clone();
        invalidSearch.Players[0].StatusEffects[1].Magnitude = 1;
        Assert.Throws<InvalidDataException>(() => model.Apply(
            new AuthoritativeClientMessage(Snapshot: invalidSearch)));
        var invalidEtherealize = snapshot.Clone();
        invalidEtherealize.Players[0].StatusEffects[2].Magnitude = 1;
        Assert.Throws<InvalidDataException>(() => model.Apply(
            new AuthoritativeClientMessage(Snapshot: invalidEtherealize)));
    }

    [Fact]
    public void AppliesRemoteDeathAndServerOwnedResurrectionProjection()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Tick = 1,
                Players = {
                    new PlayerSnapshot { EntityId = 7, Life = 1000, LifeMaximum = 1000 },
                    new PlayerSnapshot {
                        EntityId = 8,
                        PositionX = 10,
                        PositionY = 10,
                        LifeMaximum = 1000,
                        CombatMode = PlayerCombatMode.Death,
                    },
                },
            }));
        Assert.Equal(PlayerCombatMode.Death, model.Snapshot!.Players[1].CombatMode);

        model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Tick = 13,
                Players = {
                    new PlayerSnapshot { EntityId = 7, Life = 1000, LifeMaximum = 1000 },
                    new PlayerSnapshot {
                        EntityId = 8,
                        PositionX = 10,
                        PositionY = 10,
                        Life = 640,
                        LifeMaximum = 1000,
                    },
                },
                Projectiles = {
                    new ProjectileSnapshot {
                        EntityId = 0x80000000,
                        SourceEntityId = 7,
                        TargetEntityId = 8,
                        SpellId = 32,
                        PositionX = 10,
                        PositionY = 10,
                        TargetX = 10,
                        TargetY = 10,
                        RemainingTicks = 15,
                        Behavior = 11,
                    },
                },
            }));

        Assert.Equal(7U, model.PlayerEntityId);
        Assert.Equal(640, model.Snapshot!.Players[1].Life);
        var beam = Assert.Single(model.Snapshot.Projectiles);
        Assert.Equal(8U, beam.TargetEntityId);
        Assert.Equal(11, beam.Behavior);
    }

    [Fact]
    public void AppliesServerOwnedRestartTownBarrier()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Tick = 42,
                Players = {
                    new PlayerSnapshot {
                        EntityId = 7,
                        PositionX = 75,
                        PositionY = 68,
                        Life = 64,
                        LifeMaximum = 640,
                        CombatMode = PlayerCombatMode.NewLevel,
                        CombatModeEndTick = 43,
                        CurrentAction = new PlayerActionSnapshot {
                            Kind = PlayerActionKind.RestartTown,
                            StartedAtTick = 42,
                            EffectTick = 42,
                            ReadyTick = 43,
                            EndTick = 43,
                            EffectApplied = true,
                        },
                    },
                },
            }));

        var player = Assert.Single(model.Snapshot!.Players);
        Assert.Equal((0U, 75, 68, 64), (
            player.LevelId,
            player.PositionX,
            player.PositionY,
            player.Life));
        Assert.Equal(PlayerActionKind.RestartTown, player.CurrentAction!.Kind);

        var invalid = model.Snapshot.Clone();
        invalid.Players[0].CurrentAction.TargetEntityId = 5;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
    }

    [Fact]
    public void ValidatesAndRetainsAuthoritativePlayerCombatMode()
    {
        var model = new AuthoritativeClientModel();
        var blocking = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    CombatMode = PlayerCombatMode.Block,
                    CombatModeEndTick = 9,
                },
            },
        };

        model.Apply(new AuthoritativeClientMessage(Snapshot: blocking));

        Assert.Equal(PlayerCombatMode.Block, model.Snapshot!.Players[0].CombatMode);
        var invalid = blocking.Clone();
        invalid.Players[0].CombatModeEndTick = 0;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
        var invalidRemote = blocking.Clone();
        invalidRemote.Players.Add(new PlayerSnapshot {
            EntityId = 8,
            CombatMode = PlayerCombatMode.GotHit,
        });
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidRemote)));
    }

    [Fact]
    public void RejectsImpossibleAuthoritativePlayerResources()
    {
        var model = new AuthoritativeClientModel();
        var valid = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    Life = 30,
                    LifeMaximum = 40,
                    Mana = 10,
                    ManaMaximum = 20,
                },
            },
        };
        model.Apply(new AuthoritativeClientMessage(Snapshot: valid));

        foreach (var mutate in new Action<PlayerSnapshot>[] {
            player => player.Life = 41,
            player => player.Mana = 21,
            player => player.Life = -1,
            player => player.ManaMaximum = -1,
        }) {
            var invalid = valid.Clone();
            mutate(invalid.Players[0]);
            Assert.Throws<InvalidDataException>(() =>
                model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
        }
    }

    [Fact]
    public void RejectsImpossibleAuthoritativeMonsterVitality()
    {
        var model = new AuthoritativeClientModel();
        var valid = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 9,
                    Alive = true,
                    HitPoints = 3,
                    MaxHitPoints = 5,
                    HitPointsQ6 = 192,
                    MaxHitPointsQ6 = 320,
                },
            },
        };
        model.Apply(new AuthoritativeClientMessage(Snapshot: valid));

        foreach (var mutate in new Action<MonsterSnapshot>[] {
            monster => monster.HitPoints = 6,
            monster => monster.HitPointsQ6 = 321,
            monster => monster.MaxHitPoints = 2,
            monster => monster.HitPoints = -1,
        }) {
            var invalid = valid.Clone();
            mutate(invalid.Monsters[0]);
            Assert.Throws<InvalidDataException>(() =>
                model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
        }
    }

    [Fact]
    public void RejectsMalformedAuthoritativeMonsterCombatState()
    {
        var model = new AuthoritativeClientModel();
        var valid = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 9,
                    HitPoints = 3,
                    MaxHitPoints = 5,
                    HitPointsQ6 = 192,
                    MaxHitPointsQ6 = 320,
                },
            },
        };
        model.Apply(new AuthoritativeClientMessage(Snapshot: valid));

        foreach (var mutate in new Action<MonsterSnapshot>[] {
            monster => monster.AttackDamage = -1,
            monster => monster.MinDamage = 5,
            monster => monster.MaxDamage = -1,
            monster => monster.MinDamageSpecial = 5,
            monster => monster.MaxDamageSpecial = -1,
            monster => monster.AggroRange = -1,
            monster => monster.MonsterLevel = -1,
            monster => monster.DeathAnimationTicks = 1025,
            monster => monster.DeathTicksRemaining = -1,
            monster => monster.ExperienceReward = 1_000_001,
            monster => monster.UniqueIdentity = new string('x', 129),
            monster => monster.UniqueIdentity = new string('é', 65),
            monster => monster.UniqueIdentity = "not-unique",
            monster => monster.ToHit = 256,
            monster => monster.ToHitSpecial = -1,
            monster => monster.Intelligence = 256,
            monster => monster.Direction = 8,
            monster => monster.FireResistance = -101,
            monster => monster.LightningResistance = 101,
            monster => monster.MagicResistance = -101,
            monster => monster.AcidResistance = 101,
            monster => monster.OwnerPlayerId = 11,
        }) {
            var invalid = valid.Clone();
            mutate(invalid.Monsters[0]);
            Assert.Throws<InvalidDataException>(() =>
                model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
        }

        var invalidDeathTimeline = valid.Clone();
        invalidDeathTimeline.Monsters[0].DeathAnimationTicks = 5;
        invalidDeathTimeline.Monsters[0].DeathTicksRemaining = 1;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidDeathTimeline)));
    }

    [Fact]
    public void RejectsMalformedAuthoritativeMonsterMovementState()
    {
        var model = new AuthoritativeClientModel();
        var moving = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 9,
                    PositionX = 2,
                    PositionY = 3,
                    Moving = true,
                    FuturePositionX = 3,
                    FuturePositionY = 3,
                },
            },
        };
        model.Apply(new AuthoritativeClientMessage(Snapshot: moving));

        var invalidMoving = moving.Clone();
        invalidMoving.Monsters[0].FuturePositionX = 5;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidMoving)));

        var stationary = moving.Clone();
        stationary.Monsters[0].Moving = false;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: stationary)));
    }

    [Fact]
    public void ValidatesAndRetainsAuthoritativePlayerFacing()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    FacingDirection = PlayerDirection.NorthEast,
                },
            },
        };

        model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot));

        Assert.Equal(PlayerDirection.NorthEast, model.Snapshot!.Players[0].FacingDirection);
        var invalid = snapshot.Clone();
        invalid.Players[0].FacingDirection = (PlayerDirection)8;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
    }

    [Fact]
    public void ValidatesTimedMovementAndPresentsItsReservedDestination()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, PositionX = 2, PositionY = 3 },
            },
        }));
        model.TrackPredictedMove(12, 1, 0);
        var moving = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    PositionX = 2,
                    PositionY = 3,
                    CombatMode = PlayerCombatMode.WalkSouthwards,
                    CombatModeEndTick = 18,
                    CurrentAction = new PlayerActionSnapshot {
                        Kind = PlayerActionKind.Move,
                        StartedAtTick = 10,
                        EffectTick = 18,
                        ReadyTick = 18,
                        EndTick = 18,
                        DirectionX = 1,
                    },
                },
            },
        };

        model.Apply(new AuthoritativeClientMessage(Snapshot: moving));

        Assert.Equal(new ClientPosition(3, 3), model.PredictedPlayerPosition);
        Assert.True(model.IsCorrectingPosition);
        var applied = moving.Clone();
        applied.Players[0].PositionX = 3;
        applied.Players[0].CurrentAction.EffectApplied = true;
        model.Apply(new AuthoritativeClientMessage(Snapshot: applied));
        Assert.Null(model.PredictedPlayerPosition);
        Assert.False(model.IsCorrectingPosition);
        var invalid = moving.Clone();
        invalid.Players[0].CurrentAction.ReadyTick = 19;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
    }

    [Fact]
    public void RejectsMovementIntoProjectedBlockedCell()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 2,
                    Width = 4,
                    Height = 4,
                    BlockedCells = { 1 * 4 + 2 },
                },
            },
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    LevelId = 2,
                    PositionX = 1,
                    PositionY = 1,
                    CombatMode = PlayerCombatMode.WalkSideways,
                    CombatModeEndTick = 18,
                    CurrentAction = new PlayerActionSnapshot {
                        Kind = PlayerActionKind.Move,
                        StartedAtTick = 10,
                        EffectTick = 18,
                        ReadyTick = 18,
                        EndTick = 18,
                        DirectionX = 1,
                    },
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));

        snapshot.LevelGeometry[0].BlockedCells.Clear();
        snapshot.Players[0].PositionX = 3;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));

        snapshot.Players[0].PositionX = 1;
        model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot));
        Assert.Equal(PlayerActionKind.Move, model.Snapshot!.Players[0].CurrentAction.Kind);
    }

    [Fact]
    public void BoundsUnacknowledgedMovementPrediction()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7 },
            },
        }));

        for (ulong sequence = 1; sequence <= 16; sequence++)
            model.TrackPredictedMove(sequence, 1, 0);
        model.TrackPredictedMove(17, -1, 0);

        Assert.Equal(new ClientPosition(16, 0), model.PredictedPlayerPosition);
        Assert.True(model.IsCorrectingPosition);
    }

    [Fact]
    public void ClearsPendingMovementPredictionWhenTheAuthoritativePlayerChangesLevel()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 1, PositionX = 2, PositionY = 3 },
            },
        }));
        model.TrackPredictedMove(1, 1, 0);
        Assert.Equal(new ClientPosition(3, 3), model.PredictedPlayerPosition);

        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 2, PositionX = 10, PositionY = 11 },
            },
        }));

        Assert.Null(model.PredictedPlayerPosition);
        Assert.False(model.IsCorrectingPosition);
        model.TrackPredictedMove(2, 1, 0);
        Assert.Equal(new ClientPosition(11, 11), model.PredictedPlayerPosition);
    }

    [Fact]
    public void ResetsPresentationPredictionWithoutDroppingAuthoritativeSnapshot()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, PositionX = 2, PositionY = 3 },
            },
        }));
        model.TrackPredictedMove(1, 1, 0);

        model.ResetPrediction();

        Assert.NotNull(model.Snapshot);
        Assert.Null(model.PredictedPlayerPosition);
        Assert.False(model.IsCorrectingPosition);
    }

    [Fact]
    public void ResetsTransientPresentationWithoutDroppingAuthoritativeState()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Tick = 4,
            Players = {
                new PlayerSnapshot { EntityId = 7, PositionX = 2, PositionY = 3 },
            },
        }));
        model.Apply(new AuthoritativeClientMessage(Events: new EventBatch {
            Tick = 4,
            Events = {
                new GameEvent { Experience = new ExperienceEvent { PlayerEntityId = 7, Amount = 10 } },
            },
        }));
        model.TrackPredictedMove(1, 1, 0);

        model.ResetTransientPresentation();

        Assert.NotNull(model.Snapshot);
        Assert.Equal(4UL, model.CurrentTick);
        Assert.Empty(model.RecentEvents);
        Assert.Null(model.PredictedPlayerPosition);
        Assert.False(model.IsCorrectingPosition);
    }

    [Fact]
    public void IgnoresStaleSnapshotsWhileApplyingTheirAcknowledgements()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Tick = 10,
            Players = {
                new PlayerSnapshot { EntityId = 7, PositionX = 4, PositionY = 5 },
            },
        }));
        model.TrackPredictedMove(1, 1, 0);

        model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Tick = 9,
                Players = {
                    new PlayerSnapshot { EntityId = 7, PositionX = 1, PositionY = 1 },
                },
            },
            Acknowledgement: new CommandAck {
                Results = {
                    new CommandResult {
                        ClientSequence = 1,
                        Status = CommandStatus.Rejected,
                    },
                },
            }));

        Assert.Equal(10UL, model.CurrentTick);
        Assert.Equal((4, 5), (model.Snapshot!.Players[0].PositionX, model.Snapshot.Players[0].PositionY));
        Assert.Null(model.PredictedPlayerPosition);
        Assert.Equal(1UL, model.LastCommandResult!.ClientSequence);
    }

    [Fact]
    public void RetainsEventsThatArriveBeforeTheirAuthoritativeSnapshot()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Tick = 10,
            Players = { new PlayerSnapshot { EntityId = 7 } },
        }));
        model.Apply(new AuthoritativeClientMessage(Events: new EventBatch {
            Tick = 11,
            Events = {
                new GameEvent { Experience = new ExperienceEvent { PlayerEntityId = 7, Amount = 10 } },
            },
        }));
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Tick = 11,
            Players = { new PlayerSnapshot { EntityId = 7, PositionX = 1 } },
        }));

        var gameEvent = Assert.Single(model.RecentEvents);
        Assert.Equal(10U, gameEvent.Experience.Amount);
        Assert.Equal(11UL, model.CurrentTick);
    }

    [Fact]
    public void IgnoresStaleEventsAndDoesNotRegressCommandResultPresentation()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Tick = 10,
            Players = { new PlayerSnapshot { EntityId = 7 } },
        }));
        model.Apply(new AuthoritativeClientMessage(Acknowledgement: new CommandAck {
            Results = {
                new CommandResult { ClientSequence = 2, Status = CommandStatus.Accepted },
            },
        }));
        model.Apply(new AuthoritativeClientMessage(
            Events: new EventBatch {
                Tick = 9,
                Events = {
                    new GameEvent { Experience = new ExperienceEvent { PlayerEntityId = 7, Amount = 99 } },
                },
            },
            Acknowledgement: new CommandAck {
                Results = {
                    new CommandResult { ClientSequence = 1, Status = CommandStatus.Rejected },
                },
            }));

        Assert.Empty(model.RecentEvents);
        Assert.Equal(2UL, model.LastCommandResult!.ClientSequence);
        Assert.Equal(CommandStatus.Accepted, model.LastCommandResult.Status);
    }

    [Theory]
    [InlineData(PlayerActionKind.Death, PlayerCombatMode.Death, 0, 9U)]
    [InlineData(
        PlayerActionKind.LevelTransition,
        PlayerCombatMode.NewLevel,
        20,
        5U)]
    public void ValidatesTerminalPlayerActions(
        PlayerActionKind kind,
        PlayerCombatMode mode,
        int life,
        uint targetEntityId)
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    Life = life,
                    CombatMode = mode,
                    CombatModeEndTick = 40,
                    CurrentAction = new PlayerActionSnapshot {
                        Kind = kind,
                        StartedAtTick = 1,
                        EffectTick = 1,
                        ReadyTick = 40,
                        EndTick = 40,
                        EffectApplied = true,
                        TargetEntityId = targetEntityId,
                    },
                },
            },
        };

        model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot));

        Assert.Equal(kind, model.Snapshot!.Players[0].CurrentAction.Kind);
        var invalid = snapshot.Clone();
        invalid.Players[0].CurrentAction.EffectApplied = false;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
    }

    [Fact]
    public void KeepsTheLatestCommandResultForPresentationFeedback()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(
            Acknowledgement: new CommandAck {
                Results = {
                    new CommandResult {
                        ClientSequence = 4,
                        Status = CommandStatus.Accepted,
                        AppliedTick = 22,
                    },
                },
            }));

        Assert.NotNull(model.LastCommandResult);
        Assert.Equal(4UL, model.LastCommandResult!.ClientSequence);
        Assert.Equal(22UL, model.LastCommandResult.AppliedTick);
    }

    [Fact]
    public void ProjectsMultiCellInventoryItemsAndFindsFallbackAnchors()
    {
        var items = new List<ItemSnapshot> {
            new() { State = new ItemStateSnapshot { InventoryWidth = 2, InventoryHeight = 2 } },
            new() { State = new ItemStateSnapshot { InventoryWidth = 1, InventoryHeight = 1 } },
        };
        var layout = InventoryLayout.Build(items, new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 4, 3);

        Assert.Equal(0, layout.Anchors[0]);
        Assert.Equal(2, layout.Anchors[1]);
        Assert.Equal(new[] { 0, 0, 1, -1, 0, 0, -1, -1, -1, -1, -1, -1 }, layout.Occupants);
    }

    [Fact]
    public async Task CountsQueuedCommandsBeforeTransportSend()
    {
        await using var client = new AuthoritativeClient(new ClientConnectionOptions("127.0.0.1", 6113, "test", "0.1.0", "test-hash"));

        Assert.Equal(0, client.PendingCommandCount);
        client.Queue(new Command { RefillManaRequested = new RefillManaRequested() }, 5);
        Assert.Equal(1, client.PendingCommandCount);

        Assert.Equal(1, client.PendingCommandCount);
    }
}

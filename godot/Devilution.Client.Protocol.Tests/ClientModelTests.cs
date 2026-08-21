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
                        new PlayerSpellLevelSnapshot { SpellId = 3, Level = 2, DisplayName = "Lightning" },
                        new PlayerSpellLevelSnapshot { SpellId = 1, Level = 1, DisplayName = "Firebolt" },
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
        Assert.Equal("Firebolt (level 1)", sources[0].DisplayName);
        Assert.Equal(CastSourceKind.ChargedItem, sources[2].Kind);
        Assert.Equal("Charged Bolt (staff, 3 charges)", sources[2].DisplayName);
        Assert.Equal(PlayerItemLocation.Equipment, sources[2].SourceItem!.Location);
        Assert.Equal(4U, sources[2].SourceItem!.Slot);
        Assert.Equal(700U, sources[2].SourceItem!.StableItemId);
        Assert.Equal(CastSourceKind.ConsumableItem, sources[3].Kind);
        Assert.Equal(PlayerItemLocation.Inventory, sources[3].SourceItem!.Location);
        Assert.Equal(701U, sources[3].SourceItem!.StableItemId);
        Assert.Equal(702U, sources[4].SourceItem!.StableItemId);
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
            Objects = { new ObjectSnapshot { EntityId = 10 } },
            Projectiles = { new ProjectileSnapshot { EntityId = 11 } },
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

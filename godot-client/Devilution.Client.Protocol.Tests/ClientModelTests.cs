using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Xunit;
using ProtocolItemPresentationKind = Devilution.Protocol.V1.ItemPresentationKind;

namespace Devilution.Client.Protocol.Tests;

public sealed partial class ClientModelTests
{
    [Theory]
    [InlineData(0U, 1U)]
    [InlineData(1U, 0U)]
    [InlineData(0U, 0U)]
    public void ExplicitNativeInitializationAcceptsExactZeroSeeds(uint itemSeed, uint aiSeed)
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot { Tick = 1, Monsters = { new MonsterSnapshot {
            EntityId = 9, MonsterItemSeed = itemSeed, MonsterAiSeed = aiSeed,
            NativeInitializationPresent = true,
        } } };
        snapshot.Players.Add(new PlayerSnapshot { EntityId = 1 });
        model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot));
        Assert.Equal(itemSeed, model.Snapshot!.Monsters[0].MonsterItemSeed);
        Assert.Equal(aiSeed, model.Snapshot.Monsters[0].MonsterAiSeed);
        Assert.True(model.Snapshot.Monsters[0].NativeInitializationPresent);
        var unmarked = snapshot.Clone();
        unmarked.Tick = 2;
        unmarked.Monsters[0].ClearNativeInitializationPresent();
        if ((itemSeed == 0) != (aiSeed == 0))
        {
            Assert.Throws<InvalidDataException>(() => model.Apply(new AuthoritativeClientMessage(Snapshot: unmarked)));
            Assert.Equal(1UL, model.CurrentTick);
        }
        else
        {
            model.Apply(new AuthoritativeClientMessage(Snapshot: unmarked));
            Assert.Equal(2UL, model.CurrentTick);
            Assert.False(model.Snapshot!.Monsters[0].NativeInitializationPresent);
        }
    }

    [Fact]
    public void ValidatesMalformedStaleSnapshotsBeforeDiscardingThem()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Tick = 5,
            Players = { new PlayerSnapshot { EntityId = 7 } },
        }));

        var stale = new Snapshot {
            Tick = 4,
            StateSha256 = "not-a-canonical-hash",
            Players = { new PlayerSnapshot { EntityId = 7 } },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: stale)));
        Assert.Equal(5UL, model.CurrentTick);
    }

    [Fact]
    public void SelectsAdjacentServerOwnedTownerGreeting()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = { new PlayerSnapshot { EntityId = 7, LevelId = 0, PositionX = 2, PositionY = 3 } },
                LevelGeometry = { new LevelGeometrySnapshot { LevelId = 0, Width = 10, Height = 10 } },
                TownerGreetings = {
                    new TownerGreetingSnapshot { TownerId = 4, LevelId = 0, PositionX = 2, PositionY = 2, MessageId = 150 },
                },
            }));

        var greeting = model.TownerGreetingAt(2, 2);
        Assert.NotNull(greeting);
        Assert.Equal(4, greeting!.TownerId);
        Assert.Equal(150, greeting.MessageId);

        var heldSnapshot = model.Snapshot!.Clone();
        heldSnapshot.Players[0].HeldItem = new ItemSnapshot {
            ItemSeed = 1,
            State = new ItemStateSnapshot { ItemType = 1, ItemIndex = 1 },
        };
        model.Apply(new AuthoritativeClientMessage(Snapshot: heldSnapshot));
        Assert.Null(model.TownerGreetingAt(2, 2));
    }

    [Fact]
    public void RejectsDuplicateTownerGreetingIds()
    {
        var model = new AuthoritativeClientModel();
        Assert.Throws<InvalidDataException>(() => model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = { new PlayerSnapshot { EntityId = 7 } },
                TownerGreetings = {
                    new TownerGreetingSnapshot { TownerId = 4, LevelId = 0, PositionX = 2, PositionY = 2, MessageId = 150 },
                    new TownerGreetingSnapshot { TownerId = 4, LevelId = 0, PositionX = 3, PositionY = 3, MessageId = 151 },
                },
            })));
    }

    [Fact]
    public void ValidatesAuthoritativeStateHashFormatWhileRetainingLegacySnapshots()
    {
        var model = new AuthoritativeClientModel();
        var canonicalHash = new string('A', 64);
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            StateSha256 = canonicalHash,
            Players = { new PlayerSnapshot { EntityId = 7 } },
        }));

        Assert.Equal(canonicalHash, model.Snapshot!.StateSha256);

        foreach (var invalidHash in new[] {
            new string('a', 63),
            new string('a', 65),
            new string('g', 64),
        }) {
            var invalid = new Snapshot {
                StateSha256 = invalidHash,
                Players = { new PlayerSnapshot { EntityId = 7 } },
            };
            Assert.Throws<InvalidDataException>(() =>
                model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
        }

        var legacy = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
        };
        model.Apply(new AuthoritativeClientMessage(Snapshot: legacy));
        Assert.Empty(model.Snapshot!.StateSha256);
    }

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
    public void RejectsMoreWarpTriggersThanTheNativePoolPerLevel()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
            LevelGeometry = {
                new LevelGeometrySnapshot { LevelId = 1, Width = 10, Height = 10 },
            },
        };
        for (var index = 1; index <= 8; index++)
            snapshot.LevelGeometry[0].WarpTriggers.Add(new WarpTriggerSnapshot {
                WarpId = (uint)index,
                TargetLevelId = 1,
                TargetX = 1,
                TargetY = 1,
                Available = true,
            });

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
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

        var maximumNativeGeometry = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 1,
                    Height = 1,
                    NativeTransparency = { 255 },
                    NativeObjectLayer = { 146 },
                    NativeMegaTileLayer = { 255 },
                    NativePieceLayer = { 1378 },
                    NativeMonsterLayer = { 157 },
                },
            },
        };
        model.Apply(new AuthoritativeClientMessage(Snapshot: maximumNativeGeometry));

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

        var signedTransparencyWire = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 1,
                    Height = 1,
                    NativeTransparency = { 255 },
                },
            },
        };
        model.Apply(new AuthoritativeClientMessage(Snapshot: signedTransparencyWire));

        var invalidNativePresentation = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 1,
                    Height = 1,
                    NativeTransparency = { 256 },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidNativePresentation)));

        invalidNativePresentation.LevelGeometry[0].NativeTransparency.Clear();
        invalidNativePresentation.LevelGeometry[0].NativeMegaTileLayer.Add(256);
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidNativePresentation)));

        invalidNativePresentation.LevelGeometry[0].NativeMegaTileLayer.Clear();
        invalidNativePresentation.LevelGeometry[0].NativePieceLayer.Add(1379);
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidNativePresentation)));

        invalidNativePresentation.LevelGeometry[0].NativePieceLayer.Clear();
        invalidNativePresentation.LevelGeometry[0].NativeObjectLayer.Add(147);
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidNativePresentation)));

        invalidNativePresentation.LevelGeometry[0].NativeObjectLayer.Clear();
        invalidNativePresentation.LevelGeometry[0].NativeMonsterLayer.Add(158);
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
                        NativeObjectLayer = { 0, 109, 100, 99 },
                        NativeMegaTileLayer = { 115 },
                        NativePieceLayer = { 0, 297, 300, 299 },
                        NativeMonsterLayer = { 0, 109, 157, 1 },
                    },
                },
            }));

        Assert.Equal(
            [0U, 109U, 100U, 99U],
            Assert.Single(model.Snapshot!.LevelGeometry).NativeObjectLayer);
        Assert.Equal(
            [115U],
            Assert.Single(model.Snapshot.LevelGeometry).NativeMegaTileLayer);
        Assert.Equal(
            [0U, 297U, 300U, 299U],
            Assert.Single(model.Snapshot.LevelGeometry).NativePieceLayer);
        Assert.Equal(
            [0U, 109U, 157U, 1U],
            Assert.Single(model.Snapshot.LevelGeometry).NativeMonsterLayer);
    }

    [Fact]
    public void RejectsRawDunObjectLayerValuesOutsideCompatibilityRange()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 1,
                    Height = 1,
                    NativeObjectLayer = { 147 },
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
    public void EnforcesNativeObjectPoolCapacityPerLevel()
    {
        var model = new AuthoritativeClientModel();
        var atCapacity = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
        };
        for (var index = 0; index < 127; index++) {
            atCapacity.Objects.Add(new ObjectSnapshot {
                EntityId = (uint)(100 + index),
                ObjectId = 4,
                LevelId = 1,
            });
        }

        model.Apply(new AuthoritativeClientMessage(Snapshot: atCapacity));
        Assert.Equal(127, model.Snapshot!.Objects.Count);

        var overCapacity = atCapacity.Clone();
        overCapacity.Objects.Add(new ObjectSnapshot {
            EntityId = 1000,
            ObjectId = 4,
            LevelId = 1,
        });
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: overCapacity)));
    }

    [Fact]
    public void EnforcesNativeWorldItemPoolCapacityPerLevel()
    {
        var model = new AuthoritativeClientModel();
        var atCapacity = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
        };
        for (var index = 0; index < 127; index++) {
            atCapacity.WorldItems.Add(new WorldItemSnapshot {
                EntityId = (uint)(100 + index),
                LevelId = 1,
                PositionX = index,
                ItemSeed = (uint)(index + 1),
                State = new ItemStateSnapshot(),
            });
        }

        model.Apply(new AuthoritativeClientMessage(Snapshot: atCapacity));
        Assert.Equal(127, model.Snapshot!.WorldItems.Count);

        var overCapacity = atCapacity.Clone();
        overCapacity.WorldItems.Add(new WorldItemSnapshot {
            EntityId = 1000,
            LevelId = 1,
            PositionX = 127,
            ItemSeed = 1000,
            State = new ItemStateSnapshot(),
        });
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: overCapacity)));
    }

    [Fact]
    public void EnforcesNativePortalPoolCapacity()
    {
        var model = new AuthoritativeClientModel();
        var atCapacity = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
        };
        for (var index = 1; index <= 4; index++)
            atCapacity.Portals.Add(new PortalSnapshot { PortalId = (uint)index });

        model.Apply(new AuthoritativeClientMessage(Snapshot: atCapacity));
        Assert.Equal(4, model.Snapshot!.Portals.Count);

        var overCapacity = atCapacity.Clone();
        overCapacity.Portals.Add(new PortalSnapshot { PortalId = 5 });
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: overCapacity)));
    }

    [Fact]
    public void EnforcesNativeMonsterPoolCapacityPerLevel()
    {
        var model = new AuthoritativeClientModel();
        var atCapacity = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
        };
        for (var index = 0; index < 200; index++)
            atCapacity.Monsters.Add(new MonsterSnapshot {
                EntityId = (uint)(100 + index),
                LevelId = 1,
            });

        model.Apply(new AuthoritativeClientMessage(Snapshot: atCapacity));
        Assert.Equal(200, model.Snapshot!.Monsters.Count);

        var overCapacity = atCapacity.Clone();
        overCapacity.Monsters.Add(new MonsterSnapshot {
            EntityId = 1000,
            LevelId = 1,
        });
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: overCapacity)));
    }

    [Fact]
    public void RejectsSnapshotsBeyondNativePlayerCapacity()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot();
        for (var index = 1; index <= 5; index++)
            snapshot.Players.Add(new PlayerSnapshot { EntityId = (uint)index });

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
    public void RejectsProjectileTargetOutsideProjectedLevelGeometry()
    {
        var model = new AuthoritativeClientModel();
        var valid = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 10,
                    Height = 10,
                },
            },
            Projectiles = {
                new ProjectileSnapshot {
                    EntityId = 11,
                    SourceEntityId = 7,
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

        model.Apply(new AuthoritativeClientMessage(Snapshot: valid));

        var invalid = valid.Clone();
        invalid.Projectiles[0].TargetY = 10;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
    }

    [Fact]
    public void AcceptsOnlyNativeElementalImpactCenterOneTileBeyondLevelEdge()
    {
        var model = new AuthoritativeClientModel();
        var valid = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 10,
                    Height = 10,
                },
            },
            Projectiles = {
                new ProjectileSnapshot {
                    EntityId = 11,
                    SourceEntityId = 7,
                    SpellId = 31,
                    LevelId = 1,
                    PositionX = 10,
                    PositionY = 4,
                    TargetX = 10,
                    TargetY = 4,
                    RemainingTicks = 1,
                    Behavior = (int)AuthoritativeProjectileBehavior.Elemental,
                    Phase = AuthoritativeProtocolLimits.ElementalProjectileImpactPhase,
                },
            },
        };

        model.Apply(new AuthoritativeClientMessage(Snapshot: valid));

        foreach (var mutate in new Action<Snapshot>[] {
            snapshot => snapshot.Projectiles[0].PositionX = 11,
            snapshot => snapshot.Projectiles[0].Behavior = (int)AuthoritativeProjectileBehavior.Firebolt,
            snapshot => snapshot.Projectiles[0].Phase = 0,
        }) {
            var invalid = valid.Clone();
            mutate(invalid);
            Assert.Throws<InvalidDataException>(() =>
                model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
        }
    }

    [Fact]
    public void AcceptsCellTargetedAttackAndRejectsItsInvalidDestination()
    {
        var model = new AuthoritativeClientModel();
        var valid = new Snapshot {
            Tick = 1,
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 10,
                    Height = 10,
                },
            },
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
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

        model.Apply(new AuthoritativeClientMessage(Snapshot: valid));

        var invalid = valid.Clone();
        invalid.Players[0].CurrentAction.TargetY = 10;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
    }

    [Fact]
    public void AcceptsTileTargetedCastAndRejectsItsInvalidDestination()
    {
        var model = new AuthoritativeClientModel();
        var valid = new Snapshot {
            Tick = 1,
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 10,
                    Height = 10,
                },
            },
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
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

        model.Apply(new AuthoritativeClientMessage(Snapshot: valid));

        var invalid = valid.Clone();
        invalid.Players[0].CurrentAction.Cast.TargetY = 10;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
    }

    [Fact]
    public void RejectsCastSourceReferenceWithoutMatchingSourceKind()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
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

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public void RequiresCastItemSourcesToMatchProjectedOwnership()
    {
        var model = new AuthoritativeClientModel();
        var valid = new Snapshot {
            Tick = 1,
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    CombatMode = PlayerCombatMode.Spell,
                    CombatModeEndTick = 3,
                    Equipment = {
                        new EquippedItemSnapshot {
                            Slot = 4,
                            ItemSeed = 12,
                            State = new ItemStateSnapshot { StableItemId = 42 },
                        },
                    },
                    CurrentAction = new PlayerActionSnapshot {
                        Kind = PlayerActionKind.Cast,
                        StartedAtTick = 1,
                        EffectTick = 1,
                        ReadyTick = 2,
                        EndTick = 3,
                        EffectApplied = true,
                        Cast = new CastRequested {
                            SpellId = 1,
                            Source = CastSourceKind.ChargedItem,
                            SourceItem = new PlayerItemReference {
                                Location = PlayerItemLocation.Equipment,
                                Slot = 4,
                                StableItemId = 42,
                            },
                        },
                    },
                },
            },
        };

        model.Apply(new AuthoritativeClientMessage(Snapshot: valid));

        var invalid = valid.Clone();
        invalid.Players[0].CurrentAction.Cast.SourceItem.StableItemId = 99;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
    }

    [Fact]
    public void RejectsEntityAttackWithActionLevelTargetCoordinates()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    CombatMode = PlayerCombatMode.Attack,
                    CombatModeEndTick = 3,
                    CurrentAction = new PlayerActionSnapshot {
                        Kind = PlayerActionKind.Attack,
                        StartedAtTick = 1,
                        EffectTick = 1,
                        ReadyTick = 2,
                        EndTick = 3,
                        TargetEntityId = 9,
                        TargetX = 1,
                    },
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
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
    public void ItemAbsentQuestTownerStageYieldsToDirectHandInWhenRequiredItemIsCarried()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    LevelId = 0,
                    PositionX = 77,
                    PositionY = 43,
                    VisitedLevelIds = { 0, 17 },
                },
            },
            Quests = {
                new QuestSnapshot {
                    QuestId = 3019,
                    Initial = true,
                },
            },
            QuestTownerInteractions = {
                new QuestTownerInteractionSnapshot {
                    InteractionId = 301901,
                    QuestId = 3019,
                    TownerId = 11,
                    LevelId = 0,
                    PositionX = 77,
                    PositionY = 43,
                    Kind = 0,
                    MessageId = 283,
                    RequiredVisitedLevel1 = 17,
                    RequiredItemId = 2102,
                    RequireItemPresent = false,
                },
                new QuestTownerInteractionSnapshot {
                    InteractionId = 301903,
                    QuestId = 3019,
                    TownerId = 11,
                    LevelId = 0,
                    PositionX = 77,
                    PositionY = 43,
                    ItemId = 2102,
                    Kind = 2,
                    MessageId = 285,
                    ConsumeItem = true,
                    AllowInitial = true,
                },
            },
        };

        Assert.Equal(
            301901U,
            AuthoritativeQuestTownerInteractionSelector.Select(snapshot)!.InteractionId);

        snapshot.Players[0].Inventory.Add(new ItemSnapshot {
            ItemSeed = 42,
            State = new ItemStateSnapshot { StableItemId = 2102 },
        });
        Assert.Equal(
            301903U,
            AuthoritativeQuestTownerInteractionSelector.Select(snapshot)!.InteractionId);
    }

    [Fact]
    public void RejectsExtremeQuestTownerCoordinatesWithoutOverflowing()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    LevelId = 0,
                    PositionX = int.MinValue,
                    PositionY = int.MaxValue,
                },
            },
            Quests = {
                new QuestSnapshot { QuestId = 3002, Initial = true },
            },
            QuestTownerInteractions = {
                new QuestTownerInteractionSnapshot {
                    InteractionId = 300201,
                    QuestId = 3002,
                    TownerId = 6,
                    LevelId = 0,
                    PositionX = int.MaxValue,
                    PositionY = int.MinValue,
                    Kind = 0,
                },
            },
        };

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
    public void RejectsDuplicateLevelGeometryBeforeQuestTownerValidationCanEscape()
    {
        var model = new AuthoritativeClientModel();
        Assert.Throws<InvalidDataException>(() => model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = { new PlayerSnapshot { EntityId = 7 } },
                Quests = { new QuestSnapshot { QuestId = 3002, RequiredProgress = 1 } },
                LevelGeometry = {
                    new LevelGeometrySnapshot { LevelId = 0, Width = 10, Height = 10 },
                    new LevelGeometrySnapshot { LevelId = 0, Width = 10, Height = 10 },
                },
                QuestTownerInteractions = {
                    new QuestTownerInteractionSnapshot {
                        InteractionId = 300201,
                        QuestId = 3002,
                        LevelId = 0,
                        PositionX = 2,
                        PositionY = 2,
                        Kind = 0,
                        MessageId = 129,
                    },
                },
            })));
    }

    [Fact]
    public void ValidatesSetLevelObjectOriginsAgainstProjectedGeometry()
    {
        var accepted = new AuthoritativeClientModel();
        accepted.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = { new PlayerSnapshot { EntityId = 7, LevelId = 17 } },
                LevelGeometry = {
                    new LevelGeometrySnapshot { LevelId = 17, Width = 8, Height = 9 },
                },
                SetLevelObjectOrigins = {
                    new SetLevelObjectOriginSnapshot {
                        SetLevelId = 6,
                        LevelId = 17,
                        PositionX = 7,
                        PositionY = 8,
                    },
                },
            }));

        Assert.Single(accepted.Snapshot!.SetLevelObjectOrigins);

        var rejected = new AuthoritativeClientModel();
        Assert.Throws<InvalidDataException>(() => rejected.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = { new PlayerSnapshot { EntityId = 7, LevelId = 17 } },
                LevelGeometry = {
                    new LevelGeometrySnapshot { LevelId = 17, Width = 8, Height = 9 },
                },
                SetLevelObjectOrigins = {
                    new SetLevelObjectOriginSnapshot {
                        SetLevelId = 6,
                        LevelId = 17,
                        PositionX = 8,
                        PositionY = 8,
                    },
                },
            })));
    }

    [Fact]
    public void RejectsDuplicateMaterializedProceduralLevels()
    {
        var model = new AuthoritativeClientModel();
        Assert.Throws<InvalidDataException>(() => model.Apply(new AuthoritativeClientMessage(
            Snapshot: new Snapshot {
                Players = { new PlayerSnapshot { EntityId = 7 } },
                MaterializedProceduralLevelIds = { 17, 17 },
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
    public void SelectsCompletionAfterQuestInteractionWhenCompletionGateIsPresent()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    LevelId = 0,
                    PositionX = 62,
                    PositionY = 16,
                },
            },
            Quests = {
                new QuestSnapshot {
                    QuestId = 3018,
                    Completed = true,
                    Variable2 = 0,
                },
            },
            QuestTownerInteractions = {
                new QuestTownerInteractionSnapshot {
                    InteractionId = 301802,
                    QuestId = 3018,
                    TownerId = 10,
                    LevelId = 0,
                    PositionX = 62,
                    PositionY = 16,
                    Kind = 4,
                    RequiredVariable2 = 0,
                    Variable2 = 1,
                    MessageId = 280,
                    AllowCompleted = true,
                    UseQuestCompletionReward = true,
                },
            },
        };

        Assert.Equal(
            301802U,
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
    public void RejectsVisitedLevelsOutsideNativeRange()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    LevelId = 4,
                    VisitedLevelIds = { 0, 4, 25 },
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public void RejectsCurrentPlayerLevelOutsideNativeRange()
    {
        var model = new AuthoritativeClientModel();
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
                Players = {
                    new PlayerSnapshot {
                        EntityId = 7,
                        LevelId = 25,
                    },
                },
            })));
    }

    [Fact]
    public void AcceptsMaterializedSetLevelPlayerIdentityWithoutRegularVisitedFlag()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    LevelId = AuthoritativeLevelIdRules.RuntimeSetLevelIdBase + 1,
                },
            },
        }));

        Assert.Equal(
            AuthoritativeLevelIdRules.RuntimeSetLevelIdBase + 1,
            model.Snapshot!.Players[0].LevelId);
    }

    [Fact]
    public void RejectsInvalidAuthoritativeSessionMetadata()
    {
        var model = new AuthoritativeClientModel();
        var valid = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Difficulty = GameDifficulty.Hell,
            ActiveWorldMutationIds = { 10, 20 },
        };
        model.Apply(new AuthoritativeClientMessage(Snapshot: valid));

        foreach (var mutate in new Action<Snapshot>[] {
            snapshot => snapshot.Difficulty = (GameDifficulty)99,
            snapshot => snapshot.ActiveWorldMutationIds[0] = 0,
            snapshot => snapshot.ActiveWorldMutationIds.Add(20),
            snapshot => snapshot.ActiveWorldMutationIds.AddRange(
                Enumerable.Range(21, 4096).Select(value => (uint)value)),
        }) {
            var invalid = valid.Clone();
            mutate(invalid);
            Assert.Throws<InvalidDataException>(() =>
                model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
        }
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

}

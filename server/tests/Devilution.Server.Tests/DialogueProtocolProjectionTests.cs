using Google.Protobuf;
using Devilution.Diablo;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Devilution.Server.Snapshots;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class DialogueProtocolProjectionTests
{
    [Fact]
    public void ProjectsTownerGreetingMetadata()
    {
        var source = CreateOriginSnapshot(
            new DiabloSetLevelObjectOriginState(6, 17, 7, 8)) with {
                TownerGreetings = [
                    new AuthoritativeTownerGreetingDefinition(4, 17, 7, 8, 150),
                ],
            };

        var greeting = Assert.Single(
            DiabloProtocolMapper.ToProtocolSnapshot(source).TownerGreetings);

        Assert.Equal(4, greeting.TownerId);
        Assert.Equal(17U, greeting.LevelId);
        Assert.Equal((7, 8), (greeting.PositionX, greeting.PositionY));
        Assert.Equal(150, greeting.MessageId);
    }

    [Fact]
    public void RejectsDuplicateTownerGreetingMetadata()
    {
        var source = CreateOriginSnapshot(
            new DiabloSetLevelObjectOriginState(6, 17, 7, 8)) with {
                TownerGreetings = [
                    new AuthoritativeTownerGreetingDefinition(4, 17, 7, 8, 150),
                    new AuthoritativeTownerGreetingDefinition(4, 17, 8, 8, 151),
                ],
            };

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source));
    }

    [Fact]
    public void ProjectsPlayerLightRadiusThroughTheProtocolSnapshot()
    {
        var source = CreateOriginSnapshot(
            new DiabloSetLevelObjectOriginState(6, 17, 7, 8));
        source = source with { Player = source.Player with { LightRadius = 14 } };

        var projected = DiabloProtocolMapper.ToProtocolSnapshot(source);

        Assert.Equal(14, Assert.Single(projected.Players).LightRadius);
    }

    [Fact]
    public void RejectsMorePlayersThanTheNativePool()
    {
        var source = CreateOriginSnapshot(
            new DiabloSetLevelObjectOriginState(6, 17, 7, 8));
        var participants = Enumerable.Range(2, 4)
            .Select(entityId => source.Player with { EntityId = (uint)entityId })
            .ToArray();

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source, participants));
        Assert.Contains("native player pool capacity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsPlayerLevelOutsideNativeRange()
    {
        var source = CreateOriginSnapshot(
            new DiabloSetLevelObjectOriginState(6, 17, 7, 8));
        source = source with { Player = source.Player with { LevelId = 25 } };

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source));
    }

    [Fact]
    public void ProjectsNativeMonsterRuntimeSeeds()
    {
        var source = CreateOriginSnapshot(
            new DiabloSetLevelObjectOriginState(6, 17, 7, 8)) with {
                Monsters = [new DiabloMonsterState(
                    9,
                    2,
                    17,
                    1,
                    1,
                    10,
                    10,
                    0,
                    1,
                    6,
                    0,
                    0,
                    0,
                    0) {
                    MonsterItemSeed = 0x12345678,
                    MonsterAiSeed = 0x87654321,
                }],
            };

        var monster = Assert.Single(
            DiabloProtocolMapper.ToProtocolSnapshot(source).Monsters);

        Assert.Equal(0x12345678U, monster.MonsterItemSeed);
        Assert.Equal(0x87654321U, monster.MonsterAiSeed);
    }

    [Fact]
    public void ProjectsAndRestoresMonsterGroupUnityState()
    {
        var source = CreateOriginSnapshot(
            new DiabloSetLevelObjectOriginState(6, 17, 7, 8)) with {
                Monsters = [
                    new DiabloMonsterState(
                        9,
                        2,
                        17,
                        1,
                        1,
                        10,
                        10,
                        0,
                        1,
                        6,
                        0,
                        0,
                        0,
                        0) {
                        PackSize = 1,
                        ActiveForTicks = 6,
                    },
                    new DiabloMonsterState(
                        10,
                        3,
                        17,
                        2,
                        1,
                        10,
                        10,
                        0,
                        1,
                        6,
                        0,
                        0,
                        0,
                        0) {
                        LeaderEntityId = 9,
                        LeaderRelation = AuthoritativeMonsterLeaderRelation.Leashed,
                        ActiveForTicks = 5,
                    },
                ],
            };

        var projected = DiabloProtocolMapper.ToProtocolSnapshot(source);
        var leader = projected.Monsters.Single(monster => monster.EntityId == 9);
        var member = projected.Monsters.Single(monster => monster.EntityId == 10);
        Assert.Equal(1, leader.PackSize);
        Assert.Equal(6, leader.ActiveForTicks);
        Assert.Equal(9U, member.LeaderEntityId);
        Assert.Equal(1, member.LeaderRelation);
        Assert.Equal(5, member.ActiveForTicks);

        var restored = DiabloProtocolMapper.FromProtocolSnapshot(projected);
        var restoredMember = restored.Monsters.Single(monster => monster.EntityId == 10);
        Assert.Equal(9U, restoredMember.LeaderEntityId);
        Assert.Equal(AuthoritativeMonsterLeaderRelation.Leashed, restoredMember.LeaderRelation);
        Assert.Equal(5, restoredMember.ActiveForTicks);
    }

    [Fact]
    public void ProjectsAndRestoresMonsterMovementTiming()
    {
        var source = CreateOriginSnapshot(
            new DiabloSetLevelObjectOriginState(6, 17, 7, 8)) with {
                Monsters = [new DiabloMonsterState(
                    9,
                    2,
                    17,
                    1,
                    1,
                    10,
                    10,
                    0,
                    1,
                    6,
                    0,
                    0,
                    0,
                    0) {
                    Moving = true,
                    FuturePositionX = 2,
                    FuturePositionY = 1,
                    MovementStartedTick = 12,
                    MovementEffectTick = 13,
                }],
            };

        var projected = DiabloProtocolMapper.ToProtocolSnapshot(source);
        var monster = Assert.Single(projected.Monsters);
        Assert.Equal(12UL, monster.MovementStartedTick);
        Assert.Equal(13UL, monster.MovementEffectTick);

        var restored = DiabloProtocolMapper.FromProtocolSnapshot(projected);
        var restoredMonster = Assert.Single(restored.Monsters);
        Assert.Equal(12UL, restoredMonster.MovementStartedTick);
        Assert.Equal(13UL, restoredMonster.MovementEffectTick);
    }

    [Fact]
    public void RejectsMalformedMonsterMovementTiming()
    {
        var source = CreateOriginSnapshot(
            new DiabloSetLevelObjectOriginState(6, 17, 7, 8)) with {
                Monsters = [new DiabloMonsterState(
                    9,
                    2,
                    17,
                    1,
                    1,
                    10,
                    10,
                    0,
                    1,
                    6,
                    0,
                    0,
                    0,
                    0) {
                    Moving = true,
                    FuturePositionX = 2,
                    FuturePositionY = 1,
                    MovementStartedTick = 12,
                    MovementEffectTick = 11,
                }],
            };

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source));
    }

    [Fact]
    public void RejectsInvalidMonsterGroupUnityRelation()
    {
        var source = CreateOriginSnapshot(
            new DiabloSetLevelObjectOriginState(6, 17, 7, 8)) with {
                Monsters = [new DiabloMonsterState(
                    9,
                    2,
                    17,
                    1,
                    1,
                    10,
                    10,
                    0,
                    1,
                    6,
                    0,
                    0,
                    0,
                    0)],
            };
        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(source);
        snapshot.Monsters[0].LeaderRelation = 3;
        snapshot.StateSha256 = SnapshotStateHasher.Compute(snapshot);

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.FromProtocolSnapshot(snapshot));
    }

    [Fact]
    public void ProjectsSetLevelObjectOriginsWithinSuppliedGeometry()
    {
        var source = CreateOriginSnapshot(
            new DiabloSetLevelObjectOriginState(6, 17, 7, 8));

        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(
            source,
            levelGeometry: [new AuthoritativeLevel(17, 8, 9)]);

        var origin = Assert.Single(snapshot.SetLevelObjectOrigins);
        Assert.Equal(6, origin.SetLevelId);
        Assert.Equal(17U, origin.LevelId);
        Assert.Equal((7, 8), (origin.PositionX, origin.PositionY));
    }

    [Fact]
    public void RejectsSetLevelObjectOriginsOutsideSuppliedGeometry()
    {
        var source = CreateOriginSnapshot(
            new DiabloSetLevelObjectOriginState(6, 17, 8, 8));

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(
                source,
                levelGeometry: [new AuthoritativeLevel(17, 8, 9)]));
    }

    [Fact]
    public void RejectsImportedSetLevelObjectOriginsOutsideProtocolGeometry()
    {
        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(
            CreateOriginSnapshot(new DiabloSetLevelObjectOriginState(6, 17, 7, 8)),
            levelGeometry: [new AuthoritativeLevel(17, 8, 9)]);
        snapshot.SetLevelObjectOrigins[0].PositionX = 8;

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.FromProtocolSnapshot(snapshot));
    }

    [Fact]
    public void ItemPresentationUsesContentEarTagBeforeLegacyIdentity()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [new DiabloWorldItemState(
                2,
                0,
                1,
                1,
                7,
                0,
                AuthoritativeItemState.Empty with {
                    StableItemId = 24,
                    ItemType = 11,
                    ItemIndex = 91,
                    MiscId = 99,
                    ItemClass = 0,
                    BaseName = "Ear",
                    IdentifiedName = "Ear",
                    NativeCursorGraphic = 21,
                })],
            [],
            [],
            [],
            null);
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(24, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, false) {
                StableSymbol = "ear",
                GenerationTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                    "Misc",
                    "Ear",
                },
                NativeItemIndex = 23,
                MiscId = 44,
                ItemClass = 3,
            },
        ]);

        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(source, items: items);

        Assert.Equal(
            ItemPresentationKind.Ear,
            Assert.Single(snapshot.WorldItems).State.PresentationKind);
    }

    private static DiabloSimulationSnapshot CreateOriginSnapshot(
        DiabloSetLevelObjectOriginState origin)
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 17,
        };
        return new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null) {
            SetLevelObjectOrigins = [origin],
        };
    }

    [Fact]
    public void ProjectsSpellTargetingMetadataFromAuthoritativeSpellBehavior()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 1,
            AbilitySpellId = 33,
            MemorizedSpells = [new MemorizedSpellState(33, 1)],
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(33, 15, 0, 0, 0, 0) {
                DisplayName = "Telekinesis",
                ProjectileBehavior = AuthoritativeProjectileBehavior.Telekinesis,
            },
        ]);

        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(source, spells: spells);

        Assert.Equal(
            SpellTargetingKind.WorldEntity,
            snapshot.Players[0].AbilityTargetingKind);
        Assert.Equal(
            SpellTargetingKind.WorldEntity,
            Assert.Single(snapshot.Players[0].MemorizedSpells).TargetingKind);
    }

    [Fact]
    public void RejectsPlayerResourcesOutsideTheSharedProtocolRange()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);

        void AssertRejected(Func<StorePlayerSnapshot, StorePlayerSnapshot> mutate)
        {
            Assert.Throws<InvalidDataException>(() =>
                DiabloProtocolMapper.ToProtocolSnapshot(source with {
                    Player = mutate(player),
                }));
        }

        AssertRejected(value => value with { Gold = (uint)int.MaxValue + 1U });
        AssertRejected(value => value with { StashGold = (uint)int.MaxValue + 1U });
        AssertRejected(value => value with { StatPoints = (uint)int.MaxValue + 1U });
        AssertRejected(value => value with { Life = -1 });
        AssertRejected(value => value with { LifeMaximum = -1 });
        AssertRejected(value => value with { Life = 65, LifeMaximum = 64 });
        AssertRejected(value => value with { Mana = -1 });
        AssertRejected(value => value with { ManaMaximum = -1 });
        AssertRejected(value => value with { Mana = 65, ManaMaximum = 64 });
        AssertRejected(value => value with { BaseLifeMaximum = -1 });
        AssertRejected(value => value with { BaseManaMaximum = -1 });
        AssertRejected(value => value with { ReflectCharges = ushort.MaxValue + 1U });
        AssertRejected(value => value with { CharacterLevel = 51 });
    }

    [Fact]
    public void RejectsNegativePlayerAttributesBeforeProjection()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with {
                    Attributes = PlayerAttributesState.Zero with {
                        Dexterity = new PlayerAttributeState(-1, 0),
                    },
                },
            }));
    }

    [Fact]
    public void RejectsInvalidPlayerEnumsBeforeProjection()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);

        void AssertRejected(Func<StorePlayerSnapshot, StorePlayerSnapshot> mutate)
        {
            Assert.Throws<InvalidDataException>(() =>
                DiabloProtocolMapper.ToProtocolSnapshot(source with {
                    Player = mutate(player),
                }));
        }

        AssertRejected(value => value with {
            AbilityKind = (DiabloPlayerAbilityKind)99,
        });
        AssertRejected(value => value with {
            FacingDirection = (DiabloPlayerDirection)99,
        });
        AssertRejected(value => value with {
            CombatMode = (DiabloPlayerCombatMode)99,
        });
    }

    [Fact]
    public void RejectsInvalidPlayerActionBeforeProjection()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
            CombatMode = DiabloPlayerCombatMode.Attack,
            CombatModeEndTick = 5,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with {
                    CurrentAction = new AuthoritativePlayerActionState(
                        DiabloPlayerActionKind.Attack,
                        3,
                        2,
                        4,
                        5) {
                        TargetEntityId = 2,
                    },
                },
            }));

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with {
                    CurrentAction = new AuthoritativePlayerActionState(
                        (DiabloPlayerActionKind)99,
                        1,
                        2,
                        3,
                        5),
                },
            }));
    }

    [Fact]
    public void RejectsInvalidPlayerSpellAndStatusProjections()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with {
                    MemorizedSpells = [new MemorizedSpellState(0, 1)],
                },
            }));
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with {
                    MemorizedSpells = [new MemorizedSpellState(1, 16)],
                },
            }));
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with {
                    StatusEffects = [new AuthoritativeStatusEffect(1, 0, 0)],
                },
            }));
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with {
                    StatusEffects = [new AuthoritativeStatusEffect(1, 1, 1) {
                        Behavior = AuthoritativeStatusEffectBehavior.Etherealize,
                    }],
                },
            }));
    }

    [Fact]
    public void RejectsInvalidVisitedLevelProjections()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 1,
            VisitedLevelIds = [1],
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);

        void AssertRejected(IReadOnlyList<uint> visitedLevelIds)
        {
            Assert.Throws<InvalidDataException>(() =>
                DiabloProtocolMapper.ToProtocolSnapshot(source with {
                    Player = player with { VisitedLevelIds = visitedLevelIds },
                }));
        }

        AssertRejected([1, 1]);
        AssertRejected([2]);
        AssertRejected(Enumerable.Range(0, 257).Select(value => (uint)value).ToArray());
    }

    [Fact]
    public void ProjectsMaterializedSetLevelPlayerIdentityWithoutRegularVisitedFlag()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = AuthoritativeLevelIdRules.RuntimeSetLevelIdBase + 1,
            VisitedLevelIds = [],
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);

        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(source);
        Assert.Equal(AuthoritativeLevelIdRules.RuntimeSetLevelIdBase + 1, snapshot.Players.Single().LevelId);
    }

    [Fact]
    public void RejectsInconsistentStoreAndStashProjections()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with { ActiveStoreId = 12 },
            }));
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                ActiveStore = new DiabloStoreState(12, []),
            }));
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with { StashOpen = true, ActiveStoreId = 12 },
                ActiveStore = new DiabloStoreState(12, []),
            }));
    }

    [Fact]
    public void RejectsDuplicateActiveStoreSlots()
    {
        var player = new StorePlayerSnapshot(
            0,
            12,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            new DiabloStoreState(
                12,
                [
                    new StoreItem(1, 7, 0),
                    new StoreItem(1, 8, 0),
                ]));

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source));
    }

    [Fact]
    public void RejectsInvalidCharacterNameBeforeProjection()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with { CharacterName = "Invalid Name" },
            }));
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with { CharacterName = new string('A', 32) },
            }));
    }

    [Fact]
    public void RejectsInvalidItemStateBeforeProjection()
    {
        var invalidItem = new OwnedStoreItem(
            0,
            0,
            7,
            0,
            0,
            AuthoritativeItemState.Empty with { Value = -1 });
        var player = new StorePlayerSnapshot(
            0,
            null,
            [invalidItem],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source));

        var partialPresentation = invalidItem with {
            State = AuthoritativeItemState.Empty with { BaseName = "Potion" },
        };
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with { Inventory = [partialPresentation] },
            }));

        var missingCursor = invalidItem with {
            State = AuthoritativeItemState.Empty with {
                BaseName = "Potion",
                IdentifiedName = "Potion",
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with { Inventory = [missingCursor] },
            }));

        var unrepresentableCreateInfo = invalidItem with {
            State = AuthoritativeItemState.Empty with {
                CreateInfo = ushort.MaxValue + 1U,
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with { Inventory = [unrepresentableCreateInfo] },
            }));

        var unrepresentableBonus = invalidItem with {
            State = AuthoritativeItemState.Empty with {
                PlusDamage = short.MaxValue + 1,
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with { Inventory = [unrepresentableBonus] },
            }));

        var invalidCursor = invalidItem with {
            State = AuthoritativeItemState.Empty with {
                NativeCursorGraphic = -1,
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with { Inventory = [invalidCursor] },
            }));
    }

    [Fact]
    public void RejectsUnrepresentableProjectedItemPrices()
    {
        const uint unrepresentablePrice = (uint)int.MaxValue + 1U;
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);

        var pricedItem = new OwnedStoreItem(0, 0, 7, unrepresentablePrice, 0);
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with { Inventory = [pricedItem] },
            }));

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with { ActiveStoreId = 12 },
                ActiveStore = new DiabloStoreState(
                    12,
                    [new StoreItem(0, 7, unrepresentablePrice)]),
            }));

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                WorldItems = [new DiabloWorldItemState(
                    2,
                    0,
                    0,
                    0,
                    7,
                    unrepresentablePrice,
                    AuthoritativeItemState.Empty)],
            }));
    }

    [Fact]
    public void RejectsWorldItemsOutsideProjectedPositionBounds()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);

        void AssertRejected(DiabloWorldItemState item, IReadOnlyList<AuthoritativeLevel>? levels = null)
        {
            Assert.Throws<InvalidDataException>(() =>
                DiabloProtocolMapper.ToProtocolSnapshot(
                    source with { WorldItems = [item] },
                    levelGeometry: levels));
        }

        var validItem = new DiabloWorldItemState(
            2,
            1,
            4,
            5,
            7,
            0,
            AuthoritativeItemState.Empty);
        AssertRejected(validItem with { PositionX = -1 });
        AssertRejected(validItem with { PositionY = byte.MaxValue + 1 });
        AssertRejected(
            validItem with { PositionX = 10 },
            [new AuthoritativeLevel(1, 10, 10)]);
    }

    [Fact]
    public void EnforcesNativeWorldItemPoolCapacityPerLevel()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);
        var atCapacity = source with {
            WorldItems = Enumerable.Range(0, AuthoritativeLevel.NativeMaximumItemCount)
                .Select(index => new DiabloWorldItemState(
                    (uint)(100 + index),
                    1,
                    index,
                    0,
                    (uint)(index + 1),
                    0,
                    AuthoritativeItemState.Empty))
                .ToArray(),
        };

        var projected = DiabloProtocolMapper.ToProtocolSnapshot(atCapacity);
        Assert.Equal(AuthoritativeLevel.NativeMaximumItemCount, projected.WorldItems.Count);

        var overCapacity = atCapacity with {
            WorldItems = atCapacity.WorldItems
                .Append(new DiabloWorldItemState(
                    1000,
                    1,
                    AuthoritativeLevel.NativeMaximumItemCount,
                    0,
                    1000,
                    0,
                    AuthoritativeItemState.Empty))
                .ToArray(),
        };
        var exception = Assert.Throws<InvalidDataException>(
            () => DiabloProtocolMapper.ToProtocolSnapshot(overCapacity));
        Assert.Contains("127-item capacity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnforcesNativePortalPoolCapacity()
    {
        var player = new StorePlayerSnapshot(
            0, null, [], 0, 64, 0, 0, PlayerAttributesState.Zero, [], [], []) {
            EntityId = 1,
        };
        var source = new DiabloSimulationSnapshot(
            1, player, [], [], [], [], [], null) with {
            Portals = Enumerable.Range(1, AuthoritativePortalRules.NativeMaximumPortalCount)
                .Select(index => new DiabloPortalState(
                    (uint)index, 1, index, 1, 2, index, 2))
                .ToArray(),
        };

        var projected = DiabloProtocolMapper.ToProtocolSnapshot(source);
        Assert.Equal(AuthoritativePortalRules.NativeMaximumPortalCount, projected.Portals.Count);

        var overCapacity = source with {
            Portals = source.Portals
                .Append(new DiabloPortalState(99, 1, 1, 1, 2, 1, 2))
                .ToArray(),
        };
        var exception = Assert.Throws<InvalidDataException>(
            () => DiabloProtocolMapper.ToProtocolSnapshot(overCapacity));
        Assert.Contains("4-portal capacity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectsAuthoredMonsterDialogueState()
    {
        var source = CreateOriginSnapshot(
            new DiabloSetLevelObjectOriginState(6, 17, 7, 8)) with {
                Monsters = [new DiabloMonsterState(
                    9,
                    2,
                    17,
                    1,
                    1,
                    10,
                    10,
                    0,
                    1,
                    2,
                    0,
                    0,
                    0) {
                    IsUnique = true,
                    UniqueIdentity = "Warlord",
                    DialogueMessageId = 110,
                    DialogueState = AuthoritativeMonsterDialogueState.Inquiring,
                }],
            };

        var projected = DiabloProtocolMapper.ToProtocolSnapshot(source);
        var monster = Assert.Single(projected.Monsters);
        Assert.Equal(MonsterDialogueState.Inquiring, monster.DialogueState);

        var restored = DiabloProtocolMapper.FromProtocolSnapshot(projected);
        Assert.Equal(
            AuthoritativeMonsterDialogueState.Inquiring,
            Assert.Single(restored.Monsters).DialogueState);
    }

    [Fact]
    public void EnforcesNativeMonsterPoolCapacityPerLevel()
    {
        var source = CreateOriginSnapshot(
            new DiabloSetLevelObjectOriginState(6, 17, 7, 8)) with {
                Monsters = Enumerable.Range(0, AuthoritativeLevel.NativeMaximumMonsterCount)
                    .Select(index => new DiabloMonsterState(
                        (uint)(100 + index),
                        1,
                        1,
                        1,
                        1,
                        1,
                        1,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0))
                    .ToArray(),
            };

        var projected = DiabloProtocolMapper.ToProtocolSnapshot(source);
        Assert.Equal(AuthoritativeLevel.NativeMaximumMonsterCount, projected.Monsters.Count);

        var overCapacity = source with {
            Monsters = source.Monsters
                .Append(source.Monsters[^1] with { EntityId = 999 })
                .ToArray(),
        };
        var exception = Assert.Throws<InvalidDataException>(
            () => DiabloProtocolMapper.ToProtocolSnapshot(overCapacity));
        Assert.Contains("native monster capacity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsObjectsOutsideProjectedPositionBounds()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);

        void AssertRejected(DiabloObjectState @object, IReadOnlyList<AuthoritativeLevel>? levels = null)
        {
            Assert.Throws<InvalidDataException>(() =>
                DiabloProtocolMapper.ToProtocolSnapshot(
                    source with { Objects = [@object] },
                    levelGeometry: levels));
        }

        var validObject = new DiabloObjectState(
            2,
            4,
            1,
            4,
            5,
            false,
            0,
            AuthoritativeObjectEffectKind.None,
            0);
        AssertRejected(validObject with { PositionX = -1 });
        AssertRejected(validObject with { PositionY = byte.MaxValue + 1 });
        AssertRejected(
            validObject with { PositionY = 10 },
            [new AuthoritativeLevel(1, 10, 10)]);

        var atCapacity = source with {
            Objects = Enumerable.Range(0, AuthoritativeLevel.NativeMaximumObjectCount)
                .Select(index => new DiabloObjectState(
                    (uint)(100 + index),
                    4,
                    1,
                    index,
                    0,
                    false,
                    0,
                    AuthoritativeObjectEffectKind.None,
                    0))
                .ToArray(),
        };
        Assert.Equal(
            AuthoritativeLevel.NativeMaximumObjectCount,
            DiabloProtocolMapper.ToProtocolSnapshot(atCapacity).Objects.Count);

        var overCapacity = atCapacity with {
            Objects = atCapacity.Objects
                .Append(new DiabloObjectState(
                    1000,
                    4,
                    1,
                    127,
                    0,
                    false,
                    0,
                    AuthoritativeObjectEffectKind.None,
                    0))
                .ToArray(),
        };
        var capacityException = Assert.Throws<InvalidDataException>(
            () => DiabloProtocolMapper.ToProtocolSnapshot(overCapacity));
        Assert.Contains("127-object capacity", capacityException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsInvalidProjectedProjectileState()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);
        var projectile = new DiabloProjectileState(
            2,
            1,
            0,
            2,
            1,
            4,
            5,
            6,
            5,
            1,
            AuthoritativeDamageType.Physical,
            0,
            1);

        void AssertRejected(
            Func<DiabloProjectileState, DiabloProjectileState> mutate,
            IReadOnlyList<AuthoritativeLevel>? levels = null)
        {
            Assert.Throws<InvalidDataException>(() =>
                DiabloProtocolMapper.ToProtocolSnapshot(
                    source with { Projectiles = [mutate(projectile)] },
                    levelGeometry: levels));
        }

        AssertRejected(value => value with { SourceEntityId = 0 });
        AssertRejected(value => value with { SpellId = 0 });
        AssertRejected(value => value with { RemainingTicks = 0 });
        AssertRejected(value => value with { Damage = -1 });
        AssertRejected(value => value with { AreaRadius = -1 });
        AssertRejected(value => value with { DamageMinimum = -1 });
        AssertRejected(value => value with { DamageMaximum = -1 });
        AssertRejected(value => value with { DamageQ6 = -1 });
        AssertRejected(value => value with { ElementalFireDamage = -1 });
        AssertRejected(value => value with { ElementalLightningDamage = -1 });
        AssertRejected(value => value with { PerilSelfDamageQ6 = -1 });
        AssertRejected(value => value with { EffectiveSpellLevel = -1 });
        AssertRejected(value => value with { DamageType = (AuthoritativeDamageType)99 });
        AssertRejected(value => value with { Behavior = (AuthoritativeProjectileBehavior)99 });
        AssertRejected(value => value with { Phase = (AuthoritativeProjectilePhase)99 });
        AssertRejected(value => value with {
            Behavior = AuthoritativeProjectileBehavior.Firebolt,
            OriginalDirection = 8,
        });
        AssertRejected(value => value with { PositionX = -1 });
        AssertRejected(
            value => value with { PositionY = 10 },
            [new AuthoritativeLevel(1, 10, 10)]);
    }

    [Fact]
    public void RejectsRemainingProjectedEntityPositionsOutsideBounds()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 1,
            PositionX = 4,
            PositionY = 5,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);
        var levels = new[] { new AuthoritativeLevel(1, 10, 10) };

        void AssertRejected(DiabloSimulationSnapshot value)
        {
            Assert.Throws<InvalidDataException>(() =>
                DiabloProtocolMapper.ToProtocolSnapshot(value, levelGeometry: levels));
        }

        AssertRejected(source with {
            Player = player with { PositionX = -1 },
        });

        var monster = new DiabloMonsterState(
            2,
            4,
            1,
            4,
            5,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            0) {
            Moving = true,
            FuturePositionX = 10,
            FuturePositionY = 5,
        };
        AssertRejected(source with { Monsters = [monster] });

        AssertRejected(source with {
            Corpses = [new DiabloCorpseState(3, 1, 4, 10)],
        });

        AssertRejected(source with {
            Portals = [new DiabloPortalState(4, 1, 4, 5, 1, 10, 5)],
        });
    }

    [Fact]
    public void RejectsInvalidProjectedMonsterState()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 1,
            PositionX = 4,
            PositionY = 5,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);
        var monster = new DiabloMonsterState(
            2,
            4,
            1,
            4,
            5,
            10,
            20,
            0,
            1,
            2,
            0,
            0,
            0) {
            FuturePositionX = 4,
            FuturePositionY = 5,
            MinDamage = 1,
            MaxDamage = 2,
            MinDamageSpecial = 1,
            MaxDamageSpecial = 2,
            DeathAnimationTicks = 4,
            ToHit = 1,
            ToHitSpecial = 1,
            Intelligence = 1,
        };

        void AssertRejected(Func<DiabloMonsterState, DiabloMonsterState> mutate)
        {
            Assert.Throws<InvalidDataException>(() =>
                DiabloProtocolMapper.ToProtocolSnapshot(source with {
                    Monsters = [mutate(monster)],
                }));
        }

        AssertRejected(value => value with { HitPoints = -1 });
        AssertRejected(value => value with { HitPoints = 21 });
        AssertRejected(value => value with { HitPointsQ6 = -1 });
        AssertRejected(value => value with { MaxHitPointsQ6 = 1, HitPointsQ6 = 2 });
        AssertRejected(value => value with { DialogueMessageId = 1 });
        AssertRejected(value => value with {
            DialogueState = AuthoritativeMonsterDialogueState.Combat,
        });
        AssertRejected(value => value with { AttackDamage = -1 });
        AssertRejected(value => value with { MaxDamage = 0 });
        AssertRejected(value => value with { MinDamageSpecial = 3, MaxDamageSpecial = 2 });
        AssertRejected(value => value with { AggroRange = -1 });
        AssertRejected(value => value with { MonsterLevel = -1 });
        AssertRejected(value => value with { DeathAnimationTicks = 1025 });
        AssertRejected(value => value with { DeathAnimationTicks = 1, DeathTicksRemaining = 2 });
        AssertRejected(value => value with { HitPointsQ6 = 1, DeathTicksRemaining = 1 });
        AssertRejected(value => value with { ExperienceReward = 1_000_001 });
        AssertRejected(value => value with { UniqueIdentity = new string('x', 129) });
        AssertRejected(value => value with { UniqueIdentity = new string('é', 65) });
        AssertRejected(value => value with { UniqueIdentity = "not-unique" });
        AssertRejected(value => value with { ToHit = 256 });
        AssertRejected(value => value with { ToHitSpecial = 256 });
        AssertRejected(value => value with { Intelligence = 256 });
        AssertRejected(value => value with { Direction = 8 });
        AssertRejected(value => value with { FireResistance = 101 });
        AssertRejected(value => value with { LightningResistance = -101 });
        AssertRejected(value => value with { MagicResistance = 101 });
        AssertRejected(value => value with { AcidResistance = -101 });
        AssertRejected(value => value with { OwnerPlayerId = 7 });
        AssertRejected(value => value with { Moving = true });
        AssertRejected(value => value with { FuturePositionX = 4, FuturePositionY = 6 });
    }

    [Fact]
    public void RejectsInvalidProjectedObjectState()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);
        var @object = new DiabloObjectState(
            2,
            4,
            0,
            4,
            5,
            false,
            0,
            AuthoritativeObjectEffectKind.None,
            0);

        void AssertRejected(Func<DiabloObjectState, DiabloObjectState> mutate)
        {
            Assert.Throws<InvalidDataException>(() =>
                DiabloProtocolMapper.ToProtocolSnapshot(source with {
                    Objects = [mutate(@object)],
                }));
        }

        AssertRejected(value => value with { ObjectId = 0 });
        AssertRejected(value => value with { EffectAmount = -1 });
        AssertRejected(value => value with { EffectKind = (AuthoritativeObjectEffectKind)99 });
        AssertRejected(value => value with { QuestId = 1 });
        AssertRejected(value => value with {
            QuestTransitionKind = AuthoritativeObjectQuestTransitionKind.ActivateQuest,
        });
        AssertRejected(value => value with { QuestTransitionAmount = 256 });
        AssertRejected(value => value with { QuestTransitionRequiredVariable1 = -2 });
        AssertRejected(value => value with { RewardKind = (AuthoritativeObjectRewardKind)99 });
        AssertRejected(value => value with { RewardItemLevel = 256 });
        AssertRejected(value => value with { RewardOffsetX = 17 });
        AssertRejected(value => value with {
            RewardKind = AuthoritativeObjectRewardKind.NormalItem,
        });
        AssertRejected(value => value with { ProjectileTargetOffsetX = 33 });
        AssertRejected(value => value with { ProjectileTargetOffsetY = 1 });
        AssertRejected(value => value with {
            QuestSequenceFollowUpKind = (AuthoritativeQuestSequenceFollowUpKind)99,
        });
        AssertRejected(value => value with { QuestSequenceFollowUpOffsetX = -33 });
        AssertRejected(value => value with {
            QuestSequenceFollowUpKind = AuthoritativeQuestSequenceFollowUpKind.WorldMutation,
            QuestSequenceFollowUpAmount = 1,
        });

        var transitionFollowUp = @object with {
            QuestId = 1,
            QuestTransitionKind = AuthoritativeObjectQuestTransitionKind.CompleteAndSetVariable1,
            QuestTransitionAmount = 1,
            QuestSequenceFollowUpKind = AuthoritativeQuestSequenceFollowUpKind.WorldMutation,
            QuestSequenceFollowUpAmount = 1,
        };
        var transitionSnapshot = DiabloProtocolMapper.ToProtocolSnapshot(source with {
            Objects = [transitionFollowUp],
            Quests = [new DiabloQuestState(1, 1, 0, 2, false)],
        });
        Assert.Equal(
            (int)AuthoritativeQuestSequenceFollowUpKind.WorldMutation,
            transitionSnapshot.Objects.Single().QuestSequenceFollowUpKind);
        AssertRejected(value => value with { ProjectileSourceKind = (AuthoritativeObjectProjectileSourceKind)99 });
        AssertRejected(value => value with {
            ProjectileSourceKind = AuthoritativeObjectProjectileSourceKind.ActivatingPlayer,
        });
        AssertRejected(value => value with {
            NativeObjectId = (uint)(AuthoritativeLevel.NativeMaximumObjectId + 1),
        });
        AssertRejected(value => value with {
            Breakable = true,
            MissilePassable = true,
        });
        AssertRejected(value => value with { Destroyed = true });
    }

    [Fact]
    public void RejectsInvalidProjectedQuestAndPortalState()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);
        var quest = new DiabloQuestState(1, 1, 0, 2, false);
        var portal = new DiabloPortalState(1, 1, 0, 0, 2, 1, 1);

        void AssertQuestRejected(Func<DiabloQuestState, DiabloQuestState> mutate)
        {
            Assert.Throws<InvalidDataException>(() =>
                DiabloProtocolMapper.ToProtocolSnapshot(source with {
                    Quests = [mutate(quest)],
                }));
        }

        AssertQuestRejected(value => value with { QuestId = 0 });
        AssertQuestRejected(value => value with { NativeQuestId = AuthoritativeQuestTypeCatalog.MaximumNativeQuestId + 1 });
        AssertQuestRejected(value => value with { RequiredProgress = 0 });
        AssertQuestRejected(value => value with { Progress = 3 });
        AssertQuestRejected(value => value with { Unavailable = true, Progress = 1 });
        AssertQuestRejected(value => value with { Variable1 = 256 });
        AssertQuestRejected(value => value with { Variable2 = -1 });
        AssertQuestRejected(value => value with { MessageId = short.MaxValue + 1 });
        AssertQuestRejected(value => value with { TurnInTownerId = -2 });
        AssertQuestRejected(value => value with { TurnInAlternateMessageId = short.MaxValue + 1 });
        AssertQuestRejected(value => value with { TurnInCompletionVariable2 = -2 });
        AssertQuestRejected(value => value with { TurnInCompletionRequiredVariable1 = 256 });
        AssertQuestRejected(value => value with { TurnInCompletionVariable1 = -2 });
        AssertQuestRejected(value => value with { TurnInRequiredVariable1 = 256 });
        AssertQuestRejected(value => value with { TurnInVariable1 = -2 });
        AssertQuestRejected(value => value with {
            CompletionUniqueIdentity = new string('x', 129),
        });
        AssertQuestRejected(value => value with {
            CompletionUniqueIdentity = new string('é', 65),
        });
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Quests = [quest, quest],
            }));
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Quests = [
                    quest with { QuestId = 1, NativeQuestId = 0 },
                    quest with { QuestId = 2, NativeQuestId = 0 },
                ],
            }));

        void AssertPortalRejected(Func<DiabloPortalState, DiabloPortalState> mutate)
        {
            Assert.Throws<InvalidDataException>(() =>
                DiabloProtocolMapper.ToProtocolSnapshot(source with {
                    Portals = [mutate(portal)],
                }));
        }

        AssertPortalRejected(value => value with { PortalId = 0 });
        AssertPortalRejected(value => value with { Bidirectional = true });
        AssertPortalRejected(value => value with {
            Bidirectional = true,
            OwnerEntityId = 2,
        });
        AssertPortalRejected(value => value with { OwnerEntityId = 2 });
        AssertPortalRejected(value => value with { RemainingTicks = 1 });
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Portals = [portal, portal],
            }));
    }

    [Fact]
    public void RejectsInvalidProjectedWarpTargets()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 1,
            PositionX = 4,
            PositionY = 5,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);
        var levels = new[] {
            new AuthoritativeLevel(1, 10, 10),
            new AuthoritativeLevel(2, 10, 10),
        };
        var validWarp = new AuthoritativeWarpTarget(1, 1, 4, 5, 2, 1, 1) {
            StableSymbol = "stairs",
        };

        void AssertRejected(AuthoritativeWarpTarget warp)
        {
            Assert.Throws<InvalidDataException>(() =>
                DiabloProtocolMapper.ToProtocolSnapshot(
                    source,
                    levelGeometry: levels,
                    warpTargets: [warp]));
        }

        AssertRejected(new AuthoritativeWarpTarget(2, 1, -1, 5, 2, 1, 1));
        AssertRejected(new AuthoritativeWarpTarget(2, 1, 10, 5, 2, 1, 1));
        AssertRejected(new AuthoritativeWarpTarget(2, 1, 4, 5, 2, 10, 1));
        AssertRejected(new AuthoritativeWarpTarget(2, 1, 4, 5, 99, 1, 1));
        AssertRejected(new AuthoritativeWarpTarget(2, 99, 4, 5, 2, 1, 1));
        AssertRejected(new AuthoritativeWarpTarget(2, 1, 4, 5, 2, 1, 1, 99));
        AssertRejected(new AuthoritativeWarpTarget(2, 1, 4, 5, 2, 1, 1) {
            StableSymbol = "bad\u0001symbol",
        });
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(
                source,
                levelGeometry: levels,
                warpTargets: [validWarp, validWarp]));
    }

    [Fact]
    public void RejectsInvalidProjectedQuestTownerInteractions()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 1,
            PositionX = 4,
            PositionY = 5,
        };
        var quest = new DiabloQuestState(1, 1, 0, 2, false);
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [ ],
            null) with {
                Quests = [quest],
            };
        var levels = new[] { new AuthoritativeLevel(1, 10, 10) };
        var interaction = new AuthoritativeQuestTownerInteractionDefinition(
            1,
            1,
            2,
            1,
            4,
            5,
            7,
            AuthoritativeQuestTownerInteractionKind.Activate,
            MessageId: 10);

        var completionInteraction = interaction with {
            Kind = AuthoritativeQuestTownerInteractionKind.CompleteAfterQuest,
            AllowCompleted = true,
            UseQuestCompletionReward = true,
        };
        var projectedCompletion = DiabloProtocolMapper.ToProtocolSnapshot(
            source with { QuestTownerInteractions = [completionInteraction] },
            levelGeometry: levels);
        Assert.Equal(4, projectedCompletion.QuestTownerInteractions.Single().Kind);
        Assert.True(projectedCompletion.QuestTownerInteractions.Single().UseQuestCompletionReward);

        void AssertRejected(
            Func<AuthoritativeQuestTownerInteractionDefinition,
                AuthoritativeQuestTownerInteractionDefinition> mutate)
        {
            Assert.Throws<InvalidDataException>(() =>
                DiabloProtocolMapper.ToProtocolSnapshot(source with {
                    QuestTownerInteractions = [mutate(interaction)],
                }, levelGeometry: levels));
        }

        AssertRejected(value => value with { InteractionId = 0 });
        AssertRejected(value => value with { QuestId = 99 });
        AssertRejected(value => value with { LevelId = 99 });
        AssertRejected(value => value with { PositionX = 10 });
        AssertRejected(value => value with { PositionX = -1 });
        AssertRejected(value => value with { TownerId = -1 });
        AssertRejected(value => value with { TownerId = 256 });
        AssertRejected(value => value with { LevelId = ushort.MaxValue + 1U });
        AssertRejected(value => value with { RequiredVisitedLevel1 = ushort.MaxValue + 1U });
        AssertRejected(value => value with {
            Kind = (AuthoritativeQuestTownerInteractionKind)99,
        });
        AssertRejected(value => value with { MessageId = 0 });
        AssertRejected(value => value with { MessageId = short.MaxValue + 1 });
        AssertRejected(value => value with { RequiredVariable1 = -2 });
        AssertRejected(value => value with { RequiredVariable2 = 256 });
        AssertRejected(value => value with { Variable1 = -2 });
        AssertRejected(value => value with { Variable2 = 256 });
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                QuestTownerInteractions = [interaction, interaction],
            }, levelGeometry: levels));
    }

    [Fact]
    public void RejectsInvalidProjectedWorldMutationIdentities()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);

        void AssertRejected(IReadOnlyList<uint> mutationIds)
        {
            Assert.Throws<InvalidDataException>(() =>
                DiabloProtocolMapper.ToProtocolSnapshot(source with {
                    ActiveWorldMutationIds = mutationIds,
                }));
        }

        AssertRejected([0]);
        AssertRejected([7, 7]);
        AssertRejected(Enumerable.Range(1, DiabloWorldState.MaximumWorldStateEntries + 1)
            .Select(value => (uint)value)
            .ToArray());
    }

    [Fact]
    public void RejectsInvalidProjectedCombatModeTiming()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with {
                    CombatModeEndTick = 1,
                },
            }));
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with {
                    CombatMode = DiabloPlayerCombatMode.Block,
                },
            }));
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with {
                    CombatMode = DiabloPlayerCombatMode.GotHit,
                },
            }));
    }

    [Fact]
    public void RejectsNullProjectedInventoryAndStashEntries()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with {
                    Inventory = [null!],
                },
            }));
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with {
                    Stash = [null!],
                },
            }));
    }

    [Fact]
    public void RejectsSeedlessItemWithoutPresentationIdentity()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [new OwnedStoreItem(
                0,
                0,
                0,
                0,
                0,
                AuthoritativeItemState.Empty)],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source));
    }

    [Fact]
    public void RejectsInvalidProjectedPlayerItemTopology()
    {
        var item = new OwnedStoreItem(0, 0, 7, 0, 0, AuthoritativeItemState.Empty);
        var player = new StorePlayerSnapshot(
            0,
            null,
            [item],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [],
            null);

        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with {
                    Belt = [new BeltStoreItem(0, 7, AuthoritativeItemState.Empty)],
                },
            }));
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with {
                    Equipment = [new EquippedStoreItem(7, 8, AuthoritativeItemState.Empty)],
                },
            }));
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with { InventoryGrid = [0, 1] },
            }));

        var malformedGrid = Enumerable.Repeat(-1, 40).ToArray();
        malformedGrid[0] = 0;
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with {
                    Inventory = [item with {
                        State = AuthoritativeItemState.Empty with { InventoryWidth = 2 },
                    }],
                    InventoryGrid = malformedGrid,
                },
            }));

        var stashItemState = AuthoritativeItemState.Empty with { InventoryWidth = 2 };
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with {
                    Stash = [
                        new StashStoreItem(0, 0, 0, 0, 8, 0, 0, stashItemState),
                        new StashStoreItem(0, 1, 0, 0, 9, 0, 0, stashItemState),
                    ],
                },
            }));
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(source with {
                Player = player with {
                    Stash = [new StashStoreItem(
                        DiabloStashLayout.PageCount,
                        0,
                        0,
                        0,
                        8,
                        0,
                        0,
                        AuthoritativeItemState.Empty)],
                },
            }));
    }

    [Fact]
    public void ProjectsAuthoredWarpTriggersWithQuestAvailability()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 0,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [new DiabloQuestState(3017, 0, 0, 1, true) { NativeQuestId = 16 }],
            [],
            null);
        var warp = new AuthoritativeWarpTarget(
            2,
            0,
            3,
            4,
            1,
            1,
            1,
            requiredQuestId: 3017,
            requiredQuestCompleted: true) {
            StableSymbol = "grave_matters_dungeon",
        };

        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(
            source,
            levelGeometry: [
                new AuthoritativeLevel(0, 10, 10),
                new AuthoritativeLevel(1, 10, 10),
            ],
            warpTargets: [warp]);

        var trigger = Assert.Single(
            snapshot.LevelGeometry.Single(level => level.LevelId == 0).WarpTriggers);
        Assert.Equal(2U, trigger.WarpId);
        Assert.Equal((3, 4), (trigger.TriggerX, trigger.TriggerY));
        Assert.Equal((1U, 1, 1), (trigger.TargetLevelId, trigger.TargetX, trigger.TargetY));
        Assert.Equal(3017U, trigger.RequiredQuestId);
        Assert.True(trigger.RequiredQuestCompleted);
        Assert.True(trigger.Available);
        Assert.Equal("grave_matters_dungeon", trigger.StableSymbol);
        Assert.Equal(16U, Assert.Single(snapshot.Quests).NativeQuestId);
    }

    [Fact]
    public void RejectsMoreWarpTriggersThanTheNativePoolPerLevel()
    {
        var source = CreateOriginSnapshot(
            new DiabloSetLevelObjectOriginState(6, 17, 7, 8));
        var warpTargets = Enumerable.Range(1, 8)
            .Select(index => new AuthoritativeWarpTarget(
                (uint)index,
                0,
                index - 1,
                0,
                1,
                1,
                1))
            .ToArray();

        var exception = Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(
                source,
                levelGeometry: [
                    new AuthoritativeLevel(0, 10, 10),
                    new AuthoritativeLevel(1, 10, 10),
                ],
                warpTargets: warpTargets));
        Assert.Contains("native trigger capacity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OmitsNativeQuestOrdinalWhenNoContentMappingExists()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) { EntityId = 1 };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [new DiabloQuestState(3017, 0, 0, 1, false)],
            [],
            null);

        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(source);

        Assert.False(Assert.Single(snapshot.Quests).HasNativeQuestId);
    }

    [Fact]
    public void ProjectsServerOwnedTextAndPreservesNumericIdentity()
    {
        var catalog = new AuthoritativeDialogueMessageCatalog([
            new(110, "TEXT_WARLRD9", "My blade sings."),
        ]);
        var batch = DiabloProtocolMapper.ToProtocolEventBatch([
            new DiabloGameplayEvent(
                DiabloGameplayEventKind.Dialogue,
                12,
                34,
                0) {
                QuestId = 3012,
                MessageId = 110,
            },
        ], 12, 7, catalog);

        Assert.NotNull(batch);
        var dialogue = Assert.Single(batch!.Events).Dialogue;
        Assert.Equal(3012U, dialogue.QuestId);
        Assert.Equal(110, dialogue.MessageId);
        Assert.Equal("My blade sings.", dialogue.Text);
    }

    [Fact]
    public void OmittingCatalogKeepsIdOnlyCompatibility()
    {
        var batch = DiabloProtocolMapper.ToProtocolEventBatch([
            new DiabloGameplayEvent(
                DiabloGameplayEventKind.Dialogue,
                0,
                34,
                0) {
                MessageId = 21,
            },
        ], 1, 1);

        Assert.NotNull(batch);
        Assert.Equal(string.Empty, Assert.Single(batch!.Events).Dialogue.Text);
    }

    [Fact]
    public void RejectsVictoryRankOutsideNativeRange()
    {
        Assert.Throws<InvalidDataException>(() => DiabloProtocolMapper.ToProtocolEventBatch([
            new DiabloGameplayEvent(
                DiabloGameplayEventKind.Victory,
                1,
                34,
                byte.MaxValue + 1),
        ], 1, 7));
    }

    [Fact]
    public void ProjectsCathedralMapOpenToTheReceivingPlayer()
    {
        var batch = DiabloProtocolMapper.ToProtocolEventBatch([
            new DiabloGameplayEvent(
                DiabloGameplayEventKind.MapOpened,
                0,
                34,
                0),
        ], 12, 7);

        var mapOpened = Assert.Single(batch!.Events).MapOpened;
        Assert.Equal(34U, mapOpened.PlayerEntityId);
    }

    [Theory]
    [InlineData(AuthoritativeProjectileBehavior.ChargedBolt, NativeProjectileVisual.ChargedBolt)]
    [InlineData(AuthoritativeProjectileBehavior.MonsterChargedBolt, NativeProjectileVisual.ChargedBolt)]
    [InlineData(AuthoritativeProjectileBehavior.Firebolt, NativeProjectileVisual.Firebolt)]
    [InlineData(AuthoritativeProjectileBehavior.Lightning, NativeProjectileVisual.Lightning)]
    [InlineData(AuthoritativeProjectileBehavior.LightningSegment, NativeProjectileVisual.ThinLightning)]
    [InlineData(AuthoritativeProjectileBehavior.FlashBottom, NativeProjectileVisual.FlashBottom)]
    [InlineData(AuthoritativeProjectileBehavior.FlashTop, NativeProjectileVisual.FlashTop)]
    [InlineData(AuthoritativeProjectileBehavior.ResurrectionBeam, NativeProjectileVisual.ResurrectBeam)]
    [InlineData(AuthoritativeProjectileBehavior.Phasing, NativeProjectileVisual.Phasing)]
    [InlineData(AuthoritativeProjectileBehavior.Fireball, NativeProjectileVisual.Fireball)]
    [InlineData(AuthoritativeProjectileBehavior.HolyBolt, NativeProjectileVisual.HolyBolt)]
    [InlineData(AuthoritativeProjectileBehavior.BloodStar, NativeProjectileVisual.BloodStar)]
    [InlineData(AuthoritativeProjectileBehavior.BoneSpirit, NativeProjectileVisual.BoneSpirit)]
    [InlineData(AuthoritativeProjectileBehavior.FireWallSegment, NativeProjectileVisual.FireWall)]
    [InlineData(AuthoritativeProjectileBehavior.FlameWaveController, NativeProjectileVisual.FlameWaveControl)]
    [InlineData(AuthoritativeProjectileBehavior.FlameWaveSegment, NativeProjectileVisual.FlameWave)]
    [InlineData(AuthoritativeProjectileBehavior.InfernoSegment, NativeProjectileVisual.Inferno)]
    [InlineData(AuthoritativeProjectileBehavior.ChainLightningBolt, NativeProjectileVisual.ChainLightning)]
    [InlineData(AuthoritativeProjectileBehavior.ApocalypseBoom, NativeProjectileVisual.ApocalypseBoom)]
    [InlineData(AuthoritativeProjectileBehavior.Elemental, NativeProjectileVisual.Elemental)]
    [InlineData(AuthoritativeProjectileBehavior.GuardianController, NativeProjectileVisual.Guardian)]
    [InlineData(AuthoritativeProjectileBehavior.Etherealize, NativeProjectileVisual.Etherealize)]
    [InlineData(AuthoritativeProjectileBehavior.Acid, NativeProjectileVisual.Acid)]
    [InlineData(AuthoritativeProjectileBehavior.AcidSplat, NativeProjectileVisual.AcidSplat)]
    [InlineData(AuthoritativeProjectileBehavior.AcidPuddle, NativeProjectileVisual.AcidPuddle)]
    [InlineData(AuthoritativeProjectileBehavior.HorkSpawn, NativeProjectileVisual.HorkSpawn)]
    [InlineData(AuthoritativeProjectileBehavior.NovaBall, NativeProjectileVisual.NovaBall)]
    [InlineData(AuthoritativeProjectileBehavior.WeaponArrow, NativeProjectileVisual.Arrow)]
    [InlineData(AuthoritativeProjectileBehavior.MonsterArrow, NativeProjectileVisual.Arrow)]
    public void ProjectsServerOwnedNativeProjectileVisual(
        AuthoritativeProjectileBehavior behavior,
        NativeProjectileVisual expectedVisual)
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 1,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [new DiabloProjectileState(
                2,
                1,
                0,
                2,
                1,
                4,
                4,
                4,
                4,
                0,
                AuthoritativeDamageType.Fire,
                0,
                3) {
                Behavior = behavior,
            }],
            null);

        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(source);

        Assert.Equal(expectedVisual, Assert.Single(snapshot.Projectiles).NativeVisual);
        if (behavior == AuthoritativeProjectileBehavior.NovaBall)
            Assert.Equal(ProjectilePresentationKind.NovaBall, Assert.Single(snapshot.Projectiles).PresentationKind);
    }

    [Theory]
    [InlineData("Lightning", NativeProjectileVisual.MonsterLightning)]
    [InlineData("OrangeFlare", NativeProjectileVisual.MonsterOrangeFlare)]
    [InlineData("BlueFlare", NativeProjectileVisual.MonsterBlueFlare)]
    [InlineData("RedFlare", NativeProjectileVisual.MonsterRedFlare)]
    [InlineData("YellowFlare", NativeProjectileVisual.MonsterYellowFlare)]
    [InlineData("BlueFlare2", NativeProjectileVisual.MonsterBlueFlare2)]
    [InlineData("MagmaBall", NativeProjectileVisual.MonsterMagmaBall)]
    public void ProjectsAndRestoresContentOwnedMonsterMagicIdentity(
        string missileId,
        NativeProjectileVisual expectedVisual)
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 1,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [new DiabloProjectileState(
                2, 1, 0, 2, 1, 4, 4, 4, 4, 0,
                AuthoritativeDamageType.Magic, 0, 3) {
                Behavior = AuthoritativeProjectileBehavior.MonsterMagic,
                NativeMissileId = missileId,
            }],
            null);

        var projected = DiabloProtocolMapper.ToProtocolSnapshot(source);
        var projectile = Assert.Single(projected.Projectiles);
        Assert.Equal(expectedVisual, projectile.NativeVisual);
        Assert.Equal(missileId, projectile.NativeMissileId);

        var wireRoundTrip = Snapshot.Parser.ParseFrom(projected.ToByteArray());
        Assert.Equal(missileId, Assert.Single(wireRoundTrip.Projectiles).NativeMissileId);

        var restored = DiabloProtocolMapper.FromProtocolSnapshot(projected);
        Assert.Equal(missileId, Assert.Single(restored.Projectiles).NativeMissileId);

        var unknown = source with {
            Projectiles = [source.Projectiles[0] with { NativeMissileId = "Unknown" }],
        };
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(unknown));

        var misplaced = source with {
            Projectiles = [source.Projectiles[0] with {
                Behavior = AuthoritativeProjectileBehavior.Firebolt,
            }],
        };
        Assert.Throws<InvalidDataException>(() =>
            DiabloProtocolMapper.ToProtocolSnapshot(misplaced));
    }
    [Fact]
    public void ProjectsLightningWallSegmentsAsLightningInsteadOfFire()
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 1,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [new DiabloProjectileState(
                2,
                1,
                0,
                2,
                1,
                4,
                4,
                4,
                4,
                0,
                AuthoritativeDamageType.Lightning,
                0,
                3) {
                Behavior = AuthoritativeProjectileBehavior.FireWallSegment,
            }],
            null);

        var projectile = Assert.Single(DiabloProtocolMapper.ToProtocolSnapshot(source).Projectiles);

        Assert.Equal(NativeProjectileVisual.LightningWall, projectile.NativeVisual);
        Assert.Equal(ProjectilePresentationKind.Lightning, projectile.PresentationKind);
    }

    [Theory]
    [InlineData(AuthoritativeProjectileBehavior.Targeted)]
    [InlineData(AuthoritativeProjectileBehavior.Flash)]
    [InlineData(AuthoritativeProjectileBehavior.Healing)]
    [InlineData(AuthoritativeProjectileBehavior.Teleport)]
    [InlineData(AuthoritativeProjectileBehavior.MonsterMagic)]
    public void LeavesNonSpriteProjectileBehaviorsUnspecified(
        AuthoritativeProjectileBehavior behavior)
    {
        var player = new StorePlayerSnapshot(
            0,
            null,
            [],
            0,
            64,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 1,
            LevelId = 1,
        };
        var source = new DiabloSimulationSnapshot(
            1,
            player,
            [],
            [],
            [],
            [],
            [new DiabloProjectileState(
                2,
                1,
                0,
                2,
                1,
                4,
                4,
                4,
                4,
                0,
                AuthoritativeDamageType.Fire,
                0,
                3) {
                Behavior = behavior,
            }],
            null);

        var snapshot = DiabloProtocolMapper.ToProtocolSnapshot(source);

        Assert.Equal(NativeProjectileVisual.Unspecified, Assert.Single(snapshot.Projectiles).NativeVisual);
    }
}

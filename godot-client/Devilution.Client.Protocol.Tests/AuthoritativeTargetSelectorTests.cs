using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeTargetSelectorTests
{
    [Fact]
    public void PrefersALivingMonsterAndExcludesHiddenOrDeadParticipants()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 2, PositionX = 1, PositionY = 1 },
                new PlayerSnapshot { EntityId = 8, LevelId = 2, PositionX = 4, PositionY = 4, Life = 20 },
                new PlayerSnapshot { EntityId = 9, LevelId = 3, PositionX = 4, PositionY = 4, Life = 20 },
                new PlayerSnapshot { EntityId = 10, LevelId = 2, PositionX = 5, PositionY = 5, Life = 0 },
                new PlayerSnapshot { EntityId = 11, LevelId = 3, PositionX = 6, PositionY = 6, Life = 20 },
            },
            Monsters = {
                new MonsterSnapshot { EntityId = 20, LevelId = 2, PositionX = 4, PositionY = 4, Alive = true },
                new MonsterSnapshot { EntityId = 21, LevelId = 2, PositionX = 5, PositionY = 5, Alive = false },
                new MonsterSnapshot { EntityId = 22, LevelId = 2, PositionX = 7, PositionY = 7, Alive = true, SneakHidden = true },
            },
        };

        Assert.Equal(20U, AuthoritativeTargetSelector.SelectCombatTarget(snapshot, 7, 4, 4));
        Assert.Null(AuthoritativeTargetSelector.SelectCombatTarget(snapshot, 7, 5, 5));
        Assert.Null(AuthoritativeTargetSelector.SelectCombatTarget(snapshot, 7, 6, 6));
        Assert.Null(AuthoritativeTargetSelector.SelectCombatTarget(snapshot, 7, 7, 7));
    }

    [Fact]
    public void SelectsLivingRemotePlayersAfterMonsterPriority()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 2 },
                new PlayerSnapshot { EntityId = 8, LevelId = 2, PositionX = 3, PositionY = 4, Life = 10 },
            },
        };

        Assert.Equal(8U, AuthoritativeTargetSelector.SelectCombatTarget(snapshot, 7, 3, 4));
        Assert.Null(AuthoritativeTargetSelector.SelectCombatTarget(snapshot, 7, 3, 5));
    }

    [Fact]
    public void DoesNotSelectRemotePlayersOutsideProjectedNativeVision()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 2, PositionX = 0, PositionY = 0 },
                new PlayerSnapshot { EntityId = 8, LevelId = 2, PositionX = 4, PositionY = 0, Life = 10 },
            },
            LevelGeometry = {
                new LevelGeometrySnapshot { LevelId = 2, Width = 5, Height = 1 },
            },
        };
        snapshot.LevelGeometry[0].NativeVisionBlockedCells.Add(1);

        Assert.Null(AuthoritativeTargetSelector.SelectCombatTarget(snapshot, 7, 4, 0));
        Assert.Null(AuthoritativeTargetSelector.SelectRemotePlayerTarget(snapshot, 7, 4, 0, false));
    }

    [Fact]
    public void SelectsAStableMonsterIdentityForSpellTargeting()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 2 },
            },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 20,
                    LevelId = 2,
                    PositionX = 4,
                    PositionY = 4,
                    Alive = true,
                },
                new MonsterSnapshot {
                    EntityId = 19,
                    LevelId = 2,
                    PositionX = 4,
                    PositionY = 4,
                    Alive = true,
                },
                new MonsterSnapshot {
                    EntityId = 21,
                    LevelId = 3,
                    PositionX = 4,
                    PositionY = 4,
                    Alive = true,
                },
            },
        };

        Assert.Equal(
            19U,
            AuthoritativeTargetSelector.SelectMonsterTarget(snapshot, 7, 4, 4));
    }

    [Fact]
    public void DoesNotSelectDeadOrHiddenMonstersForSpellTargeting()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 2 },
            },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 20,
                    LevelId = 2,
                    PositionX = 4,
                    PositionY = 4,
                    Alive = false,
                },
                new MonsterSnapshot {
                    EntityId = 21,
                    LevelId = 2,
                    PositionX = 4,
                    PositionY = 4,
                    Alive = true,
                    SneakHidden = true,
                },
            },
        };

        Assert.Null(AuthoritativeTargetSelector.SelectMonsterTarget(snapshot, 7, 4, 4));
    }

    [Fact]
    public void DoesNotSelectMonstersOutsideProjectedNativeVision()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 2, PositionX = 0, PositionY = 0 },
            },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 20,
                    LevelId = 2,
                    PositionX = 4,
                    Alive = true,
                },
            },
            LevelGeometry = {
                new LevelGeometrySnapshot { LevelId = 2, Width = 5, Height = 1 },
            },
        };
        snapshot.LevelGeometry[0].NativeVisionBlockedCells.Add(1);

        Assert.Null(AuthoritativeTargetSelector.SelectCombatTarget(snapshot, 7, 4, 0));
        Assert.Null(AuthoritativeTargetSelector.SelectWorldEntityTarget(snapshot, 7, 4, 0));
    }

    [Fact]
    public void DoesNotSelectWorldItemsOrObjectsOutsideProjectedNativeVision()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 2, PositionX = 0, PositionY = 0 },
            },
            WorldItems = {
                new WorldItemSnapshot { EntityId = 30, LevelId = 2, PositionX = 4 },
            },
            Objects = {
                new ObjectSnapshot {
                    EntityId = 31,
                    LevelId = 2,
                    PositionX = 4,
                    EffectKind = 1,
                },
            },
            LevelGeometry = {
                new LevelGeometrySnapshot { LevelId = 2, Width = 5, Height = 1 },
            },
        };
        snapshot.LevelGeometry[0].NativeVisionBlockedCells.Add(1);

        Assert.Null(AuthoritativeTargetSelector.SelectWorldEntityTarget(snapshot, 7, 4, 0));
    }

    [Fact]
    public void SelectsAnAuthoredUniqueMonsterDialogueTarget()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 4 },
            },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 20,
                    LevelId = 4,
                    PositionX = 5,
                    PositionY = 6,
                    Alive = true,
                    IsUnique = true,
                    UniqueIdentity = "Gharbad",
                    DialogueMessageId = 145,
                },
            },
            Quests = {
                new QuestSnapshot {
                    QuestId = 3003,
                    LevelId = 4,
                    CompletionUniqueIdentity = "Gharbad",
                },
            },
        };

        Assert.Equal(
            new AuthoritativeMonsterDialogueTarget(3003, 20),
            AuthoritativeMonsterDialogueSelector.Select(snapshot, 7, 5, 6));
    }

    [Fact]
    public void DoesNotSelectHiddenUniqueMonsterDialogueTarget()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 4, PositionX = 0, PositionY = 0 },
            },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 20,
                    LevelId = 4,
                    PositionX = 4,
                    Alive = true,
                    IsUnique = true,
                    UniqueIdentity = "Gharbad",
                    DialogueMessageId = 145,
                },
            },
            Quests = {
                new QuestSnapshot {
                    QuestId = 3003,
                    LevelId = 4,
                    CompletionUniqueIdentity = "Gharbad",
                },
            },
            LevelGeometry = {
                new LevelGeometrySnapshot { LevelId = 4, Width = 5, Height = 1 },
            },
        };
        snapshot.LevelGeometry[0].NativeVisionBlockedCells.Add(1);

        Assert.Null(AuthoritativeMonsterDialogueSelector.Select(snapshot, 7, 4, 0));
    }

    [Fact]
    public void SelectsMovingMonsterFromItsFutureOccupiedCell()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 4 },
            },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 20,
                    LevelId = 4,
                    PositionX = 5,
                    PositionY = 6,
                    Moving = true,
                    FuturePositionX = 6,
                    FuturePositionY = 6,
                    Alive = true,
                },
                new MonsterSnapshot {
                    EntityId = 21,
                    LevelId = 4,
                    PositionX = 7,
                    PositionY = 6,
                    Moving = true,
                    FuturePositionX = 8,
                    FuturePositionY = 6,
                    Alive = true,
                    IsUnique = true,
                    UniqueIdentity = "Gharbad",
                    DialogueMessageId = 145,
                },
            },
            Quests = {
                new QuestSnapshot {
                    QuestId = 3003,
                    LevelId = 4,
                    CompletionUniqueIdentity = "Gharbad",
                },
            },
        };

        Assert.Equal(
            20U,
            AuthoritativeTargetSelector.SelectMonsterTarget(snapshot, 7, 6, 6));
        Assert.Equal(
            20U,
            AuthoritativeTargetSelector.SelectWorldEntityTarget(snapshot, 7, 6, 6));
        Assert.Equal(
            new AuthoritativeMonsterDialogueTarget(3003, 21),
            AuthoritativeMonsterDialogueSelector.Select(snapshot, 7, 8, 6));
    }

    [Fact]
    public void DoesNotUseAnUnoccupiedFutureCellWhenMonsterIsNotMoving()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 4 },
            },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 20,
                    LevelId = 4,
                    PositionX = 5,
                    PositionY = 6,
                    FuturePositionX = 6,
                    FuturePositionY = 6,
                    Alive = true,
                },
            },
        };

        Assert.Null(AuthoritativeTargetSelector.SelectMonsterTarget(snapshot, 7, 6, 6));
    }

    [Fact]
    public void TreatsZeroLevelAsVisibleCompatibilityWildcard()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 4 },
            },
            WorldItems = {
                new WorldItemSnapshot {
                    EntityId = 30,
                    LevelId = 0,
                    PositionX = 3,
                    PositionY = 3,
                },
            },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 20,
                    LevelId = 0,
                    PositionX = 4,
                    PositionY = 4,
                    Alive = true,
                    IsUnique = true,
                    UniqueIdentity = "Gharbad",
                    DialogueMessageId = 145,
                },
            },
            Quests = {
                new QuestSnapshot {
                    QuestId = 3003,
                    LevelId = 0,
                    CompletionUniqueIdentity = "Gharbad",
                },
            },
        };

        Assert.Equal(
            30U,
            AuthoritativeTargetSelector.SelectWorldEntityTarget(snapshot, 7, 3, 3));
        Assert.Equal(
            new AuthoritativeMonsterDialogueTarget(3003, 20),
            AuthoritativeMonsterDialogueSelector.Select(snapshot, 7, 4, 4));
    }

    [Fact]
    public void RejectsDialogueWithoutAnActiveMatchingQuestOrTarget()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 4 },
            },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 20,
                    LevelId = 4,
                    PositionX = 5,
                    PositionY = 6,
                    Alive = true,
                    IsUnique = true,
                    UniqueIdentity = "Gharbad",
                    DialogueMessageId = 145,
                },
                new MonsterSnapshot {
                    EntityId = 21,
                    LevelId = 4,
                    PositionX = 6,
                    PositionY = 6,
                    Alive = true,
                    IsUnique = true,
                    UniqueIdentity = "Zhar",
                },
            },
            Quests = {
                new QuestSnapshot {
                    QuestId = 3003,
                    LevelId = 4,
                    CompletionUniqueIdentity = "Gharbad",
                    Completed = true,
                },
                new QuestSnapshot {
                    QuestId = 3004,
                    LevelId = 4,
                    CompletionUniqueIdentity = "Zhar",
                },
            },
        };

        Assert.Null(AuthoritativeMonsterDialogueSelector.Select(snapshot, 7, 5, 6));
        Assert.Null(AuthoritativeMonsterDialogueSelector.Select(snapshot, 7, 6, 6));
    }

    [Fact]
    public void SelectsWorldEntitiesInServerTelekinesisPrecedenceOrder()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 2 },
            },
            WorldItems = {
                new WorldItemSnapshot {
                    EntityId = 30,
                    LevelId = 2,
                    PositionX = 4,
                    PositionY = 4,
                },
                new WorldItemSnapshot {
                    EntityId = 33,
                    LevelId = 2,
                    PositionX = 5,
                    PositionY = 5,
                },
            },
            Objects = {
                new ObjectSnapshot {
                    EntityId = 31,
                    LevelId = 2,
                    PositionX = 5,
                    PositionY = 5,
                    EffectKind = 1,
                },
            },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 32,
                    LevelId = 2,
                    PositionX = 6,
                    PositionY = 6,
                    Alive = true,
                },
            },
        };

        Assert.Equal(30U, AuthoritativeTargetSelector.SelectWorldEntityTarget(snapshot, 7, 4, 4));
        Assert.Equal(31U, AuthoritativeTargetSelector.SelectWorldEntityTarget(snapshot, 7, 5, 5));
        Assert.Equal(32U, AuthoritativeTargetSelector.SelectWorldEntityTarget(snapshot, 7, 6, 6));
    }

    [Fact]
    public void DoesNotUseAuthoredDialogueMonsterAsWorldSpellTarget()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 2 },
            },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 20,
                    LevelId = 2,
                    PositionX = 4,
                    PositionY = 4,
                    Alive = true,
                    IsUnique = true,
                    DialogueMessageId = 145,
                },
            },
        };

        Assert.Null(AuthoritativeTargetSelector.SelectWorldEntityTarget(snapshot, 7, 4, 4));
    }

    [Fact]
    public void OrdersOverlappingWorldTargetsByEntityIdentity()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 2 },
            },
            WorldItems = {
                new WorldItemSnapshot { EntityId = 30, LevelId = 2, PositionX = 4, PositionY = 4 },
                new WorldItemSnapshot { EntityId = 29, LevelId = 2, PositionX = 4, PositionY = 4 },
            },
            Objects = {
                new ObjectSnapshot { EntityId = 20, LevelId = 2, PositionX = 5, PositionY = 5, EffectKind = 1 },
                new ObjectSnapshot { EntityId = 19, LevelId = 2, PositionX = 5, PositionY = 5, EffectKind = 1 },
            },
        };

        Assert.Equal(29U, AuthoritativeTargetSelector.SelectWorldEntityTarget(snapshot, 7, 4, 4));
        Assert.Equal(19U, AuthoritativeTargetSelector.SelectWorldEntityTarget(snapshot, 7, 5, 5));
    }

    [Fact]
    public void SelectsRemotePlayersAtTheirReservedWalkDestination()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 2 },
                new PlayerSnapshot {
                    EntityId = 8,
                    LevelId = 2,
                    PositionX = 3,
                    PositionY = 4,
                    Life = 10,
                    CombatMode = PlayerCombatMode.WalkSideways,
                    CurrentAction = new PlayerActionSnapshot {
                        Kind = PlayerActionKind.Move,
                        DirectionX = 1,
                    },
                },
            },
        };

        Assert.Equal(8U, AuthoritativeTargetSelector.SelectCombatTarget(snapshot, 7, 4, 4));
        Assert.Equal(
            8U,
            AuthoritativeTargetSelector.SelectRemotePlayerTarget(
                snapshot,
                7,
                4,
                4,
                includeDead: false));
    }

    [Fact]
    public void SelectsRemotePlayerCastTargetsWithExplicitDeadTargetPolicy()
    {
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 2 },
                new PlayerSnapshot { EntityId = 8, LevelId = 2, PositionX = 3, PositionY = 4, Life = 10 },
                new PlayerSnapshot { EntityId = 9, LevelId = 2, PositionX = 3, PositionY = 5, Life = 0 },
                new PlayerSnapshot { EntityId = 10, LevelId = 3, PositionX = 3, PositionY = 6, Life = 10 },
            },
        };

        Assert.Equal(
            8U,
            AuthoritativeTargetSelector.SelectRemotePlayerTarget(snapshot, 7, 3, 4, includeDead: false));
        Assert.Null(
            AuthoritativeTargetSelector.SelectRemotePlayerTarget(snapshot, 7, 3, 5, includeDead: false));
        Assert.Equal(
            9U,
            AuthoritativeTargetSelector.SelectRemotePlayerTarget(snapshot, 7, 3, 5, includeDead: true));
        Assert.Null(
            AuthoritativeTargetSelector.SelectRemotePlayerTarget(snapshot, 7, 3, 6, includeDead: true));
    }
}

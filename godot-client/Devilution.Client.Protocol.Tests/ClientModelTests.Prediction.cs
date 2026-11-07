using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;
using Xunit;
using ProtocolItemPresentationKind = Devilution.Protocol.V1.ItemPresentationKind;

namespace Devilution.Client.Protocol.Tests;

public sealed partial class ClientModelTests
{
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
            monster => {
                monster.HitPointsQ6 = 0;
                monster.MaxHitPointsQ6 = 0;
                monster.MaxHitPoints = 2;
            },
            monster => monster.HitPoints = -1,
            monster => monster.Alive = false,
            monster => {
                monster.Alive = true;
                monster.HitPoints = 0;
                monster.HitPointsQ6 = 0;
            },
            monster => monster.DialogueState = (MonsterDialogueState)3,
        }) {
            var invalid = valid.Clone();
            mutate(invalid.Monsters[0]);
            Assert.Throws<InvalidDataException>(() =>
                model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
        }
    }

    [Fact]
    public void RejectsNativeMonsterMovementModesAndTurnsOutsideSharedBounds()
    {
        var model = new AuthoritativeClientModel();
        var valid = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Monsters = { new MonsterSnapshot { EntityId = 9 } },
        };
        model.Apply(new AuthoritativeClientMessage(Snapshot: valid));

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
                model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
        }
    }

    [Fact]
    public void RejectsNativeMonsterAiTimersAndCountersOutsideSharedBounds()
    {
        var model = new AuthoritativeClientModel();
        var valid = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Monsters = { new MonsterSnapshot { EntityId = 9 } },
        };
        model.Apply(new AuthoritativeClientMessage(Snapshot: valid));

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
            monster => monster.HorkDemonMoveTicks = -2,
            monster => monster.HorkDemonMoveTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterMovementTicks + 1,
            monster => monster.HorkDemonMoveTurn = -1,
            monster => monster.HorkDemonMoveTurn = 2,
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
                model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
        }
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 1)]
    [InlineData(AuthoritativeProtocolLimits.MaximumNativeMonsterMovementTicks, 1)]
    public void AcceptsHorkDemonMoveGoalBoundaries(int ticks, int turn)
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Monsters = { new MonsterSnapshot {
                EntityId = 9, HorkDemonMoveTicks = ticks, HorkDemonMoveTurn = turn,
            } },
        }));
    }

    [Fact]
    public void RejectsNativeMonsterAiIdentityDirectionsAndScavengerReferencesOutsideSharedBounds()
    {
        var model = new AuthoritativeClientModel();
        var valid = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
            Monsters = { new MonsterSnapshot { EntityId = 9, LevelId = 1 } },
        };
        model.Apply(new AuthoritativeClientMessage(Snapshot: valid));

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
                model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
        }

        var wrongLevelReference = valid.Clone();
        wrongLevelReference.Corpses.Add(new CorpseSnapshot { EntityId = 2, LevelId = 2 });
        wrongLevelReference.Monsters[0].ScavengerCorpseEntityId = 2;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: wrongLevelReference)));
    }

    [Fact]
    public void RejectsOneSidedNativeMonsterRuntimeSeeds()
    {
        var model = new AuthoritativeClientModel();
        var valid = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Monsters = { new MonsterSnapshot { EntityId = 9 } },
        };
        model.Apply(new AuthoritativeClientMessage(Snapshot: valid));

        foreach (var mutate in new Action<MonsterSnapshot>[] {
            monster => monster.MonsterItemSeed = 1,
            monster => monster.MonsterAiSeed = 1,
        }) {
            var invalid = valid.Clone();
            mutate(invalid.Monsters[0]);
            Assert.Throws<InvalidDataException>(() =>
                model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
        }
    }

    [Fact]
    public void RejectsPlayerMinionOwnersMissingFromTheSnapshotOrOnAnotherLevel()
    {
        var model = new AuthoritativeClientModel();
        var valid = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7, LevelId = 1 } },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 9,
                    LevelId = 1,
                    IsPlayerMinion = true,
                    OwnerPlayerId = 7,
                },
            },
        };
        model.Apply(new AuthoritativeClientMessage(Snapshot: valid));

        var missingOwner = valid.Clone();
        missingOwner.Monsters[0].OwnerPlayerId = 8;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: missingOwner)));

        var wrongLevel = valid.Clone();
        wrongLevel.Players[0].LevelId = 2;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: wrongLevel)));
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
    public void AcceptsAndRetainsAuthoritativeMonsterGroupUnityState()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 9,
                    Alive = true,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    PackSize = 2,
                    ActiveForTicks = 6,
                },
                new MonsterSnapshot {
                    EntityId = 10,
                    Alive = true,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    LeaderEntityId = 9,
                    LeaderRelation = 1,
                    PackSize = 2,
                    ActiveForTicks = 5,
                },
            },
        };

        model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot));

        Assert.Equal(9U, model.Snapshot!.Monsters[1].LeaderEntityId);
        Assert.Equal(1, model.Snapshot.Monsters[1].LeaderRelation);
        Assert.Equal(2, model.Snapshot.Monsters[0].PackSize);
        Assert.Equal(5, model.Snapshot.Monsters[1].ActiveForTicks);
    }

    [Fact]
    public void RejectsInvalidAuthoritativeMonsterGroupUnityState()
    {
        var valid = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 9,
                    Alive = true,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                },
                new MonsterSnapshot {
                    EntityId = 10,
                    Alive = true,
                    HitPoints = 5,
                    MaxHitPoints = 5,
                    LeaderEntityId = 9,
                    LeaderRelation = 1,
                },
            },
        };
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: valid));

        foreach (var mutate in new Action<Snapshot>[] {
            snapshot => snapshot.Monsters[1].LeaderEntityId = 0,
            snapshot => snapshot.Monsters[1].LeaderEntityId = 11,
            snapshot => snapshot.Monsters[1].LeaderRelation = 3,
            snapshot => snapshot.Monsters[1].IsUnique = true,
            snapshot => snapshot.Monsters[0].Alive = false,
            snapshot => snapshot.Monsters[1].PackSize = 256,
            snapshot => snapshot.Monsters[1].ActiveForTicks = -1,
        }) {
            var invalid = valid.Clone();
            mutate(invalid);
            Assert.Throws<InvalidDataException>(() =>
                model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
        }
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

        var invalidTiming = moving.Clone();
        invalidTiming.Monsters[0].MovementStartedTick = 12;
        invalidTiming.Monsters[0].MovementEffectTick = 11;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidTiming)));

        var stationary = moving.Clone();
        stationary.Monsters[0].Moving = false;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: stationary)));

        var legacyStationary = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 9,
                    PositionX = 4,
                    PositionY = 5,
                    HitPoints = 10,
                    MaxHitPoints = 10,
                    Alive = true,
                },
            },
        };
        model.Apply(new AuthoritativeClientMessage(Snapshot: legacyStationary));
    }

    [Fact]
    public void RejectsOverflowingMonsterMovementDeltaAsInvalidSnapshot()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 7 } },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 9,
                    PositionX = 0,
                    PositionY = 0,
                    Moving = true,
                    FuturePositionX = int.MinValue,
                    FuturePositionY = 0,
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
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
    public void ValidatesTimedMovementWithoutTreatingItsReservedDestinationAsPendingPrediction()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Players = {
                new PlayerSnapshot { EntityId = 7, PositionX = 2, PositionY = 3 },
            },
        }));
        model.TrackPredictedMove(12, 1, 0);
        var moving = new Snapshot {
            Tick = 12,
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

        Assert.Null(model.PredictedPlayerPosition);
        Assert.False(model.IsCorrectingPosition);
        var applied = moving.Clone();
        applied.Tick = 18;
        applied.Players[0].PositionX = 3;
        applied.Players[0].CurrentAction.EffectApplied = true;
        model.Apply(new AuthoritativeClientMessage(Snapshot: applied));
        Assert.Null(model.PredictedPlayerPosition);
        Assert.False(model.IsCorrectingPosition);
        var invalid = moving.Clone();
        invalid.Tick = 18;
        invalid.Players[0].CurrentAction.EffectApplied = true;
        invalid.Players[0].CurrentAction.ReadyTick = 19;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalid)));
    }

    [Fact]
    public void RejectsAnAppliedEffectBeforeItsAuthoritativeEffectTick()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Tick = 12,
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    CombatMode = PlayerCombatMode.WalkSouthwards,
                    CombatModeEndTick = 18,
                    CurrentAction = new PlayerActionSnapshot {
                        Kind = PlayerActionKind.Move,
                        StartedAtTick = 10,
                        EffectTick = 18,
                        ReadyTick = 18,
                        EndTick = 18,
                        EffectApplied = true,
                        DirectionX = 1,
                    },
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public void AcceptsPendingMovementIntoProjectedBlockedCellButRejectsOutOfBounds()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Tick = 12,
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

        model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot));
        Assert.Equal(PlayerActionKind.Move, model.Snapshot!.Players[0].CurrentAction.Kind);

        snapshot.LevelGeometry[0].BlockedCells.Clear();
        snapshot.Players[0].PositionX = 3;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));

        snapshot.Players[0].PositionX = 1;
        model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot));
        Assert.Equal(PlayerActionKind.Move, model.Snapshot!.Players[0].CurrentAction.Kind);
    }

    [Fact]
    public void RejectsInvalidProjectedNativeVisionBlockerCell()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 2,
                    Width = 2,
                    Height = 2,
                    NativeVisionBlockedCells = { 1, 1 },
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public void RejectsNativeVisionLayerThatOmitsMovementBlockers()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 2,
                    Width = 2,
                    Height = 2,
                    BlockedCells = { 0 },
                    NativeVisionBlockedCells = { 1 },
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public void RejectsInvalidProjectedPlayerLightRadius()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    LightRadius = 16,
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
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
    public void StopsPredictionAtKnownBlockedCell()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
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
                    PositionX = 0,
                    PositionY = 1,
                },
            },
        }));

        model.TrackPredictedMove(1, 1, 0);
        model.TrackPredictedMove(2, 1, 0);

        Assert.Equal(new ClientPosition(1, 1), model.PredictedPlayerPosition);
        Assert.True(model.IsCorrectingPosition);
    }

    [Fact]
    public void StopsPredictionAtKnownMonsterOccupancy()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    LevelId = 1,
                    PositionX = 0,
                    PositionY = 1,
                },
            },
            Monsters = {
                new MonsterSnapshot {
                    EntityId = 9,
                    LevelId = 1,
                    PositionX = 1,
                    PositionY = 2,
                    HitPoints = 10,
                    MaxHitPoints = 10,
                    Alive = true,
                },
            },
        }));

        model.TrackPredictedMove(1, 1, 0);
        model.TrackPredictedMove(2, 0, 1);

        Assert.Equal(new ClientPosition(1, 1), model.PredictedPlayerPosition);
        Assert.True(model.IsCorrectingPosition);
    }

    [Fact]
    public void StopsPredictionAtKnownRemotePlayerOccupancy()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    LevelId = 1,
                    PositionX = 0,
                    PositionY = 1,
                },
                new PlayerSnapshot {
                    EntityId = 8,
                    LevelId = 1,
                    Life = 10,
                    LifeMaximum = 10,
                    PositionX = 1,
                    PositionY = 1,
                },
            },
        }));

        model.TrackPredictedMove(1, 1, 0);

        Assert.Null(model.PredictedPlayerPosition);
        Assert.False(model.IsCorrectingPosition);
    }

    [Fact]
    public void StopsPredictionAtKnownSolidAuthoredObjectOccupancy()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    LevelId = 1,
                    PositionX = 0,
                    PositionY = 1,
                },
            },
            Objects = {
                new ObjectSnapshot {
                    EntityId = 20,
                    ObjectId = 1,
                    LevelId = 1,
                    PositionX = 1,
                    PositionY = 1,
                },
            },
        }));

        model.TrackPredictedMove(1, 1, 0);

        Assert.Null(model.PredictedPlayerPosition);
        Assert.False(model.IsCorrectingPosition);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void PredictsThroughOnlyPassableTypedCathedralDoorStates(int doorState, bool passable)
    {
        var animationFrame = doorState == 0 ? 1U : 3U;
        var missilePassable = doorState != 0;
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    LevelId = 1,
                    PositionX = 0,
                    PositionY = 1,
                },
            },
            Objects = {
                new ObjectSnapshot {
                    EntityId = 20,
                    ObjectId = 1,
                    NativeObjectId = 1,
                    LevelId = 1,
                    PositionX = 1,
                    PositionY = 1,
                    KeepVisibleAfterActivation = true,
                    AnimationFrame = animationFrame,
                    MissilePassable = missilePassable,
                    CathedralDoor = new CathedralDoorSnapshot {
                        EntityId = 20,
                        LevelId = 1,
                        PositionX = 1,
                        PositionY = 1,
                        NativeObjectId = 1,
                        DoorState = doorState,
                        CachedPiece = 43,
                        CachedNeighborPiece = 49,
                        AnimationFrame = animationFrame,
                        SelectionRegion = doorState == 0 ? 3U : 2U,
                        PreDraw = doorState != 0,
                        MissilePassable = missilePassable,
                    },
                },
            },
        }));

        model.TrackPredictedMove(1, 1, 0);

        if (passable)
            Assert.Equal(new ClientPosition(1, 1), model.PredictedPlayerPosition);
        else
            Assert.Null(model.PredictedPlayerPosition);
    }

    [Fact]
    public void KeepsLegacyNativeIdPeerAsOccupied()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    LevelId = 1,
                    PositionX = 0,
                    PositionY = 1,
                },
            },
            Objects = {
                new ObjectSnapshot {
                    EntityId = 20,
                    ObjectId = 1,
                    NativeObjectId = 1,
                    LevelId = 1,
                    PositionX = 1,
                    PositionY = 1,
                },
            },
        }));

        model.TrackPredictedMove(1, 1, 0);

        Assert.Null(model.PredictedPlayerPosition);
    }

    [Fact]
    public void StaticObjectDPiece269BlocksMovementButNotNativeVision()
    {
        var geometry = new LevelGeometrySnapshot {
            LevelId = 1,
            Width = 2,
            Height = 2,
            NativePieceLayer = { 0, 0, 0, 269 },
            StaticObjectBlockedCells = { 3 },
        };
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            LevelGeometry = { geometry },
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    LevelId = 1,
                    PositionX = 0,
                    PositionY = 1,
                },
            },
        }));

        model.TrackPredictedMove(1, 1, 0);

        Assert.Null(model.PredictedPlayerPosition);
        Assert.True(AuthoritativeNativeVisionVisibility.IsVisible(
            1, 1, 1, 1, geometry, 0, 1, 10));
    }

    [Fact]
    public void RejectsHybridTypedDoorDuringMovementPrediction()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    LevelId = 1,
                    PositionX = 0,
                    PositionY = 1,
                },
            },
            Objects = {
                new ObjectSnapshot {
                    EntityId = 20,
                    ObjectId = 1,
                    NativeObjectId = 1,
                    LevelId = 1,
                    PositionX = 1,
                    PositionY = 1,
                    Breakable = true,
                    KeepVisibleAfterActivation = true,
                    AnimationFrame = 3,
                    CathedralDoor = new CathedralDoorSnapshot {
                        EntityId = 20,
                        LevelId = 1,
                        PositionX = 1,
                        PositionY = 1,
                        NativeObjectId = 1,
                        DoorState = 1,
                        CachedPiece = 43,
                        CachedNeighborPiece = 49,
                        AnimationFrame = 3,
                        SelectionRegion = 2,
                        PreDraw = true,
                        MissilePassable = true,
                    },
                },
            },
        }));

        model.TrackPredictedMove(1, 1, 0);

        Assert.Null(model.PredictedPlayerPosition);
    }

    [Fact]
    public void RejectsTypedDoorWithConflictingNativeSpecialLayer()
    {
        var model = new AuthoritativeClientModel();
        var snapshot = new Snapshot {
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 1,
                    Width = 2,
                    Height = 2,
                    NativeSpecialLayer = { 0, 0, 0, 8 },
                },
            },
            Players = {
                new PlayerSnapshot { EntityId = 7, LevelId = 1 },
            },
            Objects = {
                new ObjectSnapshot {
                    EntityId = 20,
                    ObjectId = 1,
                    NativeObjectId = 1,
                    LevelId = 1,
                    PositionX = 1,
                    PositionY = 1,
                    KeepVisibleAfterActivation = true,
                    AnimationFrame = 3,
                    MissilePassable = true,
                    CathedralDoor = new CathedralDoorSnapshot {
                        EntityId = 20,
                        LevelId = 1,
                        PositionX = 1,
                        PositionY = 1,
                        NativeObjectId = 1,
                        DoorState = 1,
                        CachedPiece = 43,
                        CachedNeighborPiece = 49,
                        AnimationFrame = 3,
                        SelectionRegion = 2,
                        PreDraw = true,
                        MissilePassable = true,
                    },
                },
            },
        };

        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
        snapshot.LevelGeometry[0].NativeSpecialLayer[3] = 7;
        snapshot.Objects[0].PositionX = 2;
        snapshot.Objects[0].CathedralDoor.PositionX = 2;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: snapshot)));
    }

    [Fact]
    public void RejectsNewGeometryLayersWithoutPositiveDimensions()
    {
        var staticCells = new Snapshot {
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 2,
                    Width = 0,
                    Height = 1,
                    StaticObjectBlockedCells = { 0 },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeClientModel().Apply(
                new AuthoritativeClientMessage(Snapshot: staticCells)));

        var specialLayer = new Snapshot {
            LevelGeometry = {
                new LevelGeometrySnapshot {
                    LevelId = 2,
                    Width = 1,
                    Height = 0,
                    NativeSpecialLayer = { 0 },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() =>
            new AuthoritativeClientModel().Apply(
                new AuthoritativeClientMessage(Snapshot: specialLayer)));
    }

    [Fact]
    public void AllowsPredictionIntoAnUnactivatedOccupancyTriggerObject()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Players = {
                new PlayerSnapshot {
                    EntityId = 7,
                    LevelId = 1,
                    PositionX = 0,
                    PositionY = 1,
                },
            },
            Objects = {
                new ObjectSnapshot {
                    EntityId = 20,
                    ObjectId = 1,
                    LevelId = 1,
                    PositionX = 1,
                    PositionY = 1,
                    QuestId = 3001,
                    ActivateOnOccupancy = true,
                },
            },
            Quests = {
                new QuestSnapshot { QuestId = 3001, RequiredProgress = 1 },
            },
        }));

        model.TrackPredictedMove(1, 1, 0);

        Assert.Equal(new ClientPosition(1, 1), model.PredictedPlayerPosition);
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
    public void ReconnectAcceptsLowerTickSnapshotAsNewServerEpoch()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Tick = 10,
            Players = {
                new PlayerSnapshot { EntityId = 7, PositionX = 2, PositionY = 3 },
            },
        }));
        model.Apply(new AuthoritativeClientMessage(Events: new EventBatch {
            Tick = 10,
            Events = {
                new GameEvent { Experience = new ExperienceEvent { PlayerEntityId = 7, Amount = 10 } },
            },
        }));
        model.Apply(new AuthoritativeClientMessage(Acknowledgement: new CommandAck {
            Results = {
                new CommandResult { ClientSequence = 4, Status = CommandStatus.Accepted },
            },
        }));
        model.TrackPredictedMove(1, 1, 0);

        model.ResetTransientPresentation();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Tick = 1,
            Players = {
                new PlayerSnapshot { EntityId = 7, PositionX = 9, PositionY = 8 },
            },
        }));

        Assert.Equal(1UL, model.CurrentTick);
        Assert.Equal((9, 8), (model.Snapshot!.Players[0].PositionX, model.Snapshot.Players[0].PositionY));
        Assert.Empty(model.RecentEvents);
        Assert.Null(model.PredictedPlayerPosition);
        Assert.False(model.IsCorrectingPosition);
        Assert.Equal(4UL, model.LastCommandResult!.ClientSequence);

        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Tick = 0,
            Players = {
                new PlayerSnapshot { EntityId = 7, PositionX = 1, PositionY = 1 },
            },
        }));

        Assert.Equal(1UL, model.CurrentTick);
        Assert.Equal((9, 8), (model.Snapshot.Players[0].PositionX, model.Snapshot.Players[0].PositionY));
    }

    [Fact]
    public void IgnoresEventsUntilTheReconnectEpochSnapshotArrives()
    {
        var model = new AuthoritativeClientModel();
        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Tick = 10,
            Players = { new PlayerSnapshot { EntityId = 7 } },
        }));

        model.ResetTransientPresentation();
        model.Apply(new AuthoritativeClientMessage(Events: new EventBatch {
            Tick = 11,
            Events = {
                new GameEvent {
                    Experience = new ExperienceEvent { PlayerEntityId = 7, Amount = 99 },
                },
            },
        }));

        Assert.Empty(model.RecentEvents);

        model.Apply(new AuthoritativeClientMessage(Snapshot: new Snapshot {
            Tick = 1,
            Players = { new PlayerSnapshot { EntityId = 7 } },
        }));
        model.Apply(new AuthoritativeClientMessage(Events: new EventBatch {
            Tick = 2,
            Events = {
                new GameEvent {
                    Experience = new ExperienceEvent { PlayerEntityId = 7, Amount = 10 },
                },
            },
        }));

        var accepted = Assert.Single(model.RecentEvents);
        Assert.Equal(10U, accepted.Experience.Amount);
        Assert.Equal(1UL, model.CurrentTick);
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
            Tick = 1,
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

        var invalidSource = snapshot.Clone();
        invalidSource.Players[0].CurrentAction.TargetEntityId = 0;
        Assert.Throws<InvalidDataException>(() =>
            model.Apply(new AuthoritativeClientMessage(Snapshot: invalidSource)));
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

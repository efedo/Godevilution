using System.Text.Json;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeSaveCodecTests
{
    [Fact]
    public void VersionOneRoundTripsNeutralStateThroughStableEnvelope()
    {
        var codec = new AuthoritativeSaveCodec();
        var source = CreateSnapshot();

        var serialized = codec.Serialize(source);
        var document = JsonSerializer.Deserialize<AuthoritativeSaveDocument>(serialized)!;
        var restored = codec.Deserialize(serialized);

        Assert.Equal(1, AuthoritativeSaveCodec.CurrentFormatVersion);
        Assert.Equal(1, document.FormatVersion);
        Assert.NotEmpty(document.SnapshotBase64);
        Assert.Equal(source.Tick, restored.Tick);
        Assert.Equal(source.Player.Gold, restored.Player.Gold);
        Assert.Equal(source.Player.EntityId, restored.Player.EntityId);
        Assert.Equal(source.Player.BaseLifeMaximum, restored.Player.BaseLifeMaximum);
        Assert.Equal(source.Player.BaseManaMaximum, restored.Player.BaseManaMaximum);
        Assert.Equal(source.Player.CharacterClassId, restored.Player.CharacterClassId);
        Assert.Equal(source.Player.CharacterName, restored.Player.CharacterName);
        Assert.Equal(source.Player.ManaShieldActive, restored.Player.ManaShieldActive);
        Assert.Equal(source.Player.ReflectCharges, restored.Player.ReflectCharges);
        Assert.Equal(source.Player.DiabloKillLevel, restored.Player.DiabloKillLevel);
        Assert.Equal(source.Player.AbilitySpellId, restored.Player.AbilitySpellId);
        Assert.Equal(source.Player.FriendlyMode, restored.Player.FriendlyMode);
        Assert.Equal(source.Player.FacingDirection, restored.Player.FacingDirection);
        Assert.Equal(source.Player.CombatMode, restored.Player.CombatMode);
        Assert.Equal(source.Player.CombatModeEndTick, restored.Player.CombatModeEndTick);
        Assert.Equal(source.Player.CurrentAction, restored.Player.CurrentAction);
        Assert.Equal(source.Player.HeldItem, restored.Player.HeldItem);
        Assert.True(restored.Player.CurrentAction!.EffectApplied);
        Assert.Equal(
            source.Player.MemorizedSpells.ToArray(),
            restored.Player.MemorizedSpells.ToArray());
        Assert.Equal(
            source.Player.StatusEffects.ToArray(),
            restored.Player.StatusEffects.ToArray());
        Assert.Equal(source.Player.Attributes, restored.Player.Attributes);
        Assert.Equal(source.Monsters.ToArray(), restored.Monsters.ToArray());
        Assert.Equal(5, restored.Monsters[0].FallenFearTicks);
        Assert.Equal(7, restored.Monsters[0].FallenFearDirection);
        Assert.Equal(source.WorldItems.ToArray(), restored.WorldItems.ToArray());
        Assert.Equal(source.Objects.ToArray(), restored.Objects.ToArray());
        Assert.Equal(source.Projectiles.ToArray(), restored.Projectiles.ToArray());
        Assert.Equal(source.RandomState, restored.RandomState);
        Assert.Equal(source.VictoryTriggered, restored.VictoryTriggered);
        Assert.Equal(source.Difficulty, restored.Difficulty);
        Assert.Equal(source.ActiveStore!.StoreId, restored.ActiveStore!.StoreId);
        Assert.Equal(
            source.ActiveStore.Items.ToArray(),
            restored.ActiveStore.Items.ToArray());
    }

    [Fact]
    public void RejectsMalformedIncompleteAndUnknownVersionEnvelopes()
    {
        var codec = new AuthoritativeSaveCodec();

        Assert.Throws<InvalidDataException>(() => codec.Deserialize("{"));
        Assert.Throws<InvalidDataException>(() => codec.Deserialize(
            JsonSerializer.Serialize(new AuthoritativeSaveDocument(1, "not-base64"))));
        var unsupported = JsonSerializer.Serialize(new AuthoritativeSaveDocument(
            2,
            Convert.ToBase64String([1])));
        var exception = Assert.Throws<InvalidDataException>(() =>
            codec.Deserialize(unsupported));

        Assert.Contains("format 2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VersionOneRoundTripsSneakStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "Sneak",
                FallenFearTicks = 0,
                FallenFearDirection = 0,
                SneakHidden = true,
                SneakRetreatTicks = 5,
            }],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        var monster = Assert.Single(restored.Monsters);
        Assert.True(monster.SneakHidden);
        Assert.Equal(5, monster.SneakRetreatTicks);
    }

    [Fact]
    public void VersionOneRoundTripsCorpseStateThroughTheProtocolSave()
    {
        var source = CreateSnapshot() with {
            Corpses = [new DiabloCorpseState(30, 0, 3, 3)],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        var corpse = Assert.Single(restored.Corpses);
        Assert.Equal((30U, 0U, 3, 3), (
            corpse.EntityId,
            corpse.LevelId,
            corpse.PositionX,
            corpse.PositionY));
    }

    [Fact]
    public void VersionOneRoundTripsScavengerHealingStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "Scavenger",
                FallenFearTicks = 0,
                FallenFearDirection = 0,
                ScavengerHealingTicks = 7,
                ScavengerCorpseEntityId = 30,
            }],
            Corpses = [new DiabloCorpseState(30, 0, 3, 3)],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        var monster = Assert.Single(restored.Monsters);
        Assert.Equal((7, 30U), (monster.ScavengerHealingTicks, monster.ScavengerCorpseEntityId));
        Assert.Equal(30U, Assert.Single(restored.Corpses).EntityId);
    }

    [Fact]
    public void VersionOneRoundTripsBatRetreatStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "Bat",
                BatRetreatTicks = 1,
                BatRetreatDirection = 7,
                BatMovementIdleTicks = 21,
                BatMovementMode = 1,
            }],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        var monster = Assert.Single(restored.Monsters);
        Assert.Equal((1, 7), (monster.BatRetreatTicks, monster.BatRetreatDirection));
        Assert.Equal((21, 1), (monster.BatMovementIdleTicks, monster.BatMovementMode));
    }

    [Fact]
    public void VersionOneRoundTripsRangedAvoidanceMoveGoalStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "Magma",
                RangedAvoidanceMoveTicks = 7,
                RangedAvoidanceTurn = 1,
            }],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        var monster = Assert.Single(restored.Monsters);
        Assert.Equal((7, 1), (
            monster.RangedAvoidanceMoveTicks,
            monster.RangedAvoidanceTurn));
    }

    [Fact]
    public void VersionOneRoundTripsRangedAvoidanceMovementCounterStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "Magma",
                RangedAvoidanceMovementIdleTicks = 10,
                RangedAvoidanceMovementMode = 1,
            }],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        var monster = Assert.Single(restored.Monsters);
        Assert.Equal((10, 1), (
            monster.RangedAvoidanceMovementIdleTicks,
            monster.RangedAvoidanceMovementMode));
    }

    [Fact]
    public void VersionOneRoundTripsRhinoMoveGoalStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "Rhino",
                RhinoMovementTicks = 7,
                RhinoMovementTurn = 1,
            }],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        var monster = Assert.Single(restored.Monsters);
        Assert.Equal((7, 1), (
            monster.RhinoMovementTicks,
            monster.RhinoMovementTurn));
    }

    [Fact]
    public void VersionOneRoundTripsAiAvoidanceMoveGoalStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "GoatMelee",
                AvoidanceMovementTicks = 7,
                AvoidanceMovementTurn = 1,
            }],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        var monster = Assert.Single(restored.Monsters);
        Assert.Equal((7, 1), (
            monster.AvoidanceMovementTicks,
            monster.AvoidanceMovementTurn));
    }

    [Fact]
    public void VersionOneRoundTripsAiAvoidanceMovementCounterStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "GoatMelee",
                AvoidanceMovementIdleTicks = 10,
                AvoidanceMovementMode = 1,
            }],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        var monster = Assert.Single(restored.Monsters);
        Assert.Equal((10, 1), (
            monster.AvoidanceMovementIdleTicks,
            monster.AvoidanceMovementMode));
    }

    [Fact]
    public void VersionOneRoundTripsSkeletonMeleeDelayStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "SkeletonMelee",
                SkeletonMeleeDelayTicks = 7,
            }],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        Assert.Equal(7, Assert.Single(restored.Monsters).SkeletonMeleeDelayTicks);
    }

    [Fact]
    public void VersionOneRoundTripsSkeletonRangedMovementCounterStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "SkeletonRanged",
                SkeletonRangedMovementIdleTicks = 10,
                SkeletonRangedMovementMode = 1,
            }],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        var monster = Assert.Single(restored.Monsters);
        Assert.Equal((10, 1), (
            monster.SkeletonRangedMovementIdleTicks,
            monster.SkeletonRangedMovementMode));
    }

    [Fact]
    public void VersionOneRoundTripsFatMovementCounterStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "Fat",
                FatMovementIdleTicks = 10,
                FatMovementMode = 1,
            }],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        var monster = Assert.Single(restored.Monsters);
        Assert.Equal((10, 1), (
            monster.FatMovementIdleTicks,
            monster.FatMovementMode));
    }

    [Fact]
    public void VersionOneRoundTripsRhinoMovementAndDelayStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "Rhino",
                RhinoMovementIdleTicks = 10,
                RhinoMovementMode = 1,
                RhinoDelayTicks = 7,
            }],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        var monster = Assert.Single(restored.Monsters);
        Assert.Equal((10, 1, 7), (
            monster.RhinoMovementIdleTicks,
            monster.RhinoMovementMode,
            monster.RhinoDelayTicks));
    }

    [Fact]
    public void VersionOneRoundTripsSkeletonKingDelayStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "SkeletonKing",
                SkeletonKingDelayTicks = 7,
            }],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        Assert.Equal(7, Assert.Single(restored.Monsters).SkeletonKingDelayTicks);
    }

    [Fact]
    public void VersionOneRoundTripsHorkDemonDelayStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "HorkDemon",
                HorkDemonDelayTicks = 7,
            }],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        Assert.Equal(7, Assert.Single(restored.Monsters).HorkDemonDelayTicks);
    }

    [Fact]
    public void VersionOneRoundTripsRangedAvoidanceDelayStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "Magma",
                RangedAvoidanceDelayTicks = 7,
            }],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        Assert.Equal(7, Assert.Single(restored.Monsters).RangedAvoidanceDelayTicks);
    }

    [Fact]
    public void VersionOneRoundTripsSnakeDelayStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "Snake",
                SnakeDelayTicks = 7,
            }],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        Assert.Equal(7, Assert.Single(restored.Monsters).SnakeDelayTicks);
    }

    [Fact]
    public void VersionOneRoundTripsCounselorDelayStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "Counselor",
                CounselorDelayTicks = 7,
            }],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        Assert.Equal(7, Assert.Single(restored.Monsters).CounselorDelayTicks);
    }

    [Fact]
    public void VersionOneRoundTripsAvoidanceDelayStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "GoatMelee",
                AvoidanceDelayTicks = 7,
            }],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        Assert.Equal(7, Assert.Single(restored.Monsters).AvoidanceDelayTicks);
    }

    [Fact]
    public void VersionOneRoundTripsRangedDelayStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "FireBat",
                RangedDelayTicks = 7,
            }],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        Assert.Equal(7, Assert.Single(restored.Monsters).RangedDelayTicks);
    }

    [Fact]
    public void VersionOneRoundTripsMegaDelayStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "Mega",
                MegaDelayTicks = 7,
            }],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        Assert.Equal(7, Assert.Single(restored.Monsters).MegaDelayTicks);
    }

    [Fact]
    public void VersionOneRoundTripsCounselorRetreatStateThroughTheProtocolSave()
    {
        var baseline = CreateSnapshot();
        var source = baseline with {
            Monsters = [baseline.Monsters[0] with {
                NativeAi = "Counselor",
                CounselorRetreatTicks = 3,
            }],
        };

        var restored = new AuthoritativeSaveCodec().Deserialize(
            new AuthoritativeSaveCodec().Serialize(source));

        Assert.Equal(3, Assert.Single(restored.Monsters).CounselorRetreatTicks);
    }

    [Theory]
    [InlineData(
        DiabloPlayerActionKind.Death,
        DiabloPlayerCombatMode.Death,
        0,
        9U,
        40UL)]
    [InlineData(
        DiabloPlayerActionKind.LevelTransition,
        DiabloPlayerCombatMode.NewLevel,
        20,
        5U,
        11UL)]
    public void VersionOneRoundTripsTerminalPlayerActions(
        DiabloPlayerActionKind kind,
        DiabloPlayerCombatMode mode,
        int life,
        uint targetEntityId,
        ulong endTick)
    {
        var baseline = CreateSnapshot();
        var action = new AuthoritativePlayerActionState(
            kind,
            1,
            1,
            endTick,
            endTick) {
            EffectApplied = true,
            TargetEntityId = targetEntityId,
        };
        var source = baseline with {
            Player = baseline.Player with {
                Life = life,
                CombatMode = mode,
                CombatModeEndTick = endTick,
                CurrentAction = action,
            },
        };
        var codec = new AuthoritativeSaveCodec();

        var restored = codec.Deserialize(codec.Serialize(source));

        Assert.Equal(mode, restored.Player.CombatMode);
        Assert.Equal(endTick, restored.Player.CombatModeEndTick);
        Assert.Equal(action, restored.Player.CurrentAction);
    }

    private static DiabloSimulationSnapshot CreateSnapshot()
    {
        var player = new StorePlayerSnapshot(
            75,
            1,
            [],
            12,
            20,
            4,
            8,
            PlayerAttributesState.Zero,
            [],
            [],
            []) {
            EntityId = 7,
            LifeMaximum = 30,
            BaseLifeMaximum = 30,
            BaseManaMaximum = 8,
            CharacterLevel = 2,
            CharacterClassId = 3,
            CharacterName = "Dead-Monk",
            ManaShieldActive = true,
            ReflectCharges = 12,
            DiabloKillLevel = 1,
            AbilitySpellId = 46,
            FriendlyMode = false,
            FacingDirection = DiabloPlayerDirection.East,
            CombatMode = DiabloPlayerCombatMode.Spell,
            CombatModeEndTick = 13,
            CurrentAction = new AuthoritativePlayerActionState(
                DiabloPlayerActionKind.Cast,
                3,
                8,
                8,
                13) {
                EffectApplied = true,
                CastIntent = new DiabloCastIntent(
                    46,
                    0,
                    2,
                    3,
                    DiabloCastSourceKind.Ability),
            },
            MemorizedSpells = [new MemorizedSpellState(1, 2)],
            StatusEffects = [
                new AuthoritativeStatusEffect(9, 1584, 0) {
                    Behavior = AuthoritativeStatusEffectBehavior.Infravision,
                },
                new AuthoritativeStatusEffect(46, 255, 0) {
                    Behavior = AuthoritativeStatusEffectBehavior.Search,
                },
            ],
            HeldItem = new OwnedStoreItem(
                0,
                0,
                77,
                15,
                8,
                AuthoritativeItemState.Empty with {
                    ItemType = 11,
                    ItemIndex = 0,
                    Value = 15,
                    IdentifiedValue = 15,
                    StatFlag = true,
                    ManaToLifePercent = 50,
                    LifeToManaPercent = 40,
                }),
        };
        return new DiabloSimulationSnapshot(
            9,
            player,
            [new DiabloMonsterState(20, 3, 0, 1, 1, 0, 10, 2, 1, 4, 0, 0, 0, 100) {
                MonsterLevel = 7,
                Moving = true,
                FuturePositionX = 2,
                FuturePositionY = 1,
                Petrified = true,
                StoneCurseImmune = true,
                IsUndead = true,
                IsDemon = true,
                IsAnimal = true,
                HasKnockback = true,
                DeathAnimationTicks = 3,
                DeathTicksRemaining = 2,
                NativeAi = "Fallen",
                FallenFearTicks = 5,
                FallenFearDirection = 7,
                HitPointsQ6 = 0,
            }],
            [new DiabloWorldItemState(
                21,
                0,
                2,
                2,
                1633955154,
                0,
                AuthoritativeItemState.Empty with {
                    CreateInfo = 17509,
                    ItemType = 0,
                    EquipLocation = 7,
                    ItemClass = 3,
                    MiscId = 44,
                    ItemIndex = 23,
                    Value = 3,
                    BaseName = "Ear of Dead-RogueDM",
                    IdentifiedName = "Dead-RogueDM",
                    NativeCursorGraphic = 21,
                })],
            [new DiabloObjectState(
                20,
                4,
                0,
                1,
                1,
                false,
                0,
                AuthoritativeObjectEffectKind.None,
                0) {
                Breakable = true,
                Destroyed = true,
            }],
            [],
            [new DiabloProjectileState(
                0x80000000,
                7,
                0,
                30,
                0,
                1,
                1,
                4,
                0,
                7,
                AuthoritativeDamageType.Lightning,
                0,
                252) {
                Behavior = AuthoritativeProjectileBehavior.ChargedBolt,
                StartX = 0,
                StartY = 0,
                TraveledY = 1048576,
                VelocityY = 262144,
                OriginalDirection = 7,
                PathIndex = 14,
                PathTicks = 13,
                Speed = 8,
                ImpactLifetime = 8,
                ResistantDamageDivisor = 4,
                EffectiveSpellLevel = 3,
                LastCollisionTargetEntityId = 9,
            }, new DiabloProjectileState(
                0x80000001,
                7,
                0,
                4,
                0,
                5,
                5,
                5,
                5,
                294,
                AuthoritativeDamageType.Magic,
                1,
                18) {
                Behavior = AuthoritativeProjectileBehavior.FlashTop,
                StartX = 5,
                StartY = 5,
                ResistantDamageDivisor = 4,
                EffectiveSpellLevel = 3,
            }, new DiabloProjectileState(
                0x80000002,
                7,
                0,
                23,
                0,
                6,
                6,
                6,
                6,
                0,
                AuthoritativeDamageType.Magic,
                0,
                1) {
                Behavior = AuthoritativeProjectileBehavior.Teleport,
                StartX = 6,
                StartY = 6,
            }, new DiabloProjectileState(
                0x80000003,
                7,
                0,
                10,
                0,
                7,
                6,
                7,
                6,
                0,
                AuthoritativeDamageType.Magic,
                0,
                2) {
                Behavior = AuthoritativeProjectileBehavior.Phasing,
                StartX = 7,
                StartY = 6,
            }, new DiabloProjectileState(
                0x80000004,
                7,
                0,
                12,
                0,
                8,
                6,
                12,
                6,
                58,
                AuthoritativeDamageType.Fire,
                1,
                9) {
                Behavior = AuthoritativeProjectileBehavior.Fireball,
                Phase = AuthoritativeProjectilePhase.Impact,
                StartX = 7,
                StartY = 6,
                TraveledX = 2038960,
                TraveledY = 1019480,
                OriginalDirection = 7,
                Speed = 22,
                ImpactLifetime = 14,
                ResistantDamageDivisor = 4,
                EffectiveSpellLevel = 3,
                LastCollisionTargetEntityId = 9,
            }, new DiabloProjectileState(
                0x80000005,
                7,
                9,
                0,
                0,
                8,
                6,
                10,
                6,
                10,
                AuthoritativeDamageType.Physical,
                0,
                3) {
                Behavior = AuthoritativeProjectileBehavior.WeaponArrow,
                StartX = 8,
                StartY = 6,
                DamageQ6 = 640,
                ElementalFireDamage = 4,
                ElementalLightningDamage = 6,
                AttackHit = true,
                PerilSelfDamageQ6 = 128,
                DoppelgangerProc = true,
                ActivationDelayTicks = 2,
            }],
            new DiabloStoreState(1, [new StoreItem(0, 42, 10)])) {
            RandomState = 0x49BFD245,
            VictoryTriggered = true,
            Difficulty = DiabloDifficulty.Hell,
        };
    }
}

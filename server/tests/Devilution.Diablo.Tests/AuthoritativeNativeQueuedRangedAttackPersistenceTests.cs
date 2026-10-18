using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed partial class AuthoritativeCathedralNativePopulationIntegrationTests
{
    [Fact(Explicit = true)]
    public void NativeDepthQueuedRangedTargetCapturesAndRestoresOnlyInV10()
    {
        using var setup = CreateSarcophagusSetup(captureOptions: false, progressionSemantics: true);
        Assert.NotNull(setup);
        var activation = DiabloModule.Instance.CathedralNativeDepthProgressionContentBootstrap
            .ActivateNativeDepthProgression(setup!.Fixture.Content, 0x13579BDF);
        var simulation = activation.Simulation;
        var world = simulation.WorldState;
        Assert.True(activation.TownerGreetings!.TryGet(1, out var pepin));
        var startOffset = Enumerable.Range(1, 8)
            .Select(value => AuthoritativeNativePlayerPathfinder.GetDisplacement(value))
            .First(offset => world.ContainsPosition(0, pepin.PositionX + offset.X, pepin.PositionY + offset.Y)
                && world.IsWalkable(0, pepin.PositionX + offset.X, pepin.PositionY + offset.Y)
                && !world.IsOccupied(0, pepin.PositionX + offset.X, pepin.PositionY + offset.Y, []));
        var startX = pepin.PositionX + startOffset.X;
        var startY = pepin.PositionY + startOffset.Y;
        var moveDirection = Enumerable.Range(1, 8)
            .Select(value => (Code: value, Offset: AuthoritativeNativePlayerPathfinder.GetDisplacement(value)))
            .First(value => world.ContainsPosition(0, startX + value.Offset.X, startY + value.Offset.Y)
                && world.IsWalkable(0, startX + value.Offset.X, startY + value.Offset.Y)
                && !world.IsOccupied(0, startX + value.Offset.X, startY + value.Offset.Y, []));
        var rogue = activation.PlayerClasses.Definitions.Single(row => row.Symbol == "rogue");
        const string session = "native-depth-queued-ranged";
        var runtime = simulation.CreateRuntime(activation.PlayerLoadouts.CreateTemplate(
            session, rogue.ClassId, null, null, null, startingLevelId: 0,
            startingPositionX: startX, startingPositionY: startY));
        runtime.Write(session, player => player.EntityId = 0x70000001);
        runtime.AdvanceTo(1);
        var player = runtime.Read(session, value => value);
        var mode = (moveDirection.Offset.X, moveDirection.Offset.Y) switch {
            (1, 1) or (0, 1) or (1, 0) => DiabloPlayerCombatMode.WalkSouthwards,
            (-1, 0) or (-1, -1) or (0, -1) => DiabloPlayerCombatMode.WalkNorthwards,
            (-1, 1) or (1, -1) => DiabloPlayerCombatMode.WalkSideways,
            _ => throw new InvalidOperationException("The pathfinder returned an invalid native walk direction."),
        };
        var staleEntityTarget = new DiabloNativeQueuedRangedAttackState(0, 0x7FFFFFFE, 0, 0, false);
        runtime.Write(session, value => {
            value.CurrentAction = new AuthoritativePlayerActionState(
                DiabloPlayerActionKind.Move, 1, 2, 10, 10) {
                DirectionX = moveDirection.Offset.X,
                DirectionY = moveDirection.Offset.Y,
            };
            value.CombatMode = mode;
            value.CombatModeEndTick = 10;
            value.NativeWalkPath = null;
            value.NativeQueuedRangedAttack = staleEntityTarget;
        });

        var codec = new DiabloCohortCodec();
        var checkpoint = simulation.CaptureCohortState(runtime);
        Assert.Equal(DiabloCohortState.NativeDepthProgressionFormatVersion, checkpoint.FormatVersion);
        Assert.Equal(staleEntityTarget, checkpoint.Participants.Single().Player.NativeQueuedRangedAttack);
        var bytes = codec.Serialize(checkpoint);
        Assert.Contains("\"NativeQueuedRangedAttack\"", bytes);
        var decoded = codec.Deserialize(bytes);
        Assert.Equal(staleEntityTarget, decoded.Participants.Single().Player.NativeQueuedRangedAttack);

        var candidate = activation.CohortCandidateFactory!(decoded);
        var restored = candidate.CreateRuntime();
        Assert.Equal(bytes, codec.Serialize(candidate.Simulation.CaptureCohortState(restored)));
        Assert.Equal(staleEntityTarget,
            restored.Read(session, value => value.NativeQueuedRangedAttack));
        restored.AdvanceTo(10);
        Assert.Null(restored.Read(session, value => value.NativeQueuedRangedAttack));
        Assert.Null(restored.Read(session, value => value.CurrentAction));
        Assert.Equal(checkpoint.RandomState,
            candidate.Simulation.CaptureCohortState(restored).RandomState);

        var withoutPending = decoded with {
            Participants = [decoded.Participants.Single() with {
                Player = decoded.Participants.Single().Player with { NativeQueuedRangedAttack = null },
            }],
        };
        var absentBytes = codec.Serialize(withoutPending);
        Assert.DoesNotContain("\"NativeQueuedRangedAttack\"", absentBytes);
        Assert.Equal(absentBytes, codec.Serialize(codec.Deserialize(absentBytes)));

        var legacy = withoutPending with {
            FormatVersion = DiabloCohortState.NativeProgressionFormatVersion,
        };
        var legacyBytes = codec.Serialize(legacy);
        Assert.DoesNotContain("\"NativeQueuedRangedAttack\"", legacyBytes);
        Assert.Equal(legacyBytes, codec.Serialize(codec.Deserialize(legacyBytes)));
        Assert.Throws<InvalidDataException>(() => codec.Serialize(legacy with {
            Participants = [legacy.Participants.Single() with {
                Player = legacy.Participants.Single().Player with {
                    NativeQueuedRangedAttack = staleEntityTarget,
                },
            }],
        }));

        Assert.Throws<InvalidDataException>(() => codec.Serialize(decoded with {
            Participants = [decoded.Participants.Single() with {
                Player = decoded.Participants.Single().Player with {
                    CurrentAction = null,
                    CombatMode = DiabloPlayerCombatMode.Stand,
                    CombatModeEndTick = 0,
                },
            }],
        }));

        var recoveryCellTarget = new DiabloNativeQueuedRangedAttackState(
            0, 0, pepin.PositionX, pepin.PositionY, true);
        var recovery = decoded with {
            Participants = [decoded.Participants.Single() with {
                Player = decoded.Participants.Single().Player with {
                    CombatMode = DiabloPlayerCombatMode.GotHit,
                    CombatModeEndTick = checked(decoded.Tick + 5),
                    CurrentAction = null,
                    NativeQueuedRangedAttack = recoveryCellTarget,
                },
            }],
        };
        var recoveryBytes = codec.Serialize(recovery);
        Assert.Equal(recoveryCellTarget,
            codec.Deserialize(recoveryBytes).Participants.Single().Player.NativeQueuedRangedAttack);

        var rangedPendingTarget = new DiabloNativeQueuedRangedAttackState(
            0, 0, pepin.PositionX, pepin.PositionY, true);
        var rangedPending = decoded with {
            Tick = 2,
            Participants = [decoded.Participants.Single() with {
                Player = decoded.Participants.Single().Player with {
                    CurrentAction = new AuthoritativePlayerActionState(
                        DiabloPlayerActionKind.Attack, 1, 3, 4, 8) {
                        TargetCell = true,
                        TargetX = pepin.PositionX,
                        TargetY = pepin.PositionY,
                    },
                    CombatMode = DiabloPlayerCombatMode.RangedAttack,
                    CombatModeEndTick = 8,
                    NativeQueuedRangedAttack = rangedPendingTarget,
                },
            }],
        };
        var rangedPendingBytes = codec.Serialize(rangedPending);
        var rangedPendingRoundTrip = codec.Deserialize(rangedPendingBytes);
        Assert.Equal(rangedPendingTarget,
            rangedPendingRoundTrip.Participants.Single().Player.NativeQueuedRangedAttack);
        Assert.Equal(rangedPending.Participants.Single().Player.CurrentAction,
            rangedPendingRoundTrip.Participants.Single().Player.CurrentAction);
        Assert.Equal(rangedPendingBytes, codec.Serialize(rangedPendingRoundTrip));

        var readyWithPending = rangedPending with {
            Tick = 4,
            Participants = [rangedPending.Participants.Single() with {
                Player = rangedPending.Participants.Single().Player with {
                    CurrentAction = rangedPending.Participants.Single().Player.CurrentAction! with {
                        EffectApplied = true,
                    },
                },
            }],
        };
        Assert.Throws<InvalidDataException>(() => codec.Serialize(readyWithPending));

        var legacyRangedPending = legacy with {
            Participants = [legacy.Participants.Single() with {
                Player = legacy.Participants.Single().Player with {
                    CurrentAction = rangedPending.Participants.Single().Player.CurrentAction,
                    CombatMode = DiabloPlayerCombatMode.RangedAttack,
                    CombatModeEndTick = 8,
                    NativeQueuedRangedAttack = rangedPendingTarget,
                },
            }],
        };
        Assert.Throws<InvalidDataException>(() => codec.Serialize(legacyRangedPending));

        Assert.Throws<InvalidDataException>(() => codec.Serialize(decoded with {
            Participants = [decoded.Participants.Single() with {
                Player = decoded.Participants.Single().Player with {
                    NativeQueuedRangedAttack = staleEntityTarget with { LevelId = 1 },
                },
            }],
        }));
        Assert.Throws<InvalidDataException>(() => codec.Serialize(decoded with {
            Participants = [decoded.Participants.Single() with {
                Player = decoded.Participants.Single().Player with {
                    NativeQueuedRangedAttack = staleEntityTarget with { TargetEntityId = 0 },
                },
            }],
        }));
    }
}

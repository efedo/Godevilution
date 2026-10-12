using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Focused coverage for the ordered command-bearing module tick seam. */
public sealed class DiabloSimulationTickCommandTests
{
    [Fact]
    public void CommandCallbacksRunOnceInParticipantOrderAndPreserveEventSlots()
    {
        var simulation = CreateSimulation();
        var first = CreatePlayer(11);
        var second = CreatePlayer(22);
        var callbackOrder = new List<uint>();

        var events = simulation.AdvanceTickWithCommands(
            1,
            [first, second],
            player => {
                callbackOrder.Add(player.EntityId);
                if (ReferenceEquals(player, first))
                    second.Life = 17;
                return [new DiabloDirectedGameplayEvent(
                    player,
                    new DiabloGameplayEvent(
                        DiabloGameplayEventKind.MapOpened,
                        player.EntityId,
                        player.EntityId,
                        0))];
            });

        Assert.Equal([11U, 22U], callbackOrder);
        Assert.Equal(17, second.Life);
        Assert.Equal([11U, 22U], events.Select(delivery => delivery.Recipient.EntityId));
    }

    [Fact]
    public void VictoryStillInvokesEveryCallbackWithoutAllowingGameplayExecution()
    {
        var simulation = CreateSimulation();
        simulation.WorldState.VictoryTriggered = true;
        var first = CreatePlayer(11);
        var second = CreatePlayer(22);
        var callbackOrder = new List<uint>();

        var events = simulation.AdvanceTickWithCommands(
            4,
            [first, second],
            player => {
                callbackOrder.Add(player.EntityId);
                return [new DiabloDirectedGameplayEvent(
                    player,
                    new DiabloGameplayEvent(
                        DiabloGameplayEventKind.Victory,
                        player.EntityId,
                        player.EntityId,
                        0))];
            });

        Assert.Equal([11U, 22U], callbackOrder);
        Assert.Equal(2, events.Count);
        Assert.True(simulation.Execute(
            first,
            new DiabloAttackIntent(99),
            4,
            [first, second]).Failure == DiabloRuleFailure.NotAllowed);
    }

    [Fact]
    public void CompletedAttackOrdersOldEffectCommandReceiptAndReplacementEffect()
    {
        var target = new AuthoritativeCombatTarget(99, 1, 0, 100, petrified: true);
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
                FallbackWidth = 8,
                FallbackHeight = 8,
                FallbackBlockedCells = [],
                FallbackMissileBlockedCells = [],
                CombatTargets = [target],
            });
        var first = CreatePlayer(11);
        var second = CreatePlayer(22, positionX: 4);
        first.CombatMode = DiabloPlayerCombatMode.Attack;
        first.CombatModeEndTick = 1;
        first.CurrentAction = new AuthoritativePlayerActionState(
            DiabloPlayerActionKind.Attack,
            0,
            1,
            1,
            1) { TargetEntityId = target.EntityId };

        var callbackOrder = new List<uint>();
        var lifeAfterOld = 0;
        var targetLifeSeenBySecond = target.HitPoints;
        var events = simulation.AdvanceTickWithCommands(
            1,
            [first, second],
            player => {
                callbackOrder.Add(player.EntityId);
                if (ReferenceEquals(player, first)) {
                    lifeAfterOld = target.HitPoints;
                    player.CombatMode = DiabloPlayerCombatMode.Attack;
                    player.CombatModeEndTick = 1;
                    player.CurrentAction = new AuthoritativePlayerActionState(
                        DiabloPlayerActionKind.Attack,
                        1,
                        1,
                        1,
                        1) { TargetEntityId = target.EntityId };
                    return [new DiabloDirectedGameplayEvent(
                        player,
                        new DiabloGameplayEvent(
                            DiabloGameplayEventKind.MapOpened,
                            player.EntityId,
                            player.EntityId,
                            0))];
                } else if (ReferenceEquals(player, second)) {
                    targetLifeSeenBySecond = target.HitPoints;
                }
                return [];
            });

        Assert.Equal([11U, 22U], callbackOrder);
        Assert.True(lifeAfterOld < 100);
        Assert.True(targetLifeSeenBySecond < lifeAfterOld);
        Assert.Null(first.CurrentAction);
        Assert.Equal(
            [
                DiabloGameplayEventKind.Damage,
                DiabloGameplayEventKind.MapOpened,
                DiabloGameplayEventKind.Damage,
            ],
            events.Select(delivery => delivery.Event.Kind));
    }

    [Fact]
    public void NullCommandCallbackIsRejectedBeforeChangingTheWorld()
    {
        var simulation = CreateSimulation();
        var player = CreatePlayer(11);

        Assert.Throws<ArgumentNullException>(() => simulation.AdvanceTickWithCommands(
            1,
            [player],
            null!));
        Assert.Equal((0, 0), (player.PositionX, player.PositionY));
    }

    [Theory]
    [InlineData(DiabloPlayerCombatMode.Block)]
    [InlineData(DiabloPlayerCombatMode.GotHit)]
    public void BlockAndHitRecoveryCompletionRunsReplacementImmediateWork(
        DiabloPlayerCombatMode completedMode)
    {
        var simulation = CreateSimulation();
        var first = CreatePlayer(11);
        var second = CreatePlayer(22, positionX: 4);
        first.CombatMode = completedMode;
        first.CombatModeEndTick = 1;
        var secondObservedPosition = -1;

        simulation.AdvanceTickWithCommands(
            1,
            [first, second],
            player => {
                if (ReferenceEquals(player, first)) {
                    player.CombatMode = DiabloPlayerCombatMode.WalkSouthwards;
                    player.CombatModeEndTick = 1;
                    player.CurrentAction = new AuthoritativePlayerActionState(
                        DiabloPlayerActionKind.Move,
                        1,
                        1,
                        1,
                        1) {
                        DirectionX = 1,
                        DirectionY = 0,
                    };
                } else {
                    secondObservedPosition = first.PositionX;
                }
                return [];
            });

        Assert.Equal(1, secondObservedPosition);
        Assert.Null(first.CurrentAction);
    }

    [Theory]
    [InlineData(DiabloPlayerCombatMode.Block)]
    [InlineData(DiabloPlayerCombatMode.GotHit)]
    public void BlockAndHitCompletionUseFirstFrameForTheNextNativeAttack(
        DiabloPlayerCombatMode completedMode)
    {
        var simulation = CreateNativeTimingSimulation(
            1U << 18,
            ranged: false,
            out var template);
        var player = template.CreatePlayer();
        player.EntityId = 11;
        player.CombatMode = completedMode;
        player.CombatModeEndTick = 1;

        simulation.AdvanceTickWithCommands(
            1,
            [player],
            state => {
                Assert.True(simulation.Execute(
                    state,
                    new DiabloAttackIntent(99),
                    1,
                    [state]).Succeeded);
                return [];
            });

        Assert.Equal((7UL, 8UL, 14UL), (
            player.CurrentAction!.EffectTick,
            player.CurrentAction.ReadyTick,
            player.CurrentAction.EndTick));
    }

    [Theory]
    [InlineData(DiabloPlayerActionKind.LevelTransition, DiabloPlayerCombatMode.NewLevel)]
    [InlineData(DiabloPlayerActionKind.RestartTown, DiabloPlayerCombatMode.NewLevel)]
    [InlineData(DiabloPlayerActionKind.Death, DiabloPlayerCombatMode.Death)]
    public void NonPrimaryCleanupDoesNotEnterCompletionLoop(
        DiabloPlayerActionKind actionKind,
        DiabloPlayerCombatMode mode)
    {
        var simulation = CreateSimulation();
        var player = CreatePlayer(11);
        player.CombatMode = mode;
        player.CombatModeEndTick = 1;
        player.CurrentAction = new AuthoritativePlayerActionState(
            actionKind,
            0,
            1,
            1,
            1) {
            EffectApplied = true,
        };

        simulation.AdvanceTickWithCommands(
            1,
            [player],
            state => {
                state.CombatMode = DiabloPlayerCombatMode.WalkSouthwards;
                state.CombatModeEndTick = 1;
                state.CurrentAction = new AuthoritativePlayerActionState(
                    DiabloPlayerActionKind.Move,
                    1,
                    1,
                    1,
                    1) {
                    DirectionX = 1,
                    DirectionY = 0,
                };
                return [];
            });

        Assert.Equal((0, 0), (player.PositionX, player.PositionY));
        Assert.NotNull(player.CurrentAction);
        Assert.False(player.CurrentAction!.EffectApplied);
    }

    [Theory]
    [InlineData(DiabloPlayerActionKind.LevelTransition)]
    [InlineData(DiabloPlayerActionKind.RestartTown)]
    public void LevelCleanupCommandsDoNotUseCompletionFirstFrame(
        DiabloPlayerActionKind actionKind)
    {
        var simulation = CreateNativeTimingSimulation(
            1U << 18,
            ranged: false,
            out var template);
        var player = template.CreatePlayer();
        player.EntityId = 11;
        player.CombatMode = DiabloPlayerCombatMode.NewLevel;
        player.CombatModeEndTick = 1;
        player.CurrentAction = new AuthoritativePlayerActionState(
            actionKind,
            0,
            1,
            1,
            1) {
            EffectApplied = true,
        };

        simulation.AdvanceTickWithCommands(
            1,
            [player],
            state => {
                Assert.True(simulation.Execute(
                    state,
                    new DiabloAttackIntent(99),
                    1,
                    [state]).Succeeded);
                return [];
            });

        Assert.Equal((8UL, 9UL, 15UL), (
            player.CurrentAction!.EffectTick,
            player.CurrentAction.ReadyTick,
            player.CurrentAction.EndTick));
    }

    [Fact]
    public void DeathCleanupDoesNotOpenACompletionAttackWindow()
    {
        var simulation = CreateNativeTimingSimulation(
            1U << 18,
            ranged: false,
            out var template);
        var player = template.CreatePlayer();
        player.EntityId = 11;
        player.CombatMode = DiabloPlayerCombatMode.Death;
        player.CombatModeEndTick = 1;
        player.CurrentAction = new AuthoritativePlayerActionState(
            DiabloPlayerActionKind.Death,
            0,
            1,
            1,
            1) {
            EffectApplied = true,
        };

        simulation.AdvanceTickWithCommands(
            1,
            [player],
            state => {
                var result = simulation.Execute(
                    state,
                    new DiabloAttackIntent(99),
                    1,
                    [state]);
                Assert.Equal(DiabloRuleFailure.NotAllowed, result.Failure);
                return [];
            });

        Assert.Equal(DiabloPlayerCombatMode.Death, player.CombatMode);
        Assert.Null(player.CurrentAction);
    }

    [Fact]
    public void CallbackExceptionClearsNativeCallerPhaseBeforeLegacyExecution()
    {
        var simulation = CreateNativeTimingSimulation(
            1U << 18,
            ranged: false,
            out var template);
        var player = template.CreatePlayer();
        player.EntityId = 11;
        Assert.True(simulation.Execute(
            player,
            new DiabloAttackIntent(99),
            10,
            [player]).Succeeded);
        var readyTick = player.CurrentAction!.ReadyTick;

        Assert.Throws<InvalidOperationException>(() => simulation.AdvanceTickWithCommands(
            readyTick,
            [player],
            state => throw new InvalidOperationException("callback failure")));

        Assert.True(simulation.Execute(
            player,
            new DiabloAttackIntent(99),
            readyTick,
            [player]).Succeeded);
        Assert.Equal(31UL, player.CurrentAction!.EndTick);
    }

    [Fact]
    public void RejectedCallbackCommandDoesNotLeakNativeCallerPhase()
    {
        var simulation = CreateNativeTimingSimulation(
            1U << 18,
            ranged: false,
            out var template);
        var player = template.CreatePlayer();
        player.EntityId = 11;
        Assert.True(simulation.Execute(
            player,
            new DiabloAttackIntent(99),
            10,
            [player]).Succeeded);
        var readyTick = player.CurrentAction!.ReadyTick;

        simulation.AdvanceTickWithCommands(
            readyTick,
            [player],
            state => {
                var rejected = simulation.Execute(
                    state,
                    new DiabloAttackIntent(404),
                    readyTick,
                    [state]);
                Assert.Equal(DiabloRuleFailure.InvalidTarget, rejected.Failure);
                return [];
            });

        Assert.True(simulation.Execute(
            player,
            new DiabloAttackIntent(99),
            readyTick,
            [player]).Succeeded);
        Assert.Equal(31UL, player.CurrentAction!.EndTick);
    }

    [Fact]
    public void NativeCallerPhaseIsBoundToTheCallbackActor()
    {
        var simulation = CreateNativeTimingSimulation(
            1U << 18,
            ranged: false,
            out var template);
        var first = template.CreatePlayer();
        var second = template.CreatePlayer();
        first.EntityId = 11;
        second.EntityId = 22;
        Assert.True(simulation.Execute(
            first,
            new DiabloAttackIntent(99),
            10,
            [first, second]).Succeeded);
        Assert.True(simulation.Execute(
            second,
            new DiabloAttackIntent(99),
            10,
            [first, second]).Succeeded);
        var readyTick = first.CurrentAction!.ReadyTick;

        simulation.AdvanceTickWithCommands(
            readyTick,
            [first, second],
            player => {
                if (ReferenceEquals(player, second)) {
                    Assert.True(simulation.Execute(
                        first,
                        new DiabloAttackIntent(99),
                        readyTick,
                        [first, second]).Succeeded);
                }
                return [];
            });

        Assert.Equal(31UL, first.CurrentAction!.EndTick);
    }

    [Fact]
    public void LegacyAdvanceTickDoesNotEnterTheCommandCallbackSeam()
    {
        var simulation = CreateSimulation();
        var player = CreatePlayer(11);

        var events = simulation.AdvanceTick(1, [player]);

        Assert.Empty(events);
        Assert.Null(player.CurrentAction);
    }

    [Fact]
    public void ExecuteTickCatchesUpAndReturnsMultipleCommandResultsInInputOrder()
    {
        var simulation = CreateSimulation();
        var runtime = simulation.CreateRuntime(
            DiabloPlayerTemplate.CreateDefault(0, 50, 0, 0));
        runtime.Write("first", state => {
            state.EntityId = 11;
            state.Life = 50;
        });
        runtime.Write("second", state => {
            state.EntityId = 22;
            state.Life = 50;
        });

        var callbackOrder = new List<uint>();
        var results = runtime.ExecuteTick(
            3,
            [
                new SimulationTickCommand<AuthoritativePlayerState, DiabloGameplayEvent, int>(
                    "second",
                    (state, participants) => {
                        callbackOrder.Add(state.EntityId);
                        return new SimulationStepResult<int, DiabloGameplayEvent>(
                            22,
                            [new DiabloGameplayEvent(
                                DiabloGameplayEventKind.MapOpened,
                                state.EntityId,
                                state.EntityId,
                                0)]);
                    }),
                new SimulationTickCommand<AuthoritativePlayerState, DiabloGameplayEvent, int>(
                    "first",
                    (state, participants) => {
                        callbackOrder.Add(state.EntityId);
                        return new SimulationStepResult<int, DiabloGameplayEvent>(
                            11,
                            [new DiabloGameplayEvent(
                                DiabloGameplayEventKind.MapOpened,
                                state.EntityId,
                                state.EntityId,
                                0)]);
                    }),
            ]);

        Assert.Equal([11U, 22U], callbackOrder);
        Assert.Equal([22, 11], results);
        Assert.Equal(3UL, runtime.LastAdvancedTick);
        Assert.Single(runtime.DrainEvents("first"));
        Assert.Single(runtime.DrainEvents("second"));
    }

    [Fact]
    public void ExecuteTickRunsEachActorCallbackOnceForMultipleCommands()
    {
        var simulation = CreateSimulation();
        var runtime = simulation.CreateRuntime(
            DiabloPlayerTemplate.CreateDefault(0, 50, 0, 0));
        var callbackCount = 0;
        runtime.Write("player", state => state.EntityId = 11);

        var results = runtime.ExecuteTick(
            1,
            [
                new SimulationTickCommand<AuthoritativePlayerState, DiabloGameplayEvent, int>(
                    "player",
                    (state, participants) => {
                        callbackCount++;
                        return new SimulationStepResult<int, DiabloGameplayEvent>(1);
                    }),
                new SimulationTickCommand<AuthoritativePlayerState, DiabloGameplayEvent, int>(
                    "player",
                    (state, participants) => {
                        callbackCount++;
                        return new SimulationStepResult<int, DiabloGameplayEvent>(2);
                    }),
            ]);

        Assert.Equal(2, callbackCount);
        Assert.Equal([1, 2], results);
    }

    [Fact]
    public void ExecuteTickStartsIdleMoveAndCastInParticipantOrderAndRejectsSecondMove()
    {
        var (simulation, template, target, animation) = CreateNativeMoveCastFixture();
        Assert.Equal((8, 20, 14), (
            animation.WalkingFrames,
            animation.CastingFrames,
            animation.CastingActionFrame));
        var runtime = simulation.CreateRuntime(template);
        runtime.Write("a-caster", state => state.EntityId = 11);
        runtime.Write("z-mover", state => {
            state.EntityId = 22;
            state.PositionY = 4;
        });
        Assert.Equal(DiabloPlayerCombatMode.Stand, runtime.Read("a-caster", state => state.CombatMode));
        Assert.Null(runtime.Read("a-caster", state => state.CurrentAction));
        Assert.Equal(DiabloPlayerCombatMode.Stand, runtime.Read("z-mover", state => state.CombatMode));
        Assert.Null(runtime.Read("z-mover", state => state.CurrentAction));
        var operationOrder = new List<(uint EntityId, string Intent)>();
        var targetHitPointsBefore = target.HitPoints;

        var results = runtime.ExecuteTick<DiabloRuleFailure?>(
            1,
            [
                CreateMoveTickCommand(
                    "z-mover",
                    simulation,
                    1,
                    1,
                    0,
                    operationOrder),
                CreateCastTickCommand(
                    "a-caster",
                    simulation,
                    1,
                    new DiabloCastIntent(7, 99, 2, 0),
                    operationOrder),
                CreateMoveTickCommand(
                    "z-mover",
                    simulation,
                    1,
                    0,
                    1,
                    operationOrder),
            ]);

        Assert.Equal(
            new (uint EntityId, string Intent)[] {
                (11, "Cast"),
                (22, "Move"),
                (22, "Move"),
            },
            operationOrder);
        Assert.Equal(
            new DiabloRuleFailure?[] {
                null,
                null,
                DiabloRuleFailure.NotAllowed,
            },
            results);
        Assert.Equal(1UL, runtime.LastAdvancedTick);

        var caster = runtime.Read("a-caster", state => state);
        var cast = Assert.IsType<AuthoritativePlayerActionState>(caster.CurrentAction);
        var expectedCastEffectTick = 1UL + (ulong)animation.CastingActionFrame;
        Assert.Equal(DiabloPlayerActionKind.Cast, cast.Kind);
        Assert.Equal((1UL, expectedCastEffectTick, expectedCastEffectTick,
            1UL + (ulong)(animation.CastingFrames - 1)), (
            cast.StartedAtTick,
            cast.EffectTick,
            cast.ReadyTick,
            cast.EndTick));
        Assert.Equal(DiabloPlayerCombatMode.Spell, caster.CombatMode);
        Assert.Equal(10, caster.Mana);
        Assert.Equal(targetHitPointsBefore, target.HitPoints);

        var mover = runtime.Read("z-mover", state => state);
        var move = Assert.IsType<AuthoritativePlayerActionState>(mover.CurrentAction);
        var expectedMoveEndTick = 1UL + (ulong)animation.WalkingFrames;
        Assert.Equal(DiabloPlayerActionKind.Move, move.Kind);
        Assert.Equal((1UL, expectedMoveEndTick, expectedMoveEndTick, expectedMoveEndTick), (
            move.StartedAtTick,
            move.EffectTick,
            move.ReadyTick,
            move.EndTick));
        Assert.Equal((0, 4), (mover.PositionX, mover.PositionY));
        Assert.Equal((1, 4), (mover.FuturePositionX, mover.FuturePositionY));
        Assert.Equal(targetHitPointsBefore, target.HitPoints);
        Assert.Empty(runtime.DrainEvents("a-caster"));
        Assert.Empty(runtime.DrainEvents("z-mover"));
    }

    [Fact]
    public void ExecuteTickCompletesMoveBeforeAdmittingCastWithNativeFrameTiming()
    {
        var (simulation, template, target, animation) = CreateNativeMoveCastFixture();
        var runtime = simulation.CreateRuntime(template);
        runtime.Write("player", state => state.EntityId = 11);
        var targetHitPointsBefore = target.HitPoints;

        var moveResults = runtime.ExecuteTick<DiabloRuleFailure?>(
            1,
            [CreateMoveTickCommand("player", simulation, 1, 1, 0)]);
        Assert.Equal(new DiabloRuleFailure?[] { null }, moveResults);
        var move = runtime.Read("player", state => state.CurrentAction)!;
        var moveEndTick = 1UL + (ulong)animation.WalkingFrames;
        Assert.Equal(DiabloPlayerActionKind.Move, move.Kind);
        Assert.Equal(moveEndTick, move.EffectTick);
        Assert.Equal(moveEndTick, move.EndTick);

        var completionObserved = false;
        var castResults = runtime.ExecuteTick<DiabloRuleFailure?>(
            moveEndTick,
            [
                new SimulationTickCommand<AuthoritativePlayerState, DiabloGameplayEvent, DiabloRuleFailure?>(
                    "player",
                    (state, participants) => {
                        completionObserved = state.PositionX == 1
                            && state.PositionY == 0
                            && state.CombatMode == DiabloPlayerCombatMode.Stand
                            && state.CurrentAction is null;
                        var result = simulation.Execute(
                            state,
                            new DiabloCastIntent(7, 99, 2, 0),
                            moveEndTick,
                            participants);
                        return new SimulationStepResult<DiabloRuleFailure?, DiabloGameplayEvent>(
                            result.Failure,
                            result.Events);
                    }),
            ]);

        Assert.True(completionObserved);
        Assert.Equal(new DiabloRuleFailure?[] { null }, castResults);
        var player = runtime.Read("player", state => state);
        var cast = Assert.IsType<AuthoritativePlayerActionState>(player.CurrentAction);
        var expectedEffectTick = moveEndTick + (ulong)animation.CastingActionFrame;
        var expectedEndTick = moveEndTick + (ulong)(animation.CastingFrames - 1);
        Assert.Equal(DiabloPlayerActionKind.Cast, cast.Kind);
        Assert.Equal((moveEndTick, expectedEffectTick, expectedEffectTick, expectedEndTick), (
            cast.StartedAtTick,
            cast.EffectTick,
            cast.ReadyTick,
            cast.EndTick));
        Assert.Equal((1, 0), (player.PositionX, player.PositionY));
        Assert.Equal(10, player.Mana);
        Assert.Equal(targetHitPointsBefore, target.HitPoints);

        runtime.ExecuteTick<DiabloRuleFailure?>(expectedEffectTick, []);
        Assert.Equal(targetHitPointsBefore - 6, target.HitPoints);
        Assert.Equal(6, player.Mana);
        var damage = Assert.Single(runtime.DrainEvents("player"));
        Assert.Equal(DiabloGameplayEventKind.Damage, damage.Kind);
        Assert.Equal(99U, damage.TargetEntityId);
        Assert.Equal(6, damage.Amount);
        Assert.True(runtime.Read("player", state => state.CurrentAction!.EffectApplied));
    }

    [Fact]
    public void ExecuteTickTracesPreparedMoveEffectAndCompletionAcrossTenTicks()
    {
        // This records managed module timing with the authored 8-frame movement metadata.
        var (simulation, template, target, animation) = CreateNativeMoveCastFixture();
        Assert.Equal(8, animation.WalkingFrames);

        var runtime = simulation.CreateRuntime(template);
        runtime.Write("player", state => {
            state.EntityId = 11;
            state.PositionX = 4;
            state.PositionY = 4;
        });
        const ulong startTick = 100;
        const ulong endTick = 109;
        const ulong effectAndCompletionTick = 108;
        var initialHitPoints = target.HitPoints;
        var callbackOrder = new List<(ulong Tick, string Operation)>();
        var trace = new List<(
            ulong Tick,
            int PositionX,
            int PositionY,
            int FuturePositionX,
            int FuturePositionY,
            DiabloPlayerCombatMode CombatMode,
            DiabloPlayerActionKind? ActionKind,
            ulong? StartedAtTick,
            ulong? EffectTick,
            ulong? ReadyTick,
            ulong? EndTick,
            bool? EffectApplied,
            ulong LastAdvancedTick,
            int TargetHitPoints,
            int Mana)>();

        for (var tick = startTick; tick <= endTick; tick++) {
            var command = new SimulationTickCommand<
                AuthoritativePlayerState,
                DiabloGameplayEvent,
                DiabloRuleFailure?>(
                "player",
                (state, participants) => {
                    var isMoveTick = tick == startTick;
                    callbackOrder.Add((tick, isMoveTick ? "Move" : "NoOp"));
                    if (isMoveTick) {
                        var move = simulation.Execute(
                            state,
                            new DiabloMoveIntent(0, -1),
                            tick,
                            participants);
                        return new SimulationStepResult<DiabloRuleFailure?, DiabloGameplayEvent>(
                            move.Failure,
                            move.Events);
                    }

                    return new SimulationStepResult<DiabloRuleFailure?, DiabloGameplayEvent>(null, []);
                });

            var results = runtime.ExecuteTick<DiabloRuleFailure?>(tick, [command]);
            Assert.Equal(new DiabloRuleFailure?[] { null }, results);
            var row = runtime.Read("player", state => {
                var action = state.CurrentAction;
                return (
                    tick,
                    state.PositionX,
                    state.PositionY,
                    state.FuturePositionX,
                    state.FuturePositionY,
                    state.CombatMode,
                    action?.Kind,
                    action?.StartedAtTick,
                    action?.EffectTick,
                    action?.ReadyTick,
                    action?.EndTick,
                    action?.EffectApplied,
                    runtime.LastAdvancedTick,
                    target.HitPoints,
                    state.Mana);
            });
            trace.Add(row);
            Assert.Empty(runtime.DrainEvents("player"));
        }

        Assert.Equal(10, callbackOrder.Count);
        Assert.Equal(
            Enumerable.Range((int)startTick, (int)(endTick - startTick + 1))
                .Select(tick => ((ulong)tick, tick == (int)startTick ? "Move" : "NoOp")),
            callbackOrder);
        Assert.Equal(10, trace.Count);
        var expected = new (
            ulong Tick,
            int PositionX,
            int PositionY,
            int FuturePositionX,
            int FuturePositionY,
            DiabloPlayerCombatMode CombatMode,
            DiabloPlayerActionKind? ActionKind,
            ulong? StartedAtTick,
            ulong? EffectTick,
            ulong? ReadyTick,
            ulong? EndTick,
            bool? EffectApplied,
            ulong LastAdvancedTick,
            int TargetHitPoints,
            int Mana)[] {
            (100, 4, 4, 4, 3, DiabloPlayerCombatMode.WalkNorthwards, DiabloPlayerActionKind.Move, 100, 108, 108, 108, false, 100, 20, 10),
            (101, 4, 4, 4, 3, DiabloPlayerCombatMode.WalkNorthwards, DiabloPlayerActionKind.Move, 100, 108, 108, 108, false, 101, 20, 10),
            (102, 4, 4, 4, 3, DiabloPlayerCombatMode.WalkNorthwards, DiabloPlayerActionKind.Move, 100, 108, 108, 108, false, 102, 20, 10),
            (103, 4, 4, 4, 3, DiabloPlayerCombatMode.WalkNorthwards, DiabloPlayerActionKind.Move, 100, 108, 108, 108, false, 103, 20, 10),
            (104, 4, 4, 4, 3, DiabloPlayerCombatMode.WalkNorthwards, DiabloPlayerActionKind.Move, 100, 108, 108, 108, false, 104, 20, 10),
            (105, 4, 4, 4, 3, DiabloPlayerCombatMode.WalkNorthwards, DiabloPlayerActionKind.Move, 100, 108, 108, 108, false, 105, 20, 10),
            (106, 4, 4, 4, 3, DiabloPlayerCombatMode.WalkNorthwards, DiabloPlayerActionKind.Move, 100, 108, 108, 108, false, 106, 20, 10),
            (107, 4, 4, 4, 3, DiabloPlayerCombatMode.WalkNorthwards, DiabloPlayerActionKind.Move, 100, 108, 108, 108, false, 107, 20, 10),
            (108, 4, 3, 4, 3, DiabloPlayerCombatMode.Stand, null, null, null, null, null, null, 108, 20, 10),
            (109, 4, 3, 4, 3, DiabloPlayerCombatMode.Stand, null, null, null, null, null, null, 109, 20, 10),
        };
        Assert.Equal(expected, trace);
        Assert.Equal(effectAndCompletionTick, trace[8].Tick);
        Assert.Equal(initialHitPoints, trace[0].TargetHitPoints);
        Assert.Equal(initialHitPoints, trace[^1].TargetHitPoints);
        Assert.Equal(endTick, runtime.LastAdvancedTick);
    }

    [Fact]
    public void ExecuteTickTracesPreparedCastStartEffectAndCompletionBoundaries()
    {
        // The test-only 8/5 cast timing matches the prepared native spell fixture;
        // it records module timing without claiming native caller parity.
        var (simulation, template, target, animation) = CreateNativeMoveCastFixture(
            (CastingFrames: 8, CastingActionFrame: 5));
        Assert.Equal((8, 5), (animation.CastingFrames, animation.CastingActionFrame));

        var runtime = simulation.CreateRuntime(template);
        runtime.Write("player", state => state.EntityId = 11);
        var initialHitPoints = target.HitPoints;
        const ulong startTick = 100;
        const ulong readyTick = 105;
        const ulong endTick = 107;
        var callbackCount = 0;
        var trace = new List<(
            ulong Tick,
            string Point,
            DiabloPlayerCombatMode Mode,
            DiabloPlayerActionKind? Kind,
            ulong? StartedAtTick,
            ulong? EffectTick,
            ulong? ReadyTick,
            ulong? EndTick,
            bool? EffectApplied,
            int TargetHitPoints,
            int Mana)>();
        var eventsByTick = new List<(ulong Tick, int EventCount, int DamageEvents)>();

        void Record(ulong tick, string point, AuthoritativePlayerState state)
        {
            var action = state.CurrentAction;
            trace.Add((
                tick,
                point,
                state.CombatMode,
                action?.Kind,
                action?.StartedAtTick,
                action?.EffectTick,
                action?.ReadyTick,
                action?.EndTick,
                action?.EffectApplied,
                target.HitPoints,
                state.Mana));
        }

        for (var tick = startTick; tick <= endTick; tick++) {
            _ = runtime.Read("player", state => {
                Record(tick, "before-tick", state);
                return 0;
            });

            var command = new SimulationTickCommand<
                AuthoritativePlayerState,
                DiabloGameplayEvent,
                DiabloRuleFailure?>(
                "player",
                (state, participants) => {
                    callbackCount++;
                    Record(tick, "callback-before", state);
                    if (tick == startTick) {
                        var cast = simulation.Execute(
                            state,
                            new DiabloCastIntent(7, 99, 2, 0),
                            tick,
                            participants);
                        Record(tick, "callback-after", state);
                        return new SimulationStepResult<DiabloRuleFailure?, DiabloGameplayEvent>(
                            cast.Failure,
                            cast.Events);
                    }

                    Record(tick, "callback-after", state);
                    return new SimulationStepResult<DiabloRuleFailure?, DiabloGameplayEvent>(null, []);
                });

            var results = runtime.ExecuteTick<DiabloRuleFailure?>(tick, [command]);
            _ = runtime.Read("player", state => {
                Record(tick, "after-tick", state);
                return 0;
            });
            Assert.Equal(new DiabloRuleFailure?[] { null }, results);
            var tickEvents = runtime.DrainEvents("player");
            eventsByTick.Add((
                tick,
                tickEvents.Count,
                tickEvents.Count(gameEvent => gameEvent.Kind == DiabloGameplayEventKind.Damage)));
        }

        Assert.Equal(8, callbackCount);
        Assert.Equal(endTick, runtime.LastAdvancedTick);
        Assert.Equal(32, trace.Count);
        Assert.All(
            trace.GroupBy(row => row.Tick),
            tickRows => {
                Assert.Equal(4, tickRows.Count());
                Assert.Equal(4, tickRows.Select(row => row.Point).Distinct(StringComparer.Ordinal).Count());
            });

        var expectedAfterTickTrace = new (
            ulong Tick,
            DiabloPlayerCombatMode Mode,
            DiabloPlayerActionKind? Kind,
            ulong? StartedAtTick,
            ulong? EffectTick,
            ulong? ReadyTick,
            ulong? EndTick,
            bool? EffectApplied,
            int TargetHitPoints,
            int Mana)[] {
            (100, DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 100, 105, 105, 107, false, initialHitPoints, 10),
            (101, DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 100, 105, 105, 107, false, initialHitPoints, 10),
            (102, DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 100, 105, 105, 107, false, initialHitPoints, 10),
            (103, DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 100, 105, 105, 107, false, initialHitPoints, 10),
            (104, DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 100, 105, 105, 107, false, initialHitPoints, 10),
            (105, DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 100, 105, 105, 107, true, initialHitPoints - 6, 6),
            (106, DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 100, 105, 105, 107, true, initialHitPoints - 6, 6),
            (107, DiabloPlayerCombatMode.Stand, null, null, null, null, null, null, initialHitPoints - 6, 6),
        };
        Assert.Equal(
            expectedAfterTickTrace,
            trace.Where(row => row.Point == "after-tick")
                .Select(row => (
                    row.Tick,
                    row.Mode,
                    row.Kind,
                    row.StartedAtTick,
                    row.EffectTick,
                    row.ReadyTick,
                    row.EndTick,
                    row.EffectApplied,
                    row.TargetHitPoints,
                    row.Mana))
                .ToArray());

        var beforeStart = Assert.Single(
            trace,
            row => row.Tick == startTick && row.Point == "callback-before");
        Assert.Equal(DiabloPlayerCombatMode.Stand, beforeStart.Mode);
        Assert.Null(beforeStart.Kind);
        Assert.Equal(initialHitPoints, beforeStart.TargetHitPoints);
        Assert.Equal(10, beforeStart.Mana);

        var afterStart = Assert.Single(
            trace,
            row => row.Tick == startTick && row.Point == "callback-after");
        Assert.Equal(DiabloPlayerCombatMode.Spell, afterStart.Mode);
        Assert.Equal(DiabloPlayerActionKind.Cast, afterStart.Kind);
        Assert.Equal(startTick, afterStart.StartedAtTick);
        Assert.Equal(readyTick, afterStart.EffectTick);
        Assert.Equal(readyTick, afterStart.ReadyTick);
        Assert.Equal(endTick, afterStart.EndTick);
        Assert.Equal(false, afterStart.EffectApplied);
        Assert.Equal(initialHitPoints, afterStart.TargetHitPoints);
        Assert.Equal(10, afterStart.Mana);

        var beforeReady = Assert.Single(
            trace,
            row => row.Tick == readyTick && row.Point == "before-tick");
        Assert.Equal(false, beforeReady.EffectApplied);
        Assert.Equal(initialHitPoints, beforeReady.TargetHitPoints);
        var afterReadyAdvance = Assert.Single(
            trace,
            row => row.Tick == readyTick && row.Point == "callback-before");
        Assert.Equal(DiabloPlayerCombatMode.Spell, afterReadyAdvance.Mode);
        Assert.Equal(DiabloPlayerActionKind.Cast, afterReadyAdvance.Kind);
        Assert.Equal(true, afterReadyAdvance.EffectApplied);
        Assert.Equal(initialHitPoints - 6, afterReadyAdvance.TargetHitPoints);
        Assert.Equal(6, afterReadyAdvance.Mana);

        var beforeCompletion = Assert.Single(
            trace,
            row => row.Tick == endTick && row.Point == "before-tick");
        Assert.Equal(DiabloPlayerActionKind.Cast, beforeCompletion.Kind);
        var afterCompletionAdvance = Assert.Single(
            trace,
            row => row.Tick == endTick && row.Point == "callback-before");
        Assert.Equal(DiabloPlayerCombatMode.Stand, afterCompletionAdvance.Mode);
        Assert.Null(afterCompletionAdvance.Kind);
        Assert.Equal(initialHitPoints - 6, afterCompletionAdvance.TargetHitPoints);
        Assert.Equal(6, afterCompletionAdvance.Mana);

        Assert.Equal(1, eventsByTick.Count(row => row.EventCount == 1));
        Assert.Equal((1, 1), (
            Assert.Single(eventsByTick, row => row.Tick == readyTick).EventCount,
            Assert.Single(eventsByTick, row => row.Tick == readyTick).DamageEvents));
        Assert.All(
            eventsByTick.Where(row => row.Tick != readyTick),
            row => Assert.Equal((0, 0), (row.EventCount, row.DamageEvents)));
        Assert.Equal(initialHitPoints - 6, target.HitPoints);
    }

    [Fact]
    public void ExecuteTickTracesPreparedCastReadmissionThroughSecondEffectAndCompletion()
    {
        // The prepared 8/5 timing isolates the ready-boundary re-admission sequence.
        var (simulation, template, target, animation) = CreateNativeMoveCastFixture(
            (CastingFrames: 8, CastingActionFrame: 5));
        Assert.Equal((8, 5), (animation.CastingFrames, animation.CastingActionFrame));

        var runtime = simulation.CreateRuntime(template);
        runtime.Write("player", state => state.EntityId = 11);
        const ulong startTick = 100;
        const ulong firstReadyTick = 105;
        const ulong secondEndTick = 112;
        var initialHitPoints = target.HitPoints;
        var callbackOrder = new List<(ulong Tick, string Operation)>();
        var postTickRows = new List<(
            ulong Tick,
            DiabloPlayerCombatMode Mode,
            DiabloPlayerActionKind? Kind,
            ulong? StartedAtTick,
            ulong? EffectTick,
            ulong? ReadyTick,
            ulong? EndTick,
            bool? EffectApplied,
            ulong LastAdvancedTick,
            int HitPoints,
            int Mana)>();
        var readyBoundaryRows = new List<(
            string Point,
            DiabloPlayerCombatMode Mode,
            DiabloPlayerActionKind? Kind,
            ulong? StartedAtTick,
            ulong? EffectTick,
            ulong? ReadyTick,
            ulong? EndTick,
            bool? EffectApplied,
            int HitPoints,
            int Mana)>();
        var eventRows = new List<(ulong Tick, int EventCount)>();
        var damageRows = new List<(ulong Tick, DiabloGameplayEventKind Kind, uint TargetEntityId, int Amount)>();
        var callbackCount = 0;

        for (var tick = startTick; tick <= secondEndTick; tick++) {
            var command = new SimulationTickCommand<
                AuthoritativePlayerState,
                DiabloGameplayEvent,
                DiabloRuleFailure?>(
                "player",
                (state, participants) => {
                    callbackCount++;
                    var isInitialCast = tick == startTick;
                    var isReadyCast = tick == firstReadyTick;
                    callbackOrder.Add((
                        tick,
                        isInitialCast ? "InitialCast" : isReadyCast ? "ReadyCast" : "NoOp"));

                    if (isReadyCast)
                        RecordReadyBoundary("callback-before", state);
                    if (isInitialCast || isReadyCast) {
                        var cast = simulation.Execute(
                            state,
                            new DiabloCastIntent(7, 99, 2, 0),
                            tick,
                            participants);
                        if (isReadyCast)
                            RecordReadyBoundary("callback-after", state);
                        return new SimulationStepResult<DiabloRuleFailure?, DiabloGameplayEvent>(
                            cast.Failure,
                            cast.Events);
                    }

                    return new SimulationStepResult<DiabloRuleFailure?, DiabloGameplayEvent>(null, []);
                });

            var results = runtime.ExecuteTick<DiabloRuleFailure?>(tick, [command]);
            Assert.Equal(new DiabloRuleFailure?[] { null }, results);
            var row = runtime.Read("player", state => {
                var action = state.CurrentAction;
                return (
                    tick,
                    state.CombatMode,
                    action?.Kind,
                    action?.StartedAtTick,
                    action?.EffectTick,
                    action?.ReadyTick,
                    action?.EndTick,
                    action?.EffectApplied,
                    runtime.LastAdvancedTick,
                    target.HitPoints,
                    state.Mana);
            });
            postTickRows.Add(row);

            var tickEvents = runtime.DrainEvents("player");
            eventRows.Add((tick, tickEvents.Count));
            damageRows.AddRange(tickEvents
                .Where(gameEvent => gameEvent.Kind == DiabloGameplayEventKind.Damage)
                .Select(gameEvent => (
                    tick,
                    gameEvent.Kind,
                    gameEvent.TargetEntityId,
                    gameEvent.Amount)));
        }

        void RecordReadyBoundary(string point, AuthoritativePlayerState state)
        {
            var action = state.CurrentAction;
            readyBoundaryRows.Add((
                point,
                state.CombatMode,
                action?.Kind,
                action?.StartedAtTick,
                action?.EffectTick,
                action?.ReadyTick,
                action?.EndTick,
                action?.EffectApplied,
                target.HitPoints,
                state.Mana));
        }

        Assert.Equal(13, callbackCount);
        Assert.Equal(
            Enumerable.Range((int)startTick, (int)(secondEndTick - startTick + 1))
                .Select(tick => (
                    (ulong)tick,
                    tick == (int)startTick ? "InitialCast"
                    : tick == (int)firstReadyTick ? "ReadyCast"
                    : "NoOp")),
            callbackOrder);
        Assert.Equal(13, postTickRows.Count);
        var expectedPostTickRows = new (
            ulong Tick,
            DiabloPlayerCombatMode Mode,
            DiabloPlayerActionKind? Kind,
            ulong? StartedAtTick,
            ulong? EffectTick,
            ulong? ReadyTick,
            ulong? EndTick,
            bool? EffectApplied,
            ulong LastAdvancedTick,
            int HitPoints,
            int Mana)[] {
            (100, DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 100, 105, 105, 107, false, 100, 20, 10),
            (101, DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 100, 105, 105, 107, false, 101, 20, 10),
            (102, DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 100, 105, 105, 107, false, 102, 20, 10),
            (103, DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 100, 105, 105, 107, false, 103, 20, 10),
            (104, DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 100, 105, 105, 107, false, 104, 20, 10),
            (105, DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 105, 110, 110, 112, false, 105, 14, 6),
            (106, DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 105, 110, 110, 112, false, 106, 14, 6),
            (107, DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 105, 110, 110, 112, false, 107, 14, 6),
            (108, DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 105, 110, 110, 112, false, 108, 14, 6),
            (109, DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 105, 110, 110, 112, false, 109, 14, 6),
            (110, DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 105, 110, 110, 112, true, 110, 8, 2),
            (111, DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 105, 110, 110, 112, true, 111, 8, 2),
            (112, DiabloPlayerCombatMode.Stand, null, null, null, null, null, null, 112, 8, 2),
        };
        Assert.Equal(expectedPostTickRows, postTickRows);
        Assert.Equal(
            new (
                string Point,
                DiabloPlayerCombatMode Mode,
                DiabloPlayerActionKind? Kind,
                ulong? StartedAtTick,
                ulong? EffectTick,
                ulong? ReadyTick,
                ulong? EndTick,
                bool? EffectApplied,
                int HitPoints,
                int Mana)[] {
                ("callback-before", DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 100, 105, 105, 107, true, 14, 6),
                ("callback-after", DiabloPlayerCombatMode.Spell, DiabloPlayerActionKind.Cast, 105, 110, 110, 112, false, 14, 6),
            },
            readyBoundaryRows);
        Assert.Equal(13, eventRows.Count);
        Assert.Equal(2, eventRows.Sum(row => row.EventCount));
        Assert.Equal(
            new (ulong Tick, DiabloGameplayEventKind Kind, uint TargetEntityId, int Amount)[] {
                (105, DiabloGameplayEventKind.Damage, 99, 6),
                (110, DiabloGameplayEventKind.Damage, 99, 6),
            },
            damageRows);
        Assert.Equal(initialHitPoints, postTickRows[0].HitPoints);
        Assert.Equal(2, postTickRows[^1].Mana);
        Assert.Equal(secondEndTick, runtime.LastAdvancedTick);
    }

    [Fact]
    public void ExecuteTickAdmitsCastAtSpellReadyTickAndRejectsSecondSameTickCast()
    {
        var (simulation, template, target, animation) = CreateNativeMoveCastFixture();
        var runtime = simulation.CreateRuntime(template);
        runtime.Write("player", state => state.EntityId = 11);
        var initialHitPoints = target.HitPoints;

        var initialResults = runtime.ExecuteTick<DiabloRuleFailure?>(
            1,
            [
                CreateCastTickCommand(
                    "player",
                    simulation,
                    1,
                    new DiabloCastIntent(7, 99, 2, 0)),
                CreateCastTickCommand(
                    "player",
                    simulation,
                    1,
                    new DiabloCastIntent(7, 99, 2, 0)),
            ]);

        Assert.Equal(
            new DiabloRuleFailure?[] { null, DiabloRuleFailure.NotAllowed },
            initialResults);
        var firstCast = runtime.Read("player", state => state.CurrentAction)!;
        var firstEffectTick = 1UL + (ulong)animation.CastingActionFrame;
        Assert.Equal(DiabloPlayerActionKind.Cast, firstCast.Kind);
        Assert.False(firstCast.EffectApplied);
        Assert.Equal(firstEffectTick, firstCast.ReadyTick);
        Assert.Equal(initialHitPoints, target.HitPoints);
        Assert.Equal(10, runtime.Read("player", state => state.Mana));

        var actionAfterFirstReadyCommand = (AuthoritativePlayerActionState?)null;
        var readyObservation = (EffectApplied: false, HitPoints: 0, Mana: 0);
        var readyResults = runtime.ExecuteTick<DiabloRuleFailure?>(
            firstCast.ReadyTick,
            [
                new SimulationTickCommand<AuthoritativePlayerState, DiabloGameplayEvent, DiabloRuleFailure?>(
                    "player",
                    (state, participants) => {
                        readyObservation = (
                            state.CurrentAction?.EffectApplied ?? false,
                            target.HitPoints,
                            state.Mana);
                        var result = simulation.Execute(
                            state,
                            new DiabloCastIntent(7, 99, 2, 0),
                            firstCast.ReadyTick,
                            participants);
                        actionAfterFirstReadyCommand = state.CurrentAction;
                        return new SimulationStepResult<DiabloRuleFailure?, DiabloGameplayEvent>(
                            result.Failure,
                            result.Events);
                    }),
                CreateCastTickCommand(
                    "player",
                    simulation,
                    firstCast.ReadyTick,
                    new DiabloCastIntent(7, 99, 2, 0)),
            ]);

        Assert.Equal((true, initialHitPoints - 6, 6), readyObservation);
        Assert.Equal(
            new DiabloRuleFailure?[] { null, DiabloRuleFailure.NotAllowed },
            readyResults);
        var replacement = runtime.Read("player", state => state.CurrentAction)!;
        Assert.Same(actionAfterFirstReadyCommand, replacement);
        Assert.Equal(DiabloPlayerActionKind.Cast, replacement.Kind);
        Assert.False(replacement.EffectApplied);
        var expectedReplacementEffectTick = firstCast.ReadyTick
            + (ulong)animation.CastingActionFrame;
        var expectedReplacementEndTick = firstCast.ReadyTick
            + (ulong)(animation.CastingFrames - 1);
        Assert.Equal((firstCast.ReadyTick, expectedReplacementEffectTick,
            expectedReplacementEffectTick, expectedReplacementEndTick), (
            replacement.StartedAtTick,
            replacement.EffectTick,
            replacement.ReadyTick,
            replacement.EndTick));
        Assert.Equal(initialHitPoints - 6, target.HitPoints);
        Assert.Equal(6, runtime.Read("player", state => state.Mana));
        var damage = Assert.Single(runtime.DrainEvents("player"));
        Assert.Equal(DiabloGameplayEventKind.Damage, damage.Kind);
        Assert.Equal(99U, damage.TargetEntityId);
        Assert.Equal(6, damage.Amount);
        Assert.Equal(firstCast.ReadyTick, runtime.LastAdvancedTick);
    }
    [Fact]
    public void CohortRuntimeUsesTheSameOrderedCommandLifecycle()
    {
        var options = CreateOptions();
        var source = DiabloSimulationFactory.Instance.Create(options);
        var sourceRuntime = source.CreateRuntime(
            DiabloPlayerTemplate.CreateDefault(0, 50, 0, 0));
        sourceRuntime.Write("player", state => state.EntityId = 11);
        sourceRuntime.RestoreAtTick(2);
        var captured = source.CaptureCohortState(sourceRuntime);
        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(options, captured);
        var runtime = candidate.CreateRuntime();

        var results = runtime.ExecuteTick(
            3,
            [new SimulationTickCommand<AuthoritativePlayerState, DiabloGameplayEvent, int>(
                "player",
                (state, participants) => new SimulationStepResult<int, DiabloGameplayEvent>(7))]);

        Assert.Equal([7], results);
        Assert.Equal(3UL, runtime.LastAdvancedTick);
    }

    [Theory]
    [InlineData(0U, 18, 19, 25, 27, 28, 34, 33, 34, 40)]
    [InlineData(1U << 17, 18, 19, 25, 27, 28, 34, 32, 33, 39)]
    [InlineData(1U << 18, 17, 18, 24, 25, 26, 32, 30, 31, 37)]
    [InlineData(1U << 19, 16, 17, 23, 23, 24, 30, 28, 29, 35)]
    [InlineData(1U << 20, 16, 17, 23, 23, 24, 30, 27, 28, 34)]
    [InlineData((1U << 17) | (1U << 18) | (1U << 19) | (1U << 20), 16, 17, 23, 23, 24, 30, 27, 28, 34)]
    public void ReadyRepeatUsesWithoutFirstFrameAndCompletedAttackUsesWithFirstFrame(
        uint attackFlags,
        int initialEffect,
        int initialReady,
        int initialEnd,
        int readyEffect,
        int readyReady,
        int readyEnd,
        int completionEffect,
        int completionReady,
        int completionEnd)
    {
        AssertAbsoluteAttackTiming(
            attackFlags,
            ranged: false,
            initialEffect,
            initialReady,
            initialEnd,
            readyEffect,
            readyReady,
            readyEnd,
            completionEffect,
            completionReady,
            completionEnd);
    }

    [Theory]
    [InlineData(0U, 20, 21, 25, 31, 32, 36, 35, 36, 40)]
    [InlineData(1U << 17, 20, 21, 25, 31, 32, 36, 34, 35, 39)]
    [InlineData(1U << 18, 19, 20, 24, 29, 30, 34, 32, 33, 37)]
    [InlineData(1U << 19, 20, 21, 25, 31, 32, 36, 35, 36, 40)]
    [InlineData(1U << 20, 20, 21, 25, 31, 32, 36, 35, 36, 40)]
    [InlineData((1U << 17) | (1U << 18) | (1U << 19) | (1U << 20), 19, 20, 24, 29, 30, 34, 32, 33, 37)]
    public void RangedReadyRepeatUsesTheNativeSkipMatrix(
        uint attackFlags,
        int initialEffect,
        int initialReady,
        int initialEnd,
        int readyEffect,
        int readyReady,
        int readyEnd,
        int completionEffect,
        int completionReady,
        int completionEnd)
    {
        AssertAbsoluteAttackTiming(
            attackFlags,
            ranged: true,
            initialEffect,
            initialReady,
            initialEnd,
            readyEffect,
            readyReady,
            readyEnd,
            completionEffect,
            completionReady,
            completionEnd);
    }

    [Fact]
    public void HellfireRangedUsesZeroSkipForReadyAndCompletionPhases()
    {
        var simulation = CreateNativeTimingSimulation(
            0,
            ranged: true,
            out var template,
            DiabloGameVariant.Hellfire);
        var player = template.CreatePlayer();
        player.EntityId = 11;
        Assert.True(simulation.Execute(
            player,
            new DiabloAttackIntent(99),
            10,
            [player]).Succeeded);
        Assert.Equal((20UL, 21UL, 25UL), (
            player.CurrentAction!.EffectTick,
            player.CurrentAction.ReadyTick,
            player.CurrentAction.EndTick));
        simulation.AdvanceTickWithCommands(
            21,
            [player],
            state => {
                Assert.True(simulation.Execute(
                    state,
                    new DiabloAttackIntent(99),
                    21,
                    [state]).Succeeded);
                return [];
            });
        Assert.Equal((31UL, 32UL, 36UL), (
            player.CurrentAction!.EffectTick,
            player.CurrentAction.ReadyTick,
            player.CurrentAction.EndTick));

        var completionSimulation = CreateNativeTimingSimulation(
            0,
            ranged: true,
            out var completionTemplate,
            DiabloGameVariant.Hellfire);
        var completionPlayer = completionTemplate.CreatePlayer();
        completionPlayer.EntityId = 11;
        Assert.True(completionSimulation.Execute(
            completionPlayer,
            new DiabloAttackIntent(99),
            10,
            [completionPlayer]).Succeeded);
        completionSimulation.AdvanceTickWithCommands(
            25,
            [completionPlayer],
            state => {
                Assert.True(completionSimulation.Execute(
                    state,
                    new DiabloAttackIntent(99),
                    25,
                    [state]).Succeeded);
                return [];
            });
        Assert.Equal((35UL, 36UL, 40UL), (
            completionPlayer.CurrentAction!.EffectTick,
            completionPlayer.CurrentAction.ReadyTick,
            completionPlayer.CurrentAction.EndTick));
    }

    [Fact]
    public void FastMeleeReadyAndCompletionUseAbsoluteNativeActionTicks()
    {
        var readySimulation = CreateNativeTimingSimulation(
            1U << 18,
            ranged: false,
            out var readyTemplate);
        var readyPlayer = readyTemplate.CreatePlayer();
        readyPlayer.EntityId = 11;
        Assert.True(readySimulation.Execute(
            readyPlayer,
            new DiabloAttackIntent(99),
            10,
            [readyPlayer]).Succeeded);
        Assert.Equal((17UL, 18UL, 24UL), (
            readyPlayer.CurrentAction!.EffectTick,
            readyPlayer.CurrentAction.ReadyTick,
            readyPlayer.CurrentAction.EndTick));
        readySimulation.AdvanceTickWithCommands(
            18,
            [readyPlayer],
            player => {
                Assert.True(readySimulation.Execute(
                    player,
                    new DiabloAttackIntent(99),
                    18,
                    [player]).Succeeded);
                return [];
            });
        Assert.Equal((25UL, 26UL, 32UL), (
            readyPlayer.CurrentAction!.EffectTick,
            readyPlayer.CurrentAction.ReadyTick,
            readyPlayer.CurrentAction.EndTick));

        var completionSimulation = CreateNativeTimingSimulation(
            1U << 18,
            ranged: false,
            out var completionTemplate);
        var completionPlayer = completionTemplate.CreatePlayer();
        completionPlayer.EntityId = 11;
        Assert.True(completionSimulation.Execute(
            completionPlayer,
            new DiabloAttackIntent(99),
            10,
            [completionPlayer]).Succeeded);
        completionSimulation.AdvanceTickWithCommands(
            24,
            [completionPlayer],
            player => {
                Assert.True(completionSimulation.Execute(
                    player,
                    new DiabloAttackIntent(99),
                    24,
                    [player]).Succeeded);
                return [];
            });
        Assert.Equal((30UL, 31UL, 37UL), (
            completionPlayer.CurrentAction!.EffectTick,
            completionPlayer.CurrentAction.ReadyTick,
            completionPlayer.CurrentAction.EndTick));
    }

    [Fact]
    public void HellfireRangedAllSpeedFlagsUseAbsoluteZeroSkipTicks()
    {
        var allSpeedFlags = (1U << 17) | (1U << 18) | (1U << 19) | (1U << 20);
        var simulation = CreateNativeTimingSimulation(
            allSpeedFlags,
            ranged: true,
            out var template,
            DiabloGameVariant.Hellfire);
        var player = template.CreatePlayer();
        player.EntityId = 11;
        Assert.True(simulation.Execute(
            player,
            new DiabloAttackIntent(99),
            10,
            [player]).Succeeded);
        Assert.Equal((20UL, 21UL, 25UL), (
            player.CurrentAction!.EffectTick,
            player.CurrentAction.ReadyTick,
            player.CurrentAction.EndTick));
        simulation.AdvanceTickWithCommands(
            21,
            [player],
            state => {
                Assert.True(simulation.Execute(
                    state,
                    new DiabloAttackIntent(99),
                    21,
                    [state]).Succeeded);
                return [];
            });
        Assert.Equal((31UL, 32UL, 36UL), (
            player.CurrentAction!.EffectTick,
            player.CurrentAction.ReadyTick,
            player.CurrentAction.EndTick));
    }

    [Fact]
    public void ExecuteTickExposesOrdinaryPhaseOnlyToMatchingNativeCallbackActor()
    {
        var (simulation, template, _, _) = CreateNativeMoveCastFixture();
        var runtime = simulation.CreateRuntime(template);
        AuthoritativePlayerState? owner = null;
        runtime.Write("a-owner", state => {
            state.EntityId = 11;
            owner = state;
        });
        runtime.Write("z-other", state => state.EntityId = 22);

        Assert.Null(simulation.GetCurrentCommandCallbackPhase(owner!));
        var observedPhases = new List<DiabloCommandCallbackPhase?>();
        var results = runtime.ExecuteTick<int>(
            1,
            [
                new SimulationTickCommand<
                    AuthoritativePlayerState,
                    DiabloGameplayEvent,
                    int>(
                    "a-owner",
                    (state, participants) => {
                        observedPhases.Add(simulation.GetCurrentCommandCallbackPhase(state));
                        var other = Assert.Single(
                            participants,
                            participant => !ReferenceEquals(participant, state));
                        Assert.Null(simulation.GetCurrentCommandCallbackPhase(other));
                        var sameEntityIdOtherReference = template.CreatePlayer();
                        sameEntityIdOtherReference.EntityId = state.EntityId;
                        Assert.NotSame(state, sameEntityIdOtherReference);
                        Assert.Equal(state.EntityId, sameEntityIdOtherReference.EntityId);
                        Assert.Null(simulation.GetCurrentCommandCallbackPhase(sameEntityIdOtherReference));
                        return new SimulationStepResult<int, DiabloGameplayEvent>(11);
                    }),
            ]);

        Assert.Equal([11], results);
        Assert.Equal(
            [DiabloCommandCallbackPhase.OrdinaryCommandSlot],
            observedPhases);
        Assert.Null(simulation.GetCurrentCommandCallbackPhase(owner!));

        var defaultSimulation = CreateSimulation();
        var defaultRuntime = defaultSimulation.CreateRuntime(
            DiabloPlayerTemplate.CreateDefault(0, 50, 0, 0));
        AuthoritativePlayerState? defaultPlayer = null;
        defaultRuntime.Write("default-player", state => {
            state.EntityId = 33;
            defaultPlayer = state;
        });
        Assert.Null(defaultSimulation.GetCurrentCommandCallbackPhase(defaultPlayer!));
        var defaultPhases = defaultRuntime.ExecuteTick<DiabloRuleFailure?>(
            1,
            [
                new SimulationTickCommand<
                    AuthoritativePlayerState,
                    DiabloGameplayEvent,
                    DiabloRuleFailure?>(
                    "default-player",
                    (state, _) => {
                        Assert.Null(defaultSimulation.GetCurrentCommandCallbackPhase(state));
                        return new SimulationStepResult<DiabloRuleFailure?, DiabloGameplayEvent>(null);
                    }),
            ]);
        Assert.Equal(new DiabloRuleFailure?[] { null }, defaultPhases);
        Assert.Null(defaultSimulation.GetCurrentCommandCallbackPhase(defaultPlayer!));
    }

    [Theory]
    [InlineData(false, DiabloPlayerCombatMode.Attack)]
    [InlineData(true, DiabloPlayerCombatMode.RangedAttack)]
    public void ExecuteTickExposesAttackOrRangedReadyRepeatPhase(
        bool ranged,
        DiabloPlayerCombatMode expectedMode)
    {
        var simulation = CreateNativeTimingSimulation(
            0,
            ranged,
            out var template);
        var runtime = simulation.CreateRuntime(template);
        AuthoritativePlayerState? playerState = null;
        ulong readyTick = 0;
        runtime.Write("player", player => {
            player.EntityId = 11;
            playerState = player;
            var started = simulation.Execute(
                player,
                new DiabloAttackIntent(99),
                10,
                [player]);
            Assert.True(started.Succeeded);
            readyTick = player.CurrentAction!.ReadyTick;
        });

        Assert.True(readyTick > 0);
        var preReadyPhases = new List<DiabloCommandCallbackPhase?>();
        var preReadyResults = runtime.ExecuteTick<DiabloRuleFailure?>(
            readyTick - 1,
            [
                new SimulationTickCommand<
                    AuthoritativePlayerState,
                    DiabloGameplayEvent,
                    DiabloRuleFailure?>(
                    "player",
                    (player, _) => {
                        preReadyPhases.Add(simulation.GetCurrentCommandCallbackPhase(player));
                        return new SimulationStepResult<DiabloRuleFailure?, DiabloGameplayEvent>(null);
                    }),
            ]);
        Assert.Equal(new DiabloRuleFailure?[] { null }, preReadyResults);
        Assert.Equal(
            new DiabloCommandCallbackPhase?[] { DiabloCommandCallbackPhase.OrdinaryCommandSlot },
            preReadyPhases);

        var observedPhases = new List<DiabloCommandCallbackPhase?>();
        var results = runtime.ExecuteTick<DiabloRuleFailure?>(
            readyTick,
            [
                new SimulationTickCommand<
                    AuthoritativePlayerState,
                    DiabloGameplayEvent,
                    DiabloRuleFailure?>(
                    "player",
                    (player, participants) => {
                        observedPhases.Add(simulation.GetCurrentCommandCallbackPhase(player));
                        var rejected = simulation.Execute(
                            player,
                            new DiabloAttackIntent(404),
                            readyTick,
                            participants);
                        return new SimulationStepResult<DiabloRuleFailure?, DiabloGameplayEvent>(
                            rejected.Failure,
                            rejected.Events);
                    }),
                new SimulationTickCommand<
                    AuthoritativePlayerState,
                    DiabloGameplayEvent,
                    DiabloRuleFailure?>(
                    "player",
                    (player, participants) => {
                        observedPhases.Add(simulation.GetCurrentCommandCallbackPhase(player));
                        var accepted = simulation.Execute(
                            player,
                            new DiabloAttackIntent(99),
                            readyTick,
                            participants);
                        return new SimulationStepResult<DiabloRuleFailure?, DiabloGameplayEvent>(
                            accepted.Failure,
                            accepted.Events);
                    }),
            ]);

        Assert.Equal(
            new DiabloRuleFailure?[] { DiabloRuleFailure.InvalidTarget, null },
            results);
        Assert.Equal(
            new DiabloCommandCallbackPhase?[] {
                DiabloCommandCallbackPhase.AttackOrRangedReadyRepeat,
                DiabloCommandCallbackPhase.AttackOrRangedReadyRepeat,
            },
            observedPhases);
        Assert.Equal(expectedMode, playerState!.CombatMode);
        Assert.Equal(readyTick, playerState.CurrentAction!.StartedAtTick);
        Assert.Null(simulation.GetCurrentCommandCallbackPhase(playerState));
    }

    [Fact]
    public void ExecuteTickExposesSpellReadmissionPhaseWithoutChangingSameTickRefusal()
    {
        var (simulation, template, target, _) = CreateNativeMoveCastFixture();
        var initialHitPoints = target.HitPoints;
        var runtime = simulation.CreateRuntime(template);
        runtime.Write("player", state => state.EntityId = 11);
        var firstResults = runtime.ExecuteTick<DiabloRuleFailure?>(
            1,
            [
                CreateCastTickCommand(
                    "player",
                    simulation,
                    1,
                    new DiabloCastIntent(7, 99, 2, 0)),
            ]);
        Assert.Equal(new DiabloRuleFailure?[] { null }, firstResults);

        var firstCast = runtime.Read("player", state => state.CurrentAction)!;
        var readyTick = firstCast.ReadyTick;
        var observedPhases = new List<DiabloCommandCallbackPhase?>();
        (bool EffectApplied, int HitPoints, int Mana)? stateBeforeReadmission = null;

        SimulationTickCommand<
            AuthoritativePlayerState,
            DiabloGameplayEvent,
            DiabloRuleFailure?> CreateReadmissionCommand()
            => new(
                "player",
                (state, participants) => {
                    observedPhases.Add(simulation.GetCurrentCommandCallbackPhase(state));
                    if (observedPhases.Count == 1) {
                        stateBeforeReadmission = (
                            state.CurrentAction?.EffectApplied ?? false,
                            target.HitPoints,
                            state.Mana);
                    }
                    var result = simulation.Execute(
                        state,
                        new DiabloCastIntent(7, 99, 2, 0),
                        readyTick,
                        participants);
                    return new SimulationStepResult<DiabloRuleFailure?, DiabloGameplayEvent>(
                        result.Failure,
                        result.Events);
                });

        var readyResults = runtime.ExecuteTick<DiabloRuleFailure?>(
            readyTick,
            [
                CreateReadmissionCommand(),
                CreateReadmissionCommand(),
            ]);

        Assert.NotNull(stateBeforeReadmission);
        Assert.Equal((true, initialHitPoints - 6, 6), stateBeforeReadmission.Value);
        Assert.Equal(
            new DiabloRuleFailure?[] { null, DiabloRuleFailure.NotAllowed },
            readyResults);
        Assert.Equal(
            new DiabloCommandCallbackPhase?[] {
                DiabloCommandCallbackPhase.SpellReadmission,
                DiabloCommandCallbackPhase.SpellReadmission,
            },
            observedPhases);
        var replacement = runtime.Read("player", state => state.CurrentAction)!;
        Assert.Equal(readyTick, replacement.StartedAtTick);
        Assert.False(replacement.EffectApplied);
        Assert.Equal(6, runtime.Read("player", state => state.Mana));
        Assert.Null(simulation.GetCurrentCommandCallbackPhase(
            runtime.Read("player", state => state)));
    }

    [Fact]
    public void ExecuteTickExposesCompletionLoopStartAfterMoveCompletes()
    {
        var (simulation, template, _, _) = CreateNativeMoveCastFixture();
        var runtime = simulation.CreateRuntime(template);
        runtime.Write("player", state => state.EntityId = 11);
        var moveResults = runtime.ExecuteTick<DiabloRuleFailure?>(
            1,
            [CreateMoveTickCommand("player", simulation, 1, 1, 0)]);
        Assert.Equal(new DiabloRuleFailure?[] { null }, moveResults);
        var moveEndTick = runtime.Read("player", state => state.CurrentAction!.EndTick);

        AuthoritativePlayerState? observedPlayer = null;
        DiabloCommandCallbackPhase? observedPhase = null;
        var completionSeen = false;
        var castResults = runtime.ExecuteTick<DiabloRuleFailure?>(
            moveEndTick,
            [
                new SimulationTickCommand<
                    AuthoritativePlayerState,
                    DiabloGameplayEvent,
                    DiabloRuleFailure?>(
                    "player",
                    (player, participants) => {
                        observedPlayer = player;
                        observedPhase = simulation.GetCurrentCommandCallbackPhase(player);
                        completionSeen = player.PositionX == 1
                            && player.PositionY == 0
                            && player.CombatMode == DiabloPlayerCombatMode.Stand
                            && player.CurrentAction is null;
                        var result = simulation.Execute(
                            player,
                            new DiabloCastIntent(7, 99, 2, 0),
                            moveEndTick,
                            participants);
                        return new SimulationStepResult<DiabloRuleFailure?, DiabloGameplayEvent>(
                            result.Failure,
                            result.Events);
                    }),
            ]);

        Assert.True(completionSeen);
        Assert.Equal(new DiabloRuleFailure?[] { null }, castResults);
        Assert.Equal(
            DiabloCommandCallbackPhase.CompletionLoopStart,
            observedPhase);
        Assert.Null(simulation.GetCurrentCommandCallbackPhase(observedPlayer!));
    }

    [Fact]
    public void ExecuteTickClearsCommandPhaseAfterCallbackException()
    {
        var (simulation, template, _, _) = CreateNativeMoveCastFixture();
        var runtime = simulation.CreateRuntime(template);
        AuthoritativePlayerState? playerState = null;
        runtime.Write("player", state => {
            state.EntityId = 11;
            playerState = state;
        });
        DiabloCommandCallbackPhase? observedPhase = null;

        Assert.Throws<InvalidOperationException>(() => runtime.ExecuteTick<int>(
            1,
            [
                new SimulationTickCommand<
                    AuthoritativePlayerState,
                    DiabloGameplayEvent,
                    int>(
                    "player",
                    (player, _) => {
                        observedPhase = simulation.GetCurrentCommandCallbackPhase(player);
                        throw new InvalidOperationException("callback failure");
                    }),
            ]));

        Assert.Equal(DiabloCommandCallbackPhase.OrdinaryCommandSlot, observedPhase);
        Assert.Null(simulation.GetCurrentCommandCallbackPhase(playerState!));
    }

    [Fact]
    public void ExecuteTickClearsCommandPhaseWhenCallbackReturnsNullEvents()
    {
        var (simulation, template, _, _) = CreateNativeMoveCastFixture();
        var player = template.CreatePlayer();
        player.EntityId = 11;
        DiabloCommandCallbackPhase? observedPhase = null;

        Assert.Throws<InvalidOperationException>(() => simulation.AdvanceTickWithCommands(
            1,
            [player],
            state => {
                observedPhase = simulation.GetCurrentCommandCallbackPhase(state);
                return null!;
            }));

        Assert.Equal(DiabloCommandCallbackPhase.OrdinaryCommandSlot, observedPhase);
        Assert.Null(simulation.GetCurrentCommandCallbackPhase(player));
    }
    private static SimulationTickCommand<
        AuthoritativePlayerState,
        DiabloGameplayEvent,
        DiabloRuleFailure?> CreateMoveTickCommand(
        string sessionId,
        DiabloSimulation simulation,
        ulong tick,
        int directionX,
        int directionY,
        List<(uint EntityId, string Intent)>? operationOrder = null)
    {
        return new SimulationTickCommand<
            AuthoritativePlayerState,
            DiabloGameplayEvent,
            DiabloRuleFailure?>(
            sessionId,
            (player, participants) => {
                operationOrder?.Add((player.EntityId, "Move"));
                var result = simulation.Execute(
                    player,
                    new DiabloMoveIntent(directionX, directionY),
                    tick,
                    participants);
                return new SimulationStepResult<DiabloRuleFailure?, DiabloGameplayEvent>(
                    result.Failure,
                    result.Events);
            });
    }

    private static SimulationTickCommand<
        AuthoritativePlayerState,
        DiabloGameplayEvent,
        DiabloRuleFailure?> CreateCastTickCommand(
        string sessionId,
        DiabloSimulation simulation,
        ulong tick,
        DiabloCastIntent intent,
        List<(uint EntityId, string Intent)>? operationOrder = null)
    {
        return new SimulationTickCommand<
            AuthoritativePlayerState,
            DiabloGameplayEvent,
            DiabloRuleFailure?>(
            sessionId,
            (player, participants) => {
                operationOrder?.Add((player.EntityId, "Cast"));
                var result = simulation.Execute(player, intent, tick, participants);
                return new SimulationStepResult<DiabloRuleFailure?, DiabloGameplayEvent>(
                    result.Failure,
                    result.Events);
            });
    }

    private static (
        DiabloSimulation Simulation,
        DiabloPlayerTemplate Template,
        AuthoritativeCombatTarget Target,
        AuthoritativePlayerAnimationDefinition Animation)
        CreateNativeMoveCastFixture(
            (int CastingFrames, int CastingActionFrame)? castTimingOverride = null)
    {
        var (classes, animations) = LoadShippedPlayerMetadata();
        if (castTimingOverride is { } castTiming) {
            var prepared = animations.GetRequired(0) with {
                CastingFrames = castTiming.CastingFrames,
                CastingActionFrame = castTiming.CastingActionFrame,
            };
            animations = new AuthoritativePlayerAnimationCatalog(
                animations.Definitions.Select(definition => definition.ClassId == prepared.ClassId
                    ? prepared
                    : definition),
                classes);
        }
        var target = new AuthoritativeCombatTarget(
            99,
            2,
            0,
            20,
            aggroRange: 0);
        var options = new DiabloSimulationOptions(
            new StoreCatalog(),
            DiabloModule.Instance.Rules) {
            FallbackWidth = 8,
            FallbackHeight = 8,
            FallbackBlockedCells = [],
            FallbackMissileBlockedCells = [],
            PlayerClasses = classes,
            PlayerAnimations = animations,
            UseNativePlayerActionTiming = true,
            CombatRules = new AuthoritativeCombatRules(10, 1, 100),
            Spells = new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(7, 4, 0, 0, 0, 0) {
                    DamageAmount = 6,
                    Range = 4,
                },
            ]),
            CombatTargets = [target],
        };
        var template = new DiabloPlayerTemplate(
            gold: 0,
            experience: 0,
            life: 50,
            mana: 10,
            manaMaximum: 10,
            attributes: PlayerAttributesState.Zero,
            equipment: [],
            inventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
            belt: [],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 50,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0,
            characterClassId: 0,
            memorizedSpells: [new MemorizedSpellState(7, 1)]);
        return (
            DiabloSimulationFactory.Instance.Create(options),
            template,
            target,
            animations.GetRequired(0));
    }
    private static DiabloSimulation CreateSimulation()
        => DiabloSimulationFactory.Instance.Create(CreateOptions());

    private static DiabloSimulationOptions CreateOptions()
        => new(new StoreCatalog(), DiabloModule.Instance.Rules) {
            FallbackWidth = 8,
            FallbackHeight = 8,
            FallbackBlockedCells = [],
            FallbackMissileBlockedCells = [],
        };

    private static DiabloSimulation CreateNativeTimingSimulation(
        uint attackFlags,
        bool ranged,
        out DiabloPlayerTemplate template,
        DiabloGameVariant gameVariant = DiabloGameVariant.Diablo)
    {
        var (classes, animations) = LoadShippedPlayerMetadata();
        var equipment = new[] {
            new EquippedStoreItem(
                4,
                1,
                AuthoritativeItemState.Empty with {
                    StatFlag = true,
                    Flags = attackFlags,
                    ItemClass = 1,
                    ItemType = ranged ? 3 : 1,
                }),
        };
        template = new DiabloPlayerTemplate(
            gold: 0,
            experience: 0,
            life: 50,
            mana: 0,
            manaMaximum: 0,
            attributes: PlayerAttributesState.Zero,
            equipment: equipment,
            inventoryGrid: Enumerable.Repeat(-1, 40).ToArray(),
            belt: [],
            positionX: 0,
            positionY: 0,
            lifeMaximum: 50,
            characterLevel: 1,
            levelId: 0,
            armorClass: 0,
            characterClassId: 0);
        var options = CreateOptions();
        options = new DiabloSimulationOptions(options.StoreCatalog, options.StoreRules) {
            FallbackWidth = options.FallbackWidth,
            FallbackHeight = options.FallbackHeight,
            FallbackBlockedCells = [],
            FallbackMissileBlockedCells = [],
            PlayerClasses = classes,
            PlayerAnimations = animations,
            UseNativePlayerActionTiming = true,
            CombatRules = new AuthoritativeCombatRules(
                baseAttackDamage: 10,
                minimumDamage: 1,
                defeatExperience: 100,
                gameVariant: gameVariant),
            CombatTargets = [new AuthoritativeCombatTarget(99, 1, 0, 100)],
        };
        return DiabloSimulationFactory.Instance.Create(options);
    }

    private static void AssertAbsoluteAttackTiming(
        uint attackFlags,
        bool ranged,
        int initialEffect,
        int initialReady,
        int initialEnd,
        int readyEffect,
        int readyReady,
        int readyEnd,
        int completionEffect,
        int completionReady,
        int completionEnd)
    {
        var readySimulation = CreateNativeTimingSimulation(
            attackFlags,
            ranged,
            out var readyTemplate);
        var readyPlayer = readyTemplate.CreatePlayer();
        readyPlayer.EntityId = 11;
        Assert.True(readySimulation.Execute(
            readyPlayer,
            new DiabloAttackIntent(99),
            10,
            [readyPlayer]).Succeeded);
        Assert.Equal((ulong)initialEffect, readyPlayer.CurrentAction!.EffectTick);
        Assert.Equal((ulong)initialReady, readyPlayer.CurrentAction.ReadyTick);
        Assert.Equal((ulong)initialEnd, readyPlayer.CurrentAction.EndTick);
        var readyTick = readyPlayer.CurrentAction.ReadyTick;
        readySimulation.AdvanceTickWithCommands(
            readyTick,
            [readyPlayer],
            player => {
                Assert.True(readySimulation.Execute(
                    player,
                    new DiabloAttackIntent(99),
                    readyTick,
                    [player]).Succeeded);
                return [];
            });
        Assert.Equal((ulong)readyEffect, readyPlayer.CurrentAction!.EffectTick);
        Assert.Equal((ulong)readyReady, readyPlayer.CurrentAction.ReadyTick);
        Assert.Equal((ulong)readyEnd, readyPlayer.CurrentAction.EndTick);

        var completionSimulation = CreateNativeTimingSimulation(
            attackFlags,
            ranged,
            out var completionTemplate);
        var completionPlayer = completionTemplate.CreatePlayer();
        completionPlayer.EntityId = 11;
        Assert.True(completionSimulation.Execute(
            completionPlayer,
            new DiabloAttackIntent(99),
            10,
            [completionPlayer]).Succeeded);
        Assert.Equal((ulong)initialEffect, completionPlayer.CurrentAction!.EffectTick);
        Assert.Equal((ulong)initialReady, completionPlayer.CurrentAction.ReadyTick);
        Assert.Equal((ulong)initialEnd, completionPlayer.CurrentAction.EndTick);
        var completionTick = completionPlayer.CurrentAction!.EndTick;
        completionSimulation.AdvanceTickWithCommands(
            completionTick,
            [completionPlayer],
            player => {
                Assert.True(completionSimulation.Execute(
                    player,
                    new DiabloAttackIntent(99),
                    completionTick,
                    [player]).Succeeded);
                return [];
            });
        Assert.Equal((ulong)completionEffect, completionPlayer.CurrentAction!.EffectTick);
        Assert.Equal((ulong)completionReady, completionPlayer.CurrentAction.ReadyTick);
        Assert.Equal((ulong)completionEnd, completionPlayer.CurrentAction.EndTick);
    }

    private static (
        AuthoritativePlayerClassCatalog Classes,
        AuthoritativePlayerAnimationCatalog Animations)
        LoadShippedPlayerMetadata()
    {
        var root = FindRepositoryRoot();
        var contentRoot = Path.Combine(root, "server", "content", "base");
        var classes = AuthoritativePlayerClassCatalog.LoadTsv(
            "classes.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "classes.tsv")));
        var animations = AuthoritativePlayerAnimationCatalog.LoadTsv(
            "class_animations.tsv",
            File.ReadAllText(Path.Combine(contentRoot, "class_animations.tsv")),
            classes);
        return (classes, animations);
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable(
            "GODEVILUTION_REPOSITORY_ROOT");
        var searchRoots = string.IsNullOrWhiteSpace(configuredRoot)
            ? new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            : new[] { configuredRoot!, Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in searchRoots) {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent) {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                    return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException(
            "Could not locate the Godevilution repository root.");
    }

    private static AuthoritativePlayerState CreatePlayer(
        uint entityId,
        int positionX = 0,
        int positionY = 0)
    {
        var player = DiabloPlayerTemplate.CreateDefault(
            gold: 0,
            life: 50,
            mana: 0,
            levelId: 0).CreatePlayer();
        player.EntityId = entityId;
        player.PositionX = positionX;
        player.PositionY = positionY;
        return player;
    }
}

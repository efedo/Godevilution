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

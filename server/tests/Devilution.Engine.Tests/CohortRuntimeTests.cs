using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class CohortRuntimeTests
{
    [Fact]
    public void CaptureEmptyRuntimeDoesNotCreateOrAdvanceParticipants()
    {
        var lifecycle = new Lifecycle();
        var runtime = new AuthoritativeSimulationRuntime<State, string>(lifecycle);

        var captured = runtime.CaptureCohort((tick, participants) => (tick, participants.Count));

        Assert.Equal((0UL, 0), captured);
        Assert.Equal(0, lifecycle.FactoryCalls);
        Assert.Empty(lifecycle.AdvancedTicks);
        Assert.Empty(lifecycle.ParticipantTicks);
        Assert.Throws<ArgumentNullException>(() => runtime.CaptureCohort<int>(null!));
    }

    [Fact]
    public void CaptureKeepsExactClockOrdinalBindingsAndDetachedPayload()
    {
        var lifecycle = new Lifecycle();
        var runtime = new AuthoritativeSimulationRuntime<State, string>(lifecycle);
        runtime.InitializeCohort(7, [
            new("b", new State { Value = 3 }),
            new("a", new State { Value = 2 }),
            new("A", new State { Value = 1 }),
        ]);

        var captured = runtime.CaptureCohort((tick, participants) => (
            Tick: tick,
            Players: participants.Select(participant =>
                (participant.Id, participant.State.Value)).ToArray()));
        runtime.Write("a", state => state.Value = 99);

        Assert.Equal(7UL, captured.Tick);
        Assert.Equal([("A", 1), ("a", 2), ("b", 3)], captured.Players);
        Assert.Equal(0, lifecycle.FactoryCalls);
        Assert.Empty(lifecycle.AdvancedTicks);
        Assert.Empty(lifecycle.ParticipantTicks);
    }

    [Fact]
    public void CaptureDoesNotAdvanceOrDrainEventsAndCallbackFailureReleasesBarrier()
    {
        var lifecycle = new Lifecycle();
        var runtime = new AuthoritativeSimulationRuntime<State, string>(lifecycle);
        runtime.RestoreAtTick(7);
        runtime.Execute("player", 7, (_, _) => new SimulationStepResult<int, string>(0, ["event"]));

        Assert.Throws<InvalidDataException>(() => runtime.CaptureCohort<int>(
            (_, _) => throw new InvalidDataException("capture failed")));
        var captured = runtime.CaptureCohort((tick, participants) =>
            (tick, participants.Single().State.LastAppliedTick));

        Assert.Equal((7UL, 7UL), captured);
        Assert.Equal(1, lifecycle.FactoryCalls);
        Assert.Equal([7UL], lifecycle.ParticipantTicks);
        Assert.Empty(lifecycle.AdvancedTicks);
        Assert.Equal(["event"], runtime.DrainEvents("player"));
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(7UL)]
    [InlineData(ulong.MaxValue)]
    public void InitializeKeepsExactTickAndCallerConstructedStatesWithoutLifecycleHooks(ulong tick)
    {
        var lifecycle = new Lifecycle();
        var runtime = new AuthoritativeSimulationRuntime<State, string>(lifecycle);
        var player = new State { Value = 42, LastAppliedTick = tick };

        runtime.InitializeCohort(tick, [new("player", player)]);

        Assert.Equal(tick, runtime.LastAdvancedTick);
        Assert.Same(player, runtime.Read("player", state => state));
        Assert.Equal(tick, player.LastAppliedTick);
        Assert.Equal(0, lifecycle.FactoryCalls);
        Assert.Empty(lifecycle.AdvancedTicks);
        Assert.Empty(lifecycle.ParticipantTicks);
        Assert.Empty(runtime.DrainEvents("player"));
    }

    [Fact]
    public void InitializedRuntimeAdvancesOnlyFromRestoredBoundary()
    {
        var lifecycle = new Lifecycle();
        var runtime = new AuthoritativeSimulationRuntime<State, string>(lifecycle);
        var player = new State { LastAppliedTick = 7 };
        runtime.InitializeCohort(7, [new("player", player)]);

        runtime.AdvanceTo(8);

        Assert.Equal(8UL, runtime.LastAdvancedTick);
        Assert.Equal([8UL], lifecycle.AdvancedTicks);
        Assert.Equal([8UL], lifecycle.ParticipantTicks);
        Assert.Equal(8UL, player.LastAppliedTick);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void InvalidSecondBindingLeavesWholeRosterAndClockUntouched(string? invalidId)
    {
        var runtime = new AuthoritativeSimulationRuntime<State, string>(new Lifecycle());

        Assert.ThrowsAny<ArgumentException>(() => runtime.InitializeCohort(7, [
            new("valid", new State()), new(invalidId!, new State()),
        ]));

        Assert.Equal((0UL, 0), runtime.CaptureCohort((tick, players) => (tick, players.Count)));
        runtime.InitializeCohort(3, [new("valid", new State())]);
        Assert.Equal(3UL, runtime.LastAdvancedTick);
    }

    [Fact]
    public void NullCollectionRecordOrStateCannotPartiallyInitializeRuntime()
    {
        var runtime = new AuthoritativeSimulationRuntime<State, string>(new Lifecycle());

        Assert.Throws<ArgumentNullException>(() => runtime.InitializeCohort(7, null!));
        Assert.Throws<ArgumentNullException>(() => runtime.InitializeCohort(7, [
            new("valid", new State()), null!,
        ]));
        Assert.Throws<ArgumentNullException>(() => runtime.InitializeCohort(7, [
            new("valid", new State()), new("invalid", null!),
        ]));

        Assert.Equal((0UL, 0), runtime.CaptureCohort((tick, players) => (tick, players.Count)));
        runtime.InitializeCohort(3, []);
        Assert.Equal(3UL, runtime.LastAdvancedTick);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateSessionOrStateCannotPartiallyInitializeRuntime(bool duplicateState)
    {
        var runtime = new AuthoritativeSimulationRuntime<State, string>(new Lifecycle());
        var first = new State();
        var second = duplicateState ? new SimulationSession<State>("second", first)
            : new SimulationSession<State>("first", new State());

        Assert.Throws<InvalidOperationException>(() => runtime.InitializeCohort(7, [
            new("first", first), second,
        ]));

        Assert.Equal((0UL, 0), runtime.CaptureCohort((tick, players) => (tick, players.Count)));
        runtime.InitializeCohort(3, [new("first", first)]);
    }

    [Fact]
    public void EmptyTickZeroInitializationIsStillOneShot()
    {
        var runtime = new AuthoritativeSimulationRuntime<State, string>(new Lifecycle());
        runtime.InitializeCohort(0, []);

        Assert.Throws<InvalidOperationException>(() => runtime.InitializeCohort(0, []));
        Assert.Throws<InvalidOperationException>(() => runtime.InitializeCohort(9, [new("new", new State())]));
        Assert.Equal((0UL, 0), runtime.CaptureCohort((tick, players) => (tick, players.Count)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InitializationRejectsAnExistingRosterOrTimeline(bool existingRoster)
    {
        var runtime = new AuthoritativeSimulationRuntime<State, string>(new Lifecycle());
        if (existingRoster)
            runtime.AddParticipant("existing", new State { Value = 42 });
        else
            runtime.RestoreAtTick(8);

        Assert.Throws<InvalidOperationException>(() => runtime.InitializeCohort(2, [new("new", new State())]));
        Assert.Equal(existingRoster ? (0UL, 1) : (8UL, 0),
            runtime.CaptureCohort((tick, players) => (tick, players.Count)));
    }

    [Fact]
    public async Task CaptureHoldsBarrierUntilPayloadIsDetached()
    {
        var runtime = new AuthoritativeSimulationRuntime<State, string>(new Lifecycle());
        runtime.AddParticipant("player", new State { Value = 1 });
        using var captureEntered = new ManualResetEventSlim();
        using var releaseCapture = new ManualResetEventSlim();
        using var writeEntered = new ManualResetEventSlim();
        var cancellationToken = TestContext.Current.CancellationToken;
        var capture = Task.Factory.StartNew(() => runtime.CaptureCohort((_, players) => {
            captureEntered.Set();
            if (!releaseCapture.Wait(TimeSpan.FromSeconds(5), cancellationToken))
                throw new TimeoutException("The test did not release cohort capture.");
            return players.Single().State.Value;
        }), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task? write = null;
        try {
            Assert.True(captureEntered.Wait(TimeSpan.FromSeconds(5), cancellationToken));
            write = Task.Factory.StartNew(() => {
                writeEntered.Set();
                runtime.Write("player", state => state.Value = 99);
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(5), cancellationToken));
            Assert.NotSame(write, await Task.WhenAny(write,
                Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken)));
        } finally {
            releaseCapture.Set();
            // Join both workers even after capture fails or the test is cancelled,
            // before disposing the signals they may still be using.
            using var cleanupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await Task.WhenAll((Task)capture, write ?? Task.CompletedTask)
                .WaitAsync(cleanupDeadline.Token);
        }
        Assert.Equal(1, await capture);
        Assert.Equal(99, runtime.Read("player", state => state.Value));
    }

    [Fact]
    public void BatchRegistrationValidatesEnumerationBeforeAnyMutation()
    {
        var coordinator = new SimulationSessionCoordinator<State, string>(
            _ => throw new InvalidOperationException("The factory must not run."));
        IEnumerable<SimulationSession<State>> BrokenBindings()
        {
            yield return new("valid", new State());
            throw new InvalidDataException("Invalid binding stream.");
        }

        Assert.Throws<InvalidDataException>(() => coordinator.AddRange(BrokenBindings()));

        Assert.Equal(0, coordinator.Count);
        coordinator.AddRange([new("valid", new State())]);
        Assert.Equal(1, coordinator.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BatchRegistrationRejectsExistingIdOrStateBeforeAddingEarlierValidBinding(bool sharedState)
    {
        var coordinator = new SimulationSessionCoordinator<State, string>(
            _ => throw new InvalidOperationException("The factory must not run."));
        var existing = new State();
        coordinator.Add("existing", existing);
        coordinator.EnqueueEvent("existing", "kept");

        Assert.Throws<InvalidOperationException>(() => coordinator.AddRange([
            new("new", new State()),
            sharedState ? new("other", existing) : new("existing", new State()),
        ]));

        Assert.Equal(1, coordinator.Count);
        Assert.False(coordinator.TryGet("new", out _));
        Assert.Same(existing, coordinator.GetOrCreate("existing"));
        Assert.Equal(["kept"], coordinator.DrainEvents("existing"));
    }

    [Fact]
    public void EqualValuedStatesRetainDistinctReferenceOwnership()
    {
        var coordinator = new SimulationSessionCoordinator<ValueState, string>(
            _ => throw new InvalidOperationException("The factory must not run."));
        var first = new ValueState(1);
        var second = new ValueState(1);

        coordinator.AddRange([new("a", first), new("b", second)]);

        Assert.Equal(2, coordinator.Count);
        Assert.Equal("a", coordinator.GetRequiredSessionId(first));
        Assert.Equal("b", coordinator.GetRequiredSessionId(second));
    }

    [Theory]
    [InlineData(0U)]
    [InlineData(1U)]
    [InlineData(123U)]
    [InlineData(uint.MaxValue)]
    public void AllocatorRestoresExactCursorIncludingExhaustion(uint cursor)
    {
        var allocator = new DeterministicEntityIdAllocator(50);

        allocator.RestoreNextId(cursor);

        Assert.Equal(cursor, allocator.NextId);
        if (cursor == 0)
            Assert.Throws<InvalidOperationException>(() => allocator.Allocate());
        else {
            Assert.Equal(cursor, allocator.Allocate());
            Assert.Equal(cursor == uint.MaxValue ? 0U : cursor + 1, allocator.NextId);
        }
    }

    [Fact]
    public void RestoredHighWaterMarkPreservesDeletedIdsAndReserveSemantics()
    {
        var original = new DeterministicEntityIdAllocator(40);
        original.Reserve(500);
        var candidate = new DeterministicEntityIdAllocator();

        candidate.RestoreNextId(original.NextId);
        candidate.Reserve(10);
        Assert.Equal(501U, candidate.Allocate());
        candidate.Reserve(uint.MaxValue);
        Assert.Equal(0U, candidate.NextId);
        candidate.Reserve(1);
        Assert.Throws<InvalidOperationException>(() => candidate.Allocate());
    }

    private sealed class State
    {
        public int Value { get; set; }
        public ulong LastAppliedTick { get; set; }
    }

    private sealed record ValueState(int Value);

    private sealed class Lifecycle : IAuthoritativeSimulationLifecycle<State, string>
    {
        public int FactoryCalls { get; private set; }
        public List<ulong> ParticipantTicks { get; } = [];
        public List<ulong> AdvancedTicks { get; } = [];

        public State CreateParticipant(string sessionId)
        {
            FactoryCalls++;
            return new State();
        }

        public void AdvanceParticipant(State state, ulong tick)
        {
            state.LastAppliedTick = tick;
            ParticipantTicks.Add(tick);
        }

        public IReadOnlyList<DirectedSimulationEvent<State, string>> AdvanceTick(
            ulong tick, IReadOnlyList<State> participants)
        {
            AdvancedTicks.Add(tick);
            return [];
        }
    }
}

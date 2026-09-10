using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class AuthoritativeSimulationRuntimeTests
{
    [Fact]
    public void ExecutesWithOrdinalParticipantsAndQueuesOperationEvents()
    {
        var lifecycle = new TestLifecycle();
        var runtime = new AuthoritativeSimulationRuntime<TestState, string>(lifecycle);
        runtime.Read("session-b", state => state);

        var result = runtime.Execute(
            "session-a",
            4,
            (state, participants) => {
                Assert.Equal(
                    ["session-a", "session-b"],
                    participants.Select(participant => participant.Id));
                state.Value = 9;
                return new SimulationStepResult<int, string>(7, ["accepted"]);
            });

        Assert.Equal(7, result);
        Assert.Equal(["accepted"], runtime.DrainEvents("session-a"));
        Assert.Equal([("session-a", 4UL)], lifecycle.ParticipantAdvances);
        Assert.Equal(9, runtime.Read("session-a", state => state.Value));
    }

    [Fact]
    public void AdvancesEveryTickOnceAndRoutesDirectedEvents()
    {
        var lifecycle = new TestLifecycle();
        var runtime = new AuthoritativeSimulationRuntime<TestState, string>(lifecycle);
        var second = runtime.Read("second", state => state);
        var first = runtime.Read("first", state => state);
        lifecycle.Deliveries.Add(new DirectedSimulationEvent<TestState, string>(second, "tick"));

        runtime.AdvanceTo(3);
        runtime.AdvanceTo(2);
        runtime.AdvanceTo(3);

        Assert.Equal(3UL, runtime.LastAdvancedTick);
        Assert.Equal([1UL, 2UL, 3UL], lifecycle.AdvancedTicks);
        Assert.All(lifecycle.TickParticipantOrders, order =>
            Assert.Equal(["first", "second"], order));
        Assert.Equal(["tick", "tick", "tick"], runtime.DrainEvents("second"));
        Assert.Empty(runtime.DrainEvents("first"));
        Assert.Equal(3UL, first.LastAdvancedTick);
    }

    [Fact]
    public void RestoreAtTickResumesWithoutReplayingEarlierTicks()
    {
        var lifecycle = new TestLifecycle();
        var runtime = new AuthoritativeSimulationRuntime<TestState, string>(lifecycle);
        runtime.Read("player", state => state);

        runtime.AdvanceTo(3);
        runtime.RestoreAtTick(7);
        runtime.AdvanceTo(8);

        Assert.Equal(8UL, runtime.LastAdvancedTick);
        Assert.Equal([1UL, 2UL, 3UL, 8UL], lifecycle.AdvancedTicks);
        Assert.Equal(8UL, runtime.Read("player", state => state.LastAdvancedTick));
    }

    [Fact]
    public void RestoreAtTickDoesNotRewindSharedTimeline()
    {
        var lifecycle = new TestLifecycle();
        var runtime = new AuthoritativeSimulationRuntime<TestState, string>(lifecycle);
        runtime.Read("first", state => state);

        runtime.AdvanceTo(5);
        runtime.RestoreAtTick(8);
        runtime.RestoreAtTick(6);
        runtime.AdvanceTo(9);

        Assert.Equal(9UL, runtime.LastAdvancedTick);
        Assert.Equal([1UL, 2UL, 3UL, 4UL, 5UL, 9UL], lifecycle.AdvancedTicks);
    }

    [Fact]
    public void RestoreAtTickDiscardsTransientEventsFromThePreRestoreTimeline()
    {
        var lifecycle = new TestLifecycle();
        var runtime = new AuthoritativeSimulationRuntime<TestState, string>(lifecycle);

        runtime.Execute(
            "player-a",
            1,
            (_, _) => new SimulationStepResult<int, string>(1, ["stale-a"]));
        runtime.Execute(
            "player-b",
            1,
            (_, _) => new SimulationStepResult<int, string>(1, ["stale-b"]));

        runtime.RestoreAtTick(7);

        Assert.Empty(runtime.DrainEvents("player-a"));
        Assert.Empty(runtime.DrainEvents("player-b"));
        Assert.Equal(7UL, runtime.LastAdvancedTick);
    }

    [Fact]
    public void CaptureAdvancesExistingStatesBeforeCreatingNewParticipant()
    {
        var lifecycle = new TestLifecycle();
        var runtime = new AuthoritativeSimulationRuntime<TestState, string>(lifecycle);
        runtime.Read("existing", state => state);

        var captured = runtime.Capture("new", 2, state => state.LastAdvancedTick);

        Assert.Equal(2UL, captured);
        Assert.Equal([1UL, 2UL], lifecycle.AdvancedTicks);
        Assert.Equal(2, lifecycle.TickParticipantOrders.Count);
        Assert.All(lifecycle.TickParticipantOrders, order =>
            Assert.Equal(["existing"], order));
    }

    [Fact]
    public void CaptureExposesRequestedStateAndOrdinalParticipantsAtomically()
    {
        var lifecycle = new TestLifecycle();
        var runtime = new AuthoritativeSimulationRuntime<TestState, string>(lifecycle);
        runtime.Read("session-b", state => state);

        var captured = runtime.Capture(
            "session-a",
            2,
            (state, participants) => (
                state.Id,
                Participants: participants.Select(participant => participant.Id).ToArray()));

        Assert.Equal("session-a", captured.Id);
        Assert.Equal(["session-a", "session-b"], captured.Participants);
        Assert.Equal(2UL, runtime.LastAdvancedTick);
    }

    [Fact]
    public void RejectsForeignDirectedEventRecipients()
    {
        var lifecycle = new TestLifecycle();
        var runtime = new AuthoritativeSimulationRuntime<TestState, string>(lifecycle);
        runtime.Read("known", state => state);
        lifecycle.Deliveries.Add(new DirectedSimulationEvent<TestState, string>(
            new TestState("foreign"),
            "event"));

        Assert.Throws<InvalidOperationException>(() => runtime.AdvanceTo(1));
        Assert.Equal(0UL, runtime.LastAdvancedTick);
    }

    [Fact]
    public void RegistersExplicitParticipantStateBeforeImplicitAccess()
    {
        var lifecycle = new TestLifecycle();
        var runtime = new AuthoritativeSimulationRuntime<TestState, string>(lifecycle);
        var selected = new TestState("selected") { Value = 42 };

        runtime.AddParticipant("session", selected);

        Assert.Same(selected, runtime.Read("session", state => state));
        Assert.Equal(42, runtime.Read("session", state => state.Value));
        Assert.False(runtime.TryAddParticipant(
            "session",
            new TestState("ignored")));
        var other = new TestState("other");
        Assert.True(runtime.TryAddParticipant("other", other));
        Assert.Same(other, runtime.Read("other", state => state));
        Assert.Throws<InvalidOperationException>(() =>
            runtime.AddParticipant("session", new TestState("duplicate")));
    }

    [Fact]
    public void WritesExistingOrRegistersOnlySuccessfullyInitializedState()
    {
        var runtime = new AuthoritativeSimulationRuntime<TestState, string>(
            new TestLifecycle());

        Assert.Throws<InvalidDataException>(() =>
            runtime.WriteOrAddParticipant(
                "rejected",
                () => new TestState("candidate"),
                _ => throw new InvalidDataException("invalid")));
        var fallback = runtime.Read("rejected", state => state);
        Assert.Equal("rejected", fallback.Id);

        var candidate = new TestState("candidate");
        Assert.True(runtime.WriteOrAddParticipant(
            "accepted",
            () => candidate,
            state => state.Value = 7));
        Assert.Same(candidate, runtime.Read("accepted", state => state));
        Assert.False(runtime.WriteOrAddParticipant(
            "accepted",
            () => throw new InvalidOperationException("must not create"),
            state => state.Value = 9));
        Assert.Equal(9, runtime.Read("accepted", state => state.Value));
    }

    private sealed class TestLifecycle : IAuthoritativeSimulationLifecycle<TestState, string>
    {
        public List<(string Id, ulong Tick)> ParticipantAdvances { get; } = [];
        public List<ulong> AdvancedTicks { get; } = [];
        public List<IReadOnlyList<string>> TickParticipantOrders { get; } = [];
        public List<DirectedSimulationEvent<TestState, string>> Deliveries { get; } = [];

        public TestState CreateParticipant(string sessionId)
        {
            return new TestState(sessionId);
        }

        public void AdvanceParticipant(TestState state, ulong tick)
        {
            state.LastAdvancedTick = tick;
            ParticipantAdvances.Add((state.Id, tick));
        }

        public IReadOnlyList<DirectedSimulationEvent<TestState, string>> AdvanceTick(
            ulong tick,
            IReadOnlyList<TestState> participants)
        {
            AdvancedTicks.Add(tick);
            TickParticipantOrders.Add(participants.Select(participant => participant.Id).ToArray());
            return Deliveries.ToArray();
        }
    }

    private sealed class TestState(string id)
    {
        public string Id { get; } = id;
        public int Value { get; set; }
        public ulong LastAdvancedTick { get; set; }
    }
}

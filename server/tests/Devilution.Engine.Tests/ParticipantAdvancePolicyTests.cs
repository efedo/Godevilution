using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class ParticipantAdvancePolicyTests
{
    [Fact]
    public void ExistingConstructorRetainsCoalescedParticipantAdvancement()
    {
        var defaultLifecycle = new RecordingLifecycle();
        var explicitLifecycle = new RecordingLifecycle();
        var defaultRuntime = new AuthoritativeSimulationRuntime<TestState, string>(defaultLifecycle);
        var explicitRuntime = new AuthoritativeSimulationRuntime<TestState, string>(
            explicitLifecycle,
            ParticipantAdvancePolicy.OncePerAdvance);
        defaultRuntime.Read("player", state => state);
        explicitRuntime.Read("player", state => state);

        defaultRuntime.AdvanceTo(3);
        explicitRuntime.AdvanceTo(3);

        Assert.Equal([("player", 3UL)], defaultLifecycle.ParticipantAdvances);
        Assert.Equal(defaultLifecycle.ParticipantAdvances, explicitLifecycle.ParticipantAdvances);
        Assert.Equal([1UL, 2UL, 3UL], defaultLifecycle.WorldTicks);
    }

    [Fact]
    public void RejectsUnknownPolicyBeforeInvokingLifecycleHooks()
    {
        var lifecycle = new RecordingLifecycle();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AuthoritativeSimulationRuntime<TestState, string>(
                lifecycle,
                (ParticipantAdvancePolicy)int.MaxValue));

        Assert.Equal(0, lifecycle.CreatedParticipants);
        Assert.Empty(lifecycle.ParticipantAdvances);
        Assert.Empty(lifecycle.WorldTicks);
    }

    [Fact]
    public void AdvancesParticipantsInOrdinalOrderBeforeEveryWorldTickAndRoutesEvents()
    {
        var lifecycle = new RecordingLifecycle { EmitFirstParticipantEvent = true };
        var runtime = new AuthoritativeSimulationRuntime<TestState, string>(
            lifecycle,
            ParticipantAdvancePolicy.BeforeEachTick);
        var second = runtime.Read("second", state => state);
        var first = runtime.Read("first", state => state);
        lifecycle.Trace.Clear();

        runtime.AdvanceTo(2);

        Assert.Equal(
            [
                "participant:first:1",
                "participant:second:1",
                "world:1",
                "participant:first:2",
                "participant:second:2",
                "world:2",
            ],
            lifecycle.Trace);
        Assert.Equal(["world-1", "world-2"], runtime.DrainEvents("first"));
        Assert.Empty(runtime.DrainEvents("second"));
        Assert.Equal(2UL, first.LastAdvancedTick);
        Assert.Equal(2UL, second.LastAdvancedTick);
        Assert.Equal(2UL, runtime.LastAdvancedTick);
    }

    [Fact]
    public void OrderedTickCatchUpAdvancesExistingStatesBeforeFactoriesAndCommandEvents()
    {
        var lifecycle = new RecordingLifecycle();
        var runtime = new AuthoritativeSimulationRuntime<TestState, string>(
            lifecycle,
            ParticipantAdvancePolicy.BeforeEachTick);
        runtime.Read("z-existing", state => state);
        lifecycle.Trace.Clear();
        var command = new SimulationTickCommand<TestState, string, int>(
            "a-new",
            (state, _) => {
                lifecycle.Trace.Add($"command:{state.Id}");
                return new SimulationStepResult<int, string>(7, ["accepted"]);
            });

        var results = runtime.ExecuteTick(3, [command]);

        Assert.Equal([7], results);
        Assert.Equal(
            [
                "participant:z-existing:1",
                "world:1",
                "participant:z-existing:2",
                "world:2",
                "factory:a-new",
                "participant:a-new:3",
                "participant:z-existing:3",
                "command-world:3",
                "slot:a-new",
                "command:a-new",
                "slot:z-existing",
            ],
            lifecycle.Trace);
        Assert.Equal(["accepted"], runtime.DrainEvents("a-new"));
        Assert.Empty(runtime.DrainEvents("z-existing"));
        Assert.Equal(3UL, runtime.LastAdvancedTick);
    }

    [Fact]
    public void WorldCreatedTimedStatusAgesAcrossCatchUpAndExpiresAtTheSameTick()
    {
        const ulong effectTick = 3;
        const uint durationAtEffect = 247;
        var stepped = new AuthoritativeSimulationRuntime<TestState, string>(
            new TimedStatusLifecycle(effectTick, durationAtEffect),
            ParticipantAdvancePolicy.BeforeEachTick);
        var jumped = new AuthoritativeSimulationRuntime<TestState, string>(
            new TimedStatusLifecycle(effectTick, durationAtEffect),
            ParticipantAdvancePolicy.BeforeEachTick);
        stepped.Read("player", state => state);
        jumped.Read("player", state => state);

        stepped.AdvanceTo(effectTick);
        jumped.AdvanceTo(effectTick - 1);
        Assert.Equal(durationAtEffect, stepped.Capture(
            "player",
            effectTick,
            state => state.RemainingTicks));
        Assert.Equal(durationAtEffect, stepped.Capture(
            "player",
            effectTick,
            state => state.RemainingTicks));

        stepped.AdvanceTo(effectTick + 1);
        Assert.Equal(246U, stepped.Read("player", state => state.RemainingTicks));
        stepped.AdvanceTo(effectTick + 2);
        stepped.AdvanceTo(effectTick + 3);
        jumped.AdvanceTo(effectTick + 3);

        Assert.Equal(244U, stepped.Read("player", state => state.RemainingTicks));
        Assert.Equal(244U, jumped.Read("player", state => state.RemainingTicks));
        stepped.AdvanceTo(effectTick + durationAtEffect);
        jumped.AdvanceTo(effectTick + durationAtEffect);
        Assert.Equal(0U, stepped.Read("player", state => state.RemainingTicks));
        Assert.Equal(0U, jumped.Read("player", state => state.RemainingTicks));
        Assert.Equal(effectTick + durationAtEffect, stepped.LastAdvancedTick);
        Assert.Equal(effectTick + durationAtEffect, jumped.LastAdvancedTick);
    }

    [Fact]
    public void BeforeEachTickPolicyPreservesNoOpBackwardAndNearLimitBehavior()
    {
        var lifecycle = new RecordingLifecycle();
        var runtime = new AuthoritativeSimulationRuntime<TestState, string>(
            lifecycle,
            ParticipantAdvancePolicy.BeforeEachTick);
        var player = new TestState("player");
        runtime.InitializeCohort(
            ulong.MaxValue - 1,
            [new SimulationSession<TestState>("player", player)]);

        runtime.AdvanceTo(ulong.MaxValue);
        runtime.AdvanceTo(ulong.MaxValue);
        runtime.AdvanceTo(ulong.MaxValue - 1);

        Assert.Equal(ulong.MaxValue, runtime.LastAdvancedTick);
        Assert.Equal([ulong.MaxValue], lifecycle.WorldTicks);
        Assert.Equal([("player", ulong.MaxValue)], lifecycle.ParticipantAdvances);
    }

    [Fact]
    public void SwallowedReentrancyDuringCatchUpStopsLaterParticipantsAndPublication()
    {
        AuthoritativeSimulationRuntime<TestState, string>? runtime = null;
        InvalidOperationException? swallowed = null;
        var lifecycle = new RecordingLifecycle {
            ParticipantAdvance = (state, _) => {
                if (state.Id != "a-first")
                    return;
                try {
                    runtime!.Read(state.Id, value => value);
                } catch (InvalidOperationException error) {
                    swallowed = error;
                }
            },
        };
        runtime = new AuthoritativeSimulationRuntime<TestState, string>(
            lifecycle,
            ParticipantAdvancePolicy.BeforeEachTick);
        runtime.Read("b-second", state => state);
        runtime.Read("a-first", state => state);
        lifecycle.Trace.Clear();
        var commandCalls = 0;
        var command = new SimulationTickCommand<TestState, string, int>(
            "c-new",
            (_, _) => {
                commandCalls++;
                return new SimulationStepResult<int, string>(7, ["must-not-publish"]);
            });

        Assert.Throws<InvalidOperationException>(() => runtime.ExecuteTick(2, [command]));

        Assert.NotNull(swallowed);
        Assert.Equal("Runtime access cannot reenter an ordered command tick.", swallowed!.Message);
        Assert.Equal(["participant:a-first:1"], lifecycle.Trace);
        Assert.Equal(2, lifecycle.CreatedParticipants);
        Assert.Empty(lifecycle.WorldTicks);
        Assert.Equal(0, commandCalls);
        Assert.Equal(0UL, runtime.LastAdvancedTick);
        Assert.Throws<InvalidOperationException>(() => runtime.Read("a-first", state => state));
        Assert.Throws<InvalidOperationException>(() => runtime.Capture("checkpoint", 2, state => state));
        Assert.Throws<InvalidOperationException>(() => runtime.DrainEvents("a-first"));
    }

    [Fact]
    public void MutatingParticipantFailureDuringCatchUpPreservesTheExceptionAndPoisonsRuntime()
    {
        AuthoritativeSimulationRuntime<TestState, string>? runtime = null;
        var failure = new InvalidOperationException("status advancement failed");
        var lifecycle = new RecordingLifecycle {
            ParticipantAdvance = (state, _) => {
                if (state.Id != "a-first")
                    return;
                state.Value = 42;
                throw failure;
            },
        };
        runtime = new AuthoritativeSimulationRuntime<TestState, string>(
            lifecycle,
            ParticipantAdvancePolicy.BeforeEachTick);
        runtime.Read("b-second", state => state);
        var first = runtime.Read("a-first", state => state);
        lifecycle.Trace.Clear();
        var commandCalls = 0;
        var command = new SimulationTickCommand<TestState, string, int>(
            "c-new",
            (_, _) => {
                commandCalls++;
                return new SimulationStepResult<int, string>(7, ["must-not-publish"]);
            });

        var observed = Assert.Throws<InvalidOperationException>(() => runtime.ExecuteTick(2, [command]));

        Assert.Same(failure, observed);
        Assert.Equal(42, first.Value);
        Assert.Equal(["participant:a-first:1"], lifecycle.Trace);
        Assert.Equal(2, lifecycle.CreatedParticipants);
        Assert.Empty(lifecycle.WorldTicks);
        Assert.Equal(0, commandCalls);
        Assert.Equal(0UL, runtime.LastAdvancedTick);
        Assert.Throws<InvalidOperationException>(() => runtime.Read("a-first", state => state));
        Assert.Throws<InvalidOperationException>(() => runtime.Capture("checkpoint", 2, state => state));
        Assert.Throws<InvalidOperationException>(() => runtime.DrainEvents("a-first"));
    }

    private sealed class TestState(string id)
    {
        public string Id { get; } = id;
        public int Value { get; set; }
        public ulong LastAdvancedTick { get; set; }
        public uint RemainingTicks { get; set; }
        public ulong? StatusAppliedTick { get; set; }
    }

    private sealed class RecordingLifecycle : IAuthoritativeSimulationTickLifecycle<TestState, string>
    {
        public List<(string Id, ulong Tick)> ParticipantAdvances { get; } = [];
        public List<ulong> WorldTicks { get; } = [];
        public List<string> Trace { get; } = [];
        public int CreatedParticipants { get; private set; }
        public bool EmitFirstParticipantEvent { get; init; }
        public Action<TestState, ulong>? ParticipantAdvance { get; init; }

        public TestState CreateParticipant(string sessionId)
        {
            CreatedParticipants++;
            Trace.Add($"factory:{sessionId}");
            return new(sessionId);
        }

        public void AdvanceParticipant(TestState state, ulong tick)
        {
            state.LastAdvancedTick = tick;
            ParticipantAdvances.Add((state.Id, tick));
            Trace.Add($"participant:{state.Id}:{tick}");
            ParticipantAdvance?.Invoke(state, tick);
        }

        public IReadOnlyList<DirectedSimulationEvent<TestState, string>> AdvanceTick(
            ulong tick,
            IReadOnlyList<TestState> participants)
        {
            WorldTicks.Add(tick);
            Trace.Add($"world:{tick}");
            if (!EmitFirstParticipantEvent)
                return [];
            return [new(
                participants.Single(state => state.Id == "first"),
                $"world-{tick}")];
        }

        public IReadOnlyList<DirectedSimulationEvent<TestState, string>> AdvanceTickWithCommands(
            ulong tick,
            IReadOnlyList<TestState> participants,
            Func<TestState, IReadOnlyList<DirectedSimulationEvent<TestState, string>>> executeCommands)
        {
            Trace.Add($"command-world:{tick}");
            var deliveries = new List<DirectedSimulationEvent<TestState, string>>();
            foreach (var participant in participants) {
                Trace.Add($"slot:{participant.Id}");
                deliveries.AddRange(executeCommands(participant));
            }
            return deliveries;
        }
    }

    private sealed class TimedStatusLifecycle(ulong effectTick, uint duration)
        : IAuthoritativeSimulationLifecycle<TestState, string>
    {
        public TestState CreateParticipant(string sessionId) => new(sessionId);

        public void AdvanceParticipant(TestState state, ulong tick)
        {
            if (state.RemainingTicks == 0 || state.StatusAppliedTick is not ulong appliedTick || tick <= appliedTick)
                return;
            var elapsed = tick - appliedTick;
            state.RemainingTicks = state.RemainingTicks > elapsed
                ? state.RemainingTicks - (uint)elapsed
                : 0;
            state.StatusAppliedTick = tick;
        }

        public IReadOnlyList<DirectedSimulationEvent<TestState, string>> AdvanceTick(
            ulong tick,
            IReadOnlyList<TestState> participants)
        {
            if (tick == effectTick) {
                foreach (var state in participants) {
                    state.RemainingTicks = duration;
                    state.StatusAppliedTick = tick;
                }
            }
            return [];
        }
    }
}

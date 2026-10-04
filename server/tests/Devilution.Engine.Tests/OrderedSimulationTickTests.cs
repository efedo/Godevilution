using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class OrderedSimulationTickTests
{
    [Fact]
    public void CommandsUseActorOrderButResultsAndPerActorCommandsRetainInputOrder()
    {
        var lifecycle = new Lifecycle();
        var runtime = Create(lifecycle);
        runtime.AddParticipant("a", new("a"));
        runtime.AddParticipant("b", new("b"));
        var calls = new List<string>();
        SimulationTickCommand<State, string, string> Command(string id, string name) =>
            new(id, (state, participants) => {
                calls.Add(name);
                Assert.Equal(["a", "b"], participants.Select(actor => actor.Id));
                state.Value++;
                return new(name, [name]);
            });

        var results = runtime.ExecuteTick(1, [Command("b", "b-first"), Command("a", "a"), Command("b", "b-second")]);

        Assert.Equal(["a", "b-first", "b-second"], calls);
        Assert.Equal(["b-first", "a", "b-second"], results);
        Assert.Equal(["before:1", "a", "after:1"], runtime.DrainEvents("a"));
        Assert.Equal(["before:1", "b-first", "b-second", "after:1"], runtime.DrainEvents("b"));
        Assert.Equal(1UL, runtime.LastAdvancedTick);
        Assert.Equal(2, runtime.Read("b", state => state.Value));
    }

    [Fact]
    public void CatchUpUsesLegacyHooksAndEventsBeforeFactoryAdmissionAtTargetTick()
    {
        var lifecycle = new Lifecycle();
        var runtime = Create(lifecycle);
        runtime.AddParticipant("existing", new("existing"));

        runtime.ExecuteTick(3, [Command("new", state => state.Value = 7)]);

        Assert.Equal([
            "status:existing:2", "legacy:1:existing", "legacy:2:existing", "create:new",
            "status:existing:3", "status:new:3", "ordered:3:existing,new",
        ], lifecycle.Timeline);
        Assert.Equal(["legacy:1", "legacy:2", "before:3", "after:3"], runtime.DrainEvents("existing"));
        Assert.Equal(7, runtime.Read("new", state => state.Value));
    }

    [Fact]
    public void MutableInputIsDetachedBeforeAnyCatchUpHook()
    {
        var lifecycle = new Lifecycle();
        var runtime = Create(lifecycle);
        var commands = new List<SimulationTickCommand<State, string, int>> { Command("original") };
        lifecycle.BeforeLegacyTick = () => commands[0] = Command("replacement");

        runtime.ExecuteTick(2, commands);

        Assert.Equal(["original"], runtime.CaptureCohort((_, participants) => participants.Select(actor => actor.Id)));
    }

    [Fact]
    public void FactoryRngFollowsCatchUpThenFirstInputAppearanceRatherThanActorSorting()
    {
        var lifecycle = new Lifecycle();
        var runtime = Create(lifecycle);
        var random = new DeterministicLegacyRandom(7);
        var expected = new DeterministicLegacyRandom(7);
        var draws = new List<(string Phase, int Value)>();
        lifecycle.BeforeLegacyTick = () => draws.Add(("catch-up", random.RandomIntLessThan(100)));
        lifecycle.Factory = id => {
            draws.Add((id, random.RandomIntLessThan(100)));
            return new(id);
        };

        runtime.ExecuteTick(2, [Command("b"), Command("a"), Command("b")]);

        Assert.Equal([
            ("catch-up", expected.RandomIntLessThan(100)),
            ("b", expected.RandomIntLessThan(100)),
            ("a", expected.RandomIntLessThan(100)),
        ], draws);
        Assert.Equal(expected.State, random.State);
        Assert.Contains("ordered:2:a,b", lifecycle.Timeline);
    }

    [Theory]
    [InlineData("collection")]
    [InlineData("entry")]
    [InlineData("null-id")]
    [InlineData("blank-id")]
    [InlineData("operation")]
    public void InvalidInputDoesNotCatchUpCreateOrPoison(string invalid)
    {
        var lifecycle = new Lifecycle();
        var runtime = Create(lifecycle);
        IReadOnlyList<SimulationTickCommand<State, string, int>> commands = invalid switch {
            "collection" => null!,
            "entry" => [Command("valid"), null!],
            "null-id" => [Command("valid"), new(null!, (_, _) => new(0))],
            "blank-id" => [Command("valid"), new(" ", (_, _) => new(0))],
            _ => [Command("valid"), new("invalid", null!)],
        };

        Assert.ThrowsAny<ArgumentException>(() => runtime.ExecuteTick(3, commands));

        Assert.Empty(lifecycle.Timeline);
        Assert.Equal((0UL, 0), runtime.CaptureCohort((tick, actors) => (tick, actors.Count)));
        Assert.Empty(runtime.ExecuteTick<int>(1, []));
    }

    [Fact]
    public void UnsupportedLifecycleIsRefusedBeforeLegacyCatchUpAndRemainsUsable()
    {
        var legacy = new LegacyLifecycle();
        var runtime = new AuthoritativeSimulationRuntime<State, string>(legacy);

        Assert.Throws<InvalidOperationException>(() => runtime.ExecuteTick(3, [Command("player")]));

        Assert.Equal(0, legacy.Calls);
        Assert.Equal(0UL, runtime.LastAdvancedTick);
        Assert.Equal(0, runtime.Execute("player", 3, (_, _) => new SimulationStepResult<int, string>(0)));
        Assert.Equal(2, legacy.Calls);
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(4UL)]
    [InlineData(5UL)]
    public void InitializationPastAndClosedTicksRefuseWithoutMutation(ulong requested)
    {
        var lifecycle = new Lifecycle();
        var runtime = Create(lifecycle);
        runtime.InitializeCohort(5, []);

        Assert.Throws<InvalidOperationException>(() => runtime.ExecuteTick(requested, [Command("new")]));

        Assert.Empty(lifecycle.Timeline);
        Assert.Equal(5UL, runtime.LastAdvancedTick);
        Assert.Empty(runtime.ExecuteTick<int>(6, []));
    }

    [Fact]
    public void EmptyTicksVisitEveryParticipantAndCloseTheTick()
    {
        var lifecycle = new Lifecycle();
        var runtime = Create(lifecycle);
        runtime.AddParticipant("player", new("player"));

        Assert.Empty(runtime.ExecuteTick<int>(1, []));

        Assert.Equal(["status:player:1", "ordered:1:player"], lifecycle.Timeline);
        Assert.Equal(["before:1", "after:1"], runtime.DrainEvents("player"));
        Assert.Throws<InvalidOperationException>(() => runtime.ExecuteTick<int>(1, []));
    }

    [Fact]
    public void MaximumTickClosesWithoutWrappingOrReplaying()
    {
        var lifecycle = new Lifecycle();
        var runtime = Create(lifecycle);
        runtime.InitializeCohort(ulong.MaxValue - 2, [new("player", new("player"))]);

        runtime.ExecuteTick(ulong.MaxValue, [Command("player")]);

        Assert.Equal(ulong.MaxValue, runtime.LastAdvancedTick);
        Assert.Equal(1, lifecycle.Timeline.Count(entry => entry.StartsWith("legacy:", StringComparison.Ordinal)));
        Assert.Equal(1, lifecycle.Timeline.Count(entry => entry.StartsWith("ordered:", StringComparison.Ordinal)));
        Assert.Throws<InvalidOperationException>(() => runtime.ExecuteTick<int>(ulong.MaxValue, []));
    }

    [Fact]
    public void SuppliedParticipantsCannotBeCastToMutableArrayOrReordered()
    {
        var lifecycle = new Lifecycle();
        var runtime = Create(lifecycle);
        runtime.AddParticipant("a", new("a"));
        runtime.AddParticipant("b", new("b"));
        lifecycle.Ordered = (_, actors, callback) => {
            Assert.False(actors is State[]);
            Assert.Throws<NotSupportedException>(() => ((IList<State>)actors)[0] = actors[1]);
            return actors.SelectMany(callback).ToArray();
        };

        runtime.ExecuteTick(1, [new SimulationTickCommand<State, string, int>("a", (_, actors) => {
            Assert.Throws<NotSupportedException>(() => ((IList<State>)actors).Clear());
            return new(1);
        })]);

        Assert.Equal(1UL, runtime.LastAdvancedTick);
    }

    [Theory]
    [InlineData("omitted")]
    [InlineData("repeat")]
    [InlineData("foreign")]
    [InlineData("value-equal")]
    [InlineData("reverse")]
    [InlineData("null")]
    [InlineData("nested")]
    public void CallbackContractViolationsPoisonEvenWhenLifecycleCatchesThem(string violation)
    {
        var lifecycle = new Lifecycle();
        var runtime = Create(lifecycle);
        runtime.AddParticipant("a", new("a"));
        runtime.AddParticipant("b", new("b"));
        var participantB = runtime.Read("b", state => state);
        Func<State, IReadOnlyList<DirectedSimulationEvent<State, string>>>? escaped = null;
        lifecycle.Ordered = (_, actors, callback) => {
            escaped = callback;
            switch (violation) {
            case "omitted": callback(actors[0]); break;
            case "repeat": callback(actors[0]); Assert.Throws<InvalidOperationException>(() => callback(actors[0])); break;
            case "foreign": Assert.Throws<InvalidOperationException>(() => callback(new("foreign"))); break;
            case "value-equal": Assert.Throws<InvalidOperationException>(() => callback(new("a"))); break;
            case "reverse": Assert.Throws<InvalidOperationException>(() => callback(actors[1])); break;
            case "null": Assert.Throws<InvalidOperationException>(() => callback(null!)); break;
            case "nested": callback(actors[0]); break;
            }
            return [];
        };
        var command = new SimulationTickCommand<State, string, int>("a", (_, _) => {
            if (violation == "nested")
                Assert.Throws<InvalidOperationException>(() => escaped!(participantB));
            return new(1);
        });

        Assert.Throws<InvalidOperationException>(() => runtime.ExecuteTick(1, [command]));

        AssertPoisoned(runtime, 0);
    }

    [Fact]
    public void CrossThreadCallbackFailsWithoutWaitingOnTheRuntimeLockAndCannotBeSwallowed()
    {
        var lifecycle = new Lifecycle();
        var runtime = Create(lifecycle);
        runtime.AddParticipant("player", new("player"));
        lifecycle.Ordered = (_, actors, callback) => {
            var attempted = Task.Run(() => Assert.Throws<InvalidOperationException>(() => callback(actors[0])));
            Assert.True(attempted.Wait(TimeSpan.FromSeconds(5)));
            return [];
        };

        Assert.Throws<InvalidOperationException>(() => runtime.ExecuteTick<int>(1, []));

        AssertPoisoned(runtime, 0);
    }

    [Fact]
    public void EscapedCallbackAfterSuccessOrFailureCannotExecuteOrDamageAHealthyLaterTick()
    {
        var lifecycle = new Lifecycle();
        var runtime = Create(lifecycle);
        Func<State, IReadOnlyList<DirectedSimulationEvent<State, string>>>? escaped = null;
        State? player = null;
        lifecycle.Ordered = (_, actors, callback) => {
            escaped = callback;
            player = actors[0];
            return callback(player);
        };
        var calls = 0;
        runtime.ExecuteTick(1, [Command("player", _ => calls++)]);
        var oldCallback = escaped!;
        Assert.Throws<InvalidOperationException>(() => oldCallback(player!));
        lifecycle.Ordered = (_, actors, callback) => {
            Assert.Throws<InvalidOperationException>(() => oldCallback(actors[0]));
            return callback(actors[0]);
        };
        runtime.ExecuteTick(2, [Command("player", _ => calls++)]);
        Assert.Equal(2, calls);
        Assert.Equal(2UL, runtime.LastAdvancedTick);

        lifecycle.Ordered = (_, actors, callback) => {
            escaped = callback;
            throw new InvalidDataException("module failure");
        };
        Assert.Throws<InvalidDataException>(() => runtime.ExecuteTick<int>(3, []));
        Assert.Throws<InvalidOperationException>(() => escaped!(player!));
        AssertPoisoned(runtime, 2);
    }

    [Theory]
    [InlineData("restore")]
    [InlineData("cohort-capture")]
    [InlineData("initialize")]
    [InlineData("add")]
    [InlineData("try-add")]
    [InlineData("write-or-add")]
    [InlineData("execute")]
    [InlineData("execute-tick")]
    [InlineData("read")]
    [InlineData("write")]
    [InlineData("capture")]
    [InlineData("capture-all")]
    [InlineData("advance")]
    [InlineData("drain")]
    public void ReentrantRuntimeAccessPoisonsEvenWhenOperationCatchesIt(string operation)
    {
        var runtime = Create(new Lifecycle());
        var actions = RuntimeActions(runtime);
        var called = 0;
        var command = new SimulationTickCommand<State, string, int>("player", (_, _) => {
            Assert.Equal(0UL, runtime.LastAdvancedTick); // clock-only diagnostic is allowed
            Assert.Throws<InvalidOperationException>(actions[operation]);
            called++;
            return new(1);
        });

        Assert.Throws<InvalidOperationException>(() => runtime.ExecuteTick(1, [command, Command("player", _ => called++)]));

        Assert.Equal(1, called);
        AssertPoisoned(runtime, 0);
    }

    [Theory]
    [InlineData("catch-up")]
    [InlineData("catch-up-status")]
    [InlineData("factory")]
    [InlineData("factory-null")]
    [InlineData("factory-alias")]
    [InlineData("existing-alias")]
    [InlineData("target-status")]
    [InlineData("module")]
    [InlineData("module-null")]
    [InlineData("foreign-event")]
    [InlineData("operation")]
    [InlineData("operation-null-events")]
    public void MutableHookFaultsCannotPublishOrBeRetriedAsValidCheckpoints(string fault)
    {
        var lifecycle = new Lifecycle();
        var runtime = Create(lifecycle);
        var existing = new State("existing");
        runtime.AddParticipant("existing", existing);
        var shared = new State("shared");
        lifecycle.Factory = id => fault switch {
            "factory" => throw new InvalidDataException("factory failed"),
            "factory-null" => null!,
            "factory-alias" => shared,
            "existing-alias" => existing,
            _ => new(id),
        };
        lifecycle.BeforeLegacyTick = () => {
            if (fault == "catch-up") throw new InvalidDataException("catch-up failed");
        };
        lifecycle.BeforeStatus = tick => {
            if ((tick == 1 && fault == "catch-up-status") || (tick == 2 && fault == "target-status"))
                throw new InvalidDataException("status failed");
        };
        lifecycle.Ordered = (_, actors, callback) => {
            if (fault == "module") throw new InvalidDataException("module failed");
            if (fault == "module-null") return null!;
            var events = actors.SelectMany(callback).ToList();
            if (fault == "foreign-event") {
                events.Add(new(existing, "must-not-publish"));
                events.Add(new(new("foreign"), "invalid"));
            }
            return events;
        };
        var command = new SimulationTickCommand<State, string, int>("new", (_, _) => fault switch {
            "operation" => throw new InvalidDataException("operation failed"),
            "operation-null-events" => new(0, null!),
            _ => new(0, ["command"]),
        });

        Assert.ThrowsAny<Exception>(() => runtime.ExecuteTick(2, [command, Command("second")]));

        AssertPoisoned(runtime, fault.StartsWith("catch-up", StringComparison.Ordinal) ? 0UL : 1UL);
    }

    [Theory]
    [InlineData("catch-up")]
    [InlineData("factory")]
    [InlineData("status")]
    public void SwallowedReentrancyInEveryPreCommandHookStillStopsExecution(string hook)
    {
        var lifecycle = new Lifecycle();
        var runtime = Create(lifecycle);
        runtime.AddParticipant("existing", new("existing"));
        void Attempt() => Assert.Throws<InvalidOperationException>(() => runtime.DrainEvents("existing"));
        if (hook == "catch-up") lifecycle.BeforeLegacyTick = Attempt;
        if (hook == "factory") lifecycle.Factory = id => { Attempt(); return new(id); };
        if (hook == "status") lifecycle.BeforeStatus = tick => { if (tick == 2) Attempt(); };
        var ran = false;

        Assert.Throws<InvalidOperationException>(() => runtime.ExecuteTick(2, [Command("new", _ => ran = true)]));

        Assert.False(ran);
        AssertPoisoned(runtime, hook == "catch-up" ? 0UL : 1UL);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SwallowedCatchUpViolationStopsBeforeAnyLaterHookOrAdmission(bool statusHook)
    {
        var lifecycle = new Lifecycle();
        var runtime = Create(lifecycle);
        runtime.AddParticipant("a", new("a"));
        runtime.AddParticipant("b", new("b"));
        void Attempt() => Assert.Throws<InvalidOperationException>(() => runtime.CaptureCohort((_, _) => 0));
        if (statusHook) lifecycle.BeforeStatus = _ => Attempt();
        else lifecycle.BeforeLegacyTick = Attempt;
        var ran = false;

        Assert.Throws<InvalidOperationException>(() => runtime.ExecuteTick(4, [Command("new", _ => ran = true)]));

        Assert.False(ran);
        Assert.Equal(statusHook
            ? ["status:a:3"]
            : ["status:a:3", "status:b:3", "legacy:1:a,b"], lifecycle.Timeline);
        AssertPoisoned(runtime, 0);
    }

    [Fact]
    public void SwallowedCommandFaultPreventsLaterActorsAndKeepsRuntimePoisoned()
    {
        var lifecycle = new Lifecycle();
        var runtime = Create(lifecycle);
        var laterRan = false;
        lifecycle.Ordered = (_, actors, callback) => {
            Assert.Throws<InvalidDataException>(() => callback(actors[0]));
            Assert.Throws<InvalidOperationException>(() => callback(actors[1]));
            return [];
        };

        Assert.Throws<InvalidOperationException>(() => runtime.ExecuteTick(1, [
            new SimulationTickCommand<State, string, int>("a", (state, _) => {
                state.Value++;
                throw new InvalidDataException("mutated then failed");
            }),
            Command("b", _ => laterRan = true),
        ]));

        Assert.False(laterRan);
        AssertPoisoned(runtime, 0);
    }

    [Fact]
    public void OrdinaryLegacyOperationsRemainAvailableAfterSuccessfulOrderedTicks()
    {
        var lifecycle = new Lifecycle();
        var runtime = Create(lifecycle);
        runtime.ExecuteTick(1, [Command("player")]);

        Assert.Equal(9, runtime.Execute("player", 9, (_, _) => new SimulationStepResult<int, string>(9, ["legacy-command"])));
        Assert.Equal(1UL, runtime.LastAdvancedTick);
        runtime.AdvanceTo(2);

        Assert.Equal(2UL, runtime.LastAdvancedTick);
        Assert.Equal(["before:1", "after:1", "legacy-command", "legacy:2"], runtime.DrainEvents("player"));
        runtime.RestoreAtTick(4);
        Assert.Equal(4UL, runtime.LastAdvancedTick);
    }

    private static AuthoritativeSimulationRuntime<State, string> Create(Lifecycle lifecycle) => new(lifecycle);

    private static SimulationTickCommand<State, string, int> Command(string id, Action<State>? operation = null) =>
        new(id, (state, _) => { operation?.Invoke(state); return new(0); });

    private static Dictionary<string, Action> RuntimeActions(AuthoritativeSimulationRuntime<State, string> runtime) => new() {
        ["restore"] = () => runtime.RestoreAtTick(8),
        ["cohort-capture"] = () => runtime.CaptureCohort((_, _) => 0),
        ["initialize"] = () => runtime.InitializeCohort(0, []),
        ["add"] = () => runtime.AddParticipant("new", new("new")),
        ["try-add"] = () => runtime.TryAddParticipant("new", new("new")),
        ["write-or-add"] = () => runtime.WriteOrAddParticipant("new", () => new("new"), _ => { }),
        ["execute"] = () => runtime.Execute("player", 8, (_, _) => new SimulationStepResult<int, string>(0)),
        ["execute-tick"] = () => runtime.ExecuteTick<int>(8, []),
        ["read"] = () => runtime.Read("player", _ => 0),
        ["write"] = () => runtime.Write("player", _ => { }),
        ["capture"] = () => runtime.Capture("player", 8, _ => 0),
        ["capture-all"] = () => runtime.Capture("player", 8, (_, _) => 0),
        ["advance"] = () => runtime.AdvanceTo(8),
        ["drain"] = () => runtime.DrainEvents("player"),
    };

    private static void AssertPoisoned(AuthoritativeSimulationRuntime<State, string> runtime, ulong lastTick)
    {
        Assert.Equal(lastTick, runtime.LastAdvancedTick);
        foreach (var action in RuntimeActions(runtime).Values)
            Assert.Throws<InvalidOperationException>(action);
    }

    private sealed record State(string Id)
    {
        public int Value { get; set; }
    }

    private sealed class Lifecycle : IAuthoritativeSimulationTickLifecycle<State, string>
    {
        public List<string> Timeline { get; } = [];
        public Func<string, State>? Factory { get; set; }
        public Action? BeforeLegacyTick { get; set; }
        public Action<ulong>? BeforeStatus { get; set; }
        public Func<ulong, IReadOnlyList<State>, Func<State, IReadOnlyList<DirectedSimulationEvent<State, string>>>,
            IReadOnlyList<DirectedSimulationEvent<State, string>>>? Ordered { get; set; }

        public State CreateParticipant(string sessionId)
        {
            Timeline.Add($"create:{sessionId}");
            return Factory is null ? new(sessionId) : Factory(sessionId);
        }

        public void AdvanceParticipant(State state, ulong tick)
        {
            Timeline.Add($"status:{state.Id}:{tick}");
            BeforeStatus?.Invoke(tick);
        }

        public IReadOnlyList<DirectedSimulationEvent<State, string>> AdvanceTick(ulong tick, IReadOnlyList<State> participants)
        {
            Timeline.Add($"legacy:{tick}:{string.Join(',', participants.Select(actor => actor.Id))}");
            BeforeLegacyTick?.Invoke();
            return participants.Select(actor => new DirectedSimulationEvent<State, string>(actor, $"legacy:{tick}")).ToArray();
        }

        public IReadOnlyList<DirectedSimulationEvent<State, string>> AdvanceTickWithCommands(
            ulong tick, IReadOnlyList<State> participants,
            Func<State, IReadOnlyList<DirectedSimulationEvent<State, string>>> executeCommands)
        {
            Timeline.Add($"ordered:{tick}:{string.Join(',', participants.Select(actor => actor.Id))}");
            if (Ordered is not null) return Ordered(tick, participants, executeCommands);
            var events = new List<DirectedSimulationEvent<State, string>>();
            foreach (var actor in participants) {
                events.Add(new(actor, $"before:{tick}"));
                events.AddRange(executeCommands(actor));
                events.Add(new(actor, $"after:{tick}"));
            }
            return events;
        }
    }

    private sealed class LegacyLifecycle : IAuthoritativeSimulationLifecycle<State, string>
    {
        public int Calls { get; private set; }
        public State CreateParticipant(string sessionId) { Calls++; return new(sessionId); }
        public void AdvanceParticipant(State state, ulong tick) => Calls++;
        public IReadOnlyList<DirectedSimulationEvent<State, string>> AdvanceTick(ulong tick, IReadOnlyList<State> participants)
        { Calls++; return []; }
    }
}

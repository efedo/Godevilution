using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class SimulationSessionCoordinatorTests
{
    [Fact]
    public void CreatesStateOnceAndReturnsSessionsInOrdinalOrder()
    {
        var created = new List<string>();
        var coordinator = new SimulationSessionCoordinator<TestState, string>(sessionId => {
            created.Add(sessionId);
            return new TestState(sessionId);
        });

        var second = coordinator.GetOrCreate("session-b");
        var first = coordinator.GetOrCreate("session-a");

        Assert.Same(second, coordinator.GetOrCreate("session-b"));
        Assert.Equal(["session-b", "session-a"], created);
        Assert.Equal(
            ["session-a", "session-b"],
            coordinator.GetOrderedSessions().Select(session => session.Id));
        Assert.Same(first, coordinator.GetOrderedSessions()[0].State);
        Assert.Equal("session-b", coordinator.GetRequiredSessionId(second));
    }

    [Fact]
    public void QueuesAndDrainsEventsPerSession()
    {
        var coordinator = new SimulationSessionCoordinator<TestState, string>(
            sessionId => new TestState(sessionId));
        var first = coordinator.GetOrCreate("first");
        coordinator.GetOrCreate("second");

        coordinator.EnqueueEvent(first, "one");
        coordinator.EnqueueEvent("first", "two");
        coordinator.EnqueueEvent("second", "other");

        Assert.Equal(["one", "two"], coordinator.DrainEvents("first"));
        Assert.Empty(coordinator.DrainEvents("first"));
        Assert.Equal(["other"], coordinator.DrainEvents("second"));
    }

    [Fact]
    public void AddsExplicitParticipantStateWithoutCallingTheFallbackFactory()
    {
        var factoryCalls = 0;
        var coordinator = new SimulationSessionCoordinator<TestState, string>(_ => {
            factoryCalls++;
            return new TestState("fallback");
        });
        var explicitState = new TestState("explicit");

        coordinator.Add("selected", explicitState);

        Assert.Equal(0, factoryCalls);
        Assert.Same(explicitState, coordinator.GetOrCreate("selected"));
        Assert.False(coordinator.TryAdd("selected", new TestState("ignored")));
        Assert.True(coordinator.TryAdd("other", new TestState("other")));
        Assert.Throws<InvalidOperationException>(() =>
            coordinator.Add("selected", new TestState("replacement")));
        Assert.Throws<InvalidOperationException>(() =>
            coordinator.Add("third", explicitState));
    }

    [Fact]
    public void RejectsInvalidSessionsForeignStatesAndSharedFactoryState()
    {
        var shared = new TestState("shared");
        var coordinator = new SimulationSessionCoordinator<TestState, string>(_ => shared);
        coordinator.GetOrCreate("first");

        Assert.Throws<ArgumentException>(() => coordinator.GetOrCreate(" "));
        Assert.Throws<InvalidOperationException>(() => coordinator.GetOrCreate("second"));
        Assert.Throws<InvalidOperationException>(() =>
            coordinator.EnqueueEvent("missing", "event"));
        Assert.Throws<InvalidOperationException>(() =>
            coordinator.GetRequiredSessionId(new TestState("foreign")));
    }

    private sealed record TestState(string Name);
}

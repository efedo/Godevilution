using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Focused module coverage for detached cohort capture and fresh installation. */
public sealed class DiabloSimulationCohortTests
{
    [Fact]
    public void CaptureAndFreshCandidatePreserveRosterTickAndDetachedPlayerState()
    {
        var options = CreateOptions();
        var simulation = DiabloSimulationFactory.Instance.Create(options);
        var runtime = simulation.CreateRuntime(
            DiabloPlayerTemplate.CreateDefault(3, 0, 0, 0));
        var player = DiabloPlayerTemplate.CreateDefault(3, 0, 0, 0).CreatePlayer();
        player.EntityId = 17;
        runtime.AddParticipant("p", player);
        runtime.RestoreAtTick(9);

        var captured = simulation.CaptureCohortState(runtime);
        player.Gold = 99;

        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(
            options,
            captured);
        Assert.Throws<InvalidOperationException>(() => candidate.Simulation.CreateRuntime(
            DiabloPlayerTemplate.CreateDefault(3, 0, 0, 0)));
        var restoredRuntime = candidate.CreateRuntime();
        var recaptured = candidate.Simulation.CaptureCohortState(restoredRuntime);

        Assert.Equal(9UL, recaptured.Tick);
        Assert.Equal("p", Assert.Single(recaptured.Participants).SessionId);
        Assert.Equal(17U, Assert.Single(recaptured.Participants).EntityId);
        Assert.Equal(3U, Assert.Single(recaptured.Participants).Player.Gold);
        Assert.Empty(recaptured.WorldInfrastructure.EffectiveLevels!);
        Assert.Equal(captured.RandomState, recaptured.RandomState);
        Assert.Throws<InvalidOperationException>(() => candidate.CreateRuntime());
    }

    [Fact]
    public void CaptureRejectsRuntimeOwnedByAnotherSimulation()
    {
        var options = CreateOptions();
        var first = DiabloSimulationFactory.Instance.Create(options);
        var second = DiabloSimulationFactory.Instance.Create(options);
        var foreignRuntime = second.CreateRuntime(
            DiabloPlayerTemplate.CreateDefault(0, 0, 0, 0));

        Assert.Throws<InvalidOperationException>(() => first.CaptureCohortState(foreignRuntime));
    }

    [Fact]
    public void CandidateRejectsParticipantStateThatWouldBeNormalized()
    {
        var options = CreateOptions();
        var simulation = DiabloSimulationFactory.Instance.Create(options);
        var runtime = simulation.CreateRuntime(
            DiabloPlayerTemplate.CreateDefault(3, 0, 0, 0));
        var player = DiabloPlayerTemplate.CreateDefault(3, 0, 0, 0).CreatePlayer();
        player.EntityId = 17;
        runtime.AddParticipant("p", player);
        runtime.RestoreAtTick(9);
        var captured = simulation.CaptureCohortState(runtime);
        var participant = Assert.Single(captured.Participants);

        var malformedStates = new[] {
            participant with {
                Player = participant.Player with { PositionX = -1 },
            },
            participant with {
                Player = participant.Player with {
                    Life = participant.Player.LifeMaximum + 1,
                },
            },
            participant with {
                Player = participant.Player with {
                    LightRadius = participant.Player.LightRadius + 1,
                },
            },
        };

        foreach (var malformed in malformedStates) {
            var state = captured with { Participants = [malformed] };
            Assert.Throws<InvalidDataException>(() =>
                DiabloSimulationFactory.Instance.CreateCohortCandidate(options, state));
        }
    }

    [Fact]
    public void CaptureRejectsMultipleLegacyRuntimeOwners()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(CreateOptions());
        var template = DiabloPlayerTemplate.CreateDefault(0, 0, 0, 0);
        var first = simulation.CreateRuntime(template);
        var second = simulation.CreateRuntime(template);

        Assert.Throws<InvalidOperationException>(() => simulation.CaptureCohortState(first));
        Assert.Throws<InvalidOperationException>(() => simulation.CaptureCohortState(second));
    }

    [Fact]
    public void CompleteCaptureRejectsAnUnboundLegacyParticipant()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(CreateOptions());
        var runtime = simulation.CreateRuntime(DiabloPlayerTemplate.CreateDefault(0, 0, 0, 0));
        runtime.Write("unbound", _ => { });

        Assert.Throws<InvalidDataException>(() => simulation.CaptureCohortState(runtime));
        Assert.Equal(0U, runtime.Read("unbound", player => player.EntityId));
    }

    [Fact]
    public void FailedRuntimeConstructionDoesNotPoisonOwnerBinding()
    {
        var options = new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
            FallbackWidth = 8,
            FallbackHeight = 8,
            FallbackBlockedCells = [0],
        };
        var simulation = DiabloSimulationFactory.Instance.Create(options);
        Assert.Throws<InvalidDataException>(() => simulation.CreateRuntime(
            DiabloPlayerTemplate.CreateDefault(0, 0, 0, 0)));
        var template = new DiabloPlayerTemplate(
            0, 0, 0, 0, 0, null, null, null, null, 1, 1, 0, 1, 0, 0);
        var runtime = simulation.CreateRuntime(template);
        runtime.Write("p", player => player.EntityId = 17);

        Assert.Equal(17U, Assert.Single(simulation.CaptureCohortState(runtime).Participants).EntityId);
    }

    [Fact]
    public void CaptureInsideAnOwnedRuntimeWriteUsesTheSameBarrier()
    {
        var simulation = DiabloSimulationFactory.Instance.Create(CreateOptions());
        var runtime = simulation.CreateRuntime(DiabloPlayerTemplate.CreateDefault(0, 0, 0, 0));
        DiabloCohortState? captured = null;
        runtime.Write("p", player => {
            player.EntityId = 17;
            captured = simulation.CaptureCohortState(runtime);
        });

        Assert.NotNull(captured);
        Assert.Equal(0UL, captured.Tick);
        Assert.Equal(17U, Assert.Single(captured.Participants).EntityId);
    }

    private static DiabloSimulationOptions CreateOptions()
        => new(new StoreCatalog(), DiabloModule.Instance.Rules) {
            FallbackWidth = 8,
            FallbackHeight = 8,
            FallbackBlockedCells = [],
            FallbackMissileBlockedCells = [],
        };
}

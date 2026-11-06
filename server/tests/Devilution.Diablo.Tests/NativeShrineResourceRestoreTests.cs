using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class NativeShrineResourceRestoreTests
{
    [Theory]
    [InlineData(false, 6400)]
    [InlineData(true, 6400)]
    [InlineData(false, -1)]
    [InlineData(true, -1)]
    public void NegativeManaWithoutNativeBalancesIsRejectedAtLoad(bool cohort, int maximum)
    {
        var fixture = Create();
        var captured = fixture.Simulation.CaptureSnapshot(fixture.Player, 0);
        Assert.Null(captured.Player.NativeManaBase);
        var corrupt = captured with { Player = captured.Player with { Mana = -1, ManaMaximum = maximum } };
        if (cohort) {
            var state = fixture.Simulation.CaptureCohortState(fixture.Runtime);
            var participant = Assert.Single(state.Participants);
            Assert.Throws<InvalidDataException>(() => DiabloSimulationFactory.Instance.CreateCohortCandidate(fixture.Options,
                state with { Participants = [participant with { Player = corrupt.Player }] }));
        } else {
            Assert.Throws<InvalidDataException>(() => fixture.Simulation.RestoreSnapshot(fixture.Player, corrupt));
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void LegacyPositiveAndExplicitNativeNegativeManaStillLoad(bool cohort, bool native)
    {
        var fixture = Create();
        if (native) {
            fixture.Player.RestoreNativeMana(-64, 6400);
            fixture.Player.RestoreNativeResourceBases(6400, -64);
        }
        var captured = fixture.Simulation.CaptureSnapshot(fixture.Player, 0);
        if (cohort) {
            var state = fixture.Simulation.CaptureCohortState(fixture.Runtime);
            var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(fixture.Options, state);
            var restored = candidate.Simulation.CaptureCohortState(candidate.CreateRuntime());
            Assert.Equal(native ? -64 : 6400, Assert.Single(restored.Participants).Player.Mana);
        } else {
            fixture.Simulation.RestoreSnapshot(fixture.Player, captured);
            Assert.Equal(native ? -64 : 6400, fixture.Player.Mana);
        }
    }

    private static (DiabloSimulationOptions Options, DiabloSimulation Simulation, AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent> Runtime, AuthoritativePlayerState Player) Create()
    {
        var options = new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules) {
            FallbackWidth = 8, FallbackHeight = 8,
        };
        var simulation = DiabloSimulationFactory.Instance.Create(options);
        var template = DiabloPlayerTemplate.CreateDefault(0, 6400, 6400, 0);
        var player = template.CreatePlayer(); player.EntityId = 1;
        var runtime = simulation.CreateRuntime(template); runtime.AddParticipant("hero", player);
        return (options, simulation, runtime, player);
    }
}

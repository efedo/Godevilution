using System.Text.Json;
using Devilution.Data;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Checks timed Search status age across ordinary and restored runtime catch-up. */
public sealed class TimedStatusCatchUpTests
{
    private const uint MonkClassId = 3;
    private const uint SearchSpellId = 46;
    private const uint CathedralLevelId = 1;
    private const int StartX = 20;
    private const int StartY = 20;
    private const ulong CastStartTick = 8;

    private sealed record CastContext(
        DiabloContentActivation Activation,
        DiabloSimulation Simulation,
        AuthoritativeSimulationRuntime<AuthoritativePlayerState, DiabloGameplayEvent> Runtime,
        string SessionId,
        ulong EffectTick);

    [Fact]
    public void SearchDurationAgesFromItsEffectTickDuringSteppedAndJumpCatchUp()
    {
        var stepped = CreateAndCast("search-catch-up-stepped");
        var jumped = CreateAndCast("search-catch-up-jumped");
        Assert.Equal(stepped.EffectTick, jumped.EffectTick);
        Assert.True(stepped.EffectTick > CastStartTick);

        stepped.Runtime.AdvanceTo(stepped.EffectTick - 1);
        jumped.Runtime.AdvanceTo(jumped.EffectTick - 1);
        Assert.Empty(ReadSearchEffects(stepped));
        Assert.Empty(ReadSearchEffects(jumped));

        stepped.Runtime.AdvanceTo(stepped.EffectTick);
        Assert.Equal(247U, ReadSearchDuration(stepped));
        Assert.Equal(247U, stepped.Runtime.Capture(
            stepped.SessionId,
            stepped.EffectTick,
            player => SearchDuration(player)));

        stepped.Runtime.AdvanceTo(stepped.EffectTick + 1);
        Assert.Equal(246U, ReadSearchDuration(stepped));
        stepped.Runtime.AdvanceTo(stepped.EffectTick + 2);
        stepped.Runtime.AdvanceTo(stepped.EffectTick + 3);
        jumped.Runtime.AdvanceTo(jumped.EffectTick + 3);

        Assert.Equal(244U, ReadSearchDuration(stepped));
        Assert.Equal(244U, ReadSearchDuration(jumped));

        var expiryTick = checked(stepped.EffectTick + 247UL);
        stepped.Runtime.AdvanceTo(expiryTick);
        jumped.Runtime.AdvanceTo(expiryTick);
        Assert.Empty(ReadSearchEffects(stepped));
        Assert.Empty(ReadSearchEffects(jumped));
    }

    [Fact]
    public void RestoredCohortAgesSearchStatusAcrossTheSameCatchUpTicks()
    {
        var source = CreateAndCast("search-catch-up-cohort");
        source.Runtime.AdvanceTo(source.EffectTick);
        Assert.Equal(247U, ReadSearchDuration(source));

        var checkpoint = source.Simulation.CaptureCohortState(source.Runtime);
        var candidateFactory = source.Activation.CohortCandidateFactory
            ?? throw new InvalidOperationException("The activated module has no cohort candidate factory.");
        var candidate = candidateFactory(checkpoint);
        var restoredRuntime = candidate.CreateRuntime();
        var restored = new CastContext(
            source.Activation,
            candidate.Simulation,
            restoredRuntime,
            source.SessionId,
            source.EffectTick);

        AssertExactCohort(checkpoint, restored.Simulation.CaptureCohortState(restored.Runtime));
        Assert.Equal(source.EffectTick, candidate.Tick);
        Assert.Equal(11U, source.Runtime.Read(source.SessionId, player => player.EntityId));
        Assert.Equal(11U, restored.Runtime.Read(restored.SessionId, player => player.EntityId));
        Assert.Equal(247U, ReadSearchDuration(restored));
        Assert.Equal((ulong?)source.EffectTick,
            restored.Runtime.Read(restored.SessionId, player => player.LastAppliedTick));
        Assert.True(restored.Runtime.Read(
            restored.SessionId,
            player => player.CurrentAction?.EffectApplied ?? false));
        source.Runtime.AdvanceTo(source.EffectTick + 1);
        restored.Runtime.AdvanceTo(restored.EffectTick + 1);
        Assert.Equal(246U, ReadSearchDuration(source));
        Assert.Equal(246U, ReadSearchDuration(restored));
        AssertExactCohort(
            source.Simulation.CaptureCohortState(source.Runtime),
            restored.Simulation.CaptureCohortState(restored.Runtime));
        source.Runtime.AdvanceTo(source.EffectTick + 2);
        source.Runtime.AdvanceTo(source.EffectTick + 3);
        restored.Runtime.AdvanceTo(restored.EffectTick + 3);

        Assert.Equal(244U, ReadSearchDuration(source));
        Assert.Equal(244U, ReadSearchDuration(restored));
        AssertExactCohort(
            source.Simulation.CaptureCohortState(source.Runtime),
            restored.Simulation.CaptureCohortState(restored.Runtime));

        var expiryTick = checked(source.EffectTick + 247UL);
        source.Runtime.AdvanceTo(expiryTick);
        restored.Runtime.AdvanceTo(expiryTick);
        Assert.Empty(ReadSearchEffects(source));
        Assert.Empty(ReadSearchEffects(restored));
        AssertExactCohort(
            source.Simulation.CaptureCohortState(source.Runtime),
            restored.Simulation.CaptureCohortState(restored.Runtime));
    }

    private static CastContext CreateAndCast(string sessionId)
    {
        var root = FindRepositoryRoot();
        var basePack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(root, "server", "content", "base"));
        var content = ComposedContent.Compose(
            new ContentManifest("devilution.base", "0.1.0", [basePack]));
        var activation = DiabloModule.Instance.ContentBootstrap.Activate(content, randomSeed: 0);
        var world = Assert.IsType<AuthoritativeWorld>(activation.Simulation.WorldState.World);
        Assert.True(world.IsWalkable(CathedralLevelId, StartX, StartY));
        Assert.DoesNotContain(activation.Simulation.WorldState.CombatTargets.Values,
            target => target.LevelId == CathedralLevelId
                && DeterministicGridMetrics.ChebyshevDistance(
                    target.PositionX, target.PositionY, StartX, StartY) <= target.AggroRange);

        var loadout = activation.PlayerLoadouts.GetRequired(MonkClassId);
        Assert.Equal(SearchSpellId, loadout.AbilitySpellId);
        Assert.Equal(DiabloPlayerAbilityKind.Search, loadout.AbilityKind);
        var template = activation.PlayerLoadouts.CreateTemplate(
            sessionId,
            MonkClassId,
            null,
            null,
            null,
            CathedralLevelId,
            startingPositionX: StartX,
            startingPositionY: StartY);
        var runtime = activation.Simulation.CreateRuntime(template);
        runtime.Write(sessionId, player => player.EntityId = 11);
        runtime.AdvanceTo(CastStartTick);

        var castResult = runtime.Execute(
            sessionId,
            CastStartTick,
            (player, participants) => {
                var result = activation.Simulation.Execute(
                    player,
                    new DiabloCastIntent(
                        SearchSpellId,
                        0,
                        player.PositionX,
                        player.PositionY,
                        DiabloCastSourceKind.Ability),
                    CastStartTick,
                    participants);
                return new SimulationStepResult<DiabloCombatIntentResult, DiabloGameplayEvent>(
                    result,
                    result.Events);
            });
        Assert.True(castResult.Succeeded, castResult.Failure?.ToString());

        var action = runtime.Read(sessionId, player => player.CurrentAction);
        Assert.NotNull(action);
        Assert.Equal(DiabloPlayerActionKind.Cast, action!.Kind);
        Assert.Equal(SearchSpellId, action.CastIntent?.SpellId);
        Assert.Equal(DiabloCastSourceKind.Ability, action.CastIntent?.Source);
        var playerIdentity = runtime.Read(sessionId, player => (
            player.CharacterClassId,
            player.CharacterLevel,
            player.AbilitySpellId,
            player.AbilityKind,
            player.EntityId,
            player.LevelId,
            player.PositionX,
            player.PositionY));
        Assert.Equal(MonkClassId, playerIdentity.CharacterClassId);
        Assert.Equal(1U, playerIdentity.CharacterLevel);
        Assert.Equal(SearchSpellId, playerIdentity.AbilitySpellId);
        Assert.Equal(DiabloPlayerAbilityKind.Search, playerIdentity.AbilityKind);
        Assert.Equal(11U, playerIdentity.EntityId);
        Assert.Equal(CathedralLevelId, playerIdentity.LevelId);
        Assert.Equal((StartX, StartY), (playerIdentity.PositionX, playerIdentity.PositionY));

        return new CastContext(activation, activation.Simulation, runtime, sessionId, action.EffectTick);
    }

    private static void AssertExactCohort(DiabloCohortState expected, DiabloCohortState actual)
        => Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));

    private static AuthoritativeStatusEffect[] ReadSearchEffects(CastContext context)
        => context.Runtime.Read(context.SessionId,
            player => player.StatusEffects
                .Where(effect => effect.Behavior == AuthoritativeStatusEffectBehavior.Search)
                .ToArray());

    private static uint ReadSearchDuration(CastContext context)
        => context.Runtime.Read(context.SessionId, SearchDuration);

    private static uint SearchDuration(AuthoritativePlayerState player)
        => Assert.Single(player.StatusEffects,
            effect => effect.Behavior == AuthoritativeStatusEffectBehavior.Search).RemainingTicks;

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent) {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository.");
    }
}

using System.Text.Json;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Explicit native policy, existing simulation decisions and detached continuation. */
public sealed class NativeMonsterInitializationTests
{
    private const string Fingerprint = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Theory]
    [InlineData(0U, 1U)]
    [InlineData(1U, 0U)]
    [InlineData(0U, 0U)]
    [InlineData(0x80000000U, 0xFFFFFFFFU)]
    public void NativePolicyRetainsExactSeedsFractionalHpAndCohortContinuation(uint itemSeed, uint aiSeed)
    {
        var target = Target(itemSeed, aiSeed);
        target.RestoreHitPointsQ6(1, 65);
        var options = Options(target);
        var simulation = DiabloSimulationFactory.Instance.Create(options);
        var runtime = simulation.CreateRuntime(DiabloPlayerTemplate.CreateDefault(100, 50, 0, 0));
        runtime.Write("hero", player => { player.EntityId = 1; player.PositionX = 1; player.PositionY = 1; });
        var captured = simulation.CaptureCohortState(runtime);
        Assert.Equal(DiabloCohortState.NativeMonsterFormatVersion, captured.FormatVersion);
        var state = Assert.Single(captured.CombatTargets);
        Assert.True(state.Monster.NativeInitializationPresent);
        Assert.Equal((itemSeed, aiSeed), (state.Monster.MonsterItemSeed, state.Monster.MonsterAiSeed));
        Assert.Equal((1, 65), (state.Monster.HitPointsQ6, state.Monster.MaxHitPointsQ6));
        Assert.Equal(target.NativeInitialization, state.NativeInitialization);

        var candidate = DiabloSimulationFactory.Instance.CreateCohortCandidate(options, captured);
        var restored = candidate.CreateRuntime();
        Assert.Equal(JsonSerializer.Serialize(captured),
            JsonSerializer.Serialize(candidate.Simulation.CaptureCohortState(restored)));
        runtime.AdvanceTo(1);
        restored.AdvanceTo(1);
        Assert.Equal(JsonSerializer.Serialize(simulation.CaptureCohortState(runtime)),
            JsonSerializer.Serialize(candidate.Simulation.CaptureCohortState(restored)));
        Assert.True(candidate.Simulation.WorldState.CombatTargets[9].UsesNativeRandomSeeds);
    }

    [Theory]
    [InlineData(0U, 0U)]
    [InlineData(1U, 0U)]
    public void ExplicitZeroAiSeedUsesExistingNativeMultiplayerProcessStream(uint itemSeed, uint aiSeed)
    {
        var target = Target(itemSeed, aiSeed);
        var random = new DeterministicLegacyRandom(0xCAFEBABE);
        var expected = new DeterministicLegacyRandom(aiSeed);
        var expectedSeed = unchecked((uint)expected.AdvanceRndSeed());
        var processor = Processor(target, random, DiabloGameMode.Multiplayer);
        processor.AdvanceTick(1, [Player()]);
        Assert.Equal(itemSeed, target.MonsterItemSeed);
        Assert.Equal(expectedSeed, target.MonsterAiSeed);
        Assert.Equal(expected.State, random.State);
        Assert.True(target.NativeInitializationPresent);
    }

    [Fact]
    public void NativeZeroExperienceDoesNotUseLegacyRewardFallback()
    {
        var target = Target(0, 1);
        target.RestoreHitPointsQ6(1, 64);
        var player = Player();
        player.PositionX = 2; player.PositionY = 3;
        var result = Processor(target, new(71), DiabloGameMode.SinglePlayer)
            .Execute(player, new DiabloAttackIntent(9), 1);
        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.Equal(0, target.HitPointsQ6);
        Assert.Contains(result.Events, value => value.Kind == DiabloGameplayEventKind.Experience && value.Amount == 0);
    }

    [Fact]
    public void LegacyConstructorAndUninitializedZeroPolicyRemainUnchanged()
    {
        Assert.Throws<ArgumentException>(() => new AuthoritativeCombatTarget(9, 3, 3, 1,
            monsterItemSeed: 0, monsterAiSeed: 1));
        var legacy = new AuthoritativeCombatTarget(9, 3, 3, 1);
        Assert.False(legacy.NativeInitializationPresent);
        Assert.False(legacy.UsesNativeRandomSeeds);
        var random = new DeterministicLegacyRandom(71);
        Processor(legacy, random, DiabloGameMode.Multiplayer).AdvanceTick(1, [Player()]);
        Assert.Equal(71U, random.State);
    }

    [Fact]
    public void MetadataAndLegacyRestoreFencesRejectBeforeMutation()
    {
        var target = Target(0, 1);
        Assert.Throws<InvalidDataException>(() => DiabloSimulationFactory.Instance.Create(new(
            new StoreCatalog(), DiabloModule.Instance.Rules) { CombatTargets = [target] }));
        var options = Options(target);
        var simulation = DiabloSimulationFactory.Instance.Create(options);
        var runtime = simulation.CreateRuntime(DiabloPlayerTemplate.CreateDefault(100, 50, 0, 0));
        runtime.Write("hero", player => { player.EntityId = 1; player.PositionX = 1; player.PositionY = 1; });
        var captured = simulation.CaptureCohortState(runtime);
        Assert.Throws<InvalidDataException>(() => DiabloSimulationFactory.Instance.CreateCohortCandidate(options,
            captured with { Configuration = captured.Configuration with {
                NativeMonsterMetadataFingerprint = new string('B', 64),
            } }));
        runtime.Write("hero", player => Assert.Throws<InvalidDataException>(() =>
            simulation.RestoreSnapshot(player, simulation.CaptureSnapshot(player, 0))));
        Assert.Equal(JsonSerializer.Serialize(captured), JsonSerializer.Serialize(simulation.CaptureCohortState(runtime)));
    }

    [Theory]
    [InlineData("slot")]
    [InlineData("type")]
    [InlineData("unique")]
    [InlineData("entity")]
    [InlineData("content")]
    [InlineData("level")]
    public void InconsistentNativeBindingsRejectBeforeChangingActor(string defect)
    {
        var target = Target(0, 1);
        var original = target.NativeInitialization!;
        var binding = original.Binding;
        var changed = defect switch {
            "slot" => binding with { NativeSlot = 8 },
            "type" => binding with { NativeTypeId = 1 },
            "unique" => binding with { NativeUniqueId = 1 },
            "entity" => binding with { EntityId = 10 },
            "content" => binding with { ContentMonsterId = 10 },
            "level" => binding with { LevelId = 10 },
            _ => throw new InvalidOperationException(),
        };
        Assert.Throws<InvalidDataException>(() => target.RestoreNativeInitialization(
            original with { Binding = changed }, 99, 100));
        Assert.Same(original, target.NativeInitialization);
        Assert.Equal((0U, 1U), (target.MonsterItemSeed, target.MonsterAiSeed));
    }

    [Theory]
    [InlineData("bad\0identity")]
    [InlineData("bad\nidentity")]
    [InlineData("surrogate")]
    public void NativeUniqueBindingRejectsControlAndUnpairedSurrogateText(string identity)
    {
        if (identity == "surrogate") identity = new string((char)0xD800, 1);
        var original = Target(0, 1).NativeInitialization!;
        var unique = original with {
            InitialState = original.InitialState with { UniqueType = 0 },
            Binding = original.Binding with { NativeUniqueId = 0, UniqueIdentity = identity },
        };
        Assert.Throws<InvalidDataException>(unique.Validate);
    }

    [Fact]
    public void NativeFactoryRejectsDuplicateLevelSlotBeforeFirstPublication()
    {
        var first = Target(0, 1);
        var second = new AuthoritativeCombatTarget(10, 4, 4, 10, monsterId: first.MonsterId);
        second.RestoreNativeInitialization(first.NativeInitialization! with {
            Binding = first.NativeInitialization!.Binding with { EntityId = second.EntityId },
        }, 0, 1);
        Assert.Throws<InvalidDataException>(() => DiabloSimulationFactory.Instance.Create(new(
            new StoreCatalog(), DiabloModule.Instance.Rules) {
                CombatTargets = [first, second], NativeMonsterMetadataFingerprint = Fingerprint,
            }));
    }

    [Theory]
    [InlineData(DiabloGameMode.Multiplayer, DiabloDifficulty.Normal)]
    [InlineData(DiabloGameMode.SinglePlayer, DiabloDifficulty.Nightmare)]
    [InlineData(DiabloGameMode.SinglePlayer, DiabloDifficulty.Hell)]
    public void NativeMetadataProfileRejectsUnsupportedModeAndDifficultyEvenWithoutActors(
        DiabloGameMode mode, DiabloDifficulty difficulty)
    {
        Assert.Throws<InvalidDataException>(() => DiabloSimulationFactory.Instance.Create(new(
            new StoreCatalog(), DiabloModule.Instance.Rules) {
                GameMode = mode, Difficulty = difficulty, NativeMonsterMetadataFingerprint = Fingerprint,
            }));
    }

    private static DiabloSimulationOptions Options(AuthoritativeCombatTarget target) => new(
        new StoreCatalog(), DiabloModule.Instance.Rules) {
        FallbackWidth = 8, FallbackHeight = 8, CombatTargets = [target], RandomSeed = 71,
        NativeMonsterMetadataFingerprint = Fingerprint,
    };

    private static AuthoritativeCombatTarget Target(uint itemSeed, uint aiSeed)
    {
        var target = new AuthoritativeCombatTarget(9, 3, 3, 10, maxHitPoints: 10, monsterId: 90);
        var raw = new AuthoritativeCathedralPopulationMonsterState {
            Slot = 7, Type = 0, LevelType = 1, UniqueType = 255, Leader = 255,
            Tile = new(3, 3), Future = new(3, 3), Old = new(3, 3), LightId = -1,
            HitPoints = 640, MaxHitPoints = 640, RndItemSeed = itemSeed, AiSeed = aiSeed,
        };
        target.RestoreNativeInitialization(new(Fingerprint, raw, 0, 0, 1, 1,
            new(raw.Slot, raw.Type, raw.UniqueType, target.EntityId, target.MonsterId, target.LevelId, string.Empty), false),
            itemSeed, aiSeed);
        return target;
    }

    private static DiabloCombatCommandProcessor Processor(AuthoritativeCombatTarget target,
        DeterministicLegacyRandom random, DiabloGameMode mode) => new(
        new DiabloWorldState(combatTargets: [target]),
        new AuthoritativeSpellCatalog([new AuthoritativeSpellDefinition(1, 1, 1, 0, 0, 0)]),
        new AuthoritativeCombatRules(10, 1, 100), random, gameMode: mode);

    private static AuthoritativePlayerState Player()
    {
        var player = DiabloPlayerTemplate.CreateDefault(100, 50, 0, 0).CreatePlayer();
        player.EntityId = 1; player.PositionX = 1; player.PositionY = 1;
        return player;
    }
}

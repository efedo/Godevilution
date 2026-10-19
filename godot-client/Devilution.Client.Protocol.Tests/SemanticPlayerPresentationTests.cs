using System.Text.Json;
using Devilution.Client.PresentationAssets;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

/** Exercises client art selection without resource loading or gameplay mutation. */
public sealed class SemanticPlayerPresentationTests
{
    [Theory]
    [InlineData("idle", PlayerActionKind.Unspecified)]
    [InlineData("move", PlayerActionKind.Move)]
    [InlineData("attack", PlayerActionKind.Attack)]
    [InlineData("cast", PlayerActionKind.Cast)]
    [InlineData("death", PlayerActionKind.Death)]
    [InlineData("level-transition", PlayerActionKind.LevelTransition)]
    [InlineData("level_transition", PlayerActionKind.LevelTransition)]
    [InlineData("LevelTransition", PlayerActionKind.LevelTransition)]
    [InlineData("restart-town", PlayerActionKind.RestartTown)]
    [InlineData("restart_town", PlayerActionKind.RestartTown)]
    [InlineData("RestartTown", PlayerActionKind.RestartTown)]
    public void EverySupportedActionSelectsWithoutChangingTheServerSnapshot(string action, PlayerActionKind kind)
    {
        var catalog = Parse($$"""[{"id":"selected","assetId":"texture","action":"{{action}}"}]""");
        var player = NewPlayer();
        player.CurrentAction = new PlayerActionSnapshot { Kind = kind, StartedAtTick = 12, EffectTick = 14, ReadyTick = 18 };
        var before = player.Clone();

        Assert.True(SemanticPlayerPresentation.TrySelect(catalog, player, out var selection));
        Assert.Equal(new PresentationActorSelection("selected", "texture", 1), selection);
        Assert.Equal(before, player);
    }

    [Fact]
    public void MissingAndUnspecifiedActionsBothSelectIdle()
    {
        var catalog = Parse("""[{"id":"idle","assetId":"texture","action":"idle"}]""");
        var player = NewPlayer();
        Assert.Null(player.CurrentAction);
        Assert.True(SemanticPlayerPresentation.TrySelect(catalog, player, out var missing));
        player.CurrentAction = new PlayerActionSnapshot { Kind = PlayerActionKind.Unspecified };
        Assert.True(SemanticPlayerPresentation.TrySelect(catalog, player, out var unspecified));
        Assert.Equal(missing, unspecified);
    }

    public static IEnumerable<object[]> CombatModes()
        => Enum.GetValues<PlayerCombatMode>().Select(mode => new object[] { mode });

    [Theory]
    [MemberData(nameof(CombatModes))]
    public void EveryDefinedCombatModeIsASeparateTypedFilter(PlayerCombatMode mode)
    {
        var catalog = Parse($$"""[{"id":"selected","assetId":"texture","combatMode":"{{mode}}"}]""");
        var player = NewPlayer();
        player.CombatMode = mode;
        Assert.True(SemanticPlayerPresentation.TrySelect(catalog, player, out var selection));
        Assert.Equal("selected", selection.RuleId);
        player.CombatMode = mode == PlayerCombatMode.Stand ? PlayerCombatMode.Attack : PlayerCombatMode.Stand;
        Assert.False(SemanticPlayerPresentation.TrySelect(catalog, player, out _));
    }

    [Fact]
    public void EverySpecifiedFilterMustMatch()
    {
        var catalog = Parse("""[{"id":"exact","assetId":"texture","classId":1,"action":"cast","combatMode":"spell","facing":"north-east"}]""");
        var player = NewPlayer();
        player.CurrentAction = new PlayerActionSnapshot { Kind = PlayerActionKind.Cast };
        player.CombatMode = PlayerCombatMode.Spell;
        player.FacingDirection = PlayerDirection.NorthEast;
        Assert.True(SemanticPlayerPresentation.TrySelect(catalog, player, out var selected));
        Assert.Equal(4, selected.Specificity);
        foreach (var change in new Action<PlayerSnapshot>[] {
                     candidate => candidate.CharacterClassId = 2,
                     candidate => candidate.CurrentAction!.Kind = PlayerActionKind.Attack,
                     candidate => candidate.CombatMode = PlayerCombatMode.Stand,
                     candidate => candidate.FacingDirection = PlayerDirection.North,
                 }) {
            var candidate = player.Clone();
            change(candidate);
            Assert.False(SemanticPlayerPresentation.TrySelect(catalog, candidate, out _));
        }
    }

    [Theory]
    [InlineData("action")]
    [InlineData("combat")]
    [InlineData("facing")]
    public void UnknownEnumsRefuseEvenAClassOnlyRule(string field)
    {
        var catalog = Parse("""[{"id":"class","assetId":"texture","classId":1}]""");
        var player = NewPlayer();
        if (field == "action")
            player.CurrentAction = new PlayerActionSnapshot { Kind = (PlayerActionKind)99 };
        else if (field == "combat")
            player.CombatMode = (PlayerCombatMode)99;
        else
            player.FacingDirection = (PlayerDirection)99;
        var before = player.Clone();
        Assert.False(SemanticPlayerPresentation.TrySelect(catalog, player, out _));
        Assert.Equal(before, player);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"id\":\"bad\",\"assetId\":\"texture\"}")]
    [InlineData("{\"id\":\"bad\",\"assetId\":\"missing\",\"classId\":1}")]
    [InlineData("{\"id\":\"bad\",\"assetId\":\"audio\",\"classId\":1}")]
    [InlineData("{\"id\":\"bad\",\"assetId\":\"text\",\"classId\":1}")]
    [InlineData("{\"id\":\"bad\",\"assetId\":\"texture\",\"classId\":-1}")]
    [InlineData("{\"id\":\"bad\",\"assetId\":\"texture\",\"classId\":4294967296}")]
    [InlineData("{\"id\":\"bad\",\"assetId\":\"texture\",\"classId\":1.5}")]
    [InlineData("{\"id\":\"bad\",\"assetId\":\"texture\",\"classId\":\"1\"}")]
    [InlineData("{\"id\":\"bad\",\"assetId\":\"texture\",\"action\":\"unknown\"}")]
    [InlineData("{\"id\":\"bad\",\"assetId\":\"texture\",\"action\":\"0\"}")]
    [InlineData("{\"id\":\"bad\",\"assetId\":\"texture\",\"classId\":1,\"action\":\" \"}")]
    [InlineData("{\"id\":\"bad\",\"assetId\":\"texture\",\"classId\":1,\"combatMode\":\"\"}")]
    [InlineData("{\"id\":\"bad\",\"assetId\":\"texture\",\"classId\":1,\"facing\":\" \"}")]
    [InlineData("{\"id\":\"bad\",\"assetId\":\"texture\",\"combatMode\":\"unknown\"}")]
    [InlineData("{\"id\":\"bad\",\"assetId\":\"texture\",\"combatMode\":\"0\"}")]
    [InlineData("{\"id\":\"bad\",\"assetId\":\"texture\",\"facing\":\"unknown\"}")]
    [InlineData("{\"id\":\"bad\",\"assetId\":\"texture\",\"facing\":\"0\"}")]
    [InlineData("{\"id\":\"bad\",\"assetId\":\"texture\",\"classId\":1,\"damage\":50}")]
    [InlineData("{\"id\":\"bad\",\"assetId\":\"texture\",\"action\":\"idle\",\"Action\":\"move\"}")]
    public void InvalidActorRulesRefuseTheWholeCatalog(string rule)
    {
        Assert.False(PresentationAssetCatalog.TryParse(Manifest("[" + rule + "]"), out var catalog, out var diagnostics));
        Assert.Null(catalog);
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void DuplicateRuleIdentitiesRefuseEvenDisjointSelectors()
    {
        var rules = """[{"id":"same","assetId":"texture","classId":1},{"id":"same","assetId":"texture","classId":2}]""";
        Assert.False(PresentationAssetCatalog.TryParse(Manifest(rules), out var catalog, out var diagnostics));
        Assert.Null(catalog);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Contains("more than once", StringComparison.Ordinal));
    }

    [Fact]
    public void MaximumRuleCountIsAcceptedAndCatalogCannotBeMutated()
    {
        var rules = Enumerable.Range(0, PresentationAssetCatalog.MaximumActorRuleCount)
            .Select(index => new { id = "rule-" + index, assetId = "texture", classId = (uint)index });
        var catalog = Parse(JsonSerializer.Serialize(rules));
        Assert.Equal(PresentationAssetCatalog.MaximumActorRuleCount, catalog.ActorRules.Count);
        var mutableView = Assert.IsAssignableFrom<IList<PresentationActorRule>>(catalog.ActorRules);
        Assert.Throws<NotSupportedException>(() => mutableView.Clear());
        var player = NewPlayer();
        player.CharacterClassId = (uint)(PresentationAssetCatalog.MaximumActorRuleCount - 1);
        Assert.True(SemanticPlayerPresentation.TrySelect(catalog, player, out var selected));
        Assert.Equal("rule-" + player.CharacterClassId, selected.RuleId);
    }

    [Fact]
    public void ExcessRuleCountRefusesBeforeQuadraticAmbiguityDiagnostics()
    {
        var rules = Enumerable.Range(0, PresentationAssetCatalog.MaximumActorRuleCount + 1)
            .Select(index => new { id = "rule-" + index, assetId = "texture", classId = 1 });
        Assert.False(PresentationAssetCatalog.TryParse(Manifest(JsonSerializer.Serialize(rules)), out var catalog, out var diagnostics));
        Assert.Null(catalog);
        Assert.Single(diagnostics);
        Assert.Contains("more than", diagnostics[0], StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Contains("ambiguous", StringComparison.Ordinal));
    }

    [Fact]
    public void NullArgumentsAreRejectedWithoutConstructingASelection()
    {
        var catalog = Parse("[]");
        Assert.Throws<ArgumentNullException>(() => SemanticPlayerPresentation.TrySelect(null!, NewPlayer(), out _));
        Assert.Throws<ArgumentNullException>(() => SemanticPlayerPresentation.TrySelect(catalog, null!, out _));
    }

    private static PlayerSnapshot NewPlayer()
        => new() { EntityId = 42, CharacterClassId = 1, CombatMode = PlayerCombatMode.Stand, FacingDirection = PlayerDirection.South };

    private static PresentationAssetCatalog Parse(string rules)
    {
        Assert.True(PresentationAssetCatalog.TryParse(Manifest(rules), out var catalog, out var diagnostics), string.Join(" ", diagnostics));
        return catalog!;
    }

    private static string Manifest(string rules)
        => $$"""
        { "schemaVersion":1, "packId":"semantic-tests",
          "assets":[{"id":"texture","kind":"texture","path":"player.svg"},
                    {"id":"audio","kind":"audio","path":"feedback.wav"},
                    {"id":"text","kind":"localization","text":"Caption"}],
          "actors":{{rules}} }
        """;
}

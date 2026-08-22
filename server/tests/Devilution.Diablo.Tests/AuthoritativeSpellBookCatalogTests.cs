using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeSpellBookCatalogTests
{
    [Fact]
    public void RandomBookSelectionWalksNativeSpellIdsInsteadOfStableIds()
    {
        var items = CreateBookTemplateCatalog();
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(100, 1, 0, 0, 0, 0) { NativeSpellId = 3 },
            new AuthoritativeSpellDefinition(200, 1, 0, 0, 0, 0) { NativeSpellId = 1 },
        ]);
        var books = new AuthoritativeSpellBookCatalog([
            new AuthoritativeSpellBookDefinition(
                100,
                "Stable Hundred",
                1,
                10,
                AuthoritativeSpellBookMagicType.Fire,
                1),
            new AuthoritativeSpellBookDefinition(
                200,
                "Stable Two Hundred",
                1,
                20,
                AuthoritativeSpellBookMagicType.Magic,
                1),
        ]);

        books.Validate(spells, items);

        // Native GetBookSpell rolls over a table of size four here. Native ID
        // 1 resolves to stable 200 and native ID 3 resolves to stable 100.
        for (uint seed = 1; seed <= 128; seed++) {
            var roll = new AuthoritativeLegacyRandom(seed).Next(4) + 1;
            var expectedSpellId = roll is 1 or 3 ? 200 : 100;
            var state = books.GenerateRandom(items, seed, itemLevel: 1);

            Assert.Equal(expectedSpellId, state.SpellId);
            Assert.Equal(DiabloItemActionKind.ReadBook, state.ItemActionKind);
        }
    }

    [Fact]
    public void SinglePlayerBookSelectionSkipsResurrectAndHealOther()
    {
        var items = CreateBookTemplateCatalog();
        var spells = new AuthoritativeSpellCatalog([
            new AuthoritativeSpellDefinition(1, 1, 0, 0, 0, 0) { NativeSpellId = 32 },
            new AuthoritativeSpellDefinition(2, 1, 0, 0, 0, 0) { NativeSpellId = 34 },
            new AuthoritativeSpellDefinition(3, 1, 0, 0, 0, 0) { NativeSpellId = 35 },
        ]);
        var books = new AuthoritativeSpellBookCatalog([
            new AuthoritativeSpellBookDefinition(
                1,
                "Resurrect",
                1,
                10,
                AuthoritativeSpellBookMagicType.Magic,
                1),
            new AuthoritativeSpellBookDefinition(
                2,
                "Heal Other",
                1,
                10,
                AuthoritativeSpellBookMagicType.Magic,
                1),
            new AuthoritativeSpellBookDefinition(
                3,
                "Blood Star",
                1,
                10,
                AuthoritativeSpellBookMagicType.Magic,
                1),
        ]);

        books.Validate(spells, items);

        for (uint seed = 1; seed <= 128; seed++) {
            var singlePlayer = books.GenerateRandom(
                items,
                seed,
                itemLevel: 1,
                gameMode: DiabloGameMode.SinglePlayer);
            Assert.Equal(3, singlePlayer.SpellId);
        }

        var multiplayerSpellIds = Enumerable.Range(1, 128)
            .Select(seed => books.GenerateRandom(
                items,
                (uint)seed,
                itemLevel: 1,
                gameMode: DiabloGameMode.Multiplayer).SpellId)
            .ToHashSet();
        Assert.Contains(1, multiplayerSpellIds);
        Assert.Contains(2, multiplayerSpellIds);
        Assert.Contains(3, multiplayerSpellIds);
    }

    [Fact]
    public void SinglePlayerBookSelectionUsesSemanticSpellBehaviorForRemappedIds()
    {
        var items = CreateBookTemplateCatalog();
        var spells = new AuthoritativeSpellCatalog([
            CreateResurrectionSpell() with { SpellId = 700 },
            CreateHealOtherSpell() with { SpellId = 800 },
            new AuthoritativeSpellDefinition(900, 1, 0, 0, 0, 0),
        ]);
        var books = new AuthoritativeSpellBookCatalog([
            new AuthoritativeSpellBookDefinition(
                700,
                "Resurrect",
                1,
                10,
                AuthoritativeSpellBookMagicType.Magic,
                1),
            new AuthoritativeSpellBookDefinition(
                800,
                "Heal Other",
                1,
                10,
                AuthoritativeSpellBookMagicType.Magic,
                1),
            new AuthoritativeSpellBookDefinition(
                900,
                "Blood Star",
                1,
                10,
                AuthoritativeSpellBookMagicType.Magic,
                1),
        ]);

        books.Validate(spells, items);

        for (uint seed = 1; seed <= 128; seed++) {
            var singlePlayer = books.GenerateRandom(
                items,
                seed,
                itemLevel: 1,
                gameMode: DiabloGameMode.SinglePlayer);
            Assert.Equal(900, singlePlayer.SpellId);
        }

        var multiplayerSpellIds = Enumerable.Range(1, 128)
            .Select(seed => books.GenerateRandom(
                items,
                (uint)seed,
                itemLevel: 1,
                gameMode: DiabloGameMode.Multiplayer).SpellId)
            .ToHashSet();
        Assert.Contains(700, multiplayerSpellIds);
        Assert.Contains(800, multiplayerSpellIds);
        Assert.Contains(900, multiplayerSpellIds);
    }

    [Fact]
    public void DropGenerationRetainsTheRequestedBookTemplate()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(114, 6, 75, 75, 0, 0, 0, 0, 0, 1, 1, true) {
                MiscId = 25,
                MinimumMonsterLevel = 2,
                GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
            },
            new AuthoritativeItemDefinition(115, 6, 80, 80, 0, 0, 0, 0, 0, 1, 1, true) {
                MiscId = 25,
                MinimumMonsterLevel = 8,
                GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
            },
        ]);
        var books = new AuthoritativeSpellBookCatalog([
            new AuthoritativeSpellBookDefinition(
                7,
                "Firebolt",
                1,
                25,
                AuthoritativeSpellBookMagicType.Fire,
                15),
        ]);

        var state = items.GenerateDrop(115, 42, 4, spellBooks: books);

        Assert.Equal(115, state.ItemIndex);
        Assert.Equal(105, state.Value);
        Assert.Equal(7, state.SpellId);
    }

    [Fact]
    public void UsesContentBookTemplateThresholdsBeforeLegacyFallback()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(114, 6, 75, 75, 0, 0, 0, 0, 0, 1, 1, true) {
                MiscId = 25,
                MinimumMonsterLevel = 99,
                BookTemplateMinimumItemLevel = 1,
                GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
            },
            new AuthoritativeItemDefinition(115, 6, 80, 80, 0, 0, 0, 0, 0, 1, 1, true) {
                MiscId = 25,
                MinimumMonsterLevel = 99,
                BookTemplateMinimumItemLevel = 4,
                GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
            },
        ]);
        var books = new AuthoritativeSpellBookCatalog([
            new AuthoritativeSpellBookDefinition(
                7,
                "Firebolt",
                1,
                25,
                AuthoritativeSpellBookMagicType.Fire,
                15),
        ]);
        books.Validate(
            new AuthoritativeSpellCatalog([
                new AuthoritativeSpellDefinition(7, 1, 0, 0, 0, 0),
            ]),
            items);

        for (uint seed = 1; seed <= 32; seed++)
            Assert.Equal(114U, books.GenerateRandom(items, seed, itemLevel: 2).StableItemId);

        var higherLevelTemplates = Enumerable.Range(1, 128)
            .Select(seed => books.GenerateRandom(items, (uint)seed, itemLevel: 4).StableItemId)
            .ToHashSet();
        Assert.Contains(114U, higherLevelTemplates);
        Assert.Contains(115U, higherLevelTemplates);
    }

    private static AuthoritativeItemCatalog CreateBookTemplateCatalog()
    {
        return new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(114, 6, 75, 75, 0, 0, 0, 0, 0, 1, 1, true) {
                MiscId = 25,
                GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
            },
        ]);
    }

    private static AuthoritativeSpellDefinition CreateHealOtherSpell()
    {
        return new AuthoritativeSpellDefinition(34, 5, 1, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileLifetime = 1,
            ProjectileBehavior = AuthoritativeProjectileBehavior.HealOther,
            ManaAdjustment = 3,
            CharacterLevelManaCost = 2,
            MinimumManaCost = 1,
            ManaCostScale = 64,
            RandomHealingRange = 10,
            CharacterLevelRandomHealingRange = 4,
            CharacterLevelHealing = 1,
            SpellLevelRandomHealingRange = 6,
            SpellLevelHealing = 1,
            HealingScale = 64,
        };
    }

    private static AuthoritativeSpellDefinition CreateResurrectionSpell()
    {
        return new AuthoritativeSpellDefinition(32, 20, 0, 0, 0, 0) {
            Range = 112,
            DamageType = AuthoritativeDamageType.Magic,
            ProjectileLifetime = 16,
            ProjectileBehavior = AuthoritativeProjectileBehavior.Resurrection,
            ManaAdjustment = 2,
            MinimumManaCost = 20,
            ManaCostScale = 64,
            ResurrectionLife = 10,
            ResurrectionLifeScale = 64,
            ResurrectionSearchRadius = 50,
        };
    }
}

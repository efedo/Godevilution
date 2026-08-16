using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeSpellBookCatalogTests
{
    [Fact]
    public void RandomBookSelectionWalksNativeSpellIdsInsteadOfStableIds()
    {
        var items = new AuthoritativeItemCatalog([
            new AuthoritativeItemDefinition(114, 6, 75, 75, 0, 0, 0, 0, 0, 1, 1, true) {
                MiscId = 25,
                GenerationTags = new HashSet<string>(["Book"], StringComparer.OrdinalIgnoreCase),
            },
        ]);
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
}

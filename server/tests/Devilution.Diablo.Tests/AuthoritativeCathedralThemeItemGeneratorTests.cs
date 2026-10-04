using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Source-grounded synthetic regressions for detached ordinary Cathedral item generation. */
public sealed class AuthoritativeCathedralThemeItemGeneratorTests
{
    private static readonly AuthoritativeCathedralThemeItemPolicy Policy =
        new(1);

    [Fact]
    public void SetupAllItemsPreservesUntouchedStateWhenResetIsDisabled()
    {
        var input = new AuthoritativeCathedralInitialItemState {
            PositionX = 17,
            PositionY = 19,
            PlusDamage = 73,
            Buff = 0xA5000000,
            Animation = new AuthoritativeCathedralInitialItemAnimation {
                TicksPerFrame = 7,
                NumberOfFrames = 8,
                CurrentFrame = 4,
            },
        };
        var request = new AuthoritativeCathedralThemeItemRequest(
            0,
            1,
            2,
            0,
            false,
            false,
            InputItem: input,
            ResetBeforeGetItemAttrs: false);

        var result = AuthoritativeCathedralThemeItemGenerator.SetupAllItems(request, Catalog(), Policy);

        Assert.Equal(input.PositionX, result.Item.PositionX);
        Assert.Equal(input.PositionY, result.Item.PositionY);
        Assert.Equal(input.Buff, result.Item.Buff);
        Assert.Equal(input.Animation, result.Item.Animation);
        Assert.Equal(input.PlusDamage, result.Item.PlusDamage);
    }

    [Fact]
    public void SetupAllItemsExplicitResetZerosUntouchedStateAndConsumesNativeZeroWidthRolls()
    {
        var input = new AuthoritativeCathedralInitialItemState {
            PositionX = 17,
            PositionY = 19,
            PlusDamage = 73,
            Buff = 0xA5000000,
        };
        var request = new AuthoritativeCathedralThemeItemRequest(
            0x80000000,
            4,
            2,
            0,
            false,
            false,
            InputItem: input,
            ResetBeforeGetItemAttrs: true);

        var catalog = Catalog(new AuthoritativeItemGenerationRules(0, 0, 1, 15, 4, 25, 66, 66, 59, 74));
        var result = AuthoritativeCathedralThemeItemGenerator.SetupAllItems(request, catalog, Policy);
        var expected = new DeterministicLegacyRandom(request.ItemSeed);
        _ = expected.GenerateRnd(1); // GetItemAttrs equal-range armor roll.
        _ = expected.GenerateRnd(100); // GetItemBLevel base quality roll.
        _ = expected.GenerateRnd(100); // Native short-circuit reaches the level quality roll.

        Assert.Equal(0, result.Item.PositionX);
        Assert.Equal(0, result.Item.PositionY);
        Assert.Equal(0u, result.Item.Buff);
        Assert.Equal(0, result.Item.PlusDamage);
        Assert.Equal(expected.State, result.FinalRandomState);
    }

    [Fact]
    public void MagicPowerUsesNativeAllocationOrderAndEqualRangePowerConsumesItsNativeRoll()
    {
        var catalog = Catalog(new AuthoritativeItemGenerationRules(100, 0, 1, 15, 4, 100, 100, 0, 59, 0));
        var request = new AuthoritativeCathedralThemeItemRequest(1234, 1, 2, 0, false, false);

        var result = AuthoritativeCathedralThemeItemGenerator.GenerateSetupAndUniqueContinuation(request, catalog, Policy);

        Assert.Equal(1, result.Item.Magical);
        Assert.Equal(7, result.Item.PlusToHit);
        Assert.NotEqual(-1, result.Item.PrefixPower);
        Assert.NotEqual(-1, result.Item.SuffixPower);
    }

    [Fact]
    public void UniqueContinuationUsesImmutableFlagsAndMarksGeneratedUnique()
    {
        var catalog = Catalog(new AuthoritativeItemGenerationRules(100, 0, 100, 15, 4, 0, 0, 0, 59, 0));
        var flags = new HashSet<int>();
        var request = new AuthoritativeCathedralThemeItemRequest(
            2588,
            1,
            2,
            100,
            false,
            false,
            UniqueItemFlags: flags);

        var result = AuthoritativeCathedralThemeItemGenerator.GenerateSetupAndUniqueContinuation(request, catalog, Policy);

        Assert.Equal(2, result.Item.Magical);
        Assert.Equal(0, result.Item.UniqueId);
        Assert.Contains(0, result.UniqueItemFlags);
        Assert.Empty(flags);
    }

    [Fact]
    public void GoldGenerationUsesCurrentCathedralLevelAndNativeCursorThresholds()
    {
        var result = AuthoritativeCathedralThemeItemGenerator.CreateTypeItem(
            new AuthoritativeCathedralThemeItemSelectionRequest(91, 1, false, 11),
            Catalog(),
            Policy);

        Assert.Equal(11, result.Item.ItemType);
        Assert.InRange(result.Item.Value, 5, 15);
        Assert.Contains(result.Item.CursorGraphic, new[] { 4, 5, 6 });
    }

    [Fact]
    public void BookAndStaffGenerationUseExternallySuppliedSpellRows()
    {
        var catalog = Catalog(new AuthoritativeItemGenerationRules(100, 0, 1, 15, 4, 0, 0, 0, 59, 0));
        var book = AuthoritativeCathedralThemeItemGenerator.GenerateSetupAndUniqueContinuation(
            new AuthoritativeCathedralThemeItemRequest(7, 2, 2, 0, true, false), catalog, Policy);
        var staff = Enumerable.Range(1, 64)
            .Select(seed => AuthoritativeCathedralThemeItemGenerator.GenerateSetupAndUniqueContinuation(
                new AuthoritativeCathedralThemeItemRequest((uint)seed, 3, 2, 0, true, false), catalog, Policy))
            .FirstOrDefault(result => result.Item.SpellId != 0);

        Assert.Equal(24, book.Item.MiscId);
        Assert.Equal(55, book.Item.SpellId);
        Assert.EndsWith("Spell55", book.Item.BaseName);
        Assert.NotNull(staff);
        Assert.Equal(23, staff!.Item.MiscId);
        Assert.NotEqual(0, staff.Item.SpellId);
        Assert.True(staff.Item.Charges > 0);
    }

    [Fact]
    public void NativeMiscIdsDistinguishBookRingAndUniqueRows()
    {
        var uniqueRow = Item(6, 1, 1, 27, "Unique base", 10) with {
            DropRate = 0,
            EquipLocation = 1,
        };
        var catalog = Catalog(items: Catalog().Items.Append(uniqueRow).ToArray());

        var book = AuthoritativeCathedralThemeItemGenerator.SetupAllItems(
            new AuthoritativeCathedralThemeItemRequest(7, 2, 2, 0, true, false), catalog, Policy);
        var ring = AuthoritativeCathedralThemeItemGenerator.SetupAllItems(
            new AuthoritativeCathedralThemeItemRequest(7, 5, 2, 0, false, false), catalog, Policy);
        var unique = AuthoritativeCathedralThemeItemGenerator.GenerateSetupAndUniqueContinuation(
            new AuthoritativeCathedralThemeItemRequest(0, 6, 2, 0, false, false), catalog, Policy);

        Assert.Equal(24, book.Item.MiscId);
        Assert.Equal(25, ring.Item.MiscId);
        Assert.Equal(27, unique.Item.MiscId);
        Assert.Equal(0, unique.Item.UniqueId);
    }

    [Fact]
    public void NativeNonPositiveRandomBoundsDoNotConsumeTheItemStream()
    {
        var catalog = Catalog(
            new AuthoritativeItemGenerationRules(100, 0, 1, 15, 4, 100, 0, 0, 59, 0),
            prefixes: [Affix("Dim", 38, 2, 0, 1, 1, true)]);
        var request = new AuthoritativeCathedralThemeItemRequest(1234, 1, 2, 0, true, false);
        var result = AuthoritativeCathedralThemeItemGenerator.GenerateSetupAndUniqueContinuation(request, catalog, Policy);
        var expected = new DeterministicLegacyRandom(request.ItemSeed);

        _ = expected.GenerateRnd(1); // GetItemAttrs equal-range armor.
        _ = expected.GenerateRnd(100); // GetItemBLevel base quality.
        _ = expected.GenerateRnd(100); // CheckUnique chance, even when it fails.
        _ = expected.GenerateRnd(100); // GetItemPower prefix allocation.
        _ = expected.GenerateRnd(100); // GetItemPower suffix allocation.
        _ = expected.GenerateRnd(1); // SelectAffix's one weighted row.
        _ = expected.GenerateRnd(10); // ItemRndDur.

        Assert.Equal(2, result.Item.PlusLight);
        Assert.Equal(expected.State, result.FinalRandomState);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(89)]
    public void UnknownNativeToHitBonusLevelsFailClosed(int powerType)
    {
        var catalog = Catalog(
            new AuthoritativeItemGenerationRules(100, 0, 1, 15, 4, 100, 0, 0, 59, 0),
            prefixes: [Affix("Invalid", powerType, 0, 1, 1, 1, true)]);
        var request = new AuthoritativeCathedralThemeItemRequest(1234, 1, 2, 0, true, false);

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralThemeItemGenerator.GenerateSetupAndUniqueContinuation(request, catalog, Policy));
    }

    [Fact]
    public void LiteralReplacementRuneStopsNativePanelWidthAndPreservesLongStaffName()
    {
        var longName = new string('A', 20) + '\uFFFD' + new string('B', 20);
        var items = Catalog().Items
            .Select(item => item.NativeItemIndex == 3 ? item with { Name = longName, ShortName = "Staff" } : item)
            .ToArray();
        var catalog = Catalog(
            new AuthoritativeItemGenerationRules(100, 0, 1, 15, 4, 0, 0, 0, 59, 0),
            prefixes: Array.Empty<AuthoritativeCathedralNativeAffixDefinition>(),
            suffixes: Array.Empty<AuthoritativeCathedralNativeAffixDefinition>(),
            items: items);
        var staff = Enumerable.Range(1, 64)
            .Select(seed => AuthoritativeCathedralThemeItemGenerator.GenerateSetupAndUniqueContinuation(
                new AuthoritativeCathedralThemeItemRequest((uint)seed, 3, 2, 0, false, false), catalog, Policy))
            .First(result => result.Item.SpellId != 0);

        Assert.Contains('\uFFFD', staff.Item.BaseName);
        Assert.DoesNotContain("Staff of", staff.Item.BaseName);

        var fallbackItems = items
            .Select(item => item.NativeItemIndex == 3
                ? item with { Name = new string('A', 40) + '\uFFFD', ShortName = "Staff" }
                : item)
            .ToArray();
        var fallbackCatalog = Catalog(
            new AuthoritativeItemGenerationRules(100, 0, 1, 15, 4, 0, 0, 0, 59, 0),
            prefixes: Array.Empty<AuthoritativeCathedralNativeAffixDefinition>(),
            suffixes: Array.Empty<AuthoritativeCathedralNativeAffixDefinition>(),
            items: fallbackItems);
        var fallback = Enumerable.Range(1, 64)
            .Select(seed => AuthoritativeCathedralThemeItemGenerator.GenerateSetupAndUniqueContinuation(
                new AuthoritativeCathedralThemeItemRequest((uint)seed, 3, 2, 0, false, false), fallbackCatalog, Policy))
            .First(result => result.Item.SpellId != 0);

        Assert.StartsWith("Staff of", fallback.Item.BaseName);
    }

    [Fact]
    public void ExhaustedUniqueHistoryUsesNormalChanceForAnUnrelatedChanceInput()
    {
        var catalog = Catalog(new AuthoritativeItemGenerationRules(100, 0, 0, 2, 4, 0, 0, 0, 59, 0));
        var flags = new HashSet<int> { 0 };
        var request = new AuthoritativeCathedralThemeItemRequest(
            2588,
            1,
            2,
            100,
            false,
            false,
            UniqueItemFlags: flags);

        var result = AuthoritativeCathedralThemeItemGenerator.GenerateSetupAndUniqueContinuation(request, catalog, Policy);

        Assert.NotEqual(2, result.Item.Magical);
        Assert.Contains(0, result.UniqueItemFlags);
        Assert.Contains(0, flags);
    }

    [Fact]
    public void UnsupportedPolicyFailsClosedBeforeRandomMutation()
    {
        var random = new AuthoritativeCathedralThemeItemPolicy(1, DiabloGameMode.Multiplayer);
        var request = new AuthoritativeCathedralThemeItemRequest(1, 1, 2, 0, false, false);

        Assert.Throws<InvalidDataException>(() =>
            AuthoritativeCathedralThemeItemGenerator.SetupAllItems(request, Catalog(), random));
    }

    [Fact]
    public void CatalogRejectsNonContiguousNativeSourceOrdinals()
    {
        var items = Catalog().Items
            .Select(item => item.NativeItemIndex == 1 ? item with { NativeItemIndex = 7 } : item)
            .ToArray();

        Assert.Throws<InvalidDataException>(() => new AuthoritativeCathedralThemeItemCatalog(
            items,
            Catalog().Prefixes,
            Catalog().Suffixes,
            Catalog().Uniques,
            Catalog().Spells,
            Catalog().CursorAnimations,
            Catalog().GlyphWidths,
            Catalog().Cursors,
            replacementGlyphWidth: 8));
    }

    [Fact]
    public void CatalogRetainsCursor255SentinelButOrdinaryGenerationRejectsIt()
    {
        var sentinel = Item(6, 6, 0, 0, "Sentinel", 0) with {
            DropRate = 0,
            CursorGraphic = 255,
            AnimationFrameCount = 0,
        };
        var catalog = Catalog(items: Catalog().Items.Append(sentinel).ToArray());

        Assert.Contains(catalog.Items, item => item.NativeItemIndex == 6 && item.CursorGraphic == 255
            && item.AnimationFrameCount == 0 && item.Available);
        Assert.Throws<InvalidDataException>(() => AuthoritativeCathedralThemeItemGenerator.SetupAllItems(
            new AuthoritativeCathedralThemeItemRequest(1, 6, 2, 0, false, false), catalog, Policy));

        var positiveDropRate = Item(6, 6, 0, 0, "Sentinel", 0) with {
            DropRate = 1,
            CursorGraphic = 255,
            AnimationFrameCount = 0,
        };
        Assert.Throws<InvalidDataException>(() => Catalog(items: Catalog().Items.Append(positiveDropRate).ToArray()));

        var nonzeroFrame = Item(6, 6, 0, 0, "Sentinel", 0) with {
            DropRate = 0,
            CursorGraphic = 255,
            AnimationFrameCount = 1,
        };
        Assert.Throws<InvalidDataException>(() => Catalog(items: Catalog().Items.Append(nonzeroFrame).ToArray()));
    }

    [Fact]
    public void RejectedPublicApisDoNotMutateInputItemOrUniqueFlags()
    {
        var sentinel = Item(6, 6, 0, 0, "Sentinel", 0) with {
            DropRate = 0,
            CursorGraphic = 255,
            AnimationFrameCount = 0,
        };
        var catalog = Catalog(items: Catalog().Items.Append(sentinel).ToArray());
        var input = new AuthoritativeCathedralInitialItemState {
            PositionX = 31,
            PositionY = 37,
            PlusDamage = 41,
        };
        var originalInput = input;
        var flags = new HashSet<int> { 0 };
        var generationRequest = new AuthoritativeCathedralThemeItemRequest(
            1, 6, 2, 0, false, false, InputItem: input, UniqueItemFlags: flags);

        void AssertRejected(Action action)
        {
            Assert.Throws<InvalidDataException>(action);
            Assert.Equal(originalInput, input);
            Assert.Single(flags);
            Assert.Contains(0, flags);
        }

        AssertRejected(() => AuthoritativeCathedralThemeItemGenerator.Generate(generationRequest, catalog, Policy));
        AssertRejected(() => AuthoritativeCathedralThemeItemGenerator.GenerateSetupAndUniqueContinuation(
            generationRequest, catalog, Policy));
        AssertRejected(() => AuthoritativeCathedralThemeItemGenerator.SetupAllItems(generationRequest, catalog, Policy));

        var selectionRequest = new AuthoritativeCathedralThemeItemSelectionRequest(
            1, 1, false, InputItem: input, UniqueItemFlags: flags);
        var unsupportedPolicy = new AuthoritativeCathedralThemeItemPolicy(1, DiabloGameMode.Multiplayer);
        AssertRejected(() => AuthoritativeCathedralThemeItemGenerator.CreateRandomItem(
            selectionRequest, catalog, unsupportedPolicy));
        AssertRejected(() => AuthoritativeCathedralThemeItemGenerator.CreateTypeItem(
            selectionRequest, catalog, unsupportedPolicy));
    }

    [Fact]
    public void CatalogSnapshotsRulesNumericSetsAndUniquePowers()
    {
        var stringSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "book" };
        var numericSet = new HashSet<int> { 24 };
        var powers = new List<AuthoritativeCathedralNativeItemPower> {
            new(2, 3, 3),
        };
        var unique = new AuthoritativeCathedralNativeUniqueDefinition(0, "Snapshot", 255, 1, 0, 999, powers);
        var rules = new AuthoritativeItemGenerationRules(10, 1, 1, 15, 4, 25, 66, 66, 59, 74) {
            AlwaysMagicMisc = stringSet,
        };
        var catalog = Catalog(rules: rules, uniques: [unique], alwaysMagicMiscIds: numericSet);

        stringSet.Add("mutated");
        numericSet.Add(25);
        powers.Clear();

        Assert.Contains("book", catalog.Rules.AlwaysMagicMisc);
        Assert.DoesNotContain("mutated", catalog.Rules.AlwaysMagicMisc);
        Assert.Contains(24, catalog.AlwaysMagicMiscIds);
        Assert.DoesNotContain(25, catalog.AlwaysMagicMiscIds);
        Assert.Single(catalog.Uniques[0].Powers);
        Assert.Equal(2, catalog.Uniques[0].Powers[0].Type);
    }

    [Fact]
    public void CatalogRejectsNullNestedRuleSet()
    {
        var rules = new AuthoritativeItemGenerationRules(10, 1, 1, 15, 4, 25, 66, 66, 59, 74) {
            AlwaysMagicMisc = null!,
        };

        Assert.Throws<InvalidDataException>(() => Catalog(rules: rules));
    }

    [Fact]
    public void GroundGenerationUsesLevelLoadingToSelectFinalAnimationFrame()
    {
        var catalog = CatalogWithAnimations();
        var loading = AuthoritativeCathedralThemeItemGenerator.Generate(
            new AuthoritativeCathedralThemeItemRequest(3, 1, 2, 0, false, false,
                Player: new AuthoritativeCathedralThemeItemPlayerContext(true, true)),
            catalog,
            Policy);
        var active = AuthoritativeCathedralThemeItemGenerator.Generate(
            new AuthoritativeCathedralThemeItemRequest(3, 1, 2, 0, false, false,
                Player: new AuthoritativeCathedralThemeItemPlayerContext(true, false)),
            catalog,
            Policy);

        Assert.False(loading.Item.AnimFlag);
        Assert.Equal(3, loading.Item.Animation.CurrentFrame);
        Assert.True(active.Item.AnimFlag);
        Assert.Equal(0, active.Item.Animation.CurrentFrame);
    }

    private static AuthoritativeCathedralThemeItemCatalog Catalog(
        AuthoritativeItemGenerationRules? rules = null,
        IReadOnlyList<AuthoritativeCathedralNativeAffixDefinition>? prefixes = null,
        IReadOnlyList<AuthoritativeCathedralNativeAffixDefinition>? suffixes = null,
        IReadOnlyList<AuthoritativeCathedralNativeItemDefinition>? items = null,
        IReadOnlyList<AuthoritativeCathedralNativeSpellDefinition>? spells = null,
        IReadOnlyList<AuthoritativeCathedralNativeUniqueDefinition>? uniques = null,
        IReadOnlySet<int>? alwaysMagicMiscIds = null)
        => new(
            items ?? [
                Item(0, 0, 11, 4, "Gold", 0),
                Item(1, 1, 1, 10, "Sword", 10),
                Item(2, 2, 0, 24, "Book ", 10),
                Item(3, 3, 10, 23, "Staff", 30),
                Item(4, 4, 0, 0, "Plain", 1),
                Item(5, 5, 12, 25, "Ring", 10),
            ],
            prefixes ?? [
                Affix("Sharp", 0, 5, 5, 1, 1, true),
            ],
            suffixes ?? [
                Affix("Bright", 0, 2, 2, 1, 1, true),
            ],
            uniques ?? [
                new AuthoritativeCathedralNativeUniqueDefinition(
                    0, "Sevenfold Sword", 255, 1, 0, 999,
                    [new AuthoritativeCathedralNativeItemPower(2, 3, 3)]),
            ],
            spells ?? Spells(),
            new Dictionary<int, AuthoritativeCathedralNativeAnimationDefinition>(),
            GlyphWidths(),
            new AuthoritativeCathedralNativeCursorDefinition(87, 88, 86, 4, 5, 6),
            alwaysMagicMiscIds: alwaysMagicMiscIds,
            rules: rules,
            replacementGlyphWidth: 8);

    private static IReadOnlyDictionary<int, int> GlyphWidths()
        => Enumerable.Range(0, 256).ToDictionary(codepoint => codepoint, _ => 8);

    private static AuthoritativeCathedralThemeItemCatalog CatalogWithAnimations()
    {
        var catalog = Catalog();
        return new AuthoritativeCathedralThemeItemCatalog(
            catalog.Items,
            catalog.Prefixes,
            catalog.Suffixes,
            catalog.Uniques,
            catalog.Spells,
            new Dictionary<int, AuthoritativeCathedralNativeAnimationDefinition> {
                [4] = new(4), [10] = new(4), [24] = new(4), [25] = new(4),
                [86] = new(4), [87] = new(4), [88] = new(4),
            },
            catalog.GlyphWidths,
            catalog.Cursors,
            rules: catalog.Rules,
            replacementGlyphWidth: catalog.ReplacementGlyphWidth);
    }

    private static AuthoritativeCathedralNativeItemDefinition Item(
        int index,
        int nativeItemId,
        int type,
        int miscId,
        string name,
        int value)
        => new(index, nativeItemId, 1, type == 11 ? 4 : 1, 7, type == 11 ? 4 : 10,
            type, name, name, 0, type is 0 or 11 ? 0 : 20, 1, 4, 0, 0, 0, 0, 0, 0, miscId, 0, true, value, 4);

    private static AuthoritativeCathedralNativeAffixDefinition Affix(
        string name,
        int power,
        int param1,
        int param2,
        int minimumLevel,
        int type,
        bool useful)
        => new(name, new AuthoritativeCathedralNativeItemPower(power, param1, param2), minimumLevel,
            type == 1 ? 8 : 4, 0, 1, useful, 1, 1, 1);

    private static IReadOnlyList<AuthoritativeCathedralNativeSpellDefinition> Spells()
    {
        var spells = Enumerable.Range(0, 58)
            .Select(index => new AuthoritativeCathedralNativeSpellDefinition(
                index,
                index == 1 ? "Firebolt" : $"Spell{index}",
                index == 0 ? -1 : 0,
                index == 1 ? 0 : -1,
                1,
                10,
                10,
                0,
                1,
                3))
            .ToArray();
        return spells;
    }
}

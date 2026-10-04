using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Independent raw native setup/unique continuation; no expected item is a generator input. */
public sealed class AuthoritativeCathedralThemeItemNativeOracleTests
{
    private const string DirectoryVariable = "GODEVILUTION_THEME_ITEM_ORACLE_DIRECTORY";
    private const string FileName = "native-theme-items.json";
    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    [Theory(Explicit = true)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void SetupUniqueAndGroundMatchNative(int depth)
    {
        var directory = AuthoritativeCathedralEncounterNativeOracleTests.RequiredExternalDirectory(DirectoryVariable);
        var entries = AuthoritativeCathedralEncounterNativeOracleTests.RegularEntries(directory);
        Assert.Equal(new[] { FileName }, entries.Select(Path.GetFileName));
        var corpus = ReadCorpus(AuthoritativeCathedralEncounterNativeOracleTests.ParseFile(Path.Combine(directory, FileName), 32 * 1024 * 1024));
        var cases = corpus.Cases.Where(value => value.Depth == depth).ToArray();
        Assert.NotEmpty(cases);
        foreach (var value in cases) {
            var callerFlags = value.InputUniqueFlags.Order().ToArray();
            var request = new AuthoritativeCathedralThemeItemRequest(value.Seed, value.NativeItemId, 2 * depth,
                corpus.Catalog.Rules.UniqueChanceNormal, value.OnlyGood, true,
                InputItem: value.InputItem, ResetBeforeGetItemAttrs: false,
                Player: new(MaximumManaBase: corpus.PlayerMaximumMana, MaximumLifeBase: corpus.PlayerMaximumLife),
                UniqueItemFlags: value.InputUniqueFlags);
            var policy = new AuthoritativeCathedralThemeItemPolicy(depth);
            var setup = AuthoritativeCathedralThemeItemGenerator.SetupAllItems(request, corpus.Catalog, policy);
            EqualItem(value, "SetupAllItems", value.SetupItem, setup.Item);
            Assert.Equal(value.SetupRandomState, setup.FinalRandomState);
            Assert.Equal(value.InputUniqueFlags.Order(), setup.UniqueItemFlags.Order());
            var continuation = AuthoritativeCathedralThemeItemGenerator.GenerateSetupAndUniqueContinuation(request, corpus.Catalog, policy);
            EqualItem(value, "TryRandomUniqueItem", value.ExpectedItem, continuation.Item);
            Assert.Equal(value.ExpectedRandomState, continuation.FinalRandomState);
            Assert.Equal(value.ExpectedUniqueFlags.Order(), continuation.UniqueItemFlags.Order());
            var ground = AuthoritativeCathedralThemeItemGenerator.Generate(request, corpus.Catalog, policy);
            EqualItem(value, "SetupItem", value.GroundItem, ground.Item);
            Assert.Equal(value.ExpectedRandomState, ground.FinalRandomState);
            Assert.Equal(value.ExpectedUniqueFlags.Order(), ground.UniqueItemFlags.Order());
            // Both APIs must detach caller storage, including untouched fields.
            Assert.Equal(callerFlags, request.UniqueItemFlags!.Order());
            Assert.Equal(value.InputItem, request.InputItem);
        }
    }

    private static void EqualItem(ItemCase value, string stage,
        AuthoritativeCathedralInitialItemState expected, AuthoritativeCathedralInitialItemState actual)
        => Assert.True(expected == actual,
            $"{value.Name}: depth={value.Depth} index={value.NativeItemId} seed={value.Seed} good={value.OnlyGood} {stage}" +
            $"\nExpected: {JsonSerializer.Serialize(expected, JsonOptions)}\nActual: {JsonSerializer.Serialize(actual, JsonOptions)}");

    private static Corpus ReadCorpus(JsonElement value)
    {
        Properties(value, "format", "version", "policy", "playerMaximumLife", "playerMaximumMana", "catalog", "cases",
            "caseCount", "initialUniqueCount", "changedUniqueCount", "exhaustedUniqueCount", "coverage");
        if (S(value, "format") != "godevilution-cathedral-theme-item-setup" || N(value, "version") != 1 ||
            S(value, "policy") != "original-diablo-single-player-normal-raw-setup-and-unique-continuation")
            throw new InvalidDataException("The native theme-item corpus identity does not match its detached contract.");
        var catalog = ReadCatalog(value.GetProperty("catalog"));
        var cases = A(value, "cases").EnumerateArray().Select(ReadCase).ToArray();
        if (cases.Length <= 1000 || cases.Length > 20000 || cases.Length != N(value, "caseCount"))
            throw new InvalidDataException("Native theme-item case count is incomplete or unbounded.");
        var identity = cases.Select(row => $"{row.Name}:{row.Depth}:{row.NativeItemId}:{row.Seed}:{row.OnlyGood}:" +
            string.Join(',', row.InputUniqueFlags.Order())).ToArray();
        if (identity.Distinct(StringComparer.Ordinal).Count() != identity.Length)
            throw new InvalidDataException("Native theme-item case identities must be unique.");
        var seeds = new uint[] { 0, 1, 2588, 743271966, 0x80000000, uint.MaxValue, 1457187811, 988045466 };
        foreach (var depth in Enumerable.Range(1, 4)) {
            foreach (var item in catalog.Items.Where(row => row.Available && row.DropRate > 0 &&
                    (row.MinimumMonsterLevel <= 2 * depth || row.ItemType == 0 && row.MiscId == 24) &&
                    row.SpellId is not (32 or 34))) {
                foreach (var seed in seeds) {
                    foreach (var good in new[] { false, true }) {
                        if (!good && item.MinimumMonsterLevel > 2 * depth) continue;
                        if (cases.Count(row => row.Name == "ordinary" && row.Depth == depth &&
                                row.NativeItemId == item.NativeItemIndex && row.Seed == seed && row.OnlyGood == good) != 1)
                            throw new InvalidDataException("The native ordinary/book input matrix is incomplete.");
                    }
                }
            }
        }
        var coverage = value.GetProperty("coverage");
        Properties(coverage, "reverseCompatibleUniques", "offsetUniques", "books", "gold", "staffSpells",
            "staffPowers", "magicalAffixes", "ordinaryDurability");
        var changed = cases.Where(row => row.SetupItem.Magical == 2 && row.ExpectedItem.Magical == 2 &&
            row.SetupItem.UniqueId != row.ExpectedItem.UniqueId).ToArray();
        var coverageCounts = new Dictionary<string, int> {
            ["reverseCompatibleUniques"] = changed.Count(row => (row.ExpectedItem.Buff & 30) == 0 && row.ExpectedItem.Seed != row.Seed),
            ["offsetUniques"] = changed.Count(row => (row.ExpectedItem.Buff & 30) != 0),
            ["books"] = cases.Count(row => row.ExpectedItem.MiscId == 24),
            ["gold"] = cases.Count(row => row.ExpectedItem.ItemType == 11),
            ["staffSpells"] = cases.Count(row => row.ExpectedItem.ItemType == 10 && row.ExpectedItem.Magical != 2 && row.ExpectedItem.SpellId != 0),
            // This counter includes the signed-counter Null fallback, not only the four-coin branch.
            ["staffPowers"] = cases.Count(row => row.ExpectedItem.ItemType == 10 && row.ExpectedItem.Magical != 2 && row.ExpectedItem.SpellId == 0 && row.OnlyGood),
            ["magicalAffixes"] = cases.Count(row => row.ExpectedItem.Magical == 1 && (row.ExpectedItem.PrefixPower != -1 || row.ExpectedItem.SuffixPower != -1)),
            ["ordinaryDurability"] = cases.Count(row => row.ExpectedItem.Magical == 0 && row.ExpectedItem.Durability > 0 && row.ExpectedItem.Durability != 255 && row.ExpectedItem.Durability != row.ExpectedItem.MaxDurability),
        };
        foreach (var field in coverage.EnumerateObject())
            if (N(coverage, field.Name) <= 0 || N(coverage, field.Name) != coverageCounts[field.Name])
                throw new InvalidDataException($"Missing native theme-item branch family: {field.Name}.");
        if (!cases.Any(row => row.Name == "staff-power" && row.ExpectedItem.SpellId == 0 &&
                (row.ExpectedItem.PrefixPower != -1 || row.ExpectedItem.SuffixPower != -1)) ||
            cases.Count(row => row.Name == "staff-signed-counter" && row.ExpectedItem.SpellId == 0) != 1)
            throw new InvalidDataException("The native staff affix and signed-counter cases are incomplete.");
        var initialUniques = cases.Count(row => row.SetupItem.Magical == 2);
        var changedUniques = cases.Count(row => row.SetupItem.Magical == 2 && row.ExpectedItem.Magical == 2 &&
            row.SetupItem.UniqueId != row.ExpectedItem.UniqueId);
        var exhausted = cases.Count(row => row.SetupItem.Magical == 2 && row.ExpectedItem.Magical != 2);
        if (initialUniques != N(value, "initialUniqueCount") || changedUniques != N(value, "changedUniqueCount") ||
            exhausted != N(value, "exhaustedUniqueCount") || changedUniques == 0 || exhausted == 0)
            throw new InvalidDataException("Native unique-history continuation counters do not match the complete records.");
        return new(catalog, cases, N(value, "playerMaximumLife"), N(value, "playerMaximumMana"));
    }

    private static ItemCase ReadCase(JsonElement value)
    {
        Properties(value, "name", "depth", "nativeItemId", "seed", "onlygood", "inputItem", "inputUniqueFlags", "setupItem",
            "setupRandomState", "expectedItem", "expectedRandomState", "expectedUniqueFlags", "groundItem");
        var name = S(value, "name");
        var depth = N(value, "depth");
        if (name is not ("ordinary" or "staff-power" or "staff-signed-counter" or "unique-fresh" or "unique-exhausted" or "unique-one-remaining" or "retained-fields") ||
            depth is < 1 or > 4)
            throw new InvalidDataException("Native theme-item case is outside the selected policy.");
        return new(name, depth, N(value, "nativeItemId"), U(value, "seed"), B(value, "onlygood"),
            ReadItem(value.GetProperty("inputItem")), Flags(value, "inputUniqueFlags"),
            ReadItem(value.GetProperty("setupItem")), U(value, "setupRandomState"),
            ReadItem(value.GetProperty("expectedItem")), U(value, "expectedRandomState"), Flags(value, "expectedUniqueFlags"),
            ReadItem(value.GetProperty("groundItem")));
    }

    internal static AuthoritativeCathedralThemeItemCatalog ReadCatalog(JsonElement value)
    {
        Properties(value, "items", "prefixes", "suffixes", "uniques", "spells", "glyphWidths", "glyphRows", "animations", "cursors", "generationRules");
        var animations = A(value, "animations").EnumerateArray().Select(row => {
            Properties(row, "cursorGraphic", "frameCount", "ticksPerFrame");
            return new KeyValuePair<int, AuthoritativeCathedralNativeAnimationDefinition>(N(row, "cursorGraphic"),
                new(N(row, "frameCount"), N(row, "ticksPerFrame")));
        }).ToDictionary();
        var items = A(value, "items").EnumerateArray().Select(row => {
            Properties(row, "nativeItemId", "available", "dropRate", "uniqueBaseItemId", "itemType", "cursorGraphic", "name", "shortName",
                "equipLocation", "itemClass", "minimumMonsterLevel", "minDamage", "maxDamage", "minArmorClass", "maxArmorClass", "flags",
                "miscId", "spellId", "value", "durability", "minimumStrength", "minimumMagic", "minimumDexterity", "usable");
            var cursor = N(row, "cursorGraphic");
            var animation = ReadBaseAnimation(cursor, N(row, "dropRate"), animations);
            return new AuthoritativeCathedralNativeItemDefinition(N(row, "nativeItemId"), N(row, "uniqueBaseItemId"), N(row, "dropRate"),
                N(row, "itemClass"), N(row, "equipLocation"), cursor, N(row, "itemType"), S(row, "name"), S(row, "shortName"),
                N(row, "minimumMonsterLevel"), N(row, "durability"), N(row, "minDamage"), N(row, "maxDamage"), N(row, "minArmorClass"),
                N(row, "maxArmorClass"), N(row, "minimumStrength"), N(row, "minimumMagic"), N(row, "minimumDexterity"), U(row, "flags"),
                N(row, "miscId"), N(row, "spellId"), B(row, "usable"), N(row, "value"), animation.FrameCount, animation.TicksPerFrame, B(row, "available"));
        }).ToArray();
        if (items.Where((row, index) => row.NativeItemIndex != index).Any())
            throw new InvalidDataException("Raw item indices must retain native table order.");
        var uniques = A(value, "uniques").EnumerateArray().Select(row => {
            Properties(row, "nativeUniqueId", "name", "cursorGraphic", "uniqueBaseItemId", "minimumLevel", "numberOfPowers", "value", "powers");
            var powers = A(row, "powers").EnumerateArray().Select(ReadPower).ToArray();
            if (powers.Length != 6 || N(row, "numberOfPowers") is < 0 or > 6)
                throw new InvalidDataException("Raw unique powers must retain all six native slots.");
            return new AuthoritativeCathedralNativeUniqueDefinition(N(row, "nativeUniqueId"), S(row, "name"), N(row, "cursorGraphic"),
                N(row, "uniqueBaseItemId"), N(row, "minimumLevel"), N(row, "value"), powers);
        }).ToArray();
        if (uniques.Where((row, index) => row.NativeUniqueId != index).Any())
            throw new InvalidDataException("Raw unique IDs must retain native table order.");
        var spells = A(value, "spells").EnumerateArray().Select(row => {
            Properties(row, "nativeSpellId", "name", "bookLevel", "staffLevel", "minimumMagic", "bookCost", "staffCost", "magicType", "staffMinCharges", "staffMaxCharges");
            return new AuthoritativeCathedralNativeSpellDefinition(N(row, "nativeSpellId"), S(row, "name"), N(row, "bookLevel"), N(row, "staffLevel"),
                N(row, "minimumMagic"), N(row, "bookCost"), N(row, "staffCost"), N(row, "magicType"), N(row, "staffMinCharges"), N(row, "staffMaxCharges"));
        }).ToArray();
        var rowZero = A(value, "glyphWidths").EnumerateArray().Select(Integer).ToArray();
        if (rowZero.Length != 256) throw new InvalidDataException("The native font requires all 256 row-zero glyph widths.");
        var glyphs = new Dictionary<int, int>();
        foreach (var row in A(value, "glyphRows").EnumerateArray()) {
            Properties(row, "row", "widths");
            var number = N(row, "row");
            var widths = A(row, "widths").EnumerateArray().Select(Integer).ToArray();
            if (number is < 0 or > 0x10FF || widths.Length != 256)
                throw new InvalidDataException("Native font rows require complete bounded glyph sets.");
            for (var index = 0; index < 256; index++) {
                if (widths[index] is < 0 or > ushort.MaxValue || !glyphs.TryAdd(number * 256 + index, widths[index]))
                    throw new InvalidDataException("Native font rows have invalid or duplicate widths.");
            }
        }
        if (!Enumerable.Range(0, 256).All(index => glyphs.TryGetValue(index, out var width) && width == rowZero[index]))
            throw new InvalidDataException("Native font row-zero projections disagree.");
        var cursors = value.GetProperty("cursors");
        Properties(cursors, "bookRed", "bookBlue", "bookGrey", "goldSmall", "goldMedium", "goldLarge");
        var rules = value.GetProperty("generationRules");
        Properties(rules, "magicChanceBase", "magicChancePerLevel", "uniqueChanceNormal", "uniqueChanceUnique", "bonusLevelsUnique",
            "prefixPercent", "suffixPercent", "onlygoodChance", "noDropPercent", "goldPercent", "alwaysMagicMisc");
        return new(items, ReadAffixes(value, "prefixes"), ReadAffixes(value, "suffixes"), uniques, spells, animations, glyphs,
            new(N(cursors, "bookRed"), N(cursors, "bookBlue"), N(cursors, "bookGrey"), N(cursors, "goldSmall"), N(cursors, "goldMedium"), N(cursors, "goldLarge")),
            A(rules, "alwaysMagicMisc").EnumerateArray().Select(Integer).ToHashSet(),
            new(N(rules, "magicChanceBase"), N(rules, "magicChancePerLevel"), N(rules, "uniqueChanceNormal"), N(rules, "uniqueChanceUnique"),
                N(rules, "bonusLevelsUnique"), N(rules, "prefixPercent"), N(rules, "suffixPercent"), N(rules, "onlygoodChance"), N(rules, "noDropPercent"), N(rules, "goldPercent")),
            glyphs['?']);
    }

    private static AuthoritativeCathedralNativeAffixDefinition[] ReadAffixes(JsonElement parent, string name)
        => A(parent, name).EnumerateArray().Select(row => {
            Properties(row, "name", "power", "minimumLevel", "itemTypes", "alignment", "chance", "useful", "minValue", "maxValue", "valueMultiplier");
            return new AuthoritativeCathedralNativeAffixDefinition(S(row, "name"), ReadPower(row.GetProperty("power")), N(row, "minimumLevel"),
                N(row, "itemTypes"), N(row, "alignment"), N(row, "chance"), B(row, "useful"), N(row, "minValue"), N(row, "maxValue"), N(row, "valueMultiplier"));
        }).ToArray();

    private static AuthoritativeCathedralNativeAnimationDefinition ReadBaseAnimation(int cursor, int dropRate,
        IReadOnlyDictionary<int, AuthoritativeCathedralNativeAnimationDefinition> animations)
    {
        // Preserve non-dropping native placeholder rows and their source ordinals.
        // They have no sprite, and the bounded generator rejects selecting them.
        if (cursor == byte.MaxValue && dropRate == 0)
            return new(0);
        return animations.TryGetValue(cursor, out var animation) ? animation
            : throw new InvalidDataException("A native base item has no captured cursor animation.");
    }

    private static AuthoritativeCathedralNativeItemPower ReadPower(JsonElement value)
    {
        Properties(value, "type", "param1", "param2");
        return new(N(value, "type"), N(value, "param1"), N(value, "param2"));
    }

    internal static AuthoritativeCathedralInitialItemState ReadItem(JsonElement value)
    {
        TypedProperties<AuthoritativeCathedralInitialItemState>(value);
        TypedProperties<AuthoritativeCathedralInitialItemAnimation>(value.GetProperty("animation"));
        return value.Deserialize<AuthoritativeCathedralInitialItemState>(JsonOptions)
            ?? throw new InvalidDataException("Native item projection must not be null.");
    }

    private static void TypedProperties<T>(JsonElement value)
    {
        var fields = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        Properties(value, fields.Select(field => JsonNamingPolicy.CamelCase.ConvertName(field.Name)).ToArray());
        foreach (var field in fields) {
            var raw = value.GetProperty(JsonNamingPolicy.CamelCase.ConvertName(field.Name));
            if (field.PropertyType == typeof(bool) && raw.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                field.PropertyType == typeof(string) && raw.ValueKind != JsonValueKind.String)
                throw new InvalidDataException($"Native projection field {field.Name} has the wrong scalar type.");
            if (field.PropertyType == typeof(int) || field.PropertyType == typeof(uint) || field.PropertyType == typeof(ushort))
                RequireIntegral(raw);
        }
    }

    internal static HashSet<int> Flags(JsonElement value, string name)
    {
        var entries = A(value, name).EnumerateArray().ToArray();
        if (entries.Length != 128 || entries.Any(row => row.ValueKind is not (JsonValueKind.True or JsonValueKind.False)))
            throw new InvalidDataException("Native unique history requires all 128 Boolean slots.");
        return entries.Select((row, index) => (row, index)).Where(value => value.row.GetBoolean()).Select(value => value.index).ToHashSet();
    }

    internal static void Properties(JsonElement value, params string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.EnumerateObject().Select(row => row.Name).Order(StringComparer.Ordinal)
                .SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidDataException("Native corpus object has missing, duplicate, or extra properties.");
    }

    internal static JsonElement A(JsonElement value, string name)
        => value.GetProperty(name).ValueKind == JsonValueKind.Array ? value.GetProperty(name) : throw new InvalidDataException($"{name} must be an array.");
    internal static int N(JsonElement value, string name) => Integer(value.GetProperty(name));
    internal static int Integer(JsonElement value) { RequireIntegral(value); return value.GetInt32(); }
    internal static uint U(JsonElement value, string name) { var raw = value.GetProperty(name); RequireIntegral(raw); return raw.GetUInt32(); }
    internal static string S(JsonElement value, string name)
        => value.GetProperty(name).ValueKind == JsonValueKind.String ? value.GetProperty(name).GetString()! : throw new InvalidDataException($"{name} must be text.");
    internal static bool B(JsonElement value, string name)
        => value.GetProperty(name).ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetProperty(name).GetBoolean() : throw new InvalidDataException($"{name} must be Boolean.");
    private static void RequireIntegral(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || value.GetRawText().Any(ch => ch is not (>= '0' and <= '9' or '-')))
            throw new InvalidDataException("Native numeric evidence must use integral JSON tokens, not floats or exponents.");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("float")]
    [InlineData("boolean")]
    public void ItemProjectionRejectsMalformedEvidence(string mutation)
    {
        var node = JsonSerializer.SerializeToNode(new AuthoritativeCathedralInitialItemState(), JsonOptions)!.AsObject();
        if (mutation == "missing") node.Remove("charges");
        if (mutation == "extra") node["extra"] = 1;
        if (mutation == "float") node["charges"] = 0.5;
        if (mutation == "boolean") node["identified"] = 0;
        using var document = JsonDocument.Parse(node.ToJsonString());
        Assert.Throws<InvalidDataException>(() => ReadItem(document.RootElement));
    }

    [Fact]
    public void ItemProjectionRejectsDuplicateEvidence()
    {
        var original = JsonSerializer.Serialize(new AuthoritativeCathedralInitialItemState(), JsonOptions);
        using var document = JsonDocument.Parse(original[..^1] + ",\"charges\":0}");
        Assert.Throws<InvalidDataException>(() => ReadItem(document.RootElement));
    }

    [Fact]
    public void BaseAnimationRequiresCapturedMetadataExceptNonDroppingSentinel()
    {
        var animations = new Dictionary<int, AuthoritativeCathedralNativeAnimationDefinition> { [10] = new(3) };
        Assert.Equal(new(3), ReadBaseAnimation(10, 1, animations));
        Assert.Equal(new(0), ReadBaseAnimation(255, 0, animations));
        Assert.Throws<InvalidDataException>(() => ReadBaseAnimation(255, 1, animations));
        Assert.Throws<InvalidDataException>(() => ReadBaseAnimation(9, 0, animations));
    }

    private sealed record Corpus(AuthoritativeCathedralThemeItemCatalog Catalog, ItemCase[] Cases, int PlayerMaximumLife, int PlayerMaximumMana);
    private sealed record ItemCase(string Name, int Depth, int NativeItemId, uint Seed, bool OnlyGood,
        AuthoritativeCathedralInitialItemState InputItem, HashSet<int> InputUniqueFlags,
        AuthoritativeCathedralInitialItemState SetupItem, uint SetupRandomState,
        AuthoritativeCathedralInitialItemState ExpectedItem, uint ExpectedRandomState, HashSet<int> ExpectedUniqueFlags,
        AuthoritativeCathedralInitialItemState GroundItem);
}

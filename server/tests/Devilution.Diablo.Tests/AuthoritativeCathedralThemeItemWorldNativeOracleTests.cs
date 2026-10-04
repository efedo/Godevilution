using System.Text.Json;
using System.Text.Json.Nodes;
using Devilution.Diablo;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeItemNativeOracleTests;

namespace Devilution.Diablo.Tests;

/** Actual native selectors/world calls; expected selections, positions and RNG are comparison-only. */
public sealed class AuthoritativeCathedralThemeItemWorldNativeOracleTests
{
    private const string DirectoryVariable = "GODEVILUTION_THEME_ITEM_PLACEMENT_ORACLE_DIRECTORY";
    private const string FileName = "native-theme-item-placement.json";
    private const int Cells = 112 * 112;
    private static readonly uint[] Seeds = [0, 1, 2588, 743271966, 0x80000000, uint.MaxValue, 1457187811, 988045466];
    private static readonly (bool Good, int Type, int Misc)[] SelectorLanes = [
        (false, -1, -1), (true, -1, -1), (false, 11, -1), (false, 0, 24),
        (false, 10, -1), (false, 0, 3), (false, 1, -1),
    ];
    private static readonly string[] WorldNames = [
        "random-open", "random-good-objects", "typed-gold-fallback", "typed-book-signed",
        "typed-staff-last-slot", "random-full-pool", "typed-gold-no-space", "typed-heal-edge",
        "typed-sword-unique", "typed-sword-exhausted",
    ];
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Theory(Explicit = true)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void WorldAndSelectorsMatchNative(int depth)
    {
        var directory = AuthoritativeCathedralEncounterNativeOracleTests.RequiredExternalDirectory(DirectoryVariable);
        Assert.Equal(new[] { FileName }, AuthoritativeCathedralEncounterNativeOracleTests.RegularEntries(directory).Select(Path.GetFileName));
        var corpus = ReadCorpus(AuthoritativeCathedralEncounterNativeOracleTests.ParseFile(Path.Combine(directory, FileName), 32 * 1024 * 1024));
        var policy = new AuthoritativeCathedralThemeItemPolicy(depth);
        foreach (var value in corpus.Selectors.Where(row => row.Depth == depth)) {
            var request = new AuthoritativeCathedralThemeItemWorldSelectionRequest(value.Seed, depth, value.Good,
                corpus.Catalog, policy, value.Type, value.Misc);
            var actual = value.Type < 0
                ? AuthoritativeCathedralThemeItemWorldGenerator.SelectRandomItem(request)
                : AuthoritativeCathedralThemeItemWorldGenerator.SelectTypeItem(request);
            Assert.Equal(value.SelectedIndex, actual.SelectedNativeItemIndex);
            Assert.Equal(value.RandomState, actual.FinalRandomState);
        }
        foreach (var value in corpus.Worlds.Where(row => row.Depth == depth)) {
            var before = Snapshot(value.Input, value.Seed);
            var request = new AuthoritativeCathedralThemeItemWorldRequest(value.Input, corpus.Catalog, policy, value.Seed,
                value.X, value.Y, value.Good, value.Delta, value.Type, value.Misc,
                new(LevelLoading: value.LevelLoading, MaximumLifeBase: corpus.MaximumLife, MaximumManaBase: corpus.MaximumMana));
            var actual = value.Type < 0
                ? AuthoritativeCathedralThemeItemWorldGenerator.CreateRandomItem(request)
                : AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(request);
            Assert.Equal(value.SelectedIndex, actual.SelectedNativeItemIndex);
            Assert.Equal(value.SelectionRandomState, actual.SelectorRandomState);
            EqualState(value.Name, value.Expected, Snapshot(actual.WorldState, actual.FinalRandomState));
            EqualState("caller unchanged", before, Snapshot(value.Input, value.Seed));
            Assert.Equal(value.Input.PlayerRefs, actual.PlayerRefs);
            Assert.Equal(value.Input.MonsterRefs, actual.MonsterRefs);
            Assert.Equal(value.Input.ObjectRefs, actual.ObjectRefs);
            Assert.Equal(value.Input.SolidTiles, actual.SolidTiles);
            Assert.Equal(value.Input.SetPieceTiles, actual.SetPieceTiles);
            Assert.Equal(value.Input.ObjectSolid, actual.ObjectSolid);
            Assert.Equal(value.Input.ObjectCanInteract, actual.ObjectCanInteract);
            // The allocation slot comes only from pre-call native pool state.
            var slot = value.Input.ActiveItemCount == 127 ? -1 : value.Input.ActiveItemIds[value.Input.ActiveItemCount];
            Assert.Equal(slot, actual.SelectedSlot);
            if (slot < 0) {
                Assert.Null(actual.Item);
                Assert.Equal(0u, actual.ItemSeed);
                Assert.False(actual.PlacementSucceeded);
            } else {
                // Result.ItemSeed is the initial setup seed. Unique retries may
                // replace the final item's seed, compared separately above.
                Assert.Equal(InitialSeedAfterPlacement(value.SelectionRandomState), actual.ItemSeed);
                Assert.Equal(value.Expected.Items[slot], actual.Item);
                Assert.Equal(value.Expected.Items[slot].PositionX, actual.PlacedX);
                Assert.Equal(value.Expected.Items[slot].PositionY, actual.PlacedY);
                Assert.Equal(value.Expected.ItemRefs.Contains(slot + 1), actual.PlacementSucceeded);
                Assert.NotSame(value.Input.Items[slot], actual.Items[slot]);
                Assert.NotSame(value.Input.Items[slot].Animation, actual.Items[slot].Animation);
            }
        }
    }

    private static State Snapshot(AuthoritativeCathedralThemeItemWorldState state, uint randomState)
        => new(randomState, state.ActiveItemCount, state.ActiveItemIds.ToArray(), state.Items.ToArray(),
            state.UniqueItemFlags.ToHashSet(), state.ItemRefs.ToArray());

    private static uint InitialSeedAfterPlacement(uint selectorState)
    {
        // Independent literal native LCG: GetItemSpace always draws once, then
        // SetupBaseItem calls AdvanceRndSeed (absolute signed result).
        var placementState = unchecked(selectorState * 0x015A4E35u + 1);
        var seedState = unchecked((int)(placementState * 0x015A4E35u + 1));
        return unchecked((uint)(seedState < 0 ? -seedState : seedState));
    }

    private static void EqualState(string name, State expected, State actual)
    {
        Assert.Equal(expected.RandomState, actual.RandomState);
        Assert.Equal(expected.Count, actual.Count);
        Assert.Equal(expected.ActiveIds, actual.ActiveIds);
        Assert.Equal(expected.ItemRefs, actual.ItemRefs);
        Assert.Equal(expected.Flags.Order(), actual.Flags.Order());
        Assert.Equal(expected.Items.Length, actual.Items.Length);
        for (var slot = 0; slot < expected.Items.Length; slot++)
            Assert.True(expected.Items[slot] == actual.Items[slot], $"{name}: full native item projection differs at slot {slot}.");
    }

    private static Corpus ReadCorpus(JsonElement value)
    {
        Properties(value, "format", "version", "policy", "playerMaximumLife", "playerMaximumMana", "catalog",
            "selectors", "worldCases", "selectorCount", "worldCount", "fullPools", "exhaustedPlacements",
            "signedOverwrites", "generatedUniques", "exhaustedHistories");
        if (S(value, "format") != "godevilution-cathedral-theme-item-placement" || N(value, "version") != 1 ||
            S(value, "policy") != "original-diablo-single-player-normal-world-item-creation")
            throw new InvalidDataException("Native world-item corpus identity differs from its contract.");
        var catalog = ReadCatalog(value.GetProperty("catalog"));
        if (catalog.Items.Count != 168 || catalog.Prefixes.Count != 83 || catalog.Suffixes.Count != 95 ||
            catalog.Uniques.Count != 90 || catalog.Spells.Count != 37)
            throw new InvalidDataException("The original-Diablo raw item tables are incomplete.");
        var selectors = A(value, "selectors").EnumerateArray().Select(ReadSelector).ToArray();
        var worlds = A(value, "worldCases").EnumerateArray().Select(ReadWorld).ToArray();
        ValidateMatrix(selectors, worlds);
        if (N(value, "selectorCount") != selectors.Length || N(value, "worldCount") != worlds.Length)
            throw new InvalidDataException("Native world-item corpus counters disagree with its records.");
        foreach (var (field, count) in DerivedCoverage(worlds))
            if (count != 4 || N(value, field) != count)
                throw new InvalidDataException($"Native world-item coverage is incomplete: {field}.");
        var life = N(value, "playerMaximumLife");
        var mana = N(value, "playerMaximumMana");
        if (life != 6400 || mana != 3200)
            throw new InvalidDataException("Native fixture player power inputs changed.");
        return new(catalog, selectors, worlds, life, mana);
    }

    private static Selector ReadSelector(JsonElement value)
    {
        Properties(value, "depth", "seed", "onlygood", "itemType", "miscId", "selectedIndex", "randomState");
        var row = new Selector(N(value, "depth"), U(value, "seed"), B(value, "onlygood"),
            N(value, "itemType"), N(value, "miscId"), N(value, "selectedIndex"), U(value, "randomState"));
        if (row.Depth is < 1 or > 4 || row.SelectedIndex is < 0 or >= 168 ||
            !SelectorLanes.Contains((row.Good, row.Type, row.Misc)))
            throw new InvalidDataException("Native selector is outside the captured bounded matrix.");
        return row;
    }

    private static World ReadWorld(JsonElement value)
    {
        Properties(value, "name", "depth", "seed", "onlygood", "delta", "levelLoading", "itemType", "miscId",
            "x", "y", "selectedIndex", "selectionRandomState", "input", "expected");
        var inputValue = value.GetProperty("input");
        var input = ReadInput(inputValue);
        var row = new World(S(value, "name"), N(value, "depth"), U(value, "seed"), B(value, "onlygood"), B(value, "delta"),
            B(value, "levelLoading"), N(value, "itemType"), N(value, "miscId"), N(value, "x"), N(value, "y"),
            N(value, "selectedIndex"), U(value, "selectionRandomState"), input, ReadState(value.GetProperty("expected"), false));
        if (!WorldNames.Contains(row.Name) || row.Depth is < 1 or > 4 || row.X is < 0 or >= 112 || row.Y is < 0 or >= 112 ||
            row.SelectedIndex is < 0 or >= 168 || U(inputValue, "randomState") != row.Seed)
            throw new InvalidDataException("Native world-item case identity or pre-call seed is invalid.");
        var isFull = input.ActiveItemCount == 127;
        if (row.Expected.Count != input.ActiveItemCount + (isFull ? 0 : 1) ||
            !row.Expected.ActiveIds.SequenceEqual(input.ActiveItemIds))
            throw new InvalidDataException("Native world-item allocation does not preserve pool order/count.");
        if (isFull && row.Expected.RandomState != row.SelectionRandomState)
            throw new InvalidDataException("Native full-pool world has no item-seed continuation.");
        return row;
    }

    private static State ReadState(JsonElement value, bool inputs)
    {
        Properties(value, inputs
            ? ["randomState", "activeCount", "activeIds", "items", "uniqueFlags", "itemRefs", "tiles", "objects"]
            : ["randomState", "activeCount", "activeIds", "items", "uniqueFlags", "itemRefs"]);
        var count = N(value, "activeCount");
        var active = A(value, "activeIds").EnumerateArray().Select(Integer).ToArray();
        var items = A(value, "items").EnumerateArray().Select(ReadItem).ToArray();
        var refs = A(value, "itemRefs").EnumerateArray().Select(Integer).ToArray();
        var flags = Flags(value, "uniqueFlags");
        if (count is < 0 or > 127 || active.Length != 127 || active.Any(id => id is < 0 or >= 127) ||
            active.Distinct().Count() != 127 || items.Length != 128 || refs.Length != Cells ||
            refs.Any(item => item is < -128 or > 127))
            throw new InvalidDataException("Native item pool/grid is incomplete or outside its field widths.");
        foreach (var item in items) AuthoritativeCathedralInitialItemGenerator.ValidateItemProjection(item);
        return new(U(value, "randomState"), count, active, items, flags, refs);
    }

    private static AuthoritativeCathedralThemeItemWorldState ReadInput(JsonElement value)
    {
        var state = ReadState(value, true);
        var tiles = A(value, "tiles").EnumerateArray().ToArray();
        var objects = A(value, "objects").EnumerateArray().ToArray();
        if (tiles.Length != Cells || objects.Length != 127)
            throw new InvalidDataException("Native pre-call tile/object channels are incomplete.");
        var player = new int[Cells]; var monster = new int[Cells]; var refs = new int[Cells];
        var setPiece = new bool[Cells]; var solid = new bool[Cells];
        for (var i = 0; i < tiles.Length; i++) {
            var tuple = Tuple(tiles[i], 6);
            player[i] = Integer(tuple[0]); monster[i] = Integer(tuple[1]); refs[i] = Integer(tuple[2]);
            if (Integer(tuple[3]) != state.ItemRefs[i])
                throw new InvalidDataException("Native pre-call item grid projections disagree.");
            setPiece[i] = Boolean(tuple[4]); solid[i] = Boolean(tuple[5]);
        }
        var objectSolid = new bool[127]; var interactive = new bool[127];
        for (var i = 0; i < objects.Length; i++) {
            var tuple = Tuple(objects[i], 2);
            objectSolid[i] = Boolean(tuple[0]);
            var selection = Integer(tuple[1]);
            if (selection is < 0 or > 7) throw new InvalidDataException("Native object selection bits are invalid.");
            interactive[i] = selection != 0;
        }
        return new(state.Items, state.ActiveIds, state.Count, state.ItemRefs, player, monster, refs,
            solid, setPiece, objectSolid, interactive, state.Flags);
    }

    private static JsonElement[] Tuple(JsonElement value, int count)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != count)
            throw new InvalidDataException("Native channel tuple has the wrong shape.");
        return value.EnumerateArray().ToArray();
    }

    private static bool Boolean(JsonElement value)
        => value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean()
            : throw new InvalidDataException("Native channel flags must be Boolean tokens.");

    private static void ValidateMatrix(Selector[] selectors, World[] worlds)
    {
        if (selectors.Length != 224 || worlds.Length != 40)
            throw new InvalidDataException("Native selector/world matrix is incomplete.");
        foreach (var depth in Enumerable.Range(1, 4)) {
            foreach (var seed in Seeds)
                foreach (var lane in SelectorLanes)
                    if (selectors.Count(row => row.Depth == depth && row.Seed == seed &&
                            (row.Good, row.Type, row.Misc) == lane) != 1)
                        throw new InvalidDataException("Native selector matrix contains missing or duplicate cases.");
            foreach (var name in WorldNames)
                if (worlds.Count(row => row.Depth == depth && row.Name == name) != 1)
                    throw new InvalidDataException("Native world matrix contains missing or duplicate cases.");
            var fresh = worlds.Single(row => row.Depth == depth && row.Name == "typed-sword-unique");
            var exhausted = worlds.Single(row => row.Depth == depth && row.Name == "typed-sword-exhausted");
            if (fresh.Seed is < 1 or > 4096 || fresh.Seed != exhausted.Seed || fresh.Input.UniqueItemFlags.Count != 0 ||
                exhausted.Input.UniqueItemFlags.Count != 128 || !fresh.Good || !exhausted.Good ||
                fresh.Type != 1 || exhausted.Type != 1 || fresh.Misc != -1 || exhausted.Misc != -1)
                throw new InvalidDataException("Native unique history comparisons need matching bounded sword inputs.");
        }
    }

    private static Dictionary<string, int> DerivedCoverage(World[] worlds)
    {
        var allocated = worlds.Where(row => row.Input.ActiveItemCount < 127).ToArray();
        int Slot(World row) => row.Input.ActiveItemIds[row.Input.ActiveItemCount];
        return new() {
            ["fullPools"] = worlds.Count(row => row.Input.ActiveItemCount == 127),
            ["exhaustedPlacements"] = allocated.Count(row => row.Expected.ItemRefs.SequenceEqual(row.Input.ItemRefs)),
            ["signedOverwrites"] = allocated.Count(row => {
                var item = row.Expected.Items[Slot(row)];
                return item.PositionX is >= 0 and < 112 && item.PositionY is >= 0 and < 112 &&
                    row.Input.ItemRefs[item.PositionY * 112 + item.PositionX] != 0 &&
                    row.Expected.ItemRefs[item.PositionY * 112 + item.PositionX] == Slot(row) + 1;
            }),
            ["generatedUniques"] = allocated.Count(row => row.Expected.Items[Slot(row)].Magical == 2),
            ["exhaustedHistories"] = allocated.Count(row => row.Input.UniqueItemFlags.Count == 128 && row.Expected.Items[Slot(row)].Magical != 2),
        };
    }

    [Theory]
    [InlineData("pool-short")]
    [InlineData("scratch-missing")]
    [InlineData("duplicate-id")]
    [InlineData("active-count")]
    [InlineData("grid-short")]
    [InlineData("signed-item")]
    [InlineData("float")]
    [InlineData("flags-short")]
    [InlineData("flags-numeric")]
    [InlineData("tiles-short")]
    [InlineData("tuple-short")]
    [InlineData("inconsistent-grid")]
    [InlineData("player-width")]
    [InlineData("monster-width")]
    [InlineData("unsafe-object")]
    [InlineData("objects-short")]
    [InlineData("selection-bits")]
    [InlineData("solid-numeric")]
    [InlineData("item-width")]
    [InlineData("extra")]
    public void PreCallWorldRejectsMalformedEvidence(string mutation)
    {
        var node = InputNode();
        switch (mutation) {
            case "pool-short": node["activeIds"]!.AsArray().RemoveAt(0); break;
            case "scratch-missing": node["items"]!.AsArray().RemoveAt(127); break;
            case "duplicate-id": node["activeIds"]![0] = 1; break;
            case "active-count": node["activeCount"] = 128; break;
            case "grid-short": node["itemRefs"]!.AsArray().RemoveAt(0); break;
            case "signed-item": node["itemRefs"]![0] = 128; break;
            case "float": node["activeCount"] = 0.5; break;
            case "flags-short": node["uniqueFlags"]!.AsArray().RemoveAt(127); break;
            case "flags-numeric": node["uniqueFlags"]![127] = 1; break;
            case "tiles-short": node["tiles"]!.AsArray().RemoveAt(0); break;
            case "tuple-short": node["tiles"]![0]!.AsArray().RemoveAt(0); break;
            case "inconsistent-grid": node["tiles"]![0]![3] = 1; break;
            case "player-width": node["tiles"]![0]![0] = -129; break;
            case "monster-width": node["tiles"]![0]![1] = 32768; break;
            case "unsafe-object": node["tiles"]![0]![2] = -128; break;
            case "objects-short": node["objects"]!.AsArray().RemoveAt(126); break;
            case "selection-bits": node["objects"]![0]![1] = 8; break;
            case "solid-numeric": node["tiles"]![0]![5] = 1; break;
            case "item-width": node["items"]![0]!["itemType"] = 128; break;
            case "extra": node["extra"] = true; break;
        }
        using var document = JsonDocument.Parse(node.ToJsonString());
        Assert.Throws<InvalidDataException>(() => ReadInput(document.RootElement));
    }

    [Fact]
    public void PreCallWorldRejectsDuplicateProperties()
    {
        var text = InputNode().ToJsonString();
        using var document = JsonDocument.Parse(text[..^1] + ",\"randomState\":0}");
        Assert.Throws<InvalidDataException>(() => ReadInput(document.RootElement));
    }

    [Fact]
    public void PreCallWorldRetainsHistory127AndUnreferencedActiveItems()
    {
        var node = InputNode();
        node["activeCount"] = 1;
        node["uniqueFlags"]![127] = true;
        using var document = JsonDocument.Parse(node.ToJsonString());
        var state = ReadInput(document.RootElement);
        Assert.Contains(127, state.UniqueItemFlags);
        Assert.Equal(1, state.ActiveItemCount);
        Assert.All(state.ItemRefs, value => Assert.Equal(0, value));
    }

    [Theory]
    [InlineData("index")]
    [InlineData("depth")]
    [InlineData("type")]
    [InlineData("misc")]
    [InlineData("boolean")]
    [InlineData("float")]
    [InlineData("extra")]
    public void SelectorRejectsMalformedEvidence(string mutation)
    {
        var node = new JsonObject { ["depth"] = 1, ["seed"] = 0, ["onlygood"] = false,
            ["itemType"] = -1, ["miscId"] = -1, ["selectedIndex"] = 0, ["randomState"] = 0 };
        switch (mutation) {
            case "index": node["selectedIndex"] = 168; break;
            case "depth": node["depth"] = 0; break;
            case "type": node["itemType"] = 12; break;
            case "misc": node["miscId"] = 24; break;
            case "boolean": node["onlygood"] = 1; break;
            case "float": node["randomState"] = 0.5; break;
            case "extra": node["extra"] = true; break;
        }
        using var document = JsonDocument.Parse(node.ToJsonString());
        Assert.Throws<InvalidDataException>(() => ReadSelector(document.RootElement));
    }

    private static JsonObject InputNode()
    {
        var items = new JsonArray();
        for (var slot = 0; slot < 128; slot++)
            items.Add(JsonSerializer.SerializeToNode(new AuthoritativeCathedralInitialItemState(), JsonOptions));
        var tiles = new JsonArray();
        for (var index = 0; index < Cells; index++) tiles.Add(new JsonArray(0, 0, 0, 0, false, false));
        var objects = new JsonArray();
        for (var index = 0; index < 127; index++) objects.Add(new JsonArray(false, 0));
        return new() {
            ["randomState"] = 0, ["activeCount"] = 0,
            ["activeIds"] = JsonSerializer.SerializeToNode(Enumerable.Range(0, 127).ToArray()),
            ["items"] = items, ["uniqueFlags"] = JsonSerializer.SerializeToNode(new bool[128]),
            ["itemRefs"] = JsonSerializer.SerializeToNode(new int[Cells]), ["tiles"] = tiles, ["objects"] = objects,
        };
    }

    private sealed record State(uint RandomState, int Count, int[] ActiveIds,
        AuthoritativeCathedralInitialItemState[] Items, HashSet<int> Flags, int[] ItemRefs);
    private sealed record Selector(int Depth, uint Seed, bool Good, int Type, int Misc, int SelectedIndex, uint RandomState);
    private sealed record World(string Name, int Depth, uint Seed, bool Good, bool Delta, bool LevelLoading, int Type, int Misc,
        int X, int Y, int SelectedIndex, uint SelectionRandomState, AuthoritativeCathedralThemeItemWorldState Input, State Expected);
    private sealed record Corpus(AuthoritativeCathedralThemeItemCatalog Catalog, Selector[] Selectors, World[] Worlds,
        int MaximumLife, int MaximumMana);
}

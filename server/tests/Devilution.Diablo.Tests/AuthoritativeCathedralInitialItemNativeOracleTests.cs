using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Private InitItems comparison after independently composed managed encounters. */
public sealed class AuthoritativeCathedralInitialItemNativeOracleTests
{
    private const string DirectoryVariable = "GODEVILUTION_CATHEDRAL_INITIAL_ITEM_ORACLE_DIRECTORY";
    private const string FileName = "native-initial-items.json";
    private const string Format = "godevilution-cathedral-initial-items";
    private const string Policy = "ordinary-original-diablo-post-initmonsters-inititems";
    private const int Cells = AuthoritativeCathedralPopulationGeometry.CellCount;
    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private static readonly string[] PublicAnimationFields = [
        "ticksPerFrame", "tickCounterOfCurrentFrame", "numberOfFrames", "currentFrame", "isPetrified",
    ];

    public static TheoryData<int, uint, string> Cases => AuthoritativeCathedralEncounterNativeOracleTests.Cases;

    [Theory(Explicit = true)]
    [MemberData(nameof(Cases))]
    public void ItemsMatchNativeOracle(int depth, uint seed, string entry)
    {
        var directory = AuthoritativeCathedralEncounterNativeOracleTests.RequiredExternalDirectory(DirectoryVariable);
        var entries = AuthoritativeCathedralEncounterNativeOracleTests.RegularEntries(directory);
        Assert.Equal(new[] { FileName }, entries.Select(Path.GetFileName));
        var root = AuthoritativeCathedralEncounterNativeOracleTests.ParseFile(Path.Combine(directory, FileName), 32 * 1024 * 1024);
        var corpus = ReadCorpus(root);
        var itemCase = corpus.Cases.Single(value => value.Depth == depth && value.InputSeed == seed && value.Entry == entry);
        var managed = AuthoritativeCathedralEncounterNativeOracleTests.GenerateManagedCase(depth, seed, entry);
        var pre = itemCase.PreState;
        Assert.Equal(managed.Encounter.FinalRandomState, pre.RandomState);
        Assert.Equal(managed.Population.Pieces.Select(value => (int)value), pre.Pieces);
        Assert.Equal(managed.Encounter.TileFlags.Select(value => (int)value), pre.TileFlags);
        Assert.Equal(managed.Encounter.MonsterTileRefs, pre.MonsterTileRefs);
        Assert.Equal(managed.PlayerTileRefs, pre.PlayerTileRefs);
        Assert.Equal(managed.Population.TileObjectRefs, pre.ObjectTileRefs);
        var freshSlots = Enumerable.Range(0, AuthoritativeCathedralInitialItemGenerator.PoolCapacity)
            .Select(_ => new AuthoritativeCathedralInitialItemState()).ToArray();
        var freshIds = Enumerable.Range(0, AuthoritativeCathedralInitialItemGenerator.ActiveCapacity).ToArray();
        Assert.Equal(0, pre.ActiveItemCount);
        Assert.Equal(freshIds, pre.ActiveItemIds);
        Assert.Equal(freshSlots, pre.Items);
        Assert.All(pre.ItemTileRefs, value => Assert.Equal(0, value));
        var tiles = Enumerable.Range(0, Cells).Select(index => new AuthoritativeCathedralInitialItemTile(
            managed.PlayerTileRefs[index], managed.Encounter.MonsterTileRefs[index], managed.Population.TileObjectRefs[index],
            SetPiece: (managed.Encounter.TileFlags[index] & 8) != 0,
            Solid: (managed.PieceProperties[managed.Population.Pieces[index]] & 1) != 0)).ToArray();
        var actual = AuthoritativeCathedralInitialItemGenerator.Generate(new(
            depth, managed.Encounter.FinalRandomState, tiles, freshSlots, freshIds, 0, corpus.Potions));
        var expected = itemCase.ExpectedState;
        Assert.Equal(expected.RandomState, actual.FinalRandomState);
        Assert.Equal(expected.ActiveItemCount, actual.ActiveItemCount);
        Assert.Equal(expected.ActiveItemIds, actual.ActiveItemIds);
        Assert.Equal(expected.ItemTileRefs, actual.ItemTileRefs);
        // The native sidecar intentionally excludes sprites and private visual
        // distribution internals. Compare every public scalar/string field.
        Assert.Equal(expected.Items, actual.Items);
        Assert.Equal(pre.Pieces, expected.Pieces);
        Assert.Equal(pre.TileFlags, expected.TileFlags);
        Assert.Equal(pre.MonsterTileRefs, expected.MonsterTileRefs);
        Assert.Equal(pre.PlayerTileRefs, expected.PlayerTileRefs);
        Assert.Equal(pre.ObjectTileRefs, expected.ObjectTileRefs);
        Assert.False(actual.ShowUniqueItemInfoBox);
        Assert.Equal(0, actual.ItemGetRecordCount);
        Assert.All(actual.ItemGetRecords, value => Assert.Equal(new AuthoritativeCathedralInitialItemGetRecord(), value));
    }

    private static ItemCorpus ReadCorpus(JsonElement root)
    {
        RequireProperties(root, "format", "version", "policy", "caseCount", "potionDefinitions", "cases");
        if (String(root, "format") != Format || Integer(root, "version") != 1
            || String(root, "policy") != Policy || Integer(root, "caseCount") != 16)
            throw new InvalidDataException("Initial-item corpus identity does not match the native phase contract.");
        var potions = Array(root, "potionDefinitions").EnumerateArray().Select(value => {
            RequireTypedProperties<AuthoritativeCathedralInitialItemDefinition>(value);
            return Deserialize<AuthoritativeCathedralInitialItemDefinition>(value);
        }).ToArray();
        if (potions.Length != 2 || !potions.Select(value => value.NativeItemId).Order()
                .SequenceEqual(new[] { 24, 25 }))
            throw new InvalidDataException("Initial-item corpus requires native HEAL/MANA rows 24/25.");
        var cases = Array(root, "cases").EnumerateArray().Select(value => {
            RequireProperties(value, "snapshot", "depth", "inputSeed", "entry", "preState", "expectedState");
            var depth = Integer(value, "depth");
            var seed = Unsigned(value, "inputSeed");
            var entry = String(value, "entry");
            var snapshot = String(value, "snapshot");
            if (depth is < 1 or > 4 || seed is not (2588 or 743271966) || entry is not ("main" or "previous")
                || snapshot != $"cathedral-population-depth-{depth}-{seed}-{entry}.json")
                throw new InvalidDataException("Initial-item case is outside the required ordinary Cathedral matrix.");
            return new ItemCase(snapshot, depth, seed, entry,
                ReadState(value.GetProperty("preState")), ReadState(value.GetProperty("expectedState")));
        }).ToArray();
        var expectedNames = (from depth in Enumerable.Range(1, 4)
            from seed in new uint[] { 2588, 743271966 }
            from entry in new[] { "main", "previous" }
            select $"cathedral-population-depth-{depth}-{seed}-{entry}.json").Order().ToArray();
        if (cases.Length != 16 || !cases.Select(value => value.Snapshot).Order().SequenceEqual(expectedNames))
            throw new InvalidDataException("Initial-item corpus must contain each of the sixteen cases exactly once.");
        return new(potions, cases);
    }

    private static ItemState ReadState(JsonElement value)
    {
        RequireTypedProperties<ItemState>(value);
        foreach (var item in Array(value, "items").EnumerateArray()) {
            RequireTypedProperties<AuthoritativeCathedralInitialItemState>(item);
            RequireProperties(item.GetProperty("animation"), PublicAnimationFields);
        }
        var state = Deserialize<ItemState>(value);
        if (state.Items.Length != 128 || state.ActiveItemIds.Length != 127
            || !state.ActiveItemIds.SequenceEqual(Enumerable.Range(0, 127)) || state.ActiveItemCount is < 0 or > 127)
            throw new InvalidDataException("Initial-item state requires all 128 slots and 127 identity active IDs.");
        foreach (var item in state.Items) {
            AuthoritativeCathedralInitialItemGenerator.ValidateItemProjection(item);
            if (item.ItemType is < -1 or > 13 || item.Magical is < 0 or > 2
                || item.EquipLocation is < -1 or > 8 || item.ItemClass is < 0 or > 5
                || item.ItemIndex < -1)
                throw new InvalidDataException("Initial-item scalar state is outside the captured native representation.");
        }
        ValidateArray(state.ItemTileRefs, 0, 127);
        ValidateArray(state.Pieces, 0, AuthoritativeCathedralPopulationGeometry.PieceCount - 1);
        ValidateArray(state.TileFlags, 0, byte.MaxValue);
        ValidateArray(state.MonsterTileRefs, -200, 200);
        ValidateArray(state.PlayerTileRefs, -4, 4);
        ValidateArray(state.ObjectTileRefs, -127, 127);
        var seen = new HashSet<int>();
        for (var index = 0; index < state.ItemTileRefs.Length; index++) {
            var reference = state.ItemTileRefs[index];
            if (reference == 0) continue;
            var slot = reference - 1;
            if (slot >= state.ActiveItemCount || !seen.Add(slot)
                || state.Items[slot].PositionX != index % 112 || state.Items[slot].PositionY != index / 112
                || state.Items[slot].ItemType == -1)
                throw new InvalidDataException("Initial-item references must uniquely match active item positions.");
        }
        if (seen.Count != state.ActiveItemCount)
            throw new InvalidDataException("Every active initial item must occupy one tile.");
        return state;
    }

    private static void ValidateArray(int[] values, int minimum, int maximum)
    {
        if (values.Length != Cells || values.Any(value => value < minimum || value > maximum))
            throw new InvalidDataException("Initial-item world channels must be complete native arrays.");
    }

    private static void RequireTypedProperties<T>(JsonElement value)
        => RequireProperties(value, typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name)).ToArray());

    private static void RequireProperties(JsonElement value, params string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Initial-item records must be JSON objects.");
        var properties = value.EnumerateObject().Select(property => property.Name).ToArray();
        if (properties.Length != properties.Distinct(StringComparer.Ordinal).Count()
            || !properties.Order().SequenceEqual(expected.Order()))
            throw new InvalidDataException("Initial-item record has duplicate, missing or extra properties.");
    }

    private static T Deserialize<T>(JsonElement value)
    {
        try { return value.Deserialize<T>(JsonOptions) ?? throw new InvalidDataException("Null initial-item record."); }
        catch (JsonException exception) { throw new InvalidDataException("Invalid typed initial-item record.", exception); }
    }

    private static string String(JsonElement value, string name)
        => value.GetProperty(name).ValueKind == JsonValueKind.String
            ? value.GetProperty(name).GetString()! : throw new InvalidDataException("Expected initial-item string.");
    private static int Integer(JsonElement value, string name)
        => value.GetProperty(name).ValueKind == JsonValueKind.Number && value.GetProperty(name).TryGetInt32(out var number)
            ? number : throw new InvalidDataException("Expected initial-item int32.");
    private static uint Unsigned(JsonElement value, string name)
        => value.GetProperty(name).ValueKind == JsonValueKind.Number && value.GetProperty(name).TryGetUInt32(out var number)
            ? number : throw new InvalidDataException("Expected initial-item uint32.");
    private static JsonElement Array(JsonElement value, string name)
        => value.GetProperty(name).ValueKind == JsonValueKind.Array
            ? value.GetProperty(name) : throw new InvalidDataException("Expected initial-item array.");

    [Fact]
    public void ReaderRejectsDuplicateExtraAndMissingProperties()
    {
        using var duplicate = JsonDocument.Parse("{\"value\":1,\"value\":2}");
        using var extra = JsonDocument.Parse("{\"value\":1,\"extra\":2}");
        using var missing = JsonDocument.Parse("{}");
        Assert.Throws<InvalidDataException>(() => RequireProperties(duplicate.RootElement, "value"));
        Assert.Throws<InvalidDataException>(() => RequireProperties(extra.RootElement, "value"));
        Assert.Throws<InvalidDataException>(() => RequireProperties(missing.RootElement, "value"));
    }

    [Fact]
    public void ReaderRejectsFloatingAndStringIntegers()
    {
        using var floating = JsonDocument.Parse("{\"value\":1.5}");
        using var text = JsonDocument.Parse("{\"value\":\"1\"}");
        Assert.Throws<InvalidDataException>(() => Integer(floating.RootElement, "value"));
        Assert.Throws<InvalidDataException>(() => Integer(text.RootElement, "value"));
    }

    [Fact]
    public void ReaderRejectsMalformedChannelDimensionsAndRanges()
    {
        Assert.Throws<InvalidDataException>(() => ValidateArray(new int[Cells - 1], 0, 127));
        var values = new int[Cells];
        values[0] = -1;
        Assert.Throws<InvalidDataException>(() => ValidateArray(values, 0, 127));
        values[0] = 128;
        Assert.Throws<InvalidDataException>(() => ValidateArray(values, 0, 127));
    }

    [Fact]
    public void ReaderRequiresFullScalarItemsIdentityIdsAndUniqueOccupancy()
    {
        var state = new ItemState(0, 0, Enumerable.Range(0, 127).ToArray(),
            Enumerable.Range(0, 128).Select(_ => new AuthoritativeCathedralInitialItemState()).ToArray(),
            new int[Cells], new int[Cells], new int[Cells], new int[Cells], new int[Cells], new int[Cells]);
        var valid = CapturedStateJson(state);
        Assert.Equal(128, ReadState(JsonSerializer.SerializeToElement(valid)).Items.Length);
        var missing = valid.DeepClone();
        missing["items"]![0]!.AsObject().Remove("seed");
        Assert.Throws<InvalidDataException>(() => ReadState(JsonSerializer.SerializeToElement(missing)));
        var badIds = state.ActiveItemIds.ToArray();
        badIds[^1] = badIds[^2];
        Assert.Throws<InvalidDataException>(() => ReadState(JsonSerializer.SerializeToElement(CapturedStateJson(state with { ActiveItemIds = badIds }))));
        var activeItems = state.Items.ToArray();
        activeItems[0] = activeItems[0] with { ItemType = 0, PositionX = 20, PositionY = 20 };
        var active = state with { Items = activeItems, ActiveItemCount = 1 };
        Assert.Throws<InvalidDataException>(() => ReadState(JsonSerializer.SerializeToElement(CapturedStateJson(active))));
        var refs = new int[Cells];
        refs[20 * 112 + 20] = 1;
        active = active with { ItemTileRefs = refs };
        Assert.Equal(1, ReadState(JsonSerializer.SerializeToElement(CapturedStateJson(active))).ActiveItemCount);
        refs[20 * 112 + 21] = 1;
        Assert.Throws<InvalidDataException>(() => ReadState(JsonSerializer.SerializeToElement(CapturedStateJson(active))));
    }

    private static JsonNode CapturedStateJson(ItemState state)
    {
        var node = JsonSerializer.SerializeToNode(state, JsonOptions)!;
        foreach (var item in node["items"]!.AsArray()) {
            var animation = item!["animation"]!.AsObject();
            foreach (var name in animation.Select(property => property.Key).Except(PublicAnimationFields).ToArray())
                animation.Remove(name);
        }
        return node;
    }

    private sealed record ItemCorpus(AuthoritativeCathedralInitialItemDefinition[] Potions, ItemCase[] Cases);
    private sealed record ItemCase(string Snapshot, int Depth, uint InputSeed, string Entry, ItemState PreState, ItemState ExpectedState);
    private sealed record ItemState(uint RandomState, int ActiveItemCount, int[] ActiveItemIds,
        AuthoritativeCathedralInitialItemState[] Items, int[] ItemTileRefs, int[] Pieces,
        int[] TileFlags, int[] MonsterTileRefs, int[] PlayerTileRefs, int[] ObjectTileRefs);
}

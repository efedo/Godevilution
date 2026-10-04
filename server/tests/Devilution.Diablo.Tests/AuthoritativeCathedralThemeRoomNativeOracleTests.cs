using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Devilution.Diablo;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeItemNativeOracleTests;

namespace Devilution.Diablo.Tests;

/** Complete actual CreateThemeRooms input/output; expected world state is comparison-only. */
public sealed class AuthoritativeCathedralThemeRoomNativeOracleTests
{
    private const int Cells = 112 * 112;
    private const string FileName = "native-theme-rooms.json";
    private static readonly int[] Types = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 13, 14, 15];
    private static readonly string[] Names = [.. Types.Select(type => $"type-{type}"),
        "ordered", "object-full-library", "object-full-failed-fit", "item-full-signed-pit",
        "monster-full-skeleton", "monster-full-goat", "loading-shrine", "empty"];
    private static readonly JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    [Theory(Explicit = true)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void CompleteOrderedThemeWorldMatchesNative(int depth)
    {
        var directory = AuthoritativeCathedralEncounterNativeOracleTests.RequiredExternalDirectory(
            "GODEVILUTION_THEME_ROOM_ORACLE_DIRECTORY");
        Assert.Equal(new[] { FileName }, AuthoritativeCathedralEncounterNativeOracleTests.RegularEntries(directory).Select(Path.GetFileName));
        var corpus = ReadCorpus(AuthoritativeCathedralEncounterNativeOracleTests.ParseFile(
            Path.Combine(directory, FileName), 128 * 1024 * 1024));
        foreach (var row in corpus.Cases.Where(row => row.Depth == depth)) {
            var callerBefore = JsonSerializer.Serialize(row.Input.State, Options);
            var request = new AuthoritativeCathedralThemeRoomRequest(row.Input.State, corpus.RoomCatalog,
                corpus.ItemCatalog, new(depth), row.Themes, row.Seed, row.Player,
                new(MaximumLifeBase: corpus.MaximumLife, MaximumManaBase: corpus.MaximumMana),
                row.Loading, row.Input.ThemeVar1, InitialThemeOrigin: row.Input.Origin);
            var result = AuthoritativeCathedralThemeRoomGenerator.Generate(request);
            EqualWorld(row.Name, row.Expected, new(result.State, result.FinalRandomState,
                result.FinalThemeOrigin, result.FinalThemeVar1));
            Assert.Equal(callerBefore, JsonSerializer.Serialize(row.Input.State, Options));
            Assert.NotSame(row.Input.State.ObjectSlots, result.State.ObjectSlots);
            Assert.NotSame(row.Input.State.MonsterSlots, result.State.MonsterSlots);
            Assert.NotSame(row.Input.State.ItemSlots, result.State.ItemSlots);
        }
    }

    internal static void EqualWorld(string name, World expected, World actual)
    {
        Assert.True(expected.RandomState == actual.RandomState, $"{name}: final RNG expected {expected.RandomState}, actual {actual.RandomState}.");
        Assert.True(expected.Origin == actual.Origin, $"{name}: scratch origin expected {expected.Origin}, actual {actual.Origin}.");
        Assert.True(expected.ThemeVar1 == actual.ThemeVar1, $"{name}: scratch variant expected {expected.ThemeVar1}, actual {actual.ThemeVar1}.");
        var e = expected.State; var a = actual.State;
        Assert.Equal(e.ActiveObjectCount, a.ActiveObjectCount);
        Assert.Equal(e.ActiveObjectIds.ToArray(), a.ActiveObjectIds.ToArray());
        Assert.Equal(e.AvailableObjectIds.ToArray(), a.AvailableObjectIds.ToArray());
        Assert.Equal(e.ActiveMonsterCount, a.ActiveMonsterCount);
        Assert.Equal(e.MonsterTargetCount, a.MonsterTargetCount);
        Assert.Equal(e.ActiveMonsterIds.ToArray(), a.ActiveMonsterIds.ToArray());
        Assert.Equal(e.MonsterPetrified.ToArray(), a.MonsterPetrified.ToArray());
        Assert.Equal(e.ActiveItemCount, a.ActiveItemCount);
        Assert.Equal(e.ActiveItemIds.ToArray(), a.ActiveItemIds.ToArray());
        Assert.Equal(e.UniqueItemFlags.Order(), a.UniqueItemFlags.Order());
        Assert.Equal(e.Pieces.ToArray(), a.Pieces.ToArray());
        Assert.Equal(e.Special.ToArray(), a.Special.ToArray());
        Assert.Equal(e.Transparency.ToArray(), a.Transparency.ToArray());
        Assert.Equal(e.TileFlags.ToArray(), a.TileFlags.ToArray());
        Assert.Equal(e.Corpses.ToArray(), a.Corpses.ToArray());
        Assert.Equal(e.PlayerRefs.ToArray(), a.PlayerRefs.ToArray());
        Assert.Equal(e.MonsterRefs.ToArray(), a.MonsterRefs.ToArray());
        Assert.Equal(e.ObjectRefs.ToArray(), a.ObjectRefs.ToArray());
        Assert.Equal(e.ItemRefs.ToArray(), a.ItemRefs.ToArray());
        Assert.Equal(e.Light.ToArray(), a.Light.ToArray());
        Assert.Equal(e.PreLight.ToArray(), a.PreLight.ToArray());
        Assert.Equal(e.PieceProperties.ToArray(), a.PieceProperties.ToArray());
        Assert.Equal(e.Flags, a.Flags);
        Assert.Equal(e.UpdateLighting, a.UpdateLighting);
        Assert.Equal(e.DisableLighting, a.DisableLighting);
        Assert.Equal(e.ActiveLightCount, a.ActiveLightCount);
        Assert.Equal(e.ActiveLightIds.ToArray(), a.ActiveLightIds.ToArray());
        Assert.Equal(e.LightRecords.ToArray(), a.LightRecords.ToArray());
        EqualSlots(name, "object", e.ObjectSlots, a.ObjectSlots);
        EqualSlots(name, "monster", e.MonsterSlots, a.MonsterSlots);
        EqualSlots(name, "item", e.ItemSlots, a.ItemSlots);
    }

    private static void EqualSlots<T>(string name, string kind, IReadOnlyList<T> expected, IReadOnlyList<T> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var slot = 0; slot < expected.Count; slot++)
            Assert.True(EqualityComparer<T>.Default.Equals(expected[slot], actual[slot]),
                $"{name}: complete {kind} slot {slot} differs.\nExpected: {JsonSerializer.Serialize(expected[slot], Options)}\nActual: {JsonSerializer.Serialize(actual[slot], Options)}");
    }

    internal sealed record World(AuthoritativeCathedralThemeRoomState State, uint RandomState,
        AuthoritativeCathedralPopulationPoint Origin, int ThemeVar1);
    private sealed record Case(string Name, int Depth, uint Seed, bool Loading,
        AuthoritativeCathedralThemeEntry[] Themes, AuthoritativeCathedralPopulationPlayer Player,
        World Input, World Expected);
    private sealed record Corpus(AuthoritativeCathedralThemeItemCatalog ItemCatalog,
        AuthoritativeCathedralThemeRoomCatalog RoomCatalog, Case[] Cases, int MaximumLife, int MaximumMana);

    private static Corpus ReadCorpus(JsonElement root)
    {
        Properties(root, "format", "version", "policy", "playerMaximumLife", "playerMaximumMana",
            "itemCatalog", "roomCatalog", "cases", "caseCount");
        if (S(root, "format") != "godevilution-cathedral-theme-rooms" || N(root, "version") != 1
            || S(root, "policy") != "original-diablo-single-player-normal-complete-selected-cathedral-themes"
            || N(root, "caseCount") != 88 || N(root, "playerMaximumLife") != 6400 || N(root, "playerMaximumMana") != 3200)
            throw new InvalidDataException("Theme-room corpus identity or scope is not supported.");
        var itemCatalog = ReadCatalog(root.GetProperty("itemCatalog"));
        if (itemCatalog.Items.Count != 168 || itemCatalog.Prefixes.Count != 83 || itemCatalog.Suffixes.Count != 95
            || itemCatalog.Uniques.Count != 90 || itemCatalog.Spells.Count != 37)
            throw new InvalidDataException("Original Diablo requires complete raw item tables.");
        var roomCatalog = ReadRoomCatalog(root.GetProperty("roomCatalog"));
        var cases = A(root, "cases").EnumerateArray().Select(ReadCase).ToArray();
        ValidateNames(cases.Select(row => $"{row.Depth}:{row.Name}"));
        return new(itemCatalog, roomCatalog, cases, 6400, 3200);
    }

    private static Case ReadCase(JsonElement value)
    {
        Properties(value, "name", "depth", "seed", "loadingMapObjects", "themes", "player", "input", "expected");
        var name = S(value, "name"); var depth = N(value, "depth"); var seed = U(value, "seed");
        if (!Names.Contains(name) || depth is < 1 or > 4
            || seed != (name == "item-full-signed-pit" ? 1457187811U : 2588U + (uint)depth * 17))
            throw new InvalidDataException("Theme-room case name/depth/seed is outside the exact matrix.");
        var themes = A(value, "themes").EnumerateArray().Select(row => {
            var fields = row.ValueKind == JsonValueKind.Array ? row.EnumerateArray().Select(Integer).ToArray()
                : throw new InvalidDataException("Theme entries require integral pairs.");
            if (fields.Length != 2 || !Types.Contains(fields[0]) || fields[1] != 1)
                throw new InvalidDataException("Theme selectors are outside the exact fixture scope.");
            return new AuthoritativeCathedralThemeEntry(fields[0], fields[1]);
        }).ToArray();
        var expectedTypes = name == "empty" ? [] : name == "ordered" ? Types
            : new[] { name.StartsWith("type-", StringComparison.Ordinal) ? int.Parse(name[5..])
                : name is "object-full-library" or "object-full-failed-fit" ? 5
                : name == "item-full-signed-pit" ? 2 : name == "monster-full-skeleton" ? 3
                : name == "monster-full-goat" ? 11 : 1 };
        if (!themes.Select(row => row.ThemeType).SequenceEqual(expectedTypes))
            throw new InvalidDataException("Theme sequence is incomplete or reordered.");
        var player = ReadRecord<AuthoritativeCathedralPopulationPlayer>(value.GetProperty("player"));
        if (player != new AuthoritativeCathedralPopulationPlayer(0, depth, true, false, new(60, 60), new(61, 60)))
            throw new InvalidDataException("The complete pre-call fixture player context is required.");
        var input = ReadWorld(value.GetProperty("input"));
        var expected = ReadWorld(value.GetProperty("expected"));
        var loading = B(value, "loadingMapObjects");
        if (input.RandomState != seed || loading != (name == "loading-shrine")
            || input.Origin != new AuthoritativeCathedralPopulationPoint(29, 31) || input.ThemeVar1 != 2)
            throw new InvalidDataException("Native pre-call RNG/scratch/loading identity differs from its case.");
        return new(name, depth, seed, loading, themes, player, input, expected);
    }

    internal static AuthoritativeCathedralThemeRoomCatalog ReadRoomCatalog(JsonElement value, bool requireDetachedBestiary = true)
    {
        Properties(value, "objects", "bestiary", "shrineAvailable");
        var objects = A(value, "objects").EnumerateArray().Select(row => {
            Properties(row, "nativeType", "flags", "animDelay", "animLength", "animWidth", "selectionRegion");
            return new AuthoritativeCathedralPopulationObjectDefinition(N(row, "nativeType"),
                Byte(N(row, "flags")), Byte(N(row, "animDelay")), Byte(N(row, "animLength")),
                Byte(N(row, "animWidth")), N(row, "selectionRegion"));
        }).ToArray();
        var bestiary = A(value, "bestiary").EnumerateArray().Select(row => {
            Properties(row, "nativeType", "placeFlags", "isSkeleton", "isGoat", "minimumHitPoints",
                "maximumHitPoints", "ai", "intelligence", "abilityFlags", "minDamage", "maxDamage",
                "minDamageSpecial", "maxDamageSpecial", "armorClass", "resistance", "reducePlayerStrength",
                "reducePlayerMagic", "reducePlayerDexterity", "reducePlayerVitality", "reducePlayerMaxHP",
                "reducePlayerMaxMana", "frames", "rates");
            var frames = IntArray(row, "frames", 6); var rates = IntArray(row, "rates", 6);
            if (frames.Concat(rates).Any(number => number is < sbyte.MinValue or > sbyte.MaxValue))
                throw new InvalidDataException("Loaded native bestiary animations/order are invalid.");
            return new AuthoritativeCathedralThemeRoomMonsterDefinition(new(N(row, "nativeType"), B(row, "isSkeleton"),
                N(row, "minimumHitPoints"), N(row, "maximumHitPoints"), N(row, "ai"), N(row, "intelligence"), U(row, "abilityFlags"),
                N(row, "minDamage"), N(row, "maxDamage"), N(row, "minDamageSpecial"), N(row, "maxDamageSpecial"),
                N(row, "armorClass"), N(row, "resistance"), N(row, "reducePlayerStrength"), N(row, "reducePlayerMagic"),
                N(row, "reducePlayerDexterity"), N(row, "reducePlayerVitality"), N(row, "reducePlayerMaxHP"),
                N(row, "reducePlayerMaxMana"), frames, rates), U(row, "placeFlags"), B(row, "isGoat"));
        }).ToArray();
        if (requireDetachedBestiary && !bestiary.Select(row => row.Definition.NativeType).SequenceEqual(new[] { 109, 0, 8, 20, 34, 42 }))
            throw new InvalidDataException("The exact six loaded native bestiary rows are required.");
        return new(objects, bestiary, BoolArray(value, "shrineAvailable", 26));
    }

    internal static World ReadWorld(JsonElement value)
    {
        Properties(value, "randomState", "activeObjectCount", "activeObjectIds", "availableObjectIds", "objects",
            "activeMonsterCount", "monsterTargetCount", "activeMonsterIds", "monsters", "monsterPetrified",
            "activeItemCount", "activeItemIds", "items", "uniqueFlags", "tiles", "sol", "themeX", "themeY",
            "themeVar1", "flags", "lightAllocator");
        var objects = A(value, "objects").EnumerateArray().Select(ReadRecord<AuthoritativeCathedralPopulationObjectState>).ToArray();
        var monsters = A(value, "monsters").EnumerateArray().Select(ReadRecord<AuthoritativeCathedralPopulationMonsterState>).ToArray();
        if (objects.Length != 127 || monsters.Length != 200
            || objects.Where((row, index) => row.Slot != index || row.Type is < -1 or >= 109).Any()
            || monsters.Where((row, index) => row.Slot != index || row.Type is < 0 or > 137 || row.LevelType is < 0 or > 23).Any())
            throw new InvalidDataException("Every ordered native object/monster slot is required.");
        var items = A(value, "items").EnumerateArray().Select(ReadItem).ToArray();
        var tuples = A(value, "tiles").EnumerateArray().Select(row => {
            var fields = row.ValueKind == JsonValueKind.Array ? row.EnumerateArray().Select(Integer).ToArray()
                : throw new InvalidDataException("Native tile channels require integral arrays.");
            if (fields.Length != 11 || fields[0] is < 0 or >= 1379 || fields[1] is < 0 or > 255
                || fields[2] is < -128 or > 127 || fields[3] is < 0 or > 255 || fields[4] is < 0 or > 255
                || fields[5] is < -128 or > 127 || fields[6] is < short.MinValue or > short.MaxValue
                || fields[7] is < -127 or > 127 || fields[8] is < -128 or > 127
                || fields[9] is < 0 or > 15 || fields[10] is < 0 or > 15)
                throw new InvalidDataException("Native tile channel width/range is invalid.");
            return fields;
        }).ToArray();
        if (tuples.Length != Cells) throw new InvalidDataException("All 112x112 native tiles are required.");
        int[] Channel(int column) => tuples.Select(row => row[column]).ToArray();
        var allocator = value.GetProperty("lightAllocator");
        Properties(allocator, "activeCount", "updateLighting", "disableLighting", "activeIds", "records");
        var lights = A(allocator, "records").EnumerateArray().Select(ReadRecord<AuthoritativeCathedralEncounterLightState>).ToArray();
        if (lights.Length != 32 || lights.Where((row, index) => row.LightId != index
                || row.OffsetX is < -128 or > 127 || row.OffsetY is < -128 or > 127
                || row.Radius is < 0 or > 15 || row.OldRadius is < 0 or > 15).Any())
            throw new InvalidDataException("All native indexed light slots require valid scalar ranges.");
        var state = new AuthoritativeCathedralThemeRoomState(objects, IntArray(value, "activeObjectIds", 127),
            IntArray(value, "availableObjectIds", 127), N(value, "activeObjectCount"), monsters,
            IntArray(value, "activeMonsterIds", 200), N(value, "activeMonsterCount"), N(value, "monsterTargetCount"),
            Channel(6), Channel(5), items, IntArray(value, "activeItemIds", 127), N(value, "activeItemCount"), Channel(8),
            Flags(value, "uniqueFlags"), Channel(0).Select(number => (ushort)number).ToArray(),
            Channel(2).Select(number => (sbyte)number).ToArray(), Channel(1).Select(Byte).ToArray(),
            IntArray(value, "sol", 1379).Select(Byte).ToArray(), Channel(3).Select(Byte).ToArray(), Channel(7),
            Channel(9).Select(Byte).ToArray(), Channel(10).Select(Byte).ToArray(),
            IntArray(allocator, "activeIds", 32), N(allocator, "activeCount"), lights,
            B(allocator, "updateLighting"), B(allocator, "disableLighting"),
            ReadRecord<AuthoritativeCathedralThemeRoomFlags>(value.GetProperty("flags")),
            Channel(4).Select(Byte).ToArray(), BoolArray(value, "monsterPetrified", 200));
        var origin = new AuthoritativeCathedralPopulationPoint(N(value, "themeX"), N(value, "themeY"));
        var variant = N(value, "themeVar1");
        if (origin.X is < 0 or > 111 || origin.Y is < 0 or > 111 || variant is < 0 or > 23)
            throw new InvalidDataException("Native theme scratch projection is outside its supported width.");
        return new(state, U(value, "randomState"), origin, variant);
    }

    private static T ReadRecord<T>(JsonElement value)
    {
        var fields = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        Properties(value, fields.Select(field => JsonNamingPolicy.CamelCase.ConvertName(field.Name)).ToArray());
        foreach (var field in fields) {
            var raw = value.GetProperty(JsonNamingPolicy.CamelCase.ConvertName(field.Name));
            if (field.PropertyType == typeof(bool) && raw.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new InvalidDataException("Native Boolean projection requires a Boolean token.");
            if (field.PropertyType == typeof(int) || field.PropertyType == typeof(uint) || field.PropertyType == typeof(ushort)) {
                if (field.PropertyType == typeof(uint)) _ = U(value, JsonNamingPolicy.CamelCase.ConvertName(field.Name));
                else _ = Integer(raw);
            }
            if (field.PropertyType == typeof(AuthoritativeCathedralPopulationPoint))
                _ = ReadRecord<AuthoritativeCathedralPopulationPoint>(raw);
        }
        return value.Deserialize<T>(Options) ?? throw new InvalidDataException("Native projection must not be null.");
    }

    private static byte Byte(int value)
        => value is >= 0 and <= 255 ? (byte)value : throw new InvalidDataException("Native byte is outside 0..255.");
    private static int[] IntArray(JsonElement value, string name, int count)
    {
        var result = A(value, name).EnumerateArray().Select(Integer).ToArray();
        if (result.Length != count) throw new InvalidDataException($"{name} requires {count} native values.");
        return result;
    }
    private static bool[] BoolArray(JsonElement value, string name, int count)
    {
        var result = A(value, name).EnumerateArray().Select(row => row.ValueKind switch {
            JsonValueKind.True => true, JsonValueKind.False => false,
            _ => throw new InvalidDataException("Native Boolean arrays reject coercion."),
        }).ToArray();
        if (result.Length != count) throw new InvalidDataException($"{name} requires {count} native values.");
        return result;
    }
    private static void ValidateNames(IEnumerable<string> actual)
    {
        var expected = from depth in Enumerable.Range(1, 4) from name in Names select $"{depth}:{name}";
        if (!actual.SequenceEqual(expected)) throw new InvalidDataException("The exact ordered 88-case matrix is required.");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("float")]
    [InlineData("light")]
    [InlineData("signed")]
    [InlineData("slots")]
    [InlineData("tiles")]
    [InlineData("boolean")]
    [InlineData("pool")]
    [InlineData("nested")]
    [InlineData("enemyWidth")]
    [InlineData("goalWidth")]
    [InlineData("animationWidth")]
    [InlineData("objectWidth")]
    [InlineData("bookMessageWidth")]
    [InlineData("talkMessageWidth")]
    public void IncompleteOrUnsafeWorldEvidenceIsRejected(string mutation)
    {
        var value = PortableWorld();
        switch (mutation) {
        case "missing": value.Remove("themeVar1"); break;
        case "extra": value["invented"] = 0; break;
        case "float": value["tiles"]![0]![0] = 0.5; break;
        case "light": value["tiles"]![0]![9] = 16; break;
        case "signed": value["tiles"]![0]![5] = -129; break;
        case "slots": value["objects"]!.AsArray().RemoveAt(0); break;
        case "tiles": value["tiles"]!.AsArray().RemoveAt(0); break;
        case "boolean": value["monsterPetrified"]![0] = 1; break;
        case "pool": value["activeItemIds"]![1] = 0; break;
        case "nested": value["monsters"]![0]!["tile"]!["extra"] = 0; break;
        case "enemyWidth": value["monsters"]![199]!["enemy"] = 256; break;
        case "goalWidth": value["monsters"]![199]!["goalVar2"] = 128; break;
        case "animationWidth": value["monsters"]![199]!["animCurrentFrame"] = 128; break;
        case "objectWidth": value["objects"]![126]!["breakFlag"] = 128; break;
        case "bookMessageWidth": value["objects"]![126]!["bookMessage"] = 32768; break;
        case "talkMessageWidth": value["monsters"]![199]!["talkMessage"] = -32769; break;
        }
        using var document = JsonDocument.Parse(value.ToJsonString());
        Assert.ThrowsAny<Exception>(() => ReadWorld(document.RootElement));
    }

    [Fact]
    public void CompletePortableWorldPreservesSignedStaleAndInactiveChannels()
    {
        var value = PortableWorld();
        value["tiles"]![0]![5] = -128; value["tiles"]![1]![6] = short.MinValue;
        value["objects"]![126]!["var4"] = -91; value["monsterPetrified"]![199] = true;
        value["objects"]![126]!["bookMessage"] = short.MinValue;
        value["monsters"]![199]!["talkMessage"] = short.MaxValue;
        using var document = JsonDocument.Parse(value.ToJsonString());
        var world = ReadWorld(document.RootElement);
        Assert.Equal(-128, world.State.PlayerRefs[0]);
        Assert.Equal(short.MinValue, world.State.MonsterRefs[1]);
        Assert.Equal(-91, world.State.ObjectSlots[126].Var4);
        Assert.True(world.State.MonsterPetrified[199]);
        Assert.Equal(short.MinValue, world.State.ObjectSlots[126].BookMessage);
        Assert.Equal(short.MaxValue, world.State.MonsterSlots[199].TalkMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    [InlineData(256)]
    public void BestiaryPlaceFlagsRequireNativeByteWidth(int flags)
    {
        var bestiary = new JsonArray();
        foreach (var type in new[] { 109, 0, 8, 20, 34, 42 }) {
            var definition = new AuthoritativeCathedralPopulationMonsterDefinition(type,
                AuthoritativeCathedralThemeRoomGenerator.IsNativeSkeleton(type), 1, 1, 0, 0, 0,
                1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, new int[6], new int[6]);
            var row = JsonSerializer.SerializeToNode(definition, Options)!.AsObject();
            row["placeFlags"] = flags;
            row["isGoat"] = AuthoritativeCathedralThemeRoomGenerator.IsNativeGoat(type);
            bestiary.Add(row);
        }
        var value = new JsonObject {
            ["objects"] = JsonSerializer.SerializeToNode(Enumerable.Range(0, 109).Select(type =>
                new AuthoritativeCathedralPopulationObjectDefinition(type, 0, 0, 0, 1, 0)), Options),
            ["bestiary"] = bestiary,
            ["shrineAvailable"] = JsonSerializer.SerializeToNode(Enumerable.Repeat(true, 26), Options),
        };
        using var document = JsonDocument.Parse(value.ToJsonString());
        if (flags > byte.MaxValue)
            Assert.Throws<InvalidDataException>(() => ReadRoomCatalog(document.RootElement));
        else
            Assert.All(ReadRoomCatalog(document.RootElement).OrderedBestiary,
                row => Assert.Equal((uint)flags, row.PlaceFlags));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("reordered")]
    [InlineData("depth")]
    public void ExactCaseMatrixRejectsReplacementAndMissingCases(string mutation)
    {
        var values = (from depth in Enumerable.Range(1, 4) from name in Names select $"{depth}:{name}").ToList();
        if (mutation == "duplicate") values[^1] = values[^2];
        if (mutation == "missing") values.RemoveAt(0);
        if (mutation == "reordered") (values[0], values[1]) = (values[1], values[0]);
        if (mutation == "depth") values[0] = "5:type-0";
        Assert.Throws<InvalidDataException>(() => ValidateNames(values));
    }

    [Fact]
    public void DuplicateWorldKeysAreRejected()
    {
        var text = PortableWorld().ToJsonString().Insert(1, "\"themeVar1\":0,");
        using var document = JsonDocument.Parse(text);
        Assert.Throws<InvalidDataException>(() => ReadWorld(document.RootElement));
    }

    private static JsonObject PortableWorld()
    {
        JsonNode? Node<T>(T value) => JsonSerializer.SerializeToNode(value, Options);
        var objects = Enumerable.Range(0, 127).Select(slot => new AuthoritativeCathedralPopulationObjectState { Slot = slot }).ToArray();
        var monsters = Enumerable.Range(0, 200).Select(slot => new AuthoritativeCathedralPopulationMonsterState { Slot = slot }).ToArray();
        return new() {
            ["randomState"] = 0, ["activeObjectCount"] = 0,
            ["activeObjectIds"] = Node(new int[127]), ["availableObjectIds"] = Node(Enumerable.Range(0, 127).ToArray()),
            ["objects"] = Node(objects), ["activeMonsterCount"] = 0, ["monsterTargetCount"] = 0,
            ["activeMonsterIds"] = Node(Enumerable.Range(0, 200).ToArray()), ["monsters"] = Node(monsters),
            ["monsterPetrified"] = Node(new bool[200]), ["activeItemCount"] = 0,
            ["activeItemIds"] = Node(Enumerable.Range(0, 127).ToArray()),
            ["items"] = Node(Enumerable.Repeat(new AuthoritativeCathedralInitialItemState(), 128).ToArray()),
            ["uniqueFlags"] = Node(new bool[128]),
            ["tiles"] = Node(Enumerable.Range(0, Cells).Select(_ => new int[11]).ToArray()),
            ["sol"] = Node(new int[1379]), ["themeX"] = 29, ["themeY"] = 31, ["themeVar1"] = 2,
            ["flags"] = Node(new AuthoritativeCathedralThemeRoomFlags()),
            ["lightAllocator"] = new JsonObject {
                ["activeCount"] = 0, ["updateLighting"] = false, ["disableLighting"] = false,
                ["activeIds"] = Node(Enumerable.Range(0, 32).ToArray()),
                ["records"] = Node(Enumerable.Range(0, 32).Select(slot => new AuthoritativeCathedralEncounterLightState(
                    slot, new(0, 0), new(0, 0), 0, 0, 0, 0, false, false)).ToArray()),
            },
        };
    }
}

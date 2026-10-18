using System.Text.Json;
using Devilution.Diablo;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class DiabloCohortGeometryConverterTests
{
    private static readonly JsonSerializerOptions DefaultOptions = new();

    [Fact]
    public void GeometryConverterMatchesDefaultWriterAndReaderForLevelVariants()
    {
        var options = CreateConverterOptions();
        for (uint levelId = 0; levelId <= 4; levelId++) {
            AssertEquivalent(CreateLevel(levelId, effective: false), options);
            AssertEquivalent(CreateLevel(levelId, effective: true), options);
            AssertEquivalent(CreateLevel(levelId, effective: false), options);
        }

        AssertEquivalent(CreateLevel(9, effective: false), options);
        AssertEquivalent(CreateLevel(uint.MaxValue, effective: true), options);
        AssertEquivalent(CreateLevel(4, effective: true), CreateConverterOptions());
    }

    [Fact]
    public void GeometryConverterComparesEachPublicPropertyIndependently()
    {
        var options = CreateConverterOptions();
        var baseline = CreateLevel(2, effective: false);
        var baselineBytes = Serialize(baseline, DefaultOptions);
        AssertEquivalent(baseline, options);

        var mutations = new (string Name, Func<DiabloWorldGeometryLevelState, DiabloWorldGeometryLevelState> Change)[] {
            (nameof(DiabloWorldGeometryLevelState.LevelId), value => value with { LevelId = 3 }),
            (nameof(DiabloWorldGeometryLevelState.StableSymbol), value => value with { StableSymbol = value.StableSymbol + "-changed" }),
            (nameof(DiabloWorldGeometryLevelState.Width), value => value with { Width = value.Width + 1 }),
            (nameof(DiabloWorldGeometryLevelState.Height), value => value with { Height = value.Height + 1 }),
            (nameof(DiabloWorldGeometryLevelState.BlockedCells), value => value with { BlockedCells = [101, 102] }),
            (nameof(DiabloWorldGeometryLevelState.MissileBlockedCells), value => value with { MissileBlockedCells = [103, 104] }),
            (nameof(DiabloWorldGeometryLevelState.NativeTransparency), value => value with { NativeTransparency = [(ushort)105, (ushort)106] }),
            (nameof(DiabloWorldGeometryLevelState.NativeObjectLayer), value => value with { NativeObjectLayer = [(ushort)107, (ushort)108] }),
            (nameof(DiabloWorldGeometryLevelState.NativeMegaTileLayer), value => value with { NativeMegaTileLayer = [(ushort)109, (ushort)110] }),
            (nameof(DiabloWorldGeometryLevelState.NativePieceLayer), value => value with { NativePieceLayer = [(ushort)111, (ushort)112] }),
            (nameof(DiabloWorldGeometryLevelState.NativePieceProperties), value => {
                var properties = value.NativePieceProperties!.ToDictionary(pair => pair.Key, pair => pair.Value);
                properties[7] = AuthoritativeDunTileFlags.Trap;
                return value with { NativePieceProperties = properties };
            }),
            (nameof(DiabloWorldGeometryLevelState.NativePieceCollisionCells), value => value with { NativePieceCollisionCells = [113, 114] }),
            (nameof(DiabloWorldGeometryLevelState.NativeVisionBlockedCells), value => value with { NativeVisionBlockedCells = [115, 116] }),
            (nameof(DiabloWorldGeometryLevelState.HasExplicitNativeVisionBlockedCells), value => value with { HasExplicitNativeVisionBlockedCells = true }),
            (nameof(DiabloWorldGeometryLevelState.NativePieceMissileBlockedCells), value => value with { NativePieceMissileBlockedCells = [(ushort)117, (ushort)118] }),
            (nameof(DiabloWorldGeometryLevelState.NativeMonsterLayer), value => value with { NativeMonsterLayer = [(ushort)119, (ushort)120] }),
            (nameof(DiabloWorldGeometryLevelState.IsDefaultStart), value => value with { IsDefaultStart = false }),
            (nameof(DiabloWorldGeometryLevelState.SuppressMultiplayerDeathDrops), value => value with { SuppressMultiplayerDeathDrops = true }),
            (nameof(DiabloWorldGeometryLevelState.StartPositionX), value => value with { StartPositionX = value.StartPositionX + 1 }),
            (nameof(DiabloWorldGeometryLevelState.StartPositionY), value => value with { StartPositionY = value.StartPositionY + 1 }),
        };

        foreach (var mutation in mutations) {
            var changed = mutation.Change(baseline);
            var changedBytes = Serialize(changed, DefaultOptions);
            Assert.True(!baselineBytes.AsSpan().SequenceEqual(changedBytes),
                $"Changing {mutation.Name} should change the default JSON bytes.");
            AssertEquivalent(changed, options);
            AssertEquivalent(baseline, options);
        }
    }

    [Fact]
    public void GeometryConverterObservesSameReferenceCollectionAndDictionaryMutations()
    {
        var options = CreateConverterOptions();
        var level = CreateMutableLevel();
        AssertEquivalent(level, options);
        var mutateOneCollectionAtATime = new Action[] {
            () => ((int[])level.BlockedCells!)[0] = 90,
            () => ((List<int>)level.MissileBlockedCells!).Add(91),
            () => ((ushort[])level.NativeTransparency!)[0] = 92,
            () => ((List<ushort>)level.NativeObjectLayer!).Add(93),
            () => ((ushort[])level.NativeMegaTileLayer!)[0] = 94,
            () => ((List<ushort>)level.NativePieceLayer!).Add(95),
            () => ((Dictionary<ushort, AuthoritativeDunTileFlags>)level.NativePieceProperties!)[11] = AuthoritativeDunTileFlags.Trap,
            () => ((int[])level.NativePieceCollisionCells!)[0] = 96,
            () => ((List<int>)level.NativeVisionBlockedCells!).Add(97),
            () => ((int[])level.NativePieceMissileBlockedCells!)[0] = 98,
            () => ((List<ushort>)level.NativeMonsterLayer!).Add(99),
        };
        foreach (var mutate in mutateOneCollectionAtATime) {
            mutate();
            AssertEquivalent(level, options);
        }
    }

    [Fact]
    public void GeometryConverterPreservesDictionaryOrderAndNullVersusEmptyCollections()
    {
        var options = CreateConverterOptions();
        var ordered = (Dictionary<ushort, AuthoritativeDunTileFlags>)CreateMutableLevel().NativePieceProperties!;
        var level = CreateMutableLevel() with { NativePieceProperties = ordered };
        AssertEquivalent(level, options);

        var beforeReorder = Serialize(level, DefaultOptions);
        var firstValue = ordered[7];
        var secondValue = ordered[3];
        ordered.Clear();
        ordered.Add(3, secondValue);
        ordered.Add(7, firstValue);
        var afterReorder = Serialize(level, DefaultOptions);
        Assert.NotEqual(Convert.ToHexString(beforeReorder), Convert.ToHexString(afterReorder));
        AssertEquivalent(level, options);

        var nullCollections = CreateLevel(2, effective: false) with {
            BlockedCells = null,
            NativePieceProperties = null,
            NativeMonsterLayer = null,
        };
        var emptyCollections = nullCollections with {
            BlockedCells = [],
            NativePieceProperties = new Dictionary<ushort, AuthoritativeDunTileFlags>(),
            NativeMonsterLayer = [],
        };
        var nullBytes = Serialize(nullCollections, DefaultOptions);
        var emptyBytes = Serialize(emptyCollections, DefaultOptions);
        Assert.NotEqual(Convert.ToHexString(nullBytes), Convert.ToHexString(emptyBytes));
        AssertEquivalent(nullCollections, options);
        AssertEquivalent(emptyCollections, options);
    }

    [Fact]
    public async Task GeometryConverterKeepsConcurrentLevelCallsIndependent()
    {
        var options = CreateConverterOptions();
        var levels = Enumerable.Range(0, 20)
            .Select(index => CreateLevel((uint)(index % 5), effective: index % 2 != 0))
            .ToArray();
        var results = await Task.WhenAll(levels.Select(level => Task.Run(() => Serialize(level, options))));

        for (var index = 0; index < levels.Length; index++) {
            Assert.Equal(Convert.ToHexString(Serialize(levels[index], DefaultOptions)), Convert.ToHexString(results[index]));
            AssertRoundTripsToDefaultValue(results[index]);
        }
    }

    private static JsonSerializerOptions CreateConverterOptions()
    {
        var uncachedOptions = new JsonSerializerOptions(DefaultOptions);
        var options = new JsonSerializerOptions(DefaultOptions);
        options.Converters.Add(new DiabloCohortGeometryConverter(uncachedOptions));
        return options;
    }

    private static void AssertEquivalent(DiabloWorldGeometryLevelState level, JsonSerializerOptions options)
    {
        var expected = Serialize(level, DefaultOptions);
        var actual = Serialize(level, options);
        Assert.Equal(Convert.ToHexString(expected), Convert.ToHexString(actual));
        AssertRoundTripsToDefaultValue(actual);

        if (actual.Length > 0) {
            actual[0] ^= 0x20;
            Assert.Equal(Convert.ToHexString(expected), Convert.ToHexString(Serialize(level, options)));
        }
    }

    private static void AssertRoundTripsToDefaultValue(byte[] encoded)
    {
        var decoded = JsonSerializer.Deserialize<DiabloWorldGeometryLevelState>(encoded, CreateConverterOptions());
        Assert.NotNull(decoded);
        Assert.Equal(Convert.ToHexString(encoded), Convert.ToHexString(Serialize(decoded, DefaultOptions)));
    }

    private static byte[] Serialize(DiabloWorldGeometryLevelState level, JsonSerializerOptions options) =>
        JsonSerializer.SerializeToUtf8Bytes(level, options);

    private static DiabloWorldGeometryLevelState CreateLevel(uint levelId, bool effective)
    {
        var offset = effective ? 100 : 0;
        var flags = effective ? AuthoritativeDunTileFlags.BlockMissile : AuthoritativeDunTileFlags.Solid;
        return new DiabloWorldGeometryLevelState(
            levelId,
            $"native-level-{levelId}-{(effective ? "effective" : "backing")}",
            20 + offset,
            16 + offset,
            [1 + offset, 2 + offset],
            [3 + offset, 4 + offset],
            [(ushort)(5 + offset), (ushort)(6 + offset)],
            [(ushort)(7 + offset), (ushort)(8 + offset)],
            [(ushort)(9 + offset), (ushort)(10 + offset)],
            [(ushort)(11 + offset), (ushort)(12 + offset)],
            new Dictionary<ushort, AuthoritativeDunTileFlags> {
                [(ushort)(7 + offset)] = flags,
                [(ushort)(3 + offset)] = AuthoritativeDunTileFlags.BlockLight,
            },
            [13 + offset, 14 + offset],
            [15 + offset, 16 + offset],
            effective,
            [17 + offset, 18 + offset],
            [(ushort)(19 + offset), (ushort)(20 + offset)],
            !effective,
            effective,
            21 + offset,
            22 + offset);
    }

    private static DiabloWorldGeometryLevelState CreateMutableLevel() => new(
        1,
        "mutable-level",
        20,
        16,
        new[] { 1, 2 },
        new List<int> { 3, 4 },
        new ushort[] { 5, 6 },
        new List<ushort> { 7, 8 },
        new ushort[] { 9, 10 },
        new List<ushort> { 11, 12 },
        new Dictionary<ushort, AuthoritativeDunTileFlags> {
            [7] = AuthoritativeDunTileFlags.Solid,
            [3] = AuthoritativeDunTileFlags.BlockLight,
        },
        new[] { 13, 14 },
        new List<int> { 15, 16 },
        true,
        new[] { 17, 18 },
        new List<ushort> { 19, 20 },
        false,
        true,
        21,
        22);
}

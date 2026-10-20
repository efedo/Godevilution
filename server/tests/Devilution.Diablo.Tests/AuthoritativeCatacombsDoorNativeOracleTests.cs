using System.Text.Json;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Actual native operations over controlled SOL/occupancy inputs; floor ownership is a separate gate. */
public sealed class AuthoritativeCatacombsDoorNativeOracleTests
{
    private static readonly Lazy<JsonElement> Corpus = new(LoadCorpus);
    public static TheoryData<uint, int, int> Cases {
        get {
            var result = new TheoryData<uint, int, int>();
            for (uint depth = 5; depth <= 8; depth++)
                foreach (var type in new[] { 42, 43 })
                    for (var occupancy = 0; occupancy < 4; occupancy++) result.Add(depth, type, occupancy);
            return result;
        }
    }

    [Theory(Explicit = true)]
    [MemberData(nameof(Cases))]
    public void NativeL2DoorOperationsMatchFramesGeometryAndBlockedClose(uint depth, int type, int occupancy)
    {
        var row = Corpus.Value.GetProperty("cases").EnumerateArray().Single(value =>
            N(value, "depth") == depth && N(value, "nativeType") == type && N(value, "occupancy") == occupancy);
        var catalog = AuthoritativeCatacombsDoorCatalog.Load(
            AuthoritativeCatacombsInitialPopulationNativeOracleTests.QualifiedInputs().Metadata);
        var initial = row.GetProperty("initialObject");
        var states = row.GetProperty("states").EnumerateArray().ToArray();
        Assert.Equal(13, states.Length);
        var properties = Enumerable.Range(0, AuthoritativeDunTilePropertyCatalog.NativeTileCount)
            .ToDictionary(id => (ushort)id, _ => AuthoritativeDunTileFlags.None);
        properties[537] = properties[539] = AuthoritativeDunTileFlags.Solid
            | AuthoritativeDunTileFlags.BlockLight | AuthoritativeDunTileFlags.BlockMissile;
        var pieces = new ushort[112 * 112];
        var special = new ushort[pieces.Length];
        var transparency = new ushort[pieces.Length];
        var cells = states[0].GetProperty("cells");
        for (var index = 0; index < 25; index++) {
            var cell = (48 + index / 5) * 112 + 48 + index % 5;
            pieces[cell] = cells[index][0].GetUInt16();
            special[cell] = cells[index][1].GetUInt16();
            transparency[cell] = cells[index][2].GetUInt16();
        }
        const int center = 50 * 112 + 50;
        var level = new AuthoritativeLevel(700, 112, 112, blockedCells: [center], missileBlockedCells: [center],
            nativePieceLayer: pieces, nativeSpecialLayer: special, nativeTransparency: transparency,
            nativePieceProperties: properties, nativePieceCollisionCells: [center],
            nativePieceMissileBlockedCells: [center], nativeVisionBlockedCells: [center]);
        var raw = new AuthoritativeCathedralPopulationObjectState {
            Slot = N(initial, "slot"), Type = type, Position = new(50, 50),
            AnimDelay = N(initial, "animDelay"), AnimLength = initial.GetProperty("animLength").GetUInt32(),
            AnimFrame = initial.GetProperty("animFrame").GetUInt32(), AnimWidth = initial.GetProperty("animWidth").GetUInt16(),
            SelectionRegion = N(initial, "selectionRegion"), DoorFlag = B(initial, "doorFlag"),
            TrapFlag = B(initial, "trapFlag"), SolidFlag = B(initial, "solidFlag"),
            MissilePassable = B(initial, "missilePassable"), PreFlag = B(initial, "preFlag"), Var4 = N(initial, "var4"),
        };
        var entity = new AuthoritativeWorldObject(900, 1, 700, 50, 50,
            nativeObjectId: (uint)type, animationFrame: (int)raw.AnimFrame);
        var door = AuthoritativeCatacombsDoors.Bind(level, depth, catalog, raw, entity);
        var random = states[0].GetProperty("random").GetUInt32();
        foreach (var expected in states) {
            var operation = expected.GetProperty("operation").GetString();
            var transition = operation switch {
                "initial" => new AuthoritativeCatacombsDoorTransition(level, door),
                "occupied-update" or "occupied-update-after-sync" => new(level,
                    AuthoritativeCatacombsDoors.UpdateOccupancy(door, occupied: true)),
                "clear-update" => new(level, AuthoritativeCatacombsDoors.UpdateOccupancy(door, occupied: false)),
                "occupied-sync" or "open-sync" or "closed-sync" => AuthoritativeCatacombsDoors.Synchronize(level, depth, catalog, door),
                "delta-open" or "repeat-delta-open" => AuthoritativeCatacombsDoors.DeltaOpen(level, depth, catalog, door),
                "open" or "blocked-close" or "close" or "close-after-delta" =>
                    AuthoritativeCatacombsDoors.Operate(level, depth, catalog, door, occupied: operation == "blocked-close"),
                _ => throw new InvalidDataException("Unknown native door operation."),
            };
            level = transition.Level;
            door = transition.Door;
            var expectedObject = expected.GetProperty("object");
            Assert.Equal(N(expectedObject, "var4"), (int)door.Status);
            Assert.Equal(N(expectedObject, "animFrame"), door.AnimationFrame);
            Assert.Equal(N(expectedObject, "selectionRegion"), door.SelectionRegion);
            Assert.Equal(B(expectedObject, "preFlag"), door.PreFlag);
            Assert.Equal(B(expectedObject, "missilePassable"), door.MissFlag);
            Assert.Equal(B(expectedObject, "solidFlag"), door.SolidFlag);
            Assert.Equal(random, expected.GetProperty("random").GetUInt32());
            var expectedCells = expected.GetProperty("cells");
            for (var index = 0; index < 25; index++) {
                var cell = (48 + index / 5) * 112 + 48 + index % 5;
                Assert.Equal(expectedCells[index][0].GetUInt16(), level.NativePieceLayer[cell]);
                Assert.Equal(expectedCells[index][1].GetUInt16(), level.NativeSpecialLayer[cell]);
                Assert.Equal(expectedCells[index][2].GetUInt16(), level.NativeTransparency[cell]);
                var flags = (AuthoritativeDunTileFlags)expectedCells[index][3].GetByte();
                Assert.Equal(flags.HasFlag(AuthoritativeDunTileFlags.Solid), level.BlockedCells.Contains(cell));
                Assert.Equal(flags.HasFlag(AuthoritativeDunTileFlags.BlockMissile), level.MissileBlockedCells.Contains(cell));
                Assert.Equal((flags & (AuthoritativeDunTileFlags.Solid | AuthoritativeDunTileFlags.BlockLight)) != 0,
                    level.NativeVisionBlockedCells.Contains(cell));
            }
        }
    }

    private static JsonElement LoadCorpus()
    {
        var directory = Environment.GetEnvironmentVariable("GODEVILUTION_CATACOMBS_DOOR_ORACLE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            throw new InvalidDataException("An explicit native L2 door reference directory is required.");
        directory = Path.GetFullPath(directory);
        Assert.Equal(new[] { "native-catacombs-door-operations.json" },
            AuthoritativeCathedralEncounterNativeOracleTests.RegularEntries(directory).Select(Path.GetFileName));
        var result = AuthoritativeCathedralEncounterNativeOracleTests.ParseFile(
            Path.Combine(directory, "native-catacombs-door-operations.json"), 2 * 1024 * 1024);
        Assert.Equal("godevilution-catacombs-door-operations", result.GetProperty("format").GetString());
        Assert.Equal("controlled-original-diablo-catacombs-l2-door-sol-and-occupancy", result.GetProperty("policy").GetString());
        Assert.Equal(1, N(result, "version"));
        Assert.Equal(32, N(result, "caseCount"));
        Assert.Equal(32, result.GetProperty("cases").GetArrayLength());
        return result;
    }

    private static int N(JsonElement value, string key) => value.GetProperty(key).GetInt32();
    private static bool B(JsonElement value, string key) => value.GetProperty(key).GetBoolean();
}

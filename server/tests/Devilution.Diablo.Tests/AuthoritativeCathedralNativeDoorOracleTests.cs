using System.Text.Json;
using System.Text.Json.Serialization;
using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

/**
 * Explicit managed/native comparison for the Cathedral AddL1Objs door path.
 *
 * The native corpus is intentionally kept outside the repository because its
 * geometry is captured from licensed local assets. The managed comparison owns
 * the d-piece, special, transparency, SOL, collision, and independently derived
 * item-occupancy channels. Native itemBlocking remains only in native artifact
 * reconstruction checks.
 */
public sealed class AuthoritativeCathedralNativeDoorOracleTests
{
    private const string OracleDirectoryVariable = "GODEVILUTION_CATHEDRAL_DOOR_ORACLE_DIRECTORY";
    private const string OracleFormat = "godevilution-cathedral-l1-door-oracle";
    private const string OraclePolicy = "native-add-l1-objects-door-only";
    private const string OracleRowMajor = "y*nativeWidth+x";
    private const int OracleVersion = 1;
    private const int NativeWidth = 112;
    private const int NativeHeight = 112;
    private const int NativeCellCount = NativeWidth * NativeHeight;
    private const byte NativeOpaqueSolBit = 0x40;
    private const byte SupportedSolMask = 0xBF;

    public static TheoryData<string, int, uint, string, bool> DoorOracleCases
    {
        get {
            var cases = new TheoryData<string, int, uint, string, bool>();
            foreach (var depth in Enumerable.Range(1, 4)) {
                foreach (var seed in new uint[] { 2588, 743271966 }) {
                    foreach (var entry in new[] { "main", "previous" }) {
                        cases.Add(
                            $"cathedral-door-depth-{depth}-{seed}-{entry}.json",
                            depth,
                            seed,
                            entry,
                            false);
                    }
                }
            }
            cases.Add(
                "cathedral-door-delta-depth-2-743271966-main.json",
                2,
                743271966,
                "main",
                true);
            return cases;
        }
    }

    [Theory(Explicit = true)]
    [MemberData(nameof(DoorOracleCases))]
    public void NativeCathedralDoorOracleMatchesManagedDiscoveryAndOperations(
        string snapshotFileName,
        int depth,
        uint seed,
        string entryName,
        bool deltaOpen)
    {
        var root = RequireOracleDirectory();
        var snapshot = ReadSnapshot(root, snapshotFileName);
        AssertCaseIdentity(snapshot, snapshotFileName, depth, seed, entryName, deltaOpen);

        var rawTil = ReadRequiredFile(root, "l1.til");
        Assert.Equal(206 * sizeof(ushort) * 4, rawTil.Length);
        var rawSol = ReadRequiredFile(root, "l1.sol");
        Assert.InRange(rawSol.Length, 1, AuthoritativeDunTilePropertyCatalog.NativeTileCount);
        var rawSolBeforeNormalization = rawSol.ToArray();
        var paddedSol = AuthoritativeCathedralNativeGeometryOracleTests.PreparePaddedSol(rawSol);
        var comparisonSol = AuthoritativeCathedralNativeGeometryOracleTests.StripNativeOpaqueSolBit(paddedSol);

        // 0x40 is a native opaque-byte channel, not a managed SOL flag. Keep
        // the raw bytes intact and compare managed semantics only after masking
        // this boundary bit.
        Equal(rawSolBeforeNormalization.Length, rawSol.Length, "raw l1.sol length");
        for (var pieceId = 0; pieceId < rawSol.Length; pieceId++)
            Equal(rawSolBeforeNormalization[pieceId], rawSol[pieceId], $"raw l1.sol byte {pieceId}");
        Assert.Equal(NativeOpaqueSolBit, (byte)(paddedSol[170] & NativeOpaqueSolBit));
        Assert.Equal((byte)0, (byte)(comparisonSol[170] & NativeOpaqueSolBit));
        Assert.Equal((byte)(paddedSol[170] & SupportedSolMask), comparisonSol[170]);

        var pieces = AuthoritativeDunPieceCatalog.LoadTil("oracle:l1.til", rawTil);
        var properties = AuthoritativeDunTilePropertyCatalog.LoadSol(
            "oracle:l1.sol (zero-extended and opaque-bit stripped for comparison)",
            comparisonSol);

        var entry = ParseEntry(entryName);
        var generation = AuthoritativeCathedralDepthGenerator.Generate(depth, seed, entry);
        Assert.Equal(depth, generation.Depth);
        Assert.Equal(AuthoritativeCathedralDepthGenerationPolicy.QuestDisabledOriginalDiabloStairs, generation.Policy);
        Assert.Equal(seed, snapshot.InputSeed);
        Assert.Equal(generation.AttemptSeed, snapshot.AttemptSeed);
        Assert.Equal(generation.FinalRandomState, snapshot.FinalRandomState);
        Assert.Equal(snapshot.FinalRandomState, snapshot.DoorScanRandomStateBefore);

        var managedBaselines = new ManagedBaseline[snapshot.ClosedBaselines.Length];
        Assert.Equal(snapshot.BaselineRebuildCount, snapshot.ClosedBaselines.Length);
        for (var index = 0; index < managedBaselines.Length; index++) {
            var baseline = snapshot.ClosedBaselines[index];
            Assert.Equal(index + 1, baseline.Ordinal);
            AssertLayerShape(baseline.Layer, $"{snapshotFileName}.closedBaselines[{index}]");
            Equal(snapshot.ClosedBaselineHashes.FullLayer, baseline.Hashes.FullLayer,
                $"{snapshotFileName} baseline {baseline.Ordinal} full-layer hash");
            AssertLayerHashes(baseline.Layer, baseline.Hashes, $"{snapshotFileName}.closedBaselines[{index}]");
            if (index == 0) {
                AssertLayerEqual(snapshot.ClosedBaseline, baseline.Layer,
                    $"{snapshotFileName}.closedBaseline vs first baseline");
                AssertLayerHashes(snapshot.ClosedBaseline, snapshot.ClosedBaselineHashes,
                    $"{snapshotFileName}.closedBaseline");
                Assert.Equal(snapshot.DoorScanRandomStateBefore, baseline.DoorScanRandomStateBefore);
                Assert.Equal(snapshot.DoorScanRandomStateAfter, baseline.DoorScanRandomStateAfter);
            }

            managedBaselines[index] = BuildManagedBaseline(
                depth,
                seed,
                entry,
                pieces,
                properties);
            AssertManagedLayerMatchesNative(
                managedBaselines[index],
                baseline.Layer,
                $"{snapshotFileName}.closedBaselines[{index}]");
            // This recomputes the native artifact hash only; managed parity is asserted above.
            Equal(HashLayer(ManagedLayer.FromOracle(baseline.Layer)), baseline.Hashes.FullLayer,
                $"{snapshotFileName}.closedBaselines[{index}] full-layer hash");
        }

        Assert.NotEmpty(snapshot.Doors);
        for (var ordinal = 0; ordinal < snapshot.Doors.Length; ordinal++) {
            var expectedDoor = snapshot.Doors[ordinal];
            Assert.InRange(expectedDoor.FreshBaselineOrdinal, 1, managedBaselines.Length);
            var baselineIndex = expectedDoor.FreshBaselineOrdinal - 1;
            var managedBaseline = managedBaselines[baselineIndex];
            var discoveredDoors = managedBaseline.Discovery.Doors;
            Assert.Equal(snapshot.Doors.Length, discoveredDoors.Count);
            Assert.Equal(ordinal, expectedDoor.Ordinal);
            Assert.Equal(deltaOpen ? "delta" : "sync", expectedDoor.OpenPath);

            var managedDoor = discoveredDoors[ordinal];
            AssertDoorIdentity(expectedDoor, managedDoor, managedBaseline.Discovery.Level, snapshotFileName);
            AssertDoorObject(expectedDoor.ClosedObject, managedDoor, $"{snapshotFileName}.doors[{ordinal}].closedObject");

            var baselineLayer = LayerState.FromOracle(snapshot.ClosedBaselines[baselineIndex].Layer);
            var expectedOpenLayer = baselineLayer.Clone();
            var expectedReclosedLayer = baselineLayer.Clone();
            ApplyChangedCells(expectedDoor, expectedOpenLayer, expectedReclosedLayer,
                snapshotFileName, ordinal, snapshot.ClosedBaselines[baselineIndex].Layer);

            var open = deltaOpen
                ? AuthoritativeCathedralDoors.DeltaOpen(managedBaseline.Discovery.Level, managedDoor)
                : AuthoritativeCathedralDoors.Operate(managedBaseline.Discovery.Level, managedDoor, occupied: false);
            var reclosed = AuthoritativeCathedralDoors.Operate(open.Level, open.Door, occupied: false);
            AssertDoorObject(expectedDoor.OpenObject, open.Door, $"{snapshotFileName}.doors[{ordinal}].openObject");
            AssertDoorObject(expectedDoor.ReclosedObject, reclosed.Door,
                $"{snapshotFileName}.doors[{ordinal}].reclosedObject");

            var openDoors = managedBaseline.Discovery.Doors.ToArray();
            openDoors[ordinal] = open.Door;
            var reclosedDoors = openDoors.ToArray();
            reclosedDoors[ordinal] = reclosed.Door;
            var managedOpenLayer = ManagedLayer.FromLevel(open.Level, openDoors);
            var managedReclosedLayer = ManagedLayer.FromLevel(reclosed.Level, reclosedDoors);
            AssertManagedLayerEqual(expectedOpenLayer, managedOpenLayer,
                $"{snapshotFileName}.doors[{ordinal}].open");
            AssertManagedLayerEqual(expectedReclosedLayer, managedReclosedLayer,
                $"{snapshotFileName}.doors[{ordinal}].reclosed");
            AssertLevelCollisionProjection(open.Level, expectedOpenLayer,
                $"{snapshotFileName}.doors[{ordinal}].open");
            AssertLevelCollisionProjection(reclosed.Level, expectedReclosedLayer,
                $"{snapshotFileName}.doors[{ordinal}].reclosed");
            Equal(HashLayer(managedOpenLayer), expectedDoor.OpenLayerHash,
                $"{snapshotFileName}.doors[{ordinal}].openLayerHash");
            Equal(HashLayer(managedReclosedLayer), expectedDoor.ReclosedLayerHash,
                $"{snapshotFileName}.doors[{ordinal}].reclosedLayerHash");

            var reclosedEqualsClosed = LayersEqual(baselineLayer, expectedReclosedLayer);
            Assert.Equal(reclosedEqualsClosed, expectedDoor.ReclosedLayerEqualsClosed);
            Equal(ObjectsEqual(expectedDoor.ClosedObject, expectedDoor.ReclosedObject),
                expectedDoor.ReclosedObjectEqualsClosed);
            Equal(expectedDoor.ReclosedLayerEqualsClosed,
                LayersEqual(managedBaseline.Layer, managedReclosedLayer));
            Equal(expectedDoor.ReclosedObjectEqualsClosed,
                managedDoor == reclosed.Door);

            Equal(expectedDoor.ClosedLayerHash,
                snapshot.ClosedBaselines[baselineIndex].Hashes.FullLayer,
                $"{snapshotFileName}.doors[{ordinal}].closedLayerHash");
            Equal(HashLayer(managedBaseline.Layer), expectedDoor.ClosedLayerHash,
                $"{snapshotFileName}.doors[{ordinal}].closedLayerHash managed");
            AssertChangedCellUnion(expectedDoor, baselineLayer, expectedOpenLayer, expectedReclosedLayer,
                snapshotFileName, ordinal);
        }
    }

    private static string RequireOracleDirectory()
    {
        var root = Environment.GetEnvironmentVariable(OracleDirectoryVariable);
        Assert.False(string.IsNullOrWhiteSpace(root),
            $"{OracleDirectoryVariable} must point to the native Cathedral door oracle directory.");
        Assert.True(Directory.Exists(root), $"Native Cathedral door oracle directory does not exist: {root}");
        return root!;
    }

    private static ManagedBaseline BuildManagedBaseline(
        int depth,
        uint seed,
        AuthoritativeCathedralEntry entry,
        AuthoritativeDunPieceCatalog pieces,
        AuthoritativeDunTilePropertyCatalog properties)
    {
        var generation = AuthoritativeCathedralDepthGenerator.Generate(depth, seed, entry);
        var geometry = AuthoritativeCathedralDepthGeometryMaterializer.Materialize(
            checked((uint)depth),
            generation,
            pieces,
            properties,
            $"cathedral_depth_{depth}",
            isDefaultStart: false);
        var discovery = AuthoritativeCathedralDoors.Discover(geometry.Level);
        return new ManagedBaseline(discovery, ManagedLayer.FromLevel(discovery.Level, discovery.Doors));
    }

    private static void AssertCaseIdentity(
        DoorOracleCase snapshot,
        string fileName,
        int depth,
        uint seed,
        string entry,
        bool deltaOpen)
    {
        Assert.Equal(OracleFormat, snapshot.Format);
        Assert.Equal(OracleVersion, snapshot.Version);
        Assert.Equal(OraclePolicy, snapshot.Policy);
        Assert.Equal(depth, snapshot.Level);
        Assert.Equal(seed, snapshot.InputSeed);
        Assert.Equal(entry, snapshot.Entry);
        Assert.Equal(NativeWidth, snapshot.NativeWidth);
        Assert.Equal(NativeHeight, snapshot.NativeHeight);
        Assert.Equal(OracleRowMajor, snapshot.RowMajor);
        Assert.NotEmpty(snapshot.Doors);
        Assert.Equal(deltaOpen ? "delta" : "sync", snapshot.Doors.First().OpenPath);
        Assert.Equal(1, snapshot.BaselineRebuildCount);
        AssertLayerShape(snapshot.ClosedBaseline, $"{fileName}.closedBaseline");
        Assert.EndsWith(".json", fileName, StringComparison.Ordinal);
    }

    private static void AssertLayerShape(DoorOracleLayer layer, string context)
    {
        Equal(NativeCellCount, layer.Pieces.Length, $"{context}.pieces length");
        Equal(NativeCellCount, layer.Special.Length, $"{context}.special length");
        Equal(NativeCellCount, layer.Transparency.Length, $"{context}.transparency length");
        Equal(NativeCellCount, layer.Properties.Length, $"{context}.properties length");
        Equal(NativeCellCount, layer.Solid.Length, $"{context}.solid length");
        Equal(NativeCellCount, layer.MissileBlocked.Length, $"{context}.missileBlocked length");
        Equal(NativeCellCount, layer.BlockLight.Length, $"{context}.blockLight length");
        Equal(NativeCellCount, layer.Walkable.Length, $"{context}.walkable length");
        Equal(NativeCellCount, layer.ItemBlocking.Length, $"{context}.itemBlocking length");
    }

    private static AuthoritativeCathedralEntry ParseEntry(string entry)
        => entry switch {
            "main" => AuthoritativeCathedralEntry.Main,
            "previous" => AuthoritativeCathedralEntry.Previous,
            _ => throw new InvalidDataException($"Unknown Cathedral entry '{entry}'."),
        };

    private static byte[] ReadRequiredFile(string root, string fileName)
    {
        var path = Path.Combine(root, fileName);
        Assert.True(File.Exists(path), $"Native Cathedral door oracle file is missing: {fileName}");
        var bytes = File.ReadAllBytes(path);
        Assert.NotEmpty(bytes);
        return bytes;
    }

    private static DoorOracleCase ReadSnapshot(string root, string fileName)
    {
        var path = Path.Combine(root, fileName);
        Assert.True(File.Exists(path), $"Native Cathedral door oracle snapshot is missing: {fileName}");
        var snapshot = JsonSerializer.Deserialize<DoorOracleCase>(
            File.ReadAllText(path),
            new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true,
                Converters = { new NumericByteArrayConverter() },
            });
        Assert.NotNull(snapshot);
        return snapshot!;
    }

    private static void AssertDoorIdentity(
        DoorOracleDoor expected,
        AuthoritativeCathedralDoorState actual,
        AuthoritativeLevel level,
        string fileName)
    {
        var nativeTypeId = actual.Kind == AuthoritativeCathedralDoorKind.Left ? 1 : 2;
        var nativeType = actual.Kind == AuthoritativeCathedralDoorKind.Left ? "OBJ_L1LDOOR" : "OBJ_L1RDOOR";
        Assert.Equal(nativeTypeId, expected.NativeTypeId);
        Assert.Equal(nativeType, expected.NativeType);
        Assert.Equal(2, expected.Position.Length);
        Assert.Equal(expected.Position[0], actual.PositionX);
        Assert.Equal(expected.Position[1], actual.PositionY);
        Assert.Equal(expected.Var1OneBased, actual.Var1);
        Assert.Equal(expected.Var2OneBased, actual.Var2);
        Assert.Equal(expected.SourcePieceObserved, actual.Var1 - 1);
        Assert.Equal(expected.NeighborPieceObserved, actual.Var2 - 1);
        var cell = actual.PositionY * level.Width + actual.PositionX;
        Equal(expected.SourcePieceObserved, (int)level.NativePieceLayer[cell], $"{fileName} source piece");
        var neighbor = actual.Kind == AuthoritativeCathedralDoorKind.Left
            ? (actual.PositionX, actual.PositionY - 1)
            : (actual.PositionX - 1, actual.PositionY);
        Equal(expected.NeighborPieceObserved,
            (int)level.NativePieceLayer[neighbor.Item2 * level.Width + neighbor.Item1],
            $"{fileName} neighbor piece");
    }

    private static void AssertDoorObject(
        DoorOracleObjectState expected,
        AuthoritativeCathedralDoorState actual,
        string context)
    {
        Equal((int)actual.Status, expected.State, context);
        Equal(actual.Var1, expected.Var1OneBased, context);
        Equal(actual.Var2, expected.Var2OneBased, context);
        Equal(actual.AnimationFrame, expected.AnimationFrame, context);
        Equal(actual.SelectionRegion, expected.SelectionRegion, context);
        Assert.True(expected.DoorFlag, $"{context}.doorFlag");
        Equal(actual.PreFlag, expected.PreFlag, context);
        Equal(actual.MissFlag, expected.MissFlag, context);
        Equal(actual.SolidFlag, expected.SolidFlag, context);
    }

    private static void ApplyChangedCells(
        DoorOracleDoor door,
        LayerState open,
        LayerState reclosed,
        string fileName,
        int ordinal,
        DoorOracleLayer closedBaseline)
    {
        var lastIndex = -1;
        foreach (var changed in door.ChangedCells) {
            Assert.InRange(changed.Index, 0, NativeCellCount - 1);
            Assert.Equal(changed.Index % NativeWidth, changed.X);
            Assert.Equal(changed.Index / NativeWidth, changed.Y);
            Assert.True(changed.Index > lastIndex,
                $"{fileName}.doors[{ordinal}] changed-cell indices are not strictly ordered.");
            lastIndex = changed.Index;
            AssertTileEqual(ReadTile(closedBaseline, changed.Index), changed.Closed,
                $"{fileName}.doors[{ordinal}].changedCells[{changed.Index}].closed baseline");
            open.Apply(changed.Index, changed.Open);
            reclosed.Apply(changed.Index, changed.Reclosed);
            Assert.True(!TilesEqual(changed.Closed, changed.Open) || !TilesEqual(changed.Closed, changed.Reclosed),
                $"{fileName}.doors[{ordinal}] contains an unchanged changed-cell record.");
        }
        Assert.NotEmpty(door.ChangedCells);
    }

    private static void AssertChangedCellUnion(
        DoorOracleDoor door,
        LayerState closed,
        LayerState open,
        LayerState reclosed,
        string fileName,
        int ordinal)
    {
        var expectedIndexes = new List<int>();
        for (var index = 0; index < NativeCellCount; index++) {
            if (!TilesEqual(closed.ToTile(index), open.ToTile(index))
                || !TilesEqual(closed.ToTile(index), reclosed.ToTile(index))) {
                expectedIndexes.Add(index);
            }
        }
        Equal(expectedIndexes.Count, door.ChangedCells.Length,
            $"{fileName}.doors[{ordinal}] changed-cell union count");
        for (var index = 0; index < expectedIndexes.Count; index++)
            Equal(expectedIndexes[index], door.ChangedCells[index].Index,
                $"{fileName}.doors[{ordinal}] changed-cell union ordinal {index}");
    }

    private static void AssertLayerHashes(
        DoorOracleLayer layer,
        DoorOracleLayerHashes hashes,
        string context)
    {
        var value = LayerState.FromOracle(layer);
        Equal(hashes.FullLayer, HashLayer(value), $"{context}.fullLayer");
        Equal(hashes.Pieces, HashUShorts(value.Pieces), $"{context}.pieces");
        Equal(hashes.Special, HashBytes(value.Special), $"{context}.special");
        Equal(hashes.Transparency, HashBytes(value.Transparency), $"{context}.transparency");
        Equal(hashes.Properties, HashBytes(value.Properties), $"{context}.properties");
        Equal(hashes.Solid, HashBytes(value.Solid), $"{context}.solid");
        Equal(hashes.MissileBlocked, HashBytes(value.MissileBlocked), $"{context}.missileBlocked");
        Equal(hashes.BlockLight, HashBytes(value.BlockLight), $"{context}.blockLight");
        Equal(hashes.Walkable, HashBytes(value.Walkable), $"{context}.walkable");
        Equal(hashes.ItemBlocking, HashBytes(value.ItemBlocking), $"{context}.itemBlocking");
    }

    private static void AssertLayerEqual(DoorOracleLayer left, DoorOracleLayer right, string context)
    {
        var expected = LayerState.FromOracle(left);
        var actual = LayerState.FromOracle(right);
        AssertManagedLayerEqual(expected, actual, context);
    }

    private static void AssertManagedLayerMatchesNative(
        ManagedBaseline managedBaseline,
        DoorOracleLayer native,
        string context)
    {
        var managedLevel = managedBaseline.Discovery.Level;
        var actual = managedBaseline.Layer;
        var expected = LayerState.FromOracle(native);
        AssertManagedLayerEqual(expected, actual, context);
        Equal(expected.Solid.Count(value => value != 0), managedLevel.BlockedCells.Count,
            $"{context}.solid collision count");
        Equal(expected.MissileBlocked.Count(value => value != 0), managedLevel.MissileBlockedCells.Count,
            $"{context}.missile collision count");
        AssertSequenceEqual(
            expected.Solid.Select((value, index) => value != 0 ? index : -1).Where(index => index >= 0),
            managedLevel.BlockedCells,
            $"{context}.blockedCells");
        AssertSequenceEqual(
            expected.MissileBlocked.Select((value, index) => value != 0 ? index : -1).Where(index => index >= 0),
            managedLevel.MissileBlockedCells,
            $"{context}.missileBlockedCells");
        AssertLevelCollisionProjection(managedLevel, expected, context);
    }

    private static void AssertLevelCollisionProjection(
        AuthoritativeLevel managedLevel,
        ManagedLayer expected,
        string context)
    {
        var solidCells = expected.Solid
            .Select((value, index) => value != 0 ? index : -1)
            .Where(index => index >= 0);
        var missileCells = expected.MissileBlocked
            .Select((value, index) => value != 0 ? index : -1)
            .Where(index => index >= 0);
        AssertSequenceEqual(solidCells, managedLevel.NativePieceCollisionCells,
            $"{context}.nativePieceCollisionCells");
        AssertSequenceEqual(missileCells, managedLevel.NativePieceMissileBlockedCells,
            $"{context}.nativePieceMissileBlockedCells");

        // The shared protocol projection conservatively unions movement-solid
        // cells into vision. This is not a claim that it equals native physical
        // BlockLight; the SOL property channel above is compared separately.
        var conservativeVision = expected.Solid
            .Select((value, index) => (value, index))
            .Concat(expected.BlockLight.Select((value, index) => (value, index)))
            .Where(value => value.value != 0)
            .Select(value => value.index)
            .Distinct()
            .OrderBy(index => index);
        AssertSequenceEqual(conservativeVision, managedLevel.NativeVisionBlockedCells,
            $"{context}.conservativeVision");
        Assert.True(managedLevel.HasExplicitNativeVisionBlockedCells, $"{context}.explicitVision");
    }

    private static void AssertManagedLayerEqual(ManagedLayer expected, ManagedLayer actual, string context)
    {
        AssertUShortArray(expected.Pieces, actual.Pieces, $"{context}.pieces");
        AssertByteArray(expected.Special, actual.Special, $"{context}.special");
        AssertByteArray(expected.Transparency, actual.Transparency, $"{context}.transparency");
        AssertByteArray(expected.Properties, actual.Properties, $"{context}.properties");
        AssertByteArray(expected.Solid, actual.Solid, $"{context}.solid");
        AssertByteArray(expected.MissileBlocked, actual.MissileBlocked, $"{context}.missileBlocked");
        AssertByteArray(expected.BlockLight, actual.BlockLight, $"{context}.blockLight");
        AssertByteArray(expected.Walkable, actual.Walkable, $"{context}.walkable");
        AssertByteArray(expected.ItemBlocking, actual.ItemBlocking, $"{context}.itemBlocking");
    }

    private static void AssertTileEqual(DoorTile expected, DoorTile actual, string context)
    {
        Equal(expected.Piece, actual.Piece, context);
        Equal(expected.Special, actual.Special, context);
        Equal(expected.Transparency, actual.Transparency, context);
        Equal(expected.Properties, actual.Properties, context);
        Equal(expected.Solid, actual.Solid, context);
        Equal(expected.MissileBlocked, actual.MissileBlocked, context);
        Equal(expected.BlockLight, actual.BlockLight, context);
        Equal(expected.Walkable, actual.Walkable, context);
        Equal(expected.ItemBlocking, actual.ItemBlocking, context);
    }

    private static DoorTile ReadTile(DoorOracleLayer layer, int index)
        => new(
            layer.Pieces[index],
            layer.Special[index],
            layer.Transparency[index],
            layer.Properties[index],
            layer.Solid[index],
            layer.MissileBlocked[index],
            layer.BlockLight[index],
            layer.Walkable[index],
            layer.ItemBlocking[index]);

    private static bool TilesEqual(DoorTile left, DoorTile right)
        => left.Equals(right);

    private static bool LayersEqual(ManagedLayer left, ManagedLayer right)
        => left.Pieces.SequenceEqual(right.Pieces)
            && left.Special.SequenceEqual(right.Special)
            && left.Transparency.SequenceEqual(right.Transparency)
            && left.Properties.SequenceEqual(right.Properties)
            && left.Solid.SequenceEqual(right.Solid)
            && left.MissileBlocked.SequenceEqual(right.MissileBlocked)
            && left.BlockLight.SequenceEqual(right.BlockLight)
            && left.Walkable.SequenceEqual(right.Walkable)
            && left.ItemBlocking.SequenceEqual(right.ItemBlocking);

    private static bool ObjectsEqual(DoorOracleObjectState left, DoorOracleObjectState right)
        => left.State == right.State
            && left.Var1OneBased == right.Var1OneBased
            && left.Var2OneBased == right.Var2OneBased
            && left.AnimationFrame == right.AnimationFrame
            && left.SelectionRegion == right.SelectionRegion
            && left.DoorFlag == right.DoorFlag
            && left.PreFlag == right.PreFlag
            && left.MissFlag == right.MissFlag
            && left.SolidFlag == right.SolidFlag;

    private static void Equal<T>(T expected, T actual, string context = "Native door values differ.")
    {
        var equal = expected is not string && expected is System.Collections.IEnumerable left
            && actual is System.Collections.IEnumerable right
                ? left.Cast<object?>().SequenceEqual(right.Cast<object?>())
                : EqualityComparer<T>.Default.Equals(expected, actual);
        Assert.True(equal, context);
    }

    private static void AssertUShortArray(IReadOnlyList<ushort> expected, IReadOnlyList<ushort> actual, string context)
    {
        Equal(expected.Count, actual.Count, context);
        for (var index = 0; index < expected.Count; index++)
            Equal(expected[index], actual[index], $"{context}[{index}]");
    }

    private static void AssertByteArray(IReadOnlyList<byte> expected, IReadOnlyList<byte> actual, string context)
    {
        Equal(expected.Count, actual.Count, context);
        for (var index = 0; index < expected.Count; index++)
            Equal(expected[index], actual[index], $"{context}[{index}]");
    }

    private static void AssertSequenceEqual(IEnumerable<int> expected, IReadOnlyList<int> actual, string context)
    {
        var expectedValues = expected.ToArray();
        Equal(expectedValues.Length, actual.Count, context);
        for (var index = 0; index < expectedValues.Length; index++)
            Equal(expectedValues[index], actual[index], $"{context}[{index}]");
    }

    private static string HashLayer(ManagedLayer layer)
    {
        var hash = FnvOffset;
        AppendHash(ref hash, HashUShortValues(layer.Pieces));
        AppendHash(ref hash, HashByteValues(layer.Special));
        AppendHash(ref hash, HashByteValues(layer.Transparency));
        AppendHash(ref hash, HashByteValues(layer.Properties));
        AppendHash(ref hash, HashByteValues(layer.Solid));
        AppendHash(ref hash, HashByteValues(layer.MissileBlocked));
        AppendHash(ref hash, HashByteValues(layer.BlockLight));
        AppendHash(ref hash, HashByteValues(layer.Walkable));
        AppendHash(ref hash, HashByteValues(layer.ItemBlocking));
        return FormatHash(hash);
    }

    private static string HashUShorts(IReadOnlyList<ushort> values)
        => FormatHash(HashUShortValues(values));

    private static string HashBytes(IReadOnlyList<byte> values)
        => FormatHash(HashByteValues(values));

    private static ulong HashUShortValues(IReadOnlyList<ushort> values)
    {
        var hash = FnvOffset;
        foreach (var value in values) {
            hash = Add(hash, (byte)value);
            hash = Add(hash, (byte)(value >> 8));
        }
        return hash;
    }

    private static ulong HashByteValues(IReadOnlyList<byte> values)
    {
        var hash = FnvOffset;
        foreach (var value in values)
            hash = Add(hash, value);
        return hash;
    }

    private static void AppendHash(ref ulong hash, ulong value)
    {
        for (var byteIndex = 0; byteIndex < sizeof(ulong); byteIndex++)
            hash = Add(hash, (byte)(value >> (byteIndex * 8)));
    }

    private static ulong Add(ulong hash, byte value)
        => unchecked((hash ^ value) * FnvPrime);

    private static string FormatHash(ulong hash)
        => $"0x{hash:x16}";

    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private sealed record ManagedBaseline(
        AuthoritativeCathedralDoorDiscovery Discovery,
        ManagedLayer Layer);

    private static byte[] DeriveManagedItemBlocking(
        AuthoritativeLevel level,
        IReadOnlyList<AuthoritativeCathedralDoorState> doors)
    {
        var count = checked(level.Width * level.Height);
        var interactive = doors
            .Where(door => door.SelectionRegion != 0)
            .Select(door => (door.PositionX, door.PositionY))
            .ToHashSet();
        var solid = doors
            .Where(door => door.SolidFlag)
            .Select(door => (door.PositionX, door.PositionY))
            .ToHashSet();
        // AddL1Objs also retains solid, non-selectable L1 lights at d-piece 269.
        // Their static overlap is part of this native object query, not door SOL.
        for (var cell = 0; cell < count; cell++) {
            if (level.NativePieceLayer[cell] == 269)
                solid.Add((cell % level.Width, cell / level.Width));
        }
        var itemBlocking = new byte[count];
        for (var y = 0; y < level.Height; y++) {
            for (var x = 0; x < level.Width; x++) {
                // Native IsItemBlockingObjectAtPosition checks a solid object
                // on the item cell, an interactive object to its south, or
                // interactive objects at both south-east and south-west.
                var directSolid = solid.Contains((x, y));
                var southInteractive = interactive.Contains((x + 1, y + 1));
                var diagonalInteractive = interactive.Contains((x + 1, y))
                    && interactive.Contains((x, y + 1));
                if (directSolid || southInteractive || diagonalInteractive)
                    itemBlocking[y * level.Width + x] = 1;
            }
        }
        return itemBlocking;
    }

    private class ManagedLayer
    {
        public ushort[] Pieces { get; init; } = [];
        public byte[] Special { get; init; } = [];
        public byte[] Transparency { get; init; } = [];
        public byte[] Properties { get; init; } = [];
        public byte[] Solid { get; init; } = [];
        public byte[] MissileBlocked { get; init; } = [];
        public byte[] BlockLight { get; init; } = [];
        public byte[] Walkable { get; init; } = [];
        public byte[] ItemBlocking { get; init; } = [];

        public static ManagedLayer FromLevel(
            AuthoritativeLevel level,
            IReadOnlyList<AuthoritativeCathedralDoorState> doors)
        {
            var count = checked(level.Width * level.Height);
            var pieces = level.NativePieceLayer.ToArray();
            var special = level.NativeSpecialLayer.Select(value => checked((byte)value)).ToArray();
            var transparency = level.NativeTransparency.Select(value => checked((byte)value)).ToArray();
            var properties = new byte[count];
            var solid = new byte[count];
            var missileBlocked = new byte[count];
            var blockLight = new byte[count];
            var walkable = new byte[count];
            var blocked = level.BlockedCells.ToHashSet();
            var missile = level.MissileBlockedCells.ToHashSet();
            for (var index = 0; index < count; index++) {
                Assert.True(level.NativePieceProperties.TryGetValue(pieces[index], out var flags),
                    $"Managed level is missing SOL properties for d-piece {pieces[index]} at cell {index}.");
                properties[index] = (byte)flags;
                solid[index] = flags.HasFlag(AuthoritativeDunTileFlags.Solid) ? (byte)1 : (byte)0;
                missileBlocked[index] = flags.HasFlag(AuthoritativeDunTileFlags.BlockMissile) ? (byte)1 : (byte)0;
                blockLight[index] = flags.HasFlag(AuthoritativeDunTileFlags.BlockLight) ? (byte)1 : (byte)0;
                walkable[index] = blocked.Contains(index) ? (byte)0 : (byte)1;
            }
            var items = DeriveManagedItemBlocking(level, doors);
            Assert.Equal(count, items.Length);
            return new ManagedLayer {
                Pieces = pieces,
                Special = special,
                Transparency = transparency,
                Properties = properties,
                Solid = solid,
                MissileBlocked = missileBlocked,
                BlockLight = blockLight,
                Walkable = walkable,
                ItemBlocking = items,
            };
        }

        public static ManagedLayer FromOracle(DoorOracleLayer layer)
            => new() {
                Pieces = layer.Pieces.ToArray(),
                Special = layer.Special.ToArray(),
                Transparency = layer.Transparency.ToArray(),
                Properties = layer.Properties.ToArray(),
                Solid = layer.Solid.ToArray(),
                MissileBlocked = layer.MissileBlocked.ToArray(),
                BlockLight = layer.BlockLight.ToArray(),
                Walkable = layer.Walkable.ToArray(),
                ItemBlocking = layer.ItemBlocking.ToArray(),
            };

        public ManagedLayer Clone()
            => new() {
                Pieces = Pieces.ToArray(),
                Special = Special.ToArray(),
                Transparency = Transparency.ToArray(),
                Properties = Properties.ToArray(),
                Solid = Solid.ToArray(),
                MissileBlocked = MissileBlocked.ToArray(),
                BlockLight = BlockLight.ToArray(),
                Walkable = Walkable.ToArray(),
                ItemBlocking = ItemBlocking.ToArray(),
            };

        public DoorTile ToTile(int index)
            => new(Pieces[index], Special[index], Transparency[index], Properties[index], Solid[index],
                MissileBlocked[index], BlockLight[index], Walkable[index], ItemBlocking[index]);
    }

    private sealed class LayerState : ManagedLayer
    {
        public void Apply(int index, DoorTile tile)
        {
            Pieces[index] = tile.Piece;
            Special[index] = tile.Special;
            Transparency[index] = tile.Transparency;
            Properties[index] = tile.Properties;
            Solid[index] = tile.Solid;
            MissileBlocked[index] = tile.MissileBlocked;
            BlockLight[index] = tile.BlockLight;
            Walkable[index] = tile.Walkable;
            ItemBlocking[index] = tile.ItemBlocking;
        }

        public static new LayerState FromOracle(DoorOracleLayer layer)
            => new() {
                Pieces = layer.Pieces.ToArray(),
                Special = layer.Special.ToArray(),
                Transparency = layer.Transparency.ToArray(),
                Properties = layer.Properties.ToArray(),
                Solid = layer.Solid.ToArray(),
                MissileBlocked = layer.MissileBlocked.ToArray(),
                BlockLight = layer.BlockLight.ToArray(),
                Walkable = layer.Walkable.ToArray(),
                ItemBlocking = layer.ItemBlocking.ToArray(),
            };

        public new LayerState Clone()
            => new() {
                Pieces = Pieces.ToArray(),
                Special = Special.ToArray(),
                Transparency = Transparency.ToArray(),
                Properties = Properties.ToArray(),
                Solid = Solid.ToArray(),
                MissileBlocked = MissileBlocked.ToArray(),
                BlockLight = BlockLight.ToArray(),
                Walkable = Walkable.ToArray(),
                ItemBlocking = ItemBlocking.ToArray(),
            };
    }

    private sealed class DoorTile : IEquatable<DoorTile>
    {
        public DoorTile() { }

        public DoorTile(
            ushort piece,
            byte special,
            byte transparency,
            byte properties,
            byte solid,
            byte missileBlocked,
            byte blockLight,
            byte walkable,
            byte itemBlocking)
        {
            Piece = piece;
            Special = special;
            Transparency = transparency;
            Properties = properties;
            Solid = solid;
            MissileBlocked = missileBlocked;
            BlockLight = blockLight;
            Walkable = walkable;
            ItemBlocking = itemBlocking;
        }

        public ushort Piece { get; set; }
        public byte Special { get; set; }
        public byte Transparency { get; set; }
        public byte Properties { get; set; }
        public byte Solid { get; set; }
        public byte MissileBlocked { get; set; }
        public byte BlockLight { get; set; }
        public byte Walkable { get; set; }
        public byte ItemBlocking { get; set; }

        public bool Equals(DoorTile? other)
            => other is not null
                && Piece == other.Piece
                && Special == other.Special
                && Transparency == other.Transparency
                && Properties == other.Properties
                && Solid == other.Solid
                && MissileBlocked == other.MissileBlocked
                && BlockLight == other.BlockLight
                && Walkable == other.Walkable
                && ItemBlocking == other.ItemBlocking;

        public override bool Equals(object? obj) => Equals(obj as DoorTile);

        public override int GetHashCode()
            => HashCode.Combine(
                HashCode.Combine(Piece, Special, Transparency, Properties, Solid, MissileBlocked, BlockLight, Walkable),
                ItemBlocking);
    }

    /** Native captures use numeric byte arrays, never JSON's base64 byte[] convention. */
    private sealed class NumericByteArrayConverter : JsonConverter<byte[]>
    {
        public override byte[] Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("Native oracle byte layers must be numeric arrays.");
            var result = new List<byte>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) {
                if (result.Count >= NativeCellCount || reader.TokenType != JsonTokenType.Number
                    || !reader.TryGetByte(out var value))
                    throw new JsonException("Native oracle byte layer is invalid or oversized.");
                result.Add(value);
            }
            if (reader.TokenType != JsonTokenType.EndArray)
                throw new JsonException("Native oracle byte layer is incomplete.");
            return result.ToArray();
        }

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
            => throw new NotSupportedException("The private native corpus is read-only.");
    }

    private sealed class DoorOracleCase
    {
        public string Format { get; set; } = string.Empty;
        public int Version { get; set; }
        public string Policy { get; set; } = string.Empty;
        public int Level { get; set; }
        public string Entry { get; set; } = string.Empty;
        public uint InputSeed { get; set; }
        public uint AttemptSeed { get; set; }
        public uint FinalRandomState { get; set; }
        public uint DoorScanRandomStateBefore { get; set; }
        public uint DoorScanRandomStateAfter { get; set; }
        public int NativeWidth { get; set; }
        public int NativeHeight { get; set; }
        public string RowMajor { get; set; } = string.Empty;
        public int BaselineRebuildCount { get; set; }
        public DoorOracleLayerHashes ClosedBaselineHashes { get; set; } = new();
        public DoorOracleLayer ClosedBaseline { get; set; } = new();
        public DoorOracleBaseline[] ClosedBaselines { get; set; } = [];
        public DoorOracleDoor[] Doors { get; set; } = [];
    }

    private sealed class DoorOracleBaseline
    {
        public int Ordinal { get; set; }
        public uint DoorScanRandomStateBefore { get; set; }
        public uint DoorScanRandomStateAfter { get; set; }
        public DoorOracleLayerHashes Hashes { get; set; } = new();
        public DoorOracleLayer Layer { get; set; } = new();
    }

    private sealed class DoorOracleLayerHashes
    {
        public string FullLayer { get; set; } = string.Empty;
        public string Pieces { get; set; } = string.Empty;
        public string Special { get; set; } = string.Empty;
        public string Transparency { get; set; } = string.Empty;
        public string Properties { get; set; } = string.Empty;
        public string Solid { get; set; } = string.Empty;
        public string MissileBlocked { get; set; } = string.Empty;
        public string BlockLight { get; set; } = string.Empty;
        public string Walkable { get; set; } = string.Empty;
        public string ItemBlocking { get; set; } = string.Empty;
    }

    private sealed class DoorOracleLayer
    {
        public ushort[] Pieces { get; set; } = [];
        public byte[] Special { get; set; } = [];
        public byte[] Transparency { get; set; } = [];
        public byte[] Properties { get; set; } = [];
        public byte[] Solid { get; set; } = [];
        public byte[] MissileBlocked { get; set; } = [];
        public byte[] BlockLight { get; set; } = [];
        public byte[] Walkable { get; set; } = [];
        public byte[] ItemBlocking { get; set; } = [];
    }

    private sealed class DoorOracleDoor
    {
        public int Ordinal { get; set; }
        public int FreshBaselineOrdinal { get; set; }
        public string OpenPath { get; set; } = string.Empty;
        public int NativeTypeId { get; set; }
        public string NativeType { get; set; } = string.Empty;
        public int[] Position { get; set; } = [];
        public int Var1OneBased { get; set; }
        public int Var2OneBased { get; set; }
        public int SourcePieceObserved { get; set; }
        public int NeighborPieceObserved { get; set; }
        public DoorOracleObjectState ClosedObject { get; set; } = new();
        public DoorOracleObjectState OpenObject { get; set; } = new();
        public DoorOracleObjectState ReclosedObject { get; set; } = new();
        public string ClosedLayerHash { get; set; } = string.Empty;
        public string OpenLayerHash { get; set; } = string.Empty;
        public string ReclosedLayerHash { get; set; } = string.Empty;
        public bool ReclosedLayerEqualsClosed { get; set; }
        public bool ReclosedObjectEqualsClosed { get; set; }
        public DoorOracleChangedCell[] ChangedCells { get; set; } = [];
    }

    private sealed class DoorOracleObjectState
    {
        public int State { get; set; }
        public int Var1OneBased { get; set; }
        public int Var2OneBased { get; set; }
        public int AnimationFrame { get; set; }
        public int SelectionRegion { get; set; }
        public bool DoorFlag { get; set; }
        public bool PreFlag { get; set; }
        public bool MissFlag { get; set; }
        public bool SolidFlag { get; set; }
    }

    private sealed class DoorOracleChangedCell
    {
        public int Index { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public DoorTile Closed { get; set; } = new();
        public DoorTile Open { get; set; } = new();
        public DoorTile Reclosed { get; set; } = new();
    }
}

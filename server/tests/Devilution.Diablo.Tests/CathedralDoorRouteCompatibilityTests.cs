using System.Security.Cryptography;
using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;
using Godevilution.Tools;
using Xunit;

namespace Devilution.Diablo.Tests;

/**
 * Explicit private-input integration coverage for depth routes through scanned
 * closed doors. This exercises the production integration, not an independent
 * native door oracle; the existing oracle remains the parity reference.
 */
public sealed class CathedralDoorRouteCompatibilityTests
{
    private const string ExpectedOrdinaryDepthTwoFailure =
        "Generated Cathedral depth 2 has no MAIN to Down stair path. "
        + "The diagnostic profile does not supply native object/door placement; choose explicit supported inputs, not altered geometry.";

    [Fact(Explicit = true)]
    public void DoorProfileAcceptsTheExactDepthWhoseRawClosedRoutesTheOrdinaryCompilerRefuses()
    {
        var geometryRoot = ResolvePrivateGeometryRoot();
        if (geometryRoot is null)
        {
            Assert.Skip("Private semantic Cathedral TIL/SOL inputs are not available.");
            return;
        }

        var piecesPath = Path.Combine(geometryRoot, "native_dun_piece_definitions.tsv");
        var propertiesPath = Path.Combine(geometryRoot, "native_dun_tile_properties.tsv");
        var pieces = File.ReadAllText(piecesPath);
        var properties = File.ReadAllText(propertiesPath);
        var inputHashes = new[] { HashFile(piecesPath), HashFile(propertiesPath) };
        var basePack = LoadBasePack();
        var originalPackTables = SnapshotTables(basePack);
        var root = CreateOwnedRoot();
        var completed = false;

        try
        {
            var baselineSeeds = new uint[] { 2588, 2588, 1, uint.MaxValue };
            var variantSeeds = new uint[] { 2588, 743271966, 1, uint.MaxValue };

            var ordinaryBaseline = CathedralDepthPlaytestCompiler.Compile(
                new CathedralDepthPlaytestCompilerOptions(
                    basePack,
                    Path.Combine(root, "ordinary-baseline"),
                    pieces,
                    properties,
                    baselineSeeds)
                {
                    PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.None,
                });
            Assert.Equal(baselineSeeds, ordinaryBaseline.DepthSeeds);

            var baselineDoorPath = Path.Combine(root, "door-baseline");
            var baselineDoorProfile = CathedralDoorPlaytestCompiler.Compile(
                new CathedralDoorPlaytestCompilerOptions(
                    basePack,
                    baselineDoorPath,
                    pieces,
                    properties,
                    baselineSeeds));
            var baselinePlan = PrepareAndCompareDoorProfile(
                baselineDoorPath, baselineDoorProfile, baselineSeeds, pieces, properties,
                expectedClosedDoorCount: 44);
            AssertRawDepthTwoRoutes(
                baselinePlan.Candidates.Single(candidate => candidate.Identity.Depth == 2),
                expectedMainToDown: true,
                expectedPreviousToUp: true);

            var variantDoorPath = Path.Combine(root, "door-variant");
            var variantDoorProfile = CathedralDoorPlaytestCompiler.Compile(
                new CathedralDoorPlaytestCompilerOptions(
                    basePack,
                    variantDoorPath,
                    pieces,
                    properties,
                    variantSeeds));
            var variantPlan = PrepareAndCompareDoorProfile(
                variantDoorPath, variantDoorProfile, variantSeeds, pieces, properties,
                expectedClosedDoorCount: 47);
            AssertRawDepthTwoRoutes(
                variantPlan.Candidates.Single(candidate => candidate.Identity.Depth == 2),
                expectedMainToDown: false,
                expectedPreviousToUp: false);

            var ordinaryVariantPath = Path.Combine(root, "ordinary-variant");
            var error = Assert.Throws<InvalidDataException>(() =>
                CathedralDepthPlaytestCompiler.Compile(
                    new CathedralDepthPlaytestCompilerOptions(
                        basePack,
                        ordinaryVariantPath,
                        pieces,
                        properties,
                        variantSeeds)
                    {
                        PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.None,
                    }));
            Assert.Equal(ExpectedOrdinaryDepthTwoFailure, error.Message);
            Assert.False(Directory.Exists(ordinaryVariantPath));
            Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(root), path =>
                Path.GetFileName(path).StartsWith("ordinary-variant.staging-", StringComparison.Ordinal));

            Assert.Equal(originalPackTables, SnapshotTables(basePack));
            Assert.Equal(inputHashes, new[] { HashFile(piecesPath), HashFile(propertiesPath) });
            completed = true;
        }
        finally
        {
            if (completed)
                DeleteOwnedRoot(root);
        }
    }

    private static AuthoritativeCathedralDepthGenerationPlan PrepareAndCompareDoorProfile(
        string outputPath,
        CathedralDoorPlaytestProfile profile,
        IReadOnlyList<uint> seeds,
        string pieces,
        string properties,
        int expectedClosedDoorCount)
    {
        Assert.Equal(seeds, profile.DepthSeeds);
        Assert.False(profile.NativePopulationClaim);
        Assert.Equal(new[] {
            "1001|town_to_cathedral|down|0|town|1|cathedral",
            "1002|cathedral_to_cathedral_depth_2|down|1|cathedral|2|cathedral_depth_2",
            "1003|cathedral_depth_2_to_cathedral_depth_3|down|2|cathedral_depth_2|3|cathedral_depth_3",
            "1004|cathedral_depth_3_to_cathedral_depth_4|down|3|cathedral_depth_3|4|cathedral_depth_4",
            "1099|cathedral_to_town|up|1|cathedral|0|town",
            "1100|cathedral_depth_2_to_cathedral|up|2|cathedral_depth_2|1|cathedral",
            "1101|cathedral_depth_3_to_cathedral_depth_2|up|3|cathedral_depth_3|2|cathedral_depth_2",
            "1102|cathedral_depth_4_to_cathedral_depth_3|up|4|cathedral_depth_4|3|cathedral_depth_3",
        }, profile.Routes.Select(route =>
            $"{route.RouteId}|{route.RouteSymbol}|{route.Direction}|{route.SourceLevelId}|{route.SourceLevelSymbol}|{route.DestinationLevelId}|{route.DestinationLevelSymbol}"));
        Assert.Equal(4, profile.Levels.Count);

        var pack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(outputPath, CathedralDoorPlaytestCompiler.ContentDirectoryName));
        var manifest = new ContentManifest(pack.Id, pack.Version, [pack]);
        var composed = ComposedContent.Compose(manifest);
        var activation = DiabloModule.Instance.CathedralDoorContentBootstrap.ActivateDoors(composed);
        var worldState = activation.Simulation.WorldState;
        Assert.True(worldState.HasCathedralDoors);
        var world = worldState.World ?? throw new InvalidOperationException(
            "The activated Cathedral door profile has no authoritative world.");

        var stableIdTable = pack.Tables.Single(table => table.SourcePath == "content_ids.tsv");
        var stableIds = StableContentIdCatalog.LoadTsv(
            stableIdTable.SourcePath, stableIdTable.ToCanonicalTsv());
        var depthTable = pack.Tables.Single(table =>
            table.SourcePath == AuthoritativeCathedralDepthRuntimeCatalog.TableName);
        var depthCatalog = AuthoritativeCathedralDepthRuntimeCatalog.Load(
            depthTable.ToCanonicalTsv(), stableIds);
        Assert.Equal(seeds, depthCatalog.Definitions.Select(definition => definition.DungeonSeed));
        var geometry = AuthoritativeNativeDunGeometryCatalog.LoadTsv(
            AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath,
            pieces,
            AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath,
            properties);

        var originalLevels = world.Levels.ToArray();
        var originalGeometry = originalLevels.Select(CaptureLevel).ToArray();
        var plan = depthCatalog.Prepare(world, geometry);

        Assert.Equal(4, plan.Candidates.Count);
        Assert.Equal(originalLevels.Length, world.Levels.Count);
        for (var index = 0; index < originalLevels.Length; index++)
        {
            Assert.Same(originalLevels[index], world.Levels[index]);
            AssertLevelEqual(originalGeometry[index], world.Levels[index]);
        }

        var closedDoorObjects = worldState.CaptureCathedralDoorObjects();
        Assert.Equal(expectedClosedDoorCount, closedDoorObjects.Count);
        foreach (var door in closedDoorObjects)
            Assert.True(door.DoorState == 0, "The fresh door profile did not remain closed.");

        foreach (var candidate in plan.Candidates)
        {
            var depth = candidate.Identity.Depth;
            Assert.Equal(seeds[depth - 1], candidate.Identity.DungeonSeed);
            Assert.Equal(candidate.Identity.LevelId, candidate.Level.LevelId);
            Assert.Equal(candidate.Identity.LevelSymbol, candidate.Level.StableSymbol);
            Assert.Matches("^[A-F0-9]{64}$", candidate.Identity.RouteFingerprint);
            Assert.Same(originalLevels.Single(level => level.LevelId == candidate.Level.LevelId),
                candidate.OriginalLevel);

            var profileLevel = profile.Levels.Single(level => level.Depth == depth);
            Assert.Equal(seeds[depth - 1], profileLevel.DungeonSeed);
            AssertProfileRoutePoints(candidate.Routes, profileLevel);
            var activeLevel = worldState.LevelGeometry.Single(level => level.LevelId == candidate.Level.LevelId);

            // Prepare returns raw materialized geometry; native door discovery adds the
            // closed special layer and recomputes collision. Compare those channels
            // through the same production scanner used by activation, not a duplicate.
            Assert.Empty(candidate.Level.NativeSpecialLayer);
            var rawCandidateGeometry = CaptureLevel(candidate.Level);
            var discovered = AuthoritativeCathedralDoors.Discover(candidate.Level);
            AssertLevelEqual(rawCandidateGeometry, candidate.Level);
            Assert.Equal(candidate.Level.Width * candidate.Level.Height,
                discovered.Level.NativeSpecialLayer.Count);
            AssertLevelEqual(CaptureLevel(discovered.Level), activeLevel);
            AssertPreservedRawIdentity(candidate.Level, activeLevel);

            var expectedDoors = discovered.Doors
                .OrderBy(door => door.PositionY).ThenBy(door => door.PositionX).ToArray();
            var actualDoors = closedDoorObjects.Where(door => door.LevelId == candidate.Level.LevelId)
                .OrderBy(door => door.PositionY).ThenBy(door => door.PositionX).ToArray();
            Assert.Equal(expectedDoors.Length, actualDoors.Length);
            for (var index = 0; index < expectedDoors.Length; index++)
            {
                var expected = expectedDoors[index];
                var actual = actualDoors[index];
                Assert.Equal(expected.PositionX, actual.PositionX);
                Assert.Equal(expected.PositionY, actual.PositionY);
                Assert.Equal((int)expected.Kind, actual.NativeObjectId);
                Assert.Equal((int)expected.Status, actual.DoorState);
                Assert.Equal(expected.Var1, actual.CachedPiece);
                Assert.Equal(expected.Var2, actual.CachedNeighborPiece);
                Assert.Equal(expected.AnimationFrame, actual.AnimationFrame);
                Assert.Equal(expected.SelectionRegion, actual.SelectionRegion);
                Assert.Equal(expected.PreFlag, actual.PreDraw);
                Assert.Equal(expected.MissFlag, actual.MissilePassable);
                Assert.Equal(expected.SolidFlag, actual.Solid);
                Assert.Equal(DiabloWorldState.GetCathedralDoorEntityId(
                    candidate.Level.LevelId, candidate.Level.Width, expected.PositionX, expected.PositionY),
                    actual.EntityId);
            }
        }

        return plan;
    }

    private static void AssertProfileRoutePoints(
        AuthoritativeCathedralLevel1RoutePoints routes,
        CathedralDepthPlaytestLevelProfile profile)
    {
        Assert.Equal(routes.MainEntry.X, profile.MainEntry.X);
        Assert.Equal(routes.MainEntry.Y, profile.MainEntry.Y);
        Assert.Equal(routes.PreviousEntry.X, profile.PreviousEntry.X);
        Assert.Equal(routes.PreviousEntry.Y, profile.PreviousEntry.Y);
        Assert.Equal(routes.UpTrigger.X, profile.UpTrigger.X);
        Assert.Equal(routes.UpTrigger.Y, profile.UpTrigger.Y);
        Assert.Equal(routes.DownTrigger.X, profile.DownTrigger.X);
        Assert.Equal(routes.DownTrigger.Y, profile.DownTrigger.Y);
        Assert.Equal("main", profile.Entry);
    }

    private static void AssertRawDepthTwoRoutes(
        AuthoritativeCathedralDepthGenerationCandidate candidate,
        bool expectedMainToDown,
        bool expectedPreviousToUp)
    {
        Assert.Equal(2, candidate.Identity.Depth);
        var routes = candidate.Routes;
        Assert.Equal(expectedMainToDown, HasRawWalkPath(
            candidate.Level, routes.MainEntry, routes.DownTrigger, routes.UpTrigger));
        Assert.Equal(expectedPreviousToUp, HasRawWalkPath(
            candidate.Level, routes.PreviousEntry, routes.UpTrigger, routes.DownTrigger));
    }

    /** Raw closed-SOL reachability only; no native door transitions are simulated here. */
    private static bool HasRawWalkPath(
        AuthoritativeLevel level,
        AuthoritativeCathedralPoint start,
        AuthoritativeCathedralPoint target,
        AuthoritativeCathedralPoint excludedTrigger)
    {
        if (!level.IsWalkable(start.X, start.Y)
            || !level.IsWalkable(target.X, target.Y)
            || start == excludedTrigger
            || target == excludedTrigger)
            return false;

        var queue = new Queue<AuthoritativeCathedralPoint>();
        var seen = new HashSet<AuthoritativeCathedralPoint> { start };
        queue.Enqueue(start);
        while (queue.Count != 0)
        {
            var current = queue.Dequeue();
            if (current == target)
                return true;
            foreach (var offset in DeterministicGridNavigation.NeighborDisplacements)
            {
                var next = new AuthoritativeCathedralPoint(current.X + offset.X, current.Y + offset.Y);
                if (next == excludedTrigger || !level.IsWalkable(next.X, next.Y) || !seen.Add(next))
                    continue;
                queue.Enqueue(next);
            }
        }
        return false;
    }

    private static void AssertPreservedRawIdentity(AuthoritativeLevel raw, AuthoritativeLevel activated)
    {
        Assert.Equal(raw.LevelId, activated.LevelId);
        Assert.Equal(raw.StableSymbol, activated.StableSymbol);
        Assert.Equal(raw.Width, activated.Width);
        Assert.Equal(raw.Height, activated.Height);
        Assert.Equal(raw.IsDefaultStart, activated.IsDefaultStart);
        Assert.Equal(raw.SuppressMultiplayerDeathDrops, activated.SuppressMultiplayerDeathDrops);
        Assert.Equal(raw.StartPositionX, activated.StartPositionX);
        Assert.Equal(raw.StartPositionY, activated.StartPositionY);
        AssertSameSequence(raw.NativeTransparency, activated.NativeTransparency,
            "raw-to-activated native transparency", raw.LevelId);
        AssertSameSequence(raw.NativePieceProperties.OrderBy(pair => pair.Key),
            activated.NativePieceProperties.OrderBy(pair => pair.Key),
            "raw-to-activated native piece properties", raw.LevelId);
    }

    private static LevelProjection CaptureLevel(AuthoritativeLevel level)
        => new(
            level.LevelId,
            level.StableSymbol,
            level.Width,
            level.Height,
            level.BlockedCells.ToArray(),
            level.MissileBlockedCells.ToArray(),
            level.NativeTransparency.ToArray(),
            level.NativeObjectLayer.ToArray(),
            level.NativeMegaTileLayer.ToArray(),
            level.NativePieceLayer.ToArray(),
            level.NativeSpecialLayer.ToArray(),
            level.NativePieceProperties.OrderBy(pair => pair.Key).ToArray(),
            level.NativePieceCollisionCells.ToArray(),
            level.NativePieceMissileBlockedCells.ToArray(),
            level.NativeVisionBlockedCells.ToArray(),
            level.HasExplicitNativeVisionBlockedCells,
            level.NativeMonsterLayer.ToArray(),
            level.StaticObjectBlockedCells.ToArray(),
            level.IsDefaultStart,
            level.SuppressMultiplayerDeathDrops,
            level.StartPositionX,
            level.StartPositionY);

    private static void AssertLevelEqual(LevelProjection expected, AuthoritativeLevel actual)
    {
        Assert.Equal(expected.LevelId, actual.LevelId);
        Assert.Equal(expected.StableSymbol, actual.StableSymbol);
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        AssertSameSequence(expected.BlockedCells, actual.BlockedCells,
            "closed activated collision", expected.LevelId);
        AssertSameSequence(expected.MissileBlockedCells, actual.MissileBlockedCells,
            "closed activated missile collision", expected.LevelId);
        AssertSameSequence(expected.NativeTransparency, actual.NativeTransparency,
            "closed activated transparency", expected.LevelId);
        AssertSameSequence(expected.NativeObjectLayer, actual.NativeObjectLayer,
            "closed activated object layer", expected.LevelId);
        AssertSameSequence(expected.NativeMegaTileLayer, actual.NativeMegaTileLayer,
            "closed activated mega-tile layer", expected.LevelId);
        AssertSameSequence(expected.NativePieceLayer, actual.NativePieceLayer,
            "closed activated piece layer", expected.LevelId);
        AssertSameSequence(expected.NativeSpecialLayer, actual.NativeSpecialLayer,
            "closed activated special layer", expected.LevelId);
        AssertSameSequence(expected.NativePieceProperties,
            actual.NativePieceProperties.OrderBy(pair => pair.Key),
            "closed activated piece properties", expected.LevelId);
        AssertSameSequence(expected.NativePieceCollisionCells, actual.NativePieceCollisionCells,
            "closed activated piece collision", expected.LevelId);
        AssertSameSequence(expected.NativePieceMissileBlockedCells, actual.NativePieceMissileBlockedCells,
            "closed activated piece missile collision", expected.LevelId);
        AssertSameSequence(expected.NativeVisionBlockedCells, actual.NativeVisionBlockedCells,
            "closed activated vision collision", expected.LevelId);
        Assert.Equal(expected.HasExplicitNativeVisionBlockedCells, actual.HasExplicitNativeVisionBlockedCells);
        AssertSameSequence(expected.NativeMonsterLayer, actual.NativeMonsterLayer,
            "closed activated monster layer", expected.LevelId);
        AssertSameSequence(expected.StaticObjectBlockedCells, actual.StaticObjectBlockedCells,
            "closed activated static object collision", expected.LevelId);
        Assert.Equal(expected.IsDefaultStart, actual.IsDefaultStart);
        Assert.Equal(expected.SuppressMultiplayerDeathDrops, actual.SuppressMultiplayerDeathDrops);
        Assert.Equal(expected.StartPositionX, actual.StartPositionX);
        Assert.Equal(expected.StartPositionY, actual.StartPositionY);
    }

    private static void AssertSameSequence<T>(
        IEnumerable<T> expected,
        IEnumerable<T> actual,
        string channel,
        uint levelId)
        => Assert.True(expected.SequenceEqual(actual),
            $"Cathedral depth {levelId} {channel} differs.");

    private static ContentPack LoadBasePack()
        => ContentPackLoader.LoadManifestDirectory(
            Path.Combine(FindRepositoryRoot(), "server", "content", "base"));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static string? ResolvePrivateGeometryRoot()
    {
        var root = Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_DOOR_PLAYTEST_GEOMETRY");
        if (string.IsNullOrWhiteSpace(root))
            root = Path.Combine(FindRepositoryRoot(), ".local", "cathedral-depths-playtest", "content");
        return File.Exists(Path.Combine(root, "native_dun_piece_definitions.tsv"))
            && File.Exists(Path.Combine(root, "native_dun_tile_properties.tsv"))
                ? root
                : null;
    }

    private static string[] SnapshotTables(ContentPack pack)
        => pack.Tables.OrderBy(table => table.SourcePath, StringComparer.Ordinal)
            .Select(table => table.SourcePath + ":" + HashText(table.ToCanonicalTsv())).ToArray();

    private static string HashText(string value)
        => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    private static string HashFile(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static string CreateOwnedRoot()
    {
        var parent = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = Path.Combine(parent, "godevilution-cathedral-route-" + Guid.NewGuid().ToString("N"));
        if (Directory.Exists(root) || File.Exists(root))
            throw new IOException("The generated Cathedral route test root already exists.");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteOwnedRoot(string path)
    {
        var fullRoot = Path.GetFullPath(path);
        var parent = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Assert.Equal(parent, Directory.GetParent(fullRoot)?.FullName, ignoreCase: true);
        Assert.Matches("^godevilution-cathedral-route-[0-9a-f]{32}$", Path.GetFileName(fullRoot));
        Assert.False((File.GetAttributes(fullRoot) & FileAttributes.ReparsePoint) != 0);
        Directory.Delete(fullRoot, recursive: true);
    }

    private sealed record LevelProjection(
        uint LevelId,
        string StableSymbol,
        int Width,
        int Height,
        int[] BlockedCells,
        int[] MissileBlockedCells,
        ushort[] NativeTransparency,
        ushort[] NativeObjectLayer,
        ushort[] NativeMegaTileLayer,
        ushort[] NativePieceLayer,
        ushort[] NativeSpecialLayer,
        KeyValuePair<ushort, AuthoritativeDunTileFlags>[] NativePieceProperties,
        int[] NativePieceCollisionCells,
        int[] NativePieceMissileBlockedCells,
        int[] NativeVisionBlockedCells,
        bool HasExplicitNativeVisionBlockedCells,
        ushort[] NativeMonsterLayer,
        int[] StaticObjectBlockedCells,
        bool IsDefaultStart,
        bool SuppressMultiplayerDeathDrops,
        int StartPositionX,
        int StartPositionY);
}

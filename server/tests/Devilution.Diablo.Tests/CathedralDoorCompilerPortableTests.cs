using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using Devilution.Data;
using Devilution.Diablo;
using Godevilution.Tools;
using Xunit;

namespace Devilution.Diablo.Tests;

/**
 * Portable Cathedral-door compiler coverage using labeled synthetic TIL/SOL
 * semantics. These fixtures are diagnostic only; they are not native geometry
 * or a shipped-content claim.
 */
public sealed class CathedralDoorCompilerPortableTests
{
    [Fact]
    public void CompilesAllFourDepthsWithDoorIdentityAndNonePolicy()
    {
        var basePack = LoadBasePack();
        var before = SnapshotTables(basePack);
        var seeds = new uint[] { 17, 2588, 1, uint.MaxValue };
        var output = NewOutput();
        Exception? primary = null;
        try
        {
            var profile = CathedralDoorPlaytestCompiler.Compile(
                new CathedralDoorPlaytestCompilerOptions(
                    basePack,
                    output,
                    CreateDoorPieces(),
                    CreateProperties(),
                    seeds) {
                    PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.None,
                });

            Assert.Equal(1, profile.FormatVersion);
            Assert.Equal(CathedralDoorPlaytestCompiler.AdapterId, profile.AdapterId);
            Assert.Equal(CathedralDoorPlaytestCompiler.PackId, profile.ContentId);
            Assert.Equal(CathedralDoorPlaytestCompiler.PackVersion, profile.ContentVersion);
            Assert.Equal(seeds, profile.DepthSeeds);
            Assert.Equal("main", profile.Entry);
            Assert.Equal("none", profile.PlacementPolicy);
            Assert.False(profile.NativePopulationClaim);
            Assert.Equal(AuthoritativeCathedralDoorCatalog.ProfileId, profile.DoorProfileId);
            Assert.Equal(AuthoritativeCathedralDoorCatalog.Revision, profile.DoorProfileRevision);
            Assert.NotEqual(string.Empty, profile.DoorCatalogFingerprint);
            Assert.NotEqual(string.Empty, profile.ContentManifestHash);
            Assert.NotEqual(string.Empty, profile.PackFileHash);
            Assert.Equal(4, profile.Levels.Count);
            Assert.Equal(4, profile.DoorMappings.Count);
            Assert.Equal(
                new[] { "cathedral", "cathedral_depth_2", "cathedral_depth_3", "cathedral_depth_4" },
                profile.Levels.Select(level => level.LevelSymbol));
            Assert.All(profile.Levels, level => {
                Assert.Null(level.EncounterPosition);
                Assert.Null(level.ObjectPosition);
            });

            var pack = ContentPackLoader.LoadManifestDirectory(
                Path.Combine(output, CathedralDoorPlaytestCompiler.ContentDirectoryName));
            var composedManifest = new ContentManifest(pack.Id, pack.Version, [pack]);
            Assert.Equal(composedManifest.Sha256, profile.ContentManifestHash);
            Assert.Equal(ComputePackFileHash(
                Path.Combine(output, CathedralDoorPlaytestCompiler.ContentDirectoryName)),
                profile.PackFileHash);
            var doorTable = pack.Tables.Single(table =>
                table.SourcePath == AuthoritativeCathedralDoorCatalog.TableName);
            Assert.Equal(4, doorTable.Rows.Count);
            Assert.Equal(
                new[] { "l1_left_door", "l1_right_door" },
                new[] {
                    doorTable.Rows[0].Required("left_object_symbol"),
                    doorTable.Rows[0].Required("right_object_symbol"),
                });
            Assert.Empty(pack.Tables.Single(table => table.SourcePath == "monsters.tsv").Rows);
            Assert.Equal(2, pack.Tables.Single(table => table.SourcePath == "objects.tsv").Rows.Count);
            Assert.True(File.Exists(Path.Combine(output, CathedralDoorPlaytestCompiler.ProfileFileName)));
            Assert.False(File.Exists(Path.Combine(output, CathedralDepthPlaytestCompiler.ProfileFileName)));

            var content = ComposedContent.Compose(composedManifest);
            var activation = DiabloModule.Instance.CathedralDoorContentBootstrap.ActivateDoors(content);
            var doors = activation.Simulation.WorldState.CaptureCathedralDoorObjects();
            Assert.NotEmpty(doors);
            Assert.Equal(
                new uint[] { 1, 2, 3, 4 },
                doors.Select(door => door.LevelId).Distinct().OrderBy(level => level));
            Assert.Contains(activation.Simulation.WorldState.LevelGeometry,
                level => level.LevelId is >= 1 and <= 4
                    && level.NativePieceLayer.Contains((ushort)43));
            Assert.Contains(activation.Simulation.WorldState.LevelGeometry,
                level => level.LevelId is >= 1 and <= 4
                    && level.NativePieceLayer.Contains((ushort)45));
            Assert.Contains(activation.Simulation.WorldState.LevelGeometry,
                level => level.LevelId is >= 1 and <= 4
                    && level.NativePieceLayer.Contains((ushort)269));
            Assert.Equal(before, SnapshotTables(basePack));
        }
        catch (Exception error)
        {
            primary = error;
            throw;
        }
        finally
        {
            CleanupOutput(output, primary);
        }
    }

    [Fact]
    public void PublishesReproducibleTreeAndRefusesOverwrite()
    {
        var basePack = LoadBasePack();
        var first = NewOutput();
        var second = NewOutput();
        Exception? primary = null;
        try
        {
            var options = new CathedralDoorPlaytestCompilerOptions(
                basePack,
                first,
                CreateDoorPieces(),
                CreateProperties(),
                [1U, 2U, 3U, 4U]) {
                PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.None,
            };
            var left = CathedralDoorPlaytestCompiler.Compile(options);
            var right = CathedralDoorPlaytestCompiler.Compile(options with { OutputDirectory = second });

            Assert.Equal(left.ContentManifestHash, right.ContentManifestHash);
            Assert.Equal(left.PackFileHash, right.PackFileHash);
            Assert.Equal(
                SnapshotFiles(first),
                SnapshotFiles(second));

            var profilePath = Path.Combine(first, CathedralDoorPlaytestCompiler.ProfileFileName);
            var profileBefore = File.ReadAllBytes(profilePath);
            var error = Assert.Throws<IOException>(() => CathedralDoorPlaytestCompiler.Compile(options));
            Assert.Contains("must not already exist", error.Message, StringComparison.Ordinal);
            Assert.Equal(profileBefore, File.ReadAllBytes(profilePath));
            AssertNoStagingSiblings(first);
        }
        catch (Exception error)
        {
            primary = error;
            throw;
        }
        finally
        {
            CleanupOutputs(first, second, primary);
        }
    }

    [Fact]
    public void RejectsNativeScannerLightPoolExhaustionWithoutPublication()
    {
        var basePack = LoadBasePack();
        var before = SnapshotTables(basePack);
        var output = NewOutput();
        Exception? primary = null;
        try
        {
            var error = Assert.Throws<InvalidDataException>(() => CathedralDoorPlaytestCompiler.Compile(
                new CathedralDoorPlaytestCompilerOptions(
                    basePack,
                    output,
                    CreateOverflowPieces(),
                    CreateProperties(),
                    [1U, 2U, 3U, 4U]) {
                    PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.None,
                }));

            Assert.Equal("Cathedral door/light scan exceeds the native object pool.", error.Message);
            Assert.False(Directory.Exists(output));
            AssertNoStagingSiblings(output);
            Assert.Equal(before, SnapshotTables(basePack));
        }
        catch (Exception error)
        {
            primary = error;
            throw;
        }
        finally
        {
            CleanupOutput(output, primary);
        }
    }

    [Fact]
    public void CompilesExactMixedNativeScannerCapacityAndRejectsOneOverWithoutPublication()
    {
        var basePack = LoadBasePack();
        var seeds = new uint[] { 17, 2588, 1, uint.MaxValue };
        var before = SnapshotTables(basePack);
        var exact = CreateScannerBoundaryFixture(seeds, 127);
        AssertMaterializedScannerFixture(exact);
        var exactOutput = NewOutput();
        Exception? primary = null;
        try
        {
            var profile = CathedralDoorPlaytestCompiler.Compile(new CathedralDoorPlaytestCompilerOptions(
                basePack,
                exactOutput,
                exact.PieceDefinitionsTsv,
                CreateProperties(),
                seeds) {
                PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.None,
            });
            var generated = ContentPackLoader.LoadManifestDirectory(
                Path.Combine(exactOutput, CathedralDoorPlaytestCompiler.ContentDirectoryName));
            var content = ComposedContent.Compose(new ContentManifest(generated.Id, generated.Version, [generated]));
            var activation = DiabloModule.Instance.CathedralDoorContentBootstrap.ActivateDoors(content);
            var doors = activation.Simulation.WorldState.CaptureCathedralDoorObjects();
            var targetLevelId = checked((uint)(exact.TargetDepth + 1));
            var targetGeometry = activation.Simulation.WorldState.LevelGeometry
                .Single(level => level.LevelId == targetLevelId);
            var targetDoorCount = doors.Count(door => door.LevelId == targetLevelId);
            var targetLightCount = targetGeometry.NativePieceLayer.Count(piece => piece == 269);

            Assert.Equal("none", profile.PlacementPolicy);
            Assert.True(targetDoorCount > 0);
            Assert.True(targetLightCount > 0);
            Assert.Equal(127, targetDoorCount + targetLightCount);
            for (var depthIndex = 0; depthIndex < 4; depthIndex++)
            {
                var levelId = checked((uint)(depthIndex + 1));
                var geometry = activation.Simulation.WorldState.LevelGeometry
                    .Single(level => level.LevelId == levelId);
                var scannerCount = doors.Count(door => door.LevelId == levelId)
                    + geometry.NativePieceLayer.Count(piece => piece == 269);
                Assert.Equal(exact.ExpectedScannerCounts[depthIndex], scannerCount);
                Assert.InRange(scannerCount, 0, 127);
            }
            Assert.Equal(before, SnapshotTables(basePack));
            AssertNoStagingSiblings(exactOutput);
        }
        catch (Exception error)
        {
            primary = error;
            throw;
        }
        finally
        {
            CleanupOutput(exactOutput, primary);
        }

        var overflow = CreateScannerBoundaryFixture(seeds, 128);
        AssertMaterializedScannerFixture(overflow);
        var overflowOutput = NewOutput();
        primary = null;
        try
        {
            var error = Assert.Throws<InvalidDataException>(() => CathedralDoorPlaytestCompiler.Compile(
                new CathedralDoorPlaytestCompilerOptions(
                    basePack,
                    overflowOutput,
                    overflow.PieceDefinitionsTsv,
                    CreateProperties(),
                    seeds) {
                    PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.None,
                }));

            Assert.Equal("Cathedral door/light scan exceeds the native object pool.", error.Message);
            Assert.Equal(128, overflow.ExpectedScannerCounts[overflow.TargetDepth]);
            Assert.False(Directory.Exists(overflowOutput));
            AssertNoStagingSiblings(overflowOutput);
            Assert.Equal(before, SnapshotTables(basePack));
        }
        catch (Exception error)
        {
            primary = error;
            throw;
        }
        finally
        {
            CleanupOutput(overflowOutput, primary);
        }
    }

    [Fact]
    public void CompilesAnAdjacentAuthoredFootprintButRejectsTheExactDoorItemFootprintBeforePublication()
    {
        var basePack = LoadBasePack();
        var seeds = new uint[] { 17, 2588, 1, uint.MaxValue };
        var pieces = CreateDoorPieces();
        var baselineOutput = NewOutput();
        CathedralDepthPlaytestLevelProfile firstLevel;
        Exception? primary = null;
        try
        {
            var baseline = CathedralDoorPlaytestCompiler.Compile(new CathedralDoorPlaytestCompilerOptions(
                basePack,
                baselineOutput,
                pieces,
                CreateProperties(),
                seeds) {
                PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic,
            });
            firstLevel = baseline.Levels[0];
        }
        catch (Exception error)
        {
            primary = error;
            throw;
        }
        finally
        {
            CleanupOutput(baselineOutput, primary);
        }

        var authoredObject = Assert.IsType<CathedralDepthPlaytestPoint>(firstLevel.ObjectPosition);
        var generated = AuthoritativeCathedralDepthGenerator.Generate(
            firstLevel.Depth, firstLevel.DungeonSeed, AuthoritativeCathedralEntry.Main);
        var safeDoorPosition = (X: authoredObject.X - 1, Y: authoredObject.Y);
        var safePieces = SetScannerPieceAt(pieces, generated, safeDoorPosition.X, safeDoorPosition.Y, 45);
        Assert.NotNull(safePieces);
        Assert.Equal((ushort)45, MaterializeGeneratedLevel(safePieces!, generated)
            .NativePieceLayer[safeDoorPosition.Y * AuthoritativeCathedralDepthGeometryMaterializer.NativeWidth
                + safeDoorPosition.X]);

        var safeOutput = NewOutput();
        primary = null;
        try
        {
            var safeProfile = CathedralDoorPlaytestCompiler.Compile(new CathedralDoorPlaytestCompilerOptions(
                basePack,
                safeOutput,
                safePieces!,
                CreateProperties(),
                seeds) {
                PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic,
            });
            var generatedPack = ContentPackLoader.LoadManifestDirectory(
                Path.Combine(safeOutput, CathedralDoorPlaytestCompiler.ContentDirectoryName));
            var content = ComposedContent.Compose(
                new ContentManifest(generatedPack.Id, generatedPack.Version, [generatedPack]));
            var activation = DiabloModule.Instance.CathedralDoorContentBootstrap.ActivateDoors(content);
            var doors = activation.Simulation.WorldState.CaptureCathedralDoorObjects();

            Assert.Equal(authoredObject, safeProfile.Levels[0].ObjectPosition!.Value);
            Assert.Contains(doors, door => door.LevelId == 1
                && door.PositionX == safeDoorPosition.X && door.PositionY == safeDoorPosition.Y);
            Assert.False(activation.Simulation.WorldState.IsCathedralDoorItemBlocked(
                1, authoredObject.X, authoredObject.Y));
            AssertNoStagingSiblings(safeOutput);
        }
        catch (Exception error)
        {
            primary = error;
            throw;
        }
        finally
        {
            CleanupOutput(safeOutput, primary);
        }

        var unsafeDoorPosition = (X: authoredObject.X + 1, Y: authoredObject.Y + 1);
        var unsafePieces = SetScannerPieceAt(pieces, generated,
            unsafeDoorPosition.X, unsafeDoorPosition.Y, 45);
        Assert.NotNull(unsafePieces);
        Assert.Equal((ushort)45, MaterializeGeneratedLevel(unsafePieces!, generated)
            .NativePieceLayer[unsafeDoorPosition.Y * AuthoritativeCathedralDepthGeometryMaterializer.NativeWidth
                + unsafeDoorPosition.X]);
        var unsafeOutput = NewOutput();
        primary = null;
        try
        {
            var error = Assert.Throws<InvalidDataException>(() => CathedralDoorPlaytestCompiler.Compile(
                new CathedralDoorPlaytestCompilerOptions(
                    basePack,
                    unsafeOutput,
                    unsafePieces!,
                    CreateProperties(),
                    seeds) {
                    PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic,
                }));

            Assert.Equal("Authored Cathedral population occupies a native door/light item footprint.", error.Message);
            Assert.False(Directory.Exists(unsafeOutput));
            AssertNoStagingSiblings(unsafeOutput);
        }
        catch (Exception error)
        {
            primary = error;
            throw;
        }
        finally
        {
            CleanupOutput(unsafeOutput, primary);
        }
    }

    private sealed record ScannerBoundaryFixture(
        string PieceDefinitionsTsv,
        int TargetDepth,
        int TargetScannerCount,
        IReadOnlyList<int> ExpectedScannerCounts,
        IReadOnlyList<AuthoritativeCathedralDepthResult> GeneratedLevels);

    private sealed record LightSelectionState(
        int TargetAdded,
        int[] AddedByDepth,
        LightSelectionState? Previous,
        int MegaTileId,
        int QuadrantCount);

    private static ScannerBoundaryFixture CreateScannerBoundaryFixture(
        IReadOnlyList<uint> seeds,
        int targetScannerCount)
    {
        if (targetScannerCount is not 127 and not 128)
            throw new ArgumentOutOfRangeException(nameof(targetScannerCount));

        var rows = CreateDoorPieces().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\t')).ToArray();
        var rowIndexes = rows.Skip(1).Select((row, index) => (row, index: index + 1))
            .ToDictionary(value => int.Parse(value.row[0], CultureInfo.InvariantCulture), value => value.index);
        var tileFrequencies = Enumerable.Range(0, 4)
            .Select(_ => new int[AuthoritativeDunTilePropertyCatalog.NativeTileCount]).ToArray();
        var generatedLevels = new AuthoritativeCathedralDepthResult[4];
        for (var depth = 0; depth < 4; depth++)
        {
            var generated = AuthoritativeCathedralDepthGenerator.Generate(
                depth + 1, seeds[depth], AuthoritativeCathedralEntry.Main);
            generatedLevels[depth] = generated;
            foreach (var tileId in generated.MegaTiles)
                tileFrequencies[depth][tileId]++;
        }

        var scannerCounts = new int[4];
        var doorCounts = new int[4];
        for (var depth = 0; depth < 4; depth++)
        {
            foreach (var (tileId, frequency) in tileFrequencies[depth].Select((count, id) => (id, count)))
            {
                if (frequency == 0)
                    continue;
                var row = rows[rowIndexes[tileId - 1]];
                var pieces = row.Skip(1).Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray();
                scannerCounts[depth] += frequency * pieces.Count(IsNativeScannerPiece);
                doorCounts[depth] += frequency * pieces.Count(IsNativeDoorPiece);
            }
        }

        var targetDepth = Array.FindIndex(doorCounts, count => count > 0);
        if (targetDepth < 0)
            throw new InvalidOperationException("The deterministic boundary fixture has no generated native door.");
        if (scannerCounts.Any(count => count > 127) || scannerCounts[targetDepth] >= targetScannerCount)
            throw new InvalidOperationException("The deterministic boundary fixture cannot reach the requested scanner capacity.");

        var needed = targetScannerCount - scannerCounts[targetDepth];
        var states = new LightSelectionState?[needed + 1];
        states[0] = new LightSelectionState(0, new int[4], null, -1, 0);
        for (var tileId = 0; tileId < tileFrequencies[targetDepth].Length; tileId++)
        {
            var targetFrequency = tileFrequencies[targetDepth][tileId];
            if (targetFrequency == 0)
                continue;
            var rowIndex = rowIndexes[tileId - 1];
            var rowPieces = rows[rowIndex].Skip(1)
                .Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray();
            // Only untouched floor rows are candidates; stairs, existing doors,
            // lights, and their authored frames retain their exact mapping.
            if (rowPieces.Any(piece => piece != 2))
                continue;

            var next = (LightSelectionState?[])states.Clone();
            for (var added = 0; added <= needed; added++)
            {
                var state = states[added];
                if (state is null)
                    continue;
                for (var quadrants = 1; quadrants <= 4; quadrants++)
                {
                    var targetIncrease = targetFrequency * quadrants;
                    if (targetIncrease > needed - added)
                        break;
                    var newAdded = added + targetIncrease;
                    var totals = (int[])state.AddedByDepth.Clone();
                    var acceptable = true;
                    for (var depth = 0; depth < 4; depth++)
                    {
                        totals[depth] += tileFrequencies[depth][tileId] * quadrants;
                        var depthLimit = depth == targetDepth ? targetScannerCount : 127;
                        if (scannerCounts[depth] + totals[depth] > depthLimit)
                            acceptable = false;
                    }
                    if (!acceptable)
                        continue;
                    var candidate = new LightSelectionState(newAdded, totals, state, tileId, quadrants);
                    if (next[newAdded] is null || IsBetterLightSelection(candidate, next[newAdded]!, targetDepth))
                        next[newAdded] = candidate;
                }
            }
            states = next;
        }

        var selected = states[needed]
            ?? throw new InvalidOperationException(
                $"No bounded floor-row mapping reaches the exact {targetScannerCount}-slot scanner boundary.");
        var selectedQuadrants = new Dictionary<int, int>();
        for (var state = selected; state.Previous is not null; state = state.Previous)
            selectedQuadrants.Add(state.MegaTileId, state.QuadrantCount);
        foreach (var (tileId, count) in selectedQuadrants)
        {
            var row = rows[rowIndexes[tileId - 1]];
            for (var quadrant = 1; quadrant <= count; quadrant++)
                row[quadrant] = "269";
        }

        var expected = scannerCounts.Select((count, depth) => count + selected.AddedByDepth[depth]).ToArray();
        if (expected[targetDepth] != targetScannerCount
            || expected.Where((_, depth) => depth != targetDepth).Any(count => count > 127))
            throw new InvalidOperationException("The bounded scanner fixture selection is inconsistent.");
        return new ScannerBoundaryFixture(
            string.Join('\n', rows.Select(row => string.Join('\t', row))) + "\n",
            targetDepth,
            targetScannerCount,
            Array.AsReadOnly(expected),
            Array.AsReadOnly(generatedLevels));
    }

    private static void AssertMaterializedScannerFixture(ScannerBoundaryFixture fixture)
    {
        var geometry = LoadDiagnosticGeometry(fixture.PieceDefinitionsTsv);
        for (var depth = 0; depth < fixture.GeneratedLevels.Count; depth++)
        {
            var materialized = AuthoritativeCathedralDepthGeometryMaterializer.Materialize(
                checked((uint)(depth + 1)),
                fixture.GeneratedLevels[depth],
                geometry.Pieces,
                geometry.TileProperties,
                $"cathedral_depth_{depth + 1}",
                isDefaultStart: false);
            var level = materialized.Level;
            var scannerPieceCount = level.NativePieceLayer.Count(piece => IsNativeScannerPiece(piece));
            Assert.Equal(fixture.ExpectedScannerCounts[depth], scannerPieceCount);
            if (scannerPieceCount <= 127)
            {
                var discovered = AuthoritativeCathedralDoors.Discover(level);
                var doorCount = discovered.Doors.Count;
                var lightCount = level.NativePieceLayer.Count(piece => piece == 269);
                Assert.Equal(scannerPieceCount, doorCount + lightCount);
            }
            if (depth == fixture.TargetDepth)
            {
                Assert.Equal(fixture.TargetScannerCount, scannerPieceCount);
                Assert.Contains((ushort)269, level.NativePieceLayer);
                Assert.Contains(level.NativePieceLayer, piece => IsNativeDoorPiece(piece));
            }
        }
    }

    private static AuthoritativeLevel MaterializeGeneratedLevel(
        string pieceDefinitions,
        AuthoritativeCathedralDepthResult generated)
    {
        var geometry = LoadDiagnosticGeometry(pieceDefinitions);
        return AuthoritativeCathedralDepthGeometryMaterializer.Materialize(
            checked((uint)generated.Depth),
            generated,
            geometry.Pieces,
            geometry.TileProperties,
            $"cathedral_depth_{generated.Depth}",
            isDefaultStart: false).Level;
    }

    private static AuthoritativeNativeDunGeometryCatalog LoadDiagnosticGeometry(string pieceDefinitions)
        => AuthoritativeNativeDunGeometryCatalog.LoadTsv(
            AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath,
            pieceDefinitions,
            AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath,
            CreateProperties());

    private static bool IsBetterLightSelection(
        LightSelectionState candidate,
        LightSelectionState current,
        int targetDepth)
    {
        var candidateMaximum = candidate.AddedByDepth.Where((_, depth) => depth != targetDepth).Max();
        var currentMaximum = current.AddedByDepth.Where((_, depth) => depth != targetDepth).Max();
        if (candidateMaximum != currentMaximum)
            return candidateMaximum < currentMaximum;
        var candidateTotal = candidate.AddedByDepth.Where((_, depth) => depth != targetDepth).Sum();
        var currentTotal = current.AddedByDepth.Where((_, depth) => depth != targetDepth).Sum();
        if (candidateTotal != currentTotal)
            return candidateTotal < currentTotal;
        for (var depth = 0; depth < candidate.AddedByDepth.Length; depth++)
        {
            if (depth == targetDepth || candidate.AddedByDepth[depth] == current.AddedByDepth[depth])
                continue;
            return candidate.AddedByDepth[depth] < current.AddedByDepth[depth];
        }
        return false;
    }

    private static bool IsNativeDoorPiece(int piece)
        => piece is 43 or 45 or 50 or 55 or 213;

    private static bool IsNativeScannerPiece(int piece)
        => piece == 269 || IsNativeDoorPiece(piece);

    private static string? SetScannerPieceAt(
        string pieceDefinitions,
        AuthoritativeCathedralDepthResult generated,
        int x,
        int y,
        int scannerPiece)
    {
        var coarseX = x - AuthoritativeCathedralLevel1GeometryMaterializer.InteriorOffset;
        var coarseY = y - AuthoritativeCathedralLevel1GeometryMaterializer.InteriorOffset;
        if (coarseX < 0 || coarseX >= AuthoritativeCathedralDepthResult.MegaWidth * 2
            || coarseY < 0 || coarseY >= AuthoritativeCathedralDepthResult.MegaHeight * 2)
            return null;
        var megaX = coarseX / 2;
        var megaY = coarseY / 2;
        var megaTileId = generated.GetMegaTile(megaX, megaY);
        if (megaTileId == 0)
            return null;
        var rows = pieceDefinitions.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\t')).ToArray();
        var catalogMegaTileId = megaTileId - 1;
        var index = Array.FindIndex(rows, 1,
            row => int.Parse(row[0], CultureInfo.InvariantCulture) == catalogMegaTileId);
        if (index < 0)
            return null;
        var quadrant = (coarseY % 2) * 2 + coarseX % 2;
        if (int.Parse(rows[index][quadrant + 1], CultureInfo.InvariantCulture) != 2)
            return null;
        rows[index][quadrant + 1] = scannerPiece.ToString(CultureInfo.InvariantCulture);
        return string.Join('\n', rows.Select(row => string.Join('\t', row))) + "\n";
    }

    [Fact]
    public void RejectsAuthoredObjectOnScannerDoorBeforeUsingCorruptedPack()
    {
        var basePack = LoadBasePack();
        var output = NewOutput();
        Exception? primary = null;
        try
        {
            _ = CathedralDoorPlaytestCompiler.Compile(
                new CathedralDoorPlaytestCompilerOptions(
                    basePack,
                    output,
                    CreateDoorPieces(),
                    CreateProperties(),
                    [1U, 2U, 3U, 4U]) {
                    PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.None,
                });
            var generated = ContentPackLoader.LoadManifestDirectory(
                Path.Combine(output, CathedralDoorPlaytestCompiler.ContentDirectoryName));
            var content = ComposedContent.Compose(
                new ContentManifest(generated.Id, generated.Version, [generated]));
            var baseline = DiabloModule.Instance.CathedralDoorContentBootstrap.ActivateDoors(content);
            var doors = baseline.Simulation.WorldState.CaptureCathedralDoorObjects();
            Assert.NotEmpty(doors);
            var door = doors[0];
            var objects = generated.Tables.Single(table => table.SourcePath == "objects.tsv");
            var template = objects.Rows.Single(row => row.RequiredUInt32("entity_id") == 2001);
            var values = template.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            values["entity_id"] = "29999";
            values["level_id"] = door.LevelId.ToString(CultureInfo.InvariantCulture);
            values["level_symbol"] = "cathedral";
            values["position_x"] = door.PositionX.ToString(CultureInfo.InvariantCulture);
            values["position_y"] = door.PositionY.ToString(CultureInfo.InvariantCulture);
            values["template_only"] = "0";
            var corruptedObjects = TsvTable.Parse(
                objects.SourcePath,
                string.Join('\t', objects.Columns) + "\n"
                    + objects.ToCanonicalTsv().Split('\n', 2)[1]
                    + string.Join('\t', objects.Columns.Select(column => values[column])) + "\n");
            var corrupted = ReplaceTable(generated, corruptedObjects);
            var corruptedContent = ComposedContent.Compose(
                new ContentManifest(corrupted.Id, corrupted.Version, [corrupted]));

            var error = Assert.Throws<InvalidDataException>(() =>
                DiabloModule.Instance.CathedralDoorContentBootstrap.ActivateDoors(corruptedContent));
            Assert.Equal("The door profile excludes overlapping authored scanner objects.", error.Message);
            Assert.True(File.Exists(Path.Combine(output, CathedralDoorPlaytestCompiler.ProfileFileName)));
            AssertNoStagingSiblings(output);
        }
        catch (Exception error)
        {
            primary = error;
            throw;
        }
        finally
        {
            CleanupOutput(output, primary);
        }
    }

    private static ContentPack ReplaceTable(ContentPack source, TsvTable replacement)
        => new(source.Id, source.Version,
            source.Tables.Select(table => table.SourcePath == replacement.SourcePath ? replacement : table).ToArray(),
            source.Dependencies, source.Overlays, source.Schema, source.DependencyConstraints, source.Assets);

    private static IReadOnlyList<string> SnapshotTables(ContentPack pack)
        => pack.Tables.OrderBy(table => table.SourcePath, StringComparer.Ordinal)
            .Select(table => table.SourcePath + "\0" + table.ToCanonicalTsv()).ToArray();

    private static IReadOnlyList<string> SnapshotFiles(string root)
        => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => {
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
                return relative + "\0" + hash;
            })
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();

    private static string ComputePackFileHash(string root)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Select(path => new {
                         FullPath = path,
                         Relative = Path.GetRelativePath(root, path).Replace('\\', '/'),
                     })
                     .OrderBy(file => file.Relative, StringComparer.Ordinal))
        {
            var fileHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path.FullPath)));
            var record = $"{path.Relative.Length}:{path.Relative}:{fileHash}\n";
            hash.AppendData(Encoding.UTF8.GetBytes(record));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string CreateDoorPieces()
    {
        var lines = new List<string> {
            "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east",
        };
        for (var id = 0; id < AuthoritativeDunTilePropertyCatalog.NativeTileCount; id++)
        {
            var pieces = id switch {
                25 or 26 => "42\t45\t43\t2", // paired synthetic doors with an unreserved item footprint
                63 => "128\t2\t2\t2", // stair-up marker used by the depth fixture
                58 => "114\t2\t2\t2", // stair-down marker used by the depth fixture
                21 => "1\t2\t2\t2", // labeled blocked diagnostic cell
                129 => "42\t45\t43\t269", // paired doors plus one light leave an unreserved footprint
                _ => "2\t2\t2\t2",
            };
            lines.Add($"{id}\t{pieces}");
        }
        return string.Join('\n', lines) + "\n";
    }

    private static string CreateOverflowPieces()
    {
        var lines = new List<string> {
            "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east",
        };
        for (var id = 0; id < AuthoritativeDunTilePropertyCatalog.NativeTileCount; id++)
        {
            var pieces = id switch {
                63 => "128\t2\t2\t2",
                58 => "114\t2\t2\t2",
                21 => "1\t2\t2\t2",
                _ => "269\t269\t269\t269",
            };
            lines.Add($"{id}\t{pieces}");
        }
        return string.Join('\n', lines) + "\n";
    }

    private static string CreateProperties()
    {
        var lines = new List<string> {
            "piece_id\tsolid\tblock_light\tblock_missile\ttransparent\ttransparent_left\ttransparent_right\ttrap",
        };
        for (var id = 0; id < AuthoritativeDunTilePropertyCatalog.NativeTileCount; id++)
            lines.Add($"{id}\t{id == 1}\tfalse\tfalse\tfalse\tfalse\tfalse\tfalse");
        return string.Join('\n', lines) + "\n";
    }

    private static ContentPack LoadBasePack()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return ContentPackLoader.LoadManifestDirectory(
                    Path.Combine(directory.FullName, "server", "content", "base"));
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static string NewOutput()
        => Path.Combine(Path.GetTempPath(), "godevilution-cathedral-door-portable-"
            + Guid.NewGuid().ToString("N"));

    private static void AssertNoStagingSiblings(string output)
    {
        var full = Path.GetFullPath(output);
        var parent = Directory.GetParent(full)
            ?? throw new InvalidOperationException("Temporary output has no parent.");
        var name = Path.GetFileName(full);
        Assert.Empty(Directory.EnumerateDirectories(parent.FullName, name + ".door-staging-*"));
        Assert.Empty(Directory.EnumerateDirectories(parent.FullName, name + ".staging-*"));
    }

    private static void CleanupOutputs(string first, string second, Exception? primary)
    {
        ExceptionDispatchInfo? cleanupFailure = null;
        try
        {
            CleanupOutput(first, primary);
        }
        catch (Exception error)
        {
            cleanupFailure = ExceptionDispatchInfo.Capture(error);
        }
        try
        {
            CleanupOutput(second, primary);
        }
        catch (Exception error)
        {
            cleanupFailure ??= ExceptionDispatchInfo.Capture(error);
        }
        if (primary is null && cleanupFailure is not null)
            cleanupFailure.Throw();
    }

    private static void CleanupOutput(string output, Exception? primary)
    {
        try
        {
            DeleteOutput(output);
        }
        catch (Exception error)
        {
            if (primary is null)
                throw;
            // Keep a primary assertion failure intact and leave the fixture for diagnosis.
            try
            {
                primary.Data[$"SyntheticFixtureCleanup:{output}"] = error;
            }
            catch
            {
                // A diagnostic attachment must never replace the primary failure.
            }
            try
            {
                Console.Error.WriteLine($"Synthetic fixture cleanup failed for '{output}': {error.Message}");
            }
            catch
            {
                // Diagnostic output is best effort and must not replace the primary failure.
            }
        }
    }

    private static void DeleteOutput(string output)
    {
        var target = Path.GetFullPath(output);
        var tempRoot = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!target.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(target).StartsWith("godevilution-cathedral-door-portable-",
                StringComparison.Ordinal))
            throw new InvalidOperationException("Unsafe synthetic fixture cleanup target.");
        var tempDirectory = tempRoot.TrimEnd(Path.DirectorySeparatorChar);
        var targetInfo = new DirectoryInfo(target);
        var ancestor = targetInfo.Exists ? targetInfo : targetInfo.Parent;
        while (ancestor is not null)
        {
            if ((ancestor.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Synthetic fixture cleanup refuses reparse points.");
            if (string.Equals(ancestor.FullName.TrimEnd(Path.DirectorySeparatorChar),
                    tempDirectory, StringComparison.OrdinalIgnoreCase))
                break;
            ancestor = ancestor.Parent;
        }
        if (ancestor is null)
            throw new InvalidOperationException("Synthetic fixture cleanup escaped the temp directory.");
        if (!targetInfo.Exists)
            return;

        var directories = new Stack<DirectoryInfo>([new DirectoryInfo(target)]);
        while (directories.Count != 0)
        {
            var directory = directories.Pop();
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Synthetic fixture cleanup refuses reparse points.");
            foreach (var entry in directory.EnumerateFileSystemInfos())
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Synthetic fixture cleanup refuses reparse points.");
                if (entry is DirectoryInfo child)
                    directories.Push(child);
            }
        }
        Directory.Delete(target, recursive: true);
    }
}

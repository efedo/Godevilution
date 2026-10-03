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

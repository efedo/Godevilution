using Devilution.Data;
using Godevilution.Tools;
using System.Diagnostics;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class CathedralL1PlaytestCompilerTests
{
    [Theory]
    [InlineData(0U)]
    [InlineData(2588U)]
    public void CompilesFreshPackWithReachableDiagnosticPopulationWithoutMutatingBase(uint seed)
    {
        var basePack = LoadBasePack();
        var originalMonster = basePack.Tables.Single(table => table.SourcePath == "monsters.tsv")
            .Rows.Single(row => row.RequiredUInt32("entity_id") == CathedralL1PlaytestCompiler.MonsterEntityId);
        var generated = AuthoritativeCathedralLevel1Generator.Generate(seed, AuthoritativeCathedralEntry.Main);
        var output = Path.Combine(Path.GetTempPath(), "godevilution-cathedral-playtest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var profile = CathedralL1PlaytestCompiler.Compile(
                new CathedralL1PlaytestCompilerOptions(
                    basePack,
                    output,
                    CreatePieces(generated),
                    CreateProperties(),
                    seed,
                    AuthoritativeCathedralEntry.Main));

            Assert.Equal(CathedralL1PlaytestCompiler.AdapterId, profile.AdapterId);
            Assert.Equal("main", profile.Entry);
            Assert.Equal(CathedralL1PlaytestCompiler.ObjectEntityId, profile.ObjectEntityId);
            Assert.Equal(CathedralL1PlaytestCompiler.DropEntityId, profile.DropEntityId);
            Assert.True(profile.TraversalPathDistance >= 7);
            Assert.True(profile.BlockedFineCell != profile.SafeResumePosition);
            Assert.True(File.Exists(Path.Combine(output, CathedralL1PlaytestCompiler.ProfileFileName)));
            var generatedPack = ContentPackLoader.LoadManifestDirectory(
                Path.Combine(output, CathedralL1PlaytestCompiler.ContentDirectoryName));
            Assert.Equal("godevilution.cathedral-playtest", profile.ContentId);
            var activation = DiabloModule.Instance.NativeGeneratedContentBootstrap.ActivateGenerated(
                ComposedContent.Compose(new ContentManifest(
                    generatedPack.Id,
                    generatedPack.Version,
                    [generatedPack])),
                profile.DungeonSeed);
            var generatedLevel = activation.Simulation.WorldState.World!.Levels.Single(level => level.LevelId == 1);
            Assert.Equal((112, 112), (generatedLevel.Width, generatedLevel.Height));
            Assert.True(generatedLevel.IsWalkable(profile.ObjectPosition.X, profile.ObjectPosition.Y));
            Assert.True(generatedLevel.IsWalkable(profile.MonsterPosition.X, profile.MonsterPosition.Y));
            Assert.False(generatedLevel.IsWalkable(profile.BlockedFineCell.X, profile.BlockedFineCell.Y));
            var returnWarp = activation.Simulation.WorldState.World.WarpTargets.Single(warp => warp.WarpId == 1099);
            Assert.Equal((profile.UpTrigger.X, profile.UpTrigger.Y), (returnWarp.TriggerX, returnWarp.TriggerY));
            Assert.Equal((25, 31), (returnWarp.TargetX, returnWarp.TargetY));
            var entryWarp = activation.Simulation.WorldState.World.WarpTargets.Single(warp => warp.WarpId == 1001);
            Assert.Equal((25, 29), (entryWarp.TriggerX, entryWarp.TriggerY));
            Assert.Equal((profile.MainEntry.X, profile.MainEntry.Y), (entryWarp.TargetX, entryWarp.TargetY));
            var monster = generatedPack.Tables.Single(table => table.SourcePath == "monsters.tsv")
                .Rows.Single(row => row.RequiredUInt32("entity_id") == CathedralL1PlaytestCompiler.MonsterEntityId);
            Assert.Equal(profile.MonsterPosition.X.ToString(), monster.Required("position_x"));
            Assert.Equal(profile.MonsterPosition.Y.ToString(), monster.Required("position_y"));
            Assert.Equal("6001", monster.Required("drop_item_seed"));
            var objects = generatedPack.Tables.Single(table => table.SourcePath == "objects.tsv");
            var marker = objects.Rows.Single(row => row.RequiredUInt32("entity_id") == CathedralL1PlaytestCompiler.ObjectEntityId);
            Assert.Equal("Heal", marker.Required("effect_kind"));
            Assert.Equal("0", marker.Required("effect_amount"));
            Assert.Equal("1", marker.Required("missile_passable"));
            Assert.Equal("0", marker.Required("template_only"));
            var door = objects.Rows.Single(row => row.RequiredUInt32("entity_id") == 2001);
            Assert.Equal("1", door.Required("template_only"));
            Assert.Empty(generatedPack.Tables.Single(table => table.SourcePath == "native_dun_fragments.tsv").Rows);
            Assert.Empty(generatedPack.Tables.Single(table => table.SourcePath == "native_dun_fragment_objects.tsv").Rows);
            var starts = generatedPack.Tables.Single(table => table.SourcePath == "level_start_positions.tsv");
            var townStart = Assert.Single(starts.Rows);
            Assert.Equal("0", townStart.Required("level_id"));
            Assert.Equal("75", townStart.Required("start_x"));
            Assert.Equal("68", townStart.Required("start_y"));
            Assert.Equal("1", originalMonster.Required("position_x"));
            Assert.Equal("0", originalMonster.Required("position_y"));
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void RejectsNonFreshOutputDirectoryBeforeWriting()
    {
        var basePack = LoadBasePack();
        var output = Path.Combine(Path.GetTempPath(), "godevilution-cathedral-playtest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        try
        {
            var generated = AuthoritativeCathedralLevel1Generator.Generate(2588, AuthoritativeCathedralEntry.Main);
            Assert.Throws<IOException>(() => CathedralL1PlaytestCompiler.Compile(
                new CathedralL1PlaytestCompilerOptions(
                    basePack,
                    output,
                    CreatePieces(generated),
                    CreateProperties())));
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void RejectsMalformedSemanticGeometryBeforeCreatingOutput()
    {
        var basePack = LoadBasePack();
        var output = Path.Combine(Path.GetTempPath(), "godevilution-cathedral-playtest-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Throws<InvalidDataException>(() => CathedralL1PlaytestCompiler.Compile(
                new CathedralL1PlaytestCompilerOptions(
                    basePack,
                    output,
                    "mega_tile_id\tpiece_north_west\n0\t0\n",
                    CreateProperties())));
            Assert.False(Directory.Exists(output));
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void RejectsReparsePointOutputPathBeforeWriting()
    {
        var basePack = LoadBasePack();
        var root = Path.Combine(Path.GetTempPath(), "godevilution-cathedral-reparse-" + Guid.NewGuid().ToString("N"));
        var target = Path.Combine(root, "target");
        var link = Path.Combine(root, "link");
        Directory.CreateDirectory(target);
        try
        {
            try
            {
                if (OperatingSystem.IsWindows()) {
                    var command = $"New-Item -ItemType Junction -Path '{link.Replace("'", "''", StringComparison.Ordinal)}' -Target '{target.Replace("'", "''", StringComparison.Ordinal)}' | Out-Null";
                    var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true };
                    start.ArgumentList.Add("-NoProfile");
                    start.ArgumentList.Add("-NonInteractive");
                    start.ArgumentList.Add("-Command");
                    start.ArgumentList.Add(command);
                    using var child = Process.Start(start)!;
                    try {
                        Assert.True(child.WaitForExit(10000), "Creating the private test junction timed out.");
                        Assert.Equal(0, child.ExitCode);
                        Assert.True(Directory.Exists(link));
                    } finally {
                        if (!child.HasExited) {
                            child.Kill();
                            Assert.True(child.WaitForExit(5000), "The owned junction helper did not terminate.");
                        }
                    }
                } else {
                    Directory.CreateSymbolicLink(link, target);
                }
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or PlatformNotSupportedException
                or NotSupportedException)
            {
                Assert.Skip($"Symbolic-link test requires link creation permission: {exception.Message}");
                return;
            }

            var generated = AuthoritativeCathedralLevel1Generator.Generate(2588, AuthoritativeCathedralEntry.Main);
            Assert.Throws<InvalidDataException>(() => CathedralL1PlaytestCompiler.Compile(
                new CathedralL1PlaytestCompilerOptions(
                    basePack,
                    link,
                    CreatePieces(generated),
                    CreateProperties())));
            Assert.Empty(Directory.EnumerateFileSystemEntries(target));
        }
        finally
        {
            if (Directory.Exists(link))
                Directory.Delete(link);
            if (Directory.Exists(target))
                Directory.Delete(target);
            if (Directory.Exists(root))
                Directory.Delete(root);
        }
    }

    [Fact(Explicit = true)]
    public void CompilesPrivateNativeOracleIntoFreshPlaytestProfile()
    {
        var oracleDirectory = Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_ORACLE_DIRECTORY");
        var output = Environment.GetEnvironmentVariable("GODEVILUTION_CATHEDRAL_PLAYTEST_DIRECTORY");
        Assert.False(string.IsNullOrWhiteSpace(oracleDirectory));
        Assert.False(string.IsNullOrWhiteSpace(output));
        Assert.False(Directory.Exists(output));

        var pieces = AuthoritativeDunPieceCatalog.LoadTil(
            "private-oracle:l1.til",
            File.ReadAllBytes(Path.Combine(oracleDirectory!, "l1.til")));
        var rawSol = File.ReadAllBytes(Path.Combine(oracleDirectory!, "l1.sol"));
        Assert.InRange(rawSol.Length, 1, AuthoritativeDunTilePropertyCatalog.NativeTileCount);
        // Test-fixture-only normalization: zero extend .sol and mask its
        // unnamed native 0x40 bit before serializing semantic TSV input.
        var semanticSol = new byte[AuthoritativeDunTilePropertyCatalog.NativeTileCount];
        for (var index = 0; index < rawSol.Length; index++)
            semanticSol[index] = (byte)(rawSol[index] & 0xBF);
        var properties = AuthoritativeDunTilePropertyCatalog.LoadSol(
            "private-oracle:semantic-l1.sol", semanticSol);
        var profile = CathedralL1PlaytestCompiler.Compile(
            new CathedralL1PlaytestCompilerOptions(
                LoadBasePack(),
                output!,
                SerializePieces(pieces),
                SerializeProperties(properties),
                2588,
                AuthoritativeCathedralEntry.Main));

        Assert.Equal("godevilution.cathedral-playtest", profile.ContentId);
        var generatedPack = ContentPackLoader.LoadManifestDirectory(
            Path.Combine(output!, CathedralL1PlaytestCompiler.ContentDirectoryName));
        Assert.Empty(generatedPack.Tables.Single(table => table.SourcePath == "native_dun_fragments.tsv").Rows);
        Assert.Empty(generatedPack.Tables.Single(table => table.SourcePath == "native_dun_fragment_objects.tsv").Rows);
        Assert.True(File.Exists(Path.Combine(output!, CathedralL1PlaytestCompiler.ProfileFileName)));
    }

    private static string CreatePieces(AuthoritativeCathedralLevel1Result generated)
    {
        var unique = generated.MegaTiles
            .GroupBy(tile => tile)
            .Where(group => group.Key != AuthoritativeCathedralLevel1GeometryMaterializer.DefaultExteriorMegaTileId
                && group.Count() == 1)
            .Select(group => (uint)group.Key - 1)
            .Take(2)
            .ToArray();
        Assert.Equal(2, unique.Length);
        var blockedMega = generated.MegaTiles
            .GroupBy(tile => tile)
            .Where(group => group.Key != AuthoritativeCathedralLevel1GeometryMaterializer.DefaultExteriorMegaTileId
                && !unique.Contains((uint)group.Key - 1))
            .Select(group => (uint)group.Key - 1)
            .First();
        var lines = new List<string> {
            "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east",
        };
        for (var id = 0; id < AuthoritativeDunTilePropertyCatalog.NativeTileCount; id++)
        {
            var northWest = (uint)id == unique[0]
                ? 128
                : (uint)id == unique[1]
                    ? 114
                    : (uint)id == blockedMega ? 1 : 2;
            lines.Add($"{id}\t{northWest}\t2\t2\t2");
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

    private static string SerializePieces(AuthoritativeDunPieceCatalog catalog)
    {
        var lines = new List<string> {
            "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east",
        };
        lines.AddRange(catalog.Definitions.Select(piece =>
            $"{piece.MegaTileId}\t{piece.NorthWestPieceId}\t{piece.NorthEastPieceId}\t"
            + $"{piece.SouthWestPieceId}\t{piece.SouthEastPieceId}"));
        return string.Join('\n', lines) + "\n";
    }

    private static string SerializeProperties(AuthoritativeDunTilePropertyCatalog catalog)
    {
        var flags = new[] {
            AuthoritativeDunTileFlags.Solid,
            AuthoritativeDunTileFlags.BlockLight,
            AuthoritativeDunTileFlags.BlockMissile,
            AuthoritativeDunTileFlags.Transparent,
            AuthoritativeDunTileFlags.TransparentLeft,
            AuthoritativeDunTileFlags.TransparentRight,
            AuthoritativeDunTileFlags.Trap,
        };
        var lines = new List<string> {
            "piece_id\tsolid\tblock_light\tblock_missile\ttransparent\ttransparent_left\ttransparent_right\ttrap",
        };
        lines.AddRange(catalog.Definitions.Select(property => property.PieceId + "\t"
            + string.Join('\t', flags.Select(flag => property.Flags.HasFlag(flag) ? "true" : "false"))));
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
}

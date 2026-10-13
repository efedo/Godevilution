using System.Text.Json;
using Devilution.Data;
using Godevilution.Tools;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Offline Cathedral depths profile, remapping, and publication guards. */
public sealed class CathedralDepthPlaytestCompilerTests
{
    [Fact]
    public void CompilesFourDepthProfileWithOrderedSeedsAndStrictRouteFamily()
    {
        var basePack = LoadBasePack();
        var seeds = new uint[] { 0, 2588, 17, uint.MaxValue };
        var output = NewOutput();
        try
        {
            var profile = CathedralDepthPlaytestCompiler.Compile(
                new CathedralDepthPlaytestCompilerOptions(
                    basePack,
                    output,
                    CreatePieces(seeds),
                    CreateProperties(),
                    seeds));

            Assert.Equal(1, profile.FormatVersion);
            Assert.Equal(CathedralDepthPlaytestCompiler.AdapterId, profile.AdapterId);
            Assert.Equal("godevilution.cathedral-depths-playtest", profile.ContentId);
            Assert.Equal(seeds, profile.DepthSeeds);
            Assert.False(profile.NativePopulationClaim);
            Assert.Equal("authored-diagnostic", profile.PlacementPolicy);
            Assert.Equal(4, profile.ReadonlyLevels.Count);
            Assert.Equal(
                new[] { "cathedral", "cathedral_depth_2", "cathedral_depth_3", "cathedral_depth_4" },
                profile.Levels.Select(level => level.LevelSymbol));
            Assert.Equal(seeds, profile.Levels.Select(level => level.DungeonSeed));
            Assert.Equal(8, profile.Routes.Count);
            Assert.Equal(
                new uint[] { 1001, 1002, 1003, 1004, 1099, 1100, 1101, 1102 },
                profile.Routes.Select(route => route.RouteId));
            Assert.Equal(new uint?[] { 1099, 1100, 1101, 1102 },
                profile.Levels.Select(level => level.UpWarpId));
            Assert.Equal(new uint?[] { 1002, 1003, 1004, null },
                profile.Levels.Select(level => level.DownWarpId));

            var pack = ContentPackLoader.LoadManifestDirectory(
                Path.Combine(output, CathedralDepthPlaytestCompiler.ContentDirectoryName));
            var catalog = pack.Tables.Single(table =>
                table.SourcePath == AuthoritativeCathedralDepthRuntimeCatalog.TableName);
            Assert.Equal(
                new[] {
                    "level_id", "level_symbol", "depth", "generator_id",
                    "generator_revision", "dungeon_seed", "entry", "policy",
                },
                catalog.Columns);
            Assert.Equal(new uint[] { 1, 2, 3, 4 }, catalog.Rows.Select(row => row.RequiredUInt32("level_id")));
            Assert.All(catalog.Rows, row => Assert.Equal(
                CathedralDepthPlaytestCompiler.Policy,
                row.Required("policy")));

            var levels = pack.Tables.Single(table => table.SourcePath == "levels.tsv");
            Assert.Equal(
                new[] { "town", "cathedral", "cathedral_depth_2", "cathedral_depth_3", "cathedral_depth_4" },
                levels.Rows.Select(row => row.Required("level_symbol")));
            var warps = pack.Tables.Single(table => table.SourcePath == "warps.tsv");
            Assert.Equal(8, warps.Rows.Count);
            Assert.DoesNotContain(warps.Rows, row => row.Required("target_level_symbol") is
                "catacombs" or "caves" or "hell" or "cathedral_depth_5");
            Assert.Empty(pack.Tables.Single(table => table.SourcePath == "native_dun_fragments.tsv").Rows);
            var defaultDrops = pack.Tables.Single(table => table.SourcePath == "monsters.tsv").Rows;
            Assert.Equal(4, defaultDrops.Count);
            Assert.All(defaultDrops, row => {
                Assert.Equal(1U, row.RequiredUInt32("drop_item_id"));
                Assert.Equal("short_sword", row.Required("drop_item_symbol"));
            });
            using (var profileJson = JsonDocument.Parse(File.ReadAllText(
                       Path.Combine(output, CathedralDepthPlaytestCompiler.ProfileFileName))))
            {
                Assert.False(profileJson.RootElement.TryGetProperty("diagnosticDropItemSymbol", out _));
                Assert.False(profileJson.RootElement.TryGetProperty("townPortalRoute", out _));
                Assert.False(profileJson.RootElement.TryGetProperty("effectiveInputManifestHash", out _));
            }
            Assert.DoesNotContain(pack.Tables, table => table.SourcePath == "procedural_levels.tsv");
            Assert.DoesNotContain(pack.Tables, table => table.SourcePath is
                "quest_types.tsv" or "unique_monsters.tsv" or "unique_monster_placements.tsv"
                or "unique_monster_packs.tsv" or "set_level_maps.tsv" or "set_level_objects.tsv");
            Assert.Empty(pack.Tables.Single(table => table.SourcePath == "portals.tsv").Rows);
            Assert.Empty(pack.Tables.Single(table => table.SourcePath == "quests.tsv").Rows);
            var ids = pack.Tables.Single(table => table.SourcePath == "content_ids.tsv");
            Assert.DoesNotContain(ids.Rows, row => row.Required("kind") is
                "quest" or "portal" or "set_level" or "procedural_origin" or "unique_monster");
            Assert.DoesNotContain(ids.Rows, row => row.Required("kind") == "level"
                && row.Required("symbol") is "catacombs" or "caves" or "hell");
            Assert.Equal(profile.Routes.Select(route => route.RouteSymbol).Order(),
                ids.Rows.Where(row => row.Required("kind") == "warp")
                    .Select(row => row.Required("symbol")).Order());
            Assert.True(File.Exists(Path.Combine(output, CathedralDepthPlaytestCompiler.ProfileFileName)));
        }
        finally
        {
            DeleteOutput(output);
        }
    }

    [Fact]
    public void AuthoredDiagnosticDropItemUsesValidatedStableBaseItemIdentity()
    {
        var output = NewOutput();
        try
        {
            var profile = CathedralDepthPlaytestCompiler.Compile(
                new CathedralDepthPlaytestCompilerOptions(
                    LoadBasePack(),
                    output,
                    CreatePieces([1, 2, 3, 4]),
                    CreateProperties(),
                    [1U, 2U, 3U, 4U]) {
                    PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic,
                    DiagnosticDropItemSymbol = "short_sword_standard",
                });

            Assert.Equal("short_sword_standard", profile.DiagnosticDropItemSymbol);
            var pack = ContentPackLoader.LoadManifestDirectory(
                Path.Combine(output, CathedralDepthPlaytestCompiler.ContentDirectoryName));
            var drops = pack.Tables.Single(table => table.SourcePath == "monsters.tsv").Rows;
            Assert.Equal(4, drops.Count);
            Assert.All(drops, row => {
                Assert.Equal(2160U, row.RequiredUInt32("drop_item_id"));
                Assert.Equal("short_sword_standard", row.Required("drop_item_symbol"));
            });
            using var profileJson = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(output, CathedralDepthPlaytestCompiler.ProfileFileName)));
            Assert.Equal("short_sword_standard",
                profileJson.RootElement.GetProperty("diagnosticDropItemSymbol").GetString());
        }
        finally
        {
            DeleteOutput(output);
        }
    }

    [Fact]
    public void TownPortalRouteComposesTheExplicitPreviewChainAndPreservesAuthoredStock()
    {
        var basePack = LoadBasePack();
        var (townLoopPreview, townPortalPreview) = LoadTownPreviewPacks();
        var inputManifest = new ContentManifest(
            basePack.Id,
            basePack.Version,
            [basePack, townLoopPreview, townPortalPreview]);
        var output = NewOutput();
        try
        {
            var profile = CathedralDepthPlaytestCompiler.Compile(
                new CathedralDepthPlaytestCompilerOptions(
                    basePack,
                    output,
                    CreatePieces([1, 2, 3, 4]),
                    CreateProperties(),
                    [1U, 2U, 3U, 4U]) {
                    PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic,
                    DiagnosticDropItemSymbol = "short_sword_standard",
                    TownPortalRoute = CathedralDepthPlaytestCompiler.TownPortalRouteMarker,
                    TownLoopPreviewPack = townLoopPreview,
                    TownPortalPreviewPack = townPortalPreview,
                });

            Assert.Equal(CathedralDepthPlaytestCompiler.TownPortalRouteMarker, profile.TownPortalRoute);
            Assert.Equal(inputManifest.Sha256, profile.EffectiveInputManifestHash);
            var compiled = ContentPackLoader.LoadManifestDirectory(
                Path.Combine(output, CathedralDepthPlaytestCompiler.ContentDirectoryName));
            var stores = compiled.Tables.Single(table => table.SourcePath == "stores.tsv");
            var portalStock = Assert.Single(stores.Rows, row =>
                row.RequiredUInt32("store_id") == 1 && row.RequiredUInt32("store_slot") == 2);
            Assert.Equal("smith", portalStock.Required("store_symbol"));
            Assert.Equal("Smith", portalStock.Required("store_role"));
            Assert.Equal(9100U, portalStock.RequiredUInt32("item_seed"));
            Assert.Equal(50U, portalStock.RequiredUInt32("price"));
            Assert.Equal(2119U, portalStock.RequiredUInt32("item_id"));
            Assert.Equal("scroll_of_town_portal", portalStock.Required("item_symbol"));

            var originalLoadouts = basePack.Tables.Single(table => table.SourcePath == "class_loadouts.tsv");
            Assert.Equal(originalLoadouts.ToCanonicalTsv(),
                compiled.Tables.Single(table => table.SourcePath == "class_loadouts.tsv").ToCanonicalTsv());
            Assert.Equal("100", originalLoadouts.Rows.Single(row => row.Required("class_symbol") == "warrior")
                .Required("starting_gold"));
            Assert.All(compiled.Tables.Single(table => table.SourcePath == "monsters.tsv").Rows,
                row => Assert.Equal("short_sword_standard", row.Required("drop_item_symbol")));
            Assert.Equal(8, compiled.Tables.Single(table => table.SourcePath == "warps.tsv").Rows.Count);
            Assert.Empty(compiled.Tables.Single(table => table.SourcePath == "portals.tsv").Rows);

            using var profileJson = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(output, CathedralDepthPlaytestCompiler.ProfileFileName)));
            Assert.Equal(CathedralDepthPlaytestCompiler.TownPortalRouteMarker,
                profileJson.RootElement.GetProperty("townPortalRoute").GetString());
            Assert.Equal(inputManifest.Sha256,
                profileJson.RootElement.GetProperty("effectiveInputManifestHash").GetString());
        }
        finally
        {
            DeleteOutput(output);
        }
    }

    [Fact]
    public void TownPortalRouteRequiresItsMarkerAndBothExplicitPreviewPacks()
    {
        var basePack = LoadBasePack();
        var (townLoopPreview, townPortalPreview) = LoadTownPreviewPacks();
        var output = NewOutput();
        try
        {
            Assert.Throws<ArgumentException>(() => CathedralDepthPlaytestCompiler.Compile(
                Options(basePack, output) with {
                    TownPortalRoute = CathedralDepthPlaytestCompiler.TownPortalRouteMarker,
                    TownLoopPreviewPack = townLoopPreview,
                    TownPortalPreviewPack = townPortalPreview,
                }));
            Assert.False(Directory.Exists(output));

            Assert.Throws<ArgumentException>(() => CathedralDepthPlaytestCompiler.Compile(
                Options(basePack, output) with {
                    PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic,
                    DiagnosticDropItemSymbol = "short_sword",
                    TownPortalRoute = CathedralDepthPlaytestCompiler.TownPortalRouteMarker,
                    TownLoopPreviewPack = townLoopPreview,
                    TownPortalPreviewPack = townPortalPreview,
                }));
            Assert.False(Directory.Exists(output));

            Assert.Throws<ArgumentException>(() => CathedralDepthPlaytestCompiler.Compile(
                Options(basePack, output) with {
                    PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic,
                    DiagnosticDropItemSymbol = "short_sword_standard",
                    TownPortalRoute = CathedralDepthPlaytestCompiler.TownPortalRouteMarker,
                }));
            Assert.False(Directory.Exists(output));

            Assert.Throws<ArgumentException>(() => CathedralDepthPlaytestCompiler.Compile(
                Options(basePack, output) with {
                    PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic,
                    DiagnosticDropItemSymbol = "short_sword_standard",
                    TownPortalRoute = CathedralDepthPlaytestCompiler.TownPortalRouteMarker,
                    TownLoopPreviewPack = townLoopPreview,
                }));
            Assert.False(Directory.Exists(output));

            Assert.Throws<ArgumentException>(() => CathedralDepthPlaytestCompiler.Compile(
                Options(basePack, output) with {
                    PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic,
                    DiagnosticDropItemSymbol = "short_sword_standard",
                    TownLoopPreviewPack = townLoopPreview,
                    TownPortalPreviewPack = townPortalPreview,
                }));
            Assert.False(Directory.Exists(output));
        }
        finally
        {
            DeleteOutput(output);
        }
    }

    [Fact]
    public void TownPortalRouteRejectsWrongOverlayOrderAndMissingAuthoredStockBeforePublishing()
    {
        var basePack = LoadBasePack();
        var (townLoopPreview, townPortalPreview) = LoadTownPreviewPacks();
        var output = NewOutput();
        try
        {
            var wrongOrder = Options(basePack, output) with {
                PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic,
                DiagnosticDropItemSymbol = "short_sword_standard",
                TownPortalRoute = CathedralDepthPlaytestCompiler.TownPortalRouteMarker,
                TownLoopPreviewPack = townPortalPreview,
                TownPortalPreviewPack = townLoopPreview,
            };
            Assert.Throws<InvalidDataException>(() => CathedralDepthPlaytestCompiler.Compile(wrongOrder));
            Assert.False(Directory.Exists(output));

            var portalStores = townPortalPreview.Tables.Single(table => table.SourcePath == "stores.tsv");
            var emptyStores = TsvTable.Parse(portalStores.SourcePath,
                string.Join('\t', portalStores.Columns) + "\n");
            var missingStockPack = new ContentPack(
                townPortalPreview.Id,
                townPortalPreview.Version,
                townPortalPreview.Tables.Select(table => table.SourcePath == "stores.tsv" ? emptyStores : table).ToArray(),
                townPortalPreview.Dependencies,
                townPortalPreview.Overlays,
                townPortalPreview.Schema,
                townPortalPreview.DependencyConstraints,
                townPortalPreview.Assets);
            var missingStock = Options(basePack, output) with {
                PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic,
                DiagnosticDropItemSymbol = "short_sword_standard",
                TownPortalRoute = CathedralDepthPlaytestCompiler.TownPortalRouteMarker,
                TownLoopPreviewPack = townLoopPreview,
                TownPortalPreviewPack = missingStockPack,
            };
            Assert.Throws<InvalidDataException>(() => CathedralDepthPlaytestCompiler.Compile(missingStock));
            Assert.False(Directory.Exists(output));

            var baseItems = basePack.Tables.Single(table => table.SourcePath == "items.tsv");
            var wrongPortalSpell = TsvTable.Parse(baseItems.SourcePath,
                string.Join('\t', baseItems.Columns) + "\n"
                + string.Join('\n', baseItems.Rows.Select(row => string.Join('\t', baseItems.Columns.Select(column =>
                    row.RequiredUInt32("item_id") == 2119 && column == "spell_id"
                        ? "38"
                        : row.Values[column])))) + "\n");
            var wrongSpellBase = new ContentPack(basePack.Id, basePack.Version,
                basePack.Tables.Select(table => table.SourcePath == "items.tsv" ? wrongPortalSpell : table).ToArray(),
                schema: basePack.Schema,
                assets: basePack.Assets);
            var wrongSpell = Options(wrongSpellBase, output) with {
                PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic,
                DiagnosticDropItemSymbol = "short_sword_standard",
                TownPortalRoute = CathedralDepthPlaytestCompiler.TownPortalRouteMarker,
                TownLoopPreviewPack = townLoopPreview,
                TownPortalPreviewPack = townPortalPreview,
            };
            Assert.Throws<InvalidDataException>(() => CathedralDepthPlaytestCompiler.Compile(wrongSpell));
            Assert.False(Directory.Exists(output));
        }
        finally
        {
            DeleteOutput(output);
        }
    }

    [Fact]
    public void RejectsDiagnosticDropItemForNonePolicyBeforePublishing()
    {
        var output = NewOutput();
        try
        {
            Assert.Throws<ArgumentException>(() => CathedralDepthPlaytestCompiler.Compile(
                new CathedralDepthPlaytestCompilerOptions(
                    LoadBasePack(), output, CreatePieces([1, 2, 3, 4]), CreateProperties(),
                    [1U, 2U, 3U, 4U]) {
                    PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.None,
                    DiagnosticDropItemSymbol = "short_sword_standard",
                }));
            Assert.False(Directory.Exists(output));
        }
        finally
        {
            DeleteOutput(output);
        }
    }

    [Fact]
    public void RejectsUnknownDiagnosticDropItemBeforePublishing()
    {
        var output = NewOutput();
        try
        {
            Assert.Throws<InvalidDataException>(() => CathedralDepthPlaytestCompiler.Compile(
                new CathedralDepthPlaytestCompilerOptions(
                    LoadBasePack(), output, CreatePieces([1, 2, 3, 4]), CreateProperties(),
                    [1U, 2U, 3U, 4U]) {
                    PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic,
                    DiagnosticDropItemSymbol = "not_a_base_item",
                }));
            Assert.False(Directory.Exists(output));
        }
        finally
        {
            DeleteOutput(output);
        }
    }

    [Fact]
    public void NonePlacementPolicyPublishesGeometryWithoutAuthoredPopulation()
    {
        var output = NewOutput();
        try
        {
            var profile = CathedralDepthPlaytestCompiler.Compile(
                new CathedralDepthPlaytestCompilerOptions(
                    LoadBasePack(),
                    output,
                    CreatePieces([1, 2, 3, 4]),
                    CreateProperties(),
                    [1U, 2U, 3U, 4U]) {
                    PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.None,
                });

            Assert.Equal("none", profile.PlacementPolicy);
            Assert.All(profile.Levels, level => {
                Assert.Null(level.EncounterPosition);
                Assert.Null(level.ObjectPosition);
            });
            var pack = ContentPackLoader.LoadManifestDirectory(
                Path.Combine(output, CathedralDepthPlaytestCompiler.ContentDirectoryName));
            Assert.Empty(pack.Tables.Single(table => table.SourcePath == "monsters.tsv").Rows);
            Assert.Single(pack.Tables.Single(table => table.SourcePath == "objects.tsv").Rows);
        }
        finally
        {
            DeleteOutput(output);
        }
    }

    [Fact]
    public void RejectsMalformedSeedCountBeforeCreatingOutput()
    {
        var output = NewOutput();
        try
        {
            Assert.Throws<ArgumentException>(() => CathedralDepthPlaytestCompiler.Compile(
                new CathedralDepthPlaytestCompilerOptions(
                    LoadBasePack(),
                    output,
                    "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east\n",
                    CreateProperties(),
                    [1U, 2U, 3U])));
            Assert.False(Directory.Exists(output));
        }
        finally
        {
            DeleteOutput(output);
        }
    }

    [Fact]
    public void RejectsExistingOutputWithoutWriting()
    {
        var basePack = LoadBasePack();
        var existing = NewOutput();
        Directory.CreateDirectory(existing);
        try
        {
            Assert.Throws<IOException>(() => CathedralDepthPlaytestCompiler.Compile(
                new CathedralDepthPlaytestCompilerOptions(
                    basePack,
                    existing,
                    CreatePieces([1, 2, 3, 4]),
                    CreateProperties(),
                    [1U, 2U, 3U, 4U])));
            Assert.Empty(Directory.EnumerateFileSystemEntries(existing));
        }
        finally
        {
            DeleteOutput(existing);
        }
    }

    [Fact]
    public void DoesNotMutateBasePackOrLegacyProfileIdentity()
    {
        var basePack = LoadBasePack();
        var originalLevels = basePack.Tables.Single(table => table.SourcePath == "levels.tsv").ToCanonicalTsv();
        var originalIds = basePack.Tables.Single(table => table.SourcePath == "content_ids.tsv").ToCanonicalTsv();
        var output = NewOutput();
        try
        {
            _ = CathedralDepthPlaytestCompiler.Compile(
                new CathedralDepthPlaytestCompilerOptions(
                    basePack,
                    output,
                    CreatePieces([2588, 2588, 2588, 2588]),
                    CreateProperties(),
                    [2588U, 2588U, 2588U, 2588U]));
            Assert.Equal(originalLevels, basePack.Tables.Single(table => table.SourcePath == "levels.tsv").ToCanonicalTsv());
            Assert.Equal(originalIds, basePack.Tables.Single(table => table.SourcePath == "content_ids.tsv").ToCanonicalTsv());
            Assert.Equal("cathedral-playtest.json", CathedralL1PlaytestCompiler.ProfileFileName);
            Assert.Equal("devilution.diablo.cathedral-l1.v1", CathedralL1PlaytestCompiler.AdapterId);
        }
        finally
        {
            DeleteOutput(output);
        }
    }

    [Theory]
    [InlineData(2U, "catacombs")]
    [InlineData(3U, "caves")]
    [InlineData(4U, "hell")]
    public void RejectsRetainedOldFamilyDependencyWithoutPublishing(uint levelId, string oldSymbol)
    {
        var original = LoadBasePack();
        var greetings = original.Tables.Single(table => table.SourcePath == "towner_greetings.tsv");
        var columns = greetings.Columns;
        var rows = greetings.Rows.Select((row, index) => columns.Select(column =>
            index == 0 && column == "level_id" ? levelId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : index == 0 && column == "level_symbol" ? oldSymbol
            : row.Required(column)));
        var changed = TsvTable.Parse(greetings.SourcePath,
            string.Join('\t', columns) + "\n" + string.Join('\n', rows.Select(row => string.Join('\t', row))) + "\n");
        var pack = new ContentPack(original.Id, original.Version,
            original.Tables.Select(table => table.SourcePath == greetings.SourcePath ? changed : table).ToArray(),
            schema: original.Schema, assets: original.Assets);
        var output = NewOutput();
        try
        {
            Assert.Throws<InvalidDataException>(() => CathedralDepthPlaytestCompiler.Compile(
                Options(pack, output)));
            Assert.False(Directory.Exists(output));
            Assert.Empty(Directory.EnumerateDirectories(Path.GetDirectoryName(output)!,
                Path.GetFileName(output) + ".staging-*"));
        }
        finally
        {
            DeleteOutput(output);
        }
    }

    [Fact]
    public void PublishesIdenticalContentAndTreeHashesForIdenticalExplicitInputs()
    {
        var pack = LoadBasePack();
        var first = NewOutput();
        var second = NewOutput();
        try
        {
            var left = CathedralDepthPlaytestCompiler.Compile(Options(pack, first));
            var right = CathedralDepthPlaytestCompiler.Compile(Options(pack, second));
            Assert.Equal(left.ContentManifestHash, right.ContentManifestHash);
            Assert.Equal(left.PackFileHash, right.PackFileHash);
            var files = Directory.EnumerateFiles(first, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(first, path)).Order().ToArray();
            Assert.Equal(files, Directory.EnumerateFiles(second, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(second, path)).Order());
            foreach (var file in files)
                Assert.Equal(File.ReadAllBytes(Path.Combine(first, file)), File.ReadAllBytes(Path.Combine(second, file)));
        }
        finally
        {
            DeleteOutput(first);
            DeleteOutput(second);
        }
    }

    private static CathedralDepthPlaytestCompilerOptions Options(ContentPack pack, string output)
        => new(pack, output, CreatePieces([1, 2, 3, 4]), CreateProperties(), [1U, 2U, 3U, 4U]) {
            PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.None,
        };

    [Fact]
    public void RejectsDisconnectedWalkableStairWithoutPublishing()
    {
        var output = NewOutput();
        var pieces = CreatePieces([1, 2, 3, 4])
            .Replace("\n63\t128\t2\t2\t2\n", "\n63\t128\t1\t1\t1\n", StringComparison.Ordinal)
            .Replace("\n62\t2\t2\t2\t2\n", "\n62\t2\t1\t2\t1\n", StringComparison.Ordinal)
            .Replace("\n65\t2\t2\t2\t2\n", "\n65\t2\t2\t1\t1\n", StringComparison.Ordinal);
        try
        {
            // Keep MAIN, PREV, Up and Down walkable, but isolate Up from all
            // eight neighbors. A placement corridor must not hide a broken route.
            var error = Assert.Throws<InvalidDataException>(() => CathedralDepthPlaytestCompiler.Compile(
                Options(LoadBasePack(), output) with { PieceDefinitionsTsv = pieces }));
            Assert.Contains("MAIN to Up stair path", error.Message, StringComparison.Ordinal);
            Assert.False(Directory.Exists(output));
            Assert.Empty(Directory.EnumerateDirectories(Path.GetDirectoryName(output)!,
                Path.GetFileName(output) + ".staging-*"));
        }
        finally
        {
            DeleteOutput(output);
        }
    }

    private static string CreatePieces(IReadOnlyList<uint> seeds)
    {
        var lines = new List<string> {
            "mega_tile_id\tpiece_north_west\tpiece_north_east\tpiece_south_west\tpiece_south_east",
        };
        for (var id = 0; id < AuthoritativeDunTilePropertyCatalog.NativeTileCount; id++)
        {
            if (id == 21)
            {
                // Native mega tile 22 is a wall; preserve a blocked diagnostic cell.
                lines.Add($"{id}\t1\t1\t1\t1");
                continue;
            }
            var nw = id == 63 ? 128 : id == 58 ? 114 : 2;
            lines.Add($"{id}\t{nw}\t2\t2\t2");
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

    private static (ContentPack TownLoopPreview, ContentPack TownPortalPreview) LoadTownPreviewPacks()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) {
                var contentRoot = Path.Combine(directory.FullName, "server", "content");
                return (
                    ContentPackLoader.LoadManifestDirectory(Path.Combine(contentRoot, "town-loop-preview")),
                    ContentPackLoader.LoadManifestDirectory(Path.Combine(contentRoot, "town-portal-preview")));
            }
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static string NewOutput()
        => Path.Combine(Path.GetTempPath(), "godevilution-cathedral-depths-" + Guid.NewGuid().ToString("N"));

    private static void DeleteOutput(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}

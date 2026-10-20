using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Devilution.Data;
using Devilution.Diablo;
using Godevilution.Tools;
using Devilution.Tools.NativePlayerArt;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class CathedralNativeTownPlaytestCompilerTests
{
    [Fact]
    public void TownPackPreservesNativeMapWordsTileFlagsRosterAndBoundSettings()
    {
        using var fixture = new TownSourceFixture();
        var pack = CathedralNativeTownPlaytestCompiler.CompileTownPack(
            fixture.SourceRoot, fixture.RosterPath, uint.MaxValue);

        Assert.Equal(CathedralNativeTownPlaytestCompiler.TownPackId, pack.Id);
        Assert.Equal(CathedralNativeTownPlaytestCompiler.TownPackVersion, pack.Version);
        var maps = GetTable(pack, "native_town_geometry/maps.tsv");
        Assert.Collection(maps.Rows,
            row => Assert.Equal(["sector1_south", "25", "25"], [row["map_id"], row["mega_width"], row["mega_height"]]),
            row => Assert.Equal(["sector2_south", "25", "23"], [row["map_id"], row["mega_width"], row["mega_height"]]),
            row => Assert.Equal(["sector3_south", "23", "25"], [row["map_id"], row["mega_width"], row["mega_height"]]),
            row => Assert.Equal(["sector4_south", "23", "23"], [row["map_id"], row["mega_width"], row["mega_height"]]),
            row => Assert.Equal(["automap", "40", "40"], [row["map_id"], row["mega_width"], row["mega_height"]]));

        var tiles = GetTable(pack, "native_town_geometry/map_tiles.tsv");
        Assert.Equal(25 * 25 + 25 * 23 + 23 * 25 + 23 * 23 + 40 * 40, tiles.Rows.Count);
        Assert.Equal("0", tiles.Rows[0]["mega_tile_id"]);
        Assert.Equal("1", tiles.Rows[1]["mega_tile_id"]);
        var automap = tiles.Rows[^1];
        Assert.Equal("automap", automap["map_id"]);
        Assert.Equal("39", automap["x"]);
        Assert.Equal("39", automap["y"]);
        Assert.Equal("1", automap["mega_tile_id"]);

        var pieceRows = GetTable(pack, "native_town_geometry/piece_definitions.tsv").Rows;
        Assert.Equal(2, pieceRows.Count);
        Assert.Equal("6", pieceRows[1]["piece_south_west"]);

        var properties = GetTable(pack, "native_town_geometry/tile_properties.tsv");
        Assert.Equal(AuthoritativeDunTilePropertyCatalog.NativeTileCount, properties.Rows.Count);
        Assert.Equal("64", properties.Rows[0]["source_flags"]);
        Assert.Equal("false", properties.Rows[0]["solid"]);
        Assert.Equal("191", properties.Rows[1]["source_flags"]);
        Assert.Equal("true", properties.Rows[1]["solid"]);
        Assert.Equal("true", properties.Rows[1]["trap"]);
        Assert.Equal("0", properties.Rows[^1]["source_flags"]);

        var roster = GetTable(pack, "native_town_geometry/roster.tsv");
        Assert.Equal(11, roster.Rows.Count);
        Assert.Equal("0", roster.Rows[0]["towner_id"]);
        Assert.Equal("smith", roster.Rows[0]["towner_symbol"]);
        Assert.Equal("Griswold the Blacksmith", roster.Rows[0]["name"]);
        Assert.Equal("griswold", roster.Rows[0]["short_name"]);
        Assert.Equal("62", roster.Rows[0]["position_x"]);
        Assert.Equal("63", roster.Rows[0]["position_y"]);
        Assert.Equal("south_west", roster.Rows[0]["direction"]);
        Assert.Equal(3, roster.Rows.Count(row => row["towner_id"] == "9"));
        Assert.DoesNotContain(roster.Rows, row => row["towner_id"] is "2" or "10" or "11" or "12");
        Assert.Equal("pegboy", roster.Rows.Single(row => row["towner_id"] == "8")["towner_symbol"]);

        var settings = GetTable(pack, "native_town_geometry/settings.tsv");
        var setting = settings.Rows.Single();
        Assert.Equal("false", setting["is_spawn"]);
        Assert.Equal("false", setting["is_multiplayer"]);
        Assert.Equal("false", setting["is_hellfire"]);
        Assert.Equal("1", setting["character_level"]);
        Assert.Equal("0", setting["warp_unlocks"]);
        Assert.Equal("other", setting["farmer_quest_state"]);
        Assert.Equal("other", setting["grave_quest_state"]);
        Assert.Equal("main", setting["entry"]);
        Assert.Equal("0", setting["town_warp_from"]);
        Assert.Equal("not_available", setting["water_quest_state"]);
        Assert.Equal("4294967295", setting["initial_random_state"]);

        var catalog = AuthoritativeNativeTownCatalog.Load(ComposedContent.Compose(
            new ContentManifest(pack.Id, pack.Version, [pack])));
        Assert.Equal(uint.MaxValue, catalog.Settings.InitialRandomState);
        Assert.Equal(11, catalog.Roster.Residents.Count);
    }

    [Fact]
    public void TownPackRejectsMalformedMapsAndIncompleteNativeRoster()
    {
        using var fixture = new TownSourceFixture();
        File.WriteAllBytes(Path.Combine(fixture.SourceRoot, "sector2s.dun"), [1, 0, 1, 0]);
        Assert.Throws<InvalidDataException>(() => CathedralNativeTownPlaytestCompiler.CompileTownPack(
            fixture.SourceRoot, fixture.RosterPath, 1));

        fixture.WriteCanonicalMaps();
        var truncatedPrefix = new byte[sizeof(ushort) * 2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(truncatedPrefix, 25);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(truncatedPrefix.AsSpan(sizeof(ushort)), 25);
        File.WriteAllBytes(Path.Combine(fixture.SourceRoot, "sector1s.dun"), truncatedPrefix);
        Assert.Throws<InvalidDataException>(() => CathedralNativeTownPlaytestCompiler.CompileTownPack(
            fixture.SourceRoot, fixture.RosterPath, 1));

        fixture.WriteCanonicalMaps();
        File.WriteAllText(fixture.RosterPath,
            fixture.RosterText.Replace("TOWN_PEGBOY\tWirt the Peg-legged boy\twirt\t11\t53\tSouth", "", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => CathedralNativeTownPlaytestCompiler.CompileTownPack(
            fixture.SourceRoot, fixture.RosterPath, 1));
    }

    [Fact]
    public void TownProfileRetainsFormatFourIdentityAndAddsAValidatedTownBinding()
    {
        using var fixture = new TownProfileFixture();
        var profiles = NativePlayerArtProfiles.LoadDirectories([fixture.Root]);

        Assert.Equal([fixture.FinalManifestHash], profiles.ContentManifestHashes);
        Assert.Equal(5, profiles.LevelBindings.Count);
        Assert.Equal("town", profiles.LevelBindings.Single(binding => binding.LevelId == 0).Location);
        Assert.All(profiles.LevelBindings.Where(binding => binding.LevelId > 0),
            binding => Assert.Equal("cathedral", binding.Location));
    }

    [Fact]
    public void TownProfileRejectsEditedNestedClassIdentityAndTownSeedMismatch()
    {
        using var fixture = new TownProfileFixture();
        var original = File.ReadAllText(fixture.TownProfilePath);
        File.WriteAllText(fixture.TownProfilePath,
            original.Replace("\"townInitialSeed\":17", "\"townInitialSeed\":18", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => NativePlayerArtProfiles.LoadDirectories([fixture.Root]));

        File.WriteAllText(fixture.TownProfilePath, original.Replace("\"heroClassSymbol\":\"warrior\"",
            "\"heroClassSymbol\":\"sorcerer\"", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => NativePlayerArtProfiles.LoadDirectories([fixture.Root]));

        File.WriteAllText(fixture.TownProfilePath,
            original.Replace(fixture.CatalogFingerprint, new string('0', 64), StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => NativePlayerArtProfiles.LoadDirectories([fixture.Root]));
    }

    [Fact]
    public void TownCompilerCliRequiresAnExplicitSeedAndDoesNotPublishOnInvalidInput()
    {
        using var temporary = new TemporaryDirectory();
        var output = Path.Combine(temporary.Path, "town-profile");
        var program = typeof(CathedralNativeTownPlaytestCompiler).Assembly
            .GetType("Godevilution.Tools.Program", throwOnError: true)!;
        var main = program.GetMethod("Main", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Cathedral.Playtest has no public Main entry point.");
        var originalError = Console.Error;
        using var error = new StringWriter();
        try {
            Console.SetError(error);
            var args = new[] {
                "--native-cathedral-town-progression",
                "--class-profile-directory", Path.Combine(temporary.Path, "class"),
                "--town-source-directory", Path.Combine(temporary.Path, "source"),
                "--roster-table", Path.Combine(temporary.Path, "towners.tsv"),
                "--output-directory", output,
                "--town-seed", "4294967296",
            };
            var exitCode = (int)(main.Invoke(null, [args])
                ?? throw new InvalidOperationException("Cathedral.Playtest Main returned no status."));
            Assert.Equal(1, exitCode);
            Assert.False(Directory.Exists(output));
            Assert.Contains("--town-seed", error.ToString());
        } finally {
            Console.SetError(originalError);
        }
    }

    private static TsvTable GetTable(ContentPack pack, string path)
        => pack.Tables.Single(table => table.SourcePath == path);

    private sealed class TownSourceFixture : IDisposable
    {
        private static readonly (string Type, string Name, string ShortName, int X, int Y, string Direction)[] Residents = [
            ("TOWN_SMITH", "Griswold the Blacksmith", "griswold", 62, 63, "SouthWest"),
            ("TOWN_HEALER", "Pepin the Healer", "pepin", 55, 79, "SouthEast"),
            ("TOWN_DEADGUY", "Wounded Townsman", "deadguy", 24, 32, "North"),
            ("TOWN_TAVERN", "Ogden the Tavern owner", "ogden", 55, 62, "SouthWest"),
            ("TOWN_STORY", "Cain the Elder", "cain", 62, 71, "South"),
            ("TOWN_DRUNK", "Farnham the Drunk", "farnham", 71, 84, "South"),
            ("TOWN_WITCH", "Adria the Witch", "adria", 80, 20, "South"),
            ("TOWN_BMAID", "Gillian the Barmaid", "gillian", 43, 66, "South"),
            ("TOWN_PEGBOY", "Wirt the Peg-legged boy", "wirt", 11, 53, "South"),
            ("TOWN_COW", "Cow", "cow", 58, 16, "SouthWest"),
            ("TOWN_COW", "Cow", "cow", 56, 14, "NorthWest"),
            ("TOWN_COW", "Cow", "cow", 59, 20, "North"),
            ("TOWN_FARMER", "Lester the Farmer", "lester", 30, 60, "South"),
            ("TOWN_GIRL", "Celia the Little Girl", "celia", 35, 55, "South"),
            ("TOWN_COWFARM", "Complete Nut the Farmer", "nut", 25, 65, "South"),
        ];

        public string SourceRoot { get; } = Path.Combine(Path.GetTempPath(), "Godevilution-TownSource-" + Guid.NewGuid().ToString("N"));
        public string RosterPath => Path.Combine(SourceRoot, "towners.tsv");
        public string RosterText { get; }

        public TownSourceFixture()
        {
            Directory.CreateDirectory(SourceRoot);
            WriteCanonicalMaps();
            var til = new byte[2 * 8];
            for (var index = 0; index < 8; index++)
                System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(til.AsSpan(index * 2), (ushort)index);
            File.WriteAllBytes(Path.Combine(SourceRoot, "town.til"), til);
            File.WriteAllBytes(Path.Combine(SourceRoot, "town.sol"), [0x40, 0xBF]);
            var text = new StringBuilder("type\tname\tshortName\tposition_x\tposition_y\tdirection\n");
            foreach (var resident in Residents)
                text.Append(resident.Type).Append('\t').Append(resident.Name).Append('\t').Append(resident.ShortName)
                    .Append('\t').Append(resident.X).Append('\t').Append(resident.Y).Append('\t').Append(resident.Direction).Append('\n');
            RosterText = text.ToString();
            File.WriteAllText(RosterPath, RosterText);
        }

        public void WriteCanonicalMaps()
        {
            foreach (var (fileName, width, height) in new[] {
                         ("sector1s.dun", 25, 25), ("sector2s.dun", 25, 23),
                         ("sector3s.dun", 23, 25), ("sector4s.dun", 23, 23),
                     }) {
                var map = new ushort[width * height];
                map[1] = 1;
                WriteDun(Path.Combine(SourceRoot, fileName), width, height, map, appendNativeTail: true);
            }
            var automap = new ushort[40 * 40];
            automap[^1] = 1;
            WriteDun(Path.Combine(SourceRoot, "automap.dun"), 40, 40, automap, appendNativeTail: true);
        }

        private static void WriteDun(string path, int width, int height, ushort[] tileLayer, bool appendNativeTail = false)
        {
            var bytes = new byte[4 + tileLayer.Length * 2 + (appendNativeTail ? 23 : 0)];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)width);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), (ushort)height);
            for (var index = 0; index < tileLayer.Length; index++)
                System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4 + index * 2), tileLayer[index]);
            if (appendNativeTail)
                Array.Fill(bytes, (byte)0xA5, 4 + tileLayer.Length * 2, 23);
            File.WriteAllBytes(path, bytes);
        }

        public void Dispose()
        {
            if (Directory.Exists(SourceRoot))
                Directory.Delete(SourceRoot, recursive: true);
        }
    }

    private sealed class TownProfileFixture : IDisposable
    {
        private const uint Seed = 17;
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "Godevilution-TownProfile-" + Guid.NewGuid().ToString("N"));
        public string TownProfilePath => Path.Combine(Root, "cathedral-native-town-progression-playtest.json");
        public string FinalManifestHash { get; }
        public string CatalogFingerprint { get; }

        public TownProfileFixture()
        {
            Directory.CreateDirectory(Root);
            var contentRoot = Path.Combine(Root, "content");
            var populationRoot = Path.Combine(Root, "native_cathedral_population");
            var runtimeRoot = Path.Combine(Root, "native_cathedral_runtime");
            var townRoot = Path.Combine(Root, "native_town_geometry");
            var levels = new StringBuilder("level_id\tlevel_symbol\twidth\theight\tblocked_cells\tmissile_blocked_cells\tdefault_start\tsuppress_multiplayer_death_drops\n");
            for (var levelId = 0; levelId <= 4; levelId++) {
                var symbol = levelId switch {
                    0 => "town",
                    1 => "cathedral",
                    _ => $"cathedral_depth_{levelId}",
                };
                levels.Append(levelId).Append('\t').Append(symbol).Append("\t112\t112\tnone\tnone\t0\t0\n");
            }
            WritePack(contentRoot, "godevilution.cathedral-doors-playtest", "0.1.0", [
                ("levels.tsv", levels.ToString()),
            ]);
            WritePack(populationRoot, "godevilution.cathedral-native-population-metadata", "1.0.0", [
                ("population.tsv", "id\n0\n"),
            ]);
            WritePack(runtimeRoot, "godevilution.cathedral-native-runtime", "1.0.0", [
                ("native_cathedral_runtime/native-character-start.tsv",
                    "class_id\tclass_symbol\thero_base_life_maximum\thero_base_mana_maximum\thero_light_radius\thero_idle_frames\n0\twarrior\t4480\t640\t10\t10\n"),
            ]);
            using var sourceFixture = new TownSourceFixture();
            var compiledTownPack = CathedralNativeTownPlaytestCompiler.CompileTownPack(
                sourceFixture.SourceRoot, sourceFixture.RosterPath, Seed);
            WritePack(townRoot, compiledTownPack.Id, compiledTownPack.Version,
                compiledTownPack.Tables.Select(table => (table.SourcePath, table.ToCanonicalTsv())).ToArray());

            var contentPack = ContentPackLoader.LoadManifestDirectory(contentRoot);
            var populationPack = ContentPackLoader.LoadManifestDirectory(populationRoot);
            var runtimePack = ContentPackLoader.LoadManifestDirectory(runtimeRoot);
            var townPack = ContentPackLoader.LoadManifestDirectory(townRoot);
            var classManifest = new ContentManifest(contentPack.Id, contentPack.Version,
                [contentPack, populationPack, runtimePack]);
            using var classDoc = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(new {
                formatVersion = 4,
                adapterId = NativePlayerArtProfiles.ClassProgressionAdapterId,
                contentDirectory = "content",
                contentId = contentPack.Id,
                contentVersion = contentPack.Version,
                contentManifestHash = classManifest.Sha256,
                heroClassSymbol = "warrior",
                nativeDepth = 4,
                populationDirectory = "native_cathedral_population",
                populationPackId = populationPack.Id,
                populationPackVersion = populationPack.Version,
                runtimeDirectory = "native_cathedral_runtime",
                runtimePackId = runtimePack.Id,
                runtimePackVersion = runtimePack.Version,
            }));
            var classProfilePath = Path.Combine(Root, "cathedral-native-class-progression-playtest.json");
            File.WriteAllText(classProfilePath, JsonSerializer.Serialize(classDoc.RootElement));

            var manifest = new ContentManifest(contentPack.Id, contentPack.Version,
                [contentPack, populationPack, runtimePack, townPack]);
            FinalManifestHash = manifest.Sha256;
            CatalogFingerprint = AuthoritativeNativeTownCatalog.Load(ComposedContent.Compose(manifest)).Fingerprint;
            var outer = new {
                formatVersion = 5,
                adapterId = NativePlayerArtProfiles.TownProgressionAdapterId,
                classProfile = classDoc.RootElement,
                townDirectory = "native_town_geometry",
                townPackId = townPack.Id,
                townPackVersion = townPack.Version,
                townPackTreeHash = ComputeTreeHash(townRoot),
                townCatalogFingerprint = CatalogFingerprint,
                townInitialSeed = Seed,
                contentManifestHash = FinalManifestHash,
            };
            File.WriteAllText(TownProfilePath, JsonSerializer.Serialize(outer));
        }

        private static void WritePack(string root, string id, string version, IReadOnlyList<(string Path, string Text)> tables)
        {
            Directory.CreateDirectory(root);
            foreach (var (relativePath, text) in tables) {
                var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text);
            }
            File.WriteAllText(Path.Combine(root, ContentPackLoader.ManifestFileName),
                JsonSerializer.Serialize(new { formatVersion = 1, id, version }));
        }

        private static string ComputeTreeHash(string root)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                         .Select(fullPath => new { FullPath = fullPath, Relative = Path.GetRelativePath(root, fullPath).Replace('\\', '/') })
                         .OrderBy(file => file.Relative, StringComparer.Ordinal)) {
                var fileHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path.FullPath)));
                hash.AppendData(Encoding.UTF8.GetBytes($"{path.Relative.Length}:{path.Relative}:{fileHash}\n"));
            }
            return Convert.ToHexString(hash.GetHashAndReset());
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "Godevilution-Native-Town-CLI-" + Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}

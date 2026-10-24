using System.Globalization;
using System.Text;
using System.Text.Json;
using Devilution.Data;
using Devilution.Diablo;
using Devilution.Tools.NativeObjectArt;
using Devilution.Tools.NativePlayerArt;
using Devilution.Tools.NativeSceneArt;
using Devilution.Tools.NativeTownerArt;
using Godevilution.Tools.Archive;

namespace Godevilution.Tools.Setup;

public sealed record NativeSetupProfile(string ClassSymbol, string Directory, string ContentManifestHash);

/** The published build the launcher reads from current.json. Pack paths are Godot res:// catalog paths. */
public sealed record NativeSetupState(
    int SchemaVersion,
    string Fingerprint,
    string Archive,
    IReadOnlyList<NativeSetupProfile> Profiles,
    string PlayerPack,
    string ScenePack,
    string TownerPack,
    string ObjectPack);

/**
 * Builds the native Town profiles and the client art packs from the player's
 * archive once per fingerprint and publishes them by writing current.json last.
 * Profiles live under the setup root; packs live in the ignored client asset
 * folders the launcher already stages. Older builds are removed after publish.
 */
public static class NativeSetupBuilder
{
    public const string StateFileName = "current.json";
    public const string BuildPrefix = "build-";
    public const string PackPrefix = "setup-";
    public static readonly IReadOnlyList<string> ClassSymbols = ["warrior", "sorcerer"];

    // The seeds the qualified native class and Town profiles were proven with.
    private const string DepthSeeds = "2588,2588,1,4294967295";
    private const uint TownSeed = 2588;

    private static readonly (string Folder, string Catalog)[] PackFolders = [
        ("native-players", "native-player-catalog.json"),
        ("native-scenes", "native-scene-catalog.json"),
        ("native-towners", NativeTownerArtCompiler.CatalogFileName),
        ("native-objects", NativeObjectArtCompiler.CatalogFileName),
    ];

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string ClientAssetsDirectory(string repositoryRoot)
        => Path.Combine(repositoryRoot, "godot-client", "Devilution.Client", "assets");

    /** The published state when it matches the fingerprint and every output still exists. */
    public static NativeSetupState? TryReuse(string setupRoot, string repositoryRoot, string fingerprint)
    {
        var path = Path.Combine(setupRoot, StateFileName);
        if (!File.Exists(path))
            return null;
        NativeSetupState? state;
        try {
            state = JsonSerializer.Deserialize<NativeSetupState>(File.ReadAllText(path), Json);
        } catch (JsonException) {
            return null;
        }
        if (state is null || state.SchemaVersion != NativeSetupFingerprint.SchemaVersion || state.Fingerprint != fingerprint
            || state.Profiles is not { Count: > 0 }
            || state.Profiles.Any(profile => !File.Exists(Path.Combine(profile.Directory, TownProfileFileName)))
            || new[] { state.PlayerPack, state.ScenePack, state.TownerPack, state.ObjectPack }
                .Any(pack => !File.Exists(ResourceToFile(repositoryRoot, pack))))
            return null;
        return state;
    }

    public const string TownProfileFileName = "cathedral-native-town-progression-playtest.json";

    public static NativeSetupState Build(string setupRoot, string repositoryRoot, string archivePath, string fingerprint, TextWriter log)
    {
        var key = fingerprint[..16];
        var assets = Path.Combine(repositoryRoot, "assets");
        var buildDirectory = Path.Combine(setupRoot, BuildPrefix + key);
        var staging = Path.Combine(setupRoot, $".{BuildPrefix}{key}.staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(setupRoot);
        try {
            using var archive = MpqArchive.Open(archivePath);
            log.WriteLine("Exporting native profile sources from the archive...");
            var sources = NativeSourceExport.Write(archive, assets, Path.Combine(staging, "sources"));
            var metadataDirectory = Path.Combine(staging, "metadata");
            CathedralPopulationMetadataCompiler.CompileDirectory(sources.MetadataDirectory, metadataDirectory);
            var townProfiles = new List<string>();
            foreach (var classSymbol in ClassSymbols) {
                log.WriteLine($"Compiling the native {classSymbol} profile...");
                var classDirectory = Path.Combine(staging, classSymbol + "-class");
                CompileClassProfile(repositoryRoot, sources, metadataDirectory, classDirectory, classSymbol);
                var townDirectory = Path.Combine(staging, classSymbol);
                CathedralNativeTownPlaytestCompiler.Compile(new(classDirectory, sources.TownSourceDirectory,
                    Path.Combine(assets, "txtdata", "towners", "towners.tsv"), townDirectory, TownSeed));
                townProfiles.Add(townDirectory);
            }

            var packs = PackFolders.Select(pack => Path.Combine(ClientAssetsDirectory(repositoryRoot), pack.Folder, PackPrefix + key)).ToArray();
            foreach (var pack in packs.Where(Directory.Exists))
                Directory.Delete(pack, recursive: true);
            var source = new NativeArchiveSource(archive, assets);
            log.WriteLine("Importing player, scene, resident and object art...");
            var profileSet = NativePlayerArtProfiles.LoadDirectories(townProfiles);
            NativePlayerArtCompiler.Import(new(string.Empty, Path.Combine(assets, "txtdata"), Path.Combine(repositoryRoot, "server", "content", "base"),
                packs[0], "levels/towndata/town.pal", "levels/l1data/l1_1.pal", profileSet.ContentManifestHashes, profileSet.LevelBindings, source));
            NativeSceneArtCompiler.Import(new(string.Empty, packs[1], "levels/towndata/town.pal", "levels/l1data/l1_1.pal", townProfiles, source));
            NativeTownerArtCompiler.Import(new(string.Empty, Path.Combine(assets, "txtdata"), townProfiles, packs[2], source));
            NativeObjectArtCompiler.Import(new(string.Empty, Path.Combine(assets, "txtdata"), townProfiles, packs[3], source));

            // Sources and intermediate class profiles are retail-derived build inputs; keep only the Town profiles.
            Directory.Delete(sources.GeometryDirectory, recursive: true);
            Directory.Delete(Path.Combine(staging, "sources"), recursive: true);
            if (Directory.Exists(buildDirectory))
                Directory.Delete(buildDirectory, recursive: true);
            Directory.Move(staging, buildDirectory);

            var state = new NativeSetupState(NativeSetupFingerprint.SchemaVersion, fingerprint, archivePath,
                ClassSymbols.Select(classSymbol => {
                    var directory = Path.Combine(buildDirectory, classSymbol);
                    using var profile = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, TownProfileFileName)));
                    return new NativeSetupProfile(classSymbol, directory, profile.RootElement.GetProperty("contentManifestHash").GetString()!);
                }).ToArray(),
                FileToResource(repositoryRoot, packs[0], PackFolders[0].Catalog),
                FileToResource(repositoryRoot, packs[1], PackFolders[1].Catalog),
                FileToResource(repositoryRoot, packs[2], PackFolders[2].Catalog),
                FileToResource(repositoryRoot, packs[3], PackFolders[3].Catalog));
            var statePath = Path.Combine(setupRoot, StateFileName);
            File.WriteAllText(statePath + ".tmp", JsonSerializer.Serialize(state, Json).Replace("\n", "\r\n", StringComparison.Ordinal),
                new UTF8Encoding(false));
            File.Move(statePath + ".tmp", statePath, overwrite: true);
            RemoveOtherBuilds(setupRoot, repositoryRoot, key);
            return state;
        } finally {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    /** The --native-cathedral-depth-progression --native-class path of Cathedral.Playtest. */
    private static void CompileClassProfile(string repositoryRoot, NativeSourceExportLayout sources, string metadataDirectory,
        string outputDirectory, string classSymbol)
    {
        var basePack = ContentPackLoader.LoadManifestDirectory(Path.Combine(repositoryRoot, "server", "content", "base"));
        var metadata = AuthoritativeCathedralPopulationMetadata.Load(ContentPackLoader.LoadManifestDirectory(metadataDirectory));
        var runtime = AuthoritativeCathedralMonsterRuntimeCatalog.LoadTable(TsvTable.Parse(
            AuthoritativeCathedralMonsterRuntimeCatalog.TableName, File.ReadAllText(sources.MonsterRuntimeTable)));
        var footprints = TsvTable.Parse(NativeSourceFootprintExport.TableName, File.ReadAllText(sources.InventoryFootprintTable));
        var geometry = ContentPackLoader.LoadDirectory("explicit-cathedral-geometry", "1", sources.GeometryDirectory);
        string Table(string name) => geometry.Tables.Single(table => table.SourcePath == name).ToCanonicalTsv();
        var seeds = DepthSeeds.Split(',').Select(seed => uint.Parse(seed, CultureInfo.InvariantCulture)).ToArray();
        CathedralNativePopulationPlaytestCompiler.CompileClassProgression(new(basePack, metadata, runtime, footprints, outputDirectory,
            Table(AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath),
            Table(AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath), seeds) { NativeClassSymbol = classSymbol });
    }

    private static void RemoveOtherBuilds(string setupRoot, string repositoryRoot, string key)
    {
        foreach (var directory in Directory.EnumerateDirectories(setupRoot, BuildPrefix + "*"))
            if (Path.GetFileName(directory) != BuildPrefix + key)
                Directory.Delete(directory, recursive: true);
        foreach (var (folder, _) in PackFolders) {
            var parent = Path.Combine(ClientAssetsDirectory(repositoryRoot), folder);
            if (!Directory.Exists(parent))
                continue;
            foreach (var directory in Directory.EnumerateDirectories(parent, PackPrefix + "*"))
                if (Path.GetFileName(directory) != PackPrefix + key)
                    Directory.Delete(directory, recursive: true);
        }
    }

    public static string FileToResource(string repositoryRoot, string packDirectory, string catalog)
        => "res://" + Path.GetRelativePath(Path.Combine(repositoryRoot, "godot-client", "Devilution.Client"),
            Path.Combine(packDirectory, catalog)).Replace('\\', '/');

    public static string ResourceToFile(string repositoryRoot, string resource)
        => Path.Combine(repositoryRoot, "godot-client", "Devilution.Client",
            resource.StartsWith("res://", StringComparison.Ordinal) ? resource["res://".Length..] : resource);
}

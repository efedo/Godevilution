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

/** The published build the launcher reads from current.json. Pack paths are Godot res:// catalog paths. */
public sealed record NativeSetupState(
    int SchemaVersion,
    string Fingerprint,
    string Archive,
    string ProfileDirectory,
    string ContentManifestHash,
    string PlayerPack,
    string ScenePack,
    string TownerPack,
    string ObjectPack);

/**
 * Builds the all-class native Town profile and the client art packs from the player's
 * archive once per fingerprint and publishes them by writing current.json last.
 * Profiles live under the setup root; packs live in the ignored client asset
 * folders the launcher already stages. Older builds are removed after publish.
 */
public static class NativeSetupBuilder
{
    public const string StateFileName = "current.json";
    public const string BuildPrefix = "build-";
    public const string PackPrefix = "setup-";
    public const string ProfileDirectoryName = "all-classes";
    public const string ProfilePrefix = "profile-";

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
            || string.IsNullOrEmpty(state.ProfileDirectory)
            || !File.Exists(Path.Combine(state.ProfileDirectory, TownProfileFileName))
            || new[] { state.PlayerPack, state.ScenePack, state.TownerPack, state.ObjectPack }
                .Any(pack => !File.Exists(ResourceToFile(repositoryRoot, pack))))
            return null;
        return state;
    }

    public const string TownProfileFileName = CathedralNativeTownPlaytestCompiler.AllClassProfileFileName;

    public static NativeSetupState Build(string setupRoot, string repositoryRoot, string archivePath, string fingerprint, TextWriter log)
    {
        var key = fingerprint[..16];
        var assets = Path.Combine(repositoryRoot, "assets");
        var staging = Path.Combine(setupRoot, $".{BuildPrefix}{key}.staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(setupRoot);
        try {
            using var archive = MpqArchive.Open(archivePath);
            log.WriteLine("Exporting native profile sources from the archive...");
            var sources = NativeSourceExport.Write(archive, assets, Path.Combine(staging, "sources"));
            var metadataDirectory = Path.Combine(staging, "metadata");
            CathedralPopulationMetadataCompiler.CompileDirectory(sources.MetadataDirectory, metadataDirectory);
            log.WriteLine("Compiling the native Warrior, Rogue and Sorcerer profile...");
            var classDirectory = Path.Combine(staging, "all-classes-progression");
            CompileClassProfile(repositoryRoot, sources, metadataDirectory, classDirectory);
            var townDirectory = Path.Combine(staging, ProfileDirectoryName);
            CathedralNativeTownPlaytestCompiler.Compile(new(classDirectory, sources.TownSourceDirectory,
                Path.Combine(assets, "txtdata", "towners", "towners.tsv"), townDirectory, TownSeed) { AllClasses = true });
            Directory.Delete(classDirectory, recursive: true);
            string[] townProfiles = [townDirectory];

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

            var profileDirectory = PublishProfile(setupRoot, townDirectory);
            using var profile = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(profileDirectory, TownProfileFileName)));
            var state = new NativeSetupState(NativeSetupFingerprint.SchemaVersion, fingerprint, archivePath, profileDirectory,
                profile.RootElement.GetProperty("contentManifestHash").GetString()!,
                FileToResource(repositoryRoot, packs[0], PackFolders[0].Catalog),
                FileToResource(repositoryRoot, packs[1], PackFolders[1].Catalog),
                FileToResource(repositoryRoot, packs[2], PackFolders[2].Catalog),
                FileToResource(repositoryRoot, packs[3], PackFolders[3].Catalog));
            var statePath = Path.Combine(setupRoot, StateFileName);
            File.WriteAllText(statePath + ".tmp", JsonSerializer.Serialize(state, Json).Replace("\n", "\r\n", StringComparison.Ordinal),
                new UTF8Encoding(false));
            File.Move(statePath + ".tmp", statePath, overwrite: true);
            RemoveOtherBuilds(setupRoot, repositoryRoot, key, profileDirectory);
            return state;
        } finally {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    /**
     * Profiles live at profile-<content hash> with their saves in its sessions folder (the
     * launcher requires that). A rebuild with unchanged content keeps the existing profile and
     * its saves; changed metadata for the same content replaces the files but keeps the saves.
     */
    private static string PublishProfile(string setupRoot, string builtProfile)
    {
        var built = File.ReadAllBytes(Path.Combine(builtProfile, TownProfileFileName));
        using var document = JsonDocument.Parse(built);
        var contentHash = document.RootElement.GetProperty("contentManifestHash").GetString()!;
        var target = Path.Combine(setupRoot, ProfilePrefix + contentHash[..16]);
        var existingProfile = Path.Combine(target, TownProfileFileName);
        if (File.Exists(existingProfile) && File.ReadAllBytes(existingProfile).AsSpan().SequenceEqual(built))
            return target;
        if (Directory.Exists(target)) {
            var sessions = Path.Combine(target, "sessions");
            if (Directory.Exists(sessions))
                Directory.Move(sessions, Path.Combine(builtProfile, "sessions"));
            Directory.Delete(target, recursive: true);
        }
        Directory.Move(builtProfile, target);
        return target;
    }

    /** The --native-cathedral-depth-progression path of Cathedral.Playtest, starting every base-game class. */
    private static void CompileClassProfile(string repositoryRoot, NativeSourceExportLayout sources, string metadataDirectory,
        string outputDirectory)
    {
        var basePack = ContentPackLoader.LoadManifestDirectory(Path.Combine(repositoryRoot, "server", "content", "base"));
        var metadata = AuthoritativeCathedralPopulationMetadata.Load(ContentPackLoader.LoadManifestDirectory(metadataDirectory));
        var runtime = AuthoritativeCathedralMonsterRuntimeCatalog.LoadTable(TsvTable.Parse(
            AuthoritativeCathedralMonsterRuntimeCatalog.TableName, File.ReadAllText(sources.MonsterRuntimeTable)));
        var footprints = TsvTable.Parse(NativeSourceFootprintExport.TableName, File.ReadAllText(sources.InventoryFootprintTable));
        var geometry = ContentPackLoader.LoadDirectory("explicit-cathedral-geometry", "1", sources.GeometryDirectory);
        string Table(string name) => geometry.Tables.Single(table => table.SourcePath == name).ToCanonicalTsv();
        var seeds = DepthSeeds.Split(',').Select(seed => uint.Parse(seed, CultureInfo.InvariantCulture)).ToArray();
        CathedralNativePopulationPlaytestCompiler.CompileAllClassProgression(new(basePack, metadata, runtime, footprints, outputDirectory,
            Table(AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath),
            Table(AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath), seeds));
    }

    /** Removes earlier build folders, art packs, and profiles that hold no saves. */
    private static void RemoveOtherBuilds(string setupRoot, string repositoryRoot, string key, string currentProfile)
    {
        foreach (var directory in Directory.EnumerateDirectories(setupRoot, BuildPrefix + "*"))
            Directory.Delete(directory, recursive: true);
        foreach (var directory in Directory.EnumerateDirectories(setupRoot, ProfilePrefix + "*"))
            if (!string.Equals(Path.GetFullPath(directory), Path.GetFullPath(currentProfile), StringComparison.OrdinalIgnoreCase)
                && !(Directory.Exists(Path.Combine(directory, "sessions"))
                    && Directory.EnumerateFileSystemEntries(Path.Combine(directory, "sessions")).Any()))
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

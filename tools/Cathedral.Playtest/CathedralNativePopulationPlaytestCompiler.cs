using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Devilution.Data;
using Devilution.Diablo;

namespace Godevilution.Tools;

/** Explicit input for the opt-in original-population Cathedral route compiler. */
public sealed record CathedralNativePopulationPlaytestCompilerOptions(
    ContentPack BasePack,
    AuthoritativeCathedralPopulationMetadata PopulationMetadata,
    AuthoritativeCathedralMonsterRuntimeCatalog MonsterRuntime,
    TsvTable InventoryFootprints,
    string OutputDirectory,
    string PieceDefinitionsTsv,
    string TilePropertiesTsv,
    IReadOnlyList<uint> DepthSeeds)
{
    /** Optional base-game class for the isolated class-bound native progression profile. */
    public string? NativeClassSymbol { get; init; }
}

/**
 * Builds the opt-in, native-populated Cathedral route. The ordinary depth and
 * door compilers remain the source of generated geometry; this compiler adds
 * only the separately validated native metadata and runtime sidecars.
 */
public static class CathedralNativePopulationPlaytestCompiler
{
    public const string AdapterId = "devilution.diablo.cathedral-native-population.v1";
    public const string ProgressionAdapterId = "devilution.diablo.cathedral-native-progression.v1";
    public const string ProgressionProfileFileName = "cathedral-native-progression-playtest.json";
    public const string ProgressionPolicy = "original-diablo-single-player-normal-rogue-town-l1-l2";
    public const string DepthProgressionAdapterId = "devilution.diablo.cathedral-native-depth-progression.v1";
    public const string DepthProgressionProfileFileName = "cathedral-native-depth-progression-playtest.json";
    public const string DepthProgressionPolicy = "original-diablo-single-player-normal-rogue-town-l1-l4";
    public const string ClassProgressionAdapterId = "devilution.diablo.cathedral-native-class-progression.v1";
    public const string ClassProgressionProfileFileName = "cathedral-native-class-progression-playtest.json";
    public const string ClassProgressionPolicyPrefix = "original-diablo-single-player-normal-";
    public const string NativeCharacterStartTablePath = "native-character-start.tsv";
    public const string ProfileFileName = "cathedral-native-population-playtest.json";
    public const string PopulationDirectoryName = "native_cathedral_population";
    public const string RuntimeDirectoryName = "native_cathedral_runtime";
    public const string PopulationPackId = "godevilution.cathedral-native-population-metadata";
    public const string RuntimePackId = "godevilution.cathedral-native-runtime";
    public const string PackVersion = "1.0.0";
    public const int FormatVersion = 1;
    public const uint StorySeed = 0x13579BDF;
    public const int NativeDepth = 1;
    public const string PopulationPolicy = "original-diablo-single-player-normal-rogue-l1-main";

    private static readonly uint[] RequiredDepthSeeds = [2588, 2588, 1, uint.MaxValue];
    internal const string SupportedDepthSeedErrorMessage =
        "Native population requires the four explicit supported depth seeds [2588,2588,1,4294967295].";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    internal static bool HasSupportedDepthSeeds(IReadOnlyList<uint> depthSeeds)
        => depthSeeds.SequenceEqual(RequiredDepthSeeds);

    /** Requested native population is distinct from an acceptance claim. */
    /** CanonicalSha256 fields hash normalized TSV content, not the CRLF bytes written into sidecar files. */
    public sealed record Profile(
        int FormatVersion,
        string AdapterId,
        string ContentDirectory,
        string ContentId,
        string ContentVersion,
        string ContentManifestHash,
        string PackFileHash,
        IReadOnlyList<uint> DepthSeeds,
        string Entry,
        string PlacementPolicy,
        bool NativePopulation,
        bool NativePopulationClaim,
        string PopulationDirectory,
        string PopulationPackId,
        string PopulationPackVersion,
        string PopulationPackFileHash,
        string RuntimeDirectory,
        string RuntimePackId,
        string RuntimePackVersion,
        string RuntimePackFileHash,
        string PopulationMetadataFingerprint,
        string PopulationMetadataManifestHash,
        string MonsterRuntimeFingerprint,
        string InventoryFootprintCanonicalSha256,
        string ItemSemanticsCanonicalSha256,
        int SemanticItemCount,
        string PopulationPolicy,
        int NativeDepth,
        uint DungeonSeed,
        uint StorySeed,
        string HeroClassSymbol,
        int HeroBaseLifeMaximum,
        int HeroBaseManaMaximum,
        int HeroLightRadius,
        int HeroIdleFrames,
        int PreEntryFutureX,
        int PreEntryFutureY,
        bool PreEntryLevelChanging,
        bool PreEntryLevelLoading,
        IReadOnlyList<CathedralDepthPlaytestLevelProfile> Levels,
        IReadOnlyList<CathedralDepthPlaytestRouteProfile> Routes,
        string DoorProfileId,
        int DoorProfileRevision,
        string DoorCatalogFingerprint,
        IReadOnlyList<CathedralDoorPlaytestDepthMapping> DoorMappings);

    public static Profile Compile(CathedralNativePopulationPlaytestCompilerOptions options)
        => CompileCore(options, progression: false);

    public static Profile CompileProgression(CathedralNativePopulationPlaytestCompilerOptions options)
        => CompileCore(options, progression: true);

    public static Profile CompileDepthProgression(CathedralNativePopulationPlaytestCompilerOptions options)
        => CompileCore(options, progression: true, depthProgression: true);

    /** Builds an isolated Warrior or Sorcerer variant of the four-floor native route. */
    public static Profile CompileClassProgression(CathedralNativePopulationPlaytestCompilerOptions options)
        => CompileCore(options, progression: true, depthProgression: true, classBound: true);

    private static Profile CompileCore(
        CathedralNativePopulationPlaytestCompilerOptions options,
        bool progression,
        bool depthProgression = false,
        bool classBound = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.BasePack);
        ArgumentNullException.ThrowIfNull(options.PopulationMetadata);
        ArgumentNullException.ThrowIfNull(options.MonsterRuntime);
        ArgumentNullException.ThrowIfNull(options.InventoryFootprints);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.PieceDefinitionsTsv);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TilePropertiesTsv);
        if (options.DepthSeeds is null)
            throw new ArgumentException("Native population requires its four explicit Cathedral depth seeds.", nameof(options.DepthSeeds));
        var depthSeeds = options.DepthSeeds.ToArray();
        if (!HasSupportedDepthSeeds(depthSeeds))
            throw new ArgumentException(SupportedDepthSeedErrorMessage, nameof(options.DepthSeeds));
        if (!string.Equals(options.BasePack.Id, "devilution.base", StringComparison.Ordinal)
            || options.BasePack.Dependencies.Count != 0 || options.BasePack.Overlays.Count != 0)
            throw new InvalidDataException("Native population requires the standalone devilution.base input pack.");
        if (classBound && (!progression || !depthProgression))
            throw new ArgumentException("Class-bound native starts require the four-floor depth progression profile.", nameof(options));
        var nativeClassSymbol = ResolveNativeClassSymbol(options.NativeClassSymbol, classBound);

        var module = DiabloModule.Instance;
        var inputManifest = new ContentManifest(options.BasePack.Id, options.BasePack.Version, [options.BasePack]);
        var baseActivation = module.ContentBootstrap.Activate(ComposedContent.Compose(inputManifest));
        var baseItems = baseActivation.Simulation.Items
            ?? throw new InvalidDataException("Base content did not provide its item catalog.");
        var nativeItems = options.PopulationMetadata.Items.Catalog;
        var semanticsTable = AuthoritativeCathedralNativePopulationBuilder.CreateSemanticTable(
            nativeItems, baseItems, baseActivation.Simulation.Spells,
            AuthoritativeCathedralNativeItemSidecars.LoadFootprints(options.InventoryFootprints),
            includeNativeJewelry: progression,
            includeNativeDepthItems: depthProgression);
        var itemSidecars = AuthoritativeCathedralNativeItemSidecars.Load(
            options.InventoryFootprints, semanticsTable);
        if (classBound) {
            var classId = nativeClassSymbol == "warrior" ? 0U : 2U;
            ValidateNativeClassStartingItems(baseItems,
                baseActivation.PlayerLoadouts.GetRequired(classId), itemSidecars);
        }
        var effectiveItems = AuthoritativeCathedralNativePopulationBuilder.ApplyItemSidecars(
            nativeItems, baseItems, baseActivation.Simulation.Spells, itemSidecars);
        var storesTable = options.BasePack.Tables.SingleOrDefault(table =>
            string.Equals(table.SourcePath, "stores.tsv", StringComparison.Ordinal))
            ?? throw new InvalidDataException("Base content is missing stores.tsv.");
        var selectedStock = AuthoritativeCathedralNativePopulationBuilder.PrepareNativeProfileStoreTable(
            storesTable, effectiveItems, itemSidecars, nativeProgression: progression);
        var depthBasePack = ReplaceTable(options.BasePack, selectedStock);
        if (progression)
            depthBasePack = AddTable(
                depthBasePack,
                AuthoritativeCathedralNativePopulationBuilder.CreateNativeProgressionStoreCapabilityTable());

        var populationPack = CreatePopulationSidecar(options.PopulationMetadata);
        var runtimeTable = options.MonsterRuntime.CreateTable();
        var (hero, characterStartTable) = ResolveHero(baseActivation, nativeClassSymbol);
        var runtimePack = CreateRuntimeSidecar(runtimeTable, options.InventoryFootprints, semanticsTable, characterStartTable);

        var outputRoot = ValidateFreshOutputPath(options.OutputDirectory);
        var stagingRoot = outputRoot + ".native-staging-" + Guid.NewGuid().ToString("N");
        RejectReparseAncestors(stagingRoot);
        try {
            Directory.CreateDirectory(stagingRoot);
            var generatedRoot = Path.Combine(stagingRoot, "generated");
            var doorProfile = CathedralDoorPlaytestCompiler.Compile(
                new CathedralDoorPlaytestCompilerOptions(
                    depthBasePack,
                    generatedRoot,
                    options.PieceDefinitionsTsv,
                    options.TilePropertiesTsv,
                    depthSeeds) {
                    Entry = AuthoritativeCathedralEntry.Main,
                    PlacementPolicy = CathedralDepthPlaytestPlacementPolicy.None,
                });

            var generatedContentRoot = Path.Combine(generatedRoot, CathedralDoorPlaytestCompiler.ContentDirectoryName);
            var finalContentRoot = Path.Combine(stagingRoot, CathedralDoorPlaytestCompiler.ContentDirectoryName);
            Directory.Move(generatedContentRoot, finalContentRoot);
            Directory.Delete(generatedRoot, recursive: true);
            WriteSchemaNeutralPack(Path.Combine(stagingRoot, PopulationDirectoryName), populationPack);
            WriteSchemaNeutralPack(Path.Combine(stagingRoot, RuntimeDirectoryName), runtimePack);

            var contentPack = ContentPackLoader.LoadManifestDirectory(finalContentRoot);
            var publishedPopulationPack = ContentPackLoader.LoadManifestDirectory(
                Path.Combine(stagingRoot, PopulationDirectoryName));
            var publishedRuntimePack = ContentPackLoader.LoadManifestDirectory(
                Path.Combine(stagingRoot, RuntimeDirectoryName));
            var effectiveManifest = new ContentManifest(contentPack.Id, contentPack.Version,
                [contentPack, publishedPopulationPack, publishedRuntimePack]);
            var composed = ComposedContent.Compose(effectiveManifest);
            if (depthProgression)
                _ = module.CathedralNativeDepthProgressionContentBootstrap.ActivateNativeDepthProgression(
                    composed, StorySeed);
            else if (progression)
                _ = module.CathedralNativeProgressionContentBootstrap.ActivateNativeProgression(composed, StorySeed);
            else
                _ = module.CathedralNativePopulationContentBootstrap.ActivateNativePopulation(
                    composed, StorySeed, DiabloGameMode.SinglePlayer, difficulty: DiabloDifficulty.Normal);

            var profile = new Profile(
                classBound ? 4 : depthProgression ? 3 : progression ? 2 : FormatVersion,
                classBound ? ClassProgressionAdapterId : depthProgression ? DepthProgressionAdapterId : progression ? ProgressionAdapterId : AdapterId,
                CathedralDoorPlaytestCompiler.ContentDirectoryName,
                contentPack.Id,
                contentPack.Version,
                effectiveManifest.Sha256,
                ComputePackFileHash(finalContentRoot),
                Array.AsReadOnly(depthSeeds),
                "main",
                "none",
                NativePopulation: true,
                NativePopulationClaim: false,
                PopulationDirectoryName,
                publishedPopulationPack.Id,
                publishedPopulationPack.Version,
                ComputePackFileHash(Path.Combine(stagingRoot, PopulationDirectoryName)),
                RuntimeDirectoryName,
                publishedRuntimePack.Id,
                publishedRuntimePack.Version,
                ComputePackFileHash(Path.Combine(stagingRoot, RuntimeDirectoryName)),
                options.PopulationMetadata.Fingerprint,
                options.PopulationMetadata.ManifestHash,
                options.MonsterRuntime.Fingerprint,
                ComputeCanonicalTextHash(options.InventoryFootprints.ToCanonicalTsv()),
                ComputeCanonicalTextHash(semanticsTable.ToCanonicalTsv()),
                semanticsTable.Rows.Count,
                classBound ? ClassProgressionPolicyPrefix + nativeClassSymbol + "-town-l1-l4"
                    : depthProgression ? DepthProgressionPolicy : progression ? ProgressionPolicy : PopulationPolicy,
                depthProgression ? 4 : progression ? 2 : NativeDepth,
                RequiredDepthSeeds[0],
                StorySeed,
                hero.ClassSymbol,
                hero.BaseLifeMaximum,
                hero.BaseManaMaximum,
                hero.LightRadius,
                hero.IdleFrames,
                hero.StartX,
                hero.StartY,
                PreEntryLevelChanging: true,
                PreEntryLevelLoading: true,
                doorProfile.Levels,
                doorProfile.Routes,
                doorProfile.DoorProfileId,
                doorProfile.DoorProfileRevision,
                doorProfile.DoorCatalogFingerprint,
                doorProfile.DoorMappings);
            var profileFileName = classBound ? ClassProgressionProfileFileName
                : depthProgression ? DepthProgressionProfileFileName
                : progression ? ProgressionProfileFileName : ProfileFileName;
            WriteProfile(Path.Combine(stagingRoot, profileFileName), profile);
            Directory.Move(stagingRoot, outputRoot);
            return profile;
        }
        catch {
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, recursive: true);
            throw;
        }
    }

    private sealed record HeroFacts(
        uint ClassId,
        string ClassSymbol,
        int BaseLifeMaximum,
        int BaseManaMaximum,
        int LightRadius,
        int IdleFrames,
        int StartX,
        int StartY);

    private static void ValidateNativeClassStartingItems(
        AuthoritativeItemCatalog items,
        AuthoritativePlayerLoadoutDefinition loadout,
        AuthoritativeCathedralNativeItemSidecars sidecars)
    {
        foreach (var startingItem in loadout.Items) {
            var item = items.GetRequired(startingItem.ItemId);
            if (item.NativeItemIndex < 0
                || !sidecars.Footprints.ContainsKey(item.NativeItemIndex)
                || !sidecars.Semantics.TryGetValue(item.NativeItemIndex, out var semantic)
                || !string.Equals(semantic.StableItemSymbol, item.StableSymbol, StringComparison.Ordinal)) {
                throw new InvalidDataException(
                    $"Class-bound native progression cannot bind starting item '{item.StableSymbol}' to the captured native item sidecars.");
            }
        }
    }

    private static string ResolveNativeClassSymbol(string? requested, bool classBound)
    {
        if (!classBound) {
            if (requested is not null)
                throw new ArgumentException("A selected native class requires CompileClassProgression.", nameof(requested));
            return "rogue";
        }
        if (requested is not ("warrior" or "sorcerer"))
            throw new ArgumentException("Class-bound native progression supports only the base-game Warrior or Sorcerer.", nameof(requested));
        return requested;
    }

    private static (HeroFacts Hero, TsvTable? CharacterStartTable) ResolveHero(
        DiabloContentActivation activation,
        string classSymbol)
    {
        var playerClass = activation.PlayerClasses.Definitions.SingleOrDefault(definition =>
            string.Equals(definition.Symbol, classSymbol, StringComparison.Ordinal))
            ?? throw new InvalidDataException($"Base content does not define the canonical {classSymbol} class.");
        if (playerClass.Availability != DiabloPlayerClassAvailability.BaseGame
            || (classSymbol == "warrior" && playerClass.ClassId != 0)
            || (classSymbol == "rogue" && playerClass.ClassId != 1)
            || (classSymbol == "sorcerer" && playerClass.ClassId != 2))
            throw new InvalidDataException("Native Cathedral class-bound profiles require canonical base-game class identities.");
        var template = activation.PlayerLoadouts.CreateTemplate(
            "native-cathedral-population-profile", playerClass.ClassId, null, null, null, 1,
            startingPositionX: 57, startingPositionY: 40);
        var idleFrames = activation.PlayerAnimations.GetRequired(playerClass.ClassId).IdleFrames;
        if (template.BaseLifeMaximum <= 0 || template.BaseManaMaximum < 0 || idleFrames <= 0)
            throw new InvalidDataException($"Canonical {classSymbol} route inputs are incomplete.");

        if (classSymbol == "rogue") {
            var legacy = AuthoritativeCathedralNativeCharacterStart.Resolve(
                null, activation.PlayerClasses, activation.PlayerAnimations, activation.PlayerLoadouts);
            return (new(legacy.ClassId, legacy.ClassSymbol, legacy.Hero.MaximumLifeBase,
                legacy.Hero.MaximumManaBase, legacy.Hero.LightRadius, legacy.Hero.IdleFrames, 57, 40), null);
        }

        var lightRadius = template.CreatePlayer().LightRadius;
        var startTable = CreateNativeCharacterStartTable(new(
            playerClass.ClassId,
            classSymbol,
            template.BaseLifeMaximum,
            template.BaseManaMaximum,
            lightRadius,
            idleFrames,
            57,
            40));
        var resolved = AuthoritativeCathedralNativeCharacterStart.Resolve(
            startTable, activation.PlayerClasses, activation.PlayerAnimations, activation.PlayerLoadouts);
        return (new(resolved.ClassId, resolved.ClassSymbol, resolved.Hero.MaximumLifeBase,
            resolved.Hero.MaximumManaBase, resolved.Hero.LightRadius, resolved.Hero.IdleFrames, 57, 40), startTable);
    }

    private static TsvTable CreateNativeCharacterStartTable(HeroFacts hero)
    {
        var values = new[] {
            hero.ClassId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            hero.ClassSymbol,
            hero.BaseLifeMaximum.ToString(System.Globalization.CultureInfo.InvariantCulture),
            hero.BaseManaMaximum.ToString(System.Globalization.CultureInfo.InvariantCulture),
            hero.LightRadius.ToString(System.Globalization.CultureInfo.InvariantCulture),
            hero.IdleFrames.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        var contents = string.Join("\r\n", [
            "class_id\tclass_symbol\thero_base_life_maximum\thero_base_mana_maximum\thero_light_radius\thero_idle_frames",
            string.Join('\t', values),
            string.Empty,
        ]);
        return TsvTable.Parse(NativeCharacterStartTablePath, contents);
    }

    private static ContentPack CreatePopulationSidecar(AuthoritativeCathedralPopulationMetadata metadata)
    {
        var tables = metadata.ToContentPack().Tables
            .Select(table => table with { SourcePath = "native_cathedral_population/" + table.SourcePath })
            .ToArray();
        return new ContentPack(PopulationPackId, PackVersion, tables);
    }

    private static ContentPack CreateRuntimeSidecar(
        TsvTable runtimeTable,
        TsvTable footprintsTable,
        TsvTable semanticsTable,
        TsvTable? characterStartTable = null)
    {
        if (!string.Equals(runtimeTable.SourcePath, AuthoritativeCathedralMonsterRuntimeCatalog.TableName, StringComparison.Ordinal))
            throw new InvalidDataException("Native monster runtime table has an unsupported source path.");
        var sourceTables = new List<TsvTable> { runtimeTable, footprintsTable, semanticsTable };
        if (characterStartTable is not null)
            sourceTables.Add(characterStartTable);
        var tables = sourceTables
            .Select(table => table with { SourcePath = "native_cathedral_runtime/" + table.SourcePath })
            .ToArray();
        return new ContentPack(RuntimePackId, PackVersion, tables);
    }

    private static ContentPack ReplaceTable(ContentPack source, TsvTable replacement)
    {
        var replaced = 0;
        var tables = source.Tables.Select(table => {
            if (!string.Equals(table.SourcePath, replacement.SourcePath, StringComparison.Ordinal))
                return table;
            replaced++;
            return replacement;
        }).ToArray();
        if (replaced != 1)
            throw new InvalidDataException("Native profile stock preparation must replace exactly one stores.tsv table.");
        return new ContentPack(source.Id, source.Version, tables, source.Dependencies, source.Overlays,
            source.Schema, source.DependencyConstraints, source.Assets);
    }

    private static ContentPack AddTable(ContentPack source, TsvTable addition)
    {
        if (source.Tables.Any(table => string.Equals(table.SourcePath, addition.SourcePath, StringComparison.Ordinal)))
            throw new InvalidDataException($"Native progression content already contains '{addition.SourcePath}'.");
        return new ContentPack(source.Id, source.Version, source.Tables.Append(addition).ToArray(),
            source.Dependencies, source.Overlays, source.Schema, source.DependencyConstraints, source.Assets);
    }

    private static string ValidateFreshOutputPath(string requested)
    {
        var path = Path.GetFullPath(requested);
        RejectReparseAncestors(path);
        if (Path.Exists(path))
            throw new IOException("Native Cathedral profile output must be a new path.");
        return path;
    }

    private static void RejectReparseAncestors(string path)
    {
        for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
            if (directory.Exists && (File.GetAttributes(directory.FullName) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"Native profile path traverses reparse point '{directory.FullName}'.");
    }

    private static void WriteSchemaNeutralPack(string root, ContentPack pack)
    {
        Directory.CreateDirectory(root);
        foreach (var table in pack.Tables) {
            var path = Path.Combine(root, table.SourcePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, ToCrLf(table.ToCanonicalTsv()), Utf8);
        }
        var manifest = new { formatVersion = 1, id = pack.Id, version = pack.Version };
        File.WriteAllText(Path.Combine(root, ContentPackLoader.ManifestFileName),
            ToCrLf(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true })), Utf8);
        var loaded = ContentPackLoader.LoadManifestDirectory(root);
        if (loaded.Id != pack.Id || loaded.Version != pack.Version
            || !loaded.Tables.Select(table => table.SourcePath).Order(StringComparer.Ordinal)
                .SequenceEqual(pack.Tables.Select(table => table.SourcePath).Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidDataException("Native sidecar pack changed during publication.");
    }

    private static void WriteProfile(string path, Profile profile)
        => File.WriteAllText(path, ToCrLf(JsonSerializer.Serialize(profile, new JsonSerializerOptions {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        })), Utf8);

    private static string ComputePackFileHash(string root)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Select(path => new { FullPath = path, Relative = Path.GetRelativePath(root, path).Replace('\\', '/') })
                     .OrderBy(value => value.Relative, StringComparer.Ordinal)) {
            var fileHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file.FullPath)));
            hash.AppendData(Encoding.UTF8.GetBytes($"{file.Relative.Length}:{file.Relative}:{fileHash}\n"));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string ComputeCanonicalTextHash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string ToCrLf(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", "\r\n", StringComparison.Ordinal);
}

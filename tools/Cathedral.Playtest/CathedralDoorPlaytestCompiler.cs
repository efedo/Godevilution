using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Devilution.Data;
using Devilution.Diablo;
using Devilution.Engine;

namespace Godevilution.Tools;

/** Immutable public mapping for one generated Cathedral depth's native door types. */
public sealed record CathedralDoorPlaytestDepthMapping(
    int Depth,
    uint LevelId,
    string LevelSymbol,
    uint LeftObjectId,
    string LeftObjectSymbol,
    uint LeftNativeObjectId,
    uint RightObjectId,
    string RightObjectSymbol,
    uint RightNativeObjectId);

/** Separate, door-enabled Cathedral package marker; depth/L1 markers remain unchanged. */
public sealed record CathedralDoorPlaytestProfile(
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
    bool NativePopulationClaim,
    [property: JsonPropertyName("levels")]
    IReadOnlyList<CathedralDepthPlaytestLevelProfile> Levels,
    IReadOnlyList<CathedralDepthPlaytestRouteProfile> Routes,
    string DoorProfileId,
    int DoorProfileRevision,
    string DoorCatalogFingerprint,
    IReadOnlyList<CathedralDoorPlaytestDepthMapping> DoorMappings)
{
    [JsonIgnore]
    public IReadOnlyList<CathedralDepthPlaytestLevelProfile> ReadonlyLevels => Levels;
}

/** Inputs for the bounded, explicit-semantic Cathedral door playtest package. */
public sealed record CathedralDoorPlaytestCompilerOptions(
    ContentPack BasePack,
    string OutputDirectory,
    string PieceDefinitionsTsv,
    string TilePropertiesTsv,
    IReadOnlyList<uint> DepthSeeds)
{
    public CathedralDoorPlaytestCompilerOptions(
        ContentPack basePack,
        string outputDirectory,
        string pieceDefinitionsTsv,
        string tilePropertiesTsv,
        uint depth1Seed,
        uint depth2Seed,
        uint depth3Seed,
        uint depth4Seed)
        : this(basePack, outputDirectory, pieceDefinitionsTsv, tilePropertiesTsv,
            [depth1Seed, depth2Seed, depth3Seed, depth4Seed])
    {
    }

    public string PackId { get; init; } = CathedralDoorPlaytestCompiler.PackId;
    public string PackVersion { get; init; } = CathedralDoorPlaytestCompiler.PackVersion;
    public AuthoritativeCathedralEntry Entry { get; init; } = AuthoritativeCathedralEntry.Main;
    public CathedralDepthPlaytestPlacementPolicy PlacementPolicy { get; init; }
        = CathedralDepthPlaytestPlacementPolicy.None;
}

/**
 * Builds a fresh, opt-in Cathedral depth-one-through-four package with the
 * native-scanned mutable door capability. The only geometry inputs are the
 * caller-supplied semantic TIL/SOL TSVs; no MPQ or installation discovery is
 * performed. Native population, campaign mapping, and art parity remain false.
 */
public static class CathedralDoorPlaytestCompiler
{
    public const string AdapterId = "devilution.diablo.cathedral-doors.v1";
    public const string PackId = "godevilution.cathedral-doors-playtest";
    public const string PackVersion = "0.1.0";
    public const string ProfileFileName = "cathedral-doors-playtest.json";
    public const string ContentDirectoryName = "content";

    private const uint LeftObjectId = 1;
    private const uint RightObjectId = 2;
    private const string LeftObjectSymbol = "l1_left_door";
    private const string RightObjectSymbol = "l1_right_door";
    private const uint LeftNativeObjectId = 1;
    private const uint RightNativeObjectId = 2;

    public static CathedralDoorPlaytestProfile Compile(
        CathedralDoorPlaytestCompilerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.BasePack);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.PieceDefinitionsTsv);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TilePropertiesTsv);
        if (options.DepthSeeds is null || options.DepthSeeds.Count != 4)
            throw new ArgumentException("Exactly four explicit Cathedral depth seeds are required.", nameof(options.DepthSeeds));
        var depthSeeds = options.DepthSeeds.ToArray();
        if (!string.Equals(options.PackId, PackId, StringComparison.Ordinal)
            || !string.Equals(options.PackVersion, PackVersion, StringComparison.Ordinal))
            throw new ArgumentException($"Cathedral door packages must use {PackId}@{PackVersion}.");
        if (!Enum.IsDefined(options.Entry))
            throw new ArgumentOutOfRangeException(nameof(options.Entry));
        if (options.Entry != AuthoritativeCathedralEntry.Main)
            throw new ArgumentException("Cathedral door playtests require the launcher-compatible Main entry.", nameof(options.Entry));
        if (!Enum.IsDefined(options.PlacementPolicy))
            throw new ArgumentOutOfRangeException(nameof(options.PlacementPolicy));

        var outputRoot = ValidateFreshOutputPath(options.OutputDirectory);
        var stagingRoot = outputRoot + ".door-staging-" + Guid.NewGuid().ToString("N");
        RejectReparseAncestors(stagingRoot);
        try
        {
            // The depth compiler owns generator/table/path-safety behavior. Door mode
            // only enables its narrow operable-door reachability extension.
            var depthProfile = CathedralDepthPlaytestCompiler.Compile(
                new CathedralDepthPlaytestCompilerOptions(
                    options.BasePack,
                    stagingRoot,
                    options.PieceDefinitionsTsv,
                    options.TilePropertiesTsv,
                    depthSeeds) {
                    PackId = PackId,
                    PackVersion = PackVersion,
                    Entry = options.Entry,
                    PlacementPolicy = options.PlacementPolicy,
                    DoorAwareConnectivity = true,
                });

            var contentRoot = Path.Combine(stagingRoot, ContentDirectoryName);
            var depthPack = ContentPackLoader.LoadManifestDirectory(contentRoot);
            var doorPack = AddDoorContent(depthPack);
            var stableIds = StableContentIdCatalog.LoadTsv(
                "content_ids.tsv",
                doorPack.Tables.Single(table => table.SourcePath == "content_ids.tsv").ToCanonicalTsv());
            var doorCatalog = AuthoritativeCathedralDoorCatalog.Load(
                doorPack.Tables.Single(table => table.SourcePath == AuthoritativeCathedralDoorCatalog.TableName)
                    .ToCanonicalTsv(), stableIds);

            WritePack(contentRoot, doorPack);
            WritePackManifest(contentRoot, doorPack);
            var validatedPack = ContentPackLoader.LoadManifestDirectory(contentRoot);
            var manifest = new ContentManifest(validatedPack.Id, validatedPack.Version, [validatedPack]);
            var composed = ComposedContent.Compose(manifest);
            var activation = DiabloModule.Instance.CathedralDoorContentBootstrap.ActivateDoors(composed);
            ValidateAuthoredPopulationSafety(activation);
            ValidateDoorTransitions(activation);
            // Door close/open transitions intentionally retain native shared-arch
            // history. Prove routes on a fresh activation, never on that mutated copy.
            ValidateActivatedRoutes(composed, depthProfile.Levels);
            var writtenManifest = new ContentManifest(validatedPack.Id, validatedPack.Version, [validatedPack]);

            var oldProfile = Path.Combine(stagingRoot, CathedralDepthPlaytestCompiler.ProfileFileName);
            if (File.Exists(oldProfile))
                File.Delete(oldProfile);
            var profile = new CathedralDoorPlaytestProfile(
                1,
                AdapterId,
                ContentDirectoryName,
                PackId,
                PackVersion,
                writtenManifest.Sha256,
                ComputePackFileHash(contentRoot),
                Array.AsReadOnly(depthSeeds),
                options.Entry.ToString().ToLowerInvariant(),
                options.PlacementPolicy == CathedralDepthPlaytestPlacementPolicy.AuthoredDiagnostic
                    ? "authored-diagnostic" : "none",
                false,
                FreezeLevels(depthProfile.Levels),
                FreezeRoutes(depthProfile.Routes),
                AuthoritativeCathedralDoorCatalog.ProfileId,
                AuthoritativeCathedralDoorCatalog.Revision,
                doorCatalog.Fingerprint,
                Array.AsReadOnly(doorCatalog.Definitions.Select(definition => new CathedralDoorPlaytestDepthMapping(
                    checked((int)definition.LevelId),
                    definition.LevelId,
                    LevelSymbol(definition.LevelId),
                    definition.LeftObjectId,
                    LeftObjectSymbol,
                    LeftNativeObjectId,
                    definition.RightObjectId,
                    RightObjectSymbol,
                    RightNativeObjectId)).ToArray()));
            WriteProfile(Path.Combine(stagingRoot, ProfileFileName), profile);
            Directory.Move(stagingRoot, outputRoot);
            return profile;
        }
        catch
        {
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, recursive: true);
            throw;
        }
    }

    private static ContentPack AddDoorContent(ContentPack source)
    {
        var tables = source.Tables
            .Select(table => table)
            .ToDictionary(table => table.SourcePath, StringComparer.OrdinalIgnoreCase);
        var ids = AddStableRightDoorIdentity(RequireTable(tables, "content_ids.tsv"));
        var objects = AddRightDoorTemplate(RequireTable(tables, "objects.tsv"));
        var doors = BuildDoorCatalogTable();
        tables[ids.SourcePath] = ids;
        tables[objects.SourcePath] = objects;
        tables[doors.SourcePath] = doors;
        return new ContentPack(
            PackId,
            PackVersion,
            tables.Values.OrderBy(table => table.SourcePath, StringComparer.Ordinal).ToArray(),
            source.Dependencies,
            source.Overlays,
            source.Schema,
            source.DependencyConstraints,
            source.Assets);
    }

    private static TsvTable AddStableRightDoorIdentity(TsvTable source)
    {
        if (!source.Columns.SequenceEqual(["kind", "symbol", "numeric_id"], StringComparer.Ordinal))
            throw new InvalidDataException("content_ids.tsv has an unsupported schema.");
        var rows = source.Rows.Select(row => row.Values.ToDictionary(value => value.Key, value => value.Value,
                StringComparer.Ordinal)).ToList();
        var existing = rows.Where(row => row["kind"] == "object" &&
                (row["symbol"] == RightObjectSymbol || row["numeric_id"] == RightObjectId.ToString(CultureInfo.InvariantCulture)))
            .ToArray();
        if (existing.Length != 0)
        {
            if (existing.Length != 1 || existing[0]["symbol"] != RightObjectSymbol
                || existing[0]["numeric_id"] != RightObjectId.ToString(CultureInfo.InvariantCulture))
                throw new InvalidDataException("Right Cathedral door identity conflicts with the base stable-ID catalog.");
            return NormalizeTable(source);
        }
        rows.Add(new Dictionary<string, string>(StringComparer.Ordinal) {
            ["kind"] = "object", ["symbol"] = RightObjectSymbol,
            ["numeric_id"] = RightObjectId.ToString(CultureInfo.InvariantCulture),
        });
        return CreateTable(source, rows);
    }

    private static TsvTable AddRightDoorTemplate(TsvTable source)
    {
        var template = source.Rows.SingleOrDefault(row => row.RequiredUInt32("entity_id") == 2001)
            ?? throw new InvalidDataException("Base content is missing the level-1 left-door template.");
        var rows = source.Rows.Select(row => row.Values.ToDictionary(value => value.Key, value => value.Value,
                StringComparer.Ordinal)).ToList();
        if (rows.Any(row => row["entity_id"] == "2003"))
            throw new InvalidDataException("Reserved right-door template entity 2003 is already in use.");
        var copy = template.Values.ToDictionary(value => value.Key, value => value.Value,
            StringComparer.Ordinal);
        copy["entity_id"] = "2003";
        copy["object_id"] = RightObjectId.ToString(CultureInfo.InvariantCulture);
        copy["object_symbol"] = RightObjectSymbol;
        copy["level_id"] = "0";
        copy["level_symbol"] = "";
        copy["position_x"] = "0";
        copy["position_y"] = "0";
        copy["template_only"] = "1";
        copy["native_object_id"] = "0";
        rows.Add(copy);
        return CreateTable(source, rows);
    }

    private static TsvTable BuildDoorCatalogTable()
    {
        var output = new StringBuilder(
            "level_id\tlevel_symbol\tleft_object_id\tleft_object_symbol\tright_object_id\tright_object_symbol\n");
        for (var level = 1; level <= 4; level++)
            output.Append(level).Append('\t').Append(LevelSymbol((uint)level)).Append('\t')
                .Append(LeftObjectId).Append('\t').Append(LeftObjectSymbol).Append('\t')
                .Append(RightObjectId).Append('\t').Append(RightObjectSymbol).Append('\n');
        return TsvTable.Parse(AuthoritativeCathedralDoorCatalog.TableName, output.ToString());
    }

    private static void ValidateDoorTransitions(DiabloContentActivation activation)
    {
        var state = activation.Simulation.WorldState;
        var doors = state.CaptureCathedralDoorObjects();
        if (doors.Count == 0)
            throw new InvalidDataException("Door-enabled Cathedral activation discovered no native doors.");
        foreach (var door in doors.OrderBy(value => value.LevelId).ThenBy(value => value.PositionY)
                     .ThenBy(value => value.PositionX))
        {
            if (!state.TryOperateCathedralDoor(door.EntityId, []))
                throw new InvalidDataException("Generated Cathedral door failed its open transition.");
            var opened = state.CaptureCathedralDoorObjects().Single(value => value.EntityId == door.EntityId);
            if (opened.DoorState == 0)
                throw new InvalidDataException("Generated Cathedral door remained closed after validation.");
            if (!state.TryOperateCathedralDoor(door.EntityId, []))
                throw new InvalidDataException("Generated Cathedral door failed its close transition.");
        }
    }

    /** Item-footprint safety is stricter than direct scanner overlap. */
    private static void ValidateAuthoredPopulationSafety(DiabloContentActivation activation)
    {
        var state = activation.Simulation.WorldState;
        foreach (var value in state.Objects.Values)
        {
            if (state.IsCathedralDoorObject(value.EntityId, value.LevelId, value.PositionX, value.PositionY))
                continue;
            if (state.IsCathedralDoorItemBlocked(value.LevelId, value.PositionX, value.PositionY))
                throw new InvalidDataException(
                    "Authored Cathedral population occupies a native door/light item footprint.");
        }
        foreach (var value in state.CombatTargets.Values.Where(value => value.LevelId is >= 1 and <= 4))
            if (state.IsCathedralDoorItemBlocked(value.LevelId, value.PositionX, value.PositionY))
                throw new InvalidDataException(
                    "Authored Cathedral encounter occupies a native door/light item footprint.");
    }

    /** Rechecks routes against the activated projection, lights, and authored occupancy. */
    private static void ValidateActivatedRoutes(
        ComposedContent composed,
        IReadOnlyList<CathedralDepthPlaytestLevelProfile> levels)
    {
        foreach (var level in levels)
        {
            var state = ActivateDoorValidationState(composed);
            RequireActivatedPath(state, level.LevelId, level.MainEntry, level.UpTrigger,
                level.Depth < 4 ? level.DownTrigger : null, "MAIN to Up");
            if (level.Depth < 4)
            {
                state = ActivateDoorValidationState(composed);
                RequireActivatedPath(state, level.LevelId, level.MainEntry, level.DownTrigger,
                    level.UpTrigger, "MAIN to Down");
                state = ActivateDoorValidationState(composed);
                RequireActivatedPath(state, level.LevelId, level.PreviousEntry, level.UpTrigger,
                    level.DownTrigger, "PREV to Up");
            }
        }
    }

    private static DiabloWorldState ActivateDoorValidationState(ComposedContent composed)
        => DiabloModule.Instance.CathedralDoorContentBootstrap.ActivateDoors(composed).Simulation.WorldState;

    private static void RequireActivatedPath(
        DiabloWorldState state,
        uint levelId,
        CathedralDepthPlaytestPoint start,
        CathedralDepthPlaytestPoint target,
        CathedralDepthPlaytestPoint? excluded,
        string direction)
    {
        var reachable = OpenReachableActivatedDoors(state, levelId, start, excluded);
        if (!reachable.Contains((target.X, target.Y)))
        {
            CloseAllDoors(state);
            throw new InvalidDataException(
                $"Generated Cathedral depth {levelId} has no activated {direction} stair path.");
        }
        CloseAllDoors(state);
    }

    private static HashSet<(int X, int Y)> OpenReachableActivatedDoors(
        DiabloWorldState state,
        uint levelId,
        CathedralDepthPlaytestPoint start,
        CathedralDepthPlaytestPoint? excluded)
    {
        var opened = new HashSet<uint>();
        while (true)
        {
            var level = state.LevelGeometry.Single(value => value.LevelId == levelId);
            var reachable = FloodActivated(level, state, start, excluded);
            var changed = false;
            foreach (var door in state.CaptureCathedralDoorObjects()
                         .Where(value => value.LevelId == levelId && value.DoorState == 0)
                         .OrderBy(value => value.PositionY).ThenBy(value => value.PositionX))
            {
                if (opened.Contains(door.EntityId)
                    || !IsDoorApproachable(door.PositionX, door.PositionY, reachable))
                    continue;
                if (!state.TryOperateCathedralDoor(door.EntityId, []))
                    continue;
                opened.Add(door.EntityId);
                changed = true;
            }
            if (!changed)
                return reachable;
        }
    }

    private static HashSet<(int X, int Y)> FloodActivated(
        AuthoritativeLevel level,
        DiabloWorldState state,
        CathedralDepthPlaytestPoint start,
        CathedralDepthPlaytestPoint? excluded)
    {
        var occupied = state.Objects.Values
            .Where(value => value.LevelId == level.LevelId
                && !state.IsCathedralDoorObject(value.EntityId, value.LevelId, value.PositionX, value.PositionY))
            .Select(value => (value.PositionX, value.PositionY)).ToHashSet();
        occupied.UnionWith(state.WorldItems.Values.Where(value => value.LevelId == level.LevelId)
            .Select(value => (value.PositionX, value.PositionY)));
        occupied.UnionWith(state.Corpses.Values.Where(value => value.LevelId == level.LevelId)
            .Select(value => (value.PositionX, value.PositionY)));
        occupied.UnionWith(state.CombatTargets.Values.Where(value =>
                (value.HitPoints > 0 || value.DeathTicksRemaining > 0 || value.IsPetrified)
                && value.LevelId == level.LevelId)
            .Select(value => (value.PositionX, value.PositionY)));

        bool Walkable(int x, int y)
            => level.ContainsPosition(x, y)
                && level.IsWalkable(x, y)
                && level.NativePieceLayer[y * level.Width + x] != 269
                && !occupied.Contains((x, y));

        var reachable = new HashSet<(int X, int Y)>();
        if (!Walkable(start.X, start.Y))
            return reachable;
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((start.X, start.Y));
        reachable.Add((start.X, start.Y));
        while (queue.Count != 0)
        {
            var current = queue.Dequeue();
            foreach (var offset in DeterministicGridNavigation.NeighborDisplacements)
            {
                var next = (X: current.X + offset.X, Y: current.Y + offset.Y);
                if (excluded is not null && next == (excluded.Value.X, excluded.Value.Y))
                    continue;
                if (reachable.Contains(next) || !Walkable(next.X, next.Y))
                    continue;
                reachable.Add(next);
                queue.Enqueue(next);
            }
        }
        return reachable;
    }

    private static bool IsDoorApproachable(
        int doorX,
        int doorY,
        IReadOnlySet<(int X, int Y)> reachable)
    {
        for (var y = doorY - 1; y <= doorY + 1; y++)
            for (var x = doorX - 1; x <= doorX + 1; x++)
                if (reachable.Contains((x, y)))
                    return true;
        return false;
    }

    private static void CloseAllDoors(DiabloWorldState state)
    {
        foreach (var door in state.CaptureCathedralDoorObjects().Where(value => value.DoorState != 0))
            _ = state.TryOperateCathedralDoor(door.EntityId, []);
    }

    private static IReadOnlyList<CathedralDepthPlaytestLevelProfile> FreezeLevels(
        IReadOnlyList<CathedralDepthPlaytestLevelProfile> levels)
        => Array.AsReadOnly(levels.Select(level => new CathedralDepthPlaytestLevelProfile(
            level.Depth,
            level.LevelId,
            level.LevelSymbol,
            level.DungeonSeed,
            level.Entry,
            level.MainEntry,
            level.PreviousEntry,
            level.UpTrigger,
            level.DownTrigger,
            level.SafeResumePosition,
            level.BlockedFineCell,
            level.EncounterPosition,
            level.EncounterEntityId,
            level.ObjectPosition,
            level.ObjectEntityId,
            level.TraversalPathDistance,
            Array.AsReadOnly(level.RouteIds.ToArray())) {
            UpWarpId = level.UpWarpId,
            DownWarpId = level.DownWarpId,
        }).ToArray());

    private static IReadOnlyList<CathedralDepthPlaytestRouteProfile> FreezeRoutes(
        IReadOnlyList<CathedralDepthPlaytestRouteProfile> routes)
        => Array.AsReadOnly(routes.ToArray());

    private static TsvTable RequireTable(
        IReadOnlyDictionary<string, TsvTable> tables,
        string sourcePath)
        => tables.TryGetValue(sourcePath, out var table)
            ? table
            : throw new InvalidDataException($"Base content is missing {sourcePath}.");

    private static TsvTable CreateTable(
        TsvTable source,
        IEnumerable<IReadOnlyDictionary<string, string>> rows)
    {
        var builder = new StringBuilder();
        builder.AppendJoin('\t', source.Columns).Append('\n');
        foreach (var row in rows)
            builder.AppendJoin('\t', source.Columns.Select(column => row[column])).Append('\n');
        return NormalizeTable(TsvTable.Parse(source.SourcePath, builder.ToString()));
    }

    private static TsvTable NormalizeTable(TsvTable table)
        => TsvTable.Parse(table.SourcePath, table.ToCanonicalTsv());

    private static string LevelSymbol(uint levelId)
        => levelId switch {
            1 => "cathedral",
            2 => "cathedral_depth_2",
            3 => "cathedral_depth_3",
            4 => "cathedral_depth_4",
            _ => throw new ArgumentOutOfRangeException(nameof(levelId)),
        };

    private static void WritePack(string root, ContentPack pack)
    {
        foreach (var table in pack.Tables)
        {
            var path = Path.Combine(root, table.SourcePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, ToCrLf(table.ToCanonicalTsv()), new UTF8Encoding(false));
        }
        foreach (var asset in pack.Assets)
        {
            var path = Path.Combine(root, asset.SourcePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, asset.Contents.ToArray());
        }
    }

    private static void WritePackManifest(string root, ContentPack pack)
    {
        var manifest = new {
            formatVersion = 1,
            id = pack.Id,
            version = pack.Version,
            schema = pack.Schema is null ? null : new { id = pack.Schema.Id, version = pack.Schema.Version.ToString() },
        };
        File.WriteAllText(
            Path.Combine(root, ContentPackLoader.ManifestFileName),
            ToCrLf(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true })),
            new UTF8Encoding(false));
    }

    private static void WriteProfile(string path, CathedralDoorPlaytestProfile profile)
        => File.WriteAllText(path,
            ToCrLf(JsonSerializer.Serialize(profile, new JsonSerializerOptions {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            })), new UTF8Encoding(false));

    private static string ComputePackFileHash(string root)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Select(path => new { FullPath = path, Relative = Path.GetRelativePath(root, path).Replace('\\', '/') })
                     .OrderBy(file => file.Relative, StringComparer.Ordinal))
        {
            var fileHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path.FullPath)));
            var record = $"{path.Relative.Length}:{path.Relative}:{fileHash}\n";
            hash.AppendData(Encoding.UTF8.GetBytes(record));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string ValidateFreshOutputPath(string requested)
    {
        var path = Path.GetFullPath(requested);
        RejectReparseAncestors(path);
        if (Directory.Exists(path))
            throw new IOException($"Playtest output directory '{requested}' must not already exist.");
        if (File.Exists(path))
            throw new IOException($"Playtest output path '{requested}' is an existing file.");
        return path;
    }

    private static void RejectReparseAncestors(string path)
    {
        for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
            if (directory.Exists && (File.GetAttributes(directory.FullName) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException(
                    $"Playtest output path '{path}' traverses reparse-point directory '{directory.FullName}'.");
    }

    private static string ToCrLf(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
}

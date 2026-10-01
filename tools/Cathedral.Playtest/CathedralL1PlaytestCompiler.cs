using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Devilution.Data;
using Devilution.Diablo;

namespace Godevilution.Tools;

/** Inputs for the opt-in, offline Cathedral level-1 playtest pack compiler. */
public sealed record CathedralL1PlaytestCompilerOptions(
    ContentPack BasePack,
    string OutputDirectory,
    string PieceDefinitionsTsv,
    string TilePropertiesTsv,
    uint DungeonSeed = 2588,
    AuthoritativeCathedralEntry Entry = AuthoritativeCathedralEntry.Main)
{
    public string PackId { get; init; } = "godevilution.cathedral-playtest";

    public string PackVersion { get; init; } = "0.1.0";
}

/** Native coordinates and path metadata exposed to the playtest launcher. */
public readonly record struct CathedralL1PlaytestPoint(int X, int Y);

/** Compact, content-addressed metadata emitted beside a generated playtest pack. */
public sealed record CathedralL1PlaytestProfile(
    int FormatVersion,
    string AdapterId,
    string ContentDirectory,
    string ContentId,
    string ContentVersion,
    string ContentManifestHash,
    string PackFileHash,
    uint DungeonSeed,
    string Entry,
    uint MonsterEntityId,
    uint DropEntityId,
    uint DropItemSeed,
    uint ObjectEntityId,
    CathedralL1PlaytestPoint MainEntry,
    CathedralL1PlaytestPoint PreviousEntry,
    CathedralL1PlaytestPoint UpTrigger,
    CathedralL1PlaytestPoint DownTrigger,
    CathedralL1PlaytestPoint ObjectPosition,
    CathedralL1PlaytestPoint MonsterPosition,
    CathedralL1PlaytestPoint SafeResumePosition,
    CathedralL1PlaytestPoint BlockedFineCell,
    int TraversalPathDistance);

/**
 * Builds an ordinary generated-Cathedral content pack from explicit semantic
 * geometry. The compiler is deliberately file-pack only: it does not inspect
 * MPQs, discover native assets, or access server saves.
 */
public static class CathedralL1PlaytestCompiler
{
    public const string AdapterId = "devilution.diablo.cathedral-l1.v1";
    public const string ProfileFileName = "cathedral-playtest.json";
    public const string ContentDirectoryName = "content";
    public const uint MonsterEntityId = 9;
    public const uint DropEntityId = 1_000_009;
    public const uint DropItemSeed = 6001;
    public const uint ObjectEntityId = 21_000;

    private const uint ReturnWarpId = 1099;
    private const int LevelId = 1;

    public static CathedralL1PlaytestProfile Compile(
        CathedralL1PlaytestCompilerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.BasePack);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.PieceDefinitionsTsv);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TilePropertiesTsv);
        if (!Enum.IsDefined(options.Entry))
            throw new ArgumentOutOfRangeException(nameof(options.Entry));

        var pieceTable = TsvTable.Parse(
            AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath,
            options.PieceDefinitionsTsv);
        var propertyTable = TsvTable.Parse(
            AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath,
            options.TilePropertiesTsv);
        AuthoritativeDunPieceCatalog.ValidateExternalizedProduction(pieceTable);
        AuthoritativeDunTilePropertyCatalog.ValidateExternalizedProduction(propertyTable);
        var geometry = AuthoritativeNativeDunGeometryCatalog.LoadTsv(
            pieceTable.SourcePath,
            pieceTable.ToCanonicalTsv(),
            propertyTable.SourcePath,
            propertyTable.ToCanonicalTsv());

        var tables = options.BasePack.Tables
            .Select(NormalizeTable)
            .ToDictionary(table => table.SourcePath, StringComparer.OrdinalIgnoreCase);
        var stableIds = LoadStableIds(tables);
        var generated = BuildGeneratedCatalog(options.DungeonSeed, options.Entry);
        var world = BuildLevelOneWorld(tables);
        var runtimeCatalog = AuthoritativeCathedralLevel1RuntimeCatalog.Load(
            generated.ToCanonicalTsv(),
            stableIds);
        var plan = runtimeCatalog.Prepare(world, geometry);
        var candidate = plan.Candidates.Single();
        var placements = SelectPlacements(candidate.Level, candidate.Routes);

        tables[AuthoritativeNativeDunGeometryCatalog.PieceDefinitionsTablePath] = NormalizeTable(pieceTable);
        tables[AuthoritativeNativeDunGeometryCatalog.TilePropertiesTablePath] = NormalizeTable(propertyTable);
        tables["native_cathedral_level1.tsv"] = generated;
        tables[AuthoritativeNativeGeneratedRouteBindings.TableName] = BuildRouteBindings();
        tables["content_ids.tsv"] = AddReturnWarpIdentity(tables["content_ids.tsv"]);
        tables["warps.tsv"] = AddReturnWarp(tables["warps.tsv"]);
        if (tables.TryGetValue("native_dun_fragments.tsv", out var fragments))
            tables[fragments.SourcePath] = ClearFragments(fragments);
        if (tables.TryGetValue("native_dun_fragment_objects.tsv", out var fragmentObjects))
            tables[fragmentObjects.SourcePath] = ClearFragments(fragmentObjects);
        tables["level_start_positions.tsv"] = BuildTownStartPositions();
        tables["monsters.tsv"] = PlaceDiagnosticMonster(tables["monsters.tsv"], placements.Monster);
        tables["objects.tsv"] = PlaceDiagnosticObject(
            tables["objects.tsv"],
            placements.Object,
            placements.Monster);

        var outputPack = new ContentPack(
            options.PackId,
            options.PackVersion,
            tables.Values.OrderBy(table => table.SourcePath, StringComparer.Ordinal).ToArray(),
            schema: options.BasePack.Schema,
            assets: options.BasePack.Assets);
        // Validate the exact composed pack before creating the caller-owned
        // output directory. Serialization is checked again below so a profile
        // is never published for a changed or partially written pack.
        var prevalidatedManifest = ValidatePack(outputPack, options.DungeonSeed);
        var outputRoot = PrepareOutputDirectory(options.OutputDirectory);
        var contentRoot = Path.Combine(outputRoot, ContentDirectoryName);
        Directory.CreateDirectory(contentRoot);
        WritePack(contentRoot, outputPack);
        WritePackManifest(contentRoot, outputPack);
        var validatedManifest = ValidateWrittenPack(contentRoot, options.DungeonSeed);
        if (!string.Equals(
                prevalidatedManifest.Sha256,
                validatedManifest.Sha256,
                StringComparison.Ordinal)) {
            throw new InvalidDataException(
                "Serialized Cathedral playtest content changed after in-memory validation.");
        }
        var profile = new CathedralL1PlaytestProfile(
            1,
            AdapterId,
            ContentDirectoryName,
            options.PackId,
            options.PackVersion,
            validatedManifest.Sha256,
            ComputePackFileHash(contentRoot),
            options.DungeonSeed,
            options.Entry.ToString().ToLowerInvariant(),
            MonsterEntityId,
            DropEntityId,
            DropItemSeed,
            ObjectEntityId,
            ToPlaytestPoint(candidate.Routes.MainEntry),
            ToPlaytestPoint(candidate.Routes.PreviousEntry),
            ToPlaytestPoint(candidate.Routes.UpTrigger),
            ToPlaytestPoint(candidate.Routes.DownTrigger),
            placements.Object,
            placements.Monster,
            placements.SafeResume,
            placements.Blocked,
            placements.PathDistance);
        WriteProfile(Path.Combine(outputRoot, ProfileFileName), profile);
        return profile;
    }

    private static CathedralL1PlaytestPoint[] PathFromEntry(
        AuthoritativeLevel level,
        AuthoritativeCathedralLevel1RoutePoints routes)
    {
        var start = (level.StartPositionX, level.StartPositionY);
        var blockedAnchors = new HashSet<(int X, int Y)>
        {
            (routes.UpTrigger.X, routes.UpTrigger.Y),
            (routes.DownTrigger.X, routes.DownTrigger.Y),
            (routes.MainEntry.X, routes.MainEntry.Y),
            (routes.PreviousEntry.X, routes.PreviousEntry.Y),
        };
        var queue = new Queue<(int X, int Y)>();
        var previous = new Dictionary<(int X, int Y), (int X, int Y)> { [start] = start };
        queue.Enqueue(start);
        var directions = new[] { (1, 0), (0, 1), (-1, 0), (0, -1) };
        (int X, int Y)? target = null;
        while (queue.Count != 0)
        {
            var current = queue.Dequeue();
            var distance = Distance(previous, current);
            if (distance >= 8 && !blockedAnchors.Contains(current))
            {
                target = current;
                break;
            }
            foreach (var (dx, dy) in directions)
            {
                var next = (current.X + dx, current.Y + dy);
                if (previous.ContainsKey(next)
                    || blockedAnchors.Contains(next)
                    || !level.IsWalkable(next.Item1, next.Item2))
                    continue;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }
        if (target is null)
            throw new InvalidDataException("Generated Cathedral has no reachable diagnostic placement corridor.");
        var path = new List<CathedralL1PlaytestPoint>();
        var cursor = target.Value;
        while (true)
        {
            path.Add(new CathedralL1PlaytestPoint(cursor.X, cursor.Y));
            if (cursor == start)
                break;
            cursor = previous[cursor];
        }
        path.Reverse();
        return path.ToArray();

        static int Distance(
            IReadOnlyDictionary<(int X, int Y), (int X, int Y)> previous,
            (int X, int Y) point)
        {
            var distance = 0;
            var cursor = point;
            while (previous[cursor] != cursor)
            {
                cursor = previous[cursor];
                distance++;
            }
            return distance;
        }
    }

    private static (CathedralL1PlaytestPoint Object,
        CathedralL1PlaytestPoint Monster,
        CathedralL1PlaytestPoint SafeResume,
        CathedralL1PlaytestPoint Blocked,
        int PathDistance) SelectPlacements(
        AuthoritativeLevel level,
        AuthoritativeCathedralLevel1RoutePoints routes)
    {
        var path = PathFromEntry(level, routes);
        if (path.Length < 8)
            throw new InvalidDataException("Generated Cathedral diagnostic corridor is too short.");
        var objectPoint = path[3];
        var monsterPoint = path[7];
        var safePoint = path[6];
        if (!level.IsWalkable(objectPoint.X, objectPoint.Y)
            || !level.IsWalkable(monsterPoint.X, monsterPoint.Y)
            || objectPoint == monsterPoint)
            throw new InvalidDataException("Generated diagnostic population is not walkable.");

        var blocked = FindNearbyBlockedCell(level, safePoint);
        return (objectPoint, monsterPoint, safePoint, blocked, 7);
    }

    private static CathedralL1PlaytestPoint FindNearbyBlockedCell(
        AuthoritativeLevel level,
        CathedralL1PlaytestPoint origin)
    {
        for (var radius = 1; radius < 16; radius++)
        {
            for (var y = origin.Y - radius; y <= origin.Y + radius; y++)
            {
                for (var x = origin.X - radius; x <= origin.X + radius; x++)
                {
                    if (Math.Abs(x - origin.X) + Math.Abs(y - origin.Y) != radius
                        || !level.ContainsPosition(x, y)
                        || level.IsWalkable(x, y))
                        continue;
                    return new CathedralL1PlaytestPoint(x, y);
                }
            }
        }
        throw new InvalidDataException("Generated Cathedral has no nearby blocked fine cell for the playtest profile.");
    }

    private static TsvTable BuildGeneratedCatalog(uint seed, AuthoritativeCathedralEntry entry)
        => TsvTable.Parse(
            "native_cathedral_level1.tsv",
            "level_id\tlevel_symbol\tgenerator_id\tgenerator_revision\tdungeon_seed\tentry\n"
            + $"1\tcathedral\t{AuthoritativeCathedralLevel1RuntimeCatalog.GeneratorId}\t1\t{seed}\t{(entry == AuthoritativeCathedralEntry.Main ? "main" : "previous")}\n");

    private static AuthoritativeWorld BuildLevelOneWorld(
        IReadOnlyDictionary<string, TsvTable> tables)
    {
        var levelTable = tables["levels.tsv"];
        var row = levelTable.Rows.Single(candidate => candidate.RequiredUInt32("level_id") == LevelId);
        var level = new AuthoritativeLevel(
            LevelId,
            row.RequiredInt32("width"),
            row.RequiredInt32("height"),
            isDefaultStart: row.OptionalBoolean("default_start"),
            suppressMultiplayerDeathDrops: row.OptionalBoolean("suppress_multiplayer_death_drops")) {
            StableSymbol = row.Required("level_symbol"),
        };
        var world = new AuthoritativeWorld();
        world.AddLevel(level);
        return world;
    }

    private static StableContentIdCatalog LoadStableIds(
        IReadOnlyDictionary<string, TsvTable> tables)
    {
        var table = tables["content_ids.tsv"];
        var rows = table.Rows.Select(row => new StableContentId(
                row.Required("kind"),
                row.Required("symbol"),
                row.RequiredUInt32("numeric_id")))
            .ToList();
        var existingSymbol = rows.SingleOrDefault(row => row.Kind == "warp" && row.Symbol == "cathedral_to_town");
        if (existingSymbol is not null && existingSymbol.NumericId != ReturnWarpId)
            throw new InvalidDataException("Base content assigns cathedral_to_town to an unexpected warp ID.");
        if (existingSymbol is null && rows.Any(row => row.Kind == "warp" && row.NumericId == ReturnWarpId))
            throw new InvalidDataException($"Base content already uses warp ID {ReturnWarpId} for another symbol.");
        if (existingSymbol is null)
            rows.Add(new StableContentId("warp", "cathedral_to_town", ReturnWarpId));
        return new StableContentIdCatalog(rows);
    }

    private static TsvTable BuildRouteBindings()
        => TsvTable.Parse(
            AuthoritativeNativeGeneratedRouteBindings.TableName,
            "route_kind\troute_id\troute_symbol\tendpoint\tlevel_id\tlevel_symbol\tanchor\n"
            + "warp\t1001\ttown_to_cathedral\tsource\t0\ttown\ttown_cathedral_trigger\n"
            + "warp\t1001\ttown_to_cathedral\tdestination\t1\tcathedral\tmain_entry\n"
            + "warp\t1002\tcathedral_to_catacombs\tsource\t1\tcathedral\tdown_trigger\n"
            + "warp\t1099\tcathedral_to_town\tsource\t1\tcathedral\tup_trigger\n"
            + "warp\t1099\tcathedral_to_town\tdestination\t0\ttown\ttown_cathedral_return\n"
            + "portal\t1\tcathedral_entrance\tsource\t1\tcathedral\tmain_entry\n");

    private static TsvTable AddReturnWarpIdentity(TsvTable source)
    {
        var existingSymbol = source.Rows.SingleOrDefault(row =>
            row.Required("kind") == "warp"
            && row.Required("symbol") == "cathedral_to_town");
        if (existingSymbol is not null)
        {
            if (existingSymbol.RequiredUInt32("numeric_id") != ReturnWarpId)
                throw new InvalidDataException("Base content assigns cathedral_to_town to an unexpected warp ID.");
            return source;
        }
        if (source.Rows.Any(row => row.Required("kind") == "warp"
                && row.RequiredUInt32("numeric_id") == ReturnWarpId))
            throw new InvalidDataException($"Base content already uses warp ID {ReturnWarpId} for another symbol.");
        return AppendRow(source, new Dictionary<string, string>(StringComparer.Ordinal) {
            ["kind"] = "warp", ["symbol"] = "cathedral_to_town", ["numeric_id"] = ReturnWarpId.ToString(),
        });
    }

    private static TsvTable AddReturnWarp(TsvTable source)
    {
        var existingSymbol = source.Rows.SingleOrDefault(row => row.Required("warp_symbol") == "cathedral_to_town");
        if (existingSymbol is not null)
        {
            if (existingSymbol.RequiredUInt32("warp_id") != ReturnWarpId)
                throw new InvalidDataException("Base content assigns cathedral_to_town to an unexpected warp ID.");
            return source;
        }
        if (source.Rows.Any(row => row.RequiredUInt32("warp_id") == ReturnWarpId))
            throw new InvalidDataException($"Base content already defines return warp {ReturnWarpId}.");
        return AppendRow(source, new Dictionary<string, string>(StringComparer.Ordinal) {
            ["warp_id"] = ReturnWarpId.ToString(), ["warp_symbol"] = "cathedral_to_town",
            ["source_level_id"] = "1", ["source_level_symbol"] = "cathedral",
            ["trigger_x"] = "25", ["trigger_y"] = "29", ["target_level_id"] = "0",
            ["target_level_symbol"] = "town", ["target_x"] = "25", ["target_y"] = "31",
            ["required_quest_id"] = "0", ["required_quest_symbol"] = "",
            ["required_quest_completed"] = "0", ["target_set_level_id"] = "none",
            ["target_set_level_symbol"] = "", ["target_set_level_active_map"] = "false",
        });
    }

    // A semantic Cathedral geometry catalog is not a source for campaign
    // fragments. The offline diagnostic profile is deliberately non-campaign
    // and therefore publishes neither native fragment definitions nor objects.
    private static TsvTable ClearFragments(TsvTable source)
        => FilterRows(source, _ => false);

    private static TsvTable BuildTownStartPositions()
        => TsvTable.Parse(
            "level_start_positions.tsv",
            "level_id\tlevel_symbol\tstart_x\tstart_y\n0\ttown\t75\t68\n");

    private static TsvTable PlaceDiagnosticMonster(
        TsvTable source,
        CathedralL1PlaytestPoint point)
    {
        var baseRow = source.Rows.SingleOrDefault(row => row.RequiredUInt32("entity_id") == MonsterEntityId)
            ?? throw new InvalidDataException($"Base content is missing diagnostic monster entity {MonsterEntityId}.");
        var values = baseRow.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        values["level_id"] = LevelId.ToString();
        values["level_symbol"] = "cathedral";
        values["position_x"] = point.X.ToString();
        values["position_y"] = point.Y.ToString();
        values["drop_item_entity_id"] = DropEntityId.ToString();
        values["drop_item_seed"] = DropItemSeed.ToString();
        var rows = source.Rows
            .Where(row => row.RequiredUInt32("level_id") != LevelId)
            .Select(row => row.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal))
            .Append(values);
        return CreateTable(source, rows);
    }

    private static TsvTable PlaceDiagnosticObject(
        TsvTable source,
        CathedralL1PlaytestPoint point,
        CathedralL1PlaytestPoint monster)
    {
        var template = source.Rows.SingleOrDefault(row => row.RequiredUInt32("entity_id") == 2001)
            ?? throw new InvalidDataException("Base content is missing the level-1 door template entity 2001.");
        var rows = source.Rows
            .Where(row => row.RequiredUInt32("entity_id") == 2001
                || row.RequiredBoolean("template_only")
                || row.RequiredUInt32("level_id") != LevelId)
            .Select(row => {
                var copy = row.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                if (copy["entity_id"] == "2001")
                    copy["template_only"] = "1";
                return copy;
            })
            .ToList();
        var values = template.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        values["entity_id"] = ObjectEntityId.ToString();
        values["object_symbol"] = "";
        values["level_id"] = LevelId.ToString();
        values["level_symbol"] = "cathedral";
        values["position_x"] = point.X.ToString();
        values["position_y"] = point.Y.ToString();
        values["effect_kind"] = "Heal";
        values["effect_amount"] = "0";
        values["missile_passable"] = "1";
        values["template_only"] = "0";
        values["native_object_id"] = "0";
        values["quest_id"] = "0";
        values["quest_symbol"] = "";
        values["reward_kind"] = "None";
        values["reward_id"] = "0";
        values["activate_on_occupancy"] = "false";
        if (point == monster)
            throw new InvalidDataException("Diagnostic object overlaps diagnostic monster.");
        rows.Add(values);
        return CreateTable(source, rows);
    }

    private static TsvTable AppendRow(
        TsvTable source,
        IReadOnlyDictionary<string, string> values)
    {
        var rows = source.Rows
            .Select(row => row.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal))
            .Append(values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
        return CreateTable(source, rows);
    }

    private static TsvTable FilterRows(TsvTable source, Func<TsvRow, bool> predicate)
        => CreateTable(source, source.Rows.Where(predicate)
            .Select(row => row.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)));

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

    private static string PrepareOutputDirectory(string requested)
    {
        var path = Path.GetFullPath(requested);
        RejectReparseAncestors(path);
        if (Directory.Exists(path))
            throw new IOException($"Playtest output directory '{requested}' must not already exist.");
        else if (File.Exists(path))
            throw new IOException($"Playtest output path '{requested}' is an existing file.");
        Directory.CreateDirectory(path);
        RejectReparseAncestors(path);
        return path;
    }

    private static void RejectReparseAncestors(string path)
    {
        for (var directory = new DirectoryInfo(path);
             directory is not null;
             directory = directory.Parent)
        {
            if (directory.Exists
                && (File.GetAttributes(directory.FullName) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException(
                    $"Playtest output path '{path}' traverses reparse-point directory '{directory.FullName}'.");
        }
    }

    private static ContentManifest ValidateWrittenPack(
        string contentRoot,
        uint dungeonSeed)
    {
        var writtenPack = ContentPackLoader.LoadManifestDirectory(contentRoot);
        return ValidatePack(writtenPack, dungeonSeed);
    }

    private static ContentManifest ValidatePack(
        ContentPack pack,
        uint dungeonSeed)
    {
        var manifest = new ContentManifest(
            pack.Id,
            pack.Version,
            [pack]);
        var composed = ComposedContent.Compose(manifest);
        _ = DiabloModule.Instance.NativeGeneratedContentBootstrap
            .ActivateGenerated(composed, dungeonSeed);
        return manifest;
    }

    private static CathedralL1PlaytestPoint ToPlaytestPoint(
        AuthoritativeCathedralPoint point)
        => new(point.X, point.Y);

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

    private static void WriteProfile(string path, CathedralL1PlaytestProfile profile)
    {
        File.WriteAllText(
            path,
            ToCrLf(JsonSerializer.Serialize(profile, new JsonSerializerOptions {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            })),
            new UTF8Encoding(false));
    }

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

    private static string ToCrLf(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
}

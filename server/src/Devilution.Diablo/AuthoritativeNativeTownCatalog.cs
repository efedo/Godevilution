using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Devilution.Data;

namespace Devilution.Diablo;

/** Validated semantic inputs for the opt-in original Diablo Town generator. */
public sealed class AuthoritativeNativeTownCatalog
{
    public const string IdentityTableName = "native_town_geometry/identity.tsv";
    public const string SettingsTableName = "native_town_geometry/settings.tsv";
    public const string MapsTableName = "native_town_geometry/maps.tsv";
    public const string MapTilesTableName = "native_town_geometry/map_tiles.tsv";
    public const string PieceDefinitionsTableName = "native_town_geometry/piece_definitions.tsv";
    public const string TilePropertiesTableName = "native_town_geometry/tile_properties.tsv";
    public const string RosterTableName = AuthoritativeNativeTownRoster.TableName;
    public const uint TownLevelId = 0;
    public const string TownSymbol = "town";
    public const string GeneratorId = "original-diablo.town";
    public const int GeneratorRevision = 1;
    public const string CatalogId = "native-diablo-town";
    public const int MaximumMutationCount = 256;
    public const int MaximumMapTileRows = (25 * 25) + (25 * 23) + (23 * 25) + (23 * 23) + (40 * 40);
    public const int MaximumPieceDefinitionRows = ushort.MaxValue + 1;

    private static readonly string[] IdentityColumns = [
        "level_id",
        "level_symbol",
        "generator_id",
        "generator_revision",
    ];
    private static readonly string[] SettingsColumns = [
        "is_spawn",
        "is_multiplayer",
        "is_hellfire",
        "character_level",
        "warp_unlocks",
        "farmer_quest_state",
        "grave_quest_state",
        "entry",
        "town_warp_from",
        "water_quest_state",
        "initial_random_state",
    ];
    private static readonly string[] MapsColumns = ["map_id", "mega_width", "mega_height"];
    private static readonly string[] MapTilesColumns = ["map_id", "x", "y", "mega_tile_id"];
    private static readonly string[] PieceDefinitionColumns = [
        "mega_tile_id",
        "piece_north_west",
        "piece_north_east",
        "piece_south_west",
        "piece_south_east",
    ];
    private static readonly string[] TilePropertyColumns = [
        "piece_id",
        "source_flags",
        "solid",
        "block_light",
        "block_missile",
        "transparent",
        "transparent_left",
        "transparent_right",
        "trap",
    ];
    private static readonly string[] MapIds = [
        "sector1_south",
        "sector2_south",
        "sector3_south",
        "sector4_south",
        "automap",
    ];
    private const byte KnownSolFlagMask = (byte)(
        AuthoritativeDunTileFlags.Solid
        | AuthoritativeDunTileFlags.BlockLight
        | AuthoritativeDunTileFlags.BlockMissile
        | AuthoritativeDunTileFlags.Transparent
        | AuthoritativeDunTileFlags.TransparentLeft
        | AuthoritativeDunTileFlags.TransparentRight
        | AuthoritativeDunTileFlags.Trap);
    private const byte NativeReservedSolFlagMask = 0x40;

    private readonly AuthoritativeTownTileMap sector1South;
    private readonly AuthoritativeTownTileMap sector2South;
    private readonly AuthoritativeTownTileMap sector3South;
    private readonly AuthoritativeTownTileMap sector4South;
    private readonly AuthoritativeTownTileMap automap;
    private readonly IReadOnlyList<byte> nativeSourceFlags;

    private AuthoritativeNativeTownCatalog(
        AuthoritativeNativeTownSettings settings,
        AuthoritativeDunPieceCatalog pieceCatalog,
        AuthoritativeDunTilePropertyCatalog tileProperties,
        IReadOnlyList<byte> nativeSourceFlags,
        AuthoritativeTownTileMap sector1South,
        AuthoritativeTownTileMap sector2South,
        AuthoritativeTownTileMap sector3South,
        AuthoritativeTownTileMap sector4South,
        AuthoritativeTownTileMap automap,
        AuthoritativeNativeTownRoster roster,
        string fingerprint)
    {
        Settings = settings;
        PieceCatalog = pieceCatalog;
        TileProperties = tileProperties;
        this.nativeSourceFlags = Array.AsReadOnly(nativeSourceFlags.ToArray());
        this.sector1South = sector1South;
        this.sector2South = sector2South;
        this.sector3South = sector3South;
        this.sector4South = sector4South;
        this.automap = automap;
        Roster = roster;
        Fingerprint = fingerprint;
    }

    public string Id => CatalogId;
    public string Fingerprint { get; }
    public uint LevelId => TownLevelId;
    public string LevelSymbol => TownSymbol;
    public string BoundGeneratorId => GeneratorId;
    public int BoundGeneratorRevision => GeneratorRevision;
    public AuthoritativeNativeTownSettings Settings { get; }
    public AuthoritativeNativeTownRoster Roster { get; }
    internal AuthoritativeDunPieceCatalog PieceCatalog { get; }
    internal AuthoritativeDunTilePropertyCatalog TileProperties { get; }
    /** Full raw source .sol byte values, including the native reserved 0x40 bit. */
    public IReadOnlyList<byte> NativeSourceFlags => nativeSourceFlags;

    public static AuthoritativeNativeTownCatalog Load(ComposedContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var tables = new Dictionary<string, TsvTable>(StringComparer.Ordinal) {
            [IdentityTableName] = content.GetRequiredTable(IdentityTableName),
            [SettingsTableName] = content.GetRequiredTable(SettingsTableName),
            [MapsTableName] = content.GetRequiredTable(MapsTableName),
            [MapTilesTableName] = content.GetRequiredTable(MapTilesTableName),
            [PieceDefinitionsTableName] = content.GetRequiredTable(PieceDefinitionsTableName),
            [TilePropertiesTableName] = content.GetRequiredTable(TilePropertiesTableName),
            [RosterTableName] = content.GetRequiredTable(RosterTableName),
        };

        var identity = tables[IdentityTableName];
        ValidateColumns(identity, IdentityColumns);
        RequireExactRows(identity, 1);
        var identityRow = identity.Rows[0];
        if (ParseUInt32(identityRow, "level_id") != TownLevelId
            || !string.Equals(identityRow.Required("level_symbol"), TownSymbol, StringComparison.Ordinal)
            || !string.Equals(identityRow.Required("generator_id"), GeneratorId, StringComparison.Ordinal)
            || ParseInt32(identityRow, "generator_revision") != GeneratorRevision) {
            throw new InvalidDataException(
                $"{IdentityTableName} must bind Town level 0 to {GeneratorId} revision {GeneratorRevision}.");
        }

        var settingsTable = tables[SettingsTableName];
        ValidateColumns(settingsTable, SettingsColumns);
        RequireExactRows(settingsTable, 1);
        var settings = ParseSettings(settingsTable.Rows[0]);

        var piecesTable = tables[PieceDefinitionsTableName];
        ValidateColumns(piecesTable, PieceDefinitionColumns);
        if (piecesTable.Rows.Count is < 1 or > MaximumPieceDefinitionRows)
            throw new InvalidDataException($"{PieceDefinitionsTableName} has an invalid row count.");
        var pieceCatalog = new AuthoritativeDunPieceCatalog(piecesTable.Rows.Select(row =>
            new AuthoritativeDunPieceDefinition(
                ParseUInt32(row, "mega_tile_id"),
                ParseUInt32(row, "piece_north_west"),
                ParseUInt32(row, "piece_north_east"),
                ParseUInt32(row, "piece_south_west"),
                ParseUInt32(row, "piece_south_east"))));

        var propertiesTable = tables[TilePropertiesTableName];
        ValidateColumns(propertiesTable, TilePropertyColumns);
        var (tileProperties, rawSourceFlags) = ParseTileProperties(propertiesTable);

        var maps = LoadMaps(tables[MapsTableName], tables[MapTilesTableName]);
        ValidateMapReferences(maps, pieceCatalog);
        var roster = AuthoritativeNativeTownRoster.Load(tables[RosterTableName]);
        var fingerprint = ComputeFingerprint(content.Manifest.Sha256, tables);

        return new AuthoritativeNativeTownCatalog(
            settings,
            pieceCatalog,
            tileProperties,
            rawSourceFlags,
            maps["sector1_south"],
            maps["sector2_south"],
            maps["sector3_south"],
            maps["sector4_south"],
            maps["automap"],
            roster,
            fingerprint);
    }

    /** Builds fresh original-SP Town geometry solely from this catalog's bound settings and maps. */
    public AuthoritativeTownGeometry CreateInitialGeometry()
        => AuthoritativeTownGeometryBuilder.Build(new AuthoritativeTownGeometryRequest(
            sector1South,
            sector2South,
            sector3South,
            sector4South,
            automap,
            PieceCatalog,
            TileProperties,
            Settings.WarpPolicy,
            Settings.Entry,
            Settings.TownWarpFrom,
            Settings.WaterQuestState,
            Settings.InitialRandomState));

    /** Captures state only when typed history exactly replays from the catalog-bound initial state. */
    public DiabloNativeTownCheckpoint Capture(
        AuthoritativeTownGeometry currentGeometry,
        IReadOnlyList<AuthoritativeNativeTownMutation> typedMutationHistory)
    {
        ArgumentNullException.ThrowIfNull(currentGeometry);
        var mutations = ValidateMutations(typedMutationHistory);
        var replay = Replay(mutations);
        ValidateGeometryEquals(currentGeometry, replay, "current Town geometry");
        return CreateCheckpoint(replay, mutations);
    }

    /** Rebuilds a detached Town state without advancing any shared simulation RNG. */
    public AuthoritativeTownGeometry Restore(DiabloNativeTownCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        checkpoint.ValidateShape();
        if (checkpoint.Settings is null
            || checkpoint.ClosedHellTileChoices is null
            || checkpoint.Mutations is null
            || checkpoint.NativePieceLayer is null
            || checkpoint.NativeMegaTileLayer is null
            || checkpoint.NativeProtectedMegaCells is null
            || checkpoint.NativeSpecialLayer is null
            || checkpoint.NativeTransparencyLayer is null
            || checkpoint.NativeSolPropertyLayer is null
            || checkpoint.NativeSolidCells is null
            || checkpoint.NativeMissileBlockedCells is null
            || checkpoint.NativeBlockLightCells is null
            || checkpoint.NativePieceProperties is null) {
            throw new InvalidDataException("Native Town checkpoint is missing required state.");
        }
        var mutations = ValidateMutations(checkpoint.Mutations);
        var replay = Replay(mutations);
        ValidateCheckpoint(checkpoint, replay, mutations);
        return replay;
    }

    /** Materializes only the 112x112 fine layer; native 40x40 coarse state stays in the checkpoint. */
    public AuthoritativeLevel CreateBackingLevel(
        AuthoritativeTownGeometry geometry,
        AuthoritativeLevel prototype)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(prototype);
        ValidateGeometryForBacking(geometry);
        if (prototype.LevelId != LevelId
            || prototype.Width != AuthoritativeTownGeometry.FineWidth
            || prototype.Height != AuthoritativeTownGeometry.FineHeight) {
            throw new InvalidDataException(
                "Native Town backing requires the existing level-zero 112x112 prototype.");
        }

        return new AuthoritativeLevel(
            LevelId,
            AuthoritativeTownGeometry.FineWidth,
            AuthoritativeTownGeometry.FineHeight,
            geometry.NativeSolidCells,
            geometry.NativeMissileBlockedCells,
            prototype.IsDefaultStart,
            prototype.SuppressMultiplayerDeathDrops,
            geometry.NativeTransparencyLayer,
            prototype.NativeObjectLayer,
            null,
            geometry.NativePieceLayer,
            geometry.NativePieceProperties,
            geometry.NativeSolidCells,
            geometry.NativeMissileBlockedCells,
            prototype.NativeMonsterLayer,
            prototype.StartPositionX,
            prototype.StartPositionY,
            geometry.NativeBlockLightCells,
            geometry.NativeSpecialLayer,
            staticObjectBlockedCells: []) {
            StableSymbol = LevelSymbol,
        };
    }

    private AuthoritativeTownGeometry Replay(IReadOnlyList<AuthoritativeNativeTownMutation> mutations)
    {
        var geometry = CreateInitialGeometry();
        foreach (var mutation in mutations) {
            geometry = mutation switch {
                AuthoritativeNativeTownMutation.OpenHive => AuthoritativeTownGeometryBuilder.OpenHive(geometry),
                AuthoritativeNativeTownMutation.CloseHive => AuthoritativeTownGeometryBuilder.CloseHive(geometry),
                AuthoritativeNativeTownMutation.OpenGrave => AuthoritativeTownGeometryBuilder.OpenGrave(geometry),
                AuthoritativeNativeTownMutation.CloseGrave => AuthoritativeTownGeometryBuilder.CloseGrave(geometry),
                AuthoritativeNativeTownMutation.CleanFountain => AuthoritativeTownGeometryBuilder.CleanFountain(geometry),
                _ => throw new InvalidDataException($"Native Town mutation value {(byte)mutation} is unsupported."),
            };
        }
        return geometry;
    }

    private DiabloNativeTownCheckpoint CreateCheckpoint(
        AuthoritativeTownGeometry geometry,
        IReadOnlyList<AuthoritativeNativeTownMutation> mutations)
        => new(
            Fingerprint,
            LevelId,
            GeneratorId,
            GeneratorRevision,
            AuthoritativeTownWarpPolicy.Version,
            Settings,
            geometry.WarpOpenings,
            geometry.InitialRandomState,
            geometry.FinalRandomState,
            geometry.ViewPositionX,
            geometry.ViewPositionY,
            geometry.MinimumDungeonPositionX,
            geometry.MinimumDungeonPositionY,
            geometry.MaximumDungeonPositionX,
            geometry.MaximumDungeonPositionY,
            geometry.ClosedHellTileChoices,
            mutations,
            geometry.NativePieceLayer,
            geometry.NativeMegaTileLayer,
            geometry.NativeProtectedMegaCells,
            geometry.NativeSpecialLayer,
            geometry.NativeTransparencyLayer,
            geometry.NativeSolPropertyLayer,
            geometry.NativeSolidCells,
            geometry.NativeMissileBlockedCells,
            geometry.NativeBlockLightCells,
            geometry.NativePieceProperties);

    private void ValidateCheckpoint(
        DiabloNativeTownCheckpoint checkpoint,
        AuthoritativeTownGeometry expected,
        IReadOnlyList<AuthoritativeNativeTownMutation> mutations)
    {
        if (checkpoint.NativePieceLayer.Count != AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight
            || checkpoint.NativeMegaTileLayer.Count != AuthoritativeTownGeometry.MegaWidth * AuthoritativeTownGeometry.MegaHeight
            || checkpoint.NativeProtectedMegaCells.Count > AuthoritativeTownGeometry.MegaWidth * AuthoritativeTownGeometry.MegaHeight
            || checkpoint.NativeSpecialLayer.Count != AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight
            || checkpoint.NativeTransparencyLayer.Count != AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight
            || checkpoint.NativeSolPropertyLayer.Count != AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight
            || checkpoint.NativeSolidCells.Count > AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight
            || checkpoint.NativeMissileBlockedCells.Count > AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight
            || checkpoint.NativeBlockLightCells.Count > AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight
            || checkpoint.NativePieceProperties.Count != AuthoritativeDunTilePropertyCatalog.NativeTileCount
            || checkpoint.ClosedHellTileChoices.Count > 10) {
            throw new InvalidDataException("Native Town checkpoint contains an invalid retained-channel shape.");
        }
        if (!string.Equals(checkpoint.CatalogFingerprint, Fingerprint, StringComparison.Ordinal)
            || checkpoint.LevelId != LevelId
            || !string.Equals(checkpoint.GeneratorId, GeneratorId, StringComparison.Ordinal)
            || checkpoint.GeneratorRevision != GeneratorRevision
            || !string.Equals(checkpoint.WarpPolicyVersion, AuthoritativeTownWarpPolicy.Version, StringComparison.Ordinal)
            || checkpoint.Settings != Settings
            || checkpoint.WarpOpenings != expected.WarpOpenings
            || checkpoint.InitialRandomState != Settings.InitialRandomState
            || checkpoint.FinalRandomState != expected.FinalRandomState
            || checkpoint.ViewPositionX != expected.ViewPositionX
            || checkpoint.ViewPositionY != expected.ViewPositionY
            || checkpoint.MinimumDungeonPositionX != expected.MinimumDungeonPositionX
            || checkpoint.MinimumDungeonPositionY != expected.MinimumDungeonPositionY
            || checkpoint.MaximumDungeonPositionX != expected.MaximumDungeonPositionX
            || checkpoint.MaximumDungeonPositionY != expected.MaximumDungeonPositionY
            || !checkpoint.ClosedHellTileChoices.SequenceEqual(expected.ClosedHellTileChoices)
            || !checkpoint.Mutations.SequenceEqual(mutations)
            || !checkpoint.NativePieceLayer.SequenceEqual(expected.NativePieceLayer)
            || !checkpoint.NativeMegaTileLayer.SequenceEqual(expected.NativeMegaTileLayer)
            || !checkpoint.NativeProtectedMegaCells.SequenceEqual(expected.NativeProtectedMegaCells)
            || !checkpoint.NativeSpecialLayer.SequenceEqual(expected.NativeSpecialLayer)
            || !checkpoint.NativeTransparencyLayer.SequenceEqual(expected.NativeTransparencyLayer)
            || !checkpoint.NativeSolPropertyLayer.SequenceEqual(expected.NativeSolPropertyLayer)
            || !checkpoint.NativeSolidCells.SequenceEqual(expected.NativeSolidCells)
            || !checkpoint.NativeMissileBlockedCells.SequenceEqual(expected.NativeMissileBlockedCells)
            || !checkpoint.NativeBlockLightCells.SequenceEqual(expected.NativeBlockLightCells)
            || !DictionaryEqual(checkpoint.NativePieceProperties, expected.NativePieceProperties)) {
            throw new InvalidDataException("Native Town checkpoint does not match its catalog-bound replay.");
        }
    }

    private static void ValidateGeometryEquals(
        AuthoritativeTownGeometry actual,
        AuthoritativeTownGeometry expected,
        string label)
    {
        if (actual.Entry != expected.Entry
            || actual.TownWarpFrom != expected.TownWarpFrom
            || actual.WaterQuestState != expected.WaterQuestState
            || actual.InitialRandomState != expected.InitialRandomState
            || actual.FinalRandomState != expected.FinalRandomState
            || actual.WarpPolicy != expected.WarpPolicy
            || actual.WarpPolicyVersion != expected.WarpPolicyVersion
            || actual.WarpOpenings != expected.WarpOpenings
            || actual.ViewPositionX != expected.ViewPositionX
            || actual.ViewPositionY != expected.ViewPositionY
            || actual.MinimumDungeonPositionX != expected.MinimumDungeonPositionX
            || actual.MinimumDungeonPositionY != expected.MinimumDungeonPositionY
            || actual.MaximumDungeonPositionX != expected.MaximumDungeonPositionX
            || actual.MaximumDungeonPositionY != expected.MaximumDungeonPositionY
            || !actual.ClosedHellTileChoices.SequenceEqual(expected.ClosedHellTileChoices)
            || !actual.NativePieceLayer.SequenceEqual(expected.NativePieceLayer)
            || !actual.NativeMegaTileLayer.SequenceEqual(expected.NativeMegaTileLayer)
            || !actual.NativeProtectedMegaCells.SequenceEqual(expected.NativeProtectedMegaCells)
            || !actual.NativeSpecialLayer.SequenceEqual(expected.NativeSpecialLayer)
            || !actual.NativeTransparencyLayer.SequenceEqual(expected.NativeTransparencyLayer)
            || !actual.NativeSolPropertyLayer.SequenceEqual(expected.NativeSolPropertyLayer)
            || !actual.NativeSolidCells.SequenceEqual(expected.NativeSolidCells)
            || !actual.NativeMissileBlockedCells.SequenceEqual(expected.NativeMissileBlockedCells)
            || !actual.NativeBlockLightCells.SequenceEqual(expected.NativeBlockLightCells)
            || !DictionaryEqual(actual.NativePieceProperties, expected.NativePieceProperties)) {
            throw new InvalidDataException($"{label} does not match the catalog-bound Town geometry.");
        }
    }

    private void ValidateGeometryForBacking(AuthoritativeTownGeometry geometry)
    {
        if (!ReferenceEquals(geometry.PieceCatalog, PieceCatalog)
            || !ReferenceEquals(geometry.TileProperties, TileProperties)
            || geometry.Entry != Settings.Entry
            || geometry.TownWarpFrom != Settings.TownWarpFrom
            || geometry.WaterQuestState != Settings.WaterQuestState
            || geometry.InitialRandomState != Settings.InitialRandomState
            || geometry.WarpPolicy != Settings.WarpPolicy
            || geometry.WarpPolicyVersion != AuthoritativeTownWarpPolicy.Version
            || geometry.WarpOpenings != Settings.WarpPolicy.ResolveOpenings()
            || geometry.NativePieceLayer.Count != AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight
            || geometry.NativeMegaTileLayer.Count != AuthoritativeTownGeometry.MegaWidth * AuthoritativeTownGeometry.MegaHeight
            || geometry.NativeSpecialLayer.Count != AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight
            || geometry.NativeTransparencyLayer.Count != AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight) {
            throw new InvalidDataException("Town backing geometry does not belong to this native Town catalog.");
        }
        var channels = AuthoritativeTownGeometryBuilder.DeriveSolChannels(
            geometry.NativePieceLayer,
            TileProperties);
        if (!channels.Properties.SequenceEqual(geometry.NativeSolPropertyLayer)
            || !channels.Solid.SequenceEqual(geometry.NativeSolidCells)
            || !channels.Missile.SequenceEqual(geometry.NativeMissileBlockedCells)
            || !channels.BlockLight.SequenceEqual(geometry.NativeBlockLightCells)) {
            throw new InvalidDataException("Town backing SOL channels do not match the bound catalog.");
        }
    }

    private static bool DictionaryEqual<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> left,
        IReadOnlyDictionary<TKey, TValue> right)
        where TKey : notnull
        => left.Count == right.Count && left.All(pair =>
            right.TryGetValue(pair.Key, out var value)
            && EqualityComparer<TValue>.Default.Equals(pair.Value, value));

    private static IReadOnlyList<AuthoritativeNativeTownMutation> ValidateMutations(
        IReadOnlyList<AuthoritativeNativeTownMutation> mutations)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        if (mutations.Count > MaximumMutationCount)
            throw new InvalidDataException($"Native Town mutation history exceeds {MaximumMutationCount} operations.");
        var copy = mutations.ToArray();
        if (copy.Any(mutation => !Enum.IsDefined(mutation)))
            throw new InvalidDataException("Native Town mutation history contains an unsupported operation.");
        return Array.AsReadOnly(copy);
    }

    private static AuthoritativeNativeTownSettings ParseSettings(TsvRow row)
    {
        var settings = new AuthoritativeNativeTownSettings(
            row.RequiredBoolean("is_spawn"),
            row.RequiredBoolean("is_multiplayer"),
            row.RequiredBoolean("is_hellfire"),
            ParseInt32(row, "character_level"),
            (AuthoritativeTownWarpUnlocks)ParseByte(row, "warp_unlocks"),
            ParseFarmerState(row.Required("farmer_quest_state")),
            ParseGraveState(row.Required("grave_quest_state")),
            ParseEntry(row.Required("entry")),
            ParseInt32(row, "town_warp_from"),
            ParseWaterState(row.Required("water_quest_state")),
            ParseUInt32(row, "initial_random_state"));
        if (settings.IsSpawn
            || settings.IsMultiplayer
            || settings.IsHellfire
            || settings.CharacterLevel != 1
            || settings.WarpUnlocks != AuthoritativeTownWarpUnlocks.None
            || settings.FarmerQuestState != AuthoritativeTownFarmerQuestState.Other
            || settings.GraveQuestState != AuthoritativeTownGraveQuestState.Other
            || settings.Entry != AuthoritativeTownEntry.Main
            || settings.TownWarpFrom != 0
            || settings.WaterQuestState != AuthoritativeTownWaterQuestState.NotAvailable) {
            throw new InvalidDataException(
                $"{SettingsTableName} must bind original single-player normal Town entry with its canonical initial quest state.");
        }
        settings.WarpPolicy.Validate();
        return settings;
    }

    private static (AuthoritativeDunTilePropertyCatalog Properties, byte[] RawFlags) ParseTileProperties(TsvTable table)
    {
        if (table.Rows.Count != AuthoritativeDunTilePropertyCatalog.NativeTileCount)
            throw new InvalidDataException(
                $"{TilePropertiesTableName} must contain all {AuthoritativeDunTilePropertyCatalog.NativeTileCount} d-piece rows.");
        var definitions = new AuthoritativeDunTileProperty[table.Rows.Count];
        var rawFlags = new byte[table.Rows.Count];
        for (var index = 0; index < table.Rows.Count; index++) {
            var row = table.Rows[index];
            var pieceId = ParseUInt32(row, "piece_id");
            if (pieceId != (uint)index)
                throw new InvalidDataException(
                    $"{TilePropertiesTableName} must be ordered by complete zero-based piece_id; row {row.LineNumber} is out of order.");
            var rawValue = ParseUInt32(row, "source_flags");
            if (rawValue > byte.MaxValue)
                throw new InvalidDataException(
                    $"{TilePropertiesTableName} row {row.LineNumber} source_flags exceeds a byte.");
            var sourceFlags = checked((byte)rawValue);
            var semanticFlags = ReadSemanticFlags(row);
            const byte allowed = KnownSolFlagMask | NativeReservedSolFlagMask;
            if ((sourceFlags & ~allowed) != 0
                || (sourceFlags & KnownSolFlagMask) != (byte)semanticFlags) {
                throw new InvalidDataException(
                    $"{TilePropertiesTableName} row {row.LineNumber} source_flags does not match its known semantic flags.");
            }
            rawFlags[index] = sourceFlags;
            definitions[index] = new AuthoritativeDunTileProperty(pieceId, semanticFlags);
        }
        return (new AuthoritativeDunTilePropertyCatalog(definitions), rawFlags);
    }

    private static AuthoritativeDunTileFlags ReadSemanticFlags(TsvRow row)
    {
        var flags = AuthoritativeDunTileFlags.None;
        if (row.RequiredBoolean("solid")) flags |= AuthoritativeDunTileFlags.Solid;
        if (row.RequiredBoolean("block_light")) flags |= AuthoritativeDunTileFlags.BlockLight;
        if (row.RequiredBoolean("block_missile")) flags |= AuthoritativeDunTileFlags.BlockMissile;
        if (row.RequiredBoolean("transparent")) flags |= AuthoritativeDunTileFlags.Transparent;
        if (row.RequiredBoolean("transparent_left")) flags |= AuthoritativeDunTileFlags.TransparentLeft;
        if (row.RequiredBoolean("transparent_right")) flags |= AuthoritativeDunTileFlags.TransparentRight;
        if (row.RequiredBoolean("trap")) flags |= AuthoritativeDunTileFlags.Trap;
        return flags;
    }

    private static IReadOnlyDictionary<string, AuthoritativeTownTileMap> LoadMaps(
        TsvTable mapsTable,
        TsvTable mapTilesTable)
    {
        ValidateColumns(mapsTable, MapsColumns);
        RequireExactRows(mapsTable, MapIds.Length);
        var dimensions = new Dictionary<string, (int Width, int Height)>(StringComparer.Ordinal);
        for (var rowIndex = 0; rowIndex < mapsTable.Rows.Count; rowIndex++) {
            var row = mapsTable.Rows[rowIndex];
            var mapId = row.Required("map_id");
            if (rowIndex >= MapIds.Length || mapId != MapIds[rowIndex])
                throw new InvalidDataException($"{MapsTableName} has unsupported map id '{mapId}'.");
            var width = ParseInt32(row, "mega_width");
            var height = ParseInt32(row, "mega_height");
            var (expectedWidth, expectedHeight) = mapId switch {
                "sector1_south" => (25, 25),
                "sector2_south" => (25, 23),
                "sector3_south" => (23, 25),
                "sector4_south" => (23, 23),
                "automap" => (AuthoritativeTownGeometry.MegaWidth, AuthoritativeTownGeometry.MegaHeight),
                _ => throw new InvalidDataException($"{MapsTableName} has unsupported map id '{mapId}'."),
            };
            if (width != expectedWidth || height != expectedHeight) {
                throw new InvalidDataException($"{MapsTableName} row {row.LineNumber} has invalid dimensions for '{mapId}'.");
            }
            if (!dimensions.TryAdd(mapId, (width, height)))
                throw new InvalidDataException($"{MapsTableName} contains duplicate map id '{mapId}'.");
        }
        if (dimensions.Count != MapIds.Length)
            throw new InvalidDataException($"{MapsTableName} must define all five Town maps.");

        ValidateColumns(mapTilesTable, MapTilesColumns);
        var expectedRows = dimensions.Values.Sum(value => checked(value.Width * value.Height));
        if (expectedRows > MaximumMapTileRows || mapTilesTable.Rows.Count != expectedRows)
            throw new InvalidDataException($"{MapTilesTableName} must contain every row-major map cell exactly once.");
        var layers = dimensions.ToDictionary(
            pair => pair.Key,
            pair => new ushort[checked(pair.Value.Width * pair.Value.Height)],
            StringComparer.Ordinal);
        var expectedCell = MapIds.ToDictionary(mapId => mapId, _ => 0, StringComparer.Ordinal);
        var observedMapOrder = 0;
        for (var rowIndex = 0; rowIndex < mapTilesTable.Rows.Count; rowIndex++) {
            var row = mapTilesTable.Rows[rowIndex];
            var mapId = row.Required("map_id");
            while (observedMapOrder < MapIds.Length
                && expectedCell[MapIds[observedMapOrder]] == layers[MapIds[observedMapOrder]].Length) {
                observedMapOrder++;
            }
            if (observedMapOrder >= MapIds.Length || mapId != MapIds[observedMapOrder])
                throw new InvalidDataException(
                    $"{MapTilesTableName} rows must follow the fixed map and row-major cell order.");
            var (width, height) = dimensions[mapId];
            var cell = expectedCell[mapId];
            var x = ParseInt32(row, "x");
            var y = ParseInt32(row, "y");
            if (x != cell % width || y != cell / width || y >= height)
                throw new InvalidDataException(
                    $"{MapTilesTableName} row {row.LineNumber} is missing, duplicated, or out of row-major order.");
            var tileValue = ParseUInt32(row, "mega_tile_id");
            if (tileValue > ushort.MaxValue
                || mapId == "automap" && tileValue > byte.MaxValue) {
                throw new InvalidDataException(
                    $"{MapTilesTableName} row {row.LineNumber} has a mega-tile value outside the native range.");
            }
            layers[mapId][cell] = checked((ushort)tileValue);
            expectedCell[mapId] = cell + 1;
        }

        var maps = new Dictionary<string, AuthoritativeTownTileMap>(StringComparer.Ordinal);
        foreach (var mapId in MapIds) {
            var (width, height) = dimensions[mapId];
            maps.Add(mapId, CreateTileMap(mapId, width, height, layers[mapId]));
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, AuthoritativeTownTileMap>(maps);
    }

    private static AuthoritativeTownTileMap CreateTileMap(
        string mapId,
        int width,
        int height,
        IReadOnlyList<ushort> tiles)
    {
        var bytes = new byte[checked(4 + tiles.Count * sizeof(ushort))];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)width));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(sizeof(ushort)), checked((ushort)height));
        for (var cell = 0; cell < tiles.Count; cell++)
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4 + cell * sizeof(ushort)), tiles[cell]);
        return AuthoritativeTownTileMap.Load(
            $"native_town_geometry/{mapId}.semantic-map",
            bytes);
    }

    private static void ValidateMapReferences(
        IReadOnlyDictionary<string, AuthoritativeTownTileMap> maps,
        AuthoritativeDunPieceCatalog pieces)
    {
        foreach (var mapId in MapIds[..4]) {
            foreach (var oneBasedId in maps[mapId].TileLayer) {
                if (oneBasedId != 0 && !pieces.TryGet((uint)(oneBasedId - 1), out _))
                    throw new InvalidDataException(
                        $"Town map '{mapId}' references missing one-based mega-tile {oneBasedId}.");
            }
        }
    }

    private static string ComputeFingerprint(string manifestHash, IReadOnlyDictionary<string, TsvTable> tables)
    {
        var source = new StringBuilder()
            .Append(CatalogId).Append('\n')
            .Append(GeneratorId).Append('\n')
            .Append(GeneratorRevision.ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append(AuthoritativeTownWarpPolicy.Version).Append('\n')
            .Append(manifestHash).Append('\n');
        foreach (var path in tables.Keys.OrderBy(path => path, StringComparer.Ordinal))
            source.Append(path).Append('\n').Append(tables[path].ToCanonicalTsv());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.ToString()))).ToLowerInvariant();
    }

    private static void ValidateColumns(TsvTable table, IReadOnlyList<string> expected)
    {
        if (!table.Columns.SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"{table.SourcePath} must have the exact ordered header: {string.Join(',', expected)}.");
    }

    private static void RequireExactRows(TsvTable table, int expected)
    {
        if (table.Rows.Count != expected)
            throw new InvalidDataException($"{table.SourcePath} must contain exactly {expected} rows.");
    }

    private static int ParseInt32(TsvRow row, string column)
    {
        var text = row.Required(column);
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            throw new InvalidDataException(
                $"{row.LineNumber}:{column} must be a non-negative 32-bit integer.");
        return value;
    }

    private static uint ParseUInt32(TsvRow row, string column)
    {
        var text = row.Required(column);
        if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            throw new InvalidDataException(
                $"{row.LineNumber}:{column} must be an unsigned decimal 32-bit integer.");
        return value;
    }

    private static byte ParseByte(TsvRow row, string column)
    {
        var value = ParseUInt32(row, column);
        if (value > byte.MaxValue)
            throw new InvalidDataException($"{row.LineNumber}:{column} exceeds a byte.");
        return checked((byte)value);
    }

    private static AuthoritativeTownFarmerQuestState ParseFarmerState(string value)
        => value switch {
            "other" => AuthoritativeTownFarmerQuestState.Other,
            "done" => AuthoritativeTownFarmerQuestState.Done,
            "hive_done" => AuthoritativeTownFarmerQuestState.HiveDone,
            _ => throw new InvalidDataException($"Unsupported native Town farmer quest state '{value}'."),
        };

    private static AuthoritativeTownGraveQuestState ParseGraveState(string value)
        => value switch {
            "other" => AuthoritativeTownGraveQuestState.Other,
            "done" => AuthoritativeTownGraveQuestState.Done,
            _ => throw new InvalidDataException($"Unsupported native Town grave quest state '{value}'."),
        };

    private static AuthoritativeTownEntry ParseEntry(string value)
        => value switch {
            "main" => AuthoritativeTownEntry.Main,
            "previous" => AuthoritativeTownEntry.Previous,
            "set_level" => AuthoritativeTownEntry.SetLevel,
            "return_level" => AuthoritativeTownEntry.ReturnLevel,
            "load" => AuthoritativeTownEntry.Load,
            "warp_level" => AuthoritativeTownEntry.WarpLevel,
            "town_warp_down" => AuthoritativeTownEntry.TownWarpDown,
            "town_warp_up" => AuthoritativeTownEntry.TownWarpUp,
            _ => throw new InvalidDataException($"Unsupported native Town entry '{value}'."),
        };

    private static AuthoritativeTownWaterQuestState ParseWaterState(string value)
        => value switch {
            "not_available" => AuthoritativeTownWaterQuestState.NotAvailable,
            "init" => AuthoritativeTownWaterQuestState.Init,
            "active" => AuthoritativeTownWaterQuestState.Active,
            "done" => AuthoritativeTownWaterQuestState.Done,
            _ => throw new InvalidDataException($"Unsupported native Town water quest state '{value}'."),
        };
}

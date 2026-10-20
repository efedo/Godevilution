using System.Text.Json.Serialization;

namespace Devilution.Diablo;

/** Typed operations retained in their original order for native Town checkpoint replay. */
public enum AuthoritativeNativeTownMutation : byte
{
    OpenHive,
    CloseHive,
    OpenGrave,
    CloseGrave,
    CleanFountain,
}

/** Generation choices bound to one explicit native Town content profile. */
public sealed record AuthoritativeNativeTownSettings(
    bool IsSpawn,
    bool IsMultiplayer,
    bool IsHellfire,
    int CharacterLevel,
    AuthoritativeTownWarpUnlocks WarpUnlocks,
    AuthoritativeTownFarmerQuestState FarmerQuestState,
    AuthoritativeTownGraveQuestState GraveQuestState,
    AuthoritativeTownEntry Entry,
    int TownWarpFrom,
    AuthoritativeTownWaterQuestState WaterQuestState,
    uint InitialRandomState)
{
    [JsonIgnore]
    public AuthoritativeTownWarpPolicy WarpPolicy => new(
        IsSpawn,
        IsMultiplayer,
        IsHellfire,
        CharacterLevel,
        WarpUnlocks,
        FarmerQuestState,
        GraveQuestState);
}

/** Detached, catalog-bound native Town state. Restore always regenerates and verifies every channel. */
public sealed record DiabloNativeTownCheckpoint
{
    public DiabloNativeTownCheckpoint(
        string catalogFingerprint,
        uint levelId,
        string generatorId,
        int generatorRevision,
        string warpPolicyVersion,
        AuthoritativeNativeTownSettings settings,
        AuthoritativeTownWarpOpenings warpOpenings,
        uint initialRandomState,
        uint finalRandomState,
        int viewPositionX,
        int viewPositionY,
        int minimumDungeonPositionX,
        int minimumDungeonPositionY,
        int maximumDungeonPositionX,
        int maximumDungeonPositionY,
        IReadOnlyList<int> closedHellTileChoices,
        IReadOnlyList<AuthoritativeNativeTownMutation> mutations,
        IReadOnlyList<ushort> nativePieceLayer,
        IReadOnlyList<ushort> nativeMegaTileLayer,
        IReadOnlyList<int> nativeProtectedMegaCells,
        IReadOnlyList<ushort> nativeSpecialLayer,
        IReadOnlyList<ushort> nativeTransparencyLayer,
        IReadOnlyList<AuthoritativeDunTileFlags> nativeSolPropertyLayer,
        IReadOnlyList<int> nativeSolidCells,
        IReadOnlyList<int> nativeMissileBlockedCells,
        IReadOnlyList<int> nativeBlockLightCells,
        IReadOnlyDictionary<ushort, AuthoritativeDunTileFlags> nativePieceProperties)
    {
        CatalogFingerprint = catalogFingerprint;
        LevelId = levelId;
        GeneratorId = generatorId;
        GeneratorRevision = generatorRevision;
        WarpPolicyVersion = warpPolicyVersion;
        if (settings is null || settings.InitialRandomState != initialRandomState)
            throw new InvalidDataException("Native Town checkpoint initial RNG state does not match its settings.");
        Settings = settings;
        WarpOpenings = warpOpenings;
        InitialRandomState = initialRandomState;
        FinalRandomState = finalRandomState;
        ViewPositionX = viewPositionX;
        ViewPositionY = viewPositionY;
        MinimumDungeonPositionX = minimumDungeonPositionX;
        MinimumDungeonPositionY = minimumDungeonPositionY;
        MaximumDungeonPositionX = maximumDungeonPositionX;
        MaximumDungeonPositionY = maximumDungeonPositionY;
        ClosedHellTileChoices = closedHellTileChoices;
        Mutations = mutations;
        NativePieceLayer = nativePieceLayer;
        NativeMegaTileLayer = nativeMegaTileLayer;
        NativeProtectedMegaCells = nativeProtectedMegaCells;
        NativeSpecialLayer = nativeSpecialLayer;
        NativeTransparencyLayer = nativeTransparencyLayer;
        NativeSolPropertyLayer = nativeSolPropertyLayer;
        NativeSolidCells = nativeSolidCells;
        NativeMissileBlockedCells = nativeMissileBlockedCells;
        NativeBlockLightCells = nativeBlockLightCells;
        NativePieceProperties = nativePieceProperties;
        ValidateShape();
    }

    public string CatalogFingerprint { get; init; }
    public uint LevelId { get; init; }
    public string GeneratorId { get; init; }
    public int GeneratorRevision { get; init; }
    public string WarpPolicyVersion { get; init; }
    public AuthoritativeNativeTownSettings Settings { get; init; }
    public AuthoritativeTownWarpOpenings WarpOpenings { get; init; }
    public uint InitialRandomState { get; init; }
    public uint FinalRandomState { get; init; }
    public int ViewPositionX { get; init; }
    public int ViewPositionY { get; init; }
    public int MinimumDungeonPositionX { get; init; }
    public int MinimumDungeonPositionY { get; init; }
    public int MaximumDungeonPositionX { get; init; }
    public int MaximumDungeonPositionY { get; init; }
    public IReadOnlyList<int> ClosedHellTileChoices { get; init; }
    public IReadOnlyList<AuthoritativeNativeTownMutation> Mutations { get; init; }
    public IReadOnlyList<ushort> NativePieceLayer { get; init; }
    public IReadOnlyList<ushort> NativeMegaTileLayer { get; init; }
    public IReadOnlyList<int> NativeProtectedMegaCells { get; init; }
    public IReadOnlyList<ushort> NativeSpecialLayer { get; init; }
    public IReadOnlyList<ushort> NativeTransparencyLayer { get; init; }
    public IReadOnlyList<AuthoritativeDunTileFlags> NativeSolPropertyLayer { get; init; }
    public IReadOnlyList<int> NativeSolidCells { get; init; }
    public IReadOnlyList<int> NativeMissileBlockedCells { get; init; }
    public IReadOnlyList<int> NativeBlockLightCells { get; init; }
    public IReadOnlyDictionary<ushort, AuthoritativeDunTileFlags> NativePieceProperties { get; init; }

    /** Rejects malformed checkpoint envelopes before any catalog replay or world mutation. */
    public void ValidateShape()
    {
        const AuthoritativeDunTileFlags supportedFlags =
            AuthoritativeDunTileFlags.Solid
            | AuthoritativeDunTileFlags.BlockLight
            | AuthoritativeDunTileFlags.BlockMissile
            | AuthoritativeDunTileFlags.Transparent
            | AuthoritativeDunTileFlags.TransparentLeft
            | AuthoritativeDunTileFlags.TransparentRight
            | AuthoritativeDunTileFlags.Trap;
        var fineCellCount = AuthoritativeTownGeometry.FineWidth * AuthoritativeTownGeometry.FineHeight;
        var megaCellCount = AuthoritativeTownGeometry.MegaWidth * AuthoritativeTownGeometry.MegaHeight;
        if (CatalogFingerprint is null || CatalogFingerprint.Length != 64
            || CatalogFingerprint.Any(value => value is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            || LevelId != AuthoritativeNativeTownCatalog.TownLevelId
            || GeneratorId != AuthoritativeNativeTownCatalog.GeneratorId
            || GeneratorRevision != AuthoritativeNativeTownCatalog.GeneratorRevision
            || WarpPolicyVersion != AuthoritativeTownWarpPolicy.Version
            || Settings is null
            || ClosedHellTileChoices is null
            || Mutations is null
            || NativePieceLayer is null
            || NativeMegaTileLayer is null
            || NativeProtectedMegaCells is null
            || NativeSpecialLayer is null
            || NativeTransparencyLayer is null
            || NativeSolPropertyLayer is null
            || NativeSolidCells is null
            || NativeMissileBlockedCells is null
            || NativeBlockLightCells is null
            || NativePieceProperties is null) {
            throw new InvalidDataException("Native Town checkpoint has an unsupported or incomplete shape.");
        }

        try {
            Settings.WarpPolicy.Validate();
        } catch (ArgumentOutOfRangeException exception) {
            throw new InvalidDataException("Native Town checkpoint contains invalid warp-policy settings.", exception);
        }
        if (Settings.IsSpawn
            || Settings.IsMultiplayer
            || Settings.IsHellfire
            || Settings.CharacterLevel != 1
            || Settings.WarpUnlocks != AuthoritativeTownWarpUnlocks.None
            || Settings.FarmerQuestState != AuthoritativeTownFarmerQuestState.Other
            || Settings.GraveQuestState != AuthoritativeTownGraveQuestState.Other
            || Settings.Entry != AuthoritativeTownEntry.Main
            || Settings.TownWarpFrom != 0
            || Settings.WaterQuestState != AuthoritativeTownWaterQuestState.NotAvailable
            || InitialRandomState != Settings.InitialRandomState
            || WarpOpenings != new AuthoritativeTownWarpOpenings(false, false, false, false, false)
            || (ViewPositionX, ViewPositionY) != (75, 68)
            || (MinimumDungeonPositionX, MinimumDungeonPositionY,
                MaximumDungeonPositionX, MaximumDungeonPositionY) != (10, 10, 84, 84)
            || ClosedHellTileChoices.Count != 10
            || ClosedHellTileChoices.Any(choice => choice is < 1 or > 4)
            || Mutations.Count > AuthoritativeNativeTownCatalog.MaximumMutationCount
            || Mutations.Any(mutation => !Enum.IsDefined(mutation))) {
            throw new InvalidDataException("Native Town checkpoint contains invalid bound settings or generation metadata.");
        }

        if (NativePieceLayer.Count != fineCellCount
            || NativePieceLayer.Any(pieceId => pieceId >= AuthoritativeDunTilePropertyCatalog.NativeTileCount)
            || NativeMegaTileLayer.Count != megaCellCount
            || NativeMegaTileLayer.Any(megaTileId => megaTileId > byte.MaxValue)
            || NativeProtectedMegaCells.Any(cell => cell < 0 || cell >= megaCellCount)
            || NativeProtectedMegaCells.Distinct().Count() != NativeProtectedMegaCells.Count
            || !NativeProtectedMegaCells.SequenceEqual(NativeProtectedMegaCells.Order())
            || NativeSpecialLayer.Count != fineCellCount
            || NativeSpecialLayer.Any(special => special > 18)
            || NativeTransparencyLayer.Count != fineCellCount
            || NativeTransparencyLayer.Any(value => value != 0)
            || NativeSolPropertyLayer.Count != fineCellCount
            || NativeSolPropertyLayer.Any(flags => (flags & ~supportedFlags) != 0)
            || !IsSortedUniqueCells(NativeSolidCells, fineCellCount)
            || !IsSortedUniqueCells(NativeMissileBlockedCells, fineCellCount)
            || !IsSortedUniqueCells(NativeBlockLightCells, fineCellCount)
            || NativePieceProperties.Count != AuthoritativeDunTilePropertyCatalog.NativeTileCount
            || NativePieceProperties.Any(pair => pair.Key >= AuthoritativeDunTilePropertyCatalog.NativeTileCount
                || (pair.Value & ~supportedFlags) != 0)) {
            throw new InvalidDataException("Native Town checkpoint contains invalid retained geometry channels.");
        }
    }

    private static bool IsSortedUniqueCells(IReadOnlyList<int> cells, int maximumCellCount)
    {
        if (cells.Any(cell => cell < 0 || cell >= maximumCellCount))
            return false;
        for (var index = 1; index < cells.Count; index++) {
            if (cells[index - 1] >= cells[index])
                return false;
        }
        return true;
    }
}

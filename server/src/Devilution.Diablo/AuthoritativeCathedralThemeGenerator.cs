using Devilution.Engine;

namespace Devilution.Diablo;

/** One Cathedral theme selected by native InitThemes, retaining its region identity. */
public sealed record AuthoritativeCathedralThemeEntry(int ThemeType, int RegionId);

/** InitThemes flags which remain live for the later CreateThemeRooms phase. */
public sealed record AuthoritativeCathedralThemeFlags(
    bool ArmorAvailable,
    bool WeaponAvailable,
    bool CauldronAvailable,
    bool BloodFountainAvailable,
    bool PurifyingFountainAvailable,
    bool MurkyFountainAvailable,
    bool TearFountainAvailable,
    bool TreasureAvailable);

/** Detached Cathedral inputs for the pure InitThemes + HoldThemeRooms phase. */
public sealed class AuthoritativeCathedralThemeParameters
{
    public const int Width = 112;
    public const int CellCount = Width * Width;
    public const int PieceCount = 1379;
    public const int MaxThemes = 50;
    public const int MaxNativeMonsterId = 137;
    public const int MaxSelectedBestiaryTypes = 24;

    public AuthoritativeCathedralThemeParameters(
        int depth,
        IReadOnlyList<ushort> pieces,
        IReadOnlyList<byte> transparency,
        IReadOnlyList<byte> pieceProperties,
        IReadOnlyList<bool> initialPopulated,
        IReadOnlyList<bool> objectOccupied,
        int nextTransparencyValue,
        IReadOnlyList<AuthoritativeCathedralPopulationPoint> triggerPositions,
        IReadOnlyList<int> selectedBestiaryNativeIds,
        uint startingRandomState,
        int maximumThemeSelectionAttempts = 1_000_000)
        : this(depth, pieces, transparency, pieceProperties, initialPopulated, objectOccupied,
            nextTransparencyValue, triggerPositions, selectedBestiaryNativeIds, startingRandomState,
            maximumThemeSelectionAttempts, AuthoritativeNativeDungeonFamilyContext.Cathedral(depth))
    {
    }

    internal AuthoritativeCathedralThemeParameters(
        int depth,
        IReadOnlyList<ushort> pieces,
        IReadOnlyList<byte> transparency,
        IReadOnlyList<byte> pieceProperties,
        IReadOnlyList<bool> initialPopulated,
        IReadOnlyList<bool> objectOccupied,
        int nextTransparencyValue,
        IReadOnlyList<AuthoritativeCathedralPopulationPoint> triggerPositions,
        IReadOnlyList<int> selectedBestiaryNativeIds,
        uint startingRandomState,
        int maximumThemeSelectionAttempts,
        AuthoritativeNativeDungeonFamilyContext familyContext)
    {
        familyContext.RequireMatch(depth);
        if (familyContext.IsCatacombs != (depth is >= 5 and <= 8))
            throw new InvalidDataException("Native theme depth does not match its explicit dungeon family.");
        if (nextTransparencyValue is < -128
            || (familyContext.IsCatacombs ? nextTransparencyValue > sbyte.MaxValue : nextTransparencyValue > 126))
            throw new InvalidDataException("The native int8 next transparency value is outside the safe theme-loop range.");
        if (maximumThemeSelectionAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumThemeSelectionAttempts));

        Pieces = Copy(pieces, CellCount, nameof(pieces));
        Transparency = Copy(transparency, CellCount, nameof(transparency));
        PieceProperties = Copy(pieceProperties, PieceCount, nameof(pieceProperties));
        InitialPopulated = Copy(initialPopulated, CellCount, nameof(initialPopulated));
        ObjectOccupied = Copy(objectOccupied, CellCount, nameof(objectOccupied));
        if (ObjectOccupied.Any(occupied => occupied))
            throw new InvalidDataException("Cathedral theme generation requires fresh empty object occupancy.");
        if (Pieces.Any(piece => piece >= PieceCount))
            throw new InvalidDataException("Cathedral pieces must resolve in the supplied SOL catalog.");

        ArgumentNullException.ThrowIfNull(triggerPositions);
        var triggers = triggerPositions.ToArray();
        if (triggers.Any(position => !Contains(position)))
            throw new InvalidDataException("Cathedral theme trigger positions must be in bounds.");
        TriggerPositions = Array.AsReadOnly(triggers);

        ArgumentNullException.ThrowIfNull(selectedBestiaryNativeIds);
        var bestiary = selectedBestiaryNativeIds.ToArray();
        if (bestiary.Length > MaxSelectedBestiaryTypes)
            throw new InvalidDataException($"Cathedral bestiary selection cannot exceed {MaxSelectedBestiaryTypes} native types.");
        if (bestiary.Any(nativeId => nativeId is < 0 or > MaxNativeMonsterId))
            throw new InvalidDataException("Cathedral bestiary native IDs are outside the native monster range.");
        if (bestiary.Distinct().Count() != bestiary.Length)
            throw new InvalidDataException("Cathedral bestiary native IDs must be unique.");
        SelectedBestiaryNativeIds = Array.AsReadOnly(bestiary);

        Depth = depth;
        FamilyContext = familyContext;
        NextTransparencyValue = nextTransparencyValue;
        StartingRandomState = startingRandomState;
        MaximumThemeSelectionAttempts = maximumThemeSelectionAttempts;
    }

    public int Depth { get; }
    internal AuthoritativeNativeDungeonFamilyContext FamilyContext { get; }
    public IReadOnlyList<ushort> Pieces { get; }
    public IReadOnlyList<byte> Transparency { get; }
    public IReadOnlyList<byte> PieceProperties { get; }
    /** Native DungeonFlag::Populated before InitThemes; also represents set-piece rejection. */
    public IReadOnlyList<bool> InitialPopulated { get; }
    public IReadOnlyList<bool> ObjectOccupied { get; }
    public int NextTransparencyValue { get; }
    public IReadOnlyList<AuthoritativeCathedralPopulationPoint> TriggerPositions { get; }
    public IReadOnlyList<int> SelectedBestiaryNativeIds { get; }
    public uint StartingRandomState { get; }
    public int MaximumThemeSelectionAttempts { get; }

    internal static bool Contains(AuthoritativeCathedralPopulationPoint point)
        => point.X is >= 0 and < Width && point.Y is >= 0 and < Width;

    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values, int count, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        if (values.Count != count)
            throw new InvalidDataException($"Cathedral theme {name} requires exactly {count} values.");
        return Array.AsReadOnly(values.ToArray());
    }
}

/** Detached result of pure Cathedral InitThemes followed by Cathedral HoldThemeRooms. */
public sealed class AuthoritativeCathedralThemeResult
{
    internal AuthoritativeCathedralThemeResult(
        IReadOnlyList<AuthoritativeCathedralThemeEntry> themes,
        IReadOnlyList<bool> heldPopulatedCells,
        uint finalRandomState,
        int nextTransparencyValue,
        AuthoritativeCathedralThemeFlags flags,
        int placementVariant = 0,
        AuthoritativeCathedralPopulationPoint placementOrigin = default)
    {
        Themes = Array.AsReadOnly(themes.ToArray());
        HeldPopulatedCells = Array.AsReadOnly(heldPopulatedCells.ToArray());
        FinalRandomState = finalRandomState;
        NextTransparencyValue = nextTransparencyValue;
        Flags = flags ?? throw new ArgumentNullException(nameof(flags));
        PlacementVariant = placementVariant;
        PlacementOrigin = placementOrigin;
    }

    public IReadOnlyList<AuthoritativeCathedralThemeEntry> Themes { get; }
    public IReadOnlyList<bool> HeldPopulatedCells { get; }
    /** Alias matching the native tile flag's post-HoldThemeRooms meaning. */
    public IReadOnlyList<bool> PopulatedCells => HeldPopulatedCells;
    public uint FinalRandomState { get; }
    public int NextTransparencyValue { get; }
    public AuthoritativeCathedralThemeFlags Flags { get; }
    /** Native themeVar1 carry required by a later ordered CreateThemeRooms pass. */
    public int PlacementVariant { get; }
    /** Native themex/themey carry from the selection phase, before room creation. */
    public AuthoritativeCathedralPopulationPoint PlacementOrigin { get; }
}

/** Pure, bounded managed implementation of the ordinary Cathedral theme phase. */
public static class AuthoritativeCathedralThemeGenerator
{
    public const int ThemeBarrel = 0;
    public const int ThemeShrine = 1;
    public const int ThemeMonsterPit = 2;
    public const int ThemeSkeletonRoom = 3;
    public const int ThemeTreasure = 4;
    public const int ThemeLibrary = 5;
    public const int ThemeTorture = 6;
    public const int ThemeBloodFountain = 7;
    public const int ThemeDecapitated = 8;
    public const int ThemePurifyingFountain = 9;
    public const int ThemeArmorStand = 10;
    public const int ThemeGoatShrine = 11;
    public const int ThemeCauldron = 12;
    public const int ThemeMurkyFountain = 13;
    public const int ThemeTearFountain = 14;
    public const int ThemeBurningCross = 15;
    public const int ThemeWeaponRack = 16;

    private const byte SolidFlag = 1;
    private const byte TrapFlag = 128;
    private static readonly int[] ThemeGood = [ThemeGoatShrine, ThemeShrine, ThemeSkeletonRoom, ThemeLibrary];
    private static readonly int[] SkeletonNativeIds = [8, 9, 10, 11, 20, 21, 22, 23, 24, 25, 26, 27];
    private static readonly int[] GoatNativeIds = [34, 35, 36, 37, 42, 43, 44, 45];
    private static readonly (int X, int Y)[] Cardinal = [(0, -1), (1, 0), (0, 1), (-1, 0)];

    private static bool IsSkeleton(int nativeId) => Array.IndexOf(SkeletonNativeIds, nativeId) >= 0;
    private static bool IsGoat(int nativeId) => Array.IndexOf(GoatNativeIds, nativeId) >= 0;

    public static AuthoritativeCathedralThemeResult Generate(AuthoritativeCathedralThemeParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return Generate(parameters, AuthoritativeNativeDungeonFamilyContext.Cathedral(parameters.Depth));
    }

    internal static AuthoritativeCathedralThemeResult Generate(
        AuthoritativeCathedralThemeParameters parameters,
        AuthoritativeNativeDungeonFamilyContext familyContext)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        familyContext.RequireMatch(parameters.Depth);
        if (parameters.FamilyContext != familyContext)
            throw new InvalidDataException("Native theme parameters were created for a different dungeon family.");
        return new ThemePhase(parameters, familyContext, null, null).Generate();
    }

    internal static (AuthoritativeCathedralThemeResult Themes, IReadOnlyList<AuthoritativeCatacombsSelectedTheme> Selections)
        GenerateCatacombs(
            AuthoritativeCathedralThemeParameters parameters,
            AuthoritativeNativeDungeonFamilyContext familyContext,
            IReadOnlyList<AuthoritativeCatacombsThemeRoom> generatedRooms,
            IReadOnlyList<int> selectedBestiaryNativeIds,
            uint startingRandomState)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(generatedRooms);
        ArgumentNullException.ThrowIfNull(selectedBestiaryNativeIds);
        familyContext.RequireMatch(parameters.Depth);
        if (!familyContext.IsCatacombs || parameters.FamilyContext != familyContext)
            throw new InvalidDataException("Generated L2 themes require explicit Catacombs parameters.");
        return new ThemePhase(parameters, familyContext, generatedRooms, selectedBestiaryNativeIds, startingRandomState).GenerateCatacombs();
    }

    private sealed class ThemePhase
    {
        private readonly AuthoritativeCathedralThemeParameters input;
        private readonly AuthoritativeNativeDungeonFamilyContext familyContext;
        private readonly IReadOnlyList<AuthoritativeCatacombsThemeRoom>? generatedRooms;
        private readonly IReadOnlyList<int> selectedBestiaryNativeIds;
        private readonly DeterministicLegacyRandom random;
        private readonly sbyte[] transparency;
        private readonly bool[] heldPopulated;
        private readonly ThemeFlags flags = new();
        private readonly ThemeScratch scratch = new();
        private readonly List<AuthoritativeCathedralThemeEntry> themes = [];
        private readonly List<AuthoritativeCatacombsSelectedTheme> catacombsSelections = [];

        public ThemePhase(
            AuthoritativeCathedralThemeParameters input,
            AuthoritativeNativeDungeonFamilyContext familyContext,
            IReadOnlyList<AuthoritativeCatacombsThemeRoom>? generatedRooms,
            IReadOnlyList<int>? selectedBestiaryNativeIds,
            uint? startingRandomState = null)
        {
            this.input = input;
            this.familyContext = familyContext;
            this.generatedRooms = generatedRooms;
            this.selectedBestiaryNativeIds = selectedBestiaryNativeIds ?? input.SelectedBestiaryNativeIds;
            random = new DeterministicLegacyRandom(startingRandomState ?? input.StartingRandomState);
            transparency = input.Transparency.Select(value => unchecked((sbyte)value)).ToArray();
            heldPopulated = input.InitialPopulated.ToArray();
        }

        public AuthoritativeCathedralThemeResult Generate()
        {
            if (familyContext.IsCatacombs)
                throw new InvalidOperationException("Catacombs theme selection requires the ordered generated rooms.");
            // The native int8 loop checks a region, then breaks only when the current
            // region is greater than nextTransparencyValue; therefore next+1 is visited.
            var lastRegion = Math.Max(0, input.NextTransparencyValue + 1);
            for (var region = 0; region <= lastRegion && themes.Count < AuthoritativeCathedralThemeParameters.MaxThemes; region++) {
                if (!CheckThemeRoom(region))
                    continue;
                var themeType = SelectThemeType(themes.Count, region);
                themes.Add(new AuthoritativeCathedralThemeEntry(themeType, region));
            }

            foreach (var theme in themes)
                for (var index = 0; index < AuthoritativeCathedralThemeParameters.CellCount; index++)
                    if (transparency[index] == theme.RegionId)
                        heldPopulated[index] = true;

            return new(
                themes,
                heldPopulated,
                random.State,
                input.NextTransparencyValue,
                flags.ToPublic(),
                scratch.Variant,
                new(scratch.X, scratch.Y));
        }

        public (AuthoritativeCathedralThemeResult Themes, IReadOnlyList<AuthoritativeCatacombsSelectedTheme> Selections)
            GenerateCatacombs()
        {
            if (!familyContext.IsCatacombs || generatedRooms is null)
                throw new InvalidOperationException("Catacombs theme selection requires the explicit generated-room list.");
            if (generatedRooms.Count > AuthoritativeCathedralThemeParameters.MaxThemes)
                throw new InvalidDataException("Catacombs generated theme rooms exceed the native theme limit.");
            foreach (var room in generatedRooms) {
                if (room is null || room.Transparency is < sbyte.MinValue or > sbyte.MaxValue)
					throw new InvalidDataException("Catacombs theme rooms require signed native transparency IDs.");
                var themeType = SelectThemeType(themes.Count, room.Transparency);
                themes.Add(new AuthoritativeCathedralThemeEntry(themeType, room.Transparency));
                catacombsSelections.Add(new(themeType, room with { }, scratch.X, scratch.Y, scratch.Variant));
                for (var y = room.Y; y < room.Y + room.Height - 1; y++) {
                    for (var x = room.X; x < room.X + room.Width - 1; x++) {
                        var fineX = 16 + 2 * x;
                        var fineY = 16 + 2 * y;
                        for (var dy = 0; dy <= 1; dy++)
                            for (var dx = 0; dx <= 1; dx++)
                                heldPopulated[Index(fineX + dx, fineY + dy)] = true;
                    }
                }
            }
            var result = new AuthoritativeCathedralThemeResult(themes, heldPopulated, random.State,
                input.NextTransparencyValue, flags.ToPublic(), scratch.Variant, new(scratch.X, scratch.Y));
            return (result, Array.AsReadOnly(catacombsSelections.ToArray()));
        }

        private int SelectThemeType(int themeIndex, int region)
        {
            _ = themeIndex;
            for (var attempt = 0; attempt < input.MaximumThemeSelectionAttempts; attempt++) {
                var candidate = attempt == 0
                    ? ThemeGood[RequireNonNegativeRandom(4)]
                    : RequireThemeRandom(17);
                if (SpecialThemeFit(region, candidate))
                    return candidate;
            }
            throw new InvalidDataException("Cathedral theme selection exhausted the configured safety bound.");
        }

        private bool CheckThemeRoom(int region)
        {
            foreach (var trigger in input.TriggerPositions)
                if (RegionAt(trigger) == region)
                    return false;

            var area = 0;
            for (var y = 0; y < AuthoritativeCathedralThemeParameters.Width; y++) {
                for (var x = 0; x < AuthoritativeCathedralThemeParameters.Width; x++) {
                    var index = Index(x, y);
                    if (transparency[index] != region)
                        continue;
                    if (input.InitialPopulated[index])
                        return false;
                    area++;
                }
            }
            if (area is < 9 or > 100)
                return false;

            for (var y = 0; y < AuthoritativeCathedralThemeParameters.Width; y++) {
                for (var x = 0; x < AuthoritativeCathedralThemeParameters.Width; x++) {
                    var position = new AuthoritativeCathedralPopulationPoint(x, y);
                    if (RegionAt(position) != region || IsSolid(position))
                        continue;
                    foreach (var (dx, dy) in Cardinal) {
                        var neighbor = new AuthoritativeCathedralPopulationPoint(x + dx, y + dy);
                        if (IsNotSolid(neighbor) && RegionAt(neighbor) != region)
                            return false;
                    }
                }
            }
            return true;
        }

        private bool SpecialThemeFit(int region, int themeType)
        {
            if (!ThemeRequirementsAllow(themeType))
                return false;
            switch (themeType) {
            case ThemeShrine:
            case ThemeLibrary:
                return FitShrine(region);
            case ThemeSkeletonRoom:
                if (!selectedBestiaryNativeIds.Any(IsSkeleton))
                    return false;
                scratch.Variant = Array.FindIndex(selectedBestiaryNativeIds.ToArray(), IsSkeleton);
                return FitObj5(region);
            case ThemeGoatShrine:
                if (!selectedBestiaryNativeIds.Any(IsGoat))
                    return false;
                scratch.Variant = Array.FindIndex(selectedBestiaryNativeIds.ToArray(), IsGoat);
                return FitObj5(region);
            case ThemeBloodFountain:
                if (!FitObj5(region)) return false;
                flags.BloodFountainAvailable = false;
                return true;
            case ThemePurifyingFountain:
                if (!FitObj5(region)) return false;
                flags.PurifyingFountainAvailable = false;
                return true;
            case ThemeMurkyFountain:
                if (!FitObj5(region)) return false;
                flags.MurkyFountainAvailable = false;
                return true;
            case ThemeTearFountain:
                if (!FitObj5(region)) return false;
                flags.TearFountainAvailable = false;
                return true;
            case ThemeCauldron:
                if (!FitObj5(region)) return false;
                flags.CauldronAvailable = false;
                return true;
            case ThemeTorture:
            case ThemeDecapitated:
            case ThemeArmorStand:
            case ThemeBurningCross:
            case ThemeWeaponRack:
                return FitObj3(region, 4);
            case ThemeTreasure:
                flags.TreasureAvailable = false;
                return true;
            default:
                return true;
            }
        }

        private bool ThemeRequirementsAllow(int themeType)
        {
            switch (themeType) {
            case ThemeShrine:
            case ThemeSkeletonRoom:
            case ThemeLibrary:
                return true;
            case ThemeArmorStand:
            case ThemeWeaponRack:
                return familyContext.IsCatacombs;
            case ThemeCauldron:
                return false;
            case ThemeBloodFountain:
                return flags.BloodFountainAvailable;
            case ThemePurifyingFountain:
                return flags.PurifyingFountainAvailable;
            case ThemeMurkyFountain:
                return flags.MurkyFountainAvailable;
            case ThemeTearFountain:
                return flags.TearFountainAvailable;
            case ThemeTreasure:
                return flags.TreasureAvailable;
            default:
                return true;
            }
        }

        private bool FitShrine(int region)
        {
            for (var y = 0; y < AuthoritativeCathedralThemeParameters.Width; y++) {
                for (var x = 0; x < AuthoritativeCathedralThemeParameters.Width; x++) {
                    var position = new AuthoritativeCathedralPopulationPoint(x, y);
                    if (RegionAt(position) != region)
                        continue;
                    var northEast = Offset(position, 0, -1);
                    if (HasFlag(northEast, TrapFlag)
                        && IsNotSolid(Offset(position, -1, 0))
                        && IsNotSolid(Offset(position, 1, 0))
                        && RegionAt(Offset(position, -1, 0)) == region
                        && RegionAt(Offset(position, 1, 0)) == region
                        && !HasObject(Offset(position, -1, -1))
                        && !HasObject(Offset(position, 1, -1))) {
                        scratch.X = x;
                        scratch.Y = y;
                        scratch.Variant = 1;
                        return true;
                    }

                    var northWest = Offset(position, -1, 0);
                    if (HasFlag(northWest, TrapFlag)
                        && IsNotSolid(Offset(position, 0, -1))
                        && IsNotSolid(Offset(position, 0, 1))
                        && RegionAt(Offset(position, 0, -1)) == region
                        && RegionAt(Offset(position, 0, 1)) == region
                        && !HasObject(Offset(position, -1, -1))
                        && !HasObject(Offset(position, -1, 1))) {
                        scratch.X = x;
                        scratch.Y = y;
                        scratch.Variant = 2;
                        return true;
                    }
                }
            }
            return false;
        }

        private bool FitObj5(int region)
        {
            var targetCandidates = random.GenerateRnd(5);
            if (targetCandidates < 0) {
                scratch.X = 0;
                scratch.Y = 0;
                return true;
            }

            var candidatesFound = 0;
            for (var y = 0; y < AuthoritativeCathedralThemeParameters.Width; y++) {
                for (var x = 0; x < AuthoritativeCathedralThemeParameters.Width; x++) {
                    var position = new AuthoritativeCathedralPopulationPoint(x, y);
                    if (RegionAt(position) != region || !IsNotSolid(position) || !CheckObj5(position, region))
                        continue;
                    scratch.X = x;
                    scratch.Y = y;
                    candidatesFound++;
                    if (candidatesFound > targetCandidates)
                        return true;
                }
            }
            return candidatesFound > 0;
        }

        private bool CheckObj5(AuthoritativeCathedralPopulationPoint origin, int region)
        {
            for (var y = -2; y <= 2; y++)
                for (var x = -2; x <= 2; x++) {
                    var position = Offset(origin, x, y);
                    if (!InBounds(position) || IsSolid(position) || RegionAt(position) != region)
                        return false;
                }
            return true;
        }

        private bool FitObj3(int region, uint frequency)
        {
            for (var y = 1; y < AuthoritativeCathedralThemeParameters.Width - 1; y++)
                for (var x = 1; x < AuthoritativeCathedralThemeParameters.Width - 1; x++)
                    if (CheckObj3(new AuthoritativeCathedralPopulationPoint(x, y), region, frequency)) {
                        scratch.X = x;
                        scratch.Y = y;
                        return true;
                    }
            return false;
        }

        private bool CheckObj3(AuthoritativeCathedralPopulationPoint origin, int region, uint frequency)
        {
            for (var y = -1; y <= 1; y++)
                for (var x = -1; x <= 1; x++) {
                    var position = Offset(origin, x, y);
                    if (!InBounds(position) || IsSolid(position) || RegionAt(position) != region || HasObject(position))
                        return false;
                    if (frequency > 0 && random.FlipCoin(frequency))
                        return false;
                }
            return true;
        }

        private bool IsSolid(AuthoritativeCathedralPopulationPoint position)
            => InBounds(position) && (input.PieceProperties[input.Pieces[Index(position)]] & SolidFlag) != 0;

        private bool IsNotSolid(AuthoritativeCathedralPopulationPoint position)
            => InBounds(position) && !IsSolid(position);

        private bool HasFlag(AuthoritativeCathedralPopulationPoint position, byte flag)
            => InBounds(position) && (input.PieceProperties[input.Pieces[Index(position)]] & flag) != 0;

        private bool HasObject(AuthoritativeCathedralPopulationPoint position)
            => InBounds(position) && input.ObjectOccupied[Index(position)];

        private int RegionAt(AuthoritativeCathedralPopulationPoint position)
            => InBounds(position) ? transparency[Index(position)] : int.MinValue;

        private static AuthoritativeCathedralPopulationPoint Offset(AuthoritativeCathedralPopulationPoint position, int x, int y)
            => new(position.X + x, position.Y + y);

        private static bool InBounds(AuthoritativeCathedralPopulationPoint position)
            => AuthoritativeCathedralThemeParameters.Contains(position);

        private int Index(AuthoritativeCathedralPopulationPoint position)
            => position.X + position.Y * AuthoritativeCathedralThemeParameters.Width;

        private int Index(int x, int y) => x + y * AuthoritativeCathedralThemeParameters.Width;

        private int RequireNonNegativeRandom(int limit)
        {
            var value = random.GenerateRnd(limit);
            if (value is < 0 or >= 4)
                throw new InvalidDataException("Native Cathedral theme selection produced an invalid random index.");
            return value;
        }

        private int RequireThemeRandom(int limit)
        {
            var value = random.GenerateRnd(limit);
            if (value >= 17)
                throw new InvalidDataException("Native Cathedral theme retry produced an invalid random theme ID.");
            return value;
        }
    }

    private sealed class ThemeFlags
    {
        public bool ArmorAvailable { get; set; } = true;
        public bool WeaponAvailable { get; set; } = true;
        public bool CauldronAvailable { get; set; } = true;
        public bool BloodFountainAvailable { get; set; } = true;
        public bool PurifyingFountainAvailable { get; set; } = true;
        public bool MurkyFountainAvailable { get; set; } = true;
        public bool TearFountainAvailable { get; set; } = true;
        public bool TreasureAvailable { get; set; } = true;

        public AuthoritativeCathedralThemeFlags ToPublic()
            => new(ArmorAvailable, WeaponAvailable, CauldronAvailable, BloodFountainAvailable,
                PurifyingFountainAvailable, MurkyFountainAvailable, TearFountainAvailable, TreasureAvailable);
    }

    private sealed class ThemeScratch
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Variant { get; set; }

    }
}

using System.Collections.Frozen;
using Devilution.Engine;

using Point = Devilution.Diablo.AuthoritativeCathedralPopulationPoint;

namespace Devilution.Diablo;

/** Native place flags needed by Theme_PlaceThemeMonsts. */
public sealed record AuthoritativeCathedralThemeRoomMonsterDefinition(
    AuthoritativeCathedralPopulationMonsterDefinition Definition,
    uint PlaceFlags,
    bool IsGoat);

/** InitThemes flags retained around the selected Cathedral theme sequence. */
public sealed record AuthoritativeCathedralThemeRoomFlags(
    bool ArmorAvailable = true,
    bool WeaponAvailable = true,
    bool TreasureAvailable = true,
    bool BloodFountainAvailable = true,
    bool PurifyingFountainAvailable = true,
    bool MurkyFountainAvailable = true,
    bool TearFountainAvailable = true,
    bool CauldronAvailable = true);

/** External raw tables used by CreateThemeRooms; no renderer or MPQ dependency is implied. */
public sealed class AuthoritativeCathedralThemeRoomCatalog
{
    public AuthoritativeCathedralThemeRoomCatalog(
        IReadOnlyList<AuthoritativeCathedralPopulationObjectDefinition> objects,
        IReadOnlyList<AuthoritativeCathedralThemeRoomMonsterDefinition> orderedBestiary,
        IReadOnlyList<bool> shrineAvailable)
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(orderedBestiary);
        ArgumentNullException.ThrowIfNull(shrineAvailable);
        if (objects.Count != AuthoritativeCathedralThemeRoomGenerator.NativeObjectCatalogCount
            || objects.Where((value, index) => value is null || value.NativeType != index || (value.Flags & ~63) != 0 || value.SelectionRegion is < 0 or > 7).Any())
            throw new InvalidDataException("The complete ordered native object table is required.");
        if (orderedBestiary.Count is < 1 or > 24
            || orderedBestiary.Any(value => value is null || value.Definition is null || value.Definition.Frames is null || value.Definition.Rates is null)
            || orderedBestiary.Any(value => value.Definition.NativeType is < 0 or > 137 || value.Definition.Frames.Count != 6 || value.Definition.Rates.Count != 6
                || value.PlaceFlags > byte.MaxValue)
            || orderedBestiary.Any(value => value.Definition.MinimumHitPoints is < 0 or > ushort.MaxValue
                || value.Definition.MaximumHitPoints < value.Definition.MinimumHitPoints
                || value.Definition.MaximumHitPoints > ushort.MaxValue
                || value.Definition.Ai is < -1 or > 39 || value.Definition.AbilityFlags > ushort.MaxValue
                || new[] { value.Definition.Intelligence, value.Definition.MinDamage, value.Definition.MaxDamage,
                    value.Definition.MinDamageSpecial, value.Definition.MaxDamageSpecial, value.Definition.ArmorClass,
                    value.Definition.Resistance, value.Definition.ReducePlayerStrength, value.Definition.ReducePlayerMagic,
                    value.Definition.ReducePlayerDexterity, value.Definition.ReducePlayerVitality,
                    value.Definition.ReducePlayerMaxHP, value.Definition.ReducePlayerMaxMana }.Any(number => number is < 0 or > 255)
                || value.Definition.Frames.Concat(value.Definition.Rates).Any(number => number is < sbyte.MinValue or > sbyte.MaxValue))
            || orderedBestiary.Any(value => value.Definition.IsSkeleton != AuthoritativeCathedralThemeRoomGenerator.IsNativeSkeleton(value.Definition.NativeType)
                || value.IsGoat != AuthoritativeCathedralThemeRoomGenerator.IsNativeGoat(value.Definition.NativeType))
            || orderedBestiary.Select(value => value.Definition.NativeType).Distinct().Count() != orderedBestiary.Count)
            throw new InvalidDataException("The ordered theme bestiary is invalid.");
        if (shrineAvailable.Count != 26 || !shrineAvailable.Any(value => value))
            throw new InvalidDataException("Original Diablo requires all 26 shrine availability flags.");
        Objects = Array.AsReadOnly(objects.ToArray());
        OrderedBestiary = Array.AsReadOnly(orderedBestiary.Select(value => value with {
            Definition = value.Definition with {
                Frames = Array.AsReadOnly(value.Definition.Frames.ToArray()),
                Rates = Array.AsReadOnly(value.Definition.Rates.ToArray()),
            },
        }).ToArray());
        ShrineAvailable = Array.AsReadOnly(shrineAvailable.ToArray());
    }

    public IReadOnlyList<AuthoritativeCathedralPopulationObjectDefinition> Objects { get; }
    public IReadOnlyList<AuthoritativeCathedralThemeRoomMonsterDefinition> OrderedBestiary { get; }
    public IReadOnlyList<bool> ShrineAvailable { get; }
}

/** Complete detached state consumed and returned by one ordered CreateThemeRooms pass. */
public sealed class AuthoritativeCathedralThemeRoomState
{
    public const int Width = 112;
    public const int CellCount = Width * Width;
    public const int ObjectCapacity = 127;
    public const int MonsterCapacity = 200;
    public const int ItemCapacity = 128;
    public const int ActiveItemCapacity = 127;
    public const int LightCapacity = 32;

    public AuthoritativeCathedralThemeRoomState(
        IReadOnlyList<AuthoritativeCathedralPopulationObjectState> objectSlots,
        IReadOnlyList<int> activeObjectIds,
        IReadOnlyList<int> availableObjectIds,
        int activeObjectCount,
        IReadOnlyList<AuthoritativeCathedralPopulationMonsterState> monsterSlots,
        IReadOnlyList<int> activeMonsterIds,
        int activeMonsterCount,
        int monsterTargetCount,
        IReadOnlyList<int> monsterRefs,
        IReadOnlyList<int> playerRefs,
        IReadOnlyList<AuthoritativeCathedralInitialItemState> itemSlots,
        IReadOnlyList<int> activeItemIds,
        int activeItemCount,
        IReadOnlyList<int> itemRefs,
        IReadOnlySet<int>? uniqueItemFlags,
        IReadOnlyList<ushort> pieces,
        IReadOnlyList<sbyte> transparency,
        IReadOnlyList<byte> special,
        IReadOnlyList<byte> pieceProperties,
        IReadOnlyList<byte> tileFlags,
        IReadOnlyList<int> objectRefs,
        IReadOnlyList<byte> light,
        IReadOnlyList<byte> preLight,
        IReadOnlyList<int> activeLightIds,
        int activeLightCount,
        IReadOnlyList<AuthoritativeCathedralEncounterLightState> lightRecords,
        bool updateLighting,
        bool disableLighting,
        AuthoritativeCathedralThemeRoomFlags? flags = null,
        IReadOnlyList<byte>? corpses = null,
        IReadOnlyList<bool>? monsterPetrified = null)
    {
        ArgumentNullException.ThrowIfNull(objectSlots);
        ArgumentNullException.ThrowIfNull(activeObjectIds);
        ArgumentNullException.ThrowIfNull(availableObjectIds);
        ArgumentNullException.ThrowIfNull(monsterSlots);
        ArgumentNullException.ThrowIfNull(activeMonsterIds);
        ArgumentNullException.ThrowIfNull(itemSlots);
        ArgumentNullException.ThrowIfNull(activeItemIds);
        ArgumentNullException.ThrowIfNull(uniqueItemFlags);
        ArgumentNullException.ThrowIfNull(pieces);
        ArgumentNullException.ThrowIfNull(transparency);
        ArgumentNullException.ThrowIfNull(special);
        ArgumentNullException.ThrowIfNull(pieceProperties);
        ArgumentNullException.ThrowIfNull(tileFlags);
        ArgumentNullException.ThrowIfNull(objectRefs);
        ArgumentNullException.ThrowIfNull(light);
        ArgumentNullException.ThrowIfNull(preLight);
        ArgumentNullException.ThrowIfNull(activeLightIds);
        ArgumentNullException.ThrowIfNull(lightRecords);
        RequireCount(objectSlots, ObjectCapacity, nameof(objectSlots));
        if (objectSlots.Any(value => value is null))
            throw new InvalidDataException("Native object storage contains a null slot.");
        if (objectSlots.Select((value, index) => value.Slot == index).Any(value => !value))
            throw new InvalidDataException("Native object storage slot identities are not ordered.");
        foreach (var value in objectSlots)
            ValidateObjectState(value);
        if (activeObjectCount is < 0 or > ObjectCapacity)
            throw new InvalidDataException("Native active object count is outside 0..127.");
        RequirePrefixPool(activeObjectIds, ObjectCapacity, activeObjectCount, nameof(activeObjectIds));
        RequirePrefixPool(availableObjectIds, ObjectCapacity, ObjectCapacity - activeObjectCount, nameof(availableObjectIds));
        if (activeObjectIds.Take(activeObjectCount).Intersect(
                availableObjectIds.Take(ObjectCapacity - activeObjectCount)).Any())
            throw new InvalidDataException("Native active and available object prefixes overlap.");
        RequireCount(monsterSlots, MonsterCapacity, nameof(monsterSlots));
        if (monsterSlots.Any(value => value is null))
            throw new InvalidDataException("Native monster storage contains a null slot.");
        if (monsterSlots.Select((value, index) => value.Slot == index).Any(value => !value))
            throw new InvalidDataException("Native monster storage slot identities are not ordered.");
        foreach (var value in monsterSlots)
            ValidateMonsterState(value);
        if (activeMonsterCount is < 0 or > MonsterCapacity
            || monsterTargetCount is < 0 or > MonsterCapacity)
            throw new InvalidDataException("Native monster counts are outside 0..200.");
        RequirePermutation(activeMonsterIds, MonsterCapacity, nameof(activeMonsterIds));
        RequireCount(itemSlots, ItemCapacity, nameof(itemSlots));
        if (itemSlots.Any(value => value is null))
            throw new InvalidDataException("Native item storage contains a null slot.");
        foreach (var item in itemSlots)
            AuthoritativeCathedralInitialItemGenerator.ValidateItemProjection(item);
        RequirePermutation(activeItemIds, ActiveItemCapacity, nameof(activeItemIds));
        if (activeItemCount is < 0 or > ActiveItemCapacity)
            throw new InvalidDataException("Native active item count is outside 0..127.");
        RequireCount(monsterRefs, CellCount, nameof(monsterRefs));
        monsterPetrified ??= new bool[MonsterCapacity];
        RequireCount(monsterPetrified, MonsterCapacity, nameof(monsterPetrified));
        RequireCount(playerRefs, CellCount, nameof(playerRefs));
        RequireCount(itemRefs, CellCount, nameof(itemRefs));
        RequireCount(pieces, CellCount, nameof(pieces));
        RequireCount(transparency, CellCount, nameof(transparency));
        RequireCount(special, CellCount, nameof(special));
        // These trailing projections are optional only for old callers; a native
        // capture should always supply both complete channels.
        corpses ??= new byte[CellCount];
        RequireCount(corpses, CellCount, nameof(corpses));
        RequireCount(tileFlags, CellCount, nameof(tileFlags));
        RequireCount(objectRefs, CellCount, nameof(objectRefs));
        if (pieceProperties.Count != AuthoritativeCathedralPopulationGeometry.PieceCount
            || pieces.Any(value => value >= pieceProperties.Count))
            throw new InvalidDataException("Theme pieces require the complete 1379-row SOL property table.");
        RequireCount(light, CellCount, nameof(light));
        RequireCount(preLight, CellCount, nameof(preLight));
        RequirePermutation(activeLightIds, LightCapacity, nameof(activeLightIds));
        if (activeLightCount is < 0 or > LightCapacity || lightRecords.Count != LightCapacity)
            throw new InvalidDataException("Native indexed light state is invalid.");
        if (lightRecords.Select((value, index) => value.LightId == index).Any(value => !value))
            throw new InvalidDataException("Native indexed light identities are not ordered.");
        if (lightRecords.Any(value => !IsNativeWorldPoint(value.Tile) || !IsNativeWorldPoint(value.OldTile)
                || value.OffsetX is < sbyte.MinValue or > sbyte.MaxValue
                || value.OffsetY is < sbyte.MinValue or > sbyte.MaxValue
                || value.Radius is < byte.MinValue or > byte.MaxValue
                || value.OldRadius is < byte.MinValue or > byte.MaxValue))
            throw new InvalidDataException("Native indexed light scalar state exceeds its field width.");
        if (monsterRefs.Any(value => value is < short.MinValue or > short.MaxValue)
            || playerRefs.Any(value => value is < sbyte.MinValue or > sbyte.MaxValue)
            || itemRefs.Any(value => value is < sbyte.MinValue or > sbyte.MaxValue)
            || objectRefs.Any(value => value is < -ObjectCapacity or > ObjectCapacity))
            throw new InvalidDataException("Signed native tile references exceed their safe bounds.");
        if (uniqueItemFlags.Any(value => value is < 0 or >= ItemCapacity))
            throw new InvalidDataException("Unique item history contains an unsafe native ID.");

        ObjectSlots = Array.AsReadOnly(objectSlots.Select(CloneObject).ToArray());
        ActiveObjectIds = Array.AsReadOnly(activeObjectIds.ToArray());
        AvailableObjectIds = Array.AsReadOnly(availableObjectIds.ToArray());
        ActiveObjectCount = activeObjectCount;
        MonsterSlots = Array.AsReadOnly(monsterSlots.Select(CloneMonster).ToArray());
        ActiveMonsterIds = Array.AsReadOnly(activeMonsterIds.ToArray());
        ActiveMonsterCount = activeMonsterCount;
        MonsterTargetCount = monsterTargetCount;
        MonsterRefs = Array.AsReadOnly(monsterRefs.ToArray());
        MonsterPetrified = Array.AsReadOnly(monsterPetrified.ToArray());
        PlayerRefs = Array.AsReadOnly(playerRefs.ToArray());
        ItemSlots = Array.AsReadOnly(itemSlots.Select(CloneItem).ToArray());
        ActiveItemIds = Array.AsReadOnly(activeItemIds.ToArray());
        ActiveItemCount = activeItemCount;
        ItemRefs = Array.AsReadOnly(itemRefs.ToArray());
        UniqueItemFlags = uniqueItemFlags.ToFrozenSet();
        Pieces = Array.AsReadOnly(pieces.ToArray());
        Transparency = Array.AsReadOnly(transparency.ToArray());
        Special = Array.AsReadOnly(special.ToArray());
        Corpses = Array.AsReadOnly(corpses.ToArray());
        PieceProperties = Array.AsReadOnly(pieceProperties.ToArray());
        TileFlags = Array.AsReadOnly(tileFlags.ToArray());
        ObjectRefs = Array.AsReadOnly(objectRefs.ToArray());
        Light = Array.AsReadOnly(light.ToArray());
        PreLight = Array.AsReadOnly(preLight.ToArray());
        ActiveLightIds = Array.AsReadOnly(activeLightIds.ToArray());
        ActiveLightCount = activeLightCount;
        LightRecords = Array.AsReadOnly(lightRecords.Select(value => value with { }).ToArray());
        UpdateLighting = updateLighting;
        DisableLighting = disableLighting;
        Flags = flags ?? new();
    }

    public IReadOnlyList<AuthoritativeCathedralPopulationObjectState> ObjectSlots { get; }
    public IReadOnlyList<int> ActiveObjectIds { get; }
    public IReadOnlyList<int> AvailableObjectIds { get; }
    public int ActiveObjectCount { get; }
    public IReadOnlyList<AuthoritativeCathedralPopulationMonsterState> MonsterSlots { get; }
    public IReadOnlyList<int> ActiveMonsterIds { get; }
    public int ActiveMonsterCount { get; }
    public int MonsterTargetCount { get; }
    public IReadOnlyList<int> MonsterRefs { get; }
    public IReadOnlyList<bool> MonsterPetrified { get; }
    public IReadOnlyList<int> PlayerRefs { get; }
    public IReadOnlyList<AuthoritativeCathedralInitialItemState> ItemSlots { get; }
    public IReadOnlyList<int> ActiveItemIds { get; }
    public int ActiveItemCount { get; }
    public IReadOnlyList<int> ItemRefs { get; }
    public IReadOnlySet<int> UniqueItemFlags { get; }
    public IReadOnlyList<ushort> Pieces { get; }
    public IReadOnlyList<sbyte> Transparency { get; }
    public IReadOnlyList<byte> Special { get; }
    public IReadOnlyList<byte> Corpses { get; }
    public IReadOnlyList<byte> PieceProperties { get; }
    public IReadOnlyList<byte> TileFlags { get; }
    public IReadOnlyList<int> ObjectRefs { get; }
    public IReadOnlyList<byte> Light { get; }
    public IReadOnlyList<byte> PreLight { get; }
    public IReadOnlyList<int> ActiveLightIds { get; }
    public int ActiveLightCount { get; }
    public IReadOnlyList<AuthoritativeCathedralEncounterLightState> LightRecords { get; }
    public bool UpdateLighting { get; }
    public bool DisableLighting { get; }
    public AuthoritativeCathedralThemeRoomFlags Flags { get; }

    private static void RequireCount<T>(IReadOnlyList<T> values, int count, string name)
    {
        if (values.Count != count)
            throw new InvalidDataException($"Native {name} requires exactly {count} entries.");
    }

    private static void ValidateObjectState(AuthoritativeCathedralPopulationObjectState value)
    {
        if (value.Type is < sbyte.MinValue or > sbyte.MaxValue
            || value.BreakFlag is < sbyte.MinValue or > sbyte.MaxValue
            || value.SelectionRegion is < byte.MinValue or > byte.MaxValue
            || value.BookMessage is < short.MinValue or > short.MaxValue)
            throw new InvalidDataException("Native object scalar state exceeds its field width.");
    }

    private static void ValidateMonsterState(AuthoritativeCathedralPopulationMonsterState value)
    {
        if (value.Type is < short.MinValue or > short.MaxValue
            || value.LevelType is < byte.MinValue or > byte.MaxValue
            || value.Mode is < byte.MinValue or > byte.MaxValue
            || value.Goal is < byte.MinValue or > byte.MaxValue
            || value.GoalVar1 is < short.MinValue or > short.MaxValue
            || value.GoalVar2 is < sbyte.MinValue or > sbyte.MaxValue
            || value.GoalVar3 is < sbyte.MinValue or > sbyte.MaxValue
            || value.Var1 is < short.MinValue or > short.MaxValue
            || value.Var2 is < short.MinValue or > short.MaxValue
            || value.Var3 is < sbyte.MinValue or > sbyte.MaxValue
            || value.PathCount is < byte.MinValue or > byte.MaxValue
            || value.Direction is < 0 or > 8
            || value.Enemy is < byte.MinValue or > byte.MaxValue
            || value.Ai is < sbyte.MinValue or > sbyte.MaxValue
            || value.Intelligence is < byte.MinValue or > byte.MaxValue
            || value.ActiveForTicks is < byte.MinValue or > byte.MaxValue
            || value.UniqueType is < byte.MinValue or > byte.MaxValue
            || value.UniqueTranslation is < byte.MinValue or > byte.MaxValue
            || value.CorpseId is < sbyte.MinValue or > sbyte.MaxValue
            || value.WhoHit is < sbyte.MinValue or > sbyte.MaxValue
            || value.TalkMessage is < short.MinValue or > short.MaxValue
            || new[] { value.MinDamage, value.MaxDamage, value.MinDamageSpecial, value.MaxDamageSpecial,
                value.ArmorClass, value.ReducePlayerStrength, value.ReducePlayerMagic, value.ReducePlayerDexterity,
                value.ReducePlayerVitality, value.ReducePlayerMaxHP, value.ReducePlayerMaxMana, value.Leader,
                value.LeaderRelation, value.PackSize }.Any(number => number is < byte.MinValue or > byte.MaxValue)
            || value.GolemToHit is < ushort.MinValue or > ushort.MaxValue
            || value.Resistance is < ushort.MinValue or > ushort.MaxValue
            || new[] { value.AnimTicksPerFrame, value.AnimTickCounter, value.AnimNumberOfFrames, value.AnimCurrentFrame }
                .Any(number => number is < sbyte.MinValue or > sbyte.MaxValue)
            || new[] { value.Tile, value.Future, value.Old, value.Last, value.Temp, value.EnemyPosition }
                .Any(position => !IsNativeWorldPoint(position)))
            throw new InvalidDataException("Native monster scalar state exceeds its field width.");
    }

    private static bool IsNativeWorldPoint(Point position)
        => position.X is >= byte.MinValue and <= byte.MaxValue && position.Y is >= byte.MinValue and <= byte.MaxValue;

    private static void RequirePermutation(IReadOnlyList<int> values, int count, string name)
    {
        RequireCount(values, count, name);
        if (values.Any(value => value < 0 || value >= count) || values.Distinct().Count() != count)
            throw new InvalidDataException($"Native {name} must be a permutation of 0..{count - 1}.");
    }

    private static void RequirePrefixPool(IReadOnlyList<int> values, int capacity, int usedCount, string name)
    {
        RequireCount(values, capacity, name);
        if (values.Any(value => value < 0 || value >= capacity)
            || values.Take(usedCount).Distinct().Count() != usedCount)
            throw new InvalidDataException($"Native {name} has an unsafe active prefix.");
    }

    private static AuthoritativeCathedralPopulationObjectState CloneObject(AuthoritativeCathedralPopulationObjectState value)
        => value with { };

    private static AuthoritativeCathedralPopulationMonsterState CloneMonster(AuthoritativeCathedralPopulationMonsterState value)
        => value with { };

    private static AuthoritativeCathedralInitialItemState CloneItem(AuthoritativeCathedralInitialItemState value)
        => value with { Animation = value.Animation with { } };
}

/** Ordered CreateThemeRooms input; themes are consumed in the supplied order. */
public sealed record AuthoritativeCathedralThemeRoomRequest(
    AuthoritativeCathedralThemeRoomState State,
    AuthoritativeCathedralThemeRoomCatalog Catalog,
    AuthoritativeCathedralThemeItemCatalog ItemCatalog,
    AuthoritativeCathedralThemeItemPolicy ItemPolicy,
    IReadOnlyList<AuthoritativeCathedralThemeEntry> Themes,
    uint RandomState,
    AuthoritativeCathedralPopulationPlayer TargetPlayer,
    AuthoritativeCathedralThemeItemPlayerContext? ItemPlayer = null,
    bool LoadingMapObjects = false,
    int InitialThemeVar1 = 0,
    int MaximumPlacementAttempts = 1_000_000,
    AuthoritativeCathedralPopulationPoint InitialThemeOrigin = default,
    IReadOnlyList<AuthoritativeCathedralPopulationPlayer>? PlayerContexts = null);

/** Detached CreateThemeRooms output. */
public sealed class AuthoritativeCathedralThemeRoomResult
{
    internal AuthoritativeCathedralThemeRoomResult(
        AuthoritativeCathedralThemeRoomState state,
        uint finalRandomState,
        IReadOnlyList<AuthoritativeCathedralThemeEntry> themes,
        IReadOnlyList<AuthoritativeCathedralPopulationPoint> themeOrigins,
        IReadOnlyList<int> themeVars,
        IReadOnlyList<int> itemNoFlippySlots,
        int finalThemeVar1,
        AuthoritativeCathedralPopulationPoint finalThemeOrigin)
    {
        State = state;
        FinalRandomState = finalRandomState;
        Themes = Array.AsReadOnly(themes.ToArray());
        ThemeOrigins = Array.AsReadOnly(themeOrigins.ToArray());
        ThemeVars = Array.AsReadOnly(themeVars.ToArray());
        ItemNoFlippySlots = Array.AsReadOnly(itemNoFlippySlots.ToArray());
        FinalThemeVar1 = finalThemeVar1;
        FinalThemeOrigin = finalThemeOrigin;
    }

    public AuthoritativeCathedralThemeRoomState State { get; }
    public uint FinalRandomState { get; }
    public IReadOnlyList<AuthoritativeCathedralThemeEntry> Themes { get; }
    public IReadOnlyList<AuthoritativeCathedralPopulationPoint> ThemeOrigins { get; }
    public IReadOnlyList<int> ThemeVars { get; }
    public IReadOnlyList<int> ItemNoFlippySlots { get; }
    public int FinalThemeVar1 { get; }
    public AuthoritativeCathedralPopulationPoint FinalThemeOrigin { get; }
}

/** Complete detached ordinary original-Diablo Cathedral CreateThemeRooms port. */
public static class AuthoritativeCathedralThemeRoomGenerator
{
    public const int Width = AuthoritativeCathedralThemeRoomState.Width;
    public const int CellCount = AuthoritativeCathedralThemeRoomState.CellCount;
    public const int ObjectCapacity = AuthoritativeCathedralThemeRoomState.ObjectCapacity;
    public const int NativeObjectCatalogCount = 109;
    public const int MonsterCapacity = AuthoritativeCathedralThemeRoomState.MonsterCapacity;
    public const int ItemCapacity = AuthoritativeCathedralThemeRoomState.ItemCapacity;
    private const int MaxThemes = 50;

    private const int Barrel = 0;
    private const int Shrine = 1;
    private const int MonsterPit = 2;
    private const int SkeletonRoom = 3;
    private const int Treasure = 4;
    private const int Library = 5;
    private const int Torture = 6;
    private const int BloodFountain = 7;
    private const int Decapitated = 8;
    private const int PurifyingFountain = 9;
    private const int GoatShrine = 11;
    private const int MurkyFountain = 13;
    private const int TearFountain = 14;
    private const int BurningCross = 15;
    private const int ObjSkFire = 3;
    private const int ObjCandle2 = 9;
    private const int ObjBannerL = 11;
    private const int ObjBannerM = 12;
    private const int ObjBannerR = 13;
    private const int ObjTorturedBody = 30;
    private const int ObjBarrel = 57;
    private const int ObjBarrelExplosive = 58;
    private const int ObjShrineL = 59;
    private const int ObjShrineR = 60;
    private const int ObjSkeletonBook = 61;
    private const int ObjBookcaseL = 62;
    private const int ObjBookcaseR = 63;
    private const int ObjBookstand = 64;
    private const int ObjBookCandle = 65;
    private const int ObjBloodFountain = 66;
    private const int ObjDecapitated = 67;
    private const int ObjPurifyingFountain = 76;
    private const int ObjGoatShrine = 79;
    private const int ObjMurkyFountain = 81;
    private const int ObjTearFountain = 82;
    private const int ObjBurningCross = 91;
    private const int ItemTypeGold = 11;
    private const int MiscNone = -1;
    private const uint Animated = 1;
    private const uint Solid = 2;
    private const uint MissilePassable = 4;
    private const uint ApplyLighting = 8;
    private const uint Breakable = 32;
    private const byte TilePopulated = 1 << 3;
    private const byte TrapProperty = 128;
    private const byte SolidProperty = 1;
    private const int NoLight = -1;
    private const uint NoEnemy = 1U << 10;
    private const uint TargetsMonster = 1U << 4;
    private const uint Golem = 1U << 5;
    private const uint Berserk = 1U << 11;
    private const uint AnimationLock = 1U << 1;
    private const uint AllowSpecial = 1U << 2;

    internal static bool IsNativeSkeleton(int nativeType)
        => nativeType is >= 8 and <= 11 or >= 20 and <= 27;

    internal static bool IsNativeGoat(int nativeType)
        => nativeType is >= 34 and <= 37 or >= 42 and <= 45;

    public static AuthoritativeCathedralThemeRoomResult Generate(AuthoritativeCathedralThemeRoomRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var engine = new Engine(request);
        return engine.Generate();
    }

    private sealed class Engine
    {
        private readonly AuthoritativeCathedralThemeRoomRequest input;
        private readonly IReadOnlyList<AuthoritativeCathedralPopulationPlayer> players;
        private readonly DeterministicLegacyRandom random;
        private AuthoritativeCathedralPopulationObjectState[] objects;
        private int[] activeObjects;
        private int[] availableObjects;
        private int activeObjectCount;
        private AuthoritativeCathedralPopulationMonsterState[] monsters;
        private int[] activeMonsters;
        private int activeMonsterCount;
        private readonly int monsterTargetCount;
        private int[] monsterRefs;
        private bool[] monsterPetrified;
        private int[] playerRefs;
        private AuthoritativeCathedralInitialItemState[] items;
        private int[] activeItems;
        private int activeItemCount;
        private int[] itemRefs;
        private HashSet<int> uniqueFlags;
        private ushort[] pieces;
        private sbyte[] transparency;
        private byte[] special;
        private byte[] corpses;
        private readonly byte[] pieceProperties;
        private byte[] tileFlags;
        private int[] objectRefs;
        private byte[] light;
        private byte[] preLight;
        private readonly int[] activeLights;
        private readonly int activeLightCount;
        private readonly AuthoritativeCathedralEncounterLightState[] lightRecords;
        private bool updateLighting;
        private readonly bool disableLighting;
        private AuthoritativeCathedralThemeRoomFlags flags;
        private int themex;
        private int themey;
        private int themeVar1;
        private sbyte currentRegion;
        private int itemNoFlippySlot = -1;
        private readonly List<AuthoritativeCathedralPopulationPoint> origins = [];
        private readonly List<int> vars = [];
        private readonly List<int> noFlippy = [];

        public Engine(AuthoritativeCathedralThemeRoomRequest input)
        {
            this.input = input;
            random = new(input.RandomState);
            ValidateRequest(input);
            players = (input.PlayerContexts ?? new[] { input.TargetPlayer }).OrderBy(player => player.Id).ToArray();
            var state = input.State;
            objects = state.ObjectSlots.Select(value => value with { }).ToArray();
            activeObjects = state.ActiveObjectIds.ToArray();
            availableObjects = state.AvailableObjectIds.ToArray();
            activeObjectCount = state.ActiveObjectCount;
            monsters = state.MonsterSlots.Select(value => value with { }).ToArray();
            activeMonsters = state.ActiveMonsterIds.ToArray();
            activeMonsterCount = state.ActiveMonsterCount;
            monsterTargetCount = state.MonsterTargetCount;
            monsterRefs = state.MonsterRefs.ToArray();
            monsterPetrified = state.MonsterPetrified.ToArray();
            playerRefs = state.PlayerRefs.ToArray();
            items = state.ItemSlots.Select(value => value with { Animation = value.Animation with { } }).ToArray();
            activeItems = state.ActiveItemIds.ToArray();
            activeItemCount = state.ActiveItemCount;
            itemRefs = state.ItemRefs.ToArray();
            uniqueFlags = state.UniqueItemFlags.ToHashSet();
            pieces = state.Pieces.ToArray();
            transparency = state.Transparency.ToArray();
            special = state.Special.ToArray();
            corpses = state.Corpses.ToArray();
            pieceProperties = state.PieceProperties.ToArray();
            tileFlags = state.TileFlags.ToArray();
            objectRefs = state.ObjectRefs.ToArray();
            light = state.Light.ToArray();
            preLight = state.PreLight.ToArray();
            activeLights = state.ActiveLightIds.ToArray();
            activeLightCount = state.ActiveLightCount;
            lightRecords = state.LightRecords.Select(value => value with { }).ToArray();
            updateLighting = state.UpdateLighting;
            disableLighting = state.DisableLighting;
            flags = state.Flags;
            themeVar1 = input.InitialThemeVar1;
            themex = input.InitialThemeOrigin.X;
            themey = input.InitialThemeOrigin.Y;
        }

        public AuthoritativeCathedralThemeRoomResult Generate()
        {
            foreach (var theme in input.Themes) {
                themex = 0;
                themey = 0;
                itemNoFlippySlot = -1;
                GenerateTheme(theme);
                origins.Add(new(themex, themey));
                vars.Add(themeVar1);
                noFlippy.Add(itemNoFlippySlot);
            }
            var state = new AuthoritativeCathedralThemeRoomState(
                objects, activeObjects, availableObjects, activeObjectCount,
                monsters, activeMonsters, activeMonsterCount, monsterTargetCount, monsterRefs, playerRefs,
                items, activeItems, activeItemCount, itemRefs, uniqueFlags,
                pieces, transparency, special, pieceProperties, tileFlags, objectRefs,
                light, preLight, activeLights, activeLightCount, lightRecords,
                updateLighting, disableLighting, flags, corpses, monsterPetrified);
            return new(state, random.State, input.Themes, origins, vars, noFlippy, themeVar1, new(themex, themey));
        }

        private void GenerateTheme(AuthoritativeCathedralThemeEntry entry)
        {
            currentRegion = checked((sbyte)entry.RegionId);
            switch (entry.ThemeType) {
            case Barrel: ThemeBarrel((sbyte)entry.RegionId); break;
            case Shrine: ThemeShrine((sbyte)entry.RegionId); break;
            case MonsterPit: ThemeMonsterPit((sbyte)entry.RegionId); break;
            case SkeletonRoom: ThemeSkeletonRoom((sbyte)entry.RegionId); break;
            case Treasure: ThemeTreasure((sbyte)entry.RegionId); break;
            case Library: ThemeLibrary((sbyte)entry.RegionId); break;
            case Torture: ThemeTorture((sbyte)entry.RegionId); break;
            case BloodFountain: ThemeBloodFountain((sbyte)entry.RegionId); break;
            case Decapitated: ThemeDecapitated((sbyte)entry.RegionId); break;
            case PurifyingFountain: ThemePurifyingFountain((sbyte)entry.RegionId); break;
            case GoatShrine: ThemeGoatShrine((sbyte)entry.RegionId); break;
            case MurkyFountain: ThemeMurkyFountain((sbyte)entry.RegionId); break;
            case TearFountain: ThemeTearFountain((sbyte)entry.RegionId); break;
            case BurningCross: ThemeBurningCross((sbyte)entry.RegionId); break;
            default: throw new InvalidDataException("Theme is not an ordinary Cathedral theme.");
            }
        }

        private void ThemeBarrel(sbyte region)
        {
            var chance = BarrelFrequency();
            for (var y = 0; y < Width; y++) for (var x = 0; x < Width; x++) {
                if (IsRegion(x, y, region) && NotSolid(x, y) && random.FlipCoin((uint)chance))
                    AddObject(random.FlipCoin((uint)chance) ? ObjBarrel : ObjBarrelExplosive, new(x, y));
            }
            PlaceThemeMonsters(region, MonsterFrequency(0));
        }

        private void ThemeShrine(sbyte region)
        {
            FitShrine(region);
            if (themeVar1 == 1) {
                AddObject(ObjCandle2, new(themex - 1, themey));
                AddObject(ObjShrineR, new(themex, themey));
                AddObject(ObjCandle2, new(themex + 1, themey));
            } else {
                AddObject(ObjCandle2, new(themex, themey - 1));
                AddObject(ObjShrineL, new(themex, themey));
                AddObject(ObjCandle2, new(themex, themey + 1));
            }
            PlaceThemeMonsters(region, MonsterFrequency(1));
        }

        private void ThemeMonsterPit(sbyte region)
        {
            var remaining = random.GenerateRnd(100) + 1;
            var ixp = 0;
            var iyp = 0;
            var attempts = 0;
            while (remaining > 0 && attempts++ < input.MaximumPlacementAttempts) {
                if (IsRegion(ixp, iyp, region) && NotSolid(ixp, iyp)) remaining--;
                if (remaining <= 0) break;
                if (++ixp == Width) { ixp = 0; if (++iyp == Width) iyp = 0; }
            }
            if (remaining > 0)
                throw new InvalidDataException("Theme monster-pit placement exceeded its bounded search.");
            CreateRandomItem(new(ixp, iyp), true);
            ItemNoFlippy();
            PlaceThemeMonsters(region, MonsterFrequency(2));
        }

        private void ThemeSkeletonRoom(sbyte region)
        {
            if (TryFindSkeleton(out var skeletonType)) {
                themeVar1 = skeletonType;
                FitObj5(region);
            }
            var x = themex; var y = themey;
            AddObject(ObjSkFire, new(x, y));
            SpawnObjectOrSkeleton(6, ObjBannerL, new(x - 1, y - 1));
            ActivatePreSpawnSkeleton(new(x, y - 1));
            SpawnObjectOrSkeleton(6, ObjBannerR, new(x + 1, y - 1));
            SpawnObjectOrSkeleton(6, ObjBannerM, new(x - 1, y));
            SpawnObjectOrSkeleton(6, ObjBannerM, new(x + 1, y));
            SpawnObjectOrSkeleton(6, ObjBannerR, new(x - 1, y + 1));
            ActivatePreSpawnSkeleton(new(x, y + 1));
            SpawnObjectOrSkeleton(6, ObjBannerL, new(x + 1, y + 1));
            if (!IsObjectAt(x, y - 3)) AddObject(ObjSkeletonBook, new(x, y - 2));
            if (!IsObjectAt(x, y + 3)) AddObject(ObjSkeletonBook, new(x, y + 2));
        }

        private void ThemeTreasure(sbyte region)
        {
            random.Discard(1);
            var chance = TreasureFrequency();
            for (var y = 0; y < Width; y++) for (var x = 0; x < Width; x++) {
                if (!IsRegion(x, y, region) || !NotSolid(x, y)) continue;
                var rv = random.GenerateRnd(chance);
                if (random.FlipCoin((uint)chance)) {
                    CreateTypeItem(new(x, y), ItemTypeGold, MiscNone, false);
                    ItemNoFlippy();
                }
                if (rv == 0) {
                    CreateRandomItem(new(x, y), false);
                    ItemNoFlippy();
                }
            }
            PlaceThemeMonsters(region, MonsterFrequency(4));
        }

        private void ThemeLibrary(sbyte region)
        {
            FitShrine(region);
            if (themeVar1 == 1) {
                AddObject(ObjBookCandle, new(themex - 1, themey)); AddObject(ObjBookcaseR, new(themex, themey)); AddObject(ObjBookCandle, new(themex + 1, themey));
            } else {
                AddObject(ObjBookCandle, new(themex, themey - 1)); AddObject(ObjBookcaseL, new(themex, themey)); AddObject(ObjBookCandle, new(themex, themey + 1));
            }
            var chance = LibraryFrequency();
            for (var y = 1; y < Width - 1; y++) for (var x = 1; x < Width - 1; x++) {
                if (!CheckObj3(new(x, y), region, 0) || MonsterAt(x, y) || !random.FlipCoin((uint)chance)) continue;
                var slot = AddObject(ObjBookstand, new(x, y));
                if (!random.FlipCoin(2) && slot >= 0)
                    objects[slot] = objects[slot] with { SelectionRegion = 0, AnimFrame = objects[slot].AnimFrame + 2 };
            }
            PlaceThemeMonsters(region, MonsterFrequency(5));
        }

        private void ThemeTorture(sbyte region)
        {
            var chance = TortureFrequency();
            for (var y = 1; y < Width - 1; y++) for (var x = 1; x < Width - 1; x++)
                if (IsRegion(x, y, region) && NotSolid(x, y) && CheckObj3(new(x, y), region, 0) && random.FlipCoin((uint)chance))
                    AddObject(ObjTorturedBody, new(x, y));
            PlaceThemeMonsters(region, MonsterFrequency(6));
        }

        private void ThemeBloodFountain(sbyte region) { FitObj5(region); AddObject(ObjBloodFountain, new(themex, themey)); PlaceThemeMonsters(region, MonsterFrequency(7)); }
        private void ThemePurifyingFountain(sbyte region) { FitObj5(region); AddObject(ObjPurifyingFountain, new(themex, themey)); PlaceThemeMonsters(region, MonsterFrequency(9)); }
        private void ThemeMurkyFountain(sbyte region) { FitObj5(region); AddObject(ObjMurkyFountain, new(themex, themey)); PlaceThemeMonsters(region, MonsterFrequency(13)); }
        private void ThemeTearFountain(sbyte region) { FitObj5(region); AddObject(ObjTearFountain, new(themex, themey)); PlaceThemeMonsters(region, MonsterFrequency(14)); }

        private void ThemeDecapitated(sbyte region)
        {
            var chance = TortureFrequency();
            for (var y = 1; y < Width - 1; y++) for (var x = 1; x < Width - 1; x++)
                if (IsRegion(x, y, region) && NotSolid(x, y) && CheckObj3(new(x, y), region, 0) && random.FlipCoin((uint)chance))
                    AddObject(ObjDecapitated, new(x, y));
            PlaceThemeMonsters(region, MonsterFrequency(8));
        }

        private void ThemeGoatShrine(sbyte region)
        {
            if (TryFindGoat(out var goatType)) {
                themeVar1 = goatType;
                FitObj5(region);
            }
            AddObject(ObjGoatShrine, new(themex, themey));
            for (var y = themey - 1; y <= themey + 1; y++) for (var x = themex - 1; x <= themex + 1; x++)
            {
                if (x == themex && y == themey) continue;
                RequireNativeTile(x, y, "goat shrine region");
                if (IsRegion(x, y, region) && NotSolid(x, y)) AddMonster(themeVar1, new(x, y), 1, true);
            }
        }

        private void ThemeBurningCross(sbyte region)
        {
            var chance = BurningCrossFrequency();
            for (var y = 0; y < Width; y++) for (var x = 0; x < Width; x++)
                if (IsRegion(x, y, region) && NotSolid(x, y) && CheckObj3(new(x, y), region, 0) && random.FlipCoin((uint)chance))
                    AddObject(ObjBurningCross, new(x, y));
            PlaceThemeMonsters(region, MonsterFrequency(15));
        }

        private void PlaceThemeMonsters(sbyte region, int frequency)
        {
            var scatter = input.Catalog.OrderedBestiary.Select((value, index) => (value, index))
                .Where(value => (value.value.PlaceFlags & 1) != 0).Select(value => value.index).ToArray();
            if (scatter.Length == 0) throw new InvalidDataException("Theme requires a scatter bestiary entry.");
            var typeIndex = random.GenerateRnd(scatter.Length);
            if (typeIndex < 0 || typeIndex >= scatter.Length) throw new InvalidDataException("Theme scatter RNG was unsafe.");
            var levelType = scatter[typeIndex];
            for (var y = 0; y < Width; y++) for (var x = 0; x < Width; x++) {
                if (!IsRegion(x, y, region) || SolidAt(x, y) || ItemAt(x, y) != 0 || IsObjectAt(x, y)) continue;
                if (random.FlipCoin((uint)frequency)) AddMonster(levelType, new(x, y), unchecked((byte)random.GenerateRnd(8)), true);
            }
        }

        private void SpawnObjectOrSkeleton(int frequency, int objectType, Point tile)
        {
            if (random.FlipCoin((uint)frequency)) AddObject(objectType, tile);
            else ActivatePreSpawnSkeleton(tile);
        }

        private void ActivatePreSpawnSkeleton(Point position)
        {
            var slot = PreSpawnSkeleton();
            if (slot < 0) return;
            if (IsTileAvailable(position)) {
                ActivateSpawn(slot, position, 1);
                return;
            }
            var dirs = new[] { 4, 5, 6, 3, 7, 2, 1, 0 };
            var available = dirs.Where(dir => IsTileAvailable(new(position.X + Dx(dir), position.Y + Dy(dir)))).ToArray();
            if (available.Length == 0) return;
            var choice = random.GenerateRnd(15);
            var selected = (int)(unchecked((ulong)(long)choice) % (ulong)available.Length);
            var dir = available[selected];
            ActivateSpawn(slot, new(position.X + Dx(dir), position.Y + Dy(dir)), Opposite(dir));
        }

        private int PreSpawnSkeleton()
        {
            var candidates = input.Catalog.OrderedBestiary.Select((value, index) => (value, index))
                .Where(value => value.value.Definition.IsSkeleton).Select(value => value.index).ToArray();
            if (candidates.Length == 0) return -1;
            var selected = random.GenerateRnd(candidates.Length);
            if (selected < 0 || selected >= candidates.Length) throw new InvalidDataException("Skeleton selection RNG was unsafe.");
            var slot = AddMonster(candidates[selected], new(0, 0), 0, false);
            if (slot >= 0) monsters[slot] = StartStand(monsters[slot], 0);
            return slot;
        }

        private int AddMonster(int levelType, Point position, int direction, bool inMap)
        {
            if (activeMonsterCount >= MonsterCapacity) return -1;
            if (levelType < 0 || levelType >= input.Catalog.OrderedBestiary.Count)
                throw new InvalidDataException("Theme monster selection reached an invalid native bestiary index.");
            var data = input.Catalog.OrderedBestiary[levelType].Definition;
            var animationRate = data.Rates[0];
            var tick = random.GenerateRnd(animationRate - 1);
            var frame = random.GenerateRnd(data.Frames[0] - 1);
            var rawHp = data.MinimumHitPoints + random.RandomIntLessThan(data.MaximumHitPoints - data.MinimumHitPoints + 1);
            var hp = Math.Max((rawHp << 6) / 2, 64);
            var slot = activeMonsters[activeMonsterCount++];
            var monster = monsters[slot] with {
                Slot = slot, Type = data.NativeType, LevelType = levelType, Mode = 0, Goal = 1,
                GoalVar1 = 0, GoalVar2 = 0, GoalVar3 = 0, PathCount = 0, Enemy = 0,
                ActiveForTicks = 0, WhoHit = 0, Tile = position, Future = position, Old = position,
                Direction = direction, Invalid = false, Ai = data.Ai, Intelligence = data.Intelligence,
                UniqueType = 255, Leader = 255, LeaderRelation = 0, TalkMessage = -1, LightId = NoLight,
                HitPoints = hp, MaxHitPoints = hp, AnimTicksPerFrame = animationRate, AnimTickCounter = unchecked((sbyte)tick),
                AnimNumberOfFrames = data.Frames[0], AnimCurrentFrame = unchecked((sbyte)frame), RndItemSeed = random.AdvanceSeed(),
                AiSeed = random.AdvanceSeed(), MinDamage = data.MinDamage, MaxDamage = data.MaxDamage,
                MinDamageSpecial = data.MinDamageSpecial, MaxDamageSpecial = data.MaxDamageSpecial,
                ArmorClass = data.ArmorClass, Resistance = data.Resistance, Flags = data.AbilityFlags,
                ReducePlayerStrength = data.ReducePlayerStrength, ReducePlayerMagic = data.ReducePlayerMagic,
                ReducePlayerDexterity = data.ReducePlayerDexterity, ReducePlayerVitality = data.ReducePlayerVitality,
                ReducePlayerMaxHP = data.ReducePlayerMaxHP, ReducePlayerMaxMana = data.ReducePlayerMaxMana,
            };
            if (monster.Ai == 12) monster = monster with { AnimTicksPerFrame = data.Rates[5], AnimNumberOfFrames = data.Frames[5], AnimCurrentFrame = 0, Flags = monster.Flags | AllowSpecial, Mode = 7 };
            monsters[slot] = monster;
            monsterPetrified[slot] = false;
            if (inMap) monsterRefs[Index(position)] = slot + 1;
            return slot;
        }

        private AuthoritativeCathedralPopulationMonsterState StartStand(AuthoritativeCathedralPopulationMonsterState monster, int direction)
        {
            var data = input.Catalog.OrderedBestiary[monster.LevelType].Definition;
            monsterPetrified[monster.Slot] = false;
            var updated = monster with {
                Direction = direction, Mode = 0,
                Var1 = monster.Mode, Var2 = 0, Var3 = 0, Temp = new(0, 0),
                Future = monster.Tile, Old = monster.Tile,
                AnimTicksPerFrame = Math.Max(data.Rates[0], 1), AnimTickCounter = 0,
                AnimNumberOfFrames = data.Frames[0], AnimCurrentFrame = 0,
                Flags = monster.Flags & ~(AnimationLock | AllowSpecial),
            };
            return UpdateEnemy(updated);
        }

        private void ActivateSpawn(int slot, Point position, int direction)
        {
            if (!Contains(position)) return;
            monsterRefs[Index(position)] = slot + 1;
            var data = input.Catalog.OrderedBestiary[monsters[slot].LevelType].Definition;
            monsterPetrified[slot] = false;
            monsters[slot] = monsters[slot] with {
                Tile = position, Future = position, Old = position, Direction = direction,
                Mode = 11, Flags = monsters[slot].Flags & ~(AnimationLock | AllowSpecial), AnimTicksPerFrame = Math.Max(data.Rates[5], 1),
                AnimTickCounter = 0, AnimNumberOfFrames = data.Frames[5], AnimCurrentFrame = 0,
            };
        }

        private AuthoritativeCathedralPopulationMonsterState UpdateEnemy(AuthoritativeCathedralPopulationMonsterState monster)
        {
            var enemy = -1;
            var enemyPosition = default(Point);
            var bestDistance = int.MaxValue;
            var bestSameRegion = false;
            foreach (var player in players) {
                if (!player.Active || player.LevelChanging || player.Level != input.ItemPolicy.Depth)
                    continue;
                var sameRegion = SameRegion(monster.Tile, player.Tile);
                var distance = WalkingDistance(monster.Tile, player.Tile);
                if (enemy < 0 || (sameRegion && !bestSameRegion)
                    || ((sameRegion || !bestSameRegion) && distance < bestDistance)) {
                    enemy = player.Id;
                    enemyPosition = player.Future;
                    bestDistance = distance;
                    bestSameRegion = sameRegion;
                }
            }

            var isPlayerMinion = (monster.Flags & Golem) != 0 && (monster.Flags & Berserk) == 0;
            if (isPlayerMinion) {
                enemy = -1;
                enemyPosition = default;
                bestDistance = int.MaxValue;
                bestSameRegion = false;
            }
            var targetsMonster = (monster.Flags & Golem) != 0 || (monster.Flags & Berserk) != 0;
            var isRanged = monster.Ai is 3 or 7 or 14 or 29;
            var selectedMonster = -1;
            foreach (var slot in activeMonsters.Take(activeMonsterCount)) {
                if (slot == monster.Slot)
                    continue;
                var other = monsters[slot];
                if ((other.HitPoints >> 6) <= 0 || IsTalker(other) || IsHoldingCell(other.Tile))
                    continue;
                var otherIsGolem = (other.Flags & Golem) != 0;
                var otherIsPlayerMinion = otherIsGolem && (other.Flags & Berserk) == 0;
                if (isPlayerMinion && otherIsPlayerMinion)
                    continue;
                var distance = WalkingDistance(monster.Tile, other.Tile);
                if ((!targetsMonster && distance >= 2 && !isRanged)
                    || (!targetsMonster && !otherIsGolem))
                    continue;
                var sameRegion = SameRegion(monster.Tile, other.Tile);
                if (enemy < 0 || (sameRegion && !bestSameRegion)
                    || ((sameRegion || !bestSameRegion) && distance < bestDistance)) {
                    selectedMonster = slot;
                    enemy = slot;
                    enemyPosition = other.Future;
                    bestDistance = distance;
                    bestSameRegion = sameRegion;
                }
            }
            if (selectedMonster >= 0)
                return monster with { Enemy = enemy, EnemyPosition = enemyPosition, Flags = (monster.Flags | TargetsMonster) & ~(NoEnemy | AnimationLock | AllowSpecial) };
            if (enemy >= 0)
                return monster with { Enemy = enemy, EnemyPosition = enemyPosition, Flags = (monster.Flags & ~TargetsMonster) & ~(NoEnemy | AnimationLock | AllowSpecial) };
            return monster with { Flags = monster.Flags | NoEnemy };
        }

        private static bool IsHoldingCell(Point position) => position.X == 1 && position.Y == 0;

        private static bool IsTalker(AuthoritativeCathedralPopulationMonsterState monster)
            => monster.TalkMessage != -1 && monster.Ai is 18 or 22 or 23 or 28 or 29 or 30 or 31;

        private void CreateRandomItem(Point position, bool onlyGood)
        {
            var result = AuthoritativeCathedralThemeItemWorldGenerator.CreateRandomItem(new(
                ToItemWorldState(), input.ItemCatalog, input.ItemPolicy, random.State, position.X, position.Y, onlyGood, true, -1, -1, input.ItemPlayer));
            ApplyItemWorld(result);
        }

        private void CreateTypeItem(Point position, int type, int misc, bool onlyGood)
        {
            var result = AuthoritativeCathedralThemeItemWorldGenerator.CreateTypeItem(new(
                ToItemWorldState(), input.ItemCatalog, input.ItemPolicy, random.State, position.X, position.Y, onlyGood, true, type, misc, input.ItemPlayer));
            ApplyItemWorld(result);
        }

        private void ApplyItemWorld(AuthoritativeCathedralThemeItemWorldResult result)
        {
            random.Restore(result.FinalRandomState);
            items = result.Items.Select(value => value with { Animation = value.Animation with { } }).ToArray();
            activeItems = result.ActiveItemIds.ToArray();
            activeItemCount = result.ActiveItemCount;
            itemRefs = result.ItemRefs.ToArray();
            uniqueFlags = result.UniqueItemFlags.ToHashSet();
        }

        private AuthoritativeCathedralThemeItemWorldState ToItemWorldState()
        {
            var solid = Enumerable.Range(0, CellCount).Select(index => (pieceProperties[pieces[index]] & SolidProperty) != 0).ToArray();
            var objectSolid = objects.Select(value => value.SolidFlag).ToArray();
            var interaction = objects.Select(value => value.SelectionRegion != 0).ToArray();
            return new(items, activeItems, activeItemCount, itemRefs, playerRefs, monsterRefs, objectRefs,
                solid, tileFlags.Select(value => (value & TilePopulated) != 0).ToArray(), objectSolid, interaction, uniqueFlags);
        }

        private int AddObject(int type, Point position)
        {
            if (activeObjectCount >= ObjectCapacity) return -1;
            if (!Contains(position))
                throw new InvalidDataException("Theme object placement reached an unsafe native tile coordinate.");
            var slot = availableObjects[0];
            availableObjects[0] = availableObjects[ObjectCapacity - 1 - activeObjectCount];
            activeObjects[activeObjectCount] = slot;
            objectRefs[Index(position)] = slot + 1;
            var data = input.Catalog.Objects[type];
            var animated = (data.Flags & Animated) != 0;
            var obj = objects[slot] with {
                Slot = slot, Type = type, Position = position, AnimFlag = animated ? 1U : 0U,
                AnimDelay = animated ? data.AnimDelay : 1000, AnimCount = animated ? random.GenerateRnd(data.AnimDelay) : 0,
                AnimLength = data.AnimLength, AnimFrame = animated ? unchecked((uint)(random.GenerateRnd(data.AnimLength - 1) + 1)) : data.AnimDelay,
                AnimWidth = data.AnimWidth, SolidFlag = (data.Flags & Solid) != 0, MissilePassable = (data.Flags & MissilePassable) != 0,
                ApplyLighting = (data.Flags & ApplyLighting) != 0, DeleteFlag = false, BreakFlag = (data.Flags & Breakable) != 0 ? 1 : 0,
                SelectionRegion = data.SelectionRegion, PreFlag = false, TrapFlag = false, DoorFlag = false,
            };
            switch (type) {
            case ObjBarrel:
            case ObjBarrelExplosive:
                obj = obj with { RndSeed = random.AdvanceSeed(), Var1 = 0, Var2 = type == ObjBarrelExplosive ? 0 : random.GenerateRnd(10), Var3 = random.GenerateRnd(3) };
                if (obj.Var2 >= 8) obj = obj with { Var4 = PreSpawnSkeleton() };
                break;
            case ObjShrineL:
            case ObjShrineR:
                obj = AddShrine(obj);
                break;
            case ObjBookcaseL:
            case ObjBookcaseR:
                obj = obj with { RndSeed = random.AdvanceSeed(), PreFlag = true };
                break;
            case ObjBookstand:
                obj = obj with { RndSeed = random.AdvanceSeed() };
                break;
            case ObjSkeletonBook:
            case ObjBloodFountain:
            case ObjGoatShrine:
            case ObjTearFountain:
                obj = obj with { RndSeed = random.AdvanceSeed() };
                break;
            case ObjPurifyingFountain:
            case ObjMurkyFountain:
                SetLargeObjectRefs(slot, position);
                obj = obj with { RndSeed = random.AdvanceSeed() };
                break;
            case ObjDecapitated:
                obj = obj with { RndSeed = random.AdvanceSeed(), AnimFrame = unchecked((uint)(random.GenerateRnd(8) + 1)), PreFlag = true };
                break;
            case ObjTorturedBody:
                obj = obj with { RndSeed = random.AdvanceSeed(), AnimFrame = unchecked((uint)(random.GenerateRnd(4) + 1)), PreFlag = true };
                break;
            case ObjBurningCross:
                obj = obj with { RndSeed = random.AdvanceSeed() };
                break;
            }
            objects[slot] = obj;
            if (type is ObjCandle2 or ObjSkFire or ObjBookCandle or ObjBurningCross) {
                AuthoritativeCathedralThemeStaticLighting.Apply(light, preLight, position, 5, input.LoadingMapObjects);
                if (input.LoadingMapObjects)
                    updateLighting = true;
                objects[slot] = objects[slot] with { Var1 = -1 };
            }
            activeObjectCount++;
            return slot;
        }

        private AuthoritativeCathedralPopulationObjectState AddShrine(AuthoritativeCathedralPopulationObjectState obj)
        {
            var choices = input.Catalog.ShrineAvailable;
            var shrineSeed = random.AdvanceSeed();
            var index = 0;
            var attempts = 0;
            do {
                index = random.GenerateRnd(26);
                if (index < 0 || index >= choices.Count) throw new InvalidDataException("Shrine RNG was unsafe.");
                if (++attempts > input.MaximumPlacementAttempts)
                    throw new InvalidDataException("Shrine selection exhausted its bounded availability search.");
            } while (!choices[index]);
            obj = obj with { RndSeed = shrineSeed, PreFlag = true, Var1 = index };
            if (!random.FlipCoin(2)) obj = obj with { AnimFrame = 12, AnimLength = 22 };
            return obj;
        }

        private void SetLargeObjectRefs(int slot, Point position)
        {
            if (!Contains(position.X, position.Y - 1) || !Contains(position.X - 1, position.Y) || !Contains(position.X - 1, position.Y - 1))
                throw new InvalidDataException("Large theme object reached an unsafe native tile coordinate.");
            var id = -(slot + 1);
            SetObjectRef(position.X, position.Y - 1, id); SetObjectRef(position.X - 1, position.Y, id); SetObjectRef(position.X - 1, position.Y - 1, id);
        }

        private void ItemNoFlippy()
        {
            if (activeItemCount == 0)
                throw new InvalidDataException("Native ItemNoFlippy reached an empty item pool.");
            var slot = activeItems[activeItemCount - 1];
            items[slot] = items[slot] with {
                AnimFlag = false,
                SelectionRegion = 1,
                Animation = items[slot].Animation with { CurrentFrame = unchecked((sbyte)(items[slot].Animation.NumberOfFrames - 1)) },
            };
            itemNoFlippySlot = slot;
        }

        private bool FitShrine(sbyte region)
        {
            var found = 0;
            for (var y = 0; y < Width && found == 0; y++) for (var x = 0; x < Width && found == 0; x++) {
                if (!IsRegion(x, y, region)) continue;
                RequireNativeTile(x, y - 1, "shrine trap");
                if (Trap(x, y - 1) && NotSolid(x - 1, y) && NotSolid(x + 1, y)
                    && RawRegion(x - 1, y, region) && RawRegion(x + 1, y, region)
                    && !RawObjectAt(x - 1, y - 1) && !RawObjectAt(x + 1, y - 1)) found = 1;
                else {
                    RequireNativeTile(x - 1, y, "shrine trap");
                    if (Trap(x - 1, y) && NotSolid(x, y - 1) && NotSolid(x, y + 1)
                        && RawRegion(x, y - 1, region) && RawRegion(x, y + 1, region)
                        && !RawObjectAt(x - 1, y - 1) && !RawObjectAt(x - 1, y + 1)) found = 2;
                }
                if (found != 0) { themex = x; themey = y; }
            }
            if (found != 0)
                themeVar1 = found;
            return found != 0;
        }

        private bool FitObj5(sbyte region)
        {
            var target = random.GenerateRnd(5);
            if (target < 0) { themex = 0; themey = 0; return true; }
            var found = 0;
            for (var y = 0; y < Width; y++) for (var x = 0; x < Width; x++)
                if (IsRegion(x, y, region) && NotSolid(x, y) && CheckObj5(new(x, y), region)) {
                    themex = x; themey = y; if (++found > target) return true;
                }
            return found > 0;
        }

        private bool CheckObj5(Point origin, sbyte region)
        {
            for (var y = -2; y <= 2; y++) for (var x = -2; x <= 2; x++)
            {
                RequireNativeTile(origin.X + x, origin.Y + y, "theme 5x5 region");
                if (SolidAt(origin.X + x, origin.Y + y) || !IsRegion(origin.X + x, origin.Y + y, region)) return false;
            }
            return true;
        }

        private bool CheckObj3(Point origin, sbyte region, int frequency)
        {
            for (var y = -1; y <= 1; y++) for (var x = -1; x <= 1; x++) {
                Point p = new(origin.X + x, origin.Y + y);
                if (!Contains(p) || SolidAt(p.X, p.Y) || !IsRegion(p.X, p.Y, region) || IsObjectAt(p.X, p.Y)) return false;
                if (frequency > 0 && random.FlipCoin((uint)frequency)) return false;
            }
            return true;
        }

        private bool TryFindSkeleton(out int index)
        {
            for (var i = 0; i < input.Catalog.OrderedBestiary.Count; i++)
                if (input.Catalog.OrderedBestiary[i].Definition.IsSkeleton) {
                    index = i;
                    return true;
                }
            index = -1;
            return false;
        }

        private bool TryFindGoat(out int index)
        {
            for (var i = 0; i < input.Catalog.OrderedBestiary.Count; i++)
                if (input.Catalog.OrderedBestiary[i].IsGoat) {
                    index = i;
                    return true;
                }
            index = -1;
            return false;
        }

        // CreateThemeRooms receives Cathedral's level type, not the dungeon
        // depth used by item generation. All ordinary Cathedral rows therefore
        // use the first entry of each native frequency table.
        private static int BarrelFrequency() => 2;
        private static int TreasureFrequency() => 4;
        private static int LibraryFrequency() => 1;
        private static int TortureFrequency() => 6;
        private static int BurningCrossFrequency() => 5;
        private int MonsterFrequency(int theme) => theme switch {
            0 => 5,
            1 => 6,
            2 => 6,
            4 => 6,
            5 => 5,
            6 or 7 or 8 or 9 or 13 or 14 or 15 => 6,
            _ => throw new InvalidDataException("Theme has no Cathedral monster frequency."),
        };

        private bool SameRegion(Point a, Point b)
        {
            if (!Contains(a) || !Contains(b))
                throw new InvalidDataException("Native transparency lookup reached an unsafe tile coordinate.");
            return transparency[Index(a)] == transparency[Index(b)];
        }
        private void RequireNativeTile(int x, int y, string operation)
        {
            if (!Contains(x, y))
                throw new InvalidDataException($"Native {operation} lookup reached an unsafe tile coordinate.");
        }
        private bool RawRegion(int x, int y, sbyte region)
        {
            RequireNativeTile(x, y, "theme transparency");
            return transparency[Index(x, y)] == region;
        }
        private bool RawObjectAt(int x, int y)
            => IsObjectAt(x, y);
        private static int WalkingDistance(Point a, Point b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        private bool IsRegion(int x, int y, sbyte region) => Contains(x, y) && transparency[Index(x, y)] == region;
        private bool NotSolid(int x, int y) => Contains(x, y) && !SolidAt(x, y);
        private bool SolidAt(int x, int y) => Contains(x, y) && (pieceProperties[pieces[Index(x, y)]] & SolidProperty) != 0;
        private bool Trap(int x, int y) => Contains(x, y) && (pieceProperties[pieces[Index(x, y)]] & TrapProperty) != 0;
        private bool IsObjectAt(int x, int y) => Contains(x, y) && objectRefs[Index(x, y)] != 0;
        private bool MonsterAt(int x, int y) => Contains(x, y) && monsterRefs[Index(x, y)] != 0;
        private int ItemAt(int x, int y) => Contains(x, y) ? itemRefs[Index(x, y)] : 0;
        private void SetObjectRef(int x, int y, int value) { if (Contains(x, y)) objectRefs[Index(x, y)] = value; }
        private bool IsTileAvailable(Point p) => Contains(p) && playerRefs[Index(p)] == 0 && monsterRefs[Index(p)] == 0 && !SolidAt(p.X, p.Y) && !SolidObjectAt(p.X, p.Y);
        private bool SolidObjectAt(int x, int y)
        {
            if (!Contains(x, y)) return false;
            var reference = objectRefs[Index(x, y)];
            if (reference == 0) return false;
            var slot = Math.Abs(reference) - 1;
            return slot >= 0 && slot < objects.Length && objects[slot].SolidFlag;
        }
        private static bool Contains(Point p) => Contains(p.X, p.Y);
        private static bool IsNativeWorldPoint(Point p)
            => p.X is >= 0 and <= byte.MaxValue && p.Y is >= 0 and <= byte.MaxValue;
        private static bool Contains(int x, int y) => x is >= 0 and < Width && y is >= 0 and < Width;
        private static int Index(Point p) => checked(p.Y * Width + p.X);
        private static int Index(int x, int y) => checked(y * Width + x);
        private static int Dx(int direction) => direction switch { 0 => 1, 1 => 0, 2 => -1, 3 => -1, 4 => -1, 5 => 0, 6 => 1, 7 => 1, _ => 0 };
        private static int Dy(int direction) => direction switch { 0 => 1, 1 => 1, 2 => 1, 3 => 0, 4 => -1, 5 => -1, 6 => -1, 7 => 0, _ => 0 };
        private static int Opposite(int direction) => (direction + 4) % 8;

        private static void ValidateRequest(AuthoritativeCathedralThemeRoomRequest value)
        {
            ArgumentNullException.ThrowIfNull(value.State);
            ArgumentNullException.ThrowIfNull(value.Catalog);
            ArgumentNullException.ThrowIfNull(value.ItemCatalog);
            ArgumentNullException.ThrowIfNull(value.ItemPolicy);
            ArgumentNullException.ThrowIfNull(value.Themes);
            ArgumentNullException.ThrowIfNull(value.TargetPlayer);
            ValidatePolicy(value.ItemPolicy);
            if (value.PlayerContexts is not null) {
                if (value.PlayerContexts.Count is < 1 or > 4
                    || value.PlayerContexts.Any(player => player is null))
                    throw new InvalidDataException("Player contexts must contain at most four distinct in-range players.");
                if (value.PlayerContexts.Any(player => player.Id is < 0 or > 3
                        || player.Level is < 0 or > 24
                        || !IsNativeWorldPoint(player.Tile) || !IsNativeWorldPoint(player.Future)
                        || value.PlayerContexts.Count(other => other.Id == player.Id) != 1))
                    throw new InvalidDataException("Player contexts must contain at most four distinct in-range players.");
            }
            if (value.Themes.Count > MaxThemes
                || value.Themes.Any(theme => theme is null || theme.ThemeType is 10 or 12 or 16 || theme.ThemeType is < 0 or > 16 || theme.RegionId is < sbyte.MinValue or > sbyte.MaxValue))
                throw new InvalidDataException("Only the fourteen ordinary Cathedral theme IDs are supported.");
            if (value.MaximumPlacementAttempts <= 0 || value.ItemPolicy.Depth is < 1 or > 4)
                throw new InvalidDataException("Theme placement bounds or Cathedral depth are invalid.");
            if (value.InitialThemeVar1 is < 0 or > 23)
                throw new InvalidDataException("Initial theme scratch Var1 is outside the native theme range.");
            if (!Contains(value.InitialThemeOrigin))
                throw new InvalidDataException("Initial theme scratch coordinates are outside dungeon bounds.");
            if (value.TargetPlayer.Id is < 0 or > 3
                || value.TargetPlayer.Level is < 0 or > 24
                || !IsNativeWorldPoint(value.TargetPlayer.Tile) || !IsNativeWorldPoint(value.TargetPlayer.Future))
                throw new InvalidDataException("Target player state is outside dungeon bounds.");
        }

        // Room objects and item callbacks share the ordinary SP Normal policy boundary.
        private static void ValidatePolicy(AuthoritativeCathedralThemeItemPolicy policy)
        {
            if (policy.Depth is < 1 or > 4 || policy.CurrentDungeonLevel is < 0 or > 4
                || policy.GameMode != DiabloGameMode.SinglePlayer || policy.Difficulty != DiabloDifficulty.Normal
                || policy.Hellfire || policy.Spawn || policy.QuestEnabled || policy.SetLevel
                || !policy.LegacyLevelItemGenerationAllowed
                || (policy.CurrentDungeonLevel != 0 && policy.CurrentDungeonLevel != policy.Depth))
                throw new InvalidDataException("Only ordinary original-Diablo Cathedral SP Normal generation is supported.");
        }
    }
}

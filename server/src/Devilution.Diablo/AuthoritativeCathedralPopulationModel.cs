namespace Devilution.Diablo;

/** Native fine-coordinate inputs and outputs for the ordinary initial-object phase. */
public readonly record struct AuthoritativeCathedralPopulationPoint(int X, int Y);

/** SetupObject metadata; native type IDs are distinct from runtime entity IDs. */
public sealed record AuthoritativeCathedralPopulationObjectDefinition(
    int NativeType, byte Flags, byte AnimDelay, byte AnimLength,
    byte AnimWidth, int SelectionRegion);

/** InitMonster prerequisites from the ordered, pre-phase bestiary and raw data. */
public sealed record AuthoritativeCathedralPopulationMonsterDefinition(
    int NativeType, bool IsSkeleton, int MinimumHitPoints, int MaximumHitPoints,
    int Ai, int Intelligence, uint AbilityFlags,
    int MinDamage, int MaxDamage, int MinDamageSpecial, int MaxDamageSpecial,
    int ArmorClass, int Resistance,
    int ReducePlayerStrength, int ReducePlayerMagic, int ReducePlayerDexterity,
    int ReducePlayerVitality, int ReducePlayerMaxHP, int ReducePlayerMaxMana,
    IReadOnlyList<int> Frames, IReadOnlyList<int> Rates);

/** The only potential player target in the single-player phase contract. */
public sealed record AuthoritativeCathedralPopulationPlayer(
    int Id, int Level, bool Active, bool LevelChanging,
    AuthoritativeCathedralPopulationPoint Tile,
    AuthoritativeCathedralPopulationPoint Future);

/** Detached pre-InitGolems geometry. SOL is indexed by piece, never by cell. */
public sealed class AuthoritativeCathedralPopulationGeometry
{
    public const int Width = 112;
    public const int CellCount = Width * Width;
    public const int PieceCount = 1379;

    public AuthoritativeCathedralPopulationGeometry(
        IReadOnlyList<ushort> pieces, IReadOnlyList<byte> special,
        IReadOnlyList<byte> transparency, IReadOnlyList<byte> pieceProperties,
        IReadOnlyList<bool> populated, IReadOnlyList<bool> playerOccupied,
        IReadOnlyList<bool> monsterOccupied)
    {
        Pieces = Copy(pieces, CellCount, nameof(pieces));
        Special = Copy(special, CellCount, nameof(special));
        Transparency = Copy(transparency, CellCount, nameof(transparency));
        PieceProperties = Copy(pieceProperties, PieceCount, nameof(pieceProperties));
        Populated = Copy(populated, CellCount, nameof(populated));
        PlayerOccupied = Copy(playerOccupied, CellCount, nameof(playerOccupied));
        MonsterOccupied = Copy(monsterOccupied, CellCount, nameof(monsterOccupied));
        if (Pieces.Any(piece => piece >= PieceCount))
            throw new InvalidDataException("Cathedral pieces must resolve in the supplied SOL catalog.");
    }

    public IReadOnlyList<ushort> Pieces { get; }
    public IReadOnlyList<byte> Special { get; }
    public IReadOnlyList<byte> Transparency { get; }
    /** Preserve the native opaque 0x40 channel; only named Solid/Trap bits affect this phase. */
    public IReadOnlyList<byte> PieceProperties { get; }
    public IReadOnlyList<bool> Populated { get; }
    public IReadOnlyList<bool> PlayerOccupied { get; }
    public IReadOnlyList<bool> MonsterOccupied { get; }

    internal static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values, int count, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        if (values.Count != count)
            throw new InvalidDataException($"{name} requires exactly {count} values.");
        return Array.AsReadOnly(values.ToArray());
    }
}

/**
 * Validated phase inputs, not a generated object/monster replay. This contract is
 * ordinary original Diablo, quest-disabled, single-player Normal, with fresh
 * object refs and monster slots whose untouched fields are zero. GetLevelMTypes,
 * InitThemes and HoldThemeRooms belong upstream; no archives are read here.
 */
public sealed class AuthoritativeCathedralPopulationParameters
{
    public const int ObjectCapacity = 127;
    public const int MonsterCapacity = 200;
    public const int GolemNativeType = 109;
    public const string Policy = "ordinary-original-diablo-fresh-initial-objects";
    private static readonly int[] SkeletonTypes = [8, 9, 10, 11, 20, 21, 22, 23, 24, 25, 26, 27];

    public AuthoritativeCathedralPopulationParameters(
        int depth, uint populationSeed, uint storySeed,
        AuthoritativeCathedralPopulationGeometry geometry,
        IReadOnlyList<AuthoritativeCathedralPopulationObjectDefinition> objects,
        IReadOnlyList<AuthoritativeCathedralPopulationMonsterDefinition> orderedBestiary,
        AuthoritativeCathedralPopulationPlayer player, int trapPercent,
        IReadOnlyList<int> storyMessages, int maximumPlacementAttempts = 1_000_000)
    {
        if (depth is < 1 or > 4 || trapPercent is < 0 or > 100
            || maximumPlacementAttempts <= 0)
            throw new InvalidDataException("Invalid ordinary Cathedral phase bounds.");
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(orderedBestiary);
        ArgumentNullException.ThrowIfNull(player);
        if (objects.Count != 109 || objects.Where((entry, index) =>
                entry is null || entry.NativeType != index || (entry.Flags & ~63) != 0
                || entry.SelectionRegion is < 0 or > 7).Any())
            throw new InvalidDataException("The complete ordered native object catalog is required.");
        if (orderedBestiary.Count is < 1 or > 24
            || orderedBestiary[0] is null
            || orderedBestiary[0].NativeType != GolemNativeType
            || orderedBestiary.Any(entry => entry is null)
            || orderedBestiary.Select(entry => entry.NativeType).Distinct().Count() != orderedBestiary.Count)
            throw new InvalidDataException("The ordered native bestiary must start with Golem and contain unique types.");
        var detached = new List<AuthoritativeCathedralPopulationMonsterDefinition>();
        foreach (var entry in orderedBestiary) {
            if (entry.NativeType is < 0 or >= MonsterCapacity
                || entry.IsSkeleton != SkeletonTypes.Contains(entry.NativeType)
                || entry.MinimumHitPoints is < 0 or > ushort.MaxValue
                || entry.MaximumHitPoints < entry.MinimumHitPoints
                || entry.MaximumHitPoints > ushort.MaxValue
                || entry.Ai is < -1 or > 39 || entry.AbilityFlags > ushort.MaxValue
                || new[] { entry.Intelligence, entry.MinDamage, entry.MaxDamage,
                    entry.MinDamageSpecial, entry.MaxDamageSpecial, entry.ArmorClass,
                    entry.Resistance, entry.ReducePlayerStrength, entry.ReducePlayerMagic,
                    entry.ReducePlayerDexterity, entry.ReducePlayerVitality,
                    entry.ReducePlayerMaxHP, entry.ReducePlayerMaxMana }.Any(value => value is < 0 or > 255))
                throw new InvalidDataException("Invalid native monster metadata.");
            var frames = AuthoritativeCathedralPopulationGeometry.Copy(entry.Frames, 6, "monster frames");
            var rates = AuthoritativeCathedralPopulationGeometry.Copy(entry.Rates, 6, "monster rates");
            if (frames.Concat(rates).Any(value => value is < 0 or > 127))
                throw new InvalidDataException("Monster frames/rates must fit the native nonnegative int8 metadata.");
            detached.Add(entry with { Frames = frames, Rates = rates });
        }
        if (player.Id is < 0 or > 3 || player.Level is < 0 or > 24
            || !Contains(player.Tile) || !Contains(player.Future))
            throw new InvalidDataException("Invalid single-player phase context.");
        var messages = AuthoritativeCathedralPopulationGeometry.Copy(storyMessages, 3, nameof(storyMessages));
        if (messages.Any(value => value < 0))
            throw new InvalidDataException("All three Cathedral story speech identities are required.");
        Depth = depth;
        PopulationSeed = populationSeed;
        StorySeed = storySeed;
        Geometry = geometry;
        Objects = Array.AsReadOnly(objects.ToArray());
        OrderedBestiary = detached.AsReadOnly();
        Player = player;
        TrapPercent = trapPercent;
        StoryMessages = messages;
        MaximumPlacementAttempts = maximumPlacementAttempts;
    }

    public int Depth { get; }
    public uint PopulationSeed { get; }
    public uint StorySeed { get; }
    public AuthoritativeCathedralPopulationGeometry Geometry { get; }
    public IReadOnlyList<AuthoritativeCathedralPopulationObjectDefinition> Objects { get; }
    public IReadOnlyList<AuthoritativeCathedralPopulationMonsterDefinition> OrderedBestiary { get; }
    public AuthoritativeCathedralPopulationPlayer Player { get; }
    public int TrapPercent { get; }
    public IReadOnlyList<int> StoryMessages { get; }
    public int MaximumPlacementAttempts { get; }
    internal static bool Contains(AuthoritativeCathedralPopulationPoint p)
        => p.X is >= 0 and < 112 && p.Y is >= 0 and < 112;
}

/** Actual current-native Object fields, including signed values and uint seeds. */
public sealed record AuthoritativeCathedralPopulationObjectState
{
    public int Slot { get; init; }
    public int Type { get; init; } = -1;
    public AuthoritativeCathedralPopulationPoint Position { get; init; }
    public bool ApplyLighting { get; init; }
    public uint AnimFlag { get; init; }
    public int AnimDelay { get; init; }
    public int AnimCount { get; init; }
    public uint AnimLength { get; init; }
    public uint AnimFrame { get; init; }
    public ushort AnimWidth { get; init; }
    public bool DeleteFlag { get; init; }
    public int BreakFlag { get; init; }
    public bool SolidFlag { get; init; }
    public bool MissilePassable { get; init; }
    public int SelectionRegion { get; init; }
    public bool PreFlag { get; init; }
    public bool TrapFlag { get; init; }
    public bool DoorFlag { get; init; }
    public int LightId { get; init; }
    public uint RndSeed { get; init; }
    public int Var1 { get; init; }
    public int Var2 { get; init; }
    public int Var3 { get; init; }
    public int Var4 { get; init; }
    public int Var5 { get; init; }
    public uint Var6 { get; init; }
    public int BookMessage { get; init; } = -1;
    public int Var8 { get; init; }
}

/** Active monster fields written or preserved by fresh InitMonster/startStand. */
public sealed record AuthoritativeCathedralPopulationMonsterState
{
    public int Slot { get; init; }
    public int Type { get; init; }
    public int LevelType { get; init; }
    public int Mode { get; init; }
    public int Goal { get; init; }
    public int GoalVar1 { get; init; }
    public int GoalVar2 { get; init; }
    public int GoalVar3 { get; init; }
    public int Var1 { get; init; }
    public int Var2 { get; init; }
    public int Var3 { get; init; }
    public int PathCount { get; init; }
    public AuthoritativeCathedralPopulationPoint Tile { get; init; }
    public AuthoritativeCathedralPopulationPoint Future { get; init; }
    public AuthoritativeCathedralPopulationPoint Old { get; init; }
    public AuthoritativeCathedralPopulationPoint Last { get; init; }
    public AuthoritativeCathedralPopulationPoint Temp { get; init; }
    public int Direction { get; init; }
    public int Enemy { get; init; }
    public AuthoritativeCathedralPopulationPoint EnemyPosition { get; init; }
    public bool Invalid { get; init; }
    public int Ai { get; init; }
    public int Intelligence { get; init; }
    public int ActiveForTicks { get; init; }
    public int UniqueType { get; init; }
    public int UniqueTranslation { get; init; }
    public int CorpseId { get; init; }
    public int WhoHit { get; init; }
    public int MinDamage { get; init; }
    public int MaxDamage { get; init; }
    public int MinDamageSpecial { get; init; }
    public int MaxDamageSpecial { get; init; }
    public int ArmorClass { get; init; }
    public int Resistance { get; init; }
    public int TalkMessage { get; init; }
    public int Leader { get; init; }
    public int LeaderRelation { get; init; }
    public int PackSize { get; init; }
    public uint Flags { get; init; }
    public uint RndItemSeed { get; init; }
    public uint AiSeed { get; init; }
    public int LightId { get; init; }
    public int HitPoints { get; init; }
    public int MaxHitPoints { get; init; }
    public int AnimTicksPerFrame { get; init; }
    public int AnimTickCounter { get; init; }
    public int AnimNumberOfFrames { get; init; }
    public int AnimCurrentFrame { get; init; }
    public int GolemToHit { get; init; }
    public int ReducePlayerStrength { get; init; }
    public int ReducePlayerMagic { get; init; }
    public int ReducePlayerDexterity { get; init; }
    public int ReducePlayerVitality { get; init; }
    public int ReducePlayerMaxHP { get; init; }
    public int ReducePlayerMaxMana { get; init; }
}

/** Detached output; active/available object arrays include their native unused tails. */
public sealed class AuthoritativeCathedralPopulationResult
{
    internal AuthoritativeCathedralPopulationResult(
        AuthoritativeCathedralPopulationObjectState[] objects, int activeObjectCount,
        int[] activeObjectIds, int[] availableObjectIds, int[] tileObjectRefs,
        AuthoritativeCathedralPopulationMonsterState[] monsters,
        ushort[] pieces, byte[] special, byte[] transparency,
        uint randomAfterGolems, uint finalRandomState)
    {
        Objects = Array.AsReadOnly(objects.ToArray());
        ActiveObjectCount = activeObjectCount;
        ActiveObjectIds = Array.AsReadOnly(activeObjectIds.ToArray());
        AvailableObjectIds = Array.AsReadOnly(availableObjectIds.ToArray());
        TileObjectRefs = Array.AsReadOnly(tileObjectRefs.ToArray());
        Monsters = Array.AsReadOnly(monsters.ToArray());
        ActiveMonsterIds = Array.AsReadOnly(monsters.Select(m => m.Slot).ToArray());
        Pieces = Array.AsReadOnly(pieces.ToArray());
        Special = Array.AsReadOnly(special.ToArray());
        Transparency = Array.AsReadOnly(transparency.ToArray());
        RandomAfterGolems = randomAfterGolems;
        FinalRandomState = finalRandomState;
    }
    public IReadOnlyList<AuthoritativeCathedralPopulationObjectState> Objects { get; }
    public int ActiveObjectCount { get; }
    public IReadOnlyList<int> ActiveObjectIds { get; }
    public IReadOnlyList<int> AvailableObjectIds { get; }
    public IReadOnlyList<int> TileObjectRefs { get; }
    public IReadOnlyList<AuthoritativeCathedralPopulationMonsterState> Monsters { get; }
    public IReadOnlyList<int> ActiveMonsterIds { get; }
    public IReadOnlyList<ushort> Pieces { get; }
    public IReadOnlyList<byte> Special { get; }
    public IReadOnlyList<byte> Transparency { get; }
    public uint RandomAfterGolems { get; }
    public uint FinalRandomState { get; }
}

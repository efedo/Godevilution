using Devilution.Engine;

namespace Devilution.Diablo;

/** One deterministic server-owned monster placement produced from validated content. */
public sealed record AuthoritativeMonsterSpawn(
    AuthoritativeMonsterTypeDefinition Definition,
    uint LevelId,
    int PositionX,
    int PositionY,
    int HitPoints,
    int AttackDamage)
{
    /** Native placement direction selected before monster initialization. */
    public int Direction { get; init; }

    /** Native InitMonster seed used for monster-owned item drops. */
    public uint MonsterItemSeed { get; init; }

    /** Native InitMonster seed used for multiplayer AI synchronization. */
    public uint MonsterAiSeed { get; init; }

    /** Native InitMonster standing-animation frame selected before combat. */
    public int InitialStandAnimationFrame { get; init; }

    /** Native InitMonster standing-animation tick selected before combat. */
    public int InitialStandAnimationTick { get; init; }
}

/**
 * Selects level-valid monster types and walkable cells without depending on
 * presentation, transport, or native map code. Static encounter rows remain
 * authoritative until a complete native map-generation replacement is ready.
 */
public sealed class AuthoritativeMonsterSpawnPlanner
{
    /** Reserved entity range shared with the authoritative combat processor. */
    public const uint FirstGeneratedMonsterEntityId = 0x10000000;

    /**
     * Native ordinary-level scatter leaves ten of the 200 monster slots
     * available for quest and runtime summons.
     */
    public const int NativeOrdinaryLevelMonsterCapacity =
        AuthoritativeLevelGenerationRule.DefaultOrdinaryMonsterCapacity;

    /** Reserved world-item range for drops attached to generated monsters. */
    public const uint FirstGeneratedDropEntityId = 0x40000000;

    private readonly AuthoritativeWorld world;
    private readonly AuthoritativeMonsterTypeCatalog monsterTypes;
    private readonly AuthoritativeLevelGenerationCatalog levelGeneration;
    private readonly AuthoritativeItemCatalog? itemCatalog;
    private readonly AuthoritativeMonsterCatalog? monsterCatalog;
    private readonly AuthoritativeSpellBookCatalog? spellBooks;
    private readonly AuthoritativeUniqueMonsterCatalog? uniqueMonsters;
    private readonly AuthoritativeMonsterSpawnPolicyCatalog? spawnPolicies;

    public AuthoritativeMonsterSpawnPlanner(
        AuthoritativeWorld world,
        AuthoritativeMonsterTypeCatalog monsterTypes,
        AuthoritativeLevelGenerationCatalog levelGeneration,
        AuthoritativeItemCatalog? itemCatalog = null,
        AuthoritativeMonsterCatalog? monsterCatalog = null,
        AuthoritativeSpellBookCatalog? spellBooks = null,
        AuthoritativeUniqueMonsterCatalog? uniqueMonsters = null,
        AuthoritativeMonsterSpawnPolicyCatalog? spawnPolicies = null)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.monsterTypes = monsterTypes
            ?? throw new ArgumentNullException(nameof(monsterTypes));
        this.levelGeneration = levelGeneration
            ?? throw new ArgumentNullException(nameof(levelGeneration));
        this.itemCatalog = itemCatalog;
        this.monsterCatalog = monsterCatalog;
        this.spellBooks = spellBooks;
        this.uniqueMonsters = uniqueMonsters;
        this.spawnPolicies = spawnPolicies;
    }

    /** Plans bounded deterministic placements while avoiding reserved cells. */
    public IReadOnlyList<AuthoritativeMonsterSpawn> Plan(
        uint levelId,
        uint seed,
        bool multiplayer,
        int maximumMonsters,
        IEnumerable<(int X, int Y)>? reservedPositions = null)
    {
        if (maximumMonsters < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumMonsters));
        if (!world.ContainsLevel(levelId))
            throw new InvalidDataException($"Monster spawn level {levelId} is not defined.");
        if (levelId > int.MaxValue)
            throw new InvalidDataException($"Monster spawn level {levelId} exceeds native level bounds.");

        var candidates = monsterTypes.GetAvailableForDungeonLevel((int)levelId)
            .Where(IsOrdinarySpawnCandidate)
            .ToArray();
        if (maximumMonsters == 0 || candidates.Length == 0)
            return [];

        var reserved = (reservedPositions ?? []).ToHashSet();
        var level = world.Levels.Single(candidate => candidate.LevelId == levelId);
        var positions = Enumerable.Range(0, checked(level.Width * level.Height))
            .Select(cell => (X: cell % level.Width, Y: cell / level.Width))
            .Where(position => level.IsWalkable(position.X, position.Y)
                && !reserved.Contains(position))
            .ToArray();
        var requestedCount = levelGeneration.CalculateMonsterCount(
            levelId,
            positions.Length,
            multiplayer);
        var spawnCount = Math.Min(
            Math.Min(maximumMonsters, requestedCount),
            levelGeneration.GetRequired(levelId).OrdinaryMonsterCapacity);
        if (spawnCount == 0)
            return [];

        var random = new DeterministicLegacyRandom(seed ^ levelId);
        DeterministicRandomOrdering.Shuffle(positions, random.Next);
        var orderedCandidates = candidates;
        DeterministicRandomOrdering.Shuffle(orderedCandidates, random.Next);
        var spawns = new AuthoritativeMonsterSpawn[spawnCount];
        for (var index = 0; index < spawnCount; index++) {
            var definition = orderedCandidates[index % orderedCandidates.Length];
            var direction = random.Next(8);
            var initialAnimation = ConsumeNativeInitialAnimationRandom(random, definition);
            var hitPoints = random.Next(
                checked(definition.MaximumHitPoints - definition.MinimumHitPoints + 1))
                + definition.MinimumHitPoints;
            var monsterItemSeed = random.AdvanceSeed();
            var monsterAiSeed = random.AdvanceSeed();
            var attackDamage = random.Next(
                checked(definition.MaximumDamage - definition.MinimumDamage + 1))
                + definition.MinimumDamage;
            var position = positions[index];
            spawns[index] = new AuthoritativeMonsterSpawn(
                definition,
                levelId,
                position.X,
                position.Y,
                hitPoints,
                attackDamage) {
                    Direction = direction,
                    MonsterItemSeed = monsterItemSeed,
                    MonsterAiSeed = monsterAiSeed,
                    InitialStandAnimationFrame = initialAnimation.Frame,
                    InitialStandAnimationTick = initialAnimation.Tick,
                };
        }
        return spawns;
    }

    /** Converts planned placements into validated authoritative combat targets. */
    public IReadOnlyList<AuthoritativeCombatTarget> PlanCombatTargets(
        uint levelId,
        uint seed,
        bool multiplayer,
        int maximumMonsters,
        uint firstEntityId = FirstGeneratedMonsterEntityId,
        IEnumerable<(int X, int Y)>? reservedPositions = null)
    {
        if (firstEntityId == 0)
            throw new ArgumentOutOfRangeException(nameof(firstEntityId));
        var spawns = Plan(
            levelId,
            seed,
            multiplayer,
            maximumMonsters,
            reservedPositions);
        var targets = spawns
            .Select((spawn, index) => CreateCombatTarget(
                spawn,
                index,
                seed,
                firstEntityId,
                multiplayer,
                spawn.Direction))
            .ToArray();
        uniqueMonsters?.ApplyToGenerated(targets);
        return targets;
    }

    /** Creates one level-valid summon selected by its validated native table identity. */
    public AuthoritativeCombatTarget? CreateNativeMonsterSummon(
        uint levelId,
        uint nativeMonsterId,
        uint seed,
        int positionX,
        int positionY,
        uint entityId,
        bool multiplayer = false,
        int direction = 0,
        bool ignoreDungeonLevelRange = false,
        uint? stableMonsterId = null,
        bool applyUniqueSelector = true,
        int? intelligenceOverride = null)
    {
        if (entityId == 0)
            throw new ArgumentOutOfRangeException(nameof(entityId));
        if (!world.ContainsLevel(levelId) || levelId > int.MaxValue)
            return null;
        AuthoritativeMonsterTypeDefinition definition;
        if (stableMonsterId is not null) {
            if (!monsterTypes.TryGet(stableMonsterId.Value, out definition)
                || definition.NativeMonsterId != nativeMonsterId)
                return null;
        } else if (!monsterTypes.TryGetNative(nativeMonsterId, out definition)
            && !monsterTypes.TryGet(nativeMonsterId, out definition))
            return null;
        if (!ignoreDungeonLevelRange
            && (definition.MinimumDungeonLevel > (int)levelId
                || definition.MaximumDungeonLevel < (int)levelId))
            return null;

        var hitPointRange = checked(
            definition.MaximumHitPoints - definition.MinimumHitPoints + 1);
        var damageRange = checked(
            definition.MaximumDamage - definition.MinimumDamage + 1);
        var hitPoints = definition.MinimumHitPoints
            + (int)(seed % (uint)hitPointRange);
        var attackDamage = definition.MinimumDamage
            + (int)((seed / 0x100U) % (uint)damageRange);
        var uniqueDefinition = applyUniqueSelector
            ? uniqueMonsters?.Definitions.FirstOrDefault(uniqueDefinition =>
                uniqueDefinition.EntityId == 0
                && (uniqueDefinition.MonsterId == 0
                    || uniqueDefinition.MonsterId == definition.MonsterId)
                && (uniqueDefinition.NativeAi.Length == 0
                    || string.Equals(
                        uniqueDefinition.NativeAi,
                        definition.NativeAi,
                        StringComparison.Ordinal)))
            : null;
        var target = CreateCombatTarget(
            new AuthoritativeMonsterSpawn(
                definition,
                levelId,
                positionX,
                positionY,
                hitPoints,
                attackDamage),
            0,
            seed,
            entityId,
            multiplayer,
            direction,
            preserveSinglePlayerHealth: uniqueDefinition is not null,
            intelligenceOverride: intelligenceOverride,
            monsterItemSeed: CreateRuntimeSeed(seed, definition.MonsterId ^ 0xA11CEU),
            monsterAiSeed: CreateRuntimeSeed(seed, definition.MonsterId ^ 0xA17EEDU));
        if (uniqueDefinition is not null) {
            target.IsUnique = true;
            uniqueMonsters!.ApplyToGenerated([target]);
        }
        return target;
    }

    /**
     * Creates the additional monsters placed by a native unique encounter.
     * Placement is deterministic, bounded, and uses the same authoritative
     * planner as ordinary summons; generated members never become unique
     * selector matches themselves.
     */
    public IReadOnlyList<AuthoritativeCombatTarget> CreateNativeMonsterPack(
        AuthoritativeCombatTarget leader,
        uint minionMonsterId,
        int packSize,
        uint seed,
        uint firstEntityId,
        bool multiplayer,
        AuthoritativeUniqueMonsterPackKind kind,
        IEnumerable<(uint LevelId, int X, int Y)>? reservedPositions = null)
    {
        ArgumentNullException.ThrowIfNull(leader);
        if (minionMonsterId == 0)
            throw new ArgumentOutOfRangeException(nameof(minionMonsterId));
        if (packSize is < 1 or > 255)
            throw new ArgumentOutOfRangeException(nameof(packSize));
        if (firstEntityId == 0)
            throw new ArgumentOutOfRangeException(nameof(firstEntityId));
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (!world.ContainsLevel(leader.LevelId))
            throw new InvalidDataException(
                $"Unique monster leader {leader.EntityId} references an unknown level.");
        if (!monsterTypes.TryGet(minionMonsterId, out _))
            throw new InvalidDataException(
                $"Unique monster pack references unknown minion monster {minionMonsterId}.");

        var reserved = (reservedPositions ?? [])
            .Select(position => (position.LevelId, position.X, position.Y))
            .ToHashSet();
        reserved.Add((leader.LevelId, leader.PositionX, leader.PositionY));

        var random = new DeterministicLegacyRandom(seed ^ leader.EntityId ^ minionMonsterId);
        var members = new List<AuthoritativeCombatTarget>(packSize);
        var level = world.Levels.Single(level => level.LevelId == leader.LevelId);
        for (var groupAttempt = 0; groupAttempt < 10 && members.Count < packSize; groupAttempt++) {
            members.Clear();
            var occupied = reserved.ToHashSet();
            var anchorDirection = random.Next(8);
            var anchorDisplacement = DeterministicGridDirections.GetDisplacement(
                (DeterministicGridDirection)anchorDirection);
            var anchorX = checked(leader.PositionX + anchorDisplacement.X);
            var anchorY = checked(leader.PositionY + anchorDisplacement.Y);
            var cursorX = anchorX;
            var cursorY = anchorY;

            for (var placementAttempt = 0;
                members.Count < packSize && placementAttempt < 100;
                placementAttempt++) {
                var xDisplacement = DeterministicGridDirections.GetDisplacement(
                    (DeterministicGridDirection)random.Next(8));
                var yDisplacement = DeterministicGridDirections.GetDisplacement(
                    (DeterministicGridDirection)random.Next(8));
                var candidate = (
                    X: checked(cursorX + xDisplacement.X),
                    Y: checked(cursorY + yDisplacement.Y));
                cursorX = candidate.X;
                cursorY = candidate.Y;
                if (!world.ContainsPosition(leader.LevelId, candidate.X, candidate.Y)
                    || !world.IsWalkable(leader.LevelId, candidate.X, candidate.Y)
                    || occupied.Contains((leader.LevelId, candidate.X, candidate.Y))
                    || !level.IsSameTransparencyRegion(anchorX, anchorY, candidate.X, candidate.Y))
                    continue;
                if (kind == AuthoritativeUniqueMonsterPackKind.Leashed
                    && (DeterministicGridMetrics.ChebyshevDistance(
                            candidate.X,
                            candidate.Y,
                            anchorX,
                            anchorY) >= AuthoritativeMonsterGroupUnity.ReunionDistance
                        || DeterministicGridMetrics.ChebyshevDistance(
                            candidate.X,
                            candidate.Y,
                            leader.PositionX,
                            leader.PositionY) >= AuthoritativeMonsterGroupUnity.ReunionDistance))
                    continue;

                var memberIndex = members.Count;
                var memberEntityId = checked(firstEntityId + (uint)memberIndex);
                if (memberEntityId == leader.EntityId)
                    throw new InvalidDataException(
                        $"Unique monster pack member {memberIndex + 1} reuses leader entity {leader.EntityId}.");
                var member = CreateNativeMonsterSummon(
                    leader.LevelId,
                    minionMonsterId,
                    random.AdvanceSeed(),
                    candidate.X,
                    candidate.Y,
                    memberEntityId,
                    multiplayer,
                    random.Next(8),
                    ignoreDungeonLevelRange: true,
                    applyUniqueSelector: false,
                    intelligenceOverride: leader.Intelligence);
                if (member is null)
                    throw new InvalidDataException(
                        $"Unique monster leader {leader.EntityId} could not materialize pack member {memberIndex + 1}.");
                member.RestoreHitPointsQ6(
                    checked(member.MaxHitPointsQ6 * 2),
                    checked(member.MaxHitPointsQ6 * 2));
                if (kind == AuthoritativeUniqueMonsterPackKind.Leashed)
                    member.RestoreGroupUnity(
                        leader.EntityId,
                        AuthoritativeMonsterLeaderRelation.Leashed,
                        0);
                occupied.Add((leader.LevelId, candidate.X, candidate.Y));
                members.Add(member);
            }
        }

        if (members.Count < packSize)
            throw new InvalidDataException(
                $"Unique monster leader {leader.EntityId} could not place pack member {members.Count + 1}.");

        leader.RestoreGroupUnity(
            leaderEntityId: 0,
            leaderRelation: AuthoritativeMonsterLeaderRelation.None,
            packSize: kind == AuthoritativeUniqueMonsterPackKind.Leashed
                ? members.Count
                : 0);
        return members;
    }

    /** Creates one content-defined SkeletonMelee summon at an explicit cell. */
    public AuthoritativeCombatTarget? CreateSkeletonSummon(
        uint levelId,
        uint seed,
        int positionX,
        int positionY,
        uint entityId,
        bool multiplayer = false,
        int direction = 0)
        => CreateSkeletonSummon(
            levelId,
            new DeterministicLegacyRandom(seed),
            positionX,
            positionY,
            entityId,
            multiplayer,
            direction);

    /**
     * Creates a SkeletonMelee summon while advancing the supplied native LCG.
     * The overload is used by the authoritative tick so a single-player
     * summon leaves the process stream after the same native initialization
     * draws as InitializeSpawnedMonster.
     */
    public AuthoritativeCombatTarget? CreateSkeletonSummon(
        uint levelId,
        DeterministicLegacyRandom random,
        int positionX,
        int positionY,
        uint entityId,
        bool multiplayer = false,
        int direction = 0)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (entityId == 0)
            throw new ArgumentOutOfRangeException(nameof(entityId));
        if (direction is < 0 or >= 8)
            throw new ArgumentOutOfRangeException(nameof(direction));
        if (!world.ContainsLevel(levelId) || levelId > int.MaxValue)
            return null;

        var definitions = monsterTypes.GetAvailableForDungeonLevel((int)levelId)
            .Where(IsSkeletonSummonCandidate)
            .OrderBy(definition => definition.MonsterId)
            .ToArray();
        if (definitions.Length == 0)
            return null;

        // Native GetRandomSkeletonTypeIndex consumes one GenerateRnd draw
        // before InitializeSpawnedMonster captures the selected type.
        var summonSeed = random.State;
        var definition = definitions[random.RandomIntLessThan(definitions.Length)];
        var initialAnimation = ConsumeNativeInitialAnimationRandom(random, definition);
        var hitPointRange = checked(
            definition.MaximumHitPoints - definition.MinimumHitPoints + 1);
        var hitPoints = random.RandomIntLessThan(hitPointRange)
            + definition.MinimumHitPoints;
        var monsterItemSeed = unchecked((uint)random.AdvanceRndSeed());
        var monsterAiSeed = unchecked((uint)random.AdvanceRndSeed());
        // AttackDamage remains a legacy projection until the combat target
        // consumes native min/max damage at attack time. Keep its historical
        // seed-derived value without adding a non-native RNG draw.
        var damageRange = checked(
            definition.MaximumDamage - definition.MinimumDamage + 1);
        var attackDamage = definition.MinimumDamage
            + (int)((summonSeed / 0x100U) % (uint)damageRange);
        return CreateCombatTarget(
            new AuthoritativeMonsterSpawn(
                definition,
                levelId,
                positionX,
                positionY,
                hitPoints,
                attackDamage),
            0,
            random.State,
            entityId,
            multiplayer,
            direction,
            monsterItemSeed: monsterItemSeed,
            monsterAiSeed: monsterAiSeed,
            initialStandAnimationFrame: initialAnimation.Frame,
            initialStandAnimationTick: initialAnimation.Tick);
    }

    private static bool IsSkeletonSummonCandidate(
        AuthoritativeMonsterTypeDefinition definition)
        => definition.SkeletonSummonCandidate
            ?? (definition.NativeAi == "SkeletonMelee"
                || (definition.SkeletonMeleeDelayChanceBase > 0
                    && definition.SkeletonMeleeDelayChanceIntelligenceScale > 0
                    && definition.SkeletonMeleeAttackChanceBase > 0
                    && definition.SkeletonMeleeAttackChanceIntelligenceScale > 0));

    private bool IsOrdinarySpawnCandidate(
        AuthoritativeMonsterTypeDefinition definition)
        => spawnPolicies?.IsOrdinary(definition)
            ?? AuthoritativeMonsterSpawnPolicyCatalog.IsLegacyOrdinary(definition);

    private AuthoritativeCombatTarget CreateCombatTarget(
        AuthoritativeMonsterSpawn spawn,
        int index,
        uint seed,
        uint firstEntityId,
        bool multiplayer,
        int direction = 0,
        uint monsterItemSeed = 0,
        uint monsterAiSeed = 0,
        int initialStandAnimationFrame = 0,
        int initialStandAnimationTick = 0,
        bool preserveSinglePlayerHealth = false,
        int? intelligenceOverride = null)
    {
        var dropDefinition = monsterCatalog?.TryGetDropDefinition(
            spawn.Definition.MonsterId,
            out var resolvedDrop) == true
            ? resolvedDrop
            : null;
        AuthoritativeItemState? dropState = null;
        var dropSeed = 0U;
        var dropEntityId = 0U;
        var dropPrice = 0U;
        if (dropDefinition?.HasDrop == true) {
            if (itemCatalog is null)
                throw new InvalidDataException(
                    "Procedural monster drops require an authoritative item catalog.");
            dropSeed = spawn.MonsterItemSeed != 0
                ? spawn.MonsterItemSeed
                : monsterItemSeed != 0
                    ? monsterItemSeed
                    : CreateDropSeed(seed, spawn.Definition.MonsterId, index);
            dropState = dropDefinition.DropUniqueItemId != 0
                ? itemCatalog.GenerateUnique(
                    dropDefinition.DropUniqueItemId,
                    dropSeed,
                    dropDefinition.ItemLevel)
                : itemCatalog.GenerateDrop(
                    dropDefinition.DropItemId,
                    dropSeed,
                    dropDefinition.ItemLevel,
                    dropDefinition.OnlyGood,
                    spellBooks: spellBooks,
                    gameMode: multiplayer
                        ? DiabloGameMode.Multiplayer
                        : DiabloGameMode.SinglePlayer);
            var monsterEntityId = checked(firstEntityId + (uint)index);
            dropEntityId = monsterEntityId >= FirstGeneratedMonsterEntityId
                ? checked(FirstGeneratedDropEntityId
                    + (monsterEntityId - FirstGeneratedMonsterEntityId))
                : checked(FirstGeneratedDropEntityId + (uint)index);
            dropPrice = checked((uint)Math.Max(0, dropState.Value));
        }

        var hitPoints = ResolveInitialMonsterHealth(
            spawn.HitPoints,
            multiplayer,
            preserveSinglePlayerHealth);
        var maximumHitPoints = ResolveInitialMonsterHealth(
            spawn.Definition.MaximumHitPoints,
            multiplayer,
            preserveSinglePlayerHealth);
        return new AuthoritativeCombatTarget(
            checked(firstEntityId + (uint)index),
            spawn.PositionX,
            spawn.PositionY,
            hitPoints,
            spawn.Definition.ArmorClass,
            maximumHitPoints,
            spawn.LevelId,
            spawn.Definition.MonsterId,
            dropEntityId,
            dropSeed,
            dropPrice,
            dropState,
            attackDamage: spawn.AttackDamage,
            aggroRange: spawn.Definition.AggroRange,
            monsterLevel: spawn.Definition.MonsterLevel,
            fireResistance: spawn.Definition.FireResistance,
            lightningResistance: spawn.Definition.LightningResistance,
            magicResistance: spawn.Definition.MagicResistance,
            acidResistance: spawn.Definition.AcidResistance,
            stoneCurseImmune: spawn.Definition.StoneCurseImmune,
            undead: spawn.Definition.Undead,
            berserkImmune: spawn.Definition.BerserkImmune,
            minDamage: spawn.Definition.MinimumDamage,
            maxDamage: spawn.Definition.MaximumDamage,
            minDamageSpecial: spawn.Definition.MinimumSpecialDamage,
            maxDamageSpecial: spawn.Definition.MaximumSpecialDamage,
            demon: spawn.Definition.Demon,
            animal: spawn.Definition.Animal,
            unique: dropDefinition?.MonsterUnique ?? false,
            diablo: spawn.Definition.Diablo || (dropDefinition?.MonsterDiablo ?? false),
            deathAnimationTicks: spawn.Definition.DeathAnimationTicks,
            experienceReward: spawn.Definition.ExperienceReward,
            toHit: spawn.Definition.ToHit,
            toHitSpecial: spawn.Definition.ToHitSpecial,
            nativeAi: spawn.Definition.NativeAi,
            intelligence: intelligenceOverride ?? spawn.Definition.Intelligence,
            gargoyleSpecialActive: spawn.Definition.NativeAi == "Gargoyle",
            knockback: spawn.Definition.Knockback,
            direction: direction,
            monsterItemSeed: spawn.MonsterItemSeed != 0
                ? spawn.MonsterItemSeed
                : monsterItemSeed,
            monsterAiSeed: spawn.MonsterAiSeed != 0
                ? spawn.MonsterAiSeed
                : monsterAiSeed,
            initialStandAnimationFrame: spawn.InitialStandAnimationFrame != 0
                ? spawn.InitialStandAnimationFrame
                : initialStandAnimationFrame,
            initialStandAnimationTick: spawn.InitialStandAnimationTick != 0
                ? spawn.InitialStandAnimationTick
                : initialStandAnimationTick);
    }

    /** Native InitMonster halves ordinary single-player monster health. */
    private static int ResolveInitialMonsterHealth(
        int health,
        bool multiplayer,
        bool preserveSinglePlayerHealth)
        => multiplayer || preserveSinglePlayerHealth
            ? health
            : Math.Max(health / 2, 1);

    private static uint CreateDropSeed(uint seed, uint monsterId, int index)
    {
        var mixed = seed
            ^ unchecked(monsterId * 0x9E3779B9U)
            ^ unchecked((uint)(index + 1) * 0x85EBCA6BU);
        return mixed == 0 ? 1U : mixed;
    }

    private static uint CreateRuntimeSeed(uint seed, uint salt)
    {
        var mixed = DeterministicHash.Mix32(
            seed ^ unchecked(salt * 0x9E3779B9U));
        return mixed == 0 ? 1U : mixed;
    }

    /** Native InitMonster consumes stand animation timing before rolling health. */
    private static (int Tick, int Frame) ConsumeNativeInitialAnimationRandom(
        DeterministicLegacyRandom random,
        AuthoritativeMonsterTypeDefinition definition)
    {
        var tick = random.Next(definition.StandAnimationTicksPerFrame - 1);
        var frame = random.Next(definition.StandAnimationFrames - 1);
        return (tick, frame);
    }

}

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
    public const int NativeOrdinaryLevelMonsterCapacity = 190;

    /** Reserved world-item range for drops attached to generated monsters. */
    public const uint FirstGeneratedDropEntityId = 0x40000000;

    private readonly AuthoritativeWorld world;
    private readonly AuthoritativeMonsterTypeCatalog monsterTypes;
    private readonly AuthoritativeLevelGenerationCatalog levelGeneration;
    private readonly AuthoritativeItemCatalog? itemCatalog;
    private readonly AuthoritativeMonsterCatalog? monsterCatalog;
    private readonly AuthoritativeSpellBookCatalog? spellBooks;
    private readonly AuthoritativeUniqueMonsterCatalog? uniqueMonsters;

    public AuthoritativeMonsterSpawnPlanner(
        AuthoritativeWorld world,
        AuthoritativeMonsterTypeCatalog monsterTypes,
        AuthoritativeLevelGenerationCatalog levelGeneration,
        AuthoritativeItemCatalog? itemCatalog = null,
        AuthoritativeMonsterCatalog? monsterCatalog = null,
        AuthoritativeSpellBookCatalog? spellBooks = null,
        AuthoritativeUniqueMonsterCatalog? uniqueMonsters = null)
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

        var candidates = monsterTypes.GetAvailableForDungeonLevel((int)levelId);
        if (maximumMonsters == 0 || candidates.Count == 0)
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
            NativeOrdinaryLevelMonsterCapacity);
        if (spawnCount == 0)
            return [];

        var random = new AuthoritativeLegacyRandom(seed ^ levelId);
        Shuffle(positions, random);
        var orderedCandidates = candidates.ToArray();
        Shuffle(orderedCandidates, random);
        var spawns = new AuthoritativeMonsterSpawn[spawnCount];
        for (var index = 0; index < spawnCount; index++) {
            var definition = orderedCandidates[index % orderedCandidates.Length];
            var direction = random.Next(8);
            var hitPoints = random.Next(
                checked(definition.MaximumHitPoints - definition.MinimumHitPoints + 1))
                + definition.MinimumHitPoints;
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
                multiplayer))
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
        uint? stableMonsterId = null)
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
            direction);
        if (uniqueMonsters?.Definitions.Any(uniqueDefinition =>
                uniqueDefinition.EntityId == 0
                && (uniqueDefinition.MonsterId == 0
                    || uniqueDefinition.MonsterId == definition.MonsterId)
                && (uniqueDefinition.NativeAi.Length == 0
                    || string.Equals(
                        uniqueDefinition.NativeAi,
                        definition.NativeAi,
                        StringComparison.Ordinal))) == true) {
            target.IsUnique = true;
            uniqueMonsters.ApplyToGenerated([target]);
        }
        return target;
    }

    /** Creates one content-defined SkeletonMelee summon at an explicit cell. */
    public AuthoritativeCombatTarget? CreateSkeletonSummon(
        uint levelId,
        uint seed,
        int positionX,
        int positionY,
        uint entityId,
        bool multiplayer = false)
    {
        if (entityId == 0)
            throw new ArgumentOutOfRangeException(nameof(entityId));
        if (!world.ContainsLevel(levelId) || levelId > int.MaxValue)
            return null;

        var definitions = monsterTypes.GetAvailableForDungeonLevel((int)levelId)
            .Where(IsSkeletonSummonCandidate)
            .OrderBy(definition => definition.MonsterId)
            .ToArray();
        if (definitions.Length == 0)
            return null;

        var definition = definitions[(int)(seed % (uint)definitions.Length)];
        var hitPointRange = checked(
            definition.MaximumHitPoints - definition.MinimumHitPoints + 1);
        var damageRange = checked(
            definition.MaximumDamage - definition.MinimumDamage + 1);
        var hitPoints = definition.MinimumHitPoints
            + (int)((seed / (uint)definitions.Length) % (uint)hitPointRange);
        var attackDamage = definition.MinimumDamage
            + (int)((seed / 0x100U) % (uint)damageRange);
        return CreateCombatTarget(
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
            multiplayer);
    }

    private static bool IsSkeletonSummonCandidate(
        AuthoritativeMonsterTypeDefinition definition)
        => definition.SkeletonSummonCandidate
            ?? (definition.NativeAi == "SkeletonMelee"
                || (definition.SkeletonMeleeDelayChanceBase > 0
                    && definition.SkeletonMeleeDelayChanceIntelligenceScale > 0
                    && definition.SkeletonMeleeAttackChanceBase > 0
                    && definition.SkeletonMeleeAttackChanceIntelligenceScale > 0));

    private AuthoritativeCombatTarget CreateCombatTarget(
        AuthoritativeMonsterSpawn spawn,
        int index,
        uint seed,
        uint firstEntityId,
        bool multiplayer,
        int direction = 0)
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
            dropSeed = CreateDropSeed(seed, spawn.Definition.MonsterId, index);
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

        var hitPoints = ResolveInitialMonsterHealth(spawn.HitPoints, multiplayer);
        var maximumHitPoints = ResolveInitialMonsterHealth(
            spawn.Definition.MaximumHitPoints,
            multiplayer);
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
            intelligence: spawn.Definition.Intelligence,
            knockback: spawn.Definition.Knockback,
            direction: spawn.Direction);
    }

    /** Native InitMonster halves ordinary single-player monster health. */
    private static int ResolveInitialMonsterHealth(int health, bool multiplayer)
        => multiplayer ? health : Math.Max(health / 2, 1);

    private static uint CreateDropSeed(uint seed, uint monsterId, int index)
    {
        var mixed = seed
            ^ unchecked(monsterId * 0x9E3779B9U)
            ^ unchecked((uint)(index + 1) * 0x85EBCA6BU);
        return mixed == 0 ? 1U : mixed;
    }

    private static void Shuffle<T>(T[] values, AuthoritativeLegacyRandom random)
    {
        for (var index = values.Length - 1; index > 0; index--) {
            var swapIndex = random.Next(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }
}

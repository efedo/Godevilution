namespace Devilution.Diablo;

/** One deterministic server-owned monster placement produced from validated content. */
public sealed record AuthoritativeMonsterSpawn(
    AuthoritativeMonsterTypeDefinition Definition,
    uint LevelId,
    int PositionX,
    int PositionY,
    int HitPoints,
    int AttackDamage);

/**
 * Selects level-valid monster types and walkable cells without depending on
 * presentation, transport, or native map code. Static encounter rows remain
 * authoritative until a complete native map-generation replacement is ready.
 */
public sealed class AuthoritativeMonsterSpawnPlanner
{
    /** Reserved entity range shared with the authoritative combat processor. */
    public const uint FirstGeneratedMonsterEntityId = 0x10000000;

    private readonly AuthoritativeWorld world;
    private readonly AuthoritativeMonsterTypeCatalog monsterTypes;
    private readonly AuthoritativeLevelGenerationCatalog levelGeneration;

    public AuthoritativeMonsterSpawnPlanner(
        AuthoritativeWorld world,
        AuthoritativeMonsterTypeCatalog monsterTypes,
        AuthoritativeLevelGenerationCatalog levelGeneration)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.monsterTypes = monsterTypes
            ?? throw new ArgumentNullException(nameof(monsterTypes));
        this.levelGeneration = levelGeneration
            ?? throw new ArgumentNullException(nameof(levelGeneration));
    }

    /** Plans bounded deterministic placements for one validated dungeon level. */
    public IReadOnlyList<AuthoritativeMonsterSpawn> Plan(
        uint levelId,
        uint seed,
        bool multiplayer,
        int maximumMonsters)
    {
        if (maximumMonsters < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumMonsters));
        if (!world.ContainsLevel(levelId))
            throw new InvalidDataException($"Monster spawn level {levelId} is not defined.");
        if (levelId > int.MaxValue)
            throw new InvalidDataException($"Monster spawn level {levelId} exceeds native level bounds.");

        var rule = levelGeneration.GetRequired(levelId);
        var candidates = monsterTypes.GetAvailableForDungeonLevel((int)levelId);
        if (maximumMonsters == 0 || candidates.Count == 0)
            return [];

        var level = world.Levels.Single(candidate => candidate.LevelId == levelId);
        var positions = Enumerable.Range(0, checked(level.Width * level.Height))
            .Select(cell => (X: cell % level.Width, Y: cell / level.Width))
            .Where(position => level.IsWalkable(position.X, position.Y))
            .ToArray();
        var baseCount = positions.Length / rule.MonsterDensityDivisor;
        var requestedCount = multiplayer
            ? checked(baseCount * rule.MultiplayerMonsterMultiplierPercent / 100)
            : baseCount;
        var spawnCount = Math.Min(maximumMonsters, requestedCount);
        if (spawnCount == 0)
            return [];

        var random = new AuthoritativeLegacyRandom(seed ^ levelId);
        Shuffle(positions, random);
        var orderedCandidates = candidates.ToArray();
        Shuffle(orderedCandidates, random);
        var spawns = new AuthoritativeMonsterSpawn[spawnCount];
        for (var index = 0; index < spawnCount; index++) {
            var definition = orderedCandidates[index % orderedCandidates.Length];
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
                attackDamage);
        }
        return spawns;
    }

    /**
     * Converts planned placements into combat targets accepted by the
     * authoritative simulation. Generated targets intentionally have no item
     * drops or unique flags until those catalogs are part of spawn planning.
     */
    public IReadOnlyList<AuthoritativeCombatTarget> PlanCombatTargets(
        uint levelId,
        uint seed,
        bool multiplayer,
        int maximumMonsters)
    {
        var spawns = Plan(levelId, seed, multiplayer, maximumMonsters);
        return spawns
            .Select((spawn, index) => new AuthoritativeCombatTarget(
                checked(FirstGeneratedMonsterEntityId + (uint)index),
                spawn.PositionX,
                spawn.PositionY,
                spawn.HitPoints,
                spawn.Definition.ArmorClass,
                spawn.Definition.MaximumHitPoints,
                spawn.LevelId,
                spawn.Definition.MonsterId,
                attackDamage: spawn.AttackDamage,
                monsterLevel: spawn.Definition.MonsterLevel,
                fireResistance: spawn.Definition.FireResistance,
                lightningResistance: spawn.Definition.LightningResistance,
                magicResistance: spawn.Definition.MagicResistance,
                acidResistance: spawn.Definition.AcidResistance,
                stoneCurseImmune: spawn.Definition.StoneCurseImmune,
                undead: spawn.Definition.Undead,
                minDamage: spawn.Definition.MinimumDamage,
                maxDamage: spawn.Definition.MaximumDamage,
                minDamageSpecial: spawn.Definition.MinimumSpecialDamage,
                maxDamageSpecial: spawn.Definition.MaximumSpecialDamage,
                demon: spawn.Definition.Demon,
                animal: spawn.Definition.Animal,
                knockback: spawn.Definition.Knockback))
            .ToArray();
    }

    private static void Shuffle<T>(T[] values, AuthoritativeLegacyRandom random)
    {
        for (var index = values.Length - 1; index > 0; index--) {
            var swapIndex = random.Next(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }
}

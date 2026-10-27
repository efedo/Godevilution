namespace Devilution.Diablo;

/**
 * Batch 1.6: what is bound once per level lives in one entry per level instead of a
 * registry per feature. Entity-keyed state (doors, barrels, traps, actions) keeps its maps.
 */
public sealed partial class DiabloWorldState
{
    private sealed class LevelState
    {
        public AuthoritativeNativePopulationBindings? Population, CatacombsDoorOwner;
        public AuthoritativeLevel? CathedralDoorLevel, CatacombsDoorLevel;
        // Immutable admitted Gargoyle phases, in native order; set once phases are configured.
        public IReadOnlyList<AuthoritativeNativeCatacombsMonsterPhase>? MonsterPhaseBindings;
        public AuthoritativeMonsterMissileCatalog? RangedMissiles;
        public DiabloNativeFloorEntry? FloorEntry;
    }

    // Insertion-ordered, as each former per-feature dictionary was.
    private readonly Dictionary<uint, LevelState> levelStates = [];

    private LevelState LevelStateFor(uint levelId)
    {
        if (!levelStates.TryGetValue(levelId, out var state))
            levelStates.Add(levelId, state = new());
        return state;
    }

    private LevelState? FindLevelState(uint levelId) => levelStates.GetValueOrDefault(levelId);

    /** The levels whose selected field is set, in insertion order. */
    private IEnumerable<KeyValuePair<uint, T>> LevelValues<T>(Func<LevelState, T?> select) where T : class
        => levelStates.Where(entry => select(entry.Value) is not null)
            .Select(entry => KeyValuePair.Create(entry.Key, select(entry.Value)!));

    /** Sets a once-per-level field, as the former per-feature dictionaries' Add did. */
    private static void Bind<T>(ref T? field, T value) where T : class
        => field = field is null ? value : throw new ArgumentException($"The level already has its {typeof(T).Name}.");

    /** A detached floor only brings levels the live world has not bound yet. */
    private void RequireAdoptableLevelStates(DiabloWorldState floor)
    {
        if (floor.levelStates.Keys.Any(levelStates.ContainsKey))
            throw new InvalidOperationException("A prepared floor's level state collides with live state.");
    }

    private void AdoptLevelStates(DiabloWorldState floor)
    {
        RequireAdoptableLevelStates(floor);
        foreach (var entry in floor.levelStates)
            levelStates.Add(entry.Key, entry.Value);
    }
}

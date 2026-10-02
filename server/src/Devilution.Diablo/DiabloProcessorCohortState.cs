namespace Devilution.Diablo;

/** Exact allocator cursors owned by non-combat world command processing. */
public sealed record DiabloWorldCommandAllocatorState(
    uint NextWorldItemEntityId,
    uint NextAuthoredProjectileEntityId,
    uint NextGeneratedMonsterEntityId);

/** Exact allocator cursors owned by combat command processing. */
public sealed record DiabloCombatCommandAllocatorState(
    uint NextWorldItemEntityId,
    uint NextMonsterEntityId,
    uint NextProjectileEntityId);

/** Detached infrastructure state required to rebuild authoritative world projection. */
public sealed record DiabloWorldInfrastructureState(
    int FallbackWidth,
    int FallbackHeight,
    IReadOnlyList<int> BlockedCells,
    IReadOnlyList<int> MissileBlockedCells,
    IReadOnlyList<uint> ActiveWorldMutationIds,
    ulong? LastPortalTick,
    IReadOnlyList<DiabloWorldGeometryLevelState>? EffectiveLevels = null);

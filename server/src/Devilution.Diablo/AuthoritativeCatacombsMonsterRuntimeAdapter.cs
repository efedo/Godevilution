namespace Devilution.Diablo;

/** Catacombs admission layered over shared detached runtime records. */
internal sealed class AuthoritativeCatacombsMonsterRuntimeAdapterResult
{
    internal AuthoritativeCatacombsMonsterRuntimeAdapterResult(
        int depth,
        uint levelId,
        AuthoritativeCathedralMonsterRuntimeAdapterResult sharedRuntime,
        IReadOnlyList<AuthoritativeNativeCatacombsMonsterPhase> gargoylePhases)
    {
        if (depth is < 5 or > 8 || levelId == 0)
            throw new InvalidDataException("Catacombs actor admission requires an explicit L5-L8 identity.");
        ArgumentNullException.ThrowIfNull(sharedRuntime);
        ArgumentNullException.ThrowIfNull(gargoylePhases);
        Depth = depth;
        LevelId = levelId;
        SharedRuntime = sharedRuntime;
        GargoylePhases = Array.AsReadOnly(gargoylePhases.Select(phase => phase with { }).ToArray());
        foreach (var phase in GargoylePhases)
            phase.ValidateShape();
    }

    public int Depth { get; }
    public uint LevelId { get; }
    public AuthoritativeCathedralMonsterRuntimeAdapterResult SharedRuntime { get; }
    public IReadOnlyList<AuthoritativeNativeCatacombsMonsterPhase> GargoylePhases { get; }
}

/** Explicit L5-L8 runtime admission. This never broadens the public C1 adapter policy. */
internal static class AuthoritativeCatacombsMonsterRuntimeAdapter
{
    private const int NativeGargoyleAi = 12;
    private const int NativeGargoyleSpecialMode = 7;
    private const int NativeStandMode = 0;
    private const uint NativeAllowSpecial = 1U << 2;
    private const int NativeGolemType = AuthoritativeCathedralPopulationParameters.GolemNativeType;

    internal static AuthoritativeCatacombsMonsterRuntimeAdapterResult AdaptWithBindings(
        int depth,
        uint levelId,
        AuthoritativeCathedralThemeRoomState state,
        AuthoritativeCathedralMonsterRuntimeCatalog runtimeCatalog,
        string metadataFingerprint,
        IReadOnlyList<AuthoritativeCathedralMonsterRuntimeBinding> bindings,
        IReadOnlyList<AuthoritativeCathedralRawMonsterDefinition> populationMonsters,
        DiabloDifficulty difficulty = DiabloDifficulty.Normal,
        IReadOnlyList<AuthoritativeCathedralUniqueDefinition>? uniqueDefinitions = null,
        AuthoritativeMonsterTypeCatalog? monsterTypeCatalog = null,
        IReadOnlyDictionary<int, string>? uniqueIdentities = null)
    {
        if (depth is < 5 or > 8)
            throw new ArgumentOutOfRangeException(nameof(depth));
        if (levelId == 0)
            throw new ArgumentOutOfRangeException(nameof(levelId));
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(runtimeCatalog);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(populationMonsters);

        var expectedSlots = state.ActiveMonsterIds.Take(state.ActiveMonsterCount).ToArray();
        if (bindings.Count != expectedSlots.Length
            || bindings.Where((binding, index) => binding is null
                || binding.NativeSlot != expectedSlots[index]
                || binding.LevelId != levelId).Any())
            throw new InvalidDataException("Catacombs monster bindings must preserve the active order and one explicit floor identity.");

        var shared = AuthoritativeCathedralMonsterRuntimeAdapter.AdaptCatacombsWithBindings(
            depth, state, runtimeCatalog, metadataFingerprint, bindings, populationMonsters,
            difficulty, uniqueDefinitions, monsterTypeCatalog, uniqueIdentities);
        var phases = new List<AuthoritativeNativeCatacombsMonsterPhase>();
        foreach (var record in shared.Records)
        {
            var initial = record.InitialState;
            if (initial.Ai == NativeGargoyleAi || initial.Mode == NativeGargoyleSpecialMode)
            {
                if (record.Target is null || record.UnsupportedReason != AuthoritativeCathedralMonsterRuntimeUnsupportedReason.None
                    || initial.Ai != NativeGargoyleAi || initial.Mode != NativeGargoyleSpecialMode
                    || initial.UniqueType != byte.MaxValue || (initial.Flags & NativeAllowSpecial) == 0
                    || initial.AnimCurrentFrame != 0 || initial.AnimTickCounter != 0
                    || record.Initialization.NativeSpecialAttackFrameCount is < 1 or > sbyte.MaxValue
                    || record.Initialization.NativeSpecialAttackTicksPerFrame is < 1 or > sbyte.MaxValue)
                    throw new NotSupportedException($"Catacombs Gargoyle slot {record.Binding.NativeSlot} is not the verified ordinary frozen-statue state.");
                phases.Add(new AuthoritativeNativeCatacombsMonsterPhase(
                    record.Binding.NativeSlot,
                    record.Binding.EntityId,
                    levelId,
                    initial.Type,
                    metadataFingerprint,
                    record.Initialization.NativeSpecialAttackFrameCount,
                    record.Initialization.NativeSpecialAttackTicksPerFrame,
                    0,
                    0,
                    AuthoritativeNativeCatacombsMonsterPhaseState.Frozen));
                continue;
            }

            if (record.Target is not null)
            {
                if (record.UnsupportedReason != AuthoritativeCathedralMonsterRuntimeUnsupportedReason.None
                    || record.Target.IsPlayerMinion)
                    throw new NotSupportedException($"Catacombs monster slot {record.Binding.NativeSlot} has unsupported live state.");
                continue;
            }

            if (!IsAllowedReservation(record))
                throw new NotSupportedException($"Catacombs monster slot {record.Binding.NativeSlot} cannot be activated: {record.UnsupportedReason}.");
        }

        return new AuthoritativeCatacombsMonsterRuntimeAdapterResult(depth, levelId, shared, phases);
    }

    private static bool IsAllowedReservation(AuthoritativeCathedralMonsterRuntimeRecord record)
    {
        var state = record.InitialState;
        if (record.UnsupportedReason == AuthoritativeCathedralMonsterRuntimeUnsupportedReason.DormantGolem)
            return state.Type == NativeGolemType && state.Mode == NativeStandMode;
        return record.UnsupportedReason == AuthoritativeCathedralMonsterRuntimeUnsupportedReason.PreSpawnSkeleton
            && state.Mode == NativeStandMode && state.Tile == default && state.Future == default
            && state.Old == default && state.Goal == 1 && state.GoalVar1 == 0
            && state.GoalVar2 == 0 && state.GoalVar3 == 0 && state.UniqueType == byte.MaxValue;
    }
}

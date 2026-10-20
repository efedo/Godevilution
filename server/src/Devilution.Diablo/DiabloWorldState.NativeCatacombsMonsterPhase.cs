namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    private readonly Dictionary<uint, AuthoritativeNativeCatacombsMonsterPhase> nativeCatacombsMonsterPhases = [];
    private readonly Dictionary<uint, IReadOnlyList<uint>> nativeCatacombsMonsterPhaseOrders = [];
    private readonly HashSet<uint> nativeCatacombsMonsterPhaseLevels = [];
    private readonly Dictionary<uint, IReadOnlyList<AuthoritativeNativeCatacombsMonsterPhase>> nativeCatacombsMonsterPhaseBindings = [];

    internal IReadOnlyList<AuthoritativeNativeCatacombsMonsterPhase> CaptureNativeCatacombsMonsterPhases()
        => Array.AsReadOnly(nativeCatacombsMonsterPhaseOrders.OrderBy(entry => entry.Key)
            .SelectMany(entry => entry.Value)
            .Where(nativeCatacombsMonsterPhases.ContainsKey)
            .Select(entityId => nativeCatacombsMonsterPhases[entityId] with { }).ToArray());

    internal IReadOnlyList<AuthoritativeNativeCatacombsMonsterPhase> CaptureNativeCatacombsMonsterPhases(uint levelId)
        => Array.AsReadOnly(nativeCatacombsMonsterPhaseOrders.GetValueOrDefault(levelId, Array.Empty<uint>())
            .Where(nativeCatacombsMonsterPhases.ContainsKey)
            .Select(entityId => nativeCatacombsMonsterPhases[entityId] with { }).ToArray());

    internal void ConfigureNativeCatacombsMonsterPhases(
        AuthoritativeCatacombsMonsterRuntimeAdapterResult admission,
        IReadOnlyList<AuthoritativeNativeCatacombsMonsterPhase>? saved = null)
    {
        ArgumentNullException.ThrowIfNull(admission);
        if (nativeCatacombsMonsterPhaseLevels.Contains(admission.LevelId))
            throw new InvalidOperationException("Catacombs monster phases are configured once per owned floor.");

        var expected = admission.GargoylePhases;
        var actual = saved ?? expected;
        if (actual.Count != expected.Count)
            throw new InvalidDataException("Catacombs phase state omitted or added a generated Gargoyle.");

        var detached = new Dictionary<uint, AuthoritativeNativeCatacombsMonsterPhase>();
        var ordered = new uint[expected.Count];
        for (var index = 0; index < expected.Count; index++)
        {
            var phase = actual[index];
            var bound = expected[index];
            phase.ValidateShape();
            if (phase with {
                    FrameIndex = bound.FrameIndex,
                    TickCounter = bound.TickCounter,
                    State = bound.State,
                } != bound
                || phase.LevelId != admission.LevelId
                || nativeCatacombsMonsterPhases.ContainsKey(phase.EntityId)
                || !detached.TryAdd(phase.EntityId, phase with { }))
                throw new InvalidDataException("Catacombs phase order or immutable monster binding changed.");

            var admissionRecord = admission.SharedRuntime.Records.SingleOrDefault(record =>
                    record.Binding.NativeSlot == phase.NativeSlot
                    && record.Binding.EntityId == phase.EntityId
                    && record.Binding.LevelId == phase.LevelId
                    && record.InitialState.Type == phase.NativeTypeId);
            if (admissionRecord is null)
                throw new InvalidDataException("A Catacombs phase is not bound to the exact admitted native actor.");

            if (CombatTargets.TryGetValue(phase.EntityId, out var target))
            {
                ValidateNativeCatacombsPhaseTarget(target, phase);
                if (target.NativeInitialization!.InitialState != admissionRecord.InitialState
                    || target.NativeInitialization.MetadataFingerprint != admission.SharedRuntime.MetadataFingerprint)
                    throw new InvalidDataException("A Catacombs phase actor changed its admitted native initialization.");
                if ((phase.State is AuthoritativeNativeCatacombsMonsterPhaseState.Frozen
                    or AuthoritativeNativeCatacombsMonsterPhaseState.Playing)
                    && (target.IsMoving || target.HitPointsQ6 <= 0
                        || target.PositionX != target.NativeInitialization!.InitialState.Tile.X
                        || target.PositionY != target.NativeInitialization.InitialState.Tile.Y
                        || target.FuturePositionX != target.PositionX || target.FuturePositionY != target.PositionY))
                    throw new InvalidDataException("An active native Gargoyle phase changed position or died without interruption.");
            }
            else if (phase.State is not (AuthoritativeNativeCatacombsMonsterPhaseState.Completed
                    or AuthoritativeNativeCatacombsMonsterPhaseState.Interrupted)
                || !Corpses.TryGetValue(phase.EntityId, out var corpse)
                || corpse.LevelId != phase.LevelId
                || !ContainsPosition(corpse.LevelId, corpse.PositionX, corpse.PositionY))
            {
                throw new InvalidDataException("A retained Catacombs phase has no exact live actor or terminal corpse on its bound floor.");
            }
            ordered[index] = phase.EntityId;
        }

        foreach (var phase in actual)
            nativeCatacombsMonsterPhases.Add(phase.EntityId, phase with { });
        nativeCatacombsMonsterPhaseOrders.Add(admission.LevelId, Array.AsReadOnly(ordered));
        nativeCatacombsMonsterPhaseLevels.Add(admission.LevelId);
        nativeCatacombsMonsterPhaseBindings.Add(admission.LevelId,
            Array.AsReadOnly(expected.Select(phase => phase with { }).ToArray()));
    }

    /** Validates mutable phase clocks against the complete immutable per-floor admission. */
    internal void ValidateNativeCatacombsMonsterPhases(AuthoritativeNativePopulationBindings owner)
    {
        if (owner.Family != AuthoritativeNativePopulationFamily.Catacombs
            || !ReferenceEquals(NativePopulationForLevel(owner.LevelId), owner)
            || !nativeCatacombsMonsterPhaseLevels.Contains(owner.LevelId)
            || !nativeCatacombsMonsterPhaseOrders.TryGetValue(owner.LevelId, out var order)
            || !nativeCatacombsMonsterPhaseBindings.TryGetValue(owner.LevelId, out var bindings)
            || !order.SequenceEqual(bindings.Select(phase => phase.EntityId))
            || nativeCatacombsMonsterPhases.Values.Count(phase => phase.LevelId == owner.LevelId) != bindings.Count)
            throw new InvalidDataException("Catacombs phase coverage lost its exact per-floor population owner.");
        foreach (var bound in bindings) {
            if (!nativeCatacombsMonsterPhases.TryGetValue(bound.EntityId, out var phase))
                throw new InvalidDataException("A Catacombs Gargoyle lost its retained phase clock.");
            phase.ValidateShape();
            if (phase with {
                    FrameIndex = bound.FrameIndex, TickCounter = bound.TickCounter, State = bound.State,
                } != bound
                || phase.MetadataFingerprint != owner.MonsterMetadataFingerprint)
                throw new InvalidDataException("A Catacombs phase changed its immutable native identity.");
            if (CombatTargets.TryGetValue(phase.EntityId, out var target)) {
                ValidateNativeCatacombsPhaseTarget(target, phase);
                if (phase.State is AuthoritativeNativeCatacombsMonsterPhaseState.Frozen
                        or AuthoritativeNativeCatacombsMonsterPhaseState.Playing
                    && (target.HitPointsQ6 <= 0 || target.IsMoving
                        || target.PositionX != target.NativeInitialization!.InitialState.Tile.X
                        || target.PositionY != target.NativeInitialization.InitialState.Tile.Y
                        || target.FuturePositionX != target.PositionX || target.FuturePositionY != target.PositionY))
                    throw new InvalidDataException("An active Catacombs Gargoyle phase moved or died without interruption.");
            } else if (phase.State is not (AuthoritativeNativeCatacombsMonsterPhaseState.Completed
                        or AuthoritativeNativeCatacombsMonsterPhaseState.Interrupted)
                || !Corpses.TryGetValue(phase.EntityId, out var corpse) || corpse.LevelId != owner.LevelId
                || !ContainsPosition(corpse.LevelId, corpse.PositionX, corpse.PositionY)) {
                throw new InvalidDataException("A Catacombs Gargoyle phase lost its live actor or terminal corpse.");
            }
        }
    }

    /** A frozen Gargoyle consumes the waking player hit without applying damage. */
    internal bool TryWakeNativeCatacombsGargoyle(AuthoritativeCombatTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.HitPointsQ6 <= 0
            || !TryGetNativeCatacombsPhase(target, out var phase)
            || phase.State != AuthoritativeNativeCatacombsMonsterPhaseState.Frozen)
            return false;
        var playing = phase.Start();
        if (playing == phase)
            return false;
        nativeCatacombsMonsterPhases[target.EntityId] = playing;
        return true;
    }

    /** Native proximity wake requires an active monster and a strict distance threshold. */
    internal bool TryWakeNativeCatacombsGargoyleByProximity(
        AuthoritativeCombatTarget target, int distance, int maximumDistance)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (distance < 0)
            throw new ArgumentOutOfRangeException(nameof(distance));
        if (maximumDistance <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumDistance));
        return target.ActiveForTicks > 0 && distance < maximumDistance
            && TryStartNativeCatacombsGargoylePhase(target);
    }

    /** Mirrors FollowTheLeader for the current follower; call at its active-list turn. */
    internal bool ApplyNativeCatacombsGargoyleFollowerWakeBeforeAi(AuthoritativeCombatTarget follower)
    {
        ArgumentNullException.ThrowIfNull(follower);
        if (!CombatTargets.TryGetValue(follower.EntityId, out var currentFollower)
            || !ReferenceEquals(currentFollower, follower)
            || follower.LeaderRelation != AuthoritativeMonsterLeaderRelation.Leashed
            || follower.LeaderEntityId == 0 || follower.LeaderEntityId == follower.EntityId
            || !CombatTargets.TryGetValue(follower.LeaderEntityId, out var leader)
            || leader.LevelId != follower.LevelId
            || IsNativeCatacombsSpecialMelee(leader))
            return false;

        return TryStartNativeCatacombsGargoylePhase(follower);
    }

    /** Mirrors GroupUnity's converse Gargoyle wake for this member's current turn. */
    internal bool ApplyNativeCatacombsGargoyleLeaderWakeAfterGroupUnity(AuthoritativeCombatTarget member)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (!CombatTargets.TryGetValue(member.EntityId, out var currentMember)
            || !ReferenceEquals(currentMember, member)
            || member.LeaderRelation != AuthoritativeMonsterLeaderRelation.Leashed
            || member.LeaderEntityId == 0 || member.LeaderEntityId == member.EntityId
            || !CombatTargets.TryGetValue(member.LeaderEntityId, out var leader)
            || leader.LevelId != member.LevelId
            || !nativeCatacombsMonsterPhases.ContainsKey(leader.EntityId)
            || !TryGetNativeCatacombsPhase(leader, out var phase)
            || phase.State != AuthoritativeNativeCatacombsMonsterPhaseState.Frozen)
            return false;

        return TryStartNativeCatacombsGargoylePhase(leader);
    }

    /** Holds AI for the locked statue and while its zero-damage Special animation plays. */
    internal bool AdvanceNativeCatacombsGargoyle(AuthoritativeCombatTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!TryGetNativeCatacombsPhase(target, out var phase))
            return false;
        if (phase.State == AuthoritativeNativeCatacombsMonsterPhaseState.Frozen)
            return true;
        if (phase.State != AuthoritativeNativeCatacombsMonsterPhaseState.Playing)
            return false;
        if (target.HitPointsQ6 <= 0 || target.IsMoving)
        {
            InterruptNativeCatacombsGargoyle(target.EntityId);
            return false;
        }

        var next = phase.Advance();
        nativeCatacombsMonsterPhases[target.EntityId] = next;
        return next.State == AuthoritativeNativeCatacombsMonsterPhaseState.Playing;
    }

    internal void InterruptNativeCatacombsGargoyle(uint entityId)
    {
        if (nativeCatacombsMonsterPhases.TryGetValue(entityId, out var phase))
            nativeCatacombsMonsterPhases[entityId] = phase.Interrupt();
    }

    internal AuthoritativeNativeCatacombsMonsterPhase? NativeCatacombsMonsterPhase(uint entityId)
        => nativeCatacombsMonsterPhases.TryGetValue(entityId, out var phase) ? phase with { } : null;

    /** Lets the C2 tick reach frozen/playing animation before its normal inactive-AI skip. */
    internal bool HasNativeCatacombsMonsterPhase(uint entityId)
        => nativeCatacombsMonsterPhases.TryGetValue(entityId, out var phase)
            && phase.State is AuthoritativeNativeCatacombsMonsterPhaseState.Frozen
                or AuthoritativeNativeCatacombsMonsterPhaseState.Playing;

    private bool TryStartNativeCatacombsGargoylePhase(AuthoritativeCombatTarget target)
    {
        if (!TryGetNativeCatacombsPhase(target, out var phase)
            || phase.State != AuthoritativeNativeCatacombsMonsterPhaseState.Frozen)
            return false;
        nativeCatacombsMonsterPhases[target.EntityId] = phase.Start();
        return true;
    }

    private bool IsNativeCatacombsSpecialMelee(AuthoritativeCombatTarget target)
    {
        if (target.HitPointsQ6 <= 0)
            return false;

        if (nativeCatacombsMonsterPhases.ContainsKey(target.EntityId)
            && TryGetNativeCatacombsPhase(target, out var phase)
            && phase.State is (AuthoritativeNativeCatacombsMonsterPhaseState.Frozen
                or AuthoritativeNativeCatacombsMonsterPhaseState.Playing))
            return true;

        return target.NativeInitialization is { CatacombsDepth: >= 5 and <= 8 }
            && target.NativeMeleeCursor is { UsesSpecialAttack: true, HitRecovery: false };
    }

    private bool TryGetNativeCatacombsPhase(
        AuthoritativeCombatTarget target,
        out AuthoritativeNativeCatacombsMonsterPhase phase)
    {
        phase = null!;
        if (!nativeCatacombsMonsterPhases.TryGetValue(target.EntityId, out var candidate))
            return false;
        candidate.ValidateShape();
        ValidateNativeCatacombsPhaseTarget(target, candidate);
        phase = candidate;
        return true;
    }

    private static void ValidateNativeCatacombsPhaseTarget(
        AuthoritativeCombatTarget target,
        AuthoritativeNativeCatacombsMonsterPhase phase)
    {
        var initialization = target.NativeInitialization;
        if (initialization is null
            || target.EntityId != phase.EntityId || target.LevelId != phase.LevelId
            || initialization.MetadataFingerprint != phase.MetadataFingerprint
            || initialization.Binding.NativeSlot != phase.NativeSlot
            || initialization.Binding.NativeTypeId != phase.NativeTypeId
            || initialization.Binding.EntityId != phase.EntityId
            || initialization.Binding.LevelId != phase.LevelId
            || initialization.NativeSpecialAttackFrameCount != phase.FrameCount
            || initialization.NativeSpecialAttackTicksPerFrame != phase.TicksPerFrame
            || initialization.InitialState.Mode != 7 || initialization.InitialState.Ai != 12
            || initialization.InitialState.UniqueType != byte.MaxValue
            || (initialization.InitialState.Flags & (1U << 2)) == 0)
            throw new InvalidDataException("Catacombs phase no longer matches its bound native Gargoyle initialization.");
    }
}

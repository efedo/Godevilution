using Devilution.Engine;

namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    private Dictionary<uint, DiabloNativeTrapState> nativeTrapsByEntityId = [];

    internal bool HasMutatedNativeTraps
        => nativeTrapsByEntityId.Values.Any(state => state.Disposition != DiabloNativeTrapDisposition.Armed
            || IsNativeTrapTriggerReady(state.Facts));

    internal IReadOnlyList<DiabloNativeTrapState>? CaptureNativeTraps()
        => nativePopulationPolicy is null
            ? null
            : Array.AsReadOnly(nativeTrapsByEntityId.Values
                .OrderBy(value => value.Facts.LevelId).ThenBy(value => value.Facts.TrapObjectSlot).ToArray());

    internal void ConfigureNativeTraps(
        AuthoritativeNativePopulationBindings policy,
        int formatVersion,
        IReadOnlyList<DiabloNativeTrapState>? savedStates)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (!ReferenceEquals(NativePopulationForLevel(policy.LevelId), policy))
            throw new InvalidOperationException("Native trap state requires its configured immutable population owner.");

        var initial = policy.CreateInitialNativeTrapStates();
        if (formatVersion >= DiabloCohortState.NativeTrapFormatVersion) {
            if (savedStates is null || savedStates.Count != initial.Count)
                throw new InvalidDataException("Native trap cohorts require complete generated wall-trap lifecycle state.");
            var restored = new Dictionary<uint, DiabloNativeTrapState>();
            for (var index = 0; index < initial.Count; index++) {
                var actual = savedStates[index];
                actual.ValidateShape();
                if (actual.Facts != initial[index].Facts
                    || !restored.TryAdd(actual.Facts.TrapEntityId, actual))
                    throw new InvalidDataException("Native trap state differs from its exact generated trap/trigger link.");
            }
            foreach (var entry in restored)
                nativeTrapsByEntityId.Add(entry.Key, entry.Value);
        } else {
            if (savedStates is not null)
                throw new InvalidDataException("Older native cohorts cannot carry wall-trap lifecycle state.");
            foreach (var entry in initial)
                nativeTrapsByEntityId.Add(entry.Facts.TrapEntityId, entry);
        }

        ValidateNativeTrapState(policy);
    }

    internal bool IsNativeTrapTrigger(AuthoritativeWorldObject value)
        => TryGetNativeTrapFacts(value, out _);

    internal bool CanOperateNativeTrapTrigger(AuthoritativeWorldObject value)
    {
        if (!TryGetNativeTrapFacts(value, out var facts) || facts.Count == 0)
            return false;
        var rawType = checked((int)value.NativeObjectId);
        return rawType is 1 or 2 || (rawType is 5 or 6 or 7) && !value.Activated;
    }

    internal bool TryGetNativeTrapFacts(
        AuthoritativeWorldObject trigger,
        out IReadOnlyList<AuthoritativeCathedralNativeTrapFacts> facts)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        var nativePopulationPolicy = NativePopulationForLevel(trigger.LevelId);
        facts = Array.Empty<AuthoritativeCathedralNativeTrapFacts>();
        if (nativePopulationPolicy is null || trigger.LevelId != nativePopulationPolicy.LevelId
            || !Objects.TryGetValue(trigger.EntityId, out var current) || !ReferenceEquals(current, trigger)
            || !nativePopulationPolicy.TryGetNativeTrapFacts(trigger, out facts))
            return false;
        ValidateNativeTrapState(nativePopulationPolicy);
        return true;
    }

    /** Performs only the native disarm roll and state change; the caller still operates the target this command. */
    internal DiabloNativeTrapDisarmResult TryDisarmNativeTrapTrigger(
        AuthoritativePlayerState player,
        AuthoritativeWorldObject trigger,
        DeterministicLegacyRandom random)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(trigger);
        var nativePopulationPolicy = NativePopulationForLevel(trigger.LevelId);
        ArgumentNullException.ThrowIfNull(random);
        if (!CanOperateNativeTrapTrigger(trigger) || !trigger.TrapArmed
            || !player.HasAbility(DiabloPlayerAbilityKind.TrapDisarm)
            || player.Life <= 0 || player.LevelId != trigger.LevelId
            || DeterministicGridMetrics.ChebyshevDistance(
                trigger.PositionX, trigger.PositionY, player.PositionX, player.PositionY) > 1)
            return DiabloNativeTrapDisarmResult.NotApplicable;

        if (!TryGetNativeTrapFacts(trigger, out var facts))
            return DiabloNativeTrapDisarmResult.NotApplicable;
        var linkedStates = facts.Select(value => nativeTrapsByEntityId[value.TrapEntityId]).ToArray();
        if (linkedStates.Length == 0
            || linkedStates.Any(value => value.Disposition != DiabloNativeTrapDisposition.Armed
                || value.TrapVar4 != value.Facts.InitialTrapVar4 || !value.TriggerTrapArmed))
            return DiabloNativeTrapDisarmResult.NotApplicable;

        var chance = checked(2 * player.Attributes.Dexterity.Current - 5 * nativePopulationPolicy!.Depth);
        if (random.GenerateRnd(100) > chance)
            return DiabloNativeTrapDisarmResult.Failed;

        // Native TryDisarm consumes every active trap linked to this trigger.
        foreach (var value in linkedStates) {
            nativeTrapsByEntityId[value.Facts.TrapEntityId] = value with {
                TrapVar4 = 1,
                TriggerTrapArmed = false,
                Disposition = DiabloNativeTrapDisposition.Disarmed,
            };
        }
        trigger.TrapArmed = false;
        ValidateNativeTrapState(nativePopulationPolicy!);
        return DiabloNativeTrapDisarmResult.Disarmed;
    }

    /** Commits ready traps once, in native active-object order, before callers create the returned missiles. */
    internal IReadOnlyList<DiabloNativeTrapFiringRecord> ConsumeReadyNativeTraps(
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        var activeLevels = players.Select(player => player.LevelId).ToHashSet();
        var activeOwners = NativePopulationOwners.Where(policy => activeLevels.Contains(policy.LevelId)).ToArray();
        foreach (var policy in activeOwners)
            ValidateNativeTrapState(policy);

        var ready = nativeTrapsByEntityId.Values
            .Where(state => activeLevels.Contains(state.Facts.LevelId)
                && state.Disposition == DiabloNativeTrapDisposition.Armed
                && IsNativeTrapTriggerReady(state.Facts))
            .OrderBy(state => state.Facts.LevelId).ThenBy(state => state.Facts.ActiveObjectOrder)
            .ToArray();
        if (ready.Length == 0)
            return Array.Empty<DiabloNativeTrapFiringRecord>();

        var firing = new DiabloNativeTrapFiringRecord[ready.Length];
        for (var index = 0; index < ready.Length; index++) {
            var state = ready[index];
            var facts = state.Facts;
            var target = FindNativeTrapTarget(facts, players);
            firing[index] = new(facts.TrapEntityId, facts.TrapObjectSlot, facts.LevelId,
                facts.SourceX, facts.SourceY, target.X, target.Y, facts.MissileTypeId,
                (int)DeterministicGridDirections.Toward(facts.SourceX, facts.SourceY, target.X, target.Y));
        }

        // Validate every output first, then publish all one-shot owner mutations together.
        if (ready.Any(state => !Objects.TryGetValue(state.Facts.TriggerEntityId, out var trigger)
                || !Objects.ContainsKey(state.Facts.TrapEntityId)
                || !IsNativeTrapTriggerReady(trigger, state.Facts)))
            throw new InvalidDataException("A pending native wall trap lost its ready trigger before consumption.");
        foreach (var state in ready) {
            nativeTrapsByEntityId[state.Facts.TrapEntityId] = state with {
                TrapVar4 = 1,
                TriggerTrapArmed = false,
                Disposition = DiabloNativeTrapDisposition.Fired,
            };
            Objects[state.Facts.TriggerEntityId].TrapArmed = false;
        }
        foreach (var policy in activeOwners)
            ValidateNativeTrapState(policy);
        return Array.AsReadOnly(firing);
    }

    internal void ValidateNativeTrapState(AuthoritativeNativePopulationBindings policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (!ReferenceEquals(NativePopulationForLevel(policy.LevelId), policy))
            throw new InvalidDataException("Native trap lifecycle state has no matching immutable population owner.");

        var initial = policy.CreateInitialNativeTrapStates();
        if (nativeTrapsByEntityId.Values.Count(value => value.Facts.LevelId == policy.LevelId) != initial.Count)
            throw new InvalidDataException("Native trap lifecycle state omits a supported generated trap.");
        var states = nativeTrapsByEntityId.Values.Where(value => value.Facts.LevelId == policy.LevelId).OrderBy(value => value.Facts.TrapObjectSlot).ToArray();
        for (var index = 0; index < initial.Count; index++) {
            var state = states[index];
            state.ValidateShape();
            if (state.Facts != initial[index].Facts
                || !nativeTrapsByEntityId.TryGetValue(state.Facts.TrapEntityId, out var bySlot)
                || bySlot != state
                || !Objects.TryGetValue(state.Facts.TrapEntityId, out var trap)
                || trap.LevelId != state.Facts.LevelId || trap.NativeObjectId != (uint)state.Facts.TrapNativeTypeId
                || trap.PositionX != state.Facts.SourceX || trap.PositionY != state.Facts.SourceY
                || !Objects.TryGetValue(state.Facts.TriggerEntityId, out var trigger)
                || trigger.LevelId != state.Facts.LevelId || trigger.NativeObjectId != (uint)state.Facts.TriggerNativeTypeId
                || trigger.PositionX != state.Facts.TriggerX || trigger.PositionY != state.Facts.TriggerY
                || trigger.TrapArmed != state.TriggerTrapArmed)
                throw new InvalidDataException("Native wall-trap state conflicts with its exact generated objects.");
        }

        foreach (var group in states.GroupBy(value => value.Facts.TriggerObjectSlot)) {
            if (group.Select(value => value.Disposition).Distinct().Skip(1).Any()
                || group.Select(value => value.TriggerTrapArmed).Distinct().Skip(1).Any())
                throw new InvalidDataException("One native trigger cannot be partially disarmed or fired.");
        }
    }

    private bool IsNativeTrapTriggerReady(AuthoritativeWorldObject? trigger,
        AuthoritativeCathedralNativeTrapFacts facts)
    {
        if (trigger is null || trigger.LevelId != facts.LevelId || trigger.EntityId != facts.TriggerEntityId)
            return false;
        return facts.TriggerNativeTypeId is 1 or 2
            ? trigger.CathedralDoor is { Status: not AuthoritativeCathedralDoorStatus.Closed }
            : trigger.Activated;
    }

    private bool IsNativeTrapTriggerReady(AuthoritativeCathedralNativeTrapFacts facts)
        => Objects.TryGetValue(facts.TriggerEntityId, out var trigger)
            && IsNativeTrapTriggerReady(trigger, facts);

    private (int X, int Y) FindNativeTrapTarget(
        AuthoritativeCathedralNativeTrapFacts facts,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        // Native PointsInRectangle(target, 1) is row-major; reverse iteration checks
        // the bottom-right cell first and falls back to the trigger tile.
        for (var dy = 1; dy >= -1; dy--) {
            for (var dx = 1; dx >= -1; dx--) {
                var x = facts.TriggerX + dx;
                var y = facts.TriggerY + dy;
                if ((uint)x >= AuthoritativeCathedralThemeRoomState.Width
                    || (uint)y >= AuthoritativeCathedralThemeRoomState.Width)
                    continue;
                if (players.Any(player => player.EntityId != 0 && player.Life > 0
                    && player.LevelId == facts.LevelId
                    && (player.PositionX == x && player.PositionY == y
                        || player.IsMoving && player.FuturePositionX == x && player.FuturePositionY == y)))
                    return (x, y);
            }
        }
        return (facts.TriggerX, facts.TriggerY);
    }
}

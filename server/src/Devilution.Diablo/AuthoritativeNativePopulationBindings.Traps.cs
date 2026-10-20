namespace Devilution.Diablo;

internal sealed partial class AuthoritativeNativePopulationBindings
{
    private const int NativeLeftWallTrap = 53;
    private const int NativeRightWallTrap = 54;
    private const int NativeArrowMissile = 0;

    /** Full source-order trap bindings; unsupported links are intentionally absent. */
    internal IReadOnlyList<AuthoritativeCathedralNativeTrapFacts> CreateInitialNativeTrapFacts()
    {
        var active = InitialState.ActiveObjectIds.Take(InitialState.ActiveObjectCount).ToArray();
        var result = new List<AuthoritativeCathedralNativeTrapFacts>();
        for (var order = 0; order < active.Length; order++) {
            var trap = InitialState.ObjectSlots[active[order]];
            if (trap.Type is not (NativeLeftWallTrap or NativeRightWallTrap)
                || trap.Var1 is < 0 or >= AuthoritativeCathedralThemeRoomState.Width
                || trap.Var2 is < 0 or >= AuthoritativeCathedralThemeRoomState.Width
                || trap.Var3 != NativeArrowMissile || trap.Var4 != 0)
                continue;

            var cell = trap.Var2 * AuthoritativeCathedralThemeRoomState.Width + trap.Var1;
            var reference = InitialState.ObjectRefs[cell];
            if (reference == 0)
                continue;
            var triggerSlot = checked((int)(Math.Abs((long)reference) - 1));
            if (!objectEntitiesBySlot.TryGetValue(triggerSlot, out var triggerEntityId))
                continue;
            var trigger = InitialState.ObjectSlots[triggerSlot];
            if (!IsSupportedNativeTrapTriggerType(trigger)
                || !trigger.TrapFlag
                || trigger.Position.X != trap.Var1 || trigger.Position.Y != trap.Var2
                || !HasSupportedNativeTrapLink(InitialState, trigger))
                continue;

            var facts = new AuthoritativeCathedralNativeTrapFacts(
                trap.Slot,
                objectEntitiesBySlot[trap.Slot],
                trap.Type,
                LevelId,
                order,
                trap.Position.X,
                trap.Position.Y,
                trap.RndSeed,
                trap.Var1,
                trap.Var2,
                trap.Var3,
                trap.Var4,
                trigger.Slot,
                triggerEntityId,
                trigger.Type,
                trigger.Position.X,
                trigger.Position.Y,
                trigger.TrapFlag);
            facts.ValidateShape();
            result.Add(facts);
        }
        return Array.AsReadOnly(result.OrderBy(value => value.TrapObjectSlot).ToArray());
    }

    internal IReadOnlyList<DiabloNativeTrapState> CreateInitialNativeTrapStates()
        => Array.AsReadOnly(CreateInitialNativeTrapFacts()
            .Select(facts => new DiabloNativeTrapState(facts, facts.InitialTrapVar4,
                facts.InitialTriggerTrapArmed, DiabloNativeTrapDisposition.Armed))
            .ToArray());

    internal bool TryGetNativeTrapFacts(
        AuthoritativeWorldObject trigger,
        out IReadOnlyList<AuthoritativeCathedralNativeTrapFacts> facts)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        facts = Array.Empty<AuthoritativeCathedralNativeTrapFacts>();
        ValidateObject(trigger);
        if (!objectRows.TryGetValue(trigger.EntityId, out var rawTrigger)
            || !IsSupportedNativeTrapTriggerType(rawTrigger))
            return false;
        var linked = CreateInitialNativeTrapFacts()
            .Where(value => value.TriggerObjectSlot == rawTrigger.Slot)
            .OrderBy(value => value.ActiveObjectOrder)
            .ToArray();
        if (linked.Length == 0 || !HasSupportedNativeTrapLink(InitialState, rawTrigger))
            return false;
        facts = Array.AsReadOnly(linked);
        return true;
    }

    internal AuthoritativeCathedralNativeTrapFacts GetNativeTrapFacts(int trapObjectSlot)
        => CreateInitialNativeTrapFacts().SingleOrDefault(value => value.TrapObjectSlot == trapObjectSlot)
            ?? throw new InvalidDataException("The requested wall trap has no supported immutable trigger link.");

    internal static bool IsSupportedNativeTrapTriggerType(
        AuthoritativeCathedralPopulationObjectState value)
        => (value.Type is 1 or 2) && value.DoorFlag
            || (value.Type is 5 or 6 or 7) && !value.DoorFlag;

    /** A trigger is supported only when every wall trap linked to it is an L1 Arrow trap. */
    internal static bool HasSupportedNativeTrapLink(
        AuthoritativeCathedralThemeRoomState state,
        AuthoritativeCathedralPopulationObjectState trigger)
    {
        if (!trigger.TrapFlag || !IsSupportedNativeTrapTriggerType(trigger)
            || trigger.Position.X is < 0 or >= AuthoritativeCathedralThemeRoomState.Width
            || trigger.Position.Y is < 0 or >= AuthoritativeCathedralThemeRoomState.Width)
            return false;
        var reference = state.ObjectRefs[trigger.Position.Y * AuthoritativeCathedralThemeRoomState.Width
            + trigger.Position.X];
        if (reference == 0 || Math.Abs((long)reference) - 1 != trigger.Slot)
            return false;

        var linked = state.ActiveObjectIds.Take(state.ActiveObjectCount)
            .Select(slot => state.ObjectSlots[slot])
            .Where(value => value.Type is NativeLeftWallTrap or NativeRightWallTrap
                && value.Var1 == trigger.Position.X && value.Var2 == trigger.Position.Y)
            .ToArray();
        return linked.Length != 0 && linked.All(value =>
            value.Var3 == NativeArrowMissile && value.Var4 == 0
            && (uint)value.Position.X < AuthoritativeCathedralThemeRoomState.Width
            && (uint)value.Position.Y < AuthoritativeCathedralThemeRoomState.Width);
    }
}

namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    private readonly Dictionary<uint, AuthoritativeNativeInitialSpecialStand> nativeInitialSpecialStand = [];

    internal IReadOnlyList<AuthoritativeNativeInitialSpecialStand> CaptureNativeInitialSpecialStand()
        => nativeInitialSpecialStand.Values.OrderBy(value => value.LevelId)
            .ThenBy(value => value.NativeSlot).ToArray();

    private void ConfigureNativeInitialSpecialStand(AuthoritativeCathedralNativePopulationPolicy policy,
        IReadOnlyList<AuthoritativeNativeInitialSpecialStand>? saved = null)
    {
        var expected = policy.InitialSpecialStandActors;
        var actual = saved ?? expected;
        if (actual.Count != expected.Count)
            throw new InvalidDataException("Initial native emergence omitted a generated skeleton.");
        foreach (var pair in actual.Zip(expected)) {
            pair.First.ValidateShape();
            CombatTargets.TryGetValue(pair.First.EntityId, out var target);
            if (pair.First with {
                    FrameIndex = pair.Second.FrameIndex, TickCounter = pair.Second.TickCounter,
                    Completed = pair.Second.Completed,
                } != pair.Second
                || target is null && !Corpses.ContainsKey(pair.First.EntityId)
                || target?.NativeInitialization?.SpecialAttackEffectFrame is not (null or 0))
                throw new InvalidDataException("Initial native emergence changed its exact generated identity or sound predicate.");
            if (!pair.First.Completed && (target is null || target.IsMoving || target.HitPointsQ6 <= 0
                || target.PositionX != target.NativeInitialization!.InitialState.Tile.X
                || target.PositionY != target.NativeInitialization.InitialState.Tile.Y
                || target.FuturePositionX != target.PositionX || target.FuturePositionY != target.PositionY))
                throw new InvalidDataException("An emerging native skeleton moved or died without interrupting its phase.");
        }
        foreach (var value in actual)
            nativeInitialSpecialStand.Add(value.EntityId, value);
    }

    /** The admitted source skeletons have no SpecialStand sound frame (native metadata is zero). */
    internal bool AdvanceNativeInitialSpecialStand(AuthoritativeCombatTarget target)
    {
        if (!nativeInitialSpecialStand.TryGetValue(target.EntityId, out var phase) || phase.Completed)
            return false;
        if (target.IsPetrified && target.HitPointsQ6 > 0)
            return true;
        if (target.HitPointsQ6 <= 0 || target.NativeMeleeCursor is { HitRecovery: true }) {
            InterruptNativeInitialSpecialStand(target.EntityId);
            return false;
        }
        var next = phase.Advance();
        nativeInitialSpecialStand[target.EntityId] = next;
        return !next.Completed;
    }

    internal void InterruptNativeInitialSpecialStand(uint entityId)
    {
        if (nativeInitialSpecialStand.TryGetValue(entityId, out var phase) && !phase.Completed)
            nativeInitialSpecialStand[entityId] = phase with {
                FrameIndex = 0, TickCounter = 0, Completed = true,
            };
    }
}

namespace Devilution.Diablo;

public sealed partial record DiabloCohortState
{
    private void ValidateNativeTrapShape()
    {
        if (FormatVersion != NativeTrapFormatVersion) {
            if (NativeTraps is not null || Projectiles.Any(value => value?.NativeTrapArrow is not null))
                throw new InvalidDataException("Native trap state cannot be down-converted to an older cohort.");
            return;
        }
        if (NativeTraps is null || NativeTraps.Any(value => value is null))
            throw new InvalidDataException("Native trap cohorts require complete lifecycle records.");
        foreach (var state in NativeTraps) {
            if (state.Facts is null)
                throw new InvalidDataException("Native trap state is missing its immutable facts.");
            state.ValidateShape();
        }
        if (CathedralItems?.Levels.Count != 1)
            throw new InvalidDataException("Native traps require exactly one retained native level.");
        if (!NativeTraps.SequenceEqual(NativeTraps.OrderBy(value => value.Facts.TrapObjectSlot))
            || NativeTraps.Select(value => value.Facts.TrapObjectSlot).Distinct().Count() != NativeTraps.Count
            || NativeTraps.Select(value => value.Facts.TrapEntityId).Distinct().Count() != NativeTraps.Count
            || Objects.Select(value => value.EntityId).Distinct().Count() != Objects.Count)
            throw new InvalidDataException("Native trap identities must be unique and sorted by raw slot.");
        var objects = Objects.ToDictionary(value => value.EntityId);
        foreach (var state in NativeTraps) {
            state.ValidateShape();
            var facts = state.Facts;
            if (CathedralItems is null || facts.LevelId != CathedralItems.Levels.Single().LevelId
                || !objects.TryGetValue(facts.TrapEntityId, out var trap)
                || trap.NativeObjectId != facts.TrapNativeTypeId || trap.LevelId != facts.LevelId
                || trap.PositionX != facts.SourceX || trap.PositionY != facts.SourceY
                || !objects.TryGetValue(facts.TriggerEntityId, out var trigger)
                || trigger.NativeObjectId != facts.TriggerNativeTypeId || trigger.LevelId != facts.LevelId
                || trigger.PositionX != facts.TriggerX || trigger.PositionY != facts.TriggerY
                || trigger.TrapArmed != state.TriggerTrapArmed)
                throw new InvalidDataException("Native trap state conflicts with its retained source or trigger.");
        }
        var arrows = Projectiles.Where(value => value.NativeTrapArrow is not null).ToArray();
        if (arrows.Select(value => value.SourceEntityId).Distinct().Count() != arrows.Length)
            throw new InvalidDataException("A native one-shot trap cannot own duplicate live arrows.");
        foreach (var projectile in arrows) {
            var arrow = projectile.NativeTrapArrow!;
            arrow.ValidateShape(projectile);
            var owner = NativeTraps.SingleOrDefault(value => value.Facts.TrapObjectSlot == arrow.NativeObjectSlot
                && value.Facts.TrapEntityId == projectile.SourceEntityId);
            if (owner is null || owner.Disposition != DiabloNativeTrapDisposition.Fired
                || projectile.LevelId != owner.Facts.LevelId
                || projectile.TargetEntityId != owner.Facts.TriggerEntityId
                || projectile.StartX != owner.Facts.SourceX || projectile.StartY != owner.Facts.SourceY
                || Math.Abs((long)projectile.TargetX - owner.Facts.TriggerX) > 1
                || Math.Abs((long)projectile.TargetY - owner.Facts.TriggerY) > 1)
                throw new InvalidDataException("A native trap arrow changed its fired owner, source or frozen aim.");
        }
    }
}

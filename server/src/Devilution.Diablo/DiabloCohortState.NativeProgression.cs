namespace Devilution.Diablo;

/** First-entry generation contexts and mutable initial emergence for an explicit native run. */
public sealed record DiabloNativeProgressionCheckpoint(
    string RunFingerprint,
    IReadOnlyList<DiabloNativeFloorEntry> Entries,
    IReadOnlyList<AuthoritativeNativeInitialSpecialStand> InitialSpecialStand);

public sealed partial record DiabloCohortState
{
    private void ValidateNativeWalkPathShape()
    {
        var hasPaths = Participants.Any(participant => participant.Player.NativeWalkPath is not null);
        if (FormatVersion != NativeDepthProgressionFormatVersion) {
            if (hasPaths)
                throw new InvalidDataException("Native destination walks require the depth-progression cohort format.");
            return;
        }

        var ownedLevels = new HashSet<uint> { 0, 1 };
        foreach (var entry in NativeProgression!.Entries)
            ownedLevels.Add(entry.LevelId);
        foreach (var participant in Participants) {
            if (participant.Player.NativeWalkPath is not { } path)
                continue;
            if (path.LevelId != participant.Player.LevelId || !ownedLevels.Contains(path.LevelId)
                || path.Directions is null || path.Directions.Count is < 1 or > AuthoritativeNativePlayerPathfinder.MaximumPathLength
                || path.Directions.Any(direction => direction is < 1 or > 8)
                || participant.Player.Life <= 0
                || participant.Player.CombatMode is DiabloPlayerCombatMode.Death
                    or DiabloPlayerCombatMode.NewLevel or DiabloPlayerCombatMode.Quit)
                throw new InvalidDataException("A native destination walk has an invalid owner, queue, or lifecycle.");
        }
    }

    private void ValidateNativeProgressionShape()
    {
        var nativeDepthProgression = FormatVersion == NativeDepthProgressionFormatVersion;
        if (FormatVersion != NativeProgressionFormatVersion && !nativeDepthProgression) {
            if (NativeProgression is not null)
                throw new InvalidDataException("Native progression cannot be down-converted to an older cohort.");
            return;
        }
        var run = NativeProgression
            ?? throw new InvalidDataException("A native progression cohort requires its explicit run checkpoint.");
        var allowedEntries = nativeDepthProgression ? 3 : 1;
        if (!AuthoritativeNativeMonsterInitialization.IsFingerprint(run.RunFingerprint)
            || run.Entries is null || run.InitialSpecialStand is null
            || run.Entries.Count > allowedEntries)
            throw new InvalidDataException("Native first-entry contexts require exact supported floor identities and unique history.");
        var expectedEntryLevels = Enumerable.Range(2, run.Entries.Count).Select(value => (uint)value).ToArray();
        if (!run.Entries.Select(entry => entry?.LevelId ?? 0U).SequenceEqual(expectedEntryLevels)
            || run.Entries.Any(entry => entry is null || entry.LevelId is < 2 or > 4 || entry.Hero is null
                || !AuthoritativeNativeMonsterInitialization.IsFingerprint(entry.GenerationFingerprint)
                || entry.InitialUniqueItemFlags is null
                || entry.InitialUniqueItemFlags.Any(flag => flag is < 0 or >= 128)
                || !entry.InitialUniqueItemFlags.SequenceEqual(entry.InitialUniqueItemFlags.Distinct().Order())))
            throw new InvalidDataException("Native first-entry contexts require exact supported floor identities and unique history.");
        var owned = new[] { 1U }.Concat(run.Entries.Select(entry => entry.LevelId)).ToArray();
        if (CathedralItems is null || CathedralDoors is null
            || !CathedralItems.Levels.Select(level => level.LevelId).SequenceEqual(owned)
            || !CathedralDoors.DoorLevels.Select(level => level.LevelId).SequenceEqual(owned))
            throw new InvalidDataException("A native run must retain every visited floor's item and door owners.");
        if (run.InitialSpecialStand.Any(value => value is null)
            || !run.InitialSpecialStand.SequenceEqual(run.InitialSpecialStand
                .OrderBy(value => value.LevelId).ThenBy(value => value.NativeSlot))
            || run.InitialSpecialStand.Select(value => value.EntityId).Distinct().Count() != run.InitialSpecialStand.Count
            || run.InitialSpecialStand.Select(value => (value.LevelId, value.NativeSlot)).Distinct().Count() != run.InitialSpecialStand.Count)
            throw new InvalidDataException("Initial native emergence requires unique, ordered floor/slot and entity identities.");
        foreach (var phase in run.InitialSpecialStand) {
            phase.ValidateShape();
            if (!owned.Contains(phase.LevelId))
                throw new InvalidDataException("Initial native emergence belongs to an unvisited floor.");
            var target = CombatTargets.FirstOrDefault(value => value.Monster.EntityId == phase.EntityId);
            if (target is null) {
                if (!phase.Completed || !Corpses.Any(value => value.EntityId == phase.EntityId && value.LevelId == phase.LevelId))
                    throw new InvalidDataException("Initial native emergence lost its retained actor or death disposition.");
                continue;
            }
            if (target.NativeInitialization is not { } native
                || native.InitialState.Mode != 11 || native.InitialState.Ai != 2
                || native.Binding.NativeSlot != phase.NativeSlot || native.Binding.LevelId != phase.LevelId
                || target.Monster.LevelId != phase.LevelId || native.SpecialAttackEffectFrame != 0
                || !phase.Completed && (target.Monster.Moving || target.Monster.HitPointsQ6 <= 0
                    || target.Monster.NativeMeleeCursor is not null
                    || target.Monster.PositionX != native.InitialState.Tile.X
                    || target.Monster.PositionY != native.InitialState.Tile.Y
                    || target.Monster.FuturePositionX != target.Monster.PositionX
                    || target.Monster.FuturePositionY != target.Monster.PositionY))
                throw new InvalidDataException("Initial native emergence conflicts with immutable provenance or its live phase.");
        }
        if (CombatTargets.Any(target => target.NativeInitialization?.InitialState.Mode == 11
            && !run.InitialSpecialStand.Any(phase => phase.EntityId == target.Monster.EntityId)))
            throw new InvalidDataException("A native run omitted an initial SpecialStand actor's live phase.");
    }
}

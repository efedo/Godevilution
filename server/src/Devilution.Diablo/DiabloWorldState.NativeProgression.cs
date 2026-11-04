namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    private AuthoritativeCathedralNativeProgression? nativeProgression;

    internal bool HasNativeProgression => nativeProgression is not null;
    internal string? NativeProgressionFingerprint => nativeProgression?.Fingerprint;
    internal Func<DiabloContentActivation>? NativeProgressionFreshActivation => nativeProgression?.FreshActivation;
    internal int NativeProgressionCohortFormatVersion
        => nativeProgression?.CohortFormatVersion
            ?? throw new InvalidOperationException("Native progression is not configured.");
    internal bool SupportsNativeTargetCellPathing
        => nativeProgression?.CohortFormatVersion is DiabloCohortState.NativeDepthProgressionFormatVersion
            or DiabloCohortState.NativeTownProgressionFormatVersion or DiabloCohortState.NativeCatacombsProgressionFormatVersion;
    internal int NativePopulationDepth(uint levelId)
        => NativePopulationForLevel(levelId)?.Depth
            ?? throw new InvalidDataException("Native gameplay requires its exact owned floor depth.");

    internal void ConfigureNativeProgression(AuthoritativeCathedralNativeProgression? definition)
    {
        if (definition is null) return;
        if (nativeProgression is not null || nativePopulationPolicy is null || cathedralItems is null)
            throw new InvalidOperationException("A native run is configured once after its initial population and item owner.");
        nativeProgression = definition;
    }

    internal bool CanEnterNativePopulationLevel(uint levelId)
        => AllowsNativePopulationLevel(levelId) || nativeProgression?.Supports(levelId) == true;

    /** A prepared first entry of either native family; exactly one admission is set. */
    internal sealed record NativeProgressionAdmission(NativeFloorAdmission? Cathedral, NativeCatacombsFloorAdmission? Catacombs)
    {
        internal DiabloWorldState Floor => Cathedral?.Floor ?? Catacombs!.Floor;
        internal uint FinalRandomState => Cathedral?.Build.Generated.Population.FinalRandomState
            ?? Catacombs!.Build.Ready.Request.FreshLevel.Population.FinalRandomState;
        internal uint LevelId => Cathedral?.Build.Policy.LevelId ?? Catacombs!.Build.Policy.Bindings.LevelId;
        internal string GenerationFingerprint => Cathedral?.Build.Policy.Fingerprint ?? Catacombs!.Build.Policy.Fingerprint;
    }

    internal (NativeProgressionAdmission Admission, DiabloNativeFloorEntry Entry)? PrepareNativeProgressionEntry(
        AuthoritativePlayerState player, uint levelId, IReadOnlyList<AuthoritativePlayerState> players,
        AuthoritativeItemCatalog items, AuthoritativeSpellCatalog spells, AuthoritativeCatacombsEntry? firstEntry = null,
        bool verifyAtCommit = true)
    {
        if (OwnsNativePopulationLevel(levelId) || levelId == 0 || nativeProgression is null)
            return null;
        if (nativeProgression.IsCatacombs(levelId)) {
            var catacombs = nativeProgression.PrepareCatacombs(levelId, player, cathedralItems!.UniqueItemFlags, firstEntry);
            return (new(null, PrepareNativeCatacombsFloorAdmission(catacombs.Build, items, spells,
                nativeProgression.CatacombsMissiles!, players, verifyAtCommit)), catacombs.Entry);
        }
        var prepared = nativeProgression.Prepare(levelId, player, cathedralItems!.UniqueItemFlags);
        // The new floor has a complete allowlist independent of carried item state.
        foreach (var item in player.Inventory.Select(value => value.State)
                     .Concat(player.Equipment.Select(value => value.State))
                     .Concat(player.Belt.Select(value => value.State))
                     .Concat(player.HeldItem is { } held ? [held.State] : [])) {
            if (!prepared.Build.Items.ItemBindings.TryGetValue(item.ItemIndex, out var binding)
                || binding.StableItemId != item.StableItemId)
                throw new InvalidDataException("A native floor entry cannot admit an unbound carried item.");
        }
        var door = nativeProgression.GetDoor(levelId, cathedralDoorCatalog!);
        return (new(PrepareNativeFloorAdmission(prepared.Build, door, items, spells, players, verifyAtCommit), null), prepared.Entry);
    }

    /** Enters each floor of this throwaway run, as live first entries would; see WarmNativeFloorEntries. */
    internal void WarmNativeFloorEntries(AuthoritativeItemCatalog items, AuthoritativeSpellCatalog spells)
    {
        var definition = nativeProgression
            ?? throw new InvalidOperationException("Native progression is not configured.");
        for (var levelId = 2U; definition.Supports(levelId); levelId++) {
            var entry = definition.CreateWarmupEntry(levelId, nativePopulationParticipantClassId ?? 0,
                cathedralItems!.UniqueItemFlags);
            if (definition.IsCatacombs(levelId)) {
                var catacombs = definition.BuildCatacombsEntry(entry);
                var floor = PrepareNativeCatacombsFloorAdmission(catacombs.Build, items, spells,
                    definition.CatacombsMissiles!, [], verifyAtCommit: false);
                CommitNativeProgressionEntry(new NativeProgressionAdmission(null, floor), catacombs.Entry);
                continue;
            }
            var cathedral = definition.BuildCathedralEntry(entry);
            var admission = PrepareNativeFloorAdmission(cathedral.Build,
                definition.GetDoor(levelId, cathedralDoorCatalog!), items, spells, [], verifyAtCommit: false);
            CommitNativeProgressionEntry(admission, cathedral.Entry);
        }
    }

    internal void CommitNativeProgressionEntry(NativeProgressionAdmission admission, DiabloNativeFloorEntry entry)
    {
        if (nativeProgression is null || FindLevelState(entry.LevelId)?.FloorEntry is not null
            || entry.LevelId != admission.LevelId
            || entry.GenerationFingerprint != admission.GenerationFingerprint)
            throw new InvalidOperationException("A native floor entry lost its configured run or immutable generation identity.");
        if (admission.Cathedral is { } cathedral)
            CommitNativeFloorAdmission(cathedral);
        else
            CommitNativeCatacombsFloorAdmission(admission.Catacombs!);
        Bind(ref LevelStateFor(entry.LevelId).FloorEntry, CopyFloorEntry(entry));
    }

    internal void CommitNativeProgressionEntry(NativeFloorAdmission admission, DiabloNativeFloorEntry entry)
        => CommitNativeProgressionEntry(new NativeProgressionAdmission(admission, null), entry);

    internal NativeProgressionRestoreContext CaptureNativeProgressionOwners(
        IReadOnlyList<AuthoritativeCatacombsNativePopulationBuild>? catacombs = null)
    {
        var floors = (catacombs ?? []).OrderBy(build => build.Policy.Bindings.LevelId).ToArray();
        if (!NativePopulationOwners.Where(owner => owner.Family == AuthoritativeNativePopulationFamily.Catacombs)
                .Select(owner => owner.LevelId).Order().SequenceEqual(floors.Select(build => build.Policy.Bindings.LevelId))
            || floors.Any(build => !ReferenceEquals(NativePopulationForLevel(build.Policy.Bindings.LevelId), build.Policy.Bindings)))
            throw new InvalidDataException("Native progression capture requires every admitted Catacombs floor build.");
        return new(NativePopulationOwners.Where(owner => owner.Family != AuthoritativeNativePopulationFamily.Catacombs)
                .Select(owner => owner.CathedralPolicy
                    ?? throw new InvalidDataException("Legacy progression cannot capture a different native family."))
                .OrderBy(policy => policy.LevelId).ToArray(), cathedralItems!.Profile,
            cathedralDoorCatalog!, World!.CaptureGeometryState()) { CatacombsFloors = floors };
    }

    internal void RestoreNativeProgressionOwners(NativeProgressionRestoreContext owners,
        AuthoritativeCathedralNativeProgression definition, DiabloCohortState state)
    {
        foreach (var bindings in owners.Owners.Select(policy => policy.Bindings)
                     .Concat(owners.CatacombsFloors.Select(build => build.Policy.Bindings))) {
            var levelId = bindings.LevelId;
            ConfigureNativeSarcophagi(bindings, state.FormatVersion,
                state.NativeSarcophagi!.Where(value => Objects[value.EntityId].LevelId == levelId).ToArray());
            ConfigureNativeBarrels(bindings, state.FormatVersion,
                state.NativeBarrels!.Where(value => Objects[value.EntityId].LevelId == levelId).ToArray());
            ConfigureNativeTraps(bindings, state.FormatVersion,
                state.NativeTraps!.Where(value => value.Facts.LevelId == levelId).ToArray());
            ConfigureNativeInitialSpecialStand(bindings,
                state.NativeProgression!.InitialSpecialStand.Where(value => value.LevelId == levelId).ToArray());
        }
        // Doors rebind from the restored objects and effective geometry; the rest restores from format 12.
        var catacombs = state.NativeCatacombs;
        foreach (var build in owners.CatacombsFloors) {
            var bindings = build.Policy.Bindings;
            ConfigureNativeCatacombsDoors(bindings, build.DoorCatalog,
                catacombs!.Doors.Where(value => value.LevelId == bindings.LevelId).ToArray());
            ConfigureNativeCatacombsMonsterPhases(build.Monsters,
                catacombs!.MonsterPhases.Where(value => value.LevelId == bindings.LevelId).ToArray());
            ConfigureNativeCatacombsRangedActions(bindings, definition.CatacombsMissiles!,
                catacombs.RangedActions.Where(value => value.LevelId == bindings.LevelId).ToArray());
            RestoreNativeCatacombsArrows(bindings, catacombs.Arrows.Where(value => value.LevelId == bindings.LevelId).ToArray());
        }
        nativePopulationLifecycleConfigured = true;
        // Item restoration follows this method; configure the run after that boundary.
        nativeProgression = definition;
        foreach (var entry in state.NativeProgression!.Entries)
            Bind(ref LevelStateFor(entry.LevelId).FloorEntry, CopyFloorEntry(entry));
    }

    private static DiabloNativeFloorEntry CopyFloorEntry(DiabloNativeFloorEntry entry)
        => entry with { InitialUniqueItemFlags = entry.InitialUniqueItemFlags.ToArray() };

    internal IReadOnlyList<DiabloNativeFloorEntry>? CaptureNativeFloorEntries()
        => nativeProgression is null ? null : LevelValues(state => state.FloorEntry).Select(entry => entry.Value)
            .OrderBy(entry => entry.LevelId).Select(CopyFloorEntry).ToArray();
}

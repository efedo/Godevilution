namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    private AuthoritativeCathedralNativeProgression? nativeProgression;
    private readonly Dictionary<uint, DiabloNativeFloorEntry> nativeFloorEntries = [];

    internal bool HasNativeProgression => nativeProgression is not null;
    internal string? NativeProgressionFingerprint => nativeProgression?.Fingerprint;
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

    internal (NativeFloorAdmission Admission, DiabloNativeFloorEntry Entry)? PrepareNativeProgressionEntry(
        AuthoritativePlayerState player, uint levelId, IReadOnlyList<AuthoritativePlayerState> players,
        AuthoritativeItemCatalog items, AuthoritativeSpellCatalog spells)
    {
        if (OwnsNativePopulationLevel(levelId) || levelId == 0 || nativeProgression is null)
            return null;
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
        var door = cathedralDoorCatalog!.Definitions.Single(value => value.LevelId == 1) with { LevelId = levelId };
        return (PrepareNativeFloorAdmission(prepared.Build, door, items, spells, players), prepared.Entry);
    }

    internal void CommitNativeProgressionEntry(NativeFloorAdmission admission, DiabloNativeFloorEntry entry)
    {
        if (nativeProgression is null || nativeFloorEntries.ContainsKey(entry.LevelId)
            || entry.LevelId != admission.Build.Policy.LevelId
            || entry.GenerationFingerprint != admission.Build.Policy.Fingerprint)
            throw new InvalidOperationException("A native floor entry lost its configured run or immutable generation identity.");
        CommitNativeFloorAdmission(admission);
        nativeFloorEntries.Add(entry.LevelId, entry with { InitialUniqueItemFlags = entry.InitialUniqueItemFlags.ToArray() });
    }

    internal NativeProgressionRestoreContext CaptureNativeProgressionOwners()
        => new(NativePopulationOwners.OrderBy(policy => policy.LevelId).ToArray(), cathedralItems!.Profile,
            cathedralDoorCatalog!, World!.CaptureGeometryState());

    internal void RestoreNativeProgressionOwners(NativeProgressionRestoreContext owners,
        AuthoritativeCathedralNativeProgression definition, DiabloCohortState state)
    {
        foreach (var policy in owners.Owners) {
            ConfigureNativeSarcophagi(policy, state.FormatVersion,
                state.NativeSarcophagi!.Where(value => Objects[value.EntityId].LevelId == policy.LevelId).ToArray());
            ConfigureNativeBarrels(policy, state.FormatVersion,
                state.NativeBarrels!.Where(value => Objects[value.EntityId].LevelId == policy.LevelId).ToArray());
            ConfigureNativeTraps(policy, state.FormatVersion,
                state.NativeTraps!.Where(value => value.Facts.LevelId == policy.LevelId).ToArray());
            ConfigureNativeInitialSpecialStand(policy,
                state.NativeProgression!.InitialSpecialStand.Where(value => value.LevelId == policy.LevelId).ToArray());
        }
        nativePopulationLifecycleConfigured = true;
        // Item restoration follows this method; configure the run after that boundary.
        nativeProgression = definition;
        foreach (var entry in state.NativeProgression!.Entries)
            nativeFloorEntries.Add(entry.LevelId, entry with { InitialUniqueItemFlags = entry.InitialUniqueItemFlags.ToArray() });
    }

    internal IReadOnlyList<DiabloNativeFloorEntry>? CaptureNativeFloorEntries()
        => nativeProgression is null ? null : nativeFloorEntries.Values.OrderBy(entry => entry.LevelId)
            .Select(entry => entry with { InitialUniqueItemFlags = entry.InitialUniqueItemFlags.ToArray() }).ToArray();
}

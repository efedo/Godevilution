namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    private AuthoritativeNativePopulationBindings? nativePopulationPolicy;
    private readonly Dictionary<uint, AuthoritativeNativePopulationBindings> nativePopulationPolicies = [];

    private AuthoritativeNativePopulationBindings? NativePopulationForLevel(uint levelId)
        => nativePopulationPolicies.GetValueOrDefault(levelId)
            ?? (nativePopulationPolicy?.LevelId == levelId ? nativePopulationPolicy : null);

    private AuthoritativeNativePopulationBindings? NativePopulationForEntity(uint entityId)
        => NativePopulationOwners.SingleOrDefault(policy => policy.BoundEntityIds.Contains(entityId));

    private IEnumerable<AuthoritativeNativePopulationBindings> NativePopulationOwners
        => nativePopulationPolicies.Count == 0
            ? nativePopulationPolicy is null ? [] : [nativePopulationPolicy]
            : nativePopulationPolicies.Values;

    private uint? nativePopulationParticipantClassId;
    private bool nativePopulationLifecycleConfigured;

    internal bool HasNativePopulation => nativePopulationPolicy is not null;

    internal void ConfigureNativePopulationPolicy(AuthoritativeCathedralNativePopulationPolicy? policy,
        AuthoritativeCathedralItemRuntimeProfile? items, string? monsterMetadataFingerprint,
        int nativeSarcophagusFormatVersion = 0,
        IReadOnlyList<DiabloNativeSarcophagusState>? nativeSarcophagusStates = null,
        IReadOnlyList<DiabloNativeBarrelState>? nativeBarrelStates = null,
        IReadOnlyList<DiabloNativeTrapState>? nativeTrapStates = null)
        => ConfigureNativePopulationBindings(policy?.Bindings, items, monsterMetadataFingerprint,
            nativeSarcophagusFormatVersion, nativeSarcophagusStates, nativeBarrelStates, nativeTrapStates);

    internal void ConfigureNativePopulationBindings(AuthoritativeNativePopulationBindings? policy,
        AuthoritativeCathedralItemRuntimeProfile? items, string? monsterMetadataFingerprint,
        int nativeSarcophagusFormatVersion = 0,
        IReadOnlyList<DiabloNativeSarcophagusState>? nativeSarcophagusStates = null,
        IReadOnlyList<DiabloNativeBarrelState>? nativeBarrelStates = null,
        IReadOnlyList<DiabloNativeTrapState>? nativeTrapStates = null)
    {
        if (policy is null)
            return;
        if (items is null || items.GenerationFingerprint != policy.Fingerprint
            || monsterMetadataFingerprint != policy.MonsterMetadataFingerprint
            || items.LevelProfiles.Count != 1 || items.LevelProfiles[0].LevelId != policy.LevelId)
            throw new InvalidDataException("The native population owner must bind the exact item and monster generation identities.");
        policy.ValidateItemProfile(items);
        if (nativePopulationPolicy is not null && !ReferenceEquals(nativePopulationPolicy, policy))
            throw new InvalidOperationException("A detached native world cannot change its immutable population owner.");
        if (nativePopulationLifecycleConfigured)
            throw new InvalidOperationException("Native population lifecycle state is configured once on a detached world.");
        nativePopulationPolicy ??= policy;
        nativePopulationPolicies.TryAdd(policy.LevelId, policy);
        ConfigureNativeSarcophagi(policy, nativeSarcophagusFormatVersion, nativeSarcophagusStates);
        ConfigureNativeBarrels(policy, nativeSarcophagusFormatVersion, nativeBarrelStates);
        ConfigureNativeTraps(policy, nativeSarcophagusFormatVersion, nativeTrapStates);
        ConfigureNativeInitialSpecialStand(policy);
        policy.ValidateWorld(this);
        nativePopulationLifecycleConfigured = true;
    }

    internal void ValidateNativePopulationObject(AuthoritativeWorldObject value)
    {
        var nativePopulationPolicy = NativePopulationForLevel(value.LevelId);
        if (nativePopulationPolicy is null || value.LevelId != nativePopulationPolicy.LevelId)
            throw new InvalidDataException("Explicitly refused native objects require the populated profile owner.");
        nativePopulationPolicy.ValidateObject(value);
        if (!ContainsPosition(value.LevelId, value.PositionX, value.PositionY))
            throw new InvalidDataException("A retained native object lies outside its owned level.");
    }

    internal bool CanOperateNativeObject(AuthoritativeWorldObject value)
        => NativePopulationForLevel(value.LevelId)?.CanOperate(value) ?? true;

    internal bool CanBreakWorldObject([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] AuthoritativeWorldObject? value)
    {
        var nativePopulationPolicy = NativePopulationForLevel(value?.LevelId ?? uint.MaxValue);
        if (value is null || !value.Breakable)
            return false;
        if (nativePopulationPolicy is null || value.LevelId != nativePopulationPolicy.LevelId)
            return true;
        nativePopulationPolicy.ValidateObject(value);
        return false;
    }

    /** Selects gameplay behavior only for the explicitly owned native level. */
    internal bool OwnsNativePopulationLevel(uint levelId)
        => NativePopulationForLevel(levelId) is not null;

    internal bool AllowsNativePopulationLevel(uint levelId)
        => !HasNativePopulation || levelId == 0 || OwnsNativePopulationLevel(levelId);

    private bool IsNativePopulationObjectSolid(uint levelId, int x, int y)
        => NativePopulationForLevel(levelId) is { } policy
            && policy.ObjectEntityAt(x, y) is { } entityId
            && policy.ObjectIsSolid(entityId)
            && !IsNativeBarrelBroken(entityId);

    private bool IsNativeBarrelBroken(uint entityId)
        => NativePopulationForEntity(entityId) is { } policy
            && policy.TryGetNativeBarrelObjectSlot(entityId, out _)
            && nativeBarrelsByEntityId.TryGetValue(entityId, out var state)
            && state.Broken;

    private bool TryGetNativePopulationMissileObject(uint levelId, int x, int y,
        out AuthoritativeWorldObject? result)
    {
        var nativePopulationPolicy = NativePopulationForLevel(levelId);
        result = null;
        if (nativePopulationPolicy is null || levelId != nativePopulationPolicy.LevelId
            || nativePopulationPolicy.ObjectEntityAt(x, y) is not { } entityId)
            return false;
        if (!Objects.TryGetValue(entityId, out var value))
            throw new InvalidDataException("A retained native object footprint lost its entity.");
        nativePopulationPolicy.ValidateObject(value);
        if (value.MissilePassable)
            return false;
        result = value;
        return true;
    }

    internal bool AllowsNativeMonsterActivation(uint levelId)
        => !OwnsNativePopulationLevel(levelId);

    internal void ValidateNativePopulationState(IEnumerable<AuthoritativePlayerState> participants)
    {
        if (nativePopulationPolicy is null)
            return;
        foreach (var policy in NativePopulationOwners)
            policy.ValidateWorld(this);
        if (participants.Any(player => !AllowsNativePopulationLevel(player.LevelId)
            || player.CharacterClassId != nativePopulationParticipantClassId))
            throw new InvalidDataException("The native route requires its bound base-game class in Town or an owned Cathedral level.");
        if (participants.Any(player => NativePopulationOwners.Any(policy => policy.ReservesEntityId(player.EntityId))))
            throw new InvalidDataException("A participant reused a dormant native monster entity reservation.");
    }
}

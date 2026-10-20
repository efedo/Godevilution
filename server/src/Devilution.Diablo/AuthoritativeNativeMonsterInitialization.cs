using System.Text.Json.Serialization;

namespace Devilution.Diablo;

/** Immutable native-slot/type to runtime identity binding, independent of mutable state. */
public sealed record AuthoritativeNativeMonsterBinding(
    int NativeSlot, int NativeTypeId, int NativeUniqueId,
    uint EntityId, uint ContentMonsterId, uint LevelId, string UniqueIdentity);

/**
 * Immutable native initialization provenance, not a second live monster state.
 * Mutable HP, seeds, occupancy and AI progress remain owned by CombatTarget.
 * InitialState retains loader-only fields until their live runtime ports exist.
 */
public sealed record AuthoritativeNativeMonsterInitialization(
    string MetadataFingerprint,
    AuthoritativeCathedralPopulationMonsterState InitialState,
    int Treasure,
    int SelectionRegion,
    int AttackEffectFrame,
    int SpecialAttackEffectFrame,
    AuthoritativeNativeMonsterBinding Binding,
    bool InitialAnimationPetrified)
{
    /** Native Bestiary animation frame count for graphic slot 2 (normal attack). */
    public int NativeAttackFrameCount { get; init; }

    /** Native Bestiary rate for graphic slot 2; AnimationInfo clamps nonpositive values to one. */
    public int NativeAttackTicksPerFrame { get; init; }

    /** Explicit additive family admission; legacy Cathedral initialization retains zero. */
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int CatacombsDepth { get; init; }

    /** C2-only Special graphic metadata; absent from legacy serialized initialization. */
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int NativeSpecialAttackFrameCount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int NativeSpecialAttackTicksPerFrame { get; init; }

    /** Native Bestiary frame count for graphic slot 3 (hit recovery). */
    public int NativeHitRecoveryFrameCount { get; init; }

    /** Native Bestiary rate for graphic slot 3; AnimationInfo clamps nonpositive values to one. */
    public int NativeHitRecoveryTicksPerFrame { get; init; }

    /** Native Bestiary frame count for graphic slot 1 (walk). */
    public int NativeWalkFrameCount { get; init; }

    /** Native Bestiary rate for graphic slot 1; AnimationInfo clamps nonpositive values to one. */
    public int NativeWalkTicksPerFrame { get; init; }

    public void Validate()
    {
        if (!IsFingerprint(MetadataFingerprint))
            throw new InvalidDataException("Native monster metadata requires a canonical SHA256 fingerprint.");
        ArgumentNullException.ThrowIfNull(InitialState);
        ArgumentNullException.ThrowIfNull(Binding);
        AuthoritativeCathedralThemeRoomState.ValidateMonsterState(InitialState);
        if (InitialState.Slot is < 0 or >= AuthoritativeCathedralThemeRoomState.MonsterCapacity
            || InitialState.Type is < 0 or > 137
            || InitialState.LevelType is < 0 or > 23
            || (InitialState.UniqueType is < 0 or > 99 && InitialState.UniqueType != 255)
            || Treasure is < 0 or > ushort.MaxValue
            || SelectionRegion is < 0 or > 7
            || AttackEffectFrame is < sbyte.MinValue or > sbyte.MaxValue
            || SpecialAttackEffectFrame is < sbyte.MinValue or > sbyte.MaxValue
            || NativeAttackFrameCount is < 0 or > sbyte.MaxValue
            || NativeAttackTicksPerFrame is < 0 or > sbyte.MaxValue
            || (CatacombsDepth != 0 && CatacombsDepth is < 5 or > 8)
            || CatacombsDepth == 0 && (NativeSpecialAttackFrameCount != 0 || NativeSpecialAttackTicksPerFrame != 0)
            || NativeSpecialAttackFrameCount is < 0 or > sbyte.MaxValue
            || NativeSpecialAttackTicksPerFrame is < 0 or > sbyte.MaxValue
            || NativeHitRecoveryFrameCount is < 0 or > sbyte.MaxValue
            || NativeHitRecoveryTicksPerFrame is < 0 or > sbyte.MaxValue
            || NativeWalkFrameCount is < 0 or > sbyte.MaxValue
            || NativeWalkTicksPerFrame is < 0 or > sbyte.MaxValue)
            throw new InvalidDataException("Native monster initialization provenance is outside native bounds.");
        if (Binding.NativeSlot != InitialState.Slot || Binding.NativeTypeId != InitialState.Type
            || Binding.NativeUniqueId != InitialState.UniqueType || Binding.EntityId == 0
            || Binding.ContentMonsterId == 0 || Binding.UniqueIdentity is null
            || Binding.UniqueIdentity.Any(char.IsControl)
            || (InitialState.UniqueType == 255 ? Binding.UniqueIdentity.Length != 0
                : string.IsNullOrWhiteSpace(Binding.UniqueIdentity)))
            throw new InvalidDataException("Native monster initialization has inconsistent identity bindings.");
        try {
            if (new System.Text.UTF8Encoding(false, true).GetByteCount(Binding.UniqueIdentity) > 128)
                throw new InvalidDataException("Native monster unique identity exceeds the protocol boundary.");
        } catch (System.Text.EncoderFallbackException exception) {
            throw new InvalidDataException("Native monster unique identity is not valid UTF-8 text.", exception);
        }
    }

    internal void ValidateTarget(uint entityId, uint contentMonsterId, uint levelId,
        bool isUnique, string uniqueIdentity)
    {
        Validate();
        if (Binding.EntityId != entityId || Binding.ContentMonsterId != contentMonsterId
            || Binding.LevelId != levelId || (InitialState.UniqueType != 255) != isUnique
            || Binding.UniqueIdentity != uniqueIdentity)
            throw new InvalidDataException("Native monster initialization is bound to a different runtime actor.");
    }

    internal static bool IsFingerprint(string? value) => value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F');
}

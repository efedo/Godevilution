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
            || SpecialAttackEffectFrame is < sbyte.MinValue or > sbyte.MaxValue)
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

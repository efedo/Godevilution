namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    private AuthoritativeCathedralNativePopulationPolicy? nativePopulationPolicy;
    private uint? nativePopulationParticipantClassId;

    internal bool HasNativePopulation => nativePopulationPolicy is not null;

    internal void ConfigureNativePopulationPolicy(AuthoritativeCathedralNativePopulationPolicy? policy,
        AuthoritativeCathedralItemRuntimeProfile? items, string? monsterMetadataFingerprint)
    {
        if (policy is null)
            return;
        if (items is null || items.GenerationFingerprint != policy.Fingerprint
            || monsterMetadataFingerprint != policy.MonsterMetadataFingerprint
            || items.LevelProfiles.Count != 1 || items.LevelProfiles[0].LevelId != policy.LevelId)
            throw new InvalidDataException("The native population owner must bind the exact item and monster generation identities.");
        policy.ValidateItemProfile(items);
        policy.ValidateWorld(this);
        nativePopulationPolicy = policy;
    }

    internal void ValidateNativePopulationObject(AuthoritativeWorldObject value)
    {
        if (nativePopulationPolicy is null || value.LevelId != nativePopulationPolicy.LevelId)
            throw new InvalidDataException("Explicitly refused native objects require the populated profile owner.");
        nativePopulationPolicy.ValidateObject(value);
        if (!ContainsPosition(value.LevelId, value.PositionX, value.PositionY))
            throw new InvalidDataException("A retained native object lies outside its owned level.");
    }

    internal bool CanOperateNativeObject(AuthoritativeWorldObject value)
        => nativePopulationPolicy is null || value.LevelId != nativePopulationPolicy.LevelId
            || nativePopulationPolicy.CanOperate(value);

    internal bool CanBreakWorldObject([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] AuthoritativeWorldObject? value)
    {
        if (value is null || !value.Breakable)
            return false;
        if (nativePopulationPolicy is null || value.LevelId != nativePopulationPolicy.LevelId)
            return true;
        nativePopulationPolicy.ValidateObject(value);
        return false;
    }

    /** Selects gameplay behavior only for the explicitly owned native level. */
    internal bool OwnsNativePopulationLevel(uint levelId)
        => nativePopulationPolicy is not null && levelId == nativePopulationPolicy.LevelId;

    internal bool AllowsNativePopulationLevel(uint levelId)
        => nativePopulationPolicy is null || levelId == 0 || levelId == nativePopulationPolicy.LevelId;

    private bool IsNativePopulationObjectSolid(uint levelId, int x, int y)
        => nativePopulationPolicy is not null && levelId == nativePopulationPolicy.LevelId
            && nativePopulationPolicy.ObjectEntityAt(x, y) is { } entityId
            && nativePopulationPolicy.ObjectIsSolid(entityId);

    private bool TryGetNativePopulationMissileObject(uint levelId, int x, int y,
        out AuthoritativeWorldObject? result)
    {
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
        => nativePopulationPolicy is null || levelId != nativePopulationPolicy.LevelId;

    internal void ValidateNativePopulationState(IEnumerable<AuthoritativePlayerState> participants)
    {
        if (nativePopulationPolicy is null)
            return;
        nativePopulationPolicy.ValidateWorld(this);
        if (participants.Any(player => !AllowsNativePopulationLevel(player.LevelId)
            || player.CharacterClassId != nativePopulationParticipantClassId))
            throw new InvalidDataException("The native route requires Rogue participants in Town or its owned Cathedral level.");
        if (participants.Any(player => nativePopulationPolicy.ReservesEntityId(player.EntityId)))
            throw new InvalidDataException("A participant reused a dormant native monster entity reservation.");
    }
}

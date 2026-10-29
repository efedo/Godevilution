namespace Devilution.Diablo;

public sealed partial class DiabloSimulation
{
    /** Native actor metadata identity is retained even when the live roster is empty. */
    public string? NativeMonsterMetadataFingerprint { get; private set; }

    internal void ConfigureNativeMonsterMetadata(string? fingerprint)
    {
        if (fingerprint is not null
            && !AuthoritativeNativeMonsterInitialization.IsFingerprint(fingerprint))
            throw new InvalidDataException("Native monster runtime metadata identity is invalid.");
        if (fingerprint is not null
            && (gameMode is not (DiabloGameMode.SinglePlayer or DiabloGameMode.Multiplayer) || difficulty != DiabloDifficulty.Normal
                || CombatRules.GameVariant != DiabloGameVariant.Diablo))
            throw new InvalidDataException("Cold Cathedral actor imports require original Diablo Normal.");
        var nativeSlots = new HashSet<(uint Level, int Slot)>();
        foreach (var target in WorldState.CombatTargets.Values) {
            if (target.NativeInitialization is not { } native)
                continue;
            native.ValidateTarget(target.EntityId, target.MonsterId, target.LevelId,
                target.IsUnique, target.UniqueIdentity);
            // Catacombs actors carry their floor owner's C2 metadata identity.
            var expected = native.CatacombsDepth == 0 ? fingerprint
                : WorldState.NativePopulationForLevel(target.LevelId)?.MonsterMetadataFingerprint;
            if (expected != native.MetadataFingerprint)
                throw new InvalidDataException("Native actor initialization uses different runtime metadata.");
            if (!nativeSlots.Add((target.LevelId, native.InitialState.Slot)))
                throw new InvalidDataException("Native actor initialization repeats a level/slot binding.");
        }
        NativeMonsterMetadataFingerprint = fingerprint;
    }
}

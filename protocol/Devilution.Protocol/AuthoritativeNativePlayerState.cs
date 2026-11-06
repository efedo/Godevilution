using Devilution.Protocol.V1;

namespace Devilution.Protocol;

/** Additive native balances and known-spell bits; legacy projections retain their original bounds. */
public static class AuthoritativeNativePlayerState
{
    public static void Validate(PlayerSnapshot player)
    {
        if (player.HasNativeMana != player.HasNativeManaMaximum
            || player.HasNativeMana && (player.NativeMana >= 0 && player.NativeManaMaximum >= 0
                || player.NativeMana < -(2000 << 6) || player.NativeManaMaximum < -(2000 << 6)
                || player.NativeMana > player.NativeManaMaximum
                || player.Mana != Math.Max(0, player.NativeMana)
                || player.ManaMaximum != Math.Max(0, player.NativeManaMaximum)))
            throw new InvalidDataException("The native mana projection is inconsistent with its legacy balance.");
        var maps = player.FullyExploredLevelIds;
        if (maps.Count > AuthoritativeProtocolLimits.MaximumNativeVisitedLevelCount
            || maps.Distinct().Count() != maps.Count
            || maps.Any(level => level == 0 || !AuthoritativeLevelIdRules.IsRegularLevel(level)))
            throw new InvalidDataException("The fully explored native map levels are invalid.");
        if (player.MemorizedSpells.Count + player.ZeroLevelMemorizedSpells.Count
            > AuthoritativeProtocolLimits.MaximumMemorizedSpellCount)
            throw new InvalidDataException("The native known-spell count exceeds the shared limit.");
        var ids = player.MemorizedSpells.Select(spell => spell.SpellId).ToHashSet();
        foreach (var spell in player.ZeroLevelMemorizedSpells)
            if (spell.SpellId is 0 or > AuthoritativeProtocolLimits.MaximumStableSpellId
                || spell.Level != 0 || !ids.Add(spell.SpellId) || !Enum.IsDefined(spell.TargetingKind)
                || !AuthoritativeProtocolLimits.IsDialogueTextValid(spell.DisplayName))
                throw new InvalidDataException("The native zero-level known-spell projection is invalid.");
    }
}

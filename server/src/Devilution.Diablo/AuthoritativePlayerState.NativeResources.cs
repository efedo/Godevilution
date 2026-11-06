namespace Devilution.Diablo;

public sealed partial class AuthoritativePlayerState
{
    private bool nativeResourceBases;
    private int nativeLifeBase;
    private int nativeManaBase;
    internal int? NativeLifeBase => nativeResourceBases ? nativeLifeBase : null;
    internal int? NativeManaBase => nativeResourceBases ? nativeManaBase : null;

    internal void EnableNativeResourceBases()
    {
        if (nativeResourceBases) return;
        nativeLifeBase = checked(Life + BaseLifeMaximum - LifeMaximum);
        nativeManaBase = checked(Mana + BaseManaMaximum - ManaMaximum);
        nativeResourceBases = true;
    }

    internal void RestoreNativeResourceBases(int? lifeBase, int? manaBase)
    {
        if (lifeBase.HasValue != manaBase.HasValue)
            throw new InvalidDataException("Native current base resources must be retained together.");
        nativeResourceBases = lifeBase.HasValue;
        nativeLifeBase = lifeBase ?? 0;
        nativeManaBase = manaBase ?? 0;
    }

    internal void KeepNativeDeathLifeZero() => lifeValue = 0;

    /** Native players.cpp setHitPoints and spells.cpp ApplyResurrect replace, rather than increment, current bases. */
    internal void ResetNativeCurrentResourceBases()
    {
        if (!nativeResourceBases) return;
        nativeLifeBase = checked(Life + BaseLifeMaximum - LifeMaximum);
        nativeManaBase = checked(Mana + BaseManaMaximum - ManaMaximum);
    }

    /** Native items.cpp CalcPlrLifeMana: current base balances survive maximum clipping and negative mana. */
    private void RecalculateNativeResources()
    {
        var wasDead = Life <= 0 || CombatMode == DiabloPlayerCombatMode.Death;
        var adjusted = AuthoritativeEquipmentResourceRules.ApplyHellfireConversions(
            BaseLifeMaximum, BaseManaMaximum, Equipment, PlayerClass);
        LifeMaximum = Math.Clamp(adjusted.LifeQ6, 64, 2000 << 6);
        ManaMaximum = Math.Clamp(adjusted.ManaQ6, 0, 2000 << 6);
        lifeValue = Math.Min(checked(adjusted.LifeQ6 - BaseLifeMaximum + nativeLifeBase), LifeMaximum);
        if (lifeValue < 64) {
            lifeValue = 0;
            nativeLifeBase = BaseLifeMaximum - LifeMaximum;
        }
        manaValue = Math.Min(checked(adjusted.ManaQ6 - BaseManaMaximum + nativeManaBase), ManaMaximum);
        if (wasDead) KeepNativeDeathLifeZero();
    }
}

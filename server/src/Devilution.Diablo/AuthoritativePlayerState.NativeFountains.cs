namespace Devilution.Diablo;

public sealed partial class AuthoritativePlayerState
{
    /** Native objects.cpp OperateFountains increments both balances; only strict overflow replaces the current base. */
    internal void DrinkNativeFountain(bool life)
    {
        EnableNativeResourceBases();
        if (life) {
            if (lifeValue >= LifeMaximum) return;
            lifeValue += 64;
            nativeLifeBase += 64;
            if (lifeValue > LifeMaximum) {
                lifeValue = LifeMaximum;
                nativeLifeBase = BaseLifeMaximum;
            }
        } else {
            if (manaValue >= ManaMaximum) return;
            manaValue += 64;
            nativeManaBase += 64;
            if (manaValue > ManaMaximum) {
                manaValue = ManaMaximum;
                nativeManaBase = BaseManaMaximum;
            }
        }
    }
}

namespace Devilution.Diablo;

/** Native player light-radius derivation from identified equipped modifiers. */
public static class AuthoritativeEquipmentLightRules
{
    public const int NativeDefaultLightRadius = 10;
    public const int NativeMinimumLightRadius = 2;
    public const int NativeMaximumLightRadius = 15;

    /** Calculates the native-clamped light radius for one equipment set. */
    public static int CalculateLightRadius(IEnumerable<EquippedStoreItem> equipment)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        long lightRadius = NativeDefaultLightRadius;
        foreach (var equippedItem in equipment) {
            ArgumentNullException.ThrowIfNull(equippedItem);
            var state = equippedItem.State;
            if (!state.StatFlag || (state.Magical != 0 && !state.Identified))
                continue;
            lightRadius += state.PlusLight;
        }

        return (int)Math.Clamp(
            lightRadius,
            NativeMinimumLightRadius,
            NativeMaximumLightRadius);
    }
}

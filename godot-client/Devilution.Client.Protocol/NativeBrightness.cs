namespace Devilution.Client.Protocol;

/**
 * The DevilutionX brightness option: 0 shows the native palette unchanged and
 * 100 brightens most, through the quadratic tone curve of legacy-native
 * engine/gfx/palette.cpp. Values snap down to steps of five like LoadBrightness.
 */
public static class NativeBrightness
{
    public const int Minimum = 0;
    public const int Maximum = 100;
    public const int Step = 5;

    /** Clamps a stored value to the native range and step. */
    public static int Normalize(int brightness)
    {
        var clamped = Math.Clamp(brightness, Minimum, Maximum);
        return clamped - clamped % Step;
    }

    /** IncreaseBrightness and DecreaseBrightness: one step, bounded at the range ends. */
    public static int Adjust(int brightness, int direction)
        => Normalize(Normalize(brightness) + Math.Sign(direction) * Step);

    /** CalculateToneMappingParameter: 0 at neutral, -2 at maximum brightening. */
    public static float ToneMappingParameter(int brightness)
        => -(Normalize(brightness) / 100.0F) * 2.0F;

    /** MapTone: f(x) = a*x^2 + (1-a)*x on one 8-bit channel. */
    public static byte MapTone(int brightness, byte channel)
    {
        var a = ToneMappingParameter(brightness);
        var x = channel / 255.0F;
        var y = Math.Clamp((a * x * x) + ((1.0F - a) * x), 0.0F, 1.0F);
        return (byte)((y * 255.0F) + 0.5F);
    }
}

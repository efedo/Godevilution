namespace Devilution.Protocol;

/**
 * Game-speed multipliers scale only wall-clock pacing: the host runs
 * tick_rate_hz * multiplier authoritative ticks per second and each tick is unchanged.
 */
public static class GameSpeedLimits
{
    public const uint Minimum = 1;
    public const uint Maximum = 20;

    /** The client's selectable steps, ascending. */
    public static readonly IReadOnlyList<uint> Steps = [1, 2, 3, 5, 10, 20];

    public static bool IsValid(uint multiplier) => multiplier is >= Minimum and <= Maximum;

    /** Zero from an older peer means 1x. */
    public static uint FromWire(uint multiplier) => multiplier == 0 ? Minimum : multiplier;

    /** The nearest step at or below a saved or negotiated value. */
    public static uint NearestStep(uint multiplier)
    {
        var result = Steps[0];
        foreach (var step in Steps)
            if (step <= multiplier)
                result = step;
        return result;
    }

    /** One step up (direction above zero) or down from the step nearest to current. */
    public static uint Step(uint current, int direction)
    {
        var index = 0;
        var nearest = NearestStep(current);
        for (var i = 0; i < Steps.Count; i++)
            if (Steps[i] == nearest)
                index = i;
        index = Math.Clamp(index + Math.Sign(direction), 0, Steps.Count - 1);
        return Steps[index];
    }
}

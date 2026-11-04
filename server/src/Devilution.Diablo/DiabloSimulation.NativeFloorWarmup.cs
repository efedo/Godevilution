namespace Devilution.Diablo;

public sealed partial class DiabloSimulation
{
    /**
     * Enters every supported floor once on a throwaway run, so that the live first entry
     * finds its code compiled. Fresh activation shares only immutable content with this
     * run, which makes this safe off the authority lock. Other profiles have nothing to do.
     */
    public void WarmNativeFloorEntries()
    {
        if (WorldState.NativeProgressionFreshActivation is not { } activate)
            return;
        var fresh = activate();
        var trusted = fresh.TrustedOptions
            ?? throw new InvalidDataException("Fresh native run activation omitted trusted construction inputs.");
        fresh.Simulation.WorldState.WarmNativeFloorEntries(trusted.Items!, trusted.Spells!);
    }
}

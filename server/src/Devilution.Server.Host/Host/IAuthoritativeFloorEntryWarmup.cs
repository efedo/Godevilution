namespace Devilution.Server.Host;

/** An adapter whose first entry to a floor is slow until its code has run once. */
public interface IAuthoritativeFloorEntryWarmup
{
    /** Prepares that code without touching live state; call it off the authority lock. */
    void WarmFloorEntries();
}

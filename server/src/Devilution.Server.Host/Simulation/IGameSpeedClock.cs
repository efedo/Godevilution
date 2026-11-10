namespace Devilution.Server.Simulation;

/** A paced authoritative clock whose wall-clock rate can be scaled without changing ticks. */
public interface IGameSpeedClock
{
    uint SpeedMultiplier { get; }

    /** Applies at a tick boundary and returns the first tick paced at the new rate. */
    ulong SetSpeedMultiplier(uint multiplier);
}

namespace Devilution.Server.Simulation;

/** Compatibility alias for the engine-owned authoritative clock contract. */
public interface IAuthoritativeClock : Devilution.Engine.IAuthoritativeClock
{
}

/** Compatibility adapter retained for existing server-host callers. */
public sealed class DelegateAuthoritativeClock : IAuthoritativeClock
{
    private readonly Func<ulong> provider;

    public DelegateAuthoritativeClock(Func<ulong> provider)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public ulong CurrentTick => provider();
}

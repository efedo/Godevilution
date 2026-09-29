namespace Devilution.Engine;

/** Supplies the simulation tick at an authoritative engine boundary. */
public interface IAuthoritativeClock
{
    ulong CurrentTick { get; }
}

/** Optional clock capability for continuing a persisted timeline after restart. */
public interface IResumableAuthoritativeClock : IAuthoritativeClock
{
    void RestoreAtTick(ulong tick);
}

/** Test and embedding adapter for a caller-owned authoritative tick source. */
public sealed class DelegateAuthoritativeClock : IAuthoritativeClock
{
    private readonly Func<ulong> provider;

    public DelegateAuthoritativeClock(Func<ulong> provider)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public ulong CurrentTick => provider();
}

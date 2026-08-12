namespace Devilution.Engine;

/**
 * Pins one published ruleset generation for the duration of an operation.
 * Disposing the lease allows a replaced generation to be retired safely.
 */
public sealed class GameplayRulesetLease<TActivation> : IDisposable
    where TActivation : class
{
    private Action? release;

    internal GameplayRulesetLease(
        GameplayRulesetActivation<TActivation> activation,
        Action release)
    {
        Activation = activation ?? throw new ArgumentNullException(nameof(activation));
        this.release = release ?? throw new ArgumentNullException(nameof(release));
    }

    public GameplayRulesetActivation<TActivation> Activation { get; }

    public void Dispose()
    {
        Interlocked.Exchange(ref release, null)?.Invoke();
    }
}

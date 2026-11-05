using System.Diagnostics;

namespace Devilution.Client.Acceptance;

/** Cumulative wall budget across floor visits; the injected monotonic clock keeps tests deterministic. */
internal sealed class AcceptanceStageBudget(string name, double seconds, Func<double>? clock = null)
{
    private readonly Func<double> readClock = clock ?? (() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
    private double spent;
    private double started;
    private bool active;

    internal double ElapsedSeconds => spent + (active ? readClock() - started : 0);

    internal IDisposable Enter(Action? onExit = null)
    {
        if (active) throw new InvalidOperationException($"{name} already has an active scope.");
        Check();
        started = readClock();
        active = true;
        return new Scope(() => {
            spent += readClock() - started;
            active = false;
            onExit?.Invoke();
        });
    }

    internal void Check()
    {
        if (ElapsedSeconds >= seconds)
            throw new TimeoutException($"{name} exceeded its {seconds:0}s total acceptance budget; the object stage stopped.");
    }

    private sealed class Scope(Action stop) : IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            stop();
        }
    }
}

using System.Threading;

namespace Devilution.Client.Acceptance;

/** A startup stage must progress even when the Godot frame thread is blocked. */
internal sealed class AcceptanceStartupWatchdog : IDisposable
{
    private readonly object synchronization = new();
    private readonly TimeSpan timeout;
    private readonly Func<TimeSpan, Action, IDisposable> schedule;
    private readonly Action<string> fail;
    private IDisposable? timer;
    private string stage = "initializing acceptance";
    private long generation;
    private bool ended;

    internal AcceptanceStartupWatchdog(TimeSpan timeout, Action<string> fail,
        Func<TimeSpan, Action, IDisposable>? schedule = null)
    {
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        this.timeout = timeout;
        this.fail = fail;
        this.schedule = schedule ?? ((duration, expire) => new Timer(_ => expire(), null, duration, Timeout.InfiniteTimeSpan));
        lock (synchronization) RestartTimer();
    }

    internal void Mark(string value)
    {
        IDisposable? previous;
        lock (synchronization) {
            if (ended || stage == value) return;
            stage = value;
            previous = timer;
            RestartTimer();
        }
        previous?.Dispose();
    }

    private void RestartTimer()
    {
        var current = ++generation;
        timer = schedule(timeout, () => Expire(current));
    }

    private void Expire(long current)
    {
        string stalledStage;
        IDisposable? expired;
        lock (synchronization) {
            // A callback already queued by the previous stage cannot expire the new stage.
            if (ended || current != generation) return;
            ended = true;
            stalledStage = stage;
            expired = timer;
            timer = null;
        }
        expired?.Dispose();
        fail(stalledStage);
    }

    public void Dispose()
    {
        IDisposable? previous;
        lock (synchronization) {
            ended = true;
            previous = timer;
            timer = null;
        }
        previous?.Dispose();
    }
}

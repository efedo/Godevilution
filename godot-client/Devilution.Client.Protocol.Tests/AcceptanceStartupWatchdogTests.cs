using Devilution.Client.Acceptance;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AcceptanceStartupWatchdogTests
{
    [Fact]
    public void BlockedFrameThreadFailsAtTheLastStartupStageExactlyOnce()
    {
        Action? expire = null;
        var failures = new List<string>();
        using var watchdog = new AcceptanceStartupWatchdog(TimeSpan.FromSeconds(60), failures.Add,
            (_, callback) => { expire = callback; return new Subscription(); });
        watchdog.Mark("Main _Ready returned; awaiting first frame");
        expire!(); expire();
        Assert.Equal("Main _Ready returned; awaiting first frame", Assert.Single(failures));
    }

    [Fact]
    public void FirstSnapshotCompletionDisarmsTheStartupDeadline()
    {
        Action? expire = null; var failed = false;
        var watchdog = new AcceptanceStartupWatchdog(TimeSpan.FromSeconds(60), _ => failed = true,
            (_, callback) => { expire = callback; return new Subscription(); });
        watchdog.Dispose(); expire!();
        Assert.False(failed);
    }

    [Fact]
    public void HealthySceneLoadAndJoinEachReceiveTheirOwnStallDeadline()
    {
        var scheduler = new VirtualScheduler();
        var failures = new List<string>();
        using var watchdog = new AcceptanceStartupWatchdog(TimeSpan.FromSeconds(60), failures.Add, scheduler.Schedule);
        watchdog.Mark("loading Main scene");
        scheduler.Advance(TimeSpan.FromSeconds(50));
        watchdog.Mark("connecting to authoritative host");
        scheduler.Advance(TimeSpan.FromSeconds(15));
        Assert.Empty(failures);
        scheduler.Advance(TimeSpan.FromSeconds(45));
        Assert.Equal("connecting to authoritative host", Assert.Single(failures));
    }

    [Fact]
    public void AQueuedOldTimerCannotExpireAStageThatHasAdvanced()
    {
        var callbacks = new List<Action>(); var failures = new List<string>();
        using var watchdog = new AcceptanceStartupWatchdog(TimeSpan.FromSeconds(60), failures.Add,
            (_, callback) => { callbacks.Add(callback); return new Subscription(); });
        watchdog.Mark("joining");
        callbacks[0]();
        Assert.Empty(failures);
        callbacks[^1](); callbacks[^1]();
        Assert.Equal("joining", Assert.Single(failures));
    }

    [Fact]
    public void RepeatingTheSameStageDoesNotKeepAStallAlive()
    {
        var scheduler = new VirtualScheduler(); var failures = new List<string>();
        using var watchdog = new AcceptanceStartupWatchdog(TimeSpan.FromSeconds(60), failures.Add, scheduler.Schedule);
        watchdog.Mark("joining"); scheduler.Advance(TimeSpan.FromSeconds(50));
        watchdog.Mark("joining"); scheduler.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal("joining", Assert.Single(failures));
    }

    private sealed class VirtualScheduler
    {
        private TimeSpan now;
        private readonly List<Scheduled> pending = [];
        public IDisposable Schedule(TimeSpan duration, Action callback)
        {
            var scheduled = new Scheduled(now + duration, callback); pending.Add(scheduled); return scheduled;
        }
        public void Advance(TimeSpan duration)
        {
            now += duration;
            foreach (var scheduled in pending.Where(value => !value.Disposed && value.Due <= now).ToArray()) {
                scheduled.Disposed = true; scheduled.Callback();
            }
        }
        private sealed class Scheduled(TimeSpan due, Action callback) : IDisposable
        {
            public TimeSpan Due { get; } = due;
            public Action Callback { get; } = callback;
            public bool Disposed { get; set; }
            public void Dispose() => Disposed = true;
        }
    }

    private sealed class Subscription : IDisposable { public void Dispose() { } }
}

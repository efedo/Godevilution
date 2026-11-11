namespace Devilution.Client.Acceptance;

/** Wall-clock deadlines also cover awaited frame/input signals; timeouts cancel further scenario input. */
internal sealed class TownCaptureRun(Action<string> log, Func<TimeSpan, CancellationToken, Task>? delay = null)
{
    internal string Phase { get; private set; } = "initializing";
    internal string Operation { get; set; } = "initializing";
    internal bool Completed { get; private set; }
    internal string? Failure { get; private set; }
    internal List<string> CompletedPhases { get; } = [];

    internal async Task PhaseAsync(string name, double seconds, Func<CancellationToken, Task> action, Func<string> describe)
    {
        Phase = name; Operation = "starting";
        log($"Town capture: begin {name}; deadline={seconds:0.#}s; {describe()}");
        using var workCancellation = new CancellationTokenSource();
        using var timerCancellation = new CancellationTokenSource();
        Task? work = null;
        try {
            var expired = (delay ?? ((duration, cancellation) => Task.Delay(duration, cancellation)))(
                TimeSpan.FromSeconds(seconds), timerCancellation.Token);
            work = action(workCancellation.Token);
            if (await Task.WhenAny(work, expired) != work) {
                await expired;
                throw new TimeoutException($"deadline of {seconds:0.#}s expired");
            }
            await work;
            CompletedPhases.Add(name);
            log($"Town capture: completed {name}; {describe()}");
        } catch (Exception exception) {
            workCancellation.Cancel();
            // A timed-out engine signal may complete later; observe its cancellation without waiting on it again.
            if (work is not null) _ = ObserveAsync(work);
            Failure = $"Town capture phase '{name}' failed during '{Operation}': {exception.Message}; {describe()}";
            log(Failure);
            throw new InvalidOperationException(Failure, exception);
        } finally { timerCancellation.Cancel(); }
    }

    /** Completion publishes synchronously: there is no post-workload renderer signal or GPU readback to await. */
    internal void Complete(Action publish)
    {
        if (Phase != "finalizing" || new[] { "idle", "walking", "unreachable-clicks", "resident-clicks" }
                .Any(phase => !CompletedPhases.Contains(phase)))
            throw new InvalidOperationException("Town capture cannot complete before all four measured phases finish.");
        publish();
        Completed = true;
    }

    internal void Fail(Exception exception)
    {
        Completed = false;
        Failure ??= $"Town capture phase '{Phase}' failed during '{Operation}': {exception.Message}";
        log(Failure);
    }

    private static async Task ObserveAsync(Task work)
    {
        try { await work; } catch { /* The phase failure is already logged and exported. */ }
    }
}

using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Devilution.Server.Simulation;
using Xunit;

namespace Devilution.Server.Tests;

/** Fatal notification must precede peer execution and final advancement. */
public sealed class SchedulerFatalFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MutatingExecutionFailureStopsPeersAndFinalAdvance(bool observerThrows)
    {
        var executor = new ThrowingExecutor();
        var server = new AuthoritativeCommandServer(executor);
        Exception? observed = null;
        using var scheduler = new ClockGatedCommandScheduler(server, null,
            ClockGatedCommandScheduler.DefaultMaximumPendingCommands, default, failure => {
                observed = failure;
                if (observerThrows)
                    throw new InvalidOperationException("diagnostic failure");
            });
        var failed = scheduler.SubmitBatchAsync("a", Attack(120), 111, TestContext.Current.CancellationToken);
        var peer = scheduler.SubmitBatchAsync("b", Attack(125), 111, TestContext.Current.CancellationToken);
        scheduler.DrainAndAdvanceTo(130);
        Assert.Equal(1, executor.Mutations);
        Assert.IsType<InvalidDataException>(observed);
        Assert.DoesNotContain(130UL, executor.Advances);
        Assert.True(server.CurrentTick <= 120);
        Assert.IsType<InvalidDataException>(await Record.ExceptionAsync(async () =>
            await failed.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)));
        Assert.IsAssignableFrom<OperationCanceledException>(await Record.ExceptionAsync(async () =>
            await peer.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)));
        Assert.Equal(0, scheduler.PendingCommandCount);
        Assert.Throws<ObjectDisposedException>(() => scheduler.DrainAndAdvanceTo(140));
    }

    [Fact]
    public async Task ImmediateBatchAlsoNotifiesBeforeFurtherAdmission()
    {
        var executor = new ThrowingExecutor();
        var server = new AuthoritativeCommandServer(executor);
        var notifications = 0;
        using var scheduler = new ClockGatedCommandScheduler(server, null,
            ClockGatedCommandScheduler.DefaultMaximumPendingCommands, default, _ => notifications++);
        var failed = scheduler.SubmitBatchAsync("a", Attack(111), 111, TestContext.Current.CancellationToken);
        Assert.IsType<InvalidDataException>(await Record.ExceptionAsync(async () =>
            await failed.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)));
        Assert.Equal(1, notifications);
        Assert.Throws<ObjectDisposedException>(() => { _ = scheduler.SubmitBatchAsync("b", Attack(112), 112, TestContext.Current.CancellationToken); });
        Assert.Equal(1, executor.Mutations);
    }

    private static CommandBatch Attack(ulong tick) => new() {
        Commands = { new Command { ClientSequence = 1, RequestedTick = tick,
            AttackRequested = new AttackRequested { TargetEntityId = 1 } } },
    };

    private sealed class ThrowingExecutor : IAuthoritativeCommandExecutor, IAuthoritativeTickExecutor
    {
        public int Mutations { get; private set; }
        public List<ulong> Advances { get; } = [];
        public void AdvanceTo(ulong tick) => Advances.Add(tick);
        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
        {
            Mutations++;
            throw new InvalidDataException("partially mutated execution");
        }
    }
}

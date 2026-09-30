using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Xunit;

namespace Devilution.Server.Tests;

/** Characterizes the generic clock gate without host or TCP timing. */
public sealed class ClockGatedCommandSchedulerTests
{
    [Fact]
    public async Task FutureBatchesWaitForThePhysicalClockAndDrainInRequestedTickOrder()
    {
        var fixture = CreateFixture();
        using var scheduler = fixture.Scheduler;

        var at125 = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(1, requestedTick: 125, targetEntityId: 1)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);
        var at120 = scheduler.SubmitBatchAsync(
            "player-b",
            Batch(Attack(1, requestedTick: 120, targetEntityId: 2)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(at125.IsCompleted);
        Assert.False(at120.IsCompleted);
        Assert.Equal(0, fixture.Executor.CallCount);
        Assert.Equal(0UL, fixture.Server.CurrentTick);

        Assert.Equal(0, scheduler.DrainAndAdvanceTo(111));
        Assert.False(at125.IsCompleted);
        Assert.False(at120.IsCompleted);
        Assert.Equal(0, fixture.Executor.CallCount);
        Assert.Equal(111UL, fixture.Server.CurrentTick);

        Assert.Equal(1, scheduler.DrainAndAdvanceTo(120));
        var firstAcknowledgement = await AwaitWithin(at120);
        Assert.Equal(CommandStatus.Accepted, Assert.Single(firstAcknowledgement.Results).Status);
        Assert.False(at125.IsCompleted);
        Assert.Equal([1UL], fixture.Executor.ClientSequences);
        Assert.Equal([120UL], fixture.Executor.AppliedTicks);
        Assert.Equal(120UL, fixture.Server.CurrentTick);

        Assert.Equal(1, scheduler.DrainAndAdvanceTo(130));
        var secondAcknowledgement = await AwaitWithin(at125);
        Assert.Equal(CommandStatus.Accepted, Assert.Single(secondAcknowledgement.Results).Status);
        Assert.Equal([1UL, 1UL], fixture.Executor.ClientSequences);
        Assert.Equal([2UL, 1UL], fixture.Executor.TargetEntityIds);
        Assert.Equal([120UL, 125UL], fixture.Executor.AppliedTicks);
        Assert.Equal(130UL, fixture.Server.CurrentTick);
        Assert.Equal(2, fixture.Publications.Count);
    }

    [Fact]
    public async Task ALaterSubmissionDrainsOlderDueWorkAtItsAdmissionBoundary()
    {
        var fixture = CreateFixture();
        using var scheduler = fixture.Scheduler;

        var first = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(1, requestedTick: 120, targetEntityId: 1)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);
        var second = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(2, requestedTick: 125, targetEntityId: 2)),
            actualAdmissionTick: 120, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Accepted, Assert.Single((await AwaitWithin(first)).Results).Status);
        Assert.False(second.IsCompleted);
        Assert.Equal([1UL], fixture.Executor.ClientSequences);
        Assert.Equal(120UL, fixture.Server.CurrentTick);

        scheduler.DrainAndAdvanceTo(125);
        Assert.Equal(CommandStatus.Accepted, Assert.Single((await AwaitWithin(second)).Results).Status);
        Assert.Equal([1UL, 2UL], fixture.Executor.ClientSequences);
    }

    [Fact]
    public async Task AscendingBatchHeadsExecuteAtTheirOwnTicksAndPreserveWireOrder()
    {
        var fixture = CreateFixture();
        using var scheduler = fixture.Scheduler;

        var acknowledgementTask = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(
                Attack(1, requestedTick: 120, targetEntityId: 1),
                Attack(2, requestedTick: 125, targetEntityId: 2)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(acknowledgementTask.IsCompleted);
        Assert.Equal(1, scheduler.DrainAndAdvanceTo(120));
        Assert.False(acknowledgementTask.IsCompleted);
        Assert.Equal([1UL], fixture.Executor.ClientSequences);
        Assert.Equal([120UL], fixture.Executor.AppliedTicks);

        Assert.Equal(1, scheduler.DrainAndAdvanceTo(125));
        var acknowledgement = await AwaitWithin(acknowledgementTask);
        Assert.Collection(
            acknowledgement.Results,
            first => Assert.Equal(CommandStatus.Accepted, first.Status),
            second => Assert.Equal(CommandStatus.Accepted, second.Status));
        Assert.Equal([1UL, 2UL], fixture.Executor.ClientSequences);
        Assert.Equal([120UL, 125UL], fixture.Executor.AppliedTicks);
    }

    [Fact]
    public async Task NonMonotonicBatchHeadIsRejectedAtTheCurrentAuthoritativeTick()
    {
        var fixture = CreateFixture();
        using var scheduler = fixture.Scheduler;

        var acknowledgementTask = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(
                Attack(1, requestedTick: 125, targetEntityId: 1),
                Attack(2, requestedTick: 120, targetEntityId: 2)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, scheduler.DrainAndAdvanceTo(130));
        var acknowledgement = await AwaitWithin(acknowledgementTask);
        Assert.Collection(
            acknowledgement.Results,
            first => {
                Assert.Equal(CommandStatus.Accepted, first.Status);
                Assert.Equal(125UL, first.AppliedTick);
            },
            second => {
                Assert.Equal(CommandStatus.Rejected, second.Status);
                Assert.Equal(CommandRejectReason.TooLate, second.RejectReason);
                Assert.Equal(125UL, second.AppliedTick);
            });
        Assert.Equal([1UL], fixture.Executor.ClientSequences);
        Assert.Equal([125UL], fixture.Executor.AppliedTicks);
    }

    [Fact]
    public async Task EqualTickBatchesUseStableArrivalOrder()
    {
        var fixture = CreateFixture();
        using var scheduler = fixture.Scheduler;

        var laterWireBatch = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(2, requestedTick: 120, targetEntityId: 2)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);
        var earlierWireBatch = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(1, requestedTick: 120, targetEntityId: 1)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, scheduler.DrainAndAdvanceTo(120));
        await AwaitWithin(laterWireBatch);
        await AwaitWithin(earlierWireBatch);
        Assert.Equal([2UL, 1UL], fixture.Executor.ClientSequences);
        Assert.Equal([120UL, 120UL], fixture.Executor.AppliedTicks);
    }

    [Fact]
    public async Task LateGameplayIsRejectedButNonCriticalCommandsUseTheTwoTickWindow()
    {
        var fixture = CreateFixture();
        using var scheduler = fixture.Scheduler;

        var late = await scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(1, requestedTick: 120, targetEntityId: 1)),
            actualAdmissionTick: 123, cancellationToken: TestContext.Current.CancellationToken);
        var rescheduled = await scheduler.SubmitBatchAsync(
            "player-a",
            Batch(OpenStore(2, requestedTick: 121)),
            actualAdmissionTick: 123, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Rejected, Assert.Single(late.Results).Status);
        Assert.Equal(CommandRejectReason.TooLate, Assert.Single(late.Results).RejectReason);
        Assert.Equal(CommandStatus.Rescheduled, Assert.Single(rescheduled.Results).Status);
        Assert.Equal(123UL, Assert.Single(rescheduled.Results).AppliedTick);
        Assert.Equal([2UL], fixture.Executor.ClientSequences);
        Assert.Equal([123UL], fixture.Executor.AppliedTicks);
    }

    [Fact]
    public async Task PendingDuplicateKeepsTheFirstPayloadAndCompletedRetryIsImmediateDuplicate()
    {
        var fixture = CreateFixture();
        using var scheduler = fixture.Scheduler;

        var first = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(7, requestedTick: 125, targetEntityId: 1)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);
        var changedPending = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(7, requestedTick: 125, targetEntityId: 99)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(changedPending.IsCompleted);
        Assert.Equal(1, scheduler.DrainAndAdvanceTo(125));
        Assert.Equal(CommandStatus.Accepted, Assert.Single((await AwaitWithin(first)).Results).Status);
        Assert.Equal(CommandStatus.Duplicate, Assert.Single((await AwaitWithin(changedPending)).Results).Status);
        Assert.Equal([1UL], fixture.Executor.TargetEntityIds);

        var changedCompleted = await scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(7, requestedTick: 130, targetEntityId: 100)),
            actualAdmissionTick: 130, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(CommandStatus.Duplicate, Assert.Single(changedCompleted.Results).Status);
        Assert.Equal(125UL, Assert.Single(changedCompleted.Results).AppliedTick);
        Assert.Equal([1UL], fixture.Executor.TargetEntityIds);
    }

    [Fact]
    public async Task DuplicateAndMalformedWireEntriesDoNotReplaceTheFirstPendingCommand()
    {
        var fixture = CreateFixture();
        using var scheduler = fixture.Scheduler;

        var acknowledgementTask = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(
                Attack(8, requestedTick: 120, targetEntityId: 8),
                Attack(8, requestedTick: 125, targetEntityId: 88),
                new Command()),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, scheduler.DrainAndAdvanceTo(120));
        var acknowledgement = await AwaitWithin(acknowledgementTask);
        Assert.Collection(
            acknowledgement.Results,
            first => Assert.Equal(CommandStatus.Accepted, first.Status),
            duplicate => Assert.Equal(CommandStatus.Duplicate, duplicate.Status),
            malformed => {
                Assert.Equal(CommandStatus.Rejected, malformed.Status);
                Assert.Equal(CommandRejectReason.Malformed, malformed.RejectReason);
            });
        Assert.Equal([8UL], fixture.Executor.ClientSequences);
        Assert.Equal([8UL], fixture.Executor.TargetEntityIds);
    }

    [Fact]
    public async Task MalformedHeadFarFutureAndEmptyOrBlankSessionsRemainBounded()
    {
        var fixture = CreateFixture();
        using var scheduler = fixture.Scheduler;

        var malformedHead = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(
                new Command { ClientSequence = 9, RequestedTick = ulong.MaxValue },
                Attack(1, requestedTick: 120, targetEntityId: 1)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(malformedHead.IsCompleted);
        Assert.Equal(0, fixture.Executor.CallCount);
        Assert.Equal(0, scheduler.DrainAndAdvanceTo(111));
        Assert.Equal(1, scheduler.DrainAndAdvanceTo(120));
        var headAcknowledgement = await AwaitWithin(malformedHead);
        Assert.Collection(
            headAcknowledgement.Results,
            malformed => {
                Assert.Equal(CommandStatus.Rejected, malformed.Status);
                Assert.Equal(CommandRejectReason.Malformed, malformed.RejectReason);
            },
            accepted => Assert.Equal(CommandStatus.Accepted, accepted.Status));

        var empty = await scheduler.SubmitBatchAsync(
            "player-a",
            new CommandBatch(),
            actualAdmissionTick: 120, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(CommandStatus.Rejected, Assert.Single(empty.Results).Status);
        Assert.Equal(CommandRejectReason.Malformed, Assert.Single(empty.Results).RejectReason);

        var blankSession = await scheduler.SubmitBatchAsync(
            " ",
            Batch(Attack(2, requestedTick: ulong.MaxValue, targetEntityId: 2)),
            actualAdmissionTick: 120, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(CommandStatus.Rejected, Assert.Single(blankSession.Results).Status);
        Assert.Equal(CommandRejectReason.Malformed, Assert.Single(blankSession.Results).RejectReason);
        Assert.Equal([1UL], fixture.Executor.ClientSequences);
    }

    [Fact]
    public async Task ConnectionCancellationAfterAcceptanceDoesNotCancelQueuedWork()
    {
        var fixture = CreateFixture();
        using var scheduler = fixture.Scheduler;
        using var connectionCancellation = new CancellationTokenSource();

        var acknowledgementTask = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(1, requestedTick: 125, targetEntityId: 1)),
            actualAdmissionTick: 111,
            connectionCancellation.Token);
        connectionCancellation.Cancel();

        Assert.False(acknowledgementTask.IsCompleted);
        scheduler.DrainAndAdvanceTo(125);
        Assert.Equal(CommandStatus.Accepted, Assert.Single((await AwaitWithin(acknowledgementTask)).Results).Status);
        Assert.Equal(1, fixture.Executor.CallCount);
    }

    [Fact]
    public async Task PendingDuplicateBlocksItsBatchTailUntilTheOwnerIsDurable()
    {
        var fixture = CreateFixture();
        using var scheduler = fixture.Scheduler;

        var owner = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(1, requestedTick: 125, targetEntityId: 1)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);
        var dependent = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(
                Attack(1, requestedTick: 125, targetEntityId: 99),
                Attack(2, requestedTick: 120, targetEntityId: 2)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, scheduler.DrainAndAdvanceTo(120));
        Assert.False(dependent.IsCompleted);
        Assert.Empty(fixture.Executor.ClientSequences);

        Assert.Equal(2, scheduler.DrainAndAdvanceTo(125));
        Assert.Equal(CommandStatus.Accepted, Assert.Single((await AwaitWithin(owner)).Results).Status);
        var acknowledgement = await AwaitWithin(dependent);
        Assert.Collection(
            acknowledgement.Results,
            duplicate => Assert.Equal(CommandStatus.Duplicate, duplicate.Status),
            late => {
                Assert.Equal(CommandStatus.Rejected, late.Status);
                Assert.Equal(CommandRejectReason.TooLate, late.RejectReason);
            });
        Assert.Equal([1UL], fixture.Executor.ClientSequences);
    }

    [Fact]
    public async Task FailedLaterPublicationDoesNotFailAnEarlierDurableOwnerOrItsDependentTail()
    {
        var failSequenceTwoPublication = true;
        var executor = new RecordingExecutor();
        var server = new AuthoritativeCommandServer(executor);
        using var scheduler = new ClockGatedCommandScheduler(
            server,
            durablePublication: (_, acknowledgement) => {
                var result = Assert.Single(acknowledgement.Results);
                if (failSequenceTwoPublication && result.ClientSequence == 2)
                    throw new InvalidOperationException("later checkpoint failed");
            });

        var originalBatch = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(
                Attack(1, requestedTick: 120, targetEntityId: 1),
                Attack(2, requestedTick: 125, targetEntityId: 2)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);
        var dependentBatch = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(
                Attack(1, requestedTick: 125, targetEntityId: 99),
                Attack(3, requestedTick: 130, targetEntityId: 3)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, scheduler.DrainAndAdvanceTo(120));
        Assert.False(originalBatch.IsCompleted);
        Assert.False(dependentBatch.IsCompleted);
        scheduler.DrainAndAdvanceTo(125);
        await Assert.ThrowsAsync<InvalidOperationException>(() => AwaitWithin(originalBatch));

        failSequenceTwoPublication = false;
        Assert.Equal(1, scheduler.DrainAndAdvanceTo(130));
        var dependentAcknowledgement = await AwaitWithin(dependentBatch);
        Assert.Collection(
            dependentAcknowledgement.Results,
            duplicate => Assert.Equal(CommandStatus.Duplicate, duplicate.Status),
            accepted => Assert.Equal(CommandStatus.Accepted, accepted.Status));
        var retryAcknowledgement = await scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(2, requestedTick: 130, targetEntityId: 99)),
            actualAdmissionTick: 130, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(CommandStatus.Duplicate, Assert.Single(retryAcknowledgement.Results).Status);
        Assert.Equal([1UL, 2UL, 3UL], executor.ClientSequences);
    }

    [Fact]
    public async Task FailedOwnerReleasesWaitingDuplicateAndTailForFreshRetries()
    {
        var failSequenceOnePublication = true;
        var executor = new RecordingExecutor();
        var server = new AuthoritativeCommandServer(executor);
        using var scheduler = new ClockGatedCommandScheduler(
            server,
            durablePublication: (_, acknowledgement) => {
                var result = Assert.Single(acknowledgement.Results);
                if (failSequenceOnePublication && result.ClientSequence == 1)
                    throw new InvalidOperationException("owner checkpoint failed");
            });

        var owner = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(1, requestedTick: 125, targetEntityId: 1)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);
        var dependent = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(
                Attack(1, requestedTick: 125, targetEntityId: 99),
                Attack(2, requestedTick: 130, targetEntityId: 2)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);

        scheduler.DrainAndAdvanceTo(125);
        await Assert.ThrowsAsync<InvalidOperationException>(() => AwaitWithin(owner));
        await Assert.ThrowsAsync<InvalidOperationException>(() => AwaitWithin(dependent));
        Assert.Equal(0, scheduler.PendingCommandCount);

        failSequenceOnePublication = false;
        var retryOwner = await scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(1, requestedTick: 130, targetEntityId: 99)),
            actualAdmissionTick: 130, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(CommandStatus.Duplicate, Assert.Single(retryOwner.Results).Status);
        var freshTail = await scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(2, requestedTick: 130, targetEntityId: 2)),
            actualAdmissionTick: 130, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(CommandStatus.Accepted, Assert.Single(freshTail.Results).Status);
        Assert.Equal([1UL, 2UL], executor.ClientSequences);
    }

    [Fact]
    public async Task FailedOwnerFaultsFarFutureDuplicateTailWithoutWaitingForItsTick()
    {
        var failSequenceOnePublication = true;
        var executor = new RecordingExecutor();
        var server = new AuthoritativeCommandServer(executor);
        using var scheduler = new ClockGatedCommandScheduler(
            server,
            durablePublication: (_, acknowledgement) => {
                var result = Assert.Single(acknowledgement.Results);
                if (failSequenceOnePublication && result.ClientSequence == 1)
                    throw new InvalidOperationException("owner checkpoint failed");
            });

        var owner = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(1, requestedTick: 125, targetEntityId: 1)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);
        var farFutureDependent = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(
                Attack(1, requestedTick: 125, targetEntityId: 99),
                Attack(2, requestedTick: ulong.MaxValue, targetEntityId: 2)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);

        scheduler.DrainAndAdvanceTo(125);
        await Assert.ThrowsAsync<InvalidOperationException>(() => AwaitWithin(owner));
        await Assert.ThrowsAsync<InvalidOperationException>(() => AwaitWithin(farFutureDependent));
        Assert.Equal(0, scheduler.PendingCommandCount);
    }

    [Fact]
    public async Task FailedPublicationWithSameBatchDuplicateReleasesWorkWithoutRecursiveFailure()
    {
        var failPublication = true;
        var executor = new RecordingExecutor();
        var server = new AuthoritativeCommandServer(executor);
        using var scheduler = new ClockGatedCommandScheduler(
            server,
            durablePublication: (_, _) => {
                if (failPublication)
                    throw new InvalidOperationException("same-batch checkpoint failed");
            });

        var failed = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(
                Attack(1, requestedTick: 125, targetEntityId: 1),
                Attack(1, requestedTick: 125, targetEntityId: 99)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);

        scheduler.DrainAndAdvanceTo(125);
        await Assert.ThrowsAsync<InvalidOperationException>(() => AwaitWithin(failed));
        Assert.Equal(0, scheduler.PendingCommandCount);

        failPublication = false;
        var retry = await scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(1, requestedTick: 130, targetEntityId: 99)),
            actualAdmissionTick: 130, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(CommandStatus.Duplicate, Assert.Single(retry.Results).Status);
        Assert.Equal([1UL], executor.ClientSequences);
    }

    [Fact]
    public async Task SubmittedInputAndReturnedAcknowledgementAreDefensivelyCopied()
    {
        var fixture = CreateFixture();
        using var scheduler = fixture.Scheduler;
        var command = Attack(1, requestedTick: 125, targetEntityId: 1);
        var batch = Batch(command);
        var acknowledgementTask = scheduler.SubmitBatchAsync(
            "player-a",
            batch,
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);

        command.AttackRequested.TargetEntityId = 99;
        batch.Commands[0].RequestedTick = 999;
        scheduler.DrainAndAdvanceTo(125);
        var acknowledgement = await AwaitWithin(acknowledgementTask);
        acknowledgement.Results[0].AppliedTick = 999;

        var duplicate = await scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(1, requestedTick: 130, targetEntityId: 100)),
            actualAdmissionTick: 130, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(CommandStatus.Duplicate, Assert.Single(duplicate.Results).Status);
        Assert.Equal(125UL, Assert.Single(duplicate.Results).AppliedTick);
        Assert.Equal([1UL], fixture.Executor.TargetEntityIds);
    }

    [Fact]
    public async Task PublicationFailureProducesNoSuccessAckAndCanBeRedeliveredAsDuplicate()
    {
        var failPublication = true;
        var publicationCount = 0;
        var executor = new RecordingExecutor();
        var server = new AuthoritativeCommandServer(executor);
        using var scheduler = new ClockGatedCommandScheduler(
            server,
            durablePublication: (_, _) => {
            publicationCount++;
            if (failPublication)
                throw new InvalidOperationException("checkpoint failed");
            });

        var failed = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(1, requestedTick: 125, targetEntityId: 1)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);
        scheduler.DrainAndAdvanceTo(125);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => AwaitWithin(failed));
        Assert.Equal("checkpoint failed", exception.Message);
        Assert.Equal(1, executor.CallCount);
        Assert.Equal(1, publicationCount);

        failPublication = false;
        var redelivered = await scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(1, requestedTick: 130, targetEntityId: 99)),
            actualAdmissionTick: 130, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(CommandStatus.Duplicate, Assert.Single(redelivered.Results).Status);
        Assert.Equal(1, executor.CallCount);
        Assert.Equal(2, publicationCount);
    }

    [Fact]
    public async Task PublicationCallbackCancellationStopsAnInitialPreDrainFromAcceptingNewWork()
    {
        var executor = new RecordingExecutor();
        var server = new AuthoritativeCommandServer(executor);
        ClockGatedCommandScheduler? scheduler = null;
        scheduler = new ClockGatedCommandScheduler(
            server,
            durablePublication: (_, acknowledgement) => {
                if (Assert.Single(acknowledgement.Results).ClientSequence == 1)
                    scheduler!.Cancel();
            });
        using var ownedScheduler = scheduler;

        var first = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(1, requestedTick: 120, targetEntityId: 1)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Throws<ObjectDisposedException>(() => {
            _ = scheduler.SubmitBatchAsync(
                "player-a",
                Batch(Attack(2, requestedTick: 130, targetEntityId: 2)),
                actualAdmissionTick: 120, cancellationToken: TestContext.Current.CancellationToken);
        });
        Assert.True(first.IsCanceled);
        Assert.Equal(0, scheduler.PendingCommandCount);
        await Assert.ThrowsAsync<TaskCanceledException>(() => AwaitWithin(first));
    }

    [Fact]
    public async Task PublicationCallbackCannotReenterSubmitDrainOrDispose()
    {
        var executor = new RecordingExecutor();
        var server = new AuthoritativeCommandServer(executor);
        ClockGatedCommandScheduler? scheduler = null;
        scheduler = new ClockGatedCommandScheduler(
            server,
            durablePublication: (sessionId, acknowledgement) => {
                Assert.Equal(1UL, Assert.Single(acknowledgement.Results).ClientSequence);
                Assert.Throws<InvalidOperationException>(() => scheduler!.DrainAndAdvanceTo(125));
                Assert.Throws<InvalidOperationException>(() => {
                    _ = scheduler!.SubmitBatchAsync(
                        "player-a",
                        Batch(Attack(2, requestedTick: 125, targetEntityId: 2)),
                        actualAdmissionTick: 125, cancellationToken: TestContext.Current.CancellationToken);
                });
                Assert.Throws<InvalidOperationException>(() => scheduler!.Dispose());
            });
        using var ownedScheduler = scheduler;

        var acknowledgementTask = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(1, requestedTick: 125, targetEntityId: 1)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);
        scheduler.DrainAndAdvanceTo(125);
        Assert.Equal(CommandStatus.Accepted, Assert.Single((await AwaitWithin(acknowledgementTask)).Results).Status);
        Assert.Equal(1, executor.CallCount);
    }

    [Fact]
    public async Task HostTokenCancellationEndsAllPendingWaitersWithoutExecutingFutureWork()
    {
        var executor = new RecordingExecutor();
        var server = new AuthoritativeCommandServer(executor);
        using var hostCancellation = new CancellationTokenSource();
        using var scheduler = new ClockGatedCommandScheduler(
            server,
            cancellationToken: hostCancellation.Token);

        var original = scheduler.SubmitBatchAsync(
            "player-a", Batch(Attack(1, 125, 1)), actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);
        var duplicate = scheduler.SubmitBatchAsync(
            "player-a", Batch(Attack(1, 125, 99)), actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);
        hostCancellation.Cancel();

        Assert.Equal(0, scheduler.PendingCommandCount);
        Assert.Equal(0, executor.CallCount);
        await Assert.ThrowsAsync<TaskCanceledException>(() => AwaitWithin(original));
        await Assert.ThrowsAsync<TaskCanceledException>(() => AwaitWithin(duplicate));
        Assert.Throws<ObjectDisposedException>(() => scheduler.DrainAndAdvanceTo(125));
    }

    [Fact]
    public void PreCancelledConnectionDoesNotAdvanceOrClaimNewWork()
    {
        var fixture = CreateFixture();
        using var scheduler = fixture.Scheduler;
        using var connectionCancellation = new CancellationTokenSource();
        connectionCancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => {
            _ = scheduler.SubmitBatchAsync(
                "player-a", Batch(Attack(1, 120, 1)), actualAdmissionTick: 123,
                cancellationToken: connectionCancellation.Token);
        });
        Assert.Equal(0, scheduler.PendingCommandCount);
        Assert.Equal(0UL, fixture.Server.CurrentTick);
        Assert.Equal(0, fixture.Executor.CallCount);
    }

    [Fact]
    public async Task CapacityFarFutureAndCancellationRemainBounded()
    {
        var fixture = CreateFixture(maximumPendingCommands: 1);
        using var scheduler = fixture.Scheduler;

        var first = scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(1, requestedTick: ulong.MaxValue, targetEntityId: 1)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, scheduler.PendingCommandCount);
        Assert.Throws<InvalidOperationException>(() => {
            _ = scheduler.SubmitBatchAsync(
                "player-a",
                Batch(Attack(1, requestedTick: ulong.MaxValue, targetEntityId: 99)),
                actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);
        });
        Assert.Throws<InvalidOperationException>(() => {
            _ = scheduler.SubmitBatchAsync(
                "player-a",
                Batch(Attack(2, requestedTick: ulong.MaxValue, targetEntityId: 2)),
                actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);
        });

        Assert.Equal(0, scheduler.DrainAndAdvanceTo(130));
        Assert.False(first.IsCompleted);
        Assert.Equal(0, fixture.Executor.CallCount);

        scheduler.Cancel();
        Assert.True(first.IsCanceled);
        Assert.Equal(0, scheduler.PendingCommandCount);
        Assert.Throws<ObjectDisposedException>(() => scheduler.DrainAndAdvanceTo(ulong.MaxValue));
        await Assert.ThrowsAsync<TaskCanceledException>(() => AwaitWithin(first));
    }

    [Fact]
    public async Task CapacityCountsCompletedDuplicateSlotsAlongsideNewFutureSlots()
    {
        var fixture = CreateFixture(maximumPendingCommands: 1);
        using var scheduler = fixture.Scheduler;

        var completed = await scheduler.SubmitBatchAsync(
            "player-a",
            Batch(Attack(1, requestedTick: 111, targetEntityId: 1)),
            actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(CommandStatus.Accepted, Assert.Single(completed.Results).Status);

        Assert.Throws<InvalidOperationException>(() => {
            _ = scheduler.SubmitBatchAsync(
                "player-a",
                Batch(
                    Attack(1, requestedTick: 111, targetEntityId: 99),
                    Attack(2, requestedTick: ulong.MaxValue, targetEntityId: 2)),
                actualAdmissionTick: 111, cancellationToken: TestContext.Current.CancellationToken);
        });
        Assert.Equal(0, scheduler.PendingCommandCount);
    }

    private static Fixture CreateFixture(int maximumPendingCommands = ClockGatedCommandScheduler.DefaultMaximumPendingCommands)
    {
        var executor = new RecordingExecutor();
        var server = new AuthoritativeCommandServer(executor);
        var publications = new List<CommandAck>();
        var scheduler = new ClockGatedCommandScheduler(
            server,
            durablePublication: (_, acknowledgement) => publications.Add(acknowledgement.Clone()),
            maximumPendingCommands: maximumPendingCommands);
        return new Fixture(server, scheduler, executor, publications);
    }

    private static CommandBatch Batch(params Command[] commands)
    {
        var batch = new CommandBatch();
        batch.Commands.Add(commands);
        return batch;
    }

    private static Task<CommandAck> AwaitWithin(Task<CommandAck> task)
        => task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    private static Command Attack(ulong clientSequence, ulong requestedTick, uint targetEntityId)
    {
        return new Command {
            ClientSequence = clientSequence,
            RequestedTick = requestedTick,
            AttackRequested = new AttackRequested { TargetEntityId = targetEntityId },
        };
    }

    private static Command OpenStore(ulong clientSequence, ulong requestedTick)
    {
        return new Command {
            ClientSequence = clientSequence,
            RequestedTick = requestedTick,
            OpenStoreRequested = new OpenStoreRequested { StoreId = 1 },
        };
    }

    private sealed class Fixture(
        AuthoritativeCommandServer server,
        ClockGatedCommandScheduler scheduler,
        RecordingExecutor executor,
        List<CommandAck> publications)
    {
        public AuthoritativeCommandServer Server { get; } = server;
        public ClockGatedCommandScheduler Scheduler { get; } = scheduler;
        public RecordingExecutor Executor { get; } = executor;
        public List<CommandAck> Publications { get; } = publications;
    }

    private sealed class RecordingExecutor : IAuthoritativeCommandExecutor
    {
        public int CallCount { get; private set; }

        public List<ulong> AppliedTicks { get; } = [];

        public List<ulong> ClientSequences { get; } = [];

        public List<ulong> TargetEntityIds { get; } = [];

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
        {
            CallCount++;
            AppliedTicks.Add(appliedTick);
            ClientSequences.Add(command.ClientSequence);
            TargetEntityIds.Add(command.AttackRequested?.TargetEntityId ?? 0);
            return CommandExecutionResult.Accepted;
        }
    }
}

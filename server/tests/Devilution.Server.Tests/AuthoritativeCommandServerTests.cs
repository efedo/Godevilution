using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeCommandServerTests
{
    [Fact]
    public void DuplicateCommandExecutesOnceAndReusesItsReceipt()
    {
        var executor = new RecordingExecutor();
        var server = new AuthoritativeCommandServer(executor);
        var command = Attack(clientSequence: 1, requestedTick: 10);

        var accepted = server.Process("player-a", command, currentTick: 10);
        var duplicate = server.Process("player-a", command, currentTick: 11);

        Assert.Equal(CommandStatus.Accepted, accepted.Status);
        Assert.Equal(CommandStatus.Duplicate, duplicate.Status);
        Assert.Equal(accepted.ServerReceiptSequence, duplicate.ServerReceiptSequence);
        Assert.Equal(accepted.AppliedTick, duplicate.AppliedTick);
        Assert.Equal(1, executor.CallCount);
    }

    [Fact]
    public void ClientSequencesAreScopedToTheirSession()
    {
        var executor = new RecordingExecutor();
        var server = new AuthoritativeCommandServer(executor);
        var command = Attack(clientSequence: 1, requestedTick: 10);

        var first = server.Process("player-a", command, currentTick: 10);
        var second = server.Process("player-b", command, currentTick: 10);

        Assert.Equal(CommandStatus.Accepted, first.Status);
        Assert.Equal(CommandStatus.Accepted, second.Status);
        Assert.NotEqual(first.ServerReceiptSequence, second.ServerReceiptSequence);
        Assert.Equal(2, executor.CallCount);
    }

    [Fact]
    public void LateGameplayCriticalCommandIsRejectedWithoutExecution()
    {
        var executor = new RecordingExecutor();
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", Purchase(clientSequence: 1, requestedTick: 9), currentTick: 10);

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(CommandRejectReason.TooLate, result.RejectReason);
        Assert.Equal(10UL, result.AppliedTick);
        Assert.Equal(0, executor.CallCount);
    }

    [Fact]
    public void LateNonCriticalCommandIsRescheduledWithinItsTolerance()
    {
        var executor = new RecordingExecutor();
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", OpenStore(clientSequence: 1, requestedTick: 8), currentTick: 10);

        Assert.Equal(CommandStatus.Rescheduled, result.Status);
        Assert.Equal(10UL, result.AppliedTick);
        Assert.Equal(1, executor.CallCount);
        Assert.Equal(10UL, executor.AppliedTicks.Single());
    }

    [Fact]
    public void NonCriticalCommandOutsideItsToleranceIsRejected()
    {
        var executor = new RecordingExecutor();
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", OpenStore(clientSequence: 1, requestedTick: 7), currentTick: 10);

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(CommandRejectReason.TooLate, result.RejectReason);
        Assert.Equal(0, executor.CallCount);
    }

    [Fact]
    public void ExecutionRejectionIsRememberedForRetries()
    {
        var executor = new RecordingExecutor(CommandExecutionResult.Rejected(CommandRejectReason.InvalidTarget));
        var server = new AuthoritativeCommandServer(executor);
        var command = Attack(clientSequence: 1, requestedTick: 10);

        var rejected = server.Process("player-a", command, currentTick: 10);
        var duplicate = server.Process("player-a", command, currentTick: 11);

        Assert.Equal(CommandStatus.Rejected, rejected.Status);
        Assert.Equal(CommandRejectReason.InvalidTarget, rejected.RejectReason);
        Assert.Equal(CommandStatus.Duplicate, duplicate.Status);
        Assert.Equal(CommandRejectReason.InvalidTarget, duplicate.RejectReason);
        Assert.Equal(1, executor.CallCount);
    }

    [Fact]
    public void BatchPreservesWireOrderWhileDeduplicating()
    {
        var executor = new RecordingExecutor();
        var server = new AuthoritativeCommandServer(executor);
        var batch = new CommandBatch {
            Commands = { Attack(clientSequence: 1, requestedTick: 10), Attack(clientSequence: 1, requestedTick: 10) },
        };

        var acknowledgement = server.ProcessBatch("player-a", batch, currentTick: 10);

        Assert.Collection(
            acknowledgement.Results,
            accepted => Assert.Equal(CommandStatus.Accepted, accepted.Status),
            duplicate => Assert.Equal(CommandStatus.Duplicate, duplicate.Status));
        Assert.Equal(1, executor.CallCount);
    }

    [Fact]
    public void MalformedCommandsDoNotReceiveAReceiptOrExecute()
    {
        var executor = new RecordingExecutor();
        var server = new AuthoritativeCommandServer(executor);

        var result = server.Process("player-a", new Command { ClientSequence = 0 }, currentTick: 10);

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(CommandRejectReason.Malformed, result.RejectReason);
        Assert.Equal(0UL, result.ServerReceiptSequence);
        Assert.Equal(0, executor.CallCount);
    }

    [Fact]
    public void EmptyBatchReturnsOneMalformedResultWithoutAReceipt()
    {
        var executor = new RecordingExecutor();
        var server = new AuthoritativeCommandServer(executor);

        var acknowledgement = server.ProcessBatch("player-a", new CommandBatch(), currentTick: 10);

        var result = Assert.Single(acknowledgement.Results);
        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal(CommandRejectReason.Malformed, result.RejectReason);
        Assert.Equal(0UL, result.ServerReceiptSequence);
        Assert.Equal(0, executor.CallCount);
    }

    [Fact]
    public void ExportAndImportPreserveDuplicateResultsAndReceiptSequence()
    {
        var sourceExecutor = new RecordingExecutor();
        var source = new AuthoritativeCommandServer(sourceExecutor);
        var accepted = source.Process("player-a", Attack(7, 10), currentTick: 10);
        var exported = source.ExportCompletedCommands("player-a");

        var replacementExecutor = new RecordingExecutor();
        var replacement = new AuthoritativeCommandServer(replacementExecutor);
        replacement.ImportCompletedCommands("player-a", exported.Values);

        var duplicate = replacement.Process("player-a", Attack(7, 10), currentTick: 11);
        var next = replacement.Process("player-a", Attack(8, 11), currentTick: 11);

        Assert.Equal(CommandStatus.Duplicate, duplicate.Status);
        Assert.Equal(accepted.ServerReceiptSequence, duplicate.ServerReceiptSequence);
        Assert.Equal(CommandStatus.Accepted, next.Status);
        Assert.Equal(accepted.ServerReceiptSequence + 1, next.ServerReceiptSequence);
        Assert.Equal(1, replacementExecutor.CallCount);
    }

    [Fact]
    public void ImportRejectsASecondInitializationForTheSameSession()
    {
        var server = new AuthoritativeCommandServer(new RecordingExecutor());
        server.Process("player-a", Attack(1, 10), currentTick: 10);

        Assert.Throws<InvalidOperationException>(() =>
            server.ImportCompletedCommands(
                "player-a",
                [new CommandResult {
                    ClientSequence = 2,
                    ServerReceiptSequence = 2,
                    AppliedTick = 10,
                    Status = CommandStatus.Accepted,
                }]));
    }

    [Fact]
    public void ImportRejectsReceiptSequenceAlreadyOwnedByAnotherSession()
    {
        var server = new AuthoritativeCommandServer(new RecordingExecutor());
        var receipt = new CommandResult {
            ClientSequence = 1,
            ServerReceiptSequence = 1,
            AppliedTick = 10,
            Status = CommandStatus.Accepted,
            RejectReason = CommandRejectReason.Unspecified,
        };
        server.ImportCompletedCommands("player-a", [receipt]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            server.ImportCompletedCommands("player-b", [receipt]));

        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(server.ExportCompletedCommands("player-b"));
    }

    [Fact]
    public async Task SynchronizedSessionCallbackExcludesConcurrentCommandExecution()
    {
        var executor = new RecordingExecutor();
        var server = new AuthoritativeCommandServer(executor);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var persistence = Task.Run(() => server.WithSynchronizedSession("player-a", () => {
            entered.SetResult();
            release.Task.GetAwaiter().GetResult();
            return true;
        }));
        await entered.Task;

        var command = Task.Run(() => server.Process("player-a", Attack(1, 10), currentTick: 10));
        var completedBeforeRelease = await Task.WhenAny(
            command,
            Task.Delay(100, TestContext.Current.CancellationToken));
        Assert.NotSame(command, completedBeforeRelease);

        release.SetResult();
        Assert.True(await persistence);
        var result = await command;
        Assert.Equal(CommandStatus.Accepted, result.Status);
        Assert.Equal(1, executor.CallCount);
    }

    private static Command Attack(ulong clientSequence, ulong requestedTick)
    {
        return new Command {
            ClientSequence = clientSequence,
            RequestedTick = requestedTick,
            AttackRequested = new AttackRequested { TargetEntityId = 1 },
        };
    }

    private static Command Purchase(ulong clientSequence, ulong requestedTick)
    {
        return new Command {
            ClientSequence = clientSequence,
            RequestedTick = requestedTick,
            PurchaseRequested = new PurchaseRequested { StoreId = 1, StoreSlot = 0 },
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

    private sealed class RecordingExecutor : IAuthoritativeCommandExecutor
    {
        private readonly CommandExecutionResult result;

        public RecordingExecutor(CommandExecutionResult? result = null)
        {
            this.result = result ?? CommandExecutionResult.Accepted;
        }

        public int CallCount { get; private set; }

        public List<ulong> AppliedTicks { get; } = [];

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
        {
            CallCount++;
            AppliedTicks.Add(appliedTick);
            return result;
        }
    }
}

using Devilution.Protocol.V1;
using Devilution.Server.Commands;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeCommandCohortTests
{
    [Fact]
    public void EmptyCaptureDoesNotCreateLedgersOrCallExecutor()
    {
        var executor = new Executor();
        var server = new AuthoritativeCommandServer(executor);

        var captured = server.CaptureCohortState();

        Assert.Equal(0UL, captured.CurrentTick);
        Assert.Equal(1UL, captured.NextServerReceiptSequence);
        Assert.Empty(captured.Sessions);
        Assert.Empty(executor.AdvancedTicks);
        Assert.Equal(0, executor.Executions);
    }

    [Fact]
    public void CaptureDetachesEveryLedgerInOrdinalSessionAndClientSequenceOrder()
    {
        var server = new AuthoritativeCommandServer(new Executor());
        server.Process("z", Attack(9, 11), 11);
        server.Process("a", Attack(4, 11), 11);
        server.Process("A", Attack(7, 11), 11);
        server.Process("a", Attack(2, 10), 11);

        var captured = server.CaptureCohortState();
        captured.Sessions.Single(session => session.SessionId == "a").CommandResults[0].ClientSequence = 999;

        Assert.Equal(["A", "a", "z"], captured.Sessions.Select(session => session.SessionId));
        Assert.Equal([2UL, 4UL], server.CaptureCohortState().Sessions[1].CommandResults.Select(result => result.ClientSequence));
        Assert.Equal(11UL, captured.CurrentTick);
        Assert.Equal(5UL, captured.NextServerReceiptSequence);
        Assert.Equal(4UL, server.LastClientSequence("a"));
    }

    [Fact]
    public void RestoreKeepsAllReceiptsClientFloorsEmptySessionsAndExactCursor()
    {
        var original = new AuthoritativeCommandServer(new Executor());
        original.Process("b", Attack(8, 11), 11);
        var rejected = original.Process("a", Attack(42, 10), 11);
        original.Process("a", Attack(2, 11), 11);
        var source = original.CaptureCohortState();
        source = source with {
            NextServerReceiptSequence = 70,
            Sessions = source.Sessions.Reverse().Append(new("offline", [])).ToArray(),
        };
        var executor = new Executor();
        var restored = new AuthoritativeCommandServer(executor);

        restored.InitializeCohortState(source);

        Assert.Empty(executor.AdvancedTicks);
        Assert.Equal(0, executor.Executions);
        Assert.Equal(11UL, restored.CurrentTick);
        Assert.Equal(42UL, restored.LastClientSequence("a"));
        Assert.Equal(8UL, restored.LastClientSequence("b"));
        Assert.Equal(0UL, restored.LastClientSequence("offline"));
        Assert.Equal(["a", "b", "offline"], restored.CaptureCohortState().Sessions.Select(session => session.SessionId));
        var duplicate = restored.Process("a", Attack(42, 11), 11);
        Assert.Equal(CommandStatus.Duplicate, duplicate.Status);
        Assert.Equal(rejected.RejectReason, duplicate.RejectReason);
        Assert.Equal(rejected.ServerReceiptSequence, duplicate.ServerReceiptSequence);
        Assert.Equal(rejected.AppliedTick, duplicate.AppliedTick);
        Assert.Equal(0, executor.Executions);
        Assert.Equal(70UL, restored.Process("offline", Attack(1, 11), 11).ServerReceiptSequence);
        Assert.Equal(71UL, restored.CaptureCohortState().NextServerReceiptSequence);
    }

    [Fact]
    public void RestoreDoesNotInferCursorFromSurvivingReceipts()
    {
        var original = new AuthoritativeCommandServer(new Executor { ThrowOnExecute = true });
        Assert.Throws<InvalidOperationException>(() => original.Process("failed", Attack(1, 0), 0));
        var captured = original.CaptureCohortState();
        Assert.Equal(2UL, captured.NextServerReceiptSequence);
        Assert.Empty(Assert.Single(captured.Sessions).CommandResults);
        var restored = new AuthoritativeCommandServer(new Executor());

        restored.InitializeCohortState(captured);

        Assert.Equal(2UL, restored.Process("new", Attack(1, 0), 0).ServerReceiptSequence);
    }

    [Fact]
    public void MalformedCommandsKeepTheLegacyZeroReceiptWithoutConsumingInitialization()
    {
        var executor = new Executor();
        var server = new AuthoritativeCommandServer(executor);
        var malformed = server.ProcessBatch("unused", new CommandBatch(), 99).Results.Single();
        Assert.Equal(0UL, malformed.ServerReceiptSequence);
        Assert.Equal(CommandRejectReason.Malformed, malformed.RejectReason);
        Assert.Equal(0UL, server.Process("unused", Attack(0, 99), 99).ServerReceiptSequence);
        Assert.Equal(0UL, server.Process(" ", Attack(1, 99), 99).ServerReceiptSequence);
        var captured = server.CaptureCohortState();
        Assert.Equal(0UL, captured.CurrentTick);
        Assert.Equal(1UL, captured.NextServerReceiptSequence);
        Assert.Empty(captured.Sessions);
        Assert.Empty(executor.AdvancedTicks);
        Assert.Equal(0, executor.Executions);

        server.InitializeCohortState(new(7, 100, []));

        Assert.Equal(7UL, server.CurrentTick);
    }

    [Fact]
    public void RestoreClonesCallerOwnedReceiptsBeforeRegistration()
    {
        var receipt = Receipt(4, 10);
        var server = new AuthoritativeCommandServer(new Executor());
        server.InitializeCohortState(new(11, 20, [new("player", [receipt])]));

        receipt.ClientSequence = 99;
        receipt.ServerReceiptSequence = 100;
        receipt.RejectReason = CommandRejectReason.InvalidTarget;

        var actual = server.ExportCompletedCommands("player").Single();
        Assert.Equal(4UL, actual.Key);
        Assert.Equal(10UL, actual.Value.ServerReceiptSequence);
        Assert.Equal(CommandRejectReason.Unspecified, actual.Value.RejectReason);
    }

    [Fact]
    public void DirectImmediateFutureReceiptRoundTripsWithoutAdvancingTheAdmissionClock()
    {
        var original = new AuthoritativeCommandServer(new Executor());
        var receipt = original.Process("player", Attack(1, 12), 11);
        Assert.Equal(CommandStatus.Accepted, receipt.Status);
        Assert.Equal(12UL, receipt.AppliedTick);
        var captured = original.CaptureCohortState();
        Assert.Equal(11UL, captured.CurrentTick);
        var executor = new Executor();
        var restored = new AuthoritativeCommandServer(executor);

        restored.InitializeCohortState(captured);

        Assert.Equal(11UL, restored.CurrentTick);
        Assert.Empty(executor.AdvancedTicks);
        Assert.Equal(0, executor.Executions);
        var duplicate = restored.Process("player", Attack(1, 12), 11);
        Assert.Equal(CommandStatus.Duplicate, duplicate.Status);
        Assert.Equal(receipt.AppliedTick, duplicate.AppliedTick);
        Assert.Equal(receipt.ServerReceiptSequence, duplicate.ServerReceiptSequence);
        Assert.Equal(11UL, restored.CurrentTick);
        Assert.Equal(0, executor.Executions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactExhaustedCursorDoesNotWrapOrDependOnRetainedMaximum(bool includeMaximum)
    {
        var server = new AuthoritativeCommandServer(new Executor());
        server.InitializeCohortState(new(11, 0, [
            new("player", [Receipt(1, includeMaximum ? ulong.MaxValue : 10)]),
        ]));

        Assert.Equal(0UL, server.CaptureCohortState().NextServerReceiptSequence);
        Assert.Throws<InvalidOperationException>(() => server.Process("new", Attack(1, 11), 11));
        Assert.Equal(0UL, server.CaptureCohortState().NextServerReceiptSequence);
    }

    [Fact]
    public void LegacyImporterRetainsItsExistingMaximumReceiptRejection()
    {
        var server = new AuthoritativeCommandServer(new Executor());

        Assert.Throws<InvalidDataException>(() => server.ImportCompletedCommands("legacy", [Receipt(1, ulong.MaxValue)]));

        Assert.Equal(1UL, server.CaptureCohortState().NextServerReceiptSequence);
        Assert.Empty(server.CaptureCohortState().Sessions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    public void MalformedSecondSessionCannotPartiallyInstallAnyLedgerOrCursor(int fault)
    {
        var executor = new Executor();
        var server = new AuthoritativeCommandServer(executor);
        var bad = Receipt(2, 20);
        var second = new AuthoritativeSessionCommandLedger("second", [bad]);
        var state = new AuthoritativeCommandCohortState(11, 30, [new("first", [Receipt(1, 10)]), second]);
        switch (fault) {
        case 0: state = state with { Sessions = null! }; break;
        case 1: state = state with { Sessions = [new("first", [Receipt(1, 10)]), null!] }; break;
        case 2: second = second with { SessionId = " " }; break;
        case 3: second = second with { SessionId = "first" }; break;
        case 4: second = second with { CommandResults = null! }; break;
        case 5: second = second with { CommandResults = [null!] }; break;
        case 6: bad.ClientSequence = 0; break;
        case 7: bad.ServerReceiptSequence = 0; break;
        case 8: bad.ServerReceiptSequence = 10; break;
        case 9: second = second with { CommandResults = [bad, Receipt(2, 21)] }; break;
        case 10: bad.Status = (CommandStatus)999; break;
        case 11: bad.RejectReason = (CommandRejectReason)999; break;
        case 12: second = second with { CommandResults = [bad, Receipt(3, 20)] }; break;
        case 13: state = state with { NextServerReceiptSequence = 20 }; break;
        case 14: state = state with { NextServerReceiptSequence = 19 }; break;
        }
        if (fault is >= 2 and <= 12)
            state = state with { Sessions = [new("first", [Receipt(1, 10)]), second] };

        Assert.Throws<InvalidDataException>(() => server.InitializeCohortState(state));

        var after = server.CaptureCohortState();
        Assert.Equal(0UL, after.CurrentTick);
        Assert.Equal(1UL, after.NextServerReceiptSequence);
        Assert.Empty(after.Sessions);
        Assert.Empty(executor.AdvancedTicks);
        Assert.Equal(0, executor.Executions);
        server.InitializeCohortState(new(11, 30, [new("valid", [Receipt(1, 10)])]));
        Assert.Equal(11UL, server.CurrentTick);
    }

    [Fact]
    public void EmptyTickZeroInitializationIsOneShotAndNullArgumentDoesNotConsumeIt()
    {
        var server = new AuthoritativeCommandServer(new Executor());
        Assert.Throws<ArgumentNullException>(() => server.InitializeCohortState(null!));
        server.InitializeCohortState(new(0, 1, []));

        Assert.Throws<InvalidOperationException>(() => server.InitializeCohortState(new(0, 1, [])));
        Assert.Empty(server.CaptureCohortState().Sessions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void InitializationRejectsExistingCommandsTimelineOrUntrackedAllocatedReceipts(int priorOperation)
    {
        var server = new AuthoritativeCommandServer(new Executor { ThrowOnExecute = priorOperation == 2 });
        if (priorOperation == 0)
            server.Process("existing", Attack(1, 11), 11);
        else if (priorOperation == 1)
            server.AdvanceTo(11);
        else
            Assert.Throws<InvalidOperationException>(() => server.Process("failed", Attack(1, 0), 0));
        var before = server.CaptureCohortState();

        Assert.Throws<InvalidOperationException>(() => server.InitializeCohortState(new(7, 100, [])));

        var after = server.CaptureCohortState();
        Assert.Equal(before.CurrentTick, after.CurrentTick);
        Assert.Equal(before.NextServerReceiptSequence, after.NextServerReceiptSequence);
        Assert.Equal(before.Sessions.Select(session => session.SessionId), after.Sessions.Select(session => session.SessionId));
    }

    private static Command Attack(ulong sequence, ulong tick) => new() {
        ClientSequence = sequence, RequestedTick = tick,
        AttackRequested = new AttackRequested { TargetEntityId = 9 },
    };

    private static CommandResult Receipt(ulong sequence, ulong receipt) => new() {
        ClientSequence = sequence, ServerReceiptSequence = receipt, AppliedTick = 11,
        Status = CommandStatus.Accepted, RejectReason = CommandRejectReason.Unspecified,
    };

    private sealed class Executor : IAuthoritativeCommandExecutor, IAuthoritativeTickExecutor
    {
        public int Executions { get; private set; }
        public bool ThrowOnExecute { get; init; }
        public List<ulong> AdvancedTicks { get; } = [];

        public CommandExecutionResult Execute(string sessionId, Command command, ulong appliedTick)
        {
            Executions++;
            if (ThrowOnExecute)
                throw new InvalidOperationException("Deliberate execution failure after receipt allocation.");
            return CommandExecutionResult.Accepted;
        }

        public void AdvanceTo(ulong tick) => AdvancedTicks.Add(tick);
    }
}

using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeSessionPersistenceTests
{
    [Fact]
    public void CombinedCheckpointIsPublishedBeforeCompatibilityFailure()
    {
        var primaryRoot = CreateRoot("primary");
        var compatibilityRoot = CreateRoot("compatibility");
        try {
            Directory.CreateDirectory(Path.Combine(compatibilityRoot, "session-a.json"));
            var persistence = new AuthoritativeSessionPersistence(
                new AuthoritativeSaveStore(compatibilityRoot),
                sessionStore: new AuthoritativeSessionStore(primaryRoot));

            var exception = Record.Exception(() => persistence.Save(
                "session-a",
                "authoritative-state",
                new Dictionary<ulong, CommandResult> {
                    [1] = new CommandResult {
                        ClientSequence = 1,
                        ServerReceiptSequence = 1,
                        AppliedTick = 2,
                        Status = CommandStatus.Accepted,
                        RejectReason = CommandRejectReason.Unspecified,
                    },
                },
                "ruleset-a"));
            Assert.NotNull(exception);
            Assert.True(exception is IOException or UnauthorizedAccessException);

            var checkpoint = new AuthoritativeSessionStore(primaryRoot).Load("session-a");
            Assert.NotNull(checkpoint);
            Assert.Equal("authoritative-state", checkpoint.SerializedState);
            Assert.Equal("ruleset-a", checkpoint.RulesetIdentityHash);
            Assert.Single(checkpoint.CommandReceipts);
        } finally {
            DeleteRoot(primaryRoot);
            DeleteRoot(compatibilityRoot);
        }
    }

    [Fact]
    public void CompatibilityMirrorsRoundTripWhenCombinedCheckpointIsDisabled()
    {
        var root = CreateRoot("mirrors");
        try {
            var persistence = new AuthoritativeSessionPersistence(
                new AuthoritativeSaveStore(root),
                new AuthoritativeCommandReceiptStore(root));
            var results = new Dictionary<ulong, CommandResult> {
                [2] = new CommandResult {
                    ClientSequence = 2,
                    ServerReceiptSequence = 3,
                    AppliedTick = 4,
                    Status = CommandStatus.Rejected,
                    RejectReason = CommandRejectReason.InvalidTarget,
                },
            };

            persistence.Save("session-a", "legacy-state", results);

            Assert.Equal("legacy-state", new AuthoritativeSaveStore(root).Load("session-a"));
            Assert.Single(new AuthoritativeCommandReceiptStore(root).Load("session-a"));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void BatchWriteRollsBackEarlierSessionsWhenALaterMirrorFails()
    {
        var primaryRoot = CreateRoot("batch-primary");
        var compatibilityRoot = CreateRoot("batch-compatibility");
        try {
            Directory.CreateDirectory(Path.Combine(compatibilityRoot, "session-b.json"));
            var persistence = new AuthoritativeSessionPersistence(
                new AuthoritativeSaveStore(compatibilityRoot),
                new AuthoritativeCommandReceiptStore(compatibilityRoot),
                new AuthoritativeSessionStore(primaryRoot));
            persistence.Save(
                "session-a",
                "old-state",
                new Dictionary<ulong, CommandResult> {
                    [1] = new CommandResult {
                        ClientSequence = 1,
                        ServerReceiptSequence = 1,
                        AppliedTick = 1,
                        Status = CommandStatus.Accepted,
                        RejectReason = CommandRejectReason.Unspecified,
                    },
                },
                "ruleset-old");

            var exception = Record.Exception(() => persistence.SaveBatch([
                new AuthoritativeSessionWrite(
                    "session-a",
                    "state-a",
                    new Dictionary<ulong, CommandResult>(),
                    "ruleset-new"),
                new AuthoritativeSessionWrite(
                    "session-b",
                    "state-b",
                    new Dictionary<ulong, CommandResult>(),
                    "ruleset-new"),
            ]));

            Assert.NotNull(exception);
            var sessionStore = new AuthoritativeSessionStore(primaryRoot);
            var restored = sessionStore.Load("session-a");
            Assert.NotNull(restored);
            Assert.Equal("old-state", restored.SerializedState);
            Assert.Equal("ruleset-old", restored.RulesetIdentityHash);
            Assert.Single(restored.CommandReceipts);
            Assert.Equal("old-state", new AuthoritativeSaveStore(compatibilityRoot).Load("session-a"));
            Assert.Single(new AuthoritativeCommandReceiptStore(compatibilityRoot).Load("session-a"));
            Assert.Null(sessionStore.Load("session-b"));
            Assert.True(File.Exists(Path.Combine(primaryRoot, "session-a.session.json")));
            Assert.True(File.Exists(Path.Combine(compatibilityRoot, "session-a.json")));
            Assert.True(File.Exists(Path.Combine(compatibilityRoot, "session-a.commands.json")));
        } finally {
            DeleteRoot(primaryRoot);
            DeleteRoot(compatibilityRoot);
        }
    }

    [Fact]
    public void BatchWriteRejectsDuplicateGlobalReceiptsBeforePublishing()
    {
        var root = CreateRoot("batch-duplicate-receipts");
        try {
            var persistence = new AuthoritativeSessionPersistence(
                sessionStore: new AuthoritativeSessionStore(root));
            var first = new CommandResult {
                ClientSequence = 1,
                ServerReceiptSequence = 7,
                AppliedTick = 4,
                Status = CommandStatus.Accepted,
                RejectReason = CommandRejectReason.Unspecified,
            };
            var second = first.Clone();

            var exception = Assert.Throws<InvalidDataException>(() => persistence.SaveBatch([
                new AuthoritativeSessionWrite(
                    "session-a",
                    "state-a",
                    new Dictionary<ulong, CommandResult> { [first.ClientSequence] = first },
                    "ruleset-a"),
                new AuthoritativeSessionWrite(
                    "session-b",
                    "state-b",
                    new Dictionary<ulong, CommandResult> { [second.ClientSequence] = second },
                    "ruleset-a"),
            ]));

            Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(new AuthoritativeSessionStore(root).Load("session-a"));
            Assert.Null(new AuthoritativeSessionStore(root).Load("session-b"));
        } finally {
            DeleteRoot(root);
        }
    }

    private static string CreateRoot(string name)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-session-persistence-tests",
            name,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}

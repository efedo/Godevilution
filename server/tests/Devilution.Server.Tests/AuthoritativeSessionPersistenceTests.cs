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

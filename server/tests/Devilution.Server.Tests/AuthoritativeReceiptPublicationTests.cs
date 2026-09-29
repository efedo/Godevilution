using Devilution.Protocol.V1;
using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeReceiptPublicationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnchangedReceiptsRemainPublishedWhileStateCheckpointsAdvance(bool combined)
    {
        WithRoot(root => {
            var persistence = new AuthoritativeSessionPersistence(new AuthoritativeSaveStore(root),
                new AuthoritativeCommandReceiptStore(root), combined ? new AuthoritativeSessionStore(root) : null);
            var receipts = Receipts();
            persistence.Save("session-a", "state-at-1", receipts, "ruleset");
            var path = Path.Combine(root, "session-a.commands.json");
            var originalBytes = File.ReadAllBytes(path);
            var timestamp = MarkWriteTime(path);

            persistence.Save("session-a", "state-at-2", receipts, "ruleset");

            AssertPublicationTimestamp(path, timestamp);
            Assert.Equal(originalBytes, File.ReadAllBytes(path));
            Assert.Equal("state-at-2", new AuthoritativeSaveStore(root).Load("session-a"));
            if (combined) {
                var checkpoint = new AuthoritativeSessionStore(root).Load("session-a", "ruleset")!;
                Assert.Equal("state-at-2", checkpoint.SerializedState);
                Assert.Equal(5UL, Assert.Single(checkpoint.CommandReceipts).AppliedTick);
            }
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        });
    }

    [Fact]
    public void EmptyLedgerCreationAndRepeatedSaveFollowPlatformPublicationPolicy()
    {
        WithRoot(root => {
            var store = new AuthoritativeCommandReceiptStore(root);
            store.Save("session-a", new Dictionary<ulong, CommandResult>());
            var path = Path.Combine(root, "session-a.commands.json");
            Assert.Equal("[]", File.ReadAllText(path));
            var timestamp = MarkWriteTime(path);
            new AuthoritativeCommandReceiptStore(root).Save("session-a", new Dictionary<ulong, CommandResult>());
            AssertPublicationTimestamp(path, timestamp);
            Assert.Empty(store.Load("session-a"));
        });
    }

    [Fact]
    public void ChangedDeletedAndCorruptMirrorsAreRepublishedWithoutCachedEquality()
    {
        WithRoot(root => {
            var store = new AuthoritativeCommandReceiptStore(root);
            var receipts = Receipts();
            store.Save("session-a", receipts);
            var path = Path.Combine(root, "session-a.commands.json");
            receipts[1].AppliedTick = 6;
            var timestamp = MarkWriteTime(path);
            store.Save("session-a", receipts);
            Assert.NotEqual(timestamp, File.GetLastWriteTimeUtc(path));
            Assert.Equal(6UL, Assert.Single(store.Load("session-a")).AppliedTick);

            File.Delete(path);
            store.Save("session-a", receipts);
            Assert.Equal(6UL, Assert.Single(store.Load("session-a")).AppliedTick);
            var validBytes = File.ReadAllBytes(path);
            var sameLengthCorruption = validBytes.ToArray();
            sameLengthCorruption[0] = 0xFF;
            File.WriteAllBytes(path, sameLengthCorruption);
            store.Save("session-a", receipts);
            Assert.Equal(validBytes, File.ReadAllBytes(path));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        });
    }

    [Fact]
    public void MultiBufferLedgerComparisonIncludesTheTailAndRepairsLengthChanges()
    {
        WithRoot(root => {
            var receipts = Enumerable.Range(1, 100).ToDictionary(index => (ulong)index,
                index => new CommandResult { ClientSequence = (ulong)index,
                    ServerReceiptSequence = (ulong)index, AppliedTick = 5, Status = CommandStatus.Accepted });
            var store = new AuthoritativeCommandReceiptStore(root);
            store.Save("session-a", receipts);
            var path = Path.Combine(root, "session-a.commands.json");
            Assert.True(new FileInfo(path).Length > 8192);
            var timestamp = MarkWriteTime(path);
            store.Save("session-a", receipts);
            AssertPublicationTimestamp(path, timestamp);
            receipts[100].AppliedTick = 6;
            store.Save("session-a", receipts);
            Assert.Equal(6UL, store.Load("session-a")[^1].AppliedTick);
            File.AppendAllText(path, "unexpected-tail");
            store.Save("session-a", receipts);
            Assert.Equal(100, store.Load("session-a").Count);
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        });
    }

    [Fact]
    public void InvalidDesiredReceiptsNeverBypassValidationOrDamageTheExistingMirror()
    {
        WithRoot(root => {
            var store = new AuthoritativeCommandReceiptStore(root);
            var receipts = Receipts();
            store.Save("session-a", receipts);
            var path = Path.Combine(root, "session-a.commands.json");
            var before = File.ReadAllBytes(path);
            receipts[1].ClientSequence = 2;
            Assert.Throws<InvalidDataException>(() => store.Save("session-a", receipts));
            Assert.Equal(before, File.ReadAllBytes(path));
            Assert.Equal(1UL, Assert.Single(store.Load("session-a")).ClientSequence);
        });
    }

    [Fact]
    public void ConcurrentIdenticalStoreInstancesSerializePublication()
    {
        WithRoot(root => {
            new AuthoritativeCommandReceiptStore(root).Save("session-a", Receipts());
            var path = Path.Combine(root, "session-a.commands.json");
            var timestamp = MarkWriteTime(path);
            Parallel.For(0, 16, _ => new AuthoritativeCommandReceiptStore(root).Save("session-a", Receipts()));
            AssertPublicationTimestamp(path, timestamp);
            Assert.Equal(5UL, Assert.Single(new AuthoritativeCommandReceiptStore(root).Load("session-a")).AppliedTick);
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        });
    }

    [Fact]
    public void WindowsNoDeleteSharingAllowsAnIdenticalFlushButRejectsChangedPublicationUntilRetry()
    {
        if (!OperatingSystem.IsWindows())
            return; // Windows sharing semantics; the portable cases above still run.
        WithRoot(root => {
            var receipts = Receipts();
            var persistence = new AuthoritativeSessionPersistence(new AuthoritativeSaveStore(root),
                new AuthoritativeCommandReceiptStore(root), new AuthoritativeSessionStore(root));
            persistence.Save("session-a", "old-state", receipts, "ruleset");
            var path = Path.Combine(root, "session-a.commands.json");
            // Permit read/write handles, but forbid replacement while held.
            using (var noDelete = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                persistence.Save("session-a", "new-state", receipts, "ruleset");
                receipts[1].AppliedTick = 6;
                var failure = Record.Exception(() => persistence.Save("session-a", "changed-command", receipts, "ruleset"));
                Assert.True(failure is IOException or UnauthorizedAccessException, $"Expected a sharing failure, got {failure}");
                Assert.Equal(5UL, Assert.Single(new AuthoritativeCommandReceiptStore(root).Load("session-a")).AppliedTick);
                // The combined checkpoint still precedes compatibility files.
                Assert.Equal(6UL, Assert.Single(new AuthoritativeSessionStore(root).Load("session-a")!.CommandReceipts).AppliedTick);
            }
            persistence.Save("session-a", "changed-command", receipts, "ruleset");
            Assert.Equal(6UL, Assert.Single(new AuthoritativeCommandReceiptStore(root).Load("session-a")).AppliedTick);
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        });
    }

    private static Dictionary<ulong, CommandResult> Receipts() => new() {
        [1] = new CommandResult { ClientSequence = 1, ServerReceiptSequence = 1,
            AppliedTick = 5, Status = CommandStatus.Accepted },
    };

    private static DateTime MarkWriteTime(string path)
    {
        File.SetLastWriteTimeUtc(path, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        return File.GetLastWriteTimeUtc(path);
    }

    private static void AssertPublicationTimestamp(string path, DateTime original)
        // Only Windows enables unchanged-file reuse; others preserve replacement.
        => Assert.Equal(OperatingSystem.IsWindows(), original == File.GetLastWriteTimeUtc(path));

    private static void WithRoot(Action<string> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "Godevilution-Receipt-Publication-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { test(root); }
        finally { Directory.Delete(root, recursive: true); }
    }
}

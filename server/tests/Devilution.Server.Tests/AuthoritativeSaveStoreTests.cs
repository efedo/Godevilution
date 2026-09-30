using Devilution.Server.Stores;
using Devilution.Protocol.V1;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeSaveStoreTests
{
    [Fact]
    public async Task ConcurrentStoreInstancesPublishCompleteAtomicSaves()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-save-store-tests",
            Guid.NewGuid().ToString("N"));
        var payloads = Enumerable.Range(0, 16)
            .Select(index => $"save-{index:D2}-{new string('x', 512)}")
            .ToArray();

        try {
            var stores = payloads
                .Select(_ => new AuthoritativeSaveStore(root))
                .ToArray();
            using var barrier = new Barrier(payloads.Length);
            // Blocking barrier participants need dedicated threads; a bounded
            // test-runner pool can otherwise starve before all writers arrive.
            await Task.WhenAll(payloads.Select((payload, index) => Task.Factory.StartNew(() => {
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("Concurrent save writers did not start together.");
                stores[index].Save("shared-session", payload);
            }, TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default)));

            var persisted = new AuthoritativeSaveStore(root).Load("shared-session");
            Assert.NotNull(persisted);
            Assert.Contains(payloads, payload => payload == persisted);
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        } finally {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CommandReceiptStoreRoundTripsResultsAndUsesSafeSessionPaths()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-command-receipt-store-tests",
            Guid.NewGuid().ToString("N"));
        try {
            var store = new AuthoritativeCommandReceiptStore(root);
            store.Save("session-a", new Dictionary<ulong, CommandResult> {
                [3] = new CommandResult {
                    ClientSequence = 3,
                    ServerReceiptSequence = 9,
                    AppliedTick = 14,
                    Status = CommandStatus.Rejected,
                    RejectReason = CommandRejectReason.InvalidTarget,
                },
            });

            var result = Assert.Single(store.Load("session-a"));
            Assert.Equal(3UL, result.ClientSequence);
            Assert.Equal(9UL, result.ServerReceiptSequence);
            Assert.Equal(14UL, result.AppliedTick);
            Assert.Equal(CommandStatus.Rejected, result.Status);
            Assert.Equal(CommandRejectReason.InvalidTarget, result.RejectReason);
            Assert.Throws<ArgumentException>(() => store.Load("../session-a"));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        } finally {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AllPersistenceStoresRejectInvalidSessionIdsForEveryPathOperation()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-persistence-path-tests",
            Guid.NewGuid().ToString("N"));
        var results = new Dictionary<ulong, CommandResult>();
        try {
            var saveStore = new AuthoritativeSaveStore(root);
            var receiptStore = new AuthoritativeCommandReceiptStore(root);
            var sessionStore = new AuthoritativeSessionStore(root);

            foreach (var sessionId in new[] { "", " ", "../outside", "session.name" }) {
                Assert.Throws<ArgumentException>(() => saveStore.Save(sessionId, "state"));
                Assert.Throws<ArgumentException>(() => saveStore.Load(sessionId));
                Assert.Throws<ArgumentException>(() => saveStore.Delete(sessionId));

                Assert.Throws<ArgumentException>(() => receiptStore.Save(sessionId, results));
                Assert.Throws<ArgumentException>(() => receiptStore.Load(sessionId));
                Assert.Throws<ArgumentException>(() => receiptStore.Exists(sessionId));
                Assert.Throws<ArgumentException>(() => receiptStore.Delete(sessionId));

                Assert.Throws<ArgumentException>(() => sessionStore.Save(sessionId, "state", results));
                Assert.Throws<ArgumentException>(() => sessionStore.Load(sessionId));
                Assert.Throws<ArgumentException>(() => sessionStore.Delete(sessionId));
            }

            saveStore.Save("session-a_1", "state");
            receiptStore.Save("session-a_1", results);
            sessionStore.Save("session-a_1", "state", results);
            Assert.Equal("state", saveStore.Load("session-a_1"));
            Assert.True(receiptStore.Exists("session-a_1"));
            Assert.NotNull(sessionStore.Load("session-a_1"));
        } finally {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CommandReceiptStoreRejectsDuplicateClientSequencesOnDisk()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-command-receipt-store-tests",
            Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(root);
            File.WriteAllText(
                Path.Combine(root, "session-a.commands.json"),
                "[{\"ClientSequence\":1,\"ServerReceiptSequence\":1,\"AppliedTick\":1,\"Status\":1,\"RejectReason\":0},{\"ClientSequence\":1,\"ServerReceiptSequence\":2,\"AppliedTick\":1,\"Status\":1,\"RejectReason\":0}]");

            Assert.Throws<InvalidDataException>(() =>
                new AuthoritativeCommandReceiptStore(root).Load("session-a"));
        } finally {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveAndReceiptStoresRejectMalformedUtf8()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-persistence-encoding-tests",
            Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(root);
            var invalidTextException = Assert.Throws<InvalidDataException>(() =>
                new AuthoritativeSaveStore(root).Save("session-b", "\uD800"));
            Assert.Contains("UTF-16", invalidTextException.Message);
            Assert.False(File.Exists(Path.Combine(root, "session-b.json")));

            File.WriteAllBytes(
                Path.Combine(root, "session-a.json"),
                [0x7B, 0xC3, 0x28]);
            var saveException = Assert.Throws<InvalidDataException>(() =>
                new AuthoritativeSaveStore(root).Load("session-a"));
            Assert.Contains("UTF-8", saveException.Message);

            File.WriteAllBytes(
                Path.Combine(root, "session-a.commands.json"),
                [0x5B, 0xC3, 0x28, 0x5D]);
            var receiptException = Assert.Throws<InvalidDataException>(() =>
                new AuthoritativeCommandReceiptStore(root).Load("session-a"));
            Assert.Contains("UTF-8", receiptException.Message);
        } finally {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SessionStoreRoundTripsStateAndCommandReceiptsAtomically()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-session-store-tests",
            Guid.NewGuid().ToString("N"));
        try {
            var store = new AuthoritativeSessionStore(root);
            store.Save("session-a", "authoritative-state", new Dictionary<ulong, CommandResult> {
                [2] = new CommandResult {
                    ClientSequence = 2,
                    ServerReceiptSequence = 7,
                    AppliedTick = 14,
                    Status = CommandStatus.Rejected,
                    RejectReason = CommandRejectReason.InvalidTarget,
                },
                [5] = new CommandResult {
                    ClientSequence = 5,
                    ServerReceiptSequence = 8,
                    AppliedTick = 16,
                    Status = CommandStatus.Accepted,
                    RejectReason = CommandRejectReason.Unspecified,
                },
            }, "ruleset-v4");

            var document = store.Load("session-a");
            Assert.NotNull(document);
            Assert.Equal(AuthoritativeSessionStore.CurrentFormatVersion, document.FormatVersion);
            Assert.Equal("ruleset-v4", document.RulesetIdentityHash);
            Assert.Equal("authoritative-state", document.SerializedState);
            Assert.Collection(
                document.CommandReceipts,
                first => {
                    Assert.Equal(2UL, first.ClientSequence);
                    Assert.Equal(7UL, first.ServerReceiptSequence);
                    Assert.Equal(CommandStatus.Rejected, first.Status);
                },
                second => {
                    Assert.Equal(5UL, second.ClientSequence);
                    Assert.Equal(8UL, second.ServerReceiptSequence);
                    Assert.Equal(CommandStatus.Accepted, second.Status);
                });
            Assert.Throws<ArgumentException>(() => store.Load("../session-a"));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        } finally {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SessionStoreRejectsCheckpointWrittenForAnotherRuleset()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-session-ruleset-tests",
            Guid.NewGuid().ToString("N"));
        try {
            var store = new AuthoritativeSessionStore(root);
            store.Save(
                "session-a",
                "state",
                new Dictionary<ulong, CommandResult>(),
                "ruleset-a");

            var exception = Assert.Throws<InvalidDataException>(() =>
                store.Load("session-a", "ruleset-b"));
            Assert.Contains("ruleset identity", exception.Message, StringComparison.Ordinal);
            var document = store.Load("session-a", "ruleset-a");
            Assert.NotNull(document);
            Assert.Equal("ruleset-a", document.RulesetIdentityHash);
        } finally {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SessionStoreAcceptsLegacyCheckpointWhenRulesetIdentityIsRequested()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-session-legacy-ruleset-tests",
            Guid.NewGuid().ToString("N"));
        try {
            var store = new AuthoritativeSessionStore(root);
            store.Save("session-a", "legacy-state", new Dictionary<ulong, CommandResult>());

            var document = store.Load("session-a", "current-ruleset");

            Assert.NotNull(document);
            Assert.Equal(3, document.FormatVersion);
            Assert.Null(document.RulesetIdentityHash);
            Assert.Equal("legacy-state", document.SerializedState);
        } finally {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SessionStoreRejectsCurrentCheckpointWithoutRulesetIdentity()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-session-missing-ruleset-tests",
            Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(root);
            var stateHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes("state")));
            var receiptsHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes("[]")));
            File.WriteAllText(
                Path.Combine(root, "session-a.session.json"),
                "{\"FormatVersion\":4,\"SerializedState\":\"state\",\"CommandReceipts\":[],"
                + "\"StateSha256\":\"" + stateHash + "\","
                + "\"CommandReceiptsSha256\":\"" + receiptsHash + "\"}");

            var exception = Assert.Throws<InvalidDataException>(() =>
                new AuthoritativeSessionStore(root).Load("session-a"));
            Assert.Contains("ruleset identity", exception.Message, StringComparison.Ordinal);
        } finally {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SessionStoreWritesStateIntegrityHashAndRejectsTampering()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-session-integrity-tests",
            Guid.NewGuid().ToString("N"));
        try {
            var store = new AuthoritativeSessionStore(root);
            store.Save("session-a", "authoritative-state", new Dictionary<ulong, CommandResult>());

            var path = Path.Combine(root, "session-a.session.json");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var stateHash = document.RootElement.GetProperty("StateSha256").GetString();
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("authoritative-state"))),
                stateHash);

            File.WriteAllText(
                path,
                "{\"FormatVersion\":2,\"SerializedState\":\"tampered-state\",\"CommandReceipts\":[],"
                + "\"StateSha256\":\"" + stateHash + "\"}");

            var exception = Assert.Throws<InvalidDataException>(() => store.Load("session-a"));
            Assert.Contains("integrity hash", exception.Message, StringComparison.Ordinal);
        } finally {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SessionStoreAcceptsVersionOneCheckpointsForMigration()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-session-migration-tests",
            Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(root);
            File.WriteAllText(
                Path.Combine(root, "session-a.session.json"),
                "{\"FormatVersion\":1,\"SerializedState\":\"legacy-state\",\"CommandReceipts\":[]}");

            var document = new AuthoritativeSessionStore(root).Load("session-a");

            Assert.NotNull(document);
            Assert.Equal(1, document.FormatVersion);
            Assert.Equal("legacy-state", document.SerializedState);
            Assert.Empty(document.CommandReceipts);
        } finally {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SessionStoreRejectsMissingVersionTwoIntegrityHash()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-session-integrity-missing-tests",
            Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(root);
            File.WriteAllText(
                Path.Combine(root, "session-a.session.json"),
                "{\"FormatVersion\":2,\"SerializedState\":\"state\",\"CommandReceipts\":[]}");

            var exception = Assert.Throws<InvalidDataException>(() =>
                new AuthoritativeSessionStore(root).Load("session-a"));
            Assert.Contains("integrity hash", exception.Message, StringComparison.Ordinal);
        } finally {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SessionStoreWritesReceiptIntegrityHashAndRejectsTampering()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-session-receipt-integrity-tests",
            Guid.NewGuid().ToString("N"));
        try {
            var store = new AuthoritativeSessionStore(root);
            store.Save("session-a", "state", new Dictionary<ulong, CommandResult> {
                [2] = new CommandResult {
                    ClientSequence = 2,
                    ServerReceiptSequence = 7,
                    AppliedTick = 14,
                    Status = CommandStatus.Accepted,
                    RejectReason = CommandRejectReason.Unspecified,
                },
            });

            var path = Path.Combine(root, "session-a.session.json");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var stateHash = document.RootElement.GetProperty("StateSha256").GetString();
            var receiptHash = document.RootElement.GetProperty("CommandReceiptsSha256").GetString();
            var tampered = "{\"FormatVersion\":3,\"SerializedState\":\"state\","
                + "\"CommandReceipts\":[{\"ClientSequence\":2,\"ServerReceiptSequence\":7,"
                + "\"AppliedTick\":15,\"Status\":1,\"RejectReason\":0}],"
                + "\"StateSha256\":\"" + stateHash + "\","
                + "\"CommandReceiptsSha256\":\"" + receiptHash + "\"}";
            File.WriteAllText(path, tampered);

            var exception = Assert.Throws<InvalidDataException>(() => store.Load("session-a"));
            Assert.Contains("command-ledger integrity hash", exception.Message, StringComparison.Ordinal);
        } finally {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SessionStoreAcceptsVersionTwoCheckpointsForMigration()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-session-format-two-tests",
            Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(root);
            var stateHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes("state")));
            File.WriteAllText(
                Path.Combine(root, "session-a.session.json"),
                "{\"FormatVersion\":2,\"SerializedState\":\"state\",\"CommandReceipts\":[],"
                + "\"StateSha256\":\"" + stateHash + "\"}");

            var document = new AuthoritativeSessionStore(root).Load("session-a");

            Assert.NotNull(document);
            Assert.Equal(2, document.FormatVersion);
            Assert.Equal("state", document.SerializedState);
            Assert.Empty(document.CommandReceipts);
        } finally {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SessionStoreRejectsMissingVersionThreeReceiptIntegrityHash()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-session-receipt-integrity-missing-tests",
            Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(root);
            var stateHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes("state")));
            File.WriteAllText(
                Path.Combine(root, "session-a.session.json"),
                "{\"FormatVersion\":3,\"SerializedState\":\"state\",\"CommandReceipts\":[],"
                + "\"StateSha256\":\"" + stateHash + "\"}");

            var exception = Assert.Throws<InvalidDataException>(() =>
                new AuthoritativeSessionStore(root).Load("session-a"));
            Assert.Contains("command-ledger integrity hash", exception.Message, StringComparison.Ordinal);
        } finally {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ConcurrentSessionStoreInstancesPublishCompleteCheckpoints()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-session-store-tests",
            Guid.NewGuid().ToString("N"));
        var states = Enumerable.Range(0, 12)
            .Select(index => (State: $"state-{index:D2}", Sequence: (ulong)(index + 1)))
            .ToArray();

        try {
            var stores = states.Select(_ => new AuthoritativeSessionStore(root)).ToArray();
            using var barrier = new Barrier(states.Length);
            // Blocking a thread-pool worker at the barrier can starve the other
            // writers during the full suite. Dedicated workers preserve genuine
            // simultaneous admission without relaxing the five-second deadline.
            await Task.WhenAll(states.Select((entry, index) => Task.Factory.StartNew(() => {
                if (!barrier.SignalAndWait(
                        TimeSpan.FromSeconds(5),
                        TestContext.Current.CancellationToken))
                    throw new TimeoutException("Concurrent session writers did not start together.");
                stores[index].Save("shared-session", entry.State, new Dictionary<ulong, CommandResult> {
                    [entry.Sequence] = new CommandResult {
                        ClientSequence = entry.Sequence,
                        ServerReceiptSequence = entry.Sequence,
                        AppliedTick = entry.Sequence,
                        Status = CommandStatus.Accepted,
                        RejectReason = CommandRejectReason.Unspecified,
                    },
                });
            }, TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default)));

            var persisted = new AuthoritativeSessionStore(root).Load("shared-session");
            Assert.NotNull(persisted);
            var expected = states.Single(entry => entry.State == persisted.SerializedState);
            var receipt = Assert.Single(persisted.CommandReceipts);
            Assert.Equal(expected.Sequence, receipt.ClientSequence);
            Assert.Equal(expected.Sequence, receipt.ServerReceiptSequence);
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        } finally {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SessionStoreRejectsMalformedOrDuplicateReceiptsOnDisk()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-session-store-tests",
            Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(root);
            File.WriteAllText(
                Path.Combine(root, "session-a.session.json"),
                "{\"SerializedState\":\"state\",\"CommandReceipts\":["
                + "{\"ClientSequence\":1,\"ServerReceiptSequence\":1,\"AppliedTick\":1,\"Status\":1,\"RejectReason\":0},"
                + "{\"ClientSequence\":2,\"ServerReceiptSequence\":1,\"AppliedTick\":1,\"Status\":1,\"RejectReason\":0}]}" );

            Assert.Throws<InvalidDataException>(() =>
                new AuthoritativeSessionStore(root).Load("session-a"));
        } finally {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SessionStoreRejectsMalformedUtf8BeforeJsonParsing()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-session-store-tests",
            Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(root);
            var invalidTextException = Assert.Throws<InvalidDataException>(() =>
                new AuthoritativeSessionStore(root).Save(
                    "session-b",
                    "\uD800",
                    new Dictionary<ulong, CommandResult>()));
            Assert.Contains("UTF-16", invalidTextException.Message);
            Assert.False(File.Exists(Path.Combine(root, "session-b.session.json")));

            File.WriteAllBytes(
                Path.Combine(root, "session-a.session.json"),
                [0x7B, 0xC3, 0x28, 0x7D]);

            var exception = Assert.Throws<InvalidDataException>(() =>
                new AuthoritativeSessionStore(root).Load("session-a"));
            Assert.Contains("UTF-8", exception.Message);
        } finally {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}

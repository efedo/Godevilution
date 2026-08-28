using Devilution.Server.Stores;
using Devilution.Protocol.V1;
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
            await Task.WhenAll(payloads.Select((payload, index) => Task.Run(() => {
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("Concurrent save writers did not start together.");
                stores[index].Save("shared-session", payload);
            })));

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
}

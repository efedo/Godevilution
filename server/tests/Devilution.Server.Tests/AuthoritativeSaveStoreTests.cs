using Devilution.Server.Stores;
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
}

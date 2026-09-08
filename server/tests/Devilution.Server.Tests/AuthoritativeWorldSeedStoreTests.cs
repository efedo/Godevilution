using Devilution.Server.Stores;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class AuthoritativeWorldSeedStoreTests
{
    [Fact]
    public void ExplicitSeedIsWrittenAndReusedAcrossStoreInstances()
    {
        var root = CreateRoot();
        try {
            var first = new AuthoritativeWorldSeedStore(root);
            Assert.Equal(0x12345678U, first.LoadOrCreate(0x12345678U));

            var second = new AuthoritativeWorldSeedStore(root);
            Assert.Equal(0x12345678U, second.LoadOrCreate());
            Assert.Equal(
                "305419896",
                File.ReadAllText(Path.Combine(root, AuthoritativeWorldSeedStore.FileName)).Trim());
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void GeneratedSeedPersistsWhenNoExplicitSeedIsProvided()
    {
        var root = CreateRoot();
        try {
            var first = new AuthoritativeWorldSeedStore(root).LoadOrCreate();
            var second = new AuthoritativeWorldSeedStore(root).LoadOrCreate();

            Assert.Equal(first, second);
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ConcurrentStoreInstancesShareOneGeneratedSeed()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-world-seed-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var stores = Enumerable.Range(0, 16)
                .Select(_ => new AuthoritativeWorldSeedStore(root))
                .ToArray();
            var seeds = await Task.WhenAll(stores.Select(store =>
                Task.Run(() => store.LoadOrCreate())));

            Assert.NotEmpty(seeds);
            Assert.All(seeds, seed => Assert.Equal(seeds[0], seed));
            Assert.Equal(seeds[0], new AuthoritativeWorldSeedStore(root).LoadOrCreate());
        } finally {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void RejectsConflictingExplicitSeedAndMalformedPersistence()
    {
        var root = CreateRoot();
        try {
            var store = new AuthoritativeWorldSeedStore(root);
            store.LoadOrCreate(7);
            Assert.Throws<InvalidDataException>(() => store.LoadOrCreate(8));

            File.WriteAllText(
                Path.Combine(root, AuthoritativeWorldSeedStore.FileName),
                "not-a-seed");
            Assert.Throws<InvalidDataException>(() => store.LoadOrCreate());

            File.WriteAllBytes(
                Path.Combine(root, AuthoritativeWorldSeedStore.FileName),
                [0x33, 0xC3, 0x28]);
            var exception = Assert.Throws<InvalidDataException>(() => store.LoadOrCreate());
            Assert.Contains("UTF-8", exception.Message);
        } finally {
            DeleteRoot(root);
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "godevilution-world-seed-tests",
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

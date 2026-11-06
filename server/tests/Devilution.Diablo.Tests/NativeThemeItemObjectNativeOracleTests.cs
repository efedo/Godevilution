using System.Text.Json;
using Xunit;
using static Devilution.Diablo.Tests.AuthoritativeCathedralThemeItemNativeOracleTests;

namespace Devilution.Diablo.Tests;

/** Comparison-only corpus from the real archive-free OperateObject and item path. */
public sealed class NativeThemeItemObjectNativeOracleTests
{
    [Fact]
    [Trait("Category", "Slow")]
    public void EveryItemObjectMatchesNativeAtEveryDepthInBothGameModes()
    {
        var root = Environment.GetEnvironmentVariable("GODEVILUTION_THEME_ITEM_OBJECTS_A_ORACLE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(root)) Assert.Skip("Run tools/run-native-oracle-parity.sh for the part A item-object capture.");
        var path = Path.Combine(root, "native-theme-item-objects-a.json");
        Assert.InRange(new FileInfo(path).Length, 1, 16 * 1024 * 1024);
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var corpus = document.RootElement;
        Assert.Equal("godevilution-theme-item-objects-a", S(corpus, "format"));
        Assert.Equal(1, N(corpus, "version"));
        Assert.Equal(3456, N(corpus, "caseCount"));
        var catalog = ReadCatalog(corpus.GetProperty("catalog"));
        var empty = ReadItem(corpus.GetProperty("emptyItem"));
        var rows = corpus.GetProperty("cases").EnumerateArray().ToArray();
        var keys = rows.Select(row => (N(row, "depth"), B(row, "multiplayer"), N(row, "type"), U(row, "seed"), N(row, "variant"))).ToHashSet();
        Assert.Equal(3456, rows.Length);
        Assert.Equal(rows.Length, keys.Count);
        foreach (var depth in Enumerable.Range(1, 8))
            foreach (var multiplayer in new[] {false, true})
                foreach (var type in new[] {61, 62, 63, 64, 67, 77, 89, 90, 92})
                    foreach (var seed in new uint[] {0, 1, 2588, 0x80000000, uint.MaxValue, 0x12345678})
                        foreach (var variant in Enumerable.Range(0, 4))
                            Assert.Contains((depth, multiplayer, type, seed, variant), keys);
        var bodyDrops = rows.Where(row => N(row, "type") == 67 && N(row, "variant") == 0)
            .Select(row => ReadItem(row.GetProperty("item"))).ToArray();
        Assert.Contains(bodyDrops, item => item.ItemType == 11);
        Assert.Contains(bodyDrops, item => item.ItemType >= 0 && item.ItemType != 11);
        var count = 0;
        foreach (var row in rows) {
            ++count;
            var depth = N(row, "depth"); var type = N(row, "type"); var variant = N(row, "variant");
            var name = $"L{depth}/type{type}/seed{U(row, "seed")}/variant{variant}/MP{B(row, "multiplayer")}";
            const int cells = 112 * 112;
            var objectRefs = new int[cells]; objectRefs[50 * 112 + 50] = 1;
            // PrepareRoom retains a walking player and monster in these two signed cells.
            var players = new int[cells]; players[50 * 112 + 50] = -1;
            var monsters = new int[cells]; monsters[50 * 112 + 51] = -1;
            var input = new AuthoritativeCathedralThemeItemWorldState(
                Enumerable.Repeat(empty, 128).ToArray(), Enumerable.Range(0, 127).ToArray(), variant == 1 ? 127 : 0,
                new int[cells], players, monsters, objectRefs,
                Enumerable.Repeat(variant == 2, cells).ToArray(), new bool[cells], [true], [variant != 3]);
            var policy = new AuthoritativeCathedralThemeItemPolicy(depth,
                B(row, "multiplayer") ? DiabloGameMode.Multiplayer : DiabloGameMode.SinglePlayer);
            var request = new AuthoritativeNativeThemeItemLootRequest(input, catalog, policy,
                type, 0, 50, 50, U(row, "seed"), N(row, "initialFrame"),
                new(true, true, N(corpus, "playerMaximumMana"), N(corpus, "playerMaximumLife")));
            if (variant == 3) {
                Assert.Throws<InvalidDataException>(() => AuthoritativeNativeThemeItemLootGenerator.Generate(request));
                Assert.Equal(N(row, "initialFrame"), N(row, "frame"));
                Assert.Equal(0x12345678U, U(row, "randomState"));
                continue;
            }
            var generated = AuthoritativeNativeThemeItemLootGenerator.Generate(request);
            Assert.True(generated.AnimationFrame == N(row, "frame"), name + " frame");
            Assert.Equal(0, N(row, "selection"));
            Assert.True(generated.Loot.FinalRandomState == U(row, "randomState"), name + " RNG");
            Assert.True(generated.Loot.WorldState.ActiveItemCount == N(row, "activeCount"), name + " pool");
            Assert.Equal(ReadItem(row.GetProperty("item")), generated.Loot.WorldState.Items[0]);
            Assert.Equal(Flags(row, "uniqueFlags").Order(), generated.Loot.WorldState.UniqueItemFlags.Order());
            Assert.False(generated.Loot.WorldState.ObjectCanInteract[0]);
            Assert.True(input.ObjectCanInteract[0]);
            Assert.Equal(U(row, "randomState"), U(row, "repeatedRandomState"));
        }
        Assert.Equal(3456, count);
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using Devilution.Client.Protocol;
using Devilution.Diablo;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Devilution.Tests;
using Google.Protobuf;
using Xunit;
using static Devilution.Server.Tests.NativeShrineHostPublicationTests;

namespace Devilution.Server.Tests;

/** Every captured item kind crosses real content binding, owned/floor publication and fresh-owner restore. */
public sealed class NativeThemeCorpusItemPublicationTests(ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions Json = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private readonly record struct Context(int Object, int Depth, bool Multiplayer);
    // Spell and cursor are dynamic for books/staves/gold. Keep unique/magic and raw
    // stat/identification lanes, but do not repeat identical kinds for every RNG seed.
    private readonly record struct Kind(int Index, int Type, int Misc, int Spell, int Magic, int Unique, int Cursor, bool Stat, bool Identified);
    private sealed record Sample(Context Context, Kind Kind, AuthoritativeCathedralInitialItemState Raw, string Origin);

    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    [Trait("Category", "Slow")]
    public void EveryCapturedItemKindPublishesDropsAndRestoresWithProductionBindings(string part)
    {
        var variable = part == "A" ? "GODEVILUTION_THEME_ITEM_OBJECTS_A_ORACLE_DIRECTORY" : "GODEVILUTION_THEME_EFFECTS_PART_B_ORACLE_DIRECTORY";
        var directory = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(directory)) Assert.Skip("Run tools/run-native-oracle-parity.sh for both theme-object corpora.");
        var file = part == "A" ? "native-theme-item-objects-a.json" : "native-theme-effects-part-b.json";
        var path = Path.Combine(directory!, file);
        Assert.InRange(new FileInfo(path).Length, 1, 32 * 1024 * 1024);
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        Assert.Equal(part == "A" ? "godevilution-theme-item-objects-a" : "godevilution-native-theme-effects-part-b", root.GetProperty("format").GetString());
        Assert.Equal(part == "A" ? 1 : 2, root.GetProperty("version").GetInt32());
        var rows = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(root.GetProperty("caseCount").GetInt32(), rows.Length);
        var contexts = rows.Select(row => Scope(row, part)).ToHashSet();
        foreach (var type in contexts.Select(value => value.Object).Distinct())
            foreach (var depth in Enumerable.Range(1, 8))
                foreach (var multiplayer in new[] {false, true})
                    Assert.Contains(new Context(type, depth, multiplayer), contexts);
        var samples = new List<Sample>();
        foreach (var row in rows) {
            var context = Scope(row, part);
            var rawItems = part == "A" ? new[] {row.GetProperty("item")}
                : row.GetProperty("groundItems").EnumerateArray().Concat(row.GetProperty("inventoryItems").EnumerateArray()).ToArray();
            if (part == "B") {
                Assert.Equal(row.GetProperty("groundCount").GetInt32(), row.GetProperty("groundItems").GetArrayLength());
                Assert.Equal(row.GetProperty("expected").GetProperty("goldPiles").GetArrayLength(), row.GetProperty("inventoryItems").GetArrayLength());
            }
            foreach (var value in rawItems) {
                var raw = value.Deserialize<AuthoritativeCathedralInitialItemState>(Json)!;
                if (raw.ItemType < 0) continue;
                AuthoritativeCathedralInitialItemGenerator.ValidateItemProjection(raw);
                var kind = new Kind(raw.ItemIndex, raw.ItemType, raw.MiscId, raw.SpellId, raw.Magical,
                    raw.UniqueId, raw.CursorGraphic, raw.StatFlag, raw.Identified);
                samples.Add(new(context, kind, raw, $"{context}/seed{row.GetProperty("seed").GetUInt32()}/{kind}"));
            }
        }
        Assert.NotEmpty(samples);
        var distinct = samples.DistinctBy(sample => (sample.Context, sample.Kind)).ToArray();
        var failures = new List<string>();
        foreach (var group in distinct.GroupBy(sample => sample.Context)) {
            try { Exercise(group.Key, group.ToArray()); }
            catch (Exception exception) { failures.Add($"{group.Key}: {exception.Message}"); output.WriteLine(exception.ToString()); }
        }
        output.WriteLine($"Part {part}: {rows.Length} rows, {samples.Count} items, {distinct.Length} representatives, {distinct.Select(value => value.Kind).Distinct().Count()} kinds, {distinct.Select(value => value.Context).Distinct().Count()} item-producing contexts.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static Context Scope(JsonElement row, string part)
        => new(row.GetProperty(part == "A" ? "type" : "object").GetInt32(), row.GetProperty("depth").GetInt32(), row.GetProperty("multiplayer").GetBoolean());

    private void Exercise(Context context, IReadOnlyList<Sample> samples)
    {
        var mode = context.Multiplayer ? DiabloGameMode.Multiplayer : DiabloGameMode.SinglePlayer;
        var fixture = new NativeShrineTestFixture(context.Depth, 0, mode: mode, nativeObjectType: context.Object, productionItemBindings: true);
        var hero = Player(fixture, context.Depth);
        hero.Inventory.Clear(); hero.Equipment.Clear(); hero.Belt.Clear(); hero.Gold = 0;
        for (var cell = 0; cell < hero.InventoryGrid.Count; cell++) hero.InventoryGrid[cell] = -1;
        var profile = fixture.ItemProfile;
        var rawRuntime = new AuthoritativeCathedralItemRuntime(profile, profile.InitialCheckpoint, null,
            new Dictionary<uint, AuthoritativeNativeDungeonFamilyContext> {
                [(uint)context.Depth] = context.Depth <= 4 ? AuthoritativeNativeDungeonFamilyContext.Cathedral(context.Depth)
                    : AuthoritativeNativeDungeonFamilyContext.Catacombs(context.Depth),
            });
        ulong tick = 0;
        foreach (var (sample, index) in samples.Select((value, index) => (value, index))) {
            output.WriteLine(sample.Origin);
            hero.PositionX = 45 + index * 3; hero.PositionY = 60;
            var raw = sample.Raw with {PositionX = hero.PositionX, PositionY = hero.PositionY + 1};
            var binding = profile.ItemBindings[raw.ItemIndex];
            var definition = fixture.Items.GetRequired(binding.StableItemId);
            Assert.Equal(definition.StableSymbol, binding.StableSymbol);
            Assert.Equal(definition.ItemActionKind, binding.ItemActionKind);
            var projection = rawRuntime.ProjectNativeItem(0, 0x0A000000U + (uint)index, raw.Seed,
                (uint)Math.Max(0, raw.Identified ? raw.IdentifiedValue : raw.Value), raw, binding, profile.ContentInventoryDimensions);
            fixture.World.AddWorldItem(new(projection.EntityId, (uint)context.Depth, raw.PositionX, raw.PositionY,
                projection.ItemSeed, projection.Price, projection.State));
            Admit(fixture.Simulation.CapturePresentationSnapshot(hero, ++tick), fixture);
            var pickup = fixture.Simulation.Execute(hero, new DiabloPickupWorldItemIntent(projection.EntityId), ++tick, [hero]);
            Assert.True(pickup.Succeeded, $"{sample.Origin}: pickup {pickup.Failure}");
            var owned = Assert.Single(hero.Inventory);
            Assert.Equal(binding.StableItemId, owned.State.StableItemId);
            Admit(fixture.Simulation.CapturePresentationSnapshot(hero, tick), fixture);
            var drop = fixture.Simulation.Execute(hero, new DiabloDropItemIntent(new(DiabloPlayerItemLocation.Inventory, 0, binding.StableItemId),
                hero.PositionX + 1, hero.PositionY + 1), ++tick, [hero]);
            Assert.True(drop.Succeeded, $"{sample.Origin}: drop {drop.Failure}");
            Assert.Empty(hero.Inventory);
            Admit(fixture.Simulation.CapturePresentationSnapshot(hero, tick), fixture);
        }
        var saved = fixture.Simulation.CaptureSnapshot(hero, tick);
        Assert.Equal(samples.Count, saved.WorldItems.Count);
        var codec = new DiabloCohortCodec();
        var bytes = codec.Serialize(Checkpoint(fixture, hero, saved, context.Depth));
        var decoded = codec.Deserialize(bytes);
        Assert.Equal(bytes, codec.Serialize(decoded));
        var pool = decoded.CathedralItems! with {Levels = decoded.CathedralItems!.Levels.Where(value => value.LevelId == (uint)context.Depth).ToArray()};
        var fresh = new NativeShrineTestFixture(context.Depth, 0, mode: mode, restoredItems: pool,
            nativeObjectType: context.Object, productionItemBindings: true);
        var restored = Player(fresh, context.Depth);
        fresh.Simulation.RestoreSnapshot(restored, saved with {
            Player = Assert.Single(decoded.Participants).Player, Objects = decoded.Objects,
            WorldItems = decoded.WorldItems, Projectiles = decoded.Projectiles,
        });
        var recaptured = fresh.Simulation.CaptureSnapshot(restored, tick);
        Assert.Equal(saved.WorldItems, recaptured.WorldItems);
        Assert.Equal(JsonSerializer.Serialize(fixture.World.CaptureCathedralItems()), JsonSerializer.Serialize(fresh.World.CaptureCathedralItems()));
        Admit(fresh.Simulation.CapturePresentationSnapshot(restored, tick), fresh);
    }

    private static void Admit(DiabloSimulationSnapshot snapshot, NativeShrineTestFixture fixture)
    {
        var wire = DiabloProtocolMapper.ToProtocolSnapshot(snapshot, spells: fixture.Spells);
        AuthoritativeSnapshotValidator.Validate(wire);
        var envelope = Envelope.Parser.ParseFrom(new Envelope {Snapshot = wire}.ToByteArray());
        var client = new AuthoritativeClientModel();
        client.Apply(new AuthoritativeClientMessage(Snapshot: envelope.Snapshot));
        Assert.Equal(wire.Tick, client.Snapshot!.Tick);
        Assert.Equal(wire.WorldItems.Count, client.Snapshot.WorldItems.Count);
    }

}

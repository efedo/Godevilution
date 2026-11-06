using Devilution.Client.Protocol;
using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Protocol.V1;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Devilution.Tests;
using Google.Protobuf;
using Xunit;

namespace Devilution.Server.Tests;

/** Real native shrine owners, module snapshots, live host publication, wire/client admission and fresh-world restore. */
public sealed partial class NativeShrineHostPublicationTests
{
    public static IEnumerable<object[]> Shrines => Enumerable.Range(0, 26).Select(type => new object[] { type });

    [Theory]
    [MemberData(nameof(Shrines))]
    public async Task ShrineStatePublishesAndRestoresAcrossItsCompleteLifecycle(int type)
    {
        foreach (var depth in new[] { 1, 8 }) {
            var fixture = new NativeShrineTestFixture(depth, type);
            var hero = Player(fixture, depth);
            var template = DiabloPlayerTemplate.CreateDefault(0, 50, 51, (uint)depth);
            var runtime = fixture.Simulation.CreateRuntime(template);
            runtime.AddParticipant("hero", hero);
            var result = fixture.Simulation.Execute(hero, new DiabloOperateObjectIntent(NativeShrineTestFixture.ShrineEntityId), 0, [hero]);
            Assert.True(result.Succeeded);
            var batch = DiabloProtocolMapper.ToProtocolEventBatch(result.Events, hero.EntityId, 0)!;
            Assert.NotEmpty(batch.Events);
            Assert.False(string.IsNullOrEmpty(batch.Events[0].Dialogue.Text));
            var eventModel = new AuthoritativeClientModel();
            eventModel.Apply(new AuthoritativeClientMessage(Events: EventBatch.Parser.ParseFrom(batch.ToByteArray())));
            void Advance(ulong tick) => runtime.AdvanceTo(tick);
            Snapshot Build(ulong tick) => DiabloProtocolMapper.ToProtocolSnapshot(fixture.Simulation.CapturePresentationSnapshot(hero, tick), spells: fixture.Spells);
            await using var host = new NativeTrapHostPublicationTests.SnapshotHost(0, Build, Advance);
            await host.ConnectAsync();
            var boundaries = new HashSet<string>();
            for (ulong tick = 0; tick <= 300; tick++) {
                var wire = tick == 0 ? host.Model.Snapshot! : await host.PollAsync(tick);
                Assert.Equal(tick, wire.Tick);
                Assert.True(wire.Objects.Single(value => value.EntityId == NativeShrineTestFixture.ShrineEntityId).Activated);
                var signature = string.Join(',', wire.Projectiles.Select(projectile => projectile.Behavior).Distinct().Order());
                var final = fixture.World.Projectiles.Count == 0;
                if (tick == 0 || boundaries.Add(signature) || final) {
                    var saved = fixture.Simulation.CaptureSnapshot(hero, tick);
                    var checkpoint = Checkpoint(fixture, hero, saved, depth);
                    var codec = new DiabloCohortCodec();
                    var bytes = codec.Serialize(checkpoint);
                    var decoded = codec.Deserialize(bytes);
                    Assert.Equal(bytes, codec.Serialize(decoded));
                    var pool = decoded.CathedralItems! with { Levels = decoded.CathedralItems!.Levels
                        .Where(value => value.LevelId == (uint)depth).ToArray() };
                    var fresh = new NativeShrineTestFixture(depth, type, restoredItems: pool);
                    var restored = Player(fresh, depth);
                    fresh.Simulation.RestoreSnapshot(restored, saved with {
                        Player = Assert.Single(decoded.Participants).Player,
                        Objects = decoded.Objects, WorldItems = decoded.WorldItems, Projectiles = decoded.Projectiles,
                    });
                    var recaptured = fresh.Simulation.CaptureSnapshot(restored, tick);
                    Assert.Equal(saved.Objects, recaptured.Objects);
                    Assert.Equal(saved.Projectiles, recaptured.Projectiles);
                    Assert.Equal(saved.Player.Mana, restored.Mana);
                    Assert.Equal(saved.Player.Gold, restored.Gold);
                    Assert.Equal(saved.Player.ManaShieldActive, restored.ManaShieldActive);
                    Assert.Equal(saved.Player.NativeLifeBase, restored.NativeLifeBase);
                    Assert.Equal(saved.Player.NativeManaBase, restored.NativeManaBase);
                    Assert.Equal(saved.Player.ManaMaximum, restored.ManaMaximum);
                    Assert.Equal(saved.Player.Attributes, restored.Attributes);
                    Assert.Equal(saved.Player.Inventory, restored.Inventory);
                    Assert.Equal(saved.Player.Equipment, restored.Equipment);
                    Assert.Equal(saved.Player.Belt, restored.Belt);
                    Assert.Equal(saved.Player.MemorizedSpells.OrderBy(value => value.SpellId), restored.MemorizedSpells.OrderBy(value => value.SpellId));
                    Assert.Equal(saved.Player.FullyExploredLevelIds ?? [], restored.FullyExploredLevelIds.Order());
                    Assert.False(fresh.Simulation.Execute(restored, new DiabloOperateObjectIntent(NativeShrineTestFixture.ShrineEntityId), tick, [restored]).Succeeded);
                }
                if (final) break;
                Assert.True(tick < 300, "A shrine missile failed to finish its native lifetime.");
            }
            Assert.Empty(fixture.World.Projectiles);
            Assert.Null(host.Server.LastClientFailure);
            Assert.False(host.Server.Completion.IsCompleted);
        }
    }

    private static DiabloCohortState Checkpoint(NativeShrineTestFixture fixture,
        AuthoritativePlayerState hero, DiabloSimulationSnapshot snapshot, int depth)
    {
        // The synthetic outer Town/other-floor owners are unchanged. Every shrine-owned
        // object, projectile, player and raw item below is captured from the real module.
        var template = NativeMissileCohortFixture.Create(depth);
        var pool = fixture.World.CaptureCathedralItems()!;
        return template with {
            Tick = snapshot.Tick,
            Configuration = template.Configuration with {
                CathedralItemMetadataFingerprint = pool.MetadataFingerprint,
                CathedralItemGenerationFingerprint = pool.GenerationFingerprint,
            },
            Participants = [template.Participants[0] with {
                EntityId = hero.EntityId, Player = snapshot.Player, BaseArmorClass = hero.BaseArmorClass,
                MaximumAttribute = hero.MaximumAttribute, EffectiveArmorClass = hero.ArmorClass,
                AppliedEquipmentAttributeBonuses = hero.CaptureAppliedEquipmentAttributeBonuses(),
                PlayerClassBound = true, PlayerClassDefinition = hero.PlayerClass, ItemCatalogBound = true,
            }],
            Objects = snapshot.Objects, WorldItems = snapshot.WorldItems, Projectiles = snapshot.Projectiles,
            CathedralItems = pool with {
                Levels = template.CathedralItems!.Levels.Select(value => value.LevelId == (uint)depth
                    ? pool.Levels.Single() : value).ToArray(),
            },
        };
    }

    private static AuthoritativePlayerState Player(NativeShrineTestFixture fixture, int depth)
    {
        var weapon = fixture.Items.Definitions.First(item => item.ItemType == 1 && item.Durability > 0);
        var staff = fixture.Items.Definitions.First(item => item.ItemType == 10);
        var potion = fixture.Items.Definitions.First(item => item.MiscId == 3);
        var equipment = new EquippedStoreItem(4, 100, fixture.Items.Generate(weapon.ItemId, 100) with { StatFlag = true, Durability = 1 });
        var inventory = new[] {
            new OwnedStoreItem(0, 0, 200, 0, 0, fixture.Items.Generate(staff.ItemId, 200) with { StatFlag = true, Charges = 0, MaxCharges = 10 }),
            new OwnedStoreItem(0, 0, 201, 0, 0, fixture.Items.Generate(potion.ItemId, 201) with { StatFlag = true }),
        };
        var grid = Enumerable.Repeat(-1, 40).ToArray(); grid[0] = 0; grid[1] = 1;
        return new(0, 0, 3200, 0, 6400,
            new(new(50, 50), new(40, 40), new(45, 45), new(60, 60)), [equipment], grid, [], 50, 51,
            6400, 10, (uint)depth, 0, inventory: inventory,
            memorizedSpells: [new(1, 1), new(4, 1), new(22, 1)], playerClass: fixture.Classes.GetRequired(0), items: fixture.Items) { EntityId = 1 };
    }
}

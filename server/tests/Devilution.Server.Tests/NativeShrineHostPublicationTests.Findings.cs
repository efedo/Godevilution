using Devilution.Client.Protocol;
using Devilution.Diablo;
using Devilution.Engine;
using Devilution.Server.Protocol;
using Devilution.Server.Stores;
using Devilution.Tests;
using Xunit;

namespace Devilution.Server.Tests;

public sealed partial class NativeShrineHostPublicationTests
{
    [Fact]
    public async Task EnchantedWithOnlyOutOfVanillaRangeKnownSpellsTerminatesWithoutChangingThem()
    {
        var fixture = new NativeShrineTestFixture(5, 7);
        var hero = Player(fixture, 5);
        hero.MemorizedSpells.Clear();
        foreach (var native in new[] { 38U, 41U }) {
            Assert.True(fixture.Spells.TryGetByNativeId(native, out var spell));
            hero.MemorizedSpells.Add(new(spell.SpellId, 5));
        }
        var before = hero.MemorizedSpells.ToArray();
        // This deadline catches nontermination, rather than imposing a capture-performance budget.
        var result = await Task.Run(() => fixture.Simulation.Execute(hero,
            new DiabloOperateObjectIntent(NativeShrineTestFixture.ShrineEntityId), 0, [hero]))
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded);
        Assert.Equal(before, hero.MemorizedSpells);
        Assert.True(fixture.Shrine.Activated);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void SpiritualGoldCanBeSpentThroughTheNormalGoldPath(int depth)
    {
        var fixture = new NativeShrineTestFixture(depth, 17);
        var hero = Player(fixture, depth);
        Assert.True(fixture.Simulation.Execute(hero, new DiabloOperateObjectIntent(NativeShrineTestFixture.ShrineEntityId), 0, [hero]).Succeeded);
        var gold = hero.Inventory.Where(item => DiabloGoldInventory.HasGoldIdentity(item.State, fixture.Simulation.CombatRules, fixture.Items)).ToArray();
        Assert.NotEmpty(gold);
        Assert.All(gold, item => Assert.True(DiabloGoldInventory.IsValidStack(item, fixture.Simulation.CombatRules, fixture.Items)));
        var wallet = hero.Gold;
        Assert.True(DiabloGoldInventory.TrySpend(hero, 1, fixture.Simulation.CombatRules, items: fixture.Items));
        Assert.Equal(wallet - 1, hero.Gold);
    }

    [Theory]
    [InlineData(1, 2588U)]
    [InlineData(5, 2588U)]
    [InlineData(5, 3604671459U)]
    public void ThaumaturgicRestoredChestProducesTheSameLootAsUninterruptedPlay(int depth, uint shrineSeed)
    {
        var control = new NativeShrineTestFixture(depth, 8, shrineSeed, chestItemCount: 1);
        var hero = Player(control, depth);
        Assert.True(control.Simulation.Execute(hero, new DiabloOperateObjectIntent(NativeShrineTestFixture.ShrineEntityId), 0, [hero]).Succeeded);
        var chest = control.World.Objects.Values.Single(value => value.NativeObjectId == 5);
        var saved = control.Simulation.CaptureSnapshot(hero, 0);
        if (shrineSeed == 3604671459U) Assert.Equal(0U, chest.NativeRandomSeed);
        Assert.Equal(chest.NativeRandomSeed, saved.Objects.Single(value => value.EntityId == chest.EntityId).NativeRandomSeed);
        var codec = new DiabloCohortCodec();
        var decoded = codec.Deserialize(codec.Serialize(Checkpoint(control, hero, saved, depth)));
        var pool = decoded.CathedralItems! with { Levels = decoded.CathedralItems!.Levels.Where(value => value.LevelId == (uint)depth).ToArray() };
        var fresh = new NativeShrineTestFixture(depth, 8, shrineSeed, restoredItems: pool, chestItemCount: 1);
        var restored = Player(fresh, depth);
        fresh.Simulation.RestoreSnapshot(restored, saved with { Player = decoded.Participants.Single().Player,
            Objects = decoded.Objects, WorldItems = decoded.WorldItems, Projectiles = decoded.Projectiles });
        hero.PositionX = restored.PositionX = 55; hero.PositionY = restored.PositionY = 51;
        Assert.True(control.Simulation.Execute(hero, new DiabloOperateObjectIntent(chest.EntityId), 1, [hero]).Succeeded);
        Assert.True(fresh.Simulation.Execute(restored, new DiabloOperateObjectIntent(chest.EntityId), 1, [restored]).Succeeded);
        var expected = control.World.WorldItems.Values.OrderBy(item => item.EntityId).Select(item => (item.ItemSeed, item.State)).ToArray();
        Assert.NotEmpty(expected);
        Assert.Equal(expected, fresh.World.WorldItems.Values.OrderBy(item => item.EntityId).Select(item => (item.ItemSeed, item.State)).ToArray());
        Assert.Equal(control.Simulation.CaptureSnapshot(hero, 1).RandomState, fresh.Simulation.CaptureSnapshot(restored, 1).RandomState);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 1, true)]
    [InlineData(1, -1, false)]
    [InlineData(1, 0, true)]
    public void MemorizedCastingRequiresPositiveEffectiveLevelIncludingEquipment(int learned, int bonus, bool allowed)
    {
        var fixture = new NativeShrineTestFixture(5, 7);
        var hero = Player(fixture, 5); hero.Mana = hero.ManaMaximum;
        hero.MemorizedSpells.Clear(); hero.MemorizedSpells.Add(new(1, (uint)learned));
        hero.Equipment[0] = hero.Equipment[0] with { State = hero.Equipment[0].State with { SpellLevelAdd = bonus } };
        var wire = DiabloProtocolMapper.ToProtocolSnapshot(fixture.Simulation.CapturePresentationSnapshot(hero, 0), spells: fixture.Spells);
        Assert.Equal(allowed, AuthoritativeSpellSourceSelector.GetSources(wire).Any(source => source.SpellId == 1));
        var result = fixture.Simulation.Execute(hero, new DiabloCastIntent(1, 0, 54, 51, DiabloCastSourceKind.MemorizedSpell), 0, [hero]);
        Assert.Equal(allowed, result.Succeeded);
        if (!allowed) Assert.Equal(DiabloRuleFailure.NotAllowed, result.Failure);
    }

    [Fact]
    public void TaintedZeroBaseManaRestoresIntoAFreshNonzeroManaPlayer()
    {
        var fixture = new NativeShrineTestFixture(5, 25, 743271966, mode: DiabloGameMode.Multiplayer);
        var actor = Player(fixture, 5); var peer = Player(fixture, 5); peer.EntityId = 2; peer.PositionX = 52;
        peer.Attributes = peer.Attributes with { Magic = new(1, 1) };
        peer.BaseManaMaximum = 64; peer.RestoreNativeMana(0, 64); peer.RestoreNativeResourceBases(3200, 0);
        Assert.True(fixture.Simulation.Execute(actor, new DiabloOperateObjectIntent(NativeShrineTestFixture.ShrineEntityId), 0, [actor, peer]).Succeeded);
        var saved = fixture.Simulation.CaptureSnapshot(peer, 0);
        Assert.Equal((0, 0, -64), (saved.Player.BaseManaMaximum, saved.Player.ManaMaximum, saved.Player.Mana));
        var fresh = new NativeShrineTestFixture(5, 25, 743271966, mode: DiabloGameMode.Multiplayer);
        var restored = Player(fresh, 5); restored.EntityId = 2;
        fresh.Simulation.RestoreSnapshot(restored, saved);
        Assert.Equal((0, 0, -64), (restored.BaseManaMaximum, restored.ManaMaximum, restored.Mana));
        Admit(DiabloProtocolMapper.ToProtocolSnapshot(fresh.Simulation.CapturePresentationSnapshot(restored, 0), spells: fresh.Spells));
    }
    [Fact]
    public void SignedShrineResourcesSurviveTheProtocolSaveRoundTrip()
    {
        var fixture = new NativeShrineTestFixture(5, 25);
        var hero = Player(fixture, 5);
        hero.BaseManaMaximum = 0; hero.RestoreNativeMana(-64, 0); hero.RestoreNativeResourceBases(3200, -64);
        var wire = DiabloProtocolMapper.ToProtocolSnapshot(fixture.Simulation.CapturePresentationSnapshot(hero, 0), spells: fixture.Spells);
        var saved = DiabloProtocolMapper.FromProtocolSnapshot(wire);
        Assert.Equal((0, 0, -64), (saved.Player.BaseManaMaximum, saved.Player.ManaMaximum, saved.Player.Mana));
        Admit(DiabloProtocolMapper.ToProtocolSnapshot(saved, spells: fixture.Spells));
    }

    [Fact]
    public void NativeZeroBaseLifeRemainsZeroWhenRestored()
    {
        var fixture = new NativeShrineTestFixture(5, 25);
        var hero = Player(fixture, 5);
        hero.RestoreNativeResourceBases(0, 0);
        hero.BaseLifeMaximum = 0; hero.RecalculateEquipmentResources(); hero.Life = 0;
        var saved = fixture.Simulation.CaptureSnapshot(hero, 0);
        Assert.Equal((0, 64, 0), (saved.Player.BaseLifeMaximum, saved.Player.LifeMaximum, saved.Player.Life));
        var fresh = new NativeShrineTestFixture(5, 25);
        var restored = Player(fresh, 5);
        fresh.Simulation.RestoreSnapshot(restored, saved);
        Assert.Equal(0, restored.BaseLifeMaximum);
        Assert.Equal(0, restored.Life);
    }

    [Fact]
    public void SpiritualNativeZeroSeedUsesAValidGoldIdentityWithoutAnExtraDraw()
    {
        var fixture = new NativeShrineTestFixture(5, 17);
        var hero = Player(fixture, 5);
        // Native PlaySfxLoc consumes the first global draw; MakeGoldStack's next seed is zero.
        fixture.Simulation.RestoreSnapshot(hero, fixture.Simulation.CaptureSnapshot(hero, 0) with { RandomState = 3135529114 });
        Assert.True(fixture.Simulation.Execute(hero, new DiabloOperateObjectIntent(NativeShrineTestFixture.ShrineEntityId), 0, [hero]).Succeeded);
        Assert.Equal(uint.MaxValue, hero.Inventory[2].ItemSeed);
        Assert.True(DiabloGoldInventory.IsValidStack(hero.Inventory[2], fixture.Simulation.CombatRules, fixture.Items));
        var expected = new DeterministicLegacyRandom(3135529114);
        for (var index = 0; index < 39; index++) expected.AdvanceSeed();
        Assert.Equal(expected.State, fixture.Simulation.CaptureSnapshot(hero, 0).RandomState);
        var saved = fixture.Simulation.CaptureSnapshot(hero, 0);
        Admit(DiabloProtocolMapper.ToProtocolSnapshot(fixture.Simulation.CapturePresentationSnapshot(hero, 0), spells: fixture.Spells));
        var codec = new DiabloCohortCodec();
        var decoded = codec.Deserialize(codec.Serialize(Checkpoint(fixture, hero, saved, 5)));
        var fresh = new NativeShrineTestFixture(5, 17);
        var restored = Player(fresh, 5);
        fresh.Simulation.RestoreSnapshot(restored, saved with { Player = Assert.Single(decoded.Participants).Player });
        Assert.True(DiabloGoldInventory.TrySpend(restored, 1, fresh.Simulation.CombatRules, items: fresh.Items));
    }

    [Fact]
    public async Task HiddenRejectsAnEquipmentSlotOutsideTheNativeBodyBeforeSampling()
    {
        var fixture = new NativeShrineTestFixture(5, 1);
        var hero = Player(fixture, 5);
        hero.Equipment[0] = hero.Equipment[0] with { Slot = 7 };
        // Native InvBody has exactly seven slots; corrupt state must not enter rejection sampling.
        var operation = Task.Run(() => fixture.Simulation.Execute(hero,
            new DiabloOperateObjectIntent(NativeShrineTestFixture.ShrineEntityId), 0, [hero]));
        await Assert.ThrowsAsync<InvalidDataException>(async () => await operation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

}

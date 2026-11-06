using Devilution.Engine;
using Devilution.Tests;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class NativeThemeEffectTests
{
    internal static AuthoritativePlayerState Player(NativeShrineTestFixture fixture, int depth, uint id = 1)
        => new(0, 0, 3200, 0, 6400,
            new(new(50, 50), new(40, 40), new(45, 45), new(60, 60)), [],
            Enumerable.Repeat(-1, 40).ToArray(), [], 50, 51, 6400, 10, (uint)depth, 0,
            memorizedSpells: [new(1, 1)], playerClass: fixture.Classes.GetRequired(0), items: fixture.Items) { EntityId = id };

    [Theory]
    [InlineData(1, 66)] [InlineData(5, 66)] [InlineData(1, 76)] [InlineData(5, 76)]
    public void ResourceFountainsRemainUsableIncludingAtFullAndDoNotDrawRandom(int depth, int type)
    {
        var fixture = new NativeShrineTestFixture(depth, 0, nativeObjectId: type);
        var hero = Player(fixture, depth);
        var peer = Player(fixture, depth, 2);
        var initial = fixture.Random.State;
        var before = type == 66 ? hero.Life : hero.Mana;
        for (var i = 1; i <= 2; i++) {
            Assert.True(fixture.Rules.Execute(hero, new DiabloOperateObjectIntent(fixture.Shrine.EntityId), 0, [hero, peer]).Succeeded);
            Assert.Equal(before + 64 * i, type == 66 ? hero.Life : hero.Mana);
            Assert.False(fixture.Shrine.Activated);
        }
        hero.Life = hero.LifeMaximum - 1; hero.Mana = hero.ManaMaximum - 1;
        Assert.True(fixture.Rules.Execute(hero, new DiabloOperateObjectIntent(fixture.Shrine.EntityId), 0, [hero, peer]).Succeeded);
        Assert.Equal(type == 66 ? hero.LifeMaximum : hero.ManaMaximum, type == 66 ? hero.Life : hero.Mana);
        Assert.Equal(type == 66 ? hero.BaseLifeMaximum : hero.BaseManaMaximum, type == 66 ? hero.NativeLifeBase : hero.NativeManaBase);
        Assert.True(fixture.Rules.Execute(hero, new DiabloOperateObjectIntent(fixture.Shrine.EntityId), 0, [hero, peer]).Succeeded);
        Assert.Equal(initial, fixture.Random.State);
        Assert.Equal(3200, peer.Life); Assert.Equal(0, peer.Mana);
        Assert.Contains(fixture.Shrine.EntityId, fixture.Simulation.CaptureSnapshot(hero, 0).NativeThemeEffectInteractableEntityIds);
    }

    [Theory]
    [InlineData(1, 2004U)] [InlineData(5, 2535U)]
    public void MurkyPoolUsesNativeFamilyDurationAndRetainsLongerInfravision(int depth, uint duration)
    {
        var fixture = new NativeShrineTestFixture(depth, 0, nativeObjectId: 81);
        var hero = Player(fixture, depth);
        var peer = Player(fixture, depth, 2);
        Assert.True(fixture.Rules.Execute(hero, new DiabloOperateObjectIntent(fixture.Shrine.EntityId), 0, [hero, peer]).Succeeded);
        var status = Assert.Single(hero.StatusEffects);
        Assert.Equal(AuthoritativeStatusEffectBehavior.Infravision, status.Behavior);
        Assert.Equal(duration, status.RemainingTicks);
        Assert.Empty(peer.StatusEffects);
        Assert.False(fixture.Rules.Execute(hero, new DiabloOperateObjectIntent(fixture.Shrine.EntityId), 0, [hero, peer]).Succeeded);
        var second = new NativeShrineTestFixture(depth, 0, nativeObjectId: 81);
        hero.StatusEffects[0] = status with { RemainingTicks = duration + 100 };
        Assert.True(second.Rules.Execute(hero, new DiabloOperateObjectIntent(second.Shrine.EntityId), 0, [hero]).Succeeded);
        Assert.Equal(duration + 100, Assert.Single(hero.StatusEffects).RemainingTicks);
    }

    [Theory]
    [InlineData(1)] [InlineData(5)]
    [Trait("Category", "Slow")]
    public void TearsTransfersExactlyOneAttributeAndClampsEachEndIndependently(int depth)
    {
        // All twelve native pairs, including zero source and capped destination, with no RNG consumption.
        for (uint pair = 0; pair < 12; pair++) {
            var fixture = new NativeShrineTestFixture(depth, 0, pair << 16, nativeObjectId: 82);
            var hero = Player(fixture, depth);
            hero.Attributes = new(new(0, 0), new(50, 50), new(60, 60), new(100, 100));
            var original = new[] { 0, 50, 60, 100 };
            var from = (int)pair / 3; var to = (int)pair % 3; if (to >= from) to++;
            original[from] = Math.Max(0, original[from] - 1);
            var maxima = new[] { 250, 50, 60, 100 };
            original[to] = Math.Min(maxima[to], original[to] + 1);
            var state = fixture.Random.State;
            Assert.True(fixture.Rules.Execute(hero, new DiabloOperateObjectIntent(fixture.Shrine.EntityId), 0, [hero]).Succeeded);
            Assert.Equal(original, new[] { hero.Attributes.Strength.Base, hero.Attributes.Magic.Base, hero.Attributes.Dexterity.Base, hero.Attributes.Vitality.Base });
            Assert.Equal(state, fixture.Random.State);
            Assert.True(fixture.Shrine.Activated);
            Assert.False(fixture.Rules.Execute(hero, new DiabloOperateObjectIntent(fixture.Shrine.EntityId), 0, [hero]).Succeeded);
        }
    }

    [Theory]
    [InlineData(1, 26, 6)] [InlineData(5, 91, 8)]
    public void BurningCrossAppliesEachTickOnlyOnItsNorthTileThroughManaShieldAndDeath(int depth, int type, int damage)
    {
        var fixture = new NativeShrineTestFixture(depth, 0, nativeObjectId: type);
        var hero = Player(fixture, depth); hero.PositionY = 49;
        hero.Mana = 640; hero.ManaShieldActive = true;
        var events = fixture.Simulation.AdvanceTick(1, [hero]);
        Assert.Equal(3200, hero.Life); Assert.Equal(640 - damage, hero.Mana);
        Assert.Equal(damage, Assert.Single(events).Event.AmountQ6);
        Assert.Null(hero.CurrentAction);
        hero.PositionX = 51;
        Assert.Empty(fixture.Simulation.AdvanceTick(2, [hero]));
        Assert.Equal(640 - damage, hero.Mana);
        hero.PositionX = 50; hero.ManaShieldActive = false; hero.Life = 64;
        fixture.Simulation.AdvanceTick(3, [hero]);
        Assert.Equal(0, hero.Life); Assert.Equal(DiabloPlayerCombatMode.Death, hero.CombatMode);
        Assert.DoesNotContain(fixture.Simulation.AdvanceTick(4, [hero]), value => value.Event.Kind == DiabloGameplayEventKind.Damage);
    }

    [Theory]
    [InlineData(66)] [InlineData(76)]
    public void ResourceFountainPreservesIndependentBaseOnExactMaximumAndResetsOnlyOverflow(int type)
    {
        var fixture = new NativeShrineTestFixture(5, 0, nativeObjectId: type);
        var hero = Player(fixture, 5);
        hero.Life = hero.LifeMaximum - 64; hero.Mana = hero.ManaMaximum - 64;
        hero.RestoreNativeResourceBases(6000, 6000);
        Assert.True(fixture.Rules.Execute(hero, new DiabloOperateObjectIntent(fixture.Shrine.EntityId), 0, [hero]).Succeeded);
        Assert.Equal(6064, type == 66 ? hero.NativeLifeBase : hero.NativeManaBase);
        if (type == 66) hero.Life -= 1; else hero.Mana -= 1;
        Assert.True(fixture.Rules.Execute(hero, new DiabloOperateObjectIntent(fixture.Shrine.EntityId), 0, [hero]).Succeeded);
        Assert.Equal(6400, type == 66 ? hero.NativeLifeBase : hero.NativeManaBase);
    }

    [Theory]
    [InlineData(66)] [InlineData(76)] [InlineData(79)] [InlineData(80)] [InlineData(81)] [InlineData(82)]
    public void TheRealOwnerRejectsInvalidConsumedFramesAndRepeatableConsumption(int type)
    {
        var fixture = new NativeShrineTestFixture(5, 0, nativeObjectId: type);
        var hero = Player(fixture, 5);
        fixture.Shrine.Activated = true;
        fixture.Shrine.AnimationFrame = type == 80 ? 4 : 2;
        Assert.Throws<InvalidDataException>(() => fixture.Simulation.CaptureSnapshot(hero, 0));
    }

    [Theory]
    [InlineData(1, 26, 6)] [InlineData(5, 91, 8)]
    public void EngineTicksApplyBurningCrossDamageAndNativeDeathGate(int depth, int type, int damage)
    {
        var fixture = new NativeShrineTestFixture(depth, 0, nativeObjectId: type);
        var hero = Player(fixture, depth); hero.PositionY = 49;
        var runtime = fixture.Simulation.CreateRuntime(DiabloPlayerTemplate.CreateDefault(0, 50, 49, (uint)depth));
        runtime.AddParticipant("hero", hero);
        runtime.AdvanceTo(3);
        Assert.Equal(3200 - 3 * damage, hero.Life);
        Assert.Equal(hero.Life, hero.NativeLifeBase);
        Assert.Equal(3UL, hero.LastAppliedTick);
        Assert.Null(hero.CurrentAction);
        // Native guards the death mode, rather than refusing a zero-life player awaiting its death transition.
        hero.Life = 0;
        runtime.AdvanceTo(4);
        Assert.Equal(DiabloPlayerCombatMode.Death, hero.CombatMode);
    }

    [Theory]
    [InlineData(1, 26, 6, 68, false)] [InlineData(1, 26, 6, 68, true)]
    [InlineData(5, 91, 8, 70, false)] [InlineData(5, 91, 8, 70, true)]
    public void BurningCrossClearsFractionalManaShieldAtTickEnd(int depth, int type, int damage, int mana, bool withCommands)
    {
        var fixture = new NativeShrineTestFixture(depth, 0, nativeObjectId: type);
        var hero = Player(fixture, depth); hero.PositionY = 49;
        hero.Mana = mana; hero.ManaShieldActive = true;
        Advance(1);
        Assert.Equal(62, hero.Mana);
        Assert.Equal(62, hero.NativeManaBase);
        Assert.Equal(3200, hero.Life);
        var shieldAfterFirstHit = hero.ManaShieldActive;
        Advance(2);
        Assert.Equal(3200 - damage, hero.Life);
        Assert.Equal(62, hero.Mana);
        Assert.False(shieldAfterFirstHit);
        Assert.Equal(hero.Life, hero.NativeLifeBase);

        void Advance(ulong tick)
        {
            if (withCommands) fixture.Simulation.AdvanceTickWithCommands(tick, [hero], _ => []);
            else fixture.Simulation.AdvanceTick(tick, [hero]);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BurningCrossKeepsShieldThroughTheLastWholeMana(bool withCommands)
    {
        var fixture = new NativeShrineTestFixture(5, 0, nativeObjectId: 91);
        var hero = Player(fixture, 5); hero.PositionY = 49;
        hero.Mana = 72; hero.ManaShieldActive = true;
        Advance(1);
        Assert.Equal(64, hero.Mana);
        Assert.True(hero.ManaShieldActive);
        Advance(2);
        Assert.Equal(56, hero.Mana);
        Assert.Equal(3200, hero.Life);
        Assert.False(hero.ManaShieldActive);
        Advance(3);
        Assert.Equal(56, hero.Mana);
        Assert.Equal(3192, hero.Life);

        void Advance(ulong tick)
        {
            if (withCommands) fixture.Simulation.AdvanceTickWithCommands(tick, [hero], _ => []);
            else fixture.Simulation.AdvanceTick(tick, [hero]);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ManaShieldClearsOnlyAfterAllDamageInTheTick(bool withCommands)
    {
        var fixture = new NativeShrineTestFixture(5, 0, nativeObjectId: 91);
        var hero = Player(fixture, 5); hero.PositionY = 49;
        hero.Mana = 66; hero.ManaShieldActive = true;
        hero.Equipment.Add(new(0, 1, AuthoritativeItemState.Empty with {
            Flags = AuthoritativeEquipmentIncomingDamageRules.DrainLifeFlag, StatFlag = true,
        }));
        // Native player processing spends four Q6 first. The cross still uses the remaining
        // fractional mana during ProcessObjects, before ProcessMissiles removes the shield.
        Advance(1);
        Assert.Equal(54, hero.Mana);
        Assert.Equal(3200, hero.Life);
        Assert.False(hero.ManaShieldActive);
        Advance(2);
        Assert.Equal(54, hero.Mana);
        Assert.Equal(3188, hero.Life);

        void Advance(ulong tick)
        {
            if (withCommands) fixture.Simulation.AdvanceTickWithCommands(tick, [hero], _ => []);
            else fixture.Simulation.AdvanceTick(tick, [hero]);
        }
    }
}

using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Google.Protobuf;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeThemeObjectJourneyEvidenceTests
{
    [Theory]
    [InlineData(61U, 4U, 6U)]
    [InlineData(62U, 4U, 2U)]
    [InlineData(63U, 4U, 2U)]
    [InlineData(64U, 4U, 6U)]
    [InlineData(67U, 4U, 4U)]
    [InlineData(77U, 4U, 5U)]
    [InlineData(89U, 4U, 5U)]
    [InlineData(90U, 4U, 5U)]
    [InlineData(92U, 4U, 5U)]
    public void ItemObjectsRequireTheirNativeConsumedFrameAndOneUseStateAfterWireResume(uint type, uint frame, uint usedFrame)
    {
        var target = Target(type); target.AnimationFrame = frame;
        Assert.True(NativeThemeObjectJourneyEvidence.CanDemonstrate(target, Player()));
        Assert.True(NativeThemeObjectJourneyEvidence.IsLoot(type));
        Assert.Equal(usedFrame, NativeThemeObjectJourneyEvidence.UsedFrame(target));
        Assert.Throws<InvalidOperationException>(() => NativeThemeObjectJourneyEvidence.VerifyRetained(target, 5, type, usedFrame));
        target.Activated = true; target.NativeThemeObjectInteractable = false; target.AnimationFrame = usedFrame;
        var resumed = ObjectSnapshot.Parser.ParseFrom(target.ToByteArray());
        NativeThemeObjectJourneyEvidence.VerifyRetained(resumed, 5, type, usedFrame);
        foreach (var corrupt in new Action<ObjectSnapshot>[] {
            value => value.Activated = false, value => value.NativeThemeObjectInteractable = true,
            value => value.Destroyed = true, value => value.LevelId++, value => value.AnimationFrame++, value => value.NativeObjectId = 5,
        }) {
            var broken = resumed.Clone(); corrupt(broken);
            Assert.Throws<InvalidOperationException>(() => NativeThemeObjectJourneyEvidence.VerifyRetained(broken, 5, type, usedFrame));
        }
    }

    [Theory]
    [InlineData(1U)] [InlineData(4U)] [InlineData(5U)] [InlineData(8U)]
    public void LootEvidenceRequiresTheNativeDoubleDepthItemLevelAndStableIdentity(uint depth)
    {
        var item = new WorldItemSnapshot { EntityId = 15, LevelId = depth, ItemSeed = 17,
            State = new ItemStateSnapshot { StableItemId = 21, CreateInfo = 2 * depth } };
        NativeThemeObjectJourneyEvidence.VerifyDrop(WorldItemSnapshot.Parser.ParseFrom(item.ToByteArray()), depth);
        item.State.CreateInfo = depth;
        Assert.Throws<InvalidOperationException>(() => NativeThemeObjectJourneyEvidence.VerifyDrop(item, depth));
        item.State.CreateInfo = 2 * depth; item.State.StableItemId = 0;
        Assert.Throws<InvalidOperationException>(() => NativeThemeObjectJourneyEvidence.VerifyDrop(item, depth));
    }

    [Theory]
    [InlineData(66U)] [InlineData(76U)]
    public void ReusableFountainsRequireOneNativeHitPointPerUseIncludingClamping(uint type)
    {
        var before = Player(); var after = before.Clone();
        Assert.True(NativeThemeObjectJourneyEvidence.CanDemonstrate(Target(type), before));
        Assert.False(NativeThemeObjectJourneyEvidence.EffectApplied(type, before, after));
        if (type == 66) after.Life += 64; else after.Mana += 64;
        Assert.True(NativeThemeObjectJourneyEvidence.EffectApplied(type, before, after));
        before.Life = 990; before.Mana = 990; after.Life = 1000; after.Mana = 1000;
        Assert.True(NativeThemeObjectJourneyEvidence.EffectApplied(type, before, after));
        Assert.False(NativeThemeObjectJourneyEvidence.CanDemonstrate(Target(type), after));
        // A second accepted use at full resources is legal and still proves reusable admission.
        Assert.True(NativeThemeObjectJourneyEvidence.EffectApplied(type, after, after.Clone()));
        var fountain = Target(type);
        NativeThemeObjectJourneyEvidence.VerifyRetained(fountain, 5, type, 4);
        fountain.Activated = true;
        Assert.Throws<InvalidOperationException>(() => NativeThemeObjectJourneyEvidence.VerifyRetained(fountain, 5, type, 4));
    }

    [Fact]
    public void PurifyingEvidenceUsesTheNativeSignedManaProjection()
    {
        var before = Player(); before.NativeMana = -100; before.NativeManaMaximum = 1000; before.Mana = 0;
        var after = before.Clone(); after.NativeMana += 64;
        Assert.True(NativeThemeObjectJourneyEvidence.EffectApplied(76, before, after));
        Assert.False(NativeThemeObjectJourneyEvidence.EffectApplied(76, before, before.Clone()));
    }

    [Fact]
    public void OneUseFountainsProveTheNativeAttributeOrStatusEffectAndCannotBeSelectedAgain()
    {
        var before = Player(); var after = before.Clone();
        Assert.True(NativeThemeObjectJourneyEvidence.CanDemonstrate(Target(82), before));
        Assert.False(NativeThemeObjectJourneyEvidence.EffectApplied(82, before, after));
        after.Attributes.Strength.Base--; after.Attributes.Magic.Base++;
        Assert.True(NativeThemeObjectJourneyEvidence.EffectApplied(82, before, after));
        after.Attributes.Dexterity.Base++;
        Assert.False(NativeThemeObjectJourneyEvidence.EffectApplied(82, before, after));
        after = before.Clone(); after.StatusEffects.Add(new StatusEffectSnapshot { Behavior = StatusEffectBehavior.Infravision, RemainingTicks = 100 });
        Assert.True(NativeThemeObjectJourneyEvidence.EffectApplied(81, before, after));
        Assert.False(NativeThemeObjectJourneyEvidence.CanDemonstrate(Target(81), after));
        var used = Target(81); used.Activated = true; used.NativeThemeObjectInteractable = false;
        Assert.False(NativeThemeObjectJourneyEvidence.CanDemonstrate(used, before));
        NativeThemeObjectJourneyEvidence.VerifyRetained(used, 5, 81, 4);
        Assert.False(NativeThemeObjectJourneyEvidence.CanDemonstrate(Target(79), before));
        Assert.False(NativeThemeObjectJourneyEvidence.CanDemonstrate(Target(80), before));
    }

    [Fact]
    public void MeasurementAndJourneyKeepAnObjectBehindAClosedRouteDoorAndMonsterButNeverIgnoreAWall()
    {
        var snapshot = new Snapshot {
            Players = { new PlayerSnapshot { EntityId = 1, LevelId = 5, PositionX = 1, PositionY = 2 } },
            Objects = { new ObjectSnapshot { EntityId = 10, LevelId = 5, NativeObjectId = 42, PositionX = 3, PositionY = 2 } },
            Monsters = { new MonsterSnapshot { EntityId = 20, LevelId = 5, Alive = true, PositionX = 6, PositionY = 2 } },
        };
        var geometry = new LevelGeometrySnapshot { LevelId = 5, Width = 9, Height = 5 };
        for (var y = 0; y < geometry.Height; y++)
            for (var x = 0; x < geometry.Width; x++)
                if (y != 2) geometry.BlockedCells.Add(y * geometry.Width + x);
        var target = Target(62); target.PositionX = 7; target.PositionY = 2;
        Assert.Null(NativeObjectApproachPath.Find(snapshot, geometry, target));
        var route = NativeThemeObjectJourneyEvidence.FindApproach(snapshot, geometry, target);
        Assert.NotNull(route); Assert.Equal((6, 2), route![^1]);
        Assert.Equal(10U, NativeObjectApproachPath.FirstClosedDoorOnRoute(snapshot, 5, route)!.EntityId);
        snapshot.Objects[0].MissilePassable = true; snapshot.Monsters[0].Alive = false;
        Assert.Equal(route, NativeObjectApproachPath.Find(snapshot, geometry, target));
        geometry.BlockedCells.Add(2 * geometry.Width + 4);
        Assert.Null(NativeThemeObjectJourneyEvidence.FindApproach(snapshot, geometry, target));
    }

    private static ObjectSnapshot Target(uint type) => new() { EntityId = 10, LevelId = 5, NativeObjectId = type,
        AnimationFrame = 4, NativeThemeObjectInteractable = true };
    private static PlayerSnapshot Player() => new() { Life = 100, LifeMaximum = 1000, Mana = 100, ManaMaximum = 1000,
        Attributes = new() { Strength = new() { Base = 80 }, Magic = new() { Base = 10 },
            Dexterity = new() { Base = 30 }, Vitality = new() { Base = 65 } } };
}

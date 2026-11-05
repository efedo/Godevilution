using System.Text.Json;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class NativeObjectCapabilityTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(42)] [InlineData(43)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void NativeDoorAndOrdinaryChestBindingsAdmitEveryL1L8WallMissile(int triggerType)
    {
        foreach (var type in new[] { 0, 1, 7 }) {
            var state = State(triggerType, type);
            var trigger = state.ObjectSlots[0];
            Assert.True(AuthoritativeNativePopulationBindings.IsSupportedNativeTrapTriggerType(trigger));
            Assert.True(AuthoritativeNativePopulationBindings.HasSupportedNativeTrapLink(state, trigger));
            if (triggerType is 5 or 6 or 7) {
                Assert.True(AuthoritativeNativePopulationBindings.IsSupportedNativeChest(state, trigger));
                Assert.True(AuthoritativeNativePopulationBindings.IsSupportedNativeCatacombsChest(state, trigger));
            }
        }
        Assert.False(AuthoritativeNativePopulationBindings.HasSupportedNativeTrapLink(State(triggerType, 99),
            State(triggerType, 99).ObjectSlots[0]));
    }

    [Fact]
    public void ConvertedIntrinsicChestRetainsItsWallRowWithoutDispatchingIt()
    {
        var state = State(68, 7);
        var chest = state.ObjectSlots[0];
        Assert.True(AuthoritativeNativePopulationBindings.IsSupportedNativeCatacombsChest(state, chest));
        Assert.True(AuthoritativeNativePopulationBindings.HasSupportedNativeTrapLink(state, chest));
        Assert.False(DiabloWorldState.UpdateTrapState(0, 68, 0, false, false));
        var facts = new AuthoritativeCathedralNativeTrapFacts(1, 701, 53, 5, 1,
            48, 50, 0, 50, 50, 7, 0, 0, 700, 68, 50, 50, true);
        var opened = new DiabloNativeTrapState(facts, 0, false, DiabloNativeTrapDisposition.Armed);
        opened.ValidateShape();
        (opened with { TrapVar4 = 1, Disposition = DiabloNativeTrapDisposition.Disarmed }).ValidateShape();
        Assert.Throws<InvalidDataException>(() =>
            (opened with { TrapVar4 = 1, Disposition = DiabloNativeTrapDisposition.Fired }).ValidateShape());
    }

    [Fact]
    public void OldArrowPayloadKeepsItsExactJsonAndNewKindsRoundTripAdditively()
    {
        var old = new AuthoritativeNativeTrapArrowState(2, 3, 0);
        Assert.Equal("{\"NativeObjectSlot\":2,\"MissileDistance\":3,\"LastCollisionTargetHash\":0}", JsonSerializer.Serialize(old));
        foreach (var type in new[] { 1, 7, 27 }) {
            var state = old with { MissileTypeId = type, IntrinsicChest = type == 27 };
            Assert.Equal(state, JsonSerializer.Deserialize<AuthoritativeNativeTrapArrowState>(JsonSerializer.Serialize(state)));
        }
    }

    private static AuthoritativeCathedralThemeRoomState State(int triggerType, int missileType)
    {
        var objects = Enumerable.Range(0, 127).Select(slot => new AuthoritativeCathedralPopulationObjectState {
            Slot = slot, Type = -1, BookMessage = -1,
        }).ToArray();
        objects[0] = objects[0] with { Type = triggerType, Position = new(50, 50),
            TrapFlag = true, DoorFlag = triggerType is 1 or 2 or 42 or 43, SelectionRegion = 3,
            AnimFrame = 1, Var1 = 1, Var2 = 1, Var4 = 1 };
        objects[1] = objects[1] with { Type = 53, Position = new(48, 50), Var1 = 50, Var2 = 50, Var3 = missileType };
        var refs = new int[112 * 112];
        refs[50 * 112 + 50] = 1;
        refs[50 * 112 + 48] = 2;
        var monsters = Enumerable.Range(0, 200).Select(slot => new AuthoritativeCathedralPopulationMonsterState {
            Slot = slot, UniqueType = 255,
        }).ToArray();
        var items = Enumerable.Repeat(new AuthoritativeCathedralInitialItemState(), 128).ToArray();
        var lights = Enumerable.Range(0, 32).Select(slot => new AuthoritativeCathedralEncounterLightState(
            slot, new(0, 0), new(0, 0), 0, 0, 0, 0, false, false)).ToArray();
        return new(objects, Enumerable.Range(0, 127).ToArray(), Enumerable.Range(2, 125).Concat(new[] { 0, 1 }).ToArray(), 2,
            monsters, Enumerable.Range(0, 200).ToArray(), 0, 0, new int[112 * 112], new int[112 * 112], items,
            Enumerable.Range(0, 127).ToArray(), 0, new int[112 * 112], new HashSet<int>(),
            new ushort[112 * 112], new sbyte[112 * 112], new byte[112 * 112], new byte[1379],
            new byte[112 * 112], refs, new byte[112 * 112], new byte[112 * 112],
            Enumerable.Range(0, 32).ToArray(), 0, lights, false, false);
    }
}

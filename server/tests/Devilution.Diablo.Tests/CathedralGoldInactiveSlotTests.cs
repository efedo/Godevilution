using System.Text.Json;
using Xunit;

namespace Devilution.Diablo.Tests;

/** Native GoldAutoPlace changes the inactive slot before CleanupItems. */
public sealed class CathedralGoldInactiveSlotTests
{
    [Theory]
    [InlineData(0, 4)]
    [InlineData(1400, 5)]
    [InlineData(2700, 6)]
    public void PickupRetainsGoldRemainderAndCursorThroughRawPoolRestore(int remainder, int cursor)
    {
        var initial = AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint();
        var level = initial.Levels.Single();
        var slots = level.Items.ToArray();
        slots[0] = slots[0] with {
            CreateInfo = 0x8000,
            Value = 3000,
            IdentifiedValue = 3000,
            CursorGraphic = 6,
        };
        initial = initial with { Levels = [level with { Items = slots }] };
        var runtime = new AuthoritativeCathedralItemRuntimeTestData.ProfileRuntime(initial);
        var projection = runtime.GetProjection(1, 101)!;
        var worldItem = new AuthoritativeWorldItem(101, 1, 1, 1, projection.ItemSeed, projection.Price, projection.State);
        var owned = projection.State with {
            Value = remainder,
            NativeCursorGraphic = cursor,
            StatFlag = true,
        };

        Assert.True(runtime.RemoveWorldItem(worldItem, owned));
        var saved = runtime.CaptureCheckpoint();
        var inactive = saved.Levels.Single().Items[0];
        Assert.Equal(remainder, inactive.Value);
        Assert.Equal(cursor, inactive.CursorGraphic);
        Assert.Equal(3000, inactive.IdentifiedValue);
        Assert.Equal(0, inactive.CreateInfo & 0x8000);
        Assert.True(inactive.StatFlag);
        Assert.Equal(1, saved.Levels.Single().ActiveItemCount);
        Assert.Equal(0, saved.Levels.Single().SignedItemRefs[113]);
        var restored = new AuthoritativeCathedralItemRuntimeTestData.ProfileRuntime(saved);
        Assert.Equal(inactive, restored.CaptureCheckpoint().Levels.Single().Items[0]);
    }

    [Theory]
    [InlineData(-1, 4)]
    [InlineData(101, 4)]
    [InlineData(0, null)]
    [InlineData(0, 256)]
    public void InvalidGoldPickupStateRefusesBeforeChangingThePool(int remainder, int? cursor)
    {
        var runtime = new AuthoritativeCathedralItemRuntimeTestData.ProfileRuntime(
            AuthoritativeCathedralItemRuntimeTestData.TwoBoundItemCheckpoint());
        var projection = runtime.GetProjection(1, 101)!;
        var item = new AuthoritativeWorldItem(101, 1, 1, 1, projection.ItemSeed, projection.Price, projection.State);
        var before = JsonSerializer.Serialize(runtime.CaptureCheckpoint());
        var malformed = projection.State with { Value = remainder, NativeCursorGraphic = cursor };

        Assert.Throws<InvalidDataException>(() => runtime.RemoveWorldItem(item, malformed));
        Assert.Equal(before, JsonSerializer.Serialize(runtime.CaptureCheckpoint()));
    }
}

using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativePickedUpItemIdentityTests
{
    [Fact]
    public void AReusedEntityForADifferentNativeItemIsNotThePickedUpDrop()
    {
        var snapshot = new Snapshot { WorldItems = { Item(1073741825, 251598481, 150) } };
        Assert.False(NativePickedUpItemIdentity.IsOnFloor(snapshot, 231948336, 25));
    }

    [Theory]
    [InlineData(1073741825U, 5U)]
    [InlineData(1073741826U, 5U)]
    [InlineData(1073741826U, 8U)]
    public void ARealReappearanceIsDetectedEvenWithAnotherEntityOrFloor(uint entity, uint level)
    {
        var item = Item(entity, 231948336, 25); item.LevelId = level;
        Assert.True(NativePickedUpItemIdentity.IsOnFloor(new Snapshot { WorldItems = { item } }, 231948336, 25));
    }

    [Theory]
    [InlineData(231948337U, 25U)]
    [InlineData(231948336U, 150U)]
    public void BothSeedAndStableIdentityMustMatch(uint seed, uint stable)
        => Assert.False(NativePickedUpItemIdentity.IsOnFloor(new Snapshot { WorldItems = { Item(1, seed, stable) } }, 231948336, 25));

    private static WorldItemSnapshot Item(uint entity, uint seed, uint stable)
        => new() { EntityId = entity, LevelId = 5, ItemSeed = seed, State = new ItemStateSnapshot { StableItemId = stable } };
}

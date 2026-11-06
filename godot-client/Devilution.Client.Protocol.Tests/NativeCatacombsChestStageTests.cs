using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeCatacombsChestStageTests
{
    [Fact]
    public void UnseededJourneyRequiresAChestAndPickup()
    {
        Assert.True(NativeCatacombsChestStage.RequiresEvidence(null));
        Assert.Throws<InvalidOperationException>(() => NativeCatacombsChestStage.VerifyAvailability(8, null, false));
        NativeCatacombsChestStage.VerifyAvailability(8, null, true);
        NativeCatacombsChestStage.VerifyAvailability(4, null, false);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("98")]
    public void SeededFeatureJourneyCanCompleteWithoutAChest(string seed)
    {
        Assert.False(NativeCatacombsChestStage.RequiresEvidence(seed));
        NativeCatacombsChestStage.VerifyAvailability(8, seed, false);
        NativeCatacombsChestStage.VerifyAvailability(8, seed, true);
    }

    [Fact]
    public void ASeededDoorOnlyCheckpointRetainsRequiredDoorEvidenceWithoutInventingAChestPickup()
    {
        NativeCatacombsChestStage.VerifyAvailability(8, "0", hasChest: false);
        NativeDepthResumeEvidence.Verify(false, false, true, doorEntity: 4, trapSource: 5, firingTick: 6);
        Assert.Throws<InvalidOperationException>(() => NativeCatacombsChestStage.VerifyAvailability(8, null, hasChest: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("98")]
    public void AvailabilityModeDoesNotPermitARejectedPickupWrongItemOrBadItemLevel(string? seed)
    {
        NativeCatacombsChestStage.VerifyAvailability(8, seed, true);
        var after = new Snapshot { Players = { new PlayerSnapshot { EntityId = 1, Gold = 100 } } };
        var drop = new WorldItemSnapshot { EntityId = 20, LevelId = 8, ItemSeed = 42,
            State = new ItemStateSnapshot { StableItemId = 100, CreateInfo = 16, ItemType = 1 } };
        var accepted = new CommandResult { Status = CommandStatus.Accepted };
        Assert.Equal(0U, NativeCatacombsChestStage.VerifyPickup(accepted, after, drop, 8, 100, 1));
        Assert.Throws<InvalidOperationException>(() => NativeCatacombsChestStage.VerifyPickup(
            new CommandResult { Status = CommandStatus.Rejected, RejectReason = CommandRejectReason.InvalidTarget }, after, drop, 8, 100, 1));
        Assert.Throws<InvalidOperationException>(() => NativeCatacombsChestStage.VerifyPickup(accepted, after, drop, 8, 100, 0));
        Assert.Throws<InvalidOperationException>(() => NativeCatacombsChestStage.VerifyPickup(accepted, after, drop, 8, 100, 2));
        after.WorldItems.Add(drop.Clone());
        Assert.Throws<InvalidOperationException>(() => NativeCatacombsChestStage.VerifyPickup(accepted, after, drop, 8, 100, 1));
        after.WorldItems.Clear();
        drop.State.CreateInfo = 15;
        Assert.Throws<InvalidOperationException>(() => NativeCatacombsChestStage.VerifyPickup(accepted, after, drop, 8, 100, 1));
    }

    [Theory]
    [InlineData(5U)]
    [InlineData(6U)]
    [InlineData(7U)]
    [InlineData(8U)]
    public void NativeRandomAndUsefulChestItemLevelsAndGoldRemainValid(uint level)
    {
        var after = new Snapshot { Players = { new PlayerSnapshot { EntityId = 1, Gold = 125 } } };
        var accepted = new CommandResult { Status = CommandStatus.Accepted };
        var drop = new WorldItemSnapshot { ItemSeed = 42, LevelId = level,
            State = new ItemStateSnapshot { StableItemId = 100, ItemType = 1, CreateInfo = 2 * level } };
        Assert.Equal(0U, NativeCatacombsChestStage.VerifyPickup(accepted, after, drop, level, 100, 1));
        drop.State.CreateInfo = level | 0x180U; // Native SetupAllUseful.
        Assert.Equal(0U, NativeCatacombsChestStage.VerifyPickup(accepted, after, drop, level, 100, 1));
        drop.State.ItemType = 11; drop.State.CreateInfo = 2 * level;
        Assert.Equal(25U, NativeCatacombsChestStage.VerifyPickup(accepted, after, drop, level, 100, 0));
    }
}

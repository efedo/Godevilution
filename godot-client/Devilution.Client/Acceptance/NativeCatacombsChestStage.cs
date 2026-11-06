using Devilution.Client.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client.Acceptance;

/** The unseeded journey requires a chest; selected seeds try it opportunistically. Pickup correctness is unconditional. */
internal static class NativeCatacombsChestStage
{
    internal static bool RequiresEvidence(string? explicitCatacombsSeed)
        => NativeDepthResumeEvidence.RequiresChestEvidence(explicitCatacombsSeed is not null);

    internal static void VerifyAvailability(uint deepestLevel, string? explicitCatacombsSeed, bool hasChest)
    {
        if (deepestLevel >= 5 && RequiresEvidence(explicitCatacombsSeed) && !hasChest)
            throw new InvalidOperationException("No visited Catacombs floor offered a reachable chest with a drop to pick up.");
    }

    internal static uint VerifyPickup(CommandResult receipt, Snapshot after, WorldItemSnapshot drop,
        uint chestLevel, uint goldBefore, int itemOccurrences)
    {
        if (!AuthoritativeCommandAckValidator.IsAcceptedOrSuccessfulDuplicate(receipt))
            throw new InvalidOperationException($"The native chest drop pickup was rejected: {receipt.Status}/{receipt.RejectReason}.");
        // Native items.cpp SetupBaseItem uses 2*curlv; SetupAllUseful stores curlv with CF_USEFUL (0x180).
        var createInfo = drop.State?.CreateInfo ?? 0;
        var expectedLevel = (createInfo & 0x180U) == 0x180U ? chestLevel : 2 * chestLevel;
        if (drop.LevelId != chestLevel || drop.State is null || drop.State.StableItemId == 0
            || (createInfo & 0x3fU) != expectedLevel)
            throw new InvalidOperationException("The native Catacombs chest drop has the wrong identity, floor or item level.");
        if (NativePickedUpItemIdentity.IsOnFloor(after, drop.ItemSeed, drop.State.StableItemId))
            throw new InvalidOperationException("The Catacombs drop was not completely picked up through the client control.");
        var gold = drop.State.ItemType == 11;
        var goldAfter = after.Players.Single().Gold;
        if (gold ? goldAfter <= goldBefore : itemOccurrences != 1)
            throw new InvalidOperationException("The Catacombs chest drop did not reach the player's inventory.");
        return gold ? goldAfter - goldBefore : 0;
    }
}

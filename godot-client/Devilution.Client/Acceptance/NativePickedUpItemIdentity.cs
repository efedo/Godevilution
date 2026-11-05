using Devilution.Protocol.V1;

namespace Devilution.Client.Acceptance;

/** Native slots and freed entity ids can be reused; the picked-up item's identity must stay off every floor. */
internal static class NativePickedUpItemIdentity
{
    internal static bool IsOnFloor(Snapshot snapshot, uint seed, uint stableItemId)
        => snapshot.WorldItems.Any(value => value.ItemSeed == seed && value.State.StableItemId == stableItemId);
}

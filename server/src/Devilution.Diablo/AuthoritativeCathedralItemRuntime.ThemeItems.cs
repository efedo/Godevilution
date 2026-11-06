namespace Devilution.Diablo;

public partial class AuthoritativeCathedralItemRuntime
{
    internal AuthoritativeCathedralItemChestTransaction PrepareThemeItemLoot(uint levelId,
        AuthoritativeNativeThemeItemLootRequest request)
    {
        var level = GetLevel(levelId);
        var profileLevel = GetLevelProfile(levelId);
        if (profileLevel.Depth != request.Policy.Depth
            || !profileLevel.ObjectBindings.Any(value => value.NativeObjectSlot == request.ObjectSlot
                && value.ObjectType == request.NativeObjectId && value.PositionX == request.PositionX
                && value.PositionY == request.PositionY)
            || request.WorldState.ActiveItemCount != level.ActiveItemCount
            || !request.WorldState.Items.SequenceEqual(level.Items)
            || !request.WorldState.ItemRefs.SequenceEqual(level.SignedItemRefs)
            || !request.WorldState.ActiveItemIds.SequenceEqual(level.ActiveItemIds)
            || !request.WorldState.UniqueItemFlags.SetEquals(uniqueItemFlags))
            throw new InvalidOperationException("Native item object preparation requires its unchanged shared item pool.");
        // Operation support is checked by the population owner, not the immutable saved placement-policy tag.
        // This preserves pre-handler ExplicitlyRefused bindings and their generation fingerprints.
        var result = AuthoritativeNativeThemeItemLootGenerator.Generate(request);
        return BuildChestTransaction(level, result.Loot);
    }
}

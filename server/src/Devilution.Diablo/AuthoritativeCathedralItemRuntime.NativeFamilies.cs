namespace Devilution.Diablo;

public partial class AuthoritativeCathedralItemRuntime
{
    /** C2 chest facts and loot stay detached until the ordinary versioned item commit. */
    internal (AuthoritativeCathedralItemChestTransaction Items, AuthoritativeCatacombsChestLootResult Operation)
        PrepareCatacombsChestLoot(uint levelId, AuthoritativeCatacombsChestLootRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Chest);
        ArgumentNullException.ThrowIfNull(request.WorldState);
        ArgumentNullException.ThrowIfNull(request.Policy);
        var family = NativeItemFamily(levelId);
        if (!family.IsCatacombs || request.Policy.Depth != family.Depth)
            throw new InvalidDataException("Catacombs chest transactions require their explicit family pool.");
        var level = GetLevel(levelId);
        var chest = request.Chest;
        if (!GetLevelProfile(levelId).ObjectBindings.Any(binding =>
                binding.NativeObjectSlot == chest.ObjectSlot && binding.ObjectType == chest.NativeObjectId
                && binding.MutationPolicy == AuthoritativeCathedralItemObjectMutationPolicy.NativeChest
                && binding.PositionX == chest.PositionX && binding.PositionY == chest.PositionY))
            throw new InvalidDataException("Catacombs chest facts do not match their owned object binding.");
        if (request.WorldState.ActiveItemCount != level.ActiveItemCount
            || !request.WorldState.ActiveItemIds.SequenceEqual(level.ActiveItemIds)
            || !request.WorldState.Items.SequenceEqual(level.Items)
            || !request.WorldState.ItemRefs.SequenceEqual(level.SignedItemRefs)
            || !request.WorldState.UniqueItemFlags.SetEquals(uniqueItemFlags))
            throw new InvalidOperationException("Catacombs chest loot was prepared from stale item-pool state.");
        var operation = AuthoritativeCatacombsChestLootGenerator.Generate(request, family);
        return (BuildChestTransaction(level, operation.Loot), operation);
    }

    private static void ValidateNativeFamilyDepth(uint levelId, int depth,
        IReadOnlyDictionary<uint, AuthoritativeNativeDungeonFamilyContext>? contexts)
    {
        if (contexts is null) {
            if (depth is < 1 or > 4)
                throw new InvalidDataException("Native Cathedral item level depth is outside the supported range.");
            return;
        }
        if (!contexts.TryGetValue(levelId, out var context))
            throw new InvalidDataException("Native item level has no explicit family owner.");
        context.RequireMatch(depth);
    }

    private static void ValidateNativeFamilyCoverage(IEnumerable<(uint LevelId, int Depth)> levels,
        IReadOnlyDictionary<uint, AuthoritativeNativeDungeonFamilyContext>? contexts)
    {
        if (contexts is null)
            return;
        var entries = levels.ToArray();
        if (entries.Length == 0 || entries.Select(level => level.LevelId).Distinct().Count() != entries.Length
            || !entries.Select(level => level.LevelId).Order().SequenceEqual(contexts.Keys.Order())
            || entries.Any(level => level.LevelId == 0))
            throw new InvalidDataException("Native item levels require complete, unique explicit family ownership.");
        foreach (var entry in entries)
            ValidateNativeFamilyDepth(entry.LevelId, entry.Depth, contexts);
    }

    private AuthoritativeNativeDungeonFamilyContext NativeItemFamily(uint levelId)
    {
        var level = GetLevelProfile(levelId);
        ValidateNativeFamilyDepth(levelId, level.Depth, nativeFamilyContexts);
        return nativeFamilyContexts is null
            ? AuthoritativeNativeDungeonFamilyContext.Cathedral(level.Depth)
            : nativeFamilyContexts[levelId];
    }
}

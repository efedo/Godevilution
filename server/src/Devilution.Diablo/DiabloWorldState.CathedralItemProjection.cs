namespace Devilution.Diablo;

public sealed partial class DiabloWorldState
{
    internal bool MatchesNativeWorldItemProjection(DiabloWorldItemState item)
    {
        var expected = cathedralItems?.GetProjection(item.LevelId, item.EntityId);
        return expected is not null && item.ItemSeed == expected.ItemSeed && item.Price == expected.Price
            && item.PositionX == expected.Raw.PositionX && item.PositionY == expected.Raw.PositionY
            && item.State == expected.State;
    }

    internal DiabloWorldItemState RestoreNativeWorldItemProjection(DiabloWorldItemState item)
    {
        var expected = cathedralItems?.GetProjection(item.LevelId, item.EntityId);
        // Older snapshot writers filled an uncalculated ground Gold _iIvalue with its value.
        // Accept only that exact legacy projection, then recover the unchanged raw pool payload.
        if (expected?.State is { ItemType: 11, IdentifiedValue: 0 } raw
            && item.State == raw with { IdentifiedValue = raw.Value })
            return item with { State = raw };
        return item;
    }
}

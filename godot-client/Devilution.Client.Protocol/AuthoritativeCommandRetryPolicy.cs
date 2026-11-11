using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Conservative staleness checks, not gameplay admission: the host still validates every fresh command. */
internal static class AuthoritativeCommandRetryPolicy
{
    internal sealed record Context(uint Actor, uint Level, int X, int Y, string Content, string Rules,
        uint Store, bool Stash, ObjectSnapshot? Object, WorldItemSnapshot? Drop,
        (uint Seed, ItemStateSnapshot State)? Consumable, (uint Seed, ItemStateSnapshot State)? Target, QuestSnapshot? Quest);

    public static Context? Capture(Command command, Snapshot? snapshot)
    {
        if (snapshot?.Players.FirstOrDefault() is not { Life: > 0 } player || !Supported(command.IntentCase)) return null;
        var obj = command.OperateObjectRequested is { } operate
            ? snapshot.Objects.FirstOrDefault(row => row.EntityId == operate.ObjectEntityId) : null;
        var drop = command.PickupWorldItemRequested is { } pickup
            ? snapshot.WorldItems.FirstOrDefault(row => row.EntityId == pickup.ItemEntityId) : null;
        var use = command.UseConsumableRequested;
        var source = use?.Item ?? BookReference(command);
        return new(player.EntityId, player.LevelId, player.PositionX, player.PositionY,
            snapshot.ContentManifestHash, snapshot.RulesetIdentityHash, snapshot.ActiveStore?.StoreId ?? 0, player.StashOpen,
            obj?.Clone(), drop?.Clone(), source is null ? null : Item(player, source), use?.TargetItem is null ? null : Item(player, use.TargetItem),
            command.TalkToQuestRequested is { } talk ? snapshot.Quests.FirstOrDefault(row => row.QuestId == talk.QuestId)?.Clone() : null);
    }

    public static bool StillValid(Command command, Context context, Snapshot snapshot)
    {
        var player = snapshot.Players.FirstOrDefault();
        if (player is not { Life: > 0 } || player.EntityId != context.Actor || player.LevelId != context.Level
            || snapshot.ContentManifestHash != context.Content || snapshot.RulesetIdentityHash != context.Rules) return false;
        switch (command.IntentCase) {
        case Command.IntentOneofCase.OpenStoreRequested:
        case Command.IntentOneofCase.CloseStoreRequested:
            return (snapshot.ActiveStore?.StoreId ?? 0) == context.Store && player.PositionX == context.X && player.PositionY == context.Y;
        case Command.IntentOneofCase.OpenStashRequested:
        case Command.IntentOneofCase.CloseStashRequested:
            return player.StashOpen == context.Stash && player.PositionX == context.X && player.PositionY == context.Y;
        case Command.IntentOneofCase.MoveRequested:
            var move = command.MoveRequested;
            return move.TargetCell
                ? move.TargetX >= 0 && move.TargetY >= 0
                    && snapshot.LevelGeometry.Any(level => level.LevelId == player.LevelId
                        && move.TargetX < level.Width && move.TargetY < level.Height)
                : player.PositionX == context.X && player.PositionY == context.Y
                    && move.DirectionX is >= -1 and <= 1 && move.DirectionY is >= -1 and <= 1;
        case Command.IntentOneofCase.OperateObjectRequested:
            var current = snapshot.Objects.FirstOrDefault(row => row.EntityId == command.OperateObjectRequested.ObjectEntityId);
            // Ignore cosmetic animation ticks; a changed door, capability or effect is a different interaction.
            return context.Object is { } before && current is not null && !current.Destroyed && current.LevelId == player.LevelId
                && (!current.Activated || current.CathedralDoor is not null || current.NativeThemeObjectInteractable)
                && SameObject(before, current) && Near(player, current.PositionX, current.PositionY, 2);
        case Command.IntentOneofCase.PickupWorldItemRequested:
            var item = snapshot.WorldItems.FirstOrDefault(row => row.EntityId == command.PickupWorldItemRequested.ItemEntityId);
            return context.Drop is { } old && item is not null && item.LevelId == player.LevelId
                && item.Equals(old)
                && Near(player, item.PositionX, item.PositionY, 1);
        case Command.IntentOneofCase.UseConsumableRequested:
            var use = command.UseConsumableRequested;
            return context.Consumable is { } source && Item(player, use.Item)?.Equals(source) == true
                && (use.TargetItem is null || context.Target is { } target && Item(player, use.TargetItem)?.Equals(target) == true);
        case Command.IntentOneofCase.ReadBookRequested:
            return context.Consumable is { } book && Item(player, BookReference(command))?.Equals(book) == true;
        case Command.IntentOneofCase.TalkToTownerRequested:
            return snapshot.NativeTownResidents.Any(row => row.TownerId == command.TalkToTownerRequested.TownerId
                && row.LevelId == player.LevelId && Near(player, row.PositionX, row.PositionY, 2));
        case Command.IntentOneofCase.TalkToQuestRequested:
            return context.Quest is not null && context.Quest.Equals(snapshot.Quests.FirstOrDefault(row => row.QuestId == command.TalkToQuestRequested.QuestId))
                && snapshot.QuestTownerInteractions.Any(row => row.QuestId == context.Quest.QuestId && row.LevelId == player.LevelId
                    && Near(player, row.PositionX, row.PositionY, 2));
        default: return false;
        }
    }

    private static bool Supported(Command.IntentOneofCase intent) => intent is
        Command.IntentOneofCase.OpenStoreRequested or Command.IntentOneofCase.CloseStoreRequested
        or Command.IntentOneofCase.OpenStashRequested or Command.IntentOneofCase.CloseStashRequested
        or Command.IntentOneofCase.MoveRequested or Command.IntentOneofCase.OperateObjectRequested
        or Command.IntentOneofCase.TalkToTownerRequested or Command.IntentOneofCase.TalkToQuestRequested
        or Command.IntentOneofCase.PickupWorldItemRequested or Command.IntentOneofCase.UseConsumableRequested
        or Command.IntentOneofCase.ReadBookRequested;

    private static PlayerItemReference? BookReference(Command command)
        => command.ReadBookRequested is not { } book ? null : book.Item
            ?? new PlayerItemReference { Location = PlayerItemLocation.Inventory, Slot = book.InventoryIndex };

    private static bool Near(PlayerSnapshot player, int x, int y, int range)
        => Math.Abs((long)player.PositionX - x) <= range && Math.Abs((long)player.PositionY - y) <= range;

    private static bool SameObject(ObjectSnapshot before, ObjectSnapshot after)
    {
        var left = before.Clone(); var right = after.Clone();
        left.AnimationFrame = right.AnimationFrame = 0;
        return left.Equals(right);
    }

    private static (uint Seed, ItemStateSnapshot State)? Item(PlayerSnapshot player, PlayerItemReference? reference)
    {
        if (reference is null) return null;
        uint seed; ItemStateSnapshot? state;
        switch (reference.Location) {
        case PlayerItemLocation.Inventory when reference.Slot < player.Inventory.Count:
            var inventory = player.Inventory[(int)reference.Slot]; seed = inventory.ItemSeed; state = inventory.State; break;
        case PlayerItemLocation.Belt:
            var belt = player.Belt.FirstOrDefault(row => row.Slot == reference.Slot);
            if (belt is null) return null;
            seed = belt.ItemSeed; state = belt.State; break;
        case PlayerItemLocation.Equipment:
            var equipped = player.Equipment.FirstOrDefault(row => row.Slot == reference.Slot);
            if (equipped is null) return null;
            seed = equipped.ItemSeed; state = equipped.State; break;
        default: return null;
        }
        return seed == 0 || state is null || state.StableItemId == 0 || reference.StableItemId != 0 && reference.StableItemId != state.StableItemId
            ? null : (seed, state.Clone());
    }
}

using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Selects the first server-declared quest/towner stage available to the local player. */
public static class AuthoritativeQuestTownerInteractionSelector
{
    private const int ActivateKind = 0;

    public static QuestTownerInteractionSnapshot? Select(Snapshot? snapshot)
    {
        var player = snapshot?.Players.FirstOrDefault();
        if (player is null)
            return null;

        var quests = snapshot!.Quests.ToDictionary(quest => quest.QuestId);
        var geometry = snapshot.LevelGeometry.FirstOrDefault(level => level.LevelId == player.LevelId);
        var lightRadius = player.LightRadius is >= 2 and <= 15 ? player.LightRadius : 10;
        return Available(snapshot, player, quests, geometry, lightRadius)
            .OrderBy(interaction => interaction.InteractionId)
            .FirstOrDefault();
    }

    /** Selects an available authored interaction at the exact clicked tile. */
    public static QuestTownerInteractionSnapshot? SelectAt(
        Snapshot? snapshot,
        int x,
        int y)
    {
        var player = snapshot?.Players.FirstOrDefault();
        if (player is null)
            return null;

        var quests = snapshot!.Quests.ToDictionary(quest => quest.QuestId);
        var geometry = snapshot.LevelGeometry.FirstOrDefault(level => level.LevelId == player.LevelId);
        var lightRadius = player.LightRadius is >= 2 and <= 15 ? player.LightRadius : 10;
        return Available(snapshot, player, quests, geometry, lightRadius)
            .Where(interaction => interaction.PositionX == x && interaction.PositionY == y)
            .OrderBy(interaction => interaction.InteractionId)
            .FirstOrDefault();
    }

    private static IEnumerable<QuestTownerInteractionSnapshot> Available(
        Snapshot snapshot,
        PlayerSnapshot player,
        IReadOnlyDictionary<uint, QuestSnapshot> quests,
        LevelGeometrySnapshot? geometry,
        int lightRadius)
    {
        return snapshot.QuestTownerInteractions
            .Where(interaction => IsAvailable(
                interaction,
                player,
                quests,
                geometry,
                lightRadius));
    }

    private static bool IsAvailable(
        QuestTownerInteractionSnapshot interaction,
        PlayerSnapshot player,
        IReadOnlyDictionary<uint, QuestSnapshot> quests,
        LevelGeometrySnapshot? geometry,
        int lightRadius)
    {
        if (interaction.InteractionId == 0
            || interaction.QuestId == 0
            || interaction.TownerId is < 0 or > byte.MaxValue
            || interaction.LevelId != player.LevelId
            || Math.Max(
                Math.Abs((long)interaction.PositionX - player.PositionX),
                Math.Abs((long)interaction.PositionY - player.PositionY)) > 1
            || !AuthoritativeNativeVisionVisibility.IsVisible(
                interaction.LevelId,
                interaction.PositionX,
                interaction.PositionY,
                player.LevelId,
                geometry,
                player.PositionX,
                player.PositionY,
                lightRadius)
            || !quests.TryGetValue(interaction.QuestId, out var quest)
            || quest.Unavailable) {
            return false;
        }

        if (interaction.AllowCompleted ? !quest.Completed : quest.Completed)
            return false;
        if (interaction.RequireUnlogged && quest.Logged)
            return false;

        var hasRequiredVisitedLevel =
            (interaction.RequiredVisitedLevel1 != 0
                && player.VisitedLevelIds.Contains(interaction.RequiredVisitedLevel1))
            || (interaction.RequiredVisitedLevel2 != 0
                && player.VisitedLevelIds.Contains(interaction.RequiredVisitedLevel2));
        if ((interaction.RequiredVisitedLevel1 != 0
                || interaction.RequiredVisitedLevel2 != 0)
            && !hasRequiredVisitedLevel) {
            return false;
        }

        var active = interaction.Kind == ActivateKind
            ? quest.Initial
                || (interaction.HasRequiredVariable1
                    || interaction.HasRequiredVariable2)
            : !quest.Initial
                || interaction.AllowInitial && quest.Initial;
        if (!active
            || (interaction.HasRequiredVariable1
                && quest.Variable1 != interaction.RequiredVariable1)
            || (interaction.HasRequiredVariable2
                && quest.Variable2 != interaction.RequiredVariable2)) {
            return false;
        }

        if (interaction.ItemId != 0 && !HasItem(player, interaction.ItemId))
            return false;
        if (interaction.RequiredItemId != 0) {
            var hasRequiredItem = HasItem(player, interaction.RequiredItemId);
            if (interaction.HasRequireItemPresent
                ? interaction.RequireItemPresent != hasRequiredItem
                : !hasRequiredItem) {
                return false;
            }
        }
        return true;
    }

    private static bool HasItem(PlayerSnapshot player, uint itemId)
    {
        return player.Inventory.Any(item => item.State?.StableItemId == itemId)
            || player.Belt.Any(item => item.State?.StableItemId == itemId);
    }
}

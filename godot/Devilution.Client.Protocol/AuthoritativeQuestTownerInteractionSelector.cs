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
        return snapshot.QuestTownerInteractions
            .OrderBy(interaction => interaction.InteractionId)
            .FirstOrDefault(interaction => IsAvailable(interaction, player, quests));
    }

    private static bool IsAvailable(
        QuestTownerInteractionSnapshot interaction,
        PlayerSnapshot player,
        IReadOnlyDictionary<uint, QuestSnapshot> quests)
    {
        if (interaction.InteractionId == 0
            || interaction.QuestId == 0
            || interaction.TownerId is < 0 or > byte.MaxValue
            || interaction.LevelId != player.LevelId
            || Math.Max(
                Math.Abs(interaction.PositionX - player.PositionX),
                Math.Abs(interaction.PositionY - player.PositionY)) > 1
            || !quests.TryGetValue(interaction.QuestId, out var quest)
            || quest.Unavailable) {
            return false;
        }

        if (interaction.AllowCompleted ? !quest.Completed : quest.Completed)
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
            : !quest.Initial;
        if (!active
            || (interaction.HasRequiredVariable1
                && quest.Variable1 != interaction.RequiredVariable1)
            || (interaction.HasRequiredVariable2
                && quest.Variable2 != interaction.RequiredVariable2)) {
            return false;
        }

        return interaction.ItemId == 0 || HasItem(player, interaction.ItemId);
    }

    private static bool HasItem(PlayerSnapshot player, uint itemId)
    {
        return player.Inventory.Any(item => item.State?.StableItemId == itemId)
            || player.Belt.Any(item => item.State?.StableItemId == itemId);
    }
}

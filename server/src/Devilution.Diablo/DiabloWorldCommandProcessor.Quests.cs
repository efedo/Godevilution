using Devilution.Domain.Inventory;
using Devilution.Engine;

namespace Devilution.Diablo;

public sealed partial class DiabloWorldCommandProcessor
{
    private DiabloWorldIntentResult AdvanceQuest(
        AuthoritativePlayerState player,
        uint questId)
    {
        if (!world.Quests.TryGetValue(questId, out var quest)
            || quest.Unavailable
            || quest.Completed
            || quest.LevelId != player.LevelId) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        quest.Active = true;
        quest.Logged = true;
        quest.Progress = Math.Min(quest.RequiredProgress, checked(quest.Progress + 1));
        if (quest.Progress >= quest.RequiredProgress) {
            quest.Completed = true;
            quest.Active = false;
        }
        return DiabloWorldIntentResult.Accepted;
    }

    /** Validates and emits one general towner greeting without mutating quest state. */
    private DiabloWorldIntentResult TalkToTowner(
        AuthoritativePlayerState player,
        int townerId)
    {
        if (townerGreetings is null
            || !townerGreetings.TryGet(townerId, out var greeting)
            || greeting.LevelId != player.LevelId
            || player.HeldItem is not null
            || DeterministicGridMetrics.ChebyshevDistance(
                greeting.PositionX,
                greeting.PositionY,
                player.PositionX,
                player.PositionY) > 1) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        if (townerId == HealerTownerId && player.Life > 0)
            player.Life = player.LifeMaximum;

        return DiabloWorldIntentResult.AcceptedWith(
            new DiabloGameplayEvent(
                DiabloGameplayEventKind.Dialogue,
                player.EntityId,
                0,
                0) {
                MessageId = greeting.MessageId,
            });
    }

    /**
     * Starts the native quest-dialogue boundary without exposing presentation
     * text or encounter state to the client. Content supplies the quest's
     * message identity and unique completion identity; the server validates
     * both before activating or completing the conversation.
     */
    private DiabloWorldIntentResult TalkToQuest(
        AuthoritativePlayerState player,
        uint questId,
        uint targetEntityId,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        if (!participants.Contains(player))
            participants = participants.Append(player).ToArray();
        if (!world.Quests.TryGetValue(questId, out var quest)
            || quest.Unavailable
            || quest.Completed
            || quest.LevelId != player.LevelId
            || string.IsNullOrWhiteSpace(quest.CompletionUniqueIdentity)
            || !world.CombatTargets.TryGetValue(targetEntityId, out var target)
            || !target.IsUnique
            || target.HitPoints <= 0
            || target.LevelId != player.LevelId
            || !string.Equals(
                target.UniqueIdentity,
                quest.CompletionUniqueIdentity,
                StringComparison.Ordinal)
            || DeterministicGridMetrics.ChebyshevDistance(
                target.PositionX,
                target.PositionY,
                player.PositionX,
                player.PositionY) > 1) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        if (questDialogueStages?.ContainsQuest(questId) == true) {
            bool MatchesStageItemRequirement(AuthoritativeQuestDialogueStage candidate)
            {
                if (candidate.RequiredItemId == 0)
                    return candidate.RequireItemPresent;
                if (items is null
                    || !items.TryGet(candidate.RequiredItemId, out var candidateItem)) {
                    return false;
                }
                var hasItem = player.Inventory.Any(item =>
                    items.IsStateForDefinition(item.State, candidateItem));
                return hasItem == candidate.RequireItemPresent;
            }

            if (!questDialogueStages.TryGet(
                    questId,
                    quest.Variable1,
                    quest.Variable2,
                    MatchesStageItemRequirement,
                    out var stage)) {
                return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }

            if (stage.RewardKind != AuthoritativeQuestDialogueRewardKind.None) {
                if (items is null)
                    return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
                var rewardX = checked(target.PositionX + stage.RewardOffsetX);
                var rewardY = checked(target.PositionY + stage.RewardOffsetY);
                if (!CanPlaceWorldItemReward(
                        target.LevelId,
                        rewardX,
                        rewardY,
                        participants))
                    return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }

            if (stage.FollowUpKind == AuthoritativeQuestDialogueFollowUpKind.WorldMutation
                && world.WorldMutations?.TryGet(stage.FollowUpAmount, out _) != true) {
                return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }

            var stageInventoryIndex = -1;
            if (stage.RequiredItemId != 0) {
                if (items is null
                    || !items.TryGet(stage.RequiredItemId, out var requiredItem)) {
                    return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
                }
                for (var index = 0; index < player.Inventory.Count; index++) {
                    if (items.IsStateForDefinition(
                            player.Inventory[index].State,
                            requiredItem)) {
                        stageInventoryIndex = index;
                        break;
                    }
                }
                if (stage.RequireItemPresent && stageInventoryIndex < 0
                    || !stage.RequireItemPresent && stageInventoryIndex >= 0) {
                    return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
                }
            }

            if (stageInventoryIndex >= 0 && stage.ConsumeItem) {
                InventoryGridLayout.RemoveAndCompact(
                    player.InventoryGrid,
                    stageInventoryIndex);
                player.Inventory.RemoveAt(stageInventoryIndex);
            }

            quest.Logged = true;
            quest.Variable1 = Math.Max(quest.Variable1, stage.NextVariable1);
            quest.Variable2 = Math.Max(quest.Variable2, stage.NextVariable2);
            var nextMessageId = questDialogueStages.TryGet(
                    questId,
                    quest.Variable1,
                    quest.Variable2,
                    out var nextStage)
                ? nextStage.MessageId
                : 0;
            target.DialogueMessageId = stage.NextMessageId != 0
                ? stage.NextMessageId
                : nextMessageId;
            target.DialogueState = target.DialogueMessageId != 0
                ? AuthoritativeMonsterDialogueState.Inquiring
                : AuthoritativeMonsterDialogueState.Combat;
            if (stage.CompleteQuest) {
                quest.Progress = quest.RequiredProgress;
                quest.Completed = true;
                quest.Active = false;
            } else {
                quest.Active = true;
            }
            if (stage.RewardKind != AuthoritativeQuestDialogueRewardKind.None)
                SpawnDialogueStageReward(stage, target, participants);
            ApplyQuestDialogueFollowUp(stage);
            if (stage.DefeatTarget)
                target.DefeatForDialogue();
            return DiabloWorldIntentResult.AcceptedWith(
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Dialogue,
                    player.EntityId,
                    target.EntityId,
                    0) {
                    MessageId = stage.MessageId,
                    QuestId = quest.QuestId,
                });
        }

        var dialogueInventoryIndex = -1;
        if (quest.DialogueItemId != 0) {
            if (items is null || !items.TryGet(quest.DialogueItemId, out var dialogueItem))
                return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            for (var index = 0; index < player.Inventory.Count; index++) {
                if (items.IsStateForDefinition(player.Inventory[index].State, dialogueItem)) {
                    dialogueInventoryIndex = index;
                    break;
                }
            }
            if (dialogueInventoryIndex >= 0
                && (quest.DialogueRewardUniqueId != 0 || quest.DialogueRewardItemId != 0)) {
                var rewardX = checked(target.PositionX + quest.DialogueRewardOffsetX);
                var rewardY = checked(target.PositionY + quest.DialogueRewardOffsetY);
                if (!CanPlaceWorldItemReward(
                        target.LevelId,
                        rewardX,
                        rewardY,
                        participants))
                    return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }
        }

        var rewardOnActivation = quest.DialogueRewardOnActivation && !quest.Active;
        if (rewardOnActivation
            && (items is null
                || (quest.DialogueRewardUniqueId == 0
                    && quest.DialogueRewardItemId == 0))) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        if ((rewardOnActivation || dialogueInventoryIndex >= 0)
            && (quest.DialogueRewardUniqueId != 0 || quest.DialogueRewardItemId != 0)) {
            var rewardX = checked(target.PositionX + quest.DialogueRewardOffsetX);
            var rewardY = checked(target.PositionY + quest.DialogueRewardOffsetY);
            if (!CanPlaceWorldItemReward(
                    target.LevelId,
                    rewardX,
                    rewardY,
                    participants))
                return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        if (dialogueInventoryIndex >= 0) {
            InventoryGridLayout.RemoveAndCompact(
                player.InventoryGrid,
                dialogueInventoryIndex);
            player.Inventory.RemoveAt(dialogueInventoryIndex);
            quest.Progress = quest.RequiredProgress;
            quest.Completed = true;
            quest.Active = false;
            quest.Logged = true;
            quest.Variable1 = Math.Max(quest.Variable1, quest.CompletionVariable1);
            quest.Variable2 = Math.Max(quest.Variable2, quest.CompletionVariable2);
            target.DialogueMessageId = quest.DialogueCompletionMessageId;
            target.DialogueState = target.DialogueMessageId != 0
                ? AuthoritativeMonsterDialogueState.Inquiring
                : AuthoritativeMonsterDialogueState.Unspecified;

            if (quest.CompletionActivateQuestId != 0
                && world.Quests.TryGetValue(quest.CompletionActivateQuestId, out var nextQuest)
                && !nextQuest.Unavailable
                && !nextQuest.Completed) {
                nextQuest.Active = true;
            }

            SpawnDialogueReward(quest, target, participants);
            if (quest.DialogueCompletionDefeatsTarget)
                target.DefeatForDialogue();

            return quest.DialogueCompletionMessageId == 0
                ? DiabloWorldIntentResult.Accepted
                : DiabloWorldIntentResult.AcceptedWith(
                    new DiabloGameplayEvent(
                        DiabloGameplayEventKind.Dialogue,
                        player.EntityId,
                        target.EntityId,
                        0) {
                        MessageId = quest.DialogueCompletionMessageId,
                        QuestId = quest.QuestId,
                    });
        }

        quest.Active = true;
        quest.Logged = true;
        var dialogueVariable1 = quest.DialogueVariable1;
        if (quest.DialogueCompletionVariable1 >= 0
            && quest.DialogueVariable1 > 0
            && quest.Variable1 >= quest.DialogueVariable1) {
            dialogueVariable1 = quest.DialogueCompletionVariable1;
        }
        quest.Variable1 = Math.Max(quest.Variable1, dialogueVariable1);
        quest.Variable2 = Math.Max(quest.Variable2, quest.DialogueVariable2);
        target.DialogueMessageId = quest.MessageId;
        target.DialogueState = target.DialogueMessageId != 0
            ? AuthoritativeMonsterDialogueState.Inquiring
            : AuthoritativeMonsterDialogueState.Unspecified;
        if (rewardOnActivation)
            SpawnDialogueReward(quest, target, participants);
        return DiabloWorldIntentResult.AcceptedWith(
            new DiabloGameplayEvent(
                DiabloGameplayEventKind.Dialogue,
                player.EntityId,
                target.EntityId,
                0) {
                MessageId = quest.MessageId,
                QuestId = quest.QuestId,
        });
    }

    private void ApplyQuestDialogueFollowUp(AuthoritativeQuestDialogueStage stage)
    {
        switch (stage.FollowUpKind) {
        case AuthoritativeQuestDialogueFollowUpKind.None:
            return;
        case AuthoritativeQuestDialogueFollowUpKind.WorldMutation:
            if (!world.ApplyWorldMutation(stage.FollowUpAmount))
                throw new InvalidDataException(
                    $"Quest dialogue follow-up references unknown world mutation {stage.FollowUpAmount}.");
            return;
        default:
            throw new InvalidDataException(
                $"Quest dialogue follow-up kind '{stage.FollowUpKind}' is unsupported.");
        }
    }

    /** Validates an authored world-item reward tile before quest mutation. */
    private bool CanPlaceWorldItemReward(
        uint levelId,
        int positionX,
        int positionY,
        IReadOnlyList<AuthoritativePlayerState> participants,
        uint excludedEntityId = 0,
        bool requireWalkable = true)
    {
        if (world.HasNativeItemLevel(levelId))
            return world.CanPutNativeItem(levelId, positionX, positionY);
        return (!requireWalkable || world.IsWalkable(levelId, positionX, positionY))
            && !world.IsCathedralDoorItemBlocked(levelId, positionX, positionY)
            && !world.IsOccupied(
                levelId,
                positionX,
                positionY,
                participants,
                excludedEntityId)
            && !world.WorldItems.Values.Any(item =>
                item.LevelId == levelId
                && item.PositionX == positionX
                && item.PositionY == positionY);
    }

    /** Generates a content-declared staged conversation reward once per stage. */
    private void SpawnDialogueStageReward(
        AuthoritativeQuestDialogueStage stage,
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        if (items is null)
            throw new InvalidDataException("The dialogue stage reward requires an active item catalog.");

        var itemSeed = stage.RewardKind == AuthoritativeQuestDialogueRewardKind.RandomUniqueMonsterDrop
            ? CreateDialogueRewardSeed(target.EntityId, target.MonsterId, stage.QuestId)
            : AllocateGoldStackSeed();
        var rewardState = stage.RewardKind switch {
            AuthoritativeQuestDialogueRewardKind.RandomUniqueMonsterDrop => items.GenerateRandomUniqueMonsterDrop(
                itemSeed,
                stage.RewardItemLevel > 0 ? stage.RewardItemLevel : target.MonsterLevel,
                spellBooks,
                gameMode),
            AuthoritativeQuestDialogueRewardKind.NormalItem => spellBooks is not null
                && spellBooks.IsBookTemplate(items, stage.RewardItemId)
                ? spellBooks.GenerateRandom(
                    items,
                    itemSeed,
                    stage.RewardItemLevel,
                    gameMode,
                    stage.RewardItemId)
                : items.Generate(stage.RewardItemId, itemSeed, stage.RewardItemLevel),
            AuthoritativeQuestDialogueRewardKind.UniqueItem => items.GenerateUnique(
                stage.RewardUniqueId,
                itemSeed,
                stage.RewardItemLevel),
            _ => throw new InvalidDataException(
                $"Dialogue stage reward kind '{stage.RewardKind}' is unsupported."),
        };
        var rewardX = checked(target.PositionX + stage.RewardOffsetX);
        var rewardY = checked(target.PositionY + stage.RewardOffsetY);
        var entityId = AllocateWorldItemEntityId(participants);
        world.SetWorldItem(new AuthoritativeWorldItem(
            entityId,
            target.LevelId,
            rewardX,
            rewardY,
            itemSeed,
            checked((uint)Math.Max(0, rewardState.IdentifiedValue)),
            rewardState));
    }

    private static uint CreateDialogueRewardSeed(
        uint targetEntityId,
        uint monsterId,
        uint questId)
    {
        var seed = targetEntityId
            ^ unchecked(monsterId * 0x9E3779B9U)
            ^ unchecked(questId * 0x85EBCA6BU);
        return seed == 0 ? 1U : seed;
    }

    /** Generates the authored one-time reward at the unique-monster dialogue position. */
    private void SpawnDialogueReward(
        AuthoritativeQuestState quest,
        AuthoritativeCombatTarget target,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        var rewardX = checked(target.PositionX + quest.DialogueRewardOffsetX);
        var rewardY = checked(target.PositionY + quest.DialogueRewardOffsetY);
        if (quest.DialogueRewardUniqueId != 0) {
            var itemSeed = AllocateGoldStackSeed();
            var rewardState = items!.GenerateUnique(
                quest.DialogueRewardUniqueId,
                itemSeed,
                quest.DialogueRewardItemLevel);
            var entityId = AllocateWorldItemEntityId(participants);
            world.SetWorldItem(new AuthoritativeWorldItem(
                entityId,
                target.LevelId,
                rewardX,
                rewardY,
                itemSeed,
                0,
                rewardState));
        }
        if (quest.DialogueRewardItemId != 0) {
            var itemSeed = AllocateGoldStackSeed();
            var rewardState = spellBooks is not null
                && spellBooks.IsBookTemplate(items!, quest.DialogueRewardItemId)
                ? spellBooks.GenerateRandom(
                    items!,
                    itemSeed,
                    quest.DialogueRewardItemLevel,
                    gameMode,
                    quest.DialogueRewardItemId)
                : items!.Generate(
                    quest.DialogueRewardItemId,
                    itemSeed,
                    quest.DialogueRewardItemLevel);
            var entityId = AllocateWorldItemEntityId(participants);
            world.SetWorldItem(new AuthoritativeWorldItem(
                entityId,
                target.LevelId,
                rewardX,
                rewardY,
                itemSeed,
                checked((uint)Math.Max(0, rewardState.IdentifiedValue)),
                rewardState));
        }
    }

    /** Applies the first active content-owned repeatable quest/towner stage. */
    private DiabloWorldIntentResult? TryQuestTownerInteraction(
        AuthoritativePlayerState player,
        uint questId,
        uint townerId,
        IReadOnlyList<AuthoritativePlayerState> participants,
        out bool hasActiveInteraction)
    {
        hasActiveInteraction = false;
        if (questTownerInteractions is null
            || !world.Quests.TryGetValue(questId, out var quest)
            || quest.Unavailable) {
            return null;
        }

        foreach (var interaction in questTownerInteractions.Definitions
                     .Where(definition => definition.QuestId == questId
                         && definition.TownerId == townerId)
                     .OrderBy(definition => definition.InteractionId)) {
            if (!IsQuestTownerInteractionActive(player, quest, interaction))
                continue;
            if (!IsRequiredItemConditionSatisfied(player, interaction)) {
                // A missing item-present gate still owns the interaction and
                // must prevent a fallback turn-in from mutating the quest.
                if (interaction.RequiredItemId != 0
                    && interaction.RequireItemPresent)
                    hasActiveInteraction = true;
                continue;
            }
            hasActiveInteraction = true;
            if (interaction.LevelId != player.LevelId
                || DeterministicGridMetrics.ChebyshevDistance(
                    interaction.PositionX,
                    interaction.PositionY,
                    player.PositionX,
                    player.PositionY) > 1)
                continue;

            if (interaction.ItemId != 0) {
                if (items is null
                    || !items.TryGet(interaction.ItemId, out var itemDefinition)
                    || !TryFindPlayerItem(
                        player,
                        itemDefinition,
                        consume: false,
                        out _)) {
                    return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
                }
            }
            if (interaction.UseQuestTurnInReward
                && interaction.Kind is (AuthoritativeQuestTownerInteractionKind.Activate
                    or AuthoritativeQuestTownerInteractionKind.Advance
                    or AuthoritativeQuestTownerInteractionKind.Complete)
                && (quest.TurnInRewardUniqueId != 0 || quest.TurnInRewardItemId != 0)
                && !CanPlaceWorldItemReward(
                    quest.TurnInLevelId,
                    quest.TurnInRewardPositionX,
                    quest.TurnInRewardPositionY,
                    participants,
                    player.EntityId,
                    requireWalkable: false)) {
                return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }
            if (interaction.UseQuestCompletionReward
                && interaction.Kind == AuthoritativeQuestTownerInteractionKind.CompleteAfterQuest
                && (quest.TurnInCompletionRewardUniqueId != 0
                    || quest.TurnInCompletionRewardItemId != 0)
                && !CanPlaceWorldItemReward(
                    quest.TurnInLevelId,
                    quest.TurnInCompletionRewardPositionX,
                    quest.TurnInCompletionRewardPositionY,
                    participants,
                    player.EntityId,
                    requireWalkable: false)) {
                return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }
            if (interaction.ItemId != 0 && interaction.ConsumeItem) {
                if (items is null
                    || !items.TryGet(interaction.ItemId, out var itemDefinition)
                    || !TryFindPlayerItem(
                        player,
                        itemDefinition,
                        consume: true,
                        out _)) {
                    return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
                }
            }
            quest.Logged = true;
            if (interaction.Variable1 >= 0)
                quest.Variable1 = Math.Max(quest.Variable1, interaction.Variable1);
            if (interaction.Variable2 >= 0)
                quest.Variable2 = Math.Max(quest.Variable2, interaction.Variable2);
            if (interaction.MessageId != 0)
                quest.MessageId = interaction.MessageId;
            if (interaction.Kind == AuthoritativeQuestTownerInteractionKind.Activate)
                quest.Active = true;
            if (interaction.Kind is (AuthoritativeQuestTownerInteractionKind.Activate
                    or AuthoritativeQuestTownerInteractionKind.Advance)
                && interaction.UseQuestTurnInReward
                && items is not null) {
                SpawnQuestTurnInReward(quest, participants);
            }
            if (interaction.Kind == AuthoritativeQuestTownerInteractionKind.CompleteAfterQuest) {
                if (quest.TurnInCompletionVariable2 >= 0)
                    quest.Variable2 = Math.Max(
                        quest.Variable2,
                        quest.TurnInCompletionVariable2);
                if (quest.TurnInCompletionVariable1 >= 0)
                    quest.Variable1 = Math.Max(
                        quest.Variable1,
                        quest.TurnInCompletionVariable1);
                if (quest.CompletionActivateQuestId != 0
                    && world.Quests.TryGetValue(
                        quest.CompletionActivateQuestId,
                        out var nextQuest)
                    && !nextQuest.Unavailable
                    && !nextQuest.Completed) {
                    nextQuest.Active = true;
                    nextQuest.Logged = true;
                }
                if (interaction.UseQuestCompletionReward && items is not null)
                    SpawnQuestCompletionReward(quest, participants);
            }
            if (interaction.Kind == AuthoritativeQuestTownerInteractionKind.Complete) {
                quest.Progress = quest.RequiredProgress;
                quest.Completed = true;
                quest.Active = false;
                if (quest.CompletionActivateQuestId != 0
                    && world.Quests.TryGetValue(
                        quest.CompletionActivateQuestId,
                        out var nextQuest)
                    && !nextQuest.Unavailable
                    && !nextQuest.Completed) {
                    nextQuest.Active = true;
                    nextQuest.Logged = true;
                }
                if (interaction.UseQuestTurnInReward && items is not null)
                    SpawnQuestTurnInReward(quest, participants);
            }

            return DiabloWorldIntentResult.AcceptedWith(
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Dialogue,
                    player.EntityId,
                    townerId,
                    0) {
                    MessageId = interaction.MessageId,
                    QuestId = quest.QuestId,
                });
        }
        return null;
    }

    /** Generates the normal item or unique reward authored on a quest turn-in. */
    private void SpawnQuestTurnInReward(
        AuthoritativeQuestState quest,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        if (items is null)
            throw new InvalidOperationException("Quest turn-in reward generation requires an item catalog.");
        if (quest.TurnInRewardUniqueId == 0 && quest.TurnInRewardItemId == 0)
            return;
        var itemSeed = AllocateGoldStackSeed();
        var entityId = AllocateWorldItemEntityId(participants);
        if (quest.TurnInRewardUniqueId != 0) {
            var rewardState = items.GenerateUnique(
                quest.TurnInRewardUniqueId,
                itemSeed,
                quest.TurnInRewardItemLevel);
            world.SetWorldItem(new AuthoritativeWorldItem(
                entityId,
                quest.TurnInLevelId,
                quest.TurnInRewardPositionX,
                quest.TurnInRewardPositionY,
                itemSeed,
                0,
                rewardState));
        } else if (quest.TurnInRewardItemId != 0) {
            var rewardState = items.Generate(
                quest.TurnInRewardItemId,
                itemSeed,
                quest.TurnInRewardItemLevel);
            world.SetWorldItem(new AuthoritativeWorldItem(
                entityId,
                quest.TurnInLevelId,
                quest.TurnInRewardPositionX,
                quest.TurnInRewardPositionY,
                itemSeed,
                checked((uint)Math.Max(0, rewardState.IdentifiedValue)),
                rewardState));
        }
    }

    /** Generates the reward authored for a completion-after-quest stage. */
    private void SpawnQuestCompletionReward(
        AuthoritativeQuestState quest,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        if (items is null)
            throw new InvalidOperationException("Quest completion reward generation requires an item catalog.");
        if (quest.TurnInCompletionRewardUniqueId == 0
            && quest.TurnInCompletionRewardItemId == 0)
            return;
        var itemSeed = AllocateGoldStackSeed();
        var entityId = AllocateWorldItemEntityId(participants);
        if (quest.TurnInCompletionRewardUniqueId != 0) {
            var rewardState = items.GenerateUnique(
                quest.TurnInCompletionRewardUniqueId,
                itemSeed,
                quest.TurnInCompletionRewardItemLevel);
            world.SetWorldItem(new AuthoritativeWorldItem(
                entityId,
                quest.TurnInLevelId,
                quest.TurnInCompletionRewardPositionX,
                quest.TurnInCompletionRewardPositionY,
                itemSeed,
                0,
                rewardState));
        } else if (quest.TurnInCompletionRewardItemId != 0) {
            var rewardState = items.Generate(
                quest.TurnInCompletionRewardItemId,
                itemSeed,
                quest.TurnInCompletionRewardItemLevel);
            world.SetWorldItem(new AuthoritativeWorldItem(
                entityId,
                quest.TurnInLevelId,
                quest.TurnInCompletionRewardPositionX,
                quest.TurnInCompletionRewardPositionY,
                itemSeed,
                checked((uint)Math.Max(0, rewardState.IdentifiedValue)),
                rewardState));
        }
    }

    private bool IsRequiredItemConditionSatisfied(
        AuthoritativePlayerState player,
        AuthoritativeQuestTownerInteractionDefinition interaction)
    {
        if (interaction.RequiredItemId == 0)
            return true;
        if (items is null || !items.TryGet(interaction.RequiredItemId, out var itemDefinition))
            return false;
        var hasItem = TryFindPlayerItem(player, itemDefinition, consume: false, out _);
        return interaction.RequireItemPresent == hasItem;
    }

    private static bool IsQuestTownerInteractionActive(
        AuthoritativePlayerState player,
        AuthoritativeQuestState quest,
        AuthoritativeQuestTownerInteractionDefinition interaction)
    {
        if (interaction.Kind == AuthoritativeQuestTownerInteractionKind.CompleteAfterQuest) {
            if (!quest.Completed || quest.Unavailable)
                return false;
        } else if (interaction.AllowCompleted) {
            if (!quest.Completed || quest.Unavailable)
                return false;
        } else if (interaction.Kind == AuthoritativeQuestTownerInteractionKind.Activate) {
            if (quest.Completed || quest.Unavailable
                || (quest.Active
                    && interaction.RequiredVariable1 < 0
                    && interaction.RequiredVariable2 < 0))
                return false;
        } else if ((!quest.Active && !interaction.AllowInitial)
            || quest.Completed
            || quest.Unavailable) {
            return false;
        }
        if (interaction.RequireUnlogged && quest.Logged)
            return false;
        return ((interaction.RequiredVisitedLevel1 == 0
                    && interaction.RequiredVisitedLevel2 == 0)
                || (interaction.RequiredVisitedLevel1 != 0
                    && player.HasVisitedLevel(interaction.RequiredVisitedLevel1))
                || (interaction.RequiredVisitedLevel2 != 0
                    && player.HasVisitedLevel(interaction.RequiredVisitedLevel2)))
            && (interaction.RequiredVariable1 < 0
                || quest.Variable1 == interaction.RequiredVariable1)
            && (interaction.RequiredVariable2 < 0
                || quest.Variable2 == interaction.RequiredVariable2);
    }

    private bool TryFindPlayerItem(
        AuthoritativePlayerState player,
        AuthoritativeItemDefinition itemDefinition,
        bool consume,
        out bool fromBelt)
    {
        for (var index = 0; index < player.Inventory.Count; index++) {
            if (!items!.IsStateForDefinition(player.Inventory[index].State, itemDefinition))
                continue;
            fromBelt = false;
            if (consume) {
                InventoryGridLayout.RemoveAndCompact(player.InventoryGrid, index);
                player.Inventory.RemoveAt(index);
            }
            return true;
        }
        for (var index = 0; index < player.Belt.Count; index++) {
            if (!items!.IsStateForDefinition(player.Belt[index].State, itemDefinition))
                continue;
            fromBelt = true;
            if (consume)
                player.Belt.RemoveAt(index);
            return true;
        }
        fromBelt = false;
        return false;
    }

    /** Applies a content-defined native towner turn-in, including item consumption and reward spawn. */
    private DiabloWorldIntentResult TurnInQuest(
        AuthoritativePlayerState player,
        uint questId,
        uint townerId,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        if (!participants.Contains(player))
            participants = participants.Append(player).ToArray();
        var authoredInteraction = TryQuestTownerInteraction(
            player,
            questId,
            townerId,
            participants,
            out var hasActiveInteraction);
        if (hasActiveInteraction)
            return authoredInteraction
                ?? DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);

        var completionStage = world.Quests.TryGetValue(questId, out var candidateQuest)
            && candidateQuest.TurnInOnCompletion
            && candidateQuest.Completed
            && ((candidateQuest.TurnInCompletionVariable2 >= 0
                    && candidateQuest.Variable2 < candidateQuest.TurnInCompletionVariable2)
                || (candidateQuest.TurnInCompletionVariable2 < 0
                    && candidateQuest.TurnInCompletionRequiredVariable1 >= 0
                    && candidateQuest.TurnInCompletionVariable1 >= 0
                    && candidateQuest.Variable1 == candidateQuest.TurnInCompletionRequiredVariable1));
        if (items is null
            || !world.Quests.TryGetValue(questId, out var quest)
            || quest.TurnInTownerId < 0
            || quest.TurnInTownerId != townerId
            || quest.Unavailable
            || (!completionStage
                && (quest.Completed
                    || (!quest.TurnInActivationOnly && !quest.Active)
                    || (quest.TurnInActivationOnly && quest.Active)))
            || (completionStage && !quest.Completed)
            || (completionStage
                && quest.TurnInCompletionRequiredVariable1 >= 0
                && quest.Variable1 != quest.TurnInCompletionRequiredVariable1)
            || (!completionStage
                && quest.TurnInProgressOnly
                && (quest.TurnInRequiredVariable1 < 0
                    || quest.TurnInVariable1 < 0
                    || quest.Variable1 != quest.TurnInRequiredVariable1))
            || (quest.TurnInBlockedVisitedLevel != 0
                && player.HasVisitedLevel(quest.TurnInBlockedVisitedLevel))
            || quest.TurnInLevelId != player.LevelId
            || DeterministicGridMetrics.ChebyshevDistance(
                quest.TurnInPositionX,
                quest.TurnInPositionY,
                player.PositionX,
                player.PositionY) > 1) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        if (completionStage) {
            if ((quest.TurnInCompletionRewardUniqueId != 0
                    || quest.TurnInCompletionRewardItemId != 0)
                && !CanPlaceWorldItemReward(
                    quest.TurnInLevelId,
                    quest.TurnInCompletionRewardPositionX,
                    quest.TurnInCompletionRewardPositionY,
                    participants,
                    player.EntityId,
                    requireWalkable: false)) {
                return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }
            if (quest.TurnInCompletionVariable2 >= 0)
                quest.Variable2 = Math.Max(
                    quest.Variable2,
                    quest.TurnInCompletionVariable2);
            if (quest.TurnInCompletionVariable1 >= 0)
                quest.Variable1 = Math.Max(
                    quest.Variable1,
                    quest.TurnInCompletionVariable1);
            if (quest.CompletionActivateQuestId != 0
                && world.Quests.TryGetValue(quest.CompletionActivateQuestId, out var followOnQuest)
                && !followOnQuest.Unavailable
                && !followOnQuest.Completed) {
                followOnQuest.Active = true;
                followOnQuest.Logged = true;
            }
            if (quest.TurnInCompletionRewardUniqueId != 0) {
                var itemSeed = AllocateGoldStackSeed();
                var rewardState = items.GenerateUnique(
                    quest.TurnInCompletionRewardUniqueId,
                    itemSeed,
                    quest.TurnInCompletionRewardItemLevel);
                var entityId = AllocateWorldItemEntityId(participants);
                world.SetWorldItem(new AuthoritativeWorldItem(
                    entityId,
                    quest.TurnInLevelId,
                    quest.TurnInCompletionRewardPositionX,
                    quest.TurnInCompletionRewardPositionY,
                    itemSeed,
                    0,
                    rewardState));
            }
            if (quest.TurnInCompletionRewardItemId != 0) {
                var itemSeed = AllocateGoldStackSeed();
                var rewardState = items.Generate(
                    quest.TurnInCompletionRewardItemId,
                    itemSeed,
                    quest.TurnInCompletionRewardItemLevel);
                var entityId = AllocateWorldItemEntityId(participants);
                world.SetWorldItem(new AuthoritativeWorldItem(
                    entityId,
                    quest.TurnInLevelId,
                    quest.TurnInCompletionRewardPositionX,
                    quest.TurnInCompletionRewardPositionY,
                    itemSeed,
                    checked((uint)Math.Max(0, rewardState.IdentifiedValue)),
                    rewardState));
            }
            return quest.TurnInCompletionMessageId == 0
                ? DiabloWorldIntentResult.Accepted
                : DiabloWorldIntentResult.AcceptedWith(
                    new DiabloGameplayEvent(
                        DiabloGameplayEventKind.Dialogue,
                        player.EntityId,
                        townerId,
                        0) {
                        MessageId = quest.TurnInCompletionMessageId,
                        QuestId = quest.QuestId,
                    });
        }

        if (quest.TurnInDialogueOnly) {
            if (quest.MessageId == quest.TurnInMessageId
                || !items.TryGet(quest.TurnInItemId, out var dialogueItem)
                || !player.Inventory.Any(item =>
                    items.IsStateForDefinition(item.State, dialogueItem))) {
                return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }
            quest.Logged = true;
            quest.MessageId = quest.TurnInMessageId;
            return DiabloWorldIntentResult.AcceptedWith(
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Dialogue,
                    player.EntityId,
                    townerId,
                    0) {
                    MessageId = quest.TurnInMessageId,
                    QuestId = quest.QuestId,
                });
        }

        if (quest.TurnInActivationOnly) {
            var activationRewardRequired = quest.TurnInRewardItemId != 0
                && items.TryGet(quest.TurnInRewardItemId, out var activationRewardItem)
                && !player.Inventory.Any(item =>
                    items.IsStateForDefinition(item.State, activationRewardItem));
            if (activationRewardRequired
                && !CanPlaceWorldItemReward(
                    quest.TurnInLevelId,
                    quest.TurnInRewardPositionX,
                    quest.TurnInRewardPositionY,
                    participants,
                    player.EntityId,
                    requireWalkable: false)) {
                return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }
            if (quest.TurnInItemId != 0) {
                if (!items.TryGet(quest.TurnInItemId, out var requiredActivationItem))
                    return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
                var activationInventoryIndex = -1;
                for (var index = 0; index < player.Inventory.Count; index++) {
                    if (items.IsStateForDefinition(player.Inventory[index].State, requiredActivationItem)) {
                        activationInventoryIndex = index;
                        break;
                    }
                }
                if (activationInventoryIndex < 0)
                    return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
                InventoryGridLayout.RemoveAndCompact(
                    player.InventoryGrid,
                    activationInventoryIndex);
                player.Inventory.RemoveAt(activationInventoryIndex);
            }
            quest.Active = true;
            quest.Logged = true;
            var activationVariable1 = quest.TurnInActivationVariable1 >= 0
                ? quest.TurnInActivationVariable1
                : quest.CompletionVariable1;
            quest.Variable1 = Math.Max(quest.Variable1, activationVariable1);
            if (quest.TurnInRewardItemId != 0
                && items.TryGet(quest.TurnInRewardItemId, out var activationItem)
                && !player.Inventory.Any(item =>
                    items.IsStateForDefinition(item.State, activationItem))) {
                var itemSeed = AllocateGoldStackSeed();
                var rewardState = items.Generate(
                    quest.TurnInRewardItemId,
                    itemSeed,
                    quest.TurnInRewardItemLevel);
                var entityId = AllocateWorldItemEntityId(participants);
                world.SetWorldItem(new AuthoritativeWorldItem(
                    entityId,
                    quest.TurnInLevelId,
                    quest.TurnInRewardPositionX,
                    quest.TurnInRewardPositionY,
                    itemSeed,
                    checked((uint)Math.Max(0, rewardState.IdentifiedValue)),
                    rewardState));
            }
            var activationMessageId = quest.TurnInMessageId != 0
                ? quest.TurnInMessageId
                : quest.MessageId;
            return activationMessageId == 0
                ? DiabloWorldIntentResult.Accepted
                : DiabloWorldIntentResult.AcceptedWith(
                    new DiabloGameplayEvent(
                        DiabloGameplayEventKind.Dialogue,
                        player.EntityId,
                        townerId,
                        0) {
                        MessageId = activationMessageId,
                        QuestId = quest.QuestId,
                    });
        }

        if (quest.TurnInRequiredVariable2 >= 0
            && quest.Variable2 != quest.TurnInRequiredVariable2) {
            if (quest.Variable2 > quest.TurnInRequiredVariable2)
                return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            quest.Variable2 = quest.TurnInRequiredVariable2;
            quest.Logged = true;
            return quest.MessageId == 0
                ? DiabloWorldIntentResult.Accepted
                : DiabloWorldIntentResult.AcceptedWith(
                    new DiabloGameplayEvent(
                        DiabloGameplayEventKind.Dialogue,
                        player.EntityId,
                        townerId,
                        0) {
                        MessageId = quest.MessageId,
                        QuestId = quest.QuestId,
                    });
        }

        if (quest.TurnInProgressOnly
            && quest.Variable1 != quest.TurnInRequiredVariable1) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        if (quest.TurnInItemId == 0
            || !items.TryGet(quest.TurnInItemId, out var requiredItem)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        if (quest.TurnInAlternateItemId != 0
            && items.TryGet(quest.TurnInAlternateItemId, out var alternateItem)) {
            var alternateInventoryIndex = -1;
            for (var index = 0; index < player.Inventory.Count; index++) {
                if (items.IsStateForDefinition(player.Inventory[index].State, alternateItem)) {
                    alternateInventoryIndex = index;
                    break;
                }
            }
            if (alternateInventoryIndex >= 0) {
                InventoryGridLayout.RemoveAndCompact(
                    player.InventoryGrid,
                    alternateInventoryIndex);
                player.Inventory.RemoveAt(alternateInventoryIndex);
                return quest.TurnInAlternateMessageId == 0
                    ? DiabloWorldIntentResult.Accepted
                    : DiabloWorldIntentResult.AcceptedWith(
                        new DiabloGameplayEvent(
                            DiabloGameplayEventKind.Dialogue,
                            player.EntityId,
                            townerId,
                            0) {
                            MessageId = quest.TurnInAlternateMessageId,
                            QuestId = quest.QuestId,
                        });
            }
        }

        var inventoryIndex = -1;
        for (var index = 0; index < player.Inventory.Count; index++) {
            if (items.IsStateForDefinition(player.Inventory[index].State, requiredItem)) {
                inventoryIndex = index;
                break;
            }
        }
        if (inventoryIndex < 0)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);

        if ((quest.TurnInRewardUniqueId != 0 || quest.TurnInRewardItemId != 0)
            && !CanPlaceWorldItemReward(
                quest.TurnInLevelId,
                quest.TurnInRewardPositionX,
                quest.TurnInRewardPositionY,
                participants,
                player.EntityId,
                requireWalkable: false)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        InventoryGridLayout.RemoveAndCompact(player.InventoryGrid, inventoryIndex);
        player.Inventory.RemoveAt(inventoryIndex);
        quest.Logged = true;
        if (quest.TurnInProgressOnly) {
            quest.Variable1 = Math.Max(quest.Variable1, quest.TurnInVariable1);
        } else {
            quest.Progress = quest.RequiredProgress;
            quest.Completed = true;
            quest.Active = false;
            quest.Variable1 = Math.Max(quest.Variable1, quest.CompletionVariable1);
            quest.Variable2 = Math.Max(quest.Variable2, quest.CompletionVariable2);

            if (quest.CompletionActivateQuestId != 0
                && world.Quests.TryGetValue(quest.CompletionActivateQuestId, out var nextQuest)
                && !nextQuest.Unavailable
                && !nextQuest.Completed) {
                nextQuest.Active = true;
            }
        }

        if (quest.TurnInRewardUniqueId != 0) {
            var itemSeed = AllocateGoldStackSeed();
            var rewardState = items.GenerateUnique(
                quest.TurnInRewardUniqueId,
                itemSeed,
                quest.TurnInRewardItemLevel);
            var entityId = AllocateWorldItemEntityId(participants);
            world.SetWorldItem(new AuthoritativeWorldItem(
                entityId,
                quest.TurnInLevelId,
                quest.TurnInRewardPositionX,
                quest.TurnInRewardPositionY,
                itemSeed,
                0,
                rewardState));
        }
        if (quest.TurnInRewardItemId != 0) {
            var itemSeed = AllocateGoldStackSeed();
            var rewardState = items.Generate(
                quest.TurnInRewardItemId,
                itemSeed,
                quest.TurnInRewardItemLevel);
            var entityId = AllocateWorldItemEntityId(participants);
            world.SetWorldItem(new AuthoritativeWorldItem(
                entityId,
                quest.TurnInLevelId,
                quest.TurnInRewardPositionX,
                quest.TurnInRewardPositionY,
                itemSeed,
                checked((uint)Math.Max(0, rewardState.IdentifiedValue)),
                rewardState));
        }

        return quest.TurnInMessageId == 0
            ? DiabloWorldIntentResult.Accepted
            : DiabloWorldIntentResult.AcceptedWith(
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Dialogue,
                    player.EntityId,
                    townerId,
                    0) {
                    MessageId = quest.TurnInMessageId,
                    QuestId = quest.QuestId,
                });
    }

}

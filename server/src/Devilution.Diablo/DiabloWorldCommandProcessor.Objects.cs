using Devilution.Domain.Inventory;
using Devilution.Engine;

namespace Devilution.Diablo;

public sealed partial class DiabloWorldCommandProcessor
{
    private DiabloWorldIntentResult OperateObject(
        AuthoritativePlayerState player,
        uint objectEntityId,
        IReadOnlyList<AuthoritativePlayerState> participants,
        bool requireAdjacent = true,
        bool applyEffects = true)
    {
        if (!participants.Contains(player))
            participants = participants.Append(player).ToArray();
        if (!world.Objects.TryGetValue(objectEntityId, out var @object))
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        var nativeOperation = world.OwnsNativePopulationLevel(@object.LevelId)
            && world.CanOperateNativeObject(@object);
        if (@object.Activated
            || !@object.HasAuthoritativeInteraction && !nativeOperation
            || @object.LevelId != player.LevelId
            || (requireAdjacent
                && DeterministicGridMetrics.ChebyshevDistance(
                    @object.PositionX,
                    @object.PositionY,
                    player.PositionX,
                    player.PositionY) > 1)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        if (@object.QuestId != 0
            && world.Quests.TryGetValue(@object.QuestId, out var linkedQuest)
            && linkedQuest.Unavailable) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        if (!world.CanOperateNativeObject(@object))
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        if (TryOperateNativeObject(player, @object, participants, applyEffects, out var nativeResult))
            return nativeResult;
        if (@object.CathedralDoor is not null)
            return !applyEffects || world.TryOperateCathedralDoor(objectEntityId, participants)
                ? DiabloWorldIntentResult.Accepted
                : DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        if (@object.QuestTransitionKind != AuthoritativeObjectQuestTransitionKind.None
            && (!world.Quests.TryGetValue(@object.QuestId, out var transitionQuest)
                || transitionQuest.Unavailable
                || !IsQuestAtPlayerLevel(transitionQuest, player.LevelId, @object.LevelId)
                || (@object.QuestTransitionRequiredVariable1 >= 0
                    && transitionQuest.Variable1 != @object.QuestTransitionRequiredVariable1)
                || (@object.QuestTransitionRequiredVariable2 >= 0
                    && transitionQuest.Variable2 != @object.QuestTransitionRequiredVariable2))) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        if (@object.EffectKind == AuthoritativeObjectEffectKind.WorldMutation
            && (@object.EffectAmount <= 0
                || world.WorldMutations?.TryGet((uint)@object.EffectAmount, out _) != true)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        if (!CanApplyObjectReward(@object, player, participants))
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        if (!TryFindObjectItemGateIndex(@object, player, out var gateIndex))
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        if (!CanApplyObjectSpellEffect(@object, player, participants))
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        if (!applyEffects)
            return DiabloWorldIntentResult.Accepted;

        if (gateIndex >= 0
            && objectItemGates!.TryGet(@object.EntityId, out var itemGate)
            && itemGate.ConsumeItem) {
            InventoryGridLayout.RemoveAndCompact(player.InventoryGrid, gateIndex);
            player.Inventory.RemoveAt(gateIndex);
        }

        if (@object.EffectKind == AuthoritativeObjectEffectKind.QuestSequence)
            return ApplyQuestSequence(player, @object, participants);
        if (@object.EffectKind == AuthoritativeObjectEffectKind.QuestItemCompletion)
            return ApplyQuestItemCompletion(player, @object);

        @object.Activated = true;
        @object.AnimationFrame = @object.ActivatedAnimationFrame;
        if (@object.QuestId != 0 && world.Quests.TryGetValue(@object.QuestId, out var quest)
            && @object.QuestTransitionKind == AuthoritativeObjectQuestTransitionKind.None
            && !quest.Unavailable
            && !quest.Completed
            && IsQuestAtPlayerLevel(quest, player.LevelId, @object.LevelId)) {
            quest.Active = true;
            quest.Logged = true;
            quest.Progress = Math.Min(
                quest.RequiredProgress,
                checked(quest.Progress + @object.QuestProgressAmount));
            if (quest.Progress >= quest.RequiredProgress) {
                quest.Completed = true;
                quest.Active = false;
            }
        }

        ApplyQuestTransition(@object);
        if (@object.DialogueMessageId != 0
            && @object.QuestId != 0
            && world.Quests.TryGetValue(@object.QuestId, out var dialogueQuest)) {
            dialogueQuest.MessageId = @object.DialogueMessageId;
        }
        if (@object.EffectKind == AuthoritativeObjectEffectKind.WorldMutation) {
            world.ApplyWorldMutation((uint)@object.EffectAmount);
            ApplyAuthoredObjectFollowUp(@object, player, participants);
        } else if (@object.QuestTransitionKind != AuthoritativeObjectQuestTransitionKind.None
            && (@object.QuestSequenceFollowUpKind != AuthoritativeQuestSequenceFollowUpKind.None
                || @object.QuestSequenceFollowUpWorldMutationId != 0)) {
            // Quest-transition objects (books, levers, and similar set-piece
            // controls) may apply their authored map/encounter follow-up only
            // after the transition succeeds.
            ApplyAuthoredObjectFollowUp(@object, player, participants);
        }

        SpawnObjectReward(@object, participants);
        if (@object.EffectKind is AuthoritativeObjectEffectKind.GrantSpellLevel
                or AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile)
            ApplyGrantedSpellLevel(player, checked((uint)@object.EffectAmount));
        if (@object.EffectKind is AuthoritativeObjectEffectKind.SpawnSpellProjectile
                or AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile)
            SpawnObjectSpellProjectile(@object, player);
        if (@object.EffectKind == AuthoritativeObjectEffectKind.WorldMutation
            || @object.EffectAmount <= 0)
            return CreateObjectDialogueResult(@object, player);

        switch (@object.EffectKind) {
        case AuthoritativeObjectEffectKind.Heal:
            var healing = Math.Min(
                @object.EffectAmount,
                Math.Max(0, player.LifeMaximum - player.Life));
            player.Life = checked(player.Life + healing);
            return healing > 0
                ? DiabloWorldIntentResult.AcceptedWith(new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Healing,
                    @object.EntityId,
                    player.EntityId,
                    healing))
                : DiabloWorldIntentResult.Accepted;
        case AuthoritativeObjectEffectKind.Damage:
            var damageAmountQ6 = @object.EffectAmount;
            if (@object.TrapArmed
                && HasUsableEquipmentFlag(
                    player,
                    AuthoritativeEquipmentIncomingDamageRules.HalfTrapDamageFlag)) {
                damageAmountQ6 /= 2;
            }
            var damage = playerDamage?.Apply(player, damageAmountQ6)
                ?? ApplyUnshieldedDamage(player, damageAmountQ6);
            return damage.IncomingDamageQ6 > 0
                ? DiabloWorldIntentResult.AcceptedWith(new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    @object.EntityId,
                    player.EntityId,
                    DeterministicFixedPointQ6.ToWholeTruncated(
                        damage.IncomingDamageQ6)) {
                    AmountQ6 = damage.IncomingDamageQ6,
                })
                : DiabloWorldIntentResult.Accepted;
        case AuthoritativeObjectEffectKind.Experience:
            DiabloPlayerProgression.GrantExperience(
                player,
                checked((uint)@object.EffectAmount),
                experience);
            return DiabloWorldIntentResult.AcceptedWith(new DiabloGameplayEvent(
                DiabloGameplayEventKind.Experience,
                player.EntityId,
                player.EntityId,
                @object.EffectAmount));
        default:
            return CreateObjectDialogueResult(@object, player);
        }
    }

    private bool TryFindObjectItemGateIndex(
        AuthoritativeWorldObject @object,
        AuthoritativePlayerState player,
        out int inventoryIndex)
    {
        inventoryIndex = -1;
        if (objectItemGates is null || !objectItemGates.TryGet(@object.EntityId, out var gate))
            return true;
        if (items is null || !items.TryGet(gate.ItemId, out var itemDefinition))
            return false;
        for (var index = 0; index < player.Inventory.Count; index++) {
            if (!items.IsStateForDefinition(player.Inventory[index].State, itemDefinition))
                continue;
            inventoryIndex = index;
            return true;
        }
        return false;
    }

    private static DiabloWorldIntentResult CreateObjectDialogueResult(
        AuthoritativeWorldObject @object,
        AuthoritativePlayerState player)
    {
        return @object.DialogueMessageId == 0
            ? DiabloWorldIntentResult.Accepted
            : DiabloWorldIntentResult.AcceptedWith(
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Dialogue,
                    player.EntityId,
                    @object.EntityId,
                    0) {
                    MessageId = @object.DialogueMessageId,
                    QuestId = @object.QuestId,
                });
    }

    private void ApplyGrantedSpellLevel(
        AuthoritativePlayerState player,
        uint spellId)
    {
        if (spells is null || !spells.TryGet(spellId, out _))
            throw new InvalidDataException(
                $"Object spell-level grant references unknown spell {spellId}.");
        var index = player.MemorizedSpells.FindIndex(spell => spell.SpellId == spellId);
        var currentLevel = index >= 0 ? player.MemorizedSpells[index].Level : 0;
        var nextLevel = Math.Min(
            (uint)AuthoritativePlayerLoadoutCatalog.MaximumNativeSpellLevel,
            checked(currentLevel + 1));
        if (index >= 0)
            player.MemorizedSpells[index] = new MemorizedSpellState(spellId, nextLevel);
        else
            player.MemorizedSpells.Add(new MemorizedSpellState(spellId, nextLevel));
    }

    private bool CanApplyObjectSpellEffect(
        AuthoritativeWorldObject @object,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        if ((@object.EffectKind is AuthoritativeObjectEffectKind.GrantSpellLevel
                or AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile)
            && (spells?.TryGet(checked((uint)@object.EffectAmount), out _) != true
                || @object.EffectAmount <= 0)) {
            return false;
        }
        if (@object.EffectKind is AuthoritativeObjectEffectKind.SpawnSpellProjectile
                or AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile)
            return CanSpawnObjectSpellProjectile(
                @object,
                checked((uint)@object.EffectAmount),
                @object.ProjectileTargetOffsetX,
                @object.ProjectileTargetOffsetY);
        if (@object.QuestSequenceFollowUpKind
            == AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster) {
            return CanSpawnNativeMonster(@object, participants);
        }
        if (@object.QuestSequenceFollowUpKind
            != AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile) {
            return true;
        }
        return CanSpawnObjectSpellProjectile(
            @object,
            @object.QuestSequenceFollowUpAmount,
            @object.QuestSequenceFollowUpOffsetX,
            @object.QuestSequenceFollowUpOffsetY);
    }

    private bool CanSpawnNativeMonster(
        AuthoritativeWorldObject @object,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        if (monsterSpawnPlanner is null
            || monsterTypes is null
            || @object.QuestSequenceFollowUpAmount == 0
            || !monsterTypes.TryGet(
                @object.QuestSequenceFollowUpAmount,
                out _)) {
            return false;
        }
        var targetX = checked(@object.PositionX + @object.QuestSequenceFollowUpOffsetX);
        var targetY = checked(@object.PositionY + @object.QuestSequenceFollowUpOffsetY);
        return world.ContainsPosition(@object.LevelId, targetX, targetY)
            && world.IsWalkable(@object.LevelId, targetX, targetY)
            && !world.IsOccupied(
                @object.LevelId,
                targetX,
                targetY,
                participants,
                0);
    }

    private void SpawnNativeMonster(
        AuthoritativeWorldObject @object,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        if (!CanSpawnNativeMonster(@object, participants))
            throw new InvalidDataException(
                $"Object {@object.EntityId} cannot materialize monster {@object.QuestSequenceFollowUpAmount}.");

        var targetX = checked(@object.PositionX + @object.QuestSequenceFollowUpOffsetX);
        var targetY = checked(@object.PositionY + @object.QuestSequenceFollowUpOffsetY);
        var entityId = AllocateGeneratedMonsterEntityId();
        var target = monsterSpawnPlanner!.CreateStableMonsterSummon(
            @object.LevelId,
            @object.QuestSequenceFollowUpAmount,
            random.NextUInt32(),
            targetX,
            targetY,
            entityId,
            gameMode == DiabloGameMode.Multiplayer,
            ignoreDungeonLevelRange: true);
        if (target is null)
            throw new InvalidDataException(
                $"Object {@object.EntityId} references monster {@object.QuestSequenceFollowUpAmount} that is unavailable on level {@object.LevelId}.");
        DiabloDifficultyMonsterScaling.ApplyInitialHealth(
            target,
            difficulty,
            gameVariant,
            gameMode,
            combatRules.DifficultyRules);
        DiabloDifficultyMonsterScaling.ApplyInitialResistances(
            target,
            difficulty,
            monsterTypes);
        world.CombatTargets.Add(target.EntityId, target);
    }

    private uint AllocateGeneratedMonsterEntityId()
    {
        return generatedMonsterEntityIds.Allocate(world.IsEntityIdOccupied);
    }

    private bool CanSpawnObjectSpellProjectile(
        AuthoritativeWorldObject @object,
        uint spellId,
        int offsetX,
        int offsetY)
    {
        if (spellId == 0
            || spells?.TryGet(spellId, out var spell) != true
            || !AuthoritativeWorldObjectCatalog.IsSupportedSpellProjectile(spell)) {
            return false;
        }
        if (@object.ProjectileSourceKind
                == AuthoritativeObjectProjectileSourceKind.ActivatingPlayer
            && spell.ProjectileBehavior is not (
                AuthoritativeProjectileBehavior.Phasing
                or AuthoritativeProjectileBehavior.GuardianController)) {
            return false;
        }
        var targetX = checked(@object.PositionX + offsetX);
        var targetY = checked(@object.PositionY + offsetY);
        return world.ContainsPosition(@object.LevelId, targetX, targetY);
    }

    /** Accepts ordinary quest levels and the native set-level route identity. */
    private bool IsQuestAtPlayerLevel(
        AuthoritativeQuestState quest,
        uint playerLevelId,
        uint objectLevelId = 0)
    {
        if (quest.LevelId == 0
            || quest.LevelId == playerLevelId
            || objectLevelId != 0 && objectLevelId == playerLevelId)
            return true;
        if (questTypes?.TryGet(quest.QuestId, out var definition) != true
            || definition.SetLevel == 0) {
            return false;
        }
        return AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(
            definition.SetLevel,
            activeMap: true) == playerLevelId;
    }

    private void SpawnObjectSpellProjectile(
        AuthoritativeWorldObject @object,
        AuthoritativePlayerState player)
    {
        var directProjectile = @object.EffectKind is
            AuthoritativeObjectEffectKind.SpawnSpellProjectile
            or AuthoritativeObjectEffectKind.GrantSpellLevelAndSpawnProjectile;
        var spellId = directProjectile
            ? checked((uint)@object.EffectAmount)
            : @object.QuestSequenceFollowUpAmount;
        if (spells is null || !spells.TryGet(spellId, out var spell)) {
            throw new InvalidDataException(
                $"Object spell projectile references unknown spell {spellId}.");
        }

        var projectileEntityId = AllocateAuthoredProjectileEntityId();
        var sourceEntityId = player.EntityId == 0 ? 1U : player.EntityId;
        var offsetX = directProjectile
            ? @object.ProjectileTargetOffsetX
            : @object.QuestSequenceFollowUpOffsetX;
        var offsetY = directProjectile
            ? @object.ProjectileTargetOffsetY
            : @object.QuestSequenceFollowUpOffsetY;
        var targetX = checked(@object.PositionX + offsetX);
        var targetY = checked(@object.PositionY + offsetY);
        var sourceX = @object.ProjectileSourceKind
            == AuthoritativeObjectProjectileSourceKind.ActivatingPlayer
            ? player.PositionX
            : targetX;
        var sourceY = @object.ProjectileSourceKind
            == AuthoritativeObjectProjectileSourceKind.ActivatingPlayer
            ? player.PositionY
            : targetY;
        var effectiveSpellLevel = player.MemorizedSpells
            .FirstOrDefault(memorized => memorized.SpellId == spell.SpellId)
            ?.Level
            ?? 0;
        if (spell.ProjectileBehavior == AuthoritativeProjectileBehavior.Phasing) {
            world.Projectiles.Add(
                projectileEntityId,
                new AuthoritativeProjectile(
                    projectileEntityId,
                    sourceEntityId,
                    0,
                    spell.SpellId,
                    player.LevelId,
                    sourceX,
                    sourceY,
                    targetX,
                    targetY,
                    0,
                    spell.DamageType,
                    0,
                    checked((uint)spell.ProjectileLifetime)) {
                    Behavior = AuthoritativeProjectileBehavior.Phasing,
                });
            return;
        }

        var durationUnits = Math.Min(
            spell.GuardianMaximumDuration,
            checked((int)effectiveSpellLevel)
                + checked((int)player.CharacterLevel
                    / Math.Max(1, spell.GuardianCharacterLevelDurationDivisor)));
        var duration = Math.Max(
            spell.GuardianMinimumDuration,
            checked(durationUnits * spell.GuardianDurationTickScale));
        world.Projectiles.Add(
            projectileEntityId,
            new AuthoritativeProjectile(
                projectileEntityId,
                sourceEntityId,
                0,
                spell.SpellId,
                player.LevelId,
                sourceX,
                sourceY,
                targetX,
                targetY,
                0,
                spell.DamageType,
                0,
                checked((uint)duration)) {
                Behavior = AuthoritativeProjectileBehavior.GuardianController,
                OriginalDirection = (int)player.FacingDirection,
                EffectiveSpellLevel = checked((int)effectiveSpellLevel),
            });
    }

    private uint AllocateAuthoredProjectileEntityId()
    {
        return authoredProjectileEntityIds.Allocate(world.IsEntityIdOccupied);
    }

    private void ApplyQuestTransition(AuthoritativeWorldObject @object)
    {
        if (@object.QuestTransitionKind == AuthoritativeObjectQuestTransitionKind.None
            || !world.Quests.TryGetValue(@object.QuestId, out var quest)) {
            return;
        }

        var kind = @object.QuestTransitionKind;
        if (kind is AuthoritativeObjectQuestTransitionKind.ActivateQuest
                or AuthoritativeObjectQuestTransitionKind.ActivateAndSetVariable1
                or AuthoritativeObjectQuestTransitionKind.ActivateAndSetVariable2) {
            quest.Active = true;
            quest.Logged = true;
        }
        if (kind is AuthoritativeObjectQuestTransitionKind.CompleteQuest
                or AuthoritativeObjectQuestTransitionKind.CompleteAndSetVariable1
                or AuthoritativeObjectQuestTransitionKind.CompleteAndSetVariable2) {
            quest.Completed = true;
            quest.Active = false;
            quest.Progress = quest.RequiredProgress;
            quest.Logged = true;
        }
        if (kind is AuthoritativeObjectQuestTransitionKind.SetVariable1
                or AuthoritativeObjectQuestTransitionKind.ActivateAndSetVariable1
                or AuthoritativeObjectQuestTransitionKind.CompleteAndSetVariable1) {
            quest.Variable1 = @object.QuestTransitionAmount;
        }
        if (kind is AuthoritativeObjectQuestTransitionKind.SetVariable2
                or AuthoritativeObjectQuestTransitionKind.ActivateAndSetVariable2
                or AuthoritativeObjectQuestTransitionKind.CompleteAndSetVariable2) {
            quest.Variable2 = @object.QuestTransitionAmount;
        }
    }

    private bool CanApplyObjectReward(
        AuthoritativeWorldObject @object,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        if (@object.RewardKind == AuthoritativeObjectRewardKind.None)
            return true;
        if (items is null
            || !AuthoritativeWorldObject.IsValidReward(
                @object.RewardKind,
                @object.RewardId,
                @object.RewardItemLevel,
                @object.RewardOffsetX,
                @object.RewardOffsetY,
                @object.RewardOriginKind,
                @object.RewardSetLevelId)) {
            return false;
        }
        if (@object.RewardKind == AuthoritativeObjectRewardKind.NormalItem
            && !items.TryGet(@object.RewardId, out _)) {
            return false;
        }
        if (@object.RewardKind == AuthoritativeObjectRewardKind.UniqueItem
            && (!items.TryGetUnique(@object.RewardId, out var unique)
                || @object.RewardItemLevel < unique.MinimumLevel)) {
            return false;
        }
        if (!TryResolveObjectRewardPosition(@object, out var rewardLevelId, out var rewardX, out var rewardY)
            || rewardLevelId != @object.LevelId) {
            return false;
        }
        return world.IsWalkable(rewardLevelId, rewardX, rewardY)
            && !world.IsOccupied(
                rewardLevelId,
                rewardX,
                rewardY,
                participants,
                excludedEntityId: @object.EntityId)
            && !world.WorldItems.Values.Any(item =>
                item.LevelId == rewardLevelId
                && item.PositionX == rewardX
                && item.PositionY == rewardY);
    }

    private void SpawnObjectReward(
        AuthoritativeWorldObject @object,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        if (@object.RewardKind == AuthoritativeObjectRewardKind.None)
            return;
        var itemSeed = AllocateGoldStackSeed();
        var rewardState = @object.RewardKind == AuthoritativeObjectRewardKind.UniqueItem
            ? items!.GenerateUnique(@object.RewardId, itemSeed, @object.RewardItemLevel)
            : items!.Generate(@object.RewardId, itemSeed, @object.RewardItemLevel);
        if (!TryResolveObjectRewardPosition(@object, out var rewardLevelId, out var rewardX, out var rewardY))
            throw new InvalidDataException("Object reward origin is unavailable.");
        var entityId = AllocateWorldItemEntityId(participants);
        world.SetWorldItem(new AuthoritativeWorldItem(
            entityId,
            rewardLevelId,
            rewardX,
            rewardY,
            itemSeed,
            checked((uint)Math.Max(0, rewardState.IdentifiedValue)),
            rewardState));
    }

    private bool TryResolveObjectRewardPosition(
        AuthoritativeWorldObject @object,
        out uint levelId,
        out int rewardX,
        out int rewardY)
    {
        if (@object.RewardOriginKind == AuthoritativeObjectRewardOriginKind.Object) {
            levelId = @object.LevelId;
            rewardX = checked(@object.PositionX + @object.RewardOffsetX);
            rewardY = checked(@object.PositionY + @object.RewardOffsetY);
            return true;
        }
        if (setLevelOriginResolver is null
            || setLevelOriginResolver(@object.RewardSetLevelId) is not { } origin) {
            levelId = 0;
            rewardX = 0;
            rewardY = 0;
            return false;
        }
        levelId = origin.LevelId;
        rewardX = checked(origin.PositionX + @object.RewardOffsetX);
        rewardY = checked(origin.PositionY + @object.RewardOffsetY);
        return true;
    }

    /** Consumes a content-selected inventory item at a quest interaction boundary. */
    private DiabloWorldIntentResult ApplyQuestItemCompletion(
        AuthoritativePlayerState player,
        AuthoritativeWorldObject @object)
    {
        if (items is null
            || !world.Quests.TryGetValue(@object.QuestId, out var quest)
            || quest.Unavailable
            || quest.Completed
            || !items.TryGet((uint)@object.EffectAmount, out var itemDefinition)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        var inventoryIndex = -1;
        for (var index = 0; index < player.Inventory.Count; index++) {
            if (items.IsStateForDefinition(
                    player.Inventory[index].State,
                    itemDefinition)) {
                inventoryIndex = index;
                break;
            }
        }
        if (inventoryIndex < 0)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);

        InventoryGridLayout.RemoveAndCompact(player.InventoryGrid, inventoryIndex);
        player.Inventory.RemoveAt(inventoryIndex);
        @object.Activated = true;
        @object.AnimationFrame = @object.ActivatedAnimationFrame;
        quest.Progress = quest.RequiredProgress;
        quest.Completed = true;
        quest.Active = false;
        quest.Logged = true;
        quest.Variable1 = Math.Max(quest.Variable1, quest.CompletionVariable1);
        quest.Variable2 = Math.Max(quest.Variable2, quest.CompletionVariable2);
        if (quest.CompletionActivateQuestId != 0
            && world.Quests.TryGetValue(
                quest.CompletionActivateQuestId,
                out var nextQuest)
            && !nextQuest.Unavailable
            && !nextQuest.Completed) {
            nextQuest.Active = true;
        }
        return DiabloWorldIntentResult.Accepted;
    }

    /** Applies one ordered quest interaction step, preserving native reset semantics. */
    private DiabloWorldIntentResult ApplyQuestSequence(
        AuthoritativePlayerState player,
        AuthoritativeWorldObject @object,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        if (!world.Quests.TryGetValue(@object.QuestId, out var quest)
            || quest.Unavailable
            || quest.Completed
            || quest.LevelId != player.LevelId) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        quest.Active = true;
        quest.Logged = true;
        var expectedStep = checked(quest.Variable1 + 1);
        if (@object.EffectAmount != expectedStep) {
            quest.Variable1 = 0;
            return DiabloWorldIntentResult.Accepted;
        }

        quest.Variable1 = @object.EffectAmount;
        if (quest.CompletionVariable1 > 0
            && quest.Variable1 >= quest.CompletionVariable1) {
            quest.Progress = quest.RequiredProgress;
            quest.Completed = true;
            quest.Active = false;
            quest.Variable1 = Math.Max(quest.Variable1, quest.CompletionVariable1);
            quest.Variable2 = Math.Max(quest.Variable2, quest.CompletionVariable2);
            if (quest.CompletionActivateQuestId != 0
                && world.Quests.TryGetValue(
                    quest.CompletionActivateQuestId,
                    out var nextQuest)
                && !nextQuest.Unavailable
                && !nextQuest.Completed) {
                nextQuest.Active = true;
            }
        }
        ApplyAuthoredObjectFollowUp(@object, player, participants);
        return quest.MessageId == 0
            ? DiabloWorldIntentResult.Accepted
            : DiabloWorldIntentResult.AcceptedWith(
                new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Dialogue,
                    player.EntityId,
                    @object.EntityId,
                    0) {
                    MessageId = quest.MessageId,
                    QuestId = quest.QuestId,
                });
    }

    /** Applies a validated follow-up after a quest step or direct world mutation. */
    private void ApplyAuthoredObjectFollowUp(
        AuthoritativeWorldObject @object,
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        switch (@object.QuestSequenceFollowUpKind) {
        case AuthoritativeQuestSequenceFollowUpKind.None:
            break;
        case AuthoritativeQuestSequenceFollowUpKind.WorldMutation:
            if (!world.ApplyWorldMutation(@object.QuestSequenceFollowUpAmount))
                throw new InvalidDataException(
                    $"Object authored follow-up references unknown world mutation {@object.QuestSequenceFollowUpAmount}.");
            break;
        case AuthoritativeQuestSequenceFollowUpKind.SpawnSpellProjectile:
            SpawnObjectSpellProjectile(@object, player);
            break;
        case AuthoritativeQuestSequenceFollowUpKind.SpawnNativeMonster:
            SpawnNativeMonster(@object, player, participants);
            break;
        default:
            throw new InvalidDataException(
                $"Object authored follow-up kind '{@object.QuestSequenceFollowUpKind}' is unsupported.");
        }
        if (@object.QuestSequenceFollowUpWorldMutationId != 0
            && !world.ApplyWorldMutation(@object.QuestSequenceFollowUpWorldMutationId)) {
            throw new InvalidDataException(
                $"Object authored follow-up references unknown world mutation {@object.QuestSequenceFollowUpWorldMutationId}.");
        }
    }

    private DiabloWorldIntentResult DisarmTrap(
        AuthoritativePlayerState player,
        uint objectEntityId,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        if (!player.HasAbility(
                DiabloPlayerAbilityKind.TrapDisarm,
                trapDisarmAbilitySpellId)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }
        if (!world.Objects.TryGetValue(objectEntityId, out var @object)
            || @object.LevelId != player.LevelId
            || DeterministicGridMetrics.ChebyshevDistance(
                @object.PositionX,
                @object.PositionY,
                player.PositionX,
                player.PositionY) > 1) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        if (world.IsNativeTrapTrigger(@object)) {
            // ACTION_DISARM performs TryDisarm then OperateObject, including a failed roll.
            var admission = OperateObject(player, objectEntityId, participants, applyEffects: false);
            if (!admission.Succeeded)
                return admission;
            if (@object.TrapArmed
                && world.TryDisarmNativeTrapTrigger(player, @object, random)
                    == DiabloNativeTrapDisarmResult.NotApplicable)
                return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
            return OperateObject(player, objectEntityId, participants);
        }
        if (!@object.TrapArmed)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        if (!world.CanOperateNativeObject(@object))
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        @object.TrapArmed = false;
        return DiabloWorldIntentResult.Accepted;
    }

    private static AuthoritativePlayerDamageResult ApplyUnshieldedDamage(
        AuthoritativePlayerState player,
        int incomingDamage)
    {
        var lifeDamage = Math.Min(player.Life, incomingDamage);
        player.Life -= lifeDamage;
        return new AuthoritativePlayerDamageResult(
            incomingDamage,
            lifeDamage,
            0,
            false);
    }

    private static bool HasUsableEquipmentFlag(
        AuthoritativePlayerState player,
        uint flag)
    {
        return player.Equipment.Any(item => {
            var state = item.State;
            return state.StatFlag
                && (state.Magical == 0 || state.Identified)
                && (state.Flags & flag) != 0;
        });
    }

}

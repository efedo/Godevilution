using Devilution.Domain.Inventory;

namespace Devilution.Diablo;

/** Applies non-combat world intents without transport or host dependencies. */
public sealed class DiabloWorldCommandProcessor
{
    private readonly DiabloWorldState world;
    private readonly AuthoritativePlayerAnimationCatalog? playerAnimations;
    private readonly bool useNativePlayerActionTiming;
    private readonly AuthoritativeCombatRules combatRules;
    private readonly DiabloRandom random;
    private readonly AuthoritativePlayerDamageResolver? playerDamage;
    private readonly uint trapDisarmAbilitySpellId;
    private uint nextWorldItemEntityId = 0x40000000;

    public DiabloWorldCommandProcessor(
        DiabloWorldState world,
        AuthoritativePlayerAnimationCatalog? playerAnimations = null,
        bool useNativePlayerActionTiming = false,
        AuthoritativeCombatRules? combatRules = null,
        DiabloRandom? random = null,
        AuthoritativePlayerDamageResolver? playerDamage = null,
        uint trapDisarmAbilitySpellId = 0)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.playerAnimations = playerAnimations;
        this.useNativePlayerActionTiming = useNativePlayerActionTiming;
        this.combatRules = combatRules ?? new AuthoritativeCombatRules(10, 1, 100);
        this.random = random ?? new DiabloRandom(0);
        this.playerDamage = playerDamage;
        this.trapDisarmAbilitySpellId = trapDisarmAbilitySpellId;
        if (useNativePlayerActionTiming && playerAnimations is null) {
            throw new ArgumentException(
                "Native player action timing requires a player animation catalog.",
                nameof(playerAnimations));
        }
    }

    public DiabloWorldIntentResult Execute(
        AuthoritativePlayerState player,
        DiabloWorldIntent intent,
        ulong appliedTick,
        IEnumerable<AuthoritativePlayerState> players)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(players);

        if (intent is DiabloRestartTownIntent)
            return RestartTown(player, appliedTick);

        if (useNativePlayerActionTiming
            && (player.Life <= 0
                || player.CombatMode is DiabloPlayerCombatMode.Death
                    or DiabloPlayerCombatMode.NewLevel
                    or DiabloPlayerCombatMode.Quit)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }

        return intent switch {
            DiabloMoveIntent move => Move(player, move, appliedTick, players),
            DiabloUsePortalIntent portal => UsePortal(
                player,
                portal.PortalId,
                appliedTick,
                players),
            DiabloPickupWorldItemIntent pickup => PickupWorldItem(
                player,
                pickup.ItemEntityId,
                appliedTick),
            DiabloDropHeldItemIntent drop => DropHeldItem(
                player,
                drop.TargetX,
                drop.TargetY,
                players.ToArray()),
            DiabloOperateObjectIntent operation => OperateObject(player, operation.ObjectEntityId),
            DiabloDisarmTrapIntent disarm => DisarmTrap(player, disarm.ObjectEntityId),
            DiabloAdvanceQuestIntent quest => AdvanceQuest(player, quest.QuestId),
            DiabloSetFriendlyModeIntent mode => SetFriendlyMode(player, mode.FriendlyMode),
            _ => DiabloWorldIntentResult.Rejected(DiabloRuleFailure.Malformed),
        };
    }

    /** Applies the native Telekinesis target operation after cast validation. */
    public DiabloWorldIntentResult ExecuteTelekinesis(
        AuthoritativePlayerState player,
        uint targetEntityId,
        int targetX,
        int targetY,
        ulong appliedTick,
        IEnumerable<AuthoritativePlayerState> players,
        bool applyEffects = true)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(players);
        var participants = players.ToArray();
        if (targetEntityId == 0
            || !world.HasLineOfSight(
                player.LevelId,
                player.PositionX,
                player.PositionY,
                targetX,
                targetY)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        if (world.WorldItems.TryGetValue(targetEntityId, out var item)) {
            if (item.LevelId != player.LevelId
                || item.PositionX != targetX
                || item.PositionY != targetY) {
                return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }
            return PickupWorldItem(
                player,
                targetEntityId,
                appliedTick,
                requireAdjacent: false,
                applyEffects: applyEffects);
        }

        if (world.Objects.TryGetValue(targetEntityId, out var @object)) {
            if (@object.LevelId != player.LevelId
                || @object.PositionX != targetX
                || @object.PositionY != targetY) {
                return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }
            return OperateObject(
                player,
                targetEntityId,
                requireAdjacent: false,
                applyEffects: applyEffects);
        }

        if (!world.CombatTargets.TryGetValue(targetEntityId, out var target)
            || target.LevelId != player.LevelId
            || target.HitPoints <= 0
            || target.PositionX != targetX
            || target.PositionY != targetY) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        if (!applyEffects)
            return DiabloWorldIntentResult.Accepted;

        var directionX = Math.Sign(target.PositionX - player.PositionX);
        var directionY = Math.Sign(target.PositionY - player.PositionY);
        if (directionX == 0 && directionY == 0)
            return DiabloWorldIntentResult.Accepted;
        var destinationX = target.PositionX + directionX;
        var destinationY = target.PositionY + directionY;
        if (world.IsWalkable(target.LevelId, destinationX, destinationY)
            && !world.IsOccupied(
                target.LevelId,
                destinationX,
                destinationY,
                participants,
                target.EntityId)) {
            target.ApplyKnockback(destinationX, destinationY);
        }
        return DiabloWorldIntentResult.Accepted;
    }

    private DiabloWorldIntentResult RestartTown(
        AuthoritativePlayerState player,
        ulong appliedTick)
    {
        if (!useNativePlayerActionTiming
            || player.Life != 0
            || player.CombatMode != DiabloPlayerCombatMode.Death
            || player.CurrentAction is not null) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }
        if (!world.IsWalkable(
                combatRules.RestartLevelId,
                combatRules.RestartPositionX,
                combatRules.RestartPositionY)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }

        player.LevelId = combatRules.RestartLevelId;
        player.PositionX = combatRules.RestartPositionX;
        player.PositionY = combatRules.RestartPositionY;
        player.Life = Math.Min(
            player.LifeMaximum,
            checked(combatRules.RestartLife * combatRules.RestartLifeScale));
        player.Mana = 0;
        player.ManaShieldActive = false;
        RemoveLevelTransitionStatuses(player);
        player.ActiveStoreId = null;
        var endTick = checked(appliedTick + combatRules.RestartTransitionTicks);
        DiabloPlayerActionLifecycle.Begin(
            player,
            new AuthoritativePlayerActionState(
                DiabloPlayerActionKind.RestartTown,
                appliedTick,
                appliedTick,
                endTick,
                endTick) {
                EffectApplied = true,
            },
            DiabloPlayerCombatMode.NewLevel);
        return DiabloWorldIntentResult.Accepted;
    }

    private DiabloWorldIntentResult Move(
        AuthoritativePlayerState player,
        DiabloMoveIntent intent,
        ulong appliedTick,
        IEnumerable<AuthoritativePlayerState> players)
    {
        if (intent.DirectionX is < -1 or > 1 || intent.DirectionY is < -1 or > 1
            || (intent.DirectionX == 0 && intent.DirectionY == 0)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.Malformed);
        }

        var targetX = player.PositionX + intent.DirectionX;
        var targetY = player.PositionY + intent.DirectionY;
        if (!world.IsWalkable(player.LevelId, targetX, targetY)
            || world.IsOccupied(player.LevelId, targetX, targetY, players, player.EntityId)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        if (useNativePlayerActionTiming) {
            if (!DiabloPlayerActionLifecycle.CanStart(player, appliedTick))
                return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
            player.FacingDirection = DiabloPlayerDirections.FromAdjacentStep(
                intent.DirectionX,
                intent.DirectionY);
            var animation = playerAnimations!.GetRequired(player.CharacterClassId);
            var endTick = checked(appliedTick + (ulong)animation.WalkingFrames);
            DiabloPlayerActionLifecycle.Begin(
                player,
                new AuthoritativePlayerActionState(
                    DiabloPlayerActionKind.Move,
                    appliedTick,
                    endTick,
                    endTick,
                    endTick) {
                    DirectionX = intent.DirectionX,
                    DirectionY = intent.DirectionY,
                },
                GetWalkMode(intent.DirectionX, intent.DirectionY));
            return DiabloWorldIntentResult.Accepted;
        }

        player.FacingDirection = DiabloPlayerDirections.FromAdjacentStep(
            intent.DirectionX,
            intent.DirectionY);
        player.PositionX = targetX;
        player.PositionY = targetY;
        return DiabloWorldIntentResult.Accepted;
    }

    private static DiabloPlayerCombatMode GetWalkMode(int directionX, int directionY)
    {
        return (directionX, directionY) switch {
            (1, 1) or (0, 1) or (1, 0) => DiabloPlayerCombatMode.WalkSouthwards,
            (-1, 0) or (-1, -1) or (0, -1) => DiabloPlayerCombatMode.WalkNorthwards,
            (-1, 1) or (1, -1) => DiabloPlayerCombatMode.WalkSideways,
            _ => throw new ArgumentOutOfRangeException(nameof(directionX)),
        };
    }

    private DiabloWorldIntentResult UsePortal(
        AuthoritativePlayerState player,
        uint portalId,
        ulong appliedTick,
        IEnumerable<AuthoritativePlayerState> players)
    {
        if (!world.Portals.TryGetValue(portalId, out var portal)
            || player.LevelId != portal.SourceLevelId
            || player.PositionX != portal.SourcePositionX
            || player.PositionY != portal.SourcePositionY) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        if (!world.IsWalkable(
                portal.DestinationLevelId,
                portal.DestinationPositionX,
                portal.DestinationPositionY)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }
        if (world.IsOccupied(
                portal.DestinationLevelId,
                portal.DestinationPositionX,
                portal.DestinationPositionY,
                players,
                player.EntityId)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }
        if (useNativePlayerActionTiming
            && (player.CombatMode != DiabloPlayerCombatMode.Stand
                || player.CurrentAction is not null)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }

        player.LevelId = portal.DestinationLevelId;
        player.PositionX = portal.DestinationPositionX;
        player.PositionY = portal.DestinationPositionY;
        player.ManaShieldActive = false;
        RemoveLevelTransitionStatuses(player);
        if (useNativePlayerActionTiming) {
            var endTick = checked(appliedTick + 1);
            var action = new AuthoritativePlayerActionState(
                DiabloPlayerActionKind.LevelTransition,
                appliedTick,
                appliedTick,
                endTick,
                endTick) {
                EffectApplied = true,
                TargetEntityId = portalId,
            };
            DiabloPlayerActionLifecycle.Begin(
                player,
                action,
                DiabloPlayerCombatMode.NewLevel);
        }
        return DiabloWorldIntentResult.Accepted;
    }

    private static void RemoveLevelTransitionStatuses(AuthoritativePlayerState player)
    {
        player.StatusEffects.RemoveAll(effect =>
            effect.Behavior is AuthoritativeStatusEffectBehavior.Infravision
                or AuthoritativeStatusEffectBehavior.Search);
    }

    private DiabloWorldIntentResult PickupWorldItem(
        AuthoritativePlayerState player,
        uint itemEntityId,
        ulong appliedTick,
        bool requireAdjacent = true,
        bool applyEffects = true)
    {
        if (!world.WorldItems.TryGetValue(itemEntityId, out var item)
            || item.LevelId != player.LevelId
            || (requireAdjacent && Math.Max(
                Math.Abs(item.PositionX - player.PositionX),
                Math.Abs(item.PositionY - player.PositionY)) > 1)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        if (player.HeldItem is not null)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);

        if (item.State.ItemType == combatRules.DeathGoldItemType
            && item.State.ItemIndex == combatRules.DeathGoldItemIndex) {
            if (item.State.Value <= 0
                || item.State.Value > combatRules.DeathGoldMaximumStack
                || item.State.IdentifiedValue != item.State.Value
                || item.Price != checked((uint)item.State.Value)
                || !item.State.StatFlag
                || uint.MaxValue - player.Gold < item.Price) {
                return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.Malformed);
            }
            if (DiabloGoldInventory.TryGetTopologyTotal(
                    player,
                    combatRules,
                    allowTruncatedWalletRemainder: true,
                    out var topologyTotal)) {
                if (uint.MaxValue - topologyTotal < item.Price)
                    return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.Malformed);
                if (!applyEffects)
                    return DiabloWorldIntentResult.Accepted;
                if (!DiabloGoldInventory.TryAddPartial(
                        player,
                        item.Price,
                        combatRules,
                        AllocateGoldStackSeed,
                        allowTruncatedWalletRemainder: true,
                        recalculateWalletFromTopology: true,
                        out var remainder)) {
                    return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.Malformed);
                }
                if (remainder > 0) {
                    player.HeldItem = new OwnedStoreItem(
                        0,
                        0,
                        item.ItemSeed,
                        remainder,
                        appliedTick,
                        item.State with {
                            Value = checked((int)remainder),
                            IdentifiedValue = checked((int)remainder),
                        });
                }
            } else {
                if (!applyEffects)
                    return DiabloWorldIntentResult.Accepted;
                player.Gold += item.Price;
            }
            world.WorldItems.Remove(itemEntityId);
            return DiabloWorldIntentResult.Accepted;
        }

        var targetCell = InventoryGridLayout.FindFirstPlacement(
            player.InventoryGrid,
            item.State.InventoryWidth,
            item.State.InventoryHeight);
        if (targetCell < 0)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        if (!applyEffects)
            return DiabloWorldIntentResult.Accepted;

        var inventoryIndex = player.Inventory.Count;
        player.Inventory.Add(new OwnedStoreItem(
            0,
            0,
            item.ItemSeed,
            item.Price,
            appliedTick,
            item.State));
        InventoryGridLayout.Fill(
            player.InventoryGrid,
            targetCell,
            inventoryIndex,
            item.State.InventoryWidth,
            item.State.InventoryHeight);
        world.WorldItems.Remove(itemEntityId);
        return DiabloWorldIntentResult.Accepted;
    }

    private DiabloWorldIntentResult DropHeldItem(
        AuthoritativePlayerState player,
        int targetX,
        int targetY,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (player.HeldItem is not { } heldItem)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        var originX = player.FuturePositionX;
        var originY = player.FuturePositionY;
        if (Math.Max(
                Math.Abs(targetX - originX),
                Math.Abs(targetY - originY)) > 1
            || !world.IsWalkable(player.LevelId, targetX, targetY)
            || world.IsOccupied(
                player.LevelId,
                targetX,
                targetY,
                players,
                excludedEntityId: player.EntityId)
            || world.WorldItems.Values.Any(item =>
                item.LevelId == player.LevelId
                && item.PositionX == targetX
                && item.PositionY == targetY)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }

        var entityId = AllocateWorldItemEntityId(players);
        world.WorldItems.Add(entityId, new AuthoritativeWorldItem(
            entityId,
            player.LevelId,
            targetX,
            targetY,
            heldItem.ItemSeed,
            heldItem.Price,
            heldItem.State));
        player.HeldItem = null;
        return DiabloWorldIntentResult.Accepted;
    }

    private uint AllocateGoldStackSeed()
    {
        uint itemSeed;
        do {
            itemSeed = unchecked((uint)random.AdvanceRndSeed());
        } while (itemSeed == 0);
        return itemSeed;
    }

    private uint AllocateWorldItemEntityId(
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        while (nextWorldItemEntityId == 0
            || players.Any(player => player.EntityId == nextWorldItemEntityId)
            || world.CombatTargets.ContainsKey(nextWorldItemEntityId)
            || world.WorldItems.ContainsKey(nextWorldItemEntityId)
            || world.Objects.ContainsKey(nextWorldItemEntityId)
            || world.Projectiles.ContainsKey(nextWorldItemEntityId)) {
            nextWorldItemEntityId = checked(nextWorldItemEntityId + 1);
        }
        return nextWorldItemEntityId++;
    }

    private DiabloWorldIntentResult OperateObject(
        AuthoritativePlayerState player,
        uint objectEntityId,
        bool requireAdjacent = true,
        bool applyEffects = true)
    {
        if (!world.Objects.TryGetValue(objectEntityId, out var @object)
            || @object.Activated
            || @object.LevelId != player.LevelId
            || (requireAdjacent && Math.Max(
                Math.Abs(@object.PositionX - player.PositionX),
                Math.Abs(@object.PositionY - player.PositionY)) > 1)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        if (!applyEffects)
            return DiabloWorldIntentResult.Accepted;

        @object.Activated = true;
        if (@object.QuestId != 0 && world.Quests.TryGetValue(@object.QuestId, out var quest)
            && !quest.Completed && quest.LevelId == player.LevelId) {
            quest.Progress++;
            if (quest.Progress >= quest.RequiredProgress)
                quest.Completed = true;
        }

        if (@object.EffectAmount <= 0)
            return DiabloWorldIntentResult.Accepted;

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
            var damage = playerDamage?.Apply(player, @object.EffectAmount)
                ?? ApplyUnshieldedDamage(player, @object.EffectAmount);
            return damage.IncomingDamageQ6 > 0
                ? DiabloWorldIntentResult.AcceptedWith(new DiabloGameplayEvent(
                    DiabloGameplayEventKind.Damage,
                    @object.EntityId,
                    player.EntityId,
                    damage.IncomingDamageQ6))
                : DiabloWorldIntentResult.Accepted;
        case AuthoritativeObjectEffectKind.Experience:
            DiabloPlayerProgression.GrantExperience(
                player,
                checked((uint)@object.EffectAmount));
            return DiabloWorldIntentResult.AcceptedWith(new DiabloGameplayEvent(
                DiabloGameplayEventKind.Experience,
                player.EntityId,
                player.EntityId,
                @object.EffectAmount));
        default:
            return DiabloWorldIntentResult.Accepted;
        }
    }

    private DiabloWorldIntentResult DisarmTrap(
        AuthoritativePlayerState player,
        uint objectEntityId)
    {
        if (trapDisarmAbilitySpellId == 0
            || player.AbilitySpellId != trapDisarmAbilitySpellId) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }
        if (!world.Objects.TryGetValue(objectEntityId, out var @object)
            || !@object.TrapArmed
            || @object.LevelId != player.LevelId
            || Math.Max(
                Math.Abs(@object.PositionX - player.PositionX),
                Math.Abs(@object.PositionY - player.PositionY)) > 1) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

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

    private DiabloWorldIntentResult AdvanceQuest(
        AuthoritativePlayerState player,
        uint questId)
    {
        if (!world.Quests.TryGetValue(questId, out var quest)
            || quest.Completed
            || quest.LevelId != player.LevelId) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        quest.Progress++;
        if (quest.Progress >= quest.RequiredProgress)
            quest.Completed = true;
        return DiabloWorldIntentResult.Accepted;
    }

    private static DiabloWorldIntentResult SetFriendlyMode(
        AuthoritativePlayerState player,
        bool friendlyMode)
    {
        player.FriendlyMode = friendlyMode;
        return DiabloWorldIntentResult.Accepted;
    }

}

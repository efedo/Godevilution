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
    private readonly AuthoritativeQuestTypeCatalog? questTypes;
    private readonly DiabloGameMode gameMode;
    private uint nextWorldItemEntityId = 0x40000000;

    public DiabloWorldCommandProcessor(
        DiabloWorldState world,
        AuthoritativePlayerAnimationCatalog? playerAnimations = null,
        bool useNativePlayerActionTiming = false,
        AuthoritativeCombatRules? combatRules = null,
        DiabloRandom? random = null,
        AuthoritativePlayerDamageResolver? playerDamage = null,
        uint trapDisarmAbilitySpellId = 0,
        AuthoritativeQuestTypeCatalog? questTypes = null,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.playerAnimations = playerAnimations;
        this.useNativePlayerActionTiming = useNativePlayerActionTiming;
        this.combatRules = combatRules ?? new AuthoritativeCombatRules(10, 1, 100);
        this.random = random ?? new DiabloRandom(0);
        this.playerDamage = playerDamage;
        this.trapDisarmAbilitySpellId = trapDisarmAbilitySpellId;
        this.questTypes = questTypes;
        this.gameMode = gameMode;
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

        ActivateInitialQuestsForLevel(player.LevelId);

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

    /** Creates the server-owned dynamic portal used by the native Town Portal spell. */
    public DiabloWorldIntentResult ExecuteTownPortal(
        AuthoritativePlayerState player,
        int targetX,
        int targetY,
        ulong appliedTick,
        IEnumerable<AuthoritativePlayerState> players,
        bool applyEffects = true,
        int searchRadius = 5,
        int lifetime = 100)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(players);
        if (searchRadius is <= 0 or > DiabloCrawl.MaximumSupportedRadius || lifetime <= 0)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.Malformed);
        var participants = players.ToArray();
        if (player.LevelId == AuthoritativePortalRules.TownLevelId
            || !world.HasMissileLineOfSight(
                player.LevelId,
                player.PositionX,
                player.PositionY,
                targetX,
                targetY)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        var portalPosition = DiabloCrawl.FindClosestValidPosition(
            targetX,
            targetY,
            0,
            searchRadius,
            (candidateX, candidateY) =>
                world.IsWalkable(player.LevelId, candidateX, candidateY)
                && !world.IsMissileBlocked(player.LevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    player.LevelId,
                    candidateX,
                    candidateY,
                    participants)
                && !IsPortalEndpoint(player.LevelId, candidateX, candidateY));

        if (!applyEffects)
            return DiabloWorldIntentResult.Accepted;

        foreach (var existing in world.Portals.Values
                     .Where(portal => portal.OwnerEntityId == player.EntityId)
                     .Select(portal => portal.PortalId)
                     .ToArray()) {
            world.Portals.Remove(existing);
        }
        if (!portalPosition.HasValue)
            return DiabloWorldIntentResult.Accepted;

        var portalId = AllocatePortalId();
        world.Portals.Add(
            portalId,
            new AuthoritativePortal(
                portalId,
                player.LevelId,
                portalPosition.Value.X,
                portalPosition.Value.Y,
                AuthoritativePortalRules.TownLevelId,
                AuthoritativePortalRules.GetTownPositionX(player.EntityId),
                AuthoritativePortalRules.TownPositionY) {
                OwnerEntityId = player.EntityId,
                Bidirectional = true,
                RemainingTicks = checked((uint)lifetime),
            });
        return DiabloWorldIntentResult.Accepted;
    }

    /** Applies the native Warp spell to the nearest normalized level trigger. */
    public DiabloWorldIntentResult ExecuteWarp(
        AuthoritativePlayerState player,
        ulong appliedTick,
        IEnumerable<AuthoritativePlayerState> players,
        bool applyEffects = true,
        int searchRadius = 5)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(players);
        if (world.World is null
            || player.LevelId == AuthoritativePortalRules.TownLevelId) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }

        var warp = world.World.FindNearestWarpTarget(
            player.LevelId,
            player.PositionX,
            player.PositionY);
        if (warp is null)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);

        var participants = players.ToArray();
        var destination = DiabloCrawl.FindClosestValidPosition(
            warp.TargetX,
            warp.TargetY,
            0,
            searchRadius,
            (candidateX, candidateY) =>
                world.IsWalkable(warp.TargetLevelId, candidateX, candidateY)
                && !world.IsMissileBlocked(warp.TargetLevelId, candidateX, candidateY)
                && !world.IsOccupied(
                    warp.TargetLevelId,
                    candidateX,
                    candidateY,
                    participants,
                    player.EntityId)
                && !world.World.IsWarpTrigger(
                    warp.TargetLevelId,
                    candidateX,
                    candidateY));
        if (!destination.HasValue)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        if (!applyEffects)
            return DiabloWorldIntentResult.Accepted;

        player.LevelId = warp.TargetLevelId;
        player.PositionX = destination.Value.X;
        player.PositionY = destination.Value.Y;
        ActivateInitialQuestsForLevel(player.LevelId);
        player.ManaShieldActive = false;
        RemoveLevelTransitionStatuses(player);
        if (useNativePlayerActionTiming) {
            var endTick = checked(appliedTick + 2);
            DiabloPlayerActionLifecycle.Begin(
                player,
                new AuthoritativePlayerActionState(
                    DiabloPlayerActionKind.LevelTransition,
                    appliedTick,
                    appliedTick,
                    endTick,
                    endTick) {
                    EffectApplied = true,
                    TargetEntityId = warp.WarpId,
                },
                DiabloPlayerCombatMode.NewLevel);
        }
        return DiabloWorldIntentResult.Accepted;
    }

    private uint AllocatePortalId()
    {
        const uint firstDynamicPortalId = 0x20000000;
        var candidate = world.Portals.Keys
            .Where(id => id >= firstDynamicPortalId)
            .DefaultIfEmpty(firstDynamicPortalId - 1)
            .Max();
        do {
            candidate = checked(candidate + 1);
        } while (world.Portals.ContainsKey(candidate));
        return candidate;
    }

    private bool IsPortalEndpoint(uint levelId, int positionX, int positionY)
    {
        return world.Portals.Values.Any(portal =>
            (portal.SourceLevelId == levelId
                && portal.SourcePositionX == positionX
                && portal.SourcePositionY == positionY)
            || (portal.DestinationLevelId == levelId
                && portal.DestinationPositionX == positionX
                && portal.DestinationPositionY == positionY));
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
        ActivateInitialQuestsForLevel(player.LevelId);
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
        if (!world.Portals.TryGetValue(portalId, out var portal)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        var useSourceEndpoint = player.LevelId == portal.SourceLevelId
            && player.PositionX == portal.SourcePositionX
            && player.PositionY == portal.SourcePositionY;
        var useDestinationEndpoint = portal.Bidirectional
            && player.LevelId == portal.DestinationLevelId
            && player.PositionX == portal.DestinationPositionX
            && player.PositionY == portal.DestinationPositionY;
        if (!useSourceEndpoint && !useDestinationEndpoint)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);

        var destinationLevelId = useSourceEndpoint
            ? portal.DestinationLevelId
            : portal.SourceLevelId;
        var destinationPositionX = useSourceEndpoint
            ? portal.DestinationPositionX
            : portal.SourcePositionX;
        var destinationPositionY = useSourceEndpoint
            ? portal.DestinationPositionY
            : portal.SourcePositionY;
        if (!world.IsWalkable(
                destinationLevelId,
                destinationPositionX,
                destinationPositionY)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }
        if (world.IsOccupied(
                destinationLevelId,
                destinationPositionX,
                destinationPositionY,
                players,
                player.EntityId)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }
        if (useNativePlayerActionTiming
            && (player.CombatMode != DiabloPlayerCombatMode.Stand
                || player.CurrentAction is not null)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }

        player.LevelId = destinationLevelId;
        player.PositionX = destinationPositionX;
        player.PositionY = destinationPositionY;
        ActivateInitialQuestsForLevel(player.LevelId);
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
                or AuthoritativeStatusEffectBehavior.Search
                or AuthoritativeStatusEffectBehavior.Etherealize);
    }

    /** Activates external QUEST_INIT rows when the authoritative player enters their route level. */
    private void ActivateInitialQuestsForLevel(uint levelId)
    {
        if (questTypes is null)
            return;
        foreach (var quest in world.Quests.Values) {
            if (quest.Unavailable
                || quest.Completed
                || quest.Active
                || !questTypes.TryGet(quest.QuestId, out var definition)) {
                continue;
            }
            if (gameMode == DiabloGameMode.Multiplayer && definition.SinglePlayerOnly)
                continue;
            var routeLevel = gameMode == DiabloGameMode.Multiplayer
                ? definition.MultiplayerLevel
                : definition.SinglePlayerLevel;
            if (routeLevel >= 0 && (uint)routeLevel == levelId)
                quest.Active = true;
        }
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

        if (DiabloGoldInventory.HasGoldIdentity(
                item.State,
                combatRules,
                player.Items)) {
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
                    out var topologyTotal,
                    player.Items)) {
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
                        out var remainder,
                        player.Items)) {
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
        if (@object.QuestId != 0
            && world.Quests.TryGetValue(@object.QuestId, out var linkedQuest)
            && linkedQuest.Unavailable) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        if (!applyEffects)
            return DiabloWorldIntentResult.Accepted;

        @object.Activated = true;
        if (@object.QuestId != 0 && world.Quests.TryGetValue(@object.QuestId, out var quest)
            && !quest.Unavailable
            && !quest.Completed && quest.LevelId == player.LevelId) {
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
                    DiabloFixedPointQ6.ToWholeTruncated(
                        damage.IncomingDamageQ6)) {
                    AmountQ6 = damage.IncomingDamageQ6,
                })
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
        if (!player.HasAbility(
                DiabloPlayerAbilityKind.TrapDisarm,
                trapDisarmAbilitySpellId)) {
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

    private static DiabloWorldIntentResult SetFriendlyMode(
        AuthoritativePlayerState player,
        bool friendlyMode)
    {
        player.FriendlyMode = friendlyMode;
        return DiabloWorldIntentResult.Accepted;
    }

}

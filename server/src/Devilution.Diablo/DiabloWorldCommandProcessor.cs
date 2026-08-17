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
    private readonly AuthoritativeItemCatalog? items;
    private readonly AuthoritativeItemCombinationCatalog? itemCombinations;
    private readonly AuthoritativeItemQuestEffectCatalog? itemQuestEffects;
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
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer,
        AuthoritativeItemCatalog? items = null,
        AuthoritativeItemCombinationCatalog? itemCombinations = null,
        AuthoritativeItemQuestEffectCatalog? itemQuestEffects = null)
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
        this.items = items;
        this.itemCombinations = itemCombinations;
        this.itemQuestEffects = itemQuestEffects;
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
            DiabloTalkToQuestIntent talk => TalkToQuest(
                player,
                talk.QuestId,
                talk.TargetEntityId),
            DiabloTurnInQuestIntent turnIn => TurnInQuest(
                player,
                turnIn.QuestId,
                turnIn.TownerId),
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
        int lifetime = 100,
        int destinationPositionX = AuthoritativePortalRules.TownPositionX,
        int destinationPositionY = AuthoritativePortalRules.TownPositionY,
        int destinationSlotCount = AuthoritativePortalRules.TownPortalSlotCount,
        int destinationSlotSpacing = AuthoritativePortalRules.TownPortalSlotSpacing)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(players);
        if (searchRadius is <= 0 or > DiabloCrawl.MaximumSupportedRadius || lifetime <= 0)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.Malformed);
        if (destinationPositionX < 0
            || destinationPositionY < 0
            || destinationSlotCount <= 0
            || destinationSlotSpacing < 0)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.Malformed);
        var participants = players.ToArray();
        if (player.LevelId == combatRules.TownLevelId
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
                combatRules.TownLevelId,
                AuthoritativePortalRules.GetTownPositionX(
                    player.EntityId,
                    destinationPositionX,
                    destinationSlotCount,
                    destinationSlotSpacing),
                destinationPositionY) {
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
            || player.LevelId == combatRules.TownLevelId) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }

        var warp = world.World.FindNearestWarpTarget(
            player.LevelId,
            player.PositionX,
            player.PositionY,
            IsWarpAvailable);
        if (warp is null)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);

        return ApplyWarpTarget(
            player,
            warp,
            appliedTick,
            players,
            applyEffects,
            searchRadius);
    }

    /** Applies an exact authored level-trigger after movement reaches its tile. */
    public DiabloWorldIntentResult ExecuteLevelTrigger(
        AuthoritativePlayerState player,
        ulong appliedTick,
        IEnumerable<AuthoritativePlayerState> players,
        bool applyEffects = true,
        int searchRadius = 5)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(players);
        if (world.World is null) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }

        var warp = world.World.WarpTargets
            .Where(target => target.SourceLevelId == player.LevelId
                && target.TriggerX == player.PositionX
                && target.TriggerY == player.PositionY)
            .Where(IsWarpAvailable)
            .OrderBy(target => target.WarpId)
            .FirstOrDefault();
        if (warp is null)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);

        return ApplyWarpTarget(
            player,
            warp,
            appliedTick,
            players,
            applyEffects,
            searchRadius);
    }

    private DiabloWorldIntentResult ApplyWarpTarget(
        AuthoritativePlayerState player,
        AuthoritativeWarpTarget warp,
        ulong appliedTick,
        IEnumerable<AuthoritativePlayerState> players,
        bool applyEffects,
        int searchRadius)
    {

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
                && !world.World!.IsWarpTrigger(
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
        var levelTrigger = ExecuteLevelTrigger(player, appliedTick, players);
        if (levelTrigger.Succeeded)
            return levelTrigger;
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
                || !quest.ActivateOnRoute
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
        ApplyItemQuestEffects(item.State);
        ApplyItemCombinations(player, appliedTick);
        return DiabloWorldIntentResult.Accepted;
    }

    /** Resolves content-owned quest gates on native level triggers. */
    private bool IsWarpAvailable(AuthoritativeWarpTarget warp)
    {
        if (warp.RequiredQuestId == 0)
            return true;
        if (!world.Quests.TryGetValue(warp.RequiredQuestId, out var quest)
            || quest.Unavailable
            || (warp.RequiredQuestCompleted
                ? !quest.Completed
                : !quest.Active)) {
            return false;
        }
        return true;
    }

    /** Applies deterministic content-owned quest transitions after pickup. */
    private void ApplyItemQuestEffects(AuthoritativeItemState itemState)
    {
        if (items is null || itemQuestEffects is null)
            return;

        foreach (var effect in itemQuestEffects.Definitions) {
            if (!items.TryGet(effect.ItemId, out var itemDefinition)
                || !items.IsStateForDefinition(itemState, itemDefinition)
                || !world.Quests.TryGetValue(effect.QuestId, out var quest)
                || quest.Unavailable) {
                continue;
            }
            if (effect.RequiredVariable1 is int requiredVariable1
                && quest.Variable1 != requiredVariable1) {
                continue;
            }

            switch (effect.EffectKind) {
            case AuthoritativeItemQuestEffectKind.ActivateQuest:
                if (!quest.Completed)
                    quest.Active = true;
                quest.Logged = true;
                quest.Variable1 = Math.Max(quest.Variable1, effect.EffectAmount);
                break;
            case AuthoritativeItemQuestEffectKind.CompleteQuest:
                if (!quest.Active)
                    continue;
                quest.Progress = quest.RequiredProgress;
                quest.Completed = true;
                quest.Active = false;
                quest.Logged = true;
                quest.Variable1 = Math.Max(quest.Variable1, effect.EffectAmount);
                break;
            case AuthoritativeItemQuestEffectKind.SetVariable1:
                if (!quest.Active)
                    continue;
                quest.Variable1 = effect.EffectAmount;
                break;
            default:
                throw new InvalidDataException(
                    $"Unsupported item quest effect {effect.EffectKind}.");
            }
        }
    }

    /** Applies deterministic content-owned item combinations after pickup. */
    private void ApplyItemCombinations(
        AuthoritativePlayerState player,
        ulong appliedTick)
    {
        if (items is null || itemCombinations is null)
            return;

        while (true) {
            AuthoritativeItemCombinationDefinition? selected = null;
            var consumedIndices = Array.Empty<int>();
            foreach (var definition in itemCombinations.Definitions) {
                var usedIndices = new HashSet<int>();
                var matchingIndices = new List<int>();
                var allInputsPresent = true;
                foreach (var itemId in definition.RequiredItemIds) {
                    if (!items.TryGet(itemId, out var itemDefinition)) {
                        allInputsPresent = false;
                        break;
                    }
                    var matchingIndex = -1;
                    for (var index = 0; index < player.Inventory.Count; index++) {
                        if (!usedIndices.Contains(index)
                            && items.IsStateForDefinition(
                                player.Inventory[index].State,
                                itemDefinition)) {
                            matchingIndex = index;
                            break;
                        }
                    }
                    if (matchingIndex < 0) {
                        allInputsPresent = false;
                        break;
                    }
                    usedIndices.Add(matchingIndex);
                    matchingIndices.Add(matchingIndex);
                }
                if (allInputsPresent) {
                    selected = definition;
                    consumedIndices = matchingIndices.ToArray();
                    break;
                }
            }

            if (selected is null)
                return;

            var resultIndex = consumedIndices.Min();
            foreach (var index in consumedIndices
                         .Where(index => index != resultIndex)
                         .OrderByDescending(index => index)) {
                InventoryGridLayout.RemoveAndCompact(player.InventoryGrid, index);
                player.Inventory.RemoveAt(index);
            }

            var resultAnchor = InventoryGridLayout.FindAnchor(
                player.InventoryGrid,
                resultIndex);
            var state = items.Generate(
                selected.ResultItemId,
                AllocateGoldStackSeed());
            InventoryGridLayout.Clear(player.InventoryGrid, resultIndex);
            var targetCell = resultAnchor >= 0
                && InventoryGridLayout.CanPlace(
                    player.InventoryGrid,
                    (uint)resultAnchor,
                    state.InventoryWidth,
                    state.InventoryHeight)
                ? resultAnchor
                : InventoryGridLayout.FindFirstPlacement(
                    player.InventoryGrid,
                    state.InventoryWidth,
                    state.InventoryHeight);
            if (targetCell < 0)
                throw new InvalidDataException(
                    $"Item combination result {selected.ResultItemId} has no inventory placement.");
            player.Inventory[resultIndex] = new OwnedStoreItem(
                0,
                0,
                state.CreateInfo,
                0,
                appliedTick,
                state);
            InventoryGridLayout.Fill(
                player.InventoryGrid,
                targetCell,
                resultIndex,
                state.InventoryWidth,
                state.InventoryHeight);
        }
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

        if (@object.EffectKind == AuthoritativeObjectEffectKind.QuestSequence)
            return ApplyQuestSequence(player, @object);
        if (@object.EffectKind == AuthoritativeObjectEffectKind.QuestItemCompletion)
            return ApplyQuestItemCompletion(player, @object);

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
        AuthoritativeWorldObject @object)
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
        return DiabloWorldIntentResult.Accepted;
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

    /**
     * Starts the native quest-dialogue boundary without exposing presentation
     * text or encounter state to the client. Content supplies the quest's
     * message identity and unique completion identity; the server validates
     * both before activating the conversation.
     */
    private DiabloWorldIntentResult TalkToQuest(
        AuthoritativePlayerState player,
        uint questId,
        uint targetEntityId)
    {
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
            || Math.Max(
                Math.Abs(target.PositionX - player.PositionX),
                Math.Abs(target.PositionY - player.PositionY)) > 1) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        quest.Active = true;
        quest.Logged = true;
        quest.Variable1 = Math.Max(quest.Variable1, quest.DialogueVariable1);
        quest.Variable2 = Math.Max(quest.Variable2, quest.DialogueVariable2);
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

    /** Applies a content-defined native towner turn-in, including item consumption and reward spawn. */
    private DiabloWorldIntentResult TurnInQuest(
        AuthoritativePlayerState player,
        uint questId,
        uint townerId)
    {
        if (items is null
            || !world.Quests.TryGetValue(questId, out var quest)
            || quest.TurnInTownerId < 0
            || quest.TurnInTownerId != townerId
            || quest.Unavailable
            || quest.Completed
            || !quest.Active
            || quest.TurnInLevelId != player.LevelId
            || Math.Max(
                Math.Abs(quest.TurnInPositionX - player.PositionX),
                Math.Abs(quest.TurnInPositionY - player.PositionY)) > 1
            || quest.TurnInItemId == 0
            || !items.TryGet(quest.TurnInItemId, out var requiredItem)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        if (quest.TurnInRequiredVariable2 >= 0
            && quest.Variable2 != quest.TurnInRequiredVariable2) {
            if (quest.Variable2 > quest.TurnInRequiredVariable2)
                return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            quest.Variable2 = quest.TurnInRequiredVariable2;
            quest.Logged = true;
            return DiabloWorldIntentResult.Accepted;
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

        InventoryGridLayout.RemoveAndCompact(player.InventoryGrid, inventoryIndex);
        player.Inventory.RemoveAt(inventoryIndex);
        quest.Progress = quest.RequiredProgress;
        quest.Completed = true;
        quest.Active = false;
        quest.Logged = true;
        quest.Variable1 = Math.Max(quest.Variable1, quest.CompletionVariable1);
        quest.Variable2 = Math.Max(quest.Variable2, quest.CompletionVariable2);

        if (quest.CompletionActivateQuestId != 0
            && world.Quests.TryGetValue(quest.CompletionActivateQuestId, out var nextQuest)
            && !nextQuest.Unavailable
            && !nextQuest.Completed) {
            nextQuest.Active = true;
        }

        if (quest.TurnInRewardUniqueId != 0) {
            var itemSeed = AllocateGoldStackSeed();
            var rewardState = items.GenerateUnique(
                quest.TurnInRewardUniqueId,
                itemSeed,
                quest.TurnInRewardItemLevel);
            var entityId = AllocateWorldItemEntityId([player]);
            world.WorldItems[entityId] = new AuthoritativeWorldItem(
                entityId,
                quest.TurnInLevelId,
                quest.TurnInRewardPositionX,
                quest.TurnInRewardPositionY,
                itemSeed,
                0,
                rewardState);
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

    private static DiabloWorldIntentResult SetFriendlyMode(
        AuthoritativePlayerState player,
        bool friendlyMode)
    {
        player.FriendlyMode = friendlyMode;
        return DiabloWorldIntentResult.Accepted;
    }

}

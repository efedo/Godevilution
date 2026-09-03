using Devilution.Domain.Inventory;
using Devilution.Engine;

namespace Devilution.Diablo;

/** Applies non-combat world intents without transport or host dependencies. */
public sealed class DiabloWorldCommandProcessor
{
    private readonly DiabloWorldState world;
    private readonly AuthoritativePlayerAnimationCatalog? playerAnimations;
    private readonly bool useNativePlayerActionTiming;
    private readonly AuthoritativeCombatRules combatRules;
    private readonly DeterministicLegacyRandom random;
    private readonly AuthoritativePlayerDamageResolver? playerDamage;
    private readonly uint trapDisarmAbilitySpellId;
    private readonly AuthoritativeQuestTypeCatalog? questTypes;
    private readonly DiabloGameMode gameMode;
    private readonly AuthoritativeItemCatalog? items;
    private readonly AuthoritativeSpellCatalog? spells;
    private readonly AuthoritativeItemCombinationCatalog? itemCombinations;
    private readonly AuthoritativeItemQuestEffectCatalog? itemQuestEffects;
    private readonly AuthoritativeSpellBookCatalog? spellBooks;
    private readonly AuthoritativeQuestTownerInteractionCatalog? questTownerInteractions;
    private readonly AuthoritativeQuestDialogueStageCatalog? questDialogueStages;
    private readonly AuthoritativeMonsterSpawnPlanner? monsterSpawnPlanner;
    private readonly DiabloDifficulty difficulty;
    private readonly DiabloGameVariant gameVariant;
    private readonly AuthoritativeMonsterTypeCatalog? monsterTypes;
    private readonly AuthoritativeExperienceCatalog? experience;
    private Func<int, AuthoritativeSetLevelObjectOrigin?>? setLevelOriginResolver;
    private readonly DeterministicEntityIdAllocator worldItemEntityIds = new(0x40000000);
    private readonly DeterministicEntityIdAllocator authoredProjectileEntityIds = new(0xC0000000);
    private readonly DeterministicEntityIdAllocator generatedMonsterEntityIds = new(
        AuthoritativeMonsterSpawnPlanner.FirstGeneratedMonsterEntityId);

    public DiabloWorldCommandProcessor(
        DiabloWorldState world,
        AuthoritativePlayerAnimationCatalog? playerAnimations = null,
        bool useNativePlayerActionTiming = false,
        AuthoritativeCombatRules? combatRules = null,
        DeterministicLegacyRandom? random = null,
        AuthoritativePlayerDamageResolver? playerDamage = null,
        uint trapDisarmAbilitySpellId = 0,
        AuthoritativeQuestTypeCatalog? questTypes = null,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer,
        AuthoritativeItemCatalog? items = null,
        AuthoritativeItemCombinationCatalog? itemCombinations = null,
        AuthoritativeItemQuestEffectCatalog? itemQuestEffects = null,
        AuthoritativeSpellBookCatalog? spellBooks = null,
        AuthoritativeQuestTownerInteractionCatalog? questTownerInteractions = null,
        AuthoritativeQuestDialogueStageCatalog? questDialogueStages = null,
        AuthoritativeSpellCatalog? spells = null,
        AuthoritativeMonsterSpawnPlanner? monsterSpawnPlanner = null,
        DiabloDifficulty difficulty = DiabloDifficulty.Normal,
        DiabloGameVariant gameVariant = DiabloGameVariant.Diablo,
        AuthoritativeMonsterTypeCatalog? monsterTypes = null,
        AuthoritativeExperienceCatalog? experience = null)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.playerAnimations = playerAnimations;
        this.useNativePlayerActionTiming = useNativePlayerActionTiming;
        this.combatRules = combatRules ?? new AuthoritativeCombatRules(10, 1, 100);
        this.random = random ?? new DeterministicLegacyRandom(0);
        this.playerDamage = playerDamage;
        this.trapDisarmAbilitySpellId = trapDisarmAbilitySpellId;
        this.questTypes = questTypes;
        this.gameMode = gameMode;
        this.items = items;
        this.spells = spells;
        this.itemCombinations = itemCombinations;
        this.itemQuestEffects = itemQuestEffects;
        this.spellBooks = spellBooks;
        this.questTownerInteractions = questTownerInteractions;
        this.questDialogueStages = questDialogueStages;
        this.monsterSpawnPlanner = monsterSpawnPlanner;
        this.difficulty = difficulty;
        this.gameVariant = gameVariant;
        this.monsterTypes = monsterTypes;
        this.experience = experience;
        if (useNativePlayerActionTiming && playerAnimations is null) {
            throw new ArgumentException(
                "Native player action timing requires a player animation catalog.",
                nameof(playerAnimations));
        }
    }

    internal void SetSetLevelOriginResolver(
        Func<int, AuthoritativeSetLevelObjectOrigin?> resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        setLevelOriginResolver = resolver;
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

        EnsureLevelEntryState(player.LevelId);

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
            DiabloMoveToIntent moveTo => MoveTo(player, moveTo, appliedTick, players),
            DiabloUsePortalIntent portal => UsePortal(
                player,
                portal.PortalId,
                appliedTick,
                players),
            DiabloPickupWorldItemIntent pickup => PickupWorldItem(
                player,
                pickup.ItemEntityId,
                appliedTick,
                players),
            DiabloDropHeldItemIntent drop => DropHeldItem(
                player,
                drop.TargetX,
                drop.TargetY,
                players.ToArray()),
            DiabloDropItemIntent drop => DropItem(
                player,
                drop.Item,
                drop.TargetX,
                drop.TargetY,
                players.ToArray()),
            DiabloOperateObjectIntent operation => OperateObject(
                player,
                operation.ObjectEntityId,
                players.ToArray()),
            DiabloDisarmTrapIntent disarm => DisarmTrap(player, disarm.ObjectEntityId),
            DiabloAdvanceQuestIntent quest => AdvanceQuest(player, quest.QuestId),
            DiabloTalkToQuestIntent talk => TalkToQuest(
                player,
                talk.QuestId,
                talk.TargetEntityId,
                players.ToArray()),
            DiabloTurnInQuestIntent turnIn => TurnInQuest(
                player,
                turnIn.QuestId,
                turnIn.TownerId,
                players.ToArray()),
            DiabloSetFriendlyModeIntent mode => SetFriendlyMode(player, mode.FriendlyMode),
            DiabloAllocateAttributeIntent allocation => AllocateAttribute(player, allocation),
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
                participants,
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
                participants,
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

        var directionX = Math.Sign((long)target.PositionX - player.PositionX);
        var directionY = Math.Sign((long)target.PositionY - player.PositionY);
        if (directionX == 0 && directionY == 0)
            return DiabloWorldIntentResult.Accepted;
        if (!TryOffsetCoordinate(target.PositionX, directionX, out var destinationX)
            || !TryOffsetCoordinate(target.PositionY, directionY, out var destinationY))
            return DiabloWorldIntentResult.Accepted;
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
        if (searchRadius is <= 0 or > DeterministicGridCrawl.MaximumSupportedRadius || lifetime <= 0)
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

        var portalPosition = DeterministicGridCrawl.FindClosestValidPosition(
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
        if (world.Portals.Count >= AuthoritativePortalRules.NativeMaximumPortalCount)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);

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

    /** Applies authored object operations whose trigger tile is occupied by a player. */
    public DiabloWorldIntentResult ExecuteOccupancyTriggers(
        AuthoritativePlayerState player,
        IEnumerable<AuthoritativePlayerState> players)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(players);
        var participants = players.ToArray();
        var events = new List<DiabloGameplayEvent>();
        foreach (var @object in world.Objects.Values
                     .Where(candidate => candidate.ActivateOnOccupancy
                         && !candidate.Activated
                         && candidate.LevelId == player.LevelId
                         && candidate.PositionX == player.PositionX
                         && candidate.PositionY == player.PositionY
                         && HasRequiredActivatedLinkedObjects(candidate))
                     .OrderBy(candidate => candidate.EntityId)
                     .ToArray()) {
            var result = OperateObject(
                player,
                @object.EntityId,
                participants,
                requireAdjacent: false);
            if (!result.Succeeded)
                continue;
            events.AddRange(result.Events);
        }

        return events.Count == 0
            ? DiabloWorldIntentResult.Accepted
            : DiabloWorldIntentResult.AcceptedWith(events.ToArray());
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
        var destination = DeterministicGridCrawl.FindClosestValidPosition(
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
        player.MarkLevelVisited(player.LevelId);
        player.PositionX = destination.Value.X;
        player.PositionY = destination.Value.Y;
        EnsureLevelEntryState(player.LevelId);
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
        player.MarkLevelVisited(player.LevelId);
        player.PositionX = combatRules.RestartPositionX;
        player.PositionY = combatRules.RestartPositionY;
        EnsureLevelEntryState(player.LevelId);
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

    /** Adds a one-cell movement offset without allowing malformed coordinates to wrap. */
    private static bool TryOffsetCoordinate(int coordinate, int offset, out int result)
    {
        var candidate = (long)coordinate + offset;
        if (candidate is < int.MinValue or > int.MaxValue) {
            result = 0;
            return false;
        }
        result = (int)candidate;
        return true;
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

        if (!TryOffsetCoordinate(player.PositionX, intent.DirectionX, out var targetX)
            || !TryOffsetCoordinate(player.PositionY, intent.DirectionY, out var targetY))
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        if (!world.IsWalkable(player.LevelId, targetX, targetY)
            || world.IsOccupied(player.LevelId, targetX, targetY, players, player.EntityId)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }

        if (useNativePlayerActionTiming) {
            if (!DiabloPlayerActionLifecycle.CanStart(player, appliedTick))
                return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
            player.FacingDirection = (DiabloPlayerDirection)DeterministicGridDirections.FromAdjacentStep(
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

        player.FacingDirection = (DiabloPlayerDirection)DeterministicGridDirections.FromAdjacentStep(
            intent.DirectionX,
            intent.DirectionY);
        player.PositionX = targetX;
        player.PositionY = targetY;
        ExecuteOccupancyTriggers(player, players);
        var levelTrigger = ExecuteLevelTrigger(player, appliedTick, players);
        if (levelTrigger.Succeeded)
            return levelTrigger;
        return DiabloWorldIntentResult.Accepted;
    }

    private bool HasRequiredActivatedLinkedObjects(AuthoritativeWorldObject @object)
    {
        if (@object.RequiredActivatedLinkedObjects <= 0)
            return true;
        return world.Objects.Values.Count(candidate =>
                candidate.EntityId != @object.EntityId
                && candidate.QuestId == @object.QuestId
                && candidate.LevelId == @object.LevelId
                && candidate.Activated)
            >= @object.RequiredActivatedLinkedObjects;
    }

    /** Applies one server-owned deterministic path step toward a native walk target. */
    private DiabloWorldIntentResult MoveTo(
        AuthoritativePlayerState player,
        DiabloMoveToIntent intent,
        ulong appliedTick,
        IEnumerable<AuthoritativePlayerState> players)
    {
        if (!world.ContainsPosition(player.LevelId, intent.TargetX, intent.TargetY))
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        if (player.PositionX == intent.TargetX && player.PositionY == intent.TargetY)
            return DiabloWorldIntentResult.Accepted;

        var participants = players.ToArray();
        var targetOccupied = world.IsOccupied(
            player.LevelId,
            intent.TargetX,
            intent.TargetY,
            participants,
            player.EntityId);
        var targetDistance = DeterministicGridMetrics.ChebyshevDistance(
            intent.TargetX,
            intent.TargetY,
            player.PositionX,
            player.PositionY);
        if (targetOccupied && targetDistance <= 1)
            return DiabloWorldIntentResult.Accepted;

        var step = DeterministicGridNavigation.FindStep(
            player.PositionX,
            player.PositionY,
            intent.TargetX,
            intent.TargetY,
            maximumDepth: 100,
            isWalkable: (x, y) => world.IsWalkable(player.LevelId, x, y),
            isOccupied: (x, y) => world.IsOccupied(
                player.LevelId,
                x,
                y,
                participants,
                player.EntityId));
        if (!step.HasValue)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);

        return Move(
            player,
            new DiabloMoveIntent(
                step.Value.X - player.PositionX,
                step.Value.Y - player.PositionY),
            appliedTick,
            participants);
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
        player.MarkLevelVisited(player.LevelId);
        player.PositionX = destinationPositionX;
        player.PositionY = destinationPositionY;
        EnsureLevelEntryState(player.LevelId);
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

    /** Ensures external QUEST_INIT rows are active whenever authoritative state observes a route level. */
    internal void EnsureLevelEntryState(uint levelId)
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
            var entersOrdinaryRoute = routeLevel >= 0 && (uint)routeLevel == levelId;
            var entersActiveSetLevel = definition.SetLevel > 0
                && AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(
                    definition.SetLevel,
                    activeMap: true) == levelId;
            if (!entersOrdinaryRoute && !entersActiveSetLevel)
                continue;

            quest.Active = true;
            if (quest.RouteActivationVariable1 >= 0)
                quest.Variable1 = Math.Max(quest.Variable1, quest.RouteActivationVariable1);
        }
    }

    private DiabloWorldIntentResult PickupWorldItem(
        AuthoritativePlayerState player,
        uint itemEntityId,
        ulong appliedTick,
        IEnumerable<AuthoritativePlayerState> players,
        bool requireAdjacent = true,
        bool applyEffects = true)
    {
        if (!world.WorldItems.TryGetValue(itemEntityId, out var item)
            || item.LevelId != player.LevelId
            || (requireAdjacent
                && DeterministicGridMetrics.ChebyshevDistance(
                    item.PositionX,
                    item.PositionY,
                    player.PositionX,
                    player.PositionY) > 1)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        var itemState = ResolveWorldItemState(player, item);
        var targetCell = InventoryGridLayout.FindFirstPlacement(
            player.InventoryGrid,
            itemState.InventoryWidth,
            itemState.InventoryHeight);
        var participants = players as IReadOnlyList<AuthoritativePlayerState>
            ?? players.ToArray();
        if (player.HeldItem is not null
            && !CanDropHeldItemForPickup(player, participants)) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }
        if (DiabloGoldInventory.HasGoldIdentity(
                itemState,
                combatRules,
                player.Items)) {
            if (itemState.Value <= 0
                || itemState.Value > combatRules.DeathGoldMaximumStack
                || itemState.IdentifiedValue != itemState.Value
                || item.Price != checked((uint)itemState.Value)
                || !itemState.StatFlag
                || (ulong)player.Gold + item.Price > int.MaxValue
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
                if (player.HeldItem is not null)
                    DropHeldItemForPickup(player, participants);
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
                        itemState with {
                            Value = checked((int)remainder),
                            IdentifiedValue = checked((int)remainder),
                        });
                }
            } else {
                if (!applyEffects)
                    return DiabloWorldIntentResult.Accepted;
                if (player.HeldItem is not null)
                    DropHeldItemForPickup(player, participants);
                player.Gold += item.Price;
            }
            world.WorldItems.Remove(itemEntityId);
            return DiabloWorldIntentResult.Accepted;
        }

        if (!applyEffects)
            return DiabloWorldIntentResult.Accepted;

        if (player.HeldItem is not null)
            DropHeldItemForPickup(player, participants);

        if (targetCell < 0) {
            player.HeldItem = new OwnedStoreItem(
                0,
                0,
                item.ItemSeed,
                item.Price,
                appliedTick,
                itemState);
            world.WorldItems.Remove(itemEntityId);
            return DiabloWorldIntentResult.Accepted;
        }

        var inventoryIndex = player.Inventory.Count;
        player.Inventory.Add(new OwnedStoreItem(
            0,
            0,
            item.ItemSeed,
            item.Price,
            appliedTick,
            itemState));
        InventoryGridLayout.Fill(
            player.InventoryGrid,
            targetCell,
            inventoryIndex,
            itemState.InventoryWidth,
            itemState.InventoryHeight);
        world.WorldItems.Remove(itemEntityId);
        ApplyItemQuestEffects(itemState);
        ApplyItemCombinations(player, appliedTick);
        return DiabloWorldIntentResult.Accepted;
    }

    /** Validates the native pickup origin for a cursor-item replacement. */
    private bool CanDropHeldItemForPickup(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        if (player.HeldItem is null)
            return true;
        var targetX = player.FuturePositionX;
        var targetY = player.FuturePositionY;
        if (!world.IsWalkable(player.LevelId, targetX, targetY)
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
            return false;
        }
        return true;
    }

    /** Drops the cursor item at the native pickup origin before replacing it. */
    private void DropHeldItemForPickup(
        AuthoritativePlayerState player,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        var heldItem = player.HeldItem
            ?? throw new InvalidOperationException("A cursor item is required for pickup replacement.");
        if (!CanDropHeldItemForPickup(player, players))
            throw new InvalidOperationException("The authoritative pickup origin cannot hold a dropped cursor item.");
        var targetX = player.FuturePositionX;
        var targetY = player.FuturePositionY;

        var entityId = AllocateWorldItemEntityId(players);
        var droppedItem = new AuthoritativeWorldItem(
            entityId,
            player.LevelId,
            targetX,
            targetY,
            heldItem.ItemSeed,
            heldItem.Price,
            heldItem.State);
        world.WorldItems.Add(entityId, droppedItem);
        player.HeldItem = null;
        ApplyWorldItemAnchorQuest(droppedItem);
    }

    private AuthoritativeItemState ResolveWorldItemState(
        AuthoritativePlayerState player,
        AuthoritativeWorldItem item)
    {
        var state = items?.ResolveAuthoredMetadata(item.State, item.ItemSeed) ?? item.State;
        if (item.Price <= int.MaxValue
            && DiabloGoldInventory.HasGoldIdentity(
                state,
                combatRules,
                player.Items ?? items)) {
            state = state with {
                Value = state.Value == 0 ? checked((int)item.Price) : state.Value,
                IdentifiedValue = state.IdentifiedValue == 0
                    ? checked((int)item.Price)
                    : state.IdentifiedValue,
            };
        }
        return state;
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
        return DropItem(player, null, targetX, targetY, players);
    }

    private DiabloWorldIntentResult DropItem(
        AuthoritativePlayerState player,
        DiabloPlayerItemReference? sourceReference,
        int targetX,
        int targetY,
        IReadOnlyList<AuthoritativePlayerState> players)
    {
        OwnedStoreItem? sourceItem;
        if (sourceReference is { } reference) {
            if (reference.Location == DiabloPlayerItemLocation.Stash
                || player.HeldItem is not null
                || !DiabloPlayerItemAccess.TryGet(player, reference, out sourceItem)) {
                return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
            }
        } else if (player.HeldItem is { } heldItem) {
            sourceItem = heldItem;
        } else {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        }
        var originX = player.FuturePositionX;
        var originY = player.FuturePositionY;
        if (!world.ContainsPosition(player.LevelId, targetX, targetY))
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        if (DeterministicGridMetrics.ChebyshevDistance(
                targetX,
                targetY,
                originX,
                originY) > 1
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
        var worldItem = new AuthoritativeWorldItem(
            entityId,
            player.LevelId,
            targetX,
            targetY,
            sourceItem!.ItemSeed,
            sourceItem.Price,
            sourceItem.State);
        world.WorldItems.Add(entityId, worldItem);
        if (sourceReference is { } source) {
            DiabloPlayerItemAccess.Remove(player, source);
            if (source.Location == DiabloPlayerItemLocation.Equipment)
                player.RecalculateEquipmentResources();
        } else {
            player.HeldItem = null;
        }
        ApplyWorldItemAnchorQuest(worldItem);
        return DiabloWorldIntentResult.Accepted;
    }

    /** Completes a content-authored item-placement quest after the item is persisted in world state. */
    private void ApplyWorldItemAnchorQuest(AuthoritativeWorldItem droppedItem)
    {
        if (questTypes is null)
            return;

        foreach (var quest in world.Quests.Values) {
            if (quest.Unavailable
                || !quest.Active
                || quest.Completed
                || !questTypes.TryGet(quest.QuestId, out var questType)) {
                continue;
            }
            if (gameMode == DiabloGameMode.Multiplayer && questType.SinglePlayerOnly)
                continue;
            if (questType.WorldItemAnchorPositionX is not { } anchorX
                || questType.WorldItemAnchorPositionY is not { } anchorY
                || quest.LevelId != droppedItem.LevelId
                || anchorX != droppedItem.PositionX
                || anchorY != droppedItem.PositionY) {
                continue;
            }

            quest.Progress = quest.RequiredProgress;
            quest.Completed = true;
            quest.Active = false;
            quest.Logged = true;
        }
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
        return worldItemEntityIds.Allocate(entityId =>
            players.Any(player => player.EntityId == entityId)
            || world.IsEntityIdOccupied(entityId));
    }

    private DiabloWorldIntentResult OperateObject(
        AuthoritativePlayerState player,
        uint objectEntityId,
        IReadOnlyList<AuthoritativePlayerState> participants,
        bool requireAdjacent = true,
        bool applyEffects = true)
    {
        if (!participants.Contains(player))
            participants = participants.Append(player).ToArray();
        if (!world.Objects.TryGetValue(objectEntityId, out var @object)
            || @object.Activated
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
        if (@object.QuestTransitionKind != AuthoritativeObjectQuestTransitionKind.None
            && (!world.Quests.TryGetValue(@object.QuestId, out var transitionQuest)
                || transitionQuest.Unavailable
                || !IsQuestAtPlayerLevel(transitionQuest, player.LevelId)
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
        if (!CanApplyObjectSpellEffect(@object, player, participants))
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        if (!applyEffects)
            return DiabloWorldIntentResult.Accepted;

        if (@object.EffectKind == AuthoritativeObjectEffectKind.QuestSequence)
            return ApplyQuestSequence(player, @object, participants);
        if (@object.EffectKind == AuthoritativeObjectEffectKind.QuestItemCompletion)
            return ApplyQuestItemCompletion(player, @object);

        @object.Activated = true;
        @object.AnimationFrame = @object.ActivatedAnimationFrame;
        if (@object.QuestId != 0 && world.Quests.TryGetValue(@object.QuestId, out var quest)
            && @object.QuestTransitionKind == AuthoritativeObjectQuestTransitionKind.None
            && !quest.Unavailable
            && !quest.Completed && IsQuestAtPlayerLevel(quest, player.LevelId)) {
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
        uint playerLevelId)
    {
        if (quest.LevelId == 0 || quest.LevelId == playerLevelId)
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
        world.WorldItems[entityId] = new AuthoritativeWorldItem(
            entityId,
            rewardLevelId,
            rewardX,
            rewardY,
            itemSeed,
            checked((uint)Math.Max(0, rewardState.IdentifiedValue)),
            rewardState);
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
            || DeterministicGridMetrics.ChebyshevDistance(
                @object.PositionX,
                @object.PositionY,
                player.PositionX,
                player.PositionY) > 1) {
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
        return (!requireWalkable || world.IsWalkable(levelId, positionX, positionY))
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
        world.WorldItems[entityId] = new AuthoritativeWorldItem(
            entityId,
            target.LevelId,
            rewardX,
            rewardY,
            itemSeed,
            checked((uint)Math.Max(0, rewardState.IdentifiedValue)),
            rewardState);
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
            world.WorldItems[entityId] = new AuthoritativeWorldItem(
                entityId,
                target.LevelId,
                rewardX,
                rewardY,
                itemSeed,
                0,
                rewardState);
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
            world.WorldItems[entityId] = new AuthoritativeWorldItem(
                entityId,
                target.LevelId,
                rewardX,
                rewardY,
                itemSeed,
                checked((uint)Math.Max(0, rewardState.IdentifiedValue)),
                rewardState);
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
            world.WorldItems[entityId] = new AuthoritativeWorldItem(
                entityId,
                quest.TurnInLevelId,
                quest.TurnInRewardPositionX,
                quest.TurnInRewardPositionY,
                itemSeed,
                0,
                rewardState);
        } else if (quest.TurnInRewardItemId != 0) {
            var rewardState = items.Generate(
                quest.TurnInRewardItemId,
                itemSeed,
                quest.TurnInRewardItemLevel);
            world.WorldItems[entityId] = new AuthoritativeWorldItem(
                entityId,
                quest.TurnInLevelId,
                quest.TurnInRewardPositionX,
                quest.TurnInRewardPositionY,
                itemSeed,
                checked((uint)Math.Max(0, rewardState.IdentifiedValue)),
                rewardState);
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
            world.WorldItems[entityId] = new AuthoritativeWorldItem(
                entityId,
                quest.TurnInLevelId,
                quest.TurnInCompletionRewardPositionX,
                quest.TurnInCompletionRewardPositionY,
                itemSeed,
                0,
                rewardState);
        } else if (quest.TurnInCompletionRewardItemId != 0) {
            var rewardState = items.Generate(
                quest.TurnInCompletionRewardItemId,
                itemSeed,
                quest.TurnInCompletionRewardItemLevel);
            world.WorldItems[entityId] = new AuthoritativeWorldItem(
                entityId,
                quest.TurnInLevelId,
                quest.TurnInCompletionRewardPositionX,
                quest.TurnInCompletionRewardPositionY,
                itemSeed,
                checked((uint)Math.Max(0, rewardState.IdentifiedValue)),
                rewardState);
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
                world.WorldItems[entityId] = new AuthoritativeWorldItem(
                    entityId,
                    quest.TurnInLevelId,
                    quest.TurnInCompletionRewardPositionX,
                    quest.TurnInCompletionRewardPositionY,
                    itemSeed,
                    0,
                    rewardState);
            }
            if (quest.TurnInCompletionRewardItemId != 0) {
                var itemSeed = AllocateGoldStackSeed();
                var rewardState = items.Generate(
                    quest.TurnInCompletionRewardItemId,
                    itemSeed,
                    quest.TurnInCompletionRewardItemLevel);
                var entityId = AllocateWorldItemEntityId(participants);
                world.WorldItems[entityId] = new AuthoritativeWorldItem(
                    entityId,
                    quest.TurnInLevelId,
                    quest.TurnInCompletionRewardPositionX,
                    quest.TurnInCompletionRewardPositionY,
                    itemSeed,
                    checked((uint)Math.Max(0, rewardState.IdentifiedValue)),
                    rewardState);
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
                world.WorldItems[entityId] = new AuthoritativeWorldItem(
                    entityId,
                    quest.TurnInLevelId,
                    quest.TurnInRewardPositionX,
                    quest.TurnInRewardPositionY,
                    itemSeed,
                    checked((uint)Math.Max(0, rewardState.IdentifiedValue)),
                    rewardState);
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
            world.WorldItems[entityId] = new AuthoritativeWorldItem(
                entityId,
                quest.TurnInLevelId,
                quest.TurnInRewardPositionX,
                quest.TurnInRewardPositionY,
                itemSeed,
                0,
                rewardState);
        }
        if (quest.TurnInRewardItemId != 0) {
            var itemSeed = AllocateGoldStackSeed();
            var rewardState = items.Generate(
                quest.TurnInRewardItemId,
                itemSeed,
                quest.TurnInRewardItemLevel);
            var entityId = AllocateWorldItemEntityId(participants);
            world.WorldItems[entityId] = new AuthoritativeWorldItem(
                entityId,
                quest.TurnInLevelId,
                quest.TurnInRewardPositionX,
                quest.TurnInRewardPositionY,
                itemSeed,
                checked((uint)Math.Max(0, rewardState.IdentifiedValue)),
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

    private static DiabloWorldIntentResult AllocateAttribute(
        AuthoritativePlayerState player,
        DiabloAllocateAttributeIntent allocation)
    {
        if (!Enum.IsDefined(allocation.Attribute) || allocation.Amount == 0)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.Malformed);
        if (allocation.Amount > player.StatPoints)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InsufficientResources);
        return player.AllocateAttribute(allocation.Attribute, allocation.Amount)
            ? DiabloWorldIntentResult.Accepted
            : DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
    }

}

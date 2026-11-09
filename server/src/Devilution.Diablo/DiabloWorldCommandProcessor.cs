using System.Buffers;
using Devilution.Domain.Inventory;
using Devilution.Engine;

namespace Devilution.Diablo;

/** Applies non-combat world intents without transport or host dependencies. */
public sealed partial class DiabloWorldCommandProcessor
{
    private const int HealerTownerId = 1;

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
    private readonly AuthoritativeObjectItemGateCatalog? objectItemGates;
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
    private readonly AuthoritativeTownerGreetingCatalog? townerGreetings;
    private readonly AuthoritativeQuestRouteMessageCatalog? questRouteMessages;
    private readonly List<DiabloGameplayEvent> pendingEntryEvents = [];
    private Func<int, AuthoritativeSetLevelObjectOrigin?>? setLevelOriginResolver;
    private Action<AuthoritativePlayerState, uint, uint>? nativeStoreTransitionInvalidator;
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
        AuthoritativeExperienceCatalog? experience = null,
        AuthoritativeTownerGreetingCatalog? townerGreetings = null,
        AuthoritativeObjectItemGateCatalog? objectItemGates = null,
        AuthoritativeQuestRouteMessageCatalog? questRouteMessages = null)
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
        this.objectItemGates = objectItemGates;
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
        this.townerGreetings = townerGreetings;
        this.questRouteMessages = questRouteMessages;
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

    internal void SetNativeStoreTransitionInvalidator(
        Action<AuthoritativePlayerState, uint, uint> invalidator)
    {
        ArgumentNullException.ThrowIfNull(invalidator);
        nativeStoreTransitionInvalidator = invalidator;
    }

    /** Captures allocator cursors without inferring them from surviving entities. */
    internal DiabloWorldCommandAllocatorState CaptureCohortAllocators()
    {
        return new DiabloWorldCommandAllocatorState(
            worldItemEntityIds.NextId,
            authoredProjectileEntityIds.NextId,
            generatedMonsterEntityIds.NextId);
    }

    /** Restores exact allocator cursors, including zero exhaustion. */
    internal void RestoreCohortAllocators(DiabloWorldCommandAllocatorState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        worldItemEntityIds.RestoreNextId(state.NextWorldItemEntityId);
        authoredProjectileEntityIds.RestoreNextId(state.NextAuthoredProjectileEntityId);
        generatedMonsterEntityIds.RestoreNextId(state.NextGeneratedMonsterEntityId);
    }

    /** Rejects a cohort capture while entry events are still being assembled. */
    internal void AssertCohortCaptureBoundary()
    {
        if (pendingEntryEvents.Count != 0)
            throw new InvalidOperationException(
                "A cohort cannot be captured while world entry events are pending.");
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

        pendingEntryEvents.Clear();
        EnsureLevelEntryState(player.LevelId, player.EntityId, emitDialogue: true);

        if (intent is DiabloRestartTownIntent) {
            var restart = AttachPendingEntryEvents(RestartTown(player, appliedTick));
            if (restart.Succeeded) {
                player.NativeWalkPath = null;
                player.NativeQueuedRangedAttack = null;
            }
            return restart;
        }

        if (useNativePlayerActionTiming
            && (player.Life <= 0
                || player.CombatMode is DiabloPlayerCombatMode.Death
                    or DiabloPlayerCombatMode.NewLevel
                    or DiabloPlayerCombatMode.Quit)) {
            pendingEntryEvents.Clear();
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }

        var result = intent switch {
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
            DiabloDisarmTrapIntent disarm => DisarmTrap(player, disarm.ObjectEntityId, players.ToArray()),
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
            DiabloTalkToTownerIntent talk => TalkToTowner(player, talk.TownerId),
            DiabloSetFriendlyModeIntent mode => SetFriendlyMode(player, mode.FriendlyMode),
            DiabloAllocateAttributeIntent allocation => AllocateAttribute(player, allocation),
            _ => DiabloWorldIntentResult.Rejected(DiabloRuleFailure.Malformed),
        };
        if (result.Succeeded && ReplacesNativeWalkPath(intent))
            player.NativeWalkPath = null;
        if (result.Succeeded && (ReplacesNativeWalkPath(intent) || intent is DiabloMoveToIntent))
            player.NativeQueuedRangedAttack = null;
        return AttachPendingEntryEvents(result);
    }

    private static bool ReplacesNativeWalkPath(DiabloWorldIntent intent)
        => intent is DiabloMoveIntent
            or DiabloMoveToIntent { FollowPath: false }
            or DiabloUsePortalIntent
            or DiabloPickupWorldItemIntent
            or DiabloOperateObjectIntent
            or DiabloDisarmTrapIntent
            or DiabloTalkToQuestIntent
            or DiabloTurnInQuestIntent
            or DiabloTalkToTownerIntent;

    private DiabloWorldIntentResult AttachPendingEntryEvents(
        DiabloWorldIntentResult result)
    {
        if (!result.Succeeded || pendingEntryEvents.Count == 0) {
            pendingEntryEvents.Clear();
            return result;
        }
        var events = result.Events.Concat(pendingEntryEvents).ToArray();
        pendingEntryEvents.Clear();
        return DiabloWorldIntentResult.AcceptedWith(events);
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
            || !world.HasTargetLineOfSight(
                player.LevelId,
                player.PositionX,
                player.PositionY,
                targetX,
                targetY,
                player.LightRadius)) {
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

        return AttachPendingEntryEvents(ApplyWarpTarget(
            player,
            warp,
            appliedTick,
            players,
            applyEffects,
            searchRadius));
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

        // Native ProcessTownPortal runs before CheckTriggers and selects the
        // portal before a same-tile stair. Keep the endpoint available for the
        // explicit server portal intent, even if its eventual destination is busy.
        if (world.Portals.Values.Any(portal => portal.OwnerEntityId != 0
                && portal.RemainingTicks > 0 && portal.Bidirectional
                && portal.SourceLevelId != combatRules.TownLevelId
                && portal.DestinationLevelId == combatRules.TownLevelId
                && (portal.SourceLevelId == player.LevelId
                    && portal.SourcePositionX == player.PositionX
                    && portal.SourcePositionY == player.PositionY
                    || portal.DestinationLevelId == player.LevelId
                    && portal.DestinationPositionX == player.PositionX
                    && portal.DestinationPositionY == player.PositionY))) {
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
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
        // Native multiplayer CheckTriggers turns a low-level player back one tile south (CMD_WALKXY).
        if (player.CharacterLevel < warp.RequiredCharacterLevel) {
            if (applyEffects)
                CaptureNativeDestinationPath(player, new DiabloMoveToIntent(
                    player.PositionX, player.PositionY + 1, FollowPath: true), appliedTick, players);
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        }

        return AttachPendingEntryEvents(ApplyWarpTarget(
            player,
            warp,
            appliedTick,
            players,
            applyEffects,
            searchRadius));
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

        if (!world.AllowsNativePopulationLevel(warp.SourceLevelId)
            || !world.CanEnterNativePopulationLevel(warp.TargetLevelId))
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        var participants = players.ToArray();
        // Commit follows in this call with only read-only checks between, so nothing can go stale.
        var entry = world.PrepareNativeProgressionEntry(player, warp.TargetLevelId, participants,
            items!, spells!, warp.FirstEntry, verifyAtCommit: false);
        var destinationWorld = entry?.Admission.Floor ?? world;
        var destination = DeterministicGridCrawl.FindClosestValidPosition(
            warp.TargetX,
            warp.TargetY,
            0,
            searchRadius,
            (candidateX, candidateY) =>
                destinationWorld.IsWalkable(warp.TargetLevelId, candidateX, candidateY)
                && !destinationWorld.IsMissileBlocked(warp.TargetLevelId, candidateX, candidateY)
                && !destinationWorld.IsOccupied(
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

        if (entry is { } first) {
            world.CommitNativeProgressionEntry(first.Admission, first.Entry);
            random.Restore(first.Admission.FinalRandomState);
        }
        var sourceLevelId = player.LevelId;
        world.UnlockNativeTownWarp(warp.UnlocksTownWarp);
        player.NativeWalkPath = null;
        player.NativeQueuedRangedAttack = null;
        player.LevelId = warp.TargetLevelId;
        player.MarkLevelVisited(player.LevelId);
        player.PositionX = destination.Value.X;
        player.PositionY = destination.Value.Y;
        EnsureLevelEntryState(player.LevelId, player.EntityId, emitDialogue: true);
        player.ManaShieldActive = false;
        RemoveLevelTransitionStatuses(player);
        nativeStoreTransitionInvalidator?.Invoke(
            player,
            sourceLevelId,
            player.LevelId);
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
        EnsureLevelEntryState(player.LevelId, player.EntityId, emitDialogue: true);
        player.Life = Math.Min(
            player.LifeMaximum,
            checked(combatRules.RestartLife * combatRules.RestartLifeScale));
        player.Mana = 0;
        player.ResetNativeCurrentResourceBases();
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
        IEnumerable<AuthoritativePlayerState> players,
        bool initialNativeWalkStep = false)
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
            // A Stand CheckNewPath starts at animation frame -1; a chained
            // completion-loop StartWalk begins at frame 0 on the same tick.
            var endTick = checked(appliedTick + (ulong)animation.WalkingFrames
                + (initialNativeWalkStep ? 1UL : 0UL));
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
        var occupancyResult = ExecuteOccupancyTriggers(player, players);
        var levelTrigger = ExecuteLevelTrigger(player, appliedTick, players);
        if (levelTrigger.Succeeded) {
            if (occupancyResult.Events.Count == 0)
                return levelTrigger;
            return DiabloWorldIntentResult.AcceptedWith(
                occupancyResult.Events.Concat(levelTrigger.Events).ToArray());
        }
        return occupancyResult;
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
        if (intent.FollowPath)
            return CaptureNativeDestinationPath(player, intent, appliedTick, players);

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

        // Ordinary walk targets include their endpoint (native makePath endspace=true).
        // Keep adjacent-goal navigation for blocked interaction targets and longer approaches.
        if (targetDistance == 1 && !targetOccupied && world.IsWalkable(player.LevelId, intent.TargetX, intent.TargetY)) {
            return Move(player, new DiabloMoveIntent(
                intent.TargetX - player.PositionX, intent.TargetY - player.PositionY), appliedTick, participants);
        }

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

    private DiabloWorldIntentResult CaptureNativeDestinationPath(
        AuthoritativePlayerState player,
        DiabloMoveToIntent intent,
        ulong appliedTick,
        IEnumerable<AuthoritativePlayerState> players)
    {
        if (!world.SupportsNativeTargetCellPathing || !useNativePlayerActionTiming)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
        if (!world.ContainsPosition(player.LevelId, intent.TargetX, intent.TargetY)
            || (uint)player.FuturePositionX > byte.MaxValue
            || (uint)player.FuturePositionY > byte.MaxValue
            || (uint)intent.TargetX > byte.MaxValue
            || (uint)intent.TargetY > byte.MaxValue)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        if (player.Life <= 0 || player.CombatMode is DiabloPlayerCombatMode.Death
                or DiabloPlayerCombatMode.NewLevel or DiabloPlayerCombatMode.Quit)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);

        var participants = players as IReadOnlyList<AuthoritativePlayerState>
            ?? players.ToArray();
        var startX = player.FuturePositionX;
        var startY = player.FuturePositionY;
        if (!world.ContainsPosition(player.LevelId, startX, startY))
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
        if (startX == intent.TargetX && startY == intent.TargetY) {
            player.NativeWalkPath = null;
            return DiabloWorldIntentResult.Accepted;
        }

        // Occupancy is immutable during this command. A* revisits neighboring cells;
        // evaluate each native uint8 coordinate once and return the buffer before leaving.
        var validity = ArrayPool<byte>.Shared.Rent(256 * 256);
        validity.AsSpan(0, 256 * 256).Clear();
        int[] directions;
        try {
            directions = AuthoritativeNativePlayerPathfinder.FindPath(
                startX,
                startY,
                intent.TargetX,
                intent.TargetY,
                AuthoritativeNativePlayerPathfinder.MaximumPathLength,
                (x, y) => {
                    var cell = y * 256 + x;
                    if (validity[cell] == 0)
                        validity[cell] = world.ContainsPosition(player.LevelId, x, y)
                            && world.IsWalkable(player.LevelId, x, y)
                            && !world.IsOccupied(player.LevelId, x, y, participants, player.EntityId) ? (byte)1 : (byte)2;
                    return validity[cell] == 1;
                },
                (fromX, fromY, toX, toY) => {
                    var dx = toX - fromX;
                    var dy = toY - fromY;
                    if (dx == 0 || dy == 0)
                        return true;
                    return world.IsWalkable(player.LevelId, fromX + dx, fromY)
                        && world.IsWalkable(player.LevelId, fromX, fromY + dy);
                });
        } finally { ArrayPool<byte>.Shared.Return(validity); }
        if (directions.Length == 0)
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);

        player.NativeWalkPath = new DiabloNativeWalkPathState(player.LevelId, directions);
        return DiabloWorldIntentResult.Accepted;
    }

    /** Starts queued native directions at the normal player scheduling boundary. */
    internal DiabloWorldIntentResult AdvanceNativeWalkContinuation(
        AuthoritativePlayerState player,
        ulong tick,
        IReadOnlyList<AuthoritativePlayerState> players,
        int maximumSchedulingAttempts = 1)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(players);
        if (maximumSchedulingAttempts is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(maximumSchedulingAttempts));
        if (!world.SupportsNativeTargetCellPathing || !useNativePlayerActionTiming)
            return DiabloWorldIntentResult.Accepted;
        if (player.NativeWalkPath is not { } path)
            return DiabloWorldIntentResult.Accepted;
        if (path.LevelId != player.LevelId || player.Life <= 0
            || player.CombatMode is DiabloPlayerCombatMode.Death
                or DiabloPlayerCombatMode.NewLevel or DiabloPlayerCombatMode.Quit) {
            player.NativeWalkPath = null;
            return DiabloWorldIntentResult.Accepted;
        }
        if (player.CurrentAction is not null || player.CombatMode != DiabloPlayerCombatMode.Stand)
            return DiabloWorldIntentResult.Accepted;

        var result = DiabloWorldIntentResult.Accepted;
        for (var attempt = 0; attempt < maximumSchedulingAttempts; attempt++) {
            if (player.NativeWalkPath is not { } remaining || remaining.Directions.Count == 0)
                return result;

            var directions = remaining.Directions;
            var direction = directions[0];
            var displacement = AuthoritativeNativePlayerPathfinder.GetDisplacement(direction);
            player.NativeWalkPath = directions.Count == 1
                ? null
                : new DiabloNativeWalkPathState(remaining.LevelId, directions.Skip(1).ToArray());

            // Native CheckNewPath consumes a direction even when PlrDirOK blocks
            // it. The completion do/while may call CheckNewPath once more.
            if (!NativeWalkCornerAllowsDirection(player, direction)) {
                result = DiabloWorldIntentResult.Rejected(DiabloRuleFailure.InvalidTarget);
                continue;
            }

            result = Move(
                player,
                new DiabloMoveIntent(displacement.X, displacement.Y),
                tick,
                players,
                initialNativeWalkStep: maximumSchedulingAttempts == 1 || attempt > 0);
            if (result.Succeeded)
                return result;
        }
        return result;
    }

    private bool NativeWalkCornerAllowsDirection(AuthoritativePlayerState player, int direction)
    {
        // Player::PlrDirOK has these two additional static-corner checks.
        // Other directions validate only their destination tile.
        var x = player.FuturePositionX;
        var y = player.FuturePositionY;
        return direction switch {
            6 => IsNativeWalkCornerTileWalkable(player.LevelId, x, y, 1, 0), // East: test southeast.
            8 => IsNativeWalkCornerTileWalkable(player.LevelId, x, y, 0, 1), // West: test southwest.
            _ => true,
        };
    }

    private bool IsNativeWalkCornerTileWalkable(uint levelId, int x, int y, int offsetX, int offsetY)
    {
        var candidateX = (long)x + offsetX;
        var candidateY = (long)y + offsetY;
        return candidateX is >= int.MinValue and <= int.MaxValue
            && candidateY is >= int.MinValue and <= int.MaxValue
            && world.IsWalkable(levelId, (int)candidateX, (int)candidateY);
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
        if (!world.AllowsNativePopulationLevel(player.LevelId)
            || !world.AllowsNativePopulationLevel(portal.SourceLevelId)
            || !world.AllowsNativePopulationLevel(portal.DestinationLevelId))
            return DiabloWorldIntentResult.Rejected(DiabloRuleFailure.NotAllowed);
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

        var closesOwnerTownPortal = useDestinationEndpoint
            && !useSourceEndpoint
            && player.LevelId == combatRules.TownLevelId
            && destinationLevelId != combatRules.TownLevelId
            && portal.OwnerEntityId == player.EntityId;
        var sourceLevelId = player.LevelId;
        player.LevelId = destinationLevelId;
        player.MarkLevelVisited(player.LevelId);
        player.PositionX = destinationPositionX;
        player.PositionY = destinationPositionY;
        EnsureLevelEntryState(player.LevelId, player.EntityId, emitDialogue: true);
        player.ManaShieldActive = false;
        RemoveLevelTransitionStatuses(player);
        nativeStoreTransitionInvalidator?.Invoke(
            player,
            sourceLevelId,
            player.LevelId);
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
        // Native GetPortalLevel deactivates only the owner's accepted return
        // from town. Other players and rejected transitions retain the portal.
        if (closesOwnerTownPortal)
            world.Portals.Remove(portalId);
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
    internal void EnsureLevelEntryState(
        uint levelId,
        uint sourceEntityId = 0,
        bool emitDialogue = false)
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
            quest.Logged = true;
            if (questRouteMessages is not null
                && questRouteMessages.TryGet(quest.QuestId, out var routeMessage)
                && routeMessage.LevelId == levelId) {
                quest.MessageId = routeMessage.MessageId;
                if (emitDialogue) {
                    pendingEntryEvents.Add(new DiabloGameplayEvent(
                        DiabloGameplayEventKind.Dialogue,
                        sourceEntityId,
                        0,
                        0) {
                        MessageId = routeMessage.MessageId,
                        QuestId = quest.QuestId,
                    });
                }
            }
            if (quest.RouteActivationVariable1 >= 0)
                quest.Variable1 = Math.Max(quest.Variable1, quest.RouteActivationVariable1);
        }
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

using Devilution.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Main-thread authoritative projection consumed by Godot presentation nodes. */
public sealed class AuthoritativeClientModel
{
    private const int NativeMaximumWidth = AuthoritativeProtocolLimits.NativeMaximumWidth;
    private const int NativeMaximumHeight = AuthoritativeProtocolLimits.NativeMaximumHeight;
    private const int NativeMaximumMegaWidth = AuthoritativeProtocolLimits.NativeMaximumMegaWidth;
    private const int NativeMaximumMegaHeight = AuthoritativeProtocolLimits.NativeMaximumMegaHeight;
    // Object snapshots carry converted _object_id values; geometry carries raw
    // DUN conversion indices and therefore uses a separate compatibility bound.
    private const uint NativeMaximumObjectId = AuthoritativeProtocolLimits.NativeMaximumObjectId;
    private const int NativeMaximumPlayerCount = AuthoritativeProtocolLimits.NativeMaximumPlayerCount;
    private const int NativeMaximumInventoryCount = AuthoritativeProtocolLimits.NativeMaximumInventoryCount;
    private const int NativeMaximumEquipmentCount = AuthoritativeProtocolLimits.NativeMaximumEquipmentCount;
    private const int NativeMaximumBeltCount = AuthoritativeProtocolLimits.NativeMaximumBeltCount;
    private const int NativeMaximumInventoryGridCount = AuthoritativeProtocolLimits.NativeMaximumInventoryGridCount;
    private const int NativeInventoryWidth = AuthoritativeProtocolLimits.NativeInventoryWidth;
    private const int NativeInventoryHeight = AuthoritativeProtocolLimits.NativeInventoryHeight;
    private const int NativeMaximumObjectCount = AuthoritativeProtocolLimits.NativeMaximumObjectCount;
    private const int NativeMaximumItemCount = AuthoritativeProtocolLimits.NativeMaximumItemCount;
    private const int NativeMaximumMonsterCount = AuthoritativeProtocolLimits.NativeMaximumMonsterCount;
    private const int NativeMaximumPortalCount = AuthoritativeProtocolLimits.NativeMaximumPortalCount;
    private const int NativeMaximumWarpTriggerCount = AuthoritativeProtocolLimits.NativeMaximumWarpTriggerCount;
    private const uint NativeMaximumDunObjectLayerId = AuthoritativeProtocolLimits.NativeMaximumDunObjectLayerId;
    private const uint NativeMaximumDunPieceId = AuthoritativeProtocolLimits.NativeMaximumDunPieceId;
    private const uint NativeMaximumQuestId = AuthoritativeProtocolLimits.NativeMaximumQuestId;

    private const int MaxPredictedMoves = 16;
    private const int MaxRecentEvents = 256;
    private const int MaximumEventBatchCount = AuthoritativeProtocolLimits.MaximumEventBatchCount;
    private const int MaximumWorldMutationCount = AuthoritativeProtocolLimits.MaximumWorldMutationCount;
    private const int MaximumSnapshotQuestCount = AuthoritativeProtocolLimits.MaximumSnapshotQuestCount;
    private const int MaximumSetLevelObjectOriginCount = AuthoritativeProtocolLimits.MaximumSetLevelObjectOriginCount;
    private const int MaximumMaterializedProceduralLevelCount = AuthoritativeProtocolLimits.MaximumMaterializedProceduralLevelCount;
    private const int MaximumNativeSetLevelId = AuthoritativeProtocolLimits.MaximumNativeSetLevelId;
    private const int MaximumMonsterLeaderRelation = AuthoritativeProtocolLimits.MaximumMonsterLeaderRelation;
    private const uint MaximumStableSpellId = AuthoritativeProtocolLimits.MaximumStableSpellId;
    private const uint MaximumNativeSpellLevel = AuthoritativeProtocolLimits.MaximumNativeSpellLevel;
    private const int MaximumMemorizedSpellCount = AuthoritativeProtocolLimits.MaximumMemorizedSpellCount;
    private const uint MaximumNativeCharacterLevel = AuthoritativeProtocolLimits.MaximumNativeCharacterLevel;
    private const uint MaximumNativeDiabloKillLevel = AuthoritativeProtocolLimits.MaximumNativeDiabloKillLevel;
    private const uint MaximumNativeReflectCharges = AuthoritativeProtocolLimits.MaximumNativeReflectCharges;
    private const int MinimumNativeLightRadius = AuthoritativeProtocolLimits.MinimumNativeLightRadius;
    private const int MaximumNativeLightRadius = AuthoritativeProtocolLimits.MaximumNativeLightRadius;
    private const int MaximumProjectileDamageType = AuthoritativeProtocolLimits.MaximumProjectileDamageType;
    private const int MaximumProjectileBehavior = AuthoritativeProtocolLimits.MaximumProjectileBehavior;
    private const int MaximumProjectilePresentationKind = AuthoritativeProtocolLimits.MaximumProjectilePresentationKind;
    private const int MaximumNativeProjectileVisual = AuthoritativeProtocolLimits.MaximumNativeProjectileVisual;
    private const int MaximumStatusEffectBehavior = AuthoritativeProtocolLimits.MaximumStatusEffectBehavior;
    private const int MaximumStatusEffectCount = AuthoritativeProtocolLimits.MaximumStatusEffectCount;
    private const int MaximumItemPresentationKind = AuthoritativeProtocolLimits.MaximumItemPresentationKind;
    private const int MaximumItemSpellSourceKind = AuthoritativeProtocolLimits.MaximumItemSpellSourceKind;
    private const int MaximumItemActionKind = AuthoritativeProtocolLimits.MaximumItemActionKind;
    private const int MaximumSpellTargetingKind = AuthoritativeProtocolLimits.MaximumSpellTargetingKind;
    private const int MaximumProjectilePhase = AuthoritativeProtocolLimits.MaximumProjectilePhase;
    private const int MaximumMonsterDialogueState = AuthoritativeProtocolLimits.MaximumMonsterDialogueState;
    private const int MaximumNativeMonsterDirection = AuthoritativeProtocolLimits.MaximumNativeMonsterDirection;
    private const int MaximumNativeMonsterDeathAnimationTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterDeathAnimationTicks;
    private const uint MaximumNativeMonsterExperienceReward = AuthoritativeProtocolLimits.MaximumNativeMonsterExperienceReward;
    private const int MaximumNativeMonsterToHit = AuthoritativeProtocolLimits.MaximumNativeMonsterToHit;
    private const int MaximumNativeMonsterIntelligence = AuthoritativeProtocolLimits.MaximumNativeMonsterIntelligence;
    private const int MaximumNativeMonsterFallenStandAnimationFrame = AuthoritativeProtocolLimits.MaximumNativeMonsterFallenStandAnimationFrame;
    private const int MaximumNativeMonsterFallenStandAnimationTick = AuthoritativeProtocolLimits.MaximumNativeMonsterFallenStandAnimationTick;
    private const int MaximumNativeMonsterFallenRallyTicks = AuthoritativeProtocolLimits.MaximumNativeMonsterFallenRallyTicks;
    private const int MinimumNativeMonsterResistance = AuthoritativeProtocolLimits.MinimumNativeMonsterResistance;
    private const int MaximumNativeMonsterResistance = AuthoritativeProtocolLimits.MaximumNativeMonsterResistance;
    private const int MaximumIdentityBytes = AuthoritativeProtocolLimits.MaximumIdentityBytes;
    private const int MaximumObjectEffectKind = AuthoritativeProtocolLimits.MaximumObjectEffectKind;
    private const int MaximumObjectPresentationKind = AuthoritativeProtocolLimits.MaximumObjectPresentationKind;
    private const int MaximumObjectQuestTransitionKind = AuthoritativeProtocolLimits.MaximumObjectQuestTransitionKind;
    private const int MaximumObjectRewardKind = AuthoritativeProtocolLimits.MaximumObjectRewardKind;
    private const int MaximumObjectRewardOriginKind = AuthoritativeProtocolLimits.MaximumObjectRewardOriginKind;
    private const int MaximumObjectQuestSequenceFollowUpKind = AuthoritativeProtocolLimits.MaximumObjectQuestSequenceFollowUpKind;
    private const int MaximumObjectProjectileSourceKind = AuthoritativeProtocolLimits.MaximumObjectProjectileSourceKind;
    private const int MaximumQuestTownerInteractionKind = AuthoritativeProtocolLimits.MaximumQuestTownerInteractionKind;
    private readonly List<GameEvent> recentEvents = [];
    private readonly Dictionary<ulong, PredictedMove> predictedMoves = [];
    private ulong recentEventsStartSequence;
    private ulong recentEventsEndSequence;
    private bool acceptNextSnapshotAsNewEpoch;

    public Snapshot? Snapshot { get; private set; }

    public string? LastError { get; private set; }

    public CommandResult? LastCommandResult { get; private set; }

    public IReadOnlyList<GameEvent> RecentEvents => recentEvents;

    /** Sequence number of the first event retained in the bounded history. */
    public ulong RecentEventsStartSequence => recentEventsStartSequence;

    /** Exclusive sequence number immediately after the retained event history. */
    public ulong RecentEventsEndSequence => recentEventsEndSequence;

    /** Server-owned armed traps available to presentation code for typed interaction. */
    public IReadOnlyList<ObjectSnapshot> ArmedTrapObjects => Snapshot?.Objects
        .Where(@object => @object.TrapArmed)
        .ToArray() ?? [];

    /** Presentation-only level triggers for the local player's active level. */
    public IReadOnlyList<WarpTriggerSnapshot> VisibleWarpTriggers
        => Snapshot is { } snapshot
            ? snapshot.LevelGeometry
                .Where(level => level.LevelId == snapshot.Players.FirstOrDefault()?.LevelId)
                .SelectMany(level => level.WarpTriggers)
                .ToArray()
            : [];

    /** Returns the server-authored route marker at a tile on the active level. */
    public WarpTriggerSnapshot? WarpTriggerAt(int x, int y)
        => VisibleWarpTriggers.FirstOrDefault(trigger =>
            trigger.TriggerX == x
            && trigger.TriggerY == y);

    /** Returns the validated server-owned stash page for presentation. */
    public StashLayout GetStashLayout(uint page)
    {
        return StashLayout.Build(
            Snapshot?.Players.FirstOrDefault()?.Stash,
            page);
    }

    /** Returns an available authored quest/towner interaction at a clicked tile. */
    public QuestTownerInteractionSnapshot? QuestTownerInteractionAt(int x, int y)
        => AuthoritativeQuestTownerInteractionSelector.SelectAt(Snapshot, x, y);

    /** Returns a general towner greeting at a clicked tile; range is checked before submission. */
    public TownerGreetingSnapshot? TownerGreetingAt(int x, int y)
        => AuthoritativeTownerGreetingSelector.SelectAt(Snapshot, x, y);

    public ulong CurrentTick => Snapshot?.Tick ?? 0;

    /** The server puts the receiving participant first in every snapshot. */
    public uint PlayerEntityId => Snapshot?.Players.FirstOrDefault()?.EntityId ?? 0;

    /** Whether the authoritative local stash session is currently open. */
    public bool IsStashOpen => Snapshot?.Players.FirstOrDefault()?.StashOpen == true;

    public ulong NextCommandTick => checked(CurrentTick + 1);

    public ClientPosition? PredictedPlayerPosition { get; private set; }

    public bool IsCorrectingPosition { get; private set; }

    public void Apply(AuthoritativeClientMessage message)
    {
        if (message.Snapshot is { } incomingSnapshot
            && Snapshot is { } currentSnapshot
            && incomingSnapshot.Tick < currentSnapshot.Tick
            && !acceptNextSnapshotAsNewEpoch)
            message = message with { Snapshot = null };
        if (message.Snapshot is not null) {
            if (message.Snapshot.Players.Count == 0)
                throw new InvalidDataException("The authoritative snapshot has no local player.");
            if (message.Snapshot.Players.Count > NativeMaximumPlayerCount)
                throw new InvalidDataException(
                    $"The authoritative snapshot contains more than {NativeMaximumPlayerCount} "
                    + "players, exceeding the native player pool capacity.");
            ValidateSessionMetadata(message.Snapshot);
            var entityIds = new HashSet<uint>();
            foreach (var player in message.Snapshot.Players) {
                ValidateEntityId(entityIds, player.EntityId);
                if (!AuthoritativePlayerNameRules.IsValidShape(player.CharacterName)) {
                    throw new InvalidDataException(
                        "The authoritative snapshot contains an invalid player character name.");
                }
                if (!Enum.IsDefined(player.FacingDirection)) {
                    throw new InvalidDataException(
                        "The authoritative snapshot contains an invalid player facing direction.");
                }
                if (!Enum.IsDefined(player.AbilityKind)) {
                    throw new InvalidDataException(
                        "The authoritative snapshot contains an invalid player ability kind.");
                }
                if (!Enum.IsDefined(player.AbilityTargetingKind)) {
                    throw new InvalidDataException(
                        "The authoritative snapshot contains an invalid ability targeting kind.");
                }
                ValidatePlayerResources(player);
                ValidatePlayerAttributes(player);
                ValidateVisitedLevels(player);
                ValidatePlayerSpells(player);
                ValidateStash(player);
                ValidateInventoryTopology(player);
                ValidatePlayerItemFootprints(player);
                if (!Enum.IsDefined(player.CombatMode)
                    || (player.CombatMode == PlayerCombatMode.Stand
                        && player.CombatModeEndTick != 0)
                    || ((player.CombatMode is PlayerCombatMode.Block
                            or PlayerCombatMode.GotHit)
                        && player.CombatModeEndTick == 0)) {
                    throw new InvalidDataException(
                        "The authoritative snapshot contains an invalid player combat mode.");
                }
                if (player.HeldItem is { } heldItem
                    && (!HasValidItemIdentity(heldItem.ItemSeed, heldItem.State)
                        || heldItem.State.InventoryWidth > NativeInventoryWidth
                        || heldItem.State.InventoryHeight > NativeInventoryHeight)) {
                    throw new InvalidDataException(
                        "The authoritative snapshot contains an invalid held item.");
                }
                ValidateStatusEffects(player);
                ValidatePlayerAction(message.Snapshot, player);
            }
            ValidateActiveStore(message.Snapshot);
            foreach (var monster in message.Snapshot.Monsters) {
                ValidateEntityId(entityIds, monster.EntityId);
                ValidateMonsterVitality(monster);
                ValidateMonsterCombatState(monster);
                ValidateMonsterMovementState(monster);
            }
            ValidateMonsterGroupState(message.Snapshot.Monsters);
            foreach (var item in message.Snapshot.WorldItems) {
                ValidateEntityId(entityIds, item.EntityId);
                if (item.Price > int.MaxValue) {
                    throw new InvalidDataException(
                        "The authoritative snapshot contains an unrepresentable world-item price.");
                }
                ValidateItemState(item.State);
                if (!HasValidItemIdentity(item.ItemSeed, item.State)) {
                    throw new InvalidDataException(
                        "The authoritative snapshot contains an invalid world item.");
                }
            }
            ValidateQuests(message.Snapshot);
            ValidateWorldItemPoolCapacity(message.Snapshot);
            ValidateObjectPoolCapacity(message.Snapshot);
            ValidateMonsterPoolCapacity(message.Snapshot);
            ValidatePortalPoolCapacity(message.Snapshot);
            var questIds = message.Snapshot.Quests
                .Select(quest => quest.QuestId)
                .ToHashSet();
            foreach (var @object in message.Snapshot.Objects) {
                ValidateEntityId(entityIds, @object.EntityId);
                ValidateObject(@object, questIds);
            }
            foreach (var projectile in message.Snapshot.Projectiles) {
                ValidateEntityId(entityIds, projectile.EntityId);
                ValidateProjectile(projectile);
            }
            foreach (var corpse in message.Snapshot.Corpses)
                ValidateEntityId(entityIds, corpse.EntityId);
            ValidateWarpTriggers(message.Snapshot);
            ValidateSetLevelObjectOrigins(message.Snapshot);
            ValidateMaterializedProceduralLevels(message.Snapshot);
            ValidateEntityPositions(message.Snapshot);
            ValidateQuestTownerInteractions(message.Snapshot);
            ValidateTownerGreetings(message.Snapshot);
            var portalIds = new HashSet<uint>();
            foreach (var portal in message.Snapshot.Portals) {
                if (portal.PortalId == 0
                    || !portalIds.Add(portal.PortalId)
                    || (portal.Bidirectional
                        && (portal.OwnerEntityId == 0 || portal.RemainingTicks == 0))
                    || (!portal.Bidirectional
                        && (portal.OwnerEntityId != 0 || portal.RemainingTicks != 0))) {
                    throw new InvalidDataException(
                        "The authoritative snapshot contains an invalid portal.");
                }
            }
            var previousPlayer = Snapshot?.Players.FirstOrDefault();
            var nextPlayer = message.Snapshot.Players[0];
            if (previousPlayer is null
                || previousPlayer.EntityId != nextPlayer.EntityId
                || AuthoritativeEntityPositionProjection.RequiresLevelReset(
                    previousPlayer.LevelId,
                    nextPlayer.LevelId))
                predictedMoves.Clear();
            // Keep an owned copy so callers cannot mutate validated state
            // through a reused transport buffer after admission.
            Snapshot = message.Snapshot.Clone();
            acceptNextSnapshotAsNewEpoch = false;
            LastError = null;
            RebuildPredictedPosition();
        }

        if (message.Events is { } events
            && !acceptNextSnapshotAsNewEpoch
            && events.Tick >= CurrentTick) {
            ValidateEventBatch(events);
            recentEvents.AddRange(events.Events.Select(gameEvent => gameEvent.Clone()));
            recentEventsEndSequence = checked(
                recentEventsEndSequence + (ulong)events.Events.Count);
            var excess = recentEvents.Count - MaxRecentEvents;
            if (excess > 0) {
                recentEvents.RemoveRange(0, excess);
                recentEventsStartSequence = checked(
                    recentEventsStartSequence + (ulong)excess);
            }
        }

        if (message.Error is not null)
            LastError = message.Error.Detail;

        if (message.Acknowledgement is not null) {
            AuthoritativeCommandAckValidator.Validate(message.Acknowledgement);
            if (message.Acknowledgement.Results.Count > 0) {
                var latestResult = message.Acknowledgement.Results
                    .OrderBy(result => result.ClientSequence)
                    .Last();
                if (LastCommandResult is null
                    || latestResult.ClientSequence >= LastCommandResult.ClientSequence)
                    LastCommandResult = latestResult.Clone();
            }
            foreach (var result in message.Acknowledgement.Results)
                predictedMoves.Remove(result.ClientSequence);
            RebuildPredictedPosition();
        }
    }

    public void ClearRecentEvents()
    {
        if (recentEvents.Count == 0)
            return;
        recentEvents.Clear();
        recentEventsStartSequence = recentEventsEndSequence;
    }

    /** Clears presentation-only movement prediction without discarding server state. */
    public void ResetPrediction()
    {
        predictedMoves.Clear();
        RebuildPredictedPosition();
    }

    /**
     * Clears transient presentation state when reconnecting without discarding
     * the authoritative snapshot or transport command-result ledger.
     */
    public void ResetTransientPresentation()
    {
        recentEvents.Clear();
        acceptNextSnapshotAsNewEpoch = true;
        ResetPrediction();
    }

    public void TrackPredictedMove(ulong sequence, int directionX, int directionY)
    {
        if (sequence == 0 || (directionX == 0 && directionY == 0))
            return;
        if (!predictedMoves.ContainsKey(sequence) && predictedMoves.Count >= MaxPredictedMoves)
            return;
        predictedMoves[sequence] = new PredictedMove(directionX, directionY);
        RebuildPredictedPosition();
    }

    private void RebuildPredictedPosition()
    {
        if (Snapshot?.Players.FirstOrDefault() is not { } player) {
            PredictedPlayerPosition = null;
            IsCorrectingPosition = false;
            return;
        }

        var authoritativePosition = AuthoritativePlayerPositionProjection.Resolve(player);
        var x = authoritativePosition.X;
        var y = authoritativePosition.Y;
        var authoritativeMove = player.CurrentAction is { Kind: PlayerActionKind.Move } action
            ? action
            : null;
        var skippedAuthoritativeMove = false;
        foreach (var move in predictedMoves.OrderBy(pair => pair.Key).Select(pair => pair.Value)) {
            if (!skippedAuthoritativeMove
                && authoritativeMove is not null
                && move.DirectionX == authoritativeMove.DirectionX
                && move.DirectionY == authoritativeMove.DirectionY) {
                skippedAuthoritativeMove = true;
                continue;
            }
            var nextX = (long)x + move.DirectionX;
            var nextY = (long)y + move.DirectionY;
            if (nextX < int.MinValue
                || nextX > int.MaxValue
                || nextY < int.MinValue
                || nextY > int.MaxValue
                || !IsProjectedMoveAvailable(Snapshot, player, (int)nextX, (int)nextY))
                break;
            (x, y) = ((int)nextX, (int)nextY);
        }

        IsCorrectingPosition = x != player.PositionX || y != player.PositionY;
        PredictedPlayerPosition = IsCorrectingPosition ? new ClientPosition(x, y) : null;
    }

    /**
     * Applies only collision facts already present in the authoritative
     * snapshot. Prediction never invents local geometry or entity state; when
     * either is absent, the queued move remains eligible for presentation.
     */
    private static bool IsProjectedMoveAvailable(
        Snapshot snapshot,
        PlayerSnapshot player,
        int targetX,
        int targetY)
    {
        var level = snapshot.LevelGeometry
            .FirstOrDefault(candidate => candidate.LevelId == player.LevelId);
        if (level is { Width: > 0, Height: > 0 }) {
            if (targetX < 0
                || targetY < 0
                || targetX >= level.Width
                || targetY >= level.Height)
                return false;
            var targetCell = (long)targetY * level.Width + targetX;
            if (targetCell > int.MaxValue || level.BlockedCells.Contains((int)targetCell))
                return false;
        }

        if (snapshot.Players.Any(other =>
                other.EntityId != player.EntityId
                && other.Life > 0
                && other.LevelId == player.LevelId
                && AuthoritativePlayerPositionProjection.Resolve(other)
                    == new ClientPosition(targetX, targetY)))
            return false;

        return !snapshot.Monsters.Any(monster =>
            monster.Alive
            && monster.LevelId == player.LevelId
            && ((monster.PositionX == targetX && monster.PositionY == targetY)
                || (monster.Moving
                    && monster.FuturePositionX == targetX
                    && monster.FuturePositionY == targetY)));
    }

    private static void ValidatePlayerAction(Snapshot snapshot, PlayerSnapshot player)
    {
        var action = player.CurrentAction;
        if (action is null)
            return;
        if (!Enum.IsDefined(action.Kind)
            || action.Kind == PlayerActionKind.Unspecified
            || action.EffectTick < action.StartedAtTick
            || action.ReadyTick < action.EffectTick
            || action.ReadyTick > action.EndTick
            || action.EndTick != player.CombatModeEndTick) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid player action timing.");
        }

        var valid = action.Kind switch {
            PlayerActionKind.Move =>
                player.CombatMode is PlayerCombatMode.WalkNorthwards
                    or PlayerCombatMode.WalkSouthwards
                    or PlayerCombatMode.WalkSideways
                && action.DirectionX is >= -1 and <= 1
                && action.DirectionY is >= -1 and <= 1
                && (action.DirectionX != 0 || action.DirectionY != 0)
                && action.TargetEntityId == 0
                && action.Cast is null
                && IsProjectedMoveWalkable(snapshot, player, action),
            PlayerActionKind.Attack =>
                player.CombatMode is PlayerCombatMode.Attack
                    or PlayerCombatMode.RangedAttack
                && action.DirectionX == 0
                && action.DirectionY == 0
                && action.TargetEntityId != 0
                && action.Cast is null,
            PlayerActionKind.Cast =>
                player.CombatMode == PlayerCombatMode.Spell
                && action.DirectionX == 0
                && action.DirectionY == 0
                && action.Cast is not null
                && action.Cast.SpellId != 0
                && action.TargetEntityId == action.Cast.TargetEntityId
                && Enum.IsDefined(action.Cast.Source)
                && (action.Cast.SourceItem is null
                    || Enum.IsDefined(action.Cast.SourceItem.Location)),
            PlayerActionKind.Death =>
                player.CombatMode == PlayerCombatMode.Death
                && player.Life == 0
                && action.EffectApplied
                && action.EffectTick == action.StartedAtTick
                && action.ReadyTick == action.EndTick
                && action.DirectionX == 0
                && action.DirectionY == 0
                && action.Cast is null,
            PlayerActionKind.LevelTransition =>
                player.CombatMode == PlayerCombatMode.NewLevel
                && player.Life > 0
                && action.EffectApplied
                && action.EffectTick == action.StartedAtTick
                && action.ReadyTick == action.EndTick
                && action.DirectionX == 0
                && action.DirectionY == 0
                && action.TargetEntityId != 0
                && action.Cast is null,
            PlayerActionKind.RestartTown =>
                player.CombatMode == PlayerCombatMode.NewLevel
                && player.Life > 0
                && action.EffectApplied
                && action.EffectTick == action.StartedAtTick
                && action.ReadyTick == action.EndTick
                && action.DirectionX == 0
                && action.DirectionY == 0
                && action.TargetEntityId == 0
                && action.Cast is null,
            _ => false,
        };
        if (!valid) {
            throw new InvalidDataException(
                "The authoritative snapshot contains an invalid player action.");
        }
    }

    private static bool IsProjectedMoveWalkable(
        Snapshot snapshot,
        PlayerSnapshot player,
        PlayerActionSnapshot action)
    {
        var level = snapshot.LevelGeometry
            .FirstOrDefault(candidate => candidate.LevelId == player.LevelId);
        if (level is null || level.Width == 0 || level.Height == 0)
            return true;

        var targetX = (long)player.PositionX + action.DirectionX;
        var targetY = (long)player.PositionY + action.DirectionY;
        if (targetX < 0
            || targetY < 0
            || targetX >= level.Width
            || targetY >= level.Height) {
            return false;
        }

        var targetCell = targetY * level.Width + targetX;
        return targetCell <= int.MaxValue
            && !level.BlockedCells.Contains((int)targetCell);
    }

    private static void ValidateStatusEffects(PlayerSnapshot player)
    {
        if (player.StatusEffects.Count > MaximumStatusEffectCount) {
            throw new InvalidDataException(
                "The authoritative snapshot contains too many status effects.");
        }
        var effectIds = new HashSet<uint>();
        foreach (var effect in player.StatusEffects) {
            if (effect.EffectId == 0
                || effect.RemainingTicks == 0
                || (int)effect.Behavior is < 0 or > MaximumStatusEffectBehavior
                || !Enum.IsDefined(effect.Behavior)
                || ((effect.Behavior is StatusEffectBehavior.Infravision
                        or StatusEffectBehavior.Search
                        or StatusEffectBehavior.Etherealize)
                    && effect.Magnitude != 0)
                || !effectIds.Add(effect.EffectId)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid status-effect state.");
            }
        }
    }

    private static void ValidateStash(PlayerSnapshot player)
    {
        if (player.StashOpen && player.ActiveStoreId != 0)
            throw new InvalidDataException(
                "The authoritative snapshot opens a stash and store simultaneously.");
        if (player.Stash.Count > StashLayout.PageCount * StashLayout.CellCount)
            throw new InvalidDataException(
                "The authoritative snapshot contains too many stash items.");
        var pages = player.Stash
            .Select(item => item.Page)
            .Distinct()
            .ToArray();
        if (pages.Any(page => page >= StashLayout.PageCount))
            throw new InvalidDataException(
                "The authoritative snapshot contains an invalid stash page.");
        foreach (var page in pages)
            _ = StashLayout.Build(player.Stash, page);
    }

    private static void ValidateInventoryTopology(PlayerSnapshot player)
    {
        if (player.Inventory.Count > NativeMaximumInventoryCount
            || player.Belt.Count > NativeMaximumBeltCount
            || player.InventoryGrid.Count != 0 && player.InventoryGrid.Count != NativeMaximumInventoryGridCount
            || player.InventoryGrid.Any(cell => cell < -1 || cell >= player.Inventory.Count)) {
            throw new InvalidDataException(
                "The authoritative snapshot contains an invalid inventory topology.");
        }

        var equipmentSlots = new HashSet<uint>();
        foreach (var item in player.Equipment) {
            if (item is null || item.Slot >= NativeMaximumEquipmentCount || !equipmentSlots.Add(item.Slot)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid or duplicate equipment slots.");
            }
        }

        var beltSlots = new HashSet<uint>();
        foreach (var item in player.Belt) {
            if (item is null || item.Slot >= NativeMaximumBeltCount || !beltSlots.Add(item.Slot)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid or duplicate belt slots.");
            }
        }

        if (player.InventoryGrid.Count == 0)
            return;

        var expected = Enumerable.Repeat(-1, player.InventoryGrid.Count).ToArray();
        for (var itemIndex = 0; itemIndex < player.Inventory.Count; itemIndex++) {
            var anchor = player.InventoryGrid.IndexOf(itemIndex);
            var state = player.Inventory[itemIndex]?.State;
            var width = (int)Math.Clamp(state?.InventoryWidth ?? 1U, 1U, 10U);
            var height = (int)Math.Clamp(state?.InventoryHeight ?? 1U, 1U, 4U);
            if (anchor < 0
                || !HasExactInventoryFootprint(
                    expected,
                    anchor,
                    itemIndex,
                    width,
                    height)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains an invalid inventory footprint topology.");
            }
        }
        if (!player.InventoryGrid.SequenceEqual(expected)) {
            throw new InvalidDataException(
                "The authoritative snapshot contains an invalid inventory-grid projection.");
        }
    }

    private static bool HasExactInventoryFootprint(
        int[] expected,
        int anchor,
        int itemIndex,
        int width,
        int height)
    {
        var column = anchor % NativeInventoryWidth;
        var row = anchor / NativeInventoryWidth;
        if (column + width > NativeInventoryWidth || row + height > NativeInventoryHeight)
            return false;
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                var cell = (row + y) * NativeInventoryWidth + column + x;
                if (expected[cell] >= 0)
                    return false;
                expected[cell] = itemIndex;
            }
        }
        return true;
    }

    private static void ValidateActiveStore(Snapshot snapshot)
    {
        var player = snapshot.Players[0];
        var store = snapshot.ActiveStore;
        if ((store is null) != (player.ActiveStoreId == 0)
            || store is not null
                && (store.StoreId == 0 || store.StoreId != player.ActiveStoreId)) {
            throw new InvalidDataException(
                "The authoritative snapshot contains an inconsistent active store.");
        }
        if (store is null)
            return;

        var slots = new HashSet<uint>();
        foreach (var item in store.Items) {
            if (!slots.Add(item.StoreSlot)
                || item.Price > int.MaxValue
                || !HasValidItemIdentity(item.ItemSeed, item.State)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid active-store stock.");
            }
            ValidateItemState(item.State);
        }
    }

    private static void ValidatePlayerItemFootprints(PlayerSnapshot player)
    {
        var itemSeeds = new HashSet<uint>();
        foreach (var item in player.Inventory) {
            ValidatePlayerItemPrice(item?.Price ?? 0);
            ValidatePlayerItem(item?.ItemSeed ?? 0, item?.State, itemSeeds);
        }
        foreach (var item in player.Belt) {
            ValidatePlayerItem(item?.ItemSeed ?? 0, item?.State, itemSeeds);
        }
        foreach (var item in player.Equipment) {
            ValidatePlayerItem(item?.ItemSeed ?? 0, item?.State, itemSeeds);
        }
        foreach (var item in player.Stash)
            if (item?.Item is { } stashItem) {
                ValidatePlayerItemPrice(stashItem.Price);
                ValidatePlayerItem(stashItem.ItemSeed, stashItem.State, itemSeeds);
            }
        if (player.HeldItem is { } heldItem) {
            ValidatePlayerItemPrice(heldItem.Price);
            ValidatePlayerItem(heldItem.ItemSeed, heldItem.State, itemSeeds);
        }
    }

    private static void ValidatePlayerItemPrice(uint price)
    {
        if (price > int.MaxValue)
            throw new InvalidDataException(
                "The authoritative snapshot contains a player-item price outside the native range.");
    }

    private static void ValidatePlayerItem(
        uint itemSeed,
        ItemStateSnapshot? state,
        ISet<uint> itemSeeds)
    {
        if (itemSeed != 0 && !itemSeeds.Add(itemSeed))
            throw new InvalidDataException(
                "The authoritative snapshot contains duplicate player item references.");
        if (state is not null && !HasValidItemIdentity(itemSeed, state))
            throw new InvalidDataException(
                "The authoritative snapshot contains a player item with an invalid identity.");
        ValidateInventoryFootprint(state);
        ValidateItemState(state);
    }

    private static void ValidateInventoryFootprint(ItemStateSnapshot? state)
    {
        if (state is null)
            return;
        if (state.InventoryWidth > NativeInventoryWidth || state.InventoryHeight > NativeInventoryHeight) {
            throw new InvalidDataException(
                "The authoritative snapshot contains an item footprint outside the native inventory grid.");
        }
    }

    private static void ValidateItemState(ItemStateSnapshot? state)
    {
        if (state is null)
            return;
        if (state.Value < 0
            || state.IdentifiedValue < 0
            || state.MinDamage < 0
            || state.MaxDamage < state.MinDamage
            || state.ArmorClass < 0
            || state.Charges < 0
            || state.MaxCharges < state.Charges
            || state.Durability < 0
            || state.MaxDurability < state.Durability
            || state.FireMinDamage < 0
            || state.FireMaxDamage < state.FireMinDamage
            || state.LightningMinDamage < 0
            || state.LightningMaxDamage < state.LightningMinDamage
            || !IsNativeItemStateRepresentable(state)
            || (int)state.PresentationKind is < 0 or > MaximumItemPresentationKind
            || !Enum.IsDefined(state.PresentationKind)
            || (int)state.SpellSourceKind is < 0 or > MaximumItemSpellSourceKind
            || !Enum.IsDefined(state.SpellSourceKind)
            || (int)state.ItemActionKind is < 0 or > MaximumItemActionKind
            || !Enum.IsDefined(state.ItemActionKind)
            || (int)state.SpellTargetingKind is < 0 or > MaximumSpellTargetingKind
            || !Enum.IsDefined(state.SpellTargetingKind)
            || state.SpellDisplayName.Contains('\0')) {
            throw new InvalidDataException(
                "The authoritative snapshot contains impossible item state ranges.");
        }
    }

    private static bool IsNativeItemStateRepresentable(ItemStateSnapshot state)
    {
        return state.CreateInfo <= ushort.MaxValue
            && state.ItemType is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.Magical is >= byte.MinValue and <= byte.MaxValue
            && state.EquipLocation is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.ItemClass is >= byte.MinValue and <= byte.MaxValue
            && state.MinDamage is >= byte.MinValue and <= byte.MaxValue
            && state.MaxDamage is >= byte.MinValue and <= byte.MaxValue
            && state.ArmorClass is >= short.MinValue and <= short.MaxValue
            && state.MiscId is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.SpellId is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.ItemIndex is >= short.MinValue and <= short.MaxValue
            && state.PlusDamage is >= short.MinValue and <= short.MaxValue
            && state.PlusToHit is >= short.MinValue and <= short.MaxValue
            && state.PlusArmorClass is >= short.MinValue and <= short.MaxValue
            && state.PlusStrength is >= short.MinValue and <= short.MaxValue
            && state.PlusMagic is >= short.MinValue and <= short.MaxValue
            && state.PlusDexterity is >= short.MinValue and <= short.MaxValue
            && state.PlusVitality is >= short.MinValue and <= short.MaxValue
            && state.PlusFireResistance is >= short.MinValue and <= short.MaxValue
            && state.PlusLightningResistance is >= short.MinValue and <= short.MaxValue
            && state.PlusMagicResistance is >= short.MinValue and <= short.MaxValue
            && state.PlusMana is >= short.MinValue and <= short.MaxValue
            && state.PlusHitPoints is >= short.MinValue and <= short.MaxValue
            && state.PlusDamageModifier is >= short.MinValue and <= short.MaxValue
            && state.PlusGetHit is >= short.MinValue and <= short.MaxValue
            && state.PlusLight is >= short.MinValue and <= short.MaxValue
            && state.SpellLevelAdd is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.FireMinDamage is >= short.MinValue and <= short.MaxValue
            && state.FireMaxDamage is >= short.MinValue and <= short.MaxValue
            && state.LightningMinDamage is >= short.MinValue and <= short.MaxValue
            && state.LightningMaxDamage is >= short.MinValue and <= short.MaxValue
            && state.PlusEnemyArmorClass is >= short.MinValue and <= short.MaxValue
            && state.PrefixPower is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.SuffixPower is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.MinimumStrength is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.MinimumMagic is >= byte.MinValue and <= byte.MaxValue
            && state.MinimumDexterity is >= sbyte.MinValue and <= sbyte.MaxValue
            && state.HellfireDamageArmorFlags is >= byte.MinValue and <= byte.MaxValue;
    }

    private static void ValidatePlayerResources(PlayerSnapshot player)
    {
        if (player.Life < 0
            || player.LifeMaximum < 0
            || player.LifeMaximum > 0 && player.Life > player.LifeMaximum
            || player.Mana < 0
            || player.ManaMaximum < 0
            || player.ManaMaximum > 0 && player.Mana > player.ManaMaximum
            || player.BaseLifeMaximum < 0
            || player.BaseManaMaximum < 0
            || player.Gold > int.MaxValue
            || player.StashGold > int.MaxValue
            || player.StatPoints > int.MaxValue
            || player.ReflectCharges > MaximumNativeReflectCharges
            || (player.LightRadius != 0
                && (player.LightRadius is < MinimumNativeLightRadius or > MaximumNativeLightRadius))
            || player.CharacterLevel > MaximumNativeCharacterLevel
            || player.DiabloKillLevel > MaximumNativeDiabloKillLevel) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid player resources.");
        }
    }

    private static void ValidatePlayerAttributes(PlayerSnapshot player)
    {
        var attributes = player.Attributes;
        if (attributes is null)
            return;

        if (attributes.Strength is null
            || attributes.Magic is null
            || attributes.Dexterity is null
            || attributes.Vitality is null
            || attributes.Strength.Base < 0
            || attributes.Strength.Current < 0
            || attributes.Magic.Base < 0
            || attributes.Magic.Current < 0
            || attributes.Dexterity.Base < 0
            || attributes.Dexterity.Current < 0
            || attributes.Vitality.Base < 0
            || attributes.Vitality.Current < 0) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid player attributes.");
        }
    }

    private static void ValidateEntityId(HashSet<uint> entityIds, uint entityId)
    {
        if (entityId == 0 || !entityIds.Add(entityId)) {
            throw new InvalidDataException(
                "The authoritative snapshot contains duplicate or zero entity IDs.");
        }
    }

    private static void ValidateMonsterVitality(MonsterSnapshot monster)
    {
        var hitPointsQ6 = monster.HitPointsQ6 == 0 && monster.HitPoints != 0
            ? (long)monster.HitPoints * 64
            : monster.HitPointsQ6;
        var maxHitPointsQ6 = monster.MaxHitPointsQ6 == 0 && monster.MaxHitPoints != 0
            ? (long)monster.MaxHitPoints * 64
            : monster.MaxHitPointsQ6;
        if (monster.EntityId == 0
            || monster.HitPoints < 0
            || monster.MaxHitPoints < 0
            || monster.MaxHitPoints > 0 && monster.HitPoints > monster.MaxHitPoints
            || monster.HitPointsQ6 < 0
            || monster.MaxHitPointsQ6 < 0
            || maxHitPointsQ6 < hitPointsQ6
            || hitPointsQ6 < 0
            || monster.Alive != (hitPointsQ6 > 0)
            || System.Text.Encoding.UTF8.GetByteCount(monster.UniqueIdentity) > MaximumIdentityBytes
            || !string.IsNullOrEmpty(monster.UniqueIdentity) && !monster.IsUnique) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid monster vitality.");
        }
        if (monster.DialogueMessageId < 0
            || monster.DialogueMessageId > short.MaxValue
            || monster.DialogueMessageId != 0 && !monster.IsUnique) {
            throw new InvalidDataException(
                "The authoritative snapshot contains an invalid monster dialogue identity.");
        }
        var dialogueState = (int)monster.DialogueState;
        if (dialogueState < 0
            || dialogueState > MaximumMonsterDialogueState
            || dialogueState != 0 && !monster.IsUnique) {
            throw new InvalidDataException(
                "The authoritative snapshot contains an invalid monster dialogue state.");
        }
    }

    private static void ValidateObject(
        ObjectSnapshot @object,
        IReadOnlySet<uint> questIds)
    {
        var transitionKind = @object.QuestTransitionKind;
        var rewardKind = @object.RewardKind;
        var followUpKind = @object.QuestSequenceFollowUpKind;
        var effectKind = @object.EffectKind;
        var transitionSetsVariable = transitionKind is 3 or 4 or 5 or 6 or 7 or 8;
        var transitionValid = transitionKind == 0
            || @object.QuestId != 0
                && (transitionSetsVariable
                    ? @object.QuestTransitionAmount > 0
                    : @object.QuestTransitionAmount == 0);
        var rewardValid = rewardKind == 0
            ? @object.RewardId == 0
                && @object.RewardItemLevel == 0
                && @object.RewardOffsetX == 0
                && @object.RewardOffsetY == 0
                && @object.RewardOriginKind == 0
                && @object.RewardSetLevelId == 0
            : @object.RewardId != 0;
        var producesProjectile = effectKind is 8 or 9
            || followUpKind == 2;
        var followUpValid = followUpKind switch {
            0 => @object.QuestSequenceFollowUpAmount == 0
                && @object.QuestSequenceFollowUpOffsetX == 0
                && @object.QuestSequenceFollowUpOffsetY == 0,
            1 => (effectKind == 4 || transitionKind != 0)
                && @object.QuestSequenceFollowUpAmount != 0
                && @object.QuestSequenceFollowUpOffsetX == 0
                && @object.QuestSequenceFollowUpOffsetY == 0,
            2 => (effectKind == 4 || effectKind == 6 || transitionKind != 0)
                && @object.QuestSequenceFollowUpAmount != 0,
            3 => (effectKind == 4 || transitionKind != 0)
                && @object.QuestSequenceFollowUpAmount != 0,
            _ => false,
        };
        if (@object.ObjectId == 0
            || @object.EffectAmount < 0
            || effectKind < 0
            || effectKind > MaximumObjectEffectKind
            || (int)@object.PresentationKind is < 0 or > MaximumObjectPresentationKind
            || !Enum.IsDefined(@object.PresentationKind)
            || @object.QuestId != 0 && !questIds.Contains(@object.QuestId)
            || transitionKind is < 0 or > MaximumObjectQuestTransitionKind
            || @object.QuestTransitionAmount is < 0 or > byte.MaxValue
            || @object.QuestTransitionRequiredVariable1 is < -1 or > byte.MaxValue
            || @object.QuestTransitionRequiredVariable2 is < -1 or > byte.MaxValue
            || !transitionValid
            || rewardKind is < 0 or > MaximumObjectRewardKind
            || @object.RewardItemLevel is < 0 or > byte.MaxValue
            || @object.RewardOffsetX is < -16 or > 16
            || @object.RewardOffsetY is < -16 or > 16
            || @object.RewardOriginKind is < 0 or > MaximumObjectRewardOriginKind
            || @object.RewardOriginKind == 0 && @object.RewardSetLevelId != 0
            || @object.RewardOriginKind == 1 && @object.RewardSetLevelId is < 0 or > MaximumNativeSetLevelId
            || !rewardValid
            || @object.ProjectileTargetOffsetX is < -32 or > 32
            || @object.ProjectileTargetOffsetY is < -32 or > 32
            || !producesProjectile
                && (@object.ProjectileTargetOffsetX != 0
                    || @object.ProjectileTargetOffsetY != 0)
            || followUpKind is < 0 or > MaximumObjectQuestSequenceFollowUpKind
            || @object.QuestSequenceFollowUpOffsetX is < -32 or > 32
            || @object.QuestSequenceFollowUpOffsetY is < -32 or > 32
            || !followUpValid
            || @object.ProjectileSourceKind is < 0 or > MaximumObjectProjectileSourceKind
            || !producesProjectile && @object.ProjectileSourceKind != 0
            || @object.NativeObjectId > NativeMaximumObjectId
            || @object.DialogueMessageId is < 0 or > short.MaxValue
            || @object.AnimationFrame > byte.MaxValue
            || @object.ActivatedAnimationFrame > byte.MaxValue
            || @object.Breakable && @object.MissilePassable
            || @object.Destroyed && !@object.Breakable) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid object effect metadata.");
        }
    }

    private static void ValidateObjectPoolCapacity(Snapshot snapshot)
    {
        foreach (var group in snapshot.Objects.GroupBy(@object => @object.LevelId)) {
            if (group.Count() > NativeMaximumObjectCount)
                throw new InvalidDataException(
                    $"The authoritative snapshot contains more than {NativeMaximumObjectCount} "
                    + $"objects on level {group.Key}, exceeding the native object capacity.");
        }
    }

    private static void ValidateWorldItemPoolCapacity(Snapshot snapshot)
    {
        foreach (var group in snapshot.WorldItems.GroupBy(item => item.LevelId)) {
            if (group.Count() > NativeMaximumItemCount)
                throw new InvalidDataException(
                    $"The authoritative snapshot contains more than {NativeMaximumItemCount} "
                    + $"world items on level {group.Key}, exceeding the native item capacity.");
        }
    }

    private static void ValidatePortalPoolCapacity(Snapshot snapshot)
    {
        if (snapshot.Portals.Count > NativeMaximumPortalCount)
            throw new InvalidDataException(
                $"The authoritative snapshot contains more than {NativeMaximumPortalCount} "
                + "portals, exceeding the native portal capacity.");
    }

    private static void ValidateMonsterPoolCapacity(Snapshot snapshot)
    {
        foreach (var group in snapshot.Monsters.GroupBy(monster => monster.LevelId)) {
            if (group.Count() > NativeMaximumMonsterCount)
                throw new InvalidDataException(
                    $"The authoritative snapshot contains more than {NativeMaximumMonsterCount} "
                    + $"monsters on level {group.Key}, exceeding the native monster capacity.");
        }
    }

    private static void ValidateQuests(Snapshot snapshot)
    {
        if (snapshot.Quests.Count > MaximumSnapshotQuestCount) {
            throw new InvalidDataException(
                "The authoritative snapshot contains too many quests.");
        }

        var questIds = new HashSet<uint>();
        var nativeQuestIds = new HashSet<uint>();
        foreach (var quest in snapshot.Quests) {
            if (quest.QuestId == 0
                || !questIds.Add(quest.QuestId)
                || quest.HasNativeQuestId
                    && (quest.NativeQuestId > NativeMaximumQuestId
                        || !nativeQuestIds.Add(quest.NativeQuestId))
                || quest.RequiredProgress == 0
                || quest.Progress > quest.RequiredProgress
                || (quest.Unavailable && (quest.Completed || quest.Progress != 0))
                || quest.Variable1 is < 0 or > byte.MaxValue
                || quest.Variable2 is < 0 or > byte.MaxValue
                || quest.MessageId is < 0 or > short.MaxValue
                || quest.TurnInTownerId is < -1 or > byte.MaxValue
                || quest.TurnInAlternateMessageId is < 0 or > short.MaxValue
                || quest.TurnInCompletionVariable2 is < -1 or > byte.MaxValue
                || quest.TurnInCompletionRequiredVariable1 is < -1 or > byte.MaxValue
                || quest.TurnInCompletionVariable1 is < -1 or > byte.MaxValue
                || quest.TurnInRequiredVariable1 is < -1 or > byte.MaxValue
                || quest.TurnInVariable1 is < -1 or > byte.MaxValue
                || System.Text.Encoding.UTF8.GetByteCount(quest.CompletionUniqueIdentity) > MaximumIdentityBytes) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid quest state.");
            }
        }
    }

    private static void ValidateQuestTownerInteractions(Snapshot snapshot)
    {
        if (snapshot.QuestTownerInteractions.Count > AuthoritativeProtocolLimits.MaximumQuestTownerInteractionCount) {
            throw new InvalidDataException(
                "The authoritative snapshot contains too many quest/towner interactions.");
        }
        var questIds = snapshot.Quests
            .Select(quest => quest.QuestId)
            .ToHashSet();
        var levelGeometry = new Dictionary<uint, LevelGeometrySnapshot>();
        foreach (var level in snapshot.LevelGeometry) {
            if (!levelGeometry.TryAdd(level.LevelId, level)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains duplicate level geometry.");
            }
        }
        var interactionIds = new HashSet<uint>();
        foreach (var interaction in snapshot.QuestTownerInteractions) {
            if (interaction.InteractionId == 0
                || !interactionIds.Add(interaction.InteractionId)
                || interaction.QuestId == 0
                || !questIds.Contains(interaction.QuestId)
                || levelGeometry.Count > 0
                    && (!levelGeometry.TryGetValue(interaction.LevelId, out var level)
                        || level.Width > 0 && interaction.PositionX >= level.Width
                        || level.Height > 0 && interaction.PositionY >= level.Height)
                || interaction.TownerId is < 0 or > byte.MaxValue
                || interaction.LevelId > ushort.MaxValue
                || interaction.RequiredVisitedLevel1 > ushort.MaxValue
                || interaction.RequiredVisitedLevel2 > ushort.MaxValue
                || interaction.RequiredItemId > ushort.MaxValue
                || interaction.PositionX < 0
                || interaction.PositionY < 0
            || interaction.Kind is < 0 or > MaximumQuestTownerInteractionKind
                || !interaction.HasMessageId
                || interaction.MessageId is <= 0 or > short.MaxValue
                || (interaction.HasRequiredVariable1
                    && interaction.RequiredVariable1 is < 0 or > byte.MaxValue)
                || (interaction.HasRequiredVariable2
                    && interaction.RequiredVariable2 is < 0 or > byte.MaxValue)
                || (interaction.HasVariable1
                    && interaction.Variable1 is < 0 or > byte.MaxValue)
                || (interaction.HasVariable2
                    && interaction.Variable2 is < 0 or > byte.MaxValue)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains an invalid quest/towner interaction.");
            }
        }
    }

    private static void ValidateTownerGreetings(Snapshot snapshot)
    {
        if (snapshot.TownerGreetings.Count > AuthoritativeProtocolLimits.MaximumTownerGreetingCount) {
            throw new InvalidDataException(
                "The authoritative snapshot contains too many towner greetings.");
        }

        var levelGeometry = snapshot.LevelGeometry.ToDictionary(level => level.LevelId);
        var townerIds = new HashSet<int>();
        foreach (var greeting in snapshot.TownerGreetings) {
            if (greeting.TownerId is < 0 or > byte.MaxValue
                || !townerIds.Add(greeting.TownerId)
                || greeting.LevelId > ushort.MaxValue
                || greeting.PositionX is < 0 or > byte.MaxValue
                || greeting.PositionY is < 0 or > byte.MaxValue
                || levelGeometry.Count > 0
                    && (!levelGeometry.TryGetValue(greeting.LevelId, out var level)
                        || level.Width > 0 && greeting.PositionX >= level.Width
                        || level.Height > 0 && greeting.PositionY >= level.Height)
                || greeting.MessageId is <= 0 or > short.MaxValue) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains an invalid towner greeting.");
            }
        }
    }

    private static void ValidateEntityPositions(Snapshot snapshot)
    {
        var levels = snapshot.LevelGeometry.ToDictionary(level => level.LevelId);
        bool IsNativePosition(int x, int y)
            => x is >= 0 and <= byte.MaxValue
                && y is >= 0 and <= byte.MaxValue;

        bool IsInBounds(uint levelId, int x, int y)
        {
            if (!IsNativePosition(x, y))
                return false;
            if (!levels.TryGetValue(levelId, out var level)
                || level.Width == 0
                || level.Height == 0) {
                return true;
            }
            return x >= 0
                && y >= 0
                && x < level.Width
                && y < level.Height;
        }

        bool IsValidMonster(MonsterSnapshot monster)
            => IsInBounds(monster.LevelId, monster.PositionX, monster.PositionY)
                && (!monster.Moving
                    || IsInBounds(
                        monster.LevelId,
                        monster.FuturePositionX,
                        monster.FuturePositionY));

        if (snapshot.Players.Any(player =>
                !IsInBounds(player.LevelId, player.PositionX, player.PositionY))
            || snapshot.Monsters.Any(monster => !IsValidMonster(monster))
            || snapshot.WorldItems.Any(item =>
                !IsInBounds(item.LevelId, item.PositionX, item.PositionY))
            || snapshot.Objects.Any(@object =>
                !IsInBounds(@object.LevelId, @object.PositionX, @object.PositionY))
            || snapshot.Projectiles.Any(projectile =>
                !IsInBounds(projectile.LevelId, projectile.PositionX, projectile.PositionY))
            || snapshot.Corpses.Any(corpse =>
                !IsInBounds(corpse.LevelId, corpse.PositionX, corpse.PositionY))
            || snapshot.Portals.Any(portal =>
                !IsInBounds(
                    portal.SourceLevelId,
                    portal.SourcePositionX,
                    portal.SourcePositionY)
                || !IsInBounds(
                    portal.DestinationLevelId,
                    portal.DestinationPositionX,
                    portal.DestinationPositionY))) {
            throw new InvalidDataException(
                "The authoritative snapshot contains an entity outside projected level geometry.");
        }
    }

    /** Validates generated set-piece origins against supplied level geometry. */
    private static void ValidateSetLevelObjectOrigins(Snapshot snapshot)
    {
        if (snapshot.SetLevelObjectOrigins.Count > MaximumSetLevelObjectOriginCount
            || snapshot.SetLevelObjectOrigins.Any(origin => origin.SetLevelId is < 0 or > MaximumNativeSetLevelId)
            || snapshot.SetLevelObjectOrigins
                .Select(origin => origin.SetLevelId)
                .Distinct()
                .Count() != snapshot.SetLevelObjectOrigins.Count) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid or duplicate set-level object origins.");
        }
        if (snapshot.LevelGeometry.Count == 0)
            return;

        var levels = snapshot.LevelGeometry.ToDictionary(level => level.LevelId);
        foreach (var origin in snapshot.SetLevelObjectOrigins) {
            if (!levels.TryGetValue(origin.LevelId, out var level)
                || origin.PositionX < 0
                || origin.PositionY < 0
                || origin.PositionX >= level.Width
                || origin.PositionY >= level.Height) {
                throw new InvalidDataException(
                    $"The authoritative snapshot contains a set-level object origin outside level {origin.LevelId}.");
            }
        }
    }

    /** Validates the server-owned procedural encounter materialization set. */
    private static void ValidateMaterializedProceduralLevels(Snapshot snapshot)
    {
        if (snapshot.MaterializedProceduralLevelIds.Count > MaximumMaterializedProceduralLevelCount
            || snapshot.MaterializedProceduralLevelIds
                .Distinct()
                .Count() != snapshot.MaterializedProceduralLevelIds.Count) {
            throw new InvalidDataException(
                "The authoritative snapshot contains duplicate materialized procedural levels.");
        }
    }

    private static void ValidateWarpTriggers(Snapshot snapshot)
    {
        var levels = new Dictionary<uint, LevelGeometrySnapshot>();
        foreach (var level in snapshot.LevelGeometry) {
            if (level.WarpTriggers.Count > NativeMaximumWarpTriggerCount)
                throw new InvalidDataException(
                    $"The authoritative snapshot contains more than {NativeMaximumWarpTriggerCount} "
                    + $"warp triggers on level {level.LevelId}, exceeding the native trigger capacity.");
            var coarseWidth = (level.Width + 1L) / 2;
            var coarseHeight = (level.Height + 1L) / 2;
            if (level.Width < 0
                || level.Height < 0
                || level.Width > NativeMaximumWidth
                || level.Height > NativeMaximumHeight
                || level.NativeTransparency.Any(value => value > byte.MaxValue)
                || level.NativeObjectLayer.Any(value => value > NativeMaximumDunObjectLayerId)
                || level.NativeMegaTileLayer.Any(value => value > byte.MaxValue)
                || level.NativePieceLayer.Any(value => value > NativeMaximumDunPieceId)
                || level.NativeMegaTileLayer.Count != 0
                    && (coarseWidth > NativeMaximumMegaWidth
                        || coarseHeight > NativeMaximumMegaHeight)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid level geometry dimensions.");
            }
            if (level.Width > 0 && level.Height > 0) {
                var cellCount = checked((long)level.Width * level.Height);
                if (level.BlockedCells.Count > cellCount
                    || level.MissileBlockedCells.Count > cellCount
                    || level.NativeVisionBlockedCells.Count > cellCount
                    || level.BlockedCells.Any(cell => cell < 0 || cell >= cellCount)
                    || level.MissileBlockedCells.Any(cell => cell < 0 || cell >= cellCount)
                    || level.NativeVisionBlockedCells.Any(cell => cell < 0 || cell >= cellCount)
                    || level.BlockedCells.Distinct().Count() != level.BlockedCells.Count
                    || level.MissileBlockedCells.Distinct().Count()
                        != level.MissileBlockedCells.Count
                    || level.NativeVisionBlockedCells.Distinct().Count()
                        != level.NativeVisionBlockedCells.Count
                    || level.NativeTransparency.Count != 0
                        && level.NativeTransparency.Count != cellCount
                    || level.NativeObjectLayer.Count != 0
                        && level.NativeObjectLayer.Count != cellCount
                    || level.NativeMegaTileLayer.Count != 0
                        && level.NativeMegaTileLayer.Count
                            != ((level.Width + 1L) / 2) * ((level.Height + 1L) / 2)
                    || level.NativePieceLayer.Count != 0
                        && level.NativePieceLayer.Count != cellCount) {
                    throw new InvalidDataException(
                        "The authoritative snapshot contains invalid level geometry cells.");
                }
            }
            if (!levels.TryAdd(level.LevelId, level)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains duplicate level geometry.");
            }
        }

        var questIds = snapshot.Quests
            .Select(quest => quest.QuestId)
            .ToHashSet();
        var warpIds = new HashSet<uint>();
        foreach (var level in snapshot.LevelGeometry) {
            foreach (var warp in level.WarpTriggers) {
                var hasTargetLevel = levels.TryGetValue(warp.TargetLevelId, out var targetLevel);
                if (warp.WarpId == 0
                    || !warpIds.Add(warp.WarpId)
                    || warp.TriggerX < 0
                    || warp.TriggerY < 0
                    || (level.Width > 0 && warp.TriggerX >= level.Width)
                    || (level.Height > 0 && warp.TriggerY >= level.Height)
                    || !hasTargetLevel
                    || warp.TargetX < 0
                    || warp.TargetY < 0
                    || (targetLevel!.Width > 0 && warp.TargetX >= targetLevel.Width)
                    || (targetLevel.Height > 0 && warp.TargetY >= targetLevel.Height)
                    || warp.RequiredQuestId != 0
                        && !questIds.Contains(warp.RequiredQuestId)
                    || (warp.RequiredQuestId == 0 && warp.RequiredQuestCompleted)
                    || (warp.RequiredQuestId == 0 && !warp.Available)
                    || warp.StableSymbol.Any(character => character is < (char)0x21 or > (char)0x7e)) {
                    throw new InvalidDataException(
                        "The authoritative snapshot contains an invalid warp trigger.");
                }
            }
        }
    }

    private static void ValidateVisitedLevels(PlayerSnapshot player)
    {
        if (!AuthoritativeLevelIdRules.IsSupported(player.LevelId)
            || player.VisitedLevelIds.Count > AuthoritativeProtocolLimits.MaximumNativeVisitedLevelCount
            || player.VisitedLevelIds.Any(levelId => !AuthoritativeLevelIdRules.IsRegularLevel(levelId))
            || (player.VisitedLevelIds.Count > 0
                && AuthoritativeLevelIdRules.IsRegularLevel(player.LevelId)
                && !player.VisitedLevelIds.Contains(player.LevelId))
            || player.VisitedLevelIds.Distinct().Count() != player.VisitedLevelIds.Count) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid visited-level state.");
        }
    }

    private static void ValidateSessionMetadata(Snapshot snapshot)
    {
        if (!Enum.IsDefined(snapshot.Difficulty)) {
            throw new InvalidDataException(
                "The authoritative snapshot contains an invalid game difficulty.");
        }

        if (snapshot.StateSha256.Length != 0
            && (snapshot.StateSha256.Length != 64
                || snapshot.StateSha256.Any(character => !Uri.IsHexDigit(character)))) {
            throw new InvalidDataException(
                "The authoritative snapshot contains an invalid SHA-256 state hash.");
        }

        var mutationIds = new HashSet<uint>();
        if (snapshot.ActiveWorldMutationIds.Count > MaximumWorldMutationCount) {
            throw new InvalidDataException(
                "The authoritative snapshot contains too many active world mutations.");
        }
        foreach (var mutationId in snapshot.ActiveWorldMutationIds) {
            if (mutationId == 0 || !mutationIds.Add(mutationId)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid or duplicate world-mutation identities.");
            }
        }
    }

    private static void ValidateEventBatch(EventBatch events)
    {
        if (events.Events.Count > MaximumEventBatchCount) {
            throw new InvalidDataException(
                "The authoritative event batch is too large.");
        }

        foreach (var gameEvent in events.Events) {
            if (gameEvent is null) {
                throw new InvalidDataException(
                    "The authoritative event batch contains a null event.");
            }

            switch (gameEvent.EventCase) {
            case GameEvent.EventOneofCase.Damage:
                if (gameEvent.Damage.Amount < 0
                    || gameEvent.Damage.AmountQ6 < 0
                    || gameEvent.Damage.AmountQ6 == 0
                        && gameEvent.Damage.Amount > int.MaxValue / 64) {
                    throw new InvalidDataException(
                        "The authoritative event batch contains invalid damage.");
                }
                break;
            case GameEvent.EventOneofCase.Experience:
                break;
            case GameEvent.EventOneofCase.Healing:
                if (gameEvent.Healing.Amount < 0 || gameEvent.Healing.AmountQ6 < 0) {
                    throw new InvalidDataException(
                        "The authoritative event batch contains invalid healing.");
                }
                break;
            case GameEvent.EventOneofCase.Dialogue:
                if (gameEvent.Dialogue.MessageId < 0
                    || !AuthoritativeProtocolLimits.IsDialogueTextValid(gameEvent.Dialogue.Text)) {
                    throw new InvalidDataException(
                        "The authoritative event batch contains invalid dialogue.");
                }
                break;
            case GameEvent.EventOneofCase.Victory:
                if (gameEvent.Victory.DiabloKillLevel > MaximumNativeDiabloKillLevel) {
                    throw new InvalidDataException(
                        "The authoritative event batch contains an out-of-range Diablo kill level.");
                }
                break;
            case GameEvent.EventOneofCase.MapOpened:
                break;
            default:
                throw new InvalidDataException(
                    "The authoritative event batch contains an unknown event kind.");
            }
        }
    }

    private static void ValidateMonsterCombatState(MonsterSnapshot monster)
    {
        if (monster.AttackDamage < 0
            || monster.MinDamage < 0
            || monster.MaxDamage < 0
            || monster.MaxDamage < monster.MinDamage
            || monster.MinDamageSpecial < 0
            || monster.MaxDamageSpecial < 0
            || monster.MaxDamageSpecial < monster.MinDamageSpecial
            || monster.AggroRange < 0
            || monster.MonsterLevel < 0
            || monster.DeathAnimationTicks is < 0 or > MaximumNativeMonsterDeathAnimationTicks
            || monster.DeathTicksRemaining < 0
            || monster.DeathTicksRemaining > monster.DeathAnimationTicks
            || (monster.HitPointsQ6 > 0 && monster.DeathTicksRemaining != 0)
            || monster.ExperienceReward > MaximumNativeMonsterExperienceReward
            || monster.ToHit is < 0 or > MaximumNativeMonsterToHit
            || monster.ToHitSpecial is < 0 or > MaximumNativeMonsterToHit
            || monster.Intelligence is < 0 or > MaximumNativeMonsterIntelligence
            || monster.Direction is < 0 or > MaximumNativeMonsterDirection
            || monster.FireResistance is < MinimumNativeMonsterResistance or > MaximumNativeMonsterResistance
            || monster.LightningResistance is < MinimumNativeMonsterResistance or > MaximumNativeMonsterResistance
            || monster.MagicResistance is < MinimumNativeMonsterResistance or > MaximumNativeMonsterResistance
            || monster.AcidResistance is < MinimumNativeMonsterResistance or > MaximumNativeMonsterResistance
            || monster.LeaderRelation is < 0 or > MaximumMonsterLeaderRelation
            || (monster.LeaderRelation == 0
                ? monster.LeaderEntityId != 0
                : monster.LeaderEntityId == 0)
            || monster.LeaderRelation != 0 && monster.IsUnique
            || monster.PackSize is < 0 or > AuthoritativeProtocolLimits.MaximumNativeMonsterPackSize
            || monster.ActiveForTicks is < 0 or > AuthoritativeProtocolLimits.MaximumNativeMonsterActiveTicks
            || monster.FallenStandAnimationFrame is < 0 or > MaximumNativeMonsterFallenStandAnimationFrame
            || monster.FallenStandAnimationTick is < 0 or > MaximumNativeMonsterFallenStandAnimationTick
            || monster.FallenRallyTicks is < 0 or > MaximumNativeMonsterFallenRallyTicks
            || (!monster.IsPlayerMinion && monster.OwnerPlayerId != 0)) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid monster combat state.");
        }
    }

    private static void ValidateMonsterGroupState(
        IReadOnlyList<MonsterSnapshot> monsters)
    {
        var monstersById = new Dictionary<uint, MonsterSnapshot>();
        foreach (var monster in monsters) {
            if (!monstersById.TryAdd(monster.EntityId, monster))
                throw new InvalidDataException(
                    "The authoritative snapshot contains duplicate monster entity IDs.");
        }
        foreach (var monster in monsters.Where(monster => monster.LeaderRelation != 0)) {
            if (!monstersById.TryGetValue(monster.LeaderEntityId, out var leader)
                || leader.EntityId == monster.EntityId
                || !leader.Alive
                || leader.LevelId != monster.LevelId) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains an invalid monster pack leader.");
            }
        }
    }

    private static void ValidateMonsterMovementState(MonsterSnapshot monster)
    {
        // Movement validation runs before entity-coordinate validation, so
        // widen network-derived deltas before subtracting malformed values.
        var futureDeltaX = Math.Abs((long)monster.FuturePositionX - monster.PositionX);
        var futureDeltaY = Math.Abs((long)monster.FuturePositionY - monster.PositionY);
        if ((monster.Moving
                && Math.Max(futureDeltaX, futureDeltaY) != 1)
            || (!monster.Moving
                && (monster.FuturePositionX != 0 || monster.FuturePositionY != 0)
                && (monster.FuturePositionX != monster.PositionX
                    || monster.FuturePositionY != monster.PositionY))) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid monster occupancy state.");
        }
        if ((!monster.Moving
                && (monster.MovementStartedTick != 0
                    || monster.MovementEffectTick != 0))
            || (monster.MovementStartedTick != 0
                || monster.MovementEffectTick != 0)
                && monster.MovementEffectTick <= monster.MovementStartedTick) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid monster movement timing.");
        }
    }

    private static void ValidatePlayerSpells(PlayerSnapshot player)
    {
        if (player.AbilitySpellId > MaximumStableSpellId
            || player.MemorizedSpells.Count > MaximumMemorizedSpellCount) {
            throw new InvalidDataException(
                "The authoritative snapshot contains out-of-range player spell state.");
        }

        var spellIds = new HashSet<uint>();
        foreach (var spell in player.MemorizedSpells) {
            if (spell is null
                || spell.SpellId == 0
                || spell.SpellId > MaximumStableSpellId
                || spell.Level == 0
                || spell.Level > MaximumNativeSpellLevel
                || !Enum.IsDefined(spell.TargetingKind)
                || spell.DisplayName.Contains('\0')
                || !spellIds.Add(spell.SpellId)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid or duplicate memorized spells.");
            }
        }
    }

    private static void ValidateProjectile(ProjectileSnapshot projectile)
    {
        if (projectile.SourceEntityId == 0
            || (projectile.SpellId == 0 && !IsWeaponProjectile(projectile.Behavior))
            || projectile.RemainingTicks == 0
            || projectile.Damage < 0
            || projectile.AreaRadius < 0
            || projectile.DamageMinimum < 0
            || projectile.DamageMaximum < projectile.DamageMinimum
            || projectile.DamageQ6 < 0
            || projectile.ElementalFireDamage < 0
            || projectile.ElementalLightningDamage < 0
            || projectile.PerilSelfDamageQ6 < 0
            || projectile.EffectiveSpellLevel < 0
            || projectile.DamageType is < 0 or > MaximumProjectileDamageType
            || projectile.Behavior is < 0 or > MaximumProjectileBehavior
            || projectile.Phase is < 0 or > MaximumProjectilePhase
            || (int)projectile.PresentationKind is < 0 or > MaximumProjectilePresentationKind
            || !Enum.IsDefined(projectile.PresentationKind)
            || (int)projectile.NativeVisual is < 0 or > MaximumNativeProjectileVisual
            || !Enum.IsDefined(projectile.NativeVisual)
            || UsesDirectionalPresentation(projectile)
                && projectile.OriginalDirection is < 0 or > MaximumNativeMonsterDirection
            || !IsValidNativeMissileIdentity(projectile)) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid projectile state.");
        }
    }

    private static bool UsesDirectionalPresentation(ProjectileSnapshot projectile)
    {
        if (projectile.NativeVisual != NativeProjectileVisual.Unspecified
            || !string.IsNullOrEmpty(projectile.NativeMissileId))
            return true;

        return (AuthoritativeProjectileBehavior)projectile.Behavior is
            AuthoritativeProjectileBehavior.ChargedBolt
                or AuthoritativeProjectileBehavior.Firebolt
                or AuthoritativeProjectileBehavior.Lightning
                or AuthoritativeProjectileBehavior.LightningSegment
                or AuthoritativeProjectileBehavior.FlashBottom
                or AuthoritativeProjectileBehavior.FlashTop
                or AuthoritativeProjectileBehavior.ResurrectionBeam
                or AuthoritativeProjectileBehavior.Phasing
                or AuthoritativeProjectileBehavior.Fireball
                or AuthoritativeProjectileBehavior.HolyBolt
                or AuthoritativeProjectileBehavior.BloodStar
                or AuthoritativeProjectileBehavior.BoneSpirit
                or AuthoritativeProjectileBehavior.FireWallSegment
                or AuthoritativeProjectileBehavior.FlameWaveController
                or AuthoritativeProjectileBehavior.FlameWaveSegment
                or AuthoritativeProjectileBehavior.InfernoSegment
                or AuthoritativeProjectileBehavior.ChainLightningBolt
                or AuthoritativeProjectileBehavior.NovaBall
                or AuthoritativeProjectileBehavior.ApocalypseBoom
                or AuthoritativeProjectileBehavior.Elemental
                or AuthoritativeProjectileBehavior.GuardianController
                or AuthoritativeProjectileBehavior.Etherealize
                or AuthoritativeProjectileBehavior.Acid
                or AuthoritativeProjectileBehavior.AcidSplat
                or AuthoritativeProjectileBehavior.AcidPuddle
                or AuthoritativeProjectileBehavior.MonsterChargedBolt
                or AuthoritativeProjectileBehavior.HorkSpawn
                or AuthoritativeProjectileBehavior.WeaponArrow
                or AuthoritativeProjectileBehavior.MonsterArrow;
    }

    private static bool IsWeaponProjectile(int behavior)
        => (AuthoritativeProjectileBehavior)behavior is
            AuthoritativeProjectileBehavior.WeaponArrow
                or AuthoritativeProjectileBehavior.MonsterArrow;

    private static bool IsValidNativeMissileIdentity(ProjectileSnapshot projectile)
    {
        if (string.IsNullOrEmpty(projectile.NativeMissileId))
            return true;
        if ((AuthoritativeProjectileBehavior)projectile.Behavior
                != AuthoritativeProjectileBehavior.MonsterMagic
            || !AuthoritativeProtocolLimits.IsIdentityWithinLimit(projectile.NativeMissileId)
            || !IsAsciiLetter(projectile.NativeMissileId[0])
                && projectile.NativeMissileId[0] != '_') {
            return false;
        }
        return projectile.NativeMissileId.Skip(1).All(character =>
            IsAsciiLetter(character)
                || character is >= '0' and <= '9'
                || character is '_' or '-');
    }

    private static bool IsAsciiLetter(char character)
        => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool HasValidItemIdentity(
        uint itemSeed,
        ItemStateSnapshot? state)
    {
        if (state is null)
            return false;
        var hasPresentation = !string.IsNullOrEmpty(state.BaseName)
            || !string.IsNullOrEmpty(state.IdentifiedName)
            || state.HasNativeCursorGraphic;
        if (!hasPresentation)
            return itemSeed != 0;
        var hasValidNames = !string.IsNullOrEmpty(state.BaseName)
            && !string.IsNullOrEmpty(state.IdentifiedName)
            && !state.BaseName.Contains('\0')
            && !state.IdentifiedName.Contains('\0')
            && System.Text.Encoding.UTF8.GetByteCount(state.BaseName) < 64
            && System.Text.Encoding.UTF8.GetByteCount(state.IdentifiedName) < 64;
        return hasValidNames
            && state.HasNativeCursorGraphic
            && state.NativeCursorGraphic is >= 0 and <= byte.MaxValue;
    }

    private readonly record struct PredictedMove(int DirectionX, int DirectionY);
}

public readonly record struct ClientPosition(int X, int Y);

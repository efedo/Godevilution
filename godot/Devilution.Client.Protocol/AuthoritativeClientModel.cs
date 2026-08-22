using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Main-thread authoritative projection consumed by Godot presentation nodes. */
public sealed class AuthoritativeClientModel
{
    private const int MaxPredictedMoves = 16;
    private const int MaxRecentEvents = 256;
    private const uint MaximumStableSpellId = byte.MaxValue;
    private const uint MaximumNativeSpellLevel = 15;
    private const int MaximumMemorizedSpellCount = 51;
    private const uint MaximumNativeCharacterLevel = 50;
    private const uint MaximumNativeReflectCharges = ushort.MaxValue;
    private const int MaximumProjectileDamageType = 4;
    private const int MaximumProjectileBehavior = 59;
    private const int MaximumProjectilePhase = 1;
    private const int MaximumNativeMonsterDirection = 7;
    private const int MaximumNativeMonsterDeathAnimationTicks = 1024;
    private const uint MaximumNativeMonsterExperienceReward = 1_000_000;
    private const int MaximumNativeMonsterToHit = 255;
    private const int MaximumNativeMonsterIntelligence = 255;
    private const int MaximumObjectEffectKind = 9;
    private readonly List<GameEvent> recentEvents = [];
    private readonly Dictionary<ulong, PredictedMove> predictedMoves = [];
    private ulong recentEventsStartSequence;
    private ulong recentEventsEndSequence;

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
            && incomingSnapshot.Tick < currentSnapshot.Tick)
            message = message with { Snapshot = null };
        if (message.Snapshot is not null) {
            if (message.Snapshot.Players.Count == 0)
                throw new InvalidDataException("The authoritative snapshot has no local player.");
            var entityIds = new HashSet<uint>();
            foreach (var player in message.Snapshot.Players) {
                ValidateEntityId(entityIds, player.EntityId);
                if (player.CharacterName.Length > 31
                    || player.CharacterName.Any(character => character is < (char)0x21 or > (char)0x7e)) {
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
                ValidatePlayerResources(player);
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
                        || heldItem.State.InventoryWidth > 10
                        || heldItem.State.InventoryHeight > 4)) {
                    throw new InvalidDataException(
                        "The authoritative snapshot contains an invalid held item.");
                }
                ValidateStatusEffects(player);
                ValidatePlayerAction(player);
            }
            ValidateActiveStore(message.Snapshot);
            foreach (var monster in message.Snapshot.Monsters) {
                ValidateEntityId(entityIds, monster.EntityId);
                ValidateMonsterVitality(monster);
                ValidateMonsterCombatState(monster);
                ValidateMonsterMovementState(monster);
            }
            foreach (var item in message.Snapshot.WorldItems) {
                ValidateEntityId(entityIds, item.EntityId);
                ValidateItemState(item.State);
                if (!HasValidItemIdentity(item.ItemSeed, item.State)) {
                    throw new InvalidDataException(
                        "The authoritative snapshot contains an invalid world item.");
                }
            }
            ValidateQuests(message.Snapshot);
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
            ValidateEntityPositions(message.Snapshot);
            ValidateQuestTownerInteractions(message.Snapshot);
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
            Snapshot = message.Snapshot;
            LastError = null;
            RebuildPredictedPosition();
        }

        if (message.Events is { } events
            && events.Tick >= CurrentTick) {
            recentEvents.AddRange(events.Events);
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
            (x, y) = (x + move.DirectionX, y + move.DirectionY);
        }

        IsCorrectingPosition = x != player.PositionX || y != player.PositionY;
        PredictedPlayerPosition = IsCorrectingPosition ? new ClientPosition(x, y) : null;
    }

    private static void ValidatePlayerAction(PlayerSnapshot player)
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
                && action.Cast is null,
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

    private static void ValidateStatusEffects(PlayerSnapshot player)
    {
        if (player.StatusEffects.Count > 128) {
            throw new InvalidDataException(
                "The authoritative snapshot contains too many status effects.");
        }
        var effectIds = new HashSet<uint>();
        foreach (var effect in player.StatusEffects) {
            if (effect.EffectId == 0
                || effect.RemainingTicks == 0
                || !Enum.IsDefined(effect.Behavior)
                || ((effect.Behavior is StatusEffectBehavior.Infravision
                        or StatusEffectBehavior.Search)
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
        if (player.Inventory.Count > 40
            || player.Belt.Count > 8
            || player.InventoryGrid.Count != 0 && player.InventoryGrid.Count != 40
            || player.InventoryGrid.Any(cell => cell < -1 || cell >= player.Inventory.Count)) {
            throw new InvalidDataException(
                "The authoritative snapshot contains an invalid inventory topology.");
        }

        var equipmentSlots = new HashSet<uint>();
        foreach (var item in player.Equipment) {
            if (item is null || item.Slot >= 7 || !equipmentSlots.Add(item.Slot)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid or duplicate equipment slots.");
            }
        }

        var beltSlots = new HashSet<uint>();
        foreach (var item in player.Belt) {
            if (item is null || item.Slot >= 8 || !beltSlots.Add(item.Slot)) {
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
        var column = anchor % 10;
        var row = anchor / 10;
        if (column + width > 10 || row + height > 4)
            return false;
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                var cell = (row + y) * 10 + column + x;
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
        foreach (var item in player.Inventory)
            ValidatePlayerItem(item?.ItemSeed ?? 0, item?.State, itemSeeds);
        foreach (var item in player.Belt)
            ValidatePlayerItem(item?.ItemSeed ?? 0, item?.State, itemSeeds);
        foreach (var item in player.Equipment)
            ValidatePlayerItem(item?.ItemSeed ?? 0, item?.State, itemSeeds);
        foreach (var item in player.Stash)
            if (item?.Item is { } stashItem)
                ValidatePlayerItem(stashItem.ItemSeed, stashItem.State, itemSeeds);
        if (player.HeldItem is { } heldItem)
            ValidatePlayerItem(heldItem.ItemSeed, heldItem.State, itemSeeds);
    }

    private static void ValidatePlayerItem(
        uint itemSeed,
        ItemStateSnapshot? state,
        ISet<uint> itemSeeds)
    {
        if (itemSeed != 0 && !itemSeeds.Add(itemSeed))
            throw new InvalidDataException(
                "The authoritative snapshot contains duplicate player item references.");
        ValidateInventoryFootprint(state);
        ValidateItemState(state);
    }

    private static void ValidateInventoryFootprint(ItemStateSnapshot? state)
    {
        if (state is null)
            return;
        if (state.InventoryWidth > 10 || state.InventoryHeight > 4) {
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
            || state.LightningMaxDamage < state.LightningMinDamage) {
            throw new InvalidDataException(
                "The authoritative snapshot contains impossible item state ranges.");
        }
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
            || player.CharacterLevel > MaximumNativeCharacterLevel) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid player resources.");
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
        if (monster.EntityId == 0
            || monster.HitPoints < 0
            || monster.MaxHitPoints < 0
            || monster.MaxHitPoints > 0 && monster.HitPoints > monster.MaxHitPoints
            || monster.HitPointsQ6 < 0
            || monster.MaxHitPointsQ6 < 0
            || monster.MaxHitPointsQ6 > 0 && monster.HitPointsQ6 > monster.MaxHitPointsQ6) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid monster vitality.");
        }
        if (monster.DialogueMessageId < 0
            || monster.DialogueMessageId > short.MaxValue
            || monster.DialogueMessageId != 0 && !monster.IsUnique) {
            throw new InvalidDataException(
                "The authoritative snapshot contains an invalid monster dialogue identity.");
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
            : @object.RewardId != 0;
        var producesProjectile = effectKind is 8 or 9
            || followUpKind == 2;
        var followUpValid = followUpKind switch {
            0 => @object.QuestSequenceFollowUpAmount == 0
                && @object.QuestSequenceFollowUpOffsetX == 0
                && @object.QuestSequenceFollowUpOffsetY == 0,
            1 => effectKind == 4
                && @object.QuestSequenceFollowUpAmount != 0
                && @object.QuestSequenceFollowUpOffsetX == 0
                && @object.QuestSequenceFollowUpOffsetY == 0,
            2 => (effectKind == 4 || effectKind == 6)
                && @object.QuestSequenceFollowUpAmount != 0,
            3 => effectKind == 4
                && @object.QuestSequenceFollowUpAmount != 0,
            _ => false,
        };
        if (@object.ObjectId == 0
            || @object.EffectAmount < 0
            || effectKind < 0
            || effectKind > MaximumObjectEffectKind
            || !Enum.IsDefined(@object.PresentationKind)
            || @object.QuestId != 0 && !questIds.Contains(@object.QuestId)
            || transitionKind is < 0 or > 8
            || @object.QuestTransitionAmount is < 0 or > byte.MaxValue
            || @object.QuestTransitionRequiredVariable1 is < -1 or > byte.MaxValue
            || @object.QuestTransitionRequiredVariable2 is < -1 or > byte.MaxValue
            || !transitionValid
            || rewardKind is < 0 or > 2
            || @object.RewardItemLevel is < 0 or > byte.MaxValue
            || @object.RewardOffsetX is < -16 or > 16
            || @object.RewardOffsetY is < -16 or > 16
            || !rewardValid
            || @object.ProjectileTargetOffsetX is < -32 or > 32
            || @object.ProjectileTargetOffsetY is < -32 or > 32
            || !producesProjectile
                && (@object.ProjectileTargetOffsetX != 0
                    || @object.ProjectileTargetOffsetY != 0)
            || followUpKind is < 0 or > 3
            || @object.QuestSequenceFollowUpOffsetX is < -32 or > 32
            || @object.QuestSequenceFollowUpOffsetY is < -32 or > 32
            || !followUpValid
            || @object.ProjectileSourceKind is < 0 or > 1
            || !producesProjectile && @object.ProjectileSourceKind != 0
            || @object.Breakable && @object.MissilePassable
            || @object.Destroyed && !@object.Breakable) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid object effect metadata.");
        }
    }

    private static void ValidateQuests(Snapshot snapshot)
    {
        if (snapshot.Quests.Count > 4096) {
            throw new InvalidDataException(
                "The authoritative snapshot contains too many quests.");
        }

        var questIds = new HashSet<uint>();
        foreach (var quest in snapshot.Quests) {
            if (quest.QuestId == 0
                || !questIds.Add(quest.QuestId)
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
                || quest.CompletionUniqueIdentity.Length > 128) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid quest state.");
            }
        }
    }

    private static void ValidateQuestTownerInteractions(Snapshot snapshot)
    {
        if (snapshot.QuestTownerInteractions.Count > 1024) {
            throw new InvalidDataException(
                "The authoritative snapshot contains too many quest/towner interactions.");
        }
        var questIds = snapshot.Quests
            .Select(quest => quest.QuestId)
            .ToHashSet();
        var levelGeometry = snapshot.LevelGeometry
            .ToDictionary(level => level.LevelId);
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
                || interaction.PositionX < 0
                || interaction.PositionY < 0
                || interaction.Kind is < 0 or > 3
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

    private static void ValidateEntityPositions(Snapshot snapshot)
    {
        var levels = snapshot.LevelGeometry.ToDictionary(level => level.LevelId);
        bool IsInBounds(uint levelId, int x, int y)
        {
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

    private static void ValidateWarpTriggers(Snapshot snapshot)
    {
        var levels = new Dictionary<uint, LevelGeometrySnapshot>();
        foreach (var level in snapshot.LevelGeometry) {
            if (level.Width < 0
                || level.Height < 0
                || level.NativeTransparency.Any(value => value > ushort.MaxValue)
                || level.NativeObjectLayer.Any(value => value > ushort.MaxValue)
                || level.NativeMegaTileLayer.Any(value => value > ushort.MaxValue)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains invalid level geometry dimensions.");
            }
            if (level.Width > 0 && level.Height > 0) {
                var cellCount = checked((long)level.Width * level.Height);
                if (level.BlockedCells.Count > cellCount
                    || level.MissileBlockedCells.Count > cellCount
                    || level.BlockedCells.Any(cell => cell < 0 || cell >= cellCount)
                    || level.MissileBlockedCells.Any(cell => cell < 0 || cell >= cellCount)
                    || level.BlockedCells.Distinct().Count() != level.BlockedCells.Count
                    || level.MissileBlockedCells.Distinct().Count()
                        != level.MissileBlockedCells.Count
                    || level.NativeTransparency.Count != 0
                        && level.NativeTransparency.Count != cellCount
                    || level.NativeObjectLayer.Count != 0
                        && level.NativeObjectLayer.Count != cellCount
                    || level.NativeMegaTileLayer.Count != 0
                        && level.NativeMegaTileLayer.Count
                            != ((level.Width + 1L) / 2) * ((level.Height + 1L) / 2)) {
                    throw new InvalidDataException(
                        "The authoritative snapshot contains invalid level geometry cells.");
                }
            }
            if (!levels.TryAdd(level.LevelId, level)) {
                throw new InvalidDataException(
                    "The authoritative snapshot contains duplicate level geometry.");
            }
        }

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
        if (player.VisitedLevelIds.Count > 256
            || player.VisitedLevelIds.Distinct().Count() != player.VisitedLevelIds.Count) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid visited-level state.");
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
            || monster.FireResistance is < -100 or > 100
            || monster.LightningResistance is < -100 or > 100
            || monster.MagicResistance is < -100 or > 100
            || monster.AcidResistance is < -100 or > 100
            || (!monster.IsPlayerMinion && monster.OwnerPlayerId != 0)) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid monster combat state.");
        }
    }

    private static void ValidateMonsterMovementState(MonsterSnapshot monster)
    {
        var futureDeltaX = Math.Abs(monster.FuturePositionX - monster.PositionX);
        var futureDeltaY = Math.Abs(monster.FuturePositionY - monster.PositionY);
        if ((monster.Moving
                && Math.Max(futureDeltaX, futureDeltaY) != 1)
            || (!monster.Moving
                && (monster.FuturePositionX != monster.PositionX
                    || monster.FuturePositionY != monster.PositionY))) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid monster occupancy state.");
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
            || (projectile.SpellId == 0 && projectile.Behavior is not (58 or 59))
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
            || !Enum.IsDefined(projectile.PresentationKind)
            || !Enum.IsDefined(projectile.NativeVisual)) {
            throw new InvalidDataException(
                "The authoritative snapshot contains invalid projectile state.");
        }
    }

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
            && (!state.HasNativeCursorGraphic
                || state.NativeCursorGraphic is >= 0 and <= byte.MaxValue);
    }

    private readonly record struct PredictedMove(int DirectionX, int DirectionY);
}

public readonly record struct ClientPosition(int X, int Y);

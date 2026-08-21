using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Main-thread authoritative projection consumed by Godot presentation nodes. */
public sealed class AuthoritativeClientModel
{
    private readonly List<GameEvent> recentEvents = [];
    private readonly Dictionary<ulong, PredictedMove> predictedMoves = [];

    public Snapshot? Snapshot { get; private set; }

    public string? LastError { get; private set; }

    public CommandResult? LastCommandResult { get; private set; }

    public IReadOnlyList<GameEvent> RecentEvents => recentEvents;

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

    /** Returns an available authored quest/towner interaction at a clicked tile. */
    public QuestTownerInteractionSnapshot? QuestTownerInteractionAt(int x, int y)
        => AuthoritativeQuestTownerInteractionSelector.SelectAt(Snapshot, x, y);

    public ulong CurrentTick => Snapshot?.Tick ?? 0;

    /** The server puts the receiving participant first in every snapshot. */
    public uint PlayerEntityId => Snapshot?.Players.FirstOrDefault()?.EntityId ?? 0;

    public ulong NextCommandTick => checked(CurrentTick + 1);

    public ClientPosition? PredictedPlayerPosition { get; private set; }

    public bool IsCorrectingPosition { get; private set; }

    public void Apply(AuthoritativeClientMessage message)
    {
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
            foreach (var monster in message.Snapshot.Monsters) {
                ValidateEntityId(entityIds, monster.EntityId);
                ValidateMonsterVitality(monster);
            }
            foreach (var item in message.Snapshot.WorldItems) {
                ValidateEntityId(entityIds, item.EntityId);
                if (!HasValidItemIdentity(item.ItemSeed, item.State)) {
                    throw new InvalidDataException(
                        "The authoritative snapshot contains an invalid world item.");
                }
            }
            foreach (var @object in message.Snapshot.Objects)
                ValidateEntityId(entityIds, @object.EntityId);
            foreach (var projectile in message.Snapshot.Projectiles)
                ValidateEntityId(entityIds, projectile.EntityId);
            foreach (var corpse in message.Snapshot.Corpses)
                ValidateEntityId(entityIds, corpse.EntityId);
            ValidateWarpTriggers(message.Snapshot);
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
            Snapshot = message.Snapshot;
            LastError = null;
            recentEvents.Clear();
            RebuildPredictedPosition();
        }

        if (message.Events is not null)
            recentEvents.AddRange(message.Events.Events);

        if (message.Error is not null)
            LastError = message.Error.Detail;

        if (message.Acknowledgement is not null) {
            if (message.Acknowledgement.Results.Count > 0)
                LastCommandResult = message.Acknowledgement.Results[^1].Clone();
            foreach (var result in message.Acknowledgement.Results)
                predictedMoves.Remove(result.ClientSequence);
            RebuildPredictedPosition();
        }
    }

    public void ClearRecentEvents()
    {
        recentEvents.Clear();
    }

    public void TrackPredictedMove(ulong sequence, int directionX, int directionY)
    {
        if (sequence == 0 || (directionX == 0 && directionY == 0))
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
        foreach (var move in predictedMoves.Values)
            (x, y) = (x + move.DirectionX, y + move.DirectionY);

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

    private static void ValidatePlayerResources(PlayerSnapshot player)
    {
        if (player.Life < 0
            || player.LifeMaximum < 0
            || player.LifeMaximum > 0 && player.Life > player.LifeMaximum
            || player.Mana < 0
            || player.ManaMaximum < 0
            || player.ManaMaximum > 0 && player.Mana > player.ManaMaximum) {
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
    }

    private static void ValidateQuestTownerInteractions(Snapshot snapshot)
    {
        if (snapshot.QuestTownerInteractions.Count > 1024) {
            throw new InvalidDataException(
                "The authoritative snapshot contains too many quest/towner interactions.");
        }
        var interactionIds = new HashSet<uint>();
        foreach (var interaction in snapshot.QuestTownerInteractions) {
            if (interaction.InteractionId == 0
                || !interactionIds.Add(interaction.InteractionId)
                || interaction.QuestId == 0
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

    private static void ValidateWarpTriggers(Snapshot snapshot)
    {
        var levels = new Dictionary<uint, LevelGeometrySnapshot>();
        foreach (var level in snapshot.LevelGeometry) {
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

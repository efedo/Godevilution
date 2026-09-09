using System.Text.Json;
using Devilution.Diablo;
using Devilution.Protocol.V1;
using ProtocolLimits = Devilution.Protocol.AuthoritativeProtocolLimits;

namespace Devilution.Server.Replay;

/** Strict loader for the versioned cross-language replay fixture envelope. */
public static class ReplayFixtureLoader
{
    private const int NativeMaximumInventoryCount = ProtocolLimits.NativeMaximumInventoryCount;
    private const int NativeMaximumEquipmentCount = ProtocolLimits.NativeMaximumEquipmentCount;
    private const int NativeMaximumBeltCount = ProtocolLimits.NativeMaximumBeltCount;
    private const int NativeMaximumInventoryGridCount = ProtocolLimits.NativeMaximumInventoryGridCount;
    private const int MaximumNativeMonsterToHit = ProtocolLimits.MaximumNativeMonsterToHit;
    private const int MaximumNativeMonsterIntelligence = ProtocolLimits.MaximumNativeMonsterIntelligence;
    private const int MaximumNativeMonsterDirection = ProtocolLimits.MaximumNativeMonsterDirection;
    private const int MaximumNativeMonsterActiveTicks = ProtocolLimits.MaximumNativeMonsterActiveTicks;
    private const int MaximumNativeMonsterFallenStandAnimationFrame = ProtocolLimits.MaximumNativeMonsterFallenStandAnimationFrame;
    private const int MaximumNativeMonsterFallenStandAnimationTick = ProtocolLimits.MaximumNativeMonsterFallenStandAnimationTick;
    private const int MaximumNativeMonsterFallenRallyTicks = ProtocolLimits.MaximumNativeMonsterFallenRallyTicks;
    private const int MaximumNativeMonsterAiTimerTicks = ProtocolLimits.MaximumNativeMonsterAiTimerTicks;
    private const int MaximumNativeMonsterShortTimerTicks = ProtocolLimits.MaximumNativeMonsterShortTimerTicks;
    private const int MaximumNativeMonsterRetreatTicks = ProtocolLimits.MaximumNativeMonsterRetreatTicks;
    private const int MaximumNativeMonsterMovementMode = ProtocolLimits.MaximumNativeMonsterMovementMode;
    private const int MinimumNativeMonsterResistance = ProtocolLimits.MinimumNativeMonsterResistance;
    private const int MaximumNativeMonsterResistance = ProtocolLimits.MaximumNativeMonsterResistance;
    private const uint MaximumNativeCharacterLevel = ProtocolLimits.MaximumNativeCharacterLevel;

    public static ReplayFixture Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var fixture = new ReplayFixture(
            RequiredInt(root, "format_version"),
            RequiredString(root, "fixture_id"),
            RequiredString(root, "protocol_schema_version"),
            RequiredUInt(root, "tick_rate_hz"),
            RequiredULong(root, "rng_seed"),
            ParseInitialState(root.GetProperty("initial_state")),
            ParseCommands(root.GetProperty("commands")),
            ParseCheckpoints(root.GetProperty("checkpoints"))) {
            InitialProjectiles = ParseInitialProjectiles(root),
            LegacyStoreState = ParseLegacyStoreState(root),
            ContentManifest = ParseContentManifest(root),
            QuestSelection = ParseQuestSelection(root),
            FinalStateSha256 = OptionalString(root, "final_state_sha256"),
            GameVariant = ParseGameVariant(root),
            GameMode = ParseGameMode(root),
            Difficulty = ParseDifficulty(root),
            PlayerActionTiming = ParsePlayerActionTiming(root),
        };

        if (fixture.FormatVersion != 1)
            throw new InvalidDataException($"Unsupported replay fixture format {fixture.FormatVersion}.");
        if (fixture.TickRateHz == 0)
            throw new InvalidDataException("Replay fixture tick rate must be non-zero.");
        if (fixture.RngSeed > uint.MaxValue)
            throw new InvalidDataException("Replay fixture RNG seed must fit the native 32-bit stream.");
        if (fixture.InitialState.Life < 0
            || fixture.InitialState.LifeMaximum < fixture.InitialState.Life
            || !Enum.IsDefined(fixture.InitialState.FacingDirection)
            || (!string.IsNullOrEmpty(fixture.InitialState.CharacterName)
                && !DiabloPlayerName.IsValid(fixture.InitialState.CharacterName))) {
            throw new InvalidDataException(
                "Replay fixture contains invalid initial player life.");
        }
        if ((fixture.InitialState.GoldStackSeed == 0
                && fixture.InitialState.GoldStackCell != 0)
            || (fixture.InitialState.GoldStackSeed != 0
                && (fixture.InitialState.Gold == 0
                    || fixture.InitialState.GoldStackCell >= (uint)NativeMaximumInventoryGridCount))) {
            throw new InvalidDataException(
                "Replay fixture contains invalid initial Gold-stack topology.");
        }
        if (fixture.InitialState.InventoryFillerCount >= (uint)NativeMaximumInventoryCount
            || (fixture.InitialState.InventoryFillerCount != 0
                && fixture.InitialState.GoldStackSeed == 0)) {
            throw new InvalidDataException(
                "Replay fixture contains invalid inventory filler state.");
        }
        if ((fixture.InitialState.ObjectBreakable && fixture.InitialState.ObjectMissilePassable)
            || (fixture.InitialState.ObjectDestroyed && !fixture.InitialState.ObjectBreakable)
            || (fixture.InitialState.ObjectEntityId == 0
                && (fixture.InitialState.ObjectMissilePassable
                    || fixture.InitialState.ObjectBreakable
                    || fixture.InitialState.ObjectDestroyed
                    || fixture.InitialState.ObjectQuestId != 0
                    || fixture.InitialState.ObjectEffectKind != 0
                    || fixture.InitialState.ObjectEffectAmount != 0))
            || fixture.InitialState.ObjectEffectKind is < (int)AuthoritativeObjectEffectKind.None
                or > (int)AuthoritativeObjectEffectKind.Experience
            || fixture.InitialState.ObjectEffectAmount < 0) {
            throw new InvalidDataException(
                "Replay fixture contains invalid object state.");
        }
        if (fixture.InitialState.WarpId == 0
            && (fixture.InitialState.WarpSourceLevelId != 0
                || fixture.InitialState.WarpTriggerX != 0
                || fixture.InitialState.WarpTriggerY != 0
                || fixture.InitialState.WarpTargetLevelId != 0
                || fixture.InitialState.WarpTargetX != 0
                || fixture.InitialState.WarpTargetY != 0)) {
            throw new InvalidDataException(
                "Replay fixture Warp state requires a nonzero warp identity.");
        }
        var initialTarget = fixture.InitialState;
        if ((initialTarget.CombatTargetMonsterItemSeed == 0)
            != (initialTarget.CombatTargetMonsterAiSeed == 0)) {
            throw new InvalidDataException(
                "Replay fixture monster runtime seeds must be supplied together.");
        }
        // Match the native parser's validation precedence when a companion is
        // present on an otherwise invalid zero-entity combat target.
        if (initialTarget.CombatTargetEntityId == 0
            && (initialTarget.CombatTargetPositionX != 0
                || initialTarget.CombatTargetPositionY != 0
                || initialTarget.CombatTargetHitPoints != 0
                || initialTarget.CombatTargetMaxHitPoints != 0
                || initialTarget.CombatTargetLevelId != 0)) {
            throw new InvalidDataException(
                "Replay fixture contains invalid combat-target occupancy state.");
        }
        if (initialTarget.CombatTargetCompanion is { } companion
            && (initialTarget.CombatTargetEntityId == 0
                || companion.EntityId == 0
                || companion.EntityId == initialTarget.CombatTargetEntityId
                || companion.HitPoints < 0
                || companion.MaxHitPoints < companion.HitPoints
                || companion.LevelId != initialTarget.CombatTargetLevelId
                || companion.Intelligence is < 0 or > MaximumNativeMonsterIntelligence
                || companion.FallenStandAnimationFrame is < 0 or > MaximumNativeMonsterFallenStandAnimationFrame
                || companion.FallenStandAnimationTick is < 0 or > MaximumNativeMonsterFallenStandAnimationTick
                || companion.FallenRallyTicks is < 0 or > MaximumNativeMonsterFallenRallyTicks
                || !string.Equals(companion.NativeAi, "Fallen", StringComparison.Ordinal))) {
            throw new InvalidDataException(
                "Replay fixture contains invalid combat-target companion state.");
        }
        if ((initialTarget.CombatTargetEntityId == 0
                && (initialTarget.CombatTargetPositionX != 0
                    || initialTarget.CombatTargetPositionY != 0
                    || initialTarget.CombatTargetHitPoints != 0
                    || initialTarget.CombatTargetMaxHitPoints != 0
                    || initialTarget.CombatTargetArmorClass != 0
                    || initialTarget.CombatTargetAttackDamage != 0
                    || initialTarget.CombatTargetAggroRange != 0
                    || !string.IsNullOrEmpty(initialTarget.CombatTargetNativeAi)
                    || initialTarget.CombatTargetMonsterItemSeed != 0
                    || initialTarget.CombatTargetMonsterAiSeed != 0
                    || initialTarget.CombatTargetMonsterId != 0
                    || initialTarget.CombatTargetToHit != 0
                    || initialTarget.CombatTargetToHitSpecial != 0
                    || initialTarget.CombatTargetMinDamage != 0
                    || initialTarget.CombatTargetMaxDamage != 0
                    || initialTarget.CombatTargetMinDamageSpecial != 0
                    || initialTarget.CombatTargetMaxDamageSpecial != 0
                    || initialTarget.CombatTargetIntelligence != 0
                    || initialTarget.CombatTargetFallenStandAnimationFrame != 0
                    || initialTarget.CombatTargetFallenStandAnimationTick != 0
                    || initialTarget.CombatTargetFallenRallyTicks != 0
                    || initialTarget.CombatTargetActiveForTicks != 0
                    || initialTarget.CombatTargetGargoyleSpecialActive
                    || initialTarget.CombatTargetHorkDemonDelayTicks != 0
                    || initialTarget.CombatTargetSkeletonKingDelayTicks != 0
                    || initialTarget.CombatTargetSkeletonMeleeDelayTicks != 0
                    || initialTarget.CombatTargetSnakeDelayTicks != 0
                    || initialTarget.CombatTargetCounselorDelayTicks != 0
                    || initialTarget.CombatTargetCounselorRetreatTicks != 0
                    || initialTarget.CombatTargetRangedDelayTicks != 0
                    || initialTarget.CombatTargetMegaDelayTicks != 0
                    || initialTarget.CombatTargetSkeletonRangedMovementIdleTicks != -1
                    || initialTarget.CombatTargetSkeletonRangedMovementMode != 0
                    || initialTarget.CombatTargetSneakHidden
                    || initialTarget.CombatTargetSneakRetreatTicks != 0
                    || initialTarget.CombatTargetBatRetreatTicks != 0
                    || initialTarget.CombatTargetBatRetreatDirection != 0
                    || initialTarget.CombatTargetScavengerHealingTicks != 0
                    || initialTarget.CombatTargetScavengerCorpseEntityId != 0
                    || initialTarget.CombatTargetFatMovementIdleTicks != -1
                    || initialTarget.CombatTargetFatMovementMode != 0
                    || initialTarget.CombatTargetFireResistance != 0
                    || initialTarget.CombatTargetLightningResistance != 0
                    || initialTarget.CombatTargetMagicResistance != 0
                    || initialTarget.CombatTargetAcidResistance != 0
                    || initialTarget.CombatTargetMonsterLevel != 0
                    || initialTarget.CombatTargetUndead
                    || initialTarget.CombatTargetDemon
                    || initialTarget.CombatTargetAnimal
                    || initialTarget.CombatTargetLevelId != 0
                    || initialTarget.CombatTargetMoving
                    || initialTarget.CombatTargetPetrified))
            || initialTarget.CombatTargetHitPoints < 0
            || initialTarget.CombatTargetMaxHitPoints
                < initialTarget.CombatTargetHitPoints
            || initialTarget.CombatTargetArmorClass < 0
            || initialTarget.CombatTargetAttackDamage < 0
            || initialTarget.CombatTargetAggroRange < 0
            || initialTarget.CombatTargetToHit is < 0 or > MaximumNativeMonsterToHit
            || initialTarget.CombatTargetToHitSpecial is < 0 or > MaximumNativeMonsterToHit
            || initialTarget.CombatTargetMinDamage < 0
            || initialTarget.CombatTargetMaxDamage < initialTarget.CombatTargetMinDamage
            || initialTarget.CombatTargetMinDamageSpecial < 0
            || initialTarget.CombatTargetMaxDamageSpecial < initialTarget.CombatTargetMinDamageSpecial
            || initialTarget.CombatTargetIntelligence is < 0 or > MaximumNativeMonsterIntelligence
            || initialTarget.CombatTargetFallenStandAnimationFrame is < 0 or > MaximumNativeMonsterFallenStandAnimationFrame
            || initialTarget.CombatTargetFallenStandAnimationTick is < 0 or > MaximumNativeMonsterFallenStandAnimationTick
            || initialTarget.CombatTargetFallenRallyTicks is < 0 or > MaximumNativeMonsterFallenRallyTicks
            || initialTarget.CombatTargetActiveForTicks is < 0 or > MaximumNativeMonsterActiveTicks
            || initialTarget.CombatTargetHorkDemonDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
            || initialTarget.CombatTargetSkeletonKingDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
            || initialTarget.CombatTargetSkeletonMeleeDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
            || initialTarget.CombatTargetSnakeDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
            || initialTarget.CombatTargetCounselorDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
            || initialTarget.CombatTargetCounselorRetreatTicks is < 0 or > MaximumNativeMonsterRetreatTicks
            || initialTarget.CombatTargetRangedDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
            || initialTarget.CombatTargetMegaDelayTicks is < 0 or > MaximumNativeMonsterAiTimerTicks
            || initialTarget.CombatTargetSkeletonRangedMovementIdleTicks is < -1 or > MaximumNativeMonsterAiTimerTicks
            || initialTarget.CombatTargetSkeletonRangedMovementMode is < -1 or > MaximumNativeMonsterMovementMode
            || initialTarget.CombatTargetSneakRetreatTicks is < 0 or > MaximumNativeMonsterShortTimerTicks
            || initialTarget.CombatTargetBatRetreatTicks is < 0 or > MaximumNativeMonsterRetreatTicks
            || initialTarget.CombatTargetBatRetreatDirection is < 0 or > MaximumNativeMonsterDirection
            || initialTarget.CombatTargetScavengerHealingTicks is < 0 or > MaximumNativeMonsterShortTimerTicks
            || initialTarget.CombatTargetFatMovementIdleTicks is < -1 or > MaximumNativeMonsterAiTimerTicks
            || initialTarget.CombatTargetFatMovementMode is < 0 or > MaximumNativeMonsterMovementMode
            || (!string.IsNullOrEmpty(initialTarget.CombatTargetNativeAi)
                && !AuthoritativeMonsterTypeCatalog.IsValidNativeAi(
                    initialTarget.CombatTargetNativeAi))
            || initialTarget.CombatTargetFireResistance is < MinimumNativeMonsterResistance or > MaximumNativeMonsterResistance
            || initialTarget.CombatTargetLightningResistance is < MinimumNativeMonsterResistance or > MaximumNativeMonsterResistance
            || initialTarget.CombatTargetMagicResistance is < MinimumNativeMonsterResistance or > MaximumNativeMonsterResistance
            || initialTarget.CombatTargetAcidResistance is < MinimumNativeMonsterResistance or > MaximumNativeMonsterResistance
            || initialTarget.CombatTargetMonsterLevel < 0
            || (initialTarget.CombatTargetMoving
                && Math.Max(
                    Math.Abs(initialTarget.CombatTargetFuturePositionX
                        - initialTarget.CombatTargetPositionX),
                    Math.Abs(initialTarget.CombatTargetFuturePositionY
                        - initialTarget.CombatTargetPositionY)) != 1)
            || (!initialTarget.CombatTargetMoving
                && (initialTarget.CombatTargetFuturePositionX
                        != initialTarget.CombatTargetPositionX
                    || initialTarget.CombatTargetFuturePositionY
                        != initialTarget.CombatTargetPositionY))) {
            throw new InvalidDataException(
                "Replay fixture contains invalid combat-target occupancy state.");
        }
        if (fixture.Commands.Count == 0)
            throw new InvalidDataException("Replay fixture must contain commands.");
        var commandPlayers = fixture.Commands
            .Select(command => string.IsNullOrEmpty(command.Player)
                ? fixture.InitialState.Player
                : command.Player)
            .ToArray();
        if (fixture.Commands
                .Zip(commandPlayers)
                .GroupBy(pair => pair.Second)
                .Any(group => group.Select(pair => pair.First.ClientSequence).Distinct().Count() != group.Count()))
            throw new InvalidDataException("Replay fixture command sequences must be unique per participant.");
        if (fixture.Commands.Any(command => !string.IsNullOrEmpty(command.Player)
                && !string.Equals(command.Player, fixture.InitialState.Player, StringComparison.Ordinal)
                && !string.Equals(command.Player, fixture.InitialState.SecondaryPlayer?.Player, StringComparison.Ordinal)))
            throw new InvalidDataException("Replay fixture command participant must be the primary or secondary player.");
        if (fixture.Commands.Any(command => string.Equals(
                command.Player,
                fixture.InitialState.SecondaryPlayer?.Player,
                StringComparison.Ordinal)
                && command.Kind is not ("Move" or "Attack" or "UsePortal" or "DropItem" or "PickupWorldItem" or "DropHeldItem")))
            throw new InvalidDataException(
                "Secondary replay participant supports Move, Attack, UsePortal, DropItem, PickupWorldItem, and DropHeldItem commands only.");
        if (fixture.Checkpoints.Select(checkpoint => checkpoint.Tick).Distinct().Count() != fixture.Checkpoints.Count)
            throw new InvalidDataException("Replay fixture checkpoint ticks must be unique.");
        if (fixture.ContentManifest is not null) {
            foreach (var commandTick in fixture.Commands.Select(command => command.TargetTick).Distinct()) {
                if (!fixture.Checkpoints.Any(checkpoint => checkpoint.Tick == commandTick))
                    throw new InvalidDataException($"Replay fixture must include a checkpoint at command tick {commandTick}.");
            }
        }

        return fixture;
    }

    private static ReplayPlayerActionTiming? ParsePlayerActionTiming(JsonElement root)
    {
        if (!root.TryGetProperty("player_action_timing", out var value))
            return null;
        if (value.ValueKind != JsonValueKind.Object) {
            throw new InvalidDataException(
                "Replay fixture player_action_timing must be an object.");
        }

        var timing = new ReplayPlayerActionTiming(
            RequiredInt(value, "walking_frames"),
            RequiredInt(value, "attack_frames"),
            RequiredInt(value, "attack_action_frame"),
            RequiredInt(value, "casting_frames"),
            RequiredInt(value, "casting_action_frame")) {
            DeathFrames = GetOptionalInt(value, "death_frames"),
        };
        if (timing.WalkingFrames is <= 0 or > 24
            || timing.AttackFrames is <= 0 or > sbyte.MaxValue
            || timing.AttackActionFrame <= 0
            || timing.AttackActionFrame > timing.AttackFrames
            || timing.CastingFrames is <= 0 or > sbyte.MaxValue
            || timing.CastingActionFrame < 0
            || timing.CastingActionFrame >= timing.CastingFrames
            || timing.DeathFrames is < 0 or > sbyte.MaxValue) {
            throw new InvalidDataException(
                "Replay fixture contains invalid player-action timing.");
        }
        return timing;
    }

    private static ReplayContentManifest? ParseContentManifest(JsonElement root)
    {
        if (!root.TryGetProperty("content_manifest", out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Array)
            return null;
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Replay fixture content_manifest must be an object or legacy array.");

        return new ReplayContentManifest(
            RequiredString(value, "id"),
            RequiredString(value, "version"),
            RequiredString(value, "sha256"));
    }

    private static ReplayQuestSelection? ParseQuestSelection(JsonElement root)
    {
        if (!root.TryGetProperty("quest_selection", out var value))
            return null;
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Replay fixture quest_selection must be an object.");
        var seed = RequiredUInt(value, "seed");
        if (!value.TryGetProperty("pools", out var poolsElement)
            || poolsElement.ValueKind != JsonValueKind.Array
            || poolsElement.GetArrayLength() == 0) {
            throw new InvalidDataException("Replay fixture quest_selection must define pools.");
        }
        var pools = new List<ReplayQuestPool>();
        foreach (var poolElement in poolsElement.EnumerateArray()) {
            if (poolElement.ValueKind != JsonValueKind.Object
                || !poolElement.TryGetProperty("quest_ids", out var questIdsElement)
                || questIdsElement.ValueKind != JsonValueKind.Array
                || questIdsElement.GetArrayLength() < 2) {
                throw new InvalidDataException("Replay fixture quest_selection contains an invalid pool.");
            }
            var poolId = RequiredInt(poolElement, "pool_id");
            var questIds = questIdsElement.EnumerateArray()
                .Select(element => element.GetUInt32())
                .OrderBy(questId => questId)
                .ToArray();
            if (poolId <= 0
                || questIds.Any(questId => questId == 0)
                || questIds.Distinct().Count() != questIds.Length
                || pools.Any(pool => pool.PoolId == poolId)) {
                throw new InvalidDataException("Replay fixture quest_selection contains duplicate or invalid identities.");
            }
            pools.Add(new ReplayQuestPool(poolId, questIds));
        }
        return new ReplayQuestSelection(seed, pools.OrderBy(pool => pool.PoolId).ToArray());
    }

    private static ReplayInitialState ParseInitialState(JsonElement value)
    {
        var combatTargetPositionX = GetOptionalInt(
            value,
            "combat_target_position_x");
        var combatTargetPositionY = GetOptionalInt(
            value,
            "combat_target_position_y");
        var combatTargetHitPoints = GetOptionalInt(
            value,
            "combat_target_hit_points");
        return new ReplayInitialState(
            RequiredString(value, "player"),
            RequiredUInt(value, "gold"),
            RequiredUInt(value, "experience"),
            RequiredInt(value, "life"),
            value.TryGetProperty("mana", out var mana) ? mana.GetInt32() : 0) {
            LifeMaximum = value.TryGetProperty("life_maximum", out var lifeMaximum)
                ? lifeMaximum.GetInt32()
                : RequiredInt(value, "life"),
            CharacterName = OptionalString(value, "character_name") ?? string.Empty,
            ManaShieldActive = GetOptionalBool(value, "mana_shield_active"),
            ReflectCharges = GetOptionalUInt(value, "reflect_charges"),
            ManaMaximum = value.TryGetProperty("mana_maximum", out var manaMaximum) ? manaMaximum.GetInt32() : 0,
            Magic = value.TryGetProperty("magic", out var magic) ? magic.GetInt32() : 0,
            Strength = value.TryGetProperty("strength", out var strength) ? strength.GetInt32() : 0,
            Dexterity = value.TryGetProperty("dexterity", out var dexterity) ? dexterity.GetInt32() : 0,
            Vitality = value.TryGetProperty("vitality", out var vitality) ? vitality.GetInt32() : 0,
            CharacterClass = value.TryGetProperty("character_class", out var characterClass) ? characterClass.GetInt32() : 0,
            CharacterLevel = value.TryGetProperty("character_level", out var characterLevel) ? characterLevel.GetByte() : (byte)1,
            PositionX = value.TryGetProperty("position_x", out var positionX) ? positionX.GetInt32() : 0,
            PositionY = value.TryGetProperty("position_y", out var positionY) ? positionY.GetInt32() : 0,
            LevelId = value.TryGetProperty("level_id", out var levelId) ? levelId.GetUInt32() : 0,
            WarpId = GetOptionalUInt(value, "warp_id"),
            WarpSourceLevelId = GetOptionalUInt(value, "warp_source_level_id"),
            WarpTriggerX = GetOptionalInt(value, "warp_trigger_x"),
            WarpTriggerY = GetOptionalInt(value, "warp_trigger_y"),
            WarpTargetLevelId = GetOptionalUInt(value, "warp_target_level_id"),
            WarpTargetX = GetOptionalInt(value, "warp_target_x"),
            WarpTargetY = GetOptionalInt(value, "warp_target_y"),
            FacingDirection = (DiabloPlayerDirection)GetOptionalUInt(
                value,
                "facing_direction"),
            SuppressMultiplayerDeathDrops = GetOptionalBool(
                value,
                "suppress_multiplayer_death_drops"),
            GoldStackSeed = value.TryGetProperty("gold_stack_seed", out var goldStackSeed)
                ? goldStackSeed.GetUInt32()
                : 0,
            GoldStackStableItemId = GetOptionalUInt(value, "gold_stack_stable_item_id"),
            GoldStackCell = value.TryGetProperty("gold_stack_cell", out var goldStackCell)
                ? goldStackCell.GetUInt32()
                : 0,
            InventoryFillerCount = GetOptionalUInt(value, "inventory_filler_count"),
            WorldItemEntityId = value.TryGetProperty("world_item_entity_id", out var worldItemEntityId) ? worldItemEntityId.GetUInt32() : 0,
            WorldItemSeed = value.TryGetProperty("world_item_seed", out var worldItemSeed) ? worldItemSeed.GetUInt32() : 0,
            WorldItemPrice = value.TryGetProperty("world_item_price", out var worldItemPrice) ? worldItemPrice.GetUInt32() : 0,
            ObjectEntityId = value.TryGetProperty("object_entity_id", out var objectEntityId) ? objectEntityId.GetUInt32() : 0,
            ObjectId = value.TryGetProperty("object_id", out var objectId) ? objectId.GetUInt32() : 0,
            ObjectQuestId = GetOptionalUInt(value, "object_quest_id"),
            ObjectPositionX = value.TryGetProperty("object_position_x", out var objectPositionX) ? objectPositionX.GetInt32() : 0,
            ObjectPositionY = value.TryGetProperty("object_position_y", out var objectPositionY) ? objectPositionY.GetInt32() : 0,
            ObjectEffectKind = GetOptionalInt(value, "object_effect_kind"),
            ObjectEffectAmount = GetOptionalInt(value, "object_effect_amount"),
            ObjectMissilePassable = GetOptionalBool(value, "object_missile_passable"),
            ObjectBreakable = GetOptionalBool(value, "object_breakable"),
            ObjectDestroyed = GetOptionalBool(value, "object_destroyed"),
            CombatTargetEntityId = GetOptionalUInt(value, "combat_target_entity_id"),
            CombatTargetLevelId = GetOptionalUInt(value, "combat_target_level_id"),
            CombatTargetPositionX = combatTargetPositionX,
            CombatTargetPositionY = combatTargetPositionY,
            CombatTargetHitPoints = combatTargetHitPoints,
            CombatTargetMaxHitPoints = value.TryGetProperty(
                "combat_target_max_hit_points",
                out var combatTargetMaxHitPoints)
                ? combatTargetMaxHitPoints.GetInt32()
                : combatTargetHitPoints,
            CombatTargetArmorClass = GetOptionalInt(
                value,
                "combat_target_armor_class"),
            CombatTargetAttackDamage = GetOptionalInt(
                value,
                "combat_target_attack_damage"),
            CombatTargetAggroRange = GetOptionalInt(
                value,
                "combat_target_aggro_range"),
            CombatTargetFireResistance = GetOptionalInt(
                value,
                "combat_target_fire_resistance"),
            CombatTargetLightningResistance = GetOptionalInt(
                value,
                "combat_target_lightning_resistance"),
            CombatTargetMagicResistance = GetOptionalInt(
                value,
                "combat_target_magic_resistance"),
            CombatTargetAcidResistance = GetOptionalInt(
                value,
                "combat_target_acid_resistance"),
            CombatTargetMonsterLevel = GetOptionalInt(
                value,
                "combat_target_monster_level"),
            CombatTargetUndead = GetOptionalBool(
                value,
                "combat_target_undead"),
            CombatTargetAnimal = GetOptionalBool(
                value,
                "combat_target_animal"),
            CombatTargetNativeAi = OptionalString(value, "combat_target_native_ai") ?? string.Empty,
            CombatTargetMonsterItemSeed = GetOptionalUInt(value, "combat_target_monster_item_seed"),
            CombatTargetMonsterAiSeed = GetOptionalUInt(value, "combat_target_monster_ai_seed"),
            CombatTargetMonsterId = GetOptionalUInt(value, "combat_target_monster_id"),
            CombatTargetToHit = GetOptionalInt(value, "combat_target_to_hit"),
            CombatTargetToHitSpecial = GetOptionalInt(value, "combat_target_to_hit_special"),
            CombatTargetMinDamage = GetOptionalInt(value, "combat_target_min_damage"),
            CombatTargetMaxDamage = GetOptionalInt(value, "combat_target_max_damage"),
            CombatTargetMinDamageSpecial = GetOptionalInt(value, "combat_target_min_damage_special"),
            CombatTargetMaxDamageSpecial = GetOptionalInt(value, "combat_target_max_damage_special"),
            CombatTargetIntelligence = GetOptionalInt(value, "combat_target_intelligence"),
            CombatTargetFallenStandAnimationFrame = GetOptionalInt(
                value,
                "combat_target_fallen_stand_animation_frame"),
            CombatTargetFallenStandAnimationTick = GetOptionalInt(
                value,
                "combat_target_fallen_stand_animation_tick"),
            CombatTargetFallenRallyTicks = GetOptionalInt(
                value,
                "combat_target_fallen_rally_ticks"),
            CombatTargetActiveForTicks = GetOptionalInt(
                value,
                "combat_target_active_for_ticks"),
            CombatTargetCompanion = ParseCombatTargetCompanion(value),
            CombatTargetGargoyleSpecialActive = GetOptionalBool(
                value,
                "combat_target_gargoyle_special_active"),
            CombatTargetHorkDemonDelayTicks = GetOptionalInt(
                value,
                "combat_target_hork_demon_delay_ticks"),
            CombatTargetSkeletonKingDelayTicks = GetOptionalInt(
                value,
                "combat_target_skeleton_king_delay_ticks"),
            CombatTargetSkeletonMeleeDelayTicks = GetOptionalInt(
                value,
                "combat_target_skeleton_melee_delay_ticks"),
            CombatTargetSnakeDelayTicks = GetOptionalInt(
                value,
                "combat_target_snake_delay_ticks"),
            CombatTargetCounselorDelayTicks = GetOptionalInt(
                value,
                "combat_target_counselor_delay_ticks"),
            CombatTargetCounselorRetreatTicks = GetOptionalInt(
                value,
                "combat_target_counselor_retreat_ticks"),
            CombatTargetRangedDelayTicks = GetOptionalInt(
                value,
                "combat_target_ranged_delay_ticks"),
            CombatTargetRangedRetreatMinimumDistance = GetOptionalInt(
                value,
                "combat_target_ranged_retreat_minimum_distance"),
            CombatTargetRangedRetreatChanceBase = GetOptionalInt(
                value,
                "combat_target_ranged_retreat_chance_base"),
            CombatTargetRangedRetreatChanceIntelligenceScale = GetOptionalInt(
                value,
                "combat_target_ranged_retreat_chance_intelligence_scale"),
            CombatTargetMegaDelayTicks = GetOptionalInt(
                value,
                "combat_target_mega_delay_ticks"),
            CombatTargetSkeletonRangedMovementIdleTicks = GetOptionalInt(
                value,
                "combat_target_skeleton_ranged_movement_idle_ticks",
                defaultValue: -1),
            CombatTargetSkeletonRangedMovementMode = GetOptionalInt(
                value,
                "combat_target_skeleton_ranged_movement_mode"),
            CombatTargetSneakHidden = GetOptionalBool(
                value,
                "combat_target_sneak_hidden"),
            CombatTargetSneakRetreatTicks = GetOptionalInt(
                value,
                "combat_target_sneak_retreat_ticks"),
            CombatTargetBatRetreatTicks = GetOptionalInt(
                value,
                "combat_target_bat_retreat_ticks"),
            CombatTargetBatRetreatDirection = GetOptionalInt(
                value,
                "combat_target_bat_retreat_direction"),
            CombatTargetScavengerHealingTicks = GetOptionalInt(
                value,
                "combat_target_scavenger_healing_ticks"),
            CombatTargetScavengerCorpseEntityId = GetOptionalUInt(
                value,
                "combat_target_scavenger_corpse_entity_id"),
            CombatTargetFatMovementIdleTicks = value.TryGetProperty(
                "combat_target_fat_movement_idle_ticks",
                out var fatMovementIdleTicks)
                ? fatMovementIdleTicks.GetInt32()
                : -1,
            CombatTargetFatMovementMode = GetOptionalInt(
                value,
                "combat_target_fat_movement_mode"),
            Corpses = ParseCorpses(value),
            CombatTargetMoving = GetOptionalBool(value, "combat_target_moving"),
            CombatTargetFuturePositionX = value.TryGetProperty(
                "combat_target_future_position_x",
                out var combatTargetFuturePositionX)
                ? combatTargetFuturePositionX.GetInt32()
                : combatTargetPositionX,
            CombatTargetFuturePositionY = value.TryGetProperty(
                "combat_target_future_position_y",
                out var combatTargetFuturePositionY)
                ? combatTargetFuturePositionY.GetInt32()
                : combatTargetPositionY,
            CombatTargetPetrified = GetOptionalBool(
                value,
                "combat_target_petrified"),
            CombatTargetStoneCurseImmune = GetOptionalBool(
                value,
                "combat_target_stone_curse_immune"),
            CombatTargetDemon = GetOptionalBool(
                value,
                "combat_target_demon"),
            QuestId = value.TryGetProperty("quest_id", out var questId) ? questId.GetUInt32() : 0,
            QuestRequiredProgress = value.TryGetProperty("quest_required_progress", out var questRequiredProgress) ? questRequiredProgress.GetUInt32() : 0,
            StatusEffectId = value.TryGetProperty("status_effect_id", out var statusEffectId) ? statusEffectId.GetUInt32() : 0,
            StatusDuration = value.TryGetProperty("status_duration", out var statusDuration) ? statusDuration.GetUInt32() : 0,
            StatusMagnitude = value.TryGetProperty("status_magnitude", out var statusMagnitude) ? statusMagnitude.GetInt32() : 0,
            AbilitySpellId = value.TryGetProperty("ability_spell_id", out var abilitySpellId) ? abilitySpellId.GetUInt32() : 0,
            MemorizedSpells = ParseMemorizedSpells(value),
            Equipment = ParseEquipment(value),
            Belt = ParseBelt(value),
            SecondaryPlayer = ParseSecondaryPlayer(value),
        };
    }

    private static IReadOnlyList<ReplayInitialProjectile> ParseInitialProjectiles(JsonElement root)
    {
        if (!root.TryGetProperty("initial_projectiles", out var value))
            return [];
        if (value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Replay fixture initial_projectiles must be an array.");

        var projectiles = value.EnumerateArray()
            .Select(projectile => new ReplayInitialProjectile(
                RequiredUInt(projectile, "entity_id"),
                RequiredUInt(projectile, "source_entity_id"),
                GetOptionalUInt(projectile, "target_entity_id"),
                GetOptionalUInt(projectile, "level_id"),
                RequiredInt(projectile, "position_x"),
                RequiredInt(projectile, "position_y"),
                RequiredInt(projectile, "target_x"),
                RequiredInt(projectile, "target_y"),
                RequiredInt(projectile, "damage"),
                GetOptionalInt(projectile, "damage_type"),
                RequiredUInt(projectile, "remaining_ticks")) {
                SpellId = GetOptionalUInt(projectile, "spell_id"),
                AreaRadius = GetOptionalInt(projectile, "area_radius"),
                Behavior = GetOptionalInt(projectile, "behavior"),
                Phase = GetOptionalInt(projectile, "phase"),
                Speed = GetOptionalInt(projectile, "speed"),
                ImpactLifetime = projectile.TryGetProperty(
                    "impact_lifetime",
                    out var impactLifetime)
                    ? impactLifetime.GetInt32()
                    : 1,
                DamageMinimum = GetOptionalInt(projectile, "damage_minimum"),
                DamageMaximum = GetOptionalInt(projectile, "damage_maximum"),
            })
            .ToArray();
        if (projectiles.Any(projectile =>
                projectile.EntityId == 0
                || projectile.SourceEntityId == 0
                || projectile.Damage < 0
                || projectile.DamageType < 0
                || !Enum.IsDefined((AuthoritativeDamageType)projectile.DamageType)
                || projectile.RemainingTicks == 0
                || projectile.AreaRadius < 0
                || !Enum.IsDefined((AuthoritativeProjectileBehavior)projectile.Behavior)
                || !Enum.IsDefined((AuthoritativeProjectilePhase)projectile.Phase)
                || projectile.Speed < 0
                || projectile.ImpactLifetime < 0
                || projectile.DamageMinimum < 0
                || projectile.DamageMaximum < projectile.DamageMinimum)
            || projectiles.Select(projectile => projectile.EntityId).Distinct().Count()
                != projectiles.Length) {
            throw new InvalidDataException(
                "Replay fixture initial_projectiles contains invalid or duplicate state.");
        }
        return projectiles;
    }

    private static DiabloGameVariant ParseGameVariant(JsonElement root)
    {
        if (!root.TryGetProperty("game_variant", out var value))
            return DiabloGameVariant.Diablo;
        if (value.ValueKind != JsonValueKind.String
            || !Enum.TryParse<DiabloGameVariant>(
                value.GetString(),
                ignoreCase: false,
                out var variant)
            || !Enum.IsDefined(variant)
            || Enum.GetName(variant) != value.GetString()) {
            throw new InvalidDataException(
                "Replay fixture game_variant must be Diablo or Hellfire.");
        }
        return variant;
    }

    private static DiabloGameMode ParseGameMode(JsonElement root)
    {
        if (!root.TryGetProperty("game_mode", out var value))
            return DiabloGameMode.SinglePlayer;
        if (value.ValueKind != JsonValueKind.String
            || !Enum.TryParse<DiabloGameMode>(
                value.GetString(),
                ignoreCase: false,
                out var mode)
            || !Enum.IsDefined(mode)
            || Enum.GetName(mode) != value.GetString()) {
            throw new InvalidDataException(
                "Replay fixture game_mode must be SinglePlayer or Multiplayer.");
        }
        return mode;
    }

    private static DiabloDifficulty ParseDifficulty(JsonElement root)
    {
        if (!root.TryGetProperty("difficulty", out var value))
            return DiabloDifficulty.Normal;
        if (value.ValueKind != JsonValueKind.String
            || !Enum.TryParse<DiabloDifficulty>(
                value.GetString(),
                ignoreCase: false,
                out var difficulty)
            || !Enum.IsDefined(difficulty)
            || Enum.GetName(difficulty) != value.GetString()) {
            throw new InvalidDataException(
                "Replay fixture difficulty must be Normal, Nightmare, or Hell.");
        }
        return difficulty;
    }

    private static ReplaySecondaryPlayer? ParseSecondaryPlayer(JsonElement value)
    {
        if (!value.TryGetProperty("secondary_player", out var player))
            return null;
        if (player.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Replay fixture secondary_player must be an object.");
        var friendlyMode = true;
        if (player.TryGetProperty("friendly_mode", out var friendly)) {
            if (friendly.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) {
                throw new InvalidDataException(
                    "Replay fixture property 'friendly_mode' must be a boolean.");
            }
            friendlyMode = friendly.GetBoolean();
        }
        var result = new ReplaySecondaryPlayer(
            RequiredString(player, "player"),
            RequiredUInt(player, "entity_id"),
            RequiredInt(player, "life"),
            RequiredInt(player, "position_x"),
            RequiredInt(player, "position_y")) {
            LifeMaximum = player.TryGetProperty("life_maximum", out var lifeMaximum)
                ? lifeMaximum.GetInt32()
                : RequiredInt(player, "life"),
            CharacterName = OptionalString(player, "character_name") ?? string.Empty,
            Gold = GetOptionalUInt(player, "gold"),
            LevelId = GetOptionalUInt(player, "level_id"),
            CharacterLevel = player.TryGetProperty("character_level", out var level)
                ? level.GetUInt32()
                : 1,
            CharacterClass = GetOptionalUInt(player, "character_class"),
            Magic = GetOptionalInt(player, "magic"),
            Dexterity = GetOptionalInt(player, "dexterity"),
            FriendlyMode = friendlyMode,
            BlockCapable = GetOptionalBool(player, "block_capable"),
            Equipment = ParseEquipment(player),
            CombatMode = (DiabloPlayerCombatMode)GetOptionalUInt(player, "combat_mode"),
            CombatModeEndTick = player.TryGetProperty(
                "combat_mode_end_tick",
                out var combatModeEndTick)
                ? combatModeEndTick.GetUInt64()
                : 0,
            InventoryFillerCount = GetOptionalUInt(player, "inventory_filler_count"),
            InventoryItem = ParseSecondaryInventoryItem(player),
            HeldItem = ParseSecondaryHeldItem(player),
        };
        if (result.EntityId == 0
            || result.Life < 0
            || result.LifeMaximum <= 0
            || result.Life > result.LifeMaximum
            || result.CharacterLevel is < 1 or > MaximumNativeCharacterLevel
            || (!string.IsNullOrEmpty(result.CharacterName)
                && !DiabloPlayerName.IsValid(result.CharacterName))
            || !Enum.IsDefined(result.CombatMode)
            || (result.Life == 0) != (result.CombatMode == DiabloPlayerCombatMode.Death)
            || (result.CombatMode == DiabloPlayerCombatMode.Stand
                && result.CombatModeEndTick != 0)
            || (result.CombatMode == DiabloPlayerCombatMode.Death
                && result.CombatModeEndTick != 0)
            || ((result.CombatMode is DiabloPlayerCombatMode.Block
                    or DiabloPlayerCombatMode.GotHit)
                && result.CombatModeEndTick == 0)
            || result.InventoryFillerCount > (uint)NativeMaximumInventoryCount
            || (result.InventoryItem is not null
                && result.InventoryFillerCount >= (uint)NativeMaximumInventoryCount)
            || (result.HeldItem is not null && result.HeldItem.Slot != 0)) {
            throw new InvalidDataException(
                "Replay fixture contains invalid secondary-player state.");
        }
        return result;
    }

    private static ReplayCombatCompanion? ParseCombatTargetCompanion(JsonElement value)
    {
        if (!value.TryGetProperty("combat_target_companion_entity_id", out var entityId)
            && !value.EnumerateObject().Any(property =>
                property.Name.StartsWith("combat_target_companion_", StringComparison.Ordinal)))
            return null;

        var companionEntityId = value.TryGetProperty(
            "combat_target_companion_entity_id",
            out var declaredEntityId)
            ? declaredEntityId.GetUInt32()
            : 0U;

        var positionX = GetOptionalInt(value, "combat_target_companion_position_x");
        var positionY = GetOptionalInt(value, "combat_target_companion_position_y");
        var hitPoints = GetOptionalInt(value, "combat_target_companion_hit_points");
        return new ReplayCombatCompanion(
            companionEntityId,
            positionX,
            positionY,
            hitPoints) {
            LevelId = GetOptionalUInt(value, "combat_target_companion_level_id"),
            MaxHitPoints = value.TryGetProperty(
                "combat_target_companion_max_hit_points",
                out var maxHitPoints)
                ? maxHitPoints.GetInt32()
                : hitPoints,
            MonsterId = GetOptionalUInt(value, "combat_target_companion_monster_id"),
            NativeAi = OptionalString(value, "combat_target_companion_native_ai") ?? string.Empty,
            Intelligence = GetOptionalInt(value, "combat_target_companion_intelligence"),
            FallenStandAnimationFrame = GetOptionalInt(
                value,
                "combat_target_companion_fallen_stand_animation_frame"),
            FallenStandAnimationTick = GetOptionalInt(
                value,
                "combat_target_companion_fallen_stand_animation_tick"),
            FallenRallyTicks = GetOptionalInt(
                value,
                "combat_target_companion_fallen_rally_ticks"),
        };
    }

    private static ReplaySecondaryInventoryItem? ParseSecondaryInventoryItem(JsonElement player)
    {
        return ParseSecondaryItem(player, "inventory_item", "secondary inventory_item");
    }

    private static ReplaySecondaryInventoryItem? ParseSecondaryHeldItem(JsonElement player)
    {
        return ParseSecondaryItem(player, "held_item", "secondary held_item");
    }

    private static ReplaySecondaryInventoryItem? ParseSecondaryItem(
        JsonElement player,
        string propertyName,
        string description)
    {
        if (!player.TryGetProperty(propertyName, out var item))
            return null;
        if (item.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"Replay fixture {description} must be an object.");
        var result = new ReplaySecondaryInventoryItem(
            RequiredUInt(item, "slot"),
            RequiredUInt(item, "item_seed"),
            RequiredUInt(item, "stable_item_id"),
            RequiredUInt(item, "price"),
            RequiredInt(item, "item_type"),
            item.TryGetProperty("item_index", out var itemIndex)
                ? itemIndex.GetInt32()
                : -1,
            RequiredBool(item, "stat_flag"));
        if (result.Slot >= (uint)NativeMaximumInventoryCount
            || result.ItemSeed == 0
            || result.StableItemId == 0
            || result.Price > int.MaxValue
            || result.ItemType is < 0 or > byte.MaxValue
            || result.ItemIndex is < -1 or > byte.MaxValue)
            throw new InvalidDataException(
                $"Replay fixture {description} contains invalid item state.");
        return result;
    }

    private static IReadOnlyList<ReplayMemorizedSpell> ParseMemorizedSpells(JsonElement value)
    {
        if (!value.TryGetProperty("memorized_spells", out var spells))
            return [];
        if (spells.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Replay fixture memorized_spells must be an array.");

        var result = spells.EnumerateArray()
            .Select(spell => new ReplayMemorizedSpell(
                RequiredUInt(spell, "spell_id"),
                RequiredUInt(spell, "level")))
            .ToArray();
        if (result.Any(spell => spell.SpellId is 0 or > AuthoritativePlayerLoadoutCatalog.MaximumSpellId
                || spell.Level is 0 or > AuthoritativePlayerLoadoutCatalog.MaximumNativeSpellLevel)
            || result.Select(spell => spell.SpellId).Distinct().Count() != result.Length) {
            throw new InvalidDataException(
                "Replay fixture memorized_spells contains invalid or duplicate spell state.");
        }
        return result;
    }

    private static IReadOnlyList<ReplayEquippedItem> ParseEquipment(JsonElement value)
    {
        if (!value.TryGetProperty("equipment", out var equipment))
            return [];
        if (equipment.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Replay fixture equipment must be an array.");

        var result = equipment.EnumerateArray()
            .Select(item => new ReplayEquippedItem(
                RequiredUInt(item, "slot"),
                RequiredUInt(item, "item_seed"),
                RequiredInt(item, "item_type"),
                RequiredInt(item, "item_class"),
                RequiredUInt(item, "flags"),
                RequiredBool(item, "stat_flag")) {
                ItemIndex = item.TryGetProperty("item_index", out var itemIndex)
                    ? itemIndex.GetInt32()
                    : -1,
                Value = GetOptionalInt(item, "value"),
                IdentifiedValue = GetOptionalInt(item, "identified_value"),
                Identified = GetOptionalBool(item, "identified"),
                Magical = GetOptionalInt(item, "magical"),
                MinDamage = GetOptionalInt(item, "min_damage"),
                MaxDamage = GetOptionalInt(item, "max_damage"),
                PlusDamage = GetOptionalInt(item, "plus_damage"),
                PlusToHit = GetOptionalInt(item, "plus_to_hit"),
                PlusDamageModifier = GetOptionalInt(item, "plus_damage_modifier"),
                PlusEnemyArmorClass = GetOptionalInt(item, "plus_enemy_armor_class"),
                FireMinDamage = GetOptionalInt(item, "fire_min_damage"),
                FireMaxDamage = GetOptionalInt(item, "fire_max_damage"),
                LightningMinDamage = GetOptionalInt(item, "lightning_min_damage"),
                LightningMaxDamage = GetOptionalInt(item, "lightning_max_damage"),
                StableItemId = GetOptionalUInt(item, "stable_item_id"),
            })
            .ToArray();
        if (result.Any(item => item.Slot >= (uint)NativeMaximumEquipmentCount
                || item.ItemSeed == 0
                || item.ItemType is < 0 or > byte.MaxValue
                || item.ItemClass is < 0 or > 3
                || item.MinDamage < 0
                || item.MaxDamage < item.MinDamage
                || item.PlusDamage is < short.MinValue or > short.MaxValue
                || item.PlusToHit is < short.MinValue or > short.MaxValue
                || item.PlusDamageModifier is < short.MinValue or > short.MaxValue
                || item.PlusEnemyArmorClass is < short.MinValue or > short.MaxValue
                || item.FireMinDamage < 0
                || item.FireMaxDamage < item.FireMinDamage
                || item.LightningMinDamage < 0
                || item.LightningMaxDamage < item.LightningMinDamage)
            || result.Select(item => item.Slot).Distinct().Count() != result.Length) {
            throw new InvalidDataException(
                "Replay fixture equipment contains invalid or duplicate item state.");
        }
        return result;
    }

    private static IReadOnlyList<ReplayBeltItem> ParseBelt(JsonElement value)
    {
        if (!value.TryGetProperty("belt", out var belt))
            return [];
        if (belt.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Replay fixture belt must be an array.");

        var result = belt.EnumerateArray()
            .Select(item => new ReplayBeltItem(
                RequiredUInt(item, "slot"),
                RequiredUInt(item, "item_seed"),
                RequiredInt(item, "item_type"),
                RequiredInt(item, "item_index"),
                RequiredInt(item, "misc_id"),
                RequiredInt(item, "spell_id"),
                RequiredBool(item, "stat_flag")) {
                StableItemId = GetOptionalUInt(item, "stable_item_id"),
            })
            .ToArray();
        if (result.Length > NativeMaximumBeltCount
            || result.Any(item => item.Slot >= (uint)NativeMaximumBeltCount
                || item.ItemSeed == 0
                || item.ItemType is < 0 or > byte.MaxValue
                || item.ItemIndex is < -1 or > byte.MaxValue
                || item.MiscId is not (21 or 22)
                || item.SpellId is < 1 or > (int)AuthoritativePlayerLoadoutCatalog.MaximumSpellId)
            || result.Select(item => item.Slot).Distinct().Count() != result.Length) {
            throw new InvalidDataException(
                "Replay fixture belt contains invalid or duplicate consumable state.");
        }
        return result;
    }

    private static ReplayLegacyStoreState ParseLegacyStoreState(JsonElement root)
    {
        if (!root.TryGetProperty("legacy_store_state", out var value))
            return new ReplayLegacyStoreState(1, 0, 3, [42]);
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Replay fixture legacy_store_state must be an object.");

        var seeds = value.TryGetProperty("premium_item_seeds", out var seedValues) && seedValues.ValueKind == JsonValueKind.Array
            ? seedValues.EnumerateArray().Select(seed => seed.GetUInt32()).ToArray()
            : throw new InvalidDataException("Replay fixture premium_item_seeds must be an array.");
        return new ReplayLegacyStoreState(
            value.TryGetProperty("active_store", out var activeStore) ? activeStore.GetInt32() : 1,
            value.TryGetProperty("premium_item_count", out var itemCount) ? itemCount.GetInt32() : 0,
            value.TryGetProperty("premium_item_level", out var itemLevel) ? itemLevel.GetInt32() : 3,
            seeds);
    }

    private static IReadOnlyList<ReplayFixtureCommand> ParseCommands(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Replay fixture commands must be an array.");

        var commands = new List<ReplayFixtureCommand>();
        foreach (var command in value.EnumerateArray()) {
            var kind = RequiredString(command, "kind");
            var payload = command.TryGetProperty("payload", out var payloadValue)
                ? payloadValue
                : default;
            var storeId = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("store_id", out var store)
                ? store.GetUInt32()
                : 1U;
            var storeSlot = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("item_index", out var item)
                ? item.GetUInt32()
                : payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("store_slot", out var slot)
                    ? slot.GetUInt32()
                    : payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("inventory_index", out var inventory)
                        ? inventory.GetUInt32()
                        : 0U;

            if (kind is not ("OpenStore" or "BuyItem" or "SellItem" or "RefillMana" or "Move" or "Attack" or "Cast" or "UsePortal" or "RestartTown" or "PickupWorldItem" or "DropHeldItem" or "DropItem" or "OperateObject" or "AdvanceQuest" or "TalkToQuest"))
                throw new InvalidDataException($"Unsupported replay command kind '{kind}'.");

            commands.Add(new ReplayFixtureCommand(
                RequiredULong(command, "client_sequence"),
                RequiredULong(command, "target_tick"),
                RequiredULong(command, "server_receipt_sequence"),
                kind,
                storeId,
                storeSlot) {
                ExpectedStatus = ParseExpectedStatus(command),
                ExpectedRejectReason = ParseExpectedRejectReason(command),
                Player = OptionalString(command, "player") ?? string.Empty,
                DirectionX = GetOptionalInt(payload, "direction_x"),
                DirectionY = GetOptionalInt(payload, "direction_y"),
                TargetEntityId = GetOptionalUInt(payload, "target_entity_id"),
                SpellId = GetOptionalUInt(payload, "spell_id"),
                TargetX = GetOptionalInt(payload, "target_x"),
                TargetY = GetOptionalInt(payload, "target_y"),
                PortalId = GetOptionalUInt(payload, "portal_id"),
                WorldItemEntityId = GetOptionalUInt(payload, "item_entity_id"),
                ObjectEntityId = GetOptionalUInt(payload, "object_entity_id"),
                QuestId = GetOptionalUInt(payload, "quest_id"),
                StableItemId = GetOptionalUInt(payload, "stable_item_id"),
                ItemLocation = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("location", out var location)
                    ? location.GetString() ?? "Inventory"
                    : "Inventory",
            });
        }

        return commands;
    }

    private static CommandStatus? ParseExpectedStatus(JsonElement command)
    {
        var value = OptionalString(command, "expected_status");
        if (value is null)
            return null;
        return value switch {
            "Accepted" => CommandStatus.Accepted,
            "Rejected" => CommandStatus.Rejected,
            _ => throw new InvalidDataException(
                "Replay fixture command expected_status must be Accepted or Rejected."),
        };
    }

    private static IReadOnlyList<ReplayCorpse> ParseCorpses(JsonElement value)
    {
        if (!value.TryGetProperty("corpses", out var corpses))
            return [];
        if (corpses.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Replay fixture corpses must be an array.");
        var result = corpses.EnumerateArray()
            .Select(corpse => new ReplayCorpse(
                RequiredUInt(corpse, "entity_id"),
                GetOptionalUInt(corpse, "level_id"),
                RequiredInt(corpse, "position_x"),
                RequiredInt(corpse, "position_y")))
            .ToArray();
        if (result.Any(corpse => corpse.EntityId == 0)
            || result.Select(corpse => corpse.EntityId).Distinct().Count() != result.Length)
            throw new InvalidDataException("Replay fixture corpses contain invalid or duplicate state.");
        return result;
    }

    private static CommandRejectReason? ParseExpectedRejectReason(JsonElement command)
    {
        var value = OptionalString(command, "expected_reject_reason");
        if (value is null)
            return null;
        return value switch {
            "Malformed" => CommandRejectReason.Malformed,
            "TooLate" => CommandRejectReason.TooLate,
            "NotAllowed" => CommandRejectReason.NotAllowed,
            "InvalidTarget" => CommandRejectReason.InvalidTarget,
            "InsufficientResources" => CommandRejectReason.InsufficientResources,
            _ => throw new InvalidDataException(
                "Replay fixture command expected_reject_reason is not recognized."),
        };
    }

    private static IReadOnlyList<ReplayCheckpoint> ParseCheckpoints(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Replay fixture checkpoints must be an array.");

        return value.EnumerateArray()
            .Select(checkpoint => new ReplayCheckpoint(
                RequiredULong(checkpoint, "tick"),
                RequiredString(checkpoint, "state_sha256")))
            .ToArray();
    }

    private static string RequiredString(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result) || result.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"Replay fixture property '{property}' must be a string.");
        var text = result.GetString();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException($"Replay fixture property '{property}' must not be empty.");
        return text;
    }

    private static string? OptionalString(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result))
            return null;
        if (result.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"Replay fixture property '{property}' must be a string.");
        var text = result.GetString();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException($"Replay fixture property '{property}' must not be empty.");
        return text;
    }

    private static int RequiredInt(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result) || result.ValueKind != JsonValueKind.Number || !result.TryGetInt32(out var number))
            throw new InvalidDataException($"Replay fixture property '{property}' must be an integer.");
        return number;
    }

    private static uint RequiredUInt(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result) || result.ValueKind != JsonValueKind.Number || !result.TryGetUInt32(out var number))
            throw new InvalidDataException($"Replay fixture property '{property}' must be an unsigned integer.");
        return number;
    }

    private static ulong RequiredULong(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result) || result.ValueKind != JsonValueKind.Number || !result.TryGetUInt64(out var number))
            throw new InvalidDataException($"Replay fixture property '{property}' must be an unsigned integer.");
        return number;
    }

    private static bool RequiredBool(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var result)
            || result.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) {
            throw new InvalidDataException(
                $"Replay fixture property '{property}' must be a boolean.");
        }
        return result.GetBoolean();
    }

    private static int GetOptionalInt(JsonElement value, string property)
    {
        return value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var result) ? result.GetInt32() : 0;
    }

    private static int GetOptionalInt(JsonElement value, string property, int defaultValue)
    {
        return value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var result) ? result.GetInt32() : defaultValue;
    }

    private static uint GetOptionalUInt(JsonElement value, string property)
    {
        return value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var result) ? result.GetUInt32() : 0;
    }

    private static bool GetOptionalBool(JsonElement value, string property)
    {
        if (value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty(property, out var result)) {
            return false;
        }
        if (result.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) {
            throw new InvalidDataException(
                $"Replay fixture property '{property}' must be a boolean.");
        }
        return result.GetBoolean();
    }
}

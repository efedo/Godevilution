using System.Text.Json;
using Devilution.Diablo;

namespace Devilution.Server.Replay;

/** Strict loader for the versioned cross-language replay fixture envelope. */
public static class ReplayFixtureLoader
{
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
                    || fixture.InitialState.GoldStackCell >= 40))) {
            throw new InvalidDataException(
                "Replay fixture contains invalid initial Gold-stack topology.");
        }
        if (fixture.InitialState.InventoryFillerCount > 39
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
                    || fixture.InitialState.ObjectDestroyed))) {
            throw new InvalidDataException(
                "Replay fixture contains invalid object missile-collision state.");
        }
        var initialTarget = fixture.InitialState;
        if ((initialTarget.CombatTargetEntityId == 0
                && (initialTarget.CombatTargetPositionX != 0
                    || initialTarget.CombatTargetPositionY != 0
                    || initialTarget.CombatTargetHitPoints != 0
                    || initialTarget.CombatTargetMaxHitPoints != 0
                    || initialTarget.CombatTargetArmorClass != 0
                    || initialTarget.CombatTargetAttackDamage != 0
                    || initialTarget.CombatTargetAggroRange != 0
                    || !string.IsNullOrEmpty(initialTarget.CombatTargetNativeAi)
                    || initialTarget.CombatTargetMonsterId != 0
                    || initialTarget.CombatTargetToHit != 0
                    || initialTarget.CombatTargetMinDamage != 0
                    || initialTarget.CombatTargetMaxDamage != 0
                    || initialTarget.CombatTargetIntelligence != 0
                    || initialTarget.CombatTargetHorkDemonDelayTicks != 0
                    || initialTarget.CombatTargetFireResistance != 0
                    || initialTarget.CombatTargetLightningResistance != 0
                    || initialTarget.CombatTargetMagicResistance != 0
                    || initialTarget.CombatTargetAcidResistance != 0
                    || initialTarget.CombatTargetMonsterLevel != 0
                    || initialTarget.CombatTargetLevelId != 0
                    || initialTarget.CombatTargetMoving
                    || initialTarget.CombatTargetPetrified))
            || initialTarget.CombatTargetHitPoints < 0
            || initialTarget.CombatTargetMaxHitPoints
                < initialTarget.CombatTargetHitPoints
            || initialTarget.CombatTargetArmorClass < 0
            || initialTarget.CombatTargetAttackDamage < 0
            || initialTarget.CombatTargetAggroRange < 0
            || initialTarget.CombatTargetToHit is < 0 or > 255
            || initialTarget.CombatTargetMinDamage < 0
            || initialTarget.CombatTargetMaxDamage < initialTarget.CombatTargetMinDamage
            || initialTarget.CombatTargetIntelligence is < 0 or > 255
            || initialTarget.CombatTargetHorkDemonDelayTicks is < 0 or > 1024
            || (!string.IsNullOrEmpty(initialTarget.CombatTargetNativeAi)
                && !AuthoritativeMonsterTypeCatalog.IsValidNativeAi(
                    initialTarget.CombatTargetNativeAi))
            || initialTarget.CombatTargetFireResistance is < -100 or > 100
            || initialTarget.CombatTargetLightningResistance is < -100 or > 100
            || initialTarget.CombatTargetMagicResistance is < -100 or > 100
            || initialTarget.CombatTargetAcidResistance is < -100 or > 100
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
                && command.Kind is not ("Move" or "Attack")))
            throw new InvalidDataException(
                "Secondary replay participant currently supports Move and Attack commands only.");
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
            CharacterClass = value.TryGetProperty("character_class", out var characterClass) ? characterClass.GetInt32() : 0,
            CharacterLevel = value.TryGetProperty("character_level", out var characterLevel) ? characterLevel.GetByte() : (byte)1,
            PositionX = value.TryGetProperty("position_x", out var positionX) ? positionX.GetInt32() : 0,
            PositionY = value.TryGetProperty("position_y", out var positionY) ? positionY.GetInt32() : 0,
            LevelId = value.TryGetProperty("level_id", out var levelId) ? levelId.GetUInt32() : 0,
            FacingDirection = (DiabloPlayerDirection)GetOptionalUInt(
                value,
                "facing_direction"),
            SuppressMultiplayerDeathDrops = GetOptionalBool(
                value,
                "suppress_multiplayer_death_drops"),
            GoldStackSeed = value.TryGetProperty("gold_stack_seed", out var goldStackSeed)
                ? goldStackSeed.GetUInt32()
                : 0,
            GoldStackCell = value.TryGetProperty("gold_stack_cell", out var goldStackCell)
                ? goldStackCell.GetUInt32()
                : 0,
            InventoryFillerCount = GetOptionalUInt(value, "inventory_filler_count"),
            WorldItemEntityId = value.TryGetProperty("world_item_entity_id", out var worldItemEntityId) ? worldItemEntityId.GetUInt32() : 0,
            WorldItemSeed = value.TryGetProperty("world_item_seed", out var worldItemSeed) ? worldItemSeed.GetUInt32() : 0,
            WorldItemPrice = value.TryGetProperty("world_item_price", out var worldItemPrice) ? worldItemPrice.GetUInt32() : 0,
            ObjectEntityId = value.TryGetProperty("object_entity_id", out var objectEntityId) ? objectEntityId.GetUInt32() : 0,
            ObjectId = value.TryGetProperty("object_id", out var objectId) ? objectId.GetUInt32() : 0,
            ObjectPositionX = value.TryGetProperty("object_position_x", out var objectPositionX) ? objectPositionX.GetInt32() : 0,
            ObjectPositionY = value.TryGetProperty("object_position_y", out var objectPositionY) ? objectPositionY.GetInt32() : 0,
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
            CombatTargetNativeAi = OptionalString(value, "combat_target_native_ai") ?? string.Empty,
            CombatTargetMonsterId = GetOptionalUInt(value, "combat_target_monster_id"),
            CombatTargetToHit = GetOptionalInt(value, "combat_target_to_hit"),
            CombatTargetMinDamage = GetOptionalInt(value, "combat_target_min_damage"),
            CombatTargetMaxDamage = GetOptionalInt(value, "combat_target_max_damage"),
            CombatTargetIntelligence = GetOptionalInt(value, "combat_target_intelligence"),
            CombatTargetHorkDemonDelayTicks = GetOptionalInt(
                value,
                "combat_target_hork_demon_delay_ticks"),
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
            LevelId = GetOptionalUInt(player, "level_id"),
            CharacterLevel = player.TryGetProperty("character_level", out var level)
                ? level.GetUInt32()
                : 1,
            CharacterClass = GetOptionalUInt(player, "character_class"),
            Magic = GetOptionalInt(player, "magic"),
            Dexterity = GetOptionalInt(player, "dexterity"),
            FriendlyMode = friendlyMode,
            BlockCapable = GetOptionalBool(player, "block_capable"),
            CombatMode = (DiabloPlayerCombatMode)GetOptionalUInt(player, "combat_mode"),
            CombatModeEndTick = player.TryGetProperty(
                "combat_mode_end_tick",
                out var combatModeEndTick)
                ? combatModeEndTick.GetUInt64()
                : 0,
        };
        if (result.EntityId == 0
            || result.Life < 0
            || result.LifeMaximum <= 0
            || result.Life > result.LifeMaximum
            || result.CharacterLevel is < 1 or > 50
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
                && result.CombatModeEndTick == 0)) {
            throw new InvalidDataException(
                "Replay fixture contains invalid secondary-player state.");
        }
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
                FireMinDamage = GetOptionalInt(item, "fire_min_damage"),
                FireMaxDamage = GetOptionalInt(item, "fire_max_damage"),
                LightningMinDamage = GetOptionalInt(item, "lightning_min_damage"),
                LightningMaxDamage = GetOptionalInt(item, "lightning_max_damage"),
            })
            .ToArray();
        if (result.Any(item => item.Slot >= 7
                || item.ItemSeed == 0
                || item.ItemType is < 0 or > byte.MaxValue
                || item.ItemClass is < 0 or > 3
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
                RequiredBool(item, "stat_flag")))
            .ToArray();
        if (result.Length > 8
            || result.Any(item => item.Slot >= 8
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

            if (kind is not ("OpenStore" or "BuyItem" or "SellItem" or "RefillMana" or "Move" or "Attack" or "Cast" or "UsePortal" or "RestartTown" or "PickupWorldItem" or "DropHeldItem" or "OperateObject" or "AdvanceQuest" or "TalkToQuest"))
                throw new InvalidDataException($"Unsupported replay command kind '{kind}'.");

            commands.Add(new ReplayFixtureCommand(
                RequiredULong(command, "client_sequence"),
                RequiredULong(command, "target_tick"),
                RequiredULong(command, "server_receipt_sequence"),
                kind,
                storeId,
                storeSlot) {
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
            });
        }

        return commands;
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

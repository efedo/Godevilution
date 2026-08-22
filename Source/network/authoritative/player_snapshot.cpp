#include "network/authoritative/player_snapshot.hpp"

#include <cstdlib>
#include <limits>
#include <set>
#include <vector>
#include <unordered_set>
#include <utility>

#include <algorithm>

#include "devilution.pb.h"
#include "game/players/players.hpp"
#include "network/authoritative/item_snapshot.hpp"
#include "qol/floatingnumbers.h"
#include "tables/textdat.h"
#include "ui/minitext.h"

namespace devilution::authoritative {
namespace protocol = ::devilution::protocol::v1;
namespace {

constexpr uint32_t StashPageCount = 100;
constexpr uint32_t StashCellCount = 100;
constexpr uint32_t StashGridWidth = 10;
constexpr uint32_t StashGridHeight = 10;

bool HasValidItemIdentity(uint32_t itemSeed, const protocol::ItemStateSnapshot &state)
{
	const bool hasDynamicPresentation = !state.base_name().empty()
	    || !state.identified_name().empty()
	    || state.has_native_cursor_graphic();
	if (!hasDynamicPresentation)
		return itemSeed != 0;
	return !state.base_name().empty()
	    && !state.identified_name().empty()
	    && state.has_native_cursor_graphic();
}

bool IsValidSpellTargetingKind(uint32_t value)
{
	return value <= static_cast<uint32_t>(protocol::SPELL_TARGETING_KIND_WORLD_ENTITY);
}

bool IsValidObjectPresentationKind(protocol::ObjectPresentationKind value)
{
	switch (value) {
	case protocol::OBJECT_PRESENTATION_KIND_UNSPECIFIED:
	case protocol::OBJECT_PRESENTATION_KIND_GENERIC:
	case protocol::OBJECT_PRESENTATION_KIND_SHRINE:
	case protocol::OBJECT_PRESENTATION_KIND_QUEST:
	case protocol::OBJECT_PRESENTATION_KIND_BREAKABLE:
	case protocol::OBJECT_PRESENTATION_KIND_TRAP:
		return true;
	default:
		return false;
	}
}

bool IsValidNativeTileCoordinate(int32_t x, int32_t y)
{
	return x >= 0 && y >= 0
	    && x <= static_cast<int32_t>(std::numeric_limits<uint8_t>::max())
	    && y <= static_cast<int32_t>(std::numeric_limits<uint8_t>::max());
}

} // namespace

tl::expected<ProjectedPlayerSnapshot, std::string> ProjectPlayerSnapshot(const protocol::Snapshot &snapshot, uint32_t entityId)
{
	const protocol::PlayerSnapshot *player = nullptr;
	std::unordered_set<uint32_t> entityIds;
	for (const auto &candidate : snapshot.players()) {
		if (candidate.entity_id() == 0)
			return tl::make_unexpected("Server-backed snapshot contains an invalid player entity ID.");
		if (!entityIds.insert(candidate.entity_id()).second)
			return tl::make_unexpected("Server-backed snapshot contains duplicate player entity IDs.");
		if (candidate.entity_id() == entityId)
			player = &candidate;
	}
	if (player == nullptr)
		return tl::make_unexpected("Server-backed snapshot does not contain the requested player entity.");
	if (player->visited_level_ids_size() > 256) {
		return tl::make_unexpected("Server-backed snapshot contains too many visited levels.");
	}
	std::unordered_set<uint32_t> visitedLevelIds;
	for (const auto levelId : player->visited_level_ids()) {
		if (levelId >= NUMLEVELS)
			return tl::make_unexpected("Server-backed snapshot contains a visited level outside the native range.");
		if (!visitedLevelIds.insert(levelId).second)
			return tl::make_unexpected("Server-backed snapshot contains duplicate visited levels.");
	}
	if (player->visited_level_ids_size() > 0
	    && std::find(player->visited_level_ids().begin(), player->visited_level_ids().end(), player->level_id())
	        == player->visited_level_ids().end()) {
		return tl::make_unexpected("Server-backed snapshot omits the player's current visited level.");
	}
	if (player->reflect_charges() > std::numeric_limits<uint16_t>::max())
		return tl::make_unexpected("Server-backed snapshot contains Reflect charges outside the native range.");
	if (player->stash_size() > StashPageCount * StashCellCount)
		return tl::make_unexpected("Server-backed snapshot contains too many stash items.");
	if (player->stash_gold() > static_cast<uint32_t>(std::numeric_limits<int32_t>::max()))
		return tl::make_unexpected("Server-backed snapshot contains stash gold outside the native range.");
	if (player->stash_open() && player->active_store_id() != 0)
		return tl::make_unexpected("Server-backed snapshot opens both a stash and a vendor.");
	std::set<uint32_t> itemSeeds;
	const auto hasUniqueItemSeed = [&itemSeeds](uint32_t itemSeed) {
		return itemSeed == 0 || itemSeeds.insert(itemSeed).second;
	};
	for (const auto &source : player->inventory()) {
		if (!hasUniqueItemSeed(source.item_seed()))
			return tl::make_unexpected("Server-backed snapshot contains duplicate player item references.");
	}
	for (const auto &source : player->equipment()) {
		if (!hasUniqueItemSeed(source.item_seed()))
			return tl::make_unexpected("Server-backed snapshot contains duplicate player item references.");
	}
	for (const auto &source : player->belt()) {
		if (!hasUniqueItemSeed(source.item_seed()))
			return tl::make_unexpected("Server-backed snapshot contains duplicate player item references.");
	}
	for (const auto &source : player->stash()) {
		if (!hasUniqueItemSeed(source.item().item_seed()))
			return tl::make_unexpected("Server-backed snapshot contains duplicate player item references.");
	}
	if (player->has_held_item() && !hasUniqueItemSeed(player->held_item().item_seed()))
		return tl::make_unexpected("Server-backed snapshot contains duplicate player item references.");
	if (!IsValidSpellTargetingKind(static_cast<uint32_t>(player->ability_targeting_kind())))
		return tl::make_unexpected("Server-backed snapshot contains an invalid ability targeting kind.");

	ProjectedPlayerSnapshot projected {
		.entityId = player->entity_id(),
		.positionX = player->position_x(),
		.positionY = player->position_y(),
		.life = player->life(),
		.lifeMaximum = player->life_maximum(),
		.lifeMaximumBase = player->base_life_maximum() == 0 ? player->life_maximum() : player->base_life_maximum(),
		.mana = player->mana(),
		.manaMaximum = player->mana_maximum(),
		.manaMaximumBase = player->base_mana_maximum() == 0 ? player->mana_maximum() : player->base_mana_maximum(),
		.gold = player->gold(),
		.experience = player->experience(),
		.characterLevel = player->character_level(),
		.diabloKillLevel = player->diablo_kill_level(),
		.reflectCharges = player->reflect_charges(),
		.statPoints = player->stat_points(),
		.characterClassId = player->character_class_id(),
		.characterName = player->character_name(),
		.manaShieldActive = player->mana_shield_active(),
		.abilitySpellId = player->ability_spell_id(),
		.abilitySpellTargetingKind = static_cast<ProjectedSpellTargetingKind>(player->ability_targeting_kind()),
		.levelId = player->level_id(),
		.visitedLevelIds = { player->visited_level_ids().begin(), player->visited_level_ids().end() },
		.friendlyMode = !player->hostile_mode(),
		.facingDirection = static_cast<uint32_t>(player->facing_direction()),
		.combatMode = static_cast<uint32_t>(player->combat_mode()),
		.combatModeEndTick = player->combat_mode_end_tick(),
		.serverTick = snapshot.tick(),
		.strength = { player->attributes().strength().base(), player->attributes().strength().current() },
		.magic = { player->attributes().magic().base(), player->attributes().magic().current() },
		.dexterity = { player->attributes().dexterity().base(), player->attributes().dexterity().current() },
		.vitality = { player->attributes().vitality().base(), player->attributes().vitality().current() },
		.activeStoreId = player->active_store_id() == 0 ? std::nullopt : std::optional<uint32_t>(player->active_store_id()),
	};
	if (player->has_current_action()) {
		const auto &action = player->current_action();
		ProjectedPlayerActionSnapshot projectedAction {
			.kind = static_cast<uint32_t>(action.kind()),
			.startedAtTick = action.started_at_tick(),
			.effectTick = action.effect_tick(),
			.readyTick = action.ready_tick(),
			.endTick = action.end_tick(),
			.effectApplied = action.effect_applied(),
			.directionX = action.direction_x(),
			.directionY = action.direction_y(),
			.targetEntityId = action.target_entity_id(),
			.hasCast = action.has_cast(),
		};
		if (action.has_cast()) {
			projectedAction.spellId = action.cast().spell_id();
			projectedAction.castTargetEntityId = action.cast().target_entity_id();
			projectedAction.targetX = action.cast().target_x();
			projectedAction.targetY = action.cast().target_y();
			projectedAction.castSource = static_cast<uint32_t>(action.cast().source());
			projectedAction.hasCastSourceItem = action.cast().has_source_item();
			if (action.cast().has_source_item()) {
				projectedAction.castSourceItemLocation = static_cast<uint32_t>(action.cast().source_item().location());
				projectedAction.castSourceItemSlot = action.cast().source_item().slot();
				projectedAction.castSourceItemStableId = action.cast().source_item().stable_item_id();
			}
		}
		projected.currentAction = projectedAction;
	}
	projected.memorizedSpells.reserve(player->memorized_spells_size());
	for (const auto &spell : player->memorized_spells()) {
		if (!IsValidSpellTargetingKind(static_cast<uint32_t>(spell.targeting_kind())))
			return tl::make_unexpected("Server-backed snapshot contains an invalid memorized-spell targeting kind.");
		projected.memorizedSpells.push_back({
		    .spellId = spell.spell_id(),
		    .level = spell.level(),
		    .targetingKind = static_cast<ProjectedSpellTargetingKind>(spell.targeting_kind()),
		});
	}
	projected.statusEffects.reserve(player->status_effects_size());
	for (const auto &effect : player->status_effects())
		projected.statusEffects.push_back({ effect.effect_id(), effect.remaining_ticks(), effect.magnitude(), static_cast<uint32_t>(effect.behavior()) });
	projected.inventory.reserve(player->inventory_size());
	for (const auto &source : player->inventory()) {
		if (!HasValidItemIdentity(source.item_seed(), source.state()))
			return tl::make_unexpected("Server-backed snapshot contains an inventory item with an invalid native identity.");
		auto item = ProjectNativeItem(source.state(), source.item_seed(), source.price());
		if (!item.has_value())
			return tl::make_unexpected(item.error());
		projected.inventory.push_back({
		    .storeId = source.store_id(),
		    .storeSlot = source.store_slot(),
		    .itemSeed = source.item_seed(),
		    .stableItemId = source.state().stable_item_id(),
		    .spellId = static_cast<uint32_t>(source.state().spell_id()),
		    .spellTargetingKind = static_cast<ProjectedSpellTargetingKind>(source.state().spell_targeting_kind()),
		    .price = source.price(),
		    .purchasedAtTick = source.purchased_at_tick(),
		    .item = std::move(*item),
		});
	}
	if (player->has_held_item()) {
		const auto &source = player->held_item();
		if (!HasValidItemIdentity(source.item_seed(), source.state()))
			return tl::make_unexpected("Server-backed snapshot contains a held item with an invalid native identity.");
		auto item = ProjectNativeItem(source.state(), source.item_seed(), source.price());
		if (!item.has_value())
			return tl::make_unexpected(item.error());
		projected.heldItem = ProjectedInventoryItem {
			.storeId = source.store_id(),
			.storeSlot = source.store_slot(),
			.itemSeed = source.item_seed(),
			.stableItemId = source.state().stable_item_id(),
			.spellId = static_cast<uint32_t>(source.state().spell_id()),
			.spellTargetingKind = static_cast<ProjectedSpellTargetingKind>(source.state().spell_targeting_kind()),
			.price = source.price(),
			.purchasedAtTick = source.purchased_at_tick(),
			.item = std::move(*item),
		};
	}
	projected.equipment.reserve(player->equipment_size());
	for (const auto &source : player->equipment()) {
		if (!HasValidItemIdentity(source.item_seed(), source.state()))
			return tl::make_unexpected("Server-backed snapshot contains an equipment item with an invalid native identity.");
		auto item = ProjectNativeItem(source.state(), source.item_seed(), 0);
		if (!item.has_value())
			return tl::make_unexpected(item.error());
		projected.equipment.push_back({
		    .slot = source.slot(),
		    .itemSeed = source.item_seed(),
		    .stableItemId = source.state().stable_item_id(),
		    .spellId = static_cast<uint32_t>(source.state().spell_id()),
		    .spellTargetingKind = static_cast<ProjectedSpellTargetingKind>(source.state().spell_targeting_kind()),
		    .item = std::move(*item),
		});
	}
	projected.belt.reserve(player->belt_size());
	for (const auto &source : player->belt()) {
		if (!HasValidItemIdentity(source.item_seed(), source.state()))
			return tl::make_unexpected("Server-backed snapshot contains a belt item with an invalid native identity.");
		auto item = ProjectNativeItem(source.state(), source.item_seed(), 0);
		if (!item.has_value())
			return tl::make_unexpected(item.error());
		projected.belt.push_back({
		    .slot = source.slot(),
		    .itemSeed = source.item_seed(),
		    .stableItemId = source.state().stable_item_id(),
		    .spellId = static_cast<uint32_t>(source.state().spell_id()),
		    .spellTargetingKind = static_cast<ProjectedSpellTargetingKind>(source.state().spell_targeting_kind()),
		    .item = std::move(*item),
		});
	}
	projected.stash.reserve(player->stash_size());
	std::vector<bool> occupied(StashPageCount * StashCellCount, false);
	for (const auto &source : player->stash()) {
		if (source.page() >= StashPageCount || source.cell() >= StashCellCount)
			return tl::make_unexpected("Server-backed snapshot contains a stash item outside the native page grid.");
		if (!HasValidItemIdentity(source.item().item_seed(), source.item().state()))
			return tl::make_unexpected("Server-backed snapshot contains a stash item with an invalid native identity.");
		const uint32_t width = std::max(1U, source.item().state().inventory_width());
		const uint32_t height = std::max(1U, source.item().state().inventory_height());
		const uint32_t column = source.cell() % StashGridWidth;
		const uint32_t row = source.cell() / StashGridWidth;
		if (width > StashGridWidth || height > StashGridHeight
		    || column + width > StashGridWidth || row + height > StashGridHeight)
			return tl::make_unexpected("Server-backed snapshot contains a stash item with an invalid footprint.");
		for (uint32_t y = 0; y < height; ++y) {
			for (uint32_t x = 0; x < width; ++x) {
				const std::size_t index = static_cast<std::size_t>(source.page()) * StashCellCount
				    + static_cast<std::size_t>(row + y) * StashGridWidth + column + x;
				if (occupied[index])
					return tl::make_unexpected("Server-backed snapshot contains overlapping stash items.");
				occupied[index] = true;
			}
		}
		auto item = ProjectNativeItem(source.item().state(), source.item().item_seed(), source.item().price());
		if (!item.has_value())
			return tl::make_unexpected(item.error());
		projected.stash.push_back({
		    .page = source.page(),
		    .cell = source.cell(),
		    .itemSeed = source.item().item_seed(),
		    .stableItemId = source.item().state().stable_item_id(),
		    .spellId = static_cast<uint32_t>(source.item().state().spell_id()),
		    .spellTargetingKind = static_cast<ProjectedSpellTargetingKind>(source.item().state().spell_targeting_kind()),
		    .price = source.item().price(),
		    .purchasedAtTick = source.item().purchased_at_tick(),
		    .width = width,
		    .height = height,
		    .item = std::move(*item),
		});
	}
	projected.stashGold = player->stash_gold();
	projected.stashOpen = player->stash_open();
	projected.inventoryGrid.assign(player->inventory_grid().begin(), player->inventory_grid().end());
	return projected;
}

std::optional<ProjectedSpellTargetingKind> ProjectedSpellTargetingKindForSpell(
	const ProjectedPlayerSnapshot &snapshot,
	uint32_t spellId) noexcept
{
	if (spellId == 0)
		return std::nullopt;
	if (snapshot.abilitySpellId == spellId)
		return snapshot.abilitySpellTargetingKind;
	for (const auto &spell : snapshot.memorizedSpells) {
		if (spell.spellId == spellId)
			return spell.targetingKind;
	}
	const auto itemTargetingKind = [spellId](const auto &item) -> std::optional<ProjectedSpellTargetingKind> {
		if (item.spellId == spellId)
			return item.spellTargetingKind;
		return std::nullopt;
	};
	for (const auto &item : snapshot.inventory) {
		if (const auto targetingKind = itemTargetingKind(item); targetingKind.has_value())
			return targetingKind;
	}
	if (snapshot.heldItem.has_value()) {
		if (const auto targetingKind = itemTargetingKind(*snapshot.heldItem); targetingKind.has_value())
			return targetingKind;
	}
	for (const auto &item : snapshot.equipment) {
		if (const auto targetingKind = itemTargetingKind(item); targetingKind.has_value())
			return targetingKind;
	}
	for (const auto &item : snapshot.belt) {
		if (const auto targetingKind = itemTargetingKind(item); targetingKind.has_value())
			return targetingKind;
	}
	for (const auto &item : snapshot.stash) {
		if (item.spellId == spellId)
			return item.spellTargetingKind;
	}
	return std::nullopt;
}

tl::expected<std::vector<ProjectedPlayerSnapshot>, std::string> ProjectPlayerSnapshots(const protocol::Snapshot &snapshot)
{
	if (snapshot.players_size() == 0)
		return tl::make_unexpected("Server-backed snapshot contains no player entity.");
	std::vector<ProjectedPlayerSnapshot> projected;
	projected.reserve(snapshot.players_size());
	for (const auto &source : snapshot.players()) {
		auto player = ProjectPlayerSnapshot(snapshot, source.entity_id());
		if (!player.has_value())
			return tl::make_unexpected(player.error());
		projected.push_back(std::move(*player));
	}
	std::sort(projected.begin(), projected.end(), [](const auto &left, const auto &right) {
		return left.entityId < right.entityId;
	});
	return projected;
}

tl::expected<std::vector<ProjectedMonsterSnapshot>, std::string> ProjectMonsterSnapshots(const protocol::Snapshot &snapshot)
{
	std::vector<ProjectedMonsterSnapshot> projected;
	projected.reserve(snapshot.monsters_size());
	for (const auto &source : snapshot.monsters()) {
		if (source.entity_id() == 0)
			return tl::make_unexpected("Server-backed snapshot contains a monster with an invalid entity ID.");
		if (std::any_of(projected.begin(), projected.end(), [&](const auto &candidate) { return candidate.entityId == source.entity_id(); }))
			return tl::make_unexpected("Server-backed snapshot contains duplicate monster entity IDs.");
		if (source.unique_identity().size() > 128 || (!source.is_unique() && !source.unique_identity().empty()))
			return tl::make_unexpected("Server-backed snapshot contains an invalid unique monster identity.");
		const int64_t hitPointsQ6 = source.hit_points_q6() == 0 && source.hit_points() != 0
		    ? static_cast<int64_t>(source.hit_points()) * 64
		    : source.hit_points_q6();
		const int64_t maxHitPointsQ6 = source.max_hit_points_q6() == 0 && source.max_hit_points() != 0
		    ? static_cast<int64_t>(source.max_hit_points()) * 64
		    : source.max_hit_points_q6();
		if (hitPointsQ6 < 0 || hitPointsQ6 > std::numeric_limits<int32_t>::max()
		    || maxHitPointsQ6 < hitPointsQ6 || maxHitPointsQ6 > std::numeric_limits<int32_t>::max()) {
			return tl::make_unexpected("Server-backed snapshot contains invalid fixed-point monster hit points.");
		}
		const int32_t futurePositionX = source.moving() ? source.future_position_x() : source.position_x();
		const int32_t futurePositionY = source.moving() ? source.future_position_y() : source.position_y();
		if (source.moving()
		    && std::max(
		           std::abs(futurePositionX - source.position_x()),
		           std::abs(futurePositionY - source.position_y()))
		        != 1) {
			return tl::make_unexpected("Server-backed snapshot contains an invalid moving monster destination.");
		}
		if (!source.moving()
		    && (source.future_position_x() != 0 || source.future_position_y() != 0)
		    && (source.future_position_x() != source.position_x()
		        || source.future_position_y() != source.position_y())) {
			return tl::make_unexpected("Server-backed snapshot contains inconsistent stationary monster occupancy.");
		}
		projected.push_back({
		    .entityId = source.entity_id(),
		    .monsterId = source.monster_id(),
		    .isUnique = source.is_unique(),
		    .uniqueIdentity = source.unique_identity(),
		    .levelId = source.level_id(),
		    .positionX = source.position_x(),
		    .positionY = source.position_y(),
		    .hitPoints = source.hit_points(),
		    .maxHitPoints = source.max_hit_points(),
		    .hitPointsQ6 = static_cast<int32_t>(hitPointsQ6),
		    .maxHitPointsQ6 = static_cast<int32_t>(maxHitPointsQ6),
		    .armorClass = source.armor_class(),
		    .alive = source.alive(),
		    .attackDamage = source.attack_damage(),
		    .aggroRange = source.aggro_range(),
		    .fireResistance = source.fire_resistance(),
		    .lightningResistance = source.lightning_resistance(),
		    .magicResistance = source.magic_resistance(),
		    .moving = source.moving(),
		    .futurePositionX = futurePositionX,
		    .futurePositionY = futurePositionY,
		    .petrified = source.petrified(),
		    .stoneCurseImmune = source.stone_curse_immune(),
		});
	}
	std::sort(projected.begin(), projected.end(), [](const auto &left, const auto &right) { return left.entityId < right.entityId; });
	return projected;
}

tl::expected<std::vector<ProjectedWorldItemSnapshot>, std::string> ProjectWorldItemSnapshots(const protocol::Snapshot &snapshot)
{
	std::vector<ProjectedWorldItemSnapshot> projected;
	projected.reserve(snapshot.world_items_size());
	for (const auto &source : snapshot.world_items()) {
		if (source.entity_id() == 0)
			return tl::make_unexpected("Server-backed snapshot contains a world item with an invalid entity ID.");
		if (std::any_of(projected.begin(), projected.end(), [&](const auto &candidate) { return candidate.entityId == source.entity_id(); }))
			return tl::make_unexpected("Server-backed snapshot contains duplicate world item entity IDs.");
		if (!HasValidItemIdentity(source.item_seed(), source.state()))
			return tl::make_unexpected("Server-backed snapshot contains a world item with an invalid native identity.");
		auto item = ProjectNativeItem(source.state(), source.item_seed(), source.price());
		if (!item.has_value())
			return tl::make_unexpected(item.error());
		projected.push_back({
		    .entityId = source.entity_id(),
		    .levelId = source.level_id(),
		    .positionX = source.position_x(),
		    .positionY = source.position_y(),
		    .itemSeed = source.item_seed(),
		    .price = source.price(),
		    .item = std::move(*item),
		});
	}
	std::sort(projected.begin(), projected.end(), [](const auto &left, const auto &right) { return left.entityId < right.entityId; });
	return projected;
}

tl::expected<std::vector<ProjectedObjectSnapshot>, std::string> ProjectObjectSnapshots(const protocol::Snapshot &snapshot)
{
	std::vector<ProjectedObjectSnapshot> projected;
	projected.reserve(snapshot.objects_size());
	std::unordered_set<uint32_t> questIds;
	questIds.reserve(snapshot.quests_size());
	for (const auto &quest : snapshot.quests())
		questIds.insert(quest.quest_id());
	for (const auto &source : snapshot.objects()) {
		if (source.entity_id() == 0 || source.object_id() == 0)
			return tl::make_unexpected("Server-backed snapshot contains an object with an invalid entity or object ID.");
		const bool transitionSetsVariable = source.quest_transition_kind() >= 3 && source.quest_transition_kind() <= 8;
		const bool transitionValid = source.quest_transition_kind() == 0
		    || (source.quest_id() != 0
		        && questIds.find(source.quest_id()) != questIds.end()
		        && (transitionSetsVariable ? source.quest_transition_amount() > 0 : source.quest_transition_amount() == 0));
		const bool rewardValid = source.reward_kind() == 0
		    ? source.reward_id() == 0 && source.reward_item_level() == 0 && source.reward_offset_x() == 0 && source.reward_offset_y() == 0
		    : source.reward_id() != 0;
		const bool producesProjectile = source.effect_kind() == static_cast<int32_t>(ProjectedObjectEffectKind::SpawnSpellProjectile)
		    || source.effect_kind() == static_cast<int32_t>(ProjectedObjectEffectKind::GrantSpellLevelAndSpawnProjectile)
		    || source.quest_sequence_follow_up_kind() == 2;
		const bool followUpValid = source.quest_sequence_follow_up_kind() == 0
		    ? source.quest_sequence_follow_up_amount() == 0
		        && source.quest_sequence_follow_up_offset_x() == 0
		        && source.quest_sequence_follow_up_offset_y() == 0
		    : source.quest_sequence_follow_up_kind() == 1
		        ? source.effect_kind() == static_cast<int32_t>(ProjectedObjectEffectKind::QuestSequence)
		            && source.quest_sequence_follow_up_amount() != 0
		            && source.quest_sequence_follow_up_offset_x() == 0
		            && source.quest_sequence_follow_up_offset_y() == 0
		        : source.quest_sequence_follow_up_kind() == 2
		            ? (source.effect_kind() == static_cast<int32_t>(ProjectedObjectEffectKind::QuestSequence)
		                || source.effect_kind() == static_cast<int32_t>(ProjectedObjectEffectKind::WorldMutation))
		                && source.quest_sequence_follow_up_amount() != 0
		            : source.quest_sequence_follow_up_kind() == 3
		                && source.effect_kind() == static_cast<int32_t>(ProjectedObjectEffectKind::QuestSequence)
		                && source.quest_sequence_follow_up_amount() != 0;
		if ((source.breakable() && source.missile_passable()) || (source.destroyed() && !source.breakable()))
			return tl::make_unexpected("Server-backed snapshot contains an object with invalid missile-collision state.");
		if (source.effect_amount() < 0
		    || source.effect_kind() < static_cast<int32_t>(ProjectedObjectEffectKind::None)
		    || source.effect_kind() > static_cast<int32_t>(ProjectedObjectEffectKind::GrantSpellLevelAndSpawnProjectile)
		    || !IsValidObjectPresentationKind(source.presentation_kind())
		    || (source.quest_id() != 0 && questIds.find(source.quest_id()) == questIds.end())
		    || source.quest_transition_kind() < 0 || source.quest_transition_kind() > 8
		    || source.quest_transition_amount() < 0 || source.quest_transition_amount() > 255
		    || source.quest_transition_required_variable1() < -1 || source.quest_transition_required_variable1() > 255
		    || source.quest_transition_required_variable2() < -1 || source.quest_transition_required_variable2() > 255
		    || !transitionValid
		    || source.reward_kind() < 0 || source.reward_kind() > 2
		    || source.reward_item_level() > 255
		    || source.reward_offset_x() < -16 || source.reward_offset_x() > 16
		    || source.reward_offset_y() < -16 || source.reward_offset_y() > 16
		    || !rewardValid
		    || source.projectile_target_offset_x() < -32 || source.projectile_target_offset_x() > 32
		    || source.projectile_target_offset_y() < -32 || source.projectile_target_offset_y() > 32
		    || (!producesProjectile && (source.projectile_target_offset_x() != 0 || source.projectile_target_offset_y() != 0))
		    || source.quest_sequence_follow_up_kind() < 0 || source.quest_sequence_follow_up_kind() > 3
		    || source.quest_sequence_follow_up_offset_x() < -32 || source.quest_sequence_follow_up_offset_x() > 32
		    || source.quest_sequence_follow_up_offset_y() < -32 || source.quest_sequence_follow_up_offset_y() > 32
		    || !followUpValid
		    || source.projectile_source_kind() < 0 || source.projectile_source_kind() > 1
		    || (!producesProjectile && source.projectile_source_kind() != 0))
			return tl::make_unexpected("Server-backed snapshot contains invalid object effect metadata.");
		if (std::any_of(projected.begin(), projected.end(), [&](const auto &candidate) { return candidate.entityId == source.entity_id(); }))
			return tl::make_unexpected("Server-backed snapshot contains duplicate object entity IDs.");
		projected.push_back({
		    .entityId = source.entity_id(),
		    .objectId = source.object_id(),
		    .levelId = source.level_id(),
		    .positionX = source.position_x(),
		    .positionY = source.position_y(),
		    .activated = source.activated(),
		    .questId = source.quest_id(),
		    .effectKind = source.effect_kind(),
		    .effectAmount = source.effect_amount(),
		    .missilePassable = source.missile_passable(),
		    .breakable = source.breakable(),
		    .destroyed = source.destroyed(),
		    .questTransitionKind = source.quest_transition_kind(),
		    .rewardKind = source.reward_kind(),
		    .questSequenceFollowUpKind = source.quest_sequence_follow_up_kind(),
		});
	}
	std::sort(projected.begin(), projected.end(), [](const auto &left, const auto &right) { return left.entityId < right.entityId; });
	return projected;
}

tl::expected<std::vector<ProjectedProjectileSnapshot>, std::string> ProjectProjectileSnapshots(const protocol::Snapshot &snapshot)
{
	std::vector<ProjectedProjectileSnapshot> projected;
	projected.reserve(snapshot.projectiles_size());
	for (const auto &source : snapshot.projectiles()) {
		if (source.entity_id() == 0 || source.source_entity_id() == 0 || source.spell_id() == 0 || source.remaining_ticks() == 0)
			return tl::make_unexpected("Server-backed snapshot contains a projectile with an invalid identity or lifetime.");
		if (source.damage() < 0 || source.area_radius() < 0)
			return tl::make_unexpected("Server-backed snapshot contains a projectile with an invalid effect.");
		if (std::any_of(projected.begin(), projected.end(), [&](const auto &candidate) { return candidate.entityId == source.entity_id(); }))
			return tl::make_unexpected("Server-backed snapshot contains duplicate projectile entity IDs.");
		projected.push_back({
		    .entityId = source.entity_id(),
		    .sourceEntityId = source.source_entity_id(),
		    .targetEntityId = source.target_entity_id(),
		    .spellId = source.spell_id(),
		    .levelId = source.level_id(),
		    .positionX = source.position_x(),
		    .positionY = source.position_y(),
		    .targetX = source.target_x(),
		    .targetY = source.target_y(),
		    .damage = source.damage(),
		    .damageType = source.damage_type(),
		    .areaRadius = source.area_radius(),
		    .remainingTicks = source.remaining_ticks(),
		    .behavior = source.behavior(),
		    .nativeVisual = source.native_visual(),
		    .phase = source.phase(),
		    .startX = source.start_x(),
		    .startY = source.start_y(),
		    .traveledX = source.traveled_x(),
		    .traveledY = source.traveled_y(),
		    .velocityX = source.velocity_x(),
		    .velocityY = source.velocity_y(),
		    .originalDirection = source.original_direction(),
		    .pathIndex = source.path_index(),
		    .pathTicks = source.path_ticks(),
		    .speed = source.speed(),
		    .impactLifetime = source.impact_lifetime(),
		    .resistantDamageDivisor = source.resistant_damage_divisor(),
		    .lastCollisionTargetEntityId = source.last_collision_target_entity_id(),
		    .effectiveSpellLevel = source.effective_spell_level(),
		});
	}
	std::sort(projected.begin(), projected.end(), [](const auto &left, const auto &right) { return left.entityId < right.entityId; });
	return projected;
}

tl::expected<std::vector<ProjectedPortalSnapshot>, std::string> ProjectPortalSnapshots(const protocol::Snapshot &snapshot)
{
	std::vector<ProjectedPortalSnapshot> projected;
	projected.reserve(snapshot.portals_size());
	for (const auto &source : snapshot.portals()) {
		if (source.portal_id() == 0)
			return tl::make_unexpected("Server-backed snapshot contains a portal with an invalid portal ID.");
		if (!IsValidNativeTileCoordinate(source.source_position_x(), source.source_position_y())
		    || !IsValidNativeTileCoordinate(source.destination_position_x(), source.destination_position_y())
		    || (source.bidirectional() && (source.owner_entity_id() == 0 || source.remaining_ticks() == 0))
		    || (!source.bidirectional() && (source.owner_entity_id() != 0 || source.remaining_ticks() != 0)))
			return tl::make_unexpected("Server-backed snapshot contains an invalid portal route state.");
		if (std::any_of(projected.begin(), projected.end(), [&](const auto &candidate) { return candidate.portalId == source.portal_id(); }))
			return tl::make_unexpected("Server-backed snapshot contains duplicate portal IDs.");
		projected.push_back({
		    .portalId = source.portal_id(),
		    .sourceLevelId = source.source_level_id(),
		    .sourcePositionX = source.source_position_x(),
		    .sourcePositionY = source.source_position_y(),
		    .destinationLevelId = source.destination_level_id(),
		    .destinationPositionX = source.destination_position_x(),
		    .destinationPositionY = source.destination_position_y(),
		    .remainingTicks = source.remaining_ticks(),
		    .ownerEntityId = source.owner_entity_id(),
		    .bidirectional = source.bidirectional(),
		});
	}
	std::sort(projected.begin(), projected.end(), [](const auto &left, const auto &right) { return left.portalId < right.portalId; });
	return projected;
}

tl::expected<void, std::string> ValidateQuestSnapshots(const protocol::Snapshot &snapshot)
{
	if (snapshot.quests_size() > 4096)
		return tl::make_unexpected("Server-backed snapshot contains too many quests.");
	std::unordered_set<uint32_t> questIds;
	questIds.reserve(snapshot.quests_size());
	for (const auto &quest : snapshot.quests()) {
		if (quest.quest_id() == 0 || !questIds.insert(quest.quest_id()).second
		    || quest.required_progress() == 0 || quest.progress() > quest.required_progress()
		    || (quest.unavailable() && (quest.completed() || quest.progress() != 0))
		    || quest.variable_1() < 0 || quest.variable_1() > 255
		    || quest.variable_2() < 0 || quest.variable_2() > 255
		    || quest.message_id() < 0 || quest.message_id() > std::numeric_limits<int16_t>::max()
		    || quest.turn_in_towner_id() < -1 || quest.turn_in_towner_id() > 255
		    || quest.turn_in_alternate_message_id() < 0 || quest.turn_in_alternate_message_id() > std::numeric_limits<int16_t>::max()
		    || quest.turn_in_completion_variable2() < -1 || quest.turn_in_completion_variable2() > 255
		    || quest.turn_in_completion_required_variable1() < -1 || quest.turn_in_completion_required_variable1() > 255
		    || quest.turn_in_completion_variable1() < -1 || quest.turn_in_completion_variable1() > 255
		    || quest.turn_in_required_variable1() < -1 || quest.turn_in_required_variable1() > 255
		    || quest.turn_in_variable1() < -1 || quest.turn_in_variable1() > 255
		    || quest.completion_unique_identity().size() > 128)
			return tl::make_unexpected("Server-backed snapshot contains invalid quest state.");
	}
	return {};
}

void ApplyServerBackedEventBatch(Player &player, const protocol::EventBatch &eventBatch, uint32_t entityId)
{
	for (const auto &event : eventBatch.events()) {
		if (event.has_damage() && event.damage().target_entity_id() == entityId) {
			const int32_t damageQ6 = event.damage().amount_q6() == 0 && event.damage().amount() != 0
			    ? event.damage().amount() * 64
			    : event.damage().amount_q6();
			player.life.current = std::max(0, player.life.current - std::max(0, damageQ6));
			player.UpdateHitPointPercentage();
			AddFloatingDamageNumber(player.position.tile, damageQ6, 0, true);
		}
		if (event.has_healing() && event.healing().target_entity_id() == entityId) {
			const int32_t healingQ6 = event.healing().amount_q6() == 0
			    ? event.healing().amount()
			    : event.healing().amount_q6();
			const int32_t lifeBefore = player.life.current;
			player.life.current = std::min(player.life.maximum, player.life.current + std::max(0, healingQ6));
			player.UpdateHitPointPercentage();
			AddFloatingHealingNumber(player.position.tile, player.life.current - lifeBefore);
		}
		if (event.has_experience() && event.experience().player_entity_id() == entityId) {
			player._pExperience += event.experience().amount();
			AddFloatingExperienceNumber(player.position.tile, event.experience().amount());
		}
		if (event.has_dialogue()
		    && event.dialogue().source_entity_id() == entityId
		    && event.dialogue().message_id() >= 0
		    && static_cast<std::size_t>(event.dialogue().message_id()) < Speeches.size()) {
			InitQTextMsg(static_cast<_speech_id>(event.dialogue().message_id()));
		}
	}
}

bool ServerBackedPlayerState::ApplySnapshot(ProjectedPlayerSnapshot snapshot)
{
	if (snapshot.entityId == 0)
		return false;
	if (snapshot_
	    && snapshot.serverTick < snapshot_->serverTick)
		return false;
	snapshot_ = std::move(snapshot);
	return true;
}

void ServerBackedPlayerState::Clear() noexcept
{
	snapshot_.reset();
}

const ProjectedInventoryItem *ServerBackedPlayerState::FindInventoryItem(std::size_t index) const noexcept
{
	if (!snapshot_ || index >= snapshot_->inventory.size())
		return nullptr;
	return &snapshot_->inventory[index];
}

} // namespace devilution::authoritative

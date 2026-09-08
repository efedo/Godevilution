#include "network/authoritative/player_snapshot.hpp"

#include "network/authoritative/protocol_limits.hpp"

#include <cstdlib>
#include <limits>
#include <set>
#include <vector>
#include <unordered_map>
#include <unordered_set>
#include <utility>

#include <algorithm>

#include "devilution.pb.h"
#include "game/levels/dungeon_common_defs.hpp"
#include "game/levels/triggers.h"
#include "game/items/items.hpp"
#include "game/monsters/monsters.hpp"
#include "game/players/players.hpp"
#include "game/objects/objects.hpp"
#include "game/portals/portal.hpp"
#include "game/quests/quests.hpp"
#include "game/quests/quest_stars.hpp"
#include "network/authoritative/authoritative_level_id.hpp"
#include "network/authoritative/item_snapshot.hpp"
#include "qol/floatingnumbers.h"
#include "tables/objdat.h"
#include "tables/textdat.h"
#include "ui/minitext.h"

namespace devilution::authoritative {
namespace protocol = ::devilution::protocol::v1;
namespace {

constexpr uint32_t StashPageCount = NativeStashPageCount;
constexpr uint32_t StashCellCount = NativeStashCellCount;
constexpr uint32_t StashGridWidth = NativeStashGridWidth;
constexpr uint32_t StashGridHeight = NativeStashGridHeight;
constexpr int MaxWorldMutationCount = static_cast<int>(MaximumWorldMutationCount);

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
	return static_cast<int32_t>(value) >= 0
	    && static_cast<int32_t>(value) <= MaximumObjectPresentationKind;
}

bool IsValidNativeTileCoordinate(int32_t x, int32_t y)
{
	return x >= 0 && y >= 0
	    && x <= static_cast<int32_t>(std::numeric_limits<uint8_t>::max())
	    && y <= static_cast<int32_t>(std::numeric_limits<uint8_t>::max());
}

bool IsValidSnapshotHash(std::string_view value)
{
	if (value.empty())
		return true;
	if (value.size() != 64)
		return false;
	return std::all_of(value.begin(), value.end(), [](char character) {
		return (character >= '0' && character <= '9')
		    || (character >= 'a' && character <= 'f')
		    || (character >= 'A' && character <= 'F');
	});
}

bool IsValidCharacterName(std::string_view value)
{
	if (value.size() > 31)
		return false;
	return std::all_of(value.begin(), value.end(), [](char character) {
		const auto value = static_cast<unsigned char>(character);
		return value >= 0x21 && value <= 0x7E;
	});
}

} // namespace

bool IsValidNativeMissileIdentity(const std::string &value)
{
	if (value.empty())
		return true;
	if (value.size() > MaximumIdentityBytes)
		return false;
	const auto isLetter = [](char character) {
		return character >= 'A' && character <= 'Z'
		    || character >= 'a' && character <= 'z';
	};
	const auto isPart = [&](char character) {
		return isLetter(character)
		    || character >= '0' && character <= '9'
		    || character == '_' || character == '-';
	};
	return (isLetter(value.front()) || value.front() == '_')
	    && std::all_of(value.begin() + 1, value.end(), isPart);
}

tl::expected<ProjectedPlayerSnapshot, std::string> ProjectPlayerSnapshot(const protocol::Snapshot &snapshot, uint32_t entityId)
{
	if (!IsValidSnapshotHash(snapshot.state_sha256()))
		return tl::make_unexpected("Server-backed snapshot contains an invalid state hash.");
	if (snapshot.players_size() > NativeMaximumPlayerCount)
		return tl::make_unexpected("Server-backed snapshot contains too many players for the native player pool.");
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
	if (!IsAuthoritativeSupportedLevel(player->level_id()))
		return tl::make_unexpected("Server-backed snapshot contains an unsupported player level identity.");
	if (!IsValidCharacterName(player->character_name()))
		return tl::make_unexpected("Server-backed snapshot contains an invalid character name.");
	if (player->visited_level_ids_size() > MaximumNativeVisitedLevelCount) {
		return tl::make_unexpected("Server-backed snapshot contains too many visited levels.");
	}
	std::unordered_set<uint32_t> visitedLevelIds;
	for (const auto levelId : player->visited_level_ids()) {
		if (!IsAuthoritativeRegularLevel(levelId))
			return tl::make_unexpected("Server-backed snapshot contains a visited level outside the regular native range.");
		if (!visitedLevelIds.insert(levelId).second)
			return tl::make_unexpected("Server-backed snapshot contains duplicate visited levels.");
	}
	if (IsAuthoritativeRegularLevel(player->level_id()) && player->visited_level_ids_size() > 0
	    && std::find(player->visited_level_ids().begin(), player->visited_level_ids().end(), player->level_id())
	        == player->visited_level_ids().end()) {
		return tl::make_unexpected("Server-backed snapshot omits the player's current visited level.");
	}
	if (player->reflect_charges() > MaximumNativeReflectCharges)
		return tl::make_unexpected("Server-backed snapshot contains Reflect charges outside the native range.");
	if (player->light_radius() != 0
	    && (player->light_radius() < MinimumNativeLightRadius || player->light_radius() > MaximumNativeLightRadius))
		return tl::make_unexpected("Server-backed snapshot contains a light radius outside the native range.");
	if (player->status_effects_size() > MaximumStatusEffectCount)
		return tl::make_unexpected("Server-backed snapshot contains too many status effects.");
	std::unordered_set<uint32_t> statusEffectIds;
	for (const auto &effect : player->status_effects()) {
		const auto behavior = static_cast<int>(effect.behavior());
		if (effect.effect_id() == 0
		    || effect.remaining_ticks() == 0
		    || behavior < 0
		    || behavior > MaximumStatusEffectBehavior
		    || (behavior == static_cast<int>(protocol::STATUS_EFFECT_BEHAVIOR_INFRAVISION)
		        || behavior == static_cast<int>(protocol::STATUS_EFFECT_BEHAVIOR_SEARCH)
		        || behavior == static_cast<int>(protocol::STATUS_EFFECT_BEHAVIOR_ETHEREALIZE))
		        && effect.magnitude() != 0
		    || !statusEffectIds.insert(effect.effect_id()).second)
			return tl::make_unexpected("Server-backed snapshot contains invalid status-effect state.");
	}
	if (player->stash_size() > StashPageCount * StashCellCount)
		return tl::make_unexpected("Server-backed snapshot contains too many stash items.");
	if (player->stash_gold() > static_cast<uint32_t>(std::numeric_limits<int32_t>::max()))
		return tl::make_unexpected("Server-backed snapshot contains stash gold outside the native range.");
	if (player->stash_open() && player->active_store_id() != 0)
		return tl::make_unexpected("Server-backed snapshot opens both a stash and a vendor.");
	if (player->inventory_size() > NativeMaximumInventoryCount
	    || player->equipment_size() > NativeMaximumEquipmentCount
	    || player->belt_size() > NativeMaximumBeltCount)
		return tl::make_unexpected("Server-backed snapshot contains player item collections outside the native topology.");
	if (player->inventory_grid_size() != 0 && player->inventory_grid_size() != NativeMaximumInventoryGridCount)
		return tl::make_unexpected("Server-backed snapshot contains an invalid inventory grid size.");
	for (const auto cell : player->inventory_grid()) {
		if (cell < -1 || cell >= player->inventory_size())
			return tl::make_unexpected("Server-backed snapshot contains an invalid inventory grid reference.");
	}
	if (player->inventory_grid_size() > 0) {
		std::vector<int32_t> expectedInventoryGrid(player->inventory_grid_size(), -1);
		for (int itemIndex = 0; itemIndex < player->inventory_size(); ++itemIndex) {
			const auto &state = player->inventory(itemIndex).state();
			const uint32_t width = std::max(1U, state.inventory_width());
			const uint32_t height = std::max(1U, state.inventory_height());
			const auto anchor = std::find(player->inventory_grid().begin(), player->inventory_grid().end(), itemIndex);
			if (width > NativeInventoryWidth || height > NativeInventoryHeight || anchor == player->inventory_grid().end())
				return tl::make_unexpected("Server-backed snapshot contains an invalid inventory footprint topology.");
			const auto anchorIndex = static_cast<uint32_t>(std::distance(player->inventory_grid().begin(), anchor));
			const uint32_t column = anchorIndex % NativeInventoryWidth;
			const uint32_t row = anchorIndex / NativeInventoryWidth;
			if (column + width > NativeInventoryWidth || row + height > NativeInventoryHeight)
				return tl::make_unexpected("Server-backed snapshot contains an invalid inventory footprint topology.");
			for (uint32_t y = 0; y < height; ++y) {
				for (uint32_t x = 0; x < width; ++x) {
					const auto cellIndex = static_cast<std::size_t>((row + y) * NativeInventoryWidth + column + x);
					if (expectedInventoryGrid[cellIndex] != -1)
						return tl::make_unexpected("Server-backed snapshot contains overlapping inventory footprints.");
					expectedInventoryGrid[cellIndex] = itemIndex;
				}
			}
		}
		if (!std::equal(expectedInventoryGrid.begin(), expectedInventoryGrid.end(), player->inventory_grid().begin()))
			return tl::make_unexpected("Server-backed snapshot inventory grid does not match item footprints.");
	}
	std::set<uint32_t> equipmentSlots;
	for (const auto &item : player->equipment()) {
		if (item.slot() >= NativeMaximumEquipmentCount || !equipmentSlots.insert(item.slot()).second)
			return tl::make_unexpected("Server-backed snapshot contains invalid or duplicate equipment slots.");
	}
	std::set<uint32_t> beltSlots;
	for (const auto &item : player->belt()) {
		if (item.slot() >= NativeMaximumBeltCount || !beltSlots.insert(item.slot()).second)
			return tl::make_unexpected("Server-backed snapshot contains invalid or duplicate belt slots.");
	}
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
		.lightRadius = player->light_radius(),
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
	if (snapshot.players_size() > NativeMaximumPlayerCount)
		return tl::make_unexpected("Server-backed snapshot contains too many players for the native player pool.");
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
	std::unordered_map<uint32_t, size_t> monsterCountsByLevel;
	for (const auto &source : snapshot.monsters()) {
		if (++monsterCountsByLevel[source.level_id()] > NativeMaximumMonsterCount)
			return tl::make_unexpected("Server-backed snapshot contains too many monsters on one level.");
	}
	std::vector<ProjectedMonsterSnapshot> projected;
	projected.reserve(snapshot.monsters_size());
	for (const auto &source : snapshot.monsters()) {
		if (source.entity_id() == 0)
			return tl::make_unexpected("Server-backed snapshot contains a monster with an invalid entity ID.");
		if (std::any_of(projected.begin(), projected.end(), [&](const auto &candidate) { return candidate.entityId == source.entity_id(); }))
			return tl::make_unexpected("Server-backed snapshot contains duplicate monster entity IDs.");
		if (source.unique_identity().size() > MaximumIdentityBytes || (!source.is_unique() && !source.unique_identity().empty()))
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
		if ((!source.moving()
		     && (source.movement_started_tick() != 0 || source.movement_effect_tick() != 0))
		    || (source.movement_started_tick() != 0 || source.movement_effect_tick() != 0)
		        && source.movement_effect_tick() <= source.movement_started_tick()) {
			return tl::make_unexpected("Server-backed snapshot contains invalid monster movement timing.");
		}
		if (source.leader_relation() < 0 || source.leader_relation() > MaximumMonsterLeaderRelation
		    || (source.leader_relation() == 0
		        ? source.leader_entity_id() != 0
		        : source.leader_entity_id() == 0)
		    || source.leader_relation() != 0 && source.is_unique()
		    || source.pack_size() < 0 || source.pack_size() > MaximumNativeMonsterPackSize
		    || source.active_for_ticks() < 0 || source.active_for_ticks() > MaximumNativeMonsterActiveTicks) {
			return tl::make_unexpected("Server-backed snapshot contains invalid monster group-unity state.");
		}
		projected.push_back({
		    .entityId = source.entity_id(),
		    .monsterId = source.monster_id(),
		    .isUnique = source.is_unique(),
		    .uniqueIdentity = source.unique_identity(),
		    .dialogueMessageId = source.dialogue_message_id(),
		    .dialogueState = source.dialogue_state(),
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
		    .direction = source.direction(),
		    .moving = source.moving(),
		    .futurePositionX = futurePositionX,
		    .futurePositionY = futurePositionY,
		    .movementStartedTick = source.movement_started_tick(),
		    .movementEffectTick = source.movement_effect_tick(),
		    .petrified = source.petrified(),
		    .sneakHidden = source.sneak_hidden(),
		    .stoneCurseImmune = source.stone_curse_immune(),
		    .leaderEntityId = source.leader_entity_id(),
		    .leaderRelation = source.leader_relation(),
		    .packSize = source.pack_size(),
		    .activeForTicks = source.active_for_ticks(),
		});
	}
	for (const auto &monster : projected) {
		if (monster.leaderRelation == 0)
			continue;
		const auto leader = std::find_if(projected.begin(), projected.end(), [&](const auto &candidate) {
			return candidate.entityId == monster.leaderEntityId;
		});
		if (leader == projected.end()
		    || leader->entityId == monster.entityId
		    || !leader->alive
		    || leader->levelId != monster.levelId) {
			return tl::make_unexpected("Server-backed snapshot contains an invalid monster pack leader.");
		}
	}
	std::sort(projected.begin(), projected.end(), [](const auto &left, const auto &right) { return left.entityId < right.entityId; });
	return projected;
}

tl::expected<std::vector<ProjectedWorldItemSnapshot>, std::string> ProjectWorldItemSnapshots(const protocol::Snapshot &snapshot)
{
	std::vector<ProjectedWorldItemSnapshot> projected;
	projected.reserve(snapshot.world_items_size());
	std::unordered_map<uint32_t, int> itemCountsByLevel;
	itemCountsByLevel.reserve(snapshot.world_items_size());
	for (const auto &source : snapshot.world_items()) {
		if (source.entity_id() == 0)
			return tl::make_unexpected("Server-backed snapshot contains a world item with an invalid entity ID.");
		if (++itemCountsByLevel[source.level_id()] > NativeMaximumItemCount)
			return tl::make_unexpected("Server-backed snapshot exceeds the native world-item capacity on one level.");
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
	std::unordered_map<uint32_t, int> objectCountsByLevel;
	objectCountsByLevel.reserve(snapshot.objects_size());
	for (const auto &quest : snapshot.quests())
		questIds.insert(quest.quest_id());
	for (const auto &source : snapshot.objects()) {
		if (source.entity_id() == 0 || source.object_id() == 0)
			return tl::make_unexpected("Server-backed snapshot contains an object with an invalid entity or object ID.");
		if (++objectCountsByLevel[source.level_id()] > NativeMaximumObjectCount)
			return tl::make_unexpected("Server-backed snapshot exceeds the native object capacity on one level.");
		if (source.native_object_id() > static_cast<uint32_t>(OBJ_LAST))
			return tl::make_unexpected("Server-backed snapshot contains an invalid native object presentation ID.");
		if (source.animation_frame() > std::numeric_limits<uint8_t>::max()
		    || source.activated_animation_frame() > std::numeric_limits<uint8_t>::max())
			return tl::make_unexpected("Server-backed snapshot contains an invalid object animation frame.");
		if (source.dialogue_message_id() < 0
		    || source.dialogue_message_id() > std::numeric_limits<int16_t>::max())
			return tl::make_unexpected("Server-backed snapshot contains an invalid object dialogue identity.");
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
		    || source.quest_transition_kind() < 0 || source.quest_transition_kind() > MaximumObjectQuestTransitionKind
		    || source.quest_transition_amount() < 0 || source.quest_transition_amount() > MaximumNativeQuestByte
		    || source.quest_transition_required_variable1() < MinimumNativeOptionalQuestByte || source.quest_transition_required_variable1() > MaximumNativeQuestByte
		    || source.quest_transition_required_variable2() < MinimumNativeOptionalQuestByte || source.quest_transition_required_variable2() > MaximumNativeQuestByte
		    || !transitionValid
		    || source.reward_kind() < 0 || source.reward_kind() > MaximumObjectRewardKind
		    || source.reward_item_level() > MaximumNativeQuestByte
		    || source.reward_offset_x() < MinimumNativeRewardOffset || source.reward_offset_x() > MaximumNativeRewardOffset
		    || source.reward_offset_y() < MinimumNativeRewardOffset || source.reward_offset_y() > MaximumNativeRewardOffset
		    || !rewardValid
		    || source.projectile_target_offset_x() < -32 || source.projectile_target_offset_x() > 32
		    || source.projectile_target_offset_y() < -32 || source.projectile_target_offset_y() > 32
		    || (!producesProjectile && (source.projectile_target_offset_x() != 0 || source.projectile_target_offset_y() != 0))
		    || source.quest_sequence_follow_up_kind() < 0 || source.quest_sequence_follow_up_kind() > MaximumObjectQuestSequenceFollowUpKind
		    || source.quest_sequence_follow_up_offset_x() < -32 || source.quest_sequence_follow_up_offset_x() > 32
		    || source.quest_sequence_follow_up_offset_y() < -32 || source.quest_sequence_follow_up_offset_y() > 32
		    || !followUpValid
		    || source.projectile_source_kind() < 0 || source.projectile_source_kind() > MaximumObjectProjectileSourceKind
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
		    .trapArmed = source.trap_armed(),
		    .nativeObjectId = source.native_object_id(),
		    .keepVisibleAfterActivation = source.keep_visible_after_activation(),
		    .animationFrame = source.animation_frame(),
		    .activatedAnimationFrame = source.activated_animation_frame(),
		    .questTransitionKind = source.quest_transition_kind(),
		    .rewardKind = source.reward_kind(),
		    .questSequenceFollowUpKind = source.quest_sequence_follow_up_kind(),
		    .dialogueMessageId = source.dialogue_message_id(),
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
		if (source.entity_id() == 0 || source.source_entity_id() == 0
		    || (source.spell_id() == 0
		        && source.behavior() != static_cast<int>(ProjectileBehavior::WeaponArrow)
		        && source.behavior() != static_cast<int>(ProjectileBehavior::MonsterArrow))
		    || source.remaining_ticks() == 0)
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
		    .nativeMissileId = source.native_missile_id(),
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
	if (snapshot.portals_size() > NativeMaximumPortalCount)
		return tl::make_unexpected("Server-backed snapshot exceeds the native portal capacity.");
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

tl::expected<std::vector<ProjectedCorpseSnapshot>, std::string> ProjectCorpseSnapshots(const protocol::Snapshot &snapshot)
{
	std::vector<ProjectedCorpseSnapshot> projected;
	projected.reserve(snapshot.corpses_size());
	for (const auto &source : snapshot.corpses()) {
		if (source.entity_id() == 0 || !IsValidNativeTileCoordinate(source.position_x(), source.position_y()))
			return tl::make_unexpected("Server-backed snapshot contains a corpse with an invalid identity or position.");
		if (std::any_of(projected.begin(), projected.end(), [&](const auto &candidate) { return candidate.entityId == source.entity_id(); }))
			return tl::make_unexpected("Server-backed snapshot contains duplicate corpse entity IDs.");
		projected.push_back({
		    .entityId = source.entity_id(),
		    .levelId = source.level_id(),
		    .positionX = source.position_x(),
		    .positionY = source.position_y(),
		});
	}
	std::sort(projected.begin(), projected.end(), [](const auto &left, const auto &right) { return left.entityId < right.entityId; });
	return projected;
}

tl::expected<ProjectedSessionMetadata, std::string> ProjectSessionMetadata(const protocol::Snapshot &snapshot)
{
	ProjectedGameDifficulty difficulty = ProjectedGameDifficulty::Normal;
	switch (snapshot.difficulty()) {
	case protocol::GAME_DIFFICULTY_UNSPECIFIED:
	case protocol::GAME_DIFFICULTY_NORMAL:
		difficulty = ProjectedGameDifficulty::Normal;
		break;
	case protocol::GAME_DIFFICULTY_NIGHTMARE:
		difficulty = ProjectedGameDifficulty::Nightmare;
		break;
	case protocol::GAME_DIFFICULTY_HELL:
		difficulty = ProjectedGameDifficulty::Hell;
		break;
	default:
		return tl::make_unexpected("Server-backed snapshot contains an invalid game difficulty.");
	}

	ProjectedSessionMetadata projected {
		.randomState = snapshot.random_state(),
		.victoryTriggered = snapshot.victory_triggered(),
		.difficulty = difficulty,
	};
	if (snapshot.active_world_mutation_ids_size() > MaxWorldMutationCount)
		return tl::make_unexpected("Server-backed snapshot contains too many active world mutations.");
	projected.activeWorldMutationIds.reserve(snapshot.active_world_mutation_ids_size());
	for (const uint32_t mutationId : snapshot.active_world_mutation_ids()) {
		if (mutationId == 0)
			return tl::make_unexpected("Server-backed snapshot contains an invalid world-mutation identity.");
		if (std::find(projected.activeWorldMutationIds.begin(), projected.activeWorldMutationIds.end(), mutationId)
		    != projected.activeWorldMutationIds.end())
			return tl::make_unexpected("Server-backed snapshot contains duplicate world-mutation identities.");
		projected.activeWorldMutationIds.push_back(mutationId);
	}
	std::sort(projected.activeWorldMutationIds.begin(), projected.activeWorldMutationIds.end());
	return projected;
}

tl::expected<std::vector<ProjectedQuestSnapshot>, std::string> ProjectQuestSnapshots(const protocol::Snapshot &snapshot)
{
	if (auto validation = ValidateQuestSnapshots(snapshot); !validation.has_value())
		return tl::make_unexpected(validation.error());

	std::vector<ProjectedQuestSnapshot> projected;
	projected.reserve(snapshot.quests_size());
	for (const auto &source : snapshot.quests()) {
		if (!source.has_native_quest_id())
			continue;
		projected.push_back({
		    .nativeQuestId = source.native_quest_id(),
		    .hasNativeQuestId = true,
		    .completed = source.completed(),
		    .unavailable = source.unavailable(),
		    .initial = source.initial(),
		    .logged = source.logged(),
		    .variable1 = static_cast<uint8_t>(source.variable_1()),
		    .variable2 = static_cast<uint8_t>(source.variable_2()),
		    .messageId = static_cast<int16_t>(source.message_id()),
		});
	}
	std::sort(projected.begin(), projected.end(), [](const auto &left, const auto &right) {
		return left.nativeQuestId < right.nativeQuestId;
	});
	return projected;
}

void ApplyServerBackedQuestSnapshots(const std::vector<ProjectedQuestSnapshot> &quests) noexcept
{
	for (const auto &snapshot : quests) {
		if (!snapshot.hasNativeQuestId || snapshot.nativeQuestId >= MAXQUESTS)
			continue;
		Quest &quest = Quests[snapshot.nativeQuestId];
		quest._qactive = snapshot.unavailable
		    ? QUEST_NOTAVAIL
		    : snapshot.completed
		        ? QUEST_DONE
		        : snapshot.initial ? QUEST_INIT : QUEST_ACTIVE;
		quest._qlog = snapshot.logged;
		quest._qvar1 = snapshot.variable1;
		quest._qvar2 = snapshot.variable2;
		quest._qmsg = static_cast<_speech_id>(snapshot.messageId);
	}
}

tl::expected<std::vector<ProjectedLevelGeometrySnapshot>, std::string> ProjectLevelGeometrySnapshots(const protocol::Snapshot &snapshot)
{
	if (auto validation = ValidateLevelGeometrySnapshots(snapshot); !validation.has_value())
		return tl::make_unexpected(validation.error());

	std::vector<ProjectedLevelGeometrySnapshot> projected;
	projected.reserve(snapshot.level_geometry_size());
	for (const auto &source : snapshot.level_geometry()) {
		ProjectedLevelGeometrySnapshot level {
			.levelId = source.level_id(),
			.width = source.width(),
			.height = source.height(),
		};
		level.blockedCells.assign(source.blocked_cells().begin(), source.blocked_cells().end());
		level.missileBlockedCells.assign(source.missile_blocked_cells().begin(), source.missile_blocked_cells().end());
		level.nativeTransparency.reserve(source.native_transparency_size());
		for (const uint32_t value : source.native_transparency())
			level.nativeTransparency.push_back(static_cast<uint16_t>(value));
		level.nativeObjectLayer.reserve(source.native_object_layer_size());
		for (const uint32_t value : source.native_object_layer())
			level.nativeObjectLayer.push_back(static_cast<uint16_t>(value));
		level.nativeMegaTileLayer.reserve(source.native_mega_tile_layer_size());
		for (const uint32_t value : source.native_mega_tile_layer())
			level.nativeMegaTileLayer.push_back(static_cast<uint16_t>(value));
		level.nativePieceLayer.reserve(source.native_piece_layer_size());
		for (const uint32_t value : source.native_piece_layer())
			level.nativePieceLayer.push_back(static_cast<uint16_t>(value));
		level.warpTriggers.reserve(source.warp_triggers_size());
		for (const auto &warp : source.warp_triggers()) {
			level.warpTriggers.push_back({
				.warpId = warp.warp_id(),
				.triggerX = warp.trigger_x(),
				.triggerY = warp.trigger_y(),
				.targetLevelId = warp.target_level_id(),
				.targetX = warp.target_x(),
				.targetY = warp.target_y(),
				.requiredQuestId = warp.required_quest_id(),
				.requiredQuestCompleted = warp.required_quest_completed(),
				.stableSymbol = warp.stable_symbol(),
				.available = warp.available(),
			});
		}
		std::sort(level.warpTriggers.begin(), level.warpTriggers.end(), [](const auto &left, const auto &right) {
			return left.warpId < right.warpId;
		});
		projected.push_back(std::move(level));
	}
	std::sort(projected.begin(), projected.end(), [](const auto &left, const auto &right) {
		return left.levelId < right.levelId;
	});
	return projected;
}

tl::expected<void, std::string> ValidateQuestSnapshots(const protocol::Snapshot &snapshot)
{
	if (snapshot.quests_size() > MaximumSnapshotQuestCount)
		return tl::make_unexpected("Server-backed snapshot contains too many quests.");
	std::unordered_set<uint32_t> questIds;
	questIds.reserve(snapshot.quests_size());
	std::unordered_set<uint32_t> nativeQuestIds;
	nativeQuestIds.reserve(snapshot.quests_size());
	for (const auto &quest : snapshot.quests()) {
		if (quest.quest_id() == 0 || !questIds.insert(quest.quest_id()).second
		    || (quest.has_native_quest_id()
		        && (quest.native_quest_id() >= MAXQUESTS || !nativeQuestIds.insert(quest.native_quest_id()).second))
		    || quest.required_progress() == 0 || quest.progress() > quest.required_progress()
		    || (quest.unavailable() && (quest.completed() || quest.progress() != 0))
		    || quest.variable_1() < 0 || quest.variable_1() > MaximumNativeQuestByte
		    || quest.variable_2() < 0 || quest.variable_2() > MaximumNativeQuestByte
		    || quest.message_id() < 0 || quest.message_id() > std::numeric_limits<int16_t>::max()
		    || quest.turn_in_towner_id() < MinimumNativeOptionalQuestByte || quest.turn_in_towner_id() > MaximumNativeQuestByte
		    || quest.turn_in_alternate_message_id() < 0 || quest.turn_in_alternate_message_id() > std::numeric_limits<int16_t>::max()
		    || quest.turn_in_completion_variable2() < MinimumNativeOptionalQuestByte || quest.turn_in_completion_variable2() > MaximumNativeQuestByte
		    || quest.turn_in_completion_required_variable1() < MinimumNativeOptionalQuestByte || quest.turn_in_completion_required_variable1() > MaximumNativeQuestByte
		    || quest.turn_in_completion_variable1() < MinimumNativeOptionalQuestByte || quest.turn_in_completion_variable1() > MaximumNativeQuestByte
		    || quest.turn_in_required_variable1() < MinimumNativeOptionalQuestByte || quest.turn_in_required_variable1() > MaximumNativeQuestByte
		    || quest.turn_in_variable1() < MinimumNativeOptionalQuestByte || quest.turn_in_variable1() > MaximumNativeQuestByte
		    || quest.completion_unique_identity().size() > MaximumIdentityBytes)
			return tl::make_unexpected("Server-backed snapshot contains invalid quest state.");
	}
	return {};
}

tl::expected<void, std::string> ValidateQuestTownerInteractions(const protocol::Snapshot &snapshot)
{
	if (snapshot.quest_towner_interactions_size() > MaximumQuestTownerInteractionCount)
		return tl::make_unexpected("Server-backed snapshot contains too many quest/towner interactions.");
	std::unordered_set<uint32_t> questIds;
	questIds.reserve(snapshot.quests_size());
	for (const auto &quest : snapshot.quests())
		questIds.insert(quest.quest_id());
	std::unordered_set<uint32_t> interactionIds;
	interactionIds.reserve(snapshot.quest_towner_interactions_size());
	const bool hasLevelGeometry = snapshot.level_geometry_size() > 0;
	for (const auto &interaction : snapshot.quest_towner_interactions()) {
		const auto level = std::find_if(snapshot.level_geometry().begin(), snapshot.level_geometry().end(), [&](const auto &candidate) {
			return candidate.level_id() == interaction.level_id();
		});
		if (interaction.interaction_id() == 0
		    || !interactionIds.insert(interaction.interaction_id()).second
		    || interaction.quest_id() == 0
		    || questIds.find(interaction.quest_id()) == questIds.end()
		    || (hasLevelGeometry
		        && (level == snapshot.level_geometry().end()
	            || (level->width() > 0 && interaction.position_x() >= level->width())
	            || (level->height() > 0 && interaction.position_y() >= level->height())))
		    || interaction.towner_id() < 0 || interaction.towner_id() > MaximumNativeQuestByte
		    || interaction.level_id() > std::numeric_limits<uint16_t>::max()
		    || interaction.required_visited_level1() > std::numeric_limits<uint16_t>::max()
		    || interaction.required_visited_level2() > std::numeric_limits<uint16_t>::max()
		    || interaction.required_item_id() > std::numeric_limits<uint16_t>::max()
		    || interaction.position_x() < 0 || interaction.position_y() < 0
		    || interaction.kind() < 0 || interaction.kind() > MaximumQuestTownerInteractionKind
		    || !interaction.has_message_id()
		    || interaction.message_id() <= 0 || interaction.message_id() > std::numeric_limits<int16_t>::max()
		    || (interaction.has_required_variable1() && (interaction.required_variable1() < 0 || interaction.required_variable1() > MaximumNativeQuestByte))
		    || (interaction.has_required_variable2() && (interaction.required_variable2() < 0 || interaction.required_variable2() > MaximumNativeQuestByte))
		    || (interaction.has_variable1() && (interaction.variable1() < 0 || interaction.variable1() > MaximumNativeQuestByte))
		    || (interaction.has_variable2() && (interaction.variable2() < 0 || interaction.variable2() > MaximumNativeQuestByte)))
			return tl::make_unexpected("Server-backed snapshot contains an invalid quest/towner interaction.");
	}
	return {};
}

tl::expected<void, std::string> ValidateTownerGreetingSnapshots(const protocol::Snapshot &snapshot)
{
	if (snapshot.towner_greetings_size() > MaximumTownerGreetingCount)
		return tl::make_unexpected("Server-backed snapshot contains too many towner greetings.");
	std::unordered_map<uint32_t, const protocol::LevelGeometrySnapshot *> levels;
	levels.reserve(snapshot.level_geometry_size());
	for (const auto &level : snapshot.level_geometry()) {
		if (!levels.emplace(level.level_id(), &level).second)
			return tl::make_unexpected("Server-backed snapshot contains duplicate level geometry.");
	}
	std::unordered_set<int32_t> townerIds;
	for (const auto &greeting : snapshot.towner_greetings()) {
		const auto level = levels.find(greeting.level_id());
		if (greeting.towner_id() < 0
		    || greeting.towner_id() > MaximumNativeQuestByte
		    || !townerIds.insert(greeting.towner_id()).second
		    || greeting.level_id() > std::numeric_limits<uint16_t>::max()
		    || greeting.position_x() < 0
		    || greeting.position_x() > MaximumNativeQuestByte
		    || greeting.position_y() < 0
		    || greeting.position_y() > MaximumNativeQuestByte
		    || !levels.empty()
		        && (level == levels.end()
		            || level->second->width() > 0 && greeting.position_x() >= level->second->width()
		            || level->second->height() > 0 && greeting.position_y() >= level->second->height())
		    || greeting.message_id() <= 0
		    || greeting.message_id() > std::numeric_limits<int16_t>::max())
			return tl::make_unexpected("Server-backed snapshot contains an invalid towner greeting.");
	}
	return {};
}

tl::expected<void, std::string> ValidateLevelGeometrySnapshots(const protocol::Snapshot &snapshot)
{
	std::unordered_map<uint32_t, const protocol::LevelGeometrySnapshot *> levels;
	levels.reserve(snapshot.level_geometry_size());
	for (const auto &level : snapshot.level_geometry()) {
		if (level.width() < 0 || level.height() < 0
		    || level.width() > NativeMaximumWidth || level.height() > NativeMaximumHeight
		    || (!level.native_mega_tile_layer().empty()
		        && ((static_cast<uint64_t>(level.width()) + 1) / 2 > NativeMaximumMegaWidth
		            || (static_cast<uint64_t>(level.height()) + 1) / 2 > NativeMaximumMegaHeight))
			|| !std::all_of(level.native_transparency().begin(), level.native_transparency().end(), [](uint32_t value) {
				return value <= std::numeric_limits<uint8_t>::max();
			})
		    || !std::all_of(level.native_object_layer().begin(), level.native_object_layer().end(), [](uint32_t value) {
				return value <= NativeMaximumDunObjectLayerId;
			})
		    || !std::all_of(level.native_mega_tile_layer().begin(), level.native_mega_tile_layer().end(), [](uint32_t value) {
				return value <= std::numeric_limits<uint8_t>::max();
			})
		    || !std::all_of(level.native_piece_layer().begin(), level.native_piece_layer().end(), [](uint32_t value) {
				return value <= NativeMaximumDunPieceId;
			}))
			return tl::make_unexpected("Server-backed snapshot contains invalid level geometry dimensions.");
		if (!levels.emplace(level.level_id(), &level).second)
			return tl::make_unexpected("Server-backed snapshot contains duplicate level geometry.");
		if (level.warp_triggers_size() > NativeMaximumWarpTriggerCount)
			return tl::make_unexpected("Server-backed snapshot contains too many warp triggers on one level.");
		if (level.width() == 0 || level.height() == 0)
			continue;
		const auto cellCount = static_cast<uint64_t>(level.width()) * static_cast<uint64_t>(level.height());
		const auto validateCells = [cellCount](const auto &cells) {
			if (static_cast<uint64_t>(cells.size()) > cellCount)
				return false;
			std::unordered_set<int32_t> uniqueCells;
			for (const auto cell : cells) {
				if (cell < 0 || static_cast<uint64_t>(cell) >= cellCount || !uniqueCells.insert(cell).second)
					return false;
			}
			return true;
		};
		if (!validateCells(level.blocked_cells()) || !validateCells(level.missile_blocked_cells())
		    || (level.native_transparency_size() != 0 && static_cast<uint64_t>(level.native_transparency_size()) != cellCount)
		    || (level.native_object_layer_size() != 0 && static_cast<uint64_t>(level.native_object_layer_size()) != cellCount)
		    || (level.native_mega_tile_layer_size() != 0
		        && static_cast<uint64_t>(level.native_mega_tile_layer_size())
		            != (static_cast<uint64_t>(level.width()) + 1) / 2 * ((static_cast<uint64_t>(level.height()) + 1) / 2))
		    || (level.native_piece_layer_size() != 0
		        && static_cast<uint64_t>(level.native_piece_layer_size()) != cellCount))
			return tl::make_unexpected("Server-backed snapshot contains invalid level geometry cells.");
	}
	std::unordered_set<uint32_t> questIds;
	questIds.reserve(snapshot.quests_size());
	for (const auto &quest : snapshot.quests())
		questIds.insert(quest.quest_id());
	std::unordered_set<uint32_t> warpIds;
	for (const auto &level : snapshot.level_geometry()) {
		for (const auto &warp : level.warp_triggers()) {
			const auto target = levels.find(warp.target_level_id());
			if (warp.warp_id() == 0 || !warpIds.insert(warp.warp_id()).second
			    || warp.trigger_x() < 0 || warp.trigger_y() < 0
			    || (level.width() > 0 && warp.trigger_x() >= level.width())
			    || (level.height() > 0 && warp.trigger_y() >= level.height())
			    || target == levels.end()
			    || warp.target_x() < 0 || warp.target_y() < 0
			    || (target->second->width() > 0 && warp.target_x() >= target->second->width())
			    || (target->second->height() > 0 && warp.target_y() >= target->second->height())
			    || (warp.required_quest_id() != 0 && questIds.find(warp.required_quest_id()) == questIds.end())
			    || (warp.required_quest_id() == 0 && warp.required_quest_completed())
			    || (warp.required_quest_id() == 0 && !warp.available())
			    || !std::all_of(warp.stable_symbol().begin(), warp.stable_symbol().end(), [](char value) {
					const auto character = static_cast<unsigned char>(value);
					return character >= 0x21 && character <= 0x7E;
				}))
				return tl::make_unexpected("Server-backed snapshot contains an invalid warp trigger.");
		}
	}
	return {};
}

tl::expected<void, std::string> ValidateSetLevelObjectOriginSnapshots(const protocol::Snapshot &snapshot)
{
	if (snapshot.set_level_object_origins_size() > MaximumSetLevelObjectOriginCount)
		return tl::make_unexpected("Server-backed snapshot contains too many set-level object origins.");
	std::unordered_set<int32_t> setLevelIds;
	setLevelIds.reserve(snapshot.set_level_object_origins_size());
	for (const auto &origin : snapshot.set_level_object_origins())
		if (origin.set_level_id() < 0 || origin.set_level_id() > MaximumNativeSetLevelId
		    || !setLevelIds.insert(origin.set_level_id()).second)
			return tl::make_unexpected("Server-backed snapshot contains invalid or duplicate set-level object origins.");
	if (snapshot.level_geometry_size() == 0)
		return {};
	std::unordered_map<uint32_t, const protocol::LevelGeometrySnapshot *> levels;
	levels.reserve(snapshot.level_geometry_size());
	for (const auto &level : snapshot.level_geometry())
		levels.emplace(level.level_id(), &level);
	for (const auto &origin : snapshot.set_level_object_origins()) {
		const auto level = levels.find(origin.level_id());
		if (level == levels.end()
		    || level->second->width() <= 0 || level->second->height() <= 0
		    || origin.position_x() < 0 || origin.position_y() < 0
		    || origin.position_x() >= level->second->width()
		    || origin.position_y() >= level->second->height())
			return tl::make_unexpected("Server-backed snapshot contains a set-level object origin outside projected level geometry.");
	}
	return {};
}

tl::expected<void, std::string> ValidateMaterializedProceduralLevelSnapshots(const protocol::Snapshot &snapshot)
{
	if (snapshot.materialized_procedural_level_ids_size() > MaximumMaterializedProceduralLevelCount)
		return tl::make_unexpected("Server-backed snapshot contains too many materialized procedural levels.");
	std::unordered_set<uint32_t> levelIds;
	levelIds.reserve(snapshot.materialized_procedural_level_ids_size());
	for (const uint32_t levelId : snapshot.materialized_procedural_level_ids())
		if (!levelIds.insert(levelId).second)
			return tl::make_unexpected("Server-backed snapshot contains duplicate materialized procedural levels.");
	return {};
}

tl::expected<void, std::string> ValidateEntityPositionSnapshots(const protocol::Snapshot &snapshot)
{
	std::unordered_map<uint32_t, const protocol::LevelGeometrySnapshot *> levels;
	levels.reserve(snapshot.level_geometry_size());
	for (const auto &level : snapshot.level_geometry()) {
		if (!levels.emplace(level.level_id(), &level).second)
			return tl::make_unexpected("Server-backed snapshot contains duplicate level geometry.");
	}
	const auto isInBounds = [&](uint32_t levelId, int32_t x, int32_t y) {
		if (!IsValidNativeTileCoordinate(x, y))
			return false;
		const auto level = levels.find(levelId);
		if (level == levels.end() || level->second->width() == 0 || level->second->height() == 0)
			return true;
		return x < level->second->width() && y < level->second->height();
	};
	const auto isValidMonster = [&](const protocol::MonsterSnapshot &monster) {
		return isInBounds(monster.level_id(), monster.position_x(), monster.position_y())
		    && (!monster.moving()
		        || isInBounds(monster.level_id(), monster.future_position_x(), monster.future_position_y()));
	};
	const auto invalidPlayer = std::any_of(snapshot.players().begin(), snapshot.players().end(), [&](const auto &player) {
		return !isInBounds(player.level_id(), player.position_x(), player.position_y());
	});
	const auto invalidMonster = std::any_of(snapshot.monsters().begin(), snapshot.monsters().end(), [&](const auto &monster) {
		return !isValidMonster(monster);
	});
	const auto invalidWorldItem = std::any_of(snapshot.world_items().begin(), snapshot.world_items().end(), [&](const auto &item) {
		return !isInBounds(item.level_id(), item.position_x(), item.position_y());
	});
	const auto invalidObject = std::any_of(snapshot.objects().begin(), snapshot.objects().end(), [&](const auto &object) {
		return !isInBounds(object.level_id(), object.position_x(), object.position_y());
	});
	const auto invalidProjectile = std::any_of(snapshot.projectiles().begin(), snapshot.projectiles().end(), [&](const auto &projectile) {
		return !isInBounds(projectile.level_id(), projectile.position_x(), projectile.position_y());
	});
	const auto invalidCorpse = std::any_of(snapshot.corpses().begin(), snapshot.corpses().end(), [&](const auto &corpse) {
		return !isInBounds(corpse.level_id(), corpse.position_x(), corpse.position_y());
	});
	const auto invalidPortal = std::any_of(snapshot.portals().begin(), snapshot.portals().end(), [&](const auto &portal) {
		return !isInBounds(portal.source_level_id(), portal.source_position_x(), portal.source_position_y())
		    || !isInBounds(portal.destination_level_id(), portal.destination_position_x(), portal.destination_position_y());
	});
	if (invalidPlayer || invalidMonster || invalidWorldItem || invalidObject || invalidProjectile || invalidCorpse || invalidPortal)
		return tl::make_unexpected("Server-backed snapshot contains an entity outside projected level geometry.");
	return {};
}

tl::expected<void, std::string> ValidateEntityIdentitySnapshots(const protocol::Snapshot &snapshot)
{
	std::unordered_set<uint32_t> entityIds;
	const auto validate = [&entityIds](const auto &entities) {
		for (const auto &entity : entities) {
			if (entity.entity_id() == 0 || !entityIds.insert(entity.entity_id()).second)
				return false;
		}
		return true;
	};
	if (!validate(snapshot.players())
	    || !validate(snapshot.monsters())
	    || !validate(snapshot.world_items())
	    || !validate(snapshot.objects())
	    || !validate(snapshot.projectiles())
	    || !validate(snapshot.corpses()))
		return tl::make_unexpected("Server-backed snapshot contains duplicate or zero entity IDs.");
	return {};
}

tl::expected<void, std::string> ValidatePlayerActionSnapshots(const protocol::Snapshot &snapshot)
{
	std::unordered_map<uint32_t, const protocol::LevelGeometrySnapshot *> levels;
	levels.reserve(snapshot.level_geometry_size());
	for (const auto &level : snapshot.level_geometry()) {
		if (!levels.emplace(level.level_id(), &level).second)
			return tl::make_unexpected("Server-backed snapshot contains duplicate level geometry.");
	}
	const auto isProjectedMoveWalkable = [&](const protocol::PlayerSnapshot &player, const protocol::PlayerActionSnapshot &action) {
		const auto level = levels.find(player.level_id());
		if (level == levels.end() || level->second->width() == 0 || level->second->height() == 0)
			return true;
		const int64_t targetX = static_cast<int64_t>(player.position_x()) + action.direction_x();
		const int64_t targetY = static_cast<int64_t>(player.position_y()) + action.direction_y();
		if (targetX < 0 || targetY < 0 || targetX >= level->second->width() || targetY >= level->second->height())
			return false;
		const int64_t targetCell = targetY * level->second->width() + targetX;
		if (targetCell > std::numeric_limits<int32_t>::max())
			return false;
		return std::find(level->second->blocked_cells().begin(), level->second->blocked_cells().end(), static_cast<int32_t>(targetCell))
		    == level->second->blocked_cells().end();
	};
	for (const auto &player : snapshot.players()) {
		if (!player.has_current_action())
			continue;
		const auto &action = player.current_action();
		const int32_t kind = static_cast<int32_t>(action.kind());
		const int32_t combatMode = static_cast<int32_t>(player.combat_mode());
		const bool timingIsValid = kind >= 1 && kind <= 6
		    && action.effect_tick() >= action.started_at_tick()
		    && action.ready_tick() >= action.effect_tick()
		    && action.ready_tick() <= action.end_tick()
		    && action.end_tick() == player.combat_mode_end_tick();
		const bool moveIsValid = kind == 1
		    && (combatMode == protocol::PLAYER_COMBAT_MODE_WALK_NORTHWARDS
		        || combatMode == protocol::PLAYER_COMBAT_MODE_WALK_SOUTHWARDS
		        || combatMode == protocol::PLAYER_COMBAT_MODE_WALK_SIDEWAYS)
		    && action.direction_x() >= -1 && action.direction_x() <= 1
		    && action.direction_y() >= -1 && action.direction_y() <= 1
		    && (action.direction_x() != 0 || action.direction_y() != 0)
		    && action.target_entity_id() == 0
		    && !action.has_cast()
		    && isProjectedMoveWalkable(player, action);
		const bool attackIsValid = kind == 2
		    && (combatMode == protocol::PLAYER_COMBAT_MODE_ATTACK
		        || combatMode == protocol::PLAYER_COMBAT_MODE_RANGED_ATTACK)
		    && action.direction_x() == 0 && action.direction_y() == 0
		    && action.target_entity_id() != 0 && !action.has_cast();
		const bool castIsValid = kind == 3
		    && combatMode == protocol::PLAYER_COMBAT_MODE_SPELL
		    && action.direction_x() == 0 && action.direction_y() == 0
		    && action.has_cast() && action.cast().spell_id() != 0
		    && action.target_entity_id() == action.cast().target_entity_id()
		    && static_cast<int32_t>(action.cast().source()) >= protocol::CAST_SOURCE_KIND_UNSPECIFIED
		    && static_cast<int32_t>(action.cast().source()) <= protocol::CAST_SOURCE_KIND_CONSUMABLE_ITEM
		    && (!action.cast().has_source_item()
		        || (static_cast<int32_t>(action.cast().source_item().location()) >= protocol::PLAYER_ITEM_LOCATION_INVENTORY
		            && static_cast<int32_t>(action.cast().source_item().location()) <= protocol::PLAYER_ITEM_LOCATION_STASH));
		const bool deathIsValid = kind == 4
		    && combatMode == protocol::PLAYER_COMBAT_MODE_DEATH
		    && player.life() == 0 && action.effect_applied()
		    && action.effect_tick() == action.started_at_tick()
		    && action.ready_tick() == action.end_tick()
		    && action.direction_x() == 0 && action.direction_y() == 0
		    && !action.has_cast();
		const bool transitionIsValid = kind == 5
		    && combatMode == protocol::PLAYER_COMBAT_MODE_NEW_LEVEL
		    && player.life() > 0 && action.effect_applied()
		    && action.effect_tick() == action.started_at_tick()
		    && action.ready_tick() == action.end_tick()
		    && action.direction_x() == 0 && action.direction_y() == 0
		    && action.target_entity_id() != 0 && !action.has_cast();
		const bool restartTownIsValid = kind == 6
		    && combatMode == protocol::PLAYER_COMBAT_MODE_NEW_LEVEL
		    && player.life() > 0 && action.effect_applied()
		    && action.effect_tick() == action.started_at_tick()
		    && action.ready_tick() == action.end_tick()
		    && action.direction_x() == 0 && action.direction_y() == 0
		    && action.target_entity_id() == 0 && !action.has_cast();
		if (!timingIsValid || (!moveIsValid && !attackIsValid && !castIsValid && !deathIsValid && !transitionIsValid && !restartTownIsValid))
			return tl::make_unexpected("Server-backed snapshot contains an invalid player action.");
	}
	return {};
}

tl::expected<void, std::string> ValidateMonsterSnapshots(const protocol::Snapshot &snapshot)
{
	std::unordered_map<uint32_t, const protocol::MonsterSnapshot *> monstersById;
	monstersById.reserve(snapshot.monsters_size());
	for (const auto &monster : snapshot.monsters()) {
		const int64_t hitPointsQ6 = monster.hit_points_q6() == 0 && monster.hit_points() != 0
		    ? static_cast<int64_t>(monster.hit_points()) * 64
		    : monster.hit_points_q6();
		if (monster.unique_identity().size() > MaximumIdentityBytes
		    || (!monster.is_unique() && !monster.unique_identity().empty())
		    || hitPointsQ6 < 0
		    || monster.alive() != (hitPointsQ6 > 0)
		    || monster.attack_damage() < 0
		    || monster.min_damage() < 0
		    || monster.max_damage() < monster.min_damage()
		    || monster.min_damage_special() < 0
		    || monster.max_damage_special() < monster.min_damage_special()
		    || monster.aggro_range() < 0
		    || monster.monster_level() < 0
		    || monster.death_animation_ticks() < 0
		    || monster.death_animation_ticks() > MaximumNativeMonsterDeathAnimationTicks
		    || monster.death_ticks_remaining() < 0
		    || monster.death_ticks_remaining() > monster.death_animation_ticks()
		    || (monster.hit_points_q6() > 0 && monster.death_ticks_remaining() != 0)
		    || monster.experience_reward() > MaximumNativeMonsterExperienceReward
		    || monster.to_hit() < 0 || monster.to_hit() > MaximumNativeMonsterToHit
		    || monster.to_hit_special() < 0 || monster.to_hit_special() > MaximumNativeMonsterToHit
		    || monster.intelligence() < 0 || monster.intelligence() > MaximumNativeMonsterIntelligence
		    || monster.direction() < 0 || monster.direction() > MaximumNativeMonsterDirection
		    || monster.fire_resistance() < MinimumNativeMonsterResistance || monster.fire_resistance() > MaximumNativeMonsterResistance
		    || monster.lightning_resistance() < MinimumNativeMonsterResistance || monster.lightning_resistance() > MaximumNativeMonsterResistance
		    || monster.magic_resistance() < MinimumNativeMonsterResistance || monster.magic_resistance() > MaximumNativeMonsterResistance
		    || monster.acid_resistance() < MinimumNativeMonsterResistance || monster.acid_resistance() > MaximumNativeMonsterResistance
		    || (!monster.is_player_minion() && monster.owner_player_id() != 0)
		    || monster.dialogue_message_id() < 0
		    || monster.dialogue_message_id() > std::numeric_limits<int16_t>::max()
		    || (monster.dialogue_message_id() != 0 && !monster.is_unique())
		    || monster.dialogue_state() < 0 || monster.dialogue_state() > MaximumMonsterDialogueState
		    || (monster.dialogue_state() != 0 && !monster.is_unique())
		    || monster.leader_relation() < 0 || monster.leader_relation() > MaximumMonsterLeaderRelation
		    || (monster.leader_relation() == 0
		        ? monster.leader_entity_id() != 0
		        : monster.leader_entity_id() == 0)
		    || monster.leader_relation() != 0 && monster.is_unique()
		    || monster.pack_size() < 0 || monster.pack_size() > MaximumNativeMonsterPackSize
		    || monster.active_for_ticks() < 0 || monster.active_for_ticks() > MaximumNativeMonsterActiveTicks
		    || (!monster.moving()
		        && (monster.movement_started_tick() != 0 || monster.movement_effect_tick() != 0))
		    || (monster.movement_started_tick() != 0 || monster.movement_effect_tick() != 0)
		        && monster.movement_effect_tick() <= monster.movement_started_tick())
			return tl::make_unexpected("Server-backed snapshot contains invalid monster combat state.");
		if (monster.entity_id() == 0 || !monstersById.emplace(monster.entity_id(), &monster).second)
			return tl::make_unexpected("Server-backed snapshot contains duplicate monster entity IDs.");
	}
	for (const auto &monster : snapshot.monsters()) {
		if (monster.leader_relation() == 0)
			continue;
		const auto leader = monstersById.find(monster.leader_entity_id());
		if (leader == monstersById.end()
		    || leader->second->entity_id() == monster.entity_id()
		    || !leader->second->alive()
		    || leader->second->level_id() != monster.level_id()) {
			return tl::make_unexpected("Server-backed snapshot contains an invalid monster pack leader.");
		}
	}
	return {};
}

tl::expected<void, std::string> ValidateProjectileSnapshots(const protocol::Snapshot &snapshot)
{
	const auto usesDirectionalPresentation = [](const protocol::ProjectileSnapshot &projectile) {
		if (projectile.native_visual() != protocol::NATIVE_PROJECTILE_VISUAL_UNSPECIFIED
		    || !projectile.native_missile_id().empty()) {
			return true;
		}
		switch (static_cast<ProjectileBehavior>(projectile.behavior())) {
		case ProjectileBehavior::ChargedBolt:
		case ProjectileBehavior::Firebolt:
		case ProjectileBehavior::Lightning:
		case ProjectileBehavior::LightningSegment:
		case ProjectileBehavior::FlashBottom:
		case ProjectileBehavior::FlashTop:
		case ProjectileBehavior::ResurrectionBeam:
		case ProjectileBehavior::Phasing:
		case ProjectileBehavior::Fireball:
		case ProjectileBehavior::HolyBolt:
		case ProjectileBehavior::BloodStar:
		case ProjectileBehavior::BoneSpirit:
		case ProjectileBehavior::FireWallSegment:
		case ProjectileBehavior::FlameWaveController:
		case ProjectileBehavior::FlameWaveSegment:
		case ProjectileBehavior::InfernoSegment:
		case ProjectileBehavior::ChainLightningBolt:
		case ProjectileBehavior::NovaBall:
		case ProjectileBehavior::ApocalypseBoom:
		case ProjectileBehavior::Elemental:
		case ProjectileBehavior::GuardianController:
		case ProjectileBehavior::Etherealize:
		case ProjectileBehavior::Acid:
		case ProjectileBehavior::AcidSplat:
		case ProjectileBehavior::AcidPuddle:
		case ProjectileBehavior::MonsterChargedBolt:
		case ProjectileBehavior::HorkSpawn:
		case ProjectileBehavior::WeaponArrow:
		case ProjectileBehavior::MonsterArrow:
			return true;
		default:
			return false;
		}
	};
	for (const auto &projectile : snapshot.projectiles()) {
		if (projectile.source_entity_id() == 0
		    || (projectile.spell_id() == 0
		        && projectile.behavior() != static_cast<int>(ProjectileBehavior::WeaponArrow)
		        && projectile.behavior() != static_cast<int>(ProjectileBehavior::MonsterArrow))
		    || projectile.remaining_ticks() == 0
		    || projectile.damage() < 0
		    || projectile.area_radius() < 0
		    || projectile.damage_minimum() < 0
		    || projectile.damage_maximum() < projectile.damage_minimum()
		    || projectile.damage_q6() < 0
		    || projectile.elemental_fire_damage() < 0
		    || projectile.elemental_lightning_damage() < 0
		    || projectile.peril_self_damage_q6() < 0
		    || projectile.effective_spell_level() < 0
		    || projectile.damage_type() < 0 || projectile.damage_type() > MaximumProjectileDamageType
		    || projectile.behavior() < 0 || projectile.behavior() > MaximumProjectileBehavior
		    || projectile.phase() < 0 || projectile.phase() > MaximumProjectilePhase
		    || static_cast<int32_t>(projectile.presentation_kind()) < 0
		    || static_cast<int32_t>(projectile.presentation_kind()) > MaximumProjectilePresentationKind
		    || static_cast<int32_t>(projectile.native_visual()) < 0
		    || static_cast<int32_t>(projectile.native_visual()) > MaximumNativeProjectileVisual
		    || usesDirectionalPresentation(projectile)
		        && (projectile.original_direction() < 0 || projectile.original_direction() > MaximumNativeMonsterDirection)
		    || !projectile.native_missile_id().empty()
		        && (projectile.behavior() != static_cast<int>(ProjectileBehavior::MonsterMagic)
		            || !IsValidNativeMissileIdentity(projectile.native_missile_id())))
			return tl::make_unexpected("Server-backed snapshot contains invalid projectile state.");
	}
	return {};
}

tl::expected<void, std::string> ValidateServerBackedEventBatch(const protocol::EventBatch &eventBatch)
{
	if (eventBatch.events_size() > MaximumEventBatchCount)
		return tl::make_unexpected("Server-backed event batch is too large.");
	for (const auto &event : eventBatch.events()) {
		if (event.has_damage()) {
			if (event.damage().amount() < 0
			    || event.damage().amount_q6() < 0
			    || (event.damage().amount_q6() == 0
			        && event.damage().amount() > std::numeric_limits<int32_t>::max() / 64))
				return tl::make_unexpected("Server-backed event batch contains invalid damage.");
			continue;
		}
		if (event.has_experience())
			continue;
		if (event.has_healing()) {
			if (event.healing().amount() < 0 || event.healing().amount_q6() < 0)
				return tl::make_unexpected("Server-backed event batch contains invalid healing.");
			continue;
		}
		if (event.has_dialogue()) {
			if (event.dialogue().message_id() < 0
			    || event.dialogue().text().find('\0') != std::string::npos
			    || event.dialogue().text().find('\r') != std::string::npos
			    || event.dialogue().text().size() > MaximumDialogueTextBytes)
				return tl::make_unexpected("Server-backed event batch contains invalid dialogue.");
			continue;
		}
		if (event.has_victory()) {
			if (event.victory().diablo_kill_level() > MaximumNativeDiabloKillLevel)
				return tl::make_unexpected("Server-backed event batch contains a Diablo kill level outside the native range.");
			continue;
		}
		if (event.has_map_opened()) {
			continue;
		}
		return tl::make_unexpected("Server-backed event batch contains an unknown event kind.");
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
		if (event.has_victory() && event.victory().player_entity_id() == entityId) {
			const auto killLevel = std::min<uint32_t>(event.victory().diablo_kill_level(), MaximumNativeDiabloKillLevel);
			player.pDiabloKillLevel = static_cast<uint8_t>(killLevel);
		}
		if (event.has_map_opened() && event.map_opened().player_entity_id() == entityId) {
			doom_init();
		}
		if (event.has_dialogue()
		    && event.dialogue().source_entity_id() == entityId) {
			if (!event.dialogue().text().empty()) {
				InitQTextMsgText(event.dialogue().text());
			} else if (event.dialogue().message_id() >= 0
			           && static_cast<std::size_t>(event.dialogue().message_id()) < Speeches.size()) {
				InitQTextMsg(static_cast<_speech_id>(event.dialogue().message_id()));
			}
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

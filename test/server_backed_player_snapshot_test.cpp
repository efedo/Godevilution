#include <gtest/gtest.h>

#include <array>
#include <limits>
#include <string>

#include "devilution.pb.h"
#include "engine/cursor.h"
#include "game/items/item_pool.hpp"
#include "game/missiles/missiles.hpp"
#include "game/monsters/monsters.hpp"
#include "game/levels/dungeon_common.h"
#include "game/levels/triggers.h"
#include "game/objects/object_pool.hpp"
#include "game/objects/objects.hpp"
#include "game/spells/spells.hpp"
#include "network/authoritative/player_snapshot.hpp"
#include "network/authoritative/protocol_limits.hpp"
#include "network/authoritative/authoritative_level_id.hpp"
#include "network/authoritative/item_snapshot.hpp"
#include "network/authoritative/server_backed_player_ui.hpp"
#include "network/authoritative/server_backed_world_presentation.hpp"
#include "network/authoritative/server_backed_world_projection.hpp"
#include "tables/playerdat.hpp"
#include "tables/monstdat.h"
#include "tables/objdat.h"
#include "ui/minitext.h"
#include "ui/automap.h"

namespace devilution::authoritative {
namespace protocol = ::devilution::protocol::v1;
namespace {

TEST(ServerBackedPlayerSnapshot, SpellTargetResolutionFailsClosedForMissingEntity)
{
	EXPECT_EQ(
	    ResolveServerBackedSpellTarget(true, true, true, true, false, true),
	    ServerBackedSpellTargetResolution::MissingAuthoritativeEntity);
	EXPECT_EQ(
	    ResolveServerBackedSpellTarget(false, true, false, true, false, false),
	    ServerBackedSpellTargetResolution::MissingAuthoritativeEntity);
}

TEST(ServerBackedPlayerSnapshot, SpellTargetResolutionPreservesNativePrecedence)
{
	EXPECT_EQ(
	    ResolveServerBackedSpellTarget(false, false, true, true, false, false),
	    ServerBackedSpellTargetResolution::Tile);
	EXPECT_EQ(
	    ResolveServerBackedSpellTarget(true, true, true, true, true, true),
	    ServerBackedSpellTargetResolution::Player);
	EXPECT_EQ(
	    ResolveServerBackedSpellTarget(false, true, false, true, false, true),
	    ServerBackedSpellTargetResolution::CombatEntity);
}

TEST(ServerBackedPlayerSnapshot, ProjectsResourcesInventoryAndEquipment)
{
	protocol::Snapshot snapshot;
	auto *player = snapshot.add_players();
	player->set_entity_id(7);
	player->set_position_x(11);
	player->set_position_y(-4);
	player->set_life(640);
	player->set_life_maximum(800);
	player->set_base_life_maximum(640);
	player->set_mana(32);
	player->set_mana_maximum(640);
	player->set_base_mana_maximum(512);
	player->set_light_radius(14);
	player->set_gold(125);
	player->set_experience(99);
	player->set_character_level(3);
	player->set_diablo_kill_level(2);
	player->set_reflect_charges(17);
	player->add_visited_level_ids(4);
	player->add_visited_level_ids(7);
	player->add_visited_level_ids(21);
	player->set_character_class_id(3);
	player->set_character_name("Dead-RogueDM");
	player->set_mana_shield_active(true);
	player->set_ability_spell_id(static_cast<uint32_t>(SpellID::Search));
	player->set_ability_targeting_kind(protocol::SPELL_TARGETING_KIND_TILE);
	player->set_hostile_mode(true);
	player->set_facing_direction(protocol::PLAYER_DIRECTION_NORTH_EAST);
	player->set_stash_gold(350);
	player->set_stash_open(true);
	auto *memorizedSpell = player->add_memorized_spells();
	memorizedSpell->set_spell_id(static_cast<uint32_t>(SpellID::Firebolt));
	memorizedSpell->set_level(2);
	memorizedSpell->set_targeting_kind(protocol::SPELL_TARGETING_KIND_COMBAT_ENTITY);
	player->set_level_id(7);
	player->add_status_effects()->set_effect_id(1);
	player->mutable_status_effects(0)->set_remaining_ticks(10);
	player->mutable_status_effects(0)->set_behavior(protocol::STATUS_EFFECT_BEHAVIOR_INFRAVISION);
	auto *search = player->add_status_effects();
	search->set_effect_id(static_cast<uint32_t>(SpellID::Search));
	search->set_remaining_ticks(255);
	search->set_behavior(protocol::STATUS_EFFECT_BEHAVIOR_SEARCH);
	player->mutable_attributes()->mutable_strength()->set_current(12);
	player->mutable_attributes()->mutable_strength()->set_base(10);
	for (int i = 0; i < 40; ++i)
		player->mutable_inventory_grid()->Add(i == 0 ? 0 : -1);
	auto *inventory = player->add_inventory();
	inventory->set_store_id(1);
	inventory->set_store_slot(2);
	inventory->set_item_seed(42);
	inventory->mutable_state()->set_stable_item_id(700);
	inventory->mutable_state()->set_spell_id(static_cast<uint32_t>(SpellID::Telekinesis));
	inventory->mutable_state()->set_spell_targeting_kind(protocol::SPELL_TARGETING_KIND_WORLD_ENTITY);
	inventory->set_price(75);
	inventory->set_purchased_at_tick(20);
	inventory->mutable_state()->set_item_type(4);
	auto *equipment = player->add_equipment();
	equipment->set_slot(0);
	equipment->set_item_seed(77);
	equipment->mutable_state()->set_stable_item_id(701);
	equipment->mutable_state()->set_item_type(5);
	auto *belt = player->add_belt();
	belt->set_slot(2);
	belt->set_item_seed(88);
	belt->mutable_state()->set_stable_item_id(702);
	belt->mutable_state()->set_item_type(6);
	auto *heldItem = player->mutable_held_item();
	heldItem->set_item_seed(99);
	heldItem->mutable_state()->set_stable_item_id(703);
	heldItem->set_price(65);
	heldItem->mutable_state()->set_item_type(static_cast<int32_t>(ItemType::Gold));
	heldItem->mutable_state()->set_item_index(IDI_GOLD);
	heldItem->mutable_state()->set_value(65);
	heldItem->mutable_state()->set_stat_flag(true);
	auto *stashItem = player->add_stash();
	stashItem->set_page(2);
	stashItem->set_cell(13);
	stashItem->mutable_item()->set_item_seed(1234);
	stashItem->mutable_item()->set_price(90);
	stashItem->mutable_item()->mutable_state()->set_stable_item_id(704);
	stashItem->mutable_item()->mutable_state()->set_item_type(7);
	stashItem->mutable_item()->mutable_state()->set_inventory_width(2);
	stashItem->mutable_item()->mutable_state()->set_inventory_height(1);

	auto projected = ProjectPlayerSnapshot(snapshot, 7);
	ASSERT_TRUE(projected.has_value()) << projected.error();
	EXPECT_EQ(projected->entityId, 7U);
	EXPECT_EQ(projected->positionX, 11);
	EXPECT_EQ(projected->life, 640);
	EXPECT_EQ(projected->lifeMaximum, 800);
	EXPECT_EQ(projected->lifeMaximumBase, 640);
	EXPECT_EQ(projected->manaMaximum, 640);
	EXPECT_EQ(projected->manaMaximumBase, 512);
	EXPECT_EQ(projected->lightRadius, 14);
	EXPECT_EQ(projected->gold, 125U);
	EXPECT_EQ(projected->diabloKillLevel, 2U);
	EXPECT_EQ(projected->reflectCharges, 17U);
	EXPECT_EQ(projected->visitedLevelIds, std::vector<uint32_t>({ 4, 7, 21 }));
	EXPECT_EQ(projected->characterLevel, 3U);
	EXPECT_EQ(projected->characterClassId, 3U);
	EXPECT_EQ(projected->characterName, "Dead-RogueDM");
	EXPECT_TRUE(projected->manaShieldActive);
	EXPECT_EQ(projected->abilitySpellId, static_cast<uint32_t>(SpellID::Search));
	EXPECT_EQ(projected->abilitySpellTargetingKind, ProjectedSpellTargetingKind::Tile);
	EXPECT_FALSE(projected->friendlyMode);
	EXPECT_EQ(projected->facingDirection, static_cast<uint32_t>(Direction::NorthEast));
	ASSERT_EQ(projected->memorizedSpells.size(), 1U);
	EXPECT_EQ(projected->memorizedSpells[0].spellId, static_cast<uint32_t>(SpellID::Firebolt));
	EXPECT_EQ(projected->memorizedSpells[0].level, 2U);
	EXPECT_EQ(projected->memorizedSpells[0].targetingKind, ProjectedSpellTargetingKind::CombatEntity);
	EXPECT_EQ(projected->levelId, 7U);
	ASSERT_EQ(projected->statusEffects.size(), 2U);
	EXPECT_EQ(projected->statusEffects[0].remainingTicks, 10U);
	EXPECT_EQ(projected->statusEffects[0].behavior, 1U);
	EXPECT_EQ(projected->statusEffects[1].remainingTicks, 255U);
	EXPECT_EQ(projected->statusEffects[1].behavior, 2U);
	EXPECT_EQ(projected->strength.base, 10);
	EXPECT_EQ(projected->strength.current, 12);
	ASSERT_EQ(projected->inventory.size(), 1U);
	EXPECT_EQ(projected->inventory[0].itemSeed, 42U);
	EXPECT_EQ(projected->inventory[0].stableItemId, 700U);
	EXPECT_EQ(projected->inventory[0].spellId, static_cast<uint32_t>(SpellID::Telekinesis));
	EXPECT_EQ(projected->inventory[0].spellTargetingKind, ProjectedSpellTargetingKind::WorldEntity);
	EXPECT_EQ(projected->inventory[0].item._itype, static_cast<ItemType>(4));
	ASSERT_EQ(projected->equipment.size(), 1U);
	EXPECT_EQ(projected->equipment[0].itemSeed, 77U);
	EXPECT_EQ(projected->equipment[0].stableItemId, 701U);
	ASSERT_EQ(projected->belt.size(), 1U);
	EXPECT_EQ(projected->belt[0].slot, 2U);
	EXPECT_EQ(projected->belt[0].itemSeed, 88U);
	EXPECT_EQ(projected->belt[0].stableItemId, 702U);
	ASSERT_TRUE(projected->heldItem.has_value());
	EXPECT_EQ(projected->heldItem->itemSeed, 99U);
	EXPECT_EQ(projected->heldItem->stableItemId, 703U);
	EXPECT_EQ(projected->heldItem->item._itype, ItemType::Gold);
	EXPECT_EQ(projected->heldItem->item._ivalue, 65);
	std::vector<int32_t> expectedInventoryGrid(40, -1);
	expectedInventoryGrid[0] = 0;
	EXPECT_EQ(projected->inventoryGrid, expectedInventoryGrid);
	EXPECT_EQ(projected->stashGold, 350U);
	EXPECT_TRUE(projected->stashOpen);
	ASSERT_EQ(projected->stash.size(), 1U);
	EXPECT_EQ(projected->stash[0].page, 2U);
	EXPECT_EQ(projected->stash[0].cell, 13U);
	EXPECT_EQ(projected->stash[0].itemSeed, 1234U);
	EXPECT_EQ(projected->stash[0].stableItemId, 704U);
	EXPECT_EQ(projected->stash[0].width, 2U);
	EXPECT_EQ(projected->stash[0].height, 1U);
}

TEST(ServerBackedPlayerSnapshot, ValidatesNonEmptyStateHashShape)
{
	protocol::Snapshot snapshot;
	snapshot.add_players()->set_entity_id(7);

	for (const std::string &hash : {
		     std::string("not-a-hash"),
		     std::string(63, '0'),
		     std::string(65, '0'),
		     std::string(64, 'g'),
	     }) {
		snapshot.set_state_sha256(hash);
		EXPECT_FALSE(ProjectPlayerSnapshot(snapshot, 7).has_value()) << hash;
	}

	snapshot.clear_state_sha256();
	EXPECT_TRUE(ProjectPlayerSnapshot(snapshot, 7).has_value());
	snapshot.set_state_sha256(std::string(64, 'A'));
	EXPECT_TRUE(ProjectPlayerSnapshot(snapshot, 7).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsInvalidLightRadius)
{
	protocol::Snapshot snapshot;
	auto *player = snapshot.add_players();
	player->set_entity_id(7);
	player->set_light_radius(16);

	EXPECT_FALSE(ProjectPlayerSnapshot(snapshot, 7).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsPlayerLevelOutsideNativeRange)
{
	protocol::Snapshot snapshot;
	auto *player = snapshot.add_players();
	player->set_entity_id(7);
	player->set_level_id(NUMLEVELS);

	EXPECT_FALSE(ProjectPlayerSnapshot(snapshot, 7).has_value());
}

TEST(ServerBackedPlayerSnapshot, ProjectsMaterializedSetLevelIdentity)
{
	protocol::Snapshot snapshot;
	auto *player = snapshot.add_players();
	player->set_entity_id(7);
	player->set_level_id(AuthoritativeRuntimeSetLevelIdBase + 1);

	auto projected = ProjectPlayerSnapshot(snapshot, 7);
	ASSERT_TRUE(projected.has_value()) << projected.error();
	EXPECT_EQ(projected->levelId, AuthoritativeRuntimeSetLevelIdBase + 1);
}

TEST(ServerBackedPlayerSnapshot, RejectsInvalidCharacterNameShape)
{
	protocol::Snapshot snapshot;
	snapshot.add_players()->set_entity_id(7);

	for (const std::string &name : {
		     std::string(32, 'A'),
		     std::string("bad\nname"),
		     std::string("bad\0name", 8),
	     }) {
		snapshot.mutable_players(0)->set_character_name(name);
		EXPECT_FALSE(ProjectPlayerSnapshot(snapshot, 7).has_value()) << name;
	}

	snapshot.mutable_players(0)->clear_character_name();
	EXPECT_TRUE(ProjectPlayerSnapshot(snapshot, 7).has_value());
}

TEST(ServerBackedPlayerSnapshot, ResolvesTargetingKindAcrossAuthoritativeSources)
{
	ProjectedPlayerSnapshot snapshot;
	snapshot.abilitySpellId = 10;
	snapshot.abilitySpellTargetingKind = ProjectedSpellTargetingKind::Player;
	snapshot.memorizedSpells.push_back({ 11, 1, ProjectedSpellTargetingKind::CombatEntity });
	snapshot.inventory.push_back({
	    .spellId = 12,
	    .spellTargetingKind = ProjectedSpellTargetingKind::WorldEntity,
	});
	snapshot.belt.push_back({
	    .spellId = 13,
	    .spellTargetingKind = ProjectedSpellTargetingKind::Tile,
	});

	EXPECT_EQ(ProjectedSpellTargetingKindForSpell(snapshot, 10), ProjectedSpellTargetingKind::Player);
	EXPECT_EQ(ProjectedSpellTargetingKindForSpell(snapshot, 11), ProjectedSpellTargetingKind::CombatEntity);
	EXPECT_EQ(ProjectedSpellTargetingKindForSpell(snapshot, 12), ProjectedSpellTargetingKind::WorldEntity);
	EXPECT_EQ(ProjectedSpellTargetingKindForSpell(snapshot, 13), ProjectedSpellTargetingKind::Tile);
	EXPECT_FALSE(ProjectedSpellTargetingKindForSpell(snapshot, 99).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsInvalidTargetingMetadataDuringProjection)
{
	protocol::Snapshot snapshot;
	auto *player = snapshot.add_players();
	player->set_entity_id(7);
	player->set_ability_targeting_kind(static_cast<protocol::SpellTargetingKind>(99));
	EXPECT_FALSE(ProjectPlayerSnapshot(snapshot, 7).has_value());

	player->set_ability_targeting_kind(protocol::SPELL_TARGETING_KIND_UNSPECIFIED);
	player->add_memorized_spells()->set_targeting_kind(static_cast<protocol::SpellTargetingKind>(99));
	EXPECT_FALSE(ProjectPlayerSnapshot(snapshot, 7).has_value());

	player->clear_memorized_spells();
	player->add_inventory()->mutable_state()->set_spell_targeting_kind(static_cast<protocol::SpellTargetingKind>(99));
	EXPECT_FALSE(ProjectPlayerSnapshot(snapshot, 7).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsUnrepresentableItemPricesDuringProjection)
{
	protocol::Snapshot snapshot;
	auto *player = snapshot.add_players();
	player->set_entity_id(7);
	auto *inventory = player->add_inventory();
	inventory->set_price(std::numeric_limits<uint32_t>::max());
	EXPECT_FALSE(ProjectPlayerSnapshot(snapshot, 7).has_value());

	protocol::Snapshot worldSnapshot;
	auto *worldItem = worldSnapshot.add_world_items();
	worldItem->set_entity_id(9);
	worldItem->set_price(std::numeric_limits<uint32_t>::max());
	EXPECT_FALSE(ProjectWorldItemSnapshots(worldSnapshot).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsDuplicatePlayerItemSeedsDuringProjection)
{
	protocol::Snapshot snapshot;
	auto *player = snapshot.add_players();
	player->set_entity_id(7);
	player->add_inventory()->set_item_seed(42);
	player->add_equipment()->set_item_seed(42);
	EXPECT_FALSE(ProjectPlayerSnapshot(snapshot, 7).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsMalformedPlayerItemTopologyDuringProjection)
{
	protocol::Snapshot invalidGridSize;
	auto *gridSizePlayer = invalidGridSize.add_players();
	gridSizePlayer->set_entity_id(7);
	gridSizePlayer->add_inventory_grid(0);
	EXPECT_FALSE(ProjectPlayerSnapshot(invalidGridSize, 7).has_value());

	protocol::Snapshot invalidGridReference;
	auto *gridReferencePlayer = invalidGridReference.add_players();
	gridReferencePlayer->set_entity_id(7);
	for (int i = 0; i < 40; ++i)
		gridReferencePlayer->add_inventory_grid(i == 0 ? 1 : -1);
	EXPECT_FALSE(ProjectPlayerSnapshot(invalidGridReference, 7).has_value());

	protocol::Snapshot invalidFootprint;
	auto *footprintPlayer = invalidFootprint.add_players();
	footprintPlayer->set_entity_id(7);
	auto *inventory = footprintPlayer->add_inventory();
	inventory->set_item_seed(42);
	inventory->mutable_state()->set_inventory_width(2);
	for (int i = 0; i < 40; ++i)
		footprintPlayer->add_inventory_grid(i == 0 ? 0 : -1);
	EXPECT_FALSE(ProjectPlayerSnapshot(invalidFootprint, 7).has_value());

	protocol::Snapshot duplicateEquipmentSlot;
	auto *equipmentPlayer = duplicateEquipmentSlot.add_players();
	equipmentPlayer->set_entity_id(7);
	equipmentPlayer->add_equipment()->set_slot(1);
	equipmentPlayer->add_equipment()->set_slot(1);
	EXPECT_FALSE(ProjectPlayerSnapshot(duplicateEquipmentSlot, 7).has_value());

	protocol::Snapshot invalidBeltSlot;
	auto *beltPlayer = invalidBeltSlot.add_players();
	beltPlayer->set_entity_id(7);
	beltPlayer->add_belt()->set_slot(8);
	EXPECT_FALSE(ProjectPlayerSnapshot(invalidBeltSlot, 7).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsInvalidPlayerItemIdentityDuringProjection)
{
	protocol::Snapshot snapshot;
	auto *player = snapshot.add_players();
	player->set_entity_id(7);
	player->add_inventory()->set_item_seed(0);
	EXPECT_FALSE(ProjectPlayerSnapshot(snapshot, 7).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsInvalidItemSemanticStateDuringProjection)
{
	protocol::Snapshot snapshot;
	auto *player = snapshot.add_players();
	player->set_entity_id(7);
	auto *inventory = player->add_inventory();
	inventory->set_item_seed(42);
	inventory->mutable_state()->set_charges(-1);
	EXPECT_FALSE(ProjectPlayerSnapshot(snapshot, 7).has_value());

	inventory->mutable_state()->set_charges(0);
	inventory->mutable_state()->set_presentation_kind(static_cast<protocol::ItemPresentationKind>(99));
	EXPECT_FALSE(ProjectPlayerSnapshot(snapshot, 7).has_value());

	inventory->mutable_state()->set_presentation_kind(protocol::ITEM_PRESENTATION_KIND_UNSPECIFIED);
	inventory->mutable_state()->set_spell_display_name(std::string("bad\0name", 8));
	EXPECT_FALSE(ProjectPlayerSnapshot(snapshot, 7).has_value());

	inventory->mutable_state()->clear_spell_display_name();
	inventory->mutable_state()->set_create_info(std::numeric_limits<uint16_t>::max() + 1U);
	EXPECT_FALSE(ProjectPlayerSnapshot(snapshot, 7).has_value());

	inventory->mutable_state()->set_create_info(0);
	inventory->mutable_state()->set_plus_damage(std::numeric_limits<int16_t>::max() + 1);
	EXPECT_FALSE(ProjectPlayerSnapshot(snapshot, 7).has_value());
}

TEST(ServerBackedPlayerSnapshot, AppliesAuthoritativeGoldAndEarPresentationIdentity)
{
	protocol::ItemStateSnapshot state;
	state.set_item_type(static_cast<int32_t>(ItemType::Sword));
	state.set_item_class(ICLASS_WEAPON);
	state.set_item_index(123);
	state.set_misc_id(IMISC_BOOK);
	state.set_presentation_kind(protocol::ITEM_PRESENTATION_KIND_GOLD);
	auto gold = ProjectNativeItem(state, 1, 10);
	ASSERT_TRUE(gold.has_value()) << gold.error();
	EXPECT_EQ(gold->_itype, ItemType::Gold);
	EXPECT_EQ(gold->_iClass, ICLASS_GOLD);
	EXPECT_EQ(gold->_iLoc, ILOC_UNEQUIPABLE);
	EXPECT_EQ(gold->_iMiscId, IMISC_NONE);
	EXPECT_EQ(gold->IDidx, IDI_GOLD);

	state.set_presentation_kind(protocol::ITEM_PRESENTATION_KIND_EAR);
	auto ear = ProjectNativeItem(state, 2, 0);
	ASSERT_TRUE(ear.has_value()) << ear.error();
	EXPECT_EQ(ear->_itype, ItemType::Misc);
	EXPECT_EQ(ear->_iClass, ICLASS_MISC);
	EXPECT_EQ(ear->_iLoc, ILOC_UNEQUIPABLE);
	EXPECT_EQ(ear->_iMiscId, IMISC_EAR);
	EXPECT_EQ(ear->IDidx, IDI_EAR);
}

TEST(ServerBackedPlayerSnapshot, AppliesAuthoritativeEventBatchToNativeResources)
{
	Player player;
	player._pExperience = 0;
	player.life.maximum = 100;
	player.life.maximumBase = 100;
	player.life.current = 80;
	protocol::EventBatch events;
	auto *damage = events.add_events()->mutable_damage();
	damage->set_target_entity_id(7);
	damage->set_amount_q6(25);
	auto *experience = events.add_events()->mutable_experience();
	experience->set_player_entity_id(7);
	experience->set_amount(40);
	auto *healing = events.add_events()->mutable_healing();
	healing->set_target_entity_id(7);
	healing->set_amount(10);
	auto *exactHealing = events.add_events()->mutable_healing();
	exactHealing->set_target_entity_id(7);
	exactHealing->set_amount_q6(5);
	auto *victory = events.add_events()->mutable_victory();
	victory->set_player_entity_id(7);
	victory->set_boss_entity_id(99);
	victory->set_diablo_kill_level(3);

	ApplyServerBackedEventBatch(player, events, 7);

	EXPECT_EQ(player.life.current, 70);
	EXPECT_EQ(player._pExperience, 40U);
	EXPECT_EQ(player.pDiabloKillLevel, 3U);
}

TEST(ServerBackedPlayerSnapshot, UsesServerOwnedDialogueTextBeforeLegacySpeechIdentity)
{
	Player player;
	qtextflag = false;
	protocol::EventBatch events;
	auto *dialogue = events.add_events()->mutable_dialogue();
	dialogue->set_source_entity_id(7);
	dialogue->set_message_id(0);
	dialogue->set_text("The server owns this greeting.");

	ApplyServerBackedEventBatch(player, events, 7);

	EXPECT_TRUE(qtextflag);
	qtextflag = false;
}

TEST(ServerBackedPlayerSnapshot, CapsAuthoritativeHealingBeforeProjectingFeedback)
{
	Player player;
	player.life.maximum = 100;
	player.life.maximumBase = 100;
	player.life.current = 95;
	protocol::EventBatch events;
	auto *healing = events.add_events()->mutable_healing();
	healing->set_target_entity_id(7);
	healing->set_amount_q6(20);

	ApplyServerBackedEventBatch(player, events, 7);

	EXPECT_EQ(player.life.current, 100);
}

TEST(ServerBackedPlayerSnapshot, RejectsMalformedAuthoritativeEventBatches)
{
	const std::array<void (*)(protocol::EventBatch *), 11> configure = {
		[](protocol::EventBatch *batch) { batch->add_events()->mutable_damage()->set_amount(-1); },
		[](protocol::EventBatch *batch) { batch->add_events()->mutable_damage()->set_amount_q6(-1); },
		[](protocol::EventBatch *batch) { batch->add_events()->mutable_damage()->set_amount(std::numeric_limits<int32_t>::max() / 64 + 1); },
		[](protocol::EventBatch *batch) { batch->add_events()->mutable_healing()->set_amount(-1); },
		[](protocol::EventBatch *batch) { batch->add_events()->mutable_healing()->set_amount_q6(-1); },
		[](protocol::EventBatch *batch) { batch->add_events()->mutable_dialogue()->set_message_id(-1); },
		[](protocol::EventBatch *batch) { batch->add_events()->mutable_dialogue()->set_text(std::string("bad\0text", 8)); },
		[](protocol::EventBatch *batch) { batch->add_events()->mutable_dialogue()->set_text("bad\rtext"); },
		[](protocol::EventBatch *batch) { batch->add_events()->mutable_dialogue()->set_text(std::string(16 * 1024 + 1, 'x')); },
		[](protocol::EventBatch *batch) { batch->add_events()->mutable_victory()->set_diablo_kill_level(std::numeric_limits<uint8_t>::max() + 1U); },
		[](protocol::EventBatch *batch) { batch->add_events(); },
	};
	for (const auto &configureEvent : configure) {
		protocol::EventBatch invalid;
		configureEvent(&invalid);
		auto result = ValidateServerBackedEventBatch(invalid);
		EXPECT_FALSE(result.has_value());
	}

	protocol::EventBatch maximumDialogue;
	maximumDialogue.add_events()->mutable_dialogue()->set_text(
	    std::string(MaximumDialogueTextBytes, 'x'));
	EXPECT_TRUE(ValidateServerBackedEventBatch(maximumDialogue).has_value());

	protocol::EventBatch oversized;
	for (std::size_t index = 0; index <= MaximumEventBatchCount; index++)
		oversized.add_events()->mutable_experience()->set_amount(static_cast<uint32_t>(index));
	EXPECT_FALSE(ValidateServerBackedEventBatch(oversized).has_value());
}

TEST(ServerBackedPlayerSnapshot, ProjectsCompleteTimedCastAction)
{
	protocol::Snapshot snapshot;
	snapshot.set_tick(12);
	auto *player = snapshot.add_players();
	player->set_entity_id(7);
	player->set_combat_mode(protocol::PLAYER_COMBAT_MODE_SPELL);
	player->set_combat_mode_end_tick(20);
	auto *action = player->mutable_current_action();
	action->set_kind(protocol::PLAYER_ACTION_KIND_CAST);
	action->set_started_at_tick(2);
	action->set_effect_tick(10);
	action->set_ready_tick(10);
	action->set_end_tick(20);
	action->set_effect_applied(true);
	action->set_target_entity_id(9);
	action->mutable_cast()->set_spell_id(30);
	action->mutable_cast()->set_target_entity_id(9);
	action->mutable_cast()->set_target_x(3);
	action->mutable_cast()->set_target_y(4);
	action->mutable_cast()->set_source(protocol::CAST_SOURCE_KIND_CHARGED_ITEM);
	action->mutable_cast()->mutable_source_item()->set_location(protocol::PLAYER_ITEM_LOCATION_EQUIPMENT);
	action->mutable_cast()->mutable_source_item()->set_slot(4);
	action->mutable_cast()->mutable_source_item()->set_stable_item_id(704);

	auto projected = ProjectPlayerSnapshot(snapshot, 7);

	ASSERT_TRUE(projected.has_value()) << projected.error();
	EXPECT_EQ(projected->serverTick, 12U);
	ASSERT_TRUE(projected->currentAction.has_value());
	EXPECT_EQ(projected->currentAction->kind, 3U);
	EXPECT_EQ(projected->currentAction->effectTick, 10U);
	EXPECT_TRUE(projected->currentAction->effectApplied);
	EXPECT_TRUE(projected->currentAction->hasCast);
	EXPECT_EQ(projected->currentAction->spellId, 30U);
	EXPECT_EQ(projected->currentAction->castTargetEntityId, 9U);
	EXPECT_TRUE(projected->currentAction->hasCastSourceItem);
	EXPECT_EQ(projected->currentAction->castSourceItemLocation, 2U);
	EXPECT_EQ(projected->currentAction->castSourceItemSlot, 4U);
	EXPECT_EQ(projected->currentAction->castSourceItemStableId, 704U);
}

TEST(ServerBackedPlayerSnapshot, SelectsRequestedPlayerAndRejectsInvalidEntitySets)
{
	protocol::Snapshot missing;
	EXPECT_FALSE(ProjectPlayerSnapshot(missing, 7).has_value());

	protocol::Snapshot multiple;
	multiple.add_players()->set_entity_id(8);
	multiple.add_players()->set_entity_id(7);
	auto selected = ProjectPlayerSnapshot(multiple, 7);
	ASSERT_TRUE(selected.has_value()) << selected.error();
	EXPECT_EQ(selected->entityId, 7U);
	auto allPlayers = ProjectPlayerSnapshots(multiple);
	ASSERT_TRUE(allPlayers.has_value()) << allPlayers.error();
	ASSERT_EQ(allPlayers->size(), 2U);
	EXPECT_EQ((*allPlayers)[0].entityId, 7U);
	EXPECT_EQ((*allPlayers)[1].entityId, 8U);

	protocol::Snapshot duplicate;
	duplicate.add_players()->set_entity_id(7);
	duplicate.add_players()->set_entity_id(8);
	duplicate.add_players()->set_entity_id(8);
	EXPECT_FALSE(ProjectPlayerSnapshot(duplicate, 7).has_value());
	EXPECT_FALSE(ProjectPlayerSnapshots(duplicate).has_value());

	protocol::Snapshot invalid;
	invalid.add_players()->set_entity_id(7);
	invalid.add_players()->set_entity_id(0);
	EXPECT_FALSE(ProjectPlayerSnapshot(invalid, 7).has_value());
	EXPECT_FALSE(ProjectPlayerSnapshots(invalid).has_value());

	protocol::Snapshot invalidNativeState;
	auto *invalidPlayer = invalidNativeState.add_players();
	invalidPlayer->set_entity_id(7);
	invalidPlayer->add_visited_level_ids(NUMLEVELS);
	EXPECT_FALSE(ProjectPlayerSnapshot(invalidNativeState, 7).has_value());
	invalidPlayer->clear_visited_level_ids();
	invalidPlayer->set_level_id(7);
	invalidPlayer->add_visited_level_ids(4);
	EXPECT_FALSE(ProjectPlayerSnapshot(invalidNativeState, 7).has_value());
	invalidPlayer->clear_visited_level_ids();
	invalidPlayer->set_reflect_charges(static_cast<uint32_t>(std::numeric_limits<uint16_t>::max()) + 1);
	EXPECT_FALSE(ProjectPlayerSnapshot(invalidNativeState, 7).has_value());
	invalidPlayer->set_reflect_charges(0);
	auto *invalidStatus = invalidPlayer->add_status_effects();
	invalidStatus->set_effect_id(1);
	invalidStatus->set_remaining_ticks(0);
	EXPECT_FALSE(ProjectPlayerSnapshot(invalidNativeState, 7).has_value());
	invalidPlayer->clear_status_effects();
	for (uint32_t effectId = 1; effectId <= MaximumStatusEffectCount + 1; ++effectId) {
		auto *status = invalidPlayer->add_status_effects();
		status->set_effect_id(effectId);
		status->set_remaining_ticks(1);
	}
	EXPECT_FALSE(ProjectPlayerSnapshot(invalidNativeState, 7).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsMorePlayersThanTheNativePool)
{
	protocol::Snapshot snapshot;
	for (uint32_t entityId = 1; entityId <= MAX_PLRS + 1; ++entityId)
		snapshot.add_players()->set_entity_id(entityId);

	EXPECT_FALSE(ProjectPlayerSnapshot(snapshot, 1).has_value());
	EXPECT_FALSE(ProjectPlayerSnapshots(snapshot).has_value());
}

TEST(ServerBackedPlayerSnapshot, ProjectsAndSortsAuthoritativeMonsters)
{
	protocol::Snapshot snapshot;
	const auto leader = snapshot.add_monsters();
	leader->set_entity_id(1);
	leader->set_hit_points(10);
	leader->set_max_hit_points(10);
	leader->set_alive(true);
	auto *second = snapshot.add_monsters();
	second->set_entity_id(9);
	second->set_monster_id(12);
	second->set_is_unique(true);
	second->set_unique_identity("Defiler");
	second->set_dialogue_message_id(274);
	second->set_dialogue_state(protocol::MONSTER_DIALOGUE_STATE_INQUIRING);
	second->set_level_id(2);
	second->set_position_x(4);
	second->set_position_y(5);
	second->set_hit_points(7);
	second->set_max_hit_points(20);
	second->set_hit_points_q6(432);
	second->set_max_hit_points_q6(1280);
	second->set_armor_class(3);
	second->set_alive(true);
	second->set_attack_damage(11);
	second->set_aggro_range(6);
	second->set_fire_resistance(25);
	second->set_lightning_resistance(-10);
	second->set_magic_resistance(40);
	second->set_direction(static_cast<int32_t>(Direction::NorthEast));
	second->set_moving(true);
	second->set_future_position_x(5);
	second->set_future_position_y(5);
	second->set_movement_started_tick(12);
	second->set_movement_effect_tick(13);
	second->set_petrified(true);
	second->set_sneak_hidden(true);
	second->set_stone_curse_immune(true);
	second->set_pack_size(2);
	second->set_active_for_ticks(5);
	const auto first = snapshot.add_monsters();
	first->set_entity_id(3);
	first->set_hit_points(0);
	first->set_max_hit_points(10);
	first->set_alive(false);

	auto projected = ProjectMonsterSnapshots(snapshot);
	ASSERT_TRUE(projected.has_value()) << projected.error();
	ASSERT_EQ(projected->size(), 3U);
	EXPECT_EQ((*projected)[0].entityId, 1U);
	EXPECT_TRUE((*projected)[0].alive);
	EXPECT_EQ((*projected)[1].entityId, 3U);
	EXPECT_FALSE((*projected)[1].alive);
	EXPECT_EQ((*projected)[2].entityId, 9U);
	EXPECT_EQ((*projected)[2].monsterId, 12U);
	EXPECT_TRUE((*projected)[2].isUnique);
	EXPECT_EQ((*projected)[2].uniqueIdentity, "Defiler");
	EXPECT_EQ((*projected)[2].dialogueMessageId, 274);
	EXPECT_EQ((*projected)[2].dialogueState, 1);
	EXPECT_EQ((*projected)[2].hitPoints, 7);
	EXPECT_EQ((*projected)[2].hitPointsQ6, 432);
	EXPECT_EQ((*projected)[2].maxHitPointsQ6, 1280);
	EXPECT_EQ((*projected)[2].attackDamage, 11);
	EXPECT_EQ((*projected)[2].aggroRange, 6);
	EXPECT_EQ((*projected)[2].fireResistance, 25);
	EXPECT_EQ((*projected)[2].lightningResistance, -10);
	EXPECT_EQ((*projected)[2].magicResistance, 40);
	EXPECT_EQ((*projected)[2].direction, static_cast<int32_t>(Direction::NorthEast));
	EXPECT_TRUE((*projected)[2].moving);
	EXPECT_EQ((*projected)[2].futurePositionX, 5);
	EXPECT_EQ((*projected)[2].futurePositionY, 5);
	EXPECT_EQ((*projected)[2].movementStartedTick, 12U);
	EXPECT_EQ((*projected)[2].movementEffectTick, 13U);
	EXPECT_TRUE((*projected)[2].petrified);
	EXPECT_TRUE((*projected)[2].sneakHidden);
	EXPECT_TRUE((*projected)[2].stoneCurseImmune);
	EXPECT_EQ((*projected)[2].leaderEntityId, 0U);
	EXPECT_EQ((*projected)[2].leaderRelation, 0);
	EXPECT_EQ((*projected)[2].packSize, 2);
	EXPECT_EQ((*projected)[2].activeForTicks, 5);
}

TEST(ServerBackedPlayerSnapshot, RejectsNativeMonsterPoolOverflowPerLevel)
{
	protocol::Snapshot snapshot;
	for (uint32_t entityId = 1; entityId <= MaxMonsters + 1; ++entityId) {
		auto *monster = snapshot.add_monsters();
		monster->set_entity_id(entityId);
		monster->set_level_id(1);
	}

	EXPECT_FALSE(ProjectMonsterSnapshots(snapshot).has_value());
}

TEST(ServerBackedPlayerSnapshot, ProjectsAuthoritativeMonsterPackRelation)
{
	protocol::Snapshot snapshot;
	auto *leader = snapshot.add_monsters();
	leader->set_entity_id(1);
	leader->set_level_id(2);
	leader->set_hit_points(10);
	leader->set_max_hit_points(10);
	leader->set_alive(true);
	auto *member = snapshot.add_monsters();
	member->set_entity_id(2);
	member->set_level_id(2);
	member->set_hit_points(5);
	member->set_max_hit_points(5);
	member->set_alive(true);
	member->set_leader_entity_id(1);
	member->set_leader_relation(1);

	auto projected = ProjectMonsterSnapshots(snapshot);
	ASSERT_TRUE(projected.has_value()) << projected.error();
	ASSERT_EQ(projected->size(), 2U);
	EXPECT_EQ((*projected)[1].leaderEntityId, 1U);
	EXPECT_EQ((*projected)[1].leaderRelation, 1);
}

TEST(ServerBackedPlayerSnapshot, NormalizesOmittedStationaryMonsterDestination)
{
	protocol::Snapshot snapshot;
	auto *monster = snapshot.add_monsters();
	monster->set_entity_id(1);
	monster->set_position_x(4);
	monster->set_position_y(5);
	monster->set_hit_points(10);
	monster->set_max_hit_points(10);
	monster->set_alive(true);

	auto projected = ProjectMonsterSnapshots(snapshot);
	ASSERT_TRUE(projected.has_value()) << projected.error();
	ASSERT_EQ(projected->size(), 1U);
	EXPECT_FALSE((*projected)[0].moving);
	EXPECT_EQ((*projected)[0].futurePositionX, 4);
	EXPECT_EQ((*projected)[0].futurePositionY, 5);
}

TEST(ServerBackedPlayerSnapshot, RejectsMonsterWithEffectiveMaxBelowCurrentHitPoints)
{
	protocol::Snapshot snapshot;
	auto *monster = snapshot.add_monsters();
	monster->set_entity_id(1);
	monster->set_hit_points(3);
	monster->set_max_hit_points(2);
	monster->set_alive(true);

	EXPECT_FALSE(ProjectMonsterSnapshots(snapshot).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsMalformedUniqueMonsterIdentity)
{
	protocol::Snapshot snapshot;
	auto *monster = snapshot.add_monsters();
	monster->set_entity_id(1);
	monster->set_is_unique(false);
	monster->set_unique_identity("not-unique");
	EXPECT_FALSE(ProjectMonsterSnapshots(snapshot).has_value());

	monster->clear_unique_identity();
	monster->set_is_unique(true);
	monster->set_unique_identity(std::string(129, 'x'));
	EXPECT_FALSE(ProjectMonsterSnapshots(snapshot).has_value());

	std::string oversizedUtf8Identity;
	for (int i = 0; i < 65; ++i)
		oversizedUtf8Identity += "\xC3\xA9";
	monster->set_unique_identity(oversizedUtf8Identity);
	EXPECT_FALSE(ProjectMonsterSnapshots(snapshot).has_value());
}

TEST(ServerBackedPlayerSnapshot, ProjectsAndSortsAuthoritativeWorldItems)
{
	devilution::protocol::v1::Snapshot snapshot;
	auto *second = snapshot.add_world_items();
	second->set_entity_id(20);
	second->set_item_seed(42);
	second->set_price(75);
	second->mutable_state()->set_item_type(static_cast<int32_t>(ItemType::Gold));
	second->mutable_state()->set_item_index(IDI_GOLD);
	second->mutable_state()->set_value(75);
	second->mutable_state()->set_identified_value(75);
	second->mutable_state()->set_stat_flag(true);
	auto *first = snapshot.add_world_items();
	first->set_entity_id(10);
	first->set_item_seed(43);
	first->set_price(80);
	first->mutable_state()->set_item_type(1);

	auto projected = ProjectWorldItemSnapshots(snapshot);
	ASSERT_TRUE(projected.has_value());
	ASSERT_EQ(projected->size(), 2U);
	EXPECT_EQ((*projected)[0].entityId, 10U);
	EXPECT_EQ((*projected)[1].entityId, 20U);
	EXPECT_EQ((*projected)[0].itemSeed, 43U);
	EXPECT_EQ((*projected)[1].item._itype, ItemType::Gold);
	EXPECT_EQ((*projected)[1].item.IDidx, IDI_GOLD);
	EXPECT_EQ((*projected)[1].item._ivalue, 75);
	EXPECT_EQ((*projected)[1].item._iSeed, 42U);
	EXPECT_TRUE((*projected)[1].item._iStatFlag);
}

TEST(ServerBackedPlayerSnapshot, EnforcesNativeWorldItemPoolCapacityPerLevel)
{
	protocol::Snapshot snapshot;
	for (int index = 0; index < MAXITEMS; ++index) {
		auto *worldItem = snapshot.add_world_items();
		worldItem->set_entity_id(static_cast<uint32_t>(100 + index));
		worldItem->set_level_id(1);
		worldItem->set_position_x(index);
		worldItem->set_item_seed(static_cast<uint32_t>(index + 1));
	}

	auto projected = ProjectWorldItemSnapshots(snapshot);
	ASSERT_TRUE(projected.has_value()) << projected.error();
	ASSERT_EQ(projected->size(), static_cast<size_t>(MAXITEMS));

	auto *overflow = snapshot.add_world_items();
	overflow->set_entity_id(1000);
	overflow->set_level_id(1);
	overflow->set_position_x(MAXITEMS);
	overflow->set_item_seed(1000);
	auto rejected = ProjectWorldItemSnapshots(snapshot);
	ASSERT_FALSE(rejected.has_value());
	EXPECT_NE(rejected.error().find("native world-item capacity"), std::string::npos);
}

TEST(ServerBackedPlayerSnapshot, EnforcesNativePortalPoolCapacity)
{
	protocol::Snapshot snapshot;
	for (uint32_t index = 1; index <= MAXPORTAL; ++index)
		snapshot.add_portals()->set_portal_id(index);

	auto projected = ProjectPortalSnapshots(snapshot);
	ASSERT_TRUE(projected.has_value()) << projected.error();
	ASSERT_EQ(projected->size(), static_cast<size_t>(MAXPORTAL));

	snapshot.add_portals()->set_portal_id(MAXPORTAL + 1);
	auto rejected = ProjectPortalSnapshots(snapshot);
	ASSERT_FALSE(rejected.has_value());
	EXPECT_NE(rejected.error().find("native portal capacity"), std::string::npos);
}

TEST(ServerBackedPlayerSnapshot, ProjectsZeroSeedPvpEarDynamicPresentation)
{
	protocol::Snapshot snapshot;
	auto *source = snapshot.add_world_items();
	source->set_entity_id(20);
	source->set_item_seed(0);
	auto *state = source->mutable_state();
	state->set_create_info(0x4100);
	state->set_item_type(static_cast<int32_t>(ItemType::Misc));
	state->set_equip_location(ILOC_UNEQUIPABLE);
	state->set_item_class(ICLASS_MISC);
	state->set_value(3);
	state->set_misc_id(IMISC_EAR);
	state->set_item_index(IDI_EAR);
	state->set_base_name("Ear of A");
	state->set_identified_name("A");
	state->set_native_cursor_graphic(ICURS_EAR_ROGUE);

	auto projected = ProjectWorldItemSnapshots(snapshot);

	ASSERT_TRUE(projected.has_value()) << projected.error();
	ASSERT_EQ(projected->size(), 1U);
	const Item &ear = projected->front().item;
	EXPECT_EQ(ear._iSeed, 0U);
	EXPECT_EQ(ear._iCreateInfo, 0x4100);
	EXPECT_EQ(ear.IDidx, IDI_EAR);
	EXPECT_EQ(ear._iMiscId, IMISC_EAR);
	EXPECT_EQ(ear._ivalue, 3);
	EXPECT_STREQ(ear._iName, "Ear of A");
	EXPECT_STREQ(ear._iIName, "A");
	EXPECT_EQ(ear._iCurs, ICURS_EAR_ROGUE);
}

TEST(ServerBackedPlayerSnapshot, RejectsIncompleteDynamicItemIdentity)
{
	protocol::Snapshot worldSnapshot;
	auto *worldItem = worldSnapshot.add_world_items();
	worldItem->set_entity_id(20);
	worldItem->mutable_state()->set_base_name("Ear of A");
	worldItem->mutable_state()->set_native_cursor_graphic(ICURS_EAR_ROGUE);
	EXPECT_FALSE(ProjectWorldItemSnapshots(worldSnapshot).has_value());

	protocol::Snapshot playerSnapshot;
	auto *player = playerSnapshot.add_players();
	player->set_entity_id(7);
	player->mutable_held_item()->mutable_state()->set_identified_name("A");
	EXPECT_FALSE(ProjectPlayerSnapshot(playerSnapshot, 7).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsInvalidStashFootprintsAndSessionConflicts)
{
	protocol::Snapshot snapshot;
	auto *player = snapshot.add_players();
	player->set_entity_id(7);
	auto *first = player->add_stash();
	first->set_page(0);
	first->set_cell(9);
	first->mutable_item()->set_item_seed(1);
	first->mutable_item()->mutable_state()->set_item_type(1);
	first->mutable_item()->mutable_state()->set_inventory_width(2);
	EXPECT_FALSE(ProjectPlayerSnapshot(snapshot, 7).has_value());

	first->mutable_item()->mutable_state()->set_inventory_width(1);
	auto *second = player->add_stash();
	second->set_page(0);
	second->set_cell(9);
	second->mutable_item()->set_item_seed(2);
	second->mutable_item()->mutable_state()->set_item_type(1);
	EXPECT_FALSE(ProjectPlayerSnapshot(snapshot, 7).has_value());

	player->clear_stash();
	player->set_stash_open(true);
	player->set_active_store_id(1);
	EXPECT_FALSE(ProjectPlayerSnapshot(snapshot, 7).has_value());
}

TEST(ServerBackedPlayerSnapshot, ProjectsAndSortsAuthoritativeObjects)
{
	protocol::Snapshot snapshot;
	snapshot.add_quests()->set_quest_id(30);
	auto *second = snapshot.add_objects();
	second->set_entity_id(20);
	second->set_object_id(2);
	second->set_native_object_id(107);
	second->set_level_id(1);
	second->set_position_x(8);
	second->set_position_y(9);
	second->set_activated(true);
	second->set_quest_id(30);
	second->set_effect_kind(static_cast<int32_t>(ProjectedObjectEffectKind::QuestSequence));
	second->set_effect_amount(2);
	second->set_quest_transition_kind(6);
	second->set_quest_transition_amount(2);
	second->set_reward_kind(2);
	second->set_reward_id(1);
	second->set_quest_sequence_follow_up_kind(1);
	second->set_quest_sequence_follow_up_amount(1);
	second->set_breakable(true);
	second->set_destroyed(true);
	second->set_trap_armed(true);
	auto *first = snapshot.add_objects();
	first->set_entity_id(10);
	first->set_object_id(1);
	first->set_level_id(1);
	first->set_position_x(4);
	first->set_position_y(4);
	first->set_missile_passable(true);

	auto projected = ProjectObjectSnapshots(snapshot);
	ASSERT_TRUE(projected.has_value());
	ASSERT_EQ(projected->size(), 2U);
	EXPECT_EQ((*projected)[0].entityId, 10U);
	EXPECT_EQ((*projected)[0].objectId, 1U);
	EXPECT_FALSE((*projected)[0].activated);
	EXPECT_TRUE((*projected)[0].missilePassable);
	EXPECT_EQ((*projected)[1].entityId, 20U);
	EXPECT_TRUE((*projected)[1].activated);
	EXPECT_EQ((*projected)[1].questId, 30U);
	EXPECT_EQ((*projected)[1].effectKind, static_cast<int32_t>(ProjectedObjectEffectKind::QuestSequence));
	EXPECT_EQ((*projected)[1].effectAmount, 2);
	EXPECT_EQ((*projected)[1].nativeObjectId, 107U);
	EXPECT_EQ((*projected)[1].questTransitionKind, 6);
	EXPECT_EQ((*projected)[1].rewardKind, 2);
	EXPECT_EQ((*projected)[1].questSequenceFollowUpKind, 1);
	EXPECT_TRUE((*projected)[1].breakable);
	EXPECT_TRUE((*projected)[1].destroyed);
	EXPECT_FALSE((*projected)[1].missilePassable);
	EXPECT_TRUE((*projected)[1].trapArmed);
}

TEST(ServerBackedPlayerSnapshot, RemovesUnclaimedCompatibilityWorldEntitySlots)
{
	InitializeItemPool();
	ObjectPoolAdapter::ResetLegacyObjectPools();

	const Point staleItemPosition { 10, 10 };
	const Point claimedItemPosition { 11, 11 };
	const unsigned staleItemSlot = 0;
	const unsigned claimedItemSlot = 1;
	ActiveItemCount = 2;
	Items[staleItemSlot] = {};
	Items[staleItemSlot]._iSeed = 101;
	Items[staleItemSlot].position = staleItemPosition;
	Items[claimedItemSlot] = {};
	Items[claimedItemSlot]._iSeed = 42;
	Items[claimedItemSlot].position = claimedItemPosition;
	tileAt(staleItemPosition).setItem(static_cast<int>(staleItemSlot + 1));
	tileAt(claimedItemPosition).setItem(static_cast<int>(claimedItemSlot + 1));

	const Point staleObjectPosition { 12, 12 };
	const Point claimedObjectPosition { 13, 13 };
	const int staleObjectSlot = ObjectPoolAdapter::ReserveObjectSlot();
	ASSERT_GE(staleObjectSlot, 0);
	Objects[staleObjectSlot] = {};
	Objects[staleObjectSlot]._otype = OBJ_BARREL;
	Objects[staleObjectSlot].position = staleObjectPosition;
	tileAt(staleObjectPosition).setObject(staleObjectSlot + 1);
	ObjectPoolAdapter::CommitReservedObjectSlot();
	const int claimedObjectSlot = ObjectPoolAdapter::ReserveObjectSlot();
	ASSERT_GE(claimedObjectSlot, 0);
	Objects[claimedObjectSlot] = {};
	Objects[claimedObjectSlot]._otype = OBJ_BARREL;
	Objects[claimedObjectSlot].position = claimedObjectPosition;
	tileAt(claimedObjectPosition).setObject(claimedObjectSlot + 1);
	ObjectPoolAdapter::CommitReservedObjectSlot();

	ProjectedWorldItemSnapshot claimedItem;
	claimedItem.entityId = 10;
	claimedItem.levelId = 1;
	claimedItem.positionX = claimedItemPosition.x;
	claimedItem.positionY = claimedItemPosition.y;
	claimedItem.itemSeed = 42;
	claimedItem.item._iSeed = 42;
	claimedItem.item._itype = ItemType::Gold;
	ProjectedObjectSnapshot claimedObject;
	claimedObject.entityId = 20;
	claimedObject.objectId = static_cast<uint32_t>(OBJ_BARREL);
	claimedObject.nativeObjectId = static_cast<uint32_t>(OBJ_BARREL);
	claimedObject.levelId = 1;
	claimedObject.positionX = claimedObjectPosition.x;
	claimedObject.positionY = claimedObjectPosition.y;

	ServerBackedWorldProjection projection;
	ASSERT_TRUE(projection.Apply({}, { claimedItem }, { claimedObject }, 1).has_value());
	ServerBackedWorldPresentation presentation;
	ASSERT_TRUE(presentation.Apply(projection, 1).has_value());

	ASSERT_EQ(ActiveItemCount, 1);
	EXPECT_EQ(tileAt(staleItemPosition).item(), 0);
	EXPECT_EQ(tileAt(claimedItemPosition).item(), static_cast<int>(claimedItemSlot + 1));
	ASSERT_EQ(ObjectPoolAdapter::ActiveObjectCountValue(), 1);
	EXPECT_EQ(ObjectPoolAdapter::ActiveObjectIndexAt(0), claimedObjectSlot);
	EXPECT_EQ(tileAt(staleObjectPosition).object(), 0);
	EXPECT_EQ(tileAt(claimedObjectPosition).object(), claimedObjectSlot + 1);

	presentation.Clear();
	EXPECT_EQ(ActiveItemCount, 0);
	EXPECT_EQ(ObjectPoolAdapter::ActiveObjectCountValue(), 0);
	EXPECT_EQ(tileAt(claimedItemPosition).item(), 0);
	EXPECT_EQ(tileAt(claimedObjectPosition).object(), 0);
	InitializeItemPool();
	ObjectPoolAdapter::ResetLegacyObjectPools();
}

TEST(ServerBackedPlayerSnapshot, RebindsPresentationMappingsAfterNativePoolReload)
{
	InitializeItemPool();
	ObjectPoolAdapter::ResetLegacyObjectPools();

	ProjectedWorldItemSnapshot firstItem;
	firstItem.entityId = 1;
	firstItem.levelId = 1;
	firstItem.positionX = 14;
	firstItem.positionY = 14;
	firstItem.itemSeed = 101;
	firstItem.item._iSeed = 101;
	firstItem.item._itype = ItemType::Gold;
	ServerBackedWorldProjection firstItemProjection;
	ASSERT_TRUE(firstItemProjection.Apply({}, { firstItem }, {}, 1).has_value());
	ServerBackedWorldPresentation itemPresentation;
	ASSERT_TRUE(itemPresentation.Apply(firstItemProjection, 1).has_value());

	// A native level reload can reuse slot zero while the authoritative mapping
	// still refers to the previous entity. The new entity must take ownership of
	// that slot without the old mapping deleting it during reconciliation.
	InitializeItemPool();
	Items[0] = {};
	Items[0]._iSeed = 202;
	Items[0].position = { 15, 15 };
	ActiveItems[0] = 0;
	ActiveItemCount = 1;
	tileAt(15, 15).setItem(1);

	ProjectedWorldItemSnapshot secondItem = firstItem;
	secondItem.entityId = 2;
	secondItem.positionX = 15;
	secondItem.positionY = 15;
	secondItem.itemSeed = 202;
	secondItem.item._iSeed = 202;
	ServerBackedWorldProjection secondItemProjection;
	ASSERT_TRUE(secondItemProjection.Apply({}, { secondItem }, {}, 1).has_value());
	ASSERT_TRUE(itemPresentation.Apply(secondItemProjection, 1).has_value());
	ASSERT_EQ(ActiveItemCount, 1);
	EXPECT_EQ(Items[0]._iSeed, 202U);
	EXPECT_EQ(tileAt(15, 15).item(), 1);
	itemPresentation.Clear();

	ObjectPoolAdapter::ResetLegacyObjectPools();
	const int firstObjectSlot = ObjectPoolAdapter::ReserveObjectSlot();
	ASSERT_EQ(firstObjectSlot, 0);
	Objects[firstObjectSlot] = {};
	Objects[firstObjectSlot]._otype = OBJ_BARREL;
	Objects[firstObjectSlot].position = { 18, 18 };
	tileAt(18, 18).setObject(firstObjectSlot + 1);
	ObjectPoolAdapter::CommitReservedObjectSlot();

	ProjectedObjectSnapshot firstObject;
	firstObject.entityId = 3;
	firstObject.objectId = OBJ_BARREL;
	firstObject.nativeObjectId = OBJ_BARREL;
	firstObject.levelId = 1;
	firstObject.positionX = 18;
	firstObject.positionY = 18;
	ServerBackedWorldProjection firstObjectProjection;
	ASSERT_TRUE(firstObjectProjection.Apply({}, {}, { firstObject }, 1).has_value());
	ServerBackedWorldPresentation objectPresentation;
	ASSERT_TRUE(objectPresentation.Apply(firstObjectProjection, 1).has_value());

	ObjectPoolAdapter::ResetLegacyObjectPools();
	const int secondObjectSlot = ObjectPoolAdapter::ReserveObjectSlot();
	ASSERT_EQ(secondObjectSlot, 0);
	Objects[secondObjectSlot] = {};
	Objects[secondObjectSlot]._otype = OBJ_BARREL;
	Objects[secondObjectSlot].position = { 19, 19 };
	tileAt(19, 19).setObject(secondObjectSlot + 1);
	ObjectPoolAdapter::CommitReservedObjectSlot();

	ProjectedObjectSnapshot secondObject = firstObject;
	secondObject.entityId = 4;
	secondObject.positionX = 19;
	secondObject.positionY = 19;
	ServerBackedWorldProjection secondObjectProjection;
	ASSERT_TRUE(secondObjectProjection.Apply({}, {}, { secondObject }, 1).has_value());
	ASSERT_TRUE(objectPresentation.Apply(secondObjectProjection, 1).has_value());
	ASSERT_EQ(ObjectPoolAdapter::ActiveObjectCountValue(), 1);
	EXPECT_EQ(ObjectPoolAdapter::ActiveObjectIndexAt(0), 0);
	EXPECT_EQ(tileAt(19, 19).object(), 1);
	objectPresentation.Clear();

	InitializeItemPool();
	ObjectPoolAdapter::ResetLegacyObjectPools();
}

TEST(ServerBackedPlayerSnapshot, ResynchronizesObjectTileOccupancyAcrossLifecycleStates)
{
	ObjectPoolAdapter::ResetLegacyObjectPools();
	const int objectSlot = ObjectPoolAdapter::ReserveObjectSlot();
	ASSERT_EQ(objectSlot, 0);
	Objects[objectSlot] = {};
	Objects[objectSlot]._otype = OBJ_BARREL;
	Objects[objectSlot].position = { 20, 20 };
	tileAt(20, 20).setObject(objectSlot + 1);
	ObjectPoolAdapter::CommitReservedObjectSlot();

	ProjectedObjectSnapshot snapshot;
	snapshot.entityId = 77;
	snapshot.objectId = OBJ_BARREL;
	snapshot.nativeObjectId = OBJ_BARREL;
	snapshot.levelId = 1;
	snapshot.positionX = 20;
	snapshot.positionY = 20;
	ServerBackedWorldProjection projection;
	ASSERT_TRUE(projection.Apply({}, {}, { snapshot }, 1).has_value());
	ServerBackedWorldPresentation presentation;
	ASSERT_TRUE(presentation.Apply(projection, 1).has_value());
	ASSERT_EQ(tileAt(20, 20).object(), objectSlot + 1);

	snapshot.activated = true;
	ASSERT_TRUE(projection.Apply({}, {}, { snapshot }, 1).has_value());
	ASSERT_TRUE(presentation.Apply(projection, 1).has_value());
	EXPECT_EQ(tileAt(20, 20).object(), 0);
	EXPECT_TRUE(Objects[objectSlot]._oDelFlag);

	snapshot.keepVisibleAfterActivation = true;
	snapshot.positionX = 21;
	ASSERT_TRUE(projection.Apply({}, {}, { snapshot }, 1).has_value());
	ASSERT_TRUE(presentation.Apply(projection, 1).has_value());
	EXPECT_EQ(tileAt(20, 20).object(), 0);
	EXPECT_EQ(tileAt(21, 20).object(), objectSlot + 1);
	EXPECT_FALSE(Objects[objectSlot]._oDelFlag);

	snapshot.destroyed = true;
	ASSERT_TRUE(projection.Apply({}, {}, { snapshot }, 1).has_value());
	ASSERT_TRUE(presentation.Apply(projection, 1).has_value());
	EXPECT_EQ(tileAt(21, 20).object(), 0);
	EXPECT_TRUE(Objects[objectSlot]._oDelFlag);

	presentation.Clear();
	ObjectPoolAdapter::ResetLegacyObjectPools();
}

TEST(ServerBackedPlayerSnapshot, ProjectsAndSortsAuthoritativeProjectiles)
{
	protocol::Snapshot snapshot;
	auto *second = snapshot.add_projectiles();
	second->set_entity_id(12);
	second->set_source_entity_id(1);
	second->set_spell_id(4);
	second->set_remaining_ticks(2);
	second->set_damage(6);
	auto *first = snapshot.add_projectiles();
	first->set_entity_id(11);
	first->set_source_entity_id(1);
	first->set_spell_id(4);
	first->set_remaining_ticks(1);
	first->set_damage(6);
	first->set_behavior(7);
	first->set_phase(1);
	first->set_start_x(2);
	first->set_start_y(3);
	first->set_traveled_x(4);
	first->set_traveled_y(5);
	first->set_velocity_x(-6);
	first->set_velocity_y(7);
	first->set_original_direction(8);
	first->set_path_index(9);
	first->set_path_ticks(10);
	first->set_speed(11);
	first->set_impact_lifetime(12);
	first->set_resistant_damage_divisor(13);
	first->set_last_collision_target_entity_id(9);
	first->set_effective_spell_level(14);
	first->set_native_missile_id("BlueFlare2");

	auto projected = ProjectProjectileSnapshots(snapshot);
	ASSERT_TRUE(projected.has_value());
	ASSERT_EQ(projected->size(), 2u);
	EXPECT_EQ((*projected)[0].entityId, 11u);
	EXPECT_EQ((*projected)[0].behavior, 7);
	EXPECT_EQ((*projected)[0].phase, 1);
	EXPECT_EQ((*projected)[0].startX, 2);
	EXPECT_EQ((*projected)[0].startY, 3);
	EXPECT_EQ((*projected)[0].traveledX, 4);
	EXPECT_EQ((*projected)[0].traveledY, 5);
	EXPECT_EQ((*projected)[0].velocityX, -6);
	EXPECT_EQ((*projected)[0].velocityY, 7);
	EXPECT_EQ((*projected)[0].originalDirection, 8);
	EXPECT_EQ((*projected)[0].pathIndex, 9);
	EXPECT_EQ((*projected)[0].pathTicks, 10);
	EXPECT_EQ((*projected)[0].speed, 11);
	EXPECT_EQ((*projected)[0].impactLifetime, 12);
	EXPECT_EQ((*projected)[0].resistantDamageDivisor, 13);
	EXPECT_EQ((*projected)[0].lastCollisionTargetEntityId, 9u);
	EXPECT_EQ((*projected)[0].effectiveSpellLevel, 14);
	EXPECT_EQ((*projected)[0].nativeMissileId, "BlueFlare2");
	EXPECT_EQ((*projected)[1].entityId, 12u);
}

TEST(ServerBackedPlayerSnapshot, ProjectsPortalsInStableOrderAndRejectsDuplicates)
{
	protocol::Snapshot snapshot;
	auto *second = snapshot.add_portals();
	second->set_portal_id(9);
	second->set_source_level_id(1);
	second->set_source_position_x(10);
	second->set_source_position_y(11);
	second->set_destination_level_id(0);
	second->set_destination_position_x(57);
	second->set_destination_position_y(40);
	second->set_remaining_ticks(100);
	second->set_owner_entity_id(4);
	second->set_bidirectional(true);
	auto *first = snapshot.add_portals();
	first->set_portal_id(3);
	first->set_source_level_id(2);
	first->set_source_position_x(4);
	first->set_source_position_y(5);
	first->set_destination_level_id(0);
	first->set_destination_position_x(57);
	first->set_destination_position_y(40);

	auto projected = ProjectPortalSnapshots(snapshot);
	ASSERT_TRUE(projected.has_value()) << projected.error();
	ASSERT_EQ(projected->size(), 2U);
	EXPECT_EQ((*projected)[0].portalId, 3U);
	EXPECT_EQ((*projected)[0].sourceLevelId, 2U);
	EXPECT_EQ((*projected)[1].portalId, 9U);
	EXPECT_EQ((*projected)[1].remainingTicks, 100U);
	EXPECT_EQ((*projected)[1].ownerEntityId, 4U);
	EXPECT_TRUE((*projected)[1].bidirectional);

	snapshot.add_portals()->set_portal_id(3);
	EXPECT_FALSE(ProjectPortalSnapshots(snapshot).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsMalformedPortalRouteState)
{
	protocol::Snapshot invalidOwner;
	auto *portal = invalidOwner.add_portals();
	portal->set_portal_id(1);
	portal->set_bidirectional(true);
	portal->set_remaining_ticks(5);
	EXPECT_FALSE(ProjectPortalSnapshots(invalidOwner).has_value());

	protocol::Snapshot invalidLifetime;
	portal = invalidLifetime.add_portals();
	portal->set_portal_id(1);
	portal->set_bidirectional(true);
	portal->set_owner_entity_id(2);
	EXPECT_FALSE(ProjectPortalSnapshots(invalidLifetime).has_value());

	protocol::Snapshot oneWayState;
	portal = oneWayState.add_portals();
	portal->set_portal_id(1);
	portal->set_owner_entity_id(2);
	EXPECT_FALSE(ProjectPortalSnapshots(oneWayState).has_value());

	protocol::Snapshot invalidCoordinate;
	portal = invalidCoordinate.add_portals();
	portal->set_portal_id(1);
	portal->set_source_position_x(-1);
	EXPECT_FALSE(ProjectPortalSnapshots(invalidCoordinate).has_value());

	protocol::Snapshot oversizedCoordinate;
	portal = oversizedCoordinate.add_portals();
	portal->set_portal_id(1);
	portal->set_destination_position_y(256);
	EXPECT_FALSE(ProjectPortalSnapshots(oversizedCoordinate).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsMalformedQuestState)
{
	protocol::Snapshot missingId;
	missingId.add_quests()->set_required_progress(1);
	EXPECT_FALSE(ValidateQuestSnapshots(missingId).has_value());

	protocol::Snapshot duplicate;
	duplicate.add_quests()->set_quest_id(1);
	duplicate.mutable_quests(0)->set_required_progress(1);
	duplicate.add_quests()->set_quest_id(1);
	duplicate.mutable_quests(1)->set_required_progress(1);
	EXPECT_FALSE(ValidateQuestSnapshots(duplicate).has_value());

	protocol::Snapshot impossibleProgress;
	auto *quest = impossibleProgress.add_quests();
	quest->set_quest_id(1);
	quest->set_required_progress(2);
	quest->set_progress(3);
	EXPECT_FALSE(ValidateQuestSnapshots(impossibleProgress).has_value());

	protocol::Snapshot unavailableProgress;
	quest = unavailableProgress.add_quests();
	quest->set_quest_id(1);
	quest->set_required_progress(1);
	quest->set_unavailable(true);
	quest->set_progress(1);
	EXPECT_FALSE(ValidateQuestSnapshots(unavailableProgress).has_value());

	protocol::Snapshot invalidMetadata;
	quest = invalidMetadata.add_quests();
	quest->set_quest_id(1);
	quest->set_required_progress(1);
	quest->set_variable_1(256);
	EXPECT_FALSE(ValidateQuestSnapshots(invalidMetadata).has_value());

	protocol::Snapshot oversizedIdentity;
	quest = oversizedIdentity.add_quests();
	quest->set_quest_id(1);
	quest->set_required_progress(1);
	quest->set_completion_unique_identity(std::string(129, 'x'));
	EXPECT_FALSE(ValidateQuestSnapshots(oversizedIdentity).has_value());

	protocol::Snapshot oversizedUtf8Identity;
	quest = oversizedUtf8Identity.add_quests();
	quest->set_quest_id(1);
	quest->set_required_progress(1);
	std::string utf8Identity;
	for (int i = 0; i < 65; ++i)
		utf8Identity += "\xC3\xA9";
	quest->set_completion_unique_identity(utf8Identity);
	EXPECT_FALSE(ValidateQuestSnapshots(oversizedUtf8Identity).has_value());

	protocol::Snapshot invalidNativeQuestId;
	quest = invalidNativeQuestId.add_quests();
	quest->set_quest_id(1);
	quest->set_required_progress(1);
	quest->set_native_quest_id(MAXQUESTS);
	EXPECT_FALSE(ValidateQuestSnapshots(invalidNativeQuestId).has_value());

	protocol::Snapshot duplicateNativeQuestIds;
	quest = duplicateNativeQuestIds.add_quests();
	quest->set_quest_id(1);
	quest->set_required_progress(1);
	quest->set_native_quest_id(0);
	quest = duplicateNativeQuestIds.add_quests();
	quest->set_quest_id(2);
	quest->set_required_progress(1);
	quest->set_native_quest_id(0);
	EXPECT_FALSE(ValidateQuestSnapshots(duplicateNativeQuestIds).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsMalformedQuestTownerInteractions)
{
	protocol::Snapshot missingId;
	missingId.add_quests()->set_quest_id(1);
	missingId.mutable_quests(0)->set_required_progress(1);
	auto *interaction = missingId.add_quest_towner_interactions();
	interaction->set_quest_id(1);
	interaction->set_message_id(1);
	EXPECT_FALSE(ValidateQuestTownerInteractions(missingId).has_value());

	protocol::Snapshot unknownQuest;
	interaction = unknownQuest.add_quest_towner_interactions();
	interaction->set_interaction_id(1);
	interaction->set_quest_id(2);
	interaction->set_message_id(1);
	EXPECT_FALSE(ValidateQuestTownerInteractions(unknownQuest).has_value());

	protocol::Snapshot duplicate;
	duplicate.add_quests()->set_quest_id(1);
	duplicate.mutable_quests(0)->set_required_progress(1);
	interaction = duplicate.add_quest_towner_interactions();
	interaction->set_interaction_id(1);
	interaction->set_quest_id(1);
	interaction->set_message_id(1);
	interaction = duplicate.add_quest_towner_interactions();
	interaction->set_interaction_id(1);
	interaction->set_quest_id(1);
	interaction->set_message_id(1);
	EXPECT_FALSE(ValidateQuestTownerInteractions(duplicate).has_value());

	protocol::Snapshot invalidMessage;
	invalidMessage.add_quests()->set_quest_id(1);
	invalidMessage.mutable_quests(0)->set_required_progress(1);
	interaction = invalidMessage.add_quest_towner_interactions();
	interaction->set_interaction_id(1);
	interaction->set_quest_id(1);
	EXPECT_FALSE(ValidateQuestTownerInteractions(invalidMessage).has_value());

	protocol::Snapshot outsideGeometry;
	outsideGeometry.add_quests()->set_quest_id(1);
	outsideGeometry.mutable_quests(0)->set_required_progress(1);
	auto *level = outsideGeometry.add_level_geometry();
	level->set_level_id(2);
	level->set_width(10);
	level->set_height(10);
	interaction = outsideGeometry.add_quest_towner_interactions();
	interaction->set_interaction_id(1);
	interaction->set_quest_id(1);
	interaction->set_level_id(2);
	interaction->set_position_x(10);
	interaction->set_message_id(1);
	EXPECT_FALSE(ValidateQuestTownerInteractions(outsideGeometry).has_value());

	protocol::Snapshot completionAfterQuest;
	completionAfterQuest.add_quests()->set_quest_id(1);
	completionAfterQuest.mutable_quests(0)->set_required_progress(1);
	interaction = completionAfterQuest.add_quest_towner_interactions();
	interaction->set_interaction_id(1);
	interaction->set_quest_id(1);
	interaction->set_kind(4);
	interaction->set_allow_completed(true);
	interaction->set_message_id(1);
	EXPECT_TRUE(ValidateQuestTownerInteractions(completionAfterQuest).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsMalformedTownerGreetings)
{
	protocol::Snapshot duplicate;
	for (const auto messageId : { 150, 151 }) {
		auto *greeting = duplicate.add_towner_greetings();
		greeting->set_towner_id(4);
		greeting->set_message_id(messageId);
	}
	EXPECT_FALSE(ValidateTownerGreetingSnapshots(duplicate).has_value());

	protocol::Snapshot invalidMessage;
	auto *greeting = invalidMessage.add_towner_greetings();
	greeting->set_towner_id(4);
	EXPECT_FALSE(ValidateTownerGreetingSnapshots(invalidMessage).has_value());

	protocol::Snapshot outsideGeometry;
	auto *level = outsideGeometry.add_level_geometry();
	level->set_level_id(1);
	level->set_width(10);
	level->set_height(10);
	greeting = outsideGeometry.add_towner_greetings();
	greeting->set_towner_id(4);
	greeting->set_level_id(1);
	greeting->set_position_x(10);
	greeting->set_message_id(150);
	EXPECT_FALSE(ValidateTownerGreetingSnapshots(outsideGeometry).has_value());

	protocol::Snapshot valid;
	greeting = valid.add_towner_greetings();
	greeting->set_towner_id(4);
	greeting->set_level_id(1);
	greeting->set_position_x(2);
	greeting->set_position_y(3);
	greeting->set_message_id(150);
	EXPECT_TRUE(ValidateTownerGreetingSnapshots(valid).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsMalformedLevelGeometryAndWarpTriggers)
{
	protocol::Snapshot negativeDimensions;
	negativeDimensions.add_level_geometry()->set_width(-1);
	EXPECT_FALSE(ValidateLevelGeometrySnapshots(negativeDimensions).has_value());

	protocol::Snapshot duplicateLevels;
	duplicateLevels.add_level_geometry()->set_level_id(1);
	duplicateLevels.add_level_geometry()->set_level_id(1);
	EXPECT_FALSE(ValidateLevelGeometrySnapshots(duplicateLevels).has_value());

	protocol::Snapshot invalidCells;
	auto *level = invalidCells.add_level_geometry();
	level->set_level_id(1);
	level->set_width(2);
	level->set_height(2);
	level->add_blocked_cells(4);
	level->add_blocked_cells(4);
	EXPECT_FALSE(ValidateLevelGeometrySnapshots(invalidCells).has_value());

	protocol::Snapshot invalidLayer;
	level = invalidLayer.add_level_geometry();
	level->set_width(1);
	level->set_height(1);
	level->add_native_transparency(65536);
	level->add_native_piece_layer(65536);
	EXPECT_FALSE(ValidateLevelGeometrySnapshots(invalidLayer).has_value());

	protocol::Snapshot invalidNativePresentation;
	level = invalidNativePresentation.add_level_geometry();
	level->set_width(1);
	level->set_height(1);
	level->add_native_transparency(128);
	EXPECT_TRUE(ValidateLevelGeometrySnapshots(invalidNativePresentation).has_value());
	level->clear_native_transparency();
	level->add_native_transparency(256);
	EXPECT_FALSE(ValidateLevelGeometrySnapshots(invalidNativePresentation).has_value());
	level->clear_native_transparency();
	level->add_native_mega_tile_layer(256);
	EXPECT_FALSE(ValidateLevelGeometrySnapshots(invalidNativePresentation).has_value());
	level->clear_native_mega_tile_layer();
	level->add_native_object_layer(static_cast<uint32_t>(DUN_OBJECT_ID_LAST) + 1);
	EXPECT_FALSE(ValidateLevelGeometrySnapshots(invalidNativePresentation).has_value());

	protocol::Snapshot invalidNativeDimensions;
	level = invalidNativeDimensions.add_level_geometry();
	level->set_width(MAXDUNX + 1);
	level->set_height(1);
	EXPECT_FALSE(ValidateLevelGeometrySnapshots(invalidNativeDimensions).has_value());
	level->set_width(82);
	level->set_height(82);
	for (int index = 0; index < 41 * 41; ++index)
		level->add_native_mega_tile_layer(0);
	EXPECT_FALSE(ValidateLevelGeometrySnapshots(invalidNativeDimensions).has_value());

	protocol::Snapshot validNativeTown;
	level = validNativeTown.add_level_geometry();
	level->set_width(MAXDUNX);
	level->set_height(MAXDUNY);
	EXPECT_TRUE(ValidateLevelGeometrySnapshots(validNativeTown).has_value());

	protocol::Snapshot tooManyTriggers;
	level = tooManyTriggers.add_level_geometry();
	level->set_level_id(1);
	level->set_width(10);
	level->set_height(10);
	for (uint32_t warpId = 1; warpId <= MAXTRIGGERS + 1; ++warpId) {
		auto *warp = level->add_warp_triggers();
		warp->set_warp_id(warpId);
		warp->set_trigger_x(static_cast<int32_t>(warpId - 1));
		warp->set_target_level_id(1);
		warp->set_target_x(1);
		warp->set_target_y(1);
		warp->set_available(true);
	}
	EXPECT_FALSE(ValidateLevelGeometrySnapshots(tooManyTriggers).has_value());

	protocol::Snapshot invalidPieceSize;
	level = invalidPieceSize.add_level_geometry();
	level->set_width(2);
	level->set_height(2);
	level->add_native_piece_layer(297);
	EXPECT_FALSE(ValidateLevelGeometrySnapshots(invalidPieceSize).has_value());

	protocol::Snapshot missingTarget;
	level = missingTarget.add_level_geometry();
	auto *warp = level->add_warp_triggers();
	warp->set_warp_id(1);
	warp->set_available(true);
	warp->set_target_level_id(2);
	EXPECT_FALSE(ValidateLevelGeometrySnapshots(missingTarget).has_value());

	protocol::Snapshot invalidSymbol;
	level = invalidSymbol.add_level_geometry();
	level->set_level_id(1);
	warp = level->add_warp_triggers();
	warp->set_warp_id(1);
	warp->set_available(true);
	warp->set_target_level_id(1);
	warp->set_stable_symbol("bad symbol");
	EXPECT_FALSE(ValidateLevelGeometrySnapshots(invalidSymbol).has_value());

	protocol::Snapshot unknownQuest;
	level = unknownQuest.add_level_geometry();
	level->set_level_id(1);
	level->set_width(10);
	level->set_height(10);
	warp = level->add_warp_triggers();
	warp->set_warp_id(1);
	warp->set_trigger_x(3);
	warp->set_trigger_y(4);
	warp->set_target_level_id(1);
	warp->set_target_x(1);
	warp->set_target_y(1);
	warp->set_required_quest_id(9);
	warp->set_available(true);
	EXPECT_FALSE(ValidateLevelGeometrySnapshots(unknownQuest).has_value());
}

TEST(ServerBackedPlayerSnapshot, AcceptsRawDunObjectLayerIndices)
{
	protocol::Snapshot snapshot;
	auto *level = snapshot.add_level_geometry();
	level->set_level_id(1);
	level->set_width(1);
	level->set_height(1);
	level->add_native_object_layer(109);

	EXPECT_TRUE(ValidateLevelGeometrySnapshots(snapshot).has_value());
}

TEST(ServerBackedPlayerSnapshot, ValidatesGeneratedSetPieceOriginsAgainstProjectedGeometry)
{
	protocol::Snapshot valid;
	auto *level = valid.add_level_geometry();
	level->set_level_id(17);
	level->set_width(8);
	level->set_height(9);
	auto *origin = valid.add_set_level_object_origins();
	origin->set_set_level_id(6);
	origin->set_level_id(17);
	origin->set_position_x(7);
	origin->set_position_y(8);
	EXPECT_TRUE(ValidateSetLevelObjectOriginSnapshots(valid).has_value());

	protocol::Snapshot outside = valid;
	outside.mutable_set_level_object_origins(0)->set_position_x(8);
	EXPECT_FALSE(ValidateSetLevelObjectOriginSnapshots(outside).has_value());

	protocol::Snapshot duplicate = valid;
	duplicate.add_set_level_object_origins()->CopyFrom(*origin);
	EXPECT_FALSE(ValidateSetLevelObjectOriginSnapshots(duplicate).has_value());

	protocol::Snapshot legacy;
	legacy.add_set_level_object_origins()->set_set_level_id(6);
	EXPECT_TRUE(ValidateSetLevelObjectOriginSnapshots(legacy).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsDuplicateMaterializedProceduralLevels)
{
	protocol::Snapshot valid;
	valid.add_materialized_procedural_level_ids(0);
	valid.add_materialized_procedural_level_ids(17);
	EXPECT_TRUE(ValidateMaterializedProceduralLevelSnapshots(valid).has_value());

	protocol::Snapshot duplicate = valid;
	duplicate.add_materialized_procedural_level_ids(17);
	EXPECT_FALSE(ValidateMaterializedProceduralLevelSnapshots(duplicate).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsEntityPositionsOutsideNativeGeometry)
{
	const auto withLevel = [](protocol::Snapshot &snapshot) {
		auto *level = snapshot.add_level_geometry();
		level->set_level_id(1);
		level->set_width(4);
		level->set_height(4);
	};

	protocol::Snapshot playerOutside;
	withLevel(playerOutside);
	auto *player = playerOutside.add_players();
	player->set_level_id(1);
	player->set_position_x(4);
	player->set_position_y(1);
	EXPECT_FALSE(ValidateEntityPositionSnapshots(playerOutside).has_value());

	protocol::Snapshot movingMonsterOutside;
	withLevel(movingMonsterOutside);
	auto *monster = movingMonsterOutside.add_monsters();
	monster->set_level_id(1);
	monster->set_position_x(2);
	monster->set_position_y(2);
	monster->set_moving(true);
	monster->set_future_position_x(4);
	monster->set_future_position_y(2);
	EXPECT_FALSE(ValidateEntityPositionSnapshots(movingMonsterOutside).has_value());

	protocol::Snapshot worldEntitiesOutside;
	withLevel(worldEntitiesOutside);
	worldEntitiesOutside.add_world_items()->set_position_x(-1);
	worldEntitiesOutside.mutable_world_items(0)->set_level_id(1);
	EXPECT_FALSE(ValidateEntityPositionSnapshots(worldEntitiesOutside).has_value());
	worldEntitiesOutside.clear_world_items();
	worldEntitiesOutside.add_objects()->set_position_y(4);
	worldEntitiesOutside.mutable_objects(0)->set_level_id(1);
	EXPECT_FALSE(ValidateEntityPositionSnapshots(worldEntitiesOutside).has_value());
	worldEntitiesOutside.clear_objects();
	worldEntitiesOutside.add_projectiles()->set_position_x(256);
	worldEntitiesOutside.mutable_projectiles(0)->set_level_id(1);
	EXPECT_FALSE(ValidateEntityPositionSnapshots(worldEntitiesOutside).has_value());
	worldEntitiesOutside.clear_projectiles();
	worldEntitiesOutside.add_corpses()->set_position_y(4);
	worldEntitiesOutside.mutable_corpses(0)->set_level_id(1);
	EXPECT_FALSE(ValidateEntityPositionSnapshots(worldEntitiesOutside).has_value());

	protocol::Snapshot portalOutside;
	withLevel(portalOutside);
	auto *portal = portalOutside.add_portals();
	portal->set_source_level_id(1);
	portal->set_source_position_x(1);
	portal->set_source_position_y(1);
	portal->set_destination_level_id(1);
	portal->set_destination_position_x(4);
	portal->set_destination_position_y(1);
	EXPECT_FALSE(ValidateEntityPositionSnapshots(portalOutside).has_value());

	protocol::Snapshot nativeRangeOutside;
	nativeRangeOutside.add_players()->set_position_x(256);
	EXPECT_FALSE(ValidateEntityPositionSnapshots(nativeRangeOutside).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsDuplicateEntityIdsAcrossSnapshotCollections)
{
	protocol::Snapshot duplicateAcrossCollections;
	duplicateAcrossCollections.add_players()->set_entity_id(7);
	duplicateAcrossCollections.add_monsters()->set_entity_id(7);
	EXPECT_FALSE(ValidateEntityIdentitySnapshots(duplicateAcrossCollections).has_value());

	protocol::Snapshot invalidCorpse;
	invalidCorpse.add_corpses()->set_entity_id(0);
	EXPECT_FALSE(ValidateEntityIdentitySnapshots(invalidCorpse).has_value());

	protocol::Snapshot duplicateCorpses;
	duplicateCorpses.add_corpses()->set_entity_id(8);
	duplicateCorpses.add_corpses()->set_entity_id(8);
	EXPECT_FALSE(ValidateEntityIdentitySnapshots(duplicateCorpses).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsMalformedPlayerActions)
{
	protocol::Snapshot invalidTiming;
	auto *player = invalidTiming.add_players();
	player->set_combat_mode(protocol::PLAYER_COMBAT_MODE_ATTACK);
	player->set_combat_mode_end_tick(4);
	auto *action = player->mutable_current_action();
	action->set_kind(protocol::PLAYER_ACTION_KIND_ATTACK);
	action->set_started_at_tick(3);
	action->set_effect_tick(2);
	action->set_ready_tick(4);
	action->set_end_tick(4);
	action->set_target_entity_id(9);
	EXPECT_FALSE(ValidatePlayerActionSnapshots(invalidTiming).has_value());

	protocol::Snapshot blockedMove;
	player = blockedMove.add_players();
	player->set_level_id(1);
	player->set_position_x(0);
	player->set_position_y(0);
	player->set_combat_mode(protocol::PLAYER_COMBAT_MODE_WALK_SIDEWAYS);
	player->set_combat_mode_end_tick(4);
	action = player->mutable_current_action();
	action->set_kind(protocol::PLAYER_ACTION_KIND_MOVE);
	action->set_started_at_tick(1);
	action->set_effect_tick(2);
	action->set_ready_tick(3);
	action->set_end_tick(4);
	action->set_direction_x(1);
	auto *level = blockedMove.add_level_geometry();
	level->set_level_id(1);
	level->set_width(2);
	level->set_height(2);
	level->add_blocked_cells(1);
	EXPECT_FALSE(ValidatePlayerActionSnapshots(blockedMove).has_value());

	protocol::Snapshot invalidCast;
	player = invalidCast.add_players();
	player->set_combat_mode(protocol::PLAYER_COMBAT_MODE_SPELL);
	player->set_combat_mode_end_tick(4);
	action = player->mutable_current_action();
	action->set_kind(protocol::PLAYER_ACTION_KIND_CAST);
	action->set_started_at_tick(1);
	action->set_effect_tick(2);
	action->set_ready_tick(3);
	action->set_end_tick(4);
	action->set_target_entity_id(9);
	action->mutable_cast()->set_spell_id(1);
	action->mutable_cast()->set_target_entity_id(10);
	EXPECT_FALSE(ValidatePlayerActionSnapshots(invalidCast).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsMalformedMonsterCombatMetadata)
{
	const auto makeMonster = []() {
		protocol::Snapshot snapshot;
		auto *monster = snapshot.add_monsters();
		monster->set_entity_id(1);
		monster->set_hit_points(10);
		monster->set_max_hit_points(10);
		monster->set_alive(true);
		return snapshot;
	};

	protocol::Snapshot invalidAlive = makeMonster();
	invalidAlive.mutable_monsters(0)->set_alive(false);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidAlive).has_value());

	protocol::Snapshot invalidHitPoints = makeMonster();
	invalidHitPoints.mutable_monsters(0)->set_hit_points(0);
	invalidHitPoints.mutable_monsters(0)->set_alive(true);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidHitPoints).has_value());

	protocol::Snapshot invalidDamage = makeMonster();
	invalidDamage.mutable_monsters(0)->set_max_damage(1);
	invalidDamage.mutable_monsters(0)->set_min_damage(2);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidDamage).has_value());

	protocol::Snapshot invalidResistance = makeMonster();
	invalidResistance.mutable_monsters(0)->set_fire_resistance(101);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidResistance).has_value());

	protocol::Snapshot invalidDeath = makeMonster();
	invalidDeath.mutable_monsters(0)->set_death_animation_ticks(2);
	invalidDeath.mutable_monsters(0)->set_death_ticks_remaining(3);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidDeath).has_value());

	protocol::Snapshot invalidOwner = makeMonster();
	invalidOwner.mutable_monsters(0)->set_owner_player_id(4);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidOwner).has_value());

	protocol::Snapshot invalidDialogue = makeMonster();
	invalidDialogue.mutable_monsters(0)->set_dialogue_message_id(1);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidDialogue).has_value());
	protocol::Snapshot invalidDialogueState = makeMonster();
	invalidDialogueState.mutable_monsters(0)->set_dialogue_state(static_cast<protocol::MonsterDialogueState>(3));
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidDialogueState).has_value());

	protocol::Snapshot invalidGroupRelation = makeMonster();
	invalidGroupRelation.mutable_monsters(0)->set_leader_relation(3);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidGroupRelation).has_value());

	protocol::Snapshot invalidGroupLeader = makeMonster();
	invalidGroupLeader.mutable_monsters(0)->set_leader_relation(1);
	invalidGroupLeader.mutable_monsters(0)->set_leader_entity_id(99);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidGroupLeader).has_value());

	protocol::Snapshot invalidGroupSize = makeMonster();
	invalidGroupSize.mutable_monsters(0)->set_pack_size(256);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidGroupSize).has_value());

	protocol::Snapshot invalidGroupActiveTicks = makeMonster();
	invalidGroupActiveTicks.mutable_monsters(0)->set_active_for_ticks(-1);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidGroupActiveTicks).has_value());

	protocol::Snapshot invalidBatMovementMode = makeMonster();
	invalidBatMovementMode.mutable_monsters(0)->set_bat_movement_mode(MaximumNativeMonsterMovementMode + 1);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidBatMovementMode).has_value());
	protocol::Snapshot invalidRangedAvoidanceTurn = makeMonster();
	invalidRangedAvoidanceTurn.mutable_monsters(0)->set_ranged_avoidance_turn(MaximumNativeMonsterMovementTurn + 1);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidRangedAvoidanceTurn).has_value());
	protocol::Snapshot invalidRangedAvoidanceMode = makeMonster();
	invalidRangedAvoidanceMode.mutable_monsters(0)->set_ranged_avoidance_movement_mode(MinimumNativeMonsterMovementMode - 1);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidRangedAvoidanceMode).has_value());
	protocol::Snapshot invalidRhinoTurn = makeMonster();
	invalidRhinoTurn.mutable_monsters(0)->set_rhino_movement_turn(MinimumNativeMonsterMovementTurn - 1);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidRhinoTurn).has_value());
	protocol::Snapshot invalidAvoidanceTurn = makeMonster();
	invalidAvoidanceTurn.mutable_monsters(0)->set_avoidance_movement_turn(MaximumNativeMonsterMovementTurn + 1);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidAvoidanceTurn).has_value());
	protocol::Snapshot invalidAvoidanceMode = makeMonster();
	invalidAvoidanceMode.mutable_monsters(0)->set_avoidance_movement_mode(MaximumNativeMonsterMovementMode + 1);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidAvoidanceMode).has_value());
	protocol::Snapshot invalidSkeletonRangedMode = makeMonster();
	invalidSkeletonRangedMode.mutable_monsters(0)->set_skeleton_ranged_movement_mode(MinimumNativeMonsterMovementMode - 1);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidSkeletonRangedMode).has_value());
	protocol::Snapshot invalidFatMode = makeMonster();
	invalidFatMode.mutable_monsters(0)->set_fat_movement_mode(MaximumNativeMonsterMovementMode + 1);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidFatMode).has_value());
	protocol::Snapshot invalidRhinoMode = makeMonster();
	invalidRhinoMode.mutable_monsters(0)->set_rhino_movement_mode(MinimumNativeMonsterMovementMode - 1);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidRhinoMode).has_value());

	protocol::Snapshot invalidMovementTiming = makeMonster();
	invalidMovementTiming.mutable_monsters(0)->set_movement_started_tick(12);
	invalidMovementTiming.mutable_monsters(0)->set_movement_effect_tick(11);
	EXPECT_FALSE(ValidateMonsterSnapshots(invalidMovementTiming).has_value());

	protocol::Snapshot stationaryMovementTiming = makeMonster();
	stationaryMovementTiming.mutable_monsters(0)->set_movement_effect_tick(13);
	EXPECT_FALSE(ValidateMonsterSnapshots(stationaryMovementTiming).has_value());
}

TEST(ServerBackedPlayerSnapshot, AcceptsAuthoredUniqueMonsterPackLeader)
{
	protocol::Snapshot snapshot;
	auto *leader = snapshot.add_monsters();
	leader->set_entity_id(100);
	leader->set_level_id(1);
	leader->set_position_x(10);
	leader->set_position_y(10);
	leader->set_hit_points(10);
	leader->set_max_hit_points(10);
	leader->set_alive(true);
	leader->set_is_unique(true);
	leader->set_pack_size(2);
	auto *member = snapshot.add_monsters();
	member->set_entity_id(0x10000001);
	member->set_level_id(1);
	member->set_position_x(11);
	member->set_position_y(10);
	member->set_hit_points(10);
	member->set_max_hit_points(10);
	member->set_alive(true);
	member->set_leader_entity_id(100);
	member->set_leader_relation(1);

	EXPECT_TRUE(ValidateMonsterSnapshots(snapshot).has_value());
	auto projected = ProjectMonsterSnapshots(snapshot);
	ASSERT_TRUE(projected.has_value()) << projected.error();
	ASSERT_EQ(projected->size(), 2U);
	EXPECT_TRUE((*projected)[0].isUnique);
	EXPECT_EQ((*projected)[1].leaderEntityId, 100U);
}

TEST(ServerBackedPlayerSnapshot, RejectsMalformedProjectileMetadata)
{
	const auto makeProjectile = []() {
		protocol::Snapshot snapshot;
		auto *projectile = snapshot.add_projectiles();
		projectile->set_entity_id(1);
		projectile->set_source_entity_id(2);
		projectile->set_spell_id(3);
		projectile->set_remaining_ticks(1);
		return snapshot;
	};

	protocol::Snapshot invalidDamage = makeProjectile();
	invalidDamage.mutable_projectiles(0)->set_damage_minimum(4);
	invalidDamage.mutable_projectiles(0)->set_damage_maximum(3);
	EXPECT_FALSE(ValidateProjectileSnapshots(invalidDamage).has_value());

	protocol::Snapshot invalidLifecycle = makeProjectile();
	invalidLifecycle.mutable_projectiles(0)->set_behavior(MaximumProjectileBehavior + 1);
	EXPECT_FALSE(ValidateProjectileSnapshots(invalidLifecycle).has_value());

	protocol::Snapshot lightningWallPresentation = makeProjectile();
	lightningWallPresentation.mutable_projectiles(0)->set_native_visual(protocol::NATIVE_PROJECTILE_VISUAL_LIGHTNING_WALL);
	EXPECT_TRUE(ValidateProjectileSnapshots(lightningWallPresentation).has_value());

	protocol::Snapshot invalidDirection = makeProjectile();
	invalidDirection.mutable_projectiles(0)->set_behavior(2); // Firebolt
	invalidDirection.mutable_projectiles(0)->set_original_direction(8);
	EXPECT_FALSE(ValidateProjectileSnapshots(invalidDirection).has_value());

	protocol::Snapshot invalidPresentation = makeProjectile();
	invalidPresentation.mutable_projectiles(0)->set_native_visual(static_cast<protocol::NativeProjectileVisual>(36));
	EXPECT_FALSE(ValidateProjectileSnapshots(invalidPresentation).has_value());

	protocol::Snapshot invalidNativeIdentity = makeProjectile();
	invalidNativeIdentity.mutable_projectiles(0)->set_behavior(50); // MonsterMagic
	invalidNativeIdentity.mutable_projectiles(0)->set_native_missile_id("-invalid");
	EXPECT_FALSE(ValidateProjectileSnapshots(invalidNativeIdentity).has_value());

	protocol::Snapshot oversizedNativeIdentity = makeProjectile();
	oversizedNativeIdentity.mutable_projectiles(0)->set_behavior(50); // MonsterMagic
	oversizedNativeIdentity.mutable_projectiles(0)->set_native_missile_id(std::string(129, 'x'));
	EXPECT_FALSE(ValidateProjectileSnapshots(oversizedNativeIdentity).has_value());

	protocol::Snapshot maximumNativeIdentity = makeProjectile();
	maximumNativeIdentity.mutable_projectiles(0)->set_behavior(50); // MonsterMagic
	maximumNativeIdentity.mutable_projectiles(0)->set_native_missile_id(
	    std::string(MaximumIdentityBytes, 'x'));
	EXPECT_TRUE(ValidateProjectileSnapshots(maximumNativeIdentity).has_value());

	protocol::Snapshot misplacedNativeIdentity = makeProjectile();
	misplacedNativeIdentity.mutable_projectiles(0)->set_behavior(49);
	misplacedNativeIdentity.mutable_projectiles(0)->set_native_missile_id("Lightning");
	EXPECT_FALSE(ValidateProjectileSnapshots(misplacedNativeIdentity).has_value());

	protocol::Snapshot zeroSpellArrow = makeProjectile();
	zeroSpellArrow.mutable_projectiles(0)->set_spell_id(0);
	zeroSpellArrow.mutable_projectiles(0)->set_behavior(58);
	EXPECT_TRUE(ValidateProjectileSnapshots(zeroSpellArrow).has_value());
}

TEST(ServerBackedPlayerSnapshot, ProjectsAndSortsAuthoritativeCorpses)
{
	protocol::Snapshot snapshot;
	auto *second = snapshot.add_corpses();
	second->set_entity_id(20);
	second->set_level_id(3);
	second->set_position_x(8);
	second->set_position_y(9);
	auto *first = snapshot.add_corpses();
	first->set_entity_id(10);
	first->set_level_id(2);
	first->set_position_x(4);
	first->set_position_y(5);

	auto projected = ProjectCorpseSnapshots(snapshot);
	ASSERT_TRUE(projected.has_value()) << projected.error();
	ASSERT_EQ(projected->size(), 2U);
	EXPECT_EQ(projected->front().entityId, 10U);
	EXPECT_EQ(projected->front().levelId, 2U);
	EXPECT_EQ(projected->back().entityId, 20U);
	EXPECT_EQ(projected->back().positionY, 9);
}

TEST(ServerBackedPlayerSnapshot, ProjectsSessionMetadataWithoutLocalSimulation)
{
	protocol::Snapshot snapshot;
	snapshot.set_random_state(0x12345678);
	snapshot.set_victory_triggered(true);
	snapshot.set_difficulty(protocol::GAME_DIFFICULTY_HELL);
	snapshot.add_active_world_mutation_ids(20);
	snapshot.add_active_world_mutation_ids(10);

	auto projected = ProjectSessionMetadata(snapshot);
	ASSERT_TRUE(projected.has_value()) << projected.error();
	EXPECT_EQ(projected->randomState, 0x12345678U);
	EXPECT_TRUE(projected->victoryTriggered);
	EXPECT_EQ(projected->difficulty, ProjectedGameDifficulty::Hell);
	ASSERT_EQ(projected->activeWorldMutationIds.size(), 2U);
	EXPECT_EQ(projected->activeWorldMutationIds[0], 10U);
	EXPECT_EQ(projected->activeWorldMutationIds[1], 20U);
}

TEST(ServerBackedPlayerSnapshot, RejectsInvalidSessionMetadata)
{
	protocol::Snapshot invalidDifficulty;
	invalidDifficulty.set_difficulty(static_cast<protocol::GameDifficulty>(99));
	EXPECT_FALSE(ProjectSessionMetadata(invalidDifficulty).has_value());

	protocol::Snapshot invalidMutation;
	invalidMutation.add_active_world_mutation_ids(0);
	EXPECT_FALSE(ProjectSessionMetadata(invalidMutation).has_value());

	protocol::Snapshot duplicateMutation;
	duplicateMutation.add_active_world_mutation_ids(7);
	duplicateMutation.add_active_world_mutation_ids(7);
	EXPECT_FALSE(ProjectSessionMetadata(duplicateMutation).has_value());

	protocol::Snapshot tooManyMutations;
	for (uint32_t mutationId = 1; mutationId <= 4097; mutationId++)
		tooManyMutations.add_active_world_mutation_ids(mutationId);
	EXPECT_FALSE(ProjectSessionMetadata(tooManyMutations).has_value());
}

TEST(ServerBackedPlayerSnapshot, ProjectsAndSortsAuthoritativeLevelGeometry)
{
	protocol::Snapshot snapshot;
	auto *second = snapshot.add_level_geometry();
	second->set_level_id(20);
	second->set_width(2);
	second->set_height(2);
	second->add_blocked_cells(3);
	second->add_missile_blocked_cells(1);
	second->add_native_transparency(0x12);
	second->add_native_transparency(0x56);
	second->add_native_transparency(0x7A);
	second->add_native_transparency(0x7F);
	second->add_native_object_layer(5);
	second->add_native_object_layer(6);
	second->add_native_object_layer(7);
	second->add_native_object_layer(8);
	second->add_native_mega_tile_layer(42);
	second->add_native_piece_layer(297);
	second->add_native_piece_layer(300);
	second->add_native_piece_layer(299);
	second->add_native_piece_layer(298);
	auto *warp = second->add_warp_triggers();
	warp->set_warp_id(200);
	warp->set_trigger_x(1);
	warp->set_trigger_y(1);
	warp->set_target_level_id(10);
	warp->set_target_x(0);
	warp->set_target_y(0);
	warp->set_stable_symbol("stairs-down");
	warp->set_available(true);
	auto *first = snapshot.add_level_geometry();
	first->set_level_id(10);
	first->set_width(2);
	first->set_height(2);

	auto projected = ProjectLevelGeometrySnapshots(snapshot);
	ASSERT_TRUE(projected.has_value()) << projected.error();
	ASSERT_EQ(projected->size(), 2U);
	EXPECT_EQ(projected->front().levelId, 10U);
	EXPECT_EQ(projected->back().levelId, 20U);
	ASSERT_EQ(projected->back().warpTriggers.size(), 1U);
	EXPECT_EQ(projected->back().warpTriggers.front().stableSymbol, "stairs-down");
	EXPECT_EQ(projected->back().nativeTransparency[2], 0x7AU);
	EXPECT_EQ(projected->back().nativeMegaTileLayer.front(), 42U);
	EXPECT_EQ(projected->back().nativePieceLayer[0], 297U);
	EXPECT_EQ(projected->back().nativePieceLayer[3], 298U);
}

TEST(ServerBackedPlayerSnapshot, WorldProjectionRetainsEntitiesFromAnotherLevel)
{
	ServerBackedWorldProjection projection;
	ProjectedMonsterSnapshot monster;
	monster.entityId = 7;
	monster.levelId = 2;

	ASSERT_TRUE(projection.Apply({ monster }, { }, { }, 1).has_value());
	ASSERT_EQ(projection.Monsters().size(), 1U);
	EXPECT_EQ(projection.Monsters()[0].levelId, 2U);
	EXPECT_TRUE(projection.Projectiles().empty());
}

TEST(ServerBackedPlayerSnapshot, WorldProjectionUsesAuthoritativeGeometryQueries)
{
	EXPECT_TRUE(IsAuthoritativeLevelVisible(0, 1));
	EXPECT_TRUE(IsAuthoritativeLevelVisible(1, 1));
	EXPECT_FALSE(IsAuthoritativeLevelVisible(2, 1));

	ProjectedLevelGeometrySnapshot geometry;
	geometry.levelId = 1;
	geometry.width = 3;
	geometry.height = 2;
	geometry.blockedCells = { 4 };
	geometry.missileBlockedCells = { 1 };

	ServerBackedWorldProjection projection;
	ASSERT_TRUE(projection.Apply({ }, { }, { }, { }, { geometry }, 1).has_value());
	ASSERT_TRUE(projection.CurrentLevelGeometry() != nullptr);
	EXPECT_EQ(projection.LevelGeometryAt(1), projection.CurrentLevelGeometry());
	ASSERT_TRUE(projection.IsBlocked(1, 1).has_value());
	EXPECT_TRUE(*projection.IsBlocked(1, 1));
	EXPECT_FALSE(*projection.IsBlocked(0, 0));
	EXPECT_TRUE(*projection.IsMissileBlocked(1, 0));
	EXPECT_FALSE(projection.IsBlocked(3, 0).has_value());
}

TEST(ServerBackedPlayerSnapshot, FiltersDeadAndHiddenMonstersFromNativePresentation)
{
	ProjectedMonsterSnapshot snapshot;
	snapshot.alive = true;
	EXPECT_TRUE(IsAuthoritativeMonsterPresentable(snapshot));

	snapshot.alive = false;
	EXPECT_FALSE(IsAuthoritativeMonsterPresentable(snapshot));

	snapshot.alive = true;
	snapshot.sneakHidden = true;
	EXPECT_FALSE(IsAuthoritativeMonsterPresentable(snapshot));
}

TEST(ServerBackedPlayerSnapshot, AppliesAuthoritativeNativeGeometryLayers)
{
	ProjectedLevelGeometrySnapshot geometry;
	geometry.levelId = 1;
	geometry.width = 4;
	geometry.height = 4;
	geometry.nativeTransparency.assign(16, 0);
	geometry.nativeTransparency[5] = 9;
	geometry.nativeTransparency[6] = 128;
	geometry.nativePieceLayer.assign(16, 42);
	geometry.nativePieceLayer[5] = 77;
	geometry.nativeMegaTileLayer = { 12, 13, 14, 15 };

	const auto previousPiece = tileAt(1, 1).piece();
	const auto previousTransparency = tileAt(1, 1).transVal();
	const auto previousMegaTile = megaTileAt(0, 0).current();
	struct RestoreNativeGeometry {
		uint16_t piece;
		int8_t transparency;
		uint8_t megaTile;
		~RestoreNativeGeometry()
		{
			tileAt(1, 1).setPiece(piece);
			tileAt(1, 1).setTransVal(transparency);
			megaTileAt(0, 0).setCurrent(megaTile);
		}
	} restore { previousPiece, previousTransparency, previousMegaTile };

	ServerBackedWorldProjection projection;
	ASSERT_TRUE(projection.Apply({ }, { }, { }, { }, { geometry }, 1).has_value());
	ServerBackedWorldPresentation presentation;
	ASSERT_TRUE(presentation.Apply(projection, 1).has_value());
	EXPECT_EQ(tileAt(1, 1).piece(), 77);
	EXPECT_EQ(tileAt(1, 1).transVal(), 9);
	EXPECT_EQ(tileAt(2, 1).transVal(), -128);
	EXPECT_EQ(megaTileAt(0, 0).current(), 12);

	ProjectedLevelGeometrySnapshot townGeometry;
	townGeometry.levelId = 0;
	townGeometry.width = MAXDUNX;
	townGeometry.height = MAXDUNY;
	ServerBackedWorldProjection townProjection;
	ASSERT_TRUE(townProjection.Apply({ }, { }, { }, { }, { townGeometry }, 0).has_value());
	EXPECT_TRUE(presentation.Apply(townProjection, 0).has_value());

	ProjectedLevelGeometrySnapshot invalid = geometry;
	invalid.nativeMegaTileLayer[0] = 256;
	ServerBackedWorldProjection invalidProjection;
	ASSERT_TRUE(invalidProjection.Apply({ }, { }, { }, { }, { invalid }, 1).has_value());
	const auto result = presentation.Apply(invalidProjection, 1);
	EXPECT_FALSE(result.has_value());

	invalid = geometry;
	invalid.nativeObjectLayer.assign(16, 0);
	invalid.nativeObjectLayer[0] = static_cast<uint16_t>(DUN_OBJECT_ID_LAST) + 1;
	ASSERT_TRUE(invalidProjection.Apply({ }, { }, { }, { }, { invalid }, 1).has_value());
	EXPECT_FALSE(presentation.Apply(invalidProjection, 1).has_value());
}

TEST(ServerBackedPlayerSnapshot, WorldProjectionRejectsDuplicateLevelGeometry)
{
	ProjectedLevelGeometrySnapshot first;
	first.levelId = 1;
	ProjectedLevelGeometrySnapshot duplicate = first;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ }, { }, { }, { }, { first, duplicate }, 1).has_value());
}

TEST(ServerBackedPlayerSnapshot, WorldProjectionRejectsEntitiesOutsideProjectedGeometry)
{
	ProjectedLevelGeometrySnapshot geometry;
	geometry.levelId = 1;
	geometry.width = 2;
	geometry.height = 2;

	ProjectedMonsterSnapshot monster;
	monster.entityId = 11;
	monster.levelId = 1;
	monster.positionX = 2;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ monster }, { }, { }, { }, { geometry }, 1).has_value());

	ProjectedMonsterSnapshot movingMonster = monster;
	movingMonster.entityId = 12;
	movingMonster.positionX = 1;
	movingMonster.futurePositionX = 2;
	movingMonster.moving = true;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ movingMonster }, { }, { }, { }, { geometry }, 1).has_value());

	ProjectedWorldItemSnapshot item;
	item.entityId = 13;
	item.levelId = 1;
	item.positionY = -1;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ }, { item }, { }, { }, { geometry }, 1).has_value());

	ProjectedObjectSnapshot object;
	object.entityId = 14;
	object.levelId = 1;
	object.positionX = 2;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ }, { }, { object }, { }, { geometry }, 1).has_value());

	ProjectedProjectileSnapshot projectile;
	projectile.entityId = 15;
	projectile.sourceEntityId = 1;
	projectile.spellId = 4;
	projectile.levelId = 1;
	projectile.positionY = 2;
	projectile.remainingTicks = 1;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ }, { }, { }, { projectile }, { geometry }, 1).has_value());

	ProjectedWorldItemSnapshot invalidNativeCoordinate;
	invalidNativeCoordinate.entityId = 16;
	invalidNativeCoordinate.positionX = -1;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ }, { invalidNativeCoordinate }, { }, 1).has_value());
	invalidNativeCoordinate.entityId = 17;
	invalidNativeCoordinate.positionX = 256;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ }, { invalidNativeCoordinate }, { }, 1).has_value());
}

TEST(ServerBackedPlayerSnapshot, WorldProjectionRejectsInvalidOrDuplicateMonsters)
{
	ProjectedMonsterSnapshot invalid;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ invalid }, { }, { }, 1).has_value());

	ProjectedMonsterSnapshot first;
	first.entityId = 11;
	ProjectedMonsterSnapshot duplicate = first;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ first, duplicate }, { }, { }, 1).has_value());

	ProjectedMonsterSnapshot invalidDirection = first;
	invalidDirection.entityId = 12;
	invalidDirection.direction = 8;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ invalidDirection }, { }, { }, 1).has_value());

	ProjectedMonsterSnapshot nonAdjacent = first;
	nonAdjacent.entityId = 13;
	nonAdjacent.moving = true;
	nonAdjacent.futurePositionX = 2;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ nonAdjacent }, { }, { }, 1).has_value());

	ProjectedMonsterSnapshot invalidTiming = first;
	invalidTiming.entityId = 20;
	invalidTiming.movementStartedTick = 12;
	invalidTiming.movementEffectTick = 11;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ invalidTiming }, { }, { }, 1).has_value());

	ProjectedMonsterSnapshot stationaryTiming = first;
	stationaryTiming.entityId = 21;
	stationaryTiming.movementEffectTick = 13;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ stationaryTiming }, { }, { }, 1).has_value());

	ProjectedWorldItemSnapshot item;
	item.entityId = first.entityId;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ first }, { item }, { }, 1).has_value());

	ProjectedMonsterSnapshot invalidGroupRelation = first;
	invalidGroupRelation.entityId = 14;
	invalidGroupRelation.leaderRelation = 3;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ invalidGroupRelation }, { }, { }, 1).has_value());

	ProjectedMonsterSnapshot invalidGroupLeader = first;
	invalidGroupLeader.entityId = 15;
	invalidGroupLeader.leaderRelation = 1;
	invalidGroupLeader.leaderEntityId = 99;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ invalidGroupLeader }, { }, { }, 1).has_value());

	ProjectedMonsterSnapshot leader = first;
	leader.entityId = 16;
	leader.levelId = 1;
	leader.alive = true;
	ProjectedMonsterSnapshot member = first;
	member.entityId = 17;
	member.levelId = 1;
	member.leaderEntityId = leader.entityId;
	member.leaderRelation = 1;
	EXPECT_TRUE(ServerBackedWorldProjection {}.Apply({ member, leader }, { }, { }, 1).has_value());

	ProjectedMonsterSnapshot invalidUniqueMember = member;
	invalidUniqueMember.entityId = 18;
	invalidUniqueMember.isUnique = true;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ invalidUniqueMember, leader }, { }, { }, 1).has_value());

	ProjectedMonsterSnapshot invalidPackSize = first;
	invalidPackSize.entityId = 19;
	invalidPackSize.packSize = 256;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ invalidPackSize }, { }, { }, 1).has_value());
}

TEST(ServerBackedPlayerSnapshot, WorldProjectionRetainsSortedAuthoritativeProjectiles)
{
	ProjectedProjectileSnapshot second;
	second.entityId = 12;
	second.sourceEntityId = 1;
	second.spellId = 4;
	second.remainingTicks = 2;
	second.nativeVisual = 2;
	ProjectedProjectileSnapshot first;
	first.entityId = 11;
	first.sourceEntityId = 1;
	first.spellId = 4;
	first.remainingTicks = 1;
	first.behavior = 7;
	first.nativeVisual = 6;

	ServerBackedWorldProjection projection;
	ASSERT_TRUE(projection.Apply({ }, { }, { }, { second, first }, 1).has_value());
	ASSERT_EQ(projection.Projectiles().size(), 2U);
	EXPECT_EQ(projection.Projectiles()[0].entityId, 11U);
	EXPECT_EQ(projection.Projectiles()[0].behavior, 7);
	EXPECT_EQ(projection.Projectiles()[0].nativeVisual, 6);
	EXPECT_EQ(projection.Projectiles()[1].entityId, 12U);
	EXPECT_EQ(projection.Projectiles()[1].nativeVisual, 2);
}

TEST(ServerBackedPlayerSnapshot, WorldPresentationIgnoresOlderAuthoritativeProjection)
{
	LoadMissileData();
	Missiles.clear();

	ProjectedProjectileSnapshot projectile;
	projectile.entityId = 101;
	projectile.sourceEntityId = 1;
	projectile.spellId = 4;
	projectile.levelId = 1;
	projectile.positionX = 4;
	projectile.positionY = 4;
	projectile.remainingTicks = 5;
	projectile.behavior = 2; // Firebolt

	ServerBackedWorldProjection newer;
	ASSERT_TRUE(newer.Apply({ }, { }, { }, { projectile }, { }, 1, 10).has_value());
	EXPECT_EQ(newer.ServerTick(), std::optional<uint64_t> { 10 });
	ServerBackedWorldPresentation presentation;
	ASSERT_TRUE(presentation.Apply(newer, 1).has_value());
	ASSERT_EQ(Missiles.size(), 1U);

	projectile.positionX = 5;
	ServerBackedWorldProjection older;
	ASSERT_TRUE(older.Apply({ }, { }, { }, { projectile }, { }, 1, 9).has_value());
	ASSERT_TRUE(presentation.Apply(older, 1).has_value());
	EXPECT_EQ(Missiles.front().position.tile, (WorldTilePosition { 4, 4 }));

	ServerBackedWorldProjection equal;
	ASSERT_TRUE(equal.Apply({ }, { }, { }, { projectile }, { }, 1, 10).has_value());
	ASSERT_TRUE(presentation.Apply(equal, 1).has_value());
	EXPECT_EQ(Missiles.front().position.tile, (WorldTilePosition { 5, 4 }));

	presentation.Clear();
	Missiles.clear();
}

TEST(ServerBackedPlayerSnapshot, WorldProjectionRejectsInvalidOrDuplicateProjectiles)
{
	ProjectedProjectileSnapshot invalid;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ }, { }, { }, { invalid }, 1).has_value());

	ProjectedProjectileSnapshot first;
	first.entityId = 11;
	first.sourceEntityId = 1;
	first.spellId = 4;
	first.remainingTicks = 1;
	ProjectedProjectileSnapshot duplicate = first;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ }, { }, { }, { first, duplicate }, 1).has_value());

	ProjectedProjectileSnapshot invalidBehavior = first;
	invalidBehavior.entityId = 12;
	invalidBehavior.behavior = 60;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ }, { }, { }, { invalidBehavior }, 1).has_value());

	ProjectedProjectileSnapshot invalidDirection = first;
	invalidDirection.entityId = 13;
	invalidDirection.behavior = 2; // Firebolt
	invalidDirection.originalDirection = 8;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ }, { }, { }, { invalidDirection }, 1).has_value());

	ProjectedProjectileSnapshot invalidNativeMissile = first;
	invalidNativeMissile.entityId = 14;
	invalidNativeMissile.behavior = 50; // MonsterMagic
	invalidNativeMissile.nativeMissileId = "Invalid?";
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ }, { }, { }, { invalidNativeMissile }, 1).has_value());

	ProjectedProjectileSnapshot misplacedNativeMissile = first;
	misplacedNativeMissile.entityId = 15;
	misplacedNativeMissile.behavior = 2; // Firebolt
	misplacedNativeMissile.nativeMissileId = "Lightning";
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ }, { }, { }, { misplacedNativeMissile }, 1).has_value());

	ProjectedProjectileSnapshot invalidDamageType = first;
	invalidDamageType.entityId = 16;
	invalidDamageType.damageType = 5;
	EXPECT_FALSE(ServerBackedWorldProjection {}.Apply({ }, { }, { }, { invalidDamageType }, 1).has_value());

	ProjectedProjectileSnapshot arrow = first;
	arrow.entityId = 17;
	arrow.spellId = 0;
	arrow.behavior = 58; // WeaponArrow
	EXPECT_TRUE(ServerBackedWorldProjection {}.Apply({ }, { }, { }, { arrow }, 1).has_value());
	arrow.entityId = 18;
	arrow.behavior = 59; // MonsterArrow
	EXPECT_TRUE(ServerBackedWorldProjection {}.Apply({ }, { }, { }, { arrow }, 1).has_value());
}

TEST(ServerBackedPlayerSnapshot, PresentsAuthoritativeProjectileWithoutLocalSimulation)
{
	LoadMissileData();
	Missiles.clear();

	ProjectedProjectileSnapshot projectile;
	projectile.entityId = 101;
	projectile.sourceEntityId = 1;
	projectile.spellId = 4;
	projectile.levelId = 1;
	projectile.positionX = 4;
	projectile.positionY = 4;
	projectile.remainingTicks = 5;
	projectile.behavior = 2; // Firebolt
	projectile.nativeVisual = 25; // ChargedBolt; server metadata overrides behavior fallback.
	projectile.originalDirection = 3;

	ServerBackedWorldProjection projection;
	ASSERT_TRUE(projection.Apply({ }, { }, { }, { projectile }, 1).has_value());
	ServerBackedWorldPresentation presentation;
	ASSERT_TRUE(presentation.Apply(projection, 1).has_value());

	ASSERT_EQ(Missiles.size(), 1U);
	const Missile &missile = Missiles.front();
	EXPECT_EQ(missile._miAuthoritativeEntityId, 101U);
	EXPECT_EQ(missile._mitype, MissileID::ChargedBolt);
	const WorldTilePosition expectedPosition { 4, 4 };
	EXPECT_EQ(missile.position.tile, expectedPosition);
	EXPECT_EQ(missile.duration, 5);
	EXPECT_EQ(missile.position.velocity, Displacement {});
	EXPECT_EQ(missile.position.traveled, Displacement {});

	projectile.positionX = 5;
	projectile.positionY = 6;
	projectile.remainingTicks = 3;
	ASSERT_TRUE(projection.Apply({ }, { }, { }, { projectile }, 1).has_value());
	ASSERT_TRUE(presentation.Apply(projection, 1).has_value());
	ASSERT_EQ(Missiles.size(), 1U);
	const WorldTilePosition updatedPosition { 5, 6 };
	EXPECT_EQ(Missiles.front().position.tile, updatedPosition);
	EXPECT_EQ(Missiles.front().duration, 3);

	ASSERT_TRUE(projection.Apply({ }, { }, { }, { }, 1).has_value());
	ASSERT_TRUE(presentation.Apply(projection, 1).has_value());
	EXPECT_TRUE(Missiles.empty());

	presentation.Clear();
	Missiles.clear();
}

TEST(ServerBackedPlayerSnapshot, AdvancesAuthoritativeMissileAnimationWithoutSimulation)
{
	LoadMissileData();
	Missiles.clear();

	ProjectedProjectileSnapshot projectile;
	projectile.entityId = 102;
	projectile.sourceEntityId = 1;
	projectile.spellId = 1;
	projectile.levelId = 1;
	projectile.positionX = 4;
	projectile.positionY = 4;
	projectile.remainingTicks = 5;
	projectile.behavior = 2; // Firebolt

	ServerBackedWorldProjection projection;
	ASSERT_TRUE(projection.Apply({ }, { }, { }, { projectile }, 1).has_value());
	ServerBackedWorldPresentation presentation;
	ASSERT_TRUE(presentation.Apply(projection, 1).has_value());
	ASSERT_EQ(Missiles.size(), 1U);
	ASSERT_NE(Missiles.front()._miAnimFlags, MissileGraphicsFlags::NotAnimated);

	const int initialFrame = Missiles.front()._miAnimFrame;
	const int initialDuration = Missiles.front().duration;
	const int initialAnimationCount = Missiles.front()._miAnimCnt;
	const std::size_t advanced = AdvanceAuthoritativePresentationMissileAnimations();
	EXPECT_EQ(advanced, 1U);
	EXPECT_EQ(Missiles.front().duration, initialDuration);
	EXPECT_TRUE(Missiles.front()._miAnimFrame != initialFrame
	    || Missiles.front()._miAnimCnt != initialAnimationCount);

	presentation.Clear();
	Missiles.clear();
}

TEST(ServerBackedPlayerSnapshot, RetainsRemotePlayersAsPresentationOnlyState)
{
	LoadPlayerDataFiles();
	LoadSpellData();
	ProjectedPlayerSnapshot local;
	local.entityId = 1;
	local.levelId = 1;
	local.characterClassId = 0;
	ProjectedPlayerSnapshot remote = local;
	remote.entityId = 2;
	remote.levelId = 2;
	remote.positionX = 4;
	remote.positionY = 5;
	remote.gold = 1234;
	remote.experience = 5678;
	remote.life = 640;
	remote.lifeMaximum = 1000;
	remote.lifeMaximumBase = 800;
	remote.mana = 320;
	remote.manaMaximum = 500;
	remote.manaMaximumBase = 400;
	remote.characterLevel = 7;
	remote.diabloKillLevel = 2;
	remote.reflectCharges = 17;
	remote.visitedLevelIds = { 4 };
	remote.statPoints = 9;
	remote.characterName = "Remote";
	remote.strength = { 12, 14 };
	remote.inventoryGrid.assign(InventoryGridCells, -1);
	remote.combatMode = PM_BLOCK;
	Item staff;
	staff._itype = ItemType::Staff;
	staff._iClass = ICLASS_WEAPON;
	staff._iSpell = SpellID::Firebolt;
	staff._iCharges = 5;
	staff._iStatFlag = true;
	remote.equipment.push_back({ .slot = INVLOC_HAND_LEFT, .item = staff });
	Item scroll;
	scroll._itype = ItemType::Misc;
	scroll._iMiscId = IMISC_SCROLL;
	scroll._iSpell = SpellID::Healing;
	scroll._iStatFlag = true;
	remote.inventory.push_back({ .item = scroll });
	remote.inventoryGrid[0] = 0;

	ServerBackedWorldProjection projection;
	ASSERT_TRUE(projection.Apply({ }, { }, { }, { }, 1).has_value());
	ServerBackedWorldPresentation presentation;
	ASSERT_TRUE(presentation.Apply(projection, 1, { local, remote }, 1).has_value());
	EXPECT_EQ(presentation.RemotePlayerCount(), 1U);
	uint32_t visitedEntityId = 0;
	presentation.ForEachRemotePlayer([&](uint32_t entityId, const Player &player) {
		visitedEntityId = entityId;
		EXPECT_TRUE(player.plractive);
		EXPECT_TRUE(player.isOnLevel(2));
		EXPECT_EQ(player.position.tile.x, remote.positionX);
		EXPECT_EQ(player.position.tile.y, remote.positionY);
		EXPECT_EQ(player._pGold, 1234);
		EXPECT_EQ(player._pExperience, 5678U);
		EXPECT_EQ(player.life.percentage, 51);
		EXPECT_EQ(player.mana.percentage, 51);
		EXPECT_EQ(player.getCharacterLevel(), 7);
		EXPECT_EQ(player.pDiabloKillLevel, 2);
		EXPECT_EQ(player.wReflections, 17);
		EXPECT_TRUE(player._pLvlVisited[4]);
		EXPECT_EQ(player._pStatPts, 9);
		EXPECT_EQ(player.attributes.strength.base, 12);
		EXPECT_EQ(player.attributes.strength.current, 14);
		EXPECT_STREQ(player._pName, "Remote");
		EXPECT_TRUE(player._pBlockFlag);
		EXPECT_NE(player._pISpells & GetSpellBitmask(SpellID::Firebolt), 0U);
		EXPECT_NE(player._pScrlSpells & GetSpellBitmask(SpellID::Healing), 0U);
	});
	EXPECT_EQ(visitedEntityId, remote.entityId);
	Player *remotePlayer = presentation.FindRemotePlayer(remote.entityId);
	ASSERT_NE(remotePlayer, nullptr);
	remote.combatMode = PM_NEWLVL;
	ASSERT_TRUE(presentation.Apply(projection, 1, { local, remote }, 1).has_value());
	remotePlayer = presentation.FindRemotePlayer(remote.entityId);
	ASSERT_NE(remotePlayer, nullptr);
	EXPECT_TRUE(remotePlayer->_pLvlChanging);

	remote.combatMode = PM_STAND;
	remote.life = 160;
	remote.mana = 100;
	remote.equipment.clear();
	remote.inventory.clear();
	remote.inventoryGrid.assign(InventoryGridCells, -1);
	ASSERT_TRUE(presentation.Apply(projection, 1, { local, remote }, 1).has_value());
	remotePlayer = presentation.FindRemotePlayer(remote.entityId);
	ASSERT_NE(remotePlayer, nullptr);
	EXPECT_FALSE(remotePlayer->_pBlockFlag);
	EXPECT_FALSE(remotePlayer->_pLvlChanging);
	EXPECT_EQ(remotePlayer->life.percentage, 12);
	EXPECT_EQ(remotePlayer->mana.percentage, 16);
	EXPECT_EQ(remotePlayer->_pISpells, 0U);
	EXPECT_EQ(remotePlayer->_pScrlSpells, 0U);
	const uint8_t previousLevel = currentLevelNumber();
	currentLevelNumber() = 2;
	EXPECT_TRUE(IsRemotePlayerOnActiveLevel(remote.levelId, *remotePlayer));
	currentLevelNumber() = 1;
	EXPECT_FALSE(IsRemotePlayerOnActiveLevel(remote.levelId, *remotePlayer));
	EXPECT_TRUE(IsRemotePlayerOnActiveLevel(0, *remotePlayer));
	currentLevelNumber() = previousLevel;
	EXPECT_EQ(presentation.FindRemotePlayer(99), nullptr);
	ProjectedPlayerSnapshot secondRemote = remote;
	secondRemote.entityId = 3;
	ASSERT_TRUE(presentation.Apply(projection, 1, { local, remote, secondRemote }, 1).has_value());
	EXPECT_EQ(presentation.FindRemotePlayer(remote.entityId), remotePlayer);

	ASSERT_TRUE(presentation.Apply(projection, 1, { local }, 1).has_value());
	EXPECT_EQ(presentation.RemotePlayerCount(), 0U);

	presentation.Clear();
}

TEST(ServerBackedPlayerSnapshot, ClearsRemoteInventoryWhenLegacyGridIsOmitted)
{
	ProjectedPlayerSnapshot local;
	local.entityId = 1;
	local.levelId = 1;
	local.characterClassId = 0;
	ProjectedPlayerSnapshot remote = local;
	remote.entityId = 2;
	remote.characterLevel = 1;
	remote.inventoryGrid.assign(InventoryGridCells, -1);
	Item equipped;
	equipped._itype = ItemType::Sword;
	remote.equipment.push_back({ .slot = INVLOC_HAND_LEFT, .item = equipped });
	Item held;
	held._itype = ItemType::Gold;
	remote.heldItem = ProjectedInventoryItem { .item = held };

	ServerBackedWorldProjection projection;
	ASSERT_TRUE(projection.Apply({ }, { }, { }, { }, 1).has_value());
	ServerBackedWorldPresentation presentation;
	ASSERT_TRUE(presentation.Apply(projection, 1, { local, remote }, 1).has_value());
	Player *remotePlayer = presentation.FindRemotePlayer(remote.entityId);
	ASSERT_NE(remotePlayer, nullptr);
	EXPECT_EQ(remotePlayer->InvBody[INVLOC_HAND_LEFT]._itype, ItemType::Sword);
	EXPECT_EQ(remotePlayer->HoldItem._itype, ItemType::Gold);

	remote.equipment.clear();
	remote.heldItem.reset();
	remote.inventoryGrid.clear();
	ASSERT_TRUE(presentation.Apply(projection, 1, { local, remote }, 1).has_value());
	remotePlayer = presentation.FindRemotePlayer(remote.entityId);
	ASSERT_NE(remotePlayer, nullptr);
	EXPECT_TRUE(remotePlayer->InvBody[INVLOC_HAND_LEFT].isEmpty());
	EXPECT_TRUE(remotePlayer->HoldItem.isEmpty());
	EXPECT_EQ(remotePlayer->_pNumInv, 0);
	EXPECT_EQ(remotePlayer->InvGrid[0], 0);

	presentation.Clear();
}

TEST(ServerBackedPlayerSnapshot, ProjectsRemoteWalkDestinationIntoNativeFuturePosition)
{
	ProjectedPlayerSnapshot local;
	local.entityId = 1;
	local.levelId = 1;
	ProjectedPlayerSnapshot remote = local;
	remote.entityId = 2;
	remote.positionX = 12;
	remote.positionY = 13;
	remote.combatMode = PM_WALK_SOUTHWARDS;
	remote.currentAction = ProjectedPlayerActionSnapshot {
		.kind = 1,
		.directionX = 1,
		.directionY = -1,
	};

	ServerBackedWorldProjection projection;
	ASSERT_TRUE(projection.Apply({ }, { }, { }, { }, 1).has_value());
	ServerBackedWorldPresentation presentation;
	ASSERT_TRUE(presentation.Apply(projection, 1, { local, remote }, 1).has_value());

	const Player *remotePlayer = presentation.FindRemotePlayer(remote.entityId);
	ASSERT_NE(remotePlayer, nullptr);
	EXPECT_EQ(remotePlayer->position.tile, (WorldTilePosition { 12, 13 }));
	EXPECT_EQ(remotePlayer->position.future, (WorldTilePosition { 13, 12 }));

	remote.positionX = 13;
	remote.positionY = 12;
	remote.currentAction->effectApplied = true;
	ASSERT_TRUE(presentation.Apply(projection, 1, { local, remote }, 1).has_value());
	remotePlayer = presentation.FindRemotePlayer(remote.entityId);
	ASSERT_NE(remotePlayer, nullptr);
	EXPECT_EQ(remotePlayer->position.future, (WorldTilePosition { 13, 12 }));

	presentation.Clear();
}

TEST(ServerBackedPlayerSnapshot, IgnoresStaleRemotePlayerSnapshots)
{
	ProjectedPlayerSnapshot local;
	local.entityId = 1;
	local.levelId = 1;
	ProjectedPlayerSnapshot remote = local;
	remote.entityId = 2;
	remote.life = 640;
	remote.lifeMaximum = 640;
	remote.positionX = 12;
	remote.positionY = 13;
	remote.serverTick = 10;

	ServerBackedWorldProjection projection;
	ASSERT_TRUE(projection.Apply({ }, { }, { }, { }, 1).has_value());
	ServerBackedWorldPresentation presentation;
	ASSERT_TRUE(presentation.Apply(projection, 1, { local, remote }, 1).has_value());

	remote.positionX = 2;
	remote.positionY = 3;
	remote.serverTick = 9;
	remote.combatMode = PM_DEATH;
	ASSERT_TRUE(presentation.Apply(projection, 1, { local, remote }, 1).has_value());

	const Player *remotePlayer = presentation.FindRemotePlayer(remote.entityId);
	ASSERT_NE(remotePlayer, nullptr);
	EXPECT_EQ(remotePlayer->position.tile, (WorldTilePosition { 12, 13 }));
	EXPECT_NE(remotePlayer->_pmode, PM_DEATH);

	presentation.Clear();
}

TEST(ServerBackedPlayerSnapshot, RejectsRemoteWalkDestinationOutsideNativeDungeon)
{
	ProjectedPlayerSnapshot local;
	local.entityId = 1;
	local.levelId = 1;
	ProjectedPlayerSnapshot remote = local;
	remote.entityId = 2;
	remote.positionX = 0;
	remote.positionY = 0;
	remote.combatMode = PM_WALK_SOUTHWARDS;
	remote.currentAction = ProjectedPlayerActionSnapshot {
		.kind = 1,
		.directionX = -1,
	};

	ServerBackedWorldProjection projection;
	ASSERT_TRUE(projection.Apply({ }, { }, { }, { }, 1).has_value());
	ServerBackedWorldPresentation presentation;
	const auto result = presentation.Apply(projection, 1, { local, remote }, 1);

	ASSERT_FALSE(result.has_value());
	EXPECT_EQ(result.error(), "Authoritative remote player walk destination cannot be represented by the native world tile type.");

	presentation.Clear();
}

TEST(ServerBackedPlayerSnapshot, PlacesRemoteAnimationAtAuthoritativeActionTick)
{
	LoadPlayerDataFiles();
	LoadSpellData();
	ProjectedPlayerSnapshot local;
	local.entityId = 1;
	local.levelId = 1;
	local.characterClassId = 0;
	ProjectedPlayerSnapshot remote = local;
	remote.entityId = 2;
	remote.life = 640;
	remote.lifeMaximum = 640;
	remote.combatMode = PM_SPELL;
	remote.serverTick = 3;
	remote.currentAction = ProjectedPlayerActionSnapshot {
		.kind = 3,
		.startedAtTick = 1,
		.hasCast = true,
		.spellId = static_cast<uint32_t>(SpellID::Firebolt),
	};

	ServerBackedWorldProjection projection;
	ASSERT_TRUE(projection.Apply({ }, { }, { }, { }, 1).has_value());
	ServerBackedWorldPresentation presentation;
	ASSERT_TRUE(presentation.Apply(projection, 1, { local, remote }, 1).has_value());
	int firstFrame = -1;
	presentation.ForEachRemotePlayer([&](uint32_t, const Player &player) {
		firstFrame = player.animInfo.currentFrame;
	});
	EXPECT_EQ(firstFrame, 2);

	remote.serverTick = 4;
	ASSERT_TRUE(presentation.Apply(projection, 1, { local, remote }, 1).has_value());
	int secondFrame = -1;
	presentation.ForEachRemotePlayer([&](uint32_t, const Player &player) {
		secondFrame = player.animInfo.currentFrame;
	});
	EXPECT_EQ(secondFrame, 3);

	presentation.Clear();
}

TEST(ServerBackedPlayerSnapshot, AppliesProjectedPlayerAnimationAtAuthoritativeActionTick)
{
	LoadPlayerDataFiles();
	LoadSpellData();
	ProjectedPlayerSnapshot snapshot;
	snapshot.characterClassId = static_cast<uint32_t>(HeroClass::Warrior);
	snapshot.life = 640;
	snapshot.lifeMaximum = 640;
	snapshot.combatMode = PM_SPELL;
	snapshot.serverTick = 3;
	snapshot.currentAction = ProjectedPlayerActionSnapshot {
		.kind = 3,
		.startedAtTick = 1,
		.hasCast = true,
		.spellId = static_cast<uint32_t>(SpellID::Firebolt),
	};

	Player player;
	player._pClass = HeroClass::Warrior;
	player.setAnimations();
	ApplyProjectedPlayerAnimation(
	    player,
	    snapshot,
	    GetRemotePlayerGraphic(snapshot),
	    true,
	    false,
	    0);

	EXPECT_EQ(player.animInfo.currentFrame, 2);
	AdvanceProjectedPlayerAnimation(player, GetRemotePlayerGraphic(snapshot), 1, false);
	EXPECT_EQ(player.animInfo.currentFrame, 3);
}

TEST(ServerBackedPlayerSnapshot, AdvancesRemotePlayerPresentationAnimationWithoutSimulation)
{
	LoadPlayerDataFiles();
	LoadSpellData();
	ProjectedPlayerSnapshot local;
	local.entityId = 1;
	local.levelId = 1;
	local.characterClassId = 0;
	ProjectedPlayerSnapshot remote = local;
	remote.entityId = 2;
	remote.life = 640;
	remote.lifeMaximum = 640;
	remote.combatMode = PM_SPELL;
	remote.serverTick = 3;
	remote.currentAction = ProjectedPlayerActionSnapshot {
		.kind = 3,
		.startedAtTick = 1,
		.hasCast = true,
		.spellId = static_cast<uint32_t>(SpellID::Firebolt),
	};

	ServerBackedWorldProjection projection;
	ASSERT_TRUE(projection.Apply({ }, { }, { }, { }, 1).has_value());
	ServerBackedWorldPresentation presentation;
	ASSERT_TRUE(presentation.Apply(projection, 1, { local, remote }, 1).has_value());
	int initialFrame = -1;
	presentation.ForEachRemotePlayer([&](uint32_t, const Player &player) {
		initialFrame = player.animInfo.currentFrame;
	});

	EXPECT_EQ(presentation.AdvanceAnimations(), 1U);
	int advancedFrame = -1;
	presentation.ForEachRemotePlayer([&](uint32_t, const Player &player) {
		advancedFrame = player.animInfo.currentFrame;
	});
	EXPECT_EQ(initialFrame, 2);
	EXPECT_EQ(advancedFrame, 3);

	presentation.Clear();
}

TEST(ServerBackedPlayerSnapshot, DoesNotDoubleAdvanceRemotePlayerAnimationBetweenSnapshots)
{
	LoadPlayerDataFiles();
	ProjectedPlayerSnapshot local;
	local.entityId = 1;
	local.levelId = 1;
	ProjectedPlayerSnapshot remote = local;
	remote.entityId = 2;
	remote.life = 640;
	remote.lifeMaximum = 640;
	remote.serverTick = 3;

	ServerBackedWorldProjection projection;
	ASSERT_TRUE(projection.Apply({ }, { }, { }, { }, 1).has_value());
	ServerBackedWorldPresentation presentation;
	ASSERT_TRUE(presentation.Apply(projection, 1, { local, remote }, 1).has_value());
	ASSERT_EQ(presentation.AdvanceAnimations(), 1U);

	int tickCounterAfterAdvance = -1;
	presentation.ForEachRemotePlayer([&](uint32_t, const Player &player) {
		tickCounterAfterAdvance = player.animInfo.tickCounterOfCurrentFrame;
	});
	ASSERT_EQ(tickCounterAfterAdvance, 1);

	remote.serverTick = 4;
	ASSERT_TRUE(presentation.Apply(projection, 1, { local, remote }, 1).has_value());
	int tickCounterAfterSnapshot = -1;
	presentation.ForEachRemotePlayer([&](uint32_t, const Player &player) {
		tickCounterAfterSnapshot = player.animInfo.tickCounterOfCurrentFrame;
	});
	EXPECT_EQ(tickCounterAfterSnapshot, tickCounterAfterAdvance);

	presentation.Clear();
}

TEST(ServerBackedPlayerSnapshot, MapsRemotePlayerCombatModesToNativeGraphics)
{
	ProjectedPlayerSnapshot snapshot;
	snapshot.life = 640;
	EXPECT_EQ(GetRemotePlayerGraphic(snapshot), player_graphic::Stand);

	snapshot.combatMode = PM_WALK_NORTHWARDS;
	EXPECT_EQ(GetRemotePlayerGraphic(snapshot), player_graphic::Walk);
	snapshot.combatMode = PM_ATTACK;
	EXPECT_EQ(GetRemotePlayerGraphic(snapshot), player_graphic::Attack);
	snapshot.combatMode = PM_RATTACK;
	EXPECT_EQ(GetRemotePlayerGraphic(snapshot), player_graphic::Attack);
	snapshot.combatMode = PM_BLOCK;
	EXPECT_EQ(GetRemotePlayerGraphic(snapshot), player_graphic::Block);
	snapshot.combatMode = PM_GOTHIT;
	EXPECT_EQ(GetRemotePlayerGraphic(snapshot), player_graphic::Hit);
	snapshot.combatMode = PM_SPELL;
	EXPECT_EQ(GetRemotePlayerGraphic(snapshot), player_graphic::Magic);
	snapshot.combatMode = PM_DEATH;
	EXPECT_EQ(GetRemotePlayerGraphic(snapshot), player_graphic::Death);
	snapshot.combatMode = PM_STAND;
	snapshot.life = 0;
	EXPECT_EQ(GetRemotePlayerGraphic(snapshot), player_graphic::Death);
}

TEST(ServerBackedPlayerSnapshot, MapsRemotePlayerSpellActionsToNativeGraphics)
{
	LoadSpellData();
	ProjectedPlayerSnapshot snapshot;
	snapshot.life = 640;
	snapshot.combatMode = PM_SPELL;
	snapshot.currentAction = ProjectedPlayerActionSnapshot {};

	snapshot.currentAction->spellId = static_cast<uint32_t>(SpellID::Firebolt);
	EXPECT_EQ(GetRemotePlayerGraphic(snapshot), player_graphic::Fire);
	snapshot.currentAction->spellId = static_cast<uint32_t>(SpellID::Lightning);
	EXPECT_EQ(GetRemotePlayerGraphic(snapshot), player_graphic::Lightning);
	snapshot.currentAction->spellId = static_cast<uint32_t>(SpellID::Healing);
	EXPECT_EQ(GetRemotePlayerGraphic(snapshot), player_graphic::Magic);
	snapshot.currentAction->spellId = 999;
	EXPECT_EQ(GetRemotePlayerGraphic(snapshot), player_graphic::Magic);
}

TEST(ServerBackedPlayerSnapshot, MapsRemotePlayerEquipmentToNativeGraphicBits)
{
	ProjectedPlayerSnapshot snapshot;
	EXPECT_EQ(GetRemotePlayerEquipmentGraphicBits(snapshot), static_cast<uint8_t>(PlayerWeaponGraphic::Unarmed));

	ProjectedEquippedItem sword;
	sword.slot = INVLOC_HAND_LEFT;
	sword.item._itype = ItemType::Sword;
	sword.item._iClass = ICLASS_WEAPON;
	ProjectedEquippedItem shield;
	shield.slot = INVLOC_HAND_RIGHT;
	shield.item._itype = ItemType::Shield;
	shield.item._iClass = ICLASS_ARMOR;
	ProjectedEquippedItem mediumArmor;
	mediumArmor.slot = INVLOC_CHEST;
	mediumArmor.item._itype = ItemType::MediumArmor;
	mediumArmor.item._iClass = ICLASS_ARMOR;
	snapshot.equipment = { sword, shield, mediumArmor };
	EXPECT_EQ(GetRemotePlayerEquipmentGraphicBits(snapshot), static_cast<uint8_t>(PlayerWeaponGraphic::SwordShield) | (1 << 4));

	sword.item._itype = ItemType::Bow;
	shield.item.clear();
	mediumArmor.item._itype = ItemType::HeavyArmor;
	snapshot.equipment = { sword, shield, mediumArmor };
	EXPECT_EQ(GetRemotePlayerEquipmentGraphicBits(snapshot), static_cast<uint8_t>(PlayerWeaponGraphic::Bow) | (1 << 5));
}

TEST(ServerBackedPlayerSnapshot, RefreshesProjectedPlayerGraphicsFromAuthoritativeEquipment)
{
	LoadPlayerDataFiles();
	ProjectedPlayerSnapshot snapshot;
	snapshot.characterClassId = static_cast<uint32_t>(HeroClass::Warrior);
	ProjectedEquippedItem sword;
	sword.slot = INVLOC_HAND_LEFT;
	sword.item._itype = ItemType::Sword;
	sword.item._iClass = ICLASS_WEAPON;
	ProjectedEquippedItem shield;
	shield.slot = INVLOC_HAND_RIGHT;
	shield.item._itype = ItemType::Shield;
	shield.item._iClass = ICLASS_ARMOR;
	snapshot.equipment = { sword, shield };
	Player player;
	player._pClass = HeroClass::Warrior;
	player._pgfxnum = static_cast<uint8_t>(PlayerWeaponGraphic::Unarmed);
	player._pNFrames = 0;

	RefreshProjectedPlayerGraphics(player, snapshot);

	EXPECT_EQ(player._pgfxnum, static_cast<uint8_t>(PlayerWeaponGraphic::SwordShield));
	EXPECT_EQ(player._pNFrames, 20);
}

TEST(ServerBackedPlayerSnapshot, RejectsUnknownExplicitNativeProjectileVisual)
{
	LoadMissileData();
	Missiles.clear();

	ProjectedProjectileSnapshot projectile;
	projectile.entityId = 120;
	projectile.sourceEntityId = 1;
	projectile.spellId = 4;
	projectile.levelId = 1;
	projectile.positionX = 4;
	projectile.positionY = 4;
	projectile.remainingTicks = 5;
	projectile.behavior = 2;
	projectile.nativeVisual = 999;

	ServerBackedWorldProjection projection;
	const auto result = projection.Apply({ }, { }, { }, { projectile }, 1);
	ASSERT_FALSE(result.has_value());
	EXPECT_EQ(result.error(), "Authoritative projectile projection contains invalid identity, lifecycle, or presentation data.");
}

TEST(ServerBackedPlayerSnapshot, PresentsContentOwnedMonsterMagicVisualFamilies)
{
	struct Family { int nativeVisual; MissileID missileId; };
	const Family families[] = {
		{ 29, MissileID::Lightning }, { 30, MissileID::OrangeFlare },
		{ 31, MissileID::BlueFlare }, { 32, MissileID::RedFlare },
		{ 33, MissileID::YellowFlare }, { 34, MissileID::BlueFlare2 },
		{ 35, MissileID::MagmaBall },
	};
	LoadMissileData();
	for (const auto &family : families) {
		Missiles.clear();
		ProjectedProjectileSnapshot projectile;
		projectile.entityId = static_cast<uint32_t>(500 + family.nativeVisual);
		projectile.sourceEntityId = 1;
		projectile.spellId = 4;
		projectile.levelId = 1;
		projectile.positionX = 4;
		projectile.positionY = 5;
		projectile.remainingTicks = 4;
		projectile.behavior = 50;
		projectile.nativeVisual = family.nativeVisual;
		ServerBackedWorldProjection projection;
		ASSERT_TRUE(projection.Apply({ }, { }, { }, { projectile }, 1).has_value());
		ServerBackedWorldPresentation presentation;
		ASSERT_TRUE(presentation.Apply(projection, 1).has_value());
		if (!IsMissileDataAvailable(family.missileId)) {
			EXPECT_TRUE(Missiles.empty());
			continue;
		}
		ASSERT_EQ(Missiles.size(), 1U);
		EXPECT_EQ(Missiles.front()._mitype, family.missileId);
		presentation.Clear();
	}
	Missiles.clear();
}

TEST(ServerBackedPlayerSnapshot, PrefersServerNativeVisualOverLegacyMonsterMagicIdentity)
{
	LoadMissileData();
	Missiles.clear();

	ProjectedProjectileSnapshot projectile;
	projectile.entityId = 600;
	projectile.sourceEntityId = 1;
	projectile.spellId = 4;
	projectile.levelId = 1;
	projectile.positionX = 4;
	projectile.positionY = 5;
	projectile.remainingTicks = 4;
	projectile.behavior = 50; // MonsterMagic
	projectile.nativeVisual = 25; // ChargedBolt server-owned visual.
	projectile.nativeMissileId = "Lightning";

	ServerBackedWorldProjection projection;
	ASSERT_TRUE(projection.Apply({ }, { }, { }, { projectile }, 1).has_value());
	ServerBackedWorldPresentation presentation;
	ASSERT_TRUE(presentation.Apply(projection, 1).has_value());

	ASSERT_EQ(Missiles.size(), 1U);
	EXPECT_EQ(Missiles.front()._mitype, MissileID::ChargedBolt);
	presentation.Clear();
	Missiles.clear();
}

TEST(ServerBackedPlayerSnapshot, PresentsAuthoritativeMonsterProjectileFamilies)
{
	struct Family {
		int behavior;
		MissileID missileId;
	};
	const Family families[] = {
		{ 51, MissileID::Acid },
		{ 52, MissileID::AcidSplat },
		{ 53, MissileID::AcidPuddle },
		{ 56, MissileID::ChargedBolt },
		{ 57, MissileID::HorkSpawn },
		{ 59, MissileID::Arrow },
	};

	LoadMissileData();
	for (const auto &family : families) {
		Missiles.clear();
		ProjectedProjectileSnapshot projectile;
		projectile.entityId = static_cast<uint32_t>(200 + family.behavior);
		projectile.sourceEntityId = 1;
		projectile.spellId = 4;
		projectile.levelId = 1;
		projectile.positionX = 6;
		projectile.positionY = 7;
		projectile.remainingTicks = 4;
		projectile.behavior = family.behavior;

		ServerBackedWorldProjection projection;
		ASSERT_TRUE(projection.Apply({ }, { }, { }, { projectile }, 1).has_value());
		ServerBackedWorldPresentation presentation;
		ASSERT_TRUE(presentation.Apply(projection, 1).has_value());
		if (!IsMissileDataAvailable(family.missileId)) {
			EXPECT_TRUE(Missiles.empty());
			continue;
		}
		if (!IsMissileDataAvailable(family.missileId)) {
			EXPECT_TRUE(Missiles.empty());
			continue;
		}
		ASSERT_EQ(Missiles.size(), 1U);
		EXPECT_EQ(Missiles.front()._mitype, family.missileId);
		EXPECT_EQ(Missiles.front()._miAuthoritativeEntityId, projectile.entityId);
		presentation.Clear();
	}
	Missiles.clear();
}

TEST(ServerBackedPlayerSnapshot, PresentsAuthoritativeAreaProjectileFamilies)
{
	struct Family {
		int behavior;
		MissileID missileId;
	};
	const Family families[] = {
		{ 4, MissileID::ThinLightning },
		{ 24, MissileID::FlameWaveControl },
		{ 25, MissileID::FlameWave },
		{ 27, MissileID::Inferno },
		{ 33, MissileID::ApocalypseBoom },
		{ 35, MissileID::Elemental },
		{ 31, MissileID::NovaBall },
	};

	LoadMissileData();
	for (const auto &family : families) {
		Missiles.clear();
		ProjectedProjectileSnapshot projectile;
		projectile.entityId = static_cast<uint32_t>(300 + family.behavior);
		projectile.sourceEntityId = 1;
		projectile.spellId = 4;
		projectile.levelId = 1;
		projectile.positionX = 6;
		projectile.positionY = 7;
		projectile.remainingTicks = 4;
		projectile.behavior = family.behavior;

		ServerBackedWorldProjection projection;
		ASSERT_TRUE(projection.Apply({ }, { }, { }, { projectile }, 1).has_value());
		ServerBackedWorldPresentation presentation;
		ASSERT_TRUE(presentation.Apply(projection, 1).has_value());
		if (!IsMissileDataAvailable(family.missileId)) {
			EXPECT_TRUE(Missiles.empty());
			continue;
		}
		if (!IsMissileDataAvailable(family.missileId)) {
			EXPECT_TRUE(Missiles.empty());
			continue;
		}
		ASSERT_EQ(Missiles.size(), 1U);
		EXPECT_EQ(Missiles.front()._mitype, family.missileId);
		EXPECT_EQ(Missiles.front()._miAuthoritativeEntityId, projectile.entityId);
		presentation.Clear();
	}
	Missiles.clear();
}

TEST(ServerBackedPlayerSnapshot, PresentsAuthoritativeLayeredAndArrowProjectileFamilies)
{
	struct Family {
		int behavior;
		MissileID missileId;
	};
	const Family families[] = {
		{ 6, MissileID::FlashBottom },
		{ 7, MissileID::FlashTop },
		{ 11, MissileID::ResurrectBeam },
		{ 58, MissileID::Arrow },
	};

	LoadMissileData();
	for (const auto &family : families) {
		Missiles.clear();
		ProjectedProjectileSnapshot projectile;
		projectile.entityId = static_cast<uint32_t>(400 + family.behavior);
		projectile.sourceEntityId = 1;
		projectile.spellId = 4;
		projectile.levelId = 1;
		projectile.positionX = 8;
		projectile.positionY = 9;
		projectile.remainingTicks = 6;
		projectile.behavior = family.behavior;

		ServerBackedWorldProjection projection;
		ASSERT_TRUE(projection.Apply({ }, { }, { }, { projectile }, 1).has_value());
		ServerBackedWorldPresentation presentation;
		ASSERT_TRUE(presentation.Apply(projection, 1).has_value());
		if (!IsMissileDataAvailable(family.missileId)) {
			EXPECT_TRUE(Missiles.empty());
			continue;
		}
		if (!IsMissileDataAvailable(family.missileId)) {
			EXPECT_TRUE(Missiles.empty());
			continue;
		}
		ASSERT_EQ(Missiles.size(), 1U);
		EXPECT_EQ(Missiles.front()._mitype, family.missileId);
		EXPECT_EQ(Missiles.front()._miAuthoritativeEntityId, projectile.entityId);
		presentation.Clear();
	}
	Missiles.clear();
}

TEST(ServerBackedPlayerSnapshot, WorldProjectionProvidesStableInteractionLookups)
{
	ServerBackedWorldProjection projection;
	ProjectedMonsterSnapshot monster;
	monster.entityId = 40;
	monster.levelId = 1;
	monster.positionX = 4;
	monster.positionY = 5;
	monster.alive = true;
	monster.moving = true;
	monster.futurePositionX = 5;
	monster.futurePositionY = 5;
	ProjectedWorldItemSnapshot item;
	item.entityId = 20;
	item.levelId = 2;
	item.positionX = 4;
	item.positionY = 5;
	ProjectedObjectSnapshot object;
	object.entityId = 30;
	object.levelId = 1;
	object.positionX = 4;
	object.positionY = 5;
	object.trapArmed = true;

	ASSERT_TRUE(projection.Apply({ monster }, { item }, { object }, 1).has_value());
	EXPECT_EQ(projection.MonsterAt(4, 5), std::optional<uint32_t> { 40 });
	EXPECT_EQ(projection.MonsterAt(5, 5), std::optional<uint32_t> { 40 });
	EXPECT_FALSE(projection.MonsterAt(6, 5).has_value());
	EXPECT_FALSE(projection.WorldItemAt(4, 5).has_value());
	EXPECT_EQ(projection.ObjectAt(4, 5), std::optional<uint32_t>(30));
	EXPECT_FALSE(projection.QuestObjectAt(4, 5).has_value());
	EXPECT_EQ(projection.AuthoritativeObjectAt(4, 5), std::optional<uint32_t>(30));
	monster.sneakHidden = true;
	ASSERT_TRUE(projection.Apply({ monster }, { }, { }, 1).has_value());
	EXPECT_FALSE(projection.MonsterAt(4, 5).has_value());
	EXPECT_FALSE(projection.MonsterAt(5, 5).has_value());
	ProjectedObjectSnapshot questObject;
	questObject.entityId = 32;
	questObject.levelId = 1;
	questObject.positionX = 7;
	questObject.positionY = 8;
	questObject.effectKind = static_cast<int32_t>(ProjectedObjectEffectKind::QuestSequence);
	ASSERT_TRUE(projection.Apply({ monster }, { item }, { questObject }, 1).has_value());
	EXPECT_EQ(projection.QuestObjectAt(7, 8), std::optional<uint32_t>(32));
	EXPECT_EQ(projection.AuthoritativeObjectAt(7, 8), std::optional<uint32_t>(32));
	questObject.activated = true;
	ASSERT_TRUE(projection.Apply({ monster }, { item }, { questObject }, 1).has_value());
	EXPECT_FALSE(projection.QuestObjectAt(7, 8).has_value());
	EXPECT_FALSE(projection.AuthoritativeObjectAt(7, 8).has_value());
	ProjectedObjectSnapshot mutationObject;
	mutationObject.entityId = 33;
	mutationObject.levelId = 1;
	mutationObject.positionX = 9;
	mutationObject.positionY = 10;
	mutationObject.effectKind = static_cast<int32_t>(ProjectedObjectEffectKind::WorldMutation);
	ASSERT_TRUE(projection.Apply({ }, { }, { mutationObject }, 1).has_value());
	EXPECT_EQ(projection.AuthoritativeObjectAt(9, 10), std::optional<uint32_t>(33));
	EXPECT_FALSE(projection.QuestObjectAt(9, 10).has_value());
	object.activated = true;
	ASSERT_TRUE(projection.Apply({ }, { item }, { object }, 1).has_value());
	EXPECT_FALSE(projection.ObjectAt(4, 5).has_value());
	object.activated = false;
	object.destroyed = true;
	ASSERT_TRUE(projection.Apply({ }, { item }, { object }, 1).has_value());
	EXPECT_FALSE(projection.ObjectAt(4, 5).has_value());
	ASSERT_TRUE(projection.Apply({ }, { item }, { }, 2).has_value());
	EXPECT_EQ(projection.WorldItemAt(4, 5), std::optional<uint32_t>(20));
}

TEST(ServerBackedPlayerSnapshot, WorldProjectionOrdersOverlappingEntitiesByIdentity)
{
	ProjectedMonsterSnapshot higherMonster;
	higherMonster.entityId = 40;
	higherMonster.levelId = 1;
	higherMonster.positionX = 4;
	higherMonster.positionY = 5;
	higherMonster.alive = true;
	ProjectedMonsterSnapshot lowerMonster = higherMonster;
	lowerMonster.entityId = 39;

	ProjectedWorldItemSnapshot higherItem;
	higherItem.entityId = 30;
	higherItem.levelId = 1;
	higherItem.positionX = 4;
	higherItem.positionY = 5;
	ProjectedWorldItemSnapshot lowerItem = higherItem;
	lowerItem.entityId = 29;

	ProjectedObjectSnapshot higherObject;
	higherObject.entityId = 20;
	higherObject.levelId = 1;
	higherObject.positionX = 4;
	higherObject.positionY = 5;
	ProjectedObjectSnapshot lowerObject = higherObject;
	lowerObject.entityId = 19;

	ServerBackedWorldProjection projection;
	ASSERT_TRUE(projection.Apply(
		{ higherMonster, lowerMonster },
		{ higherItem, lowerItem },
		{ higherObject, lowerObject },
		1)
		.has_value());
	EXPECT_EQ(projection.MonsterAt(4, 5), std::optional<uint32_t> { 39 });
	EXPECT_EQ(projection.WorldItemAt(4, 5), std::optional<uint32_t> { 29 });
	EXPECT_EQ(projection.ObjectAt(4, 5), std::optional<uint32_t> { 19 });
}

TEST(ServerBackedPlayerSnapshot, RejectsMalformedMonsterOccupancy)
{
	protocol::Snapshot distantDestination;
	auto *monster = distantDestination.add_monsters();
	monster->set_entity_id(7);
	monster->set_position_x(1);
	monster->set_position_y(1);
	monster->set_moving(true);
	monster->set_future_position_x(3);
	monster->set_future_position_y(1);
	EXPECT_FALSE(ProjectMonsterSnapshots(distantDestination).has_value());

	protocol::Snapshot inconsistentStationary;
	monster = inconsistentStationary.add_monsters();
	monster->set_entity_id(7);
	monster->set_position_x(1);
	monster->set_position_y(1);
	monster->set_future_position_x(2);
	monster->set_future_position_y(1);
	EXPECT_FALSE(ProjectMonsterSnapshots(inconsistentStationary).has_value());

	protocol::Snapshot invalidIdentity;
	monster = invalidIdentity.add_monsters();
	monster->set_entity_id(8);
	monster->set_unique_identity("Defiler");
	EXPECT_FALSE(ProjectMonsterSnapshots(invalidIdentity).has_value());

	protocol::Snapshot oversizedIdentity;
	monster = oversizedIdentity.add_monsters();
	monster->set_entity_id(8);
	monster->set_is_unique(true);
	monster->set_unique_identity(std::string(129, 'x'));
	EXPECT_FALSE(ProjectMonsterSnapshots(oversizedIdentity).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsMovingDestinationOutsideNativeDungeon)
{
	ProjectedMonsterSnapshot monster;
	monster.entityId = 31;
	monster.levelId = 1;
	monster.positionX = 0;
	monster.positionY = 1;
	monster.alive = true;
	monster.moving = true;
	monster.futurePositionX = -1;
	monster.futurePositionY = 1;
	ServerBackedWorldProjection projection;
	const auto result = projection.Apply({ monster }, { }, { }, 1);
	ASSERT_FALSE(result.has_value());
	EXPECT_EQ(result.error(), "Authoritative monster projection is outside projected level geometry.");
}

TEST(ServerBackedPlayerSnapshot, AdvancesAuthoritativeMonsterWalkAnimation)
{
	InitLevelMonsters();
	LoadMonsterData();
	ASSERT_TRUE(AddMonsterType(MT_NZOMBIE, PLACE_SCATTER).has_value());

	ProjectedMonsterSnapshot monster;
	monster.entityId = 41;
	monster.monsterId = MT_NZOMBIE;
	monster.levelId = 1;
	monster.positionX = 4;
	monster.positionY = 5;
	monster.futurePositionX = 5;
	monster.futurePositionY = 5;
	monster.alive = true;
	monster.moving = true;
	monster.direction = static_cast<int32_t>(Direction::East);

	ServerBackedWorldProjection projection;
	ASSERT_TRUE(projection.Apply({ monster }, { }, { }, 1).has_value());
	ServerBackedWorldPresentation presentation;
	ASSERT_TRUE(presentation.Apply(projection, 1).has_value());

	EXPECT_EQ(presentation.AdvanceAnimations(), 1U);

	presentation.Clear();
}

TEST(ServerBackedPlayerSnapshot, AppliesAuthoredMonsterDialogueStateToNativePresentation)
{
	InitLevelMonsters();
	LoadMonsterData();
	ASSERT_TRUE(AddMonsterType(MT_NZOMBIE, PLACE_SCATTER).has_value());

	ProjectedMonsterSnapshot monster;
	monster.entityId = 42;
	monster.monsterId = MT_NZOMBIE;
	monster.levelId = 1;
	monster.positionX = 4;
	monster.positionY = 5;
	monster.alive = true;
	monster.isUnique = true;
	monster.uniqueIdentity = "Warlord";
	monster.dialogueState = 1;

	ServerBackedWorldProjection projection;
	ASSERT_TRUE(projection.Apply({ monster }, { }, { }, 1).has_value());
	ServerBackedWorldPresentation presentation;
	ASSERT_TRUE(presentation.Apply(projection, 1).has_value());
	EXPECT_EQ(
	    ResolveAuthoritativeMonsterDialoguePresentationState(monster.dialogueState),
	    AuthoritativeMonsterDialoguePresentationState::Inquiring);

	monster.dialogueState = 2;
	ASSERT_TRUE(projection.Apply({ monster }, { }, { }, 1).has_value());
	ASSERT_TRUE(presentation.Apply(projection, 1).has_value());
	EXPECT_EQ(
	    ResolveAuthoritativeMonsterDialoguePresentationState(monster.dialogueState),
	    AuthoritativeMonsterDialoguePresentationState::Combat);
	EXPECT_EQ(
	    ResolveAuthoritativeMonsterDialoguePresentationState(0),
	    AuthoritativeMonsterDialoguePresentationState::Legacy);

	presentation.Clear();
}

TEST(ServerBackedPlayerSnapshot, RejectsMalformedAuthoritativeObjects)
{
	protocol::Snapshot missingId;
	missingId.add_objects()->set_object_id(1);
	EXPECT_FALSE(ProjectObjectSnapshots(missingId).has_value());

	protocol::Snapshot duplicate;
	duplicate.add_objects()->set_entity_id(7);
	duplicate.mutable_objects(0)->set_object_id(1);
	duplicate.add_objects()->set_entity_id(7);
	duplicate.mutable_objects(1)->set_object_id(2);
	EXPECT_FALSE(ProjectObjectSnapshots(duplicate).has_value());

	protocol::Snapshot passableBreakable;
	auto *invalidFlags = passableBreakable.add_objects();
	invalidFlags->set_entity_id(7);
	invalidFlags->set_object_id(1);
	invalidFlags->set_missile_passable(true);
	invalidFlags->set_breakable(true);
	EXPECT_FALSE(ProjectObjectSnapshots(passableBreakable).has_value());

	protocol::Snapshot destroyedUnbreakable;
	invalidFlags = destroyedUnbreakable.add_objects();
	invalidFlags->set_entity_id(7);
	invalidFlags->set_object_id(1);
	invalidFlags->set_destroyed(true);
	EXPECT_FALSE(ProjectObjectSnapshots(destroyedUnbreakable).has_value());

	protocol::Snapshot invalidEffect;
	invalidFlags = invalidEffect.add_objects();
	invalidFlags->set_entity_id(7);
	invalidFlags->set_object_id(1);
	invalidFlags->set_effect_kind(10);
	EXPECT_FALSE(ProjectObjectSnapshots(invalidEffect).has_value());

	protocol::Snapshot invalidPresentation;
	invalidFlags = invalidPresentation.add_objects();
	invalidFlags->set_entity_id(7);
	invalidFlags->set_object_id(1);
	invalidFlags->set_presentation_kind(static_cast<protocol::ObjectPresentationKind>(99));
	EXPECT_FALSE(ProjectObjectSnapshots(invalidPresentation).has_value());

	protocol::Snapshot invalidReward;
	invalidFlags = invalidReward.add_objects();
	invalidFlags->set_entity_id(7);
	invalidFlags->set_object_id(1);
	invalidFlags->set_reward_kind(1);
	EXPECT_FALSE(ProjectObjectSnapshots(invalidReward).has_value());

	protocol::Snapshot invalidFollowUp;
	invalidFlags = invalidFollowUp.add_objects();
	invalidFlags->set_entity_id(7);
	invalidFlags->set_object_id(1);
	invalidFlags->set_quest_sequence_follow_up_kind(1);
	invalidFlags->set_quest_sequence_follow_up_amount(1);
	EXPECT_FALSE(ProjectObjectSnapshots(invalidFollowUp).has_value());

	protocol::Snapshot invalidNativePresentation;
	invalidFlags = invalidNativePresentation.add_objects();
	invalidFlags->set_entity_id(7);
	invalidFlags->set_object_id(6007);
	invalidFlags->set_native_object_id(static_cast<uint32_t>(OBJ_LAST) + 1);
	EXPECT_FALSE(ProjectObjectSnapshots(invalidNativePresentation).has_value());

	protocol::Snapshot dialogueOnly;
	auto *dialogueObject = dialogueOnly.add_objects();
	dialogueObject->set_entity_id(7);
	dialogueObject->set_object_id(1);
	dialogueObject->set_dialogue_message_id(412);
	auto projectedDialogue = ProjectObjectSnapshots(dialogueOnly);
	ASSERT_TRUE(projectedDialogue.has_value());
	EXPECT_EQ(projectedDialogue->front().dialogueMessageId, 412);

	protocol::Snapshot invalidDialogue = dialogueOnly;
	invalidDialogue.mutable_objects(0)->set_dialogue_message_id(-1);
	EXPECT_FALSE(ProjectObjectSnapshots(invalidDialogue).has_value());
	invalidDialogue = dialogueOnly;
	invalidDialogue.mutable_objects(0)->set_dialogue_message_id(std::numeric_limits<int16_t>::max() + 1);
	EXPECT_FALSE(ProjectObjectSnapshots(invalidDialogue).has_value());

	protocol::Snapshot atCapacity;
	for (int index = 0; index < MAXOBJECTS; ++index) {
		auto *object = atCapacity.add_objects();
		object->set_entity_id(static_cast<uint32_t>(index + 1));
		object->set_object_id(1);
		object->set_level_id(3);
	}
	EXPECT_TRUE(ProjectObjectSnapshots(atCapacity).has_value());

	protocol::Snapshot overCapacity = atCapacity;
	auto *overflow = overCapacity.add_objects();
	overflow->set_entity_id(MAXOBJECTS + 1);
	overflow->set_object_id(1);
	overflow->set_level_id(3);
	EXPECT_FALSE(ProjectObjectSnapshots(overCapacity).has_value());
}

TEST(ServerBackedPlayerSnapshot, StateRetainsLastValidSnapshotUntilReplacement)
{
	protocol::Snapshot snapshot;
	auto *player = snapshot.add_players();
	player->set_entity_id(7);
	player->set_combat_mode(protocol::PLAYER_COMBAT_MODE_BLOCK);
	player->set_combat_mode_end_tick(9);
	player->add_inventory()->set_item_seed(1);
	auto projected = ProjectPlayerSnapshot(snapshot, 7);
	ASSERT_TRUE(projected.has_value()) << projected.error();
	EXPECT_EQ(projected->combatMode, static_cast<uint32_t>(PM_BLOCK));
	EXPECT_EQ(projected->combatModeEndTick, 9U);

	ServerBackedPlayerState state;
	ASSERT_TRUE(state.ApplySnapshot(std::move(*projected)));
	ASSERT_TRUE(state.HasSnapshot());
	ASSERT_NE(state.FindInventoryItem(0), nullptr);
	state.Clear();
	EXPECT_FALSE(state.HasSnapshot());
	EXPECT_EQ(state.FindInventoryItem(0), nullptr);
}

TEST(ServerBackedPlayerSnapshot, StateRejectsStaleSnapshotWithoutRegression)
{
	ServerBackedPlayerState state;
	ProjectedPlayerSnapshot current;
	current.entityId = 7;
	current.serverTick = 12;
	current.positionX = 4;
	current.positionY = 5;
	ASSERT_TRUE(state.ApplySnapshot(current));

	ProjectedPlayerSnapshot stale = current;
	stale.serverTick = 11;
	stale.positionX = 1;
	stale.positionY = 2;
	EXPECT_FALSE(state.ApplySnapshot(stale));
	ASSERT_NE(state.Snapshot(), nullptr);
	EXPECT_EQ(state.Snapshot()->serverTick, 12U);
	EXPECT_EQ(state.Snapshot()->positionX, 4);
	EXPECT_EQ(state.Snapshot()->positionY, 5);
}

TEST(ServerBackedPlayerSnapshot, AppliesResourcesEquipmentInventoryAndGrid)
{
	LoadSpellData();
	Player player;
	player.life.maximum = 1000;
	player.life.maximumBase = 1000;
	player.mana.maximum = 500;
	player.mana.maximumBase = 500;
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.positionX = 12;
	snapshot.positionY = 13;
	snapshot.life = 800;
	snapshot.lifeMaximum = 1000;
	snapshot.lifeMaximumBase = 800;
	snapshot.mana = 125;
	snapshot.manaMaximum = 750;
	snapshot.manaMaximumBase = 500;
	snapshot.gold = 450;
	snapshot.experience = 99;
	snapshot.characterLevel = 4;
	snapshot.diabloKillLevel = 2;
	snapshot.reflectCharges = 17;
	snapshot.visitedLevelIds = { 0, 4, 21 };
	snapshot.characterClassId = static_cast<uint32_t>(HeroClass::Monk);
	snapshot.characterName = "Dead-Monk";
	snapshot.manaShieldActive = true;
	snapshot.statusEffects.push_back({
	    .effectId = static_cast<uint32_t>(SpellID::Infravision),
	    .remainingTicks = 1584,
	    .behavior = 1,
	});
	snapshot.statusEffects.push_back({
	    .effectId = static_cast<uint32_t>(SpellID::Search),
	    .remainingTicks = 255,
	    .behavior = 2,
	});
	snapshot.statusEffects.push_back({
	    .effectId = 53,
	    .remainingTicks = UINT32_MAX,
	    .behavior = 3,
	});
	snapshot.friendlyMode = false;
	snapshot.combatMode = static_cast<uint32_t>(PM_BLOCK);
	snapshot.combatModeEndTick = 9;
	snapshot.abilitySpellId = static_cast<uint32_t>(SpellID::Search);
	snapshot.memorizedSpells.push_back({ static_cast<uint32_t>(SpellID::Firebolt), 2 });
	snapshot.strength = { 10, 12 };
	snapshot.inventoryGrid.assign(InventoryGridCells, -1);
	Item inventoryItem;
	inventoryItem._itype = ItemType::Sword;
	snapshot.inventory.push_back({ .itemSeed = 42, .item = inventoryItem });
	snapshot.inventoryGrid[0] = 0;
	Item equippedItem;
	equippedItem._itype = ItemType::Shield;
	snapshot.equipment.push_back({ .slot = INVLOC_HAND_LEFT, .itemSeed = 43, .item = equippedItem });
	Item staffItem;
	staffItem._itype = ItemType::Staff;
	staffItem._iClass = ICLASS_WEAPON;
	staffItem._iSpell = SpellID::Firebolt;
	staffItem._iCharges = 5;
	staffItem._iStatFlag = true;
	snapshot.equipment.push_back({ .slot = INVLOC_HAND_RIGHT, .itemSeed = 46, .item = staffItem });
	Item beltItem;
	beltItem._itype = ItemType::Misc;
	snapshot.belt.push_back({ .slot = 2, .itemSeed = 44, .item = beltItem });
	Item stashScroll;
	stashScroll._itype = ItemType::Misc;
	stashScroll._iMiscId = IMISC_SCROLL;
	stashScroll._iSpell = SpellID::Healing;
	snapshot.stashOpen = true;
	snapshot.stash.push_back({
		.page = 0,
		.cell = 3,
		.itemSeed = 45,
		.stableItemId = 46,
		.spellId = static_cast<uint32_t>(SpellID::Healing),
		.spellTargetingKind = ProjectedSpellTargetingKind::Tile,
		.item = stashScroll,
	});
	Item heldItem;
	heldItem._itype = ItemType::Gold;
	heldItem._iSeed = 99;
	heldItem._iCurs = 4;
	heldItem._ivalue = 65;
	snapshot.heldItem = ProjectedInventoryItem {
		.itemSeed = 99,
		.price = 65,
		.item = heldItem,
	};

	ASSERT_TRUE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_EQ(player.position.tile.x, 12);
	EXPECT_EQ(player.position.tile.y, 13);
	EXPECT_EQ(player._pGold, 450);
	EXPECT_EQ(player._pExperience, 99U);
	EXPECT_EQ(player.pDiabloKillLevel, 2U);
	EXPECT_EQ(player.wReflections, 17U);
	EXPECT_TRUE(player._pLvlVisited[4]);
	EXPECT_TRUE(player._pLvlVisited[21]);
	EXPECT_FALSE(player._pLvlVisited[5]);
	EXPECT_EQ(player._pClass, HeroClass::Monk);
	EXPECT_STREQ(player._pName, "Dead-Monk");
	EXPECT_TRUE(player.pManaShield);
	EXPECT_TRUE(player._pInfraFlag);
	EXPECT_TRUE(HasAnyOf(player._pSpellFlags, SpellFlag::Etherealize));
	EXPECT_TRUE(CurrentAutomapManager.GetAutoMapShowItems());
	EXPECT_FALSE(player.friendlyMode);
	EXPECT_EQ(player._pmode, PM_BLOCK);
	EXPECT_TRUE(player._pBlockFlag);
	EXPECT_EQ(player._pAblSpells, GetSpellBitmask(SpellID::Search));
	EXPECT_EQ(player._pMemSpells, GetSpellBitmask(SpellID::Firebolt));
	EXPECT_NE(player._pISpells & GetSpellBitmask(SpellID::Firebolt), 0U);
	EXPECT_NE(player._pScrlSpells & GetSpellBitmask(SpellID::Healing), 0U);
	EXPECT_EQ(player._pSplLvl[static_cast<size_t>(SpellID::Firebolt)], 2);
	EXPECT_EQ(player._pRSpell, SpellID::Firebolt);
	EXPECT_EQ(player._pRSplType, SpellType::Spell);
	EXPECT_EQ(player.attributes.strength.base, 10);
	EXPECT_EQ(player.attributes.strength.current, 12);
	EXPECT_EQ(player.life.current, 800);
	EXPECT_EQ(player.life.maximum, 1000);
	EXPECT_EQ(player.life.maximumBase, 800);
	EXPECT_EQ(player.mana.current, 125);
	EXPECT_EQ(player.mana.maximum, 750);
	EXPECT_EQ(player.mana.maximumBase, 500);
	EXPECT_EQ(player.SpdList[2]._itype, ItemType::Misc);
	EXPECT_EQ(player._pNumInv, 1);
	EXPECT_EQ(player.InvList[0]._itype, ItemType::Sword);
	EXPECT_EQ(player.InvBody[INVLOC_HAND_LEFT]._itype, ItemType::Shield);
	EXPECT_EQ(player.InvGrid[0], 1);
	EXPECT_EQ(player.InvGrid[1], 0);
	EXPECT_EQ(player.HoldItem._itype, ItemType::Gold);
	EXPECT_EQ(player.HoldItem._iSeed, 99U);
	EXPECT_EQ(pcurs, CURSOR_FIRSTITEM + 4);

	snapshot.heldItem.reset();
	snapshot.statusEffects.clear();
	snapshot.equipment.clear();
	snapshot.combatMode = static_cast<uint32_t>(PM_STAND);
	snapshot.combatModeEndTick = 0;
	ASSERT_TRUE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_TRUE(player.HoldItem.isEmpty());
	EXPECT_FALSE(player._pBlockFlag);
	EXPECT_EQ(player._pISpells, 0U);
	EXPECT_FALSE(player._pInfraFlag);
	EXPECT_FALSE(HasAnyOf(player._pSpellFlags, SpellFlag::Etherealize));
	EXPECT_FALSE(CurrentAutomapManager.GetAutoMapShowItems());
	EXPECT_EQ(pcurs, CURSOR_HAND);
}

TEST(ServerBackedPlayerSnapshot, ClearsStaleStashScrollSourcesOnSnapshotRefresh)
{
	LoadSpellData();
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.characterLevel = 1;
	snapshot.inventoryGrid.assign(InventoryGridCells, -1);
	Item inventoryScroll;
	inventoryScroll._itype = ItemType::Misc;
	inventoryScroll._iMiscId = IMISC_SCROLL;
	inventoryScroll._iSpell = SpellID::Firebolt;
	inventoryScroll._iStatFlag = true;
	snapshot.inventory.push_back({ .itemSeed = 2, .item = inventoryScroll });
	snapshot.inventoryGrid[0] = 0;
	snapshot.stashOpen = true;
	Item scroll;
	scroll._itype = ItemType::Misc;
	scroll._iMiscId = IMISC_SCROLL;
	scroll._iSpell = SpellID::Healing;
	snapshot.stash.push_back({
		.page = 0,
		.cell = 0,
		.itemSeed = 1,
		.spellId = static_cast<uint32_t>(SpellID::Healing),
		.spellTargetingKind = ProjectedSpellTargetingKind::Tile,
		.width = 1,
		.height = 1,
		.item = scroll,
	});
	Player player;

	ASSERT_TRUE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_NE(player._pScrlSpells & GetSpellBitmask(SpellID::Healing), 0U);
	EXPECT_NE(player._pScrlSpells & GetSpellBitmask(SpellID::Firebolt), 0U);

	snapshot.stashOpen = false;
	snapshot.stash.clear();
	ASSERT_TRUE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_EQ(player._pScrlSpells & GetSpellBitmask(SpellID::Healing), 0U);
	EXPECT_NE(player._pScrlSpells & GetSpellBitmask(SpellID::Firebolt), 0U);
}

TEST(ServerBackedPlayerSnapshot, AppliesTimedMoveDestinationToNativeFuturePosition)
{
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.positionX = 12;
	snapshot.positionY = 13;
	snapshot.facingDirection = static_cast<uint32_t>(Direction::SouthEast);
	snapshot.combatMode = static_cast<uint32_t>(PM_WALK_SOUTHWARDS);
	snapshot.combatModeEndTick = 18;
	snapshot.inventoryGrid.assign(InventoryGridCells, -1);
	snapshot.currentAction = ProjectedPlayerActionSnapshot {
		.kind = 1,
		.startedAtTick = 10,
		.effectTick = 18,
		.readyTick = 18,
		.endTick = 18,
		.directionX = 1,
	};
	Player player;

	ASSERT_TRUE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_EQ(player.position.tile.x, 12);
	EXPECT_EQ(player.position.tile.y, 13);
	EXPECT_EQ(player.position.future.x, 13);
	EXPECT_EQ(player.position.future.y, 13);
	EXPECT_EQ(player.direction, Direction::SouthEast);

	snapshot.positionX = 13;
	snapshot.positionY = 13;
	snapshot.currentAction->effectApplied = true;
	ASSERT_TRUE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_EQ(player.position.future, (WorldTilePosition { 13, 13 }));
}

TEST(ServerBackedPlayerSnapshot, AppliesDeathAndLevelTransitionActions)
{
	ProjectedPlayerSnapshot death;
	death.entityId = 7;
	death.life = 0;
	death.lifeMaximum = 20;
	death.combatMode = static_cast<uint32_t>(PM_DEATH);
	death.combatModeEndTick = 40;
	death.inventoryGrid.assign(InventoryGridCells, -1);
	death.currentAction = ProjectedPlayerActionSnapshot {
		.kind = 4,
		.startedAtTick = 1,
		.effectTick = 1,
		.readyTick = 40,
		.endTick = 40,
		.effectApplied = true,
		.targetEntityId = 9,
	};
	Player deadPlayer;

	ASSERT_TRUE(ApplyServerBackedPlayerSnapshot(deadPlayer, death).has_value());
	EXPECT_EQ(deadPlayer._pmode, PM_DEATH);
	EXPECT_EQ(deadPlayer.life.current, 0);

	ProjectedPlayerSnapshot transition;
	transition.entityId = 7;
	transition.life = 20;
	transition.lifeMaximum = 20;
	transition.levelId = 2;
	transition.positionX = 3;
	transition.positionY = 4;
	transition.combatMode = static_cast<uint32_t>(PM_NEWLVL);
	transition.combatModeEndTick = 2;
	transition.inventoryGrid.assign(InventoryGridCells, -1);
	transition.currentAction = ProjectedPlayerActionSnapshot {
		.kind = 5,
		.startedAtTick = 1,
		.effectTick = 1,
		.readyTick = 2,
		.endTick = 2,
		.effectApplied = true,
		.targetEntityId = 5,
	};
	Player transitioningPlayer;

	ASSERT_TRUE(ApplyServerBackedPlayerSnapshot(
	                transitioningPlayer,
	                transition)
	                .has_value());
	EXPECT_EQ(transitioningPlayer._pmode, PM_NEWLVL);
	EXPECT_TRUE(transitioningPlayer._pLvlChanging);
	EXPECT_TRUE(transitioningPlayer.isOnLevel(2));
	EXPECT_EQ(transitioningPlayer.position.tile.x, 3);
	EXPECT_EQ(transitioningPlayer.position.tile.y, 4);

	ProjectedPlayerSnapshot restartTown = transition;
	restartTown.levelId = 0;
	restartTown.positionX = 75;
	restartTown.positionY = 68;
	restartTown.currentAction->kind = 6;
	restartTown.currentAction->targetEntityId = 0;
	Player restartedPlayer;

	ASSERT_TRUE(ApplyServerBackedPlayerSnapshot(restartedPlayer, restartTown).has_value());
	EXPECT_TRUE(restartedPlayer.isOnLevel(0));
	EXPECT_EQ(restartedPlayer._pmode, PM_NEWLVL);
	EXPECT_TRUE(restartedPlayer._pLvlChanging);
	EXPECT_EQ(restartedPlayer.position.tile.x, 75);
	EXPECT_EQ(restartedPlayer.position.tile.y, 68);

	transition.combatMode = static_cast<uint32_t>(PM_STAND);
	transition.combatModeEndTick = 0;
	transition.currentAction.reset();
	ASSERT_TRUE(ApplyServerBackedPlayerSnapshot(transitioningPlayer, transition).has_value());
	EXPECT_FALSE(transitioningPlayer._pLvlChanging);
}

TEST(ServerBackedPlayerSnapshot, AppliesMaterializedSetLevelIdentityToNativePlayer)
{
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.levelId = AuthoritativeRuntimeSetLevelIdBase + 1;
	snapshot.characterLevel = 1;

	Player player;
	ASSERT_TRUE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_TRUE(player.isOnLevel(SL_SKELKING));
}

TEST(ServerBackedPlayerSnapshot, RejectsMalformedSnapshotWithoutMutation)
{
	Player player;
	player._pGold = 42;
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.inventoryGrid.assign(InventoryGridCells, -1);
	snapshot.inventoryGrid[0] = 3;

	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_EQ(player._pGold, 42);
}

TEST(ServerBackedPlayerSnapshot, AcceptsOmittedLegacyInventoryGridAndClearsStaleCells)
{
	Player player;
	player.InvGrid[0] = 1;
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.characterLevel = 1;

	EXPECT_TRUE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_EQ(player.InvGrid[0], 0);
}

TEST(ServerBackedPlayerSnapshot, ClearsStaleCharacterNameWhenProjectionOmitsLegacyIdentity)
{
	Player player;
	player._pName[0] = 'S';
	player._pName[1] = 't';
	player._pName[2] = 'a';
	player._pName[3] = 'l';
	player._pName[4] = 'e';
	player._pName[5] = '\0';

	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.characterLevel = 1;

	ASSERT_TRUE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_STREQ(player._pName, "");
}

TEST(ServerBackedPlayerSnapshot, RejectsReflectChargesOutsideNativeRangeWithoutMutation)
{
	Player player;
	player.wReflections = 3;
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.reflectCharges = static_cast<uint32_t>(UINT16_MAX) + 1;
	snapshot.inventoryGrid.assign(InventoryGridCells, -1);

	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_EQ(player.wReflections, 3);
}

TEST(ServerBackedPlayerSnapshot, RejectsDiabloKillLevelOutsideNativeRangeWithoutMutation)
{
	Player player;
	player.pDiabloKillLevel = 2;
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.diabloKillLevel = static_cast<uint32_t>(UINT8_MAX) + 1;
	snapshot.inventoryGrid.assign(InventoryGridCells, -1);

	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_EQ(player.pDiabloKillLevel, 2);
}

TEST(ServerBackedPlayerSnapshot, RejectsLevelOutsideNativeRangeWithoutMutation)
{
	for (const uint32_t levelId : { static_cast<uint32_t>(NUMLEVELS), static_cast<uint32_t>(UINT8_MAX) + 1 }) {
		Player player;
		player.setLevel(2);
		ProjectedPlayerSnapshot snapshot;
		snapshot.entityId = 7;
		snapshot.levelId = levelId;
		snapshot.inventoryGrid.assign(InventoryGridCells, -1);

		EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
		EXPECT_TRUE(player.isOnLevel(2));
	}
}

TEST(ServerBackedPlayerSnapshot, RejectsInvalidVisitedLevelWithoutMutation)
{
	Player player;
	player._pLvlVisited[4] = true;
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.visitedLevelIds = { 4, NUMLEVELS };
	snapshot.inventoryGrid.assign(InventoryGridCells, -1);

	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_TRUE(player._pLvlVisited[4]);

	ProjectedPlayerSnapshot missingCurrent;
	missingCurrent.entityId = 7;
	missingCurrent.levelId = 0;
	missingCurrent.visitedLevelIds = { 4 };
	missingCurrent.inventoryGrid.assign(InventoryGridCells, -1);
	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, missingCurrent).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsDuplicateEquipmentSlots)
{
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.inventoryGrid.assign(InventoryGridCells, -1);
	snapshot.equipment.push_back({ .slot = INVLOC_HEAD });
	snapshot.equipment.push_back({ .slot = INVLOC_HEAD });

	Player player;
	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsUnknownCharacterClassWithoutMutation)
{
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.characterClassId = static_cast<uint32_t>(HeroClass::LAST) + 1;
	snapshot.inventoryGrid.assign(InventoryGridCells, -1);

	Player player;
	player._pClass = HeroClass::Rogue;
	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_EQ(player._pClass, HeroClass::Rogue);
}

TEST(ServerBackedPlayerSnapshot, RejectsInvalidMemorizedSpellWithoutMutation)
{
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.inventoryGrid.assign(InventoryGridCells, -1);
	snapshot.memorizedSpells.push_back({ static_cast<uint32_t>(SpellID::Firebolt), MaxSpellLevel + 1 });

	Player player;
	player._pRSpell = SpellID::Healing;
	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_EQ(player._pRSpell, SpellID::Healing);
}

TEST(ServerBackedPlayerSnapshot, RejectsInvalidTargetingMetadataWithoutMutation)
{
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.characterLevel = 1;
	snapshot.inventoryGrid.assign(InventoryGridCells, -1);

	Player player;
	player._pRSpell = SpellID::Healing;
	snapshot.abilitySpellTargetingKind = static_cast<ProjectedSpellTargetingKind>(99);
	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_EQ(player._pRSpell, SpellID::Healing);

	snapshot.abilitySpellTargetingKind = ProjectedSpellTargetingKind::Unspecified;
	snapshot.memorizedSpells.push_back({ static_cast<uint32_t>(SpellID::Firebolt), 1, static_cast<ProjectedSpellTargetingKind>(99) });
	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());

	snapshot.memorizedSpells.clear();
	snapshot.inventory.push_back({ .spellTargetingKind = static_cast<ProjectedSpellTargetingKind>(99) });
	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());

	snapshot.inventory.clear();
	snapshot.inventory.push_back({ .price = std::numeric_limits<uint32_t>::max() });
	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsDuplicatePlayerItemSeedsWithoutMutation)
{
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.characterLevel = 1;
	snapshot.inventoryGrid.assign(InventoryGridCells, -1);
	snapshot.inventory.push_back({ .itemSeed = 42 });
	snapshot.equipment.push_back({ .itemSeed = 42 });

	Player player;
	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsInvalidPlayerItemIdentityWithoutMutation)
{
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.characterLevel = 1;
	snapshot.inventoryGrid.assign(InventoryGridCells, -1);
	snapshot.inventory.push_back({ .itemSeed = 0 });

	Player player;
	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsInvalidOrDuplicateStatusEffectsWithoutMutation)
{
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.inventoryGrid.assign(InventoryGridCells, -1);
	snapshot.statusEffects.push_back({ .effectId = 9, .remainingTicks = 10, .behavior = 1 });
	snapshot.statusEffects.push_back({ .effectId = 9, .remainingTicks = 8 });

	Player player;
	player._pInfraFlag = true;
	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_TRUE(player._pInfraFlag);

	snapshot.statusEffects.resize(1);
	snapshot.statusEffects[0].magnitude = 1;
	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());

	snapshot.statusEffects[0] = { .effectId = 46, .remainingTicks = 10, .magnitude = 1, .behavior = 2 };
	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());

	snapshot.statusEffects[0] = { .effectId = 53, .remainingTicks = 10, .magnitude = 1, .behavior = 3 };
	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());

	snapshot.statusEffects[0] = { .effectId = 9, .remainingTicks = 10, .behavior = 4 };
	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
}

TEST(ServerBackedPlayerSnapshot, RejectsInvalidCombatModeWithoutMutation)
{
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.combatMode = static_cast<uint32_t>(PM_QUIT) + 1;
	snapshot.inventoryGrid.assign(InventoryGridCells, -1);

	Player player;
	player._pmode = PM_ATTACK;
	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_EQ(player._pmode, PM_ATTACK);
}

TEST(ServerBackedPlayerSnapshot, RejectsInvalidFacingDirectionWithoutMutation)
{
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.facingDirection = static_cast<uint32_t>(Direction::SouthEast) + 1;
	snapshot.inventoryGrid.assign(InventoryGridCells, -1);

	Player player;
	player.direction = Direction::West;
	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_EQ(player.direction, Direction::West);
}

} // namespace
} // namespace devilution::authoritative

#include <gtest/gtest.h>

#include <limits>
#include <string>

#include "devilution.pb.h"
#include "engine/cursor.h"
#include "game/missiles/missiles.hpp"
#include "game/levels/dungeon_common.h"
#include "game/spells/spells.hpp"
#include "network/authoritative/player_snapshot.hpp"
#include "network/authoritative/server_backed_player_ui.hpp"
#include "network/authoritative/server_backed_world_presentation.hpp"
#include "network/authoritative/server_backed_world_projection.hpp"
#include "tables/playerdat.hpp"
#include "ui/automap.h"

namespace devilution::authoritative {
namespace protocol = ::devilution::protocol::v1;
namespace {

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
	player->mutable_inventory_grid()->Add(0);
	player->mutable_inventory_grid()->Add(-1);
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
	EXPECT_EQ(projected->inventoryGrid, std::vector<int32_t>({ 0, -1 }));
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

	ApplyServerBackedEventBatch(player, events, 7);

	EXPECT_EQ(player.life.current, 70);
	EXPECT_EQ(player._pExperience, 40U);
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
}

TEST(ServerBackedPlayerSnapshot, ProjectsAndSortsAuthoritativeMonsters)
{
	protocol::Snapshot snapshot;
	auto *second = snapshot.add_monsters();
	second->set_entity_id(9);
	second->set_monster_id(12);
	second->set_is_unique(true);
	second->set_unique_identity("Defiler");
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
	second->set_moving(true);
	second->set_future_position_x(5);
	second->set_future_position_y(5);
	second->set_petrified(true);
	second->set_stone_curse_immune(true);
	const auto first = snapshot.add_monsters();
	first->set_entity_id(3);
	first->set_hit_points(0);
	first->set_max_hit_points(10);
	first->set_alive(false);

	auto projected = ProjectMonsterSnapshots(snapshot);
	ASSERT_TRUE(projected.has_value()) << projected.error();
	ASSERT_EQ(projected->size(), 2U);
	EXPECT_EQ((*projected)[0].entityId, 3U);
	EXPECT_FALSE((*projected)[0].alive);
	EXPECT_EQ((*projected)[1].entityId, 9U);
	EXPECT_EQ((*projected)[1].monsterId, 12U);
	EXPECT_TRUE((*projected)[1].isUnique);
	EXPECT_EQ((*projected)[1].uniqueIdentity, "Defiler");
	EXPECT_EQ((*projected)[1].hitPoints, 7);
	EXPECT_EQ((*projected)[1].hitPointsQ6, 432);
	EXPECT_EQ((*projected)[1].maxHitPointsQ6, 1280);
	EXPECT_EQ((*projected)[1].attackDamage, 11);
	EXPECT_EQ((*projected)[1].aggroRange, 6);
	EXPECT_EQ((*projected)[1].fireResistance, 25);
	EXPECT_EQ((*projected)[1].lightningResistance, -10);
	EXPECT_EQ((*projected)[1].magicResistance, 40);
	EXPECT_TRUE((*projected)[1].moving);
	EXPECT_EQ((*projected)[1].futurePositionX, 5);
	EXPECT_EQ((*projected)[1].futurePositionY, 5);
	EXPECT_TRUE((*projected)[1].petrified);
	EXPECT_TRUE((*projected)[1].stoneCurseImmune);
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
	auto *first = snapshot.add_objects();
	first->set_entity_id(10);
	first->set_object_id(1);
	first->set_level_id(1);
	first->set_position_x(4);
	first->set_position_y(4);

	auto projected = ProjectObjectSnapshots(snapshot);
	ASSERT_TRUE(projected.has_value());
	ASSERT_EQ(projected->size(), 2U);
	EXPECT_EQ((*projected)[0].entityId, 10U);
	EXPECT_EQ((*projected)[0].objectId, 1U);
	EXPECT_FALSE((*projected)[0].activated);
	EXPECT_EQ((*projected)[1].entityId, 20U);
	EXPECT_TRUE((*projected)[1].activated);
	EXPECT_EQ((*projected)[1].questId, 30U);
	EXPECT_EQ((*projected)[1].effectKind, static_cast<int32_t>(ProjectedObjectEffectKind::QuestSequence));
	EXPECT_EQ((*projected)[1].effectAmount, 2);
	EXPECT_EQ((*projected)[1].questTransitionKind, 6);
	EXPECT_EQ((*projected)[1].rewardKind, 2);
	EXPECT_EQ((*projected)[1].questSequenceFollowUpKind, 1);
	EXPECT_TRUE((*projected)[1].breakable);
	EXPECT_TRUE((*projected)[1].destroyed);
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
	EXPECT_FALSE(ValidateLevelGeometrySnapshots(invalidLayer).has_value());

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

TEST(ServerBackedPlayerSnapshot, RetainsRemotePlayersAsPresentationOnlyState)
{
	LoadPlayerDataFiles();
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
	remote.characterLevel = 7;
	remote.diabloKillLevel = 2;
	remote.reflectCharges = 17;
	remote.visitedLevelIds = { 4 };
	remote.statPoints = 9;
	remote.characterName = "Remote";
	remote.strength = { 12, 14 };
	remote.inventoryGrid.assign(InventoryGridCells, -1);

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
		EXPECT_EQ(player.getCharacterLevel(), 7);
		EXPECT_EQ(player.pDiabloKillLevel, 2);
		EXPECT_EQ(player.wReflections, 17);
		EXPECT_TRUE(player._pLvlVisited[4]);
		EXPECT_EQ(player._pStatPts, 9);
		EXPECT_EQ(player.attributes.strength.base, 12);
		EXPECT_EQ(player.attributes.strength.current, 14);
		EXPECT_STREQ(player._pName, "Remote");
	});
	EXPECT_EQ(visitedEntityId, remote.entityId);
	Player *remotePlayer = presentation.FindRemotePlayer(remote.entityId);
	ASSERT_NE(remotePlayer, nullptr);
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
	ASSERT_TRUE(projection.Apply({ }, { }, { }, { projectile }, 1).has_value());
	ServerBackedWorldPresentation presentation;
	ASSERT_TRUE(presentation.Apply(projection, 1).has_value());
	EXPECT_TRUE(Missiles.empty());
	presentation.Clear();
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
		if (family.behavior == 31)
			projectile.nativeVisual = 27; // NovaBall; explicit metadata is authoritative.

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

	ASSERT_TRUE(projection.Apply({ monster }, { item }, { object }, 1).has_value());
	EXPECT_EQ(projection.MonsterAt(4, 5), std::optional<uint32_t> { 40 });
	EXPECT_EQ(projection.MonsterAt(5, 5), std::optional<uint32_t> { 40 });
	EXPECT_FALSE(projection.MonsterAt(6, 5).has_value());
	EXPECT_FALSE(projection.WorldItemAt(4, 5).has_value());
	EXPECT_EQ(projection.ObjectAt(4, 5), std::optional<uint32_t>(30));
	EXPECT_FALSE(projection.QuestObjectAt(4, 5).has_value());
	EXPECT_FALSE(projection.AuthoritativeObjectAt(4, 5).has_value());
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
	ASSERT_TRUE(projection.Apply({ monster }, { }, { }, 1).has_value());
	ServerBackedWorldPresentation presentation;

	const auto result = presentation.Apply(projection, 1);

	ASSERT_FALSE(result.has_value());
	EXPECT_EQ(result.error(), "Authoritative moving monster destination cannot be represented by the native world tile type.");
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
	Item beltItem;
	beltItem._itype = ItemType::Misc;
	snapshot.belt.push_back({ .slot = 2, .itemSeed = 44, .item = beltItem });
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
	EXPECT_TRUE(CurrentAutomapManager.GetAutoMapShowItems());
	EXPECT_FALSE(player.friendlyMode);
	EXPECT_EQ(player._pmode, PM_BLOCK);
	EXPECT_EQ(player._pAblSpells, GetSpellBitmask(SpellID::Search));
	EXPECT_EQ(player._pMemSpells, GetSpellBitmask(SpellID::Firebolt));
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
	ASSERT_TRUE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_TRUE(player.HoldItem.isEmpty());
	EXPECT_FALSE(player._pInfraFlag);
	EXPECT_FALSE(CurrentAutomapManager.GetAutoMapShowItems());
	EXPECT_EQ(pcurs, CURSOR_HAND);
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
	EXPECT_EQ(restartedPlayer.position.tile.x, 75);
	EXPECT_EQ(restartedPlayer.position.tile.y, 68);
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

TEST(ServerBackedPlayerSnapshot, RejectsLevelOutsideNativeRangeWithoutMutation)
{
	Player player;
	player.setLevel(2);
	ProjectedPlayerSnapshot snapshot;
	snapshot.entityId = 7;
	snapshot.levelId = 256;
	snapshot.inventoryGrid.assign(InventoryGridCells, -1);

	EXPECT_FALSE(ApplyServerBackedPlayerSnapshot(player, snapshot).has_value());
	EXPECT_TRUE(player.isOnLevel(2));
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

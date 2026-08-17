#include <gtest/gtest.h>

#include "devilution.pb.h"
#include "network/authoritative/store_command.hpp"

namespace devilution::authoritative {
namespace protocol = ::devilution::protocol::v1;
namespace {

TEST(ServerBackedVendorCommand, BuildsOpenStoreIntent)
{
	auto command = MakeOpenStoreCommand(4, 19);
	ASSERT_TRUE(command.has_value()) << command.error();
	EXPECT_EQ(command->requested_tick(), 19U);
	ASSERT_TRUE(command->has_open_store_requested());
	EXPECT_EQ(command->open_store_requested().store_id(), 4U);
	EXPECT_EQ(command->client_sequence(), 0U);
}

TEST(ServerBackedVendorCommand, BuildsPurchaseIntentWithStableSlot)
{
	auto command = MakePurchaseCommand(4, 12, 21);
	ASSERT_TRUE(command.has_value()) << command.error();
	EXPECT_EQ(command->requested_tick(), 21U);
	ASSERT_TRUE(command->has_purchase_requested());
	EXPECT_EQ(command->purchase_requested().store_id(), 4U);
	EXPECT_EQ(command->purchase_requested().store_slot(), 12U);
}

TEST(ServerBackedVendorCommand, RejectsInvalidStoreIdentifier)
{
	EXPECT_FALSE(MakeOpenStoreCommand(0, 1).has_value());
	EXPECT_FALSE(MakePurchaseCommand(0, 0, 1).has_value());
}

TEST(ServerBackedVendorCommand, BuildsInventoryIntents)
{
	const auto sell = MakeSellItemCommand(2, 9);
	const auto repair = MakeRepairItemCommand(2, 9);
	const auto recharge = MakeRechargeItemCommand(2, 9);
	const auto identify = MakeIdentifyItemCommand(2, 9);
	const auto move = MakeMoveInventoryItemCommand(2, 4, 9);
	ASSERT_TRUE(sell.has_value());
	ASSERT_TRUE(repair.has_value());
	ASSERT_TRUE(recharge.has_value());
	ASSERT_TRUE(identify.has_value());
	ASSERT_TRUE(move.has_value());
	EXPECT_EQ(sell->intent_case(), protocol::Command::kSellItemRequested);
	EXPECT_EQ(repair->repair_item_requested().inventory_index(), 2U);
	EXPECT_EQ(recharge->recharge_item_requested().inventory_index(), 2U);
	EXPECT_EQ(identify->identify_item_requested().inventory_index(), 2U);
	EXPECT_EQ(move->move_inventory_item_requested().target_cell(), 4U);
}

TEST(ServerBackedVendorCommand, RejectsSentinelInventoryValues)
{
	EXPECT_FALSE(MakeSellItemCommand(UINT32_MAX, 1).has_value());
	EXPECT_FALSE(MakeMoveInventoryItemCommand(1, UINT32_MAX, 1).has_value());
}

TEST(ServerBackedVendorCommand, BuildsStableEquipmentBeltAndManaIntents)
{
	const ServerBackedItemReference belt { .location = ServerBackedItemLocation::Belt, .slot = 6, .stableItemId = 702 };
	const ServerBackedItemReference equipment { .location = ServerBackedItemLocation::Equipment, .slot = 3, .stableItemId = 701 };
	const auto sell = MakeSellItemCommand(belt, 10);
	const auto repair = MakeRepairItemCommand(equipment, 11);
	const auto refill = MakeRefillManaCommand(12);
	ASSERT_TRUE(sell.has_value());
	ASSERT_TRUE(repair.has_value());
	ASSERT_TRUE(refill.has_value());
	EXPECT_EQ(sell->sell_item_requested().item().location(), protocol::PLAYER_ITEM_LOCATION_BELT);
	EXPECT_EQ(sell->sell_item_requested().item().slot(), 6U);
	EXPECT_EQ(sell->sell_item_requested().item().stable_item_id(), 702U);
	EXPECT_EQ(repair->repair_item_requested().item().location(), protocol::PLAYER_ITEM_LOCATION_EQUIPMENT);
	EXPECT_EQ(repair->repair_item_requested().item().slot(), 3U);
	EXPECT_EQ(repair->repair_item_requested().item().stable_item_id(), 701U);
	EXPECT_EQ(refill->intent_case(), protocol::Command::kRefillManaRequested);
}

TEST(ServerBackedVendorCommand, BuildsStableReadBookIntent)
{
	const ServerBackedItemReference book { .location = ServerBackedItemLocation::Inventory, .slot = 8, .stableItemId = 2127 };
	const auto command = MakeReadBookCommand(book, 14);
	ASSERT_TRUE(command.has_value()) << command.error();
	ASSERT_TRUE(command->has_read_book_requested());
	EXPECT_EQ(command->requested_tick(), 14U);
	EXPECT_EQ(command->read_book_requested().item().location(), protocol::PLAYER_ITEM_LOCATION_INVENTORY);
	EXPECT_EQ(command->read_book_requested().item().slot(), 8U);
	EXPECT_EQ(command->read_book_requested().item().stable_item_id(), 2127U);
}

TEST(ServerBackedVendorCommand, DistinguishesClassAbilitiesFromVendorServices)
{
	const ServerBackedItemReference inventory { .location = ServerBackedItemLocation::Inventory, .slot = 2 };
	const auto identifyVendor = MakeIdentifyItemCommand(inventory, 10);
	const auto identifyAbility = MakeIdentifyItemCommand(
	    inventory,
	    ServerBackedItemServiceSourceKind::ClassAbility,
	    11);
	const auto repairVendor = MakeRepairItemCommand(inventory, 12);
	const auto repairAbility = MakeRepairItemCommand(
	    inventory,
	    ServerBackedItemServiceSourceKind::ClassAbility,
	    13);
	const auto rechargeVendor = MakeRechargeItemCommand(inventory, 14);
	const auto rechargeAbility = MakeRechargeItemCommand(
	    inventory,
	    ServerBackedItemServiceSourceKind::ClassAbility,
	    15);

	ASSERT_TRUE(identifyVendor.has_value()) << identifyVendor.error();
	ASSERT_TRUE(identifyAbility.has_value()) << identifyAbility.error();
	ASSERT_TRUE(repairVendor.has_value()) << repairVendor.error();
	ASSERT_TRUE(repairAbility.has_value()) << repairAbility.error();
	ASSERT_TRUE(rechargeVendor.has_value()) << rechargeVendor.error();
	ASSERT_TRUE(rechargeAbility.has_value()) << rechargeAbility.error();
	EXPECT_EQ(identifyVendor->identify_item_requested().source(), protocol::ITEM_SERVICE_SOURCE_KIND_UNSPECIFIED);
	EXPECT_EQ(identifyAbility->identify_item_requested().source(), protocol::ITEM_SERVICE_SOURCE_KIND_CLASS_ABILITY);
	EXPECT_EQ(repairVendor->repair_item_requested().source(), protocol::ITEM_SERVICE_SOURCE_KIND_UNSPECIFIED);
	EXPECT_EQ(repairAbility->repair_item_requested().source(), protocol::ITEM_SERVICE_SOURCE_KIND_CLASS_ABILITY);
	EXPECT_EQ(rechargeVendor->recharge_item_requested().source(), protocol::ITEM_SERVICE_SOURCE_KIND_UNSPECIFIED);
	EXPECT_EQ(rechargeAbility->recharge_item_requested().source(), protocol::ITEM_SERVICE_SOURCE_KIND_CLASS_ABILITY);
	EXPECT_EQ(repairAbility->repair_item_requested().item().location(), protocol::PLAYER_ITEM_LOCATION_INVENTORY);
	EXPECT_EQ(repairAbility->repair_item_requested().item().slot(), 2U);
	EXPECT_EQ(repairAbility->requested_tick(), 13U);
}

TEST(ServerBackedVendorCommand, BuildsExplicitInventoryToBeltTransfer)
{
	const ServerBackedItemReference inventory { .location = ServerBackedItemLocation::Inventory, .slot = 2 };
	const ServerBackedItemReference belt { .location = ServerBackedItemLocation::Belt, .slot = 5 };
	const auto command = MakeMoveItemCommand(inventory, belt, 17);
	ASSERT_TRUE(command.has_value()) << command.error();
	ASSERT_TRUE(command->has_move_item_requested());
	EXPECT_EQ(command->move_item_requested().item().location(), protocol::PLAYER_ITEM_LOCATION_INVENTORY);
	EXPECT_EQ(command->move_item_requested().item().slot(), 2U);
	EXPECT_EQ(command->move_item_requested().destination().location(), protocol::PLAYER_ITEM_LOCATION_BELT);
	EXPECT_EQ(command->move_item_requested().destination().slot(), 5U);
}

TEST(ServerBackedVendorCommand, BuildsStableInventoryMoveIntent)
{
	const ServerBackedItemReference inventory { .location = ServerBackedItemLocation::Inventory, .slot = 2, .stableItemId = 702 };
	const auto command = MakeMoveInventoryItemCommand(inventory, 4, 17);
	ASSERT_TRUE(command.has_value()) << command.error();
	ASSERT_TRUE(command->has_move_inventory_item_requested());
	EXPECT_EQ(command->move_inventory_item_requested().inventory_index(), 2U);
	EXPECT_EQ(command->move_inventory_item_requested().target_cell(), 4U);
	EXPECT_EQ(command->move_inventory_item_requested().item().location(), protocol::PLAYER_ITEM_LOCATION_INVENTORY);
	EXPECT_EQ(command->move_inventory_item_requested().item().slot(), 2U);
	EXPECT_EQ(command->move_inventory_item_requested().item().stable_item_id(), 702U);
}

TEST(ServerBackedVendorCommand, BuildsAuthoritativeGameplayIntents)
{
	const auto move = MakeMoveCommand(1, -1, 20);
	const auto attack = MakeAttackCommand(9, 21);
	const auto cast = MakeCastCommand(2, 7, 22);
	const auto portal = MakeUsePortalCommand(4, 23);
	const auto pickup = MakePickupWorldItemCommand(20, 24);
	const auto drop = MakeDropHeldItemCommand(-2, 5, 25);
	const auto object = MakeOperateObjectCommand(30, 25);
	const auto quest = MakeAdvanceQuestCommand(40, 26);
	const auto friendlyMode = MakeSetFriendlyModeCommand(false, 27);
	const auto restartTown = MakeRestartTownCommand(28);
	ASSERT_TRUE(move.has_value());
	ASSERT_TRUE(attack.has_value());
	ASSERT_TRUE(cast.has_value());
	ASSERT_TRUE(portal.has_value());
	ASSERT_TRUE(pickup.has_value());
	ASSERT_TRUE(drop.has_value());
	ASSERT_TRUE(object.has_value());
	ASSERT_TRUE(quest.has_value());
	ASSERT_TRUE(friendlyMode.has_value());
	ASSERT_TRUE(restartTown.has_value());
	EXPECT_EQ(move->move_requested().direction_x(), 1);
	EXPECT_EQ(move->move_requested().direction_y(), -1);
	EXPECT_EQ(attack->attack_requested().target_entity_id(), 9U);
	EXPECT_EQ(cast->cast_requested().spell_id(), 2U);
	EXPECT_EQ(portal->use_portal_requested().portal_id(), 4U);
	EXPECT_EQ(pickup->pickup_world_item_requested().item_entity_id(), 20U);
	EXPECT_EQ(drop->requested_tick(), 25U);
	EXPECT_EQ(drop->drop_held_item_requested().target_x(), -2);
	EXPECT_EQ(drop->drop_held_item_requested().target_y(), 5);
	EXPECT_EQ(object->operate_object_requested().object_entity_id(), 30U);
	EXPECT_EQ(quest->advance_quest_requested().quest_id(), 40U);
	EXPECT_TRUE(friendlyMode->has_set_friendly_mode_requested());
	EXPECT_FALSE(friendlyMode->set_friendly_mode_requested().friendly_mode());
	EXPECT_EQ(restartTown->requested_tick(), 28U);
	EXPECT_TRUE(restartTown->has_restart_town_requested());
}

TEST(ServerBackedVendorCommand, BuildsCellTargetedCastIntent)
{
	const auto cast = MakeCastCommand(7, 0, 4, 5, 22);
	ASSERT_TRUE(cast.has_value()) << cast.error();
	EXPECT_EQ(cast->cast_requested().spell_id(), 7U);
	EXPECT_EQ(cast->cast_requested().target_entity_id(), 0U);
	EXPECT_EQ(cast->cast_requested().target_x(), 4);
	EXPECT_EQ(cast->cast_requested().target_y(), 5);
}

TEST(ServerBackedVendorCommand, BuildsSourceAwareCastIntents)
{
	const auto memorized = MakeCastCommand(
	    1,
	    0,
	    4,
	    5,
	    { ServerBackedCastSourceKind::MemorizedSpell, std::nullopt },
	    22);
	const auto charged = MakeCastCommand(
	    30,
	    0,
	    4,
	    5,
	    {
	        ServerBackedCastSourceKind::ChargedItem,
	        ServerBackedItemReference { ServerBackedItemLocation::Equipment, 4 },
	    },
	    23);

	ASSERT_TRUE(memorized.has_value()) << memorized.error();
	EXPECT_EQ(memorized->cast_requested().source(), protocol::CAST_SOURCE_KIND_MEMORIZED_SPELL);
	EXPECT_FALSE(memorized->cast_requested().has_source_item());
	ASSERT_TRUE(charged.has_value()) << charged.error();
	EXPECT_EQ(charged->cast_requested().source(), protocol::CAST_SOURCE_KIND_CHARGED_ITEM);
	EXPECT_EQ(charged->cast_requested().source_item().location(), protocol::PLAYER_ITEM_LOCATION_EQUIPMENT);
	EXPECT_EQ(charged->cast_requested().source_item().slot(), 4U);
}

TEST(ServerBackedVendorCommand, RejectsInvalidSourceItemCombinations)
{
	const auto itemlessCharge = MakeCastCommand(
	    30,
	    0,
	    0,
	    0,
	    { ServerBackedCastSourceKind::ChargedItem, std::nullopt },
	    22);
	const auto itemBackedAbility = MakeCastCommand(
	    26,
	    0,
	    0,
	    0,
	    {
	        ServerBackedCastSourceKind::Ability,
	        ServerBackedItemReference { ServerBackedItemLocation::Equipment, 4 },
	    },
	    23);

	EXPECT_FALSE(itemlessCharge.has_value());
	EXPECT_FALSE(itemBackedAbility.has_value());
}

} // namespace
} // namespace devilution::authoritative

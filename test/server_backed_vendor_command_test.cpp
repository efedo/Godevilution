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

TEST(ServerBackedVendorCommand, BuildsCloseStoreIntent)
{
	const auto command = MakeCloseStoreCommand(20);
	ASSERT_TRUE(command.has_value()) << command.error();
	EXPECT_EQ(command->requested_tick(), 20U);
	EXPECT_TRUE(command->has_close_store_requested());
	EXPECT_EQ(command->intent_case(), protocol::Command::kCloseStoreRequested);
}

TEST(ServerBackedVendorCommand, BuildsStashSessionAndGoldIntents)
{
	const auto open = MakeOpenStashCommand(21);
	const auto close = MakeCloseStashCommand(22);
	const auto deposit = MakeDepositStashGoldCommand(300, 23);
	const auto withdraw = MakeWithdrawStashGoldCommand(125, 24);
	const auto sort = MakeSortInventoryCommand(25);
	const ServerBackedItemReference gold { .location = ServerBackedItemLocation::Inventory, .slot = 3, .stableItemId = 701 };
	const auto split = MakeSplitGoldCommand(gold, 75, 26);
	ASSERT_TRUE(open.has_value());
	ASSERT_TRUE(close.has_value());
	ASSERT_TRUE(deposit.has_value());
	ASSERT_TRUE(withdraw.has_value());
	ASSERT_TRUE(sort.has_value());
	ASSERT_TRUE(split.has_value());
	EXPECT_EQ(open->intent_case(), protocol::Command::kOpenStashRequested);
	EXPECT_EQ(close->intent_case(), protocol::Command::kCloseStashRequested);
	EXPECT_EQ(deposit->deposit_stash_gold_requested().amount(), 300U);
	EXPECT_EQ(withdraw->withdraw_stash_gold_requested().amount(), 125U);
	EXPECT_EQ(sort->intent_case(), protocol::Command::kSortInventoryRequested);
	EXPECT_EQ(split->intent_case(), protocol::Command::kSplitGoldRequested);
	EXPECT_EQ(split->split_gold_requested().item().slot(), 3U);
	EXPECT_EQ(split->split_gold_requested().item().stable_item_id(), 701U);
	EXPECT_EQ(split->split_gold_requested().amount(), 75U);
	EXPECT_FALSE(MakeDepositStashGoldCommand(0, 1).has_value());
	EXPECT_FALSE(MakeWithdrawStashGoldCommand(0, 1).has_value());
	EXPECT_FALSE(MakeSplitGoldCommand(gold, 0, 1).has_value());
	EXPECT_FALSE(MakeSplitGoldCommand(
	    ServerBackedItemReference { .location = ServerBackedItemLocation::Stash, .slot = 1, .page = 0 },
	    1,
	    1)
	                 .has_value());
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

TEST(ServerBackedVendorCommand, BuildsCellTargetedAttackIntent)
{
	const auto command = MakeAttackAtCommand(-7, 12, 44);
	ASSERT_TRUE(command.has_value()) << command.error();
	ASSERT_TRUE(command->has_attack_requested());
	EXPECT_TRUE(command->attack_requested().target_cell());
	EXPECT_EQ(command->attack_requested().target_x(), -7);
	EXPECT_EQ(command->attack_requested().target_y(), 12);
	EXPECT_EQ(command->requested_tick(), 44U);
	EXPECT_EQ(command->attack_requested().target_entity_id(), 0U);
}

TEST(ServerBackedVendorCommand, BuildsAttributeAllocationIntent)
{
	const auto command = MakeAllocateAttributeCommand(ServerBackedPlayerAttribute::Vitality, 5, 23);
	ASSERT_TRUE(command.has_value()) << command.error();
	ASSERT_TRUE(command->has_allocate_attribute_requested());
	EXPECT_EQ(command->requested_tick(), 23U);
	EXPECT_EQ(command->allocate_attribute_requested().attribute(), protocol::PLAYER_ATTRIBUTE_VITALITY);
	EXPECT_EQ(command->allocate_attribute_requested().amount(), 5U);
	EXPECT_FALSE(MakeAllocateAttributeCommand(ServerBackedPlayerAttribute::Strength, 0, 23).has_value());
}

TEST(ServerBackedVendorCommand, RejectsSentinelInventoryValues)
{
	EXPECT_FALSE(MakeSellItemCommand(UINT32_MAX, 1).has_value());
	EXPECT_FALSE(MakeMoveInventoryItemCommand(1, UINT32_MAX, 1).has_value());
}

TEST(ServerBackedVendorCommand, RejectsInvalidQuestTurnInIdentifiers)
{
	EXPECT_FALSE(MakeTurnInQuestCommand(0, 0, 1).has_value());
	EXPECT_FALSE(MakeTurnInQuestCommand(3011, UINT8_MAX + 1U, 1).has_value());
}

TEST(ServerBackedVendorCommand, RejectsInvalidTrapIdentifiers)
{
	EXPECT_FALSE(MakeDisarmTrapCommand(0, 1).has_value());
}

TEST(ServerBackedVendorCommand, BuildsQuestDialogueIntent)
{
	const auto command = MakeTalkToQuestCommand(3021, 44, 17);
	ASSERT_TRUE(command.has_value()) << command.error();
	ASSERT_TRUE(command->has_talk_to_quest_requested());
	EXPECT_EQ(command->requested_tick(), 17U);
	EXPECT_EQ(command->talk_to_quest_requested().quest_id(), 3021U);
	EXPECT_EQ(command->talk_to_quest_requested().target_entity_id(), 44U);
}

TEST(ServerBackedVendorCommand, RejectsInvalidQuestDialogueIdentifiers)
{
	EXPECT_FALSE(MakeTalkToQuestCommand(0, 44, 1).has_value());
	EXPECT_FALSE(MakeTalkToQuestCommand(3021, 0, 1).has_value());
}

TEST(ServerBackedVendorCommand, BuildsTownerGreetingIntent)
{
	const auto command = MakeTalkToTownerCommand(7, 19);
	ASSERT_TRUE(command.has_value()) << command.error();
	ASSERT_TRUE(command->has_talk_to_towner_requested());
	EXPECT_EQ(command->requested_tick(), 19U);
	EXPECT_EQ(command->talk_to_towner_requested().towner_id(), 7U);
	EXPECT_FALSE(MakeTalkToTownerCommand(UINT8_MAX + 1U, 19).has_value());
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

TEST(ServerBackedVendorCommand, BuildsStableUseConsumableIntent)
{
	const ServerBackedItemReference potion { .location = ServerBackedItemLocation::Belt, .slot = 3, .stableItemId = 2185 };
	const auto command = MakeUseConsumableCommand(potion, 15);
	ASSERT_TRUE(command.has_value()) << command.error();
	ASSERT_TRUE(command->has_use_consumable_requested());
	EXPECT_EQ(command->requested_tick(), 15U);
	EXPECT_EQ(command->use_consumable_requested().item().location(), protocol::PLAYER_ITEM_LOCATION_BELT);
	EXPECT_EQ(command->use_consumable_requested().item().slot(), 3U);
	EXPECT_EQ(command->use_consumable_requested().item().stable_item_id(), 2185U);
}

TEST(ServerBackedVendorCommand, DistinguishesClassAbilitiesFromVendorServices)
{
	const ServerBackedItemReference inventory { .location = ServerBackedItemLocation::Inventory, .slot = 2 };
	const ServerBackedItemReference stash { .location = ServerBackedItemLocation::Stash, .slot = 13, .stableItemId = 704, .page = 2 };
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
	const auto stashRepairAbility = MakeRepairItemCommand(
	    stash,
	    ServerBackedItemServiceSourceKind::ClassAbility,
	    16);

	ASSERT_TRUE(identifyVendor.has_value()) << identifyVendor.error();
	ASSERT_TRUE(identifyAbility.has_value()) << identifyAbility.error();
	ASSERT_TRUE(repairVendor.has_value()) << repairVendor.error();
	ASSERT_TRUE(repairAbility.has_value()) << repairAbility.error();
	ASSERT_TRUE(rechargeVendor.has_value()) << rechargeVendor.error();
	ASSERT_TRUE(rechargeAbility.has_value()) << rechargeAbility.error();
	ASSERT_TRUE(stashRepairAbility.has_value()) << stashRepairAbility.error();
	EXPECT_EQ(identifyVendor->identify_item_requested().source(), protocol::ITEM_SERVICE_SOURCE_KIND_UNSPECIFIED);
	EXPECT_EQ(identifyAbility->identify_item_requested().source(), protocol::ITEM_SERVICE_SOURCE_KIND_CLASS_ABILITY);
	EXPECT_EQ(repairVendor->repair_item_requested().source(), protocol::ITEM_SERVICE_SOURCE_KIND_UNSPECIFIED);
	EXPECT_EQ(repairAbility->repair_item_requested().source(), protocol::ITEM_SERVICE_SOURCE_KIND_CLASS_ABILITY);
	EXPECT_EQ(rechargeVendor->recharge_item_requested().source(), protocol::ITEM_SERVICE_SOURCE_KIND_UNSPECIFIED);
	EXPECT_EQ(rechargeAbility->recharge_item_requested().source(), protocol::ITEM_SERVICE_SOURCE_KIND_CLASS_ABILITY);
	EXPECT_EQ(repairAbility->repair_item_requested().item().location(), protocol::PLAYER_ITEM_LOCATION_INVENTORY);
	EXPECT_EQ(repairAbility->repair_item_requested().item().slot(), 2U);
	EXPECT_EQ(repairAbility->requested_tick(), 13U);
	EXPECT_EQ(stashRepairAbility->repair_item_requested().item().location(), protocol::PLAYER_ITEM_LOCATION_STASH);
	EXPECT_EQ(stashRepairAbility->repair_item_requested().item().slot(), 13U);
	EXPECT_EQ(stashRepairAbility->repair_item_requested().item().page(), 2U);
	EXPECT_EQ(stashRepairAbility->repair_item_requested().item().stable_item_id(), 704U);
}

TEST(ServerBackedVendorCommand, BuildsExplicitInventoryToBeltTransfer)
{
	const ServerBackedItemReference inventory { .location = ServerBackedItemLocation::Inventory, .slot = 2, .stableItemId = 702 };
	const ServerBackedItemReference belt { .location = ServerBackedItemLocation::Belt, .slot = 5, .stableItemId = 703 };
	const auto command = MakeMoveItemCommand(inventory, belt, 17);
	ASSERT_TRUE(command.has_value()) << command.error();
	ASSERT_TRUE(command->has_move_item_requested());
	EXPECT_EQ(command->move_item_requested().item().location(), protocol::PLAYER_ITEM_LOCATION_INVENTORY);
	EXPECT_EQ(command->move_item_requested().item().slot(), 2U);
	EXPECT_EQ(command->move_item_requested().item().stable_item_id(), 702U);
	EXPECT_EQ(command->move_item_requested().destination().location(), protocol::PLAYER_ITEM_LOCATION_BELT);
	EXPECT_EQ(command->move_item_requested().destination().slot(), 5U);
	EXPECT_EQ(command->move_item_requested().destination().stable_item_id(), 703U);
}

TEST(ServerBackedVendorCommand, BuildsStashItemReferenceWithPage)
{
	const ServerBackedItemReference stash {
		.location = ServerBackedItemLocation::Stash,
		.slot = 37,
		.stableItemId = 704,
		.page = 12,
	};
	const ServerBackedItemReference inventory {
		.location = ServerBackedItemLocation::Inventory,
		.slot = 2,
		.stableItemId = 702,
	};
	const auto command = MakeMoveItemCommand(stash, inventory, 25);
	ASSERT_TRUE(command.has_value()) << command.error();
	EXPECT_EQ(command->move_item_requested().item().location(), protocol::PLAYER_ITEM_LOCATION_STASH);
	EXPECT_EQ(command->move_item_requested().item().page(), 12U);
	EXPECT_EQ(command->move_item_requested().item().slot(), 37U);
	EXPECT_EQ(command->move_item_requested().destination().location(), protocol::PLAYER_ITEM_LOCATION_INVENTORY);
	EXPECT_FALSE(MakeMoveItemCommand(
		ServerBackedItemReference { .location = ServerBackedItemLocation::Stash, .slot = 100, .page = 12 },
		inventory,
		25)
		.has_value());
}

TEST(ServerBackedVendorCommand, BuildsStashDestinationReferenceWithPage)
{
	const ServerBackedItemReference inventory {
		.location = ServerBackedItemLocation::Inventory,
		.slot = 2,
		.stableItemId = 702,
	};
	const ServerBackedItemReference stash {
		.location = ServerBackedItemLocation::Stash,
		.slot = 37,
		.stableItemId = 0,
		.page = 12,
	};
	const auto command = MakeMoveItemCommand(inventory, stash, 25);
	ASSERT_TRUE(command.has_value()) << command.error();
	EXPECT_EQ(command->move_item_requested().item().location(), protocol::PLAYER_ITEM_LOCATION_INVENTORY);
	EXPECT_EQ(command->move_item_requested().destination().location(), protocol::PLAYER_ITEM_LOCATION_STASH);
	EXPECT_EQ(command->move_item_requested().destination().page(), 12U);
	EXPECT_EQ(command->move_item_requested().destination().slot(), 37U);
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

TEST(ServerBackedVendorCommand, BuildsStableAutoEquipIntent)
{
	const ServerBackedItemReference inventory { .location = ServerBackedItemLocation::Inventory, .slot = 2, .stableItemId = 702 };
	const auto command = MakeAutoEquipItemCommand(inventory, 17);
	ASSERT_TRUE(command.has_value()) << command.error();
	ASSERT_TRUE(command->has_auto_equip_item_requested());
	EXPECT_EQ(command->auto_equip_item_requested().item().location(), protocol::PLAYER_ITEM_LOCATION_INVENTORY);
	EXPECT_EQ(command->auto_equip_item_requested().item().slot(), 2U);
	EXPECT_EQ(command->auto_equip_item_requested().item().stable_item_id(), 702U);
	EXPECT_FALSE(command->auto_equip_item_requested().has_equipment_slot());
	EXPECT_EQ(command->requested_tick(), 17U);

	const auto targeted = MakeAutoEquipItemCommand(inventory, 5, 18);
	ASSERT_TRUE(targeted.has_value()) << targeted.error();
	ASSERT_TRUE(targeted->has_auto_equip_item_requested());
	EXPECT_TRUE(targeted->auto_equip_item_requested().has_equipment_slot());
	EXPECT_EQ(targeted->auto_equip_item_requested().equipment_slot(), 5U);
	EXPECT_EQ(targeted->requested_tick(), 18U);
}

TEST(ServerBackedVendorCommand, BuildsAuthoritativeGameplayIntents)
{
	const auto move = MakeMoveCommand(1, -1, 20);
	const auto moveTo = MakeMoveToCommand(4, 5, 20);
	const auto attack = MakeAttackCommand(9, 21);
	const auto cast = MakeCastCommand(2, 7, 22);
	const auto portal = MakeUsePortalCommand(4, 23);
	const auto pickup = MakePickupWorldItemCommand(20, 24);
	const auto drop = MakeDropHeldItemCommand(-2, 5, 25);
	const auto object = MakeOperateObjectCommand(30, 25);
	const auto disarm = MakeDisarmTrapCommand(31, 26);
	const auto quest = MakeAdvanceQuestCommand(40, 26);
	const auto talkToQuest = MakeTalkToQuestCommand(3021, 44, 27);
	const auto turnIn = MakeTurnInQuestCommand(3011, 0, 27);
	const auto friendlyMode = MakeSetFriendlyModeCommand(false, 27);
	const auto restartTown = MakeRestartTownCommand(28);
	ASSERT_TRUE(move.has_value());
	ASSERT_TRUE(moveTo.has_value());
	ASSERT_TRUE(attack.has_value());
	ASSERT_TRUE(cast.has_value());
	ASSERT_TRUE(portal.has_value());
	ASSERT_TRUE(pickup.has_value());
	ASSERT_TRUE(drop.has_value());
	ASSERT_TRUE(object.has_value());
	ASSERT_TRUE(disarm.has_value());
	ASSERT_TRUE(quest.has_value());
	ASSERT_TRUE(talkToQuest.has_value());
	ASSERT_TRUE(turnIn.has_value());
	ASSERT_TRUE(friendlyMode.has_value());
	ASSERT_TRUE(restartTown.has_value());
	EXPECT_EQ(move->move_requested().direction_x(), 1);
	EXPECT_EQ(move->move_requested().direction_y(), -1);
	EXPECT_TRUE(moveTo->move_requested().target_cell());
	EXPECT_EQ(moveTo->move_requested().target_x(), 4);
	EXPECT_EQ(moveTo->move_requested().target_y(), 5);
	EXPECT_EQ(attack->attack_requested().target_entity_id(), 9U);
	EXPECT_EQ(cast->cast_requested().spell_id(), 2U);
	EXPECT_EQ(portal->use_portal_requested().portal_id(), 4U);
	EXPECT_EQ(pickup->pickup_world_item_requested().item_entity_id(), 20U);
	EXPECT_EQ(drop->requested_tick(), 25U);
	EXPECT_EQ(drop->drop_held_item_requested().target_x(), -2);
	EXPECT_EQ(drop->drop_held_item_requested().target_y(), 5);
	EXPECT_EQ(object->operate_object_requested().object_entity_id(), 30U);
	EXPECT_EQ(disarm->disarm_trap_requested().object_entity_id(), 31U);
	EXPECT_EQ(quest->advance_quest_requested().quest_id(), 40U);
	EXPECT_EQ(talkToQuest->talk_to_quest_requested().quest_id(), 3021U);
	EXPECT_EQ(talkToQuest->talk_to_quest_requested().target_entity_id(), 44U);
	EXPECT_EQ(turnIn->turn_in_quest_requested().quest_id(), 3011U);
	EXPECT_EQ(turnIn->turn_in_quest_requested().towner_id(), 0U);
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

TEST(ServerBackedVendorCommand, BuildsStableWorldDropIntent)
{
	const ServerBackedItemReference inventory {
		.location = ServerBackedItemLocation::Inventory,
		.slot = 3,
		.stableItemId = 702,
	};
	const auto command = MakeDropItemCommand(inventory, 8, 9, 31);
	ASSERT_TRUE(command.has_value()) << command.error();
	ASSERT_TRUE(command->has_drop_item_requested());
	const auto &request = command->drop_item_requested();
	EXPECT_EQ(command->requested_tick(), 31U);
	EXPECT_EQ(request.target_x(), 8);
	EXPECT_EQ(request.target_y(), 9);
	EXPECT_EQ(request.item().location(), protocol::PLAYER_ITEM_LOCATION_INVENTORY);
	EXPECT_EQ(request.item().slot(), 3U);
	EXPECT_EQ(request.item().stable_item_id(), 702U);
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
	const auto consumable = MakeCastCommand(
	    32,
	    0,
	    4,
	    5,
	    {
	        ServerBackedCastSourceKind::ConsumableItem,
	        ServerBackedItemReference { ServerBackedItemLocation::Belt, 3, 2116 },
	    },
	    24);
	const auto healOther = MakeCastCommand(
	    34,
	    12,
	    8,
	    9,
	    { ServerBackedCastSourceKind::MemorizedSpell, std::nullopt },
	    25);

	ASSERT_TRUE(memorized.has_value()) << memorized.error();
	EXPECT_EQ(memorized->cast_requested().source(), protocol::CAST_SOURCE_KIND_MEMORIZED_SPELL);
	EXPECT_FALSE(memorized->cast_requested().has_source_item());
	ASSERT_TRUE(charged.has_value()) << charged.error();
	EXPECT_EQ(charged->cast_requested().source(), protocol::CAST_SOURCE_KIND_CHARGED_ITEM);
	EXPECT_EQ(charged->cast_requested().source_item().location(), protocol::PLAYER_ITEM_LOCATION_EQUIPMENT);
	EXPECT_EQ(charged->cast_requested().source_item().slot(), 4U);
	ASSERT_TRUE(consumable.has_value()) << consumable.error();
	EXPECT_EQ(consumable->cast_requested().source(), protocol::CAST_SOURCE_KIND_CONSUMABLE_ITEM);
	EXPECT_EQ(consumable->cast_requested().source_item().location(), protocol::PLAYER_ITEM_LOCATION_BELT);
	EXPECT_EQ(consumable->cast_requested().source_item().slot(), 3U);
	EXPECT_EQ(consumable->cast_requested().source_item().stable_item_id(), 2116U);
	ASSERT_TRUE(healOther.has_value()) << healOther.error();
	EXPECT_EQ(healOther->cast_requested().spell_id(), 34U);
	EXPECT_EQ(healOther->cast_requested().target_entity_id(), 12U);
	EXPECT_EQ(healOther->cast_requested().source(), protocol::CAST_SOURCE_KIND_MEMORIZED_SPELL);
	EXPECT_FALSE(healOther->cast_requested().has_source_item());
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

/**
 * @file network/authoritative/store_command.cpp
 *
 * Server-backed vendor intent construction at the Protobuf boundary.
 */

#include "network/authoritative/store_command.hpp"

#include "devilution.pb.h"

namespace devilution::authoritative {

tl::expected<protocol::v1::PlayerItemReference, std::string> MakeItemReference(ServerBackedItemReference item)
{
	if (item.slot == UINT32_MAX)
		return tl::make_unexpected("Cannot reference an invalid server-backed item slot.");
	protocol::v1::PlayerItemReference reference;
	reference.set_slot(item.slot);
	reference.set_stable_item_id(item.stableItemId);
	switch (item.location) {
	case ServerBackedItemLocation::Inventory:
		reference.set_location(protocol::v1::PLAYER_ITEM_LOCATION_INVENTORY);
		break;
	case ServerBackedItemLocation::Belt:
		reference.set_location(protocol::v1::PLAYER_ITEM_LOCATION_BELT);
		break;
	case ServerBackedItemLocation::Equipment:
		reference.set_location(protocol::v1::PLAYER_ITEM_LOCATION_EQUIPMENT);
		break;
	case ServerBackedItemLocation::Stash:
		if (item.page >= 100 || item.slot >= 100)
			return tl::make_unexpected("Cannot reference a stash item outside the native page grid.");
		reference.set_location(protocol::v1::PLAYER_ITEM_LOCATION_STASH);
		reference.set_page(item.page);
		break;
	default:
		return tl::make_unexpected("The server-backed item location is invalid.");
	}
	return reference;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeOpenStoreCommand(uint32_t storeId, uint64_t requestedTick)
{
	if (storeId == 0)
		return tl::make_unexpected("Cannot open an invalid server-backed vendor.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_open_store_requested()->set_store_id(storeId);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeCloseStoreCommand(uint64_t requestedTick)
{
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_close_store_requested();
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeOpenStashCommand(uint64_t requestedTick)
{
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_open_stash_requested();
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeCloseStashCommand(uint64_t requestedTick)
{
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_close_stash_requested();
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeDepositStashGoldCommand(uint32_t amount, uint64_t requestedTick)
{
	if (amount == 0)
		return tl::make_unexpected("Cannot deposit zero stash gold.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_deposit_stash_gold_requested()->set_amount(amount);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeWithdrawStashGoldCommand(uint32_t amount, uint64_t requestedTick)
{
	if (amount == 0)
		return tl::make_unexpected("Cannot withdraw zero stash gold.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_withdraw_stash_gold_requested()->set_amount(amount);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeSplitGoldCommand(ServerBackedItemReference item, uint32_t amount, uint64_t requestedTick)
{
	if (amount == 0)
		return tl::make_unexpected("Cannot split zero gold.");
	const auto reference = MakeItemReference(item);
	if (!reference.has_value())
		return tl::make_unexpected(reference.error());
	if (item.location != ServerBackedItemLocation::Inventory
	    && item.location != ServerBackedItemLocation::Belt)
		return tl::make_unexpected("Gold can only be split from inventory or belt.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_split_gold_requested()->mutable_item()->CopyFrom(*reference);
	command.mutable_split_gold_requested()->set_amount(amount);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeSortInventoryCommand(uint64_t requestedTick)
{
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_sort_inventory_requested();
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakePurchaseCommand(uint32_t storeId, uint32_t storeSlot, uint64_t requestedTick)
{
	if (storeId == 0)
		return tl::make_unexpected("Cannot purchase from an invalid server-backed vendor.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	auto *request = command.mutable_purchase_requested();
	request->set_store_id(storeId);
	request->set_store_slot(storeSlot);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeSellItemCommand(uint32_t inventoryIndex, uint64_t requestedTick)
{
	if (inventoryIndex == UINT32_MAX)
		return tl::make_unexpected("Cannot sell an invalid inventory index.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_sell_item_requested()->set_inventory_index(inventoryIndex);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeRepairItemCommand(uint32_t inventoryIndex, uint64_t requestedTick)
{
	if (inventoryIndex == UINT32_MAX)
		return tl::make_unexpected("Cannot repair an invalid inventory index.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_repair_item_requested()->set_inventory_index(inventoryIndex);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeRechargeItemCommand(uint32_t inventoryIndex, uint64_t requestedTick)
{
	if (inventoryIndex == UINT32_MAX)
		return tl::make_unexpected("Cannot recharge an invalid inventory index.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_recharge_item_requested()->set_inventory_index(inventoryIndex);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeIdentifyItemCommand(uint32_t inventoryIndex, uint64_t requestedTick)
{
	if (inventoryIndex == UINT32_MAX)
		return tl::make_unexpected("Cannot identify an invalid inventory index.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_identify_item_requested()->set_inventory_index(inventoryIndex);
	return command;
}

template <typename Request>
tl::expected<::devilution::protocol::v1::Command, std::string> MakeItemCommand(ServerBackedItemReference item, uint64_t requestedTick, Request request)
{
	auto reference = MakeItemReference(item);
	if (!reference.has_value())
		return tl::make_unexpected(reference.error());
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	request(command)->mutable_item()->CopyFrom(*reference);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeSellItemCommand(ServerBackedItemReference item, uint64_t requestedTick)
{
	return MakeItemCommand(item, requestedTick, [](protocol::v1::Command &command) { return command.mutable_sell_item_requested(); });
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeRepairItemCommand(ServerBackedItemReference item, uint64_t requestedTick)
{
	return MakeRepairItemCommand(item, ServerBackedItemServiceSourceKind::Vendor, requestedTick);
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeRepairItemCommand(ServerBackedItemReference item, ServerBackedItemServiceSourceKind source, uint64_t requestedTick)
{
	auto command = MakeItemCommand(item, requestedTick, [](protocol::v1::Command &value) { return value.mutable_repair_item_requested(); });
	if (!command.has_value())
		return command;
	switch (source) {
	case ServerBackedItemServiceSourceKind::Vendor:
		command->mutable_repair_item_requested()->set_source(protocol::v1::ITEM_SERVICE_SOURCE_KIND_UNSPECIFIED);
		break;
	case ServerBackedItemServiceSourceKind::ClassAbility:
		command->mutable_repair_item_requested()->set_source(protocol::v1::ITEM_SERVICE_SOURCE_KIND_CLASS_ABILITY);
		break;
	default:
		return tl::make_unexpected("The server-backed item-service source is invalid.");
	}
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeRechargeItemCommand(ServerBackedItemReference item, uint64_t requestedTick)
{
	return MakeRechargeItemCommand(item, ServerBackedItemServiceSourceKind::Vendor, requestedTick);
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeRechargeItemCommand(ServerBackedItemReference item, ServerBackedItemServiceSourceKind source, uint64_t requestedTick)
{
	auto command = MakeItemCommand(item, requestedTick, [](protocol::v1::Command &value) { return value.mutable_recharge_item_requested(); });
	if (!command.has_value())
		return command;
	switch (source) {
	case ServerBackedItemServiceSourceKind::Vendor:
		command->mutable_recharge_item_requested()->set_source(protocol::v1::ITEM_SERVICE_SOURCE_KIND_UNSPECIFIED);
		break;
	case ServerBackedItemServiceSourceKind::ClassAbility:
		command->mutable_recharge_item_requested()->set_source(protocol::v1::ITEM_SERVICE_SOURCE_KIND_CLASS_ABILITY);
		break;
	default:
		return tl::make_unexpected("The server-backed item-service source is invalid.");
	}
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeIdentifyItemCommand(ServerBackedItemReference item, uint64_t requestedTick)
{
	return MakeIdentifyItemCommand(item, ServerBackedItemServiceSourceKind::Vendor, requestedTick);
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeIdentifyItemCommand(ServerBackedItemReference item, ServerBackedItemServiceSourceKind source, uint64_t requestedTick)
{
	auto command = MakeItemCommand(item, requestedTick, [](protocol::v1::Command &value) { return value.mutable_identify_item_requested(); });
	if (!command.has_value())
		return command;
	switch (source) {
	case ServerBackedItemServiceSourceKind::Vendor:
		command->mutable_identify_item_requested()->set_source(protocol::v1::ITEM_SERVICE_SOURCE_KIND_UNSPECIFIED);
		break;
	case ServerBackedItemServiceSourceKind::ClassAbility:
		command->mutable_identify_item_requested()->set_source(protocol::v1::ITEM_SERVICE_SOURCE_KIND_CLASS_ABILITY);
		break;
	default:
		return tl::make_unexpected("The server-backed item-service source is invalid.");
	}
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeReadBookCommand(ServerBackedItemReference item, uint64_t requestedTick)
{
	return MakeItemCommand(item, requestedTick, [](protocol::v1::Command &command) { return command.mutable_read_book_requested(); });
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeUseConsumableCommand(ServerBackedItemReference item, uint64_t requestedTick)
{
	return MakeItemCommand(item, requestedTick, [](protocol::v1::Command &command) { return command.mutable_use_consumable_requested(); });
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeUseConsumableCommand(ServerBackedItemReference item, ServerBackedItemReference target, uint64_t requestedTick)
{
	auto command = MakeUseConsumableCommand(item, requestedTick);
	if (!command.has_value())
		return command;
	auto targetReference = MakeItemReference(target);
	if (!targetReference.has_value())
		return tl::make_unexpected(targetReference.error());
	command->mutable_use_consumable_requested()->mutable_target_item()->CopyFrom(*targetReference);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeRefillManaCommand(uint64_t requestedTick)
{
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_refill_mana_requested();
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeMoveInventoryItemCommand(uint32_t inventoryIndex, uint32_t targetCell, uint64_t requestedTick)
{
	if (inventoryIndex == UINT32_MAX || targetCell == UINT32_MAX)
		return tl::make_unexpected("Cannot move an invalid inventory item or cell.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	auto *request = command.mutable_move_inventory_item_requested();
	request->set_inventory_index(inventoryIndex);
	request->set_target_cell(targetCell);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeMoveInventoryItemCommand(ServerBackedItemReference item, uint32_t targetCell, uint64_t requestedTick)
{
	if (item.location != ServerBackedItemLocation::Inventory || item.slot == UINT32_MAX || targetCell == UINT32_MAX)
		return tl::make_unexpected("Cannot move an invalid inventory item or cell.");
	auto reference = MakeItemReference(item);
	if (!reference.has_value())
		return tl::make_unexpected(reference.error());
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	auto *request = command.mutable_move_inventory_item_requested();
	request->set_inventory_index(item.slot);
	request->set_target_cell(targetCell);
	request->mutable_item()->CopyFrom(*reference);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeMoveItemCommand(ServerBackedItemReference item, ServerBackedItemReference destination, uint64_t requestedTick)
{
	if (item.slot == UINT32_MAX || destination.slot == UINT32_MAX)
		return tl::make_unexpected("Cannot move an invalid item or destination.");
	auto sourceReference = MakeItemReference(item);
	if (!sourceReference.has_value())
		return tl::make_unexpected(sourceReference.error());
	auto destinationReference = MakeItemReference(destination);
	if (!destinationReference.has_value())
		return tl::make_unexpected(destinationReference.error());
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	auto *request = command.mutable_move_item_requested();
	request->mutable_item()->CopyFrom(*sourceReference);
	request->mutable_destination()->CopyFrom(*destinationReference);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeAutoEquipItemCommand(ServerBackedItemReference item, uint64_t requestedTick)
{
	return MakeItemCommand(item, requestedTick, [](protocol::v1::Command &command) { return command.mutable_auto_equip_item_requested(); });
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeAutoEquipItemCommand(ServerBackedItemReference item, uint32_t equipmentSlot, uint64_t requestedTick)
{
	return MakeItemCommand(item, requestedTick, [equipmentSlot](protocol::v1::Command &command) {
		auto *request = command.mutable_auto_equip_item_requested();
		request->set_equipment_slot(equipmentSlot);
		return request;
	});
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeMoveCommand(int32_t directionX, int32_t directionY, uint64_t requestedTick)
{
	if (directionX < -1 || directionX > 1 || directionY < -1 || directionY > 1 || (directionX == 0 && directionY == 0))
		return tl::make_unexpected("Movement direction must be a non-zero adjacent step.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_move_requested()->set_direction_x(directionX);
	command.mutable_move_requested()->set_direction_y(directionY);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeMoveToCommand(int32_t targetX, int32_t targetY, uint64_t requestedTick)
{
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	auto *request = command.mutable_move_requested();
	request->set_target_x(targetX);
	request->set_target_y(targetY);
	request->set_target_cell(true);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeAttackCommand(uint32_t targetEntityId, uint64_t requestedTick)
{
	if (targetEntityId == 0)
		return tl::make_unexpected("Cannot attack an invalid entity.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_attack_requested()->set_target_entity_id(targetEntityId);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeAttackAtCommand(int32_t targetX, int32_t targetY, uint64_t requestedTick)
{
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	auto *request = command.mutable_attack_requested();
	request->set_target_x(targetX);
	request->set_target_y(targetY);
	request->set_target_cell(true);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeCastCommand(uint32_t spellId, uint32_t targetEntityId, uint64_t requestedTick)
{
	return MakeCastCommand(spellId, targetEntityId, 0, 0, requestedTick);
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeCastCommand(uint32_t spellId, uint32_t targetEntityId, int32_t targetX, int32_t targetY, uint64_t requestedTick)
{
	return MakeCastCommand(spellId, targetEntityId, targetX, targetY, { }, requestedTick);
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeCastCommand(uint32_t spellId, uint32_t targetEntityId, int32_t targetX, int32_t targetY, ServerBackedCastSource source, uint64_t requestedTick)
{
	if (spellId == 0)
		return tl::make_unexpected("Cannot cast an invalid spell.");
	const bool requiresItem = source.kind == ServerBackedCastSourceKind::ChargedItem
	    || source.kind == ServerBackedCastSourceKind::ConsumableItem;
	if (source.item.has_value() != requiresItem)
		return tl::make_unexpected("The server-backed cast source has an invalid item reference.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	auto *request = command.mutable_cast_requested();
	request->set_spell_id(spellId);
	request->set_target_entity_id(targetEntityId);
	request->set_target_x(targetX);
	request->set_target_y(targetY);
	switch (source.kind) {
	case ServerBackedCastSourceKind::Unspecified:
		request->set_source(protocol::v1::CAST_SOURCE_KIND_UNSPECIFIED);
		break;
	case ServerBackedCastSourceKind::Ability:
		request->set_source(protocol::v1::CAST_SOURCE_KIND_ABILITY);
		break;
	case ServerBackedCastSourceKind::MemorizedSpell:
		request->set_source(protocol::v1::CAST_SOURCE_KIND_MEMORIZED_SPELL);
		break;
	case ServerBackedCastSourceKind::ChargedItem:
		request->set_source(protocol::v1::CAST_SOURCE_KIND_CHARGED_ITEM);
		break;
	case ServerBackedCastSourceKind::ConsumableItem:
		request->set_source(protocol::v1::CAST_SOURCE_KIND_CONSUMABLE_ITEM);
		break;
	default:
		return tl::make_unexpected("The server-backed cast source is invalid.");
	}
	if (source.item.has_value()) {
		auto reference = MakeItemReference(*source.item);
		if (!reference.has_value())
			return tl::make_unexpected(reference.error());
		request->mutable_source_item()->CopyFrom(*reference);
	}
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeUsePortalCommand(uint32_t portalId, uint64_t requestedTick)
{
	if (portalId == 0)
		return tl::make_unexpected("Cannot use an invalid portal.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_use_portal_requested()->set_portal_id(portalId);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakePickupWorldItemCommand(uint32_t itemEntityId, uint64_t requestedTick)
{
	if (itemEntityId == 0)
		return tl::make_unexpected("Cannot pick up an invalid world item.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_pickup_world_item_requested()->set_item_entity_id(itemEntityId);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeDropHeldItemCommand(int32_t targetX, int32_t targetY, uint64_t requestedTick)
{
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	auto *request = command.mutable_drop_held_item_requested();
	request->set_target_x(targetX);
	request->set_target_y(targetY);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeDropItemCommand(ServerBackedItemReference item, int32_t targetX, int32_t targetY, uint64_t requestedTick)
{
	auto reference = MakeItemReference(item);
	if (!reference.has_value())
		return tl::make_unexpected(reference.error());
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	auto *request = command.mutable_drop_item_requested();
	request->mutable_item()->CopyFrom(*reference);
	request->set_target_x(targetX);
	request->set_target_y(targetY);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeOperateObjectCommand(uint32_t objectEntityId, uint64_t requestedTick)
{
	if (objectEntityId == 0)
		return tl::make_unexpected("Cannot operate an invalid object.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_operate_object_requested()->set_object_entity_id(objectEntityId);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeDisarmTrapCommand(uint32_t objectEntityId, uint64_t requestedTick)
{
	if (objectEntityId == 0)
		return tl::make_unexpected("Cannot disarm an invalid object.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_disarm_trap_requested()->set_object_entity_id(objectEntityId);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeAdvanceQuestCommand(uint32_t questId, uint64_t requestedTick)
{
	if (questId == 0)
		return tl::make_unexpected("Cannot advance an invalid quest.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_advance_quest_requested()->set_quest_id(questId);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeTalkToQuestCommand(uint32_t questId, uint32_t targetEntityId, uint64_t requestedTick)
{
	if (questId == 0)
		return tl::make_unexpected("Cannot talk for an invalid quest.");
	if (targetEntityId == 0)
		return tl::make_unexpected("Cannot talk to an invalid quest target.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	auto *request = command.mutable_talk_to_quest_requested();
	request->set_quest_id(questId);
	request->set_target_entity_id(targetEntityId);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeTurnInQuestCommand(uint32_t questId, uint32_t townerId, uint64_t requestedTick)
{
	if (questId == 0)
		return tl::make_unexpected("Cannot turn in an invalid quest.");
	if (townerId > UINT8_MAX)
		return tl::make_unexpected("Cannot turn in to an invalid towner.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	auto *request = command.mutable_turn_in_quest_requested();
	request->set_quest_id(questId);
	request->set_towner_id(townerId);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeTalkToTownerCommand(uint32_t townerId, uint64_t requestedTick)
{
	if (townerId > UINT8_MAX)
		return tl::make_unexpected("Cannot talk to an invalid towner.");
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_talk_to_towner_requested()->set_towner_id(townerId);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeSetFriendlyModeCommand(bool friendlyMode, uint64_t requestedTick)
{
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_set_friendly_mode_requested()->set_friendly_mode(friendlyMode);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeAllocateAttributeCommand(ServerBackedPlayerAttribute attribute, uint32_t amount, uint64_t requestedTick)
{
	if (amount == 0)
		return tl::make_unexpected("Attribute allocation amount must be positive.");

	::devilution::protocol::v1::PlayerAttribute protocolAttribute;
	switch (attribute) {
	case ServerBackedPlayerAttribute::Strength:
		protocolAttribute = ::devilution::protocol::v1::PLAYER_ATTRIBUTE_STRENGTH;
		break;
	case ServerBackedPlayerAttribute::Magic:
		protocolAttribute = ::devilution::protocol::v1::PLAYER_ATTRIBUTE_MAGIC;
		break;
	case ServerBackedPlayerAttribute::Dexterity:
		protocolAttribute = ::devilution::protocol::v1::PLAYER_ATTRIBUTE_DEXTERITY;
		break;
	case ServerBackedPlayerAttribute::Vitality:
		protocolAttribute = ::devilution::protocol::v1::PLAYER_ATTRIBUTE_VITALITY;
		break;
	default:
		return tl::make_unexpected("Unknown player attribute.");
	}

	::devilution::protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	auto *request = command.mutable_allocate_attribute_requested();
	request->set_attribute(protocolAttribute);
	request->set_amount(amount);
	return command;
}

tl::expected<::devilution::protocol::v1::Command, std::string> MakeRestartTownCommand(uint64_t requestedTick)
{
	protocol::v1::Command command;
	command.set_requested_tick(requestedTick);
	command.mutable_restart_town_requested();
	return command;
}

} // namespace devilution::authoritative

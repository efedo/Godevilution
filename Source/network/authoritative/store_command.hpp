#pragma once

/**
 * @file network/authoritative/store_command.hpp
 *
 * Server-backed vendor intent construction at the Protobuf boundary.
 */

#include <cstdint>
#include <optional>
#include <string>

#include <expected.hpp>

#include "devilution.pb.h"

namespace devilution::authoritative {

enum class ServerBackedItemLocation {
	Inventory,
	Belt,
	Equipment,
	Stash,
};

struct ServerBackedItemReference {
	ServerBackedItemLocation location = ServerBackedItemLocation::Inventory;
	uint32_t slot = 0;
	/** Stable content identity; zero preserves positional compatibility. */
	uint32_t stableItemId = 0;
	/** Native-compatible stash page; ignored for non-stash references. */
	uint32_t page = 0;
};

enum class ServerBackedItemServiceSourceKind {
	Vendor,
	ClassAbility,
};

enum class ServerBackedCastSourceKind {
	Unspecified,
	Ability,
	MemorizedSpell,
	ChargedItem,
	ConsumableItem,
};

enum class ServerBackedPlayerAttribute {
	Strength,
	Magic,
	Dexterity,
	Vitality,
};

struct ServerBackedCastSource {
	ServerBackedCastSourceKind kind = ServerBackedCastSourceKind::Unspecified;
	std::optional<ServerBackedItemReference> item;
};

[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeOpenStoreCommand(uint32_t storeId, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeCloseStoreCommand(uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeOpenStashCommand(uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeCloseStashCommand(uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeDepositStashGoldCommand(uint32_t amount, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeWithdrawStashGoldCommand(uint32_t amount, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeSplitGoldCommand(ServerBackedItemReference item, uint32_t amount, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeSortInventoryCommand(uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakePurchaseCommand(uint32_t storeId, uint32_t storeSlot, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeSellItemCommand(uint32_t inventoryIndex, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeRepairItemCommand(uint32_t inventoryIndex, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeRechargeItemCommand(uint32_t inventoryIndex, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeIdentifyItemCommand(uint32_t inventoryIndex, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeSellItemCommand(ServerBackedItemReference item, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeRepairItemCommand(ServerBackedItemReference item, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeRepairItemCommand(ServerBackedItemReference item, ServerBackedItemServiceSourceKind source, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeRechargeItemCommand(ServerBackedItemReference item, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeRechargeItemCommand(ServerBackedItemReference item, ServerBackedItemServiceSourceKind source, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeIdentifyItemCommand(ServerBackedItemReference item, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeIdentifyItemCommand(ServerBackedItemReference item, ServerBackedItemServiceSourceKind source, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeReadBookCommand(ServerBackedItemReference item, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeUseConsumableCommand(ServerBackedItemReference item, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeUseConsumableCommand(ServerBackedItemReference item, ServerBackedItemReference target, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeRefillManaCommand(uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeMoveInventoryItemCommand(uint32_t inventoryIndex, uint32_t targetCell, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeMoveInventoryItemCommand(ServerBackedItemReference item, uint32_t targetCell, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeMoveItemCommand(ServerBackedItemReference item, ServerBackedItemReference destination, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeAutoEquipItemCommand(ServerBackedItemReference item, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeAutoEquipItemCommand(ServerBackedItemReference item, uint32_t equipmentSlot, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeMoveCommand(int32_t directionX, int32_t directionY, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeMoveToCommand(int32_t targetX, int32_t targetY, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeAttackCommand(uint32_t targetEntityId, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeAttackAtCommand(int32_t targetX, int32_t targetY, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeCastCommand(uint32_t spellId, uint32_t targetEntityId, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeCastCommand(uint32_t spellId, uint32_t targetEntityId, int32_t targetX, int32_t targetY, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeCastCommand(uint32_t spellId, uint32_t targetEntityId, int32_t targetX, int32_t targetY, ServerBackedCastSource source, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeUsePortalCommand(uint32_t portalId, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakePickupWorldItemCommand(uint32_t itemEntityId, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeDropHeldItemCommand(int32_t targetX, int32_t targetY, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeDropItemCommand(ServerBackedItemReference item, int32_t targetX, int32_t targetY, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeOperateObjectCommand(uint32_t objectEntityId, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeDisarmTrapCommand(uint32_t objectEntityId, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeAdvanceQuestCommand(uint32_t questId, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeTalkToQuestCommand(uint32_t questId, uint32_t targetEntityId, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeTurnInQuestCommand(uint32_t questId, uint32_t townerId, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeTalkToTownerCommand(uint32_t townerId, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeSetFriendlyModeCommand(bool friendlyMode, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeAllocateAttributeCommand(ServerBackedPlayerAttribute attribute, uint32_t amount, uint64_t requestedTick);
[[nodiscard]] tl::expected<::devilution::protocol::v1::Command, std::string> MakeRestartTownCommand(uint64_t requestedTick);

} // namespace devilution::authoritative

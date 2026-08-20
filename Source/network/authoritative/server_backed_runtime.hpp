#pragma once

/**
 * @file network/authoritative/server_backed_runtime.hpp
 *
 * Opt-in lifecycle bridge between the game loop and the server-backed session.
 */

#include <cstddef>
#include <cstdint>
#include <memory>
#include <optional>
#include <string>

#include <expected.hpp>

#include "game/players/players.hpp"
#include "game/stores/stores.hpp"
#include "network/authoritative/server_backed_configuration.hpp"
#include "network/authoritative/server_backed_session.hpp"
#include "network/authoritative/server_backed_vendor_ui.hpp"
#include "network/authoritative/server_backed_world_presentation.hpp"
#include "network/authoritative/server_backed_world_projection.hpp"

namespace devilution::authoritative {

class ServerBackedRuntime {
public:
	tl::expected<void, std::string> Start(const ServerBackedRuntimeConfiguration &configuration, StoreManager &storeManager);
	tl::expected<void, std::string> Start(const ServerBackedRuntimeConfiguration &configuration, StoreManager &storeManager, Player &player);
	void Stop() noexcept;

	[[nodiscard]] bool IsConnected() const noexcept { return session_ != nullptr; }
	[[nodiscard]] bool IsAuthoritativeMode() const noexcept { return authoritativeMode_; }
	[[nodiscard]] ServerBackedSession *Session() noexcept { return session_.get(); }
	[[nodiscard]] const ServerBackedSession *Session() const noexcept { return session_.get(); }
	[[nodiscard]] const ServerBackedWorldProjection &WorldProjection() const noexcept { return worldProjection_; }

	/** Opens the experimental Smith store and applies its authoritative stock to the legacy UI buffers. */
	tl::expected<void, std::string> OpenVendor(uint32_t storeId, ServerBackedVendorDestination destination, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> PurchaseVendor(uint32_t storeId, uint32_t storeSlot, ServerBackedVendorDestination destination, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> OpenSmithStore(uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> OpenWitchStore(uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> OpenWirtStore(uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> OpenHealerStore(uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> OpenAdriaStore(uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> PurchaseSmith(uint32_t storeSlot, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> PurchaseWitch(uint32_t storeSlot, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> PurchaseWirt(uint32_t storeSlot, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> PurchaseHealer(uint32_t storeSlot, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> SellItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> RepairItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> RechargeItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> IdentifyItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> SellItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> RepairItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> RepairItemWithAbility(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> RechargeItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> RechargeItemWithAbility(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> IdentifyItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> IdentifyItemWithAbility(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> ReadBook(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> UseConsumable(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> UseConsumable(ServerBackedItemReference item, ServerBackedItemReference target, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> RefillMana(uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> MoveInventoryItem(uint32_t inventoryIndex, uint32_t targetCell, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> MoveInventoryItem(ServerBackedItemReference item, uint32_t targetCell, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> MoveItem(ServerBackedItemReference item, ServerBackedItemReference destination, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> AutoEquipItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> AutoEquipItem(ServerBackedItemReference item, uint32_t equipmentSlot, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> Move(int32_t directionX, int32_t directionY, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> MoveTo(int32_t targetX, int32_t targetY, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> Attack(uint32_t targetEntityId, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> AttackAt(int32_t targetX, int32_t targetY, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> Cast(uint32_t spellId, uint32_t targetEntityId, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> Cast(uint32_t spellId, uint32_t targetEntityId, int32_t targetX, int32_t targetY, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> Cast(uint32_t spellId, uint32_t targetEntityId, int32_t targetX, int32_t targetY, ServerBackedCastSource source, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> UsePortal(uint32_t portalId, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> PickupWorldItem(uint32_t itemEntityId, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> DropHeldItem(int32_t targetX, int32_t targetY, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> OperateObject(uint32_t objectEntityId, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> DisarmTrap(uint32_t objectEntityId, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> AdvanceQuest(uint32_t questId, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> TalkToQuest(uint32_t questId, uint32_t targetEntityId, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> TurnInQuest(uint32_t questId, uint32_t townerId, uint64_t requestedTick, uint64_t nowMs);
	[[nodiscard]] bool HasActiveQuest(uint32_t questId) const noexcept;
	[[nodiscard]] std::optional<uint32_t> ActiveQuestForTowner(uint32_t townerId) const noexcept;
	/** Returns the lowest-ID active quest authored for a projected unique monster. */
	[[nodiscard]] std::optional<uint32_t> ActiveQuestForMonster(uint32_t monsterEntityId) const noexcept;
	tl::expected<void, std::string> SetFriendlyMode(bool friendlyMode, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> AllocateAttribute(ServerBackedPlayerAttribute attribute, uint32_t amount, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> RestartTown(uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> Poll(uint64_t nowMs);

	[[nodiscard]] std::optional<uint32_t> SmithStoreSlotAt(std::size_t index) const noexcept;
	[[nodiscard]] std::optional<uint32_t> StoreSlotAt(std::size_t index) const noexcept;
	/** Returns the authoritative entity ID for a projected monster index. */
	[[nodiscard]] std::optional<uint32_t> MonsterEntityIdAt(std::size_t index) const noexcept;
	/** Finds the lowest-ID authoritative world item at a projected cell. */
	[[nodiscard]] std::optional<uint32_t> WorldItemEntityIdAt(int32_t positionX, int32_t positionY) const noexcept;
	/** Finds the lowest-ID live authoritative monster at a projected cell. */
	[[nodiscard]] std::optional<uint32_t> MonsterEntityIdAt(int32_t positionX, int32_t positionY) const noexcept;
	/** Finds the lowest-ID remote authoritative player at a projected cell. */
	[[nodiscard]] std::optional<uint32_t> PlayerEntityIdAt(int32_t positionX, int32_t positionY) const noexcept;
	/** Finds the lowest-ID authoritative object at a projected cell. */
	[[nodiscard]] std::optional<uint32_t> ObjectEntityIdAt(int32_t positionX, int32_t positionY) const noexcept;
	/** Finds the active content-owned quest object at a projected cell. */
	[[nodiscard]] std::optional<uint32_t> QuestObjectEntityIdAt(int32_t positionX, int32_t positionY) const noexcept;
	/** Finds the lowest-ID authoritative portal endpoint at a projected cell. */
	[[nodiscard]] std::optional<uint32_t> PortalEntityIdAt(int32_t positionX, int32_t positionY) const noexcept;
	/** Finds the authoritative location of an item, including while it is held by the legacy cursor. */
	[[nodiscard]] std::optional<ServerBackedItemReference> PlayerItemReferenceForSeed(uint32_t itemSeed) const noexcept;
	/** Retains a native inventory drag selection while authoritative snapshots refresh. */
	[[nodiscard]] bool BeginInventoryItemDrag(uint32_t itemSeed) noexcept;
	/** Cancels the presentation-only inventory drag selection. */
	void CancelInventoryItemDrag() noexcept;
	/** Arms a native oil cursor action with its authoritative source reference. */
	[[nodiscard]] bool BeginOilUse(ServerBackedItemReference item) noexcept;
	/** Returns the authoritative source retained by the native oil cursor. */
	[[nodiscard]] std::optional<ServerBackedItemReference> PendingOilUse() const noexcept;
	/** Cancels the presentation-only native oil cursor action. */
	void CancelOilUse() noexcept;

private:
	tl::expected<void, std::string> StartImpl(const ServerBackedRuntimeConfiguration &configuration, StoreManager &storeManager, Player *player);
	tl::expected<void, std::string> ApplyCurrentPlayerSnapshot();
	tl::expected<void, std::string> EnsureCommandAccepted() const;

	std::unique_ptr<ServerBackedSession> session_;
	std::unique_ptr<ServerBackedVendorUiAdapter> vendorUiAdapter_;
	Player *player_ = nullptr;
	bool authoritativeMode_ = false;
	std::optional<ServerBackedItemReference> pendingInventoryItemDrag_;
	std::optional<uint32_t> pendingInventoryItemDragSeed_;
	std::optional<ServerBackedItemReference> pendingOilUse_;
	ServerBackedWorldProjection worldProjection_;
	ServerBackedWorldPresentation worldPresentation_;
};

/** Returns the process-wide opt-in runtime bridge. */
ServerBackedRuntime &GetServerBackedRuntime();

} // namespace devilution::authoritative

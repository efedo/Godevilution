#pragma once

/**
 * @file network/authoritative/server_backed_session.hpp
 *
 * Runtime lifecycle for the opt-in server-backed inventory/store slice.
 */

#include <cstdint>
#include <map>
#include <memory>
#include <optional>
#include <string>
#include <string_view>
#include <vector>

#include <expected.hpp>

#include "network/authoritative/player_snapshot.hpp"
#include "network/authoritative/server_backed_client.hpp"
#include "network/authoritative/server_backed_vendor_state.hpp"
#include "network/authoritative/store_command.hpp"

namespace devilution::authoritative {

class ServerBackedSession {
public:
	enum class CommandResolution {
		None,
		Accepted,
		Rejected,
		Rescheduled,
		Duplicate,
	};

	struct Configuration {
		ServerBackedClient::Configuration client;
	};

	static tl::expected<std::unique_ptr<ServerBackedSession>, std::string> Connect(Configuration configuration);

	[[nodiscard]] const ServerBackedClient &Client() const noexcept { return *client_; }
	[[nodiscard]] uint32_t EntityId() const noexcept { return entityId_; }
	[[nodiscard]] const ServerBackedPlayerState &PlayerState() const noexcept { return playerState_; }
	[[nodiscard]] const std::vector<ProjectedPlayerSnapshot> &PlayerStates() const noexcept { return playerStates_; }
	[[nodiscard]] const std::vector<ProjectedMonsterSnapshot> &MonsterState() const noexcept { return monsterState_; }
	[[nodiscard]] const std::vector<ProjectedWorldItemSnapshot> &WorldItemState() const noexcept { return worldItemState_; }
	[[nodiscard]] const std::vector<ProjectedObjectSnapshot> &ObjectState() const noexcept { return objectState_; }
	[[nodiscard]] const std::vector<ProjectedProjectileSnapshot> &ProjectileState() const noexcept { return projectileState_; }
	[[nodiscard]] const std::vector<ProjectedPortalSnapshot> &PortalState() const noexcept { return portalState_; }
	[[nodiscard]] const std::vector<ProjectedCorpseSnapshot> &CorpseState() const noexcept { return corpseState_; }
	[[nodiscard]] const std::vector<ProjectedQuestSnapshot> &QuestState() const noexcept { return questState_; }
	[[nodiscard]] const ProjectedSessionMetadata &SessionMetadata() const noexcept { return sessionMetadata_; }
	[[nodiscard]] const std::vector<ProjectedLevelGeometrySnapshot> &LevelGeometryState() const noexcept { return levelGeometryState_; }
	[[nodiscard]] const ServerBackedVendorState &VendorState() const noexcept { return vendorState_; }
	/** Takes event batches received with the latest authoritative snapshot. */
	std::vector<protocol::EventBatch> TakePendingEventBatches() { return client_->TakePendingEventBatches(); }

	/** Opens a vendor and waits for its authoritative snapshot. */
	tl::expected<void, std::string> OpenVendor(uint32_t storeId, uint64_t requestedTick, uint64_t nowMs);
	/** Closes the active vendor and waits for the authoritative empty snapshot. */
	tl::expected<void, std::string> CloseVendor(uint64_t requestedTick, uint64_t nowMs);
	/** Opens the server-owned stash session and waits for its projection. */
	tl::expected<void, std::string> OpenStash(uint64_t requestedTick, uint64_t nowMs);
	/** Closes the server-owned stash session and waits for its projection. */
	tl::expected<void, std::string> CloseStash(uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> DepositStashGold(uint32_t amount, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> WithdrawStashGold(uint32_t amount, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> SplitGold(ServerBackedItemReference item, uint32_t amount, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> SortInventory(uint64_t requestedTick, uint64_t nowMs);

	/** Purchases one stable vendor slot and waits for the updated snapshot. */
	tl::expected<void, std::string> Purchase(uint32_t storeId, uint32_t storeSlot, uint64_t requestedTick, uint64_t nowMs);
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
	tl::expected<void, std::string> DropItem(ServerBackedItemReference item, int32_t targetX, int32_t targetY, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> OperateObject(uint32_t objectEntityId, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> DisarmTrap(uint32_t objectEntityId, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> AdvanceQuest(uint32_t questId, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> TalkToQuest(uint32_t questId, uint32_t targetEntityId, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> TurnInQuest(uint32_t questId, uint32_t townerId, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> TalkToTowner(uint32_t townerId, uint64_t requestedTick, uint64_t nowMs);
	/** Returns whether the latest authoritative snapshot has an active quest. */
	[[nodiscard]] bool HasActiveQuest(uint32_t questId) const noexcept;
	/** Returns the lowest-ID active quest whose content declares this towner for turn-in. */
	[[nodiscard]] std::optional<uint32_t> ActiveQuestForTowner(uint32_t townerId) const noexcept;
	/** Returns the lowest-ID eligible quest whose content declares this unique target identity. */
	[[nodiscard]] std::optional<uint32_t> ActiveQuestForUniqueIdentity(std::string_view uniqueIdentity) const noexcept;
	tl::expected<void, std::string> SetFriendlyMode(bool friendlyMode, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> AllocateAttribute(ServerBackedPlayerAttribute attribute, uint32_t amount, uint64_t requestedTick, uint64_t nowMs);
	tl::expected<void, std::string> RestartTown(uint64_t requestedTick, uint64_t nowMs);

	/** Polls for commands whose adaptive acknowledgement timeout has elapsed. */
	tl::expected<void, std::string> Poll(uint64_t nowMs);

	[[nodiscard]] CommandResolution LastCommandResolution() const noexcept { return lastCommandResolution_; }

	/** Reconnects, applies the resynchronization snapshot, and resolves retries. */
	tl::expected<void, std::string> Reconnect(uint64_t nowMs);

	void Close() noexcept;

private:
	struct PendingIntent {
		enum class Kind {
			OpenVendor,
			CloseVendor,
			Purchase,
			Inventory,
			Gameplay,
		};
		Kind kind;
		uint32_t storeId = 0;
		uint32_t storeSlot = 0;
	};

	ServerBackedSession() = default;

	tl::expected<void, std::string> ApplySnapshot(const protocol::Snapshot &snapshot);
	tl::expected<void, std::string> Flush(uint64_t nowMs, std::optional<uint64_t> focusSequence = std::nullopt);
	tl::expected<void, std::string> SubmitInventoryCommand(tl::expected<protocol::Command, std::string> command, uint64_t nowMs);
	tl::expected<void, std::string> SubmitGameplayCommand(tl::expected<protocol::Command, std::string> command, uint64_t nowMs);
	void ApplyAcknowledgements(const protocol::CommandAck &acknowledgement, std::optional<uint64_t> focusSequence = std::nullopt);
	[[nodiscard]] std::optional<ServerBackedItemReference> PlayerInventoryItemReference(uint32_t inventoryIndex) const noexcept;

	std::unique_ptr<ServerBackedClient> client_;
	uint32_t entityId_ = 0;
	ServerBackedPlayerState playerState_;
	std::vector<ProjectedPlayerSnapshot> playerStates_;
	std::vector<ProjectedMonsterSnapshot> monsterState_;
	std::vector<ProjectedWorldItemSnapshot> worldItemState_;
	std::vector<ProjectedObjectSnapshot> objectState_;
	std::vector<ProjectedProjectileSnapshot> projectileState_;
	std::vector<ProjectedPortalSnapshot> portalState_;
	std::vector<ProjectedCorpseSnapshot> corpseState_;
	std::vector<ProjectedQuestSnapshot> questState_;
	ProjectedSessionMetadata sessionMetadata_;
	std::vector<ProjectedLevelGeometrySnapshot> levelGeometryState_;
	std::vector<protocol::QuestSnapshot> questStates_;
	std::vector<protocol::QuestTownerInteractionSnapshot> questTownerInteractions_;
	ServerBackedVendorState vendorState_;
	std::map<uint64_t, PendingIntent> pendingIntents_;
	CommandResolution lastCommandResolution_ = CommandResolution::None;
	std::optional<uint64_t> lastSnapshotRequestMs_;
	bool acceptingReconnectSnapshot_ = false;
};

} // namespace devilution::authoritative

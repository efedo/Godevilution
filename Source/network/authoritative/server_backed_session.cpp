#include "network/authoritative/server_backed_session.hpp"

#include <algorithm>

#include "network/authoritative/store_command.hpp"

namespace devilution::authoritative {

tl::expected<std::unique_ptr<ServerBackedSession>, std::string> ServerBackedSession::Connect(Configuration configuration)
{
	configuration.client.expectInitialSnapshot = true;
	auto client = ServerBackedClient::Connect(std::move(configuration.client));
	if (!client.has_value())
		return tl::make_unexpected(client.error());

	auto session = std::unique_ptr<ServerBackedSession>(new ServerBackedSession());
	session->client_ = std::move(*client);
	auto initialSnapshot = session->client_->ReadSnapshot();
	if (!initialSnapshot.has_value())
		return tl::make_unexpected(initialSnapshot.error());
	if (auto result = session->ApplySnapshot(*initialSnapshot); !result.has_value())
		return tl::make_unexpected(result.error());
	return session;
}

tl::expected<void, std::string> ServerBackedSession::OpenVendor(uint32_t storeId, uint64_t requestedTick, uint64_t nowMs)
{
	lastCommandResolution_ = CommandResolution::None;
	if (vendorState_.OpenStore(storeId) != VendorIntentRoute::Pending)
		return tl::make_unexpected("The server-backed vendor is not ready to open.");
	auto command = MakeOpenStoreCommand(storeId, requestedTick);
	if (!command.has_value())
		return tl::make_unexpected(command.error());
	const uint64_t sequence = client_->QueueCommand(std::move(*command));
	pendingIntents_.emplace(sequence, PendingIntent { .kind = PendingIntent::Kind::OpenVendor, .storeId = storeId });
	return Flush(nowMs, sequence);
}

tl::expected<void, std::string> ServerBackedSession::Purchase(uint32_t storeId, uint32_t storeSlot, uint64_t requestedTick, uint64_t nowMs)
{
	lastCommandResolution_ = CommandResolution::None;
	if (vendorState_.Purchase(storeId, storeSlot) != VendorIntentRoute::Pending)
		return tl::make_unexpected("The server-backed vendor cannot accept this purchase.");
	auto command = MakePurchaseCommand(storeId, storeSlot, requestedTick);
	if (!command.has_value())
		return tl::make_unexpected(command.error());
	const uint64_t sequence = client_->QueueCommand(std::move(*command));
	pendingIntents_.emplace(sequence, PendingIntent { .kind = PendingIntent::Kind::Purchase, .storeId = storeId, .storeSlot = storeSlot });
	return Flush(nowMs, sequence);
}

tl::expected<void, std::string> ServerBackedSession::SellItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs)
{
	const auto item = PlayerInventoryItemReference(inventoryIndex);
	if (!item.has_value())
		return tl::make_unexpected("The authoritative inventory item is no longer present.");
	return SellItem(*item, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedSession::SellItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeSellItemCommand(item, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::RepairItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs)
{
	const auto item = PlayerInventoryItemReference(inventoryIndex);
	if (!item.has_value())
		return tl::make_unexpected("The authoritative inventory item is no longer present.");
	return RepairItem(*item, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedSession::RepairItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeRepairItemCommand(item, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::RepairItemWithAbility(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeRepairItemCommand(item, ServerBackedItemServiceSourceKind::ClassAbility, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::RechargeItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs)
{
	const auto item = PlayerInventoryItemReference(inventoryIndex);
	if (!item.has_value())
		return tl::make_unexpected("The authoritative inventory item is no longer present.");
	return RechargeItem(*item, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedSession::RechargeItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeRechargeItemCommand(item, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::RechargeItemWithAbility(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeRechargeItemCommand(item, ServerBackedItemServiceSourceKind::ClassAbility, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::IdentifyItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs)
{
	const auto item = PlayerInventoryItemReference(inventoryIndex);
	if (!item.has_value())
		return tl::make_unexpected("The authoritative inventory item is no longer present.");
	return IdentifyItem(*item, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedSession::IdentifyItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeIdentifyItemCommand(item, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::IdentifyItemWithAbility(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeIdentifyItemCommand(item, ServerBackedItemServiceSourceKind::ClassAbility, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::ReadBook(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeReadBookCommand(item, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::UseConsumable(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeUseConsumableCommand(item, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::UseConsumable(ServerBackedItemReference item, ServerBackedItemReference target, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeUseConsumableCommand(item, target, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::RefillMana(uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeRefillManaCommand(requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::MoveInventoryItem(uint32_t inventoryIndex, uint32_t targetCell, uint64_t requestedTick, uint64_t nowMs)
{
	const auto item = PlayerInventoryItemReference(inventoryIndex);
	if (!item.has_value())
		return tl::make_unexpected("The authoritative inventory item is no longer present.");
	return MoveInventoryItem(*item, targetCell, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedSession::MoveInventoryItem(ServerBackedItemReference item, uint32_t targetCell, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeMoveInventoryItemCommand(item, targetCell, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::MoveItem(ServerBackedItemReference item, ServerBackedItemReference destination, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeMoveItemCommand(item, destination, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::AutoEquipItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeAutoEquipItemCommand(item, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::AutoEquipItem(ServerBackedItemReference item, uint32_t equipmentSlot, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeAutoEquipItemCommand(item, equipmentSlot, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::Move(int32_t directionX, int32_t directionY, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeMoveCommand(directionX, directionY, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::Attack(uint32_t targetEntityId, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeAttackCommand(targetEntityId, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::Cast(uint32_t spellId, uint32_t targetEntityId, uint64_t requestedTick, uint64_t nowMs)
{
	return Cast(spellId, targetEntityId, 0, 0, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedSession::Cast(uint32_t spellId, uint32_t targetEntityId, int32_t targetX, int32_t targetY, uint64_t requestedTick, uint64_t nowMs)
{
	return Cast(spellId, targetEntityId, targetX, targetY, { }, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedSession::Cast(uint32_t spellId, uint32_t targetEntityId, int32_t targetX, int32_t targetY, ServerBackedCastSource source, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeCastCommand(spellId, targetEntityId, targetX, targetY, source, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::UsePortal(uint32_t portalId, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeUsePortalCommand(portalId, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::PickupWorldItem(uint32_t itemEntityId, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakePickupWorldItemCommand(itemEntityId, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::DropHeldItem(int32_t targetX, int32_t targetY, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeDropHeldItemCommand(targetX, targetY, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::OperateObject(uint32_t objectEntityId, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeOperateObjectCommand(objectEntityId, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::AdvanceQuest(uint32_t questId, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeAdvanceQuestCommand(questId, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::TurnInQuest(uint32_t questId, uint32_t townerId, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeTurnInQuestCommand(questId, townerId, requestedTick), nowMs);
}

bool ServerBackedSession::HasActiveQuest(uint32_t questId) const noexcept
{
	return std::any_of(questStates_.begin(), questStates_.end(), [questId](const auto &quest) {
		return quest.quest_id() == questId
		    && !quest.initial()
		    && !quest.completed()
		    && !quest.unavailable();
	});
}

tl::expected<void, std::string> ServerBackedSession::SetFriendlyMode(bool friendlyMode, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeSetFriendlyModeCommand(friendlyMode, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::RestartTown(uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeRestartTownCommand(requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::SubmitInventoryCommand(tl::expected<protocol::Command, std::string> command, uint64_t nowMs)
{
	lastCommandResolution_ = CommandResolution::None;
	if (!command.has_value())
		return tl::make_unexpected(command.error());
	const uint64_t sequence = client_->QueueCommand(std::move(*command));
	pendingIntents_.emplace(sequence, PendingIntent { .kind = PendingIntent::Kind::Inventory });
	return Flush(nowMs, sequence);
}

tl::expected<void, std::string> ServerBackedSession::SubmitGameplayCommand(tl::expected<protocol::Command, std::string> command, uint64_t nowMs)
{
	lastCommandResolution_ = CommandResolution::None;
	if (!command.has_value())
		return tl::make_unexpected(command.error());
	const uint64_t sequence = client_->QueueCommand(std::move(*command));
	pendingIntents_.emplace(sequence, PendingIntent { .kind = PendingIntent::Kind::Gameplay });
	return Flush(nowMs, sequence);
}

tl::expected<void, std::string> ServerBackedSession::Poll(uint64_t nowMs)
{
	if (client_->PendingTrackedCommandCount() != 0) {
		auto resubmissions = client_->PrepareTrackedResubmissions(nowMs);
		if (!resubmissions.has_value())
			return tl::make_unexpected(resubmissions.error());
		if (!resubmissions->empty()) {
			auto acknowledgement = client_->ReceiveCommandAcknowledgement(nowMs);
			if (!acknowledgement.has_value())
				return tl::make_unexpected(acknowledgement.error());
			ApplyAcknowledgements(*acknowledgement);
			auto snapshot = client_->ReadSnapshot();
			if (!snapshot.has_value())
				return tl::make_unexpected(snapshot.error());
			return ApplySnapshot(*snapshot);
		}
		return { };
	}

	constexpr uint64_t SnapshotPollIntervalMs = 50;
	if (lastSnapshotRequestMs_.has_value() && nowMs - *lastSnapshotRequestMs_ < SnapshotPollIntervalMs)
		return { };
	lastSnapshotRequestMs_ = nowMs;
	auto snapshot = client_->RequestSnapshot();
	if (!snapshot.has_value())
		return tl::make_unexpected(snapshot.error());
	return ApplySnapshot(*snapshot);
}

tl::expected<void, std::string> ServerBackedSession::Reconnect(uint64_t nowMs)
{
	if (auto result = client_->Reconnect(nowMs); !result.has_value())
		return tl::make_unexpected(result.error());
	vendorState_.SetConnected(true);
	auto snapshot = client_->ReadSnapshot();
	if (!snapshot.has_value())
		return tl::make_unexpected(snapshot.error());
	if (auto result = ApplySnapshot(*snapshot); !result.has_value())
		return tl::make_unexpected(result.error());
	if (client_->PendingTrackedCommandCount() == 0)
		return { };
	return Flush(nowMs);
}

void ServerBackedSession::Close() noexcept
{
	if (client_)
		client_->Close();
	vendorState_.SetConnected(false);
}

tl::expected<void, std::string> ServerBackedSession::ApplySnapshot(const protocol::Snapshot &snapshot)
{
	if (entityId_ == 0) {
		if (snapshot.players_size() == 0)
			return tl::make_unexpected("Server-backed snapshot contains no player entity.");
		entityId_ = snapshot.players(0).entity_id();
	}
	auto players = ProjectPlayerSnapshots(snapshot);
	if (!players.has_value())
		return tl::make_unexpected(players.error());
	const auto player = std::find_if(players->begin(), players->end(), [&](const auto &candidate) {
		return candidate.entityId == entityId_;
	});
	if (player == players->end())
		return tl::make_unexpected("Server-backed snapshot does not contain the requested player entity.");
	if (!playerState_.ApplySnapshot(*player))
		return tl::make_unexpected("Server-backed player snapshot could not be applied.");
	playerStates_ = std::move(*players);
	auto monsters = ProjectMonsterSnapshots(snapshot);
	if (!monsters.has_value())
		return tl::make_unexpected(monsters.error());
	monsterState_ = std::move(*monsters);
	auto worldItems = ProjectWorldItemSnapshots(snapshot);
	if (!worldItems.has_value())
		return tl::make_unexpected(worldItems.error());
	worldItemState_ = std::move(*worldItems);
	auto objects = ProjectObjectSnapshots(snapshot);
	if (!objects.has_value())
		return tl::make_unexpected(objects.error());
	objectState_ = std::move(*objects);
	auto projectiles = ProjectProjectileSnapshots(snapshot);
	if (!projectiles.has_value())
		return tl::make_unexpected(projectiles.error());
	projectileState_ = std::move(*projectiles);
	questStates_.assign(snapshot.quests().begin(), snapshot.quests().end());

	vendorState_.SetEnabled(true);
	vendorState_.SetConnected(true);
	if (snapshot.has_active_store()) {
		auto vendor = ProjectVendorSnapshot(snapshot);
		if (!vendor.has_value())
			return tl::make_unexpected(vendor.error());
		if (!vendorState_.ApplySnapshot(std::move(*vendor)))
			return tl::make_unexpected("Server-backed vendor snapshot could not be applied.");
	} else {
		vendorState_.ClearSnapshot();
	}
	return { };
}

tl::expected<void, std::string> ServerBackedSession::Flush(uint64_t nowMs, std::optional<uint64_t> focusSequence)
{
	if (pendingIntents_.empty())
		return { };
	if (auto result = client_->SendQueuedCommands(nowMs); !result.has_value())
		return result;
	auto acknowledgement = client_->ReceiveCommandAcknowledgement(nowMs);
	if (!acknowledgement.has_value())
		return tl::make_unexpected(acknowledgement.error());
	ApplyAcknowledgements(*acknowledgement, focusSequence);
	auto snapshot = client_->ReadSnapshot();
	if (!snapshot.has_value())
		return tl::make_unexpected(snapshot.error());
	return ApplySnapshot(*snapshot);
}

void ServerBackedSession::ApplyAcknowledgements(const protocol::CommandAck &acknowledgement, std::optional<uint64_t> focusSequence)
{
	for (const auto &result : acknowledgement.results()) {
		const auto pending = pendingIntents_.find(result.client_sequence());
		if (pending == pendingIntents_.end())
			continue;
		if (pending->second.kind == PendingIntent::Kind::Purchase) {
			if (result.status() == protocol::COMMAND_STATUS_REJECTED)
				(void)vendorState_.ResolvePurchase(pending->second.storeId, pending->second.storeSlot, PurchaseResolution::Rejected);
			else if (result.status() == protocol::COMMAND_STATUS_ACCEPTED || result.status() == protocol::COMMAND_STATUS_DUPLICATE)
				(void)vendorState_.ResolvePurchase(pending->second.storeId, pending->second.storeSlot, PurchaseResolution::Accepted);
		}
		if (!focusSequence.has_value() || *focusSequence == result.client_sequence()) {
			switch (result.status()) {
			case protocol::COMMAND_STATUS_ACCEPTED:
				lastCommandResolution_ = CommandResolution::Accepted;
				break;
			case protocol::COMMAND_STATUS_REJECTED:
				lastCommandResolution_ = CommandResolution::Rejected;
				break;
			case protocol::COMMAND_STATUS_RESCHEDULED:
				lastCommandResolution_ = CommandResolution::Rescheduled;
				break;
			case protocol::COMMAND_STATUS_DUPLICATE:
				lastCommandResolution_ = CommandResolution::Duplicate;
				break;
			default:
				lastCommandResolution_ = CommandResolution::None;
				break;
			}
		}
		// The delivery tracker treats every acknowledgement status as terminal.
		// Keep the session intent map consistent so a reconnect cannot replay a
		// command that the server has already resolved.
		pendingIntents_.erase(pending);
	}
}

std::optional<ServerBackedItemReference> ServerBackedSession::PlayerInventoryItemReference(uint32_t inventoryIndex) const noexcept
{
	const auto *snapshot = playerState_.Snapshot();
	if (snapshot == nullptr || static_cast<std::size_t>(inventoryIndex) >= snapshot->inventory.size())
		return std::nullopt;
	const auto &item = snapshot->inventory[static_cast<std::size_t>(inventoryIndex)];
	return ServerBackedItemReference {
		.location = ServerBackedItemLocation::Inventory,
		.slot = inventoryIndex,
		.stableItemId = item.stableItemId,
	};
}

} // namespace devilution::authoritative

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
	if (sequence == 0)
		return tl::make_unexpected("The authoritative client sequence space is exhausted.");
	pendingIntents_.emplace(sequence, PendingIntent { .kind = PendingIntent::Kind::OpenVendor, .storeId = storeId });
	return Flush(nowMs, sequence);
}

tl::expected<void, std::string> ServerBackedSession::CloseVendor(uint64_t requestedTick, uint64_t nowMs)
{
	lastCommandResolution_ = CommandResolution::None;
	if (!client_ || !vendorState_.Snapshot())
		return tl::make_unexpected("The server-backed vendor is not open.");
	auto command = MakeCloseStoreCommand(requestedTick);
	if (!command.has_value())
		return tl::make_unexpected(command.error());
	const uint64_t sequence = client_->QueueCommand(std::move(*command));
	if (sequence == 0)
		return tl::make_unexpected("The authoritative client sequence space is exhausted.");
	pendingIntents_.emplace(sequence, PendingIntent { .kind = PendingIntent::Kind::CloseVendor });
	return Flush(nowMs, sequence);
}

tl::expected<void, std::string> ServerBackedSession::OpenStash(uint64_t requestedTick, uint64_t nowMs)
{
	lastCommandResolution_ = CommandResolution::None;
	const auto *snapshot = playerState_.Snapshot();
	if (snapshot == nullptr || snapshot->activeStoreId.has_value())
		return tl::make_unexpected("The server-backed stash cannot open while a vendor is active.");
	return SubmitInventoryCommand(MakeOpenStashCommand(requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::CloseStash(uint64_t requestedTick, uint64_t nowMs)
{
	lastCommandResolution_ = CommandResolution::None;
	const auto *snapshot = playerState_.Snapshot();
	if (snapshot == nullptr || !snapshot->stashOpen)
		return tl::make_unexpected("The server-backed stash is not open.");
	return SubmitInventoryCommand(MakeCloseStashCommand(requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::DepositStashGold(uint32_t amount, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeDepositStashGoldCommand(amount, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::WithdrawStashGold(uint32_t amount, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeWithdrawStashGoldCommand(amount, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::SplitGold(ServerBackedItemReference item, uint32_t amount, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeSplitGoldCommand(item, amount, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::SortInventory(uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitInventoryCommand(MakeSortInventoryCommand(requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::Purchase(uint32_t storeId, uint32_t storeSlot, uint64_t requestedTick, uint64_t nowMs)
{
	lastCommandResolution_ = CommandResolution::None;
	if (vendorState_.Purchase(storeId, storeSlot) != VendorIntentRoute::Pending)
		return tl::make_unexpected("The server-backed vendor cannot accept this purchase.");
	auto command = MakePurchaseCommand(storeId, storeSlot, requestedTick);
	if (!command.has_value()) {
		(void)vendorState_.ResolvePurchase(storeId, storeSlot, PurchaseResolution::Rejected);
		return tl::make_unexpected(command.error());
	}
	const uint64_t sequence = client_->QueueCommand(std::move(*command));
	if (sequence == 0) {
		(void)vendorState_.ResolvePurchase(storeId, storeSlot, PurchaseResolution::Rejected);
		return tl::make_unexpected("The authoritative client sequence space is exhausted.");
	}
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

tl::expected<void, std::string> ServerBackedSession::MoveTo(int32_t targetX, int32_t targetY, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeMoveToCommand(targetX, targetY, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::Attack(uint32_t targetEntityId, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeAttackCommand(targetEntityId, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::AttackAt(int32_t targetX, int32_t targetY, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeAttackAtCommand(targetX, targetY, requestedTick), nowMs);
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

tl::expected<void, std::string> ServerBackedSession::DropItem(ServerBackedItemReference item, int32_t targetX, int32_t targetY, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeDropItemCommand(item, targetX, targetY, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::OperateObject(uint32_t objectEntityId, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeOperateObjectCommand(objectEntityId, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::DisarmTrap(uint32_t objectEntityId, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeDisarmTrapCommand(objectEntityId, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::AdvanceQuest(uint32_t questId, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeAdvanceQuestCommand(questId, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::TalkToQuest(uint32_t questId, uint32_t targetEntityId, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeTalkToQuestCommand(questId, targetEntityId, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::TurnInQuest(uint32_t questId, uint32_t townerId, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeTurnInQuestCommand(questId, townerId, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::TalkToTowner(uint32_t townerId, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeTalkToTownerCommand(townerId, requestedTick), nowMs);
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

std::optional<uint32_t> ServerBackedSession::ActiveQuestForTowner(uint32_t townerId) const noexcept
{
	std::optional<uint32_t> result;
	const auto *currentPlayer = playerState_.Snapshot();
	const auto hasVisitedLevel = [&](uint32_t levelId) {
		return currentPlayer != nullptr
		    && levelId != 0
		    && std::find(
			       currentPlayer->visitedLevelIds.begin(),
			       currentPlayer->visitedLevelIds.end(),
			       levelId)
			        != currentPlayer->visitedLevelIds.end();
	};
	const auto hasItem = [&](uint32_t itemId) {
		if (currentPlayer == nullptr || itemId == 0)
			return false;
		return std::any_of(currentPlayer->inventory.begin(), currentPlayer->inventory.end(), [itemId](const auto &item) {
			       return item.stableItemId == itemId;
		       })
		    || std::any_of(currentPlayer->belt.begin(), currentPlayer->belt.end(), [itemId](const auto &item) {
			       return item.stableItemId == itemId;
		       });
	};
	for (const auto &interaction : questTownerInteractions_) {
		if (interaction.towner_id() < 0
		    || static_cast<uint32_t>(interaction.towner_id()) != townerId) {
			continue;
		}
		auto questIt = std::find_if(questStates_.begin(), questStates_.end(), [&interaction](const auto &quest) {
			return quest.quest_id() == interaction.quest_id();
		});
		if (questIt == questStates_.end()
		    || questIt->unavailable()
		    || (questIt->completed() && !interaction.allow_completed())
		    || (!questIt->completed() && interaction.allow_completed())) {
			continue;
		}
		if (interaction.require_unlogged() && questIt->logged())
			continue;
		if ((interaction.required_visited_level1() != 0
		        || interaction.required_visited_level2() != 0)
		    && !hasVisitedLevel(interaction.required_visited_level1())
		    && !hasVisitedLevel(interaction.required_visited_level2())) {
			continue;
		}
		if (interaction.item_id() != 0 && !hasItem(interaction.item_id()))
			continue;
		if (interaction.required_item_id() != 0) {
			const bool requiredItemPresent = hasItem(interaction.required_item_id());
			const bool requireItemPresent = !interaction.has_require_item_present()
			    || interaction.require_item_present();
			if (requireItemPresent != requiredItemPresent)
				continue;
		}
		const bool active = interaction.kind() == 0
		    ? questIt->initial()
		        || interaction.has_required_variable1()
		        || interaction.has_required_variable2()
		    : !questIt->initial()
		        || interaction.allow_initial() && questIt->initial();
		if (!active
		    || (interaction.has_required_variable1()
		        && questIt->variable_1() != interaction.required_variable1())
		    || (interaction.has_required_variable2()
		        && questIt->variable_2() != interaction.required_variable2())) {
			continue;
		}
		if (!result.has_value() || questIt->quest_id() < *result)
			result = questIt->quest_id();
	}
	for (const auto &quest : questStates_) {
		if (!quest.has_turn_in_towner_id()
		    || quest.turn_in_towner_id() < 0
		    || static_cast<uint32_t>(quest.turn_in_towner_id()) != townerId
		    || quest.unavailable()) {
			continue;
		}
		if (quest.has_turn_in_blocked_visited_level()
		    && hasVisitedLevel(quest.turn_in_blocked_visited_level())) {
			continue;
		}
		const bool dialogueTurnIn = quest.turn_in_dialogue_only()
		    && (!quest.has_turn_in_message_id()
		        || quest.message_id() != quest.turn_in_message_id());
		const bool activeTurnIn = quest.turn_in_dialogue_only()
		    ? dialogueTurnIn && !quest.initial() && !quest.completed()
		    : quest.turn_in_activation_only()
		        ? quest.initial() && !quest.completed()
		        : quest.turn_in_progress_only()
		            ? !quest.initial()
		                && !quest.completed()
		                && quest.has_turn_in_required_variable1()
		                && quest.has_turn_in_variable1()
		                && quest.variable_1() == quest.turn_in_required_variable1()
		        : !quest.initial() && !quest.completed();
		const bool completionTurnIn = quest.completed()
		    && quest.turn_in_on_completion()
		    && ((quest.has_turn_in_completion_variable2()
		            && quest.variable_2() < quest.turn_in_completion_variable2())
		        || (!quest.has_turn_in_completion_variable2()
		            && quest.has_turn_in_completion_required_variable1()
		            && quest.has_turn_in_completion_variable1()
		            && quest.variable_1() == quest.turn_in_completion_required_variable1()));
		if (!activeTurnIn && !completionTurnIn)
			continue;
		if (!result.has_value() || quest.quest_id() < *result)
			result = quest.quest_id();
	}
	return result;
}

std::optional<uint32_t> ServerBackedSession::ActiveQuestForUniqueIdentity(std::string_view uniqueIdentity) const noexcept
{
	if (uniqueIdentity.empty())
		return std::nullopt;
	std::optional<uint32_t> result;
	for (const auto &quest : questStates_) {
		if (quest.completed()
		    || quest.unavailable()
		    || quest.completion_unique_identity() != uniqueIdentity)
			continue;
		if (!result.has_value() || quest.quest_id() < *result)
			result = quest.quest_id();
	}
	return result;
}

tl::expected<void, std::string> ServerBackedSession::SetFriendlyMode(bool friendlyMode, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeSetFriendlyModeCommand(friendlyMode, requestedTick), nowMs);
}

tl::expected<void, std::string> ServerBackedSession::AllocateAttribute(ServerBackedPlayerAttribute attribute, uint32_t amount, uint64_t requestedTick, uint64_t nowMs)
{
	return SubmitGameplayCommand(MakeAllocateAttributeCommand(attribute, amount, requestedTick), nowMs);
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
	if (sequence == 0)
		return tl::make_unexpected("The authoritative client sequence space is exhausted.");
	pendingIntents_.emplace(sequence, PendingIntent { .kind = PendingIntent::Kind::Inventory });
	return Flush(nowMs, sequence);
}

tl::expected<void, std::string> ServerBackedSession::SubmitGameplayCommand(tl::expected<protocol::Command, std::string> command, uint64_t nowMs)
{
	lastCommandResolution_ = CommandResolution::None;
	if (!command.has_value())
		return tl::make_unexpected(command.error());
	const uint64_t sequence = client_->QueueCommand(std::move(*command));
	if (sequence == 0)
		return tl::make_unexpected("The authoritative client sequence space is exhausted.");
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
	acceptingReconnectSnapshot_ = true;
	auto snapshot = client_->ReadSnapshot();
	if (!snapshot.has_value()) {
		acceptingReconnectSnapshot_ = false;
		return tl::make_unexpected(snapshot.error());
	}
	if (auto result = ApplySnapshot(*snapshot); !result.has_value())
	{
		acceptingReconnectSnapshot_ = false;
		return tl::make_unexpected(result.error());
	}
	acceptingReconnectSnapshot_ = false;
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
	if (auto geometry = ValidateLevelGeometrySnapshots(snapshot); !geometry.has_value())
		return tl::make_unexpected(geometry.error());
	if (auto origins = ValidateSetLevelObjectOriginSnapshots(snapshot); !origins.has_value())
		return tl::make_unexpected(origins.error());
	if (auto proceduralLevels = ValidateMaterializedProceduralLevelSnapshots(snapshot); !proceduralLevels.has_value())
		return tl::make_unexpected(proceduralLevels.error());
	if (auto positions = ValidateEntityPositionSnapshots(snapshot); !positions.has_value())
		return tl::make_unexpected(positions.error());
	if (auto identities = ValidateEntityIdentitySnapshots(snapshot); !identities.has_value())
		return tl::make_unexpected(identities.error());
	if (auto actions = ValidatePlayerActionSnapshots(snapshot); !actions.has_value())
		return tl::make_unexpected(actions.error());
	if (auto monsters = ValidateMonsterSnapshots(snapshot); !monsters.has_value())
		return tl::make_unexpected(monsters.error());
	if (auto projectiles = ValidateProjectileSnapshots(snapshot); !projectiles.has_value())
		return tl::make_unexpected(projectiles.error());
	if (auto quests = ValidateQuestSnapshots(snapshot); !quests.has_value())
		return tl::make_unexpected(quests.error());
	if (auto interactions = ValidateQuestTownerInteractions(snapshot); !interactions.has_value())
		return tl::make_unexpected(interactions.error());
	if (auto greetings = ValidateTownerGreetingSnapshots(snapshot); !greetings.has_value())
		return tl::make_unexpected(greetings.error());
	auto sessionMetadata = ProjectSessionMetadata(snapshot);
	if (!sessionMetadata.has_value())
		return tl::make_unexpected(sessionMetadata.error());
	auto levelGeometry = ProjectLevelGeometrySnapshots(snapshot);
	if (!levelGeometry.has_value())
		return tl::make_unexpected(levelGeometry.error());
	const uint32_t requestedEntityId = entityId_ == 0 && snapshot.players_size() > 0
	    ? snapshot.players(0).entity_id()
	    : entityId_;
	if (requestedEntityId == 0)
		return tl::make_unexpected("Server-backed snapshot contains no player entity.");
	auto players = ProjectPlayerSnapshots(snapshot);
	if (!players.has_value())
		return tl::make_unexpected(players.error());
	const auto player = std::find_if(players->begin(), players->end(), [&](const auto &candidate) {
		return candidate.entityId == requestedEntityId;
	});
	if (player == players->end())
		return tl::make_unexpected("Server-backed snapshot does not contain the requested player entity.");
	const bool hasActiveStore = snapshot.has_active_store();
	if (hasActiveStore != player->activeStoreId.has_value()
	    || (hasActiveStore && *player->activeStoreId != snapshot.active_store().store_id())) {
		return tl::make_unexpected("Server-backed player and vendor snapshots disagree about the active store.");
	}
	if (const auto *previous = playerState_.Snapshot(); previous != nullptr
	    && player->serverTick < previous->serverTick
	    && !acceptingReconnectSnapshot_)
		return { };
	auto monsters = ProjectMonsterSnapshots(snapshot);
	if (!monsters.has_value())
		return tl::make_unexpected(monsters.error());
	auto worldItems = ProjectWorldItemSnapshots(snapshot);
	if (!worldItems.has_value())
		return tl::make_unexpected(worldItems.error());
	auto objects = ProjectObjectSnapshots(snapshot);
	if (!objects.has_value())
		return tl::make_unexpected(objects.error());
	auto projectiles = ProjectProjectileSnapshots(snapshot);
	if (!projectiles.has_value())
		return tl::make_unexpected(projectiles.error());
	auto portals = ProjectPortalSnapshots(snapshot);
	if (!portals.has_value())
		return tl::make_unexpected(portals.error());
	auto corpses = ProjectCorpseSnapshots(snapshot);
	if (!corpses.has_value())
		return tl::make_unexpected(corpses.error());
	auto quests = ProjectQuestSnapshots(snapshot);
	if (!quests.has_value())
		return tl::make_unexpected(quests.error());
	std::optional<ProjectedVendorSnapshot> vendor;
	if (snapshot.has_active_store()) {
		auto projectedVendor = ProjectVendorSnapshot(snapshot);
		if (!projectedVendor.has_value())
			return tl::make_unexpected(projectedVendor.error());
		vendor = std::move(*projectedVendor);
	}
	ServerBackedPlayerState candidatePlayerState = playerState_;
	if (acceptingReconnectSnapshot_)
		candidatePlayerState.Clear();
	if (!candidatePlayerState.ApplySnapshot(*player))
		return tl::make_unexpected("Server-backed player snapshot could not be applied.");
	ServerBackedVendorState candidateVendorState = vendorState_;
	candidateVendorState.SetEnabled(true);
	candidateVendorState.SetConnected(true);
	if (vendor.has_value()) {
		if (!candidateVendorState.ApplySnapshot(std::move(*vendor)))
			return tl::make_unexpected("Server-backed vendor snapshot could not be applied.");
	} else {
		candidateVendorState.ClearSnapshot();
	}
	entityId_ = requestedEntityId;
	playerState_ = std::move(candidatePlayerState);
	playerStates_ = std::move(*players);
	monsterState_ = std::move(*monsters);
	worldItemState_ = std::move(*worldItems);
	objectState_ = std::move(*objects);
	projectileState_ = std::move(*projectiles);
	portalState_ = std::move(*portals);
	corpseState_ = std::move(*corpses);
	questState_ = std::move(*quests);
	sessionMetadata_ = std::move(*sessionMetadata);
	levelGeometryState_ = std::move(*levelGeometry);
	questStates_.assign(snapshot.quests().begin(), snapshot.quests().end());
	questTownerInteractions_.assign(
		snapshot.quest_towner_interactions().begin(),
		snapshot.quest_towner_interactions().end());
	vendorState_ = std::move(candidateVendorState);
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
		const bool rejectedDuplicate = result.status() == protocol::COMMAND_STATUS_DUPLICATE
		    && result.reject_reason() != protocol::COMMAND_REJECT_REASON_UNSPECIFIED;
		if (pending->second.kind == PendingIntent::Kind::Purchase) {
			if (result.status() == protocol::COMMAND_STATUS_REJECTED || rejectedDuplicate)
				(void)vendorState_.ResolvePurchase(pending->second.storeId, pending->second.storeSlot, PurchaseResolution::Rejected);
			else if (result.status() == protocol::COMMAND_STATUS_ACCEPTED
			    || result.status() == protocol::COMMAND_STATUS_DUPLICATE)
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
				lastCommandResolution_ = rejectedDuplicate ? CommandResolution::Rejected : CommandResolution::Duplicate;
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

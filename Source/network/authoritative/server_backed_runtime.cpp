#include "network/authoritative/server_backed_runtime.hpp"

#include <array>

#include "network/authoritative/server_backed_player_ui.hpp"
#include "network/authoritative/protocol_limits.hpp"
#include "network/protocol/multi.h"
#include "engine/cursor.h"
#include <algorithm>
#include <exception>
#include <filesystem>
#include <fstream>
#include <utility>

namespace devilution::authoritative {
namespace {

constexpr uint32_t SmithStoreId = 1;
constexpr uint32_t WitchStoreId = 2;
constexpr uint32_t WirtStoreId = 3;
constexpr uint32_t HealerStoreId = 4;
constexpr uint32_t AdriaStoreId = 10;
constexpr uint32_t StorytellerStoreId = 11;

bool SameItemReference(const ServerBackedItemReference &left, const ServerBackedItemReference &right)
{
	return left.location == right.location
	    && left.slot == right.slot
	    && left.page == right.page
	    && left.stableItemId == right.stableItemId;
}

std::string LoadResumeToken(const std::string &path)
{
	if (path.empty())
		return { };
	std::ifstream tokenFile(path, std::ios::binary);
	if (!tokenFile)
		return { };
	std::string token;
	std::getline(tokenFile, token);
	return token;
}

tl::expected<void, std::string> SaveResumeToken(const std::string &path, const std::string &token)
{
	if (path.empty() || token.empty())
		return { };
	try {
		const std::filesystem::path tokenPath(path);
		if (tokenPath.has_parent_path())
			std::filesystem::create_directories(tokenPath.parent_path());
		std::ofstream tokenFile(tokenPath, std::ios::binary | std::ios::trunc);
		if (!tokenFile)
			return tl::make_unexpected("Could not write the authoritative resume token to " + tokenPath.string() + ".");
		tokenFile << token << '\n';
		return { };
	} catch (const std::exception &exception) {
		return tl::make_unexpected("Could not persist the authoritative resume token: " + std::string(exception.what()));
	}
}

} // namespace

tl::expected<void, std::string> ServerBackedRuntime::Start(const ServerBackedRuntimeConfiguration &configuration, StoreManager &storeManager)
{
	return StartImpl(configuration, storeManager, nullptr);
}

tl::expected<void, std::string> ServerBackedRuntime::Start(const ServerBackedRuntimeConfiguration &configuration, StoreManager &storeManager, Player &player)
{
	return StartImpl(configuration, storeManager, &player);
}

tl::expected<void, std::string> ServerBackedRuntime::StartImpl(const ServerBackedRuntimeConfiguration &configuration, StoreManager &storeManager, Player *player)
{
	Stop();
	if (!configuration.enabled && configuration.mode != ServerBackedRuntimeConfiguration::GameMode::Authoritative)
		return { };
	if (configuration.clientBuildId.empty() || configuration.protocolSchemaVersion.empty() || configuration.contentManifestHash.empty())
		return tl::make_unexpected("Server-backed runtime requires client build, protocol, and content identity values.");
	if (configuration.protocolSchemaVersion == "0.2.0" && configuration.rulesetIdentityHash.empty())
		return tl::make_unexpected("Protocol v0.2 requires a gameplay ruleset identity value.");
	authoritativeMode_ = true;
	const std::string resumeToken = configuration.resumeToken.empty() ? LoadResumeToken(configuration.resumeTokenPath) : configuration.resumeToken;

	ServerBackedSession::Configuration sessionConfiguration {
		.client = {
		    .host = configuration.host,
		    .port = configuration.port,
		    .clientBuildId = configuration.clientBuildId,
		    .protocolSchemaVersion = configuration.protocolSchemaVersion,
		    .contentManifestHash = configuration.contentManifestHash,
		    .rulesetIdentityHash = configuration.rulesetIdentityHash,
		    .resumeToken = resumeToken,
		    .requestedCharacterClassId = player == nullptr
		        ? std::nullopt
		        : std::optional<uint32_t> { static_cast<uint32_t>(player->_pClass) },
		    .requestedCharacterName = player == nullptr
		        ? std::nullopt
		        : std::optional<std::string> { player->_pName },
		    .diagnosticsDirectory = configuration.diagnosticsDirectory,
		    .expectInitialSnapshot = true,
		},
	};
	auto session = ServerBackedSession::Connect(std::move(sessionConfiguration));
	if (!session.has_value()) {
		authoritativeMode_ = false;
		return tl::make_unexpected(session.error());
	}

	session_ = std::move(*session);
	const auto gameMode = session_->Client().ServerHello().game_mode();
	switch (gameMode) {
	case protocol::GAME_MODE_UNSPECIFIED:
		// Additive compatibility: older servers omit the field, so retain the
		// launch-selected legacy mode until the server declares one explicitly.
		break;
	case protocol::GAME_MODE_SINGLE_PLAYER:
		gbIsMultiplayer = false;
		break;
	case protocol::GAME_MODE_MULTIPLAYER:
		gbIsMultiplayer = true;
		break;
	default:
		Stop();
		return tl::make_unexpected("Authoritative server declared an invalid game mode.");
	}
	if (auto result = SaveResumeToken(configuration.resumeTokenPath, session_->Client().ServerHello().session_token()); !result.has_value()) {
		Stop();
		return result;
	}
	vendorUiAdapter_ = std::make_unique<ServerBackedVendorUiAdapter>(storeManager);
	player_ = player;
	if (player_ != nullptr) {
		if (auto result = ApplyCurrentPlayerSnapshot(); !result.has_value()) {
			Stop();
			return result;
		}
	}
	return { };
}

void ServerBackedRuntime::Stop() noexcept
{
	if (session_)
		session_->Close();
	vendorUiAdapter_.reset();
	session_.reset();
	authoritativeMode_ = false;
	pendingInventoryItemDrag_.reset();
	pendingInventoryItemDragSeed_.reset();
	pendingOilUse_.reset();
	playerPresentationGraphic_.reset();
	playerPresentationActionKind_.reset();
	playerPresentationActionStartedAtTick_.reset();
	playerPresentationLevelId_.reset();
	playerPresentationServerTick_.reset();
	playerPresentationTicksSinceServerTick_ = 0;
	worldProjection_.Clear();
	worldPresentation_.Clear();
	player_ = nullptr;
}

void ServerBackedRuntime::AdvancePresentationAnimations() noexcept
{
	(void)worldPresentation_.AdvanceAnimations();
	if (player_ == nullptr || !playerPresentationGraphic_.has_value())
		return;
	const bool loop = *playerPresentationGraphic_ == player_graphic::Stand
	    || *playerPresentationGraphic_ == player_graphic::Walk;
	AdvanceProjectedPlayerAnimation(*player_, *playerPresentationGraphic_, 1, loop);
	++playerPresentationTicksSinceServerTick_;
}

tl::expected<void, std::string> ServerBackedRuntime::OpenSmithStore(uint64_t requestedTick, uint64_t nowMs)
{
	return OpenVendor(SmithStoreId, ServerBackedVendorDestination::Smith, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedRuntime::OpenWitchStore(uint64_t requestedTick, uint64_t nowMs)
{
	return OpenVendor(WitchStoreId, ServerBackedVendorDestination::Witch, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedRuntime::OpenWirtStore(uint64_t requestedTick, uint64_t nowMs)
{
	return OpenVendor(WirtStoreId, ServerBackedVendorDestination::Wirt, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedRuntime::OpenHealerStore(uint64_t requestedTick, uint64_t nowMs)
{
	return OpenVendor(HealerStoreId, ServerBackedVendorDestination::Healer, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedRuntime::OpenVendor(uint32_t storeId, ServerBackedVendorDestination destination, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_ || !vendorUiAdapter_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->OpenVendor(storeId, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	const ProjectedVendorSnapshot *snapshot = session_->VendorState().Snapshot();
	if (snapshot == nullptr)
		return tl::make_unexpected("The authoritative vendor returned no stock snapshot.");
	return vendorUiAdapter_->Apply(*snapshot, destination, storeId);
}

tl::expected<void, std::string> ServerBackedRuntime::CloseVendor(uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->CloseVendor(requestedTick, nowMs); !result.has_value())
		return result;
	return EnsureCommandAccepted();
}

tl::expected<void, std::string> ServerBackedRuntime::OpenStash(uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->OpenStash(requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::CloseStash(uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->CloseStash(requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::DepositStashGold(uint32_t amount, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->DepositStashGold(amount, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::WithdrawStashGold(uint32_t amount, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->WithdrawStashGold(amount, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::SplitGold(ServerBackedItemReference item, uint32_t amount, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->SplitGold(item, amount, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::SortInventory(uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->SortInventory(requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::PurchaseVendor(uint32_t storeId, uint32_t storeSlot, ServerBackedVendorDestination destination, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_ || !vendorUiAdapter_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->Purchase(storeId, storeSlot, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	if (auto result = ApplyCurrentPlayerSnapshot(); !result.has_value())
		return result;
	const ProjectedVendorSnapshot *snapshot = session_->VendorState().Snapshot();
	if (snapshot == nullptr)
		return tl::make_unexpected("The authoritative vendor purchase returned no stock snapshot.");
	return vendorUiAdapter_->Apply(*snapshot, destination, storeId);
}

tl::expected<void, std::string> ServerBackedRuntime::OpenAdriaStore(uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->OpenVendor(AdriaStoreId, requestedTick, nowMs); !result.has_value())
		return result;
	return EnsureCommandAccepted();
}

tl::expected<void, std::string> ServerBackedRuntime::OpenStorytellerStore(uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->OpenVendor(StorytellerStoreId, requestedTick, nowMs); !result.has_value())
		return result;
	return EnsureCommandAccepted();
}

tl::expected<void, std::string> ServerBackedRuntime::CloseStorytellerStore(uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	const auto *snapshot = session_->VendorState().Snapshot();
	if (snapshot == nullptr)
		return { };
	if (snapshot->storeId != StorytellerStoreId)
		return tl::make_unexpected("A different server-backed vendor is active.");
	return CloseVendor(requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedRuntime::PurchaseSmith(uint32_t storeSlot, uint64_t requestedTick, uint64_t nowMs)
{
	return PurchaseVendor(SmithStoreId, storeSlot, ServerBackedVendorDestination::Smith, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedRuntime::PurchaseWitch(uint32_t storeSlot, uint64_t requestedTick, uint64_t nowMs)
{
	return PurchaseVendor(WitchStoreId, storeSlot, ServerBackedVendorDestination::Witch, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedRuntime::PurchaseWirt(uint32_t storeSlot, uint64_t requestedTick, uint64_t nowMs)
{
	return PurchaseVendor(WirtStoreId, storeSlot, ServerBackedVendorDestination::Wirt, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedRuntime::PurchaseHealer(uint32_t storeSlot, uint64_t requestedTick, uint64_t nowMs)
{
	return PurchaseVendor(HealerStoreId, storeSlot, ServerBackedVendorDestination::Healer, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedRuntime::SellItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->SellItem(inventoryIndex, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::SellItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->SellItem(item, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::RepairItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->RepairItem(inventoryIndex, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::RepairItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->RepairItem(item, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::RepairItemWithAbility(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->RepairItemWithAbility(item, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::RechargeItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->RechargeItem(inventoryIndex, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::RechargeItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->RechargeItem(item, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::RechargeItemWithAbility(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->RechargeItemWithAbility(item, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::IdentifyItem(uint32_t inventoryIndex, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->IdentifyItem(inventoryIndex, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::IdentifyItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->IdentifyItem(item, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::IdentifyItemWithAbility(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->IdentifyItemWithAbility(item, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::ReadBook(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->ReadBook(item, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::UseConsumable(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->UseConsumable(item, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::UseConsumable(ServerBackedItemReference item, ServerBackedItemReference target, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->UseConsumable(item, target, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	pendingOilUse_.reset();
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::RefillMana(uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->RefillMana(requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::MoveInventoryItem(uint32_t inventoryIndex, uint32_t targetCell, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->MoveInventoryItem(inventoryIndex, targetCell, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::MoveInventoryItem(ServerBackedItemReference item, uint32_t targetCell, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->MoveInventoryItem(item, targetCell, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::MoveItem(ServerBackedItemReference item, ServerBackedItemReference destination, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->MoveItem(item, destination, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = ApplyCurrentPlayerSnapshot(); !result.has_value())
		return result;
	return EnsureCommandAccepted();
}

tl::expected<void, std::string> ServerBackedRuntime::AutoEquipItem(ServerBackedItemReference item, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->AutoEquipItem(item, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::AutoEquipItem(ServerBackedItemReference item, uint32_t equipmentSlot, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->AutoEquipItem(item, equipmentSlot, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::Move(int32_t directionX, int32_t directionY, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->Move(directionX, directionY, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::MoveTo(int32_t targetX, int32_t targetY, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->MoveTo(targetX, targetY, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::Attack(uint32_t targetEntityId, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->Attack(targetEntityId, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::AttackAt(int32_t targetX, int32_t targetY, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->AttackAt(targetX, targetY, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::Cast(uint32_t spellId, uint32_t targetEntityId, uint64_t requestedTick, uint64_t nowMs)
{
	return Cast(spellId, targetEntityId, 0, 0, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedRuntime::Cast(uint32_t spellId, uint32_t targetEntityId, int32_t targetX, int32_t targetY, uint64_t requestedTick, uint64_t nowMs)
{
	return Cast(spellId, targetEntityId, targetX, targetY, { }, requestedTick, nowMs);
}

tl::expected<void, std::string> ServerBackedRuntime::Cast(uint32_t spellId, uint32_t targetEntityId, int32_t targetX, int32_t targetY, ServerBackedCastSource source, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->Cast(spellId, targetEntityId, targetX, targetY, source, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::UsePortal(uint32_t portalId, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->UsePortal(portalId, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<bool, std::string> ServerBackedRuntime::UsePortalAt(int32_t positionX, int32_t positionY, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	const auto portalId = PortalEntityIdAt(positionX, positionY);
	if (!portalId.has_value())
		return false;
	if (auto result = UsePortal(*portalId, requestedTick, nowMs); !result.has_value())
		return tl::make_unexpected(result.error());
	return true;
}

tl::expected<void, std::string> ServerBackedRuntime::PickupWorldItem(uint32_t itemEntityId, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->PickupWorldItem(itemEntityId, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::DropHeldItem(int32_t targetX, int32_t targetY, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->DropHeldItem(targetX, targetY, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::DropItem(ServerBackedItemReference item, int32_t targetX, int32_t targetY, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->DropItem(item, targetX, targetY, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::OperateObject(uint32_t objectEntityId, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->OperateObject(objectEntityId, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::DisarmTrap(uint32_t objectEntityId, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->DisarmTrap(objectEntityId, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::AdvanceQuest(uint32_t questId, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->AdvanceQuest(questId, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::TalkToQuest(uint32_t questId, uint32_t targetEntityId, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->TalkToQuest(questId, targetEntityId, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::TurnInQuest(uint32_t questId, uint32_t townerId, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->TurnInQuest(questId, townerId, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::TalkToTowner(uint32_t townerId, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->TalkToTowner(townerId, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

bool ServerBackedRuntime::HasActiveQuest(uint32_t questId) const noexcept
{
	return session_ != nullptr && session_->HasActiveQuest(questId);
}

std::optional<uint32_t> ServerBackedRuntime::ActiveQuestForTowner(uint32_t townerId) const noexcept
{
	return session_ == nullptr
	    ? std::nullopt
	    : session_->ActiveQuestForTowner(townerId);
}

std::optional<uint32_t> ServerBackedRuntime::ActiveQuestForMonster(uint32_t monsterEntityId) const noexcept
{
	if (session_ == nullptr || monsterEntityId == 0)
		return std::nullopt;
	const auto monster = std::find_if(worldProjection_.Monsters().begin(), worldProjection_.Monsters().end(), [monsterEntityId](const auto &candidate) {
		return candidate.entityId == monsterEntityId;
	});
	if (monster == worldProjection_.Monsters().end() || !monster->alive || !monster->isUnique || monster->uniqueIdentity.empty())
		return std::nullopt;
	return session_->ActiveQuestForUniqueIdentity(monster->uniqueIdentity);
}

tl::expected<void, std::string> ServerBackedRuntime::SetFriendlyMode(bool friendlyMode, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->SetFriendlyMode(friendlyMode, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::AllocateAttribute(ServerBackedPlayerAttribute attribute, uint32_t amount, uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->AllocateAttribute(attribute, amount, requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

tl::expected<void, std::string> ServerBackedRuntime::RestartTown(uint64_t requestedTick, uint64_t nowMs)
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	if (auto result = session_->RestartTown(requestedTick, nowMs); !result.has_value())
		return result;
	if (auto result = EnsureCommandAccepted(); !result.has_value())
		return result;
	if (auto result = ApplyCurrentPlayerSnapshot(); !result.has_value())
		return result;
	if (player_ != nullptr)
		PresentRestartTown(*player_);
	return { };
}

tl::expected<void, std::string> ServerBackedRuntime::Poll(uint64_t nowMs)
{
	if (!session_)
		return { };
	if (auto result = session_->Poll(nowMs); !result.has_value())
		return result;
	return ApplyCurrentPlayerSnapshot();
}

std::optional<uint32_t> ServerBackedRuntime::SmithStoreSlotAt(std::size_t index) const noexcept
{
	return StoreSlotAt(index);
}

std::optional<uint32_t> ServerBackedRuntime::StoreSlotAt(std::size_t index) const noexcept
{
	if (!vendorUiAdapter_)
		return std::nullopt;
	return vendorUiAdapter_->StoreSlotAt(index);
}

std::optional<uint32_t> ServerBackedRuntime::MonsterEntityIdAt(std::size_t index) const noexcept
{
	if (index >= worldProjection_.Monsters().size())
		return std::nullopt;
	const auto entityId = worldProjection_.Monsters()[index].entityId;
	return entityId == 0 ? std::nullopt : std::optional<uint32_t> { entityId };
}

std::optional<uint32_t> ServerBackedRuntime::WorldItemEntityIdAt(int32_t positionX, int32_t positionY) const noexcept
{
	return worldProjection_.WorldItemAt(positionX, positionY);
}

std::optional<uint32_t> ServerBackedRuntime::MonsterEntityIdAt(int32_t positionX, int32_t positionY) const noexcept
{
	if (!session_)
		return worldProjection_.MonsterAt(positionX, positionY);
	const auto *local = session_->PlayerState().Snapshot();
	if (local == nullptr)
		return worldProjection_.MonsterAt(positionX, positionY);
	return worldProjection_.MonsterAt(
	    positionX,
	    positionY,
	    local->positionX,
	    local->positionY,
	    local->lightRadius);
}

std::optional<uint32_t> ServerBackedRuntime::PlayerEntityIdAt(int32_t positionX, int32_t positionY) const noexcept
{
	if (!session_)
		return std::nullopt;
	const auto *local = session_->PlayerState().Snapshot();
	if (local == nullptr)
		return std::nullopt;
	for (const auto &player : session_->PlayerStates()) {
		if (player.entityId == 0 || player.entityId == session_->EntityId()
		    || !IsAuthoritativeLevelVisible(player.levelId, local->levelId)) {
			continue;
		}
		const bool atCurrentPosition = player.positionX == positionX
		    && player.positionY == positionY;
		const bool atFuturePosition = player.currentAction.has_value()
		    && player.currentAction->kind == 1
		    && !player.currentAction->effectApplied
		    && player.positionX + player.currentAction->directionX == positionX
		    && player.positionY + player.currentAction->directionY == positionY;
		if (atCurrentPosition || atFuturePosition)
			return player.entityId;
	}
	return std::nullopt;
}

std::optional<uint32_t> ServerBackedRuntime::ObjectEntityIdAt(int32_t positionX, int32_t positionY) const noexcept
{
	return worldProjection_.ObjectAt(positionX, positionY);
}

std::optional<uint32_t> ServerBackedRuntime::QuestObjectEntityIdAt(int32_t positionX, int32_t positionY) const noexcept
{
	return worldProjection_.QuestObjectAt(positionX, positionY);
}

std::optional<uint32_t> ServerBackedRuntime::AuthoritativeObjectEntityIdAt(int32_t positionX, int32_t positionY) const noexcept
{
	return worldProjection_.AuthoritativeObjectAt(positionX, positionY);
}

std::optional<uint32_t> ServerBackedRuntime::PortalEntityIdAt(int32_t positionX, int32_t positionY) const noexcept
{
	if (!session_)
		return std::nullopt;
	const auto *local = session_->PlayerState().Snapshot();
	if (local == nullptr)
		return std::nullopt;
	for (const auto &portal : session_->PortalState()) {
		const bool atSource = portal.sourceLevelId == local->levelId
		    && portal.sourcePositionX == positionX
		    && portal.sourcePositionY == positionY;
		const bool atDestination = portal.bidirectional
		    && portal.destinationLevelId == local->levelId
		    && portal.destinationPositionX == positionX
		    && portal.destinationPositionY == positionY;
		if (atSource || atDestination)
			return portal.portalId;
	}
	return std::nullopt;
}

std::optional<ProjectedSpellTargetingKind> ServerBackedRuntime::SpellTargetingKindForSpell(uint32_t spellId) const noexcept
{
	if (!session_)
		return std::nullopt;
	const auto *snapshot = session_->PlayerState().Snapshot();
	if (snapshot == nullptr)
		return std::nullopt;
	return ProjectedSpellTargetingKindForSpell(*snapshot, spellId);
}

std::optional<ServerBackedItemReference> ServerBackedRuntime::PlayerItemReferenceForSeed(uint32_t itemSeed) const noexcept
{
	if (!session_)
		return std::nullopt;
	const ProjectedPlayerSnapshot *snapshot = session_->PlayerState().Snapshot();
	if (snapshot == nullptr)
		return std::nullopt;
	for (std::size_t index = 0; index < snapshot->inventory.size(); ++index) {
		if (snapshot->inventory[index].itemSeed == itemSeed)
			return ServerBackedItemReference {
			    .location = ServerBackedItemLocation::Inventory,
			    .slot = static_cast<uint32_t>(index),
			    .stableItemId = snapshot->inventory[index].stableItemId,
			};
	}
	for (const auto &beltItem : snapshot->belt) {
		if (beltItem.itemSeed == itemSeed)
			return ServerBackedItemReference {
			    .location = ServerBackedItemLocation::Belt,
			    .slot = beltItem.slot,
			    .stableItemId = beltItem.stableItemId,
			};
	}
	for (const auto &equipment : snapshot->equipment) {
		if (equipment.itemSeed == itemSeed)
			return ServerBackedItemReference {
			    .location = ServerBackedItemLocation::Equipment,
			    .slot = equipment.slot,
			    .stableItemId = equipment.stableItemId,
			};
	}
	for (const auto &stashItem : snapshot->stash) {
		if (stashItem.itemSeed == itemSeed)
			return ServerBackedItemReference {
			    .location = ServerBackedItemLocation::Stash,
			    .slot = stashItem.cell,
			    .stableItemId = stashItem.stableItemId,
			    .page = stashItem.page,
			};
	}
	return std::nullopt;
}

std::optional<ServerBackedItemReference> ServerBackedRuntime::StashItemReferenceAt(uint32_t page, uint32_t cell) const noexcept
{
	if (!session_ || page >= NativeStashPageCount || cell >= NativeStashCellCount)
		return std::nullopt;
	const auto *snapshot = session_->PlayerState().Snapshot();
	if (snapshot == nullptr)
		return std::nullopt;
	const uint32_t targetColumn = cell % NativeStashGridWidth;
	const uint32_t targetRow = cell / NativeStashGridWidth;
	for (const auto &stashItem : snapshot->stash) {
		const uint32_t itemColumn = stashItem.cell % NativeStashGridWidth;
		const uint32_t itemRow = stashItem.cell / NativeStashGridWidth;
		if (stashItem.page == page
		    && targetColumn >= itemColumn
		    && targetColumn < itemColumn + stashItem.width
		    && targetRow >= itemRow
		    && targetRow < itemRow + stashItem.height)
			return ServerBackedItemReference {
			    .location = ServerBackedItemLocation::Stash,
			    .slot = stashItem.cell,
			    .stableItemId = stashItem.stableItemId,
			    .page = page,
			};
	}
	return std::nullopt;
}

std::optional<ServerBackedItemReference> ServerBackedRuntime::FindStashPlacement(uint32_t width, uint32_t height, uint32_t preferredPage) const noexcept
{
	if (!session_ || width == 0 || height == 0 || width > NativeStashGridWidth || height > NativeStashGridHeight || preferredPage >= NativeStashPageCount)
		return std::nullopt;
	const auto *snapshot = session_->PlayerState().Snapshot();
	if (snapshot == nullptr)
		return std::nullopt;
	for (uint32_t pageOffset = 0; pageOffset < NativeStashPageCount; ++pageOffset) {
		const uint32_t page = (preferredPage + pageOffset) % NativeStashPageCount;
		std::array<bool, NativeStashCellCount> occupied { };
		for (const auto &stashItem : snapshot->stash) {
			if (stashItem.page != page)
				continue;
			const uint32_t itemColumn = stashItem.cell % NativeStashGridWidth;
			const uint32_t itemRow = stashItem.cell / NativeStashGridWidth;
			for (uint32_t y = 0; y < stashItem.height; ++y) {
				for (uint32_t x = 0; x < stashItem.width; ++x)
					occupied[(itemRow + y) * NativeStashGridWidth + itemColumn + x] = true;
			}
		}
		for (uint32_t row = 0; row + height <= NativeStashGridHeight; ++row) {
			for (uint32_t column = 0; column + width <= NativeStashGridWidth; ++column) {
				bool fits = true;
				for (uint32_t y = 0; y < height && fits; ++y) {
					for (uint32_t x = 0; x < width; ++x) {
						if (occupied[(row + y) * NativeStashGridWidth + column + x]) {
							fits = false;
							break;
						}
					}
				}
				if (fits)
					return ServerBackedItemReference {
					    .location = ServerBackedItemLocation::Stash,
					    .slot = row * 10 + column,
					    .stableItemId = 0,
					    .page = page,
					};
			}
		}
	}
	return std::nullopt;
}

bool ServerBackedRuntime::BeginInventoryItemDrag(uint32_t itemSeed) noexcept
{
	const auto reference = PlayerItemReferenceForSeed(itemSeed);
	if (!reference.has_value())
		return false;
	pendingInventoryItemDrag_ = *reference;
	pendingInventoryItemDragSeed_ = itemSeed;
	return true;
}

void ServerBackedRuntime::CancelInventoryItemDrag() noexcept
{
	pendingInventoryItemDrag_.reset();
	pendingInventoryItemDragSeed_.reset();
}

bool ServerBackedRuntime::BeginOilUse(ServerBackedItemReference item) noexcept
{
	if (!session_)
		return false;
	pendingOilUse_ = item;
	return true;
}

std::optional<ServerBackedItemReference> ServerBackedRuntime::PendingOilUse() const noexcept
{
	return pendingOilUse_;
}

void ServerBackedRuntime::CancelOilUse() noexcept
{
	pendingOilUse_.reset();
}

tl::expected<void, std::string> ServerBackedRuntime::ApplyCurrentPlayerSnapshot()
{
	if (!session_ || player_ == nullptr)
		return { };
	const ProjectedPlayerSnapshot *snapshot = session_->PlayerState().Snapshot();
	if (snapshot == nullptr)
		return tl::make_unexpected("The server-backed session has no player snapshot.");
	const player_graphic graphic = GetRemotePlayerGraphic(*snapshot);
	const bool classChanged = player_->_pClass != static_cast<HeroClass>(snapshot->characterClassId);
	const bool equipmentChanged = player_->_pgfxnum != GetRemotePlayerEquipmentGraphicBits(*snapshot);
	const std::optional<uint32_t> actionKind = snapshot->currentAction.has_value()
	    ? std::optional<uint32_t>(snapshot->currentAction->kind)
	    : std::nullopt;
	const std::optional<uint64_t> actionStartedAtTick = snapshot->currentAction.has_value()
	    ? std::optional<uint64_t>(snapshot->currentAction->startedAtTick)
	    : std::nullopt;
	const bool animationChanged = classChanged || equipmentChanged
	    || !playerPresentationGraphic_.has_value()
	    || *playerPresentationGraphic_ != graphic
	    || playerPresentationActionKind_ != actionKind
	    || playerPresentationActionStartedAtTick_ != actionStartedAtTick
	    || !playerPresentationLevelId_.has_value()
	    || *playerPresentationLevelId_ != snapshot->levelId
	    || player_->direction != static_cast<Direction>(snapshot->facingDirection);
	const bool hasPreviousTick = playerPresentationServerTick_.has_value();
	const uint64_t previousTick = playerPresentationServerTick_.value_or(0);
	const uint64_t presentationTicksSincePreviousSnapshot = playerPresentationTicksSinceServerTick_;
	std::optional<Item> pendingItem;
	if (pendingInventoryItemDrag_.has_value() && pendingInventoryItemDragSeed_.has_value()) {
		if (!player_->HoldItem.isEmpty()
		    && player_->HoldItem._iSeed == *pendingInventoryItemDragSeed_) {
			pendingItem = player_->HoldItem;
		} else if (player_->HoldItem.isEmpty()) {
			CancelInventoryItemDrag();
		}
	}
	ApplyServerBackedQuestSnapshots(session_->QuestState());
	if (auto result = worldProjection_.Apply(session_->MonsterState(), session_->WorldItemState(), session_->ObjectState(), session_->ProjectileState(), session_->LevelGeometryState(), snapshot->levelId, snapshot->serverTick); !result.has_value())
		return result;
	if (auto result = worldPresentation_.Apply(
			worldProjection_,
			snapshot->levelId,
			session_->PlayerStates(),
			session_->EntityId());
		!result.has_value())
		return result;
	for (const auto &eventBatch : session_->TakePendingEventBatches()) {
		if (auto result = ValidateServerBackedEventBatch(eventBatch); !result.has_value())
			return result;
		if (eventBatch.tick() < snapshot->serverTick)
			continue;
		ApplyServerBackedEventBatch(*player_, eventBatch, session_->EntityId());
	}
	if (auto result = ApplyServerBackedPlayerSnapshot(*player_, *snapshot); !result.has_value())
		return result;
	if (classChanged || equipmentChanged)
		RefreshProjectedPlayerGraphics(*player_, *snapshot);
	ApplyProjectedPlayerAnimation(
	    *player_,
	    *snapshot,
	    graphic,
	    animationChanged,
	    hasPreviousTick,
	    previousTick,
	    presentationTicksSincePreviousSnapshot);
	playerPresentationGraphic_ = graphic;
	playerPresentationActionKind_ = actionKind;
	playerPresentationActionStartedAtTick_ = actionStartedAtTick;
	playerPresentationLevelId_ = snapshot->levelId;
	playerPresentationServerTick_ = snapshot->serverTick;
	if (animationChanged || snapshot->currentAction.has_value() || snapshot->serverTick > previousTick)
		playerPresentationTicksSinceServerTick_ = 0;
	if (auto result = ApplyServerBackedStashProjection(*snapshot); !result.has_value())
		return result;
	if (!pendingInventoryItemDrag_.has_value() || !pendingItem.has_value())
		return { };
	const auto currentReference = PlayerItemReferenceForSeed(*pendingInventoryItemDragSeed_);
	if (!currentReference.has_value() || !SameItemReference(*currentReference, *pendingInventoryItemDrag_)) {
		CancelInventoryItemDrag();
		return { };
	}
	player_->HoldItem = *pendingItem;
	NewCursor(player_->HoldItem);
	return { };
}

tl::expected<void, std::string> ServerBackedRuntime::EnsureCommandAccepted() const
{
	if (!session_)
		return tl::make_unexpected("The server-backed runtime is not connected.");
	switch (session_->LastCommandResolution()) {
	case ServerBackedSession::CommandResolution::Accepted:
	case ServerBackedSession::CommandResolution::Duplicate:
		return { };
	case ServerBackedSession::CommandResolution::Rejected:
		return tl::make_unexpected("The authoritative server rejected the command.");
	case ServerBackedSession::CommandResolution::Rescheduled:
		return tl::make_unexpected("The authoritative server rescheduled the command; it was not applied locally.");
	case ServerBackedSession::CommandResolution::None:
		return tl::make_unexpected("The authoritative server did not resolve the command.");
	}
	return tl::make_unexpected("The authoritative server returned an unknown command status.");
}

ServerBackedRuntime &GetServerBackedRuntime()
{
	static ServerBackedRuntime runtime;
	return runtime;
}

} // namespace devilution::authoritative

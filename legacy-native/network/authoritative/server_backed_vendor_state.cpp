/**
 * @file network/authoritative/server_backed_vendor_state.cpp
 *
 * Protocol-free routing and lifecycle state for server-backed vendors.
 */

#include "network/authoritative/server_backed_vendor_state.hpp"

#include <set>

namespace devilution::authoritative {

void ServerBackedVendorState::SetEnabled(bool enabled) noexcept
{
	if (enabled_ == enabled)
		return;

	enabled_ = enabled;
	connected_ = false;
	ready_ = false;
	if (enabled)
		return;

	pendingOpenStoreId_.reset();
	snapshot_.reset();
	itemIndexBySlot_.clear();
	pendingPurchases_.clear();
}

void ServerBackedVendorState::SetConnected(bool connected) noexcept
{
	connected_ = enabled_ && connected;
	ready_ = false;
}

ServerBackedVendorPhase ServerBackedVendorState::Phase() const noexcept
{
	if (!enabled_)
		return ServerBackedVendorPhase::Disabled;
	if (!connected_)
		return ServerBackedVendorPhase::Disconnected;
	return ready_ ? ServerBackedVendorPhase::Ready : ServerBackedVendorPhase::AwaitingSnapshot;
}

VendorIntentRoute ServerBackedVendorState::OpenStore(uint32_t storeId)
{
	if (!enabled_)
		return VendorIntentRoute::Local;
	if (!connected_ || storeId == 0)
		return VendorIntentRoute::Blocked;

	pendingOpenStoreId_ = storeId;
	ready_ = false;
	return VendorIntentRoute::Pending;
}

VendorIntentRoute ServerBackedVendorState::Purchase(uint32_t storeId, uint32_t storeSlot)
{
	if (!enabled_)
		return VendorIntentRoute::Local;
	if (!connected_ || !ready_ || !snapshot_.has_value() || snapshot_->storeId != storeId)
		return VendorIntentRoute::Blocked;
	if (itemIndexBySlot_.find(storeSlot) == itemIndexBySlot_.end())
		return VendorIntentRoute::Blocked;

	const PurchaseKey key { storeId, storeSlot };
	if (pendingPurchases_.find(key) != pendingPurchases_.end())
		return VendorIntentRoute::Blocked;

	pendingPurchases_.emplace(key, PendingPurchaseState::AwaitingResult);
	return VendorIntentRoute::Pending;
}

bool ServerBackedVendorState::ApplySnapshot(ProjectedVendorSnapshot snapshot)
{
	if (!enabled_ || !connected_ || snapshot.storeId == 0)
		return false;
	if (pendingOpenStoreId_.has_value() && *pendingOpenStoreId_ != snapshot.storeId)
		return false;

	std::map<uint32_t, std::size_t> itemIndexBySlot;
	for (std::size_t i = 0; i < snapshot.items.size(); ++i) {
		if (!itemIndexBySlot.emplace(snapshot.items[i].storeSlot, i).second)
			return false;
	}

	std::set<uint32_t> presentSlots;
	for (const auto &[slot, index] : itemIndexBySlot) {
		(void)index;
		presentSlots.insert(slot);
	}
	for (auto it = pendingPurchases_.begin(); it != pendingPurchases_.end();) {
		const auto &[storeId, storeSlot] = it->first;
		if (storeId == snapshot.storeId
		    && it->second == PendingPurchaseState::Accepted
		    && presentSlots.find(storeSlot) == presentSlots.end()) {
			it = pendingPurchases_.erase(it);
		} else {
			++it;
		}
	}

	pendingOpenStoreId_.reset();
	snapshot_ = std::move(snapshot);
	itemIndexBySlot_ = std::move(itemIndexBySlot);
	ready_ = true;
	return true;
}

void ServerBackedVendorState::ClearSnapshot() noexcept
{
	pendingOpenStoreId_.reset();
	snapshot_.reset();
	itemIndexBySlot_.clear();
	ready_ = false;
}

bool ServerBackedVendorState::ResolvePurchase(uint32_t storeId, uint32_t storeSlot, PurchaseResolution resolution)
{
	const auto it = pendingPurchases_.find({ storeId, storeSlot });
	if (it == pendingPurchases_.end())
		return false;

	if (resolution == PurchaseResolution::Rejected)
		pendingPurchases_.erase(it);
	else
		it->second = PendingPurchaseState::Accepted;
	return true;
}

const ProjectedVendorItem *ServerBackedVendorState::FindItem(uint32_t storeId, uint32_t storeSlot) const noexcept
{
	if (!ready_ || !snapshot_.has_value() || snapshot_->storeId != storeId)
		return nullptr;
	const auto index = itemIndexBySlot_.find(storeSlot);
	if (index == itemIndexBySlot_.end())
		return nullptr;
	return &snapshot_->items[index->second];
}

bool ServerBackedVendorState::IsPurchasePending(uint32_t storeId, uint32_t storeSlot) const noexcept
{
	return pendingPurchases_.find({ storeId, storeSlot }) != pendingPurchases_.end();
}

} // namespace devilution::authoritative

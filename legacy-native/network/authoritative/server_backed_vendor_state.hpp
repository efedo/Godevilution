#pragma once

/**
 * @file network/authoritative/server_backed_vendor_state.hpp
 *
 * Protocol-free routing and lifecycle state for server-backed vendors.
 */

#include <cstddef>
#include <cstdint>
#include <map>
#include <optional>
#include <utility>

#include "network/authoritative/vendor_snapshot.hpp"

namespace devilution::authoritative {

enum class VendorIntentRoute {
	Local,
	Blocked,
	Pending,
};

enum class ServerBackedVendorPhase {
	Disabled,
	Disconnected,
	AwaitingSnapshot,
	Ready,
};

enum class PurchaseResolution {
	Accepted,
	Rejected,
};

/**
 * Owns protocol-independent vendor routing state.
 *
 * Disabled routing always returns Local, preserving the existing client path.
 * Enabled routing never falls back to local mutations.
 */
class ServerBackedVendorState {
public:
	void SetEnabled(bool enabled) noexcept;
	void SetConnected(bool connected) noexcept;

	[[nodiscard]] ServerBackedVendorPhase Phase() const noexcept;
	[[nodiscard]] VendorIntentRoute OpenStore(uint32_t storeId);
	[[nodiscard]] VendorIntentRoute Purchase(uint32_t storeId, uint32_t storeSlot);

	/** Applies a validated snapshot and reconciles accepted purchases. */
	[[nodiscard]] bool ApplySnapshot(ProjectedVendorSnapshot snapshot);

	/** Drops the displayed vendor snapshot while preserving pending purchases. */
	void ClearSnapshot() noexcept;

	/** Applies a server result to an existing pending purchase. */
	[[nodiscard]] bool ResolvePurchase(uint32_t storeId, uint32_t storeSlot, PurchaseResolution resolution);

	[[nodiscard]] const ProjectedVendorItem *FindItem(uint32_t storeId, uint32_t storeSlot) const noexcept;
	[[nodiscard]] const ProjectedVendorSnapshot *Snapshot() const noexcept { return snapshot_ ? &*snapshot_ : nullptr; }
	[[nodiscard]] bool IsPurchasePending(uint32_t storeId, uint32_t storeSlot) const noexcept;
	[[nodiscard]] std::size_t PendingPurchaseCount() const noexcept { return pendingPurchases_.size(); }
	[[nodiscard]] std::optional<uint32_t> PendingOpenStoreId() const noexcept { return pendingOpenStoreId_; }

private:
	enum class PendingPurchaseState {
		AwaitingResult,
		Accepted,
	};

	using PurchaseKey = std::pair<uint32_t, uint32_t>;

	bool enabled_ = false;
	bool connected_ = false;
	bool ready_ = false;
	std::optional<uint32_t> pendingOpenStoreId_;
	std::optional<ProjectedVendorSnapshot> snapshot_;
	std::map<uint32_t, std::size_t> itemIndexBySlot_;
	std::map<PurchaseKey, PendingPurchaseState> pendingPurchases_;
};

} // namespace devilution::authoritative

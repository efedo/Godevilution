#pragma once

/**
 * @file network/authoritative/server_backed_vendor_ui.hpp
 *
 * Applies authoritative vendor snapshots to the legacy store presentation
 * buffers while retaining the server's stable item-slot identifiers.
 */

#include <cstddef>
#include <cstdint>
#include <optional>
#include <string>
#include <vector>

#include <expected.hpp>

#include "game/stores/stores.hpp"
#include "network/authoritative/vendor_snapshot.hpp"

namespace devilution::authoritative {

enum class ServerBackedVendorDestination {
	Smith,
	PremiumSmith,
	Witch,
	Healer,
	Wirt,
};

/**
 * Bridges a validated vendor snapshot into legacy store presentation state.
 *
 * The destination and expected store ID are explicit so a snapshot from one
 * server-owned store cannot silently populate another store's UI. The slot
 * mapping is retained separately because the legacy Item type has no stable
 * protocol-slot field.
 */
class ServerBackedVendorUiAdapter {
public:
	explicit ServerBackedVendorUiAdapter(StoreManager &storeManager)
	    : storeManager_(storeManager)
	{
	}

	tl::expected<void, std::string> Apply(const ProjectedVendorSnapshot &snapshot,
	    ServerBackedVendorDestination destination, uint32_t expectedStoreId);

	[[nodiscard]] std::optional<uint32_t> StoreSlotAt(std::size_t index) const noexcept;
	[[nodiscard]] std::size_t ItemCount() const noexcept { return storeSlots_.size(); }

private:
	StoreManager &storeManager_;
	std::vector<uint32_t> storeSlots_;
};

} // namespace devilution::authoritative

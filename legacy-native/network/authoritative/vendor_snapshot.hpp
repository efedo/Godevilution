#pragma once

/**
 * @file network/authoritative/vendor_snapshot.hpp
 *
 * Native projection of server-owned vendor-stock snapshots.
 */

#include <cstdint>
#include <string>
#include <vector>

#include <expected.hpp>

#include "game/items/items.hpp"

namespace devilution::protocol::v1 {
class Snapshot;
}

namespace devilution::authoritative {

struct ProjectedVendorItem {
	uint32_t storeSlot = 0;
	uint32_t itemSeed = 0;
	uint32_t price = 0;
	Item item;
};

struct ProjectedVendorSnapshot {
	uint32_t storeId = 0;
	std::vector<ProjectedVendorItem> items;
};

/** Converts server-owned vendor stock into a protocol-independent projection. */
[[nodiscard]] tl::expected<ProjectedVendorSnapshot, std::string> ProjectVendorSnapshot(const ::devilution::protocol::v1::Snapshot &snapshot);

} // namespace devilution::authoritative

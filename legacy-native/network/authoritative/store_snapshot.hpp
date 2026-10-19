#pragma once

/**
 * @file network/authoritative/store_snapshot.hpp
 *
 * Compatibility include for the renamed server-backed vendor projection.
 */

#include "network/authoritative/vendor_snapshot.hpp"

namespace devilution::authoritative {

using ProjectedStoreItem [[deprecated("Use ProjectedVendorItem")]] = ProjectedVendorItem;
using ProjectedStoreSnapshot [[deprecated("Use ProjectedVendorSnapshot")]] = ProjectedVendorSnapshot;

inline tl::expected<ProjectedVendorSnapshot, std::string> ProjectStoreSnapshot(const ::devilution::protocol::v1::Snapshot &snapshot)
{
	return ProjectVendorSnapshot(snapshot);
}

} // namespace devilution::authoritative

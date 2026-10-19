/**
 * @file network/authoritative/vendor_snapshot.cpp
 *
 * Native projection of authoritative vendor-stock snapshots.
 */

#include "network/authoritative/vendor_snapshot.hpp"

#include <unordered_set>
#include <utility>

#include "devilution.pb.h"
#include "network/authoritative/item_snapshot.hpp"

namespace devilution::authoritative {
namespace protocol = ::devilution::protocol::v1;
namespace {

bool HasValidItemIdentity(uint32_t itemSeed, const protocol::ItemStateSnapshot &state)
{
	const bool hasDynamicPresentation = !state.base_name().empty()
	    || !state.identified_name().empty()
	    || state.has_native_cursor_graphic();
	if (!hasDynamicPresentation)
		return itemSeed != 0;
	return !state.base_name().empty()
	    && !state.identified_name().empty()
	    && state.has_native_cursor_graphic();
}

} // namespace

tl::expected<ProjectedVendorSnapshot, std::string> ProjectVendorSnapshot(const protocol::Snapshot &snapshot)
{
	if (!snapshot.has_active_store())
		return tl::make_unexpected("Server-backed snapshot has no active vendor.");
	if (snapshot.active_store().store_id() == 0)
		return tl::make_unexpected("Server-backed vendor snapshot has an invalid store identifier.");

	ProjectedVendorSnapshot projected { .storeId = snapshot.active_store().store_id() };
	std::unordered_set<uint32_t> slots;
	projected.items.reserve(snapshot.active_store().items_size());
	for (const protocol::StoreItemSnapshot &source : snapshot.active_store().items()) {
		if (!slots.insert(source.store_slot()).second)
			return tl::make_unexpected("Server-backed vendor snapshot contains a duplicate slot.");
		if (source.price() > static_cast<uint32_t>(std::numeric_limits<int>::max()))
			return tl::make_unexpected("Server-backed vendor snapshot contains an unsupported price.");
		if (!HasValidItemIdentity(source.item_seed(), source.state()))
			return tl::make_unexpected("Server-backed vendor snapshot contains an item with an invalid native identity.");
		auto item = ProjectNativeItem(source.state(), source.item_seed(), source.price());
		if (!item.has_value())
			return tl::make_unexpected(item.error());
		projected.items.push_back({
		    .storeSlot = source.store_slot(),
		    .itemSeed = source.item_seed(),
		    .price = source.price(),
		    .item = std::move(*item),
		});
	}
	return projected;
}

} // namespace devilution::authoritative

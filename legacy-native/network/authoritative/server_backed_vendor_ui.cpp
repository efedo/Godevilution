#include "network/authoritative/server_backed_vendor_ui.hpp"

#include <algorithm>
#include <iterator>

namespace devilution::authoritative {
namespace {

tl::expected<void, std::string> ValidateSnapshot(const ProjectedVendorSnapshot &snapshot, uint32_t expectedStoreId, std::size_t capacity)
{
	if (expectedStoreId == 0 || snapshot.storeId != expectedStoreId)
		return tl::make_unexpected("Server-backed vendor snapshot does not match the requested store.");
	if (snapshot.items.size() > capacity)
		return tl::make_unexpected("Server-backed vendor snapshot exceeds the legacy store capacity.");
	return {};
}

template <typename StoreItems>
void ReplaceItems(StoreItems &destination, const ProjectedVendorSnapshot &snapshot)
{
	destination.clear();
	for (const ProjectedVendorItem &source : snapshot.items)
		destination.push_back(source.item);
}

} // namespace

tl::expected<void, std::string> ServerBackedVendorUiAdapter::Apply(const ProjectedVendorSnapshot &snapshot,
    ServerBackedVendorDestination destination, uint32_t expectedStoreId)
{
	const std::size_t capacity = [&] {
		switch (destination) {
		case ServerBackedVendorDestination::Smith:
			return static_cast<std::size_t>(NumSmithBasicItemsHf);
		case ServerBackedVendorDestination::PremiumSmith:
			return static_cast<std::size_t>(NumSmithItemsHf);
		case ServerBackedVendorDestination::Witch:
			return static_cast<std::size_t>(NumWitchItemsHf);
		case ServerBackedVendorDestination::Healer:
			return static_cast<std::size_t>(NumHealerItemsHf);
		case ServerBackedVendorDestination::Wirt:
			return std::size_t { 1 };
		}
		return std::size_t { 0 };
	}();

	if (auto result = ValidateSnapshot(snapshot, expectedStoreId, capacity); !result.has_value())
		return result;

	switch (destination) {
	case ServerBackedVendorDestination::Smith:
		ReplaceItems(storeManager_.smithItems(), snapshot);
		break;
	case ServerBackedVendorDestination::PremiumSmith:
		ReplaceItems(storeManager_.premiumItems(), snapshot);
		break;
	case ServerBackedVendorDestination::Witch:
		ReplaceItems(storeManager_.witchItems(), snapshot);
		break;
	case ServerBackedVendorDestination::Healer:
		ReplaceItems(storeManager_.healerItems(), snapshot);
		break;
	case ServerBackedVendorDestination::Wirt:
		storeManager_.boyItem().clear();
		if (!snapshot.items.empty())
			storeManager_.boyItem() = snapshot.items.front().item;
		break;
	}

	storeSlots_.clear();
	storeSlots_.reserve(snapshot.items.size());
	std::ranges::transform(snapshot.items, std::back_inserter(storeSlots_), &ProjectedVendorItem::storeSlot);
	return {};
}

std::optional<uint32_t> ServerBackedVendorUiAdapter::StoreSlotAt(std::size_t index) const noexcept
{
	if (index >= storeSlots_.size())
		return std::nullopt;
	return storeSlots_[index];
}

} // namespace devilution::authoritative

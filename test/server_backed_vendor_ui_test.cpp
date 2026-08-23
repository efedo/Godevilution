#include <gtest/gtest.h>

#include <initializer_list>

#include "network/authoritative/server_backed_vendor_ui.hpp"

namespace devilution::authoritative {
namespace {

ProjectedVendorSnapshot MakeSnapshot(uint32_t storeId, std::initializer_list<uint32_t> slots)
{
	ProjectedVendorSnapshot snapshot { .storeId = storeId };
	for (const uint32_t slot : slots) {
		ProjectedVendorItem item;
		item.storeSlot = slot;
		item.itemSeed = slot + 100;
		item.price = slot + 10;
		item.item._iSeed = item.itemSeed;
		item.item._iIvalue = static_cast<int>(item.price);
		snapshot.items.push_back(item);
	}
	return snapshot;
}

TEST(ServerBackedVendorUi, AppliesItemsAndRetainsStableSlots)
{
	StoreManager store;
	ServerBackedVendorUiAdapter adapter(store);

	auto result = adapter.Apply(MakeSnapshot(7, { 4, 9 }), ServerBackedVendorDestination::Smith, 7);
	ASSERT_TRUE(result.has_value()) << result.error();
	ASSERT_EQ(store.smithItems().size(), 2U);
	EXPECT_EQ(store.smithItems()[0]._iSeed, 104U);
	EXPECT_EQ(store.smithItems()[1]._iIvalue, 19);
	EXPECT_EQ(adapter.ItemCount(), 2U);
	ASSERT_TRUE(adapter.StoreSlotAt(1).has_value());
	EXPECT_EQ(*adapter.StoreSlotAt(1), 9U);
	EXPECT_FALSE(adapter.StoreSlotAt(2).has_value());
}

TEST(ServerBackedVendorUi, RejectsWrongStoreWithoutChangingExistingItems)
{
	StoreManager store;
	ServerBackedVendorUiAdapter adapter(store);
	ASSERT_TRUE(adapter.Apply(MakeSnapshot(7, { 2 }), ServerBackedVendorDestination::Smith, 7).has_value());

	auto result = adapter.Apply(MakeSnapshot(8, { 3, 4 }), ServerBackedVendorDestination::Smith, 7);
	EXPECT_FALSE(result.has_value());
	ASSERT_EQ(store.smithItems().size(), 1U);
	EXPECT_EQ(store.smithItems()[0]._iSeed, 102U);
	EXPECT_EQ(adapter.ItemCount(), 1U);
}

TEST(ServerBackedVendorUi, RejectsWirtSnapshotWithMoreThanOneItem)
{
	StoreManager store;
	ServerBackedVendorUiAdapter adapter(store);

	auto result = adapter.Apply(MakeSnapshot(7, { 1, 2 }), ServerBackedVendorDestination::Wirt, 7);
	EXPECT_FALSE(result.has_value());
	EXPECT_TRUE(store.boyItem().isEmpty());
}

TEST(ServerBackedVendorUi, AppliesEveryVisualStoreDestination)
{
	StoreManager store;
	ServerBackedVendorUiAdapter adapter(store);

	ASSERT_TRUE(adapter.Apply(MakeSnapshot(1, { 4 }), ServerBackedVendorDestination::Smith, 1).has_value());
	ASSERT_TRUE(adapter.Apply(MakeSnapshot(2, { 5 }), ServerBackedVendorDestination::Witch, 2).has_value());
	ASSERT_TRUE(adapter.Apply(MakeSnapshot(4, { 6 }), ServerBackedVendorDestination::Healer, 4).has_value());
	ASSERT_TRUE(adapter.Apply(MakeSnapshot(3, { 7 }), ServerBackedVendorDestination::Wirt, 3).has_value());

	EXPECT_EQ(store.smithItems().size(), 1U);
	EXPECT_EQ(store.smithItems()[0]._iSeed, 104U);
	EXPECT_EQ(store.witchItems().size(), 1U);
	EXPECT_EQ(store.witchItems()[0]._iSeed, 105U);
	EXPECT_EQ(store.healerItems().size(), 1U);
	EXPECT_EQ(store.healerItems()[0]._iSeed, 106U);
	EXPECT_EQ(store.boyItem()._iSeed, 107U);
}

} // namespace
} // namespace devilution::authoritative

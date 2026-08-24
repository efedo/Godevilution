#include <gtest/gtest.h>

#include "devilution.pb.h"
#include "network/authoritative/vendor_snapshot.hpp"

namespace devilution::authoritative {
namespace protocol = ::devilution::protocol::v1;
namespace {

TEST(ServerBackedVendorSnapshot, ProjectsVendorStockWithoutProtocolTypes)
{
	protocol::Snapshot snapshot;
	auto *store = snapshot.mutable_active_store();
	store->set_store_id(7);
	auto *source = store->add_items();
	source->set_store_slot(3);
	source->set_item_seed(42);
	source->set_price(75);
	source->mutable_state()->set_item_type(4);
	source->mutable_state()->set_identified(true);
	source->mutable_state()->set_durability(12);
	source->mutable_state()->set_max_durability(20);

	auto projected = ProjectVendorSnapshot(snapshot);

	ASSERT_TRUE(projected.has_value()) << projected.error();
	EXPECT_EQ(projected->storeId, 7U);
	ASSERT_EQ(projected->items.size(), 1U);
	EXPECT_EQ(projected->items[0].storeSlot, 3U);
	EXPECT_EQ(projected->items[0].itemSeed, 42U);
	EXPECT_EQ(projected->items[0].price, 75U);
	EXPECT_EQ(projected->items[0].item._itype, static_cast<ItemType>(4));
	EXPECT_TRUE(projected->items[0].item._iIdentified);
	EXPECT_EQ(projected->items[0].item._iIvalue, 75);
	EXPECT_EQ(projected->items[0].item._iDurability, 12);
	EXPECT_EQ(projected->items[0].item._iMaxDur, 20);
}

TEST(ServerBackedVendorSnapshot, RejectsMissingStoreAndDuplicateSlots)
{
	protocol::Snapshot missing;
	EXPECT_FALSE(ProjectVendorSnapshot(missing).has_value());

	protocol::Snapshot duplicate;
	duplicate.mutable_active_store()->set_store_id(1);
	duplicate.mutable_active_store()->add_items()->set_store_slot(2);
	duplicate.mutable_active_store()->add_items()->set_store_slot(2);
	EXPECT_FALSE(ProjectVendorSnapshot(duplicate).has_value());
}

TEST(ServerBackedVendorSnapshot, RejectsInvalidIdentifiersAndPrices)
{
	protocol::Snapshot zeroStore;
	zeroStore.mutable_active_store()->set_store_id(0);
	EXPECT_FALSE(ProjectVendorSnapshot(zeroStore).has_value());

	protocol::Snapshot excessivePrice;
	excessivePrice.mutable_active_store()->set_store_id(1);
	excessivePrice.mutable_active_store()->add_items()->set_price(UINT32_MAX);
	EXPECT_FALSE(ProjectVendorSnapshot(excessivePrice).has_value());

	protocol::Snapshot excessiveDamage;
	excessiveDamage.mutable_active_store()->set_store_id(1);
	excessiveDamage.mutable_active_store()->add_items()->mutable_state()->set_min_damage(256);
	EXPECT_FALSE(ProjectVendorSnapshot(excessiveDamage).has_value());
}

TEST(ServerBackedVendorSnapshot, RejectsInvalidItemIdentity)
{
	protocol::Snapshot snapshot;
	snapshot.mutable_active_store()->set_store_id(1);
	auto *item = snapshot.mutable_active_store()->add_items();
	item->set_store_slot(1);
	item->set_item_seed(0);
	EXPECT_FALSE(ProjectVendorSnapshot(snapshot).has_value());
}

} // namespace
} // namespace devilution::authoritative

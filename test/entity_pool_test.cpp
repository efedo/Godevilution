#include <gtest/gtest.h>

#include "utils/container/entity_pool.hpp"

namespace devilution {
namespace {

struct TestEntity {
	int value = 0;
};

TEST(EntityPool, SparsePoolInitializesFreeListAndReusesReleasedIndex)
{
	DenseEntityPool<TestEntity, 3, SparseAllocationPolicy> pool;
	EXPECT_TRUE(pool.empty());

	TestEntity *first = pool.allocate();
	TestEntity *second = pool.allocate();
	ASSERT_NE(first, nullptr);
	ASSERT_NE(second, nullptr);
	first->value = 10;
	second->value = 20;
	EXPECT_EQ(pool.activeCount(), 2);
	auto sparseIt = pool.begin();
	EXPECT_EQ(sparseIt.index(), 0);

	pool.deallocate(0);
	pool.deallocate(0);
	EXPECT_EQ(pool.activeCount(), 1);
	TestEntity *reused = pool.allocate();
	ASSERT_NE(reused, nullptr);
	EXPECT_EQ(reused, first);
	EXPECT_EQ(pool.activeCount(), 2);

	EXPECT_NE(pool.allocate(), nullptr);
	EXPECT_EQ(pool.allocate(), nullptr);
	pool.clear();
	EXPECT_TRUE(pool.empty());
	EXPECT_EQ(pool.allocate(), first);
}

TEST(EntityPool, DensePoolPacksElementsAfterRelease)
{
	DenseEntityPool<TestEntity, 3, DenseAllocationPolicy> pool;
	TestEntity *first = pool.allocate();
	TestEntity *second = pool.allocate();
	TestEntity *third = pool.allocate();
	ASSERT_NE(first, nullptr);
	ASSERT_NE(second, nullptr);
	ASSERT_NE(third, nullptr);
	first->value = 1;
	second->value = 2;
	third->value = 3;

	pool.deallocate(1);
	ASSERT_EQ(pool.activeCount(), 2);
	EXPECT_EQ(pool[1].value, 3);
	auto denseIt = pool.begin();
	EXPECT_EQ(denseIt->value, 1);
	++denseIt;
	EXPECT_EQ(denseIt->value, 3);

	TestEntity *replacement = pool.allocate();
	ASSERT_NE(replacement, nullptr);
	replacement->value = 4;
	EXPECT_EQ(pool.activeCount(), 3);
	EXPECT_EQ(pool[2].value, 4);
	pool.deallocate(99);
	EXPECT_EQ(pool.activeCount(), 3);
}

} // namespace
} // namespace devilution

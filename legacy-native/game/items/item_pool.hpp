/**
 * @file game/items/item_pool.hpp
 *
 * ItemPool: Modern container for floor item management.
 *
 * Provides a type-safe, STL-compatible fixed-capacity pool using dense allocation.
 * Items use swap-with-last deletion (DenseAllocationPolicy) because item slot
 * indices only need to be stable within a single level load — they are not stored
 * persistently in the binary save format.
 *
 * gItemPool owns the Item storage. Items is a pointer alias into pool memory.
 * ActiveItems[] and ActiveItemCount are separate uint8_t storage (owned here)
 * because the pool's internal index storage uses int, which cannot be aliased to uint8_t.
 *
 * DESIGN NOTES:
 * - Items[MAXITEMS] (the extra slot) is a scratch buffer used by item-generation
 *   routines to build an item before committing it. The pool capacity is MAXITEMS+1
 *   to preserve this slot at that exact index.
 * - See docs/ENTITY_POOLS.md for the analogous object pool documentation.
 */
#pragma once

#include <cstring>
#include <iterator>
#include <span>

#include "game/items/items.hpp"
#include "utils/container/entity_pool.hpp"

namespace devilution {

struct Item;

// Pool capacity matches the legacy array size (MAXITEMS active slots + 1 scratch slot)
using ItemPool = DenseEntityPool<Item, MAXITEMS + 1, SparseAllocationPolicy>;

// Global pool instance (owns Item storage)
extern ItemPool gItemPool;

// Active/available item index bookkeeping (uint8_t, separate from pool internals)
extern DVL_API_FOR_TEST uint8_t ActiveItems[MAXITEMS];
extern DVL_API_FOR_TEST uint8_t ActiveItemCount;

// Initialization hook (call when starting a new level)
void InitializeItemPool();

namespace ItemPoolAdapter {

class ActiveItemIterator {
public:
	using iterator_category = std::forward_iterator_tag;
	using value_type = Item;
	using difference_type = int;
	using pointer = Item *;
	using reference = Item &;

	explicit ActiveItemIterator(int activeIndex)
	    : activeIndex_(activeIndex)
	{
	}

	reference operator*() const
	{
		return Items[ActiveItems[activeIndex_]];
	}

	pointer operator->() const
	{
		return &Items[ActiveItems[activeIndex_]];
	}

	ActiveItemIterator &operator++()
	{
		++activeIndex_;
		return *this;
	}

	bool operator==(const ActiveItemIterator &other) const
	{
		return activeIndex_ == other.activeIndex_;
	}

	bool operator!=(const ActiveItemIterator &other) const
	{
		return !(*this == other);
	}

	// Returns the pool index (ActiveItems[activeIndex_]) for the current item
	[[nodiscard]] int itemIndex() const
	{
		return ActiveItems[activeIndex_];
	}

private:
	int activeIndex_;
};

class ActiveItemRange {
public:
	ActiveItemIterator begin() const
	{
		return ActiveItemIterator(0);
	}

	ActiveItemIterator end() const
	{
		return ActiveItemIterator(static_cast<int>(ActiveItemCount));
	}
};

[[nodiscard]] inline ActiveItemRange ActiveItemsRange()
{
	return ActiveItemRange {};
}

[[nodiscard]] inline bool HasFreeItemSlot()
{
	return ActiveItemCount < MAXITEMS;
}

[[nodiscard]] inline int ActiveItemCountValue()
{
	return static_cast<int>(ActiveItemCount);
}

[[nodiscard]] inline std::span<uint8_t, MAXITEMS> ActiveItemIds()
{
	return std::span<uint8_t, MAXITEMS>(ActiveItems, MAXITEMS);
}

inline void ResetLegacyItemPools()
{
	for (int i = 0; i < MAXITEMS; i++) {
		ActiveItems[i] = static_cast<uint8_t>(i);
	}
	ActiveItemCount = 0;
}

} // namespace ItemPoolAdapter

} // namespace devilution

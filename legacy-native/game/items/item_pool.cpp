/**
 * @file game/items/item_pool.cpp
 *
 * Implementation of item pool management.
 */
#include "game/items/item_pool.hpp"

#include "game/items/items.hpp"

namespace devilution {

// Global pool instance — owns all Item storage
ItemPool gItemPool;

// Legacy pointer alias: Items[i] == gItemPool[i] (same memory)
Item *Items = gItemPool.data();

// Active/available index bookkeeping (uint8_t, separate from pool internals)
uint8_t ActiveItems[MAXITEMS];
uint8_t ActiveItemCount;

void InitializeItemPool()
{
	gItemPool.clear();
	ItemPoolAdapter::ResetLegacyItemPools();
}

} // namespace devilution

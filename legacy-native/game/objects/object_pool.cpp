/**
 * ObjectPool implementation: Modern container management.
 *
 * Provides a parallel pool structure that can be synchronized with legacy globals.
 * This allows gradual migration without requiring immediate changes to all code paths.
 * 
 * The pool uses sparse allocation to maintain stable indices, which is essential for:
 * - Binary save/load compatibility (object indices don't change)
 * - Network sync messages (indices are the entity identifiers)
 * - Persistent map references (dObject array uses object indices)
 */
/**
 * @file game/objects/object_pool.cpp
 *
 * Implementation of object pool management.
 */
#include "game/objects/object_pool.hpp"

#include "game/objects/objects.hpp"

namespace devilution {

// Global pool instance
ObjectPool gObjectPool;

// Legacy global aliases bound to ObjectPool storage
Object *Objects = gObjectPool.data();
int *AvailableObjects = gObjectPool.availableIndices();
int *ActiveObjects = gObjectPool.activeIndices();
int &ActiveObjectCount = gObjectPool.activeCountRef();

void InitializeObjectPool()
{
	// Clear and reinitialize the pool
	// This sets up the free-list for sparse allocation
	gObjectPool.clear();
}

} // namespace devilution

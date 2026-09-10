/**
 * @file game/monsters/monster_pool.hpp
 *
 * MonsterPool: Modern container for monster management.
 *
 * Provides a type-safe, STL-compatible fixed-capacity pool using sparse allocation.
 * Sparse allocation is required because monsters are referenced by slot index in the
 * dMonster tile map and in network sync messages — indices must remain stable.
 *
 * gMonsterPool owns the Monster storage. Monsters is a pointer alias into pool memory.
 * ActiveMonsters[] and ActiveMonsterCount are separate owned globals because the pool's
 * internal index storage uses int, which cannot be aliased to unsigned/size_t.
 *
 * DESIGN NOTES:
 * - The full ActiveMonsters[MaxMonsters] array is serialized to the save file (all slots,
 *   not just the active count), so the alias must cover the full MaxMonsters extent.
 * - See docs/ENTITY_POOLS.md for usage and extension guidance.
 */
#pragma once

#include <span>

#include "game/monsters/monsters.hpp"
#include "utils/container/entity_pool.hpp"

namespace devilution {

struct Monster;

using MonsterPool = DenseEntityPool<Monster, MaxMonsters, SparseAllocationPolicy>;

extern MonsterPool gMonsterPool;

// Active monster index bookkeeping (separate from pool internals)
extern unsigned ActiveMonsters[MaxMonsters];
extern size_t ActiveMonsterCount;

void InitializeMonsterPool();

namespace MonsterPoolAdapter {

[[nodiscard]] inline std::span<Monster, MaxMonsters> AllMonsters()
{
	return std::span<Monster, MaxMonsters>(Monsters, MaxMonsters);
}

// Full-capacity span of the ActiveMonsters index array (MaxMonsters entries).
[[nodiscard]] inline std::span<unsigned, MaxMonsters> ActiveMonsterIds()
{
	return std::span<unsigned, MaxMonsters>(ActiveMonsters, MaxMonsters);
}

// Span covering only the currently active monster indices [0, ActiveMonsterCount).
[[nodiscard]] inline std::span<unsigned> ActiveMonsterRange()
{
	return std::span<unsigned>(ActiveMonsters, ActiveMonsterCount);
}

[[nodiscard]] size_t ActiveMonsterCountValue();

[[nodiscard]] inline bool HasFreeMonsterSlot()
{
	return ActiveMonsterCount < MaxMonsters;
}

} // namespace MonsterPoolAdapter

} // namespace devilution

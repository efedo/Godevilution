/**
 * @file game/monsters/monster_pool.cpp
 *
 * Implementation of monster pool management.
 */
#include "game/monsters/monster_pool.hpp"

#include "game/monsters/monsters.hpp"

namespace devilution {

MonsterPool gMonsterPool;

// Legacy pointer alias: Monsters[i] == gMonsterPool[i] (same memory)
Monster *Monsters = gMonsterPool.data();

// Active index bookkeeping (separate from pool internals due to type mismatch)
unsigned ActiveMonsters[MaxMonsters];
size_t ActiveMonsterCount;

void InitializeMonsterPool()
{
	gMonsterPool.clear();
	ActiveMonsterCount = 0;
}

namespace MonsterPoolAdapter {

size_t ActiveMonsterCountValue()
{
	return ActiveMonsterCount;
}

} // namespace MonsterPoolAdapter

} // namespace devilution

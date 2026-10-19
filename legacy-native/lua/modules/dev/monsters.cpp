/**
 * @file lua/modules/dev/monsters.cpp
 *
 * Implementation of monsters.
 */


#ifdef _DEBUG
#include "lua/modules/dev/monsters.hpp"

#include <sol/sol.hpp>

#include "application/debug.h"
#include "lua/metadoc.hpp"

namespace devilution {

sol::table LuaDevMonstersModule(sol::state_view &lua)
{
	sol::table table = lua.create_table();
	LuaSetDocFn(table, "spawn", "(name: string, count: number = 1)", "Spawn monster(s)", &devilution::DebugCmdSpawnMonster);
	LuaSetDocFn(table, "spawnUnique", "(name: string, count: number = 1)", "Spawn unique monster(s)", &devilution::DebugCmdSpawnUniqueMonster);
	return table;
}

} // namespace devilution
#endif // _DEBUG

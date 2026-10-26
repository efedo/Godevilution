/**
 * @file lua/modules/dev/level/warp.cpp
 *
 * Implementation of warp.
 */

#ifdef _DEBUG
#include "lua/modules/dev/level/warp.hpp"

#include <sol/sol.hpp>

#include "application/debug.h"
#include "lua/metadoc.hpp"

namespace devilution {
sol::table LuaDevLevelWarpModule(sol::state_view &lua)
{
	sol::table table = lua.create_table();
	LuaSetDocFn(table, "dungeon", "(n: number)", "Go to dungeon level (0 for town).", &DebugCmdWarpToDungeonLevel);
	LuaSetDocFn(table, "map", "(path: string, dunType: number, x: number, y: number)", "Go to custom {path}.dun level", &DebugCmdWarpToCustomMap);
	LuaSetDocFn(table, "quest", "(n: number)", "Go to quest level.", &DebugCmdWarpToQuestLevel);
	return table;
}

} // namespace devilution
#endif // _DEBUG

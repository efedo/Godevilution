/**
 * @file lua/modules/dev/level.cpp
 *
 * Implementation of level.
 */


#ifdef _DEBUG
#include "lua/modules/dev/level.hpp"

#include <sol/sol.hpp>

#include "application/debug.h"
#include "lua/metadoc.hpp"
#include "lua/modules/dev/level/map.hpp"
#include "lua/modules/dev/level/warp.hpp"

namespace devilution {

sol::table LuaDevLevelModule(sol::state_view &lua)
{
	sol::table table = lua.create_table();
	LuaSetDocFn(table, "exportDun", "()", "Save the current level as a dun-file.", &devilution::DebugCmdExportDun);
	LuaSetDocFn(table, "map", "", "Automap-related commands.", LuaDevLevelMapModule(lua));
	LuaSetDocFn(table, "reset", "(n: number, seed: number = nil)", "Resets specified level.", &DebugCmdResetLevel);
	LuaSetDocFn(table, "seed", "(level: number = nil)", "Get the seed of the current or given level.", &DebugCmdLevelSeed);
	LuaSetDocFn(table, "warp", "", "Warp to a level or a custom map.", LuaDevLevelWarpModule(lua));
	return table;
}

} // namespace devilution
#endif // _DEBUG

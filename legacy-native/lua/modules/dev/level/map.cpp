/**
 * @file lua/modules/dev/level/map.cpp
 *
 * Implementation of map.
 */


#ifdef _DEBUG
#include "lua/modules/dev/level/map.hpp"

#include <string>

#include <sol/sol.hpp>

#include "application/debug.h"
#include "lua/metadoc.hpp"

namespace devilution {
sol::table LuaDevLevelMapModule(sol::state_view &lua)
{
	sol::table table = lua.create_table();
	LuaSetDocFn(table, "hide", "()", "Hide the map.", &DebugCmdMapHide);
	LuaSetDocFn(table, "reveal", "()", "Reveal the map.", &DebugCmdMapReveal);
	return table;
}

} // namespace devilution
#endif // _DEBUG

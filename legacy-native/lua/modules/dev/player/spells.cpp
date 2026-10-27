/**
 * @file lua/modules/dev/player/spells.cpp
 *
 * Implementation of spells.
 */

#ifdef _DEBUG
#include "lua/modules/dev/player/spells.hpp"

#include <cstdint>

#include <sol/sol.hpp>

#include "application/debug.h"
#include "lua/metadoc.hpp"

namespace devilution {

sol::table LuaDevPlayerSpellsModule(sol::state_view &lua)
{
	sol::table table = lua.create_table();
	LuaSetDocFn(table, "setLevel", "(level: number)", "Set spell level for all spells.", &DebugCmdSetSpellsLevel);
	return table;
}

} // namespace devilution
#endif // _DEBUG

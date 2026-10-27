/**
 * @file lua/modules/dev/towners.cpp
 *
 * Implementation of towners.
 */

#ifdef _DEBUG
#include "lua/modules/dev/towners.hpp"

#include <sol/sol.hpp>

#include "application/debug.h"
#include "lua/metadoc.hpp"

namespace devilution {

sol::table LuaDevTownersModule(sol::state_view &lua)
{
	sol::table table = lua.create_table();
	LuaSetDocFn(table, "talk", "(name: string)", "Talk to towner.", &devilution::DebugCmdTalkToTowner);
	LuaSetDocFn(table, "visit", "(name: string)", "Teleport to towner.", &devilution::DebugCmdVisitTowner);
	return table;
}

} // namespace devilution
#endif // _DEBUG

/**
 * @file lua/modules/system.cpp
 *
 * Implementation of system.
 */

#include "lua/modules/system.hpp"

#include <sol/sol.hpp>

#include "lua/metadoc.hpp"
#include "utils/timer.hpp"

namespace devilution {

sol::table LuaSystemModule(sol::state_view &lua)
{
	sol::table table = lua.create_table();

	LuaSetDocFn(table, "get_ticks", "() -> integer", "Returns the number of milliseconds since the game started.",
	    []() { return static_cast<int>(GetMillisecondsSinceStartup()); });

	return table;
}

} // namespace devilution

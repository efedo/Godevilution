/**
 * @file lua/modules/dev/search.cpp
 *
 * Implementation of search.
 */

#ifdef _DEBUG
#include "lua/modules/dev/search.hpp"

#include <sol/sol.hpp>

#include "application/debug.h"
#include "lua/metadoc.hpp"

namespace devilution {
namespace {

} // namespace

sol::table LuaDevSearchModule(sol::state_view &lua)
{
	sol::table table = lua.create_table();
	LuaSetDocFn(table, "clear", "()", "Clear search results from the map.", &DebugCmdClearSearch);
	LuaSetDocFn(table, "item", "(name: string)", "Search the map for an item.", &DebugCmdSearchItem);
	LuaSetDocFn(table, "monster", "(name: string)", "Search the map for a monster.", &DebugCmdSearchMonster);
	LuaSetDocFn(table, "object", "(name: string)", "Search the map for an object.", &DebugCmdSearchObject);
	return table;
}

} // namespace devilution
#endif // _DEBUG

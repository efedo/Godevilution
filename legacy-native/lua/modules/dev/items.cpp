/**
 * @file lua/modules/dev/items.cpp
 *
 * Implementation of items.
 */


#ifdef _DEBUG
#include "lua/modules/dev/items.hpp"

#include <sol/sol.hpp>

#include "application/debug.h"
#include "game/items/items.hpp"
#include "lua/metadoc.hpp"

namespace devilution {

sol::table LuaDevItemsModule(sol::state_view &lua)
{
	sol::table table = lua.create_table();
	LuaSetDocFn(table, "get", "() -> Item", "Get the currently selected item.", &devilution::DebugCmdGetSelectedItem);
	LuaSetDocFn(table, "info", "()", "Show info of currently selected item.", &devilution::DebugCmdItemInfo);
	LuaSetDocFn(table, "spawn", "(name: string)", "Attempt to generate an item.", &devilution::DebugCmdSpawnItem);
	LuaSetDocFn(table, "spawnUnique", "(name: string)", "Attempt to generate a unique item.", &devilution::DebugCmdSpawnUniqueItem);
	return table;
}

} // namespace devilution
#endif // _DEBUG

/**
 * @file lua/modules/dev/player/stats.cpp
 *
 * Implementation of stats.
 */


#ifdef _DEBUG
#include "lua/modules/dev/player/stats.hpp"

#include <string>

#include <sol/sol.hpp>

#include "application/debug.h"
#include "lua/metadoc.hpp"

namespace devilution {

namespace {

std::string DebugCmdLevelUpLua(std::optional<int> levels)
{
	return DebugCmdLevelUp(levels.value_or(1));
}

} // namespace

sol::table LuaDevPlayerStatsModule(sol::state_view &lua)
{
	sol::table table = lua.create_table();
	LuaSetDocFn(table, "adjustHealth", "(amount: number)", "Adjust HP (amount can be negative)", &DebugCmdChangeHealth);
	LuaSetDocFn(table, "adjustMana", "(amount: number)", "Adjust mana (amount can be negative)", &DebugCmdChangeMana);
	LuaSetDocFn(table, "levelUp", "(amount: number = 1)", "Level the player up.", &DebugCmdLevelUpLua);
	LuaSetDocFn(table, "rejuvenate", "()", "Refill health", &DebugCmdRejuvenate);
	LuaSetDocFn(table, "setAttrToMax", "()", "Set Str, Mag, Dex, and Vit to maximum.", &DebugCmdMaxStats);
	LuaSetDocFn(table, "setAttrToMin", "()", "Set Str, Mag, Dex, and Vit to minimum.", &DebugCmdMinStats);
	return table;
}

} // namespace devilution
#endif // _DEBUG

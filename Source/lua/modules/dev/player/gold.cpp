/**
 * @file lua/modules/dev/player/gold.cpp
 *
 * Implementation of gold.
 */


#ifdef _DEBUG
#include "lua/modules/dev/player/gold.hpp"

#include <cstdint>
#include <optional>
#include <string>

#include <sol/sol.hpp>

#include "lua/metadoc.hpp"
#include "application/debug.h"
#include "game/items/items.hpp"
#include "game/players/players.hpp"

namespace devilution {
namespace {

std::string DebugCmdGiveGoldLua(std::optional<int> amount)
{
	return DebugCmdGiveGoldCheat(amount.value_or(GOLD_MAX_LIMIT * InventoryGridCells));
}

std::string DebugCmdTakeGoldLua(std::optional<int> amount)
{
	return DebugCmdTakeGoldCheat(amount.value_or(GOLD_MAX_LIMIT * InventoryGridCells));
}

} // namespace

sol::table LuaDevPlayerGoldModule(sol::state_view &lua)
{
	sol::table table = lua.create_table();
	LuaSetDocFn(table, "give", "(amount: number = MAX)", "Gives the player gold.", &DebugCmdGiveGoldLua);
	LuaSetDocFn(table, "take", "(amount: number = MAX)", "Takes the player's gold away.", &DebugCmdTakeGoldLua);
	return table;
}

} // namespace devilution
#endif // _DEBUG

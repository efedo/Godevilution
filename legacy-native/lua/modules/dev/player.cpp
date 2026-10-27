/**
 * @file lua/modules/dev/player.cpp
 *
 * Implementation of player.
 */

#ifdef _DEBUG
#include "lua/modules/dev/player.hpp"

#include <cstdint>
#include <string>

#include <sol/sol.hpp>

#include "application/debug.h"
#include "engine/assets.hpp"
#include "game/players/players.hpp"
#include "lua/metadoc.hpp"
#include "lua/modules/dev/player/gold.hpp"
#include "lua/modules/dev/player/spells.hpp"
#include "lua/modules/dev/player/stats.hpp"

namespace devilution {

namespace {

sol::table LuaDevPlayerTrnModule(sol::state_view &lua)
{
	sol::table table = lua.create_table();
	LuaSetDocFn(table, "mon", "(name: string)", "Set player TRN to monsters\\${name}.trn",
	    [](std::string_view name) { return DebugCmdSetPlayerTrn(StrCat("monsters\\", name, ".trn")); });
	LuaSetDocFn(table, "plr", "(name: string)", "Set player TRN to plrgfx\\${name}.trn",
	    [](std::string_view name) { return DebugCmdSetPlayerTrn(StrCat("plrgfx\\", name, ".trn")); });
	LuaSetDocFn(table, "clear", "()", "Unset player TRN",
	    []() { return DebugCmdSetPlayerTrn(""); });
	return table;
}

} // namespace

sol::table LuaDevPlayerModule(sol::state_view &lua)
{
	sol::table table = lua.create_table();
	LuaSetDocFn(table, "arrow", "(effect: 'normal'|'fire'|'lightning'|'spectral')", "Set arrow effect.", &DebugCmdArrow);
	LuaSetDocFn(table, "god", "(on: boolean = nil)", "Toggle god mode.", &DebugCmdGodMode);
	LuaSetDoc(table, "gold", "", "Adjust player gold.", LuaDevPlayerGoldModule(lua));
	LuaSetDocFn(table, "info", "(id: number = 0)", "Show player info.", &DebugCmdPlayerInfo);
	LuaSetDoc(table, "spells", "", "Adjust player spells.", LuaDevPlayerSpellsModule(lua));
	LuaSetDoc(table, "stats", "", "Adjust player stats (Strength, HP, etc).", LuaDevPlayerStatsModule(lua));
	LuaSetDoc(table, "trn", "", "Set player TRN to '${name}.trn'", LuaDevPlayerTrnModule(lua));
	LuaSetDocFn(table, "invisible", "(on: boolean = nil)", "Toggle invisibility.", &DebugCmdInvisible);
	return table;
}

} // namespace devilution
#endif // _DEBUG

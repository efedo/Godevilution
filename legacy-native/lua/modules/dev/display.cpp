/**
 * @file lua/modules/dev/display.cpp
 *
 * Implementation of display and window management.
 */

#ifdef _DEBUG
#include "lua/modules/dev/display.hpp"

#include <sol/sol.hpp>

#include "application/debug.h"
#include "lua/metadoc.hpp"

namespace devilution {

sol::table LuaDevDisplayModule(sol::state_view &lua)
{
	sol::table table = lua.create_table();
	LuaSetDocFn(table, "fps", "(name: string = nil)", "Toggle FPS display.", &DebugCmdToggleFPS);
	LuaSetDocFn(table, "fullbright", "(on: boolean = nil)", "Toggle light shading.", &DebugCmdFullbright);
	LuaSetDocFn(table, "grid", "(on: boolean = nil)", "Toggle showing the grid.", &DebugCmdShowGrid);
	LuaSetDocFn(table, "path", "(on: boolean = nil)", "Toggle path debug rendering.", &DebugCmdPath);
	LuaSetDocFn(table, "scrollView", "(on: boolean = nil)", "Toggle view scrolling via Shift+Mouse.", &DebugCmdScrollView);
	LuaSetDocFn(table, "tileData", "(name: string = nil)", "Toggle showing tile data.", &DebugCmdShowTileData);
	LuaSetDocFn(table, "vision", "(on: boolean = nil)", "Toggle vision debug rendering.", &DebugCmdVision);
	return table;
}

} // namespace devilution
#endif // _DEBUG

/**
 * @file lua/modules/dev/level/warp.cpp
 *
 * Implementation of warp.
 */


#ifdef _DEBUG
#include "lua/modules/dev/level/warp.hpp"

#include <cstdint>
#include <string>
#include <string_view>

#include <sol/sol.hpp>

#include "application/debug.h"
#include "ui/load_screens.h"
#include "game/levels/setmaps.h"
#include "lua/metadoc.hpp"
#include "game/players/players.hpp"
#include "game/quests/quests.hpp"
#include "utils/string/str_cat.hpp"

namespace devilution {
namespace {

std::string DebugCmdWarpToQuestLevel(uint8_t level)
{
	if (level < 1)
		return StrCat("Quest level number must be 1 or higher!");
	if (isSetLevel() && setLevelNumber() == level)
		return StrCat("You are already on quest level", level, "!");

	for (Quest &quest : Quests) {
		if (level != quest._qslvl)
			continue;

		setLevelType() = quest._qlvltype;
		StartNewLvl(*MyPlayer, WM_DIABSETLVL, level);

		return StrCat("Moved you to quest level ", SetLevelNames[level], ".");
	}

	return StrCat("Quest level ", level, " does not exist!");
}

std::string DebugCmdWarpToCustomMap(std::string_view path, int dunType, int x, int y)
{
	if (path.empty()) return "path is required";
	if (dunType < DTYPE_CATHEDRAL || dunType > DTYPE_LAST) return "invalid dunType";

	const Point spawn { x, y };
	if (!InDungeonBounds(spawn)) return "spawn location is out of bounds";

	TestMapPath = StrCat(path, ".dun");
	setLevelType() = static_cast<dungeon_type>(dunType);
	viewPosition() = spawn;

	StartNewLvl(*MyPlayer, WM_DIABSETLVL, SL_NONE);

	return StrCat("Moved you to ", TestMapPath, ".");
}

} // namespace

sol::table LuaDevLevelWarpModule(sol::state_view &lua)
{
	sol::table table = lua.create_table();
	LuaSetDocFn(table, "dungeon", "(n: number)", "Go to dungeon level (0 for town).", &DebugCmdWarpToDungeonLevel);
	LuaSetDocFn(table, "map", "(path: string, dunType: number, x: number, y: number)", "Go to custom {path}.dun level", &DebugCmdWarpToCustomMap);
	LuaSetDocFn(table, "quest", "(n: number)", "Go to quest level.", &DebugCmdWarpToQuestLevel);
	return table;
}

} // namespace devilution
#endif // _DEBUG

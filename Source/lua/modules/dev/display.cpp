/**
 * @file lua/modules/dev/display.cpp
 *
 * Implementation of display and window management.
 */


#ifdef _DEBUG
#include "lua/modules/dev/display.hpp"

#include <array>
#include <optional>
#include <string>

#include <sol/sol.hpp>

#include "application/debug.h"
#include "engine/lighting.h"
#include "lua/metadoc.hpp"
#include "game/players/players.hpp"
#include "utils/string/str_cat.hpp"

namespace devilution {
namespace {

std::string DebugCmdShowTileData(std::optional<std::string_view> dataType)
{
	static const std::array<std::string_view, 23> DataTypes {
		"microTiles",
		"piece",
		"transVal",
		"dLight",
		"dPreLight",
		"dFlags",
		"dPlayer",
		"dMonster",
		"missiles",
		"dCorpse",
		"dObject",
		"dItem",
		"dSpecial",
		"coords",
		"cursorcoords",
		"objectindex",
		"solid",
		"transparent",
		"trap",
		"AutomapView",
		"currentMegaTile",
		"replacementMegaTile",
		"protectedTiles()",
	};
	if (!dataType.has_value()) {
		std::string result = "Valid values for the first argument:\nclear";
		for (const std::string_view &str : DataTypes)
			StrAppend(result, ", ", str);
		return result;
	}
	if (*dataType == "clear") {
		SetDebugGridTextType(DebugGridTextItem::None);
		return "Tile data cleared.";
	}
	std::string_view requestedType = *dataType;
	if (requestedType == "dPiece")
		requestedType = "piece";
	if (requestedType == "dTransVal")
		requestedType = "transVal";
	bool found = false;
	int index = 0;
	for (const std::string_view &param : DataTypes) {
		index++;
		if (requestedType != param)
			continue;
		found = true;
		auto newGridText = static_cast<DebugGridTextItem>(index);
		if (newGridText == GetDebugGridTextType()) {
			SetDebugGridTextType(DebugGridTextItem::None);
			return "Tile data: Off";
		}
		SetDebugGridTextType(newGridText);
		break;
	}
	if (!found) {
		std::string result = "Invalid name! Valid names are:\nclear";
		for (const std::string_view &str : DataTypes)
			StrAppend(result, ", ", str);
		return result;
	}

	return "Tile data: On";
}

std::string DebugCmdScrollView(std::optional<bool> on)
{
	DebugScrollViewEnabled = on.value_or(!DebugScrollViewEnabled);
	if (!DebugScrollViewEnabled)
		InitMultiView();
	return StrCat("Scroll view: ", DebugScrollViewEnabled ? "On" : "Off");
}

} // namespace

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

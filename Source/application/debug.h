/**
 * @file debug.h
 *
 * Interface of debug functions.
 */
#pragma once

#include <cstdint>
#include <optional>
#include <string>
#include <string_view>

#include <ankerl/unordered_dense.h>

#include "application/diablo.h"
#include "engine/gfx/clx_sprite.hpp"
#include "engine/math/point.hpp"

namespace devilution {

extern std::string TestMapPath;
extern OptionalOwnedClxSpriteList pSquareCel;
extern bool DebugToggle;
extern bool DebugGodMode;
extern bool DebugInvisible;
extern bool DebugVision;
extern bool DebugPath;
extern bool DebugGrid;
extern ankerl::unordered_dense::map<int, Point> DebugCoordsMap;
extern bool DebugScrollViewEnabled;
extern std::string debugTRN;
extern uint32_t glMid1Seed[NUMLEVELS];
extern uint32_t glMid2Seed[NUMLEVELS];
extern uint32_t glMid3Seed[NUMLEVELS];
extern uint32_t glEndSeed[NUMLEVELS];

enum class DebugGridTextItem : uint16_t {
	None,
	microTiles,
	DPiece,
	DTransVal,
	DLight,
	DPreLight,
	DFlags,
	DPlayer,
	DMonster,
	missiles,
	DCorpse,
	DObject,
	DItem,
	DSpecial,

	coords,
	cursorcoords,
	objectindex,

	// take the tile piece ID as index
	Solid,
	Transparent,
	Trap,

	// megatiles
	AutomapView,
	CurrentMegaTile,
	ReplacementMegaTile,
	DProtected,
};

void FreeDebugGFX();
void LoadDebugGFX();
void GetDebugMonster();
void NextDebugMonster();
void SetDebugLevelSeedInfos(uint32_t mid1Seed, uint32_t mid2Seed, uint32_t mid3Seed, uint32_t endSeed);
bool IsDebugGridTextNeeded();
bool IsDebugGridInMegatiles();
DebugGridTextItem GetDebugGridTextType();
void SetDebugGridTextType(DebugGridTextItem value);
bool GetDebugGridText(Point dungeonCoords, std::string &debugGridText);
bool IsDebugAutomapHighlightNeeded();
bool ShouldHighlightDebugAutomapTile(Point position);
void AddDebugAutomapMonsterHighlight(std::string_view name);
void AddDebugAutomapItemHighlight(std::string_view name);
void AddDebugAutomapObjectHighlight(std::string_view name);
void ClearDebugAutomapHighlights();

#ifdef _DEBUG
std::string DebugCmdGodMode(std::optional<bool> on);
std::string DebugCmdInvisible(std::optional<bool> on);
std::string DebugCmdArrow(std::string_view effect);
std::string DebugCmdPlayerInfo(std::optional<uint8_t> id);
std::string DebugCmdRejuvenate();
std::string DebugCmdChangeHealth(int change);
std::string DebugCmdChangeMana(int change);
std::string DebugCmdMaxStats();
std::string DebugCmdMinStats();
std::string DebugCmdLevelUp(int levels);
std::string DebugCmdSetPlayerTrn(std::string_view path);
std::string DebugCmdShowGrid(std::optional<bool> on);
std::string DebugCmdVision(std::optional<bool> on);
std::string DebugCmdPath(std::optional<bool> on);
std::string DebugCmdFullbright(std::optional<bool> on);
std::string DebugCmdToggleFPS(std::optional<bool> on);
std::string DebugCmdShowTileData(std::optional<std::string_view> dataType);
std::string DebugCmdScrollView(std::optional<bool> on);
std::string DebugCmdLevelSeed(std::optional<uint8_t> level);
std::string DebugCmdSearchMonster(std::string_view name);
std::string DebugCmdSearchItem(std::string_view name);
std::string DebugCmdSearchObject(std::string_view name);
std::string DebugCmdClearSearch();
void RegisterNativeDebugCommands();
#endif

} // namespace devilution

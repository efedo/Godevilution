/**
 * @file themes.h
 *
 * Interface of the theme room placing algorithms.
 */
#pragma once

#include <cstdint>
#include <cstddef>

#include "game/levels/dungeon_common.h"
#include "tables/objdat.h"

namespace devilution {

struct ThemeStruct {
	theme_id ttype;
	int8_t ttval;
};

extern DVL_API_FOR_TEST ThemeStruct themes[MAXTHEMES];

void InitThemes();

/**
 * @brief HoldThemeRooms marks theme rooms as populated.
 */
void HoldThemeRooms();

/**
 * CreateThemeRooms adds thematic elements to rooms.
 */
void CreateThemeRooms();

#ifdef BUILD_TESTING
/** Fixture-owned private phase scratch/flags; no production replay interface. */
struct ThemeRoomGlobalsForTesting {
	int x;
	int y;
	size_t variant;
	bool cauldron;
	bool bloodFountain;
	bool murkyFountain;
	bool purifyingFountain;
	bool tearFountain;
	bool treasure;
};
ThemeRoomGlobalsForTesting CaptureThemeRoomGlobalsForTesting();
void RestoreThemeRoomGlobalsForTesting(const ThemeRoomGlobalsForTesting &state);
#endif

} // namespace devilution

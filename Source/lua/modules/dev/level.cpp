/**
 * @file lua/modules/dev/level.cpp
 *
 * Implementation of level.
 */


#ifdef _DEBUG
#include "lua/modules/dev/level.hpp"

#include <cstddef>
#include <cstdio>
#include <optional>
#include <string>

#include <sol/sol.hpp>

#include "application/debug.h"
#include "application/diablo.h"
#include "game/levels/dungeon_common.h"
#include "lua/metadoc.hpp"
#include "lua/modules/dev/level/map.hpp"
#include "lua/modules/dev/level/warp.hpp"
#include "game/monsters/monsters.hpp"
#include "game/objects/objects.hpp"
#include "game/players/players.hpp"
#include "utils/endian/endian_stream.hpp"
#include "utils/file/file_util.h"

namespace devilution {

namespace {

std::string ExportDun()
{
	const std::string levelName = StrCat(currentLevelNumber(), "-", DungeonSeeds[currentLevelNumber()], ".dun");
	FILE *dunFile = OpenFile(levelName.c_str(), "ab");

	WriteLE16(dunFile, DMAXX);
	WriteLE16(dunFile, DMAXY);

	/** Tiles. */
	for (int y = 0; y < DMAXY; y++) {
		for (int x = 0; x < DMAXX; x++) {
			WriteLE16(dunFile, megaTileAt(x, y).current());
		}
	}

	/** Padding */
	for (int y = 16; y < MAXDUNY - 16; y++) {
		for (int x = 16; x < MAXDUNX - 16; x++) {
			WriteLE16(dunFile, 0);
		}
	}

	/** Monsters */
	for (int y = 16; y < MAXDUNY - 16; y++) {
		for (int x = 16; x < MAXDUNX - 16; x++) {
			uint16_t monsterId = 0;
			const int16_t activeMonsterId = tileAt(x, y).monster();
			if (activeMonsterId > 0) {
				for (int i = 0; i < 157; i++) {
					if (MonstConvTbl[i] == Monsters[std::abs(activeMonsterId) - 1].type().type) {
						monsterId = i + 1;
						break;
					}
				}
			}
			WriteLE16(dunFile, monsterId);
		}
	}

	/** Objects */
	for (int y = 16; y < MAXDUNY - 16; y++) {
		for (int x = 16; x < MAXDUNX - 16; x++) {
			uint16_t objectId = 0;
			Object *object = FindObjectAtPosition({ x, y }, false);
			if (object != nullptr) {
				for (int i = 0; i < 147; i++) {
					if (ObjTypeConv[i] == object->_otype) {
						objectId = i;
						break;
					}
				}
			}
			WriteLE16(dunFile, objectId);
		}
	}

	/** Transparency */
	for (int y = 16; y < MAXDUNY - 16; y++) {
		for (int x = 16; x < MAXDUNX - 16; x++) {
			WriteLE16(dunFile, tileAt(x, y).transVal());
		}
	}
	std::fclose(dunFile);

	return StrCat("Successfully exported ", levelName, ".");
}

} // namespace

sol::table LuaDevLevelModule(sol::state_view &lua)
{
	sol::table table = lua.create_table();
	LuaSetDocFn(table, "exportDun", "()", "Save the current level as a dun-file.", &ExportDun);
	LuaSetDocFn(table, "map", "", "Automap-related commands.", LuaDevLevelMapModule(lua));
	LuaSetDocFn(table, "reset", "(n: number, seed: number = nil)", "Resets specified level.", &DebugCmdResetLevel);
	LuaSetDocFn(table, "seed", "(level: number = nil)", "Get the seed of the current or given level.", &DebugCmdLevelSeed);
	LuaSetDocFn(table, "warp", "", "Warp to a level or a custom map.", LuaDevLevelWarpModule(lua));
	return table;
}

} // namespace devilution
#endif // _DEBUG

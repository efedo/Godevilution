#pragma once

/**
 * @file game/levels/dungeon_common_defs.hpp
 *
 * Interface for dungeon_common defs.
 */


#include <cstdint>

#include "engine/math/rectangle.hpp"
#include "utils/enum_traits.h"

#define DMAXX 40
#define DMAXY 40

#define MAXDUNX (16 + DMAXX * 2 + 16)
#define MAXDUNY (16 + DMAXY * 2 + 16)

#define MAXTHEMES 50
#define MAXTILES 1379

namespace devilution {

enum dungeon_type : int8_t {
	DTYPE_TOWN,
	DTYPE_CATHEDRAL,
	DTYPE_CATACOMBS,
	DTYPE_CAVES,
	DTYPE_HELL,
	DTYPE_NEST,
	DTYPE_CRYPT,

	DTYPE_LAST = DTYPE_CRYPT,
	DTYPE_NONE = -1,
};

enum lvl_entry : uint8_t {
	ENTRY_MAIN,
	ENTRY_PREV,
	ENTRY_SETLVL,
	ENTRY_RTNLVL,
	ENTRY_LOAD,
	ENTRY_WARPLVL,
	ENTRY_TWARPDN,
	ENTRY_TWARPUP,
};

enum _setlevels : int8_t {
	SL_NONE,
	SL_SKELKING,
	SL_BONECHAMB,
	SL_MAZE,
	SL_POISONWATER,
	SL_VILEBETRAYER,

	SL_ARENA_CHURCH,
	SL_ARENA_HELL,
	SL_ARENA_CIRCLE_OF_LIFE,

	SL_FIRST_ARENA = SL_ARENA_CHURCH,
	SL_LAST = SL_ARENA_CIRCLE_OF_LIFE,
};

enum _difficulty : uint8_t {
	DIFF_NORMAL,
	DIFF_NIGHTMARE,
	DIFF_HELL,

	DIFF_LAST = DIFF_HELL,
};

enum class DungeonFlag : uint8_t {
	// clang-format off
	None                  = 0,
	Missile               = 1 << 0,
	Visible               = 1 << 1,
	DeadPlayer            = 1 << 2,
	Populated             = 1 << 3,
	MissileFireWall       = 1 << 4,
	MissileLightningWall  = 1 << 5,
	Lit                   = 1 << 6,
	Explored              = 1 << 7,
	SavedFlags            = (Populated | Lit | Explored),
	LoadedFlags           = (Missile | Visible | DeadPlayer | Populated | Lit | Explored)
	// clang-format on
};
use_enum_as_flags(DungeonFlag);

struct THEME_LOC {
	RectangleOf<uint8_t> room;
	int8_t ttval;
};

struct MegaTile {
	uint16_t micro1;
	uint16_t micro2;
	uint16_t micro3;
	uint16_t micro4;
};

struct ShadowStruct {
	uint8_t strig;
	uint8_t s1;
	uint8_t s2;
	uint8_t s3;
	uint8_t nv1;
	uint8_t nv2;
	uint8_t nv3;
};

} // namespace devilution

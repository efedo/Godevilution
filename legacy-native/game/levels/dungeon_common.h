/**
 * @file dungeon_common.h
 *
 * Interface of general dungeon generation code.
 */
#pragma once

#include <cstddef>
#include <cstdint>
#include <memory>
#include <optional>
#include <span>
#include <string>
#include <string_view>

#include <expected.hpp>

#include "engine/gfx/clx_sprite.hpp"
#include "engine/math/point.hpp"
#include "engine/math/rectangle.hpp"
#include "engine/render/world_renderer.h"
#include "engine/world.hpp"
#include "engine/math/world_tile.hpp"
#include "game/levels/dun_tile.hpp"
#include "game/levels/dungeon_common_defs.hpp"
#include "utils/attributes.h"
#include "utils/container/bitset2d.hpp"
#include "utils/enum_traits.h"

namespace devilution {

// _setlevels, _difficulty, DungeonFlag, THEME_LOC, MegaTile, ShadowStruct,
// MAXTHEMES and MAXTILES are defined in gendung_defs.hpp (included above).

inline bool IsArenaLevel(_setlevels setLevel)
{
	switch (setLevel) {
	case SL_ARENA_CHURCH:
	case SL_ARENA_HELL:
	case SL_ARENA_CIRCLE_OF_LIFE:
		return true;
	default:
		return false;
	}
}

tl::expected<dungeon_type, std::string> ParseDungeonType(std::string_view value);
tl::expected<_setlevels, std::string> ParseSetLevel(std::string_view value);

inline decltype(auto) dungeonMask() // Megatiles used by the generated map.
{
	return (currentLevel().DungeonMask_);
}

inline decltype(auto) protectedTiles() // Megatiles protected from generator changes.
{
	return (currentLevel().Protected_);
}

inline decltype(auto) setPieceRoom() // Set-piece room membership map.
{
	return (currentLevel().SetPieceRoom_);
}

inline decltype(auto) setPiece() // Active quest set-piece area.
{
	return (currentLevel().SetPiece_);
}

inline decltype(auto) specialCels() // Special CEL sprite data.
{
	return (currentLevel().pSpecialCels_);
}

inline decltype(auto) megaTiles() // Megatile definitions for the active dungeon.
{
	return (currentLevel().pMegaTiles_);
}

inline decltype(auto) dungeonCels() // Dungeon CEL sprite data.
{
	return (currentLevel().pDungeonCels_);
}

inline decltype(auto) tileProperties() // Properties indexed by dungeon piece.
{
	return (currentLevel().SOLData_);
}

inline decltype(auto) minimumDungeonPosition() // Minimum rendered dungeon position.
{
	return (currentLevel().dminPosition_);
}

inline decltype(auto) maximumDungeonPosition() // Maximum rendered dungeon position.
{
	return (currentLevel().dmaxPosition_);
}

inline decltype(auto) levelType() // Active dungeon type.
{
	return (currentLevel().typeRef());
}

inline decltype(auto) currentLevelNumber() // Active dungeon level number.
{
	return (currentLevel().levelNumRef());
}

inline decltype(auto) isSetLevel() // Whether the active level is a quest level.
{
	return (currentLevel().isSetLevelRef());
}

inline decltype(auto) setLevelNumber() // Active quest-level identifier.
{
	return (currentLevel().setLevelIdRef());
}

inline decltype(auto) setLevelType() // Dungeon type of the active quest level.
{
	return (currentLevel().typeRef());
}

inline decltype(auto) viewPosition() // Player viewpoint position.
{
	return (currentLevel().ViewPosition_);
}

inline decltype(auto) microTileLength() // Microtiles used by each dungeon piece.
{
	return (currentLevel().MicroTileLen_);
}

inline decltype(auto) nextTransparencyValue() // Next transparency-region identifier.
{
	return (currentLevel().TransVal_);
}

inline decltype(auto) visibleTransparencyRegions() // Transparency regions visible to the player.
{
	return (currentLevel().TransList_);
}

inline decltype(auto) themeCount() // Number of generated theme rooms.
{
	return (currentLevel().themeCount_);
}

inline decltype(auto) themeLocations() // Generated theme-room areas.
{
	return (currentLevel().themeLoc_);
}

inline decltype(auto) numThemes() // Number of placed theme instances.
{
	return (currentLevel().numThemes_);
}

inline decltype(auto) armorFlag() // Armor theme flag.
{
	return (currentLevel().armorFlag_);
}

inline decltype(auto) weaponFlag() // Weapon theme flag.
{
	return (currentLevel().weaponFlag_);
}

inline decltype(auto) zharlib() // Zhar library theme index.
{
	return (currentLevel().zharlib_);
}

inline decltype(auto) trigFlag() // Whether a trigger is active.
{
	return (currentLevel().trigFlag_);
}

inline decltype(auto) numTriggers() // Number of active triggers.
{
	return (currentLevel().numTriggers_);
}

inline decltype(auto) tWarpFrom() // Town warp origin level.
{
	return (currentLevel().tWarpFrom_);
}

inline decltype(auto) uberRow() // Uber room row.
{
	return (currentLevel().uberRow_);
}

inline decltype(auto) uberCol() // Uber room column.
{
	return (currentLevel().uberCol_);
}

inline decltype(auto) isUberRoomOpened() // Whether the uber room has been opened.
{
	return (currentLevel().isUberRoomOpened_);
}

inline decltype(auto) isUberLeverActivated() // Whether the uber lever has been activated.
{
	return (currentLevel().isUberLeverActivated_);
}

inline decltype(auto) uberDiabloMonsterIndex() // Index of the Uber Diablo monster.
{
	return (currentLevel().uberDiabloMonsterIndex_);
}

inline decltype(auto) tiles() // Runtime state for each world tile.
{
	return (currentLevel().tiles_);
}

inline decltype(auto) tileAt(Point position) // Runtime state for one world tile.
{
	return (currentLevel().tileAt(position));
}

inline decltype(auto) tileAt(int x, int y) // Runtime state for one world tile.
{
	return (currentLevel().tileAt(x, y));
}

/** Access a coarse dungeon megatile in the current level. */
inline DungeonMegaTile &megaTileAt(int x, int y)
{
	return currentLevel().megaTileAt(x, y);
}

/** Access a coarse dungeon megatile in the current level. */
inline DungeonMegaTile &megaTileAt(Point position)
{
	return currentLevel().megaTileAt(position);
}

/**
 * @brief Returns a std::span over the level's MICROS lookup table.
 *
 * The table is indexed by piece ID (0..MAXTILES-1) and maps each piece
 * to its LevelCelBlock data for rendering.
 */
[[nodiscard]] inline std::span<MICROS, MAXTILES> levelMicros() // Microtile lookup indexed by dungeon piece.
{
	return std::span<MICROS, MAXTILES>(currentLevel().DPieceMicros_);
}

#ifdef BUILD_TESTING
std::optional<WorldTileSize> GetSizeForThemeRoom();
#endif

dungeon_type GetLevelType(int level);
void CreateDungeon(uint32_t rseed, lvl_entry entry);

DVL_ALWAYS_INLINE constexpr bool InDungeonBounds(Point position)
{
	return position.x >= 0 && position.x < MAXDUNX && position.y >= 0 && position.y < MAXDUNY;
}

// Checks if a given tile contains at least one missile
DVL_ALWAYS_INLINE bool TileContainsMissile(Point position)
{
	return InDungeonBounds(position) && tileAt(position).hasAnyFlag(DungeonFlag::Missile);
}

// Checks if a given tile contains a player corpse
DVL_ALWAYS_INLINE bool TileContainsDeadPlayer(Point position)
{
	return InDungeonBounds(position) && tileAt(position).hasAnyFlag(DungeonFlag::DeadPlayer);
}

// Check if a given tile contains a decorative object (or similar non-pathable set piece)
DVL_ALWAYS_INLINE bool TileContainsSetPiece(Point position)
{
	return InDungeonBounds(position) && tileAt(position).hasAnyFlag(DungeonFlag::Populated);
}

// Checks if any player can currently see this tile
// Currently only used by monster AI routines so basic monsters out of sight can be ignored
// until they're likely to interact with the player

DVL_ALWAYS_INLINE bool IsTileVisible(Point position)
{
	return InDungeonBounds(position) && tileAt(position).hasAnyFlag(DungeonFlag::Visible);
}

// Checks if a light source is illuminating this tile
DVL_ALWAYS_INLINE bool IsTileLit(Point position)
{
	return InDungeonBounds(position) && tileAt(position).hasAnyFlag(DungeonFlag::Lit);
}

struct Miniset {
	WorldTileSize size;
	/* these are indexed as [y][x] */
	uint8_t search[6][6];
	uint8_t replace[6][6];

	/**
	 * @param position Coordinates of the dungeon tile to check
	 * @param respectProtected Match bug from Crypt levels if false
	 */
	bool matches(WorldTilePosition position, bool respectProtected = true) const
	{
		for (WorldTileCoord yy = 0; yy < size.height; yy++) {
			for (WorldTileCoord xx = 0; xx < size.width; xx++) {
				if (search[yy][xx] != 0 && megaTileAt(xx + position.x, yy + position.y).current() != search[yy][xx])
					return false;
				if (respectProtected && protectedTiles().test(xx + position.x, yy + position.y))
					return false;
			}
		}
		return true;
	}

	void place(WorldTilePosition position, bool protect = false) const
	{
		for (WorldTileCoord y = 0; y < size.height; y++) {
			for (WorldTileCoord x = 0; x < size.width; x++) {
				if (replace[y][x] == 0)
					continue;
				megaTileAt(x + position.x, y + position.y).setCurrent(replace[y][x]);
				if (protect)
					protectedTiles().set(x + position.x, y + position.y);
			}
		}
	}
};

DVL_ALWAYS_INLINE bool TileHasAny(Point coords, TileProperties property)
{
	return HasAnyOf(tileProperties()[tileAt(coords).piece()], property);
}

tl::expected<void, std::string> LoadLevelSOLData();
void SetDungeonMicros(std::unique_ptr<std::byte[]> &dungeonCels, uint_fast8_t &microTileLen);
void DRLG_InitTrans();
void FillCurrentMegaTiles(uint8_t value);
void SnapshotReplacementMegaTiles();
void DRLG_MRectTrans(WorldTilePosition origin, WorldTilePosition extent);
void DRLG_MRectTrans(WorldTileRectangle area);
void DRLG_RectTrans(WorldTileRectangle area);
void DRLG_CopyTrans(int sx, int sy, int dx, int dy);
void LoadTransparency(const uint16_t *dunData);
void LoadDungeonBase(const char *path, Point spawn, int floorId, int dirtId);
void Make_SetPC(WorldTileRectangle area);
/**
 * @param miniset The miniset to place
 * @param tries Tiles to try, 1600 will scan the full map
 * @param drlg1Quirk Match buggy behaviour of Diablo's Cathedral
 */
std::optional<Point> PlaceMiniSet(const Miniset &miniset, int tries = 199, bool drlg1Quirk = false);
void PlaceDunTiles(const uint16_t *dunData, Point position, int floorId = 0);
void DRLG_PlaceThemeRooms(int minSize, int maxSize, int floor, int freq, bool rndSize);
void DRLG_HoldThemeRooms();

// Returns the size in tiles of the specified ".dun" Data
WorldTileSize GetDunSize(const uint16_t *dunData);
void DRLG_LPass3(int lv);

// Checks if a theme room is located near (within 2 tiles of) the target point
bool IsNearThemeRoom(WorldTilePosition position);
void InitLevels();
void FloodTransparencyValues(uint8_t floorID);

} // namespace devilution

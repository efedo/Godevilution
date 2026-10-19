/**
 * @file levels/level.hpp
 *
 * The Level class owns all data for a single dungeon level: its identity,
 * fine world tiles, coarse dungeon megatiles, entity occupation maps, lighting tables,
 * theme rooms and per-level monster catalogue (Bestiary).
 *
 * Data members use a trailing underscore. Legacy state still being migrated
 * may have macro aliases in gendung.h; migrated state uses named accessors.
 */
#pragma once

#include <array>
#include <cstdint>
#include <memory>
#include <optional>

#include "engine/bestiary.hpp"
#include "engine/gfx/clx_sprite.hpp"
#include "engine/math/point.hpp"
#include "engine/math/rectangle.hpp"
#include "engine/math/world_tile.hpp"
#include "game/levels/dun_tile.hpp"
#include "game/levels/dungeon_common_defs.hpp"
#include "game/levels/tile.hpp"
#include "utils/container/bitset2d.hpp"

namespace devilution {

// ---------------------------------------------------------------------------
// LevelIndex / LevelId
// ---------------------------------------------------------------------------

using LevelIndex = int8_t; // Key for storing a Level inside World. Regular levels 0-24 map directly to their number.

constexpr LevelIndex LevelIndexNone = -1; // Sentinel: "no level / not loaded".

// Immutable identity of a dungeon level.
struct LevelId {
	uint8_t      levelNum   = 0;          // Number of the regular dungeon level.
	dungeon_type type       = DTYPE_TOWN; // Dungeon environment used by the level.
	bool         isSetLevel = false;      // Whether this identifies a quest set level.
	_setlevels   setLevelId = SL_NONE;    // Quest set-level identifier.

	bool operator==(const LevelId &) const = default;
};

// Coarse dungeon-map cell containing active and event replacement IDs.
// One megatile expands to a 2x2 area in the fine-grained world tile grid.
class DungeonMegaTile {
public:
	[[nodiscard]] uint8_t current() const;
	void setCurrent(uint8_t value);

	[[nodiscard]] uint8_t replacement() const;
	void setReplacement(uint8_t value);

	void snapshotReplacement();
	void applyReplacement();

private:
	uint8_t current_ = 0;     // Active megatile ID.
	uint8_t replacement_ = 0; // Megatile ID applied by scripted map changes.
};

static_assert(sizeof(DungeonMegaTile) == 2);

// ---------------------------------------------------------------------------
// Level
// ---------------------------------------------------------------------------

// All data that belongs to a single dungeon level.
// Every field previously a standalone global in gendung.cpp lives here; remaining legacy state may still
// have transitional aliases in gendung.h.
class Level {
public:
	Level(const Level &) = delete;
	Level &operator=(const Level &) = delete;

	Level(Level &&) = default;
	Level &operator=(Level &&) = default;

	// -------------------------------------------------------------------------
	// Identity
	// -------------------------------------------------------------------------
	[[nodiscard]] const LevelId &id()         const { return id_; }
	[[nodiscard]] uint8_t        levelNum()   const { return id_.levelNum; }
	[[nodiscard]] dungeon_type   type()       const { return id_.type; }
	[[nodiscard]] bool           isSetLevel() const { return id_.isSetLevel; }
	[[nodiscard]] _setlevels     setLevelId() const { return id_.setLevelId; }

	// brief Updates the level identity without recreating the level data.
	void setId(const LevelId &newId) { id_ = newId; }

	// -------------------------------------------------------------------------
	// Tile access (NEW - Phase 2 migration)
	// -------------------------------------------------------------------------

	//brief Access a tile at the given coordinates
	[[nodiscard]] Tile &tileAt(int x, int y) { return tiles_[x][y]; }
	[[nodiscard]] const Tile &tileAt(int x, int y) const { return tiles_[x][y]; }

	//Access a tile at the given Point.
	[[nodiscard]] Tile &tileAt(Point position) { return tiles_[position.x][position.y]; }
	[[nodiscard]] const Tile &tileAt(Point position) const { return tiles_[position.x][position.y]; }

	// Direct access to the tile grid (for bulk operations).
	[[nodiscard]] TileGrid &tiles() { return tiles_; }
	[[nodiscard]] const TileGrid &tiles() const { return tiles_; }

	[[nodiscard]] DungeonMegaTile &megaTileAt(int x, int y) { return megaTiles_[x][y]; }
	[[nodiscard]] const DungeonMegaTile &megaTileAt(int x, int y) const { return megaTiles_[x][y]; }
	[[nodiscard]] DungeonMegaTile &megaTileAt(Point position) { return megaTiles_[position.x][position.y]; }
	[[nodiscard]] const DungeonMegaTile &megaTileAt(Point position) const { return megaTiles_[position.x][position.y]; }

	[[nodiscard]] DungeonMegaTile (&megaTiles())[DMAXX][DMAXY] { return megaTiles_; }
	[[nodiscard]] const DungeonMegaTile (&megaTiles() const)[DMAXX][DMAXY] { return megaTiles_; }

	// -------------------------------------------------------------------------
	// Monster catalogue
	// -------------------------------------------------------------------------
	Bestiary bestiary; // Monster types available on this level.

	// -------------------------------------------------------------------------
	// Coarse dungeon generation state
	// -------------------------------------------------------------------------

	// EF todo: further cleanup and consolidation
	Bitset2d<DMAXX, DMAXY> DungeonMask_;            // Megatiles occupied during procedural generation.
	DungeonMegaTile megaTiles_[DMAXX][DMAXY] = {};  // Active and replacement IDs for each dungeon megatile.
	Bitset2d<DMAXX, DMAXY> Protected_;              // Megatiles that the level generator may not overwrite.
	WorldTileRectangle SetPieceRoom_ = {};          // Room reserved for the generated set piece.
	WorldTileRectangle SetPiece_ = {};              // Active set-piece area in world-tile coordinates.
	OptionalOwnedClxSpriteList pSpecialCels_;       // Special dungeon sprites used by the current level.
	std::unique_ptr<MegaTile[]> pMegaTiles_;        // Megatile definitions for the current dungeon type.
	std::unique_ptr<std::byte[]> pDungeonCels_;     // Raw dungeon CEL data for the current dungeon type.
	TileProperties SOLData_[MAXTILES] = {};         // Properties indexed by dungeon piece ID.
	WorldTilePosition dminPosition_ = {};           // Minimum populated world-tile coordinates.
	WorldTilePosition dmaxPosition_ = {};           // Maximum populated world-tile coordinates.

	// -------------------------------------------------------------------------
	// Mutable accessors for identity fields (for gendung backward compat)
	// -------------------------------------------------------------------------
	/** @return reference to the dungeon type held in id_. */
	[[nodiscard]] dungeon_type &typeRef() { return id_.type; }
	/** @return reference to the level number held in id_. */
	[[nodiscard]] uint8_t &levelNumRef() { return id_.levelNum; }
	/** @return reference to the set-level flag held in id_. */
	[[nodiscard]] bool &isSetLevelRef() { return id_.isSetLevel; }
	/** @return reference to the set-level id held in id_. */
	[[nodiscard]] _setlevels &setLevelIdRef() { return id_.setLevelId; }

	// -------------------------------------------------------------------------
	// View / rendering
	// -------------------------------------------------------------------------
	Point ViewPosition_ = {};                   // Player viewpoint in world-tile coordinates.
	uint_fast8_t MicroTileLen_ = 0;             // Number of microtiles in each dungeon-piece definition.
	int8_t TransVal_ = 0;                       // Next transparency-region identifier.
	std::array<bool, 256> TransList_ = {};      // Transparency regions currently visible to the player.

	// -------------------------------------------------------------------------
	// Per-tile maps  (MAXDUNX × MAXDUNY)
	// -------------------------------------------------------------------------

	TileGrid tiles_; // Runtime state for each world tile.

	// TODO: Migrate DPieceMicros_ to tiles_ and remove
	MICROS DPieceMicros_[MAXTILES] = {}; // Microtile layout indexed by dungeon piece ID.
	// -------------------------------------------------------------------------
	// Theme rooms
	// -------------------------------------------------------------------------
	int themeCount_ = 0;                    // Number of generated theme rooms.
	THEME_LOC themeLoc_[MAXTHEMES] = {};    // Generated theme-room locations.
	int numThemes_ = 0;                     // Number of placed theme instances.
	bool armorFlag_ = false;                // Armor theme flag.
	bool weaponFlag_ = false;               // Weapon theme flag.
	int zharlib_ = 0;                       // Zhar library theme index.

	// -------------------------------------------------------------------------
	// Triggers
	// -------------------------------------------------------------------------
	bool trigFlag_ = false;                 // Whether a trigger is active.
	int numTriggers_ = 0;                   // Number of active triggers.
	int tWarpFrom_ = 0;                     // Town warp origin level.

	// -------------------------------------------------------------------------
	// Crypt (Uber room)
	// -------------------------------------------------------------------------
	int uberRow_ = 0;                       // Uber room row.
	int uberCol_ = 0;                       // Uber room column.
	bool isUberRoomOpened_ = false;         // Whether the uber room has been opened.
	bool isUberLeverActivated_ = false;     // Whether the uber lever has been activated.
	int uberDiabloMonsterIndex_ = 0;        // Index of the Uber Diablo monster.

	// -------------------------------------------------------------------------
	// Factory
	// -------------------------------------------------------------------------
	explicit Level(LevelId id);
	static Level create(LevelId id);

private:
	LevelId id_; // Immutable identity used to store and retrieve the level.
};

} // namespace devilution

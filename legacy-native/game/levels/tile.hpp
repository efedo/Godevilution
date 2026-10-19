/**
 * @file levels/tile.hpp
 *
 * Tile class that consolidates all per-tile data previously stored
 * in separate [MAXDUNX][MAXDUNY] arrays.
 */
#pragma once

#include <cstddef>
#include <cstdint>
#include <iterator>
#include <type_traits>

#include "game/levels/dun_tile.hpp"
#include "game/levels/dungeon_common_defs.hpp"

namespace devilution {

/**
 * @brief Represents all data for a single tile in the dungeon.
 *
 * This class consolidates all the per-tile information that was previously
 * scattered across multiple [MAXDUNX][MAXDUNY] arrays in Level:
 * - piece ID
 * - transparency value
 * - dLight_: current realtime lighting
 * - dPreLight_: precalculated static lights
 * - dFlags_: various tile flags
 * - dPlayer_: player number at this tile
 * - dMonster_: monster number at this tile
 * - dCorpse_: corpse index and direction
 * - dObject_: object number at this tile
 * - dSpecial_: arch frame number from special tileset
 * - dItem_: item number at this tile
 *
 * By grouping these together, we improve:
 * - Data locality (cache performance)
 * - Code organization (related data in one place)
 * - Type safety (methods can validate tile state)
 * - Debuggability (inspect entire tile state at once)
 */
class Tile {
public:
	// -------------------------------------------------------------------------
	// Construction
	// -------------------------------------------------------------------------

	/** @brief Default constructor initializes all fields to zero/empty. */
	constexpr Tile() = default;

	// -------------------------------------------------------------------------
	// Piece / Rendering
	// -------------------------------------------------------------------------

	/** @brief The piece ID of this tile (from the tileset). */
	[[nodiscard]] constexpr uint16_t piece() const { return piece_; }
	constexpr void setPiece(uint16_t value) { piece_ = value; }

	/** @brief Transparency value for this tile. */
	[[nodiscard]] constexpr int8_t transVal() const { return transVal_; }
	constexpr void setTransVal(int8_t value) { transVal_ = value; }

	// -------------------------------------------------------------------------
	// Lighting
	// -------------------------------------------------------------------------

	/** @brief Current realtime lighting value. */
	[[nodiscard]] constexpr uint8_t light() const { return light_; }
	constexpr void setLight(uint8_t value) { light_ = value; }

	/** @brief Precalculated static light (baseline for dLight). */
	[[nodiscard]] constexpr uint8_t preLight() const { return preLight_; }
	constexpr void setPreLight(uint8_t value) { preLight_ = value; }

	// -------------------------------------------------------------------------
	// Flags
	// -------------------------------------------------------------------------

	/** @brief Various tile flags (missiles, visibility, populated, etc.). */
	[[nodiscard]] constexpr DungeonFlag flags() const { return flags_; }
	constexpr void setFlags(DungeonFlag value) { flags_ = value; }

	/** @brief Check if any of the given flags are set. */
	[[nodiscard]] constexpr bool hasAnyFlag(DungeonFlag mask) const {
		return HasAnyOf(flags_, mask);
	}

	/** @brief Check if all of the given flags are set. */
	[[nodiscard]] constexpr bool hasAllFlags(DungeonFlag mask) const {
		return HasAllOf(flags_, mask);
	}

	/** @brief Add flags to this tile. */
	constexpr void addFlags(DungeonFlag mask) {
		flags_ |= mask;
	}

	/** @brief Remove flags from this tile. */
	constexpr void removeFlags(DungeonFlag mask) {
		flags_ &= ~mask;
	}

	// -------------------------------------------------------------------------
	// Entity Occupation
	// -------------------------------------------------------------------------

	/**
	 * @brief Player number at this tile (players array index).
	 * @return 0 if no player, otherwise player index.
	 */
	[[nodiscard]] constexpr int8_t player() const { return player_; }
	constexpr void setPlayer(int8_t value) { player_ = value; }
	[[nodiscard]] constexpr bool hasPlayer() const { return player_ != 0; }

	/**
	 * @brief Monster number at this tile (monsters array index).
	 * Negative id indicates monster is moving.
	 * @return 0 if no monster, positive for stationary, negative for moving.
	 */
	[[nodiscard]] constexpr int16_t monster() const { return monster_; }
	constexpr void setMonster(int16_t value) { monster_ = value; }
	[[nodiscard]] constexpr bool hasMonster() const { return monster_ != 0; }
	[[nodiscard]] constexpr bool hasMovingMonster() const { return monster_ < 0; }

	/**
	 * @brief Corpse index and direction encoded together.
	 * corpse() & 0x1F = index of corpse (deads array)
	 * corpse() >> 5 = direction
	 * @return 0 if no corpse, otherwise encoded corpse data.
	 */
	[[nodiscard]] constexpr int8_t corpse() const { return corpse_; }
	constexpr void setCorpse(int8_t value) { corpse_ = value; }
	[[nodiscard]] constexpr bool hasCorpse() const { return corpse_ != 0; }
	[[nodiscard]] constexpr int8_t corpseIndex() const { return corpse_ & 0x1F; }
	[[nodiscard]] constexpr int8_t corpseDirection() const { return static_cast<uint8_t>(corpse_) >> 5; }

	/**
	 * @brief Object number at this tile (objects array index).
	 * Large objects have negative id for their extended area.
	 * @return 0 if no object, otherwise object index (negative for extended).
	 */
	[[nodiscard]] constexpr int8_t object() const { return object_; }
	constexpr void setObject(int8_t value) { object_ = value; }
	[[nodiscard]] constexpr bool hasObject() const { return object_ != 0; }
	[[nodiscard]] constexpr bool isObjectExtension() const { return object_ < 0; }

	/**
	 * @brief Item number at this tile (items array index).
	 * @return 0 if no item, otherwise item index.
	 */
	[[nodiscard]] constexpr int8_t item() const { return item_; }
	constexpr void setItem(int8_t value) { item_ = value; }
	[[nodiscard]] constexpr bool hasItem() const { return item_ != 0; }

	// -------------------------------------------------------------------------
	// Special / Set Pieces
	// -------------------------------------------------------------------------

	/**
	 * @brief Arch frame number from special tileset.
	 * In Tristram, this contains trees rather than arches.
	 * @return 0 if no special tile, otherwise frame index.
	 */
	[[nodiscard]] constexpr int8_t special() const { return special_; }
	constexpr void setSpecial(int8_t value) { special_ = value; }
	[[nodiscard]] constexpr bool hasSpecial() const { return special_ != 0; }

	// -------------------------------------------------------------------------
	// Utility / Query Methods
	// -------------------------------------------------------------------------

	/** @brief Clear all tile data to default/empty state. */
	constexpr void clear() {
		piece_ = 0;
		transVal_ = 0;
		light_ = 0;
		preLight_ = 0;
		flags_ = DungeonFlag::None;
		player_ = 0;
		monster_ = 0;
		corpse_ = 0;
		object_ = 0;
		special_ = 0;
		item_ = 0;
	}

	/** @brief Check if this tile is completely empty (no entities, special tiles, etc.). */
	[[nodiscard]] constexpr bool isEmpty() const {
		return player_ == 0 && monster_ == 0 && corpse_ == 0 
			&& object_ == 0 && item_ == 0 && special_ == 0;
	}

	/** @brief Check if this tile is occupied by any entity (player, monster, object, item). */
	[[nodiscard]] constexpr bool isOccupied() const {
		return player_ != 0 || monster_ != 0 || object_ != 0 || item_ != 0;
	}

	/** @brief Check if this tile is passable (no solid entities). */
	[[nodiscard]] constexpr bool isPassable() const {
		return !hasPlayer() && !hasMonster() && (object_ >= 0);
	}

	/** @brief Check if this tile contains any missile. */
	[[nodiscard]] constexpr bool hasMissile() const {
		return hasAnyFlag(DungeonFlag::Missile);
	}

	/** @brief Check if this tile is visible to players. */
	[[nodiscard]] constexpr bool isVisible() const {
		return hasAnyFlag(DungeonFlag::Visible);
	}

	/** @brief Check if this tile has been explored. */
	[[nodiscard]] constexpr bool isExplored() const {
		return hasAnyFlag(DungeonFlag::Explored);
	}

	/** @brief Check if this tile is lit. */
	[[nodiscard]] constexpr bool isLit() const {
		return hasAnyFlag(DungeonFlag::Lit);
	}

	/** @brief Check if this tile contains a dead player. */
	[[nodiscard]] constexpr bool hasDeadPlayer() const {
		return hasAnyFlag(DungeonFlag::DeadPlayer);
	}

	/** @brief Check if this tile is populated (contains set piece/decorative object). */
	[[nodiscard]] constexpr bool isPopulated() const {
		return hasAnyFlag(DungeonFlag::Populated);
	}

private:
	// -------------------------------------------------------------------------
	// Data Members
	// -------------------------------------------------------------------------

	/** Piece ID from the active tileset. */
	uint16_t piece_ = 0;

	/** Transparency-region ID. */
	int8_t transVal_ = 0;

	/** @see dLight - current realtime lighting */
	uint8_t light_ = 0;

	/** @see dPreLight - precalculated static light */
	uint8_t preLight_ = 0;

	/** @see dFlags - various tile flags */
	DungeonFlag flags_ = DungeonFlag::None;

	/** @see dPlayer - player number (players array index) */
	int8_t player_ = 0;

	/** @see dMonster - monster number (monsters array index, negative if moving) */
	int16_t monster_ = 0;

	/** @see dCorpse - corpse index and direction encoded (& 0x1F = index, >> 5 = direction) */
	int8_t corpse_ = 0;

	/** @see dObject - object number (objects array index, negative for extension) */
	int8_t object_ = 0;

	/** @see dSpecial - arch frame number from special tileset */
	int8_t special_ = 0;

	/** @see dItem - item number (items array index) */
	int8_t item_ = 0;
};

// Verify that Tile is small and efficient
static_assert(sizeof(Tile) <= 16, "Tile should be compact for cache efficiency");

class TileGrid {
	template <bool IsConst>
	struct PositionEntry {
		using TileReference = std::conditional_t<IsConst, const Tile &, Tile &>;

		Point position;
		TileReference tile;
	};

	template <bool IsConst, bool ColumnMajor>
	class Iterator {
		using Grid = std::conditional_t<IsConst, const TileGrid, TileGrid>;

	public:
		using difference_type = std::ptrdiff_t;
		using value_type = Tile;
		using pointer = std::conditional_t<IsConst, const Tile *, Tile *>;
		using reference = std::conditional_t<IsConst, const Tile &, Tile &>;
		using iterator_category = std::forward_iterator_tag;

		Iterator() = default;
		Iterator(Grid *grid, size_t index)
			: grid_(grid)
			, index_(index)
		{
		}

		template <bool OtherConst>
			requires(IsConst && !OtherConst)
		Iterator(const Iterator<OtherConst, ColumnMajor> &other)
			: grid_(other.grid_)
			, index_(other.index_)
		{
		}

		reference operator*() const
		{
			const size_t x = ColumnMajor ? index_ / MAXDUNY : index_ % MAXDUNX;
			const size_t y = ColumnMajor ? index_ % MAXDUNY : index_ / MAXDUNX;
			return (*grid_)[x][y];
		}

		pointer operator->() const
		{
			return &operator*();
		}

		Iterator &operator++()
		{
			++index_;
			return *this;
		}

		Iterator operator++(int)
		{
			Iterator previous = *this;
			++*this;
			return previous;
		}

		bool operator==(const Iterator &) const = default;

	private:
		template <bool, bool>
		friend class Iterator;

		Grid *grid_ = nullptr;
		size_t index_ = 0;
	};

	template <bool IsConst, bool ColumnMajor>
	class PositionIterator {
		using Grid = std::conditional_t<IsConst, const TileGrid, TileGrid>;

	public:
		using difference_type = std::ptrdiff_t;
		using value_type = PositionEntry<IsConst>;
		using reference = value_type;
		using iterator_category = std::forward_iterator_tag;

		PositionIterator() = default;
		PositionIterator(Grid *grid, size_t index)
			: grid_(grid)
			, index_(index)
		{
		}

		template <bool OtherConst>
			requires(IsConst && !OtherConst)
		PositionIterator(const PositionIterator<OtherConst, ColumnMajor> &other)
			: grid_(other.grid_)
			, index_(other.index_)
		{
		}

		reference operator*() const
		{
			const size_t x = ColumnMajor ? index_ / MAXDUNY : index_ % MAXDUNX;
			const size_t y = ColumnMajor ? index_ % MAXDUNY : index_ / MAXDUNX;
			return {
				{ static_cast<WorldTileCoord>(x), static_cast<WorldTileCoord>(y) },
				(*grid_)[x][y],
			};
		}

		PositionIterator &operator++()
		{
			++index_;
			return *this;
		}

		PositionIterator operator++(int)
		{
			PositionIterator previous = *this;
			++*this;
			return previous;
		}

		bool operator==(const PositionIterator &) const = default;

	private:
		template <bool, bool>
		friend class PositionIterator;

		Grid *grid_ = nullptr;
		size_t index_ = 0;
	};

	template <bool IsConst, bool ColumnMajor, bool IncludePositions>
	class Range {
		using Grid = std::conditional_t<IsConst, const TileGrid, TileGrid>;

	public:
		explicit Range(Grid &grid)
			: grid_(&grid)
		{
		}

		[[nodiscard]] auto begin() const
		{
			if constexpr (IncludePositions)
				return PositionIterator<IsConst, ColumnMajor> { grid_, 0 };
			else
				return Iterator<IsConst, ColumnMajor> { grid_, 0 };
		}

		[[nodiscard]] auto end() const
		{
			if constexpr (IncludePositions)
				return PositionIterator<IsConst, ColumnMajor> { grid_, MAXDUNX * MAXDUNY };
			else
				return Iterator<IsConst, ColumnMajor> { grid_, MAXDUNX * MAXDUNY };
		}

	private:
		Grid *grid_;
	};

public:
	using iterator = Iterator<false, false>;
	using const_iterator = Iterator<true, false>;

	[[nodiscard]] Tile *operator[](size_t x)
	{
		return tiles_[x];
	}

	[[nodiscard]] const Tile *operator[](size_t x) const
	{
		return tiles_[x];
	}

	[[nodiscard]] iterator begin()
	{
		return { this, 0 };
	}

	[[nodiscard]] iterator end()
	{
		return { this, MAXDUNX * MAXDUNY };
	}

	[[nodiscard]] const_iterator begin() const
	{
		return { this, 0 };
	}

	[[nodiscard]] const_iterator end() const
	{
		return { this, MAXDUNX * MAXDUNY };
	}

	[[nodiscard]] auto columnMajor()
	{
		return Range<false, true, false> { *this };
	}

	[[nodiscard]] auto columnMajor() const
	{
		return Range<true, true, false> { *this };
	}

	// Iterates tiles together with their positions in Y-major order.
	[[nodiscard]] auto withPositions()
	{
		return Range<false, false, true> { *this };
	}

	[[nodiscard]] auto withPositions() const
	{
		return Range<true, false, true> { *this };
	}

	// Iterates tiles together with their positions in X-major order.
	[[nodiscard]] auto withPositionsColumnMajor()
	{
		return Range<false, true, true> { *this };
	}

	[[nodiscard]] auto withPositionsColumnMajor() const
	{
		return Range<true, true, true> { *this };
	}

private:
	Tile tiles_[MAXDUNX][MAXDUNY] = {};
};

static_assert(sizeof(TileGrid) == sizeof(Tile[MAXDUNX][MAXDUNY]));

} // namespace devilution

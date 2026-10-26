#include <gtest/gtest.h>

#include <type_traits>
#include <vector>

#include "game/levels/dungeon_common.h"
#include "game/levels/level.hpp"
#include "game/levels/town.h"
#include "game/objects/objects.hpp"

#if defined(DungeonMask) || defined(Protected) || defined(SetPieceRoom) || defined(SetPiece)      \
    || defined(pSpecialCels) || defined(pMegaTiles) || defined(pDungeonCels) || defined(SOLData)  \
    || defined(dminPosition) || defined(dmaxPosition) || defined(leveltype) || defined(currlevel) \
    || defined(setlevel) || defined(setlvlnum) || defined(setlvltype) || defined(ViewPosition)    \
    || defined(MicroTileLen) || defined(TransVal) || defined(TransList) || defined(DPieceMicros)  \
    || defined(themeCount) || defined(themeLoc) || defined(tiles) || defined(tileAt)
#error "Legacy dungeon_common macro shims must not be defined"
#endif

namespace devilution {
namespace {

template <typename T>
concept HasLegacyDTransVal = requires(T level) {
	level.dTransVal_;
};

template <typename T>
concept HasLegacyDungeon = requires(T level) {
	level.dungeon_;
};

template <typename T>
concept HasLegacyPDungeon = requires(T level) {
	level.pdungeon_;
};

static_assert(!HasLegacyDTransVal<Level>);
static_assert(!HasLegacyDungeon<Level>);
static_assert(!HasLegacyPDungeon<Level>);
static_assert(sizeof(TileGrid) == sizeof(Tile[MAXDUNX][MAXDUNY]));

void SelectTownLevel()
{
	currentLevel().setId(Level::create(LevelId { .levelNum = 0, .type = DTYPE_TOWN }).id());
}

TEST(TileMigrationSyncTest, TileHasAnyUsesTilePiece)
{
	SelectTownLevel();
	InitLevels();

	constexpr Point Pos { 16, 16 };
	tileAt(Pos).setPiece(230);
	tileProperties()[230] = TileProperties::Solid;

	EXPECT_TRUE(TileHasAny(Pos, TileProperties::Solid));
}

TEST(TileMigrationSyncTest, TileGridDefaultsToYMajorTraversal)
{
	TileGrid grid;
	for (int x = 0; x < MAXDUNX; ++x) {
		for (int y = 0; y < MAXDUNY; ++y)
			grid[x][y].setPiece(static_cast<uint16_t>(y * MAXDUNX + x));
	}

	std::vector<uint16_t> pieces;
	for (const Tile &tile : grid)
		pieces.push_back(tile.piece());

	ASSERT_EQ(pieces.size(), MAXDUNX * MAXDUNY);
	for (size_t i = 0; i < pieces.size(); ++i)
		EXPECT_EQ(pieces[i], i);
}

TEST(TileMigrationSyncTest, TileGridSupportsXMajorTraversal)
{
	TileGrid grid;
	for (int x = 0; x < MAXDUNX; ++x) {
		for (int y = 0; y < MAXDUNY; ++y)
			grid[x][y].setPiece(static_cast<uint16_t>(x * MAXDUNY + y));
	}

	std::vector<uint16_t> pieces;
	const TileGrid &constGrid = grid;
	for (const Tile &tile : constGrid.columnMajor())
		pieces.push_back(tile.piece());

	ASSERT_EQ(pieces.size(), MAXDUNX * MAXDUNY);
	for (size_t i = 0; i < pieces.size(); ++i)
		EXPECT_EQ(pieces[i], i);
}

TEST(TileMigrationSyncTest, TileGridIterationSupportsMutationAndConstAccess)
{
	TileGrid grid;
	for (Tile &tile : grid)
		tile.setTransVal(7);

	const TileGrid &constGrid = grid;
	size_t visited = 0;
	for (const Tile &tile : constGrid) {
		EXPECT_EQ(tile.transVal(), 7);
		++visited;
	}

	EXPECT_EQ(visited, MAXDUNX * MAXDUNY);
	EXPECT_EQ(grid[3][4].transVal(), 7);
}

TEST(TileMigrationSyncTest, TileGridPositionRangeDefaultsToYMajorTraversal)
{
	TileGrid grid;
	size_t visited = 0;
	for (auto [position, tile] : grid.withPositions()) {
		const Point expected {
			static_cast<WorldTileCoord>(visited % MAXDUNX),
			static_cast<WorldTileCoord>(visited / MAXDUNX),
		};
		EXPECT_EQ(position, expected);
		tile.setPiece(static_cast<uint16_t>(visited));
		++visited;
	}

	EXPECT_EQ(visited, MAXDUNX * MAXDUNY);
	for (size_t y = 0; y < MAXDUNY; ++y) {
		for (size_t x = 0; x < MAXDUNX; ++x)
			EXPECT_EQ(grid[x][y].piece(), y * MAXDUNX + x);
	}

	for (auto [position, tile] : grid.withPositionsColumnMajor())
		tile.setTransVal(static_cast<int8_t>((position.x + position.y) % 16));
	EXPECT_EQ(grid[3][4].transVal(), 7);
}

TEST(TileMigrationSyncTest, TileGridPositionRangeSupportsXMajorConstTraversal)
{
	TileGrid grid;
	for (size_t x = 0; x < MAXDUNX; ++x) {
		for (size_t y = 0; y < MAXDUNY; ++y)
			grid[x][y].setPiece(static_cast<uint16_t>(x * MAXDUNY + y));
	}

	const TileGrid &constGrid = grid;
	size_t visited = 0;
	for (auto [position, tile] : constGrid.withPositionsColumnMajor()) {
		const Point expected {
			static_cast<WorldTileCoord>(visited / MAXDUNY),
			static_cast<WorldTileCoord>(visited % MAXDUNY),
		};
		EXPECT_EQ(position, expected);
		EXPECT_EQ(tile.piece(), visited);
		static_assert(std::is_const_v<std::remove_reference_t<decltype(tile)>>);
		++visited;
	}

	EXPECT_EQ(visited, MAXDUNX * MAXDUNY);

	visited = 0;
	for ([[maybe_unused]] auto entry : constGrid.withPositions())
		++visited;
	EXPECT_EQ(visited, MAXDUNX * MAXDUNY);
}

TEST(TileMigrationSyncTest, MegaTileStatesAreIndependent)
{
	Level level = Level::create(LevelId { .levelNum = 1, .type = DTYPE_CATHEDRAL });

	EXPECT_EQ(level.megaTileAt(3, 4).current(), 0);
	EXPECT_EQ(level.megaTileAt(3, 4).replacement(), 0);

	level.megaTileAt(3, 4).setCurrent(7);
	level.megaTileAt(3, 4).setReplacement(9);

	EXPECT_EQ(level.megaTileAt(3, 4).current(), 7);
	EXPECT_EQ(level.megaTileAt(3, 4).replacement(), 9);
}

TEST(TileMigrationSyncTest, SnapshotAndApplyMegaTileReplacement)
{
	SelectTownLevel();
	InitLevels();

	FillCurrentMegaTiles(3);
	megaTileAt(5, 6).setCurrent(7);
	SnapshotReplacementMegaTiles();
	megaTileAt(5, 6).setReplacement(9);

	EXPECT_EQ(megaTileAt(0, 0).replacement(), 3);
	EXPECT_EQ(megaTileAt(5, 6).current(), 7);
	EXPECT_EQ(megaTileAt(5, 6).replacement(), 9);

	megaTileAt(5, 6).applyReplacement();

	EXPECT_EQ(megaTileAt(5, 6).current(), 9);
	EXPECT_EQ(megaTileAt(5, 6).replacement(), 9);
}

TEST(TileMigrationSyncTest, RuntimeMapChangeAppliesReplacementMegaTile)
{
	SelectTownLevel();
	InitLevels();
	levelType() = DTYPE_TOWN;
	megaTiles() = std::make_unique<MegaTile[]>(1);
	megaTileAt(5, 6).setCurrent(2);
	megaTileAt(5, 6).setReplacement(1);

	ObjChangeMap(5, 6, 5, 6);

	EXPECT_EQ(megaTileAt(5, 6).current(), 1);
	EXPECT_EQ(megaTileAt(5, 6).replacement(), 1);
}

TEST(TileMigrationSyncTest, InitTransparencyClearsOnlyTransparency)
{
	SelectTownLevel();
	InitLevels();

	constexpr Point Pos { 20, 20 };
	tileAt(Pos).setPiece(321);
	tileAt(Pos).setTransVal(7);

	DRLG_InitTrans();

	EXPECT_EQ(tileAt(Pos).piece(), 321);
	EXPECT_EQ(tileAt(Pos).transVal(), 0);
	EXPECT_EQ(nextTransparencyValue(), 1);
}

TEST(TileMigrationSyncTest, RectTransparencyAssignsAndAdvancesRegion)
{
	SelectTownLevel();
	InitLevels();
	DRLG_InitTrans();

	DRLG_RectTrans({ { 20, 30 }, { 2, 1 } });

	EXPECT_EQ(tileAt(20, 30).transVal(), 1);
	EXPECT_EQ(tileAt(21, 30).transVal(), 1);
	EXPECT_EQ(tileAt(22, 31).transVal(), 1);
	EXPECT_EQ(tileAt(19, 30).transVal(), 0);
	EXPECT_EQ(nextTransparencyValue(), 2);
}

TEST(TileMigrationSyncTest, CopyTransparencyCopiesTileRegion)
{
	SelectTownLevel();
	InitLevels();
	tileAt(10, 11).setTransVal(9);
	tileAt(12, 13).setTransVal(0);

	DRLG_CopyTrans(10, 11, 12, 13);

	EXPECT_EQ(tileAt(12, 13).transVal(), 9);
}

TEST(TileMigrationSyncTest, TownOpenHivePopulatesTilePieces)
{
	SelectTownLevel();
	InitLevels();

	TownOpenHive();

	EXPECT_EQ(tileAt(78, 60).piece(), 0x489);
	EXPECT_EQ(tileAt(79, 61).piece(), 0x50d);
	EXPECT_EQ(tileAt(86, 61).piece(), 17);
}

TEST(TileMigrationSyncTest, TownOpenGravePopulatesTilePieces)
{
	SelectTownLevel();
	InitLevels();

	TownOpenGrave();

	EXPECT_EQ(tileAt(36, 21).piece(), 0x532);
	EXPECT_EQ(tileAt(37, 24).piece(), 0x539);
	EXPECT_EQ(tileAt(34, 21).piece(), 0x53b);
}

} // namespace
} // namespace devilution

/**
 * @file phase4b_entity_test.cpp
 *
 * GTest suite for Phase 4B entity data migration to Tile API.
 * Tests validate that dPlayer, dMonster, dCorpse, dObject, dItem, dSpecial
 * accessors work correctly through the Tile class.
 */

#include <gtest/gtest.h>

#include <memory>

#include "engine/math/point.hpp"
#include "engine/math/world_tile.hpp"
#include "game/levels/dungeon_common.h"
#include "game/levels/level.hpp"
#include "game/levels/tile.hpp"
#include "game/levels/tile_properties.hpp"
#include "game/objects/objects.hpp"
#include "utils/endian/endian_swap.hpp"

namespace devilution {

class Phase4BEntityTest : public ::testing::Test {
protected:
	void SetUp() override
	{
		// Initialize a test level
		currentLevel().setId(Level::create(LevelId { .levelNum = 0, .type = DTYPE_TOWN }).id());
	}

	void TearDown() override
	{
		// Clean up if needed
	}
};

// ============================================================================
// Player Entity Tests
// ============================================================================

TEST_F(Phase4BEntityTest, PlayerEntityRead)
{
	const Point testPos { 50, 50 };
	Tile &tile = tileAt(testPos);

	// Initially should be 0 (no player)
	EXPECT_EQ(tile.player(), 0);
	EXPECT_FALSE(tile.hasPlayer());

	// Set player
	tile.setPlayer(1);
	EXPECT_EQ(tile.player(), 1);
	EXPECT_TRUE(tile.hasPlayer());
}

TEST_F(Phase4BEntityTest, LegacyGenerationPassUpdatesTilePieces)
{
	currentLevel().setId(Level::create(LevelId { .levelNum = 1, .type = DTYPE_CATHEDRAL }).id());
	megaTiles() = std::make_unique<MegaTile[]>(1);
	megaTiles()[0] = MegaTile {
		Swap16LE(static_cast<uint16_t>(101)),
		Swap16LE(static_cast<uint16_t>(102)),
		Swap16LE(static_cast<uint16_t>(103)),
		Swap16LE(static_cast<uint16_t>(104)),
	};

	for (auto &column : currentLevel().megaTiles()) {
		for (DungeonMegaTile &megaTile : column) {
			megaTile.setCurrent(1);
		}
	}
	tiles()[16][16].clear();

	DRLG_LPass3(0);

	EXPECT_EQ(tileAt(16, 16).piece(), 101);
	EXPECT_EQ(tileAt(17, 16).piece(), 102);
	EXPECT_EQ(tileAt(16, 17).piece(), 103);
	EXPECT_EQ(tileAt(17, 17).piece(), 104);
}

TEST_F(Phase4BEntityTest, ObjectMicroTileUpdatesTilePieces)
{
	const Point position { 60, 60 };
	tileAt(position).setPiece(0);

	TestObjSetMicro(position, 314);

	EXPECT_EQ(tileAt(position).piece(), 314);
}

TEST_F(Phase4BEntityTest, TileOccupancyQueriesUseTileEntities)
{
	const Point monsterPos { 50, 50 };
	const Point playerPos { 51, 50 };
	tileAt(monsterPos).setMonster(7);
	tileAt(playerPos).setPlayer(2);

	EXPECT_TRUE(IsTileOccupied(monsterPos));
	EXPECT_TRUE(IsTileOccupied(playerPos));
}

TEST_F(Phase4BEntityTest, PlayerEntityWrite)
{
	const Point testPos { 60, 60 };
	Tile &tile = tileAt(testPos);

	// Write positive (stationary player)
	tile.setPlayer(3);
	EXPECT_EQ(tile.player(), 3);

	// Write negative (moving player)
	tile.setPlayer(-2);
	EXPECT_EQ(tile.player(), -2);

	// Write zero (clear)
	tile.setPlayer(0);
	EXPECT_EQ(tile.player(), 0);
	EXPECT_FALSE(tile.hasPlayer());
}

TEST_F(Phase4BEntityTest, PlayerMovingDetection)
{
	const Point testPos { 70, 70 };
	Tile &tile = tileAt(testPos);

	// Positive: stationary player
	tile.setPlayer(1);
	EXPECT_TRUE(tile.hasPlayer());

	// Negative: moving player (use -moving check if exists in implementation)
	tile.setPlayer(-1);
	EXPECT_TRUE(tile.hasPlayer());
}

// ============================================================================
// Monster Entity Tests
// ============================================================================

TEST_F(Phase4BEntityTest, MonsterEntityRead)
{
	const Point testPos { 40, 40 };
	Tile &tile = tileAt(testPos);

	// Initially should be 0 (no monster)
	EXPECT_EQ(tile.monster(), 0);
	EXPECT_FALSE(tile.hasMonster());

	// Set monster
	tile.setMonster(5);
	EXPECT_EQ(tile.monster(), 5);
	EXPECT_TRUE(tile.hasMonster());
}

TEST_F(Phase4BEntityTest, MonsterEntityWrite)
{
	const Point testPos { 45, 45 };
	Tile &tile = tileAt(testPos);

	// Write positive (stationary monster)
	tile.setMonster(2);
	EXPECT_EQ(tile.monster(), 2);

	// Write negative (moving monster)
	tile.setMonster(-3);
	EXPECT_EQ(tile.monster(), -3);
	EXPECT_TRUE(tile.hasMovingMonster());

	// Write zero (clear)
	tile.setMonster(0);
	EXPECT_EQ(tile.monster(), 0);
	EXPECT_FALSE(tile.hasMonster());
}

TEST_F(Phase4BEntityTest, MonsterMovingDetection)
{
	const Point testPos { 55, 55 };
	Tile &tile = tileAt(testPos);

	// Stationary
	tile.setMonster(1);
	EXPECT_FALSE(tile.hasMovingMonster());

	// Moving (negative)
	tile.setMonster(-1);
	EXPECT_TRUE(tile.hasMovingMonster());
}

// ============================================================================
// Corpse Entity Tests
// ============================================================================

TEST_F(Phase4BEntityTest, CorpseEntityRead)
{
	const Point testPos { 30, 30 };
	Tile &tile = tileAt(testPos);

	// Initially should be 0 (no corpse)
	EXPECT_EQ(tile.corpse(), 0);
	EXPECT_FALSE(tile.hasCorpse());

	// Set corpse with encoded direction
	// corpse() & 0x1F = index, corpse() >> 5 = direction
	int8_t encoded = (2 << 5) | 5; // direction 2, index 5
	tile.setCorpse(encoded);
	EXPECT_EQ(tile.corpse(), encoded);
	EXPECT_TRUE(tile.hasCorpse());
	EXPECT_EQ(tile.corpseIndex(), 5);
	EXPECT_EQ(tile.corpseDirection(), 2);
}

TEST_F(Phase4BEntityTest, CorpseEntityWrite)
{
	const Point testPos { 35, 35 };
	Tile &tile = tileAt(testPos);

	// Write corpse with encoding
	int8_t encoded = (1 << 5) | 3;
	tile.setCorpse(encoded);
	EXPECT_EQ(tile.corpse(), encoded);

	// Clear corpse
	tile.setCorpse(0);
	EXPECT_EQ(tile.corpse(), 0);
	EXPECT_FALSE(tile.hasCorpse());
}

TEST_F(Phase4BEntityTest, CorpseIndexExtraction)
{
	const Point testPos { 38, 38 };
	Tile &tile = tileAt(testPos);

	// Test various encodings
	for (int dir = 0; dir < 8; dir++) {
		for (int idx = 0; idx < 32; idx++) {
			int8_t encoded = (dir << 5) | idx;
			tile.setCorpse(encoded);
			EXPECT_EQ(tile.corpseIndex(), idx);
			EXPECT_EQ(tile.corpseDirection(), dir);
		}
	}
}

// ============================================================================
// Object Entity Tests
// ============================================================================

TEST_F(Phase4BEntityTest, ObjectEntityRead)
{
	const Point testPos { 25, 25 };
	Tile &tile = tileAt(testPos);

	// Initially should be 0 (no object)
	EXPECT_EQ(tile.object(), 0);
	EXPECT_FALSE(tile.hasObject());

	// Set object
	tile.setObject(1);
	EXPECT_EQ(tile.object(), 1);
	EXPECT_TRUE(tile.hasObject());
}

TEST_F(Phase4BEntityTest, ObjectEntityWrite)
{
	const Point testPos { 28, 28 };
	Tile &tile = tileAt(testPos);

	// Write positive (main object)
	tile.setObject(4);
	EXPECT_EQ(tile.object(), 4);
	EXPECT_FALSE(tile.isObjectExtension());

	// Write negative (extended object area)
	tile.setObject(-2);
	EXPECT_EQ(tile.object(), -2);
	EXPECT_TRUE(tile.isObjectExtension());

	// Write zero (clear)
	tile.setObject(0);
	EXPECT_EQ(tile.object(), 0);
	EXPECT_FALSE(tile.hasObject());
}

TEST_F(Phase4BEntityTest, ObjectExtensionDetection)
{
	const Point testPos { 32, 32 };
	Tile &tile = tileAt(testPos);

	// Positive: main object
	tile.setObject(1);
	EXPECT_FALSE(tile.isObjectExtension());

	// Negative: extended area
	tile.setObject(-1);
	EXPECT_TRUE(tile.isObjectExtension());
}

// ============================================================================
// Item Entity Tests
// ============================================================================

TEST_F(Phase4BEntityTest, ItemEntityRead)
{
	const Point testPos { 20, 20 };
	Tile &tile = tileAt(testPos);

	// Initially should be 0 (no item)
	EXPECT_EQ(tile.item(), 0);
	EXPECT_FALSE(tile.hasItem());

	// Set item
	tile.setItem(7);
	EXPECT_EQ(tile.item(), 7);
	EXPECT_TRUE(tile.hasItem());
}

TEST_F(Phase4BEntityTest, ItemEntityWrite)
{
	const Point testPos { 22, 22 };
	Tile &tile = tileAt(testPos);

	// Write positive
	tile.setItem(3);
	EXPECT_EQ(tile.item(), 3);

	// Write negative
	tile.setItem(-1);
	EXPECT_EQ(tile.item(), -1);

	// Write zero (clear)
	tile.setItem(0);
	EXPECT_EQ(tile.item(), 0);
	EXPECT_FALSE(tile.hasItem());
}

// ============================================================================
// Special Entity Tests
// ============================================================================

TEST_F(Phase4BEntityTest, SpecialEntityRead)
{
	const Point testPos { 15, 15 };
	Tile &tile = tileAt(testPos);

	// Initially should be 0 (no special)
	EXPECT_EQ(tile.special(), 0);
	EXPECT_FALSE(tile.hasSpecial());

	// Set special
	tile.setSpecial(9);
	EXPECT_EQ(tile.special(), 9);
	EXPECT_TRUE(tile.hasSpecial());
}

TEST_F(Phase4BEntityTest, SpecialEntityWrite)
{
	const Point testPos { 17, 17 };
	Tile &tile = tileAt(testPos);

	// Write positive
	tile.setSpecial(6);
	EXPECT_EQ(tile.special(), 6);

	// Write negative
	tile.setSpecial(-1);
	EXPECT_EQ(tile.special(), -1);

	// Write zero (clear)
	tile.setSpecial(0);
	EXPECT_EQ(tile.special(), 0);
	EXPECT_FALSE(tile.hasSpecial());
}

// ============================================================================
// Multi-Entity Tests
// ============================================================================

TEST_F(Phase4BEntityTest, MultipleEntitiesIndependence)
{
	const Point testPos { 100, 100 };
	Tile &tile = tileAt(testPos);

	// Set multiple entities at same tile
	tile.setPlayer(2);
	tile.setMonster(3);
	tile.setCorpse(4);
	tile.setObject(5);
	tile.setItem(6);
	tile.setSpecial(7);

	// Verify each is independent
	EXPECT_EQ(tile.player(), 2);
	EXPECT_EQ(tile.monster(), 3);
	EXPECT_EQ(tile.corpse(), 4);
	EXPECT_EQ(tile.object(), 5);
	EXPECT_EQ(tile.item(), 6);
	EXPECT_EQ(tile.special(), 7);
}

TEST_F(Phase4BEntityTest, EntityClear)
{
	const Point testPos { 105, 105 };
	Tile &tile = tileAt(testPos);

	// Set multiple entities
	tile.setPlayer(1);
	tile.setMonster(2);
	tile.setObject(3);
	tile.setItem(4);
	tile.setSpecial(5);
	tile.setCorpse(6);

	// Clear individual entities
	tile.setPlayer(0);
	EXPECT_FALSE(tile.hasPlayer());
	EXPECT_TRUE(tile.hasMonster()); // Others still set

	tile.setMonster(0);
	EXPECT_FALSE(tile.hasMonster());

	tile.clear(); // Clear all
	EXPECT_FALSE(tile.hasPlayer());
	EXPECT_FALSE(tile.hasMonster());
	EXPECT_FALSE(tile.hasObject());
	EXPECT_FALSE(tile.hasItem());
	EXPECT_FALSE(tile.hasSpecial());
	EXPECT_FALSE(tile.hasCorpse());
}

// ============================================================================
// Boundary Tests
// ============================================================================

TEST_F(Phase4BEntityTest, EntityBoundaryConditions)
{
	// Test corner tiles()
	const Point corners[] = {
		{ 0, 0 },
		{ MAXDUNX - 1, 0 },
		{ 0, MAXDUNY - 1 },
		{ MAXDUNX - 1, MAXDUNY - 1 }
	};

	for (const Point &pos : corners) {
		Tile &tile = tileAt(pos);
		tile.setPlayer(1);
		tile.setMonster(2);
		tile.setItem(3);

		EXPECT_EQ(tile.player(), 1);
		EXPECT_EQ(tile.monster(), 2);
		EXPECT_EQ(tile.item(), 3);
	}
}

// ============================================================================
// Stress Tests
// ============================================================================

TEST_F(Phase4BEntityTest, StressTestEntityBulkOperations)
{
	// Set entities on many tiles()
	for (int x = 10; x < 20; x++) {
		for (int y = 10; y < 20; y++) {
			Point pos { static_cast<WorldTileCoord>(x), static_cast<WorldTileCoord>(y) };
			Tile &tile = tileAt(pos);
			tile.setPlayer(x + y);
			tile.setMonster(x * y);
			tile.setItem(x - y);
		}
	}

	// Verify all set correctly
	for (int x = 10; x < 20; x++) {
		for (int y = 10; y < 20; y++) {
			Point pos { static_cast<WorldTileCoord>(x), static_cast<WorldTileCoord>(y) };
			const Tile &tile = tileAt(pos);
			EXPECT_EQ(tile.player(), x + y);
			EXPECT_EQ(tile.monster(), x * y);
			EXPECT_EQ(tile.item(), x - y);
		}
	}
}

TEST_F(Phase4BEntityTest, StressTestEntitySignedValues)
{
	// Test that signed values work correctly (positive and negative)
	const Point testPos { 110, 110 };
	Tile &tile = tileAt(testPos);

	// Test all entity types with various signed values
	const int8_t testValues[] = { -128, -100, -1, 0, 1, 100, 127 };
	const int16_t monsterTestValues[] = { -32768, -1000, -1, 0, 1, 1000, 32767 };

	for (int8_t val : testValues) {
		tile.setPlayer(val);
		EXPECT_EQ(tile.player(), val);

		tile.setCorpse(val);
		EXPECT_EQ(tile.corpse(), val);

		tile.setObject(val);
		EXPECT_EQ(tile.object(), val);

		tile.setItem(val);
		EXPECT_EQ(tile.item(), val);

		tile.setSpecial(val);
		EXPECT_EQ(tile.special(), val);
	}

	for (int16_t val : monsterTestValues) {
		tile.setMonster(val);
		EXPECT_EQ(tile.monster(), val);
	}
}

TEST_F(Phase4BEntityTest, StressTestTileOccupancy)
{
	// Test isOccupied() and isEmpty() with various entity combinations
	const Point testPos { 115, 115 };
	Tile &tile = tileAt(testPos);

	// Empty tile
	tile.clear();
	EXPECT_TRUE(tile.isEmpty());
	EXPECT_FALSE(tile.isOccupied());

	// With player
	tile.setPlayer(1);
	EXPECT_FALSE(tile.isEmpty());
	EXPECT_TRUE(tile.isOccupied());

	// With monster (but no player)
	tile.clear();
	tile.setMonster(1);
	EXPECT_FALSE(tile.isEmpty());
	EXPECT_TRUE(tile.isOccupied());

	// With only corpse (doesn't count as occupied)
	tile.clear();
	tile.setCorpse(1);
	EXPECT_FALSE(tile.isEmpty());
	EXPECT_FALSE(tile.isOccupied()); // Corpse doesn't block

	// With item only
	tile.clear();
	tile.setItem(1);
	EXPECT_FALSE(tile.isEmpty());
	EXPECT_TRUE(tile.isOccupied()); // Items occupy tiles()

	// With object only
	tile.clear();
	tile.setObject(1);
	EXPECT_FALSE(tile.isEmpty());
	EXPECT_TRUE(tile.isOccupied()); // Objects occupy tiles()
}

} // namespace devilution

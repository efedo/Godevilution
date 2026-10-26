#include <gtest/gtest.h>

#include "engine/lighting.h"
#include "engine/lighting_defs.hpp"
#include "engine/math/displacement.hpp"
#include "engine/math/point.hpp"
#include "engine/world_tile.hpp"
#include "game/levels/dungeon_common.h"
#include "game/levels/level.hpp"
#include "game/levels/tile.hpp"

namespace devilution {

class Phase4ALightingTest : public ::testing::Test {
protected:
	void SetUp() override
	{
		// Level is already initialized via CurrentWorld
	}

	void TearDown() override
	{
		// Level cleanup is handled by CurrentWorld
	}
};

// Test DoUnLight functionality (restores to preLight level)
TEST_F(Phase4ALightingTest, DoUnLightRestoresPreLight)
{
	const Point testPos { 50, 50 };

	// Set up: preLight and current light
	tileAt(testPos).setPreLight(10);
	tileAt(testPos).setLight(20);

	// Verify starting state
	EXPECT_EQ(tileAt(testPos).preLight(), 10);
	EXPECT_EQ(tileAt(testPos).light(), 20);

	// DoUnLight should restore light to preLight level
	CurrentLightManager.DoUnLight(testPos, 0);
	EXPECT_EQ(tileAt(testPos).light(), 10);
}

// Test DoLighting applies light cone
TEST_F(Phase4ALightingTest, DoLightingAppliesLight)
{
	const Point centerPos { 50, 50 };

	// Initialize the area around center to max darkness (255 = darkest in this lighting system)
	for (int x = 45; x < 55; x++) {
		for (int y = 45; y < 55; y++) {
			Point pos { static_cast<WorldTileCoord>(x), static_cast<WorldTileCoord>(y) };
			tileAt(pos).setPreLight(255);
			tileAt(pos).setLight(255);
		}
	}

	// Apply lighting
	CurrentLightManager.DoLighting(centerPos, 5, DisplacementOf<int8_t> { 0, 0 });

	// In this lighting system 0 = fully lit, 255 = darkest.
	// After DoLighting the center tile must have been brightened (value < 255).
	EXPECT_LT(tileAt(centerPos).light(), 255);
}

// Test that flags are preserved through lighting operations
TEST_F(Phase4ALightingTest, FlagsPreservedThroughLighting)
{
	const Point testPos { 50, 50 };
	Tile &tile = tileAt(testPos);

	// Set various flags
	tile.setPreLight(10);
	tile.setLight(0);
	tile.addFlags(DungeonFlag::Explored);

	// Apply DoUnLight
	CurrentLightManager.DoUnLight(testPos, 0);

	// Flags should be preserved
	EXPECT_TRUE(tile.hasAnyFlag(DungeonFlag::Explored));
	EXPECT_EQ(tile.preLight(), 10);
	EXPECT_EQ(tile.light(), 10);
}

// Test multiple tiles in lighting operations
TEST_F(Phase4ALightingTest, MultiTileLighting)
{
	const Point centerPos { 50, 50 };

	// Initialize region
	for (int x = 45; x < 55; x++) {
		for (int y = 45; y < 55; y++) {
			Point pos { static_cast<WorldTileCoord>(x), static_cast<WorldTileCoord>(y) };
			tileAt(pos).setPreLight(5);
			tileAt(pos).setLight(15);
		}
	}

	// Apply DoUnLight
	CurrentLightManager.DoUnLight(centerPos, 3);

	// All tiles in radius should be back to preLight
	for (int x = 45; x < 55; x++) {
		for (int y = 45; y < 55; y++) {
			Point pos { static_cast<WorldTileCoord>(x), static_cast<WorldTileCoord>(y) };
			// Tiles within radius should have light restored to preLight
			if (std::abs(pos.x - centerPos.x) <= 5 && std::abs(pos.y - centerPos.y) <= 5) {
				EXPECT_EQ(tileAt(pos).light(), tileAt(pos).preLight());
			}
		}
	}
}

// Test Tile flag operations
TEST_F(Phase4ALightingTest, TileFlagOperations)
{
	const Point testPos { 30, 30 };
	Tile &tile = tileAt(testPos);

	// Test adding single flag
	tile.addFlags(DungeonFlag::Lit);
	EXPECT_TRUE(tile.hasAnyFlag(DungeonFlag::Lit));
	EXPECT_TRUE(tile.isLit());

	// Test adding multiple flags
	tile.addFlags(DungeonFlag::Explored);
	EXPECT_TRUE(tile.hasAnyFlag(DungeonFlag::Lit));
	EXPECT_TRUE(tile.hasAnyFlag(DungeonFlag::Explored));
	EXPECT_TRUE(tile.isExplored());

	// Test removing flags
	tile.removeFlags(DungeonFlag::Lit);
	EXPECT_FALSE(tile.hasAnyFlag(DungeonFlag::Lit));
	EXPECT_FALSE(tile.isLit());
	EXPECT_TRUE(tile.isExplored()); // Other flag still set
}

TEST_F(Phase4ALightingTest, DungeonFlagHelpersUseTileFlags)
{
	const Point testPos { 31, 31 };
	tileAt(testPos).setFlags(DungeonFlag::None);
	Tile &tile = tileAt(testPos);
	tile.setFlags(DungeonFlag::Visible | DungeonFlag::Lit | DungeonFlag::Populated);

	EXPECT_TRUE(IsTileVisible(testPos));
	EXPECT_TRUE(IsTileLit(testPos));
	EXPECT_TRUE(TileContainsSetPiece(testPos));
}

// Test light boundary conditions (using Tile API)
TEST_F(Phase4ALightingTest, LightBoundaries)
{
	// Test corner tiles
	const Point topLeft { 0, 0 };
	const Point topRight { MAXDUNX - 1, 0 };
	const Point bottomLeft { 0, MAXDUNY - 1 };
	const Point bottomRight { MAXDUNX - 1, MAXDUNY - 1 };

	tileAt(topLeft).setLight(1);
	tileAt(topRight).setLight(2);
	tileAt(bottomLeft).setLight(3);
	tileAt(bottomRight).setLight(4);

	EXPECT_EQ(tileAt(topLeft).light(), 1);
	EXPECT_EQ(tileAt(topRight).light(), 2);
	EXPECT_EQ(tileAt(bottomLeft).light(), 3);
	EXPECT_EQ(tileAt(bottomRight).light(), 4);
}

// Test light independence from other tile data
TEST_F(Phase4ALightingTest, LightIndependence)
{
	const Point testPos { 50, 50 };
	Tile &tile = tileAt(testPos);

	// Set various tile properties
	tile.setPiece(123);
	tile.setMonster(5);
	tile.setItem(10);
	tile.addFlags(DungeonFlag::Visible);

	// Set light
	tile.setLight(7);

	// Verify light is unaffected by other properties
	EXPECT_EQ(tile.light(), 7);
	EXPECT_EQ(tile.piece(), 123);
	EXPECT_EQ(tile.monster(), 5);
	EXPECT_EQ(tile.item(), 10);
	EXPECT_TRUE(tile.isVisible());

	// Change other properties
	tile.setPiece(456);
	tile.setMonster(-3);

	// Light should still be 7
	EXPECT_EQ(tile.light(), 7);
}

// Test that tiles are properly initialized
TEST_F(Phase4ALightingTest, TileInitialization)
{
	// Create a new test position
	const Point testPos { 100, 100 };
	Tile &tile = tileAt(testPos);

	// Should be initialized to 0 (from default constructor)
	EXPECT_EQ(tile.light(), 0);
	EXPECT_EQ(tile.preLight(), 0);

	// Set values
	tile.setLight(42);
	tile.setPreLight(21);

	EXPECT_EQ(tile.light(), 42);
	EXPECT_EQ(tile.preLight(), 21);
}

// Stress test: set and get light for many tiles using Tile API
TEST_F(Phase4ALightingTest, StressTestManyTiles)
{
	// Set lights in a pattern
	for (int x = 0; x < MAXDUNX; x += 10) {
		for (int y = 0; y < MAXDUNY; y += 10) {
			const Point pos { static_cast<WorldTileCoord>(x), static_cast<WorldTileCoord>(y) };
			const uint8_t lightValue = static_cast<uint8_t>((x + y) % 256);
			tileAt(pos).setLight(lightValue);
		}
	}

	// Verify the pattern
	for (int x = 0; x < MAXDUNX; x += 10) {
		for (int y = 0; y < MAXDUNY; y += 10) {
			const Point pos { static_cast<WorldTileCoord>(x), static_cast<WorldTileCoord>(y) };
			const uint8_t expected = static_cast<uint8_t>((x + y) % 256);
			EXPECT_EQ(tileAt(pos).light(), expected);
		}
	}
}

} // namespace devilution

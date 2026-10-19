/**
 * @file levels/tile_test.cpp
 *
 * Quick validation of Tile class size and functionality.
 */

#include "game/levels/tile.hpp"

#include <cstdio>

using namespace devilution;

int main() {
	// Size check
	printf("sizeof(Tile) = %zu bytes\n", sizeof(Tile));
	printf("Expected to be <= 16 bytes for cache efficiency\n\n");

	// Field sizes
	printf("Field sizes:\n");
	printf("  piece (uint16_t): %zu\n", sizeof(uint16_t));
	printf("  transVal (int8_t): %zu\n", sizeof(int8_t));
	printf("  light (uint8_t): %zu\n", sizeof(uint8_t));
	printf("  preLight (uint8_t): %zu\n", sizeof(uint8_t));
	printf("  flags (DungeonFlag/uint8_t): %zu\n", sizeof(DungeonFlag));
	printf("  player (int8_t): %zu\n", sizeof(int8_t));
	printf("  monster (int16_t): %zu\n", sizeof(int16_t));
	printf("  corpse (int8_t): %zu\n", sizeof(int8_t));
	printf("  object (int8_t): %zu\n", sizeof(int8_t));
	printf("  special (int8_t): %zu\n", sizeof(int8_t));
	printf("  item (int8_t): %zu\n", sizeof(int8_t));
	printf("\n");

	// Basic functionality test
	Tile tile;

	// Test default state
	if (!tile.isEmpty()) {
		printf("ERROR: Default tile should be empty\n");
		return 1;
	}

	// Test setting/getting values
	tile.setPiece(42);
	tile.setLight(128);
	tile.setPlayer(3);
	tile.addFlags(DungeonFlag::Visible | DungeonFlag::Explored);

	if (tile.piece() != 42) {
		printf("ERROR: piece() mismatch\n");
		return 1;
	}
	if (tile.light() != 128) {
		printf("ERROR: light() mismatch\n");
		return 1;
	}
	if (tile.player() != 3) {
		printf("ERROR: player() mismatch\n");
		return 1;
	}
	if (!tile.isVisible()) {
		printf("ERROR: should be visible\n");
		return 1;
	}
	if (!tile.isExplored()) {
		printf("ERROR: should be explored\n");
		return 1;
	}

	// Test corpse encoding
	tile.setCorpse((2 << 5) | 5); // direction=2, index=5
	if (tile.corpseDirection() != 2) {
		printf("ERROR: corpseDirection() mismatch\n");
		return 1;
	}
	if (tile.corpseIndex() != 5) {
		printf("ERROR: corpseIndex() mismatch\n");
		return 1;
	}

	// Test clear
	tile.clear();
	if (!tile.isEmpty()) {
		printf("ERROR: Tile should be empty after clear()\n");
		return 1;
	}

	printf("All basic tests passed!\n");
	return 0;
}

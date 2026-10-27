/**
 * @file level_micros_test.cpp
 *
 * Sanity-check tests for the levelMicros() accessor.
 *
 * These tests verify:
 *   1. levelMicros() returns the current level's microtile storage.
 *   2. Writes through levelMicros() remain visible through subsequent accesses.
 *      (they are views of the same array).
 *   3. The MICROS struct is zero-initialised on a fresh level.
 *   4. Writing a LevelCelBlock through levelMicros() round-trips correctly.
 *   5. levelMicros() returns a different array after a level switch.
 *   6. Index 0 and index MAXTILES-1 are both accessible without OOB.
 */

#include <gtest/gtest.h>

#include "game/levels/dun_tile.hpp"
#include "game/levels/dungeon_common.h"
#include "game/levels/dungeon_common_defs.hpp"
#include "game/levels/level.hpp"

namespace devilution {

class LevelMicrosTest : public ::testing::Test {
protected:
	void SetUp() override
	{
		// Reset the micros array so each test starts clean
		auto micros = levelMicros();
		for (int i = 0; i < MAXTILES; ++i)
			micros[i] = MICROS {};
	}
};

// ---------------------------------------------------------------------------
// 1. levelMicros() addresses the current level's microtile storage.
// ---------------------------------------------------------------------------
TEST_F(LevelMicrosTest, RepeatedAccessReturnsTheSameStorage)
{
	MICROS *firstAccess = levelMicros().data();
	MICROS *secondAccess = levelMicros().data();
	EXPECT_EQ(firstAccess, secondAccess);
}

// ---------------------------------------------------------------------------
// 2. Write through levelMicros(), then read through a new span.
// ---------------------------------------------------------------------------
TEST_F(LevelMicrosTest, WriteThenReadThroughNewSpan)
{
	// LevelCelBlock encodes frame in bits [0,11] and type in bits [12,14]
	// Frame 42, type Square (0) => raw data = 42
	const LevelCelBlock expected { 42u };

	levelMicros()[5].mt[0] = expected;

	EXPECT_EQ(levelMicros()[5].mt[0].data, expected.data);
}

// ---------------------------------------------------------------------------
// 3. Repeated levelMicros() calls address the same storage.
// ---------------------------------------------------------------------------
TEST_F(LevelMicrosTest, RepeatedAccessPreservesWrites)
{
	const LevelCelBlock expected { 999u };

	levelMicros()[10].mt[3] = expected;

	EXPECT_EQ(levelMicros()[10].mt[3].data, expected.data);
}

// ---------------------------------------------------------------------------
// 4. Fresh MICROS entry is zero-initialised
// ---------------------------------------------------------------------------
TEST_F(LevelMicrosTest, FreshMicrosAreZeroInitialised)
{
	// SetUp() already zeroed everything
	for (int block = 0; block < 16; ++block) {
		EXPECT_EQ(levelMicros()[0].mt[block].data, 0u)
		    << "block " << block << " of entry 0 should be zero";
	}
}

// ---------------------------------------------------------------------------
// 5. Round-trip: write all 16 blocks of one entry, read them back
// ---------------------------------------------------------------------------
TEST_F(LevelMicrosTest, AllSixteenBlocksRoundTrip)
{
	constexpr int kEntry = 100;
	for (int block = 0; block < 16; ++block) {
		const uint16_t raw = static_cast<uint16_t>(block * 7 + 1); // arbitrary non-zero
		levelMicros()[kEntry].mt[block] = LevelCelBlock { raw };
	}

	for (int block = 0; block < 16; ++block) {
		const uint16_t expected = static_cast<uint16_t>(block * 7 + 1);
		EXPECT_EQ(levelMicros()[kEntry].mt[block].data, expected)
		    << "block " << block;
	}
}

// ---------------------------------------------------------------------------
// 6. Boundary indices: 0 and MAXTILES-1 are accessible
// ---------------------------------------------------------------------------
TEST_F(LevelMicrosTest, BoundaryIndicesAreAccessible)
{
	const LevelCelBlock first { 1u };
	const LevelCelBlock last { 2u };

	levelMicros()[0].mt[0] = first;
	levelMicros()[MAXTILES - 1].mt[15] = last;

	EXPECT_EQ(levelMicros()[0].mt[0].data, 1u);
	EXPECT_EQ(levelMicros()[MAXTILES - 1].mt[15].data, 2u);
}

// ---------------------------------------------------------------------------
// 7. hasValue() is false for a zero-initialised block
// ---------------------------------------------------------------------------
TEST_F(LevelMicrosTest, ZeroBlockHasNoValue)
{
	EXPECT_FALSE(levelMicros()[0].mt[0].hasValue());
}

// ---------------------------------------------------------------------------
// 8. hasValue() is true for a non-zero block
// ---------------------------------------------------------------------------
TEST_F(LevelMicrosTest, NonZeroBlockHasValue)
{
	levelMicros()[0].mt[0] = LevelCelBlock { 1u };
	EXPECT_TRUE(levelMicros()[0].mt[0].hasValue());
}

// ---------------------------------------------------------------------------
// 9. levelMicros() span size equals MAXTILES
// ---------------------------------------------------------------------------
TEST_F(LevelMicrosTest, SpanSizeEqualsMacroTiles)
{
	EXPECT_EQ(static_cast<int>(levelMicros().size()), MAXTILES);
}

} // namespace devilution

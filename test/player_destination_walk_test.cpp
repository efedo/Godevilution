#include <algorithm>
#include <iterator>
#include <vector>

#include <gtest/gtest.h>

#include "application/headless_mode.hpp"
#include "engine/lighting.h"
#include "engine/random.hpp"
#include "game/levels/dungeon_common.h"
#include "game/players/players.hpp"
#include "network/protocol/multi.h"
#include "network/storm/storm_net.hpp"

namespace devilution {
void TestPlayerProcessDestinationWalkTick(Player &player);
}

using namespace devilution;

namespace {
class NativeDestinationWalk : public testing::Test {
protected:
	void SetUp() override
	{
		oldType_ = levelType();
		oldRun_ = sgGameInitInfo.bRunInTown;
		oldHeadless_ = HeadlessMode;
		oldMyPlayer_ = MyPlayer;
		oldMyPlayerId_ = MyPlayerId;
		oldView_ = viewPosition();
		oldRandom_ = GetLCGEngineState();
		Players.swap(oldPlayers_);
		levelType() = DTYPE_TOWN;
		sgGameInitInfo.bRunInTown = 0;
		HeadlessMode = true;
		const auto &properties = tileProperties();
		size_t piece = 0;
		while (piece < std::size(properties) && HasAnyOf(properties[piece], TileProperties::Solid))
			++piece;
		ASSERT_LT(piece, std::size(properties));
		for (int y = 0; y < MAXDUNY; ++y) {
			for (int x = 0; x < MAXDUNX; ++x) {
				oldTiles_.push_back(tileAt({ x, y }));
				tileAt({ x, y }) = {};
				tileAt({ x, y }).setPiece(static_cast<uint16_t>(piece));
			}
		}
		Players.resize(1);
		MyPlayer = &Players[0];
		MyPlayerId = 0;
		auto &player = *MyPlayer;
		player._pClass = HeroClass::Rogue;
		player.life = { 6400, 6400, 6400, 6400, 0 };
		player.lightId = NO_LIGHT;
		player.position.tile = { 75, 68 };
		player.position.future = player.position.tile;
		player.position.old = player.position.tile;
		player.position.temp = player.position.tile;
		player._pmode = PM_STAND;
		player._pNFrames = 8;
		player._pWFrames = 8;
		player.animInfo.setNewAnimation(std::nullopt, 8, 1);
		player.clearPath();
		player.occupyTile(player.position.tile, false);
	}

	void TearDown() override
	{
		size_t index = 0;
		for (int y = 0; y < MAXDUNY; ++y) {
			for (int x = 0; x < MAXDUNX; ++x)
				tileAt({ x, y }) = oldTiles_.at(index++);
		}
		Players.swap(oldPlayers_);
		MyPlayer = oldMyPlayer_;
		MyPlayerId = oldMyPlayerId_;
		viewPosition() = oldView_;
		levelType() = oldType_;
		sgGameInitInfo.bRunInTown = oldRun_;
		HeadlessMode = oldHeadless_;
		SetRndSeed(oldRandom_);
	}

private:
	std::vector<Tile> oldTiles_;
	std::vector<Player> oldPlayers_;
	Player *oldMyPlayer_ = nullptr;
	uint8_t oldMyPlayerId_ = 0;
	Point oldView_;
	dungeon_type oldType_;
	uint8_t oldRun_ = 0;
	bool oldHeadless_ = false;
	uint32_t oldRandom_ = 0;
};

TEST_F(NativeDestinationWalk, ThreeStepsRetainInitialAndCompletionLoopFrameTiming)
{
	auto &player = *MyPlayer;
	player.walkpath[0] = WALK_NE;
	player.walkpath[1] = WALK_NE;
	player.walkpath[2] = WALK_NE;
	const auto random = GetLCGEngineState();
	for (int tick = 0; tick <= 25; ++tick) {
		TestPlayerProcessDestinationWalkTick(player);
		SCOPED_TRACE(tick);
		const int completed = tick < 9 ? 0 : tick < 17 ? 1 : tick < 25 ? 2 : 3;
		EXPECT_EQ(player.position.tile, (Point { 75, 68 - completed }));
		const int reserved = std::min(completed + 1, 3);
		EXPECT_EQ(player.position.future, (Point { 75, 68 - reserved }));
		EXPECT_EQ(GetLCGEngineState(), random);
		if (tick == 0)
			EXPECT_EQ(player.animInfo.currentFrame, -1);
		if (tick == 9 || tick == 17)
			EXPECT_EQ(player.animInfo.currentFrame, 0);
	}
	EXPECT_EQ(player._pmode, PM_STAND);
	EXPECT_EQ(player.walkpath[0], WALK_NONE);
}
TEST_F(NativeDestinationWalk, StandConsumesOnlyOneBlockedDirectionPerTick)
{
	auto &player = *MyPlayer;
	player.walkpath[0] = WALK_NE;
	player.walkpath[1] = WALK_NW;
	player.walkpath[2] = WALK_SE;
	tileAt({ 75, 67 }).setMonster(1);
	tileAt({ 74, 68 }).setMonster(1);
	TestPlayerProcessDestinationWalkTick(player);
	EXPECT_EQ(player._pmode, PM_STAND);
	EXPECT_EQ(player.walkpath[0], WALK_NW);
	TestPlayerProcessDestinationWalkTick(player);
	EXPECT_EQ(player._pmode, PM_STAND);
	EXPECT_EQ(player.walkpath[0], WALK_SE);
	TestPlayerProcessDestinationWalkTick(player);
	EXPECT_EQ(player.position.tile, (Point { 75, 68 }));
	EXPECT_EQ(player.position.future, (Point { 76, 68 }));
}

TEST_F(NativeDestinationWalk, CompletionConsumesAtMostTwoBlockedDirectionsBeforeNextTick)
{
	auto &player = *MyPlayer;
	player.walkpath[0] = WALK_NE;
	player.walkpath[1] = WALK_NE;
	player.walkpath[2] = WALK_NW;
	player.walkpath[3] = WALK_SE;
	for (int tick = 0; tick < 9; ++tick)
		TestPlayerProcessDestinationWalkTick(player);
	tileAt({ 75, 66 }).setMonster(1);
	tileAt({ 74, 67 }).setMonster(1);
	TestPlayerProcessDestinationWalkTick(player);
	EXPECT_EQ(player._pmode, PM_STAND);
	EXPECT_EQ(player.position.tile, (Point { 75, 67 }));
	EXPECT_EQ(player.position.future, player.position.tile);
	EXPECT_EQ(player.walkpath[0], WALK_SE);
	TestPlayerProcessDestinationWalkTick(player);
	EXPECT_EQ(player.position.tile, (Point { 75, 67 }));
	EXPECT_EQ(player.position.future, (Point { 76, 67 }));
}
TEST_F(NativeDestinationWalk, RetryAfterBlockedCompletionStartsWithStandFrameSkip)
{
	auto &player = *MyPlayer;
	player.walkpath[0] = WALK_NE;
	player.walkpath[1] = WALK_NE;
	player.walkpath[2] = WALK_SE;
	for (int tick = 0; tick < 9; ++tick)
		TestPlayerProcessDestinationWalkTick(player);
	tileAt({ 75, 66 }).setMonster(1);
	TestPlayerProcessDestinationWalkTick(player);
	EXPECT_EQ(player.position.tile, (Point { 75, 67 }));
	EXPECT_EQ(player.position.future, (Point { 76, 67 }));
	EXPECT_EQ(player.animInfo.currentFrame, -1);
	for (int tick = 10; tick < 18; ++tick) {
		TestPlayerProcessDestinationWalkTick(player);
		EXPECT_EQ(player.position.tile, (Point { 75, 67 }));
	}
	TestPlayerProcessDestinationWalkTick(player);
	EXPECT_EQ(player.position.tile, (Point { 76, 67 }));
}
} // namespace

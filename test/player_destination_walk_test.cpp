#include <algorithm>
#include <array>
#include <filesystem>
#include <iterator>
#include <list>
#include <string>
#include <vector>

#include <gtest/gtest.h>

#include "application/headless_mode.hpp"
#include "engine/lighting.h"
#include "engine/random.hpp"
#include "game/levels/dungeon_common.h"
#include "game/missiles/missiles.hpp"
#include "game/players/players.hpp"
#include "network/protocol/multi.h"
#include "network/storm/storm_net.hpp"
#include "tables/playerdat.hpp"
#include "utils/paths.h"

namespace devilution {
void TestPlayerCheckNewPath(Player &player);
void TestPlayerProcessDestinationWalkTick(Player &player);
bool TestPlayerProcessAttackCommand(Player &player, bool ranged);
} // namespace devilution

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

class NativeDestinationRangedWalk : public NativeDestinationWalk {
protected:
	void SetUp() override
	{
		NativeDestinationWalk::SetUp();
		previousAssets_ = paths::AssetsPath();
		previousHellfire_ = gbIsHellfire;
		previousMultiplayer_ = gbIsMultiplayer;
		previousMissiles_.swap(Missiles);
		const auto source = std::filesystem::path(GODEVILUTION_DESTINATION_WALK_SOURCE_ROOT);
		const auto configuredAssets = std::filesystem::path(previousAssets_);
		const auto assets = std::filesystem::is_regular_file(configuredAssets / "txtdata/classes/classdat.tsv")
		        && std::filesystem::is_regular_file(configuredAssets / "txtdata/missiles/misdat.tsv")
		        && std::filesystem::is_regular_file(configuredAssets / "txtdata/missiles/missile_sprites.tsv")
		    ? configuredAssets
		    : source / "assets";
		ASSERT_TRUE(std::filesystem::is_regular_file(assets / "txtdata/classes/classdat.tsv"));
		ASSERT_TRUE(std::filesystem::is_regular_file(assets / "txtdata/missiles/misdat.tsv"));
		ASSERT_TRUE(std::filesystem::is_regular_file(assets / "txtdata/missiles/missile_sprites.tsv"));
		paths::SetAssetsPath(assets.string() + "/");
		LoadPlayerDataFiles();
		LoadMissileData();
		levelType() = DTYPE_CATHEDRAL;
		gbIsHellfire = false;
		gbIsMultiplayer = false;
		networkInitialized_ = SNetInitializeProvider(SELCONN_LOOPBACK, nullptr);
		ASSERT_TRUE(networkInitialized_);
	}

	void TearDown() override
	{
		if (networkInitialized_)
			SNetDestroy();
		Missiles.swap(previousMissiles_);
		gbIsMultiplayer = previousMultiplayer_;
		gbIsHellfire = previousHellfire_;
		paths::SetAssetsPath(previousAssets_);
		NativeDestinationWalk::TearDown();
	}

private:
	std::string previousAssets_;
	bool previousHellfire_ = false;
	bool previousMultiplayer_ = false;
	std::list<Missile> previousMissiles_;
	bool networkInitialized_ = false;
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
		const int completed = tick < 9 ? 0 : tick < 17 ? 1
		    : tick < 25                                ? 2
		                                               : 3;
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

TEST_F(NativeDestinationWalk, RangedDestinationCancelsQueuedStepsAfterCurrentWalkCompletes)
{
	auto &player = *MyPlayer;
	player.walkpath[0] = WALK_NE;
	player.walkpath[1] = WALK_E;
	player.walkpath[2] = WALK_SE;

	// Begin the first native walk step; later directions remain queued.
	TestPlayerProcessDestinationWalkTick(player);
	ASSERT_NE(player._pmode, PM_STAND);
	const auto movingMode = player._pmode;
	const Point start = player.position.tile;
	const Point currentStep = player.position.future;
	ASSERT_NE(currentStep, start);
	ASSERT_EQ(player.walkpath[0], WALK_E);

	// OnRangedAttackTile clears future path directions and stores the destination
	// action without canceling the already-started walk animation.
	player.clearPath();
	player.destAction = ACTION_RATTACK;
	player.destParam1 = 80;
	player.destParam2 = 68;
	EXPECT_EQ(player._pmode, movingMode);
	EXPECT_EQ(player.position.tile, start);
	EXPECT_EQ(player.position.future, currentStep);
	EXPECT_EQ(player.walkpath[0], WALK_NONE);
	EXPECT_EQ(player.destAction, ACTION_RATTACK);

	// The current step completes normally. The ranged action starts at that
	// completion's Stand scheduling boundary, before any later path step.
	for (int tick = 1; tick < 9; ++tick) {
		TestPlayerProcessDestinationWalkTick(player);
		EXPECT_EQ(player._pmode, movingMode);
		EXPECT_EQ(player.position.tile, start);
		EXPECT_EQ(player.position.future, currentStep);
		EXPECT_EQ(player.destAction, ACTION_RATTACK);
		EXPECT_EQ(player.walkpath[0], WALK_NONE);
	}
	TestPlayerProcessDestinationWalkTick(player);
	EXPECT_EQ(player.position.tile, currentStep);
	EXPECT_EQ(player.position.future, currentStep);
	EXPECT_EQ(player._pmode, PM_RATTACK);
	EXPECT_EQ(player.destAction, ACTION_NONE);
	EXPECT_EQ(player.walkpath[0], WALK_NONE);
	EXPECT_EQ(player.position.temp, (WorldTilePosition { 80, 68 }));
}

struct NativeRangedIasCase {
	ItemSpecialEffect flags;
	int completionSkippedFrames;
};

class NativeDestinationRangedAttack : public NativeDestinationRangedWalk,
                                      public testing::WithParamInterface<NativeRangedIasCase> {
};

TEST_P(NativeDestinationRangedAttack, RogueBowUsesNativeCompletionFramesAndEffectBoundary)
{
	auto &player = *MyPlayer;
	player._pgfxnum = static_cast<uint8_t>(PlayerWeaponGraphic::Bow);
	player._pIFlags = GetParam().flags;
	player.setAnimations();
	// Keep this fixture's existing eight-tick walk; the attack values come from the
	// loaded Rogue bow row rather than hard-coded animation metadata.
	player._pWFrames = 8;
	const auto &rogue = GetPlayerAnimDataForClass(HeroClass::Rogue);
	ASSERT_EQ(player._pAFrames, rogue.bowFrames);
	ASSERT_EQ(player._pAFNum, rogue.bowActionFrame);
	ASSERT_GT(player._pAFNum, 1);
	ASSERT_LT(player._pAFNum, player._pAFrames);

	player.walkpath[0] = WALK_NE;
	player.walkpath[1] = WALK_E;
	player.walkpath[2] = WALK_SE;
	TestPlayerProcessDestinationWalkTick(player);
	ASSERT_NE(player._pmode, PM_STAND);
	const auto movingMode = player._pmode;
	const Point start = player.position.tile;
	const Point currentStep = player.position.future;

	player.clearPath();
	player.destAction = ACTION_RATTACK;
	player.destParam1 = 80;
	player.destParam2 = 68;
	for (int tick = 1; tick < 9; ++tick)
		TestPlayerProcessDestinationWalkTick(player);
	ASSERT_EQ(player.position.tile, start);
	ASSERT_EQ(player.position.future, currentStep);
	ASSERT_EQ(player._pmode, movingMode);
	ASSERT_EQ(player.destAction, ACTION_RATTACK);

	TestPlayerProcessDestinationWalkTick(player);
	ASSERT_EQ(player.position.tile, currentStep);
	ASSERT_EQ(player.position.future, currentStep);
	ASSERT_EQ(player._pmode, PM_RATTACK);
	ASSERT_EQ(player.destAction, ACTION_NONE);
	ASSERT_EQ(player.walkpath[0], WALK_NONE);
	const auto attackStart = player.animInfo.captureTimingState();
	EXPECT_EQ(attackStart.currentFrame, GetParam().completionSkippedFrames + 1);
	EXPECT_EQ(attackStart.numberOfFrames, rogue.bowFrames);
	EXPECT_EQ(attackStart.ticksPerFrame, 1);
	EXPECT_EQ(attackStart.ticksSinceSequenceStarted, 0);
	EXPECT_EQ(attackStart.skippedFramesFromPreviousAnimation, 0);

	bool effectObserved = false;
	bool readyObserved = false;
	bool attackCompleted = false;
	int lastAttackFrame = -1;
	for (int tick = 0; tick < rogue.bowFrames + 2 && !attackCompleted; ++tick) {
		lastAttackFrame = player.animInfo.currentFrame;
		const size_t missilesBefore = Missiles.size();
		attackCompleted = TestPlayerProcessAttackCommand(player, true);
		if (lastAttackFrame == rogue.bowActionFrame - 1) {
			EXPECT_EQ(Missiles.size(), missilesBefore + 1);
			EXPECT_FALSE(attackCompleted);
			effectObserved = true;
		} else {
			EXPECT_EQ(Missiles.size(), missilesBefore);
		}
		if (lastAttackFrame == rogue.bowActionFrame) {
			EXPECT_EQ(player._pmode, PM_RATTACK);
			readyObserved = true;
		}
		if (!attackCompleted)
			player.animInfo.processAnimation();
	}
	EXPECT_TRUE(effectObserved);
	EXPECT_TRUE(readyObserved);
	EXPECT_TRUE(attackCompleted);
	EXPECT_EQ(lastAttackFrame, rogue.bowFrames - 1);
	EXPECT_EQ(player._pmode, PM_STAND);
	EXPECT_EQ(player.destAction, ACTION_NONE);
	EXPECT_EQ(Missiles.size(), 1U);
}

TEST_F(NativeDestinationRangedWalk, BufferedRangedFollowupStartsAtReadyAndUsesNewAim)
{
	struct IasCase {
		ItemSpecialEffect flags;
		int readyRepeatSkip;
	};
	constexpr std::array<IasCase, 2> cases { {
		{ ItemSpecialEffect::None, 0 },
		{ ItemSpecialEffect::FastAttack, 1 },
	} };

	for (const IasCase &test : cases) {
		SCOPED_TRACE(static_cast<uint32_t>(test.flags));
		Missiles.clear();
		Player &player = *MyPlayer;
		player = {};
		player._pClass = HeroClass::Rogue;
		player._pgfxnum = static_cast<uint8_t>(PlayerWeaponGraphic::Bow);
		player._pIFlags = test.flags;
		player.setAnimations();
		player._pWFrames = 8;
		player.life = { 6400, 6400, 6400, 6400, 0 };
		player.lightId = NO_LIGHT;
		player.position.tile = { 75, 68 };
		player.position.future = player.position.tile;
		player.position.old = player.position.tile;
		player.position.temp = player.position.tile;
		player.direction = Direction::South;
		player._pmode = PM_STAND;
		player.clearPath();

		const auto &rogue = GetPlayerAnimDataForClass(HeroClass::Rogue);
		const WorldTilePosition firstAim { 80, 68 };
		const WorldTilePosition queuedAim { 75, 60 };
		ASSERT_EQ(player._pAFrames, rogue.bowFrames);
		ASSERT_EQ(player._pAFNum, rogue.bowActionFrame);

		player.destAction = ACTION_RATTACK;
		player.destParam1 = firstAim.x;
		player.destParam2 = firstAim.y;
		TestPlayerCheckNewPath(player);
		ASSERT_EQ(player._pmode, PM_RATTACK);
		ASSERT_EQ(player.position.temp, firstAim);
		ASSERT_EQ(player.destAction, ACTION_NONE);

		// Native ranged input stores the next target before the current bow
		// animation reaches its ready frame. The active shot keeps its first aim.
		player.destAction = ACTION_RATTACK;
		player.destParam1 = queuedAim.x;
		player.destParam2 = queuedAim.y;
		while (player.animInfo.currentFrame < player._pAFNum - 1) {
			ASSERT_FALSE(TestPlayerProcessAttackCommand(player, true));
			ASSERT_TRUE(Missiles.empty());
			ASSERT_EQ(player.destAction, ACTION_RATTACK);
			player.animInfo.processAnimation();
		}
		ASSERT_EQ(player.animInfo.currentFrame, player._pAFNum - 1);
		ASSERT_FALSE(TestPlayerProcessAttackCommand(player, true));
		ASSERT_EQ(Missiles.size(), 1U);
		const int firstArrowDirection = Missiles.front()._miAnimFrame;
		EXPECT_EQ(player.destAction, ACTION_RATTACK);
		EXPECT_EQ(player.position.temp, firstAim);

		player.animInfo.processAnimation();
		ASSERT_EQ(player.animInfo.currentFrame, player._pAFNum);
		ASSERT_FALSE(TestPlayerProcessAttackCommand(player, true));
		EXPECT_EQ(Missiles.size(), 1U);
		EXPECT_EQ(player._pmode, PM_RATTACK);
		EXPECT_EQ(player.destAction, ACTION_NONE);
		EXPECT_EQ(player.position.temp, queuedAim);
		EXPECT_EQ(player.animInfo.currentFrame, test.readyRepeatSkip);

		for (int guard = 0; Missiles.size() == 1 && guard < rogue.bowFrames + 2; ++guard) {
			if (player.animInfo.currentFrame == player._pAFNum - 1) {
				ASSERT_FALSE(TestPlayerProcessAttackCommand(player, true));
				ASSERT_EQ(Missiles.size(), 2U);
				break;
			}
			ASSERT_FALSE(TestPlayerProcessAttackCommand(player, true));
			ASSERT_EQ(Missiles.size(), 1U);
			player.animInfo.processAnimation();
		}
		ASSERT_EQ(Missiles.size(), 2U);
		EXPECT_NE(Missiles.back()._miAnimFrame, firstArrowDirection);
	}
}

INSTANTIATE_TEST_SUITE_P(
    NativeRogueBow,
    NativeDestinationRangedAttack,
    testing::Values(
        NativeRangedIasCase { ItemSpecialEffect::None, 0 },
        NativeRangedIasCase { ItemSpecialEffect::QuickAttack, 1 },
        NativeRangedIasCase { ItemSpecialEffect::FastAttack, 2 },
        NativeRangedIasCase { ItemSpecialEffect::QuickAttack | ItemSpecialEffect::FastAttack, 2 }));
} // namespace

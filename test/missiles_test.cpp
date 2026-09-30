#include <gmock/gmock.h>
#include <gtest/gtest.h>

#include <ankerl/unordered_dense.h>

#include <utility>
#include <vector>

#include "engine/lighting.h"
#include "engine/random.hpp"
#include "game/levels/dungeon_common.h"
#include "game/levels/triggers.h"
#include "game/levels/tile.hpp"
#include "game/missiles/missiles.hpp"
#include "network/storm/storm_net.hpp"

using namespace devilution;
using ::testing::AllOf;
using ::testing::Gt;
using ::testing::Lt;
using ::testing::Pair;
using ::testing::UnorderedElementsAre;

namespace {

class ScopedTownPortalState {
public:
	explicit ScopedTownPortalState(Point portalPosition)
	    : portalPosition_(portalPosition)
	    , savedPlayers_(std::move(Players))
	    , savedMyPlayer_(MyPlayer)
	    , savedMyPlayerId_(MyPlayerId)
	    , savedLevelType_(levelType())
	    , savedPortalTile_(tileAt(portalPosition))
	    , savedLightManager_(CurrentLightManager)
	    , savedFullyLitLightTable_(CurrentLightManager.fullyLitLightTable_)
	    , savedFullyDarkLightTable_(CurrentLightManager.fullyDarkLightTable_)
	    , savedTrigFlag_(trigFlag())
	    , savedNumTriggers_(numTriggers())
	{
		Players.clear();
		MyPlayer = nullptr;
		MyPlayerId = 0;
		levelType() = DTYPE_TOWN;
		InitNoTriggers();
	}

	~ScopedTownPortalState()
	{
		tileAt(portalPosition_) = savedPortalTile_;
		levelType() = savedLevelType_;
		CurrentLightManager = savedLightManager_;
		CurrentLightManager.fullyLitLightTable_ = savedFullyLitLightTable_;
		CurrentLightManager.fullyDarkLightTable_ = savedFullyDarkLightTable_;
		Players = std::move(savedPlayers_);
		MyPlayerId = savedMyPlayerId_;
		MyPlayer = savedMyPlayer_;
		InitNoTriggers();
		trigFlag() = savedTrigFlag_;
		numTriggers() = savedNumTriggers_;
	}

	ScopedTownPortalState(const ScopedTownPortalState &) = delete;
	ScopedTownPortalState &operator=(const ScopedTownPortalState &) = delete;

private:
	Point portalPosition_;
	std::vector<Player> savedPlayers_;
	Player *savedMyPlayer_;
	uint8_t savedMyPlayerId_;
	dungeon_type savedLevelType_;
	Tile savedPortalTile_;
	LightManager savedLightManager_;
	uint8_t *savedFullyLitLightTable_;
	uint8_t *savedFullyDarkLightTable_;
	bool savedTrigFlag_;
	int savedNumTriggers_;
};

void AssertNoActiveTriggers()
{
	ASSERT_EQ(numTriggers(), 0);
	ASSERT_FALSE(trigFlag());
}

class ScopedLoopbackNetwork {
public:
	ScopedLoopbackNetwork()
	    : initialized_(SNetInitializeProvider(SELCONN_LOOPBACK, nullptr))
	{
	}

	~ScopedLoopbackNetwork()
	{
		if (initialized_)
			SNetDestroy();
	}

	ScopedLoopbackNetwork(const ScopedLoopbackNetwork &) = delete;
	ScopedLoopbackNetwork &operator=(const ScopedLoopbackNetwork &) = delete;

	bool initialized() const { return initialized_; }

private:
	bool initialized_;
};

} // namespace

void TestArrowRotatesUniformly(Missile &missile, int startingFrame, int leftFrame, int rightFrame)
{
	ankerl::unordered_dense::map<int, unsigned> observed {};
	for (auto i = 0; i < 100; i++) {
		missile._miAnimFrame = startingFrame;
		TestRotateBlockedMissile(missile);
		observed[missile._miAnimFrame]++;
	}

	EXPECT_THAT(observed, UnorderedElementsAre(Pair(leftFrame, AllOf(Gt(30U), Lt(70U))), Pair(rightFrame, AllOf(Gt(30U), Lt(70U))))) << "Arrows should rotate either direction roughly 50% of the time";
}

void TestAnimatedMissileRotatesUniformly(Missile &missile, int startingDir, int leftDir, int rightDir)
{
	ankerl::unordered_dense::map<int, unsigned> observed {};
	for (auto i = 0; i < 100; i++) {
		missile.setFrameGroupRaw(startingDir);
		TestRotateBlockedMissile(missile);
		observed[missile.getFrameGroupRaw()]++;
	}

	EXPECT_THAT(observed, UnorderedElementsAre(Pair(leftDir, AllOf(Gt(30U), Lt(70U))), Pair(rightDir, AllOf(Gt(30U), Lt(70U))))) << "Animated missiles should rotate either direction roughly 50% of the time";
}

TEST(Missiles, RotateBlockedMissileArrow)
{
	Players.resize(1);
	MyPlayerId = 0;
	MyPlayer = &Players[MyPlayerId];
	*MyPlayer = {};
	LoadMissileData();

	const devilution::Player &player = Players[0];
	// missile can be a copy or a reference, there's no nullptr check and the functions that use it don't expect the instance to be part of a global structure so it doesn't really matter for this use.
	Missile missile = *AddMissile({ 0, 0 }, { 0, 0 }, Direction::South, MissileID::Arrow, TARGET_MONSTERS, player, 0, 0);

	// Arrows have a hardcoded frame count and use 1-indexed sprites
	EXPECT_EQ(missile._miAnimFrame, 1);

	TestArrowRotatesUniformly(missile, 5, 4, 6);
	TestArrowRotatesUniformly(missile, 1, 16, 2);
	TestArrowRotatesUniformly(missile, 16, 15, 1);

	// All other missiles use the number of 0-indexed sprites defined in MissileSpriteData
	missile = *AddMissile({ 0, 0 }, { 0, 0 }, Direction::South, MissileID::Firebolt, TARGET_MONSTERS, player, 0, 0);
	EXPECT_EQ(missile.getFrameGroupRaw(), 0);
	TestAnimatedMissileRotatesUniformly(missile, 5, 4, 6);
	TestAnimatedMissileRotatesUniformly(missile, 0, 15, 1);
	TestAnimatedMissileRotatesUniformly(missile, 15, 14, 0);
}

TEST(Missiles, GetDirection8)
{
	EXPECT_EQ(Direction::South, GetDirection({ 0, 0 }, { 15, 15 }));
	EXPECT_EQ(Direction::SouthWest, GetDirection({ 0, 0 }, { 0, 15 }));
	EXPECT_EQ(Direction::South, GetDirection({ 0, 0 }, { 8, 15 }));
	EXPECT_EQ(Direction::South, GetDirection({ 0, 0 }, { 8, 8 }));
	EXPECT_EQ(Direction::South, GetDirection({ 0, 0 }, { 15, 8 }));
	EXPECT_EQ(Direction::South, GetDirection({ 0, 0 }, { 15, 7 }));
	EXPECT_EQ(Direction::South, GetDirection({ 0, 0 }, { 11, 7 }));
	EXPECT_EQ(Direction::South, GetDirection({ 0, 0 }, { 8, 11 }));
	EXPECT_EQ(Direction::North, GetDirection({ 15, 15 }, { 0, 0 }));
	EXPECT_EQ(Direction::NorthEast, GetDirection({ 0, 15 }, { 0, 0 }));
	EXPECT_EQ(Direction::North, GetDirection({ 8, 15 }, { 0, 0 }));
	EXPECT_EQ(Direction::North, GetDirection({ 8, 8 }, { 0, 0 }));
	EXPECT_EQ(Direction::North, GetDirection({ 15, 8 }, { 0, 0 }));
	EXPECT_EQ(Direction::North, GetDirection({ 15, 7 }, { 0, 0 }));
	EXPECT_EQ(Direction::North, GetDirection({ 11, 7 }, { 0, 0 }));
	EXPECT_EQ(Direction::North, GetDirection({ 8, 11 }, { 0, 0 }));
	EXPECT_EQ(Direction::East, GetDirection({ 0, 15 }, { 15, 0 }));
	EXPECT_EQ(Direction::SouthEast, GetDirection({ 0, 0 }, { 15, 0 }));
	EXPECT_EQ(Direction::East, GetDirection({ 0, 8 }, { 15, 0 }));
	EXPECT_EQ(Direction::East, GetDirection({ 0, 8 }, { 8, 0 }));
	EXPECT_EQ(Direction::East, GetDirection({ 0, 15 }, { 8, 0 }));
	EXPECT_EQ(Direction::East, GetDirection({ 0, 15 }, { 7, 0 }));
	EXPECT_EQ(Direction::East, GetDirection({ 0, 11 }, { 7, 0 }));
	EXPECT_EQ(Direction::East, GetDirection({ 0, 8 }, { 11, 0 }));

	EXPECT_EQ(Direction::South, GetDirection({ 1, 1 }, { 2, 2 }));
	EXPECT_EQ(Direction::SouthWest, GetDirection({ 1, 1 }, { 1, 2 }));
	EXPECT_EQ(Direction::West, GetDirection({ 1, 1 }, { 0, 2 }));
	EXPECT_EQ(Direction::NorthWest, GetDirection({ 1, 1 }, { 0, 1 }));
	EXPECT_EQ(Direction::North, GetDirection({ 1, 1 }, { 0, 0 }));
	EXPECT_EQ(Direction::NorthEast, GetDirection({ 1, 1 }, { 1, 0 }));
	EXPECT_EQ(Direction::East, GetDirection({ 1, 1 }, { 2, 0 }));
	EXPECT_EQ(Direction::SouthEast, GetDirection({ 1, 1 }, { 2, 1 }));

	EXPECT_EQ(Direction::SouthWest, GetDirection({ 0, 0 }, { 0, 0 })) << "GetDirection is expected to default to Direction::SouthWest when the points occupy the same tile";
}

TEST(Missiles, GetDirection16)
{
	EXPECT_EQ(Direction16::South, GetDirection16({ 0, 0 }, { 15, 15 }));
	EXPECT_EQ(Direction16::SouthWest, GetDirection16({ 0, 0 }, { 0, 15 }));
	EXPECT_EQ(Direction16::South_SouthWest, GetDirection16({ 0, 0 }, { 8, 15 }));
	EXPECT_EQ(Direction16::South, GetDirection16({ 0, 0 }, { 8, 8 }));
	EXPECT_EQ(Direction16::South_SouthEast, GetDirection16({ 0, 0 }, { 15, 8 }));
	EXPECT_EQ(Direction16::South_SouthEast, GetDirection16({ 0, 0 }, { 15, 7 }));
	EXPECT_EQ(Direction16::South_SouthEast, GetDirection16({ 0, 0 }, { 11, 7 }));
	EXPECT_EQ(Direction16::South, GetDirection16({ 0, 0 }, { 8, 11 }));
	EXPECT_EQ(Direction16::North, GetDirection16({ 15, 15 }, { 0, 0 }));
	EXPECT_EQ(Direction16::NorthEast, GetDirection16({ 0, 15 }, { 0, 0 }));
	EXPECT_EQ(Direction16::North_NorthEast, GetDirection16({ 8, 15 }, { 0, 0 }));
	EXPECT_EQ(Direction16::North, GetDirection16({ 8, 8 }, { 0, 0 }));
	EXPECT_EQ(Direction16::North_NorthWest, GetDirection16({ 15, 8 }, { 0, 0 }));
	EXPECT_EQ(Direction16::North_NorthWest, GetDirection16({ 15, 7 }, { 0, 0 }));
	EXPECT_EQ(Direction16::North_NorthWest, GetDirection16({ 11, 7 }, { 0, 0 }));
	EXPECT_EQ(Direction16::North, GetDirection16({ 8, 11 }, { 0, 0 }));
	EXPECT_EQ(Direction16::East, GetDirection16({ 0, 15 }, { 15, 0 }));
	EXPECT_EQ(Direction16::SouthEast, GetDirection16({ 0, 0 }, { 15, 0 }));
	EXPECT_EQ(Direction16::East_SouthEast, GetDirection16({ 0, 8 }, { 15, 0 }));
	EXPECT_EQ(Direction16::East, GetDirection16({ 0, 8 }, { 8, 0 }));
	EXPECT_EQ(Direction16::East_NorthEast, GetDirection16({ 0, 15 }, { 8, 0 }));
	EXPECT_EQ(Direction16::East_NorthEast, GetDirection16({ 0, 15 }, { 7, 0 }));
	EXPECT_EQ(Direction16::East_NorthEast, GetDirection16({ 0, 11 }, { 7, 0 }));
	EXPECT_EQ(Direction16::East, GetDirection16({ 0, 8 }, { 11, 0 }));

	EXPECT_EQ(Direction16::South, GetDirection16({ 2, 2 }, { 3, 3 }));
	EXPECT_EQ(Direction16::South_SouthWest, GetDirection16({ 2, 2 }, { 3, 4 }));
	EXPECT_EQ(Direction16::SouthWest, GetDirection16({ 2, 2 }, { 2, 4 }));
	EXPECT_EQ(Direction16::West_SouthWest, GetDirection16({ 2, 2 }, { 1, 4 }));
	EXPECT_EQ(Direction16::West, GetDirection16({ 2, 2 }, { 1, 3 }));
	EXPECT_EQ(Direction16::West_NorthWest, GetDirection16({ 2, 2 }, { 0, 3 }));
	EXPECT_EQ(Direction16::NorthWest, GetDirection16({ 2, 2 }, { 0, 2 }));
	EXPECT_EQ(Direction16::North_NorthWest, GetDirection16({ 2, 2 }, { 0, 1 }));
	EXPECT_EQ(Direction16::North, GetDirection16({ 2, 2 }, { 1, 1 }));
	EXPECT_EQ(Direction16::North_NorthEast, GetDirection16({ 2, 2 }, { 1, 0 }));
	EXPECT_EQ(Direction16::NorthEast, GetDirection16({ 2, 2 }, { 2, 0 }));
	EXPECT_EQ(Direction16::East_NorthEast, GetDirection16({ 2, 2 }, { 3, 0 }));
	EXPECT_EQ(Direction16::East, GetDirection16({ 2, 2 }, { 3, 1 }));
	EXPECT_EQ(Direction16::East_SouthEast, GetDirection16({ 2, 2 }, { 4, 1 }));
	EXPECT_EQ(Direction16::SouthEast, GetDirection16({ 2, 2 }, { 4, 2 }));
	EXPECT_EQ(Direction16::South_SouthEast, GetDirection16({ 2, 2 }, { 4, 3 }));

	EXPECT_EQ(Direction16::South_SouthWest, GetDirection16({ 0, 0 }, { 0, 0 })) << "GetDirection16 is expected to default to Direction16::South_SouthWest when the points occupy the same tile";
}

TEST(Missiles, ProcessTownPortalRetainsOpeningAtDurationOneAndDeletesAtZero)
{
	constexpr Point portalPosition { 10, 10 };
	ASSERT_NO_FATAL_FAILURE(AssertNoActiveTriggers());
	ScopedTownPortalState stateGuard(portalPosition);

	// Keep the fixture in the already-open state; this characterizes lifetime semantics without loading portal sprites.
	Missile opening{};
	opening._mitype = MissileID::TownPortal;
	opening.position.tile = portalPosition;
	opening.position.start = portalPosition;
	opening.duration = 100;
	opening.var1 = -1;
	opening._miDelFlag = false;
	opening._mlid = NO_LIGHT;
	opening._miAnimType = MissileGraphicID::None;
	opening.setFrameGroup<PortalFrame>(PortalFrame::Idle);

	for (int tick = 0; tick < 99; tick++) {
		ProcessTownPortal(opening);
		EXPECT_EQ(opening.duration, 99 - tick);
		EXPECT_FALSE(opening._miDelFlag);
	}
	EXPECT_EQ(opening.duration, 1);
	ProcessTownPortal(opening);
	EXPECT_EQ(opening.duration, 1);
	EXPECT_FALSE(opening._miDelFlag);

	Missile expired{};
	expired._mitype = MissileID::TownPortal;
	expired.position.tile = portalPosition;
	expired.position.start = portalPosition;
	expired.duration = 0;
	expired.var1 = -1;
	expired._miDelFlag = false;
	expired._mlid = NO_LIGHT;

	ProcessTownPortal(expired);
	EXPECT_EQ(expired.duration, 0);
	EXPECT_TRUE(expired._miDelFlag);
}

TEST(Missiles, ProcessTownPortalPrecedesSameTileLevelTrigger)
{
	constexpr Point portalPosition { 25, 29 };
	ASSERT_NO_FATAL_FAILURE(AssertNoActiveTriggers());
	ScopedTownPortalState stateGuard(portalPosition);
	// ProcessTownPortal emits CMD_WARP for the local player; loopback keeps that command in-process.
	ScopedLoopbackNetwork networkGuard;
	ASSERT_TRUE(networkGuard.initialized());

	Players.emplace_back();
	Player &player = Players.front();
	MyPlayerId = 0;
	MyPlayer = &player;
	player.plractive = true;
	player.plrlevel = currentLevelNumber();
	player.plrIsOnSetLevel = false;
	player._pLvlChanging = false;
	player._pmode = PM_STAND;
	player.position.tile = portalPosition;
	player.position.future = portalPosition;
	player.position.last = portalPosition;
	player.position.old = portalPosition;
	player.position.temp = portalPosition;

	// InitTownTriggers places its first native Cathedral endpoint trigger at (25, 29).
	InitTownTriggers();
	ASSERT_GT(numTriggers(), 0);

	Missile portal{};
	portal._mitype = MissileID::TownPortal;
	portal.position.tile = portalPosition;
	portal.position.start = portalPosition;
	portal.duration = 2;
	portal.var1 = -1;
	portal._miDelFlag = false;
	portal._mlid = NO_LIGHT;
	portal._miAnimType = MissileGraphicID::None;
	portal.setFrameGroup<PortalFrame>(PortalFrame::Idle);

	ProcessTownPortal(portal);
	EXPECT_EQ(player._pmode, PM_NEWLVL);
	const auto levelBeforeTriggerCheck = currentLevelNumber();
	CheckTriggers();
	EXPECT_EQ(player._pmode, PM_NEWLVL);
	EXPECT_EQ(currentLevelNumber(), levelBeforeTriggerCheck);

	player._pmode = PM_WALK_NORTHWARDS;
	player._pLvlChanging = false;
	ProcessTownPortal(portal);
	EXPECT_EQ(player._pmode, PM_WALK_NORTHWARDS);

	player._pmode = PM_STAND;
	player.position.tile = portalPosition + Displacement { 1, 0 };
	ProcessTownPortal(portal);
	EXPECT_EQ(player._pmode, PM_STAND);

	player.position.tile = portalPosition;
	player._pLvlChanging = true;
	ProcessTownPortal(portal);
	EXPECT_EQ(player._pmode, PM_STAND);
}

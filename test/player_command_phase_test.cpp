#include <array>
#include <iterator>
#include <list>
#include <vector>

#include <gtest/gtest.h>

#include "application/headless_mode.hpp"
#include "engine/lighting.h"
#include "engine/random.hpp"
#include "game/levels/dungeon_common.h"
#include "game/missiles/missiles.hpp"
#include "game/players/players.hpp"
#include "network/protocol/multi.h"
#include "tables/spelldat.h"

using namespace devilution;

namespace devilution {
extern bool TestPlayerProcessAttackCommand(Player &player, bool ranged);
extern void TestPlayerCheckNewPath(Player &player);
}

namespace {

struct AttackSkipCase {
	ItemSpecialEffect flags;
	int readySkip;
	int completionSkip;
};

// Literal native caller expectations, not copied managed calculations.
constexpr std::array<AttackSkipCase, 6> MeleeCases {{
	{ ItemSpecialEffect::None, 0, 0 },
	{ ItemSpecialEffect::QuickAttack, 0, 1 },
	{ ItemSpecialEffect::FastAttack, 1, 2 },
	{ ItemSpecialEffect::FasterAttack, 2, 3 },
	{ ItemSpecialEffect::FastestAttack, 2, 4 },
	{ ItemSpecialEffect::QuickAttack | ItemSpecialEffect::FastAttack | ItemSpecialEffect::FasterAttack | ItemSpecialEffect::FastestAttack, 2, 4 },
}};
constexpr std::array<AttackSkipCase, 5> RangedCases {{
	{ ItemSpecialEffect::None, 0, 0 },
	{ ItemSpecialEffect::QuickAttack, 0, 1 },
	{ ItemSpecialEffect::FastAttack, 1, 2 },
	{ ItemSpecialEffect::FasterAttack, 0, 0 },
	{ ItemSpecialEffect::FastestAttack, 0, 0 },
}};

class NativePlayerCommandPhase : public testing::Test {
protected:
	void SetUp() override
	{
		// Completion calls startStand, which clears player refs across the full
		// grid. Own a fresh grid and restore every borrowed tile afterward.
		previousTiles_.reserve(MAXDUNX * MAXDUNY);
		for (int y = 0; y < MAXDUNY; ++y)
			for (int x = 0; x < MAXDUNX; ++x) {
				previousTiles_.push_back(tileAt(Point { x, y }));
				tileAt(Point { x, y }) = {};
			}
		Players.swap(previousPlayers_);
		previousMyPlayer_ = MyPlayer;
		previousMyPlayerId_ = MyPlayerId;
		previousView_ = viewPosition();
		previousLights_ = CurrentLightManager;
		previousHellfire_ = gbIsHellfire;
		previousHeadless_ = HeadlessMode;
		previousRandom_ = GetLCGEngineState();
		HeadlessMode = true;
		Players.resize(1);
		MyPlayerId = 0;
		MyPlayer = &Players[0];
	}

	void TearDown() override
	{
		size_t index = 0;
		for (int y = 0; y < MAXDUNY; ++y)
			for (int x = 0; x < MAXDUNX; ++x)
				tileAt(Point { x, y }) = previousTiles_[index++];
		Players.swap(previousPlayers_);
		MyPlayer = previousMyPlayer_;
		MyPlayerId = previousMyPlayerId_;
		viewPosition() = previousView_;
		CurrentLightManager = previousLights_;
		gbIsHellfire = previousHellfire_;
		HeadlessMode = previousHeadless_;
		SetRndSeed(previousRandom_);
	}

	void Check(const AttackSkipCase &test, bool ranged, bool completion, bool hellfire)
	{
		SCOPED_TRACE(testing::Message() << "ranged=" << ranged << " completion=" << completion << " hellfire=" << hellfire << " flags=" << static_cast<uint32_t>(test.flags));
		Player &player = Players[0];
		player = {};
		player._pClass = HeroClass::Warrior;
		player.life.current = 6400;
		player.lightId = NO_LIGHT;
		player.position.tile = { 75, 68 };
		player.position.future = player.position.tile;
		player.position.old = player.position.tile;
		player.direction = Direction::South;
		player.clearPath();
		player._pNFrames = 8;
		player._pAFrames = 12;
		player._pAFNum = 7;
		player._pIFlags = test.flags;
		player._pmode = ranged ? PM_RATTACK : PM_ATTACK;
		player.animInfo.setNewAnimation(std::nullopt, 12, 1);
		player.animInfo.currentFrame = completion ? 11 : 7;
		player.destAction = ranged ? ACTION_RATTACK : ACTION_ATTACK;
		player.destParam1 = 76;
		player.destParam2 = 68;
		gbIsHellfire = hellfire;
		const auto beforeRandom = GetLCGEngineState();

		EXPECT_EQ(TestPlayerProcessAttackCommand(player, ranged), completion);

		const auto cursor = player.animInfo.captureTimingState();
		const int skipped = ranged && hellfire ? 0 : completion ? test.completionSkip : test.readySkip;
		EXPECT_EQ(player._pmode, ranged ? PM_RATTACK : PM_ATTACK);
		EXPECT_EQ(player.destAction, ACTION_NONE);
		EXPECT_EQ(cursor.currentFrame, skipped);
		EXPECT_EQ(cursor.numberOfFrames, 12);
		EXPECT_EQ(cursor.ticksPerFrame, 1);
		EXPECT_EQ(cursor.ticksSinceSequenceStarted, -AnimationInfo::baseValueFraction);
		EXPECT_EQ(cursor.skippedFramesFromPreviousAnimation, completion ? 0 : 4);
		EXPECT_EQ(GetLCGEngineState(), beforeRandom);
		player.animInfo.processAnimation();
		EXPECT_EQ(player.animInfo.currentFrame, skipped + 1);
		EXPECT_EQ(player.animInfo.captureTimingState().ticksSinceSequenceStarted, 0);
	}

private:
	std::vector<Tile> previousTiles_;
	std::vector<Player> previousPlayers_;
	Player *previousMyPlayer_ = nullptr;
	uint8_t previousMyPlayerId_ = 0;
	Point previousView_;
	LightManager previousLights_;
	bool previousHellfire_ = false;
	bool previousHeadless_ = true;
	uint32_t previousRandom_ = 0;
};

TEST_F(NativePlayerCommandPhase, MeleeReadyRepeatsExcludeFirstFrame)
{
	for (const auto &test : MeleeCases)
		Check(test, false, false, false);
}

TEST_F(NativePlayerCommandPhase, MeleeCompletionLoopIncludesFirstFrame)
{
	for (const auto &test : MeleeCases)
		Check(test, false, true, false);
}

TEST_F(NativePlayerCommandPhase, DiabloRangedReadyAndCompletionUseDifferentSkips)
{
	for (const auto &test : RangedCases)
		for (const bool completion : { false, true })
			Check(test, true, completion, false);
}

TEST_F(NativePlayerCommandPhase, HellfireRangedDoesNotUseDiabloSpeedSkips)
{
	for (const auto &test : RangedCases)
		for (const bool completion : { false, true })
			Check(test, true, completion, true);
}

// BEGIN native player action-start characterization
struct NativeActionTimingRows {
	AnimationTimingState start;
	AnimationTimingState afterEndOfPlayerUpdate;
	AnimationTimingState nextTick;
};

constexpr NativeActionTimingRows IdleWalkTimingRows {
	{ 8, 1, 0, -2, false, 8, 0, 113, -128 },
	{ 8, 1, 0, -1, false, 8, 0, 113, 0 },
	{ 8, 1, 0, 0, false, 8, 0, 113, 128 },
};
constexpr NativeActionTimingRows TownRunTimingRows {
	{ 8, 1, 0, 2, false, 8, 0, 204, -128 },
	{ 8, 1, 0, 3, false, 8, 0, 204, 0 },
	{ 8, 1, 0, 4, false, 8, 0, 204, 128 },
};
constexpr NativeActionTimingRows CompletionWalkTimingRows {
	{ 8, 1, 0, -1, false, 8, 0, 128, -128 },
	{ 8, 1, 0, 0, false, 8, 0, 128, 0 },
	{ 8, 1, 0, 1, false, 8, 0, 128, 128 },
};
constexpr NativeActionTimingRows IdleSpellTimingRows {
	{ 8, 1, 0, 0, false, 4, 0, 170, -128 },
	{ 8, 1, 0, 1, false, 4, 0, 170, 0 },
	{ 8, 1, 0, 2, false, 4, 0, 170, 128 },
};
constexpr NativeActionTimingRows ReadySpellRepeatTimingRows {
	{ 8, 1, 0, 0, false, 4, 2, 256, -128 },
	{ 8, 1, 0, 1, false, 4, 2, 256, 0 },
	{ 8, 1, 0, 2, false, 4, 2, 256, 128 },
};

void ExpectAnimationTiming(const Player &player, const AnimationTimingState &expected, const char *phase)
{
	const AnimationTimingState actual = player.animInfo.captureTimingState();
	SCOPED_TRACE(phase);
	EXPECT_EQ(static_cast<int>(actual.numberOfFrames), static_cast<int>(expected.numberOfFrames));
	EXPECT_EQ(static_cast<int>(actual.ticksPerFrame), static_cast<int>(expected.ticksPerFrame));
	EXPECT_EQ(static_cast<int>(actual.tickCounterOfCurrentFrame), static_cast<int>(expected.tickCounterOfCurrentFrame));
	EXPECT_EQ(static_cast<int>(actual.currentFrame), static_cast<int>(expected.currentFrame));
	EXPECT_EQ(actual.isPetrified, expected.isPetrified);
	EXPECT_EQ(static_cast<int>(actual.relevantFramesForDistributing), static_cast<int>(expected.relevantFramesForDistributing));
	EXPECT_EQ(static_cast<int>(actual.skippedFramesFromPreviousAnimation), static_cast<int>(expected.skippedFramesFromPreviousAnimation));
	EXPECT_EQ(static_cast<unsigned>(actual.tickModifier), static_cast<unsigned>(expected.tickModifier));
	EXPECT_EQ(static_cast<int>(actual.ticksSinceSequenceStarted), static_cast<int>(expected.ticksSinceSequenceStarted));
}

void ExpectActionTimingRows(Player &player, const NativeActionTimingRows &expected)
{
	ExpectAnimationTiming(player, expected.start, "immediately after native action start");
	// ProcessPlayers calls CheckNewPath in its completion loop, then advances
	// AnimationInfo once at the end of the player update. The test hooks return
	// at that start boundary; invoke that exact animation update twice so the
	// captured rows isolate the end-of-update and following-tick transitions.
	player.animInfo.processAnimation();
	ExpectAnimationTiming(player, expected.afterEndOfPlayerUpdate, "after one end-of-player animation update");
	player.animInfo.processAnimation();
	ExpectAnimationTiming(player, expected.nextTick, "on the following animation tick");
}

class NativePlayerActionStart : public NativePlayerCommandPhase {
protected:
	void SetUp() override
	{
		NativePlayerCommandPhase::SetUp();
		previousRunInTown_ = sgGameInitInfo.bRunInTown;
		previousMultiplayer_ = gbIsMultiplayer;
		previousSpells_.swap(SpellsData);
		previousMissiles_.swap(Missiles);
		snapshotsActive_ = true;

		sgGameInitInfo.bRunInTown = 0;
		gbIsMultiplayer = false;
		gbIsHellfire = false;

		SpellsData.resize(static_cast<size_t>(SpellID::LAST) + 1);
		SpellData &firebolt = SpellsData[static_cast<size_t>(SpellID::Firebolt)];
		firebolt.flags = SpellDataFlags::Fire;
		firebolt.sSFX = SfxID::CastSpell;
		firebolt.sMissiles[0] = MissileID::Null;
		firebolt.sMissiles[1] = MissileID::Null;
	}

	void TearDown() override
	{
		if (snapshotsActive_) {
			SpellsData.swap(previousSpells_);
			Missiles.swap(previousMissiles_);
			sgGameInitInfo.bRunInTown = previousRunInTown_;
			gbIsMultiplayer = previousMultiplayer_;
			snapshotsActive_ = false;
		}
		NativePlayerCommandPhase::TearDown();
	}

	Player &PreparePlayer()
	{
		Player &player = Players[0];
		player = {};
		player._pClass = HeroClass::Warrior;
		player.life = { 6400, 6400, 6400, 6400, 0 };
		player.lightId = NO_LIGHT;
		player.position.tile = { 75, 68 };
		player.position.future = player.position.tile;
		player.position.old = player.position.tile;
		player.position.temp = player.position.tile;
		player.direction = Direction::South;
		player._pmode = PM_STAND;
		player._pNFrames = 8;
		player._pWFrames = 8;
		player._pAFrames = 8;
		player._pAFNum = 4;
		player._pSFrames = 8;
		player._pSFNum = 5;
		player.queuedSpell = { SpellID::Firebolt, SpellType::Skill, 0, 1 };
		player._pSplLvl[static_cast<size_t>(SpellID::Firebolt)] = 1;
		player.animInfo.setNewAnimation(std::nullopt, 8, 1);
		player.clearPath();
		PrepareWalkableNeighborhood(player.position.tile);
		player.occupyTile(player.position.tile, false);
		return player;
	}

private:
	void PrepareWalkableNeighborhood(Point center)
	{
		const auto &properties = tileProperties();
		size_t walkablePiece = std::size(properties);
		for (size_t piece = 0; piece < std::size(properties); ++piece) {
			if (HasNoneOf(properties[piece], TileProperties::Solid)) {
				walkablePiece = piece;
				break;
			}
		}
		ASSERT_LT(walkablePiece, std::size(properties));
		if (walkablePiece == std::size(properties))
			return;
		for (int y = -1; y <= 1; ++y) {
			for (int x = -1; x <= 1; ++x)
				tileAt({ center.x + x, center.y + y }).setPiece(static_cast<uint16_t>(walkablePiece));
		}
	}

	std::vector<SpellData> previousSpells_;
	std::list<Missile> previousMissiles_;
	uint8_t previousRunInTown_ = 0;
	bool previousMultiplayer_ = false;
	bool snapshotsActive_ = false;
};

class NativePlayerTownLevelScope {
public:
	NativePlayerTownLevelScope()
	    : previousType_(levelType())
	{
		levelType() = DTYPE_TOWN;
		tiles_.reserve(MAXDUNX * MAXDUNY);
		for (int y = 0; y < MAXDUNY; ++y) {
			for (int x = 0; x < MAXDUNX; ++x) {
				tiles_.push_back(tileAt({ x, y }));
				tileAt({ x, y }) = {};
			}
		}
	}

	~NativePlayerTownLevelScope()
	{
		size_t index = 0;
		for (int y = 0; y < MAXDUNY; ++y) {
			for (int x = 0; x < MAXDUNX; ++x)
				tileAt({ x, y }) = tiles_[index++];
		}
		levelType() = previousType_;
	}

private:
	dungeon_type previousType_;
	std::vector<Tile> tiles_;
};

TEST_F(NativePlayerActionStart, IdleWalkStartsNorthwardFamily)
{
	Player &player = PreparePlayer();
	player.walkpath[0] = WALK_N;

	TestPlayerCheckNewPath(player);

	EXPECT_EQ(player._pmode, PM_WALK_NORTHWARDS);
	EXPECT_EQ(player.direction, Direction::North);
	ExpectActionTimingRows(player, IdleWalkTimingRows);
}

TEST_F(NativePlayerActionStart, IdleWalkStartsSidewaysFamily)
{
	Player &player = PreparePlayer();
	player.walkpath[0] = WALK_E;

	TestPlayerCheckNewPath(player);

	EXPECT_EQ(player._pmode, PM_WALK_SIDEWAYS);
	EXPECT_EQ(player.direction, Direction::East);
	ExpectActionTimingRows(player, IdleWalkTimingRows);
}

TEST_F(NativePlayerActionStart, IdleWalkStartsSouthwardFamily)
{
	Player &player = PreparePlayer();
	player.walkpath[0] = WALK_S;

	TestPlayerCheckNewPath(player);

	EXPECT_EQ(player._pmode, PM_WALK_SOUTHWARDS);
	EXPECT_EQ(player.direction, Direction::South);
	ExpectActionTimingRows(player, IdleWalkTimingRows);
}

TEST_F(NativePlayerActionStart, TownRunUsesItsDistinctPendingWalkSkip)
{
	NativePlayerTownLevelScope town;
	sgGameInitInfo.bRunInTown = 1;
	Player &player = PreparePlayer();
	player.walkpath[0] = WALK_N;

	TestPlayerCheckNewPath(player);

	EXPECT_EQ(player._pmode, PM_WALK_NORTHWARDS);
	EXPECT_EQ(player.direction, Direction::North);
	ExpectActionTimingRows(player, TownRunTimingRows);
}

TEST_F(NativePlayerActionStart, AttackCompletionStartsNextWalkWithCompletionSkip)
{
	Player &player = PreparePlayer();
	player.destAction = ACTION_ATTACK;
	player.destParam1 = 76;
	player.destParam2 = 68;
	TestPlayerCheckNewPath(player);
	ASSERT_EQ(player._pmode, PM_ATTACK);
	for (int tick = 0; tick < 7; ++tick)
		player.animInfo.processAnimation();
	ASSERT_EQ(static_cast<int>(player.animInfo.currentFrame), 7);

	player.walkpath[0] = WALK_E;
	ASSERT_TRUE(TestPlayerProcessAttackCommand(player, false));

	EXPECT_EQ(player._pmode, PM_WALK_SIDEWAYS);
	EXPECT_EQ(player.direction, Direction::East);
	ExpectActionTimingRows(player, CompletionWalkTimingRows);
}

TEST_F(NativePlayerActionStart, IdleSpellStartsWithPendingDistribution)
{
	Player &player = PreparePlayer();
	player.destAction = ACTION_SPELL;
	player.destParam1 = 76;
	player.destParam2 = 68;

	TestPlayerCheckNewPath(player);

	EXPECT_EQ(player._pmode, PM_SPELL);
	EXPECT_EQ(player.destAction, ACTION_NONE);
	ExpectActionTimingRows(player, IdleSpellTimingRows);
}

TEST_F(NativePlayerActionStart, SpellReadyRepeatRetainsThePriorTruncatedFrames)
{
	Player &player = PreparePlayer();
	player.destAction = ACTION_SPELL;
	player.destParam1 = 76;
	player.destParam2 = 68;
	TestPlayerCheckNewPath(player);
	ASSERT_EQ(player._pmode, PM_SPELL);

	for (int tick = 0; tick < player._pSFNum; ++tick)
		player.animInfo.processAnimation();
	ASSERT_EQ(static_cast<int>(player.animInfo.currentFrame), player._pSFNum);

	player.destAction = ACTION_SPELL;
	TestPlayerCheckNewPath(player);

	EXPECT_EQ(player._pmode, PM_SPELL);
	EXPECT_EQ(player.destAction, ACTION_NONE);
	ExpectActionTimingRows(player, ReadySpellRepeatTimingRows);
}
// END native player action-start characterization

} // namespace

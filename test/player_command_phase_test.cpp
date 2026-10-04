#include <array>
#include <vector>

#include <gtest/gtest.h>

#include "application/headless_mode.hpp"
#include "engine/lighting.h"
#include "engine/random.hpp"
#include "game/levels/dungeon_common.h"
#include "game/players/players.hpp"

using namespace devilution;

namespace devilution {
extern bool TestPlayerProcessAttackCommand(Player &player, bool ranged);
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

} // namespace

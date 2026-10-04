#include <gtest/gtest.h>

#include <algorithm>
#include <array>
#include <utility>
#include <vector>

#include "application/game_mode.hpp"
#include "engine/bestiary.hpp"
#include "engine/random.hpp"
#include "game/levels/dungeon_common.h"
#include "game/monsters/monster_pool.hpp"
#include "game/monsters/monsters.hpp"
#include "network/protocol/multi.h"
#include "tables/monstdat.h"

namespace devilution {
namespace {

// Exercise the registered native controller, not a second implementation of
// its branches. Synthetic animation/tile data keep this oracle MPQ-free.
class HorkDemonAiTest : public testing::Test {
protected:
	static constexpr unsigned Slot = 9;
	static constexpr Point Origin { 32, 32 };

	void SetUp() override
	{
		savedRandom_ = GetLCGEngineState();
		savedMultiplayer_ = gbIsMultiplayer;
		savedRegistry_ = AiRegistry;
		savedMonster_ = std::move(Monsters[Slot]);
		// DeleteMonsterList also sweeps the four reserved Golem slots even
		// when they are absent from the active list. Preserve their ownership.
		for (unsigned slot = 0; slot < savedReserved_.size(); ++slot) {
			savedReserved_[slot] = std::move(Monsters[slot]);
			Monsters[slot] = {};
		}
		savedType_ = std::move(LevelBestiary.types[0]);
		savedCount_ = ActiveMonsterCount;
		std::copy_n(ActiveMonsters, MaxMonsters, savedActive_.begin());
		savedFloor_ = tileProperties()[0];
		savedWall_ = tileProperties()[1];
		for (int y = 24; y <= 40; ++y) {
			for (int x = 24; x <= 40; ++x) {
				const Point tile { x, y };
				savedTiles_.emplace_back(tile, tileAt(tile));
				tileAt(tile) = {};
				tileAt(tile).setTransVal(1);
				tileAt(tile).setFlags(DungeonFlag::Visible);
			}
		}
		tileProperties()[0] = TileProperties::None;
		tileProperties()[1] = TileProperties::Solid;
		LevelBestiary.types[0] = {};
		LevelBestiary.types[0].type = MT_HORKDMN;
		for (AnimStruct &animation : LevelBestiary.types[0].anims) {
			animation.frames = 12;
			animation.rate = 1;
		}
		RegisterAiFunctions();
		ActiveMonsterCount = 1;
		ActiveMonsters[0] = Slot;
		Monsters[Slot] = {};
		auto &monster = Monsters[Slot];
		monster.position.tile = Origin;
		monster.position.old = Origin;
		monster.position.future = Origin;
		monster.ai = MonsterAIID::HorkDemon;
		monster.activeForTicks = 255;
		monster.mode = MonsterMode::Stand;
		monster.goal = MonsterGoal::Normal;
		monster.uniqueType = UniqueMonsterType::None;
		monster.lightId = -1;
		monster.hitPoints = monster.maxHitPoints = 128;
		// Preserve our explicit target in ProcessMonsters without player tables.
		monster.flags = MFLAG_NO_ENEMY;
		monster.animInfo.numberOfFrames = 12;
		monster.animInfo.ticksPerFrame = 1;
		tileAt(Origin).setMonster(Slot + 1);
	}

	void TearDown() override
	{
		for (const auto &[point, tile] : savedTiles_)
			tileAt(point) = tile;
		tileProperties()[0] = savedFloor_;
		tileProperties()[1] = savedWall_;
		std::copy(savedActive_.begin(), savedActive_.end(), ActiveMonsters);
		ActiveMonsterCount = savedCount_;
		Monsters[Slot] = std::move(savedMonster_);
		for (unsigned slot = 0; slot < savedReserved_.size(); ++slot)
			Monsters[slot] = std::move(savedReserved_[slot]);
		LevelBestiary.types[0] = std::move(savedType_);
		AiRegistry = std::move(savedRegistry_);
		gbIsMultiplayer = savedMultiplayer_;
		SetRndSeed(savedRandom_);
	}

	void SetEnemyDistance(int distance)
	{
		auto &monster = Monsters[Slot];
		monster.position.last = { static_cast<WorldTileCoord>(Origin.x + distance), static_cast<WorldTileCoord>(Origin.y) };
		monster.enemyPosition = monster.position.last;
		monster.direction = GetDirection(Origin, monster.position.last);
	}

	void Decide(uint32_t seed, int distance, bool multiplayer = true)
	{
		SetEnemyDistance(distance);
		SetRndSeed(seed);
		// Native ProcessMonsters' MP prefix precedes HorkDemonAi. Full-process
		// cases below separately verify this prefix and the SP shared stream.
		if (multiplayer)
			Monsters[Slot].aiSeed = AdvanceRndSeed();
		AiRegistry.at(MonsterAIID::HorkDemon)(Monsters[Slot]);
	}

private:
	uint32_t savedRandom_;
	bool savedMultiplayer_;
	decltype(AiRegistry) savedRegistry_;
	Monster savedMonster_;
	std::array<Monster, 4> savedReserved_;
	CMonster savedType_;
	size_t savedCount_;
	std::array<unsigned, MaxMonsters> savedActive_;
	std::vector<std::pair<Point, Tile>> savedTiles_;
	TileProperties savedFloor_, savedWall_;
};

TEST_F(HorkDemonAiTest, MultiplayerNormalWalkConsumesActionMovementAndBothCoins)
{
	Decide(7, 2);
	EXPECT_EQ(Monsters[Slot].aiSeed, 158868340U);
	EXPECT_EQ(GetLCGEngineState(), 2771084784U);
	EXPECT_TRUE(IsMonsterModeMove(Monsters[Slot].mode));
	EXPECT_EQ(Monsters[Slot].goal, MonsterGoal::Normal);
	EXPECT_EQ(Monsters[Slot].position.future, (Point { 33, 32 }));
}

TEST_F(HorkDemonAiTest, MultiplayerDelayConsumesActionMovementAndDelay)
{
	Decide(17, 2);
	EXPECT_EQ(Monsters[Slot].aiSeed, 385823110U);
	EXPECT_EQ(GetLCGEngineState(), 2584126973U);
	EXPECT_EQ(Monsters[Slot].mode, MonsterMode::Delay);
	EXPECT_EQ(Monsters[Slot].var2, 15);
}

TEST_F(HorkDemonAiTest, DistantAdmissionPrecedesMovementAndSuccessfulRoundWalkHasNoCoins)
{
	Decide(7, 6);
	EXPECT_EQ(GetLCGEngineState(), 476854291U);
	EXPECT_EQ(Monsters[Slot].goal, MonsterGoal::Move);
	EXPECT_EQ(Monsters[Slot].goalVar1, 1);
	EXPECT_EQ(Monsters[Slot].goalVar2, 0);
	EXPECT_TRUE(IsMonsterModeMove(Monsters[Slot].mode));
	EXPECT_EQ(Monsters[Slot].position.future, (Point { 32, 33 }));
}

TEST_F(HorkDemonAiTest, FailedRoundWalkConsumesFallbackCoinsThenDelay)
{
	for (int y = 31; y <= 33; ++y)
		for (int x = 31; x <= 33; ++x)
			if (Point { x, y } != Origin)
				tileAt(Point { x, y }).setPiece(1);
	Decide(7, 6);
	EXPECT_EQ(GetLCGEngineState(), 978261158U);
	EXPECT_EQ(Monsters[Slot].goal, MonsterGoal::Move);
	EXPECT_EQ(Monsters[Slot].goalVar2, 1);
	EXPECT_EQ(Monsters[Slot].mode, MonsterMode::Delay);
	EXPECT_EQ(Monsters[Slot].var2, 17);
}

TEST_F(HorkDemonAiTest, ContinuingMoveGoalSkipsNewAdmissionAndTurnDraws)
{
	Monsters[Slot].goal = MonsterGoal::Move;
	Monsters[Slot].goalVar1 = 2;
	Decide(7, 6);
	EXPECT_EQ(GetLCGEngineState(), 2071244549U);
	EXPECT_EQ(Monsters[Slot].goalVar1, 3);
	EXPECT_TRUE(IsMonsterModeMove(Monsters[Slot].mode));
}

TEST_F(HorkDemonAiTest, RangedSpawnStopsAfterActionDraw)
{
	Decide(7, 3);
	EXPECT_EQ(GetLCGEngineState(), 2071244549U);
	EXPECT_EQ(Monsters[Slot].mode, MonsterMode::SpecialRangedAttack);
}

TEST_F(HorkDemonAiTest, SeededMultiplayerReplaySpawnConsumesItsNativeActionRoll)
{
	Decide(0x12345678U, 3);
	EXPECT_EQ(Monsters[Slot].aiSeed, 1788971303U);
	EXPECT_EQ(GetLCGEngineState(), 1443805422U);
	EXPECT_EQ(Monsters[Slot].mode, MonsterMode::SpecialRangedAttack);
	EXPECT_EQ(Monsters[Slot].goal, MonsterGoal::Normal);
}

TEST_F(HorkDemonAiTest, BlockedSpawnAdmissionDoesNotFallThroughToMovement)
{
	tileAt(Point { 33, 32 }).setPiece(1);
	Decide(7, 3);
	EXPECT_EQ(GetLCGEngineState(), 2071244549U);
	EXPECT_EQ(Monsters[Slot].mode, MonsterMode::Stand);
	EXPECT_EQ(Monsters[Slot].goal, MonsterGoal::Normal);
}

TEST_F(HorkDemonAiTest, ExpiredOrDifferentRoomMoveGoalReturnsToNormalWithoutTurnDraw)
{
	for (const bool otherRoom : { false, true }) {
		Monsters[Slot].mode = MonsterMode::Stand;
		Monsters[Slot].goal = MonsterGoal::Move;
		Monsters[Slot].goalVar1 = otherRoom ? 0 : 12;
		tileAt(Point { 38, 32 }).setTransVal(otherRoom ? 2 : 1);
		Decide(7, 6);
		EXPECT_EQ(GetLCGEngineState(), 2071244549U);
		EXPECT_EQ(Monsters[Slot].goal, MonsterGoal::Normal);
		EXPECT_EQ(Monsters[Slot].goalVar1, otherRoom ? 1 : 13);
		EXPECT_EQ(Monsters[Slot].mode, MonsterMode::SpecialRangedAttack);
	}
}

TEST_F(HorkDemonAiTest, AdjacentEnemyEndsMoveGoalBeforeMeleeAdmission)
{
	Monsters[Slot].goal = MonsterGoal::Move;
	Monsters[Slot].goalVar1 = 2;
	Decide(7, 1);
	EXPECT_EQ(GetLCGEngineState(), 2071244549U);
	EXPECT_EQ(Monsters[Slot].goal, MonsterGoal::Normal);
	EXPECT_EQ(Monsters[Slot].mode, MonsterMode::MeleeAttack);
}

TEST_F(HorkDemonAiTest, SignedMinimumDelayRollIsNotClamped)
{
	// Independently invert the LCG four times from 0x80000000: prefix,
	// action 76, movement 34, then GenerateRnd(10) == -8.
	Decide(2557708932U, 2);
	EXPECT_EQ(GetLCGEngineState(), 0x80000000U);
	EXPECT_EQ(Monsters[Slot].mode, MonsterMode::Delay);
	EXPECT_EQ(Monsters[Slot].var2, 2);
}

TEST_F(HorkDemonAiTest, DormantAndNonStandingAiDoNotDraw)
{
	Monsters[Slot].activeForTicks = 0;
	Decide(7, 2, false);
	EXPECT_EQ(GetLCGEngineState(), 7U);
	Monsters[Slot].activeForTicks = 255;
	Monsters[Slot].mode = MonsterMode::Delay;
	Decide(7, 2, false);
	EXPECT_EQ(GetLCGEngineState(), 7U);
}

TEST_F(HorkDemonAiTest, ActualMultiplayerTickRestoresAndAdvancesThePerMonsterSeed)
{
	SetEnemyDistance(2);
	gbIsMultiplayer = true;
	Monsters[Slot].aiSeed = 7;
	SetRndSeed(0xCAFEBABEU);
	ProcessMonsters();
	EXPECT_EQ(Monsters[Slot].aiSeed, 158868340U);
	EXPECT_EQ(GetLCGEngineState(), 2771084784U);
	EXPECT_TRUE(IsMonsterModeMove(Monsters[Slot].mode));
}

TEST_F(HorkDemonAiTest, ActualSinglePlayerTickUsesSharedStreamWithoutSeedPrefix)
{
	SetEnemyDistance(2);
	gbIsMultiplayer = false;
	Monsters[Slot].aiSeed = 0xCAFEBABEU;
	SetRndSeed(7);
	ProcessMonsters();
	EXPECT_EQ(Monsters[Slot].aiSeed, 0xCAFEBABEU);
	EXPECT_EQ(GetLCGEngineState(), 476854291U);
	EXPECT_TRUE(IsMonsterModeMove(Monsters[Slot].mode));
}

TEST(MonsterTest, RegenerationAddsRawQ6AndHonorsNativeLifeAndMaximumBoundaries)
{
	struct Case {
		int health;
		unsigned level;
		int expected;
	};
	for (const auto sample : { Case { 640, 0, 640 }, Case { 640, 1, 641 },
	         Case { 640, 4, 642 }, Case { 640, 16, 648 }, Case { 640, 31, 655 },
	         Case { 0, 4, 0 }, Case { 63, 4, 63 }, Case { 64, 4, 66 },
	         Case { 1279, 4, 1280 }, Case { 1280, 4, 1280 } }) {
		Monster monster;
		monster.hitPoints = sample.health;
		monster.maxHitPoints = 1280;
		RegenerateMonsterHitPoints(monster, sample.level);
		EXPECT_EQ(monster.hitPoints, sample.expected) << "health=" << sample.health << ", level=" << sample.level;
	}
}

TEST(MonsterTest, PetrifiedMonsterRetainsPassiveRegeneration)
{
	Monster monster;
	monster.mode = MonsterMode::Petrified;
	monster.hitPoints = 640;
	monster.maxHitPoints = 1280;
	RegenerateMonsterHitPoints(monster, 4);
	EXPECT_EQ(monster.hitPoints, 642);
	EXPECT_EQ(monster.mode, MonsterMode::Petrified);
}

TEST(MonsterTest, IsMonsterModeMove)
{
	EXPECT_TRUE(IsMonsterModeMove(MonsterMode::MoveNorthwards));
	EXPECT_TRUE(IsMonsterModeMove(MonsterMode::MoveSouthwards));
	EXPECT_TRUE(IsMonsterModeMove(MonsterMode::MoveSideways));
	EXPECT_FALSE(IsMonsterModeMove(MonsterMode::Stand));
	EXPECT_FALSE(IsMonsterModeMove(MonsterMode::MeleeAttack));
	EXPECT_FALSE(IsMonsterModeMove(MonsterMode::Death));
	EXPECT_FALSE(IsMonsterModeMove(MonsterMode::Petrified));
}

TEST(MonsterTest, GetMonsterMovementModeMatchesDirection)
{
	EXPECT_EQ(GetMonsterMovementMode(Direction::NorthWest), MonsterMode::MoveNorthwards);
	EXPECT_EQ(GetMonsterMovementMode(Direction::North), MonsterMode::MoveNorthwards);
	EXPECT_EQ(GetMonsterMovementMode(Direction::NorthEast), MonsterMode::MoveNorthwards);
	EXPECT_EQ(GetMonsterMovementMode(Direction::West), MonsterMode::MoveSideways);
	EXPECT_EQ(GetMonsterMovementMode(Direction::East), MonsterMode::MoveSideways);
	EXPECT_EQ(GetMonsterMovementMode(Direction::SouthWest), MonsterMode::MoveSouthwards);
	EXPECT_EQ(GetMonsterMovementMode(Direction::South), MonsterMode::MoveSouthwards);
	EXPECT_EQ(GetMonsterMovementMode(Direction::SouthEast), MonsterMode::MoveSouthwards);
	EXPECT_EQ(GetMonsterMovementMode(Direction::NoDirection), MonsterMode::Stand);
	EXPECT_EQ(GetMonsterMovementMode(static_cast<Direction>(255)), MonsterMode::Stand);
}

TEST(MonsterTest, IsSkel)
{
	EXPECT_TRUE(IsSkel(MT_WSKELAX));
	EXPECT_TRUE(IsSkel(MT_TSKELAX));
	EXPECT_TRUE(IsSkel(MT_RSKELAX));
	EXPECT_TRUE(IsSkel(MT_XSKELAX));
	EXPECT_TRUE(IsSkel(MT_WSKELBW));
	EXPECT_TRUE(IsSkel(MT_TSKELBW));
	EXPECT_TRUE(IsSkel(MT_RSKELBW));
	EXPECT_TRUE(IsSkel(MT_XSKELBW));
	EXPECT_TRUE(IsSkel(MT_WSKELSD));
	EXPECT_TRUE(IsSkel(MT_TSKELSD));
	EXPECT_TRUE(IsSkel(MT_RSKELSD));
	EXPECT_TRUE(IsSkel(MT_XSKELSD));

	EXPECT_FALSE(IsSkel(MT_NZOMBIE));
	EXPECT_FALSE(IsSkel(MT_NGOATMC));
	EXPECT_FALSE(IsSkel(MT_SKING));
	EXPECT_FALSE(IsSkel(MT_NSCAV));
}

TEST(MonsterTest, IsGoat)
{
	EXPECT_TRUE(IsGoat(MT_NGOATMC));
	EXPECT_TRUE(IsGoat(MT_BGOATMC));
	EXPECT_TRUE(IsGoat(MT_RGOATMC));
	EXPECT_TRUE(IsGoat(MT_GGOATMC));
	EXPECT_TRUE(IsGoat(MT_NGOATBW));
	EXPECT_TRUE(IsGoat(MT_BGOATBW));
	EXPECT_TRUE(IsGoat(MT_RGOATBW));
	EXPECT_TRUE(IsGoat(MT_GGOATBW));

	EXPECT_FALSE(IsGoat(MT_NZOMBIE));
	EXPECT_FALSE(IsGoat(MT_WSKELAX));
	EXPECT_FALSE(IsGoat(MT_SKING));
}

} // namespace
} // namespace devilution

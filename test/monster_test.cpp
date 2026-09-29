#include <gtest/gtest.h>

#include "game/monsters/monsters.hpp"
#include "tables/monstdat.h"

namespace devilution {
namespace {

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

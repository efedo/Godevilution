#include <gtest/gtest.h>

#include "engine/render/primitive_render.hpp"
#include "game/levels/dungeon_common.h"
#include "game/monsters/monster_corpses.hpp"

using namespace devilution;

TEST(Corpses, AddCorpse)
{
	AddCorpse({ 21, 48 }, 8, Direction::West);
	EXPECT_EQ(tileAt(Point { 21, 48 }).corpse(), static_cast<int8_t>(8 + (static_cast<int>(Direction::West) << 5)));
}

TEST(Corpses, AddCorpse_OOB)
{
	AddCorpse({ 21, 48 }, MaxCorpses + 1, Direction::West);
	EXPECT_EQ(tileAt(Point { 21, 48 }).corpse(), static_cast<int8_t>(0 + (static_cast<int>(Direction::West) << 5)));
}

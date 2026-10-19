/**
 * @file levels/level.cpp
 *
 * Implementation of the Level class.
 */
#include "game/levels/level.hpp"

namespace devilution {

uint8_t DungeonMegaTile::current() const
{
	return current_;
}

void DungeonMegaTile::setCurrent(uint8_t value)
{
	current_ = value;
}

uint8_t DungeonMegaTile::replacement() const
{
	return replacement_;
}

void DungeonMegaTile::setReplacement(uint8_t value)
{
	replacement_ = value;
}

void DungeonMegaTile::snapshotReplacement()
{
	replacement_ = current_;
}

void DungeonMegaTile::applyReplacement()
{
	current_ = replacement_;
}

Level::Level(LevelId id)
	: id_(id)
{
}

/*static*/
Level Level::create(LevelId id)
{
	return Level(id);
}

} // namespace devilution

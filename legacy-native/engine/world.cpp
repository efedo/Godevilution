/**
 * @file engine/world.cpp
 *
 * Implementation of the World class.
 */
#include "engine/world.hpp"

#include <cassert>
#include <memory>
#include <stdexcept>

#include "game/levels/dungeon_common_defs.hpp"

namespace devilution {

World CurrentWorld;

World::World()
{
	// Initialize with an empty placeholder level.
	// The actual level initialization (CreateLevel, CreateTown, etc.) is handled
	// by the existing initialization code via SwitchCurrentLevel() and related functions.
	// This just ensures currentLevel() is always valid.
	LevelId emptyLevel;
	currentLevel_ = std::make_unique<Level>(emptyLevel);
}

// ---------------------------------------------------------------------------
// Current level
// ---------------------------------------------------------------------------

void World::switchLevel(LevelId target)
{
	currentLevel_->setId(target);
}

Level &World::currentLevel()
{
	assert(currentLevel_ && "World::currentLevel: no current level");
	return *currentLevel_;
}

const Level &World::currentLevel() const
{
	assert(currentLevel_ && "World::currentLevel: no current level");
	return *currentLevel_;
}

// ---------------------------------------------------------------------------
// Lifecycle
// ---------------------------------------------------------------------------

void World::reset()
{
	// Reset to empty placeholder level.
	LevelId emptyLevel;
	currentLevel_ = std::make_unique<Level>(emptyLevel);
	cachedLevels_.clear();
}

} // namespace devilution

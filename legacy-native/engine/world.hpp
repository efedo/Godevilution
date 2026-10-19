/**
 * @file engine/world.hpp
 *
 * The World class is the top-level owner of all game-session state.
 *
 * It holds:
 *   - Game-wide configuration (difficulty, flags, seeds).
 *   - A collection of Level objects, keyed by LevelIndex.
 *   - The concept of the "current level" that the local player occupies.
 *
 * A single global instance, `CurrentWorld`, is provided.  The free
 * function `currentLevel()` returns a reference to whichever Level the
 * local player is currently on.
 *
 * Relationship to existing globals
 * ----------------------------------
 * The globals in gendung.h (currentLevelNumber(), levelType(), isSetLevel(), …) and
 * diablo.h (DungeonSeeds, LevelSeeds, sgGameInitInfo, …) represent state
 * that logically belongs here.  They will be migrated incrementally;
 * in the meantime World::syncFromGlobals() / syncToGlobals() bridge the
 * gap.
 */
#pragma once

#include <cstdint>
#include <optional>
#include <unordered_map>

#include "game/levels/level.hpp"
#include "game/levels/dungeon_common_defs.hpp"
#include "utils/attributes.h"

namespace devilution {

/** Number of regular dungeon levels (including town). Mirrors NUMLEVELS in diablo.h. */
constexpr int WorldNumLevels = 25;

/**
 * @brief Top-level container for all game-session state.
 *
 * Owns every Level that has been loaded during the current game session and
 * tracks which one the local player is currently occupying.
 */
class World {
public:
    World();
    ~World() = default;

    /** Non-copyable: owns all Level objects for the session. */
    World(const World &) = delete;
    World &operator=(const World &) = delete;

    // -------------------------------------------------------------------------
    // Level management
    // -------------------------------------------------------------------------

    // -------------------------------------------------------------------------
    // Current level
    // -------------------------------------------------------------------------

    /**
     * @brief Returns the Level the local player currently occupies.
     *
     * Always returns a valid Level; defaults to an empty town level.
     */
    [[nodiscard]] Level &currentLevel();
    [[nodiscard]] const Level &currentLevel() const;

    /**
     * @brief Sets the current level's identity to @p target.
     *
     * Reuses the existing Level object (no assets are freed or loaded).
     * All identity fields are written from the LevelId in one call,
     * replacing the old pattern of SwitchCurrentLevel(index) followed by
     * separate gendung macro assignments.
     */
    void switchLevel(LevelId target);

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /**
     * @brief Resets the world ready for a new game session.
     *
     * Clears cached levels and resets current level to empty town.
     */
    void reset();

private:
    /** The current level the local player is on. */
    std::unique_ptr<Level> currentLevel_;

    /**
     * Cache for other levels (not yet actively used, but available for future
     * caching of level state). Currently unused; kept for architecture compatibility.
     */
    std::unordered_map<LevelIndex, Level> cachedLevels_;
};

// ---------------------------------------------------------------------------
// Global instance and convenience accessor
// ---------------------------------------------------------------------------

/** @brief The single global World instance for the current game session. */
extern DVL_API_FOR_TEST World CurrentWorld;

/**
 * @brief Returns the Level the local player is currently on.
 *
 * Convenience wrapper around CurrentWorld.currentLevel().
 */
inline Level &currentLevel()
{
    return CurrentWorld.currentLevel();
}

/**
 * @brief Sets the current level's identity to @p target.
 *
 * Reuses the existing Level object (no assets are freed or loaded).
 * All identity fields are written from the LevelId in one call.
 *
 * Must be called before any gendung macro (currentLevelNumber(), levelType(), …) is
 * first read during a level transition so that currentLevel() resolves
 * to a valid Level object.
 */
inline void SwitchCurrentLevel(LevelId target)
{
    CurrentWorld.switchLevel(target);
}

} // namespace devilution

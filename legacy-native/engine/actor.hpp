/**
 * @file engine/actor.hpp
 *
 * Base struct for all in-world actors: players, monsters, and towners (NPCs).
 *
 * Provides the set of fields that are logically shared across all actor types,
 * using canonical names. Subclasses (Player, Monster, Towner) inherit this base
 * and add their own type-specific data.
 */
#pragma once

#include <cstdint>

#include "engine/animationinfo.h"
#include "engine/math/direction.hpp"

namespace devilution {

/**
 * @brief Base struct for all in-world actors (players, monsters, and towners).
 *
 * Contains fields that are shared by all actor types:
 *  - animInfo:     Current animation state used for rendering.
 *  - direction:    The direction the actor is currently facing.
 *  - lightId:      Index into the lighting system (-1 if not emitting light).
 *  - hitPoints:    Current hit points (fixed-point, lower 6 bits are fractional).
 *  - maxHitPoints: Maximum hit points (fixed-point).
 *
 * Towners do not participate in combat so their hitPoints/maxHitPoints/lightId
 * will always remain at their default values (0 / 0 / -1).
 *
 * Position is intentionally omitted from this base because Player and Monster use
 * ActorPosition (which carries future/last/old/temp tile positions for animation
 * interpolation) while Towner uses a plain Point. Subclasses expose position
 * through their own appropriately-typed fields.
 */
struct Actor {
    AnimationInfo animInfo; // Current animation state, used for sprite selection and frame advance.
    Direction direction = Direction::South; // Direction the actor is currently facing.
    int lightId = -1;  // Index into the global lights array, or -1 if this actor does not emit light.

    // Current hit points in fixed-point format (lower 6 bits are fractional).
    // Use `hitPoints >> 6` to obtain the integer part.
    int hitPoints = 0;

    // Maximum hit points in fixed-point format (lower 6 bits are fractional).
    int maxHitPoints = 0;

    // Returns true when the actor's integer hit points are zero or below.
    // Always returns false for towners (they have no life to lose).
    [[nodiscard]] bool hasNoLife() const
    {
        return hitPoints >> 6 <= 0;
    }

    Actor() = default;
    Actor(const Actor &) = default;
    Actor(Actor &&) noexcept = default;
    Actor &operator=(const Actor &) = default;
    Actor &operator=(Actor &&) noexcept = default;
    ~Actor() = default;
};

} // namespace devilution

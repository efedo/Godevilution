/**
 * @file engine/combat_actor.hpp
 *
 * Intermediate base struct for all in-world actors that participate in combat:
 * players and monsters. Towners (NPCs) inherit directly from Actor and do not
 * participate in combat, so they do not inherit from this class.
 *
 * CombatActor sits between Actor and the concrete Player / Monster structs in
 * the inheritance hierarchy, providing a place to add combat-specific fields
 * and helpers that are shared by both types without polluting the base Actor.
 */
#pragma once

#include <string_view>

#include "engine/actor.hpp"
#include "engine/actor_position.hpp"
#include "engine/gfx/clx_sprite.hpp"
#include "engine/math/displacement.hpp"
#include "engine/render/world_renderer.h"
#include "game/levels/dun_tile.hpp"

namespace devilution {

/**
 * @brief Intermediate base struct for combat-capable actors (Player and Monster).
 *
 * Inherits all fields from Actor (animInfo, direction, lightId, hitPoints,
 * maxHitPoints) and adds members common to both players and monsters:
 *  - position:            Tile position with walking-animation sub-positions.
 *  - name():              Returns the display name of the actor.
 *  - isWalking():         Returns true when the actor is mid-walk.
 *  - getRenderingOffset(): Computes the pixel offset for rendering, accounting
 *                          for walking animation.
 */
struct CombatActor : Actor {
    /** @brief World position and animation-interpolation sub-positions. */
    ActorPosition position;

    /**
     * @brief Returns the display name of this actor.
     */
    [[nodiscard]] virtual std::string_view name() const = 0;

    /**
     * @brief Returns true when the actor is currently mid-walk animation.
     */
    [[nodiscard]] virtual bool isWalking() const = 0;

    /**
     * @brief Computes the pixel rendering offset for this actor's sprite,
     *        including any walking-animation displacement.
     * @param sprite The current sprite frame being rendered.
     */
    [[nodiscard]] Displacement getRenderingOffset(const ClxSprite sprite) const
    {
        Displacement offset = { -CalculateSpriteTileCenterX(sprite.width()), 0 };
        if (isWalking())
            offset += GetOffsetForWalking(animInfo, direction);
        return offset;
    }

    CombatActor() = default;
    CombatActor(const CombatActor &) = default;
    CombatActor(CombatActor &&) noexcept = default;
    CombatActor &operator=(const CombatActor &) = default;
    CombatActor &operator=(CombatActor &&) noexcept = default;
    virtual ~CombatActor() = default;
};

} // namespace devilution

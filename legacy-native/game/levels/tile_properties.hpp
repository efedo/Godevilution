#pragma once

/**
 * @file game/levels/tile_properties.hpp
 *
 * Interface for tile properties.
 */


#include "engine/math/point.hpp"

namespace devilution {

[[nodiscard]] bool IsTileNotSolid(Point position);
[[nodiscard]] bool IsTileSolid(Point position);

// Checks the position is solid or blocked by an object
[[nodiscard]] bool IsTileWalkable(Point position, bool ignoreDoors = false);

// Checks if the position contains an object, player, monster, or solid dungeon piece
[[nodiscard]] bool IsTileOccupied(Point position);

// Checks if stepping from a given position to a neighbouring tile cuts a corner.
// If you step from A to B, both Xs need to be clear:
//
//  AX
//  XB
[[nodiscard]] bool CanStep(Point startPosition, Point destinationPosition);

} // namespace devilution

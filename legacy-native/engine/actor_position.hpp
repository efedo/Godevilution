/**
 * @file engine/actor_position.hpp
 *
 * Interface for actor position and movement.
 */


#pragma once

#include <cstdint>

#include "engine/animationinfo.h"
#include "engine/math/point.hpp"
#include "engine/math/world_tile.hpp"

namespace devilution {

struct ActorPosition {
	WorldTilePosition tile;
	WorldTilePosition future; // Future tile position. Set at start of walking animation.
	WorldTilePosition last; // Tile position of player. Set via network on player input.
	WorldTilePosition old; // Most recent position in dPlayer.
	WorldTilePosition temp; // Used for referring to position of player when finishing moving one tile (also used to define target coordinates for spells and ranged attacks)

	// Calculates the offset for the walking animation.
	DisplacementOf<int8_t> CalculateWalkingOffset(Direction dir, const AnimationInfo &animInfo) const;
	// Calculates the offset for the walking animation.
	DisplacementOf<int16_t> CalculateWalkingOffsetShifted4(Direction dir, const AnimationInfo &animInfo) const;
	// Calculates the offset for the walking animation.
	DisplacementOf<int16_t> CalculateWalkingOffsetShifted8(Direction dir, const AnimationInfo &animInfo) const;
	// Returns Pixel velocity while walking.
	DisplacementOf<int16_t> GetWalkingVelocityShifted4(Direction dir, const AnimationInfo &animInfo) const;
	// Returns Pixel velocity while walking.
	DisplacementOf<int16_t> GetWalkingVelocityShifted8(Direction dir, const AnimationInfo &animInfo) const;
};

} // namespace devilution

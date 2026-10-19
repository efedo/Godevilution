#pragma once

/**
 * @file engine/audio/sound_position.hpp
 *
 * Interface for 3D sound position calculation.
 */


#include "engine/math/point.hpp"

namespace devilution {

bool CalculateSoundPosition(Point soundPosition, int *plVolume, int *plPan);

} // namespace devilution

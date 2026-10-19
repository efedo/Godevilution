/**
 * @file engine/audio/sound_position.cpp
 *
 * Implementation of 3D sound position calculation.
 */


#include "engine/audio/sound_position.hpp"

#include "engine/audio/sound_defs.hpp"
#include "game/players/players.hpp"

namespace devilution {

bool CalculateSoundPosition(Point soundPosition, int *plVolume, int *plPan)
{
	const Point playerPosition { MyPlayer->position.tile };
	const Displacement delta = soundPosition - playerPosition;

	const int pan = (delta.deltaX - delta.deltaY) * 256;
	*plPan = std::clamp(pan, PAN_MIN, PAN_MAX);

	const int volume = playerPosition.ApproxDistance(soundPosition) * -64;

	if (volume <= ATTENUATION_MIN)
		return false;

	*plVolume = volume;

	return true;
}

} // namespace devilution

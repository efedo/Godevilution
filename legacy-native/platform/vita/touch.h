#pragma once

/**
 * @file platform/vita/touch.h
 *
 * Interface for touch.
 */


#ifdef __vita__

#include <SDL.h>

#include "engine/math/point.hpp"

namespace devilution {

void HandleTouchEvent(SDL_Event *event, Point mousePosition);
void FinishSimulatedMouseClicks(Point mousePosition);

} // namespace devilution

#endif

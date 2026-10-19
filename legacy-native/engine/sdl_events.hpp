#pragma once

/**
 * @file engine/sdl_events.hpp
 *
 * Interface for event handling.
 */


#include <cstdint>

#ifdef USE_SDL3
#include <SDL3/SDL_events.h>
#else
#include <SDL.h>

#ifdef USE_SDL1
#include "utils/sdl/sdl2_to_1_2_backports.h"
#endif
#endif

#include "engine/math/point.hpp"

namespace devilution {

using EventHandler = void (*)(const SDL_Event &event, uint16_t modState);

/** @brief The current input handler function */
extern EventHandler CurrentEventHandler;

EventHandler SetEventHandler(EventHandler NewProc);

bool FetchMessage(SDL_Event *event, uint16_t *modState);

void HandleMessage(const SDL_Event &event, uint16_t modState);

} // namespace devilution

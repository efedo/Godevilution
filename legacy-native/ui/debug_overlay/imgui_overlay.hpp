#pragma once

/**
 * @file ui/debug_overlay/imgui_overlay.hpp
 *
 * Interface for ImGui debug overlay.
 */


#ifdef USE_SDL3
#include <SDL3/SDL_events.h>
#else
#include <SDL.h>
#endif

#include <optional>

#include "engine/math/point.hpp"

namespace devilution {

bool DebugOverlayHandleEvent(const SDL_Event &event);
bool DebugOverlayIsAvailable();
bool DebugOverlayEditorOwnsPause();
const std::optional<Point> &DebugOverlaySelectedTile();
void DebugOverlayRender();
void DebugOverlayShutdown();

} // namespace devilution

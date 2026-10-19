#pragma once

/**
 * @file engine/gfx/backbuffer_state.hpp
 *
 * Interface for backbuffer state management.
 */


#include <cstdint>

#include "engine/math/rectangle.hpp"
#include "engine/gfx/surface.hpp"

namespace devilution {

enum class PanelDrawComponent {
	Health,
	Mana,
	ControlButtons,
	Belt,

	FIRST = Health,
	LAST = Belt
};

struct DrawnCursor {
	Rectangle rect;
	uint8_t behindBuffer[8192];
};

void InitBackbufferState();

void RedrawEverything();
bool IsRedrawEverything();
void RedrawViewport();
bool IsRedrawViewport();
void RedrawComplete();

void RedrawComponent(PanelDrawComponent component);
bool IsRedrawComponent(PanelDrawComponent component);
void RedrawComponentComplete(PanelDrawComponent component);

DrawnCursor &GetDrawnCursor();

} // namespace devilution

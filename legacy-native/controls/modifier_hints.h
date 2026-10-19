#pragma once

/**
 * @file controls/modifier_hints.h
 *
 * Interface for modifier key hints.
 */


#include "engine/gfx/surface.hpp"

namespace devilution {

void DrawControllerModifierHints(const Surface &out);
void InitModifierHints();
void FreeModifierHints();

} // namespace devilution

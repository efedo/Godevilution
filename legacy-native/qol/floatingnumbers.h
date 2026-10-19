/**
 * @file floatingnumbers.h
 *
 * Adds floating numbers QoL feature
 */
#pragma once

#include <cstdint>
#include <string>

#include "ui/menu/ui_flags.hpp"
#include "engine/math/displacement.hpp"
#include "engine/math/point.hpp"
#include "engine/gfx/surface.hpp"

namespace devilution {

void AddFloatingNumber(Point pos, Displacement offset, std::string text, UiFlags style, int id = 0, bool reverseDirection = false);
/** Adds a native-compatible damage number from an exact Q6 amount. */
void AddFloatingDamageNumber(Point pos, int damageQ6, int id = 0, bool reverseDirection = false);
/** Adds a native-compatible healing number from an exact Q6 amount. */
void AddFloatingHealingNumber(Point pos, int healingQ6, int id = 0);
/** Adds a native-compatible experience number. */
void AddFloatingExperienceNumber(Point pos, uint32_t experience, int id = 0);
void DrawFloatingNumbers(const Surface &out, Point viewPosition, Displacement offset);
void ClearFloatingNumbers();

} // namespace devilution

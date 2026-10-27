#pragma once

/**
 * @file engine/vision.hpp
 *
 * Interface for field-of-view and vision calculations.
 */

#include <cstdint>

#include <function_ref.hpp>

#include "engine/math/point.hpp"

namespace devilution {

void DoVision(Point position, uint8_t radius,
    tl::function_ref<void(Point)> markVisibleFn,
    tl::function_ref<void(Point)> markTransparentFn,
    tl::function_ref<bool(Point)> passesLightFn,
    tl::function_ref<bool(Point)> inBoundsFn);

/** Returns whether the target is reached by the native vision ray table. */
[[nodiscard]] bool IsPointVisible(
    Point observer,
    Point target,
    uint8_t radius,
    tl::function_ref<bool(Point)> passesLightFn,
    tl::function_ref<bool(Point)> inBoundsFn);

} // namespace devilution

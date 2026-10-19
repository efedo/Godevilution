#pragma once

/**
 * @file engine/ticks.hpp
 *
 * Interface for tick and frame timing.
 */


#include <cstdint>

namespace devilution {

uint32_t GetAnimationFrame(uint32_t frames, uint32_t fps = 60);

} // namespace devilution

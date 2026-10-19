#pragma once

/**
 * @file ui/panel/control_flasks.hpp
 *
 * Interface for flask indicators.
 */


#include <optional>

#include "engine/gfx/surface.hpp"

namespace devilution {

extern std::optional<OwnedSurface> pLifeBuff;
extern std::optional<OwnedSurface> pManaBuff;

} // namespace devilution

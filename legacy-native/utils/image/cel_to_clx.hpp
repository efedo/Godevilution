#pragma once

/**
 * @file utils/image/cel_to_clx.hpp
 *
 * Interface for CEL to CLX format conversion.
 */


#include <cstddef>
#include <cstdint>

#include "engine/gfx/clx_sprite.hpp"
#include "utils/pointer_value_union.hpp"

namespace devilution {

OwnedClxSpriteListOrSheet CelToClx(const uint8_t *data, size_t size, PointerOrValue<uint16_t> widthOrWidths);

} // namespace devilution

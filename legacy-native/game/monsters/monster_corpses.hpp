/**
 * @file game/monsters/monster_corpses.hpp
 *
 * Interface of functions for placing dead monsters.
 */
#pragma once

#include <cstddef>
#include <cstdint>

#include "engine/gfx/clx_sprite.hpp"
#include "engine/math/direction.hpp"
#include "engine/math/point.hpp"
#include "utils/attributes.h"

namespace devilution {

static constexpr unsigned MaxCorpses = 31;

struct Corpse {
	OptionalClxSpriteListOrSheet sprites;
	int frame;
	uint16_t width;
	uint8_t translationPaletteIndex;

	/**
	 * @brief Returns the sprite list for a given direction.
	 *
	 * @param direction One of the 16 directions. Valid range: [0, 15].
	 * @return ClxSpriteList
	 */
	[[nodiscard]] ClxSpriteList spritesForDirection(Direction direction) const
	{
		return sprites->isSheet() ? sprites->sheet()[static_cast<size_t>(direction)] : sprites->list();
	}
};

extern DVL_API_FOR_TEST Corpse Corpses[MaxCorpses];
extern DVL_API_FOR_TEST int8_t stonendx;

void InitCorpses();
void AddCorpse(Point tilePosition, int8_t dv, Direction ddir);
void MoveLightsToCorpses();

} // namespace devilution

/**
 * @file game/quests/quest_stars.cpp
 *
 * Implementation of the map of the stars quest.
 */
#include "game/quests/quest_stars.hpp"

#include <optional>

#include "ui/panel/control.hpp"
#include "engine/gfx/clx_sprite.hpp"
#include "engine/load/load_cel.hpp"
#include "engine/render/clx_render.hpp"
#include "engine/render/primitive_render.hpp"

namespace devilution {
namespace {
OptionalOwnedClxSpriteList DoomSprite;
} // namespace

bool DoomFlag;

void doom_init()
{
	DoomSprite = LoadCel("items\\map\\mapztown", 640);
	DoomFlag = true;
}

void doom_close()
{
	DoomFlag = false;
	DoomSprite = std::nullopt;
}

void doom_draw(const Surface &out)
{
	if (!DoomFlag) {
		return;
	}

	ClxDraw(out, GetUIRectangle().position + Displacement { 0, 352 }, (*DoomSprite)[0]);
}

} // namespace devilution

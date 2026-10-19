/**
 * @file ui/menu/scrollbar.cpp
 *
 * Implementation of scrollbar widget.
 */


#include "scrollbar.h"

#include <optional>

#include "engine/gfx/clx_sprite.hpp"
#include "engine/load/load_pcx.hpp"

namespace devilution {

OptionalOwnedClxSpriteList ArtScrollBarBackground;
OptionalOwnedClxSpriteList ArtScrollBarThumb;
OptionalOwnedClxSpriteList ArtScrollBarArrow;

void LoadScrollBar()
{
	ArtScrollBarBackground = LoadPcx("ui_art\\sb_bg");
	ArtScrollBarThumb = LoadPcx("ui_art\\sb_thumb");
	ArtScrollBarArrow = LoadPcxSpriteList("ui_art\\sb_arrow", 4);
}

void UnloadScrollBar()
{
	ArtScrollBarArrow = std::nullopt;
	ArtScrollBarThumb = std::nullopt;
	ArtScrollBarBackground = std::nullopt;
}

} // namespace devilution

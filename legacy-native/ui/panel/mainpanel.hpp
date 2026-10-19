#pragma once

/**
 * @file ui/panel/mainpanel.hpp
 *
 * Interface for main game panel.
 */


#include <string>

#include <expected.hpp>

#include "engine/gfx/clx_sprite.hpp"

namespace devilution {

extern OptionalOwnedClxSpriteList PanelButtonDown;
extern OptionalOwnedClxSpriteList TalkButton;

tl::expected<void, std::string> LoadMainPanel();
void FreeMainPanel();

} // namespace devilution

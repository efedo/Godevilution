#pragma once

/**
 * @file ui/menu/button.h
 *
 * Interface for menu button widget.
 */


#include "ui/menu/ui_item.h"
#include "engine/gfx/clx_sprite.hpp"

namespace devilution {

const Uint16 DialogButtonWidth = 110;
const Uint16 DialogButtonHeight = 28;

void LoadDialogButtonGraphics();
void FreeDialogButtonGraphics();
ClxSprite ButtonSprite(bool pressed);
void RenderButton(const UiButton &button);
bool HandleMouseEventButton(const SDL_Event &event, UiButton *button);
void HandleGlobalMouseUpButton(UiButton *button);

} // namespace devilution

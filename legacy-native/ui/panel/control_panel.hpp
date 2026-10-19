#pragma once

/**
 * @file ui/panel/control_panel.hpp
 *
 * Interface for game control panel.
 */


#include "engine/math/rectangle.hpp"
#include "ui/panel/ui_panels.hpp"

namespace devilution {

extern int TotalSpMainPanelButtons;
extern int TotalMpMainPanelButtons;
extern int PanelPaddingHeight;
extern const char *const PanBtnStr[8];
extern const char *const PanBtnHotKey[8];
extern Rectangle SpellButtonRect;
extern Rectangle BeltRect;

void SetPanelObjectPosition(UiPanels panel, Rectangle &button);

} // namespace devilution

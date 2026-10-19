#pragma once

/**
 * @file ui/menu/hero/hero_selection.h
 *
 * Interface for hero selection screen.
 */


#include <cstdint>

namespace devilution {

extern bool selhero_isMultiPlayer;
extern bool selhero_endMenu;

void selhero_Init();
void selhero_List_Init();

} // namespace devilution

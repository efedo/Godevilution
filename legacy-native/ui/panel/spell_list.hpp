#pragma once

/**
 * @file ui/panel/spell_list.hpp
 *
 * Interface for spell list.
 */


#include <cstddef>
#include <vector>

#include "engine/math/point.hpp"
#include "engine/gfx/surface.hpp"
#include "tables/spelldat.h"

namespace devilution {

struct SpellListItem {
	Point location;
	SpellType type;
	SpellID id;
	bool isSelected;
};

/**
 * @brief draws the current right mouse button spell.
 * @param out screen buffer representing the main UI panel
 */
void DrawSpell(const Surface &out);
void DrawSpellList(const Surface &out);
std::vector<SpellListItem> GetSpellListItems();
void SetSpell();
void SetSpeedSpell(size_t slot);
bool IsValidSpeedSpell(size_t slot);
void ToggleSpell(size_t slot);

/**
 * Draws the "Speed Book": the rows of known spells for quick-setting a spell that
 * show up when you click the spell slot at the control panel.
 */
void DoSpeedBook();

} // namespace devilution

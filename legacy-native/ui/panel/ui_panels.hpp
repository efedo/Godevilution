#pragma once

/**
 * @file ui/panel/ui_panels.hpp
 *
 * Interface for panel type definitions.
 */


#include <cstdint>

namespace devilution {

enum class UiPanels : uint8_t {
	Main,
	Quest,
	Character,
	Spell,
	Inventory,
	Stash,
};

} // namespace devilution

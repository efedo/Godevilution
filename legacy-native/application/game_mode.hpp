/**
 * @file application/game_mode.hpp
 *
 * Interface for game mode management.
 */


#pragma once

#include "utils/attributes.h"

namespace devilution {

extern DVL_API_FOR_TEST bool gbRunGame; // Are we in-game? If false, we're in the main menu.
extern DVL_API_FOR_TEST bool gbIsSpawn; // Indicate if we only have access to demo data
extern DVL_API_FOR_TEST bool gbIsHellfire; // Indicate if we have loaded the Hellfire expansion data
extern DVL_API_FOR_TEST bool gbVanilla; // Indicate if we want vanilla savefiles
extern bool forceHellfire; // Whether the Hellfire mode is required (forced).

} // namespace devilution

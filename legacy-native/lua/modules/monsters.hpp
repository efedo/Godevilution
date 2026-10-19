#pragma once

/**
 * @file lua/modules/monsters.hpp
 *
 * Interface for monsters.
 */


#include <sol/sol.hpp>

namespace devilution {

sol::table LuaMonstersModule(sol::state_view &lua);

} // namespace devilution

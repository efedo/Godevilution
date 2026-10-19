#pragma once

/**
 * @file lua/modules/player.hpp
 *
 * Interface for player.
 */


#include <sol/sol.hpp>

namespace devilution {

sol::table LuaPlayerModule(sol::state_view &lua);

} // namespace devilution

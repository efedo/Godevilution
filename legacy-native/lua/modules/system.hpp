#pragma once

/**
 * @file lua/modules/system.hpp
 *
 * Interface for system.
 */


#include <sol/sol.hpp>

namespace devilution {

sol::table LuaSystemModule(sol::state_view &lua);

} // namespace devilution

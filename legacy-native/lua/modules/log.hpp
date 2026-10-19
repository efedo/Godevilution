#pragma once

/**
 * @file lua/modules/log.hpp
 *
 * Interface for logging system.
 */


#include <sol/sol.hpp>

namespace devilution {

sol::table LuaLogModule(sol::state_view &lua);

} // namespace devilution

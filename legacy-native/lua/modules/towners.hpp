#pragma once

/**
 * @file lua/modules/towners.hpp
 *
 * Interface for towners.
 */


#include <sol/sol.hpp>

namespace devilution {

sol::table LuaTownersModule(sol::state_view &lua);

} // namespace devilution

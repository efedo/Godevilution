#pragma once

/**
 * @file lua/modules/floatingnumbers.hpp
 *
 * Interface for floating damage numbers.
 */


#include <sol/forward.hpp>

namespace devilution {

sol::table LuaFloatingNumbersModule(sol::state_view &lua);

} // namespace devilution

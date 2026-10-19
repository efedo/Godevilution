#pragma once

/**
 * @file lua/modules/items.hpp
 *
 * Interface for items.
 */


#include <sol/sol.hpp>

namespace devilution {

sol::table LuaItemModule(sol::state_view &lua);

} // namespace devilution

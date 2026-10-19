#pragma once
/**
 * @file lua/modules/dev/items.hpp
 *
 * Interface for items.
 */


#ifdef _DEBUG
#include <sol/sol.hpp>

namespace devilution {

sol::table LuaDevItemsModule(sol::state_view &lua);

} // namespace devilution
#endif // _DEBUG

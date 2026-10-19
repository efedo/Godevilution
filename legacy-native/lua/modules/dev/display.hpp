#pragma once
/**
 * @file lua/modules/dev/display.hpp
 *
 * Interface for display and window management.
 */


#ifdef _DEBUG
#include <sol/sol.hpp>

namespace devilution {

sol::table LuaDevDisplayModule(sol::state_view &lua);

} // namespace devilution
#endif // _DEBUG

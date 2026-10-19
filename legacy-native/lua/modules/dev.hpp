#pragma once
/**
 * @file lua/modules/dev.hpp
 *
 * Interface for dev.
 */


#ifdef _DEBUG
#include <sol/sol.hpp>

namespace devilution {

sol::table LuaDevModule(sol::state_view &lua);

} // namespace devilution
#endif // _DEBUG

#pragma once
/**
 * @file lua/modules/dev/level/map.hpp
 *
 * Interface for map.
 */


#ifdef _DEBUG
#include <sol/sol.hpp>

namespace devilution {

sol::table LuaDevLevelMapModule(sol::state_view &lua);

} // namespace devilution
#endif // _DEBUG

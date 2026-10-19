#pragma once
/**
 * @file lua/modules/dev/level.hpp
 *
 * Interface for level.
 */


#ifdef _DEBUG
#include <sol/sol.hpp>

namespace devilution {

sol::table LuaDevLevelModule(sol::state_view &lua);

} // namespace devilution
#endif // _DEBUG

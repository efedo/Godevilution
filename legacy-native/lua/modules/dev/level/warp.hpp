#pragma once
/**
 * @file lua/modules/dev/level/warp.hpp
 *
 * Interface for warp.
 */


#ifdef _DEBUG
#include <sol/sol.hpp>

namespace devilution {

sol::table LuaDevLevelWarpModule(sol::state_view &lua);

} // namespace devilution
#endif // _DEBUG

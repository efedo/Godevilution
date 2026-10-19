#pragma once
/**
 * @file lua/modules/dev/player/stats.hpp
 *
 * Interface for stats.
 */


#ifdef _DEBUG
#include <sol/sol.hpp>

namespace devilution {

sol::table LuaDevPlayerStatsModule(sol::state_view &lua);

} // namespace devilution
#endif // _DEBUG

#pragma once
/**
 * @file lua/modules/dev/player.hpp
 *
 * Interface for player.
 */


#ifdef _DEBUG

#include <sol/sol.hpp>

namespace devilution {

sol::table LuaDevPlayerModule(sol::state_view &lua);

} // namespace devilution
#endif // _DEBUG

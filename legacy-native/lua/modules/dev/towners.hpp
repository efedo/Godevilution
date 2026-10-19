#pragma once
/**
 * @file lua/modules/dev/towners.hpp
 *
 * Interface for towners.
 */


#ifdef _DEBUG
#include <sol/sol.hpp>

namespace devilution {

sol::table LuaDevTownersModule(sol::state_view &lua);

} // namespace devilution
#endif // _DEBUG

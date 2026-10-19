#pragma once
/**
 * @file lua/modules/dev/monsters.hpp
 *
 * Interface for monsters.
 */


#ifdef _DEBUG
#include <sol/sol.hpp>

namespace devilution {

sol::table LuaDevMonstersModule(sol::state_view &lua);

} // namespace devilution
#endif // _DEBUG

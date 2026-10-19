#pragma once
/**
 * @file lua/modules/dev/search.hpp
 *
 * Interface for search.
 */


#ifdef _DEBUG
#include <sol/sol.hpp>

namespace devilution {

sol::table LuaDevSearchModule(sol::state_view &lua);

} // namespace devilution
#endif // _DEBUG

#pragma once
/**
 * @file lua/modules/dev/player/spells.hpp
 *
 * Interface for spells.
 */


#ifdef _DEBUG
#include <sol/sol.hpp>

namespace devilution {

sol::table LuaDevPlayerSpellsModule(sol::state_view &lua);

} // namespace devilution
#endif // _DEBUG

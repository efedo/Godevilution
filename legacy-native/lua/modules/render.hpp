#pragma once

/**
 * @file lua/modules/render.hpp
 *
 * Interface for render.
 */


#include <sol/sol.hpp>

namespace devilution {

sol::table LuaRenderModule(sol::state_view &lua);

} // namespace devilution

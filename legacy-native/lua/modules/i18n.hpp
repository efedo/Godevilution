#pragma once

/**
 * @file lua/modules/i18n.hpp
 *
 * Interface for i18n.
 */


#include <sol/sol.hpp>

namespace devilution {

sol::table LuaI18nModule(sol::state_view &lua);

} // namespace devilution

#pragma once

/**
 * @file lua/modules/audio.hpp
 *
 * Interface for audio.
 */


#include <sol/sol.hpp>

namespace devilution {

sol::table LuaAudioModule(sol::state_view &lua);

} // namespace devilution

/**
 * @file data/mod_manager.cpp
 *
 * Active mod archive management.
 */


#include "data/mod_manager.hpp"

#include "application/game_mode.hpp"
#include "engine/assets.hpp"
#include "persistence/options.h"

namespace devilution {

ModManager CurrentModManager;

std::vector<std::string_view> ModManager::ReloadActiveMods()
{
	UnloadModArchives();
	gbIsHellfire = false;
	std::vector<std::string_view> activeMods = GetOptions().Mods.GetActiveModList();
	LoadModArchives(activeMods);

	// Hellfire is a declarative expansion archive, not a scripted mod. Keep its
	// archive activation in the data layer so a future client or server can
	// select the same content without initializing Lua.
	for (const std::string_view modname : activeMods) {
		if (modname != "Hellfire")
			continue;
		LoadHellfireArchives();
		gbIsHellfire = true;
		break;
	}

	return activeMods;
}

} // namespace devilution

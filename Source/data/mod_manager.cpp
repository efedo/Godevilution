/**
 * @file data/mod_manager.cpp
 *
 * Active mod archive management.
 */


#include "data/mod_manager.hpp"

#include "application/game_mode.hpp"
#include "data/game_data_manager.hpp"
#include "engine/audio/effects.h"
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

void ModManager::ReloadActiveModData()
{
	// Sound and table data are native data-layer responsibilities. Keeping this
	// phase separate lets legacy Lua scripts retain their existing pre-reload
	// hooks without making Lua the owner of the reload pipeline.
	effects_cleanup_sfx();
	if (gbRunGame)
		sound_init();
	else
		ui_sound_init();

	CurrentGameDataManager.Reload();
}

} // namespace devilution

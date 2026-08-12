#pragma once

/**
 * @file data/mod_manager.hpp
 *
 * Active mod archive management.
 */


#include <string_view>
#include <vector>

namespace devilution {

/**
 * Coordinates loading and unloading the archives selected in the mod options.
 *
 * Script runtimes and future server content loaders may use this manager, but
 * archive ownership and post-load data refreshes stay outside those consumers.
 */
class ModManager {
public:
	/** Unloads the current mod archives and loads the active mod selection. */
	std::vector<std::string_view> ReloadActiveMods();

	/** Reloads sound and table data after active mod scripts have run. */
	void ReloadActiveModData();
};

extern ModManager CurrentModManager;

} // namespace devilution

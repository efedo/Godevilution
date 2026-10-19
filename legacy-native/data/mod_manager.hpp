#pragma once

/**
 * @file data/mod_manager.hpp
 *
 * Active mod archive management.
 */


#include <string_view>
#include <vector>

#include <function_ref.hpp>

namespace devilution {

/**
 * Coordinates loading and unloading the archives selected in the mod options.
 *
 * Script runtimes and future server content loaders may use this manager, but
 * archive ownership and post-load data refreshes stay outside those consumers.
 */
class ModManager {
public:
	/** Registers the callback used when a mod option changes. */
	void RegisterOptionChangeHandler(tl::function_ref<void()> callback);

	/** Registers the native archive/data reload callback for non-scripted clients. */
	void RegisterNativeOptionChangeHandler();

	/** Unloads the current mod archives and loads the active mod selection. */
	std::vector<std::string_view> ReloadActiveMods();

	/** Reloads sound and table data after active mod archives are loaded. */
	void ReloadActiveModData();

	/** Reloads active mod archives and data without invoking a script runtime. */
	void ReloadActiveModsAndData();
};

extern ModManager CurrentModManager;

} // namespace devilution

#pragma once

/**
 * @file data/game_data_manager.hpp
 *
 * Ordered game-data loading.
 */


namespace devilution {

/**
 * Reloads all data tables whose contents may be overridden by active mods.
 *
 * The order is intentional: several later tables depend on enums or records
 * loaded by earlier tables. Keep this pipeline independent of any scripting
 * runtime so it can be reused by the future authoritative server.
 */
class GameDataManager {
public:
	void Reload();
};

extern GameDataManager CurrentGameDataManager;

} // namespace devilution

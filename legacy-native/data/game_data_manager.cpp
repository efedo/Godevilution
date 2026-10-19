/**
 * @file data/game_data_manager.cpp
 *
 * Ordered game-data loading.
 */


#include "data/game_data_manager.hpp"

#include "game/events/event_bus.hpp"
#include "game/quests/quests.hpp"
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
#include "network/authoritative/server_backed_runtime.hpp"
#include "network/protocol/legacy_lua_policy.hpp"
#endif
#include "tables/itemdat.h"
#include "tables/leveldat.h"
#include "tables/misdat.h"
#include "tables/monstdat.h"
#include "tables/objdat.h"
#include "tables/playerdat.hpp"
#include "tables/spelldat.h"
#include "tables/textdat.h"

namespace devilution {

GameDataManager CurrentGameDataManager;

void GameDataManager::Reload()
{
	LoadTextData();
	LoadPlayerDataFiles();
	LoadSpellData();
	LoadMissileData();
	LoadMonsterData();
	LoadItemData();
	LoadObjectData();
	LoadQuestData();
	LoadSetLevelNames();
	LoadQuestPools();
	LoadLevelGenerationData();
	bool processLegacyLuaEvents = true;
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	processLegacyLuaEvents = ShouldProcessLegacyLuaEvents(authoritative::GetServerBackedRuntime().IsAuthoritativeMode());
#endif
	if (processLegacyLuaEvents)
		CurrentGameEventBus.GameDataReloaded();
}

} // namespace devilution

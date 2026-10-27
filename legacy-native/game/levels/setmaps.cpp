/**
 * @file game/levels/setmaps.cpp
 *
 * Implementation of setmaps.
 */

#include "game/levels/setmaps.h"

#include <cstdint>

#ifdef _DEBUG
#include "application/debug.h"
#endif
#include "engine/gfx/palette.h"
#include "engine/load/load_file.hpp"
#include "game/levels/dungeon_common.h"
#include "game/levels/level_l1.h"
#include "game/levels/level_l2.h"
#include "game/levels/level_l3.h"
#include "game/levels/level_l4.h"
#include "game/levels/triggers.h"
#include "game/objects/objects.hpp"
#include "game/quests/quests.hpp"
#include "network/protocol/legacy_world_state_policy.hpp"
#include "network/protocol/msg.h"
#include "tables/objdat.h"
#include "utils/language.h"

#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
#include "network/authoritative/server_backed_runtime.hpp"
#endif

namespace devilution {

namespace {

bool LegacyLevelLoadMutationAllowed() noexcept
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	return ShouldProcessLegacyLevelLoadMutation(authoritative::GetServerBackedRuntime().IsConnected());
#else
	return true;
#endif
}

void AddSKingObjs()
{
	constexpr WorldTileRectangle SmallSecretRoom { { 20, 7 }, { 3, 3 } };
	ObjectAtPosition({ 64, 34 }).InitializeLoadedObject(SmallSecretRoom, 1);

	constexpr WorldTileRectangle Gate { { 20, 14 }, { 1, 2 } };
	ObjectAtPosition({ 64, 59 }).InitializeLoadedObject(Gate, 2);

	constexpr WorldTileRectangle LargeSecretRoom { { 8, 1 }, { 7, 10 } };
	ObjectAtPosition({ 27, 37 }).InitializeLoadedObject(LargeSecretRoom, 3);
	ObjectAtPosition({ 46, 35 }).InitializeLoadedObject(LargeSecretRoom, 3);
	ObjectAtPosition({ 49, 53 }).InitializeLoadedObject(LargeSecretRoom, 3);
	ObjectAtPosition({ 27, 53 }).InitializeLoadedObject(LargeSecretRoom, 3);
}

void AddSChamObjs()
{
	ObjectAtPosition({ 37, 30 }).InitializeLoadedObject({ { 17, 0 }, { 4, 5 } }, 1);
	ObjectAtPosition({ 37, 46 }).InitializeLoadedObject({ { 13, 0 }, { 3, 5 } }, 2);
}

void AddVileObjs()
{
	ObjectAtPosition({ 26, 45 }).InitializeLoadedObject({ { 1, 1 }, { 8, 9 } }, 1);
	ObjectAtPosition({ 45, 46 }).InitializeLoadedObject({ { 11, 1 }, { 9, 9 } }, 2);
	ObjectAtPosition({ 35, 36 }).InitializeLoadedObject({ { 7, 11 }, { 6, 7 } }, 3);
}

void SetMapTransparency(const char *path)
{
	auto dunData = LoadFileInMem<uint16_t>(path);
	LoadTransparency(dunData.get());
}

void LoadCustomMap(const char *path, Point viewPosition)
{
	switch (setLevelType()) {
	case DTYPE_CATHEDRAL:
	case DTYPE_CRYPT:
		LoadL1Dungeon(path, viewPosition);
		break;
	case DTYPE_CATACOMBS:
		LoadL2Dungeon(path, viewPosition);
		break;
	case DTYPE_CAVES:
	case DTYPE_NEST:
		LoadL3Dungeon(path, viewPosition);
		break;
	case DTYPE_HELL:
		LoadL4Dungeon(path, viewPosition);
		break;
	case DTYPE_TOWN:
	case DTYPE_NONE:
		break;
	}
	LoadRndLvlPal(setLevelType());
}

void LoadArenaMap(const char *path, Point viewPosition, Point exitTrigger)
{
	LoadCustomMap(path, viewPosition);
	trigFlag() = false;
	numTriggers() = 1;
	trigs[0].position = exitTrigger;
	trigs[0]._tmsg = WM_DIABRTNLVL;
}

} // namespace

void LoadSetMap()
{
	switch (setLevelNumber()) {
	case SL_SKELKING:
		if (LegacyLevelLoadMutationAllowed() && Quests[Q_SKELKING]._qactive == QUEST_INIT) {
			Quests[Q_SKELKING]._qactive = QUEST_ACTIVE;
			Quests[Q_SKELKING]._qvar1 = 1;
			NetSendCmdQuest(true, Quests[Q_SKELKING]);
		}
		LoadPreL1Dungeon("levels\\l1data\\sklkng1.dun");
		LoadL1Dungeon("levels\\l1data\\sklkng2.dun", { 83, 44 });
		SetMapTransparency("levels\\l1data\\sklkngt.dun");
		LoadPaletteAndInitBlending("levels\\l1data\\l1_2.pal");
		AddSKingObjs();
		InitSKingTriggers();
		break;
	case SL_BONECHAMB:
		LoadPreL2Dungeon("levels\\l2data\\bonecha2.dun");
		LoadL2Dungeon("levels\\l2data\\bonecha1.dun", { 70, 40 });
		SetMapTransparency("levels\\l2data\\bonechat.dun");
		LoadPaletteAndInitBlending("levels\\l2data\\l2_2.pal");
		AddSChamObjs();
		InitSChambTriggers();
		break;
	case SL_MAZE:
		break;
	case SL_POISONWATER:
		if (LegacyLevelLoadMutationAllowed() && Quests[Q_PWATER]._qactive == QUEST_INIT)
			Quests[Q_PWATER]._qactive = QUEST_ACTIVE;
		LoadL3Dungeon("levels\\l3data\\foulwatr.dun", { 31, 83 });
		LoadPaletteAndInitBlending("levels\\l3data\\l3pfoul.pal");
		InitPWaterTriggers();
		break;
	case SL_VILEBETRAYER:
		if (LegacyLevelLoadMutationAllowed()) {
			if (Quests[Q_BETRAYER]._qactive == QUEST_DONE) {
				Quests[Q_BETRAYER]._qvar2 = 4;
			} else if (Quests[Q_BETRAYER]._qactive == QUEST_ACTIVE) {
				Quests[Q_BETRAYER]._qvar2 = 3;
			}
		}
		LoadPreL1Dungeon("levels\\l1data\\vile1.dun");
		LoadL1Dungeon("levels\\l1data\\vile2.dun", { 35, 36 });
		SetMapTransparency("levels\\l1data\\vile1.dun");
		LoadPaletteAndInitBlending("levels\\l1data\\l1_2.pal");
		AddVileObjs();
		InitNoTriggers();
		break;
	case SL_ARENA_CHURCH:
		LoadArenaMap("arena\\church.dun", { 29, 22 }, { 28, 20 });
		break;
	case SL_ARENA_HELL:
		LoadArenaMap("arena\\hell.dun", { 34, 26 }, { 33, 26 });
		break;
	case SL_ARENA_CIRCLE_OF_LIFE:
		LoadArenaMap("arena\\circle_of_death.dun", { 30, 26 }, { 29, 26 });
		break;
	case SL_NONE:
#ifdef _DEBUG
		LoadCustomMap(TestMapPath.c_str(), viewPosition());
		InitNoTriggers();
#endif
		break;
	}
}

} // namespace devilution

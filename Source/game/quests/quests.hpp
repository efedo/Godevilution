/**
 * @file game/quests/quests.hpp
 *
 * Interface of functionality for handling quests.
 */
#pragma once

#include <array>
#include <cstdint>
#include <string>
#include <vector>

#include "engine/gfx/clx_sprite.hpp"
#include "engine/math/point.hpp"
#include "engine/gfx/surface.hpp"
#include "game/levels/dungeon_common.h"
#include "game/monsters/monsters.hpp"
#include "ui/panel/info_box.hpp"
#include "tables/objdat.h"
#include "tables/textdat.h"
#include "utils/attributes.h"

namespace devilution {

#define MAXQUESTS 24

// States of the mushroom quest
enum {
	QS_INIT,
	QS_TOMESPAWNED,
	QS_TOMEGIVEN,
	QS_MUSHSPAWNED,
	QS_MUSHPICKED,
	QS_MUSHGIVEN,
	QS_BRAINSPAWNED,
	QS_BRAINGIVEN,
};

// States of the gharbad the weak quest for multiplayer sync
enum {
	QS_GHARBAD_INIT,
	QS_GHARBAD_FIRST_ITEM_READY,
	QS_GHARBAD_FIRST_ITEM_SPAWNED,
	QS_GHARBAD_SECOND_ITEM_NEARLY_DONE,
	QS_GHARBAD_SECOND_ITEM_READY,
	QS_GHARBAD_ATTACKING,
};

// States of Zhar the Mad quest for multiplayer sync
enum {
	QS_ZHAR_INIT,
	QS_ZHAR_ITEM_SPAWNED,
	QS_ZHAR_ANGRY,
	QS_ZHAR_ATTACKING,
};

// States of the Warlord of Blood quest
enum {
	QS_WARLORD_INIT,
	QS_WARLORD_STEELTOME_READ,
	QS_WARLORD_TALKING,
	QS_WARLORD_ATTACKING, // State only added for multiplayer quests. Doesn't affect vanilla compatibility.
};

// States of Lachdanan quest for multiplayer sync
enum {
	QS_VEIL_INIT,
	QS_VEIL_EARLY_RETURN,
	QS_VEIL_ITEM_SPAWNED,
};

enum quest_state : uint8_t {
	QUEST_NOTAVAIL, // quest did not spawn this game
	QUEST_INIT,     // quest has spawned, waiting to trigger
	QUEST_ACTIVE,   // quest is currently in progress
	QUEST_DONE,     // quest log closed and finished
	QUEST_HIVE_TEASE1 = 7,
	QUEST_HIVE_TEASE2,
	QUEST_HIVE_ACTIVE,
	QUEST_HIVE_DONE,
	QUEST_INVALID = 0xFF,
};

struct Quest {
	quest_id _qidx;
	quest_state _qactive;
	uint8_t _qlevel;
	Point position;
	dungeon_type _qlvltype;
	_setlevels _qslvl;
	bool _qlog;
	_speech_id _qmsg;
	uint8_t _qvar1;
	uint8_t _qvar2;

	bool IsAvailable() const;
};

struct QuestData {
	uint8_t _qdlvl;
	int8_t _qdmultlvl;
	dungeon_type _qlvlt;
	int8_t questBookOrder;
	uint8_t _qdrnd;
	_setlevels _qslvl;
	bool isSinglePlayerOnly;
	_speech_id _qdmsg;
	std::string _qlstr;
};

extern DVL_API_FOR_TEST bool QuestLogIsOpen;
extern OptionalOwnedClxSpriteList pQLogCel;
extern DVL_API_FOR_TEST Quest Quests[MAXQUESTS];
extern Point ReturnLvlPosition;
extern dungeon_type ReturnLevelType;
extern int ReturnLevel;

void InitQuests();

// Deactivates quests from each quest pool at random to provide variety for single player games
// seed: The seed used to control which quests are deactivated
// quests: The available quest list, this function will make some of them inactive by the time it returns
void InitialiseQuestPools(uint32_t seed, Quest quests[]);
void CheckQuests();
bool ForceQuests();
void CheckQuestKill(const Monster &monster, bool sendmsg);
void DRLG_CheckQuests(Point position);
int GetMapReturnLevel();
Point GetMapReturnPosition();
void LoadPWaterPalette();
void UpdatePWaterPalette();
void ResyncMPQuests();
void ResyncQuests();
void DrawQuestLog(const Surface &out);
void StartQuestlog();
void QuestlogUp();
void QuestlogDown();
void QuestlogEnter();
void QuestlogESC();
void SetMultiQuest(int q, quest_state s, bool log, int v1, int v2, int16_t qmsg);
bool UseMultiplayerQuests();

/* rdata */
extern std::vector<QuestData> QuestsData;

/** Set level display names indexed by _setlevels enum. */
extern std::array<std::string, SL_LAST + 1> SetLevelNames;

/** Quest pools for single-player randomization. */
extern DVL_API_FOR_TEST std::vector<std::vector<quest_id>> QuestPools;

void LoadQuestData();
void LoadSetLevelNames();
void LoadQuestPools();

} // namespace devilution

/**
 * @file game/levels/triggers.h
 *
 * Interface of functionality for triggering events when the player enters an area.
 */
#pragma once

#include "engine/math/point.hpp"
#include "ui/load_screens.h"
#include "game/levels/dungeon_common.h"

namespace devilution {

#define MAXTRIGGERS 7

struct TriggerStruct {
	WorldTilePosition position;
	interface_mode _tmsg;
	int _tlvl;
};

extern DVL_API_FOR_TEST TriggerStruct trigs[MAXTRIGGERS];

void InitNoTriggers();
bool IsWarpOpen(dungeon_type type);
void InitTownTriggers();
void InitL1Triggers();
void InitL2Triggers();
void InitL3Triggers();
void InitL4Triggers();
void InitHiveTriggers();
void InitCryptTriggers();
void InitSKingTriggers();
void InitSChambTriggers();
void InitPWaterTriggers();
void InitVPTriggers();
void Freeupstairs();
void CheckTrigForce();
void CheckTriggers();

/**
 * @brief Check if the provided position is in the entrance boundary of the entrance.
 * @param entrance The entrance to check.
 * @param position The position to check against the entrance boundary.
 */
bool EntranceBoundaryContains(Point entrance, Point position);

} // namespace devilution

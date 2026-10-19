/**
 * @file game/objects/object_manager.hpp
 *
 * Object management API.
 *
 * Groups object lifecycle, lookup, processing, and interaction operations.
 */
#pragma once

#include <cstdint>
#include <string>

#include <expected.hpp>

#include "engine/math/point.hpp"
#include "engine/math/rectangle.hpp"
#include "engine/math/world_tile.hpp"
#include "tables/itemdat.h"
#include "tables/objdat.h"

namespace devilution {

struct Object;
struct Monster;
struct Player;
class ClxSprite;

namespace ObjectManager {

// Lookup

Object *FindObject(Point position, bool considerLargeObjects = true);
bool HasObject(Point position);
Object &ObjectAt(Point position);
bool IsBlockingObject(Point position);

// Lifecycle

tl::expected<void, std::string> InitializeGraphics();
void FreeGraphics();
void Initialize();
void LoadFromDungeon(const uint16_t *dunData, int startx, int starty);
Object *Spawn(_object_id objType, Point objPos);

// Level-specific spawning
// EF: migrate to level-loading code?
void SpawnLevel1Objects(int x1, int y1, int x2, int y2);
void SpawnLevel2Objects(int x1, int y1, int x2, int y2);
void SpawnLevel3Objects(int x1, int y1, int x2, int y2);
void SpawnCryptObjects(int x1, int y1, int x2, int y2);

// Processing

void Update();
void UpdateVision();
void CheckMonsterDoors(const Monster &monster);
void UpdateMap(int x1, int y1, int x2, int y2);
void UpdateMapAndResync(int x1, int y1, int x2, int y2);

// Interaction

void Operate(Player &player, Object &object);
void SyncOperate(Player &player, int cmd, Object &object);
void BreakFromMissile(Object &object);
void Break(const Player &player, Object &object);
bool UpdateTrap(Object &trap);
void ActivateTrap(Object &trap);

// Network/Delta sync

void DeltaSyncOperate(Object &object);
void DeltaSyncClose(Object &object);
void DeltaSyncBreak(Object &object);
void SyncBreak(const Player &player, Object &object);
void SyncAnimation(Object &object);
void SyncNakrulRoom();

// Utility

_item_indexes GetItemMiscIndex(item_misc_id imiscid);
void GetInfoString(const Object &object);

} // namespace ObjectManager

} // namespace devilution

/**
 * @file game/objects/object_manager.cpp
 *
 * Implementation of object management API.
 */
#include "game/objects/object_manager.hpp"

#include "game/monsters/monsters.hpp"
#include "game/objects/objects.hpp"
#include "game/players/players.hpp"

namespace devilution {
namespace ObjectManager {

// Lookup

Object *FindObject(Point position, bool considerLargeObjects)
{
	return FindObjectAtPosition(position, considerLargeObjects);
}

bool HasObject(Point position)
{
	return IsObjectAtPosition(position);
}

Object &ObjectAt(Point position)
{
	return ObjectAtPosition(position);
}

bool IsBlockingObject(Point position)
{
	return IsItemBlockingObjectAtPosition(position);
}

// Lifecycle

tl::expected<void, std::string> InitializeGraphics()
{
	return InitObjectGFX();
}

void FreeGraphics()
{
	FreeObjectGFX();
}

void Initialize()
{
	InitObjects();
}

void LoadFromDungeon(const uint16_t *dunData, int startx, int starty)
{
	SetMapObjects(dunData, startx, starty);
}

Object *Spawn(_object_id objType, Point objPos)
{
	return AddObject(objType, objPos);
}

// Level-specific spawning

void SpawnLevel1Objects(int x1, int y1, int x2, int y2)
{
	AddL1Objs(x1, y1, x2, y2);
}

void SpawnLevel2Objects(int x1, int y1, int x2, int y2)
{
	AddL2Objs(x1, y1, x2, y2);
}

void SpawnLevel3Objects(int x1, int y1, int x2, int y2)
{
	AddL3Objs(x1, y1, x2, y2);
}

void SpawnCryptObjects(int x1, int y1, int x2, int y2)
{
	AddCryptObjects(x1, y1, x2, y2);
}

// Processing

void Update()
{
	ProcessObjects();
}

void UpdateVision()
{
	RedoPlayerVision();
}

void CheckMonsterDoors(const Monster &monster)
{
	MonstCheckDoors(monster);
}

void UpdateMap(int x1, int y1, int x2, int y2)
{
	ObjChangeMap(x1, y1, x2, y2);
}

void UpdateMapAndResync(int x1, int y1, int x2, int y2)
{
	ObjChangeMapResync(x1, y1, x2, y2);
}

// Interaction

void Operate(Player &player, Object &object)
{
	OperateObject(player, object);
}

void SyncOperate(Player &player, int cmd, Object &object)
{
	SyncOpObject(player, cmd, object);
}

void BreakFromMissile(Object &object)
{
	BreakObjectMissile(object);
}

void Break(const Player &player, Object &object)
{
	BreakObject(player, object);
}

bool UpdateTrap(Object &trap)
{
	return UpdateTrapState(trap);
}

void ActivateTrap(Object &trap)
{
	OperateTrap(trap);
}

// Network/Delta sync

void DeltaSyncOperate(Object &object)
{
	DeltaSyncOpObject(object);
}

void DeltaSyncClose(Object &object)
{
	DeltaSyncCloseObj(object);
}

void DeltaSyncBreak(Object &object)
{
	DeltaSyncBreakObj(object);
}

void SyncBreak(const Player &player, Object &object)
{
	SyncBreakObj(player, object);
}

void SyncAnimation(Object &object)
{
	SyncObjectAnim(object);
}

void SyncNakrulRoom()
{
	devilution::SyncNakrulRoom();
}

// Utility

_item_indexes GetItemMiscIndex(item_misc_id imiscid)
{
	return ItemMiscIdIdx(imiscid);
}

void GetInfoString(const Object &object)
{
	GetObjectStr(object);
}

} // namespace ObjectManager
} // namespace devilution

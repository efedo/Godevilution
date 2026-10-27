/**
 * @file game/objects/objects.hpp
 *
 * Interface of object functionality, interaction, spawning, loading, etc.
 *
 * MODERNIZATION STRATEGY:
 * This interface is gradually being refactored through a facade pattern (see ObjectManager).
 * New code should prefer the cleaner ObjectManager API; legacy callers continue to work.
 * Direct use of these functions is acceptable but discouraged for new features.
 * When migrating call sites, batch mechanical changes first, then clean up internals in a follow-up pass.
 *
 * CONTAINER MODERNIZATION:
 * A modern, type-safe DenseEntityPool<Object> is available via game/objects/object_pool.hpp.
 * Legacy Objects[]/AvailableObjects[]/ActiveObjects[] globals remain unchanged for compatibility.
 * The pool can be used for new hot-path optimizations or for incremental migration.
 * See docs/ENTITY_POOL_MODERNIZATION.md for strategy and design rationale.
 */
#pragma once

#include <array>
#include <cmath>
#include <cstdint>
#include <string>

#include <expected.hpp>

#include "engine/cursor.h"
#include "engine/gfx/clx_sprite.hpp"
#include "engine/math/point.hpp"
#include "engine/math/rectangle.hpp"
#include "engine/math/world_tile.hpp"
#include "game/levels/dun_tile.hpp"
#include "game/monsters/monsters.hpp"
#include "tables/itemdat.h"
#include "tables/objdat.h"
#include "tables/textdat.h"
#include "utils/attributes.h"
#include "utils/is_of.hpp"
#include "utils/string/string_or_view.hpp"

namespace devilution {

#define MAXOBJECTS 127

struct Object {
	_object_id _otype = OBJ_NULL;
	bool applyLighting = false;
	bool _oTrapFlag = false;
	bool _oDoorFlag = false;

	Point position;
	uint32_t _oAnimFlag = 0;
	OptionalClxSpriteList _oAnimData;
	int _oAnimDelay = 0;      // Tick length of each frame in the current animation
	int _oAnimCnt = 0;        // Increases by one each game tick, counting how close we are to _pAnimDelay
	uint32_t _oAnimLen = 0;   // Number of frames in current animation
	uint32_t _oAnimFrame = 0; // Current frame of animation.

	// TODO: Remove this field, it is unused and always equal to:
	// (*_oAnimData)[0].width()
	uint16_t _oAnimWidth = 0;

	bool _oDelFlag = false;
	int8_t _oBreak = 0;
	bool _oSolidFlag = false;
	bool _oMissFlag = false; // True if the object allows missiles to pass through, false if it collides with missiles
	SelectionRegion selectionRegion = SelectionRegion::None;
	bool _oPreFlag = false;
	int _olid = 0;

	// Saves the absolute value of the engine state (typically from a call to AdvanceRndSeed()) to later use when spawning items from a container object
	// This is an unsigned value to avoid implementation-defined behavior when reading from this variable.
	uint32_t _oRndSeed = 0;
	int _oVar1 = 0;
	int _oVar2 = 0;
	int _oVar3 = 0;
	int _oVar4 = 0;
	int _oVar5 = 0;
	uint32_t _oVar6 = 0;
	int _oVar8 = 0;

	// ID of a quest message to play when this object is activated.
	// Used by spell book objects which trigger quest progress for Halls of the Blind, Valor, or Warlord of Blood
	_speech_id bookMessage = TEXT_NONE;

	// Returns the static data for this object type
	[[nodiscard]] const ObjectData &data() const
	{
		return AllObjects[static_cast<size_t>(_otype)];
	}

	// Returns the network identifier for this object
	// This is currently the index into the Objects array, but may change in the future.
	[[nodiscard]] unsigned int GetId() const;

	// Marks the map region to be refreshed when the player interacts with the object.
	// Some objects will cause a map region to change when a player interacts with them (e.g. Skeleton King
	// antechamber levers). The coordinates used for this region are based on a 40*40 grid overlaying the central
	// 80*80 region of the dungeon.
	// topLeftPosition: corner of the map region closest to the origin
	// bottomRightPosition: corner of the map region furthest from the origin
	void SetMapRange(WorldTilePosition topLeftPosition, WorldTilePosition bottomRightPosition);

	// Convenience function for SetMapRange(Point, Point).
	// mapRange: A rectangle defining the top left corner and size of the affected region.
	void SetMapRange(WorldTileRectangle mapRange);

	// Sets up a generic quest book which will trigger a change in the map when activated.
	// Books of this type use a generic message (see OperateSChambBook()) compared to the more specific quest books
	// initialized by InitializeQuestBook().
	// mapRange: The region to be updated when this object is activated.
	void InitializeBook(WorldTileRectangle mapRange);

	// Initializes this object as a quest book which will cause further changes and play a message when activated.
	// mapRange: The region to be updated when this object is activated.
	// leverID: An ID (distinct from the object index) to identify the new objects spawned after updating the map.
	// message: The quest text to play when this object is activated.
	void InitializeQuestBook(WorldTileRectangle mapRange, int leverID, _speech_id message);

	// Marks an object which was spawned from a sublevel in response to a lever activation.
	// mapRange: The region which was updated to spawn this object.
	// leverID: The ID (*not* an object ID/index) of the lever responsible for the map change.
	void InitializeLoadedObject(WorldTileRectangle mapRange, int leverID);

	[[nodiscard]] bool IsBreakable() const; // Is breakable (i.e. is an intact barrel or crux)?
	[[nodiscard]] bool IsBroken() const;    // Is broken?
	[[nodiscard]] bool IsDisabled() const;  // Returns true if the object is a harmful shrine and
	                                        // the player has disabled permanent shrine effects.
	[[nodiscard]] bool canInteractWith() const;
	[[nodiscard]] bool IsBarrel() const;    // Is barrel (or explosive barrel)?
	[[nodiscard]] bool isExplosive() const; // Contains explosives or caustic material?

	// Check if this object is any of the chest (or trapped chest) types (see _object_id).
	// Trapped chests get their base type change in addition to having the trap flag set, but if they get "refilled" by
	// a Thaumaturgic shrine the base type is not reverted. This means you need to consider both the base type and the
	// trap flag to differentiate between chests that are currently trapped and chests which have never been trapped.
	[[nodiscard]] bool IsChest() const;

	[[nodiscard]] bool IsTrappedChest() const;   // Is a trapped chest (i.e. has an active trap)?
	[[nodiscard]] bool IsUntrappedChest() const; // Is an untrapped chest (i.e. has no active trap)?
	[[nodiscard]] bool IsCrux() const;           // Is a crucifix?
	[[nodiscard]] bool isDoor() const;           // Is a door?
	[[nodiscard]] bool IsShrine() const;         // Is a shrine?
	[[nodiscard]] bool IsTrap() const;           // Is a trap?

	[[nodiscard]] StringOrView name() const; // Name of the object as shown in the info box

	[[nodiscard]] ClxSprite currentSprite() const;

	[[nodiscard]] Displacement getRenderingOffset(const ClxSprite sprite, Point tilePosition) const;
};

extern DVL_API_FOR_TEST Object *Objects;
extern DVL_API_FOR_TEST int *AvailableObjects;
extern DVL_API_FOR_TEST int *ActiveObjects;
extern DVL_API_FOR_TEST int &ActiveObjectCount;
extern DVL_API_FOR_TEST bool LoadingMapObjects; // Indicates that objects are being loaded during gameplay and pre-calculated data should be updated
extern int NaKrulTomeSequence;                  // Tracks progress through the tome sequence that spawns Na-Krul (see OperateNakrulBook())

// Find an object given a point in map coordinates
// considerLargeObjects: Default behavior will return a pointer to a large object that covers this tile; set
//                             this parameter to false if you only want the object whose base position matches this tile
// return: A pointer to the object or nullptr if no object exists at this location
Object *FindObjectAtPosition(Point position, bool considerLargeObjects = true);

// Check whether an object occupies this tile position
bool IsObjectAtPosition(Point position);

// Get a reference to the object located at this tile
// N.b. This function is unchecked. Trying to access an invalid position will result in out of bounds memory access
Object &ObjectAtPosition(Point position);

// Check whether an item blocking object (solid object or open door) is located at this tile position
// return: true if the tile is blocked
bool IsItemBlockingObjectAtPosition(Point position);

tl::expected<void, std::string> InitObjectGFX();
void FreeObjectGFX();
[[nodiscard]] bool IsObjectGraphicLoaded(object_graphic_id graphic);
void AddL1Objs(int x1, int y1, int x2, int y2);
void AddL2Objs(int x1, int y1, int x2, int y2);
void AddL3Objs(int x1, int y1, int x2, int y2);
void AddCryptObjects(int x1, int y1, int x2, int y2);
void InitObjects();
void SetMapObjects(const uint16_t *dunData, int startx, int starty);
Object *AddObject(_object_id objType, Point objPos); // Spawns an object of the given type at the map coordinates provided
/** Adds only the native presentation state for an authoritative object. */
Object *AddObjectPresentation(_object_id objType, Point objPos);
bool UpdateTrapState(Object &trap);
void OperateTrap(Object &trap);
void ProcessObjects();
void RedoPlayerVision();
void MonstCheckDoors(const Monster &monster);
void ObjChangeMap(int x1, int y1, int x2, int y2);
void ObjChangeMapResync(int x1, int y1, int x2, int y2);
_item_indexes ItemMiscIdIdx(item_misc_id imiscid);
void OperateObject(Player &player, Object &object);
void SyncOpObject(Player &player, int cmd, Object &object);
void BreakObjectMissile(Object &object);
void BreakObject(const Player &player, Object &object);
void DeltaSyncOpObject(Object &object);
void DeltaSyncCloseObj(Object &object);
void DeltaSyncBreakObj(Object &object);
void SyncBreakObj(const Player &player, Object &object);
void SyncObjectAnim(Object &object);
void GetObjectStr(const Object &object); // Updates the text drawn in the info box to describe the given object
void SyncNakrulRoom();
#ifdef BUILD_TESTING
void TestObjSetMicro(Point position, int pn);
/** Actual original-Diablo single-player shrine admission metadata, without selection. */
std::array<bool, 26> SinglePlayerShrineAvailabilityForTesting();
#endif

} // namespace devilution

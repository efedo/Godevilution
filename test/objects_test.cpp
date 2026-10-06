/**
 * @file objects_test.cpp
 *
 * Tests for the object subsystem (objects.cpp / objects.h).
 *
 * Coverage:
 *  - Object predicate methods (IsChest, IsUntrappedChest, etc.)
 *  - AllObjects table bounds vs. OBJ_LAST enum
 *  - AddObjTraps: OBJ_NULL-typed object does not crash (regression for OOB vector access)
 *  - UpdateTrapState: state-machine branch coverage for all trigger types
 *  - Native ACTION_DISARM path: Dexterity/level rolls and linked trap/chest flags
 */

#include <gtest/gtest.h>

#include <array>
#include <cstdint>
#include <optional>
#include <stdexcept>
#include <vector>

#include "engine/assets.hpp"
#include "engine/random.hpp"
#include "game/levels/dungeon_common.h"
#include "game/levels/level.hpp"
#include "game/objects/object_pool.hpp"
#include "game/objects/objects.hpp"
#include "game/players/players.hpp"
#include "engine/cursor.h"
#include "engine/lighting.h"
#include "tables/objdat.h"
#include "utils/paths.h"

using namespace devilution;

namespace devilution {
void TestPlayerCheckNewPath(Player &player);
}

namespace {

void SetTestAssetsPath()
{
	const std::string assetsPath = paths::BasePath() + "/assets/";
	paths::SetAssetsPath(assetsPath);
}

// ============================================================================
// Object predicate method tests (no assets / level required)
// ============================================================================

TEST(ObjectPredicates, IsUntrappedChest)
{
	Object obj;

	for (const _object_id otype : { OBJ_CHEST1, OBJ_CHEST2, OBJ_CHEST3 }) {
		obj._otype = otype;
		obj._oTrapFlag = false;
		EXPECT_TRUE(obj.IsUntrappedChest()) << "Untrapped chest should be detected for otype " << static_cast<int>(otype);

		obj._oTrapFlag = true;
		EXPECT_FALSE(obj.IsUntrappedChest()) << "Trapped chest should not be IsUntrappedChest for otype " << static_cast<int>(otype);
	}

	obj._otype = OBJ_BARREL;
	obj._oTrapFlag = false;
	EXPECT_FALSE(obj.IsUntrappedChest()) << "Non-chest object should not be IsUntrappedChest";
}

TEST(ObjectPredicates, IsTrappedChest)
{
	Object obj;

	for (const _object_id otype : { OBJ_TCHEST1, OBJ_TCHEST2, OBJ_TCHEST3 }) {
		obj._otype = otype;
		obj._oTrapFlag = true;
		EXPECT_TRUE(obj.IsTrappedChest()) << "Trapped chest should be detected for otype " << static_cast<int>(otype);

		obj._oTrapFlag = false;
		EXPECT_FALSE(obj.IsTrappedChest()) << "Chest without trap flag should not be IsTrappedChest for otype " << static_cast<int>(otype);
	}

	obj._otype = OBJ_CHEST1;
	obj._oTrapFlag = true;
	EXPECT_FALSE(obj.IsTrappedChest()) << "OBJ_CHEST1 (not TCHEST) should not be IsTrappedChest";
}

TEST(ObjectPredicates, IsChest)
{
	Object obj;

	for (const _object_id otype : { OBJ_CHEST1, OBJ_CHEST2, OBJ_CHEST3, OBJ_TCHEST1, OBJ_TCHEST2, OBJ_TCHEST3 }) {
		obj._otype = otype;
		EXPECT_TRUE(obj.IsChest()) << "Expected IsChest true for otype " << static_cast<int>(otype);
	}

	for (const _object_id otype : { OBJ_BARREL, OBJ_LEVER, OBJ_L1LDOOR, OBJ_NULL }) {
		obj._otype = otype;
		EXPECT_FALSE(obj.IsChest()) << "Expected IsChest false for otype " << static_cast<int>(otype);
	}
}

TEST(ObjectPredicates, IsBarrel)
{
	Object obj;

	for (const _object_id otype : { OBJ_BARREL, OBJ_BARRELEX, OBJ_POD, OBJ_PODEX, OBJ_URN, OBJ_URNEX }) {
		obj._otype = otype;
		EXPECT_TRUE(obj.IsBarrel()) << "Expected IsBarrel true for otype " << static_cast<int>(otype);
	}

	obj._otype = OBJ_CHEST1;
	EXPECT_FALSE(obj.IsBarrel());
}

TEST(ObjectPredicates, IsDoor)
{
	Object obj;

	for (const _object_id otype : { OBJ_L1LDOOR, OBJ_L1RDOOR, OBJ_L2LDOOR, OBJ_L2RDOOR,
			 OBJ_L3LDOOR, OBJ_L3RDOOR, OBJ_L5LDOOR, OBJ_L5RDOOR }) {
		obj._otype = otype;
		EXPECT_TRUE(obj.isDoor()) << "Expected isDoor true for otype " << static_cast<int>(otype);
	}

	obj._otype = OBJ_CHEST1;
	EXPECT_FALSE(obj.isDoor());

	obj._otype = OBJ_NULL;
	EXPECT_FALSE(obj.isDoor());
}

TEST(ObjectPredicates, IsTrap)
{
	Object obj;

	for (const _object_id otype : { OBJ_TRAPL, OBJ_TRAPR }) {
		obj._otype = otype;
		EXPECT_TRUE(obj.IsTrap()) << "Expected IsTrap true for otype " << static_cast<int>(otype);
	}

	obj._otype = OBJ_CHEST1;
	EXPECT_FALSE(obj.IsTrap());
}

// ============================================================================
// AllObjects table coverage (requires assets)
// ============================================================================

class ObjectDataTableTest : public ::testing::Test {
protected:
	static void SetUpTestSuite()
	{
		SetTestAssetsPath();
		LoadObjectData();
	}
};

TEST_F(ObjectDataTableTest, TableSizeCoversAllObjectIds)
{
	// Every valid _object_id in [0, OBJ_LAST] must have a row in AllObjects.
	// If this fails, AllObjects[someValidOtype] is an OOB access throughout the engine.
	ASSERT_GE(AllObjects.size(), static_cast<size_t>(OBJ_LAST) + 1)
		<< "AllObjects has fewer entries than OBJ_LAST requires";
}

TEST_F(ObjectDataTableTest, TrapFlagMarksObjectsThatCanTriggerTraps)
{
	// The Trap flag in AllObjects marks objects that *can serve as trap triggers*
	// (doors, levers, chests). AddObjTraps uses this flag to find trigger candidates,
	// then places OBJ_TRAPL / OBJ_TRAPR near them. The trap projectile objects
	// themselves do NOT carry this flag.
	EXPECT_FALSE(AllObjects[OBJ_TRAPL].isTrap()) << "OBJ_TRAPL is the trap itself, not a trigger";
	EXPECT_FALSE(AllObjects[OBJ_TRAPR].isTrap()) << "OBJ_TRAPR is the trap itself, not a trigger";

	// Trigger-capable objects must carry the flag so AddObjTraps can find them.
	EXPECT_TRUE(AllObjects[OBJ_LEVER].isTrap())   << "OBJ_LEVER should be a valid trap trigger";
	EXPECT_TRUE(AllObjects[OBJ_CHEST1].isTrap())  << "OBJ_CHEST1 should be a valid trap trigger";
	EXPECT_TRUE(AllObjects[OBJ_L1LDOOR].isTrap()) << "OBJ_L1LDOOR should be a valid trap trigger";

	// A barrel is never a trap trigger.
	EXPECT_FALSE(AllObjects[OBJ_BARREL].isTrap()) << "OBJ_BARREL should not be a trap trigger";
}

// ============================================================================
// AddObjTraps regression (requires level + assets)
// ============================================================================

class ObjectsLevelTest : public ::testing::Test {
protected:
	static void SetUpTestSuite()
	{
		SetTestAssetsPath();
		LoadObjectData();
	}

	void SetUp() override
	{
		currentLevel().setId(Level::create(LevelId { .levelNum = 1, .type = DTYPE_CATHEDRAL }).id());
		InitializeObjectPool();
		ObjectPoolAdapter::ResetLegacyObjectPools();
		// Clear all tile object references so FindObjectAtPosition returns nullptr everywhere.
		for (Tile &tile : tiles().columnMajor())
			tile.setObject(0);
	}
};

// Exercise ACTION_DISARM through the existing test bridge. No-op object metadata
// keeps the follow-on OperateObject call from changing the fixture.
class ScopedTryDisarmState {
public:
	ScopedTryDisarmState()
		: previousLevelId_(currentLevel().id())
		, previousObjectPool_(gObjectPool)
		, noOpObjectData_(static_cast<size_t>(OBJ_LAST) + 1, ObjectData {})
		, fixturePlayers_(2)
		, previousMyPlayer_(MyPlayer)
		, previousMyPlayerId_(MyPlayerId)
		, previousLightManager_(CurrentLightManager)
		, previousRandomSeed_(GetLCGEngineState())
	{
		size_t index = 0;
		for (const Tile &tile : currentLevel().tiles().columnMajor())
			previousTileObjectIds_[index++] = tile.object();

		currentLevel().setId(LevelId { .levelNum = 1, .type = DTYPE_CATHEDRAL });
		InitializeObjectPool();
		ObjectPoolAdapter::ResetLegacyObjectPools();
		AllObjects.swap(noOpObjectData_);
		Players.swap(fixturePlayers_);
		MyPlayer = &Players[0];
		MyPlayerId = 0;
		CurrentLightManager = LightManager {};
		for (Tile &tile : currentLevel().tiles().columnMajor())
			tile.setObject(0);
	}

	ScopedTryDisarmState(const ScopedTryDisarmState &) = delete;
	ScopedTryDisarmState &operator=(const ScopedTryDisarmState &) = delete;

	~ScopedTryDisarmState()
	{
		gObjectPool = previousObjectPool_;
		AllObjects.swap(noOpObjectData_);
		currentLevel().setId(previousLevelId_);

		size_t index = 0;
		for (Tile &tile : currentLevel().tiles().columnMajor())
			tile.setObject(previousTileObjectIds_[index++]);

		Players.swap(fixturePlayers_);
		MyPlayer = previousMyPlayer_;
		MyPlayerId = previousMyPlayerId_;
		CurrentLightManager = previousLightManager_;
		SetRndSeed(previousRandomSeed_);
	}

private:
	LevelId previousLevelId_;
	ObjectPool previousObjectPool_;
	std::vector<ObjectData> noOpObjectData_;
	std::vector<Player> fixturePlayers_;
	Player *previousMyPlayer_;
	uint8_t previousMyPlayerId_;
	LightManager previousLightManager_;
	uint32_t previousRandomSeed_;
	std::array<int8_t, MAXDUNX * MAXDUNY> previousTileObjectIds_ = {};
};

class TryDisarmTest : public ::testing::Test {
protected:
	void SetUp() override
	{
		state_.emplace();
	}

	Object &PlaceObject(_object_id objectType, Point position)
	{
		const int objectIndex = ObjectPoolAdapter::ReserveObjectSlot();
		if (objectIndex < 0 || objectIndex >= MAXOBJECTS)
			throw std::runtime_error("TryDisarm test object pool unexpectedly exhausted");
		ObjectPoolAdapter::CommitReservedObjectSlot();

		Object &object = Objects[objectIndex];
		object = {};
		object._otype = objectType;
		object.position = position;
		currentLevel().tileAt(position).setObject(static_cast<int8_t>(objectIndex + 1));
		return object;
	}

	void LinkTrapTo(Object &trap, Point triggerPosition)
	{
		trap._oVar1 = triggerPosition.x;
		trap._oVar2 = triggerPosition.y;
	}

	void DisarmThroughNativeAction(Player &player, Object &object)
	{
		player.lightId = NO_LIGHT;
		player.clearPath();
		player._pmode = PM_STAND;
		const Point adjacentPosition { object.position.x - 1, object.position.y };
		player.position.tile = adjacentPosition;
		player.position.future = adjacentPosition;
		player.position.old = adjacentPosition;
		player.destAction = ACTION_DISARM;
		player.destParam1 = static_cast<int>(&object - Objects);
		player.destParam2 = 0;
		player.destParam3 = 0;
		player.destParam4 = 0;
		TestPlayerCheckNewPath(player);
	}

	std::optional<ScopedTryDisarmState> state_;
};

TEST_F(TryDisarmTest, ActivatesOnlyLinkedLeftAndRightTrapsAndConsumesOneDraw)
{
	Player &player = Players[1];
	ASSERT_EQ(&player, &Players[1]);
	ASSERT_EQ(player.getId(), uint8_t { 1 });
	ASSERT_NE(&player, MyPlayer);
	player.attributes.dexterity.current = 26;

	constexpr Point targetPosition { 20, 20 };
	constexpr Point otherTargetPosition { 30, 30 };
	Object &target = PlaceObject(OBJ_L1LDOOR, targetPosition);
	target._oTrapFlag = true;
	target._oDoorFlag = false;
	target._oVar4 = 41;
	Object &otherTarget = PlaceObject(OBJ_L1RDOOR, otherTargetPosition);
	otherTarget._oTrapFlag = true;
	otherTarget._oDoorFlag = true;
	otherTarget._oVar4 = 42;

	Object &leftTrap = PlaceObject(OBJ_TRAPL, { 18, 20 });
	Object &rightTrap = PlaceObject(OBJ_TRAPR, { 20, 18 });
	Object &unmatchedTrap = PlaceObject(OBJ_TRAPL, { 28, 30 });
	LinkTrapTo(leftTrap, targetPosition);
	LinkTrapTo(rightTrap, targetPosition);
	LinkTrapTo(unmatchedTrap, otherTargetPosition);
	unmatchedTrap._oVar4 = 9;

	SetRndSeed(1); // The native LCG's next 100-point draw is 46.
	DisarmThroughNativeAction(player, target);

	EXPECT_FALSE(target._oTrapFlag);
	EXPECT_FALSE(target._oDoorFlag);
	EXPECT_EQ(target._oVar4, 41);
	EXPECT_EQ(leftTrap._oVar4, 1);
	EXPECT_EQ(rightTrap._oVar4, 1);
	EXPECT_EQ(unmatchedTrap._oVar4, 9);
	EXPECT_TRUE(otherTarget._oTrapFlag);
	EXPECT_TRUE(otherTarget._oDoorFlag);
	EXPECT_EQ(otherTarget._oVar4, 42);
	EXPECT_EQ(GetLCGEngineState(), 0x015A4E36U);
}

TEST_F(TryDisarmTest, FailedRollPreservesLinkedTriggerAndTrap)
{
	Player &player = Players[1];
	ASSERT_EQ(&player, &Players[1]);
	ASSERT_EQ(player.getId(), uint8_t { 1 });
	ASSERT_NE(&player, MyPlayer);
	player.attributes.dexterity.current = 25; // Level 1 threshold is 45.

	constexpr Point targetPosition { 20, 20 };
	Object &target = PlaceObject(OBJ_L1LDOOR, targetPosition);
	target._oTrapFlag = true;
	target._oVar4 = 51;
	Object &trap = PlaceObject(OBJ_TRAPR, { 20, 18 });
	LinkTrapTo(trap, targetPosition);

	SetRndSeed(1); // Draw 46 exceeds the threshold.
	DisarmThroughNativeAction(player, target);

	EXPECT_TRUE(target._oTrapFlag);
	EXPECT_EQ(target._oVar4, 51);
	EXPECT_EQ(trap._oVar4, 0);
	EXPECT_EQ(GetLCGEngineState(), 0x015A4E36U);
}

TEST_F(TryDisarmTest, EqualityRollSucceedsAtLevelTwo)
{
	currentLevel().setId(LevelId { .levelNum = 2, .type = DTYPE_CATHEDRAL });
	Player &player = Players[1];
	ASSERT_EQ(&player, &Players[1]);
	ASSERT_EQ(player.getId(), uint8_t { 1 });
	ASSERT_NE(&player, MyPlayer);
	player.attributes.dexterity.current = 28; // Level 2 threshold is exactly 46.

	constexpr Point targetPosition { 20, 20 };
	Object &target = PlaceObject(OBJ_L1RDOOR, targetPosition);
	target._oTrapFlag = true;
	Object &trap = PlaceObject(OBJ_TRAPL, { 18, 20 });
	LinkTrapTo(trap, targetPosition);

	SetRndSeed(1);
	DisarmThroughNativeAction(player, target);

	EXPECT_FALSE(target._oTrapFlag);
	EXPECT_EQ(trap._oVar4, 1);
	EXPECT_EQ(GetLCGEngineState(), 0x015A4E36U);
}

TEST_F(TryDisarmTest, HighThresholdAcceptsAndNegativeThresholdUsesSignedRandomResult)
{
	Player &player = Players[1];
	ASSERT_EQ(&player, &Players[1]);
	ASSERT_EQ(player.getId(), uint8_t { 1 });
	ASSERT_NE(&player, MyPlayer);
	player.attributes.dexterity.current = 53; // Level 1 threshold is 101, without clamping.

	constexpr Point highThresholdPosition { 20, 20 };
	Object &highThresholdTarget = PlaceObject(OBJ_L1LDOOR, highThresholdPosition);
	highThresholdTarget._oTrapFlag = true;
	Object &highThresholdTrap = PlaceObject(OBJ_TRAPL, { 18, 20 });
	LinkTrapTo(highThresholdTrap, highThresholdPosition);
	SetRndSeed(1);
	DisarmThroughNativeAction(player, highThresholdTarget);
	EXPECT_FALSE(highThresholdTarget._oTrapFlag);
	EXPECT_EQ(highThresholdTrap._oVar4, 1);
	EXPECT_EQ(GetLCGEngineState(), 0x015A4E36U);

	player.attributes.dexterity.current = 0; // Level 1 threshold is -5.
	constexpr Point negativeThresholdPosition { 40, 20 };
	Object &negativeThresholdTarget = PlaceObject(OBJ_L1LDOOR, negativeThresholdPosition);
	negativeThresholdTarget._oTrapFlag = true;
	Object &negativeThresholdTrap = PlaceObject(OBJ_TRAPR, { 40, 18 });
	LinkTrapTo(negativeThresholdTrap, negativeThresholdPosition);
	SetRndSeed(0); // Ordinary draw 0 exceeds -5.
	DisarmThroughNativeAction(player, negativeThresholdTarget);
	EXPECT_TRUE(negativeThresholdTarget._oTrapFlag);
	EXPECT_EQ(negativeThresholdTrap._oVar4, 0);
	EXPECT_EQ(GetLCGEngineState(), 1U);

	SetRndSeed(1457187811U); // Existing RNG coverage pins this seed to the signed -68 result.
	DisarmThroughNativeAction(player, negativeThresholdTarget);
	EXPECT_FALSE(negativeThresholdTarget._oTrapFlag);
	EXPECT_EQ(negativeThresholdTrap._oVar4, 1);
	EXPECT_EQ(GetLCGEngineState(), 0x80000000U);
}

TEST_F(TryDisarmTest, UnlinkedDoorStaysArmedWhileTrappedChestFallbackClears)
{
	Player &player = Players[1];
	ASSERT_EQ(&player, &Players[1]);
	ASSERT_EQ(player.getId(), uint8_t { 1 });
	ASSERT_NE(&player, MyPlayer);
	player.attributes.dexterity.current = 53;

	Object &unlinkedDoor = PlaceObject(OBJ_L1LDOOR, { 20, 20 });
	unlinkedDoor._oTrapFlag = true;
	unlinkedDoor._oDoorFlag = false;
	unlinkedDoor._oVar4 = 61;
	SetRndSeed(1);
	DisarmThroughNativeAction(player, unlinkedDoor);
	EXPECT_TRUE(unlinkedDoor._oTrapFlag);
	EXPECT_FALSE(unlinkedDoor._oDoorFlag);
	EXPECT_EQ(unlinkedDoor._oVar4, 61);
	EXPECT_EQ(GetLCGEngineState(), 0x015A4E36U);

	Object &trappedChest = PlaceObject(OBJ_TCHEST1, { 30, 20 });
	trappedChest._oTrapFlag = true;
	SetRndSeed(1);
	DisarmThroughNativeAction(player, trappedChest);
	EXPECT_FALSE(trappedChest._oTrapFlag);
	EXPECT_EQ(GetLCGEngineState(), 0x015A4E36U);

	Object &ordinaryChest = PlaceObject(OBJ_CHEST1, { 40, 20 });
	ordinaryChest._oTrapFlag = true;
	SetRndSeed(1);
	DisarmThroughNativeAction(player, ordinaryChest);
	EXPECT_TRUE(ordinaryChest._oTrapFlag);
	EXPECT_FALSE(ordinaryChest.IsTrappedChest());
	EXPECT_EQ(GetLCGEngineState(), 0x015A4E36U);
}

TEST_F(TryDisarmTest, AlreadyUnarmedObjectDoesNotConsumeRandomness)
{
	Player &player = Players[1];
	ASSERT_EQ(&player, &Players[1]);
	ASSERT_EQ(player.getId(), uint8_t { 1 });
	ASSERT_NE(&player, MyPlayer);
	player.attributes.dexterity.current = 53;

	Object &target = PlaceObject(OBJ_L1LDOOR, { 20, 20 });
	Object &trap = PlaceObject(OBJ_TRAPL, { 18, 20 });
	LinkTrapTo(trap, { 20, 20 });
	SetRndSeed(1);

	DisarmThroughNativeAction(player, target);

	EXPECT_FALSE(target._oTrapFlag);
	EXPECT_EQ(trap._oVar4, 0);
	EXPECT_EQ(GetLCGEngineState(), 1U);
}

TEST_F(ObjectsLevelTest, AddObjTraps_ObjNullDoesNotCrash)
{
	// Regression test: AddObjTraps previously crashed when FindObjectAtPosition
	// returned a non-null Object* whose _otype was OBJ_NULL (-1), because that
	// value was used as an unsigned index into AllObjects (out-of-bounds access).
	//
	// Arrange: place an Object with _otype == OBJ_NULL into the pool and register
	// it on a tile so FindObjectAtPosition will return it.
	constexpr Point testPos { 10, 10 };

	const int oi = ObjectPoolAdapter::ReserveObjectSlot();
	ObjectPoolAdapter::CommitReservedObjectSlot();
	Object &obj = Objects[oi];
	obj = {};
	obj._otype = OBJ_NULL;
	tileAt(testPos).setObject(oi + 1);

	// Act: must not crash (no EXPECT needed beyond "reaches here").
	SetRndSeed(0);
	// Drive rndv to maximum to maximise probability the RNG check passes (rndv=25 at level 1→7).
	// We just need it to not abort; iterate over the whole map.
	for (int j = 0; j < MAXDUNY; j++) {
		for (int i = 0; i < MAXDUNX; i++) {
			Object *triggerObject = FindObjectAtPosition({ i, j }, false);
			if (triggerObject == nullptr || triggerObject->_otype == OBJ_NULL)
				continue;
			// If we somehow get here with a valid otype, check AllObjects is safe.
			ASSERT_GE(static_cast<int>(triggerObject->_otype), 0);
			ASSERT_LT(static_cast<size_t>(triggerObject->_otype), AllObjects.size());
		}
	}
	// The test passes if we reach this line without an assertion / crash.
	SUCCEED();
}

// ============================================================================
// UpdateTrapState branch coverage (requires level + assets)
// ============================================================================

class UpdateTrapStateTest : public ::testing::Test {
protected:
	static void SetUpTestSuite()
	{
		SetTestAssetsPath();
		LoadObjectData();
	}

	void SetUp() override
	{
		currentLevel().setId(Level::create(LevelId { .levelNum = 1, .type = DTYPE_CATHEDRAL }).id());
		InitializeObjectPool();
		ObjectPoolAdapter::ResetLegacyObjectPools();
		for (Tile &tile : tiles().columnMajor())
			tile.setObject(0);
	}

	// Registers an object at a tile and returns a reference to it.
	Object &PlaceObject(_object_id otype, Point pos)
	{
		const int oi = ObjectPoolAdapter::ReserveObjectSlot();
		ObjectPoolAdapter::CommitReservedObjectSlot();
		Object &obj = Objects[oi];
		obj = {};
		obj._otype = otype;
		tileAt(pos).setObject(oi + 1);
		return obj;
	}
};

TEST_F(UpdateTrapStateTest, AlreadyFiredTrapReturnsFalse)
{
	// _oVar4 != 0 means the trap has already fired.
	constexpr Point trapPos { 20, 20 };
	constexpr Point triggerPos { 22, 20 };

	Object &trigger = PlaceObject(OBJ_LEVER, triggerPos);
	trigger.selectionRegion = SelectionRegion::Bottom; // canInteractWith() == true
	trigger._oTrapFlag = true;

	Object &trap = PlaceObject(OBJ_TRAPL, trapPos);
	trap._oVar1 = triggerPos.x;
	trap._oVar2 = triggerPos.y;
	trap._oVar4 = 1; // already fired

	EXPECT_FALSE(UpdateTrapState(trap)) << "Trap that already fired must return false";
}

TEST_F(UpdateTrapStateTest, LeverTrigger_Interactable_WithTrapFlag_ReturnsFalse)
{
	// A lever that is still interactable (canInteractWith) and has _oTrapFlag set
	// means the trap has not yet been triggered — UpdateTrapState should return false.
	constexpr Point trapPos { 20, 20 };
	constexpr Point triggerPos { 22, 20 };

	Object &trigger = PlaceObject(OBJ_LEVER, triggerPos);
	trigger.selectionRegion = SelectionRegion::Bottom; // canInteractWith() == true
	trigger._oTrapFlag = true;

	Object &trap = PlaceObject(OBJ_TRAPL, trapPos);
	trap._oVar1 = triggerPos.x;
	trap._oVar2 = triggerPos.y;
	trap._oVar4 = 0;

	EXPECT_FALSE(UpdateTrapState(trap))
		<< "Lever with canInteractWith and _oTrapFlag should suppress trap fire";
}

TEST_F(UpdateTrapStateTest, LeverTrigger_NotInteractable_FiresTrap)
{
	// Once the lever is no longer interactable (already pulled), the trap fires.
	constexpr Point trapPos { 20, 20 };
	constexpr Point triggerPos { 22, 20 };

	Object &trigger = PlaceObject(OBJ_LEVER, triggerPos);
	trigger.selectionRegion = SelectionRegion::None; // canInteractWith() == false
	trigger._oTrapFlag = false;

	Object &trap = PlaceObject(OBJ_TRAPL, trapPos);
	trap._oVar1 = triggerPos.x;
	trap._oVar2 = triggerPos.y;
	trap._oVar4 = 0;

	EXPECT_TRUE(UpdateTrapState(trap))
		<< "Lever that is no longer interactable should fire the trap";
	EXPECT_EQ(trap._oVar4, 1) << "Trap _oVar4 should be set to 1 after firing";
	EXPECT_FALSE(trigger._oTrapFlag) << "Trigger _oTrapFlag should be cleared after trap fires";
}

TEST_F(UpdateTrapStateTest, ChestTrigger_FiresTrap)
{
	// An opened chest (selectionRegion None, no trap flag) should fire the trap.
	constexpr Point trapPos { 30, 30 };
	constexpr Point triggerPos { 32, 30 };

	Object &trigger = PlaceObject(OBJ_CHEST1, triggerPos);
	trigger.selectionRegion = SelectionRegion::None;
	trigger._oTrapFlag = false;

	Object &trap = PlaceObject(OBJ_TRAPL, trapPos);
	trap._oVar1 = triggerPos.x;
	trap._oVar2 = triggerPos.y;
	trap._oVar4 = 0;

	EXPECT_TRUE(UpdateTrapState(trap));
	EXPECT_EQ(trap._oVar4, 1);
}

TEST_F(UpdateTrapStateTest, ClosedDoor_WithTrapFlag_DoesNotFire)
{
	// A door that is still closed (_oVar4 == DOOR_CLOSED) and _oTrapFlag is set
	// should suppress the trap.
	constexpr Point trapPos { 40, 40 };
	constexpr Point triggerPos { 42, 40 };

	Object &trigger = PlaceObject(OBJ_L1LDOOR, triggerPos);
	trigger._oVar4 = 0; // DOOR_CLOSED
	trigger._oTrapFlag = true;

	Object &trap = PlaceObject(OBJ_TRAPL, trapPos);
	trap._oVar1 = triggerPos.x;
	trap._oVar2 = triggerPos.y;
	trap._oVar4 = 0;

	EXPECT_FALSE(UpdateTrapState(trap))
		<< "Closed door with trap flag should not fire the trap";
}

TEST_F(UpdateTrapStateTest, OpenedDoor_FiresTrap)
{
	// An opened door (_oVar4 != DOOR_CLOSED) should fire the trap.
	constexpr Point trapPos { 40, 40 };
	constexpr Point triggerPos { 42, 40 };

	Object &trigger = PlaceObject(OBJ_L1LDOOR, triggerPos);
	trigger._oVar4 = 1; // DOOR_OPEN
	trigger._oTrapFlag = false;

	Object &trap = PlaceObject(OBJ_TRAPL, trapPos);
	trap._oVar1 = triggerPos.x;
	trap._oVar2 = triggerPos.y;
	trap._oVar4 = 0;

	EXPECT_TRUE(UpdateTrapState(trap))
		<< "Opened door should trigger the trap";
	EXPECT_EQ(trap._oVar4, 1);
	EXPECT_FALSE(trigger._oTrapFlag);
}

TEST_F(UpdateTrapStateTest, UnrecognizedTriggerType_ReturnsFalse)
{
	// A trigger object whose _otype is not in the expected switch cases
	// (e.g. a barrel) should cause UpdateTrapState to return false (default branch).
	constexpr Point trapPos { 50, 50 };
	constexpr Point triggerPos { 52, 50 };

	Object &trigger = PlaceObject(OBJ_BARREL, triggerPos);
	trigger.selectionRegion = SelectionRegion::None;

	Object &trap = PlaceObject(OBJ_TRAPL, trapPos);
	trap._oVar1 = triggerPos.x;
	trap._oVar2 = triggerPos.y;
	trap._oVar4 = 0;

	EXPECT_FALSE(UpdateTrapState(trap))
		<< "Unrecognized trigger type should hit the default branch and return false";
}

} // namespace

#include <gtest/gtest.h>

#include <algorithm>
#include <array>
#include <cstdint>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <optional>
#include <sstream>
#include <stdexcept>
#include <string>
#include <string_view>
#include <type_traits>
#include <vector>
#ifdef _WIN32
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#endif

#include "application/diablo.h"
#include "application/game_mode.hpp"
#include "application/headless_mode.hpp"
#include "engine/assets.hpp"
#include "engine/audio/sound.h"
#include "engine/lighting.h"
#include "engine/random.hpp"
#include "game/items/item_pool.hpp"
#include "game/levels/dungeon_common.h"
#include "game/levels/level.hpp"
#include "game/levels/themes.h"
#include "game/levels/triggers.h"
#include "game/missiles/missiles.hpp"
#include "game/monsters/monster_pool.hpp"
#include "game/monsters/monsters.hpp"
#include "game/objects/object_pool.hpp"
#include "game/objects/objects.hpp"
#include "level_test.hpp"
#include "persistence/loadsave.h"
#include "tables/itemdat.h"
#include "tables/leveldat.h"
#include "tables/monstdat.h"
#include "tables/objdat.h"

using namespace devilution;

namespace devilution {

namespace {

constexpr char PopulationOracleDirectoryVariable[] = "GODEVILUTION_CATHEDRAL_POPULATION_ORACLE_DIRECTORY";
constexpr char PopulationInputDirectoryVariable[] = "GODEVILUTION_CATHEDRAL_POPULATION_INPUT_DIRECTORY";
constexpr char EncounterInputDirectoryVariable[] = "GODEVILUTION_CATHEDRAL_ENCOUNTER_ORACLE_DIRECTORY";
constexpr char EncounterInputFormat[] = "godevilution-cathedral-l1-encounters";
constexpr char EncounterInputPolicy[] = "ordinary-original-diablo-post-initial-objects-initmonsters";
constexpr char InitialItemDirectoryVariable[] = "GODEVILUTION_CATHEDRAL_INITIAL_ITEM_ORACLE_DIRECTORY";
constexpr char InitialItemFormat[] = "godevilution-cathedral-initial-items";
constexpr char InitialItemPolicy[] = "ordinary-original-diablo-post-initmonsters-inititems";
constexpr char PopulationOracleFormat[] = "godevilution-cathedral-l1-population-oracle";
constexpr uint32_t PopulationOracleVersion = 1;
constexpr char PopulationOraclePolicy[] = "no-quest-original-diablo-initgolems-initobjects";
constexpr char PopulationInputFormat[] = "godevilution-cathedral-initial-inputs";
constexpr uint32_t PopulationInputVersion = 2;
constexpr char PopulationInputPolicy[] = "ordinary-original-diablo-fresh-initial-objects";
constexpr uint32_t StoryDungeonSeed = UINT32_C(0x13579BDF);
constexpr int CathedralFirstDepth = 1;
constexpr int CathedralLastDepth = 4;
constexpr size_t PopulationCaseCount = 16;
// The native InitGolems loop reserves one slot per supported player (four).
constexpr int InitialGolemSlots = 4;

struct NativeObjectCapture {
	int slot = 0;
	int type = 0;
	Point position;
	bool applyLighting = false;
	uint32_t animFlag = 0;
	int animDelay = 0;
	int animCount = 0;
	uint32_t animLength = 0;
	uint32_t animFrame = 0;
	uint16_t animWidth = 0;
	bool deleteFlag = false;
	int breakFlag = 0;
	bool solidFlag = false;
	bool missilePassable = false;
	int selectionRegion = 0;
	bool preFlag = false;
	bool trapFlag = false;
	bool doorFlag = false;
	int lightId = 0;
	uint32_t rndSeed = 0;
	int var1 = 0;
	int var2 = 0;
	int var3 = 0;
	int var4 = 0;
	int var5 = 0;
	uint32_t var6 = 0;
	int bookMessage = 0;
	int var8 = 0;
};

struct NativeMonsterCapture {
	int slot = 0;
	int type = 0;
	int levelType = 0;
	int mode = 0;
	int goal = 0;
	int goalVar1 = 0;
	int goalVar2 = 0;
	int goalVar3 = 0;
	int var1 = 0;
	int var2 = 0;
	int var3 = 0;
	int pathCount = 0;
	Point tile;
	Point future;
	Point old;
	Point last;
	int direction = 0;
	int enemy = 0;
	Point enemyPosition;
	bool invalid = false;
	int ai = 0;
	int intelligence = 0;
	int activeForTicks = 0;
	int uniqueType = 0;
	int uniqueTranslation = 0;
	int corpseId = 0;
	int whoHit = 0;
	int minDamage = 0;
	int maxDamage = 0;
	int minDamageSpecial = 0;
	int maxDamageSpecial = 0;
	int armorClass = 0;
	int resistance = 0;
	int talkMessage = 0;
	int leader = 0;
	int leaderRelation = 0;
	int packSize = 0;
	uint32_t flags = 0;
	uint32_t rndItemSeed = 0;
	uint32_t aiSeed = 0;
	int lightId = 0;
	int hitPoints = 0;
	int maxHitPoints = 0;
	int animTicksPerFrame = 0;
	int animTickCounter = 0;
	int animNumberOfFrames = 0;
	int animCurrentFrame = 0;
	Point temp;
	uint16_t golemToHit = 0;
	int reducePlayerStrength = 0;
	int reducePlayerMagic = 0;
	int reducePlayerDexterity = 0;
	int reducePlayerVitality = 0;
	int reducePlayerMaxHP = 0;
	int reducePlayerMaxMana = 0;
};

struct NativeBestiaryCapture {
	int index = 0;
	int type = 0;
	bool skeleton = false;
	int placeFlags = 0;
	int corpseId = 0;
	std::array<int, 6> frames {};
	std::array<int, 6> rates {};
	std::array<int, 6> widths {};
};

struct TriggerCapture {
	Point position;
	int message = 0;
	int level = 0;
};

struct PopulationCapture {
	int depth = 0;
	uint32_t inputSeed = 0;
	uint32_t geometrySeed = 0;
	uint32_t populationSeed = 0;
	lvl_entry entry = ENTRY_MAIN;
	uint32_t attemptSeed = 0;
	uint32_t rngAfterCreateLevel = 0;
	uint32_t rngAfterPopulationReset = 0;
	uint32_t rngAfterGetLevelMTypes = 0;
	uint32_t rngAfterInitThemes = 0;
	uint32_t rngAfterInitGolems = 0;
	uint32_t rngAfterInitObjects = 0;
	Point entryPosition;
	Point stairsUp { -1, -1 };
	Point stairsDown { -1, -1 };
	std::vector<TriggerCapture> triggers;
	std::vector<uint32_t> populatedCellsBeforeHoldThemeRooms;
	std::vector<uint32_t> populatedCellsAfterHoldThemeRooms;
	std::vector<uint16_t> pieces;
	std::vector<uint8_t> transparency;
	std::vector<uint8_t> solProperties;
	int activeObjectCount = 0;
	std::vector<int> activeObjectIds;
	std::vector<int> availableObjectIds;
	std::vector<int> tileObjectRefs;
	std::vector<NativeObjectCapture> objects;
	int activeMonsterCount = 0;
	std::vector<int> activeMonsterIds;
	std::vector<NativeMonsterCapture> monsters;
	std::vector<NativeBestiaryCapture> bestiary;
	int playerId = 0;
	int playerClass = 0;
	int playerLevel = 0;
	bool playerActive = false;
	bool playerLevelChanging = false;
	Point playerTile;
	Point playerFuture;
	Point playerOld;
	std::vector<uint16_t> prePhasePieces;
	std::vector<uint8_t> prePhaseSpecial;
	std::vector<uint8_t> prePhaseTransparency;
	std::vector<uint8_t> prePhaseSolProperties;
	std::vector<bool> prePhasePopulatedCells;
	std::vector<int> prePhaseMonsterTileRefs;
	std::vector<int> prePhasePlayerTileRefs;
	std::vector<NativeBestiaryCapture> prePhaseBestiary;
	int preThemeNextTransparency = 0;
	std::vector<bool> preThemePopulatedCells;
	std::vector<uint16_t> preThemePieces;
	std::vector<uint8_t> preThemeSpecial;
	std::vector<uint8_t> preThemeTransparency;
	std::vector<uint8_t> preThemeSolProperties;
	std::vector<ThemeStruct> selectedThemes;
	std::vector<uint16_t> finalPieces;
	std::vector<uint8_t> finalSpecial;
	std::vector<uint8_t> finalTransparency;
};

// This separate opt-in phase preserves the legacy reference and V2 input bytes.
struct NativeEncounterState {
	size_t totalMonsters = 0;
	uint32_t randomState = 0;
	int activeMonsterCount = 0;
	std::vector<int> activeMonsterIds;
	std::vector<NativeMonsterCapture> monsters;
	std::vector<int> monsterTileRefs;
	std::vector<int> playerTileRefs;
	std::vector<int> objectTileRefs;
	std::vector<uint8_t> tileFlags;
	std::vector<bool> visibleTransparencyRegions;
	std::vector<int> activeLightSlots;
	int activeLightCount = 0;
	bool updateLighting = false;
	bool disableLighting = false;
	std::array<Light, MAXLIGHTS> lights {};
};

struct NativeEncounterCapture {
	uint32_t preClearRandomState = 0;
	uint32_t postClearRandomState = 0;
	int depth = 0;
	uint32_t inputSeed = 0;
	lvl_entry entry = ENTRY_MAIN;
	int densityDivisor = 0;
	std::vector<uint8_t> preThemeTileFlags;
	NativeEncounterState preState;
	NativeEncounterState expectedState;
};

// Scalar/string item state excludes engine sprite handles and private visual
// animation distribution internals. No archive or capture is a server input.
struct NativeInitialItemState {
	uint32_t randomState = 0;
	int activeItemCount = 0;
	std::vector<int> activeItemIds;
	std::vector<Item> items;
	std::vector<int> itemTileRefs;
	std::vector<uint16_t> pieces;
	std::vector<uint8_t> tileFlags;
	std::vector<int> monsterTileRefs;
	std::vector<int> playerTileRefs;
	std::vector<int> objectTileRefs;
};

struct NativeInitialItemCapture {
	int depth = 0;
	uint32_t inputSeed = 0;
	lvl_entry entry = ENTRY_MAIN;
	NativeInitialItemState preState;
	NativeInitialItemState expectedState;
};

struct NativeStateGuard {
	explicit NativeStateGuard(bool resetInitialItems = false)
	    : oldMultiplayer(gbIsMultiplayer)
	    , oldSpawn(gbIsSpawn)
	    , oldHellfire(gbIsHellfire)
	    , oldHellfireSave(gbIsHellfireSaveGame)
	    , oldPlayer(MyPlayer)
	    , oldPlayers(std::move(Players))
	    , oldPlayerId(MyPlayerId)
	    , oldDifficulty(sgGameInitInfo.nDifficulty)
	    , oldLoadingMapObjects(LoadingMapObjects)
	    , oldSound(gbSndInited)
	    , oldLevelGenerationData(LevelGenerationData)
	    , resetInitialItems(resetInitialItems)
	{
	}

	~NativeStateGuard()
	{
		ResetPools();
		gbIsMultiplayer = oldMultiplayer;
		gbIsSpawn = oldSpawn;
		gbIsHellfire = oldHellfire;
		gbIsHellfireSaveGame = oldHellfireSave;
		Players = std::move(oldPlayers);
		MyPlayer = oldPlayer;
		MyPlayerId = oldPlayerId;
		sgGameInitInfo.nDifficulty = oldDifficulty;
		LoadingMapObjects = oldLoadingMapObjects;
		gbSndInited = oldSound;
		LevelGenerationData = std::move(oldLevelGenerationData);
	}

	void ResetPools() const
	{
		InitializeObjectPool();
		for (int i = 0; i < MAXOBJECTS; i++)
			Objects[i] = {};
		ObjectPoolAdapter::ResetLegacyObjectPools();
		InitializeMonsterPool();
		if (resetInitialItems) {
			InitializeItemPool();
			for (int i = 0; i <= MAXITEMS; i++)
				Items[i] = {};
			ItemPoolAdapter::ResetLegacyItemPools();
		}
		for (Tile &tile : tiles().columnMajor()) {
			tile.setObject(0);
			tile.setMonster(0);
			if (resetInitialItems)
				tile.setItem(0);
		}
		CurrentLightManager.Init();
	}

	bool oldMultiplayer;
	bool oldSpawn;
	bool oldHellfire;
	bool oldHellfireSave;
	Player *oldPlayer;
	std::vector<Player> oldPlayers;
	decltype(MyPlayerId) oldPlayerId;
	decltype(sgGameInitInfo.nDifficulty) oldDifficulty;
	bool oldLoadingMapObjects;
	bool oldSound;
	std::vector<LevelGenerationRow> oldLevelGenerationData;
	bool resetInitialItems;
};

std::string EntryName(lvl_entry entry)
{
	return entry == ENTRY_MAIN ? "main" : "previous";
}

template <typename T>
void WriteArray(std::ostream &stream, const std::vector<T> &values)
{
	stream << '[';
	for (size_t i = 0; i < values.size(); i++) {
		if (i != 0)
			stream << ',';
		if constexpr (std::is_signed_v<T>)
			stream << static_cast<int64_t>(values[i]);
		else
			stream << static_cast<uint64_t>(values[i]);
	}
	stream << ']';
}

void WritePoint(std::ostream &stream, Point point)
{
	stream << "{\"x\":" << point.x << ",\"y\":" << point.y << '}';
}

void WriteJsonString(std::ostream &stream, std::string_view value)
{
	stream << '"';
	for (const char character : value) {
		if (character == '\\' || character == '"')
			stream << '\\';
		stream << character;
	}
	stream << '"';
}

// Only the new item sidecar uses complete JSON escaping; legacy bytes stay frozen.
void WriteInitialItemJsonString(std::ostream &stream, std::string_view value)
{
	constexpr std::string_view Hex = "0123456789abcdef";
	stream << '"';
	for (const unsigned char character : value) {
		if (character == '\\' || character == '"') {
			stream << '\\' << character;
		} else if (character < 0x20) {
			stream << "\\u00" << Hex[character >> 4] << Hex[character & 0xF];
		} else {
			stream << character;
		}
	}
	stream << '"';
}

void WriteBoolArray(std::ostream &stream, const std::vector<bool> &values)
{
	stream << '[';
	for (size_t i = 0; i < values.size(); i++) {
		if (i != 0)
			stream << ',';
		stream << (values[i] ? "true" : "false");
	}
	stream << ']';
}

void WriteObject(std::ostream &stream, const NativeObjectCapture &object)
{
	stream << "{\"slot\":" << object.slot
	       << ",\"type\":" << object.type << ",\"position\":";
	WritePoint(stream, object.position);
	stream << ",\"applyLighting\":" << (object.applyLighting ? "true" : "false")
	       << ",\"animFlag\":" << object.animFlag
	       << ",\"animDelay\":" << object.animDelay
	       << ",\"animCount\":" << object.animCount
	       << ",\"animLength\":" << object.animLength
	       << ",\"animFrame\":" << object.animFrame
	       << ",\"animWidth\":" << object.animWidth
	       << ",\"deleteFlag\":" << (object.deleteFlag ? "true" : "false")
	       << ",\"breakFlag\":" << object.breakFlag
	       << ",\"solidFlag\":" << (object.solidFlag ? "true" : "false")
	       << ",\"missilePassable\":" << (object.missilePassable ? "true" : "false")
	       << ",\"selectionRegion\":" << object.selectionRegion
	       << ",\"preFlag\":" << (object.preFlag ? "true" : "false")
	       << ",\"trapFlag\":" << (object.trapFlag ? "true" : "false")
	       << ",\"doorFlag\":" << (object.doorFlag ? "true" : "false")
	       << ",\"lightId\":" << object.lightId
	       << ",\"rndSeed\":" << object.rndSeed
	       << ",\"var1\":" << object.var1
	       << ",\"var2\":" << object.var2
	       << ",\"var3\":" << object.var3
	       << ",\"var4\":" << object.var4
	       << ",\"var5\":" << object.var5
	       << ",\"var6\":" << object.var6
	       << ",\"bookMessage\":" << object.bookMessage
	       << ",\"var8\":" << object.var8 << '}';
}

void WriteMonster(std::ostream &stream, const NativeMonsterCapture &monster)
{
	stream << "{\"slot\":" << monster.slot
	       << ",\"type\":" << monster.type
	       << ",\"levelType\":" << monster.levelType
	       << ",\"mode\":" << monster.mode
	       << ",\"goal\":" << monster.goal
	       << ",\"goalVar1\":" << monster.goalVar1
	       << ",\"goalVar2\":" << monster.goalVar2
	       << ",\"goalVar3\":" << monster.goalVar3
	       << ",\"var1\":" << monster.var1
	       << ",\"var2\":" << monster.var2
	       << ",\"var3\":" << monster.var3
	       << ",\"pathCount\":" << monster.pathCount
	       << ",\"tile\":";
	WritePoint(stream, monster.tile);
	stream << ",\"future\":";
	WritePoint(stream, monster.future);
	stream << ",\"old\":";
	WritePoint(stream, monster.old);
	stream << ",\"last\":";
	WritePoint(stream, monster.last);
	stream << ",\"direction\":" << monster.direction
	       << ",\"enemy\":" << monster.enemy
	       << ",\"enemyPosition\":";
	WritePoint(stream, monster.enemyPosition);
	stream << ",\"invalid\":" << (monster.invalid ? "true" : "false")
	       << ",\"ai\":" << monster.ai
	       << ",\"intelligence\":" << monster.intelligence
	       << ",\"activeForTicks\":" << monster.activeForTicks
	       << ",\"uniqueType\":" << monster.uniqueType
	       << ",\"uniqueTranslation\":" << monster.uniqueTranslation
	       << ",\"corpseId\":" << monster.corpseId
	       << ",\"whoHit\":" << monster.whoHit
	       << ",\"minDamage\":" << monster.minDamage
	       << ",\"maxDamage\":" << monster.maxDamage
	       << ",\"minDamageSpecial\":" << monster.minDamageSpecial
	       << ",\"maxDamageSpecial\":" << monster.maxDamageSpecial
	       << ",\"armorClass\":" << monster.armorClass
	       << ",\"resistance\":" << monster.resistance
	       << ",\"talkMessage\":" << monster.talkMessage
	       << ",\"leader\":" << monster.leader
	       << ",\"leaderRelation\":" << monster.leaderRelation
	       << ",\"packSize\":" << monster.packSize
	       << ",\"flags\":" << monster.flags
	       << ",\"rndItemSeed\":" << monster.rndItemSeed
	       << ",\"aiSeed\":" << monster.aiSeed
	       << ",\"lightId\":" << monster.lightId
	       << ",\"hitPoints\":" << monster.hitPoints
	       << ",\"maxHitPoints\":" << monster.maxHitPoints
	       << ",\"animTicksPerFrame\":" << monster.animTicksPerFrame
	       << ",\"animTickCounter\":" << monster.animTickCounter
	       << ",\"animNumberOfFrames\":" << monster.animNumberOfFrames
	       << ",\"animCurrentFrame\":" << monster.animCurrentFrame << '}';
}

void WriteBestiary(std::ostream &stream, const std::vector<NativeBestiaryCapture> &entries)
{
	stream << '[';
	for (size_t i = 0; i < entries.size(); i++) {
		if (i != 0)
			stream << ',';
		stream << "{\"index\":" << entries[i].index
		       << ",\"type\":" << entries[i].type
		       << ",\"skeleton\":" << (entries[i].skeleton ? "true" : "false")
		       << ",\"placeFlags\":" << entries[i].placeFlags
		       << ",\"corpseId\":" << entries[i].corpseId
		       << ",\"frames\":";
		std::vector<int> frames(entries[i].frames.begin(), entries[i].frames.end());
		WriteArray(stream, frames);
		stream << ",\"rates\":";
		std::vector<int> rates(entries[i].rates.begin(), entries[i].rates.end());
		WriteArray(stream, rates);
		stream << ",\"widths\":";
		std::vector<int> widths(entries[i].widths.begin(), entries[i].widths.end());
		WriteArray(stream, widths);
		stream << '}';
	}
	stream << ']';
}

NativeObjectCapture CaptureObject(int slot, const Object &object)
{
	return {
		slot,
		static_cast<int>(object._otype),
		object.position,
		object.applyLighting,
		object._oAnimFlag,
		object._oAnimDelay,
		object._oAnimCnt,
		object._oAnimLen,
		object._oAnimFrame,
		object._oAnimWidth,
		object._oDelFlag,
		object._oBreak,
		object._oSolidFlag,
		object._oMissFlag,
		static_cast<int>(object.selectionRegion),
		object._oPreFlag,
		object._oTrapFlag,
		object._oDoorFlag,
		object._olid,
		object._oRndSeed,
		object._oVar1,
		object._oVar2,
		object._oVar3,
		object._oVar4,
		object._oVar5,
		object._oVar6,
		static_cast<int>(object.bookMessage),
		object._oVar8,
	};
}

NativeMonsterCapture CaptureMonster(int slot, const Monster &monster)
{
	return {
		slot,
		static_cast<int>(monster.type().type),
		monster.levelType,
		static_cast<int>(monster.mode),
		static_cast<int>(monster.goal),
		monster.goalVar1,
		monster.goalVar2,
		monster.goalVar3,
		monster.var1,
		monster.var2,
		monster.var3,
		monster.pathCount,
		monster.position.tile,
		monster.position.future,
		monster.position.old,
		monster.position.last,
		static_cast<int>(monster.direction),
		monster.enemy,
		{ monster.enemyPosition.x, monster.enemyPosition.y },
		monster.isInvalid,
		static_cast<int>(monster.ai),
		monster.intelligence,
		monster.activeForTicks,
		static_cast<int>(monster.uniqueType),
		monster.uniqTrans,
		monster.corpseId,
		monster.whoHit,
		monster.minDamage,
		monster.maxDamage,
		monster.minDamageSpecial,
		monster.maxDamageSpecial,
		monster.armorClass,
		monster.resistance,
		static_cast<int>(monster.talkMsg),
		monster.leader,
		static_cast<int>(monster.leaderRelation),
		monster.packSize,
		monster.flags,
		monster.rndItemSeed,
		monster.aiSeed,
		monster.lightId,
		monster.hitPoints,
		monster.maxHitPoints,
		monster.animInfo.ticksPerFrame,
		monster.animInfo.tickCounterOfCurrentFrame,
		monster.animInfo.numberOfFrames,
		monster.animInfo.currentFrame,
		monster.position.temp,
		monster.golemToHit,
		monster.reducePlayerStrength,
		monster.reducePlayerMagic,
		monster.reducePlayerDexterity,
		monster.reducePlayerVitality,
		monster.reducePlayerMaxHP,
		monster.reducePlayerMaxMana,
	};
}

std::vector<uint8_t> CaptureTileFlags()
{
	std::vector<uint8_t> flags;
	flags.reserve(MAXDUNX * MAXDUNY);
	for (int y = 0; y < MAXDUNY; y++)
		for (int x = 0; x < MAXDUNX; x++)
			flags.push_back(static_cast<uint8_t>(tileAt(x, y).flags()));
	return flags;
}

NativeEncounterState CaptureEncounterState()
{
	NativeEncounterState state;
	state.totalMonsters = MonsterPopulationTargetForTesting();
	state.randomState = GetLCGEngineState();
	state.activeMonsterCount = static_cast<int>(MonsterPoolAdapter::ActiveMonsterCountValue());
	for (const unsigned id : MonsterPoolAdapter::ActiveMonsterIds())
		state.activeMonsterIds.push_back(static_cast<int>(id));
	for (int slot = 0; slot < MaxMonsters; slot++)
		state.monsters.push_back(CaptureMonster(slot, Monsters[slot]));
	for (int y = 0; y < MAXDUNY; y++) {
		for (int x = 0; x < MAXDUNX; x++) {
			state.monsterTileRefs.push_back(tileAt(x, y).monster());
			state.playerTileRefs.push_back(tileAt(x, y).player());
			state.objectTileRefs.push_back(tileAt(x, y).object());
		}
	}
	state.tileFlags = CaptureTileFlags();
	for (const bool visible : visibleTransparencyRegions())
		state.visibleTransparencyRegions.push_back(visible);
	for (const uint8_t slot : CurrentLightManager.activeLights_)
		state.activeLightSlots.push_back(slot);
	state.activeLightCount = CurrentLightManager.activeLightCount_;
	state.updateLighting = CurrentLightManager.updateLighting_;
#ifdef _DEBUG
	state.disableLighting = CurrentLightManager.disableLighting_;
#endif
	std::copy(std::begin(CurrentLightManager.lights_), std::end(CurrentLightManager.lights_), state.lights.begin());
	return state;
}

std::vector<uint32_t> CapturePopulatedCells()
{
	std::vector<uint32_t> cells;
	for (int y = 0; y < MAXDUNY; y++) {
		for (int x = 0; x < MAXDUNX; x++) {
			if (tileAt(x, y).isPopulated())
				cells.push_back(static_cast<uint32_t>(y * MAXDUNX + x));
		}
	}
	return cells;
}

NativeInitialItemState CaptureInitialItemState()
{
	NativeInitialItemState state;
	state.randomState = GetLCGEngineState();
	state.activeItemCount = ActiveItemCount;
	for (const uint8_t slot : ActiveItems)
		state.activeItemIds.push_back(slot);
	for (int slot = 0; slot <= MAXITEMS; slot++)
		state.items.push_back(Items[slot]);
	for (int y = 0; y < MAXDUNY; y++) {
		for (int x = 0; x < MAXDUNX; x++) {
			const Tile &tile = tileAt(x, y);
			state.itemTileRefs.push_back(tile.item());
			state.pieces.push_back(tile.piece());
			state.tileFlags.push_back(static_cast<uint8_t>(tile.flags()));
			state.monsterTileRefs.push_back(tile.monster());
			state.playerTileRefs.push_back(tile.player());
			state.objectTileRefs.push_back(tile.object());
		}
	}
	return state;
}

std::vector<uint16_t> CapturePieces()
{
	std::vector<uint16_t> pieces;
	for (int y = 0; y < MAXDUNY; y++)
		for (int x = 0; x < MAXDUNX; x++)
			pieces.push_back(tileAt(x, y).piece());
	return pieces;
}

std::vector<uint8_t> CaptureSpecial()
{
	std::vector<uint8_t> special;
	for (int y = 0; y < MAXDUNY; y++)
		for (int x = 0; x < MAXDUNX; x++)
			special.push_back(static_cast<uint8_t>(tileAt(x, y).special()));
	return special;
}

std::vector<uint8_t> CaptureTransparency()
{
	std::vector<uint8_t> transparency;
	for (int y = 0; y < MAXDUNY; y++)
		for (int x = 0; x < MAXDUNX; x++)
			transparency.push_back(static_cast<uint8_t>(tileAt(x, y).transVal()));
	return transparency;
}

std::vector<uint8_t> CaptureSolProperties()
{
	std::vector<uint8_t> properties;
	for (int piece = 0; piece < MAXTILES; piece++)
		properties.push_back(static_cast<uint8_t>(tileProperties()[piece]));
	return properties;
}

std::vector<bool> CapturePopulatedFlags()
{
	std::vector<bool> populated;
	populated.reserve(MAXDUNX * MAXDUNY);
	for (int y = 0; y < MAXDUNY; y++)
		for (int x = 0; x < MAXDUNX; x++)
			populated.push_back(tileAt(x, y).isPopulated());
	return populated;
}

std::vector<int> CaptureMonsterTileRefs()
{
	std::vector<int> references;
	for (int y = 0; y < MAXDUNY; y++)
		for (int x = 0; x < MAXDUNX; x++)
			references.push_back(tileAt(x, y).monster());
	return references;
}

std::vector<int> CapturePlayerTileRefs()
{
	std::vector<int> references;
	for (int y = 0; y < MAXDUNY; y++)
		for (int x = 0; x < MAXDUNX; x++)
			references.push_back(tileAt(x, y).player());
	return references;
}

std::vector<NativeBestiaryCapture> CaptureBestiary()
{
	std::vector<NativeBestiaryCapture> entries;
	for (size_t index = 0; index < LevelBestiary.size(); index++) {
		const CMonster &monster = LevelBestiary[index];
		NativeBestiaryCapture capture;
		capture.index = static_cast<int>(index);
		capture.type = static_cast<int>(monster.type);
		capture.skeleton = IsSkel(monster.type);
		capture.placeFlags = monster.placeFlags;
		capture.corpseId = monster.corpseId;
		for (size_t animation = 0; animation < 6; animation++) {
			capture.frames[animation] = monster.anims[animation].frames;
			capture.rates[animation] = monster.anims[animation].rate;
			capture.widths[animation] = monster.anims[animation].width;
		}
		entries.push_back(capture);
	}
	return entries;
}

void AssertFreshMonsterSlots()
{
	if (MonsterPoolAdapter::ActiveMonsterCountValue() != 0)
		throw std::runtime_error("fresh Cathedral monster slots were not empty");
	for (int slot = 0; slot < static_cast<int>(MaxMonsters); slot++) {
		if (ActiveMonsters[slot] != static_cast<unsigned>(slot))
			throw std::runtime_error("fresh Cathedral ActiveMonsters identity was not initialized");
		const Monster &monster = Monsters[slot];
		if (monster.position.tile != WorldTilePosition { 0, 0 }
		    || monster.position.future != WorldTilePosition { 0, 0 }
		    || monster.position.old != Point { 0, 0 }
		    || monster.position.last != Point { 0, 0 }
		    || monster.position.temp != Point { 0, 0 }
		    || monster.enemyPosition != WorldTilePosition { 0, 0 }
		    || monster.enemy != 0
		    || monster.goalVar1 != 0
		    || monster.goalVar2 != 0
		    || monster.goalVar3 != 0
		    || monster.var1 != 0
		    || monster.var2 != 0
		    || monster.var3 != 0
		    || monster.goal != MonsterGoal::None
		    || monster.mode != MonsterMode::Stand
		    || monster.levelType != 0
		    || monster.pathCount != 0
		    || monster.ai != MonsterAIID::Zombie
		    || monster.intelligence != 0
		    || monster.uniqueType != UniqueMonsterType::Garbud
		    || monster.uniqTrans != 0
		    || monster.corpseId != 0
		    || monster.talkMsg != 0
		    || monster.resistance != 0
		    || monster.leader != 0
		    || monster.leaderRelation != LeaderRelation::None
		    || monster.packSize != 0
		    || monster.golemToHit != 0
		    || monster.rndItemSeed != 0
		    || monster.aiSeed != 0
		    || monster.hitPoints != 0
		    || monster.maxHitPoints != 0
		    || monster.activeForTicks != 0
		    || monster.whoHit != 0
		    || monster.minDamage != 0
		    || monster.maxDamage != 0
		    || monster.minDamageSpecial != 0
		    || monster.maxDamageSpecial != 0
		    || monster.armorClass != 0
		    || monster.reducePlayerStrength != 0
		    || monster.reducePlayerMagic != 0
		    || monster.reducePlayerDexterity != 0
		    || monster.reducePlayerVitality != 0
		    || monster.reducePlayerMaxHP != 0
		    || monster.reducePlayerMaxMana != 0
		    || monster.flags != MFLAG_NO_ENEMY
		    || monster.isInvalid
		    || monster.lightId != NO_LIGHT
		    || monster.animInfo.ticksPerFrame != 0
		    || monster.animInfo.tickCounterOfCurrentFrame != 0
		    || monster.animInfo.numberOfFrames != 0
		    || monster.animInfo.currentFrame != 0
		    || monster.animInfo.isPetrified)
			throw std::runtime_error("fresh Cathedral monster slot contains nonzero state");
	}
}

void AssertFreshObjectSlots()
{
	if (ObjectPoolAdapter::ActiveObjectCountValue() != 0)
		throw std::runtime_error("fresh Cathedral object slots were not empty");
	for (int slot = 0; slot < MAXOBJECTS; slot++) {
		if (ObjectPoolAdapter::ActiveObjectIds()[slot] != 0
		    || ObjectPoolAdapter::AvailableObjectIds()[slot] != slot)
			throw std::runtime_error("fresh Cathedral object pool references were not initialized");
	}
	for (const Tile &tile : tiles().columnMajor()) {
		if (tile.object() != 0)
			throw std::runtime_error("fresh Cathedral object tile references were not empty");
	}
}

void ResetNativePools()
{
	FreeMonsters();
	InitializeObjectPool();
	for (int i = 0; i < MAXOBJECTS; i++)
		Objects[i] = {};
	ObjectPoolAdapter::ResetLegacyObjectPools();
	for (size_t index = 0; index < MaxLvlMTypes; index++)
		LevelBestiary.types[index] = {};
	InitializeMonsterPool();
	for (Monster &monster : MonsterPoolAdapter::AllMonsters())
		monster = {};
	for (Tile &tile : tiles().columnMajor()) {
		tile.setObject(0);
		tile.setMonster(0);
		tile.setPlayer(0);
	}
	CurrentLightManager.Init();
}

void PrepareGameAssets(std::string &error)
{
	try {
		TestInitGame();
		MyPlayerId = 0;
		gbIsHellfire = false;
		gbIsHellfireSaveGame = false;
		gbSndInited = false;
		UnloadModArchives();
		MpqArchives.erase(MainMpqPriority);
		gbIsSpawn = false;
		LoadGameArchives();
		gbIsHellfire = false;
		gbIsHellfireSaveGame = false;
		if (!HaveMainData() || gbIsSpawn) {
			error = "original DIABDAT.MPQ was not discovered";
			return;
		}
		currentLevel().setId(Level::create(LevelId { .levelNum = 1, .type = DTYPE_CATHEDRAL }).id());
		currentLevelNumber() = 1;
		levelType() = DTYPE_CATHEDRAL;
#ifndef UNPACKED_MPQS
		const auto &mainArchive = MpqArchives.at(MainMpqPriority);
		for (const std::string_view assetPath : { "levels\\l1data\\l1.til", "levels\\l1data\\l1.sol" }) {
			const AssetRef asset = FindAsset(assetPath);
			if (!asset.ok() || asset.archive != &mainArchive) {
				error = "Cathedral asset did not resolve from original DIABDAT.MPQ: " + std::string(assetPath);
				return;
			}
		}
#endif
		LoadObjectData();
		LoadMonsterData();
		size_t tileCount = 0;
		auto tileDefinitions = LoadFileInMemWithStatus<MegaTile>("levels\\l1data\\l1.til", &tileCount);
		if (!tileDefinitions.has_value() || tileDefinitions->get() == nullptr
		    || tileCount != static_cast<size_t>(GetTileCount(DTYPE_CATHEDRAL))) {
			error = "Cathedral TIL data could not be loaded";
			return;
		}
		megaTiles() = std::move(*tileDefinitions);
		for (int piece = 0; piece < MAXTILES; piece++)
			tileProperties()[piece] = TileProperties::None;
		if (!LoadLevelSOLData().has_value()) {
			error = "Cathedral SOL data could not be loaded";
			return;
		}
		if (!CurrentLightManager.LoadTrns().has_value()) {
			error = "Cathedral lighting data could not be loaded";
			return;
		}
		CurrentLightManager.MakeLightTable();
	} catch (const std::exception &exception) {
		error = exception.what();
	}
}

PopulationCapture CapturePopulation(int depth, uint32_t seed, lvl_entry entry, bool captureInitialInputs,
    NativeEncounterCapture *encounter = nullptr, NativeInitialItemCapture *initialItems = nullptr)
{
	ResetNativePools();
	currentLevel().setId(Level::create(LevelId { .levelNum = static_cast<uint8_t>(depth), .type = DTYPE_CATHEDRAL }).id());
	currentLevelNumber() = depth;
	levelType() = DTYPE_CATHEDRAL;
	LevelSeeds[depth] = std::nullopt;
	DungeonSeeds[depth] = seed;
	DungeonSeeds[16] = StoryDungeonSeed;
	MyPlayer->pOriginalCathedral = true;
	MyPlayer->plractive = true;
	MyPlayer->_pClass = HeroClass::Warrior;
	MyPlayer->plrlevel = static_cast<uint8_t>(depth);
	MyPlayer->_pLvlChanging = false;
	MyPlayer->plrIsOnSetLevel = false;
	// Controlled pre-object fixture context, not a complete created-hero state.
	MyPlayer->life = { 64, 64, 64, 64, 100 };
	gbIsMultiplayer = false;
	gbIsSpawn = false;
	gbIsHellfire = false;
	gbIsHellfireSaveGame = false;
	LoadingMapObjects = false;
	sgGameInitInfo.nDifficulty = DIFF_NORMAL;
	for (Quest &quest : Quests)
		quest._qactive = QUEST_NOTAVAIL;

	CreateLevelForTesting(entry);
	PopulationCapture capture;
	capture.depth = depth;
	capture.inputSeed = seed;
	capture.geometrySeed = seed;
	capture.populationSeed = seed;
	capture.entry = entry;
	capture.attemptSeed = LevelSeeds[depth].value();
	capture.rngAfterCreateLevel = GetLCGEngineState();
	capture.entryPosition = viewPosition();
	MyPlayer->position.tile = capture.entryPosition;
	MyPlayer->position.future = capture.entryPosition;
	MyPlayer->position.old = capture.entryPosition;
	MyPlayer->position.last = capture.entryPosition;
	capture.playerId = static_cast<int>(MyPlayer->getId());
	capture.playerClass = static_cast<int>(MyPlayer->_pClass);
	capture.playerLevel = MyPlayer->plrlevel;
	capture.playerActive = MyPlayer->plractive;
	capture.playerLevelChanging = MyPlayer->_pLvlChanging;
	capture.playerTile = MyPlayer->position.tile;
	capture.playerFuture = MyPlayer->position.future;
	capture.playerOld = MyPlayer->position.old;
	for (int i = 0; i < numTriggers(); i++)
		capture.triggers.push_back({ trigs[i].position, static_cast<int>(trigs[i]._tmsg), trigs[i]._tlvl });
	for (int y = 0; y < MAXDUNY; y++) {
		for (int x = 0; x < MAXDUNX; x++) {
			const Tile &tile = tileAt(x, y);
			capture.pieces.push_back(tile.piece());
			capture.transparency.push_back(static_cast<uint8_t>(tile.transVal()));
		}
	}
	for (int y = 0; y < DMAXY; y++) {
		for (int x = 0; x < DMAXX; x++) {
			if (megaTileAt(x, y).current() == 66)
				capture.stairsUp = { x - 1, y };
			if (megaTileAt(x, y).current() == 57)
				capture.stairsDown = { x - 1, y };
		}
	}
	for (int piece = 0; piece < MAXTILES; piece++)
		capture.solProperties.push_back(static_cast<uint8_t>(tileProperties()[piece]));

	InitializeMonsterPool();
	if (encounter != nullptr)
		encounter->preClearRandomState = GetLCGEngineState();
	InitLevelMonsters();
	if (encounter != nullptr)
		encounter->postClearRandomState = GetLCGEngineState();
	DungeonSeeds[depth] = seed;
	SetRndSeed(DungeonSeeds[depth]);
	if (encounter != nullptr) {
		encounter->depth = depth;
		encounter->inputSeed = seed;
		encounter->entry = entry;
		encounter->densityDivisor = GetMonsterDensityDivisor(depth);
		encounter->preThemeTileFlags = CaptureTileFlags();
	}
	if (captureInitialInputs) {
		capture.preThemeNextTransparency = static_cast<int>(nextTransparencyValue());
		capture.preThemePopulatedCells = CapturePopulatedFlags();
		capture.preThemePieces = CapturePieces();
		capture.preThemeSpecial = CaptureSpecial();
		capture.preThemeTransparency = CaptureTransparency();
		capture.preThemeSolProperties = CaptureSolProperties();
		AssertFreshObjectSlots();
		if (LevelBestiary.size() != 0)
			throw std::runtime_error("pre-selection Cathedral bestiary was not empty");
		for (const CMonster &type : LevelBestiary.types) {
			if (type.corpseId != 0 || type.placeFlags != 0)
				throw std::runtime_error("pre-selection Cathedral bestiary fields were not fresh");
			for (const AnimStruct &animation : type.anims) {
				if (animation.frames != 0 || animation.rate != 0 || animation.width != 0)
					throw std::runtime_error("pre-selection Cathedral animation slots were not fresh");
			}
		}
	}
	if (auto levelTypes = GetLevelMTypes(); !levelTypes.has_value())
		throw std::runtime_error(levelTypes.error());
	capture.rngAfterGetLevelMTypes = GetLCGEngineState();
	capture.bestiary = CaptureBestiary();
	InitThemes();
	capture.rngAfterInitThemes = GetLCGEngineState();
	if (captureInitialInputs) {
		if (numThemes() < 0 || numThemes() > MAXTHEMES)
			throw std::runtime_error("native selected theme count exceeds its pool");
		for (int index = 0; index < numThemes(); index++)
			capture.selectedThemes.push_back(themes[index]);
	}
	capture.populatedCellsBeforeHoldThemeRooms = CapturePopulatedCells();
	SetRndSeed(DungeonSeeds[depth]);
	capture.rngAfterPopulationReset = GetLCGEngineState();
	HoldThemeRooms();
	capture.populatedCellsAfterHoldThemeRooms = CapturePopulatedCells();
	capture.playerId = static_cast<int>(MyPlayer->getId());
	capture.playerLevel = MyPlayer->plrlevel;
	capture.playerActive = MyPlayer->plractive;
	capture.playerLevelChanging = MyPlayer->_pLvlChanging;
	capture.playerTile = MyPlayer->position.tile;
	capture.playerFuture = MyPlayer->position.future;
	capture.prePhasePieces = CapturePieces();
	capture.prePhaseSpecial = CaptureSpecial();
	capture.prePhaseTransparency = CaptureTransparency();
	capture.prePhaseSolProperties = CaptureSolProperties();
	capture.prePhasePopulatedCells = CapturePopulatedFlags();
	capture.prePhaseMonsterTileRefs = CaptureMonsterTileRefs();
	capture.prePhasePlayerTileRefs = CapturePlayerTileRefs();
	capture.prePhaseBestiary = CaptureBestiary();
	// The shared serialized grids are actual pre-theme inputs, not a post-phase replay.
	if (captureInitialInputs && (capture.preThemePieces != capture.prePhasePieces || capture.preThemeSpecial != capture.prePhaseSpecial || capture.preThemeTransparency != capture.prePhaseTransparency || capture.preThemeSolProperties != capture.prePhaseSolProperties))
		throw std::runtime_error("Cathedral prerequisites mutated their shared input geometry");
	AssertFreshObjectSlots();
	AssertFreshMonsterSlots();
	InitGolems();
	capture.rngAfterInitGolems = GetLCGEngineState();
	InitObjects();
	capture.rngAfterInitObjects = GetLCGEngineState();
	if (captureInitialInputs) {
		capture.finalPieces = CapturePieces();
		capture.finalSpecial = CaptureSpecial();
		capture.finalTransparency = CaptureTransparency();
	}

	capture.activeObjectCount = ObjectPoolAdapter::ActiveObjectCountValue();
	for (const int id : ObjectPoolAdapter::ActiveObjectIds())
		capture.activeObjectIds.push_back(id);
	for (const int id : ObjectPoolAdapter::AvailableObjectIds())
		capture.availableObjectIds.push_back(id);
	for (Object &object : ObjectPoolAdapter::ActiveObjectsRange()) {
		// Derive the physical slot independently of the tile reference GetId reads.
		const int slot = static_cast<int>(&object - Objects);
		if (object.GetId() != static_cast<unsigned int>(slot))
			throw std::runtime_error("native object tile reference does not identify its physical pool slot");
		capture.objects.push_back(CaptureObject(slot, object));
	}
	capture.activeMonsterCount = static_cast<int>(MonsterPoolAdapter::ActiveMonsterCountValue());
	for (size_t index = 0; index < MonsterPoolAdapter::ActiveMonsterCountValue(); index++)
		capture.activeMonsterIds.push_back(static_cast<int>(MonsterPoolAdapter::ActiveMonsterIds()[index]));
	for (const unsigned id : MonsterPoolAdapter::ActiveMonsterRange())
		capture.monsters.push_back(CaptureMonster(static_cast<int>(id), Monsters[id]));

	if (capture.activeObjectCount <= 0 || capture.activeObjectCount > MAXOBJECTS)
		throw std::runtime_error("native Cathedral InitObjects produced an invalid object count");
	if (capture.activeMonsterCount < InitialGolemSlots)
		throw std::runtime_error("native InitGolems did not reserve the expected monster slots");
	for (int slot = 0; slot < InitialGolemSlots; slot++) {
		if (capture.activeMonsterIds[slot] != slot || Monsters[slot].type().type != MT_GOLEM
		    || Monsters[slot].position.tile != GolemHoldingCell)
			throw std::runtime_error("native InitGolems did not retain the expected golem holding slots");
	}
	if (capture.triggers.empty() || capture.stairsUp.x < 0 || capture.stairsDown.x < 0)
		throw std::runtime_error("native Cathedral capture did not contain triggers and stairs");
	for (const NativeObjectCapture &object : capture.objects) {
		if (tileAt(object.position).object() != object.slot + 1)
			throw std::runtime_error("native object primary tile reference does not match its slot");
		if (object.type == OBJ_SARC && tileAt(Point { object.position.x, object.position.y - 1 }).object() != -(object.slot + 1))
			throw std::runtime_error("native sarcophagus north reference does not match its slot");
		const bool sarcophagus = object.type == OBJ_SARC && object.var1 >= 8;
		const bool barrel = object.type == OBJ_BARREL && object.var2 >= 8;
		if (!sarcophagus && !barrel)
			continue;
		const int reservation = sarcophagus ? object.var2 : object.var4;
		if (reservation < -1 || reservation >= MaxMonsters)
			throw std::runtime_error("object skeleton reservation is out of range");
		if (reservation == -1)
			continue;
		if (std::find(capture.activeMonsterIds.begin(), capture.activeMonsterIds.end(), reservation)
		        == capture.activeMonsterIds.end()
		    || !IsSkel(Monsters[reservation].type().type)
		    || Monsters[reservation].position.tile != Point { 0, 0 })
			throw std::runtime_error("object skeleton reservation is not an active skeleton");
	}
	for (int y = 0; y < MAXDUNY; y++)
		for (int x = 0; x < MAXDUNX; x++)
			capture.tileObjectRefs.push_back(tileAt(x, y).object());
	if (encounter != nullptr) {
		encounter->preState = CaptureEncounterState();
		if (auto result = InitMonsters(); !result.has_value())
			throw std::runtime_error(result.error());
		encounter->expectedState = CaptureEncounterState();
		if (encounter->expectedState.objectTileRefs != encounter->preState.objectTileRefs
		    || encounter->expectedState.playerTileRefs != encounter->preState.playerTileRefs)
			throw std::runtime_error("native InitMonsters mutated unrelated occupancy channels");
	}
	if (initialItems != nullptr) {
		if (encounter == nullptr)
			throw std::runtime_error("initial items require the composed monster encounter phase");
		// Derive fresh prior storage independently of native expected output. Keep
		// this reset after the frozen earlier captures so their bytes do not change.
		InitializeItemPool();
		for (int slot = 0; slot <= MAXITEMS; slot++)
			Items[slot] = {};
		ItemPoolAdapter::ResetLegacyItemPools();
		for (Tile &tile : tiles().columnMajor())
			tile.setItem(0);
		initialItems->depth = depth;
		initialItems->inputSeed = seed;
		initialItems->entry = entry;
		initialItems->preState = CaptureInitialItemState();
		InitItems();
		initialItems->expectedState = CaptureInitialItemState();
		const auto &pre = initialItems->preState;
		const auto &post = initialItems->expectedState;
		if (pre.pieces != post.pieces || pre.tileFlags != post.tileFlags
		    || pre.monsterTileRefs != post.monsterTileRefs
		    || pre.playerTileRefs != post.playerTileRefs || pre.objectTileRefs != post.objectTileRefs)
			throw std::runtime_error("native InitItems mutated unrelated world channels");
	}
	return capture;
}

void WriteCompleteEncounterMonster(std::ostream &stream, const NativeMonsterCapture &monster)
{
	std::ostringstream original;
	WriteMonster(original, monster);
	const std::string fields = original.str();
	stream << fields.substr(0, fields.size() - 1) << ",\"temp\":";
	WritePoint(stream, monster.temp);
	stream << ",\"golemToHit\":" << monster.golemToHit
	       << ",\"reducePlayerStrength\":" << monster.reducePlayerStrength
	       << ",\"reducePlayerMagic\":" << monster.reducePlayerMagic
	       << ",\"reducePlayerDexterity\":" << monster.reducePlayerDexterity
	       << ",\"reducePlayerVitality\":" << monster.reducePlayerVitality
	       << ",\"reducePlayerMaxHP\":" << monster.reducePlayerMaxHP
	       << ",\"reducePlayerMaxMana\":" << monster.reducePlayerMaxMana << '}';
}

void WriteEncounterState(std::ostream &stream, const NativeEncounterState &state)
{
	stream << "{\"randomState\":" << state.randomState
	       << ",\"totalMonsters\":" << state.totalMonsters
	       << ",\"activeMonsterCount\":" << state.activeMonsterCount
	       << ",\"activeMonsterIds\":";
	WriteArray(stream, state.activeMonsterIds);
	stream << ",\"monsters\":[";
	for (size_t slot = 0; slot < state.monsters.size(); slot++) {
		if (slot != 0)
			stream << ',';
		WriteCompleteEncounterMonster(stream, state.monsters[slot]);
	}
	stream << "],\"monsterTileRefs\":";
	WriteArray(stream, state.monsterTileRefs);
	stream << ",\"playerTileRefs\":";
	WriteArray(stream, state.playerTileRefs);
	stream << ",\"objectTileRefs\":";
	WriteArray(stream, state.objectTileRefs);
	stream << ",\"tileFlags\":";
	WriteArray(stream, state.tileFlags);
	stream << ",\"visibleTransparencyRegions\":";
	WriteBoolArray(stream, state.visibleTransparencyRegions);
	stream << ",\"lightAllocator\":{\"activeLightSlots\":";
	WriteArray(stream, state.activeLightSlots);
	stream << ",\"activeLightCount\":" << state.activeLightCount
	       << ",\"updateLighting\":" << (state.updateLighting ? "true" : "false")
	       << ",\"disableLighting\":" << (state.disableLighting ? "true" : "false")
	       << ",\"lights\":[";
	for (size_t slot = 0; slot < state.lights.size(); slot++) {
		if (slot != 0)
			stream << ',';
		const Light &light = state.lights[slot];
		stream << "{\"slot\":" << slot << ",\"tile\":";
		WritePoint(stream, { light.position.tile.x, light.position.tile.y });
		stream << ",\"offset\":";
		WritePoint(stream, { light.position.offset.deltaX, light.position.offset.deltaY });
		stream << ",\"old\":";
		WritePoint(stream, { light.position.old.x, light.position.old.y });
		stream << ",\"radius\":" << static_cast<int>(light.radius)
		       << ",\"oldRadius\":" << static_cast<int>(light.oldRadius)
		       << ",\"invalid\":" << (light.isInvalid ? "true" : "false")
		       << ",\"changed\":" << (light.hasChanged ? "true" : "false") << '}';
	}
	stream << "]}}";
}

void WriteInitialItem(std::ostream &stream, const Item &item)
{
	stream << '{';
	stream << "\"seed\":";
	stream << static_cast<int64_t>(item._iSeed);
	stream << ",\"createInfo\":";
	stream << static_cast<int64_t>(item._iCreateInfo);
	stream << ",\"itemType\":";
	stream << static_cast<int64_t>(item._itype);
	stream << ",\"animFlag\":";
	stream << (item._iAnimFlag ? "true" : "false");
	stream << ",\"positionX\":";
	stream << static_cast<int64_t>(item.position.x);
	stream << ",\"positionY\":";
	stream << static_cast<int64_t>(item.position.y);
	stream << ",\"deleted\":";
	stream << (item._iDelFlag ? "true" : "false");
	stream << ",\"selectionRegion\":";
	stream << static_cast<int64_t>(item.selectionRegion);
	stream << ",\"postDraw\":";
	stream << (item._iPostDraw ? "true" : "false");
	stream << ",\"identified\":";
	stream << (item._iIdentified ? "true" : "false");
	stream << ",\"magical\":";
	stream << static_cast<int64_t>(item._iMagical);
	stream << ",\"baseName\":";
	WriteInitialItemJsonString(stream, item._iName);
	stream << ",\"identifiedName\":";
	WriteInitialItemJsonString(stream, item._iIName);
	stream << ",\"equipLocation\":";
	stream << static_cast<int64_t>(item._iLoc);
	stream << ",\"itemClass\":";
	stream << static_cast<int64_t>(item._iClass);
	stream << ",\"cursorGraphic\":";
	stream << static_cast<int64_t>(item._iCurs);
	stream << ",\"value\":";
	stream << static_cast<int64_t>(item._ivalue);
	stream << ",\"identifiedValue\":";
	stream << static_cast<int64_t>(item._iIvalue);
	stream << ",\"minDamage\":";
	stream << static_cast<int64_t>(item._iMinDam);
	stream << ",\"maxDamage\":";
	stream << static_cast<int64_t>(item._iMaxDam);
	stream << ",\"armorClass\":";
	stream << static_cast<int64_t>(item._iAC);
	stream << ",\"flags\":";
	stream << static_cast<uint32_t>(item._iFlags);
	stream << ",\"miscId\":";
	stream << static_cast<int64_t>(item._iMiscId);
	stream << ",\"spellId\":";
	stream << static_cast<int64_t>(item._iSpell);
	stream << ",\"itemIndex\":";
	stream << static_cast<int64_t>(item.IDidx);
	stream << ",\"charges\":";
	stream << static_cast<int64_t>(item._iCharges);
	stream << ",\"maxCharges\":";
	stream << static_cast<int64_t>(item._iMaxCharges);
	stream << ",\"durability\":";
	stream << static_cast<int64_t>(item._iDurability);
	stream << ",\"maxDurability\":";
	stream << static_cast<int64_t>(item._iMaxDur);
	stream << ",\"plusDamage\":";
	stream << static_cast<int64_t>(item._iPLDam);
	stream << ",\"plusToHit\":";
	stream << static_cast<int64_t>(item._iPLToHit);
	stream << ",\"plusArmorClass\":";
	stream << static_cast<int64_t>(item._iPLAC);
	stream << ",\"plusStrength\":";
	stream << static_cast<int64_t>(item._iPLStr);
	stream << ",\"plusMagic\":";
	stream << static_cast<int64_t>(item._iPLMag);
	stream << ",\"plusDexterity\":";
	stream << static_cast<int64_t>(item._iPLDex);
	stream << ",\"plusVitality\":";
	stream << static_cast<int64_t>(item._iPLVit);
	stream << ",\"plusFireResistance\":";
	stream << static_cast<int64_t>(item._iPLFR);
	stream << ",\"plusLightningResistance\":";
	stream << static_cast<int64_t>(item._iPLLR);
	stream << ",\"plusMagicResistance\":";
	stream << static_cast<int64_t>(item._iPLMR);
	stream << ",\"plusMana\":";
	stream << static_cast<int64_t>(item._iPLMana);
	stream << ",\"plusHitPoints\":";
	stream << static_cast<int64_t>(item._iPLHP);
	stream << ",\"plusDamageModifier\":";
	stream << static_cast<int64_t>(item._iPLDamMod);
	stream << ",\"plusGetHit\":";
	stream << static_cast<int64_t>(item._iPLGetHit);
	stream << ",\"plusLight\":";
	stream << static_cast<int64_t>(item._iPLLight);
	stream << ",\"spellLevelAdd\":";
	stream << static_cast<int64_t>(item._iSplLvlAdd);
	stream << ",\"uniqueId\":";
	stream << static_cast<int64_t>(item._iUid);
	stream << ",\"fireMinDamage\":";
	stream << static_cast<int64_t>(item._iFMinDam);
	stream << ",\"fireMaxDamage\":";
	stream << static_cast<int64_t>(item._iFMaxDam);
	stream << ",\"lightningMinDamage\":";
	stream << static_cast<int64_t>(item._iLMinDam);
	stream << ",\"lightningMaxDamage\":";
	stream << static_cast<int64_t>(item._iLMaxDam);
	stream << ",\"plusEnemyArmorClass\":";
	stream << static_cast<int64_t>(item._iPLEnAc);
	stream << ",\"prefixPower\":";
	stream << static_cast<int64_t>(item._iPrePower);
	stream << ",\"suffixPower\":";
	stream << static_cast<int64_t>(item._iSufPower);
	stream << ",\"valueAdd1\":";
	stream << static_cast<int64_t>(item._iVAdd1);
	stream << ",\"valueMultiply1\":";
	stream << static_cast<int64_t>(item._iVMult1);
	stream << ",\"valueAdd2\":";
	stream << static_cast<int64_t>(item._iVAdd2);
	stream << ",\"valueMultiply2\":";
	stream << static_cast<int64_t>(item._iVMult2);
	stream << ",\"minimumStrength\":";
	stream << static_cast<int64_t>(item._iMinStr);
	stream << ",\"minimumMagic\":";
	stream << static_cast<int64_t>(item._iMinMag);
	stream << ",\"minimumDexterity\":";
	stream << static_cast<int64_t>(item._iMinDex);
	stream << ",\"statFlag\":";
	stream << (item._iStatFlag ? "true" : "false");
	stream << ",\"request\":";
	stream << (item._iRequest ? "true" : "false");
	stream << ",\"hellfireDamageArmorFlags\":";
	stream << static_cast<uint32_t>(item._iDamAcFlags);
	stream << ",\"buff\":";
	stream << static_cast<uint32_t>(item.dwBuff);
	stream << ",\"animation\":{\"ticksPerFrame\":" << static_cast<int>(item.animInfo.ticksPerFrame)
	       << ",\"tickCounterOfCurrentFrame\":" << static_cast<int>(item.animInfo.tickCounterOfCurrentFrame)
	       << ",\"numberOfFrames\":" << static_cast<int>(item.animInfo.numberOfFrames)
	       << ",\"currentFrame\":" << static_cast<int>(item.animInfo.currentFrame)
	       << ",\"isPetrified\":" << (item.animInfo.isPetrified ? "true" : "false") << "}}";
}

void WriteInitialItemState(std::ostream &stream, const NativeInitialItemState &state)
{
	stream << "{\"randomState\":" << state.randomState << ",\"activeItemCount\":" << state.activeItemCount
	       << ",\"activeItemIds\":";
	WriteArray(stream, state.activeItemIds);
	stream << ",\"items\":[";
	for (size_t slot = 0; slot < state.items.size(); slot++) {
		if (slot != 0) stream << ',';
		WriteInitialItem(stream, state.items[slot]);
	}
	stream << "],\"itemTileRefs\":";
	WriteArray(stream, state.itemTileRefs);
	stream << ",\"pieces\":";
	WriteArray(stream, state.pieces);
	stream << ",\"tileFlags\":";
	WriteArray(stream, state.tileFlags);
	stream << ",\"monsterTileRefs\":";
	WriteArray(stream, state.monsterTileRefs);
	stream << ",\"playerTileRefs\":";
	WriteArray(stream, state.playerTileRefs);
	stream << ",\"objectTileRefs\":";
	WriteArray(stream, state.objectTileRefs);
	stream << '}';
}

void WriteInitialItemInputs(const std::filesystem::path &path, const std::vector<NativeInitialItemCapture> &captures)
{
	std::ofstream stream(path, std::ios::binary | std::ios::trunc);
	if (!stream.good()) throw std::runtime_error("unable to open native initial-item sidecar");
	stream << "{\"format\":\"" << InitialItemFormat << "\",\"version\":1,\"policy\":\""
	       << InitialItemPolicy << "\",\"caseCount\":" << PopulationCaseCount << ",\"potionDefinitions\":[";
	const std::array<_item_indexes, 2> potionIds { IDI_MANA, IDI_HEAL };
	for (size_t index = 0; index < potionIds.size(); index++) {
		if (index != 0) stream << ',';
		const auto id = potionIds[index];
		const ItemData &row = AllItemsList.at(static_cast<size_t>(id));
		Item probe {};
		probe._iCurs = row.iCurs;
		probe.setNewAnimation(false);
		stream << '{';
		stream << "\"nativeItemId\":";
		stream << static_cast<int>(id);
		stream << ",\"itemType\":";
		stream << static_cast<int>(row.itype);
		stream << ",\"cursorGraphic\":";
		stream << static_cast<int>(row.iCurs);
		stream << ",\"name\":";
		WriteInitialItemJsonString(stream, row.iName);
		stream << ",\"identifiedName\":";
		WriteInitialItemJsonString(stream, row.iName);
		stream << ",\"equipLocation\":";
		stream << static_cast<int>(row.iLoc);
		stream << ",\"itemClass\":";
		stream << static_cast<int>(row.iClass);
		stream << ",\"minDamage\":";
		stream << static_cast<int>(row.iMinDam);
		stream << ",\"maxDamage\":";
		stream << static_cast<int>(row.iMaxDam);
		stream << ",\"minArmorClass\":";
		stream << static_cast<int>(row.iMinAC);
		stream << ",\"maxArmorClass\":";
		stream << static_cast<int>(row.iMaxAC);
		stream << ",\"flags\":";
		stream << static_cast<uint32_t>(row.iFlags);
		stream << ",\"miscId\":";
		stream << static_cast<int>(row.iMiscId);
		stream << ",\"spellId\":";
		stream << static_cast<int>(row.iSpell);
		stream << ",\"value\":";
		stream << row.iValue;
		stream << ",\"durability\":";
		stream << static_cast<int>(row.iDurability);
		stream << ",\"minimumStrength\":";
		stream << static_cast<int>(row.iMinStr);
		stream << ",\"minimumMagic\":";
		stream << static_cast<int>(row.iMinMag);
		stream << ",\"minimumDexterity\":";
		stream << static_cast<int>(row.iMinDex);
		stream << ",\"animationFrameCount\":";
		stream << static_cast<int>(probe.animInfo.numberOfFrames);
		stream << ",\"animationTicksPerFrame\":";
		stream << static_cast<int>(probe.animInfo.ticksPerFrame);
		stream << ",\"buff\":";
		stream << 0;
		stream << '}';
	}
	stream << "],\"cases\":[";
	for (size_t index = 0; index < captures.size(); index++) {
		if (index != 0) stream << ',';
		const auto &capture = captures[index];
		stream << "{\"snapshot\":\"cathedral-population-depth-" << capture.depth << '-' << capture.inputSeed
		       << '-' << EntryName(capture.entry) << ".json\",\"depth\":" << capture.depth
		       << ",\"inputSeed\":" << capture.inputSeed << ",\"entry\":\"" << EntryName(capture.entry)
		       << "\",\"preState\":";
		WriteInitialItemState(stream, capture.preState);
		stream << ",\"expectedState\":";
		WriteInitialItemState(stream, capture.expectedState);
		stream << '}';
	}
	stream << "]}\n";
	if (!stream.good()) throw std::runtime_error("native initial-item sidecar write failed");
}

void WriteEncounterInputs(const std::filesystem::path &path, const std::vector<NativeEncounterCapture> &captures)
{
	if (UniqueMonstersData.size() != 100)
		throw std::runtime_error("the ordinary encounter oracle requires the complete 100-row native unique catalog");
	std::ofstream stream(path, std::ios::binary | std::ios::trunc);
	if (!stream.good())
		throw std::runtime_error("unable to open native encounter input sidecar");
	stream << "{\n\"format\":\"" << EncounterInputFormat << "\",\n\"version\":1,\n\"policy\":\""
	       << EncounterInputPolicy << "\",\n\"caseCount\":" << PopulationCaseCount << ",\n\"uniqueDefinitions\":[";
	for (size_t id = 0; id < UniqueMonstersData.size(); id++) {
		if (id != 0)
			stream << ',';
		const UniqueMonsterData &row = UniqueMonstersData[id];
		const bool questOnly = id == 0 || id == 2 || id == 3 || id == 7 || id == 8;
		stream << "{\"nativeUniqueId\":" << id
		       << ",\"monsterType\":" << static_cast<int>(row.mtype)
		       << ",\"dungeonLevel\":" << static_cast<int>(row.mlevel)
		       << ",\"maximumHitPoints\":" << row.mmaxhp
		       << ",\"ai\":" << static_cast<int>(row.mAi)
		       << ",\"intelligence\":" << static_cast<int>(row.mint)
		       << ",\"minDamage\":" << static_cast<int>(row.mMinDamage)
		       << ",\"maxDamage\":" << static_cast<int>(row.mMaxDamage)
		       << ",\"resistance\":" << row.mMagicRes
		       << ",\"reducePlayerStrength\":" << static_cast<int>(row.reducePlayerStrength)
		       << ",\"reducePlayerMagic\":" << static_cast<int>(row.reducePlayerMagic)
		       << ",\"reducePlayerDexterity\":" << static_cast<int>(row.reducePlayerDexterity)
		       << ",\"reducePlayerVitality\":" << static_cast<int>(row.reducePlayerVitality)
		       << ",\"reducePlayerMaxHP\":" << static_cast<int>(row.reducePlayerMaxHP)
		       << ",\"reducePlayerMaxMana\":" << static_cast<int>(row.reducePlayerMaxMana)
		       << ",\"monsterPack\":" << static_cast<int>(row.monsterPack)
		       << ",\"customToHit\":" << static_cast<int>(row.customToHit)
		       << ",\"customArmorClass\":" << static_cast<int>(row.customArmorClass)
		       << ",\"talkMessage\":" << static_cast<int>(row.mtalkmsg)
		       << ",\"questOnly\":" << (questOnly ? "true" : "false") << '}';
	}
	stream << "],\n\"cases\":[";
	for (size_t index = 0; index < captures.size(); index++) {
		if (index != 0)
			stream << ',';
		const NativeEncounterCapture &capture = captures[index];
		stream << "{\"snapshot\":\"cathedral-population-depth-" << capture.depth << '-' << capture.inputSeed
		       << '-' << EntryName(capture.entry) << ".json\",\"depth\":" << capture.depth
		       << ",\"inputSeed\":" << capture.inputSeed << ",\"entry\":\"" << EntryName(capture.entry)
		       << "\",\"densityDivisor\":" << capture.densityDivisor
		       << ",\"preClearRandomState\":" << capture.preClearRandomState
		       << ",\"postClearRandomState\":" << capture.postClearRandomState
		       << ",\"preThemeTileFlags\":";
		WriteArray(stream, capture.preThemeTileFlags);
		stream << ",\"preState\":";
		WriteEncounterState(stream, capture.preState);
		stream << ",\"expectedState\":";
		WriteEncounterState(stream, capture.expectedState);
		stream << '}';
	}
	stream << "]}\n";
	stream.flush();
	if (!stream.good())
		throw std::runtime_error("unable to finish native encounter input sidecar");
}

void WritePopulationCase(const std::filesystem::path &path, const PopulationCapture &capture)
{
	std::ofstream stream(path, std::ios::binary | std::ios::trunc);
	if (!stream.good())
		throw std::runtime_error("unable to open population oracle case: " + path.string());
	stream << "{\n\"format\":\"" << PopulationOracleFormat
	       << "\",\n\"version\":" << PopulationOracleVersion
	       << ",\n\"policy\":\"" << PopulationOraclePolicy
	       << "\",\n\"depth\":" << capture.depth
	       << ",\n\"inputSeed\":" << capture.inputSeed
	       << ",\n\"geometrySeed\":" << capture.geometrySeed
	       << ",\n\"populationSeed\":" << capture.populationSeed
	       << ",\n\"storySeed\":" << StoryDungeonSeed
	       << ",\n\"entry\":\"" << EntryName(capture.entry)
	       << "\",\n\"attemptSeed\":" << capture.attemptSeed
	       << ",\n\"rngAfterCreateLevel\":" << capture.rngAfterCreateLevel
	       << ",\n\"rngAfterPopulationReset\":" << capture.rngAfterPopulationReset
	       << ",\n\"rngAfterGetLevelMTypes\":" << capture.rngAfterGetLevelMTypes
	       << ",\n\"rngAfterInitThemes\":" << capture.rngAfterInitThemes
	       << ",\n\"rngAfterInitGolems\":" << capture.rngAfterInitGolems
	       << ",\n\"rngAfterInitObjects\":" << capture.rngAfterInitObjects
	       << ",\n\"entryPosition\":";
	WritePoint(stream, capture.entryPosition);
	stream << ",\n\"stairsUp\":";
	WritePoint(stream, capture.stairsUp);
	stream << ",\n\"stairsDown\":";
	WritePoint(stream, capture.stairsDown);
	stream << ",\n\"triggers\":[";
	for (size_t i = 0; i < capture.triggers.size(); i++) {
		if (i != 0)
			stream << ',';
		stream << "{\"position\":";
		WritePoint(stream, capture.triggers[i].position);
		stream << ",\"message\":" << capture.triggers[i].message
		       << ",\"level\":" << capture.triggers[i].level << '}';
	}
	stream << "],\n\"populatedCellsBeforeHoldThemeRooms\":";
	WriteArray(stream, capture.populatedCellsBeforeHoldThemeRooms);
	stream << ",\n\"populatedCellsAfterHoldThemeRooms\":";
	WriteArray(stream, capture.populatedCellsAfterHoldThemeRooms);
	stream << ",\n\"pieces\":";
	WriteArray(stream, capture.pieces);
	stream << ",\n\"transparency\":";
	WriteArray(stream, capture.transparency);
	stream << ",\n\"solProperties\":";
	WriteArray(stream, capture.solProperties);
	stream << ",\n\"activeObjectCount\":" << capture.activeObjectCount
	       << ",\n\"activeObjectIds\":";
	WriteArray(stream, capture.activeObjectIds);
	stream << ",\n\"availableObjectIds\":";
	WriteArray(stream, capture.availableObjectIds);
	stream << ",\n\"tileObjectRefs\":";
	WriteArray(stream, capture.tileObjectRefs);
	stream << ",\n\"objects\":[";
	for (size_t i = 0; i < capture.objects.size(); i++) {
		if (i != 0)
			stream << ',';
		WriteObject(stream, capture.objects[i]);
	}
	stream << "],\n\"activeMonsterCount\":" << capture.activeMonsterCount
	       << ",\n\"activeMonsterIds\":";
	WriteArray(stream, capture.activeMonsterIds);
	stream << ",\n\"monsters\":[";
	for (size_t i = 0; i < capture.monsters.size(); i++) {
		if (i != 0)
			stream << ',';
		WriteMonster(stream, capture.monsters[i]);
	}
	stream << "],\n\"bestiary\":";
	WriteBestiary(stream, capture.bestiary);
	stream << ",\n\"playerContext\":{\"id\":" << capture.playerId
	       << ",\"class\":" << capture.playerClass
	       << ",\"level\":" << capture.playerLevel
	       << ",\"active\":" << (capture.playerActive ? "true" : "false")
	       << ",\"levelChanging\":" << (capture.playerLevelChanging ? "true" : "false")
	       << ",\"tile\":";
	WritePoint(stream, capture.playerTile);
	stream << ",\"future\":";
	WritePoint(stream, capture.playerFuture);
	stream << ",\"old\":";
	WritePoint(stream, capture.playerOld);
	stream << "}\n}\n";
	stream.close();
	if (!stream.good())
		throw std::runtime_error("unable to write population oracle case: " + path.string());
}

void WriteMetadata(const std::filesystem::path &path)
{
	std::ofstream stream(path, std::ios::binary | std::ios::trunc);
	if (!stream.good())
		throw std::runtime_error("unable to open population oracle metadata: " + path.string());
	stream << "{\n\"format\":\"" << PopulationOracleFormat
	       << "\",\n\"version\":" << PopulationOracleVersion
	       << ",\n\"policy\":\"" << PopulationOraclePolicy
	       << "\",\n\"nativeWidth\":" << MAXDUNX
	       << ",\n\"nativeHeight\":" << MAXDUNY
	       << ",\n\"rowMajor\":true"
	       << ",\n\"headless\":true,\"soundInitialized\":false"
	       << ",\n\"game\":\"diablo\",\"gameMode\":\"singleplayer\",\"difficulty\":\"normal\",\"playerLifeQ6\":64"
	       << ",\n\"caseCount\":" << PopulationCaseCount
	       << ",\n\"depths\":[1,2,3,4]"
	       << ",\n\"seeds\":[2588,743271966]"
	       << ",\n\"entries\":[\"main\",\"previous\"]"
	       << ",\n\"storySeed\":" << StoryDungeonSeed
	       << ",\n\"objectFields\":[\"type\",\"position\",\"applyLighting\",\"animFlag\",\"animDelay\",\"animCount\",\"animLength\",\"animFrame\",\"animWidth\",\"deleteFlag\",\"breakFlag\",\"solidFlag\",\"missilePassable\",\"selectionRegion\",\"preFlag\",\"trapFlag\",\"doorFlag\",\"lightId\",\"rndSeed\",\"var1\",\"var2\",\"var3\",\"var4\",\"var5\",\"var6\",\"bookMessage\",\"var8\"]"
	       << ",\n\"monsterFields\":[\"type\",\"levelType\",\"mode\",\"goal\",\"goalVar1\",\"goalVar2\",\"goalVar3\",\"var1\",\"var2\",\"var3\",\"pathCount\",\"tile\",\"future\",\"old\",\"last\",\"direction\",\"enemy\",\"enemyPosition\",\"invalid\",\"ai\",\"intelligence\",\"activeForTicks\",\"uniqueType\",\"uniqueTranslation\",\"corpseId\",\"whoHit\",\"minDamage\",\"maxDamage\",\"minDamageSpecial\",\"maxDamageSpecial\",\"armorClass\",\"resistance\",\"talkMessage\",\"leader\",\"leaderRelation\",\"packSize\",\"flags\",\"rndItemSeed\",\"aiSeed\",\"lightId\",\"hitPoints\",\"maxHitPoints\",\"animTicksPerFrame\",\"animTickCounter\",\"animNumberOfFrames\",\"animCurrentFrame\"]"
	       << ",\n\"rngBoundaries\":[\"rngAfterCreateLevel\",\"rngAfterPopulationReset\",\"rngAfterGetLevelMTypes\",\"rngAfterInitThemes\",\"rngAfterInitGolems\",\"rngAfterInitObjects\"]"
	       << ",\n\"nativeObjectTypes\":{\"sarcophagus\":" << static_cast<int>(OBJ_SARC)
	       << ",\"barrel\":" << static_cast<int>(OBJ_BARREL) << '}'
	       << ",\n\"nativeObjectCatalog\":[";
	for (size_t index = 0; index < AllObjects.size(); index++) {
		if (index != 0)
			stream << ',';
		const ObjectData &object = AllObjects[index];
		stream << "{\"nativeObjectId\":" << index
		       << ",\"graphicIndex\":" << static_cast<int>(object.ofindex)
		       << ",\"minLevel\":" << static_cast<int>(object.minlvl)
		       << ",\"maxLevel\":" << static_cast<int>(object.maxlvl)
		       << ",\"levelType\":" << static_cast<int>(object.olvltype)
		       << ",\"theme\":" << static_cast<int>(object.otheme)
		       << ",\"quest\":" << static_cast<int>(object.oquest)
		       << ",\"flags\":" << static_cast<int>(object.flags)
		       << ",\"animDelay\":" << static_cast<int>(object.animDelay)
		       << ",\"animLength\":" << static_cast<int>(object.animLen)
		       << ",\"animWidth\":" << static_cast<int>(object.animWidth)
		       << ",\"selectionRegion\":" << static_cast<int>(object.selectionRegion)
		       << ",\"behavior\":";
		WriteJsonString(stream, object.behavior);
		stream << '}';
	}
	stream << "]\n}\n";
	stream.close();
	if (!stream.good())
		throw std::runtime_error("unable to write population oracle metadata: " + path.string());
}

void WriteNativeMonsterCatalog(std::ostream &stream)
{
	stream << '[';
	for (size_t index = 0; index < MonstersData.size(); index++) {
		if (index != 0)
			stream << ',';
		const MonsterData &monster = MonstersData[index];
		stream << "{\"nativeMonsterId\":" << index << ",\"name\":";
		WriteJsonString(stream, monster.name);
		stream << ",\"availability\":" << static_cast<int>(monster.availability)
		       << ",\"image\":" << monster.image
		       << ",\"minDungeonLevel\":" << static_cast<int>(monster.minDunLvl)
		       << ",\"maxDungeonLevel\":" << static_cast<int>(monster.maxDunLvl)
		       << ",\"level\":" << static_cast<int>(monster.level)
		       << ",\"minHp\":" << monster.hitPointsMinimum
		       << ",\"maxHp\":" << monster.hitPointsMaximum
		       << ",\"ai\":" << static_cast<int>(monster.ai)
		       << ",\"intelligence\":" << static_cast<int>(monster.intelligence)
		       << ",\"minDamage\":" << static_cast<int>(monster.minDamage)
		       << ",\"maxDamage\":" << static_cast<int>(monster.maxDamage)
		       << ",\"minDamageSpecial\":" << static_cast<int>(monster.minDamageSpecial)
		       << ",\"maxDamageSpecial\":" << static_cast<int>(monster.maxDamageSpecial)
		       << ",\"armorClass\":" << static_cast<int>(monster.armorClass)
		       << ",\"resistance\":" << static_cast<int>(monster.resistance)
		       << ",\"resistanceHell\":" << static_cast<int>(monster.resistanceHell)
		       << ",\"abilityFlags\":" << monster.abilityFlags
		       << ",\"hasSpecial\":" << (monster.hasSpecial ? "true" : "false")
		       << ",\"width\":" << monster.width
		       << ",\"frames\":[";
		for (size_t animation = 0; animation < 6; animation++) {
			if (animation != 0)
				stream << ',';
			stream << static_cast<int>(monster.frames[animation]);
		}
		stream << "],\"rates\":[";
		for (size_t animation = 0; animation < 6; animation++) {
			if (animation != 0)
				stream << ',';
			stream << static_cast<int>(monster.rate[animation]);
		}
		stream << "],\"reducePlayerStrength\":" << static_cast<int>(monster.reducePlayerStrength)
		       << ",\"reducePlayerMagic\":" << static_cast<int>(monster.reducePlayerMagic)
		       << ",\"reducePlayerDexterity\":" << static_cast<int>(monster.reducePlayerDexterity)
		       << ",\"reducePlayerVitality\":" << static_cast<int>(monster.reducePlayerVitality)
		       << ",\"reducePlayerMaxHP\":" << static_cast<int>(monster.reducePlayerMaxHP)
		       << ",\"reducePlayerMaxMana\":" << static_cast<int>(monster.reducePlayerMaxMana) << '}';
	}
	stream << ']';
}

void WriteMonsterSupplemental(std::ostream &stream, const NativeMonsterCapture &monster)
{
	stream << "{\"slot\":" << monster.slot
	       << ",\"temp\":";
	WritePoint(stream, monster.temp);
	stream << ",\"golemToHit\":" << monster.golemToHit
	       << ",\"reducePlayerStrength\":" << monster.reducePlayerStrength
	       << ",\"reducePlayerMagic\":" << monster.reducePlayerMagic
	       << ",\"reducePlayerDexterity\":" << monster.reducePlayerDexterity
	       << ",\"reducePlayerVitality\":" << monster.reducePlayerVitality
	       << ",\"reducePlayerMaxHP\":" << monster.reducePlayerMaxHP
	       << ",\"reducePlayerMaxMana\":" << monster.reducePlayerMaxMana << '}';
}

void WriteNativeInputs(const std::filesystem::path &path, const std::vector<PopulationCapture> &captures)
{
	std::ofstream stream(path, std::ios::binary | std::ios::trunc);
	if (!stream.good())
		throw std::runtime_error("unable to open population input sidecar: " + path.string());
	stream << "{\n\"format\":\"" << PopulationInputFormat
	       << "\",\n\"version\":" << PopulationInputVersion
	       << ",\n\"policy\":\"" << PopulationInputPolicy
	       << "\",\n\"caseCount\":" << PopulationCaseCount
	       << ",\n\"levelGenerationTableLoaded\":true"
	       << ",\n\"freshBestiarySlots\":true"
	       << ",\n\"storyMessages\":[" << static_cast<int>(TEXT_BOOK11)
	       << ',' << static_cast<int>(TEXT_BOOK21)
	       << ',' << static_cast<int>(TEXT_BOOK31) << ']'
	       << ",\n\"constants\":{\"golemNativeType\":" << static_cast<int>(MT_GOLEM)
	       << ",\"southDirection\":" << static_cast<int>(Direction::South)
	       << ",\"standMode\":" << static_cast<int>(MonsterMode::Stand)
	       << ",\"normalGoal\":" << static_cast<int>(MonsterGoal::Normal)
	       << ",\"specialAttackMode\":" << static_cast<int>(MonsterMode::SpecialMeleeAttack)
	       << ",\"noUniqueType\":" << static_cast<int>(UniqueMonsterType::None)
	       << ",\"noLeader\":" << static_cast<int>(Monster::NoLeader)
	       << ",\"noLight\":" << NO_LIGHT
	       << ",\"textNone\":" << static_cast<int>(TEXT_NONE)
	       << ",\"monsterNoEnemyFlag\":" << MFLAG_NO_ENEMY
	       << ",\"golemFlag\":" << MFLAG_GOLEM
	       << ",\"berserkFlag\":" << MFLAG_BERSERK
	       << ",\"targetsMonsterFlag\":" << MFLAG_TARGETS_MONSTER
	       << ",\"lockAnimationFlag\":" << MFLAG_LOCK_ANIMATION
	       << ",\"allowSpecialFlag\":" << MFLAG_ALLOW_SPECIAL
	       << ",\"tileSolidFlag\":" << static_cast<int>(TileProperties::Solid)
	       << ",\"tileTrapFlag\":" << static_cast<int>(TileProperties::Trap)
	       << ",\"arrow\":" << static_cast<int>(MissileID::Arrow)
	       << ",\"firebolt\":" << static_cast<int>(MissileID::Firebolt)
	       << ",\"lightningControl\":" << static_cast<int>(MissileID::LightningControl)
	       << ",\"maxObjects\":" << MAXOBJECTS
	       << ",\"maxMonsters\":" << MaxMonsters << '}'
	       << ",\n\"monsterData\":";
	WriteNativeMonsterCatalog(stream);
	stream << ",\n\"cases\":[";
	for (size_t index = 0; index < captures.size(); index++) {
		const PopulationCapture &capture = captures[index];
		if (index != 0)
			stream << ',';
		stream << "{\"snapshot\":\"cathedral-population-depth-" << capture.depth << '-'
		       << capture.inputSeed << '-' << EntryName(capture.entry) << ".json\",\"input\":{";
		stream << "\"depth\":" << capture.depth
		       << ",\"populationSeed\":" << capture.populationSeed
		       << ",\"storySeed\":" << StoryDungeonSeed
		       << ",\"pieces\":";
		WriteArray(stream, capture.prePhasePieces);
		stream << ",\"special\":";
		WriteArray(stream, capture.prePhaseSpecial);
		stream << ",\"transparency\":";
		WriteArray(stream, capture.prePhaseTransparency);
		stream << ",\"pieceProperties\":";
		WriteArray(stream, capture.prePhaseSolProperties);
		stream << ",\"populatedCells\":";
		WriteBoolArray(stream, capture.prePhasePopulatedCells);
		stream << ",\"playerTileRefs\":";
		WriteArray(stream, capture.prePhasePlayerTileRefs);
		stream << ",\"monsterTileRefs\":";
		WriteArray(stream, capture.prePhaseMonsterTileRefs);
		stream << ",\"freshMonsterSlots\":true,\"player\":{\"id\":" << capture.playerId
		       << ",\"level\":" << capture.playerLevel
		       << ",\"active\":" << (capture.playerActive ? "true" : "false")
		       << ",\"levelChanging\":" << (capture.playerLevelChanging ? "true" : "false")
		       << ",\"tile\":";
		WritePoint(stream, capture.playerTile);
		stream << ",\"future\":";
		WritePoint(stream, capture.playerFuture);
		stream << "},\"trapPercent\":" << GetTrapPercent(capture.depth)
		       << ",\"orderedBestiary\":";
		WriteBestiary(stream, capture.prePhaseBestiary);
		stream << ",\"prerequisites\":{\"nextTransparencyValue\":" << capture.preThemeNextTransparency
		       << ",\"populatedCells\":";
		WriteBoolArray(stream, capture.preThemePopulatedCells);
		stream << ",\"triggerPositions\":[";
		for (size_t triggerIndex = 0; triggerIndex < capture.triggers.size(); triggerIndex++) {
			if (triggerIndex != 0)
				stream << ',';
			WritePoint(stream, capture.triggers[triggerIndex].position);
		}
		stream << "]}},\"expectedOutput\":{\"themes\":[";
		for (size_t themeIndex = 0; themeIndex < capture.selectedThemes.size(); themeIndex++) {
			if (themeIndex != 0)
				stream << ',';
			const ThemeStruct &theme = capture.selectedThemes[themeIndex];
			stream << "{\"type\":" << static_cast<int>(theme.ttype)
			       << ",\"region\":" << static_cast<int>(theme.ttval) << '}';
		}
		stream << "],\"pieces\":";
		WriteArray(stream, capture.finalPieces);
		stream << ",\"special\":";
		WriteArray(stream, capture.finalSpecial);
		stream << ",\"transparency\":";
		WriteArray(stream, capture.finalTransparency);
		stream << ",\"monsterSupplemental\":[";
		for (size_t monsterIndex = 0; monsterIndex < capture.monsters.size(); monsterIndex++) {
			if (monsterIndex != 0)
				stream << ',';
			WriteMonsterSupplemental(stream, capture.monsters[monsterIndex]);
		}
		stream << "]}}";
	}
	stream << "]\n}\n";
	stream.flush();
	if (!stream.good())
		throw std::runtime_error("unable to flush population input sidecar: " + path.string());
	stream.close();
	if (stream.fail())
		throw std::runtime_error("unable to write population input sidecar: " + path.string());
}

bool SamePathComponent(const std::filesystem::path &left, const std::filesystem::path &right)
{
#ifdef _WIN32
	return CompareStringOrdinal(left.c_str(), -1, right.c_str(), -1, TRUE) == CSTR_EQUAL;
#else
	return left == right;
#endif
}

std::filesystem::path FindRepositoryRoot()
{
	auto candidate = std::filesystem::canonical(paths::BasePath());
	while (!candidate.empty()) {
		if (std::filesystem::is_regular_file(candidate / "CMakeLists.txt")
		    && std::filesystem::is_directory(candidate / "legacy-native")
		    && std::filesystem::is_directory(candidate / "test"))
			return candidate;
		const auto parent = candidate.parent_path();
		if (parent == candidate)
			break;
		candidate = parent;
	}
	throw std::runtime_error("population oracle could not identify its source repository");
}

bool ValidateOutputPath(const std::filesystem::path &path, std::string &error)
{
	try {
		const auto absoluteOutput = std::filesystem::absolute(path).lexically_normal();
		for (auto ancestor = absoluteOutput; !ancestor.empty();) {
#ifdef _WIN32
			const DWORD attributes = GetFileAttributesW(ancestor.c_str());
			if (attributes == INVALID_FILE_ATTRIBUTES)
				throw std::runtime_error("population oracle output ancestor is inaccessible");
			if ((attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0)
#else
			if (std::filesystem::is_symlink(std::filesystem::symlink_status(ancestor)))
#endif
				throw std::runtime_error("population oracle output must not traverse a reparse point or symlink");
			const auto parent = ancestor.parent_path();
			if (parent == ancestor)
				break;
			ancestor = parent;
		}
		if (!std::filesystem::is_directory(absoluteOutput))
			throw std::runtime_error("population oracle output must be an existing directory");
		const auto canonicalOutput = std::filesystem::canonical(absoluteOutput);
		const auto repository = FindRepositoryRoot();
		auto outputComponent = canonicalOutput.begin();
		bool insideRepository = true;
		for (const auto &component : repository) {
			if (outputComponent == canonicalOutput.end() || !SamePathComponent(component, *outputComponent)) {
				insideRepository = false;
				break;
			}
			++outputComponent;
		}
		if (insideRepository)
			throw std::runtime_error("population oracle output must be outside the repository");
		if (!std::filesystem::is_empty(absoluteOutput))
			throw std::runtime_error("population oracle output directory must be fresh");
		return true;
	} catch (const std::exception &exception) {
		error = exception.what();
		return false;
	}
}

bool ValidateNativeInputArtifact(const std::filesystem::path &directory, std::string &error)
{
	try {
		const auto absoluteDirectory = std::filesystem::absolute(directory).lexically_normal();
		if (!std::filesystem::is_directory(absoluteDirectory))
			throw std::runtime_error("population input root must be an existing directory");
		const auto artifact = absoluteDirectory / "native-population-inputs.json";
		if (!std::filesystem::is_regular_file(artifact))
			throw std::runtime_error("population input artifact must be a regular file");
#ifdef _WIN32
		const DWORD attributes = GetFileAttributesW(artifact.c_str());
		if (attributes == INVALID_FILE_ATTRIBUTES || (attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0)
			throw std::runtime_error("population input artifact must not be a reparse point");
#else
		if (std::filesystem::is_symlink(std::filesystem::symlink_status(artifact)))
			throw std::runtime_error("population input artifact must not be a symlink");
#endif
		if (std::filesystem::file_size(artifact) == 0)
			throw std::runtime_error("population input artifact must be nonempty");
		size_t childCount = 0;
		for (const auto &child : std::filesystem::directory_iterator(absoluteDirectory)) {
			childCount++;
			if (childCount > 1 || child.path().filename() != artifact.filename())
				throw std::runtime_error("population input root must contain exactly native-population-inputs.json");
		}
		if (childCount != 1)
			throw std::runtime_error("population input root must contain exactly native-population-inputs.json");
		return true;
	} catch (const std::exception &exception) {
		error = exception.what();
		return false;
	}
}

TEST(CathedralPopulation, RejectRepositoryOutput)
{
	const auto repository = FindRepositoryRoot();
	std::string error;
	EXPECT_FALSE(ValidateOutputPath(repository, error));
	EXPECT_EQ(error, "population oracle output must be outside the repository");
	EXPECT_FALSE(ValidateOutputPath(repository / "legacy-native", error));
	EXPECT_EQ(error, "population oracle output must be outside the repository");
#ifdef _WIN32
	std::wstring differentCase = (repository / "legacy-native").native();
	CharUpperBuffW(differentCase.data(), static_cast<DWORD>(differentCase.size()));
	EXPECT_FALSE(ValidateOutputPath(differentCase, error));
	EXPECT_EQ(error, "population oracle output must be outside the repository");
#endif
}

TEST(CathedralPopulation, InitialItemSidecarEscapesAllJsonControlBytes)
{
	std::string controls;
	std::string expected = "\"";
	constexpr std::string_view Hex = "0123456789abcdef";
	for (unsigned char value = 0; value < 0x20; value++) {
		controls.push_back(static_cast<char>(value));
		expected += "\\u00";
		expected.push_back(Hex[value >> 4]);
		expected.push_back(Hex[value & 0xF]);
	}
	expected += "\"";
	std::ostringstream stream;
	WriteInitialItemJsonString(stream, controls);
	EXPECT_EQ(stream.str(), expected);
	std::ostringstream ordinary;
	WriteInitialItemJsonString(ordinary, "é\\\"");
	EXPECT_EQ(ordinary.str(), "\"é\\\\\\\"\"");
}

TEST(CathedralPopulation, InitialItemOptInGuardClearsFullPoolAndReferences)
{
	InitializeItemPool();
	ItemPoolAdapter::ResetLegacyItemPools();
	{
		NativeStateGuard guard(true);
		Items[0]._itype = ItemType::Misc;
		Items[MAXITEMS]._iPLDam = 777;
		ActiveItemCount = 1;
		ActiveItems[0] = 9;
		tiles().columnMajor().begin()->setItem(1);
	}
	EXPECT_EQ(ActiveItemCount, 0);
	for (int slot = 0; slot < MAXITEMS; slot++)
		EXPECT_EQ(ActiveItems[slot], slot);
	EXPECT_EQ(Items[0]._itype, ItemType::None);
	EXPECT_EQ(Items[MAXITEMS]._iPLDam, 0);
	for (const Tile &tile : tiles().columnMajor())
		EXPECT_EQ(tile.item(), 0);
}

} // namespace

TEST(CathedralPopulation, NativeObjectPopulationLocalAssetOracle)
{
	const char *outputDirectory = std::getenv(PopulationOracleDirectoryVariable);
	if (outputDirectory == nullptr || outputDirectory[0] == '\0') {
		GTEST_SKIP() << "Set " << PopulationOracleDirectoryVariable
		             << " to run the opt-in native Cathedral population oracle";
	}
	const std::filesystem::path outputPath(outputDirectory);
	std::string outputError;
	ASSERT_TRUE(ValidateOutputPath(outputPath, outputError)) << outputError;
	const char *inputDirectory = std::getenv(PopulationInputDirectoryVariable);
	const bool captureInputs = inputDirectory != nullptr && inputDirectory[0] != '\0';
	std::filesystem::path inputPath;
	if (captureInputs) {
		inputPath = std::filesystem::path(inputDirectory);
		ASSERT_TRUE(ValidateOutputPath(inputPath, outputError)) << outputError;
		ASSERT_FALSE(std::filesystem::equivalent(outputPath, inputPath))
		    << "population artifacts and phase inputs require separate directories";
	}
	const char *encounterDirectory = std::getenv(EncounterInputDirectoryVariable);
	const bool captureEncounters = encounterDirectory != nullptr && encounterDirectory[0] != '\0';
	std::filesystem::path encounterPath;
	if (captureEncounters) {
		ASSERT_TRUE(captureInputs) << "encounter capture requires the independent version-2 initial inputs";
		encounterPath = std::filesystem::path(encounterDirectory);
		ASSERT_TRUE(ValidateOutputPath(encounterPath, outputError)) << outputError;
		ASSERT_FALSE(std::filesystem::equivalent(outputPath, encounterPath));
		ASSERT_FALSE(std::filesystem::equivalent(inputPath, encounterPath));
	}
	const char *itemDirectory = std::getenv(InitialItemDirectoryVariable);
	const bool captureInitialItems = itemDirectory != nullptr && itemDirectory[0] != '\0';
	std::filesystem::path itemPath;
	if (captureInitialItems) {
		ASSERT_TRUE(captureEncounters) << "initial items require the composed encounter inputs";
		itemPath = std::filesystem::path(itemDirectory);
		ASSERT_TRUE(ValidateOutputPath(itemPath, outputError)) << outputError;
		ASSERT_FALSE(std::filesystem::equivalent(outputPath, itemPath));
		ASSERT_FALSE(std::filesystem::equivalent(inputPath, itemPath));
		ASSERT_FALSE(std::filesystem::equivalent(encounterPath, itemPath));
	}
	ASSERT_TRUE(HeadlessMode) << "population oracle requires the headless GTest runner";

	NativeStateGuard stateGuard(captureInitialItems);
	std::string assetError;
	PrepareGameAssets(assetError);
	if (!assetError.empty())
		GTEST_SKIP() << assetError;
	if (captureInputs) {
		LoadLevelGenerationData();
		for (int depth = CathedralFirstDepth; depth <= CathedralLastDepth; depth++)
			ASSERT_EQ(GetTrapPercent(depth), depth == 1 ? 10 : 15)
			    << "population parity requires the shipped level-generation trap metadata";
	}
	if (captureInitialItems)
		LoadItemData();

	const std::array<uint32_t, 2> seeds { UINT32_C(2588), UINT32_C(743271966) };
	const std::array<lvl_entry, 2> entries { ENTRY_MAIN, ENTRY_PREV };
	std::vector<PopulationCapture> captures;
	captures.reserve(PopulationCaseCount);
	std::vector<NativeEncounterCapture> encounters;
	encounters.reserve(PopulationCaseCount);
	std::vector<NativeInitialItemCapture> initialItemCaptures;
	initialItemCaptures.reserve(PopulationCaseCount);
	for (int depth = CathedralFirstDepth; depth <= CathedralLastDepth; depth++) {
		for (const uint32_t seed : seeds) {
			for (const lvl_entry entry : entries) {
				PopulationCapture capture;
				PopulationCapture repeated;
				NativeEncounterCapture encounter;
				NativeEncounterCapture repeatedEncounter;
				NativeInitialItemCapture initialItems;
				NativeInitialItemCapture repeatedItems;
				try {
					capture = CapturePopulation(depth, seed, entry, captureInputs, captureEncounters ? &encounter : nullptr,
					    captureInitialItems ? &initialItems : nullptr);
					repeated = CapturePopulation(depth, seed, entry, captureInputs, captureEncounters ? &repeatedEncounter : nullptr,
					    captureInitialItems ? &repeatedItems : nullptr);
				} catch (const std::exception &exception) {
					FAIL() << "Native Cathedral population capture failed at depth " << depth
					       << ", seed " << seed << ", entry " << EntryName(entry)
					       << ": " << exception.what();
				}
				ASSERT_EQ(capture.rngAfterInitObjects, repeated.rngAfterInitObjects);
				ASSERT_EQ(capture.activeObjectCount, repeated.activeObjectCount);
				ASSERT_EQ(capture.activeMonsterCount, repeated.activeMonsterCount);
				if (captureEncounters) {
					ASSERT_EQ(encounter.expectedState.randomState, repeatedEncounter.expectedState.randomState);
					ASSERT_EQ(encounter.expectedState.activeMonsterCount, repeatedEncounter.expectedState.activeMonsterCount);
					ASSERT_EQ(encounter.preState.activeMonsterIds.size(), MaxMonsters);
					ASSERT_EQ(encounter.expectedState.monsters.size(), MaxMonsters);
					encounters.push_back(std::move(encounter));
				}
				if (captureInitialItems) {
					ASSERT_EQ(initialItems.expectedState.randomState, repeatedItems.expectedState.randomState);
					ASSERT_EQ(initialItems.expectedState.activeItemCount, repeatedItems.expectedState.activeItemCount);
					ASSERT_EQ(initialItems.preState.items.size(), MAXITEMS + 1);
					ASSERT_EQ(initialItems.expectedState.items.size(), MAXITEMS + 1);
					initialItemCaptures.push_back(std::move(initialItems));
				}
				captures.push_back(std::move(capture));
			}
		}
	}
	ASSERT_EQ(captures.size(), PopulationCaseCount);
	WriteMetadata(outputPath / "metadata.json");
	for (const PopulationCapture &capture : captures) {
		const std::string name = "cathedral-population-depth-" + std::to_string(capture.depth)
		    + "-" + std::to_string(capture.inputSeed) + "-" + EntryName(capture.entry) + ".json";
		WritePopulationCase(outputPath / name, capture);
	}
	if (captureInputs) {
		WriteNativeInputs(inputPath / "native-population-inputs.json", captures);
		std::string inputError;
		ASSERT_TRUE(ValidateNativeInputArtifact(inputPath, inputError)) << inputError;
	}
	if (captureEncounters) {
		ASSERT_EQ(encounters.size(), PopulationCaseCount);
		WriteEncounterInputs(encounterPath / "native-encounter-inputs.json", encounters);
	}
	if (captureInitialItems) {
		ASSERT_EQ(initialItemCaptures.size(), PopulationCaseCount);
		WriteInitialItemInputs(itemPath / "native-initial-items.json", initialItemCaptures);
	}
}

} // namespace devilution

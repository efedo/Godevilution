#include <gtest/gtest.h>

#include <algorithm>
#include <array>
#include <cctype>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <sstream>
#include <optional>
#include <stdexcept>
#include <string>
#include <system_error>
#include <utility>
#include <vector>

#include "application/game_mode.hpp"
#include "engine/lighting.h"
#include "engine/random.hpp"
#include "game/levels/dungeon_common.h"
#include "game/levels/tile_properties.hpp"
#include "game/objects/object_pool.hpp"
#include "game/objects/objects.hpp"
#include "network/protocol/msg.h"
#include "level_test.hpp"

using namespace devilution;

namespace {

constexpr char CathedralOracleDirectoryVariable[] = "GODEVILUTION_CATHEDRAL_ORACLE_DIRECTORY";
constexpr char CathedralOracleFormat[] = "godevilution-cathedral-fine-oracle";
constexpr uint32_t CathedralOracleVersion = 1;
constexpr char CathedralDepthOracleDirectoryVariable[] = "GODEVILUTION_CATHEDRAL_DEPTH_ORACLE_DIRECTORY";
constexpr char CathedralDepthOracleFormat[] = "godevilution-cathedral-depth-fine-oracle";
constexpr char CathedralDepthOraclePolicy[] = "quest-disabled-original-diablo";
constexpr char CathedralDoorOracleDirectoryVariable[] = "GODEVILUTION_CATHEDRAL_DOOR_ORACLE_DIRECTORY";
constexpr char CathedralDoorOracleFormat[] = "godevilution-cathedral-l1-door-oracle";
constexpr uint32_t CathedralDoorOracleVersion = 1;
constexpr size_t CathedralDoorOracleCaseCount = 16;
// These values are the private native DOOR_* constants and SelectionRegion
// flags used by OpenDoor/CloseDoor through SetDoorStateOpen/Closed in
// Source/game/objects/objects.cpp. The oracle records all other fields rather
// than reproducing native piece-transition logic.
constexpr int NativeDoorClosedState = 0;
constexpr int NativeDoorOpenState = 1;
constexpr int NativeDoorBlockedState = 2;
constexpr int NativeDoorOpenSelectionRegion = 2;
constexpr int NativeDoorClosedSelectionRegion = 3;
constexpr int NativeWorldWidth = MAXDUNX;
constexpr int NativeWorldHeight = MAXDUNY;
constexpr int CathedralDepthFirst = 1;
constexpr int CathedralDepthLast = 4;
constexpr size_t CathedralDepthOracleCaseCount = 48;

struct CathedralOracleCase {
	uint32_t inputSeed;
	lvl_entry entry;
	uint32_t attemptSeed;
	uint32_t finalRandomState;
	Point entryPosition;
	Point stairsUp;
	Point stairsDown;
	std::vector<uint16_t> megaTiles;
	std::vector<uint16_t> pieces;
	std::vector<uint8_t> transparency;
	std::vector<uint8_t> solProperties;
	std::vector<uint32_t> solidCells;
	std::vector<uint32_t> missileBlockedCells;
	std::vector<uint32_t> blockLightCells;
	int level = 1;
	std::vector<uint8_t> coarseTransparency;
};

struct CathedralDoorOracleLayer {
	std::vector<uint16_t> pieces;
	std::vector<uint8_t> special;
	std::vector<uint8_t> transparency;
	std::vector<uint8_t> properties;
	std::vector<uint8_t> solid;
	std::vector<uint8_t> missileBlocked;
	std::vector<uint8_t> blockLight;
	std::vector<uint8_t> walkable;
	std::vector<uint8_t> itemBlocking;
};

struct CathedralDoorOracleTileState {
	uint16_t piece = 0;
	uint8_t special = 0;
	uint8_t transparency = 0;
	uint8_t properties = 0;
	uint8_t solid = 0;
	uint8_t missileBlocked = 0;
	uint8_t blockLight = 0;
	uint8_t walkable = 0;
	uint8_t itemBlocking = 0;
};

struct CathedralDoorOracleObjectState {
	int state = 0;
	int var1OneBased = 0;
	int var2OneBased = 0;
	int animationFrame = 0;
	int selectionRegion = 0;
	bool doorFlag = false;
	bool preFlag = false;
	bool missFlag = false;
	bool solidFlag = false;
};

struct CathedralDoorOracleIdentity {
	int nativeType = 0;
	Point position;
	int var1OneBased = 0;
	int var2OneBased = 0;

	bool operator==(const CathedralDoorOracleIdentity &) const = default;
};

struct CathedralDoorOracleChangedCell {
	uint32_t index = 0;
	CathedralDoorOracleTileState closed;
	CathedralDoorOracleTileState open;
	CathedralDoorOracleTileState reclosed;
};

struct CathedralDoorOracleTransition {
	size_t ordinal = 0;
	size_t freshBaselineOrdinal = 0;
	bool deltaOpen = false;
	CathedralDoorOracleIdentity identity;
	CathedralDoorOracleObjectState closedObject;
	CathedralDoorOracleObjectState openObject;
	CathedralDoorOracleObjectState reclosedObject;
	uint64_t closedLayerHash = 0;
	uint64_t openLayerHash = 0;
	uint64_t reclosedLayerHash = 0;
	bool reclosedLayerEqualsClosed = false;
	bool reclosedObjectEqualsClosed = false;
	std::vector<CathedralDoorOracleChangedCell> changedCells;
};

struct CathedralDoorOracleCase {
	int level = 1;
	uint32_t inputSeed = 0;
	lvl_entry entry = ENTRY_MAIN;
	uint32_t attemptSeed = 0;
	uint32_t finalRandomState = 0;
	uint32_t doorScanRandomStateBefore = 0;
	uint32_t doorScanRandomStateAfter = 0;
	size_t baselineRebuildCount = 0;
	CathedralDoorOracleLayer closedBaseline;
	std::vector<CathedralDoorOracleLayer> closedBaselines;
	std::vector<uint32_t> baselineDoorScanRandomStateBefore;
	std::vector<uint32_t> baselineDoorScanRandomStateAfter;
	std::vector<CathedralDoorOracleTransition> transitions;
};

template <typename T>
uint64_t CathedralOracleHashValues(const std::vector<T> &values)
{
	uint64_t hash = UINT64_C(14695981039346656037);
	for (const T value : values) {
		for (size_t byte = 0; byte < sizeof(T); byte++) {
			hash ^= static_cast<uint8_t>(static_cast<uint64_t>(value) >> (byte * 8));
			hash *= UINT64_C(1099511628211);
		}
	}
	return hash;
}

uint64_t CathedralDoorOracleLayerHash(const CathedralDoorOracleLayer &layer)
{
	uint64_t hash = UINT64_C(14695981039346656037);
	const auto append = [&hash](uint64_t value) {
		for (size_t byte = 0; byte < sizeof(value); byte++) {
			hash ^= static_cast<uint8_t>(value >> (byte * 8));
			hash *= UINT64_C(1099511628211);
		}
	};
	append(CathedralOracleHashValues(layer.pieces));
	append(CathedralOracleHashValues(layer.special));
	append(CathedralOracleHashValues(layer.transparency));
	append(CathedralOracleHashValues(layer.properties));
	append(CathedralOracleHashValues(layer.solid));
	append(CathedralOracleHashValues(layer.missileBlocked));
	append(CathedralOracleHashValues(layer.blockLight));
	append(CathedralOracleHashValues(layer.walkable));
	append(CathedralOracleHashValues(layer.itemBlocking));
	return hash;
}

bool CathedralDoorOracleLayerEqual(const CathedralDoorOracleLayer &left, const CathedralDoorOracleLayer &right)
{
	return left.pieces == right.pieces
		&& left.special == right.special
		&& left.transparency == right.transparency
		&& left.properties == right.properties
		&& left.solid == right.solid
		&& left.missileBlocked == right.missileBlocked
		&& left.blockLight == right.blockLight
		&& left.walkable == right.walkable
		&& left.itemBlocking == right.itemBlocking;
}

CathedralDoorOracleLayer CaptureCathedralDoorOracleLayer()
{
	CathedralDoorOracleLayer layer;
	const size_t cellCount = static_cast<size_t>(NativeWorldWidth) * NativeWorldHeight;
	layer.pieces.reserve(cellCount);
	layer.special.reserve(cellCount);
	layer.transparency.reserve(cellCount);
	layer.properties.reserve(cellCount);
	layer.solid.reserve(cellCount);
	layer.missileBlocked.reserve(cellCount);
	layer.blockLight.reserve(cellCount);
	layer.walkable.reserve(cellCount);
	layer.itemBlocking.reserve(cellCount);
	for (int y = 0; y < NativeWorldHeight; y++) {
		for (int x = 0; x < NativeWorldWidth; x++) {
			const Point position { x, y };
			const Tile &tile = tileAt(position);
			if (tile.piece() >= MAXTILES)
				throw std::runtime_error("Cathedral door oracle produced an out-of-range d-piece");
			const TileProperties properties = tileProperties()[tile.piece()];
			layer.pieces.push_back(tile.piece());
			layer.special.push_back(static_cast<uint8_t>(tile.special()));
			layer.transparency.push_back(static_cast<uint8_t>(tile.transVal()));
			layer.properties.push_back(static_cast<uint8_t>(properties));
			layer.solid.push_back(HasAnyOf(properties, TileProperties::Solid));
			layer.missileBlocked.push_back(HasAnyOf(properties, TileProperties::BlockMissile));
			layer.blockLight.push_back(HasAnyOf(properties, TileProperties::BlockLight));
			layer.walkable.push_back(IsTileWalkable(position));
			layer.itemBlocking.push_back(IsItemBlockingObjectAtPosition(position));
		}
	}
	return layer;
}

CathedralDoorOracleTileState CathedralDoorOracleTile(const CathedralDoorOracleLayer &layer, uint32_t index)
{
	return {
		layer.pieces[index],
		layer.special[index],
		layer.transparency[index],
		layer.properties[index],
		layer.solid[index],
		layer.missileBlocked[index],
		layer.blockLight[index],
		layer.walkable[index],
		layer.itemBlocking[index],
	};
}

bool CathedralDoorOracleTileEqual(const CathedralDoorOracleTileState &left, const CathedralDoorOracleTileState &right)
{
	return left.piece == right.piece
		&& left.special == right.special
		&& left.transparency == right.transparency
		&& left.properties == right.properties
		&& left.solid == right.solid
		&& left.missileBlocked == right.missileBlocked
		&& left.blockLight == right.blockLight
		&& left.walkable == right.walkable
		&& left.itemBlocking == right.itemBlocking;
}

CathedralDoorOracleObjectState CaptureCathedralDoorOracleObject(const Object &object)
{
	return {
		object._oVar4,
		object._oVar1,
		object._oVar2,
		static_cast<int>(object._oAnimFrame),
		static_cast<int>(object.selectionRegion),
		object._oDoorFlag,
		object._oPreFlag,
		object._oMissFlag,
		object._oSolidFlag,
	};
}

bool CathedralDoorOracleObjectEqual(const CathedralDoorOracleObjectState &left, const CathedralDoorOracleObjectState &right)
{
	return left.state == right.state
		&& left.var1OneBased == right.var1OneBased
		&& left.var2OneBased == right.var2OneBased
		&& left.animationFrame == right.animationFrame
		&& left.selectionRegion == right.selectionRegion
		&& left.doorFlag == right.doorFlag
		&& left.preFlag == right.preFlag
		&& left.missFlag == right.missFlag
		&& left.solidFlag == right.solidFlag;
}

bool CathedralDoorOracleIsDoor(const Object &object)
{
	return object._otype == OBJ_L1LDOOR || object._otype == OBJ_L1RDOOR;
}

const char *CathedralDoorOracleObjectName(_object_id objectType)
{
	switch (objectType) {
	case OBJ_L1LDOOR:
		return "OBJ_L1LDOOR";
	case OBJ_L1RDOOR:
		return "OBJ_L1RDOOR";
	default:
		return "unknown";
	}
}

CathedralDoorOracleIdentity CaptureCathedralDoorOracleIdentity(const Object &object)
{
	return {
		static_cast<int>(object._otype),
		object.position,
		object._oVar1,
		object._oVar2,
	};
}

std::vector<Object *> CaptureCathedralDoorOracleDoors()
{
	std::vector<Object *> doors;
	for (Object &object : ObjectPoolAdapter::ActiveObjectsRange()) {
		if (CathedralDoorOracleIsDoor(object))
			doors.push_back(&object);
	}
	return doors;
}

const char *CathedralDoorOracleEntryName(lvl_entry entry)
{
	return entry == ENTRY_MAIN ? "main" : "previous";
}

std::string CathedralOracleHex(uint64_t value)
{
	std::ostringstream stream;
	stream << "0x" << std::hex << std::setw(16) << std::setfill('0') << value;
	return stream.str();
}

std::string CathedralOracleLower(std::string value)
{
	std::transform(value.begin(), value.end(), value.begin(), [](unsigned char character) {
		return static_cast<char>(std::tolower(character));
	});
	return value;
}

std::filesystem::path CathedralOracleRepositoryPath(std::error_code &pathError)
{
	std::filesystem::path current = std::filesystem::weakly_canonical(paths::BasePath(), pathError);
	if (pathError)
		return {};
	for (;;) {
		if (std::filesystem::exists(current / "AGENTS.md")
			&& std::filesystem::exists(current / ".git")) {
			return current;
		}
		const auto parent = current.parent_path();
		if (parent == current)
			break;
		current = parent;
	}
	pathError = std::make_error_code(std::errc::no_such_file_or_directory);
	return {};
}

void WriteCathedralOracleNumber(std::ostream &stream, uint64_t value)
{
	stream << value;
}

template <typename T>
void WriteCathedralOracleArray(std::ostream &stream, const std::vector<T> &values)
{
	stream << '[';
	for (size_t index = 0; index < values.size(); index++) {
		if (index != 0)
			stream << ',';
		WriteCathedralOracleNumber(stream, static_cast<uint64_t>(values[index]));
	}
	stream << ']';
}

void WriteCathedralOraclePoint(std::ostream &stream, Point point)
{
	stream << '[' << point.x << ',' << point.y << ']';
}

void WriteCathedralDoorOracleTile(std::ostream &stream, const CathedralDoorOracleTileState &tile)
{
	stream << "{\"piece\":" << tile.piece
	       << ",\"special\":" << static_cast<int>(tile.special)
	       << ",\"transparency\":" << static_cast<int>(tile.transparency)
	       << ",\"properties\":" << static_cast<int>(tile.properties)
	       << ",\"solid\":" << static_cast<int>(tile.solid)
	       << ",\"missileBlocked\":" << static_cast<int>(tile.missileBlocked)
	       << ",\"blockLight\":" << static_cast<int>(tile.blockLight)
	       << ",\"walkable\":" << static_cast<int>(tile.walkable)
	       << ",\"itemBlocking\":" << static_cast<int>(tile.itemBlocking) << '}';
}

void WriteCathedralDoorOracleObject(std::ostream &stream, const CathedralDoorOracleObjectState &object)
{
	stream << "{\"state\":" << object.state
	       << ",\"var1OneBased\":" << object.var1OneBased
	       << ",\"var2OneBased\":" << object.var2OneBased
	       << ",\"animationFrame\":" << object.animationFrame
	       << ",\"selectionRegion\":" << object.selectionRegion
	       << ",\"doorFlag\":" << (object.doorFlag ? "true" : "false")
	       << ",\"preFlag\":" << (object.preFlag ? "true" : "false")
	       << ",\"missFlag\":" << (object.missFlag ? "true" : "false")
	       << ",\"solidFlag\":" << (object.solidFlag ? "true" : "false") << '}';
}

void WriteCathedralDoorOracleLayerHashes(std::ostream &stream, const CathedralDoorOracleLayer &layer)
{
	stream << "{\"fullLayer\":\"" << CathedralOracleHex(CathedralDoorOracleLayerHash(layer))
	       << "\",\"pieces\":\"" << CathedralOracleHex(CathedralOracleHashValues(layer.pieces))
	       << "\",\"special\":\"" << CathedralOracleHex(CathedralOracleHashValues(layer.special))
	       << "\",\"transparency\":\"" << CathedralOracleHex(CathedralOracleHashValues(layer.transparency))
	       << "\",\"properties\":\"" << CathedralOracleHex(CathedralOracleHashValues(layer.properties))
	       << "\",\"solid\":\"" << CathedralOracleHex(CathedralOracleHashValues(layer.solid))
	       << "\",\"missileBlocked\":\"" << CathedralOracleHex(CathedralOracleHashValues(layer.missileBlocked))
	       << "\",\"blockLight\":\"" << CathedralOracleHex(CathedralOracleHashValues(layer.blockLight))
	       << "\",\"walkable\":\"" << CathedralOracleHex(CathedralOracleHashValues(layer.walkable))
	       << "\",\"itemBlocking\":\"" << CathedralOracleHex(CathedralOracleHashValues(layer.itemBlocking)) << "\"}";
}

void WriteCathedralDoorOracleLayerArrays(std::ostream &stream, const CathedralDoorOracleLayer &layer)
{
	stream << "{\"pieces\":";
	WriteCathedralOracleArray(stream, layer.pieces);
	stream << ",\"special\":";
	WriteCathedralOracleArray(stream, layer.special);
	stream << ",\"transparency\":";
	WriteCathedralOracleArray(stream, layer.transparency);
	stream << ",\"properties\":";
	WriteCathedralOracleArray(stream, layer.properties);
	stream << ",\"solid\":";
	WriteCathedralOracleArray(stream, layer.solid);
	stream << ",\"missileBlocked\":";
	WriteCathedralOracleArray(stream, layer.missileBlocked);
	stream << ",\"blockLight\":";
	WriteCathedralOracleArray(stream, layer.blockLight);
	stream << ",\"walkable\":";
	WriteCathedralOracleArray(stream, layer.walkable);
	stream << ",\"itemBlocking\":";
	WriteCathedralOracleArray(stream, layer.itemBlocking);
	stream << '}';
}

void WriteCathedralDoorOracleChangedCells(std::ostream &stream, const std::vector<CathedralDoorOracleChangedCell> &changedCells)
{
	stream << '[';
	for (size_t index = 0; index < changedCells.size(); index++) {
		if (index != 0)
			stream << ',';
		const auto &changed = changedCells[index];
		stream << "{\"index\":" << changed.index
		       << ",\"x\":" << changed.index % NativeWorldWidth
		       << ",\"y\":" << changed.index / NativeWorldWidth
		       << ",\"closed\":";
		WriteCathedralDoorOracleTile(stream, changed.closed);
		stream << ",\"open\":";
		WriteCathedralDoorOracleTile(stream, changed.open);
		stream << ",\"reclosed\":";
		WriteCathedralDoorOracleTile(stream, changed.reclosed);
		stream << '}';
	}
	stream << ']';
}

struct CathedralDoorOracleAssetBundle {
	std::vector<MegaTile> megaTiles;
	std::vector<uint8_t> tilBytes;
	std::vector<uint8_t> solBytes;
};

void ResetCathedralDoorOracleNativeObjectsAndLights()
{
	// InitializeObjectPool uses the modern sparse free-list, while the native
	// ReserveObjectSlot path still consumes the legacy zero-based aliases. Keep
	// both representations synchronized exactly as InitObjects/ClrAllObjects do.
	InitializeObjectPool();
	for (int index = 0; index < MAXOBJECTS; index++)
		Objects[index] = {};
	ObjectPoolAdapter::ResetLegacyObjectPools();
	for (Tile &tile : tiles().columnMajor())
		tile.setObject(0);
	CurrentLightManager.Init();
}

class CathedralDoorOracleScopeGuard {
public:
	CathedralDoorOracleScopeGuard()
	{
		ResetCathedralDoorOracleNativeObjectsAndLights();
	}

	~CathedralDoorOracleScopeGuard()
	{
		ResetCathedralDoorOracleNativeObjectsAndLights();
	}

	CathedralDoorOracleScopeGuard(const CathedralDoorOracleScopeGuard &) = delete;
	CathedralDoorOracleScopeGuard &operator=(const CathedralDoorOracleScopeGuard &) = delete;
};

std::optional<CathedralDoorOracleAssetBundle> LoadCathedralDoorOracleAssets(std::string &error)
{
	try {
		TestInitGame();
		// TestInitGame installs a preference directory as an asset override. The
		// door oracle must resolve the original DIABDAT assets instead.
		UnloadModArchives();
		MpqArchives.erase(MainMpqPriority);
		gbIsSpawn = false;
		LoadGameArchives();
		if (!HaveMainData()) {
			error = "DIABDAT.MPQ was not discovered by LoadGameArchives";
			return std::nullopt;
		}
		if (gbIsSpawn) {
			error = "LoadGameArchives resolved spawn.mpq instead of original DIABDAT.MPQ";
			return std::nullopt;
		}

		levelType() = DTYPE_CATHEDRAL;
		currentLevelNumber() = CathedralDepthFirst;
#ifndef UNPACKED_MPQS
		const auto &mainArchive = MpqArchives.at(MainMpqPriority);
		for (const std::string_view assetPath : std::array<std::string_view, 2> {
			"levels\\l1data\\l1.til",
			"levels\\l1data\\l1.sol",
		}) {
			const AssetRef asset = FindAsset(assetPath);
			if (!asset.ok()) {
				error = "Required Cathedral asset is unavailable: " + std::string(assetPath);
				return std::nullopt;
			}
			if (asset.archive != &mainArchive) {
				error = "Cathedral asset did not resolve from original DIABDAT.MPQ: " + std::string(assetPath);
				return std::nullopt;
			}
		}
#endif

		size_t tilCount = 0;
		auto tilDefinitions = LoadFileInMemWithStatus<MegaTile>(
			"levels\\l1data\\l1.til", &tilCount);
		if (!tilDefinitions.has_value()) {
			error = tilDefinitions.error();
			return std::nullopt;
		}
		if (tilDefinitions->get() == nullptr || tilCount != static_cast<size_t>(GetTileCount(DTYPE_CATHEDRAL))) {
			error = "Cathedral l1.til has an unexpected tile count";
			return std::nullopt;
		}

		size_t tilByteCount = 0;
		auto tilBytesResult = LoadFileInMemWithStatus<std::byte>(
			"levels\\l1data\\l1.til", &tilByteCount);
		if (!tilBytesResult.has_value() || tilBytesResult->get() == nullptr
			|| tilByteCount != tilCount * sizeof(MegaTile)) {
			error = tilBytesResult.has_value() ? "Cathedral l1.til has an unexpected byte count" : tilBytesResult.error();
			return std::nullopt;
		}

		size_t solByteCount = 0;
		auto solBytesResult = LoadFileInMemWithStatus<std::byte>(
			"levels\\l1data\\l1.sol", &solByteCount);
		if (!solBytesResult.has_value() || solBytesResult->get() == nullptr
			|| solByteCount == 0 || solByteCount > static_cast<size_t>(MAXTILES)) {
			error = solBytesResult.has_value() ? "Cathedral l1.sol has an unexpected byte count" : solBytesResult.error();
			return std::nullopt;
		}

		for (int piece = 0; piece < MAXTILES; piece++)
			tileProperties()[piece] = TileProperties::None;
		auto solLoad = LoadLevelSOLData();
		if (!solLoad.has_value()) {
			error = solLoad.error();
			return std::nullopt;
		}
		LoadObjectData();
		auto trnsLoad = CurrentLightManager.LoadTrns();
		if (!trnsLoad.has_value()) {
			error = trnsLoad.error();
			return std::nullopt;
		}
		CurrentLightManager.MakeLightTable();

		CathedralDoorOracleAssetBundle bundle;
		bundle.megaTiles.resize(tilCount);
		std::copy_n(tilDefinitions->get(), tilCount, bundle.megaTiles.begin());
		bundle.tilBytes.resize(tilByteCount);
		std::memcpy(bundle.tilBytes.data(), tilBytesResult->get(), tilByteCount);
		bundle.solBytes.resize(solByteCount);
		std::memcpy(bundle.solBytes.data(), solBytesResult->get(), solByteCount);
		return bundle;
	} catch (const std::exception &exception) {
		error = exception.what();
		return std::nullopt;
	}
}

struct CathedralDoorOraclePreparedState {
	uint32_t attemptSeed = 0;
	uint32_t finalRandomState = 0;
	uint32_t doorScanRandomStateBefore = 0;
	uint32_t doorScanRandomStateAfter = 0;
	CathedralDoorOracleLayer layer;
	std::vector<Object *> doors;
};

CathedralDoorOraclePreparedState PrepareCathedralDoorOracleLevel(
	int level, uint32_t seed, lvl_entry entry, const std::vector<MegaTile> &immutableMegaTiles)
{
	if (level < CathedralDepthFirst || level > CathedralDepthLast)
		throw std::runtime_error("Cathedral door oracle received an invalid level");
	if (MyPlayer == nullptr)
		throw std::runtime_error("Cathedral door oracle has no player");

	MyPlayer->pOriginalCathedral = true;
	for (Quest &quest : Quests)
		quest._qactive = QUEST_NOTAVAIL;
	LevelSeeds[level] = std::nullopt;
	currentLevelNumber() = level;
	levelType() = DTYPE_CATHEDRAL;
	megaTiles() = std::make_unique<MegaTile[]>(immutableMegaTiles.size());
	std::copy(immutableMegaTiles.begin(), immutableMegaTiles.end(), megaTiles().get());
	CreateDungeon(seed, entry);
	if (!LevelSeeds[level].has_value())
		throw std::runtime_error("Cathedral door oracle did not record the generated attempt seed");

	ResetCathedralDoorOracleNativeObjectsAndLights();
	CathedralDoorOraclePreparedState prepared;
	prepared.attemptSeed = *LevelSeeds[level];
	prepared.finalRandomState = GetLCGEngineState();
	prepared.doorScanRandomStateBefore = GetLCGEngineState();
	AddL1Objs(0, 0, MAXDUNX, MAXDUNY);
	prepared.doorScanRandomStateAfter = GetLCGEngineState();
	prepared.layer = CaptureCathedralDoorOracleLayer();
	prepared.doors = CaptureCathedralDoorOracleDoors();
	if (prepared.doors.empty())
		throw std::runtime_error("Cathedral door oracle generated no native Cathedral doors");
	return prepared;
}

CathedralDoorOracleTransition CaptureCathedralDoorOracleTransition(
	Object &door, size_t ordinal, size_t freshBaselineOrdinal,
	const CathedralDoorOracleLayer &closedLayer, bool deltaOpen)
{
	CathedralDoorOracleTransition transition;
	transition.ordinal = ordinal;
	transition.freshBaselineOrdinal = freshBaselineOrdinal;
	transition.deltaOpen = deltaOpen;
	transition.identity = CaptureCathedralDoorOracleIdentity(door);
	transition.closedObject = CaptureCathedralDoorOracleObject(door);
	transition.closedLayerHash = CathedralDoorOracleLayerHash(closedLayer);
	const std::string context = std::string(" for ") + CathedralDoorOracleObjectName(door._otype)
	    + " at (" + std::to_string(door.position.x) + "," + std::to_string(door.position.y) + ")";
	if (tileAt(door.position).hasPlayer())
		throw std::runtime_error("Cathedral door oracle fixture is occupied before opening" + context);
	if (transition.closedObject.state != NativeDoorClosedState)
		throw std::runtime_error("Native Cathedral door did not start CLOSED" + context);
	if (!transition.closedObject.doorFlag
		|| transition.closedObject.preFlag
		|| transition.closedObject.missFlag
		|| transition.closedObject.selectionRegion != NativeDoorClosedSelectionRegion)
		throw std::runtime_error("Native Cathedral door CLOSED flags differ from SetDoorStateClosed" + context);

	Player remotePlayer {};
	if (deltaOpen)
		DeltaSyncOpObject(door);
	else
		SyncOpObject(remotePlayer, CMD_OPENDOOR, door);
	const CathedralDoorOracleLayer openLayer = CaptureCathedralDoorOracleLayer();
	transition.openObject = CaptureCathedralDoorOracleObject(door);
	transition.openLayerHash = CathedralDoorOracleLayerHash(openLayer);
	if (transition.openObject.state != NativeDoorOpenState)
		throw std::runtime_error("Native Cathedral door did not become OPEN through the selected path" + context);
	if (transition.openObject.animationFrame != transition.closedObject.animationFrame + 2)
		throw std::runtime_error("Native Cathedral door OPEN animation did not advance by two frames" + context);
	if (!transition.openObject.doorFlag
		|| !transition.openObject.preFlag
		|| !transition.openObject.missFlag
		|| transition.openObject.selectionRegion != NativeDoorOpenSelectionRegion)
		throw std::runtime_error("Native Cathedral door OPEN flags differ from SetDoorStateOpen" + context);

	SyncOpObject(remotePlayer, CMD_CLOSEDOOR, door);
	const CathedralDoorOracleLayer reclosedLayer = CaptureCathedralDoorOracleLayer();
	transition.reclosedObject = CaptureCathedralDoorOracleObject(door);
	transition.reclosedLayerHash = CathedralDoorOracleLayerHash(reclosedLayer);
	if (transition.reclosedObject.state != NativeDoorClosedState)
		throw std::runtime_error("Native Cathedral door did not become CLOSED through the remote close path" + context);
	if (transition.reclosedObject.animationFrame != transition.openObject.animationFrame - 2)
		throw std::runtime_error("Native Cathedral door CLOSED animation did not retreat by two frames" + context);
	if (!transition.reclosedObject.doorFlag
		|| transition.reclosedObject.preFlag
		|| transition.reclosedObject.missFlag
		|| transition.reclosedObject.selectionRegion != NativeDoorClosedSelectionRegion)
		throw std::runtime_error("Native Cathedral door reclosed flags differ from SetDoorStateClosed" + context);
	transition.reclosedLayerEqualsClosed = CathedralDoorOracleLayerEqual(closedLayer, reclosedLayer);
	transition.reclosedObjectEqualsClosed = CathedralDoorOracleObjectEqual(transition.closedObject, transition.reclosedObject);

	const uint32_t cellCount = NativeWorldWidth * NativeWorldHeight;
	transition.changedCells.reserve(cellCount);
	for (uint32_t index = 0; index < cellCount; index++) {
		const auto closed = CathedralDoorOracleTile(closedLayer, index);
		const auto open = CathedralDoorOracleTile(openLayer, index);
		const auto reclosed = CathedralDoorOracleTile(reclosedLayer, index);
		if (!CathedralDoorOracleTileEqual(closed, open) || !CathedralDoorOracleTileEqual(closed, reclosed))
			transition.changedCells.push_back({ index, closed, open, reclosed });
	}
	return transition;
}

CathedralDoorOracleCase CaptureCathedralDoorOracleCase(
	int level, uint32_t seed, lvl_entry entry, const std::vector<MegaTile> &immutableMegaTiles, bool deltaOpen)
{
	CathedralDoorOraclePreparedState prepared = PrepareCathedralDoorOracleLevel(level, seed, entry, immutableMegaTiles);
	CathedralDoorOracleCase oracleCase;
	oracleCase.level = level;
	oracleCase.inputSeed = seed;
	oracleCase.entry = entry;
	oracleCase.attemptSeed = prepared.attemptSeed;
	oracleCase.finalRandomState = prepared.finalRandomState;
	oracleCase.doorScanRandomStateBefore = prepared.doorScanRandomStateBefore;
	oracleCase.doorScanRandomStateAfter = prepared.doorScanRandomStateAfter;
	oracleCase.closedBaseline = prepared.layer;
	oracleCase.closedBaselines.push_back(prepared.layer);
	oracleCase.baselineDoorScanRandomStateBefore.push_back(prepared.doorScanRandomStateBefore);
	oracleCase.baselineDoorScanRandomStateAfter.push_back(prepared.doorScanRandomStateAfter);
	oracleCase.baselineRebuildCount = 1;

	std::vector<CathedralDoorOracleIdentity> identities;
	identities.reserve(prepared.doors.size());
	for (const Object *door : prepared.doors)
		identities.push_back(CaptureCathedralDoorOracleIdentity(*door));

	for (size_t ordinal = 0; ordinal < prepared.doors.size(); ordinal++) {
		if (oracleCase.transitions.size() != ordinal)
			throw std::runtime_error("Cathedral door oracle transition order was corrupted");
		const auto transition = CaptureCathedralDoorOracleTransition(
			*prepared.doors[ordinal], ordinal, oracleCase.baselineRebuildCount, prepared.layer, deltaOpen);
		oracleCase.transitions.push_back(transition);
		if (!transition.reclosedLayerEqualsClosed || !transition.reclosedObjectEqualsClosed) {
			if (ordinal + 1 == prepared.doors.size())
				break;
			prepared = PrepareCathedralDoorOracleLevel(level, seed, entry, immutableMegaTiles);
			oracleCase.baselineRebuildCount++;
			if (prepared.doors.size() != identities.size())
				throw std::runtime_error("Cathedral door oracle changed the native door count after a fresh baseline");
			for (size_t identityIndex = 0; identityIndex < prepared.doors.size(); identityIndex++) {
				if (CaptureCathedralDoorOracleIdentity(*prepared.doors[identityIndex]) != identities[identityIndex])
					throw std::runtime_error("Cathedral door oracle changed native door identity after a fresh baseline");
			}
			oracleCase.closedBaselines.push_back(prepared.layer);
			oracleCase.baselineDoorScanRandomStateBefore.push_back(prepared.doorScanRandomStateBefore);
			oracleCase.baselineDoorScanRandomStateAfter.push_back(prepared.doorScanRandomStateAfter);
			// The next transition uses the regenerated layer and object vector. Its
			// fresh baseline is deliberately retained as evidence in that record.
		}
	}
	return oracleCase;
}

bool WriteCathedralDoorOracleCase(const std::filesystem::path &path, const CathedralDoorOracleCase &oracleCase)
{
	std::ofstream stream(path, std::ios::binary | std::ios::trunc);
	if (!stream.good())
		return false;
	stream << "{\n"
	       << "\"format\":\"" << CathedralDoorOracleFormat << "\",\n"
	       << "\"version\":" << CathedralDoorOracleVersion << ",\n"
	       << "\"policy\":\"native-add-l1-objects-door-only\",\n"
	       << "\"level\":" << oracleCase.level << ",\n"
	       << "\"entry\":\"" << CathedralDoorOracleEntryName(oracleCase.entry) << "\",\n"
	       << "\"inputSeed\":" << oracleCase.inputSeed << ",\n"
	       << "\"attemptSeed\":" << oracleCase.attemptSeed << ",\n"
	       << "\"finalRandomState\":" << oracleCase.finalRandomState << ",\n"
	       << "\"doorScanRandomStateBefore\":" << oracleCase.doorScanRandomStateBefore << ",\n"
	       << "\"doorScanRandomStateAfter\":" << oracleCase.doorScanRandomStateAfter << ",\n"
	       << "\"nativeWidth\":" << NativeWorldWidth << ",\n"
	       << "\"nativeHeight\":" << NativeWorldHeight << ",\n"
	       << "\"rowMajor\":\"y*nativeWidth+x\",\n"
	       << "\"baselineRebuildCount\":" << oracleCase.baselineRebuildCount << ",\n"
	       << "\"closedBaselineHashes\":";
	WriteCathedralDoorOracleLayerHashes(stream, oracleCase.closedBaseline);
	stream << ",\n\"closedBaseline\":";
	WriteCathedralDoorOracleLayerArrays(stream, oracleCase.closedBaseline);
	stream << ",\n\"closedBaselines\":[";
	for (size_t index = 0; index < oracleCase.closedBaselines.size(); index++) {
		if (index != 0)
			stream << ',';
		stream << "{\"ordinal\":" << index + 1
		       << ",\"doorScanRandomStateBefore\":" << oracleCase.baselineDoorScanRandomStateBefore[index]
		       << ",\"doorScanRandomStateAfter\":" << oracleCase.baselineDoorScanRandomStateAfter[index]
		       << ",\"hashes\":";
		WriteCathedralDoorOracleLayerHashes(stream, oracleCase.closedBaselines[index]);
		stream << ",\"layer\":";
		WriteCathedralDoorOracleLayerArrays(stream, oracleCase.closedBaselines[index]);
		stream << '}';
	}
	stream << "],\n\"doors\":[";
	for (size_t index = 0; index < oracleCase.transitions.size(); index++) {
		if (index != 0)
			stream << ',';
		const auto &transition = oracleCase.transitions[index];
		stream << "{\"ordinal\":" << transition.ordinal
		       << ",\"freshBaselineOrdinal\":" << transition.freshBaselineOrdinal
		       << ",\"openPath\":\"" << (transition.deltaOpen ? "delta" : "sync") << "\""
		       << ",\"nativeTypeId\":" << transition.identity.nativeType
		       << ",\"nativeType\":\"" << CathedralDoorOracleObjectName(static_cast<_object_id>(transition.identity.nativeType)) << "\""
		       << ",\"position\":";
		WriteCathedralOraclePoint(stream, transition.identity.position);
		stream << ",\"var1OneBased\":" << transition.identity.var1OneBased
		       << ",\"var2OneBased\":" << transition.identity.var2OneBased
		       << ",\"sourcePieceObserved\":" << transition.identity.var1OneBased - 1
		       << ",\"neighborPieceObserved\":" << transition.identity.var2OneBased - 1
		       << ",\"closedObject\":";
		WriteCathedralDoorOracleObject(stream, transition.closedObject);
		stream << ",\"openObject\":";
		WriteCathedralDoorOracleObject(stream, transition.openObject);
		stream << ",\"reclosedObject\":";
		WriteCathedralDoorOracleObject(stream, transition.reclosedObject);
		stream << ",\"closedLayerHash\":\"" << CathedralOracleHex(transition.closedLayerHash)
		       << "\",\"openLayerHash\":\"" << CathedralOracleHex(transition.openLayerHash)
		       << "\",\"reclosedLayerHash\":\"" << CathedralOracleHex(transition.reclosedLayerHash)
		       << "\",\"reclosedLayerEqualsClosed\":" << (transition.reclosedLayerEqualsClosed ? "true" : "false")
		       << ",\"reclosedObjectEqualsClosed\":" << (transition.reclosedObjectEqualsClosed ? "true" : "false")
		       << ",\"changedCells\":";
		WriteCathedralDoorOracleChangedCells(stream, transition.changedCells);
		stream << '}';
	}
	stream << "]\n}\n";
	stream.flush();
	if (!stream.good())
		return false;
	stream.close();
	return stream.good();
}

bool WriteCathedralDoorOracleMetadata(const std::filesystem::path &path)
{
	std::ofstream stream(path, std::ios::binary | std::ios::trunc);
	if (!stream.good())
		return false;
	stream << "{\n"
	       << "\"format\":\"" << CathedralDoorOracleFormat << "\",\n"
	       << "\"version\":" << CathedralDoorOracleVersion << ",\n"
	       << "\"policy\":\"native-add-l1-objects-door-only\",\n"
	       << "\"caseCount\":" << CathedralDoorOracleCaseCount << ",\n"
	       << "\"depths\":[1,2,3,4],\n"
	       << "\"seeds\":[2588,743271966],\n"
	       << "\"entries\":[\"main\",\"previous\"],\n"
	       << "\"rawAssets\":[\"l1.til\",\"l1.sol\"],\n"
	       << "\"width\":" << NativeWorldWidth << ",\n"
	       << "\"height\":" << NativeWorldHeight << ",\n"
	       << "\"rowMajor\":\"y*width+x\",\n"
	       << "\"deltaCase\":\"cathedral-door-delta-depth-2-743271966-main.json\"\n"
	       << "}\n";
	stream.flush();
	if (!stream.good())
		return false;
	stream.close();
	return stream.good();
}

bool ValidateCathedralDoorOracleOutputPath(const std::filesystem::path &outputPath, bool requireEmpty, std::string &error)
{
	if (!std::filesystem::exists(outputPath)) {
		error = "Cathedral door oracle output directory does not exist: " + outputPath.string();
		return false;
	}
	if (!std::filesystem::is_directory(outputPath)) {
		error = "Cathedral door oracle output path is not a directory: " + outputPath.string();
		return false;
	}
	std::error_code pathError;
	const auto canonicalOutputPath = std::filesystem::weakly_canonical(outputPath, pathError);
	if (pathError) {
		error = "Unable to canonicalize Cathedral door oracle output path: " + pathError.message();
		return false;
	}
	const auto canonicalRepositoryPath = CathedralOracleRepositoryPath(pathError);
	if (pathError) {
		error = "Unable to locate repository root for Cathedral door oracle isolation: " + pathError.message();
		return false;
	}
	std::string repositoryPrefix = CathedralOracleLower(canonicalRepositoryPath.generic_string());
	if (!repositoryPrefix.ends_with('/'))
		repositoryPrefix += '/';
	const std::string canonicalOutput = CathedralOracleLower(canonicalOutputPath.generic_string());
	if (canonicalOutput == repositoryPrefix.substr(0, repositoryPrefix.size() - 1)
		|| canonicalOutput.starts_with(repositoryPrefix)) {
		error = "Cathedral door oracle output must be outside the repository: " + canonicalOutputPath.string();
		return false;
	}
	if (requireEmpty && !std::filesystem::is_empty(outputPath)) {
		error = "Cathedral door oracle output directory must be fresh: " + outputPath.string();
		return false;
	}
	return true;
}

std::string CathedralDoorOracleCaseFingerprint(const CathedralDoorOracleCase &oracleCase)
{
	std::ostringstream stream;
	stream << oracleCase.level << ':' << oracleCase.inputSeed << ':' << static_cast<int>(oracleCase.entry)
	       << ':' << oracleCase.attemptSeed << ':' << oracleCase.finalRandomState
	       << ':' << oracleCase.doorScanRandomStateBefore << ':' << oracleCase.doorScanRandomStateAfter
	       << ':' << oracleCase.baselineRebuildCount << ':' << CathedralDoorOracleLayerHash(oracleCase.closedBaseline);
	for (const auto &baseline : oracleCase.closedBaselines)
		stream << ':' << CathedralDoorOracleLayerHash(baseline);
	for (const uint32_t state : oracleCase.baselineDoorScanRandomStateBefore)
		stream << ':' << state;
	for (const uint32_t state : oracleCase.baselineDoorScanRandomStateAfter)
		stream << ':' << state;
	for (const auto &transition : oracleCase.transitions) {
		stream << ':' << transition.ordinal << ':' << transition.freshBaselineOrdinal << ':' << transition.deltaOpen
		       << ':' << transition.identity.nativeType << ':' << transition.identity.position.x << ':' << transition.identity.position.y
		       << ':' << transition.identity.var1OneBased << ':' << transition.identity.var2OneBased
		       << ':' << transition.closedLayerHash << ':' << transition.openLayerHash << ':' << transition.reclosedLayerHash
		       << ':' << transition.reclosedLayerEqualsClosed << ':' << transition.reclosedObjectEqualsClosed;
		const auto appendObject = [&stream](const CathedralDoorOracleObjectState &object) {
			stream << ':' << object.state << ':' << object.var1OneBased << ':' << object.var2OneBased
			       << ':' << object.animationFrame << ':' << object.selectionRegion << ':' << object.doorFlag
			       << ':' << object.preFlag << ':' << object.missFlag << ':' << object.solidFlag;
		};
		appendObject(transition.closedObject);
		appendObject(transition.openObject);
		appendObject(transition.reclosedObject);
		for (const auto &changed : transition.changedCells) {
			stream << ':' << changed.index;
			const auto appendTile = [&stream](const CathedralDoorOracleTileState &tile) {
				stream << ':' << tile.piece << ':' << static_cast<int>(tile.special)
				       << ':' << static_cast<int>(tile.transparency) << ':' << static_cast<int>(tile.properties)
				       << ':' << static_cast<int>(tile.solid) << ':' << static_cast<int>(tile.missileBlocked)
				       << ':' << static_cast<int>(tile.blockLight) << ':' << static_cast<int>(tile.walkable)
				       << ':' << static_cast<int>(tile.itemBlocking);
			};
			appendTile(changed.closed);
			appendTile(changed.open);
			appendTile(changed.reclosed);
		}
	}
	return stream.str();
}

bool WriteCathedralOracleCase(const std::filesystem::path &path, const CathedralOracleCase &oracleCase)
{
	std::ofstream stream(path, std::ios::binary | std::ios::trunc);
	if (!stream.good())
		return false;
	const char *entry = oracleCase.entry == ENTRY_MAIN ? "main" : "previous";
	stream << "{\n"
	       << "\"format\":\"" << CathedralOracleFormat << "\",\n"
	       << "\"version\":" << CathedralOracleVersion << ",\n"
	       << "\"level\":1,\n"
	       << "\"entry\":\"" << entry << "\",\n"
	       << "\"inputSeed\":" << oracleCase.inputSeed << ",\n"
	       << "\"attemptSeed\":" << oracleCase.attemptSeed << ",\n"
	       << "\"finalRandomState\":" << oracleCase.finalRandomState << ",\n"
	       << "\"coarseWidth\":40,\n"
	       << "\"coarseHeight\":40,\n"
	       << "\"nativeWidth\":" << NativeWorldWidth << ",\n"
	       << "\"nativeHeight\":" << NativeWorldHeight << ",\n"
	       << "\"solCount\":" << oracleCase.solProperties.size() << ",\n"
	       << "\"entryPosition\":";
	WriteCathedralOraclePoint(stream, oracleCase.entryPosition);
	stream << ",\n\"stairsUp\":";
	WriteCathedralOraclePoint(stream, oracleCase.stairsUp);
	stream << ",\n\"stairsDown\":";
	WriteCathedralOraclePoint(stream, oracleCase.stairsDown);
	stream << ",\n\"megaTiles\":";
	WriteCathedralOracleArray(stream, oracleCase.megaTiles);
	stream << ",\n\"pieces\":";
	WriteCathedralOracleArray(stream, oracleCase.pieces);
	stream << ",\n\"transparency\":";
	WriteCathedralOracleArray(stream, oracleCase.transparency);
	stream << ",\n\"solProperties\":";
	WriteCathedralOracleArray(stream, oracleCase.solProperties);
	stream << ",\n\"solidCells\":";
	WriteCathedralOracleArray(stream, oracleCase.solidCells);
	stream << ",\n\"missileBlockedCells\":";
	WriteCathedralOracleArray(stream, oracleCase.missileBlockedCells);
	stream << ",\n\"blockLightCells\":";
	WriteCathedralOracleArray(stream, oracleCase.blockLightCells);
	stream << ",\n\"hashes\":{\n"
	       << "\"megaTiles\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.megaTiles)) << "\",\n"
	       << "\"pieces\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.pieces)) << "\",\n"
	       << "\"transparency\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.transparency)) << "\",\n"
	       << "\"solProperties\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.solProperties)) << "\",\n"
	       << "\"solidCells\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.solidCells)) << "\",\n"
	       << "\"missileBlockedCells\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.missileBlockedCells)) << "\",\n"
	       << "\"blockLightCells\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.blockLightCells)) << "\"\n"
	       << "}\n}\n";
	stream.flush();
	if (!stream.good())
		return false;
	stream.close();
	return stream.good();
}

bool WriteCathedralDepthOracleCase(const std::filesystem::path &path, const CathedralOracleCase &oracleCase)
{
	std::ofstream stream(path, std::ios::binary | std::ios::trunc);
	if (!stream.good())
		return false;
	const char *entry = oracleCase.entry == ENTRY_MAIN ? "main" : "previous";
	stream << "{\n"
	       << "\"format\":\"" << CathedralDepthOracleFormat << "\",\n"
	       << "\"version\":" << CathedralOracleVersion << ",\n"
	       << "\"level\":" << oracleCase.level << ",\n"
	       << "\"policy\":\"" << CathedralDepthOraclePolicy << "\",\n"
	       << "\"entry\":\"" << entry << "\",\n"
	       << "\"inputSeed\":" << oracleCase.inputSeed << ",\n"
	       << "\"attemptSeed\":" << oracleCase.attemptSeed << ",\n"
	       << "\"finalRandomState\":" << oracleCase.finalRandomState << ",\n"
	       << "\"coarseWidth\":40,\n"
	       << "\"coarseHeight\":40,\n"
	       << "\"coarseTransparencyWidth\":80,\n"
	       << "\"coarseTransparencyHeight\":80,\n"
	       << "\"nativeWidth\":" << NativeWorldWidth << ",\n"
	       << "\"nativeHeight\":" << NativeWorldHeight << ",\n"
	       << "\"solCount\":" << oracleCase.solProperties.size() << ",\n"
	       << "\"entryPosition\":";
	WriteCathedralOraclePoint(stream, oracleCase.entryPosition);
	stream << ",\n\"stairsUp\":";
	WriteCathedralOraclePoint(stream, oracleCase.stairsUp);
	stream << ",\n\"stairsDown\":";
	WriteCathedralOraclePoint(stream, oracleCase.stairsDown);
	stream << ",\n\"megaTiles\":";
	WriteCathedralOracleArray(stream, oracleCase.megaTiles);
	stream << ",\n\"coarseTransparency\":";
	WriteCathedralOracleArray(stream, oracleCase.coarseTransparency);
	stream << ",\n\"pieces\":";
	WriteCathedralOracleArray(stream, oracleCase.pieces);
	stream << ",\n\"transparency\":";
	WriteCathedralOracleArray(stream, oracleCase.transparency);
	stream << ",\n\"solProperties\":";
	WriteCathedralOracleArray(stream, oracleCase.solProperties);
	stream << ",\n\"solidCells\":";
	WriteCathedralOracleArray(stream, oracleCase.solidCells);
	stream << ",\n\"missileBlockedCells\":";
	WriteCathedralOracleArray(stream, oracleCase.missileBlockedCells);
	stream << ",\n\"blockLightCells\":";
	WriteCathedralOracleArray(stream, oracleCase.blockLightCells);
	stream << ",\n\"hashes\":{\n"
	       << "\"megaTiles\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.megaTiles)) << "\",\n"
	       << "\"coarseTransparency\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.coarseTransparency)) << "\",\n"
	       << "\"pieces\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.pieces)) << "\",\n"
	       << "\"transparency\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.transparency)) << "\",\n"
	       << "\"solProperties\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.solProperties)) << "\",\n"
	       << "\"solidCells\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.solidCells)) << "\",\n"
	       << "\"missileBlockedCells\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.missileBlockedCells)) << "\",\n"
	       << "\"blockLightCells\":\"" << CathedralOracleHex(CathedralOracleHashValues(oracleCase.blockLightCells)) << "\"\n"
	       << "}\n}\n";
	stream.flush();
	if (!stream.good())
		return false;
	stream.close();
	return stream.good();
}

bool WriteCathedralOracleRawAsset(const std::filesystem::path &path, const uint8_t *data, size_t size)
{
	std::ofstream stream(path, std::ios::binary | std::ios::trunc);
	if (!stream.good())
		return false;
	stream.write(reinterpret_cast<const char *>(data), static_cast<std::streamsize>(size));
	stream.flush();
	if (!stream.good())
		return false;
	stream.close();
	return stream.good();
}

CathedralOracleCase CaptureCathedralOracleCaseAtLevel(uint32_t seed, lvl_entry entry, int level, bool resetDepthPolicy)
{
	if (level < CathedralDepthFirst || level > CathedralDepthLast)
		throw std::runtime_error("Cathedral depth oracle received an invalid level");
	if (resetDepthPolicy) {
		if (MyPlayer == nullptr)
			throw std::runtime_error("Cathedral depth oracle has no player");
		MyPlayer->pOriginalCathedral = true;
		for (Quest &quest : Quests)
			quest._qactive = QUEST_NOTAVAIL;
	}
	LevelSeeds[level] = std::nullopt;
	currentLevelNumber() = level;
	levelType() = DTYPE_CATHEDRAL;
	CreateDungeon(seed, entry);

	CathedralOracleCase oracleCase {
		seed,
		entry,
		LevelSeeds[level].value(),
		GetLCGEngineState(),
		viewPosition(),
		Point { -1, -1 },
		Point { -1, -1 },
		{},
		{},
		{},
		{},
		{},
		{},
		{},
	};
	oracleCase.level = level;
	int stairsUpCount = 0;
	int stairsDownCount = 0;
	for (int y = 0; y < DMAXY; y++) {
		for (int x = 0; x < DMAXX; x++) {
			const auto value = megaTileAt(x, y).current();
			oracleCase.megaTiles.push_back(value);
			if (value == 66) {
				stairsUpCount++;
				oracleCase.stairsUp = { x - 1, y };
			}
			if (value == 57) {
				stairsDownCount++;
				oracleCase.stairsDown = { x - 1, y };
			}
		}
	}
	for (int y = 0; y < NativeWorldHeight; y++) {
		for (int x = 0; x < NativeWorldWidth; x++) {
			const int cell = y * NativeWorldWidth + x;
			const Tile &tile = tileAt(x, y);
			const uint16_t piece = tile.piece();
			if (piece >= MAXTILES)
				throw std::runtime_error("Cathedral oracle produced an out-of-range d-piece");
			const TileProperties properties = tileProperties()[piece];
			oracleCase.pieces.push_back(piece);
			oracleCase.transparency.push_back(static_cast<uint8_t>(tile.transVal()));
			if (HasAnyOf(properties, TileProperties::Solid))
				oracleCase.solidCells.push_back(static_cast<uint32_t>(cell));
			if (HasAnyOf(properties, TileProperties::BlockMissile))
				oracleCase.missileBlockedCells.push_back(static_cast<uint32_t>(cell));
			if (HasAnyOf(properties, TileProperties::BlockLight))
				oracleCase.blockLightCells.push_back(static_cast<uint32_t>(cell));
		}
	}
	for (int y = 0; y < DMAXY * 2; y++) {
		for (int x = 0; x < DMAXX * 2; x++)
			oracleCase.coarseTransparency.push_back(static_cast<uint8_t>(tileAt(x + 16, y + 16).transVal()));
	}
	for (int piece = 0; piece < MAXTILES; piece++)
		oracleCase.solProperties.push_back(static_cast<uint8_t>(tileProperties()[piece]));
	if (stairsUpCount != 1 || stairsDownCount != 1
		|| oracleCase.stairsUp.x == -1 || oracleCase.stairsDown.x == -1)
		throw std::runtime_error("Cathedral oracle could not locate generated stairs");
	return oracleCase;
}

CathedralOracleCase CaptureCathedralOracleCase(uint32_t seed, lvl_entry entry)
{
	return CaptureCathedralOracleCaseAtLevel(seed, entry, CathedralDepthFirst, false);
}

TEST(Drlg_l1, CreateL5Dungeon_diablo_1_2588)
{
	LoadExpectedLevelData("diablo/1-2588.dun");

	TestInitGame();

	TestCreateDungeon(1, 2588, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(77, 46));
	TestCreateDungeon(1, 2588, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(49, 49));
}

TEST(Drlg_l1, CreateL5Dungeon_diablo_1_743271966)
{
	LoadExpectedLevelData("diablo/1-743271966.dun");

	TestInitGame();

	TestCreateDungeon(1, 743271966, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(51, 82));
	TestCreateDungeon(1, 743271966, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(81, 47));
}

TEST(Drlg_l1, CreateL5Dungeon_rng_state_matches_native_oracle)
{
	struct OracleCase {
		uint32_t seed;
		lvl_entry entry;
		uint32_t attemptSeed;
		uint32_t finalRandomState;
	};

	const OracleCase cases[] = {
		{ UINT32_C(2588), ENTRY_MAIN, UINT32_C(1779836089), UINT32_C(1552486206) },
		{ UINT32_C(2588), ENTRY_PREV, UINT32_C(1779836089), UINT32_C(1552486206) },
		{ UINT32_C(743271966), ENTRY_MAIN, UINT32_C(283661120), UINT32_C(980632310) },
		{ UINT32_C(743271966), ENTRY_PREV, UINT32_C(283661120), UINT32_C(980632310) },
	};

	TestInitGame();
	for (const auto &testCase : cases) {
		LevelSeeds[1] = std::nullopt;
		currentLevelNumber() = 1;
		levelType() = GetLevelType(1);
		megaTiles() = std::make_unique<MegaTile[]>(GetTileCount(levelType()));

		CreateDungeon(testCase.seed, testCase.entry);
		ASSERT_TRUE(LevelSeeds[1].has_value());
		EXPECT_EQ(*LevelSeeds[1], testCase.attemptSeed);
		EXPECT_EQ(GetLCGEngineState(), testCase.finalRandomState);
	}
}

TEST(Drlg_l1, CathedralFineGeometryLocalAssetOracle)
{
	const char *outputDirectory = std::getenv(CathedralOracleDirectoryVariable);
	if (outputDirectory == nullptr || outputDirectory[0] == '\0') {
		GTEST_SKIP() << "Set " << CathedralOracleDirectoryVariable
		             << " to run the opt-in local-asset Cathedral fine-geometry oracle";
	}

	const std::filesystem::path outputPath(outputDirectory);
	ASSERT_TRUE(std::filesystem::exists(outputPath))
		<< "Cathedral oracle output directory does not exist: " << outputPath.string();
	ASSERT_TRUE(std::filesystem::is_directory(outputPath))
		<< "Cathedral oracle output path is not a directory: " << outputPath.string();
	std::error_code pathError;
	const auto canonicalOutputPath = std::filesystem::weakly_canonical(outputPath, pathError);
	ASSERT_FALSE(pathError) << "Unable to canonicalize Cathedral oracle output path: " << pathError.message();
	const auto canonicalRepositoryPath = CathedralOracleRepositoryPath(pathError);
	ASSERT_FALSE(pathError) << "Unable to locate repository root for oracle isolation: " << pathError.message();
	std::string repositoryPrefix = CathedralOracleLower(canonicalRepositoryPath.generic_string());
	if (!repositoryPrefix.ends_with('/'))
		repositoryPrefix += '/';
	const std::string canonicalOutput = CathedralOracleLower(canonicalOutputPath.generic_string());
	ASSERT_FALSE(canonicalOutput == repositoryPrefix.substr(0, repositoryPrefix.size() - 1)
		|| canonicalOutput.starts_with(repositoryPrefix))
		<< "Cathedral oracle output must be outside the repository: " << canonicalOutputPath.string();
	ASSERT_TRUE(std::filesystem::is_empty(outputPath))
		<< "Cathedral oracle output directory must be fresh: " << outputPath.string();

	TestInitGame();
	// TestInitGame installs its preference directory as an asset override.
	// Remove that override and any prior main archive before resolving the
	// original archive through the production GOG-aware search order.
	UnloadModArchives();
	MpqArchives.erase(MainMpqPriority);
	gbIsSpawn = false;
	LoadGameArchives();
	ASSERT_TRUE(HaveMainData()) << "DIABDAT.MPQ was not discovered by LoadGameArchives";
	ASSERT_FALSE(gbIsSpawn) << "Cathedral oracle resolved spawn.mpq instead of original DIABDAT.MPQ";

	levelType() = DTYPE_CATHEDRAL;
	currentLevelNumber() = 1;

#ifndef UNPACKED_MPQS
	const auto &mainArchive = MpqArchives.at(MainMpqPriority);
	for (const std::string_view assetPath : std::array<std::string_view, 2> {
		"levels\\l1data\\l1.til",
		"levels\\l1data\\l1.sol",
	}) {
		AssetRef asset = FindAsset(assetPath);
		ASSERT_TRUE(asset.ok()) << "Required Cathedral asset is unavailable: " << assetPath;
		ASSERT_EQ(asset.archive, &mainArchive)
			<< "Cathedral asset did not resolve from the original main archive: " << assetPath;
	}
#endif

	size_t tilCount = 0;
	auto tilDefinitions = LoadFileInMemWithStatus<MegaTile>(
		"levels\\l1data\\l1.til", &tilCount);
	ASSERT_TRUE(tilDefinitions.has_value()) << tilDefinitions.error();
	ASSERT_NE(tilDefinitions->get(), nullptr);
	ASSERT_EQ(tilCount, static_cast<size_t>(GetTileCount(DTYPE_CATHEDRAL)));

	size_t tilByteCount = 0;
	auto tilBytesResult = LoadFileInMemWithStatus<std::byte>(
		"levels\\l1data\\l1.til", &tilByteCount);
	ASSERT_TRUE(tilBytesResult.has_value()) << tilBytesResult.error();
	ASSERT_NE(tilBytesResult->get(), nullptr);
	ASSERT_EQ(tilByteCount, tilCount * sizeof(MegaTile));
	std::vector<uint8_t> tilBytes(tilByteCount);
	std::memcpy(tilBytes.data(), tilBytesResult->get(), tilByteCount);

	size_t solByteCount = 0;
	auto solBytesResult = LoadFileInMemWithStatus<std::byte>(
		"levels\\l1data\\l1.sol", &solByteCount);
	ASSERT_TRUE(solBytesResult.has_value()) << solBytesResult.error();
	ASSERT_NE(solBytesResult->get(), nullptr);
	ASSERT_GT(solByteCount, static_cast<size_t>(0));
	ASSERT_LE(solByteCount, static_cast<size_t>(MAXTILES));
	std::vector<uint8_t> solBytes(solByteCount);
	std::memcpy(solBytes.data(), solBytesResult->get(), solByteCount);

	for (int piece = 0; piece < MAXTILES; piece++)
		tileProperties()[piece] = TileProperties::None;
	auto solLoad = LoadLevelSOLData();
	ASSERT_TRUE(solLoad.has_value()) << solLoad.error();

	std::vector<MegaTile> immutableTil(tilCount);
	std::copy_n(tilDefinitions->get(), tilCount, immutableTil.begin());
	const struct OracleInput {
		uint32_t seed;
		lvl_entry entry;
		const char *fileName;
		std::optional<uint32_t> attemptSeed;
		std::optional<uint32_t> finalRandomState;
		std::optional<Point> expectedEntry;
	} inputs[] = {
		{ UINT32_C(2588), ENTRY_MAIN, "cathedral-2588-main.json", UINT32_C(1779836089), UINT32_C(1552486206), Point { 77, 46 } },
		{ UINT32_C(2588), ENTRY_PREV, "cathedral-2588-previous.json", UINT32_C(1779836089), UINT32_C(1552486206), Point { 49, 49 } },
		{ UINT32_C(743271966), ENTRY_MAIN, "cathedral-743271966-main.json", UINT32_C(283661120), UINT32_C(980632310), Point { 51, 82 } },
		{ UINT32_C(743271966), ENTRY_PREV, "cathedral-743271966-previous.json", UINT32_C(283661120), UINT32_C(980632310), Point { 81, 47 } },
		{ UINT32_C(0), ENTRY_MAIN, "cathedral-0-main.json", std::nullopt, std::nullopt, std::nullopt },
		{ UINT32_C(0), ENTRY_PREV, "cathedral-0-previous.json", std::nullopt, std::nullopt, std::nullopt },
		{ UINT32_C(1), ENTRY_MAIN, "cathedral-1-main.json", std::nullopt, std::nullopt, std::nullopt },
		{ UINT32_C(1), ENTRY_PREV, "cathedral-1-previous.json", std::nullopt, std::nullopt, std::nullopt },
		{ UINT32_C(0x80000000), ENTRY_MAIN, "cathedral-2147483648-main.json", std::nullopt, std::nullopt, std::nullopt },
		{ UINT32_C(0x80000000), ENTRY_PREV, "cathedral-2147483648-previous.json", std::nullopt, std::nullopt, std::nullopt },
		{ UINT32_C(0xFFFFFFFF), ENTRY_MAIN, "cathedral-4294967295-main.json", std::nullopt, std::nullopt, std::nullopt },
		{ UINT32_C(0xFFFFFFFF), ENTRY_PREV, "cathedral-4294967295-previous.json", std::nullopt, std::nullopt, std::nullopt },
	};
	std::vector<CathedralOracleCase> cases;
	cases.reserve(std::size(inputs));
	for (const auto &input : inputs) {
		megaTiles() = std::make_unique<MegaTile[]>(tilCount);
		std::copy(immutableTil.begin(), immutableTil.end(), megaTiles().get());
		cases.push_back(CaptureCathedralOracleCase(input.seed, input.entry));
		const CathedralOracleCase &captured = cases.back();
		if (input.attemptSeed.has_value())
			ASSERT_EQ(captured.attemptSeed, *input.attemptSeed);
		if (input.finalRandomState.has_value())
			ASSERT_EQ(captured.finalRandomState, *input.finalRandomState);
		if (input.expectedEntry.has_value())
			ASSERT_EQ(captured.entryPosition, *input.expectedEntry);
	}

	ASSERT_TRUE(WriteCathedralOracleRawAsset(outputPath / "l1.til", tilBytes.data(), tilBytes.size()));
	ASSERT_TRUE(WriteCathedralOracleRawAsset(outputPath / "l1.sol", solBytes.data(), solBytes.size()));
	for (size_t index = 0; index < std::size(inputs); index++) {
		ASSERT_TRUE(WriteCathedralOracleCase(outputPath / inputs[index].fileName, cases[index]));
	}
}

TEST(Drlg_l1, CathedralDepthFineGeometryLocalAssetOracle)
{
	const char *outputDirectory = std::getenv(CathedralDepthOracleDirectoryVariable);
	if (outputDirectory == nullptr || outputDirectory[0] == '\0') {
		GTEST_SKIP() << "Set " << CathedralDepthOracleDirectoryVariable
		             << " to run the opt-in local-asset Cathedral depth fine-geometry oracle";
	}

	const std::filesystem::path outputPath(outputDirectory);
	ASSERT_TRUE(std::filesystem::exists(outputPath))
		<< "Cathedral depth oracle output directory does not exist: " << outputPath.string();
	ASSERT_TRUE(std::filesystem::is_directory(outputPath))
		<< "Cathedral depth oracle output path is not a directory: " << outputPath.string();
	std::error_code pathError;
	const auto canonicalOutputPath = std::filesystem::weakly_canonical(outputPath, pathError);
	ASSERT_FALSE(pathError) << "Unable to canonicalize Cathedral depth oracle output path: " << pathError.message();
	const auto canonicalRepositoryPath = CathedralOracleRepositoryPath(pathError);
	ASSERT_FALSE(pathError) << "Unable to locate repository root for depth oracle isolation: " << pathError.message();
	std::string repositoryPrefix = CathedralOracleLower(canonicalRepositoryPath.generic_string());
	if (!repositoryPrefix.ends_with('/'))
		repositoryPrefix += '/';
	const std::string canonicalOutput = CathedralOracleLower(canonicalOutputPath.generic_string());
	ASSERT_FALSE(canonicalOutput == repositoryPrefix.substr(0, repositoryPrefix.size() - 1)
		|| canonicalOutput.starts_with(repositoryPrefix))
		<< "Cathedral depth oracle output must be outside the repository: " << canonicalOutputPath.string();
	ASSERT_TRUE(std::filesystem::is_empty(outputPath))
		<< "Cathedral depth oracle output directory must be fresh: " << outputPath.string();

	TestInitGame();
	// TestInitGame installs its preference directory as an asset override.
	// Remove that override and any prior main archive before resolving the
	// original archive through the production GOG-aware search order.
	UnloadModArchives();
	MpqArchives.erase(MainMpqPriority);
	gbIsSpawn = false;
	LoadGameArchives();
	ASSERT_TRUE(HaveMainData()) << "DIABDAT.MPQ was not discovered by LoadGameArchives";
	ASSERT_FALSE(gbIsSpawn) << "Cathedral depth oracle resolved spawn.mpq instead of original DIABDAT.MPQ";

	levelType() = DTYPE_CATHEDRAL;
	currentLevelNumber() = CathedralDepthFirst;

#ifndef UNPACKED_MPQS
	const auto &mainArchive = MpqArchives.at(MainMpqPriority);
	for (const std::string_view assetPath : std::array<std::string_view, 2> {
		"levels\\l1data\\l1.til",
		"levels\\l1data\\l1.sol",
	}) {
		AssetRef asset = FindAsset(assetPath);
		ASSERT_TRUE(asset.ok()) << "Required Cathedral depth asset is unavailable: " << assetPath;
		ASSERT_EQ(asset.archive, &mainArchive)
			<< "Cathedral depth asset did not resolve from the original main archive: " << assetPath;
	}
#endif

	size_t tilCount = 0;
	auto tilDefinitions = LoadFileInMemWithStatus<MegaTile>(
		"levels\\l1data\\l1.til", &tilCount);
	ASSERT_TRUE(tilDefinitions.has_value()) << tilDefinitions.error();
	ASSERT_NE(tilDefinitions->get(), nullptr);
	ASSERT_EQ(tilCount, static_cast<size_t>(GetTileCount(DTYPE_CATHEDRAL)));

	size_t tilByteCount = 0;
	auto tilBytesResult = LoadFileInMemWithStatus<std::byte>(
		"levels\\l1data\\l1.til", &tilByteCount);
	ASSERT_TRUE(tilBytesResult.has_value()) << tilBytesResult.error();
	ASSERT_NE(tilBytesResult->get(), nullptr);
	ASSERT_EQ(tilByteCount, tilCount * sizeof(MegaTile));
	std::vector<uint8_t> tilBytes(tilByteCount);
	std::memcpy(tilBytes.data(), tilBytesResult->get(), tilByteCount);

	size_t solByteCount = 0;
	auto solBytesResult = LoadFileInMemWithStatus<std::byte>(
		"levels\\l1data\\l1.sol", &solByteCount);
	ASSERT_TRUE(solBytesResult.has_value()) << solBytesResult.error();
	ASSERT_NE(solBytesResult->get(), nullptr);
	ASSERT_GT(solByteCount, static_cast<size_t>(0));
	ASSERT_LE(solByteCount, static_cast<size_t>(MAXTILES));
	std::vector<uint8_t> solBytes(solByteCount);
	std::memcpy(solBytes.data(), solBytesResult->get(), solByteCount);

	for (int piece = 0; piece < MAXTILES; piece++)
		tileProperties()[piece] = TileProperties::None;
	auto solLoad = LoadLevelSOLData();
	ASSERT_TRUE(solLoad.has_value()) << solLoad.error();

	std::vector<MegaTile> immutableTil(tilCount);
	std::copy_n(tilDefinitions->get(), tilCount, immutableTil.begin());
	const std::array<uint32_t, 6> seeds {
		UINT32_C(2588),
		UINT32_C(743271966),
		UINT32_C(0),
		UINT32_C(1),
		UINT32_C(0x80000000),
		UINT32_C(0xFFFFFFFF),
	};
	const std::array<lvl_entry, 2> entries { ENTRY_MAIN, ENTRY_PREV };
	size_t depthCaseCount = 0;
	for (int depth = CathedralDepthFirst; depth <= CathedralDepthLast; depth++) {
		ASSERT_EQ(GetLevelType(depth), DTYPE_CATHEDRAL);
		for (const uint32_t seed : seeds) {
			for (const lvl_entry entry : entries) {
				// Poison each per-depth slot and policy before capture. The capture
				// must reset only the selected depth and restore the explicit policy.
				constexpr uint32_t staleAttemptSeed = UINT32_C(0xDEADBEEF);
				LevelSeeds[depth] = staleAttemptSeed;
				currentLevelNumber() = depth;
				levelType() = DTYPE_CATHEDRAL;
				ASSERT_NE(MyPlayer, nullptr);
				MyPlayer->pOriginalCathedral = false;
				for (Quest &quest : Quests)
					quest._qactive = QUEST_INIT;
				ASSERT_EQ(currentLevelNumber(), depth);
				ASSERT_EQ(levelType(), DTYPE_CATHEDRAL);
				ASSERT_FALSE(MyPlayer->pOriginalCathedral);

				megaTiles() = std::make_unique<MegaTile[]>(tilCount);
				std::copy(immutableTil.begin(), immutableTil.end(), megaTiles().get());
				const CathedralOracleCase captured = CaptureCathedralOracleCaseAtLevel(seed, entry, depth, true);

				ASSERT_EQ(captured.level, depth);
				ASSERT_EQ(captured.megaTiles.size(), static_cast<size_t>(DMAXX * DMAXY));
				ASSERT_EQ(captured.coarseTransparency.size(), static_cast<size_t>(DMAXX * 2 * DMAXY * 2));
				ASSERT_EQ(captured.pieces.size(), static_cast<size_t>(NativeWorldWidth * NativeWorldHeight));
				ASSERT_EQ(captured.transparency.size(), static_cast<size_t>(NativeWorldWidth * NativeWorldHeight));
				ASSERT_EQ(captured.solProperties.size(), static_cast<size_t>(MAXTILES));
				ASSERT_EQ(currentLevelNumber(), depth);
				ASSERT_EQ(levelType(), DTYPE_CATHEDRAL);
				ASSERT_TRUE(MyPlayer->pOriginalCathedral);
				ASSERT_TRUE(LevelSeeds[depth].has_value());
				EXPECT_EQ(*LevelSeeds[depth], captured.attemptSeed);
				EXPECT_NE(captured.attemptSeed, staleAttemptSeed);
				for (const Quest &quest : Quests)
					EXPECT_EQ(quest._qactive, QUEST_NOTAVAIL);

				const char *entryName = entry == ENTRY_MAIN ? "main" : "previous";
				const std::string fileName = "cathedral-depth-" + std::to_string(depth) + "-"
				    + std::to_string(seed) + "-" + entryName + ".json";
				ASSERT_TRUE(WriteCathedralDepthOracleCase(outputPath / fileName, captured));
				depthCaseCount++;
			}
		}
	}

	ASSERT_EQ(depthCaseCount, CathedralDepthOracleCaseCount);
	ASSERT_TRUE(WriteCathedralOracleRawAsset(outputPath / "l1.til", tilBytes.data(), tilBytes.size()));
	ASSERT_TRUE(WriteCathedralOracleRawAsset(outputPath / "l1.sol", solBytes.data(), solBytes.size()));
}

TEST(Drlg_l1, CathedralDoorNativeObjectOracle)
{
	const char *outputDirectory = std::getenv(CathedralDoorOracleDirectoryVariable);
	if (outputDirectory == nullptr || outputDirectory[0] == '\0') {
		GTEST_SKIP() << "Set " << CathedralDoorOracleDirectoryVariable
		             << " to run the opt-in native Cathedral door object oracle";
	}

	const std::filesystem::path outputPath(outputDirectory);
	std::string outputError;
	ASSERT_TRUE(ValidateCathedralDoorOracleOutputPath(outputPath, true, outputError)) << outputError;
	CathedralDoorOracleScopeGuard nativeStateGuard;
	std::string assetError;
	auto assets = LoadCathedralDoorOracleAssets(assetError);
	ASSERT_TRUE(assets.has_value()) << assetError;

	const std::array<uint32_t, 2> seeds { UINT32_C(2588), UINT32_C(743271966) };
	const std::array<lvl_entry, 2> entries { ENTRY_MAIN, ENTRY_PREV };
	std::vector<CathedralDoorOracleCase> cases;
	std::vector<std::string> fileNames;
	cases.reserve(CathedralDoorOracleCaseCount);
	fileNames.reserve(CathedralDoorOracleCaseCount);
	bool hasLeftDoor = false;
	bool hasRightDoor = false;
	for (int level = CathedralDepthFirst; level <= CathedralDepthLast; level++) {
		for (const uint32_t seed : seeds) {
			for (const lvl_entry entry : entries) {
				CathedralDoorOracleCase captured;
				CathedralDoorOracleCase repeated;
				try {
					captured = CaptureCathedralDoorOracleCase(level, seed, entry, assets->megaTiles, false);
					repeated = CaptureCathedralDoorOracleCase(level, seed, entry, assets->megaTiles, false);
				} catch (const std::exception &exception) {
					ADD_FAILURE() << "Native Cathedral door capture failed for depth " << level
					             << ", seed " << seed << ", entry " << CathedralDoorOracleEntryName(entry)
					             << ": " << exception.what();
					return;
				}
				ASSERT_EQ(CathedralDoorOracleCaseFingerprint(captured), CathedralDoorOracleCaseFingerprint(repeated))
					<< "Repeated native Cathedral door capture changed for depth " << level
					<< ", seed " << seed << ", entry " << CathedralDoorOracleEntryName(entry);
				ASSERT_FALSE(captured.transitions.empty());
				for (const auto &transition : captured.transitions) {
					hasLeftDoor |= transition.identity.nativeType == static_cast<int>(OBJ_L1LDOOR);
					hasRightDoor |= transition.identity.nativeType == static_cast<int>(OBJ_L1RDOOR);
				}
				const char *entryName = CathedralDoorOracleEntryName(entry);
				fileNames.push_back("cathedral-door-depth-" + std::to_string(level) + "-"
				    + std::to_string(seed) + "-" + entryName + ".json");
				cases.push_back(std::move(captured));
			}
		}
	}

	ASSERT_EQ(cases.size(), CathedralDoorOracleCaseCount);
	ASSERT_TRUE(hasLeftDoor) << "Native Cathedral door corpus did not contain an L door";
	ASSERT_TRUE(hasRightDoor) << "Native Cathedral door corpus did not contain an R door";
	ASSERT_TRUE(WriteCathedralOracleRawAsset(outputPath / "l1.til", assets->tilBytes.data(), assets->tilBytes.size()));
	ASSERT_TRUE(WriteCathedralOracleRawAsset(outputPath / "l1.sol", assets->solBytes.data(), assets->solBytes.size()));
	ASSERT_TRUE(WriteCathedralDoorOracleMetadata(outputPath / "door-oracle-format.json"));
	for (size_t index = 0; index < cases.size(); index++)
		ASSERT_TRUE(WriteCathedralDoorOracleCase(outputPath / fileNames[index], cases[index]));
}

TEST(Drlg_l1, CathedralDoorNativeDeltaOpenOracle)
{
	const char *outputDirectory = std::getenv(CathedralDoorOracleDirectoryVariable);
	if (outputDirectory == nullptr || outputDirectory[0] == '\0') {
		GTEST_SKIP() << "Set " << CathedralDoorOracleDirectoryVariable
		             << " to run the opt-in native Cathedral delta-open oracle";
	}

	const std::filesystem::path outputPath(outputDirectory);
	std::string outputError;
	ASSERT_TRUE(ValidateCathedralDoorOracleOutputPath(outputPath, false, outputError)) << outputError;
	CathedralDoorOracleScopeGuard nativeStateGuard;
	std::string assetError;
	auto assets = LoadCathedralDoorOracleAssets(assetError);
	ASSERT_TRUE(assets.has_value()) << assetError;
	const bool outputWasEmpty = std::filesystem::is_empty(outputPath);

	CathedralDoorOracleCase captured;
	CathedralDoorOracleCase repeated;
	try {
		captured = CaptureCathedralDoorOracleCase(
			2, UINT32_C(743271966), ENTRY_MAIN, assets->megaTiles, true);
		repeated = CaptureCathedralDoorOracleCase(
			2, UINT32_C(743271966), ENTRY_MAIN, assets->megaTiles, true);
	} catch (const std::exception &exception) {
		FAIL() << "Native Cathedral delta-open capture failed: " << exception.what();
	}
	ASSERT_FALSE(captured.transitions.empty());
	ASSERT_EQ(CathedralDoorOracleCaseFingerprint(captured), CathedralDoorOracleCaseFingerprint(repeated))
		<< "Repeated native Cathedral delta-open capture changed.";
	if (outputWasEmpty) {
		ASSERT_TRUE(WriteCathedralOracleRawAsset(outputPath / "l1.til", assets->tilBytes.data(), assets->tilBytes.size()));
		ASSERT_TRUE(WriteCathedralOracleRawAsset(outputPath / "l1.sol", assets->solBytes.data(), assets->solBytes.size()));
		ASSERT_TRUE(WriteCathedralDoorOracleMetadata(outputPath / "door-oracle-format.json"));
	}
	ASSERT_TRUE(WriteCathedralDoorOracleCase(
		outputPath / "cathedral-door-delta-depth-2-743271966-main.json", captured));
}

// These tests exercise the live native door state machine without adding any
// files to the private door oracle. They intentionally use the original asset
// loader above, so the production AddObject/SyncOpObject/DeltaSyncOpObject
// paths see the same object data and L1 SOL properties as the oracle.
class CathedralDoorNativeEdgeTest : public ::testing::Test {
protected:
	void SetUp() override
	{
		const char *assetDirectory = std::getenv(CathedralDoorOracleDirectoryVariable);
		if (assetDirectory == nullptr || assetDirectory[0] == '\0') {
			GTEST_SKIP() << "Set " << CathedralDoorOracleDirectoryVariable
			             << " to run opt-in native Cathedral door edge tests";
		}
		const std::filesystem::path outputPath(assetDirectory);
		std::string outputError;
		if (!ValidateCathedralDoorOracleOutputPath(outputPath, false, outputError))
			GTEST_SKIP() << outputError;

		nativeStateGuard.emplace();
		std::string assetError;
		if (!LoadCathedralDoorOracleAssets(assetError).has_value())
			GTEST_SKIP() << assetError;

		ResetCathedralDoorOracleNativeObjectsAndLights();
		currentLevel().setId(Level::create(LevelId { .levelNum = 1, .type = DTYPE_CATHEDRAL }).id());
		currentLevelNumber() = 1;
		levelType() = DTYPE_CATHEDRAL;
		for (Tile &tile : tiles().columnMajor())
			tile.clear();
		SetRndSeed(UINT32_C(0x13579BDF));
	}

public:
	Object *AddSyntheticL1Door(_object_id objectType, Point position)
	{
		const Direction openDirection = objectType == OBJ_L1LDOOR
			? Direction::NorthEast
			: Direction::NorthWest;
		tileAt(position).setPiece(49);
		tileAt(position + openDirection).setPiece(49);
		return AddObject(objectType, position);
	}

	std::optional<CathedralDoorOracleScopeGuard> nativeStateGuard;
};

void SetCathedralDoorOccupancy(Point position, char occupancy)
{
	Tile &tile = tileAt(position);
	tile.setPlayer(0);
	tile.setMonster(0);
	tile.setItem(0);
	tile.setCorpse(0);
	switch (occupancy) {
	case 'p':
		tile.setPlayer(1);
		break;
	case 'm':
		tile.setMonster(1);
		break;
	case 'i':
		tile.setItem(1);
		break;
	case 'c':
		tile.setCorpse(1);
		break;
	default:
		break;
	}
}

void ExerciseCathedralDoorBlockedClose(CathedralDoorNativeEdgeTest &fixture, char occupancy)
{
	Object *door = fixture.AddSyntheticL1Door(OBJ_L1LDOOR, { 60, 60 });
	ASSERT_NE(door, nullptr);
	ASSERT_EQ(door->_oVar1, 50);
	ASSERT_EQ(door->_oVar2, 50);
	const CathedralDoorOracleLayer closedLayer = CaptureCathedralDoorOracleLayer();
	const CathedralDoorOracleObjectState closedObject = CaptureCathedralDoorOracleObject(*door);

	const uint32_t randomStateBeforeOpen = GetLCGEngineState();
	DeltaSyncOpObject(*door);
	ASSERT_EQ(door->_oVar4, NativeDoorOpenState);
	EXPECT_EQ(GetLCGEngineState(), randomStateBeforeOpen);
	const CathedralDoorOracleObjectState openObject = CaptureCathedralDoorOracleObject(*door);
	ASSERT_EQ(openObject.animationFrame, closedObject.animationFrame + 2);

	SetCathedralDoorOccupancy(door->position, occupancy);
	const CathedralDoorOracleLayer blockedBeforeLayer = CaptureCathedralDoorOracleLayer();
	const CathedralDoorOracleObjectState blockedBeforeObject = CaptureCathedralDoorOracleObject(*door);
	const uint32_t randomStateBeforeBlockedClose = GetLCGEngineState();
	Player remotePlayer {};
	SyncOpObject(remotePlayer, CMD_CLOSEDOOR, *door);
	EXPECT_EQ(door->_oVar4, NativeDoorBlockedState) << "native DOOR_BLOCKED state";
	EXPECT_EQ(door->_oAnimFrame, openObject.animationFrame);
	EXPECT_TRUE(CathedralDoorOracleLayerEqual(blockedBeforeLayer, CaptureCathedralDoorOracleLayer()));
	const auto blockedObject = CaptureCathedralDoorOracleObject(*door);
	EXPECT_EQ(blockedObject.var1OneBased, blockedBeforeObject.var1OneBased);
	EXPECT_EQ(blockedObject.var2OneBased, blockedBeforeObject.var2OneBased);
	EXPECT_EQ(blockedObject.animationFrame, blockedBeforeObject.animationFrame);
	EXPECT_EQ(blockedObject.selectionRegion, blockedBeforeObject.selectionRegion);
	EXPECT_EQ(blockedObject.doorFlag, blockedBeforeObject.doorFlag);
	EXPECT_EQ(blockedObject.preFlag, blockedBeforeObject.preFlag);
	EXPECT_EQ(blockedObject.missFlag, blockedBeforeObject.missFlag);
	EXPECT_EQ(blockedObject.solidFlag, blockedBeforeObject.solidFlag);
	EXPECT_EQ(GetLCGEngineState(), randomStateBeforeBlockedClose);

	SetCathedralDoorOccupancy(door->position, '\0');
	SyncOpObject(remotePlayer, CMD_CLOSEDOOR, *door);
	EXPECT_EQ(door->_oVar4, NativeDoorClosedState);
	EXPECT_EQ(door->_oAnimFrame, closedObject.animationFrame);
	EXPECT_TRUE(CathedralDoorOracleLayerEqual(closedLayer, CaptureCathedralDoorOracleLayer()));
	EXPECT_TRUE(CathedralDoorOracleObjectEqual(closedObject, CaptureCathedralDoorOracleObject(*door)));
	EXPECT_EQ(GetLCGEngineState(), randomStateBeforeBlockedClose);
}

TEST_F(CathedralDoorNativeEdgeTest, CloseBlockedByPlayerThenReleased)
{
	ExerciseCathedralDoorBlockedClose(*this, 'p');
}

TEST_F(CathedralDoorNativeEdgeTest, CloseBlockedByMonsterThenReleased)
{
	ExerciseCathedralDoorBlockedClose(*this, 'm');
}

TEST_F(CathedralDoorNativeEdgeTest, CloseBlockedByItemThenReleased)
{
	ExerciseCathedralDoorBlockedClose(*this, 'i');
}

TEST_F(CathedralDoorNativeEdgeTest, CloseBlockedByCorpseThenReleased)
{
	ExerciseCathedralDoorBlockedClose(*this, 'c');
}

TEST_F(CathedralDoorNativeEdgeTest, ProcessObjectsTracksDoorOccupancy)
{
	Object *door = AddSyntheticL1Door(OBJ_L1LDOOR, { 60, 60 });
	ASSERT_NE(door, nullptr);
	const uint32_t randomStateBeforeOpen = GetLCGEngineState();
	DeltaSyncOpObject(*door);
	ASSERT_EQ(door->_oVar4, NativeDoorOpenState);
	EXPECT_EQ(GetLCGEngineState(), randomStateBeforeOpen);
	const auto openLayer = CaptureCathedralDoorOracleLayer();
	const auto openFrame = door->_oAnimFrame;
	const uint32_t randomState = GetLCGEngineState();

	SetCathedralDoorOccupancy(door->position, 'p');
	ProcessObjects();
	EXPECT_EQ(door->_oVar4, NativeDoorBlockedState) << "UpdateDoor marks occupied open door BLOCKED";
	EXPECT_EQ(door->_oAnimFrame, openFrame);
	EXPECT_TRUE(CathedralDoorOracleLayerEqual(openLayer, CaptureCathedralDoorOracleLayer()));
	EXPECT_EQ(GetLCGEngineState(), randomState);

	SetCathedralDoorOccupancy(door->position, '\0');
	ProcessObjects();
	EXPECT_EQ(door->_oVar4, NativeDoorOpenState) << "UpdateDoor reopens state after occupancy is released";
	EXPECT_EQ(door->_oAnimFrame, openFrame);
	EXPECT_TRUE(CathedralDoorOracleLayerEqual(openLayer, CaptureCathedralDoorOracleLayer()));
	EXPECT_EQ(GetLCGEngineState(), randomState);
}

TEST_F(CathedralDoorNativeEdgeTest, DeadPlayerFlagDoesNotBlockDoor)
{
	Object *door = AddSyntheticL1Door(OBJ_L1LDOOR, { 60, 60 });
	ASSERT_NE(door, nullptr);
	DeltaSyncOpObject(*door);
	SetCathedralDoorOccupancy(door->position, '\0');
	tileAt(door->position).addFlags(DungeonFlag::DeadPlayer);
	ProcessObjects();
	EXPECT_EQ(door->_oVar4, NativeDoorOpenState);
	Player remotePlayer {};
	SyncOpObject(remotePlayer, CMD_CLOSEDOOR, *door);
	EXPECT_EQ(door->_oVar4, NativeDoorClosedState);
	EXPECT_TRUE(tileAt(door->position).hasAnyFlag(DungeonFlag::DeadPlayer));
}

TEST_F(CathedralDoorNativeEdgeTest, SharedArchCloseUsesCurrentStateAnd395Branch)
{
	const Point leftPosition { 60, 60 };
	const Point rightPosition { 61, 59 };
	const Point sharedArchPosition { 60, 59 };
	Object *leftDoor = AddSyntheticL1Door(OBJ_L1LDOOR, leftPosition);
	Object *rightDoor = AddSyntheticL1Door(OBJ_L1RDOOR, rightPosition);
	ASSERT_NE(leftDoor, nullptr);
	ASSERT_NE(rightDoor, nullptr);
	ASSERT_EQ(leftDoor->_oVar1, 50);
	ASSERT_EQ(leftDoor->_oVar2, 50);
	ASSERT_EQ(rightDoor->_oVar1, 50);
	ASSERT_EQ(rightDoor->_oVar2, 50);
	const auto closedLayer = CaptureCathedralDoorOracleLayer();
	const auto leftClosedFrame = leftDoor->_oAnimFrame;
	const auto rightClosedFrame = rightDoor->_oAnimFrame;
	const uint32_t randomState = GetLCGEngineState();
	Player remotePlayer {};

	// A close command is ignored when the native object is already CLOSED.
	SyncOpObject(remotePlayer, CMD_CLOSEDOOR, *leftDoor);
	EXPECT_EQ(leftDoor->_oVar4, NativeDoorClosedState);
	EXPECT_EQ(leftDoor->_oAnimFrame, leftClosedFrame);
	EXPECT_EQ(tileAt(sharedArchPosition).piece(), 49);
	EXPECT_EQ(GetLCGEngineState(), randomState);

	// Use the native delta path for the first open, then exercise SyncOpObject's
	// current-state guard and the shared-arch ordering through the remote path.
	DeltaSyncOpObject(*leftDoor);
	ASSERT_EQ(leftDoor->_oVar4, NativeDoorOpenState);
	EXPECT_EQ(tileAt(sharedArchPosition).piece(), 410);
	const auto leftOpenFrame = leftDoor->_oAnimFrame;
	SyncOpObject(remotePlayer, CMD_OPENDOOR, *leftDoor);
	EXPECT_EQ(leftDoor->_oVar4, NativeDoorOpenState);
	EXPECT_EQ(leftDoor->_oAnimFrame, leftOpenFrame);

	SyncOpObject(remotePlayer, CMD_OPENDOOR, *rightDoor);
	ASSERT_EQ(rightDoor->_oVar4, NativeDoorOpenState);
	EXPECT_EQ(rightDoor->_oAnimFrame, rightClosedFrame + 2);
	EXPECT_EQ(tileAt(sharedArchPosition).piece(), 395);

	// Closing left while the shared arch is 395 selects the native 411 frame;
	// reversing this order would not take the var2==50/395 branch.
	SyncOpObject(remotePlayer, CMD_CLOSEDOOR, *leftDoor);
	EXPECT_EQ(leftDoor->_oVar4, NativeDoorClosedState);
	EXPECT_EQ(leftDoor->_oAnimFrame, leftClosedFrame);
	EXPECT_EQ(tileAt(sharedArchPosition).piece(), 411);
	SyncOpObject(remotePlayer, CMD_CLOSEDOOR, *leftDoor);
	EXPECT_EQ(tileAt(sharedArchPosition).piece(), 411);

	SyncOpObject(remotePlayer, CMD_CLOSEDOOR, *rightDoor);
	EXPECT_EQ(rightDoor->_oVar4, NativeDoorClosedState);
	EXPECT_EQ(rightDoor->_oAnimFrame, rightClosedFrame);
	EXPECT_EQ(tileAt(sharedArchPosition).piece(), 49);
	EXPECT_TRUE(CathedralDoorOracleLayerEqual(closedLayer, CaptureCathedralDoorOracleLayer()));
	EXPECT_EQ(GetLCGEngineState(), randomState);
}

TEST(Drlg_l1, CreateL5Dungeon_reuses_successful_attempt_seed)
{
	TestInitGame();
	constexpr uint32_t dungeonSeed = UINT32_C(2588);
	constexpr lvl_entry entry = ENTRY_MAIN;

	LevelSeeds[1] = std::nullopt;
	currentLevelNumber() = 1;
	levelType() = GetLevelType(1);
	megaTiles() = std::make_unique<MegaTile[]>(GetTileCount(levelType()));
	CreateDungeon(dungeonSeed, entry);
	ASSERT_TRUE(LevelSeeds[1].has_value());
	const uint32_t successfulAttemptSeed = *LevelSeeds[1];
	const uint32_t initialFinalRandomState = GetLCGEngineState();

	std::array<uint16_t, DMAXX * DMAXY> initialMegaTiles;
	std::array<uint8_t, (DMAXX * 2) * (DMAXY * 2)> initialTransparency;
	for (int y = 0; y < DMAXY; y++) {
		for (int x = 0; x < DMAXX; x++)
			initialMegaTiles[y * DMAXX + x] = megaTileAt(x, y).current();
	}
	for (int y = 0; y < DMAXY * 2; y++) {
		for (int x = 0; x < DMAXX * 2; x++)
			initialTransparency[y * DMAXX * 2 + x] = tileAt(x + 16, y + 16).transVal();
	}

	LevelSeeds[1] = successfulAttemptSeed;
	megaTiles() = std::make_unique<MegaTile[]>(GetTileCount(levelType()));
	CreateDungeon(dungeonSeed, entry);
	ASSERT_TRUE(LevelSeeds[1].has_value());
	EXPECT_EQ(*LevelSeeds[1], successfulAttemptSeed);
	EXPECT_EQ(GetLCGEngineState(), initialFinalRandomState);
	for (int y = 0; y < DMAXY; y++) {
		for (int x = 0; x < DMAXX; x++)
			EXPECT_EQ(megaTileAt(x, y).current(), initialMegaTiles[y * DMAXX + x]);
	}
	for (int y = 0; y < DMAXY * 2; y++) {
		for (int x = 0; x < DMAXX * 2; x++)
			EXPECT_EQ(tileAt(x + 16, y + 16).transVal(), initialTransparency[y * DMAXX * 2 + x]);
	}
}

TEST(Drlg_l1, CreateL5Dungeon_diablo_2_1383137027)
{
	LoadExpectedLevelData("diablo/2-1383137027.dun");

	TestInitGame();
	Quests[Q_PWATER]._qactive = QUEST_INIT;
	Quests[Q_BUTCHER]._qactive = QUEST_NOTAVAIL;

	TestCreateDungeon(2, 1383137027, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(57, 74));
	TestCreateDungeon(2, 1383137027, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(57, 79));
	TestCreateDungeon(2, 1383137027, ENTRY_RTNLVL);
	EXPECT_EQ(viewPosition(), Point(49, 88));
}

TEST(Drlg_l1, CreateL5Dungeon_diablo_3_844660068)
{
	LoadExpectedLevelData("diablo/3-844660068.dun");

	TestInitGame();

	TestCreateDungeon(3, 844660068, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(67, 52));
	TestCreateDungeon(3, 844660068, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(85, 45));
}

TEST(Drlg_l1, CreateL5Dungeon_diablo_4_609325643)
{
	LoadExpectedLevelData("diablo/4-609325643.dun");

	TestInitGame();
	Quests[Q_LTBANNER]._qactive = QUEST_NOTAVAIL;

	TestCreateDungeon(4, 609325643, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(85, 78));
	TestCreateDungeon(4, 609325643, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(55, 47));
}

TEST(Drlg_l1, CreateL5Dungeon_diablo_4_902156014)
{
	LoadExpectedLevelData("diablo/4-902156014.dun");

	TestInitGame();
	Quests[Q_LTBANNER]._qactive = QUEST_INIT;

	TestCreateDungeon(4, 902156014, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(75, 56));
	TestCreateDungeon(4, 902156014, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(51, 59));
}

TEST(Drlg_l1, CreateL5Dungeon_hellfire_1_401921334)
{
	LoadExpectedLevelData("hellfire/1-401921334.dun");

	TestInitGame(true, false);

	TestCreateDungeon(1, 401921334, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(79, 80));
	TestCreateDungeon(1, 401921334, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(49, 63));
}

TEST(Drlg_l1, CreateL5Dungeon_hellfire_1_536340718)
{
	LoadExpectedLevelData("hellfire/1-536340718.dun");

	TestInitGame(true, false);

	TestCreateDungeon(1, 536340718, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(55, 72));
	TestCreateDungeon(1, 536340718, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(49, 63));
}

TEST(Drlg_l1, CreateL5Dungeon_hellfire_2_128964898)
{
	LoadExpectedLevelData("hellfire/2-128964898.dun");

	TestInitGame(true, false);
	Quests[Q_PWATER]._qactive = QUEST_NOTAVAIL;
	Quests[Q_BUTCHER]._qactive = QUEST_NOTAVAIL;

	TestCreateDungeon(2, 128964898, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(55, 68));
	TestCreateDungeon(2, 128964898, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(49, 63));
}

TEST(Drlg_l1, CreateL5Dungeon_hellfire_2_1180526547)
{
	LoadExpectedLevelData("hellfire/2-1180526547.dun");

	TestInitGame(true, false);
	Quests[Q_PWATER]._qactive = QUEST_NOTAVAIL;
	Quests[Q_BUTCHER]._qactive = QUEST_INIT;

	TestCreateDungeon(2, 1180526547, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(83, 68));
	TestCreateDungeon(2, 1180526547, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(81, 45));
}

TEST(Drlg_l1, CreateL5Dungeon_hellfire_3_1369955278)
{
	LoadExpectedLevelData("hellfire/3-1369955278.dun");

	TestInitGame(true, false);
	Quests[Q_SKELKING]._qactive = QUEST_INIT;

	TestCreateDungeon(3, 1369955278, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(63, 86));
	TestCreateDungeon(3, 1369955278, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(75, 69));
}

TEST(Drlg_l1, CreateL5Dungeon_hellfire_3_1799396623)
{
	LoadExpectedLevelData("hellfire/3-1799396623.dun");

	TestInitGame(true, false);
	Quests[Q_SKELKING]._qactive = QUEST_NOTAVAIL;

	TestCreateDungeon(3, 1799396623, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(59, 68));
	TestCreateDungeon(3, 1799396623, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(47, 55));
}

TEST(Drlg_l1, CreateL5Dungeon_hellfire_3_1512491184)
{
	LoadExpectedLevelData("hellfire/3-1512491184.dun");

	TestInitGame(true, false);
	Quests[Q_SKELKING]._qactive = QUEST_INIT;

	TestCreateDungeon(3, 1512491184, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(47, 72));
	TestCreateDungeon(3, 1512491184, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(79, 45));
}

TEST(Drlg_l1, CreateL5Dungeon_hellfire_4_1190318991)
{
	LoadExpectedLevelData("hellfire/4-1190318991.dun");

	TestInitGame(true, false);
	Quests[Q_LTBANNER]._qactive = QUEST_NOTAVAIL;

	TestCreateDungeon(4, 1190318991, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(67, 80));
	TestCreateDungeon(4, 1190318991, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(77, 45));
}

TEST(Drlg_l1, CreateL5Dungeon_hellfire_4_1924296259)
{
	LoadExpectedLevelData("hellfire/4-1924296259.dun");

	TestInitGame(true, false);
	Quests[Q_LTBANNER]._qactive = QUEST_INIT;

	TestCreateDungeon(4, 1924296259, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(83, 54));
	TestCreateDungeon(4, 1924296259, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(51, 87));
}

TEST(Drlg_l1, CreateL5Dungeon_crypt_1_2122696790)
{
	LoadExpectedLevelData("hellfire/21-2122696790.dun");

	TestInitGame(true, true, true);

	TestCreateDungeon(21, 2122696790, ENTRY_TWARPDN);
	EXPECT_EQ(viewPosition(), Point(61, 81));
	TestCreateDungeon(21, 2122696790, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(53, 67));
}

TEST(Drlg_l1, CreateL5Dungeon_crypt_2_1191662129)
{
	LoadExpectedLevelData("hellfire/22-1191662129.dun");

	TestInitGame(true, true, true);
	Quests[Q_PWATER]._qactive = QUEST_NOTAVAIL;
	Quests[Q_BUTCHER]._qactive = QUEST_NOTAVAIL;

	TestCreateDungeon(22, 1191662129, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(71, 47));
	TestCreateDungeon(22, 1191662129, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(85, 71));
}

TEST(Drlg_l1, CreateL5Dungeon_crypt_3_97055268)
{
	LoadExpectedLevelData("hellfire/23-97055268.dun");

	TestInitGame(true, true, true);

	TestCreateDungeon(23, 97055268, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(71, 57));
	TestCreateDungeon(23, 97055268, ENTRY_PREV);
	EXPECT_EQ(viewPosition(), Point(81, 59));
}

TEST(Drlg_l1, CreateL5Dungeon_crypt_4_1324803725)
{
	LoadExpectedLevelData("hellfire/24-1324803725.dun");

	TestInitGame(true, true, true);

	TestCreateDungeon(24, 1324803725, ENTRY_MAIN);
	EXPECT_EQ(viewPosition(), Point(79, 47));
}

} // namespace

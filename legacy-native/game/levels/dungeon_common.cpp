/**
 * @file game/levels/dungeon_common.cpp
 *
 * Implementation of dungeon_common.
 */


#include "game/levels/dungeon_common.h"

#include <cstddef>
#include <cstdint>
#include <memory>
#include <stack>
#include <string>
#include <utility>
#include <vector>

#include <ankerl/unordered_dense.h>
#include <expected.hpp>
#include <magic_enum/magic_enum.hpp>

#include "engine/gfx/clx_sprite.hpp"
#include "engine/load/load_file.hpp"
#include "engine/random.hpp"
#include "engine/math/world_tile.hpp"
#include "application/game_mode.hpp"
#include "game/items/items.hpp"
#include "game/levels/level_l1.h"
#include "game/levels/level_l2.h"
#include "game/levels/level_l3.h"
#include "game/levels/level_l4.h"
#include "game/levels/reencode_dun_cels.hpp"
#include "game/levels/town.h"
#include "engine/lighting.h"
#include "game/monsters/monsters.hpp"
#include "game/objects/objects.hpp"
#include "utils/algorithm/container.hpp"
#include "utils/container/bitset2d.hpp"
#include "utils/endian/endian_swap.hpp"
#include "utils/is_of.hpp"
#include "utils/log.hpp"
#include "utils/status_macros.hpp"

namespace devilution {

namespace {

std::unique_ptr<uint16_t[]> LoadMinData(size_t &tileCount)
{
	switch (levelType()) {
	case DTYPE_TOWN: {
		auto min = LoadFileInMemWithStatus<uint16_t>("nlevels\\towndata\\town.min", &tileCount);
		if (!min.has_value()) {
			return LoadFileInMem<uint16_t>("levels\\towndata\\town.min", &tileCount);
		} else {
			return std::move(*min);
		}
	}
	case DTYPE_CATHEDRAL:
		return LoadFileInMem<uint16_t>("levels\\l1data\\l1.min", &tileCount);
	case DTYPE_CATACOMBS:
		return LoadFileInMem<uint16_t>("levels\\l2data\\l2.min", &tileCount);
	case DTYPE_CAVES:
		return LoadFileInMem<uint16_t>("levels\\l3data\\l3.min", &tileCount);
	case DTYPE_HELL:
		return LoadFileInMem<uint16_t>("levels\\l4data\\l4.min", &tileCount);
	case DTYPE_NEST:
		return LoadFileInMem<uint16_t>("nlevels\\l6data\\l6.min", &tileCount);
	case DTYPE_CRYPT:
		return LoadFileInMem<uint16_t>("nlevels\\l5data\\l5.min", &tileCount);
	default:
		app_fatal("LoadMinData");
	}
}

/**
 * @brief Starting from the origin point determine how much floor space is available with the given bounds
 *
 * Essentially looks for the widest/tallest rectangular area of at least the minimum size, but due to a weird/buggy
 * bounds check can return an area smaller than the available width/height.
 *
 * @param floor what value defines floor tiles() within a dungeon
 * @param origin starting point for the search
 * @param minSize minimum allowable value for both dimensions
 * @param maxSize maximum allowable value for both dimensions
 * @return how much width/height is available for a theme room or an empty optional if there's not enough space
 */
std::optional<WorldTileSize> GetSizeForThemeRoom(uint8_t floor, WorldTilePosition origin, WorldTileCoord minSize, WorldTileCoord maxSize)
{
	if (origin.x + maxSize > DMAXX && origin.y + maxSize > DMAXY) {
		return {}; // Original broken bounds check, avoids lower right corner
	}
	if (IsNearThemeRoom(origin)) {
		return {};
	}

	const WorldTileCoord maxWidth = std::min<WorldTileCoord>(maxSize, DMAXX - origin.x);
	const WorldTileCoord maxHeight = std::min<WorldTileCoord>(maxSize, DMAXY - origin.y);

	WorldTileSize room { maxWidth, maxHeight };

	for (WorldTileCoord i = 0; i < maxSize; i++) {
		WorldTileCoord width = i < room.height ? i : 0;
		if (i < maxHeight) {
			while (width < room.width) {
				if (megaTileAt(origin.x + width, origin.y + i).current() != floor)
					break;

				width++;
			}
		}

		WorldTileCoord height = i < room.width ? i : 0;
		if (i < maxWidth) {
			while (height < room.height) {
				if (megaTileAt(origin.x + i, origin.y + height).current() != floor)
					break;

				height++;
			}
		}

		if (width < minSize || height < minSize) {
			if (i < minSize)
				return {};
			break;
		}

		room = { std::min(room.width, width), std::min(room.height, height) };
	}

	return room - 2;
}

void CreateThemeRoom(int themeIndex)
{
	const int lx = themeLocations()[themeIndex].room.position.x;
	const int ly = themeLocations()[themeIndex].room.position.y;
	const int hx = lx + themeLocations()[themeIndex].room.size.width;
	const int hy = ly + themeLocations()[themeIndex].room.size.height;

	for (int yy = ly; yy < hy; yy++) {
		for (int xx = lx; xx < hx; xx++) {
			if (levelType() == DTYPE_CATACOMBS) {
				if (yy == ly || yy == hy - 1) {
					megaTileAt(xx, yy).setCurrent(2);
				} else if (xx == lx || xx == hx - 1) {
					megaTileAt(xx, yy).setCurrent(1);
				} else {
					megaTileAt(xx, yy).setCurrent(3);
				}
			}
			if (IsAnyOf(levelType(), DTYPE_CAVES, DTYPE_NEST)) {
				if (yy == ly || yy == hy - 1) {
					megaTileAt(xx, yy).setCurrent(134);
				} else if (xx == lx || xx == hx - 1) {
					megaTileAt(xx, yy).setCurrent(137);
				} else {
					megaTileAt(xx, yy).setCurrent(7);
				}
			}
			if (levelType() == DTYPE_HELL) {
				if (yy == ly || yy == hy - 1) {
					megaTileAt(xx, yy).setCurrent(2);
				} else if (xx == lx || xx == hx - 1) {
					megaTileAt(xx, yy).setCurrent(1);
				} else {
					megaTileAt(xx, yy).setCurrent(6);
				}
			}
		}
	}

	if (levelType() == DTYPE_CATACOMBS) {
		megaTileAt(lx, ly).setCurrent(8);
		megaTileAt(hx - 1, ly).setCurrent(7);
		megaTileAt(lx, hy - 1).setCurrent(9);
		megaTileAt(hx - 1, hy - 1).setCurrent(6);
	}
	if (IsAnyOf(levelType(), DTYPE_CAVES, DTYPE_NEST)) {
		megaTileAt(lx, ly).setCurrent(150);
		megaTileAt(hx - 1, ly).setCurrent(151);
		megaTileAt(lx, hy - 1).setCurrent(152);
		megaTileAt(hx - 1, hy - 1).setCurrent(138);
	}
	if (levelType() == DTYPE_HELL) {
		megaTileAt(lx, ly).setCurrent(9);
		megaTileAt(hx - 1, ly).setCurrent(16);
		megaTileAt(lx, hy - 1).setCurrent(15);
		megaTileAt(hx - 1, hy - 1).setCurrent(12);
	}

	if (levelType() == DTYPE_CATACOMBS) {
		if (FlipCoin())
			megaTileAt(hx - 1, (ly + hy) / 2).setCurrent(4);
		else
			megaTileAt((lx + hx) / 2, hy - 1).setCurrent(5);
	}
	if (IsAnyOf(levelType(), DTYPE_CAVES, DTYPE_NEST)) {
		if (FlipCoin())
			megaTileAt(hx - 1, (ly + hy) / 2).setCurrent(147);
		else
			megaTileAt((lx + hx) / 2, hy - 1).setCurrent(146);
	}
	if (levelType() == DTYPE_HELL) {
		if (FlipCoin()) {
			const int yy = (ly + hy) / 2;
			megaTileAt(hx - 1, yy - 1).setCurrent(53);
			megaTileAt(hx - 1, yy).setCurrent(6);
			megaTileAt(hx - 1, yy + 1).setCurrent(52);
			megaTileAt(hx - 2, yy - 1).setCurrent(54);
		} else {
			const int xx = (lx + hx) / 2;
			megaTileAt(xx - 1, hy - 1).setCurrent(57);
			megaTileAt(xx, hy - 1).setCurrent(6);
			megaTileAt(xx + 1, hy - 1).setCurrent(56);
			megaTileAt(xx, hy - 2).setCurrent(59);
			megaTileAt(xx - 1, hy - 2).setCurrent(58);
		}
	}
}

bool IsFloor(Point p, uint8_t floorID)
{
	const int i = (p.x - 16) / 2;
	const int j = (p.y - 16) / 2;
	if (i < 0 || i >= DMAXX)
		return false;
	if (j < 0 || j >= DMAXY)
		return false;
	return megaTileAt(i, j).current() == floorID;
}

void FillTransparencyValues(Point floor, uint8_t floorID)
{
	const Direction allDirections[] = {
		Direction::North,
		Direction::South,
		Direction::East,
		Direction::West,
		Direction::NorthEast,
		Direction::NorthWest,
		Direction::SouthEast,
		Direction::SouthWest,
	};

	// We only fill in the surrounding tiles() if they are not floor tiles()
	// because they would otherwise not be visited by the span filling algorithm
	for (const Direction dir : allDirections) {
		const Point adjacent = floor + dir;
		if (!IsFloor(adjacent, floorID))
			tileAt(adjacent).setTransVal(nextTransparencyValue());
	}

	tileAt(floor).setTransVal(nextTransparencyValue());
}

void FindTransparencyValues(Point floor, uint8_t floorID)
{
	// Algorithm adapted from https://en.wikipedia.org/wiki/Flood_fill#Span_Filling
	// Modified to include diagonally adjacent tiles() that would otherwise not be visited
	// Also, Wikipedia's selection for the initial seed is incorrect
	struct Seed {
		int scanStart;
		int scanEnd;
		int y;
		int dy;
	};
	std::stack<Seed, std::vector<Seed>> seedStack;
	seedStack.push({ floor.x, floor.x + 1, floor.y, 1 });

	const auto isInside = [floorID](int x, int y) {
		if (tileAt(x, y).transVal() != 0)
			return false;
		return IsFloor({ x, y }, floorID);
	};

	const auto set = [floorID](int x, int y) {
		FillTransparencyValues({ x, y }, floorID);
	};

	const Displacement left = { -1, 0 };
	const Displacement right = { 1, 0 };
	const auto checkDiagonals = [&](Point p, Displacement direction) {
		const Point up = p + Displacement { 0, -1 };
		const Point upOver = up + direction;
		if (!isInside(up.x, up.y) && isInside(upOver.x, upOver.y))
			seedStack.push({ upOver.x, upOver.x + 1, upOver.y, -1 });

		const Point down = p + Displacement { 0, 1 };
		const Point downOver = down + direction;
		if (!isInside(down.x, down.y) && isInside(downOver.x, downOver.y))
			seedStack.push(Seed { downOver.x, downOver.x + 1, downOver.y, 1 });
	};

	while (!seedStack.empty()) {
		const auto [scanStart, scanEnd, y, dy] = seedStack.top();
		seedStack.pop();

		int scanLeft = scanStart;
		if (isInside(scanLeft, y)) {
			while (isInside(scanLeft - 1, y)) {
				set(scanLeft - 1, y);
				scanLeft--;
			}
			checkDiagonals({ scanLeft, y }, left);
		}
		if (scanLeft < scanStart)
			seedStack.push(Seed { scanLeft, scanStart - 1, y - dy, -dy });

		int scanRight = scanStart;
		while (scanRight < scanEnd) {
			while (isInside(scanRight, y)) {
				set(scanRight, y);
				scanRight++;
			}
			seedStack.push(Seed { scanLeft, scanRight - 1, y + dy, dy });
			if (scanRight - 1 > scanEnd)
				seedStack.push(Seed { scanEnd + 1, scanRight - 1, y - dy, -dy });
			if (scanLeft < scanRight)
				checkDiagonals({ scanRight - 1, y }, right);

			while (scanRight < scanEnd && !isInside(scanRight, y))
				scanRight++;
			scanLeft = scanRight;
			if (scanLeft < scanEnd)
				checkDiagonals({ scanLeft, y }, left);
		}
	}
}

void InitGlobals()
{
	uint8_t defaultLight = levelType() == DTYPE_TOWN ? 0 : 15;
#ifdef _DEBUG
	if (CurrentLightManager.disableLighting_)
		defaultLight = 0;
#endif
	for (Tile &tile : tiles()) {
		tile.clear();
		tile.setLight(defaultLight);
	}

	DRLG_InitTrans();

	minimumDungeonPosition() = WorldTilePosition(0, 0).megaToWorld();
	maximumDungeonPosition() = WorldTilePosition(40, 40).megaToWorld();
	setPieceRoom() = { { 0, 0 }, { 0, 0 } };
	setPiece() = { { 0, 0 }, { 0, 0 } };
}

} // namespace

#ifdef BUILD_TESTING
std::optional<WorldTileSize> GetSizeForThemeRoom()
{
	return GetSizeForThemeRoom(0, { 0, 0 }, 5, 10);
}

#endif

dungeon_type GetLevelType(int level)
{
	if (level == 0)
		return DTYPE_TOWN;
	if (level <= 4)
		return DTYPE_CATHEDRAL;
	if (level <= 8)
		return DTYPE_CATACOMBS;
	if (level <= 12)
		return DTYPE_CAVES;
	if (level <= 16)
		return DTYPE_HELL;
	if (level <= 20)
		return DTYPE_NEST;
	if (level <= 24)
		return DTYPE_CRYPT;

	return DTYPE_NONE;
}

void CreateDungeon(uint32_t rseed, lvl_entry entry)
{
	// When loading from a save file, the level data arrays have already been
	// loaded from the save, so we should not zero them with memset.
	// However, we still need to initialize other state.
	if (entry != ENTRY_LOAD) {
		InitGlobals();
	} else {
		// For ENTRY_LOAD, just do the non-array initialization from InitGlobals
		DRLG_InitTrans();
		minimumDungeonPosition() = WorldTilePosition(0, 0).megaToWorld();
		maximumDungeonPosition() = WorldTilePosition(40, 40).megaToWorld();
		setPieceRoom() = { { 0, 0 }, { 0, 0 } };
		setPiece() = { { 0, 0 }, { 0, 0 } };
	}

	switch (levelType()) {
	case DTYPE_TOWN:
		CreateTown(entry);
		break;
	case DTYPE_CATHEDRAL:
	case DTYPE_CRYPT:
		CreateL5Dungeon(rseed, entry);
		break;
	case DTYPE_CATACOMBS:
		CreateL2Dungeon(rseed, entry);
		break;
	case DTYPE_CAVES:
	case DTYPE_NEST:
		CreateL3Dungeon(rseed, entry);
		break;
	case DTYPE_HELL:
		CreateL4Dungeon(rseed, entry);
		break;
	default:
		app_fatal("Invalid level type");
	}

	Make_SetPC(setPiece());
}

tl::expected<void, std::string> LoadLevelSOLData()
{
	switch (levelType()) {
	case DTYPE_TOWN:
		if (!LoadFileInMemWithStatus("nlevels\\towndata\\town.sol", tileProperties()).has_value()) {
			RETURN_IF_ERROR(LoadFileInMemWithStatus("levels\\towndata\\town.sol", tileProperties()));
		}
		break;
	case DTYPE_CATHEDRAL:
		RETURN_IF_ERROR(LoadFileInMemWithStatus("levels\\l1data\\l1.sol", tileProperties()));
		// Fix incorrectly marked arched tiles()
		tileProperties()[9] |= TileProperties::BlockLight | TileProperties::BlockMissile;
		tileProperties()[15] |= TileProperties::BlockLight | TileProperties::BlockMissile;
		tileProperties()[16] |= TileProperties::BlockLight | TileProperties::BlockMissile;
		tileProperties()[20] |= TileProperties::BlockLight | TileProperties::BlockMissile;
		tileProperties()[21] |= TileProperties::BlockLight | TileProperties::BlockMissile;
		tileProperties()[27] |= TileProperties::BlockMissile;
		tileProperties()[28] |= TileProperties::BlockMissile;
		tileProperties()[51] |= TileProperties::BlockLight | TileProperties::BlockMissile;
		tileProperties()[56] |= TileProperties::BlockLight | TileProperties::BlockMissile;
		tileProperties()[58] |= TileProperties::BlockLight | TileProperties::BlockMissile;
		tileProperties()[61] |= TileProperties::BlockLight | TileProperties::BlockMissile;
		tileProperties()[63] |= TileProperties::BlockLight | TileProperties::BlockMissile;
		tileProperties()[65] |= TileProperties::BlockLight | TileProperties::BlockMissile;
		tileProperties()[72] |= TileProperties::BlockLight | TileProperties::BlockMissile;
		tileProperties()[208] |= TileProperties::BlockLight | TileProperties::BlockMissile;
		tileProperties()[247] |= TileProperties::BlockLight | TileProperties::BlockMissile;
		tileProperties()[253] |= TileProperties::BlockLight | TileProperties::BlockMissile;
		tileProperties()[257] |= TileProperties::BlockLight | TileProperties::BlockMissile;
		tileProperties()[323] |= TileProperties::BlockLight | TileProperties::BlockMissile;
		tileProperties()[403] |= TileProperties::BlockLight;
		// Fix incorrectly marked pillar tile
		tileProperties()[24] |= TileProperties::BlockLight;
		// Fix incorrectly marked wall tile
		tileProperties()[450] |= TileProperties::BlockLight | TileProperties::BlockMissile;
		break;
	case DTYPE_CATACOMBS:
		RETURN_IF_ERROR(LoadFileInMemWithStatus("levels\\l2data\\l2.sol", tileProperties()));
		break;
	case DTYPE_CAVES:
		RETURN_IF_ERROR(LoadFileInMemWithStatus("levels\\l3data\\l3.sol", tileProperties()));
		// The graphics for tile 48 sub-tile 171 frame 461 are partly incorrect, as they
		// have a few pixels that should belong to the solid tile 49 instead.
		// Marks the sub-tile as "BlockMissile" to avoid treating it as a floor during rendering.
		tileProperties()[170] |= TileProperties::BlockMissile;
		// Fence sub-tiles() 481 and 487 are substitutes for solid sub-tiles() 473 and 479
		// but are not marked as solid.
		tileProperties()[481] |= TileProperties::Solid;
		tileProperties()[487] |= TileProperties::Solid;
		break;
	case DTYPE_HELL:
		RETURN_IF_ERROR(LoadFileInMemWithStatus("levels\\l4data\\l4.sol", tileProperties()));
		tileProperties()[210] = TileProperties::None; // Tile is incorrectly marked as being solid
		break;
	case DTYPE_NEST:
		RETURN_IF_ERROR(LoadFileInMemWithStatus("nlevels\\l6data\\l6.sol", tileProperties()));
		break;
	case DTYPE_CRYPT:
		RETURN_IF_ERROR(LoadFileInMemWithStatus("nlevels\\l5data\\l5.sol", tileProperties()));
		tileProperties()[142] = TileProperties::None; // Tile is incorrectly marked as being solid
		break;
	default:
		return tl::make_unexpected("LoadLevelSOLData");
	}
	return {};
}

void SetDungeonMicros(std::unique_ptr<std::byte[]> &dungeonCels, uint_fast8_t &microTileLen)
{
	microTileLen = 10;
	size_t blocks = 10;

	if (levelType() == DTYPE_TOWN) {
		microTileLen = 16;
		blocks = 16;
	} else if (levelType() == DTYPE_HELL) {
		microTileLen = 12;
		blocks = 16;
	}

	size_t tileCount;
	const std::unique_ptr<uint16_t[]> levelPieces = LoadMinData(tileCount);

	ankerl::unordered_dense::map<uint16_t, DunFrameInfo> frameToTypeMap;
	frameToTypeMap.reserve(4096);
	for (size_t levelPieceId = 0; levelPieceId < tileCount / blocks; levelPieceId++) {
		uint16_t *pieces = &levelPieces[blocks * levelPieceId];
		for (uint32_t block = 0; block < blocks; block++) {
			const LevelCelBlock levelCelBlock { Swap16LE(pieces[blocks - 2 + (block & 1) - (block & 0xE)]) };
			levelMicros()[levelPieceId].mt[block] = levelCelBlock;
			if (levelCelBlock.hasValue()) {
				if (const auto it = frameToTypeMap.find(levelCelBlock.frame()); it == frameToTypeMap.end()) {
					frameToTypeMap.emplace_hint(it, levelCelBlock.frame(),
					    DunFrameInfo { static_cast<uint8_t>(block), levelCelBlock.type(), tileProperties()[levelPieceId] });
				}
			}
		}
	}
	std::vector<std::pair<uint16_t, DunFrameInfo>> frameToTypeList = std::move(frameToTypeMap).extract();
	c_sort(frameToTypeList, [](const std::pair<uint16_t, DunFrameInfo> &a, const std::pair<uint16_t, DunFrameInfo> &b) {
		return a.first < b.first;
	});
	ReencodeDungeonCels(dungeonCels, frameToTypeList);

	std::vector<std::pair<uint16_t, uint16_t>> celBlockAdjustments = ComputeCelBlockAdjustments(frameToTypeList);
	if (celBlockAdjustments.size() == 0) return;
	for (size_t levelPieceId = 0; levelPieceId < tileCount / blocks; levelPieceId++) {
		for (uint32_t block = 0; block < blocks; block++) {
			LevelCelBlock &levelCelBlock = levelMicros()[levelPieceId].mt[block];
			const uint16_t frame = levelCelBlock.frame();
			const auto pair = std::make_pair(frame, frame);
			const auto it = std::upper_bound(celBlockAdjustments.begin(), celBlockAdjustments.end(), pair,
			    [](std::pair<uint16_t, uint16_t> p1, std::pair<uint16_t, uint16_t> p2) { return p1.first < p2.first; });
			if (it != celBlockAdjustments.end()) {
				levelCelBlock.data -= it->second;
			}
		}
	}
}

void DRLG_InitTrans()
{
	for (Tile &tile : tiles())
		tile.setTransVal(0);
	visibleTransparencyRegions() = {}; // TODO duplicate reset in CurrentLightManager.Init()
	nextTransparencyValue() = 1;
}

void FillCurrentMegaTiles(uint8_t value)
{
	for (auto &column : currentLevel().megaTiles()) {
		for (DungeonMegaTile &megaTile : column)
			megaTile.setCurrent(value);
	}
}

void SnapshotReplacementMegaTiles()
{
	for (auto &column : currentLevel().megaTiles()) {
		for (DungeonMegaTile &megaTile : column)
			megaTile.snapshotReplacement();
	}
}

void DRLG_RectTrans(WorldTileRectangle area)
{
	const WorldTilePosition position = area.position;
	const WorldTileSize size = area.size;

	for (int j = position.y; j <= position.y + size.height; j++) {
		for (int i = position.x; i <= position.x + size.width; i++) {
			tileAt(i, j).setTransVal(nextTransparencyValue());
		}
	}

	nextTransparencyValue()++;
}

void DRLG_MRectTrans(WorldTileRectangle area)
{
	DRLG_RectTrans({ area.position.megaToWorld() + WorldTileDisplacement { 1, 1 }, area.size * 2 - 1 });
}

void DRLG_MRectTrans(WorldTilePosition origin, WorldTilePosition extent)
{
	DRLG_MRectTrans({ origin, WorldTileSize(extent.x - origin.x, extent.y - origin.y) });
}

void DRLG_CopyTrans(int sx, int sy, int dx, int dy)
{
	tileAt(dx, dy).setTransVal(tileAt(sx, sy).transVal());
}

void LoadTransparency(const uint16_t *dunData)
{
	WorldTileSize size = GetDunSize(dunData);

	const int layer2Offset = 2 + (size.width * size.height);

	// The rest of the layers are at dPiece scale
	size *= static_cast<WorldTileCoord>(2);

	const uint16_t *transparentLayer = &dunData[layer2Offset + (size.width * size.height * 3)];

	for (WorldTileCoord j = 0; j < size.height; j++) {
		for (WorldTileCoord i = 0; i < size.width; i++) {
			tileAt(16 + i, 16 + j).setTransVal(static_cast<int8_t>(Swap16LE(*transparentLayer)));
			transparentLayer++;
		}
	}
}

void LoadDungeonBase(const char *path, Point spawn, int floorId, int dirtId)
{
	viewPosition() = spawn;

	InitGlobals();

	FillCurrentMegaTiles(dirtId);

	auto dunData = LoadFileInMem<uint16_t>(path);
	PlaceDunTiles(dunData.get(), { 0, 0 }, floorId);
	LoadTransparency(dunData.get());

	SetMapMonsters(dunData.get(), Point(0, 0).megaToWorld());
	LevelBestiary.initAllGraphics();
	SetMapObjects(dunData.get(), 0, 0);
}

void Make_SetPC(WorldTileRectangle area)
{
	const WorldTilePosition position = area.position.megaToWorld();
	const WorldTileSize size = area.size * 2;

	for (unsigned j = 0; j < size.height; j++) {
		for (unsigned i = 0; i < size.width; i++) {
			tileAt(position.x + i, position.y + j).addFlags(DungeonFlag::Populated);
		}
	}
}

std::optional<Point> PlaceMiniSet(const Miniset &miniset, int tries, bool drlg1Quirk)
{
	const int sw = miniset.size.width;
	const int sh = miniset.size.height;
	Point position { GenerateRnd(DMAXX - sw), GenerateRnd(DMAXY - sh) };

	for (int i = 0; i < tries; i++, position.x++) {
		if (position.x == DMAXX - sw) {
			position.x = 0;
			position.y++;
			if (position.y == DMAXY - sh) {
				position.y = 0;
			}
		}

		// Limit the position of SetPieces for compatibility with Diablo bug
		if (drlg1Quirk) {
			bool valid = true;
			if (position.x <= 12) {
				position.x++;
				valid = false;
			}
			if (position.y <= 12) {
				position.y++;
				valid = false;
			}
			if (!valid) {
				continue;
			}
		}

		if (setPieceRoom().contains(position))
			continue;
		if (!miniset.matches(position))
			continue;

		miniset.place(position);

		return position;
	}

	return {};
}

void PlaceDunTiles(const uint16_t *dunData, Point position, int floorId)
{
	const WorldTileSize size = GetDunSize(dunData);

	const uint16_t *tileLayer = &dunData[2];

	for (WorldTileCoord j = 0; j < size.height; j++) {
		for (WorldTileCoord i = 0; i < size.width; i++) {
			auto tileId = static_cast<uint8_t>(Swap16LE(tileLayer[(j * size.width) + i]));
			if (tileId != 0) {
				megaTileAt(position.x + i, position.y + j).setCurrent(tileId);
				protectedTiles().set(position.x + i, position.y + j);
			} else if (floorId != 0) {
				megaTileAt(position.x + i, position.y + j).setCurrent(floorId);
			}
		}
	}
}

void DRLG_PlaceThemeRooms(int minSize, int maxSize, int floor, int freq, bool rndSize)
{
	themeCount() = 0;
	memset(themeLocations(), 0, sizeof(*themeLocations()));
	for (WorldTileCoord j = 0; j < DMAXY; j++) {
		for (WorldTileCoord i = 0; i < DMAXX; i++) {
			if (megaTileAt(i, j).current() == floor && FlipCoin(freq)) {
				std::optional<WorldTileSize> themeSize = GetSizeForThemeRoom(floor, { i, j }, minSize, maxSize);

				if (!themeSize)
					continue;

				if (rndSize) {
					const int min = minSize - 2;
					const int max = maxSize - 2;
					themeSize->width = min + GenerateRnd(GenerateRnd(themeSize->width - min + 1));
					if (themeSize->width < min || themeSize->width > max)
						themeSize->width = min;
					themeSize->height = min + GenerateRnd(GenerateRnd(themeSize->height - min + 1));
					if (themeSize->height < min || themeSize->height > max)
						themeSize->height = min;
				}

				THEME_LOC &theme = themeLocations()[themeCount()];
				theme.room = { WorldTilePosition { i, j } + Direction::South, *themeSize };
				if (IsAnyOf(levelType(), DTYPE_CAVES, DTYPE_NEST)) {
					DRLG_RectTrans({ (theme.room.position + Direction::South).megaToWorld(), theme.room.size * 2 - 5 });
				} else {
					DRLG_MRectTrans({ theme.room.position, theme.room.size - 1 });
				}
				theme.ttval = nextTransparencyValue() - 1;
				CreateThemeRoom(themeCount());
				themeCount()++;
			}
		}
	}
} // namespace

void DRLG_HoldThemeRooms()
{
	for (int i = 0; i < themeCount(); i++) {
		for (int y = themeLocations()[i].room.position.y; y < themeLocations()[i].room.position.y + themeLocations()[i].room.size.height - 1; y++) {
			for (int x = themeLocations()[i].room.position.x; x < themeLocations()[i].room.position.x + themeLocations()[i].room.size.width - 1; x++) {
				const int xx = (2 * x) + 16;
				const int yy = (2 * y) + 16;
				tileAt(xx, yy).addFlags(DungeonFlag::Populated);
				tileAt(xx + 1, yy).addFlags(DungeonFlag::Populated);
				tileAt(xx, yy + 1).addFlags(DungeonFlag::Populated);
				tileAt(xx + 1, yy + 1).addFlags(DungeonFlag::Populated);
			}
		}
	}
}

WorldTileSize GetDunSize(const uint16_t *dunData)
{
	return WorldTileSize(static_cast<WorldTileCoord>(Swap16LE(dunData[0])), static_cast<WorldTileCoord>(Swap16LE(dunData[1])));
}

void DRLG_LPass3(int lv)
{
	{
		const MegaTile mega = megaTiles()[lv];
		const int v1 = Swap16LE(mega.micro1);
		const int v2 = Swap16LE(mega.micro2);
		const int v3 = Swap16LE(mega.micro3);
		const int v4 = Swap16LE(mega.micro4);

		for (int j = 0; j < MAXDUNY; j += 2) {
			for (int i = 0; i < MAXDUNX; i += 2) {
				tileAt(i + 0, j + 0).setPiece(v1);
				tileAt(i + 1, j + 0).setPiece(v2);
				tileAt(i + 0, j + 1).setPiece(v3);
				tileAt(i + 1, j + 1).setPiece(v4);
			}
		}
	}

	int yy = 16;
	for (int j = 0; j < DMAXY; j++) {
		int xx = 16;
		for (int i = 0; i < DMAXX; i++) { // NOLINT(modernize-loop-convert)
			const int tileId = megaTileAt(i, j).current() - 1;
			const MegaTile mega = megaTiles()[tileId];
			tileAt(xx + 0, yy + 0).setPiece(Swap16LE(mega.micro1));
			tileAt(xx + 1, yy + 0).setPiece(Swap16LE(mega.micro2));
			tileAt(xx + 0, yy + 1).setPiece(Swap16LE(mega.micro3));
			tileAt(xx + 1, yy + 1).setPiece(Swap16LE(mega.micro4));
			xx += 2;
		}
		yy += 2;
	}
}

bool IsNearThemeRoom(WorldTilePosition testPosition)
{
	for (int i = 0; i < themeCount(); i++) {
		if (WorldTileRectangle(themeLocations()[i].room.position - WorldTileDisplacement { 2 }, themeLocations()[i].room.size + 5).contains(testPosition))
			return true;
	}

	return false;
}

void InitLevels()
{
	SwitchCurrentLevel(LevelId { 0, DTYPE_TOWN, false, SL_NONE });
}

void FloodTransparencyValues(uint8_t floorID)
{
	int yy = 16;
	for (int j = 0; j < DMAXY; j++) {
		int xx = 16;
		for (int i = 0; i < DMAXX; i++) {
			if (megaTileAt(i, j).current() == floorID && tileAt(xx, yy).transVal() == 0) {
				FindTransparencyValues({ xx, yy }, floorID);
				nextTransparencyValue()++;
			}
			xx += 2;
		}
		yy += 2;
	}
}

tl::expected<dungeon_type, std::string> ParseDungeonType(std::string_view value)
{
	if (value.empty()) return DTYPE_NONE;
	if (value == "DTYPE_TOWN") return DTYPE_TOWN;
	if (value == "DTYPE_CATHEDRAL") return DTYPE_CATHEDRAL;
	if (value == "DTYPE_CATACOMBS") return DTYPE_CATACOMBS;
	if (value == "DTYPE_CAVES") return DTYPE_CAVES;
	if (value == "DTYPE_HELL") return DTYPE_HELL;
	if (value == "DTYPE_NEST") return DTYPE_NEST;
	if (value == "DTYPE_CRYPT") return DTYPE_CRYPT;
	return tl::make_unexpected("Unknown enum value");
}

tl::expected<_setlevels, std::string> ParseSetLevel(std::string_view value)
{
	const std::optional<_setlevels> enumValueOpt = magic_enum::enum_cast<_setlevels>(value);
	if (enumValueOpt.has_value()) {
		return enumValueOpt.value();
	}
	return tl::make_unexpected("Unknown enum value");
}

} // namespace devilution

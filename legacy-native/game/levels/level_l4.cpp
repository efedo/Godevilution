/**
 * @file game/levels/level_l4.cpp
 *
 * Implementation of the hell level generation algorithms.
 */
#include "game/levels/level_l4.h"

#include <cstdint>

#include "engine/load/load_file.hpp"
#include "engine/random.hpp"
#include "game/levels/dungeon_common.h"
#include "game/monsters/monsters.hpp"
#include "network/protocol/multi.h"
#include "game/players/players.hpp"
#include "tables/objdat.h"
#include "utils/is_of.hpp"

namespace devilution {

WorldTilePosition DiabloQuad1;
WorldTilePosition DiabloQuad2;
WorldTilePosition DiabloQuad3;
WorldTilePosition DiabloQuad4;

namespace {

bool hallok[20];
WorldTilePosition L4Hold;

/**
 * A lookup table for the 16 possible patterns of a 2x2 area,
 * where each cell either contains a SW wall or it doesn't.
 */
const uint8_t L4ConvTbl[16] = { 30, 6, 1, 6, 2, 6, 6, 6, 9, 6, 1, 6, 2, 6, 3, 6 };

/** Miniset: Stairs up. */
const Miniset L4USTAIRS {
	{ 4, 5 },
	{
	    { 6, 6, 6, 6 },
	    { 6, 6, 6, 6 },
	    { 6, 6, 6, 6 },
	    { 6, 6, 6, 6 },
	    { 6, 6, 6, 6 },
	},
	{
	    { 0, 0, 0, 0 },
	    { 36, 38, 35, 0 },
	    { 37, 34, 33, 32 },
	    { 0, 0, 31, 0 },
	    { 0, 0, 0, 0 },
	}
};
/** Miniset: Stairs up to town. */
const Miniset L4TWARP {
	{ 4, 5 },
	{
	    { 6, 6, 6, 6 },
	    { 6, 6, 6, 6 },
	    { 6, 6, 6, 6 },
	    { 6, 6, 6, 6 },
	    { 6, 6, 6, 6 },
	},
	{
	    { 0, 0, 0, 0 },
	    { 134, 136, 133, 0 },
	    { 135, 132, 131, 130 },
	    { 0, 0, 129, 0 },
	    { 0, 0, 0, 0 },
	}
};
/** Miniset: Stairs down. */
const Miniset L4DSTAIRS {
	{ 5, 5 },
	{
	    { 6, 6, 6, 6, 6 },
	    { 6, 6, 6, 6, 6 },
	    { 6, 6, 6, 6, 6 },
	    { 6, 6, 6, 6, 6 },
	    { 6, 6, 6, 6, 6 },
	},
	{
	    { 0, 0, 0, 0, 0 },
	    { 0, 0, 45, 41, 0 },
	    { 0, 44, 43, 40, 0 },
	    { 0, 46, 42, 39, 0 },
	    { 0, 0, 0, 0, 0 },
	}
};
/** Miniset: Pentagram. */
const Miniset L4PENTA {
	{ 5, 5 },
	{
	    { 6, 6, 6, 6, 6 },
	    { 6, 6, 6, 6, 6 },
	    { 6, 6, 6, 6, 6 },
	    { 6, 6, 6, 6, 6 },
	    { 6, 6, 6, 6, 6 },
	},
	{
	    { 0, 0, 0, 0, 0 },
	    { 0, 98, 100, 103, 0 },
	    { 0, 99, 102, 105, 0 },
	    { 0, 101, 104, 106, 0 },
	    { 0, 0, 0, 0, 0 },
	}
};
/** Miniset: Pentagram portal. */
const Miniset L4PENTA2 {
	{ 5, 5 },
	{
	    { 6, 6, 6, 6, 6 },
	    { 6, 6, 6, 6, 6 },
	    { 6, 6, 6, 6, 6 },
	    { 6, 6, 6, 6, 6 },
	    { 6, 6, 6, 6, 6 },
	},
	{
	    { 0, 0, 0, 0, 0 },
	    { 0, 107, 109, 112, 0 },
	    { 0, 108, 111, 114, 0 },
	    { 0, 110, 113, 115, 0 },
	    { 0, 0, 0, 0, 0 },
	}
};

/** Maps tile IDs to their corresponding undecorated tile ID. */
const uint8_t L4BTYPES[140] = {
	0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
	10, 11, 12, 13, 14, 15, 16, 17, 0, 0,
	0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
	0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
	0, 0, 0, 0, 0, 0, 0, 0, 0, 6,
	6, 6, 0, 0, 0, 0, 0, 0, 0, 0,
	0, 1, 2, 1, 2, 1, 2, 1, 1, 2,
	2, 0, 0, 0, 0, 0, 0, 15, 16, 9,
	12, 4, 5, 7, 0, 0, 0, 0, 0, 0,
	0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
	0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
	0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
	0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
	0, 0, 0, 0, 0, 0, 0, 0, 0, 0
};

void ApplyShadowsPatterns()
{
	for (int y = 1; y < DMAXY; y++) {
		for (int x = 1; x < DMAXY; x++) {
			if (IsNoneOf(megaTileAt(x, y).current(), 3, 4, 8, 15)) {
				continue;
			}
			if (megaTileAt(x - 1, y).current() == 6) {
				megaTileAt(x - 1, y).setCurrent(47);
			}
			if (megaTileAt(x - 1, y - 1).current() == 6) {
				megaTileAt(x - 1, y - 1).setCurrent(48);
			}
		}
	}
}

void InitSetPiece()
{
	std::unique_ptr<uint16_t[]> setPieceData;

	if (Quests[Q_WARLORD].IsAvailable()) {
		setPieceData = LoadFileInMem<uint16_t>("levels\\l4data\\warlord.dun");
	} else if (currentLevelNumber() == 15 && UseMultiplayerQuests()) {
		setPieceData = LoadFileInMem<uint16_t>("levels\\l4data\\vile1.dun");
	} else {
		return; // no setpiece needed for this level
	}

	const WorldTilePosition setPiecePosition = setPieceRoom().position;
	PlaceDunTiles(setPieceData.get(), setPiecePosition, 6);
	setPiece() = { setPiecePosition, GetDunSize(setPieceData.get()) };
}

void InitDungeonFlags()
{
	dungeonMask().reset();
	protectedTiles().reset();
	FillCurrentMegaTiles(30);
}

void MapRoom(WorldTileRectangle room)
{
	for (WorldTileCoord y = 0; y < room.size.height && y + room.position.y < DMAXY / 2; y++) {
		for (WorldTileCoord x = 0; x < room.size.width && x + room.position.x < DMAXX / 2; x++) {
			dungeonMask().set(room.position.x + x, room.position.y + y);
		}
	}
}

bool CheckRoom(WorldTileRectangle room)
{
	if (room.position.x <= 0 || room.position.y <= 0) {
		return false;
	}

	for (int y = 0; y < room.size.height; y++) {
		for (int x = 0; x < room.size.width; x++) {
			if (x + room.position.x < 0 || x + room.position.x >= DMAXX / 2 || y + room.position.y < 0 || y + room.position.y >= DMAXY / 2) {
				return false;
			}
			if (dungeonMask().test(room.position.x + x, room.position.y + y)) {
				return false;
			}
		}
	}

	return true;
}

void GenerateRoom(WorldTileRectangle area, bool verticalLayout)
{
	const bool rotate = !FlipCoin(4);
	verticalLayout = (!verticalLayout && rotate) || (verticalLayout && !rotate);

	bool placeRoom1;
	WorldTileRectangle room1;

	for (int num = 0; num < 20; num++) {
		const int32_t randomWidth = (GenerateRnd(5) + 2) & ~1;
		const int32_t randomHeight = (GenerateRnd(5) + 2) & ~1;
		room1.size = WorldTileSize(randomWidth, randomHeight);
		room1.position = area.position;
		if (verticalLayout) {
			room1.position += WorldTileDisplacement(-room1.size.width, (area.size.height / 2) - (room1.size.height / 2));
			placeRoom1 = CheckRoom({ room1.position + WorldTileDisplacement { -1, -1 }, WorldTileSize(room1.size.height + 2, room1.size.width + 1) }); /// BUGFIX: swap height and width ({ room1.size.width + 1, room1.size.height + 2 }) (workaround applied below)
		} else {
			room1.position += WorldTileDisplacement((area.size.width / 2) - (room1.size.width / 2), -room1.size.height);
			placeRoom1 = CheckRoom({ room1.position + WorldTileDisplacement { -1, -1 }, WorldTileSize(room1.size.width + 2, room1.size.height + 1) });
		}
		if (placeRoom1)
			break;
	}

	if (placeRoom1)
		MapRoom({ room1.position, WorldTileSize(std::min<int>(DMAXX - room1.position.x, room1.size.width), std::min<int>(DMAXX - room1.position.y, room1.size.height)) });

	bool placeRoom2;
	WorldTileRectangle room2 = room1;
	if (verticalLayout) {
		room2.position.x = area.position.x + area.size.width;
		placeRoom2 = CheckRoom({ room2.position + WorldTileDisplacement { 0, -1 }, WorldTileSize(room2.size.width + 1, room2.size.height + 2) });
	} else {
		room2.position.y = area.position.y + area.size.height;
		placeRoom2 = CheckRoom({ room2.position + WorldTileDisplacement { -1, 0 }, WorldTileSize(room2.size.width + 2, room2.size.height + 1) });
	}

	if (placeRoom2)
		MapRoom(room2);
	if (placeRoom1)
		GenerateRoom(room1, verticalLayout);
	if (placeRoom2)
		GenerateRoom(room2, verticalLayout);
}

void FirstRoom()
{
	WorldTileRectangle room { { 0, 0 }, { 14, 14 } };
	if (currentLevelNumber() != 16) {
		if (currentLevelNumber() == Quests[Q_WARLORD]._qlevel && Quests[Q_WARLORD]._qactive != QUEST_NOTAVAIL) {
			room.size = { 11, 11 };
		} else if (currentLevelNumber() == Quests[Q_BETRAYER]._qlevel && UseMultiplayerQuests()) {
			room.size = { 11, 11 };
		} else {
			const int32_t randomWidth = GenerateRnd(5) + 2;
			const int32_t randomHeight = GenerateRnd(5) + 2;
			room.size = WorldTileSize(randomWidth, randomHeight);
		}
	}

	const int xmin = (DMAXX / 2 - room.size.width) / 2;
	const int xmax = (DMAXX / 2) - 1 - room.size.width;
	const int ymin = (DMAXY / 2 - room.size.height) / 2;
	const int ymax = (DMAXY / 2) - 1 - room.size.height;
	const int32_t randomX = GenerateRnd(xmax - xmin + 1) + xmin;
	const int32_t randomY = GenerateRnd(ymax - ymin + 1) + ymin;
	room.position = WorldTilePosition(randomX, randomY);

	if (currentLevelNumber() == 16) {
		L4Hold = room.position;
	}
	if (Quests[Q_WARLORD].IsAvailable() || (currentLevelNumber() == Quests[Q_BETRAYER]._qlevel && UseMultiplayerQuests())) {
		setPieceRoom() = { room.position + WorldTileDisplacement { 1, 1 }, WorldTileSize(room.size.width + 1, room.size.height + 1) };
	} else {
		setPieceRoom() = {};
	}

	MapRoom(room);
	GenerateRoom(room, !FlipCoin());
}

/**
 * @brief Mirrors the first quadrant to the rest of the map
 */
void MirrorDungeonLayout()
{
	for (int y = 0; y < DMAXY / 2; y++) {
		for (int x = 0; x < DMAXX / 2; x++) {
			if (dungeonMask().test(x, y)) {
				dungeonMask().set(x, DMAXY - 1 - y);
				dungeonMask().set(DMAXX - 1 - x, y);
				dungeonMask().set(DMAXX - 1 - x, DMAXY - 1 - y);
			}
		}
	}
}

void MakeDmt()
{
	for (int y = 0; y < DMAXY - 1; y++) {
		for (int x = 0; x < DMAXX - 1; x++) {
			const int val = (dungeonMask().test(x + 1, y + 1) << 3) | (dungeonMask().test(x, y + 1) << 2) | (dungeonMask().test(x + 1, y) << 1) | (dungeonMask().test(x, y) << 0);
			megaTileAt(x, y).setCurrent(L4ConvTbl[val]);
		}
	}
}

int HorizontalWallOk(int i, int j)
{
	int x;
	for (x = 1; megaTileAt(i + x, j).current() == 6; x++) {
		if (protectedTiles().test(i + x, j)) {
			break;
		}
		if (megaTileAt(i + x, j - 1).current() != 6) {
			break;
		}
		if (megaTileAt(i + x, j + 1).current() != 6) {
			break;
		}
	}

	if (IsAnyOf(megaTileAt(i + x, j).current(), 10, 12, 13, 15, 16, 21, 22) && x > 3)
		return x;

	return -1;
}

int VerticalWallOk(int i, int j)
{
	int y;
	for (y = 1; megaTileAt(i, j + y).current() == 6; y++) {
		if (protectedTiles().test(i, j + y)) {
			break;
		}
		if (megaTileAt(i - 1, j + y).current() != 6) {
			break;
		}
		if (megaTileAt(i + 1, j + y).current() != 6) {
			break;
		}
	}

	if (IsAnyOf(megaTileAt(i, j + y).current(), 8, 9, 11, 14, 15, 16, 21, 23) && y > 3)
		return y;

	return -1;
}

void HorizontalWall(int i, int j, int dx)
{
	if (megaTileAt(i, j).current() == 13) {
		megaTileAt(i, j).setCurrent(17);
	}
	if (megaTileAt(i, j).current() == 16) {
		megaTileAt(i, j).setCurrent(11);
	}
	if (megaTileAt(i, j).current() == 12) {
		megaTileAt(i, j).setCurrent(14);
	}

	for (int xx = 1; xx < dx; xx++) {
		megaTileAt(i + xx, j).setCurrent(2);
	}

	if (megaTileAt(i + dx, j).current() == 15) {
		megaTileAt(i + dx, j).setCurrent(14);
	}
	if (megaTileAt(i + dx, j).current() == 10) {
		megaTileAt(i + dx, j).setCurrent(17);
	}
	if (megaTileAt(i + dx, j).current() == 21) {
		megaTileAt(i + dx, j).setCurrent(23);
	}
	if (megaTileAt(i + dx, j).current() == 22) {
		megaTileAt(i + dx, j).setCurrent(29);
	}

	const int xx = GenerateRnd(dx - 3) + 1;
	megaTileAt(i + xx, j).setCurrent(57);
	megaTileAt(i + xx + 2, j).setCurrent(56);
	megaTileAt(i + xx + 1, j).setCurrent(60);

	if (megaTileAt(i + xx, j - 1).current() == 6) {
		megaTileAt(i + xx, j - 1).setCurrent(58);
	}
	if (megaTileAt(i + xx + 1, j - 1).current() == 6) {
		megaTileAt(i + xx + 1, j - 1).setCurrent(59);
	}
}

void VerticalWall(int i, int j, int dy)
{
	if (megaTileAt(i, j).current() == 14) {
		megaTileAt(i, j).setCurrent(17);
	}
	if (megaTileAt(i, j).current() == 8) {
		megaTileAt(i, j).setCurrent(9);
	}
	if (megaTileAt(i, j).current() == 15) {
		megaTileAt(i, j).setCurrent(10);
	}

	for (int yy = 1; yy < dy; yy++) {
		megaTileAt(i, j + yy).setCurrent(1);
	}

	if (megaTileAt(i, j + dy).current() == 11) {
		megaTileAt(i, j + dy).setCurrent(17);
	}
	if (megaTileAt(i, j + dy).current() == 9) {
		megaTileAt(i, j + dy).setCurrent(10);
	}
	if (megaTileAt(i, j + dy).current() == 16) {
		megaTileAt(i, j + dy).setCurrent(13);
	}
	if (megaTileAt(i, j + dy).current() == 21) {
		megaTileAt(i, j + dy).setCurrent(22);
	}
	if (megaTileAt(i, j + dy).current() == 23) {
		megaTileAt(i, j + dy).setCurrent(29);
	}

	const int yy = GenerateRnd(dy - 3) + 1;
	megaTileAt(i, j + yy).setCurrent(53);
	megaTileAt(i, j + yy + 2).setCurrent(52);
	megaTileAt(i, j + yy + 1).setCurrent(6);

	if (megaTileAt(i - 1, j + yy).current() == 6) {
		megaTileAt(i - 1, j + yy).setCurrent(54);
	}
	if (megaTileAt(i - 1, j + yy - 1).current() == 6) {
		megaTileAt(i - 1, j + yy - 1).setCurrent(55);
	}
}

void AddWall()
{
	for (int j = 0; j < DMAXY; j++) {
		for (int i = 0; i < DMAXX; i++) {
			if (protectedTiles().test(i, j)) {
				continue;
			}
			for (auto d : { 10, 12, 13, 15, 16, 21, 22 }) {
				if (d == megaTileAt(i, j).current()) {
					DiscardRandomValues(1);
					const int x = HorizontalWallOk(i, j);
					if (x != -1) {
						HorizontalWall(i, j, x);
					}
				}
			}
			for (auto d : { 8, 9, 11, 14, 15, 16, 21, 23 }) {
				if (d == megaTileAt(i, j).current()) {
					DiscardRandomValues(1);
					const int y = VerticalWallOk(i, j);
					if (y != -1) {
						VerticalWall(i, j, y);
					}
				}
			}
		}
	}
}

void FixTilesPatterns()
{
	for (int j = 0; j < DMAXY; j++) {
		for (int i = 0; i < DMAXX; i++) {
			if (megaTileAt(i, j).current() == 2 && megaTileAt(i + 1, j).current() == 6)
				megaTileAt(i + 1, j).setCurrent(5);
			if (megaTileAt(i, j).current() == 2 && megaTileAt(i + 1, j).current() == 1)
				megaTileAt(i + 1, j).setCurrent(13);
			if (megaTileAt(i, j).current() == 1 && megaTileAt(i, j + 1).current() == 2)
				megaTileAt(i, j + 1).setCurrent(14);
		}
	}

	for (int j = 0; j < DMAXY; j++) {
		for (int i = 0; i < DMAXX; i++) {
			if (megaTileAt(i, j).current() == 2 && megaTileAt(i + 1, j).current() == 6)
				megaTileAt(i + 1, j).setCurrent(2);
			if (megaTileAt(i, j).current() == 2 && megaTileAt(i + 1, j).current() == 9)
				megaTileAt(i + 1, j).setCurrent(11);
			if (megaTileAt(i, j).current() == 9 && megaTileAt(i + 1, j).current() == 6)
				megaTileAt(i + 1, j).setCurrent(12);
			if (megaTileAt(i, j).current() == 14 && megaTileAt(i + 1, j).current() == 1)
				megaTileAt(i + 1, j).setCurrent(13);
			if (megaTileAt(i, j).current() == 6 && megaTileAt(i + 1, j).current() == 14)
				megaTileAt(i + 1, j).setCurrent(15);
			if (megaTileAt(i, j).current() == 6 && megaTileAt(i, j + 1).current() == 13)
				megaTileAt(i, j + 1).setCurrent(16);
			if (megaTileAt(i, j).current() == 1 && megaTileAt(i, j + 1).current() == 9)
				megaTileAt(i, j + 1).setCurrent(10);
			if (megaTileAt(i, j).current() == 6 && megaTileAt(i, j - 1).current() == 1)
				megaTileAt(i, j - 1).setCurrent(1);
		}
	}

	for (int j = 0; j < DMAXY; j++) {
		for (int i = 0; i < DMAXX; i++) {
			if (megaTileAt(i, j).current() == 13 && megaTileAt(i, j + 1).current() == 30)
				megaTileAt(i, j + 1).setCurrent(27);
			if (megaTileAt(i, j).current() == 27 && megaTileAt(i + 1, j).current() == 30)
				megaTileAt(i + 1, j).setCurrent(19);
			if (megaTileAt(i, j).current() == 1 && megaTileAt(i, j + 1).current() == 30)
				megaTileAt(i, j + 1).setCurrent(27);
			if (megaTileAt(i, j).current() == 27 && megaTileAt(i + 1, j).current() == 1)
				megaTileAt(i + 1, j).setCurrent(16);
			if (megaTileAt(i, j).current() == 19 && megaTileAt(i + 1, j).current() == 27)
				megaTileAt(i + 1, j).setCurrent(26);
			if (megaTileAt(i, j).current() == 27 && megaTileAt(i + 1, j).current() == 30)
				megaTileAt(i + 1, j).setCurrent(19);
			if (megaTileAt(i, j).current() == 2 && megaTileAt(i + 1, j).current() == 15)
				megaTileAt(i + 1, j).setCurrent(14);
			if (megaTileAt(i, j).current() == 14 && megaTileAt(i + 1, j).current() == 15)
				megaTileAt(i + 1, j).setCurrent(14);
			if (megaTileAt(i, j).current() == 22 && megaTileAt(i + 1, j).current() == 1)
				megaTileAt(i + 1, j).setCurrent(16);
			if (megaTileAt(i, j).current() == 27 && megaTileAt(i + 1, j).current() == 1)
				megaTileAt(i + 1, j).setCurrent(16);
			if (megaTileAt(i, j).current() == 6 && megaTileAt(i + 1, j).current() == 27 && megaTileAt(i + 1, j + 1).current() != 0) /* check */
				megaTileAt(i + 1, j).setCurrent(22);
			if (megaTileAt(i, j).current() == 22 && megaTileAt(i + 1, j).current() == 30)
				megaTileAt(i + 1, j).setCurrent(19);
			if (megaTileAt(i, j).current() == 21 && megaTileAt(i + 1, j).current() == 1 && megaTileAt(i + 1, j - 1).current() == 1)
				megaTileAt(i + 1, j).setCurrent(13);
			if (megaTileAt(i, j).current() == 14 && megaTileAt(i + 1, j).current() == 30 && megaTileAt(i, j + 1).current() == 6)
				megaTileAt(i + 1, j).setCurrent(28);
			if (megaTileAt(i, j).current() == 16 && megaTileAt(i + 1, j).current() == 6 && megaTileAt(i, j + 1).current() == 30)
				megaTileAt(i, j + 1).setCurrent(27);
			if (megaTileAt(i, j).current() == 16 && megaTileAt(i, j + 1).current() == 30 && megaTileAt(i + 1, j + 1).current() == 30)
				megaTileAt(i, j + 1).setCurrent(27);
			if (megaTileAt(i, j).current() == 6 && megaTileAt(i + 1, j).current() == 30 && megaTileAt(i + 1, j - 1).current() == 6)
				megaTileAt(i + 1, j).setCurrent(21);
			if (megaTileAt(i, j).current() == 2 && megaTileAt(i + 1, j).current() == 27 && megaTileAt(i + 1, j + 1).current() == 9)
				megaTileAt(i + 1, j).setCurrent(29);
			if (megaTileAt(i, j).current() == 9 && megaTileAt(i + 1, j).current() == 15)
				megaTileAt(i + 1, j).setCurrent(14);
			if (megaTileAt(i, j).current() == 15 && megaTileAt(i + 1, j).current() == 27 && megaTileAt(i + 1, j + 1).current() == 2)
				megaTileAt(i + 1, j).setCurrent(29);
			if (megaTileAt(i, j).current() == 19 && megaTileAt(i + 1, j).current() == 18)
				megaTileAt(i + 1, j).setCurrent(24);
			if (megaTileAt(i, j).current() == 9 && megaTileAt(i + 1, j).current() == 15)
				megaTileAt(i + 1, j).setCurrent(14);
			if (megaTileAt(i, j).current() == 19 && megaTileAt(i + 1, j).current() == 19 && megaTileAt(i + 1, j - 1).current() == 30)
				megaTileAt(i + 1, j).setCurrent(24);
			if (megaTileAt(i, j).current() == 24 && megaTileAt(i, j - 1).current() == 30 && megaTileAt(i, j - 2).current() == 6)
				megaTileAt(i, j - 1).setCurrent(21);
			if (megaTileAt(i, j).current() == 2 && megaTileAt(i + 1, j).current() == 30)
				megaTileAt(i + 1, j).setCurrent(28);
			if (megaTileAt(i, j).current() == 15 && megaTileAt(i + 1, j).current() == 30)
				megaTileAt(i + 1, j).setCurrent(28);
			if (megaTileAt(i, j).current() == 28 && megaTileAt(i, j + 1).current() == 30)
				megaTileAt(i, j + 1).setCurrent(18);
			if (megaTileAt(i, j).current() == 28 && megaTileAt(i, j + 1).current() == 2)
				megaTileAt(i, j + 1).setCurrent(15);
			if (megaTileAt(i, j).current() == 19 && megaTileAt(i + 2, j).current() == 2 && megaTileAt(i + 1, j - 1).current() == 18 && megaTileAt(i + 1, j + 1).current() == 1)
				megaTileAt(i + 1, j).setCurrent(17);
			if (megaTileAt(i, j).current() == 19 && megaTileAt(i + 2, j).current() == 2 && megaTileAt(i + 1, j - 1).current() == 22 && megaTileAt(i + 1, j + 1).current() == 1)
				megaTileAt(i + 1, j).setCurrent(17);
			if (megaTileAt(i, j).current() == 19 && megaTileAt(i + 2, j).current() == 2 && megaTileAt(i + 1, j - 1).current() == 18 && megaTileAt(i + 1, j + 1).current() == 13)
				megaTileAt(i + 1, j).setCurrent(17);
			if (megaTileAt(i, j).current() == 21 && megaTileAt(i + 2, j).current() == 2 && megaTileAt(i + 1, j - 1).current() == 18 && megaTileAt(i + 1, j + 1).current() == 1)
				megaTileAt(i + 1, j).setCurrent(17);
			if (megaTileAt(i, j).current() == 21 && megaTileAt(i + 1, j + 1).current() == 1 && megaTileAt(i + 1, j - 1).current() == 22 && megaTileAt(i + 2, j).current() == 3)
				megaTileAt(i + 1, j).setCurrent(17);
			if (megaTileAt(i, j).current() == 15 && megaTileAt(i + 1, j).current() == 28 && megaTileAt(i + 2, j).current() == 30 && megaTileAt(i + 1, j - 1).current() == 6)
				megaTileAt(i + 1, j).setCurrent(23);
			if (megaTileAt(i, j).current() == 14 && megaTileAt(i + 1, j).current() == 28 && megaTileAt(i + 2, j).current() == 1)
				megaTileAt(i + 1, j).setCurrent(23);
			if (megaTileAt(i, j).current() == 15 && megaTileAt(i + 1, j).current() == 27 && megaTileAt(i + 1, j + 1).current() == 30)
				megaTileAt(i + 1, j).setCurrent(29);
			if (megaTileAt(i, j).current() == 28 && megaTileAt(i, j + 1).current() == 9)
				megaTileAt(i, j + 1).setCurrent(15);
			if (megaTileAt(i, j).current() == 21 && megaTileAt(i + 1, j - 1).current() == 21)
				megaTileAt(i + 1, j).setCurrent(24);
			if (megaTileAt(i, j).current() == 2 && megaTileAt(i + 1, j).current() == 27 && megaTileAt(i + 1, j + 1).current() == 30)
				megaTileAt(i + 1, j).setCurrent(29);
			if (megaTileAt(i, j).current() == 2 && megaTileAt(i + 1, j).current() == 18)
				megaTileAt(i + 1, j).setCurrent(25);
			if (megaTileAt(i, j).current() == 21 && megaTileAt(i + 1, j).current() == 9 && megaTileAt(i + 2, j).current() == 2)
				megaTileAt(i + 1, j).setCurrent(11);
			if (megaTileAt(i, j).current() == 19 && megaTileAt(i + 1, j).current() == 10)
				megaTileAt(i + 1, j).setCurrent(17);
			if (megaTileAt(i, j).current() == 15 && megaTileAt(i, j + 1).current() == 3)
				megaTileAt(i, j + 1).setCurrent(4);
			if (megaTileAt(i, j).current() == 22 && megaTileAt(i, j + 1).current() == 9)
				megaTileAt(i, j + 1).setCurrent(15);
			if (megaTileAt(i, j).current() == 18 && megaTileAt(i, j + 1).current() == 30)
				megaTileAt(i, j + 1).setCurrent(18);
			if (megaTileAt(i, j).current() == 24 && megaTileAt(i - 1, j).current() == 30)
				megaTileAt(i - 1, j).setCurrent(19);
			if (megaTileAt(i, j).current() == 21 && megaTileAt(i, j + 1).current() == 2)
				megaTileAt(i, j + 1).setCurrent(15);
			if (megaTileAt(i, j).current() == 21 && megaTileAt(i, j + 1).current() == 9)
				megaTileAt(i, j + 1).setCurrent(10);
			if (megaTileAt(i, j).current() == 22 && megaTileAt(i, j + 1).current() == 30)
				megaTileAt(i, j + 1).setCurrent(18);
			if (megaTileAt(i, j).current() == 21 && megaTileAt(i, j + 1).current() == 30)
				megaTileAt(i, j + 1).setCurrent(18);
			if (megaTileAt(i, j).current() == 16 && megaTileAt(i, j + 1).current() == 2)
				megaTileAt(i, j + 1).setCurrent(15);
			if (megaTileAt(i, j).current() == 13 && megaTileAt(i, j + 1).current() == 2)
				megaTileAt(i, j + 1).setCurrent(15);
			if (megaTileAt(i, j).current() == 22 && megaTileAt(i, j + 1).current() == 2)
				megaTileAt(i, j + 1).setCurrent(15);
			if (megaTileAt(i, j).current() == 21 && megaTileAt(i + 1, j).current() == 18 && megaTileAt(i + 2, j).current() == 30)
				megaTileAt(i + 1, j).setCurrent(24);
			if (megaTileAt(i, j).current() == 21 && megaTileAt(i + 1, j).current() == 9 && megaTileAt(i + 1, j + 1).current() == 1)
				megaTileAt(i + 1, j).setCurrent(16);
			if (megaTileAt(i, j).current() == 2 && megaTileAt(i + 1, j).current() == 27 && megaTileAt(i + 1, j + 1).current() == 2)
				megaTileAt(i + 1, j).setCurrent(29);
			if (megaTileAt(i, j).current() == 23 && megaTileAt(i, j + 1).current() == 2)
				megaTileAt(i, j + 1).setCurrent(15);
			if (megaTileAt(i, j).current() == 23 && megaTileAt(i, j + 1).current() == 9)
				megaTileAt(i, j + 1).setCurrent(15);
			if (megaTileAt(i, j).current() == 25 && megaTileAt(i, j + 1).current() == 2)
				megaTileAt(i, j + 1).setCurrent(15);
			if (megaTileAt(i, j).current() == 22 && megaTileAt(i + 1, j).current() == 9)
				megaTileAt(i + 1, j).setCurrent(11);
			if (megaTileAt(i, j).current() == 23 && megaTileAt(i + 1, j).current() == 9)
				megaTileAt(i + 1, j).setCurrent(11);
			if (megaTileAt(i, j).current() == 15 && megaTileAt(i + 1, j).current() == 1)
				megaTileAt(i + 1, j).setCurrent(16);
			if (megaTileAt(i, j).current() == 11 && megaTileAt(i + 1, j).current() == 15)
				megaTileAt(i + 1, j).setCurrent(14);
			if (megaTileAt(i, j).current() == 23 && megaTileAt(i + 1, j).current() == 1)
				megaTileAt(i + 1, j).setCurrent(16);
			if (megaTileAt(i, j).current() == 21 && megaTileAt(i + 1, j).current() == 27)
				megaTileAt(i + 1, j).setCurrent(26);
			if (megaTileAt(i, j).current() == 21 && megaTileAt(i + 1, j).current() == 18)
				megaTileAt(i + 1, j).setCurrent(24);
			if (megaTileAt(i, j).current() == 26 && megaTileAt(i + 1, j).current() == 1)
				megaTileAt(i + 1, j).setCurrent(16);
			if (megaTileAt(i, j).current() == 29 && megaTileAt(i + 1, j).current() == 1)
				megaTileAt(i + 1, j).setCurrent(16);
			if (megaTileAt(i, j).current() == 29 && megaTileAt(i, j + 1).current() == 2)
				megaTileAt(i, j + 1).setCurrent(15);
			if (megaTileAt(i, j).current() == 1 && megaTileAt(i, j - 1).current() == 15)
				megaTileAt(i, j - 1).setCurrent(10);
			if (megaTileAt(i, j).current() == 18 && megaTileAt(i, j + 1).current() == 2)
				megaTileAt(i, j + 1).setCurrent(15);
			if (megaTileAt(i, j).current() == 23 && megaTileAt(i, j + 1).current() == 30)
				megaTileAt(i, j + 1).setCurrent(18);
			if (megaTileAt(i, j).current() == 18 && megaTileAt(i, j + 1).current() == 9)
				megaTileAt(i, j + 1).setCurrent(10);
			if (megaTileAt(i, j).current() == 14 && megaTileAt(i + 1, j).current() == 30 && megaTileAt(i + 1, j + 1).current() == 30)
				megaTileAt(i + 1, j).setCurrent(23);
			if (megaTileAt(i, j).current() == 2 && megaTileAt(i + 1, j).current() == 28 && megaTileAt(i + 1, j - 1).current() == 6)
				megaTileAt(i + 1, j).setCurrent(23);
			if (megaTileAt(i, j).current() == 23 && megaTileAt(i + 1, j).current() == 18 && megaTileAt(i, j - 1).current() == 6)
				megaTileAt(i + 1, j).setCurrent(24);
			if (megaTileAt(i, j).current() == 14 && megaTileAt(i + 1, j).current() == 23 && megaTileAt(i + 2, j).current() == 30)
				megaTileAt(i + 1, j).setCurrent(28);
			if (megaTileAt(i, j).current() == 14 && megaTileAt(i + 1, j).current() == 28 && megaTileAt(i + 2, j).current() == 30 && megaTileAt(i + 1, j - 1).current() == 6)
				megaTileAt(i + 1, j).setCurrent(23);
			if (megaTileAt(i, j).current() == 23 && megaTileAt(i + 1, j).current() == 30)
				megaTileAt(i + 1, j).setCurrent(19);
			if (megaTileAt(i, j).current() == 29 && megaTileAt(i + 1, j).current() == 30)
				megaTileAt(i + 1, j).setCurrent(19);
			if (megaTileAt(i, j).current() == 29 && megaTileAt(i, j + 1).current() == 30)
				megaTileAt(i, j + 1).setCurrent(18);
			if (megaTileAt(i, j).current() == 19 && megaTileAt(i + 1, j).current() == 30)
				megaTileAt(i + 1, j).setCurrent(19);
			if (megaTileAt(i, j).current() == 21 && megaTileAt(i + 1, j).current() == 30)
				megaTileAt(i + 1, j).setCurrent(19);
			if (megaTileAt(i, j).current() == 26 && megaTileAt(i + 1, j).current() == 30)
				megaTileAt(i + 1, j).setCurrent(19);
			if (megaTileAt(i, j).current() == 16 && megaTileAt(i, j + 1).current() == 30)
				megaTileAt(i, j + 1).setCurrent(18);
			if (megaTileAt(i, j).current() == 13 && megaTileAt(i, j + 1).current() == 9)
				megaTileAt(i, j + 1).setCurrent(10);
			if (megaTileAt(i, j).current() == 25 && megaTileAt(i, j + 1).current() == 30)
				megaTileAt(i, j + 1).setCurrent(18);
			if (megaTileAt(i, j).current() == 18 && megaTileAt(i, j + 1).current() == 2)
				megaTileAt(i, j + 1).setCurrent(15);
			if (megaTileAt(i, j).current() == 11 && megaTileAt(i + 1, j).current() == 3)
				megaTileAt(i + 1, j).setCurrent(5);
			if (megaTileAt(i, j).current() == 19 && megaTileAt(i + 1, j).current() == 9)
				megaTileAt(i + 1, j).setCurrent(11);
			if (megaTileAt(i, j).current() == 19 && megaTileAt(i + 1, j).current() == 1)
				megaTileAt(i + 1, j).setCurrent(13);
			if (megaTileAt(i, j).current() == 19 && megaTileAt(i + 1, j).current() == 13 && megaTileAt(i + 1, j - 1).current() == 6)
				megaTileAt(i + 1, j).setCurrent(16);
		}
	}

	for (int j = 0; j < DMAXY; j++) {
		for (int i = 0; i < DMAXX; i++) {
			if (megaTileAt(i, j).current() == 21 && megaTileAt(i, j + 1).current() == 24 && megaTileAt(i, j + 2).current() == 1)
				megaTileAt(i, j + 1).setCurrent(17);
			if (megaTileAt(i, j).current() == 15 && megaTileAt(i + 1, j + 1).current() == 9 && megaTileAt(i + 1, j - 1).current() == 1 && megaTileAt(i + 2, j).current() == 16)
				megaTileAt(i + 1, j).setCurrent(29);
			if (megaTileAt(i, j).current() == 2 && megaTileAt(i - 1, j).current() == 6)
				megaTileAt(i - 1, j).setCurrent(8);
			if (megaTileAt(i, j).current() == 1 && megaTileAt(i, j - 1).current() == 6)
				megaTileAt(i, j - 1).setCurrent(7);
			if (megaTileAt(i, j).current() == 6 && megaTileAt(i + 1, j).current() == 15 && megaTileAt(i + 1, j + 1).current() == 4)
				megaTileAt(i + 1, j).setCurrent(10);
			if (megaTileAt(i, j).current() == 1 && megaTileAt(i, j + 1).current() == 3)
				megaTileAt(i, j + 1).setCurrent(4);
			if (megaTileAt(i, j).current() == 1 && megaTileAt(i, j + 1).current() == 6)
				megaTileAt(i, j + 1).setCurrent(4);
			if (megaTileAt(i, j).current() == 9 && megaTileAt(i, j + 1).current() == 3)
				megaTileAt(i, j + 1).setCurrent(4);
			if (megaTileAt(i, j).current() == 10 && megaTileAt(i, j + 1).current() == 3)
				megaTileAt(i, j + 1).setCurrent(4);
			if (megaTileAt(i, j).current() == 13 && megaTileAt(i, j + 1).current() == 3)
				megaTileAt(i, j + 1).setCurrent(4);
			if (megaTileAt(i, j).current() == 1 && megaTileAt(i, j + 1).current() == 5)
				megaTileAt(i, j + 1).setCurrent(12);
			if (megaTileAt(i, j).current() == 1 && megaTileAt(i, j + 1).current() == 16)
				megaTileAt(i, j + 1).setCurrent(13);
			if (megaTileAt(i, j).current() == 6 && megaTileAt(i, j + 1).current() == 13)
				megaTileAt(i, j + 1).setCurrent(16);
			if (megaTileAt(i, j).current() == 25 && megaTileAt(i, j + 1).current() == 9)
				megaTileAt(i, j + 1).setCurrent(10);
			if (megaTileAt(i, j).current() == 13 && megaTileAt(i, j + 1).current() == 5)
				megaTileAt(i, j + 1).setCurrent(12);
			if (megaTileAt(i, j).current() == 28 && megaTileAt(i, j - 1).current() == 6 && megaTileAt(i + 1, j).current() == 1)
				megaTileAt(i + 1, j).setCurrent(23);
			if (megaTileAt(i, j).current() == 19 && megaTileAt(i + 1, j).current() == 10)
				megaTileAt(i + 1, j).setCurrent(17);
			if (megaTileAt(i, j).current() == 21 && megaTileAt(i + 1, j).current() == 9)
				megaTileAt(i + 1, j).setCurrent(11);
			if (megaTileAt(i, j).current() == 11 && megaTileAt(i + 1, j).current() == 3)
				megaTileAt(i + 1, j).setCurrent(5);
			if (megaTileAt(i, j).current() == 10 && megaTileAt(i + 1, j).current() == 4)
				megaTileAt(i + 1, j).setCurrent(12);
			if (megaTileAt(i, j).current() == 14 && megaTileAt(i + 1, j).current() == 4)
				megaTileAt(i + 1, j).setCurrent(12);
			if (megaTileAt(i, j).current() == 27 && megaTileAt(i + 1, j).current() == 9)
				megaTileAt(i + 1, j).setCurrent(11);
			if (megaTileAt(i, j).current() == 15 && megaTileAt(i + 1, j).current() == 4)
				megaTileAt(i + 1, j).setCurrent(12);
			if (megaTileAt(i, j).current() == 21 && megaTileAt(i + 1, j).current() == 1)
				megaTileAt(i + 1, j).setCurrent(16);
			if (megaTileAt(i, j).current() == 11 && megaTileAt(i + 1, j).current() == 4)
				megaTileAt(i + 1, j).setCurrent(12);
			if (megaTileAt(i, j).current() == 2 && megaTileAt(i + 1, j).current() == 3)
				megaTileAt(i + 1, j).setCurrent(5);
			if (megaTileAt(i, j).current() == 9 && megaTileAt(i + 1, j).current() == 3)
				megaTileAt(i + 1, j).setCurrent(5);
			if (megaTileAt(i, j).current() == 14 && megaTileAt(i + 1, j).current() == 3)
				megaTileAt(i + 1, j).setCurrent(5);
			if (megaTileAt(i, j).current() == 15 && megaTileAt(i + 1, j).current() == 3)
				megaTileAt(i + 1, j).setCurrent(5);
			if (megaTileAt(i, j).current() == 2 && megaTileAt(i + 1, j).current() == 5 && megaTileAt(i + 1, j - 1).current() == 16)
				megaTileAt(i + 1, j).setCurrent(12);
			if (megaTileAt(i, j).current() == 2 && megaTileAt(i + 1, j).current() == 4)
				megaTileAt(i + 1, j).setCurrent(12);
			if (megaTileAt(i, j).current() == 9 && megaTileAt(i + 1, j).current() == 4)
				megaTileAt(i + 1, j).setCurrent(12);
			if (megaTileAt(i, j).current() == 1 && megaTileAt(i, j - 1).current() == 8)
				megaTileAt(i, j - 1).setCurrent(9);
			if (megaTileAt(i, j).current() == 28 && megaTileAt(i + 1, j).current() == 23 && megaTileAt(i + 1, j + 1).current() == 3)
				megaTileAt(i + 1, j).setCurrent(16);
		}
	}

	for (int j = 0; j < DMAXY; j++) {
		for (int i = 0; i < DMAXX; i++) {
			if (megaTileAt(i, j).current() == 21 && megaTileAt(i + 1, j).current() == 10)
				megaTileAt(i + 1, j).setCurrent(17);
			if (megaTileAt(i, j).current() == 17 && megaTileAt(i + 1, j).current() == 4)
				megaTileAt(i + 1, j).setCurrent(12);
			if (megaTileAt(i, j).current() == 10 && megaTileAt(i + 1, j).current() == 4)
				megaTileAt(i + 1, j).setCurrent(12);
			if (megaTileAt(i, j).current() == 17 && megaTileAt(i, j + 1).current() == 5)
				megaTileAt(i, j + 1).setCurrent(12);
			if (megaTileAt(i, j).current() == 29 && megaTileAt(i, j + 1).current() == 9)
				megaTileAt(i, j + 1).setCurrent(10);
			if (megaTileAt(i, j).current() == 13 && megaTileAt(i, j + 1).current() == 5)
				megaTileAt(i, j + 1).setCurrent(12);
			if (megaTileAt(i, j).current() == 9 && megaTileAt(i, j + 1).current() == 16)
				megaTileAt(i, j + 1).setCurrent(13);
			if (megaTileAt(i, j).current() == 10 && megaTileAt(i, j + 1).current() == 16)
				megaTileAt(i, j + 1).setCurrent(13);
			if (megaTileAt(i, j).current() == 16 && megaTileAt(i, j + 1).current() == 3)
				megaTileAt(i, j + 1).setCurrent(4);
			if (megaTileAt(i, j).current() == 11 && megaTileAt(i, j + 1).current() == 5)
				megaTileAt(i, j + 1).setCurrent(12);
			if (megaTileAt(i, j).current() == 10 && megaTileAt(i + 1, j).current() == 3 && megaTileAt(i + 1, j - 1).current() == 16)
				megaTileAt(i + 1, j).setCurrent(12);
			if (megaTileAt(i, j).current() == 16 && megaTileAt(i, j + 1).current() == 5)
				megaTileAt(i, j + 1).setCurrent(12);
			if (megaTileAt(i, j).current() == 1 && megaTileAt(i, j + 1).current() == 6)
				megaTileAt(i, j + 1).setCurrent(4);
			if (megaTileAt(i, j).current() == 21 && megaTileAt(i + 1, j).current() == 13 && megaTileAt(i, j + 1).current() == 10)
				megaTileAt(i + 1, j + 1).setCurrent(12);
			if (megaTileAt(i, j).current() == 15 && megaTileAt(i + 1, j).current() == 10)
				megaTileAt(i + 1, j).setCurrent(17);
			if (megaTileAt(i, j).current() == 22 && megaTileAt(i, j + 1).current() == 11)
				megaTileAt(i, j + 1).setCurrent(17);
			if (megaTileAt(i, j).current() == 15 && megaTileAt(i + 1, j).current() == 28 && megaTileAt(i + 2, j).current() == 16)
				megaTileAt(i + 1, j).setCurrent(23);
			if (megaTileAt(i, j).current() == 28 && megaTileAt(i + 1, j).current() == 23 && megaTileAt(i + 1, j + 1).current() == 1 && megaTileAt(i + 2, j).current() == 6)
				megaTileAt(i + 1, j).setCurrent(16);
		}
	}

	for (int j = 0; j < DMAXY; j++) {
		for (int i = 0; i < DMAXX; i++) {
			if (megaTileAt(i, j).current() == 15 && megaTileAt(i + 1, j).current() == 28 && megaTileAt(i + 2, j).current() == 16)
				megaTileAt(i + 1, j).setCurrent(23);
			if (megaTileAt(i, j).current() == 21 && megaTileAt(i + 1, j - 1).current() == 21 && megaTileAt(i + 1, j + 1).current() == 13 && megaTileAt(i + 2, j).current() == 2)
				megaTileAt(i + 1, j).setCurrent(17);
			if (megaTileAt(i, j).current() == 19 && megaTileAt(i + 1, j).current() == 15 && megaTileAt(i + 1, j + 1).current() == 12)
				megaTileAt(i + 1, j).setCurrent(17);
		}
	}
}

void Substitution()
{
	for (int y = 0; y < DMAXY; y++) {
		for (int x = 0; x < DMAXX; x++) {
			if (FlipCoin(3)) {
				const uint8_t c = L4BTYPES[megaTileAt(x, y).current()];
				if (c != 0 && !protectedTiles().test(x, y)) {
					int rv = GenerateRnd(16);
					int i = -1;
					while (rv >= 0) {
						i++;
						if (i == sizeof(L4BTYPES)) {
							i = 0;
						}
						if (c == L4BTYPES[i]) {
							rv--;
						}
					}

					megaTileAt(x, y).setCurrent(i);
				}
			}
		}
	}
	for (int y = 0; y < DMAXY; y++) {
		for (int x = 0; x < DMAXX; x++) {
			if (FlipCoin(10)) {
				const uint8_t c = megaTileAt(x, y).current();
				if (L4BTYPES[c] == 6 && !protectedTiles().test(x, y)) {
					megaTileAt(x, y).setCurrent(PickRandomlyAmong({ 95, 96, 97 }));
				}
			}
		}
	}
}

/**
 * @brief Sets up the inside borders of the first quadrant so there are valid paths after mirroring the layout
 */
void PrepareInnerBorders()
{
	for (int y = (DMAXY / 2) - 1; y >= 0; y--) {
		for (int x = (DMAXX / 2) - 1; x >= 0; x--) {
			if (!dungeonMask().test(x, y)) {
				hallok[y] = false;
			} else {
				const bool hasSouthWestRoom = y + 1 < DMAXY / 2 && dungeonMask().test(x, y + 1);
				const bool hasSouthRoom = x + 1 < DMAXX / 2 && y + 1 < DMAXY / 2 && dungeonMask().test(x + 1, y + 1);
				hallok[y] = hasSouthWestRoom && !hasSouthRoom;
				x = 0;
			}
		}
	}

	int ry = GenerateRnd((DMAXY / 2) - 1) + 1;
	do {
		if (hallok[ry]) {
			for (int x = (DMAXX / 2) - 1; x >= 0; x--) {
				if (dungeonMask().test(x, ry)) {
					x = -1;
					ry = 0;
				} else {
					dungeonMask().set(x, ry);
					dungeonMask().set(x, ry + 1);
				}
			}
		} else {
			ry++;
			if (ry == DMAXY / 2) {
				ry = 1;
			}
		}
	} while (ry != 0);

	for (int x = (DMAXX / 2) - 1; x >= 0; x--) {
		for (int y = (DMAXY / 2) - 1; y >= 0; y--) {
			if (!dungeonMask().test(x, y)) {
				hallok[x] = false;
			} else {
				const bool hasSouthEastRoom = x + 1 < DMAXX / 2 && dungeonMask().test(x + 1, y);
				const bool hasSouthRoom = x + 1 < DMAXX / 2 && y + 1 < DMAXY / 2 && dungeonMask().test(x + 1, y + 1);
				hallok[x] = hasSouthEastRoom && !hasSouthRoom;
				y = 0;
			}
		}
	}

	int rx = GenerateRnd((DMAXX / 2) - 1) + 1;
	do {
		if (hallok[rx]) {
			for (int y = (DMAXY / 2) - 1; y >= 0; y--) {
				if (dungeonMask().test(rx, y)) {
					y = -1;
					rx = 0;
				} else {
					dungeonMask().set(rx, y);
					dungeonMask().set(rx + 1, y);
				}
			}
		} else {
			rx++;
			if (rx == DMAXX / 2) {
				rx = 1;
			}
		}
	} while (rx != 0);
}

/**
 * @brief Find the number of mega tiles used by layout
 */
inline size_t FindArea()
{
	// Hell layouts are mirrored based on a single quadrant, this function is called after the quadrant has been
	// generated but before mirroring the layout. We need to multiply by 4 to get the expected number of tiles
	return dungeonMask().count() * 4;
}

void ProtectQuads()
{
	for (int y = 0; y < 14; y++) {
		for (int x = 0; x < 14; x++) {
			protectedTiles().set(L4Hold.x + x, L4Hold.y + y);
			protectedTiles().set(DMAXX - 1 - x - L4Hold.x, L4Hold.y + y);
			protectedTiles().set(L4Hold.x + x, DMAXY - 1 - y - L4Hold.y);
			protectedTiles().set(DMAXX - 1 - x - L4Hold.x, DMAXY - 1 - y - L4Hold.y);
		}
	}
}

void LoadDiabQuads(bool preflag)
{
	{
		auto dunData = LoadFileInMem<uint16_t>("levels\\l4data\\diab1.dun");
		DiabloQuad1 = L4Hold + WorldTileDisplacement { 4, 4 };
		PlaceDunTiles(dunData.get(), DiabloQuad1, 6);
	}
	{
		auto dunData = LoadFileInMem<uint16_t>(preflag ? "levels\\l4data\\diab2b.dun" : "levels\\l4data\\diab2a.dun");
		DiabloQuad2 = WorldTilePosition(27 - L4Hold.x, 1 + L4Hold.y);
		PlaceDunTiles(dunData.get(), DiabloQuad2, 6);
	}
	{
		auto dunData = LoadFileInMem<uint16_t>(preflag ? "levels\\l4data\\diab3b.dun" : "levels\\l4data\\diab3a.dun");
		DiabloQuad3 = WorldTilePosition(1 + L4Hold.x, 27 - L4Hold.y);
		PlaceDunTiles(dunData.get(), DiabloQuad3, 6);
	}
	{
		auto dunData = LoadFileInMem<uint16_t>(preflag ? "levels\\l4data\\diab4b.dun" : "levels\\l4data\\diab4a.dun");
		DiabloQuad4 = WorldTilePosition(28 - L4Hold.x, 28 - L4Hold.y);
		PlaceDunTiles(dunData.get(), DiabloQuad4, 6);
	}
}

bool IsDURightWall(char d)
{
	if (d == 25) {
		return true;
	}
	if (d == 28) {
		return true;
	}
	if (d == 23) {
		return true;
	}

	return false;
}

bool IsDLLeftWall(char dd)
{
	if (dd == 27) {
		return true;
	}
	if (dd == 26) {
		return true;
	}
	if (dd == 22) {
		return true;
	}

	return false;
}

void FixTransparency()
{
	int yy = 16;
	for (int j = 0; j < DMAXY; j++) {
		int xx = 16;
		for (int i = 0; i < DMAXX; i++) {
			const int8_t transVal = tileAt(xx, yy).transVal();
			// BUGFIX: Should check for `j > 0` first.
			if (IsDURightWall(megaTileAt(i, j).current()) && megaTileAt(i, j - 1).current() == 18) {
				tileAt(xx + 1, yy).setTransVal(transVal);
				tileAt(xx + 1, yy + 1).setTransVal(transVal);
			}
			// BUGFIX: Should check for `i + 1 < DMAXY` first.
			if (IsDLLeftWall(megaTileAt(i, j).current()) && megaTileAt(i + 1, j).current() == 19) {
				tileAt(xx, yy + 1).setTransVal(transVal);
				tileAt(xx + 1, yy + 1).setTransVal(transVal);
			}
			if (megaTileAt(i, j).current() == 18) {
				tileAt(xx + 1, yy).setTransVal(transVal);
				tileAt(xx + 1, yy + 1).setTransVal(transVal);
			}
			if (megaTileAt(i, j).current() == 19) {
				tileAt(xx, yy + 1).setTransVal(transVal);
				tileAt(xx + 1, yy + 1).setTransVal(transVal);
			}
			if (megaTileAt(i, j).current() == 24) {
				tileAt(xx + 1, yy).setTransVal(transVal);
				tileAt(xx, yy + 1).setTransVal(transVal);
				tileAt(xx + 1, yy + 1).setTransVal(transVal);
			}
			if (megaTileAt(i, j).current() == 57) {
				const int8_t lowerTransVal = tileAt(xx, yy + 1).transVal();
				tileAt(xx - 1, yy).setTransVal(lowerTransVal);
				tileAt(xx, yy).setTransVal(lowerTransVal);
			}
			if (megaTileAt(i, j).current() == 53) {
				const int8_t rightTransVal = tileAt(xx + 1, yy).transVal();
				tileAt(xx, yy - 1).setTransVal(rightTransVal);
				tileAt(xx, yy).setTransVal(rightTransVal);
			}
			xx += 2;
		}
		yy += 2;
	}
}

void FixCornerTiles()
{
	for (int j = 1; j < DMAXY - 1; j++) {
		for (int i = 1; i < DMAXX - 1; i++) {
			if (megaTileAt(i, j).current() >= 18 && megaTileAt(i, j).current() <= 30) {
				if (megaTileAt(i + 1, j).current() < 18 || megaTileAt(i, j + 1).current() < 18) {
					megaTileAt(i, j).setCurrent(megaTileAt(i, j).current() + (98));
				}
			}
		}
	}
}

/**
 * @brief Marks the edge of the map as solid/not part of the dungeon layout
 */
void CloseOuterBorders()
{
	for (int x = 0; x < DMAXX / 2; x++) { // NOLINT(modernize-loop-convert)
		dungeonMask().reset(x, 0);
	}
	for (int y = 0; y < DMAXY / 2; y++) {
		dungeonMask().reset(0, y);
	}
}

void GeneralFix()
{
	for (int j = 0; j < DMAXY - 1; j++) {
		for (int i = 0; i < DMAXX - 1; i++) {
			if ((megaTileAt(i, j).current() == 24 || megaTileAt(i, j).current() == 122) && megaTileAt(i + 1, j).current() == 2 && megaTileAt(i, j + 1).current() == 5) {
				megaTileAt(i, j).setCurrent(17);
			}
		}
	}
}

bool PlaceStairs(lvl_entry entry)
{
	std::optional<Point> position;

	// Place stairs up
	position = PlaceMiniSet(L4USTAIRS);
	if (!position)
		return false;
	if (entry == ENTRY_MAIN)
		viewPosition() = position->megaToWorld() + Displacement { 6, 6 };

	if (currentLevelNumber() != 15) {
		// Place stairs down
		if (currentLevelNumber() != 16) {
			if (Quests[Q_WARLORD].IsAvailable()) {
				if (entry == ENTRY_PREV)
					viewPosition() = setPiece().position.megaToWorld() + Displacement { 7, 7 };
			} else {
				position = PlaceMiniSet(L4DSTAIRS);
				if (!position)
					return false;
				if (entry == ENTRY_PREV)
					viewPosition() = position->megaToWorld() + Displacement { 7, 5 };
			}
		}

		// Place town warp stairs
		if (currentLevelNumber() == 13) {
			position = PlaceMiniSet(L4TWARP);
			if (!position)
				return false;
			if (entry == ENTRY_TWARPDN)
				viewPosition() = position->megaToWorld() + Displacement { 6, 6 };
		}
	} else {
		// Place hell gate
		position = PlaceMiniSet(L4PENTA2);
		if (!position)
			return false;
		Quests[Q_DIABLO].position = *position;
		if (entry == ENTRY_PREV)
			viewPosition() = position->megaToWorld() + Displacement { 6, 5 };
	}

	return true;
}

void GenerateLevel(lvl_entry entry)
{
	if (LevelSeeds[currentLevelNumber()])
		SetRndSeed(*LevelSeeds[currentLevelNumber()]);

	while (true) {
		DRLG_InitTrans();

		constexpr size_t Minarea = 692;
		do {
			LevelSeeds[currentLevelNumber()] = GetLCGEngineState();
			InitDungeonFlags();
			FirstRoom();
			CloseOuterBorders();
		} while (FindArea() < Minarea);

		PrepareInnerBorders();
		MirrorDungeonLayout();

		MakeDmt();
		FixTilesPatterns();
		if (currentLevelNumber() == 16) {
			ProtectQuads();
		}
		if (Quests[Q_WARLORD].IsAvailable() || (currentLevelNumber() == Quests[Q_BETRAYER]._qlevel && UseMultiplayerQuests())) {
			for (int spi = setPieceRoom().position.x; spi < setPieceRoom().position.x + setPieceRoom().size.width - 1; spi++) {
				for (int spj = setPieceRoom().position.y; spj < setPieceRoom().position.y + setPieceRoom().size.height - 1; spj++) {
					protectedTiles().set(spi, spj);
				}
			}
		}
		AddWall();
		FloodTransparencyValues(6);
		FixTransparency();
		InitSetPiece();
		if (currentLevelNumber() == 16) {
			LoadDiabQuads(true);
		}
		if (PlaceStairs(entry))
			break;
	}

	GeneralFix();

	if (currentLevelNumber() != 16) {
		DRLG_PlaceThemeRooms(7, 10, 6, 8, true);
	}

	ApplyShadowsPatterns();
	FixCornerTiles();
	Substitution();

	SnapshotReplacementMegaTiles();

	DRLG_CheckQuests(setPieceRoom().position);

	if (currentLevelNumber() == 15) {
		const bool isGateOpen = UseMultiplayerQuests() || IsAnyOf(Quests[Q_DIABLO]._qactive, QUEST_ACTIVE, QUEST_DONE);
		if (!isGateOpen)
			L4PENTA.place(Quests[Q_DIABLO].position);

		for (WorldTileCoord j = 1; j < DMAXY; j++) {
			for (WorldTileCoord i = 1; i < DMAXX; i++) {
				if (IsAnyOf(megaTileAt(i, j).current(), 98, 107)) {
					Make_SetPC({ WorldTilePosition(i - 1, j - 1), { 5, 5 } });
					// Set the portal position to the location of the northmost pentagram tile.
					Quests[Q_BETRAYER].position = Point(i, j).megaToWorld();
				}
			}
		}
	}
	if (currentLevelNumber() == 16) {
		LoadDiabQuads(false);
	}
}

void Pass3()
{
	DRLG_LPass3(30 - 1);
}

} // namespace

void CreateL4Dungeon(uint32_t rseed, lvl_entry entry)
{
	SetRndSeed(rseed);

	GenerateLevel(entry);

	Pass3();
}

void LoadPreL4Dungeon(const char *path)
{
	FillCurrentMegaTiles(30);

	auto dunData = LoadFileInMem<uint16_t>(path);
	PlaceDunTiles(dunData.get(), { 0, 0 }, 6);

	SnapshotReplacementMegaTiles();
}

void LoadL4Dungeon(const char *path, Point spawn)
{
	LoadDungeonBase(path, spawn, 6, 30);

	Pass3();
}

} // namespace devilution

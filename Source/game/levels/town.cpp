/**
 * @file game/levels/town.cpp
 *
 * Implementation of town.
 */


#include "game/levels/town.h"

#include <cstdint>
#include <initializer_list>
#include <utility>

#include "engine/load/load_file.hpp"
#include "engine/random.hpp"
#include "engine/math/world_tile.hpp"
#include "application/game_mode.hpp"
#include "game/levels/level_l1.h"
#include "game/levels/triggers.h"
#include "network/protocol/multi.h"
#include "game/players/players.hpp"
#include "game/quests/quests.hpp"
#include "network/protocol/legacy_world_state_policy.hpp"
#include "utils/endian/endian_swap.hpp"

#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
#include "network/authoritative/server_backed_runtime.hpp"
#endif

namespace devilution {

namespace {

void SetTownPieces(std::initializer_list<std::pair<Point, uint16_t>> pieces)
{
	for (const auto &[position, piece] : pieces)
		tileAt(position).setPiece(piece);
}

/**
 * @brief Load level piece data.
 * @param path Path of dun file
 * @param xi upper left destination
 * @param yy upper left destination
 */
void FillSector(const char *path, int xi, int yy)
{
	auto dunData = LoadFileInMem<uint16_t>(path);

	const WorldTileSize size = GetDunSize(dunData.get());
	const uint16_t *tileLayer = &dunData[2];

	for (WorldTileCoord j = 0; j < size.height; j++) {
		int xx = xi;
		for (WorldTileCoord i = 0; i < size.width; i++) {
			int v1 = 218;
			int v2 = 218;
			int v3 = 218;
			int v4 = 218;

			const int tileId = Swap16LE(tileLayer[(j * size.width) + i]) - 1;
			if (tileId >= 0) {
				const MegaTile mega = megaTiles()[tileId];
				v1 = Swap16LE(mega.micro1);
				v2 = Swap16LE(mega.micro2);
				v3 = Swap16LE(mega.micro3);
				v4 = Swap16LE(mega.micro4);
			}

			tileAt(xx + 0, yy + 0).setPiece(v1);
			tileAt(xx + 1, yy + 0).setPiece(v2);
			tileAt(xx + 0, yy + 1).setPiece(v3);
			tileAt(xx + 1, yy + 1).setPiece(v4);
			xx += 2;
		}
		yy += 2;
	}
}

/**
 * @brief Load a tile's piece data.
 * @param xx upper left destination
 * @param yy upper left destination
 * @param t tile id
 */
void FillTile(int xx, int yy, int t)
{
	const MegaTile mega = megaTiles()[t - 1];

	tileAt(xx + 0, yy + 0).setPiece(Swap16LE(mega.micro1));
	tileAt(xx + 1, yy + 0).setPiece(Swap16LE(mega.micro2));
	tileAt(xx + 0, yy + 1).setPiece(Swap16LE(mega.micro3));
	tileAt(xx + 1, yy + 1).setPiece(Swap16LE(mega.micro4));
}

/**
 * @brief Update the map to show the closed hive
 */
void TownCloseHive()
{
	megaTileAt(35, 27).setCurrent(18);
	megaTileAt(36, 27).setCurrent(63);

	SetTownPieces({
	    { { 78, 60 }, 0x489 }, { { 79, 60 }, 0x4ea },
	    { { 78, 61 }, 0x4eb }, { { 79, 61 }, 0x4ec },
	    { { 78, 62 }, 0x4ed }, { { 79, 62 }, 0x4ee },
	    { { 78, 63 }, 0x4ef }, { { 79, 63 }, 0x4f0 },
	    { { 78, 64 }, 0x4f1 }, { { 79, 64 }, 0x4f2 },
	    { { 78, 65 }, 0x4f3 }, { { 80, 60 }, 0x4f4 },
	    { { 81, 60 }, 0x4f5 }, { { 80, 61 }, 0x4f6 },
	    { { 81, 61 }, 0x4f7 }, { { 82, 60 }, 0x4f8 },
	    { { 83, 60 }, 0x4f9 }, { { 82, 61 }, 0x4fa },
	    { { 83, 61 }, 0x4fb }, { { 80, 62 }, 0x4fc },
	    { { 81, 62 }, 0x4fd }, { { 80, 63 }, 0x4fe },
	    { { 81, 63 }, 0x4ff }, { { 80, 64 }, 0x500 },
	    { { 81, 64 }, 0x501 }, { { 80, 65 }, 0x502 },
	    { { 81, 65 }, 0x503 }, { { 82, 62 }, 0x504 },
	    { { 83, 62 }, 0x505 }, { { 82, 63 }, 0x506 },
	    { { 83, 63 }, 0x507 }, { { 82, 64 }, 0x508 },
	    { { 83, 64 }, 0x509 }, { { 82, 65 }, 0x50a },
	    { { 83, 65 }, 0x50b }, { { 84, 61 }, 279 },
	    { { 84, 62 }, 280 }, { { 84, 63 }, 279 },
	    { { 84, 64 }, 10 }, { { 85, 60 }, 11 },
	    { { 85, 61 }, 12 }, { { 85, 62 }, 13 },
	    { { 85, 63 }, 14 }, { { 85, 64 }, 15 },
	    { { 86, 60 }, 16 }, { { 86, 61 }, 17 },
	});
}

/**
 * @brief Update the map to show the closed grave
 */
void TownCloseGrave()
{
	SetTownPieces({
	    { { 36, 21 }, 0x52a }, { { 37, 21 }, 0x52b },
	    { { 36, 22 }, 0x52c }, { { 37, 22 }, 0x52d },
	    { { 36, 23 }, 0x52e }, { { 37, 23 }, 0x52f },
	    { { 36, 24 }, 0x530 }, { { 37, 24 }, 0x531 },
	    { { 35, 21 }, 0x53a }, { { 34, 21 }, 0x53b },
	});
}

void InitTownPieces()
{
	for (Tile &tile : tiles()) {
		const uint16_t piece = tile.piece();
		if (piece == 359) {
			tile.setSpecial(1);
		} else if (piece == 357) {
			tile.setSpecial(2);
		} else if (piece == 128) {
			tile.setSpecial(6);
		} else if (piece == 129) {
			tile.setSpecial(7);
		} else if (piece == 127) {
			tile.setSpecial(8);
		} else if (piece == 116) {
			tile.setSpecial(9);
		} else if (piece == 156) {
			tile.setSpecial(10);
		} else if (piece == 157) {
			tile.setSpecial(11);
		} else if (piece == 155) {
			tile.setSpecial(12);
		} else if (piece == 161) {
			tile.setSpecial(13);
		} else if (piece == 159) {
			tile.setSpecial(14);
		} else if (piece == 213) {
			tile.setSpecial(15);
		} else if (piece == 211) {
			tile.setSpecial(16);
		} else if (piece == 216) {
			tile.setSpecial(17);
		} else if (piece == 215) {
			tile.setSpecial(18);
		}
	}
}

/**
 * @brief Initialize all of the levels data
 */
void DrlgTPass3()
{
	for (int yy = 0; yy < MAXDUNY; yy += 2) {
		for (int xx = 0; xx < MAXDUNX; xx += 2) {
			tileAt(xx, yy).setPiece(426);
			tileAt(xx + 1, yy).setPiece(426);
			tileAt(xx, yy + 1).setPiece(426);
			tileAt(xx + 1, yy + 1).setPiece(426);
		}
	}

	FillSector("levels\\towndata\\sector1s.dun", 46, 46);
	FillSector("levels\\towndata\\sector2s.dun", 46, 0);
	FillSector("levels\\towndata\\sector3s.dun", 0, 46);
	FillSector("levels\\towndata\\sector4s.dun", 0, 0);

	auto dunData = LoadFileInMem<uint16_t>("levels\\towndata\\automap.dun");
	PlaceDunTiles(dunData.get(), { 0, 0 });

	if (!IsWarpOpen(DTYPE_CATACOMBS)) {
		megaTileAt(20, 7).setCurrent(10);
		megaTileAt(20, 6).setCurrent(8);
		FillTile(48, 20, 320);
	}
	if (!IsWarpOpen(DTYPE_CAVES)) {
		megaTileAt(4, 30).setCurrent(8);
		FillTile(16, 68, 332);
		FillTile(16, 70, 331);
	}
	if (!IsWarpOpen(DTYPE_HELL)) {
		megaTileAt(15, 35).setCurrent(7);
		megaTileAt(16, 35).setCurrent(7);
		megaTileAt(17, 35).setCurrent(7);
		for (int x = 36; x < 46; x++) {
			FillTile(x, 78, PickRandomlyAmong({ 1, 2, 3, 4 }));
		}
	}
	if (gbIsHellfire) {
		if (IsWarpOpen(DTYPE_NEST)) {
			TownOpenHive();
		} else {
			TownCloseHive();
		}
		if (IsWarpOpen(DTYPE_CRYPT))
			TownOpenGrave();
		else
			TownCloseGrave();
	}

	if (Quests[Q_PWATER]._qactive != QUEST_DONE && Quests[Q_PWATER]._qactive != QUEST_NOTAVAIL) {
		FillTile(60, 70, 342);
	} else {
		FillTile(60, 70, 71);
	}

	InitTownPieces();
}

} // namespace

bool OpensHive(Point position)
{
	const int yp = position.y;
	const int xp = position.x;
	return xp >= 79 && xp <= 82 && yp >= 61 && yp <= 64;
}

bool OpensGrave(Point position)
{
	const int yp = position.y;
	const int xp = position.x;
	return xp >= 35 && xp <= 38 && yp >= 20 && yp <= 24;
}

void OpenHive()
{
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacyWorldStateMessage(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
	#endif

	NetSendCmd(false, CMD_OPENHIVE);
	auto &quest = Quests[Q_FARMER];
	quest._qactive = QUEST_DONE;
	if (gbIsMultiplayer)
		NetSendCmdQuest(true, quest);
}

void OpenGrave()
{
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacyWorldStateMessage(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
	#endif

	NetSendCmd(false, CMD_OPENGRAVE);
	auto &quest = Quests[Q_GRAVE];
	quest._qactive = QUEST_DONE;
	if (gbIsMultiplayer)
		NetSendCmdQuest(true, quest);
}

void TownOpenHive()
{
	megaTileAt(36, 27).setCurrent(47);

	SetTownPieces({
	    { { 78, 60 }, 0x489 }, { { 79, 60 }, 0x48a },
	    { { 78, 61 }, 0x48b }, { { 79, 61 }, 0x50d },
	    { { 78, 62 }, 0x4ed }, { { 78, 63 }, 0x4ef },
	    { { 79, 62 }, 0x50f }, { { 79, 63 }, 0x510 },
	    { { 79, 64 }, 0x511 }, { { 78, 64 }, 0x119 },
	    { { 78, 65 }, 0x11b }, { { 79, 65 }, 0x11c },
	    { { 80, 60 }, 0x512 }, { { 80, 61 }, 0x514 },
	    { { 81, 61 }, 0x515 }, { { 82, 60 }, 0x516 },
	    { { 83, 60 }, 0x517 }, { { 82, 61 }, 0x518 },
	    { { 83, 61 }, 0x519 }, { { 80, 62 }, 0x51a },
	    { { 81, 62 }, 0x51b }, { { 80, 63 }, 0x51c },
	    { { 81, 63 }, 0x51d }, { { 80, 64 }, 0x51e },
	    { { 81, 64 }, 0x51f }, { { 80, 65 }, 0x520 },
	    { { 81, 65 }, 0x521 }, { { 82, 62 }, 0x522 },
	    { { 83, 62 }, 0x523 }, { { 82, 63 }, 0x524 },
	    { { 83, 63 }, 0x525 }, { { 82, 64 }, 0x526 },
	    { { 83, 64 }, 0x527 }, { { 82, 65 }, 0x528 },
	    { { 83, 65 }, 0x529 }, { { 84, 61 }, 279 },
	    { { 84, 62 }, 280 }, { { 84, 63 }, 279 },
	    { { 84, 64 }, 10 }, { { 85, 60 }, 11 },
	    { { 85, 61 }, 12 }, { { 85, 62 }, 13 },
	    { { 85, 63 }, 14 }, { { 85, 64 }, 15 },
	    { { 86, 60 }, 16 }, { { 86, 61 }, 17 },
	});
}

void TownOpenGrave()
{
	megaTileAt(14, 8).setCurrent(47);
	megaTileAt(14, 7).setCurrent(47);

	SetTownPieces({
	    { { 36, 21 }, 0x532 }, { { 37, 21 }, 0x533 },
	    { { 36, 22 }, 0x534 }, { { 37, 22 }, 0x535 },
	    { { 36, 23 }, 0x536 }, { { 37, 23 }, 0x537 },
	    { { 36, 24 }, 0x538 }, { { 37, 24 }, 0x539 },
	    { { 35, 21 }, 0x53a }, { { 34, 21 }, 0x53b },
	});
}

void CleanTownFountain()
{
	if (!megaTiles())
		return;
	FillTile(60, 70, 71);
}

void CreateTown(lvl_entry entry)
{
	minimumDungeonPosition() = { 10, 10 };
	maximumDungeonPosition() = { 84, 84 };

	if (entry == ENTRY_MAIN) { // New game
		viewPosition() = { 75, 68 };
	} else if (entry == ENTRY_PREV) { // Cathedral
		viewPosition() = { 25, 31 };
	} else if (entry == ENTRY_TWARPUP) {
		if (tWarpFrom() == 5) {
			viewPosition() = { 49, 22 };
		}
		if (tWarpFrom() == 9) {
			viewPosition() = { 18, 69 };
		}
		if (tWarpFrom() == 13) {
			viewPosition() = { 41, 81 };
		}
		if (tWarpFrom() == 21) {
			viewPosition() = { 36, 25 };
		}
		if (tWarpFrom() == 17) {
			viewPosition() = { 79, 62 };
		}
	}

	DrlgTPass3();
}

} // namespace devilution

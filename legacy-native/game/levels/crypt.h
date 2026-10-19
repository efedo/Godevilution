/**
 * @file levels/crypt.h
 *
 * Interface of the cathedral level generation algorithms.
 */
#pragma once

#include "game/levels/dungeon_common.h"

namespace devilution {

extern const Miniset L5STAIRSUP;

void InitCryptPieces();
void SetCryptRoom();
void SetCornerRoom();
void FixCryptDirtTiles();
bool PlaceCryptStairs(lvl_entry entry);
void CryptSubstitution();
void SetCryptSetPieceRoom();
void PlaceCryptLights();

} // namespace devilution

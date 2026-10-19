/**
 * @file game/levels/level_l1.h
 *
 * Interface of the cathedral level generation algorithms.
 */
#pragma once

#include <cstdint>

#include "engine/math/world_tile.hpp"
#include "game/levels/dungeon_common.h"

namespace devilution {

void PlaceMiniSetRandom(const Miniset &miniset, int rndper);
WorldTilePosition SelectChamber();
void CreateL5Dungeon(uint32_t rseed, lvl_entry entry);
void LoadPreL1Dungeon(const char *path);
void LoadL1Dungeon(const char *path, Point spawn);

} // namespace devilution

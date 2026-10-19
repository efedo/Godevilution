/**
 * @file game/levels/level_l3.h
 *
 * Interface of the caves level generation algorithms.
 */
#pragma once

#include <cstdint>

#include "game/levels/dungeon_common.h"

namespace devilution {

void CreateL3Dungeon(uint32_t rseed, lvl_entry entry);
void LoadPreL3Dungeon(const char *sFileName);
void LoadL3Dungeon(const char *sFileName, Point spawn);

} // namespace devilution

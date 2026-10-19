/**
 * @file engine/gfx/translation_table.cpp
 *
 * Implementation of translation table handling.
 */


#include "engine/gfx/translation_table.hpp"

#include <cstdint>

#include <fmt/format.h>

#ifdef _DEBUG
#include "application/debug.h"
#endif
#include "engine/load/load_file.hpp"
#include "engine/lighting.h"
#include "utils/string/str_cat.hpp"

namespace devilution {

uint8_t *GetInfravisionTRN()
{
	return CurrentLightManager.infravisionTable_.data();
}

uint8_t *GetStoneTRN()
{
	return CurrentLightManager.stoneTable_.data();
}

uint8_t *GetPauseTRN()
{
	return CurrentLightManager.pauseTable_.data();
}

std::optional<std::array<uint8_t, 256>> GetClassTRN(Player &player)
{
	std::array<uint8_t, 256> trn;
	char path[64];

	const PlayerSpriteData &spriteData = GetPlayerSpriteDataForClass(player._pClass);
	*BufCopy(path, "plrgfx\\", spriteData.trn, ".trn") = '\0';

#ifdef _DEBUG
	if (!debugTRN.empty()) {
		*BufCopy(path, debugTRN.c_str()) = '\0';
	}
#endif
	if (LoadOptionalFileInMem(path, &trn[0], 256)) {
		return trn;
	}
	return std::nullopt;
}

std::optional<std::array<uint8_t, 256>> GetPlayerGraphicTRN(const char *pszName)
{
	char path[MaxMpqPathSize];
	*BufCopy(path, pszName, ".trn") = '\0';

	std::array<uint8_t, 256> trn;
	if (LoadOptionalFileInMem(path, &trn[0], 256)) {
		return trn;
	}
	return std::nullopt;
}

} // namespace devilution

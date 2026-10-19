#pragma once

#include <cstdint>
#include <optional>

#include "game/levels/dungeon_common_defs.hpp"

namespace devilution {

/** Shared wire-level identity rules for regular and materialized set-level maps. */
constexpr uint32_t AuthoritativeRuntimeSetLevelIdBase = 0x8000'0000;
constexpr uint32_t AuthoritativeRuntimeSetLevelVariantCount = 18;

constexpr bool IsAuthoritativeRegularLevel(const uint32_t levelId)
{
	return levelId < NUMLEVELS;
}

constexpr bool IsAuthoritativeRuntimeSetLevel(const uint32_t levelId)
{
	return levelId >= AuthoritativeRuntimeSetLevelIdBase
	    && levelId < AuthoritativeRuntimeSetLevelIdBase + AuthoritativeRuntimeSetLevelVariantCount;
}

constexpr bool IsAuthoritativeSupportedLevel(const uint32_t levelId)
{
	return IsAuthoritativeRegularLevel(levelId) || IsAuthoritativeRuntimeSetLevel(levelId);
}

/** Maps a reserved runtime set-level ID to the native set-level enum. */
constexpr std::optional<_setlevels> AuthoritativeNativeSetLevel(const uint32_t levelId)
{
	if (!IsAuthoritativeRuntimeSetLevel(levelId))
		return std::nullopt;
	const auto setLevelId = (levelId - AuthoritativeRuntimeSetLevelIdBase) / 2;
	return static_cast<_setlevels>(setLevelId + 1);
}

} // namespace devilution

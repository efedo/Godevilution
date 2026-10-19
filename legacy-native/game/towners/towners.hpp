/**
 * @file game/towners/towners.hpp
 *
 * Interface of functionality for loading and spawning towners.
 */
#pragma once

#include <cstddef>
#include <cstdint>
#include <memory>
#include <span>
#include <string>
#include <string_view>
#include <unordered_map>
#include <vector>

#include "engine/actor.hpp"
#include "engine/gfx/clx_sprite.hpp"
#include "game/levels/dun_tile.hpp"
#include "game/players/players.hpp"
#include "game/quests/quests.hpp"
#include "utils/attributes.h"

namespace devilution {

enum _talker_id : uint8_t {
	TOWN_SMITH,
	TOWN_HEALER,
	TOWN_DEADGUY,
	TOWN_TAVERN,
	TOWN_STORY,
	TOWN_DRUNK,
	TOWN_WITCH,
	TOWN_BMAID,
	TOWN_PEGBOY,
	TOWN_COW,
	TOWN_FARMER,
	TOWN_GIRL,
	TOWN_COWFARM,
	// Note: Enum values are parsed from TSV using magic_enum
	// The actual count is determined dynamically from TSV data
};

// Runtime mappings built from TSV data
extern DVL_API_FOR_TEST std::unordered_map<_talker_id, std::string> TownerLongNames; // Maps towner type enum to display name
extern std::unordered_map<_talker_id, std::string> TownerShortNames;                // Maps towner type enum to Lua/mod short name

struct Towner : Actor {
	OptionalOwnedClxSpriteList ownedAnim;
	OptionalClxSpriteList anim;
	std::span<const uint8_t> animOrder; // Specifies the animation frame sequence.
	void (*talk)(Player &player, Towner &towner);

	std::string_view name;

	Point position; // Tile position of NPC
	_speech_id gossip; // Randomly chosen topic for discussion (picked when loading into town)
	uint16_t _tAnimWidth;
	int16_t _tAnimDelay; // Tick length of each frame in the current animation
	int16_t _tAnimCnt; // Increases by one each game tick, counting how close we are to _pAnimDelay
	uint8_t _tAnimLen; // Number of frames in current animation
	uint8_t _tAnimFrame; // Current frame of animation.
	uint8_t _tAnimFrameCnt;
	_talker_id _ttype;

	[[nodiscard]] ClxSprite currentSprite() const
	{
		return (*anim)[_tAnimFrame];
	}
	[[nodiscard]] Displacement getRenderingOffset() const
	{
		return { -CalculateSpriteTileCenterX(_tAnimWidth), 0 };
	}
};

extern std::vector<Towner> Towners;

size_t GetNumTownerTypes(); // Number of unique towner types in TSV data
size_t GetNumTowners(); // Number of spawned towner instances
bool IsTownerPresent(_talker_id npc);
Towner *GetTowner(_talker_id type); // null if not initialized

void InitTowners();
void FreeTownerGFX();
void ProcessTowners();
void TalkToTowner(Player &player, int t);

void UpdateGirlAnimAfterQuestComplete();
void UpdateCowFarmerAnimAfterQuestComplete();

#ifdef _DEBUG
bool DebugTalkToTowner(_talker_id type);
#endif

} // namespace devilution

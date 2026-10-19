/**
 * @file application/debug.cpp
 *
 * Implementation of debug functions.
 */

#include "application/debug.h"
#ifdef _DEBUG

#include <algorithm>
#include <array>
#include <charconv>
#include <cmath>
#include <climits>
#include <cstdint>
#include <cstdio>
#include <random>
#include <utility>

#include <ankerl/unordered_dense.h>

#ifdef USE_SDL3
#include <SDL3/SDL_timer.h>
#else
#include <SDL.h>
#endif

#include "application/debug.h"
#include "application/debug_commands.hpp"

#include "engine/crawl.hpp"
#include "engine/random.hpp"
#include "engine/render/world_renderer.h"

#include "engine/assets.hpp"
#include "engine/gfx/backbuffer_state.hpp"
#include "game/items/items.hpp"
#include "game/items/item_pool.hpp"
#include "game/levels/dungeon_common.h"
#include "game/levels/setmaps.h"
#include "game/levels/tile_properties.hpp"
#include "game/objects/objects.hpp"
#include "game/spells/spells.hpp"
#include "game/quests/quests.hpp"
#include "game/players/players.hpp"
#include "ui/automap.h"
#include "engine/cursor.h"
#include "engine/load/load_cel.hpp"
#include "engine/math/point.hpp"
#include "engine/lighting.h"
#include "game/missiles/missiles.hpp"
#include "game/monsters/monsters.hpp"
#include "game/monsters/monster_pool.hpp"
#include "game/towners/towners.hpp"
#include "network/protocol/msg.h"
#include "network/protocol/legacy_debug_policy.hpp"
#include "persistence/pack.h"
#include "tables/spelldat.h"
#include "ui/load_screens.h"
#include "ui/player_messages.h"
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
#include "network/authoritative/server_backed_runtime.hpp"
#endif
#include "utils/endian/endian_stream.hpp"
#include "utils/file/file_util.h"
#include "utils/is_of.hpp"
#include "utils/string/str_case.hpp"
#include "utils/string/str_cat.hpp"

namespace devilution {

std::string TestMapPath;
OptionalOwnedClxSpriteList pSquareCel;
bool DebugToggle = false;
bool DebugGodMode = false;
bool DebugInvisible = false;
bool DebugVision = false;
bool DebugPath = false;
bool DebugGrid = false;
ankerl::unordered_dense::map<int, Point> DebugCoordsMap;
bool DebugScrollViewEnabled = false;
std::string debugTRN;

// Used for debugging level generation
uint32_t glMid1Seed[NUMLEVELS];
uint32_t glMid2Seed[NUMLEVELS];
uint32_t glMid3Seed[NUMLEVELS];
uint32_t glEndSeed[NUMLEVELS];

namespace {

DebugGridTextItem SelectedDebugGridTextItem;

int DebugMonsterId;

std::vector<std::string> SearchMonsters;
std::vector<std::string> SearchItems;
std::vector<std::string> SearchObjects;

std::mt19937 BetterRng;

ankerl::unordered_dense::map<std::string_view, _talker_id> TownerShortNameToTownerId = {
	{ "griswold", _talker_id::TOWN_SMITH },
	{ "smith", _talker_id::TOWN_SMITH },
	{ "pepin", _talker_id::TOWN_HEALER },
	{ "healer", _talker_id::TOWN_HEALER },
	{ "ogden", _talker_id::TOWN_TAVERN },
	{ "tavern", _talker_id::TOWN_TAVERN },
	{ "cain", _talker_id::TOWN_STORY },
	{ "story", _talker_id::TOWN_STORY },
	{ "farnham", _talker_id::TOWN_DRUNK },
	{ "drunk", _talker_id::TOWN_DRUNK },
	{ "adria", _talker_id::TOWN_WITCH },
	{ "witch", _talker_id::TOWN_WITCH },
	{ "gillian", _talker_id::TOWN_BMAID },
	{ "bmaid", _talker_id::TOWN_BMAID },
	{ "wirt", _talker_id::TOWN_PEGBOY },
	{ "pegboy", _talker_id::TOWN_PEGBOY },
	{ "lester", _talker_id::TOWN_FARMER },
	{ "farmer", _talker_id::TOWN_FARMER },
	{ "girl", _talker_id::TOWN_GIRL },
	{ "nut", _talker_id::TOWN_COWFARM },
	{ "cowfarm", _talker_id::TOWN_COWFARM },
};

std::optional<bool> ParseOptionalBool(std::string_view value)
{
	if (value.empty())
		return std::nullopt;
	if (value == "on" || value == "true" || value == "1")
		return true;
	if (value == "off" || value == "false" || value == "0")
		return false;
	return std::nullopt;
}

bool HasInvalidBoolArgument(std::string_view value)
{
	return !value.empty() && !ParseOptionalBool(value).has_value();
}

std::optional<uint8_t> ParseOptionalLevel(std::string_view value)
{
	if (value.empty())
		return std::nullopt;
	uint16_t parsed = 0;
	const auto [end, error] = std::from_chars(value.data(), value.data() + value.size(), parsed);
	if (error != std::errc {} || end != value.data() + value.size() || parsed > UINT8_MAX)
		return std::nullopt;
	return static_cast<uint8_t>(parsed);
}

std::optional<int> ParseRequiredInt(std::string_view value)
{
	if (value.empty())
		return std::nullopt;
	int parsed = 0;
	const auto [end, error] = std::from_chars(value.data(), value.data() + value.size(), parsed);
	return error == std::errc {} && end == value.data() + value.size()
	    ? std::optional<int> { parsed }
	    : std::nullopt;
}

struct NamedCountArguments {
	std::string_view name;
	std::optional<unsigned> count;
};

NamedCountArguments ParseNamedCountArguments(std::string_view value)
{
	const size_t separator = value.find_last_of(" \t");
	if (separator == std::string_view::npos)
		return NamedCountArguments { value, std::nullopt };

	const std::string_view countText = value.substr(separator + 1);
	unsigned count = 0;
	const auto [end, error] = std::from_chars(countText.data(), countText.data() + countText.size(), count);
	if (error != std::errc {} || end != countText.data() + countText.size())
		return NamedCountArguments { value, std::nullopt };

	const size_t nameEnd = value.find_last_not_of(" \t", separator);
	return NamedCountArguments {
	    nameEnd == std::string_view::npos ? std::string_view {} : value.substr(0, nameEnd + 1),
	    count,
	};
}

std::optional<std::pair<uint8_t, std::optional<int>>> ParseLevelResetArguments(std::string_view value)
{
	const size_t separator = value.find_first_of(" \t");
	const std::string_view levelText = separator == std::string_view::npos ? value : value.substr(0, separator);
	const auto level = ParseOptionalLevel(levelText);
	if (!level.has_value())
		return std::nullopt;
	if (separator == std::string_view::npos)
		return std::pair { *level, std::optional<int> {} };

	const size_t seedStart = value.find_first_not_of(" \t", separator);
	if (seedStart == std::string_view::npos)
		return std::nullopt;
	const size_t seedEnd = value.find_first_of(" \t", seedStart);
	if (seedEnd != std::string_view::npos)
		return std::nullopt;
	const auto seed = ParseRequiredInt(value.substr(seedStart));
	return seed.has_value() ? std::optional<std::pair<uint8_t, std::optional<int>>> { std::pair { *level, seed } } : std::nullopt;
}

struct CustomMapWarpArguments {
	std::string_view path;
	int dunType;
	int x;
	int y;
};

std::optional<CustomMapWarpArguments> ParseCustomMapWarpArguments(std::string_view value)
{
	std::array<std::string_view, 4> tokens;
	size_t cursor = 0;
	for (std::string_view &token : tokens) {
		const size_t tokenStart = value.find_first_not_of(" \t", cursor);
		if (tokenStart == std::string_view::npos)
			return std::nullopt;
		const size_t tokenEnd = value.find_first_of(" \t", tokenStart);
		token = value.substr(tokenStart, tokenEnd == std::string_view::npos ? std::string_view::npos : tokenEnd - tokenStart);
		cursor = tokenEnd == std::string_view::npos ? value.size() : tokenEnd;
	}
	if (value.find_first_not_of(" \t", cursor) != std::string_view::npos)
		return std::nullopt;
	const auto dunType = ParseRequiredInt(tokens[1]);
	const auto x = ParseRequiredInt(tokens[2]);
	const auto y = ParseRequiredInt(tokens[3]);
	if (!dunType.has_value() || !x.has_value() || !y.has_value())
		return std::nullopt;
	return CustomMapWarpArguments { tokens[0], *dunType, *x, *y };
}

constexpr std::array<std::string_view, 23> DebugTileDataNames {
	"microTiles",
	"piece",
	"transVal",
	"dLight",
	"dPreLight",
	"dFlags",
	"dPlayer",
	"dMonster",
	"missiles",
	"dCorpse",
	"dObject",
	"dItem",
	"dSpecial",
	"coords",
	"cursorcoords",
	"objectindex",
	"solid",
	"transparent",
	"trap",
	"AutomapView",
	"currentMegaTile",
	"replacementMegaTile",
	"protectedTiles()",
};

bool HasInvalidLevelArgument(std::string_view value)
{
	return !value.empty() && !ParseOptionalLevel(value).has_value();
}

void PrintDebugMonster(const Monster &monster)
{
	EventPlrMsg(StrCat(
	                "Monster ", static_cast<int>(monster.getId()), " = ", monster.name(),
	                "\nX = ", monster.position.tile.x, ", Y = ", monster.position.tile.y,
	                "\nEnemy = ", monster.enemy, ", HP = ", monster.hitPoints,
	                "\nMode = ", static_cast<int>(monster.mode), ", Var1 = ", monster.var1),
	    UiFlags::ColorWhite);

	bool bActive = false;

	for (const unsigned m : MonsterPoolAdapter::ActiveMonsterRange()) {
		if (&Monsters[m] == &monster) {
			bActive = true;
			break;
		}
	}

	EventPlrMsg(StrCat("Active List = ", bActive ? 1 : 0, ", Squelch = ", monster.activeForTicks), UiFlags::ColorWhite);
}

std::optional<std::string> RejectAuthoritativeDebugMutation(std::string_view command)
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacyDebugMutation(authoritative::GetServerBackedRuntime().IsAuthoritativeMode()))
		return StrCat(command, " is unavailable in authoritative mode.");
#else
	(void)command;
#endif
	return std::nullopt;
}

} // namespace

std::string DebugCmdGodMode(std::optional<bool> on)
{
	if (const auto error = RejectAuthoritativeDebugMutation("God mode"); error.has_value())
		return *error;
	DebugGodMode = on.value_or(!DebugGodMode);
	return StrCat("God mode: ", DebugGodMode ? "On" : "Off");
}

std::string DebugCmdInvisible(std::optional<bool> on)
{
	if (const auto error = RejectAuthoritativeDebugMutation("Invisibility"); error.has_value())
		return *error;
	DebugInvisible = on.value_or(!DebugInvisible);
	return StrCat("Invisible: ", DebugInvisible ? "On" : "Off");
}

std::string DebugCmdArrow(std::string_view effect)
{
	if (const auto error = RejectAuthoritativeDebugMutation("Arrow effects"); error.has_value())
		return *error;
	Player &myPlayer = *MyPlayer;

	myPlayer._pIFlags &= ~ItemSpecialEffect::FireArrows;
	myPlayer._pIFlags &= ~ItemSpecialEffect::LightningArrows;

	if (effect == "normal") {
		// The flags were cleared above.
	} else if (effect == "fire") {
		myPlayer._pIFlags |= ItemSpecialEffect::FireArrows;
	} else if (effect == "lightning") {
		myPlayer._pIFlags |= ItemSpecialEffect::LightningArrows;
	} else if (effect == "spectral") {
		myPlayer._pIFlags |= (ItemSpecialEffect::FireArrows | ItemSpecialEffect::LightningArrows);
	} else {
		return "Invalid effect!";
	}

	return StrCat("Arrows changed to: ", effect);
}

std::string DebugCmdPlayerInfo(std::optional<uint8_t> id)
{
	const uint8_t playerId = id.value_or(0);
	if (playerId >= Players.size())
		return StrCat("Invalid player ID (max: ", Players.size() - 1, ")");
	Player &player = Players[playerId];
	if (!player.plractive)
		return StrCat("Player ", playerId, " is not active!");

	const Point target = player.GetTargetPosition();
	return StrCat("Plr ", playerId, " is ", player._pName,
	    "\nLvl: ", player.plrlevel, " Changing: ", player._pLvlChanging,
	    "\nTile.x: ", player.position.tile.x, " Tile.y: ", player.position.tile.y, " Target.x: ", target.x, " Target.y: ", target.y,
	    "\nMode: ", player._pmode, " destAction: ", player.destAction, " walkpath[0]: ", player.walkpath[0],
	    "\nInvincible: ", player._pInvincible ? 1 : 0, " HitPoints: ", player.life.current);
}

std::string DebugCmdRejuvenate()
{
	if (const auto error = RejectAuthoritativeDebugMutation("Player rejuvenation"); error.has_value())
		return *error;
	Player &myPlayer = *MyPlayer;
	myPlayer.RestoreFullLife();
	myPlayer.RestoreFullMana();
	RedrawComponent(PanelDrawComponent::Health);
	RedrawComponent(PanelDrawComponent::Mana);
	return "Restored life and mana to full.";
}

std::string DebugCmdChangeHealth(int change)
{
	if (const auto error = RejectAuthoritativeDebugMutation("Player health adjustment"); error.has_value())
		return *error;
	Player &myPlayer = *MyPlayer;
	if (change == 0)
		return StrCat("Enter a value not equal to 0 to change life!");

	const int newHealth = myPlayer.life.current + (change * 64);
	myPlayer.setHitPoints(newHealth);
	if (newHealth <= 0)
		myPlayer.syncKill(DeathReason::MonsterOrTrap);

	return StrCat("Changed life by ", change);
}

std::string DebugCmdChangeMana(int change)
{
	if (const auto error = RejectAuthoritativeDebugMutation("Player mana adjustment"); error.has_value())
		return *error;
	Player &myPlayer = *MyPlayer;
	if (change == 0)
		return StrCat("Enter a value not equal to 0 to change mana!");

	myPlayer.mana.current += change * 64;
	myPlayer.mana.base = myPlayer.mana.current + myPlayer.mana.maximumBase - myPlayer.mana.maximum;
	RedrawComponent(PanelDrawComponent::Mana);

	return StrCat("Changed mana by ", change);
}

std::string DebugCmdMaxStats()
{
	if (const auto error = RejectAuthoritativeDebugMutation("Maximum attributes"); error.has_value())
		return *error;
	Player &myPlayer = *MyPlayer;
	myPlayer.modifyStrength(myPlayer.GetMaximumAttributeValue(CharacterAttribute::Strength) - myPlayer.attributes.strength.base);
	myPlayer.modifyMagic(myPlayer.GetMaximumAttributeValue(CharacterAttribute::Magic) - myPlayer.attributes.magic.base);
	myPlayer.modifyDexterity(myPlayer.GetMaximumAttributeValue(CharacterAttribute::Dexterity) - myPlayer.attributes.dexterity.base);
	myPlayer.modifyVitality(myPlayer.GetMaximumAttributeValue(CharacterAttribute::Vitality) - myPlayer.attributes.vitality.base);
	return "Set all character base attributes to maximum.";
}

std::string DebugCmdMinStats()
{
	if (const auto error = RejectAuthoritativeDebugMutation("Minimum attributes"); error.has_value())
		return *error;
	Player &myPlayer = *MyPlayer;
	myPlayer.modifyStrength(-myPlayer.attributes.strength.base);
	myPlayer.modifyMagic(-myPlayer.attributes.magic.base);
	myPlayer.modifyDexterity(-myPlayer.attributes.dexterity.base);
	myPlayer.modifyVitality(-myPlayer.attributes.vitality.base);
	return "Set all character base attributes to minimum.";
}

std::string DebugCmdLevelUp(int levels)
{
	if (const auto error = RejectAuthoritativeDebugMutation("Level-up"); error.has_value())
		return *error;
	if (levels <= 0)
		return "amount must be positive";
	Player &myPlayer = *MyPlayer;
	for (int i = 0; i < levels; i++)
		NetSendCmd(true, CMD_CHEAT_EXPERIENCE);
	return StrCat("New character level: ", myPlayer.getCharacterLevel() + levels);
}

std::string DebugCmdGiveGoldCheat(int amount)
{
	if (const auto error = RejectAuthoritativeDebugMutation("Gold adjustment"); error.has_value())
		return *error;
	if (amount <= 0)
		return "amount must be positive";
	Player &myPlayer = *MyPlayer;
	int goldToAdd = amount;
	const int goldAmountBefore = myPlayer._pGold;
	for (int8_t &itemIndex : myPlayer.InvGrid) {
		if (itemIndex < 0)
			continue;

		Item &item = myPlayer.InvList[itemIndex != 0 ? itemIndex - 1 : myPlayer._pNumInv];
		if (itemIndex != 0) {
			if ((!item.isGold() && !item.isEmpty()) || (item.isGold() && item._ivalue == GOLD_MAX_LIMIT))
				continue;
		} else if (item.isEmpty()) {
			MakeGoldStack(item, 0);
			myPlayer._pNumInv++;
			itemIndex = myPlayer._pNumInv;
		}

		const int goldThatCanBeAdded = GOLD_MAX_LIMIT - item._ivalue;
		if (goldThatCanBeAdded >= goldToAdd) {
			item._ivalue += goldToAdd;
			myPlayer._pGold += goldToAdd;
			break;
		}

		item._ivalue += goldThatCanBeAdded;
		goldToAdd -= goldThatCanBeAdded;
		myPlayer._pGold += goldThatCanBeAdded;
	}

	CalcPlrInv(myPlayer, true);
	return StrCat("Set your gold to ", myPlayer._pGold, ", added ", myPlayer._pGold - goldAmountBefore, ".");
}

std::string DebugCmdTakeGoldCheat(int amount)
{
	if (const auto error = RejectAuthoritativeDebugMutation("Gold adjustment"); error.has_value())
		return *error;
	if (amount <= 0)
		return "amount must be positive";
	Player &myPlayer = *MyPlayer;
	int goldToRemove = amount;
	const int goldAmountBefore = myPlayer._pGold;
	for (auto itemIndex : myPlayer.InvGrid) {
		itemIndex -= 1;
		if (itemIndex < 0)
			continue;

		Item &item = myPlayer.InvList[itemIndex];
		if (!item.isGold())
			continue;
		if (item._ivalue >= goldToRemove) {
			myPlayer._pGold -= goldToRemove;
			item._ivalue -= goldToRemove;
			if (item._ivalue == 0)
				myPlayer.RemoveInvItem(itemIndex);
			break;
		}

		myPlayer._pGold -= item._ivalue;
		goldToRemove -= item._ivalue;
		myPlayer.RemoveInvItem(itemIndex);
	}

	return StrCat("Set your gold to ", myPlayer._pGold, ", removed ", goldAmountBefore - myPlayer._pGold, ".");
}

std::string DebugCmdSetSpellsLevel(uint8_t level)
{
	if (const auto error = RejectAuthoritativeDebugMutation("Spell-level adjustment"); error.has_value())
		return *error;
	for (auto i = static_cast<uint8_t>(SpellID::Firebolt); i < SpellsData.size(); i++) {
		if (GetSpellBookLevel(static_cast<SpellID>(i)) != -1)
			NetSendCmdParam2(true, CMD_CHANGE_SPELL_LEVEL, i, level);
	}
	if (level == 0)
		MyPlayer->_pMemSpells = 0;

	return StrCat("Set all spell levels to ", level);
}

std::string DebugCmdEnableQuest(uint8_t questId)
{
	if (const auto error = RejectAuthoritativeDebugMutation("Quest activation"); error.has_value())
		return *error;
	if (questId >= MAXQUESTS)
		return StrCat("Quest ", questId, " does not exist!");
	Quest &quest = Quests[questId];
	if (IsNoneOf(quest._qactive, QUEST_NOTAVAIL, QUEST_INIT))
		return StrCat(QuestsData[questId]._qlstr, " is already active!");
	quest._qactive = QUEST_ACTIVE;
	quest._qlog = true;
	return StrCat(QuestsData[questId]._qlstr, " activated.");
}

std::string DebugCmdEnableQuests()
{
	if (const auto error = RejectAuthoritativeDebugMutation("Quest activation"); error.has_value())
		return *error;
	for (Quest &quest : Quests) {
		if (IsNoneOf(quest._qactive, QUEST_NOTAVAIL, QUEST_INIT))
			continue;
		quest._qactive = QUEST_ACTIVE;
		quest._qlog = true;
	}
	return "Activated all quests.";
}

std::string DebugCmdQuestInfo(uint8_t questId)
{
	if (questId >= MAXQUESTS)
		return StrCat("Quest ", questId, " does not exist!");
	const Quest &quest = Quests[questId];
	return StrCat("Quest id=", quest._qidx, " ", QuestsData[quest._qidx]._qlstr,
	    " active=", quest._qactive, " var1=", quest._qvar1, " var2=", quest._qvar2);
}

std::string DebugCmdQuestsInfo()
{
	std::string result;
	for (const Quest &quest : Quests)
		StrAppend(result, "Quest id=", quest._qidx, " ", QuestsData[quest._qidx]._qlstr,
		    " active=", quest._qactive, " var1=", quest._qvar1, " var2=", quest._qvar2, "\n");
	if (!result.empty())
		result.pop_back();
	return result;
}

std::string DebugCmdSetPlayerTrn(std::string_view path)
{
	if (!path.empty()) {
		const AssetRef ref = FindAsset(path);
		if (!ref.ok()) {
			const char *error = ref.error();
			return error == nullptr || *error == '\0' ? StrCat("File not found: ", path) : error;
		}
	}
	debugTRN = path;
	Player &player = *MyPlayer;
	player.initGraphics();
	player.startStand(player.direction);
	return path.empty() ? "TRN unset" : "TRN set";
}

std::string DebugCmdShowGrid(std::optional<bool> on)
{
	DebugGrid = on.value_or(!DebugGrid);
	return StrCat("Tile grid highlighting: ", DebugGrid ? "On" : "Off");
}

std::string DebugCmdVision(std::optional<bool> on)
{
	DebugVision = on.value_or(!DebugVision);
	return StrCat("Vision highlighting: ", DebugVision ? "On" : "Off");
}

std::string DebugCmdPath(std::optional<bool> on)
{
	DebugPath = on.value_or(!DebugPath);
	return StrCat("Path highlighting: ", DebugPath ? "On" : "Off");
}

std::string DebugCmdFullbright(std::optional<bool> /*on*/)
{
	CurrentLightManager.ToggleLighting();
	return StrCat("Fullbright: ", CurrentLightManager.disableLighting_ ? "On" : "Off");
}

std::string DebugCmdToggleFPS(std::optional<bool> on)
{
	frameflag = on.value_or(!frameflag);
	return StrCat("FPS counter: ", frameflag ? "On" : "Off");
}

std::string DebugCmdMapReveal()
{
	for (int x = 0; x < DMAXX; x++)
		for (int y = 0; y < DMAXY; y++)
			CurrentAutomapManager.UpdateAutomapExplorer({ x, y }, MAP_EXP_SHRINE);
	return "Automap fully explored.";
}

std::string DebugCmdMapHide()
{
	for (int x = 0; x < DMAXX; x++)
		for (int y = 0; y < DMAXY; y++)
			CurrentAutomapManager.GetAutomapView()[x][y] = MAP_EXP_NONE;
	return "Automap exploration removed.";
}

std::string DebugCmdResetLevel(uint8_t level, std::optional<int> seed)
{
	if (const auto error = RejectAuthoritativeDebugMutation("Level reset"); error.has_value())
		return *error;
	Player &myPlayer = *MyPlayer;
	if (level > (gbIsHellfire ? 24 : 16))
		return StrCat("Level ", level, " does not exist!");
	if (myPlayer.isOnLevel(level))
		return "Unable to reset dungeon levels occupied by players!";

	myPlayer._pLvlVisited[level] = false;
	DeltaClearLevel(level);

	if (seed.has_value()) {
		DungeonSeeds[level] = *seed;
		LevelSeeds[level] = std::nullopt;
		return StrCat("Successfully reset level ", level, " with seed ", *seed, ".");
	}
	return StrCat("Successfully reset level ", level, ".");
}

std::string DebugCmdWarpToDungeonLevel(uint8_t level)
{
	if (const auto error = RejectAuthoritativeDebugMutation("Dungeon warp"); error.has_value())
		return *error;
	Player &myPlayer = *MyPlayer;
	if (level > (gbIsHellfire ? 24 : 16))
		return StrCat("Level ", level, " does not exist!");
	if (!isSetLevel() && myPlayer.isOnLevel(level))
		return StrCat("You are already on level ", level, "!");

	StartNewLvl(myPlayer, (level != 21) ? interface_mode::WM_DIABNEXTLVL : interface_mode::WM_DIABTOWNWARP, level);
	return StrCat("Moved you to level ", level, ".");
}

std::string DebugCmdWarpToQuestLevel(uint8_t level)
{
	if (const auto error = RejectAuthoritativeDebugMutation("Quest warp"); error.has_value())
		return *error;
	if (level < 1)
		return StrCat("Quest level number must be 1 or higher!");
	if (isSetLevel() && setLevelNumber() == level)
		return StrCat("You are already on quest level", level, "!");

	for (Quest &quest : Quests) {
		if (level != quest._qslvl)
			continue;

		setLevelType() = quest._qlvltype;
		StartNewLvl(*MyPlayer, WM_DIABSETLVL, level);

		return StrCat("Moved you to quest level ", SetLevelNames[level], ".");
	}

	return StrCat("Quest level ", level, " does not exist!");
}

std::string DebugCmdWarpToCustomMap(std::string_view path, int dunType, int x, int y)
{
	if (const auto error = RejectAuthoritativeDebugMutation("Custom-map warp"); error.has_value())
		return *error;
	if (path.empty()) return "path is required";
	if (dunType < DTYPE_CATHEDRAL || dunType > DTYPE_LAST) return "invalid dunType";

	const Point spawn { x, y };
	if (!InDungeonBounds(spawn)) return "spawn location is out of bounds";

	TestMapPath = StrCat(path, ".dun");
	setLevelType() = static_cast<dungeon_type>(dunType);
	viewPosition() = spawn;

	StartNewLvl(*MyPlayer, WM_DIABSETLVL, SL_NONE);

	return StrCat("Moved you to ", TestMapPath, ".");
}

std::string DebugCmdExportDun()
{
	const std::string levelName = StrCat(currentLevelNumber(), "-", DungeonSeeds[currentLevelNumber()], ".dun");
	FILE *dunFile = OpenFile(levelName.c_str(), "ab");

	WriteLE16(dunFile, DMAXX);
	WriteLE16(dunFile, DMAXY);

	/** Tiles. */
	for (int y = 0; y < DMAXY; y++) {
		for (int x = 0; x < DMAXX; x++) {
			WriteLE16(dunFile, megaTileAt(x, y).current());
		}
	}

	/** Padding */
	for (int y = 16; y < MAXDUNY - 16; y++) {
		for (int x = 16; x < MAXDUNX - 16; x++) {
			WriteLE16(dunFile, 0);
		}
	}

	/** Monsters */
	for (int y = 16; y < MAXDUNY - 16; y++) {
		for (int x = 16; x < MAXDUNX - 16; x++) {
			uint16_t monsterId = 0;
			const int16_t activeMonsterId = tileAt(x, y).monster();
			if (activeMonsterId > 0) {
				for (int i = 0; i < 157; i++) {
					if (MonstConvTbl[i] == Monsters[std::abs(activeMonsterId) - 1].type().type) {
						monsterId = i + 1;
						break;
					}
				}
			}
			WriteLE16(dunFile, monsterId);
		}
	}

	/** Objects */
	for (int y = 16; y < MAXDUNY - 16; y++) {
		for (int x = 16; x < MAXDUNX - 16; x++) {
			uint16_t objectId = 0;
			Object *object = FindObjectAtPosition({ x, y }, false);
			if (object != nullptr) {
				for (int i = 0; i < 147; i++) {
					if (ObjTypeConv[i] == object->_otype) {
						objectId = i;
						break;
					}
				}
			}
			WriteLE16(dunFile, objectId);
		}
	}

	/** Transparency */
	for (int y = 16; y < MAXDUNY - 16; y++) {
		for (int x = 16; x < MAXDUNX - 16; x++) {
			WriteLE16(dunFile, tileAt(x, y).transVal());
		}
	}
	std::fclose(dunFile);

	return StrCat("Successfully exported ", levelName, ".");
}

std::string DebugCmdSpawnItem(std::string itemName)
{
	if (const auto error = RejectAuthoritativeDebugMutation("Item spawning"); error.has_value())
		return *error;
	if (ActiveItemCount >= MAXITEMS) return "No space to generate the item!";

	const int max_time = 3000;
	const int max_iter = 1000000;

	AsciiStrToLower(itemName);

	Item testItem;

	uint32_t begin = SDL_GetTicks();
	int i = 0;
	for (;; i++) {
		// using a better rng here to seed the item to prevent getting stuck repeating same values using old one
		std::uniform_int_distribution<int32_t> dist(0, INT_MAX);
		SetRndSeed(dist(BetterRng));
		if (SDL_GetTicks() - begin > max_time)
			return StrCat("Item not found in ", max_time / 1000, " seconds!");

		if (i > max_iter)
			return StrCat("Item not found in ", max_iter, " tries!");

		const int8_t monsterLevel = (dist(BetterRng) % CF_LEVEL) + 1;
		_item_indexes idx = RndItemForMonsterLevel(monsterLevel);
		if (IsAnyOf(idx, IDI_NONE, IDI_GOLD))
			continue;

		testItem = {};
		SetupAllItems(*MyPlayer, testItem, idx, AdvanceRndSeed(), monsterLevel, 1, false, false);
		TryRandomUniqueItem(testItem, idx, monsterLevel, 1, false, false);
		SetupItem(testItem);

		std::string tmp = AsciiStrToLower(testItem._iIName);
		if (tmp.find(itemName) != std::string::npos)
			break;
	}

	int ii = AllocateItem();
	auto &item = Items[ii];
	item = testItem.pop();
	item._iIdentified = true;
	Point pos = MyPlayer->position.tile;
	GetSuperItemSpace(pos, ii);
	NetSendCmdPItem(false, CMD_SPAWNITEM, item.position, item);
	return StrCat("Item generated successfully - iterations: ", i);
}

std::string DebugCmdSpawnUniqueItem(std::string itemName)
{
	if (const auto error = RejectAuthoritativeDebugMutation("Unique-item spawning"); error.has_value())
		return *error;
	if (ActiveItemCount >= MAXITEMS) return "No space to generate the item!";

	AsciiStrToLower(itemName);
	UniqueItem uniqueItem;
	bool foundUnique = false;
	int uniqueIndex = 0;
	for (const auto &item : UniqueItems) {
		const std::string tmp = AsciiStrToLower(std::string_view(item.UIName));
		if (tmp.find(itemName) != std::string::npos) {
			itemName = tmp;
			uniqueItem = item;
			foundUnique = true;
			break;
		}
		++uniqueIndex;
	}
	if (!foundUnique) return "No unique item found!";

	_item_indexes uniqueBaseIndex = IDI_GOLD;
	for (size_t j = 0; j < AllItemsList.size(); j++) {
		if (!IsItemAvailable(static_cast<int>(j)))
			continue;
		if (AllItemsList[j].iItemId == uniqueItem.UIItemId) {
			uniqueBaseIndex = static_cast<_item_indexes>(j);
			break;
		}
	}

	if (uniqueBaseIndex == IDI_GOLD) return "Base item not available!";

	auto &baseItemData = AllItemsList[static_cast<size_t>(uniqueBaseIndex)];

	Item testItem;

	int i = 0;
	for (uint32_t begin = SDL_GetTicks();; i++) {
		constexpr int max_time = 3000;
		if (SDL_GetTicks() - begin > max_time)
			return StrCat("Item not found in ", max_time / 1000, " seconds!");

		constexpr int max_iter = 1000000;
		if (i > max_iter)
			return StrCat("Item not found in ", max_iter, " tries!");

		testItem = {};
		testItem._iMiscId = baseItemData.iMiscId;
		std::uniform_int_distribution<int32_t> dist(0, INT_MAX);
		SetRndSeed(dist(BetterRng));
		for (auto &flag : UniqueItemFlags)
			flag = true;
		UniqueItemFlags[uniqueIndex] = false;
		SetupAllItems(*MyPlayer, testItem, uniqueBaseIndex, testItem._iMiscId == IMISC_UNIQUE ? uniqueIndex : AdvanceRndSeed(), uniqueItem.UIMinLvl, 1, false, false);
		TryRandomUniqueItem(testItem, uniqueBaseIndex, uniqueItem.UIMinLvl, 1, false, false);
		SetupItem(testItem);
		for (auto &flag : UniqueItemFlags)
			flag = false;

		if (testItem._iMagical != ITEM_QUALITY_UNIQUE)
			continue;

		const std::string tmp = AsciiStrToLower(testItem._iIName);
		if (tmp.find(itemName) != std::string::npos)
			break;
	}

	int ii = AllocateItem();
	auto &item = Items[ii];
	item = testItem.pop();
	Point pos = MyPlayer->position.tile;
	GetSuperItemSpace(pos, ii);
	item._iIdentified = true;
	NetSendCmdPItem(false, CMD_SPAWNITEM, item.position, item);

	return StrCat("Item generated successfully - iterations: ", i);
}

std::string DebugCmdSpawnUniqueMonster(std::string name, std::optional<unsigned> countOpt)
{
	if (const auto error = RejectAuthoritativeDebugMutation("Unique-monster spawning"); error.has_value())
		return *error;
	if (levelType() == DTYPE_TOWN) return "Can't spawn monsters in town";
	if (name.empty()) return "name is required";
	const unsigned count = countOpt.value_or(1);
	if (count < 1) return "count must be positive";

	AsciiStrToLower(name);

	int mtype = -1;
	UniqueMonsterType uniqueIndex = UniqueMonsterType::None;
	for (size_t i = 0; i < UniqueMonstersData.size(); ++i) {
		const auto &mondata = UniqueMonstersData[i];
		const std::string monsterName = AsciiStrToLower(std::string_view(mondata.mName));
		if (monsterName.find(name) == std::string::npos)
			continue;
		mtype = mondata.mtype;
		uniqueIndex = static_cast<UniqueMonsterType>(i);
		if (monsterName == name)
			break;
	}

	if (mtype == -1) return "Monster not found";

	size_t id = MaxLvlMTypes - 1;
	bool found = false;

	for (size_t i = 0; i < LevelBestiary.size(); i++) {
		if (LevelBestiary[i].type == mtype) {
			id = i;
			found = true;
			break;
		}
	}

	if (!found) {
		if (LevelBestiary.size() == MaxLvlMTypes)
			LevelBestiary.decrementSize();
		tl::expected<size_t, std::string> idResult = AddMonsterType(uniqueIndex, PLACE_SCATTER);
		if (!idResult.has_value()) return std::move(idResult).error();
		id = idResult.value();
		CMonster &monsterType = LevelBestiary[id];
		Bestiary::initGraphics(monsterType);
		monsterType.corpseId = 1;
	}

	Player &myPlayer = *MyPlayer;

	unsigned spawnedMonster = 0;

	auto ret = Crawl(0, MaxCrawlRadius, [&](Displacement displacement) -> std::optional<std::string> {
		Point pos = myPlayer.position.tile + displacement;
		const Tile &tile = tileAt(pos);
		if (tile.hasPlayer() || tile.hasMonster())
			return {};
		if (!IsTileWalkable(pos))
			return {};

		Monster *monster = AddMonster(pos, myPlayer.direction, id, true);
		if (monster == nullptr)
			return StrCat("Spawned ", spawnedMonster, " monsters. (Unable to spawn more)");
		PrepareUniqueMonst(*monster, uniqueIndex, 0, 0, UniqueMonstersData[static_cast<size_t>(uniqueIndex)]);
		monster->corpseId = 1;
		spawnedMonster += 1;

		if (spawnedMonster >= count)
			return StrCat("Spawned ", spawnedMonster, " monsters.");

		return {};
	});

	if (!ret.has_value())
		ret = StrCat("Spawned ", spawnedMonster, " monsters. (Unable to spawn more)");
	return *ret;
}

std::string DebugCmdSpawnMonster(std::string name, std::optional<unsigned> countOpt)
{
	if (const auto error = RejectAuthoritativeDebugMutation("Monster spawning"); error.has_value())
		return *error;
	if (levelType() == DTYPE_TOWN) return "Can't spawn monsters in town";
	if (name.empty()) return "name is required";
	const unsigned count = countOpt.value_or(1);
	if (count < 1) return "count must be positive";

	AsciiStrToLower(name);

	int mtype = -1;

	for (size_t i = 0; i < MonstersData.size(); i++) {
		const auto &mondata = MonstersData[i];
		const std::string monsterName = AsciiStrToLower(std::string_view(mondata.name));
		if (monsterName.find(name) == std::string::npos)
			continue;
		mtype = static_cast<int>(i);
		if (monsterName == name)
			break;
	}

	if (mtype == -1) return "Monster not found";
	if (!MyPlayer->isLevelOwnedByLocalClient()) return "You are not the level owner.";

	size_t id = MaxLvlMTypes - 1;
	bool found = false;

	for (size_t i = 0; i < LevelBestiary.size(); i++) {
		if (LevelBestiary[i].type == mtype) {
			id = i;
			found = true;
			break;
		}
	}

	if (!found) {
		if (LevelBestiary.size() == MaxLvlMTypes)
			LevelBestiary.decrementSize();
		tl::expected<size_t, std::string> idResult = AddMonsterType(static_cast<_monster_id>(mtype), PLACE_SCATTER);
		if (!idResult.has_value()) return std::move(idResult).error();
		id = idResult.value();
		CMonster &monsterType = LevelBestiary[id];
		Bestiary::initGraphics(monsterType);
		monsterType.corpseId = 1;
	}

	Player &myPlayer = *MyPlayer;

	size_t monstersToSpawn = std::min<size_t>(MaxMonsters - MonsterPoolAdapter::ActiveMonsterCountValue(), count);
	if (monstersToSpawn == 0)
		return "Can't spawn any monsters";

	size_t spawnedMonster = 0;
	Crawl(0, MaxCrawlRadius, [&](Displacement displacement) {
		Point pos = myPlayer.position.tile + displacement;
		const Tile &tile = tileAt(pos);
		if (tile.hasPlayer() || tile.hasMonster())
			return false;
		if (!IsTileWalkable(pos))
			return false;

		SpawnMonster(pos, myPlayer.direction, id);
		spawnedMonster += 1;

		return spawnedMonster == monstersToSpawn;
	});

	if (monstersToSpawn != count)
		return StrCat("Spawned ", spawnedMonster, " monsters. (Unable to spawn more)");
	return StrCat("Spawned ", spawnedMonster, " monsters.");
}

const Item *DebugCmdGetSelectedItem()
{
	const Player &myPlayer = *MyPlayer;
	if (!myPlayer.HoldItem.isEmpty()) return &myPlayer.HoldItem;
	if (pcursinvitem != -1) {
		if (pcursinvitem < INVITEM_INV_FIRST) return &myPlayer.InvBody[pcursinvitem];
		if (pcursinvitem <= INVITEM_INV_LAST) return &myPlayer.InvList[pcursinvitem - INVITEM_INV_FIRST];
		return &myPlayer.SpdList[pcursinvitem - INVITEM_BELT_FIRST];
	}
	if (pcursitem != -1) return &Items[pcursitem];
	return nullptr;
}

std::string DebugCmdItemInfo()
{
	const Item *pItem = DebugCmdGetSelectedItem();
	if (pItem != nullptr) {
		const Player &myPlayer = *MyPlayer;
		std::string_view netPackValidation { "N/A" };
		if (gbIsMultiplayer) {
			ItemNetPack itemPack;
			Item unpacked;
			PackNetItem(*pItem, itemPack);
			netPackValidation = UnPackNetItem(myPlayer, itemPack, unpacked) ? "Success" : "Failure";
		}
		return StrCat("Name: ", pItem->_iIName,
		    "\nIDidx: ", pItem->IDidx, " (", AllItemsList[pItem->IDidx].iName, ")",
		    "\nSeed: ", pItem->_iSeed,
		    "\nCreateInfo: ", pItem->_iCreateInfo,
		    "\nLevel: ", pItem->_iCreateInfo & CF_LEVEL,
		    "\nOnly Good: ", ((pItem->_iCreateInfo & CF_ONLYGOOD) == 0) ? "False" : "True",
		    "\nUnique Monster: ", ((pItem->_iCreateInfo & CF_UPER15) == 0) ? "False" : "True",
		    "\nDungeon Item: ", ((pItem->_iCreateInfo & CF_UPER1) == 0) ? "False" : "True",
		    "\nUnique Item: ", ((pItem->_iCreateInfo & CF_UNIQUE) == 0) ? "False" : "True",
		    "\nSmith: ", ((pItem->_iCreateInfo & CF_SMITH) == 0) ? "False" : "True",
		    "\nSmith Premium: ", ((pItem->_iCreateInfo & CF_SMITHPREMIUM) == 0) ? "False" : "True",
		    "\nBoy: ", ((pItem->_iCreateInfo & CF_BOY) == 0) ? "False" : "True",
		    "\nWitch: ", ((pItem->_iCreateInfo & CF_WITCH) == 0) ? "False" : "True",
		    "\nHealer: ", ((pItem->_iCreateInfo & CF_HEALER) == 0) ? "False" : "True",
		    "\nPregen: ", ((pItem->_iCreateInfo & CF_PREGEN) == 0) ? "False" : "True",
		    "\nNet Validation: ", netPackValidation);
	}
	return StrCat("Num items: ", ActiveItemCount);
}

std::string DebugCmdVisitTowner(std::string_view name)
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (authoritative::GetServerBackedRuntime().IsAuthoritativeMode())
		return StrCat("Towner travel is unavailable in authoritative mode.");
#endif

	Player &myPlayer = *MyPlayer;

	if (isSetLevel() || !myPlayer.isOnLevel(0))
		return StrCat("This command is only available in Town!");

	if (name.empty()) {
		std::string ret = StrCat("Please provide the name of a Towner: ");
		for (const auto &[townerName, _] : TownerShortNameToTownerId) {
			ret += ' ';
			ret.append(townerName);
		}
		return ret;
	}

	const auto it = TownerShortNameToTownerId.find(name);
	if (it == TownerShortNameToTownerId.end())
		return StrCat(name, " is invalid!");

	for (const Towner &towner : Towners) {
		if (towner._ttype != it->second)
			continue;
		CastSpell(myPlayer, SpellID::Teleport, myPlayer.position.tile, towner.position, /*spllvl=*/1);
		return StrCat("Moved you to ", name, ".");
	}

	return StrCat("Unable to locate ", name, "!");
}

std::string DebugCmdTalkToTowner(std::string_view name)
{
	if (name.empty()) {
		std::string ret = StrCat("Please provide the name of a Towner: ");
		for (const auto &[townerName, _] : TownerShortNameToTownerId) {
			ret += ' ';
			ret.append(townerName);
		}
		return ret;
	}

	const auto it = TownerShortNameToTownerId.find(name);
	if (it == TownerShortNameToTownerId.end())
		return StrCat(name, " is invalid!");

	if (!DebugTalkToTowner(it->second))
		return StrCat("Towner not found!");
	return StrCat("Opened ", name, " talk window.");
}

void RegisterNativeDebugCommands()
{
	static bool registered = false;
	if (registered)
		return;
	registered = true;

	const auto registerToggle = [](std::string name, std::string description, auto handler) {
		CurrentDebugCommandRegistry.Register(
		    std::move(name),
		    std::move(description),
		    [handler](std::string_view arguments) {
			    if (HasInvalidBoolArgument(arguments))
				    return std::string("Expected an optional boolean: on, off, true, false, 1, or 0.");
			    return handler(ParseOptionalBool(arguments));
		    });
	};

	registerToggle("debug.player.god", "Toggle player god mode.", DebugCmdGodMode);
	registerToggle("debug.player.invisible", "Toggle player invisibility.", DebugCmdInvisible);
	CurrentDebugCommandRegistry.Register("debug.player.arrow", "Set the player arrow effect.", DebugCmdArrow);
	CurrentDebugCommandRegistry.Register("debug.player.info", "Show player diagnostics.", [](std::string_view arguments) {
		if (HasInvalidLevelArgument(arguments))
			return std::string("Expected an optional player ID.");
		return DebugCmdPlayerInfo(ParseOptionalLevel(arguments));
	});
	CurrentDebugCommandRegistry.Register("debug.player.rejuvenate", "Restore player life and mana to full.", [](std::string_view arguments) {
		return arguments.empty() ? DebugCmdRejuvenate() : std::string("This command takes no arguments.");
	});
	CurrentDebugCommandRegistry.Register("debug.player.health", "Adjust player life by a whole-point amount.", [](std::string_view arguments) {
		const auto amount = ParseRequiredInt(arguments);
		return amount.has_value() ? DebugCmdChangeHealth(*amount) : std::string("Expected an integer life amount.");
	});
	CurrentDebugCommandRegistry.Register("debug.player.mana", "Adjust player mana by a whole-point amount.", [](std::string_view arguments) {
		const auto amount = ParseRequiredInt(arguments);
		return amount.has_value() ? DebugCmdChangeMana(*amount) : std::string("Expected an integer mana amount.");
	});
	CurrentDebugCommandRegistry.Register("debug.player.attributes.max", "Set all character base attributes to maximum.", [](std::string_view arguments) {
		return arguments.empty() ? DebugCmdMaxStats() : std::string("This command takes no arguments.");
	});
	CurrentDebugCommandRegistry.Register("debug.player.attributes.min", "Set all character base attributes to minimum.", [](std::string_view arguments) {
		return arguments.empty() ? DebugCmdMinStats() : std::string("This command takes no arguments.");
	});
	CurrentDebugCommandRegistry.Register("debug.player.levelUp", "Level the player up by a number of levels.", [](std::string_view arguments) {
		if (arguments.empty())
			return DebugCmdLevelUp(1);
		const auto amount = ParseRequiredInt(arguments);
		return amount.has_value() ? DebugCmdLevelUp(*amount) : std::string("Expected an integer level amount.");
	});
	CurrentDebugCommandRegistry.Register("debug.player.gold.give", "Give the player gold.", [](std::string_view arguments) {
		if (arguments.empty())
			return DebugCmdGiveGoldCheat(GOLD_MAX_LIMIT * InventoryGridCells);
		const auto amount = ParseRequiredInt(arguments);
		return amount.has_value() ? DebugCmdGiveGoldCheat(*amount) : std::string("Expected an integer gold amount.");
	});
	CurrentDebugCommandRegistry.Register("debug.player.gold.take", "Take gold from the player.", [](std::string_view arguments) {
		if (arguments.empty())
			return DebugCmdTakeGoldCheat(GOLD_MAX_LIMIT * InventoryGridCells);
		const auto amount = ParseRequiredInt(arguments);
		return amount.has_value() ? DebugCmdTakeGoldCheat(*amount) : std::string("Expected an integer gold amount.");
	});
	CurrentDebugCommandRegistry.Register("debug.player.spells.setLevel", "Set all learned spell levels.", [](std::string_view arguments) {
		const auto amount = ParseRequiredInt(arguments);
		if (!amount.has_value() || *amount < 0 || *amount > UINT8_MAX)
			return std::string("Expected a spell level from 0 to 255.");
		return DebugCmdSetSpellsLevel(static_cast<uint8_t>(*amount));
	});
	CurrentDebugCommandRegistry.Register("debug.quests.activate", "Activate a quest by ID.", [](std::string_view arguments) {
		if (HasInvalidLevelArgument(arguments) || arguments.empty())
			return std::string("Expected a quest ID.");
		return DebugCmdEnableQuest(*ParseOptionalLevel(arguments));
	});
	CurrentDebugCommandRegistry.Register("debug.quests.activateAll", "Activate all available quests.", [](std::string_view arguments) {
		return arguments.empty() ? DebugCmdEnableQuests() : std::string("This command takes no arguments.");
	});
	CurrentDebugCommandRegistry.Register("debug.quests.info", "Show quest information by ID.", [](std::string_view arguments) {
		if (HasInvalidLevelArgument(arguments) || arguments.empty())
			return std::string("Expected a quest ID.");
		return DebugCmdQuestInfo(*ParseOptionalLevel(arguments));
	});
	CurrentDebugCommandRegistry.Register("debug.quests.all", "Show all quest information.", [](std::string_view arguments) {
		return arguments.empty() ? DebugCmdQuestsInfo() : std::string("This command takes no arguments.");
	});
	CurrentDebugCommandRegistry.Register("debug.player.trn", "Set or clear the player TRN asset.", [](std::string_view arguments) {
		return DebugCmdSetPlayerTrn(arguments == "clear" ? std::string_view {} : arguments);
	});
	registerToggle("debug.display.grid", "Toggle tile-grid highlighting.", DebugCmdShowGrid);
	registerToggle("debug.display.vision", "Toggle vision highlighting.", DebugCmdVision);
	registerToggle("debug.display.path", "Toggle path highlighting.", DebugCmdPath);
	registerToggle("debug.display.fps", "Toggle the FPS counter.", DebugCmdToggleFPS);
	registerToggle("debug.display.scrollView", "Toggle view scrolling via Shift+Mouse.", DebugCmdScrollView);
	CurrentDebugCommandRegistry.Register("debug.display.tileData", "Toggle tile-data inspection.", [](std::string_view arguments) {
		return DebugCmdShowTileData(arguments.empty() ? std::nullopt : std::optional<std::string_view> { arguments });
	});
	CurrentDebugCommandRegistry.Register("debug.display.fullbright", "Toggle fullbright lighting.", [](std::string_view arguments) {
		return arguments.empty() ? DebugCmdFullbright(std::nullopt) : std::string("This command takes no arguments.");
	});
	CurrentDebugCommandRegistry.Register("debug.level.map.reveal", "Reveal the entire automap.", [](std::string_view arguments) {
		return arguments.empty() ? DebugCmdMapReveal() : std::string("This command takes no arguments.");
	});
	CurrentDebugCommandRegistry.Register("debug.level.map.hide", "Hide the entire automap.", [](std::string_view arguments) {
		return arguments.empty() ? DebugCmdMapHide() : std::string("This command takes no arguments.");
	});
	CurrentDebugCommandRegistry.Register("debug.level.reset", "Reset a dungeon level, optionally selecting its seed.", [](std::string_view arguments) {
		const auto parsed = ParseLevelResetArguments(arguments);
		return parsed.has_value() ? DebugCmdResetLevel(parsed->first, parsed->second) : std::string("Expected a level number and optional integer seed.");
	});
	CurrentDebugCommandRegistry.Register("debug.level.warp.dungeon", "Warp to a dungeon level.", [](std::string_view arguments) {
		if (arguments.empty() || HasInvalidLevelArgument(arguments))
			return std::string("Expected a dungeon level number.");
		return DebugCmdWarpToDungeonLevel(*ParseOptionalLevel(arguments));
	});
	CurrentDebugCommandRegistry.Register("debug.level.warp.quest", "Warp to a quest level.", [](std::string_view arguments) {
		if (arguments.empty() || HasInvalidLevelArgument(arguments))
			return std::string("Expected a quest level number.");
		return DebugCmdWarpToQuestLevel(*ParseOptionalLevel(arguments));
	});
	CurrentDebugCommandRegistry.Register("debug.level.warp.map", "Warp to a custom dungeon map.", [](std::string_view arguments) {
		const auto parsed = ParseCustomMapWarpArguments(arguments);
		return parsed.has_value() ? DebugCmdWarpToCustomMap(parsed->path, parsed->dunType, parsed->x, parsed->y) : std::string("Expected a map path, dungeon type, and spawn x/y coordinates.");
	});
	CurrentDebugCommandRegistry.Register("debug.items.info", "Show information about the selected item.", [](std::string_view arguments) {
		return arguments.empty() ? DebugCmdItemInfo() : std::string("This command takes no arguments.");
	});
	CurrentDebugCommandRegistry.Register("debug.items.spawn", "Generate an item matching a name.", [](std::string_view arguments) {
		return DebugCmdSpawnItem(std::string(arguments));
	});
	CurrentDebugCommandRegistry.Register("debug.items.spawnUnique", "Generate a unique item matching a name.", [](std::string_view arguments) {
		return DebugCmdSpawnUniqueItem(std::string(arguments));
	});
	CurrentDebugCommandRegistry.Register("debug.monsters.spawn", "Spawn monsters matching a name.", [](std::string_view arguments) {
		const auto parsed = ParseNamedCountArguments(arguments);
		return DebugCmdSpawnMonster(std::string(parsed.name), parsed.count);
	});
	CurrentDebugCommandRegistry.Register("debug.monsters.spawnUnique", "Spawn unique monsters matching a name.", [](std::string_view arguments) {
		const auto parsed = ParseNamedCountArguments(arguments);
		return DebugCmdSpawnUniqueMonster(std::string(parsed.name), parsed.count);
	});
	CurrentDebugCommandRegistry.Register("debug.level.exportDun", "Export the current level as a dun file.", [](std::string_view arguments) {
		return arguments.empty() ? DebugCmdExportDun() : std::string("This command takes no arguments.");
	});
	CurrentDebugCommandRegistry.Register("debug.towners.talk", "Open a towner talk window.", DebugCmdTalkToTowner);
	CurrentDebugCommandRegistry.Register("debug.towners.visit", "Teleport to a towner in town.", DebugCmdVisitTowner);
	CurrentDebugCommandRegistry.Register("debug.level.seed", "Show the current or selected level seed.", [](std::string_view arguments) {
		if (HasInvalidLevelArgument(arguments))
			return std::string("Expected an optional level number.");
		return DebugCmdLevelSeed(ParseOptionalLevel(arguments));
	});
	CurrentDebugCommandRegistry.Register("debug.search.clear", "Remove all automap search markers.", [](std::string_view arguments) {
		return arguments.empty() ? DebugCmdClearSearch() : std::string("This command takes no arguments.");
	});
	CurrentDebugCommandRegistry.Register("debug.search.item", "Highlight matching item names on the automap.", DebugCmdSearchItem);
	CurrentDebugCommandRegistry.Register("debug.search.monster", "Highlight matching monster names on the automap.", DebugCmdSearchMonster);
	CurrentDebugCommandRegistry.Register("debug.search.object", "Highlight matching object names on the automap.", DebugCmdSearchObject);
}

void LoadDebugGFX()
{
	pSquareCel = LoadCel("data\\square", 64);
}

void FreeDebugGFX()
{
	pSquareCel = std::nullopt;
}

void GetDebugMonster()
{
	int monsterIndex = pcursmonst;
	if (monsterIndex == -1)
		monsterIndex = std::abs(tileAt(cursPosition).monster()) - 1;

	if (monsterIndex == -1)
		monsterIndex = DebugMonsterId;

	PrintDebugMonster(Monsters[monsterIndex]);
}

void NextDebugMonster()
{
	DebugMonsterId++;
	if (DebugMonsterId == MaxMonsters)
		DebugMonsterId = 0;

	EventPlrMsg(StrCat("Current debug monster = ", DebugMonsterId), UiFlags::ColorWhite);
}

void SetDebugLevelSeedInfos(uint32_t mid1Seed, uint32_t mid2Seed, uint32_t mid3Seed, uint32_t endSeed)
{
	glMid1Seed[currentLevelNumber()] = mid1Seed;
	glMid2Seed[currentLevelNumber()] = mid2Seed;
	glMid3Seed[currentLevelNumber()] = mid3Seed;
	glEndSeed[currentLevelNumber()] = endSeed;
}

bool IsDebugGridTextNeeded()
{
	return SelectedDebugGridTextItem != DebugGridTextItem::None;
}

bool IsDebugGridInMegatiles()
{
	switch (SelectedDebugGridTextItem) {
	case DebugGridTextItem::AutomapView:
	case DebugGridTextItem::CurrentMegaTile:
	case DebugGridTextItem::ReplacementMegaTile:
	case DebugGridTextItem::DProtected:
		return true;
	default:
		return false;
	}
}

DebugGridTextItem GetDebugGridTextType()
{
	return SelectedDebugGridTextItem;
}

void SetDebugGridTextType(DebugGridTextItem value)
{
	SelectedDebugGridTextItem = value;
}

bool GetDebugGridText(Point dungeonCoords, std::string &debugGridText)
{
	int info = 0;
	int blankValue = 0;
	debugGridText.clear();
	Point megaCoords = dungeonCoords.worldToMega();
	switch (SelectedDebugGridTextItem) {
	case DebugGridTextItem::coords:
		StrAppend(debugGridText, dungeonCoords.x, ":", dungeonCoords.y);
		return true;
	case DebugGridTextItem::cursorcoords:
		if (dungeonCoords != cursPosition)
			return false;
		StrAppend(debugGridText, dungeonCoords.x, ":", dungeonCoords.y);
		return true;
	case DebugGridTextItem::objectindex: {
		info = 0;
		Object *object = FindObjectAtPosition(dungeonCoords);
		if (object != nullptr) {
			info = static_cast<int>(object->_otype);
		}
		break;
	}
	case DebugGridTextItem::microTiles: {
		const MICROS &micros = levelMicros()[tileAt(dungeonCoords).piece()];
		for (const LevelCelBlock tile : micros.mt) {
			if (!tile.hasValue()) break;
			if (!debugGridText.empty()) debugGridText += '\n';
			StrAppend(debugGridText, tile.frame(), " ");
			switch (tile.type()) {
			case TileType::Square: StrAppend(debugGridText, "S"); break;
			case TileType::TransparentSquare: StrAppend(debugGridText, "T"); break;
			case TileType::LeftTriangle: StrAppend(debugGridText, "<"); break;
			case TileType::RightTriangle: StrAppend(debugGridText, ">"); break;
			case TileType::LeftTrapezoid: StrAppend(debugGridText, "\\"); break;
			case TileType::RightTrapezoid: StrAppend(debugGridText, "/"); break;
			}
		}
		return !debugGridText.empty();
	} break;
	case DebugGridTextItem::DPiece:
		info = tileAt(dungeonCoords).piece();
		break;
	case DebugGridTextItem::DTransVal:
		info = tileAt(dungeonCoords).transVal();
		break;
	case DebugGridTextItem::DLight:
		info = tileAt(dungeonCoords).light();
		blankValue = LightsMax;
		break;
	case DebugGridTextItem::DPreLight:
		info = tileAt(dungeonCoords).preLight();
		blankValue = LightsMax;
		break;
	case DebugGridTextItem::DFlags:
		info = static_cast<int>(tileAt(dungeonCoords).flags());
		break;
	case DebugGridTextItem::DPlayer:
		info = tileAt(dungeonCoords).player();
		break;
	case DebugGridTextItem::DMonster:
		info = tileAt(dungeonCoords).monster();
		break;
	case DebugGridTextItem::missiles: {
		for (auto &missile : Missiles) {
			if (missile.position.tile == dungeonCoords) {
				if (!debugGridText.empty()) debugGridText += '\n';
				debugGridText.append(std::to_string((int)missile._mitype));
			}
		}
		return !debugGridText.empty();
	} break;
	case DebugGridTextItem::DCorpse:
		info = tileAt(dungeonCoords).corpse();
		break;
	case DebugGridTextItem::DItem:  // Updated from dItem to DItem
		info = tileAt(dungeonCoords).item();
		break;
	case DebugGridTextItem::DSpecial:
		info = tileAt(dungeonCoords).special();
		break;
	case DebugGridTextItem::DObject:
		info = tileAt(dungeonCoords).object();
		break;
	case DebugGridTextItem::Solid:
		info = TileHasAny(dungeonCoords, TileProperties::Solid) << 0 | TileHasAny(dungeonCoords, TileProperties::BlockLight) << 1 | TileHasAny(dungeonCoords, TileProperties::BlockMissile) << 2;
		break;
	case DebugGridTextItem::Transparent:
		info = TileHasAny(dungeonCoords, TileProperties::Transparent) << 0 | TileHasAny(dungeonCoords, TileProperties::TransparentLeft) << 1 | TileHasAny(dungeonCoords, TileProperties::TransparentRight) << 2;
		break;
	case DebugGridTextItem::Trap:
		info = TileHasAny(dungeonCoords, TileProperties::Trap);
		break;
	case DebugGridTextItem::AutomapView:
		if (megaCoords.x >= 0 && megaCoords.x < DMAXX && megaCoords.y >= 0 && megaCoords.y < DMAXY)
			info = CurrentAutomapManager.GetAutomapView()[megaCoords.x][megaCoords.y];
		break;
	case DebugGridTextItem::CurrentMegaTile:
		if (megaCoords.x >= 0 && megaCoords.x < DMAXX && megaCoords.y >= 0 && megaCoords.y < DMAXY)
			info = megaTileAt(megaCoords.x, megaCoords.y).current();
		break;
	case DebugGridTextItem::ReplacementMegaTile:
		if (megaCoords.x >= 0 && megaCoords.x < DMAXX && megaCoords.y >= 0 && megaCoords.y < DMAXY)
			info = megaTileAt(megaCoords.x, megaCoords.y).replacement();
		break;
	case DebugGridTextItem::DProtected:
		if (megaCoords.x >= 0 && megaCoords.x < DMAXX && megaCoords.y >= 0 && megaCoords.y < DMAXY)
			info = protectedTiles().test(megaCoords.x, megaCoords.y);
		break;
	case DebugGridTextItem::None:
		return false;
	}
	if (info == blankValue)
		return false;
	StrAppend(debugGridText, info);
	return true;
}

bool IsDebugAutomapHighlightNeeded()
{
	return SearchMonsters.size() > 0 || SearchItems.size() > 0 || SearchObjects.size() > 0;
}

bool ShouldHighlightDebugAutomapTile(Point position)
{
	auto matchesSearched = [](const std::string_view name, const std::vector<std::string> &searchedNames) {
		const std::string lowercaseName = AsciiStrToLower(name);
		for (const auto &searchedName : searchedNames) {
			if (lowercaseName.find(searchedName) != std::string::npos) {
				return true;
			}
		}
		return false;
	};

	if (SearchMonsters.size() > 0 && tileAt(position).hasMonster()) {
		const int mi = std::abs(tileAt(position).monster()) - 1;
		const Monster &monster = Monsters[mi];
		if (matchesSearched(monster.name(), SearchMonsters))
			return true;
	}

	if (SearchItems.size() > 0 && tileAt(position).item() != 0) {
		const int itemId = std::abs(tileAt(position).item()) - 1;
		const Item &item = Items[itemId];
		if (matchesSearched(item.getName(), SearchItems))
			return true;
	}

	if (SearchObjects.size() > 0 && IsObjectAtPosition(position)) {
		const Object &object = ObjectAtPosition(position);
		if (matchesSearched(object.name(), SearchObjects))
			return true;
	}

	return false;
}

void AddDebugAutomapMonsterHighlight(std::string_view name)
{
	SearchMonsters.emplace_back(name);
}

void AddDebugAutomapItemHighlight(std::string_view name)
{
	SearchItems.emplace_back(name);
}

void AddDebugAutomapObjectHighlight(std::string_view name)
{
	SearchObjects.emplace_back(name);
}

void ClearDebugAutomapHighlights()
{
	SearchMonsters.clear();
	SearchItems.clear();
	SearchObjects.clear();
}

std::string DebugCmdShowTileData(std::optional<std::string_view> dataType)
{
	if (!dataType.has_value()) {
		std::string result = "Valid values for the first argument:\nclear";
		for (const std::string_view name : DebugTileDataNames)
			StrAppend(result, ", ", name);
		return result;
	}
	if (*dataType == "clear") {
		SetDebugGridTextType(DebugGridTextItem::None);
		return "Tile data cleared.";
	}
	std::string_view requestedType = *dataType;
	if (requestedType == "dPiece")
		requestedType = "piece";
	if (requestedType == "dTransVal")
		requestedType = "transVal";
	int index = 0;
	for (const std::string_view name : DebugTileDataNames) {
		++index;
		if (requestedType != name)
			continue;
		const auto newGridText = static_cast<DebugGridTextItem>(index);
		if (newGridText == GetDebugGridTextType()) {
			SetDebugGridTextType(DebugGridTextItem::None);
			return "Tile data: Off";
		}
		SetDebugGridTextType(newGridText);
		return "Tile data: On";
	}
	std::string result = "Invalid name! Valid names are:\nclear";
	for (const std::string_view name : DebugTileDataNames)
		StrAppend(result, ", ", name);
	return result;
}

std::string DebugCmdScrollView(std::optional<bool> on)
{
	DebugScrollViewEnabled = on.value_or(!DebugScrollViewEnabled);
	if (!DebugScrollViewEnabled)
		InitMultiView();
	return StrCat("Scroll view: ", DebugScrollViewEnabled ? "On" : "Off");
}

std::string DebugCmdLevelSeed(std::optional<uint8_t> level)
{
	constexpr size_t NumLevels = sizeof(DungeonSeeds) / sizeof(DungeonSeeds[0]);
	if (level.has_value() && *level >= NumLevels)
		return StrCat("level out of range, max: ", NumLevels - 1);
	return StrCat(DungeonSeeds[level.value_or(currentLevelNumber())]);
}

std::string DebugCmdSearchMonster(std::string_view name)
{
	if (name.empty())
		return "Missing monster name!";
	AddDebugAutomapMonsterHighlight(AsciiStrToLower(name));
	return StrCat("Added automap marker for monster ", name, ".");
}

std::string DebugCmdSearchItem(std::string_view name)
{
	if (name.empty())
		return "Missing item name!";
	AddDebugAutomapItemHighlight(AsciiStrToLower(name));
	return StrCat("Added automap marker for item ", name, ".");
}

std::string DebugCmdSearchObject(std::string_view name)
{
	if (name.empty())
		return "Missing object name!";
	AddDebugAutomapObjectHighlight(AsciiStrToLower(name));
	return StrCat("Added automap marker for object ", name, ".");
}

std::string DebugCmdClearSearch()
{
	ClearDebugAutomapHighlights();
	return "Removed all automap search markers.";
}

} // namespace devilution

#endif

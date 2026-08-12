/**
 * @file application/debug.cpp
 *
 * Implementation of debug functions.
 */

#include "application/debug.h"
#ifdef _DEBUG

#include <array>
#include <charconv>
#include <cmath>
#include <cstdint>
#include <cstdio>

#include <ankerl/unordered_dense.h>

#include "application/debug.h"
#include "application/debug_commands.hpp"

#include "engine/render/world_renderer.h"

#include "engine/assets.hpp"
#include "engine/gfx/backbuffer_state.hpp"
#include "game/items/items.hpp"
#include "game/levels/dungeon_common.h"
#include "game/players/players.hpp"
#include "ui/automap.h"
#include "engine/cursor.h"
#include "engine/load/load_cel.hpp"
#include "engine/math/point.hpp"
#include "engine/lighting.h"
#include "game/missiles/missiles.hpp"
#include "game/monsters/monsters.hpp"
#include "game/monsters/monster_pool.hpp"
#include "network/protocol/msg.h"
#include "ui/player_messages.h"
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

} // namespace

std::string DebugCmdGodMode(std::optional<bool> on)
{
	DebugGodMode = on.value_or(!DebugGodMode);
	return StrCat("God mode: ", DebugGodMode ? "On" : "Off");
}

std::string DebugCmdInvisible(std::optional<bool> on)
{
	DebugInvisible = on.value_or(!DebugInvisible);
	return StrCat("Invisible: ", DebugInvisible ? "On" : "Off");
}

std::string DebugCmdArrow(std::string_view effect)
{
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
	Player &myPlayer = *MyPlayer;
	myPlayer.RestoreFullLife();
	myPlayer.RestoreFullMana();
	RedrawComponent(PanelDrawComponent::Health);
	RedrawComponent(PanelDrawComponent::Mana);
	return "Restored life and mana to full.";
}

std::string DebugCmdChangeHealth(int change)
{
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
	Player &myPlayer = *MyPlayer;
	myPlayer.modifyStrength(myPlayer.GetMaximumAttributeValue(CharacterAttribute::Strength) - myPlayer.attributes.strength.base);
	myPlayer.modifyMagic(myPlayer.GetMaximumAttributeValue(CharacterAttribute::Magic) - myPlayer.attributes.magic.base);
	myPlayer.modifyDexterity(myPlayer.GetMaximumAttributeValue(CharacterAttribute::Dexterity) - myPlayer.attributes.dexterity.base);
	myPlayer.modifyVitality(myPlayer.GetMaximumAttributeValue(CharacterAttribute::Vitality) - myPlayer.attributes.vitality.base);
	return "Set all character base attributes to maximum.";
}

std::string DebugCmdMinStats()
{
	Player &myPlayer = *MyPlayer;
	myPlayer.modifyStrength(-myPlayer.attributes.strength.base);
	myPlayer.modifyMagic(-myPlayer.attributes.magic.base);
	myPlayer.modifyDexterity(-myPlayer.attributes.dexterity.base);
	myPlayer.modifyVitality(-myPlayer.attributes.vitality.base);
	return "Set all character base attributes to minimum.";
}

std::string DebugCmdLevelUp(int levels)
{
	if (levels <= 0)
		return "amount must be positive";
	Player &myPlayer = *MyPlayer;
	for (int i = 0; i < levels; i++)
		NetSendCmd(true, CMD_CHEAT_EXPERIENCE);
	return StrCat("New character level: ", myPlayer.getCharacterLevel() + levels);
}

std::string DebugCmdGiveGoldCheat(int amount)
{
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

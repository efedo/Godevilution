/**
 * @file lua/modules/dev/player/stats.cpp
 *
 * Implementation of stats.
 */


#ifdef _DEBUG
#include "lua/modules/dev/player/stats.hpp"

#include <string>

#include <sol/sol.hpp>

#include "application/debug.h"
#include "lua/metadoc.hpp"
#include "game/players/players.hpp"
#include "utils/string/str_cat.hpp"

namespace devilution {

namespace {

std::string DebugCmdLevelUp(std::optional<int> levels)
{
	const int levelsToAdd = levels.value_or(1);
	if (levelsToAdd <= 0) return "amount must be positive";
	Player &myPlayer = *MyPlayer;
	for (int i = 0; i < levelsToAdd; i++)
		NetSendCmd(true, CMD_CHEAT_EXPERIENCE);
	return StrCat("New character level: ", myPlayer.getCharacterLevel() + levelsToAdd);
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

} // namespace

sol::table LuaDevPlayerStatsModule(sol::state_view &lua)
{
	sol::table table = lua.create_table();
	LuaSetDocFn(table, "adjustHealth", "(amount: number)", "Adjust HP (amount can be negative)", &DebugCmdChangeHealth);
	LuaSetDocFn(table, "adjustMana", "(amount: number)", "Adjust mana (amount can be negative)", &DebugCmdChangeMana);
	LuaSetDocFn(table, "levelUp", "(amount: number = 1)", "Level the player up.", &DebugCmdLevelUp);
	LuaSetDocFn(table, "rejuvenate", "()", "Refill health", &DebugCmdRejuvenate);
	LuaSetDocFn(table, "setAttrToMax", "()", "Set Str, Mag, Dex, and Vit to maximum.", &DebugCmdMaxStats);
	LuaSetDocFn(table, "setAttrToMin", "()", "Set Str, Mag, Dex, and Vit to minimum.", &DebugCmdMinStats);
	return table;
}

} // namespace devilution
#endif // _DEBUG

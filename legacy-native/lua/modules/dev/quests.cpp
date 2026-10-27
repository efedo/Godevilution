/**
 * @file lua/modules/dev/quests.cpp
 *
 * Implementation of quests.
 */

#ifdef _DEBUG
#include "lua/modules/dev/quests.hpp"

#include <cstdint>
#include <string>

#include <sol/sol.hpp>

#include "application/debug.h"
#include "lua/metadoc.hpp"

namespace devilution {

sol::table LuaDevQuestsModule(sol::state_view &lua)
{
	sol::table table = lua.create_table();
	LuaSetDocFn(table, "activate", "(id: number)", "Activate the given quest.", &DebugCmdEnableQuest);
	LuaSetDocFn(table, "activateAll", "()", "Activate all available quests.", &DebugCmdEnableQuests);
	LuaSetDocFn(table, "all", "()", "Information on all available quest.", &DebugCmdQuestsInfo);
	LuaSetDocFn(table, "info", "(id: number)", "Information on the given quest.", &DebugCmdQuestInfo);
	return table;
}

} // namespace devilution
#endif // _DEBUG

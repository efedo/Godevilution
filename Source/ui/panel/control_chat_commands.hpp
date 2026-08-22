#pragma once

/**
 * @file ui/panel/control_chat_commands.hpp
 *
 * Interface for chat commands.
 */


#include <string_view>

namespace devilution {

/**
 * Legacy chat commands that mutate gameplay are offline-only during the
 * server-backed migration.
 */
constexpr bool ShouldProcessLegacyChatMutation(bool serverConnected) noexcept
{
	return !serverConnected;
}

bool CheckChatCommand(std::string_view text);

} // namespace devilution

#pragma once

namespace devilution {

/**
 * Legacy quest, spell, object, and set-piece synchronization messages are
 * offline-only while the server-backed runtime owns world state.
 */
constexpr bool ShouldProcessLegacyWorldStateMessage(bool serverConnected) noexcept
{
	return !serverConnected;
}

} // namespace devilution

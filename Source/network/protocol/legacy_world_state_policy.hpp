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

/**
 * Legacy quest resynchronization rebuilds local quest-driven world state and
 * is offline-only while connected sessions use authoritative projections.
 */
constexpr bool ShouldProcessLegacyQuestResync(bool serverConnected) noexcept
{
	return ShouldProcessLegacyWorldStateMessage(serverConnected);
}

} // namespace devilution

#pragma once

namespace devilution {

/**
 * Legacy player-info and remote-player state messages are offline-only while
 * connected sessions use authoritative player snapshots.
 */
constexpr bool ShouldProcessLegacyPlayerStateMessage(bool serverConnected) noexcept
{
	return !serverConnected;
}

} // namespace devilution

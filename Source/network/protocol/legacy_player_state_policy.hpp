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

/**
 * Legacy player simulation is offline-only while the authoritative server
 * owns connected movement, combat, and player lifecycle transitions.
 */
constexpr bool ShouldProcessLegacyPlayerSimulation(bool serverConnected) noexcept
{
	return !serverConnected;
}

} // namespace devilution

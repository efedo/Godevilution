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
 * Legacy native simulation is offline-only while the authoritative server
 * owns connected movement, combat, and world-state transitions.
 */
constexpr bool ShouldProcessLegacySimulation(bool serverConnected) noexcept
{
	return !serverConnected;
}

/** Compatibility-named player simulation policy for existing callers. */
constexpr bool ShouldProcessLegacyPlayerSimulation(bool serverConnected) noexcept
{
	return ShouldProcessLegacySimulation(serverConnected);
}

} // namespace devilution

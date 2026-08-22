#pragma once

namespace devilution {

/**
 * Legacy movement, targeting, and action messages are offline-only while the
 * server-backed runtime owns input intent and simulation.
 */
constexpr bool ShouldProcessLegacyInputMessage(bool serverConnected) noexcept
{
	return !serverConnected;
}

/** Legacy telekinesis command emission is offline-only while input is server-owned. */
constexpr bool ShouldProcessLegacyTelekinesis(bool serverConnected) noexcept
{
	return !serverConnected;
}

} // namespace devilution

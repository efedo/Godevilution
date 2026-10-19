#pragma once

namespace devilution {

/**
 * Legacy level and portal transition messages are offline-only while the
 * server-backed runtime owns movement, portals, and level state.
 */
constexpr bool ShouldProcessLegacyTransitionMessage(bool serverConnected) noexcept
{
	return !serverConnected;
}

} // namespace devilution

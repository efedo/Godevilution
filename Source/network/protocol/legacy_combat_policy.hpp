#pragma once

namespace devilution {

/**
 * Legacy combat, damage, and effect messages are offline-only while the
 * server-backed runtime owns combat state and resolution.
 */
constexpr bool ShouldProcessLegacyCombatMessage(bool serverConnected) noexcept
{
	return !serverConnected;
}

} // namespace devilution

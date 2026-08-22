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

/**
 * Native death-drop generation is offline-only while the authoritative server
 * owns gold, equipment, held-item, and ear consequences.
 */
constexpr bool ShouldProcessLegacyDeathDrops(bool authoritativeMode) noexcept
{
	return !authoritativeMode;
}

} // namespace devilution

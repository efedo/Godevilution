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

/** Native spell and resurrect-beam creation is offline-only while the authoritative server owns effects. */
constexpr bool ShouldProcessLegacySpellSimulation(bool serverConnected) noexcept
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

/**
 * Native periodic equipment damage is offline-only while the authoritative
 * server owns the connected player's combat tick.
 */
constexpr bool ShouldProcessNativeDrainLife(bool serverConnected) noexcept
{
	return !serverConnected;
}

/**
 * Native status lifecycle messages are offline-only while the authoritative
 * server owns connected level-transition state.
 */
constexpr bool ShouldProcessLegacyStatusLifecycle(bool serverConnected) noexcept
{
	return !serverConnected;
}

} // namespace devilution

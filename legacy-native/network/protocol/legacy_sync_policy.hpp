#pragma once

namespace devilution {

/**
 * Legacy monster-delta synchronization and debug mutation messages are
 * offline-only while connected sessions use authoritative snapshots.
 */
constexpr bool ShouldProcessLegacySyncMessage(bool serverConnected) noexcept
{
	return !serverConnected;
}

/**
 * Legacy delta state is a synchronization implementation detail and is
 * offline-only while connected sessions use authoritative snapshots.
 */
constexpr bool ShouldProcessLegacyDeltaState(bool serverConnected) noexcept
{
	return ShouldProcessLegacySyncMessage(serverConnected);
}

} // namespace devilution

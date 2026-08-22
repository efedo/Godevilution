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

} // namespace devilution

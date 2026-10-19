#pragma once

namespace devilution {

/**
 * Debug commands that mutate player or world state are offline-only while the
 * server-backed runtime owns authoritative simulation state.
 */
constexpr bool ShouldProcessLegacyDebugMutation(bool authoritativeMode) noexcept
{
	return !authoritativeMode;
}

} // namespace devilution

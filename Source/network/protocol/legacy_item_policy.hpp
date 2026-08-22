#pragma once

namespace devilution {

/**
 * Legacy item protocol messages are unavailable while the server-backed
 * runtime owns world-item and inventory mutations.
 */
constexpr bool ShouldProcessLegacyItemProtocol(bool serverConnected) noexcept
{
	return !serverConnected;
}

} // namespace devilution

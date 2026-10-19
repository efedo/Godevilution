#pragma once

namespace devilution {

/**
 * Legacy CMD_ADD* messages are offline-only during the server-backed
 * attribute-allocation migration.
 */
constexpr bool ShouldProcessLegacyAttributeMessage(bool serverConnected) noexcept
{
	return !serverConnected;
}

} // namespace devilution

#pragma once

namespace devilution {

/**
 * Legacy store mutations are unavailable while the server-backed runtime owns
 * vendor transactions, item services, and player currency.
 */
constexpr bool ShouldProcessLegacyStoreMutation(bool serverConnected) noexcept
{
	return !serverConnected;
}

} // namespace devilution

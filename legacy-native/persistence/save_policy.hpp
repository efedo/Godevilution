/**
 * @file persistence/save_policy.hpp
 *
 * Shared policy for legacy native save access during server-backed migration.
 */
#pragma once

namespace devilution {

/**
 * Server-backed sessions own persistence; local binary saves are legacy-only.
 */
constexpr bool ShouldUseLegacyLocalSave(bool serverConnected) noexcept
{
	return !serverConnected;
}

} // namespace devilution

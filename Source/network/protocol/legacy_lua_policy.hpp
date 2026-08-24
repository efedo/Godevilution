#pragma once

namespace devilution {

/**
 * The embedded Lua runtime belongs only to the native compatibility client.
 * The authoritative client receives gameplay and presentation data through the
 * server-backed protocol and must not initialize a second script-owned state.
 */
constexpr bool ShouldInitializeLegacyLua(bool authoritativeMode) noexcept
{
	return !authoritativeMode;
}

} // namespace devilution

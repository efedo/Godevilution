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

/**
 * Direct legacy inventory mutations are unavailable while the server-backed
 * runtime owns item placement, removal, and equipment state.
 */
constexpr bool ShouldProcessLegacyInventoryMutation(bool serverConnected) noexcept
{
	return !serverConnected;
}

/** Direct legacy identify, repair, recharge, and oil callbacks are offline-only. */
constexpr bool ShouldProcessLegacyItemServiceMutation(bool serverConnected) noexcept
{
	return !serverConnected;
}

/**
 * Legacy item-use handlers remain available offline. Connected scroll and rune
 * casts are the only legacy item-use shape that is still allowed to continue
 * because UseItem immediately submits the authoritative cast intent.
 */
constexpr bool ShouldProcessLegacyItemUse(bool serverConnected, bool serverRoutedCast) noexcept
{
	return !serverConnected || serverRoutedCast;
}

/** Direct legacy Gold-stack mutation remains offline-only; connected UI submits SplitGoldRequested. */
constexpr bool ShouldProcessLegacyGoldDrop(bool serverConnected) noexcept
{
	return !serverConnected;
}

/** Ctrl-click stash transfers must use the authoritative move command when connected. */
constexpr bool ShouldProcessLegacyStashInventoryTransfer(bool serverConnected) noexcept
{
	return !serverConnected;
}

/** Automatic legacy pickup commands are offline-only while world items are server-owned. */
constexpr bool ShouldProcessLegacyAutoPickup(bool serverConnected) noexcept
{
	return !serverConnected;
}

} // namespace devilution

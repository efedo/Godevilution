#pragma once

namespace devilution {

/**
 * Legacy quest, spell, object, and set-piece synchronization messages are
 * offline-only while the server-backed runtime owns world state.
 */
constexpr bool ShouldProcessLegacyWorldStateMessage(bool serverConnected) noexcept
{
	return !serverConnected;
}

/** Legacy object synchronization mutates native object state and is offline-only. */
constexpr bool ShouldProcessLegacyObjectSynchronization(bool serverConnected) noexcept
{
	return ShouldProcessLegacyWorldStateMessage(serverConnected);
}

/**
 * Legacy per-tick quest checks can create portals, mutate quest variables, and
 * start level transitions, so they are offline-only for server-backed games.
 */
constexpr bool ShouldProcessLegacyQuestTick(bool serverConnected) noexcept
{
	return ShouldProcessLegacyWorldStateMessage(serverConnected);
}

/**
 * Legacy trigger checks can emit level-transition commands when a player
 * reaches a stair, warp, or quest entrance, so they are offline-only while
 * connected sessions use authoritative movement and transition projections.
 */
constexpr bool ShouldProcessLegacyTriggerCheck(bool serverConnected) noexcept
{
	return ShouldProcessLegacyWorldStateMessage(serverConnected);
}

/**
 * Legacy quest resynchronization rebuilds local quest-driven world state and
 * is offline-only while connected sessions use authoritative projections.
 */
constexpr bool ShouldProcessLegacyQuestResync(bool serverConnected) noexcept
{
	return ShouldProcessLegacyWorldStateMessage(serverConnected);
}

/**
 * Legacy level-load quest mutations are offline-only while connected
 * sessions use authoritative quest and transition state.
 */
constexpr bool ShouldProcessLegacyLevelLoadMutation(bool serverConnected) noexcept
{
	return ShouldProcessLegacyWorldStateMessage(serverConnected);
}

/**
 * Legacy level-load player placement may choose a nearby local tile, so it is
 * offline-only while connected sessions retain the server-selected position.
 */
constexpr bool ShouldProcessLegacyLevelLoadPlayerPlacement(bool serverConnected) noexcept
{
	return ShouldProcessLegacyLevelLoadMutation(serverConnected);
}

/**
 * Legacy missile tile flags belong to locally simulated missiles; authoritative
 * presentation missiles must not rewrite that native gameplay bookkeeping.
 */
constexpr bool ShouldClearLegacyMissileTileFlags(bool authoritativePresentation) noexcept
{
	return !authoritativePresentation;
}

} // namespace devilution

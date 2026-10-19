#pragma once

/**
 * @file network/authoritative/server_backed_player_ui.hpp
 *
 * Applies validated authoritative player state to the legacy native player.
 */

#include <string>

#include <expected.hpp>

#include "game/players/players.hpp"
#include "network/authoritative/player_snapshot.hpp"

namespace devilution::authoritative {

/** Applies one complete authoritative player snapshot without partial mutation. */
tl::expected<void, std::string> ApplyServerBackedPlayerSnapshot(Player &player, const ProjectedPlayerSnapshot &snapshot);

/** Rebuilds the legacy stash buffers from a validated server-owned projection. */
tl::expected<void, std::string> ApplyServerBackedStashProjection(const ProjectedPlayerSnapshot &snapshot);

} // namespace devilution::authoritative

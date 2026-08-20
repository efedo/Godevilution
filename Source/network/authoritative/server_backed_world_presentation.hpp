#pragma once

/**
 * @file network/authoritative/server_backed_world_presentation.hpp
 *
 * Applies authoritative world entities to the legacy presentation pools.
 */

#include <cstdint>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>

#include <expected.hpp>

#include "engine/gfx/surface.hpp"
#include "engine/math/point.hpp"
#include "game/players/players.hpp"
#include "network/authoritative/player_snapshot.hpp"
#include "network/authoritative/server_backed_world_projection.hpp"

namespace devilution::authoritative {

/** Maps an authoritative player combat mode to a native presentation graphic. */
[[nodiscard]] player_graphic GetRemotePlayerGraphic(const ProjectedPlayerSnapshot &snapshot);

/** Keeps legacy rendering and tile lookup state synchronized with the server. */
class ServerBackedWorldPresentation {
public:
	[[nodiscard]] tl::expected<void, std::string> Apply(
		const ServerBackedWorldProjection &projection,
		uint32_t levelId,
		const std::vector<ProjectedPlayerSnapshot> &players = {},
		uint32_t localEntityId = 0);

	/** Draws projected remote players without adding them to native simulation pools. */
	void DrawRemotePlayers(
		const Surface &out,
		Point tilePosition,
		Point targetBufferPosition,
		int lightTableIndex) const;

	[[nodiscard]] std::size_t RemotePlayerCount() const noexcept { return remotePlayers_.size(); }

	void Clear() noexcept;

private:
	std::unordered_map<uint32_t, unsigned> monsterSlots_;
	std::unordered_map<uint32_t, unsigned> itemSlots_;
	std::unordered_map<uint32_t, int> objectSlots_;
	std::unordered_set<uint32_t> projectileEntities_;

	struct RemotePlayerPresentation {
		Player player;
		uint32_t levelId = 0;
		Point tilePosition;
	};
	std::unordered_map<uint32_t, RemotePlayerPresentation> remotePlayers_;
};

} // namespace devilution::authoritative

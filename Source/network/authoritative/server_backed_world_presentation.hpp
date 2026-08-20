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

#include <expected.hpp>

#include "network/authoritative/server_backed_world_projection.hpp"

namespace devilution::authoritative {

/** Keeps legacy rendering and tile lookup state synchronized with the server. */
class ServerBackedWorldPresentation {
public:
	[[nodiscard]] tl::expected<void, std::string> Apply(
		const ServerBackedWorldProjection &projection,
		uint32_t levelId);

	void Clear() noexcept;

private:
	std::unordered_map<uint32_t, unsigned> monsterSlots_;
	std::unordered_map<uint32_t, unsigned> itemSlots_;
	std::unordered_map<uint32_t, int> objectSlots_;
	std::unordered_set<uint32_t> projectileEntities_;
};

} // namespace devilution::authoritative

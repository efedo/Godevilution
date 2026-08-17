#pragma once

/**
 * @file network/authoritative/server_backed_world_projection.hpp
 *
 * Presentation-owned copy of the latest authoritative world entities.
 */

#include <cstdint>
#include <optional>
#include <string>
#include <vector>

#include <expected.hpp>

#include "network/authoritative/player_snapshot.hpp"

namespace devilution::authoritative {

/** Keeps world projections stable while native render and interaction code catches up. */
class ServerBackedWorldProjection {
public:
	[[nodiscard]] tl::expected<void, std::string> Apply(
		std::vector<ProjectedMonsterSnapshot> monsters,
		std::vector<ProjectedWorldItemSnapshot> worldItems,
		std::vector<ProjectedObjectSnapshot> objects,
		uint32_t levelId);

	void Clear() noexcept;

	[[nodiscard]] uint32_t LevelId() const noexcept { return levelId_; }
	[[nodiscard]] const std::vector<ProjectedMonsterSnapshot> &Monsters() const noexcept { return monsters_; }
	[[nodiscard]] const std::vector<ProjectedWorldItemSnapshot> &WorldItems() const noexcept { return worldItems_; }
	[[nodiscard]] const std::vector<ProjectedObjectSnapshot> &Objects() const noexcept { return objects_; }

	[[nodiscard]] std::optional<uint32_t> WorldItemAt(int32_t positionX, int32_t positionY) const noexcept;
	[[nodiscard]] std::optional<uint32_t> MonsterAt(int32_t positionX, int32_t positionY) const noexcept;
	[[nodiscard]] std::optional<uint32_t> ObjectAt(int32_t positionX, int32_t positionY) const noexcept;
	/** Finds an active content-owned quest object at a projected cell. */
	[[nodiscard]] std::optional<uint32_t> QuestObjectAt(int32_t positionX, int32_t positionY) const noexcept;

private:
	std::vector<ProjectedMonsterSnapshot> monsters_;
	std::vector<ProjectedWorldItemSnapshot> worldItems_;
	std::vector<ProjectedObjectSnapshot> objects_;
	uint32_t levelId_ = 0;
};

} // namespace devilution::authoritative

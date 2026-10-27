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

/** Returns whether a projected entity level is visible on the active level. */
[[nodiscard]] bool IsAuthoritativeLevelVisible(uint32_t projectedLevelId, uint32_t activeLevelId) noexcept;

/** Keeps world projections stable while native render and interaction code catches up. */
class ServerBackedWorldProjection {
public:
	[[nodiscard]] tl::expected<void, std::string> Apply(
	    std::vector<ProjectedMonsterSnapshot> monsters,
	    std::vector<ProjectedWorldItemSnapshot> worldItems,
	    std::vector<ProjectedObjectSnapshot> objects,
	    uint32_t levelId);
	[[nodiscard]] tl::expected<void, std::string> Apply(
	    std::vector<ProjectedMonsterSnapshot> monsters,
	    std::vector<ProjectedWorldItemSnapshot> worldItems,
	    std::vector<ProjectedObjectSnapshot> objects,
	    std::vector<ProjectedProjectileSnapshot> projectiles,
	    uint32_t levelId);
	[[nodiscard]] tl::expected<void, std::string> Apply(
	    std::vector<ProjectedMonsterSnapshot> monsters,
	    std::vector<ProjectedWorldItemSnapshot> worldItems,
	    std::vector<ProjectedObjectSnapshot> objects,
	    std::vector<ProjectedProjectileSnapshot> projectiles,
	    std::vector<ProjectedLevelGeometrySnapshot> levelGeometry,
	    uint32_t levelId,
	    std::optional<uint64_t> serverTick = std::nullopt);

	void Clear() noexcept;

	[[nodiscard]] uint32_t LevelId() const noexcept { return levelId_; }
	/** Returns the source snapshot tick when this projection came from a versioned snapshot. */
	[[nodiscard]] const std::optional<uint64_t> &ServerTick() const noexcept { return serverTick_; }
	[[nodiscard]] const std::vector<ProjectedMonsterSnapshot> &Monsters() const noexcept { return monsters_; }
	[[nodiscard]] const std::vector<ProjectedWorldItemSnapshot> &WorldItems() const noexcept { return worldItems_; }
	[[nodiscard]] const std::vector<ProjectedObjectSnapshot> &Objects() const noexcept { return objects_; }
	[[nodiscard]] const std::vector<ProjectedProjectileSnapshot> &Projectiles() const noexcept { return projectiles_; }
	[[nodiscard]] const std::vector<ProjectedLevelGeometrySnapshot> &LevelGeometry() const noexcept { return levelGeometry_; }
	/** Returns effective geometry for one authoritative level, if supplied. */
	[[nodiscard]] const ProjectedLevelGeometrySnapshot *LevelGeometryAt(uint32_t levelId) const noexcept;
	/** Returns effective geometry for the currently projected level, if supplied. */
	[[nodiscard]] const ProjectedLevelGeometrySnapshot *CurrentLevelGeometry() const noexcept { return LevelGeometryAt(levelId_); }
	/** Returns the authoritative blocked-cell result, or no value when geometry is unavailable/out of bounds. */
	[[nodiscard]] std::optional<bool> IsBlocked(int32_t positionX, int32_t positionY) const noexcept;
	/** Returns the authoritative missile-blocked result, or no value when geometry is unavailable/out of bounds. */
	[[nodiscard]] std::optional<bool> IsMissileBlocked(int32_t positionX, int32_t positionY) const noexcept;
	/** Returns the authoritative native vision-blocked result, or no value when geometry is unavailable/out of bounds. */
	[[nodiscard]] std::optional<bool> IsVisionBlocked(int32_t positionX, int32_t positionY) const noexcept;
	/** Returns native ray-table visibility from a viewer, or no value when geometry is unavailable. */
	[[nodiscard]] std::optional<bool> IsVisionVisibleFrom(
	    int32_t viewerX,
	    int32_t viewerY,
	    int32_t targetX,
	    int32_t targetY,
	    int32_t viewerLightRadius) const noexcept;

	[[nodiscard]] std::optional<uint32_t> WorldItemAt(int32_t positionX, int32_t positionY) const noexcept;
	[[nodiscard]] std::optional<uint32_t> MonsterAt(int32_t positionX, int32_t positionY) const noexcept;
	/** Finds a live projected monster only when it is inside the native viewer's authoritative vision. */
	[[nodiscard]] std::optional<uint32_t> MonsterAt(
	    int32_t positionX,
	    int32_t positionY,
	    int32_t viewerX,
	    int32_t viewerY,
	    int32_t viewerLightRadius) const noexcept;
	[[nodiscard]] std::optional<uint32_t> ObjectAt(int32_t positionX, int32_t positionY) const noexcept;
	/** Finds an active content-owned quest object at a projected cell. */
	[[nodiscard]] std::optional<uint32_t> QuestObjectAt(int32_t positionX, int32_t positionY) const noexcept;
	/** Finds an active object whose behavior is owned by the authoritative server. */
	[[nodiscard]] std::optional<uint32_t> AuthoritativeObjectAt(int32_t positionX, int32_t positionY) const noexcept;

private:
	std::vector<ProjectedMonsterSnapshot> monsters_;
	std::vector<ProjectedWorldItemSnapshot> worldItems_;
	std::vector<ProjectedObjectSnapshot> objects_;
	std::vector<ProjectedProjectileSnapshot> projectiles_;
	std::vector<ProjectedLevelGeometrySnapshot> levelGeometry_;
	uint32_t levelId_ = 0;
	std::optional<uint64_t> serverTick_;
};

} // namespace devilution::authoritative

#include "network/authoritative/server_backed_world_projection.hpp"

#include <algorithm>
#include <utility>

namespace devilution::authoritative {

tl::expected<void, std::string> ServerBackedWorldProjection::Apply(
	std::vector<ProjectedMonsterSnapshot> monsters,
	std::vector<ProjectedWorldItemSnapshot> worldItems,
	std::vector<ProjectedObjectSnapshot> objects,
	uint32_t levelId)
{
	return Apply(std::move(monsters), std::move(worldItems), std::move(objects), {}, {}, levelId);
}

tl::expected<void, std::string> ServerBackedWorldProjection::Apply(
	std::vector<ProjectedMonsterSnapshot> monsters,
	std::vector<ProjectedWorldItemSnapshot> worldItems,
	std::vector<ProjectedObjectSnapshot> objects,
	std::vector<ProjectedProjectileSnapshot> projectiles,
	uint32_t levelId)
{
	return Apply(std::move(monsters), std::move(worldItems), std::move(objects), std::move(projectiles), {}, levelId);
}

tl::expected<void, std::string> ServerBackedWorldProjection::Apply(
	std::vector<ProjectedMonsterSnapshot> monsters,
	std::vector<ProjectedWorldItemSnapshot> worldItems,
	std::vector<ProjectedObjectSnapshot> objects,
	std::vector<ProjectedProjectileSnapshot> projectiles,
	std::vector<ProjectedLevelGeometrySnapshot> levelGeometry,
	uint32_t levelId)
{
	if (std::any_of(projectiles.begin(), projectiles.end(), [](const auto &projectile) {
			return projectile.entityId == 0
			    || projectile.sourceEntityId == 0
			    || projectile.spellId == 0
			    || projectile.remainingTicks == 0
			    || projectile.damage < 0
			    || projectile.areaRadius < 0;
		})) {
		return tl::make_unexpected("Authoritative projectile projection contains invalid identity, lifetime, or effect data.");
	}
	std::sort(projectiles.begin(), projectiles.end(), [](const auto &left, const auto &right) {
		return left.entityId < right.entityId;
	});
	if (std::adjacent_find(projectiles.begin(), projectiles.end(), [](const auto &left, const auto &right) {
			return left.entityId == right.entityId;
		}) != projectiles.end()) {
		return tl::make_unexpected("Authoritative projectile projection contains duplicate entity IDs.");
	}
	std::sort(levelGeometry.begin(), levelGeometry.end(), [](const auto &left, const auto &right) {
		return left.levelId < right.levelId;
	});
	if (std::adjacent_find(levelGeometry.begin(), levelGeometry.end(), [](const auto &left, const auto &right) {
			return left.levelId == right.levelId;
		}) != levelGeometry.end()) {
		return tl::make_unexpected("Authoritative level geometry projection contains duplicate level IDs.");
	}
	// Keep native presentation and interaction lookup order canonical. Godot
	// selectors use the same entity-ID order when multiple projected entities
	// occupy one presentation cell.
	std::sort(monsters.begin(), monsters.end(), [](const auto &left, const auto &right) {
		return left.entityId < right.entityId;
	});
	std::sort(worldItems.begin(), worldItems.end(), [](const auto &left, const auto &right) {
		return left.entityId < right.entityId;
	});
	std::sort(objects.begin(), objects.end(), [](const auto &left, const auto &right) {
		return left.entityId < right.entityId;
	});
	monsters_ = std::move(monsters);
	worldItems_ = std::move(worldItems);
	objects_ = std::move(objects);
	projectiles_ = std::move(projectiles);
	levelGeometry_ = std::move(levelGeometry);
	levelId_ = levelId;
	return { };
}

void ServerBackedWorldProjection::Clear() noexcept
{
	monsters_.clear();
	worldItems_.clear();
	objects_.clear();
	projectiles_.clear();
	levelGeometry_.clear();
	levelId_ = 0;
}

const ProjectedLevelGeometrySnapshot *ServerBackedWorldProjection::LevelGeometryAt(uint32_t levelId) const noexcept
{
	const auto geometry = std::find_if(levelGeometry_.begin(), levelGeometry_.end(), [levelId](const auto &candidate) {
		return candidate.levelId == levelId;
	});
	return geometry == levelGeometry_.end() ? nullptr : &*geometry;
}

std::optional<bool> ServerBackedWorldProjection::IsBlocked(int32_t positionX, int32_t positionY) const noexcept
{
	const auto *geometry = CurrentLevelGeometry();
	if (geometry == nullptr || geometry->width <= 0 || geometry->height <= 0
	    || positionX < 0 || positionY < 0 || positionX >= geometry->width || positionY >= geometry->height)
		return std::nullopt;
	const auto cell = positionY * geometry->width + positionX;
	return std::find(geometry->blockedCells.begin(), geometry->blockedCells.end(), cell) != geometry->blockedCells.end();
}

std::optional<bool> ServerBackedWorldProjection::IsMissileBlocked(int32_t positionX, int32_t positionY) const noexcept
{
	const auto *geometry = CurrentLevelGeometry();
	if (geometry == nullptr || geometry->width <= 0 || geometry->height <= 0
	    || positionX < 0 || positionY < 0 || positionX >= geometry->width || positionY >= geometry->height)
		return std::nullopt;
	const auto cell = positionY * geometry->width + positionX;
	return std::find(geometry->missileBlockedCells.begin(), geometry->missileBlockedCells.end(), cell) != geometry->missileBlockedCells.end();
}

std::optional<uint32_t> ServerBackedWorldProjection::WorldItemAt(int32_t positionX, int32_t positionY) const noexcept
{
	const auto levelId = levelId_;
	const auto item = std::find_if(worldItems_.begin(), worldItems_.end(), [positionX, positionY, levelId](const auto &candidate) {
		return candidate.entityId != 0 && (candidate.levelId == 0 || candidate.levelId == levelId)
		    && candidate.positionX == positionX && candidate.positionY == positionY;
	});
	return item == worldItems_.end() ? std::nullopt : std::optional<uint32_t> { item->entityId };
}

std::optional<uint32_t> ServerBackedWorldProjection::MonsterAt(int32_t positionX, int32_t positionY) const noexcept
{
	const auto levelId = levelId_;
	const auto monster = std::find_if(monsters_.begin(), monsters_.end(), [positionX, positionY, levelId](const auto &candidate) {
		return candidate.entityId != 0 && candidate.alive && !candidate.sneakHidden
		    && (candidate.levelId == 0 || candidate.levelId == levelId)
		    && ((candidate.positionX == positionX && candidate.positionY == positionY)
		        || (candidate.moving && candidate.futurePositionX == positionX && candidate.futurePositionY == positionY));
	});
	if (monster == monsters_.end() || monster->entityId == 0 || !monster->alive
	    || (monster->levelId != 0 && monster->levelId != levelId))
		return std::nullopt;
	return monster->entityId;
}

std::optional<uint32_t> ServerBackedWorldProjection::ObjectAt(int32_t positionX, int32_t positionY) const noexcept
{
	const auto levelId = levelId_;
	const auto object = std::find_if(objects_.begin(), objects_.end(), [positionX, positionY, levelId](const auto &candidate) {
		return candidate.entityId != 0 && !candidate.activated && !candidate.destroyed && (candidate.levelId == 0 || candidate.levelId == levelId)
		    && candidate.positionX == positionX && candidate.positionY == positionY;
	});
	return object == objects_.end() ? std::nullopt : std::optional<uint32_t> { object->entityId };
}

std::optional<uint32_t> ServerBackedWorldProjection::QuestObjectAt(int32_t positionX, int32_t positionY) const noexcept
{
	const auto levelId = levelId_;
	const auto object = std::find_if(objects_.begin(), objects_.end(), [positionX, positionY, levelId](const auto &candidate) {
		const auto effectKind = static_cast<ProjectedObjectEffectKind>(candidate.effectKind);
		const bool isQuestEffect = effectKind == ProjectedObjectEffectKind::QuestSequence
		    || effectKind == ProjectedObjectEffectKind::QuestItemCompletion;
		return candidate.entityId != 0 && !candidate.activated && !candidate.destroyed && isQuestEffect
		    && (candidate.levelId == 0 || candidate.levelId == levelId)
		    && candidate.positionX == positionX && candidate.positionY == positionY;
	});
	return object == objects_.end() ? std::nullopt : std::optional<uint32_t> { object->entityId };
}

std::optional<uint32_t> ServerBackedWorldProjection::AuthoritativeObjectAt(int32_t positionX, int32_t positionY) const noexcept
{
	const auto levelId = levelId_;
	const auto object = std::find_if(objects_.begin(), objects_.end(), [positionX, positionY, levelId](const auto &candidate) {
		const bool hasAuthoritativeBehavior = candidate.effectKind != static_cast<int32_t>(ProjectedObjectEffectKind::None)
		    || candidate.questId != 0
		    || candidate.questTransitionKind != 0
		    || candidate.rewardKind != 0
		    || candidate.questSequenceFollowUpKind != 0;
		return candidate.entityId != 0 && !candidate.activated && !candidate.destroyed
		    && hasAuthoritativeBehavior
		    && (candidate.levelId == 0 || candidate.levelId == levelId)
		    && candidate.positionX == positionX && candidate.positionY == positionY;
	});
	return object == objects_.end() ? std::nullopt : std::optional<uint32_t> { object->entityId };
}

} // namespace devilution::authoritative

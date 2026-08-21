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
	return Apply(std::move(monsters), std::move(worldItems), std::move(objects), {}, levelId);
}

tl::expected<void, std::string> ServerBackedWorldProjection::Apply(
	std::vector<ProjectedMonsterSnapshot> monsters,
	std::vector<ProjectedWorldItemSnapshot> worldItems,
	std::vector<ProjectedObjectSnapshot> objects,
	std::vector<ProjectedProjectileSnapshot> projectiles,
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
	monsters_ = std::move(monsters);
	worldItems_ = std::move(worldItems);
	objects_ = std::move(objects);
	projectiles_ = std::move(projectiles);
	levelId_ = levelId;
	return { };
}

void ServerBackedWorldProjection::Clear() noexcept
{
	monsters_.clear();
	worldItems_.clear();
	objects_.clear();
	projectiles_.clear();
	levelId_ = 0;
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
		return candidate.entityId != 0 && candidate.alive && (candidate.levelId == 0 || candidate.levelId == levelId)
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

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
	monsters_ = std::move(monsters);
	worldItems_ = std::move(worldItems);
	objects_ = std::move(objects);
	levelId_ = levelId;
	return { };
}

void ServerBackedWorldProjection::Clear() noexcept
{
	monsters_.clear();
	worldItems_.clear();
	objects_.clear();
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

} // namespace devilution::authoritative

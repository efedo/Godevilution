#include "network/authoritative/server_backed_world_presentation.hpp"

#include <algorithm>
#include <cstdlib>
#include <limits>
#include <unordered_set>

#include "engine/lighting_defs.hpp"
#include "game/items/item_pool.hpp"
#include "game/items/items.hpp"
#include "game/levels/dungeon_common.h"
#include "game/missiles/missiles.hpp"
#include "game/monsters/monster_pool.hpp"
#include "game/monsters/monsters.hpp"
#include "game/objects/object_pool.hpp"
#include "game/objects/objects.hpp"
#include "tables/objdat.h"

namespace devilution::authoritative {
namespace {

bool IsCurrentLevel(uint32_t entityLevelId, uint32_t currentLevelId)
{
	return entityLevelId == 0 || entityLevelId == currentLevelId;
}

bool IsValidWorldPosition(int32_t x, int32_t y)
{
	return x >= 0 && y >= 0
	    && x <= std::numeric_limits<WorldTileCoord>::max()
	    && y <= std::numeric_limits<WorldTileCoord>::max()
	    && InDungeonBounds({ x, y });
}

/** Returns the native sprite family that can be presented without simulation. */
std::optional<MissileID> NativePresentationMissileId(int behavior)
{
	switch (behavior) {
	case 1: // ChargedBolt
		return MissileID::ChargedBolt;
	case 2: // Firebolt
		return MissileID::Firebolt;
	case 3: // Lightning
		return MissileID::Lightning;
	case 15: // Phasing
		return MissileID::Phasing;
	case 16: // Fireball
		return MissileID::Fireball;
	case 19: // HolyBolt
		return MissileID::HolyBolt;
	case 20: // BloodStar
		return MissileID::BloodStar;
	case 21: // BoneSpirit
		return MissileID::BoneSpirit;
	case 23: // FireWallSegment
		return MissileID::FireWall;
	case 29: // ChainLightningBolt
		return MissileID::ChainLightning;
	case 36: // GuardianController
		return MissileID::Guardian;
	case 49: // Etherealize
		return MissileID::Etherealize;
	default:
		return std::nullopt;
	}
}

Missile *FindAuthoritativePresentationMissile(uint32_t entityId)
{
	const auto missile = std::find_if(Missiles.begin(), Missiles.end(), [entityId](const auto &candidate) {
		return candidate._miAuthoritativeEntityId == entityId;
	});
	return missile == Missiles.end() ? nullptr : &*missile;
}

Missile *CreateAuthoritativePresentationMissile(
	uint32_t entityId,
	MissileID missileId,
	const ProjectedProjectileSnapshot &snapshot)
{
	if (Missiles.size() >= Missiles.max_size())
		return nullptr;

	Missiles.emplace_back();
	Missile &missile = Missiles.back();
	const auto &missileData = GetMissileData(missileId);
	const WorldTilePosition position { static_cast<WorldTileCoord>(snapshot.positionX), static_cast<WorldTileCoord>(snapshot.positionY) };
	missile._mitype = missileId;
	missile._misource = -1;
	missile._micaster = TARGET_BOTH;
	missile._miAnimAdd = 1;
	missile._miAnimType = missileData.graphic;
	missile._miDrawFlag = missileData.isDrawn();
	missile._miLightFlag = false;
	missile._mlid = NO_LIGHT;
	missile._miAuthoritativeEntityId = entityId;
	missile.position.tile = position;
	missile.position.start = position;
	missile.position.tileForRendering = position;
	missile.position.offset = {};
	missile.position.offsetForRendering = {};
	missile.position.velocity = {};
	missile.position.traveled = {};
	missile.setDefaultFrameGroup();
	return &missile;
}

void RemoveAuthoritativePresentationMissile(uint32_t entityId)
{
	Missiles.remove_if([entityId](const auto &missile) {
		return missile._miAuthoritativeEntityId == entityId;
	});
}

void ApplyAuthoritativePresentationMissile(
	Missile &missile,
	const ProjectedProjectileSnapshot &snapshot)
{
	const WorldTilePosition position { static_cast<WorldTileCoord>(snapshot.positionX), static_cast<WorldTileCoord>(snapshot.positionY) };
	// The server already resolved movement. Do not reuse native velocity or
	// collision interpolation, which would turn this presentation object into
	// a second local simulation.
	missile.position.tile = position;
	missile.position.start = position;
	missile.position.tileForRendering = position;
	missile.position.offset = {};
	missile.position.offsetForRendering = {};
	missile.position.velocity = {};
	missile.position.traveled = {};
	missile.duration = static_cast<int>(std::min<uint32_t>(snapshot.remainingTicks, std::numeric_limits<int>::max()));
	missile._miDelFlag = false;
	if (missile._mitype == MissileID::Guardian) {
		missile.setFrameGroup<GuardianFrame>(GuardianFrame::Idle);
	} else if (snapshot.originalDirection >= 0 && snapshot.originalDirection <= 7) {
		missile.setFrameGroupRaw(snapshot.originalDirection * 2);
	}
}

int FindActiveItemListIndex(unsigned itemSlot)
{
	for (int activeIndex = 0; activeIndex < ItemPoolAdapter::ActiveItemCountValue(); ++activeIndex) {
		if (ItemPoolAdapter::ActiveItemIds()[activeIndex] == itemSlot)
			return activeIndex;
	}
	return -1;
}

int FindActiveMonsterListIndex(unsigned monsterSlot)
{
	for (size_t activeIndex = 0; activeIndex < MonsterPoolAdapter::ActiveMonsterCountValue(); ++activeIndex) {
		if (MonsterPoolAdapter::ActiveMonsterIds()[activeIndex] == monsterSlot)
			return static_cast<int>(activeIndex);
	}
	return -1;
}

int FindActiveObjectListIndex(int objectSlot)
{
	for (int activeIndex = 0; activeIndex < ObjectPoolAdapter::ActiveObjectCountValue(); ++activeIndex) {
		if (ObjectPoolAdapter::ActiveObjectIndexAt(activeIndex) == objectSlot)
			return activeIndex;
	}
	return -1;
}

void ClearMonsterTile(unsigned monsterSlot, const Point &position)
{
	if (InDungeonBounds(position)
	    && std::abs(tileAt(position).monster()) == static_cast<int>(monsterSlot + 1)) {
		tileAt(position).setMonster(0);
	}
}

void ApplyMonsterSnapshot(Monster &monster, const ProjectedMonsterSnapshot &snapshot)
{
	const Point position { snapshot.positionX, snapshot.positionY };
	const Point futurePosition { snapshot.futurePositionX, snapshot.futurePositionY };
	const Point oldPosition { monster.position.tile.x, monster.position.tile.y };
	const Point oldFuturePosition { monster.position.future.x, monster.position.future.y };
	ClearMonsterTile(static_cast<unsigned>(monster.getId()), oldPosition);
	if (oldFuturePosition != oldPosition)
		ClearMonsterTile(static_cast<unsigned>(monster.getId()), oldFuturePosition);
	if (!snapshot.petrified && monster.mode == MonsterMode::Petrified)
		monster.startStand(monster.direction);
	monster.position.old = position;
	monster.position.last = position;
	monster.position.future = futurePosition;
	monster.position.tile = position;
	if (snapshot.alive) {
		monster.occupyTile(position, false);
		if (snapshot.moving)
			monster.occupyTile(futurePosition, true);
	}
	monster.animInfo.isPetrified = snapshot.petrified;
	if (snapshot.petrified)
		monster.mode = MonsterMode::Petrified;
	const int32_t hitPointsQ6 = snapshot.hitPointsQ6 == 0 && snapshot.hitPoints != 0
	    ? static_cast<int32_t>(static_cast<int64_t>(snapshot.hitPoints) * 64)
	    : snapshot.hitPointsQ6;
	const int32_t maxHitPointsQ6 = snapshot.maxHitPointsQ6 == 0 && snapshot.maxHitPoints != 0
	    ? static_cast<int32_t>(static_cast<int64_t>(snapshot.maxHitPoints) * 64)
	    : snapshot.maxHitPointsQ6;
	monster.hitPoints = std::max(0, hitPointsQ6);
	monster.maxHitPoints = std::max(monster.hitPoints, std::max(0, maxHitPointsQ6));
	monster.armorClass = static_cast<uint8_t>(std::clamp(snapshot.armorClass, 0, 255));
	monster.minDamage = static_cast<uint8_t>(std::clamp(snapshot.attackDamage, 0, 255));
	monster.maxDamage = monster.minDamage;
}

void ApplyItemSnapshot(Item &item, const ProjectedWorldItemSnapshot &snapshot)
{
	const auto animationInfo = item.animInfo;
	const auto animationFlag = item._iAnimFlag;
	const auto selectionRegion = item.selectionRegion;
	const auto postDraw = item._iPostDraw;
	item = snapshot.item;
	item.position = { snapshot.positionX, snapshot.positionY };
	item.animInfo = animationInfo;
	item._iAnimFlag = animationFlag;
	item.selectionRegion = selectionRegion;
	item._iPostDraw = postDraw;
}

void ApplyObjectSnapshot(Object &object, int objectSlot, const ProjectedObjectSnapshot &snapshot)
{
	const Point position { snapshot.positionX, snapshot.positionY };
	if (object.position != position) {
		if (InDungeonBounds(object.position) && tileAt(object.position).object() == objectSlot + 1)
			tileAt(object.position).setObject(0);
		object.position = position;
	}
	if (!snapshot.activated && !snapshot.destroyed)
		tileAt(position).setObject(objectSlot + 1);
	object._oDelFlag = snapshot.activated || snapshot.destroyed;
}

void RemoveMappedItem(unsigned itemSlot)
{
	if (const int activeIndex = FindActiveItemListIndex(itemSlot); activeIndex >= 0) {
		if (InDungeonBounds(Items[itemSlot].position) && tileAt(Items[itemSlot].position).item() == static_cast<int>(itemSlot + 1))
			tileAt(Items[itemSlot].position).setItem(0);
		DeleteItem(activeIndex);
	}
}

void RemoveMappedMonster(unsigned monsterSlot)
{
	if (const int activeIndex = FindActiveMonsterListIndex(monsterSlot); activeIndex >= 0) {
		const auto &monster = Monsters[monsterSlot];
		ClearMonsterTile(monsterSlot, monster.position.tile);
		if (monster.position.future != monster.position.tile)
			ClearMonsterTile(monsterSlot, monster.position.future);
		DeleteMonsterAtActiveIndex(activeIndex);
	}
}

void RemoveMappedObject(int objectSlot)
{
	if (const int activeIndex = FindActiveObjectListIndex(objectSlot); activeIndex >= 0) {
		if (InDungeonBounds(Objects[objectSlot].position) && tileAt(Objects[objectSlot].position).object() == objectSlot + 1)
			tileAt(Objects[objectSlot].position).setObject(0);
		if (ObjectUnderCursor == &Objects[objectSlot])
			ObjectUnderCursor = nullptr;
		ObjectPoolAdapter::ReleaseObjectSlot(objectSlot, activeIndex);
	}
}

} // namespace

tl::expected<void, std::string> ServerBackedWorldPresentation::Apply(
    const ServerBackedWorldProjection &projection,
    uint32_t levelId)
{
	std::unordered_set<uint32_t> currentMonsterEntities;
	std::unordered_set<uint32_t> currentItemEntities;
	std::unordered_set<uint32_t> currentObjectEntities;
	std::unordered_set<uint32_t> currentProjectileEntities;
	std::unordered_set<unsigned> claimedMonsterSlots;
	std::unordered_set<unsigned> claimedItemSlots;
	std::unordered_set<int> claimedObjectSlots;

	for (const auto &snapshot : projection.Monsters()) {
		if (!IsCurrentLevel(snapshot.levelId, levelId))
			continue;
		if (!IsValidWorldPosition(snapshot.positionX, snapshot.positionY))
			return tl::make_unexpected("Authoritative monster position cannot be represented by the native world tile type.");
		if (snapshot.moving && !IsValidWorldPosition(snapshot.futurePositionX, snapshot.futurePositionY))
			return tl::make_unexpected("Authoritative moving monster destination cannot be represented by the native world tile type.");
		currentMonsterEntities.insert(snapshot.entityId);
		unsigned slot = std::numeric_limits<unsigned>::max();
		if (const auto mapped = monsterSlots_.find(snapshot.entityId); mapped != monsterSlots_.end()
		    && FindActiveMonsterListIndex(mapped->second) >= 0)
			slot = mapped->second;
		if (slot == std::numeric_limits<unsigned>::max()) {
			for (const auto activeSlot : MonsterPoolAdapter::ActiveMonsterRange()) {
				if (claimedMonsterSlots.contains(activeSlot))
					continue;
				if (static_cast<uint32_t>(Monsters[activeSlot].type().type) == snapshot.monsterId) {
					slot = activeSlot;
					break;
				}
			}
		}
		if (slot == std::numeric_limits<unsigned>::max() && snapshot.alive && MonsterPoolAdapter::HasFreeMonsterSlot()) {
			if (const auto typeIndex = FindNativeMonsterTypeIndex(snapshot.monsterId); typeIndex.has_value()) {
				if (Monster *monster = AddMonster({ snapshot.positionX, snapshot.positionY }, Direction::South, *typeIndex, true); monster != nullptr)
					slot = static_cast<unsigned>(monster->getId());
			}
		}
		if (slot == std::numeric_limits<unsigned>::max())
			continue;
		claimedMonsterSlots.insert(slot);
		monsterSlots_[snapshot.entityId] = slot;
		ApplyMonsterSnapshot(Monsters[slot], snapshot);
	}

	for (const auto &snapshot : projection.WorldItems()) {
		if (!IsCurrentLevel(snapshot.levelId, levelId))
			continue;
		if (!IsValidWorldPosition(snapshot.positionX, snapshot.positionY))
			return tl::make_unexpected("Authoritative item position cannot be represented by the native world tile type.");
		currentItemEntities.insert(snapshot.entityId);
		unsigned slot = std::numeric_limits<unsigned>::max();
		if (const auto mapped = itemSlots_.find(snapshot.entityId); mapped != itemSlots_.end()
		    && FindActiveItemListIndex(mapped->second) >= 0)
			slot = mapped->second;
		if (slot == std::numeric_limits<unsigned>::max()) {
			for (const auto activeSlot : ItemPoolAdapter::ActiveItemIds().subspan(0, ItemPoolAdapter::ActiveItemCountValue())) {
				if (claimedItemSlots.contains(activeSlot))
					continue;
				if (Items[activeSlot]._iSeed == snapshot.itemSeed) {
					slot = activeSlot;
					break;
				}
			}
		}
		if (slot == std::numeric_limits<unsigned>::max()) {
			if (!ItemPoolAdapter::HasFreeItemSlot())
				continue;
			slot = PlaceItemInWorld(Item { snapshot.item }, { static_cast<WorldTileCoord>(snapshot.positionX), static_cast<WorldTileCoord>(snapshot.positionY) });
		}
		claimedItemSlots.insert(slot);
		itemSlots_[snapshot.entityId] = slot;
		ApplyItemSnapshot(Items[slot], snapshot);
	}

	for (const auto &snapshot : projection.Objects()) {
		if (!IsCurrentLevel(snapshot.levelId, levelId))
			continue;
		if (!IsValidWorldPosition(snapshot.positionX, snapshot.positionY))
			return tl::make_unexpected("Authoritative object position cannot be represented by the native world tile type.");
		currentObjectEntities.insert(snapshot.entityId);
		int slot = -1;
		if (const auto mapped = objectSlots_.find(snapshot.entityId); mapped != objectSlots_.end()
		    && FindActiveObjectListIndex(mapped->second) >= 0)
			slot = mapped->second;
		if (slot < 0) {
			for (int activeIndex = 0; activeIndex < ObjectPoolAdapter::ActiveObjectCountValue(); ++activeIndex) {
				const int candidate = ObjectPoolAdapter::ActiveObjectIndexAt(activeIndex);
				if (claimedObjectSlots.contains(candidate))
					continue;
				if (static_cast<uint32_t>(Objects[candidate]._otype) == snapshot.objectId) {
					slot = candidate;
					break;
				}
			}
		}
		if (slot < 0 && snapshot.objectId <= static_cast<uint32_t>(OBJ_LAST) && ObjectPoolAdapter::HasFreeObjectSlot()) {
			if (Object *object = AddObject(static_cast<_object_id>(snapshot.objectId), { snapshot.positionX, snapshot.positionY }); object != nullptr)
				slot = static_cast<int>(object - Objects);
		}
		if (slot < 0)
			continue;
		claimedObjectSlots.insert(slot);
		objectSlots_[snapshot.entityId] = slot;
		ApplyObjectSnapshot(Objects[slot], slot, snapshot);
	}

	for (const auto &snapshot : projection.Projectiles()) {
		if (!IsCurrentLevel(snapshot.levelId, levelId))
			continue;
		const auto missileId = NativePresentationMissileId(snapshot.behavior);
		if (!missileId)
			continue;
		if (!IsValidWorldPosition(snapshot.positionX, snapshot.positionY))
			return tl::make_unexpected("Authoritative projectile position cannot be represented by the native world tile type.");

		Missile *missile = FindAuthoritativePresentationMissile(snapshot.entityId);
		if (missile != nullptr && missile->_mitype != *missileId) {
			RemoveAuthoritativePresentationMissile(snapshot.entityId);
			missile = nullptr;
		}
		if (missile == nullptr)
			missile = CreateAuthoritativePresentationMissile(snapshot.entityId, *missileId, snapshot);
		if (missile == nullptr)
			continue;
		ApplyAuthoritativePresentationMissile(*missile, snapshot);
		currentProjectileEntities.insert(snapshot.entityId);
		projectileEntities_.insert(snapshot.entityId);
	}

	for (auto it = itemSlots_.begin(); it != itemSlots_.end();) {
		if (currentItemEntities.contains(it->first)) {
			++it;
			continue;
		}
		if (const int activeIndex = FindActiveItemListIndex(it->second); activeIndex >= 0) {
			RemoveMappedItem(it->second);
		}
		it = itemSlots_.erase(it);
	}
	for (auto it = monsterSlots_.begin(); it != monsterSlots_.end();) {
		if (currentMonsterEntities.contains(it->first)) {
			++it;
			continue;
		}
		if (const int activeIndex = FindActiveMonsterListIndex(it->second); activeIndex >= 0) {
			RemoveMappedMonster(it->second);
		}
		it = monsterSlots_.erase(it);
	}
	for (auto it = objectSlots_.begin(); it != objectSlots_.end();) {
		if (currentObjectEntities.contains(it->first)) {
			++it;
			continue;
		}
		if (const int activeIndex = FindActiveObjectListIndex(it->second); activeIndex >= 0) {
			RemoveMappedObject(it->second);
		}
		it = objectSlots_.erase(it);
	}
	for (auto it = projectileEntities_.begin(); it != projectileEntities_.end();) {
		if (currentProjectileEntities.contains(*it)) {
			++it;
			continue;
		}
		RemoveAuthoritativePresentationMissile(*it);
		it = projectileEntities_.erase(it);
	}

	return { };
}

void ServerBackedWorldPresentation::Clear() noexcept
{
	for (const auto &[entityId, itemSlot] : itemSlots_)
		RemoveMappedItem(itemSlot);
	for (const auto &[entityId, monsterSlot] : monsterSlots_)
		RemoveMappedMonster(monsterSlot);
	for (const auto &[entityId, objectSlot] : objectSlots_)
		RemoveMappedObject(objectSlot);
	for (const auto entityId : projectileEntities_)
		RemoveAuthoritativePresentationMissile(entityId);
	monsterSlots_.clear();
	itemSlots_.clear();
	objectSlots_.clear();
	projectileEntities_.clear();
}

} // namespace devilution::authoritative

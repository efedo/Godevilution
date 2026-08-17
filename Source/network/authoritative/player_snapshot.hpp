#pragma once

/**
 * @file network/authoritative/player_snapshot.hpp
 *
 * Protocol-free native projection of authoritative player state.
 */

#include <cstdint>
#include <optional>
#include <string>
#include <vector>

#include <expected.hpp>

#include "game/items/items.hpp"

namespace devilution::protocol::v1 {
class Snapshot;
class EventBatch;
} // namespace devilution::protocol::v1

namespace devilution {
struct Player;
}

namespace devilution::authoritative {

struct ProjectedPlayerAttribute {
	int32_t base = 0;
	int32_t current = 0;
};

struct ProjectedInventoryItem {
	uint32_t storeId = 0;
	uint32_t storeSlot = 0;
	uint32_t itemSeed = 0;
	/** Stable content identity projected from the authoritative snapshot. */
	uint32_t stableItemId = 0;
	uint32_t price = 0;
	uint64_t purchasedAtTick = 0;
	Item item;
};

struct ProjectedEquippedItem {
	uint32_t slot = 0;
	uint32_t itemSeed = 0;
	uint32_t stableItemId = 0;
	Item item;
};

struct ProjectedBeltItem {
	uint32_t slot = 0;
	uint32_t itemSeed = 0;
	uint32_t stableItemId = 0;
	Item item;
};

struct ProjectedStatusEffect {
	uint32_t effectId = 0;
	uint32_t remainingTicks = 0;
	int32_t magnitude = 0;
	uint32_t behavior = 0;
};

struct ProjectedMemorizedSpell {
	uint32_t spellId = 0;
	uint32_t level = 0;
};

struct ProjectedMonsterSnapshot {
	uint32_t entityId = 0;
	uint32_t monsterId = 0;
	uint32_t levelId = 0;
	int32_t positionX = 0;
	int32_t positionY = 0;
	int32_t hitPoints = 0;
	int32_t maxHitPoints = 0;
	int32_t hitPointsQ6 = 0;
	int32_t maxHitPointsQ6 = 0;
	int32_t armorClass = 0;
	bool alive = false;
	int32_t attackDamage = 0;
	int32_t aggroRange = 0;
	int32_t fireResistance = 0;
	int32_t lightningResistance = 0;
	int32_t magicResistance = 0;
	bool moving = false;
	int32_t futurePositionX = 0;
	int32_t futurePositionY = 0;
	bool petrified = false;
	bool stoneCurseImmune = false;
};

struct ProjectedWorldItemSnapshot {
	uint32_t entityId = 0;
	uint32_t levelId = 0;
	int32_t positionX = 0;
	int32_t positionY = 0;
	uint32_t itemSeed = 0;
	uint32_t price = 0;
	Item item;
};

struct ProjectedObjectSnapshot {
	uint32_t entityId = 0;
	uint32_t objectId = 0;
	uint32_t levelId = 0;
	int32_t positionX = 0;
	int32_t positionY = 0;
	bool activated = false;
	uint32_t questId = 0;
	int32_t effectKind = 0;
	int32_t effectAmount = 0;
	bool missilePassable = false;
	bool breakable = false;
	bool destroyed = false;
};

struct ProjectedProjectileSnapshot {
	uint32_t entityId = 0;
	uint32_t sourceEntityId = 0;
	uint32_t targetEntityId = 0;
	uint32_t spellId = 0;
	uint32_t levelId = 0;
	int32_t positionX = 0;
	int32_t positionY = 0;
	int32_t targetX = 0;
	int32_t targetY = 0;
	int32_t damage = 0;
	int32_t damageType = 0;
	int32_t areaRadius = 0;
	uint32_t remainingTicks = 0;
	int32_t behavior = 0;
	int32_t phase = 0;
	int32_t startX = 0;
	int32_t startY = 0;
	int32_t traveledX = 0;
	int32_t traveledY = 0;
	int32_t velocityX = 0;
	int32_t velocityY = 0;
	int32_t originalDirection = 0;
	int32_t pathIndex = 0;
	int32_t pathTicks = 0;
	int32_t speed = 0;
	int32_t impactLifetime = 0;
	int32_t resistantDamageDivisor = 0;
	uint32_t lastCollisionTargetEntityId = 0;
	int32_t effectiveSpellLevel = 0;
};

struct ProjectedPlayerActionSnapshot {
	uint32_t kind = 0;
	uint64_t startedAtTick = 0;
	uint64_t effectTick = 0;
	uint64_t readyTick = 0;
	uint64_t endTick = 0;
	bool effectApplied = false;
	int32_t directionX = 0;
	int32_t directionY = 0;
	uint32_t targetEntityId = 0;
	bool hasCast = false;
	uint32_t spellId = 0;
	uint32_t castTargetEntityId = 0;
	int32_t targetX = 0;
	int32_t targetY = 0;
	uint32_t castSource = 0;
	bool hasCastSourceItem = false;
	uint32_t castSourceItemLocation = 0;
	uint32_t castSourceItemSlot = 0;
	uint32_t castSourceItemStableId = 0;
};

struct ProjectedPlayerSnapshot {
	uint32_t entityId = 0;
	int32_t positionX = 0;
	int32_t positionY = 0;
	int32_t life = 0;
	int32_t lifeMaximum = 0;
	int32_t lifeMaximumBase = 0;
	int32_t mana = 0;
	int32_t manaMaximum = 0;
	int32_t manaMaximumBase = 0;
	uint32_t gold = 0;
	uint32_t experience = 0;
	uint32_t characterLevel = 1;
	uint32_t characterClassId = 0;
	std::string characterName;
	bool manaShieldActive = false;
	uint32_t abilitySpellId = 0;
	uint32_t levelId = 0;
	bool friendlyMode = true;
	uint32_t facingDirection = 0;
	uint32_t combatMode = 0;
	uint64_t combatModeEndTick = 0;
	std::optional<ProjectedPlayerActionSnapshot> currentAction;
	ProjectedPlayerAttribute strength;
	ProjectedPlayerAttribute magic;
	ProjectedPlayerAttribute dexterity;
	ProjectedPlayerAttribute vitality;
	std::optional<uint32_t> activeStoreId;
	std::vector<ProjectedInventoryItem> inventory;
	std::optional<ProjectedInventoryItem> heldItem;
	std::vector<ProjectedEquippedItem> equipment;
	std::vector<ProjectedBeltItem> belt;
	std::vector<int32_t> inventoryGrid;
	std::vector<ProjectedStatusEffect> statusEffects;
	std::vector<ProjectedMemorizedSpell> memorizedSpells;
};

/** Projects exactly one entity's player state from a server snapshot. */
[[nodiscard]] tl::expected<ProjectedPlayerSnapshot, std::string> ProjectPlayerSnapshot(
    const ::devilution::protocol::v1::Snapshot &snapshot,
    uint32_t entityId);

/** Projects every authoritative player in stable entity-ID order. */
[[nodiscard]] tl::expected<std::vector<ProjectedPlayerSnapshot>, std::string> ProjectPlayerSnapshots(
    const ::devilution::protocol::v1::Snapshot &snapshot);

/** Projects the authoritative combat entities in stable entity-ID order. */
[[nodiscard]] tl::expected<std::vector<ProjectedMonsterSnapshot>, std::string> ProjectMonsterSnapshots(
    const ::devilution::protocol::v1::Snapshot &snapshot);

/** Projects authoritative world items in stable entity-ID order. */
[[nodiscard]] tl::expected<std::vector<ProjectedWorldItemSnapshot>, std::string> ProjectWorldItemSnapshots(
    const ::devilution::protocol::v1::Snapshot &snapshot);
[[nodiscard]] tl::expected<std::vector<ProjectedObjectSnapshot>, std::string> ProjectObjectSnapshots(
    const ::devilution::protocol::v1::Snapshot &snapshot);
[[nodiscard]] tl::expected<std::vector<ProjectedProjectileSnapshot>, std::string> ProjectProjectileSnapshots(
    const ::devilution::protocol::v1::Snapshot &snapshot);

/** Applies server-authored combat and experience events to the native projection before its snapshot arrives. */
void ApplyServerBackedEventBatch(
    Player &player,
    const ::devilution::protocol::v1::EventBatch &eventBatch,
    uint32_t entityId);

/** Reconnect-safe owner of the latest validated authoritative player state. */
class ServerBackedPlayerState {
public:
	[[nodiscard]] bool ApplySnapshot(ProjectedPlayerSnapshot snapshot);
	void Clear() noexcept;

	[[nodiscard]] bool HasSnapshot() const noexcept { return snapshot_.has_value(); }
	[[nodiscard]] const ProjectedPlayerSnapshot *Snapshot() const noexcept { return snapshot_ ? &*snapshot_ : nullptr; }
	[[nodiscard]] const ProjectedInventoryItem *FindInventoryItem(std::size_t index) const noexcept;

private:
	std::optional<ProjectedPlayerSnapshot> snapshot_;
};

} // namespace devilution::authoritative

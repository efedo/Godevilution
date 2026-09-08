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

/** Server-owned semantic targeting role for a spell source. */
enum class ProjectedSpellTargetingKind : uint32_t {
	Unspecified = 0,
	Tile = 1,
	Player = 2,
	CombatEntity = 3,
	WorldEntity = 4,
};

/** Presentation-side result for resolving a cursor target to an authoritative entity. */
enum class ServerBackedSpellTargetResolution : uint8_t {
	Tile,
	Player,
	CombatEntity,
	MissingAuthoritativeEntity,
};

/**
 * Resolves the native cursor precedence used by connected spell submission.
 *
 * A cursor target that is expected to be an authoritative player or combat
 * entity must not silently fall back to a tile cast when its entity mapping is
 * absent. Tile-targeted spells intentionally ignore creature cursors.
 */
[[nodiscard]] constexpr ServerBackedSpellTargetResolution ResolveServerBackedSpellTarget(
	bool allowsPlayerTarget,
	bool allowsCombatEntityTarget,
	bool playerUnderCursor,
	bool monsterUnderCursor,
	bool hasPlayerEntity,
	bool hasMonsterEntity) noexcept
{
	if (allowsPlayerTarget && playerUnderCursor)
		return hasPlayerEntity
		    ? ServerBackedSpellTargetResolution::Player
		    : ServerBackedSpellTargetResolution::MissingAuthoritativeEntity;
	if (allowsCombatEntityTarget && monsterUnderCursor)
		return hasMonsterEntity
		    ? ServerBackedSpellTargetResolution::CombatEntity
		    : ServerBackedSpellTargetResolution::MissingAuthoritativeEntity;
	return ServerBackedSpellTargetResolution::Tile;
}

struct ProjectedInventoryItem {
	uint32_t storeId = 0;
	uint32_t storeSlot = 0;
	uint32_t itemSeed = 0;
	/** Stable content identity projected from the authoritative snapshot. */
	uint32_t stableItemId = 0;
	uint32_t spellId = 0;
	ProjectedSpellTargetingKind spellTargetingKind = ProjectedSpellTargetingKind::Unspecified;
	uint32_t price = 0;
	uint64_t purchasedAtTick = 0;
	Item item;
};

/** One server-owned item anchored in the native-compatible stash grid. */
struct ProjectedStashItem {
	uint32_t page = 0;
	uint32_t cell = 0;
	uint32_t itemSeed = 0;
	uint32_t stableItemId = 0;
	uint32_t spellId = 0;
	ProjectedSpellTargetingKind spellTargetingKind = ProjectedSpellTargetingKind::Unspecified;
	uint32_t price = 0;
	uint64_t purchasedAtTick = 0;
	uint32_t width = 1;
	uint32_t height = 1;
	Item item;
};

struct ProjectedEquippedItem {
	uint32_t slot = 0;
	uint32_t itemSeed = 0;
	uint32_t stableItemId = 0;
	uint32_t spellId = 0;
	ProjectedSpellTargetingKind spellTargetingKind = ProjectedSpellTargetingKind::Unspecified;
	Item item;
};

struct ProjectedBeltItem {
	uint32_t slot = 0;
	uint32_t itemSeed = 0;
	uint32_t stableItemId = 0;
	uint32_t spellId = 0;
	ProjectedSpellTargetingKind spellTargetingKind = ProjectedSpellTargetingKind::Unspecified;
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
	ProjectedSpellTargetingKind targetingKind = ProjectedSpellTargetingKind::Unspecified;
};

struct ProjectedMonsterSnapshot {
	uint32_t entityId = 0;
	uint32_t monsterId = 0;
	bool isUnique = false;
	std::string uniqueIdentity;
	/** Server-owned speech identity currently displayed by this unique encounter. */
	int32_t dialogueMessageId = 0;
	/** Server-owned authored dialogue state; zero preserves legacy fallback. */
	int32_t dialogueState = 0;
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
	/** Native monster direction used by compatibility presentation. */
	int32_t direction = 0;
	bool moving = false;
	int32_t futurePositionX = 0;
	int32_t futurePositionY = 0;
	/** Server-owned one-tick movement timing retained for presentation clients. */
	uint64_t movementStartedTick = 0;
	uint64_t movementEffectTick = 0;
	bool petrified = false;
	/** Server-owned stealth state; hidden monsters are not presentation targets. */
	bool sneakHidden = false;
	bool stoneCurseImmune = false;
	/** Authoritative native pack leader entity, when this monster is a minion. */
	uint32_t leaderEntityId = 0;
	/** Native pack relation: 0 none, 1 leashed, 2 separated. */
	int32_t leaderRelation = 0;
	/** Number of living members retained by a native pack leader. */
	int32_t packSize = 0;
	/** Native activation countdown used by FollowTheLeader. */
	int32_t activeForTicks = 0;
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

enum class ProjectedObjectEffectKind : int32_t {
	None = 0,
	Heal = 1,
	Damage = 2,
	Experience = 3,
	QuestSequence = 4,
	QuestItemCompletion = 5,
	WorldMutation = 6,
	GrantSpellLevel = 7,
	SpawnSpellProjectile = 8,
	GrantSpellLevelAndSpawnProjectile = 9,
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
	/** Server-owned trap state used by authoritative disarm interaction. */
	bool trapArmed = false;
	/** Optional native object type used only by compatibility presentations. */
	uint32_t nativeObjectId = 0;
	/** Presentation-only lifecycle metadata for activated objects. */
	bool keepVisibleAfterActivation = false;
	uint32_t animationFrame = 0;
	uint32_t activatedAnimationFrame = 0;
	/** Authored quest-state transition identity, when present. */
	int32_t questTransitionKind = 0;
	/** Authored item reward identity, when present. */
	int32_t rewardKind = 0;
	/** Ordered quest/world-mutation follow-up identity, when present. */
	int32_t questSequenceFollowUpKind = 0;
	/** Optional authored dialogue identity emitted when the object activates. */
	int32_t dialogueMessageId = 0;
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
	/** Server-selected native compatibility sprite; zero retains behavior fallback. */
	int32_t nativeVisual = 0;
	/** Content-owned native missile identity; empty retains compatibility fallback. */
	std::string nativeMissileId;
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

struct ProjectedPortalSnapshot {
	uint32_t portalId = 0;
	uint32_t sourceLevelId = 0;
	int32_t sourcePositionX = 0;
	int32_t sourcePositionY = 0;
	uint32_t destinationLevelId = 0;
	int32_t destinationPositionX = 0;
	int32_t destinationPositionY = 0;
	uint32_t remainingTicks = 0;
	uint32_t ownerEntityId = 0;
	bool bidirectional = false;
};

/** Server-owned corpse tile retained for future native presentation parity. */
struct ProjectedCorpseSnapshot {
	uint32_t entityId = 0;
	uint32_t levelId = 0;
	int32_t positionX = 0;
	int32_t positionY = 0;
};

enum class ProjectedGameDifficulty : uint32_t {
	Normal = 1,
	Nightmare = 2,
	Hell = 3,
};

/** Server-owned session metadata retained for native presentation and parity. */
struct ProjectedSessionMetadata {
	uint32_t randomState = 0;
	bool victoryTriggered = false;
	ProjectedGameDifficulty difficulty = ProjectedGameDifficulty::Normal;
	std::vector<uint32_t> activeWorldMutationIds;
};

/** Server-owned native quest state used by the compatibility presentation. */
struct ProjectedQuestSnapshot {
	uint32_t nativeQuestId = 0;
	bool hasNativeQuestId = false;
	bool completed = false;
	bool unavailable = false;
	bool initial = false;
	bool logged = false;
	uint8_t variable1 = 0;
	uint8_t variable2 = 0;
	int16_t messageId = 0;
};

struct ProjectedWarpTriggerSnapshot {
	uint32_t warpId = 0;
	int32_t triggerX = 0;
	int32_t triggerY = 0;
	uint32_t targetLevelId = 0;
	int32_t targetX = 0;
	int32_t targetY = 0;
	uint32_t requiredQuestId = 0;
	bool requiredQuestCompleted = false;
	std::string stableSymbol;
	bool available = false;
};

/** Server-owned effective level geometry retained for native presentation. */
struct ProjectedLevelGeometrySnapshot {
	uint32_t levelId = 0;
	int32_t width = 0;
	int32_t height = 0;
	std::vector<int32_t> blockedCells;
	std::vector<int32_t> missileBlockedCells;
	std::vector<uint16_t> nativeTransparency;
	std::vector<ProjectedWarpTriggerSnapshot> warpTriggers;
	std::vector<uint16_t> nativeObjectLayer;
	std::vector<uint16_t> nativeMegaTileLayer;
	std::vector<uint16_t> nativePieceLayer;
};

/** Returns whether a content-owned native missile identity has valid wire syntax. */
[[nodiscard]] bool IsValidNativeMissileIdentity(const std::string &value);

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
	/** Native-clamped equipment-derived player vision radius. */
	int32_t lightRadius = 0;
	uint32_t gold = 0;
	uint32_t experience = 0;
	uint32_t characterLevel = 1;
	/** Persistent server-owned native hero rank after a Diablo defeat. */
	uint32_t diabloKillLevel = 0;
	/** Remaining server-owned Reflect charges, matching native wReflections. */
	uint32_t reflectCharges = 0;
	uint32_t statPoints = 0;
	uint32_t characterClassId = 0;
	std::string characterName;
	bool manaShieldActive = false;
	uint32_t abilitySpellId = 0;
	ProjectedSpellTargetingKind abilitySpellTargetingKind = ProjectedSpellTargetingKind::Unspecified;
	uint32_t levelId = 0;
	/** Server-owned native _pLvlVisited flags keyed by authored level ID. */
	std::vector<uint32_t> visitedLevelIds;
	bool friendlyMode = true;
	uint32_t facingDirection = 0;
	uint32_t combatMode = 0;
	uint64_t combatModeEndTick = 0;
	/** Authoritative server tick used to place presentation animations deterministically. */
	uint64_t serverTick = 0;
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
	std::vector<ProjectedStashItem> stash;
	uint32_t stashGold = 0;
	bool stashOpen = false;
	std::vector<ProjectedStatusEffect> statusEffects;
	std::vector<ProjectedMemorizedSpell> memorizedSpells;
};

/** Finds the server-projected targeting role for one authoritative spell source. */
[[nodiscard]] std::optional<ProjectedSpellTargetingKind> ProjectedSpellTargetingKindForSpell(
	const ProjectedPlayerSnapshot &snapshot,
	uint32_t spellId) noexcept;

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
/** Projects authoritative portal routes in stable portal-ID order. */
[[nodiscard]] tl::expected<std::vector<ProjectedPortalSnapshot>, std::string> ProjectPortalSnapshots(
    const ::devilution::protocol::v1::Snapshot &snapshot);
/** Projects server-owned corpse tiles in stable entity-ID order. */
[[nodiscard]] tl::expected<std::vector<ProjectedCorpseSnapshot>, std::string> ProjectCorpseSnapshots(
    const ::devilution::protocol::v1::Snapshot &snapshot);
/** Projects server-owned session metadata without enabling local simulation. */
[[nodiscard]] tl::expected<ProjectedSessionMetadata, std::string> ProjectSessionMetadata(
    const ::devilution::protocol::v1::Snapshot &snapshot);
/** Projects server-owned native quest state for compatibility presentation. */
[[nodiscard]] tl::expected<std::vector<ProjectedQuestSnapshot>, std::string> ProjectQuestSnapshots(
	const ::devilution::protocol::v1::Snapshot &snapshot);
/** Applies projected quest state without emitting legacy quest network messages. */
void ApplyServerBackedQuestSnapshots(const std::vector<ProjectedQuestSnapshot> &quests) noexcept;
/** Projects server-owned effective level geometry in stable level-ID order. */
[[nodiscard]] tl::expected<std::vector<ProjectedLevelGeometrySnapshot>, std::string> ProjectLevelGeometrySnapshots(
    const ::devilution::protocol::v1::Snapshot &snapshot);

/** Validates server-owned quest state before native interaction gates consume it. */
[[nodiscard]] tl::expected<void, std::string> ValidateQuestSnapshots(
    const ::devilution::protocol::v1::Snapshot &snapshot);

/** Validates authored quest/towner interaction records before native routing consumes them. */
[[nodiscard]] tl::expected<void, std::string> ValidateQuestTownerInteractions(
	const ::devilution::protocol::v1::Snapshot &snapshot);

/** Validates server-authored towner greeting records before native session use. */
[[nodiscard]] tl::expected<void, std::string> ValidateTownerGreetingSnapshots(
	const ::devilution::protocol::v1::Snapshot &snapshot);

/** Validates authoritative level geometry and warp-trigger metadata before projection use. */
[[nodiscard]] tl::expected<void, std::string> ValidateLevelGeometrySnapshots(
    const ::devilution::protocol::v1::Snapshot &snapshot);

/** Validates generated set-piece origins against supplied level geometry. */
[[nodiscard]] tl::expected<void, std::string> ValidateSetLevelObjectOriginSnapshots(
    const ::devilution::protocol::v1::Snapshot &snapshot);

/** Validates the server-owned procedural encounter materialization set. */
[[nodiscard]] tl::expected<void, std::string> ValidateMaterializedProceduralLevelSnapshots(
    const ::devilution::protocol::v1::Snapshot &snapshot);

/** Validates projected entity positions against native coordinates and supplied geometry. */
[[nodiscard]] tl::expected<void, std::string> ValidateEntityPositionSnapshots(
    const ::devilution::protocol::v1::Snapshot &snapshot);

/** Validates the shared nonzero entity-ID namespace across authoritative collections. */
[[nodiscard]] tl::expected<void, std::string> ValidateEntityIdentitySnapshots(
    const ::devilution::protocol::v1::Snapshot &snapshot);

/** Validates authoritative player action timing, mode, and movement admission. */
[[nodiscard]] tl::expected<void, std::string> ValidatePlayerActionSnapshots(
    const ::devilution::protocol::v1::Snapshot &snapshot);

/** Validates native-compatible monster combat metadata before projection. */
[[nodiscard]] tl::expected<void, std::string> ValidateMonsterSnapshots(
    const ::devilution::protocol::v1::Snapshot &snapshot);

/** Validates native-compatible projectile metadata before projection. */
[[nodiscard]] tl::expected<void, std::string> ValidateProjectileSnapshots(
    const ::devilution::protocol::v1::Snapshot &snapshot);

/** Validates server-authored combat and experience events before native application. */
[[nodiscard]] tl::expected<void, std::string> ValidateServerBackedEventBatch(
    const ::devilution::protocol::v1::EventBatch &eventBatch);

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

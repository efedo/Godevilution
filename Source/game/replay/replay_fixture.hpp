#pragma once

/**
 * @file game/replay_fixture.hpp
 *
 * Parser for the small, versioned replay fixture envelope used by tests.
 */

#include <array>
#include <cstdint>
#include <string>
#include <string_view>
#include <vector>

#include "game/replay/replay.hpp"

namespace devilution {

struct ReplayFixtureCommand {
	uint64_t clientSequence = 0;
	ReplayCommandOrder order;
	/** Optional expected authoritative command result status. */
	std::string expectedStatus;
	/** Optional expected authoritative rejection reason. */
	std::string expectedRejectReason;
	/** Optional participant session identity; empty targets the primary player. */
	std::string player;
	std::string kind;
	uint32_t storeId = 1;
	uint32_t storeSlot = 0;
	int32_t directionX = 0;
	int32_t directionY = 0;
	uint32_t targetEntityId = 0;
	uint32_t spellId = 0;
	int32_t targetX = 0;
	int32_t targetY = 0;
	uint32_t portalId = 0;
	uint32_t worldItemEntityId = 0;
	/** Stable item identity used by explicit carried-item references. */
	uint32_t stableItemId = 0;
	/** Item location name used by explicit carried-item references. */
	std::string itemLocation = "Inventory";
	uint32_t objectEntityId = 0;
	uint32_t questId = 0;
};

struct ReplayFixtureContentManifest {
	std::string id;
	std::string version;
	std::string sha256;
	std::vector<std::string> packs;
};

struct ReplayFixtureQuestPool {
	uint32_t poolId = 0;
	std::vector<uint32_t> questIds;
};

struct ReplayFixtureQuestSelection {
	bool defined = false;
	uint32_t seed = 0;
	std::vector<ReplayFixtureQuestPool> pools;
};

struct ReplayFixtureCheckpoint {
	uint64_t tick = 0;
	std::string stateSha256;
};

struct ReplayFixtureMemorizedSpell {
	uint32_t spellId = 0;
	uint32_t level = 0;
};

/** Minimal protocol-shaped equipment state carried by replay initial state. */
struct ReplayFixtureEquippedItem {
	uint32_t slot = 0;
	uint32_t itemSeed = 0;
	uint32_t stableItemId = 0;
	int32_t itemType = -1;
	int32_t itemClass = 0;
	uint32_t flags = 0;
	bool statFlag = false;
	int32_t itemIndex = -1;
	int32_t value = 0;
	int32_t identifiedValue = 0;
	bool identified = false;
	int32_t magical = 0;
	int32_t fireMinDamage = 0;
	int32_t fireMaxDamage = 0;
	int32_t lightningMinDamage = 0;
	int32_t lightningMaxDamage = 0;
};

struct ReplayFixtureSecondaryPlayer {
	bool defined = false;
	std::string player;
	std::string characterName;
	uint32_t entityId = 0;
	int32_t life = 0;
	int32_t lifeMaximum = 0;
	uint32_t gold = 0;
	int32_t positionX = 0;
	int32_t positionY = 0;
	uint32_t levelId = 0;
	uint32_t characterLevel = 1;
	uint32_t characterClass = 0;
	int32_t magic = 0;
	int32_t dexterity = 0;
	bool friendlyMode = true;
	bool blockCapable = false;
	uint32_t combatMode = 0;
	uint64_t combatModeEndTick = 0;
	std::vector<ReplayFixtureEquippedItem> equipment;
	bool inventoryItemDefined = false;
	uint32_t inventoryFillerCount = 0;
	uint32_t inventoryItemSlot = 0;
	uint32_t inventoryItemSeed = 0;
	uint32_t inventoryItemStableItemId = 0;
	uint32_t inventoryItemPrice = 0;
	int32_t inventoryItemType = -1;
	int32_t inventoryItemIndex = -1;
	bool inventoryItemStatFlag = false;
	bool heldItemDefined = false;
	uint32_t heldItemSlot = 0;
	uint32_t heldItemSeed = 0;
	uint32_t heldItemStableItemId = 0;
	uint32_t heldItemPrice = 0;
	int32_t heldItemType = -1;
	int32_t heldItemIndex = -1;
	bool heldItemStatFlag = false;
};

/** Minimal protocol-shaped belt item used by consumable-cast fixtures. */
struct ReplayFixtureBeltItem {
	uint32_t slot = 0;
	uint32_t itemSeed = 0;
	uint32_t stableItemId = 0;
	int32_t itemType = 0;
	int32_t itemIndex = -1;
	int32_t miscId = 0;
	int32_t spellId = 0;
	bool statFlag = false;
};

/** Minimal authoritative corpse tile carried by the compact replay projection. */
struct ReplayFixtureCorpseState {
	uint32_t entityId = 0;
	uint32_t levelId = 0;
	int32_t positionX = 0;
	int32_t positionY = 0;
};

struct ReplayFixtureInitialState {
	std::string player;
	std::string characterName;
	uint32_t gold = 0;
	uint32_t experience = 0;
	int32_t life = 0;
	int32_t lifeMaximum = 0;
	int32_t mana = 0;
	int32_t manaMaximum = 0;
	int32_t magic = 0;
	int32_t characterClass = 0;
	uint8_t characterLevel = 1;
	int32_t positionX = 0;
	int32_t positionY = 0;
	uint32_t levelId = 0;
	uint32_t warpId = 0;
	uint32_t warpSourceLevelId = 0;
	int32_t warpTriggerX = 0;
	int32_t warpTriggerY = 0;
	uint32_t warpTargetLevelId = 0;
	int32_t warpTargetX = 0;
	int32_t warpTargetY = 0;
	uint32_t facingDirection = 0;
	bool suppressMultiplayerDeathDrops = false;
	uint32_t goldStackSeed = 0;
	uint32_t goldStackStableItemId = 0;
	uint32_t goldStackCell = 0;
	uint32_t inventoryFillerCount = 0;
	uint32_t worldItemEntityId = 0;
	uint32_t worldItemSeed = 0;
	uint32_t worldItemPrice = 0;
	uint32_t objectEntityId = 0;
	uint32_t objectId = 0;
	uint32_t objectQuestId = 0;
	int32_t objectPositionX = 0;
	int32_t objectPositionY = 0;
	int32_t objectEffectKind = 0;
	int32_t objectEffectAmount = 0;
	bool objectMissilePassable = false;
	bool objectBreakable = false;
	bool objectDestroyed = false;
	uint32_t combatTargetEntityId = 0;
	uint32_t combatTargetLevelId = 0;
	int32_t combatTargetPositionX = 0;
	int32_t combatTargetPositionY = 0;
	int32_t combatTargetHitPoints = 0;
	int32_t combatTargetMaxHitPoints = 0;
	int32_t combatTargetArmorClass = 0;
	int32_t combatTargetAttackDamage = 0;
	int32_t combatTargetAggroRange = 0;
	std::string combatTargetNativeAi;
	uint32_t combatTargetMonsterItemSeed = 0;
	uint32_t combatTargetMonsterAiSeed = 0;
	uint32_t combatTargetMonsterId = 0;
	int32_t combatTargetToHit = 0;
	int32_t combatTargetToHitSpecial = 0;
	int32_t combatTargetMinDamage = 0;
	int32_t combatTargetMaxDamage = 0;
	int32_t combatTargetMinDamageSpecial = 0;
	int32_t combatTargetMaxDamageSpecial = 0;
	bool combatTargetBerserked = false;
	int32_t combatTargetIntelligence = 0;
	int32_t combatTargetFallenStandAnimationFrame = 0;
	int32_t combatTargetFallenStandAnimationTick = 0;
	int32_t combatTargetFallenRallyTicks = 0;
	bool combatTargetGargoyleSpecialActive = false;
	int32_t combatTargetHorkDemonDelayTicks = 0;
	int32_t combatTargetSkeletonMeleeDelayTicks = 0;
	int32_t combatTargetSnakeDelayTicks = 0;
	int32_t combatTargetCounselorDelayTicks = 0;
	int32_t combatTargetCounselorRetreatTicks = 0;
	int32_t combatTargetRangedDelayTicks = 0;
	// Optional compact AiRanged retreat tuning mirrored by replay content.
	int32_t combatTargetRangedRetreatMinimumDistance = 0;
	int32_t combatTargetRangedRetreatChanceBase = 0;
	int32_t combatTargetRangedRetreatChanceIntelligenceScale = 0;
	int32_t combatTargetMegaDelayTicks = 0;
	int32_t combatTargetSkeletonRangedMovementIdleTicks = -1;
	int32_t combatTargetSkeletonRangedMovementMode = 0;
	bool combatTargetSneakHidden = false;
	int32_t combatTargetSneakRetreatTicks = 0;
	int32_t combatTargetBatRetreatTicks = 0;
	int32_t combatTargetBatRetreatDirection = 0;
	int32_t combatTargetScavengerHealingTicks = 0;
	uint32_t combatTargetScavengerCorpseEntityId = 0;
	int32_t combatTargetFatMovementIdleTicks = -1;
	int32_t combatTargetFatMovementMode = 0;
	int32_t combatTargetFireResistance = 0;
	int32_t combatTargetLightningResistance = 0;
	int32_t combatTargetMagicResistance = 0;
	int32_t combatTargetMonsterLevel = 0;
	bool combatTargetUndead = false;
	bool combatTargetMoving = false;
	int32_t combatTargetFuturePositionX = 0;
	int32_t combatTargetFuturePositionY = 0;
	bool combatTargetPetrified = false;
	bool combatTargetStoneCurseImmune = false;
	uint32_t questId = 0;
	uint32_t questRequiredProgress = 0;
	uint32_t statusEffectId = 0;
	uint32_t statusDuration = 0;
	int32_t statusMagnitude = 0;
	uint32_t abilitySpellId = 0;
	bool manaShieldActive = false;
	uint32_t reflectCharges = 0;
	uint32_t combatMode = 0;
	uint64_t combatModeEndTick = 0;
	std::vector<ReplayFixtureMemorizedSpell> memorizedSpells;
	std::vector<ReplayFixtureEquippedItem> equipment;
	std::vector<ReplayFixtureBeltItem> belt;
	std::vector<ReplayFixtureCorpseState> corpses;
	ReplayFixtureSecondaryPlayer secondaryPlayer;
};

struct ReplayFixtureStoreState {
	int32_t activeStore = 1;
	int32_t premiumItemCount = 0;
	int32_t premiumItemLevel = 3;
	std::vector<uint32_t> premiumItemSeeds { 42 };
};

/** Content-derived animation boundaries for a deferred player-action fixture. */
struct ReplayFixturePlayerActionTiming {
	bool defined = false;
	uint32_t definedFields = 0;
	uint32_t walkingFrames = 0;
	uint32_t attackFrames = 0;
	uint32_t attackActionFrame = 0;
	uint32_t castingFrames = 0;
	uint32_t castingActionFrame = 0;
	uint32_t deathFrames = 0;
};

/** Seeded projectile state used by cross-runner lifecycle fixtures. */
struct ReplayFixtureInitialProjectile {
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
	int32_t speed = 0;
	int32_t impactLifetime = 1;
	int32_t damageMinimum = 0;
	int32_t damageMaximum = 0;
};

struct ReplayFixture {
	uint32_t formatVersion = 0;
	std::string fixtureId;
	std::string protocolSchemaVersion;
	uint32_t tickRateHz = 0;
	uint64_t rngSeed = 0;
	std::string gameVariant = "Diablo";
	std::string gameMode = "SinglePlayer";
	ReplayFixtureContentManifest contentManifest;
	ReplayFixtureQuestSelection questSelection;
	ReplayFixtureInitialState initialState;
	std::vector<ReplayFixtureInitialProjectile> initialProjectiles;
	ReplayFixtureStoreState storeState;
	ReplayFixturePlayerActionTiming playerActionTiming;
	std::vector<ReplayFixtureCommand> commands;
	std::vector<ReplayFixtureCheckpoint> checkpoints;
	std::string initialStateSha256;
};

/** Snapshot item fields used by the cross-language authoritative hash. */
struct ReplayFixtureItemState {
	uint32_t createInfo = 0;
	uint32_t stableItemId = 0;
	int32_t itemType = -1;
	int32_t positionX = 0;
	int32_t positionY = 0;
	bool deleted = false;
	bool identified = false;
	int32_t magical = 0;
	int32_t equipLocation = 0;
	int32_t itemClass = 0;
	int32_t value = 0;
	int32_t identifiedValue = 0;
	int32_t minDamage = 0;
	int32_t maxDamage = 0;
	int32_t armorClass = 0;
	uint32_t flags = 0;
	int32_t miscId = 0;
	int32_t spellId = 0;
	int32_t itemIndex = -1;
	int32_t charges = 0;
	int32_t maxCharges = 0;
	int32_t durability = 0;
	int32_t maxDurability = 0;
	int32_t plusDamage = 0;
	int32_t plusToHit = 0;
	int32_t plusArmorClass = 0;
	int32_t plusStrength = 0;
	int32_t plusMagic = 0;
	int32_t plusDexterity = 0;
	int32_t plusVitality = 0;
	int32_t plusFireResistance = 0;
	int32_t plusLightningResistance = 0;
	int32_t plusMagicResistance = 0;
	int32_t plusMana = 0;
	int32_t plusHitPoints = 0;
	int32_t plusDamageModifier = 0;
	int32_t plusGetHit = 0;
	int32_t plusLight = 0;
	int32_t spellLevelAdd = 0;
	int32_t uniqueId = 0;
	int32_t fireMinDamage = 0;
	int32_t fireMaxDamage = 0;
	int32_t lightningMinDamage = 0;
	int32_t lightningMaxDamage = 0;
	int32_t plusEnemyArmorClass = 0;
	int32_t prefixPower = -1;
	int32_t suffixPower = -1;
	int32_t valueAdd1 = 0;
	int32_t valueMultiply1 = 0;
	int32_t valueAdd2 = 0;
	int32_t valueMultiply2 = 0;
	int32_t minimumStrength = 0;
	int32_t minimumMagic = 0;
	int32_t minimumDexterity = 0;
	bool statFlag = false;
	int32_t hellfireDamageArmorFlags = 0;
	uint32_t buff = 0;
	uint32_t inventoryWidth = 1;
	uint32_t inventoryHeight = 1;
	std::string baseName;
	std::string identifiedName;
	bool hasNativeCursorGraphic = false;
	int32_t nativeCursorGraphic = 0;
};

struct ReplayFixtureInventoryItem {
	uint32_t storeId = 0;
	uint32_t storeSlot = 0;
	uint32_t itemSeed = 0;
	uint32_t price = 0;
	uint64_t purchasedAtTick = 0;
	ReplayFixtureItemState state;
};

struct ReplayFixtureSlottedItem {
	uint32_t slot = 0;
	uint32_t itemSeed = 0;
	ReplayFixtureItemState state;
};

struct ReplayFixtureWorldItemState {
	uint32_t entityId = 0;
	uint32_t levelId = 0;
	int32_t positionX = 0;
	int32_t positionY = 0;
	uint32_t itemSeed = 0;
	uint32_t price = 0;
	ReplayFixtureItemState state;
};

struct ReplayFixtureStoreItem {
	uint32_t storeSlot = 0;
	uint32_t itemSeed = 0;
	uint32_t price = 0;
	ReplayFixtureItemState state;
};

/** Protocol-shaped projectile state used by cross-language replay parity. */
struct ReplayFixtureProjectileState {
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
	int32_t damageMinimum = 0;
	int32_t damageMaximum = 0;
	int32_t damageQ6 = 0;
	int32_t elementalFireDamage = 0;
	int32_t elementalLightningDamage = 0;
	bool attackHit = false;
	int32_t perilSelfDamageQ6 = 0;
	bool doppelgangerProc = false;
	uint32_t activationDelayTicks = 0;
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
	int32_t impactLifetime = 1;
	int32_t resistantDamageDivisor = 1;
	int32_t effectiveSpellLevel = 0;
	uint32_t lastCollisionTargetEntityId = 0;
	int32_t wallLeftX = 0;
	int32_t wallLeftY = 0;
	int32_t wallRightX = 0;
	int32_t wallRightY = 0;
	int32_t wallLeftDirection = -1;
	int32_t wallRightDirection = -1;
	bool wallLeftStopped = false;
	bool wallRightStopped = false;
	int32_t wallHitDirection = -1;
	bool ringOfFireLimitReached = false;
	int32_t apocalypseScanX = 0;
	int32_t apocalypseScanY = 0;
	bool apocalypseHit = false;
	int32_t elementalOriginX = 0;
	int32_t elementalOriginY = 0;
	int32_t guardianState = 0;
};

struct ReplayFixturePlayerActionState {
	bool defined = false;
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
};

/** Minimal protocol-shaped state used by native replay transition tests. */
struct ReplayFixtureExecutionState {
	uint32_t entityId = 1;
	std::string characterName;
	int32_t positionX = 0;
	int32_t positionY = 0;
	int32_t life = 0;
	int32_t mana = 0;
	int32_t manaMaximum = 0;
	int32_t lifeMaximum = 0;
	uint32_t characterLevel = 1;
	uint32_t levelId = 0;
	std::vector<uint32_t> visitedLevelIds;
	uint32_t warpId = 0;
	uint32_t warpSourceLevelId = 0;
	int32_t warpTriggerX = 0;
	int32_t warpTriggerY = 0;
	uint32_t warpTargetLevelId = 0;
	int32_t warpTargetX = 0;
	int32_t warpTargetY = 0;
	uint32_t facingDirection = 0;
	bool multiplayer = false;
	bool suppressMultiplayerDeathDrops = false;
	uint32_t characterClassId = 0;
	uint32_t abilitySpellId = 0;
	bool manaShieldActive = false;
	uint32_t reflectCharges = 0;
	std::vector<ReplayFixtureMemorizedSpell> memorizedSpells;
	uint32_t combatMode = 0;
	uint64_t combatModeEndTick = 0;
	ReplayFixturePlayerActionState currentAction;
	struct StatusEffect {
		uint32_t effectId = 0;
		uint32_t remainingTicks = 0;
		int32_t magnitude = 0;
		uint32_t behavior = 0;
	};
	std::vector<StatusEffect> statusEffects;
	uint32_t gold = 0;
	uint32_t experience = 0;
	uint32_t activeStoreId = 0;
	std::array<int32_t, 8> attributes { };
	std::vector<ReplayFixtureInventoryItem> inventory;
	bool heldItemDefined = false;
	ReplayFixtureInventoryItem heldItem;
	std::vector<ReplayFixtureSlottedItem> equipment;
	std::vector<ReplayFixtureSlottedItem> belt;
	std::vector<int32_t> inventoryGrid;
	std::vector<ReplayFixtureStoreItem> activeStoreItems;
	std::vector<ReplayFixtureProjectileState> projectiles;
	uint32_t randomState = 0;
	std::string secondaryPlayer;
	std::string secondaryPlayerCharacterName;
	uint32_t secondaryPlayerEntityId = 0;
	int32_t secondaryPlayerLife = 0;
	int32_t secondaryPlayerLifeMaximum = 0;
	uint32_t secondaryPlayerGold = 0;
	int32_t secondaryPlayerPositionX = 0;
	int32_t secondaryPlayerPositionY = 0;
	uint32_t secondaryPlayerLevelId = 0;
	std::vector<uint32_t> secondaryPlayerVisitedLevelIds;
	uint32_t secondaryPlayerCharacterLevel = 1;
	uint32_t secondaryPlayerCharacterClassId = 0;
	int32_t secondaryPlayerMagic = 0;
	int32_t secondaryPlayerDexterity = 0;
	bool secondaryPlayerFriendlyMode = true;
	uint32_t secondaryPlayerFacingDirection = 0;
	bool secondaryPlayerBlockCapable = false;
	uint32_t secondaryPlayerCombatMode = 0;
	uint64_t secondaryPlayerCombatModeEndTick = 0;
	std::vector<ReplayFixtureInventoryItem> secondaryInventory;
	std::vector<int32_t> secondaryInventoryGrid;
	std::vector<ReplayFixtureSlottedItem> secondaryEquipment;
	bool secondaryHeldItemDefined = false;
	ReplayFixtureInventoryItem secondaryHeldItem;
	bool hellfire = false;
	uint32_t combatTargetEntityId = 0;
	uint32_t combatTargetLevelId = 0;
	int32_t combatTargetPositionX = 0;
	int32_t combatTargetPositionY = 0;
	int32_t combatTargetDirection = 0;
	int32_t combatTargetHitPoints = 0;
	int32_t combatTargetMaxHitPoints = 0;
	int32_t combatTargetHitPointsQ6 = 0;
	int32_t combatTargetMaxHitPointsQ6 = 0;
	int32_t combatTargetArmorClass = 0;
	int32_t combatTargetAttackDamage = 0;
	int32_t combatTargetAggroRange = 0;
	std::string combatTargetNativeAi;
	uint32_t combatTargetMonsterItemSeed = 0;
	uint32_t combatTargetMonsterAiSeed = 0;
	uint32_t combatTargetMonsterId = 0;
	bool combatTargetPlayerMinion = false;
	uint32_t combatTargetOwnerPlayerId = 0;
	int32_t combatTargetDeathAnimationTicks = 0;
	int32_t combatTargetDeathTicksRemaining = 0;
	int32_t combatTargetToHit = 0;
	int32_t combatTargetToHitSpecial = 0;
	int32_t combatTargetMinDamage = 0;
	int32_t combatTargetMaxDamage = 0;
	int32_t combatTargetMinDamageSpecial = 0;
	int32_t combatTargetMaxDamageSpecial = 0;
	bool combatTargetBerserked = false;
	int32_t combatTargetIntelligence = 0;
	int32_t combatTargetFallenStandAnimationFrame = 0;
	int32_t combatTargetFallenStandAnimationTick = 0;
	int32_t combatTargetFallenRallyTicks = 0;
	bool combatTargetGargoyleSpecialActive = false;
	bool combatTargetAutonomousActionHandled = false;
	int32_t combatTargetAvoidanceMovementTicks = -1;
	int32_t combatTargetAvoidanceMovementTurn = 0;
	int32_t combatTargetAvoidanceMovementIdleTicks = -1;
	int32_t combatTargetAvoidanceMovementMode = 0;
	int32_t combatTargetHorkDemonDelayTicks = 0;
	int32_t combatTargetSkeletonMeleeDelayTicks = 0;
	int32_t combatTargetSnakeDelayTicks = 0;
	int32_t combatTargetCounselorDelayTicks = 0;
	int32_t combatTargetCounselorRetreatTicks = 0;
	int32_t combatTargetRangedDelayTicks = 0;
	int32_t combatTargetRangedRetreatMinimumDistance = 0;
	int32_t combatTargetRangedRetreatChanceBase = 0;
	int32_t combatTargetRangedRetreatChanceIntelligenceScale = 0;
	int32_t combatTargetMegaDelayTicks = 0;
	int32_t combatTargetSkeletonRangedMovementIdleTicks = -1;
	int32_t combatTargetSkeletonRangedMovementMode = 0;
	bool combatTargetSneakHidden = false;
	int32_t combatTargetSneakRetreatTicks = 0;
	int32_t combatTargetBatRetreatTicks = 0;
	int32_t combatTargetBatRetreatDirection = 0;
	int32_t combatTargetScavengerHealingTicks = 0;
	uint32_t combatTargetScavengerCorpseEntityId = 0;
	int32_t combatTargetFatMovementIdleTicks = -1;
	int32_t combatTargetFatMovementMode = 0;
	int32_t combatTargetFireResistance = 0;
	int32_t combatTargetLightningResistance = 0;
	int32_t combatTargetMagicResistance = 0;
	int32_t combatTargetMonsterLevel = 0;
	bool combatTargetUndead = false;
	bool combatTargetMoving = false;
	int32_t combatTargetFuturePositionX = 0;
	int32_t combatTargetFuturePositionY = 0;
	bool combatTargetPetrified = false;
	bool combatTargetStoneCurseImmune = false;
	std::vector<ReplayFixtureCorpseState> corpses;
	uint32_t worldItemEntityId = 0;
	uint32_t worldItemLevelId = 0;
	int32_t worldItemPositionX = 0;
	int32_t worldItemPositionY = 0;
	uint32_t worldItemSeed = 0;
	uint32_t worldItemPrice = 0;
	ReplayFixtureItemState worldItemState;
	std::vector<ReplayFixtureWorldItemState> worldItems;
	uint32_t objectEntityId = 0;
	uint32_t objectId = 0;
	uint32_t objectLevelId = 0;
	uint32_t objectQuestId = 0;
	int32_t objectPositionX = 0;
	int32_t objectPositionY = 0;
	bool objectActivated = false;
	bool objectMissilePassable = false;
	bool objectBreakable = false;
	bool objectDestroyed = false;
	int32_t objectEffectKind = 0;
	int32_t objectEffectAmount = 0;
	uint32_t objectQuestProgressAmount = 1;
	bool objectTrapArmed = false;
	uint32_t questId = 0;
	uint32_t questLevelId = 0;
	uint32_t questProgress = 0;
	uint32_t questRequiredProgress = 0;
	bool questCompleted = false;
	bool questUnavailable = false;
	bool questInitial = false;
	bool questLogged = false;
	int32_t questVariable1 = 0;
	int32_t questVariable2 = 0;
	int32_t questMessageId = 0;
	uint32_t questCompletionMonsterId = 0;
	uint32_t spellHealingAmount = 20;
	uint32_t spellManaCost = 5;
	int32_t spellProjectileBehavior = 0;
	uint32_t spellStatusEffectId = 0;
	uint32_t spellStatusDuration = 0;
	int32_t spellStatusMagnitude = 0;
	uint64_t lastAppliedTick = 0;
	uint32_t portalId = 0;
	uint32_t portalSourceLevelId = 0;
	int32_t portalSourceX = 0;
	int32_t portalSourceY = 0;
	uint32_t portalDestinationLevelId = 0;
	int32_t portalDestinationX = 0;
	int32_t portalDestinationY = 0;
	uint32_t portalRemainingTicks = 0;
	uint32_t portalOwnerEntityId = 0;
	bool portalBidirectional = false;
};

struct ReplayFixtureTransitionResult {
	uint64_t tick = 0;
	std::string kind;
	bool accepted = false;
	std::string stateSha256;
};

struct ReplayFixtureExecutionResult {
	std::vector<ReplayFixtureTransitionResult> transitions;
};

/** Hashes the protocol-shaped state using the C# SnapshotStateHasher order. */
[[nodiscard]] std::string ComputeReplayFixtureStateHash(const ReplayFixtureExecutionState &state);

/** Applies supported fixture commands and validates every command checkpoint. */
[[nodiscard]] bool ExecuteReplayFixture(
    const ReplayFixture &fixture,
    ReplayFixtureExecutionState &state,
    ReplayFixtureExecutionResult &result,
    std::string &error);

/**
 * Parses the replay fixture fields needed by the baseline tests.
 *
 * The parser intentionally supports only the fixture envelope. Unknown fields
 * are skipped recursively so fixtures can carry metadata without adding a
 * general-purpose JSON dependency to the engine.
 */
[[nodiscard]] bool ParseReplayFixture(std::string_view json, ReplayFixture &fixture, std::string &error);

} // namespace devilution

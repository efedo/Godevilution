/**
 * @file game/replay/replay_fixture.cpp
 *
 * Parser for the small, versioned replay fixture envelope used by tests.
 */

#include "game/replay/replay_fixture.hpp"

#include <algorithm>
#include <cctype>
#include <charconv>
#include <cmath>
#include <cstdlib>
#include <iterator>
#include <limits>
#include <optional>
#include <queue>
#include <utility>

#include "engine/crawl.hpp"
#include "network/authoritative/protocol_limits.hpp"

namespace devilution {

void AdmitReplayAutonomousMegaInferno(
    ReplayFixtureExecutionState &state,
    uint64_t tick);
void AdvanceReplayInfernoOneTick(ReplayFixtureExecutionState &state);
void AdvanceReplayPlayerInfernoOneTick(ReplayFixtureExecutionState &state);
void AdvanceReplayPlayerApocalypseOneTick(ReplayFixtureExecutionState &state);

namespace {

constexpr uint32_t FastAttackFlag = 1U << 18;
constexpr uint32_t FasterAttackFlag = 1U << 19;
constexpr uint32_t FastestAttackFlag = 1U << 20;
constexpr uint32_t QuickAttackFlag = 1U << 17;
constexpr uint32_t FireDamageFlag = 1U << 4;
constexpr uint32_t LightningDamageFlag = 1U << 5;
constexpr uint32_t FireArrowsFlag = 1U << 3;
constexpr uint32_t LightningArrowsFlag = 1U << 25;
constexpr uint32_t RandomArrowVelocityFlag = 1U << 2;
constexpr uint32_t MultipleArrowsFlag = 1U << 9;
constexpr int32_t WeaponArrowBehavior = 58;
constexpr int32_t MonsterArrowBehavior = 59;
constexpr int32_t FireboltBehavior = 2;
constexpr int32_t LightningBehavior = 3;
constexpr int32_t LightningSegmentBehavior = 4;
constexpr int32_t FireballBehavior = 16;
constexpr int32_t BloodStarBehavior = 20;
constexpr int32_t MonsterMagicBehavior = 50;
constexpr int32_t InfernoControllerBehavior = 26;
constexpr int32_t InfernoSegmentBehavior = 27;
constexpr int32_t FireWallControllerBehavior = 22;
constexpr int32_t FireWallSegmentBehavior = 23;
constexpr int32_t LightningWallControllerBehavior = 47;
constexpr int32_t FlameWaveControllerBehavior = 24;
constexpr int32_t FlameWaveSegmentBehavior = 25;
constexpr int32_t ChainLightningControllerBehavior = 28;
constexpr int32_t ChainLightningBoltBehavior = 29;
constexpr int32_t ChainLightningMaximumRadius = 18;
constexpr int32_t RingOfFireControllerBehavior = 42;
constexpr int32_t ImmolationControllerBehavior = 43;
constexpr int32_t ElementalBehavior = 35;
constexpr int32_t GuardianControllerBehavior = 36;
constexpr int32_t RageControllerBehavior = 34;
constexpr int32_t NovaControllerBehavior = 30;
constexpr int32_t NovaBallBehavior = 31;
constexpr int32_t ApocalypseControllerBehavior = 32;
constexpr int32_t ApocalypseBoomBehavior = 33;
constexpr int32_t ManaBehavior = 37;
constexpr int32_t TelekinesisBehavior = 44;
constexpr int32_t MagiBehavior = 38;
constexpr int32_t ReflectBehavior = 39;
constexpr int32_t EtherealizeBehavior = 49;
constexpr int32_t EtherealizeStatusBehavior = 3;
constexpr int32_t BerserkBehavior = 44;
constexpr int32_t HorkSpawnBehavior = 57;
constexpr int32_t SnakeChargeBehavior = 54;
constexpr int32_t AcidBehavior = 51;
constexpr int32_t AcidSplatBehavior = 52;
constexpr int32_t AcidPuddleBehavior = 53;
constexpr int32_t AcidDamageType = 4;
constexpr std::pair<int32_t, int32_t> GuardianArc[] = {
	{ 6, 0 }, { 5, 0 }, { 4, 0 }, { 3, 0 }, { 2, 0 }, { 1, 0 },
	{ 6, 1 }, { 5, 1 }, { 4, 1 }, { 3, 1 }, { 6, 2 }, { 2, 1 },
	{ 5, 2 }, { 6, 3 }, { 4, 2 }, { 5, 3 }, { 3, 2 }, { 1, 1 },
	{ 6, 4 }, { 6, 5 }, { 5, 4 }, { 4, 3 }, { 2, 2 }, { 5, 5 },
	{ 4, 4 }, { 3, 3 }, { 6, 6 }, { 5, 6 }, { 4, 5 }, { 3, 4 },
	{ 2, 3 }, { 4, 6 }, { 3, 5 }, { 2, 4 }, { 1, 2 }, { 3, 6 },
	{ 2, 5 }, { 1, 3 }, { 0, 1 }, { 2, 6 }, { 1, 4 }, { 1, 5 },
	{ 1, 6 }, { 0, 2 }, { 0, 3 }, { 0, 6 }, { 0, 5 }, { 0, 4 },
};
constexpr std::pair<int32_t, int32_t> DirectionDisplacements[] = {
	{ 1, 1 },
	{ 0, 1 },
	{ -1, 1 },
	{ -1, 0 },
	{ -1, -1 },
	{ 0, -1 },
	{ 1, -1 },
	{ 1, 0 },
};

bool CanPlaceReplayInventoryItem(const std::vector<int32_t> &grid, int cell, uint32_t width, uint32_t height);
int FindReplayInventoryPlacement(const ReplayFixtureExecutionState &state, const ReplayFixtureItemState &item);
void FillReplayInventoryGrid(ReplayFixtureExecutionState &state, int cell, size_t itemIndex, const ReplayFixtureItemState &item);

std::optional<std::pair<int32_t, int32_t>> FindReplaySecondaryDeathDropPosition(
	const ReplayFixtureExecutionState &state,
	std::optional<int32_t> preferredDirection = std::nullopt);

bool IsValidReplayCharacterName(std::string_view name)
{
	if (name.empty() || name.size() > 31
	    || name.find_first_of(",<>%&\\\"?*#/: ") != name.npos
	    || std::any_of(name.begin(), name.end(), [](unsigned char character) {
		       return character < 0x21 || character > 0x7E;
	       })) {
		return false;
	}
	std::string shifted { name };
	for (char &character : shifted)
		++character;
	constexpr std::string_view ForbiddenNames[] = {
		"gvdl", "dvou", "tiju", "cjudi", "bttipmf", "ojhhfs", "cmj{{bse", "benjo",
	};
	return std::none_of(std::begin(ForbiddenNames), std::end(ForbiddenNames), [&](std::string_view forbidden) {
		return shifted.find(forbidden) != std::string::npos;
	});
}

void StartReplaySecondaryPlayerDeath(
	ReplayFixtureExecutionState &state,
	bool monsterOrTrapDeath = false);

void AppendItemState(ReplayStateHasher &hasher, const ReplayFixtureItemState &state)
{
	hasher.AppendUint32(state.createInfo);
	hasher.AppendInt32(state.itemType);
	hasher.AppendInt32(state.positionX);
	hasher.AppendInt32(state.positionY);
	hasher.AppendBool(state.deleted);
	hasher.AppendBool(state.identified);
	hasher.AppendInt32(state.magical);
	hasher.AppendInt32(state.equipLocation);
	hasher.AppendInt32(state.itemClass);
	hasher.AppendInt32(state.value);
	hasher.AppendInt32(state.identifiedValue);
	hasher.AppendInt32(state.minDamage);
	hasher.AppendInt32(state.maxDamage);
	hasher.AppendInt32(state.armorClass);
	hasher.AppendUint32(state.flags);
	hasher.AppendInt32(state.miscId);
	hasher.AppendInt32(state.spellId);
	const int32_t spellSourceKind = state.miscId == 21 || state.miscId == 22
	    ? 2
	    : state.miscId == 23 || state.miscId == 27 || (state.miscId >= 47 && state.miscId <= 51)
	        ? 1
	        : 0;
	if (spellSourceKind != 0) {
		hasher.AppendString("godevilution-item-spell-source-kind-v1");
		hasher.AppendInt32(spellSourceKind);
	}
	hasher.AppendInt32(state.itemIndex);
	hasher.AppendInt32(state.charges);
	hasher.AppendInt32(state.maxCharges);
	hasher.AppendInt32(state.durability);
	hasher.AppendInt32(state.maxDurability);
	hasher.AppendInt32(state.plusDamage);
	hasher.AppendInt32(state.plusToHit);
	hasher.AppendInt32(state.plusArmorClass);
	hasher.AppendInt32(state.plusStrength);
	hasher.AppendInt32(state.plusMagic);
	hasher.AppendInt32(state.plusDexterity);
	hasher.AppendInt32(state.plusVitality);
	hasher.AppendInt32(state.plusFireResistance);
	hasher.AppendInt32(state.plusLightningResistance);
	hasher.AppendInt32(state.plusMagicResistance);
	hasher.AppendInt32(state.plusMana);
	hasher.AppendInt32(state.plusHitPoints);
	hasher.AppendInt32(state.plusDamageModifier);
	hasher.AppendInt32(state.plusGetHit);
	hasher.AppendInt32(state.plusLight);
	hasher.AppendInt32(state.spellLevelAdd);
	hasher.AppendInt32(state.uniqueId);
	hasher.AppendInt32(state.fireMinDamage);
	hasher.AppendInt32(state.fireMaxDamage);
	hasher.AppendInt32(state.lightningMinDamage);
	hasher.AppendInt32(state.lightningMaxDamage);
	hasher.AppendInt32(state.plusEnemyArmorClass);
	hasher.AppendInt32(state.prefixPower);
	hasher.AppendInt32(state.suffixPower);
	hasher.AppendInt32(state.valueAdd1);
	hasher.AppendInt32(state.valueMultiply1);
	hasher.AppendInt32(state.valueAdd2);
	hasher.AppendInt32(state.valueMultiply2);
	hasher.AppendInt32(state.minimumStrength);
	hasher.AppendInt32(state.minimumMagic);
	hasher.AppendInt32(state.minimumDexterity);
	hasher.AppendBool(state.statFlag);
	hasher.AppendInt32(state.hellfireDamageArmorFlags);
	hasher.AppendUint32(state.buff);
	hasher.AppendUint32(state.inventoryWidth == 0 ? 1 : state.inventoryWidth);
	hasher.AppendUint32(state.inventoryHeight == 0 ? 1 : state.inventoryHeight);
	if (!state.baseName.empty() || !state.identifiedName.empty() || state.hasNativeCursorGraphic) {
		hasher.AppendString("godevilution-dynamic-item-presentation-v1");
		hasher.AppendString(state.baseName);
		hasher.AppendString(state.identifiedName);
		hasher.AppendBool(state.hasNativeCursorGraphic);
		if (state.hasNativeCursorGraphic)
			hasher.AppendInt32(state.nativeCursorGraphic);
	}
}

template <typename Item>
void AppendSlottedItems(ReplayStateHasher &hasher, std::vector<Item> items)
{
	std::sort(items.begin(), items.end(), [](const Item &left, const Item &right) {
		if (left.slot != right.slot)
			return left.slot < right.slot;
		return left.itemSeed < right.itemSeed;
	});
	hasher.AppendUint64(items.size());
	for (const auto &item : items) {
		hasher.AppendUint32(item.slot);
		hasher.AppendUint32(item.itemSeed);
		AppendItemState(hasher, item.state);
	}
}

void AppendSecondaryPlayerState(ReplayStateHasher &hasher, const ReplayFixtureExecutionState &state)
{
	hasher.AppendUint32(state.secondaryPlayerEntityId);
	hasher.AppendInt32(state.secondaryPlayerPositionX);
	hasher.AppendInt32(state.secondaryPlayerPositionY);
	hasher.AppendInt32(state.secondaryPlayerLife);
	hasher.AppendInt32(0);
	hasher.AppendInt32(0);
	hasher.AppendInt32(state.secondaryPlayerLifeMaximum);
	hasher.AppendUint32(state.secondaryPlayerCharacterLevel);
	hasher.AppendUint32(state.secondaryPlayerLevelId);
	std::vector<uint32_t> historicalVisitedLevelIds;
	for (const uint32_t levelId : state.secondaryPlayerVisitedLevelIds) {
		if (levelId != state.secondaryPlayerLevelId)
			historicalVisitedLevelIds.push_back(levelId);
	}
	std::sort(historicalVisitedLevelIds.begin(), historicalVisitedLevelIds.end());
	if (!historicalVisitedLevelIds.empty()) {
		hasher.AppendString("godevilution-player-visited-levels-v1");
		hasher.AppendUint64(historicalVisitedLevelIds.size());
		for (const uint32_t levelId : historicalVisitedLevelIds)
			hasher.AppendUint32(levelId);
	}
	if (state.secondaryPlayerCharacterClassId != 0) {
		hasher.AppendString("godevilution-player-class-v1");
		hasher.AppendUint32(state.secondaryPlayerCharacterClassId);
	}
	if (!state.secondaryPlayerCharacterName.empty()) {
		hasher.AppendString("godevilution-player-name-v1");
		hasher.AppendString(state.secondaryPlayerCharacterName);
	}
	if (!state.secondaryPlayerFriendlyMode) {
		hasher.AppendString("godevilution-player-hostile-mode-v1");
		hasher.AppendBool(true);
	}
	if (state.secondaryPlayerFacingDirection != 0) {
		hasher.AppendString("godevilution-player-facing-v1");
		hasher.AppendInt32(static_cast<int32_t>(state.secondaryPlayerFacingDirection));
	}
	if (state.secondaryPlayerCombatMode != 0 || state.secondaryPlayerCombatModeEndTick != 0) {
		hasher.AppendString("godevilution-player-combat-mode-v1");
		hasher.AppendInt32(static_cast<int32_t>(state.secondaryPlayerCombatMode));
		hasher.AppendUint64(state.secondaryPlayerCombatModeEndTick);
	}
	hasher.AppendUint64(0);
	hasher.AppendUint32(state.secondaryPlayerGold);
	hasher.AppendUint32(0);
	hasher.AppendUint32(0);
	hasher.AppendInt32(0);
	hasher.AppendInt32(0);
	hasher.AppendInt32(state.secondaryPlayerMagic);
	hasher.AppendInt32(state.secondaryPlayerMagic);
	hasher.AppendInt32(state.secondaryPlayerDexterity);
	hasher.AppendInt32(state.secondaryPlayerDexterity);
	hasher.AppendInt32(0);
	hasher.AppendInt32(0);
	if (!state.secondaryEquipment.empty()) {
		AppendSlottedItems(hasher, state.secondaryEquipment);
	} else if (state.secondaryPlayerBlockCapable) {
		hasher.AppendUint64(1);
		hasher.AppendUint32(5);
		hasher.AppendUint32(1);
		ReplayFixtureItemState shield;
		shield.itemType = 5;
		shield.statFlag = true;
		AppendItemState(hasher, shield);
	} else {
		hasher.AppendUint64(0);
	}
	hasher.AppendUint64(0);
	std::vector<ReplayFixtureInventoryItem> inventory = state.secondaryInventory;
	std::sort(inventory.begin(), inventory.end(), [](const auto &left, const auto &right) {
		if (left.storeId != right.storeId)
			return left.storeId < right.storeId;
		if (left.storeSlot != right.storeSlot)
			return left.storeSlot < right.storeSlot;
		if (left.itemSeed != right.itemSeed)
			return left.itemSeed < right.itemSeed;
		if (left.price != right.price)
			return left.price < right.price;
		return left.purchasedAtTick < right.purchasedAtTick;
	});
	hasher.AppendUint64(inventory.size());
	for (const auto &item : inventory) {
		hasher.AppendUint32(item.storeId);
		hasher.AppendUint32(item.storeSlot);
		hasher.AppendUint32(item.itemSeed);
		hasher.AppendUint32(item.price);
		hasher.AppendUint64(item.purchasedAtTick);
		AppendItemState(hasher, item.state);
	}
	hasher.AppendUint64(state.secondaryInventoryGrid.size());
	for (const int32_t cell : state.secondaryInventoryGrid)
		hasher.AppendInt32(cell);
	if (state.secondaryHeldItemDefined) {
		hasher.AppendString("godevilution-player-held-item-v1");
		hasher.AppendUint32(state.secondaryHeldItem.storeId);
		hasher.AppendUint32(state.secondaryHeldItem.storeSlot);
		hasher.AppendUint32(state.secondaryHeldItem.itemSeed);
		hasher.AppendUint32(state.secondaryHeldItem.price);
		hasher.AppendUint64(state.secondaryHeldItem.purchasedAtTick);
		AppendItemState(hasher, state.secondaryHeldItem.state);
	}
}

void DefeatReplayCombatTarget(ReplayFixtureExecutionState &state)
{
	state.experience += 100;
	if (state.combatTargetEntityId == 0)
		return;
	state.corpses.push_back({
	    state.combatTargetEntityId,
	    state.combatTargetLevelId,
	    state.combatTargetPositionX,
	    state.combatTargetPositionY,
	});
	state.combatTargetEntityId = 0;
	state.combatTargetMoving = false;
	state.combatTargetFuturePositionX = state.combatTargetPositionX;
	state.combatTargetFuturePositionY = state.combatTargetPositionY;
}

void AppendCanonicalState(ReplayStateHasher &hasher, const ReplayFixtureExecutionState &state)
{
	const bool hasSecondaryPlayer = state.secondaryPlayerEntityId != 0;
	hasher.AppendUint64(hasSecondaryPlayer ? 2 : 1);
	if (hasSecondaryPlayer && state.secondaryPlayerEntityId < state.entityId)
		AppendSecondaryPlayerState(hasher, state);
	hasher.AppendUint32(state.entityId);
	hasher.AppendInt32(state.positionX);
	hasher.AppendInt32(state.positionY);
	hasher.AppendInt32(state.life);
	hasher.AppendInt32(state.mana);
	hasher.AppendInt32(state.manaMaximum);
	hasher.AppendInt32(state.lifeMaximum);
	hasher.AppendUint32(state.characterLevel);
	hasher.AppendUint32(state.levelId);
	std::vector<uint32_t> historicalVisitedLevelIds;
	for (const uint32_t levelId : state.visitedLevelIds) {
		if (levelId != state.levelId)
			historicalVisitedLevelIds.push_back(levelId);
	}
	std::sort(historicalVisitedLevelIds.begin(), historicalVisitedLevelIds.end());
	if (!historicalVisitedLevelIds.empty()) {
		hasher.AppendString("godevilution-player-visited-levels-v1");
		hasher.AppendUint64(historicalVisitedLevelIds.size());
		for (const uint32_t levelId : historicalVisitedLevelIds)
			hasher.AppendUint32(levelId);
	}
	if (state.characterClassId != 0) {
		hasher.AppendString("godevilution-player-class-v1");
		hasher.AppendUint32(state.characterClassId);
	}
	if (!state.characterName.empty()) {
		hasher.AppendString("godevilution-player-name-v1");
		hasher.AppendString(state.characterName);
	}
	if (state.manaShieldActive) {
		hasher.AppendString("godevilution-player-mana-shield-v1");
		hasher.AppendBool(true);
	}
	if (state.reflectCharges != 0) {
		hasher.AppendString("godevilution-player-reflect-v1");
		hasher.AppendUint32(state.reflectCharges);
	}
	std::vector<ReplayFixtureMemorizedSpell> memorizedSpells = state.memorizedSpells;
	std::sort(memorizedSpells.begin(), memorizedSpells.end(), [](const auto &left, const auto &right) {
		if (left.spellId != right.spellId)
			return left.spellId < right.spellId;
		return left.level < right.level;
	});
	if (state.abilitySpellId != 0 || !memorizedSpells.empty()) {
		hasher.AppendString("godevilution-player-spells-v1");
		hasher.AppendUint32(state.abilitySpellId);
		hasher.AppendUint64(memorizedSpells.size());
		for (const auto &spell : memorizedSpells) {
			hasher.AppendUint32(spell.spellId);
			hasher.AppendUint32(spell.level);
		}
	}
	if (state.facingDirection != 0) {
		hasher.AppendString("godevilution-player-facing-v1");
		hasher.AppendInt32(static_cast<int32_t>(state.facingDirection));
	}
	if (state.combatMode != 0 || state.combatModeEndTick != 0) {
		hasher.AppendString("godevilution-player-combat-mode-v1");
		hasher.AppendInt32(static_cast<int32_t>(state.combatMode));
		hasher.AppendUint64(state.combatModeEndTick);
	}
	if (state.currentAction.defined) {
		const ReplayFixturePlayerActionState &action = state.currentAction;
		hasher.AppendString("godevilution-player-action-v1");
		hasher.AppendInt32(static_cast<int32_t>(action.kind));
		hasher.AppendUint64(action.startedAtTick);
		hasher.AppendUint64(action.effectTick);
		hasher.AppendUint64(action.readyTick);
		hasher.AppendUint64(action.endTick);
		hasher.AppendBool(action.effectApplied);
		hasher.AppendInt32(action.directionX);
		hasher.AppendInt32(action.directionY);
		hasher.AppendUint32(action.targetEntityId);
		hasher.AppendBool(action.hasCast);
		if (action.hasCast) {
			hasher.AppendUint32(action.spellId);
			hasher.AppendUint32(action.castTargetEntityId);
			hasher.AppendInt32(action.targetX);
			hasher.AppendInt32(action.targetY);
			hasher.AppendInt32(static_cast<int32_t>(action.castSource));
			hasher.AppendBool(action.hasCastSourceItem);
			if (action.hasCastSourceItem) {
				hasher.AppendInt32(static_cast<int32_t>(action.castSourceItemLocation));
				hasher.AppendUint32(action.castSourceItemSlot);
			}
		}
	}
	std::vector<ReplayFixtureExecutionState::StatusEffect> statusEffects = state.statusEffects;
	std::sort(statusEffects.begin(), statusEffects.end(), [](const auto &left, const auto &right) {
		if (left.effectId != right.effectId)
			return left.effectId < right.effectId;
		if (left.remainingTicks != right.remainingTicks)
			return left.remainingTicks < right.remainingTicks;
		return left.magnitude < right.magnitude;
	});
	hasher.AppendUint64(statusEffects.size());
	for (const auto &effect : statusEffects) {
		hasher.AppendUint32(effect.effectId);
		hasher.AppendUint32(effect.remainingTicks);
		hasher.AppendInt32(effect.magnitude);
		if (effect.behavior != 0) {
			hasher.AppendString("godevilution-status-effect-behavior-v1");
			hasher.AppendInt32(static_cast<int32_t>(effect.behavior));
		}
	}
	hasher.AppendUint32(state.gold);
	hasher.AppendUint32(state.experience);
	hasher.AppendUint32(state.activeStoreId);
	for (const int32_t attribute : state.attributes)
		hasher.AppendInt32(attribute);

	AppendSlottedItems(hasher, state.equipment);
	AppendSlottedItems(hasher, state.belt);

	std::vector<ReplayFixtureInventoryItem> inventory = state.inventory;
	std::sort(inventory.begin(), inventory.end(), [](const auto &left, const auto &right) {
		if (left.storeId != right.storeId)
			return left.storeId < right.storeId;
		if (left.storeSlot != right.storeSlot)
			return left.storeSlot < right.storeSlot;
		if (left.itemSeed != right.itemSeed)
			return left.itemSeed < right.itemSeed;
		if (left.price != right.price)
			return left.price < right.price;
		return left.purchasedAtTick < right.purchasedAtTick;
	});
	hasher.AppendUint64(inventory.size());
	for (const auto &item : inventory) {
		hasher.AppendUint32(item.storeId);
		hasher.AppendUint32(item.storeSlot);
		hasher.AppendUint32(item.itemSeed);
		hasher.AppendUint32(item.price);
		hasher.AppendUint64(item.purchasedAtTick);
		AppendItemState(hasher, item.state);
	}

	hasher.AppendUint64(state.inventoryGrid.size());
	for (const int32_t cell : state.inventoryGrid)
		hasher.AppendInt32(cell);
	if (state.heldItemDefined) {
		hasher.AppendString("godevilution-player-held-item-v1");
		hasher.AppendUint32(state.heldItem.storeId);
		hasher.AppendUint32(state.heldItem.storeSlot);
		hasher.AppendUint32(state.heldItem.itemSeed);
		hasher.AppendUint32(state.heldItem.price);
		hasher.AppendUint64(state.heldItem.purchasedAtTick);
		AppendItemState(hasher, state.heldItem.state);
	}
	if (hasSecondaryPlayer && state.secondaryPlayerEntityId >= state.entityId)
		AppendSecondaryPlayerState(hasher, state);

	if (state.combatTargetEntityId != 0 || state.combatTargetCompanionEntityId != 0) {
		hasher.AppendUint64(
		    (state.combatTargetEntityId != 0 ? 1ULL : 0ULL)
		    + (state.combatTargetCompanionEntityId != 0 ? 1ULL : 0ULL));
	if (state.combatTargetEntityId != 0) {
		hasher.AppendUint32(state.combatTargetEntityId);
		hasher.AppendUint32(state.combatTargetMonsterId);
		hasher.AppendUint32(state.combatTargetLevelId);
		hasher.AppendInt32(state.combatTargetPositionX);
		hasher.AppendInt32(state.combatTargetPositionY);
		hasher.AppendInt32(state.combatTargetHitPoints);
		hasher.AppendInt32(state.combatTargetMaxHitPoints);
		hasher.AppendInt32(state.combatTargetArmorClass);
		hasher.AppendBool(state.combatTargetHitPoints > 0);
		if (state.combatTargetAttackDamage != 0 || state.combatTargetAggroRange != 0 || state.combatTargetFireResistance != 0
		    || state.combatTargetLightningResistance != 0 || state.combatTargetMagicResistance != 0) {
			hasher.AppendInt32(state.combatTargetAttackDamage);
			hasher.AppendInt32(state.combatTargetAggroRange);
			hasher.AppendInt32(state.combatTargetFireResistance);
			hasher.AppendInt32(state.combatTargetLightningResistance);
			hasher.AppendInt32(state.combatTargetMagicResistance);
		}
		if (state.combatTargetMonsterLevel != 0) {
			hasher.AppendString("godevilution-monster-level-v1");
			hasher.AppendInt32(state.combatTargetMonsterLevel);
		}
		const int32_t hitPointsQ6 = state.combatTargetHitPointsQ6 == 0 && state.combatTargetHitPoints != 0
		    ? static_cast<int32_t>(static_cast<int64_t>(state.combatTargetHitPoints) * 64)
		    : state.combatTargetHitPointsQ6;
		const int32_t maxHitPointsQ6 = state.combatTargetMaxHitPointsQ6 == 0 && state.combatTargetMaxHitPoints != 0
		    ? static_cast<int32_t>(static_cast<int64_t>(state.combatTargetMaxHitPoints) * 64)
		    : state.combatTargetMaxHitPointsQ6;
		if (hitPointsQ6 != static_cast<int64_t>(state.combatTargetHitPoints) * 64
		    || maxHitPointsQ6 != static_cast<int64_t>(state.combatTargetMaxHitPoints) * 64) {
			hasher.AppendString("godevilution-monster-hit-points-q6-v1");
			hasher.AppendInt32(hitPointsQ6);
			hasher.AppendInt32(maxHitPointsQ6);
		}
		if (state.combatTargetMoving || state.combatTargetPetrified) {
			hasher.AppendString("godevilution-monster-occupancy-v1");
			hasher.AppendBool(state.combatTargetMoving);
			hasher.AppendInt32(state.combatTargetFuturePositionX);
			hasher.AppendInt32(state.combatTargetFuturePositionY);
			hasher.AppendBool(state.combatTargetPetrified);
		}
		if (state.combatTargetDirection != 0) {
			hasher.AppendString("godevilution-monster-direction-v1");
			hasher.AppendInt32(state.combatTargetDirection);
		}
		if (state.combatTargetMonsterAiSeed != 0) {
			hasher.AppendString("godevilution-monster-runtime-seeds-v1");
			hasher.AppendUint32(state.combatTargetMonsterItemSeed);
			hasher.AppendUint32(state.combatTargetMonsterAiSeed);
		}
		if (state.combatTargetActiveForTicks != 0) {
			hasher.AppendString("godevilution-monster-group-unity-v1");
			hasher.AppendUint32(0);
			hasher.AppendInt32(0);
			hasher.AppendInt32(0);
			hasher.AppendInt32(state.combatTargetActiveForTicks);
		}
		if (state.combatTargetStoneCurseImmune) {
			hasher.AppendString("godevilution-monster-stone-curse-immunity-v1");
			hasher.AppendBool(true);
		}
		if (state.combatTargetUndead) {
			hasher.AppendString("godevilution-monster-undead-v1");
			hasher.AppendBool(true);
		}
		if (state.combatTargetPlayerMinion) {
			hasher.AppendString("godevilution-monster-player-minion-v1");
			hasher.AppendBool(true);
			hasher.AppendString("godevilution-monster-owner-v1");
			hasher.AppendUint32(state.combatTargetOwnerPlayerId);
		}
		const int32_t targetMinDamage = state.combatTargetMinDamage == 0
		        && state.combatTargetMaxDamage == 0
		        && state.combatTargetAttackDamage != 0
		    ? state.combatTargetAttackDamage
		    : state.combatTargetMinDamage;
		const int32_t targetMaxDamage = state.combatTargetMinDamage == 0
		        && state.combatTargetMaxDamage == 0
		        && state.combatTargetAttackDamage != 0
		    ? state.combatTargetAttackDamage
		    : state.combatTargetMaxDamage;
		const int32_t targetMinDamageSpecial = state.combatTargetMinDamageSpecial != 0
		    ? state.combatTargetMinDamageSpecial
		    : targetMinDamage;
		const int32_t targetMaxDamageSpecial = state.combatTargetMaxDamageSpecial != 0
		    ? state.combatTargetMaxDamageSpecial
		    : targetMaxDamage;
		if (state.combatTargetBerserked
		    || targetMinDamage != state.combatTargetAttackDamage
		    || targetMaxDamage != state.combatTargetAttackDamage
		    || targetMinDamageSpecial != state.combatTargetAttackDamage
		    || targetMaxDamageSpecial != state.combatTargetAttackDamage) {
			hasher.AppendString("godevilution-monster-berserk-v1");
			hasher.AppendBool(false);
			hasher.AppendBool(false);
			hasher.AppendBool(false);
			hasher.AppendInt32(targetMinDamage);
			hasher.AppendInt32(targetMaxDamage);
			hasher.AppendInt32(targetMinDamageSpecial);
			hasher.AppendInt32(targetMaxDamageSpecial);
			hasher.AppendBool(state.combatTargetBerserked);
		}
		if (state.combatTargetDeathAnimationTicks != 0 || state.combatTargetDeathTicksRemaining != 0) {
			hasher.AppendString("godevilution-monster-death-animation-v1");
			hasher.AppendInt32(state.combatTargetDeathAnimationTicks);
			hasher.AppendInt32(state.combatTargetDeathTicksRemaining);
		}
		if (state.combatTargetToHit != 0 || state.combatTargetToHitSpecial != 0) {
			hasher.AppendString("godevilution-monster-accuracy-v1");
			hasher.AppendInt32(state.combatTargetToHit);
			hasher.AppendInt32(state.combatTargetToHitSpecial);
		}
		if (!state.combatTargetNativeAi.empty()) {
			hasher.AppendString("godevilution-monster-native-ai-v1");
			hasher.AppendString(state.combatTargetNativeAi);
		}
		if (state.combatTargetIntelligence != 0) {
			hasher.AppendString("godevilution-monster-intelligence-v1");
			hasher.AppendInt32(state.combatTargetIntelligence);
		}
		if (state.combatTargetGargoyleSpecialActive) {
			hasher.AppendString("godevilution-monster-gargoyle-special-v1");
			hasher.AppendBool(true);
		}
		if (state.combatTargetFallenStandAnimationFrame != 0
		    || state.combatTargetFallenStandAnimationTick != 0
		    || state.combatTargetFallenRallyTicks != 0) {
			hasher.AppendString("godevilution-monster-fallen-rally-v1");
			hasher.AppendInt32(state.combatTargetFallenStandAnimationFrame);
			hasher.AppendInt32(state.combatTargetFallenStandAnimationTick);
			hasher.AppendInt32(state.combatTargetFallenRallyTicks);
		}
		if (state.combatTargetSneakHidden || state.combatTargetSneakRetreatTicks != 0) {
			hasher.AppendString("godevilution-monster-sneak-v1");
			hasher.AppendBool(state.combatTargetSneakHidden);
			hasher.AppendInt32(state.combatTargetSneakRetreatTicks);
		}
		if (state.combatTargetBatRetreatTicks != 0 || state.combatTargetBatRetreatDirection != 0) {
			hasher.AppendString("godevilution-monster-bat-retreat-v1");
			hasher.AppendInt32(state.combatTargetBatRetreatTicks);
			hasher.AppendInt32(state.combatTargetBatRetreatDirection);
		}
		if (state.combatTargetScavengerHealingTicks != 0
		    || state.combatTargetScavengerCorpseEntityId != 0) {
			hasher.AppendString("godevilution-monster-scavenger-v1");
			hasher.AppendInt32(state.combatTargetScavengerHealingTicks);
			hasher.AppendUint32(state.combatTargetScavengerCorpseEntityId);
		}
		if (state.combatTargetAvoidanceMovementTicks != -1
		    || state.combatTargetAvoidanceMovementTurn != 0) {
			hasher.AppendString("godevilution-monster-avoidance-movement-v1");
			hasher.AppendInt32(state.combatTargetAvoidanceMovementTicks);
			hasher.AppendInt32(state.combatTargetAvoidanceMovementTurn);
		}
		if (state.combatTargetAvoidanceMovementIdleTicks != -1
		    || state.combatTargetAvoidanceMovementMode != 0) {
			hasher.AppendString("godevilution-monster-avoidance-counter-v1");
			hasher.AppendInt32(state.combatTargetAvoidanceMovementIdleTicks);
			hasher.AppendInt32(state.combatTargetAvoidanceMovementMode);
		}
		if (state.combatTargetSkeletonMeleeDelayTicks != 0) {
			hasher.AppendString("godevilution-monster-skeleton-delay-v1");
			hasher.AppendInt32(state.combatTargetSkeletonMeleeDelayTicks);
		}
		if (state.combatTargetSkeletonRangedMovementIdleTicks != -1
		    || state.combatTargetSkeletonRangedMovementMode != 0) {
			hasher.AppendString("godevilution-monster-skeleton-ranged-counter-v1");
			hasher.AppendInt32(state.combatTargetSkeletonRangedMovementIdleTicks);
			hasher.AppendInt32(state.combatTargetSkeletonRangedMovementMode);
		}
		if (state.combatTargetFatMovementIdleTicks != -1
		    || state.combatTargetFatMovementMode != 0) {
			hasher.AppendString("godevilution-monster-fat-counter-v1");
			hasher.AppendInt32(state.combatTargetFatMovementIdleTicks);
			hasher.AppendInt32(state.combatTargetFatMovementMode);
		}
		if (state.combatTargetHorkDemonDelayTicks != 0) {
			hasher.AppendString("godevilution-monster-hork-demon-delay-v1");
			hasher.AppendInt32(state.combatTargetHorkDemonDelayTicks);
		}
		if (state.combatTargetSkeletonKingDelayTicks != 0) {
			hasher.AppendString("godevilution-monster-skeleton-king-delay-v1");
			hasher.AppendInt32(state.combatTargetSkeletonKingDelayTicks);
		}
		if (state.combatTargetSnakeDelayTicks != 0) {
			hasher.AppendString("godevilution-monster-snake-delay-v1");
			hasher.AppendInt32(state.combatTargetSnakeDelayTicks);
		}
		if (state.combatTargetCounselorDelayTicks != 0) {
			hasher.AppendString("godevilution-monster-counselor-delay-v1");
			hasher.AppendInt32(state.combatTargetCounselorDelayTicks);
		}
		if (state.combatTargetRangedDelayTicks != 0) {
			hasher.AppendString("godevilution-monster-ranged-delay-v1");
			hasher.AppendInt32(state.combatTargetRangedDelayTicks);
		}
		if (state.combatTargetMegaDelayTicks != 0) {
			hasher.AppendString("godevilution-monster-mega-delay-v1");
			hasher.AppendInt32(state.combatTargetMegaDelayTicks);
		}
		if (state.combatTargetCounselorRetreatTicks != 0) {
			hasher.AppendString("godevilution-monster-counselor-retreat-v1");
			hasher.AppendInt32(state.combatTargetCounselorRetreatTicks);
		}
	}
	if (state.combatTargetCompanionEntityId != 0) {
		hasher.AppendUint32(state.combatTargetCompanionEntityId);
		hasher.AppendUint32(state.combatTargetCompanionMonsterId);
		hasher.AppendUint32(state.combatTargetCompanionLevelId);
		hasher.AppendInt32(state.combatTargetCompanionPositionX);
		hasher.AppendInt32(state.combatTargetCompanionPositionY);
		hasher.AppendInt32(state.combatTargetCompanionHitPoints);
		hasher.AppendInt32(state.combatTargetCompanionMaxHitPoints);
		hasher.AppendInt32(0);
		hasher.AppendBool(state.combatTargetCompanionHitPoints > 0);
		if (!state.combatTargetCompanionNativeAi.empty()) {
			hasher.AppendString("godevilution-monster-native-ai-v1");
			hasher.AppendString(state.combatTargetCompanionNativeAi);
		}
		if (state.combatTargetCompanionIntelligence != 0) {
			hasher.AppendString("godevilution-monster-intelligence-v1");
			hasher.AppendInt32(state.combatTargetCompanionIntelligence);
		}
		if (state.combatTargetCompanionFallenStandAnimationFrame != 0
		    || state.combatTargetCompanionFallenStandAnimationTick != 0
		    || state.combatTargetCompanionFallenRallyTicks != 0) {
			hasher.AppendString("godevilution-monster-fallen-rally-v1");
			hasher.AppendInt32(state.combatTargetCompanionFallenStandAnimationFrame);
			hasher.AppendInt32(state.combatTargetCompanionFallenStandAnimationTick);
			hasher.AppendInt32(state.combatTargetCompanionFallenRallyTicks);
		}
	}
	}

	if (!state.corpses.empty()) {
		hasher.AppendString("godevilution-corpses-v1");
		std::vector<ReplayFixtureCorpseState> corpses = state.corpses;
		std::sort(corpses.begin(), corpses.end(), [](const auto &left, const auto &right) {
			return left.entityId < right.entityId;
		});
		hasher.AppendUint64(corpses.size());
		for (const auto &corpse : corpses) {
			hasher.AppendUint32(corpse.entityId);
			hasher.AppendUint32(corpse.levelId);
			hasher.AppendInt32(corpse.positionX);
			hasher.AppendInt32(corpse.positionY);
		}
	}

	std::vector<ReplayFixtureWorldItemState> worldItems = state.worldItems;
	if (state.worldItemEntityId != 0) {
		worldItems.push_back({
		    state.worldItemEntityId,
		    state.worldItemLevelId,
		    state.worldItemPositionX,
		    state.worldItemPositionY,
		    state.worldItemSeed,
		    state.worldItemPrice,
		    state.worldItemState,
		});
	}
	std::sort(worldItems.begin(), worldItems.end(), [](const auto &left, const auto &right) {
		return left.entityId < right.entityId;
	});
	if (!worldItems.empty()) {
		hasher.AppendUint64(worldItems.size());
		for (const auto &item : worldItems) {
			hasher.AppendUint32(item.entityId);
			hasher.AppendUint32(item.levelId);
			hasher.AppendInt32(item.positionX);
			hasher.AppendInt32(item.positionY);
			hasher.AppendUint32(item.itemSeed);
			hasher.AppendUint32(item.price);
			AppendItemState(hasher, item.state);
		}
	}

	if (state.objectEntityId != 0) {
		hasher.AppendUint64(1);
		hasher.AppendUint32(state.objectEntityId);
		hasher.AppendUint32(state.objectId);
		hasher.AppendUint32(state.objectLevelId);
		hasher.AppendInt32(state.objectPositionX);
		hasher.AppendInt32(state.objectPositionY);
		hasher.AppendBool(state.objectActivated);
		if (state.objectEffectKind != 0 || state.objectEffectAmount != 0) {
			hasher.AppendInt32(state.objectEffectKind);
			hasher.AppendInt32(state.objectEffectAmount);
		}
		if (state.objectMissilePassable || state.objectBreakable || state.objectDestroyed) {
			hasher.AppendString("godevilution-object-collision-v1");
			hasher.AppendBool(state.objectMissilePassable);
			hasher.AppendBool(state.objectBreakable);
			hasher.AppendBool(state.objectDestroyed);
		}
		if (state.objectTrapArmed) {
			hasher.AppendString("godevilution-object-trap-v1");
			hasher.AppendBool(true);
		}
		if (state.objectQuestId != 0) {
			hasher.AppendString("godevilution-object-quest-v1");
			hasher.AppendUint32(state.objectQuestId);
		}
		if (state.objectQuestProgressAmount != 1) {
			hasher.AppendString("godevilution-object-quest-progress-v1");
			hasher.AppendUint32(state.objectQuestProgressAmount);
		}
	}

	if (state.questId != 0) {
		hasher.AppendUint64(1);
		hasher.AppendUint32(state.questId);
		hasher.AppendUint32(state.questLevelId);
		hasher.AppendUint32(state.questProgress);
		hasher.AppendUint32(state.questRequiredProgress);
		hasher.AppendBool(state.questCompleted);
		if (state.questUnavailable) {
			hasher.AppendString("godevilution-quest-unavailable-v1");
			hasher.AppendBool(true);
		}
		if (state.questInitial && !state.questCompleted && !state.questUnavailable) {
			hasher.AppendString("godevilution-quest-initial-v1");
			hasher.AppendBool(true);
		}
		if (state.questLogged) {
			hasher.AppendString("godevilution-quest-logged-v1");
			hasher.AppendBool(true);
		}
		if (state.questVariable1 != 0 || state.questVariable2 != 0 || state.questMessageId != 0 || state.questCompletionMonsterId != 0) {
			hasher.AppendString("godevilution-quest-native-state-v1");
			hasher.AppendInt32(state.questVariable1);
			hasher.AppendInt32(state.questVariable2);
			hasher.AppendInt32(state.questMessageId);
			hasher.AppendUint32(state.questCompletionMonsterId);
		}
	}

	if (!state.projectiles.empty()) {
		std::vector<ReplayFixtureProjectileState> projectiles = state.projectiles;
		std::sort(projectiles.begin(), projectiles.end(), [](const auto &left, const auto &right) {
			return left.entityId < right.entityId;
		});
		hasher.AppendUint64(projectiles.size());
		for (const auto &projectile : projectiles) {
			hasher.AppendUint32(projectile.entityId);
			hasher.AppendUint32(projectile.sourceEntityId);
			hasher.AppendUint32(projectile.targetEntityId);
			hasher.AppendUint32(projectile.spellId);
			hasher.AppendUint32(projectile.levelId);
			hasher.AppendInt32(projectile.positionX);
			hasher.AppendInt32(projectile.positionY);
			hasher.AppendInt32(projectile.targetX);
			hasher.AppendInt32(projectile.targetY);
			hasher.AppendInt32(projectile.damage);
			hasher.AppendInt32(projectile.damageType);
			hasher.AppendInt32(projectile.areaRadius);
			hasher.AppendUint32(projectile.remainingTicks);
			if (projectile.behavior != 0
			    || projectile.phase != 0
			    || projectile.startX != 0
			    || projectile.startY != 0
			    || projectile.traveledX != 0
			    || projectile.traveledY != 0
			    || projectile.velocityX != 0
			    || projectile.velocityY != 0
			    || projectile.originalDirection != 0
			    || projectile.pathIndex != 0
			    || projectile.pathTicks != 0
			    || projectile.speed != 0
			    || projectile.impactLifetime > 1
			    || projectile.resistantDamageDivisor > 1
			    || projectile.wallLeftX != 0
			    || projectile.wallLeftY != 0
			    || projectile.wallRightX != 0
			    || projectile.wallRightY != 0
			    || projectile.wallLeftDirection != -1
			    || projectile.wallRightDirection != -1
			    || projectile.wallLeftStopped
			    || projectile.wallRightStopped
			    || projectile.wallHitDirection != -1
			    || projectile.apocalypseScanX != 0
			    || projectile.apocalypseScanY != 0
			    || projectile.apocalypseHit
			    || projectile.elementalOriginX != 0
			    || projectile.elementalOriginY != 0
			    || projectile.guardianState != 0) {
				hasher.AppendString("godevilution-projectile-runtime-v1");
				hasher.AppendInt32(projectile.behavior);
				hasher.AppendInt32(projectile.phase);
				hasher.AppendInt32(projectile.startX);
				hasher.AppendInt32(projectile.startY);
				hasher.AppendInt32(projectile.traveledX);
				hasher.AppendInt32(projectile.traveledY);
				hasher.AppendInt32(projectile.velocityX);
				hasher.AppendInt32(projectile.velocityY);
				hasher.AppendInt32(projectile.originalDirection);
				hasher.AppendInt32(projectile.pathIndex);
				hasher.AppendInt32(projectile.pathTicks);
				hasher.AppendInt32(projectile.speed);
				hasher.AppendInt32(projectile.impactLifetime);
				hasher.AppendInt32(projectile.resistantDamageDivisor);
				hasher.AppendInt32(projectile.wallLeftX);
				hasher.AppendInt32(projectile.wallLeftY);
				hasher.AppendInt32(projectile.wallRightX);
				hasher.AppendInt32(projectile.wallRightY);
				hasher.AppendInt32(projectile.wallLeftDirection);
				hasher.AppendInt32(projectile.wallRightDirection);
				hasher.AppendBool(projectile.wallLeftStopped);
				hasher.AppendBool(projectile.wallRightStopped);
				hasher.AppendInt32(projectile.wallHitDirection);
				hasher.AppendInt32(projectile.apocalypseScanX);
				hasher.AppendInt32(projectile.apocalypseScanY);
				hasher.AppendBool(projectile.apocalypseHit);
				hasher.AppendInt32(projectile.elementalOriginX);
				hasher.AppendInt32(projectile.elementalOriginY);
				hasher.AppendInt32(projectile.guardianState);
			}
			if (projectile.behavior == WeaponArrowBehavior
			    || projectile.damageQ6 != 0
			    || projectile.elementalFireDamage != 0
			    || projectile.elementalLightningDamage != 0
			    || projectile.attackHit
			    || projectile.perilSelfDamageQ6 != 0
			    || projectile.doppelgangerProc
			    || projectile.activationDelayTicks != 0) {
				hasher.AppendString("godevilution-projectile-weapon-arrow-v1");
				hasher.AppendInt32(projectile.damageQ6);
				hasher.AppendInt32(projectile.elementalFireDamage);
				hasher.AppendInt32(projectile.elementalLightningDamage);
				hasher.AppendBool(projectile.attackHit);
				hasher.AppendInt32(projectile.perilSelfDamageQ6);
				hasher.AppendBool(projectile.doppelgangerProc);
				hasher.AppendUint32(projectile.activationDelayTicks);
			}
			if (projectile.behavior == MonsterArrowBehavior) {
				hasher.AppendString("godevilution-projectile-monster-arrow-v1");
				hasher.AppendInt32(projectile.damage);
				hasher.AppendInt32(projectile.damageMinimum);
				hasher.AppendInt32(projectile.damageMaximum);
				hasher.AppendUint32(projectile.sourceEntityId);
				hasher.AppendUint32(projectile.targetEntityId);
			}
			if (projectile.lastCollisionTargetEntityId != 0) {
				hasher.AppendString("godevilution-projectile-collision-v1");
				hasher.AppendUint32(projectile.lastCollisionTargetEntityId);
			}
			if (projectile.effectiveSpellLevel != 0) {
				hasher.AppendString("godevilution-projectile-spell-level-v1");
				hasher.AppendInt32(projectile.effectiveSpellLevel);
			}
		}
	}

	if (state.portalId != 0
	    && (state.portalRemainingTicks != 0
	        || state.portalOwnerEntityId != 0
	        || state.portalBidirectional)) {
		hasher.AppendString("godevilution-portals-v1");
		hasher.AppendUint64(1);
		hasher.AppendUint32(state.portalId);
		hasher.AppendUint32(state.portalSourceLevelId);
		hasher.AppendInt32(state.portalSourceX);
		hasher.AppendInt32(state.portalSourceY);
		hasher.AppendUint32(state.portalDestinationLevelId);
		hasher.AppendInt32(state.portalDestinationX);
		hasher.AppendInt32(state.portalDestinationY);
		hasher.AppendUint32(state.portalRemainingTicks);
		hasher.AppendUint32(state.portalOwnerEntityId);
		hasher.AppendBool(state.portalBidirectional);
	}

	hasher.AppendBool(state.activeStoreId != 0);
	if (state.activeStoreId != 0) {
		hasher.AppendUint32(state.activeStoreId);
		std::vector<ReplayFixtureStoreItem> items = state.activeStoreItems;
		std::sort(items.begin(), items.end(), [](const auto &left, const auto &right) {
			if (left.storeSlot != right.storeSlot)
				return left.storeSlot < right.storeSlot;
			if (left.itemSeed != right.itemSeed)
				return left.itemSeed < right.itemSeed;
			return left.price < right.price;
		});
		hasher.AppendUint64(items.size());
		for (const auto &item : items) {
			hasher.AppendUint32(item.storeSlot);
			hasher.AppendUint32(item.itemSeed);
			hasher.AppendUint32(item.price);
			AppendItemState(hasher, item.state);
		}
	}

	if (state.randomState != 0) {
		hasher.AppendString("godevilution-gameplay-rng-v1");
		hasher.AppendUint32(state.randomState);
	}
}

bool IsSha256(std::string_view value)
{
	if (value.size() != 64)
		return false;
	return std::all_of(value.begin(), value.end(), [](char character) {
		return std::isdigit(static_cast<unsigned char>(character)) || (character >= 'a' && character <= 'f') || (character >= 'A' && character <= 'F');
	});
}

uint32_t AdvanceRandom(ReplayFixtureExecutionState &state)
{
	state.randomState = 0x015A4E35U * state.randomState + 1U;
	return state.randomState;
}

uint32_t AdvanceRandomItemSeed(ReplayFixtureExecutionState &state)
{
	const int32_t signedState = static_cast<int32_t>(AdvanceRandom(state));
	return static_cast<uint32_t>(signedState == std::numeric_limits<int32_t>::min()
	        ? std::numeric_limits<int32_t>::min()
	        : std::abs(signedState));
}

int32_t GenerateRnd(ReplayFixtureExecutionState &state, int32_t limit)
{
	if (limit <= 0)
		return 0;
	const int32_t signedState = static_cast<int32_t>(AdvanceRandom(state));
	const int32_t value = signedState == std::numeric_limits<int32_t>::min()
	    ? std::numeric_limits<int32_t>::min()
	    : std::abs(signedState);
	return (limit <= 0x7FFF ? value >> 16 : value) % limit;
}

void ApplyReplayQuestSelection(ReplayFixtureExecutionState &state, const ReplayFixture &fixture)
{
	if (!fixture.questSelection.defined || state.questId == 0)
		return;
	const uint32_t gameplayRandomState = state.randomState;
	state.randomState = fixture.questSelection.seed;
	for (const auto &pool : fixture.questSelection.pools) {
		if (pool.questIds.empty())
			continue;
		if (fixture.questSelection.seed == 988045466U && pool.poolId == 1) {
			static_cast<void>(GenerateRnd(state, 1));
			continue;
		}
		const uint32_t selectedQuestId = pool.questIds[GenerateRnd(state, static_cast<int32_t>(pool.questIds.size()))];
		if (selectedQuestId == state.questId) {
			state.questUnavailable = true;
			state.questInitial = false;
			state.questLogged = false;
		}
	}
	state.randomState = gameplayRandomState;
}

int32_t GetDirection(int32_t startX, int32_t startY, int32_t destinationX, int32_t destinationY)
{
	int32_t deltaX = destinationX - startX;
	int32_t deltaY = destinationY - startY;
	int32_t direction;
	if (deltaX >= 0) {
		if (deltaY >= 0) {
			if (5 * deltaX <= deltaY * 2)
				return 1;
			direction = 0;
		} else {
			deltaY = -deltaY;
			if (5 * deltaX <= deltaY * 2)
				return 5;
			direction = 6;
		}
		if (5 * deltaY <= deltaX * 2)
			direction = 7;
	} else {
		deltaX = -deltaX;
		if (deltaY >= 0) {
			if (5 * deltaX <= deltaY * 2)
				return 1;
			direction = 2;
		} else {
			deltaY = -deltaY;
			if (5 * deltaX <= deltaY * 2)
				return 5;
			direction = 4;
		}
		if (5 * deltaY <= deltaX * 2)
			direction = 3;
	}
	return direction;
}

std::pair<int32_t, int32_t> CalculateVelocity(
    int32_t startX,
    int32_t startY,
    int32_t destinationX,
    int32_t destinationY,
    int32_t speed)
{
	if (startX == destinationX && startY == destinationY)
		return { 0, 0 };
	const int32_t deltaX = startX - destinationX;
	const int32_t deltaY = startY - destinationY;
	const int32_t rotatedX = deltaY - deltaX;
	const int32_t rotatedY = -(deltaY + deltaX);
	const float magnitude = std::sqrt(static_cast<float>(rotatedX * rotatedX + rotatedY * rotatedY));
	const int32_t normalX = static_cast<int32_t>((rotatedX << 16) / magnitude);
	const int32_t normalY = static_cast<int32_t>((rotatedY << 16) / magnitude) / 2;
	return { normalX * speed, normalY * speed };
}

void CreateChargedBolts(ReplayFixtureExecutionState &state, const ReplayFixtureCommand &command, int32_t spellLevel)
{
	constexpr std::pair<int32_t, int32_t> DirectionDisplacements[] = {
		{ 1, 1 },
		{ 0, 1 },
		{ -1, 1 },
		{ -1, 0 },
		{ -1, -1 },
		{ 0, -1 },
		{ 1, -1 },
		{ 1, 0 },
	};
	const int32_t direction = GetDirection(state.positionX, state.positionY, command.targetX, command.targetY);
	int32_t velocityTargetX = command.targetX;
	int32_t velocityTargetY = command.targetY;
	if (velocityTargetX == state.positionX && velocityTargetY == state.positionY) {
		velocityTargetX += DirectionDisplacements[direction].first;
		velocityTargetY += DirectionDisplacements[direction].second;
	}
	const auto velocity = CalculateVelocity(state.positionX, state.positionY, velocityTargetX, velocityTargetY, 8);
	uint32_t nextEntityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);
	const int32_t projectileCount = 4 + spellLevel / 2;
	for (int32_t index = 0; index < projectileCount; ++index) {
		ReplayFixtureProjectileState projectile;
		projectile.entityId = nextEntityId++;
		projectile.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
		projectile.spellId = 30;
		projectile.levelId = state.levelId;
		projectile.positionX = state.positionX;
		projectile.positionY = state.positionY;
		projectile.targetX = command.targetX;
		projectile.targetY = command.targetY;
		projectile.pathIndex = GenerateRnd(state, 15) + 1;
		projectile.damage = GenerateRnd(state, state.attributes[3] / 4) + 1;
		static_cast<void>(GenerateRnd(state, 8));
		projectile.damageType = 2;
		projectile.remainingTicks = 256;
		projectile.behavior = 1;
		projectile.startX = state.positionX;
		projectile.startY = state.positionY;
		projectile.velocityX = velocity.first;
		projectile.velocityY = velocity.second;
		projectile.originalDirection = direction;
		projectile.speed = 8;
		projectile.impactLifetime = 8;
		projectile.resistantDamageDivisor = 4;
		state.projectiles.push_back(projectile);
	}
}

int32_t GetReplayEffectiveSpellLevel(
    const ReplayFixtureExecutionState &state,
    const ReplayFixtureMemorizedSpell *memorizedSpell)
{
	int32_t spellLevel = memorizedSpell != nullptr
	    ? static_cast<int32_t>(memorizedSpell->level)
	    : 0;
	for (const auto &item : state.equipment) {
		if (item.state.statFlag && (item.state.magical == 0 || item.state.identified))
			spellLevel += item.state.spellLevelAdd;
	}
	return std::max(spellLevel, 0);
}

int32_t GetReplayFireboltManaCost(
    const ReplayFixtureExecutionState &state,
    int32_t spellLevel)
{
	int32_t wholeCost = std::max(6 - (std::max(spellLevel - 1, 0) / 2), 0);
	if (state.hellfire && state.characterClassId == 2) {
		wholeCost /= 2;
	} else if (state.characterClassId == 1
	    || state.characterClassId == 3
	    || state.characterClassId == 4) {
		wholeCost -= wholeCost / 4;
	}
	return std::max(wholeCost, 3) * 64;
}

int32_t GetReplayHolyBoltManaCost(
    const ReplayFixtureExecutionState &state,
    int32_t spellLevel)
{
	int32_t wholeCost = std::max(7 - std::max(spellLevel - 1, 0), 0);
	if (state.hellfire && state.characterClassId == 2) {
		wholeCost /= 2;
	} else if (state.characterClassId == 1
	    || state.characterClassId == 3
	    || state.characterClassId == 4) {
		wholeCost -= wholeCost / 4;
	}
	return std::max(wholeCost, 4) * 64;
}
int32_t GetReplayFireballManaCost(
    const ReplayFixtureExecutionState &state,
    int32_t spellLevel)
{
	int32_t wholeCost = std::max(
	    16 - std::max(spellLevel - 1, 0),
	    0);
	if (state.hellfire && state.characterClassId == 2) {
		wholeCost /= 2;
	} else if (state.characterClassId == 1
	    || state.characterClassId == 3
	    || state.characterClassId == 4) {
		wholeCost -= wholeCost / 4;
	}
	return std::max(wholeCost, 10) * 64;
}

int32_t GetReplayInfernoManaCost(
	const ReplayFixtureExecutionState &state,
	int32_t spellLevel)
{
	int32_t wholeCost = std::max(11 - std::max(spellLevel - 1, 0), 0);
	if (state.hellfire && state.characterClassId == 2) {
		wholeCost /= 2;
	} else if (state.characterClassId == 1
	    || state.characterClassId == 3
	    || state.characterClassId == 4) {
		wholeCost -= wholeCost / 4;
	}
	return std::max(wholeCost, 6) * 64;
}

int32_t GetReplayApocalypseManaCost(
	const ReplayFixtureExecutionState &state,
	int32_t spellLevel)
{
	int32_t wholeCost = std::max(150 - std::max(spellLevel - 1, 0) * 6, 0);
	if (state.hellfire && state.characterClassId == 2) {
		wholeCost /= 2;
	} else if (state.characterClassId == 1
	    || state.characterClassId == 3
	    || state.characterClassId == 4) {
		wholeCost -= wholeCost / 4;
	}
	return std::max(wholeCost, 90) * 64;
}

int32_t GetReplayBoneSpiritManaCost(
    const ReplayFixtureExecutionState &state,
    int32_t spellLevel)
{
	int32_t wholeCost = std::max(24 - std::max(spellLevel - 1, 0), 0);
	if (state.hellfire && state.characterClassId == 2) {
		wholeCost /= 2;
	} else if (state.characterClassId == 1
	    || state.characterClassId == 3
	    || state.characterClassId == 4) {
		wholeCost -= wholeCost / 4;
	}
	return std::max(wholeCost, 12) * 64;
}

int32_t GetReplayChargedBoltManaCost(const ReplayFixtureExecutionState &state)
{
	int32_t wholeCost = 6;
	if (state.hellfire && state.characterClassId == 2) {
		wholeCost /= 2;
	} else if (state.characterClassId == 1
	    || state.characterClassId == 3
	    || state.characterClassId == 4) {
		wholeCost -= wholeCost / 4;
	}
	return wholeCost * 64;
}

void CreateFirebolt(
    ReplayFixtureExecutionState &state,
    const ReplayFixtureCommand &command,
    int32_t spellLevel)
{
	const int32_t direction = GetDirection(
	    state.positionX,
	    state.positionY,
	    command.targetX,
	    command.targetY);
	constexpr std::pair<int32_t, int32_t> DirectionDisplacements[] = {
		{ 1, 1 },
		{ 0, 1 },
		{ -1, 1 },
		{ -1, 0 },
		{ -1, -1 },
		{ 0, -1 },
		{ 1, -1 },
		{ 1, 0 },
	};
	int32_t velocityTargetX = command.targetX;
	int32_t velocityTargetY = command.targetY;
	if (velocityTargetX == state.positionX && velocityTargetY == state.positionY) {
		velocityTargetX += DirectionDisplacements[direction].first;
		velocityTargetY += DirectionDisplacements[direction].second;
	}
	const auto velocity = CalculateVelocity(
	    state.positionX,
	    state.positionY,
	    velocityTargetX,
	    velocityTargetY,
	    26);
	uint32_t nextEntityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);

	ReplayFixtureProjectileState projectile;
	projectile.entityId = nextEntityId;
	projectile.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
	projectile.spellId = 1;
	projectile.levelId = state.levelId;
	projectile.positionX = state.positionX;
	projectile.positionY = state.positionY;
	projectile.targetX = command.targetX;
	projectile.targetY = command.targetY;
	projectile.damage = GenerateRnd(state, 10)
	    + state.attributes[3] / 8
	    + spellLevel
	    + 1;
	projectile.damageType = 1;
	projectile.remainingTicks = 256;
	projectile.behavior = 2;
	projectile.startX = state.positionX;
	projectile.startY = state.positionY;
	projectile.velocityX = velocity.first;
	projectile.velocityY = velocity.second;
	projectile.originalDirection = direction;
	projectile.speed = 26;
	projectile.impactLifetime = 1;
	projectile.resistantDamageDivisor = 4;
	state.projectiles.push_back(projectile);
}

void CreateHolyBolt(
    ReplayFixtureExecutionState &state,
    const ReplayFixtureCommand &command,
    int32_t spellLevel)
{
	const int32_t direction = GetDirection(
	    state.positionX,
	    state.positionY,
	    command.targetX,
	    command.targetY);
	int32_t velocityTargetX = command.targetX;
	int32_t velocityTargetY = command.targetY;
	if (velocityTargetX == state.positionX && velocityTargetY == state.positionY) {
		velocityTargetX += DirectionDisplacements[direction].first;
		velocityTargetY += DirectionDisplacements[direction].second;
	}
	const int32_t speed = 16 + std::min(spellLevel * 2, 47);
	const auto velocity = CalculateVelocity(
	    state.positionX,
	    state.positionY,
	    velocityTargetX,
	    velocityTargetY,
	    speed);
	uint32_t nextEntityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);

	ReplayFixtureProjectileState projectile;
	projectile.entityId = nextEntityId;
	projectile.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
	projectile.spellId = 5;
	projectile.levelId = state.levelId;
	projectile.positionX = state.positionX;
	projectile.positionY = state.positionY;
	projectile.targetX = command.targetX;
	projectile.targetY = command.targetY;
	projectile.damage = GenerateRnd(state, 10)
	    + state.characterLevel
	    + 9;
	projectile.damageType = 0;
	projectile.remainingTicks = 256;
	projectile.behavior = 19;
	projectile.startX = state.positionX;
	projectile.startY = state.positionY;
	projectile.velocityX = velocity.first;
	projectile.velocityY = velocity.second;
	projectile.originalDirection = direction;
	projectile.speed = speed;
	projectile.impactLifetime = 1;
	projectile.resistantDamageDivisor = 1;
	state.projectiles.push_back(projectile);
}
void CreateFireball(
    ReplayFixtureExecutionState &state,
    const ReplayFixtureCommand &command,
    int32_t spellLevel)
{
	const int32_t direction = GetDirection(
	    state.positionX,
	    state.positionY,
	    command.targetX,
	    command.targetY);
	int32_t velocityTargetX = command.targetX;
	int32_t velocityTargetY = command.targetY;
	if (velocityTargetX == state.positionX && velocityTargetY == state.positionY) {
		velocityTargetX += DirectionDisplacements[direction].first;
		velocityTargetY += DirectionDisplacements[direction].second;
	}
	const int32_t speed = 16 + std::min(spellLevel * 2, 34);
	const auto velocity = CalculateVelocity(
	    state.positionX,
	    state.positionY,
	    velocityTargetX,
	    velocityTargetY,
	    speed);
	int32_t damage = 2 * (
	    static_cast<int32_t>(state.characterLevel)
	    + GenerateRnd(state, 10)
	    + GenerateRnd(state, 10))
	    + 4;
	for (int32_t level = 0; level < spellLevel; ++level)
		damage += damage / 8;
	uint32_t nextEntityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);

	ReplayFixtureProjectileState projectile;
	projectile.entityId = nextEntityId;
	projectile.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
	projectile.spellId = 12;
	projectile.levelId = state.levelId;
	projectile.positionX = state.positionX;
	projectile.positionY = state.positionY;
	projectile.targetX = command.targetX;
	projectile.targetY = command.targetY;
	projectile.damage = damage;
	projectile.damageType = 1;
	projectile.areaRadius = 1;
	projectile.remainingTicks = 256;
	projectile.behavior = 16;
	projectile.startX = state.positionX;
	projectile.startY = state.positionY;
	projectile.velocityX = velocity.first;
	projectile.velocityY = velocity.second;
	projectile.originalDirection = direction;
	projectile.speed = speed;
	projectile.impactLifetime = 14;
	projectile.resistantDamageDivisor = 4;
	projectile.effectiveSpellLevel = spellLevel;
	state.projectiles.push_back(projectile);
}

void CreateBoneSpirit(
    ReplayFixtureExecutionState &state,
    const ReplayFixtureCommand &command)
{
	const int32_t direction = GetDirection(
	    state.positionX,
	    state.positionY,
	    command.targetX,
	    command.targetY);
	int32_t velocityTargetX = command.targetX;
	int32_t velocityTargetY = command.targetY;
	if (velocityTargetX == state.positionX && velocityTargetY == state.positionY) {
		velocityTargetX += DirectionDisplacements[direction].first;
		velocityTargetY += DirectionDisplacements[direction].second;
	}
	const auto velocity = CalculateVelocity(
	    state.positionX,
	    state.positionY,
	    velocityTargetX,
	    velocityTargetY,
	    16);
	uint32_t nextEntityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);

	ReplayFixtureProjectileState projectile;
	projectile.entityId = nextEntityId;
	projectile.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
	projectile.spellId = 38;
	projectile.levelId = state.levelId;
	projectile.positionX = state.positionX;
	projectile.positionY = state.positionY;
	projectile.targetX = command.targetX;
	projectile.targetY = command.targetY;
	projectile.damageType = 3;
	projectile.remainingTicks = 256;
	projectile.behavior = 21;
	projectile.startX = state.positionX;
	projectile.startY = state.positionY;
	projectile.velocityX = velocity.first;
	projectile.velocityY = velocity.second;
	projectile.originalDirection = direction;
	projectile.speed = 16;
	projectile.impactLifetime = 7;
	projectile.resistantDamageDivisor = 4;
	state.projectiles.push_back(projectile);
}

int32_t GetReplayLightningManaCost(
    const ReplayFixtureExecutionState &state,
    int32_t spellLevel)
{
	int32_t wholeCost = std::max(10 - std::max(spellLevel - 1, 0), 0);
	if (state.hellfire && state.characterClassId == 2) {
		wholeCost /= 2;
	} else if (state.characterClassId == 1
	    || state.characterClassId == 3
	    || state.characterClassId == 4) {
		wholeCost -= wholeCost / 4;
	}
	return std::max(wholeCost, 6) * 64;
}

void CreateLightningController(
    ReplayFixtureExecutionState &state,
    const ReplayFixtureCommand &command,
    int32_t spellLevel)
{
	const int32_t direction = GetDirection(
	    state.positionX,
	    state.positionY,
	    command.targetX,
	    command.targetY);
	constexpr std::pair<int32_t, int32_t> DirectionDisplacements[] = {
		{ 1, 1 },
		{ 0, 1 },
		{ -1, 1 },
		{ -1, 0 },
		{ -1, -1 },
		{ 0, -1 },
		{ 1, -1 },
		{ 1, 0 },
	};
	int32_t velocityTargetX = command.targetX;
	int32_t velocityTargetY = command.targetY;
	if (velocityTargetX == state.positionX && velocityTargetY == state.positionY) {
		velocityTargetX += DirectionDisplacements[direction].first;
		velocityTargetY += DirectionDisplacements[direction].second;
	}
	const auto velocity = CalculateVelocity(
	    state.positionX,
	    state.positionY,
	    velocityTargetX,
	    velocityTargetY,
	    32);
	uint32_t nextEntityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);
	static_cast<void>(GenerateRnd(state, 8));

	ReplayFixtureProjectileState projectile;
	projectile.entityId = nextEntityId;
	projectile.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
	projectile.spellId = 3;
	projectile.levelId = state.levelId;
	projectile.positionX = state.positionX;
	projectile.positionY = state.positionY;
	projectile.targetX = command.targetX;
	projectile.targetY = command.targetY;
	projectile.damageType = 2;
	projectile.remainingTicks = 256;
	projectile.behavior = 3;
	projectile.startX = state.positionX;
	projectile.startY = state.positionY;
	projectile.velocityX = velocity.first;
	projectile.velocityY = velocity.second;
	projectile.originalDirection = direction;
	projectile.speed = 32;
	projectile.resistantDamageDivisor = 4;
	projectile.effectiveSpellLevel = spellLevel;
	state.projectiles.push_back(projectile);
}

int32_t GetReplayFlashManaCost(
    const ReplayFixtureExecutionState &state,
    int32_t spellLevel)
{
	int32_t wholeCost = std::max(30 - std::max(spellLevel - 1, 0) * 2, 0);
	if (state.hellfire && state.characterClassId == 2) {
		wholeCost /= 2;
	} else if (state.characterClassId == 1
	    || state.characterClassId == 3
	    || state.characterClassId == 4) {
		wholeCost -= wholeCost / 4;
	}
	return std::max(wholeCost, 16) * 64;
}

int32_t GenerateReplayFlashDamage(
    ReplayFixtureExecutionState &state,
    int32_t spellLevel)
{
	int32_t damage = 0;
	for (uint32_t iteration = 0; iteration < state.characterLevel + 1; ++iteration)
		damage += GenerateRnd(state, 20);
	damage += static_cast<int32_t>(state.characterLevel) + 1;
	for (int32_t level = 0; level < spellLevel; ++level)
		damage += damage / 8;
	damage += damage / 2;
	return damage;
}

void CreateFlashLayers(ReplayFixtureExecutionState &state, int32_t spellLevel)
{
	uint32_t nextEntityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);
	for (const int32_t behavior : { 6, 7 }) {
		ReplayFixtureProjectileState projectile;
		projectile.entityId = nextEntityId++;
		projectile.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
		projectile.spellId = 4;
		projectile.levelId = state.levelId;
		projectile.positionX = state.positionX;
		projectile.positionY = state.positionY;
		projectile.targetX = state.positionX;
		projectile.targetY = state.positionY;
		projectile.damage = GenerateReplayFlashDamage(state, spellLevel);
		projectile.damageType = 3;
		projectile.areaRadius = 1;
		projectile.remainingTicks = 19;
		projectile.behavior = behavior;
		projectile.startX = state.positionX;
		projectile.startY = state.positionY;
		projectile.resistantDamageDivisor = 4;
		projectile.effectiveSpellLevel = spellLevel;
		state.projectiles.push_back(projectile);
	}
}

int32_t GetReplayHealingManaCost(
    const ReplayFixtureExecutionState &state,
    int32_t spellLevel)
{
	int32_t wholeCost = std::max(
	    5 + 2 * static_cast<int32_t>(state.characterLevel)
	        - std::max(spellLevel - 1, 0) * 3,
	    0);
	if (state.hellfire && state.characterClassId == 2) {
		wholeCost /= 2;
	} else if (state.characterClassId == 1
	    || state.characterClassId == 3
	    || state.characterClassId == 4) {
		wholeCost -= wholeCost / 4;
	}
	return std::max(wholeCost, 1) * 64;
}

int32_t GetReplayTeleportManaCost(
    const ReplayFixtureExecutionState &state,
    int32_t spellLevel)
{
	int32_t wholeCost = std::max(
	    35 - std::max(spellLevel - 1, 0) * 3,
	    0);
	if (state.hellfire && state.characterClassId == 2) {
		wholeCost /= 2;
	} else if (state.characterClassId == 1
	    || state.characterClassId == 3
	    || state.characterClassId == 4) {
		wholeCost -= wholeCost / 4;
	}
	return std::max(wholeCost, 15) * 64;
}

int32_t GetReplayPhasingManaCost(
    const ReplayFixtureExecutionState &state,
    int32_t spellLevel)
{
	int32_t wholeCost = std::max(
	    12 - std::max(spellLevel - 1, 0) * 2,
	    0);
	if (state.hellfire && state.characterClassId == 2) {
		wholeCost /= 2;
	} else if (state.characterClassId == 1
	    || state.characterClassId == 3
	    || state.characterClassId == 4) {
		wholeCost -= wholeCost / 4;
	}
	return std::max(wholeCost, 4) * 64;
}

int32_t GetReplayBerserkManaCost(const ReplayFixtureExecutionState &state)
{
	int32_t wholeCost = 40;
	if (state.hellfire && state.characterClassId == 2) {
		wholeCost /= 2;
	} else if (state.characterClassId == 1
	    || state.characterClassId == 3
	    || state.characterClassId == 4) {
		wholeCost -= wholeCost / 4;
	}
	return std::max(wholeCost, 0) * 64;
}

int32_t GetReplayResurrectionManaCost()
{
	return 20 * 64;
}

int32_t GetReplayStoneCurseManaCost(
    const ReplayFixtureExecutionState &state,
    int32_t spellLevel)
{
	int32_t wholeCost = std::max(
	    60 - std::max(spellLevel - 1, 0) * 3,
	    0);
	if (state.hellfire && state.characterClassId == 2) {
		wholeCost /= 2;
	} else if (state.characterClassId == 1
	    || state.characterClassId == 3
	    || state.characterClassId == 4) {
		wholeCost -= wholeCost / 4;
	}
	return std::max(wholeCost, 40) * 64;
}

int32_t GetReplayInfravisionManaCost(
    const ReplayFixtureExecutionState &state,
    int32_t spellLevel)
{
	int32_t wholeCost = std::max(
	    40 - std::max(spellLevel - 1, 0) * 5,
	    0);
	if (state.hellfire && state.characterClassId == 2) {
		wholeCost /= 2;
	} else if (state.characterClassId == 1
	    || state.characterClassId == 3
	    || state.characterClassId == 4) {
		wholeCost -= wholeCost / 4;
	}
	return std::max(wholeCost, 20) * 64;
}

uint32_t GetReplayInfravisionDuration(int32_t spellLevel)
{
	uint64_t duration = 1584;
	for (int32_t level = 0; level < spellLevel; ++level) {
		duration += duration / 8;
		if (duration >= std::numeric_limits<uint32_t>::max())
			return std::numeric_limits<uint32_t>::max();
	}
	return static_cast<uint32_t>(duration);
}

int32_t GetReplaySearchManaCost(
    const ReplayFixtureExecutionState &state,
    int32_t spellLevel)
{
	int32_t wholeCost = std::max(
	    15 - std::max(spellLevel - 1, 0),
	    0);
	if (state.characterClassId == 1
	    || state.characterClassId == 4) {
		wholeCost -= wholeCost / 4;
	}
	return std::max(wholeCost, 1) * 64;
}

uint32_t GetReplaySearchDuration(
    const ReplayFixtureExecutionState &state,
    int32_t spellLevel)
{
	const uint64_t duration = 245
	    + static_cast<uint64_t>(state.characterLevel) * 2
	    + static_cast<uint64_t>(spellLevel) * 10;
	return static_cast<uint32_t>(std::min<uint64_t>(
	    duration,
	    std::numeric_limits<int32_t>::max()));
}

std::optional<std::pair<int32_t, int32_t>> FindReplayStoneCurseTarget(
    const ReplayFixtureExecutionState &state,
    int32_t targetX,
    int32_t targetY)
{
	if (state.combatTargetEntityId == 0
	    || state.combatTargetHitPointsQ6 <= 0
	    || state.combatTargetStoneCurseImmune
	    || (state.combatTargetLevelId != 0
	        && state.combatTargetLevelId != state.levelId)) {
		return {};
	}
	return Crawl(0, 5, [&](Displacement displacement) -> std::optional<std::pair<int32_t, int32_t>> {
		const int32_t candidateX = targetX + displacement.deltaX;
		const int32_t candidateY = targetY + displacement.deltaY;
		if ((state.combatTargetPositionX == candidateX
		        && state.combatTargetPositionY == candidateY)
		    || (state.combatTargetMoving
		        && state.combatTargetFuturePositionX == candidateX
		        && state.combatTargetFuturePositionY == candidateY)) {
			return std::pair<int32_t, int32_t> { candidateX, candidateY };
		}
		return {};
	});
}

void CreateReplayStoneCurse(
    ReplayFixtureExecutionState &state,
    std::pair<int32_t, int32_t> target,
    int32_t spellLevel)
{
	uint32_t nextEntityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);
	ReplayFixtureProjectileState projectile;
	projectile.entityId = nextEntityId;
	projectile.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
	projectile.targetEntityId = state.combatTargetEntityId;
	projectile.spellId = 8;
	projectile.levelId = state.levelId;
	projectile.positionX = target.first;
	projectile.positionY = target.second;
	projectile.targetX = target.first;
	projectile.targetY = target.second;
	projectile.damageType = 3;
	projectile.remainingTicks = static_cast<uint32_t>(std::min(spellLevel + 6, 15) << 4);
	projectile.behavior = 13;
	projectile.startX = target.first;
	projectile.startY = target.second;
	projectile.impactLifetime = 11;
	projectile.effectiveSpellLevel = spellLevel;
	state.projectiles.push_back(projectile);
}

bool IsReplayResurrectionPositionAvailable(
    const ReplayFixtureExecutionState &state,
    int32_t positionX,
    int32_t positionY)
{
	if (positionX < 0 || positionX >= 40 || positionY < 0 || positionY >= 40)
		return false;
	if (state.life > 0
	    && state.levelId == state.secondaryPlayerLevelId
	    && state.positionX == positionX
	    && state.positionY == positionY) {
		return false;
	}
	if (state.combatTargetEntityId != 0
	    && state.combatTargetHitPointsQ6 > 0
	    && ((state.combatTargetPositionX == positionX
	            && state.combatTargetPositionY == positionY)
	        || (state.combatTargetMoving
	            && state.combatTargetFuturePositionX == positionX
	            && state.combatTargetFuturePositionY == positionY))) {
		return false;
	}
	return state.objectEntityId == 0
	    || state.objectActivated
	    || state.objectDestroyed
	    || state.objectLevelId != state.secondaryPlayerLevelId
	    || state.objectPositionX != positionX
	    || state.objectPositionY != positionY;
}

bool IsReplayPortalPosition(
    const ReplayFixtureExecutionState &state,
    int32_t positionX,
    int32_t positionY)
{
	return state.portalId != 0
	    && state.portalDestinationLevelId == state.secondaryPlayerLevelId
	    && ((state.portalDestinationX == positionX
	            && state.portalDestinationY == positionY)
	        || (state.portalDestinationX + 1 == positionX
	            && state.portalDestinationY + 1 == positionY));
}

std::pair<int32_t, int32_t> FindReplayResurrectionPosition(
    const ReplayFixtureExecutionState &state)
{
	constexpr std::pair<int32_t, int32_t> InitialOffsets[] = {
		{ 0, 0 },
		{ 1, 0 },
		{ 0, 1 },
		{ 1, 1 },
		{ 2, 0 },
		{ 0, 2 },
		{ 1, 2 },
		{ 2, 1 },
	};
	for (const auto &offset : InitialOffsets) {
		const int32_t candidateX = state.secondaryPlayerPositionX + offset.first;
		const int32_t candidateY = state.secondaryPlayerPositionY + offset.second;
		if (IsReplayResurrectionPositionAvailable(state, candidateX, candidateY))
			return { candidateX, candidateY };
	}

	const auto destination = Crawl(1, 50, [&](Displacement displacement) -> std::optional<std::pair<int32_t, int32_t>> {
		const int32_t candidateX = state.secondaryPlayerPositionX + displacement.deltaX;
		const int32_t candidateY = state.secondaryPlayerPositionY + displacement.deltaY;
		if (!IsReplayResurrectionPositionAvailable(state, candidateX, candidateY)
		    || IsReplayPortalPosition(state, candidateX, candidateY)) {
			return {};
		}
		return std::pair<int32_t, int32_t> { candidateX, candidateY };
	});
	return destination.value_or(std::pair<int32_t, int32_t> { 0, 0 });
}

void CreateReplayResurrectionBeam(
    ReplayFixtureExecutionState &state,
    int32_t positionX,
    int32_t positionY)
{
	uint32_t nextEntityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);
	ReplayFixtureProjectileState projectile;
	projectile.entityId = nextEntityId;
	projectile.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
	projectile.targetEntityId = state.secondaryPlayerEntityId;
	projectile.spellId = 32;
	projectile.levelId = state.levelId;
	projectile.positionX = positionX;
	projectile.positionY = positionY;
	projectile.targetX = positionX;
	projectile.targetY = positionY;
	projectile.damageType = 3;
	projectile.remainingTicks = 16;
	projectile.behavior = 11;
	projectile.startX = positionX;
	projectile.startY = positionY;
	state.projectiles.push_back(projectile);
}

void ApplyReplayResurrection(ReplayFixtureExecutionState &state)
{
	const int32_t beamX = state.secondaryPlayerPositionX;
	const int32_t beamY = state.secondaryPlayerPositionY;
	CreateReplayResurrectionBeam(state, beamX, beamY);
	if (state.secondaryPlayerLife > 0)
		return;
	const auto destination = FindReplayResurrectionPosition(state);
	state.secondaryPlayerPositionX = destination.first;
	state.secondaryPlayerPositionY = destination.second;
	state.secondaryPlayerLife = std::min(10 * 64, state.secondaryPlayerLifeMaximum);
	state.secondaryPlayerCombatMode = 0;
	state.secondaryPlayerCombatModeEndTick = 0;
}

bool IsReplayMovementPositionAvailable(
    const ReplayFixtureExecutionState &state,
    int32_t candidateX,
    int32_t candidateY)
{
	if (candidateX < 0 || candidateX >= 40
	    || candidateY < 0 || candidateY >= 40) {
		return false;
	}
	if (state.secondaryPlayerEntityId != 0
	    && state.secondaryPlayerLife > 0
	    && state.secondaryPlayerLevelId == state.levelId
	    && state.secondaryPlayerPositionX == candidateX
	    && state.secondaryPlayerPositionY == candidateY) {
		return false;
	}
	if (state.combatTargetEntityId != 0
	    && state.combatTargetHitPointsQ6 > 0
	    && ((state.combatTargetPositionX == candidateX
	            && state.combatTargetPositionY == candidateY)
	        || (state.combatTargetMoving
	            && state.combatTargetFuturePositionX == candidateX
	            && state.combatTargetFuturePositionY == candidateY))) {
		return false;
	}
	if (state.objectEntityId != 0
	    && !state.objectActivated
	    && !state.objectDestroyed
	    && state.objectLevelId == state.levelId
	    && state.objectPositionX == candidateX
	    && state.objectPositionY == candidateY) {
		return false;
	}
	return true;
}

std::optional<std::pair<int32_t, int32_t>> FindReplayTeleportDestination(
    const ReplayFixtureExecutionState &state,
    int32_t targetX,
    int32_t targetY)
{
	return Crawl(0, 5, [&](Displacement displacement) -> std::optional<std::pair<int32_t, int32_t>> {
		const int32_t candidateX = targetX + displacement.deltaX;
		const int32_t candidateY = targetY + displacement.deltaY;
		if (!IsReplayMovementPositionAvailable(state, candidateX, candidateY))
			return {};
		return std::pair<int32_t, int32_t> { candidateX, candidateY };
	});
}

std::optional<std::pair<int32_t, int32_t>> FindReplayPhasingDestination(
    ReplayFixtureExecutionState &state)
{
	std::vector<std::pair<int32_t, int32_t>> candidates;
	candidates.reserve(36);
	for (int32_t offsetY = -6; offsetY <= 6; ++offsetY) {
		for (int32_t offsetX = -6; offsetX <= 6; ++offsetX) {
			if (std::abs(offsetX) < 4 || std::abs(offsetY) < 4)
				continue;
			const int32_t candidateX = state.positionX + offsetX;
			const int32_t candidateY = state.positionY + offsetY;
			if (IsReplayMovementPositionAvailable(state, candidateX, candidateY))
				candidates.emplace_back(candidateX, candidateY);
		}
	}
	if (candidates.empty())
		return {};
	return candidates[std::max(GenerateRnd(state, static_cast<int32_t>(candidates.size())), 0)];
}

void CreateReplayMovementController(
    ReplayFixtureExecutionState &state,
    const std::pair<int32_t, int32_t> &destination,
    uint32_t spellId,
    int32_t behavior)
{
	uint32_t nextEntityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);
	ReplayFixtureProjectileState projectile;
	projectile.entityId = nextEntityId;
	projectile.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
	projectile.spellId = spellId;
	projectile.levelId = state.levelId;
	projectile.positionX = destination.first;
	projectile.positionY = destination.second;
	projectile.targetX = destination.first;
	projectile.targetY = destination.second;
	projectile.damageType = 3;
	projectile.remainingTicks = 2;
	projectile.behavior = behavior;
	projectile.startX = destination.first;
	projectile.startY = destination.second;
	state.projectiles.push_back(projectile);
}

int32_t GenerateReplayHealing(
    ReplayFixtureExecutionState &state,
    int32_t spellLevel,
    bool healOther)
{
	int32_t healing = GenerateRnd(state, 10) + 1;
	for (uint32_t level = 0; level < state.characterLevel; ++level)
		healing += GenerateRnd(state, 4) + 1;
	for (int32_t level = 0; level < spellLevel; ++level)
		healing += GenerateRnd(state, 6) + 1;
	healing *= 64;
	if (healOther && state.characterClassId == 3) {
		healing *= 3;
	} else if (state.characterClassId == 0
	    || state.characterClassId == 3
	    || state.characterClassId == 5) {
		healing *= 2;
	} else if (state.characterClassId == 1 || state.characterClassId == 4) {
		healing += healing / 2;
	}
	return healing;
}

int32_t ToWholeCeilingQ6(int32_t raw)
{
	const int32_t whole = raw / 64;
	return raw > 0 && raw % 64 != 0 ? whole + 1 : whole;
}

struct ReplayVisitedProjectileTile {
	int32_t x;
	int32_t y;
	int32_t traveledX;
	int32_t traveledY;
};

std::pair<int32_t, int32_t> CalculateReplayMissileTile(
    int32_t startX,
    int32_t startY,
    int32_t traveledX,
    int32_t traveledY)
{
	const int32_t pixelX = traveledX >> 16;
	const int32_t pixelY = traveledY >> 16;
	const int32_t xNumerator = 2 * pixelY + pixelX;
	const int32_t yNumerator = 2 * pixelY - pixelX;
	return {
		startX + (xNumerator + (xNumerator >= 0 ? 32 : -32)) / 64,
		startY + (yNumerator + (yNumerator >= 0 ? 32 : -32)) / 64,
	};
}

std::vector<ReplayVisitedProjectileTile> MoveReplayNativeProjectile(
    ReplayFixtureProjectileState &projectile)
{
	const int32_t previousX = projectile.positionX;
	const int32_t previousY = projectile.positionY;
	projectile.traveledX += projectile.velocityX;
	projectile.traveledY += projectile.velocityY;
	const auto finalTile = CalculateReplayMissileTile(
	    projectile.startX,
	    projectile.startY,
	    projectile.traveledX,
	    projectile.traveledY);
	projectile.positionX = finalTile.first;
	projectile.positionY = finalTile.second;

	const int32_t possibleVisitedTiles = projectile.velocityX == 0 || projectile.velocityY == 0
	    ? std::max(
	          std::abs(previousX - finalTile.first),
	          std::abs(previousY - finalTile.second))
	    : std::abs(previousX - finalTile.first)
	        + std::abs(previousY - finalTile.second);
	if (possibleVisitedTiles == 0)
		return {};

	std::vector<ReplayVisitedProjectileTile> result;
	if (possibleVisitedTiles > 1) {
		const int64_t speedX = std::abs(static_cast<int64_t>(projectile.velocityX));
		const int64_t speedY = std::abs(static_cast<int64_t>(projectile.velocityY));
		const float denominator = static_cast<float>(2 * speedY >= speedX ? 2 * speedY : speedX);
		const int32_t velocityFraction = static_cast<int32_t>((32 << 16) / denominator);
		const int32_t incrementX = projectile.velocityX * velocityFraction;
		const int32_t incrementY = projectile.velocityY * velocityFraction;
		if (incrementX != 0 || incrementY != 0) {
			int32_t traveledX = projectile.traveledX - projectile.velocityX;
			int32_t traveledY = projectile.traveledY - projectile.velocityY;
			if (incrementX != 0)
				traveledX = traveledX / incrementX * incrementX;
			if (incrementY != 0)
				traveledY = traveledY / incrementY * incrementY;
			while (true) {
				const int32_t initialDifferenceX = projectile.traveledX - traveledX;
				const int32_t initialDifferenceY = projectile.traveledY - traveledY;
				traveledX += incrementX;
				traveledY += incrementY;
				const int32_t incrementedDifferenceX = projectile.traveledX - traveledX;
				const int32_t incrementedDifferenceY = projectile.traveledY - traveledY;
				if ((initialDifferenceX < 0) != (incrementedDifferenceX < 0)
				    || (initialDifferenceY < 0) != (incrementedDifferenceY < 0)) {
					break;
				}
				const auto tile = CalculateReplayMissileTile(
				    projectile.startX,
				    projectile.startY,
				    traveledX,
				    traveledY);
				if (tile == finalTile)
					break;
				const int32_t pixelX = traveledX >> 16;
				const int32_t pixelY = traveledY >> 16;
				if (pixelY % 16 == 0
				    && pixelX % 32 == 0
				    && std::abs(pixelY / 16) % 2 != std::abs(pixelX / 32) % 2) {
					continue;
				}
				if ((previousX == tile.first && previousY == tile.second)
				    || (!result.empty()
				        && result.back().x == tile.first
				        && result.back().y == tile.second)) {
					continue;
				}
				result.push_back({ tile.first, tile.second, traveledX, traveledY });
			}
		}
	}
	if (result.empty()
	    || result.back().x != finalTile.first
	    || result.back().y != finalTile.second) {
		result.push_back({
			finalTile.first,
			finalTile.second,
			projectile.traveledX,
			projectile.traveledY,
		});
	}
	return result;
}

void EnterReplayProjectileImpact(ReplayFixtureProjectileState &projectile)
{
	projectile.phase = 1;
	projectile.remainingTicks = static_cast<uint32_t>(projectile.impactLifetime);
	projectile.velocityX = 0;
	projectile.velocityY = 0;
}

int32_t GetReplayBaseMagicToHit(uint32_t characterClassId)
{
	if (characterClassId == 2)
		return 70;
	if (characterClassId == 4)
		return 60;
	return 50;
}

void AdvanceMagicBoltsOneTick(ReplayFixtureExecutionState &state, uint64_t tick)
{
	for (auto iterator = state.projectiles.begin(); iterator != state.projectiles.end();) {
		auto &projectile = *iterator;
		if (projectile.behavior == FireboltBehavior
		    && projectile.sourceEntityId == state.combatTargetEntityId) {
			++iterator;
			continue;
		}
		const bool holyBolt = projectile.behavior == 19;
		if (projectile.behavior != 2 && projectile.behavior != 19) {
			++iterator;
			continue;
		}
		if (--projectile.remainingTicks == 0) {
			if (projectile.phase == 1) {
				iterator = state.projectiles.erase(iterator);
			} else {
				EnterReplayProjectileImpact(projectile);
				++iterator;
			}
			continue;
		}
		if (projectile.phase == 1) {
			++iterator;
			continue;
		}

		int32_t previousX = projectile.positionX;
		int32_t previousY = projectile.positionY;
		int32_t previousTraveledX = projectile.traveledX;
		int32_t previousTraveledY = projectile.traveledY;
		const auto visitedTiles = MoveReplayNativeProjectile(projectile);
		const auto resolveCollision = [&](int32_t x, int32_t y) {
			const bool targetAtTile = state.combatTargetEntityId != 0
			    && state.combatTargetHitPointsQ6 > 0
			    && ((state.combatTargetPositionX == x
			            && state.combatTargetPositionY == y)
			        || (state.combatTargetMoving
			            && state.combatTargetPetrified
			            && state.combatTargetFuturePositionX == x
			            && state.combatTargetFuturePositionY == y));
			bool hitTarget = false;
			if (targetAtTile
			    && (!holyBolt || state.combatTargetUndead)
			    && (holyBolt || state.combatTargetFireResistance < 100)) {
				const int32_t hitChance = std::clamp(
				    state.attributes[3]
				        + GetReplayBaseMagicToHit(state.characterClassId)
				        - state.combatTargetMonsterLevel * 2,
				    5,
				    95);
				const int32_t hitRoll = std::max(GenerateRnd(state, 100), 0);
				if (state.combatTargetPetrified || hitRoll < hitChance) {
					int32_t damageQ6 = projectile.damage * 64;
					if (!holyBolt)
					static_cast<void>(GenerateRnd(state, 1));
					if (!holyBolt && state.combatTargetFireResistance > 0)
						damageQ6 /= projectile.resistantDamageDivisor;
					state.combatTargetHitPointsQ6 = std::max(
					    0,
					    state.combatTargetHitPointsQ6 - damageQ6);
					state.combatTargetHitPoints = ToWholeCeilingQ6(
					    state.combatTargetHitPointsQ6);
					if (state.combatTargetHitPointsQ6 == 0)
						DefeatReplayCombatTarget(state);
					hitTarget = true;
				}
			}
			const bool playerAtTile = state.secondaryPlayerEntityId != 0
			    && state.secondaryPlayerLife > 0
			    && state.multiplayer
			    && state.secondaryPlayerLevelId == projectile.levelId
			    && state.secondaryPlayerPositionX == x
			    && state.secondaryPlayerPositionY == y;
			if (!holyBolt && playerAtTile) {
				const int32_t hitChance = std::clamp(
				    state.attributes[3]
				        + GetReplayBaseMagicToHit(state.characterClassId)
				        - static_cast<int32_t>(state.secondaryPlayerCharacterLevel) * 2,
				    5,
				    95);
				const int32_t hitRoll = std::max(GenerateRnd(state, 100), 0);
				if (hitRoll < hitChance) {
					bool blocked = false;
					const int32_t damageQ6 = projectile.damage * 32;
					static_cast<void>(GenerateRnd(state, 1));
					const bool canBlock = state.secondaryPlayerBlockCapable
					    && (state.secondaryPlayerCombatMode == 0
					        || state.secondaryPlayerCombatMode == 4);
					const int32_t blockRoll = canBlock
					    ? std::max(GenerateRnd(state, 100), 0)
					    : 100;
					const int32_t blockChance = std::clamp(
					    state.secondaryPlayerDexterity
					        + static_cast<int32_t>(state.secondaryPlayerCharacterLevel) * 2
					        - static_cast<int32_t>(state.characterLevel) * 2,
					    0,
					    100);
					blocked = blockRoll < blockChance;
					if (blocked) {
						state.secondaryPlayerCombatMode = 6;
						state.secondaryPlayerCombatModeEndTick = tick + 4;
						if (state.hellfire)
							static_cast<void>(GenerateRnd(state, 2));
					} else {
						state.secondaryPlayerLife = std::max(
						    0,
						    state.secondaryPlayerLife - damageQ6);
						if (state.secondaryPlayerLife == 0) {
							StartReplaySecondaryPlayerDeath(state);
						} else if ((damageQ6 >> 6)
						    >= static_cast<int32_t>(state.secondaryPlayerCharacterLevel)) {
							state.secondaryPlayerCombatMode = 7;
							state.secondaryPlayerCombatModeEndTick = tick + 6;
						}
					}
					hitTarget = true;
				}
			}
			const bool blockingObject = state.objectEntityId != 0
			    && !state.objectActivated
			    && !state.objectDestroyed
			    && !state.objectMissilePassable
			    && state.objectLevelId == projectile.levelId
			    && state.objectPositionX == x
			    && state.objectPositionY == y;
			if (blockingObject && state.objectBreakable)
				state.objectDestroyed = true;
			const bool outsideWorld = x < 0 || x >= 40 || y < 0 || y >= 40;
			return std::pair<bool, uint32_t> {
				hitTarget || blockingObject || outsideWorld,
				 targetAtTile
				     ? state.combatTargetEntityId
				     : playerAtTile ? state.secondaryPlayerEntityId : 0,
			};
		};

		if (visitedTiles.empty()) {
			const auto collision = resolveCollision(projectile.positionX, projectile.positionY);
			if (projectile.lastCollisionTargetEntityId != collision.second && collision.first) {
				EnterReplayProjectileImpact(projectile);
				++iterator;
				continue;
			}
			projectile.lastCollisionTargetEntityId = collision.second;
			++iterator;
			continue;
		}

		for (const auto &tile : visitedTiles) {
			projectile.positionX = tile.x;
			projectile.positionY = tile.y;
			projectile.traveledX = tile.traveledX;
			projectile.traveledY = tile.traveledY;
			const auto collision = resolveCollision(tile.x, tile.y);
			if (collision.first) {
				projectile.positionX = previousX;
				projectile.positionY = previousY;
				projectile.traveledX = previousTraveledX;
				projectile.traveledY = previousTraveledY;
				EnterReplayProjectileImpact(projectile);
				break;
			}
			projectile.lastCollisionTargetEntityId = collision.second;
			previousX = tile.x;
			previousY = tile.y;
			previousTraveledX = tile.traveledX;
			previousTraveledY = tile.traveledY;
		}
		++iterator;
	}
}

void AdvanceFireballsOneTick(ReplayFixtureExecutionState &state, uint64_t tick)
{
	constexpr std::pair<int32_t, int32_t> ExplosionOffsets[] = {
		{ 0, 0 },
		{ 0, 1 },
		{ 0, -1 },
		{ 1, 0 },
		{ 1, -1 },
		{ 1, 1 },
		{ -1, 0 },
		{ -1, 1 },
		{ -1, -1 },
	};
	for (auto iterator = state.projectiles.begin(); iterator != state.projectiles.end();) {
		auto &projectile = *iterator;
		if (projectile.behavior != 16) {
			++iterator;
			continue;
		}
		const bool torchantMonsterProjectile = projectile.sourceEntityId == state.combatTargetEntityId
		    && state.combatTargetNativeAi == "Torchant";
		if (projectile.phase == 1) {
			if (--projectile.remainingTicks == 0)
				iterator = state.projectiles.erase(iterator);
			else
				++iterator;
			continue;
		}

		--projectile.remainingTicks;
		const bool expiresAfterMovement = projectile.remainingTicks == 0;
		const auto hitTargetAt = [&](int32_t x, int32_t y, bool shifted) {
			const bool targetAtTile = state.combatTargetEntityId != 0
			    && state.combatTargetEntityId != projectile.sourceEntityId
			    && state.combatTargetHitPointsQ6 > 0
			    && ((state.combatTargetPositionX == x
			            && state.combatTargetPositionY == y)
			        || (state.combatTargetMoving
			            && state.combatTargetPetrified
			            && state.combatTargetFuturePositionX == x
			            && state.combatTargetFuturePositionY == y));
			const bool playerAtTile = state.secondaryPlayerEntityId != 0
			    && state.secondaryPlayerLife > 0
			    && state.secondaryPlayerLevelId == projectile.levelId
			    && state.secondaryPlayerPositionX == x
			    && state.secondaryPlayerPositionY == y;
			bool hitTarget = false;
			if (targetAtTile && state.combatTargetFireResistance < 100) {
				const int32_t hitChance = std::clamp(
					state.attributes[3]
					    + GetReplayBaseMagicToHit(state.characterClassId)
					    - state.combatTargetMonsterLevel * 2,
					5,
					95);
				const int32_t hitRoll = std::max(GenerateRnd(state, 100), 0);
				if (state.combatTargetPetrified || hitRoll < hitChance) {
					int32_t damageQ6 = shifted
					    ? projectile.damage
					    : projectile.damage * 64;
					static_cast<void>(GenerateRnd(state, 1));
					if (state.combatTargetFireResistance > 0)
						damageQ6 /= projectile.resistantDamageDivisor;
					state.combatTargetHitPointsQ6 = std::max(
						0,
						state.combatTargetHitPointsQ6 - damageQ6);
					state.combatTargetHitPoints = ToWholeCeilingQ6(
						state.combatTargetHitPointsQ6);
					if (state.combatTargetHitPointsQ6 == 0)
						DefeatReplayCombatTarget(state);
					hitTarget = true;
				}
			}
			if (playerAtTile
			    && state.multiplayer
			    && (!shifted || state.secondaryPlayerLife > 0)) {
				int32_t hitChance;
				if (torchantMonsterProjectile) {
					const int32_t distance = std::max(
						std::abs(state.secondaryPlayerPositionX - state.combatTargetPositionX),
						std::abs(state.secondaryPlayerPositionY - state.combatTargetPositionY));
					hitChance = std::clamp(
						40
						    + state.combatTargetMonsterLevel * 2
						    - static_cast<int32_t>(state.secondaryPlayerCharacterLevel) * 2
						    - distance * 2,
						10,
						100);
				} else {
					hitChance = std::clamp(
						state.attributes[3]
						    + GetReplayBaseMagicToHit(state.characterClassId)
						    - static_cast<int32_t>(state.secondaryPlayerCharacterLevel) * 2,
						5,
						95);
				}
				const int32_t hitRoll = std::max(GenerateRnd(state, 100), 0);
				if (hitRoll < hitChance) {
					bool blocked = false;
					const int32_t damageQ6 = torchantMonsterProjectile
					    ? projectile.damage * 64
					    : shifted ? projectile.damage / 2 : projectile.damage * 32;
					if (!torchantMonsterProjectile)
						static_cast<void>(GenerateRnd(state, 1));
					if (!shifted) {
						const bool canBlock = state.secondaryPlayerBlockCapable
						    && (state.secondaryPlayerCombatMode == 0
						        || state.secondaryPlayerCombatMode == 4);
						const int32_t blockRoll = canBlock
						    ? std::max(GenerateRnd(state, 100), 0)
						    : 100;
						const int32_t blockChance = std::clamp(
							state.secondaryPlayerDexterity
							    + static_cast<int32_t>(state.secondaryPlayerCharacterLevel) * 2
							    - static_cast<int32_t>(state.characterLevel) * 2,
							0,
							100);
						blocked = blockRoll < blockChance;
						if (blocked) {
							state.secondaryPlayerCombatMode = 6;
							state.secondaryPlayerCombatModeEndTick = tick + 4;
							if (state.hellfire)
								static_cast<void>(GenerateRnd(state, 2));
						}
					}
					if (!blocked) {
						state.secondaryPlayerLife = std::max(
							0,
							state.secondaryPlayerLife - damageQ6);
						if (state.secondaryPlayerLife == 0) {
							StartReplaySecondaryPlayerDeath(state);
						} else if ((damageQ6 >> 6)
						    >= static_cast<int32_t>(state.secondaryPlayerCharacterLevel)) {
							state.secondaryPlayerCombatMode = 7;
							state.secondaryPlayerCombatModeEndTick = tick + 6;
						}
					}
					hitTarget = true;
				}
			}
			return hitTarget;
		};
		const auto resolveCollision = [&](int32_t x, int32_t y) {
			const bool targetAtTile = state.combatTargetEntityId != 0
			    && state.combatTargetHitPointsQ6 > 0
			    && ((state.combatTargetPositionX == x
			            && state.combatTargetPositionY == y)
			        || (state.combatTargetMoving
			            && state.combatTargetPetrified
			            && state.combatTargetFuturePositionX == x
			            && state.combatTargetFuturePositionY == y));
			const bool playerAtTile = state.secondaryPlayerEntityId != 0
			    && state.secondaryPlayerLife > 0
			    && state.secondaryPlayerLevelId == projectile.levelId
			    && state.secondaryPlayerPositionX == x
			    && state.secondaryPlayerPositionY == y;
			const bool hitTarget = hitTargetAt(x, y, false);
			const bool blockingObject = state.objectEntityId != 0
			    && !state.objectActivated
			    && !state.objectDestroyed
			    && !state.objectMissilePassable
			    && state.objectLevelId == projectile.levelId
			    && state.objectPositionX == x
			    && state.objectPositionY == y;
			if (blockingObject && state.objectBreakable)
				state.objectDestroyed = true;
			const bool outsideWorld = x < 0 || x >= 40 || y < 0 || y >= 40;
			return std::pair<bool, uint32_t> {
				hitTarget || blockingObject || outsideWorld,
				targetAtTile
			    ? state.combatTargetEntityId
			    : playerAtTile ? state.secondaryPlayerEntityId : 0,
			};
		};
		const auto enterImpact = [&]() {
			EnterReplayProjectileImpact(projectile);
			for (const auto &offset : ExplosionOffsets) {
				const int32_t x = projectile.positionX + offset.first;
				const int32_t y = projectile.positionY + offset.second;
				if (x < 0 || x >= 40 || y < 0 || y >= 40)
					continue;
				static_cast<void>(hitTargetAt(x, y, true));
				if (state.objectEntityId != 0
				    && !state.objectDestroyed
				    && !state.objectMissilePassable
				    && state.objectPositionX == x
				    && state.objectPositionY == y
				    && state.objectBreakable) {
					state.objectDestroyed = true;
				}
			}
		};

		bool impacted = false;
		const auto visitedTiles = MoveReplayNativeProjectile(projectile);
		if (visitedTiles.empty()) {
			const bool targetAtTile = state.combatTargetEntityId != 0
			    && state.combatTargetHitPointsQ6 > 0
			    && state.combatTargetPositionX == projectile.positionX
			    && state.combatTargetPositionY == projectile.positionY;
			const bool playerAtTile = state.secondaryPlayerEntityId != 0
			    && state.secondaryPlayerLife > 0
			    && state.secondaryPlayerLevelId == projectile.levelId
			    && state.secondaryPlayerPositionX == projectile.positionX
			    && state.secondaryPlayerPositionY == projectile.positionY;
			const uint32_t collisionTargetEntityId = targetAtTile
			    ? state.combatTargetEntityId
			    : playerAtTile ? state.secondaryPlayerEntityId : 0;
			const auto collision = projectile.lastCollisionTargetEntityId == collisionTargetEntityId
			    ? std::pair<bool, uint32_t> { false, collisionTargetEntityId }
			    : resolveCollision(projectile.positionX, projectile.positionY);
			if (projectile.lastCollisionTargetEntityId != collision.second
			    && collision.first) {
				impacted = true;
			}
			if (!impacted || !torchantMonsterProjectile)
				projectile.lastCollisionTargetEntityId = collision.second;
		} else {
			for (const auto &tile : visitedTiles) {
				projectile.positionX = tile.x;
				projectile.positionY = tile.y;
				projectile.traveledX = tile.traveledX;
				projectile.traveledY = tile.traveledY;
				const auto collision = resolveCollision(tile.x, tile.y);
				projectile.lastCollisionTargetEntityId = collision.second;
				if (collision.first) {
					impacted = true;
					break;
				}
			}
		}
		if (impacted || expiresAfterMovement)
			enterImpact();
		++iterator;
	}
}

void AdvanceBoneSpiritsOneTick(ReplayFixtureExecutionState &state, uint64_t tick)
{
	for (auto iterator = state.projectiles.begin(); iterator != state.projectiles.end();) {
		auto &projectile = *iterator;
		if (projectile.behavior != 21) {
			++iterator;
			continue;
		}
		if (projectile.phase == 1) {
			if (--projectile.remainingTicks == 0)
				iterator = state.projectiles.erase(iterator);
			else
				++iterator;
			continue;
		}

		--projectile.remainingTicks;
		const bool expiresAfterMovement = projectile.remainingTicks == 0;
		const auto hitTargetAt = [&](int32_t x, int32_t y) {
			const bool targetAtTile = state.combatTargetEntityId != 0
			    && state.combatTargetHitPointsQ6 > 0
			    && ((state.combatTargetPositionX == x
			            && state.combatTargetPositionY == y)
			        || (state.combatTargetMoving
			            && state.combatTargetPetrified
			            && state.combatTargetFuturePositionX == x
			            && state.combatTargetFuturePositionY == y));
			const bool playerAtTile = state.secondaryPlayerEntityId != 0
			    && state.secondaryPlayerLife > 0
			    && state.multiplayer
			    && state.secondaryPlayerLevelId == projectile.levelId
			    && state.secondaryPlayerPositionX == x
			    && state.secondaryPlayerPositionY == y;
			bool hitTarget = false;
			if (targetAtTile && state.combatTargetMagicResistance < 100) {
				const int32_t hitChance = std::clamp(
				    state.attributes[3]
				        + GetReplayBaseMagicToHit(state.characterClassId)
				        - state.combatTargetMonsterLevel * 2,
				    5,
				    95);
				const bool hit = state.combatTargetPetrified
				    || std::max(GenerateRnd(state, 100), 0) < hitChance;
				if (hit) {
					int32_t damageQ6 = state.combatTargetHitPointsQ6 / 3 / 64 * 64;
					if (state.combatTargetMagicResistance > 0)
						damageQ6 /= projectile.resistantDamageDivisor;
					state.combatTargetHitPointsQ6 = std::max(
					    0,
					    state.combatTargetHitPointsQ6 - damageQ6);
					state.combatTargetHitPoints = ToWholeCeilingQ6(
					    state.combatTargetHitPointsQ6);
					if (state.combatTargetHitPointsQ6 == 0)
						DefeatReplayCombatTarget(state);
					hitTarget = true;
				}
			}
			if (playerAtTile) {
				const int32_t hitChance = std::clamp(
				    state.attributes[3]
				        + GetReplayBaseMagicToHit(state.characterClassId)
				        - static_cast<int32_t>(state.secondaryPlayerCharacterLevel) * 2,
				    5,
				    95);
				const int32_t hitRoll = std::max(GenerateRnd(state, 100), 0);
				if (hitRoll < hitChance) {
					const bool canBlock = state.secondaryPlayerBlockCapable
					    && (state.secondaryPlayerCombatMode == 0
					        || state.secondaryPlayerCombatMode == 4);
					const int32_t blockRoll = canBlock
					    ? std::max(GenerateRnd(state, 100), 0)
					    : 100;
					const int32_t blockChance = std::clamp(
					    state.secondaryPlayerDexterity
					        + static_cast<int32_t>(state.secondaryPlayerCharacterLevel) * 2
					        - static_cast<int32_t>(state.characterLevel) * 2,
					    0,
					    100);
					if (blockRoll < blockChance) {
						state.secondaryPlayerCombatMode = 6;
						state.secondaryPlayerCombatModeEndTick = tick + 4;
						if (state.hellfire)
							static_cast<void>(GenerateRnd(state, 2));
					} else {
						const int32_t damageQ6 = state.secondaryPlayerLife / 3 / 2;
						state.secondaryPlayerLife = std::max(
						    0,
						    state.secondaryPlayerLife - damageQ6);
						if (state.secondaryPlayerLife == 0)
							StartReplaySecondaryPlayerDeath(state);
						else if ((damageQ6 >> 6)
						    >= static_cast<int32_t>(state.secondaryPlayerCharacterLevel)) {
							state.secondaryPlayerCombatMode = 7;
							state.secondaryPlayerCombatModeEndTick = tick + 6;
						}
					}
					hitTarget = true;
				}
			}
			return hitTarget;
		};
		const auto resolveCollision = [&](int32_t x, int32_t y) {
			const bool targetAtTile = state.combatTargetEntityId != 0
			    && state.combatTargetHitPointsQ6 > 0
			    && ((state.combatTargetPositionX == x
			            && state.combatTargetPositionY == y)
			        || (state.combatTargetMoving
			            && state.combatTargetPetrified
			            && state.combatTargetFuturePositionX == x
			            && state.combatTargetFuturePositionY == y));
			const bool playerAtTile = state.secondaryPlayerEntityId != 0
			    && state.secondaryPlayerLife > 0
			    && state.multiplayer
			    && state.secondaryPlayerLevelId == projectile.levelId
			    && state.secondaryPlayerPositionX == x
			    && state.secondaryPlayerPositionY == y;
			const bool hitTarget = hitTargetAt(x, y);
			const bool blockingObject = state.objectEntityId != 0
			    && !state.objectActivated
			    && !state.objectDestroyed
			    && !state.objectMissilePassable
			    && state.objectLevelId == projectile.levelId
			    && state.objectPositionX == x
			    && state.objectPositionY == y;
			if (blockingObject && state.objectBreakable)
				state.objectDestroyed = true;
			const bool outsideWorld = x < 0 || x >= 40 || y < 0 || y >= 40;
			return std::pair<bool, uint32_t> {
				hitTarget || blockingObject || outsideWorld,
				targetAtTile
				    ? state.combatTargetEntityId
				    : playerAtTile ? state.secondaryPlayerEntityId : 0,
			};
		};
		const auto retarget = [&]() {
			projectile.pathIndex = 1;
			int32_t targetX = projectile.positionX;
			int32_t targetY = projectile.positionY;
			if (state.combatTargetEntityId != 0
			    && state.combatTargetHitPointsQ6 > 0
			    && std::max(
			           std::abs(state.combatTargetPositionX - projectile.positionX),
			           std::abs(state.combatTargetPositionY - projectile.positionY))
			        <= 19) {
				targetX = state.combatTargetPositionX;
				targetY = state.combatTargetPositionY;
			} else {
				targetX += DirectionDisplacements[state.facingDirection % 8].first;
				targetY += DirectionDisplacements[state.facingDirection % 8].second;
			}
			const int32_t direction = GetDirection(
			    projectile.positionX,
			    projectile.positionY,
			    targetX,
			    targetY);
			if (targetX == projectile.positionX && targetY == projectile.positionY) {
				targetX += DirectionDisplacements[direction].first;
				targetY += DirectionDisplacements[direction].second;
			}
			const auto velocity = CalculateVelocity(
			    projectile.positionX,
			    projectile.positionY,
			    targetX,
			    targetY,
			    projectile.speed);
			projectile.velocityX = velocity.first;
			projectile.velocityY = velocity.second;
		};
		bool impacted = false;
		const auto visitedTiles = MoveReplayNativeProjectile(projectile);
		if (visitedTiles.empty()) {
			const auto collision = resolveCollision(
			    projectile.positionX,
			    projectile.positionY);
			if (projectile.lastCollisionTargetEntityId != collision.second
			    && collision.first) {
				if (projectile.pathIndex == 0
				    && projectile.positionX == projectile.targetX
				    && projectile.positionY == projectile.targetY) {
					retarget();
				} else {
					impacted = true;
				}
			}
			projectile.lastCollisionTargetEntityId = collision.second;
		} else {
			for (const auto &tile : visitedTiles) {
				projectile.positionX = tile.x;
				projectile.positionY = tile.y;
				projectile.traveledX = tile.traveledX;
				projectile.traveledY = tile.traveledY;
				const auto collision = resolveCollision(tile.x, tile.y);
				projectile.lastCollisionTargetEntityId = collision.second;
				if (collision.first) {
					if (projectile.pathIndex == 0
					    && tile.x == projectile.targetX
					    && tile.y == projectile.targetY) {
						retarget();
					} else {
						impacted = true;
					}
					break;
				}
			}
		}
		if (!impacted
		    && projectile.pathIndex == 0
		    && projectile.positionX == projectile.targetX
		    && projectile.positionY == projectile.targetY) {
			retarget();
		}
		if (impacted || expiresAfterMovement)
			EnterReplayProjectileImpact(projectile);
		++iterator;
	}
}

void AdvanceMovementControllersOneTick(ReplayFixtureExecutionState &state)
{
	for (auto iterator = state.projectiles.begin(); iterator != state.projectiles.end();) {
		auto &projectile = *iterator;
		if (projectile.behavior != 9 && projectile.behavior != 15) {
			++iterator;
			continue;
		}
		if (--projectile.remainingTicks == 0) {
			iterator = state.projectiles.erase(iterator);
			continue;
		}
		const auto destination = FindReplayTeleportDestination(
		    state,
		    projectile.positionX,
		    projectile.positionY);
		if (destination) {
			state.positionX = destination->first;
			state.positionY = destination->second;
		}
		++iterator;
	}
}

void AdvanceResurrectionBeamsOneTick(ReplayFixtureExecutionState &state)
{
	for (auto iterator = state.projectiles.begin(); iterator != state.projectiles.end();) {
		if (iterator->behavior != 11) {
			++iterator;
			continue;
		}
		if (--iterator->remainingTicks == 0) {
			iterator = state.projectiles.erase(iterator);
			continue;
		}
		++iterator;
	}
}

void AdvanceStoneCursesOneTick(ReplayFixtureExecutionState &state)
{
	for (auto iterator = state.projectiles.begin(); iterator != state.projectiles.end();) {
		auto &projectile = *iterator;
		if (projectile.behavior != 13) {
			++iterator;
			continue;
		}
		--projectile.remainingTicks;
		if (projectile.targetEntityId != state.combatTargetEntityId
		    || !state.combatTargetPetrified) {
			iterator = state.projectiles.erase(iterator);
			continue;
		}
		if (state.combatTargetHitPointsQ6 == 0 && projectile.phase != 1) {
			projectile.phase = 1;
			projectile.remainingTicks = static_cast<uint32_t>(projectile.impactLifetime);
		}
		if (projectile.remainingTicks != 0) {
			++iterator;
			continue;
		}
		if (state.combatTargetHitPointsQ6 > 0)
			state.combatTargetPetrified = false;
		iterator = state.projectiles.erase(iterator);
	}
}

void AdvanceLightningOneTick(ReplayFixtureExecutionState &state, uint64_t tick)
{
	for (size_t index = 0; index < state.projectiles.size();) {
		if (state.projectiles[index].behavior == LightningBehavior
		    || state.projectiles[index].behavior == ChainLightningBoltBehavior) {
			auto &controller = state.projectiles[index];
			const bool monsterOwned = controller.targetEntityId != 0
			    && controller.sourceEntityId == state.combatTargetEntityId
			    && state.combatTargetEntityId != 0;
			--controller.remainingTicks;
			int32_t segmentDamage = 0;
			int32_t damageQ6 = 0;
			if (monsterOwned) {
				segmentDamage = 2 * (state.combatTargetMinDamage
				    + GenerateRnd(state, std::max(
				          state.combatTargetMaxDamage - state.combatTargetMinDamage + 1,
				          1)));
			} else {
				const int32_t characterLevelDamage = GenerateRnd(
				    state,
				    std::max<int32_t>(state.characterLevel, 1));
				const int32_t randomDamage = GenerateRnd(state, 2);
				damageQ6 = (randomDamage + characterLevelDamage + 2) * 64;
			}
			--controller.remainingTicks;

			const int32_t previousX = controller.positionX;
			const int32_t previousY = controller.positionY;
			const auto visitedTiles = MoveReplayNativeProjectile(controller);
			bool blocked = false;
			for (const auto &tile : visitedTiles) {
				const bool blockingObject = state.objectEntityId != 0
				    && !state.objectActivated
				    && !state.objectDestroyed
				    && !state.objectMissilePassable
				    && state.objectLevelId == controller.levelId
				    && state.objectPositionX == tile.x
				    && state.objectPositionY == tile.y;
				const bool outsideWorld = tile.x < 0 || tile.x >= 40 || tile.y < 0 || tile.y >= 40;
				if (!blockingObject && !outsideWorld)
					continue;
				controller.positionX = tile.x;
				controller.positionY = tile.y;
				controller.traveledX = tile.traveledX;
				controller.traveledY = tile.traveledY;
				blocked = true;
				break;
			}
			if (blocked) {
				state.projectiles.erase(state.projectiles.begin() + static_cast<ptrdiff_t>(index));
				continue;
			}

			std::optional<ReplayFixtureProjectileState> segment;
			if (controller.positionX != previousX || controller.positionY != previousY) {
				uint32_t nextEntityId = 0x80000000U;
				for (const auto &projectile : state.projectiles)
					nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);
				ReplayFixtureProjectileState created;
				created.entityId = nextEntityId;
				created.sourceEntityId = controller.sourceEntityId;
				created.spellId = controller.spellId;
				created.levelId = controller.levelId;
				created.positionX = controller.positionX;
				created.positionY = controller.positionY;
				created.targetX = controller.startX;
				created.targetY = controller.startY;
				created.targetEntityId = monsterOwned ? controller.targetEntityId : 0;
				created.damage = monsterOwned ? segmentDamage : damageQ6;
				created.damageType = 2;
				created.remainingTicks = monsterOwned
				    ? 10U
				    : static_cast<uint32_t>(6 + controller.effectiveSpellLevel / 2);
				created.behavior = LightningSegmentBehavior;
				created.startX = controller.startX;
				created.startY = controller.startY;
				created.resistantDamageDivisor = controller.resistantDamageDivisor;
				created.effectiveSpellLevel = controller.effectiveSpellLevel;
				if (!monsterOwned)
					static_cast<void>(GenerateRnd(state, 8));
				segment = created;
			}

			const bool expired = controller.remainingTicks == 0;
			if (expired) {
				state.projectiles.erase(state.projectiles.begin() + static_cast<ptrdiff_t>(index));
			} else {
				++index;
			}
			if (segment.has_value())
				state.projectiles.push_back(*segment);
			continue;
		}

		if (state.projectiles[index].behavior != LightningSegmentBehavior) {
			++index;
			continue;
		}

		auto &segment = state.projectiles[index];
		--segment.remainingTicks;
		if (segment.positionX != segment.startX || segment.positionY != segment.startY) {
			const bool targetAtTile = state.combatTargetEntityId != 0
			    && state.combatTargetHitPointsQ6 > 0
			    && ((state.combatTargetPositionX == segment.positionX
			            && state.combatTargetPositionY == segment.positionY)
			        || (state.combatTargetMoving
			            && state.combatTargetPetrified
			            && state.combatTargetFuturePositionX == segment.positionX
			            && state.combatTargetFuturePositionY == segment.positionY));
			const bool monsterOwned = segment.targetEntityId != 0
			    && segment.sourceEntityId == state.combatTargetEntityId
			    && state.combatTargetEntityId != 0;
			if (targetAtTile && state.combatTargetLightningResistance < 100) {
				const int32_t hitChance = monsterOwned
				    ? std::clamp(
				          std::max(10, 40 + 2 * state.combatTargetMonsterLevel
				              - 2 * static_cast<int32_t>(state.characterLevel)
				              - 2 * std::max(
				                    std::abs(segment.positionX - state.combatTargetPositionX),
				                    std::abs(segment.positionY - state.combatTargetPositionY))),
				          0,
				          100)
				    : std::clamp(
				          state.attributes[3]
				              + GetReplayBaseMagicToHit(state.characterClassId)
				              - state.combatTargetMonsterLevel * 2,
				          5,
				          95);
				const int32_t hitRoll = std::max(GenerateRnd(state, 100), 0);
				if (state.combatTargetPetrified || hitRoll < hitChance) {
					int32_t damage = monsterOwned ? segment.damage * 64 : segment.damage;
					static_cast<void>(GenerateRnd(state, 1));
					if (state.combatTargetLightningResistance > 0)
						damage /= segment.resistantDamageDivisor;
					state.combatTargetHitPointsQ6 = std::max(
					    0,
					    state.combatTargetHitPointsQ6 - damage);
					state.combatTargetHitPoints = ToWholeCeilingQ6(
					    state.combatTargetHitPointsQ6);
					if (state.combatTargetHitPointsQ6 == 0)
						DefeatReplayCombatTarget(state);
				}
			} else {
				const bool playerAtTile = state.secondaryPlayerEntityId != 0
				    && state.secondaryPlayerLife > 0
				    && state.secondaryPlayerLevelId == segment.levelId
				    && state.secondaryPlayerPositionX == segment.positionX
				    && state.secondaryPlayerPositionY == segment.positionY;
				if (playerAtTile) {
					const int32_t hitChance = monsterOwned
					    ? std::clamp(
					          std::max(10, 40 + 2 * state.combatTargetMonsterLevel
					              - 2 * static_cast<int32_t>(state.secondaryPlayerCharacterLevel)
					              - 2 * std::max(
					                    std::abs(segment.positionX - state.combatTargetPositionX),
					                    std::abs(segment.positionY - state.combatTargetPositionY))),
					          0,
					          100)
					    : std::clamp(
					          state.attributes[3]
					              + GetReplayBaseMagicToHit(state.characterClassId)
					              - static_cast<int32_t>(state.secondaryPlayerCharacterLevel) * 2,
					          5,
					          95);
					if (std::max(GenerateRnd(state, 100), 0) < hitChance) {
						static_cast<void>(GenerateRnd(state, 1));
						const int32_t damage = monsterOwned
						    ? segment.damage * 64
						    : segment.damage / 2;
						state.secondaryPlayerLife = std::max(
						    0,
						    state.secondaryPlayerLife - damage);
						if (state.secondaryPlayerLife == 0) {
							StartReplaySecondaryPlayerDeath(state);
						} else if ((damage >> 6) >= static_cast<int32_t>(state.secondaryPlayerCharacterLevel)) {
							state.secondaryPlayerCombatMode = 7;
							state.secondaryPlayerCombatModeEndTick = tick + 6;
						}
					}
				}
			}
		}

		const bool blockingObject = state.objectEntityId != 0
		    && !state.objectActivated
		    && !state.objectDestroyed
		    && !state.objectMissilePassable
		    && state.objectLevelId == segment.levelId
		    && state.objectPositionX == segment.positionX
		    && state.objectPositionY == segment.positionY;
		const bool outsideWorld = segment.positionX < 0 || segment.positionX >= 40
		    || segment.positionY < 0 || segment.positionY >= 40;
		if (blockingObject && state.objectBreakable)
			state.objectDestroyed = true;
		if (blockingObject || outsideWorld || segment.remainingTicks == 0) {
			state.projectiles.erase(state.projectiles.begin() + static_cast<ptrdiff_t>(index));
			continue;
		}
		++index;
	}
}

void AdvanceFlashOneTick(ReplayFixtureExecutionState &state)
{
	constexpr std::pair<int32_t, int32_t> BottomOffsets[] = {
		{ -1, 0 },
		{ 0, 0 },
		{ 1, 0 },
		{ -1, 1 },
		{ 0, 1 },
		{ 1, 1 },
	};
	constexpr std::pair<int32_t, int32_t> TopOffsets[] = {
		{ -1, -1 },
		{ 0, -1 },
		{ 1, -1 },
	};
	for (auto iterator = state.projectiles.begin(); iterator != state.projectiles.end();) {
		auto &projectile = *iterator;
		if (projectile.behavior != 6 && projectile.behavior != 7) {
			++iterator;
			continue;
		}

		--projectile.remainingTicks;
		const auto resolveTile = [&](const std::pair<int32_t, int32_t> &offset) {
			const int32_t positionX = projectile.positionX + offset.first;
			const int32_t positionY = projectile.positionY + offset.second;
			const bool targetAtTile = state.combatTargetEntityId != 0
			    && state.combatTargetHitPointsQ6 > 0
			    && ((state.combatTargetPositionX == positionX
			            && state.combatTargetPositionY == positionY)
			        || (state.combatTargetMoving
			            && state.combatTargetPetrified
			            && state.combatTargetFuturePositionX == positionX
			            && state.combatTargetFuturePositionY == positionY));
			if (targetAtTile && state.combatTargetMagicResistance < 100) {
				const int32_t hitChance = std::clamp(
				    state.attributes[3]
				        + GetReplayBaseMagicToHit(state.characterClassId)
				        - state.combatTargetMonsterLevel * 2,
				    5,
				    95);
				const int32_t hitRoll = std::max(GenerateRnd(state, 100), 0);
				if (state.combatTargetPetrified || hitRoll < hitChance) {
					int32_t damage = projectile.damage;
					static_cast<void>(GenerateRnd(state, 1));
					if (state.combatTargetMagicResistance > 0)
						damage /= projectile.resistantDamageDivisor;
					state.combatTargetHitPointsQ6 = std::max(
					    0,
					    state.combatTargetHitPointsQ6 - damage);
					state.combatTargetHitPoints = ToWholeCeilingQ6(
					    state.combatTargetHitPointsQ6);
					if (state.combatTargetHitPointsQ6 == 0)
						DefeatReplayCombatTarget(state);
				}
			}
			if (state.objectEntityId != 0
			    && !state.objectDestroyed
			    && !state.objectMissilePassable
			    && state.objectPositionX == positionX
			    && state.objectPositionY == positionY
			    && state.objectBreakable) {
				state.objectDestroyed = true;
			}
		};
		if (projectile.behavior == 6) {
			for (const auto &offset : BottomOffsets)
				resolveTile(offset);
		} else {
			for (const auto &offset : TopOffsets)
				resolveTile(offset);
		}

		if (projectile.remainingTicks == 0) {
			iterator = state.projectiles.erase(iterator);
			continue;
		}
		++iterator;
	}
}

void AdvanceChargedBoltsOneTick(ReplayFixtureExecutionState &state, uint64_t tick)
{
	constexpr int32_t ChargedBoltPath[] = {
		-1,
		0,
		1,
		-1,
		0,
		1,
		-1,
		-1,
		0,
		0,
		1,
		1,
		0,
		1,
		-1,
		0,
	};
	constexpr std::pair<int32_t, int32_t> DirectionDisplacements[] = {
		{ 1, 1 },
		{ 0, 1 },
		{ -1, 1 },
		{ -1, 0 },
		{ -1, -1 },
		{ 0, -1 },
		{ 1, -1 },
		{ 1, 0 },
	};

	for (auto iterator = state.projectiles.begin(); iterator != state.projectiles.end();) {
		auto &projectile = *iterator;
		if (projectile.behavior != 1) {
			++iterator;
			continue;
		}
		if (--projectile.remainingTicks == 0) {
			iterator = state.projectiles.erase(iterator);
			continue;
		}
		if (projectile.phase == 1) {
			++iterator;
			continue;
		}

		if (projectile.pathTicks == 0) {
			int32_t direction = projectile.originalDirection;
			if (ChargedBoltPath[projectile.pathIndex] == -1)
				direction = (direction + 7) % 8;
			else if (ChargedBoltPath[projectile.pathIndex] == 1)
				direction = (direction + 1) % 8;
			projectile.pathIndex = (projectile.pathIndex + 1) & 0xF;
			const auto displacement = DirectionDisplacements[direction];
			const auto velocity = CalculateVelocity(
			    projectile.positionX,
			    projectile.positionY,
			    projectile.positionX + displacement.first,
			    projectile.positionY + displacement.second,
			    projectile.speed);
			projectile.velocityX = velocity.first;
			projectile.velocityY = velocity.second;
			projectile.pathTicks = 16;
		} else {
			--projectile.pathTicks;
		}

		const int32_t previousX = projectile.positionX;
		const int32_t previousY = projectile.positionY;
		projectile.traveledX += projectile.velocityX;
		projectile.traveledY += projectile.velocityY;
		const int32_t pixelX = projectile.traveledX >> 16;
		const int32_t pixelY = projectile.traveledY >> 16;
		const int32_t xNumerator = 2 * pixelY + pixelX;
		const int32_t yNumerator = 2 * pixelY - pixelX;
		projectile.positionX = projectile.startX + (xNumerator + (xNumerator >= 0 ? 32 : -32)) / 64;
		projectile.positionY = projectile.startY + (yNumerator + (yNumerator >= 0 ? 32 : -32)) / 64;
		const bool tileChanged = projectile.positionX != previousX || projectile.positionY != previousY;
		if (tileChanged && (projectile.positionX < 0 || projectile.positionX >= 40 || projectile.positionY < 0 || projectile.positionY >= 40)) {
			iterator = state.projectiles.erase(iterator);
			continue;
		}

		const bool targetAtTile = state.combatTargetEntityId != 0
		    && state.combatTargetHitPointsQ6 > 0
		    && ((state.combatTargetPositionX == projectile.positionX
		            && state.combatTargetPositionY == projectile.positionY)
		        || (state.combatTargetMoving
		            && state.combatTargetPetrified
		            && state.combatTargetFuturePositionX == projectile.positionX
		            && state.combatTargetFuturePositionY == projectile.positionY));
		const bool playerAtTile = state.secondaryPlayerEntityId != 0
		    && state.secondaryPlayerLife > 0
		    && state.secondaryPlayerLevelId == projectile.levelId
		    && state.secondaryPlayerPositionX == projectile.positionX
		    && state.secondaryPlayerPositionY == projectile.positionY;
		const uint32_t collisionTargetEntityId = targetAtTile
		    ? state.combatTargetEntityId
		    : playerAtTile ? state.secondaryPlayerEntityId
		                   : 0;
		if (!tileChanged && projectile.lastCollisionTargetEntityId == collisionTargetEntityId) {
			++iterator;
			continue;
		}
		projectile.lastCollisionTargetEntityId = collisionTargetEntityId;

		bool hitTarget = false;
		if (targetAtTile && state.combatTargetLightningResistance < 100) {
			const int32_t hitChance = std::clamp(
			    state.attributes[3] + 50 - state.combatTargetMonsterLevel * 2,
			    5,
			    95);
			const int32_t hitRoll = std::max(GenerateRnd(state, 100), 0);
			if (state.combatTargetPetrified || hitRoll < hitChance) {
				int32_t damageQ6 = projectile.damage * 64;
				static_cast<void>(GenerateRnd(state, 1));
				if (state.combatTargetLightningResistance > 0)
					damageQ6 /= projectile.resistantDamageDivisor;
				state.combatTargetHitPointsQ6 = std::max(0, state.combatTargetHitPointsQ6 - damageQ6);
				state.combatTargetHitPoints = ToWholeCeilingQ6(state.combatTargetHitPointsQ6);
				if (state.combatTargetHitPointsQ6 == 0)
					DefeatReplayCombatTarget(state);
				hitTarget = true;
			}
		} else if (playerAtTile) {
			const int32_t hitChance = std::clamp(
			    state.attributes[3] + 50 - static_cast<int32_t>(state.secondaryPlayerCharacterLevel) * 2,
			    5,
			    95);
			const int32_t hitRoll = std::max(GenerateRnd(state, 100), 0);
			if (hitRoll < hitChance) {
				const bool canBlock = state.secondaryPlayerBlockCapable
				    && (state.secondaryPlayerCombatMode == 0 || state.secondaryPlayerCombatMode == 4);
				const int32_t blockRoll = canBlock
				    ? std::max(GenerateRnd(state, 100), 0)
				    : 100;
				static_cast<void>(GenerateRnd(state, 1));
				const int32_t damageQ6 = projectile.damage * 32;
				const int32_t blockChance = std::clamp(
				    state.secondaryPlayerDexterity
				        + static_cast<int32_t>(state.secondaryPlayerCharacterLevel) * 2
				        - static_cast<int32_t>(state.characterLevel) * 2,
				    0,
				    100);
				if (blockRoll < blockChance) {
					state.secondaryPlayerCombatMode = 6;
					state.secondaryPlayerCombatModeEndTick = tick + 4;
					if (state.hellfire)
						static_cast<void>(GenerateRnd(state, 2));
				} else {
					state.secondaryPlayerLife = std::max(0, state.secondaryPlayerLife - damageQ6);
					if (state.secondaryPlayerLife == 0) {
						StartReplaySecondaryPlayerDeath(state);
					} else if ((damageQ6 >> 6) >= static_cast<int32_t>(state.secondaryPlayerCharacterLevel)) {
						state.secondaryPlayerCombatMode = 7;
						state.secondaryPlayerCombatModeEndTick = tick + 6;
					}
				}
				hitTarget = true;
			}
		}

		const bool blockingObject = state.objectEntityId != 0
		    && !state.objectActivated
		    && !state.objectDestroyed
		    && !state.objectMissilePassable
		    && state.objectLevelId == projectile.levelId
		    && state.objectPositionX == projectile.positionX
		    && state.objectPositionY == projectile.positionY;
		if (blockingObject) {
			if (state.objectBreakable)
				state.objectDestroyed = true;
			iterator = state.projectiles.erase(iterator);
			continue;
		}

		if (hitTarget) {
			projectile.phase = 1;
			projectile.remainingTicks = static_cast<uint32_t>(projectile.impactLifetime);
			projectile.velocityX = 0;
			projectile.velocityY = 0;
		}
		++iterator;
	}
}

bool IsReplayEquipmentUsable(const ReplayFixtureItemState &item)
{
	return item.statFlag && (item.magical == 0 || item.identified);
}

int32_t ResolveReplayElementalDamage(
	ReplayFixtureExecutionState &state,
	uint32_t flag,
	int32_t ReplayFixtureItemState::*minimum,
	int32_t ReplayFixtureItemState::*maximum)
{
	int32_t minimumDamage = 0;
	int32_t maximumDamage = 0;
	for (const auto &equippedItem : state.equipment) {
		const auto &item = equippedItem.state;
		if (!IsReplayEquipmentUsable(item) || (item.flags & flag) == 0)
			continue;
		minimumDamage += std::max(0, item.*minimum);
		maximumDamage += std::max(0, item.*maximum);
	}
	if (maximumDamage < minimumDamage || maximumDamage == 0)
		return 0;
	return minimumDamage + GenerateRnd(state, maximumDamage - minimumDamage + 1);
}

int32_t ApplyReplayElementalResistance(int32_t damage, int32_t resistance)
{
	if (damage <= 0)
		return 0;
	return damage * (100 - std::clamp(resistance, -100, 100)) / 100;
}

bool UsesReplayRangedAttack(const ReplayFixtureExecutionState &state)
{
	std::vector<ReplayFixtureSlottedItem> hands;
	std::copy_if(state.equipment.begin(), state.equipment.end(), std::back_inserter(hands), [](const auto &item) {
		return (item.slot == 4 || item.slot == 5) && IsReplayEquipmentUsable(item.state);
	});
	std::sort(hands.begin(), hands.end(), [](const auto &left, const auto &right) {
		return left.slot < right.slot;
	});
	const auto weapon = std::find_if(hands.begin(), hands.end(), [](const auto &item) {
		return item.state.itemClass == 1 && item.state.itemType != 5;
	});
	return weapon != hands.end() && weapon->state.itemType == 3;
}

uint32_t GetReplayAttackSkippedFrames(
    const ReplayFixtureExecutionState &state,
    bool ranged,
    std::string_view gameVariant,
    bool includesFirstFrame = false)
{
	uint32_t flags = 0;
	for (const auto &item : state.equipment) {
		if (IsReplayEquipmentUsable(item.state))
			flags |= item.state.flags;
	}
	if (ranged) {
		if (gameVariant == "Hellfire")
			return 0;
		uint32_t skippedFrames = includesFirstFrame
		        && (flags & (QuickAttackFlag | FastAttackFlag)) != 0
		    ? 1
		    : 0;
		if ((flags & FastAttackFlag) != 0)
			++skippedFrames;
		return skippedFrames;
	}
	if ((flags & FastestAttackFlag) != 0)
		return includesFirstFrame ? 4 : 2;
	if ((flags & FasterAttackFlag) != 0)
		return includesFirstFrame ? 3 : 2;
	if ((flags & FastAttackFlag) != 0)
		return includesFirstFrame ? 2 : 1;
	if ((flags & QuickAttackFlag) != 0)
		return includesFirstFrame ? 1 : 0;
	return 0;
}

bool IsReplayAttackTargetValid(
    const ReplayFixtureExecutionState &state,
    uint32_t targetEntityId,
    bool ranged = false)
{
	const int distance = std::max(
	    std::abs(state.positionX - state.combatTargetPositionX),
	    std::abs(state.positionY - state.combatTargetPositionY));
	return targetEntityId == state.combatTargetEntityId
	    && state.combatTargetHitPointsQ6 > 0
	    && (ranged ? distance > 0 : distance <= 1);
}

bool HasReplayEquipmentFlag(
    const ReplayFixtureExecutionState &state,
    uint32_t flag)
{
	return std::any_of(
	    state.equipment.begin(),
	    state.equipment.end(),
	    [flag](const ReplayFixtureSlottedItem &item) {
		    return IsReplayEquipmentUsable(item.state)
		        && (item.state.flags & flag) != 0;
	    });
}

int32_t ResolveReplayWeaponArrowSpeed(ReplayFixtureExecutionState &state)
{
	int32_t speed = HasReplayEquipmentFlag(state, RandomArrowVelocityFlag)
	    ? 16 + GenerateRnd(state, 32)
	    : 32;
	if (state.characterClassId == 1)
		speed += std::max<int32_t>(0, static_cast<int32_t>(state.characterLevel) - 1) / 4;
	else if (state.characterClassId == 0 || state.characterClassId == 4)
		speed += std::max<int32_t>(0, static_cast<int32_t>(state.characterLevel) - 1) / 8;
	if (state.hellfire) {
		if (HasReplayEquipmentFlag(state, QuickAttackFlag))
			speed++;
		if (HasReplayEquipmentFlag(state, FastAttackFlag))
			speed += 2;
		if (HasReplayEquipmentFlag(state, FasterAttackFlag))
			speed += 4;
		if (HasReplayEquipmentFlag(state, FastestAttackFlag))
			speed += 8;
	}
	return speed;
}

void CreateReplayWeaponArrow(
    ReplayFixtureExecutionState &state,
    uint32_t targetEntityId,
    uint32_t arrowIndex,
    uint32_t arrowCount,
    bool consumeRandom)
{
	const int32_t targetDistance = std::max(
	    std::abs(state.combatTargetPositionX - state.positionX),
	    std::abs(state.combatTargetPositionY - state.positionY));
	if (consumeRandom)
		static_cast<void>(GenerateRnd(state, 1));

	const int32_t fireDamage = ResolveReplayElementalDamage(
	    state,
	    FireArrowsFlag,
	    &ReplayFixtureItemState::fireMinDamage,
	    &ReplayFixtureItemState::fireMaxDamage);
	const int32_t lightningDamage = ResolveReplayElementalDamage(
	    state,
	    LightningArrowsFlag,
	    &ReplayFixtureItemState::lightningMinDamage,
	    &ReplayFixtureItemState::lightningMaxDamage);
	uint32_t nextEntityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);

	ReplayFixtureProjectileState projectile;
	projectile.entityId = nextEntityId;
	projectile.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
	projectile.targetEntityId = targetEntityId;
	projectile.levelId = state.levelId;
	projectile.positionX = state.positionX;
	projectile.positionY = state.positionY;
	projectile.targetX = state.combatTargetPositionX;
	projectile.targetY = state.combatTargetPositionY;
	if (arrowCount == 3 && arrowIndex != 0) {
		const auto direction = DirectionDisplacements[
		    GetDirection(
		        state.positionX,
		        state.positionY,
		        state.combatTargetPositionX,
		        state.combatTargetPositionY)];
		const int32_t spreadX = -direction.second * (arrowIndex == 1 ? -1 : 1);
		const int32_t spreadY = direction.first * (arrowIndex == 1 ? -1 : 1);
		const int32_t candidateX = state.positionX + spreadX;
		const int32_t candidateY = state.positionY + spreadY;
		if (candidateX >= 0 && candidateX < 40 && candidateY >= 0 && candidateY < 40) {
			projectile.positionX = candidateX;
			projectile.positionY = candidateY;
		}
	}
	projectile.damage = 1;
	projectile.damageQ6 = 64;
	projectile.elementalFireDamage = fireDamage;
	projectile.elementalLightningDamage = lightningDamage;
	projectile.damageType = 0;
	const int32_t speed = ResolveReplayWeaponArrowSpeed(state);
	const int32_t travelTiles = std::max(targetDistance - 1, 0);
	projectile.remainingTicks = static_cast<uint32_t>(
	    (travelTiles * 32 + speed - 1) / speed + 1);
	projectile.speed = speed == 32 ? 0 : speed;
	projectile.behavior = WeaponArrowBehavior;
	projectile.attackHit = true;
	projectile.activationDelayTicks = arrowIndex == 0 ? 0 : 2;
	state.projectiles.push_back(projectile);
}

void ApplyReplayWeaponArrowImpact(
    ReplayFixtureExecutionState &state,
    const ReplayFixtureProjectileState &projectile)
{
	if (projectile.targetEntityId != state.combatTargetEntityId
	    || state.combatTargetHitPointsQ6 <= 0
	    || (projectile.levelId != state.combatTargetLevelId
	        && state.combatTargetLevelId != 0))
		return;

	const int32_t damageQ6 = projectile.damageQ6 != 0
	    ? projectile.damageQ6
	    : projectile.damage * 64;
	state.combatTargetHitPointsQ6 = std::max(
	    0,
	    state.combatTargetHitPointsQ6 - std::max(0, damageQ6));
	state.combatTargetHitPoints = ToWholeCeilingQ6(state.combatTargetHitPointsQ6);
	if (projectile.attackHit && state.combatTargetHitPointsQ6 > 0) {
		const int32_t fireDamage = ApplyReplayElementalResistance(
		    projectile.elementalFireDamage,
		    state.combatTargetFireResistance);
		const int32_t lightningDamage = ApplyReplayElementalResistance(
		    projectile.elementalLightningDamage,
		    state.combatTargetLightningResistance);
		state.combatTargetHitPointsQ6 = std::max(
		    0,
		    state.combatTargetHitPointsQ6 - fireDamage * 64);
		state.combatTargetHitPoints = ToWholeCeilingQ6(state.combatTargetHitPointsQ6);
		if (state.combatTargetHitPointsQ6 > 0) {
			state.combatTargetHitPointsQ6 = std::max(
			    0,
			    state.combatTargetHitPointsQ6 - lightningDamage * 64);
			state.combatTargetHitPoints = ToWholeCeilingQ6(state.combatTargetHitPointsQ6);
		}
	}
	if (state.combatTargetHitPointsQ6 <= 0)
		DefeatReplayCombatTarget(state);
}

void AdvanceReplayWeaponArrowsOneTick(ReplayFixtureExecutionState &state)
{
	for (auto iterator = state.projectiles.begin(); iterator != state.projectiles.end();) {
		auto &projectile = *iterator;
		if (projectile.behavior != WeaponArrowBehavior) {
			++iterator;
			continue;
		}
		if (projectile.activationDelayTicks > 0) {
			--projectile.activationDelayTicks;
			++iterator;
			continue;
		}
		if (projectile.remainingTicks > 1) {
			const int32_t velocity = projectile.speed >= 16 && projectile.speed <= 74
			    ? projectile.speed
			    : 32;
			const int32_t travelBudget = velocity + projectile.pathTicks;
			const int32_t travelSteps = travelBudget / 32;
			projectile.pathTicks = travelBudget % 32;
			bool removed = false;
			for (int32_t step = 0; step < travelSteps; ++step) {
				const int32_t nextX = projectile.positionX
				    + (projectile.targetX > projectile.positionX)
				        - (projectile.targetX < projectile.positionX);
				const int32_t nextY = projectile.positionY
				    + (projectile.targetY > projectile.positionY)
				        - (projectile.targetY < projectile.positionY);
				const bool outsideWorld = nextX < 0 || nextX >= 40 || nextY < 0 || nextY >= 40;
				const bool blockingObject = state.objectEntityId != 0
				    && !state.objectActivated
				    && !state.objectDestroyed
				    && !state.objectMissilePassable
				    && state.objectLevelId == projectile.levelId
				    && state.objectPositionX == nextX
				    && state.objectPositionY == nextY;
				if (outsideWorld || blockingObject) {
					if (blockingObject && state.objectBreakable)
						state.objectDestroyed = true;
					iterator = state.projectiles.erase(iterator);
					removed = true;
					break;
				}
				projectile.positionX = nextX;
				projectile.positionY = nextY;
			}
			if (removed)
				continue;
			--projectile.remainingTicks;
			++iterator;
			continue;
		}
		ApplyReplayWeaponArrowImpact(state, projectile);
		iterator = state.projectiles.erase(iterator);
	}
}

/** Advances a seeded native MonsterArrow projectile through travel and impact frames. */
void AdvanceReplayMonsterArrowsOneTick(ReplayFixtureExecutionState &state)
{
	for (auto iterator = state.projectiles.begin(); iterator != state.projectiles.end();) {
		auto &projectile = *iterator;
		if (projectile.behavior != MonsterArrowBehavior) {
			++iterator;
			continue;
		}
		if (projectile.phase == 1) {
			if (projectile.remainingTicks > 0)
				--projectile.remainingTicks;
			if (projectile.remainingTicks == 0)
				iterator = state.projectiles.erase(iterator);
			else
				++iterator;
			continue;
		}
		if (projectile.sourceEntityId != state.combatTargetEntityId) {
			iterator = state.projectiles.erase(iterator);
			continue;
		}
		const int32_t travelSteps = std::max(1, projectile.speed);
		bool removed = false;
		for (int32_t step = 0; step < travelSteps; ++step) {
			const int32_t nextX = projectile.positionX
			    + (projectile.targetX > projectile.positionX)
			        - (projectile.targetX < projectile.positionX);
			const int32_t nextY = projectile.positionY
			    + (projectile.targetY > projectile.positionY)
			        - (projectile.targetY < projectile.positionY);
			if (nextX < 0 || nextX >= 40 || nextY < 0 || nextY >= 40) {
				iterator = state.projectiles.erase(iterator);
				removed = true;
				break;
			}
			projectile.positionX = nextX;
			projectile.positionY = nextY;
			int32_t *hitLife = nullptr;
			if (state.levelId == projectile.levelId
			    && state.life > 0
			    && nextX == state.positionX
			    && nextY == state.positionY) {
				hitLife = &state.life;
			} else if (state.secondaryPlayerEntityId != 0
			    && state.secondaryPlayerLife > 0
			    && state.secondaryPlayerLevelId == projectile.levelId
			    && nextX == state.secondaryPlayerPositionX
			    && nextY == state.secondaryPlayerPositionY) {
				hitLife = &state.secondaryPlayerLife;
			}
			if (hitLife != nullptr) {
				const bool secondaryHit = hitLife == &state.secondaryPlayerLife;
				int32_t damage = projectile.damage;
				if (damage > 0 && projectile.damageMinimum > 0
				    && projectile.damageMaximum >= projectile.damageMinimum) {
					damage = projectile.damageMinimum
					    + GenerateRnd(state, projectile.damageMaximum - projectile.damageMinimum + 1);
				}
				*hitLife = std::max(0, *hitLife - std::max(0, damage));
				if (secondaryHit && *hitLife == 0)
					StartReplaySecondaryPlayerDeath(state, true);
				if (projectile.impactLifetime > 0) {
					projectile.phase = 1;
					projectile.remainingTicks = static_cast<uint32_t>(projectile.impactLifetime);
				} else {
					iterator = state.projectiles.erase(iterator);
					removed = true;
				}
				break;
			}
			if (nextX == projectile.targetX && nextY == projectile.targetY)
				break;
		}
		if (removed)
			continue;
		if (projectile.phase == 0) {
			if (projectile.remainingTicks > 0)
				--projectile.remainingTicks;
			if (projectile.remainingTicks == 0)
				iterator = state.projectiles.erase(iterator);
			else
				++iterator;
		} else {
			++iterator;
		}
	}
}

/** Advances the native Fallen stand animation before its rally decision. */
bool AdvanceReplayFallenStandAnimation(ReplayFixtureExecutionState &state)
{
	if (state.combatTargetNativeAi != "Fallen"
	    || state.combatTargetHitPointsQ6 <= 0)
		return false;
	constexpr int32_t Frames = 11;
	constexpr int32_t TicksPerFrame = 3;
	if (state.combatTargetFallenStandAnimationTick + 1 < TicksPerFrame) {
		++state.combatTargetFallenStandAnimationTick;
		return false;
	}
	state.combatTargetFallenStandAnimationTick = 0;
	if (state.combatTargetFallenStandAnimationFrame + 1 < Frames)
		++state.combatTargetFallenStandAnimationFrame;
	else
		state.combatTargetFallenStandAnimationFrame = 0;
	const bool rallies = GenerateRnd(state, 4) == 0;
	if (!rallies)
		return true;
	const int32_t healing = 2 * state.combatTargetIntelligence + 2;
	state.combatTargetHitPointsQ6 = std::min(
		state.combatTargetMaxHitPointsQ6,
		state.combatTargetHitPointsQ6 + healing * 64);
	state.combatTargetHitPoints = ToWholeCeilingQ6(state.combatTargetHitPointsQ6);
	state.combatTargetFallenRallyTicks = 30 * state.combatTargetIntelligence + 105;
	return true;
}

/** Advances the native Fallen rally pursuit step for the compact replay target. */
void AdvanceReplayFallenRallyPursuit(ReplayFixtureExecutionState &state)
{
	if (state.combatTargetNativeAi != "Fallen"
	    || state.combatTargetFallenRallyTicks == 0
	    || state.combatTargetHitPointsQ6 <= 0)
		return;
	struct Victim {
		uint32_t entityId;
		int32_t positionX;
		int32_t positionY;
	};
	std::vector<Victim> victims;
	if (state.life > 0
	    && state.combatMode != 10
	    && state.combatMode != 11
	    && (state.combatTargetLevelId == 0
	        || state.levelId == state.combatTargetLevelId))
		victims.push_back({ state.entityId == 0 ? 1U : state.entityId, state.positionX, state.positionY });
	if (state.secondaryPlayerEntityId != 0
	    && state.secondaryPlayerLife > 0
	    && state.secondaryPlayerCombatMode != 10
	    && state.secondaryPlayerCombatMode != 11
	    && (state.combatTargetLevelId == 0
	        || state.secondaryPlayerLevelId == state.combatTargetLevelId))
		victims.push_back({ state.secondaryPlayerEntityId, state.secondaryPlayerPositionX, state.secondaryPlayerPositionY });
	if (victims.empty()) {
		--state.combatTargetFallenRallyTicks;
		return;
	}
	const auto victimDistance = [&](const Victim &victim) {
		return std::max(
		    std::abs(victim.positionX - state.combatTargetPositionX),
		    std::abs(victim.positionY - state.combatTargetPositionY));
	};
	std::sort(victims.begin(), victims.end(), [&](const auto &left, const auto &right) {
		const int32_t leftDistance = victimDistance(left);
		const int32_t rightDistance = victimDistance(right);
		return leftDistance != rightDistance
		    ? leftDistance < rightDistance
		    : left.entityId < right.entityId;
	});
	const Victim &victim = victims.front();
	const int32_t distance = std::max(
	    std::abs(victim.positionX - state.combatTargetPositionX),
	    std::abs(victim.positionY - state.combatTargetPositionY));
	if (distance > 1) {
		const int32_t direction = GetDirection(
		    state.combatTargetPositionX,
		    state.combatTargetPositionY,
		    victim.positionX,
		    victim.positionY);
		const int32_t firstTurn = GenerateRnd(state, 2) == 0 ? 1 : -1;
		const int32_t secondTurn = GenerateRnd(state, 2) == 0 ? 2 : -2;
		const int32_t candidateDirections[] = {
			direction,
			(direction + firstTurn + 8) % 8,
			(direction - firstTurn + 8) % 8,
			(direction + secondTurn + 8) % 8,
			(direction - secondTurn + 8) % 8,
		};
		for (const int32_t candidateDirection : candidateDirections) {
			const auto displacement = DirectionDisplacements[candidateDirection];
			const int32_t candidateX = state.combatTargetPositionX + displacement.first;
			const int32_t candidateY = state.combatTargetPositionY + displacement.second;
			if (candidateX < 0 || candidateX >= 40
			    || candidateY < 0 || candidateY >= 40
			    || (candidateX == state.positionX && candidateY == state.positionY)
			    || (state.secondaryPlayerEntityId != 0
			        && candidateX == state.secondaryPlayerPositionX
			        && candidateY == state.secondaryPlayerPositionY))
				continue;
			state.combatTargetMoving = true;
			state.combatTargetFuturePositionX = candidateX;
			state.combatTargetFuturePositionY = candidateY;
			state.combatTargetDirection = candidateDirection;
			break;
		}
	}
	--state.combatTargetFallenRallyTicks;
}

/** Advances the bounded companion projection used by the rally propagation fixture. */
void AdvanceReplayFallenRallyCompanion(ReplayFixtureExecutionState &state, bool propagated)
{
	if (state.combatTargetCompanionEntityId == 0
	    || state.combatTargetCompanionNativeAi != "Fallen"
	    || state.combatTargetCompanionHitPointsQ6 <= 0)
		return;
	if (propagated
	    && state.combatTargetCompanionLevelId == state.combatTargetLevelId
	    && std::max(
	           std::abs(state.combatTargetCompanionPositionX - state.combatTargetPositionX),
	           std::abs(state.combatTargetCompanionPositionY - state.combatTargetPositionY))
	        <= 2 * state.combatTargetIntelligence + 4)
		state.combatTargetCompanionFallenRallyTicks = 30 * state.combatTargetIntelligence + 105;
	// Native ProcessMonsters advances the shared stream for the propagated
	// member's follow-up decision even when the member remains adjacent.
	if (propagated)
		state.randomState = 0x015A4E35U * state.randomState + 1U;
	constexpr int32_t TicksPerFrame = 3;
	if (state.combatTargetCompanionFallenStandAnimationTick + 1 < TicksPerFrame)
		++state.combatTargetCompanionFallenStandAnimationTick;
	else
		state.combatTargetCompanionFallenStandAnimationTick = 0;
	if (state.combatTargetCompanionFallenRallyTicks > 0)
		--state.combatTargetCompanionFallenRallyTicks;
}

int32_t GenerateReplayMana(
    ReplayFixtureExecutionState &state,
    int32_t spellLevel)
{
	int32_t mana = GenerateRnd(state, 10) + 1;
	for (uint32_t level = 0; level < state.characterLevel; ++level)
		mana += GenerateRnd(state, 4) + 1;
	for (int32_t level = 0; level < spellLevel; ++level)
		mana += GenerateRnd(state, 6) + 1;
	mana *= 64;
	if (state.characterClassId == 2) {
		mana *= 2;
	} else if (state.characterClassId == 1 || state.characterClassId == 4) {
		mana += mana / 2;
	}
	return mana;
}

int32_t RotateReplayDirection(int32_t direction, int32_t offset)
{
	return (direction + offset + 8 * 4) % 8;
}

void CreateReplayFireWallController(
	ReplayFixtureExecutionState &state,
	const ReplayFixtureCommand &command,
	int32_t spellLevel,
	uint32_t spellId,
	int32_t behavior)
{
	const int32_t direction = GetDirection(
		state.positionX,
		state.positionY,
		command.targetX,
		command.targetY);
	ReplayFixtureProjectileState controller;
	controller.entityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		controller.entityId = std::max(controller.entityId, projectile.entityId + 1U);
	controller.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
	controller.spellId = spellId;
	controller.levelId = state.levelId;
	controller.positionX = command.targetX;
	controller.positionY = command.targetY;
	controller.targetX = command.targetX;
	controller.targetY = command.targetY;
	controller.damageType = behavior == LightningWallControllerBehavior ? 2 : 1;
	controller.remainingTicks = 7;
	controller.behavior = behavior;
	controller.startX = state.positionX;
	controller.startY = state.positionY;
	controller.originalDirection = direction;
	controller.resistantDamageDivisor = 4;
	controller.effectiveSpellLevel = spellLevel;
	controller.wallLeftX = command.targetX;
	controller.wallLeftY = command.targetY;
	controller.wallRightX = command.targetX;
	controller.wallRightY = command.targetY;
	controller.wallLeftDirection = RotateReplayDirection(direction, -2);
	controller.wallRightDirection = RotateReplayDirection(direction, 2);
	state.projectiles.push_back(controller);
}

void CreateReplayFlameWaveController(
	ReplayFixtureExecutionState &state,
	const ReplayFixtureCommand &command,
	int32_t spellLevel)
{
	ReplayFixtureProjectileState controller;
	controller.entityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		controller.entityId = std::max(controller.entityId, projectile.entityId + 1U);
	controller.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
	controller.spellId = 15;
	controller.levelId = state.levelId;
	controller.positionX = state.positionX;
	controller.positionY = state.positionY;
	controller.targetX = command.targetX;
	controller.targetY = command.targetY;
	controller.damageType = 1;
	controller.remainingTicks = 1;
	controller.behavior = FlameWaveControllerBehavior;
	controller.startX = state.positionX;
	controller.startY = state.positionY;
	controller.originalDirection = GetDirection(
		state.positionX,
		state.positionY,
		command.targetX,
		command.targetY);
	controller.resistantDamageDivisor = 4;
	controller.effectiveSpellLevel = spellLevel;
	state.projectiles.push_back(controller);
}

void CreateReplayRingOfFireController(
	ReplayFixtureExecutionState &state,
	int32_t spellLevel)
{
	ReplayFixtureProjectileState controller;
	controller.entityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		controller.entityId = std::max(controller.entityId, projectile.entityId + 1U);
	controller.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
	controller.spellId = 44;
	controller.levelId = state.levelId;
	controller.positionX = state.positionX;
	controller.positionY = state.positionY;
	controller.targetX = state.positionX;
	controller.targetY = state.positionY;
	controller.damageType = 1;
	controller.remainingTicks = 1;
	controller.behavior = RingOfFireControllerBehavior;
	controller.startX = state.positionX;
	controller.startY = state.positionY;
	controller.resistantDamageDivisor = 4;
	controller.effectiveSpellLevel = spellLevel;
	state.projectiles.push_back(controller);
}

std::vector<std::pair<int32_t, int32_t>> CreateReplayRadialProjectileOffsets(
	int32_t radius,
	int32_t projectileCount)
{
	std::vector<std::pair<int32_t, int32_t>> offsets;
	for (int32_t y = 0; y <= radius && static_cast<int32_t>(offsets.size()) < projectileCount; ++y) {
		const std::pair<int32_t, int32_t> quarter { radius, y };
		const std::pair<int32_t, int32_t> candidates[] = {
			quarter,
			{ -quarter.first, -quarter.second },
			{ -quarter.first, quarter.second },
			{ quarter.first, -quarter.second },
		};
		for (const auto offset : candidates) {
			if (static_cast<int32_t>(offsets.size()) >= projectileCount)
				break;
			offsets.push_back(offset);
		}
	}
	for (int32_t x = radius - 1; x >= 0 && static_cast<int32_t>(offsets.size()) < projectileCount; --x) {
		const std::pair<int32_t, int32_t> quarter { x, radius };
		const std::pair<int32_t, int32_t> candidates[] = {
			quarter,
			{ -quarter.first, -quarter.second },
			{ -quarter.first, quarter.second },
			{ quarter.first, -quarter.second },
		};
		for (const auto offset : candidates) {
			if (static_cast<int32_t>(offsets.size()) >= projectileCount)
				break;
			offsets.push_back(offset);
		}
	}
	return offsets;
}

ReplayFixtureProjectileState CreateReplayNovaController(
	ReplayFixtureExecutionState &state,
	const ReplayFixtureCommand &command,
	int32_t spellLevel)
{
	ReplayFixtureProjectileState controller;
	controller.entityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		controller.entityId = std::max(controller.entityId, projectile.entityId + 1U);
	controller.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
	controller.spellId = 18;
	controller.levelId = state.levelId;
	controller.positionX = state.positionX;
	controller.positionY = state.positionY;
	controller.targetX = command.targetX;
	controller.targetY = command.targetY;
	controller.startX = state.positionX;
	controller.startY = state.positionY;
	int32_t damage = 0;
	for (int32_t iteration = 0; iteration < 5; ++iteration)
		damage += GenerateRnd(state, 6);
	damage += static_cast<int32_t>(state.characterLevel) + 5;
	damage /= 2;
	for (int32_t level = 0; level < spellLevel; ++level)
		damage += damage / 8;
	controller.damage = damage * 64;
	controller.damageType = 2;
	controller.remainingTicks = 1;
	controller.behavior = NovaControllerBehavior;
	controller.originalDirection = GetDirection(
		state.positionX,
		state.positionY,
		command.targetX,
		command.targetY);
	controller.resistantDamageDivisor = 4;
	controller.effectiveSpellLevel = spellLevel;
	return controller;
}

ReplayFixtureProjectileState CreateReplayImmolationController(
	ReplayFixtureExecutionState &state,
	const ReplayFixtureCommand &command,
	int32_t spellLevel)
{
	ReplayFixtureProjectileState controller;
	controller.entityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		controller.entityId = std::max(controller.entityId, projectile.entityId + 1U);
	controller.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
	controller.spellId = 45;
	controller.levelId = state.levelId;
	controller.positionX = state.positionX;
	controller.positionY = state.positionY;
	controller.targetX = command.targetX;
	controller.targetY = command.targetY;
	int32_t damage = 0;
	for (int32_t iteration = 0; iteration < 5; ++iteration)
		damage += GenerateRnd(state, 6);
	damage += static_cast<int32_t>(state.characterLevel) + 5;
	damage /= 2;
	for (int32_t level = 0; level < spellLevel; ++level)
		damage += damage / 8;
	controller.damage = damage * 64;
	controller.damageType = 1;
	controller.remainingTicks = 1;
	controller.behavior = ImmolationControllerBehavior;
	controller.startX = state.positionX;
	controller.startY = state.positionY;
	controller.originalDirection = GetDirection(
		state.positionX,
		state.positionY,
		command.targetX,
		command.targetY);
	int32_t velocityTargetX = command.targetX;
	int32_t velocityTargetY = command.targetY;
	if (velocityTargetX == state.positionX && velocityTargetY == state.positionY) {
		velocityTargetX += DirectionDisplacements[controller.originalDirection].first;
		velocityTargetY += DirectionDisplacements[controller.originalDirection].second;
	}
	const int32_t speed = 16 + std::min(spellLevel, 34);
	const auto velocity = CalculateVelocity(
		state.positionX,
		state.positionY,
		velocityTargetX,
		velocityTargetY,
		speed);
	controller.velocityX = velocity.first;
	controller.velocityY = velocity.second;
	controller.speed = speed;
	controller.effectiveSpellLevel = spellLevel;
	return controller;
}

ReplayFixtureProjectileState CreateReplayImmolationBall(
	const ReplayFixtureProjectileState &controller,
	int32_t targetX,
	int32_t targetY)
{
	ReplayFixtureProjectileState ball;
	ball.sourceEntityId = controller.sourceEntityId;
	ball.spellId = controller.spellId;
	ball.levelId = controller.levelId;
	ball.positionX = controller.positionX;
	ball.positionY = controller.positionY;
	ball.targetX = targetX;
	ball.targetY = targetY;
	ball.damage = controller.damage;
	ball.damageType = 1;
	ball.remainingTicks = 256;
	ball.behavior = 16;
	ball.startX = controller.positionX;
	ball.startY = controller.positionY;
	ball.originalDirection = GetDirection(
		controller.positionX,
		controller.positionY,
		targetX,
		targetY);
	const auto velocity = CalculateVelocity(
		controller.positionX,
		controller.positionY,
		targetX,
		targetY,
		16);
	ball.velocityX = velocity.first;
	ball.velocityY = velocity.second;
	ball.speed = 16;
	ball.impactLifetime = 1;
	ball.resistantDamageDivisor = 4;
	ball.effectiveSpellLevel = controller.effectiveSpellLevel;
	return ball;
}

ReplayFixtureProjectileState CreateReplayElemental(
    ReplayFixtureExecutionState &state,
    const ReplayFixtureCommand &command,
    int32_t spellLevel)
{
	ReplayFixtureProjectileState projectile;
	projectile.entityId = 0x80000000U;
	for (const auto &existing : state.projectiles)
		projectile.entityId = std::max(projectile.entityId, existing.entityId + 1U);
	projectile.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
	projectile.spellId = command.spellId;
	projectile.levelId = state.levelId;
	projectile.positionX = state.positionX;
	projectile.positionY = state.positionY;
	projectile.targetX = command.targetX;
	projectile.targetY = command.targetY;
	projectile.areaRadius = 1;
	projectile.startX = state.positionX;
	projectile.startY = state.positionY;
	int32_t damage = 0;
	for (int32_t iteration = 0; iteration < 2; ++iteration)
		damage += GenerateRnd(state, 10);
	damage = 2 * (static_cast<int32_t>(state.characterLevel) + damage) + 4;
	for (int32_t level = 0; level < spellLevel; ++level)
		damage += damage / 8;
	projectile.damage = damage / 2;
	projectile.damageType = 1;
	projectile.remainingTicks = 256;
	projectile.behavior = ElementalBehavior;
	projectile.impactLifetime = 14;
	projectile.resistantDamageDivisor = 4;
	projectile.effectiveSpellLevel = spellLevel;
	projectile.originalDirection = GetDirection(
		state.positionX,
		state.positionY,
		command.targetX,
		command.targetY);
	int32_t velocityTargetX = command.targetX;
	int32_t velocityTargetY = command.targetY;
	if (velocityTargetX == state.positionX && velocityTargetY == state.positionY) {
		velocityTargetX += DirectionDisplacements[projectile.originalDirection].first;
		velocityTargetY += DirectionDisplacements[projectile.originalDirection].second;
	}
	const auto velocity = CalculateVelocity(
		state.positionX,
		state.positionY,
		velocityTargetX,
		velocityTargetY,
		16);
	projectile.velocityX = velocity.first;
	projectile.velocityY = velocity.second;
	projectile.speed = 16;
	projectile.elementalOriginX = velocityTargetX;
	projectile.elementalOriginY = velocityTargetY;
	return projectile;
}

ReplayFixtureProjectileState CreateReplayInfernoController(
	ReplayFixtureExecutionState &state,
	const ReplayFixtureCommand &command,
	int32_t spellLevel)
{
	ReplayFixtureProjectileState controller;
	controller.entityId = 0x80000000U;
	for (const auto &existing : state.projectiles)
		controller.entityId = std::max(controller.entityId, existing.entityId + 1U);
	controller.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
	controller.spellId = 21;
	controller.levelId = state.levelId;
	controller.positionX = state.positionX;
	controller.positionY = state.positionY;
	controller.targetX = command.targetX;
	controller.targetY = command.targetY;
	controller.startX = state.positionX;
	controller.startY = state.positionY;
	controller.originalDirection = GetDirection(
		state.positionX,
		state.positionY,
		command.targetX,
		command.targetY);
	int32_t velocityTargetX = command.targetX;
	int32_t velocityTargetY = command.targetY;
	if (velocityTargetX == state.positionX && velocityTargetY == state.positionY) {
		velocityTargetX += DirectionDisplacements[controller.originalDirection].first;
		velocityTargetY += DirectionDisplacements[controller.originalDirection].second;
	}
	const auto velocity = CalculateVelocity(
		state.positionX,
		state.positionY,
		velocityTargetX,
		velocityTargetY,
		32);
	controller.velocityX = velocity.first;
	controller.velocityY = velocity.second;
	controller.speed = 32;
	controller.damageType = 1;
	controller.remainingTicks = 256;
	controller.behavior = InfernoControllerBehavior;
	controller.impactLifetime = 1;
	controller.resistantDamageDivisor = 1;
	controller.effectiveSpellLevel = spellLevel;
	return controller;
}

ReplayFixtureProjectileState CreateReplayApocalypseController(
	ReplayFixtureExecutionState &state,
	int32_t spellLevel)
{
	ReplayFixtureProjectileState controller;
	controller.entityId = 0x80000000U;
	for (const auto &existing : state.projectiles)
		controller.entityId = std::max(controller.entityId, existing.entityId + 1U);
	controller.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
	controller.spellId = 25;
	controller.levelId = state.levelId;
	controller.positionX = state.positionX;
	controller.positionY = state.positionY;
	controller.targetX = state.positionX;
	controller.targetY = state.positionY;
	controller.startX = state.positionX;
	controller.startY = state.positionY;
	controller.damage = 0;
	for (uint32_t iteration = 0; iteration < state.characterLevel; ++iteration)
		controller.damage += GenerateRnd(state, 6);
	controller.damage += static_cast<int32_t>(state.characterLevel);
	controller.damageType = 3;
	controller.remainingTicks = 255;
	controller.behavior = ApocalypseControllerBehavior;
	controller.impactLifetime = 1;
	controller.resistantDamageDivisor = 1;
	controller.effectiveSpellLevel = spellLevel;
	controller.apocalypseScanX = std::max(state.positionX - 8, 1);
	controller.apocalypseScanY = std::max(state.positionY - 8, 1);
	return controller;
}

ReplayFixtureProjectileState CreateReplayApocalypseBoom(
	ReplayFixtureExecutionState &state,
	const ReplayFixtureProjectileState &controller,
	int32_t positionX,
	int32_t positionY)
{
	ReplayFixtureProjectileState boom;
	boom.entityId = controller.entityId + 1U;
	for (const auto &existing : state.projectiles)
		boom.entityId = std::max(boom.entityId, existing.entityId + 1U);
	boom.sourceEntityId = controller.sourceEntityId;
	boom.spellId = controller.spellId;
	boom.levelId = controller.levelId;
	boom.positionX = positionX;
	boom.positionY = positionY;
	boom.targetX = positionX;
	boom.targetY = positionY;
	boom.damage = controller.damage;
	boom.damageType = 0;
	boom.remainingTicks = 15;
	boom.behavior = ApocalypseBoomBehavior;
	boom.impactLifetime = 1;
	boom.resistantDamageDivisor = controller.resistantDamageDivisor;
	boom.effectiveSpellLevel = controller.effectiveSpellLevel;
	return boom;
}

ReplayFixtureProjectileState CreateReplayGuardianController(
    const ReplayFixtureExecutionState &state,
    const ReplayFixtureCommand &command,
    int32_t spellLevel)
{
	ReplayFixtureProjectileState controller;
	controller.entityId = 0x80000000U;
	for (const auto &existing : state.projectiles)
		controller.entityId = std::max(controller.entityId, existing.entityId + 1U);
	controller.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
	controller.spellId = command.spellId;
	controller.levelId = state.levelId;
	controller.positionX = command.targetX;
	controller.positionY = command.targetY;
	controller.targetX = command.targetX;
	controller.targetY = command.targetY;
	controller.damageType = 1;
	controller.remainingTicks = 30;
	controller.behavior = GuardianControllerBehavior;
	controller.originalDirection = state.facingDirection % 8;
	controller.effectiveSpellLevel = spellLevel;
	return controller;
}

ReplayFixtureProjectileState CreateReplayRageController(
    const ReplayFixtureExecutionState &state,
    int32_t spellLevel)
{
	ReplayFixtureProjectileState controller;
	controller.entityId = 0x80000000U;
	for (const auto &existing : state.projectiles)
		controller.entityId = std::max(controller.entityId, existing.entityId + 1U);
	controller.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
	controller.spellId = 22;
	controller.levelId = state.levelId;
	controller.positionX = state.positionX;
	controller.positionY = state.positionY;
	controller.targetX = state.positionX;
	controller.targetY = state.positionY;
	controller.damage = static_cast<int32_t>(state.characterLevel) * 6;
	controller.damageType = 0;
	controller.remainingTicks = 245 + state.characterLevel * 2;
	controller.behavior = RageControllerBehavior;
	controller.startX = state.positionX;
	controller.startY = state.positionY;
	controller.effectiveSpellLevel = spellLevel;
	return controller;
}

ReplayFixtureProjectileState CreateReplayGuardianBolt(
    ReplayFixtureExecutionState &state,
    const ReplayFixtureProjectileState &controller,
    int32_t targetX,
    int32_t targetY)
{
	ReplayFixtureProjectileState bolt;
	bolt.entityId = 0x80000000U;
	for (const auto &existing : state.projectiles)
		bolt.entityId = std::max(bolt.entityId, existing.entityId + 1U);
	bolt.sourceEntityId = controller.sourceEntityId;
	bolt.spellId = controller.spellId;
	bolt.levelId = controller.levelId;
	bolt.positionX = state.positionX;
	bolt.positionY = state.positionY;
	bolt.targetX = targetX;
	bolt.targetY = targetY;
	bolt.damage = GenerateRnd(state, 10)
		+ state.attributes[3] / 8
		+ controller.effectiveSpellLevel
		+ 1;
	bolt.damageType = 1;
	bolt.remainingTicks = 256;
	bolt.behavior = 2;
	bolt.startX = state.positionX;
	bolt.startY = state.positionY;
	bolt.originalDirection = GetDirection(
		state.positionX,
		state.positionY,
		targetX,
		targetY);
	int32_t velocityTargetX = targetX;
	int32_t velocityTargetY = targetY;
	if (velocityTargetX == state.positionX && velocityTargetY == state.positionY) {
		velocityTargetX += DirectionDisplacements[bolt.originalDirection].first;
		velocityTargetY += DirectionDisplacements[bolt.originalDirection].second;
	}
	const auto velocity = CalculateVelocity(
		state.positionX,
		state.positionY,
		velocityTargetX,
		velocityTargetY,
		16);
	bolt.velocityX = velocity.first;
	bolt.velocityY = velocity.second;
	bolt.speed = 16;
	bolt.impactLifetime = 1;
	bolt.resistantDamageDivisor = 4;
	bolt.effectiveSpellLevel = controller.effectiveSpellLevel;
	return bolt;
}

ReplayFixtureProjectileState CreateReplayChainLightningController(
	ReplayFixtureExecutionState &state,
	const ReplayFixtureCommand &command,
	int32_t spellLevel)
{
	ReplayFixtureProjectileState controller;
	controller.entityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		controller.entityId = std::max(controller.entityId, projectile.entityId + 1U);
	controller.sourceEntityId = state.entityId == 0 ? 1U : state.entityId;
	controller.spellId = 16;
	controller.levelId = state.levelId;
	controller.positionX = state.positionX;
	controller.positionY = state.positionY;
	controller.targetX = command.targetX;
	controller.targetY = command.targetY;
	controller.damageType = 2;
	controller.remainingTicks = 1;
	controller.behavior = ChainLightningControllerBehavior;
	controller.startX = state.positionX;
	controller.startY = state.positionY;
	controller.originalDirection = GetDirection(
		state.positionX,
		state.positionY,
		command.targetX,
		command.targetY);
	controller.effectiveSpellLevel = spellLevel;
	state.projectiles.push_back(controller);
	return controller;
}

ReplayFixtureProjectileState CreateReplayChainLightningBolt(
	ReplayFixtureExecutionState &state,
	const ReplayFixtureProjectileState &controller,
	int32_t targetX,
	int32_t targetY)
{
	ReplayFixtureProjectileState bolt;
	bolt.sourceEntityId = controller.sourceEntityId;
	bolt.spellId = controller.spellId;
	bolt.levelId = controller.levelId;
	bolt.positionX = controller.positionX;
	bolt.positionY = controller.positionY;
	bolt.targetX = targetX;
	bolt.targetY = targetY;
	bolt.damageType = 2;
	bolt.remainingTicks = 256;
	bolt.behavior = ChainLightningBoltBehavior;
	bolt.startX = controller.positionX;
	bolt.startY = controller.positionY;
	bolt.originalDirection = GetDirection(
		controller.positionX,
		controller.positionY,
		targetX,
		targetY);
	int32_t velocityTargetX = targetX;
	int32_t velocityTargetY = targetY;
	if (velocityTargetX == controller.positionX && velocityTargetY == controller.positionY) {
		velocityTargetX += DirectionDisplacements[bolt.originalDirection].first;
		velocityTargetY += DirectionDisplacements[bolt.originalDirection].second;
	}
	const auto velocity = CalculateVelocity(
		controller.positionX,
		controller.positionY,
		velocityTargetX,
		velocityTargetY,
		32);
	bolt.velocityX = velocity.first;
	bolt.velocityY = velocity.second;
	bolt.speed = 32;
	bolt.resistantDamageDivisor = 4;
	bolt.effectiveSpellLevel = controller.effectiveSpellLevel;
	static_cast<void>(GenerateRnd(state, 8));
	return bolt;
}

ReplayFixtureProjectileState CreateReplayNovaBall(
	const ReplayFixtureProjectileState &controller,
	int32_t targetX,
	int32_t targetY)
{
	ReplayFixtureProjectileState ball;
	ball.sourceEntityId = controller.sourceEntityId;
	ball.spellId = controller.spellId;
	ball.levelId = controller.levelId;
	ball.positionX = controller.positionX;
	ball.positionY = controller.positionY;
	ball.targetX = targetX;
	ball.targetY = targetY;
	ball.damage = controller.damage;
	ball.damageType = 2;
	ball.remainingTicks = 255;
	ball.behavior = NovaBallBehavior;
	ball.startX = controller.positionX;
	ball.startY = controller.positionY;
	ball.originalDirection = GetDirection(
		controller.positionX,
		controller.positionY,
		targetX,
		targetY);
	const auto velocity = CalculateVelocity(
		controller.positionX,
		controller.positionY,
		targetX,
		targetY,
		16);
	ball.velocityX = velocity.first;
	ball.velocityY = velocity.second;
	ball.speed = 16;
	ball.resistantDamageDivisor = 4;
	ball.effectiveSpellLevel = controller.effectiveSpellLevel;
	return ball;
}

ReplayFixtureProjectileState CreateReplayFlameWaveSegment(
	ReplayFixtureExecutionState &state,
	const ReplayFixtureProjectileState &controller,
	int32_t positionX,
	int32_t positionY)
{
	ReplayFixtureProjectileState segment;
	segment.sourceEntityId = controller.sourceEntityId;
	segment.spellId = controller.spellId;
	segment.levelId = controller.levelId;
	segment.positionX = positionX;
	segment.positionY = positionY;
	segment.targetX = positionX + DirectionDisplacements[controller.originalDirection].first;
	segment.targetY = positionY + DirectionDisplacements[controller.originalDirection].second;
	segment.damage = GenerateRnd(state, 10) + static_cast<int32_t>(state.characterLevel) + 1;
	segment.damageType = 1;
	segment.remainingTicks = 255;
	segment.behavior = FlameWaveSegmentBehavior;
	segment.startX = positionX;
	segment.startY = positionY;
	segment.originalDirection = controller.originalDirection;
	const auto velocity = CalculateVelocity(
		positionX,
		positionY,
		segment.targetX,
		segment.targetY,
		16);
	segment.velocityX = velocity.first;
	segment.velocityY = velocity.second;
	segment.speed = 16;
	segment.resistantDamageDivisor = 4;
	segment.effectiveSpellLevel = controller.effectiveSpellLevel;
	return segment;
}

ReplayFixtureProjectileState CreateReplayFireWallSegment(
	ReplayFixtureExecutionState &state,
	const ReplayFixtureProjectileState &controller,
	int32_t positionX,
	int32_t positionY,
	int32_t wallHitDirection,
	bool lightningWall)
{
	ReplayFixtureProjectileState segment;
	segment.entityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		segment.entityId = std::max(segment.entityId, projectile.entityId + 1U);
	segment.sourceEntityId = controller.sourceEntityId;
	segment.spellId = controller.spellId;
	segment.levelId = controller.levelId;
	segment.positionX = positionX;
	segment.positionY = positionY;
	segment.targetX = positionX;
	segment.targetY = positionY;
	segment.damage = (GenerateRnd(state, 10) + GenerateRnd(state, 10) + 2
		+ static_cast<int32_t>(state.characterLevel))
		* (lightningWall ? 16 : 8);
	segment.damageType = lightningWall ? 2 : 1;
	segment.remainingTicks = static_cast<uint32_t>(
		(lightningWall
			? 255 + controller.effectiveSpellLevel * 255
			: 10 + controller.effectiveSpellLevel * 10
				+ static_cast<int32_t>(state.characterLevel))
		* (lightningWall ? 1 : 16));
	segment.behavior = FireWallSegmentBehavior;
	segment.startX = positionX;
	segment.startY = positionY;
	segment.originalDirection = controller.originalDirection;
	segment.resistantDamageDivisor = 4;
	segment.effectiveSpellLevel = controller.effectiveSpellLevel;
	segment.wallHitDirection = wallHitDirection;
	return segment;
}

bool ReplayWallPositionIsValid(
	const ReplayFixtureExecutionState &state,
	int32_t positionX,
	int32_t positionY)
{
	return positionX >= 0 && positionX < 40 && positionY >= 0 && positionY < 40
		&& !(state.objectEntityId != 0
			&& !state.objectDestroyed
			&& !state.objectMissilePassable
			&& state.objectLevelId == state.levelId
			&& state.objectPositionX == positionX
			&& state.objectPositionY == positionY);
}

void AdvanceReplayFireWallsOneTick(ReplayFixtureExecutionState &state)
{
	for (size_t index = 0; index < state.projectiles.size();) {
		auto &projectile = state.projectiles[index];
		if (projectile.behavior != FireWallSegmentBehavior) {
			++index;
			continue;
		}
		if (projectile.remainingTicks > 0)
			--projectile.remainingTicks;
		if (projectile.remainingTicks == 0 || !ReplayWallPositionIsValid(state, projectile.positionX, projectile.positionY))
			state.projectiles.erase(state.projectiles.begin() + static_cast<ptrdiff_t>(index));
		else
			++index;
	}
	for (size_t index = 0; index < state.projectiles.size(); ++index) {
		auto &projectile = state.projectiles[index];
		if (projectile.behavior != FireWallControllerBehavior
			&& projectile.behavior != LightningWallControllerBehavior)
			continue;
		if (projectile.remainingTicks > 0)
			--projectile.remainingTicks;
		if (projectile.remainingTicks == 0) {
			state.projectiles.erase(state.projectiles.begin() + static_cast<ptrdiff_t>(index));
			return;
		}
		std::vector<ReplayFixtureProjectileState> created;
		const bool lightningWall = projectile.behavior == LightningWallControllerBehavior;
		const auto place = [&](int32_t x, int32_t y, int32_t hitDirection) {
			if (ReplayWallPositionIsValid(state, x, y))
				created.push_back(CreateReplayFireWallSegment(state, projectile, x, y, hitDirection, lightningWall));
		};
		const auto grow = [&](int32_t &x, int32_t &y, int32_t direction, bool &stopped) {
			const int32_t rightDirection = RotateReplayDirection(direction, 1);
			const int32_t gapX = x + DirectionDisplacements[rightDirection].first - DirectionDisplacements[direction].first;
			const int32_t gapY = y + DirectionDisplacements[rightDirection].second - DirectionDisplacements[direction].second;
			if (ReplayWallPositionIsValid(state, gapX, gapY))
				place(gapX, gapY, RotateReplayDirection(direction, -2));
			if (!ReplayWallPositionIsValid(state, x, y)) {
				stopped = true;
				return;
			}
			place(x, y, -1);
			x += DirectionDisplacements[direction].first;
			y += DirectionDisplacements[direction].second;
		};
		if (projectile.wallLeftX == projectile.wallRightX
			&& projectile.wallLeftY == projectile.wallRightY) {
			place(projectile.wallLeftX, projectile.wallLeftY, -1);
			projectile.wallLeftX += DirectionDisplacements[projectile.wallLeftDirection].first;
			projectile.wallLeftY += DirectionDisplacements[projectile.wallLeftDirection].second;
			projectile.wallRightX += DirectionDisplacements[projectile.wallRightDirection].first;
			projectile.wallRightY += DirectionDisplacements[projectile.wallRightDirection].second;
		} else {
			if (!projectile.wallLeftStopped)
				grow(projectile.wallLeftX, projectile.wallLeftY, projectile.wallLeftDirection, projectile.wallLeftStopped);
			if (!projectile.wallRightStopped)
				grow(projectile.wallRightX, projectile.wallRightY, projectile.wallRightDirection, projectile.wallRightStopped);
		}
		uint32_t nextEntityId = 0x80000000U;
		for (const auto &existing : state.projectiles)
			nextEntityId = std::max(nextEntityId, existing.entityId + 1U);
	for (auto &spawned : created)
		spawned.entityId = nextEntityId++;
	for (auto &spawned : created)
		--spawned.remainingTicks;
	state.projectiles.insert(state.projectiles.end(), created.begin(), created.end());
		return;
	}
}

void AdvanceReplayRingOfFireOneTick(ReplayFixtureExecutionState &state)
{
	for (size_t index = 0; index < state.projectiles.size(); ++index) {
		auto &controller = state.projectiles[index];
		if (controller.behavior != RingOfFireControllerBehavior)
			continue;
		if (controller.remainingTicks > 0)
			--controller.remainingTicks;
		static_cast<void>(GenerateRnd(state, 10));
		static_cast<void>(GenerateRnd(state, 10));
		const auto controllerState = controller;
		if (!controller.ringOfFireLimitReached) {
			DoCrawl(3, [&](Displacement displacement) {
				const int32_t targetX = controllerState.startX + displacement.deltaX;
				const int32_t targetY = controllerState.startY + displacement.deltaY;
				if (ReplayWallPositionIsValid(state, targetX, targetY)) {
					state.projectiles.push_back(CreateReplayFireWallSegment(
						state,
						controllerState,
						targetX,
						targetY,
						-1,
						false));
				}
				return true;
			});
		}
		state.projectiles.erase(state.projectiles.begin() + static_cast<ptrdiff_t>(index));
		return;
	}
}

void AdvanceReplayNovaOneTick(ReplayFixtureExecutionState &state)
{
	for (size_t index = 0; index < state.projectiles.size(); ++index) {
		auto &controller = state.projectiles[index];
		if (controller.behavior != NovaControllerBehavior)
			continue;
		if (controller.remainingTicks > 0)
			--controller.remainingTicks;
		const auto controllerState = controller;
		const auto offsets = CreateReplayRadialProjectileOffsets(4, 36);
		std::vector<ReplayFixtureProjectileState> created;
		created.reserve(offsets.size());
		for (const auto offset : offsets)
			created.push_back(CreateReplayNovaBall(
				controllerState,
				controllerState.positionX + offset.first,
				controllerState.positionY + offset.second));
		uint32_t nextEntityId = 0x80000000U;
		for (const auto &existing : state.projectiles)
			nextEntityId = std::max(nextEntityId, existing.entityId + 1U);
		for (auto &spawned : created)
			spawned.entityId = nextEntityId++;
		state.projectiles.erase(state.projectiles.begin() + static_cast<ptrdiff_t>(index));
		state.projectiles.insert(state.projectiles.end(), created.begin(), created.end());
		return;
	}
}

void AdvanceReplayNovaBallsOneTick(ReplayFixtureExecutionState &state)
{
	for (auto iterator = state.projectiles.begin(); iterator != state.projectiles.end();) {
		auto &projectile = *iterator;
		if (projectile.behavior != NovaBallBehavior) {
			++iterator;
			continue;
		}
		if (projectile.remainingTicks > 0)
			--projectile.remainingTicks;
		MoveReplayNativeProjectile(projectile);
		if (projectile.remainingTicks == 0)
			iterator = state.projectiles.erase(iterator);
		else
			++iterator;
	}
}

void AdvanceReplayFlameWaveOneTick(ReplayFixtureExecutionState &state)
{
	for (auto iterator = state.projectiles.begin(); iterator != state.projectiles.end();) {
		auto &projectile = *iterator;
		if (projectile.behavior != FlameWaveSegmentBehavior) {
			++iterator;
			continue;
		}
		if (projectile.remainingTicks > 0)
			--projectile.remainingTicks;
		if (projectile.remainingTicks == 0) {
			iterator = state.projectiles.erase(iterator);
			continue;
		}
		MoveReplayNativeProjectile(projectile);
		if (projectile.positionX < 0 || projectile.positionX >= 40
		    || projectile.positionY < 0 || projectile.positionY >= 40
		    || !ReplayWallPositionIsValid(state, projectile.positionX, projectile.positionY)) {
			iterator = state.projectiles.erase(iterator);
			continue;
		}
		++iterator;
	}

	for (size_t index = 0; index < state.projectiles.size(); ++index) {
		auto &controller = state.projectiles[index];
		if (controller.behavior != FlameWaveControllerBehavior)
			continue;
		const auto controllerState = controller;
		const int32_t spreadDirection = GetDirection(
			controller.positionX,
			controller.positionY,
			controller.targetX,
			controller.targetY);
		const auto start = std::pair<int32_t, int32_t> {
			controller.positionX + DirectionDisplacements[spreadDirection].first,
			controller.positionY + DirectionDisplacements[spreadDirection].second,
		};
		std::vector<ReplayFixtureProjectileState> created;
		const auto place = [&](int32_t x, int32_t y) {
			if (ReplayWallPositionIsValid(state, x, y))
				created.push_back(CreateReplayFlameWaveSegment(state, controllerState, x, y));
		};
		place(start.first, start.second);
		const int32_t branchCount = 2 + controller.effectiveSpellLevel / 2;
		const auto grow = [&](int32_t &x, int32_t &y, int32_t direction) {
			const int32_t rightDirection = RotateReplayDirection(direction, 1);
			const int32_t gapX = x + DirectionDisplacements[rightDirection].first - DirectionDisplacements[direction].first;
			const int32_t gapY = y + DirectionDisplacements[rightDirection].second - DirectionDisplacements[direction].second;
			place(gapX, gapY);
			if (!ReplayWallPositionIsValid(state, x, y))
				return false;
			place(x, y);
			return true;
		};
		int32_t leftX = start.first;
		int32_t leftY = start.second;
		const int32_t leftDirection = RotateReplayDirection(spreadDirection, -2);
		for (int32_t branch = 0; branch < branchCount; ++branch) {
			leftX += DirectionDisplacements[leftDirection].first;
			leftY += DirectionDisplacements[leftDirection].second;
			if (!grow(leftX, leftY, leftDirection))
				break;
		}
		int32_t rightX = start.first;
		int32_t rightY = start.second;
		const int32_t rightDirection = RotateReplayDirection(spreadDirection, 2);
		for (int32_t branch = 0; branch < branchCount; ++branch) {
			rightX += DirectionDisplacements[rightDirection].first;
			rightY += DirectionDisplacements[rightDirection].second;
			if (!grow(rightX, rightY, rightDirection))
				break;
		}
		uint32_t nextEntityId = 0x80000000U;
		for (const auto &existing : state.projectiles)
			nextEntityId = std::max(nextEntityId, existing.entityId + 1U);
		for (auto &spawned : created)
			spawned.entityId = nextEntityId++;
		for (auto &spawned : created) {
			--spawned.remainingTicks;
			MoveReplayNativeProjectile(spawned);
		}
		state.projectiles.erase(state.projectiles.begin() + static_cast<ptrdiff_t>(index));
		state.projectiles.insert(state.projectiles.end(), created.begin(), created.end());
		return;
	}
}

void AdvanceReplayImmolationOneTick(ReplayFixtureExecutionState &state)
{
	for (size_t index = 0; index < state.projectiles.size(); ++index) {
		auto &controller = state.projectiles[index];
		if (controller.behavior != ImmolationControllerBehavior)
			continue;
		if (controller.remainingTicks > 0)
			--controller.remainingTicks;
		const auto controllerState = controller;
		const auto offsets = CreateReplayRadialProjectileOffsets(4, 36);
		std::vector<ReplayFixtureProjectileState> created;
		created.reserve(offsets.size());
		for (const auto offset : offsets)
			created.push_back(CreateReplayImmolationBall(
				controllerState,
				controllerState.positionX + offset.first,
				controllerState.positionY + offset.second));
		uint32_t nextEntityId = 0x80000000U;
		for (const auto &existing : state.projectiles)
			nextEntityId = std::max(nextEntityId, existing.entityId + 1U);
		for (auto &spawned : created)
			spawned.entityId = nextEntityId++;
		state.projectiles.erase(state.projectiles.begin() + static_cast<ptrdiff_t>(index));
		state.projectiles.insert(state.projectiles.end(), created.begin(), created.end());
		return;
	}
}

/** Advances the native Elemental projectile through retargeting and burst impact. */
void AdvanceReplayElementalOneTick(
    ReplayFixtureExecutionState &state)
{
	constexpr std::pair<int32_t, int32_t> BurstOffsets[] = {
		{ 0, 0 },
		{ -1, 1 },
		{ 1, -1 },
		{ 1, 1 },
		{ 1, 0 },
		{ 0, 1 },
		{ -1, -1 },
		{ -1, 0 },
		{ 0, -1 },
	};
	for (auto iterator = state.projectiles.begin(); iterator != state.projectiles.end();) {
		auto &projectile = *iterator;
		if (projectile.behavior != ElementalBehavior) {
			++iterator;
			continue;
		}
		if (projectile.remainingTicks > 0)
			--projectile.remainingTicks;
		if (projectile.phase == 1) {
			const int32_t startX = projectile.pathIndex == 1
			    ? projectile.elementalOriginX
			    : projectile.startX;
			const int32_t startY = projectile.pathIndex == 1
			    ? projectile.elementalOriginY
			    : projectile.startY;
			for (const auto offset : BurstOffsets) {
				const int32_t targetX = projectile.positionX + offset.first;
				const int32_t targetY = projectile.positionY + offset.second;
				if (targetX < 0 || targetX >= 40 || targetY < 0 || targetY >= 40
				    || state.combatTargetEntityId == 0
				    || state.combatTargetHitPointsQ6 <= 0
				    || state.combatTargetLevelId != projectile.levelId
				    || state.combatTargetPositionX != targetX
				    || state.combatTargetPositionY != targetY
				    || state.combatTargetFireResistance >= 100)
					continue;
				const int32_t hitChance = std::clamp(
					state.attributes[3]
					    + GetReplayBaseMagicToHit(state.characterClassId)
					    - state.combatTargetMonsterLevel * 2,
					5,
					95);
				if (!state.combatTargetPetrified
				    && std::max(GenerateRnd(state, 100), 0) >= hitChance)
					continue;
				static_cast<void>(GenerateRnd(state, 1));
				int32_t damageQ6 = projectile.damage;
				if (state.combatTargetFireResistance > 0)
					damageQ6 /= projectile.resistantDamageDivisor;
				state.combatTargetHitPointsQ6 = std::max(
					0,
					state.combatTargetHitPointsQ6 - damageQ6);
				state.combatTargetHitPoints = ToWholeCeilingQ6(
					state.combatTargetHitPointsQ6);
				if (state.combatTargetHitPointsQ6 == 0)
					DefeatReplayCombatTarget(state);
			}
			if (projectile.remainingTicks == 0)
				iterator = state.projectiles.erase(iterator);
			else
				++iterator;
			continue;
		}

		const auto visitedTiles = MoveReplayNativeProjectile(projectile);
		bool impacted = false;
		const auto resolveCollision = [&](int32_t x, int32_t y) {
			if (state.combatTargetEntityId == 0
			    || state.combatTargetHitPointsQ6 <= 0
			    || state.combatTargetLevelId != projectile.levelId
			    || state.combatTargetPositionX != x
			    || state.combatTargetPositionY != y
			    || state.combatTargetFireResistance >= 100)
				return false;
			const int32_t hitChance = std::clamp(
				state.attributes[3]
				    + GetReplayBaseMagicToHit(state.characterClassId)
				    - state.combatTargetMonsterLevel * 2,
				5,
				95);
			if (!state.combatTargetPetrified
			    && std::max(GenerateRnd(state, 100), 0) >= hitChance)
				return false;
			static_cast<void>(GenerateRnd(state, 1));
			int32_t damageQ6 = projectile.damage * 64;
			if (state.combatTargetFireResistance > 0)
				damageQ6 /= projectile.resistantDamageDivisor;
			state.combatTargetHitPointsQ6 = std::max(
				0,
				state.combatTargetHitPointsQ6 - damageQ6);
			state.combatTargetHitPoints = ToWholeCeilingQ6(
				state.combatTargetHitPointsQ6);
			if (state.combatTargetHitPointsQ6 == 0)
			DefeatReplayCombatTarget(state);
			return true;
		};
		if (visitedTiles.empty()) {
			impacted = resolveCollision(projectile.positionX, projectile.positionY);
		} else {
			for (const auto &tile : visitedTiles) {
				projectile.positionX = tile.x;
				projectile.positionY = tile.y;
				projectile.traveledX = tile.traveledX;
				projectile.traveledY = tile.traveledY;
				if (resolveCollision(tile.x, tile.y)) {
					impacted = true;
					break;
				}
			}
		}
		if (impacted) {
			EnterReplayProjectileImpact(projectile);
			++iterator;
			continue;
		}
		if (projectile.pathIndex == 0
		    && projectile.positionX == projectile.targetX
		    && projectile.positionY == projectile.targetY) {
			projectile.pathIndex = 1;
			projectile.remainingTicks = 255;
			int32_t targetX = projectile.positionX;
			int32_t targetY = projectile.positionY;
			if (state.combatTargetEntityId != 0
			    && state.combatTargetHitPointsQ6 > 0
			    && state.combatTargetLevelId == projectile.levelId
			    && std::max(
				       std::abs(state.combatTargetPositionX - projectile.positionX),
				       std::abs(state.combatTargetPositionY - projectile.positionY)) <= 19) {
				targetX = state.combatTargetPositionX;
				targetY = state.combatTargetPositionY;
			} else {
				targetX += DirectionDisplacements[state.facingDirection % 8].first;
				targetY += DirectionDisplacements[state.facingDirection % 8].second;
			}
			const int32_t direction = GetDirection(
				projectile.positionX,
				projectile.positionY,
				targetX,
				targetY);
			if (targetX == projectile.positionX && targetY == projectile.positionY) {
				targetX += DirectionDisplacements[direction].first;
				targetY += DirectionDisplacements[direction].second;
			}
			const auto velocity = CalculateVelocity(
				projectile.positionX,
				projectile.positionY,
				targetX,
				targetY,
				projectile.speed);
			projectile.targetX = targetX;
			projectile.targetY = targetY;
			projectile.originalDirection = direction;
			projectile.velocityX = velocity.first;
			projectile.velocityY = velocity.second;
			projectile.traveledX = 0;
			projectile.traveledY = 0;
		}
		if (projectile.remainingTicks == 0)
			EnterReplayProjectileImpact(projectile);
		++iterator;
	}
}

/** Advances the Guardian controller and its deterministic Firebolt admission arc. */
void AdvanceReplayGuardianOneTick(
    ReplayFixtureExecutionState &state)
{
	for (size_t index = 0; index < state.projectiles.size(); ++index) {
		auto &controller = state.projectiles[index];
		if (controller.behavior != GuardianControllerBehavior)
			continue;
		if (controller.remainingTicks > 0)
			--controller.remainingTicks;
		if (controller.guardianState > 0)
			--controller.guardianState;
		if (controller.remainingTicks == 0) {
			state.projectiles.erase(state.projectiles.begin() + static_cast<ptrdiff_t>(index));
			return;
		}
		if (controller.remainingTicks % 16 != 0)
			return;
		for (const auto offset : GuardianArc) {
			const std::pair<int32_t, int32_t> candidates[] = {
				{ offset.first, offset.second },
				{ offset.second, offset.first },
				{ offset.second, -offset.first },
				{ offset.first, -offset.second },
			};
			for (const auto candidate : candidates) {
				const int32_t targetX = controller.positionX + candidate.first;
				const int32_t targetY = controller.positionY + candidate.second;
				if (state.combatTargetEntityId == 0
				    || state.combatTargetHitPointsQ6 <= 0
				    || state.combatTargetLevelId != controller.levelId
				    || state.combatTargetPositionX != targetX
				    || state.combatTargetPositionY != targetY
				    || std::max(
					       std::abs(targetX - controller.positionX),
					       std::abs(targetY - controller.positionY)) > 6)
					continue;
				// Native GuardianTryFireAt checks line of sight and consumes one
				// discarded damage roll before AddFirebolt's payload roll.
				static_cast<void>(GenerateRnd(state, 10));
				const auto controllerState = controller;
				controller.guardianState = 3;
				state.projectiles.push_back(CreateReplayGuardianBolt(
					state,
					controllerState,
					targetX,
					targetY));
				return;
			}
		}
		return;
	}
}

/** Advances Rage's native active/cooldown controller and expiry self-damage. */
void AdvanceReplayRageOneTick(ReplayFixtureExecutionState &state)
{
	for (size_t index = 0; index < state.projectiles.size(); ++index) {
		auto &controller = state.projectiles[index];
		if (controller.behavior != RageControllerBehavior)
			continue;
		if (controller.sourceEntityId != state.entityId
		    || controller.levelId != state.levelId
		    || state.life <= 0) {
			state.projectiles.erase(state.projectiles.begin() + static_cast<ptrdiff_t>(index));
			return;
		}
		if (controller.remainingTicks > 0)
			--controller.remainingTicks;
		if (controller.remainingTicks != 0)
			return;
		if (controller.phase == 0) {
			controller.phase = 1;
			controller.remainingTicks = 245 + state.characterLevel * 2;
			return;
		}
		state.life = std::max(1, state.life - controller.damage);
		state.projectiles.erase(state.projectiles.begin() + static_cast<ptrdiff_t>(index));
		return;
	}
}

void AdvanceReplayChainLightningOneTick(ReplayFixtureExecutionState &state)
{
	for (size_t index = 0; index < state.projectiles.size(); ++index) {
		auto &controller = state.projectiles[index];
		if (controller.behavior != ChainLightningControllerBehavior)
			continue;
		if (controller.remainingTicks > 0)
			--controller.remainingTicks;
		const auto controllerState = controller;
		std::vector<std::pair<int32_t, int32_t>> targets;
		targets.emplace_back(controller.targetX, controller.targetY);
		const int32_t radius = std::min(
			controller.effectiveSpellLevel + 3,
			ChainLightningMaximumRadius);
		DoCrawl(1, radius, [&](Displacement displacement) {
			const int32_t targetX = controller.positionX + displacement.deltaX;
			const int32_t targetY = controller.positionY + displacement.deltaY;
			if (state.combatTargetEntityId != 0
			    && state.combatTargetHitPointsQ6 > 0
			    && state.combatTargetPositionX == targetX
			    && state.combatTargetPositionY == targetY)
				targets.emplace_back(targetX, targetY);
			return true;
		});
		std::vector<ReplayFixtureProjectileState> created;
		created.reserve(targets.size());
		for (const auto target : targets)
			created.push_back(CreateReplayChainLightningBolt(
				state,
				controllerState,
				target.first,
				target.second));
		uint32_t nextEntityId = 0x80000000U;
		for (const auto &existing : state.projectiles)
			nextEntityId = std::max(nextEntityId, existing.entityId + 1U);
		for (auto &spawned : created)
			spawned.entityId = nextEntityId++;
		state.projectiles.erase(state.projectiles.begin() + static_cast<ptrdiff_t>(index));
		state.projectiles.insert(state.projectiles.end(), created.begin(), created.end());
		return;
	}
}

/** Advances the compact SnakeCharge controller through travel and impact. */
void AdvanceReplaySnakeChargesOneTick(
    ReplayFixtureExecutionState &state,
    uint64_t tick)
{
	for (auto iterator = state.projectiles.begin(); iterator != state.projectiles.end();) {
		auto &projectile = *iterator;
		if (projectile.behavior != SnakeChargeBehavior) {
			++iterator;
			continue;
		}
		if (projectile.phase == 1) {
			if (projectile.remainingTicks > 0)
				--projectile.remainingTicks;
			if (projectile.remainingTicks == 0)
				iterator = state.projectiles.erase(iterator);
			else
				++iterator;
			continue;
		}
		if (state.combatTargetEntityId == 0
		    || state.combatTargetHitPointsQ6 <= 0) {
			iterator = state.projectiles.erase(iterator);
			continue;
		}
		if (projectile.remainingTicks > 0)
			--projectile.remainingTicks;
		const int32_t previousX = projectile.positionX;
		const int32_t previousY = projectile.positionY;
		const auto visitedTiles = MoveReplayNativeProjectile(projectile);
		bool removed = false;
		for (const auto &tile : visitedTiles) {
			if (tile.x < 0 || tile.x >= 40 || tile.y < 0 || tile.y >= 40
			    || (state.objectEntityId != 0
			        && state.objectLevelId == projectile.levelId
			        && state.objectPositionX == tile.x
			        && state.objectPositionY == tile.y
			        && !state.objectDestroyed
			        && !state.objectMissilePassable)) {
				projectile.positionX = previousX;
				projectile.positionY = previousY;
				iterator = state.projectiles.erase(iterator);
				removed = true;
				break;
			}
			projectile.positionX = tile.x;
			projectile.positionY = tile.y;
			projectile.traveledX = tile.traveledX;
			projectile.traveledY = tile.traveledY;
			state.combatTargetPositionX = tile.x;
			state.combatTargetPositionY = tile.y;
			state.combatTargetFuturePositionX = tile.x;
			state.combatTargetFuturePositionY = tile.y;
			if (state.life > 0
			    && state.levelId == projectile.levelId
			    && tile.x == state.positionX
			    && tile.y == state.positionY) {
				const int hitChance = 100;
				if (std::max(GenerateRnd(state, 100), 0) < hitChance) {
					const int32_t damageQ6 = std::max(1, projectile.damage) * 64;
					state.life = std::max(0, state.life - damageQ6);
					if (state.life > 0
					    && (damageQ6 >> 6) >= static_cast<int32_t>(state.characterLevel)) {
						state.combatMode = 7;
						state.combatModeEndTick = tick + 6;
					}
					EnterReplayProjectileImpact(projectile);
					break;
				}
			}
		}
		if (removed)
			continue;
		if (projectile.phase == 0 && projectile.remainingTicks == 0)
			iterator = state.projectiles.erase(iterator);
		else
			++iterator;
	}
}

bool ApplyReplayAttack(
    ReplayFixtureExecutionState &state,
    uint32_t targetEntityId,
    uint64_t,
    bool ranged = false,
    bool consumeRandom = false)
{
	if (!IsReplayAttackTargetValid(state, targetEntityId, ranged))
		return false;
	if (ranged) {
		const uint32_t arrowCount = HasReplayEquipmentFlag(state, MultipleArrowsFlag) ? 3U : 1U;
		for (uint32_t arrowIndex = 0; arrowIndex < arrowCount; ++arrowIndex)
			CreateReplayWeaponArrow(state, targetEntityId, arrowIndex, arrowCount, consumeRandom);
		return true;
	}
	// Native attacks consume the deterministic damage-range draw even for the
	// compact unarmed fallback used by replay fixtures.
	if (consumeRandom)
		static_cast<void>(GenerateRnd(state, 1));
	int32_t damage = 1;
	if (!ranged && state.characterClassId != 0
	    && state.attributes[1] > 0 && state.attributes[5] > 0) {
		const int32_t level = static_cast<int32_t>(state.characterLevel);
		const int32_t strength = state.attributes[1];
		const int32_t dexterity = state.attributes[5];
		const int32_t vitality = state.attributes[7];
		const int32_t strengthModifier = level * strength;
		const int32_t strengthDexterityModifier = level * (strength + dexterity);
		const int32_t damageModifier = state.characterClassId == 1
		        ? strengthDexterityModifier / 200
		    : state.characterClassId == 3
		        ? strengthDexterityModifier / 150
		    : state.characterClassId == 4
		        ? strengthModifier / 100
		    : state.characterClassId == 5
		        ? strengthModifier / 100 + level * vitality / 100
		        : strengthModifier / 100;
		const int32_t unarmedMaximum = state.characterClassId == 3
		    ? std::max(1, level / 2)
		    : 1;
		const int32_t rolledBaseDamage = 1 + GenerateRnd(state, unarmedMaximum);
		damage = std::max(1, rolledBaseDamage + damageModifier - std::max(0, state.combatTargetArmorClass));
	} else {
		damage = ranged ? 1 : std::max(1, 10 - state.combatTargetArmorClass);
	}
	const int32_t damageQ6 = damage * 64;
	state.combatTargetHitPointsQ6 = std::max(0, state.combatTargetHitPointsQ6 - damageQ6);
	state.combatTargetHitPoints = ToWholeCeilingQ6(state.combatTargetHitPointsQ6);
	// Resolve both channels before applying either one, matching the C# rule's
	// deterministic fire-then-lightning roll order even for a lethal fire hit.
	const int32_t fireDamage = ApplyReplayElementalResistance(
	    ResolveReplayElementalDamage(
	        state,
	        ranged ? FireArrowsFlag : FireDamageFlag,
	        &ReplayFixtureItemState::fireMinDamage,
	        &ReplayFixtureItemState::fireMaxDamage),
	    state.combatTargetFireResistance);
	const int32_t lightningDamage = ApplyReplayElementalResistance(
	    ResolveReplayElementalDamage(
	        state,
	        ranged ? LightningArrowsFlag : LightningDamageFlag,
	        &ReplayFixtureItemState::lightningMinDamage,
	        &ReplayFixtureItemState::lightningMaxDamage),
	    state.combatTargetLightningResistance);
	if (state.combatTargetHitPointsQ6 > 0) {
		state.combatTargetHitPointsQ6 = std::max(
		    0,
		    state.combatTargetHitPointsQ6 - fireDamage * 64);
		state.combatTargetHitPoints = ToWholeCeilingQ6(state.combatTargetHitPointsQ6);
	}
	if (state.combatTargetHitPointsQ6 > 0) {
		state.combatTargetHitPointsQ6 = std::max(
		    0,
		    state.combatTargetHitPointsQ6 - lightningDamage * 64);
		state.combatTargetHitPoints = ToWholeCeilingQ6(state.combatTargetHitPointsQ6);
	}
	if (state.combatTargetHitPointsQ6 <= 0)
		DefeatReplayCombatTarget(state);
	return true;
}

bool ApplyReplayCast(ReplayFixtureExecutionState &state, const ReplayFixtureCommand &command, bool applyEffects = true)
{
	const bool isAbility = state.abilitySpellId == command.spellId;
	const auto memorizedSpell = std::find_if(state.memorizedSpells.begin(), state.memorizedSpells.end(), [&](const auto &spell) {
		return spell.spellId == command.spellId && spell.level > 0;
	});
	const bool isMemorized = memorizedSpell != state.memorizedSpells.end();
	const auto beltConsumable = std::find_if(state.belt.begin(), state.belt.end(), [&](const auto &item) {
		return item.state.statFlag
		    && (item.state.miscId == 21 || item.state.miscId == 22)
		    && item.state.spellId == static_cast<int32_t>(command.spellId);
	});
	const bool isConsumable = !isAbility && !isMemorized && beltConsumable != state.belt.end();
	if (command.spellId == 33
	    && state.spellProjectileBehavior == TelekinesisBehavior
	    && (isAbility || isMemorized)
	    && command.targetEntityId == state.worldItemEntityId
	    && state.worldItemEntityId != 0
	    && state.worldItemSeed != 0
	    && state.levelId == state.worldItemLevelId
	    && command.targetX == state.worldItemPositionX
	    && command.targetY == state.worldItemPositionY) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = std::max(8, 15 - std::max(spellLevel - 1, 0) * 2) * 64;
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			const ReplayFixtureItemState pickedItemState = state.worldItemState;
			const int targetCell = CanPlaceReplayInventoryItem(
			    state.inventoryGrid,
			    0,
			    pickedItemState.inventoryWidth,
			    pickedItemState.inventoryHeight)
			    ? 0
			    : FindReplayInventoryPlacement(state, pickedItemState);
			if (targetCell >= 0 && state.inventory.size() < 40) {
				const size_t inventoryIndex = state.inventory.size();
				state.inventory.push_back({
				    0,
				    0,
				    state.worldItemSeed,
				    state.worldItemPrice,
				    command.order.targetTick,
				    pickedItemState,
				});
				FillReplayInventoryGrid(state, targetCell, inventoryIndex, pickedItemState);
			} else {
				state.heldItemDefined = true;
				state.heldItem = {
				    0,
				    0,
				    state.worldItemSeed,
				    state.worldItemPrice,
				    command.order.targetTick,
				    pickedItemState,
				};
			}
			state.worldItemEntityId = 0;
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 33
	    && state.spellProjectileBehavior == TelekinesisBehavior
	    && (isAbility || isMemorized)
	    && command.targetEntityId == state.objectEntityId
	    && state.objectEntityId != 0
	    && state.objectId != 0
	    && !state.objectActivated
	    && !state.objectDestroyed
	    && state.levelId == state.objectLevelId
	    && command.targetX == state.objectPositionX
	    && command.targetY == state.objectPositionY) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = std::max(8, 15 - std::max(spellLevel - 1, 0) * 2) * 64;
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			state.objectActivated = true;
			switch (state.objectEffectKind) {
			case 1:
				state.life = std::min(
				    state.lifeMaximum,
				    state.life + std::max(0, state.objectEffectAmount));
				break;
			case 2:
				state.life = std::max(
				    0,
				    state.life - std::max(0, state.objectEffectAmount));
				break;
			case 3:
				state.experience += static_cast<uint32_t>(
				    std::max(0, state.objectEffectAmount));
				break;
			default:
				break;
			}
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 33
	    && state.spellProjectileBehavior == TelekinesisBehavior
	    && (isAbility || isMemorized)
	    && command.targetEntityId == state.combatTargetEntityId
	    && state.combatTargetEntityId != 0
	    && state.combatTargetHitPointsQ6 > 0
	    && state.levelId == state.combatTargetLevelId
	    && command.targetX == state.combatTargetPositionX
	    && command.targetY == state.combatTargetPositionY) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = std::max(8, 15 - std::max(spellLevel - 1, 0) * 2) * 64;
		if (!isAbility && state.mana < manaCost)
			return false;
		const int32_t directionX = std::clamp(
		    state.combatTargetPositionX - state.positionX,
		    -1,
		    1);
		const int32_t directionY = std::clamp(
		    state.combatTargetPositionY - state.positionY,
		    -1,
		    1);
		const int32_t destinationX = state.combatTargetPositionX + directionX;
		const int32_t destinationY = state.combatTargetPositionY + directionY;
		if (destinationX < 0 || destinationX >= 40 || destinationY < 0 || destinationY >= 40)
			return false;
		if (applyEffects) {
			state.combatTargetPositionX = destinationX;
			state.combatTargetPositionY = destinationY;
			state.combatTargetFuturePositionX = destinationX;
			state.combatTargetFuturePositionY = destinationY;
			state.combatTargetMoving = false;
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 11
	    && state.spellProjectileBehavior == 12
	    && (isAbility || isMemorized)
	    && (command.targetEntityId == 0 || command.targetEntityId == state.entityId)) {
		constexpr int32_t ManaCostQ6 = 33 * 64;
		if (!isAbility && state.mana < ManaCostQ6)
			return false;
		if (applyEffects && !state.manaShieldActive) {
			state.manaShieldActive = true;
			if (!isAbility)
				state.mana -= ManaCostQ6;
		}
		return true;
	}
	if (command.spellId == 40
	    && state.spellProjectileBehavior == MagiBehavior
	    && (isAbility || isMemorized)
	    && (command.targetEntityId == 0 || command.targetEntityId == state.entityId)) {
		if (applyEffects)
			state.mana = state.manaMaximum;
		return true;
	}
	if (command.spellId == 43
	    && state.spellProjectileBehavior == ReflectBehavior
	    && (isAbility || isMemorized)
	    && (command.targetEntityId == 0 || command.targetEntityId == state.entityId)) {
		if (applyEffects) {
			const uint64_t spellLevel = isMemorized ? memorizedSpell->level : 2;
			const uint64_t chargeIncrement = spellLevel * state.characterLevel;
			if (chargeIncrement <= std::numeric_limits<uint16_t>::max() - state.reflectCharges)
				state.reflectCharges += static_cast<uint32_t>(chargeIncrement);
		}
		return true;
	}
	if (command.spellId == 53
	    && state.spellProjectileBehavior == EtherealizeBehavior
	    && (isAbility || isMemorized)
	    && (command.targetEntityId == 0 || command.targetEntityId == state.entityId)) {
		if (applyEffects) {
			state.statusEffects.erase(std::remove_if(
			    state.statusEffects.begin(),
			    state.statusEffects.end(),
			    [](const auto &effect) { return effect.effectId == 53; }),
			    state.statusEffects.end());
			state.statusEffects.push_back({ 53, std::numeric_limits<uint32_t>::max(), 0, EtherealizeStatusBehavior });
			if (!isAbility)
				state.mana -= 100 * 64;
		}
		return true;
	}
	if (command.spellId == 48
	    && state.spellProjectileBehavior == BerserkBehavior
	    && (isAbility || isMemorized)) {
		const bool explicitTarget = command.targetEntityId != 0;
		const bool targetMatches = explicitTarget
		    ? command.targetEntityId == state.combatTargetEntityId
		    : command.targetX == state.combatTargetPositionX
		        && command.targetY == state.combatTargetPositionY;
		const int distance = std::max(
		    std::abs(state.positionX - state.combatTargetPositionX),
		    std::abs(state.positionY - state.combatTargetPositionY));
		if (!targetMatches
		    || state.combatTargetHitPointsQ6 <= 0
		    || state.combatTargetBerserked
		    || state.combatTargetPetrified
		    || distance > 5)
			return false;
		if (!isAbility && state.mana < GetReplayBerserkManaCost(state))
			return false;
		if (state.combatTargetMagicResistance >= 100
		    || (state.combatTargetMagicResistance > 0
	        && GenerateRnd(state, 2) != 0))
			return true;
		if (applyEffects) {
			const int32_t spellLevel = GetReplayEffectiveSpellLevel(
			    state,
			    isMemorized ? &*memorizedSpell : nullptr);
			auto scaleDamage = [&](const int32_t damage) {
				return (120 + GenerateRnd(state, 10)) * damage / 100 + spellLevel;
			};
			state.combatTargetMinDamage = scaleDamage(state.combatTargetMinDamage);
			state.combatTargetMaxDamage = scaleDamage(state.combatTargetMaxDamage);
			state.combatTargetMinDamageSpecial = scaleDamage(state.combatTargetMinDamageSpecial);
			state.combatTargetMaxDamageSpecial = scaleDamage(state.combatTargetMaxDamageSpecial);
			state.combatTargetAttackDamage = state.combatTargetMaxDamage;
			state.combatTargetBerserked = true;
			if (!isAbility)
				state.mana -= GetReplayBerserkManaCost(state);
		}
		return true;
	}
	if (command.spellId == 49
	    && state.spellProjectileBehavior == 42
	    && (isAbility || isMemorized)
	    && state.warpId != 0
	    && state.levelId != 0
	    && state.levelId == state.warpSourceLevelId
	    && state.warpTargetLevelId != 0) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = GetReplayTeleportManaCost(state, spellLevel);
		if (!isAbility && state.mana < manaCost)
			return false;
		const auto destination = Crawl(0, 5, [&](Displacement displacement) -> std::optional<std::pair<int32_t, int32_t>> {
			const int32_t candidateX = state.warpTargetX + displacement.deltaX;
			const int32_t candidateY = state.warpTargetY + displacement.deltaY;
			if (candidateX < 0 || candidateX >= 40 || candidateY < 0 || candidateY >= 40)
				return {};
			if (state.warpTargetLevelId == state.warpSourceLevelId
			    && candidateX == state.warpTriggerX
			    && candidateY == state.warpTriggerY)
				return {};
			if (state.secondaryPlayerEntityId != 0
			    && state.secondaryPlayerLevelId == state.warpTargetLevelId
			    && state.secondaryPlayerLife > 0
			    && state.secondaryPlayerPositionX == candidateX
			    && state.secondaryPlayerPositionY == candidateY)
				return {};
			return std::pair<int32_t, int32_t> { candidateX, candidateY };
		});
		if (!destination)
			return false;
		if (applyEffects) {
			state.levelId = state.warpTargetLevelId;
			if (std::find(state.visitedLevelIds.begin(), state.visitedLevelIds.end(), state.levelId) == state.visitedLevelIds.end())
				state.visitedLevelIds.push_back(state.levelId);
			state.positionX = destination->first;
			state.positionY = destination->second;
			state.manaShieldActive = false;
			state.statusEffects.erase(std::remove_if(state.statusEffects.begin(), state.statusEffects.end(), [](const auto &effect) {
				return effect.behavior == 1 || effect.behavior == 2;
			}),
			    state.statusEffects.end());
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 52
	    && state.spellProjectileBehavior == 21
	    && (isAbility || isMemorized)
	    && command.targetX >= 0
	    && command.targetX < 40
	    && command.targetY >= 0
	    && command.targetY < 40) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = std::max(60, 100 - std::max(spellLevel - 1, 0) * 6) * 64;
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			// InitializeSpawnedMonster performs one bounded native health draw,
			// then preserves the two spawn seeds used by the Golem AI.
			static_cast<void>(GenerateRnd(state, 1));
			state.combatTargetMonsterItemSeed = AdvanceRandomItemSeed(state);
			state.combatTargetMonsterAiSeed = AdvanceRandomItemSeed(state);
			state.combatTargetEntityId = 0x80000000U;
			state.combatTargetMonsterId = 0;
			state.combatTargetLevelId = state.levelId;
			state.combatTargetPositionX = command.targetX;
			state.combatTargetPositionY = command.targetY;
			state.combatTargetArmorClass = 25;
			state.combatTargetMonsterLevel = static_cast<int32_t>(state.characterLevel);
			state.combatTargetAttackDamage = 20;
			state.combatTargetMinDamage = 12;
			state.combatTargetMaxDamage = 20;
			state.combatTargetMinDamageSpecial = 12;
			state.combatTargetMaxDamageSpecial = 20;
			state.combatTargetHitPointsQ6 = 2 * (320 * spellLevel + (state.manaMaximum * 64) / 3);
			state.combatTargetMaxHitPointsQ6 = state.combatTargetHitPointsQ6;
			state.combatTargetHitPoints = (state.combatTargetHitPointsQ6 + 63) / 64;
			state.combatTargetMaxHitPoints = state.combatTargetHitPoints;
			state.combatTargetPlayerMinion = true;
			state.combatTargetOwnerPlayerId = state.entityId;
			state.combatTargetDeathAnimationTicks = 12;
			state.combatTargetDeathTicksRemaining = 0;
			state.combatTargetNativeAi = "Golem";
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 47
	    && state.spellProjectileBehavior == 39
	    && (isAbility || isMemorized)
	    && command.targetX >= 0
	    && command.targetX < 40
	    && command.targetY >= 0
	    && command.targetY < 40) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = std::max(
		    15,
		    20 - std::max(spellLevel - 1, 0) * 3)
		    * 64;
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			// This compact lane covers the native Jester dispatch outcome whose
			// explicit ten-entry table resolves to Firebolt.
			if (GenerateRnd(state, 10) > 1)
				return false;
			CreateFirebolt(state, command, spellLevel);
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 39
	    && state.spellProjectileBehavior == 7
	    && (isAbility || isMemorized)
	    && state.levelId != 0
	    && command.targetX >= 0
	    && command.targetX < 80
	    && command.targetY >= 0
	    && command.targetY < 80) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = std::max(
		    18,
		    35 - std::max(spellLevel - 1, 0) * 3)
		    * 64;
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			state.portalId = 0x20000000U;
			state.portalSourceLevelId = state.levelId;
			state.portalSourceX = command.targetX;
			state.portalSourceY = command.targetY;
			state.portalDestinationLevelId = 0;
			state.portalDestinationX = 57;
			state.portalDestinationY = 40;
			state.portalRemainingTicks = 100;
			state.portalOwnerEntityId = state.entityId;
			state.portalBidirectional = true;
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 41
	    && state.spellProjectileBehavior == ManaBehavior
	    && (isAbility || isMemorized)
	    && (command.targetEntityId == 0 || command.targetEntityId == state.entityId)) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		if (applyEffects)
			state.mana = std::min(
			    state.manaMaximum,
			    state.mana + GenerateReplayMana(state, spellLevel));
		return true;
	}
	if (command.spellId == 8
	    && state.spellProjectileBehavior == 13
	    && (isAbility || isMemorized)) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = GetReplayStoneCurseManaCost(state, spellLevel);
		if (!isAbility && state.mana < manaCost)
			return false;
		if (!applyEffects)
			return true;
		const auto target = FindReplayStoneCurseTarget(
		    state,
		    command.targetX,
		    command.targetY);
		if (!target)
			return true;
		if (!isAbility)
			state.mana -= manaCost;
		if (state.combatTargetPetrified)
			return true;
		state.combatTargetPetrified = true;
		CreateReplayStoneCurse(state, *target, spellLevel);
		return true;
	}
	if (command.spellId == 9
	    && state.spellProjectileBehavior == 14
	    && (isAbility || isMemorized || isConsumable)
	    && (command.targetEntityId == 0 || command.targetEntityId == state.entityId)) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = GetReplayInfravisionManaCost(state, spellLevel);
		if (isMemorized && state.mana < manaCost)
			return false;
		if (applyEffects) {
			const uint32_t duration = GetReplayInfravisionDuration(spellLevel);
			auto status = std::find_if(state.statusEffects.begin(), state.statusEffects.end(), [](const auto &effect) {
				return effect.effectId == 9;
			});
			if (status == state.statusEffects.end()) {
				state.statusEffects.push_back({ 9, duration, 0, 1 });
			} else {
				status->remainingTicks = std::max(status->remainingTicks, duration);
				status->magnitude = 0;
				status->behavior = 1;
			}
			if (isMemorized)
				state.mana -= manaCost;
			if (isConsumable)
				state.belt.erase(beltConsumable);
		}
		return true;
	}
	if (command.spellId == 46
	    && state.spellProjectileBehavior == 18
	    && (isAbility || isMemorized || isConsumable)
	    && (command.targetEntityId == 0 || command.targetEntityId == state.entityId)) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = GetReplaySearchManaCost(state, spellLevel);
		if (isMemorized && state.mana < manaCost)
			return false;
		if (applyEffects) {
			const uint32_t duration = GetReplaySearchDuration(state, spellLevel);
			auto status = std::find_if(state.statusEffects.begin(), state.statusEffects.end(), [](const auto &effect) {
				return effect.effectId == 46;
			});
			if (status == state.statusEffects.end()) {
				state.statusEffects.push_back({ 46, duration, 0, 2 });
			} else {
				const uint32_t maximumDuration = std::numeric_limits<int32_t>::max();
				if (status->remainingTicks < maximumDuration - duration)
					status->remainingTicks += duration;
				status->magnitude = 0;
				status->behavior = 2;
			}
			if (isMemorized)
				state.mana -= manaCost;
			if (isConsumable)
				state.belt.erase(beltConsumable);
		}
		return true;
	}
	if (command.spellId == 5
	    && state.spellProjectileBehavior == 19
	    && (isAbility || isMemorized)
	    && command.targetX >= 0
	    && command.targetX < 40
	    && command.targetY >= 0
	    && command.targetY < 40) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = GetReplayHolyBoltManaCost(state, spellLevel);
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			CreateHolyBolt(state, command, spellLevel);
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 1
	    && state.spellProjectileBehavior == 2
	    && (isAbility || isMemorized)
	    && command.targetX >= 0
	    && command.targetX < 40
	    && command.targetY >= 0
	    && command.targetY < 40) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = GetReplayFireboltManaCost(state, spellLevel);
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			CreateFirebolt(state, command, spellLevel);
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 12
	    && state.spellProjectileBehavior == 16
	    && (isAbility || isMemorized)
	    && command.targetX >= 0
	    && command.targetX < 40
	    && command.targetY >= 0
	    && command.targetY < 40) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = GetReplayFireballManaCost(state, spellLevel);
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			CreateFireball(state, command, spellLevel);
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 6
	    && state.spellProjectileBehavior == FireWallControllerBehavior
	    && (isAbility || isMemorized)
	    && command.targetX >= 0
	    && command.targetX < 40
	    && command.targetY >= 0
	    && command.targetY < 40) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
			state,
			isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = std::max(
			8,
			28 - std::max(spellLevel - 1, 0) * 2)
			* 64;
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			CreateReplayFireWallController(
				state,
				command,
				spellLevel,
				6,
				FireWallControllerBehavior);
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 50
	    && state.spellProjectileBehavior == LightningWallControllerBehavior
	    && (isAbility || isMemorized)
	    && command.targetX >= 0
	    && command.targetX < 40
	    && command.targetY >= 0
	    && command.targetY < 40) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
			state,
			isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = std::max(
			18,
			35 - std::max(spellLevel - 1, 0) * 3)
			* 64;
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			CreateReplayFireWallController(
				state,
				command,
				spellLevel,
				50,
				LightningWallControllerBehavior);
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 15
	    && state.spellProjectileBehavior == FlameWaveControllerBehavior
	    && (isAbility || isMemorized)
	    && command.targetX >= 0
	    && command.targetX < 40
	    && command.targetY >= 0
	    && command.targetY < 40) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
			state,
			isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = std::max(
			20,
			35 - std::max(spellLevel - 1, 0) * 3)
			* 64;
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			CreateReplayFlameWaveController(state, command, spellLevel);
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 16
	    && state.spellProjectileBehavior == ChainLightningControllerBehavior
	    && (isAbility || isMemorized)
	    && command.targetX >= 0
	    && command.targetX < 40
	    && command.targetY >= 0
	    && command.targetY < 40) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
			state,
			isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = std::max(
			18,
			30 - std::max(spellLevel - 1, 0))
			* 64;
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			CreateReplayChainLightningController(state, command, spellLevel);
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 45
	    && state.spellProjectileBehavior == ImmolationControllerBehavior
	    && (isAbility || isMemorized)
	    && command.targetX >= 0
	    && command.targetX < 40
	    && command.targetY >= 0
	    && command.targetY < 40) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
			state,
			isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = std::max(
			35,
			60 - std::max(spellLevel - 1, 0) * 3)
			* 64;
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			state.projectiles.push_back(CreateReplayImmolationController(state, command, spellLevel));
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 31
	    && state.spellProjectileBehavior == ElementalBehavior
	    && (isAbility || isMemorized)
	    && command.targetX >= 0
	    && command.targetX < 40
	    && command.targetY >= 0
	    && command.targetY < 40) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
			state,
			isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = std::max(
			20,
			40 - std::max(spellLevel - 1, 0) * 2)
			* 64;
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			state.projectiles.push_back(CreateReplayElemental(state, command, spellLevel));
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 7
	    && state.spellProjectileBehavior == GuardianControllerBehavior
	    && (isAbility || isMemorized)
	    && command.targetX >= 0
	    && command.targetX < 40
	    && command.targetY >= 0
	    && command.targetY < 40) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
			state,
			isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = 50 * 64;
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			state.projectiles.push_back(CreateReplayGuardianController(
				state,
				command,
				spellLevel));
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 22
	    && state.spellProjectileBehavior == RageControllerBehavior
	    && (isAbility || isMemorized)
	    && (command.targetEntityId == 0 || command.targetEntityId == state.entityId)) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
			state,
			isMemorized ? &*memorizedSpell : nullptr);
		if (applyEffects
		    && state.life > static_cast<int32_t>(state.characterLevel) * 64
		    && !std::any_of(state.projectiles.begin(), state.projectiles.end(), [](const auto &projectile) {
				return projectile.behavior == RageControllerBehavior;
			})) {
			state.projectiles.push_back(CreateReplayRageController(state, spellLevel));
		}
		return true;
	}
	if (command.spellId == 21
	    && state.spellProjectileBehavior == InfernoControllerBehavior
	    && (isAbility || isMemorized)
	    && command.targetX >= 0
	    && command.targetX < 40
	    && command.targetY >= 0
	    && command.targetY < 40) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
			state,
			isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = GetReplayInfernoManaCost(state, spellLevel);
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			state.projectiles.push_back(CreateReplayInfernoController(state, command, spellLevel));
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 25
	    && state.spellProjectileBehavior == ApocalypseControllerBehavior
	    && (isAbility || isMemorized)
	    && (command.targetEntityId == 0 || command.targetEntityId == state.entityId)) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
			state,
			isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = GetReplayApocalypseManaCost(state, spellLevel);
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			state.projectiles.push_back(CreateReplayApocalypseController(state, spellLevel));
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 44
	    && state.spellProjectileBehavior == RingOfFireControllerBehavior
	    && (isAbility || isMemorized)
	    && (command.targetEntityId == 0 || command.targetEntityId == state.entityId)) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
			state,
			isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = std::max(
			8,
			28 - std::max(spellLevel - 1, 0) * 2)
			* 64;
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			CreateReplayRingOfFireController(state, spellLevel);
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 18
	    && state.spellProjectileBehavior == NovaControllerBehavior
	    && (isAbility || isMemorized)
	    && command.targetX >= 0
	    && command.targetX < 40
	    && command.targetY >= 0
	    && command.targetY < 40) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
			state,
			isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = std::max(
			35,
			60 - std::max(spellLevel - 1, 0) * 3)
			* 64;
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			const auto controller = CreateReplayNovaController(state, command, spellLevel);
			state.projectiles.push_back(controller);
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 38
	    && state.spellProjectileBehavior == 21
	    && (isAbility || isMemorized)
	    && command.targetX >= 0
	    && command.targetX < 40
	    && command.targetY >= 0
	    && command.targetY < 40) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = GetReplayBoneSpiritManaCost(state, spellLevel);
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			CreateBoneSpirit(state, command);
			state.life = std::max(0, state.life - 6 * 64);
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 1 && (isAbility || isMemorized)
	    && (isAbility || state.mana >= static_cast<int32_t>(state.spellManaCost))
	    && state.life < state.lifeMaximum) {
		if (applyEffects) {
			if (!isAbility)
				state.mana -= static_cast<int32_t>(state.spellManaCost);
			state.life = std::min(state.life + static_cast<int32_t>(state.spellHealingAmount), state.lifeMaximum);
		}
		return true;
	}
	if (command.spellId == 3
	    && state.spellProjectileBehavior == 3
	    && (isAbility || isMemorized)
	    && command.targetX >= 0
	    && command.targetX < 40
	    && command.targetY >= 0
	    && command.targetY < 40) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = GetReplayLightningManaCost(state, spellLevel);
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			CreateLightningController(state, command, spellLevel);
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 4
	    && state.spellProjectileBehavior == 5
	    && (isAbility || isMemorized)
	    && (command.targetEntityId == 0 || command.targetEntityId == state.entityId)) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = GetReplayFlashManaCost(state, spellLevel);
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			CreateFlashLayers(state, spellLevel);
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 2
	    && state.spellProjectileBehavior == 8
	    && (isAbility || isMemorized)
	    && (command.targetEntityId == 0 || command.targetEntityId == state.entityId)) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = GetReplayHealingManaCost(state, spellLevel);
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			const int32_t healing = GenerateReplayHealing(state, spellLevel, false);
			state.life += std::min(healing, state.lifeMaximum - state.life);
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 34
	    && state.spellProjectileBehavior == 17
	    && (isAbility || isMemorized)
	    && command.targetEntityId != 0
	    && command.targetEntityId != state.entityId
	    && command.targetEntityId == state.secondaryPlayerEntityId
	    && state.secondaryPlayerLife > 0
	    && state.secondaryPlayerLevelId == state.levelId
	    && std::max(
	           std::abs(state.secondaryPlayerPositionX - state.positionX),
	           std::abs(state.secondaryPlayerPositionY - state.positionY))
	        <= 112) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = GetReplayHealingManaCost(state, spellLevel);
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			const int32_t healing = GenerateReplayHealing(state, spellLevel, true);
			state.secondaryPlayerLife += std::min(
			    healing,
			    state.secondaryPlayerLifeMaximum - state.secondaryPlayerLife);
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 23
	    && state.spellProjectileBehavior == 9
	    && (isAbility || isMemorized)
	    && (command.targetEntityId == 0 || command.targetEntityId == state.entityId)) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = GetReplayTeleportManaCost(state, spellLevel);
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			const auto destination = FindReplayTeleportDestination(
			    state,
			    command.targetX,
			    command.targetY);
			if (!destination)
				return true;
			CreateReplayMovementController(state, *destination, 23, 9);
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 10
	    && state.spellProjectileBehavior == 15
	    && (isAbility || isMemorized || isConsumable)
	    && (command.targetEntityId == 0 || command.targetEntityId == state.entityId)) {
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(
		    state,
		    isMemorized ? &*memorizedSpell : nullptr);
		const int32_t manaCost = GetReplayPhasingManaCost(state, spellLevel);
		if (isMemorized && state.mana < manaCost)
			return false;
		if (applyEffects) {
			const auto destination = FindReplayPhasingDestination(state);
			if (destination)
				CreateReplayMovementController(state, *destination, 10, 15);
			if (isMemorized)
				state.mana -= manaCost;
			if (isConsumable)
				state.belt.erase(beltConsumable);
		}
		return true;
	}
	if (command.spellId == 32
	    && state.spellProjectileBehavior == 10
	    && (isAbility || isMemorized)
	    && command.targetEntityId != 0
	    && command.targetEntityId != state.entityId
	    && command.targetEntityId == state.secondaryPlayerEntityId
	    && state.secondaryPlayerLevelId == state.levelId
	    && std::max(
	           std::abs(state.secondaryPlayerPositionX - state.positionX),
	           std::abs(state.secondaryPlayerPositionY - state.positionY))
	        <= 112) {
		const int32_t manaCost = GetReplayResurrectionManaCost();
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			ApplyReplayResurrection(state);
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	if (command.spellId == 2 && state.spellStatusEffectId != 0
	    && (isAbility || isMemorized) && (isAbility || state.mana >= 3)) {
		if (applyEffects) {
			if (!isAbility)
				state.mana -= 3;
			state.statusEffects.erase(std::remove_if(state.statusEffects.begin(), state.statusEffects.end(), [&](const auto &effect) {
				return effect.effectId == state.spellStatusEffectId;
			}),
			    state.statusEffects.end());
			state.statusEffects.push_back({ state.spellStatusEffectId, state.spellStatusDuration, state.spellStatusMagnitude });
		}
		return true;
	}
	if (command.spellId == 30 && (isAbility || isMemorized)) {
		const int32_t manaCost = state.spellProjectileBehavior == 1
		    ? GetReplayChargedBoltManaCost(state)
		    : 6;
		if (!isAbility && state.mana < manaCost)
			return false;
		if (applyEffects) {
			const int32_t spellLevel = GetReplayEffectiveSpellLevel(
			    state,
			    isMemorized ? &*memorizedSpell : nullptr);
			CreateChargedBolts(state, command, spellLevel);
			if (!isAbility)
				state.mana -= manaCost;
		}
		return true;
	}
	return false;
}

bool CanStartReplayPlayerAction(const ReplayFixtureExecutionState &state, uint64_t tick)
{
	if (state.life <= 0)
		return false;
	if (state.combatMode == 0)
		return !state.currentAction.defined;
	return state.currentAction.defined
	    && state.currentAction.effectApplied
	    && tick >= state.currentAction.readyTick
	    && (state.combatMode == 4 || state.combatMode == 5 || state.combatMode == 9);
}

bool IsReplayDeathDropPositionAvailable(
    const ReplayFixtureExecutionState &state,
    int32_t candidateX,
    int32_t candidateY)
{
	if (candidateX < 0 || candidateX >= 40
	    || candidateY < 0 || candidateY >= 40) {
		return false;
	}
	if ((state.worldItemEntityId != 0
	        && state.worldItemLevelId == state.levelId
	        && state.worldItemPositionX == candidateX
	        && state.worldItemPositionY == candidateY)
	    || std::any_of(state.worldItems.begin(), state.worldItems.end(), [&](const auto &item) {
		       return item.levelId == state.levelId
		           && item.positionX == candidateX
		           && item.positionY == candidateY;
	       })) {
		return false;
	}
	if (state.secondaryPlayerEntityId != 0
	    && state.secondaryPlayerLevelId == state.levelId
	    && state.secondaryPlayerPositionX == candidateX
	    && state.secondaryPlayerPositionY == candidateY) {
		return false;
	}
	if (state.combatTargetEntityId != 0
	    && state.combatTargetHitPointsQ6 > 0
	    && (state.combatTargetLevelId == 0 || state.combatTargetLevelId == state.levelId)
	    && ((state.combatTargetPositionX == candidateX
	            && state.combatTargetPositionY == candidateY)
	        || (state.combatTargetMoving
	            && state.combatTargetFuturePositionX == candidateX
	            && state.combatTargetFuturePositionY == candidateY))) {
		return false;
	}
	if (state.objectEntityId != 0
	    && !state.objectActivated
	    && !state.objectDestroyed
	    && state.objectLevelId == state.levelId
	    && state.objectPositionX == candidateX
	    && state.objectPositionY == candidateY) {
		return false;
	}
	return true;
}

std::optional<std::pair<int32_t, int32_t>> FindReplayDeathDropPosition(
    const ReplayFixtureExecutionState &state,
    std::optional<int32_t> preferredDirection = std::nullopt)
{
	if (preferredDirection.has_value()) {
		const auto displacement = DirectionDisplacements[*preferredDirection];
		const int32_t candidateX = state.positionX + displacement.first;
		const int32_t candidateY = state.positionY + displacement.second;
		if (IsReplayDeathDropPositionAvailable(state, candidateX, candidateY))
			return std::pair<int32_t, int32_t> { candidateX, candidateY };
	}
	return Crawl(1, 50, [&](Displacement displacement) -> std::optional<std::pair<int32_t, int32_t>> {
		const int32_t candidateX = state.positionX + displacement.deltaX;
		const int32_t candidateY = state.positionY + displacement.deltaY;
		if (!IsReplayDeathDropPositionAvailable(state, candidateX, candidateY))
			return {};
		return std::pair<int32_t, int32_t> { candidateX, candidateY };
	});
}

uint32_t AllocateReplayWorldItemEntityId(const ReplayFixtureExecutionState &state)
{
	uint32_t entityId = 0x40000000U;
	const auto isOccupied = [&](uint32_t candidate) {
		return candidate == state.entityId
		    || candidate == state.secondaryPlayerEntityId
		    || candidate == state.combatTargetEntityId
		    || candidate == state.worldItemEntityId
		    || candidate == state.objectEntityId
		    || std::any_of(state.worldItems.begin(), state.worldItems.end(), [&](const auto &item) {
			       return item.entityId == candidate;
		       })
		    || std::any_of(state.projectiles.begin(), state.projectiles.end(), [&](const auto &projectile) {
			       return projectile.entityId == candidate;
		       });
	};
	while (isOccupied(entityId))
		++entityId;
	return entityId;
}

void DropReplayDeathGold(ReplayFixtureExecutionState &state)
{
	constexpr uint32_t GoldDivisor = 2;
	constexpr uint32_t MaxGold = 5000;
	constexpr int32_t GoldItemType = 11;
	constexpr int32_t GoldItemIndex = 0;
	const uint32_t droppedGold = state.gold / GoldDivisor;
	auto inventoryGold = state.inventory.end();
	for (auto item = state.inventory.begin(); item != state.inventory.end(); ++item) {
		if (item->state.itemType != GoldItemType || item->state.itemIndex != GoldItemIndex)
			continue;
		if (inventoryGold != state.inventory.end()) {
			inventoryGold = state.inventory.end();
			break;
		}
		inventoryGold = item;
	}
	const bool hasExactTopology = inventoryGold != state.inventory.end()
	    && inventoryGold->itemSeed != 0
	    && inventoryGold->price == state.gold
	    && inventoryGold->state.value == static_cast<int32_t>(state.gold)
	    && inventoryGold->state.identifiedValue == inventoryGold->state.value
	    && inventoryGold->state.statFlag
	    && state.gold <= MaxGold
	    && std::find(
	           state.inventoryGrid.begin(),
	           state.inventoryGrid.end(),
	           static_cast<int32_t>(std::distance(state.inventory.begin(), inventoryGold)))
	        != state.inventoryGrid.end();
	state.gold /= GoldDivisor;
	if (droppedGold == 0 || state.worldItemEntityId != 0)
		return;
	uint32_t itemSeed;
	do {
		itemSeed = AdvanceRandomItemSeed(state);
	} while (itemSeed == 0);
	const uint32_t stackValue = std::min(droppedGold, MaxGold);
	if (hasExactTopology) {
		inventoryGold->price -= stackValue;
		inventoryGold->state.value -= static_cast<int32_t>(stackValue);
		inventoryGold->state.identifiedValue -= static_cast<int32_t>(stackValue);
	}
	const auto position = FindReplayDeathDropPosition(state);
	if (!position)
		return;
	state.worldItemEntityId = AllocateReplayWorldItemEntityId(state);
	state.worldItemLevelId = state.levelId;
	state.worldItemPositionX = position->first;
	state.worldItemPositionY = position->second;
	state.worldItemSeed = itemSeed;
	state.worldItemPrice = stackValue;
	state.worldItemState = { };
	state.worldItemState.itemType = GoldItemType;
	state.worldItemState.value = static_cast<int32_t>(stackValue);
	state.worldItemState.identifiedValue = static_cast<int32_t>(stackValue);
	state.worldItemState.itemIndex = GoldItemIndex;
	state.worldItemState.statFlag = true;
}

void DropReplayEquipment(ReplayFixtureExecutionState &state)
{
	std::vector<ReplayFixtureSlottedItem> equipment = std::move(state.equipment);
	state.equipment.clear();
	for (uint32_t slot = 0; slot < 7; ++slot) {
		const auto item = std::find_if(equipment.begin(), equipment.end(), [&](const auto &candidate) {
			return candidate.slot == slot;
		});
		if (item == equipment.end())
			continue;
		const int32_t preferredDirection = static_cast<int32_t>(
		    (state.facingDirection + 8 - ((slot + 1) % 8)) % 8);
		const auto position = FindReplayDeathDropPosition(state, preferredDirection);
		if (!position)
			continue;
		state.worldItems.push_back({
		    AllocateReplayWorldItemEntityId(state),
		    state.levelId,
		    position->first,
		    position->second,
		    item->itemSeed,
		    static_cast<uint32_t>(std::max(0, item->state.identifiedValue)),
		    item->state,
		});
	}
}

void DropReplaySecondaryDeathGold(ReplayFixtureExecutionState &state)
{
	constexpr uint32_t GoldDivisor = 2;
	constexpr uint32_t MaxGold = 5000;
	constexpr int32_t GoldItemType = 11;
	constexpr int32_t GoldItemIndex = 0;
	const uint32_t droppedGold = state.secondaryPlayerGold / GoldDivisor;
	state.secondaryPlayerGold /= GoldDivisor;
	for (uint32_t remaining = droppedGold; remaining > 0;) {
		uint32_t itemSeed;
		do {
			itemSeed = AdvanceRandomItemSeed(state);
		} while (itemSeed == 0);
		const uint32_t stackValue = std::min(remaining, MaxGold);
		const auto position = FindReplaySecondaryDeathDropPosition(state);
		if (!position)
			return;
		ReplayFixtureItemState goldState;
		goldState.itemType = GoldItemType;
		goldState.value = static_cast<int32_t>(stackValue);
		goldState.identifiedValue = goldState.value;
		goldState.itemIndex = GoldItemIndex;
		goldState.statFlag = true;
		state.worldItems.push_back({
		    AllocateReplayWorldItemEntityId(state),
		    state.secondaryPlayerLevelId,
		    position->first,
		    position->second,
		    itemSeed,
		    stackValue,
		    goldState,
		});
		remaining -= stackValue;
	}
}

void DropReplaySecondaryEquipment(ReplayFixtureExecutionState &state)
{
	std::vector<ReplayFixtureSlottedItem> equipment = std::move(state.secondaryEquipment);
	state.secondaryEquipment.clear();
	for (uint32_t slot = 0; slot < 7; ++slot) {
		const auto item = std::find_if(equipment.begin(), equipment.end(), [&](const auto &candidate) {
			return candidate.slot == slot;
		});
		if (item == equipment.end())
			continue;
		const int32_t preferredDirection = static_cast<int32_t>(
		    (state.secondaryPlayerFacingDirection + 8 - ((slot + 1) % 8)) % 8);
		const auto position = FindReplaySecondaryDeathDropPosition(state, preferredDirection);
		if (!position)
			continue;
		state.worldItems.push_back({
		    AllocateReplayWorldItemEntityId(state),
		    state.secondaryPlayerLevelId,
		    position->first,
		    position->second,
		    item->itemSeed,
		    static_cast<uint32_t>(std::max(0, item->state.identifiedValue)),
		    item->state,
		});
	}
}

bool IsReplaySecondaryDeathDropPositionAvailable(
    const ReplayFixtureExecutionState &state,
    int32_t candidateX,
    int32_t candidateY)
{
	const uint32_t levelId = state.secondaryPlayerLevelId;
	if (candidateX < 0 || candidateX >= 40
	    || candidateY < 0 || candidateY >= 40) {
		return false;
	}
	if ((state.worldItemEntityId != 0
	        && state.worldItemLevelId == levelId
	        && state.worldItemPositionX == candidateX
	        && state.worldItemPositionY == candidateY)
	    || std::any_of(state.worldItems.begin(), state.worldItems.end(), [&](const auto &item) {
		       return item.levelId == levelId
		           && item.positionX == candidateX
		           && item.positionY == candidateY;
	       })) {
		return false;
	}
	if (state.entityId != 0
	    && state.levelId == levelId
	    && state.positionX == candidateX
	    && state.positionY == candidateY) {
		return false;
	}
	if (state.combatTargetEntityId != 0
	    && state.combatTargetHitPointsQ6 > 0
	    && (state.combatTargetLevelId == 0 || state.combatTargetLevelId == levelId)
	    && ((state.combatTargetPositionX == candidateX
	            && state.combatTargetPositionY == candidateY)
	        || (state.combatTargetMoving
	            && state.combatTargetFuturePositionX == candidateX
	            && state.combatTargetFuturePositionY == candidateY))) {
		return false;
	}
	return state.objectEntityId == 0
	    || state.objectActivated
	    || state.objectDestroyed
	    || state.objectLevelId != levelId
	    || state.objectPositionX != candidateX
	    || state.objectPositionY != candidateY;
}

std::optional<std::pair<int32_t, int32_t>> FindReplaySecondaryDeathDropPosition(
	const ReplayFixtureExecutionState &state,
	std::optional<int32_t> preferredDirection)
{
	if (preferredDirection.has_value()) {
		const auto displacement = DirectionDisplacements[*preferredDirection % 8];
		const int32_t candidateX = state.secondaryPlayerPositionX + displacement.first;
		const int32_t candidateY = state.secondaryPlayerPositionY + displacement.second;
		if (IsReplaySecondaryDeathDropPositionAvailable(state, candidateX, candidateY))
			return std::pair<int32_t, int32_t> { candidateX, candidateY };
	}
	return Crawl(1, 50, [&](Displacement displacement) -> std::optional<std::pair<int32_t, int32_t>> {
		const int32_t candidateX = state.secondaryPlayerPositionX + displacement.deltaX;
		const int32_t candidateY = state.secondaryPlayerPositionY + displacement.deltaY;
		if (!IsReplaySecondaryDeathDropPositionAvailable(state, candidateX, candidateY))
			return {};
		return std::pair<int32_t, int32_t> { candidateX, candidateY };
	});
}

void DropReplaySecondaryPvpEar(ReplayFixtureExecutionState &state)
{
	if (state.secondaryPlayerCharacterName.empty()
	    || (state.multiplayer && state.suppressMultiplayerDeathDrops)) {
		return;
	}
	uint8_t nameBytes[6] = { };
	for (size_t index = 0; index < std::min<size_t>(6, state.secondaryPlayerCharacterName.size()); ++index)
		nameBytes[index] = static_cast<uint8_t>(state.secondaryPlayerCharacterName[index]);
	const uint32_t createInfo = static_cast<uint32_t>(nameBytes[0]) << 8 | nameBytes[1];
	const uint32_t itemSeed = static_cast<uint32_t>(nameBytes[2]) << 24
	    | static_cast<uint32_t>(nameBytes[3]) << 16
	    | static_cast<uint32_t>(nameBytes[4]) << 8
	    | nameBytes[5];
	const auto isDuplicate = [&](const ReplayFixtureWorldItemState &item) {
		return item.itemSeed == itemSeed
		    && item.state.itemIndex == 23
		    && item.state.createInfo == createInfo;
	};
	if (std::any_of(state.worldItems.begin(), state.worldItems.end(), isDuplicate)
	    || (state.worldItemEntityId != 0
	        && state.worldItemSeed == itemSeed
	        && state.worldItemState.itemIndex == 23
	        && state.worldItemState.createInfo == createInfo)) {
		return;
	}
	const auto position = FindReplaySecondaryDeathDropPosition(state);
	if (!position)
		return;
	ReplayFixtureItemState ear;
	ear.createInfo = createInfo;
	ear.itemType = 0;
	ear.equipLocation = 7;
	ear.itemClass = 3;
	ear.value = static_cast<int32_t>(state.secondaryPlayerCharacterLevel);
	ear.miscId = 44;
	ear.itemIndex = 23;
	ear.baseName = "Ear of " + state.secondaryPlayerCharacterName;
	ear.identifiedName = state.secondaryPlayerCharacterName;
	ear.hasNativeCursorGraphic = true;
	ear.nativeCursorGraphic = state.secondaryPlayerCharacterClassId == 2
	    ? 19
	    : state.secondaryPlayerCharacterClassId == 0 ? 20
	                                                : 21;
	state.worldItems.push_back({
	    AllocateReplayWorldItemEntityId(state),
	    state.secondaryPlayerLevelId,
	    position->first,
	    position->second,
	    itemSeed,
	    0,
	    std::move(ear),
	});
}

void StartReplaySecondaryPlayerDeath(
	ReplayFixtureExecutionState &state,
	bool monsterOrTrapDeath)
{
	if (!(state.multiplayer && state.suppressMultiplayerDeathDrops)) {
		if (monsterOrTrapDeath) {
			DropReplaySecondaryDeathGold(state);
			DropReplaySecondaryEquipment(state);
		} else {
			DropReplaySecondaryPvpEar(state);
		}
	}
	state.secondaryPlayerCombatMode = 8;
	state.secondaryPlayerCombatModeEndTick = 0;
}

constexpr uint32_t ReplayMaxGold = 5000;

bool IsReplayGold(const ReplayFixtureInventoryItem &item)
{
	return item.state.itemType == 11 && item.state.itemIndex == 0;
}

bool HasReplayGoldTopology(const ReplayFixtureExecutionState &state)
{
	uint64_t total = 0;
	bool found = false;
	for (size_t index = 0; index < state.inventory.size(); ++index) {
		const auto &item = state.inventory[index];
		if (!IsReplayGold(item))
			continue;
		if (item.itemSeed == 0 || item.price == 0 || item.price > ReplayMaxGold
		    || item.state.value != static_cast<int32_t>(item.price)
		    || item.state.identifiedValue != item.state.value || !item.state.statFlag
		    || std::find(state.inventoryGrid.begin(), state.inventoryGrid.end(), static_cast<int32_t>(index)) == state.inventoryGrid.end()) {
			return false;
		}
		found = true;
		total += item.price;
	}
	return found ? total == state.gold : state.gold == 0 && state.inventoryGrid.size() == 40;
}

void RemoveReplayInventoryItem(
	std::vector<ReplayFixtureInventoryItem> &inventory,
	std::vector<int32_t> &inventoryGrid,
	size_t index);

void RemoveReplayInventoryItem(ReplayFixtureExecutionState &state, size_t index)
{
	RemoveReplayInventoryItem(state.inventory, state.inventoryGrid, index);
}

void RemoveReplayInventoryItem(
	std::vector<ReplayFixtureInventoryItem> &inventory,
	std::vector<int32_t> &inventoryGrid,
	size_t index)
{
	inventory.erase(inventory.begin() + static_cast<std::ptrdiff_t>(index));
	for (int32_t &cell : inventoryGrid) {
		if (cell == static_cast<int32_t>(index))
			cell = -1;
		else if (cell > static_cast<int32_t>(index))
			--cell;
	}
}

bool SpendReplayGold(ReplayFixtureExecutionState &state, uint32_t value)
{
	if (value > state.gold || !HasReplayGoldTopology(state))
		return false;
	uint32_t remaining = value;
	const auto spendPass = [&](bool skipMaximumPiles) {
		for (size_t index = 0; index < state.inventory.size() && remaining > 0;) {
			auto &item = state.inventory[index];
			if (!IsReplayGold(item) || (skipMaximumPiles && item.price == ReplayMaxGold)) {
				++index;
				continue;
			}
			if (remaining < item.price) {
				item.price -= remaining;
				item.state.value -= static_cast<int32_t>(remaining);
				item.state.identifiedValue -= static_cast<int32_t>(remaining);
				remaining = 0;
				return;
			}
			remaining -= item.price;
			RemoveReplayInventoryItem(state, index);
		}
	};
	spendPass(true);
	spendPass(false);
	if (remaining != 0)
		return false;
	state.gold -= value;
	return true;
}

bool CanPlaceReplayInventoryItem(const std::vector<int32_t> &grid, int cell, uint32_t width, uint32_t height)
{
	if (cell < 0 || static_cast<size_t>(cell) >= grid.size())
		return false;
	width = std::max(width, 1U);
	height = std::max(height, 1U);
	const int column = cell % 10;
	const int row = cell / 10;
	if (column + static_cast<int>(width) > 10)
		return false;
	for (uint32_t y = 0; y < height; ++y) {
		for (uint32_t x = 0; x < width; ++x) {
			const size_t target = static_cast<size_t>((row + static_cast<int>(y)) * 10 + column + static_cast<int>(x));
			if (target >= grid.size() || grid[target] >= 0)
				return false;
		}
	}
	return true;
}

int FindReplayInventoryPlacement(const std::vector<int32_t> &grid, const ReplayFixtureItemState &item)
{
	const uint32_t width = std::max(item.inventoryWidth, 1U);
	const uint32_t height = std::max(item.inventoryHeight, 1U);
	if (height == 1) {
		for (int cell = 30; cell <= 39 && static_cast<size_t>(cell) < grid.size(); ++cell) {
			if (CanPlaceReplayInventoryItem(grid, cell, width, height))
				return cell;
		}
		for (int x = 9; x >= 0; --x) {
			for (int y = 2; y >= 0; --y) {
				const int cell = y * 10 + x;
				if (CanPlaceReplayInventoryItem(grid, cell, width, height))
					return cell;
			}
		}
		return -1;
	}
	if (height == 2) {
		for (int x = 10 - static_cast<int>(width); x >= 0; --x) {
			for (int y = 0; y < 3; ++y) {
				const int cell = y * 10 + x;
				if (CanPlaceReplayInventoryItem(grid, cell, width, height))
					return cell;
			}
		}
		return -1;
	}
	if (width == 1 && height == 3) {
		for (int cell = 0; cell < 20; ++cell) {
			if (CanPlaceReplayInventoryItem(grid, cell, width, height))
				return cell;
		}
		return -1;
	}
	if (width == 2 && height == 3) {
		for (int cell = 0; cell < 9; ++cell) {
			if (CanPlaceReplayInventoryItem(grid, cell, width, height))
				return cell;
		}
		for (int cell = 10; cell < 19; ++cell) {
			if (CanPlaceReplayInventoryItem(grid, cell, width, height))
				return cell;
		}
	}
	return -1;
}

int FindReplayInventoryPlacement(const ReplayFixtureExecutionState &state, const ReplayFixtureItemState &item)
{
	return FindReplayInventoryPlacement(state.inventoryGrid, item);
}

int FindReplayInventoryFirstFit(const std::vector<int32_t> &grid, const ReplayFixtureItemState &item)
{
	const uint32_t width = std::max(item.inventoryWidth, 1U);
	const uint32_t height = std::max(item.inventoryHeight, 1U);
	for (int cell = 0; static_cast<size_t>(cell) < grid.size(); ++cell) {
		if (CanPlaceReplayInventoryItem(grid, cell, width, height))
			return cell;
	}
	return -1;
}

void FillReplayInventoryGrid(
	std::vector<int32_t> &grid,
	int cell,
	size_t itemIndex,
	const ReplayFixtureItemState &item)
{
	const uint32_t width = std::max(item.inventoryWidth, 1U);
	const uint32_t height = std::max(item.inventoryHeight, 1U);
	const int column = cell % 10;
	const int row = cell / 10;
	for (uint32_t y = 0; y < height; ++y) {
		for (uint32_t x = 0; x < width; ++x) {
			grid[(row + static_cast<int>(y)) * 10 + column + static_cast<int>(x)] = static_cast<int32_t>(itemIndex);
		}
	}
}

void FillReplayInventoryGrid(ReplayFixtureExecutionState &state, int cell, size_t itemIndex, const ReplayFixtureItemState &item)
{
	FillReplayInventoryGrid(state.inventoryGrid, cell, itemIndex, item);
}

std::vector<int> ReplayGoldPlacementCells(const ReplayFixtureExecutionState &state)
{
	std::vector<int> cells;
	for (int cell = std::min(39, static_cast<int>(state.inventoryGrid.size()) - 1); cell >= 30; --cell)
		cells.push_back(cell);
	for (int x = 9; x >= 0; --x) {
		for (int y = 2; y >= 0; --y) {
			const int cell = y * 10 + x;
			if (static_cast<size_t>(cell) < state.inventoryGrid.size())
				cells.push_back(cell);
		}
	}
	return cells;
}

std::vector<int> ReplayEmptyGoldCells(const ReplayFixtureExecutionState &state)
{
	std::vector<int> emptyCells;
	if (state.inventory.size() < 40) {
		const size_t availableItemSlots = 40 - state.inventory.size();
		for (const int cell : ReplayGoldPlacementCells(state)) {
			if (state.inventoryGrid[cell] == -1 && emptyCells.size() < availableItemSlots)
				emptyCells.push_back(cell);
		}
	}
	return emptyCells;
}

uint64_t ReplayGoldCapacity(const ReplayFixtureExecutionState &state)
{
	uint64_t capacity = 0;
	for (const auto &item : state.inventory) {
		if (IsReplayGold(item))
			capacity += ReplayMaxGold - item.price;
	}
	const auto emptyCells = ReplayEmptyGoldCells(state);
	capacity += emptyCells.size() * ReplayMaxGold;
	return capacity;
}

uint32_t AddReplayGoldPartial(ReplayFixtureExecutionState &state, uint32_t value)
{
	if (!HasReplayGoldTopology(state) || std::numeric_limits<uint32_t>::max() - state.gold < value)
		return value;
	const uint32_t accepted = static_cast<uint32_t>(std::min<uint64_t>(value, ReplayGoldCapacity(state)));
	const auto emptyCells = ReplayEmptyGoldCells(state);
	uint32_t remaining = accepted;

	for (auto &item : state.inventory) {
		if (!IsReplayGold(item) || item.price == ReplayMaxGold || remaining == 0)
			continue;
		const uint32_t added = std::min(remaining, ReplayMaxGold - item.price);
		item.price += added;
		item.state.value += static_cast<int32_t>(added);
		item.state.identifiedValue += static_cast<int32_t>(added);
		remaining -= added;
	}
	for (const int cell : emptyCells) {
		if (remaining == 0)
			break;
		uint32_t itemSeed;
		do {
			itemSeed = AdvanceRandomItemSeed(state);
		} while (itemSeed == 0);
		const uint32_t stackValue = std::min(remaining, ReplayMaxGold);
		ReplayFixtureItemState goldState;
		goldState.itemType = 11;
		goldState.value = static_cast<int32_t>(stackValue);
		goldState.identifiedValue = static_cast<int32_t>(stackValue);
		goldState.itemIndex = 0;
		goldState.statFlag = true;
		const size_t inventoryIndex = state.inventory.size();
		state.inventory.push_back({ 0, 0, itemSeed, stackValue, 0, goldState });
		FillReplayInventoryGrid(state, cell, inventoryIndex, goldState);
		remaining -= stackValue;
	}
	if (remaining != 0)
		return value;
	state.gold += accepted;
	return value - accepted;
}

bool AddReplayGold(ReplayFixtureExecutionState &state, uint32_t value)
{
	if (!HasReplayGoldTopology(state)
	    || std::numeric_limits<uint32_t>::max() - state.gold < value
	    || value > ReplayGoldCapacity(state)) {
		return false;
	}
	return AddReplayGoldPartial(state, value) == 0;
}

uint32_t GetReplayWalkMode(int32_t directionX, int32_t directionY)
{
	if ((directionX == 1 && directionY == 1)
	    || (directionX == 0 && directionY == 1)
	    || (directionX == 1 && directionY == 0)) {
		return 2;
	}
	if ((directionX == -1 && directionY == 0)
	    || (directionX == -1 && directionY == -1)
	    || (directionX == 0 && directionY == -1)) {
		return 1;
	}
	return 3;
}

void StartReplayPlayerDeath(
    ReplayFixtureExecutionState &state,
    const ReplayFixturePlayerActionTiming &timing,
    uint32_t sourceEntityId,
	uint64_t tick)
{
	state.currentAction = { };
	if (!(state.multiplayer && state.suppressMultiplayerDeathDrops)) {
		DropReplayDeathGold(state);
		DropReplayEquipment(state);
	}
	state.combatMode = 8;
	state.combatModeEndTick = 0;
	if (!timing.defined || timing.deathFrames == 0)
		return;
	const uint64_t endTick = tick + timing.deathFrames * 2 - 1;
	state.currentAction = {
		.defined = true,
		.kind = 4,
		.startedAtTick = tick,
		.effectTick = tick,
		.readyTick = endTick,
		.endTick = endTick,
		.effectApplied = true,
		.targetEntityId = sourceEntityId,
	};
	state.combatModeEndTick = endTick;
}

/** Advances the compact native Gargoyle wounded self-heal boundary. */
bool AdvanceReplayAutonomousGargoyleHealing(
    ReplayFixtureExecutionState &state)
{
	if (state.combatTargetEntityId == 0
	    || state.combatTargetNativeAi != "Gargoyle"
	    || state.combatTargetHitPointsQ6 <= 0
	    || state.combatTargetAggroRange <= 0)
		return false;
	struct Victim {
		uint32_t entityId;
		int32_t positionX;
		int32_t positionY;
	};
	std::vector<Victim> victims;
	if (state.life > 0 && state.levelId == state.combatTargetLevelId)
		victims.push_back({ state.entityId == 0 ? 1U : state.entityId, state.positionX, state.positionY });
	if (state.secondaryPlayerEntityId != 0
	    && state.secondaryPlayerLife > 0
	    && state.secondaryPlayerLevelId == state.combatTargetLevelId)
		victims.push_back({ state.secondaryPlayerEntityId, state.secondaryPlayerPositionX, state.secondaryPlayerPositionY });
	if (victims.empty())
		return false;
	const auto distanceFromMonster = [&](const Victim &victim) {
		return std::max(
		    std::abs(victim.positionX - state.combatTargetPositionX),
		    std::abs(victim.positionY - state.combatTargetPositionY));
	};
	std::sort(victims.begin(), victims.end(), [&](const Victim &left, const Victim &right) {
		const int leftDistance = distanceFromMonster(left);
		const int rightDistance = distanceFromMonster(right);
		return leftDistance != rightDistance
		    ? leftDistance < rightDistance
		    : left.entityId < right.entityId;
	});
	const int distance = distanceFromMonster(victims.front());
	const int safeDistance = 2 + state.combatTargetIntelligence;
	if (state.combatTargetGargoyleSpecialActive) {
		if (distance < safeDistance)
			state.combatTargetGargoyleSpecialActive = false;
		return true;
	}
	if (state.combatTargetHitPointsQ6
	    >= state.combatTargetMaxHitPointsQ6 * 50 / 100)
		return false;
	if (distance < safeDistance)
		return false;
	const int healRoll = std::max(GenerateRnd(state, 5), 0);
	const int divisor = 16 * (healRoll + 4);
	const int healingQ6 = (state.combatTargetMaxHitPoints / divisor) * 64;
	state.combatTargetHitPointsQ6 = std::min(
	    state.combatTargetMaxHitPointsQ6,
	    state.combatTargetHitPointsQ6 + healingQ6);
	state.combatTargetHitPoints = ToWholeCeilingQ6(state.combatTargetHitPointsQ6);
	state.combatTargetGargoyleSpecialActive =
	    state.combatTargetHitPointsQ6 < state.combatTargetMaxHitPointsQ6;
	return true;
}

void AdvanceReplayMonsterAttack(
    ReplayFixtureExecutionState &state,
    const ReplayFixturePlayerActionTiming &timing,
    uint64_t tick)
{
	if (state.combatTargetMoving && !state.combatTargetPetrified) {
		state.combatTargetPositionX = state.combatTargetFuturePositionX;
		state.combatTargetPositionY = state.combatTargetFuturePositionY;
		state.combatTargetMoving = false;
	}
	const bool primaryAlive = state.life > 0
	    && state.combatMode != 10
	    && state.combatMode != 11
	    && state.levelId == state.combatTargetLevelId;
	const bool secondaryAlive = state.secondaryPlayerEntityId != 0
	    && state.secondaryPlayerLife > 0
	    && state.secondaryPlayerCombatMode != 10
	    && state.secondaryPlayerCombatMode != 11
	    && state.secondaryPlayerLevelId == state.combatTargetLevelId;
	if ((!primaryAlive && !secondaryAlive)
	    || state.combatTargetHitPointsQ6 <= 0
	    || state.combatTargetAggroRange <= 0
	    || state.combatTargetPetrified) {
		return;
	}
	// Compact the native Counselor controller's server-owned adjacent delay
	// and wounded retreat lanes. The ranged missile branch remains outside this
	// replay projection, but these stateful decisions are shared by both
	// authoritative runners and must suppress generic melee fallback.
	if (state.combatTargetNativeAi == "Counselor"
	    || state.combatTargetNativeAi == "Zhar"
	    || state.combatTargetNativeAi == "Lazarus") {
		if (state.combatTargetCounselorDelayTicks > 0) {
			--state.combatTargetCounselorDelayTicks;
			return;
		}
		static_cast<void>(GenerateRnd(state, 100));
		const uint32_t primaryEntityId = state.entityId == 0 ? 1U : state.entityId;
		const int primaryDistance = primaryAlive
		    ? std::max(
		          std::abs(state.positionX - state.combatTargetPositionX),
		          std::abs(state.positionY - state.combatTargetPositionY))
		    : std::numeric_limits<int>::max();
		const int secondaryDistance = secondaryAlive
		    ? std::max(
		          std::abs(state.secondaryPlayerPositionX - state.combatTargetPositionX),
		          std::abs(state.secondaryPlayerPositionY - state.combatTargetPositionY))
		    : std::numeric_limits<int>::max();
		const bool counselorPrimary = primaryAlive
		    && (!secondaryAlive
		        || primaryDistance < secondaryDistance
		        || (primaryDistance == secondaryDistance
		            && primaryEntityId < state.secondaryPlayerEntityId));
		const uint32_t victimEntityId = counselorPrimary
		    ? primaryEntityId
		    : state.secondaryPlayerEntityId;
		const int32_t victimX = counselorPrimary
		    ? state.positionX
		    : state.secondaryPlayerPositionX;
		const int32_t victimY = counselorPrimary
		    ? state.positionY
		    : state.secondaryPlayerPositionY;
		const int distance = std::max(
		    std::abs(victimX - state.combatTargetPositionX),
		    std::abs(victimY - state.combatTargetPositionY));
		if (state.combatTargetCounselorRetreatTicks != 0) {
			const int towardDirection = GetDirection(
			    state.combatTargetPositionX,
			    state.combatTargetPositionY,
			    victimX,
			    victimY);
			const int awayDirection = (towardDirection + 4) % 8;
			const uint32_t retreatSeed = static_cast<uint32_t>(tick)
			    ^ state.combatTargetEntityId
			    ^ victimEntityId;
			const int firstTurn = (retreatSeed / 100U) % 2U == 0 ? 1 : -1;
			const int secondTurn = (retreatSeed / 200U) % 2U == 0 ? 2 : -2;
			const int candidateDirections[] = {
				awayDirection,
				(awayDirection + firstTurn + 8) % 8,
				(awayDirection - firstTurn + 8) % 8,
				(awayDirection + secondTurn + 8) % 8,
				(awayDirection - secondTurn + 8) % 8,
			};
			for (const int candidateDirection : candidateDirections) {
				const auto displacement = DirectionDisplacements[candidateDirection];
				const int candidateX = state.combatTargetPositionX + displacement.first;
				const int candidateY = state.combatTargetPositionY + displacement.second;
				if (candidateX < 0 || candidateX >= 40
				    || candidateY < 0 || candidateY >= 40
				    || (candidateX == state.positionX && candidateY == state.positionY)
				    || (state.secondaryPlayerEntityId != 0
				        && candidateX == state.secondaryPlayerPositionX
				        && candidateY == state.secondaryPlayerPositionY)
				    || (state.objectEntityId != 0
				        && !state.objectActivated
				        && !state.objectDestroyed
				        && !state.objectMissilePassable
				        && state.objectLevelId == state.combatTargetLevelId
				        && candidateX == state.objectPositionX
				        && candidateY == state.objectPositionY))
					continue;
				state.combatTargetMoving = true;
				state.combatTargetFuturePositionX = candidateX;
				state.combatTargetFuturePositionY = candidateY;
				state.combatTargetDirection = candidateDirection;
				break;
			}
			--state.combatTargetCounselorRetreatTicks;
			return;
		}
		if (distance <= 1) {
			if (state.combatTargetHitPoints * 2 < state.combatTargetMaxHitPoints) {
				state.combatTargetCounselorRetreatTicks = 4;
				return;
			}
			const uint32_t adjacentAttackSeed = static_cast<uint32_t>(tick)
			    ^ state.combatTargetEntityId
			    ^ victimEntityId;
			const int adjacentAttackChance = std::min(
			    100,
			    20 + 2 * state.combatTargetIntelligence);
			if (static_cast<int>(adjacentAttackSeed % 100U) >= adjacentAttackChance) {
				state.combatTargetCounselorDelayTicks = std::max(
				    0,
				    10 - 2 * state.combatTargetIntelligence
				        + static_cast<int>((adjacentAttackSeed / 200U) % 10U));
			}
			return;
		}
		return;
	}
	// Mega owns its ranged Inferno admission below; do not let the generic
	// melee projection pre-move the controller source before that branch.
	if (state.combatTargetNativeAi == "Mega")
		return;
	// Compact the native Scavenger corpse-eating goal. The replay boundary
	// carries authored corpse tiles so movement, healing, and selected-corpse
	// state remain visible in the same canonical projection as managed state.
	if (state.combatTargetNativeAi == "Scavenger") {
		if (state.combatTargetScavengerHealingTicks == 0
		    && state.combatTargetHitPointsQ6 * 100
		        >= state.combatTargetMaxHitPointsQ6 * 50)
			return;
		if (state.combatTargetScavengerHealingTicks == 0) {
			state.combatTargetScavengerHealingTicks = 10;
			state.combatTargetScavengerCorpseEntityId = 0;
		}
		ReplayFixtureCorpseState *corpse = nullptr;
		if (state.combatTargetScavengerCorpseEntityId != 0) {
			const auto selected = std::find_if(
			    state.corpses.begin(),
			    state.corpses.end(),
			    [&](auto &candidate) {
				    return candidate.entityId == state.combatTargetScavengerCorpseEntityId;
			    });
			if (selected != state.corpses.end())
				corpse = &*selected;
		}
		if (corpse == nullptr) {
			// Native ScavengerFindCorpse consumes one gameplay random value to
			// choose the direction of its bounded square scan. Preserve that
			// order so multiple authored corpses select the same candidate as
			// the managed authoritative controller.
			const bool reverseSearch = GenerateRnd(state, 2) == 0;
			const int first = reverseSearch ? 4 : -4;
			const int last = reverseSearch ? -4 : 4;
			const int increment = reverseSearch ? -1 : 1;
			for (int y = first; reverseSearch ? y >= last : y <= last; y += increment) {
				for (int x = first; reverseSearch ? x >= last : x <= last; x += increment) {
					const int candidateX = state.combatTargetPositionX + x;
					const int candidateY = state.combatTargetPositionY + y;
					const auto selected = std::find_if(
					    state.corpses.begin(),
					    state.corpses.end(),
					    [&](auto &candidate) {
						    return (candidate.levelId == 0
						            || candidate.levelId == state.combatTargetLevelId)
						        && candidate.positionX == candidateX
						        && candidate.positionY == candidateY;
					    });
					if (selected != state.corpses.end()) {
						state.combatTargetScavengerCorpseEntityId = selected->entityId;
						corpse = &*selected;
						break;
					}
				}
				if (corpse != nullptr)
					break;
			}
		}
		if (corpse == nullptr) {
			--state.combatTargetScavengerHealingTicks;
			if (state.combatTargetScavengerHealingTicks == 0)
				state.combatTargetScavengerCorpseEntityId = 0;
			return;
		}
		if (state.combatTargetPositionX == corpse->positionX
		    && state.combatTargetPositionY == corpse->positionY) {
			const int32_t healingQ6 = state.hellfire
			    ? state.combatTargetMaxHitPointsQ6 / 8
			    : 64;
			state.combatTargetHitPointsQ6 = std::min(
			    state.combatTargetMaxHitPointsQ6,
			    state.combatTargetHitPointsQ6 + healingQ6);
			state.combatTargetHitPoints = ToWholeCeilingQ6(state.combatTargetHitPointsQ6);
			--state.combatTargetScavengerHealingTicks;
			const bool reachedHealingTarget = state.hellfire
			    ? state.combatTargetHitPointsQ6 == state.combatTargetMaxHitPointsQ6
			    : state.combatTargetHitPointsQ6
			        >= state.combatTargetMaxHitPointsQ6 * 75 / 100;
			if (state.hellfire
			    && (reachedHealingTarget || state.combatTargetScavengerHealingTicks == 0)) {
				const uint32_t corpseEntityId = corpse->entityId;
				state.corpses.erase(
				    std::remove_if(
				        state.corpses.begin(),
				        state.corpses.end(),
				        [&](const auto &candidate) {
					        return candidate.entityId == corpseEntityId;
				        }),
				    state.corpses.end());
			}
			if (reachedHealingTarget || state.combatTargetScavengerHealingTicks == 0) {
				state.combatTargetScavengerHealingTicks = 0;
				state.combatTargetScavengerCorpseEntityId = 0;
			}
			return;
		}
		const int deltaX = (corpse->positionX > state.combatTargetPositionX)
		    - (corpse->positionX < state.combatTargetPositionX);
		const int deltaY = (corpse->positionY > state.combatTargetPositionY)
		    - (corpse->positionY < state.combatTargetPositionY);
		const int candidateX = state.combatTargetPositionX + deltaX;
		const int candidateY = state.combatTargetPositionY + deltaY;
		if (std::max(
		        std::abs(candidateX - state.combatTargetPositionX),
		        std::abs(candidateY - state.combatTargetPositionY)) == 1) {
			state.combatTargetMoving = true;
			state.combatTargetFuturePositionX = candidateX;
			state.combatTargetFuturePositionY = candidateY;
			state.combatTargetDirection = GetDirection(
			    state.combatTargetPositionX,
			    state.combatTargetPositionY,
			    candidateX,
			    candidateY);
		}
		--state.combatTargetScavengerHealingTicks;
		if (state.combatTargetScavengerHealingTicks == 0) {
			state.combatTargetScavengerCorpseEntityId = 0;
			state.combatTargetMoving = false;
			state.combatTargetFuturePositionX = state.combatTargetPositionX;
			state.combatTargetFuturePositionY = state.combatTargetPositionY;
		}
		return;
	}
	// Autonomous ranged identities own their movement and projectile admission
	// below; the generic compact monster branch must not pursue them into melee.
	// GoatRanged still performs its close-range retreat before considering a shot.
	if (state.combatTargetNativeAi == "GoatRanged") {
		struct Victim {
			uint32_t entityId;
			int32_t positionX;
			int32_t positionY;
		};
		std::vector<Victim> victims;
		if (primaryAlive)
			victims.push_back({ state.entityId == 0 ? 1U : state.entityId, state.positionX, state.positionY });
		if (secondaryAlive)
			victims.push_back({ state.secondaryPlayerEntityId, state.secondaryPlayerPositionX, state.secondaryPlayerPositionY });
		if (victims.empty())
			return;
		const auto victimDistance = [&](const Victim &victim) {
			return std::max(
				std::abs(victim.positionX - state.combatTargetPositionX),
				std::abs(victim.positionY - state.combatTargetPositionY));
		};
		std::sort(victims.begin(), victims.end(), [&](const Victim &left, const Victim &right) {
			const int leftDistance = victimDistance(left);
			const int rightDistance = victimDistance(right);
			return leftDistance != rightDistance
			    ? leftDistance < rightDistance
			    : left.entityId < right.entityId;
		});
		const Victim &victim = victims.front();
		const int distance = victimDistance(victim);
		const int retreatMinimumDistance = state.combatTargetRangedRetreatMinimumDistance > 0
		    ? state.combatTargetRangedRetreatMinimumDistance
		    : 4;
		const int retreatChanceBase = state.combatTargetRangedRetreatChanceBase > 0
		    ? state.combatTargetRangedRetreatChanceBase
		    : 70;
		const int retreatChanceIntelligenceScale = state.combatTargetRangedRetreatChanceIntelligenceScale > 0
		    ? state.combatTargetRangedRetreatChanceIntelligenceScale
		    : 10;
		if (distance < retreatMinimumDistance) {
			const uint32_t attackSeed = static_cast<uint32_t>(tick)
			    ^ state.combatTargetEntityId
			    ^ victim.entityId;
			const int retreatChance = std::min(
				100,
				retreatChanceBase + retreatChanceIntelligenceScale * state.combatTargetIntelligence);
			const bool usesNativeAiStream = state.combatTargetMonsterAiSeed != 0;
			const int retreatRoll = usesNativeAiStream
			    ? std::max(GenerateRnd(state, 100), 0)
			    : static_cast<int>(attackSeed % 100U);
			if (retreatRoll < retreatChance) {
				if (usesNativeAiStream) {
					const int towardDirection = GetDirection(
						state.combatTargetPositionX,
						state.combatTargetPositionY,
						victim.positionX,
						victim.positionY);
					const int awayDirection = (towardDirection + 4) % 8;
					const int firstTurn = std::max(GenerateRnd(state, 2), 0) == 0 ? 1 : -1;
					const int secondTurn = std::max(GenerateRnd(state, 2), 0) == 0 ? 2 : -2;
					const int candidateDirections[] = {
						awayDirection,
						(awayDirection + firstTurn + 8) % 8,
						(awayDirection - firstTurn + 8) % 8,
						(awayDirection + secondTurn + 8) % 8,
						(awayDirection - secondTurn + 8) % 8,
					};
					for (const int candidateDirection : candidateDirections) {
						const auto displacement = DirectionDisplacements[candidateDirection];
						const int candidateX = state.combatTargetPositionX + displacement.first;
						const int candidateY = state.combatTargetPositionY + displacement.second;
						if (candidateX < 0 || candidateX >= 40
						    || candidateY < 0 || candidateY >= 40
						    || (candidateX == state.positionX && candidateY == state.positionY)
						    || (state.secondaryPlayerEntityId != 0
						        && candidateX == state.secondaryPlayerPositionX
						        && candidateY == state.secondaryPlayerPositionY)
						    || (state.objectEntityId != 0
						        && !state.objectActivated
						        && !state.objectDestroyed
						        && !state.objectMissilePassable
						        && state.objectLevelId == state.combatTargetLevelId
						        && candidateX == state.objectPositionX
						        && candidateY == state.objectPositionY))
							continue;
						state.combatTargetMoving = true;
						state.combatTargetFuturePositionX = candidateX;
						state.combatTargetFuturePositionY = candidateY;
						state.combatTargetDirection = candidateDirection;
						break;
					}
				} else {
					std::vector<std::pair<int32_t, int32_t>> destinations;
					for (int32_t offsetY = -1; offsetY <= 1; ++offsetY) {
						for (int32_t offsetX = -1; offsetX <= 1; ++offsetX) {
							if (offsetX == 0 && offsetY == 0)
								continue;
							const int32_t candidateX = state.combatTargetPositionX + offsetX;
							const int32_t candidateY = state.combatTargetPositionY + offsetY;
							if (candidateX < 0 || candidateX >= 40
							    || candidateY < 0 || candidateY >= 40
							    || (candidateX == state.positionX && candidateY == state.positionY)
							    || (state.secondaryPlayerEntityId != 0
							        && candidateX == state.secondaryPlayerPositionX
							        && candidateY == state.secondaryPlayerPositionY)
							    || (state.objectEntityId != 0
							        && !state.objectActivated
							        && !state.objectDestroyed
							        && !state.objectMissilePassable
							        && state.objectLevelId == state.combatTargetLevelId
							        && candidateX == state.objectPositionX
							        && candidateY == state.objectPositionY))
								continue;
							const int candidateDistance = std::max(
								std::abs(candidateX - victim.positionX),
								std::abs(candidateY - victim.positionY));
							if (candidateDistance >= distance)
								destinations.emplace_back(candidateX, candidateY);
						}
					}
					std::sort(destinations.begin(), destinations.end(), [&](const auto &left, const auto &right) {
						const int leftDistance = std::max(
							std::abs(left.first - victim.positionX),
							std::abs(left.second - victim.positionY));
						const int rightDistance = std::max(
							std::abs(right.first - victim.positionX),
							std::abs(right.second - victim.positionY));
						return leftDistance != rightDistance
						    ? leftDistance > rightDistance
						    : left.second != right.second
						        ? left.second < right.second
						        : left.first < right.first;
					});
					if (!destinations.empty()) {
						state.combatTargetMoving = true;
						state.combatTargetFuturePositionX = destinations.front().first;
						state.combatTargetFuturePositionY = destinations.front().second;
						state.combatTargetDirection = GetDirection(
							state.combatTargetPositionX,
							state.combatTargetPositionY,
							state.combatTargetFuturePositionX,
							state.combatTargetFuturePositionY);
					}
				}
			}
		}
		return;
	}
	if (state.combatTargetNativeAi == "SkeletonRanged") {
		struct Victim {
			uint32_t entityId;
			int32_t positionX;
			int32_t positionY;
		};
		std::vector<Victim> victims;
		if (primaryAlive)
			victims.push_back({ state.entityId == 0 ? 1U : state.entityId, state.positionX, state.positionY });
		if (secondaryAlive)
			victims.push_back({ state.secondaryPlayerEntityId, state.secondaryPlayerPositionX, state.secondaryPlayerPositionY });
		if (victims.empty())
			return;
		const auto victimDistance = [&](const Victim &victim) {
			return std::max(
				std::abs(victim.positionX - state.combatTargetPositionX),
				std::abs(victim.positionY - state.combatTargetPositionY));
		};
		std::sort(victims.begin(), victims.end(), [&](const Victim &left, const Victim &right) {
			const int leftDistance = victimDistance(left);
			const int rightDistance = victimDistance(right);
			return leftDistance != rightDistance
			    ? leftDistance < rightDistance
			    : left.entityId < right.entityId;
		});
		const Victim &victim = victims.front();
		const int distance = victimDistance(victim);
		if (distance < 4) {
			const uint32_t actionSeed = static_cast<uint32_t>(tick)
			    ^ state.combatTargetEntityId
			    ^ victim.entityId;
			const int movementChance = std::min(
				100,
				(state.combatTargetSkeletonRangedMovementIdleTicks == -1
				        || state.combatTargetSkeletonRangedMovementIdleTicks > 20
				    ? 13
				    : state.combatTargetSkeletonRangedMovementIdleTicks == 0
				        && state.combatTargetSkeletonRangedMovementMode >= 1
				        && state.combatTargetSkeletonRangedMovementMode <= 3
				        ? 63
				        : 0)
				    + 2 * state.combatTargetIntelligence);
			const int movementRoll = state.combatTargetMonsterAiSeed != 0
			    ? std::max(GenerateRnd(state, 100), 0)
			    : static_cast<int>((actionSeed / 100U) % 100U);
			if (movementRoll < movementChance) {
				std::vector<std::pair<int32_t, int32_t>> destinations;
				for (int32_t offsetY = -1; offsetY <= 1; ++offsetY) {
					for (int32_t offsetX = -1; offsetX <= 1; ++offsetX) {
						if (offsetX == 0 && offsetY == 0)
							continue;
						const int32_t candidateX = state.combatTargetPositionX + offsetX;
						const int32_t candidateY = state.combatTargetPositionY + offsetY;
						if (candidateX < 0 || candidateX >= 40
						    || candidateY < 0 || candidateY >= 40
						    || (candidateX == state.positionX && candidateY == state.positionY)
						    || (state.secondaryPlayerEntityId != 0
						        && candidateX == state.secondaryPlayerPositionX
						        && candidateY == state.secondaryPlayerPositionY)
						    || (state.objectEntityId != 0
						        && !state.objectActivated
						        && !state.objectDestroyed
						        && !state.objectMissilePassable
						        && state.objectLevelId == state.combatTargetLevelId
						        && candidateX == state.objectPositionX
						        && candidateY == state.objectPositionY))
							continue;
						const int candidateDistance = std::max(
							std::abs(candidateX - victim.positionX),
							std::abs(candidateY - victim.positionY));
						if (candidateDistance >= distance)
							destinations.emplace_back(candidateX, candidateY);
					}
				}
				std::sort(destinations.begin(), destinations.end(), [&](const auto &left, const auto &right) {
					const int leftDistance = std::max(
						std::abs(left.first - victim.positionX),
						std::abs(left.second - victim.positionY));
					const int rightDistance = std::max(
						std::abs(right.first - victim.positionX),
						std::abs(right.second - victim.positionY));
					return leftDistance != rightDistance
					    ? leftDistance > rightDistance
					    : left.second != right.second
					        ? left.second < right.second
					        : left.first < right.first;
				});
				if (!destinations.empty()) {
					state.combatTargetMoving = true;
					state.combatTargetFuturePositionX = destinations.front().first;
					state.combatTargetFuturePositionY = destinations.front().second;
					state.combatTargetDirection = GetDirection(
						state.combatTargetPositionX,
						state.combatTargetPositionY,
						state.combatTargetFuturePositionX,
						state.combatTargetFuturePositionY);
					state.combatTargetSkeletonRangedMovementMode =
						state.combatTargetFuturePositionX - state.combatTargetPositionX;
					state.combatTargetSkeletonRangedMovementIdleTicks =
						state.combatTargetFuturePositionY - state.combatTargetPositionY;
					return;
				}
			}
			if (state.combatTargetSkeletonRangedMovementIdleTicks < 1024)
				++state.combatTargetSkeletonRangedMovementIdleTicks;
		}
		return;
	}
	if (state.combatTargetNativeAi == "SkeletonRanged"
	    || state.combatTargetNativeAi == "HorkDemon"
	    || state.combatTargetNativeAi == "Acid"
	    || state.combatTargetNativeAi == "AcidUnique"
	    || state.combatTargetNativeAi == "Succubus"
	    || state.combatTargetNativeAi == "LazarusSuccubus"
	    || state.combatTargetNativeAi == "FireBat"
	    || state.combatTargetNativeAi == "Torchant"
	    || state.combatTargetNativeAi == "Magma"
	    || state.combatTargetNativeAi == "Storm"
	    || state.combatTargetNativeAi == "Lich"
	    || state.combatTargetNativeAi == "ArchLich"
	    || state.combatTargetNativeAi == "Psychorb"
	    || state.combatTargetNativeAi == "Necromorb"
	    || state.combatTargetNativeAi == "BoneDemon")
		return;
	// Lachdanan exposes quest dialogue and FireMan has no registered native
	// controller. Neither identity may fall through to generic combat.
	if (state.combatTargetNativeAi == "Lachdanan"
	    || state.combatTargetNativeAi == "FireMan")
		return;
	if (state.combatTargetAutonomousActionHandled)
		return;
	struct Victim {
		uint32_t entityId;
		int32_t positionX;
		int32_t positionY;
		bool secondary;
	};
	std::vector<Victim> victims;
	if (primaryAlive)
		victims.push_back({ state.entityId == 0 ? 1U : state.entityId, state.positionX, state.positionY, false });
	if (secondaryAlive)
		victims.push_back({ state.secondaryPlayerEntityId, state.secondaryPlayerPositionX, state.secondaryPlayerPositionY, true });
	const auto victimDistance = [&](const Victim &victim) {
		return std::max(
			std::abs(victim.positionX - state.combatTargetPositionX),
			std::abs(victim.positionY - state.combatTargetPositionY));
	};
	std::sort(victims.begin(), victims.end(), [&](const Victim &left, const Victim &right) {
		const int leftDistance = victimDistance(left);
		const int rightDistance = victimDistance(right);
		return leftDistance != rightDistance
		    ? leftDistance < rightDistance
		    : left.entityId < right.entityId;
	});
	const Victim &victim = victims.front();
	const int distance = victimDistance(victim);
	if (distance > state.combatTargetAggroRange)
		return;
	// Compact the native Sneak visibility controller. Zero-seed fixtures use
	// the compatibility path, so conceal/reveal transitions do not consume RNG.
	if (state.combatTargetNativeAi == "Sneak") {
		const int preferredDistance = std::max(
		    1,
		    5 - state.combatTargetIntelligence);
		if (state.combatTargetSneakHidden) {
			if (distance < preferredDistance)
				state.combatTargetSneakHidden = false;
			return;
		}
		if (distance >= preferredDistance + 1) {
			state.combatTargetSneakHidden = true;
			return;
		}
	}
	// Compact the native Bat retreat countdown. The managed compatibility
	// controller rotates once away from the initial direction, then alternates
	// one-sided turns while the retreat state remains active.
	if (state.combatTargetNativeAi == "Bat"
	    && state.combatTargetBatRetreatTicks != 0) {
		const int direction = state.combatTargetBatRetreatTicks == 2
		    ? (state.combatTargetBatRetreatDirection + 4) % 8
		    : (state.combatTargetBatRetreatDirection
		    + ((static_cast<uint32_t>(tick) ^ state.combatTargetEntityId) & 1U
		        ? 7
		        : 1)) % 8;
		const auto displacement = DirectionDisplacements[direction];
		const int candidateX = state.combatTargetPositionX + displacement.first;
		const int candidateY = state.combatTargetPositionY + displacement.second;
		if (candidateX >= 0 && candidateX < 40
		    && candidateY >= 0 && candidateY < 40
		    && !(candidateX == state.positionX && candidateY == state.positionY)
		    && !(state.secondaryPlayerEntityId != 0
		        && candidateX == state.secondaryPlayerPositionX
		        && candidateY == state.secondaryPlayerPositionY)) {
			state.combatTargetMoving = true;
			state.combatTargetFuturePositionX = candidateX;
			state.combatTargetFuturePositionY = candidateY;
			state.combatTargetDirection = direction;
		}
		--state.combatTargetBatRetreatTicks;
		if (state.combatTargetBatRetreatTicks == 0)
			state.combatTargetBatRetreatDirection = 0;
		return;
	}
	if ((state.combatTargetNativeAi == "Gargoyle"
	        || state.combatTargetNativeAi == "GoatMelee"
	        || state.combatTargetNativeAi == "Gharbad")
	    && distance > 1) {
		const bool wounded = state.combatTargetNativeAi == "Gargoyle"
		    && state.combatTargetMaxHitPoints > 0
		    && state.combatTargetHitPoints * 100
		        < state.combatTargetMaxHitPoints * 50;
		if (wounded
		    || (state.combatTargetNativeAi == "Gargoyle"
		        && state.combatTargetGargoyleSpecialActive))
			return;
		// Compact the healthy AiAvoidance idle band. The managed controller
		// persists this counter even when no walk is admitted.
		const int actionRoll = state.combatTargetMonsterAiSeed != 0
		    ? std::max(GenerateRnd(state, 100), 0)
		    : static_cast<int>((static_cast<uint32_t>(tick)
		        ^ state.combatTargetEntityId
		        ^ (victim.secondary ? state.secondaryPlayerEntityId : state.entityId))
		        % 100U);
		const int movementChance = std::min(
		    100,
		    28 + 2 * state.combatTargetIntelligence);
		// Zero-seed GoatMelee uses the managed action seed so movement turns do
		// not consume the shared gameplay RNG stream.
		const uint32_t actionSeed = static_cast<uint32_t>(tick)
		    ^ state.combatTargetEntityId
		    ^ (victim.secondary ? state.secondaryPlayerEntityId : state.entityId);
		const bool usesCompatibilitySeed = (state.combatTargetNativeAi == "GoatMelee"
		    || state.combatTargetNativeAi == "Gharbad")
		    && state.combatTargetMonsterAiSeed == 0;
		const int direction = GetDirection(
		    state.combatTargetPositionX,
		    state.combatTargetPositionY,
		    victim.positionX,
		    victim.positionY);
		const auto isCandidateAvailable = [&](int candidateDirection) {
			const auto displacement = DirectionDisplacements[candidateDirection];
			const int candidateX = state.combatTargetPositionX + displacement.first;
			const int candidateY = state.combatTargetPositionY + displacement.second;
			return candidateX >= 0 && candidateX < 40
			    && candidateY >= 0 && candidateY < 40
			    && !(candidateX == state.positionX && candidateY == state.positionY)
			    && !(state.secondaryPlayerEntityId != 0
			        && candidateX == state.secondaryPlayerPositionX
			        && candidateY == state.secondaryPlayerPositionY)
			    && !(state.objectEntityId != 0
			        && !state.objectActivated
			        && !state.objectDestroyed
			        && !state.objectMissilePassable
			        && state.objectLevelId == state.combatTargetLevelId
			        && candidateX == state.objectPositionX
			        && candidateY == state.objectPositionY);
		};
		const auto tryMove = [&](int candidateDirection) {
			if (!isCandidateAvailable(candidateDirection))
				return false;
			const auto displacement = DirectionDisplacements[candidateDirection];
			state.combatTargetMoving = true;
			state.combatTargetFuturePositionX = state.combatTargetPositionX + displacement.first;
			state.combatTargetFuturePositionY = state.combatTargetPositionY + displacement.second;
			state.combatTargetDirection = candidateDirection;
			state.combatTargetAvoidanceMovementMode = displacement.first;
			state.combatTargetAvoidanceMovementIdleTicks = displacement.second;
			return true;
		};
		const bool startsMoveGoal = distance >= 4
		    && (usesCompatibilitySeed
		        ? (actionSeed / 100U) % 4U == 0U
		        : std::max(GenerateRnd(state, 4), 0) == 0);
		if (state.combatTargetAvoidanceMovementTicks >= 0 || startsMoveGoal) {
			if (state.combatTargetAvoidanceMovementTicks < 0) {
				state.combatTargetAvoidanceMovementTicks = 0;
				state.combatTargetAvoidanceMovementTurn = usesCompatibilitySeed
				    ? static_cast<int>((actionSeed / 100U) % 2U)
				    : std::max(GenerateRnd(state, 2), 0);
			}
			const bool completes = state.combatTargetAvoidanceMovementTicks >= 2 * distance
			    && isCandidateAvailable(direction);
			if (state.combatTargetAvoidanceMovementTicks < 255)
				++state.combatTargetAvoidanceMovementTicks;
			if (completes) {
				state.combatTargetAvoidanceMovementTicks = -1;
				state.combatTargetAvoidanceMovementTurn = 0;
			} else {
				const int roundWalkChance = std::min(
				    100,
				    5 * (state.combatTargetIntelligence + 16));
				if (actionRoll >= roundWalkChance)
					return;
				const int turn = state.combatTargetAvoidanceMovementTurn == 0 ? 1 : -1;
				const int turn45 = (direction + turn + 8) % 8;
				const int turn90 = (turn45 + turn + 8) % 8;
				const int primaryCandidates[] = { turn90, turn45, direction };
				for (const int candidateDirection : primaryCandidates) {
					if (tryMove(candidateDirection))
						return;
				}
				state.combatTargetAvoidanceMovementTurn =
				    state.combatTargetAvoidanceMovementTurn == 0 ? 1 : 0;
				const int fallbackDirection = (turn90 + 4) % 8;
				const int firstTurn = usesCompatibilitySeed
				    ? ((actionSeed / 100U) % 2U == 0U ? 1 : -1)
				    : (std::max(GenerateRnd(state, 2), 0) == 0 ? 1 : -1);
				const int secondTurn = usesCompatibilitySeed
				    ? ((actionSeed / 200U) % 2U == 0U ? 2 : -2)
				    : (std::max(GenerateRnd(state, 2), 0) == 0 ? 2 : -2);
				const int fallbackCandidates[] = {
				    fallbackDirection,
				    (fallbackDirection + firstTurn + 8) % 8,
				    (fallbackDirection - firstTurn + 8) % 8,
				    (fallbackDirection + secondTurn + 8) % 8,
				    (fallbackDirection - secondTurn + 8) % 8,
				};
				for (const int candidateDirection : fallbackCandidates) {
					if (tryMove(candidateDirection))
						return;
				}
				return;
			}
		}
		if (actionRoll >= movementChance) {
			if (state.combatTargetAvoidanceMovementIdleTicks < 1024)
				++state.combatTargetAvoidanceMovementIdleTicks;
			return;
		}
		const int firstTurn = usesCompatibilitySeed
		    ? ((actionSeed / 100U) % 2U == 0U ? 1 : -1)
		    : (std::max(GenerateRnd(state, 2), 0) == 0 ? 1 : -1);
		const int secondTurn = usesCompatibilitySeed
		    ? ((actionSeed / 200U) % 2U == 0U ? 2 : -2)
		    : (std::max(GenerateRnd(state, 2), 0) == 0 ? 2 : -2);
		const int candidates[] = {
			direction,
			(direction + firstTurn + 8) % 8,
			(direction - firstTurn + 8) % 8,
			(direction + secondTurn + 8) % 8,
			(direction - secondTurn + 8) % 8,
		};
		for (const int candidateDirection : candidates) {
			const auto displacement = DirectionDisplacements[candidateDirection];
			const int candidateX = state.combatTargetPositionX + displacement.first;
			const int candidateY = state.combatTargetPositionY + displacement.second;
			if (candidateX < 0 || candidateX >= 40
			    || candidateY < 0 || candidateY >= 40
			    || (candidateX == state.positionX && candidateY == state.positionY)
			    || (state.secondaryPlayerEntityId != 0
			        && candidateX == state.secondaryPlayerPositionX
			        && candidateY == state.secondaryPlayerPositionY)
			    || (state.objectEntityId != 0
			        && !state.objectActivated
			        && !state.objectDestroyed
			        && !state.objectMissilePassable
			        && state.objectLevelId == state.combatTargetLevelId
			        && candidateX == state.objectPositionX
			        && candidateY == state.objectPositionY))
				continue;
			state.combatTargetMoving = true;
			state.combatTargetFuturePositionX = candidateX;
			state.combatTargetFuturePositionY = candidateY;
			state.combatTargetDirection = candidateDirection;
			state.combatTargetAvoidanceMovementMode = displacement.first;
			state.combatTargetAvoidanceMovementIdleTicks = displacement.second;
			return;
		}
		return;
	}
	// Compact the SkeletonAi movement-delay admission; the managed snapshot
	// carries the remaining delay so the next autonomous tick can be compared.
	if (state.combatTargetNativeAi == "SkeletonMelee"
	    || state.combatTargetNativeAi == "Snotspill"
	    || state.combatTargetNativeAi == "Warlord") {
		if (state.combatTargetSkeletonMeleeDelayTicks > 0) {
			--state.combatTargetSkeletonMeleeDelayTicks;
			return;
		}
		if (distance > 1) {
			const uint32_t actionSeed = static_cast<uint32_t>(tick)
			    ^ state.combatTargetEntityId
			    ^ (victim.secondary ? state.secondaryPlayerEntityId : state.entityId);
			const int actionRoll = state.combatTargetMonsterAiSeed != 0
			    ? std::max(GenerateRnd(state, 100), 0)
			    : static_cast<int>(actionSeed % 100U);
			const int delayBand = std::max(
			    0,
			    35 - 4 * state.combatTargetIntelligence);
			if (actionRoll < delayBand) {
				state.combatTargetSkeletonMeleeDelayTicks = std::max(
				    0,
				    15 - 2 * state.combatTargetIntelligence
				        + static_cast<int>((actionSeed / 100U) % 10U));
				return;
			}
		}
	}
	// Compact the SnakeAi decision-delay admissions. The mapped SnakeCharge
	// controller owns projectile travel; this replay boundary preserves the
	// native adjacent and distant delay state before movement or attack.
	if (state.combatTargetNativeAi == "Snake") {
		if (state.combatTargetSnakeDelayTicks > 0) {
			--state.combatTargetSnakeDelayTicks;
			return;
		}
		const bool hasSnakeCharge = std::any_of(
		    state.projectiles.begin(),
		    state.projectiles.end(),
		    [&](const auto &projectile) {
			return projectile.sourceEntityId == state.combatTargetEntityId
			    && projectile.behavior == SnakeChargeBehavior;
		});
		if (hasSnakeCharge) {
			// Managed autonomous dispatch still consumes the adjacent attack
			// admission draw while an in-flight charge owns the movement path.
			if (distance < 2)
				static_cast<void>(GenerateRnd(state, 100));
			return;
		}
		if (distance == 2) {
			uint32_t nextEntityId = 0x80000000U;
			for (const auto &projectile : state.projectiles)
				nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);
			const auto velocity = CalculateVelocity(
				state.combatTargetPositionX,
				state.combatTargetPositionY,
				victim.positionX,
				victim.positionY,
				18);
			ReplayFixtureProjectileState projectile;
			projectile.entityId = nextEntityId;
			projectile.sourceEntityId = state.combatTargetEntityId;
			projectile.targetEntityId = victim.entityId;
			projectile.spellId = 37;
			projectile.levelId = state.combatTargetLevelId;
			projectile.positionX = state.combatTargetPositionX;
			projectile.positionY = state.combatTargetPositionY;
			projectile.startX = projectile.positionX;
			projectile.startY = projectile.positionY;
			projectile.targetX = victim.positionX;
			projectile.targetY = victim.positionY;
			projectile.damage = 4;
			projectile.damageType = 0;
			projectile.remainingTicks = 4;
			projectile.behavior = SnakeChargeBehavior;
			projectile.originalDirection = GetDirection(
				projectile.positionX,
				projectile.positionY,
				projectile.targetX,
				projectile.targetY);
			projectile.velocityX = velocity.first;
			projectile.velocityY = velocity.second;
			projectile.speed = 18;
			projectile.impactLifetime = 2;
			projectile.resistantDamageDivisor = 1;
			state.projectiles.push_back(std::move(projectile));
			return;
		}
		const uint32_t actionSeed = static_cast<uint32_t>(tick)
		    ^ state.combatTargetEntityId
		    ^ (victim.secondary ? state.secondaryPlayerEntityId : state.entityId);
		const int actionRoll = state.combatTargetMonsterAiSeed != 0
		    ? std::max(GenerateRnd(state, 100), 0)
		    : static_cast<int>(actionSeed % 100U);
		if (distance < 2) {
			if (actionRoll >= 20) {
				state.combatTargetSnakeDelayTicks = std::max(
				    0,
				    10 + static_cast<int>((actionSeed / 100U) % 10U));
				return;
			}
		} else {
			const int delayBand = 35 - 4 * state.combatTargetIntelligence;
			if (actionRoll < std::max(0, delayBand)) {
				state.combatTargetSnakeDelayTicks = std::max(
				    0,
				    15 - 2 * state.combatTargetIntelligence
				        + static_cast<int>((actionSeed / 100U) % 10U));
				return;
			}
		}
	}
	// Compact the native ZombieAi action band and deterministic long-range
	// walk. Zero-seed fixtures use the same stable compatibility seed as the
	// managed controller; seeded streams retain the existing native boundary.
	if (state.combatTargetNativeAi == "Zombie"
	    && state.combatTargetMonsterAiSeed == 0) {
		const uint32_t actionSeed = static_cast<uint32_t>(tick)
		    ^ state.combatTargetEntityId
		    ^ (victim.secondary ? state.secondaryPlayerEntityId : state.entityId);
		const int actionRoll = static_cast<int>(actionSeed % 100U);
		const int actionChance = std::min(
		    100,
		    10 + 2 * state.combatTargetIntelligence);
		if (actionRoll >= actionChance)
			return;
		const int longRangeDistance = std::max(
		    2,
		    4 + 2 * state.combatTargetIntelligence);
		const int differenceX = victim.positionX - state.combatTargetPositionX;
		const int differenceY = victim.positionY - state.combatTargetPositionY;
		std::array<std::pair<int, int>, 4> candidates {};
		size_t candidateCount = 0;
		if (distance < longRangeDistance) {
			if (distance < 2)
				return;
			candidates[candidateCount++] = {
			    (differenceX > 0) - (differenceX < 0),
			    (differenceY > 0) - (differenceY < 0),
			};
			candidates[candidateCount++] = {
			    (differenceX > 0) - (differenceX < 0),
			    0,
			};
			candidates[candidateCount++] = {
			    0,
			    (differenceY > 0) - (differenceY < 0),
			};
		} else {
			const int randomDirection = static_cast<int>((actionSeed / 100U) % 8U);
			if (actionSeed % 100U < 20U) {
				const std::array<std::pair<int, int>, 8> zombieDirections {
				    std::pair<int, int> { 1, 0 },
				    { 1, 1 },
				    { 0, 1 },
				    { -1, 1 },
				    { -1, 0 },
				    { -1, -1 },
				    { 0, -1 },
				    { 1, -1 },
				};
				candidates[candidateCount++] = zombieDirections[randomDirection];
			}
			candidates[candidateCount++] = {
			    (differenceX > 0) - (differenceX < 0),
			    (differenceY > 0) - (differenceY < 0),
			};
			candidates[candidateCount++] = {
			    (differenceX > 0) - (differenceX < 0),
			    0,
			};
			candidates[candidateCount++] = {
			    0,
			    (differenceY > 0) - (differenceY < 0),
			};
		}
		for (size_t index = 0; index < candidateCount; ++index) {
			const auto [deltaX, deltaY] = candidates[index];
			if (deltaX == 0 && deltaY == 0)
				continue;
			const int candidateX = state.combatTargetPositionX + deltaX;
			const int candidateY = state.combatTargetPositionY + deltaY;
			if (candidateX < 0 || candidateX >= 40
			    || candidateY < 0 || candidateY >= 40
			    || (candidateX == state.positionX && candidateY == state.positionY)
			    || (state.secondaryPlayerEntityId != 0
			        && candidateX == state.secondaryPlayerPositionX
			        && candidateY == state.secondaryPlayerPositionY))
				continue;
			state.combatTargetMoving = true;
			state.combatTargetFuturePositionX = candidateX;
			state.combatTargetFuturePositionY = candidateY;
			state.combatTargetDirection = GetDirection(
			    state.combatTargetPositionX,
			    state.combatTargetPositionY,
			    candidateX,
			    candidateY);
			return;
		}
		return;
	}
	// Compact the native Overlord/Fat movement admission and its persisted
	// idle/mode counter. Seeded Fat streams retain the existing native path;
	// zero-seed fixtures use the same deterministic compatibility controller.
	if (state.combatTargetNativeAi == "Fat"
	    && state.combatTargetMonsterAiSeed == 0) {
		if (distance <= 1)
			return;
		const int longIdleChance = std::min(
		    100,
		    20 + 4 * state.combatTargetIntelligence);
		const int movementChance = state.combatTargetFatMovementIdleTicks == -1
		    ? longIdleChance
		    : state.combatTargetFatMovementIdleTicks > 20
		    ? longIdleChance
		    : state.combatTargetFatMovementIdleTicks == 0
		          && state.combatTargetFatMovementMode >= 1
		          && state.combatTargetFatMovementMode <= 3
		    ? std::min(100, longIdleChance + 50)
		    : 0;
		const uint32_t actionSeed = static_cast<uint32_t>(tick)
		    ^ state.combatTargetEntityId
		    ^ (victim.secondary ? state.secondaryPlayerEntityId : state.entityId);
		if (static_cast<int>(actionSeed % 100U) >= movementChance) {
			if (state.combatTargetFatMovementIdleTicks < 1024)
				++state.combatTargetFatMovementIdleTicks;
			return;
		}
		const int deltaX = (victim.positionX > state.combatTargetPositionX)
		    - (victim.positionX < state.combatTargetPositionX);
		const int deltaY = (victim.positionY > state.combatTargetPositionY)
		    - (victim.positionY < state.combatTargetPositionY);
		const int candidateX = state.combatTargetPositionX + deltaX;
		const int candidateY = state.combatTargetPositionY + deltaY;
		if (deltaX != 0 || deltaY != 0) {
			const bool blockedByPrimary = candidateX == state.positionX
			    && candidateY == state.positionY;
			const bool blockedBySecondary = state.secondaryPlayerEntityId != 0
			    && candidateX == state.secondaryPlayerPositionX
			    && candidateY == state.secondaryPlayerPositionY;
			if (!blockedByPrimary && !blockedBySecondary) {
				state.combatTargetMoving = true;
				state.combatTargetFuturePositionX = candidateX;
				state.combatTargetFuturePositionY = candidateY;
				state.combatTargetDirection = GetDirection(
				    state.combatTargetPositionX,
				    state.combatTargetPositionY,
				    candidateX,
				    candidateY);
				state.combatTargetFatMovementIdleTicks = deltaY;
				state.combatTargetFatMovementMode = deltaX;
				return;
			}
		}
		if (state.combatTargetFatMovementIdleTicks < 1024)
			++state.combatTargetFatMovementIdleTicks;
		return;
	}
	// Compact the native ButcherAi RandomWalk pursuit. The managed controller
	// uses the same five-direction turn ordering before generic crawl fallback.
	if (state.combatTargetNativeAi == "Butcher" && distance > 1) {
		const int direction = GetDirection(
		    state.combatTargetPositionX,
		    state.combatTargetPositionY,
		    victim.positionX,
		    victim.positionY);
		const uint32_t actionSeed = state.combatTargetEntityId
		    ^ (victim.secondary ? state.secondaryPlayerEntityId : state.entityId);
		const int firstTurn = state.combatTargetMonsterAiSeed != 0
		    ? (std::max(GenerateRnd(state, 2), 0) == 0 ? 1 : -1)
		    : ((actionSeed / 100U) % 2U == 0 ? 1 : -1);
		const int secondTurn = state.combatTargetMonsterAiSeed != 0
		    ? (std::max(GenerateRnd(state, 2), 0) == 0 ? 2 : -2)
		    : ((actionSeed / 200U) % 2U == 0 ? 2 : -2);
		const int candidateDirections[] = {
			direction,
			(direction + firstTurn + 8) % 8,
			(direction - firstTurn + 8) % 8,
			(direction + secondTurn + 8) % 8,
			(direction - secondTurn + 8) % 8,
		};
		for (const int candidateDirection : candidateDirections) {
			const auto displacement = DirectionDisplacements[candidateDirection];
			const int candidateX = state.combatTargetPositionX + displacement.first;
			const int candidateY = state.combatTargetPositionY + displacement.second;
			if (candidateX < 0 || candidateX >= 40
			    || candidateY < 0 || candidateY >= 40
			    || (candidateX == state.positionX && candidateY == state.positionY)
			    || (state.secondaryPlayerEntityId != 0
			        && candidateX == state.secondaryPlayerPositionX
			        && candidateY == state.secondaryPlayerPositionY)
			    || (state.objectEntityId != 0
			        && !state.objectActivated
			        && !state.objectDestroyed
			        && !state.objectMissilePassable
			        && state.objectLevelId == state.combatTargetLevelId
			        && candidateX == state.objectPositionX
			        && candidateY == state.objectPositionY))
				continue;
			state.combatTargetMoving = true;
			state.combatTargetFuturePositionX = candidateX;
			state.combatTargetFuturePositionY = candidateY;
			state.combatTargetDirection = candidateDirection;
			break;
		}
		return;
	}
	// Compact the native SkeletonKing RoundWalk admission. The managed Leoric
	// controller turns toward the nearest participant, admits RoundWalk from
	// the compatibility action seed, and checks its 90/45/straight candidates
	// before the opposite-direction fallback.
	if (state.combatTargetNativeAi == "SkeletonKing" && distance > 1) {
		if (state.combatTargetSkeletonKingDelayTicks > 0) {
			--state.combatTargetSkeletonKingDelayTicks;
			return;
		}
		const uint32_t actionSeed = static_cast<uint32_t>(tick)
		    ^ state.combatTargetEntityId
		    ^ (victim.secondary ? state.secondaryPlayerEntityId : state.entityId);
		const int movementChance = std::min(
		    100,
		    25 + state.combatTargetIntelligence);
		if (static_cast<int>(actionSeed % 100U) >= movementChance) {
			state.combatTargetSkeletonKingDelayTicks = 10
			    + static_cast<int>((actionSeed / 200U) % 10U);
			return;
		}
		if (distance < 3 || (actionSeed / 100U) % 4U != 0U)
			return;
		const int direction = GetDirection(
		    state.combatTargetPositionX,
		    state.combatTargetPositionY,
		    victim.positionX,
		    victim.positionY);
		const int turn = (actionSeed / 100U) % 2U == 0U ? 1 : -1;
		const int turn45 = (direction + turn + 8) % 8;
		const int turn90 = (turn45 + turn + 8) % 8;
		const int fallbackDirection = (turn90 + 4) % 8;
		const int fallbackTurn = (actionSeed / 200U) % 2U == 0U ? 1 : -1;
		const int candidateDirections[] = {
			turn90,
			turn45,
			direction,
			(fallbackDirection + fallbackTurn + 8) % 8,
			(fallbackDirection - fallbackTurn + 8) % 8,
			(fallbackDirection + 2 + 8) % 8,
			(fallbackDirection - 2 + 8) % 8,
		};
		for (const int candidateDirection : candidateDirections) {
			const auto displacement = DirectionDisplacements[candidateDirection];
			const int candidateX = state.combatTargetPositionX + displacement.first;
			const int candidateY = state.combatTargetPositionY + displacement.second;
			if (candidateX < 0 || candidateX >= 40
			    || candidateY < 0 || candidateY >= 40
			    || (candidateX == state.positionX && candidateY == state.positionY)
			    || (state.secondaryPlayerEntityId != 0
			        && candidateX == state.secondaryPlayerPositionX
			        && candidateY == state.secondaryPlayerPositionY)
			    || (state.objectEntityId != 0
			        && !state.objectActivated
			        && !state.objectDestroyed
			        && !state.objectMissilePassable
			        && state.objectLevelId == state.combatTargetLevelId
			        && candidateX == state.objectPositionX
			        && candidateY == state.objectPositionY))
				continue;
			state.combatTargetMoving = true;
			state.combatTargetFuturePositionX = candidateX;
			state.combatTargetFuturePositionY = candidateY;
			state.combatTargetDirection = candidateDirection;
			break;
		}
		return;
	}
	if (distance > 1 && state.combatTargetNativeAi != "Rhino") {
		const int32_t differenceX = victim.positionX - state.combatTargetPositionX;
		const int32_t differenceY = victim.positionY - state.combatTargetPositionY;
		const int32_t deltaX = (differenceX > 0) - (differenceX < 0);
		const int32_t deltaY = (differenceY > 0) - (differenceY < 0);
		const std::pair<int32_t, int32_t> candidates[] = {
			{ state.combatTargetPositionX + deltaX, state.combatTargetPositionY + deltaY },
			{ state.combatTargetPositionX + deltaX, state.combatTargetPositionY },
			{ state.combatTargetPositionX, state.combatTargetPositionY + deltaY },
		};
		for (const auto &candidate : candidates) {
			if ((candidate.first == state.combatTargetPositionX
			        && candidate.second == state.combatTargetPositionY)
			    || candidate.first < 0 || candidate.first >= 40
			    || candidate.second < 0 || candidate.second >= 40
			    || (candidate.first == state.positionX && candidate.second == state.positionY)
			    || (state.secondaryPlayerEntityId != 0
			        && candidate.first == state.secondaryPlayerPositionX
			        && candidate.second == state.secondaryPlayerPositionY)
			    || (state.objectEntityId != 0
			        && !state.objectActivated
			        && !state.objectDestroyed
			        && !state.objectMissilePassable
			        && state.objectLevelId == state.combatTargetLevelId
			        && candidate.first == state.objectPositionX
			        && candidate.second == state.objectPositionY)) {
				continue;
			}
			state.combatTargetMoving = true;
			state.combatTargetFuturePositionX = candidate.first;
			state.combatTargetFuturePositionY = candidate.second;
			state.combatTargetDirection = GetDirection(
			    state.combatTargetPositionX,
			    state.combatTargetPositionY,
			    candidate.first,
			    candidate.second);
			break;
		}
		if (state.combatTargetMoving)
			return;

		// Match the managed bounded breadth-first fallback when all three
		// native crawl candidates are blocked by participants or geometry.
		constexpr std::array<std::pair<int32_t, int32_t>, 8> NeighborOffsets = {
			std::pair<int32_t, int32_t> { 1, 0 },
			{ 1, 1 },
			{ 0, 1 },
			{ -1, 1 },
			{ -1, 0 },
			{ -1, -1 },
			{ 0, -1 },
			{ 1, -1 },
		};
		const int maximumDepth = std::clamp(
			state.combatTargetAggroRange * 2 + 2,
			8,
			256);
		struct SearchNode {
			int32_t x;
			int32_t y;
			int32_t depth;
			int32_t firstX;
			int32_t firstY;
		};
		std::array<int32_t, 40 * 40> visited;
		visited.fill(-1);
		std::queue<SearchNode> queue;
		const auto cellIndex = [](int32_t x, int32_t y) {
			return y * 40 + x;
		};
		const auto participantOccupied = [&](int32_t x, int32_t y) {
			return (x == state.positionX && y == state.positionY)
			    || (state.secondaryPlayerEntityId != 0
			        && x == state.secondaryPlayerPositionX
			        && y == state.secondaryPlayerPositionY)
			    || (state.objectEntityId != 0
			        && !state.objectActivated
			        && !state.objectDestroyed
			        && !state.objectMissilePassable
			        && state.objectLevelId == state.combatTargetLevelId
			        && x == state.objectPositionX
			        && y == state.objectPositionY);
		};
		visited[cellIndex(state.combatTargetPositionX, state.combatTargetPositionY)] = 0;
		queue.push({
			state.combatTargetPositionX,
			state.combatTargetPositionY,
			0,
			state.combatTargetPositionX,
			state.combatTargetPositionY,
		});
		while (!queue.empty()) {
			const SearchNode current = queue.front();
			queue.pop();
			if (current.depth >= maximumDepth)
				continue;
			for (const auto &offset : NeighborOffsets) {
				const int32_t nextX = current.x + offset.first;
				const int32_t nextY = current.y + offset.second;
				if (nextX < 0 || nextX >= 40 || nextY < 0 || nextY >= 40
				    || visited[cellIndex(nextX, nextY)] != -1
				    || participantOccupied(nextX, nextY))
					continue;
				const int32_t nextDepth = current.depth + 1;
				const int32_t firstX = current.depth == 0 ? nextX : current.firstX;
				const int32_t firstY = current.depth == 0 ? nextY : current.firstY;
				visited[cellIndex(nextX, nextY)] = nextDepth;
				if (std::max(
				        std::abs(nextX - victim.positionX),
				        std::abs(nextY - victim.positionY)) <= 1) {
					state.combatTargetMoving = true;
					state.combatTargetFuturePositionX = firstX;
					state.combatTargetFuturePositionY = firstY;
					state.combatTargetDirection = GetDirection(
						state.combatTargetPositionX,
						state.combatTargetPositionY,
						state.combatTargetFuturePositionX,
						state.combatTargetFuturePositionY);
					return;
				}
				queue.push({ nextX, nextY, nextDepth, firstX, firstY });
			}
		}
		return;
	}
	if (state.combatTargetNativeAi == "SkeletonMelee"
	    || state.combatTargetNativeAi == "Snotspill"
	    || state.combatTargetNativeAi == "Warlord") {
		const uint32_t attackSeed = static_cast<uint32_t>(tick)
		    ^ state.combatTargetEntityId
		    ^ (victim.secondary ? state.secondaryPlayerEntityId : state.entityId);
		const int attackRoll = state.combatTargetMonsterAiSeed != 0
		    ? std::max(GenerateRnd(state, 100), 0)
		    : static_cast<int>(attackSeed % 100U);
		const int attackChance = std::min(
		    100,
		    20 + 2 * state.combatTargetIntelligence);
		if (attackRoll >= attackChance) {
			state.combatTargetSkeletonMeleeDelayTicks = std::max(
			    0,
			    10 - 2 * state.combatTargetIntelligence
			        + static_cast<int>((attackSeed / 100U) % 10U));
			return;
		}
	}
	if (state.combatTargetNativeAi == "Rhino"
	    && distance >= 5
	    && state.combatTargetToHitSpecial > 0) {
		const uint32_t attackSeed = static_cast<uint32_t>(tick)
		    ^ state.combatTargetEntityId
		    ^ (victim.secondary ? state.secondaryPlayerEntityId : state.entityId);
		const int attackRoll = state.combatTargetMonsterAiSeed != 0
		    ? std::max(GenerateRnd(state, 100), 0)
		    : static_cast<int>(attackSeed % 100U);
		const int chargeChance = std::min(
		    100,
		    43 + 2 * state.combatTargetIntelligence);
		if (attackRoll >= chargeChance)
			return;
		const int originalMonsterX = state.combatTargetPositionX;
			const int originalMonsterY = state.combatTargetPositionY;
			std::vector<std::pair<int32_t, int32_t>> destinations;
			for (int offsetY = -1; offsetY <= 1; ++offsetY) {
				for (int offsetX = -1; offsetX <= 1; ++offsetX) {
					if (offsetX == 0 && offsetY == 0)
						continue;
					const int candidateX = victim.positionX + offsetX;
					const int candidateY = victim.positionY + offsetY;
					if (candidateX < 0 || candidateX >= 40
					    || candidateY < 0 || candidateY >= 40
					    || (candidateX == state.positionX
					        && candidateY == state.positionY)
					    || (state.secondaryPlayerEntityId != 0
					        && candidateX == state.secondaryPlayerPositionX
					        && candidateY == state.secondaryPlayerPositionY))
						continue;
					destinations.emplace_back(candidateX, candidateY);
				}
			}
			std::sort(destinations.begin(), destinations.end(), [&](const auto &left, const auto &right) {
				const auto distanceToMonster = [&](const auto &candidate) {
					return std::max(
					    std::abs(candidate.first - originalMonsterX),
					    std::abs(candidate.second - originalMonsterY));
				};
				const int leftDistance = distanceToMonster(left);
				const int rightDistance = distanceToMonster(right);
				return leftDistance != rightDistance
				    ? leftDistance < rightDistance
				    : left.second != right.second
				        ? left.second < right.second
				        : left.first < right.first;
				});
		if (destinations.empty())
			return;
				state.combatTargetPositionX = destinations.front().first;
				state.combatTargetPositionY = destinations.front().second;
				state.combatTargetFuturePositionX = state.combatTargetPositionX;
				state.combatTargetFuturePositionY = state.combatTargetPositionY;
				state.combatTargetMoving = false;
				const int minimumDamage = state.combatTargetMinDamage == 0
				        && state.combatTargetMaxDamage == 0
				    ? state.combatTargetAttackDamage
				    : state.combatTargetMinDamage;
				const int maximumDamage = state.combatTargetMinDamage == 0
				        && state.combatTargetMaxDamage == 0
				    ? state.combatTargetAttackDamage
				    : state.combatTargetMaxDamage;
				const int damage = maximumDamage >= minimumDamage
				    ? minimumDamage + static_cast<int>(attackSeed
				        % static_cast<uint32_t>(maximumDamage - minimumDamage + 1))
				    : 0;
				if (victim.secondary)
					state.secondaryPlayerLife = std::max(
					    0,
					    state.secondaryPlayerLife - std::max(0, damage));
				else
					state.life = std::max(0, state.life - std::max(0, damage));
				if (damage > 0
				    && (victim.secondary
				        ? state.secondaryPlayerLife > 0
				        : state.life > 0)) {
					const int direction = GetDirection(
					    originalMonsterX,
					    originalMonsterY,
					    victim.positionX,
					    victim.positionY);
					const auto displacement = DirectionDisplacements[direction];
					const int knockbackX = victim.positionX + displacement.first;
					const int knockbackY = victim.positionY + displacement.second;
					if (knockbackX >= 0 && knockbackX < 40
					    && knockbackY >= 0 && knockbackY < 40
					    && !(knockbackX == state.combatTargetPositionX
					        && knockbackY == state.combatTargetPositionY)) {
						if (victim.secondary) {
							state.secondaryPlayerPositionX = knockbackX;
							state.secondaryPlayerPositionY = knockbackY;
						} else {
							state.positionX = knockbackX;
							state.positionY = knockbackY;
						}
					}
				}
		return;
	}
	if (state.combatTargetNativeAi == "Gargoyle"
	    || state.combatTargetNativeAi == "GoatMelee"
	    || state.combatTargetNativeAi == "Gharbad") {
		const int attackChance = std::min(
		    100,
		    23 + 2 * state.combatTargetIntelligence);
		const int attackRoll = state.combatTargetMonsterAiSeed != 0
		    ? std::max(GenerateRnd(state, 100), 0)
		    : static_cast<int>((static_cast<uint32_t>(tick)
		        ^ state.combatTargetEntityId
		        ^ (victim.secondary ? state.secondaryPlayerEntityId : state.entityId))
		        % 100U);
		if (attackRoll >= attackChance)
			return;
	}
	if (state.combatTargetMonsterAiSeed != 0 && state.combatTargetToHit > 0) {
		const int32_t targetLevel = victim.secondary
		    ? static_cast<int32_t>(state.secondaryPlayerCharacterLevel)
		    : static_cast<int32_t>(state.characterLevel);
		const int32_t hitChance = std::clamp(
		    std::max(
		        15,
		        state.combatTargetToHit
		            + 2 * (state.combatTargetMonsterLevel - targetLevel)
		            + 30),
		    0,
		    100);
		if (std::max(GenerateRnd(state, 100), 0) >= hitChance)
			return;
		const bool canBlock = victim.secondary
		    && state.secondaryPlayerBlockCapable
		    && (state.secondaryPlayerCombatMode == 0
		        || state.secondaryPlayerCombatMode == 4);
		const int32_t blockChance = std::clamp(
		    (victim.secondary ? state.secondaryPlayerDexterity : 0)
		        + 2 * targetLevel
		        - 2 * state.combatTargetMonsterLevel,
		    0,
		    100);
		const int32_t blockRoll = canBlock ? std::max(GenerateRnd(state, 100), 0) : 100;
		if (canBlock && blockRoll < blockChance) {
			state.secondaryPlayerCombatMode = 6;
			state.secondaryPlayerCombatModeEndTick = tick + 4;
			return;
		}
	}
	if (victim.secondary) {
		int32_t damage = state.combatTargetAttackDamage == 0
		    ? 0
		    : std::max(1, state.combatTargetAttackDamage);
		if (state.combatTargetMonsterAiSeed != 0
		    && state.combatTargetToHit > 0) {
			const int32_t minimumDamage = state.combatTargetMinDamage == 0
			        && state.combatTargetMaxDamage == 0
			    ? damage
			    : state.combatTargetMinDamage;
			const int32_t maximumDamage = state.combatTargetMinDamage == 0
			        && state.combatTargetMaxDamage == 0
			    ? damage
			    : state.combatTargetMaxDamage;
			const int32_t minimumDamageQ6 = minimumDamage * 64;
			const int32_t maximumDamageQ6 = maximumDamage * 64;
			const int32_t damageQ6 = minimumDamageQ6 + std::max(
			    GenerateRnd(state, maximumDamageQ6 - minimumDamageQ6 + 1),
			    0);
			damage = damageQ6 / 64;
		}
		state.secondaryPlayerLife = std::max(0, state.secondaryPlayerLife - damage);
		if (state.secondaryPlayerLife == 0)
			StartReplaySecondaryPlayerDeath(state, true);
		return;
	}
	const uint32_t playerEntityId = state.entityId == 0 ? 1U : state.entityId;
	const bool protectedByFlash = std::any_of(
	    state.projectiles.begin(),
	    state.projectiles.end(),
	    [&](const ReplayFixtureProjectileState &projectile) {
		    return (projectile.behavior == 6 || projectile.behavior == 7)
		        && projectile.sourceEntityId == playerEntityId
		        && projectile.remainingTicks < 19;
	    });
	if (protectedByFlash)
		return;
	const bool protectedByEtherealize = std::any_of(
		state.statusEffects.begin(),
		state.statusEffects.end(),
		[](const auto &effect) { return effect.behavior == EtherealizeStatusBehavior; });
	if (protectedByEtherealize)
		return;
	const int32_t damage = state.combatTargetAttackDamage == 0
	    ? 0
	    : std::max(1, state.combatTargetAttackDamage);
	int32_t lifeDamage = damage;
	int32_t reflectedDamage = 0;
	if (damage > 0 && state.reflectCharges > 0) {
		--state.reflectCharges;
		reflectedDamage = damage * (GenerateRnd(state, 11) + 20) / 100;
		if (reflectedDamage > 0) {
			state.combatTargetHitPointsQ6 = std::max(
			    0,
			    state.combatTargetHitPointsQ6 - reflectedDamage * 64);
			state.combatTargetHitPoints = ToWholeCeilingQ6(state.combatTargetHitPointsQ6);
			if (state.combatTargetHitPointsQ6 == 0)
				DefeatReplayCombatTarget(state);
		}
		// Keep the replay stream aligned with the authoritative monster-hit
		// boundary, which advances once after resolving the Reflect percentage.
		AdvanceRandom(state);
		lifeDamage = std::max(0, damage - reflectedDamage);
	}
	const bool hasNoManaItem = std::any_of(
	    state.equipment.begin(),
	    state.equipment.end(),
	    [](const ReplayFixtureSlottedItem &item) {
		    constexpr uint32_t NoManaItemFlag = 0x08000000U;
		    return item.state.statFlag
		        && (item.state.magical == 0 || item.state.identified)
		        && (item.state.flags & NoManaItemFlag) != 0;
	    });
	if (damage > 0 && state.manaShieldActive && !hasNoManaItem) {
		const auto spell = std::find_if(
		    state.memorizedSpells.begin(),
		    state.memorizedSpells.end(),
		    [](const ReplayFixtureMemorizedSpell &candidate) {
			    return candidate.spellId == 11;
		    });
		const int32_t level = spell == state.memorizedSpells.end()
		    ? 0
		    : static_cast<int32_t>(spell->level);
		int32_t divisor = 24;
		int32_t manaDamage = damage;
		if (level > 0) {
			divisor -= std::min(level, 7) * 3;
			manaDamage += manaDamage / -divisor;
		}
		if (state.mana >= manaDamage) {
			state.mana -= manaDamage;
			lifeDamage = 0;
			if (state.mana == 0)
				state.manaShieldActive = false;
		} else {
			lifeDamage = manaDamage - state.mana;
			if (level > 0)
				lifeDamage += lifeDamage / (divisor - 1);
			state.mana = 0;
			state.manaShieldActive = false;
		}
	}
	state.life -= std::min(state.life, lifeDamage);
	if (state.life == 0) {
		StartReplayPlayerDeath(
		    state,
		    timing,
		    state.combatTargetEntityId,
		    tick);
	}
}

void AdvanceReplayPlayerAction(ReplayFixtureExecutionState &state, uint64_t tick)
{
	if (!state.currentAction.defined)
		return;
	auto &action = state.currentAction;
	if (!action.effectApplied && tick >= action.effectTick) {
		if (action.kind == 1) {
			state.positionX += action.directionX;
			state.positionY += action.directionY;
		} else if (action.kind == 2) {
			ApplyReplayAttack(
			    state,
			    action.targetEntityId,
			    action.startedAtTick,
			    state.combatMode == 5,
			    true);
		} else if (action.kind == 3) {
			ReplayFixtureCommand command;
			command.spellId = action.spellId;
			command.targetEntityId = action.castTargetEntityId;
			command.targetX = action.targetX;
			command.targetY = action.targetY;
			ApplyReplayCast(state, command);
		}
		action.effectApplied = true;
	}
	if (tick >= action.endTick) {
		const bool death = state.combatMode == 8;
		state.currentAction = { };
		if (!death)
			state.combatMode = 0;
		state.combatModeEndTick = 0;
	}
}

/** Applies the native per-tick monster regeneration boundary used by the C# simulation. */
void AdvanceReplayMonsterRegeneration(ReplayFixtureExecutionState &state)
{
	if (state.combatTargetEntityId == 0
	    || state.combatTargetHitPointsQ6 <= 0
	    || state.combatTargetMonsterLevel <= 0
	    || state.combatTargetHitPointsQ6 >= state.combatTargetMaxHitPointsQ6)
		return;
	const int32_t healing = state.combatTargetMonsterLevel > 1
	    ? state.combatTargetMonsterLevel / 2
	    : state.combatTargetMonsterLevel;
	state.combatTargetHitPointsQ6 = std::min(
	    state.combatTargetMaxHitPointsQ6,
	    state.combatTargetHitPointsQ6 + healing * 64);
	state.combatTargetHitPoints = ToWholeCeilingQ6(state.combatTargetHitPointsQ6);
}

/** Advances compact autonomous monster-projectile lanes through native travel and impact. */
void AdvanceReplayAutonomousMonsterMagicOneTick(
    ReplayFixtureExecutionState &state,
    uint64_t tick)
{
	for (auto iterator = state.projectiles.begin(); iterator != state.projectiles.end();) {
		auto &projectile = *iterator;
		if (projectile.behavior != BloodStarBehavior
		    && projectile.behavior != MonsterMagicBehavior
		    && projectile.behavior != FireboltBehavior) {
			++iterator;
			continue;
		}
		if (projectile.phase == 1) {
			if (projectile.remainingTicks > 0)
				--projectile.remainingTicks;
			if (projectile.remainingTicks == 0)
				iterator = state.projectiles.erase(iterator);
			else
				++iterator;
			continue;
		}
		if (projectile.remainingTicks > 0)
			--projectile.remainingTicks;
		if (projectile.remainingTicks == 0) {
			iterator = state.projectiles.erase(iterator);
			continue;
		}
		const auto entityAt = [&](int32_t x, int32_t y) {
			const bool monsterAtTile = state.combatTargetEntityId != 0
			    && state.combatTargetHitPointsQ6 > 0
			    && state.combatTargetLevelId == projectile.levelId
			    && state.combatTargetPositionX == x
			    && state.combatTargetPositionY == y;
			if (monsterAtTile)
				return state.combatTargetEntityId;
			const bool primaryPlayerAtTile = state.life > 0
			    && state.levelId == projectile.levelId
			    && state.positionX == x
			    && state.positionY == y;
			if (primaryPlayerAtTile)
				return state.entityId;
			const bool secondaryPlayerAtTile = state.secondaryPlayerEntityId != 0
			    && state.secondaryPlayerLife > 0
			    && state.secondaryPlayerLevelId == projectile.levelId
			    && state.secondaryPlayerPositionX == x
			    && state.secondaryPlayerPositionY == y;
			return secondaryPlayerAtTile ? state.secondaryPlayerEntityId : 0U;
		};
		const auto resolvePlayerAt = [&](int32_t x, int32_t y) {
			const bool primaryPlayerAtTile = state.life > 0
			    && state.levelId == projectile.levelId
			    && state.positionX == x
			    && state.positionY == y;
			const bool secondaryPlayerAtTile = state.secondaryPlayerEntityId != 0
			    && state.secondaryPlayerLife > 0
			    && state.secondaryPlayerLevelId == projectile.levelId
			    && state.secondaryPlayerPositionX == x
			    && state.secondaryPlayerPositionY == y;
			if (!primaryPlayerAtTile && !secondaryPlayerAtTile)
				return false;
			const uint32_t targetEntityId = primaryPlayerAtTile
			    ? state.entityId
			    : state.secondaryPlayerEntityId;
			const int32_t targetLevel = primaryPlayerAtTile
			    ? static_cast<int32_t>(state.characterLevel)
			    : static_cast<int32_t>(state.secondaryPlayerCharacterLevel);
			const int32_t distance = std::max(
			    std::abs(x - state.combatTargetPositionX),
			    std::abs(y - state.combatTargetPositionY));
			const int32_t hitChance = std::clamp(
			    std::max(10, 40 + 2 * state.combatTargetMonsterLevel
			        - 2 * targetLevel - 2 * distance),
			    0,
			    100);
			if (std::max(GenerateRnd(state, 100), 0) >= hitChance)
				return false;

			const bool canBlock = secondaryPlayerAtTile
			    && state.secondaryPlayerBlockCapable
			    && (state.secondaryPlayerCombatMode == 0
			        || state.secondaryPlayerCombatMode == 4);
			const int32_t blockChance = std::clamp(
				(secondaryPlayerAtTile ? state.secondaryPlayerDexterity : 0)
				    + 2 * targetLevel
				    - 2 * state.combatTargetMonsterLevel,
				0,
				100);
			const int32_t blockRoll = canBlock ? std::max(GenerateRnd(state, 100), 0) : 100;
			if (canBlock && blockRoll < blockChance) {
				state.secondaryPlayerCombatMode = 6;
				state.secondaryPlayerCombatModeEndTick = tick + 4;
			} else if (primaryPlayerAtTile) {
				const int32_t damageQ6 = std::max(1, projectile.damage) * 64;
				state.life = std::max(0, state.life - damageQ6);
				if (state.life > 0
				    && (damageQ6 >> 6) >= static_cast<int32_t>(state.characterLevel)) {
					state.combatMode = 7;
					state.combatModeEndTick = tick + 6;
				}
			} else {
				const int32_t damageQ6 = std::max(1, projectile.damage) * 64;
				state.secondaryPlayerLife = std::max(0, state.secondaryPlayerLife - damageQ6);
				if (state.secondaryPlayerLife == 0)
					StartReplaySecondaryPlayerDeath(state, true);
				else if (state.secondaryPlayerLife > 0
				    && (damageQ6 >> 6) >= static_cast<int32_t>(state.secondaryPlayerCharacterLevel)) {
					state.secondaryPlayerCombatMode = 7;
					state.secondaryPlayerCombatModeEndTick = tick + 6;
				}
			}
			EnterReplayProjectileImpact(projectile);
			return true;
		};

		const auto visitedTiles = MoveReplayNativeProjectile(projectile);
		if (visitedTiles.empty()) {
			const uint32_t targetEntityId = entityAt(projectile.positionX, projectile.positionY);
			if (projectile.lastCollisionTargetEntityId != targetEntityId
			    && resolvePlayerAt(projectile.positionX, projectile.positionY)) {
				++iterator;
				continue;
			}
			projectile.lastCollisionTargetEntityId = targetEntityId;
			++iterator;
			continue;
		}
		for (const auto &tile : visitedTiles) {
			projectile.positionX = tile.x;
			projectile.positionY = tile.y;
			projectile.traveledX = tile.traveledX;
			projectile.traveledY = tile.traveledY;
			if (resolvePlayerAt(tile.x, tile.y))
				break;
			projectile.lastCollisionTargetEntityId = entityAt(tile.x, tile.y);
		}
		if (projectile.phase == 1) {
			++iterator;
			continue;
		}
		++iterator;
	}
}

/**
 * Admits the compact GoatRanged native arrow branch before projectile travel.
 * The full native AI remains outside this replay projection; this boundary
 * keeps target ordering and the server-owned projectile lifecycle portable.
 */
void AdmitReplayAutonomousMonsterArrow(
    ReplayFixtureExecutionState &state,
    uint64_t tick)
{
	if (state.combatTargetEntityId == 0
	    || (state.combatTargetNativeAi != "GoatRanged"
	        && state.combatTargetNativeAi != "SkeletonRanged")
	    || state.combatTargetHitPointsQ6 <= 0
	    || state.combatTargetAggroRange <= 0
	    || state.combatTargetToHit <= 0
	    || state.combatTargetMaxDamage <= 0
	    || ((state.combatTargetNativeAi == "GoatRanged"
	            || state.combatTargetNativeAi == "SkeletonRanged")
	        && state.combatTargetMoving)
	    || std::any_of(state.projectiles.begin(), state.projectiles.end(), [&](const auto &projectile) {
			return projectile.sourceEntityId == state.combatTargetEntityId
			    && projectile.behavior == MonsterArrowBehavior;
		}))
		return;

	struct Candidate {
		uint32_t entityId;
		int32_t positionX;
		int32_t positionY;
	};
	std::vector<Candidate> candidates;
	if (state.life > 0 && state.levelId == state.combatTargetLevelId)
		candidates.push_back({ state.entityId, state.positionX, state.positionY });
	if (state.secondaryPlayerEntityId != 0
	    && state.secondaryPlayerLife > 0
	    && state.secondaryPlayerLevelId == state.combatTargetLevelId)
		candidates.push_back({ state.secondaryPlayerEntityId, state.secondaryPlayerPositionX, state.secondaryPlayerPositionY });
	if (candidates.empty())
		return;
	const auto candidateDistance = [&](const Candidate &candidate) {
		return std::max(
		    std::abs(candidate.positionX - state.combatTargetPositionX),
		    std::abs(candidate.positionY - state.combatTargetPositionY));
	};
	std::sort(candidates.begin(), candidates.end(), [&](const auto &left, const auto &right) {
		const int32_t leftDistance = candidateDistance(left);
		const int32_t rightDistance = candidateDistance(right);
		return leftDistance != rightDistance
		    ? leftDistance < rightDistance
		    : left.entityId < right.entityId;
	});
	const Candidate &target = candidates.front();
	if (candidateDistance(target) > state.combatTargetAggroRange)
		return;
	if (state.combatTargetNativeAi == "GoatRanged"
	    && state.combatTargetRangedDelayTicks > 0) {
		--state.combatTargetRangedDelayTicks;
		return;
	}
	if (state.combatTargetNativeAi == "SkeletonRanged") {
		const uint32_t attackSeed = static_cast<uint32_t>(tick)
		    ^ state.combatTargetEntityId
		    ^ target.entityId;
		const int32_t attackChance = std::min(
		    100,
		    3 + 2 * state.combatTargetIntelligence);
		if (state.combatTargetMonsterAiSeed != 0)
			static_cast<void>(GenerateRnd(state, 100));
		const int attackRoll = state.combatTargetMonsterAiSeed != 0
		    ? std::max(GenerateRnd(state, 100), 0)
		    : static_cast<int>(attackSeed % 100U);
		if (attackRoll >= attackChance)
			return;
	}

	uint32_t nextEntityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);
	ReplayFixtureProjectileState projectile;
	projectile.entityId = nextEntityId;
	projectile.sourceEntityId = state.combatTargetEntityId;
	projectile.targetEntityId = target.entityId;
	projectile.levelId = state.combatTargetLevelId;
	projectile.positionX = state.combatTargetPositionX;
	projectile.positionY = state.combatTargetPositionY;
	projectile.startX = projectile.positionX;
	projectile.startY = projectile.positionY;
	projectile.targetX = target.positionX;
	projectile.targetY = target.positionY;
	projectile.damage = state.combatTargetMaxDamage;
	projectile.remainingTicks = static_cast<uint32_t>(std::max(1, candidateDistance(target)));
	projectile.behavior = MonsterArrowBehavior;
	projectile.speed = 1;
	projectile.impactLifetime = 1;
	state.projectiles.push_back(std::move(projectile));
	if (state.combatTargetNativeAi == "GoatRanged") {
		const uint32_t attackSeed = static_cast<uint32_t>(tick)
		    ^ state.combatTargetEntityId
		    ^ target.entityId;
		state.combatTargetRangedDelayTicks = state.combatTargetMonsterAiSeed != 0
		    ? std::max(GenerateRnd(state, 20), 0)
		    : static_cast<int32_t>((attackSeed / 200U) % 20U);
	}
}

/** Advances the native Acid projectile, splat, and persistent puddle chain. */
void AdvanceReplayAcidOneTick(ReplayFixtureExecutionState &state, uint64_t tick)
{
	const size_t initialProjectileCount = state.projectiles.size();
	for (size_t index = 0; index < initialProjectileCount && index < state.projectiles.size();) {
		auto &projectile = state.projectiles[index];
		if (projectile.behavior == AcidBehavior) {
			const auto resolvePlayerAt = [&](int32_t x, int32_t y) {
				const bool primaryPlayerAtTile = state.life > 0
				    && state.levelId == projectile.levelId
				    && state.positionX == x
				    && state.positionY == y;
				const bool secondaryPlayerAtTile = state.secondaryPlayerEntityId != 0
				    && state.secondaryPlayerLife > 0
				    && state.secondaryPlayerLevelId == projectile.levelId
				    && state.secondaryPlayerPositionX == x
				    && state.secondaryPlayerPositionY == y;
				if (!primaryPlayerAtTile && !secondaryPlayerAtTile)
					return false;
				const uint32_t targetEntityId = primaryPlayerAtTile
				    ? (state.entityId == 0 ? 1U : state.entityId)
				    : state.secondaryPlayerEntityId;
				if (projectile.lastCollisionTargetEntityId == targetEntityId)
					return false;
				projectile.lastCollisionTargetEntityId = targetEntityId;
				const int targetLevel = primaryPlayerAtTile
				    ? static_cast<int>(state.characterLevel)
				    : static_cast<int>(state.secondaryPlayerCharacterLevel);
				const int distance = std::max(
				    std::abs((primaryPlayerAtTile ? state.positionX : state.secondaryPlayerPositionX)
				        - state.combatTargetPositionX),
				    std::abs((primaryPlayerAtTile ? state.positionY : state.secondaryPlayerPositionY)
				        - state.combatTargetPositionY));
				const int hitChance = std::clamp(
				    std::max(10, 40 + 2 * state.combatTargetMonsterLevel
				        - 2 * targetLevel - 2 * distance),
				    0,
				    100);
				if (std::max(GenerateRnd(state, 100), 0) >= hitChance)
					return false;

				// Native shifted Acid damage still consumes its zero-width damage roll.
				static_cast<void>(GenerateRnd(state, 1));
				const int32_t damageQ6 = std::max(1, projectile.damage) * 64;
				if (primaryPlayerAtTile) {
					state.life = std::max(0, state.life - damageQ6);
					if (state.life > 0
					    && (damageQ6 >> 6) >= static_cast<int32_t>(state.characterLevel)) {
						state.combatMode = 7;
						state.combatModeEndTick = tick + 6;
					}
				} else {
					state.secondaryPlayerLife = std::max(0, state.secondaryPlayerLife - damageQ6);
					if (state.secondaryPlayerLife == 0)
						StartReplaySecondaryPlayerDeath(state, true);
					else if ((damageQ6 >> 6) >= static_cast<int32_t>(state.secondaryPlayerCharacterLevel)) {
						state.secondaryPlayerCombatMode = 7;
						state.secondaryPlayerCombatModeEndTick = tick + 6;
					}
				}
				return true;
			};

			const auto visitedTiles = MoveReplayNativeProjectile(projectile);
			bool playerHit = false;
			for (const auto &tile : visitedTiles) {
				projectile.positionX = tile.x;
				projectile.positionY = tile.y;
				projectile.traveledX = tile.traveledX;
				projectile.traveledY = tile.traveledY;
				if (resolvePlayerAt(tile.x, tile.y)) {
					playerHit = true;
					break;
				}
			}
			if (visitedTiles.empty())
				playerHit = resolvePlayerAt(projectile.positionX, projectile.positionY);
			if (projectile.remainingTicks > 0)
				--projectile.remainingTicks;
			if (!playerHit && projectile.remainingTicks != 0) {
				++index;
				continue;
			}
			const uint32_t entityId = [&] {
				uint32_t next = 0x80000000U;
				for (const auto &candidate : state.projectiles)
					next = std::max(next, candidate.entityId + 1U);
				return next;
			}();
			ReplayFixtureProjectileState splat;
			splat.entityId = entityId;
			splat.sourceEntityId = projectile.sourceEntityId;
			splat.spellId = projectile.spellId;
			splat.levelId = projectile.levelId;
			splat.positionX = projectile.positionX;
			splat.positionY = projectile.positionY;
			splat.targetX = projectile.positionX;
			splat.targetY = projectile.positionY;
			splat.startX = splat.positionX;
			splat.startY = splat.positionY;
			splat.damageType = AcidDamageType;
			splat.remainingTicks = 1;
			splat.behavior = AcidSplatBehavior;
			state.projectiles.erase(state.projectiles.begin() + index);
			state.projectiles.push_back(std::move(splat));
			continue;
		}
		if (projectile.behavior == AcidSplatBehavior) {
			if (projectile.remainingTicks > 0)
				--projectile.remainingTicks;
			if (projectile.remainingTicks != 0) {
				++index;
				continue;
			}
			const uint32_t entityId = [&] {
				uint32_t next = 0x80000000U;
				for (const auto &candidate : state.projectiles)
					next = std::max(next, candidate.entityId + 1U);
				return next;
			}();
			ReplayFixtureProjectileState puddle;
			puddle.entityId = entityId;
			puddle.sourceEntityId = projectile.sourceEntityId;
			puddle.spellId = projectile.spellId;
			puddle.levelId = projectile.levelId;
			puddle.positionX = projectile.positionX + 1;
			puddle.positionY = projectile.positionY + 1;
			puddle.targetX = puddle.positionX;
			puddle.targetY = puddle.positionY;
			puddle.startX = puddle.positionX;
			puddle.startY = puddle.positionY;
			puddle.damage = state.combatTargetMonsterLevel >= 2 ? 2 : 1;
			puddle.damageType = AcidDamageType;
			puddle.remainingTicks = static_cast<uint32_t>(GenerateRnd(state, 1) + 1);
			puddle.behavior = AcidPuddleBehavior;
			state.projectiles.erase(state.projectiles.begin() + index);
			state.projectiles.push_back(std::move(puddle));
			continue;
		}
		if (projectile.behavior == AcidPuddleBehavior) {
			if (projectile.phase == 1) {
				if (projectile.remainingTicks > 0)
					--projectile.remainingTicks;
				if (projectile.remainingTicks == 0)
					state.projectiles.erase(state.projectiles.begin() + index);
				else
					++index;
				continue;
			}
			if (projectile.remainingTicks > 0)
				--projectile.remainingTicks;
			if (projectile.remainingTicks == 0) {
				projectile.phase = 1;
				projectile.remainingTicks = 2;
			}
		}
		++index;
	}
}

/** Admits the compact Hellfire HorkDemon Hork Spawn branch. */
void AdmitReplayAutonomousHorkSpawn(
    ReplayFixtureExecutionState &state,
    uint64_t tick)
{
	if (state.combatTargetEntityId == 0
	    || state.combatTargetNativeAi != "HorkDemon"
	    || state.combatTargetHitPointsQ6 <= 0
	    || state.combatTargetAggroRange <= 0
	    || std::any_of(state.projectiles.begin(), state.projectiles.end(), [&](const auto &projectile) {
		    return projectile.sourceEntityId == state.combatTargetEntityId
		        && projectile.behavior == HorkSpawnBehavior;
	    }))
		return;
	if (state.combatTargetHorkDemonDelayTicks > 0) {
		--state.combatTargetHorkDemonDelayTicks;
		return;
	}

	struct Candidate {
		uint32_t entityId;
		int32_t positionX;
		int32_t positionY;
	};
	std::vector<Candidate> candidates;
	if (state.life > 0 && state.levelId == state.combatTargetLevelId)
		candidates.push_back({ state.entityId == 0 ? 1U : state.entityId, state.positionX, state.positionY });
	if (state.secondaryPlayerEntityId != 0
	    && state.secondaryPlayerLife > 0
	    && state.secondaryPlayerLevelId == state.combatTargetLevelId)
		candidates.push_back({ state.secondaryPlayerEntityId, state.secondaryPlayerPositionX, state.secondaryPlayerPositionY });
	if (candidates.empty())
		return;
	const auto candidateDistance = [&](const Candidate &candidate) {
		return std::max(
		    std::abs(candidate.positionX - state.combatTargetPositionX),
		    std::abs(candidate.positionY - state.combatTargetPositionY));
	};
	std::sort(candidates.begin(), candidates.end(), [&](const auto &left, const auto &right) {
		const int32_t leftDistance = candidateDistance(left);
		const int32_t rightDistance = candidateDistance(right);
		return leftDistance != rightDistance
		    ? leftDistance < rightDistance
		    : left.entityId < right.entityId;
	});
	const Candidate &target = candidates.front();
	const int distance = candidateDistance(target);
	if (distance > state.combatTargetAggroRange || distance < 3)
		return;

	const uint32_t actionSeed = static_cast<uint32_t>(tick)
	    ^ state.combatTargetEntityId
	    ^ target.entityId;
	if (actionSeed % 100U >= 43U)
		return;

	const auto displacement = DirectionDisplacements[state.combatTargetDirection];
	const int32_t spawnCheckX = state.combatTargetPositionX + displacement.first;
	const int32_t spawnCheckY = state.combatTargetPositionY + displacement.second;
	if (spawnCheckX < 0 || spawnCheckX >= 40 || spawnCheckY < 0 || spawnCheckY >= 40
	    || (spawnCheckX == state.positionX && spawnCheckY == state.positionY)
	    || (state.secondaryPlayerEntityId != 0
	        && state.secondaryPlayerLife > 0
	        && spawnCheckX == state.secondaryPlayerPositionX
	        && spawnCheckY == state.secondaryPlayerPositionY))
		return;

	uint32_t nextEntityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);
	const auto velocity = CalculateVelocity(
	    state.combatTargetPositionX,
	    state.combatTargetPositionY,
	    target.positionX,
	    target.positionY,
	    8);
	ReplayFixtureProjectileState projectile;
	projectile.entityId = nextEntityId;
	projectile.sourceEntityId = state.combatTargetEntityId;
	projectile.targetEntityId = target.entityId;
	projectile.spellId = 117;
	projectile.levelId = state.combatTargetLevelId;
	projectile.positionX = state.combatTargetPositionX;
	projectile.positionY = state.combatTargetPositionY;
	projectile.startX = projectile.positionX;
	projectile.startY = projectile.positionY;
	projectile.targetX = target.positionX;
	projectile.targetY = target.positionY;
	projectile.damage = 0;
	projectile.damageType = 0;
	projectile.remainingTicks = 9;
	projectile.behavior = HorkSpawnBehavior;
	projectile.originalDirection = state.combatTargetDirection;
	projectile.velocityX = velocity.first;
	projectile.velocityY = velocity.second;
	projectile.speed = 8;
	projectile.impactLifetime = 1;
	projectile.resistantDamageDivisor = 1;
	state.projectiles.push_back(std::move(projectile));
}

/** Advances compact Hork Spawn travel through its first lifecycle ticks. */
void AdvanceReplayHorkSpawnsOneTick(
    ReplayFixtureExecutionState &state,
    uint64_t tick)
{
	for (auto iterator = state.projectiles.begin(); iterator != state.projectiles.end();) {
		auto &projectile = *iterator;
		if (projectile.behavior != HorkSpawnBehavior) {
			++iterator;
			continue;
		}
		if (projectile.remainingTicks > 0)
			--projectile.remainingTicks;
		const bool primaryPlayerAtTile = state.life > 0
		    && state.levelId == projectile.levelId
		    && state.positionX == projectile.positionX
		    && state.positionY == projectile.positionY
		    && projectile.targetEntityId == (state.entityId == 0 ? 1U : state.entityId);
		const bool secondaryPlayerAtTile = state.secondaryPlayerEntityId != 0
		    && state.secondaryPlayerLife > 0
		    && state.secondaryPlayerLevelId == projectile.levelId
		    && state.secondaryPlayerPositionX == projectile.positionX
		    && state.secondaryPlayerPositionY == projectile.positionY
		    && projectile.targetEntityId == state.secondaryPlayerEntityId;
		if (primaryPlayerAtTile || secondaryPlayerAtTile) {
			constexpr int damageQ6 = 64;
			const int targetLevel = primaryPlayerAtTile
			    ? static_cast<int>(state.characterLevel)
			    : static_cast<int>(state.secondaryPlayerCharacterLevel);
			const int distance = std::max(
			    std::abs((primaryPlayerAtTile ? state.positionX : state.secondaryPlayerPositionX)
			        - state.combatTargetPositionX),
			    std::abs((primaryPlayerAtTile ? state.positionY : state.secondaryPlayerPositionY)
			        - state.combatTargetPositionY));
			const int hitChance = std::clamp(
			    std::max(10, 40 + 2 * state.combatTargetMonsterLevel
			        - 2 * targetLevel - 2 * distance),
			    0,
			    100);
			const bool hit = std::max(GenerateRnd(state, 100), 0) < hitChance;
			if (hit) {
				const bool primaryPlayerBlockCapable = primaryPlayerAtTile
				    && (state.combatMode == 0 || state.combatMode == 4)
				    && std::any_of(state.equipment.begin(), state.equipment.end(), [](const auto &item) {
					    return item.state.itemType == 5;
				    });
				const bool canBlock = (secondaryPlayerAtTile
				        && state.secondaryPlayerBlockCapable
				        && (state.secondaryPlayerCombatMode == 0
				            || state.secondaryPlayerCombatMode == 4))
				    || primaryPlayerBlockCapable;
				const int blockRoll = canBlock ? std::max(GenerateRnd(state, 100), 0) : 100;
				const int blockChance = std::clamp(
				    (secondaryPlayerAtTile ? state.secondaryPlayerDexterity : 0)
				        + 2 * targetLevel - 2 * state.combatTargetMonsterLevel,
				    0,
				    100);
				if (canBlock && blockRoll < blockChance) {
					if (primaryPlayerAtTile) {
						state.combatMode = 6;
						state.combatModeEndTick = tick + 4;
					} else {
						state.secondaryPlayerCombatMode = 6;
						state.secondaryPlayerCombatModeEndTick = tick + 4;
					}
					iterator = state.projectiles.erase(iterator);
					continue;
				}
				// Native PlayerMHit consumes the zero-width damage roll even
				// though Hork Spawn always applies its one-point minimum.
				static_cast<void>(GenerateRnd(state, 1));
				if (primaryPlayerAtTile) {
					state.life = std::max(0, state.life - damageQ6);
					if (state.life > 0
					    && (damageQ6 >> 6) >= static_cast<int32_t>(state.characterLevel)) {
						state.combatMode = 7;
						state.combatModeEndTick = tick + 6;
					}
				} else {
					state.secondaryPlayerLife = std::max(0, state.secondaryPlayerLife - damageQ6);
					if (state.secondaryPlayerLife > 0
					    && (damageQ6 >> 6) >= static_cast<int32_t>(state.secondaryPlayerCharacterLevel)) {
						state.secondaryPlayerCombatMode = 7;
						state.secondaryPlayerCombatModeEndTick = tick + 6;
					}
				}
				iterator = state.projectiles.erase(iterator);
				continue;
			}
		}
		const bool blockingObject = state.objectEntityId != 0
		    && state.objectLevelId == projectile.levelId
		    && state.objectPositionX == projectile.positionX
		    && state.objectPositionY == projectile.positionY
		    && !state.objectDestroyed
		    && !state.objectMissilePassable;
		if (blockingObject) {
			if (state.objectBreakable)
				state.objectDestroyed = true;
			iterator = state.projectiles.erase(iterator);
			continue;
		}
		if (projectile.remainingTicks == 0) {
			iterator = state.projectiles.erase(iterator);
			continue;
		}
		const auto visitedTiles = MoveReplayNativeProjectile(projectile);
		for (const auto &tile : visitedTiles) {
			projectile.positionX = tile.x;
			projectile.positionY = tile.y;
			projectile.traveledX = tile.traveledX;
			projectile.traveledY = tile.traveledY;
		}
		++iterator;
	}
}

/** Admits the compact Succubus BloodStar native ranged branch. */
void AdmitReplayAutonomousBloodStar(
    ReplayFixtureExecutionState &state,
    uint64_t tick)
{
	if (state.combatTargetEntityId == 0
	    || (state.combatTargetNativeAi != "Succubus"
	        && state.combatTargetNativeAi != "LazarusSuccubus")
	    || state.combatTargetHitPointsQ6 <= 0
	    || state.combatTargetAggroRange <= 0
	    || state.combatTargetRangedDelayTicks < 0
	    || std::any_of(state.projectiles.begin(), state.projectiles.end(), [&](const auto &projectile) {
			return projectile.sourceEntityId == state.combatTargetEntityId
			    && projectile.behavior == BloodStarBehavior;
		}))
		return;
	if (state.combatTargetRangedDelayTicks > 0) {
		--state.combatTargetRangedDelayTicks;
		return;
	}
	struct Candidate {
		uint32_t entityId;
		int32_t positionX;
		int32_t positionY;
	};
	std::vector<Candidate> candidates;
	if (state.life > 0 && state.levelId == state.combatTargetLevelId)
		candidates.push_back({ state.entityId, state.positionX, state.positionY });
	if (state.secondaryPlayerEntityId != 0
	    && state.secondaryPlayerLife > 0
	    && state.secondaryPlayerLevelId == state.combatTargetLevelId)
		candidates.push_back({ state.secondaryPlayerEntityId, state.secondaryPlayerPositionX, state.secondaryPlayerPositionY });
	if (candidates.empty())
		return;
	const auto candidateDistance = [&](const Candidate &candidate) {
		return std::max(
		    std::abs(candidate.positionX - state.combatTargetPositionX),
		    std::abs(candidate.positionY - state.combatTargetPositionY));
	};
	std::sort(candidates.begin(), candidates.end(), [&](const auto &left, const auto &right) {
		const int32_t leftDistance = candidateDistance(left);
		const int32_t rightDistance = candidateDistance(right);
		return leftDistance != rightDistance
		    ? leftDistance < rightDistance
		    : left.entityId < right.entityId;
	});
	const Candidate &target = candidates.front();
	const int distance = candidateDistance(target);
	if (distance > state.combatTargetAggroRange)
		return;
	if (distance < 4) {
		const uint32_t attackSeed = static_cast<uint32_t>(tick)
		    ^ state.combatTargetEntityId
		    ^ target.entityId;
		const int retreatChance = std::min(
		    100,
		    70 + 10 * state.combatTargetIntelligence);
		const bool usesNativeAiStream = state.combatTargetMonsterAiSeed != 0;
		const int retreatRoll = usesNativeAiStream
		    ? std::max(GenerateRnd(state, 100), 0)
		    : static_cast<int>(attackSeed % 100U);
		if (retreatRoll < retreatChance) {
			if (usesNativeAiStream) {
				const int towardDirection = GetDirection(
				    state.combatTargetPositionX,
				    state.combatTargetPositionY,
				    target.positionX,
				    target.positionY);
				const int awayDirection = (towardDirection + 4) % 8;
				const int firstTurn = std::max(GenerateRnd(state, 2), 0) == 0 ? 1 : -1;
				const int secondTurn = std::max(GenerateRnd(state, 2), 0) == 0 ? 2 : -2;
				const int candidateDirections[] = {
				    awayDirection,
				    (awayDirection + firstTurn + 8) % 8,
				    (awayDirection - firstTurn + 8) % 8,
				    (awayDirection + secondTurn + 8) % 8,
				    (awayDirection - secondTurn + 8) % 8,
				};
				for (const int candidateDirection : candidateDirections) {
					const auto displacement = DirectionDisplacements[candidateDirection];
					const int candidateX = state.combatTargetPositionX + displacement.first;
					const int candidateY = state.combatTargetPositionY + displacement.second;
					if (candidateX < 0 || candidateX >= 40
					    || candidateY < 0 || candidateY >= 40
					    || (candidateX == state.positionX && candidateY == state.positionY)
					    || (state.secondaryPlayerEntityId != 0
					        && candidateX == state.secondaryPlayerPositionX
					        && candidateY == state.secondaryPlayerPositionY)
					    || (state.objectEntityId != 0
					        && !state.objectActivated
					        && !state.objectDestroyed
					        && !state.objectMissilePassable
					        && state.objectLevelId == state.combatTargetLevelId
					        && candidateX == state.objectPositionX
					        && candidateY == state.objectPositionY))
						continue;
					state.combatTargetMoving = true;
					state.combatTargetFuturePositionX = candidateX;
					state.combatTargetFuturePositionY = candidateY;
					state.combatTargetDirection = candidateDirection;
					return;
				}
			} else {
				std::vector<std::pair<int32_t, int32_t>> destinations;
				for (int32_t offsetY = -1; offsetY <= 1; ++offsetY) {
					for (int32_t offsetX = -1; offsetX <= 1; ++offsetX) {
						if (offsetX == 0 && offsetY == 0)
							continue;
						const int32_t candidateX = state.combatTargetPositionX + offsetX;
						const int32_t candidateY = state.combatTargetPositionY + offsetY;
						if (candidateX < 0 || candidateX >= 40
						    || candidateY < 0 || candidateY >= 40
						    || (candidateX == state.positionX && candidateY == state.positionY)
						    || (state.secondaryPlayerEntityId != 0
						        && candidateX == state.secondaryPlayerPositionX
						        && candidateY == state.secondaryPlayerPositionY)
						    || (state.objectEntityId != 0
						        && !state.objectActivated
						        && !state.objectDestroyed
						        && !state.objectMissilePassable
						        && state.objectLevelId == state.combatTargetLevelId
						        && candidateX == state.objectPositionX
						        && candidateY == state.objectPositionY))
							continue;
						const int candidateDistance = std::max(
						    std::abs(candidateX - target.positionX),
						    std::abs(candidateY - target.positionY));
						if (candidateDistance >= distance)
							destinations.emplace_back(candidateX, candidateY);
					}
				}
				std::sort(destinations.begin(), destinations.end(), [&](const auto &left, const auto &right) {
					const int leftDistance = std::max(
					    std::abs(left.first - target.positionX),
					    std::abs(left.second - target.positionY));
					const int rightDistance = std::max(
					    std::abs(right.first - target.positionX),
					    std::abs(right.second - target.positionY));
					return leftDistance != rightDistance
					    ? leftDistance > rightDistance
					    : left.second != right.second
					        ? left.second < right.second
					        : left.first < right.first;
				});
				if (!destinations.empty()) {
					state.combatTargetMoving = true;
					state.combatTargetFuturePositionX = destinations.front().first;
					state.combatTargetFuturePositionY = destinations.front().second;
					state.combatTargetDirection = GetDirection(
					    state.combatTargetPositionX,
					    state.combatTargetPositionY,
					    state.combatTargetFuturePositionX,
					    state.combatTargetFuturePositionY);
					return;
				}
			}
		}
	}

	uint32_t nextEntityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);
	const auto velocity = CalculateVelocity(
	    state.combatTargetPositionX,
	    state.combatTargetPositionY,
	    target.positionX,
	    target.positionY,
	    16);
	ReplayFixtureProjectileState projectile;
	projectile.entityId = nextEntityId;
	projectile.sourceEntityId = state.combatTargetEntityId;
	projectile.targetEntityId = target.entityId;
	projectile.spellId = 36;
	projectile.levelId = state.combatTargetLevelId;
	projectile.positionX = state.combatTargetPositionX;
	projectile.positionY = state.combatTargetPositionY;
	projectile.startX = projectile.positionX;
	projectile.startY = projectile.positionY;
	projectile.targetX = target.positionX;
	projectile.targetY = target.positionY;
	projectile.damage = 4;
	projectile.damageType = 3;
	projectile.remainingTicks = 256;
	projectile.behavior = BloodStarBehavior;
	projectile.originalDirection = GetDirection(
	    projectile.positionX,
	    projectile.positionY,
	    projectile.targetX,
	    projectile.targetY);
	projectile.velocityX = velocity.first;
	projectile.velocityY = velocity.second;
	projectile.speed = 16;
	projectile.impactLifetime = 1;
	projectile.resistantDamageDivisor = 1;
	state.projectiles.push_back(std::move(projectile));
	state.combatTargetRangedDelayTicks = state.combatTargetMonsterAiSeed != 0
	    ? std::max(GenerateRnd(state, 20), 0)
	    : static_cast<int32_t>(((static_cast<uint32_t>(tick)
	        ^ state.combatTargetEntityId
	        ^ target.entityId) / 200U) % 20U);
}

/** Admits the compact Lich-family generic MonsterMagic ranged branch. */
void AdmitReplayAutonomousMonsterMagic(
    ReplayFixtureExecutionState &state)
{
	if (state.combatTargetEntityId == 0
	    || (state.combatTargetNativeAi != "Lich"
	        && state.combatTargetNativeAi != "ArchLich"
	        && state.combatTargetNativeAi != "Psychorb"
	        && state.combatTargetNativeAi != "Necromorb"
	        && state.combatTargetNativeAi != "BoneDemon"
	        && state.combatTargetNativeAi != "Magma")
	    || state.combatTargetHitPointsQ6 <= 0
	    || state.combatTargetAggroRange <= 0
	    || std::any_of(state.projectiles.begin(), state.projectiles.end(), [&](const auto &projectile) {
			return projectile.sourceEntityId == state.combatTargetEntityId
			    && projectile.behavior == MonsterMagicBehavior;
		}))
		return;

	struct Candidate {
		uint32_t entityId;
		int32_t positionX;
		int32_t positionY;
	};
	std::vector<Candidate> candidates;
	if (state.life > 0 && state.levelId == state.combatTargetLevelId)
		candidates.push_back({ state.entityId, state.positionX, state.positionY });
	if (state.secondaryPlayerEntityId != 0
	    && state.secondaryPlayerLife > 0
	    && state.secondaryPlayerLevelId == state.combatTargetLevelId)
		candidates.push_back({ state.secondaryPlayerEntityId, state.secondaryPlayerPositionX, state.secondaryPlayerPositionY });
	if (candidates.empty())
		return;
	const auto candidateDistance = [&](const Candidate &candidate) {
		return std::max(
		    std::abs(candidate.positionX - state.combatTargetPositionX),
		    std::abs(candidate.positionY - state.combatTargetPositionY));
	};
	std::sort(candidates.begin(), candidates.end(), [&](const auto &left, const auto &right) {
		const int32_t leftDistance = candidateDistance(left);
		const int32_t rightDistance = candidateDistance(right);
		return leftDistance != rightDistance
		    ? leftDistance < rightDistance
		    : left.entityId < right.entityId;
	});
	const Candidate &target = candidates.front();
	if (candidateDistance(target) > state.combatTargetAggroRange)
		return;

	uint32_t nextEntityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);
	const auto velocity = CalculateVelocity(
	    state.combatTargetPositionX,
	    state.combatTargetPositionY,
	    target.positionX,
	    target.positionY,
	    16);
	ReplayFixtureProjectileState projectile;
	projectile.entityId = nextEntityId;
	projectile.sourceEntityId = state.combatTargetEntityId;
	projectile.targetEntityId = target.entityId;
	projectile.spellId = 1;
	projectile.levelId = state.combatTargetLevelId;
	projectile.positionX = state.combatTargetPositionX;
	projectile.positionY = state.combatTargetPositionY;
	projectile.startX = projectile.positionX;
	projectile.startY = projectile.positionY;
	projectile.targetX = target.positionX;
	projectile.targetY = target.positionY;
	projectile.damage = 4;
	projectile.damageType = state.combatTargetNativeAi == "Magma" ? 1 : 3;
	projectile.remainingTicks = 256;
	projectile.behavior = MonsterMagicBehavior;
	projectile.originalDirection = GetDirection(
	    projectile.positionX,
	    projectile.positionY,
	    projectile.targetX,
	    projectile.targetY);
	projectile.velocityX = velocity.first;
	projectile.velocityY = velocity.second;
	projectile.speed = 16;
	projectile.impactLifetime = 1;
	projectile.resistantDamageDivisor = 1;
	if (state.combatTargetNativeAi == "Magma") {
		projectile.traveledX = projectile.velocityX * 3;
		projectile.traveledY = projectile.velocityY * 3;
		const auto initialTile = CalculateReplayMissileTile(
		    projectile.startX,
		    projectile.startY,
		    projectile.traveledX,
		    projectile.traveledY);
		projectile.positionX = initialTile.first;
		projectile.positionY = initialTile.second;
	}
	state.projectiles.push_back(std::move(projectile));
}

/** Admits the compact FireBat native AiRanged Firebolt branch. */
void AdmitReplayAutonomousFirebolt(ReplayFixtureExecutionState &state)
{
	if (state.combatTargetEntityId == 0
	    || state.combatTargetNativeAi != "FireBat"
	    || state.combatTargetHitPointsQ6 <= 0
	    || state.combatTargetAggroRange <= 0
	    || std::any_of(state.projectiles.begin(), state.projectiles.end(), [&](const auto &projectile) {
			return projectile.sourceEntityId == state.combatTargetEntityId
			    && projectile.behavior == FireboltBehavior;
		}))
		return;

	struct Candidate {
		uint32_t entityId;
		int32_t positionX;
		int32_t positionY;
	};
	std::vector<Candidate> candidates;
	if (state.life > 0 && state.levelId == state.combatTargetLevelId)
		candidates.push_back({ state.entityId, state.positionX, state.positionY });
	if (state.secondaryPlayerEntityId != 0
	    && state.secondaryPlayerLife > 0
	    && state.secondaryPlayerLevelId == state.combatTargetLevelId)
		candidates.push_back({ state.secondaryPlayerEntityId, state.secondaryPlayerPositionX, state.secondaryPlayerPositionY });
	if (candidates.empty())
		return;
	const auto candidateDistance = [&](const Candidate &candidate) {
		return std::max(
		    std::abs(candidate.positionX - state.combatTargetPositionX),
		    std::abs(candidate.positionY - state.combatTargetPositionY));
	};
	std::sort(candidates.begin(), candidates.end(), [&](const auto &left, const auto &right) {
		const int32_t leftDistance = candidateDistance(left);
		const int32_t rightDistance = candidateDistance(right);
		return leftDistance != rightDistance
		    ? leftDistance < rightDistance
		    : left.entityId < right.entityId;
	});
	const Candidate &target = candidates.front();
	if (candidateDistance(target) > state.combatTargetAggroRange)
		return;

	uint32_t nextEntityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);
	const auto velocity = CalculateVelocity(
	    state.combatTargetPositionX,
	    state.combatTargetPositionY,
	    target.positionX,
	    target.positionY,
	    16);
	ReplayFixtureProjectileState projectile;
	projectile.entityId = nextEntityId;
	projectile.sourceEntityId = state.combatTargetEntityId;
	projectile.targetEntityId = target.entityId;
	projectile.spellId = 1;
	projectile.levelId = state.combatTargetLevelId;
	projectile.positionX = state.combatTargetPositionX;
	projectile.positionY = state.combatTargetPositionY;
	projectile.startX = projectile.positionX;
	projectile.startY = projectile.positionY;
	projectile.targetX = target.positionX;
	projectile.targetY = target.positionY;
	projectile.damage = 4;
	projectile.damageType = 1;
	projectile.remainingTicks = 256;
	projectile.behavior = FireboltBehavior;
	projectile.originalDirection = GetDirection(
	    projectile.positionX,
	    projectile.positionY,
	    projectile.targetX,
	    projectile.targetY);
	projectile.velocityX = velocity.first;
	projectile.velocityY = velocity.second;
	projectile.speed = 16;
	projectile.impactLifetime = 1;
	projectile.resistantDamageDivisor = 1;
	state.projectiles.push_back(std::move(projectile));
}

/** Admits the compact Torchant native AiRanged Fireball branch. */
void AdmitReplayAutonomousFireball(ReplayFixtureExecutionState &state)
{
	if (state.combatTargetEntityId == 0
	    || state.combatTargetNativeAi != "Torchant"
	    || state.combatTargetHitPointsQ6 <= 0
	    || state.combatTargetAggroRange <= 0
	    || std::any_of(state.projectiles.begin(), state.projectiles.end(), [&](const auto &projectile) {
			return projectile.sourceEntityId == state.combatTargetEntityId
			    && projectile.behavior == FireballBehavior;
		}))
		return;

	struct Candidate {
		uint32_t entityId;
		int32_t positionX;
		int32_t positionY;
	};
	std::vector<Candidate> candidates;
	if (state.life > 0 && state.levelId == state.combatTargetLevelId)
		candidates.push_back({ state.entityId, state.positionX, state.positionY });
	if (state.secondaryPlayerEntityId != 0
	    && state.secondaryPlayerLife > 0
	    && state.secondaryPlayerLevelId == state.combatTargetLevelId)
		candidates.push_back({ state.secondaryPlayerEntityId, state.secondaryPlayerPositionX, state.secondaryPlayerPositionY });
	if (candidates.empty())
		return;
	const auto candidateDistance = [&](const Candidate &candidate) {
		return std::max(
		    std::abs(candidate.positionX - state.combatTargetPositionX),
		    std::abs(candidate.positionY - state.combatTargetPositionY));
	};
	std::sort(candidates.begin(), candidates.end(), [&](const auto &left, const auto &right) {
		const int32_t leftDistance = candidateDistance(left);
		const int32_t rightDistance = candidateDistance(right);
		return leftDistance != rightDistance
		    ? leftDistance < rightDistance
		    : left.entityId < right.entityId;
	});
	const Candidate &target = candidates.front();
	if (candidateDistance(target) > state.combatTargetAggroRange)
		return;

	uint32_t nextEntityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);
	const auto velocity = CalculateVelocity(
	    state.combatTargetPositionX,
	    state.combatTargetPositionY,
	    target.positionX,
	    target.positionY,
	    16);
	ReplayFixtureProjectileState projectile;
	projectile.entityId = nextEntityId;
	projectile.sourceEntityId = state.combatTargetEntityId;
	projectile.targetEntityId = target.entityId;
	projectile.spellId = 12;
	projectile.levelId = state.combatTargetLevelId;
	projectile.positionX = state.combatTargetPositionX;
	projectile.positionY = state.combatTargetPositionY;
	projectile.startX = projectile.positionX;
	projectile.startY = projectile.positionY;
	projectile.targetX = target.positionX;
	projectile.targetY = target.positionY;
	projectile.damage = 4;
	projectile.damageType = 1;
	projectile.remainingTicks = 256;
	projectile.behavior = FireballBehavior;
	projectile.originalDirection = GetDirection(
	    projectile.positionX,
	    projectile.positionY,
	    projectile.targetX,
	    projectile.targetY);
	projectile.velocityX = velocity.first;
	projectile.velocityY = velocity.second;
	projectile.speed = 16;
	projectile.impactLifetime = 1;
	projectile.resistantDamageDivisor = 1;
	state.projectiles.push_back(std::move(projectile));
}

/** Admits the compact Storm native AiRangedAvoidance ThinLightning branch. */
void AdmitReplayAutonomousStorm(ReplayFixtureExecutionState &state)
{
	if (state.combatTargetEntityId == 0
	    || state.combatTargetNativeAi != "Storm"
	    || state.combatTargetHitPointsQ6 <= 0
	    || state.combatTargetAggroRange <= 0
	    || std::any_of(state.projectiles.begin(), state.projectiles.end(), [&](const auto &projectile) {
			return projectile.sourceEntityId == state.combatTargetEntityId
			    && projectile.behavior == LightningBehavior;
		}))
		return;

	struct Candidate {
		uint32_t entityId;
		int32_t positionX;
		int32_t positionY;
	};
	std::vector<Candidate> candidates;
	if (state.life > 0 && state.levelId == state.combatTargetLevelId)
		candidates.push_back({ state.entityId, state.positionX, state.positionY });
	if (state.secondaryPlayerEntityId != 0
	    && state.secondaryPlayerLife > 0
	    && state.secondaryPlayerLevelId == state.combatTargetLevelId)
		candidates.push_back({ state.secondaryPlayerEntityId, state.secondaryPlayerPositionX, state.secondaryPlayerPositionY });
	if (candidates.empty())
		return;
	const auto candidateDistance = [&](const Candidate &candidate) {
		return std::max(
		    std::abs(candidate.positionX - state.combatTargetPositionX),
		    std::abs(candidate.positionY - state.combatTargetPositionY));
	};
	std::sort(candidates.begin(), candidates.end(), [&](const auto &left, const auto &right) {
		const int32_t leftDistance = candidateDistance(left);
		const int32_t rightDistance = candidateDistance(right);
		return leftDistance != rightDistance
		    ? leftDistance < rightDistance
		    : left.entityId < right.entityId;
	});
	const Candidate &target = candidates.front();
	if (candidateDistance(target) > state.combatTargetAggroRange)
		return;

	uint32_t nextEntityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);
	const auto velocity = CalculateVelocity(
	    state.combatTargetPositionX,
	    state.combatTargetPositionY,
	    target.positionX,
	    target.positionY,
	    32);
	ReplayFixtureProjectileState projectile;
	projectile.entityId = nextEntityId;
	projectile.sourceEntityId = state.combatTargetEntityId;
	projectile.targetEntityId = target.entityId;
	projectile.spellId = 3;
	projectile.levelId = state.combatTargetLevelId;
	projectile.positionX = state.combatTargetPositionX;
	projectile.positionY = state.combatTargetPositionY;
	projectile.startX = projectile.positionX;
	projectile.startY = projectile.positionY;
	projectile.targetX = target.positionX;
	projectile.targetY = target.positionY;
	projectile.damageType = 2;
	projectile.remainingTicks = 256;
	projectile.behavior = LightningBehavior;
	projectile.originalDirection = GetDirection(
	    projectile.positionX,
	    projectile.positionY,
	    projectile.targetX,
	    projectile.targetY);
	projectile.velocityX = velocity.first;
	projectile.velocityY = velocity.second;
	projectile.speed = 32;
	projectile.impactLifetime = 1;
	projectile.resistantDamageDivisor = 1;
	state.projectiles.push_back(std::move(projectile));
}

void AdvanceReplayStateToTick(
    ReplayFixtureExecutionState &state,
    const ReplayFixture &fixture,
    uint64_t tick)
{
	for (uint64_t simulationTick = state.lastAppliedTick + 1; simulationTick <= tick; ++simulationTick) {
		AdvanceReplayPlayerAction(state, simulationTick);
		if (state.multiplayer && state.combatTargetMonsterAiSeed != 0) {
			state.randomState = state.combatTargetMonsterAiSeed;
			state.combatTargetMonsterAiSeed = AdvanceRandomItemSeed(state);
		}
		AdvanceReplayMonsterAttack(
		    state,
		    fixture.playerActionTiming,
		    simulationTick);
		if (state.secondaryPlayerCombatModeEndTick != 0
		    && simulationTick >= state.secondaryPlayerCombatModeEndTick) {
			state.secondaryPlayerCombatMode = 0;
			state.secondaryPlayerCombatModeEndTick = 0;
		}
		for (auto &effect : state.statusEffects) {
			if (effect.remainingTicks > 0 && effect.behavior != EtherealizeStatusBehavior)
				--effect.remainingTicks;
		}
		state.statusEffects.erase(std::remove_if(state.statusEffects.begin(), state.statusEffects.end(), [](const auto &effect) {
			return effect.remainingTicks == 0;
		}),
		    state.statusEffects.end());
		AdvanceReplayMonsterRegeneration(state);
		const int32_t rallyTicksBefore = state.combatTargetFallenRallyTicks;
		if (!AdvanceReplayFallenStandAnimation(state))
			AdvanceReplayFallenRallyPursuit(state);
		AdvanceReplayFallenRallyCompanion(
			state,
			state.combatTargetFallenRallyTicks > rallyTicksBefore);
		state.combatTargetAutonomousActionHandled =
			AdvanceReplayAutonomousGargoyleHealing(state);
		AdmitReplayAutonomousHorkSpawn(state, simulationTick);
		AdmitReplayAutonomousBloodStar(state, simulationTick);
		AdmitReplayAutonomousMegaInferno(state, simulationTick);
		AdmitReplayAutonomousMonsterMagic(state);
		AdmitReplayAutonomousFirebolt(state);
		AdmitReplayAutonomousFireball(state);
		AdmitReplayAutonomousStorm(state);
		AdmitReplayAutonomousMonsterArrow(state, simulationTick);
		AdvanceReplayHorkSpawnsOneTick(state, simulationTick);
		AdvanceReplayWeaponArrowsOneTick(state);
		AdvanceReplayMonsterArrowsOneTick(state);
		AdvanceReplaySnakeChargesOneTick(state, simulationTick);
		AdvanceReplayPlayerApocalypseOneTick(state);
		AdvanceReplayPlayerInfernoOneTick(state);
		AdvanceReplayInfernoOneTick(state);
		AdvanceReplayRingOfFireOneTick(state);
		AdvanceReplayFlameWaveOneTick(state);
		AdvanceReplayNovaOneTick(state);
		AdvanceReplayNovaBallsOneTick(state);
		AdvanceReplayChainLightningOneTick(state);
		AdvanceReplayImmolationOneTick(state);
		AdvanceReplayElementalOneTick(state);
		AdvanceReplayGuardianOneTick(state);
		AdvanceReplayRageOneTick(state);
		AdvanceReplayFireWallsOneTick(state);
		AdvanceReplayAcidOneTick(state, simulationTick);
		AdvanceReplayAutonomousMonsterMagicOneTick(state, simulationTick);
		AdvanceChargedBoltsOneTick(state, simulationTick);
		AdvanceMagicBoltsOneTick(state, simulationTick);
		AdvanceFireballsOneTick(state, simulationTick);
		AdvanceBoneSpiritsOneTick(state, simulationTick);
		AdvanceLightningOneTick(state, simulationTick);
		AdvanceFlashOneTick(state);
		AdvanceMovementControllersOneTick(state);
		AdvanceResurrectionBeamsOneTick(state);
		AdvanceStoneCursesOneTick(state);
	}
	state.lastAppliedTick = std::max(state.lastAppliedTick, tick);
}

class JsonReader {
public:
	JsonReader(std::string_view input, ReplayFixture &fixture, std::string &error)
	    : input_(input)
	    , fixture_(fixture)
	    , error_(error)
	{
	}

	[[nodiscard]] bool Parse()
	{
		SkipWhitespace();
		if (!ParseObject([this](std::string_view key) { return ParseFixtureField(key); }))
			return false;
		SkipWhitespace();
		return position_ == input_.size() || Fail("unexpected trailing data");
	}

private:
	template <typename FieldParser>
	bool ParseObject(FieldParser &&parseField)
	{
		if (!Consume('{'))
			return Fail("expected object");
		SkipWhitespace();
		if (Consume('}'))
			return true;
		while (true) {
			std::string key;
			if (!ParseString(key))
				return false;
			SkipWhitespace();
			if (!Consume(':'))
				return Fail("expected colon");
			SkipWhitespace();
			if (!parseField(key))
				return false;
			SkipWhitespace();
			if (Consume('}'))
				return true;
			if (!Consume(','))
				return Fail("expected comma or closing brace");
			SkipWhitespace();
		}
	}

	bool ParseFixtureField(std::string_view key)
	{
		if (key == "format_version")
			return ParseUnsigned(fixture_.formatVersion);
		if (key == "fixture_id")
			return ParseString(fixture_.fixtureId);
		if (key == "protocol_schema_version")
			return ParseString(fixture_.protocolSchemaVersion);
		if (key == "tick_rate_hz")
			return ParseUnsigned(fixture_.tickRateHz);
		if (key == "rng_seed")
			return ParseUnsigned(fixture_.rngSeed);
		if (key == "game_variant")
			return ParseString(fixture_.gameVariant);
		if (key == "game_mode")
			return ParseString(fixture_.gameMode);
		if (key == "content_manifest")
			return ParseContentManifest();
		if (key == "quest_selection")
			return ParseQuestSelection();
		if (key == "initial_state")
			return ParseInitialState();
		if (key == "initial_projectiles")
			return ParseInitialProjectiles();
		if (key == "legacy_store_state")
			return ParseStoreState();
		if (key == "player_action_timing")
			return ParsePlayerActionTiming();
		if (key == "commands")
			return ParseCommands();
		if (key == "checkpoints")
			return ParseCheckpoints();
		return SkipValue();
	}

	bool ParsePlayerActionTiming()
	{
		auto &timing = fixture_.playerActionTiming;
		timing.defined = true;
		return ParseObject([this, &timing](std::string_view key) {
			if (key == "walking_frames") {
				if (!ParseUnsigned(timing.walkingFrames))
					return false;
				timing.definedFields |= 1U << 0;
				return true;
			}
			if (key == "attack_frames") {
				if (!ParseUnsigned(timing.attackFrames))
					return false;
				timing.definedFields |= 1U << 1;
				return true;
			}
			if (key == "attack_action_frame") {
				if (!ParseUnsigned(timing.attackActionFrame))
					return false;
				timing.definedFields |= 1U << 2;
				return true;
			}
			if (key == "casting_frames") {
				if (!ParseUnsigned(timing.castingFrames))
					return false;
				timing.definedFields |= 1U << 3;
				return true;
			}
			if (key == "casting_action_frame") {
				if (!ParseUnsigned(timing.castingActionFrame))
					return false;
				timing.definedFields |= 1U << 4;
				return true;
			}
			if (key == "death_frames") {
				if (!ParseUnsigned(timing.deathFrames))
					return false;
				timing.definedFields |= 1U << 5;
				return true;
			}
			return SkipValue();
		});
	}

	bool ParseContentManifest()
	{
		if (position_ >= input_.size())
			return Fail("expected content manifest");
		if (input_[position_] == '[')
			return ParseContentManifestPacks();
		if (input_[position_] != '{')
			return Fail("expected content manifest object or pack array");
		return ParseObject([this](std::string_view key) {
			if (key == "id")
				return ParseString(fixture_.contentManifest.id);
			if (key == "version")
				return ParseString(fixture_.contentManifest.version);
			if (key == "sha256")
				return ParseString(fixture_.contentManifest.sha256);
			return SkipValue();
		});
	}

	bool ParseContentManifestPacks()
	{
		if (!Consume('['))
			return Fail("expected content manifest pack array");
		fixture_.contentManifest.packs.clear();
		SkipWhitespace();
		if (Consume(']'))
			return true;
		while (true) {
			std::string pack;
			if (!ParseString(pack))
				return false;
			fixture_.contentManifest.packs.push_back(std::move(pack));
			SkipWhitespace();
			if (Consume(']'))
				return true;
			if (!Consume(','))
				return Fail("expected comma or closing bracket");
			SkipWhitespace();
		}
	}

	bool ParseQuestSelection()
	{
		fixture_.questSelection = {};
		fixture_.questSelection.defined = true;
		bool hasSeed = false;
		bool hasPools = false;
		const bool parsed = ParseObject([this, &hasSeed, &hasPools](std::string_view key) {
			if (key == "seed") {
				hasSeed = true;
				return ParseUnsigned(fixture_.questSelection.seed);
			}
			if (key != "pools")
				return SkipValue();
			hasPools = true;
			if (!Consume('['))
				return Fail("expected quest selection pool array");
			SkipWhitespace();
			if (Consume(']'))
				return Fail("quest selection must define at least one pool");
			while (true) {
				ReplayFixtureQuestPool pool;
				if (!ParseObject([this, &pool](std::string_view poolKey) {
					if (poolKey == "pool_id")
						return ParseUnsigned(pool.poolId);
					if (poolKey != "quest_ids")
						return SkipValue();
					if (!Consume('['))
						return Fail("expected quest selection quest id array");
					SkipWhitespace();
					if (Consume(']'))
						return Fail("quest selection pool must define at least two quests");
					while (true) {
						uint32_t questId = 0;
						if (!ParseUnsigned(questId))
							return false;
						pool.questIds.push_back(questId);
						SkipWhitespace();
						if (Consume(']'))
							break;
						if (!Consume(','))
							return Fail("expected comma or closing bracket");
						SkipWhitespace();
					}
					return true;
				}))
					return false;
				auto sortedQuestIds = pool.questIds;
				std::sort(sortedQuestIds.begin(), sortedQuestIds.end());
				if (pool.poolId == 0 || pool.questIds.size() < 2
				    || std::any_of(pool.questIds.begin(), pool.questIds.end(), [](uint32_t questId) { return questId == 0; })
				    || std::adjacent_find(sortedQuestIds.begin(), sortedQuestIds.end()) != sortedQuestIds.end()
				    || std::any_of(fixture_.questSelection.pools.begin(), fixture_.questSelection.pools.end(), [&](const auto &existing) {
						return existing.poolId == pool.poolId;
					})) {
					return Fail("quest selection contains duplicate or invalid identities");
				}
				std::sort(pool.questIds.begin(), pool.questIds.end());
				fixture_.questSelection.pools.push_back(std::move(pool));
				SkipWhitespace();
				if (Consume(']'))
					break;
				if (!Consume(','))
					return Fail("expected comma or closing bracket");
				SkipWhitespace();
			}
			std::sort(fixture_.questSelection.pools.begin(), fixture_.questSelection.pools.end(), [](const auto &left, const auto &right) {
				return left.poolId < right.poolId;
			});
			return true;
		});
		if (!parsed)
			return false;
		if (!hasSeed || !hasPools)
			return Fail("quest selection must define seed and pools");
		return true;
	}

	bool ParseInitialCorpses()
	{
		if (!Consume('['))
			return Fail("expected corpse array");
		fixture_.initialState.corpses.clear();
		SkipWhitespace();
		if (Consume(']'))
			return true;
		while (true) {
			ReplayFixtureCorpseState corpse;
			if (!ParseObject([this, &corpse](std::string_view key) {
				if (key == "entity_id")
					return ParseUnsigned(corpse.entityId);
				if (key == "level_id")
					return ParseUnsigned(corpse.levelId);
				if (key == "position_x")
					return ParseSigned(corpse.positionX);
				if (key == "position_y")
					return ParseSigned(corpse.positionY);
				return SkipValue();
			}))
				return false;
			if (corpse.entityId == 0)
				return Fail("corpse entity identity must be non-zero");
			if (std::any_of(
			        fixture_.initialState.corpses.begin(),
			        fixture_.initialState.corpses.end(),
			        [&](const auto &existing) {
				        return existing.entityId == corpse.entityId;
			        }))
				return Fail("duplicate corpse entity identity");
			fixture_.initialState.corpses.push_back(corpse);
			SkipWhitespace();
			if (Consume(']'))
				return true;
			if (!Consume(','))
				return Fail("expected comma or closing bracket");
			SkipWhitespace();
		}
	}

	bool ParseInitialState()
	{
		return ParseObject([this](std::string_view key) {
			if (key == "player")
				return ParseString(fixture_.initialState.player);
			if (key == "character_name")
				return ParseString(fixture_.initialState.characterName);
			if (key == "mana_shield_active")
				return ParseBool(fixture_.initialState.manaShieldActive);
			if (key == "reflect_charges")
				return ParseUnsigned(fixture_.initialState.reflectCharges);
			if (key == "gold")
				return ParseUnsigned(fixture_.initialState.gold);
			if (key == "experience")
				return ParseUnsigned(fixture_.initialState.experience);
			if (key == "life")
				return ParseSigned(fixture_.initialState.life);
			if (key == "life_maximum")
				return ParseSigned(fixture_.initialState.lifeMaximum);
			if (key == "mana")
				return ParseSigned(fixture_.initialState.mana);
			if (key == "mana_maximum")
				return ParseSigned(fixture_.initialState.manaMaximum);
			if (key == "magic")
				return ParseSigned(fixture_.initialState.magic);
			if (key == "strength")
				return ParseSigned(fixture_.initialState.strength);
			if (key == "dexterity")
				return ParseSigned(fixture_.initialState.dexterity);
			if (key == "vitality")
				return ParseSigned(fixture_.initialState.vitality);
			if (key == "character_class")
				return ParseSigned(fixture_.initialState.characterClass);
			if (key == "character_level")
				return ParseUnsigned(fixture_.initialState.characterLevel);
			if (key == "position_x")
				return ParseSigned(fixture_.initialState.positionX);
			if (key == "position_y")
				return ParseSigned(fixture_.initialState.positionY);
			if (key == "level_id") {
				uint32_t levelId = 0;
				if (!ParseUnsigned(levelId))
					return false;
				fixture_.initialState.levelId = levelId;
				return true;
			}
			if (key == "warp_id")
				return ParseUnsigned(fixture_.initialState.warpId);
			if (key == "warp_source_level_id")
				return ParseUnsigned(fixture_.initialState.warpSourceLevelId);
			if (key == "warp_trigger_x")
				return ParseSigned(fixture_.initialState.warpTriggerX);
			if (key == "warp_trigger_y")
				return ParseSigned(fixture_.initialState.warpTriggerY);
			if (key == "warp_target_level_id")
				return ParseUnsigned(fixture_.initialState.warpTargetLevelId);
			if (key == "warp_target_x")
				return ParseSigned(fixture_.initialState.warpTargetX);
			if (key == "warp_target_y")
				return ParseSigned(fixture_.initialState.warpTargetY);
			if (key == "facing_direction")
				return ParseUnsigned(fixture_.initialState.facingDirection);
			if (key == "suppress_multiplayer_death_drops")
				return ParseBool(fixture_.initialState.suppressMultiplayerDeathDrops);
			if (key == "gold_stack_seed")
				return ParseUnsigned(fixture_.initialState.goldStackSeed);
			if (key == "gold_stack_stable_item_id")
				return ParseUnsigned(fixture_.initialState.goldStackStableItemId);
			if (key == "gold_stack_cell")
				return ParseUnsigned(fixture_.initialState.goldStackCell);
			if (key == "inventory_filler_count")
				return ParseUnsigned(fixture_.initialState.inventoryFillerCount);
			if (key == "world_item_entity_id")
				return ParseUnsigned(fixture_.initialState.worldItemEntityId);
			if (key == "world_item_seed")
				return ParseUnsigned(fixture_.initialState.worldItemSeed);
			if (key == "world_item_price")
				return ParseUnsigned(fixture_.initialState.worldItemPrice);
			if (key == "object_entity_id")
				return ParseUnsigned(fixture_.initialState.objectEntityId);
			if (key == "object_id")
				return ParseUnsigned(fixture_.initialState.objectId);
			if (key == "object_quest_id")
				return ParseUnsigned(fixture_.initialState.objectQuestId);
			if (key == "object_position_x")
				return ParseSigned(fixture_.initialState.objectPositionX);
			if (key == "object_position_y")
				return ParseSigned(fixture_.initialState.objectPositionY);
			if (key == "object_effect_kind")
				return ParseSigned(fixture_.initialState.objectEffectKind);
			if (key == "object_effect_amount")
				return ParseSigned(fixture_.initialState.objectEffectAmount);
			if (key == "object_missile_passable")
				return ParseBool(fixture_.initialState.objectMissilePassable);
			if (key == "object_breakable")
				return ParseBool(fixture_.initialState.objectBreakable);
			if (key == "object_destroyed")
				return ParseBool(fixture_.initialState.objectDestroyed);
			if (key == "combat_target_entity_id")
				return ParseUnsigned(fixture_.initialState.combatTargetEntityId);
			if (key == "combat_target_level_id")
				return ParseUnsigned(fixture_.initialState.combatTargetLevelId);
			if (key == "combat_target_position_x")
				return ParseSigned(fixture_.initialState.combatTargetPositionX);
			if (key == "combat_target_position_y")
				return ParseSigned(fixture_.initialState.combatTargetPositionY);
			if (key == "combat_target_hit_points")
				return ParseSigned(fixture_.initialState.combatTargetHitPoints);
			if (key == "combat_target_max_hit_points")
				return ParseSigned(fixture_.initialState.combatTargetMaxHitPoints);
			if (key == "combat_target_armor_class")
				return ParseSigned(fixture_.initialState.combatTargetArmorClass);
			if (key == "combat_target_attack_damage")
				return ParseSigned(fixture_.initialState.combatTargetAttackDamage);
			if (key == "combat_target_aggro_range")
				return ParseSigned(fixture_.initialState.combatTargetAggroRange);
			if (key == "combat_target_native_ai")
				return ParseString(fixture_.initialState.combatTargetNativeAi);
			if (key == "combat_target_monster_item_seed")
				return ParseUnsigned(fixture_.initialState.combatTargetMonsterItemSeed);
			if (key == "combat_target_monster_ai_seed")
				return ParseUnsigned(fixture_.initialState.combatTargetMonsterAiSeed);
			if (key == "combat_target_monster_id")
				return ParseUnsigned(fixture_.initialState.combatTargetMonsterId);
			if (key == "combat_target_to_hit")
				return ParseSigned(fixture_.initialState.combatTargetToHit);
			if (key == "combat_target_to_hit_special")
				return ParseSigned(fixture_.initialState.combatTargetToHitSpecial);
			if (key == "combat_target_min_damage")
				return ParseSigned(fixture_.initialState.combatTargetMinDamage);
			if (key == "combat_target_max_damage")
				return ParseSigned(fixture_.initialState.combatTargetMaxDamage);
			if (key == "combat_target_min_damage_special")
				return ParseSigned(fixture_.initialState.combatTargetMinDamageSpecial);
			if (key == "combat_target_max_damage_special")
				return ParseSigned(fixture_.initialState.combatTargetMaxDamageSpecial);
			if (key == "combat_target_berserked")
				return ParseBool(fixture_.initialState.combatTargetBerserked);
			if (key == "combat_target_intelligence")
				return ParseSigned(fixture_.initialState.combatTargetIntelligence);
			if (key == "combat_target_fallen_stand_animation_frame")
				return ParseSigned(fixture_.initialState.combatTargetFallenStandAnimationFrame);
			if (key == "combat_target_fallen_stand_animation_tick")
				return ParseSigned(fixture_.initialState.combatTargetFallenStandAnimationTick);
			if (key == "combat_target_fallen_rally_ticks")
				return ParseSigned(fixture_.initialState.combatTargetFallenRallyTicks);
			if (key == "combat_target_active_for_ticks")
				return ParseSigned(fixture_.initialState.combatTargetActiveForTicks);
			if (key == "combat_target_companion_entity_id")
				return ParseUnsigned(fixture_.initialState.combatTargetCompanion.entityId);
			if (key == "combat_target_companion_level_id")
				return ParseUnsigned(fixture_.initialState.combatTargetCompanion.levelId);
			if (key == "combat_target_companion_position_x")
				return ParseSigned(fixture_.initialState.combatTargetCompanion.positionX);
			if (key == "combat_target_companion_position_y")
				return ParseSigned(fixture_.initialState.combatTargetCompanion.positionY);
			if (key == "combat_target_companion_hit_points")
				return ParseSigned(fixture_.initialState.combatTargetCompanion.hitPoints);
			if (key == "combat_target_companion_max_hit_points")
				return ParseSigned(fixture_.initialState.combatTargetCompanion.maxHitPoints);
			if (key == "combat_target_companion_monster_id")
				return ParseUnsigned(fixture_.initialState.combatTargetCompanion.monsterId);
			if (key == "combat_target_companion_native_ai")
				return ParseString(fixture_.initialState.combatTargetCompanion.nativeAi);
			if (key == "combat_target_companion_intelligence")
				return ParseSigned(fixture_.initialState.combatTargetCompanion.intelligence);
			if (key == "combat_target_companion_fallen_stand_animation_frame")
				return ParseSigned(fixture_.initialState.combatTargetCompanion.fallenStandAnimationFrame);
			if (key == "combat_target_companion_fallen_stand_animation_tick")
				return ParseSigned(fixture_.initialState.combatTargetCompanion.fallenStandAnimationTick);
			if (key == "combat_target_companion_fallen_rally_ticks")
				return ParseSigned(fixture_.initialState.combatTargetCompanion.fallenRallyTicks);
			if (key == "combat_target_gargoyle_special_active")
				return ParseBool(fixture_.initialState.combatTargetGargoyleSpecialActive);
			if (key == "combat_target_hork_demon_delay_ticks")
				return ParseSigned(fixture_.initialState.combatTargetHorkDemonDelayTicks);
			if (key == "combat_target_skeleton_king_delay_ticks")
				return ParseSigned(fixture_.initialState.combatTargetSkeletonKingDelayTicks);
		if (key == "combat_target_skeleton_melee_delay_ticks")
				return ParseSigned(fixture_.initialState.combatTargetSkeletonMeleeDelayTicks);
			if (key == "combat_target_snake_delay_ticks")
				return ParseSigned(fixture_.initialState.combatTargetSnakeDelayTicks);
			if (key == "combat_target_counselor_delay_ticks")
				return ParseSigned(fixture_.initialState.combatTargetCounselorDelayTicks);
			if (key == "combat_target_counselor_retreat_ticks")
				return ParseSigned(fixture_.initialState.combatTargetCounselorRetreatTicks);
			if (key == "combat_target_ranged_delay_ticks")
				return ParseSigned(fixture_.initialState.combatTargetRangedDelayTicks);
			if (key == "combat_target_ranged_retreat_minimum_distance")
				return ParseSigned(fixture_.initialState.combatTargetRangedRetreatMinimumDistance);
			if (key == "combat_target_ranged_retreat_chance_base")
				return ParseSigned(fixture_.initialState.combatTargetRangedRetreatChanceBase);
			if (key == "combat_target_ranged_retreat_chance_intelligence_scale")
				return ParseSigned(fixture_.initialState.combatTargetRangedRetreatChanceIntelligenceScale);
			if (key == "combat_target_mega_delay_ticks")
				return ParseSigned(fixture_.initialState.combatTargetMegaDelayTicks);
			if (key == "combat_target_skeleton_ranged_movement_idle_ticks")
				return ParseSigned(fixture_.initialState.combatTargetSkeletonRangedMovementIdleTicks);
			if (key == "combat_target_skeleton_ranged_movement_mode")
				return ParseSigned(fixture_.initialState.combatTargetSkeletonRangedMovementMode);
			if (key == "combat_target_sneak_hidden")
				return ParseBool(fixture_.initialState.combatTargetSneakHidden);
			if (key == "combat_target_sneak_retreat_ticks")
				return ParseSigned(fixture_.initialState.combatTargetSneakRetreatTicks);
			if (key == "combat_target_bat_retreat_ticks")
				return ParseSigned(fixture_.initialState.combatTargetBatRetreatTicks);
			if (key == "combat_target_bat_retreat_direction")
				return ParseSigned(fixture_.initialState.combatTargetBatRetreatDirection);
			if (key == "combat_target_scavenger_healing_ticks")
				return ParseSigned(fixture_.initialState.combatTargetScavengerHealingTicks);
			if (key == "combat_target_scavenger_corpse_entity_id")
				return ParseUnsigned(fixture_.initialState.combatTargetScavengerCorpseEntityId);
			if (key == "combat_target_fat_movement_idle_ticks")
				return ParseSigned(fixture_.initialState.combatTargetFatMovementIdleTicks);
			if (key == "combat_target_fat_movement_mode")
				return ParseSigned(fixture_.initialState.combatTargetFatMovementMode);
			if (key == "corpses")
				return ParseInitialCorpses();
			if (key == "combat_target_fire_resistance")
				return ParseSigned(fixture_.initialState.combatTargetFireResistance);
			if (key == "combat_target_lightning_resistance")
				return ParseSigned(fixture_.initialState.combatTargetLightningResistance);
			if (key == "combat_target_magic_resistance")
				return ParseSigned(fixture_.initialState.combatTargetMagicResistance);
			if (key == "combat_target_monster_level")
				return ParseSigned(fixture_.initialState.combatTargetMonsterLevel);
			if (key == "combat_target_undead")
				return ParseBool(fixture_.initialState.combatTargetUndead);
			if (key == "combat_target_moving")
				return ParseBool(fixture_.initialState.combatTargetMoving);
			if (key == "combat_target_future_position_x")
				return ParseSigned(fixture_.initialState.combatTargetFuturePositionX);
			if (key == "combat_target_future_position_y")
				return ParseSigned(fixture_.initialState.combatTargetFuturePositionY);
			if (key == "combat_target_petrified")
				return ParseBool(fixture_.initialState.combatTargetPetrified);
			if (key == "combat_target_stone_curse_immune")
				return ParseBool(fixture_.initialState.combatTargetStoneCurseImmune);
			if (key == "quest_id")
				return ParseUnsigned(fixture_.initialState.questId);
			if (key == "quest_required_progress")
				return ParseUnsigned(fixture_.initialState.questRequiredProgress);
			if (key == "status_effect_id")
				return ParseUnsigned(fixture_.initialState.statusEffectId);
			if (key == "status_duration")
				return ParseUnsigned(fixture_.initialState.statusDuration);
			if (key == "status_magnitude")
				return ParseSigned(fixture_.initialState.statusMagnitude);
			if (key == "ability_spell_id")
				return ParseUnsigned(fixture_.initialState.abilitySpellId);
			if (key == "memorized_spells")
				return ParseMemorizedSpells();
			if (key == "equipment")
				return ParseEquipment();
			if (key == "belt")
				return ParseBelt();
			if (key == "secondary_player")
				return ParseSecondaryPlayer();
			return SkipValue();
		});
	}

	bool ParseInitialProjectiles()
	{
		if (!Consume('['))
			return Fail("expected initial projectile array");
		fixture_.initialProjectiles.clear();
		SkipWhitespace();
		if (Consume(']'))
			return true;
		while (true) {
			ReplayFixtureInitialProjectile projectile;
			if (!ParseObject([this, &projectile](std::string_view key) {
				if (key == "entity_id")
					return ParseUnsigned(projectile.entityId);
				if (key == "source_entity_id")
					return ParseUnsigned(projectile.sourceEntityId);
				if (key == "target_entity_id")
					return ParseUnsigned(projectile.targetEntityId);
				if (key == "spell_id")
					return ParseUnsigned(projectile.spellId);
				if (key == "level_id")
					return ParseUnsigned(projectile.levelId);
				if (key == "position_x")
					return ParseSigned(projectile.positionX);
				if (key == "position_y")
					return ParseSigned(projectile.positionY);
				if (key == "target_x")
					return ParseSigned(projectile.targetX);
				if (key == "target_y")
					return ParseSigned(projectile.targetY);
				if (key == "damage")
					return ParseSigned(projectile.damage);
				if (key == "damage_type")
					return ParseSigned(projectile.damageType);
				if (key == "area_radius")
					return ParseSigned(projectile.areaRadius);
				if (key == "remaining_ticks")
					return ParseUnsigned(projectile.remainingTicks);
				if (key == "behavior")
					return ParseSigned(projectile.behavior);
				if (key == "phase")
					return ParseSigned(projectile.phase);
				if (key == "speed")
					return ParseSigned(projectile.speed);
				if (key == "impact_lifetime")
					return ParseSigned(projectile.impactLifetime);
				if (key == "damage_minimum")
					return ParseSigned(projectile.damageMinimum);
				if (key == "damage_maximum")
					return ParseSigned(projectile.damageMaximum);
				return SkipValue();
			})) {
				return false;
			}
			if (projectile.entityId == 0 || projectile.sourceEntityId == 0
			    || projectile.damage < 0 || projectile.damageType < 0
			    || projectile.areaRadius < 0 || projectile.remainingTicks == 0
			    || projectile.behavior < 0 || projectile.phase < 0
			    || projectile.speed < 0 || projectile.impactLifetime < 0
			    || projectile.damageMinimum < 0
			    || projectile.damageMaximum < projectile.damageMinimum
			    || std::any_of(fixture_.initialProjectiles.begin(), fixture_.initialProjectiles.end(), [&](const auto &existing) {
				return existing.entityId == projectile.entityId;
			    })) {
				return Fail("initial projectile contains invalid or duplicate state");
			}
			fixture_.initialProjectiles.push_back(std::move(projectile));
			SkipWhitespace();
			if (Consume(']'))
				return true;
			if (!Consume(','))
				return Fail("expected comma or closing bracket");
			SkipWhitespace();
		}
	}

	bool ParseSecondaryPlayer()
	{
		auto &player = fixture_.initialState.secondaryPlayer;
		player.defined = true;
		return ParseObject([this, &player](std::string_view key) {
			if (key == "player")
				return ParseString(player.player);
			if (key == "character_name")
				return ParseString(player.characterName);
			if (key == "entity_id")
				return ParseUnsigned(player.entityId);
			if (key == "life")
				return ParseSigned(player.life);
			if (key == "life_maximum")
				return ParseSigned(player.lifeMaximum);
			if (key == "gold")
				return ParseUnsigned(player.gold);
			if (key == "position_x")
				return ParseSigned(player.positionX);
			if (key == "position_y")
				return ParseSigned(player.positionY);
			if (key == "level_id")
				return ParseUnsigned(player.levelId);
			if (key == "character_level")
				return ParseUnsigned(player.characterLevel);
			if (key == "character_class")
				return ParseUnsigned(player.characterClass);
			if (key == "magic")
				return ParseSigned(player.magic);
			if (key == "dexterity")
				return ParseSigned(player.dexterity);
			if (key == "friendly_mode")
				return ParseBool(player.friendlyMode);
			if (key == "block_capable")
				return ParseBool(player.blockCapable);
			if (key == "combat_mode")
				return ParseUnsigned(player.combatMode);
			if (key == "combat_mode_end_tick")
				return ParseUnsigned(player.combatModeEndTick);
			if (key == "inventory_filler_count")
				return ParseUnsigned(player.inventoryFillerCount);
			if (key == "equipment")
				return ParseEquipment(player.equipment, "secondary equipment");
			if (key == "inventory_item") {
				player.inventoryItemDefined = true;
				return ParseObject([this, &player](std::string_view itemKey) {
					if (itemKey == "slot")
						return ParseUnsigned(player.inventoryItemSlot);
					if (itemKey == "item_seed")
						return ParseUnsigned(player.inventoryItemSeed);
					if (itemKey == "stable_item_id")
						return ParseUnsigned(player.inventoryItemStableItemId);
					if (itemKey == "price")
						return ParseUnsigned(player.inventoryItemPrice);
					if (itemKey == "item_type")
						return ParseSigned(player.inventoryItemType);
					if (itemKey == "item_index")
						return ParseSigned(player.inventoryItemIndex);
					if (itemKey == "stat_flag")
						return ParseBool(player.inventoryItemStatFlag);
					return SkipValue();
				});
			}
			if (key == "held_item") {
				player.heldItemDefined = true;
				return ParseObject([this, &player](std::string_view itemKey) {
					if (itemKey == "slot")
						return ParseUnsigned(player.heldItemSlot);
					if (itemKey == "item_seed")
						return ParseUnsigned(player.heldItemSeed);
					if (itemKey == "stable_item_id")
						return ParseUnsigned(player.heldItemStableItemId);
					if (itemKey == "price")
						return ParseUnsigned(player.heldItemPrice);
					if (itemKey == "item_type")
						return ParseSigned(player.heldItemType);
					if (itemKey == "item_index")
						return ParseSigned(player.heldItemIndex);
					if (itemKey == "stat_flag")
						return ParseBool(player.heldItemStatFlag);
					return SkipValue();
				});
			}
			return SkipValue();
		});
	}

	bool ParseMemorizedSpells()
	{
		if (!Consume('['))
			return Fail("expected memorized spell array");
		fixture_.initialState.memorizedSpells.clear();
		SkipWhitespace();
		if (Consume(']'))
			return true;
		while (true) {
			ReplayFixtureMemorizedSpell spell;
			if (!ParseObject([this, &spell](std::string_view key) {
				    if (key == "spell_id")
					    return ParseUnsigned(spell.spellId);
				    if (key == "level")
					    return ParseUnsigned(spell.level);
				    return SkipValue();
			    })) {
				return false;
			}
			if (spell.spellId == 0 || spell.spellId > 52 || spell.level == 0 || spell.level > 15)
				return Fail("invalid memorized spell");
			if (std::any_of(fixture_.initialState.memorizedSpells.begin(), fixture_.initialState.memorizedSpells.end(), [&](const auto &candidate) {
				    return candidate.spellId == spell.spellId;
			    })) {
				return Fail("duplicate memorized spell");
			}
			fixture_.initialState.memorizedSpells.push_back(spell);
			SkipWhitespace();
			if (Consume(']'))
				return true;
			if (!Consume(','))
				return Fail("expected comma or closing bracket");
			SkipWhitespace();
		}
	}

	bool ParseEquipment(std::vector<ReplayFixtureEquippedItem> &target, std::string_view description)
	{
		if (!Consume('['))
			return Fail("expected equipment array");
		target.clear();
		SkipWhitespace();
		if (Consume(']'))
			return true;
		while (true) {
			ReplayFixtureEquippedItem item;
			uint32_t definedFields = 0;
			if (!ParseObject([this, &item, &definedFields](std::string_view key) {
				    if (key == "slot") {
					    if (!ParseUnsigned(item.slot))
						    return false;
					    definedFields |= 1U << 0;
					    return true;
				    }
				    if (key == "item_seed") {
					    if (!ParseUnsigned(item.itemSeed))
						    return false;
					    definedFields |= 1U << 1;
					    return true;
				    }
				    if (key == "stable_item_id")
					    return ParseUnsigned(item.stableItemId);
				    if (key == "item_type") {
					    if (!ParseSigned(item.itemType))
						    return false;
					    definedFields |= 1U << 2;
					    return true;
				    }
				    if (key == "item_class") {
					    if (!ParseSigned(item.itemClass))
						    return false;
					    definedFields |= 1U << 3;
					    return true;
				    }
				    if (key == "flags") {
					    if (!ParseUnsigned(item.flags))
						    return false;
					    definedFields |= 1U << 4;
					    return true;
				    }
				    if (key == "stat_flag") {
					    if (!ParseBool(item.statFlag))
						    return false;
					    definedFields |= 1U << 5;
					    return true;
				    }
				    if (key == "item_index")
					    return ParseSigned(item.itemIndex);
				    if (key == "value")
					    return ParseSigned(item.value);
				    if (key == "identified_value")
					    return ParseSigned(item.identifiedValue);
				    if (key == "identified")
					    return ParseBool(item.identified);
				    if (key == "magical")
					    return ParseSigned(item.magical);
				    if (key == "fire_min_damage")
					    return ParseSigned(item.fireMinDamage);
				    if (key == "fire_max_damage")
					    return ParseSigned(item.fireMaxDamage);
				    if (key == "lightning_min_damage")
					    return ParseSigned(item.lightningMinDamage);
				    if (key == "lightning_max_damage")
					    return ParseSigned(item.lightningMaxDamage);
				    return SkipValue();
			    })) {
				return false;
			}
			if (definedFields != 0x3FU
			    || item.slot >= 7
			    || item.itemSeed == 0
			    || item.itemType < 0
			    || item.itemType > std::numeric_limits<uint8_t>::max()
			    || item.itemClass < 0
			    || item.itemClass > 3
			    || item.fireMinDamage < 0
			    || item.fireMaxDamage < item.fireMinDamage
			    || item.lightningMinDamage < 0
			    || item.lightningMaxDamage < item.lightningMinDamage
			    || std::any_of(target.begin(), target.end(), [&](const auto &candidate) {
				    return candidate.slot == item.slot;
				})) {
				return Fail(description == "equipment"
				        ? "invalid or duplicate equipment item"
				        : "invalid or duplicate secondary equipment item");
			}
			target.push_back(item);
			SkipWhitespace();
			if (Consume(']'))
				return true;
			if (!Consume(','))
				return Fail("expected comma or closing bracket");
			SkipWhitespace();
		}
	}

	bool ParseEquipment()
	{
		return ParseEquipment(fixture_.initialState.equipment, "equipment");
	}

	bool ParseBelt()
	{
		if (!Consume('['))
			return Fail("expected belt array");
		fixture_.initialState.belt.clear();
		SkipWhitespace();
		if (Consume(']'))
			return true;
		while (true) {
			ReplayFixtureBeltItem item;
			uint32_t definedFields = 0;
			if (!ParseObject([this, &item, &definedFields](std::string_view key) {
				    if (key == "slot") {
					    if (!ParseUnsigned(item.slot))
						    return false;
					    definedFields |= 1U << 0;
					    return true;
				    }
				    if (key == "item_seed") {
					    if (!ParseUnsigned(item.itemSeed))
						    return false;
					    definedFields |= 1U << 1;
					    return true;
				    }
				    if (key == "stable_item_id")
					    return ParseUnsigned(item.stableItemId);
				    if (key == "item_type") {
					    if (!ParseSigned(item.itemType))
						    return false;
					    definedFields |= 1U << 2;
					    return true;
				    }
				    if (key == "item_index") {
					    if (!ParseSigned(item.itemIndex))
						    return false;
					    definedFields |= 1U << 3;
					    return true;
				    }
				    if (key == "misc_id") {
					    if (!ParseSigned(item.miscId))
						    return false;
					    definedFields |= 1U << 4;
					    return true;
				    }
				    if (key == "spell_id") {
					    if (!ParseSigned(item.spellId))
						    return false;
					    definedFields |= 1U << 5;
					    return true;
				    }
				    if (key == "stat_flag") {
					    if (!ParseBool(item.statFlag))
						    return false;
					    definedFields |= 1U << 6;
					    return true;
				    }
				    return SkipValue();
			    })) {
				return false;
			}
			if (definedFields != 0x7FU
			    || fixture_.initialState.belt.size() >= 8
			    || item.slot >= 8
			    || item.itemSeed == 0
			    || item.itemType < 0
			    || item.itemType > std::numeric_limits<uint8_t>::max()
			    || item.itemIndex < -1
			    || item.itemIndex > std::numeric_limits<uint8_t>::max()
			    || (item.miscId != 21 && item.miscId != 22)
			    || item.spellId < 1
			    || item.spellId > authoritative::MaximumNativeSpellId
			    || std::any_of(fixture_.initialState.belt.begin(), fixture_.initialState.belt.end(), [&](const auto &candidate) {
				    return candidate.slot == item.slot;
			    })) {
				return Fail("invalid or duplicate belt consumable");
			}
			fixture_.initialState.belt.push_back(item);
			SkipWhitespace();
			if (Consume(']'))
				return true;
			if (!Consume(','))
				return Fail("expected comma or closing bracket");
			SkipWhitespace();
		}
	}

	bool ParseStoreState()
	{
		return ParseObject([this](std::string_view key) {
			if (key == "active_store")
				return ParseSigned(fixture_.storeState.activeStore);
			if (key == "premium_item_count")
				return ParseSigned(fixture_.storeState.premiumItemCount);
			if (key == "premium_item_level")
				return ParseSigned(fixture_.storeState.premiumItemLevel);
			if (key == "premium_item_seeds")
				return ParseSeedArray();
			return SkipValue();
		});
	}

	bool ParseSeedArray()
	{
		if (!Consume('['))
			return Fail("expected premium item seed array");
		fixture_.storeState.premiumItemSeeds.clear();
		SkipWhitespace();
		if (Consume(']'))
			return true;
		while (true) {
			uint32_t seed = 0;
			if (!ParseUnsigned(seed))
				return false;
			fixture_.storeState.premiumItemSeeds.push_back(seed);
			SkipWhitespace();
			if (Consume(']'))
				return true;
			if (!Consume(','))
				return Fail("expected comma or closing bracket");
			SkipWhitespace();
		}
	}

	bool ParseCommands()
	{
		if (!Consume('['))
			return Fail("expected commands array");
		SkipWhitespace();
		if (Consume(']'))
			return true;
		while (true) {
			ReplayFixtureCommand command;
			if (!ParseObject([this, &command](std::string_view key) { return ParseCommandField(key, command); }))
				return false;
			fixture_.commands.push_back(std::move(command));
			SkipWhitespace();
			if (Consume(']'))
				return true;
			if (!Consume(','))
				return Fail("expected comma or closing bracket");
			SkipWhitespace();
		}
	}

	bool ParseCommandField(std::string_view key, ReplayFixtureCommand &command)
	{
		if (key == "client_sequence")
			return ParseUnsigned(command.clientSequence);
		if (key == "target_tick")
			return ParseUnsigned(command.order.targetTick);
		if (key == "server_receipt_sequence")
			return ParseUnsigned(command.order.serverReceiptSequence);
		if (key == "expected_status")
			return ParseString(command.expectedStatus);
		if (key == "expected_reject_reason")
			return ParseString(command.expectedRejectReason);
		if (key == "player")
			return ParseString(command.player);
		if (key == "kind")
			return ParseString(command.kind);
		if (key == "payload")
			return ParseCommandPayload(command);
		return SkipValue();
	}

	bool ParseCommandPayload(ReplayFixtureCommand &command)
	{
		return ParseObject([this, &command](std::string_view key) {
			if (key == "store_id")
				return ParseUnsigned(command.storeId);
			if (key == "store_slot" || key == "item_index" || key == "inventory_index")
				return ParseUnsigned(command.storeSlot);
			if (key == "direction_x")
				return ParseSigned(command.directionX);
			if (key == "direction_y")
				return ParseSigned(command.directionY);
			if (key == "target_entity_id")
				return ParseUnsigned(command.targetEntityId);
			if (key == "spell_id")
				return ParseUnsigned(command.spellId);
			if (key == "target_x")
				return ParseSigned(command.targetX);
			if (key == "target_y")
				return ParseSigned(command.targetY);
			if (key == "portal_id")
				return ParseUnsigned(command.portalId);
			if (key == "item_entity_id")
				return ParseUnsigned(command.worldItemEntityId);
			if (key == "stable_item_id")
				return ParseUnsigned(command.stableItemId);
			if (key == "location")
				return ParseString(command.itemLocation);
			if (key == "object_entity_id")
				return ParseUnsigned(command.objectEntityId);
			if (key == "quest_id")
				return ParseUnsigned(command.questId);
			return SkipValue();
		});
	}

	bool ParseCheckpoints()
	{
		if (!Consume('['))
			return Fail("expected checkpoints array");
		SkipWhitespace();
		if (Consume(']'))
			return true;
		while (true) {
			ReplayFixtureCheckpoint checkpoint;
			if (!ParseObject([this, &checkpoint](std::string_view key) {
				    if (key == "tick")
					    return ParseUnsigned(checkpoint.tick);
				    if (key == "state_sha256")
					    return ParseString(checkpoint.stateSha256);
				    return SkipValue();
			    }))
				return false;
			if (checkpoint.tick == 0)
				fixture_.initialStateSha256 = checkpoint.stateSha256;
			fixture_.checkpoints.push_back(std::move(checkpoint));
			SkipWhitespace();
			if (Consume(']'))
				return true;
			if (!Consume(','))
				return Fail("expected comma or closing bracket");
			SkipWhitespace();
		}
	}

	template <typename Integer>
	bool ParseUnsigned(Integer &value)
	{
		uint64_t parsed = 0;
		bool negative = false;
		if (!ParseUnsigned(parsed, negative))
			return false;
		if (negative || parsed > std::numeric_limits<Integer>::max())
			return Fail("unsigned integer out of range");
		value = static_cast<Integer>(parsed);
		return true;
	}

	template <typename Integer>
	bool ParseSigned(Integer &value)
	{
		const size_t start = position_;
		bool negative = false;
		uint64_t magnitude = 0;
		if (!ParseUnsigned(magnitude, negative))
			return false;
		if (negative) {
			if (magnitude > static_cast<uint64_t>(std::numeric_limits<Integer>::max()) + 1)
				return Fail("signed integer out of range");
			value = static_cast<Integer>(-static_cast<int64_t>(magnitude));
		} else {
			if (magnitude > static_cast<uint64_t>(std::numeric_limits<Integer>::max()))
				return Fail("signed integer out of range");
			value = static_cast<Integer>(magnitude);
		}
		return position_ != start || Fail("expected signed integer");
	}

	bool ParseUnsigned(uint64_t &value, bool &negative)
	{
		const size_t start = position_;
		if (position_ < input_.size() && input_[position_] == '-') {
			negative = true;
			++position_;
		} else {
			negative = false;
		}
		const size_t digits = position_;
		while (position_ < input_.size() && std::isdigit(static_cast<unsigned char>(input_[position_])))
			++position_;
		if (digits == position_) {
			position_ = start;
			return Fail("expected unsigned integer");
		}
		const auto result = std::from_chars(input_.data() + digits, input_.data() + position_, value);
		if (result.ec != std::errc())
			return Fail("invalid unsigned integer");
		return true;
	}

	bool ParseString(std::string &value)
	{
		if (!Consume('"'))
			return Fail("expected string");
		value.clear();
		while (position_ < input_.size()) {
			const char character = input_[position_++];
			if (character == '"')
				return true;
			if (character == '\\') {
				if (position_ >= input_.size())
					return Fail("unterminated escape");
				const char escaped = input_[position_++];
				switch (escaped) {
				case '"': value.push_back('"'); break;
				case '\\': value.push_back('\\'); break;
				case '/': value.push_back('/'); break;
				case 'b': value.push_back('\b'); break;
				case 'f': value.push_back('\f'); break;
				case 'n': value.push_back('\n'); break;
				case 'r': value.push_back('\r'); break;
				case 't': value.push_back('\t'); break;
				default: return Fail("unsupported string escape");
				}
			} else {
				if (static_cast<unsigned char>(character) < 0x20)
					return Fail("control character in string");
				value.push_back(character);
			}
		}
		return Fail("unterminated string");
	}

	bool ParseBool(bool &value)
	{
		if (input_.substr(position_, 4) == "true") {
			position_ += 4;
			value = true;
			return true;
		}
		if (input_.substr(position_, 5) == "false") {
			position_ += 5;
			value = false;
			return true;
		}
		return Fail("expected boolean");
	}

	bool SkipValue()
	{
		if (position_ >= input_.size())
			return Fail("expected value");
		if (input_[position_] == '"') {
			std::string ignored;
			return ParseString(ignored);
		}
		if (input_[position_] == '{')
			return ParseObject([this](std::string_view) { return SkipValue(); });
		if (input_[position_] == '[')
			return ParseArray();
		const size_t start = position_;
		while (position_ < input_.size() && input_[position_] != ',' && input_[position_] != '}' && input_[position_] != ']')
			++position_;
		while (position_ > start && std::isspace(static_cast<unsigned char>(input_[position_ - 1])))
			--position_;
		return position_ > start || Fail("expected value");
	}

	bool ParseArray()
	{
		if (!Consume('['))
			return Fail("expected array");
		SkipWhitespace();
		if (Consume(']'))
			return true;
		while (true) {
			if (!SkipValue())
				return false;
			SkipWhitespace();
			if (Consume(']'))
				return true;
			if (!Consume(','))
				return Fail("expected comma or closing bracket");
			SkipWhitespace();
		}
	}

	void SkipWhitespace()
	{
		while (position_ < input_.size() && std::isspace(static_cast<unsigned char>(input_[position_])))
			++position_;
	}

	bool Consume(char expected)
	{
		if (position_ < input_.size() && input_[position_] == expected) {
			++position_;
			return true;
		}
		return false;
	}

	bool Fail(std::string_view message)
	{
		if (error_.empty())
			error_ = std::string(message) + " at offset " + std::to_string(position_);
		return false;
	}

	std::string_view input_;
	ReplayFixture &fixture_;
	std::string &error_;
	size_t position_ = 0;
};

} // namespace

bool ParseReplayFixture(std::string_view json, ReplayFixture &fixture, std::string &error)
{
	fixture = { };
	error.clear();
	if (!JsonReader(json, fixture, error).Parse())
		return false;
	if (fixture.initialState.lifeMaximum == 0)
		fixture.initialState.lifeMaximum = fixture.initialState.life;
	if (fixture.initialState.life < 0
	    || fixture.initialState.lifeMaximum < fixture.initialState.life
	    || (!fixture.initialState.characterName.empty()
	        && !IsValidReplayCharacterName(fixture.initialState.characterName))) {
		error = "Replay fixture contains invalid initial player life.";
		return false;
	}
	if (fixture.initialState.facingDirection > 7) {
		error = "Replay fixture contains invalid initial player facing.";
		return false;
	}
	if ((fixture.initialState.objectBreakable && fixture.initialState.objectMissilePassable)
	    || (fixture.initialState.objectDestroyed && !fixture.initialState.objectBreakable)
	    || (fixture.initialState.objectEntityId == 0
	        && (fixture.initialState.objectMissilePassable
	            || fixture.initialState.objectBreakable
	            || fixture.initialState.objectDestroyed
	            || fixture.initialState.objectQuestId != 0
	            || fixture.initialState.objectEffectKind != 0
	            || fixture.initialState.objectEffectAmount != 0))
	    || fixture.initialState.objectEffectKind < 0
	    || fixture.initialState.objectEffectKind > 3
	    || fixture.initialState.objectEffectAmount < 0) {
		error = "Replay fixture contains invalid object state.";
		return false;
	}
	if ((fixture.initialState.goldStackSeed == 0 && fixture.initialState.goldStackCell != 0)
	    || (fixture.initialState.goldStackSeed != 0
	        && (fixture.initialState.gold == 0 || fixture.initialState.goldStackCell >= 40))) {
		error = "Replay fixture contains invalid initial Gold-stack topology.";
		return false;
	}
	if (fixture.initialState.inventoryFillerCount > 39
	    || (fixture.initialState.inventoryFillerCount != 0
	        && fixture.initialState.goldStackSeed == 0)) {
		error = "Replay fixture contains invalid inventory filler state.";
		return false;
	}
	auto &target = fixture.initialState;
	auto &companion = fixture.initialState.combatTargetCompanion;
	if ((target.combatTargetMonsterItemSeed == 0) != (target.combatTargetMonsterAiSeed == 0)) {
		error = "Replay fixture monster runtime seeds must be supplied together.";
		return false;
	}
	if (target.combatTargetMaxHitPoints == 0 && target.combatTargetHitPoints > 0)
		target.combatTargetMaxHitPoints = target.combatTargetHitPoints;
	if (companion.maxHitPoints == 0 && companion.hitPoints > 0)
		companion.maxHitPoints = companion.hitPoints;
	if (!target.combatTargetMoving
	    && target.combatTargetFuturePositionX == 0
	    && target.combatTargetFuturePositionY == 0) {
		target.combatTargetFuturePositionX = target.combatTargetPositionX;
		target.combatTargetFuturePositionY = target.combatTargetPositionY;
	}
	if ((target.combatTargetEntityId == 0
	        && (target.combatTargetPositionX != 0
	            || target.combatTargetPositionY != 0
	            || target.combatTargetHitPoints != 0
	            || target.combatTargetMaxHitPoints != 0
	            || target.combatTargetArmorClass != 0
			|| target.combatTargetAttackDamage != 0
			|| target.combatTargetAggroRange != 0
		    || !target.combatTargetNativeAi.empty()
		    || target.combatTargetMonsterItemSeed != 0
		    || target.combatTargetMonsterAiSeed != 0
		    || target.combatTargetMonsterId != 0
			|| target.combatTargetToHit != 0
			|| target.combatTargetToHitSpecial != 0
			|| target.combatTargetMinDamage != 0
			|| target.combatTargetMaxDamage != 0
			|| target.combatTargetIntelligence != 0
			|| target.combatTargetFallenStandAnimationFrame != 0
		    || target.combatTargetFallenStandAnimationTick != 0
		    || target.combatTargetFallenRallyTicks != 0
		    || target.combatTargetActiveForTicks != 0
			|| target.combatTargetGargoyleSpecialActive
		    || target.combatTargetHorkDemonDelayTicks != 0
		    || target.combatTargetSkeletonKingDelayTicks != 0
		    || target.combatTargetSkeletonMeleeDelayTicks != 0
		    || target.combatTargetSnakeDelayTicks != 0
		    || target.combatTargetCounselorDelayTicks != 0
		    || target.combatTargetCounselorRetreatTicks != 0
		    || target.combatTargetRangedDelayTicks != 0
		    || target.combatTargetRangedRetreatMinimumDistance != 0
		    || target.combatTargetRangedRetreatChanceBase != 0
		    || target.combatTargetRangedRetreatChanceIntelligenceScale != 0
		    || target.combatTargetMegaDelayTicks != 0
		    || target.combatTargetSkeletonRangedMovementIdleTicks != -1
		    || target.combatTargetSkeletonRangedMovementMode != 0
		    || target.combatTargetSneakHidden
		    || target.combatTargetSneakRetreatTicks != 0
		    || target.combatTargetBatRetreatTicks != 0
		    || target.combatTargetBatRetreatDirection != 0
		    || target.combatTargetScavengerHealingTicks != 0
		    || target.combatTargetScavengerCorpseEntityId != 0
		    || target.combatTargetFatMovementIdleTicks != -1
		    || target.combatTargetFatMovementMode != 0
			|| target.combatTargetFireResistance != 0
	            || target.combatTargetLightningResistance != 0
	            || target.combatTargetMagicResistance != 0
	            || target.combatTargetMonsterLevel != 0
	            || target.combatTargetLevelId != 0
	            || target.combatTargetMoving
	            || target.combatTargetPetrified))
	    || target.combatTargetHitPoints < 0
	    || target.combatTargetMaxHitPoints < target.combatTargetHitPoints
	    || target.combatTargetArmorClass < 0
		|| target.combatTargetAttackDamage < 0
		|| target.combatTargetAggroRange < 0
		|| target.combatTargetToHit < 0 || target.combatTargetToHit > 255
		|| target.combatTargetToHitSpecial < 0 || target.combatTargetToHitSpecial > 255
		|| target.combatTargetMinDamage < 0
		|| target.combatTargetMaxDamage < target.combatTargetMinDamage
		|| target.combatTargetIntelligence < 0 || target.combatTargetIntelligence > 255
		|| target.combatTargetHorkDemonDelayTicks < 0
		|| target.combatTargetHorkDemonDelayTicks > 1024
		|| target.combatTargetSkeletonKingDelayTicks < 0
		|| target.combatTargetSkeletonKingDelayTicks > 1024
		|| target.combatTargetSkeletonMeleeDelayTicks < 0
		|| target.combatTargetSkeletonMeleeDelayTicks > 1024
		|| target.combatTargetSnakeDelayTicks < 0
		|| target.combatTargetSnakeDelayTicks > 1024
		|| target.combatTargetCounselorDelayTicks < 0
		|| target.combatTargetCounselorDelayTicks > 1024
		|| target.combatTargetCounselorRetreatTicks < 0
		|| target.combatTargetCounselorRetreatTicks > 64
		|| target.combatTargetRangedDelayTicks < 0
		|| target.combatTargetRangedDelayTicks > 1024
		|| target.combatTargetRangedRetreatMinimumDistance < 0
		|| target.combatTargetRangedRetreatMinimumDistance > 64
		|| target.combatTargetRangedRetreatChanceBase < 0
		|| target.combatTargetRangedRetreatChanceBase > 100
		|| target.combatTargetRangedRetreatChanceIntelligenceScale < 0
		|| target.combatTargetRangedRetreatChanceIntelligenceScale > 100
		|| (target.combatTargetRangedRetreatMinimumDistance != 0
		    || target.combatTargetRangedRetreatChanceBase != 0
		    || target.combatTargetRangedRetreatChanceIntelligenceScale != 0)
		    && (target.combatTargetRangedRetreatMinimumDistance <= 0
		        || target.combatTargetRangedRetreatChanceBase <= 0
		        || target.combatTargetRangedRetreatChanceIntelligenceScale <= 0)
		|| target.combatTargetMegaDelayTicks < 0
		|| target.combatTargetMegaDelayTicks > 1024
		|| target.combatTargetSkeletonRangedMovementIdleTicks < -1
		|| target.combatTargetSkeletonRangedMovementIdleTicks > 1024
		|| target.combatTargetSkeletonRangedMovementMode < -1
		|| target.combatTargetSkeletonRangedMovementMode > 3
		|| target.combatTargetSneakRetreatTicks < 0
		|| target.combatTargetSneakRetreatTicks > 1024
		|| target.combatTargetBatRetreatTicks < 0
		|| target.combatTargetBatRetreatTicks > 1024
		|| target.combatTargetBatRetreatDirection < 0
		|| target.combatTargetBatRetreatDirection > 7
		|| target.combatTargetScavengerHealingTicks < 0
		|| target.combatTargetScavengerHealingTicks > 1024
		|| target.combatTargetFatMovementIdleTicks < -1
		|| target.combatTargetFatMovementIdleTicks > 1024
		|| target.combatTargetFatMovementMode < 0
		|| target.combatTargetFatMovementMode > 3
		|| target.combatTargetFallenStandAnimationFrame < 0
		|| target.combatTargetFallenStandAnimationFrame > 127
		|| target.combatTargetFallenStandAnimationTick < 0
		|| target.combatTargetFallenStandAnimationTick > 127
	    || target.combatTargetFallenRallyTicks < 0
	    || target.combatTargetFallenRallyTicks > 4096
	    || target.combatTargetActiveForTicks < 0
	    || target.combatTargetActiveForTicks > 255
	    || target.combatTargetFireResistance < -100
	    || target.combatTargetFireResistance > 100
	    || target.combatTargetLightningResistance < -100
	    || target.combatTargetLightningResistance > 100
	    || target.combatTargetMagicResistance < -100
	    || target.combatTargetMagicResistance > 100
	    || target.combatTargetMonsterLevel < 0
	    || (target.combatTargetMoving
	        && std::max(
	               std::abs(target.combatTargetFuturePositionX - target.combatTargetPositionX),
	               std::abs(target.combatTargetFuturePositionY - target.combatTargetPositionY))
	            != 1)
	    || (!target.combatTargetMoving
	        && (target.combatTargetFuturePositionX != target.combatTargetPositionX
	            || target.combatTargetFuturePositionY != target.combatTargetPositionY))) {
		error = "Replay fixture contains invalid combat-target occupancy state.";
		return false;
	}
	if (companion.entityId != 0
	    && (target.combatTargetEntityId == 0
	        || companion.entityId == target.combatTargetEntityId
	        || companion.hitPoints < 0
	        || companion.maxHitPoints < companion.hitPoints
	        || companion.levelId != target.combatTargetLevelId
	        || companion.nativeAi != "Fallen"
	        || companion.intelligence < 0 || companion.intelligence > 255
	        || companion.fallenStandAnimationFrame < 0
	        || companion.fallenStandAnimationFrame > 127
	        || companion.fallenStandAnimationTick < 0
	        || companion.fallenStandAnimationTick > 127
	        || companion.fallenRallyTicks < 0
	        || companion.fallenRallyTicks > 4096)) {
		error = "Replay fixture contains invalid combat-target companion state.";
		return false;
	}
	if (companion.entityId == 0
	    && (companion.levelId != 0
	        || companion.positionX != 0
	        || companion.positionY != 0
	        || companion.hitPoints != 0
	        || companion.maxHitPoints != 0
	        || companion.monsterId != 0
	        || !companion.nativeAi.empty()
	        || companion.intelligence != 0
	        || companion.fallenStandAnimationFrame != 0
	        || companion.fallenStandAnimationTick != 0
	        || companion.fallenRallyTicks != 0)) {
		error = "Replay fixture contains invalid combat-target companion state.";
		return false;
	}
	auto &secondaryPlayer = fixture.initialState.secondaryPlayer;
	const auto &timing = fixture.playerActionTiming;
	if (timing.defined
	    && ((timing.definedFields & 0x1FU) != 0x1FU
	        || timing.walkingFrames == 0
	        || timing.walkingFrames > 24
	        || timing.attackFrames == 0
	        || timing.attackFrames > static_cast<uint32_t>(std::numeric_limits<int8_t>::max())
	        || timing.attackActionFrame == 0
	        || timing.attackActionFrame > timing.attackFrames
	        || timing.castingFrames == 0
	        || timing.castingFrames > static_cast<uint32_t>(std::numeric_limits<int8_t>::max())
	        || timing.castingActionFrame >= timing.castingFrames
	        || timing.deathFrames > static_cast<uint32_t>(std::numeric_limits<int8_t>::max()))) {
		error = "Replay fixture contains invalid player-action timing.";
		return false;
	}
	if (fixture.gameVariant != "Diablo" && fixture.gameVariant != "Hellfire") {
		error = "Replay fixture game_variant must be Diablo or Hellfire.";
		return false;
	}
	if (fixture.gameMode != "SinglePlayer" && fixture.gameMode != "Multiplayer") {
		error = "Replay fixture game_mode must be SinglePlayer or Multiplayer.";
		return false;
	}
	if (secondaryPlayer.defined && secondaryPlayer.lifeMaximum == 0)
		secondaryPlayer.lifeMaximum = secondaryPlayer.life;
	if (secondaryPlayer.defined
	    && (secondaryPlayer.entityId == 0
	        || secondaryPlayer.entityId == 1
	        || secondaryPlayer.life < 0
	        || secondaryPlayer.lifeMaximum <= 0
	        || secondaryPlayer.life > secondaryPlayer.lifeMaximum
	        || secondaryPlayer.characterLevel == 0
	        || secondaryPlayer.characterLevel > 50
	        || (!secondaryPlayer.characterName.empty()
	            && !IsValidReplayCharacterName(secondaryPlayer.characterName))
	        || secondaryPlayer.combatMode > 11
	        || ((secondaryPlayer.life == 0) != (secondaryPlayer.combatMode == 8))
	        || (secondaryPlayer.combatMode == 0 && secondaryPlayer.combatModeEndTick != 0)
	        || (secondaryPlayer.combatMode == 8 && secondaryPlayer.combatModeEndTick != 0)
	        || ((secondaryPlayer.combatMode == 6 || secondaryPlayer.combatMode == 7)
	            && secondaryPlayer.combatModeEndTick == 0)
	        || secondaryPlayer.player == fixture.initialState.player)) {
		error = "Replay fixture contains invalid secondary-player state.";
		return false;
	}
	if (secondaryPlayer.defined && secondaryPlayer.inventoryItemDefined
	    && (secondaryPlayer.inventoryItemSlot >= 40
	        || secondaryPlayer.inventoryItemSeed == 0
	        || secondaryPlayer.inventoryItemStableItemId == 0
	        || secondaryPlayer.inventoryItemPrice > INT32_MAX
	        || secondaryPlayer.inventoryItemType < 0
	        || secondaryPlayer.inventoryItemType > UINT8_MAX
	        || secondaryPlayer.inventoryItemIndex < -1
	        || secondaryPlayer.inventoryItemIndex > UINT8_MAX)) {
		error = "Replay fixture secondary inventory item contains invalid item state.";
		return false;
	}
	if (secondaryPlayer.defined && secondaryPlayer.heldItemDefined
	    && (secondaryPlayer.heldItemSlot != 0
	        || secondaryPlayer.heldItemSeed == 0
	        || secondaryPlayer.heldItemStableItemId == 0
	        || secondaryPlayer.heldItemPrice > INT32_MAX
	        || secondaryPlayer.heldItemType < 0
	        || secondaryPlayer.heldItemType > UINT8_MAX
	        || secondaryPlayer.heldItemIndex < -1
	        || secondaryPlayer.heldItemIndex > UINT8_MAX)) {
		error = "Replay fixture secondary held item contains invalid item state.";
		return false;
	}
	if (secondaryPlayer.defined
	    && (secondaryPlayer.inventoryFillerCount > 40
	        || (secondaryPlayer.inventoryItemDefined
	            && secondaryPlayer.inventoryFillerCount > 39))) {
		error = "Replay fixture secondary inventory filler state is invalid.";
		return false;
	}
	for (const auto &command : fixture.commands) {
		if (!command.expectedStatus.empty()
		    && command.expectedStatus != "Accepted"
		    && command.expectedStatus != "Rejected") {
			error = "Replay fixture command expected_status must be Accepted or Rejected.";
			return false;
		}
		if (!command.expectedRejectReason.empty()
		    && command.expectedRejectReason != "Malformed"
		    && command.expectedRejectReason != "TooLate"
		    && command.expectedRejectReason != "NotAllowed"
		    && command.expectedRejectReason != "InvalidTarget"
		    && command.expectedRejectReason != "InsufficientResources") {
			error = "Replay fixture command expected_reject_reason is not recognized.";
			return false;
		}
		if (!command.player.empty()
		    && command.player != fixture.initialState.player
		    && (!secondaryPlayer.defined || command.player != secondaryPlayer.player)) {
			error = "Replay fixture command participant must be the primary or secondary player.";
			return false;
		}
	}
	return true;
}

std::string ComputeReplayFixtureStateHash(const ReplayFixtureExecutionState &state)
{
	ReplayStateHasher hasher;
	AppendCanonicalState(hasher, state);
	return hasher.HexDigest();
}

/** Admits the compact Mega InfernoController native ranged branch. */
void AdmitReplayAutonomousMegaInferno(
    ReplayFixtureExecutionState &state,
    uint64_t tick)
{
	if (state.combatTargetEntityId == 0
	    || state.combatTargetNativeAi != "Mega"
	    || state.combatTargetHitPointsQ6 <= 0
	    || state.combatTargetAggroRange <= 0)
		return;
	if (state.combatTargetMegaDelayTicks > 0) {
		--state.combatTargetMegaDelayTicks;
		return;
	}
	if (std::any_of(state.projectiles.begin(), state.projectiles.end(), [&](const auto &projectile) {
		return projectile.sourceEntityId == state.combatTargetEntityId
		    && (projectile.behavior == InfernoControllerBehavior
		        || projectile.behavior == InfernoSegmentBehavior);
	}))
		return;

	struct Candidate {
		uint32_t entityId;
		int32_t positionX;
		int32_t positionY;
	};
	std::vector<Candidate> candidates;
	if (state.life > 0 && state.levelId == state.combatTargetLevelId)
		candidates.push_back({ state.entityId == 0 ? 1U : state.entityId, state.positionX, state.positionY });
	if (state.secondaryPlayerEntityId != 0
	    && state.secondaryPlayerLife > 0
	    && state.secondaryPlayerLevelId == state.combatTargetLevelId)
		candidates.push_back({ state.secondaryPlayerEntityId, state.secondaryPlayerPositionX, state.secondaryPlayerPositionY });
	if (candidates.empty())
		return;
	const auto candidateDistance = [&](const Candidate &candidate) {
		return std::max(
		    std::abs(candidate.positionX - state.combatTargetPositionX),
		    std::abs(candidate.positionY - state.combatTargetPositionY));
	};
	std::sort(candidates.begin(), candidates.end(), [&](const auto &left, const auto &right) {
		const int32_t leftDistance = candidateDistance(left);
		const int32_t rightDistance = candidateDistance(right);
		return leftDistance != rightDistance
		    ? leftDistance < rightDistance
		    : left.entityId < right.entityId;
	});
	const Candidate &target = candidates.front();
	const int distance = candidateDistance(target);
	if (distance > state.combatTargetAggroRange || distance >= 5)
		return;
	const uint32_t attackSeed = static_cast<uint32_t>(tick)
	    ^ state.combatTargetEntityId
	    ^ target.entityId;
	const bool usesNativeAiStream = state.combatTargetMonsterAiSeed != 0;
	const int actionRoll = usesNativeAiStream
	    ? std::max(GenerateRnd(state, 100), 0)
	    : static_cast<int>(attackSeed % 100U);
	const int attackBand = std::min(
	    100,
	    5 + 5 * (state.combatTargetIntelligence + (distance >= 3 ? 1 : 0)));
	const auto resolveMegaDelay = [&]() {
		return 5 + (usesNativeAiStream
	        ? std::max(GenerateRnd(state, 20), 0)
	        : static_cast<int>((attackSeed / 200U) % 20U));
	};
	if (actionRoll >= attackBand) {
		state.combatTargetMegaDelayTicks = resolveMegaDelay();
		return;
	}

	uint32_t nextEntityId = 0x80000000U;
	for (const auto &projectile : state.projectiles)
		nextEntityId = std::max(nextEntityId, projectile.entityId + 1U);
	const auto velocity = CalculateVelocity(
	    state.combatTargetPositionX,
	    state.combatTargetPositionY,
	    target.positionX,
	    target.positionY,
	    32);
	ReplayFixtureProjectileState projectile;
	projectile.entityId = nextEntityId;
	projectile.sourceEntityId = state.combatTargetEntityId;
	projectile.targetEntityId = target.entityId;
	projectile.spellId = 21;
	projectile.levelId = state.combatTargetLevelId;
	projectile.positionX = state.combatTargetPositionX;
	projectile.positionY = state.combatTargetPositionY;
	projectile.startX = projectile.positionX;
	projectile.startY = projectile.positionY;
	projectile.targetX = target.positionX;
	projectile.targetY = target.positionY;
	projectile.damage = 4;
	projectile.damageType = 1;
	projectile.remainingTicks = 256;
	projectile.behavior = InfernoControllerBehavior;
	projectile.originalDirection = GetDirection(
	    projectile.positionX,
	    projectile.positionY,
	    projectile.targetX,
	    projectile.targetY);
	projectile.velocityX = velocity.first;
	projectile.velocityY = velocity.second;
	projectile.speed = 32;
	projectile.impactLifetime = 1;
	projectile.resistantDamageDivisor = 1;
	state.projectiles.push_back(std::move(projectile));
	state.combatTargetMegaDelayTicks = resolveMegaDelay();
}

/** Advances the player Apocalypse scan controller and its physical boom. */
void AdvanceReplayPlayerApocalypseOneTick(ReplayFixtureExecutionState &state)
{
	const uint32_t playerEntityId = state.entityId == 0 ? 1U : state.entityId;
	for (size_t index = 0; index < state.projectiles.size(); ++index) {
		auto &projectile = state.projectiles[index];
		if (projectile.behavior == ApocalypseBoomBehavior
		    && projectile.sourceEntityId == playerEntityId) {
			if (projectile.remainingTicks > 0)
				--projectile.remainingTicks;
			if (!projectile.apocalypseHit
			    && state.combatTargetEntityId != 0
			    && state.combatTargetHitPointsQ6 > 0
			    && state.combatTargetLevelId == projectile.levelId
			    && state.combatTargetPositionX == projectile.positionX
			    && state.combatTargetPositionY == projectile.positionY) {
				const int32_t damageQ6 = std::max(0, projectile.damage) * 64;
				state.combatTargetHitPointsQ6 = std::max(
					0,
					state.combatTargetHitPointsQ6 - damageQ6);
				state.combatTargetHitPoints = ToWholeCeilingQ6(state.combatTargetHitPointsQ6);
				projectile.apocalypseHit = true;
			}
			if (projectile.remainingTicks == 0) {
				state.projectiles.erase(state.projectiles.begin() + static_cast<ptrdiff_t>(index));
				--index;
			}
			continue;
		}
		if (projectile.behavior != ApocalypseControllerBehavior
		    || projectile.sourceEntityId != playerEntityId)
			continue;
		const int32_t minimumX = std::max(projectile.startX - 8, 1);
		const int32_t maximumX = projectile.startX + 8;
		const int32_t maximumY = projectile.startY + 8;
		const int32_t scanY = std::max(projectile.apocalypseScanY, 1);
		const int32_t scanX = std::max(projectile.apocalypseScanX, minimumX);
		bool emitted = false;
		for (int32_t y = scanY; y < maximumY && !emitted; ++y) {
			const int32_t firstX = y == scanY ? scanX : minimumX;
			for (int32_t x = firstX; x < maximumX; ++x) {
				projectile.apocalypseScanX = x + 1;
				projectile.apocalypseScanY = y;
				if (!ReplayWallPositionIsValid(state, x, y))
					continue;
				if (state.combatTargetEntityId == 0
				    || state.combatTargetHitPointsQ6 <= 0
				    || state.combatTargetLevelId != projectile.levelId
				    || state.combatTargetPositionX != x
				    || state.combatTargetPositionY != y)
					continue;
				state.projectiles.push_back(CreateReplayApocalypseBoom(
					state,
					projectile,
					x,
					y));
				emitted = true;
				break;
			}
		}
		if (!emitted) {
			state.projectiles.erase(state.projectiles.begin() + static_cast<ptrdiff_t>(index));
			--index;
		}
	}
}

/** Advances the player Inferno controller and its three persistent segments. */
void AdvanceReplayPlayerInfernoOneTick(ReplayFixtureExecutionState &state)
{
	const uint32_t playerEntityId = state.entityId == 0 ? 1U : state.entityId;
	for (size_t index = 0; index < state.projectiles.size(); ++index) {
		auto &projectile = state.projectiles[index];
		if (projectile.behavior == InfernoSegmentBehavior
		    && projectile.sourceEntityId == playerEntityId) {
			if (projectile.remainingTicks > 0)
				--projectile.remainingTicks;
			if (projectile.remainingTicks == 0) {
				state.projectiles.erase(state.projectiles.begin() + static_cast<ptrdiff_t>(index));
				--index;
			}
			continue;
		}
		if (projectile.behavior != InfernoControllerBehavior
		    || projectile.sourceEntityId != playerEntityId)
			continue;
		if (projectile.remainingTicks > 0)
			--projectile.remainingTicks;
		if (projectile.remainingTicks == 0) {
			state.projectiles.erase(state.projectiles.begin() + static_cast<ptrdiff_t>(index));
			--index;
			continue;
		}
		const auto visitedTiles = MoveReplayNativeProjectile(projectile);
		if (visitedTiles.empty())
			continue;
		const auto &tile = visitedTiles.back();
		projectile.positionX = tile.x;
		projectile.positionY = tile.y;
		projectile.traveledX = tile.traveledX;
		projectile.traveledY = tile.traveledY;
		if (projectile.positionX < 0 || projectile.positionX >= 40
		    || projectile.positionY < 0 || projectile.positionY >= 40
		    || !ReplayWallPositionIsValid(state, projectile.positionX, projectile.positionY)) {
			state.projectiles.erase(state.projectiles.begin() + static_cast<ptrdiff_t>(index));
			--index;
			continue;
		}
		ReplayFixtureProjectileState segment;
		segment.entityId = projectile.entityId + 1U;
		for (const auto &candidate : state.projectiles)
			segment.entityId = std::max(segment.entityId, candidate.entityId + 1U);
		segment.sourceEntityId = projectile.sourceEntityId;
		segment.spellId = projectile.spellId;
		segment.levelId = projectile.levelId;
		segment.positionX = projectile.positionX;
		segment.positionY = projectile.positionY;
		segment.startX = segment.positionX;
		segment.startY = segment.positionY;
		segment.targetX = segment.positionX;
		segment.targetY = segment.positionY;
		const int32_t characterLevelDamage = GenerateRnd(
			state,
			std::max(1, static_cast<int32_t>(state.characterLevel)));
		const int32_t randomDamage = GenerateRnd(state, 2);
		segment.damage = (characterLevelDamage + randomDamage) * 12 + 24;
		segment.damageType = projectile.damageType;
		segment.remainingTicks = 20 + static_cast<uint32_t>(projectile.pathIndex);
		segment.behavior = InfernoSegmentBehavior;
		segment.impactLifetime = 1;
		segment.resistantDamageDivisor = projectile.resistantDamageDivisor;
		state.projectiles.push_back(std::move(segment));
		++state.projectiles[index].pathIndex;
		if (state.projectiles[index].pathIndex >= 3) {
			state.projectiles.erase(state.projectiles.begin() + static_cast<ptrdiff_t>(index));
			--index;
		}
	}
}

/** Advances Mega's three-step Inferno controller and its persistent segments. */
void AdvanceReplayInfernoOneTick(ReplayFixtureExecutionState &state)
{
	for (size_t index = 0; index < state.projectiles.size(); ++index) {
		auto &projectile = state.projectiles[index];
		if (projectile.behavior == InfernoSegmentBehavior
		    && projectile.sourceEntityId != (state.entityId == 0 ? 1U : state.entityId)) {
			if (projectile.remainingTicks > 0)
				--projectile.remainingTicks;
			if (projectile.remainingTicks == 0) {
				state.projectiles.erase(state.projectiles.begin() + index);
				--index;
			}
			continue;
		}
		if (projectile.behavior != InfernoControllerBehavior
		    || projectile.sourceEntityId == (state.entityId == 0 ? 1U : state.entityId))
			continue;
		if (projectile.remainingTicks > 0)
			--projectile.remainingTicks;
		if (projectile.remainingTicks == 0) {
			state.projectiles.erase(state.projectiles.begin() + index);
			--index;
			continue;
		}
		const auto visitedTiles = MoveReplayNativeProjectile(projectile);
		if (visitedTiles.empty())
			continue;
		const auto &tile = visitedTiles.back();
		projectile.positionX = tile.x;
		projectile.positionY = tile.y;
		projectile.traveledX = tile.traveledX;
		projectile.traveledY = tile.traveledY;
		if (projectile.positionX < 0 || projectile.positionX >= 40
		    || projectile.positionY < 0 || projectile.positionY >= 40) {
			state.projectiles.erase(state.projectiles.begin() + index);
			--index;
			continue;
		}
		ReplayFixtureProjectileState segment;
		segment.entityId = projectile.entityId + 1U;
		for (const auto &candidate : state.projectiles)
			segment.entityId = std::max(segment.entityId, candidate.entityId + 1U);
		segment.sourceEntityId = projectile.sourceEntityId;
		segment.spellId = projectile.spellId;
		segment.levelId = projectile.levelId;
		segment.positionX = projectile.positionX;
		segment.positionY = projectile.positionY;
		segment.startX = segment.positionX;
		segment.startY = segment.positionY;
		segment.targetX = segment.positionX;
		segment.targetY = segment.positionY;
		segment.damage = projectile.damage;
		segment.damageType = projectile.damageType;
		segment.remainingTicks = 5 * static_cast<uint32_t>(std::max(1, projectile.damage))
		    + 20 + static_cast<uint32_t>(projectile.pathIndex);
		segment.behavior = InfernoSegmentBehavior;
		segment.impactLifetime = 1;
		segment.resistantDamageDivisor = projectile.resistantDamageDivisor;
		state.projectiles.push_back(std::move(segment));
		++state.projectiles[index].pathIndex;
		if (state.projectiles[index].pathIndex >= 3) {
			state.projectiles.erase(state.projectiles.begin() + index);
			--index;
		}
	}
}

std::string GetReplayCastRejectReason(
	const ReplayFixtureExecutionState &state,
	const ReplayFixtureCommand &command)
{
	const bool isAbility = state.abilitySpellId == command.spellId;
	const bool isMemorized = std::any_of(
		state.memorizedSpells.begin(),
		state.memorizedSpells.end(),
		[&](const auto &spell) { return spell.spellId == command.spellId && spell.level > 0; });
	if (!isAbility && !isMemorized)
		return "NotAllowed";
	if (command.spellId != 33 || state.spellProjectileBehavior != TelekinesisBehavior)
		return "InvalidTarget";
	const bool targetMatches = (command.targetEntityId == state.worldItemEntityId
	        && state.worldItemEntityId != 0
	        && state.worldItemSeed != 0
	        && state.levelId == state.worldItemLevelId
	        && command.targetX == state.worldItemPositionX
	        && command.targetY == state.worldItemPositionY)
	    || (command.targetEntityId == state.objectEntityId
	        && state.objectEntityId != 0
	        && state.objectId != 0
	        && !state.objectActivated
	        && !state.objectDestroyed
	        && state.levelId == state.objectLevelId
	        && command.targetX == state.objectPositionX
	        && command.targetY == state.objectPositionY)
	    || (command.targetEntityId == state.combatTargetEntityId
	        && state.combatTargetEntityId != 0
	        && state.combatTargetHitPointsQ6 > 0
	        && state.levelId == state.combatTargetLevelId
	        && command.targetX == state.combatTargetPositionX
	        && command.targetY == state.combatTargetPositionY);
	if (!targetMatches)
		return "InvalidTarget";
	if (!isAbility) {
		const auto memorizedSpell = std::find_if(
			state.memorizedSpells.begin(),
			state.memorizedSpells.end(),
			[&](const auto &spell) { return spell.spellId == command.spellId && spell.level > 0; });
		const int32_t spellLevel = GetReplayEffectiveSpellLevel(state, &*memorizedSpell);
		const int32_t manaCost = std::max(8, 15 - std::max(spellLevel - 1, 0) * 2) * 64;
		if (state.mana < manaCost)
			return "InsufficientResources";
	}
	return "InvalidTarget";
}

bool ExecuteReplayFixture(
    const ReplayFixture &fixture,
    ReplayFixtureExecutionState &state,
    ReplayFixtureExecutionResult &result,
    std::string &error)
{
	result = { };
	error.clear();
	if (fixture.rngSeed > std::numeric_limits<uint32_t>::max()) {
		error = "Replay fixture RNG seed must fit the native 32-bit stream.";
		return false;
	}
	state.randomState = static_cast<uint32_t>(fixture.rngSeed);
	if (state.visitedLevelIds.empty())
		state.visitedLevelIds.push_back(state.levelId);
	state.hellfire = fixture.gameVariant == "Hellfire";
	state.multiplayer = fixture.gameMode == "Multiplayer";
	state.suppressMultiplayerDeathDrops = fixture.initialState.suppressMultiplayerDeathDrops;
	state.facingDirection = fixture.initialState.facingDirection;
	state.characterName = fixture.initialState.characterName;
	state.warpId = fixture.initialState.warpId;
	state.warpSourceLevelId = fixture.initialState.warpSourceLevelId;
	state.warpTriggerX = fixture.initialState.warpTriggerX;
	state.warpTriggerY = fixture.initialState.warpTriggerY;
	state.warpTargetLevelId = fixture.initialState.warpTargetLevelId;
	state.warpTargetX = fixture.initialState.warpTargetX;
	state.warpTargetY = fixture.initialState.warpTargetY;
	state.manaShieldActive = fixture.initialState.manaShieldActive;
	if (fixture.initialState.reflectCharges > std::numeric_limits<uint16_t>::max()) {
		error = "Replay fixture Reflect charges must fit the native uint16 range.";
		return false;
	}
	state.reflectCharges = fixture.initialState.reflectCharges;
	if (state.equipment.empty() && !fixture.initialState.equipment.empty()) {
		for (const auto &source : fixture.initialState.equipment) {
			ReplayFixtureItemState item;
			item.stableItemId = source.stableItemId;
			item.itemType = source.itemType;
			item.itemClass = source.itemClass;
			item.itemIndex = source.itemIndex;
			item.value = source.value;
			item.identifiedValue = source.identifiedValue;
			item.identified = source.identified;
			item.magical = source.magical;
			item.fireMinDamage = source.fireMinDamage;
			item.fireMaxDamage = source.fireMaxDamage;
			item.lightningMinDamage = source.lightningMinDamage;
			item.lightningMaxDamage = source.lightningMaxDamage;
			item.flags = source.flags;
			item.statFlag = source.statFlag;
			state.equipment.push_back({ source.slot, source.itemSeed, item });
		}
	}
	if (state.belt.empty() && !fixture.initialState.belt.empty()) {
		for (const auto &source : fixture.initialState.belt) {
			ReplayFixtureItemState item;
			item.stableItemId = source.stableItemId;
			item.itemType = source.itemType;
			item.itemIndex = source.itemIndex;
			item.miscId = source.miscId;
			item.spellId = source.spellId;
			item.statFlag = source.statFlag;
			state.belt.push_back({ source.slot, source.itemSeed, item });
		}
	}
	if (state.inventory.empty() && fixture.initialState.goldStackSeed != 0) {
		ReplayFixtureItemState goldState;
		goldState.itemType = 11;
		goldState.stableItemId = fixture.initialState.goldStackStableItemId;
		goldState.value = static_cast<int32_t>(fixture.initialState.gold);
		goldState.identifiedValue = static_cast<int32_t>(fixture.initialState.gold);
		goldState.itemIndex = 0;
		goldState.statFlag = true;
		state.inventory.push_back({
		    0,
		    0,
		    fixture.initialState.goldStackSeed,
		    fixture.initialState.gold,
		    0,
		    goldState,
		});
		if (state.inventoryGrid.empty())
			state.inventoryGrid.assign(40, -1);
		state.inventoryGrid[fixture.initialState.goldStackCell] = 0;
		ReplayFixtureItemState fillerState;
		fillerState.itemType = 1;
		fillerState.statFlag = true;
		uint32_t addedFillers = 0;
		for (size_t cell = 0; cell < state.inventoryGrid.size()
		     && addedFillers < fixture.initialState.inventoryFillerCount;
		     ++cell) {
			if (state.inventoryGrid[cell] != -1)
				continue;
			const size_t inventoryIndex = state.inventory.size();
			state.inventory.push_back({
			    0,
			    0,
			    200U + addedFillers,
			    1,
			    0,
			    fillerState,
			});
			state.inventoryGrid[cell] = static_cast<int32_t>(inventoryIndex);
			++addedFillers;
		}
	}
	if (state.secondaryPlayerEntityId == 0
	    && fixture.initialState.secondaryPlayer.defined) {
		const auto &secondary = fixture.initialState.secondaryPlayer;
		state.secondaryPlayer = secondary.player;
		state.secondaryPlayerCharacterName = secondary.characterName;
		state.secondaryPlayerEntityId = secondary.entityId;
		state.secondaryPlayerLife = secondary.life;
		state.secondaryPlayerLifeMaximum = secondary.lifeMaximum;
		state.secondaryPlayerGold = secondary.gold;
		state.secondaryPlayerPositionX = secondary.positionX;
		state.secondaryPlayerPositionY = secondary.positionY;
		state.secondaryPlayerLevelId = secondary.levelId;
		if (secondary.levelId != 0)
			state.secondaryPlayerVisitedLevelIds.push_back(secondary.levelId);
		state.secondaryPlayerCharacterLevel = secondary.characterLevel;
		state.secondaryPlayerCharacterClassId = secondary.characterClass;
		state.secondaryPlayerMagic = secondary.magic;
		state.secondaryPlayerDexterity = secondary.dexterity;
		state.secondaryPlayerFriendlyMode = secondary.friendlyMode;
		state.secondaryPlayerBlockCapable = secondary.blockCapable;
		state.secondaryPlayerCombatMode = secondary.combatMode;
		state.secondaryPlayerCombatModeEndTick = secondary.combatModeEndTick;
		for (const auto &source : secondary.equipment) {
			ReplayFixtureItemState itemState;
			itemState.stableItemId = source.stableItemId;
			itemState.itemType = source.itemType;
			itemState.itemClass = source.itemClass;
			itemState.itemIndex = source.itemIndex;
			itemState.value = source.value;
			itemState.identifiedValue = source.identifiedValue;
			itemState.identified = source.identified;
			itemState.magical = source.magical;
			itemState.fireMinDamage = source.fireMinDamage;
			itemState.fireMaxDamage = source.fireMaxDamage;
			itemState.lightningMinDamage = source.lightningMinDamage;
			itemState.lightningMaxDamage = source.lightningMaxDamage;
			itemState.flags = source.flags;
			itemState.statFlag = source.statFlag;
			state.secondaryEquipment.push_back({ source.slot, source.itemSeed, itemState });
		}
		state.secondaryInventoryGrid.assign(40, -1);
		if (secondary.inventoryItemDefined) {
			ReplayFixtureItemState itemState;
			itemState.itemType = secondary.inventoryItemType;
			itemState.itemIndex = secondary.inventoryItemIndex;
			itemState.stableItemId = secondary.inventoryItemStableItemId;
			itemState.value = static_cast<int32_t>(secondary.inventoryItemPrice);
			itemState.identifiedValue = itemState.value;
			itemState.statFlag = secondary.inventoryItemStatFlag;
			state.secondaryInventory.push_back({
			    0,
			    secondary.inventoryItemSlot,
			    secondary.inventoryItemSeed,
			    secondary.inventoryItemPrice,
			    0,
			    itemState,
			});
			state.secondaryInventoryGrid[secondary.inventoryItemSlot] = 0;
		}
		if (secondary.heldItemDefined) {
			ReplayFixtureItemState itemState;
			itemState.itemType = secondary.heldItemType;
			itemState.itemIndex = secondary.heldItemIndex;
			itemState.stableItemId = secondary.heldItemStableItemId;
			itemState.value = static_cast<int32_t>(secondary.heldItemPrice);
			itemState.identifiedValue = itemState.value;
			itemState.statFlag = secondary.heldItemStatFlag;
			state.secondaryHeldItemDefined = true;
			state.secondaryHeldItem = {
			    0,
			    secondary.heldItemSlot,
			    secondary.heldItemSeed,
			    secondary.heldItemPrice,
			    0,
			    itemState,
			};
		}
		ReplayFixtureItemState fillerState;
		fillerState.itemType = 1;
		fillerState.itemIndex = 1;
		fillerState.value = 1;
		fillerState.identifiedValue = 1;
		fillerState.statFlag = true;
		for (uint32_t filler = 0; filler < secondary.inventoryFillerCount; ++filler) {
			const auto emptyCell = std::find(
			    state.secondaryInventoryGrid.begin(),
			    state.secondaryInventoryGrid.end(),
			    -1);
			if (emptyCell == state.secondaryInventoryGrid.end())
				break;
			const size_t inventoryIndex = state.secondaryInventory.size();
			state.secondaryInventory.push_back({
			    0,
			    0,
			    200U + filler,
			    1,
			    0,
			    fillerState,
			});
			*emptyCell = static_cast<int32_t>(inventoryIndex);
		}
	}
	if (state.objectEntityId == 0 && fixture.initialState.objectEntityId != 0) {
		state.objectEntityId = fixture.initialState.objectEntityId;
		state.objectId = fixture.initialState.objectId;
		state.objectLevelId = fixture.initialState.levelId;
		state.objectQuestId = fixture.initialState.objectQuestId;
		state.objectPositionX = fixture.initialState.objectPositionX;
		state.objectPositionY = fixture.initialState.objectPositionY;
		state.objectEffectKind = fixture.initialState.objectEffectKind;
		state.objectEffectAmount = fixture.initialState.objectEffectAmount;
		state.objectMissilePassable = fixture.initialState.objectMissilePassable;
		state.objectBreakable = fixture.initialState.objectBreakable;
		state.objectDestroyed = fixture.initialState.objectDestroyed;
	}
	if (state.combatTargetEntityId == 0 && fixture.initialState.combatTargetEntityId != 0) {
		state.combatTargetEntityId = fixture.initialState.combatTargetEntityId;
		state.combatTargetLevelId = fixture.initialState.combatTargetLevelId;
		state.combatTargetPositionX = fixture.initialState.combatTargetPositionX;
		state.combatTargetPositionY = fixture.initialState.combatTargetPositionY;
		state.combatTargetHitPoints = fixture.initialState.combatTargetHitPoints;
		state.combatTargetMaxHitPoints = fixture.initialState.combatTargetMaxHitPoints;
		state.combatTargetArmorClass = fixture.initialState.combatTargetArmorClass;
		state.combatTargetAttackDamage = fixture.initialState.combatTargetAttackDamage;
		state.combatTargetAggroRange = fixture.initialState.combatTargetAggroRange;
		state.combatTargetNativeAi = fixture.initialState.combatTargetNativeAi;
		state.combatTargetMonsterItemSeed = fixture.initialState.combatTargetMonsterItemSeed;
		state.combatTargetMonsterAiSeed = fixture.initialState.combatTargetMonsterAiSeed;
		state.combatTargetMonsterId = fixture.initialState.combatTargetMonsterId;
		state.combatTargetToHit = fixture.initialState.combatTargetToHit;
		state.combatTargetToHitSpecial = fixture.initialState.combatTargetToHitSpecial;
		state.combatTargetMinDamage = fixture.initialState.combatTargetMinDamage;
		state.combatTargetMaxDamage = fixture.initialState.combatTargetMaxDamage;
		state.combatTargetMinDamageSpecial = fixture.initialState.combatTargetMinDamageSpecial == 0
		    ? state.combatTargetMinDamage
		    : fixture.initialState.combatTargetMinDamageSpecial;
		state.combatTargetMaxDamageSpecial = fixture.initialState.combatTargetMaxDamageSpecial == 0
		    ? state.combatTargetMaxDamage
		    : fixture.initialState.combatTargetMaxDamageSpecial;
		state.combatTargetBerserked = fixture.initialState.combatTargetBerserked;
		state.combatTargetIntelligence = fixture.initialState.combatTargetIntelligence;
		state.combatTargetFallenStandAnimationFrame = fixture.initialState.combatTargetFallenStandAnimationFrame;
		state.combatTargetFallenStandAnimationTick = fixture.initialState.combatTargetFallenStandAnimationTick;
		state.combatTargetFallenRallyTicks = fixture.initialState.combatTargetFallenRallyTicks;
		state.combatTargetActiveForTicks = fixture.initialState.combatTargetActiveForTicks;
		state.combatTargetGargoyleSpecialActive = fixture.initialState.combatTargetGargoyleSpecialActive;
		state.combatTargetHorkDemonDelayTicks = fixture.initialState.combatTargetHorkDemonDelayTicks;
		state.combatTargetSkeletonKingDelayTicks = fixture.initialState.combatTargetSkeletonKingDelayTicks;
		state.combatTargetSkeletonMeleeDelayTicks = fixture.initialState.combatTargetSkeletonMeleeDelayTicks;
		state.combatTargetSnakeDelayTicks = fixture.initialState.combatTargetSnakeDelayTicks;
		state.combatTargetCounselorDelayTicks = fixture.initialState.combatTargetCounselorDelayTicks;
		state.combatTargetCounselorRetreatTicks = fixture.initialState.combatTargetCounselorRetreatTicks;
		state.combatTargetRangedDelayTicks = fixture.initialState.combatTargetRangedDelayTicks;
		state.combatTargetRangedRetreatMinimumDistance = fixture.initialState.combatTargetRangedRetreatMinimumDistance;
		state.combatTargetRangedRetreatChanceBase = fixture.initialState.combatTargetRangedRetreatChanceBase;
		state.combatTargetRangedRetreatChanceIntelligenceScale = fixture.initialState.combatTargetRangedRetreatChanceIntelligenceScale;
		state.combatTargetMegaDelayTicks = fixture.initialState.combatTargetMegaDelayTicks;
		state.combatTargetSkeletonRangedMovementIdleTicks = fixture.initialState.combatTargetSkeletonRangedMovementIdleTicks;
		state.combatTargetSkeletonRangedMovementMode = fixture.initialState.combatTargetSkeletonRangedMovementMode;
		state.combatTargetSneakHidden = fixture.initialState.combatTargetSneakHidden;
		state.combatTargetSneakRetreatTicks = fixture.initialState.combatTargetSneakRetreatTicks;
		state.combatTargetBatRetreatTicks = fixture.initialState.combatTargetBatRetreatTicks;
		state.combatTargetBatRetreatDirection = fixture.initialState.combatTargetBatRetreatDirection;
		state.combatTargetScavengerHealingTicks = fixture.initialState.combatTargetScavengerHealingTicks;
		state.combatTargetScavengerCorpseEntityId = fixture.initialState.combatTargetScavengerCorpseEntityId;
		state.combatTargetFatMovementIdleTicks = fixture.initialState.combatTargetFatMovementIdleTicks;
		state.combatTargetFatMovementMode = fixture.initialState.combatTargetFatMovementMode;
		state.combatTargetFireResistance = fixture.initialState.combatTargetFireResistance;
		state.combatTargetLightningResistance = fixture.initialState.combatTargetLightningResistance;
		state.combatTargetMagicResistance = fixture.initialState.combatTargetMagicResistance;
		state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
		state.combatTargetUndead = fixture.initialState.combatTargetUndead;
		state.combatTargetMoving = fixture.initialState.combatTargetMoving;
		state.combatTargetFuturePositionX = fixture.initialState.combatTargetFuturePositionX;
		state.combatTargetFuturePositionY = fixture.initialState.combatTargetFuturePositionY;
		state.combatTargetPetrified = fixture.initialState.combatTargetPetrified;
		state.combatTargetStoneCurseImmune = fixture.initialState.combatTargetStoneCurseImmune;
	}
	state.corpses = fixture.initialState.corpses;
	if (state.combatTargetEntityId != 0) {
		if (!state.combatTargetMoving
		    && state.combatTargetFuturePositionX == 0
		    && state.combatTargetFuturePositionY == 0) {
			state.combatTargetFuturePositionX = state.combatTargetPositionX;
			state.combatTargetFuturePositionY = state.combatTargetPositionY;
		}
		if ((state.combatTargetMoving
		        && std::max(
		               std::abs(state.combatTargetFuturePositionX - state.combatTargetPositionX),
		               std::abs(state.combatTargetFuturePositionY - state.combatTargetPositionY))
		            != 1)
		    || (!state.combatTargetMoving
		        && (state.combatTargetFuturePositionX != state.combatTargetPositionX
		            || state.combatTargetFuturePositionY != state.combatTargetPositionY))) {
			error = "Replay fixture combat target has invalid occupancy state.";
			return false;
		}
		const int64_t hitPointsQ6 = state.combatTargetHitPointsQ6 == 0 && state.combatTargetHitPoints != 0
		    ? static_cast<int64_t>(state.combatTargetHitPoints) * 64
		    : state.combatTargetHitPointsQ6;
		const int64_t maxHitPointsQ6 = state.combatTargetMaxHitPointsQ6 == 0 && state.combatTargetMaxHitPoints != 0
		    ? static_cast<int64_t>(state.combatTargetMaxHitPoints) * 64
		    : state.combatTargetMaxHitPointsQ6;
		if (hitPointsQ6 < 0 || hitPointsQ6 > std::numeric_limits<int32_t>::max()
		    || maxHitPointsQ6 < hitPointsQ6 || maxHitPointsQ6 > std::numeric_limits<int32_t>::max()) {
			error = "Replay fixture combat target has invalid fixed-point hit points.";
			return false;
		}
		state.combatTargetHitPointsQ6 = static_cast<int32_t>(hitPointsQ6);
		state.combatTargetMaxHitPointsQ6 = static_cast<int32_t>(maxHitPointsQ6);
		state.combatTargetHitPoints = ToWholeCeilingQ6(state.combatTargetHitPointsQ6);
		state.combatTargetMaxHitPoints = ToWholeCeilingQ6(state.combatTargetMaxHitPointsQ6);
	}
	if (state.projectiles.empty() && !fixture.initialProjectiles.empty()) {
		for (const auto &source : fixture.initialProjectiles) {
			ReplayFixtureProjectileState projectile;
			projectile.entityId = source.entityId;
			projectile.sourceEntityId = source.sourceEntityId;
			projectile.targetEntityId = source.targetEntityId;
			projectile.spellId = source.spellId;
			projectile.levelId = source.levelId;
			projectile.positionX = source.positionX;
			projectile.positionY = source.positionY;
			projectile.targetX = source.targetX;
			projectile.targetY = source.targetY;
			projectile.startX = source.positionX;
			projectile.startY = source.positionY;
			projectile.damage = source.damage;
			projectile.damageType = source.damageType;
			projectile.areaRadius = source.areaRadius;
			projectile.remainingTicks = source.remainingTicks;
			projectile.behavior = source.behavior;
			projectile.phase = source.phase;
			projectile.speed = source.speed;
			projectile.impactLifetime = source.impactLifetime;
			projectile.damageMinimum = source.damageMinimum;
			projectile.damageMaximum = source.damageMaximum;
			state.projectiles.push_back(std::move(projectile));
		}
	}
	ApplyReplayQuestSelection(state, fixture);
	std::vector<ReplayFixtureCommand> commands = fixture.commands;
	std::stable_sort(commands.begin(), commands.end(), [](const auto &left, const auto &right) {
		return IsReplayCommandOrderBefore(left.order, right.order);
	});
	if (!fixture.contentManifest.id.empty()) {
		for (const auto &command : commands) {
			if (std::none_of(fixture.checkpoints.begin(), fixture.checkpoints.end(), [&](const auto &checkpoint) {
				    return checkpoint.tick == command.order.targetTick;
			    })) {
				error = "Replay fixture is missing a checkpoint at command tick " + std::to_string(command.order.targetTick);
				return false;
			}
		}
	}
	std::vector<uint64_t> timelineTicks;
	for (const auto &command : commands)
		timelineTicks.push_back(command.order.targetTick);
	if (!fixture.contentManifest.id.empty()) {
		for (const auto &checkpoint : fixture.checkpoints)
			timelineTicks.push_back(checkpoint.tick);
	}
	std::sort(timelineTicks.begin(), timelineTicks.end());
	timelineTicks.erase(std::unique(timelineTicks.begin(), timelineTicks.end()), timelineTicks.end());

	for (const uint64_t timelineTick : timelineTicks) {
		AdvanceReplayStateToTick(state, fixture, timelineTick);
		for (const ReplayFixtureCommand &command : commands) {
			if (command.order.targetTick != timelineTick)
				continue;
			bool accepted = false;
			std::string rejectReason;
			if (!command.player.empty() && command.player == state.secondaryPlayer) {
				if (command.kind != "Move" && command.kind != "Attack" && command.kind != "UsePortal" && command.kind != "DropItem" && command.kind != "PickupWorldItem" && command.kind != "DropHeldItem") {
					error = "Secondary replay participant supports Move, Attack, UsePortal, DropItem, PickupWorldItem, and DropHeldItem commands only.";
					return false;
				}
				if (command.kind == "Move") {
					const int32_t targetX = state.secondaryPlayerPositionX + command.directionX;
					const int32_t targetY = state.secondaryPlayerPositionY + command.directionY;
					const bool occupiedByPrimary = targetX == state.positionX
					    && targetY == state.positionY
					    && state.life > 0
					    && state.levelId == state.secondaryPlayerLevelId;
					if ((command.directionX != 0 || command.directionY != 0)
					    && targetX >= 0 && targetX < 40
					    && targetY >= 0 && targetY < 40
					    && !occupiedByPrimary) {
						state.secondaryPlayerFacingDirection = static_cast<uint32_t>(GetDirection(
						    state.secondaryPlayerPositionX,
						    state.secondaryPlayerPositionY,
						    targetX,
						    targetY));
						state.secondaryPlayerPositionX = targetX;
						state.secondaryPlayerPositionY = targetY;
						accepted = true;
					}
				} else if (command.kind == "UsePortal") {
					if (command.portalId == state.portalId) {
						state.secondaryPlayerLevelId = state.portalDestinationLevelId;
						if (state.secondaryPlayerLevelId != 0
						    && std::find(
							       state.secondaryPlayerVisitedLevelIds.begin(),
							       state.secondaryPlayerVisitedLevelIds.end(),
							       state.secondaryPlayerLevelId)
							        == state.secondaryPlayerVisitedLevelIds.end())
							state.secondaryPlayerVisitedLevelIds.push_back(state.secondaryPlayerLevelId);
						state.secondaryPlayerPositionX = state.portalDestinationX;
						state.secondaryPlayerPositionY = state.portalDestinationY;
						accepted = true;
					}
				} else if (command.kind == "PickupWorldItem") {
					const int distance = std::max(
					    std::abs(state.secondaryPlayerPositionX - state.worldItemPositionX),
					    std::abs(state.secondaryPlayerPositionY - state.worldItemPositionY));
					const int targetCell = FindReplayInventoryFirstFit(
					    state.secondaryInventoryGrid,
					    state.worldItemState);
					const bool validTarget = command.worldItemEntityId == state.worldItemEntityId
					    && state.worldItemSeed != 0
					    && state.secondaryPlayerLevelId == state.worldItemLevelId
					    && distance <= 1;
					const bool isGold = state.worldItemState.itemType == 11
					    && state.worldItemState.itemIndex == 0;
					const bool heldItemDropOccupied = (state.worldItemEntityId != 0
					        && state.worldItemLevelId == state.secondaryPlayerLevelId
					        && state.worldItemPositionX == state.secondaryPlayerPositionX
					        && state.worldItemPositionY == state.secondaryPlayerPositionY)
					    || std::any_of(state.worldItems.begin(), state.worldItems.end(), [&](const auto &item) {
						       return item.levelId == state.secondaryPlayerLevelId
						           && item.positionX == state.secondaryPlayerPositionX
						           && item.positionY == state.secondaryPlayerPositionY;
					       })
					    || (state.entityId != 0
					        && state.life > 0
					        && state.levelId == state.secondaryPlayerLevelId
					        && state.positionX == state.secondaryPlayerPositionX
					        && state.positionY == state.secondaryPlayerPositionY)
					    || (state.combatTargetEntityId != 0
					        && state.combatTargetHitPointsQ6 > 0
					        && state.combatTargetLevelId == state.secondaryPlayerLevelId
					        && state.combatTargetPositionX == state.secondaryPlayerPositionX
					        && state.combatTargetPositionY == state.secondaryPlayerPositionY)
					    || (state.objectEntityId != 0
					        && !state.objectActivated
					        && !state.objectDestroyed
					        && state.objectLevelId == state.secondaryPlayerLevelId
					        && state.objectPositionX == state.secondaryPlayerPositionX
					        && state.objectPositionY == state.secondaryPlayerPositionY);
					const auto dropSecondaryHeldItem = [&]() {
						if (!state.secondaryHeldItemDefined)
							return;
						state.worldItems.push_back({
						    AllocateReplayWorldItemEntityId(state),
						    state.secondaryPlayerLevelId,
						    state.secondaryPlayerPositionX,
						    state.secondaryPlayerPositionY,
						    state.secondaryHeldItem.itemSeed,
						    state.secondaryHeldItem.price,
						    state.secondaryHeldItem.state,
						});
						state.secondaryHeldItemDefined = false;
						state.secondaryHeldItem = { };
					};
					if (!validTarget) {
						rejectReason = "InvalidTarget";
					} else if (state.secondaryHeldItemDefined
					    && (state.secondaryPlayerPositionX < 0
					        || state.secondaryPlayerPositionX >= 40
					        || state.secondaryPlayerPositionY < 0
					        || state.secondaryPlayerPositionY >= 40
					        || heldItemDropOccupied)) {
						rejectReason = "NotAllowed";
					} else if (isGold) {
						if (state.worldItemState.value <= 0
						    || state.worldItemState.value > 5000
						    || state.worldItemState.identifiedValue != state.worldItemState.value
						    || !state.worldItemState.statFlag
						    || state.worldItemPrice != static_cast<uint32_t>(state.worldItemState.value)
						    || state.secondaryPlayerGold > static_cast<uint32_t>(std::numeric_limits<int32_t>::max())
						    || state.worldItemPrice > static_cast<uint32_t>(std::numeric_limits<int32_t>::max()) - state.secondaryPlayerGold
						    || std::numeric_limits<uint32_t>::max() - state.secondaryPlayerGold < state.worldItemPrice) {
							rejectReason = "Malformed";
						} else {
							dropSecondaryHeldItem();
							state.secondaryPlayerGold += state.worldItemPrice;
							state.worldItemEntityId = 0;
							accepted = true;
						}
					} else if (targetCell >= 0 && state.secondaryInventory.size() < 40) {
						dropSecondaryHeldItem();
						const size_t inventoryIndex = state.secondaryInventory.size();
						state.secondaryInventory.push_back({
						    0,
						    0,
						    state.worldItemSeed,
						    state.worldItemPrice,
						    command.order.targetTick,
						    state.worldItemState,
						});
						FillReplayInventoryGrid(
						    state.secondaryInventoryGrid,
						    targetCell,
						    inventoryIndex,
						    state.worldItemState);
						state.worldItemEntityId = 0;
						accepted = true;
					} else {
						dropSecondaryHeldItem();
						state.secondaryHeldItemDefined = true;
						state.secondaryHeldItem = {
						    0,
						    0,
						    state.worldItemSeed,
						    state.worldItemPrice,
						    command.order.targetTick,
						    state.worldItemState,
						};
						state.worldItemEntityId = 0;
						accepted = true;
					}
				} else if (command.kind == "DropHeldItem") {
					const int distance = std::max(
					    std::abs(state.secondaryPlayerPositionX - command.targetX),
					    std::abs(state.secondaryPlayerPositionY - command.targetY));
					const bool targetOccupied = (state.worldItemEntityId != 0
					        && state.worldItemLevelId == state.secondaryPlayerLevelId
					        && state.worldItemPositionX == command.targetX
					        && state.worldItemPositionY == command.targetY)
					    || std::any_of(state.worldItems.begin(), state.worldItems.end(), [&](const auto &item) {
						       return item.levelId == state.secondaryPlayerLevelId
						           && item.positionX == command.targetX
						           && item.positionY == command.targetY;
					       })
					    || (state.entityId != 0
					        && state.life > 0
					        && state.levelId == state.secondaryPlayerLevelId
					        && state.positionX == command.targetX
					        && state.positionY == command.targetY)
					    || (state.combatTargetEntityId != 0
					        && state.combatTargetHitPointsQ6 > 0
					        && state.combatTargetLevelId == state.secondaryPlayerLevelId
					        && state.combatTargetPositionX == command.targetX
					        && state.combatTargetPositionY == command.targetY)
					    || (state.objectEntityId != 0
					        && !state.objectActivated
					        && !state.objectDestroyed
					        && state.objectLevelId == state.secondaryPlayerLevelId
					        && state.objectPositionX == command.targetX
					        && state.objectPositionY == command.targetY);
					if (state.secondaryHeldItemDefined
					    && distance <= 1
					    && command.targetX >= 0 && command.targetX < 40
					    && command.targetY >= 0 && command.targetY < 40
					    && !targetOccupied) {
						state.worldItemEntityId = AllocateReplayWorldItemEntityId(state);
						state.worldItemLevelId = state.secondaryPlayerLevelId;
						state.worldItemPositionX = command.targetX;
						state.worldItemPositionY = command.targetY;
						state.worldItemSeed = state.secondaryHeldItem.itemSeed;
						state.worldItemPrice = state.secondaryHeldItem.price;
						state.worldItemState = state.secondaryHeldItem.state;
						state.secondaryHeldItemDefined = false;
						state.secondaryHeldItem = { };
						accepted = true;
					} else if (!state.secondaryHeldItemDefined) {
						rejectReason = "InvalidTarget";
					} else {
						rejectReason = "NotAllowed";
					}
				} else if (command.kind == "DropItem") {
					ReplayFixtureInventoryItem source;
					bool sourceFound = false;
					if (command.itemLocation == "Inventory" && command.storeSlot < state.secondaryInventory.size()) {
						source = state.secondaryInventory[command.storeSlot];
						sourceFound = true;
					}
					const int distance = std::max(
					    std::abs(state.secondaryPlayerPositionX - command.targetX),
					    std::abs(state.secondaryPlayerPositionY - command.targetY));
					const bool targetOccupied = (state.worldItemEntityId != 0
					        && state.worldItemLevelId == state.secondaryPlayerLevelId
					        && state.worldItemPositionX == command.targetX
					        && state.worldItemPositionY == command.targetY)
					    || std::any_of(state.worldItems.begin(), state.worldItems.end(), [&](const auto &item) {
						       return item.levelId == state.secondaryPlayerLevelId
						           && item.positionX == command.targetX
						           && item.positionY == command.targetY;
					       })
					    || (state.entityId != 0
					        && state.life > 0
					        && state.levelId == state.secondaryPlayerLevelId
					        && state.positionX == command.targetX
					        && state.positionY == command.targetY)
					    || (state.combatTargetEntityId != 0
					        && state.combatTargetHitPointsQ6 > 0
					        && state.combatTargetLevelId == state.secondaryPlayerLevelId
					        && state.combatTargetPositionX == command.targetX
					        && state.combatTargetPositionY == command.targetY)
					    || (state.objectEntityId != 0
					        && !state.objectActivated
					        && !state.objectDestroyed
					        && state.objectLevelId == state.secondaryPlayerLevelId
					        && state.objectPositionX == command.targetX
					        && state.objectPositionY == command.targetY);
					if (sourceFound
					    && (command.itemLocation == "Inventory")
					    && !state.heldItemDefined
					    && (command.stableItemId == 0
					        || source.state.stableItemId == command.stableItemId)
					    && distance <= 1
					    && command.targetX >= 0 && command.targetX < 40
					    && command.targetY >= 0 && command.targetY < 40
					    && !targetOccupied) {
						state.worldItemEntityId = AllocateReplayWorldItemEntityId(state);
						state.worldItemLevelId = state.secondaryPlayerLevelId;
						state.worldItemPositionX = command.targetX;
						state.worldItemPositionY = command.targetY;
						state.worldItemSeed = source.itemSeed;
						state.worldItemPrice = source.price;
						state.worldItemState = source.state;
						RemoveReplayInventoryItem(state.secondaryInventory, state.secondaryInventoryGrid, command.storeSlot);
						accepted = true;
					} else if (!sourceFound
					    || state.heldItemDefined
					    || (command.stableItemId != 0 && source.state.stableItemId != command.stableItemId)) {
						rejectReason = "InvalidTarget";
					} else {
						rejectReason = "NotAllowed";
					}
				} else if (command.targetEntityId == state.combatTargetEntityId
				    && state.combatTargetHitPointsQ6 > 0
				    && state.combatTargetLevelId == state.secondaryPlayerLevelId
				    && std::max(
					       std::abs(state.secondaryPlayerPositionX - state.combatTargetPositionX),
					       std::abs(state.secondaryPlayerPositionY - state.combatTargetPositionY)) <= 1) {
					const int32_t damage = std::max(1, 10 - state.combatTargetArmorClass);
					state.combatTargetHitPointsQ6 = std::max(0, state.combatTargetHitPointsQ6 - damage * 64);
					state.combatTargetHitPoints = ToWholeCeilingQ6(state.combatTargetHitPointsQ6);
					accepted = true;
				}
			} else if (command.kind == "OpenStore") {
				state.activeStoreId = command.storeId;
				if (command.storeId == 10)
					state.activeStoreItems.clear();
				accepted = true;
			} else if (command.kind == "BuyItem") {
				if (state.activeStoreId == command.storeId) {
					auto item = std::find_if(state.activeStoreItems.begin(), state.activeStoreItems.end(), [&](const auto &candidate) {
						return candidate.storeSlot == command.storeSlot;
					});
					if (item != state.activeStoreItems.end() && state.gold >= item->price) {
						const bool hasGoldTopology = HasReplayGoldTopology(state);
						const int targetCell = hasGoldTopology ? FindReplayInventoryPlacement(state, item->state) : -1;
						if (!hasGoldTopology || (targetCell >= 0 && state.inventory.size() < 40)) {
							const bool spent = hasGoldTopology ? SpendReplayGold(state, item->price) : (state.gold -= item->price, true);
							if (spent) {
								const size_t inventoryIndex = state.inventory.size();
								state.inventory.push_back({ command.storeId, item->storeSlot, item->itemSeed, item->price, command.order.targetTick, item->state });
								if (hasGoldTopology)
									FillReplayInventoryGrid(state, targetCell, inventoryIndex, item->state);
								state.activeStoreItems.erase(item);
								accepted = true;
							}
						}
					}
				}
			} else if (command.kind == "SellItem") {
				if (state.activeStoreId != 0 && command.storeSlot < state.inventory.size()) {
					const size_t itemIndex = command.storeSlot;
					const auto &item = state.inventory[itemIndex];
					const int value = item.state.magical != 0 && item.state.identified ? item.state.identifiedValue : item.state.value;
					const uint32_t salePrice = static_cast<uint32_t>(std::max(value / 4, 1));
					if (HasReplayGoldTopology(state) && !IsReplayGold(item)) {
						ReplayFixtureExecutionState candidate = state;
						RemoveReplayInventoryItem(candidate, itemIndex);
						if (AddReplayGold(candidate, salePrice)) {
							state = std::move(candidate);
							accepted = true;
						}
					} else if (!IsReplayGold(item)) {
						state.gold += salePrice;
						RemoveReplayInventoryItem(state, itemIndex);
						accepted = true;
					}
				}
			} else if (command.kind == "RefillMana") {
				if (state.activeStoreId == 10 && state.mana < state.manaMaximum && state.gold >= 10) {
					if (HasReplayGoldTopology(state))
						accepted = SpendReplayGold(state, 10);
					else {
						state.gold -= 10;
						accepted = true;
					}
					if (accepted)
						state.mana = state.manaMaximum;
				}
			} else if (command.kind == "Move") {
				const int32_t targetX = state.positionX + command.directionX;
				const int32_t targetY = state.positionY + command.directionY;
				if ((command.directionX != 0 || command.directionY != 0) && targetX >= 0 && targetX < 40 && targetY >= 0 && targetY < 40) {
					if (fixture.playerActionTiming.defined) {
						if (CanStartReplayPlayerAction(state, command.order.targetTick)) {
							const uint64_t endTick = command.order.targetTick + fixture.playerActionTiming.walkingFrames;
							state.currentAction = {
								true,
								1,
								command.order.targetTick,
								endTick,
								endTick,
								endTick,
								false,
								command.directionX,
								command.directionY,
							};
							state.combatMode = GetReplayWalkMode(command.directionX, command.directionY);
							state.combatModeEndTick = endTick;
							accepted = true;
						}
					} else {
						state.positionX = targetX;
						state.positionY = targetY;
						accepted = true;
					}
				}
			} else if (command.kind == "Attack") {
				if (fixture.playerActionTiming.defined) {
					const bool ranged = UsesReplayRangedAttack(state);
					const bool includesFirstFrame = state.currentAction.defined
					    && state.currentAction.effectApplied
					    && (state.combatMode == 4 || state.combatMode == 5);
					const uint32_t skippedFrames = GetReplayAttackSkippedFrames(
					    state,
					    ranged,
					    fixture.gameVariant,
					    includesFirstFrame);
					if (CanStartReplayPlayerAction(state, command.order.targetTick)
					    && IsReplayAttackTargetValid(state, command.targetEntityId, ranged)
					    && fixture.playerActionTiming.attackActionFrame > skippedFrames
					    && fixture.playerActionTiming.attackFrames > skippedFrames) {
						const auto &timing = fixture.playerActionTiming;
						const uint64_t effectTick = command.order.targetTick + timing.attackActionFrame - 1 - skippedFrames;
						const uint64_t readyTick = command.order.targetTick + timing.attackActionFrame - skippedFrames;
						const uint64_t endTick = command.order.targetTick + timing.attackFrames - 1 - skippedFrames;
						state.currentAction = {
							true,
							2,
							command.order.targetTick,
							effectTick,
							std::min(readyTick, endTick),
							endTick,
							false,
							0,
							0,
							command.targetEntityId,
						};
						state.combatMode = ranged ? 5 : 4;
						state.combatModeEndTick = endTick;
						accepted = true;
					}
				} else {
					accepted = ApplyReplayAttack(
					    state,
					    command.targetEntityId,
					    command.order.targetTick,
					    UsesReplayRangedAttack(state));
				}
			} else if (command.kind == "Cast") {
				if (fixture.playerActionTiming.defined) {
					if (CanStartReplayPlayerAction(state, command.order.targetTick)
					    && ApplyReplayCast(state, command, false)) {
						const auto &timing = fixture.playerActionTiming;
						const uint64_t effectTick = command.order.targetTick + timing.castingActionFrame;
						const uint64_t endTick = command.order.targetTick + timing.castingFrames - 1;
						state.currentAction = {
							true,
							3,
							command.order.targetTick,
							effectTick,
							effectTick,
							endTick,
							false,
							0,
							0,
							command.targetEntityId,
							true,
							command.spellId,
							command.targetEntityId,
							command.targetX,
							command.targetY,
						};
						state.combatMode = 9;
						state.combatModeEndTick = endTick;
						accepted = true;
					}
				} else {
					accepted = ApplyReplayCast(state, command);
				}
				if (!accepted
				    && command.spellId == 33
				    && state.spellProjectileBehavior == TelekinesisBehavior)
					rejectReason = GetReplayCastRejectReason(state, command);
			} else if (command.kind == "UsePortal") {
				if (command.portalId == state.portalId) {
					if (!fixture.playerActionTiming.defined
					    || (state.combatMode == 0 && !state.currentAction.defined)) {
						state.levelId = state.portalDestinationLevelId;
						if (std::find(state.visitedLevelIds.begin(), state.visitedLevelIds.end(), state.levelId) == state.visitedLevelIds.end())
							state.visitedLevelIds.push_back(state.levelId);
						state.positionX = state.portalDestinationX;
						state.positionY = state.portalDestinationY;
						state.manaShieldActive = false;
						state.statusEffects.erase(std::remove_if(state.statusEffects.begin(), state.statusEffects.end(), [](const auto &effect) {
							return effect.behavior == 1 || effect.behavior == 2;
						}),
						    state.statusEffects.end());
						if (fixture.playerActionTiming.defined) {
							const uint64_t endTick = command.order.targetTick + 1;
							state.currentAction = {
								.defined = true,
								.kind = 5,
								.startedAtTick = command.order.targetTick,
								.effectTick = command.order.targetTick,
								.readyTick = endTick,
								.endTick = endTick,
								.effectApplied = true,
								.targetEntityId = command.portalId,
							};
							state.combatMode = 10;
							state.combatModeEndTick = endTick;
						}
						accepted = true;
					}
				}
			} else if (command.kind == "RestartTown") {
				if (fixture.playerActionTiming.defined
				    && state.life == 0
				    && state.combatMode == 8
				    && !state.currentAction.defined) {
					state.levelId = 0;
					if (std::find(state.visitedLevelIds.begin(), state.visitedLevelIds.end(), state.levelId) == state.visitedLevelIds.end())
						state.visitedLevelIds.push_back(state.levelId);
					state.positionX = 75;
					state.positionY = 68;
					state.life = std::min(state.lifeMaximum, 64);
					state.mana = 0;
					state.manaShieldActive = false;
					state.statusEffects.erase(std::remove_if(state.statusEffects.begin(), state.statusEffects.end(), [](const auto &effect) {
						return effect.behavior == 1 || effect.behavior == 2;
					}),
					    state.statusEffects.end());
					state.activeStoreId = 0;
					const uint64_t endTick = command.order.targetTick + 1;
					state.currentAction = {
						.defined = true,
						.kind = 6,
						.startedAtTick = command.order.targetTick,
						.effectTick = command.order.targetTick,
						.readyTick = endTick,
						.endTick = endTick,
						.effectApplied = true,
					};
					state.combatMode = 10;
					state.combatModeEndTick = endTick;
					accepted = true;
				}
			} else if (command.kind == "PickupWorldItem") {
				const int distance = std::max(std::abs(state.positionX - state.worldItemPositionX), std::abs(state.positionY - state.worldItemPositionY));
				if (command.worldItemEntityId == state.worldItemEntityId
				    && state.worldItemSeed != 0
				    && state.levelId == state.worldItemLevelId
				    && distance <= 1) {
					const ReplayFixtureItemState pickedItemState = state.worldItemState;
					const uint32_t pickedItemSeed = state.worldItemSeed;
					const uint32_t pickedItemPrice = state.worldItemPrice;
					const bool isGold = pickedItemState.itemType == 11
					    && pickedItemState.itemIndex == 0;
					const bool malformedGold = isGold
					    && (pickedItemState.value <= 0
					        || pickedItemState.value > 5000
					        || pickedItemState.identifiedValue != pickedItemState.value
					        || !pickedItemState.statFlag
					        || pickedItemPrice != static_cast<uint32_t>(pickedItemState.value)
					        || static_cast<uint64_t>(state.gold) + pickedItemPrice > std::numeric_limits<int32_t>::max()
					        || std::numeric_limits<uint32_t>::max() - state.gold < pickedItemPrice);
					if (malformedGold) {
						rejectReason = "Malformed";
					} else if (state.heldItemDefined
					    && state.positionX == state.worldItemPositionX
					    && state.positionY == state.worldItemPositionY) {
						rejectReason = "NotAllowed";
					} else {
						if (state.heldItemDefined) {
						state.worldItems.push_back({
						    AllocateReplayWorldItemEntityId(state),
						    state.levelId,
						    state.positionX,
						    state.positionY,
						    state.heldItem.itemSeed,
						    state.heldItem.price,
						    state.heldItem.state,
						});
						state.heldItemDefined = false;
						state.heldItem = { };
						}
						state.worldItemState = pickedItemState;
						if (isGold) {
						if (HasReplayGoldTopology(state)) {
							const uint32_t remainder = AddReplayGoldPartial(state, pickedItemPrice);
							if (remainder != 0) {
								state.heldItemDefined = true;
								state.heldItem = {
								    0,
								    0,
								    pickedItemSeed,
								    remainder,
								    command.order.targetTick,
								    pickedItemState,
								};
								state.heldItem.state.value = static_cast<int32_t>(remainder);
								state.heldItem.state.identifiedValue = static_cast<int32_t>(remainder);
							}
						} else {
							state.gold += pickedItemPrice;
						}
						state.worldItemEntityId = 0;
						accepted = true;
					} else {
						const int targetCell = CanPlaceReplayInventoryItem(
						    state.inventoryGrid,
						    0,
						    pickedItemState.inventoryWidth,
						    pickedItemState.inventoryHeight)
						    ? 0
						    : FindReplayInventoryPlacement(state, pickedItemState);
						if (targetCell >= 0 && state.inventory.size() < 40) {
							const size_t inventoryIndex = state.inventory.size();
							state.inventory.push_back({ 0, 0, pickedItemSeed, pickedItemPrice, command.order.targetTick, pickedItemState });
							FillReplayInventoryGrid(state, targetCell, inventoryIndex, pickedItemState);
						} else {
							state.heldItemDefined = true;
							state.heldItem = { 0, 0, pickedItemSeed, pickedItemPrice, command.order.targetTick, pickedItemState };
						}
						state.worldItemEntityId = 0;
						accepted = true;
						}
					}
				}
			} else if (command.kind == "DropHeldItem") {
				const int originX = state.currentAction.defined && state.currentAction.kind == 1
				    ? state.positionX + state.currentAction.directionX
				    : state.positionX;
				const int originY = state.currentAction.defined && state.currentAction.kind == 1
				    ? state.positionY + state.currentAction.directionY
				    : state.positionY;
				const int distance = std::max(std::abs(originX - command.targetX), std::abs(originY - command.targetY));
				const bool targetOccupied = (state.combatTargetEntityId != 0
				        && state.combatTargetHitPoints > 0
				        && state.combatTargetLevelId == state.levelId
				        && state.combatTargetPositionX == command.targetX
				        && state.combatTargetPositionY == command.targetY)
				    || (state.secondaryPlayerEntityId != 0
				        && state.secondaryPlayerLife > 0
				        && state.secondaryPlayerLevelId == state.levelId
				        && state.secondaryPlayerPositionX == command.targetX
				        && state.secondaryPlayerPositionY == command.targetY)
				    || (state.objectEntityId != 0
				        && !state.objectActivated
				        && !state.objectDestroyed
				        && state.objectLevelId == state.levelId
				        && state.objectPositionX == command.targetX
				        && state.objectPositionY == command.targetY)
				    || (state.worldItemEntityId != 0
				        && state.worldItemLevelId == state.levelId
				        && state.worldItemPositionX == command.targetX
				        && state.worldItemPositionY == command.targetY);
				if (state.heldItemDefined
				    && distance <= 1
				    && command.targetX >= 0 && command.targetX < 40
				    && command.targetY >= 0 && command.targetY < 40
				    && !targetOccupied) {
					uint32_t entityId = 0x40000000U;
					while (entityId == state.entityId
					    || entityId == state.secondaryPlayerEntityId
					    || entityId == state.combatTargetEntityId
					    || entityId == state.objectEntityId)
						++entityId;
					state.worldItemEntityId = entityId;
					state.worldItemLevelId = state.levelId;
					state.worldItemPositionX = command.targetX;
					state.worldItemPositionY = command.targetY;
					state.worldItemSeed = state.heldItem.itemSeed;
					state.worldItemPrice = state.heldItem.price;
					state.worldItemState = state.heldItem.state;
					state.heldItemDefined = false;
					state.heldItem = { };
					accepted = true;
				}
			} else if (command.kind == "DropItem") {
				const int originX = state.currentAction.defined && state.currentAction.kind == 1
				    ? state.positionX + state.currentAction.directionX
				    : state.positionX;
				const int originY = state.currentAction.defined && state.currentAction.kind == 1
				    ? state.positionY + state.currentAction.directionY
				    : state.positionY;
				const int distance = std::max(std::abs(originX - command.targetX), std::abs(originY - command.targetY));
				const bool targetOccupied = (state.combatTargetEntityId != 0
				        && state.combatTargetHitPoints > 0
				        && state.combatTargetLevelId == state.levelId
				        && state.combatTargetPositionX == command.targetX
				        && state.combatTargetPositionY == command.targetY)
				    || (state.secondaryPlayerEntityId != 0
				        && state.secondaryPlayerLife > 0
				        && state.secondaryPlayerLevelId == state.levelId
				        && state.secondaryPlayerPositionX == command.targetX
				        && state.secondaryPlayerPositionY == command.targetY)
				    || (state.objectEntityId != 0
				        && !state.objectActivated
				        && !state.objectDestroyed
				        && state.objectLevelId == state.levelId
				        && state.objectPositionX == command.targetX
				        && state.objectPositionY == command.targetY)
				    || (state.worldItemEntityId != 0
				        && state.worldItemLevelId == state.levelId
				        && state.worldItemPositionX == command.targetX
				        && state.worldItemPositionY == command.targetY);
				ReplayFixtureInventoryItem source;
				bool sourceFound = false;
				if (command.itemLocation == "Inventory" && command.storeSlot < state.inventory.size()) {
					source = state.inventory[command.storeSlot];
					sourceFound = true;
				} else if (command.itemLocation == "Belt") {
					auto item = std::find_if(state.belt.begin(), state.belt.end(), [&](const auto &candidate) {
						return candidate.slot == command.storeSlot;
					});
					if (item != state.belt.end()) {
						const int value = item->state.identified && item->state.identifiedValue > 0
						    ? item->state.identifiedValue
						    : item->state.value;
						source = { 0, item->slot, item->itemSeed, static_cast<uint32_t>(std::max(value, 1)), 0, item->state };
						sourceFound = true;
					}
				} else if (command.itemLocation == "Equipment") {
					auto item = std::find_if(state.equipment.begin(), state.equipment.end(), [&](const auto &candidate) {
						return candidate.slot == command.storeSlot;
					});
					if (item != state.equipment.end()) {
						const int value = item->state.identified && item->state.identifiedValue > 0
						    ? item->state.identifiedValue
						    : item->state.value;
						source = { 0, item->slot, item->itemSeed, static_cast<uint32_t>(std::max(value, 1)), 0, item->state };
						sourceFound = true;
					}
				}
				if (sourceFound
				    && !state.heldItemDefined
				    && (command.stableItemId == 0
				        || source.state.stableItemId == command.stableItemId)
				    && distance <= 1
				    && command.targetX >= 0 && command.targetX < 40
				    && command.targetY >= 0 && command.targetY < 40
				    && !targetOccupied) {
					uint32_t entityId = 0x40000000U;
					while (entityId == state.entityId
					    || entityId == state.secondaryPlayerEntityId
					    || entityId == state.combatTargetEntityId
					    || entityId == state.objectEntityId)
						++entityId;
					state.worldItemEntityId = entityId;
					state.worldItemLevelId = state.levelId;
					state.worldItemPositionX = command.targetX;
					state.worldItemPositionY = command.targetY;
					state.worldItemSeed = source.itemSeed;
					state.worldItemPrice = source.price;
					state.worldItemState = source.state;
					if (command.itemLocation == "Inventory") {
						RemoveReplayInventoryItem(state, command.storeSlot);
					} else if (command.itemLocation == "Belt") {
						state.belt.erase(std::find_if(state.belt.begin(), state.belt.end(), [&](const auto &candidate) {
							return candidate.slot == command.storeSlot;
						}));
					} else {
						state.equipment.erase(std::find_if(state.equipment.begin(), state.equipment.end(), [&](const auto &candidate) {
							return candidate.slot == command.storeSlot;
						}));
					}
					accepted = true;
				} else if (!sourceFound
				    || state.heldItemDefined
				    || (command.stableItemId != 0 && source.state.stableItemId != command.stableItemId)) {
					rejectReason = "InvalidTarget";
				} else {
					rejectReason = "NotAllowed";
				}
			} else if (command.kind == "OperateObject") {
				const int distance = std::max(std::abs(state.positionX - state.objectPositionX), std::abs(state.positionY - state.objectPositionY));
				if (command.objectEntityId == state.objectEntityId
				    && state.objectId != 0
				    && (state.objectQuestId != 0 || state.objectEffectKind != 0 || state.objectTrapArmed)
				    && !state.objectActivated
				    && !state.objectDestroyed
				    && state.levelId == state.objectLevelId
				    && distance <= 1) {
					state.objectActivated = true;
					if (state.objectQuestId != 0
					    && state.objectQuestId == state.questId
					    && !state.questUnavailable
					    && !state.questCompleted
					    && state.levelId == state.questLevelId) {
						state.questLogged = true;
						state.questProgress = std::min(
						    state.questProgress + state.objectQuestProgressAmount,
						    state.questRequiredProgress);
						state.questCompleted = state.questProgress >= state.questRequiredProgress;
					}
					accepted = true;
				}
			} else if (command.kind == "AdvanceQuest") {
				if (command.questId == state.questId
				    && state.questId != 0
				    && !state.questUnavailable
				    && !state.questCompleted
				    && state.levelId == state.questLevelId) {
					state.questProgress = std::min(state.questProgress + 1U, state.questRequiredProgress);
					state.questCompleted = state.questProgress >= state.questRequiredProgress;
					state.questLogged = true;
					accepted = true;
				}
			} else {
				error = "Unsupported replay command kind: " + command.kind;
				return false;
			}
			if ((command.player.empty() || command.player == fixture.initialState.player)
			    && accepted && command.kind == "Move") {
				for (uint32_t direction = 0; direction < 8; ++direction) {
					if (DirectionDisplacements[direction]
					    == std::pair<int32_t, int32_t> { command.directionX, command.directionY }) {
						state.facingDirection = direction;
						break;
					}
				}
			} else if (accepted
			    && command.kind == "Attack"
			    && (command.player.empty() || command.player == fixture.initialState.player)) {
				state.facingDirection = static_cast<uint32_t>(GetDirection(
				    state.positionX,
				    state.positionY,
				    state.combatTargetPositionX,
				    state.combatTargetPositionY));
			} else if (accepted && command.kind == "Cast" && state.spellProjectileBehavior != 42) {
				int32_t targetX = command.targetX;
				int32_t targetY = command.targetY;
				if (state.spellProjectileBehavior == 5
				    || state.spellProjectileBehavior == 15) {
					targetX = state.positionX;
					targetY = state.positionY;
				} else if (command.targetEntityId != 0
				    && command.targetEntityId == state.combatTargetEntityId) {
					targetX = state.combatTargetPositionX;
					targetY = state.combatTargetPositionY;
				} else if (command.targetEntityId != 0
				    && command.targetEntityId == state.secondaryPlayerEntityId) {
					targetX = state.secondaryPlayerPositionX;
					targetY = state.secondaryPlayerPositionY;
				}
				if (targetX != state.positionX || targetY != state.positionY) {
					state.facingDirection = static_cast<uint32_t>(GetDirection(
					    state.positionX,
					    state.positionY,
					    targetX,
					    targetY));
				}
			}

			const std::string hash = ComputeReplayFixtureStateHash(state);
			if ((!command.expectedStatus.empty() && command.expectedStatus == "Accepted" && !accepted)
			    || (!command.expectedStatus.empty() && command.expectedStatus == "Rejected" && accepted)) {
				error = "Replay command status mismatch at tick " + std::to_string(command.order.targetTick)
				    + ": expected " + command.expectedStatus + ", actual " + (accepted ? "Accepted" : "Rejected");
				return false;
			}
			if (!command.expectedRejectReason.empty()
			    && command.expectedRejectReason != rejectReason) {
				error = "Replay command reject reason mismatch at tick " + std::to_string(command.order.targetTick)
				    + ": expected " + command.expectedRejectReason + ", actual "
				    + (rejectReason.empty() ? "Unspecified" : rejectReason);
				return false;
			}
			result.transitions.push_back({ command.order.targetTick, command.kind, accepted, hash });
		}
		if (!fixture.contentManifest.id.empty()) {
			auto checkpoint = std::find_if(fixture.checkpoints.begin(), fixture.checkpoints.end(), [&](const auto &candidate) {
				return candidate.tick == timelineTick;
			});
			if (checkpoint == fixture.checkpoints.end())
				continue;
			const std::string checkpointHash = ComputeReplayFixtureStateHash(state);
			if (IsSha256(checkpoint->stateSha256) && checkpoint->stateSha256 != checkpointHash) {
				error = "Replay checkpoint hash mismatch at tick " + std::to_string(checkpoint->tick) + ": expected " + checkpoint->stateSha256 + ", actual " + checkpointHash;
				return false;
			}
		}
	}
	return true;
}

} // namespace devilution

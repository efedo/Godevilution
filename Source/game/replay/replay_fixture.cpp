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
#include <utility>

#include "engine/crawl.hpp"

namespace devilution {
namespace {

constexpr uint32_t FastAttackFlag = 1U << 18;
constexpr uint32_t FasterAttackFlag = 1U << 19;
constexpr uint32_t FastestAttackFlag = 1U << 20;
constexpr uint32_t QuickAttackFlag = 1U << 17;
constexpr uint32_t FireDamageFlag = 1U << 4;
constexpr uint32_t LightningDamageFlag = 1U << 5;
constexpr uint32_t FireArrowsFlag = 1U << 3;
constexpr uint32_t LightningArrowsFlag = 1U << 25;
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

void StartReplaySecondaryPlayerDeath(ReplayFixtureExecutionState &state);

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
	if (state.secondaryPlayerCombatMode != 0 || state.secondaryPlayerCombatModeEndTick != 0) {
		hasher.AppendString("godevilution-player-combat-mode-v1");
		hasher.AppendInt32(static_cast<int32_t>(state.secondaryPlayerCombatMode));
		hasher.AppendUint64(state.secondaryPlayerCombatModeEndTick);
	}
	hasher.AppendUint64(0);
	hasher.AppendUint32(0);
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
	if (state.secondaryPlayerBlockCapable) {
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
	hasher.AppendUint64(0);
	hasher.AppendUint64(40);
	for (size_t cell = 0; cell < 40; ++cell)
		hasher.AppendInt32(-1);
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

	if (state.combatTargetEntityId != 0) {
		hasher.AppendUint64(1);
		hasher.AppendUint32(state.combatTargetEntityId);
		hasher.AppendUint32(0);
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
		if (state.combatTargetStoneCurseImmune) {
			hasher.AppendString("godevilution-monster-stone-curse-immunity-v1");
			hasher.AppendBool(true);
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

void AdvanceFireboltsOneTick(ReplayFixtureExecutionState &state)
{
	for (auto iterator = state.projectiles.begin(); iterator != state.projectiles.end();) {
		auto &projectile = *iterator;
		if (projectile.behavior != 2) {
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
			if (targetAtTile && state.combatTargetFireResistance < 100) {
				const int32_t hitChance = std::clamp(
				    state.attributes[3]
				        + GetReplayBaseMagicToHit(state.characterClassId)
				        - state.combatTargetMonsterLevel * 2,
				    5,
				    95);
				const int32_t hitRoll = std::max(GenerateRnd(state, 100), 0);
				if (state.combatTargetPetrified || hitRoll < hitChance) {
					int32_t damageQ6 = projectile.damage * 64;
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
				targetAtTile ? state.combatTargetEntityId : 0,
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

void AdvanceFireballsOneTick(ReplayFixtureExecutionState &state)
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
			    && state.combatTargetHitPointsQ6 > 0
			    && ((state.combatTargetPositionX == x
			            && state.combatTargetPositionY == y)
			        || (state.combatTargetMoving
			            && state.combatTargetPetrified
			            && state.combatTargetFuturePositionX == x
			            && state.combatTargetFuturePositionY == y));
			if (!targetAtTile || state.combatTargetFireResistance >= 100)
				return false;
			const int32_t hitChance = std::clamp(
			    state.attributes[3]
			        + GetReplayBaseMagicToHit(state.characterClassId)
			        - state.combatTargetMonsterLevel * 2,
			    5,
			    95);
			const int32_t hitRoll = std::max(GenerateRnd(state, 100), 0);
			if (!state.combatTargetPetrified && hitRoll >= hitChance)
				return false;
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
			return true;
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
				targetAtTile ? state.combatTargetEntityId : 0,
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
			const auto collision = resolveCollision(
			    projectile.positionX,
			    projectile.positionY);
			if (projectile.lastCollisionTargetEntityId != collision.second
			    && collision.first) {
				impacted = true;
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
		if (state.projectiles[index].behavior == 3) {
			auto &controller = state.projectiles[index];
			--controller.remainingTicks;
			const int32_t characterLevelDamage = GenerateRnd(
			    state,
			    std::max<int32_t>(state.characterLevel, 1));
			const int32_t randomDamage = GenerateRnd(state, 2);
			const int32_t damageQ6 = (randomDamage
			                             + characterLevelDamage
			                             + 2)
			    * 64;
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
				created.damage = damageQ6;
				created.damageType = 2;
				created.remainingTicks = static_cast<uint32_t>(
				    6 + controller.effectiveSpellLevel / 2);
				created.behavior = 4;
				created.startX = controller.startX;
				created.startY = controller.startY;
				created.resistantDamageDivisor = controller.resistantDamageDivisor;
				created.effectiveSpellLevel = controller.effectiveSpellLevel;
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

		if (state.projectiles[index].behavior != 4) {
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
			if (targetAtTile && state.combatTargetLightningResistance < 100) {
				const int32_t hitChance = std::clamp(
				    state.attributes[3]
				        + GetReplayBaseMagicToHit(state.characterClassId)
				        - state.combatTargetMonsterLevel * 2,
				    5,
				    95);
				const int32_t hitRoll = std::max(GenerateRnd(state, 100), 0);
				if (state.combatTargetPetrified || hitRoll < hitChance) {
					int32_t damage = segment.damage;
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
					const int32_t hitChance = std::clamp(
					    state.attributes[3]
					        + GetReplayBaseMagicToHit(state.characterClassId)
					        - static_cast<int32_t>(state.secondaryPlayerCharacterLevel) * 2,
					    5,
					    95);
					if (std::max(GenerateRnd(state, 100), 0) < hitChance) {
						static_cast<void>(GenerateRnd(state, 1));
						const int32_t damage = segment.damage / 2;
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

bool ApplyReplayAttack(
    ReplayFixtureExecutionState &state,
    uint32_t targetEntityId,
    uint64_t,
    bool ranged = false,
    bool consumeRandom = false)
{
	if (!IsReplayAttackTargetValid(state, targetEntityId, ranged))
		return false;
	// Native attacks consume the deterministic damage-range draw even for the
	// compact unarmed fallback used by replay fixtures.
	if (consumeRandom)
		static_cast<void>(GenerateRnd(state, 1));
	const int32_t damage = ranged
	    ? 1
	    : std::max(1, 10 - state.combatTargetArmorClass);
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
    const ReplayFixtureExecutionState &state)
{
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

void StartReplaySecondaryPlayerDeath(ReplayFixtureExecutionState &state)
{
	DropReplaySecondaryPvpEar(state);
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

void RemoveReplayInventoryItem(ReplayFixtureExecutionState &state, size_t index)
{
	state.inventory.erase(state.inventory.begin() + static_cast<std::ptrdiff_t>(index));
	for (int32_t &cell : state.inventoryGrid) {
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

int FindReplayInventoryPlacement(const ReplayFixtureExecutionState &state, const ReplayFixtureItemState &item)
{
	const uint32_t width = std::max(item.inventoryWidth, 1U);
	const uint32_t height = std::max(item.inventoryHeight, 1U);
	if (height == 1) {
		for (int cell = 30; cell <= 39 && static_cast<size_t>(cell) < state.inventoryGrid.size(); ++cell) {
			if (CanPlaceReplayInventoryItem(state.inventoryGrid, cell, width, height))
				return cell;
		}
		for (int x = 9; x >= 0; --x) {
			for (int y = 2; y >= 0; --y) {
				const int cell = y * 10 + x;
				if (CanPlaceReplayInventoryItem(state.inventoryGrid, cell, width, height))
					return cell;
			}
		}
		return -1;
	}
	if (height == 2) {
		for (int x = 10 - static_cast<int>(width); x >= 0; --x) {
			for (int y = 0; y < 3; ++y) {
				const int cell = y * 10 + x;
				if (CanPlaceReplayInventoryItem(state.inventoryGrid, cell, width, height))
					return cell;
			}
		}
		return -1;
	}
	if (width == 1 && height == 3) {
		for (int cell = 0; cell < 20; ++cell) {
			if (CanPlaceReplayInventoryItem(state.inventoryGrid, cell, width, height))
				return cell;
		}
		return -1;
	}
	if (width == 2 && height == 3) {
		for (int cell = 0; cell < 9; ++cell) {
			if (CanPlaceReplayInventoryItem(state.inventoryGrid, cell, width, height))
				return cell;
		}
		for (int cell = 10; cell < 19; ++cell) {
			if (CanPlaceReplayInventoryItem(state.inventoryGrid, cell, width, height))
				return cell;
		}
	}
	return -1;
}

void FillReplayInventoryGrid(ReplayFixtureExecutionState &state, int cell, size_t itemIndex, const ReplayFixtureItemState &item)
{
	const uint32_t width = std::max(item.inventoryWidth, 1U);
	const uint32_t height = std::max(item.inventoryHeight, 1U);
	const int column = cell % 10;
	const int row = cell / 10;
	for (uint32_t y = 0; y < height; ++y) {
		for (uint32_t x = 0; x < width; ++x) {
			state.inventoryGrid[(row + static_cast<int>(y)) * 10 + column + static_cast<int>(x)] = static_cast<int32_t>(itemIndex);
		}
	}
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
	if (state.life <= 0
	    || state.combatMode == 10
	    || state.combatMode == 11
	    || state.levelId != state.combatTargetLevelId
	    || state.combatTargetHitPointsQ6 <= 0
	    || state.combatTargetAggroRange <= 0
	    || state.combatTargetPetrified) {
		return;
	}
	const int distance = std::max(
	    std::abs(state.positionX - state.combatTargetPositionX),
	    std::abs(state.positionY - state.combatTargetPositionY));
	if (distance > state.combatTargetAggroRange)
		return;
	if (distance > 1) {
		const int32_t differenceX = state.positionX - state.combatTargetPositionX;
		const int32_t differenceY = state.positionY - state.combatTargetPositionY;
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
			        && state.secondaryPlayerLife > 0
			        && candidate.first == state.secondaryPlayerPositionX
			        && candidate.second == state.secondaryPlayerPositionY)) {
				continue;
			}
			state.combatTargetMoving = true;
			state.combatTargetFuturePositionX = candidate.first;
			state.combatTargetFuturePositionY = candidate.second;
			break;
		}
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
	const int32_t damage = state.combatTargetAttackDamage == 0
	    ? 0
	    : std::max(1, state.combatTargetAttackDamage);
	int32_t lifeDamage = damage;
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

void AdvanceReplayStateToTick(
    ReplayFixtureExecutionState &state,
    const ReplayFixture &fixture,
    uint64_t tick)
{
	for (uint64_t simulationTick = state.lastAppliedTick + 1; simulationTick <= tick; ++simulationTick) {
		AdvanceReplayPlayerAction(state, simulationTick);
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
			if (effect.remainingTicks > 0)
				--effect.remainingTicks;
		}
		state.statusEffects.erase(std::remove_if(state.statusEffects.begin(), state.statusEffects.end(), [](const auto &effect) {
			return effect.remainingTicks == 0;
		}),
		    state.statusEffects.end());
		AdvanceReplayMonsterRegeneration(state);
		AdvanceChargedBoltsOneTick(state, simulationTick);
		AdvanceFireboltsOneTick(state);
		AdvanceFireballsOneTick(state);
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

	bool ParseInitialState()
	{
		return ParseObject([this](std::string_view key) {
			if (key == "player")
				return ParseString(fixture_.initialState.player);
			if (key == "character_name")
				return ParseString(fixture_.initialState.characterName);
			if (key == "mana_shield_active")
				return ParseBool(fixture_.initialState.manaShieldActive);
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
			if (key == "facing_direction")
				return ParseUnsigned(fixture_.initialState.facingDirection);
			if (key == "suppress_multiplayer_death_drops")
				return ParseBool(fixture_.initialState.suppressMultiplayerDeathDrops);
			if (key == "gold_stack_seed")
				return ParseUnsigned(fixture_.initialState.goldStackSeed);
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
			if (key == "object_position_x")
				return ParseSigned(fixture_.initialState.objectPositionX);
			if (key == "object_position_y")
				return ParseSigned(fixture_.initialState.objectPositionY);
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
			if (key == "combat_target_fire_resistance")
				return ParseSigned(fixture_.initialState.combatTargetFireResistance);
			if (key == "combat_target_lightning_resistance")
				return ParseSigned(fixture_.initialState.combatTargetLightningResistance);
			if (key == "combat_target_magic_resistance")
				return ParseSigned(fixture_.initialState.combatTargetMagicResistance);
			if (key == "combat_target_monster_level")
				return ParseSigned(fixture_.initialState.combatTargetMonsterLevel);
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
			if (spell.spellId == 0 || spell.spellId > 51 || spell.level == 0 || spell.level > 15)
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

	bool ParseEquipment()
	{
		if (!Consume('['))
			return Fail("expected equipment array");
		fixture_.initialState.equipment.clear();
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
			    || std::any_of(fixture_.initialState.equipment.begin(), fixture_.initialState.equipment.end(), [&](const auto &candidate) {
				    return candidate.slot == item.slot;
			    })) {
				return Fail("invalid or duplicate equipment item");
			}
			fixture_.initialState.equipment.push_back(item);
			SkipWhitespace();
			if (Consume(']'))
				return true;
			if (!Consume(','))
				return Fail("expected comma or closing bracket");
			SkipWhitespace();
		}
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
			    || item.spellId > 51
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
	            || fixture.initialState.objectDestroyed))) {
		error = "Replay fixture contains invalid object missile-collision state.";
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
	if (target.combatTargetMaxHitPoints == 0 && target.combatTargetHitPoints > 0)
		target.combatTargetMaxHitPoints = target.combatTargetHitPoints;
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
	for (const auto &command : fixture.commands) {
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
	state.hellfire = fixture.gameVariant == "Hellfire";
	state.multiplayer = fixture.gameMode == "Multiplayer";
	state.suppressMultiplayerDeathDrops = fixture.initialState.suppressMultiplayerDeathDrops;
	state.facingDirection = fixture.initialState.facingDirection;
	state.characterName = fixture.initialState.characterName;
	state.manaShieldActive = fixture.initialState.manaShieldActive;
	if (state.equipment.empty() && !fixture.initialState.equipment.empty()) {
		for (const auto &source : fixture.initialState.equipment) {
			ReplayFixtureItemState item;
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
		state.secondaryPlayerPositionX = secondary.positionX;
		state.secondaryPlayerPositionY = secondary.positionY;
		state.secondaryPlayerLevelId = secondary.levelId;
		state.secondaryPlayerCharacterLevel = secondary.characterLevel;
		state.secondaryPlayerCharacterClassId = secondary.characterClass;
		state.secondaryPlayerMagic = secondary.magic;
		state.secondaryPlayerDexterity = secondary.dexterity;
		state.secondaryPlayerFriendlyMode = secondary.friendlyMode;
		state.secondaryPlayerBlockCapable = secondary.blockCapable;
		state.secondaryPlayerCombatMode = secondary.combatMode;
		state.secondaryPlayerCombatModeEndTick = secondary.combatModeEndTick;
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
		state.combatTargetFireResistance = fixture.initialState.combatTargetFireResistance;
		state.combatTargetLightningResistance = fixture.initialState.combatTargetLightningResistance;
		state.combatTargetMagicResistance = fixture.initialState.combatTargetMagicResistance;
		state.combatTargetMonsterLevel = fixture.initialState.combatTargetMonsterLevel;
		state.combatTargetMoving = fixture.initialState.combatTargetMoving;
		state.combatTargetFuturePositionX = fixture.initialState.combatTargetFuturePositionX;
		state.combatTargetFuturePositionY = fixture.initialState.combatTargetFuturePositionY;
		state.combatTargetPetrified = fixture.initialState.combatTargetPetrified;
		state.combatTargetStoneCurseImmune = fixture.initialState.combatTargetStoneCurseImmune;
	}
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
			if (!command.player.empty() && command.player == state.secondaryPlayer) {
				if (command.kind != "Move") {
					error = "Secondary replay participant currently supports Move commands only.";
					return false;
				}
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
					state.secondaryPlayerPositionX = targetX;
					state.secondaryPlayerPositionY = targetY;
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
			} else if (command.kind == "UsePortal") {
				if (command.portalId == state.portalId) {
					if (!fixture.playerActionTiming.defined
					    || (state.combatMode == 0 && !state.currentAction.defined)) {
						state.levelId = state.portalDestinationLevelId;
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
				if (!state.heldItemDefined
				    && command.worldItemEntityId == state.worldItemEntityId
				    && state.worldItemSeed != 0
				    && state.levelId == state.worldItemLevelId
				    && distance <= 1) {
					const bool isGold = state.worldItemState.itemType == 11
					    && state.worldItemState.itemIndex == 0;
					if (isGold) {
						if (state.worldItemState.value > 0
						    && state.worldItemState.value <= 5000
						    && state.worldItemState.identifiedValue == state.worldItemState.value
						    && state.worldItemState.statFlag
						    && state.worldItemPrice == static_cast<uint32_t>(state.worldItemState.value)
						    && std::numeric_limits<uint32_t>::max() - state.gold >= state.worldItemPrice) {
							if (HasReplayGoldTopology(state)) {
								const uint32_t remainder = AddReplayGoldPartial(state, state.worldItemPrice);
								if (remainder != 0) {
									state.heldItemDefined = true;
									state.heldItem = {
									    0,
									    0,
									    state.worldItemSeed,
									    remainder,
									    command.order.targetTick,
									    state.worldItemState,
									};
									state.heldItem.state.value = static_cast<int32_t>(remainder);
									state.heldItem.state.identifiedValue = static_cast<int32_t>(remainder);
								}
							} else {
								state.gold += state.worldItemPrice;
							}
							state.worldItemEntityId = 0;
							accepted = true;
						}
					} else {
						state.inventory.push_back({ 0, 0, state.worldItemSeed, state.worldItemPrice, command.order.targetTick, state.worldItemState });
						if (!state.inventoryGrid.empty())
							state.inventoryGrid[0] = 0;
						state.worldItemEntityId = 0;
						accepted = true;
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
			} else if (command.kind == "OperateObject") {
				const int distance = std::max(std::abs(state.positionX - state.objectPositionX), std::abs(state.positionY - state.objectPositionY));
				if (command.objectEntityId == state.objectEntityId
				    && state.objectId != 0
				    && !state.objectActivated
				    && !state.objectDestroyed
				    && state.levelId == state.objectLevelId
				    && distance <= 1) {
					state.objectActivated = true;
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
			} else if (accepted && command.kind == "Attack") {
				state.facingDirection = static_cast<uint32_t>(GetDirection(
				    state.positionX,
				    state.positionY,
				    state.combatTargetPositionX,
				    state.combatTargetPositionY));
			} else if (accepted && command.kind == "Cast") {
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

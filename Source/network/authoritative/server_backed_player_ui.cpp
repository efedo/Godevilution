#include "network/authoritative/server_backed_player_ui.hpp"

#include <algorithm>
#include <climits>
#include <cstddef>
#include <cstdint>
#include <set>

#include "engine/cursor.h"
#include "game/spells/spells.hpp"
#include "ui/automap.h"
#include "utils/string/utf8.hpp"

namespace devilution::authoritative {
namespace {

tl::expected<void, std::string> ValidateSnapshot(const ProjectedPlayerSnapshot &snapshot)
{
	if (snapshot.entityId == 0)
		return tl::make_unexpected("Server-backed player snapshot has an invalid entity ID.");
	if (snapshot.levelId > UINT8_MAX)
		return tl::make_unexpected("Server-backed player level is outside the native range.");
	if (snapshot.positionX < 0 || snapshot.positionX > UINT8_MAX || snapshot.positionY < 0 || snapshot.positionY > UINT8_MAX)
		return tl::make_unexpected("Server-backed player position is outside the native tile range.");
	if (snapshot.life < 0 || snapshot.lifeMaximum < 0 || snapshot.lifeMaximumBase < 0 || snapshot.mana < 0 || snapshot.manaMaximum < 0 || snapshot.manaMaximumBase < 0 || snapshot.characterLevel == 0 || snapshot.gold > static_cast<uint32_t>(INT_MAX) || snapshot.statPoints > static_cast<uint32_t>(INT_MAX))
		return tl::make_unexpected("Server-backed player resources are outside the native range.");
	if (snapshot.characterClassId > static_cast<uint32_t>(HeroClass::LAST))
		return tl::make_unexpected("Server-backed player class is outside the native range.");
	if (snapshot.characterName.size() >= PlayerNameLength
	    || snapshot.characterName.find('\0') != std::string::npos)
		return tl::make_unexpected("Server-backed player name is outside the native range.");
	if (snapshot.facingDirection > static_cast<uint32_t>(Direction::SouthEast))
		return tl::make_unexpected("Server-backed player facing direction is outside the native range.");
	if (snapshot.combatMode > static_cast<uint32_t>(PM_QUIT)
	    || (snapshot.combatMode == static_cast<uint32_t>(PM_STAND)
	        && snapshot.combatModeEndTick != 0)
	    || ((snapshot.combatMode == static_cast<uint32_t>(PM_BLOCK)
	            || snapshot.combatMode == static_cast<uint32_t>(PM_GOTHIT))
	        && snapshot.combatModeEndTick == 0)) {
		return tl::make_unexpected("Server-backed player combat mode is invalid.");
	}
	if (snapshot.currentAction.has_value()) {
		const auto &action = *snapshot.currentAction;
		const bool timingIsValid = action.kind >= 1 && action.kind <= 6
		    && action.effectTick >= action.startedAtTick
		    && action.readyTick >= action.effectTick
		    && action.readyTick <= action.endTick
		    && action.endTick == snapshot.combatModeEndTick;
		const bool moveIsValid = action.kind == 1
		    && (snapshot.combatMode == static_cast<uint32_t>(PM_WALK_NORTHWARDS)
		        || snapshot.combatMode == static_cast<uint32_t>(PM_WALK_SOUTHWARDS)
		        || snapshot.combatMode == static_cast<uint32_t>(PM_WALK_SIDEWAYS))
		    && action.directionX >= -1 && action.directionX <= 1
		    && action.directionY >= -1 && action.directionY <= 1
		    && (action.directionX != 0 || action.directionY != 0)
		    && action.targetEntityId == 0 && !action.hasCast;
		const bool attackIsValid = action.kind == 2
		    && (snapshot.combatMode == static_cast<uint32_t>(PM_ATTACK)
		        || snapshot.combatMode == static_cast<uint32_t>(PM_RATTACK))
		    && action.directionX == 0 && action.directionY == 0
		    && action.targetEntityId != 0 && !action.hasCast;
		const bool castIsValid = action.kind == 3
		    && snapshot.combatMode == static_cast<uint32_t>(PM_SPELL)
		    && action.directionX == 0 && action.directionY == 0
		    && action.hasCast && action.spellId != 0
		    && action.targetEntityId == action.castTargetEntityId
		    && action.castSource <= 4
		    && (!action.hasCastSourceItem || action.castSourceItemLocation <= 2);
		const bool deathIsValid = action.kind == 4
		    && snapshot.combatMode == static_cast<uint32_t>(PM_DEATH)
		    && snapshot.life == 0 && action.effectApplied
		    && action.effectTick == action.startedAtTick
		    && action.readyTick == action.endTick
		    && action.directionX == 0 && action.directionY == 0
		    && !action.hasCast;
		const bool transitionIsValid = action.kind == 5
		    && snapshot.combatMode == static_cast<uint32_t>(PM_NEWLVL)
		    && snapshot.life > 0 && action.effectApplied
		    && action.effectTick == action.startedAtTick
		    && action.readyTick == action.endTick
		    && action.directionX == 0 && action.directionY == 0
		    && action.targetEntityId != 0 && !action.hasCast;
		const bool restartTownIsValid = action.kind == 6
		    && snapshot.combatMode == static_cast<uint32_t>(PM_NEWLVL)
		    && snapshot.life > 0 && action.effectApplied
		    && action.effectTick == action.startedAtTick
		    && action.readyTick == action.endTick
		    && action.directionX == 0 && action.directionY == 0
		    && action.targetEntityId == 0 && !action.hasCast;
		if (!timingIsValid
		    || (!moveIsValid && !attackIsValid && !castIsValid
		        && !deathIsValid && !transitionIsValid && !restartTownIsValid)) {
			return tl::make_unexpected("Server-backed player action is invalid.");
		}
	}
	if (snapshot.abilitySpellId > static_cast<uint32_t>(SpellID::LAST))
		return tl::make_unexpected("Server-backed player ability is outside the native spell range.");
	std::set<uint32_t> memorizedSpellIds;
	for (const auto &spell : snapshot.memorizedSpells) {
		if (spell.spellId == 0 || spell.spellId > static_cast<uint32_t>(SpellID::LAST)
		    || spell.level == 0 || spell.level > MaxSpellLevel
		    || !memorizedSpellIds.insert(spell.spellId).second) {
			return tl::make_unexpected("Server-backed memorized spells contain invalid or duplicate state.");
		}
	}
	std::set<uint32_t> statusEffectIds;
	for (const auto &effect : snapshot.statusEffects) {
		if (effect.effectId == 0 || effect.remainingTicks == 0 || effect.behavior > 2
		    || ((effect.behavior == 1 || effect.behavior == 2) && effect.magnitude != 0)
		    || !statusEffectIds.insert(effect.effectId).second) {
			return tl::make_unexpected("Server-backed status effects contain invalid or duplicate state.");
		}
	}
	if (snapshot.belt.size() > MaxBeltItems)
		return tl::make_unexpected("Server-backed belt exceeds the native belt capacity.");
	for (const auto &beltItem : snapshot.belt) {
		if (beltItem.slot >= MaxBeltItems)
			return tl::make_unexpected("Server-backed belt contains an invalid slot.");
	}
	if (snapshot.inventory.size() > InventoryGridCells || snapshot.inventoryGrid.size() != InventoryGridCells)
		return tl::make_unexpected("Server-backed inventory does not fit the native inventory layout.");
	if (snapshot.heldItem.has_value()
	    && ((snapshot.heldItem->itemSeed == 0
	            && snapshot.heldItem->item._iMiscId != IMISC_EAR)
	        || snapshot.heldItem->price > static_cast<uint32_t>(INT_MAX)
	        || snapshot.heldItem->item.isEmpty())) {
		return tl::make_unexpected("Server-backed held item is invalid.");
	}

	std::set<uint32_t> equipmentSlots;
	for (const auto &equipment : snapshot.equipment) {
		if (equipment.slot >= NUM_INVLOC || !equipmentSlots.insert(equipment.slot).second)
			return tl::make_unexpected("Server-backed equipment contains a duplicate or invalid slot.");
	}
	for (const int32_t cell : snapshot.inventoryGrid) {
		if (cell < -1 || (cell >= 0 && static_cast<std::size_t>(cell) >= snapshot.inventory.size()))
			return tl::make_unexpected("Server-backed inventory grid references an invalid item.");
	}
	return { };
}

void ApplyResource(VitalResource &destination, int32_t current)
{
	destination.current = current;
	destination.base = current + destination.maximumBase - destination.maximum;
	destination.current = std::clamp(destination.current, 0, destination.maximum);
	destination.base = std::clamp(destination.base, 0, destination.maximumBase);
}

} // namespace

tl::expected<void, std::string> ApplyServerBackedPlayerSnapshot(Player &player, const ProjectedPlayerSnapshot &snapshot)
{
	if (auto result = ValidateSnapshot(snapshot); !result.has_value())
		return result;

	player.position.tile = { static_cast<WorldTileCoord>(snapshot.positionX), static_cast<WorldTileCoord>(snapshot.positionY) };
	player.position.future = player.position.tile;
	if (snapshot.currentAction.has_value() && snapshot.currentAction->kind == 1) {
		player.position.future += Displacement {
			static_cast<int8_t>(snapshot.currentAction->directionX),
			static_cast<int8_t>(snapshot.currentAction->directionY),
		};
	}
	player.position.last = player.position.tile;
	player.position.old = player.position.tile;
	player.position.temp = player.position.tile;
	player.setLevel(static_cast<uint8_t>(snapshot.levelId));
	player._pGold = static_cast<int>(snapshot.gold);
	player._pExperience = snapshot.experience;
	player._pStatPts = static_cast<int>(snapshot.statPoints);
	player.pManaShield = snapshot.manaShieldActive;
	player._pInfraFlag = std::any_of(snapshot.statusEffects.begin(), snapshot.statusEffects.end(), [](const ProjectedStatusEffect &effect) {
		return effect.behavior == 1;
	});
	CurrentAutomapManager.SetAutoMapShowItems(std::any_of(snapshot.statusEffects.begin(), snapshot.statusEffects.end(), [](const ProjectedStatusEffect &effect) {
		return effect.behavior == 2;
	}));
	player.friendlyMode = snapshot.friendlyMode;
	player.direction = static_cast<Direction>(snapshot.facingDirection);
	player._pmode = static_cast<PLR_MODE>(snapshot.combatMode);
	player._pClass = static_cast<HeroClass>(snapshot.characterClassId);
	if (!snapshot.characterName.empty())
		CopyUtf8(player._pName, snapshot.characterName, PlayerNameLength);
	player._pAblSpells = snapshot.abilitySpellId == 0
	    ? 0
	    : GetSpellBitmask(static_cast<SpellID>(snapshot.abilitySpellId));
	player._pMemSpells = 0;
	for (uint8_t &spellLevel : player._pSplLvl)
		spellLevel = 0;
	for (const auto &spell : snapshot.memorizedSpells) {
		const auto spellId = static_cast<SpellID>(spell.spellId);
		player._pMemSpells |= GetSpellBitmask(spellId);
		player._pSplLvl[spell.spellId] = static_cast<uint8_t>(spell.level);
	}
	if (!snapshot.memorizedSpells.empty()) {
		player._pRSpell = static_cast<SpellID>(snapshot.memorizedSpells.front().spellId);
		player._pRSplType = SpellType::Spell;
	} else if (snapshot.abilitySpellId != 0) {
		player._pRSpell = static_cast<SpellID>(snapshot.abilitySpellId);
		player._pRSplType = SpellType::Skill;
	} else {
		player._pRSpell = SpellID::Invalid;
		player._pRSplType = SpellType::Invalid;
	}
	player.attributes.strength = { snapshot.strength.base, snapshot.strength.current };
	player.attributes.magic = { snapshot.magic.base, snapshot.magic.current };
	player.attributes.dexterity = { snapshot.dexterity.base, snapshot.dexterity.current };
	player.attributes.vitality = { snapshot.vitality.base, snapshot.vitality.current };
	if (snapshot.lifeMaximum > 0) {
		player.life.maximum = snapshot.lifeMaximum;
		player.life.maximumBase = snapshot.lifeMaximumBase > 0
		    ? snapshot.lifeMaximumBase
		    : snapshot.lifeMaximum;
	}
	if (snapshot.manaMaximum > 0) {
		player.mana.maximum = snapshot.manaMaximum;
		player.mana.maximumBase = snapshot.manaMaximumBase > 0
		    ? snapshot.manaMaximumBase
		    : snapshot.manaMaximum;
	}
	ApplyResource(player.life, snapshot.life);
	ApplyResource(player.mana, snapshot.mana);
	if (const uint8_t maximumLevel = player.getMaxCharacterLevel(); maximumLevel > 0)
		player.setCharacterLevel(static_cast<uint8_t>(std::min<uint32_t>(snapshot.characterLevel, maximumLevel)));

	for (auto &item : player.InvBody)
		item.clear();
	for (auto &item : player.InvList)
		item.clear();
	for (auto &item : player.SpdList)
		item.clear();
	for (const auto &equipment : snapshot.equipment)
		player.InvBody[equipment.slot] = equipment.item;
	for (const auto &beltItem : snapshot.belt)
		player.SpdList[beltItem.slot] = beltItem.item;
	for (std::size_t index = 0; index < snapshot.inventory.size(); ++index)
		player.InvList[index] = snapshot.inventory[index].item;
	player._pNumInv = static_cast<int>(snapshot.inventory.size());
	for (std::size_t index = 0; index < snapshot.inventoryGrid.size(); ++index) {
		const int32_t cell = snapshot.inventoryGrid[index];
		player.InvGrid[index] = cell < 0 ? 0 : static_cast<int8_t>(cell + 1);
	}
	const bool hadHeldItem = !player.HoldItem.isEmpty();
	const uint32_t previousHeldSeed = player.HoldItem._iSeed;
	if (snapshot.heldItem.has_value()) {
		player.HoldItem = snapshot.heldItem->item;
		if (!hadHeldItem || previousHeldSeed != snapshot.heldItem->itemSeed)
			SyncCursorToHeldItem(player.HoldItem);
	} else {
		player.HoldItem.clear();
		if (hadHeldItem)
			SyncCursorToHeldItem(player.HoldItem);
	}
	player.UpdateHitPointPercentage();
	player.UpdateManaPercentage();
	return { };
}

} // namespace devilution::authoritative

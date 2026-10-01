#include "network/authoritative/server_backed_player_ui.hpp"

#include <algorithm>
#include <array>
#include <climits>
#include <cstddef>
#include <cstdint>
#include <iterator>
#include <optional>
#include <set>

#include "engine/cursor.h"
#include "engine/lighting.h"
#include "game/spells/spells.hpp"
#include "network/authoritative/authoritative_level_id.hpp"
#include "network/authoritative/protocol_limits.hpp"
#include "qol/stash.h"
#include "ui/automap.h"
#include "utils/string/utf8.hpp"

namespace devilution::authoritative {
namespace {

bool IsValidSpellTargetingKind(ProjectedSpellTargetingKind value)
{
	return static_cast<uint32_t>(value) <= static_cast<uint32_t>(ProjectedSpellTargetingKind::WorldEntity);
}

bool HasValidProjectedItemIdentity(uint32_t itemSeed, const Item &item)
{
	return (itemSeed != 0 || item._iMiscId == IMISC_EAR) && !item.isEmpty();
}

tl::expected<void, std::string> ValidateSnapshot(const ProjectedPlayerSnapshot &snapshot)
{
	if (snapshot.entityId == 0)
		return tl::make_unexpected("Server-backed player snapshot has an invalid entity ID.");
	if (!IsAuthoritativeSupportedLevel(snapshot.levelId))
		return tl::make_unexpected("Server-backed player level identity is unsupported.");
	if (!InDungeonBounds({ snapshot.positionX, snapshot.positionY }))
		return tl::make_unexpected("Server-backed player position is outside the native tile range.");
	if (snapshot.life < 0 || snapshot.lifeMaximum < 0 || snapshot.lifeMaximum > 0 && snapshot.life > snapshot.lifeMaximum
	    || snapshot.lifeMaximumBase < 0 || snapshot.mana < 0 || snapshot.manaMaximum < 0
	    || snapshot.manaMaximum > 0 && snapshot.mana > snapshot.manaMaximum
	    || snapshot.manaMaximumBase < 0 || snapshot.characterLevel == 0
	    || snapshot.characterLevel > MaximumNativeCharacterLevel
	    || snapshot.gold > static_cast<uint32_t>(INT_MAX) || snapshot.statPoints > static_cast<uint32_t>(INT_MAX) || snapshot.reflectCharges > UINT16_MAX)
		return tl::make_unexpected("Server-backed player resources are outside the native range.");
	if (snapshot.strength.base < 0 || snapshot.strength.current < 0
	    || snapshot.magic.base < 0 || snapshot.magic.current < 0
	    || snapshot.dexterity.base < 0 || snapshot.dexterity.current < 0
	    || snapshot.vitality.base < 0 || snapshot.vitality.current < 0)
		return tl::make_unexpected("Server-backed player attributes are outside the native range.");
	if (static_cast<uint32_t>(snapshot.abilityKind) > static_cast<uint32_t>(ProjectedPlayerAbilityKind::Rage))
		return tl::make_unexpected("Server-backed player ability kind is outside the native range.");
	if (snapshot.stashGold > static_cast<uint32_t>(INT_MAX)
	    || snapshot.stash.size() > NativeStashPageCount * NativeStashCellCount)
		return tl::make_unexpected("Server-backed stash resources are outside the native range.");
	if (snapshot.diabloKillLevel > UINT8_MAX)
		return tl::make_unexpected("Server-backed player rank is outside the native range.");
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
	    && action.directionX >= MinimumNativeDirection && action.directionX <= MaximumNativeDirection
	    && action.directionY >= MinimumNativeDirection && action.directionY <= MaximumNativeDirection
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
		if (moveIsValid && !action.effectApplied
		    && !InDungeonBounds({ snapshot.positionX + action.directionX, snapshot.positionY + action.directionY }))
			return tl::make_unexpected("Server-backed player walk destination is outside the native tile range.");
		if (!timingIsValid
		    || (!moveIsValid && !attackIsValid && !castIsValid
		        && !deathIsValid && !transitionIsValid && !restartTownIsValid)) {
			return tl::make_unexpected("Server-backed player action is invalid.");
		}
	}
	if (snapshot.abilitySpellId > static_cast<uint32_t>(SpellID::LAST))
		return tl::make_unexpected("Server-backed player ability is outside the native spell range.");
	if (!IsValidSpellTargetingKind(snapshot.abilitySpellTargetingKind))
		return tl::make_unexpected("Server-backed player ability contains an invalid targeting kind.");
	std::set<uint32_t> visitedLevelIds;
	for (const uint32_t visitedLevelId : snapshot.visitedLevelIds) {
		if (!IsAuthoritativeRegularLevel(visitedLevelId) || !visitedLevelIds.insert(visitedLevelId).second)
			return tl::make_unexpected("Server-backed visited levels contain an invalid or duplicate regular level.");
	}
	if (IsAuthoritativeRegularLevel(snapshot.levelId) && !snapshot.visitedLevelIds.empty()
	    && std::find(snapshot.visitedLevelIds.begin(), snapshot.visitedLevelIds.end(), snapshot.levelId)
	        == snapshot.visitedLevelIds.end()) {
		return tl::make_unexpected("Server-backed visited levels omit the player's current native level.");
	}
	std::set<uint32_t> memorizedSpellIds;
	for (const auto &spell : snapshot.memorizedSpells) {
		if (spell.spellId == 0 || spell.spellId > static_cast<uint32_t>(SpellID::LAST)
		    || spell.level == 0 || spell.level > MaxSpellLevel
		    || !IsValidSpellTargetingKind(spell.targetingKind)
		    || !memorizedSpellIds.insert(spell.spellId).second) {
			return tl::make_unexpected("Server-backed memorized spells contain invalid or duplicate state.");
		}
	}
	std::set<uint32_t> statusEffectIds;
	for (const auto &effect : snapshot.statusEffects) {
		if (effect.effectId == 0 || effect.remainingTicks == 0 || effect.behavior > MaximumStatusEffectBehavior
		    || ((effect.behavior == 1 || effect.behavior == 2 || effect.behavior == 3) && effect.magnitude != 0)
		    || !statusEffectIds.insert(effect.effectId).second) {
			return tl::make_unexpected("Server-backed status effects contain invalid or duplicate state.");
		}
	}
	std::set<uint32_t> playerItemSeeds;
	const auto hasUniqueItemSeed = [&playerItemSeeds](uint32_t itemSeed) {
		return itemSeed == 0 || playerItemSeeds.insert(itemSeed).second;
	};
	if (snapshot.belt.size() > MaxBeltItems)
		return tl::make_unexpected("Server-backed belt exceeds the native belt capacity.");
	for (const auto &beltItem : snapshot.belt) {
		if (beltItem.slot >= MaxBeltItems || !IsValidSpellTargetingKind(beltItem.spellTargetingKind)
		    || !hasUniqueItemSeed(beltItem.itemSeed)
		    || !HasValidProjectedItemIdentity(beltItem.itemSeed, beltItem.item))
			return tl::make_unexpected("Server-backed belt contains an invalid slot.");
	}
	for (const auto &inventoryItem : snapshot.inventory) {
		if (!IsValidSpellTargetingKind(inventoryItem.spellTargetingKind)
		    || inventoryItem.price > static_cast<uint32_t>(INT_MAX)
		    || !hasUniqueItemSeed(inventoryItem.itemSeed)
		    || !HasValidProjectedItemIdentity(inventoryItem.itemSeed, inventoryItem.item))
			return tl::make_unexpected("Server-backed inventory contains an invalid spell targeting kind.");
	}
	if (snapshot.heldItem.has_value() && !IsValidSpellTargetingKind(snapshot.heldItem->spellTargetingKind))
		return tl::make_unexpected("Server-backed held item contains an invalid spell targeting kind.");
	if (snapshot.heldItem.has_value() && !hasUniqueItemSeed(snapshot.heldItem->itemSeed))
		return tl::make_unexpected("Server-backed snapshot contains duplicate player item references.");
	if (snapshot.inventory.size() > InventoryGridCells
	    || (!snapshot.inventoryGrid.empty() && snapshot.inventoryGrid.size() != InventoryGridCells))
		return tl::make_unexpected("Server-backed inventory does not fit the native inventory layout.");
	if (snapshot.heldItem.has_value()
	    && ((snapshot.heldItem->itemSeed == 0
            && snapshot.heldItem->item._iMiscId != IMISC_EAR)
	        || !HasValidProjectedItemIdentity(snapshot.heldItem->itemSeed, snapshot.heldItem->item)
	        || snapshot.heldItem->price > static_cast<uint32_t>(INT_MAX)
	        || snapshot.heldItem->item.isEmpty())) {
		return tl::make_unexpected("Server-backed held item is invalid.");
	}

	std::set<uint32_t> equipmentSlots;
	for (const auto &equipment : snapshot.equipment) {
		if (equipment.slot >= NUM_INVLOC || !IsValidSpellTargetingKind(equipment.spellTargetingKind)
		    || !hasUniqueItemSeed(equipment.itemSeed)
		    || !HasValidProjectedItemIdentity(equipment.itemSeed, equipment.item)
		    || !equipmentSlots.insert(equipment.slot).second)
			return tl::make_unexpected("Server-backed equipment contains a duplicate or invalid slot.");
	}
	std::array<bool, NativeStashPageCount * NativeStashCellCount> occupied { };
	for (const auto &stashItem : snapshot.stash) {
		if (stashItem.page >= NativeStashPageCount || stashItem.cell >= NativeStashCellCount
		    || stashItem.width == 0 || stashItem.height == 0
		    || !IsValidSpellTargetingKind(stashItem.spellTargetingKind)
		    || stashItem.price > static_cast<uint32_t>(INT_MAX)
		    || !hasUniqueItemSeed(stashItem.itemSeed)
		    || !HasValidProjectedItemIdentity(stashItem.itemSeed, stashItem.item)
		    || stashItem.width > NativeStashGridWidth || stashItem.height > NativeStashGridHeight
		    || stashItem.cell % NativeStashGridWidth + stashItem.width > NativeStashGridWidth
		    || stashItem.cell / NativeStashGridWidth + stashItem.height > NativeStashGridHeight) {
			return tl::make_unexpected("Server-backed stash contains an invalid item footprint.");
		}
		for (uint32_t y = 0; y < stashItem.height; ++y) {
			for (uint32_t x = 0; x < stashItem.width; ++x) {
				const std::size_t index = static_cast<std::size_t>(stashItem.page) * NativeStashCellCount
				    + static_cast<std::size_t>(stashItem.cell / NativeStashGridWidth + y) * NativeStashGridWidth
				    + stashItem.cell % NativeStashGridWidth + x;
				if (occupied[index])
					return tl::make_unexpected("Server-backed stash contains overlapping items.");
				occupied[index] = true;
			}
		}
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

/**
 * Reconnects a server-backed player with the legacy dPlayer/dead-player presentation state.
 *
 * The authoritative projector is also used by tests with temporary Player objects.  Only a
 * player whose address is currently registered in Players may receive a native marker, because
 * Player::getId() is defined by that registration and is not valid for such temporary objects.
 */
std::optional<int8_t> RegisteredNativePlayerMarker(const Player &player)
{
	for (size_t playerIndex = 0; playerIndex < Players.size() && playerIndex < MAX_PLRS; ++playerIndex) {
		if (&Players[playerIndex] == &player)
			return static_cast<int8_t>(playerIndex + 1);
	}
	return std::nullopt;
}

void ClearNativePlayerMarkers(int8_t marker)
{
	for (int y = 0; y < MAXDUNY; ++y) {
		for (int x = 0; x < MAXDUNX; ++x) {
			Tile &tile = tileAt({ x, y });
			if (tile.player() == marker || tile.player() == -marker)
				tile.setPlayer(0);
		}
	}
}

bool HasOtherDeadPlayerAt(Point position, const Player &player)
{
	for (const Player &other : Players) {
		if (&other == &player || !other.plractive || !other.hasNoLife() || !other.isOnActiveLevel())
			continue;
		if (other.position.tile == position)
			return true;
	}
	return false;
}

void ClearDeadPlayerFlagIfUnoccupied(Point position, const Player &player)
{
	if (InDungeonBounds(position) && !HasOtherDeadPlayerAt(position, player))
		tileAt(position).removeFlags(DungeonFlag::DeadPlayer);
}

void SynchronizeNativePlayerPresentation(
	Player &player,
	const ProjectedPlayerSnapshot &snapshot,
	Point previousPosition,
	bool wasOnActiveLevel)
{
	const auto marker = RegisteredNativePlayerMarker(player);
	if (!marker.has_value())
		return;

	// The native level loader clears dPlayer and dead-player flags.  Reapplying a snapshot after
	// that load must therefore rebuild only this player's presentation state.  The raw scan also
	// removes a stale reservation left by a previous authoritative walk without consulting an
	// unregistered Player pointer.
	ClearNativePlayerMarkers(*marker);
	if (wasOnActiveLevel)
		ClearDeadPlayerFlagIfUnoccupied(previousPosition, player);
	if (!player.plractive)
		return;

	if (!player.isOnActiveLevel() || !InDungeonBounds(player.position.tile))
		return;

	// A level transition has no native actor until the next level is ready.  The authoritative
	// snapshot remains in Player::position so the level loader can use it on that next pass.
	if (player._pLvlChanging) {
		ClearDeadPlayerFlagIfUnoccupied(player.position.tile, player);
		return;
	}

	if (player.hasNoLife()) {
		tileAt(player.position.tile).addFlags(DungeonFlag::DeadPlayer);
		return;
	}

	const bool drawFromBehind = player._pmode == PM_WALK_SOUTHWARDS
	    || (player._pmode == PM_WALK_SIDEWAYS && player.direction == Direction::East);
	Point markerPosition = player.position.tile;
	if (snapshot.currentAction.has_value()
	    && snapshot.currentAction->kind == 1
	    && !snapshot.currentAction->effectApplied
	    && drawFromBehind) {
		// WalkInDirection reserves the destination.  The sign is part of native draw ordering:
		// southward/eastward walking is drawn from a negative destination marker.
		markerPosition = player.position.future;
	}
	if (!InDungeonBounds(markerPosition))
		return;

	const int8_t nativeMarker = drawFromBehind ? static_cast<int8_t>(-*marker) : *marker;
	Tile &tile = tileAt(markerPosition);
	// Never replace another registered player's marker.  A conflicting authoritative position
	// remains invisible until the server supplies a non-conflicting projection.
	if (tile.player() == 0 || tile.player() == *marker || tile.player() == -*marker)
		tile.setPlayer(nativeMarker);
	ClearDeadPlayerFlagIfUnoccupied(player.position.tile, player);
}

} // namespace

tl::expected<void, std::string> ApplyServerBackedStashProjection(const ProjectedPlayerSnapshot &snapshot)
{
	if (auto result = ValidateSnapshot(snapshot); !result.has_value())
		return result;

	const unsigned currentPage = Stash.GetPage();
	Stash.stashGrids.clear();
	Stash.stashList.clear();
	Stash.gold = static_cast<int>(snapshot.stashGold);
	Stash.dirty = false;
	for (const auto &stashItem : snapshot.stash) {
		Item item = stashItem.item;
		const unsigned column = stashItem.cell % NativeStashGridWidth;
		const unsigned row = stashItem.cell / NativeStashGridWidth;
		item.position = { static_cast<int>(column), static_cast<int>(row + stashItem.height - 1) };
		const auto stashIndex = static_cast<StashStruct::StashCell>(Stash.stashList.size());
		Stash.stashList.push_back(std::move(item));
		for (uint32_t y = 0; y < stashItem.height; ++y) {
			for (uint32_t x = 0; x < stashItem.width; ++x)
				Stash.stashGrids[stashItem.page][column + x][row + y] = stashIndex + 1;
		}
	}
	Stash.SetPage(currentPage);
	Stash.dirty = false;
	IsStashOpen = snapshot.stashOpen;
	if (!IsStashOpen)
		CloseGoldWithdraw();
	return { };
}

tl::expected<void, std::string> ApplyServerBackedPlayerSnapshot(Player &player, const ProjectedPlayerSnapshot &snapshot)
{
	if (auto result = ValidateSnapshot(snapshot); !result.has_value())
		return result;

	const Point previousPosition = player.position.tile;
	const bool wasOnActiveLevel = player.isOnActiveLevel();
	player.position.tile = { static_cast<WorldTileCoord>(snapshot.positionX), static_cast<WorldTileCoord>(snapshot.positionY) };
	player.position.future = player.position.tile;
	if (snapshot.currentAction.has_value()
	    && snapshot.currentAction->kind == 1
	    && !snapshot.currentAction->effectApplied) {
		player.position.future += Displacement {
			static_cast<int8_t>(snapshot.currentAction->directionX),
			static_cast<int8_t>(snapshot.currentAction->directionY),
		};
	}
	player.position.last = player.position.tile;
	player.position.old = player.position.tile;
	player.position.temp = player.position.tile;
	if (const auto setLevel = AuthoritativeNativeSetLevel(snapshot.levelId); setLevel.has_value())
		player.setLevel(*setLevel);
	else
		player.setLevel(static_cast<uint8_t>(snapshot.levelId));
	if (snapshot.lightRadius != 0) {
		if (player._pLightRad != snapshot.lightRadius && player.isOnActiveLevel()) {
			CurrentLightManager.ChangeLightRadius(player.lightId, snapshot.lightRadius);
			CurrentLightManager.ChangeVisionRadius(player.getId(), snapshot.lightRadius);
		}
		player._pLightRad = static_cast<int8_t>(snapshot.lightRadius);
	}
	player._pGold = static_cast<int>(snapshot.gold);
	player._pExperience = snapshot.experience;
	player.pDiabloKillLevel = snapshot.diabloKillLevel;
	player.wReflections = static_cast<uint16_t>(snapshot.reflectCharges);
	player._pStatPts = static_cast<int>(snapshot.statPoints);
	player.pManaShield = snapshot.manaShieldActive;
	player._pInfraFlag = std::any_of(snapshot.statusEffects.begin(), snapshot.statusEffects.end(), [](const ProjectedStatusEffect &effect) {
		return effect.behavior == 1;
	});
	player._pSpellFlags &= ~SpellFlag::Etherealize;
	if (std::any_of(snapshot.statusEffects.begin(), snapshot.statusEffects.end(), [](const ProjectedStatusEffect &effect) {
			return effect.behavior == 3;
		})) {
		player._pSpellFlags |= SpellFlag::Etherealize;
	}
	CurrentAutomapManager.SetAutoMapShowItems(std::any_of(snapshot.statusEffects.begin(), snapshot.statusEffects.end(), [](const ProjectedStatusEffect &effect) {
		return effect.behavior == 2;
	}));
	player.friendlyMode = snapshot.friendlyMode;
	player.direction = static_cast<Direction>(snapshot.facingDirection);
	player._pmode = static_cast<PLR_MODE>(snapshot.combatMode);
	// The authoritative transition mode owns this presentation gate. Keeping
	// the legacy flag in sync prevents stale level-change state from leaking
	// into lighting, automap, and input paths between snapshots.
	player._pLvlChanging = player._pmode == PM_NEWLVL;
	// Block animation admission is presentation state in the authoritative
	// client. Do not retain a legacy value across server-owned action changes.
	player._pBlockFlag = snapshot.combatMode == static_cast<uint32_t>(PM_BLOCK);
	player._pClass = static_cast<HeroClass>(snapshot.characterClassId);
	std::fill(std::begin(player._pLvlVisited), std::end(player._pLvlVisited), false);
	for (const uint32_t visitedLevelId : snapshot.visitedLevelIds)
		player._pLvlVisited[visitedLevelId] = true;
	std::fill(std::begin(player._pName), std::end(player._pName), '\0');
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
	player._pISpells = 0;
	for (const Item &item : player.InvBody) {
		if (!item.isEmpty() && item._iStatFlag && IsValidSpell(item._iSpell) && item._iCharges != 0)
			player._pISpells |= GetSpellBitmask(item._iSpell);
	}
	player.CalcScrolls();
	if (snapshot.stashOpen) {
		for (const auto &stashItem : snapshot.stash) {
			if (stashItem.spellId == 0 || stashItem.spellTargetingKind == ProjectedSpellTargetingKind::Unspecified)
				continue;
			const auto spellId = static_cast<SpellID>(stashItem.spellId);
			if (stashItem.item.isScrollOf(spellId) || stashItem.item.isRuneOf(spellId))
				player._pScrlSpells |= GetSpellBitmask(spellId);
		}
	}
	std::fill(std::begin(player.InvGrid), std::end(player.InvGrid), 0);
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
	SynchronizeNativePlayerPresentation(player, snapshot, previousPosition, wasOnActiveLevel);
	return { };
}

} // namespace devilution::authoritative

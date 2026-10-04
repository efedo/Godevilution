/**
 * @file game/players/players.cpp
 *
 * Implementation of player functionality, leveling, actions, creation, loading, etc.
 */
#include <algorithm>
#include <cmath>
#include <cstdint>

#ifdef USE_SDL3
#include <SDL3/SDL_events.h>
#include <SDL3/SDL_timer.h>
#else
#include <SDL.h>
#endif

#include <fmt/core.h>

#include "ui/panel/control.hpp"
#include "controls/control_mode.hpp"
#include "controls/player_controls.h"
#include "engine/cursor.h"
#include "game/monsters/monster_corpses.hpp"
#ifdef _DEBUG
#include "application/debug.h"
#endif
#include "engine/gfx/backbuffer_state.hpp"
#include "engine/load/load_cl2.hpp"
#include "engine/load/load_file.hpp"
#include "engine/math/points_in_rectangle_range.hpp"
#include "engine/random.hpp"
#include "engine/render/clx_render.hpp"
#include "engine/gfx/translation_table.hpp"
#include "engine/math/world_tile.hpp"
#include "application/game_mode.hpp"
#include "ui/gamemenu.h"
#include "application/headless_mode.hpp"
#include "ui/help.h"
#include "game/players/inv_iterators.hpp"
#include "game/levels/tile_properties.hpp"
#include "game/levels/triggers.h"
#include "engine/lighting.h"
#include "persistence/loadsave.h"
#include "game/events/event_bus.hpp"
#include "ui/minitext.h"
#include "game/missiles/missiles.hpp"
#include "game/monsters/monsters.hpp"
#include "game/monsters/monster_pool.hpp"
#include "game/items/item_pool.hpp"
#include "network/protocol/network_ticks.h"
#include "game/objects/objects.hpp"
#include "game/objects/object_pool.hpp"
#include "persistence/options.h"
#include "game/players/players.hpp"
#include "qol/autopickup.h"
#include "qol/stash.h"
#include "game/spells/spells.hpp"
#include "game/stores/stores.hpp"
#include "game/towners/towners.hpp"
#include "network/protocol/legacy_combat_policy.hpp"
#include "network/protocol/legacy_item_policy.hpp"
#include "network/protocol/legacy_player_state_policy.hpp"
#include "network/protocol/legacy_transition_policy.hpp"
#include "network/protocol/legacy_world_state_policy.hpp"
#include "utils/is_of.hpp"
#include "utils/language.h"
#include "utils/log.hpp"
#include "utils/string/str_cat.hpp"
#include "utils/string/utf8.hpp"
#include "utils/timer.hpp"

#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
#include "network/authoritative/server_backed_runtime.hpp"
#endif

namespace devilution {

uint8_t MyPlayerId;
Player *MyPlayer;
std::vector<Player> Players;
Player *InspectPlayer;
bool MyPlayerIsDead;

namespace {

bool LegacyTransitionAllowed() noexcept
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	return ShouldProcessLegacyTransitionMessage(authoritative::GetServerBackedRuntime().IsConnected());
#else
	return true;
#endif
}

bool LegacyDeathDropsAllowed() noexcept
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	return ShouldProcessLegacyCombatMessage(authoritative::GetServerBackedRuntime().IsConnected());
#else
	return true;
#endif
}

bool LegacyEquipmentDamageAllowed() noexcept
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	return ShouldProcessLegacyCombatMessage(authoritative::GetServerBackedRuntime().IsConnected());
#else
	return true;
#endif
}

bool LegacyPlayerStateMutationAllowed() noexcept
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	return ShouldProcessLegacyPlayerStateMessage(authoritative::GetServerBackedRuntime().IsConnected());
#else
	return true;
#endif
}

bool LegacyPlayerSimulationAllowed() noexcept
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	return ShouldProcessLegacyPlayerSimulation(authoritative::GetServerBackedRuntime().IsConnected());
#else
	return true;
#endif
}

bool LegacyLevelLoadPlayerPlacementAllowed() noexcept
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	return ShouldProcessLegacyLevelLoadPlayerPlacement(authoritative::GetServerBackedRuntime().IsConnected());
#else
	return true;
#endif
}

struct DirectionSettings {
	Direction dir;
	PLR_MODE walkMode;
};

void UpdatePlayerLightOffset(Player &player)
{
	if (player.lightId == NO_LIGHT)
		return;

	const WorldTileDisplacement offset = player.position.CalculateWalkingOffset(player.direction, player.animInfo);
	CurrentLightManager.ChangeLightOffset(player.lightId, offset.screenToLight());
}

void WalkInDirection(Player &player, const DirectionSettings &walkParams)
{
	player.occupyTile(player.position.future, true);
	player.position.temp = player.position.tile + walkParams.dir;
}

constexpr std::array<const DirectionSettings, 8> WalkSettings { {
	// clang-format off
	{ Direction::South,     PM_WALK_SOUTHWARDS },
	{ Direction::SouthWest, PM_WALK_SOUTHWARDS },
	{ Direction::West,      PM_WALK_SIDEWAYS   },
	{ Direction::NorthWest, PM_WALK_NORTHWARDS },
	{ Direction::North,     PM_WALK_NORTHWARDS },
	{ Direction::NorthEast, PM_WALK_NORTHWARDS },
	{ Direction::East,      PM_WALK_SIDEWAYS   },
	{ Direction::SouthEast, PM_WALK_SOUTHWARDS }
	// clang-format on
} };

bool PlrDirOK(const Player &player, Direction dir)
{
	const Point position = player.position.tile;
	const Point futurePosition = position + dir;
	if (futurePosition.x < 0 || !player.positionIsAvailable(futurePosition)) {
		return false;
	}

	if (dir == Direction::East) {
		return !IsTileSolid(position + Direction::SouthEast);
	}

	if (dir == Direction::West) {
		return !IsTileSolid(position + Direction::SouthWest);
	}

	return true;
}

void HandleWalkMode(Player &player, Direction dir)
{
	const auto &dirModeParams = WalkSettings[static_cast<size_t>(dir)];
	player.saveOldPosition();
	if (!PlrDirOK(player, dir)) {
		return;
	}

	player.direction = dir;

	// The player's tile position after finishing this movement action
	player.position.future = player.position.tile + dirModeParams.dir;

	WalkInDirection(player, dirModeParams);

	player.tempDirection = dirModeParams.dir;
	player._pmode = dirModeParams.walkMode;
}

void StartWalkAnimation(Player &player, Direction dir, bool pmWillBeCalled)
{
	int8_t skippedFrames = -2;
	if (levelType() == DTYPE_TOWN && sgGameInitInfo.bRunInTown != 0)
		skippedFrames = 2;
	if (pmWillBeCalled)
		skippedFrames += 1;
	player.setAnimation(player_graphic::Walk, dir, AnimationDistributionFlags::ProcessAnimationPending, skippedFrames);
}

/**
 * @brief Start moving a player to a new tile
 */
void StartWalk(Player &player, Direction dir, bool pmWillBeCalled)
{
	if (!LegacyPlayerSimulationAllowed())
		return;

	if (player._pInvincible && player.hasNoLife() && &player == MyPlayer) {
		player.syncKill(DeathReason::Unknown);
		return;
	}

	StartWalkAnimation(player, dir, pmWillBeCalled);
	HandleWalkMode(player, dir);
}

void ClearStateVariables(Player &player)
{
	player.position.temp = { 0, 0 };
	player.tempDirection = Direction::South;
	player.queuedSpell.spellLevel = 0;
}

void StartAttack(Player &player, Direction d, bool includesFirstFrame)
{
	if (!LegacyPlayerSimulationAllowed())
		return;

	if (player._pInvincible && player.hasNoLife() && &player == MyPlayer) {
		player.syncKill(DeathReason::Unknown);
		return;
	}

	int8_t skippedAnimationFrames = 0;
	const auto flags = player._pIFlags;

	// If the first frame is not included in vanilla, the skip logic for the first frame will not be executed.
	// This will result in a different and slower attack speed.
	if (HasAnyOf(flags, ItemSpecialEffect::FastestAttack)) {
		// If the fastest attack logic is trigger frames in vanilla two frames are skipped, so missing the first frame reduces the skip logic by two frames.
		skippedAnimationFrames = includesFirstFrame ? 4 : 2;
	} else if (HasAnyOf(flags, ItemSpecialEffect::FasterAttack)) {
		skippedAnimationFrames = includesFirstFrame ? 3 : 2;
	} else if (HasAnyOf(flags, ItemSpecialEffect::FastAttack)) {
		skippedAnimationFrames = includesFirstFrame ? 2 : 1;
	} else if (HasAnyOf(flags, ItemSpecialEffect::QuickAttack)) {
		skippedAnimationFrames = includesFirstFrame ? 1 : 0;
	}

	auto animationFlags = AnimationDistributionFlags::ProcessAnimationPending;
	if (player._pmode == PM_ATTACK)
		animationFlags = static_cast<AnimationDistributionFlags>(animationFlags | AnimationDistributionFlags::RepeatedAction);
	player.setAnimation(player_graphic::Attack, d, animationFlags, skippedAnimationFrames, player._pAFNum);
	player._pmode = PM_ATTACK;
	player.fixLocation(d);
	player.saveOldPosition();
}

void StartRangeAttack(Player &player, Direction d, WorldTileCoord cx, WorldTileCoord cy, bool includesFirstFrame)
{
	if (!LegacyPlayerSimulationAllowed())
		return;

	if (player._pInvincible && player.hasNoLife() && &player == MyPlayer) {
		player.syncKill(DeathReason::Unknown);
		return;
	}

	int8_t skippedAnimationFrames = 0;
	const auto flags = player._pIFlags;

	if (!gbIsHellfire) {
		if (includesFirstFrame && HasAnyOf(flags, ItemSpecialEffect::QuickAttack | ItemSpecialEffect::FastAttack)) {
			skippedAnimationFrames += 1;
		}
		if (HasAnyOf(flags, ItemSpecialEffect::FastAttack)) {
			skippedAnimationFrames += 1;
		}
	}

	auto animationFlags = AnimationDistributionFlags::ProcessAnimationPending;
	if (player._pmode == PM_RATTACK)
		animationFlags = static_cast<AnimationDistributionFlags>(animationFlags | AnimationDistributionFlags::RepeatedAction);
	player.setAnimation(player_graphic::Attack, d, animationFlags, skippedAnimationFrames, player._pAFNum);

	player._pmode = PM_RATTACK;
	player.fixLocation(d);
	player.saveOldPosition();
	player.position.temp = WorldTilePosition { cx, cy };
}

player_graphic GetPlayerGraphicForSpell(SpellID spellId)
{
	switch (GetSpellData(spellId).type()) {
	case MagicType::Fire:
		return player_graphic::Fire;
	case MagicType::Lightning:
		return player_graphic::Lightning;
	default:
		return player_graphic::Magic;
	}
}

void StartSpell(Player &player, Direction d, WorldTileCoord cx, WorldTileCoord cy)
{
	if (!LegacyPlayerSimulationAllowed())
		return;

	if (player._pInvincible && player.hasNoLife() && &player == MyPlayer) {
		player.syncKill(DeathReason::Unknown);
		return;
	}

	// Checks conditions for spell again, because initial check was done when spell was queued and the parameters could be changed meanwhile
	bool isValid = false;
	switch (player.queuedSpell.spellType) {
	case SpellType::Skill:
	case SpellType::Spell:
		isValid = CheckSpell(player, player.queuedSpell.spellId, player.queuedSpell.spellType, true) == SpellCheckResult::Success;
		break;
	case SpellType::Scroll:
		isValid = CanUseScroll(player, player.queuedSpell.spellId);
		break;
	case SpellType::Charges:
		isValid = CanUseStaff(player, player.queuedSpell.spellId);
		break;
	default:
		break;
	}
	if (!isValid)
		return;

	auto animationFlags = AnimationDistributionFlags::ProcessAnimationPending;
	if (player._pmode == PM_SPELL)
		animationFlags = static_cast<AnimationDistributionFlags>(animationFlags | AnimationDistributionFlags::RepeatedAction);
	player.setAnimation(GetPlayerGraphicForSpell(player.queuedSpell.spellId), d, animationFlags, 0, player._pSFNum);

	PlaySfxLoc(GetSpellData(player.queuedSpell.spellId).sSFX, player.position.tile);

	player._pmode = PM_SPELL;

	player.fixLocation(d);
	player.saveOldPosition();

	player.position.temp = WorldTilePosition { cx, cy };
	player.queuedSpell.spellLevel = player.GetSpellLevel(player.queuedSpell.spellId);
	player.executedSpell = player.queuedSpell;
}

void RespawnDeadItem(Item &&itm, Point target)
{
	if (!LegacyDeathDropsAllowed())
		return;

	if (!ItemPoolAdapter::HasFreeItemSlot())
		return;

	const int ii = AllocateItem();
	Item &item = Items[ii];

	tileAt(target).setItem(ii + 1);

	item = itm;
	item.position = target;
	RespawnItem(item, true);
	NetSendCmdPItem(false, CMD_SPAWNITEM, target, item);
}

void DeadItem(Player &player, Item &&item, Displacement direction)
{
	if (!LegacyDeathDropsAllowed())
		return;

	if (item.isEmpty())
		return;

	const Point playerTile = player.position.tile;
	if (direction != Displacement { 0, 0 }) {
		const Point target = playerTile + direction;
		if (ItemSpaceOk(target)) {
			RespawnDeadItem(std::move(item), target);
			return;
		}
	}

	std::optional<Point> dropPoint = FindClosestValidPosition(ItemSpaceOk, playerTile, 1, 50);
	if (dropPoint) {
		RespawnDeadItem(std::move(item), *dropPoint);
	}
}

int DropGold(Player &player, int amount, bool skipFullStacks)
{
	if (!LegacyDeathDropsAllowed())
		return amount;

	for (int i = 0; i < player._pNumInv && amount > 0; i++) {
		Item &item = player.InvList[i];

		if (item._itype != ItemType::Gold || (skipFullStacks && item._ivalue == MaxGold))
			continue;

		if (amount < item._ivalue) {
			Item goldItem;
			MakeGoldStack(goldItem, amount);
			DeadItem(player, std::move(goldItem), { 0, 0 });

			item._ivalue -= amount;

			return 0;
		}

		amount -= item._ivalue;
		DeadItem(player, std::move(item), { 0, 0 });
		player.RemoveInvItem(i);
		i = -1;
	}

	return amount;
}

void DropHalfPlayersGold(Player &player)
{
	if (!LegacyDeathDropsAllowed())
		return;

	const int remainingGold = DropGold(player, player._pGold / 2, true);
	if (remainingGold > 0) {
		DropGold(player, remainingGold, false);
	}

	player._pGold /= 2;
}

void InitLevelChange(Player &player)
{
	const Player &myPlayer = *MyPlayer;
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	const bool serverConnected = authoritative::GetServerBackedRuntime().IsConnected();
	#else
	const bool serverConnected = false;
	#endif

	RemoveEnemyReferences(player);
	player.removeMissiles();
	const bool processLegacyStatusLifecycle = ShouldProcessLegacyStatusLifecycle(serverConnected);
	if (processLegacyStatusLifecycle)
		player.pManaShield = false;
	if (processLegacyStatusLifecycle)
		player.wReflections = 0;
	if (&player != MyPlayer) {
		// share info about your manashield when another player joins the level
		if (processLegacyStatusLifecycle && myPlayer.pManaShield)
			NetSendCmd(true, CMD_SETSHIELD);
		// share info about your reflect charges when another player joins the level
		if (processLegacyStatusLifecycle)
			NetSendCmdParam1(true, CMD_SETREFLECT, myPlayer.wReflections);
	} else if (qtextflag) {
		qtextflag = false;
		stream_stop();
	}

	FixPlrWalkTags(player);
	player.saveOldPosition();
	if (&player == MyPlayer) {
		player.occupyTile(player.position.tile, false);
	} else {
		player._pLvlVisited[player.plrlevel] = true;
	}

	player.clearPath();
	player.destAction = ACTION_NONE;
	player._pLvlChanging = true;

	if (&player == MyPlayer) {
		player.pLvlLoad = 10;
	}
}

/**
 * @brief Continue movement towards new tile
 */
bool DoWalk(Player &player)
{
	if (!LegacyPlayerSimulationAllowed())
		return false;

	// Play walking sound effect on certain animation frames
	if (*GetOptions().Audio.walkingSound && (levelType() != DTYPE_TOWN || sgGameInitInfo.bRunInTown == 0)) {
		if (player.animInfo.currentFrame == 0
		    || player.animInfo.currentFrame == 4) {
			PlaySfxLoc(SfxID::Walk, player.position.tile);
		}
	}

	if (!player.animInfo.isLastFrame()) {
		// We didn't reach new tile so update player's "sub-tile" position
		UpdatePlayerLightOffset(player);
		return false;
	}

	// We reached the new tile -> update the player's tile position
	tileAt(player.position.tile).setPlayer(0);
	player.position.tile = player.position.temp;
	// dPlayer is set here for backwards compatibility; without it, the player would be invisible if loaded from a vanilla save.
	player.occupyTile(player.position.tile, false);

	// Update the coordinates for lighting and vision entries for the player
	if (levelType() != DTYPE_TOWN) {
		CurrentLightManager.ChangeLightXY(player.lightId, player.position.tile);
		CurrentLightManager.ChangeVisionXY(player.getId(), player.position.tile);
	}

	player.startStand(player.tempDirection);

	ClearStateVariables(player);

	// Reset the "sub-tile" position of the player's light entry to 0
	if (levelType() != DTYPE_TOWN) {
		CurrentLightManager.ChangeLightOffset(player.lightId, { 0, 0 });
	}

	AutoPickup(player);
	return true;
}

bool WeaponDecay(Player &player, int ii)
{
	if (!LegacyEquipmentDamageAllowed())
		return false;

	if (!player.InvBody[ii].isEmpty() && player.InvBody[ii]._iClass == ICLASS_WEAPON && HasAnyOf(player.InvBody[ii]._iDamAcFlags, ItemSpecialEffectHf::Decay)) {
		player.InvBody[ii]._iPLDam -= 5;
		if (player.InvBody[ii]._iPLDam <= -100) {
			RemoveEquipment(player, static_cast<inv_body_loc>(ii), true);
			CalcPlrInv(player, true);
			return true;
		}
		CalcPlrInv(player, true);
	}
	return false;
}

bool DamageWeapon(Player &player, unsigned damageFrequency)
{
	if (!LegacyEquipmentDamageAllowed())
		return false;

	if (&player != MyPlayer) {
		return false;
	}

	if (WeaponDecay(player, INVLOC_HAND_LEFT))
		return true;
	if (WeaponDecay(player, INVLOC_HAND_RIGHT))
		return true;

	if (!FlipCoin(damageFrequency)) {
		return false;
	}

	if (!player.InvBody[INVLOC_HAND_LEFT].isEmpty() && player.InvBody[INVLOC_HAND_LEFT]._iClass == ICLASS_WEAPON) {
		if (player.InvBody[INVLOC_HAND_LEFT]._iDurability == DUR_INDESTRUCTIBLE) {
			return false;
		}

		player.InvBody[INVLOC_HAND_LEFT]._iDurability--;
		if (player.InvBody[INVLOC_HAND_LEFT]._iDurability <= 0) {
			RemoveEquipment(player, INVLOC_HAND_LEFT, true);
			CalcPlrInv(player, true);
			return true;
		}
	}

	if (!player.InvBody[INVLOC_HAND_RIGHT].isEmpty() && player.InvBody[INVLOC_HAND_RIGHT]._iClass == ICLASS_WEAPON) {
		if (player.InvBody[INVLOC_HAND_RIGHT]._iDurability == DUR_INDESTRUCTIBLE) {
			return false;
		}

		player.InvBody[INVLOC_HAND_RIGHT]._iDurability--;
		if (player.InvBody[INVLOC_HAND_RIGHT]._iDurability == 0) {
			RemoveEquipment(player, INVLOC_HAND_RIGHT, true);
			CalcPlrInv(player, true);
			return true;
		}
	}

	if (player.InvBody[INVLOC_HAND_LEFT].isEmpty() && player.InvBody[INVLOC_HAND_RIGHT]._itype == ItemType::Shield) {
		if (player.InvBody[INVLOC_HAND_RIGHT]._iDurability == DUR_INDESTRUCTIBLE) {
			return false;
		}

		player.InvBody[INVLOC_HAND_RIGHT]._iDurability--;
		if (player.InvBody[INVLOC_HAND_RIGHT]._iDurability == 0) {
			RemoveEquipment(player, INVLOC_HAND_RIGHT, true);
			CalcPlrInv(player, true);
			return true;
		}
	}

	if (player.InvBody[INVLOC_HAND_RIGHT].isEmpty() && player.InvBody[INVLOC_HAND_LEFT]._itype == ItemType::Shield) {
		if (player.InvBody[INVLOC_HAND_LEFT]._iDurability == DUR_INDESTRUCTIBLE) {
			return false;
		}

		player.InvBody[INVLOC_HAND_LEFT]._iDurability--;
		if (player.InvBody[INVLOC_HAND_LEFT]._iDurability == 0) {
			RemoveEquipment(player, INVLOC_HAND_LEFT, true);
			CalcPlrInv(player, true);
			return true;
		}
	}

	return false;
}

bool PlrHitMonst(Player &player, Monster &monster, bool adjacentDamage = false)
{
	if (!LegacyPlayerSimulationAllowed())
		return false;

	int hper = 0;

	if (!monster.isPossibleToHit())
		return false;

	if (adjacentDamage) {
		if (player.getCharacterLevel() > 20)
			hper -= 30;
		else
			hper -= (35 - player.getCharacterLevel()) * 2;
	}

	int hit = GenerateRnd(100);
	if (monster.mode == MonsterMode::Petrified) {
		hit = 0;
	}

	hper += player.GetMeleePiercingToHit() - player.CalculateArmorPierce(monster.armorClass, true);
	hper = std::clamp(hper, 5, 95);

	if (monster.tryLiftGargoyle())
		return true;

	if (hit >= hper) {
#ifdef _DEBUG
		if (!DebugGodMode)
#endif
			return false;
	}

	if (gbIsHellfire && HasAllOf(player._pIFlags, ItemSpecialEffect::FireDamage | ItemSpecialEffect::LightningDamage)) {
		// Fixed off by 1 error from Hellfire
		const int midam = RandomIntBetween(player.damageBonuses.fire.minimum, player.damageBonuses.fire.maximum);
		AddMissile(player.position.tile, player.position.temp, player.direction, MissileID::SpectralArrow, TARGET_MONSTERS, player, midam, 0);
	}
	const int mind = player.damageBonuses.physical.minimum;
	const int maxd = player.damageBonuses.physical.maximum;
	int dam = RandomIntBetween(mind, maxd);
	dam += dam * player.damageBonuses.percent / 100;
	dam += player.damageBonuses.flat;
	int dam2 = dam << 6;
	dam += player._pDamageMod;

	const ClassAttributes &classAttributes = GetClassAttributes(player._pClass);
	if (HasAnyOf(classAttributes.classFlags, PlayerClassFlag::CriticalStrike)) {
		if (GenerateRnd(100) < player.getCharacterLevel()) {
			dam *= 2;
		}
	}

	ItemType phanditype = ItemType::None;
	if (player.InvBody[INVLOC_HAND_LEFT]._itype == ItemType::Sword || player.InvBody[INVLOC_HAND_RIGHT]._itype == ItemType::Sword) {
		phanditype = ItemType::Sword;
	}
	if (player.InvBody[INVLOC_HAND_LEFT]._itype == ItemType::Mace || player.InvBody[INVLOC_HAND_RIGHT]._itype == ItemType::Mace) {
		phanditype = ItemType::Mace;
	}

	switch (monster.data().monsterClass) {
	case MonsterClass::Undead:
		if (phanditype == ItemType::Sword) {
			dam -= dam / 2;
		} else if (phanditype == ItemType::Mace) {
			dam += dam / 2;
		}
		break;
	case MonsterClass::Animal:
		if (phanditype == ItemType::Mace) {
			dam -= dam / 2;
		} else if (phanditype == ItemType::Sword) {
			dam += dam / 2;
		}
		break;
	case MonsterClass::Demon:
		if (HasAnyOf(player._pIFlags, ItemSpecialEffect::TripleDemonDamage)) {
			dam *= 3;
		}
		break;
	}

	if (HasAnyOf(player.pDamAcFlags, ItemSpecialEffectHf::Devastation) && GenerateRnd(100) < 5) {
		dam *= 3;
	}

	if (HasAnyOf(player.pDamAcFlags, ItemSpecialEffectHf::Doppelganger) && monster.type().type != MT_DIABLO && !monster.isUnique() && GenerateRnd(100) < 10) {
		monster.addDoppelganger();
	}

	dam <<= 6;
	if (HasAnyOf(player.pDamAcFlags, ItemSpecialEffectHf::Jesters)) {
		int r = GenerateRnd(201);
		if (r >= 100)
			r = 100 + (r - 100) * 5;
		dam = dam * r / 100;
	}

	if (adjacentDamage)
		dam >>= 2;

	if (&player == MyPlayer) {
		if (HasAnyOf(player.pDamAcFlags, ItemSpecialEffectHf::Peril)) {
			dam2 += player._pIGetHit << 6;
			if (dam2 >= 0) {
				player.applyDamage(DamageType::Physical, 0, 1, dam2);
			}
			dam *= 2;
		}
#ifdef _DEBUG
		if (DebugGodMode) {
			dam = monster.hitPoints; /* ensure monster is killed with one hit */
		}
#endif
		monster.applyDamage(DamageType::Physical, dam);
	}

	int skdam = 0;
	if (HasAnyOf(player._pIFlags, ItemSpecialEffect::RandomStealLife)) {
		skdam = GenerateRnd(dam / 8);
		player.life.current += skdam;
		if (player.life.current > player.life.maximum) {
			player.life.current = player.life.maximum;
		}
		player.life.base += skdam;
		if (player.life.base > player.life.maximumBase) {
			player.life.base = player.life.maximumBase;
		}
		RedrawComponent(PanelDrawComponent::Health);
	}
	if (HasAnyOf(player._pIFlags, ItemSpecialEffect::StealMana3 | ItemSpecialEffect::StealMana5) && HasNoneOf(player._pIFlags, ItemSpecialEffect::NoMana)) {
		if (HasAnyOf(player._pIFlags, ItemSpecialEffect::StealMana3)) {
			skdam = 3 * dam / 100;
		}
		if (HasAnyOf(player._pIFlags, ItemSpecialEffect::StealMana5)) {
			skdam = 5 * dam / 100;
		}
		player.mana.current += skdam;
		if (player.mana.current > player.mana.maximum) {
			player.mana.current = player.mana.maximum;
		}
		player.mana.base += skdam;
		if (player.mana.base > player.mana.maximumBase) {
			player.mana.base = player.mana.maximumBase;
		}
		RedrawComponent(PanelDrawComponent::Mana);
	}
	if (HasAnyOf(player._pIFlags, ItemSpecialEffect::StealLife3 | ItemSpecialEffect::StealLife5)) {
		if (HasAnyOf(player._pIFlags, ItemSpecialEffect::StealLife3)) {
			skdam = 3 * dam / 100;
		}
		if (HasAnyOf(player._pIFlags, ItemSpecialEffect::StealLife5)) {
			skdam = 5 * dam / 100;
		}
		player.life.current += skdam;
		if (player.life.current > player.life.maximum) {
			player.life.current = player.life.maximum;
		}
		player.life.base += skdam;
		if (player.life.base > player.life.maximumBase) {
			player.life.base = player.life.maximumBase;
		}
		RedrawComponent(PanelDrawComponent::Health);
	}
	if (monster.hasNoLife()) {
		monster.startKill(player);
	} else {
		if (monster.mode != MonsterMode::Petrified && HasAnyOf(player._pIFlags, ItemSpecialEffect::Knockback))
			monster.getKnockback(player.position.tile);
		monster.startHit(player, dam);
	}
	return true;
}

bool PlrHitPlr(Player &attacker, Player &target)
{
	if (!LegacyPlayerSimulationAllowed())
		return false;

	if (target._pInvincible) {
		return false;
	}

	if (HasAnyOf(target._pSpellFlags, SpellFlag::Etherealize)) {
		return false;
	}

	const int hit = GenerateRnd(100);

	int hper = attacker.GetMeleeToHit() - target.GetArmor();
	hper = std::clamp(hper, 5, 95);

	int blk = 100;
	if ((target._pmode == PM_STAND || target._pmode == PM_ATTACK) && target._pBlockFlag) {
		blk = GenerateRnd(100);
	}

	int blkper = target.GetBlockChance() - (attacker.getCharacterLevel() * 2);
	blkper = std::clamp(blkper, 0, 100);

	if (hit >= hper) {
		return false;
	}

	if (blk < blkper) {
		const Direction dir = GetDirection(target.position.tile, attacker.position.tile);
		target.startBlock(dir);
		return true;
	}

	const int mind = attacker.damageBonuses.physical.minimum;
	const int maxd = attacker.damageBonuses.physical.maximum;
	int dam = RandomIntBetween(mind, maxd);
	dam += (dam * attacker.damageBonuses.percent) / 100;
	dam += attacker.damageBonuses.flat + attacker._pDamageMod;

	const ClassAttributes &classAttributes = GetClassAttributes(attacker._pClass);
	if (HasAnyOf(classAttributes.classFlags, PlayerClassFlag::CriticalStrike)) {
		if (GenerateRnd(100) < attacker.getCharacterLevel()) {
			dam *= 2;
		}
	}
	const int skdam = dam << 6;
	if (HasAnyOf(attacker._pIFlags, ItemSpecialEffect::RandomStealLife)) {
		const int tac = GenerateRnd(skdam / 8);
		attacker.life.current += tac;
		if (attacker.life.current > attacker.life.maximum) {
			attacker.life.current = attacker.life.maximum;
		}
		attacker.life.base += tac;
		if (attacker.life.base > attacker.life.maximumBase) {
			attacker.life.base = attacker.life.maximumBase;
		}
		RedrawComponent(PanelDrawComponent::Health);
	}
	if (&attacker == MyPlayer) {
		NetSendCmdDamage(true, target, skdam, DamageType::Physical);
	}
	target.startHit(skdam, false);

	return true;
}

bool PlrHitObj(const Player &player, Object &targetObject)
{
	if (!LegacyPlayerSimulationAllowed())
		return false;

	if (targetObject.IsBreakable()) {
		BreakObject(player, targetObject);
		return true;
	}

	return false;
}

bool DoAttack(Player &player)
{
	if (!LegacyPlayerSimulationAllowed())
		return false;

	if (player.animInfo.currentFrame == player._pAFNum - 2) {
		PlaySfxLoc(SfxID::Swing, player.position.tile);
	}

	bool didhit = false;

	if (player.animInfo.currentFrame == player._pAFNum - 1) {
		Point position = player.position.tile + player.direction;
		Monster *monster = FindMonsterAtPosition(position);

		if (monster != nullptr) {
			if (monster->canTalk()) {
				player.position.temp.x = 0; /** @todo Looks to be irrelevant, probably just remove it */
				return false;
			}
		}

		if (!gbIsHellfire || !HasAllOf(player._pIFlags, ItemSpecialEffect::FireDamage | ItemSpecialEffect::LightningDamage)) {
			if (HasAnyOf(player._pIFlags, ItemSpecialEffect::FireDamage)) {
				AddMissile(position, { 1, 0 }, Direction::South, MissileID::WeaponExplosion, TARGET_MONSTERS, player, 0, 0);
			}
			if (HasAnyOf(player._pIFlags, ItemSpecialEffect::LightningDamage)) {
				AddMissile(position, { 2, 0 }, Direction::South, MissileID::WeaponExplosion, TARGET_MONSTERS, player, 0, 0);
			}
		}

		if (monster != nullptr) {
			didhit = PlrHitMonst(player, *monster);
		} else if (PlayerAtPosition(position) != nullptr && !player.friendlyMode) {
			didhit = PlrHitPlr(player, *PlayerAtPosition(position));
		} else {
			Object *object = FindObjectAtPosition(position, false);
			if (object != nullptr) {
				didhit = PlrHitObj(player, *object);
			}
		}
		if (player.CanCleave()) {
			// playing as a class/weapon with cleave
			position = player.position.tile + Right(player.direction);
			monster = FindMonsterAtPosition(position);
			if (monster != nullptr) {
					if (!monster->canTalk() && monster->position.old == position) {
						if (PlrHitMonst(player, *monster, true))
							didhit = true;
					}
				}
				position = player.position.tile + Left(player.direction);
				monster = FindMonsterAtPosition(position);
				if (monster != nullptr) {
					if (!monster->canTalk() && monster->position.old == position) {
					if (PlrHitMonst(player, *monster, true))
						didhit = true;
				}
			}
		}

		if (didhit && DamageWeapon(player, 30)) {
			player.startStand(player.direction);
			ClearStateVariables(player);
			return true;
		}
	}

	if (player.animInfo.isLastFrame()) {
		player.startStand(player.direction);
		ClearStateVariables(player);
		return true;
	}

	return false;
}

bool DoRangeAttack(Player &player)
{
	if (!LegacyPlayerSimulationAllowed())
		return false;

	int arrows = 0;
	if (player.animInfo.currentFrame == player._pAFNum - 1) {
		arrows = 1;
	}

	if (HasAnyOf(player._pIFlags, ItemSpecialEffect::MultipleArrows) && player.animInfo.currentFrame == player._pAFNum + 1) {
		arrows = 2;
	}

	for (int arrow = 0; arrow < arrows; arrow++) {
		int xoff = 0;
		int yoff = 0;
		if (arrows != 1) {
			const int angle = arrow == 0 ? -1 : 1;
			const int x = player.position.temp.x - player.position.tile.x;
			if (x != 0)
				yoff = x < 0 ? angle : -angle;
			const int y = player.position.temp.y - player.position.tile.y;
			if (y != 0)
				xoff = y < 0 ? -angle : angle;
		}

		int dmg = 4;
		MissileID mistype = MissileID::Arrow;
		if (HasAnyOf(player._pIFlags, ItemSpecialEffect::FireArrows)) {
			mistype = MissileID::FireArrow;
		}
		if (HasAnyOf(player._pIFlags, ItemSpecialEffect::LightningArrows)) {
			mistype = MissileID::LightningArrow;
		}
		if (HasAllOf(player._pIFlags, ItemSpecialEffect::FireArrows | ItemSpecialEffect::LightningArrows)) {
			// Fixed off by 1 error from Hellfire
			dmg = RandomIntBetween(player.damageBonuses.fire.minimum, player.damageBonuses.fire.maximum);
			mistype = MissileID::SpectralArrow;
		}

		AddMissile(
		    player.position.tile,
		    player.position.temp + Displacement { xoff, yoff },
		    player.direction,
		    mistype,
		    TARGET_MONSTERS,
		    player,
		    dmg,
		    0);

		if (arrow == 0 && mistype != MissileID::SpectralArrow) {
			PlaySfxLoc(arrows != 1 ? SfxID::ShootBow2 : SfxID::ShootBow, player.position.tile);
		}

		if (DamageWeapon(player, 40)) {
			player.startStand(player.direction);
			ClearStateVariables(player);
			return true;
		}
	}

	if (player.animInfo.isLastFrame()) {
		player.startStand(player.direction);
		ClearStateVariables(player);
		return true;
	}
	return false;
}

void DamageParryItem(Player &player)
{
	if (!LegacyEquipmentDamageAllowed())
		return;

	if (&player != MyPlayer) {
		return;
	}

	if (player.InvBody[INVLOC_HAND_LEFT]._itype == ItemType::Shield || player.InvBody[INVLOC_HAND_LEFT]._itype == ItemType::Staff) {
		if (player.InvBody[INVLOC_HAND_LEFT]._iDurability == DUR_INDESTRUCTIBLE) {
			return;
		}

		player.InvBody[INVLOC_HAND_LEFT]._iDurability--;
		if (player.InvBody[INVLOC_HAND_LEFT]._iDurability == 0) {
			RemoveEquipment(player, INVLOC_HAND_LEFT, true);
			CalcPlrInv(player, true);
		}
	}

	if (player.InvBody[INVLOC_HAND_RIGHT]._itype == ItemType::Shield) {
		if (player.InvBody[INVLOC_HAND_RIGHT]._iDurability != DUR_INDESTRUCTIBLE) {
			player.InvBody[INVLOC_HAND_RIGHT]._iDurability--;
			if (player.InvBody[INVLOC_HAND_RIGHT]._iDurability == 0) {
				RemoveEquipment(player, INVLOC_HAND_RIGHT, true);
				CalcPlrInv(player, true);
			}
		}
	}
}

bool DoBlock(Player &player)
{
	if (!LegacyPlayerSimulationAllowed())
		return false;

	if (player.animInfo.isLastFrame()) {
		player.startStand(player.direction);
		ClearStateVariables(player);

		if (FlipCoin(10)) {
			DamageParryItem(player);
		}
		return true;
	}

	return false;
}

void DamageArmor(Player &player)
{
	if (!LegacyEquipmentDamageAllowed())
		return;

	if (&player != MyPlayer) {
		return;
	}

	if (player.InvBody[INVLOC_CHEST].isEmpty() && player.InvBody[INVLOC_HEAD].isEmpty()) {
		return;
	}

	bool targetHead = FlipCoin(3);
	if (!player.InvBody[INVLOC_CHEST].isEmpty() && player.InvBody[INVLOC_HEAD].isEmpty()) {
		targetHead = false;
	}
	if (player.InvBody[INVLOC_CHEST].isEmpty() && !player.InvBody[INVLOC_HEAD].isEmpty()) {
		targetHead = true;
	}

	Item *pi;
	if (targetHead) {
		pi = &player.InvBody[INVLOC_HEAD];
	} else {
		pi = &player.InvBody[INVLOC_CHEST];
	}
	if (pi->_iDurability == DUR_INDESTRUCTIBLE) {
		return;
	}

	pi->_iDurability--;
	if (pi->_iDurability != 0) {
		return;
	}

	if (targetHead) {
		RemoveEquipment(player, INVLOC_HEAD, true);
	} else {
		RemoveEquipment(player, INVLOC_CHEST, true);
	}
	CalcPlrInv(player, true);
}

bool DoSpell(Player &player)
{
	if (!LegacyPlayerSimulationAllowed())
		return false;

	if (player.animInfo.currentFrame == player._pSFNum) {
		CastSpell(
		    player,
		    player.executedSpell.spellId,
		    player.position.tile,
		    player.position.temp,
		    player.executedSpell.spellLevel);

		if (IsAnyOf(player.executedSpell.spellType, SpellType::Scroll, SpellType::Charges)) {
			EnsureValidReadiedSpell(player);
		}
	}

	if (player.animInfo.isLastFrame()) {
		player.startStand(player.direction);
		ClearStateVariables(player);
		return true;
	}

	return false;
}

bool DoGotHit(Player &player)
{
	if (!LegacyPlayerSimulationAllowed())
		return false;

	if (player.animInfo.isLastFrame()) {
		player.startStand(player.direction);
		ClearStateVariables(player);
		if (!FlipCoin(4)) {
			DamageArmor(player);
		}

		return true;
	}

	return false;
}

bool DoDeath(Player &player)
{
	if (!LegacyPlayerSimulationAllowed())
		return false;

	if (player.animInfo.isLastFrame()) {
		if (player.animInfo.tickCounterOfCurrentFrame == 0) {
			player.animInfo.ticksPerFrame = 100;
			tileAt(player.position.tile).addFlags(DungeonFlag::DeadPlayer);
		} else if (&player == MyPlayer && player.animInfo.tickCounterOfCurrentFrame == 30) {
			MyPlayerIsDead = true;
		}
	}

	return false;
}

bool IsPlayerAdjacentToObject(Player &player, Object &object)
{
	const int x = std::abs(player.position.tile.x - object.position.x);
	int y = std::abs(player.position.tile.y - object.position.y);
	if (y > 1 && object.position.y >= 1 && FindObjectAtPosition(object.position + Direction::NorthEast) == &object) {
		// special case for activating a large object from the north-east side
		y = std::abs(player.position.tile.y - object.position.y + 1);
	}
	return x <= 1 && y <= 1;
}

void TryDisarm(const Player &player, Object &object)
{
	if (&player == MyPlayer)
		NewCursor(CURSOR_HAND);

#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacyWorldStateMessage(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
#endif

	if (!object._oTrapFlag) {
		return;
	}
	const int trapdisper = (2 * player.attributes.dexterity.current) - (5 * currentLevelNumber());
	if (GenerateRnd(100) > trapdisper) {
		return;
	}
	for (Object &trap : ObjectPoolAdapter::ActiveObjectsRange()) {
		if (trap.IsTrap() && FindObjectAtPosition({ trap._oVar1, trap._oVar2 }) == &object) {
			trap._oVar4 = 1;
			object._oTrapFlag = false;
		}
	}
	if (object.IsTrappedChest()) {
		object._oTrapFlag = false;
	}
}

void CheckNewPath(Player &player, bool pmWillBeCalled)
{
	int x = 0;
	int y = 0;

	Monster *monster;
	Player *target;
	Object *object;
	Item *item;

	const int targetId = player.destParam1;

	switch (player.destAction) {
	case ACTION_ATTACKMON:
	case ACTION_RATTACKMON:
	case ACTION_SPELLMON:
		monster = &Monsters[targetId];
		if (monster->hasNoLife()) {
			player.Stop();
			return;
		}
		if (player.destAction == ACTION_ATTACKMON)
			player.makePath(monster->position.future, false);
		break;
	case ACTION_ATTACKPLR:
	case ACTION_RATTACKPLR:
	case ACTION_SPELLPLR:
		target = &Players[targetId];
		if (target->hasNoLife()) {
			player.Stop();
			return;
		}
		if (player.destAction == ACTION_ATTACKPLR)
			player.makePath(target->position.future, false);
		break;
	case ACTION_OPERATE:
	case ACTION_DISARM:
	case ACTION_OPERATETK:
		object = &Objects[targetId];
		break;
	case ACTION_PICKUPITEM:
	case ACTION_PICKUPAITEM:
		item = &Items[targetId];
		break;
	default:
		break;
	}

	Direction d;
	if (player.walkpath[0] != WALK_NONE) {
		if (player._pmode == PM_STAND) {
			if (&player == MyPlayer) {
				if (player.destAction == ACTION_ATTACKMON || player.destAction == ACTION_ATTACKPLR) {
					if (player.destAction == ACTION_ATTACKMON) {
						x = std::abs(player.position.future.x - monster->position.future.x);
						y = std::abs(player.position.future.y - monster->position.future.y);
						d = GetDirection(player.position.future, monster->position.future);
					} else {
						x = std::abs(player.position.future.x - target->position.future.x);
						y = std::abs(player.position.future.y - target->position.future.y);
						d = GetDirection(player.position.future, target->position.future);
					}

					if (x < 2 && y < 2) {
						player.clearPath();
						if (player.destAction == ACTION_ATTACKMON && monster->talkMsg != TEXT_NONE && monster->talkMsg != TEXT_VILE14) {
							TalktoMonster(player, *monster);
						} else {
							StartAttack(player, d, pmWillBeCalled);
						}
						player.destAction = ACTION_NONE;
					}
				}
			}

			switch (player.walkpath[0]) {
			case WALK_N:
				StartWalk(player, Direction::North, pmWillBeCalled);
				break;
			case WALK_NE:
				StartWalk(player, Direction::NorthEast, pmWillBeCalled);
				break;
			case WALK_E:
				StartWalk(player, Direction::East, pmWillBeCalled);
				break;
			case WALK_SE:
				StartWalk(player, Direction::SouthEast, pmWillBeCalled);
				break;
			case WALK_S:
				StartWalk(player, Direction::South, pmWillBeCalled);
				break;
			case WALK_SW:
				StartWalk(player, Direction::SouthWest, pmWillBeCalled);
				break;
			case WALK_W:
				StartWalk(player, Direction::West, pmWillBeCalled);
				break;
			case WALK_NW:
				StartWalk(player, Direction::NorthWest, pmWillBeCalled);
				break;
			}

			for (size_t j = 1; j < MaxPathLengthPlayer; j++) {
				player.walkpath[j - 1] = player.walkpath[j];
			}

			player.walkpath[MaxPathLengthPlayer - 1] = WALK_NONE;

			if (player._pmode == PM_STAND) {
				player.startStand(player.direction);
				player.destAction = ACTION_NONE;
			}
		}

		return;
	}
	if (player.destAction == ACTION_NONE) {
		return;
	}

	if (player._pmode == PM_STAND) {
		switch (player.destAction) {
		case ACTION_ATTACK:
			d = GetDirection(player.position.tile, { player.destParam1, player.destParam2 });
			StartAttack(player, d, pmWillBeCalled);
			break;
		case ACTION_ATTACKMON:
			x = std::abs(player.position.tile.x - monster->position.future.x);
			y = std::abs(player.position.tile.y - monster->position.future.y);
			if (x <= 1 && y <= 1) {
				d = GetDirection(player.position.future, monster->position.future);
				if (monster->talkMsg != TEXT_NONE && monster->talkMsg != TEXT_VILE14) {
					TalktoMonster(player, *monster);
				} else {
					StartAttack(player, d, pmWillBeCalled);
				}
			}
			break;
		case ACTION_ATTACKPLR:
			x = std::abs(player.position.tile.x - target->position.future.x);
			y = std::abs(player.position.tile.y - target->position.future.y);
			if (x <= 1 && y <= 1) {
				d = GetDirection(player.position.future, target->position.future);
				StartAttack(player, d, pmWillBeCalled);
			}
			break;
		case ACTION_RATTACK:
			d = GetDirection(player.position.tile, { player.destParam1, player.destParam2 });
			StartRangeAttack(player, d, player.destParam1, player.destParam2, pmWillBeCalled);
			break;
		case ACTION_RATTACKMON:
			d = GetDirection(player.position.future, monster->position.future);
			if (monster->talkMsg != TEXT_NONE && monster->talkMsg != TEXT_VILE14) {
				TalktoMonster(player, *monster);
			} else {
				StartRangeAttack(player, d, monster->position.future.x, monster->position.future.y, pmWillBeCalled);
			}
			break;
		case ACTION_RATTACKPLR:
			d = GetDirection(player.position.future, target->position.future);
			StartRangeAttack(player, d, target->position.future.x, target->position.future.y, pmWillBeCalled);
			break;
		case ACTION_SPELL:
			d = GetDirection(player.position.tile, { player.destParam1, player.destParam2 });
			StartSpell(player, d, player.destParam1, player.destParam2);
			break;
		case ACTION_SPELLWALL:
			StartSpell(player, static_cast<Direction>(player.destParam3), player.destParam1, player.destParam2);
			player.tempDirection = static_cast<Direction>(player.destParam3);
			break;
		case ACTION_SPELLMON:
			d = GetDirection(player.position.tile, monster->position.future);
			StartSpell(player, d, monster->position.future.x, monster->position.future.y);
			break;
		case ACTION_SPELLPLR:
			d = GetDirection(player.position.tile, target->position.future);
			StartSpell(player, d, target->position.future.x, target->position.future.y);
			break;
		case ACTION_OPERATE:
			if (IsPlayerAdjacentToObject(player, *object)) {
				if (object->_oBreak == 1) {
					d = GetDirection(player.position.tile, object->position);
					StartAttack(player, d, pmWillBeCalled);
				} else {
					OperateObject(player, *object);
				}
			}
			break;
		case ACTION_DISARM:
			if (IsPlayerAdjacentToObject(player, *object)) {
				if (object->_oBreak == 1) {
					d = GetDirection(player.position.tile, object->position);
					StartAttack(player, d, pmWillBeCalled);
				} else {
				#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
					if (&player == MyPlayer && authoritative::GetServerBackedRuntime().IsConnected()) {
						const auto objectEntityId = authoritative::GetServerBackedRuntime().AuthoritativeObjectEntityIdAt(object->position.x, object->position.y);
						switch (ResolveServerBackedObjectInteractionRoute(
						    true,
						    true,
						    objectEntityId.has_value())) {
						case ServerBackedObjectInteractionRoute::Submit:
							if (auto result = authoritative::GetServerBackedRuntime().DisarmTrap(*objectEntityId, 0, GetMillisecondsSinceStartup()); !result.has_value())
								LogError("Server-backed path trap disarm failed: {}", result.error());
							break;
						case ServerBackedObjectInteractionRoute::FailClosed:
							LogError("Server-backed path object target is not an authoritative entity.");
							break;
						case ServerBackedObjectInteractionRoute::None:
							break;
						}
						break;
					}
				#endif
					TryDisarm(player, *object);
					OperateObject(player, *object);
				}
			}
			break;
		case ACTION_OPERATETK:
			if (object->_oBreak != 1) {
				OperateObject(player, *object);
			}
			break;
		case ACTION_PICKUPITEM:
			if (&player == MyPlayer) {
				x = std::abs(player.position.tile.x - item->position.x);
				y = std::abs(player.position.tile.y - item->position.y);
				if (x <= 1 && y <= 1 && pcurs == CURSOR_HAND && !item->_iRequest) {
				#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
					if (authoritative::GetServerBackedRuntime().IsConnected()) {
						const auto entityId = authoritative::GetServerBackedRuntime().WorldItemEntityIdAt(item->position.x, item->position.y);
						if (!entityId.has_value()) {
							LogError("Server-backed pickup target has no authoritative world-item entity.");
							break;
						}
						if (auto result = authoritative::GetServerBackedRuntime().PickupWorldItem(*entityId, 0, GetMillisecondsSinceStartup()); !result.has_value())
							LogError("Server-backed world-item pickup failed: {}", result.error());
						else
							item->_iRequest = true;
						break;
					}
				#endif
					NetSendCmdGItem(true, CMD_REQUESTGITEM, player, targetId);
					item->_iRequest = true;
				}
			}
			break;
		case ACTION_PICKUPAITEM:
			if (&player == MyPlayer) {
				x = std::abs(player.position.tile.x - item->position.x);
				y = std::abs(player.position.tile.y - item->position.y);
				if (x <= 1 && y <= 1 && pcurs == CURSOR_HAND) {
				#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
					if (authoritative::GetServerBackedRuntime().IsConnected()) {
						const auto entityId = authoritative::GetServerBackedRuntime().WorldItemEntityIdAt(item->position.x, item->position.y);
						if (!entityId.has_value()) {
							LogError("Server-backed automatic pickup target has no authoritative world-item entity.");
							break;
						}
						if (auto result = authoritative::GetServerBackedRuntime().PickupWorldItem(*entityId, 0, GetMillisecondsSinceStartup()); !result.has_value())
							LogError("Server-backed automatic world-item pickup failed: {}", result.error());
						else
							item->_iRequest = true;
						break;
					}
				#endif
					NetSendCmdGItem(true, CMD_REQUESTAGITEM, player, targetId);
				}
			}
			break;
		case ACTION_TALK:
			if (&player == MyPlayer) {
				HelpFlag = false;
				TalkToTowner(player, player.destParam1);
			}
			break;
		default:
			break;
		}

		player.fixLocation(player.direction);
		player.destAction = ACTION_NONE;

		return;
	}

	if (player._pmode == PM_ATTACK && player.animInfo.currentFrame >= player._pAFNum) {
		if (player.destAction == ACTION_ATTACK) {
			d = GetDirection(player.position.future, { player.destParam1, player.destParam2 });
			StartAttack(player, d, pmWillBeCalled);
			player.destAction = ACTION_NONE;
		} else if (player.destAction == ACTION_ATTACKMON) {
			x = std::abs(player.position.tile.x - monster->position.future.x);
			y = std::abs(player.position.tile.y - monster->position.future.y);
			if (x <= 1 && y <= 1) {
				d = GetDirection(player.position.future, monster->position.future);
				StartAttack(player, d, pmWillBeCalled);
			}
			player.destAction = ACTION_NONE;
		} else if (player.destAction == ACTION_ATTACKPLR) {
			x = std::abs(player.position.tile.x - target->position.future.x);
			y = std::abs(player.position.tile.y - target->position.future.y);
			if (x <= 1 && y <= 1) {
				d = GetDirection(player.position.future, target->position.future);
				StartAttack(player, d, pmWillBeCalled);
			}
			player.destAction = ACTION_NONE;
		} else if (player.destAction == ACTION_OPERATE) {
			if (IsPlayerAdjacentToObject(player, *object)) {
				if (object->_oBreak == 1) {
					d = GetDirection(player.position.tile, object->position);
					StartAttack(player, d, pmWillBeCalled);
				}
			}
		}
	}

	if (player._pmode == PM_RATTACK && player.animInfo.currentFrame >= player._pAFNum) {
		if (player.destAction == ACTION_RATTACK) {
			d = GetDirection(player.position.tile, { player.destParam1, player.destParam2 });
			StartRangeAttack(player, d, player.destParam1, player.destParam2, pmWillBeCalled);
			player.destAction = ACTION_NONE;
		} else if (player.destAction == ACTION_RATTACKMON) {
			d = GetDirection(player.position.tile, monster->position.future);
			StartRangeAttack(player, d, monster->position.future.x, monster->position.future.y, pmWillBeCalled);
			player.destAction = ACTION_NONE;
		} else if (player.destAction == ACTION_RATTACKPLR) {
			d = GetDirection(player.position.tile, target->position.future);
			StartRangeAttack(player, d, target->position.future.x, target->position.future.y, pmWillBeCalled);
			player.destAction = ACTION_NONE;
		}
	}

	if (player._pmode == PM_SPELL && player.animInfo.currentFrame >= player._pSFNum) {
		if (player.destAction == ACTION_SPELL) {
			d = GetDirection(player.position.tile, { player.destParam1, player.destParam2 });
			StartSpell(player, d, player.destParam1, player.destParam2);
			player.destAction = ACTION_NONE;
		} else if (player.destAction == ACTION_SPELLMON) {
			d = GetDirection(player.position.tile, monster->position.future);
			StartSpell(player, d, monster->position.future.x, monster->position.future.y);
			player.destAction = ACTION_NONE;
		} else if (player.destAction == ACTION_SPELLPLR) {
			d = GetDirection(player.position.tile, target->position.future);
			StartSpell(player, d, target->position.future.x, target->position.future.y);
			player.destAction = ACTION_NONE;
		}
	}
}

bool PlrDeathModeOK(Player &player)
{
	if (&player != MyPlayer) {
		return true;
	}
	if (player._pmode == PM_DEATH) {
		return true;
	}
	if (player._pmode == PM_QUIT) {
		return true;
	}
	if (player._pmode == PM_NEWLVL) {
		return true;
	}

	return false;
}

void ValidatePlayer()
{
	assert(MyPlayer != nullptr);
	Player &myPlayer = *MyPlayer;

	// Player::setCharacterLevel ensures that the player level is within the expected range in case someone has edited their character level in memory
	myPlayer.setCharacterLevel(myPlayer.getCharacterLevel());
	// This lets us catch cases where someone is editing experience directly through memory modification and reset their experience back to the expected cap.
	if (myPlayer._pExperience > myPlayer.getNextExperienceThreshold()) {
		myPlayer._pExperience = myPlayer.getNextExperienceThreshold();
		if (*GetOptions().Gameplay.experienceBar) {
			RedrawEverything();
		}
	}

	int gt = 0;
	for (int i = 0; i < myPlayer._pNumInv; i++) {
		if (myPlayer.InvList[i]._itype == ItemType::Gold) {
			int maxGold = GOLD_MAX_LIMIT;
			if (gbIsHellfire) {
				maxGold *= 2;
			}
			if (myPlayer.InvList[i]._ivalue > maxGold) {
				myPlayer.InvList[i]._ivalue = maxGold;
			}
			gt += myPlayer.InvList[i]._ivalue;
		}
	}
	if (gt != myPlayer._pGold)
		myPlayer._pGold = gt;

	if (myPlayer.attributes.strength.base > myPlayer.GetMaximumAttributeValue(CharacterAttribute::Strength)) {
		myPlayer.attributes.strength.base = myPlayer.GetMaximumAttributeValue(CharacterAttribute::Strength);
	}
	if (myPlayer.attributes.magic.base > myPlayer.GetMaximumAttributeValue(CharacterAttribute::Magic)) {
		myPlayer.attributes.magic.base = myPlayer.GetMaximumAttributeValue(CharacterAttribute::Magic);
	}
	if (myPlayer.attributes.dexterity.base > myPlayer.GetMaximumAttributeValue(CharacterAttribute::Dexterity)) {
		myPlayer.attributes.dexterity.base = myPlayer.GetMaximumAttributeValue(CharacterAttribute::Dexterity);
	}
	if (myPlayer.attributes.vitality.base > myPlayer.GetMaximumAttributeValue(CharacterAttribute::Vitality)) {
		myPlayer.attributes.vitality.base = myPlayer.GetMaximumAttributeValue(CharacterAttribute::Vitality);
	}

	uint64_t msk = 0;
	for (auto b = static_cast<size_t>(SpellID::Firebolt); b < SpellsData.size(); b++) {
		if (GetSpellBookLevel((SpellID)b) != -1) {
			msk |= GetSpellBitmask(static_cast<SpellID>(b));
			if (myPlayer._pSplLvl[b] > MaxSpellLevel)
				myPlayer._pSplLvl[b] = MaxSpellLevel;
		}
	}

	myPlayer._pMemSpells &= msk;
	myPlayer._pInfraFlag = false;
}

HeroClass GetPlayerSpriteClass(HeroClass cls)
{
	if (cls == HeroClass::Bard && !HaveBardAssets())
		return HeroClass::Rogue;
	if (cls == HeroClass::Barbarian && !HaveBarbarianAssets())
		return HeroClass::Warrior;
	return cls;
}

PlayerWeaponGraphic GetPlayerWeaponGraphic(player_graphic graphic, PlayerWeaponGraphic weaponGraphic)
{
	if (levelType() == DTYPE_TOWN && IsAnyOf(graphic, player_graphic::Lightning, player_graphic::Fire, player_graphic::Magic)) {
		// If the hero doesn't hold the weapon in town then we should use the unarmed animation for casting
		switch (weaponGraphic) {
		case PlayerWeaponGraphic::Mace:
		case PlayerWeaponGraphic::Sword:
			return PlayerWeaponGraphic::Unarmed;
		case PlayerWeaponGraphic::SwordShield:
		case PlayerWeaponGraphic::MaceShield:
			return PlayerWeaponGraphic::UnarmedShield;
		default:
			break;
		}
	}
	return weaponGraphic;
}

uint16_t GetPlayerSpriteWidth(HeroClass cls, player_graphic graphic, PlayerWeaponGraphic weaponGraphic)
{
	const PlayerSpriteData spriteData = GetPlayerSpriteDataForClass(cls);

	switch (graphic) {
	case player_graphic::Stand:
		return spriteData.stand;
	case player_graphic::Walk:
		return spriteData.walk;
	case player_graphic::Attack:
		if (weaponGraphic == PlayerWeaponGraphic::Bow)
			return spriteData.bow;
		return spriteData.attack;
	case player_graphic::Hit:
		return spriteData.swHit;
	case player_graphic::Block:
		return spriteData.block;
	case player_graphic::Lightning:
		return spriteData.lightning;
	case player_graphic::Fire:
		return spriteData.fire;
	case player_graphic::Magic:
		return spriteData.magic;
	case player_graphic::Death:
		return spriteData.death;
	}
	app_fatal("Invalid player_graphic");
}

void GetPlayerGraphicsPath(std::string_view path, std::string_view prefix, std::string_view type, char out[256])
{
	*BufCopy(out, "plrgfx\\", path, "\\", prefix, "\\", prefix, type) = '\0';
}

} // namespace

void Player::CalcScrolls()
{
	_pScrlSpells = 0;
	for (const Item &item : InventoryAndBeltPlayerItemsRange { *this }) {
		if (item.isScroll() && item._iStatFlag) {
			_pScrlSpells |= GetSpellBitmask(item._iSpell);
		}
	}
	EnsureValidReadiedSpell(*this);
}

bool Player::CanUseItem(const Item &item) const
{
	if (!IsItemValid(*this, item))
		return false;

	return attributes.strength.current >= item._iMinStr
	    && attributes.magic.current >= item._iMinMag
	    && attributes.dexterity.current >= item._iMinDex;
}

bool Player::CanCleave()
{
	switch (_pClass) {
	case HeroClass::Warrior:
	case HeroClass::Rogue:
	case HeroClass::Sorcerer:
		return false;
	case HeroClass::Monk:
		return isEquipped(ItemType::Staff);
	case HeroClass::Bard:
		return InvBody[INVLOC_HAND_LEFT]._itype == ItemType::Sword && InvBody[INVLOC_HAND_RIGHT]._itype == ItemType::Sword;
	case HeroClass::Barbarian:
		return isEquipped(ItemType::Axe) || (!isEquipped(ItemType::Shield) && (isEquipped(ItemType::Mace, true) || isEquipped(ItemType::Sword, true)));
	default:
		return false;
	}
}

bool Player::isEquipped(ItemType itemType, bool isTwoHanded)
{
	switch (itemType) {
	case ItemType::Sword:
	case ItemType::Axe:
	case ItemType::Bow:
	case ItemType::Mace:
	case ItemType::Shield:
	case ItemType::Staff:
		return (InvBody[INVLOC_HAND_LEFT]._itype == itemType && (!isTwoHanded || InvBody[INVLOC_HAND_LEFT]._iLoc == ILOC_TWOHAND))
		    || (InvBody[INVLOC_HAND_RIGHT]._itype == itemType && (!isTwoHanded || InvBody[INVLOC_HAND_LEFT]._iLoc == ILOC_TWOHAND));
	case ItemType::LightArmor:
	case ItemType::MediumArmor:
	case ItemType::HeavyArmor:
		return InvBody[INVLOC_CHEST]._itype == itemType;
	case ItemType::Helm:
		return InvBody[INVLOC_HEAD]._itype == itemType;
	case ItemType::Ring:
		return InvBody[INVLOC_RING_LEFT]._itype == itemType || InvBody[INVLOC_RING_RIGHT]._itype == itemType;
	case ItemType::Amulet:
		return InvBody[INVLOC_AMULET]._itype == itemType;
	default:
		return false;
	}
}

item_equip_type Player::GetItemLocation(const Item &item) const
{
	if (_pClass == HeroClass::Barbarian && item._iLoc == ILOC_TWOHAND && IsAnyOf(item._itype, ItemType::Sword, ItemType::Mace))
		return ILOC_ONEHAND;
	return item._iLoc;
}

int Player::GetArmor() const
{
	return _pIBonusAC + _pIAC + attributes.dexterity.current / 5;
}

int Player::GetMeleeToHit() const
{
	return getCharacterLevel() + attributes.dexterity.current / 2 + _pIBonusToHit + getPlayerCombatData().baseMeleeToHit;
}

int Player::GetMeleePiercingToHit() const
{
	int hper = GetMeleeToHit();
	if (!gbIsHellfire)
		hper += damageBonuses.armorPiercing;
	return hper;
}

int Player::GetRangedToHit() const
{
	return getCharacterLevel() + attributes.dexterity.current + _pIBonusToHit + getPlayerCombatData().baseRangedToHit;
}

int Player::GetRangedPiercingToHit() const
{
	int hper = GetRangedToHit();
	if (!gbIsHellfire)
		hper += damageBonuses.armorPiercing;
	return hper;
}

int Player::GetMagicToHit() const
{
	return attributes.magic.current + getPlayerCombatData().baseMagicToHit;
}

int Player::GetBlockChance(bool useLevel) const
{
	int blkper = attributes.dexterity.current + getBaseToBlock();
	if (useLevel)
		blkper += getCharacterLevel() * 2;
	return blkper;
}

int Player::GetSpellLevel(SpellID spell) const
{
	if (spell == SpellID::Invalid || static_cast<std::size_t>(spell) >= sizeof(_pSplLvl)) {
		return 0;
	}

	return std::max<int>(_pISplLvlAdd + _pSplLvl[static_cast<std::size_t>(spell)], 0);
}

int Player::CalculateArmorPierce(int monsterArmor, bool isMelee) const
{
	int tmac = monsterArmor;
	if (damageBonuses.armorPiercing > 0) {
		if (gbIsHellfire) {
			int armorPiercingShift = damageBonuses.armorPiercing - 1;
			if (armorPiercingShift > 0)
				tmac >>= armorPiercingShift;
			else
				tmac -= tmac / 4;
		}
		if (isMelee && _pClass == HeroClass::Barbarian) {
			tmac -= monsterArmor / 8;
		}
	}
	if (tmac < 0)
		tmac = 0;

	return tmac;
}

int Player::UpdateHitPointPercentage()
{
	if (life.maximum <= 0) {
		life.percentage = 0;
	} else {
		life.percentage = std::clamp(life.current * 81 / life.maximum, 0, 81);
	}

	return life.percentage;
}

int Player::UpdateManaPercentage()
{
	if (mana.maximum <= 0) {
		mana.percentage = 0;
	} else {
		mana.percentage = std::clamp(mana.current * 81 / mana.maximum, 0, 81);
	}

	return mana.percentage;
}

void Player::RemoveInvItem(int iv, bool calcScrolls)
{
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacyInventoryMutation(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
	#endif

	if (this == MyPlayer) {
		// Locate the first grid index containing this item and notify remote clients
		for (size_t i = 0; i < InventoryGridCells; i++) {
			const int8_t itemIndex = InvGrid[i];
			if (std::abs(itemIndex) - 1 == iv) {
				NetSendCmdParam1(false, CMD_DELINVITEMS, static_cast<uint16_t>(i));
				break;
			}
		}
	}

	// Iterate through invGrid and remove every reference to item
	for (int8_t &itemIndex : InvGrid) {
		if (std::abs(itemIndex) - 1 == iv) {
			itemIndex = 0;
		}
	}

	InvList[iv].clear();

	_pNumInv--;

	// If the item at the end of inventory array isn't the one removed, shift all following items back one index to retain inventory order.
	if (_pNumInv > 0 && _pNumInv != iv) {
		for (int newIndex = iv; newIndex < _pNumInv; newIndex++) {
			InvList[newIndex] = InvList[newIndex + 1].pop();
		}

		for (int8_t &itemIndex : InvGrid) {
			if (itemIndex > iv + 1) { // if item was shifted, decrease the index so it's paired with the correct item.
				itemIndex--;
			}
			if (itemIndex < -(iv + 1)) {
				itemIndex++; // since occupied cells are negative, increment the index to keep it same as as top-left cell for item, only negative.
			}
		}
	}

	if (calcScrolls) {
		CalcScrolls();
	}
}

void Player::RemoveSpdBarItem(int iv)
{
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacyInventoryMutation(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
	#endif

	if (this == MyPlayer) {
		NetSendCmdParam1(false, CMD_DELBELTITEMS, iv);
	}

	SpdList[iv].clear();

	CalcScrolls();
	RedrawEverything();
}

[[nodiscard]] uint8_t Player::getId() const
{
	return static_cast<uint8_t>(std::distance<const Player *>(&Players[0], this));
}

int Player::GetBaseAttributeValue(CharacterAttribute attribute) const
{
	switch (attribute) {
	case CharacterAttribute::Dexterity:
		return this->attributes.dexterity.base;
	case CharacterAttribute::Magic:
		return this->attributes.magic.base;
	case CharacterAttribute::Strength:
		return this->attributes.strength.base;
	case CharacterAttribute::Vitality:
		return this->attributes.vitality.base;
	default:
		app_fatal("Unsupported attribute");
	}
}

int Player::GetCurrentAttributeValue(CharacterAttribute attribute) const
{
	switch (attribute) {
	case CharacterAttribute::Dexterity:
		return this->attributes.dexterity.current;
	case CharacterAttribute::Magic:
		return this->attributes.magic.current;
	case CharacterAttribute::Strength:
		return this->attributes.strength.current;
	case CharacterAttribute::Vitality:
		return this->attributes.vitality.current;
	default:
		app_fatal("Unsupported attribute");
	}
}

int Player::GetMaximumAttributeValue(CharacterAttribute attribute) const
{
	const ClassAttributes &attr = getClassAttributes();
	switch (attribute) {
	case CharacterAttribute::Strength:
		return attr.maxStr;
	case CharacterAttribute::Magic:
		return attr.maxMag;
	case CharacterAttribute::Dexterity:
		return attr.maxDex;
	case CharacterAttribute::Vitality:
		return attr.maxVit;
	}
	app_fatal("Unsupported attribute");
}

Point Player::GetTargetPosition() const
{
	// clang-format off
	constexpr int DirectionOffsetX[8] = {  0,-1, 1, 0,-1, 1, 1,-1 };
	constexpr int DirectionOffsetY[8] = { -1, 0, 0, 1,-1,-1, 1, 1 };
	// clang-format on
	Point target = position.future;
	for (auto step : walkpath) {
		if (step == WALK_NONE)
			break;
		if (step > 0) {
			target.x += DirectionOffsetX[step - 1];
			target.y += DirectionOffsetY[step - 1];
		}
	}
	return target;
}

int Player::GetPositionPathIndex(Point pos)
{
	constexpr Displacement DirectionOffset[8] = { { 0, -1 }, { -1, 0 }, { 1, 0 }, { 0, 1 }, { -1, -1 }, { 1, -1 }, { 1, 1 }, { -1, 1 } };
	Point target = position.future;
	int i = 0;
	for (auto step : walkpath) {
		if (target == pos) return i;
		if (step == WALK_NONE)
			break;
		if (step > 0) {
			target += DirectionOffset[step - 1];
		}
		++i;
	}
	return -1;
}

void Player::Say(HeroSpeech speechId) const
{
	const SfxID soundEffect = GetHeroSound(_pClass, speechId);

	if (soundEffect == SfxID::None)
		return;

	PlaySfxLoc(soundEffect, position.tile);
}

void Player::SaySpecific(HeroSpeech speechId) const
{
	const SfxID soundEffect = GetHeroSound(_pClass, speechId);

	if (soundEffect == SfxID::None || effect_is_playing(soundEffect))
		return;

	PlaySfxLoc(soundEffect, position.tile, false);
}

void Player::Say(HeroSpeech speechId, int delay) const
{
	sfxdelay = delay;
	sfxdnum = GetHeroSound(_pClass, speechId);
}

void Player::Stop()
{
	this->clearPath();
	destAction = ACTION_NONE;
}

bool Player::isWalking() const
{
	return IsAnyOf(_pmode, PM_WALK_NORTHWARDS, PM_WALK_SOUTHWARDS, PM_WALK_SIDEWAYS);
}

int Player::GetManaShieldDamageReduction()
{
	constexpr uint8_t Max = 7;
	return 24 - (std::min(_pSplLvl[static_cast<int8_t>(SpellID::ManaShield)], Max) * 3);
}

void Player::RestoreFullLife()
{
	if (!LegacyPlayerStateMutationAllowed())
		return;

	life.current = life.maximum;
	life.base = life.maximumBase;
}

void Player::RestorePartialLife()
{
	if (!LegacyPlayerStateMutationAllowed())
		return;

	const int wholeHitpoints = life.maximum >> 6;
	int l = ((wholeHitpoints / 8) + GenerateRnd(wholeHitpoints / 4)) << 6;
	if (IsAnyOf(_pClass, HeroClass::Warrior, HeroClass::Barbarian))
		l *= 2;
	if (IsAnyOf(_pClass, HeroClass::Rogue, HeroClass::Monk, HeroClass::Bard))
		l += l / 2;
	life.current = std::min(life.current + l, life.maximum);
	life.base = std::min(life.base + l, life.maximumBase);
}

void Player::RestoreFullMana()
{
	if (!LegacyPlayerStateMutationAllowed())
		return;

	if (HasNoneOf(_pIFlags, ItemSpecialEffect::NoMana)) {
		mana.current = mana.maximum;
		mana.base = mana.maximumBase;
	}
}

void Player::RestorePartialMana()
{
	if (!LegacyPlayerStateMutationAllowed())
		return;

	const int wholeManaPoints = mana.maximum >> 6;
	int l = ((wholeManaPoints / 8) + GenerateRnd(wholeManaPoints / 4)) << 6;
	if (_pClass == HeroClass::Sorcerer)
		l *= 2;
	if (IsAnyOf(_pClass, HeroClass::Rogue, HeroClass::Monk, HeroClass::Bard))
		l += l / 2;
	if (HasNoneOf(_pIFlags, ItemSpecialEffect::NoMana)) {
		mana.current = std::min(mana.current + l, mana.maximum);
		mana.base = std::min(mana.base + l, mana.maximumBase);
	}
}

bool Player::UsesRangedWeapon() const
{
	return static_cast<PlayerWeaponGraphic>(_pgfxnum & 0xF) == PlayerWeaponGraphic::Bow;
}

bool Player::CanChangeAction()
{
	if (_pmode == PM_STAND)
		return true;
	if (_pmode == PM_ATTACK && animInfo.currentFrame >= _pAFNum)
		return true;
	if (_pmode == PM_RATTACK && animInfo.currentFrame >= _pAFNum)
		return true;
	if (_pmode == PM_SPELL && animInfo.currentFrame >= _pSFNum)
		return true;
	if (isWalking() && animInfo.isLastFrame())
		return true;
	return false;
}

void Player::ReadySpellFromEquipment(inv_body_loc bodyLocation, bool forceSpell)
{
	const Item &item = InvBody[bodyLocation];
	if (item._itype == ItemType::Staff && IsValidSpell(item._iSpell) && item._iCharges > 0 && item._iStatFlag) {
		if (forceSpell || _pRSpell == SpellID::Invalid || _pRSplType == SpellType::Invalid) {
			_pRSpell = item._iSpell;
			_pRSplType = SpellType::Charges;
			RedrawEverything();
		}
	}
}

player_graphic Player::getGraphic() const
{
	switch (_pmode) {
	case PM_STAND:
	case PM_NEWLVL:
	case PM_QUIT:
		return player_graphic::Stand;
	case PM_WALK_NORTHWARDS:
	case PM_WALK_SOUTHWARDS:
	case PM_WALK_SIDEWAYS:
		return player_graphic::Walk;
	case PM_ATTACK:
	case PM_RATTACK:
		return player_graphic::Attack;
	case PM_BLOCK:
		return player_graphic::Block;
	case PM_SPELL:
		return GetPlayerGraphicForSpell(executedSpell.spellId);
	case PM_GOTHIT:
		return player_graphic::Hit;
	case PM_DEATH:
		return player_graphic::Death;
	default:
		app_fatal("SyncPlrAnim");
	}
}

uint16_t Player::getSpriteWidth() const
{
	if (!HeadlessMode)
		return (*animInfo.sprites)[0].width();
	const player_graphic graphic = getGraphic();
	const HeroClass cls = GetPlayerSpriteClass(_pClass);
	const PlayerWeaponGraphic weaponGraphic = GetPlayerWeaponGraphic(graphic, static_cast<PlayerWeaponGraphic>(_pgfxnum & 0xF));
	return GetPlayerSpriteWidth(cls, graphic, weaponGraphic);
}

void Player::getAnimationFramesAndTicksPerFrame(player_graphic graphics, int8_t &numberOfFrames, int8_t &ticksPerFrame) const
{
	ticksPerFrame = 1;
	switch (graphics) {
	case player_graphic::Stand:
		numberOfFrames = _pNFrames;
		ticksPerFrame = 4;
		break;
	case player_graphic::Walk:
		numberOfFrames = _pWFrames;
		break;
	case player_graphic::Attack:
		numberOfFrames = _pAFrames;
		break;
	case player_graphic::Hit:
		numberOfFrames = _pHFrames;
		break;
	case player_graphic::Lightning:
	case player_graphic::Fire:
	case player_graphic::Magic:
		numberOfFrames = _pSFrames;
		break;
	case player_graphic::Death:
		numberOfFrames = _pDFrames;
		ticksPerFrame = 2;
		break;
	case player_graphic::Block:
		numberOfFrames = _pBFrames;
		ticksPerFrame = 3;
		break;
	default:
		app_fatal("Unknown player graphics");
	}
}

void Player::UpdatePreviewCelSprite(_cmd_id cmdId, Point point, uint16_t wParam1, uint16_t wParam2)
{
	// if game is not running don't show a preview
	if (!gbRunGame || PauseMode != 0 || !gbProcessPlayers)
		return;

	// we can only show a preview if our command is executed in the next game tick
	if (_pmode != PM_STAND)
		return;

	std::optional<player_graphic> graphic;
	Direction dir = Direction::South;
	int minimalWalkDistance = -1;

	switch (cmdId) {
	case _cmd_id::CMD_RATTACKID: {
		const Monster &monster = Monsters[wParam1];
		dir = GetDirection(position.future, monster.position.future);
		graphic = player_graphic::Attack;
		break;
	}
	case _cmd_id::CMD_SPELLID: {
		const Monster &monster = Monsters[wParam1];
		dir = GetDirection(position.future, monster.position.future);
		graphic = GetPlayerGraphicForSpell(static_cast<SpellID>(wParam2));
		break;
	}
	case _cmd_id::CMD_ATTACKID: {
		const Monster &monster = Monsters[wParam1];
		point = monster.position.future;
		minimalWalkDistance = 2;
		if (!monster.canTalk()) {
			dir = GetDirection(position.future, monster.position.future);
			graphic = player_graphic::Attack;
		}
		break;
	}
	case _cmd_id::CMD_RATTACKPID: {
		const Player &targetPlayer = Players[wParam1];
		dir = GetDirection(position.future, targetPlayer.position.future);
		graphic = player_graphic::Attack;
		break;
	}
	case _cmd_id::CMD_SPELLPID: {
		const Player &targetPlayer = Players[wParam1];
		dir = GetDirection(position.future, targetPlayer.position.future);
		graphic = GetPlayerGraphicForSpell(static_cast<SpellID>(wParam2));
		break;
	}
	case _cmd_id::CMD_ATTACKPID: {
		const Player &targetPlayer = Players[wParam1];
		point = targetPlayer.position.future;
		minimalWalkDistance = 2;
		dir = GetDirection(position.future, targetPlayer.position.future);
		graphic = player_graphic::Attack;
		break;
	}
	case _cmd_id::CMD_RATTACKXY:
	case _cmd_id::CMD_SATTACKXY:
		dir = GetDirection(position.tile, point);
		graphic = player_graphic::Attack;
		break;
	case _cmd_id::CMD_SPELLXY:
		dir = GetDirection(position.tile, point);
		graphic = GetPlayerGraphicForSpell(static_cast<SpellID>(wParam1));
		break;
	case _cmd_id::CMD_SPELLXYD:
		dir = static_cast<Direction>(wParam2);
		graphic = GetPlayerGraphicForSpell(static_cast<SpellID>(wParam1));
		break;
	case _cmd_id::CMD_WALKXY:
		minimalWalkDistance = 1;
		break;
	case _cmd_id::CMD_TALKXY:
	case _cmd_id::CMD_DISARMXY:
	case _cmd_id::CMD_OPOBJXY:
	case _cmd_id::CMD_GOTOGETITEM:
	case _cmd_id::CMD_GOTOAGETITEM:
		minimalWalkDistance = 2;
		break;
	default:
		return;
	}

	if (minimalWalkDistance >= 0 && position.future != point) {
		int8_t testWalkPath[MaxPathLengthPlayer];
		const int steps = FindPath(CanStep, [this](Point tile) { return this->positionIsAvailable(tile); }, position.future, point, testWalkPath, MaxPathLengthPlayer);
		if (steps == 0) {
			// Can't walk to desired location => stand still
			return;
		}
		if (steps >= minimalWalkDistance) {
			graphic = player_graphic::Walk;
			switch (testWalkPath[0]) {
			case WALK_N:
				dir = Direction::North;
				break;
			case WALK_NE:
				dir = Direction::NorthEast;
				break;
			case WALK_E:
				dir = Direction::East;
				break;
			case WALK_SE:
				dir = Direction::SouthEast;
				break;
			case WALK_S:
				dir = Direction::South;
				break;
			case WALK_SW:
				dir = Direction::SouthWest;
				break;
			case WALK_W:
				dir = Direction::West;
				break;
			case WALK_NW:
				dir = Direction::NorthWest;
				break;
			}
			if (!PlrDirOK(*this, dir))
				return;
		}
	}

	if (!graphic || HeadlessMode)
		return;

	this->loadGraphic(*graphic);
	const ClxSpriteList sprites = AnimationData[static_cast<size_t>(*graphic)].spritesForDirection(dir);
	if (!previewCelSprite || *previewCelSprite != sprites[0]) {
		previewCelSprite = sprites[0];
		progressToNextGameTickWhenPreviewWasSet = ProgressToNextGameTick;
	}
}

void Player::setCharacterLevel(uint8_t level)
{
	this->_pLevel = std::clamp<uint8_t>(level, 1U, getMaxCharacterLevel());
}

uint8_t Player::getMaxCharacterLevel() const
{
	return GetMaximumCharacterLevel();
}

uint32_t Player::getNextExperienceThreshold() const
{
	return GetNextExperienceThresholdForLevel(this->getCharacterLevel());
}

int32_t Player::calculateBaseLife() const
{
	const ClassAttributes &attr = getClassAttributes();
	return attr.adjLife + (attr.lvlLife * getCharacterLevel()) + (attr.chrLife * attributes.vitality.base);
}

int32_t Player::calculateBaseMana() const
{
	const ClassAttributes &attr = getClassAttributes();
	return attr.adjMana + (attr.lvlMana * getCharacterLevel()) + (attr.chrMana * attributes.magic.base);
}

void Player::occupyTile(Point tilePosition, bool isMoving) const
{
	int16_t id = this->getId();
	id += 1;
	tileAt(tilePosition).setPlayer(isMoving ? -id : id);
}

bool Player::isLevelOwnedByLocalClient() const
{
	for (const Player &other : Players) {
		if (!other.plractive)
			continue;
		if (other._pLvlChanging)
			continue;
		if (other._pmode == PM_NEWLVL)
			continue;
		if (other.plrlevel != this->plrlevel)
			continue;
		if (other.plrIsOnSetLevel != this->plrIsOnSetLevel)
			continue;
		if (&other == MyPlayer && gbBufferMsgs != 0)
			continue;
		return &other == MyPlayer;
	}

	return false;
}

Player *PlayerAtPosition(Point position, bool ignoreMovingPlayers /*= false*/)
{
	if (!InDungeonBounds(position))
		return nullptr;

	auto playerIndex = tileAt(position).player();
	if (playerIndex == 0 || (ignoreMovingPlayers && playerIndex < 0))
		return nullptr;

	return &Players[std::abs(playerIndex) - 1];
}

ClxSprite Player::getPortraitSprite() const
{
	const Player &player = *this;
	const bool inDungeon = (player.plrlevel != 0);

	const HeroClass cls = GetPlayerSpriteClass(player._pClass);
	const PlayerWeaponGraphic animWeaponId = GetPlayerWeaponGraphic(player_graphic::Stand, static_cast<PlayerWeaponGraphic>(player._pgfxnum & 0xF));

	const PlayerSpriteData &spriteData = GetPlayerSpriteDataForClass(cls);
	const char *path = spriteData.classPath.c_str();

	std::string_view szCel = inDungeon ? "as" : "st";

	player_graphic graphic = player_graphic::Stand;
	if (player.hasNoLife()) {
		if (animWeaponId == PlayerWeaponGraphic::Unarmed) {
			szCel = "dt";
			graphic = player_graphic::Death;
		}
	}

	const char prefixBuf[3] = { spriteData.classChar, ArmourChar[player._pgfxnum >> 4], WepChar[static_cast<std::size_t>(animWeaponId)] };
	char pszName[256];
	GetPlayerGraphicsPath(path, std::string_view(prefixBuf, 3), szCel, pszName);

	const std::string spritePath { pszName };
	// Check to see if the sprite has updated.
	if (player.PartyInfoSpriteLocations[inDungeon] != spritePath) {
		// The sprite has changed so store the new location
		player.PartyInfoSpriteLocations[inDungeon] = spritePath;

		player.PartyInfoSprites[inDungeon] = std::nullopt;

		// And now load the new sprite and store it
		const uint16_t animationWidth = GetPlayerSpriteWidth(cls, graphic, animWeaponId);
		player.PartyInfoSprites[inDungeon] = LoadCl2Sheet(pszName, animationWidth);
	}

	const ClxSpriteList spriteList = (*player.PartyInfoSprites[inDungeon])[static_cast<size_t>(Direction::South)];
	return spriteList[(graphic == player_graphic::Stand) ? 0 : spriteList.numSprites() - 1];
}

bool Player::isUnarmed() const
{
	const Player &player = *this;
	const PlayerWeaponGraphic animWeaponId = GetPlayerWeaponGraphic(player_graphic::Stand, static_cast<PlayerWeaponGraphic>(player._pgfxnum & 0xF));
	return animWeaponId == PlayerWeaponGraphic::Unarmed;
}

void Player::loadGraphic(player_graphic graphic)
{
	Player &player = *this;
	if (HeadlessMode)
		return;

	auto &animationData = player.AnimationData[static_cast<size_t>(graphic)];
	if (animationData.sprites)
		return;

	const HeroClass cls = GetPlayerSpriteClass(player._pClass);
	PlayerWeaponGraphic animWeaponId = GetPlayerWeaponGraphic(graphic, static_cast<PlayerWeaponGraphic>(player._pgfxnum & 0xF));

	const PlayerSpriteData &spriteData = GetPlayerSpriteDataForClass(cls);
	const char *path = spriteData.classPath.c_str();

	std::string_view szCel;
	switch (graphic) {
	case player_graphic::Stand:
		szCel = "as";
		if (levelType() == DTYPE_TOWN)
			szCel = "st";
		break;
	case player_graphic::Walk:
		szCel = "aw";
		if (levelType() == DTYPE_TOWN)
			szCel = "wl";
		break;
	case player_graphic::Attack:
		if (levelType() == DTYPE_TOWN)
			return;
		szCel = "at";
		break;
	case player_graphic::Hit:
		if (levelType() == DTYPE_TOWN)
			return;
		szCel = "ht";
		break;
	case player_graphic::Lightning:
		szCel = "lm";
		break;
	case player_graphic::Fire:
		szCel = "fm";
		break;
	case player_graphic::Magic:
		szCel = "qm";
		break;
	case player_graphic::Death:
		// Only one Death animation exists, for unarmed characters
		animWeaponId = PlayerWeaponGraphic::Unarmed;
		szCel = "dt";
		break;
	case player_graphic::Block:
		if (levelType() == DTYPE_TOWN)
			return;
		if (!player._pBlockFlag)
			return;
		szCel = "bl";
		break;
	default:
		app_fatal("PLR:2");
	}

	const char prefixBuf[3] = { spriteData.classChar, ArmourChar[player._pgfxnum >> 4], WepChar[static_cast<std::size_t>(animWeaponId)] };
	char pszName[256];
	GetPlayerGraphicsPath(path, std::string_view(prefixBuf, 3), szCel, pszName);
	const uint16_t animationWidth = GetPlayerSpriteWidth(cls, graphic, animWeaponId);
	animationData.sprites = LoadCl2Sheet(pszName, animationWidth);
	std::optional<std::array<uint8_t, 256>> graphicTRN = GetPlayerGraphicTRN(pszName);
	if (graphicTRN) {
		ClxApplyTrans(*animationData.sprites, graphicTRN->data());
	}
	std::optional<std::array<uint8_t, 256>> classTRN = GetClassTRN(player);
	if (classTRN) {
		ClxApplyTrans(*animationData.sprites, classTRN->data());
	}
}

void Player::initGraphics()
{
	Player &player = *this;
	if (HeadlessMode)
		return;

	player.resetGraphics();

	if (player.hasNoLife()) {
		player._pgfxnum &= ~0xFU;
		player.loadGraphic(player_graphic::Death);
		return;
	}

	for (size_t i = 0; i < enum_size<player_graphic>::value; i++) {
		auto graphic = static_cast<player_graphic>(i);
		if (graphic == player_graphic::Death)
			continue;
		player.loadGraphic(graphic);
	}
}

void Player::resetGraphics()
{
	Player &player = *this;
	player.animInfo.sprites = std::nullopt;

	if (!gbRunGame) {
		player.PartyInfoSprites[0] = std::nullopt;
		player.PartyInfoSprites[1] = std::nullopt;
	}

	for (PlayerAnimationData &animData : player.AnimationData) {
		animData.sprites = std::nullopt;
	}
}

void Player::setAnimation(player_graphic graphic, Direction dir, AnimationDistributionFlags flags, int8_t numSkippedFrames, int8_t distributeFramesBeforeFrame)
{
	Player &player = *this;
	player.loadGraphic(graphic);

	OptionalClxSpriteList sprites;
	int previewShownGameTickFragments = 0;
	if (!HeadlessMode) {
		sprites = player.AnimationData[static_cast<size_t>(graphic)].spritesForDirection(dir);
		if (player.previewCelSprite && (*sprites)[0] == *player.previewCelSprite && !player.isWalking()) {
			previewShownGameTickFragments = std::clamp<int>(AnimationInfo::baseValueFraction - player.progressToNextGameTickWhenPreviewWasSet, 0, AnimationInfo::baseValueFraction);
		}
	}

	int8_t numberOfFrames;
	int8_t ticksPerFrame;
	player.getAnimationFramesAndTicksPerFrame(graphic, numberOfFrames, ticksPerFrame);
	player.animInfo.setNewAnimation(sprites, numberOfFrames, ticksPerFrame, flags, numSkippedFrames, distributeFramesBeforeFrame, static_cast<uint8_t>(previewShownGameTickFragments));
}

void Player::setAnimations()
{
	Player &player = *this;
	const HeroClass pc = player._pClass;
	const PlayerAnimData &plrAtkAnimData = GetPlayerAnimDataForClass(pc);
	auto gn = static_cast<PlayerWeaponGraphic>(player._pgfxnum & 0xFU);

	if (levelType() == DTYPE_TOWN) {
		player._pNFrames = plrAtkAnimData.townIdleFrames;
		player._pWFrames = plrAtkAnimData.townWalkingFrames;
	} else {
		player._pNFrames = plrAtkAnimData.idleFrames;
		player._pWFrames = plrAtkAnimData.walkingFrames;
		player._pHFrames = plrAtkAnimData.recoveryFrames;
		player._pBFrames = plrAtkAnimData.blockingFrames;
		switch (gn) {
		case PlayerWeaponGraphic::Unarmed:
			player._pAFrames = plrAtkAnimData.unarmedFrames;
			player._pAFNum = plrAtkAnimData.unarmedActionFrame;
			break;
		case PlayerWeaponGraphic::UnarmedShield:
			player._pAFrames = plrAtkAnimData.unarmedShieldFrames;
			player._pAFNum = plrAtkAnimData.unarmedShieldActionFrame;
			break;
		case PlayerWeaponGraphic::Sword:
			player._pAFrames = plrAtkAnimData.swordFrames;
			player._pAFNum = plrAtkAnimData.swordActionFrame;
			break;
		case PlayerWeaponGraphic::SwordShield:
			player._pAFrames = plrAtkAnimData.swordShieldFrames;
			player._pAFNum = plrAtkAnimData.swordShieldActionFrame;
			break;
		case PlayerWeaponGraphic::Bow:
			player._pAFrames = plrAtkAnimData.bowFrames;
			player._pAFNum = plrAtkAnimData.bowActionFrame;
			break;
		case PlayerWeaponGraphic::Axe:
			player._pAFrames = plrAtkAnimData.axeFrames;
			player._pAFNum = plrAtkAnimData.axeActionFrame;
			break;
		case PlayerWeaponGraphic::Mace:
			player._pAFrames = plrAtkAnimData.maceFrames;
			player._pAFNum = plrAtkAnimData.maceActionFrame;
			break;
		case PlayerWeaponGraphic::MaceShield:
			player._pAFrames = plrAtkAnimData.maceShieldFrames;
			player._pAFNum = plrAtkAnimData.maceShieldActionFrame;
			break;
		case PlayerWeaponGraphic::Staff:
			player._pAFrames = plrAtkAnimData.staffFrames;
			player._pAFNum = plrAtkAnimData.staffActionFrame;
			break;
		}
	}

	player._pDFrames = plrAtkAnimData.deathFrames;
	player._pSFrames = plrAtkAnimData.castingFrames;
	player._pSFNum = plrAtkAnimData.castingActionFrame;
	const int armorGraphicIndex = player._pgfxnum & ~0xFU;
	if (IsAnyOf(pc, HeroClass::Warrior, HeroClass::Barbarian)) {
		if (gn == PlayerWeaponGraphic::Bow && levelType() != DTYPE_TOWN)
			player._pNFrames = 8;
		if (armorGraphicIndex > 0)
			player._pDFrames = 15;
	}
}

/**
 * @param player The player reference.
 * @param c The hero class.
 */
void Player::create(HeroClass c)
{
	Player &player = *this;
	player = {};
	SetRndSeed(SDL_GetTicks());

	player.setCharacterLevel(1);
	player._pClass = c;

	const ClassAttributes &attr = player.getClassAttributes();

	player.attributes.strength.base = attr.baseStr;
	player.attributes.strength.current = player.attributes.strength.base;

	player.attributes.magic.base = attr.baseMag;
	player.attributes.magic.current = player.attributes.magic.base;

	player.attributes.dexterity.base = attr.baseDex;
	player.attributes.dexterity.current = player.attributes.dexterity.base;

	player.attributes.vitality.base = attr.baseVit;
	player.attributes.vitality.current = player.attributes.vitality.base;

	player.life.current = player.calculateBaseLife();
	player.life.maximum = player.life.current;
	player.life.base = player.life.current;
	player.life.maximumBase = player.life.current;

	player.mana.current = player.calculateBaseMana();
	player.mana.maximum = player.mana.current;
	player.mana.base = player.mana.current;
	player.mana.maximumBase = player.mana.current;

	player._pExperience = 0;
	player._pArmorClass = 0;
	player._pLightRad = 10;
	player._pInfraFlag = false;

	for (uint8_t &spellLevel : player._pSplLvl) {
		spellLevel = 0;
	}

	player._pSpellFlags = SpellFlag::None;
	player._pRSplType = SpellType::Invalid;

	// Initializing the hotkey bindings to no selection
	std::fill(player._pSplHotKey, player._pSplHotKey + NumHotkeys, SpellID::Invalid);

	// CreatePlrItems calls AutoEquip which will overwrite the player graphic if required
	player._pgfxnum = static_cast<uint8_t>(PlayerWeaponGraphic::Unarmed);

	for (bool &levelVisited : player._pLvlVisited) {
		levelVisited = false;
	}

	for (int i = 0; i < 10; i++) {
		player._pSLvlVisited[i] = false;
	}

	player._pLvlChanging = false;
	player.pTownWarps = 0;
	player.pLvlLoad = 0;
	player.pManaShield = false;
	player.pDamAcFlags = ItemSpecialEffectHf::None;
	player.wReflections = 0;

	player.initDungeonMessages();
	CreatePlrItems(player);
	SetRndSeed(0);
}

int Player::calculateStatDifference()
{
	Player &player = *this;
	int diff = 0;
	for (auto attribute : enum_values<CharacterAttribute>()) {
		diff += player.GetMaximumAttributeValue(attribute);
		diff -= player.GetBaseAttributeValue(attribute);
	}
	return diff;
}

void Player::advanceLevel()
{
	if (!LegacyPlayerStateMutationAllowed())
		return;

	Player &player = *this;
	player.setCharacterLevel(player.getCharacterLevel() + 1);

	CalcPlrInv(player, true);

	if (player.calculateStatDifference() < 5) {
		player._pStatPts = player.calculateStatDifference();
	} else {
		player._pStatPts += 5;
	}
	const int hp = player.getClassAttributes().lvlLife;

	player.life.maximum += hp;
	player.life.current = player.life.maximum;
	player.life.maximumBase += hp;
	player.life.base = player.life.maximumBase;

	if (&player == MyPlayer) {
		RedrawComponent(PanelDrawComponent::Health);
	}

	const int mana = player.getClassAttributes().lvlMana;

	player.mana.maximum += mana;
	player.mana.maximumBase += mana;

	if (HasNoneOf(player._pIFlags, ItemSpecialEffect::NoMana)) {
		player.mana.current = player.mana.maximum;
		player.mana.base = player.mana.maximumBase;
	}

	if (&player == MyPlayer) {
		RedrawComponent(PanelDrawComponent::Mana);
	}

	if (ControlMode != ControlTypes::KeyboardAndMouse)
		FocusOnCharInfo();

	CalcPlrInv(player, true);
	PlaySFX(SfxID::ItemArmor);
	PlaySFX(SfxID::ItemSign);
}

void Player::_addExperience(uint32_t experience, int levelDelta)
{
	if (!LegacyPlayerStateMutationAllowed())
		return;

	if (this != MyPlayer || hasNoLife())
		return;

	if (isMaxCharacterLevel()) {
		return;
	}

	// Adjust xp based on difference between the players current level and the target level (usually a monster level)
	uint32_t clampedExp = static_cast<uint32_t>(std::clamp<int64_t>(static_cast<int64_t>(experience * (1 + levelDelta / 10.0)), 0, std::numeric_limits<uint32_t>::max()));

	// Prevent power leveling
	if (gbIsMultiplayer) {
		// for low level characters experience gain is capped to 1/20 of current levels xp
		// for high level characters experience gain is capped to 200 * current level - this is a smaller value than 1/20 of the exp needed for the next level after level 5.
		clampedExp = std::min<uint32_t>({ clampedExp, /* level 1-5: */ getNextExperienceThreshold() / 20U, /* level 6-50: */ 200U * getCharacterLevel() });
	}

	CurrentGameEventBus.PlayerGainExperience(getId(), clampedExp);

	const uint32_t maxExperience = GetNextExperienceThresholdForLevel(getMaxCharacterLevel());

	// ensure we only add enough experience to reach the max experience cap so we don't overflow
	_pExperience += std::min(clampedExp, maxExperience - _pExperience);

	if (*GetOptions().Gameplay.experienceBar) {
		RedrawEverything();
	}

	// Increase player level if applicable
	while (!isMaxCharacterLevel() && _pExperience >= getNextExperienceThreshold()) {
		// NextPlrLevel increments character level which changes the next experience threshold
		this->advanceLevel();
	}

	NetSendCmdParam1(false, CMD_PLRLEVEL, getCharacterLevel());
}

void Player::addExperience(uint32_t experience)
{
	_addExperience(experience, 0);
}

void Player::addExperience(uint32_t experience, int monsterLevel)
{
	_addExperience(experience, monsterLevel - getCharacterLevel());
}

bool Player::isOnActiveLevel() const
{
	if (isSetLevel())
		return isOnLevel(setLevelNumber());
	return isOnLevel(currentLevelNumber());
}

bool Player::isOnLevel(uint8_t level) const
{
	return !plrIsOnSetLevel && plrlevel == level;
}

bool Player::isOnLevel(_setlevels level) const
{
	return plrIsOnSetLevel && plrlevel == static_cast<uint8_t>(level);
}

bool Player::isOnArenaLevel() const
{
	return plrIsOnSetLevel && IsArenaLevel(static_cast<_setlevels>(plrlevel));
}

void Player::setLevel(uint8_t level)
{
	plrlevel = level;
	plrIsOnSetLevel = false;
}

void Player::setLevel(_setlevels level)
{
	plrlevel = static_cast<uint8_t>(level);
	plrIsOnSetLevel = true;
}

bool Player::isHoldingItem(const ItemType type) const
{
	const Item &leftHandItem = InvBody[INVLOC_HAND_LEFT];
	const Item &rightHandItem = InvBody[INVLOC_HAND_RIGHT];

	return (type == leftHandItem._itype && leftHandItem._iStatFlag) || (type == rightHandItem._itype && rightHandItem._iStatFlag);
}

bool Player::hasNoLife() const
{
	return levelType() == DTYPE_TOWN ? false : life.current >> 6 <= 0;
}

bool Player::hasNoMana() const
{
	return mana.current >> 6 <= 0;
}

void AddPlrMonstExper(int lvl, unsigned exp, char pmask)
{
	unsigned totplrs = 0;
	for (size_t i = 0; i < Players.size(); i++) {
		if (((1 << i) & pmask) != 0) {
			totplrs++;
		}
	}

	if (totplrs != 0) {
		const unsigned e = exp / totplrs;
		if ((pmask & (1 << MyPlayerId)) != 0)
			MyPlayer->addExperience(e, lvl);
	}
}

void InitPlayer(Player &player, bool firstTime)
{
	if (firstTime) {
		player._pRSplType = SpellType::Invalid;
		player._pRSpell = SpellID::Invalid;
		if (&player == MyPlayer)
			LoadHotkeys();
		player._pSBkSpell = SpellID::Invalid;
		player.queuedSpell.spellId = player._pRSpell;
		player.queuedSpell.spellType = player._pRSplType;
		player.pManaShield = false;
		player.wReflections = 0;
	}

	player.lightId = NO_LIGHT;

	if (player.isOnActiveLevel()) {

		player.setAnimations();

		ClearStateVariables(player);

		if (!player.hasNoLife()) {
			player._pmode = PM_STAND;
			player.setAnimation(player_graphic::Stand, Direction::South);
			player.animInfo.currentFrame = GenerateRnd(player._pNFrames - 1);
			player.animInfo.tickCounterOfCurrentFrame = GenerateRnd(3);
		} else {
			player._pgfxnum &= ~0xFU;
			player._pmode = PM_DEATH;
			player.setAnimation(player_graphic::Death, Direction::South);
			player.animInfo.currentFrame = player.animInfo.numberOfFrames - 2;
		}

		player.direction = Direction::South;

		if (&player == MyPlayer && (!firstTime || levelType() != DTYPE_TOWN) && LegacyLevelLoadPlayerPlacementAllowed()) {
			player.position.tile = viewPosition();
		}

		player.saveOldPosition();
		player.walkpath[0] = WALK_NONE;
		player.destAction = ACTION_NONE;

		if (&player == MyPlayer) {
			player.lightId = CurrentLightManager.AddLight(player.position.tile, player._pLightRad);
			CurrentLightManager.ChangeLightXY(player.lightId, player.position.tile); // fix for a bug where old light is still visible at the entrance after reentering level
		}
		CurrentLightManager.ActivateVision(player.position.tile, player._pLightRad, player.getId());
	}

	player._pAblSpells = GetSpellBitmask(GetPlayerStartingLoadoutForClass(player._pClass).skill);

	player._pInvincible = false;

	if (&player == MyPlayer) {
		MyPlayerIsDead = false;
	}
}

void InitMultiView()
{
	assert(MyPlayer != nullptr);
	viewPosition() = MyPlayer->position.tile;
}

// Clears the transparency flags for the 3x3 area around the player,
// allowing the player to see through transparent tiles again after walking away from them.
void PlrClrTrans(Point position)
{
	for (int i = position.y - 1; i <= position.y + 1; i++) {
		for (int j = position.x - 1; j <= position.x + 1; j++) {
			visibleTransparencyRegions()[tileAt(j, i).transVal()] = false;
		}
	}
}

// Sets the transparency flags for the 3x3 area around the player,
// allowing the player to see through transparent tiles. This is called when the player moves onto a new tile,
// so that the player can see through any transparent tiles around the new position. If the player is in a level
// without transparent tiles, the first transparency flag is set to true to allow the player to see through all tiles.
void PlrDoTrans(Point position)
{
	if (IsNoneOf(levelType(), DTYPE_CATHEDRAL, DTYPE_CATACOMBS, DTYPE_CRYPT)) {
		visibleTransparencyRegions()[1] = true;
		return;
	}

	for (int i = position.y - 1; i <= position.y + 1; i++) {
		for (int j = position.x - 1; j <= position.x + 1; j++) {
			const int8_t transVal = tileAt(j, i).transVal();
			if (IsTileNotSolid({ j, i }) && transVal != 0) {
				visibleTransparencyRegions()[transVal] = true;
			}
		}
	}
}

void Player::saveOldPosition()
{
	Player &player = *this;
	player.position.old = player.position.tile;
}

void Player::fixLocation(Direction bDir)
{
	Player &player = *this;
	player.position.future = player.position.tile;
	player.direction = bDir;
	if (&player == MyPlayer) {
		viewPosition() = player.position.tile;
	}
	CurrentLightManager.ChangeLightXY(player.lightId, player.position.tile);
	CurrentLightManager.ChangeVisionXY(player.getId(), player.position.tile);
}

void Player::startStand(Direction dir)
{
	Player &player = *this;
	if (player._pInvincible && player.hasNoLife() && &player == MyPlayer) {
		player.syncKill(DeathReason::Unknown);
		return;
	}

	player.setAnimation(player_graphic::Stand, dir);
	player._pmode = PM_STAND;
	player.fixLocation(dir);
	FixPlrWalkTags(player);
	player.occupyTile(player.position.tile, false);
	player.saveOldPosition();
}

void Player::startBlock(Direction dir)
{
	Player &player = *this;
	if (player._pInvincible && player.hasNoLife() && &player == MyPlayer) {
		player.syncKill(DeathReason::Unknown);
		return;
	}

	PlaySfxLoc(SfxID::ItemSword, player.position.tile);

	int8_t skippedAnimationFrames = 0;
	if (HasAnyOf(player._pIFlags, ItemSpecialEffect::FastBlock)) {
		skippedAnimationFrames = (player._pBFrames - 2); // ISPL_FASTBLOCK means we cancel the animation if frame 2 was shown
	}

	player.setAnimation(player_graphic::Block, dir, AnimationDistributionFlags::SkipsDelayOfLastFrame, skippedAnimationFrames);

	player._pmode = PM_BLOCK;
	player.fixLocation(dir);
	player.saveOldPosition();
}

/**
 * @todo Figure out why clearing player.position.old sometimes fails
 */
void FixPlrWalkTags(const Player &player)
{
	for (int y = 0; y < MAXDUNY; y++) {
		for (int x = 0; x < MAXDUNX; x++) {
			if (PlayerAtPosition({ x, y }) == &player)
				tileAt(Point { x, y }).setPlayer(0);
		}
	}
}

void Player::startHit(int dam, bool forcehit)
{
	Player &player = *this;
	if (player._pInvincible && player.hasNoLife() && &player == MyPlayer) {
		player.syncKill(DeathReason::Unknown);
		return;
	}

	player.Say(HeroSpeech::ArghClang);

	RedrawComponent(PanelDrawComponent::Health);
	if (player._pClass == HeroClass::Barbarian) {
		if (dam >> 6 < player.getCharacterLevel() + player.getCharacterLevel() / 4 && !forcehit) {
			return;
		}
	} else if (dam >> 6 < player.getCharacterLevel() && !forcehit) {
		return;
	}

	const Direction pd = player.direction;

	int8_t skippedAnimationFrames = 0;
	if (HasAnyOf(player._pIFlags, ItemSpecialEffect::FastestHitRecovery)) {
		skippedAnimationFrames = 3;
	} else if (HasAnyOf(player._pIFlags, ItemSpecialEffect::FasterHitRecovery)) {
		skippedAnimationFrames = 2;
	} else if (HasAnyOf(player._pIFlags, ItemSpecialEffect::FastHitRecovery)) {
		skippedAnimationFrames = 1;
	} else {
		skippedAnimationFrames = 0;
	}

	player.setAnimation(player_graphic::Hit, pd, AnimationDistributionFlags::None, skippedAnimationFrames);

	player._pmode = PM_GOTHIT;
	player.fixLocation(pd);
	FixPlrWalkTags(player);
	player.occupyTile(player.position.tile, false);
	player.saveOldPosition();
}

#if defined(__clang__) || defined(__GNUC__)
__attribute__((no_sanitize("shift-base")))
#endif
void
Player::startKill(DeathReason deathReason)
{

#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacyCombatMessage(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
#endif

	Player &player = *this;
	if (player.hasNoLife() && player._pmode == PM_DEATH) {
		return;
	}

	bool processLegacyDeathDrops = true;
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	processLegacyDeathDrops = ShouldProcessLegacyDeathDrops(
		&player == MyPlayer && authoritative::GetServerBackedRuntime().IsAuthoritativeMode());
#endif

	if (&player == MyPlayer) {
		if (processLegacyDeathDrops)
			NetSendCmdParam1(true, CMD_PLRDEAD, static_cast<uint16_t>(deathReason));
		gamemenu_off();
	}

	const bool dropGold = !gbIsMultiplayer || !(player.isOnLevel(16) || player.isOnArenaLevel());
	const bool dropItems = dropGold && deathReason == DeathReason::MonsterOrTrap;
	const bool dropEar = dropGold && deathReason == DeathReason::Player;

	player.Say(HeroSpeech::AuughUh);

	// Are the current animations item dependent?
	if (player._pgfxnum != 0) {
		if (dropItems) {
			// Ensure death animation show the player without weapon and armor, because they drop on death
			player._pgfxnum = 0;
		} else {
			// Death animation aren't weapon specific, so always use the unarmed animations
			player._pgfxnum &= ~0xFU;
		}
		player.resetGraphics();
		player.setAnimations();
	}

	player.setAnimation(player_graphic::Death, player.direction);

	player._pBlockFlag = false;
	player._pmode = PM_DEATH;
	player._pInvincible = true;
	player.setHitPoints(0);

	if (&player != MyPlayer && dropItems) {
		// Ensure that items are removed for remote players
		// The dropped items will be synced separately (by the remote client)
		for (Item &item : player.InvBody) {
			item.clear();
		}
		CalcPlrInv(player, false);
	}

	if (player.isOnActiveLevel()) {
		player.fixLocation(player.direction);
		FixPlrWalkTags(player);
		tileAt(player.position.tile).addFlags(DungeonFlag::DeadPlayer);
		player.saveOldPosition();

		// Only generate drops once (for the local player)
		// For remote players we get separated sync messages (by the remote client)
		if (&player == MyPlayer) {
			RedrawComponent(PanelDrawComponent::Health);

			if (processLegacyDeathDrops && !player.HoldItem.isEmpty()) {
				DeadItem(player, std::move(player.HoldItem), { 0, 0 });
				NewCursor(CURSOR_HAND);
			}
			if (processLegacyDeathDrops && dropGold) {
				DropHalfPlayersGold(player);
			}
			if (processLegacyDeathDrops && dropEar) {
				Item ear;
				InitializeItem(ear, IDI_EAR);
				CopyUtf8(ear._iName, fmt::format(fmt::runtime("Ear of {:s}"), player._pName), sizeof(ear._iName));
				CopyUtf8(ear._iIName, player._pName, ItemNameLength);
				switch (player._pClass) {
				case HeroClass::Sorcerer:
					ear._iCurs = ICURS_EAR_SORCERER;
					break;
				case HeroClass::Warrior:
					ear._iCurs = ICURS_EAR_WARRIOR;
					break;
				case HeroClass::Rogue:
				case HeroClass::Monk:
				case HeroClass::Bard:
				case HeroClass::Barbarian:
					ear._iCurs = ICURS_EAR_ROGUE;
					break;
				default:
					break;
				}

				ear._iCreateInfo = player._pName[0] << 8 | player._pName[1];
				ear._iSeed = player._pName[2] << 24 | player._pName[3] << 16 | player._pName[4] << 8 | player._pName[5];
				ear._ivalue = player.getCharacterLevel();

				if (FindGetItem(ear._iSeed, IDI_EAR, ear._iCreateInfo) == -1) {
					DeadItem(player, std::move(ear), { 0, 0 });
				}
			}
			if (processLegacyDeathDrops && dropItems) {
				Direction pdd = player.direction;
				for (Item &item : player.InvBody) {
					pdd = Left(pdd);
					DeadItem(player, item.pop(), Displacement(pdd));
				}

				CalcPlrInv(player, false);
			}
			if (!processLegacyDeathDrops && !player.HoldItem.isEmpty()) {
				// The authoritative server resolves held-item drops and the next
				// snapshot reconciles the cursor. Do not synthesize a local item.
				player.HoldItem.clear();
				NewCursor(CURSOR_HAND);
			}
		}
	}
	player.setHitPoints(0);
}

void StripTopGold(Player &player)
{
	if (!LegacyDeathDropsAllowed())
		return;

	for (Item &item : InventoryPlayerItemsRange { player }) {
		if (item._itype != ItemType::Gold)
			continue;
		if (item._ivalue <= MaxGold)
			continue;
		Item excessGold;
		MakeGoldStack(excessGold, item._ivalue - MaxGold);
		item._ivalue = MaxGold;

		if (GoldAutoPlace(player, excessGold))
			continue;
		if (!player.HoldItem.isEmpty() && ItemPoolAdapter::ActiveItemCountValue() + 1 >= MAXITEMS)
			continue;
		DeadItem(player, std::move(excessGold), { 0, 0 });
	}
	player._pGold = CalculateGold(player);

	if (player.HoldItem.isEmpty())
		return;
	if (AutoEquip(player, player.HoldItem, false))
		return;
	if (CanFitItemInInventory(player, player.HoldItem))
		return;
	if (AutoPlaceItemInBelt(player, player.HoldItem))
		return;
	const std::optional<Point> itemTile = FindAdjacentPositionForItem(player.position.tile, player.direction);
	if (itemTile)
		return;
	DeadItem(player, std::move(player.HoldItem), { 0, 0 });
	NewCursor(CURSOR_HAND);
}

void Player::applyDamage(DamageType damageType, int dam, int minHP, int frac, DeathReason deathReason)
{

#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacyCombatMessage(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
#endif

	Player &player = *this;
	int totalDamage = (dam << 6) + frac;
	if (&player == MyPlayer && !player.hasNoLife()) {
		CurrentGameEventBus.PlayerTakeDamage(player.getId(), totalDamage, static_cast<int>(damageType));
	}
	if (totalDamage > 0 && player.pManaShield && HasNoneOf(player._pIFlags, ItemSpecialEffect::NoMana)) {
		const uint8_t manaShieldLevel = player._pSplLvl[static_cast<int8_t>(SpellID::ManaShield)];
		if (manaShieldLevel > 0) {
			totalDamage += totalDamage / -player.GetManaShieldDamageReduction();
		}
		if (&player == MyPlayer)
			RedrawComponent(PanelDrawComponent::Mana);
		if (player.mana.current >= totalDamage) {
			player.mana.current -= totalDamage;
			player.mana.base -= totalDamage;
			totalDamage = 0;
		} else {
			totalDamage -= player.mana.current;
			if (manaShieldLevel > 0) {
				totalDamage += totalDamage / (player.GetManaShieldDamageReduction() - 1);
			}
			player.mana.current = 0;
			player.mana.base = player.mana.maximumBase - player.mana.maximum;
			if (&player == MyPlayer) {
				bool processLegacyManaShieldCommand = true;
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
				processLegacyManaShieldCommand = ShouldProcessNativeManaShield(authoritative::GetServerBackedRuntime().IsConnected());
#endif
				if (processLegacyManaShieldCommand)
					NetSendCmd(true, CMD_REMSHIELD);
			}
		}
	}

	if (totalDamage == 0)
		return;

	RedrawComponent(PanelDrawComponent::Health);
	player.life.current -= totalDamage;
	player.life.base -= totalDamage;
	if (player.life.current > player.life.maximum) {
		player.life.current = player.life.maximum;
		player.life.base = player.life.maximumBase;
	}
	const int minHitPoints = minHP << 6;
	if (player.life.current < minHitPoints) {
		player.setHitPoints(minHitPoints);
	}
	if (player.hasNoLife()) {
		player.syncKill(deathReason);
	}
}

void Player::syncKill(DeathReason deathReason)
{

#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacyCombatMessage(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
#endif

	Player &player = *this;
	player.setHitPoints(0);
	player.startKill(deathReason);
}

void Player::removeMissiles() const
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacySimulation(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
#endif

	const Player &player = *this;
	if (levelType() != DTYPE_TOWN) {
		Monster *golem;
		while ((golem = FindGolemForPlayer(player)) != nullptr) {
			KillGolem(*golem);
		}
	}

	for (auto &missile : Missiles) {
		if (missile._mitype == MissileID::StoneCurse && &Players[missile._misource] == &player) {
			Monsters[missile.var2].mode = static_cast<MonsterMode>(missile.var1);
		}
	}
}

#if defined(__clang__) || defined(__GNUC__)
__attribute__((no_sanitize("shift-base")))
#endif
void
StartNewLvl(Player &player, interface_mode fom, int lvl)
{
	if (!LegacyTransitionAllowed())
		return;

	InitLevelChange(player);

	switch (fom) {
	case WM_DIABNEXTLVL:
	case WM_DIABPREVLVL:
	case WM_DIABRTNLVL:
	case WM_DIABTOWNWARP:
		player.setLevel(lvl);
		break;
	case WM_DIABSETLVL:
		if (&player == MyPlayer)
			setLevelNumber() = (_setlevels)lvl;
		player.setLevel(setLevelNumber());
		break;
	case WM_DIABTWARPUP:
		MyPlayer->pTownWarps |= 1 << (levelType() - 2);
		player.setLevel(lvl);
		break;
	case WM_DIABRETOWN:
		break;
	default:
		app_fatal("StartNewLvl");
	}

	if (&player == MyPlayer) {
		player._pmode = PM_NEWLVL;
		player._pInvincible = true;
		SDL_Event event;
		CustomEventToSdlEvent(event, fom);
		SDL_PushEvent(&event);
		if (gbIsMultiplayer) {
			NetSendCmdParam2(true, CMD_NEWLVL, fom, lvl);
		}
	}
}

void RestartTownLvl(Player &player)
{
	if (!LegacyTransitionAllowed())
		return;

	InitLevelChange(player);

	player.setLevel(0);
	player._pInvincible = false;

	player.setHitPoints(64);

	player.mana.current = 0;
	player.mana.base = player.mana.current - (player.mana.maximum - player.mana.maximumBase);

	CalcPlrInv(player, false);
	player._pmode = PM_NEWLVL;

	if (&player == MyPlayer) {
		player._pInvincible = true;
		SDL_Event event;
		CustomEventToSdlEvent(event, WM_DIABRETOWN);
		SDL_PushEvent(&event);
	}
}

void PresentRestartTown(Player &player)
{
	InitLevelChange(player);
	player._pInvincible = false;
	player._pmode = PM_NEWLVL;

	if (&player == MyPlayer) {
		player._pInvincible = true;
		SDL_Event event;
		CustomEventToSdlEvent(event, WM_DIABRETOWN);
		SDL_PushEvent(&event);
	}
}

void StartWarpLvl(Player &player, size_t pidx)
{
	if (!LegacyTransitionAllowed())
		return;

	InitLevelChange(player);

	if (gbIsMultiplayer) {
		if (!player.isOnLevel(0)) {
			player.setLevel(0);
		} else {
			if (Portals[pidx].setlvl)
				player.setLevel(static_cast<_setlevels>(Portals[pidx].level));
			else
				player.setLevel(Portals[pidx].level);
		}
	}

	if (&player == MyPlayer) {
		SetCurrentPortal(pidx);
		player._pmode = PM_NEWLVL;
		player._pInvincible = true;
		SDL_Event event;
		CustomEventToSdlEvent(event, WM_DIABWARPLVL);
		SDL_PushEvent(&event);
	}
}

void ProcessPlayers()
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacySimulation(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
#endif

	assert(MyPlayer != nullptr);
	Player &myPlayer = *MyPlayer;
	bool processNativeDrainLife = true;
	bool processNativeManaShield = true;
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	const bool serverConnected = authoritative::GetServerBackedRuntime().IsConnected();
	processNativeDrainLife = ShouldProcessNativeDrainLife(serverConnected);
	processNativeManaShield = ShouldProcessNativeManaShield(serverConnected);
#endif

	if (myPlayer.pLvlLoad > 0) {
		myPlayer.pLvlLoad--;
	}

	if (sfxdelay > 0) {
		sfxdelay--;
		if (sfxdelay == 0) {
			switch (sfxdnum) {
			case SfxID::Defiler1:
				InitQTextMsg(TEXT_DEFILER1);
				break;
			case SfxID::Defiler2:
				InitQTextMsg(TEXT_DEFILER2);
				break;
			case SfxID::Defiler3:
				InitQTextMsg(TEXT_DEFILER3);
				break;
			case SfxID::Defiler4:
				InitQTextMsg(TEXT_DEFILER4);
				break;
			default:
				PlaySFX(sfxdnum);
			}
		}
	}

	ValidatePlayer();

	for (size_t pnum = 0; pnum < Players.size(); pnum++) {
		Player &player = Players[pnum];
		if (player.plractive && player.isOnActiveLevel() && (&player == MyPlayer || !player._pLvlChanging)) {
			if (!PlrDeathModeOK(player) && player.hasNoLife()) {
				player.syncKill(DeathReason::Unknown);
			}

			if (&player == MyPlayer) {
				if (processNativeDrainLife && HasAnyOf(player._pIFlags, ItemSpecialEffect::DrainLife)
				    && levelType() != DTYPE_TOWN) {
					player.applyDamage(DamageType::Physical, 0, 0, 4);
				}
				if (processNativeManaShield && player.pManaShield && HasAnyOf(player._pIFlags, ItemSpecialEffect::NoMana)) {
					NetSendCmd(true, CMD_REMSHIELD);
				}
			}

			bool tplayer = false;
			do {
				switch (player._pmode) {
				case PM_STAND:
				case PM_NEWLVL:
				case PM_QUIT:
					tplayer = false;
					break;
				case PM_WALK_NORTHWARDS:
				case PM_WALK_SOUTHWARDS:
				case PM_WALK_SIDEWAYS:
					tplayer = DoWalk(player);
					break;
				case PM_ATTACK:
					tplayer = DoAttack(player);
					break;
				case PM_RATTACK:
					tplayer = DoRangeAttack(player);
					break;
				case PM_BLOCK:
					tplayer = DoBlock(player);
					break;
				case PM_SPELL:
					tplayer = DoSpell(player);
					break;
				case PM_GOTHIT:
					tplayer = DoGotHit(player);
					break;
				case PM_DEATH:
					tplayer = DoDeath(player);
					break;
				}
				CheckNewPath(player, tplayer);
			} while (tplayer);

			player.previewCelSprite = std::nullopt;
			if (player._pmode != PM_DEATH || player.animInfo.tickCounterOfCurrentFrame != 40)
				player.animInfo.processAnimation();
		}
	}
}

void Player::clearPath()
{
	Player &player = *this;
	memset(player.walkpath, WALK_NONE, sizeof(player.walkpath));
}

/**
 * @brief Determines if the target position is clear for the given player to stand on.
 *
 * This requires an ID instead of a Player& to compare with the dPlayer lookup table values.
 *
 * @param player The player to check.
 * @param position Dungeon tile coordinates.
 * @return False if something (other than the player themselves) is blocking the tile.
 */
bool Player::positionIsAvailable(Point position) const
{
	const Player &player = *this;
	if (!InDungeonBounds(position))
		return false;
	if (!IsTileWalkable(position))
		return false;
	Player *otherPlayer = PlayerAtPosition(position);
	if (otherPlayer != nullptr && otherPlayer != &player && !otherPlayer->hasNoLife())
		return false;

	if (tileAt(position).hasMonster()) {
		if (levelType() == DTYPE_TOWN) {
			return false;
		}
		if (tileAt(position).monster() <= 0) {
			return false;
		}
		if (!Monsters[tileAt(position).monster() - 1].hasNoLife()) {
			return false;
		}
	}

	return true;
}

void Player::makePath(Point targetPosition, bool endspace)
{
	Player &player = *this;
	if (player.position.future == targetPosition) {
		return;
	}

	int path = FindPath(CanStep, [&player](Point position) { return player.positionIsAvailable(position); }, player.position.future, targetPosition, player.walkpath, MaxPathLengthPlayer);
	if (path == 0) {
		return;
	}

	if (!endspace) {
		path--;
	}

	player.walkpath[path] = WALK_NONE;
}

#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
tl::expected<authoritative::ServerBackedCastSource, std::string> MakeServerBackedCastSource(
    const Player &player,
    SpellID spellId,
    SpellType spellType)
{
	using authoritative::ServerBackedCastSource;
	using authoritative::ServerBackedCastSourceKind;

	switch (spellType) {
	case SpellType::Skill:
		return ServerBackedCastSource { ServerBackedCastSourceKind::Ability, std::nullopt };
	case SpellType::Spell:
		return ServerBackedCastSource { ServerBackedCastSourceKind::MemorizedSpell, std::nullopt };
	case SpellType::Charges:
		if (const auto reference = authoritative::GetServerBackedRuntime().PlayerItemReferenceForSeed(
		        player.InvBody[INVLOC_HAND_LEFT]._iSeed);
		    reference.has_value()) {
			return ServerBackedCastSource {
				ServerBackedCastSourceKind::ChargedItem,
				*reference,
			};
		}
		return tl::make_unexpected("The authoritative charged-item source is no longer present.");
	case SpellType::Scroll:
		for (uint32_t index = 0; index < InventoryGridCells; ++index) {
			const Item &item = player.InvList[index];
			if (item.isScrollOf(spellId) || item.isRuneOf(spellId)) {
				const auto reference = authoritative::GetServerBackedRuntime().PlayerItemReferenceForSeed(item._iSeed);
				if (!reference.has_value())
					return tl::make_unexpected("The authoritative scroll source is no longer present.");
				return ServerBackedCastSource {
					ServerBackedCastSourceKind::ConsumableItem,
					*reference,
				};
			}
		}
		for (uint32_t index = 0; index < MaxBeltItems; ++index) {
			const Item &item = player.SpdList[index];
			if (item.isScrollOf(spellId) || item.isRuneOf(spellId)) {
				const auto reference = authoritative::GetServerBackedRuntime().PlayerItemReferenceForSeed(item._iSeed);
				if (!reference.has_value())
					return tl::make_unexpected("The authoritative scroll source is no longer present.");
				return ServerBackedCastSource {
					ServerBackedCastSourceKind::ConsumableItem,
					*reference,
				};
			}
		}
		if (IsStashOpen) {
			for (const Item &item : Stash.stashList) {
				if (!item.isScrollOf(spellId) && !item.isRuneOf(spellId))
					continue;
				const auto reference = authoritative::GetServerBackedRuntime().PlayerItemReferenceForSeed(item._iSeed);
				if (!reference.has_value())
					return tl::make_unexpected("The authoritative stash scroll source is no longer present.");
				return ServerBackedCastSource {
					ServerBackedCastSourceKind::ConsumableItem,
					*reference,
				};
			}
		}
		return tl::make_unexpected("The authoritative scroll source is no longer present.");
	case SpellType::Invalid:
		return tl::make_unexpected("Cannot submit an invalid authoritative cast source.");
	}
	return tl::make_unexpected("Cannot submit an unknown authoritative cast source.");
}
#endif

void CheckPlrSpell(bool isShiftHeld, SpellID spellID, SpellType spellType)
{
	bool addflag = false;

	assert(MyPlayer != nullptr);
	Player &myPlayer = *MyPlayer;

	if (!IsValidSpell(spellID)) {
		myPlayer.Say(HeroSpeech::IDontHaveASpellReady);
		return;
	}

	if (ControlMode == ControlTypes::KeyboardAndMouse) {
		if (pcurs != CURSOR_HAND)
			return;

		if (GetMainPanel().contains(MousePosition)) // inside main panel
			return;

		if (
		    (IsLeftPanelOpen() && GetLeftPanel().contains(MousePosition))      // inside left panel
		    || (IsRightPanelOpen() && GetRightPanel().contains(MousePosition)) // inside right panel
		) {
			if (spellID != SpellID::Healing
			    && spellID != SpellID::Identify
			    && spellID != SpellID::ItemRepair
			    && spellID != SpellID::Infravision
			    && spellID != SpellID::StaffRecharge)
				return;
		}
	}

	if (levelType() == DTYPE_TOWN && !GetSpellData(spellID).isAllowedInTown()) {
		myPlayer.Say(HeroSpeech::ICantCastThatHere);
		return;
	}

	SpellCheckResult spellcheck = SpellCheckResult::Success;
	switch (spellType) {
	case SpellType::Skill:
	case SpellType::Spell:
		spellcheck = CheckSpell(*MyPlayer, spellID, spellType, false);
		addflag = spellcheck == SpellCheckResult::Success;
		break;
	case SpellType::Scroll:
		addflag = pcurs == CURSOR_HAND && CanUseScroll(myPlayer, spellID);
		break;
	case SpellType::Charges:
		addflag = pcurs == CURSOR_HAND && CanUseStaff(myPlayer, spellID);
		break;
	case SpellType::Invalid:
		return;
	}

	if (!addflag) {
		if (spellType == SpellType::Spell) {
			switch (spellcheck) {
			case SpellCheckResult::Fail_NoMana:
				myPlayer.Say(HeroSpeech::NotEnoughMana);
				break;
			case SpellCheckResult::Fail_Level0:
				myPlayer.Say(HeroSpeech::ICantCastThatYet);
				break;
			default:
				myPlayer.Say(HeroSpeech::ICantDoThat);
				break;
			}
			LastPlayerAction = PlayerActionType::None;
		}
		return;
	}

#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (authoritative::GetServerBackedRuntime().IsConnected()) {
		if ((spellID == SpellID::Identify || spellID == SpellID::ItemRepair || spellID == SpellID::StaffRecharge) && spellType == SpellType::Skill) {
			if (SpellbookFlag)
				SpellbookFlag = false;
			if (!invflag) {
				invflag = true;
				if (ControlMode != ControlTypes::KeyboardAndMouse)
					FocusOnInventory();
			}
			cursor_id cursor = CURSOR_IDENTIFY;
			if (spellID == SpellID::ItemRepair)
				cursor = CURSOR_REPAIR;
			else if (spellID == SpellID::StaffRecharge)
				cursor = CURSOR_RECHARGE;
			NewCursor(cursor);
			LastPlayerAction = PlayerActionType::Spell;
			return;
		}
		const auto targetingKind = authoritative::GetServerBackedRuntime().SpellTargetingKindForSpell(static_cast<uint32_t>(spellID));
		const bool preserveLegacyTargeting = !targetingKind.has_value()
		    || *targetingKind == authoritative::ProjectedSpellTargetingKind::Unspecified
		    || (*targetingKind != authoritative::ProjectedSpellTargetingKind::Tile
		        && *targetingKind != authoritative::ProjectedSpellTargetingKind::Player
		        && *targetingKind != authoritative::ProjectedSpellTargetingKind::CombatEntity
		        && *targetingKind != authoritative::ProjectedSpellTargetingKind::WorldEntity);
		const bool allowsPlayerTarget = preserveLegacyTargeting
		    || *targetingKind == authoritative::ProjectedSpellTargetingKind::Player
		    || *targetingKind == authoritative::ProjectedSpellTargetingKind::CombatEntity;
		const bool allowsCombatEntityTarget = preserveLegacyTargeting
		    || *targetingKind == authoritative::ProjectedSpellTargetingKind::CombatEntity;
		const bool playerUnderCursor = allowsPlayerTarget && PlayerUnderCursor != nullptr;
		const bool monsterUnderCursor = allowsCombatEntityTarget && pcursmonst != -1;
		const auto playerEntityId = playerUnderCursor
		    ? authoritative::GetServerBackedRuntime().PlayerEntityIdAt(cursPosition.x, cursPosition.y)
		    : std::nullopt;
		const auto monsterEntityId = monsterUnderCursor
		    ? authoritative::GetServerBackedRuntime().MonsterEntityIdAt(cursPosition.x, cursPosition.y)
		    : std::nullopt;
		const auto targetResolution = authoritative::ResolveServerBackedSpellTarget(
		    allowsPlayerTarget,
		    allowsCombatEntityTarget,
		    playerUnderCursor,
		    monsterUnderCursor,
		    playerEntityId.has_value(),
		    monsterEntityId.has_value());
		if (targetResolution == authoritative::ServerBackedSpellTargetResolution::MissingAuthoritativeEntity) {
			LogError("Server-backed spell target is not an authoritative entity.");
			return;
		}
		uint32_t targetEntityId = 0;
		if (targetResolution == authoritative::ServerBackedSpellTargetResolution::Player)
			targetEntityId = *playerEntityId;
		else if (targetResolution == authoritative::ServerBackedSpellTargetResolution::CombatEntity)
			targetEntityId = *monsterEntityId;
		auto source = MakeServerBackedCastSource(myPlayer, spellID, spellType);
		if (!source.has_value()) {
			LogError("Server-backed spell source failed: {}", source.error());
			return;
		}
		if (auto result = authoritative::GetServerBackedRuntime().Cast(
		        static_cast<uint32_t>(spellID), targetEntityId, cursPosition.x, cursPosition.y, *source, 0, GetMillisecondsSinceStartup()); !result.has_value())
			LogError("Server-backed spell cast failed: {}", result.error());
		LastPlayerAction = targetEntityId == 0
		    ? PlayerActionType::Spell
		    : PlayerUnderCursor != nullptr ? PlayerActionType::SpellPlayerTarget : PlayerActionType::SpellMonsterTarget;
		return;
	}
#endif

	const int spellFrom = 0;
	if (IsWallSpell(spellID)) {
		LastPlayerAction = PlayerActionType::Spell;
		const Direction sd = GetDirection(myPlayer.position.tile, cursPosition);
		NetSendCmdLocParam4(true, CMD_SPELLXYD, cursPosition, static_cast<int8_t>(spellID), static_cast<uint8_t>(spellType), static_cast<uint16_t>(sd), spellFrom);
	} else if (pcursmonst != -1 && !isShiftHeld) {
		LastPlayerAction = PlayerActionType::SpellMonsterTarget;
		NetSendCmdParam4(true, CMD_SPELLID, pcursmonst, static_cast<int8_t>(spellID), static_cast<uint8_t>(spellType), spellFrom);
	} else if (PlayerUnderCursor != nullptr && !PlayerUnderCursor->hasNoLife() && !isShiftHeld && !myPlayer.friendlyMode) {
		LastPlayerAction = PlayerActionType::SpellPlayerTarget;
		NetSendCmdParam4(true, CMD_SPELLPID, PlayerUnderCursor->getId(), static_cast<int8_t>(spellID), static_cast<uint8_t>(spellType), spellFrom);
	} else {
		Point targetedTile = cursPosition;
		if (spellID == SpellID::Teleport && myPlayer.executedSpell.spellId == SpellID::Teleport) {
			// Check if the player is attempting to queue Teleport onto a tile that is currently being targeted with Teleport, or a nearby tile
			if (cursPosition.WalkingDistance(myPlayer.position.temp) <= 1) {
				// Get the relative displacement from the player's current position to the cursor position
				const WorldTileDisplacement relativeMove = cursPosition - static_cast<Point>(myPlayer.position.tile);
				// Target the tile the relative distance away from the player's targeted Teleport tile
				targetedTile = myPlayer.position.temp + relativeMove;
			}
		}
		LastPlayerAction = PlayerActionType::Spell;
		NetSendCmdLocParam3(true, CMD_SPELLXY, targetedTile, static_cast<int8_t>(spellID), static_cast<uint8_t>(spellType), spellFrom);
	}
}

void Player::syncAnimation()
{
	Player &player = *this;
	const player_graphic graphic = player.getGraphic();
	if (!HeadlessMode)
		player.animInfo.sprites = player.AnimationData[static_cast<size_t>(graphic)].spritesForDirection(player.direction);
}

void Player::syncInitialPosition()
{
	Player &player = *this;
	if (!player.isOnActiveLevel())
		return;

	const WorldTileDisplacement offset[9] = { { 0, 0 }, { 1, 0 }, { 0, 1 }, { 1, 1 }, { 2, 0 }, { 0, 2 }, { 1, 2 }, { 2, 1 }, { 2, 2 } };

	const Point position = [&]() {
		for (int i = 0; i < 8; i++) {
			Point position = player.position.tile + offset[i];
			if (player.positionIsAvailable(position))
				return position;
		}

		const std::optional<Point> nearPosition = FindClosestValidPosition(
		    [&player](Point testPosition) {
			    for (int i = 0; i < numTriggers(); i++) {
				    if (trigs[i].position == testPosition)
					    return false;
			    }
			    return player.positionIsAvailable(testPosition) && !PosOkPortal(currentLevelNumber(), testPosition);
		    },
		    player.position.tile,
		    1, // skip the starting tile since that was checked in the previous loop
		    50);

		return nearPosition.value_or(Point { 0, 0 });
	}();

	player.position.tile = position;
	player.occupyTile(position, false);
	player.position.future = position;

	if (&player == MyPlayer) {
		viewPosition() = position;
	}
}

void Player::syncInitialState()
{
	Player &player = *this;
	player.setAnimations();
	player.syncInitialPosition();
	if (&player != MyPlayer)
		player.lightId = NO_LIGHT;
}

void Player::checkStats()
{
	Player &player = *this;
	for (auto attribute : enum_values<CharacterAttribute>()) {
		const int maxStatPoint = player.GetMaximumAttributeValue(attribute);
		switch (attribute) {
		case CharacterAttribute::Strength:
			player.attributes.strength.base = std::clamp(player.attributes.strength.base, 0, maxStatPoint);
			break;
		case CharacterAttribute::Magic:
			player.attributes.magic.base = std::clamp(player.attributes.magic.base, 0, maxStatPoint);
			break;
		case CharacterAttribute::Dexterity:
			player.attributes.dexterity.base = std::clamp(player.attributes.dexterity.base, 0, maxStatPoint);
			break;
		case CharacterAttribute::Vitality:
			player.attributes.vitality.base = std::clamp(player.attributes.vitality.base, 0, maxStatPoint);
			break;
		}
	}
}

void Player::modifyStrength(int l)
{
	if (!LegacyPlayerStateMutationAllowed())
		return;

	Player &player = *this;
	l = std::clamp(l, 0 - player.attributes.strength.base, player.GetMaximumAttributeValue(CharacterAttribute::Strength) - player.attributes.strength.base);

	player.attributes.strength.current += l;
	player.attributes.strength.base += l;

	CalcPlrInv(player, true);

	if (&player == MyPlayer) {
		NetSendCmdParam1(false, CMD_SETSTR, player.attributes.strength.base);
	}
}

void Player::modifyMagic(int l)
{
	if (!LegacyPlayerStateMutationAllowed())
		return;

	Player &player = *this;
	l = std::clamp(l, 0 - player.attributes.magic.base, player.GetMaximumAttributeValue(CharacterAttribute::Magic) - player.attributes.magic.base);

	player.attributes.magic.current += l;
	player.attributes.magic.base += l;

	int ms = l;
	ms *= player.getClassAttributes().chrMana;

	player.mana.maximumBase += ms;
	player.mana.maximum += ms;
	if (HasNoneOf(player._pIFlags, ItemSpecialEffect::NoMana)) {
		player.mana.base += ms;
		player.mana.current += ms;
	}

	CalcPlrInv(player, true);

	if (&player == MyPlayer) {
		NetSendCmdParam1(false, CMD_SETMAG, player.attributes.magic.base);
	}
}

void Player::modifyDexterity(int l)
{
	if (!LegacyPlayerStateMutationAllowed())
		return;

	Player &player = *this;
	l = std::clamp(l, 0 - player.attributes.dexterity.base, player.GetMaximumAttributeValue(CharacterAttribute::Dexterity) - player.attributes.dexterity.base);

	player.attributes.dexterity.current += l;
	player.attributes.dexterity.base += l;
	CalcPlrInv(player, true);

	if (&player == MyPlayer) {
		NetSendCmdParam1(false, CMD_SETDEX, player.attributes.dexterity.base);
	}
}

void Player::modifyVitality(int l)
{
	if (!LegacyPlayerStateMutationAllowed())
		return;

	Player &player = *this;
	l = std::clamp(l, 0 - player.attributes.vitality.base, player.GetMaximumAttributeValue(CharacterAttribute::Vitality) - player.attributes.vitality.base);

	player.attributes.vitality.current += l;
	player.attributes.vitality.base += l;

	int ms = l;
	ms *= player.getClassAttributes().chrLife;

	player.life.base += ms;
	player.life.maximumBase += ms;
	player.life.current += ms;
	player.life.maximum += ms;

	CalcPlrInv(player, true);

	if (&player == MyPlayer) {
		NetSendCmdParam1(false, CMD_SETVIT, player.attributes.vitality.base);
	}
}

void Player::setHitPoints(int val)
{
	if (!LegacyPlayerStateMutationAllowed())
		return;

	Player &player = *this;
	player.life.current = val;
	player.life.base = val + player.life.maximumBase - player.life.maximum;

	if (&player == MyPlayer) {
		RedrawComponent(PanelDrawComponent::Health);
	}
}

void Player::setStrength(int v)
{
	if (!LegacyPlayerStateMutationAllowed())
		return;

	Player &player = *this;
	player.attributes.strength.base = v;
	CalcPlrInv(player, true);
}

void Player::setMagic(int v)
{
	if (!LegacyPlayerStateMutationAllowed())
		return;

	Player &player = *this;
	player.attributes.magic.base = v;

	int m = v;
	m *= player.getClassAttributes().chrMana;

	player.mana.maximumBase = m;
	player.mana.maximum = m;
	CalcPlrInv(player, true);
}

void Player::setDexterity(int v)
{
	if (!LegacyPlayerStateMutationAllowed())
		return;

	Player &player = *this;
	player.attributes.dexterity.base = v;
	CalcPlrInv(player, true);
}

void Player::setVitality(int v)
{
	if (!LegacyPlayerStateMutationAllowed())
		return;

	Player &player = *this;
	player.attributes.vitality.base = v;

	int hp = v;
	hp *= player.getClassAttributes().chrLife;

	player.life.base = hp;
	player.life.maximumBase = hp;
	CalcPlrInv(player, true);
}

void Player::initDungeonMessages()
{
	Player &player = *this;
	player.pDungMsgs = 0;
	player.pDungMsgs2 = 0;
}

enum {
	// clang-format off
	DungMsgCathedral = 1 << 0,
	DungMsgCatacombs = 1 << 1,
	DungMsgCaves     = 1 << 2,
	DungMsgHell      = 1 << 3,
	DungMsgDiablo    = 1 << 4,
	// clang-format on
};

void PlayDungMsgs()
{
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacyWorldStateMessage(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
	#endif

	assert(MyPlayer != nullptr);
	Player &myPlayer = *MyPlayer;

	if (!isSetLevel() && currentLevelNumber() == 1 && !myPlayer._pLvlVisited[1] && (myPlayer.pDungMsgs & DungMsgCathedral) == 0) {
		myPlayer.Say(HeroSpeech::TheSanctityOfThisPlaceHasBeenFouled, 40);
		myPlayer.pDungMsgs = myPlayer.pDungMsgs | DungMsgCathedral;
	} else if (!isSetLevel() && currentLevelNumber() == 5 && !myPlayer._pLvlVisited[5] && (myPlayer.pDungMsgs & DungMsgCatacombs) == 0) {
		myPlayer.Say(HeroSpeech::TheSmellOfDeathSurroundsMe, 40);
		myPlayer.pDungMsgs |= DungMsgCatacombs;
	} else if (!isSetLevel() && currentLevelNumber() == 9 && !myPlayer._pLvlVisited[9] && (myPlayer.pDungMsgs & DungMsgCaves) == 0) {
		myPlayer.Say(HeroSpeech::ItsHotDownHere, 40);
		myPlayer.pDungMsgs |= DungMsgCaves;
	} else if (!isSetLevel() && currentLevelNumber() == 13 && !myPlayer._pLvlVisited[13] && (myPlayer.pDungMsgs & DungMsgHell) == 0) {
		myPlayer.Say(HeroSpeech::IMustBeGettingClose, 40);
		myPlayer.pDungMsgs |= DungMsgHell;
	} else if (!isSetLevel() && currentLevelNumber() == 16 && !myPlayer._pLvlVisited[16] && (myPlayer.pDungMsgs & DungMsgDiablo) == 0) {
		for (auto &monster : MonsterPoolAdapter::AllMonsters()) {
			if (monster.type().type != MT_DIABLO) continue;
			if (monster.hitPoints > 0) {
				sfxdelay = 40;
				sfxdnum = SfxID::DiabloGreeting;
				myPlayer.pDungMsgs |= DungMsgDiablo;
			}
			break;
		}
	} else if (!isSetLevel() && currentLevelNumber() == 17 && !myPlayer._pLvlVisited[17] && (myPlayer.pDungMsgs2 & 1) == 0) {
		sfxdelay = 10;
		sfxdnum = SfxID::Defiler1;
		Quests[Q_DEFILER]._qactive = QUEST_ACTIVE;
		Quests[Q_DEFILER]._qlog = true;
		Quests[Q_DEFILER]._qmsg = TEXT_DEFILER1;
		NetSendCmdQuest(true, Quests[Q_DEFILER]);
		myPlayer.pDungMsgs2 |= 1;
	} else if (!isSetLevel() && currentLevelNumber() == 19 && !myPlayer._pLvlVisited[19] && (myPlayer.pDungMsgs2 & 4) == 0) {
		sfxdelay = 10;
		sfxdnum = SfxID::Defiler3;
		myPlayer.pDungMsgs2 |= 4;
	} else if (!isSetLevel() && currentLevelNumber() == 21 && !myPlayer._pLvlVisited[21] && (myPlayer.pDungMsgs & 32) == 0) {
		myPlayer.Say(HeroSpeech::ThisIsAPlaceOfGreatPower, 30);
		myPlayer.pDungMsgs |= 32;
	} else if (isSetLevel() && setLevelNumber() == SL_SKELKING && !gbIsSpawn && !myPlayer._pSLvlVisited[SL_SKELKING] && Quests[Q_SKELKING]._qactive == QUEST_ACTIVE) {
		sfxdelay = 10;
		sfxdnum = SfxID::LeoricGreeting;
	} else {
		sfxdelay = 0;
	}
}

#ifdef BUILD_TESTING
void TestPlayerCheckNewPath(Player &player)
{
	CheckNewPath(player, false);
}

// Characterize the actual completion signal passed to the command caller,
// without processing animation or entering the rest of the world tick.
bool TestPlayerProcessAttackCommand(Player &player, bool ranged)
{
	const bool completed = ranged ? DoRangeAttack(player) : DoAttack(player);
	CheckNewPath(player, completed);
	return completed;
}

bool TestPlayerDoGotHit(Player &player)
{
	return DoGotHit(player);
}
#endif

} // namespace devilution

/**
 * @file engine/track.cpp
 *
 * Implementation of functionality tracking what the mouse cursor is pointing at.
 */
#include "engine/track.h"

#include "controls/control_mode.hpp"
#include "controls/game_controls.h"
#include "controls/player_controls.h"
#include "engine/cursor.h"
#include "engine/math/point.hpp"
#include "game/players/players.hpp"
#include "game/stores/stores.hpp"
#include "utils/log.hpp"
#include "utils/timer.hpp"

#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
#include "network/authoritative/server_backed_runtime.hpp"
#endif

namespace devilution {

namespace {

void RepeatWalk(Player &player)
{
	if (!InDungeonBounds(cursPosition))
		return;

	if (player._pmode != PM_STAND && (!player.isWalking() || player.animInfo.getFrameToUseForRendering() <= 6))
		return;

	const Point target = player.GetTargetPosition();
	if (cursPosition == target)
		return;

	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (authoritative::GetServerBackedRuntime().IsConnected()) {
		if (auto result = authoritative::GetServerBackedRuntime().MoveTo(cursPosition.x, cursPosition.y, 0, GetMillisecondsSinceStartup()); !result.has_value())
			LogError("Server-backed repeated walk-to-target failed: {}", result.error());
		return;
	}
	#endif
	NetSendCmdLoc(MyPlayerId, true, CMD_WALKXY, cursPosition);
}

} // namespace

void InvalidateTargets()
{
	if (pcursmonst != -1) {
		const Monster &monster = Monsters[pcursmonst];
		if (monster.isInvalid || monster.hasNoLife()
		    || (monster.flags & MFLAG_HIDDEN) != 0
		    || !IsTileLit(monster.position.tile)) {
			pcursmonst = -1;
		}
	}

	if (ObjectUnderCursor != nullptr && !ObjectUnderCursor->canInteractWith())
		ObjectUnderCursor = nullptr;

	if (PlayerUnderCursor != nullptr) {
		const Player &targetPlayer = *PlayerUnderCursor;
		if (targetPlayer._pmode == PM_DEATH || targetPlayer._pmode == PM_QUIT || !targetPlayer.plractive
		    || !targetPlayer.isOnActiveLevel() || targetPlayer.hasNoLife()
		    || !IsTileLit(targetPlayer.position.tile))
			PlayerUnderCursor = nullptr;
	}
}

void RepeatPlayerAction()
{
	if (pcurs != CURSOR_HAND)
		return;

	if (sgbMouseDown == CLICK_NONE && ControllerActionHeld == GameActionType_NONE)
		return;

	if (CurrentStoreManager.IsPlayerInStore())
		return;

	if (LastPlayerAction == PlayerActionType::None)
		return;

	Player &myPlayer = *MyPlayer;
	if (myPlayer.destAction != ACTION_NONE)
		return;
	if (myPlayer._pInvincible)
		return;
	if (!myPlayer.CanChangeAction())
		return;

	const bool rangedAttack = myPlayer.UsesRangedWeapon();
	switch (LastPlayerAction) {
	case PlayerActionType::Attack:
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		if (authoritative::GetServerBackedRuntime().IsConnected()) {
			if (auto result = authoritative::GetServerBackedRuntime().AttackAt(cursPosition.x, cursPosition.y, 0, GetMillisecondsSinceStartup()); !result.has_value())
				LogError("Server-backed repeated cell attack failed: {}", result.error());
			break;
		}
	#endif
		if (InDungeonBounds(cursPosition))
			NetSendCmdLoc(MyPlayerId, true, rangedAttack ? CMD_RATTACKXY : CMD_SATTACKXY, cursPosition);
		break;
	case PlayerActionType::AttackMonsterTarget:
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		if (authoritative::GetServerBackedRuntime().IsConnected()) {
			if (pcursmonst == -1)
				return;
			const auto entityId = authoritative::GetServerBackedRuntime().MonsterEntityIdAt(cursPosition.x, cursPosition.y);
			if (!entityId.has_value()) {
				LogError("Server-backed repeated monster target is not an authoritative entity.");
			} else if (auto result = authoritative::GetServerBackedRuntime().Attack(*entityId, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
				LogError("Server-backed repeated monster attack failed: {}", result.error());
			}
			return;
		}
	#endif
		if (pcursmonst != -1)
			NetSendCmdParam1(true, rangedAttack ? CMD_RATTACKID : CMD_ATTACKID, pcursmonst);
		break;
	case PlayerActionType::AttackPlayerTarget:
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		if (authoritative::GetServerBackedRuntime().IsConnected()) {
			if (PlayerUnderCursor == nullptr || myPlayer.friendlyMode)
				return;
			const auto entityId = authoritative::GetServerBackedRuntime().PlayerEntityIdAt(cursPosition.x, cursPosition.y);
			if (!entityId.has_value()) {
				LogError("Server-backed repeated player target is not an authoritative entity.");
			} else if (auto result = authoritative::GetServerBackedRuntime().Attack(*entityId, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
				LogError("Server-backed repeated player attack failed: {}", result.error());
			}
			return;
		}
	#endif
		if (PlayerUnderCursor != nullptr && !myPlayer.friendlyMode)
			NetSendCmdParam1(true, rangedAttack ? CMD_RATTACKPID : CMD_ATTACKPID, PlayerUnderCursor->getId());
		break;
	case PlayerActionType::Spell:
		if (ControlMode != ControlTypes::KeyboardAndMouse) {
			UpdateSpellTarget(MyPlayer->_pRSpell);
		}
		CheckPlrSpell(ControlMode == ControlTypes::KeyboardAndMouse);
		break;
	case PlayerActionType::SpellMonsterTarget:
		if (pcursmonst != -1)
			CheckPlrSpell(false);
		break;
	case PlayerActionType::SpellPlayerTarget:
		if (PlayerUnderCursor != nullptr && !myPlayer.friendlyMode)
			CheckPlrSpell(false);
		break;
	case PlayerActionType::OperateObject:
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		if (authoritative::GetServerBackedRuntime().IsConnected()) {
			if (ObjectUnderCursor == nullptr || ObjectUnderCursor->isDoor())
				return;
			const auto entityId = authoritative::GetServerBackedRuntime().AuthoritativeObjectEntityIdAt(cursPosition.x, cursPosition.y);
			if (!entityId.has_value()) {
				LogError("Server-backed repeated object target is not an authoritative entity.");
			} else if (pcurs == CURSOR_DISARM) {
				if (auto result = authoritative::GetServerBackedRuntime().DisarmTrap(*entityId, 0, GetMillisecondsSinceStartup()); !result.has_value())
					LogError("Server-backed repeated trap disarm failed: {}", result.error());
			} else if (auto result = authoritative::GetServerBackedRuntime().OperateObject(*entityId, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
				LogError("Server-backed repeated object interaction failed: {}", result.error());
			}
			return;
		}
	#endif
		if (ObjectUnderCursor != nullptr && !ObjectUnderCursor->isDoor()) {
			NetSendCmdLoc(MyPlayerId, true, CMD_OPOBJXY, cursPosition);
		}
		break;
	case PlayerActionType::Walk:
		RepeatWalk(myPlayer);
		break;
	case PlayerActionType::None:
		break;
	}
}

bool track_isscrolling()
{
	return LastPlayerAction == PlayerActionType::Walk;
}

} // namespace devilution

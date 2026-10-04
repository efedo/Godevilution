/**
 * @file application/diablo.cpp
 *
 * Implementation of the main game initialization functions.
 */
#include "application/diablo.h"
#include <array>
#include <cstdint>
#include <optional>
#include <string_view>

#ifdef USE_SDL3
#include <SDL3/SDL_events.h>
#include <SDL3/SDL_init.h>
#include <SDL3/SDL_keycode.h>
#else
#include <SDL.h>

#ifdef USE_SDL1
#include "utils/sdl/sdl2_to_1_2_backports.h"
#endif
#endif

#include <fmt/format.h>

#include <config.h>

#include "ui/menu/startup_selection.h"
#include "application/fatal_error.h"
#include "ui/automap.h"
#include "application/capture.h"
#include "ui/panel/control.hpp"
#include "engine/cursor.h"
#include "game/monsters/monster_corpses.hpp"
#ifdef _DEBUG
#include "application/debug.h"
#include "ui/debug_overlay/imgui_overlay.hpp"
#endif
#include "ui/menu/diabloui.h"
#include "controls/control_mode.hpp"
#include "controls/keymapper.hpp"
#include "controls/player_controls.h"
#include "controls/remap_keyboard.h"
#include "application/diablo.h"
#include "ui/diablo_msg.hpp"

#include "game/quests/quest_stars.hpp"
#include "utils/crypto/encrypt.h"
#include "engine/gfx/backbuffer_state.hpp"
#include "engine/gfx/clx_sprite.hpp"
#include "engine/demomode.h"
#include "engine/gfx/graphics_pipeline.h"
#include "engine/sdl_events.hpp"
#include "engine/load/load_cel.hpp"
#include "engine/load/load_file.hpp"
#include "engine/random.hpp"
#include "engine/render/clx_render.hpp"
#include "engine/audio/sound.h"
#include "application/game_mode.hpp"
#include "ui/gamemenu.h"
#include "ui/menu_framework.h"
#include "application/headless_mode.hpp"
#include "ui/help.h"
#include "engine/hwcursor.hpp"
#include "application/init.hpp"
#include "data/mod_manager.hpp"
#include "game/players/inventory.hpp"
#include "game/levels/level_l1.h"
#include "game/levels/level_l2.h"
#include "game/levels/level_l3.h"
#include "game/levels/level_l4.h"
#include "game/levels/dungeon_common.h"
#include "game/levels/setmaps.h"
#include "game/levels/themes.h"
#include "game/levels/town.h"
#include "game/levels/triggers.h"
#include "engine/lighting.h"
#include "persistence/loadsave.h"
#include "game/events/event_bus.hpp"
#include "lua/lua_global.hpp"
#include "ui/menu.h"
#include "ui/minitext.h"
#include "game/missiles/missiles.hpp"
#include "game/monsters/monster_pool.hpp"
#include "ui/movie.h"
#include "network/protocol/multi.h"
#include "network/protocol/legacy_lua_policy.hpp"
#include "network/protocol/network_ticks.h"
#include "network/protocol/legacy_world_state_policy.hpp"
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
#include "network/authoritative/server_backed_configuration.hpp"
#include "network/authoritative/server_backed_runtime.hpp"
#include "utils/ui_fwd.h"
#endif
#include "game/objects/objects.hpp"
#include "game/objects/object_pool.hpp"
#include "persistence/options.h"
#include "ui/panel/console.hpp"
#include "ui/panel/info_box.hpp"
#include "ui/panel/partypanel.hpp"
#include "ui/panel/spell_book.hpp"
#include "ui/panel/spell_list.hpp"
#include "persistence/save_file.h"
#include "ui/player_messages.h"
#include "qol/chatlog.h"
#include "qol/floatingnumbers.h"
#include "qol/itemlabels.h"
#include "qol/monhealthbar.h"
#include "qol/stash.h"
#include "qol/visual_store.h"
#include "qol/xpbar.h"
#include "ui/quick_messages.hpp"
#include "application/restrict.h"
#include "game/stores/stores.hpp"
#include "network/storm/storm_net.hpp"
#include "network/storm/storm_svid.h"
#include "tables/leveldat.h"
#include "tables/monstdat.h"
#include "tables/objdat.h"
#include "tables/playerdat.hpp"
#include "game/towners/towners.hpp"
#include "engine/track.h"
#include "utils/console.h"
#include "utils/display.h"
#include "utils/is_of.hpp"
#include "utils/language.h"
#include "utils/parse_int.hpp"
#include "utils/paths.h"
#include "utils/screen_reader.hpp"
#include "utils/sdl/sdl_compat.h"
#include "utils/sdl/sdl_thread.h"
#include "utils/status_macros.hpp"
#include "utils/string/str_cat.hpp"
#include "utils/string/utf8.hpp"
#include "utils/timer.hpp"

#ifndef USE_SDL1
#include "controls/touch/gamepad.h"
#include "controls/touch/renderers.h"
#endif

#ifdef __vita__
#include "platform/vita/touch.h"
#endif

#ifdef GPERF_HEAP_FIRST_GAME_ITERATION
#include <gperftools/heap-profiler.h>
#endif

namespace devilution {

uint32_t DungeonSeeds[NUMLEVELS];
std::optional<uint32_t> LevelSeeds[NUMLEVELS];
Point MousePosition;
bool gbRunGameResult;
bool ReturnToMainMenu;
/** Enable updating of player character, set to false once Diablo dies */
bool gbProcessPlayers;
bool gbLoadGame;
bool cineflag;
int PauseMode;
clicktype sgbMouseDown;
uint16_t gnTickDelay = 50;
char gszProductName[64] = "DevilutionX vUnknown";

#ifdef _DEBUG
bool DebugDisableNetworkTimeout = false;
std::vector<std::string> DebugCmdsFromCommandLine;
#endif
GameLogicStep gGameLogicStep = GameLogicStep::None;

/** This and the following mouse variables are for handling in-game click-and-hold actions */
PlayerActionType LastPlayerAction = PlayerActionType::None;

// Controller support: Actions to run after updating the cursor state.
// Defined in SourceX/controls/plctrls.cpp.
extern void plrctrls_after_check_curs_move();
extern void plrctrls_every_frame();
extern void plrctrls_after_game_logic();

namespace {

char gszVersionNumber[64] = "internal version unknown";

bool gbGameLoopStartup;
bool forceSpawn;
bool forceDiablo;
int sgnTimeoutCurs;
bool gbShowIntro = true;
/** To know if these things have been done when we get to the diablo_deinit() function */
bool was_archives_init = false;
/** To know if surfaces have been initialized or not */
bool was_window_init = false;
bool was_ui_init = false;

bool LegacyLevelLoadMutationAllowed() noexcept
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	return ShouldProcessLegacyLevelLoadMutation(authoritative::GetServerBackedRuntime().IsConnected());
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

void StartGame(interface_mode uMsg)
{
	CalcViewportGeometry();
	cineflag = false;
	InitCursor();
#ifdef _DEBUG
	LoadDebugGFX();
#endif
	assert(HeadlessMode || ghMainWnd);
	music_stop();
	InitMonsterHealthBar();
	InitXPBar();
	ShowProgress(uMsg);
	gmenu_init_menu();
	InitLevelCursor();
	sgnTimeoutCurs = CURSOR_NONE;
	sgbMouseDown = CLICK_NONE;
	LastPlayerAction = PlayerActionType::None;
}

void FreeGame()
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	authoritative::GetServerBackedRuntime().Stop();
#endif
	FreeMonsterHealthBar();
	FreeXPBar();
	FreeControlPan();
	FreeInvGFX();
	FreeGMenu();
	FreeQuestText();
	FreeInfoBoxGfx();
	CurrentStoreManager.FreeStoreMem();

	for (Player &player : Players)
		player.resetGraphics();

	FreeCursor();
#ifdef _DEBUG
	FreeDebugGFX();
#endif
	FreeGameMem();
	stream_stop();
	music_stop();
}

bool ProcessInput()
{
	if (PauseMode == 2) {
		return false;
	}

	plrctrls_every_frame();

	if (!gbIsMultiplayer && gmenu_is_active()) {
		RedrawViewport();
		return false;
	}

	if (!gmenu_is_active() && sgnTimeoutCurs == CURSOR_NONE) {
#ifdef __vita__
		FinishSimulatedMouseClicks(MousePosition);
#endif
		CheckCursMove();
		plrctrls_after_check_curs_move();
		RepeatPlayerAction();
	}

	return true;
}

void LeftMouseCmd(bool bShift)
{
	bool bNear;

	assert(!GetMainPanel().contains(MousePosition));

	if (levelType() == DTYPE_TOWN) {
		CloseGoldWithdraw();
		CloseStash();
		CloseVisualStore();
		if (pcursitem != -1 && pcurs == CURSOR_HAND) {
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			if (authoritative::GetServerBackedRuntime().IsConnected()
			    && MyPlayer->position.tile.WalkingDistance(cursPosition) < 2) {
				const auto entityId = authoritative::GetServerBackedRuntime().WorldItemEntityIdAt(cursPosition.x, cursPosition.y);
				if (!entityId.has_value()) {
					LogError("Server-backed town item target is not an authoritative entity.");
				} else if (auto result = authoritative::GetServerBackedRuntime().PickupWorldItem(*entityId, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
					LogError("Server-backed town item pickup failed: {}", result.error());
				}
				return;
			}
		#endif
			NetSendCmdLocParam1(true, invflag ? CMD_GOTOGETITEM : CMD_GOTOAGETITEM, cursPosition, pcursitem);
		}
		if (pcursmonst != -1) {
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			if (authoritative::GetServerBackedRuntime().IsConnected()) {
				TalkToTowner(*MyPlayer, pcursmonst);
				return;
			}
	#endif
			NetSendCmdLocParam1(true, CMD_TALKXY, cursPosition, pcursmonst);
		}
		if (pcursitem == -1 && pcursmonst == -1 && PlayerUnderCursor == nullptr) {
			LastPlayerAction = PlayerActionType::Walk;
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			if (authoritative::GetServerBackedRuntime().IsConnected()) {
				if (auto result = authoritative::GetServerBackedRuntime().MoveTo(cursPosition.x, cursPosition.y, 0, GetMillisecondsSinceStartup()); !result.has_value())
					LogError("Server-backed town walk-to-target failed: {}", result.error());
				return;
			}
	#endif
			NetSendCmdLoc(MyPlayerId, true, CMD_WALKXY, cursPosition);
		}
		return;
	}

	const Player &myPlayer = *MyPlayer;
	bNear = myPlayer.position.tile.WalkingDistance(cursPosition) < 2;
	if (pcursitem != -1 && pcurs == CURSOR_HAND && !bShift) {
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		if (authoritative::GetServerBackedRuntime().IsConnected() && bNear) {
			const auto entityId = authoritative::GetServerBackedRuntime().WorldItemEntityIdAt(cursPosition.x, cursPosition.y);
			if (!entityId.has_value()) {
				LogError("Server-backed item target is not an authoritative entity.");
			} else if (auto result = authoritative::GetServerBackedRuntime().PickupWorldItem(*entityId, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
				LogError("Server-backed item pickup failed: {}", result.error());
			}
			return;
		}
	#endif
		NetSendCmdLocParam1(true, invflag ? CMD_GOTOGETITEM : CMD_GOTOAGETITEM, cursPosition, pcursitem);
	} else if (ObjectUnderCursor != nullptr && !ObjectUnderCursor->IsDisabled() && (!bShift || (bNear && ObjectUnderCursor->_oBreak == 1))) {
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		if (authoritative::GetServerBackedRuntime().IsConnected()) {
			const auto entityId = authoritative::GetServerBackedRuntime().AuthoritativeObjectEntityIdAt(cursPosition.x, cursPosition.y);
			if (!entityId.has_value()) {
				LogError("Server-backed object target is not an authoritative entity.");
			} else if (pcurs == CURSOR_DISARM) {
				if (auto result = authoritative::GetServerBackedRuntime().DisarmTrap(*entityId, 0, GetMillisecondsSinceStartup()); !result.has_value())
					LogError("Server-backed trap disarm failed: {}", result.error());
			} else if (auto result = authoritative::GetServerBackedRuntime().OperateObject(*entityId, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
				LogError("Server-backed object interaction failed: {}", result.error());
			}
			LastPlayerAction = PlayerActionType::OperateObject;
			return;
		}
	#endif
		LastPlayerAction = PlayerActionType::OperateObject;
		NetSendCmdLoc(MyPlayerId, true, pcurs == CURSOR_DISARM ? CMD_DISARMXY : CMD_OPOBJXY, cursPosition);
	} else if (myPlayer.UsesRangedWeapon()) {
		if (bShift) {
		#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			if (authoritative::GetServerBackedRuntime().IsConnected()) {
				if (auto result = authoritative::GetServerBackedRuntime().AttackAt(cursPosition.x, cursPosition.y, 0, GetMillisecondsSinceStartup()); !result.has_value())
					LogError("Server-backed ranged cell attack failed: {}", result.error());
				LastPlayerAction = PlayerActionType::Attack;
				return;
			}
		#endif
			LastPlayerAction = PlayerActionType::Attack;
			NetSendCmdLoc(MyPlayerId, true, CMD_RATTACKXY, cursPosition);
		} else if (pcursmonst != -1) {
			if (Monsters[pcursmonst].canTalk()) {
			#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
				if (authoritative::GetServerBackedRuntime().IsConnected()) {
					TalktoMonster(*MyPlayer, Monsters[pcursmonst]);
					return;
				}
			#endif
				NetSendCmdParam1(true, CMD_ATTACKID, pcursmonst);
			} else {
			#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
				if (authoritative::GetServerBackedRuntime().IsConnected()) {
					const auto entityId = authoritative::GetServerBackedRuntime().MonsterEntityIdAt(cursPosition.x, cursPosition.y);
					if (!entityId.has_value()) {
						LogError("Server-backed monster target is not an authoritative entity.");
					} else if (auto result = authoritative::GetServerBackedRuntime().Attack(*entityId, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
						LogError("Server-backed monster attack failed: {}", result.error());
					}
					LastPlayerAction = PlayerActionType::AttackMonsterTarget;
					return;
				}
			#endif
				LastPlayerAction = PlayerActionType::AttackMonsterTarget;
				NetSendCmdParam1(true, CMD_RATTACKID, pcursmonst);
			}
		} else if (PlayerUnderCursor != nullptr && !PlayerUnderCursor->hasNoLife() && !myPlayer.friendlyMode) {
		#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			if (authoritative::GetServerBackedRuntime().IsConnected()) {
				const auto entityId = authoritative::GetServerBackedRuntime().PlayerEntityIdAt(cursPosition.x, cursPosition.y);
				if (!entityId.has_value()) {
					LogError("Server-backed player target is not an authoritative entity.");
				} else if (auto result = authoritative::GetServerBackedRuntime().Attack(*entityId, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
					LogError("Server-backed player attack failed: {}", result.error());
				}
				LastPlayerAction = PlayerActionType::AttackPlayerTarget;
				return;
			}
		#endif
			LastPlayerAction = PlayerActionType::AttackPlayerTarget;
			NetSendCmdParam1(true, CMD_RATTACKPID, PlayerUnderCursor->getId());
		}
	} else {
		if (bShift) {
			if (pcursmonst != -1) {
				if (Monsters[pcursmonst].canTalk()) {
				#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
					if (authoritative::GetServerBackedRuntime().IsConnected()) {
						TalktoMonster(*MyPlayer, Monsters[pcursmonst]);
						return;
					}
				#endif
					NetSendCmdParam1(true, CMD_ATTACKID, pcursmonst);
				} else {
					LastPlayerAction = PlayerActionType::Attack;
				#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
					if (authoritative::GetServerBackedRuntime().IsConnected()) {
						if (auto result = authoritative::GetServerBackedRuntime().AttackAt(cursPosition.x, cursPosition.y, 0, GetMillisecondsSinceStartup()); !result.has_value())
							LogError("Server-backed melee cell attack failed: {}", result.error());
						return;
					}
				#endif
					NetSendCmdLoc(MyPlayerId, true, CMD_SATTACKXY, cursPosition);
				}
			} else {
				LastPlayerAction = PlayerActionType::Attack;
			#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
				if (authoritative::GetServerBackedRuntime().IsConnected()) {
					if (auto result = authoritative::GetServerBackedRuntime().AttackAt(cursPosition.x, cursPosition.y, 0, GetMillisecondsSinceStartup()); !result.has_value())
						LogError("Server-backed melee cell attack failed: {}", result.error());
					return;
				}
			#endif
				NetSendCmdLoc(MyPlayerId, true, CMD_SATTACKXY, cursPosition);
			}
		} else if (pcursmonst != -1) {
		#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			if (Monsters[pcursmonst].canTalk() && authoritative::GetServerBackedRuntime().IsConnected()) {
				TalktoMonster(*MyPlayer, Monsters[pcursmonst]);
				return;
			}
		#endif
		#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			if (!Monsters[pcursmonst].canTalk() && authoritative::GetServerBackedRuntime().IsConnected()) {
				const auto entityId = authoritative::GetServerBackedRuntime().MonsterEntityIdAt(cursPosition.x, cursPosition.y);
				if (!entityId.has_value()) {
					LogError("Server-backed monster target is not an authoritative entity.");
				} else if (auto result = authoritative::GetServerBackedRuntime().Attack(*entityId, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
					LogError("Server-backed monster attack failed: {}", result.error());
				}
				LastPlayerAction = PlayerActionType::AttackMonsterTarget;
				return;
			}
		#endif
			LastPlayerAction = PlayerActionType::AttackMonsterTarget;
			NetSendCmdParam1(true, CMD_ATTACKID, pcursmonst);
		} else if (PlayerUnderCursor != nullptr && !PlayerUnderCursor->hasNoLife() && !myPlayer.friendlyMode) {
		#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			if (authoritative::GetServerBackedRuntime().IsConnected()) {
				const auto entityId = authoritative::GetServerBackedRuntime().PlayerEntityIdAt(cursPosition.x, cursPosition.y);
				if (!entityId.has_value()) {
					LogError("Server-backed player target is not an authoritative entity.");
				} else if (auto result = authoritative::GetServerBackedRuntime().Attack(*entityId, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
					LogError("Server-backed player attack failed: {}", result.error());
				}
				LastPlayerAction = PlayerActionType::AttackPlayerTarget;
				return;
			}
		#endif
			LastPlayerAction = PlayerActionType::AttackPlayerTarget;
			NetSendCmdParam1(true, CMD_ATTACKPID, PlayerUnderCursor->getId());
		}
	}
	if (!bShift && pcursitem == -1 && ObjectUnderCursor == nullptr && pcursmonst == -1 && PlayerUnderCursor == nullptr) {
		LastPlayerAction = PlayerActionType::Walk;
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		if (authoritative::GetServerBackedRuntime().IsConnected()) {
			if (auto result = authoritative::GetServerBackedRuntime().MoveTo(cursPosition.x, cursPosition.y, 0, GetMillisecondsSinceStartup()); !result.has_value())
				LogError("Server-backed walk-to-target failed: {}", result.error());
			return;
		}
	#endif
		NetSendCmdLoc(MyPlayerId, true, CMD_WALKXY, cursPosition);
	}
}

bool TryOpenDungeonWithMouse()
{
	if (levelType() != DTYPE_TOWN)
		return false;

	const Item &holdItem = MyPlayer->HoldItem;
	if (holdItem.IDidx == IDI_RUNEBOMB && OpensHive(cursPosition)) {
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		if (authoritative::GetServerBackedRuntime().IsConnected()) {
			const auto entityId = authoritative::GetServerBackedRuntime().AuthoritativeObjectEntityIdAt(cursPosition.x, cursPosition.y);
			if (!entityId.has_value())
				LogError("Server-backed Rune Bomb use found no authoritative hive object.");
			else if (auto result = authoritative::GetServerBackedRuntime().OperateObject(*entityId, 0, GetMillisecondsSinceStartup()); !result.has_value())
				LogError("Server-backed Rune Bomb hive interaction failed: {}", result.error());
			NewCursor(CURSOR_HAND);
			return true;
		}
#endif
		OpenHive();
	} else if (holdItem.IDidx == IDI_MAPOFDOOM && OpensGrave(cursPosition)) {
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		if (authoritative::GetServerBackedRuntime().IsConnected()) {
			const auto entityId = authoritative::GetServerBackedRuntime().AuthoritativeObjectEntityIdAt(cursPosition.x, cursPosition.y);
			if (!entityId.has_value())
				LogError("Server-backed Cathedral Map use found no authoritative grave object.");
			else if (auto result = authoritative::GetServerBackedRuntime().OperateObject(*entityId, 0, GetMillisecondsSinceStartup()); !result.has_value())
				LogError("Server-backed Cathedral Map grave interaction failed: {}", result.error());
			NewCursor(CURSOR_HAND);
			return true;
		}
#endif
		OpenGrave();
	} else
		return false;

	NewCursor(CURSOR_HAND);
	return true;
}

void LeftMouseDown(uint16_t modState)
{
	LastPlayerAction = PlayerActionType::None;

	if (gmenu_left_mouse(true))
		return;

	if (CheckMuteButton())
		return;

	if (sgnTimeoutCurs != CURSOR_NONE)
		return;

	if (MyPlayerIsDead) {
		CheckMainPanelButtonDead();
		return;
	}

	if (PauseMode == 2) {
		return;
	}
	if (DoomFlag) {
		doom_close();
		return;
	}

	if (SpellSelectFlag) {
		SetSpell();
		return;
	}

	if (CurrentStoreManager.IsPlayerInStore()) {
		CurrentStoreManager.CheckStoreBtn();
		return;
	}

	const bool isShiftHeld = (modState & SDL_KMOD_SHIFT) != 0;
	const bool isCtrlHeld = (modState & SDL_KMOD_CTRL) != 0;

	if (!GetMainPanel().contains(MousePosition)) {
		if (!gmenu_is_active() && !TryIconCurs()) {
			if (QuestLogIsOpen && GetLeftPanel().contains(MousePosition)) {
				QuestlogESC();
			} else if (qtextflag) {
				qtextflag = false;
				stream_stop();
			} else if (CharFlag && GetLeftPanel().contains(MousePosition)) {
				CheckChrBtns();
			} else if (invflag && GetRightPanel().contains(MousePosition)) {
				if (!DropGoldFlag)
					CheckInvItem(isShiftHeld, isCtrlHeld);
			} else if (IsStashOpen && GetLeftPanel().contains(MousePosition)) {
				if (!IsWithdrawGoldOpen)
					CheckStashItem(MousePosition, isShiftHeld, isCtrlHeld);
				CheckStashButtonPress(MousePosition);
			} else if (IsVisualStoreOpen && GetLeftPanel().contains(MousePosition)) {
				if (!MyPlayer->HoldItem.isEmpty()) {
					CheckVisualStorePaste(MousePosition);
				} else {
					CheckVisualStoreItem(MousePosition, isCtrlHeld, isShiftHeld);
				}
				CheckVisualStoreButtonPress(MousePosition);
			} else if (SpellbookFlag && GetRightPanel().contains(MousePosition)) {
				CheckSBook();
			} else if (!MyPlayer->HoldItem.isEmpty()) {
				if (!TryOpenDungeonWithMouse()) {
					const Point currentPosition = MyPlayer->position.tile;
					std::optional<Point> itemTile = FindAdjacentPositionForItem(currentPosition, GetDirection(currentPosition, cursPosition));
					if (itemTile) {
					#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
						if (authoritative::GetServerBackedRuntime().IsConnected()) {
							if (auto result = authoritative::GetServerBackedRuntime().DropHeldItem(itemTile->x, itemTile->y, 0, GetMillisecondsSinceStartup()); !result.has_value())
								LogError("Server-backed held-item drop failed: {}", result.error());
							else
								NewCursor(CURSOR_HAND);
						} else
					#endif
						{
							NetSendCmdPItem(true, CMD_PUTITEM, *itemTile, MyPlayer->HoldItem);
							NewCursor(CURSOR_HAND);
						}
					}
				}
			} else {
				CheckLevelButton();
				if (!LevelButtonDown)
					LeftMouseCmd(isShiftHeld);
			}
		}
	} else {
		if (!ChatFlag && !DropGoldFlag && !IsWithdrawGoldOpen && !gmenu_is_active())
			CheckInvScrn(isShiftHeld, isCtrlHeld);
		CheckMainPanelButton();
		CheckStashButtonPress(MousePosition);
		if (pcurs > CURSOR_HAND && pcurs < CURSOR_FIRSTITEM)
			NewCursor(CURSOR_HAND);
	}
}

void LeftMouseUp(uint16_t modState)
{
	gmenu_left_mouse(false);
	CheckMuteButtonUp();
	if (MainPanelButtonDown)
		CheckMainPanelButtonUp();
	CheckStashButtonRelease(MousePosition);
	CheckVisualStoreButtonRelease(MousePosition);
	if (CharPanelButtonActive) {
		const bool isShiftHeld = (modState & SDL_KMOD_SHIFT) != 0;
		ReleaseChrBtns(isShiftHeld);
	}
	if (LevelButtonDown)
		CheckLevelButtonUp();
	if (CurrentStoreManager.IsPlayerInStore())
		CurrentStoreManager.ReleaseStoreBtn();
}

void RightMouseDown(bool isShiftHeld)
{
	LastPlayerAction = PlayerActionType::None;

	if (gmenu_is_active() || sgnTimeoutCurs != CURSOR_NONE || PauseMode == 2 || MyPlayer->_pInvincible) {
		return;
	}

	if (qtextflag) {
		qtextflag = false;
		stream_stop();
		return;
	}

	if (DoomFlag) {
		doom_close();
		return;
	}
	if (CurrentStoreManager.IsPlayerInStore())
		return;
	if (SpellSelectFlag) {
		SetSpell();
		return;
	}
	if (SpellbookFlag && GetRightPanel().contains(MousePosition))
		return;
	if (TryIconCurs())
		return;
	if (pcursinvitem != -1 && UseInvItem(pcursinvitem))
		return;
	if (pcursstashitem != StashStruct::EmptyCell && UseStashItem(pcursstashitem))
		return;
	if (DidRightClickPartyPortrait())
		return;
	if (pcurs == CURSOR_HAND) {
		CheckPlrSpell(isShiftHeld);
	} else if (pcurs > CURSOR_HAND && pcurs < CURSOR_FIRSTITEM) {
		NewCursor(CURSOR_HAND);
	}
}

void ReleaseKey(SDL_Keycode vkey)
{
	remap_keyboard_key(&vkey);
	if (sgnTimeoutCurs != CURSOR_NONE)
		return;
	KeymapperRelease(vkey);
}

void ClosePanels()
{
	if (CanPanelsCoverView()) {
		if (!IsLeftPanelOpen() && IsRightPanelOpen() && MousePosition.x < 480 && MousePosition.y < GetMainPanel().position.y) {
			SetCursorPos(MousePosition + Displacement { 160, 0 });
		} else if (!IsRightPanelOpen() && IsLeftPanelOpen() && MousePosition.x > 160 && MousePosition.y < GetMainPanel().position.y) {
			SetCursorPos(MousePosition - Displacement { 160, 0 });
		}
	}
	CloseInventory();
	CloseCharPanel();
	SpellbookFlag = false;
	QuestLogIsOpen = false;
}

void PressKey(SDL_Keycode vkey, uint16_t modState)
{
	Options &options = GetOptions();
	remap_keyboard_key(&vkey);

	if (vkey == SDLK_UNKNOWN)
		return;

	if (gmenu_presskeys(vkey) || CheckKeypress(vkey)) {
		return;
	}

	if (MyPlayerIsDead) {
		if (vkey == SDLK_ESCAPE) {
			if (!gbIsMultiplayer) {
				if (gbValidSaveFile)
					gamemenu_load_game(false);
				else
					gamemenu_exit_game(false);
			} else {
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
				if (authoritative::GetServerBackedRuntime().IsConnected()) {
					if (auto result = authoritative::GetServerBackedRuntime().RestartTown(0, GetMillisecondsSinceStartup()); !result.has_value())
						LogError("Server-backed restart-in-town failed: {}", result.error());
				} else
#endif
				NetSendCmd(true, CMD_RETOWN);
			}
			return;
		}
		if (sgnTimeoutCurs != CURSOR_NONE) {
			return;
		}
		KeymapperPress(vkey);
		if (vkey == SDLK_RETURN || vkey == SDLK_KP_ENTER) {
			if ((modState & SDL_KMOD_ALT) != 0) {
				options.Graphics.fullscreen.SetValue(!IsFullScreen());
				if (!demo::IsRunning()) SaveOptions();
			} else {
				TypeChatMessage();
			}
		}
		if (vkey != SDLK_ESCAPE) {
			return;
		}
	}
	// Disallow player from accessing escape menu during the frames before the death message appears
	if (vkey == SDLK_ESCAPE && MyPlayer->life.current > 0) {
		if (!PressEscKey()) {
			LastPlayerAction = PlayerActionType::None;
			gamemenu_on();
		}
		return;
	}

	if (DropGoldFlag) {
		control_drop_gold(vkey);
		return;
	}
	if (IsWithdrawGoldOpen) {
		WithdrawGoldKeyPress(vkey);
		return;
	}

	if (sgnTimeoutCurs != CURSOR_NONE) {
		return;
	}

	KeymapperPress(vkey);

	if (PauseMode == 2) {
		if ((vkey == SDLK_RETURN || vkey == SDLK_KP_ENTER) && (modState & SDL_KMOD_ALT) != 0) {
			options.Graphics.fullscreen.SetValue(!IsFullScreen());
			if (!demo::IsRunning()) SaveOptions();
		}
		return;
	}

	if (DoomFlag) {
		doom_close();
		return;
	}

	switch (vkey) {
	case SDLK_PLUS:
	case SDLK_KP_PLUS:
	case SDLK_EQUALS:
	case SDLK_KP_EQUALS:
		if (CurrentAutomapManager.GetAutomapActive()) {
			CurrentAutomapManager.AutomapZoomIn();
		}
		return;
	case SDLK_MINUS:
	case SDLK_KP_MINUS:
	case SDLK_UNDERSCORE:
		if (CurrentAutomapManager.GetAutomapActive()) {
			CurrentAutomapManager.AutomapZoomOut();
		}
		return;
#ifdef _DEBUG
	case SDLK_V:
		if ((modState & SDL_KMOD_SHIFT) != 0)
			NextDebugMonster();
		else
			GetDebugMonster();
		return;
#endif
	case SDLK_RETURN:
	case SDLK_KP_ENTER:
		if ((modState & SDL_KMOD_ALT) != 0) {
			options.Graphics.fullscreen.SetValue(!IsFullScreen());
			if (!demo::IsRunning()) SaveOptions();
		} else if (CurrentStoreManager.IsPlayerInStore()) {
			CurrentStoreManager.StoreEnter();
		} else if (QuestLogIsOpen) {
			QuestlogEnter();
		} else {
			TypeChatMessage();
		}
		return;
	case SDLK_UP:
		if (CurrentStoreManager.IsPlayerInStore()) {
			CurrentStoreManager.StoreUp();
		} else if (QuestLogIsOpen) {
			QuestlogUp();
		} else if (HelpFlag) {
			HelpScrollUp();
		} else if (ChatLogFlag) {
			ChatLogScrollUp();
		} else if (CurrentAutomapManager.GetAutomapActive()) {
			CurrentAutomapManager.AutomapUp();
		} else if (IsStashOpen) {
			Stash.PreviousPage();
		}
		return;
	case SDLK_DOWN:
		if (CurrentStoreManager.IsPlayerInStore()) {
			CurrentStoreManager.StoreDown();
		} else if (QuestLogIsOpen) {
			QuestlogDown();
		} else if (HelpFlag) {
			HelpScrollDown();
		} else if (ChatLogFlag) {
			ChatLogScrollDown();
		} else if (CurrentAutomapManager.GetAutomapActive()) {
			CurrentAutomapManager.AutomapDown();
		} else if (IsStashOpen) {
			Stash.NextPage();
		}
		return;
	case SDLK_PAGEUP:
		if (CurrentStoreManager.IsPlayerInStore()) {
			CurrentStoreManager.StorePrior();
		} else if (ChatLogFlag) {
			ChatLogScrollTop();
		}
		return;
	case SDLK_PAGEDOWN:
		if (CurrentStoreManager.IsPlayerInStore()) {
			CurrentStoreManager.StoreNext();
		} else if (ChatLogFlag) {
			ChatLogScrollBottom();
		}
		return;
	case SDLK_LEFT:
		if (CurrentAutomapManager.GetAutomapActive() && !ChatFlag)
			CurrentAutomapManager.AutomapLeft();
		return;
	case SDLK_RIGHT:
		if (CurrentAutomapManager.GetAutomapActive() && !ChatFlag)
			CurrentAutomapManager.AutomapRight();
		return;
	default:
		break;
	}
}

void HandleMouseButtonDown(Uint8 button, uint16_t modState)
{
	if (CurrentStoreManager.IsPlayerInStore() && (button == SDL_BUTTON_X1
#if !SDL_VERSION_ATLEAST(2, 0, 0)
	        || button == 8
#endif
	        )) {
		CurrentStoreManager.StoreESC();
		return;
	}

	switch (button) {
	case SDL_BUTTON_LEFT:
		if (sgbMouseDown == CLICK_NONE) {
			sgbMouseDown = CLICK_LEFT;
			LeftMouseDown(modState);
		}
		break;
	case SDL_BUTTON_RIGHT:
		if (sgbMouseDown == CLICK_NONE) {
			sgbMouseDown = CLICK_RIGHT;
			RightMouseDown((modState & SDL_KMOD_SHIFT) != 0);
		}
		break;
	default:
		KeymapperPress(static_cast<SDL_Keycode>(button | KeymapperMouseButtonMask));
		break;
	}
}

void HandleMouseButtonUp(Uint8 button, uint16_t modState)
{
	if (sgbMouseDown == CLICK_LEFT && button == SDL_BUTTON_LEFT) {
		LastPlayerAction = PlayerActionType::None;
		sgbMouseDown = CLICK_NONE;
		LeftMouseUp(modState);
	} else if (sgbMouseDown == CLICK_RIGHT && button == SDL_BUTTON_RIGHT) {
		LastPlayerAction = PlayerActionType::None;
		sgbMouseDown = CLICK_NONE;
	} else {
		KeymapperRelease(static_cast<SDL_Keycode>(button | KeymapperMouseButtonMask));
	}
}

[[maybe_unused]] void LogUnhandledEvent(const char *name, int value)
{
	LogVerbose("Unhandled SDL event: {} {}", name, value);
}

void PrepareForFadeIn()
{
	if (HeadlessMode) return;
	BlackPalette();

	// Render the game to the buffer(s) with a fully black palette.
	// Palette fade-in will gradually make it visible.
	RedrawEverything();
	while (IsRedrawEverything()) {
		DrawAndBlit();
	}
}

void GameEventHandler(const SDL_Event &event, uint16_t modState)
{
	[[maybe_unused]] const Options &options = GetOptions();
	StaticVector<ControllerButtonEvent, 4> ctrlEvents = ToControllerButtonEvents(event);
	for (const ControllerButtonEvent ctrlEvent : ctrlEvents) {
		GameAction action;
		if (HandleControllerButtonEvent(event, ctrlEvent, action) && action.type == GameActionType_SEND_KEY) {
			if ((action.send_key.vk_code & KeymapperMouseButtonMask) != 0) {
				const unsigned button = action.send_key.vk_code & ~KeymapperMouseButtonMask;
				if (!action.send_key.up)
					HandleMouseButtonDown(static_cast<Uint8>(button), modState);
				else
					HandleMouseButtonUp(static_cast<Uint8>(button), modState);
			} else {
				if (!action.send_key.up)
					PressKey(static_cast<SDL_Keycode>(action.send_key.vk_code), modState);
				else
					ReleaseKey(static_cast<SDL_Keycode>(action.send_key.vk_code));
			}
		}
	}
	if (ctrlEvents.size() > 0 && ctrlEvents[0].button != ControllerButton_NONE) {
		return;
	}

#ifdef _DEBUG
	if (DebugOverlayHandleEvent(event)) {
		return;
	}
	if (ConsoleHandleEvent(event)) {
		return;
	}
#endif

	if (IsChatActive() && HandleTalkTextInputEvent(event)) {
		return;
	}
	if (DropGoldFlag && HandleGoldDropTextInputEvent(event)) {
		return;
	}
	if (IsWithdrawGoldOpen && HandleGoldWithdrawTextInputEvent(event)) {
		return;
	}

	switch (event.type) {
	case SDL_EVENT_KEY_DOWN:
		PressKey(SDLC_EventKey(event), modState);
		return;
	case SDL_EVENT_KEY_UP:
		ReleaseKey(SDLC_EventKey(event));
		return;
	case SDL_EVENT_MOUSE_MOTION:
		if (ControlMode == ControlTypes::KeyboardAndMouse && invflag)
			InvalidateInventorySlot();
		MousePosition = { SDLC_EventMotionIntX(event), SDLC_EventMotionIntY(event) };
		gmenu_on_mouse_move();
		return;
	case SDL_EVENT_MOUSE_BUTTON_DOWN:
		MousePosition = { SDLC_EventButtonIntX(event), SDLC_EventButtonIntY(event) };
		HandleMouseButtonDown(event.button.button, modState);
		return;
	case SDL_EVENT_MOUSE_BUTTON_UP:
		MousePosition = { SDLC_EventButtonIntX(event), SDLC_EventButtonIntY(event) };
		HandleMouseButtonUp(event.button.button, modState);
		return;
#if SDL_VERSION_ATLEAST(2, 0, 0)
	case SDL_EVENT_MOUSE_WHEEL:
		if (SDLC_EventWheelIntY(event) > 0) { // Up
			if (CurrentStoreManager.IsPlayerInStore()) {
				CurrentStoreManager.StoreUp();
			} else if (QuestLogIsOpen) {
				QuestlogUp();
			} else if (HelpFlag) {
				HelpScrollUp();
			} else if (ChatLogFlag) {
				ChatLogScrollUp();
			} else if (IsStashOpen) {
				Stash.PreviousPage();
			} else if (SDL_GetModState() & SDL_KMOD_CTRL) {
				if (CurrentAutomapManager.GetAutomapActive()) {
					CurrentAutomapManager.AutomapZoomIn();
				}
			} else {
				KeymapperPress(MouseScrollUpButton);
			}
		} else if (SDLC_EventWheelIntY(event) < 0) { // down
			if (CurrentStoreManager.IsPlayerInStore()) {
				CurrentStoreManager.StoreDown();
			} else if (QuestLogIsOpen) {
				QuestlogDown();
			} else if (HelpFlag) {
				HelpScrollDown();
			} else if (ChatLogFlag) {
				ChatLogScrollDown();
			} else if (IsStashOpen) {
				Stash.NextPage();
			} else if (SDL_GetModState() & SDL_KMOD_CTRL) {
				if (CurrentAutomapManager.GetAutomapActive()) {
					CurrentAutomapManager.AutomapZoomOut();
				}
			} else {
				KeymapperPress(MouseScrollDownButton);
			}
		} else if (SDLC_EventWheelIntX(event) > 0) { // left
			KeymapperPress(MouseScrollLeftButton);
		} else if (SDLC_EventWheelIntX(event) < 0) { // right
			KeymapperPress(MouseScrollRightButton);
		}
		break;
#endif
	default:
		if (IsCustomEvent(event.type)) {
			if (gbIsMultiplayer)
				pfile_write_hero();
			nthread_ignore_mutex(true);
			PaletteFadeOut(8);
			sound_stop();
			ShowProgress(GetCustomEvent(event));

			PrepareForFadeIn();
			LoadPWaterPalette();
			if (gbRunGame)
				PaletteFadeIn(8);
			nthread_ignore_mutex(false);
			gbGameLoopStartup = true;
			return;
		}
		MainWndProc(event);
		break;
	}
}

void RunGameLoop(interface_mode uMsg)
{
	demo::NotifyGameLoopStart();

	nthread_ignore_mutex(true);
	StartGame(uMsg);
	assert(HeadlessMode || ghMainWnd);
	EventHandler previousHandler = SetEventHandler(GameEventHandler);
	run_delta_info();
	gbRunGame = true;
	gbProcessPlayers = IsDiabloAlive(true);
	gbRunGameResult = true;

	PrepareForFadeIn();
	LoadPWaterPalette();
	PaletteFadeIn(8);
	InitBackbufferState();
	RedrawEverything();
	gbGameLoopStartup = true;
	nthread_ignore_mutex(false);

	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (ShouldProcessLegacyLuaEvents(authoritative::GetServerBackedRuntime().IsAuthoritativeMode()))
	#else
	if (ShouldProcessLegacyLuaEvents(false))
	#endif
		CurrentGameEventBus.GameStart();
#ifdef GPERF_HEAP_FIRST_GAME_ITERATION
	unsigned run_game_iteration = 0;
#endif

	while (gbRunGame) {

#ifdef _DEBUG
		if (!gbGameLoopStartup && !DebugCmdsFromCommandLine.empty()) {
			InitConsole();
			for (const std::string &cmd : DebugCmdsFromCommandLine) {
				RunInConsole(cmd);
			}
			DebugCmdsFromCommandLine.clear();
		}
#endif

		SDL_Event event;
		uint16_t modState;
		while (FetchMessage(&event, &modState)) {
			if (event.type == SDL_EVENT_QUIT) {
				gbRunGameResult = false;
				gbRunGame = false;
				break;
			}
			HandleMessage(event, modState);
		}
		if (!gbRunGame)
			break;

#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			if (authoritative::GetServerBackedRuntime().IsConnected()) {
				if (auto result = authoritative::GetServerBackedRuntime().Poll(GetMillisecondsSinceStartup()); !result.has_value()) {
					LogError("Server-backed session polling failed: {}", result.error());
					authoritative::GetServerBackedRuntime().Stop();
					if (authoritative::GetServerBackedRuntimeConfiguration().mode == authoritative::ServerBackedRuntimeConfiguration::GameMode::Authoritative) {
						printInConsole("The authoritative server connection was lost; local simulation will not take over.");
						printNewlineInConsole();
						gbRunGameResult = false;
						gbRunGame = false;
						break;
					}
				}
				if (authoritative::GetServerBackedRuntime().NeedsNativeLevelLoad()) {
					// The server has already chosen and applied the destination.
					// NEXTLVL loads from the player's identity without decrementing
					// a legacy level or reading a local save. PrepareNativeLevel()
					// restores regular/set identity before loading its assets.
					SDL_Event levelEvent;
					CustomEventToSdlEvent(levelEvent, WM_DIABNEXTLVL);
					GameEventHandler(levelEvent, 0);
					if (!gbRunGame)
						break;
				}
			}
#endif

		bool drawGame = true;
		bool processInput = true;
		const bool runGameLoop = demo::IsRunning() ? demo::GetRunGameLoop(drawGame, processInput) : nthread_has_500ms_passed(&drawGame);
		if (demo::IsRecording())
			demo::RecordGameLoopResult(runGameLoop);

		if (!runGameLoop) {
			if (processInput)
				ProcessInput();
			DvlNet_ProcessNetworkPackets();
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			// Input can synchronously receive a destination snapshot. Do not
			// draw its player state against the previous level's geometry.
			if (authoritative::GetServerBackedRuntime().NeedsNativeLevelLoad())
				continue;
#endif
			if (!drawGame)
				continue;
			RedrawViewport();
			DrawAndBlit();
			continue;
		}

		ProcessGameMessagePackets();
		if (game_loop(gbGameLoopStartup))
			diablo_color_cyc_logic();
		gbGameLoopStartup = false;
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		if (authoritative::GetServerBackedRuntime().NeedsNativeLevelLoad())
			continue;
#endif
		if (drawGame)
			DrawAndBlit();
#ifdef GPERF_HEAP_FIRST_GAME_ITERATION
		if (run_game_iteration++ == 0)
			HeapProfilerDump("first_game_iteration");
#endif
	}

	demo::NotifyGameLoopEnd();

	if (gbIsMultiplayer) {
		pfile_write_hero(/*writeGameData=*/false);
		sfile_write_stash();
	}

	PaletteFadeOut(8);
	NewCursor(CURSOR_NONE);
	ClearScreenBuffer();
	RedrawEverything();
	scrollrt_draw_game_screen();
	previousHandler = SetEventHandler(previousHandler);
	assert(HeadlessMode || previousHandler == GameEventHandler);
	FreeGame();

	if (cineflag) {
		cineflag = false;
		DoEnding();
	}
}

void PrintWithRightPadding(std::string_view str, size_t width)
{
	printInConsole(str);
	if (str.size() >= width)
		return;
	printInConsole(std::string(width - str.size(), ' '));
}

void PrintHelpOption(std::string_view flags, std::string_view description)
{
	printInConsole("    ");
	PrintWithRightPadding(flags, 20);
	printInConsole(" ");
	PrintWithRightPadding(description, 30);
	printNewlineInConsole();
}

#if SDL_VERSION_ATLEAST(2, 0, 0)
FILE *SdlLogFile = nullptr;

extern "C" void SdlLogToFile(void *userdata, int /*category*/, SDL_LogPriority priority, const char *message)
{
	FILE *file = reinterpret_cast<FILE *>(userdata);
	static const char *const LogPriorityPrefixes[SDL_LOG_PRIORITY_COUNT] = {
		"",
		"VERBOSE",
		"DEBUG",
		"INFO",
		"WARN",
		"ERROR",
		"CRITICAL"
	};
	std::fprintf(file, "%s: %s\n", LogPriorityPrefixes[priority], message);
	std::fflush(file);
}
#endif

[[noreturn]] void PrintHelpAndExit()
{
	printInConsole((/* TRANSLATORS: Commandline Option */ "Options:"));
	printNewlineInConsole();
	PrintHelpOption("-h, --help", _(/* TRANSLATORS: Commandline Option */ "Print this message and exit"));
	PrintHelpOption("--version", _(/* TRANSLATORS: Commandline Option */ "Print the version and exit"));
	PrintHelpOption("--data-dir", _(/* TRANSLATORS: Commandline Option */ "Specify the folder of diabdat.mpq"));
	PrintHelpOption("--save-dir", _(/* TRANSLATORS: Commandline Option */ "Specify the folder of save files"));
	PrintHelpOption("--config-dir", _(/* TRANSLATORS: Commandline Option */ "Specify the location of diablo.ini"));
	PrintHelpOption("--lang", _(/* TRANSLATORS: Commandline Option */ "Specify the language code (e.g. en or pt_BR)"));
	PrintHelpOption("-n", _(/* TRANSLATORS: Commandline Option */ "Skip startup videos"));
	PrintHelpOption("-f", _(/* TRANSLATORS: Commandline Option */ "Display frames per second"));
	PrintHelpOption("--verbose", _(/* TRANSLATORS: Commandline Option */ "Enable verbose logging"));
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	PrintHelpOption("--game-mode <legacy|authoritative>", _(/* TRANSLATORS: Commandline Option */ "Select the local or authoritative game mode"));
	PrintHelpOption("--authoritative-server <host:port>", _(/* TRANSLATORS: Commandline Option */ "Use the experimental authoritative server"));
	PrintHelpOption("--authoritative-protocol-version <version>", _(/* TRANSLATORS: Commandline Option */ "Select the authoritative protocol schema version"));
	PrintHelpOption("--authoritative-content-hash <sha256>", _(/* TRANSLATORS: Commandline Option */ "Content hash advertised by the experimental authoritative server"));
	PrintHelpOption("--authoritative-ruleset-hash <sha256>", _(/* TRANSLATORS: Commandline Option */ "Gameplay ruleset hash advertised by the authoritative server"));
	PrintHelpOption("--authoritative-resume-token <path>", _(/* TRANSLATORS: Commandline Option */ "Read and persist the authoritative session token"));
	PrintHelpOption("--authoritative-diagnostics <directory>", _(/* TRANSLATORS: Commandline Option */ "Write authoritative startup diagnostics to this directory"));
#endif
#if SDL_VERSION_ATLEAST(2, 0, 0)
	PrintHelpOption("--log-to-file <path>", _(/* TRANSLATORS: Commandline Option */ "Log to a file instead of stderr"));
#endif
#ifndef DISABLE_DEMOMODE
	PrintHelpOption("--record <#>", _(/* TRANSLATORS: Commandline Option */ "Record a demo file"));
	PrintHelpOption("--demo <#>", _(/* TRANSLATORS: Commandline Option */ "Play a demo file"));
	PrintHelpOption("--timedemo", _(/* TRANSLATORS: Commandline Option */ "Disable all frame limiting during demo playback"));
#endif
	printNewlineInConsole();
	printInConsole(_(/* TRANSLATORS: Commandline Option */ "Game selection:"));
	printNewlineInConsole();
	PrintHelpOption("--spawn", _(/* TRANSLATORS: Commandline Option */ "Force Shareware mode"));
	PrintHelpOption("--diablo", _(/* TRANSLATORS: Commandline Option */ "Force Diablo mode"));
	PrintHelpOption("--hellfire", _(/* TRANSLATORS: Commandline Option */ "Force Hellfire mode"));
	printInConsole(_(/* TRANSLATORS: Commandline Option */ "Hellfire options:"));
	printNewlineInConsole();
#ifdef _DEBUG
	printNewlineInConsole();
	printInConsole("Debug options:");
	printNewlineInConsole();
	PrintHelpOption("-i", "Ignore network timeout");
	PrintHelpOption("+<internal command>", "Pass commands to the engine");
#endif
	printNewlineInConsole();
	printInConsole(_("Report bugs at https://github.com/diasurgical/devilutionX/"));
	printNewlineInConsole();
	diablo_quit(0);
}

void PrintFlagMessage(std::string_view flag, std::string_view message)
{
	printInConsole(flag);
	printInConsole(message);
	printNewlineInConsole();
}

void PrintFlagRequiresArgument(std::string_view flag)
{
	PrintFlagMessage(flag, " requires an argument");
}

void DiabloParseFlags(int argc, char **argv)
{
#ifdef _DEBUG
	int argumentIndexOfLastCommandPart = -1;
	std::string currentCommand;
#endif
#ifndef DISABLE_DEMOMODE
	bool timedemo = false;
	int demoNumber = -1;
	int recordNumber = -1;
	bool createDemoReference = false;
#endif
	for (int i = 1; i < argc; i++) {
		const std::string_view arg = argv[i];
		if (arg == "-h" || arg == "--help") {
			PrintHelpAndExit();
		} else if (arg == "--version") {
			printInConsole(PROJECT_NAME);
			printInConsole(" v");
			printInConsole(PROJECT_VERSION);
			printNewlineInConsole();
			diablo_quit(0);
		} else if (arg == "--data-dir") {
			if (i + 1 == argc) {
				PrintFlagRequiresArgument("--data-dir");
				diablo_quit(64);
			}
			paths::SetBasePath(argv[++i]);
		} else if (arg == "--save-dir") {
			if (i + 1 == argc) {
				PrintFlagRequiresArgument("--save-dir");
				diablo_quit(64);
			}
			paths::SetPrefPath(argv[++i]);
		} else if (arg == "--config-dir") {
			if (i + 1 == argc) {
				PrintFlagRequiresArgument("--config-dir");
				diablo_quit(64);
			}
			paths::SetConfigPath(argv[++i]);
		} else if (arg == "--lang") {
			if (i + 1 == argc) {
				PrintFlagRequiresArgument("--lang");
				diablo_quit(64);
			}
			forceLocale = argv[++i];
#ifndef DISABLE_DEMOMODE
		} else if (arg == "--demo") {
			if (i + 1 == argc) {
				PrintFlagRequiresArgument("--demo");
				diablo_quit(64);
			}
			ParseIntResult<int> parsedParam = ParseInt<int>(argv[++i]);
			if (!parsedParam.has_value()) {
				PrintFlagMessage("--demo", " must be a number");
				diablo_quit(64);
			}
			demoNumber = parsedParam.value();
			gbShowIntro = false;
		} else if (arg == "--timedemo") {
			timedemo = true;
		} else if (arg == "--record") {
			if (i + 1 == argc) {
				PrintFlagRequiresArgument("--record");
				diablo_quit(64);
			}
			ParseIntResult<int> parsedParam = ParseInt<int>(argv[++i]);
			if (!parsedParam.has_value()) {
				PrintFlagMessage("--record", " must be a number");
				diablo_quit(64);
			}
			recordNumber = parsedParam.value();
		} else if (arg == "--create-reference") {
			createDemoReference = true;
#else
		} else if (arg == "--demo" || arg == "--timedemo" || arg == "--record" || arg == "--create-reference") {
			printInConsole("Binary compiled without demo mode support.");
			printNewlineInConsole();
			diablo_quit(1);
#endif
		} else if (arg == "-n") {
			gbShowIntro = false;
		} else if (arg == "-f") {
			EnableFrameCount();
		} else if (arg == "--spawn") {
			forceSpawn = true;
		} else if (arg == "--diablo") {
			forceDiablo = true;
		} else if (arg == "--hellfire") {
			forceHellfire = true;
		} else if (arg == "--vanilla") {
			gbVanilla = true;
		} else if (arg == "--verbose") {
			SDL_SetLogPriorities(SDL_LOG_PRIORITY_VERBOSE);
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		} else if (arg == "--game-mode") {
			if (i + 1 == argc) {
				PrintFlagRequiresArgument("--game-mode");
				diablo_quit(64);
			}
			const std::string_view mode = argv[++i];
			auto &runtimeConfiguration = authoritative::GetServerBackedRuntimeConfiguration();
			if (mode == "legacy") {
				runtimeConfiguration.mode = authoritative::ServerBackedRuntimeConfiguration::GameMode::Legacy;
				runtimeConfiguration.enabled = false;
			} else if (mode == "authoritative") {
				runtimeConfiguration.mode = authoritative::ServerBackedRuntimeConfiguration::GameMode::Authoritative;
				runtimeConfiguration.enabled = true;
			} else {
				PrintFlagMessage("--game-mode", ": expected legacy or authoritative");
				diablo_quit(64);
			}
		} else if (arg == "--authoritative") {
			auto &runtimeConfiguration = authoritative::GetServerBackedRuntimeConfiguration();
			runtimeConfiguration.mode = authoritative::ServerBackedRuntimeConfiguration::GameMode::Authoritative;
			runtimeConfiguration.enabled = true;
		} else if (arg == "--authoritative-server") {
			if (i + 1 == argc) {
				PrintFlagRequiresArgument("--authoritative-server");
				diablo_quit(64);
			}
			auto configuration = authoritative::ParseServerEndpoint(argv[++i]);
			if (!configuration.has_value()) {
				PrintFlagMessage("--authoritative-server", ": " + configuration.error());
				diablo_quit(64);
			}
			auto &runtimeConfiguration = authoritative::GetServerBackedRuntimeConfiguration();
			const std::string protocolSchemaVersion = std::move(runtimeConfiguration.protocolSchemaVersion);
			const std::string contentManifestHash = std::move(runtimeConfiguration.contentManifestHash);
			const std::string rulesetIdentityHash = std::move(runtimeConfiguration.rulesetIdentityHash);
			const std::string resumeTokenPath = std::move(runtimeConfiguration.resumeTokenPath);
			const std::string diagnosticsDirectory = std::move(runtimeConfiguration.diagnosticsDirectory);
			runtimeConfiguration = std::move(*configuration);
			runtimeConfiguration.protocolSchemaVersion = protocolSchemaVersion;
			runtimeConfiguration.contentManifestHash = contentManifestHash;
			runtimeConfiguration.rulesetIdentityHash = rulesetIdentityHash;
			runtimeConfiguration.resumeTokenPath = resumeTokenPath;
			runtimeConfiguration.diagnosticsDirectory = diagnosticsDirectory.empty() ? "authoritative-diagnostics" : diagnosticsDirectory;
		} else if (arg == "--authoritative-protocol-version") {
			if (i + 1 == argc) {
				PrintFlagRequiresArgument("--authoritative-protocol-version");
				diablo_quit(64);
			}
			authoritative::GetServerBackedRuntimeConfiguration().protocolSchemaVersion = argv[++i];
		} else if (arg == "--authoritative-content-hash") {
			if (i + 1 == argc) {
				PrintFlagRequiresArgument("--authoritative-content-hash");
				diablo_quit(64);
			}
			authoritative::GetServerBackedRuntimeConfiguration().contentManifestHash = argv[++i];
		} else if (arg == "--authoritative-ruleset-hash") {
			if (i + 1 == argc) {
				PrintFlagRequiresArgument("--authoritative-ruleset-hash");
				diablo_quit(64);
			}
			authoritative::GetServerBackedRuntimeConfiguration().rulesetIdentityHash = argv[++i];
		} else if (arg == "--authoritative-resume-token") {
			if (i + 1 == argc) {
				PrintFlagRequiresArgument("--authoritative-resume-token");
				diablo_quit(64);
			}
			authoritative::GetServerBackedRuntimeConfiguration().resumeTokenPath = argv[++i];
		} else if (arg == "--authoritative-diagnostics") {
			if (i + 1 == argc) {
				PrintFlagRequiresArgument("--authoritative-diagnostics");
				diablo_quit(64);
			}
			authoritative::GetServerBackedRuntimeConfiguration().diagnosticsDirectory = argv[++i];
#endif
#if SDL_VERSION_ATLEAST(2, 0, 0)
		} else if (arg == "--log-to-file") {
			if (i + 1 == argc) {
				PrintFlagRequiresArgument("--log-to-file");
				diablo_quit(64);
			}
			SdlLogFile = OpenFile(argv[++i], "wb");
			if (SdlLogFile == nullptr) {
				printInConsole("Failed to open log file for writing");
				diablo_quit(64);
			}
			SDL_SetLogOutputFunction(&SdlLogToFile, /*userdata=*/SdlLogFile);
#endif
#ifdef _DEBUG
		} else if (arg == "-i") {
			DebugDisableNetworkTimeout = true;
		} else if (arg[0] == '+') {
			if (!currentCommand.empty())
				DebugCmdsFromCommandLine.push_back(currentCommand);
			argumentIndexOfLastCommandPart = i;
			currentCommand = arg.substr(1);
		} else if (arg[0] != '-' && (argumentIndexOfLastCommandPart + 1) == i) {
			currentCommand.append(" ");
			currentCommand.append(arg);
			argumentIndexOfLastCommandPart = i;
#endif
		} else {
			printInConsole("unrecognized option '");
			printInConsole(argv[i]);
			printInConsole("'");
			printNewlineInConsole();
			PrintHelpAndExit();
		}
	}

#ifdef _DEBUG
	if (!currentCommand.empty())
		DebugCmdsFromCommandLine.push_back(currentCommand);
#endif

#ifndef DISABLE_DEMOMODE
	if (demoNumber != -1)
		demo::InitPlayBack(demoNumber, timedemo);
	if (recordNumber != -1)
		demo::InitRecording(recordNumber, createDemoReference);
#endif
}

void DiabloInitScreen()
{
	MousePosition = { gnScreenWidth / 2, gnScreenHeight / 2 };
	if (ControlMode == ControlTypes::KeyboardAndMouse)
		SetCursorPos(MousePosition);

	ClrDiabloMsg();
}

void SetApplicationVersions()
{
	*BufCopy(gszProductName, PROJECT_NAME, " v", PROJECT_VERSION) = '\0';
	*BufCopy(gszVersionNumber, "version ", PROJECT_VERSION) = '\0';
}

void CheckArchivesUpToDate()
{
	const bool devilutionxMpqOutOfDate = IsDevilutionXMpqOutOfDate();
	const bool fontsMpqOutOfDate = AreExtraFontsOutOfDate();

	if (devilutionxMpqOutOfDate && fontsMpqOutOfDate) {
		app_fatal(_("Please update devilutionx.mpq and fonts.mpq to the latest version"));
	} else if (devilutionxMpqOutOfDate) {
		app_fatal(_("Failed to load UI resources.\n"
		            "\n"
		            "Make sure devilutionx.mpq is in the game folder and that it is up to date."));
	} else if (fontsMpqOutOfDate) {
		app_fatal(_("Please update fonts.mpq to the latest version"));
	}
}

void ApplicationInit()
{
	if (*GetOptions().Graphics.showFPS)
		EnableFrameCount();

	init_create_window();
	was_window_init = true;

	InitializeScreenReader();
	LanguageInitialize();

	SetApplicationVersions();

	ReadOnlyTest();
}

void DiabloInit()
{
	if (forceSpawn || *GetOptions().GameMode.shareware)
		gbIsSpawn = true;

	bool wasHellfireDiscovered = false;
	if (!forceDiablo && !forceHellfire)
		wasHellfireDiscovered = (HaveHellfire() && *GetOptions().GameMode.gameMode == StartUpGameMode::Ask);
	bool enableHellfire = forceHellfire || wasHellfireDiscovered;
	if (!forceDiablo && *GetOptions().GameMode.gameMode == StartUpGameMode::Hellfire) { // Migrate legacy options
		GetOptions().GameMode.gameMode.SetValue(StartUpGameMode::Diablo);
		enableHellfire = true;
	}
	if (forceDiablo || enableHellfire) {
		GetOptions().Mods.SetHellfireEnabled(enableHellfire);
	}

	gbIsHellfireSaveGame = gbIsHellfire;

	for (size_t i = 0; i < QuickMessages.size(); i++) {
		auto &messages = GetOptions().Chat.szHotKeyMsgs[i];
		if (messages.empty()) {
			messages.emplace_back(_(QuickMessages[i].message));
		}
	}

#ifndef USE_SDL1
	InitializeVirtualGamepad();
#endif

	UiInitialize();
	was_ui_init = true;

	if (wasHellfireDiscovered) {
		UiSelStartUpGameOption();
		if (!gbIsHellfire) {
			// Reinitialize the UI Elements because we changed the game
			UnloadUiGFX();
			UiInitialize();
			if (IsHardwareCursor())
				SetHardwareCursor(CursorInfo::UnknownCursor());
		}
	}

	DiabloInitScreen();

	snd_init();

	ui_sound_init();

	// Item graphics are loaded early, they already get touched during hero selection.
	InitItemGFX();

	// Always available.
	LoadSmallSelectionSpinner();

	CheckArchivesUpToDate();
}

void DiabloSplash()
{
	if (!gbShowIntro)
		return;

	if (*GetOptions().StartUp.splash == StartUpSplash::LogoAndTitleDialog)
		play_movie("gendata\\logo.smk", true);

	auto &intro = gbIsHellfire ? GetOptions().StartUp.hellfireIntro : GetOptions().StartUp.diabloIntro;

	if (*intro != StartUpIntro::Off) {
		if (gbIsHellfire)
			play_movie("gendata\\Hellfire.smk", true);
		else
			play_movie("gendata\\diablo1.smk", true);
		if (*intro == StartUpIntro::Once) {
			intro.SetValue(StartUpIntro::Off);
			if (!demo::IsRunning()) SaveOptions();
		}
	}

	if (IsAnyOf(*GetOptions().StartUp.splash, StartUpSplash::TitleDialog, StartUpSplash::LogoAndTitleDialog))
		UiTitleDialog();
}

void DiabloDeinit()
{
	FreeItemGFX();

#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	// Lua was never initialized for authoritative mode, so do not tear down a
	// compatibility runtime that does not exist.
	if (ShouldInitializeLegacyLua(authoritative::GetServerBackedRuntimeConfiguration().mode == authoritative::ServerBackedRuntimeConfiguration::GameMode::Authoritative))
		LuaShutdown();
#else
	LuaShutdown();
#endif
	ShutDownScreenReader();

	if (gbSndInited)
		effects_cleanup_sfx();
	snd_deinit();
	if (was_ui_init)
		UiDestroy();
	if (was_archives_init)
		init_cleanup();
	if (was_window_init)
		dx_cleanup(); // Cleanup SDL surfaces stuff, so we have to do it before SDL_Quit().
	UnloadFonts();
	if (SDL_WasInit((~0U) & ~SDL_INIT_HAPTIC) != 0)
		SDL_Quit();
}

tl::expected<void, std::string> LoadLvlGFX()
{
	assert(dungeonCels() == nullptr);
	constexpr int SpecialCelWidth = 64;

	const auto loadAll = [](const char *cel, const char *til, const char *special) -> tl::expected<void, std::string> {
		ASSIGN_OR_RETURN(dungeonCels(), LoadFileInMemWithStatus(cel));
		ASSIGN_OR_RETURN(megaTiles(), LoadFileInMemWithStatus<MegaTile>(til));
		ASSIGN_OR_RETURN(specialCels(), LoadCelWithStatus(special, SpecialCelWidth));
		return {};
	};

	switch (levelType()) {
	case DTYPE_TOWN: {
		auto cel = LoadFileInMemWithStatus("nlevels\\towndata\\town.cel");
		if (!cel.has_value()) {
			ASSIGN_OR_RETURN(dungeonCels(), LoadFileInMemWithStatus("levels\\towndata\\town.cel"));
		} else {
			dungeonCels() = std::move(*cel);
		}
		auto til = LoadFileInMemWithStatus<MegaTile>("nlevels\\towndata\\town.til");
		if (!til.has_value()) {
			ASSIGN_OR_RETURN(megaTiles(), LoadFileInMemWithStatus<MegaTile>("levels\\towndata\\town.til"));
		} else {
			megaTiles() = std::move(*til);
		}
		ASSIGN_OR_RETURN(specialCels(), LoadCelWithStatus("levels\\towndata\\towns", SpecialCelWidth));
		return {};
	}
	case DTYPE_CATHEDRAL:
		return loadAll(
		    "levels\\l1data\\l1.cel",
		    "levels\\l1data\\l1.til",
		    "levels\\l1data\\l1s");
	case DTYPE_CATACOMBS:
		return loadAll(
		    "levels\\l2data\\l2.cel",
		    "levels\\l2data\\l2.til",
		    "levels\\l2data\\l2s");
	case DTYPE_CAVES:
		return loadAll(
		    "levels\\l3data\\l3.cel",
		    "levels\\l3data\\l3.til",
		    "levels\\l1data\\l1s");
	case DTYPE_HELL:
		return loadAll(
		    "levels\\l4data\\l4.cel",
		    "levels\\l4data\\l4.til",
		    "levels\\l2data\\l2s");
	case DTYPE_NEST:
		return loadAll(
		    "nlevels\\l6data\\l6.cel",
		    "nlevels\\l6data\\l6.til",
		    "levels\\l1data\\l1s");
	case DTYPE_CRYPT:
		return loadAll(
		    "nlevels\\l5data\\l5.cel",
		    "nlevels\\l5data\\l5.til",
		    "nlevels\\l5data\\l5s");
	default:
		return tl::make_unexpected("LoadLvlGFX");
	}
}

tl::expected<void, std::string> LoadAllGFX()
{
	IncProgress();
#if !defined(USE_SDL1) && !defined(__vita__)
	InitVirtualGamepadGFX();
#endif
	IncProgress();
	RETURN_IF_ERROR(InitObjectGFX());
	IncProgress();
	RETURN_IF_ERROR(InitMissileGFX());
	IncProgress();
	return {};
}

/**
 * @param entry Where is the player entering from
 */
void CreateLevel(lvl_entry entry)
{
	CreateDungeon(DungeonSeeds[currentLevelNumber()], entry);

	switch (levelType()) {
	case DTYPE_TOWN:
		InitTownTriggers();
		break;
	case DTYPE_CATHEDRAL:
		InitL1Triggers();
		break;
	case DTYPE_CATACOMBS:
		InitL2Triggers();
		break;
	case DTYPE_CAVES:
		InitL3Triggers();
		break;
	case DTYPE_HELL:
		InitL4Triggers();
		break;
	case DTYPE_NEST:
		InitHiveTriggers();
		break;
	case DTYPE_CRYPT:
		InitCryptTriggers();
		break;
	default:
		app_fatal("CreateLevel");
	}

	if (levelType() != DTYPE_TOWN) {
		Freeupstairs();
	}
	LoadRndLvlPal(levelType());
}

void UnstuckChargers()
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (!ShouldProcessLegacyLevelLoadMonsterCleanup(authoritative::GetServerBackedRuntime().IsConnected()))
		return;
#endif

	if (gbIsMultiplayer) {
		for (Player &player : Players) {
			if (!player.plractive)
				continue;
			if (player._pLvlChanging)
				continue;
			if (!player.isOnActiveLevel())
				continue;
			if (&player == MyPlayer)
				continue;
			return;
		}
	}
	for (const unsigned m : MonsterPoolAdapter::ActiveMonsterRange()) {
		Monster &monster = Monsters[m];
		if (monster.mode == MonsterMode::Charge)
			monster.mode = MonsterMode::Stand;
	}
}

void UpdateMonsterLights()
{
	for (const unsigned m : MonsterPoolAdapter::ActiveMonsterRange()) {
		Monster &monster = Monsters[m];

		if ((monster.flags & MFLAG_BERSERK) != 0) {
			const int lightRadius = levelType() == DTYPE_NEST ? 9 : 3;
			monster.lightId = CurrentLightManager.AddLight(monster.position.tile, lightRadius);
		}

		if (monster.lightId != NO_LIGHT) {
			if (monster.lightId == MyPlayer->lightId) { // Fix old saves where some monsters had 0 instead of NO_LIGHT
				monster.lightId = NO_LIGHT;
				continue;
			}

			const Light &light = CurrentLightManager.lights_[monster.lightId];
			if (monster.position.tile != light.position.tile) {
				CurrentLightManager.ChangeLightXY(monster.lightId, monster.position.tile);
			}
		}
	}
}

void GameLogic()
{
	if (!ProcessInput()) {
		return;
	}
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	const bool serverBackedGameplay = authoritative::GetServerBackedRuntime().IsConnected();
	if (authoritative::GetServerBackedRuntime().NeedsNativeLevelLoad())
		return;
	#else
	const bool serverBackedGameplay = false;
	#endif
	if (!serverBackedGameplay && gbProcessPlayers) {
		gGameLogicStep = GameLogicStep::ProcessPlayers;
		ProcessPlayers();
	}
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (serverBackedGameplay) {
		// The connected server owns all gameplay mutation. The legacy pools remain
		// presentation data until their authoritative projection is consumed by
		// the renderer; running them locally would create a second simulation.
		authoritative::GetServerBackedRuntime().AdvancePresentationAnimations();
		AdvanceAuthoritativePresentationMissileAnimations();
		CurrentLightManager.ProcessLightList();
		CurrentLightManager.ProcessVisionList();
	} else
	#endif
	if (levelType() != DTYPE_TOWN) {
		gGameLogicStep = GameLogicStep::ProcessMonsters;
#ifdef _DEBUG
		if (!DebugInvisible)
#endif
			ProcessMonsters();
		gGameLogicStep = GameLogicStep::ProcessObjects;
		ProcessObjects();
		gGameLogicStep = GameLogicStep::ProcessMissiles;
		ProcessMissiles();
		gGameLogicStep = GameLogicStep::ProcessItems;
		ProcessItems();
		CurrentLightManager.ProcessLightList();
		CurrentLightManager.ProcessVisionList();
	} else {
		gGameLogicStep = GameLogicStep::ProcessTowners;
		ProcessTowners();
		gGameLogicStep = GameLogicStep::ProcessItemsTown;
		ProcessItems();
		gGameLogicStep = GameLogicStep::ProcessMissilesTown;
		ProcessMissiles();
	}
	gGameLogicStep = GameLogicStep::None;

#ifdef _DEBUG
	if (DebugScrollViewEnabled && (SDL_GetModState() & SDL_KMOD_SHIFT) != 0) {
		ScrollView();
	}
#endif

	sound_update();
	if (!serverBackedGameplay) {
		CheckTriggers();
		CheckQuests();
	}
	RedrawViewport();
	if (!serverBackedGameplay)
		pfile_update(false);

	plrctrls_after_game_logic();
}

void TimeoutCursor(bool bTimeout)
{
	if (bTimeout) {
		if (sgnTimeoutCurs == CURSOR_NONE && sgbMouseDown == CLICK_NONE) {
			sgnTimeoutCurs = pcurs;
			multi_net_ping();
			InfoString = StringOrView {};
			AddInfoBoxString(_("-- Network timeout --"));
			AddInfoBoxString(_("-- Waiting for players --"));
			for (uint8_t i = 0; i < Players.size(); i++) {
				bool isConnected = (player_state[i] & PS_CONNECTED) != 0;
				bool isActive = (player_state[i] & PS_ACTIVE) != 0;
				if (!isConnected || isActive) continue;

				DvlNetLatencies latencies = DvlNet_GetLatencies(i);

				std::string ping = fmt::format(
				    fmt::runtime(_(/* TRANSLATORS: {:s} means: Character Name */ "Player {:s} is timing out!")),
				    Players[i].name());

				StrAppend(ping, "\n  ", fmt::format(fmt::runtime(_(/* TRANSLATORS: Network connectivity statistics */ "Echo latency: {:d} ms")), latencies.echoLatency));

				if (latencies.providerLatency) {
					if (latencies.isRelayed && *latencies.isRelayed) {
						StrAppend(ping, "\n  ", fmt::format(fmt::runtime(_(/* TRANSLATORS: Network connectivity statistics */ "Provider latency: {:d} ms (Relayed)")), *latencies.providerLatency));
					} else {
						StrAppend(ping, "\n  ", fmt::format(fmt::runtime(_(/* TRANSLATORS: Network connectivity statistics */ "Provider latency: {:d} ms")), *latencies.providerLatency));
					}
				}
				EventPlrMsg(ping);
			}
			NewCursor(CURSOR_HOURGLASS);
			RedrawEverything();
		}
		scrollrt_draw_game_screen();
	} else if (sgnTimeoutCurs != CURSOR_NONE) {
		// Timeout is gone, we should restore the previous cursor.
		// But the timeout cursor could already be changed by the now processed messages (for example item cursor from CMD_GETITEM).
		// Changing the item cursor back to the previous (hand) cursor could result in deleted items, because this resets Player.HoldItem (see NewCursor).
		if (pcurs == CURSOR_HOURGLASS)
			NewCursor(sgnTimeoutCurs);
		sgnTimeoutCurs = CURSOR_NONE;
		InfoString = StringOrView {};
		RedrawEverything();
	}
}

void HelpKeyPressed()
{
	if (HelpFlag) {
		HelpFlag = false;
	} else if (CurrentStoreManager.IsPlayerInStore()) {
		InfoString = StringOrView {};
		AddInfoBoxString(_("No help available")); /// BUGFIX: message isn't displayed
		AddInfoBoxString(_("while in stores"));
		LastPlayerAction = PlayerActionType::None;
	} else {
		CloseInventory();
		CloseCharPanel();
		SpellbookFlag = false;
		SpellSelectFlag = false;
		if (qtextflag && levelType() == DTYPE_TOWN) {
			qtextflag = false;
			stream_stop();
		}
		QuestLogIsOpen = false;
		CancelCurrentDiabloMsg();
		gamemenu_off();
		DisplayHelp();
		doom_close();
	}
}

void InventoryKeyPressed()
{
	if (CurrentStoreManager.IsPlayerInStore())
		return;
	invflag = !invflag;
	if (!IsLeftPanelOpen() && CanPanelsCoverView()) {
		if (!invflag) { // We closed the inventory
			if (MousePosition.x < 480 && MousePosition.y < GetMainPanel().position.y) {
				SetCursorPos(MousePosition + Displacement { 160, 0 });
			}
		} else if (!SpellbookFlag) { // We opened the inventory
			if (MousePosition.x > 160 && MousePosition.y < GetMainPanel().position.y) {
				SetCursorPos(MousePosition - Displacement { 160, 0 });
			}
		}
	}
	SpellbookFlag = false;
	CloseGoldWithdraw();
	CloseStash();
	CloseVisualStore();
}

void CharacterSheetKeyPressed()
{
	if (CurrentStoreManager.IsPlayerInStore())
		return;
	if (!IsRightPanelOpen() && CanPanelsCoverView()) {
		if (CharFlag) { // We are closing the character sheet
			if (MousePosition.x > 160 && MousePosition.y < GetMainPanel().position.y) {
				SetCursorPos(MousePosition - Displacement { 160, 0 });
			}
		} else if (!QuestLogIsOpen) { // We opened the character sheet
			if (MousePosition.x < 480 && MousePosition.y < GetMainPanel().position.y) {
				SetCursorPos(MousePosition + Displacement { 160, 0 });
			}
		}
	}
	ToggleCharPanel();
}

void PartyPanelSideToggleKeyPressed()
{
	PartySidePanelOpen = !PartySidePanelOpen;
}

void QuestLogKeyPressed()
{
	if (CurrentStoreManager.IsPlayerInStore())
		return;
	if (!QuestLogIsOpen) {
		StartQuestlog();
	} else {
		QuestLogIsOpen = false;
	}
	if (!IsRightPanelOpen() && CanPanelsCoverView()) {
		if (!QuestLogIsOpen) { // We closed the quest log
			if (MousePosition.x > 160 && MousePosition.y < GetMainPanel().position.y) {
				SetCursorPos(MousePosition - Displacement { 160, 0 });
			}
		} else if (!CharFlag) { // We opened the character quest log
			if (MousePosition.x < 480 && MousePosition.y < GetMainPanel().position.y) {
				SetCursorPos(MousePosition + Displacement { 160, 0 });
			}
		}
	}
	CloseCharPanel();
	CloseGoldWithdraw();
	CloseStash();
	CloseVisualStore();
}

void DisplaySpellsKeyPressed()
{
	if (CurrentStoreManager.IsPlayerInStore())
		return;
	CloseCharPanel();
	QuestLogIsOpen = false;
	CloseInventory();
	SpellbookFlag = false;
	if (!SpellSelectFlag) {
		DoSpeedBook();
	} else {
		SpellSelectFlag = false;
	}
	LastPlayerAction = PlayerActionType::None;
}

void SpellBookKeyPressed()
{
	if (CurrentStoreManager.IsPlayerInStore())
		return;
	SpellbookFlag = !SpellbookFlag;
	if (!IsLeftPanelOpen() && CanPanelsCoverView()) {
		if (!SpellbookFlag) { // We closed the inventory
			if (MousePosition.x < 480 && MousePosition.y < GetMainPanel().position.y) {
				SetCursorPos(MousePosition + Displacement { 160, 0 });
			}
		} else if (!invflag) { // We opened the inventory
			if (MousePosition.x > 160 && MousePosition.y < GetMainPanel().position.y) {
				SetCursorPos(MousePosition - Displacement { 160, 0 });
			}
		}
	}
	CloseInventory();
	CloseVisualStore();
}

void CycleSpellHotkeys(bool next)
{
	StaticVector<size_t, NumHotkeys> validHotKeyIndexes;
	std::optional<size_t> currentIndex;
	for (size_t slot = 0; slot < NumHotkeys; slot++) {
		if (!IsValidSpeedSpell(slot))
			continue;
		if (MyPlayer->_pRSpell == MyPlayer->_pSplHotKey[slot] && MyPlayer->_pRSplType == MyPlayer->_pSplTHotKey[slot]) {
			// found current
			currentIndex = validHotKeyIndexes.size();
		}
		validHotKeyIndexes.emplace_back(slot);
	}
	if (validHotKeyIndexes.size() == 0)
		return;

	size_t newIndex;
	if (!currentIndex) {
		newIndex = next ? 0 : (validHotKeyIndexes.size() - 1);
	} else if (next) {
		newIndex = (*currentIndex == validHotKeyIndexes.size() - 1) ? 0 : (*currentIndex + 1);
	} else {
		newIndex = *currentIndex == 0 ? (validHotKeyIndexes.size() - 1) : (*currentIndex - 1);
	}
	ToggleSpell(validHotKeyIndexes[newIndex]);
}

bool IsPlayerDead()
{
	return MyPlayer->_pmode == PM_DEATH || MyPlayerIsDead;
}

bool IsGameRunning()
{
	return PauseMode != 2;
}

bool CanPlayerTakeAction()
{
	return !IsPlayerDead() && IsGameRunning();
}

bool CanAutomapBeToggledOff()
{
	// check if every window is closed - if yes, automap can be toggled off
	return !QuestLogIsOpen && !IsWithdrawGoldOpen && !IsStashOpen && !IsVisualStoreOpen && !CharFlag
	    && !SpellbookFlag && !invflag && !isGameMenuOpen && !qtextflag && !SpellSelectFlag
	    && !ChatLogFlag && !HelpFlag;
}

void OptionLanguageCodeChanged()
{
	UnloadFonts();
	LanguageInitialize();
	LoadLanguageArchive();
	effects_cleanup_sfx(false);
	if (gbRunGame)
		sound_init();
	else
		ui_sound_init();
}

const auto OptionChangeHandlerLanguage = (GetOptions().Language.code.SetValueChangedCallback(OptionLanguageCodeChanged), true);

} // namespace

#ifdef BUILD_TESTING
void CreateLevelForTesting(lvl_entry entry)
{
	CreateLevel(entry);
}
#endif

void InitKeymapActions()
{
	Options &options = GetOptions();
	for (uint32_t i = 0; i < 8; ++i) {
		options.Keymapper.AddAction(
		    "BeltItem{}",
		    N_("Belt item {}"),
		    N_("Use Belt item."),
		    '1' + i,
		    [i] {
			    const Player &myPlayer = *MyPlayer;
			    if (!myPlayer.SpdList[i].isEmpty() && myPlayer.SpdList[i]._itype != ItemType::Gold) {
				    UseInvItem(INVITEM_BELT_FIRST + i);
			    }
		    },
		    nullptr,
		    CanPlayerTakeAction,
		    i + 1);
	}
	for (uint32_t i = 0; i < NumHotkeys; ++i) {
		options.Keymapper.AddAction(
		    "QuickSpell{}",
		    N_("Quick spell {}"),
		    N_("Hotkey for skill or spell."),
		    i < 4 ? static_cast<uint32_t>(SDLK_F5) + i : static_cast<uint32_t>(SDLK_UNKNOWN),
		    [i]() {
			    if (SpellSelectFlag) {
				    SetSpeedSpell(i);
				    return;
			    }
			    if (!*GetOptions().Gameplay.quickCast)
				    ToggleSpell(i);
			    else
				    QuickCast(i);
		    },
		    nullptr,
		    CanPlayerTakeAction,
		    i + 1);
	}
	options.Keymapper.AddAction(
	    "QuickSpellPrevious",
	    N_("Previous quick spell"),
	    N_("Selects the previous quick spell (cycles)."),
	    MouseScrollUpButton,
	    [] { CycleSpellHotkeys(false); },
	    nullptr,
	    CanPlayerTakeAction);
	options.Keymapper.AddAction(
	    "QuickSpellNext",
	    N_("Next quick spell"),
	    N_("Selects the next quick spell (cycles)."),
	    MouseScrollDownButton,
	    [] { CycleSpellHotkeys(true); },
	    nullptr,
	    CanPlayerTakeAction);
	options.Keymapper.AddAction(
	    "UseHealthPotion",
	    N_("Use health potion"),
	    N_("Use health potions from belt."),
	    SDLK_UNKNOWN,
	    [] { UseBeltItem(BeltItemType::Healing); },
	    nullptr,
	    CanPlayerTakeAction);
	options.Keymapper.AddAction(
	    "UseManaPotion",
	    N_("Use mana potion"),
	    N_("Use mana potions from belt."),
	    SDLK_UNKNOWN,
	    [] { UseBeltItem(BeltItemType::Mana); },
	    nullptr,
	    CanPlayerTakeAction);
	options.Keymapper.AddAction(
	    "DisplaySpells",
	    N_("Speedbook"),
	    N_("Open Speedbook."),
	    'S',
	    DisplaySpellsKeyPressed,
	    nullptr,
	    CanPlayerTakeAction);
	options.Keymapper.AddAction(
	    "QuickSave",
	    N_("Quick save"),
	    N_("Saves the game."),
	    SDLK_F2,
	    [] { gamemenu_save_game(false); },
	    nullptr,
	    [&]() { return !gbIsMultiplayer && CanPlayerTakeAction(); });
	options.Keymapper.AddAction(
	    "QuickLoad",
	    N_("Quick load"),
	    N_("Loads the game."),
	    SDLK_F3,
	    [] { gamemenu_load_game(false); },
	    nullptr,
	    [&]() { return !gbIsMultiplayer && gbValidSaveFile && !CurrentStoreManager.IsPlayerInStore() && IsGameRunning(); });
#ifndef NOEXIT
	options.Keymapper.AddAction(
	    "QuitGame",
	    N_("Quit game"),
	    N_("Closes the game."),
	    SDLK_UNKNOWN,
	    [] { gamemenu_quit_game(false); });
#endif
	options.Keymapper.AddAction(
	    "StopHero",
	    N_("Stop hero"),
	    N_("Stops walking and cancel pending actions."),
	    SDLK_UNKNOWN,
	    [] { MyPlayer->Stop(); },
	    nullptr,
	    CanPlayerTakeAction);
	options.Keymapper.AddAction(
	    "ItemHighlighting",
	    N_("Item highlighting"),
	    N_("Show/hide items on ground."),
	    SDLK_LALT,
	    [] { HighlightKeyPressed(true); },
	    [] { HighlightKeyPressed(false); });
	options.Keymapper.AddAction(
	    "ToggleItemHighlighting",
	    N_("Toggle item highlighting"),
	    N_("Permanent show/hide items on ground."),
	    SDLK_RCTRL,
	    nullptr,
	    [] { ToggleItemLabelHighlight(); });
	options.Keymapper.AddAction(
	    "ToggleAutomap",
	    N_("Toggle automap"),
	    N_("Toggles if automap is displayed."),
	    SDLK_TAB,
	    DoAutoMap,
	    nullptr,
	    IsGameRunning);
	options.Keymapper.AddAction(
	    "CycleAutomapType",
	    N_("Cycle map type"),
	    N_("Opaque -> Transparent -> Minimap -> None"),
	    SDLK_M,
	    CycleAutomapType,
	    nullptr,
	    IsGameRunning);

	options.Keymapper.AddAction(
	    "Inventory",
	    N_("Inventory"),
	    N_("Open Inventory screen."),
	    'I',
	    InventoryKeyPressed,
	    nullptr,
	    CanPlayerTakeAction);
	options.Keymapper.AddAction(
	    "Character",
	    N_("Character"),
	    N_("Open Character screen."),
	    'C',
	    CharacterSheetKeyPressed,
	    nullptr,
	    CanPlayerTakeAction);
	options.Keymapper.AddAction(
	    "Party",
	    N_("Party"),
	    N_("Open side Party panel."),
	    'Y',
	    PartyPanelSideToggleKeyPressed,
	    nullptr,
	    CanPlayerTakeAction);
	options.Keymapper.AddAction(
	    "QuestLog",
	    N_("Quest log"),
	    N_("Open Quest log."),
	    'Q',
	    QuestLogKeyPressed,
	    nullptr,
	    CanPlayerTakeAction);
	options.Keymapper.AddAction(
	    "SpellBook",
	    N_("Spellbook"),
	    N_("Open Spellbook."),
	    'B',
	    SpellBookKeyPressed,
	    nullptr,
	    CanPlayerTakeAction);
	for (uint32_t i = 0; i < QuickMessages.size(); ++i) {
		options.Keymapper.AddAction(
		    "QuickMessage{}",
		    N_("Quick Message {}"),
		    N_("Use Quick Message in chat."),
		    (i < 4) ? static_cast<uint32_t>(SDLK_F9) + i : static_cast<uint32_t>(SDLK_UNKNOWN),
		    [i]() { DiabloHotkeyMsg(i); },
		    nullptr,
		    nullptr,
		    i + 1);
	}
	options.Keymapper.AddAction(
	    "HideInfoScreens",
	    N_("Hide Info Screens"),
	    N_("Hide all info screens."),
	    SDLK_SPACE,
	    [] {
		    if (CanAutomapBeToggledOff())
			    CurrentAutomapManager.SetAutomapActive(false);

		    ClosePanels();
		    HelpFlag = false;
		    ChatLogFlag = false;
		    SpellSelectFlag = false;
		    if (qtextflag && levelType() == DTYPE_TOWN) {
			    qtextflag = false;
			    stream_stop();
		    }

		    CancelCurrentDiabloMsg();
		    gamemenu_off();
		    doom_close();
	    },
	    nullptr,
	    IsGameRunning);
	options.Keymapper.AddAction(
	    "Zoom",
	    N_("Zoom"),
	    N_("Zoom Game Screen."),
	    'Z',
	    [] {
		    GetOptions().Graphics.zoom.SetValue(!*GetOptions().Graphics.zoom);
		    CalcViewportGeometry();
	    },
	    nullptr,
	    CanPlayerTakeAction);
	options.Keymapper.AddAction(
	    "PauseGame",
	    N_("Pause Game"),
	    N_("Pauses the game."),
	    'P',
	    diablo_pause_game);
	options.Keymapper.AddAction(
	    "PauseGameAlternate",
	    N_("Pause Game (Alternate)"),
	    N_("Pauses the game."),
	    SDLK_PAUSE,
	    diablo_pause_game);
	options.Keymapper.AddAction(
	    "DecreaseBrightness",
	    N_("Decrease Brightness"),
	    N_("Reduce screen brightness."),
	    'F',
	    DecreaseBrightness,
	    nullptr,
	    CanPlayerTakeAction);
	options.Keymapper.AddAction(
	    "IncreaseBrightness",
	    N_("Increase Brightness"),
	    N_("Increase screen brightness."),
	    'G',
	    IncreaseBrightness,
	    nullptr,
	    CanPlayerTakeAction);
	options.Keymapper.AddAction(
	    "Help",
	    N_("Help"),
	    N_("Open Help Screen."),
	    SDLK_F1,
	    HelpKeyPressed,
	    nullptr,
	    CanPlayerTakeAction);
	options.Keymapper.AddAction(
	    "Screenshot",
	    N_("Screenshot"),
	    N_("Takes a screenshot."),
	    SDLK_PRINTSCREEN,
	    nullptr,
	    CaptureScreen);
	options.Keymapper.AddAction(
	    "GameInfo",
	    N_("Game info"),
	    N_("Displays game infos."),
	    'V',
	    [] {
		    EventPlrMsg(fmt::format(
		                    fmt::runtime(_(/* TRANSLATORS: {:s} means: Project Name, Game Version. */ "{:s} {:s}")),
		                    PROJECT_NAME,
		                    PROJECT_VERSION),
		        UiFlags::ColorWhite);
	    },
	    nullptr,
	    CanPlayerTakeAction);
	options.Keymapper.AddAction(
	    "ChatLog",
	    N_("Chat Log"),
	    N_("Displays chat log."),
	    'L',
	    [] {
		    ToggleChatLog();
	    });
	options.Keymapper.AddAction(
	    "SortInv",
	    N_("Sort Inventory"),
	    N_("Sorts the inventory."),
	    'R',
	    [] {
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		    if (authoritative::GetServerBackedRuntime().IsConnected()) {
			    if (auto result = authoritative::GetServerBackedRuntime().SortInventory(0, GetMillisecondsSinceStartup()); !result.has_value())
				    LogError("Failed to submit authoritative inventory sort: {}", result.error());
			    return;
		    }
	#endif
		    ReorganizeInventory(*MyPlayer);
	    });
#ifdef _DEBUG
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	// Authoritative mode delegates gameplay and content ownership to the server;
	// keep the legacy Lua REPL and compatibility callbacks out of that path.
	if (authoritative::GetServerBackedRuntimeConfiguration().mode != authoritative::ServerBackedRuntimeConfiguration::GameMode::Authoritative) {
#endif
	options.Keymapper.AddAction(
	    "OpenConsole",
	    N_("Console"),
	    N_("Opens Lua console."),
	    SDLK_GRAVE,
	    OpenConsole);
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	}
#endif
	options.Keymapper.AddAction(
	    "DebugToggle",
	    "Debug toggle",
	    "Programming is like magic.",
	    'X',
	    [] {
		    DebugToggle = !DebugToggle;
	    });
#endif
	options.Keymapper.CommitActions();
}

void InitPadmapActions()
{
	Options &options = GetOptions();
	for (int i = 0; i < 8; ++i) {
		options.Padmapper.AddAction(
		    "BeltItem{}",
		    N_("Belt item {}"),
		    N_("Use Belt item."),
		    ControllerButton_NONE,
		    [i] {
			    const Player &myPlayer = *MyPlayer;
			    if (!myPlayer.SpdList[i].isEmpty() && myPlayer.SpdList[i]._itype != ItemType::Gold) {
				    UseInvItem(INVITEM_BELT_FIRST + i);
			    }
		    },
		    nullptr,
		    CanPlayerTakeAction,
		    i + 1);
	}
	for (uint32_t i = 0; i < NumHotkeys; ++i) {
		options.Padmapper.AddAction(
		    "QuickSpell{}",
		    N_("Quick spell {}"),
		    N_("Hotkey for skill or spell."),
		    ControllerButton_NONE,
		    [i]() {
			    if (SpellSelectFlag) {
				    SetSpeedSpell(i);
				    return;
			    }
			    if (!*GetOptions().Gameplay.quickCast)
				    ToggleSpell(i);
			    else
				    QuickCast(i);
		    },
		    nullptr,
		    []() { return CanPlayerTakeAction() && !InGameMenu(); },
		    i + 1);
	}
	options.Padmapper.AddAction(
	    "PrimaryAction",
	    N_("Primary action"),
	    N_("Attack monsters, talk to towners, lift and place inventory items."),
	    ControllerButton_BUTTON_B,
	    [] {
		    ControllerActionHeld = GameActionType_PRIMARY_ACTION;
		    LastPlayerAction = PlayerActionType::None;
		    PerformPrimaryAction();
	    },
	    [] {
		    ControllerActionHeld = GameActionType_NONE;
		    LastPlayerAction = PlayerActionType::None;
	    },
	    CanPlayerTakeAction);
	options.Padmapper.AddAction(
	    "SecondaryAction",
	    N_("Secondary action"),
	    N_("Open chests, interact with doors, pick up items."),
	    ControllerButton_BUTTON_Y,
	    [] {
		    ControllerActionHeld = GameActionType_SECONDARY_ACTION;
		    LastPlayerAction = PlayerActionType::None;
		    PerformSecondaryAction();
	    },
	    [] {
		    ControllerActionHeld = GameActionType_NONE;
		    LastPlayerAction = PlayerActionType::None;
	    },
	    CanPlayerTakeAction);
	options.Padmapper.AddAction(
	    "SpellAction",
	    N_("Spell action"),
	    N_("Cast the active spell."),
	    ControllerButton_BUTTON_X,
	    [] {
		    ControllerActionHeld = GameActionType_CAST_SPELL;
		    LastPlayerAction = PlayerActionType::None;
		    PerformSpellAction();
	    },
	    [] {
		    ControllerActionHeld = GameActionType_NONE;
		    LastPlayerAction = PlayerActionType::None;
	    },
	    []() { return CanPlayerTakeAction() && !InGameMenu(); });
	options.Padmapper.AddAction(
	    "CancelAction",
	    N_("Cancel action"),
	    N_("Close menus."),
	    ControllerButton_BUTTON_A,
	    [] {
		    if (DoomFlag) {
			    doom_close();
			    return;
		    }

		    GameAction action;
		    if (SpellSelectFlag)
			    action = GameAction(GameActionType_TOGGLE_QUICK_SPELL_MENU);
		    else if (invflag)
			    action = GameAction(GameActionType_TOGGLE_INVENTORY);
		    else if (SpellbookFlag)
			    action = GameAction(GameActionType_TOGGLE_SPELL_BOOK);
		    else if (QuestLogIsOpen)
			    action = GameAction(GameActionType_TOGGLE_QUEST_LOG);
		    else if (CharFlag)
			    action = GameAction(GameActionType_TOGGLE_CHARACTER_INFO);
		    ProcessGameAction(action);
	    },
	    nullptr,
	    [] { return DoomFlag || SpellSelectFlag || invflag || SpellbookFlag || QuestLogIsOpen || CharFlag; });
	options.Padmapper.AddAction(
	    "MoveUp",
	    N_("Move up"),
	    N_("Moves the player character up."),
	    ControllerButton_BUTTON_DPAD_UP,
	    [] {});
	options.Padmapper.AddAction(
	    "MoveDown",
	    N_("Move down"),
	    N_("Moves the player character down."),
	    ControllerButton_BUTTON_DPAD_DOWN,
	    [] {});
	options.Padmapper.AddAction(
	    "MoveLeft",
	    N_("Move left"),
	    N_("Moves the player character left."),
	    ControllerButton_BUTTON_DPAD_LEFT,
	    [] {});
	options.Padmapper.AddAction(
	    "MoveRight",
	    N_("Move right"),
	    N_("Moves the player character right."),
	    ControllerButton_BUTTON_DPAD_RIGHT,
	    [] {});
	options.Padmapper.AddAction(
	    "StandGround",
	    N_("Stand ground"),
	    N_("Hold to prevent the player from moving."),
	    ControllerButton_NONE,
	    [] {});
	options.Padmapper.AddAction(
	    "ToggleStandGround",
	    N_("Toggle stand ground"),
	    N_("Toggle whether the player moves."),
	    ControllerButton_NONE,
	    [] { StandToggle = !StandToggle; },
	    nullptr,
	    CanPlayerTakeAction);
	options.Padmapper.AddAction(
	    "UseHealthPotion",
	    N_("Use health potion"),
	    N_("Use health potions from belt."),
	    ControllerButton_BUTTON_LEFTSHOULDER,
	    [] { UseBeltItem(BeltItemType::Healing); },
	    nullptr,
	    CanPlayerTakeAction);
	options.Padmapper.AddAction(
	    "UseManaPotion",
	    N_("Use mana potion"),
	    N_("Use mana potions from belt."),
	    ControllerButton_BUTTON_RIGHTSHOULDER,
	    [] { UseBeltItem(BeltItemType::Mana); },
	    nullptr,
	    CanPlayerTakeAction);
	options.Padmapper.AddAction(
	    "Character",
	    N_("Character"),
	    N_("Open Character screen."),
	    ControllerButton_AXIS_TRIGGERLEFT,
	    [] {
		    ProcessGameAction(GameAction { GameActionType_TOGGLE_CHARACTER_INFO });
	    },
	    nullptr,
	    []() { return CanPlayerTakeAction() && !InGameMenu(); });
	options.Padmapper.AddAction(
	    "Inventory",
	    N_("Inventory"),
	    N_("Open Inventory screen."),
	    ControllerButton_AXIS_TRIGGERRIGHT,
	    [] {
		    ProcessGameAction(GameAction { GameActionType_TOGGLE_INVENTORY });
	    },
	    nullptr,
	    []() { return CanPlayerTakeAction() && !InGameMenu(); });
	options.Padmapper.AddAction(
	    "QuestLog",
	    N_("Quest log"),
	    N_("Open Quest log."),
	    { ControllerButton_BUTTON_BACK, ControllerButton_AXIS_TRIGGERLEFT },
	    [] {
		    ProcessGameAction(GameAction { GameActionType_TOGGLE_QUEST_LOG });
	    },
	    nullptr,
	    []() { return CanPlayerTakeAction() && !InGameMenu(); });
	options.Padmapper.AddAction(
	    "SpellBook",
	    N_("Spellbook"),
	    N_("Open Spellbook."),
	    { ControllerButton_BUTTON_BACK, ControllerButton_AXIS_TRIGGERRIGHT },
	    [] {
		    ProcessGameAction(GameAction { GameActionType_TOGGLE_SPELL_BOOK });
	    },
	    nullptr,
	    []() { return CanPlayerTakeAction() && !InGameMenu(); });
	options.Padmapper.AddAction(
	    "DisplaySpells",
	    N_("Speedbook"),
	    N_("Open Speedbook."),
	    ControllerButton_BUTTON_A,
	    [] {
		    ProcessGameAction(GameAction { GameActionType_TOGGLE_QUICK_SPELL_MENU });
	    },
	    nullptr,
	    []() { return CanPlayerTakeAction() && !InGameMenu(); });
	options.Padmapper.AddAction(
	    "ToggleAutomap",
	    N_("Toggle automap"),
	    N_("Toggles if automap is displayed."),
	    ControllerButton_BUTTON_LEFTSTICK,
	    DoAutoMap);
	options.Padmapper.AddAction(
	    "AutomapMoveUp",
	    N_("Automap Move Up"),
	    N_("Moves the automap up when active."),
	    ControllerButton_NONE,
	    [] {});
	options.Padmapper.AddAction(
	    "AutomapMoveDown",
	    N_("Automap Move Down"),
	    N_("Moves the automap down when active."),
	    ControllerButton_NONE,
	    [] {});
	options.Padmapper.AddAction(
	    "AutomapMoveLeft",
	    N_("Automap Move Left"),
	    N_("Moves the automap left when active."),
	    ControllerButton_NONE,
	    [] {});
	options.Padmapper.AddAction(
	    "AutomapMoveRight",
	    N_("Automap Move Right"),
	    N_("Moves the automap right when active."),
	    ControllerButton_NONE,
	    [] {});
	options.Padmapper.AddAction(
	    "MouseUp",
	    N_("Move mouse up"),
	    N_("Simulates upward mouse movement."),
	    { ControllerButton_BUTTON_BACK, ControllerButton_BUTTON_DPAD_UP },
	    [] {});
	options.Padmapper.AddAction(
	    "MouseDown",
	    N_("Move mouse down"),
	    N_("Simulates downward mouse movement."),
	    { ControllerButton_BUTTON_BACK, ControllerButton_BUTTON_DPAD_DOWN },
	    [] {});
	options.Padmapper.AddAction(
	    "MouseLeft",
	    N_("Move mouse left"),
	    N_("Simulates leftward mouse movement."),
	    { ControllerButton_BUTTON_BACK, ControllerButton_BUTTON_DPAD_LEFT },
	    [] {});
	options.Padmapper.AddAction(
	    "MouseRight",
	    N_("Move mouse right"),
	    N_("Simulates rightward mouse movement."),
	    { ControllerButton_BUTTON_BACK, ControllerButton_BUTTON_DPAD_RIGHT },
	    [] {});
	auto leftMouseDown = [] {
		const ControllerButtonCombo standGroundCombo = GetOptions().Padmapper.ButtonComboForAction("StandGround");
		const bool standGround = StandToggle || IsControllerButtonComboPressed(standGroundCombo);
		sgbMouseDown = CLICK_LEFT;
		LeftMouseDown(standGround ? SDL_KMOD_SHIFT : SDL_KMOD_NONE);
	};
	auto leftMouseUp = [] {
		const ControllerButtonCombo standGroundCombo = GetOptions().Padmapper.ButtonComboForAction("StandGround");
		const bool standGround = StandToggle || IsControllerButtonComboPressed(standGroundCombo);
		LastPlayerAction = PlayerActionType::None;
		sgbMouseDown = CLICK_NONE;
		LeftMouseUp(standGround ? SDL_KMOD_SHIFT : SDL_KMOD_NONE);
	};
	options.Padmapper.AddAction(
	    "LeftMouseClick1",
	    N_("Left mouse click"),
	    N_("Simulates the left mouse button."),
	    ControllerButton_BUTTON_RIGHTSTICK,
	    leftMouseDown,
	    leftMouseUp);
	options.Padmapper.AddAction(
	    "LeftMouseClick2",
	    N_("Left mouse click"),
	    N_("Simulates the left mouse button."),
	    { ControllerButton_BUTTON_BACK, ControllerButton_BUTTON_LEFTSHOULDER },
	    leftMouseDown,
	    leftMouseUp);
	auto rightMouseDown = [] {
		const ControllerButtonCombo standGroundCombo = GetOptions().Padmapper.ButtonComboForAction("StandGround");
		const bool standGround = StandToggle || IsControllerButtonComboPressed(standGroundCombo);
		LastPlayerAction = PlayerActionType::None;
		sgbMouseDown = CLICK_RIGHT;
		RightMouseDown(standGround);
	};
	auto rightMouseUp = [] {
		LastPlayerAction = PlayerActionType::None;
		sgbMouseDown = CLICK_NONE;
	};
	options.Padmapper.AddAction(
	    "RightMouseClick1",
	    N_("Right mouse click"),
	    N_("Simulates the right mouse button."),
	    { ControllerButton_BUTTON_BACK, ControllerButton_BUTTON_RIGHTSTICK },
	    rightMouseDown,
	    rightMouseUp);
	options.Padmapper.AddAction(
	    "RightMouseClick2",
	    N_("Right mouse click"),
	    N_("Simulates the right mouse button."),
	    { ControllerButton_BUTTON_BACK, ControllerButton_BUTTON_RIGHTSHOULDER },
	    rightMouseDown,
	    rightMouseUp);
	options.Padmapper.AddAction(
	    "PadHotspellMenu",
	    N_("Gamepad hotspell menu"),
	    N_("Hold to set or use spell hotkeys."),
	    ControllerButton_BUTTON_BACK,
	    [] { PadHotspellMenuActive = true; },
	    [] { PadHotspellMenuActive = false; });
	options.Padmapper.AddAction(
	    "PadMenuNavigator",
	    N_("Gamepad menu navigator"),
	    N_("Hold to access gamepad menu navigation."),
	    ControllerButton_BUTTON_START,
	    [] { PadMenuNavigatorActive = true; },
	    [] { PadMenuNavigatorActive = false; });
	auto toggleGameMenu = [] {
		const bool inMenu = gmenu_is_active();
		PressEscKey();
		LastPlayerAction = PlayerActionType::None;
		PadHotspellMenuActive = false;
		PadMenuNavigatorActive = false;
		if (!inMenu)
			gamemenu_on();
	};
	options.Padmapper.AddAction(
	    "ToggleGameMenu1",
	    N_("Toggle game menu"),
	    N_("Opens the game menu."),
	    {
	        ControllerButton_BUTTON_BACK,
	        ControllerButton_BUTTON_START,
	    },
	    toggleGameMenu);
	options.Padmapper.AddAction(
	    "ToggleGameMenu2",
	    N_("Toggle game menu"),
	    N_("Opens the game menu."),
	    {
	        ControllerButton_BUTTON_START,
	        ControllerButton_BUTTON_BACK,
	    },
	    toggleGameMenu);
	options.Padmapper.AddAction(
	    "QuickSave",
	    N_("Quick save"),
	    N_("Saves the game."),
	    ControllerButton_NONE,
	    [] { gamemenu_save_game(false); },
	    nullptr,
	    [&]() { return !gbIsMultiplayer && CanPlayerTakeAction(); });
	options.Padmapper.AddAction(
	    "QuickLoad",
	    N_("Quick load"),
	    N_("Loads the game."),
	    ControllerButton_NONE,
	    [] { gamemenu_load_game(false); },
	    nullptr,
	    [&]() { return !gbIsMultiplayer && gbValidSaveFile && !CurrentStoreManager.IsPlayerInStore() && IsGameRunning(); });
	options.Padmapper.AddAction(
	    "ItemHighlighting",
	    N_("Item highlighting"),
	    N_("Show/hide items on ground."),
	    ControllerButton_NONE,
	    [] { HighlightKeyPressed(true); },
	    [] { HighlightKeyPressed(false); });
	options.Padmapper.AddAction(
	    "ToggleItemHighlighting",
	    N_("Toggle item highlighting"),
	    N_("Permanent show/hide items on ground."),
	    ControllerButton_NONE,
	    nullptr,
	    [] { ToggleItemLabelHighlight(); });
	options.Padmapper.AddAction(
	    "HideInfoScreens",
	    N_("Hide Info Screens"),
	    N_("Hide all info screens."),
	    ControllerButton_NONE,
	    [] {
		    if (CanAutomapBeToggledOff())
			    CurrentAutomapManager.SetAutomapActive(false);

		    ClosePanels();
		    HelpFlag = false;
		    ChatLogFlag = false;
		    SpellSelectFlag = false;
		    if (qtextflag && levelType() == DTYPE_TOWN) {
			    qtextflag = false;
			    stream_stop();
		    }

		    CancelCurrentDiabloMsg();
		    gamemenu_off();
		    doom_close();
	    },
	    nullptr,
	    IsGameRunning);
	options.Padmapper.AddAction(
	    "Zoom",
	    N_("Zoom"),
	    N_("Zoom Game Screen."),
	    ControllerButton_NONE,
	    [] {
		    GetOptions().Graphics.zoom.SetValue(!*GetOptions().Graphics.zoom);
		    CalcViewportGeometry();
	    },
	    nullptr,
	    CanPlayerTakeAction);
	options.Padmapper.AddAction(
	    "PauseGame",
	    N_("Pause Game"),
	    N_("Pauses the game."),
	    ControllerButton_NONE,
	    diablo_pause_game);
	options.Padmapper.AddAction(
	    "DecreaseBrightness",
	    N_("Decrease Brightness"),
	    N_("Reduce screen brightness."),
	    ControllerButton_NONE,
	    DecreaseBrightness,
	    nullptr,
	    CanPlayerTakeAction);
	options.Padmapper.AddAction(
	    "IncreaseBrightness",
	    N_("Increase Brightness"),
	    N_("Increase screen brightness."),
	    ControllerButton_NONE,
	    IncreaseBrightness,
	    nullptr,
	    CanPlayerTakeAction);
	options.Padmapper.AddAction(
	    "Help",
	    N_("Help"),
	    N_("Open Help Screen."),
	    ControllerButton_NONE,
	    HelpKeyPressed,
	    nullptr,
	    CanPlayerTakeAction);
	options.Padmapper.AddAction(
	    "Screenshot",
	    N_("Screenshot"),
	    N_("Takes a screenshot."),
	    ControllerButton_NONE,
	    nullptr,
	    CaptureScreen);
	options.Padmapper.AddAction(
	    "GameInfo",
	    N_("Game info"),
	    N_("Displays game infos."),
	    ControllerButton_NONE,
	    [] {
		    EventPlrMsg(fmt::format(
		                    fmt::runtime(_(/* TRANSLATORS: {:s} means: Project Name, Game Version. */ "{:s} {:s}")),
		                    PROJECT_NAME,
		                    PROJECT_VERSION),
		        UiFlags::ColorWhite);
	    },
	    nullptr,
	    CanPlayerTakeAction);
	options.Padmapper.AddAction(
	    "SortInv",
	    N_("Sort Inventory"),
	    N_("Sorts the inventory."),
	    ControllerButton_NONE,
	    [] {
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		    if (authoritative::GetServerBackedRuntime().IsConnected()) {
			    if (auto result = authoritative::GetServerBackedRuntime().SortInventory(0, GetMillisecondsSinceStartup()); !result.has_value())
				    LogError("Failed to submit authoritative inventory sort: {}", result.error());
			    return;
		    }
	#endif
		    ReorganizeInventory(*MyPlayer);
	    });
	options.Padmapper.AddAction(
	    "ChatLog",
	    N_("Chat Log"),
	    N_("Displays chat log."),
	    ControllerButton_NONE,
	    [] {
		    ToggleChatLog();
	    });
	options.Padmapper.CommitActions();
}

void SetCursorPos(Point position)
{
	if (ControlDevice != ControlTypes::KeyboardAndMouse) {
		MousePosition = position;
		return;
	}

	LogicalToOutput(&position.x, &position.y);
	if (!demo::IsRunning())
		SDL_WarpMouseInWindow(ghMainWnd, position.x, position.y);
}

void FreeGameMem()
{
	dungeonCels() = nullptr;
	megaTiles() = nullptr;
	specialCels() = std::nullopt;

#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (authoritative::GetServerBackedRuntime().IsConnected())
		authoritative::GetServerBackedRuntime().InvalidateNativeLevelPresentation();
#endif
	FreeMonsters();
	FreeMissileGFX();
	FreeObjectGFX();
	FreeTownerGFX();
	FreeStashGFX();
	FreeVisualStoreGFX();
#ifndef USE_SDL1
	DeactivateVirtualGamepad();
	FreeVirtualGamepadGFX();
#endif
}

bool StartGame(bool bNewGame, bool bSinglePlayer)
{
	gbSelectProvider = true;
	ReturnToMainMenu = false;

	do {
		gbLoadGame = false;

		if (!NetInit(bSinglePlayer)) {
			gbRunGameResult = true;
			break;
		}

#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		if (auto result = authoritative::GetServerBackedRuntime().Start(authoritative::GetServerBackedRuntimeConfiguration(), CurrentStoreManager, *MyPlayer); !result.has_value()) {
			LogError("Failed to start the server-backed runtime: {}", result.error());
			printInConsole("Failed to start the server-backed runtime: " + result.error());
			printNewlineInConsole();
			NetClose();
			if (!HeadlessMode)
				UiErrorOkDialog(_("Unable to start authoritative game"), result.error(), /*error=*/true);
			// Menu resources are still loaded. Keep startup failures recoverable
			// without trying to render a modal dialog in a headless run.
			gbRunGameResult = !HeadlessMode;
			break;
		}
		if (authoritative::GetServerBackedRuntime().IsAuthoritativeMode()) {
			// The authoritative server owns persistence; do not load or save the
			// legacy local binary save alongside the remote session.
			gbLoadGame = false;
			gbValidSaveFile = false;
		}
#endif

		// Save 2.8 MiB of RAM by freeing all main menu resources
		// before starting the game.
		UiDestroy();

		gbSelectProvider = false;

		if (bNewGame || !gbValidSaveFile) {
			InitLevels();
			InitQuests();
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			if (authoritative::GetServerBackedRuntime().IsConnected()) {
				if (auto result = authoritative::GetServerBackedRuntime().PrepareNativeLevel(); !result.has_value()) {
					LogError("Failed to prepare the authoritative native level: {}", result.error());
					printInConsole("Failed to prepare the authoritative native level: " + result.error());
					printNewlineInConsole();
					authoritative::GetServerBackedRuntime().Stop();
					NetClose();
					if (!HeadlessMode) {
						UiInitialize();
						UiErrorOkDialog(_("Unable to start authoritative game"), result.error(), /*error=*/true);
					}
					gbRunGameResult = !HeadlessMode;
					break;
				}
			}
#endif
			InitPortals();
			MyPlayer->initDungeonMessages();
			DeltaSyncJunk();
		}
		giNumberOfLevels = gbIsHellfire ? 25 : 17;
		interface_mode uMsg = WM_DIABNEWGAME;
		if (gbValidSaveFile && gbLoadGame) {
			uMsg = WM_DIABLOADGAME;
		}
		RunGameLoop(uMsg);
		NetClose();
		UnloadFonts();

		// If the player left the game into the main menu,
		// initialize main menu resources.
		if (gbRunGameResult)
			UiInitialize();
		if (ReturnToMainMenu)
			return true;
	} while (gbRunGameResult);

	SNetDestroy();
	return gbRunGameResult;
}

void diablo_quit(int exitStatus)
{
	FreeGameMem();
	music_stop();
	DiabloDeinit();

#if SDL_VERSION_ATLEAST(2, 0, 0)
	if (SdlLogFile != nullptr) std::fclose(SdlLogFile);
#endif

	exit(exitStatus);
}

#ifdef __UWP__
void (*onInitialized)() = NULL;

void setOnInitialized(void (*callback)())
{
	onInitialized = callback;
}
#endif

int DiabloMain(int argc, char **argv)
{
#ifdef _DEBUG
	SDL_SetLogPriorities(SDL_LOG_PRIORITY_DEBUG);
#endif

	DiabloParseFlags(argc, argv);
	InitKeymapActions();
	InitPadmapActions();

	// Need to ensure devilutionx.mpq (and fonts.mpq if available) are loaded before attempting to read translation settings
	LoadCoreArchives();
	was_archives_init = true;

	// Read settings including translation next. This will use the presence of fonts.mpq and look for assets in devilutionx.mpq
	LoadOptions();
	if (demo::IsRunning()) demo::OverrideOptions();

	// Then look for a voice pack file based on the selected translation
	LoadLanguageArchive();

	ApplicationInit();
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	// Keep Lua available for explicit legacy launches, but leave the
	// server-authoritative presentation path Lua-free.
	if (ShouldInitializeLegacyLua(authoritative::GetServerBackedRuntimeConfiguration().mode == authoritative::ServerBackedRuntimeConfiguration::GameMode::Authoritative))
		LuaInitialize();
	else {
		CurrentModManager.RegisterNativeOptionChangeHandler();
		CurrentModManager.ReloadActiveModsAndData();
	}
#else
	LuaInitialize();
#endif
	if (!demo::IsRunning()) SaveOptions();

	// Finally load game data
	LoadGameArchives();

	LoadTextData();

	// Load dynamic data before we go into the menu as we need to initialise player characters in memory pretty early.
	LoadPlayerDataFiles();

	// TODO: We can probably load this much later (when the game is starting).
	LoadSpellData();
	LoadMissileData();
	LoadMonsterData();
	LoadItemData();
	LoadObjectData();
	RegisterObjectBehaviors();
	LoadQuestData();
	LoadSetLevelNames();
	LoadQuestPools();
	LoadLevelGenerationData();

	DiabloInit();
#ifdef __UWP__
	onInitialized();
#endif
	if (!demo::IsRunning()) SaveOptions();

	DiabloSplash();
	mainmenu_loop();
	DiabloDeinit();

	return 0;
}

#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
std::optional<authoritative::ServerBackedItemReference> ServerBackedReferenceForInventoryCursor(
    const Player &player,
    int inventoryCursor)
{
	if (inventoryCursor < 0)
		return std::nullopt;

	uint32_t itemSeed = 0;
	if (inventoryCursor < NUM_INVLOC) {
		itemSeed = player.InvBody[inventoryCursor]._iSeed;
	} else {
		const int inventoryIndex = inventoryCursor - NUM_INVLOC;
		if (inventoryIndex < 0 || inventoryIndex >= player._pNumInv)
			return std::nullopt;
		itemSeed = player.InvList[inventoryIndex]._iSeed;
	}
	if (itemSeed == 0)
		return std::nullopt;

	return authoritative::GetServerBackedRuntime().PlayerItemReferenceForSeed(itemSeed);
}

std::optional<authoritative::ServerBackedItemReference> ServerBackedReferenceForStashCursor(uint16_t stashItemIndex)
{
	if (stashItemIndex == StashStruct::EmptyCell || stashItemIndex >= Stash.stashList.size())
		return std::nullopt;
	const Item &item = Stash.stashList[stashItemIndex];
	if (item.isEmpty() || item._iSeed == 0)
		return std::nullopt;
	return authoritative::GetServerBackedRuntime().PlayerItemReferenceForSeed(item._iSeed);
}

std::optional<authoritative::ServerBackedItemReference> ServerBackedReferenceForSpellCursor(Player &player, SpellID &spellId)
{
	const int itemLocation = player.spellFrom;
	if (itemLocation >= INVITEM_INV_FIRST && itemLocation <= INVITEM_BELT_LAST) {
		const Item &item = GetInventoryItem(player, itemLocation);
		if (item.isEmpty() || item._iSpell == SpellID::Null)
			return std::nullopt;
		spellId = item._iSpell;
		return authoritative::GetServerBackedRuntime().PlayerItemReferenceForSeed(item._iSeed);
	}

	if (!IsStashOpen)
		return std::nullopt;
	for (const Item &item : Stash.stashList) {
		if (!item.isScrollOf(spellId) && !item.isRuneOf(spellId))
			continue;
		return authoritative::GetServerBackedRuntime().PlayerItemReferenceForSeed(item._iSeed);
	}
	return std::nullopt;
}

std::optional<authoritative::ServerBackedCastSource> ServerBackedSourceForSpellCursor(Player &player, SpellID &spellId)
{
	switch (player._pRSplType) {
	case SpellType::Spell:
		return authoritative::ServerBackedCastSource {
		    authoritative::ServerBackedCastSourceKind::MemorizedSpell,
		    std::nullopt,
		};
	case SpellType::Scroll: {
		const auto reference = ServerBackedReferenceForSpellCursor(player, spellId);
		if (!reference.has_value())
			return std::nullopt;
		return authoritative::ServerBackedCastSource {
		    authoritative::ServerBackedCastSourceKind::ConsumableItem,
		    *reference,
		};
	}
	default:
		return std::nullopt;
	}
}
#endif

bool TryIconCurs()
{
	if (pcurs == CURSOR_RESURRECT) {
		if (PlayerUnderCursor != nullptr) {
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			if (authoritative::GetServerBackedRuntime().IsConnected()) {
				SpellID spellId = SpellID::Resurrect;
				const auto targetEntityId = authoritative::GetServerBackedRuntime().PlayerEntityIdAt(cursPosition.x, cursPosition.y);
				const auto source = ServerBackedSourceForSpellCursor(*MyPlayer, spellId);
				if (!targetEntityId.has_value()) {
					LogError("Server-backed Resurrection target is not an authoritative player.");
				} else if (!source.has_value()) {
					LogError("Server-backed Resurrection cursor has no authoritative spell source.");
				} else if (auto result = authoritative::GetServerBackedRuntime().Cast(
				               static_cast<uint32_t>(spellId), *targetEntityId,
			               cursPosition.x, cursPosition.y, *source, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
					LogError("Server-backed Resurrection cast failed: {}", result.error());
				}
				NewCursor(CURSOR_HAND);
				return true;
			}
#endif
			NetSendCmdParam1(true, CMD_RESURRECT, PlayerUnderCursor->getId());
			NewCursor(CURSOR_HAND);
			return true;
		}

		return false;
	}

	if (pcurs == CURSOR_HEALOTHER) {
		if (PlayerUnderCursor != nullptr) {
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			if (authoritative::GetServerBackedRuntime().IsConnected()) {
				SpellID spellId = SpellID::HealOther;
				const auto targetEntityId = authoritative::GetServerBackedRuntime().PlayerEntityIdAt(cursPosition.x, cursPosition.y);
				const auto source = ServerBackedSourceForSpellCursor(*MyPlayer, spellId);
				if (!targetEntityId.has_value()) {
					LogError("Server-backed Heal Other target is not an authoritative player.");
				} else if (!source.has_value()) {
					LogError("Server-backed Heal Other cursor has no authoritative spell source.");
				} else if (auto result = authoritative::GetServerBackedRuntime().Cast(
				               static_cast<uint32_t>(spellId), *targetEntityId,
			               cursPosition.x, cursPosition.y, *source, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
					LogError("Server-backed Heal Other cast failed: {}", result.error());
				}
				NewCursor(CURSOR_HAND);
				return true;
			}
#endif
			NetSendCmdParam1(true, CMD_HEALOTHER, PlayerUnderCursor->getId());
			NewCursor(CURSOR_HAND);
			return true;
		}

		return false;
	}

	if (pcurs == CURSOR_TELEKINESIS) {
	#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		if (authoritative::GetServerBackedRuntime().IsConnected()) {
			uint32_t targetEntityId = 0;
			if (ObjectUnderCursor != nullptr && !ObjectUnderCursor->IsDisabled()) {
				if (const auto entityId = authoritative::GetServerBackedRuntime().AuthoritativeObjectEntityIdAt(cursPosition.x, cursPosition.y); entityId.has_value())
					targetEntityId = *entityId;
			} else if (pcursitem != -1) {
				if (const auto entityId = authoritative::GetServerBackedRuntime().WorldItemEntityIdAt(cursPosition.x, cursPosition.y); entityId.has_value())
					targetEntityId = *entityId;
			} else if (pcursmonst != -1) {
				const Monster &monster = Monsters[pcursmonst];
				if (!monster.isTalker() && monster.talkMsg == TEXT_NONE) {
					if (const auto entityId = authoritative::GetServerBackedRuntime().MonsterEntityIdAt(cursPosition.x, cursPosition.y); entityId.has_value())
						targetEntityId = *entityId;
				}
			}
			if (targetEntityId == 0) {
				LogError("Server-backed Telekinesis target is not an authoritative entity.");
			} else if (auto result = authoritative::GetServerBackedRuntime().Cast(
			               static_cast<uint32_t>(SpellID::Telekinesis), targetEntityId,
			               cursPosition.x, cursPosition.y, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
				LogError("Server-backed Telekinesis cast failed: {}", result.error());
			}
			NewCursor(CURSOR_HAND);
			return true;
		}
	#endif
		DoTelekinesis();
		return true;
	}

	Player &myPlayer = *MyPlayer;

	if (pcurs == CURSOR_IDENTIFY) {
		if (pcursinvitem != -1 && !IsInspectingPlayer()) {
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			if (authoritative::GetServerBackedRuntime().IsConnected()) {
				const auto reference = ServerBackedReferenceForInventoryCursor(myPlayer, pcursinvitem);
				if (!reference.has_value()) {
					LogError("Server-backed Identify ability has no authoritative item reference.");
				} else if (auto result = authoritative::GetServerBackedRuntime().IdentifyItemWithAbility(*reference, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
					LogError("Server-backed Identify ability failed: {}", result.error());
				}
				NewCursor(CURSOR_HAND);
				return true;
			}
#endif
			CheckIdentify(myPlayer, pcursinvitem);
		}
		else if (pcursstashitem != StashStruct::EmptyCell) {
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			if (authoritative::GetServerBackedRuntime().IsConnected()) {
				const auto reference = ServerBackedReferenceForStashCursor(pcursstashitem);
				if (!reference.has_value()) {
					LogError("Server-backed Identify ability has no authoritative stash item reference.");
				} else if (auto result = authoritative::GetServerBackedRuntime().IdentifyItemWithAbility(*reference, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
					LogError("Server-backed Identify ability failed: {}", result.error());
				}
				NewCursor(CURSOR_HAND);
				return true;
			}
#endif
			Item &item = Stash.stashList[pcursstashitem];
			item._iIdentified = true;
		}
		NewCursor(CURSOR_HAND);
		return true;
	}

	if (pcurs == CURSOR_REPAIR) {
		if (pcursinvitem != -1 && !IsInspectingPlayer()) {
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			if (authoritative::GetServerBackedRuntime().IsConnected() && !IsVisualStoreOpen) {
				const auto reference = ServerBackedReferenceForInventoryCursor(myPlayer, pcursinvitem);
				if (!reference.has_value()) {
					LogError("Server-backed Repair ability has no authoritative item reference.");
				} else if (auto result = authoritative::GetServerBackedRuntime().RepairItemWithAbility(*reference, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
					LogError("Server-backed Repair ability failed: {}", result.error());
				} else {
					PlaySfxLoc(SfxID::SpellRepair, myPlayer.position.tile);
				}
				NewCursor(CURSOR_HAND);
				return true;
			}
#endif
			if (IsVisualStoreOpen)
				VisualStoreRepairItem(pcursinvitem);
			else
				DoRepair(myPlayer, pcursinvitem);
		} else if (pcursstashitem != StashStruct::EmptyCell) {
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			if (authoritative::GetServerBackedRuntime().IsConnected()) {
				const auto reference = ServerBackedReferenceForStashCursor(pcursstashitem);
				if (!reference.has_value()) {
					LogError("Server-backed Repair ability has no authoritative stash item reference.");
				} else if (auto result = authoritative::GetServerBackedRuntime().RepairItemWithAbility(*reference, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
					LogError("Server-backed Repair ability failed: {}", result.error());
				} else {
					PlaySfxLoc(SfxID::SpellRepair, myPlayer.position.tile);
				}
				NewCursor(CURSOR_HAND);
				return true;
			}
#endif
			Item &item = Stash.stashList[pcursstashitem];
			RepairItem(item, myPlayer.getCharacterLevel());
		}
		NewCursor(CURSOR_HAND);
		return true;
	}

	if (pcurs == CURSOR_RECHARGE) {
		if (pcursinvitem != -1 && !IsInspectingPlayer()) {
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			if (authoritative::GetServerBackedRuntime().IsConnected()) {
				const auto reference = ServerBackedReferenceForInventoryCursor(myPlayer, pcursinvitem);
				if (!reference.has_value()) {
					LogError("Server-backed Staff Recharge ability has no authoritative item reference.");
				} else if (auto result = authoritative::GetServerBackedRuntime().RechargeItemWithAbility(*reference, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
					LogError("Server-backed Staff Recharge ability failed: {}", result.error());
				}
				NewCursor(CURSOR_HAND);
				return true;
			}
#endif
			DoRecharge(myPlayer, pcursinvitem);
		} else if (pcursstashitem != StashStruct::EmptyCell) {
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			if (authoritative::GetServerBackedRuntime().IsConnected()) {
				const auto reference = ServerBackedReferenceForStashCursor(pcursstashitem);
				if (!reference.has_value()) {
					LogError("Server-backed Staff Recharge ability has no authoritative stash item reference.");
				} else if (auto result = authoritative::GetServerBackedRuntime().RechargeItemWithAbility(*reference, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
					LogError("Server-backed Staff Recharge ability failed: {}", result.error());
				}
				NewCursor(CURSOR_HAND);
				return true;
			}
#endif
			Item &item = Stash.stashList[pcursstashitem];
			RechargeItem(item, myPlayer);
		}
		NewCursor(CURSOR_HAND);
		return true;
	}

	if (pcurs == CURSOR_OIL) {
		bool changeCursor = true;
		if (pcursinvitem != -1 && !IsInspectingPlayer()) {
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			if (authoritative::GetServerBackedRuntime().IsConnected()) {
				const auto target = ServerBackedReferenceForInventoryCursor(myPlayer, pcursinvitem);
				const auto source = authoritative::GetServerBackedRuntime().PendingOilUse();
				if (!target.has_value() || !source.has_value()) {
					LogError("Server-backed oil use has no authoritative source or target reference.");
					changeCursor = false;
				} else if (auto result = authoritative::GetServerBackedRuntime().UseConsumable(*source, *target, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
					LogError("Server-backed oil use failed: {}", result.error());
					changeCursor = false;
				}
				if (!changeCursor)
					return true;
			} else
#endif
			changeCursor = DoOil(myPlayer, pcursinvitem);
		} else if (pcursstashitem != StashStruct::EmptyCell) {
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
			if (authoritative::GetServerBackedRuntime().IsConnected()) {
				const auto target = ServerBackedReferenceForStashCursor(pcursstashitem);
				const auto source = authoritative::GetServerBackedRuntime().PendingOilUse();
				if (!target.has_value() || !source.has_value()) {
					LogError("Server-backed oil use has no authoritative stash target or source reference.");
					changeCursor = false;
				} else if (auto result = authoritative::GetServerBackedRuntime().UseConsumable(*source, *target, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
					LogError("Server-backed stash oil use failed: {}", result.error());
					changeCursor = false;
				}
				if (!changeCursor)
					return true;
			} else
#endif
		{
			Item &item = Stash.stashList[pcursstashitem];
			changeCursor = ApplyOilToItem(item, myPlayer);
		}
		}
		if (changeCursor)
			NewCursor(CURSOR_HAND);
		return true;
	}

	if (pcurs == CURSOR_TELEPORT) {
		SpellID spellID = myPlayer.inventorySpell;
		const SpellType spellType = SpellType::Scroll;
		const int spellFrom = myPlayer.spellFrom;
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		if (authoritative::GetServerBackedRuntime().IsConnected()) {
			uint32_t targetEntityId = 0;
			if (pcursmonst != -1 && levelType() != DTYPE_TOWN) {
				if (const auto entityId = authoritative::GetServerBackedRuntime().MonsterEntityIdAt(cursPosition.x, cursPosition.y); entityId.has_value())
					targetEntityId = *entityId;
			} else if (PlayerUnderCursor != nullptr) {
				if (const auto entityId = authoritative::GetServerBackedRuntime().PlayerEntityIdAt(cursPosition.x, cursPosition.y); entityId.has_value())
					targetEntityId = *entityId;
			}

			const auto sourceItem = ServerBackedReferenceForSpellCursor(myPlayer, spellID);
			if (!sourceItem.has_value()) {
				LogError("Server-backed spell cursor has no authoritative consumable source.");
			} else if (auto result = authoritative::GetServerBackedRuntime().Cast(
			               static_cast<uint32_t>(spellID), targetEntityId,
			               cursPosition.x, cursPosition.y,
			               { authoritative::ServerBackedCastSourceKind::ConsumableItem, sourceItem },
			               0, GetMillisecondsSinceStartup()); !result.has_value()) {
				LogError("Server-backed spell cursor cast failed: {}", result.error());
			}
			NewCursor(CURSOR_HAND);
			return true;
		}
#endif
		if (IsWallSpell(spellID)) {
			const Direction sd = GetDirection(myPlayer.position.tile, cursPosition);
			NetSendCmdLocParam4(true, CMD_SPELLXYD, cursPosition, static_cast<int8_t>(spellID), static_cast<uint8_t>(spellType), static_cast<uint16_t>(sd), spellFrom);
		} else if (pcursmonst != -1 && levelType() != DTYPE_TOWN) {
			NetSendCmdParam4(true, CMD_SPELLID, pcursmonst, static_cast<int8_t>(spellID), static_cast<uint8_t>(spellType), spellFrom);
		} else if (PlayerUnderCursor != nullptr && !PlayerUnderCursor->hasNoLife() && !myPlayer.friendlyMode) {
			NetSendCmdParam4(true, CMD_SPELLPID, PlayerUnderCursor->getId(), static_cast<int8_t>(spellID), static_cast<uint8_t>(spellType), spellFrom);
		} else {
			NetSendCmdLocParam3(true, CMD_SPELLXY, cursPosition, static_cast<int8_t>(spellID), static_cast<uint8_t>(spellType), spellFrom);
		}
		NewCursor(CURSOR_HAND);
		return true;
	}

	if (pcurs == CURSOR_DISARM && ObjectUnderCursor != nullptr) {
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		if (authoritative::GetServerBackedRuntime().IsConnected()) {
			const auto entityId = authoritative::GetServerBackedRuntime().AuthoritativeObjectEntityIdAt(cursPosition.x, cursPosition.y);
			if (!entityId.has_value()) {
				LogError("Server-backed trap target is not an authoritative entity.");
			} else if (auto result = authoritative::GetServerBackedRuntime().DisarmTrap(*entityId, 0, GetMillisecondsSinceStartup()); !result.has_value()) {
				LogError("Server-backed trap disarm failed: {}", result.error());
			}
			NewCursor(CURSOR_HAND);
			return true;
		}
#endif
	}

	if (pcurs == CURSOR_DISARM && ObjectUnderCursor == nullptr) {
		NewCursor(CURSOR_HAND);
		return true;
	}

	return false;
}

void diablo_pause_game()
{
	if (!gbIsMultiplayer) {
		if (PauseMode != 0) {
			PauseMode = 0;
		} else {
			PauseMode = 2;
			sound_stop();
			qtextflag = false;
			LastPlayerAction = PlayerActionType::None;
		}

		RedrawEverything();
	}
}

bool GameWasAlreadyPaused = false;
bool MinimizePaused = false;

bool diablo_is_focused()
{
#ifndef USE_SDL1
	return SDL_GetKeyboardFocus() == ghMainWnd;
#else
	Uint8 appState = SDL_GetAppState();
	return (appState & SDL_APPINPUTFOCUS) != 0;
#endif
}

void diablo_focus_pause()
{
	if (!movie_playing && (gbIsMultiplayer || MinimizePaused)) {
		return;
	}

	GameWasAlreadyPaused = PauseMode != 0;

	if (!GameWasAlreadyPaused) {
		PauseMode = 2;
		sound_stop();
		LastPlayerAction = PlayerActionType::None;
	}

	SVidMute();
	music_mute();

	MinimizePaused = true;
}

void diablo_focus_unpause()
{
	if (!GameWasAlreadyPaused) {
		PauseMode = 0;
	}

	SVidUnmute();
	music_unmute();

	MinimizePaused = false;
}

bool PressEscKey()
{
	bool rv = false;

	if (DoomFlag) {
		doom_close();
		rv = true;
	}

	if (HelpFlag) {
		HelpFlag = false;
		rv = true;
	}

	if (ChatLogFlag) {
		ChatLogFlag = false;
		rv = true;
	}

	if (qtextflag) {
		qtextflag = false;
		stream_stop();
		rv = true;
	}

	if (CurrentStoreManager.IsPlayerInStore()) {
		CurrentStoreManager.StoreESC();
		rv = true;
	}

	if (IsDiabloMsgAvailable()) {
		CancelCurrentDiabloMsg();
		rv = true;
	}

	if (ChatFlag) {
		ResetChat();
		rv = true;
	}

	if (DropGoldFlag) {
		control_drop_gold(SDLK_ESCAPE);
		rv = true;
	}

	if (IsWithdrawGoldOpen) {
		WithdrawGoldKeyPress(SDLK_ESCAPE);
		rv = true;
	}

	if (SpellSelectFlag) {
		SpellSelectFlag = false;
		rv = true;
	}

	if (IsLeftPanelOpen() || IsRightPanelOpen()) {
		ClosePanels();
		rv = true;
	}

	return rv;
}

void DisableInputEventHandler(const SDL_Event &event, uint16_t /*modState*/)
{
	switch (event.type) {
	case SDL_EVENT_MOUSE_MOTION:
		MousePosition = { SDLC_EventMotionIntX(event), SDLC_EventMotionIntY(event) };
		return;
	case SDL_EVENT_MOUSE_BUTTON_DOWN:
		if (sgbMouseDown != CLICK_NONE)
			return;
		switch (event.button.button) {
		case SDL_BUTTON_LEFT:
			sgbMouseDown = CLICK_LEFT;
			return;
		case SDL_BUTTON_RIGHT:
			sgbMouseDown = CLICK_RIGHT;
			return;
		default:
			return;
		}
	case SDL_EVENT_MOUSE_BUTTON_UP:
		sgbMouseDown = CLICK_NONE;
		return;
	}

	MainWndProc(event);
}

void LoadGameLevelStopMusic(_music_id neededTrack)
{
	if (neededTrack != sgnMusicTrack)
		music_stop();
}

void LoadGameLevelStartMusic(_music_id neededTrack)
{
	if (sgnMusicTrack != neededTrack)
		music_start(neededTrack);

	if (MinimizePaused) {
		music_mute();
	}
}

void LoadGameLevelResetCursor()
{
	if (pcurs > CURSOR_HAND && pcurs < CURSOR_FIRSTITEM) {
		NewCursor(CURSOR_HAND);
	}
}

void SetRndSeedForDungeonLevel()
{
	if (isSetLevel()) {
		// Maps are not randomly generated, but the monsters max hitpoints are.
		// So we need to ensure that we have a stable seed when generating quest/set-maps.
		// For this purpose we reuse the normal dungeon seeds.
		SetRndSeed(DungeonSeeds[static_cast<size_t>(setLevelNumber())]);
	} else {
		SetRndSeed(DungeonSeeds[currentLevelNumber()]);
	}
}

void LoadGameLevelFirstFlagEntry()
{
	CloseInventory();
	qtextflag = false;
	if (!HeadlessMode) {
		InitInv();
		ClearUniqueItemFlags();
		InitQuestText();
		InitInfoBoxGfx();
		InitHelp();
	}
	CurrentStoreManager.InitStores();
	CurrentAutomapManager.InitAutomapOnce();
}

void LoadGameLevelStores()
{
	if (levelType() == DTYPE_TOWN) {
		CurrentStoreManager.SetupTownStores();
	} else {
		CurrentStoreManager.FreeStoreMem();
	}
}

void LoadGameLevelStash()
{
	const bool isHellfireSaveGame = gbIsHellfireSaveGame;

	gbIsHellfireSaveGame = gbIsHellfire;
	LoadStash();
	gbIsHellfireSaveGame = isHellfireSaveGame;
}

tl::expected<void, std::string> LoadGameLevelDungeon(bool firstflag, lvl_entry lvldir, const Player &myPlayer)
{
	if (!LegacyLevelLoadMutationAllowed()) {
		// Connected worlds initialize only presentation pools and resources.
		// Do not run local monster/object/theme gameplay constructors and then
		// discard their state when the authoritative snapshot is applied.
		ObjectPoolAdapter::ResetLegacyObjectPools();
		InitializeMonsterPool();
		InitItems();
		IncProgress();
		RETURN_IF_ERROR(InitAllMonsterGFX());
		IncProgress();
		InitMissiles();
		InitCorpses();
		CurrentLightManager.SavePreLighting();
		IncProgress();
		return {};
	}
	if (firstflag || lvldir == ENTRY_LOAD || !myPlayer._pLvlVisited[currentLevelNumber()] || gbIsMultiplayer || !LegacyLevelLoadMutationAllowed()) {
		HoldThemeRooms();
		[[maybe_unused]] const uint32_t mid1Seed = GetLCGEngineState();
		InitGolems();
		InitObjects();
		[[maybe_unused]] const uint32_t mid2Seed = GetLCGEngineState();

		IncProgress();

		RETURN_IF_ERROR(InitMonsters());
		InitItems();
		CreateThemeRooms();

		IncProgress();

		[[maybe_unused]] const uint32_t mid3Seed = GetLCGEngineState();
		InitMissiles();
		InitCorpses();
#ifdef _DEBUG
		SetDebugLevelSeedInfos(mid1Seed, mid2Seed, mid3Seed, GetLCGEngineState());
#endif
		CurrentLightManager.SavePreLighting();

		IncProgress();

		if (gbIsMultiplayer)
			DeltaLoadLevel();
	} else {
		HoldThemeRooms();
		InitGolems();
		RETURN_IF_ERROR(InitMonsters());
		InitMissiles();
		InitCorpses();

		IncProgress();

		RETURN_IF_ERROR(LoadLevel());

		IncProgress();
	}
	return {};
}

void LoadGameLevelSyncPlayerEntry(lvl_entry lvldir)
{
	for (Player &player : Players) {
		if (player.plractive && player.isOnActiveLevel() && (!player._pLvlChanging || &player == MyPlayer)) {
			if (player.life.current > 0) {
				if (lvldir != ENTRY_LOAD && LegacyLevelLoadPlayerPlacementAllowed())
					player.syncInitialPosition();
			} else {
				tileAt(player.position.tile).addFlags(DungeonFlag::DeadPlayer);
			}
		}
	}
}

void LoadGameLevelLightVision()
{
	if (levelType() != DTYPE_TOWN) {
		for (Tile &tile : tiles().columnMajor())
			tile.setLight(tile.preLight());
		CurrentLightManager.ChangeLightXY(Players[MyPlayerId].lightId, Players[MyPlayerId].position.tile); // forces player light refresh
		CurrentLightManager.ProcessLightList();
		CurrentLightManager.ProcessVisionList();
	}
}

#ifdef BUILD_TESTING
void CompleteFreshLevelForTesting(lvl_entry entry)
{
	InitMissiles();
	InitCorpses();
	CurrentLightManager.SavePreLighting();
	SyncPortals();
	LoadGameLevelSyncPlayerEntry(entry);
	UpdateMonsterLights();
	UnstuckChargers();
	LoadGameLevelLightVision();
}
#endif

void LoadGameLevelReturn()
{
	if (!LegacyLevelLoadMutationAllowed())
		return;

	viewPosition() = GetMapReturnPosition();
	if (Quests[Q_BETRAYER]._qactive == QUEST_DONE)
		Quests[Q_BETRAYER]._qvar2 = 2;
}

void LoadGameLevelInitPlayers(bool firstflag, lvl_entry lvldir)
{
	for (Player &player : Players) {
		if (player.plractive && player.isOnActiveLevel()) {
			player.initGraphics();
			if (lvldir != ENTRY_LOAD)
				InitPlayer(player, firstflag);
		}
	}
}

void LoadGameLevelSetVisited()
{
	bool visited = false;
	for (const Player &player : Players) {
		if (player.plractive)
			visited = visited || player._pLvlVisited[currentLevelNumber()];
	}
}

tl::expected<void, std::string> LoadGameLevelTown(bool firstflag, lvl_entry lvldir, const Player &myPlayer)
{
	for (Tile &tile : tiles().columnMajor())
		tile.addFlags(DungeonFlag::Lit);

	InitTowners();
	InitStash();
	InitVisualStore();
	InitItems();
	InitMissiles();

	IncProgress();

	if (!firstflag && lvldir != ENTRY_LOAD && myPlayer._pLvlVisited[currentLevelNumber()] && !gbIsMultiplayer && LegacyLevelLoadMutationAllowed())
		RETURN_IF_ERROR(LoadLevel());
	if (gbIsMultiplayer)
		DeltaLoadLevel();

	IncProgress();

	for (int x = 0; x < DMAXX; x++)
		for (int y = 0; y < DMAXY; y++)
			CurrentAutomapManager.UpdateAutomapExplorer({ x, y }, MAP_EXP_SELF);
	return {};
}

tl::expected<void, std::string> LoadGameLevelSetLevel(bool firstflag, lvl_entry lvldir, const Player &myPlayer)
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (authoritative::GetServerBackedRuntime().IsConnected())
		RETURN_IF_ERROR(InitObjectGFX());
#endif
	LoadSetMap();
	IncProgress();
	RETURN_IF_ERROR(GetLevelMTypes());
	IncProgress();
	InitGolems();
	RETURN_IF_ERROR(InitMonsters());
	IncProgress();
	if (!HeadlessMode) {
#if !defined(USE_SDL1) && !defined(__vita__)
		InitVirtualGamepadGFX();
#endif
		RETURN_IF_ERROR(InitMissileGFX());
		IncProgress();
	}
	InitCorpses();
	IncProgress();

	if (lvldir == ENTRY_WARPLVL)
		GetPortalLvlPos();
	IncProgress();

	for (Player &player : Players) {
		if (player.plractive && player.isOnActiveLevel()) {
			player.initGraphics();
			if (lvldir != ENTRY_LOAD)
				InitPlayer(player, firstflag);
		}
	}
	IncProgress();
	InitMultiView();
	IncProgress();

	if (firstflag || lvldir == ENTRY_LOAD || !myPlayer._pSLvlVisited[setLevelNumber()] || gbIsMultiplayer || !LegacyLevelLoadMutationAllowed()) {
		InitItems();
		CurrentLightManager.SavePreLighting();
	} else {
		RETURN_IF_ERROR(LoadLevel());
	}
	if (gbIsMultiplayer) {
		DeltaLoadLevel();
		if (!UseMultiplayerQuests())
			ResyncQuests();
	}

	PlayDungMsgs();
	InitMissiles();
	IncProgress();
	return {};
}

tl::expected<void, std::string> LoadGameLevelStandardLevel(bool firstflag, lvl_entry lvldir, const Player &myPlayer)
{
	CreateLevel(lvldir);

	IncProgress();

	SetRndSeedForDungeonLevel();

	if (levelType() != DTYPE_TOWN) {
		RETURN_IF_ERROR(GetLevelMTypes());
		InitThemes();
		if (!HeadlessMode)
			RETURN_IF_ERROR(LoadAllGFX());
	} else if (!HeadlessMode) {
		IncProgress();

#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		if (authoritative::GetServerBackedRuntime().IsConnected())
			RETURN_IF_ERROR(InitObjectGFX());
#endif

#if !defined(USE_SDL1) && !defined(__vita__)
		InitVirtualGamepadGFX();
#endif

		IncProgress();

		RETURN_IF_ERROR(InitMissileGFX());

		IncProgress();
		IncProgress();
	}

	IncProgress();

	if (lvldir == ENTRY_RTNLVL) {
		LoadGameLevelReturn();
	}

	if (lvldir == ENTRY_WARPLVL)
		GetPortalLvlPos();

	IncProgress();

	LoadGameLevelInitPlayers(firstflag, lvldir);
	InitMultiView();

	IncProgress();

	LoadGameLevelSetVisited();

	SetRndSeedForDungeonLevel();

	if (levelType() == DTYPE_TOWN) {
		RETURN_IF_ERROR(LoadGameLevelTown(firstflag, lvldir, myPlayer));
	} else {
		RETURN_IF_ERROR(LoadGameLevelDungeon(firstflag, lvldir, myPlayer));
	}

	PlayDungMsgs();

	if (UseMultiplayerQuests())
		ResyncMPQuests();
	else
		ResyncQuests();
	return {};
}

void LoadGameLevelCrypt()
{
	if (LegacyLevelLoadMutationAllowed() && CornerStone.isAvailable()) {
		CornerstoneLoad(CornerStone.position);
	}
	if (LegacyLevelLoadMutationAllowed()
	    && Quests[Q_NAKRUL]._qactive == QUEST_DONE && currentLevelNumber() == 24) {
		SyncNakrulRoom();
	}
}

void LoadGameLevelCalculateCursor()
{
	// Recalculate mouse selection of entities after level change/load
	LastPlayerAction = PlayerActionType::None;
	sgbMouseDown = CLICK_NONE;
	ResetItemlabelHighlighted(); // level changed => item changed
	pcursmonst = -1;             // ensure pcurstemp is set to a valid value
	CheckCursMove();
}

tl::expected<void, std::string> LoadGameLevel(bool firstflag, lvl_entry lvldir)
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (authoritative::GetServerBackedRuntime().IsConnected())
		RETURN_IF_ERROR(authoritative::GetServerBackedRuntime().PrepareNativeLevel());
#endif
	const _music_id neededTrack = GetLevelMusic(levelType());

	ClearFloatingNumbers();
	LoadGameLevelStopMusic(neededTrack);
	LoadGameLevelResetCursor();
	SetRndSeedForDungeonLevel();
	NaKrulTomeSequence = 0;

	IncProgress();

	RETURN_IF_ERROR(CurrentLightManager.LoadTrns());
	CurrentLightManager.MakeLightTable();
	RETURN_IF_ERROR(LoadLevelSOLData());

	IncProgress();

	RETURN_IF_ERROR(LoadLvlGFX());
	SetDungeonMicros(dungeonCels(), microTileLength());
	ClearClxDrawCache();

	IncProgress();

	if (firstflag) {
		LoadGameLevelFirstFlagEntry();
	}

	SetRndSeedForDungeonLevel();

	LoadGameLevelStores();

	if (firstflag || lvldir == ENTRY_LOAD) {
		LoadGameLevelStash();
	}

	IncProgress();

	CurrentAutomapManager.InitAutomap();

	if (levelType() != DTYPE_TOWN && lvldir != ENTRY_LOAD) {
		CurrentLightManager.Init();
	}

	InitLevelMonsters();

	IncProgress();

	const Player &myPlayer = *MyPlayer;

	if (isSetLevel()) {
		RETURN_IF_ERROR(LoadGameLevelSetLevel(firstflag, lvldir, myPlayer));
	} else {
		RETURN_IF_ERROR(LoadGameLevelStandardLevel(firstflag, lvldir, myPlayer));
	}

	SyncPortals();
	LoadGameLevelSyncPlayerEntry(lvldir);

	IncProgress();
	IncProgress();

	if (firstflag) {
		RETURN_IF_ERROR(InitMainPanel());
	}

	IncProgress();

	UpdateMonsterLights();
	UnstuckChargers();

	LoadGameLevelLightVision();

	if (levelType() == DTYPE_CRYPT) {
		LoadGameLevelCrypt();
	}

#ifndef USE_SDL1
	ActivateVirtualGamepad();
#endif
	LoadGameLevelStartMusic(neededTrack);

	CompleteProgress();

	LoadGameLevelCalculateCursor();
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (authoritative::GetServerBackedRuntime().IsConnected())
		RETURN_IF_ERROR(authoritative::GetServerBackedRuntime().MarkNativeLevelReady());
#endif
	return {};
}

bool game_loop(bool bStartup)
{
	const uint16_t wait = bStartup ? sgGameInitInfo.nTickRate * 3 : 3;

	for (unsigned i = 0; i < wait; i++) {
		if (!multi_handle_delta()) {
			TimeoutCursor(true);
			return false;
		}
		TimeoutCursor(false);
		GameLogic();
		ClearLastSentPlayerCmd();

		if (!gbRunGame || !gbIsMultiplayer || demo::IsRunning() || demo::IsRecording() || !nthread_has_500ms_passed())
			break;
	}
	return true;
}

void diablo_color_cyc_logic()
{
	if (!*GetOptions().Graphics.colorCycling)
		return;

	if (PauseMode != 0)
		return;

	if (levelType() == DTYPE_CAVES) {
		if (isSetLevel() && setLevelNumber() == Quests[Q_PWATER]._qslvl) {
			UpdatePWaterPalette();
		} else {
			palette_update_caves();
		}
	} else if (levelType() == DTYPE_HELL) {
		CurrentLightManager.lighting_color_cycling();
	} else if (levelType() == DTYPE_NEST) {
		palette_update_hive();
	} else if (levelType() == DTYPE_CRYPT) {
		palette_update_crypt();
	}
}

bool IsDiabloAlive(bool playSFX)
{
	if (Quests[Q_DIABLO]._qactive == QUEST_DONE && !gbIsMultiplayer) {
		if (playSFX)
			PlaySFX(SfxID::DiabloDeath);
		return false;
	}

	return true;
}

void PrintScreen(SDL_Keycode vkey)
{
	ReleaseKey(vkey);
}

} // namespace devilution

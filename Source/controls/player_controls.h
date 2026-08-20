#pragma once
/**
 * @file controls/player_controls.h
 *
 * Interface for player controls.
 */


// Controller actions implementation

#include <cstddef>
#include <cstdint>

#ifdef USE_SDL3
#include <SDL3/SDL_events.h>
#else
#include <SDL.h>
#endif

#include "controls/controller.h"
#include "controls/game_controls.h"
#include "game/players/players.hpp"

namespace devilution {

enum class BeltItemType : uint8_t {
	Healing,
	Mana,
};

/**
 * Describes how a highlighted native object may be handled while the
 * server-backed client is connected.
 */
enum class ServerBackedObjectInteractionRoute : uint8_t {
	None,
	Submit,
	FailClosed,
};

/**
 * Keeps a stale native object highlight from falling through to a legacy
 * object command when the authoritative projection has no matching entity.
 */
constexpr ServerBackedObjectInteractionRoute ResolveServerBackedObjectInteractionRoute(
	bool serverConnected,
	bool objectHighlighted,
	bool projectedEntityAvailable) noexcept
{
	if (!serverConnected)
		return ServerBackedObjectInteractionRoute::None;
	if (projectedEntityAvailable)
		return ServerBackedObjectInteractionRoute::Submit;
	return objectHighlighted
	    ? ServerBackedObjectInteractionRoute::FailClosed
	    : ServerBackedObjectInteractionRoute::None;
}

extern GameActionType ControllerActionHeld;
extern bool StandToggle;

// Runs every frame.
// Handles menu movement.
void plrctrls_every_frame();

// Run after every game logic iteration.
// Handles player movement.
void plrctrls_after_game_logic();

// Runs at the end of CheckCursMove()
// Handles item, object, and monster auto-aim.
void plrctrls_after_check_curs_move();

// Moves the map if active, the cursor otherwise.
void HandleRightStickMotion();

// Whether we're in a dialog menu that the game handles natively with keyboard controls.
bool InGameMenu();

void SetPointAndClick(bool value);

bool IsPointAndClick();
bool IsMovementHandlerActive();

void DetectInputMethod(const SDL_Event &event, const ControllerButtonEvent &gamepadEvent);
void ProcessGameAction(const GameAction &action);

void UseBeltItem(BeltItemType type);

// Talk to towners, click on inv items, attack, etc.
void PerformPrimaryAction();

// Handle button releases for primary action (e.g., visual store buttons)
void PerformPrimaryActionRelease();

// Open chests, doors, pickup items.
void PerformSecondaryAction();
void UpdateSpellTarget(SpellID spell);
bool TryDropItem();
void InvalidateInventorySlot();
void FocusOnInventory();
void FocusOnVisualStore();
void PerformSpellAction();
void QuickCast(size_t slot);

extern int speedspellcount;

} // namespace devilution

#pragma once

/**
 * @file ui/panel/partypanel.hpp
 *
 * Interface for party panel.
 */


#include <string>

#include <expected.hpp>

#include "engine/gfx/clx_sprite.hpp"
#include "engine/gfx/surface.hpp"

namespace devilution {

struct Player;

extern bool PartySidePanelOpen;
extern bool InspectingFromPartyPanel;
extern int PortraitIdUnderCursor;

tl::expected<void, std::string> LoadPartyPanel();
void FreePartyPanel();
void DrawPartyMemberInfoPanel(const Surface &out);
bool DidRightClickPartyPortrait();
/** Resolves a visible portrait slot to its current presentation player. */
const Player *PartyPortraitPlayerAt(int portraitId);

} // namespace devilution

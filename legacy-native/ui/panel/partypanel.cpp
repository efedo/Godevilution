/**
 * @file ui/panel/partypanel.cpp
 *
 * Implementation of party panel.
 */

#include "ui/panel/partypanel.hpp"

#include <algorithm>
#include <array>
#include <expected.hpp>
#include <optional>

#include "engine/gfx/backbuffer_state.hpp"
#include "engine/gfx/clx_sprite.hpp"
#include "engine/gfx/palette.h"
#include "engine/load/load_cel.hpp"
#include "engine/load/load_clx.hpp"
#include "engine/math/rectangle.hpp"
#include "engine/math/size.hpp"
#include "engine/render/clx_render.hpp"
#include "engine/render/primitive_render.hpp"
#include "game/players/inventory.hpp"
#include "game/stores/stores.hpp"
#include "persistence/options.h"
#include "persistence/save_file.h"
#include "qol/monhealthbar.h"
#include "qol/stash.h"
#include "tables/playerdat.hpp"
#include "ui/automap.h"
#include "ui/panel/control.hpp"
#include "utils/image/surface_to_clx.hpp"
#include "utils/status_macros.hpp"

#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
#include "network/authoritative/server_backed_runtime.hpp"
#endif

namespace devilution {

namespace {

struct PartySpriteOffset {
	Point inTownOffset;
	Point inDungeonOffset;
	Point isDeadOffset;
};

const PartySpriteOffset ClassSpriteOffsets[] = {
	{ { -4, -18 }, { 6, -21 }, { -6, -50 } },
	{ { -2, -18 }, { 1, -20 }, { -8, -35 } },
	{ { -2, -16 }, { 3, -20 }, { 0, -50 } },
	{ { -2, -19 }, { 1, -19 }, { 28, -60 } }
};

OptionalOwnedClxSpriteList PartyMemberFrame;
OptionalOwnedClxSpriteList PlayerTags;

Point PartyPanelPos = { 8, 8 };
Rectangle PortraitFrameRects[MAX_PLRS];
int RightClickedPortraitIndex = -1;
std::array<std::optional<uint32_t>, MAX_PLRS> RemotePortraitEntityIds;
std::optional<uint32_t> RightClickedRemoteEntityId;
constexpr int HealthBarHeight = 7;
constexpr int ManaBarHeight = 7;
constexpr int FrameGap = 15;
constexpr int FrameBorderSize = 3;
constexpr int FrameSpriteSize = 12;
constexpr Size FrameSections = { 4, 4 }; // x/y can't be less than 2
constexpr Size PortraitFrameSize = { FrameSections.width * FrameSpriteSize, FrameSections.height *FrameSpriteSize };

constexpr uint8_t FrameBackgroundColor = PAL16_BLUE + 14;

void DrawBar(const Surface &out, Rectangle rect, uint8_t color)
{
	for (int x = 0; x < rect.size.width; x++) {
		DrawVerticalLine(out, { rect.position.x + x, rect.position.y }, rect.size.height, color);
	}
}

void DrawMemberFrame(const Surface &out, OwnedClxSpriteList &frame, Point pos)
{
	// Draw the frame background
	FillRect(out, pos.x, pos.y, PortraitFrameSize.width, PortraitFrameSize.height, FrameBackgroundColor);

	// Now draw the frame border
	const Size adjustedFrame = { FrameSections.width - 1, FrameSections.height - 1 };
	for (int x = 0; x <= adjustedFrame.width; x++) {
		for (int y = 0; y <= adjustedFrame.height; y++) {
			// Get what section of the frame we're drawing
			int spriteIndex = -1;
			if (x == 0 && y == 0)
				spriteIndex = 0; // Top-left corner
			else if (x == 0 && y == adjustedFrame.height)
				spriteIndex = 1; // Bottom-left corner
			else if (x == adjustedFrame.width && y == adjustedFrame.height)
				spriteIndex = 2; // Bottom-right corner
			else if (x == adjustedFrame.width && y == 0)
				spriteIndex = 3; // Top-right corner
			else if (y == 0)
				spriteIndex = 4; // Top border
			else if (x == 0)
				spriteIndex = 5; // Left border
			else if (y == adjustedFrame.height)
				spriteIndex = 6; // Bottom border
			else if (x == adjustedFrame.width)
				spriteIndex = 7; // Right border

			if (spriteIndex != -1) {
				// Draw the frame section
				RenderClxSprite(out, frame[spriteIndex], { pos.x + (x * FrameSpriteSize), pos.y + (y * FrameSpriteSize) });
			}
		}
	}
}

void HandleRightClickPortait()
{
	if (RightClickedRemoteEntityId.has_value()) {
		bool inspectionStarted = false;
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		if (authoritative::GetServerBackedRuntime().IsAuthoritativeMode()) {
			if (Player *player = authoritative::GetServerBackedRuntime().FindRemotePlayer(*RightClickedRemoteEntityId); player != nullptr) {
				InspectPlayer = player;
				inspectionStarted = true;
				OpenCharPanel();
				if (!SpellbookFlag)
					invflag = true;
				RedrawEverything();
			}
		}
#endif
		if (!inspectionStarted)
			InspectingFromPartyPanel = false;
		RightClickedRemoteEntityId.reset();
		RightClickedPortraitIndex = -1;
		return;
	}

	if (RightClickedPortraitIndex < 0 || RightClickedPortraitIndex >= MAX_PLRS)
		return;

	Player &player = Players[RightClickedPortraitIndex];
	if (player.plractive && &player != MyPlayer) {
		InspectPlayer = &player;
		OpenCharPanel();
		if (!SpellbookFlag)
			invflag = true;
		RedrawEverything();
		RightClickedPortraitIndex = -1;
	}
}

PartySpriteOffset GetClassSpriteOffset(HeroClass hClass)
{
	switch (hClass) {
	case HeroClass::Bard:
		hClass = HeroClass::Rogue;
		break;
	case HeroClass::Barbarian:
		hClass = HeroClass::Warrior;
		break;
	default:
		break;
	}

	return ClassSpriteOffsets[static_cast<size_t>(hClass)];
}

void DrawPartyMember(
    const Surface &out,
    const Player &player,
    int displayId,
    bool interactive,
    Point &pos,
    int &currentLongestNameWidth,
    bool &portraitUnderCursor)
{
	const Rectangle currentPortraitRect = { pos, PortraitFrameSize };
	const Surface gameScreen = out.subregionY(0, gnViewportHeight);

	RenderClxSprite(gameScreen, (*PartyMemberFrame)[0], pos);

	const int lifeTicks = player.life.maximum > 0
	    ? ((player.life.current * PortraitFrameSize.width) + (player.life.maximum / 2)) / player.life.maximum
	    : 0;
	const uint8_t hpBarColor = (player.pManaShield) ? PAL8_YELLOW + 5 : PAL8_RED + 4;
	DrawBar(gameScreen, { pos, { lifeTicks, HealthBarHeight } }, hpBarColor);

	pos.y += HealthBarHeight;

	const ClxSprite playerPortraitSprite = player.getPortraitSprite();
	const PartySpriteOffset offsets = GetClassSpriteOffset(player._pClass);
	Point offset = (player.isOnLevel(0)) ? offsets.inTownOffset : offsets.inDungeonOffset;
	if (player.life.current <= 0 && player.isUnarmed())
		offset = offsets.isDeadOffset;

	const Point portraitPos = { ((-(playerPortraitSprite.width() / 2)) + (PortraitFrameSize.width / 2)) + offset.x, offset.y };
	const Surface frameSubregion = gameScreen.subregion(
	    pos.x + FrameBorderSize,
	    pos.y + FrameBorderSize,
	    PortraitFrameSize.width - (FrameBorderSize * 2),
	    PortraitFrameSize.height - (FrameBorderSize * 2));

	if (interactive && displayId >= 0 && displayId < MAX_PLRS) {
		PortraitFrameRects[displayId] = {
			{ frameSubregion.region.x, frameSubregion.region.y },
			{ frameSubregion.region.w, frameSubregion.region.h }
		};
	}

	RenderClxSprite(frameSubregion, playerPortraitSprite, portraitPos);

	if (displayId >= 0 && static_cast<size_t>(displayId + 1) < (*PlayerTags).numSprites()) {
		const int tagWidth = (*PlayerTags)[displayId + 1].width();
		RenderClxSprite(
		    frameSubregion,
		    (*PlayerTags)[displayId + 1],
		    { PortraitFrameSize.width - (tagWidth + (tagWidth / 2)), 0 });
	}

	if (player.life.current <= 0) {
		DrawHalfTransparentRectTo(
		    frameSubregion,
		    0, 0,
		    PortraitFrameSize.width,
		    PortraitFrameSize.height,
		    PAL8_RED + 4);
	}

	pos.y += PortraitFrameSize.height;

	const int manaTicks = player.mana.maximum > 0
	    ? ((player.mana.current * PortraitFrameSize.width) + (player.mana.maximum / 2)) / player.mana.maximum
	    : 0;
	DrawBar(gameScreen, { pos, { manaTicks, ManaBarHeight } }, PAL8_BLUE + 3);

	pos.y += ManaBarHeight;

	DrawString(
	    gameScreen,
	    player._pName,
	    pos,
	    { .flags = UiFlags::ColorGold | UiFlags::Outlined | UiFlags::FontSize12 });

	pos.y += FrameGap + 5;

	if (interactive && currentPortraitRect.contains(MousePosition)) {
		PortraitIdUnderCursor = displayId;
		portraitUnderCursor = true;
	}

	const int width = GetLineWidth(player._pName);
	if (width >= currentLongestNameWidth)
		currentLongestNameWidth = width;

	if (pos.y >= GetMainPanel().position.y - PortraitFrameSize.height - 10) {
		pos.y = PartyPanelPos.y;
		if (CurrentAutomapManager.GetAutomapActive())
			pos.y += (FrameGap * 4);
		if (*GetOptions().Graphics.showFPS)
			pos.y += FrameGap;
		pos.x += currentLongestNameWidth + (FrameGap / 2);
	}
}

} // namespace

bool PartySidePanelOpen = true;
bool InspectingFromPartyPanel;
int PortraitIdUnderCursor = -1;

tl::expected<void, std::string> LoadPartyPanel()
{
	ASSIGN_OR_RETURN(OwnedClxSpriteList frame, LoadCelWithStatus("data\\textslid", FrameSpriteSize));
	ASSIGN_OR_RETURN(PlayerTags, LoadClxWithStatus("data\\monstertags.clx"));
	const OwnedSurface out(PortraitFrameSize.width, PortraitFrameSize.height + HealthBarHeight + ManaBarHeight);

	// Draw the health bar background
	DrawBar(out, { { 0, 0 }, { PortraitFrameSize.width, HealthBarHeight } }, PAL16_GRAY + 10);
	// Draw the frame the character portrait sprite will go
	DrawMemberFrame(out, frame, { 0, HealthBarHeight });
	// Draw the mana bar background
	DrawBar(out, { { 0, HealthBarHeight + PortraitFrameSize.height }, { PortraitFrameSize.width, ManaBarHeight } }, PAL16_GRAY + 10);

	PartyMemberFrame = SurfaceToClx(out);

	return {};
}

void FreePartyPanel()
{
	PartyMemberFrame = std::nullopt;
	PlayerTags = std::nullopt;
}

void DrawPartyMemberInfoPanel(const Surface &out)
{
	std::fill_n(PortraitFrameRects, MAX_PLRS, Rectangle {});
	RemotePortraitEntityIds.fill(std::nullopt);

	// Don't draw based on these criteria
	if (CharFlag || !gbIsMultiplayer || !MyPlayer->friendlyMode || CurrentStoreManager.IsPlayerInStore() || IsStashOpen) {
		if (PortraitIdUnderCursor != -1)
			PortraitIdUnderCursor = -1;

		return;
	}

	Point pos = PartyPanelPos;
	if (CurrentAutomapManager.GetAutomapActive())
		pos.y += (FrameGap * 4);
	if (*GetOptions().Graphics.showFPS)
		pos.y += FrameGap;

	int currentLongestNameWidth = PortraitFrameSize.width;
	bool portraitUnderCursor = false;

	for (Player &player : Players) {

		if (!player.plractive || !player.friendlyMode)
			continue;

#ifndef _DEBUG
		if (&player == MyPlayer)
			continue;
#endif
		// Get the rect of the portrait to use later
		const Rectangle currentPortraitRect = { pos, PortraitFrameSize };

		const Surface gameScreen = out.subregionY(0, gnViewportHeight);

		// Draw the characters frame
		RenderClxSprite(gameScreen, (*PartyMemberFrame)[0], pos);

		// Get the players remaining life
		// If the player is using mana shield change the color
		const int lifeTicks = ((player.life.current * PortraitFrameSize.width) + (player.life.maximum / 2)) / player.life.maximum;
		const uint8_t hpBarColor = (player.pManaShield) ? PAL8_YELLOW + 5 : PAL8_RED + 4;
		// Now draw the characters remaining life
		DrawBar(gameScreen, { pos, { lifeTicks, HealthBarHeight } }, hpBarColor);

		// Add to the position before continuing to the next item
		pos.y += HealthBarHeight;

		// Get the players current portrait sprite
		const ClxSprite playerPortraitSprite = player.getPortraitSprite();
		// Get the offset of the sprite based on the players class so it get's rendered in the correct position
		const PartySpriteOffset offsets = GetClassSpriteOffset(player._pClass);
		Point offset = (player.isOnLevel(0)) ? offsets.inTownOffset : offsets.inDungeonOffset;

		if (player.life.current <= 0 && player.isUnarmed())
			offset = offsets.isDeadOffset;

		// Calculate the players portait position
		const Point portraitPos = { ((-(playerPortraitSprite.width() / 2)) + (PortraitFrameSize.width / 2)) + offset.x, offset.y };
		// Get a subregion of the surface so the portrait doesn't get drawn over the frame
		const Surface frameSubregion = gameScreen.subregion(
		    pos.x + FrameBorderSize,
		    pos.y + FrameBorderSize,
		    PortraitFrameSize.width - (FrameBorderSize * 2),
		    PortraitFrameSize.height - (FrameBorderSize * 2));

		PortraitFrameRects[player.getId()] = {
			{ frameSubregion.region.x, frameSubregion.region.y },
			{ frameSubregion.region.w, frameSubregion.region.h }
		};

		// Draw the portrait sprite
		RenderClxSprite(
		    frameSubregion,
		    playerPortraitSprite,
		    portraitPos);

		if ((player.getId() + 1U) < (*PlayerTags).numSprites()) {
			// Draw the player tag
			const int tagWidth = (*PlayerTags)[player.getId() + 1].width();
			RenderClxSprite(
			    frameSubregion,
			    (*PlayerTags)[player.getId() + 1],
			    { PortraitFrameSize.width - (tagWidth + (tagWidth / 2)), 0 });
		}

		// Check to see if the player is dead and if so we draw a half transparent red rect over the portrait
		if (player.life.current <= 0) {
			DrawHalfTransparentRectTo(
			    frameSubregion,
			    0, 0,
			    PortraitFrameSize.width,
			    PortraitFrameSize.height,
			    PAL8_RED + 4);
		}

		// Add to the position before continuing to the next item
		pos.y += PortraitFrameSize.height;

		// Get the players remaining mana
		const int manaTicks = ((player.mana.current * PortraitFrameSize.width) + (player.mana.maximum / 2)) / player.mana.maximum;
		const uint8_t manaBarColor = PAL8_BLUE + 3;
		// Now draw the characters remaining mana
		DrawBar(gameScreen, { pos, { manaTicks, ManaBarHeight } }, manaBarColor);

		// Add to the position before continuing to the next item
		pos.y += ManaBarHeight;

		// Draw the players name under the frame
		DrawString(
		    gameScreen,
		    player._pName,
		    pos,
		    { .flags = UiFlags::ColorGold | UiFlags::Outlined | UiFlags::FontSize12 });

		// Add to the position before continuing onto the next player
		pos.y += FrameGap + 5;

		// Check to see if the player is hovering over this portrait and if so draw a string under the cursor saying they can right click to inspect
		if (currentPortraitRect.contains(MousePosition)) {
			PortraitIdUnderCursor = player.getId();
			portraitUnderCursor = true;
		}

		// Get the current players name width
		const int width = GetLineWidth(player._pName);
		// Now check to see if it's the current longest name
		if (width >= currentLongestNameWidth)
			currentLongestNameWidth = width;

		// Check to see if the Y position is more then the main panel position
		if (pos.y >= GetMainPanel().position.y - PortraitFrameSize.height - 10) {
			// If so we need to draw the next set of portraits back at the top and to the right of the original position
			pos.y = PartyPanelPos.y;
			if (CurrentAutomapManager.GetAutomapActive())
				pos.y += (FrameGap * 4);
			if (*GetOptions().Graphics.showFPS)
				pos.y += FrameGap;
			// Add the current longest name width to the X position
			pos.x += currentLongestNameWidth + (FrameGap / 2);
		}
	}

#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	if (authoritative::GetServerBackedRuntime().IsAuthoritativeMode()) {
		std::array<bool, MAX_PLRS> occupiedPortraitSlots {};
		for (const Player &localPlayer : Players) {
			if (localPlayer.plractive && localPlayer.friendlyMode && localPlayer.getId() < MAX_PLRS)
				occupiedPortraitSlots[localPlayer.getId()] = true;
		}
		authoritative::GetServerBackedRuntime().ForEachRemotePlayer([&](uint32_t entityId, const Player &player) {
			if (!player.plractive || !player.friendlyMode)
				return;
			const auto freeSlot = std::find(occupiedPortraitSlots.begin(), occupiedPortraitSlots.end(), false);
			if (freeSlot == occupiedPortraitSlots.end())
				return;
			const int displayId = static_cast<int>(std::distance(occupiedPortraitSlots.begin(), freeSlot));
			occupiedPortraitSlots[displayId] = true;
			RemotePortraitEntityIds[displayId] = entityId;
			DrawPartyMember(out, player, displayId, true, pos, currentLongestNameWidth, portraitUnderCursor);
		});
	}
#endif

	if (RightClickedPortraitIndex != -1)
		HandleRightClickPortait();

	if (!portraitUnderCursor)
		PortraitIdUnderCursor = -1;
}

bool DidRightClickPartyPortrait()
{
	for (size_t i = 0; i < sizeof(PortraitFrameRects) / sizeof(PortraitFrameRects[0]); i++) {
		if (PortraitFrameRects[i].contains(MousePosition)) {
			const int portraitIndex = static_cast<int>(i);
			RightClickedPortraitIndex = RemotePortraitEntityIds[portraitIndex].has_value() ? -1 : portraitIndex;
			RightClickedRemoteEntityId = RemotePortraitEntityIds[portraitIndex];
			InspectingFromPartyPanel = true;
			return true;
		}
	}

	return false;
}

const Player *PartyPortraitPlayerAt(int portraitId)
{
	if (portraitId < 0 || portraitId >= MAX_PLRS)
		return nullptr;
	if (RemotePortraitEntityIds[portraitId].has_value()) {
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
		if (authoritative::GetServerBackedRuntime().IsAuthoritativeMode())
			return authoritative::GetServerBackedRuntime().FindRemotePlayer(*RemotePortraitEntityIds[portraitId]);
#endif
		return nullptr;
	}
	return &Players[portraitId];
}

} // namespace devilution

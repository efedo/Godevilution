/**
 * @file ui/debug_overlay/overlay_state.cpp
 *
 * Implementation of debug overlay state.
 */


#include "ui/debug_overlay/overlay_state.hpp"

#include <algorithm>
#include <iterator>

namespace devilution {

DebugOverlayTextInputAction DebugOverlayTextInputState::Update(bool consoleVisible)
{
	if (consoleVisible == ownsTextInput_)
		return DebugOverlayTextInputAction::None;

	ownsTextInput_ = consoleVisible;
	return consoleVisible ? DebugOverlayTextInputAction::Start : DebugOverlayTextInputAction::Stop;
}

DebugOverlayDisplaySize ResolveDebugOverlayDisplaySize(
    int windowWidth, int windowHeight, int logicalWidth, int logicalHeight)
{
	if (logicalWidth > 0 && logicalHeight > 0) {
		return { static_cast<float>(logicalWidth), static_cast<float>(logicalHeight) };
	}
	return { static_cast<float>(windowWidth), static_cast<float>(windowHeight) };
}

DebugOverlayDisplaySize ResolveDebugPiecePreviewSize(
    int sourceWidth, int sourceHeight, float maximumWidth, float maximumHeight)
{
	const float scale = std::min(
	    maximumWidth / static_cast<float>(sourceWidth),
	    maximumHeight / static_cast<float>(sourceHeight));
	return {
		static_cast<float>(sourceWidth) * scale,
		static_cast<float>(sourceHeight) * scale,
	};
}

bool IsDebugPiecePreviewFoliage(bool isFloorPiece, std::size_t blockIndex, TileType tileType)
{
	return isFloorPiece && blockIndex < 2 && tileType == TileType::TransparentSquare;
}

void DebugPieceSelectorState::Open(uint16_t currentPiece)
{
	open_ = true;
	selectedPiece_ = currentPiece;
	appliedPiece_.reset();
}

bool DebugPieceSelectorState::IsOpen() const
{
	return open_;
}

void DebugPieceSelectorState::Select(uint16_t piece)
{
	selectedPiece_ = piece;
}

uint16_t DebugPieceSelectorState::GetSelectedPiece() const
{
	return selectedPiece_;
}

void DebugPieceSelectorState::Apply()
{
	appliedPiece_ = selectedPiece_;
	open_ = false;
}

void DebugPieceSelectorState::Cancel()
{
	appliedPiece_.reset();
	open_ = false;
}

std::optional<uint16_t> DebugPieceSelectorState::TakeAppliedPiece()
{
	std::optional<uint16_t> result = appliedPiece_;
	appliedPiece_.reset();
	return result;
}

bool DebugOverlayState::IsActive() const
{
	return active_;
}

void DebugOverlayState::ToggleActive()
{
	if (active_) {
		Close();
		return;
	}

	active_ = true;
}

void DebugOverlayState::Close()
{
	active_ = false;
	std::fill(std::begin(visibleWindows_), std::end(visibleWindows_), false);
}

bool DebugOverlayState::IsWindowVisible(DebugOverlayWindow window) const
{
	return visibleWindows_[static_cast<std::size_t>(window)];
}

void DebugOverlayState::ToggleWindow(DebugOverlayWindow window)
{
	const std::size_t index = static_cast<std::size_t>(window);
	visibleWindows_[index] = !visibleWindows_[index];
}

void DebugOverlayState::SetWindowVisible(DebugOverlayWindow window, bool visible)
{
	visibleWindows_[static_cast<std::size_t>(window)] = visible;
}

bool DebugOverlayState::TryOpenEditor(bool multiplayer, int pauseMode)
{
	if (multiplayer)
		return false;

	editorPreviousPauseMode_ = pauseMode;
	SetWindowVisible(DebugOverlayWindow::Editor, true);
	return true;
}

int DebugOverlayState::CloseEditor()
{
	const int previousPauseMode = editorPreviousPauseMode_.value_or(0);
	editorPreviousPauseMode_.reset();
	selectedEditorTile_.reset();
	SetWindowVisible(DebugOverlayWindow::Editor, false);
	return previousPauseMode;
}

bool DebugOverlayState::EditorOwnsPause() const
{
	return editorPreviousPauseMode_.has_value();
}

bool DebugOverlayState::ShouldSelectEditorTile(bool mouseCaptured, bool leftButton) const
{
	return IsWindowVisible(DebugOverlayWindow::Editor) && !mouseCaptured && leftButton;
}

void DebugOverlayState::SelectEditorTile(Point tile)
{
	selectedEditorTile_ = tile;
}

const std::optional<Point> &DebugOverlayState::GetSelectedEditorTile() const
{
	return selectedEditorTile_;
}

} // namespace devilution

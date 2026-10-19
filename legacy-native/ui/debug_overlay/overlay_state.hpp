#pragma once

/**
 * @file ui/debug_overlay/overlay_state.hpp
 *
 * Interface for debug overlay state.
 */


#include <cstddef>
#include <cstdint>
#include <optional>

#include "engine/math/point.hpp"
#include "game/levels/dun_tile.hpp"

namespace devilution {

enum class DebugOverlayWindow : std::size_t {
	Console,
	Inspector,
	Editor,
	Count,
};

enum class DebugOverlayTextInputAction {
	None,
	Start,
	Stop,
};

class DebugOverlayTextInputState {
public:
	DebugOverlayTextInputAction Update(bool consoleVisible);

private:
	bool ownsTextInput_ = false;
};

struct DebugOverlayDisplaySize {
	float width;
	float height;
};

DebugOverlayDisplaySize ResolveDebugOverlayDisplaySize(
    int windowWidth, int windowHeight, int logicalWidth, int logicalHeight);
DebugOverlayDisplaySize ResolveDebugPiecePreviewSize(
    int sourceWidth, int sourceHeight, float maximumWidth, float maximumHeight);
bool IsDebugPiecePreviewFoliage(bool isFloorPiece, std::size_t blockIndex, TileType tileType);

class DebugPieceSelectorState {
public:
	void Open(uint16_t currentPiece);
	bool IsOpen() const;
	void Select(uint16_t piece);
	uint16_t GetSelectedPiece() const;
	void Apply();
	void Cancel();
	std::optional<uint16_t> TakeAppliedPiece();

private:
	bool open_ = false;
	uint16_t selectedPiece_ = 0;
	std::optional<uint16_t> appliedPiece_;
};

class DebugOverlayState {
public:
	bool IsActive() const;
	void ToggleActive();
	void Close();

	bool IsWindowVisible(DebugOverlayWindow window) const;
	void ToggleWindow(DebugOverlayWindow window);
	void SetWindowVisible(DebugOverlayWindow window, bool visible);

	bool TryOpenEditor(bool multiplayer, int pauseMode);
	int CloseEditor();
	bool EditorOwnsPause() const;
	bool ShouldSelectEditorTile(bool mouseCaptured, bool leftButton) const;
	void SelectEditorTile(Point tile);
	const std::optional<Point> &GetSelectedEditorTile() const;

private:
	bool active_ = false;
	bool visibleWindows_[static_cast<std::size_t>(DebugOverlayWindow::Count)] {};
	std::optional<int> editorPreviousPauseMode_;
	std::optional<Point> selectedEditorTile_;
};

} // namespace devilution

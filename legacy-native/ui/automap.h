/**
 * @file automap.h
 *
 * Interface of the in-game map overlay.
 */
#pragma once

#include <array>
#include <cstdint>

#include "engine/gfx/surface.hpp"
#include "engine/math/displacement.hpp"
#include "engine/math/point.hpp"
#include "game/levels/dungeon_common.h"
#include "utils/attributes.h"

namespace devilution {

#ifdef BUILD_TESTING
// Packed type/flags preserve the complete lookup table, including cold tails.
DVL_API_FOR_TEST std::array<uint16_t, 256> CaptureAutomapDefinitionsForTesting();
DVL_API_FOR_TEST void RestoreAutomapDefinitionsForTesting(const std::array<uint16_t, 256> &definitions);
#endif

enum MapExplorationType : uint8_t {
	/** unexplored map tile */
	MAP_EXP_NONE,
	/** map tile explored in vanilla - compatibility reasons */
	MAP_EXP_OLD,
	/** map tile explored by a shrine */
	MAP_EXP_SHRINE,
	/** map tile explored by someone else in multiplayer */
	MAP_EXP_OTHERS,
	/** map tile explored by current player */
	MAP_EXP_SELF,
};

/** Defines the offsets used for Automap lines */
enum class AmWidthOffset : int8_t {
	None,
	EighthTileRight = TILE_WIDTH >> 4,
	QuarterTileRight = TILE_WIDTH >> 3,
	HalfTileRight = TILE_WIDTH >> 2,
	ThreeQuartersTileRight = (TILE_WIDTH >> 1) - (TILE_WIDTH >> 3),
	FullTileRight = TILE_WIDTH >> 1,
	DoubleTileRight = TILE_WIDTH,
	EighthTileLeft = -EighthTileRight,
	QuarterTileLeft = -QuarterTileRight,
	HalfTileLeft = -HalfTileRight,
	ThreeQuartersTileLeft = -ThreeQuartersTileRight,
	FullTileLeft = -FullTileRight,
	DoubleTileLeft = -DoubleTileRight,
};

enum class AmHeightOffset : int8_t {
	None,
	EighthTileDown = TILE_HEIGHT >> 4,
	QuarterTileDown = TILE_HEIGHT >> 3,
	HalfTileDown = TILE_HEIGHT >> 2,
	ThreeQuartersTileDown = (TILE_HEIGHT >> 1) - (TILE_HEIGHT >> 3),
	FullTileDown = TILE_HEIGHT >> 1,
	DoubleTileDown = TILE_HEIGHT,
	EighthTileUp = -EighthTileDown,
	QuarterTileUp = -QuarterTileDown,
	HalfTileUp = -HalfTileDown,
	ThreeQuartersTileUp = -ThreeQuartersTileDown,
	FullTileUp = -FullTileDown,
	DoubleTileUp = -DoubleTileDown,
};

enum class AmLineLength : uint8_t {
	QuarterTile = 2,
	HalfTile = 4,
	FullTile = 8,
	FullAndHalfTile = 12,
	DoubleTile = 16,
	OctupleTile = 64,
};

enum class AutomapType : uint8_t {
	Opaque,
	FIRST = Opaque,
	Transparent,
	Minimap,
	LAST = Minimap
};

class AutomapManager {
public:
	// Accessors
	[[nodiscard]] DVL_API_FOR_TEST bool GetAutomapActive() const { return automapActive_; }
	DVL_API_FOR_TEST void SetAutomapActive(bool value) { automapActive_ = value; }

	[[nodiscard]] uint8_t (*GetAutomapView())[DMAXY] { return automapView_; }

	[[nodiscard]] DVL_API_FOR_TEST int GetAutoMapScale() const { return autoMapScale_; }
	DVL_API_FOR_TEST void SetAutoMapScale(int value) { autoMapScale_ = value; }

	[[nodiscard]] DVL_API_FOR_TEST int GetMinimapScale() const { return minimapScale_; }
	void SetMinimapScale(int value) { minimapScale_ = value; }

	[[nodiscard]] DVL_API_FOR_TEST Displacement GetAutomapOffset() const { return automapOffset_; }
	DVL_API_FOR_TEST void SetAutomapOffset(Displacement value) { automapOffset_ = value; }

	[[nodiscard]] Rectangle GetMinimapRect() const { return minimapRect_; }
	void SetMinimapRect(Rectangle value) { minimapRect_ = value; }

	[[nodiscard]] DVL_API_FOR_TEST AutomapType CurrentAutomapType() const { return currentAutomapType_; }
	DVL_API_FOR_TEST void SetCurrentAutomapType(AutomapType type) { currentAutomapType_ = type; }

	[[nodiscard]] bool GetAutoMapShowItems() const { return autoMapShowItems_; }
	void SetAutoMapShowItems(bool value) { autoMapShowItems_ = value; }

	// Methods
	void InitAutomapOnce();
	void InitAutomap();
	void StartAutomap();
	void StartMinimap();
	void AutomapUp();
	void AutomapDown();
	void AutomapLeft();
	void AutomapRight();
	void AutomapZoomIn();
	void AutomapZoomOut();
	void DrawAutomap(const Surface &out);
	void UpdateAutomapExplorer(Point map, MapExplorationType explorer);
	void SetAutomapView(Point tile, MapExplorationType explorer);
	void AutomapZoomReset();

private:
	bool automapActive_ = false;
	AutomapType currentAutomapType_ = AutomapType::Opaque;
	uint8_t automapView_[DMAXX][DMAXY] = {};
	int autoMapScale_ = 50;
	int minimapScale_ = 25;
	Displacement automapOffset_ = {};
	Rectangle minimapRect_ = {};
	bool autoMapShowItems_ = false;
};

extern DVL_API_FOR_TEST AutomapManager CurrentAutomapManager;

/**
 * @brief Sets the map type. Does not change `CurrentAutomapManager.GetAutomapActive()`.
 */
inline void SetAutomapType(AutomapType type)
{
	CurrentAutomapManager.SetCurrentAutomapType(type);
}

inline AutomapType GetAutomapType()
{
	return CurrentAutomapManager.CurrentAutomapType();
}

inline Displacement AmOffset(AmWidthOffset x, AmHeightOffset y)
{
	int scale = (GetAutomapType() == AutomapType::Minimap) ? CurrentAutomapManager.GetMinimapScale() : CurrentAutomapManager.GetAutoMapScale();
	return { scale * static_cast<int>(x) / 100, scale * static_cast<int>(y) / 100 };
}

inline int AmLine(AmLineLength l)
{
	int scale = (GetAutomapType() == AutomapType::Minimap) ? CurrentAutomapManager.GetMinimapScale() : CurrentAutomapManager.GetAutoMapScale();
	return scale * static_cast<int>(l) / 100;
}

} // namespace devilution

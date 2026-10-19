/**
 * @file lighting.h
 *
 * Interface of light and vision.
 */
#pragma once

#include <array>
#include <cstdint>

#include <expected.hpp>

#include "ui/automap.h"
#include "engine/math/displacement.hpp"
#include "engine/lighting_defs.hpp"
#include "engine/math/point.hpp"
#include "engine/math/world_tile.hpp"
#include "utils/attributes.h"

namespace devilution {

struct LightPosition {
	WorldTilePosition tile;
	/** Pixel offset from tile. */
	DisplacementOf<int8_t> offset;
	/** Previous position. */
	WorldTilePosition old;
};

struct Light {
	LightPosition position;
	uint8_t radius;
	uint8_t oldRadius;
	bool isInvalid;
	bool hasChanged;
};

class LightManager {
public:
	void Init();
	tl::expected<void, std::string> LoadTrns();
	void MakeLightTable();

	int AddLight(Point position, uint8_t radius);
	void AddUnLight(int i);
	void ChangeLightRadius(int i, uint8_t radius);
	void ChangeLightXY(int i, Point position);
	void ChangeLightOffset(int i, DisplacementOf<int8_t> offset);
	void ChangeLight(int i, Point position, uint8_t radius);
	void ProcessLightList();

	void ActivateVision(Point position, int r, size_t id);
	void ChangeVisionRadius(size_t id, int r);
	void ChangeVisionXY(size_t id, Point position);
	void ProcessVisionList();
#ifdef BUILD_TESTING
	/** Read-only pending-vision observation for native load-order parity tests. */
	bool VisionUpdatePendingForTesting() const { return updateVision_; }
#endif

	void DoUnLight(Point position, uint8_t radius);
	void DoLighting(Point position, uint8_t radius, DisplacementOf<int8_t> offset);
	void DoUnVision(Point position, uint8_t radius);
	void DoVision(Point position, uint8_t radius, MapExplorationType doAutomap, bool visible);

	void SavePreLighting();
	void lighting_color_cycling();

#ifdef _DEBUG
	void ToggleLighting();
#endif

	Light visionList_[MAXVISION];
	std::array<bool, MAXVISION> visionActive_;
	Light lights_[MAXLIGHTS];
	std::array<uint8_t, MAXLIGHTS> activeLights_;
	int activeLightCount_ = 0;
	std::array<std::array<uint8_t, LightTableSize>, NumLightingLevels> lightTables_;
	/** @brief Contains a pointer to a light table that is fully lit (no color mapping is required). Can be null in hell. */
	uint8_t *fullyLitLightTable_ = nullptr;
	/** @brief Contains a pointer to a light table that is fully dark (every color result to 0/black). Can be null in hellfire levels. */
	uint8_t *fullyDarkLightTable_ = nullptr;
	std::array<uint8_t, 256> infravisionTable_;
	std::array<uint8_t, 256> stoneTable_;
	std::array<uint8_t, 256> pauseTable_;
#ifdef _DEBUG
	bool disableLighting_ = false;
#endif
	bool updateLighting_ = false;

	static constexpr size_t NumLightRadiuses = 16;

private:
	bool updateVision_ = false;
	uint8_t lightFalloffs_[NumLightRadiuses][128];
	uint8_t lightConeInterpolations_[8][8][16][16];

	void RotateRadius(DisplacementOf<int8_t> &offset, DisplacementOf<int8_t> &dist, DisplacementOf<int8_t> &light, DisplacementOf<int8_t> &block);
	DVL_ALWAYS_INLINE void SetLight(Point position, uint8_t v);
	DVL_ALWAYS_INLINE uint8_t GetLight(Point position);
	bool TileAllowsLight(Point position);
	void DoVisionFlags(Point position, MapExplorationType doAutomap, bool visible);
};

extern DVL_API_FOR_TEST LightManager CurrentLightManager;

constexpr int MaxCrawlRadius = 18;

} // namespace devilution

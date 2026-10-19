/**
 * @file engine/lighting.cpp
 *
 * Implementation of light and vision.
 */
#include "engine/lighting.h"

#include <algorithm>
#include <cstdint>
#include <cstring>
#include <numeric>
#include <string>

#include <expected.hpp>

#include "ui/automap.h"
#include "engine/math/displacement.hpp"
#include "engine/lighting_defs.hpp"
#include "engine/load/load_file.hpp"
#include "engine/math/point.hpp"
#include "engine/math/points_in_rectangle_range.hpp"
#include "engine/math/world_tile.hpp"
#include "game/levels/tile_properties.hpp"
#include "game/objects/objects.hpp"
#include "game/players/players.hpp"
#include "utils/attributes.h"
#include "utils/is_of.hpp"
#include "utils/status_macros.hpp"
#include "engine/vision.hpp"

namespace devilution {

LightManager CurrentLightManager;

void LightManager::RotateRadius(DisplacementOf<int8_t> &offset, DisplacementOf<int8_t> &dist, DisplacementOf<int8_t> &light, DisplacementOf<int8_t> &block)
{
	dist = { static_cast<int8_t>(7 - dist.deltaY), dist.deltaX };
	light = { static_cast<int8_t>(7 - light.deltaY), light.deltaX };
	offset = { static_cast<int8_t>(dist.deltaX - light.deltaX), static_cast<int8_t>(dist.deltaY - light.deltaY) };

	block.deltaX = 0;
	if (offset.deltaX < 0) {
		offset.deltaX += 8;
		block.deltaX = 1;
	}
	block.deltaY = 0;
	if (offset.deltaY < 0) {
		offset.deltaY += 8;
		block.deltaY = 1;
	}
}

DVL_ALWAYS_INLINE void LightManager::SetLight(Point position, uint8_t v)
{
	if (LoadingMapObjects)
		tileAt(position).setPreLight(v);
	else
		tileAt(position).setLight(v);
}

DVL_ALWAYS_INLINE uint8_t LightManager::GetLight(Point position)
{
	if (LoadingMapObjects)
		return tileAt(position).preLight();

	return tileAt(position).light();
}

bool LightManager::TileAllowsLight(Point position)
{
	if (!InDungeonBounds(position))
		return false;
	return !TileHasAny(position, TileProperties::BlockLight);
}

void LightManager::DoVisionFlags(Point position, MapExplorationType doAutomap, bool visible)
{
	Tile &tile = tileAt(position);

	if (doAutomap != MAP_EXP_NONE) {
		if (tile.flags() != DungeonFlag::None)
			CurrentAutomapManager.SetAutomapView(position, doAutomap);
		tile.addFlags(DungeonFlag::Explored);
	}
	if (visible)
		tile.addFlags(DungeonFlag::Lit);
	tile.addFlags(DungeonFlag::Visible);
}

void LightManager::DoUnLight(Point position, uint8_t radius)
{
	radius++;
	radius++;

	auto searchArea = PointsInRectangle(WorldTileRectangle { position, radius });

	for (const WorldTilePosition targetPosition : searchArea) {
		if (InDungeonBounds(targetPosition)) {
			Tile &tile = tileAt(targetPosition);
			tile.setLight(tile.preLight());
		}
	}
}

void LightManager::DoLighting(Point position, uint8_t radius, DisplacementOf<int8_t> offset)
{
	assert(radius >= 0 && radius <= NumLightRadiuses);
	assert(InDungeonBounds(position));

	DisplacementOf<int8_t> light = {};
	DisplacementOf<int8_t> block = {};

	if (offset.deltaX < 0) {
		offset.deltaX += 8;
		position -= { 1, 0 };
	}
	if (offset.deltaY < 0) {
		offset.deltaY += 8;
		position -= { 0, 1 };
	}

	DisplacementOf<int8_t> dist = offset;

	int minX = 15;
	if (position.x - 15 < 0) {
		minX = position.x + 1;
	}
	int maxX = 15;
	if (position.x + 15 > MAXDUNX) {
		maxX = MAXDUNX - position.x;
	}
	int minY = 15;
	if (position.y - 15 < 0) {
		minY = position.y + 1;
	}
	int maxY = 15;
	if (position.y + 15 > MAXDUNY) {
		maxY = MAXDUNY - position.y;
	}

	if (IsAnyOf(levelType(), DTYPE_NEST, DTYPE_CRYPT)) {
		if (GetLight(position) > lightFalloffs_[radius][0])
			SetLight(position, lightFalloffs_[radius][0]);
	} else {
		SetLight(position, 0);
	}

	for (int i = 0; i < 4; i++) {
		const int yBound = i > 0 && i < 3 ? maxY : minY;
		const int xBound = i < 2 ? maxX : minX;
		for (int y = 0; y < yBound; y++) {
			for (int x = 1; x < xBound; x++) {
				const int linearDistance = lightConeInterpolations_[offset.deltaX][offset.deltaY][x + block.deltaX][y + block.deltaY];
				if (linearDistance >= 128)
					continue;
				const Point temp = position + (Displacement { x, y }).Rotate(-i);
				const uint8_t v = lightFalloffs_[radius][linearDistance];
				if (!InDungeonBounds(temp))
					continue;
				if (v < GetLight(temp))
					SetLight(temp, v);
			}
		}
		RotateRadius(offset, dist, light, block);
	}
}

void LightManager::DoUnVision(Point position, uint8_t radius)
{
	radius++;
	radius++;

	auto searchArea = PointsInRectangle(WorldTileRectangle { position, radius });

	for (const WorldTilePosition targetPosition : searchArea) {
		if (InDungeonBounds(targetPosition))
			tileAt(targetPosition).removeFlags(DungeonFlag::Visible | DungeonFlag::Lit);
	}
}

void LightManager::DoVision(Point position, uint8_t radius, MapExplorationType doAutomap, bool visible)
{
	auto markVisibleFn = [doAutomap, visible, this](Point rayPoint) {
		DoVisionFlags(rayPoint, doAutomap, visible);
	};
	auto markTransparentFn = [](Point rayPoint) {
		const int8_t trans = tileAt(rayPoint).transVal();
		if (trans != 0)
			visibleTransparencyRegions()[trans] = true;
	};
	auto passesLightFn = [this](Point rayPoint) {
		return TileAllowsLight(rayPoint);
	};
	auto inBoundsFn = [](Point rayPoint) {
		return InDungeonBounds(rayPoint);
	};

	devilution::DoVision(position, radius, markVisibleFn, markTransparentFn, passesLightFn, inBoundsFn);
}

tl::expected<void, std::string> LightManager::LoadTrns()
{
	RETURN_IF_ERROR(LoadFileInMemWithStatus("plrgfx\\infra.trn", infravisionTable_));
	RETURN_IF_ERROR(LoadFileInMemWithStatus("plrgfx\\stone.trn", stoneTable_));
	return LoadFileInMemWithStatus("gendata\\pause.trn", pauseTable_);
}

void LightManager::MakeLightTable()
{
	uint8_t shade = 0;
	constexpr uint8_t Black = 0;
	constexpr uint8_t White = 255;
	for (auto &lightTable : lightTables_) {
		uint8_t colorIndex = 0;
		for (const uint8_t steps : { 16, 16, 16, 16, 16, 16, 16, 16, 8, 8, 8, 8, 16, 16, 16, 16, 16, 16 }) {
			const uint8_t shading = shade * steps / 16;
			const uint8_t shadeStart = colorIndex;
			const uint8_t shadeEnd = shadeStart + steps - 1;
			for (uint8_t step = 0; step < steps; step++) {
				if (colorIndex == Black) {
					lightTable[colorIndex++] = Black;
					continue;
				}
				int color = shadeStart + step + shading;
				if (color > shadeEnd || color == White)
					color = Black;
				lightTable[colorIndex++] = color;
			}
		}
		shade++;
	}

	lightTables_[15] = {};
	fullyLitLightTable_ = lightTables_[0].data();
	fullyDarkLightTable_ = lightTables_[LightsMax].data();

	if (levelType() == DTYPE_HELL) {
		const auto shades = static_cast<int>(lightTables_.size() - 1);
		for (int i = 0; i < shades; i++) {
			auto &lightTable = lightTables_[i];
			constexpr int Range = 16;
			for (int j = 0; j < Range; j++) {
				uint8_t color = ((Range - 1) << 4) / shades * (shades - i) / Range * (j + 1);
				color = 1 + (color >> 4);
				int idx = j + 1;
				lightTable[idx] = color;
				idx = 31 - j;
				lightTable[idx] = color;
			}
		}
		fullyLitLightTable_ = nullptr;
	} else if (IsAnyOf(levelType(), DTYPE_NEST, DTYPE_CRYPT)) {
		for (auto &lightTable : lightTables_)
			std::iota(lightTable.begin(), lightTable.begin() + 16, uint8_t { 0 });
		lightTables_[15][0] = 0;
		std::fill_n(lightTables_[15].begin() + 1, 15, 1);
		fullyDarkLightTable_ = nullptr;
	}

	assert((fullyLitLightTable_ != nullptr) == (lightTables_[0][0] == 0 && std::adjacent_find(lightTables_[0].begin(), lightTables_[0].end() - 1, [](auto x, auto y) { return (x + 1) != y; }) == lightTables_[0].end() - 1));
	assert((fullyDarkLightTable_ != nullptr) == (std::all_of(lightTables_[LightsMax].begin(), lightTables_[LightsMax].end(), [](auto x) { return x == 0; })));

	const float maxDarkness = 15;
	const float maxBrightness = 0;
	for (unsigned radius = 0; radius < NumLightRadiuses; radius++) {
		const unsigned maxDistance = (radius + 1) * 8;
		for (unsigned distance = 0; distance < 128; distance++) {
			if (distance > maxDistance) {
				lightFalloffs_[radius][distance] = 15;
			} else {
				const float factor = static_cast<float>(distance) / static_cast<float>(maxDistance);
				float scaled;
				if (IsAnyOf(levelType(), DTYPE_NEST, DTYPE_CRYPT)) {
					const float brightness = static_cast<float>(radius) * 1.25F;
					scaled = factor * factor * brightness + (maxDarkness - brightness);
					scaled = std::max(maxBrightness, scaled);
				} else {
					scaled = factor * maxDarkness;
				}
				scaled += 0.5F;
				lightFalloffs_[radius][distance] = static_cast<uint8_t>(scaled);
			}
		}
	}

	for (int offsetY = 0; offsetY < 8; offsetY++) {
		for (int offsetX = 0; offsetX < 8; offsetX++) {
			for (int y = 0; y < 16; y++) {
				for (int x = 0; x < 16; x++) {
					const int a = ((8 * x) - offsetX);
					const int b = ((8 * y) - offsetY);
					lightConeInterpolations_[offsetX][offsetY][x][y] = static_cast<uint8_t>(sqrt((a * a) + (b * b)));
				}
			}
		}
	}
}

#ifdef _DEBUG
void LightManager::ToggleLighting()
{
	disableLighting_ = !disableLighting_;

	if (disableLighting_) {
		for (Tile &tile : tiles().columnMajor())
			tile.setLight(0);
		return;
	}

	for (Tile &tile : tiles().columnMajor())
		tile.setLight(tile.preLight());

	for (const Player &player : Players) {
		if (player.plractive && player.isOnActiveLevel()) {
			DoLighting(player.position.tile, player._pLightRad, {});
		}
	}
}
#endif

void LightManager::Init()
{
	activeLightCount_ = 0;
	updateLighting_ = false;
	updateVision_ = false;
#ifdef _DEBUG
	disableLighting_ = false;
#endif

	std::iota(activeLights_.begin(), activeLights_.end(), uint8_t { 0 });
	visionActive_ = {};
	visibleTransparencyRegions() = {};
}

int LightManager::AddLight(Point position, uint8_t radius)
{
#ifdef _DEBUG
	if (disableLighting_)
		return NO_LIGHT;
#endif
	if (activeLightCount_ >= MAXLIGHTS)
		return NO_LIGHT;

	const int lid = activeLights_[activeLightCount_++];
	Light &light = lights_[lid];
	light.position.tile = position;
	light.radius = radius;
	light.position.offset = { 0, 0 };
	light.isInvalid = false;
	light.hasChanged = false;

	updateLighting_ = true;

	return lid;
}

void LightManager::AddUnLight(int i)
{
#ifdef _DEBUG
	if (disableLighting_)
		return;
#endif
	if (i == NO_LIGHT)
		return;

	lights_[i].isInvalid = true;

	updateLighting_ = true;
}

void LightManager::ChangeLightRadius(int i, uint8_t radius)
{
#ifdef _DEBUG
	if (disableLighting_)
		return;
#endif
	if (i == NO_LIGHT)
		return;

	Light &light = lights_[i];
	light.hasChanged = true;
	light.position.old = light.position.tile;
	light.oldRadius = light.radius;
	light.radius = radius;

	updateLighting_ = true;
}

void LightManager::ChangeLightXY(int i, Point position)
{
#ifdef _DEBUG
	if (disableLighting_)
		return;
#endif
	if (i == NO_LIGHT)
		return;

	Light &light = lights_[i];
	light.hasChanged = true;
	light.position.old = light.position.tile;
	light.oldRadius = light.radius;
	light.position.tile = position;

	updateLighting_ = true;
}

void LightManager::ChangeLightOffset(int i, DisplacementOf<int8_t> offset)
{
#ifdef _DEBUG
	if (disableLighting_)
		return;
#endif
	if (i == NO_LIGHT)
		return;

	Light &light = lights_[i];
	if (light.position.offset == offset)
		return;

	light.hasChanged = true;
	light.position.old = light.position.tile;
	light.oldRadius = light.radius;
	light.position.offset = offset;

	updateLighting_ = true;
}

void LightManager::ChangeLight(int i, Point position, uint8_t radius)
{
#ifdef _DEBUG
	if (disableLighting_)
		return;
#endif
	if (i == NO_LIGHT)
		return;

	Light &light = lights_[i];
	light.hasChanged = true;
	light.position.old = light.position.tile;
	light.oldRadius = light.radius;
	light.position.tile = position;
	light.radius = radius;

	updateLighting_ = true;
}

void LightManager::ProcessLightList()
{
#ifdef _DEBUG
	if (disableLighting_)
		return;
#endif
	if (!updateLighting_)
		return;
	for (int i = 0; i < activeLightCount_; i++) {
		Light &light = lights_[activeLights_[i]];
		if (light.isInvalid) {
			DoUnLight(light.position.tile, light.radius);
		}
		if (light.hasChanged) {
			DoUnLight(light.position.old, light.oldRadius);
			light.hasChanged = false;
		}
	}
	for (int i = 0; i < activeLightCount_; i++) {
		const Light &light = lights_[activeLights_[i]];
		if (light.isInvalid) {
			activeLightCount_--;
			std::swap(activeLights_[activeLightCount_], activeLights_[i]);
			i--;
			continue;
		}
		if (TileHasAny(light.position.tile, TileProperties::Solid))
			continue;
		DoLighting(light.position.tile, light.radius, light.position.offset);
	}

	updateLighting_ = false;
}

void LightManager::SavePreLighting()
{
	for (Tile &tile : tiles().columnMajor())
		tile.setPreLight(tile.light());
}

void LightManager::ActivateVision(Point position, int r, size_t id)
{
	auto &vision = visionList_[id];
	vision.position.tile = position;
	vision.radius = r;
	vision.isInvalid = false;
	vision.hasChanged = false;
	visionActive_[id] = true;

	updateVision_ = true;
}

void LightManager::ChangeVisionRadius(size_t id, int r)
{
	auto &vision = visionList_[id];
	vision.hasChanged = true;
	vision.position.old = vision.position.tile;
	vision.oldRadius = vision.radius;
	vision.radius = r;
	updateVision_ = true;
}

void LightManager::ChangeVisionXY(size_t id, Point position)
{
	auto &vision = visionList_[id];
	vision.hasChanged = true;
	vision.position.old = vision.position.tile;
	vision.oldRadius = vision.radius;
	vision.position.tile = position;
	updateVision_ = true;
}

void LightManager::ProcessVisionList()
{
	if (!updateVision_)
		return;

	visibleTransparencyRegions() = {};

	for (const Player &player : Players) {
		const size_t id = player.getId();
		if (!visionActive_[id])
			continue;
		Light &vision = visionList_[id];
		if (!player.plractive || !player.isOnActiveLevel() || (player._pLvlChanging && &player != MyPlayer)) {
			DoUnVision(vision.position.tile, vision.radius);
			visionActive_[id] = false;
			continue;
		}
		if (vision.hasChanged) {
			DoUnVision(vision.position.old, vision.oldRadius);
			vision.hasChanged = false;
		}
	}
	for (const Player &player : Players) {
		const size_t id = player.getId();
		if (!visionActive_[id])
			continue;
		const Light &vision = visionList_[id];
		MapExplorationType doautomap = MAP_EXP_SELF;
		if (&player != MyPlayer)
			doautomap = player.friendlyMode ? MAP_EXP_OTHERS : MAP_EXP_NONE;
		DoVision(
		    vision.position.tile,
		    vision.radius,
		    doautomap,
		    &player == MyPlayer);
	}

	updateVision_ = false;
}

void LightManager::lighting_color_cycling()
{
	for (auto &lightTable : lightTables_) {
		std::rotate(lightTable.begin() + 1, lightTable.begin() + 2, lightTable.begin() + 32);
	}
}

} // namespace devilution

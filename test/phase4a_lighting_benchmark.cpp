#include <benchmark/benchmark.h>
#include <cstring>
#include <random>

#include "engine/lighting.h"
#include "engine/lighting_defs.hpp"
#include "engine/math/point.hpp"
#include "engine/math/world_tile.hpp"
#include "game/levels/dungeon_common.h"
#include "game/levels/level.hpp"
#include "game/levels/tile.hpp"

namespace devilution {
namespace {

// Benchmark: DoUnLight operation (represents light restoration operations)
static void BM_DoUnLightOperation(benchmark::State &state)
{
	// Level is initialized via CurrentWorld
	const Point testPos { 50, 50 };

	for (auto _ : state) {
		// Set up light
		tileAt(testPos).setPreLight(5);
		tileAt(testPos).setLight(10);

		// Restore light (DoUnLight pattern)
		CurrentLightManager.DoUnLight(testPos, 1);
		benchmark::ClobberMemory();
	}
}

// Benchmark: DoLighting operation (represents light application operations)
static void BM_DoLightingOperation(benchmark::State &state)
{
	// Level is initialized via CurrentWorld
	const Point testPos { 50, 50 };

	// Initialize region
	for (int x = 40; x < 60; x++) {
		for (int y = 40; y < 60; y++) {
			Point pos { static_cast<WorldTileCoord>(x), static_cast<WorldTileCoord>(y) };
			tileAt(pos).setPreLight(0);
			tileAt(pos).setLight(0);
		}
	}

	for (auto _ : state) {
		// Apply lighting cone
		CurrentLightManager.DoLighting(testPos, 5, DisplacementOf<int8_t> { 0, 0 });
		benchmark::ClobberMemory();
	}
}

// Benchmark: Multiple DoUnLight
static void BM_MultiTileDoUnLight(benchmark::State &state)
{
	// Level is initialized via CurrentWorld
	// Initialize a region with light values
	for (int x = 40; x < 60; x++) {
		for (int y = 40; y < 60; y++) {
			Point pos { static_cast<WorldTileCoord>(x), static_cast<WorldTileCoord>(y) };
			tileAt(pos).setPreLight(5);
			tileAt(pos).setLight(15);
		}
	}

	for (auto _ : state) {
		// Simulate lighting removal around several points
		for (int cx = 45; cx < 55; cx += 3) {
			for (int cy = 45; cy < 55; cy += 3) {
				Point center { static_cast<WorldTileCoord>(cx), static_cast<WorldTileCoord>(cy) };
				CurrentLightManager.DoUnLight(center, 2);
			}
		}
		benchmark::ClobberMemory();
	}

	state.SetItemsProcessed(state.iterations() * 9); // 3x3 calls per iteration
}

// Benchmark: Tile flag operations (flag manipulation during lighting)
static void BM_TileFlagOperations(benchmark::State &state)
{
	// Level is initialized via CurrentWorld
	const Point testPos { 50, 50 };
	Tile &tile = tileAt(testPos);

	for (auto _ : state) {
		tile.addFlags(DungeonFlag::Explored);
		tile.addFlags(DungeonFlag::Lit);
		tile.addFlags(DungeonFlag::Visible);
		tile.removeFlags(DungeonFlag::Visible);
		benchmark::ClobberMemory();
	}
}

// Benchmark: Sequential tile light access
static void BM_SequentialTileAccess(benchmark::State &state)
{
	// Level is initialized via CurrentWorld
	for (auto _ : state) {
		// Sequential access pattern (good cache locality)
		for (int x = 0; x < 50; x++) {
			for (int y = 0; y < 50; y++) {
				Point pos { static_cast<WorldTileCoord>(x), static_cast<WorldTileCoord>(y) };
				Tile &tile = tileAt(pos);
				tile.setLight(static_cast<uint8_t>(x + y));
				tile.setPreLight(static_cast<uint8_t>(x + y) / 2);
			}
		}
		benchmark::ClobberMemory();
	}

	state.SetItemsProcessed(state.iterations() * 50 * 50);
}

// Benchmark: Random tile access
static void BM_RandomTileAccess(benchmark::State &state)
{
	// Level is initialized via CurrentWorld
	// Pre-generate random positions
	std::vector<Point> randomPositions;
	std::mt19937 rng(42); // Fixed seed for reproducibility
	std::uniform_int_distribution<int> dist(0, 49);

	for (int i = 0; i < 2500; i++) {
		randomPositions.push_back(Point {
		    static_cast<WorldTileCoord>(dist(rng)),
		    static_cast<WorldTileCoord>(dist(rng)) });
	}

	for (auto _ : state) {
		for (const Point &pos : randomPositions) {
			Tile &tile = tileAt(pos);
			tile.setLight(42);
		}
		benchmark::ClobberMemory();
	}

	state.SetItemsProcessed(state.iterations() * 2500);
}

// Benchmark: Simulated lighting area update
static void BM_LightingAreaUpdate(benchmark::State &state)
{
	// Level is initialized via CurrentWorld
	const Point centerPos { 100, 100 };
	const uint8_t radius = 5;

	// Initialize area
	for (int x = 90; x < 110; x++) {
		for (int y = 90; y < 110; y++) {
			Point pos { static_cast<WorldTileCoord>(x), static_cast<WorldTileCoord>(y) };
			if (InDungeonBounds(pos)) {
				tileAt(pos).setPreLight(0);
				tileAt(pos).setLight(0);
			}
		}
	}

	for (auto _ : state) {
		// Apply lighting in area
		CurrentLightManager.DoLighting(centerPos, radius, DisplacementOf<int8_t> { 0, 0 });
		benchmark::ClobberMemory();
	}

	state.SetItemsProcessed(state.iterations() * 20 * 20);
}

// Register benchmarks
BENCHMARK(BM_DoUnLightOperation)->Iterations(10000);
BENCHMARK(BM_DoLightingOperation)->Iterations(1000);
BENCHMARK(BM_MultiTileDoUnLight)->Iterations(1000);
BENCHMARK(BM_TileFlagOperations)->Iterations(100000);
BENCHMARK(BM_SequentialTileAccess)->Iterations(100);
BENCHMARK(BM_RandomTileAccess)->Iterations(100);
BENCHMARK(BM_LightingAreaUpdate)->Iterations(100);

} // namespace
} // namespace devilution

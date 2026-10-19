/**
 * @file palette.h
 *
 * Interface of functions for handling the engines color palette.
 */
#pragma once

#include <array>
#include <cstdint>
#include <span>
#include <string_view>

#ifdef USE_SDL3
#include <SDL3/SDL_pixels.h>
#else
#include <SDL.h>
#endif

#include "game/levels/dungeon_common_defs.hpp"

namespace devilution {

// Diablo uses a 256 color palette
// Entry 0-127 (0x00-0x7F) are level specific
// Entry 128-255 (0x80-0xFF) are global

// standard palette for all levels
// 8 or 16 shades per color
// example (dark blue): PAL16_BLUE+14, PAL8_BLUE+7
// example (light red): PAL16_RED+2, PAL8_RED
// example (orange): PAL16_ORANGE+8, PAL8_ORANGE+4
#define PAL8_BLUE 128
#define PAL8_RED 136
#define PAL8_YELLOW 144
#define PAL8_ORANGE 152
#define PAL16_BEIGE 160
#define PAL16_BLUE 176
#define PAL16_YELLOW 192
#define PAL16_ORANGE 208
#define PAL16_RED 224
#define PAL16_GRAY 240

// An RGB color without an alpha component.
struct Color {
	uint8_t rgb[3];

	[[nodiscard]] SDL_Color toSDL() const
	{
		SDL_Color sdlColor;
		sdlColor.r = rgb[0];
		sdlColor.g = rgb[1];
		sdlColor.b = rgb[2];
#ifndef USE_SDL1
		sdlColor.a = SDL_ALPHA_OPAQUE;
#endif
		return sdlColor;
	}
};

// The palette before global brightness / fade effects.
// However, color cycling / swapping is applied to this palette.
extern std::array<SDL_Color, 256> logical_palette;

// This palette is the actual palette used for rendering.
// It is usually `logical_palette` with the global brightness setting
// and fade-in/out applied.
extern std::array<SDL_Color, 256> system_palette;

void palette_init();

// Loads `logical_palette` from path.
void LoadPalette(std::string_view path);

// Loads `logical_palette` from path, and generates the blending lookup table
void LoadPaletteAndInitBlending(std::string_view path);

// Sets a single `logical_palette` color and updates the corresponding `system_color`.
void SetLogicalPaletteColor(unsigned colorIndex, const SDL_Color &color);

void LoadRndLvlPal(dungeon_type l);
void IncreaseBrightness();

// Updates the system palette by copying from `src` and applying the global brightness setting.
// `src` which is usually `logical_palette`.
void UpdateSystemPalette(std::span<const SDL_Color, 256> src);

// Fade screen from black
// fr: Steps per 50ms
void PaletteFadeIn(int fr, const std::array<SDL_Color, 256> &srcPalette = logical_palette);

// Fade screen to black
// fr: Steps per 50ms
void PaletteFadeOut(int fr, const std::array<SDL_Color, 256> &srcPalette = logical_palette);

// Applies global brightness setting to `src` and writes the result to `dst`.
void ApplyGlobalBrightness(SDL_Color *dst, const SDL_Color *src);

// Applies a fade-to-black effect to `src` and writes the result to `dst`.
// fadeval: 0 - completely black, 256 - no effect.
void ApplyFadeLevel(unsigned fadeval, SDL_Color *dst, const SDL_Color *src);

// Call this when `system_palette` is updated directly.
// You do not need to call this when updating the system palette via `UpdateSystemPalette`, `PaletteFadeIn/Out`, or `BlackPalette`.
void SystemPaletteUpdated(int first = 0, int ncolor = 256);

void DecreaseBrightness();
int UpdateBrightness(int sliderValue);

// Sets `system_palette` to all-black and calls `SystemPaletteUpdated`.
void BlackPalette();

void palette_update_caves();
void palette_update_crypt();
void palette_update_hive();

} // namespace devilution

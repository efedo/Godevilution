/**
 * @file engine/render/world_renderer.h
 *
 * Interface of functionality for rendering the dungeons, monsters and calling other render routines.
 */
#pragma once

#include "engine/animationinfo.h"
#include "engine/math/direction.hpp"
#include "engine/math/displacement.hpp"
#include "engine/math/point.hpp"
#include "engine/gfx/surface.hpp"

namespace devilution {

struct Player;

extern bool frameflag;

/**
 * @brief Returns the offset for the walking animation
 * @param animationInfo the current active walking animation
 * @param dir walking direction
 * @param cameraMode Adjusts the offset relative to the camera
 */
Displacement GetOffsetForWalking(const AnimationInfo &animationInfo, Direction dir, bool cameraMode = false);

/**
 * @brief Clear cursor state
 */
void ClearCursor();

/**
 * @brief Shifting the view area along the logical grid
 *        Note: this won't allow you to shift between even and odd rows
 * @param horizontal Shift the screen left or right
 * @param vertical Shift the screen up or down
 */
void ShiftGrid(Point *offset, int horizontal, int vertical);

/**
 * @brief Gets the number of rows covered by the main panel
 */
int RowsCoveredByPanel();

/**
 * @brief Calculate the offset needed for centering tiles in view area
 * @param offsetX Offset in pixels
 * @param offsetY Offset in pixels
 */
void CalcTileOffset(int *offsetX, int *offsetY);

/**
 * @brief Calculate the needed diamond tile to cover the view area
 * @param columns Tiles needed per row
 * @param rows Both even and odd rows
 */
void TilesInView(int *columns, int *rows);
void CalcViewportGeometry();

/**
 * @brief Calculate the screen position of a given tile
 * @param tile Position of a dungeon tile
 */
Point GetScreenPosition(Point tile);

/**
 * @brief Render the whole screen black
 */
void ClearScreenBuffer();
#ifdef _DEBUG

/**
 * @brief Scroll the screen when mouse is close to the edge
 */
void ScrollView();
#endif

/**
 * @brief Initialize the FPS meter
 */
void EnableFrameCount();

/**
 * @brief Redraw screen
 */
void scrollrt_draw_game_screen();

/**
 * @brief Render the game
 */
void DrawAndBlit();

/** Renders one player sprite using authoritative or legacy presentation state. */
void DrawPlayer(const Surface &out, const Player &player, Point tilePosition, Point targetBufferPosition, int lightTableIndex);

} // namespace devilution

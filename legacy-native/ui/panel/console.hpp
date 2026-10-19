/**
 * @file ui/panel/console.hpp
 *
 * Interface for debug console.
 */


#ifdef _DEBUG
#pragma once

#include <cstdint>
#include <string>
#include <string_view>
#include <vector>

#ifdef USE_SDL3
#include <SDL3/SDL_events.h>
#else
#include <SDL.h>
#endif

#include "engine/gfx/surface.hpp"

namespace devilution {

enum class ConsoleLineType : uint8_t {
	Help,
	Input,
	Output,
	Warning,
	Error
};

struct ConsoleLine {
	ConsoleLineType type;
	std::string text;
	std::string wrapped = {};
	int numLines = 0;
};

void InitConsole();
bool IsConsoleOpen();
void OpenConsole();
void CloseConsole();
bool IsNativeOnlyConsole();
bool ConsoleHandleEvent(const SDL_Event &event);
void DrawConsole(const Surface &out);
void RunInConsole(std::string_view code);
void PrintToConsole(std::string_view text);
void PrintWarningToConsole(std::string_view text);
const std::vector<ConsoleLine> &GetConsoleLines();

} // namespace devilution
#endif // _DEBUG

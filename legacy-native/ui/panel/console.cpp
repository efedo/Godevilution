/**
 * @file ui/panel/console.cpp
 *
 * Implementation of debug console.
 */


#ifdef _DEBUG
#include "ui/panel/console.hpp"

#include <algorithm>
#include <cstdint>
#include <string_view>

#include <function_ref.hpp>

#ifdef USE_SDL3
#include <SDL3/SDL_events.h>
#include <SDL3/SDL_keyboard.h>
#include <SDL3/SDL_keycode.h>
#include <SDL3/SDL_rect.h>
#include <SDL3/SDL_scancode.h>
#else
#include <SDL.h>

#ifdef USE_SDL1
#include "utils/sdl/sdl2_to_1_2_backports.h"
#endif
#endif

#include "ui/menu/text_input.hpp"
#include "ui/panel/control.hpp"
#include "ui/debug_overlay/imgui_overlay.hpp"
#include "engine/assets.hpp"
#include "application/debug_commands.hpp"
#include "application/debug.h"
#include "engine/math/displacement.hpp"
#include "engine/gfx/graphics_pipeline.h"
#include "engine/gfx/palette.h"
#include "engine/math/rectangle.hpp"
#include "engine/render/primitive_render.hpp"
#include "engine/render/text_render.hpp"
#include "engine/math/size.hpp"
#include "engine/gfx/surface.hpp"
#include "lua/autocomplete.hpp"
#include "lua/metadoc.hpp"
#include "lua/repl.hpp"
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
#include "network/authoritative/server_backed_configuration.hpp"
#endif
#include "utils/algorithm/container.hpp"
#include "utils/display.h"
#include "utils/sdl/sdl_compat.h"
#include "utils/sdl/sdl_geometry.h"
#include "utils/string/str_case.hpp"
#include "utils/string/str_cat.hpp"
#include "utils/string/str_split.hpp"

namespace devilution {

namespace {

constexpr std::string_view Prompt = "> ";
constexpr std::string_view HelpText =
    // Displayed as the first console message
    "Debug console\n"
    "Shift+Enter to insert a newline, PageUp/Down to scroll,"
    " Up/Down to fill the input from history,"
    " Shift+Up/Down to fill the input from output history,"
    " Ctrl+L to clear history, Esc to close.";

constexpr std::string_view CommandHelpText =
    "Debug console commands:\n"
    "  help\n"
    "    Show this command list.\n"
    "  help <module>\n"
    "    Show documented members for a Lua module (example: help dev.player).\n"
    "\n"
    "Common debug Lua commands:\n"
    "  dev.player.god()\n"
    "  dev.player.invisible()\n"
    "  dev.display.grid()\n"
    "  dev.display.vision()\n"
    "  dev.level.seed()\n"
    "\n"
    "Other useful modules:\n"
    "  dev.items, dev.level, dev.monsters, dev.quests, dev.search, dev.towners\n"
    "\n"
    "\n"
    "Native debug commands:\n"
    "  debug.player.arrow <normal|fire|lightning|spectral>\n"
    "  debug.player.god [on|off]\n"
    "  debug.player.invisible [on|off]\n"
    "  debug.player.info [id]\n"
    "  debug.player.rejuvenate\n"
    "  debug.player.health <amount>\n"
    "  debug.player.mana <amount>\n"
    "  debug.player.attributes.max\n"
    "  debug.player.attributes.min\n"
    "  debug.player.levelUp [amount]\n"
    "  debug.player.gold.give [amount]\n"
    "  debug.player.gold.take [amount]\n"
    "  debug.player.spells.setLevel <level>\n"
    "  debug.quests.activate <id>\n"
    "  debug.quests.activateAll\n"
    "  debug.quests.info <id>\n"
    "  debug.quests.all\n"
    "  debug.player.trn <asset-path|clear>\n"
    "  debug.display.grid [on|off]\n"
    "  debug.display.vision [on|off]\n"
    "  debug.display.path [on|off]\n"
    "  debug.display.fps [on|off]\n"
    "  debug.display.fullbright\n"
    "  debug.display.scrollView [on|off]\n"
    "  debug.display.tileData [name]\n"
    "  debug.level.map.reveal\n"
    "  debug.level.map.hide\n"
    "  debug.level.reset <level> [seed]\n"
    "  debug.level.warp.dungeon <level>\n"
    "  debug.level.warp.quest <level>\n"
    "  debug.level.warp.map <path> <dunType> <x> <y>\n"
    "  debug.items.info\n"
    "  debug.items.spawn <name>\n"
    "  debug.items.spawnUnique <name>\n"
    "  debug.monsters.spawn <name> [count]\n"
    "  debug.monsters.spawnUnique <name> [count]\n"
    "  debug.level.exportDun\n"
    "  debug.towners.talk <name>\n"
    "  debug.towners.visit <name>\n"
    "  debug.level.seed [level]\n"
    "  debug.search.clear\n"
    "  debug.search.item <name>\n"
    "  debug.search.monster <name>\n"
    "  debug.search.object <name>\n"
    "\n"
    "Tip: Tab autocompletes Lua symbols (for example: dev.player.<Tab>).";

std::optional<tl::expected<AssetData, std::string>> ConsolePrelude;

bool IsConsoleVisible;
char ConsoleInputBuffer[4096];
TextInputCursorState ConsoleInputCursor;
TextInputState ConsoleInputState {
	TextInputState::Options {
	    .value = ConsoleInputBuffer,
	    .cursor = &ConsoleInputCursor,
	    .maxLength = sizeof(ConsoleInputBuffer) - 1,
	}
};

enum class InputTextState {
	UpToDate,
	Edited,
	RestoredFromHistory
};

InputTextState CurrentInputTextState = InputTextState::UpToDate;
std::string WrappedInputText { Prompt };
std::vector<LuaAutocompleteSuggestion> AutocompleteSuggestions;
int AutocompleteSuggestionsMaxWidth = -1;
int AutocompleteSuggestionFocusIndex = -1;
constexpr size_t MaxSuggestions = 12;

std::vector<ConsoleLine> ConsoleLines;
size_t NumPreparedConsoleLines;
int ConsoleLinesTotalHeight;

// Index of the currently filled input/output, counting from end.
int HistoryIndex = -1;

// Draft input, saved when navigating history.
std::string DraftInput;

Rectangle OuterRect;
Rectangle InputRect;
int InputRectHeight;
constexpr int LineHeight = 20;
constexpr int TextPaddingYTop = 0;
constexpr int TextPaddingYBottom = 4;
constexpr int TextPaddingX = 4;
constexpr uint8_t BorderColor = PAL8_YELLOW;
bool FirstRender;

constexpr UiFlags TextUiFlags = UiFlags::FontSizeDialog;
constexpr UiFlags InputTextUiFlags = TextUiFlags | UiFlags::ColorDialogWhite;
constexpr UiFlags OutputTextUiFlags = TextUiFlags | UiFlags::ColorDialogWhite;
constexpr UiFlags WarningTextUiFlags = TextUiFlags | UiFlags::ColorDialogYellow;
constexpr UiFlags ErrorTextUiFlags = TextUiFlags | UiFlags::ColorDialogRed;
constexpr UiFlags AutocompleteSuggestionsTextUiFlags = TextUiFlags | UiFlags::ColorDialogWhite;
constexpr UiFlags AutocompleteSuggestionsFocusedTextUiFlags = TextUiFlags | UiFlags::ColorDialogYellow;

constexpr int TextSpacing = 0;
constexpr GameFontTables TextFontSize = GetFontSizeFromUiFlags(InputTextUiFlags);
constexpr GameFontTables AutocompleteSuggestionsTextFontSize = GetFontSizeFromUiFlags(AutocompleteSuggestionsTextUiFlags);

// Scroll offset from the bottom (in pages), to be applied on next render.
int PendingScrollPages;
// Scroll offset from the bottom in pixels.
int ScrollOffset;
constexpr int ScrollStep = LineHeight * 3;

int GetConsoleLinesInnerWidth()
{
	return OuterRect.size.width - (2 * TextPaddingX);
}

std::string_view TextWithoutPrompt(const ConsoleLine &line)
{
	std::string_view result = line.text;
	if (line.type == ConsoleLineType::Input) {
		result.remove_prefix(Prompt.size());
	}
	return result;
}

void PrepareForRender(ConsoleLine &consoleLine)
{
	consoleLine.wrapped = WordWrapString(consoleLine.text, GetConsoleLinesInnerWidth(), TextFontSize, TextSpacing);
	consoleLine.numLines += static_cast<int>(c_count(consoleLine.wrapped, '\n')) + 1;
	ConsoleLinesTotalHeight += consoleLine.numLines * LineHeight;
}

void AddConsoleLine(ConsoleLine &&consoleLine)
{
	ConsoleLines.emplace_back(std::move(consoleLine));
}

void SendInput()
{
	RunInConsole(ConsoleInputState.value());
	ConsoleInputState.clear();
	DraftInput.clear();
	HistoryIndex = -1;
}

void DrawAutocompleteSuggestions(const Surface &out, const std::vector<LuaAutocompleteSuggestion> &suggestions, Point position)
{
	const int maxInnerWidth = out.w() - (TextPaddingX * 2);
	if (AutocompleteSuggestionsMaxWidth == -1) {
		int maxWidth = 0;
		for (const LuaAutocompleteSuggestion &suggestion : suggestions) {
			maxWidth = std::max(maxWidth, GetLineWidth(suggestion.displayText, AutocompleteSuggestionsTextFontSize, TextSpacing));
		}
		AutocompleteSuggestionsMaxWidth = std::min(maxWidth, maxInnerWidth);
	}

	const int outerWidth = AutocompleteSuggestionsMaxWidth + (TextPaddingX * 2);

	if (position.x + outerWidth > out.w()) {
		position.x = out.w() - outerWidth;
	}
	const int height = (static_cast<int>(suggestions.size()) * LineHeight) + TextPaddingYBottom + TextPaddingYTop;

	position.y -= height;
	position.y = std::max(LineHeight, position.y);

	FillRect(out, position.x, position.y, outerWidth, height, PAL16_BLUE + 14);
	size_t i = 0;

	Point textPosition { position.x + TextPaddingX, position.y + TextPaddingYTop };
	for (const LuaAutocompleteSuggestion &suggestion : suggestions) {
		if (static_cast<int>(i) == AutocompleteSuggestionFocusIndex) {
			const int extraTop = i == 0 ? TextPaddingYTop : 0;
			const int extraHeight = extraTop + TextPaddingYBottom;
			FillRect(out, position.x, textPosition.y - extraTop, outerWidth, LineHeight + extraHeight, PAL16_BLUE + 8);
		}
		const int textHeight = LineHeight + TextPaddingYBottom;
		DrawString(
		    out.subregion(textPosition.x, textPosition.y, maxInnerWidth, textHeight), suggestion.displayText,
		    Rectangle { Point { 0, 0 }, Size { maxInnerWidth, textHeight } },
		    TextRenderOptions {
		        .flags = AutocompleteSuggestionsTextUiFlags,
		        .spacing = TextSpacing,
		    });
		textPosition.y += LineHeight;
		++i;
	}
}

bool IsBreakStart(std::string_view str, size_t &breakLen)
{
	const char32_t cp = DecodeFirstUtf8CodePoint(str, &breakLen);
	return cp == U'\n' || IsBreakableWhitespace(cp);
}

void DrawInputText(const Surface &out,
    Rectangle rect, std::string_view originalInputText, std::string_view wrappedInputText)
{
	int lineY = 0;
	int numRendered = -static_cast<int>(Prompt.size());
	bool prevIsOriginalWhitespace = false;

	const Surface inputTextSurface = out.subregion(rect.position.x, rect.position.y, rect.size.width, rect.size.height);
	std::optional<Point> renderedCursorPositionOut;
	for (const std::string_view line : SplitByChar(wrappedInputText, '\n')) {
		const int lineCursorPosition = static_cast<int>(ConsoleInputCursor.position) - numRendered;
		const bool isCursorOnPrevLine = lineCursorPosition == 0 && !prevIsOriginalWhitespace && numRendered > 0;
		DrawString(
		    inputTextSurface, line, { 0, lineY },
		    TextRenderOptions {
		        .flags = InputTextUiFlags,
		        .spacing = TextSpacing,
		        .cursorPosition = isCursorOnPrevLine ? -1 : lineCursorPosition,
		        .highlightRange = { static_cast<int>(ConsoleInputCursor.selection.begin) - numRendered, static_cast<int>(ConsoleInputCursor.selection.end) - numRendered },
		        .renderedCursorPositionOut = &renderedCursorPositionOut });
		lineY += LineHeight;
		numRendered += static_cast<int>(line.size());

		size_t whitespaceLength;
		prevIsOriginalWhitespace = static_cast<size_t>(numRendered) < originalInputText.size()
		    && IsBreakStart(originalInputText.substr(static_cast<size_t>(numRendered)), whitespaceLength);
		if (prevIsOriginalWhitespace) {
			// If we replaced an original whitespace with a newline, count the original whitespace as rendered.
			numRendered += static_cast<int>(whitespaceLength);
		}
		if (numRendered < 0 && IsBreakStart(Prompt.substr(Prompt.size() - static_cast<size_t>(-numRendered)), whitespaceLength)) {
			// If we replaced the whitespace in a prompt with a newline, count it as rendered.
			numRendered += static_cast<int>(whitespaceLength);
		}
	}

	if (!AutocompleteSuggestions.empty() && renderedCursorPositionOut.has_value()) {
		Point position = *renderedCursorPositionOut;
		position.x += rect.position.x;
		position.y += rect.position.y;
		DrawAutocompleteSuggestions(out, AutocompleteSuggestions, position);
	}
}

void DrawConsoleLines(const Surface &out)
{
	const int innerHeight = out.h() - 4; // Extra space for letters like g.
	if (PendingScrollPages) {
		ScrollOffset += innerHeight * PendingScrollPages;
		PendingScrollPages = 0;
	}

	if (NumPreparedConsoleLines != ConsoleLines.size()) {
		for (size_t i = NumPreparedConsoleLines; i < ConsoleLines.size(); ++i) {
			PrepareForRender(ConsoleLines[i]);
		}
		NumPreparedConsoleLines = ConsoleLines.size();
		ScrollOffset = 0;
	}

	ScrollOffset = std::clamp(ScrollOffset, 0, std::max(0, ConsoleLinesTotalHeight - innerHeight));

	int lineYEnd = innerHeight + ScrollOffset;
	// NOLINTNEXTLINE(modernize-loop-convert)
	for (auto it = ConsoleLines.rbegin(), itEnd = ConsoleLines.rend(); it != itEnd; ++it) {
		ConsoleLine &consoleLine = *it;
		const int linesYBegin = lineYEnd - (LineHeight * consoleLine.numLines);
		if (linesYBegin > innerHeight) {
			lineYEnd = linesYBegin;
			continue;
		}
		size_t end = consoleLine.wrapped.size();
		while (true) {
			const size_t begin = consoleLine.wrapped.rfind('\n', end - 1) + 1;
			const std::string_view line = std::string_view(consoleLine.wrapped.data() + begin, end - begin);
			lineYEnd -= LineHeight;
			switch (consoleLine.type) {
			case ConsoleLineType::Input:
				DrawString(out, line, { 0, lineYEnd },
				    TextRenderOptions { .flags = InputTextUiFlags, .spacing = TextSpacing });
				break;
			case ConsoleLineType::Output:
			case ConsoleLineType::Help:
				DrawString(out, line, { 0, lineYEnd },
				    TextRenderOptions { .flags = OutputTextUiFlags, .spacing = TextSpacing });
				break;
			case ConsoleLineType::Warning:
				DrawString(out, line, { 0, lineYEnd },
				    TextRenderOptions { .flags = WarningTextUiFlags, .spacing = TextSpacing });
				break;
			case ConsoleLineType::Error:
				DrawString(out, line, { 0, lineYEnd },
				    TextRenderOptions { .flags = ErrorTextUiFlags, .spacing = TextSpacing });
				break;
			}
			if (lineYEnd < 0 || begin == 0)
				break;
			end = begin - 1;
		}
	}
}

const ConsoleLine &GetConsoleLineFromEnd(int index)
{
	return *(ConsoleLines.rbegin() + index);
}

void SetHistoryIndex(int index)
{
	CurrentInputTextState = InputTextState::RestoredFromHistory;
	HistoryIndex = static_cast<int>(std::ssize(ConsoleLines)) - (index + 1);
	if (HistoryIndex == -1) {
		ConsoleInputState.assign(DraftInput);
		return;
	}
	const ConsoleLine &line = ConsoleLines[index];
	ConsoleInputState.assign(TextWithoutPrompt(line));
}

void PrevHistoryItem(tl::function_ref<bool(const ConsoleLine &line)> filter)
{
	if (HistoryIndex == -1) {
		DraftInput = ConsoleInputState.value();
	}
	const int n = static_cast<int>(std::ssize(ConsoleLines));
	for (int i = HistoryIndex + 1; i < n; ++i) {
		const int index = n - (i + 1);
		if (filter(ConsoleLines[index])) {
			SetHistoryIndex(index);
			return;
		}
	}
}

void NextHistoryItem(tl::function_ref<bool(const ConsoleLine &line)> filter)
{
	const int n = static_cast<int>(std::ssize(ConsoleLines));
	for (int i = n - HistoryIndex; i < n; ++i) {
		if (filter(ConsoleLines[i])) {
			SetHistoryIndex(i);
			return;
		}
	}
	if (HistoryIndex != -1) {
		SetHistoryIndex(n);
	}
}

bool IsHistoryInputLine(const ConsoleLine &line)
{
	if (line.type != ConsoleLineType::Input)
		return false;
	std::string_view text = line.text;
	text.remove_prefix(Prompt.size());
	if (text.empty())
		return false;
	return HistoryIndex == -1 || TextWithoutPrompt(GetConsoleLineFromEnd(HistoryIndex)) != text;
}

void PrevInput()
{
	PrevHistoryItem(IsHistoryInputLine);
}

void NextInput()
{
	NextHistoryItem(IsHistoryInputLine);
}

bool IsHistoryOutputLine(const ConsoleLine &line)
{
	return !line.text.empty()
	    && (line.type == ConsoleLineType::Output || line.type == ConsoleLineType::Warning || line.type == ConsoleLineType::Error)
	    && (HistoryIndex == -1
	        || TextWithoutPrompt(GetConsoleLineFromEnd(HistoryIndex)) != line.text);
}

void PrevOutput()
{
	PrevHistoryItem(IsHistoryOutputLine);
}

void NextOutput()
{
	NextHistoryItem(IsHistoryOutputLine);
}

void AddInitialConsoleLines()
{
	if (ConsolePrelude->has_value()) {
		std::string_view prelude { **ConsolePrelude };
		if (!prelude.empty() && prelude.back() == '\n')
			prelude.remove_suffix(1);
		AddConsoleLine(ConsoleLine { .type = ConsoleLineType::Help, .text = StrCat(HelpText, "\n", prelude) });
	} else {
		AddConsoleLine(ConsoleLine { .type = ConsoleLineType::Help, .text = std::string(HelpText) });
		AddConsoleLine(ConsoleLine { .type = ConsoleLineType::Error, .text = ConsolePrelude->error() });
	}
}

void ClearConsole()
{
	ConsoleLines.clear();
	HistoryIndex = -1;
	ScrollOffset = 0;
	NumPreparedConsoleLines = 0;
	ConsoleLinesTotalHeight = 0;
	AddInitialConsoleLines();
}

} // namespace

bool IsConsoleOpen()
{
	return IsConsoleVisible;
}

void OpenConsole()
{
	IsConsoleVisible = true;
	FirstRender = true;
}

void CloseConsole()
{
	IsConsoleVisible = false;
	SDLC_StopTextInput(ghMainWnd);
}

const std::vector<ConsoleLine> &GetConsoleLines()
{
	return ConsoleLines;
}

void AcceptSuggestion()
{
	const LuaAutocompleteSuggestion &suggestion = AutocompleteSuggestions[AutocompleteSuggestionFocusIndex];
	ConsoleInputState.type(suggestion.completionText);
	if (suggestion.cursorAdjust == -1) {
		ConsoleInputState.moveCursorLeft(/*word=*/false);
	}
}

bool ConsoleHandleEvent(const SDL_Event &event)
{
	if (!IsConsoleVisible) {
		// Make console open on the top-left keyboard key even if it is not a backtick.
		if (event.type == SDL_EVENT_KEY_DOWN && SDLC_EventScancode(event) == SDL_SCANCODE_GRAVE) {
			OpenConsole();
			return true;
		}
		return false;
	}
	if (HandleTextInputEvent(event, ConsoleInputState)) {
		CurrentInputTextState = InputTextState::Edited;
		return true;
	}
	const auto modState = SDL_GetModState();
	const bool isShift = (modState & SDL_KMOD_SHIFT) != 0;
	switch (event.type) {
	case SDL_EVENT_KEY_DOWN:
		switch (SDLC_EventKey(event)) {
		case SDLK_ESCAPE:
			if (!AutocompleteSuggestions.empty()) {
				AutocompleteSuggestions.clear();
				AutocompleteSuggestionFocusIndex = -1;
			} else {
				CloseConsole();
			}
			return true;
		case SDLK_UP:
			if (AutocompleteSuggestionFocusIndex != -1) {
				AutocompleteSuggestionFocusIndex = std::max(
				    0, AutocompleteSuggestionFocusIndex - 1);
			} else {
				isShift ? PrevOutput() : PrevInput();
			}
			return true;
		case SDLK_DOWN:
			if (AutocompleteSuggestionFocusIndex != -1) {
				AutocompleteSuggestionFocusIndex = std::min(
				    static_cast<int>(AutocompleteSuggestions.size()) - 1,
				    AutocompleteSuggestionFocusIndex + 1);
			} else {
				isShift ? NextOutput() : NextInput();
			}
			return true;
		case SDLK_TAB:
			if (AutocompleteSuggestionFocusIndex != -1) {
				AcceptSuggestion();
				CurrentInputTextState = InputTextState::Edited;
			}
			return true;
		case SDLK_RETURN:
		case SDLK_KP_ENTER:
			if (isShift) {
				ConsoleInputState.type("\n");
			} else {
				if (AutocompleteSuggestionFocusIndex != -1) {
					AcceptSuggestion();
				} else {
					SendInput();
				}
			}
			CurrentInputTextState = InputTextState::Edited;
			return true;
		case SDLK_PAGEUP:
			++PendingScrollPages;
			return true;
		case SDLK_PAGEDOWN:
			--PendingScrollPages;
			return true;
		case SDLK_L:
			ClearConsole();
			return true;
		default:
			return false;
		}
		break;
#ifndef USE_SDL1
	case SDL_EVENT_MOUSE_WHEEL:
		if (SDLC_EventWheelIntY(event) > 0) {
			ScrollOffset += ScrollStep;
		} else if (SDLC_EventWheelIntY(event) < 0) {
			ScrollOffset -= ScrollStep;
		}
		return true;
#else
	case SDL_MOUSEBUTTONDOWN:
	case SDL_MOUSEBUTTONUP:
		if (event.button.button == SDL_BUTTON_WHEELUP) {
			ScrollOffset += ScrollStep;
			return true;
		}
		if (event.button.button == SDL_BUTTON_WHEELDOWN) {
			ScrollOffset -= ScrollStep;
			return true;
		}
		return false;
#endif
	default:
		return false;
	}
	return false;
}

void DrawConsole(const Surface &out)
{
	if (DebugOverlayIsAvailable())
		return;

	if (!IsConsoleVisible)
		return;

	OuterRect.position = { 0, 0 };
	OuterRect.size = { out.w(), out.h() - GetMainPanel().size.height - 2 };

	const std::string_view originalInputText = ConsoleInputState.value();
	if (CurrentInputTextState != InputTextState::UpToDate) {
		WrappedInputText = WordWrapString(StrCat(Prompt, originalInputText), OuterRect.size.width - (2 * TextPaddingX), TextFontSize, TextSpacing);
		if (CurrentInputTextState == InputTextState::RestoredFromHistory) {
			AutocompleteSuggestions.clear();
		} else if (IsNativeOnlyConsole()) {
			AutocompleteSuggestions.clear();
		} else {
			GetLuaAutocompleteSuggestions(originalInputText, ConsoleInputCursor.position, GetLuaReplEnvironment(), /*maxSuggestions=*/MaxSuggestions, AutocompleteSuggestions);
		}
		AutocompleteSuggestionsMaxWidth = -1;
		AutocompleteSuggestionFocusIndex = AutocompleteSuggestions.empty() ? -1 : 0;
		CurrentInputTextState = InputTextState::UpToDate;
	}

	const int numLines = static_cast<int>(c_count(WrappedInputText, '\n')) + 1;
	InputRectHeight = std::min(OuterRect.size.height, (numLines * LineHeight) + TextPaddingYTop + TextPaddingYBottom);
	const int inputTextHeight = InputRectHeight - (TextPaddingYTop + TextPaddingYBottom);

	InputRect.position = { 0, OuterRect.size.height - InputRectHeight };
	InputRect.size = { OuterRect.size.width, InputRectHeight };
	const Rectangle inputTextRect {
		{ InputRect.position.x + TextPaddingX, InputRect.position.y + TextPaddingYTop },
		{ InputRect.size.width - (2 * TextPaddingX), inputTextHeight }
	};

	if (FirstRender) {
		SDL_Rect sdlInputRect = MakeSdlRect(InputRect);
		SDL_SetTextInputArea(ghMainWnd, &sdlInputRect, static_cast<int>(ConsoleInputState.cursorPosition()));
		SDLC_StartTextInput(ghMainWnd);
		FirstRender = false;
		if (ConsoleLines.empty()) {
			InitConsole();
		}
	}

	const Rectangle bgRect = OuterRect;
	DrawHalfTransparentRectTo(out, bgRect.position.x, bgRect.position.y, bgRect.size.width, bgRect.size.height);

	DrawConsoleLines(
	    out.subregion(
	        TextPaddingX,
	        TextPaddingYTop,
	        GetConsoleLinesInnerWidth(),
	        OuterRect.size.height - inputTextRect.size.height - 8));

	DrawHorizontalLine(out, InputRect.position - Displacement { 0, 1 }, InputRect.size.width, BorderColor);
	DrawInputText(
	    out,
	    Rectangle(
	        inputTextRect.position,
	        Size {
	            // Extra space for the cursor on the right:
	            inputTextRect.size.width + TextPaddingX,
	            // Extra space for letters like g.
	            inputTextRect.size.height + TextPaddingYBottom }),
	    originalInputText,
	    WrappedInputText);

	SDL_Rect sdlRect = MakeSdlRect(OuterRect);
	BltFast(&sdlRect, &sdlRect);
}

void InitConsole()
{
	if (!ConsoleLines.empty())
		return;
	RegisterNativeDebugCommands();
	if (IsNativeOnlyConsole()) {
		ConsolePrelude = std::nullopt;
		AddInitialConsoleLines();
		return;
	}
	ConsolePrelude = LoadAsset("lua\\repl_prelude.lua");
	AddInitialConsoleLines();
	if (ConsolePrelude->has_value())
		RunLuaReplLine(std::string_view(**ConsolePrelude));
}

std::string_view TrimWhitespace(std::string_view text)
{
	while (!text.empty() && (text.front() == ' ' || text.front() == '\t' || text.front() == '\n' || text.front() == '\r')) {
		text.remove_prefix(1);
	}
	while (!text.empty() && (text.back() == ' ' || text.back() == '\t' || text.back() == '\n' || text.back() == '\r')) {
		text.remove_suffix(1);
	}
	return text;
}

std::optional<std::string_view> GetHelpQuery(std::string_view code)
{
	code = TrimWhitespace(code);
	if (!AsciiStrToLower(code).starts_with("help")) {
		return std::nullopt;
	}
	code.remove_prefix(4);
	code = TrimWhitespace(code);
	return code;
}

bool IsNativeOnlyConsole()
{
#ifdef DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT
	return authoritative::GetServerBackedRuntimeConfiguration().mode
		== authoritative::ServerBackedRuntimeConfiguration::GameMode::Authoritative;
#else
	return false;
#endif
}

std::optional<sol::table> ResolveLuaModulePath(std::string_view modulePath)
{
	sol::table table = GetLuaReplEnvironment();
	if (modulePath.empty()) {
		return table;
	}
	for (const std::string_view part : SplitByChar(modulePath, '.')) {
		if (part.empty()) {
			return std::nullopt;
		}
		const auto next = table.get<std::optional<sol::object>>(part);
		if (!next.has_value() || next->get_type() != sol::type::table) {
			return std::nullopt;
		}
		table = next->as<sol::table>();
	}
	return table;
}

std::string BuildModuleHelp(std::string_view modulePath)
{
	const std::optional<sol::table> module = ResolveLuaModulePath(modulePath);
	if (!module.has_value()) {
		return StrCat("Unknown module: ", modulePath);
	}

	std::vector<std::string> lines;
	for (const auto &[key, value] : *module) {
		if (key.get_type() != sol::type::string) {
			continue;
		}
		std::string keyStr = key.as<std::string>();
		if (keyStr.empty() || keyStr.starts_with("__") || value.get_type() == sol::type::lua_nil) {
			continue;
		}
		std::string line = StrCat("  ", keyStr);
		if (const std::optional<std::string> signature = GetSignature(*module, keyStr); signature.has_value() && !signature->empty()) {
			line.append(*signature);
		}
		if (const std::optional<std::string> docstring = GetDocstring(*module, keyStr); docstring.has_value() && !docstring->empty()) {
			std::string_view firstLine = *docstring;
			if (const size_t newlinePos = firstLine.find('\n'); newlinePos != std::string_view::npos) {
				firstLine = firstLine.substr(0, newlinePos);
			}
			StrAppend(line, " - ", firstLine);
		}
		lines.push_back(std::move(line));
	}
	c_sort(lines);

	if (lines.empty()) {
		return StrCat("No documented members found for module: ", modulePath);
	}

	std::string out = StrCat("Help for ", modulePath, ":");
	for (const std::string &line : lines) {
		StrAppend(out, "\n", line);
	}
	return out;
}

void RunInConsole(std::string_view code)
{
	AddConsoleLine(ConsoleLine { .type = ConsoleLineType::Input, .text = StrCat(Prompt, code) });
	if (const std::optional<std::string_view> helpQuery = GetHelpQuery(code); helpQuery.has_value()) {
		if (helpQuery->empty()) {
			AddConsoleLine(ConsoleLine { .type = ConsoleLineType::Help, .text = IsNativeOnlyConsole()
				? CurrentDebugCommandRegistry.HelpText()
				: std::string(CommandHelpText) });
		} else if (IsNativeOnlyConsole()) {
			AddConsoleLine(ConsoleLine { .type = ConsoleLineType::Error, .text = "Module help is unavailable in native-only mode." });
		} else {
			AddConsoleLine(ConsoleLine { .type = ConsoleLineType::Help, .text = BuildModuleHelp(*helpQuery) });
		}
		return;
	}
	if (const std::optional<std::string> result = CurrentDebugCommandRegistry.Execute(code); result.has_value()) {
		if (!result->empty())
			AddConsoleLine(ConsoleLine { .type = ConsoleLineType::Output, .text = *result });
		return;
	}
	if (IsNativeOnlyConsole()) {
		AddConsoleLine(ConsoleLine { .type = ConsoleLineType::Error, .text = "Unknown native debug command." });
		return;
	}
	tl::expected<std::string, std::string> result = RunLuaReplLine(code);

	if (result.has_value()) {
		if (!result->empty()) {
			AddConsoleLine(ConsoleLine { .type = ConsoleLineType::Output, .text = *std::move(result) });
		}
	} else {
		if (!result.error().empty()) {
			AddConsoleLine(ConsoleLine { .type = ConsoleLineType::Error, .text = std::move(result).error() });
		} else {
			AddConsoleLine(ConsoleLine { .type = ConsoleLineType::Error, .text = "Unknown error" });
		}
	}
}

void PrintToConsole(std::string_view text)
{
	AddConsoleLine(ConsoleLine { .type = ConsoleLineType::Output, .text = std::string(text) });
}

void PrintWarningToConsole(std::string_view text)
{
	AddConsoleLine(ConsoleLine { .type = ConsoleLineType::Warning, .text = std::string(text) });
}

} // namespace devilution
#endif // _DEBUG

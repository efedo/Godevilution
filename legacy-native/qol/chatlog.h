/**
 * @file chatlog.h
 *
 * Adds Chat log QoL feature
 */
#pragma once

#include "engine/gfx/surface.hpp"
#include "game/players/players.hpp"
#include "ui/menu/ui_flags.hpp"

namespace devilution {

extern bool ChatLogFlag;

void ToggleChatLog();
void AddMessageToChatLog(std::string_view message, Player *player = nullptr, UiFlags flags = UiFlags::ColorWhite);
void DrawChatLog(const Surface &out);
void ChatLogScrollUp();
void ChatLogScrollDown();
void ChatLogScrollTop();
void ChatLogScrollBottom();

} // namespace devilution

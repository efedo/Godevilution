## Introduction

If you compile the game in debug, you have multiple debug features available.

## Debug overlay and console

Debug builds include a Dear ImGui overlay. Press <kbd>`</kbd> to toggle its
top toolbar, then use **Console**, **Inspector**, and **Editor** to open or
close their independent windows. Press <kbd>Escape</kbd> to close the toolbar
and all overlay windows.

The Console window executes registered native debug commands first and falls
back to Lua in the same REPL environment used by the existing debug
command-line support. The Inspector reports the tile and entity under the
cursor, or the Editor's selected tile when one is selected.

Opening Editor in single-player pauses gameplay without showing the normal
pause label. Click a tile in the game view to select it; the selected tile is
outlined in red and remains selected until Editor closes or another tile is
clicked. Closing Editor restores the pause state that was active before it
opened. Editor is disabled in multiplayer.

The Editor's piece selector displays a dense grid of assembled dungeon-piece
thumbnails. Click a thumbnail to inspect its larger preview, then click
**Apply** or double-click the thumbnail to place it. **Cancel** closes the
selector without changing the tile.

The ImGui overlay is available on SDL2 and SDL3 builds that use the SDL
renderer path. SDL1 and non-renderer builds keep the legacy in-game console
fallback.

The legacy `dev` Lua module remains available as a compatibility frontend. The
native registry provides equivalent commands for the migrated display, level,
and search diagnostics. For example:

| Command | Description |
| ------- | ----------- |
| `dev.player.god()` | Toggles god mode. |
| `dev.player.invisible()` | Toggles invisibility. |
| `dev.display.grid()` | Toggles grid rendering. |
| `dev.display.vision()` | Toggles vision debug rendering. |
| `dev.level.seed()` | Shows seed info for the current level. |

Native equivalents include `debug.display.tileData [name]`,
`debug.display.scrollView [on|off]`, `debug.level.map.reveal`,
`debug.level.map.hide`, `debug.level.seed [level]`, and
`debug.search.item <name>` (with corresponding monster, object, and clear
commands). Player diagnostics are available as `debug.player.arrow <effect>`
and `debug.player.info [id]`; `debug.player.rejuvenate` restores life and mana,
while `debug.player.health <amount>` and `debug.player.mana <amount>` adjust
the corresponding pools by whole points. `debug.player.trn <asset-path|clear>` selects
or clears the native player TRN asset.
`debug.player.attributes.max` and `.min` set all four base attributes to their
native maximum or minimum. `debug.player.levelUp [amount]` sends the same
high-priority experience commands as the legacy level-up helper.
`debug.player.gold.give [amount]` and `.take [amount]` preserve the legacy
inventory-stack behavior; omitted amounts use the maximum sweep.
`debug.player.spells.setLevel <level>` preserves the learned-spell filter and
network update sequence.
Quest diagnostics are available as `debug.quests.activate <id>`,
`debug.quests.activateAll`, `debug.quests.info <id>`, and `debug.quests.all`.

The multiplayer chat remains available for player chat and multiplayer chat
commands. Debug commands should be run from the overlay console.

## Legacy chat commands

Some commands are still available from multiplayer chat:

| Command | Description |
| ------- | ----------- |
| `/help` | Shows a list of chat commands with descriptions. |
| `/arena` | Enters a PvP arena. |
| `/arenapot` | Gives arena potions. |
| `/inspect` | Inspects another player's stats and equipment. |
| `/seedinfo` | Shows seed info for the current level. |
| `/ping` | Shows latency statistics for another player. |

Debug quick messages using `/lua ...` continue to run through the Lua
compatibility frontend.

## Command-line parameters

| Command | Description |
| ------- | ----------- |
| `+` | Executes a debug-console command when loading the first game. Native commands run first; Lua expressions such as `+dev.player.god()` remain supported for compatibility. |
| `-f` | Display frames per second. |
| `-i` | Disable network timeout. |
| `-n` | Disable startup video. |

## In-game hotkeys

| Hotkey | Description |
| ------ | ----------- |
| `Shift` | While holding, you can use the mouse to scroll screen. |
| `m` | Print debug monster info. |
| `M` | Switch current debug monster. |
| `x` | Toggles `DebugToggle` variable. `DebugToggle` is a generic solution for temporary toggles needed for debugging. |

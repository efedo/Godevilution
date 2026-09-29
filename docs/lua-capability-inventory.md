# Lua Compatibility Inventory

Lua is a legacy native-engine compatibility surface, not the target gameplay or
presentation architecture. Existing names and callback payloads remain stable
until their replacements have parity coverage, but no new authoritative behavior
should be added to Lua.

As of 2026-09-29, the authoritative C# path, active content packs, and native
authoritative mode are Lua-free. The legacy compatibility runtime and bundled
scripts still exist. Capability migration, runtime disablement, and physical
removal are different completion states. The
[completion plan](completion-plan.md#m5-remove-production-legacy-dependencies)
owns scheduling and the final removal gate; this document owns the capability
mapping and compatibility requirements.

## Ownership Map

| Legacy surface | Current role | Target owner |
| --- | --- | --- |
| Bootstrap, sandbox, `require`, mod reload | Loads legacy scripts and data hooks | Validated C# content packs and module registry |
| `devilutionx.player`, `items`, `monsters`, `towners` | Gameplay state, data mutation, and interaction | C# authoritative domains and protocol intent |
| `devilutionx.events` | Legacy callback adapter | Typed server events and protocol event batches |
| `devilutionx.audio`, `floatingnumbers`, `render`, `i18n` | Presentation | Godot services |
| `devilutionx.system`, `log`, debug namespaces, REPL | Diagnostics and developer commands | Client/server diagnostics and authenticated admin tools |
| `devilutionx.version`, `message` | Version and player notification helpers | Handshake metadata and client notifications |

The native `GameEventBus` already gives legacy Lua callbacks stable event
sources for mod loading, data loading, game lifecycle, stores, damage, and
experience. Pointer-derived Lua identities, including the legacy monster ID,
must never cross the protocol boundary; stable server entity IDs are required.
The native client now skips Lua initialization, shutdown, and debug-console
registration when launched in authoritative server-backed mode. Explicit
legacy mode retains the complete Lua lifecycle and console for backward
compatibility.
That lifecycle decision is centralized in a native policy and covered by a
focused compatibility test so future startup changes cannot accidentally
reintroduce embedded Lua into the authoritative client.
Legacy `StoreOpened` callbacks use the same policy: authoritative store
presentation does not enter the Lua event bus, while native compatibility mode
continues to deliver the callback for existing mods.
The remaining native lifecycle callbacks (`GameDataReloaded`, `GameStart`, and
`GameDrawComplete`) use that same policy. Authoritative mode therefore does not
emit Lua-facing reload, startup, or per-frame callbacks; native compatibility
mode continues to emit them for existing mods.
The authoritative Data runtime now provides validated ordered content overlays
and transactional activation, so new server data extensions no longer require
Lua reload hooks. Native `ModManager` now owns archive selection, sound
reinitialization, and table-data reload after legacy scripts run; Lua remains
only the optional compatibility script phase. Core player/display debug toggles
now also have a deterministic native command registry; the debug console routes
those commands natively and retains Lua as a compatibility frontend for the
remaining REPL and module commands. Level-seed inspection and automap search
markers now use the same native registry, while their Lua names remain as
compatibility aliases. The display tile-data and scroll-view diagnostics now
use native handlers as well; Lua remains only the compatibility frontend for
these names. Player arrow-effect selection and player inspection use native
handlers as well, with Lua retaining the old names as aliases.
Player TRN selection is also a native debug command now, with the Lua `trn`
module retained as a compatibility alias.
The native debug console now stays script-free in authoritative mode: it skips
the legacy prelude, autocomplete, module help, and interpreter fallback and
shows only the registered native command list. Legacy mode retains the existing
script console behavior.
Authoritative debug chat hotkeys also skip `/lua` entries before opening the
console, while legacy mode retains the existing script hotkey behavior.
Authoritative startup also excludes script-backed default and unpacked `init.lua` mods; legacy mode retains their discovery and reload behavior.
The `dev.player.stats.rejuvenate` helper now delegates to the native
`debug.player.rejuvenate` command; the Lua function remains only as a
compatibility alias while the native command owns the health/mana mutation and
panel redraw.
The `dev.player.stats.adjustHealth` and `adjustMana` helpers likewise delegate
to `debug.player.health` and `debug.player.mana`; their native commands retain
the original Q6 adjustment, death handling, mana-base recalculation, and redraw
behavior.
The `dev.player.stats.setAttrToMax` and `setAttrToMin` helpers now delegate to
`debug.player.attributes.max` and `.min`; the native handlers own the four
attribute mutations while the Lua names remain compatibility aliases.
The `dev.player.stats.levelUp` helper delegates to `debug.player.levelUp`; the
native command preserves the default-one behavior and high-priority experience
command sequence.
The `dev.player.gold.give` and `take` helpers now delegate to native gold
commands. The native handlers retain stack filling/removal order, inventory
recalculation, and omitted-amount behavior while Lua remains a compatibility
alias.
The `dev.player.spells.setLevel` helper now delegates to
`debug.player.spells.setLevel`; the native command preserves the learned-spell
filter, per-spell network updates, and zero-level memorization reset.
The `dev.quests` activation and information helpers now delegate to the
corresponding `debug.quests` commands; quest state mutation and formatting are
owned by the native registry while the Lua names remain compatibility aliases.
Quest activation is offline-only in authoritative mode, while connected quest
information reads the server-projected state.
The `dev.level.map` reveal and hide helpers now delegate to
`debug.level.map.reveal` and `debug.level.map.hide`; automap mutation remains
implemented by native debug handlers while the Lua names remain compatibility
aliases.
The `dev.level.reset` helper now delegates to `debug.level.reset <level> [seed]`;
native reset validation, visited-state clearing, delta clearing, and optional
seed selection remain behaviorally compatible with the Lua helper. The command
is offline-only in authoritative mode.
The `dev.level.warp.dungeon` helper now delegates to
`debug.level.warp.dungeon <level>`; native level validation, already-there
handling, and the legacy level-transition mode remain compatibility behavior;
the command is offline-only in authoritative mode.
The `dev.level.warp.quest` helper now delegates to
`debug.level.warp.quest <level>`; quest lookup, set-level type selection, and
legacy transition behavior remain native-owned compatibility behavior; the
command is offline-only in authoritative mode.
The `dev.level.warp.map` helper now delegates to
`debug.level.warp.map <path> <dunType> <x> <y>`; native path, dungeon-type,
spawn-bound, and transition validation remain compatibility behavior; the
command is offline-only in authoritative mode.
The `dev.items.info` diagnostic now delegates to `debug.items.info`; selected
item lookup and network-pack validation are native-owned while Lua retains the
legacy name as a compatibility alias.
The `dev.level.exportDun` helper now delegates to `debug.level.exportDun`; the
native handler owns the legacy binary tile, monster, object, and transparency
export while Lua retains the compatibility alias.
The compatibility `system.get_ticks` wrapper now delegates to the shared native
`GetMillisecondsSinceStartup` timer, so demo/replay time and live SDL time use
one owner; the Lua function remains only as the legacy presentation alias.
Native server-backed input, polling, portal, store, quest, and ability requests
also use that timer at the compatibility boundary, so replay-aware time is not
reintroduced by the native client adapter.
The `dev.items.spawn` and `dev.items.spawnUnique` helpers now delegate to
`debug.items.spawn` and `debug.items.spawnUnique`; item generation, placement,
and network spawning remain native-owned while Lua retains the compatibility
aliases; the commands are offline-only in authoritative mode. The
`dev.monsters.spawn` and `dev.monsters.spawnUnique` helpers likewise
delegate to native commands, preserving town, ownership, name matching, crawl,
and count behavior behind the compatibility aliases; the commands are
offline-only in authoritative mode.
The `dev.towners.talk` and `dev.towners.visit` helpers now delegate to native
`debug.towners.talk` and `debug.towners.visit` commands; town-only checks,
legacy aliases, teleportation, and talk-window dispatch remain native-owned.
Authoritative monster encounters now resolve through a validated
`monster_types.tsv` catalog with stable/native identities, so future
`addMonsterDataFromTsv` migrations have a typed content-pack destination rather
than a Lua-owned data path.
Authoritative quest state now resolves through a validated `quest_types.tsv`
catalog with stable/native identities, routing levels, and single-player
availability, so future quest migrations have a typed content-pack destination
rather than a Lua-owned data path.

## Bundled Scripts

The clock mod and floating-number feedback now have Godot presentation owners,
while the legacy native client keeps its optional scripts for compatibility.
The Adria mana-refill mod maps to trusted server store
rules. Hellfire activation
now uses a native declarative archive-selection path rather than a Lua
bootstrap. All six Diablo/Hellfire character
classes, new-session class selection, starting attributes, and native Q6
life/mana formulas now live in validated content and the C# Diablo module.
Content-defined starting gold, equipment, belt, inventory, class skills, and
learned spells are also server-created and saved, so new character creation
requires no Lua ownership. The Monk's Search skill now resolves as a validated
C# status and typed client presentation rather than a client- or Lua-owned
effect. The Bard's Identify skill likewise resolves as a validated C# item
action with client-only selection and presentation rather than local or Lua
mutation. The Warrior's Repair skill now follows the same boundary while the
C# module owns its durability degradation and RNG sequence. The Sorcerer's
Staff Recharge skill likewise uses a typed C# item action with content-owned
staff levels and server-owned charge degradation. REPL and inspection helpers
may remain as optional
native diagnostics while the compatibility client exists.

The Godot client now consumes authoritative damage, healing, and experience
events through a typed combat-feedback mapper and renders transient floating
numbers from snapshot entity positions. The bundled Lua floating-number mods
remain available to the native compatibility client, but the native
server-backed client now projects local authoritative damage, healing, and
experience events directly through its floating-number renderer; Lua no longer
defines that server-backed presentation path.

The active base and Hellfire content packs contain no Lua assets or manifest
references; the server boundary suite enforces that removal gate. Legacy Lua
files remain available only outside those packs for explicit native
compatibility launches.

## Freeze Rules

1. Preserve existing Lua names and payloads until a characterized replacement
   exists.
2. Add authoritative behavior to C# domains, not Lua.
3. Represent data extensions as validated content-pack overlays, not new
   `add*DataFromTsv` calls.
4. Add presentation behavior to Godot and consume typed authoritative events.
5. Update this inventory and add a characterization test or fixture whenever a
   compatibility surface changes.

## Removal evidence

For each remaining surface, record the replacement owner, supported legacy
behavior, and the tests proving equivalence before deleting it. Data/mod hooks
need validated pack/module replacements; audio/render/UI hooks need Godot
presentation; REPL/admin capabilities need an explicit supported diagnostic
route. Native offline mutation commands must not become an authoritative bypass.

Keep the compatibility lifecycle and script names while supported legacy
consumers remain. Production packages must exclude the runtime and scripts at
M5, while any retained native oracle/compatibility build is explicitly isolated.
If that active compatibility build still requires Lua, physical removal and M5
remain open. Complete removal requires characterized replacements followed by
deleting the runtime, bindings, and bundled scripts from the active project;
historical Git revisions may retain the original reference implementation.
Do not count a server boundary test rejecting Lua references as evidence that
all legacy presentation and developer capabilities have been replaced.

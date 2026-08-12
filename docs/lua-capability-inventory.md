# Lua Compatibility Inventory

Lua is a legacy native-engine compatibility surface, not the target gameplay or
presentation architecture. Existing names and callback payloads remain stable
until their replacements have parity coverage, but no new authoritative behavior
should be added to Lua.

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
The authoritative Data runtime now provides validated ordered content overlays
and transactional activation, so new server data extensions no longer require
Lua reload hooks. Native Lua loading remains frozen for compatibility.

## Bundled Scripts

The clock and floating-number mods are presentation candidates for Godot. The
Adria mana-refill mod maps to trusted server store rules. Hellfire activation
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

## Freeze Rules

1. Preserve existing Lua names and payloads until a characterized replacement
   exists.
2. Add authoritative behavior to C# domains, not Lua.
3. Represent data extensions as validated content-pack overlays, not new
   `add*DataFromTsv` calls.
4. Add presentation behavior to Godot and consume typed authoritative events.
5. Update this inventory and add a characterization test or fixture whenever a
   compatibility surface changes.

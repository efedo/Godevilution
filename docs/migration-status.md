# Godevilution Migration Status

Status date: 2026-08-20

## Current state

| Area | State | Remaining work |
| --- | --- | --- |
| Protocol and sessions | Operational vertical slice with one generated C# protocol assembly shared by Server and Godot, including self-first complete multiplayer snapshots, additive live authoritative level-geometry projections, per-session class selection, class-skill/learned-spell state, explicit cast sources, resume precedence, live single-player and multiplayer handshakes/cast slices, a twelve-cycle server soak plus an eight-cycle live Godot-client reconnect soak while another participant remains connected, protocol v0.2 independent content/ruleset handshake identities adopted by production launchers while preserving the v0.1 alias, server-owned adjacent melee plus line-of-sight ranged player-versus-player admission with native timing, physical/elemental damage, target resistance, shield/class blocking, equipment knockback, lethal death/ear handling, targeted Firebolt/Charged Bolt/Bone Spirit admission for living remote players, live Firebolt, Charged Bolt, Bone Spirit, Heal Other, and Resurrection player-target coverage, live two-client coverage including effect-tick shield resolution, typed Diablo victory events for client presentation, and Normal/Nightmare/Hell difficulty projection in authoritative snapshots | Add broader reconnect/multiplayer fixtures and production-client soak coverage |
| Deterministic replay | Shared C++/C# gate hashing complete multiplayer participant state, with server-owned corpse tiles, native per-tick monster regeneration, refreshed ordinary-death and spell-timeline checkpoints, deterministic quest-pool selection/rejection, a 1,000-tick autonomous regeneration soak, multiplayer autonomous-tick participant fixtures with explicit secondary-session movement and melee-attack routing, shared melee and ranged elemental-equipment fixtures covering native fire/lightning and arrow channels, resistance, and RNG, native/C# chained-action timing coverage for first-frame attack skips, seeded and autonomous GoatRanged and SkeletonRanged MonsterArrow admission/travel/impact/removal parity including secondary-participant routing, autonomous Succubus BloodStar admission/travel/player-impact parity with deterministic monster-magic hit/block RNG, Q6 damage, and block-animation state, plus Lich-family MonsterMagic admission/travel/player-impact parity for deterministic hits, misses, and secondary-participant impacts, live protocol coverage for the checked-in Hellfire encounter and Sorcerer loadout overlays, plus composed-manifest Hellfire ranged replay and shared Firebolt, Fireball, Bone Spirit player-target, and Bone Spirit homing/resistant-monster replays | Expand broader multi-actor autonomous and reconnect replay coverage |
| Stores and inventory | C# remote for vendor actions, item services, spell-book, potion, rejuvenation, elixir (including Hellfire Spectral Elixir), and Blacksmith/Accuracy/Sharpness/Fortitude oil consumption, and authoritative inventory relocation, with stable item references carried by native and Godot clients; content-backed moves now enforce native equipment-slot topology and belt footprint rules, normal native equipment drags preserve the selected slot through atomic `AutoEquipItemRequested`, all native automatic equipment, inventory, belt, potion, rejuvenation, elixir, and oil paths use stable server-owned intents, the server now enforces active-arena-only Arena Potion admission, and the Godot HUD submits inventory or belt sources as stable references for books, targetless consumables, and two-item oils through the same protocol | Expand remaining miscellaneous-item coverage, broader live-client coverage, and exact legacy edge-case characterization |
| World gameplay | C# remote vertical slice with content-owned quest item transitions, generic authored towner turn-ins (including item-consuming activation-only, non-consuming dialogue-only, completion-stage, alternate-item, and normal-item rewards), repeatable content-owned quest/towner stages (including all four Black Mushroom/Adria stages and Ogden's post-completion King Leoric dialogue), explicit itemless and completed-quest interaction availability, server-owned per-player visited-level state for native `_pLvlVisited` gates, unique-monster quest dialogue and authored follow-up variable transitions, activation rewards, and hand-ins, content-declared quest-object effects with validated quest activation/completion, native variable transitions, and one-level spell grants, and validated content-backed world-cell mutation overlays persisted with the session and projected through effective level geometry; the native authoritative client submits `TurnInQuestRequested`, `TalkToQuestRequested`, and `OperateObjectRequested` and projects server dialogue/events (first shipped base Skeleton King route/completion and Butcher activation/completion, Hellfire Farmer activation/post-Hive reward, base Cain/Lazarus staff hand-in and Lazarus Storyteller completion stage, Hellfire Grave Matters/Barmaid dialogue, base and Hellfire Magic Rock and Anvil/Blacksmith, base Ogden's Sign, Little Girl, Jersey's Jersey, Defiler, Zhar, Warlord, Lachdanan, and Na-Krul paths) | Expand AI, level generation, remaining quests, dialogs, towners, environmental behaviors, ship authored set-piece mutation rows and book/lever object placement/projectile/map rows, and multiplayer coverage |
| Persistence | Server-owned snapshot saves with an explicit version-1 compatibility codec, validated saved-class reconstruction for cold sessions, persisted per-player native-compatible `visited_level_ids`, live process-restart restoration of movement/class state through the shared protocol, and a durable save-root world seed that keeps generated geometry stable across restarts | Add concrete migrations when formats change and broader recovery/soak testing |
| Native client | Explicit legacy or authoritative runtime mode; connected native character-panel attribute allocation now submits server-owned stat-point intents and applies projected stat points/resources, with legacy `CMD_ADD*` retained only when disconnected; connected native stash-close drops now submit the authoritative held-item drop intent, native mouse/controller dungeon talker interactions now submit authoritative quest dialogue, controller item/object projections fail closed when their entity IDs are unavailable, and controller quest-target movement submits authoritative target cells; connected controller monster, remote-player, and object interactions now resolve projected targets to authoritative entity IDs with fail-closed handling for missing projections; normal item-cursor inventory/belt placement uses stable-reference transfers, normal equipment drags carry the selected slot through atomic `AutoEquipItemRequested`, automatic inventory/equipment/belt relocation remains server-owned, and content-declared towner/unique-monster/quest-object interactions submit `TurnInQuestRequested`/`TalkToQuestRequested`/`OperateObjectRequested` with server dialogue/event projection, including repeatable Black Mushroom/Adria stages, server-owned `visited_level_ids` for native `_pLvlVisited` gates, and Ogden's itemless post-completion King Leoric dialogue (first shipped by the base Skeleton King route/completion and Butcher activation/completion, Hellfire Farmer activation/post-Hive reward, base Cain/Lazarus staff hand-in and Lazarus Storyteller completion stage, base and Hellfire Magic Rock and Anvil/Blacksmith, Ogden's Sign, Little Girl, Jersey's Jersey, Defiler, Zhar, Warlord, Lachdanan, and Na-Krul paths) | Continue removing compatibility-pool assumptions as Godot presentation matures |
| Godot client | Complete for protocol-boundary demonstrations, including typed Monk Search, Rogue Trap Disarm, and Etherealize presentation, live Etherealize, Barbarian Rage, Monk Search, Rogue Trap Disarm, Bard Identify, Warrior Repair, and Sorcerer Recharge service coverage, a stable authoritative spell-source selector with server-owned memorized and item-backed spell display names, validated selection and submission of adjacent content-owned quest/towner stages, same-instance reconnect with bounded automatic retry, right-click authoritative monster and remote-player attack intent routing, behavior-driven cast-cursor remote-player targeting without client spell-ID tables, live entity-targeted monster, player attack, Firebolt, Heal Other, and Resurrection action coverage, server-owned stealth target presentation, authoritative player action indicators, and typed projectile presentation families | Production assets, broader prediction, menus, packaging, and complete play flow |
| Domain kernel | Independent dependency-free .NET 8 assembly | Extract additional game-neutral deterministic primitives only where reuse warrants it; keep Diablo state in its module |
| Data runtime | Independent .NET 8 assembly with ordered overlays, schema identities, dependency-version constraints, strict TSV Boolean parsing, immutable native `.dun`/`.til`/`.sol` asset composition and hashing, and transactional activation, depending only on Domain | Document future table extensions and broaden reload fixtures |
| Engine composition | Independent .NET 8 assembly with generic deterministic lifecycle, module compatibility preflight, transactional candidate preparation, atomic candidate registration, coordinated ruleset publication, generation leases with explicit retired-target draining, and reusable bounded grid navigation, depending only on Domain and Data | Keep filesystem/reflection outside Engine and broaden replacement fixtures |
| Diablo module | Independent .NET 8 assembly with content activation, transport-neutral simulation, and state capture/restore, depending only on Domain, Data, and Engine | Expand exact legacy gameplay/content coverage and register future capabilities through Engine |
| External content | Explicitly manifested base and Hellfire variant packs plus schema-aware, dependency-versioned composite-key overlays, including all six native-compatible classes, their complete native animation timings, complete initial loadouts, authoritative definitions for every directly castable starting source, all six validated cursor-driven class capability identities (Repair, Trap Disarm, Recharge, Identify, Search, and Rage), native level-range generation metadata, optional `procedural_levels.tsv` definitions with deterministic connected movement geometry and independently seeded missile occlusion from row and world seeds, validated level-aware monster candidate selection, a bounded server-owned seed-stable spawn planner exposed through Diablo content activation and optionally consumable as generated combat targets with deterministic validated drops, unique/Diablo encounter flags, native death-animation durations, native experience rewards, native normal, special, and magic-projectile monster attack accuracy, native monster resistance metadata, the validated Hellfire Hork Demon AI identity, Hork Spawn missile mapping and server-owned speed-8 nine-tick expiry summon controller, the validated item presentation-name projection, shared item cursor-symbol catalog, unique-item cursor projection, item random-drop minimum-level metadata, weighted drop-rate metadata, explicit usable-item metadata for consumable source admission, normalized item armor-range metadata, symbolic normalized native special-effect metadata, 22 native spell-backed scroll consumable rows, explicit Identify item-action metadata and authoritative consumable-source handling, validated item-action magnitudes, nine Hellfire quest and miscellaneous item identities, the Hellfire full-potion and rejuvenation family, Hellfire oil and elixir consumables, complete normalized base armor, standard weapon, generic ring/amulet, and Arena Potion rows, corrected native scroll projections, four normalized dynamic spell-book templates, validated metadata and deterministic generation for all 23 native bookable spells, authoritative `ReadBookRequested` learning/consumption behavior, integrated store/static-drop/procedural-drop/quest-reward spell-book generation, and the Hellfire Blood Star, Bone Spirit, Fire Wall, Lightning Wall, Flame Wave, Inferno, Chain Lightning, Nova, Apocalypse, Rage, Elemental, Guardian, Town Portal, Reflect, Magi, Mana, Ring of Fire, Immolation, Jester, Berserk, Warp, Golem, and Etherealize spell behaviors | Expand generated geometry toward native map synthesis, externalize remaining native spells, unique Hellfire quest behavior and rewards, and remaining item metadata; and extend the table reference as new schemas land |
| Gameplay modules | Typed dependency-ordered capabilities, inclusive semantic dependency-version and Engine API ranges, atomic activation, preflight diagnostics, a first-party Diablo factory, explicit hash-verified Server packages, and repeated eight-generation replacement/retirement coverage | Add further first-party capabilities |
| Host adapters | Independent .NET 8 generic Host and Diablo adapter assemblies with exact dependency tests; composition-only .NET 10 executable | Add configurable adapter selection if additional authoritative game adapters are introduced |
| Lua removal | Hellfire archive activation and the native post-bootstrap reload pipeline no longer require Lua; compatibility inventory established; authoritative data extensions use validated overlays; player/display diagnostics, item and monster spawning/diagnostics, towner interaction, level export, TRN selection, level reset/quest-warp/custom-map warp and seed inspection, automap reveal/hide and search, player rejuvenation, player health/mana adjustment, attribute max/min, level-up, player gold adjustment, spell-level adjustment, and quest diagnostics now use a deterministic native command registry; a server/Godot boundary test rejects Lua/sol2 references from the authoritative C# source and project path | Replace remaining native debug/admin and compatibility script hooks, then remove the Lua REPL and presentation capabilities |

## Release verification baseline

On 2026-08-20, the current Release artifacts passed the managed boundary and
client suites without failures: Domain 11 tests, Data 26, Engine 43, Diablo
1,388, Server 411, and Godot protocol 140. These counts are a dated
verification baseline, not a substitute for rebuilding the projects before a
future run.

## Implemented authoritative systems

- Connected native Town Portal collision now resolves the projected portal ID
  for the local authoritative level and submits `UsePortal`; disconnected
  sessions retain legacy `CMD_WARP` compatibility behavior. Portal projection
  is sorted and duplicate-checked before entering the native session state.

- The native authoritative world projection now carries sorted, validated
  server projectile snapshots through the same boundary as monsters, items,
  and objects. The native presentation bridge materializes the first supported
  spell, monster, area, impact, layered-effect, and player-arrow families
  (including Lightning segments, Flash layers, Resurrection beam, Flame Wave,
  Inferno, Apocalypse impact, and Elemental) as presentation-only missile
  sprites when the active native content pack defines them, applying
  authoritative position and
  lifetime while skipping local process, collision, and damage logic. The
  bridge fails closed for unsupported families or content-missing missile rows;
  those projectiles remain projection-only until their native presentation
  mapping is available.
  The server also supplies an additive exact native visual identity, so the
  compatibility client no longer needs to infer supported sprites from
  gameplay behavior numbers; zero-valued metadata preserves older-server
  fallback behavior and unknown explicit identities fail closed.
  Snapshot-driven collision also submits the same intent when the authoritative
  portal has no locally-created legacy missile.

- The standalone authoritative executable now exposes a local operator console
  with `status`, `reload`, `help`, and `quit`; `reload` re-reads validated
  content/module candidates and uses the existing transactional session
  migration barrier, while failed candidates leave the active ruleset intact.

- Object snapshots now project additive server-owned shrine, quest, breakable,
  and reveal-gated trap presentation families, while older or unknown metadata
  retains the generic Godot object path.

- Godot's interaction panel now renders typed server-owned dialogue events as
  quest/message identities without importing native text tables; localized text
  remains a later presentation/content concern.

- Dynamic spell-book selection now derives single-player exclusions from the
  validated `Resurrection` and `HealOther` spell behaviors, while retaining the
  native-ID fallback needed by older content packs and multiplayer availability.

- Generated authoritative item state now carries the stable content item ID
  through protocol-backed saves. Catalog resolution prefers that ID before the
  retained native item index; zero-valued legacy state and the legacy replay
  hash remain compatible.

- Server-owned item quest effects, combinations, quest-item completion, and
  PvP-ear deduplication now use that stable identity before the native index,
  so shared native projections cannot select the wrong content definition.

- Snapshot restoration rejects unknown nonzero stable item IDs when an active
  item catalog is present; legacy zero-valued IDs and catalog-less simulations
  remain compatible.

- `PlayerItemReference` now carries an additive stable item ID. Godot populates
  it from authoritative snapshots for item services and spell sources, while
  the server treats zero as legacy positional addressing.

- Inventory relocation now carries the same additive `PlayerItemReference`.
  Native and Godot indexed moves resolve the latest authoritative item before
  submission, and the C# module rejects stale identities without changing the
  inventory grid; the legacy index-only fields remain compatible.

- The Godot presentation project and protocol client now have executable
  dependency-boundary coverage: the protocol client may reference only the
  shared protocol assembly, and the Godot project may reference only those two
  presentation/protocol projects, never the server or Diablo module.

- The native client now skips Lua initialization, shutdown, and debug-console
  registration in explicit authoritative server-backed mode. Legacy mode keeps
  the existing Lua compatibility lifecycle.

- The native authoritative adapter now carries stable item identities through
  projected inventory, equipment, belt, and cast-source state. Native service,
  store, and spell-source commands resolve references from the authoritative
  snapshot instead of relying on positional slots alone.

- Native authoritative startup now registers a `ModManager` archive/data reload
  callback and activates the selected mod archives without initializing Lua.
  Legacy mode still replaces that callback with the Lua compatibility hook so
  existing scripted mod ordering remains unchanged.

- Connected native Telekinesis now submits the projected world-item, object, or
  monster entity through the authoritative cast path; the legacy cursor helper
  remains active for disconnected and legacy sessions.

- The shared protocol now carries an additive target-cell movement intent. The
  Diablo module resolves one deterministic server-owned path step through the
  Engine navigation primitive, and connected native cursor walking plus
  repeated dungeon walking use it; legacy directional and auto-path commands
  remain available for compatibility paths.

- Godot's left-click presentation flow now submits the same target-cell
  movement intent when no authoritative cast or item interaction applies;
  server-side path resolution remains authoritative.

- Connected native targeted scroll and rune cursor casts now submit the
  authoritative cast intent with the exact stable consumable source reference;
  legacy spell commands remain available outside server-backed sessions.

- Connected native direct scroll and rune item use now submits the same
  authoritative cast intent for non-keyboard and immediate-use paths, retaining
  the exact stable source reference and the legacy fallback outside connected
  server-backed mode.

- Connected native Resurrection and Heal Other cursor actions now submit
  explicit authoritative player-targeted casts with memorized-spell or
  consumable source identity, while the legacy target commands remain the
  disconnected compatibility path.

- Connected native mouse and controller town-towner interactions now enter
  `TalkToTowner`, allowing authored quest turn-ins to submit server-owned
  intents while preserving local presentation for non-authored store/dialogue
  behavior.

- Connected native mouse entity-targeted monster/player attacks and ordinary
  object operations now submit projected authoritative entity IDs, and repeated
  cursor actions use the same server-owned intents. Adjacent primary native
  world-item clicks now submit the projected world-item entity through the
  authoritative pickup intent in both town and dungeon modes; distant clicks
  retain native auto-walk compatibility. Connected native Trap Disarm now
  submits the existing `DisarmTrapRequested` command. Connected native
  location-only and repeated attacks submit the additive cell-targeted attack
  intent, which preserves native directional melee and ranged path semantics;
  legacy mode retains the original commands.

- Native authoritative inventory use now submits spell-book consumption through
  `ReadBookRequested`; the legacy client keeps its local book path only outside
  the connected server-backed mode.

- The connected native mouse held-item drop path now submits the authoritative
  drop intent; `CMD_PUTITEM` remains the disconnected compatibility path.

- Connected native trigger polling now defers level transitions entirely to
  server-owned movement/warp resolution instead of emitting legacy `CMD_NEWLVL`.

- Authoritative experience level-ups now apply native class life/mana growth,
  level-up refills, `NoMana` suppression, and server-owned stat-point awards;
  the projected native character panel receives the resulting maxima and
  resources from the snapshot.

- The shared protocol now projects every validated item action, including
  attribute-all and all four oil actions. The Godot HUD submits targetless
  potion, rejuvenation, and elixir actions as stable-reference
  `UseConsumableRequested` commands, and its oil-target list submits a second
  stable equipment or inventory reference; server validation remains the final
  authority for target restrictions.

- Native authoritative potion, rejuvenation, elixir, and supported oil use now submits
  `UseConsumableRequested` with a stable inventory or belt reference. The Diablo
  module owns native Q6 life/mana restoration, class multipliers, deterministic
  RNG, elixir stat ceilings, Q6 resource growth, Hellfire full-resource effects,
  `NoMana` suppression, persistence, and item consumption. Oil selection retains
  a presentation-only cursor until the server receives the exact source and
  target references; the server owns target restrictions, mutations, and
  atomic source consumption. Remaining miscellaneous cursor actions are still
  explicit follow-up seams.

- Arena Potion use now preserves the native set-level restriction: the
  server accepts the full-rejuvenation action only on active arena runtime
  levels and rejects the same item elsewhere without consuming it.

- Godot now exposes authoritative belt source selection for books, targetless
  consumables, and oil sources; each request carries the belt slot and stable
  item identity, while vendor service actions remain inventory-only.

- Live single-player protocol coverage now consumes a starting belt potion by
  its stable reference and verifies that only the addressed belt entry is
  removed.

- Connected native Cathedral Map use now submits the projected grave object
  through `OperateObjectRequested`; the server validates adjacency, quest state,
  and the stable map definition before consuming it.

- Connected native Hellfire Rune Bomb use now submits a projected Hive entrance
  object through `OperateObjectRequested`; the server validates the stable item,
  consumes the bomb, completes Farmer's Orchard, and exposes the gated Nest warp.

- Hellfire Magic Rock pickup is now an external `item_quest_effects.tsv`
  transition. The authoritative pickup path activates and logs quest 3001 and
  advances its native first variable; legacy native pickup remains compatible.

- Hellfire Anvil of Fury pickup now uses the same external item-to-quest
  transition catalog. The authoritative pickup path activates and logs quest
  3011; legacy native pickup remains compatible.

- Hellfire Optic Amulet and Arkaine's Valor pickup now complete their active
  content-defined quests through the same catalog (quests 3009 and 3012).
  Inactive quests retain the native no-completion behavior.

- Hellfire Black Mushroom pickup now advances quest 3002 from
  QS_MUSHSPAWNED to QS_MUSHPICKED through the content-owned
  required_variable1 precondition; unexpected quest states remain unchanged.

- Content-declared towner turn-ins are now generic. The server
  validates the towner, level, adjacency, native `_qvar2` conversation gate,
  and required Anvil item; it then consumes the item, completes quest 3011, and
  spawns Griswold's Edge through `TurnInQuestRequested`. The base and Hellfire
  Anvil/Blacksmith paths use the same definition. Legacy native behavior
  remains compatible outside authoritative mode; the connected native client
  now selects the active quest from the content-declared towner metadata and
  projects the returned dialogue event.

- Content-declared unique-monster conversations are now generic. The native
  adapter projects each monster's opaque unique identity, selects the active
  authored quest, and submits `TalkToQuestRequested`; the server validates the
  target identity, live state, level, and adjacency before activating the quest
  and returning its dialogue event. Dialogue definitions may additionally
  declare an inventory hand-in and relative reward; Hellfire Defiler remains
  the first conversation-only definition and Lachdanan is the first hand-in
  definition. Authored `dialogue_completion_variable1` now also gates unique
  monster combat: the Warlord remains non-combatant while its staged dialogue
  is pending, and direct attacks are rejected until the native terminal marker
  is reached. Legacy hardcoded monster conversations remain fallback behavior
  until their content actions are externalized.

- Staged unique-monster dialogue may now declare a deterministic
  `RandomUniqueMonsterDrop` reward. The server selects the native unique-drop
  family from validated item metadata, generates the item with the active
  quality rules, and projects it in the world snapshot; base Gharbad's second
  stage is the first shipped use.

- Content-declared quest-object effects are now generic. The native adapter
  routes active `QuestSequence` and `QuestItemCompletion` objects through
  `OperateObjectRequested`; the server owns effect ordering, quest state,
  required-item consumption, activation reconciliation, and any linked
  authored quest dialogue event. The Hellfire Na-Krul tome sequence is the
  first shipped definition; authored sequence follow-ups can now apply
  validated map mutations or server-owned Phasing/Guardian projectiles after
  a correct step. Complete native set-level bundles now materialize their
  active-map object anchors; unavailable bundles retain the legacy fallback.

- Authored `WorldMutation` object effects now load a validated
  `world_mutations.tsv` catalog. The server applies each referenced cell
  overlay once, persists active mutation IDs in transport-independent saves,
  and sends effective movement/missile geometry through the existing
  `LevelGeometrySnapshot`; native book/lever placement is supplied by the
  validated set-level catalog when its bundle is available.

- Authored object rows now also support validated quest transitions. An object
  may activate or complete its linked quest, set `_qvar1` or `_qvar2`, and gate
  the transition on prior variable values; the transition is applied in the
  same accepted `OperateObjectRequested` command as any geometry overlay.

- Authored object rows may also generate one validated normal or unique item
  reward at a relative offset. The server owns catalog validation, deterministic
  generation, placement, persistence, and snapshot projection; set-level
  anchors are materialized from complete native bundles, while their later map
  mutation effects remain a separate seam.

- Authored object rows now support the validated `GrantSpellLevel` effect.
  `OperateObjectRequested` resolves the spell through the active catalog and
  increments the player’s memorized level with native level caps. An optional
  `effect_spell_symbol` cross-checks the stable spell namespace; set-level
  anchors are materialized from complete native bundles, while their later map
  mutation effects remain a separate seam.

- Schema 1.359 adds the validated `SpawnSpellProjectile` object effect.
  Authored rows can spawn a server-owned Phasing or Guardian controller at the
  object position through the existing projectile snapshot/tick path; the
  active spell catalog rejects unsupported behaviors. Exact native target
  offsets, set-level placement, and map mutations remain later seams.

- Schema 1.360 adds optional `projectile_target_offset_x` and
  `projectile_target_offset_y` columns to authored objects. The server carries
  these validated offsets through saves, protocol snapshots, and canonical
  hashes before constructing the Phasing or Guardian controller, so native
  book-relative targets no longer require client-side coordinates.

- Schema 1.361 adds validated quest-sequence follow-up metadata. Correctly
  ordered `QuestSequence` steps may apply one content-backed world mutation or
  spawn a server-owned Phasing/Guardian controller at authored relative
  offsets; wrong-order resets do not apply follow-ups, and the state is carried
  through saves, snapshots, protocol projection, and canonical hashes.

- Schema 1.362 adds validated staged-dialogue follow-ups. An accepted
  unique-monster conversation stage may apply one content-backed world-cell
  mutation after the quest transition, item consumption, and authored reward
  validation; invalid mutation references reject before any state changes.

- Schema 1.363 adds `set_level_objects.tsv`, which externalizes the native
  fixed-object positions and template footprints for the three quest set-level
  maps that currently use hardcoded `InitializeLoadedObject` calls. The
  validated catalog is carried by Diablo activation and simulation metadata.
  When the corresponding active native bundle is present, activation now
  materializes those placements as server-owned interactive objects; older
  packs retain the metadata-only catalog. Set-level transitions remain
  separate.

- Schema 1.364 adds `set_level_maps.tsv`, which externalizes the native
  pre-map/active-map asset paths, dungeon type, and entry spawn positions for
  Skeleton King's Lair, the Chamber of Bone, and Archbishop Lazarus' Lair.
  Activation validates the stable set-level links and carries the catalog in
  simulation metadata; complete native bundles are now materialized during
  activation and reject an out-of-bounds or blocked active-map spawn point,
  while routing set-level transitions remains separate.

- Schema 1.365 adds optional `til_asset` and `sol_asset` paths to
  `set_level_maps.tsv`. Current rows identify the native dungeon-type geometry
  inputs shared by each pre-map and active-map pair; older packs remain
  compatible when the optional paths are absent, while composed materialization
  requires them.

- Schema 1.366 adds the optional native `transparency_asset` path to
  `set_level_maps.tsv`. When declared, activation composes and validates the
  transparency `.dun` layer against the active-map dimensions and retains its
  d-piece values on the authoritative level geometry; older packs remain
  compatible without it.

- The Diablo module now has a strict native `.dun` parser that preserves the
  mega-tile layer and all four d-piece layers (reserved, monsters, objects, and
  transparency) from checked-in native fixtures. It deliberately does not
  infer walkability from tile IDs; collision interpretation and set-level
  geometry materialization remain the next native-parity boundary.

- The geometry boundary now has a validated materializer contract: native
  little-endian `.til` mega-tile records and `.sol` d-piece property bytes can
  be loaded without presentation dependencies, then expanded into authoritative
  blocked and missile-blocked cells with missing mappings rejected. The shipped
  packs do not yet include the native `.til`/`.sol` assets, so the shipped
  set-level rows remain metadata-only. A complete bundle now materializes both
  pre-map and active-map variants during Diablo activation as authoritative
  levels in the reserved `0x80000000` runtime range, and its active-map object
  placements become server-owned interactive objects in a reserved entity
  range; partial bundles reject activation. The engine exposes a single
  validated bundle loader for the `.dun`/`.til`/`.sol` composition, keeping
  asset acquisition separate from geometry interpretation.

- Content packs now carry relative native `.dun`, `.til`, and `.sol` assets as
  immutable binary entries alongside TSV tables. Asset bytes participate in the
  manifest hash; identical inherited assets compose safely, conflicting bytes
  reject activation, and the Diablo geometry loader consumes the composed asset
  set without filesystem access in the engine or gameplay module.

- Content-owned towner turn-ins now support normal item rewards as well as
  unique rewards. The server validates and generates the authored reward item
  at the native reward level and position; Hellfire Little Girl consumes
  Theodore and creates the level-13 amulet path through `TurnInQuestRequested`.

- Hellfire Jersey's Jersey now uses the same authored towner boundary. Complete
  Nut's stable towner identity, Brown Suit hand-in, native conversation gate,
  Bovine Plate reward, and dialogue IDs are content-owned; the server consumes
  the suit, completes quest 3024, and spawns the unique reward at the native
  southeast position.

- Hellfire Jersey's Jersey Grey Suit tease now uses an authored alternate
  towner item interaction. The server consumes Grey Suit, emits the native tease
  dialogue, and leaves quest 3024 unchanged; the authoritative client no longer
  rejects or falls through to the local Cow Farmer branch for that item.

- Hellfire Farmer's Orchard now uses an explicit activation-only towner row.
  Complete Nut's counterpart, the Farmer, activates quest 3018, advances the
  native `_qvar1` gate, and creates the initial Rune Bomb through the same
  `TurnInQuestRequested` boundary; the existing Hive object remains the
  server-owned completion path.

- Hellfire Farmer's post-Hive reward now uses a second authored towner stage.
  Completion exposes a one-shot Farmer interaction through the protocol,
  advances the native `_qvar2` marker, generates the Auric Amulet, and projects
  the native completion dialogue; the connected client no longer falls back to
  local reward spawning.

- Base Cain's Lazarus hand-in now uses an item-consuming activation-only
  towner row. The server consumes Staff of Lazarus, writes native `_qvar1 = 2`,
  emits the activation dialogue, and exposes the quest to the authored Lazarus
  unique-monster conversation; the native client stops routing repeat Cain
  interactions after activation.

- Base Magic Rock pickup and Griswold's staged hand-in now use authored content.
  The pickup effect activates quest 3001; the first Blacksmith interaction
  advances `_qvar2` and emits the native prompt without consuming Magic Rock,
  while the second consumes it and spawns Empyrean Band at the native position.

- Base Anvil of Fury pickup and Griswold's staged hand-in now use authored
  content. The pickup effect activates quest 3011; the first Blacksmith
  interaction advances `_qvar2` and emits the native prompt without consuming
  Anvil, while the second consumes it and spawns Griswold's Edge at the native
  position.

- Base Butcher activation now uses an authored Dead Guy interaction, with normalized native The Butcher monster content supplying the completion target.

- Base Skeleton King now uses an authored route-activated quest state, with
  normalized native Skeleton King monster content supplying the completion
  target for native monster identity 50. Schema 1.339 also externalizes its
  native route `_qvar1 = 1` activation marker.

- Base Lazarus now uses an authored completion-stage Storyteller interaction:
  the server validates native `_qvar1 = 7`, writes `_qvar1 = 8`, emits the
  native completion dialogue, and logs the authored Diablo follow-on quest.

- Hellfire Grave Matters now uses an authored non-consuming Barmaid dialogue
  stage. The server requires the Cathedral Map, records native `_qmsg = 274`,
  emits Gillian's dialogue, and preserves the map and quest completion state.

- Base Poisoned Water now uses an authored Pepin completion-stage interaction.
  Its native `_qvar1`-only marker is carried through the protocol, the server
  writes `_qvar1 = 2` without changing `_qvar2`, emits `TEXT_POISON5`, and
  generates the authored Ring of Truth reward at the native position. Schema
  1.351 also externalizes Pepin's initial Caves/Poisoned Water visitation gate
  and active-but-unlogged fallback through `require_unlogged` stages.

- Base Ogden's Sign now uses authored quest state 3008. The first Tavern
  interaction advances `_qvar2` and emits the native prompt without consuming
  Tavern Sign; the second consumes it and spawns Harlequin Crest at the native
  reward position.

- Schema 1.353 extends the generated unique-encounter boundary to the base
  pack. Gharbad, Zhar, Warlord, and Lazarus now have validated native-AI
  selector declarations; static activation defers unmatched selectors, and
  the planner binds them only when the matching unique encounter materializes.

- Schema 1.354 adds `quest_dialogue_stages.tsv`, a reusable server-owned
  contract for user-visible unique-monster dialogue messages and monotonic
  `_qvar1`/`_qvar2` transitions. Base Gharbad now routes all four authored
  dialogue stages through `TalkToQuestRequested`; its random item reward and
  quest-specific map mutations remain explicit native fallbacks until those
  effects have their own content contracts.

- Schema 1.355 extends `quest_dialogue_stages.tsv` with validated item gates
  and optional quest completion. The shared Ogden's Sign/Snotspill quest now
  routes Snotspill's banner dialogue and Tavern Sign hand-in through
  `TalkToQuestRequested`; the Banner set-piece map mutation remains native
  until authored map-state operations are available.

- Schema 1.356 moves Warlord's two authored dialogue transitions into the
  staged dialogue catalog. Messages and monotonic `_qvar1` transitions through
  values `2` and `3` are server-owned; the native Warlord AI/presentation
  fallback remains until encounter-state projection is externalized.

- Schema 1.357 moves the text for authored Gharbad, Snotspill, and Warlord
  messages into validated `dialogue_messages.tsv` content. The server projects
  that text on `DialogueEvent.text`; clients retain the numeric identity
  fallback when a legacy pack has no message catalog.

- Schema 1.358 adds a validated staged-dialogue reward contract. Base Gharbad's
  second authored stage now generates one deterministic native-quality random
  unique-monster drop from the active item catalog and places it at the
  authored relative position; his set-piece map mutations remain a separate
  native fallback.

- Schema 1.352 removes the remaining static-entity dependency from authored
  unique-monster identities. `unique_monsters.tsv` can now select exactly one
  unique encounter by validated monster ID and native AI, preserving the
  identity across generated placement and the existing snapshot/protocol
  boundary. The spawn planner now applies selector rows as generated unique
  encounters materialize, while unrelated levels defer their selectors. The
  Hellfire Defiler definition exercises this path.

- Base Magic Rock and Anvil of Fury activation conversations now use authored
  quest/towner rows. Their server-owned visited-level OR-gates reproduce the
  native Blacksmith checks, while the existing validated item hand-ins and
  Empyrean Band/Griswold's Edge rewards remain in the quest catalog.

- Nova Ball now preserves the native shrine collision exception: regular
  missile-blocking objects terminate a ball, while a validated shrine on its
  target tile lets the ball continue. The optional object capability is carried
  through snapshots, protocol, saves, and canonical hashing; regular breakable
  blockers are destroyed before the ball is removed. The Release Diablo suite
  passes all 1,388 tests and the Release server suite passes all 381 tests.

- Fireball now stops on native missile-blocking geometry, destroys a hit
  breakable object, and starts its authoritative splash at the blocker tile;
  the behavior is covered by regular and breakable-object tests.

- Player- and monster-owned arrows now preserve native breakable-object
  destruction when they collide with a missile-blocking object before removal.

- Player-owned arrows now resolve the first living combatant encountered on
  each traversed tile, matching native `ProcessArrow` path collision instead
  of requiring the declared endpoint target to remain the first occupant.

- Player-owned arrows now require the declared endpoint target to still occupy
  the endpoint tile at impact; targets that leave the native travel path are
  missed rather than resolved by stale entity identity.

- Hellfire `RandomArrowVelocity` is now server-owned: validated equipped bows
  capture native 16–47 pixel speed, persist the fractional travel remainder,
  and retain deterministic multi-tile collision timing through snapshots.

- Player-owned arrows now carry the native `AddArrow` class and Hellfire
  attack-speed modifiers in their persisted pixel velocity. The compact replay
  fixture and both C++/C# runners retain the same fractional cadence and
  checkpoint hashes.

- Hellfire player-versus-player arrows now retain the native blocked-missile
  impact phase and consume the native rotation RNG draw instead of disappearing
  immediately on a shield block.

- The shared C++/C# replay gate now includes a deterministic two-participant
  Firebolt cast, covering server-owned spell damage, player hit admission, and
  the resumable projectile checkpoint lifecycle.

- Hellfire Firebolt shield blocks now preserve the native player block result,
  consume the variant rotation draw, and enter the authoritative impact phase;
  a shared replay checkpoints spawn, impact, and removal.

- Fireball now resolves direct and impact-phase player collisions through the
  same authoritative player-target boundary as native `Plr2PlrMHit`; the
  shared replay covers nonlethal Q6 damage, splash timing, and projectile
  removal in both C++ and C#.

- Hellfire Fireball shield blocks now retain the native impact phase, consume
  the variant rotation draw, and still resolve the later splash against the
  living participant through the shared C++/C# replay.

- Bone Spirit player hits now retain the native current-life divisor followed
  by the non-arrow half-damage step; a shared replay covers self-damage, hit
  RNG, impact lifetime, and complete participant-state parity.

- Bone Spirit now preserves native nearest-monster homing and resistant-monster
  damage, including monster regeneration and the retained impact phase; a
  shared replay covers the C++/C# checkpoint parity.

- Secondary replay participants now route deterministic melee attacks through
  the same authoritative combat boundary as the primary participant; a shared
  replay covers participant identity, target admission, and unarmed damage.

- Seeded MonsterArrow projectiles now advance through native travel, player
  impact damage, and the retained impact phase in both replay runners; a shared
  fixture checks the three-tick lifecycle and canonical checkpoint hashes. The
  managed replay suite passes locally; native GTest source coverage is added but
  awaits a local C++ toolchain run.

- Multiplayer MonsterArrow replay state now routes a seeded impact to the
  secondary participant and hashes both participants, preserving the one
  server-owned projectile lifecycle across session identities.

- Autonomous GoatRanged replay admission now selects the nearest eligible
  participant with stable entity-ID tie-breaking, emits a server-owned
  MonsterArrow from validated missile metadata, and shares travel/impact/removal
  checkpoints with the native compact runner.

- Autonomous SkeletonRanged replay admission now preserves its clear-lane
  attack chance and long-range MonsterArrow travel/impact/removal checkpoints
  across the managed and native compact runners.

- Autonomous Succubus replay admission now emits the validated BloodStar
  missile with native velocity, lifetime, runtime-state, deterministic hit/block
  RNG, Q6 damage, impact-phase, and secondary-player block-animation checkpoints
  in both compact runners.

- Lich-family replay admission now emits validated MonsterMagic projectiles with
  native speed, deterministic hit RNG, Q6 damage, and impact-phase checkpoints
  in both compact runners.

- Lich-family MonsterMagic misses now preserve the living player's state and
  the projectile's travel phase while retaining the failed occupant identity;
  the shared replay checkpoints this native/C# collision bookkeeping.

- Lich-family MonsterMagic admission now selects and impacts a living
  secondary participant when the primary is unavailable, preserving target
  identity, Q6 damage, and complete multiplayer checkpoint state.

- The shared protocol now projects an explicit `MonsterMagic` projectile
  presentation family, and Godot renders that server-owned family without
  inferring gameplay behavior from a generic fallback.

- The shared protocol and Godot client now classify server-owned `HorkSpawn`
  summon controllers explicitly, while older peers retain behavior fallback.

- Apocalypse behavior `32` now projects the server-owned fire controller as
  `Fire`, while persistent behavior `33` booms use the larger `FireImpact`
  family; older peers retain the same behavior fallback.

- Firebolt behavior `2` now shares the server-owned `Fire`/`FireImpact`
  projection with Fireball behavior `16`, including the legacy fallback.

- Charged Bolt behavior `1` now projects the server-owned `Lightning` family,
  including the legacy behavior fallback.

- Elemental behavior `35` now projects the server-owned `Fire` family across
  travel and burst phases, including the legacy behavior fallback.

- Guardian behavior `36` now projects an additive server-owned `Guardian`
  turret family, while older peers retain behavior-based fallback.

- Flash behaviors `5`, `6`, and `7` now project an additive server-owned
  `Flash` family for the native two-layer visual, while older peers retain
  behavior-based fallback.

- Rage behavior `34` now projects an additive server-owned `HIDDEN` family so
  current Godot clients suppress the invisible controller instead of drawing a
  generic projectile; older peers retain behavior-based fallback.

- World-item snapshots now project additive server-owned `GOLD` and `EAR`
  families for currency and dynamic PvP-ear presentation, while zero or unknown
  metadata retains the generic client path.

- Persistent Acid puddles now preserve native breakable-object destruction on
  their occupied tile without ending the puddle lifetime.

- Compatibility generic projectiles now preserve native breakable-object
  destruction when a missile-blocking tile ends their travel.

- Apocalypse booms now preserve native breakable-object destruction while
  retaining their remaining persistent lifetime.

- The Godot world presentation now renders server-owned monster health bars
  from authoritative `MonsterSnapshot` vitality, preferring exact Q6 values and
  falling back to rounded legacy fields without moving combat state into the
  client.

- Godot also renders local and remote player life/mana bars from authoritative
  `PlayerSnapshot` resources; the client clamps presentation values but never
  predicts or mutates those resources.

- Player, monster, world-item, object, projectile, and corpse IDs now share a
  validated server-owned namespace at the mapper and Godot boundaries. Entity
  allocators skip occupied portal/corpse/player IDs, while identity-neutral
  version-1 saves preserve their zero player ID until import.

- All 37 checked-in replay fixtures now bind the active
  content-manifest identities; the full 326-test server assembly passes again.

- Host shutdown now disposes the active authoritative adapter as well as
  drained retired generations, closing the ruleset lifecycle ownership gap.

- Ranged monster missile post-attack delays are now validated content fields
  (`ranged_delay_base` and `ranged_delay_random_range`) with native-compatible
  fallback behavior for older packs; the shipped base and Hellfire mappings
  carry the active values and replay identities have been refreshed.

- RangedAvoidance recovery delays are now validated missile-content fields
  (`ranged_avoidance_delay_base` and `ranged_avoidance_delay_random_range`),
  retaining the native fallback for older packs and carrying the active values
  in the shipped base mappings.

- Hellfire HorkDemon recovery delays are now validated `HorkSpawn` missile
  fields (`hork_demon_delay_base` and `hork_demon_delay_random_range`), with
  native-compatible fallback behavior for older packs.

- Native monster direction is now authoritative state carried through internal
  snapshots, the versioned protocol, saves, and canonical hashing. Hork Spawn
  uses the monster's current direction for its adjacent admission and carries
  that direction into the summoned monster instead of deriving either value
  from the victim.

- MonsterArrow projectiles now retain dynamic monster damage bounds through
  the authoritative snapshot, save, protocol, and hash boundaries and roll
  physical damage at impact, while fixed `projectile_damage` mappings remain
  unchanged.

- Hork Spawn now checks its current tile against living players before each
  movement/expiry decision, preserving native non-arrow `PlayerMHit` hit,
  block, minimum-damage, and RNG behavior before the summon path continues.

- Hork Spawn now also breaks a breakable missile-blocking object on its current
  tile before taking the native expiry/summon path.

- Hork Spawn now retains a newly blocked destination for the following tick,
  matching native movement before the current-tile expiry check.

- Snake adjacent and distant delay bands are now validated `SnakeCharge`
  missile fields, preserving native intelligence reduction and deterministic
  zero-clamped timing while retaining fallback behavior for older packs.

- Snake distant-delay admission chance is now validated `SnakeCharge` missile
  content (`snake_distant_delay_chance_base` and
  `snake_distant_delay_chance_intelligence_scale`), preserving the native
  35-minus-two-times-intelligence band with fallback behavior for older packs.

- Snake adjacent attack admission chance is now validated `SnakeCharge` missile
  content (`snake_adjacent_attack_chance_base` and
  `snake_adjacent_attack_chance_intelligence_scale`), preserving the native
  20-plus-intelligence band with fallback behavior for older packs.

- Bat long-idle and fresh-mode movement admission is now validated through
  `bat_movement_chance_base`, `bat_movement_chance_intelligence_scale`, and
  `bat_movement_fresh_chance_bonus` monster-type fields, preserving the native
  13/1/50 bands with fallback behavior for older packs.

- Counselor ranged-admission chance is now validated through
  `counselor_attack_chance_base` and
  `counselor_attack_chance_intelligence_scale` monster-type fields, preserving
  the native capped 50/5 band with fallback behavior for older packs.

- Shared AiAvoidance fresh-mode movement admission is now validated through
  the `ai_avoidance_movement_fresh_chance_bonus` monster-type field, preserving
  the native 50-point bonus with fallback behavior for older packs.

- Fat/Overlord fresh-mode movement admission is now validated through the
  `fat_movement_fresh_chance_bonus` monster-type field, preserving the native
  50-point bonus with fallback behavior for older packs.

- Rhino ordinary movement admission is now validated through the
  `rhino_movement_chance_base`, `rhino_movement_chance_intelligence_scale`, and
  `rhino_movement_fresh_chance_bonus` monster-type fields, preserving the native
  33/2/50 bands with fallback behavior for older packs.

- Rhino far-distance Move-goal admission now uses the same validated
  `rhino_movement_goal_minimum_distance` and
  `rhino_movement_goal_chance_modulo` gate in charge-miss and blocked-lane
  fallback branches, preserving the native 5/4 fallback for older packs.

- Familiar Lightning damage now uses the optional
  `projectile_damage_random_range` missile field for its native
  `GenerateRnd` upper bound, preserving the native range of ten for older
  mappings.

- Familiar post-melee Lightning now honors the existing optional
  `projectile_damage` missile field before applying its random-damage range,
  preserving the legacy random roll when older mappings omit a fixed value.

- The Hellfire pack now activates Familiar's validated `Bat`/monster-43
  post-melee Lightning mapping, so the shipped composition owns its native
  transport, eight-tick impact phase, and `GenerateRnd(10)` damage bound.

- AiRangedAvoidance adjacent attack admission now uses optional missile fields
  for its validated base, intelligence scale, and divisor, preserving the
  native 6,000/1,000-per-intelligence band and Acid half-band for older
  mappings.

- Familiar post-melee Lightning dispatch now uses an explicit validated
  `post_melee_missile` mapping flag, while older mappings retain the
  `MonsterMagic`/`Lightning` compatibility tuple.

- Mapped Bat and Snake charge projectiles now consume validated missile
  `damage_type` content, while older mappings retain the native Physical
  fallback.

- Mapped Bat and Snake charge projectiles now consume their validated missile
  `spell_id`, while older mappings retain the native spell identity `1`.

- Snake charge collision damage now applies the mapped missile `damage_type`
  through player resistance, while older mappings retain native Physical
  damage behavior.

- Snake charge collisions now honor the existing optional `projectile_damage`
  missile field before applying resistance, while older mappings retain the
  native special-damage range.

- MonsterArrow impacts now enter the validated `projectile_impact_lifetime`
  phase before removal, retaining the native one-tick fallback for older rows.

- Server-owned `MonsterMagic` and `BloodStar` impacts now enter the validated
  `projectile_impact_lifetime` phase, while player-owned spell projectiles and
  native expiry behavior remain unchanged.

- `SnakeCharge` impacts now enter the validated `projectile_impact_lifetime`
  phase after a successful player collision, while blocked, missed, and
  expired charges retain native removal behavior.

- MonsterArrow travel now consumes the validated `projectile_speed` as its
  per-tick tile-step count, while omitted values retain the native speed-one
  fallback and lifetime behavior.

- Mega Inferno segments now consume the mapped `projectile_damage` override,
  while older mappings retain the native per-segment monster-damage roll.

- MonsterArrow impacts now consume mapped `projectile_damage` after the native
  ranged hit roll, while older mappings retain native damage derivation.

- Validated `MonsterArrow` mappings now admit a clear native `AiRanged` lane at
  adjacent distance, preserving the native arrow attack instead of producing a
  no-op; mapped ranged-only monsters still suppress adjacent melee fallback.

- Mega post-action delays are now validated `InfernoController` missile
  fields (`mega_delay_base` and `mega_delay_random_range`), retaining
  native-compatible fallback behavior for older packs.

- SkeletonKing movement delays are now validated monster-type fields
  (`skeleton_king_delay_base` and `skeleton_king_delay_random_range`),
  retaining native-compatible fallback behavior for older packs.

- Rhino recovery delays are now validated monster-type fields
  (`rhino_delay_base` and `rhino_delay_random_range`), retaining
  native-compatible fallback behavior for older packs.

- SkeletonMelee movement and failed-adjacent-attack delay bands are now
  validated monster-type fields, preserving native intelligence reduction
  and deterministic exclusive random ranges for older packs.

- Shared AiAvoidance failed-RoundWalk delays are now validated monster-type
  fields for GoatMelee and Gargoyle, retaining the native exclusive random
  range through compatibility fallback for older packs.

- Counselor failed-ranged-admission and successful-attack delays are now
  separate validated monster-type fields, preserving their distinct native
  intelligence-scaled and fixed-base formulas.

- Counselor failed-ranged-admission movement chance is now a validated
  `counselor_move_chance_percent` monster-type field, preserving the native
  30-percent branch with compatibility fallback for older packs.

- Healthy Counselor-family monsters now preserve native adjacent Flash
  attacks: the server emits both stationary Flash layers with
  `2 * monster_level` magic damage and keeps the native Counselor delay,
  instead of falling through to generic melee.

- Counselor Charged Bolt mappings now emit the native three independent
  `MonsterChargedBolt` projectiles with their native fixed 15-point payload;
  validated `projectile_damage` values remain authoritative overrides.

- Counselor `MonsterChargedBolt` projectiles now retain their typed Lightning
  presentation in the server-to-Godot mapping instead of falling back to a
  generic projectile visual.

- Counselor's native admission now preserves its initial `GenerateRnd(100)`
  boundary, and mapped Firebolt/MonsterMagic projectiles capture the inclusive
  monster damage roll at launch, while fixed payload overrides consume no roll.

- Mapped monster Fireball collisions now roll the native monster damage range
  at each direct or splash impact, while validated `projectile_damage` values
  remain fixed overrides.

- Mapped `MagmaBall` projectiles now preserve the native three-velocity-quanta
  launch offset before their first authoritative simulation tick.

- Storm/ThinLightning controller ticks now roll `2 * RandomIntBetween` from
  the monster damage range before creating each lightning segment.

- Mega delegated Skeleton movement-delay admission is now validated
  `mega_delay_chance_base` and `mega_delay_chance_intelligence_scale`
  monster-type data, preserving the native 35-minus-four-times-intelligence
  band with compatibility fallback for older packs.

- Mega intermediate-distance RandomWalk admission is now validated
  `mega_intermediate_movement_chance_base` and
  `mega_intermediate_movement_chance_intelligence_scale` monster-type data,
  preserving the native 50-plus-ten-times-intelligence band with its 100
  percent cap and compatibility fallback for older packs.

- SkeletonKing RoundWalk admission is now a validated
  `skeleton_king_round_walk_chance_modulo` monster-type field, preserving the
  native modulo-four branch with compatibility fallback for older packs.

- Hork Demon far-distance RoundWalk admission is now a validated
  `hork_demon_round_walk_chance_modulo` monster-type field, preserving the
  native modulo-four branch with compatibility fallback for older packs.

- Shared AiAvoidance Move-goal admission is now validated through
  `ai_avoidance_movement_minimum_distance` and
  `ai_avoidance_movement_chance_modulo` monster-type fields, preserving the
  native minimum-four/modulo-four gate with compatibility fallback for older
  packs.

- Rhino far-distance movement-goal admission is now validated through
  `rhino_movement_goal_minimum_distance` and
  `rhino_movement_goal_chance_modulo` monster-type fields, preserving the
  native minimum-five/modulo-four gate with compatibility fallback for older
  packs.

- AiRangedAvoidance far-distance Move-goal admission is now validated through
  the `ranged_avoidance_movement_chance_modulo` missile field, preserving the
  native modulo-four gate (Acid modulo-eight variant) with compatibility
  fallback for older packs.

- AiRangedAvoidance ordinary RandomWalk admission is now validated through
  `ranged_avoidance_movement_chance_base`,
  `ranged_avoidance_movement_chance_intelligence_scale`, and
  `ranged_avoidance_movement_fresh_chance_bonus` missile fields, preserving the
  native 50/10/30 movement band with compatibility fallback for older packs.

- SkeletonRanged fresh-mode movement admission is now validated through the
  `skeleton_ranged_movement_fresh_chance_bonus` monster-type field, preserving
  the native 50-percent bonus with compatibility fallback for older packs.

- SkeletonKing movement-admission chance is now a validated monster-type
  field, preserving the native intelligence-scaled movement band.

- The client protocol model rejects impossible player life/mana and monster
  vitality bounds before publishing a snapshot to presentation; visual
  clamping remains defensive and never replaces authoritative validation.

- Combat content activation now validates the configured PvP-ear item against
  the active item catalog before constructing sessions, rejecting unknown or
  mismatched native ear projections at the data boundary.

- Stable spell and ability symbols in `content_ids.tsv` are now
  cross-validated against their active catalogs before IDs are published to a
  session.

- World-object rows now optionally resolve stable `object_symbol` values
  through `content_ids.tsv`; activation validates those identities against the
  composed object catalog while numeric-only rows remain compatible.

- Store stock and the Adria service identity now optionally resolve stable
  `store_symbol`/`adria_store_symbol` values; activation validates the store
  namespace against stock and the reserved service identity while numeric-only
  rows remain compatible.

- Static portal rows now optionally resolve stable `portal_symbol` values;
  activation cross-checks them against the retained portal IDs while numeric
  routes remain compatible.

- Static warp rows now optionally resolve stable `warp_symbol` values;
  activation cross-checks them against the retained warp IDs while numeric
  routes remain compatible.

- Quest types, quest state rows, and single-player quest pools now optionally
  resolve stable `quest_symbol` values through `content_ids.tsv`; activated
  rows reject numeric/symbol mismatches while numeric-only legacy rows remain
  compatible.

- Warp gates, linked objects, item quest effects, and follow-on quest activation
  now resolve optional stable quest symbols through the same namespace, with
  activation-time mismatch checks and numeric-only fallback.

- Starting loadout item rows now resolve optional `item_symbol` values through
  the stable item namespace. Activated packs cross-check numeric and symbolic
  identities, validate the active item entries, and retain numeric-only fallback.

- Store stock, unique-item base references, quest completion rewards, and item
  quest effects now resolve optional stable item symbols with numeric-only
  fallback and activation-time mismatch checks.

- Item combinations, catalog-backed monster drops, and the PvP-ear combat rule
  now resolve optional stable item symbols with the same validation guarantees.

- Every shipped base and Hellfire normalized item definition now carries a
  unique stable `item_symbol`; the active item namespace is checked for full
  definition coverage while numeric IDs remain the compatibility projection.

- Shipped normalized unique-item rows now carry stable `base_item_symbol`
  links. Activation cross-checks those symbols against retained numeric base
  IDs and the active item catalog, while numeric-only external packs remain
  compatible.

- Quest-item completion objects now accept `effect_item_symbol` links through
  the stable item namespace. Activation cross-checks the symbol against the
  retained numeric `effect_amount`; non-item object effects remain numeric.

- Unique-item definitions now carry stable `unique_symbol` identities in the
  `unique` namespace. Activation cross-checks each symbol against its numeric
  ID and validates the complete namespace before publication.

- Optional store unique-item stock and monster unique-drop references now
  resolve `unique_item_symbol`/`drop_unique_item_symbol` through the stable
  unique namespace, preserving numeric-only compatibility.

- Class loadouts now resolve `ability_spell_symbol` through the stable ability
  namespace and `starting_spell_symbol` through the spell namespace. Activated
  packs reject symbol/numeric mismatches, while legacy symbols fall back to the
  old spell namespace.

- Monster missile mappings now optionally resolve `spell_symbol` through the
  stable spell namespace. Activated rows reject symbol/numeric mismatches,
  while older rows retain numeric-ID fallback.

- Normalized item spell-source mappings now optionally resolve `spell_symbol`
  through the stable spell namespace. Activated rows reject symbol/numeric
  mismatches, while older rows retain numeric-ID fallback.

- Shipped spell definitions now optionally resolve `spell_symbol` through the
  stable spell namespace. Activated rows reject symbol/numeric mismatches,
  while older rows retain numeric-ID fallback.

- Spell-staff pricing rows now optionally resolve `spell_symbol` through the
  stable spell namespace. Activated rows reject symbol/numeric mismatches,
  while older pricing tables retain numeric-ID fallback.

- Jester dispatch lists now optionally resolve `jester_spell_symbols` through
  the stable spell namespace. Activated lists reject symbol/numeric mismatches,
  while older spell rows retain numeric-ID fallback.

- Direct store spell rows now optionally resolve `spell_symbol` through the
  stable spell namespace. Activated rows reject symbol/numeric mismatches,
  while older store tables retain numeric-ID fallback.

- Stable level and portal symbols are now cross-validated against active world
  and portal catalogs during content activation, without removing legacy
  no-catalog fallbacks.

- The town level identity used by Town Portal and Warp is now externalized in
  validated `world_rules.tsv` content. Active packs must point to a level in
  the externalized world; callers without world-rule content retain native
  level `0` compatibility.

- Adria's service-only store identity is now externalized in the same
  validated `world_rules.tsv` contract. Activation rejects collisions with
  stock vendor IDs, while compatibility callers retain native store `10`.

- Town Portal destination coordinates and slot layout are now optionally owned
  by validated `world_rules.tsv` content. Packs without the geometry columns
  retain their existing spell-row destination behavior. When present, every
  configured slot is checked against the activated town level for bounds and
  walkability before content activation completes.

- The Godot client now classifies server-owned projectile behavior IDs through a
  shared protocol-side presentation mapper and renders distinct elemental,
  spell, arrow, beam, impact, and movement-transition families without moving
  simulation logic into the client. Teleport and Phasing controllers remain
  server-owned while their authoritative positions are now visible to players.

- Projectile snapshots now carry additive server-owned `presentation_kind`
  metadata. Godot prefers that projection and retains behavior/phase mapping only
  as the compatibility fallback for older peers.

- Godot now converts authoritative damage, healing, and experience events into
  typed transient floating feedback positioned from authoritative entity state;
  the native Lua floating-number mods remain compatibility-only.

- The Godot client also maps authoritative status-effect identities to typed
  overlays: Infravision, Search, and Etherealize now have distinct player
  presentation while the server retains all timing, stacking, and admission
  rules.

- Partial external Gargoyle AI tuning now retains the native healing divisor
  offset when its optional random-offset column is omitted; validated content
  cannot reach the previous zero-divisor path.

- Acid puddle impact duration now consumes validated `puddle_impact_lifetime`
  metadata from `monster_missiles.tsv`; omitted values retain the native
  two-tick impact phase.

- Mapped monster projectile impact duration now consumes validated
  `projectile_impact_lifetime` metadata from `monster_missiles.tsv`; omitted
  values retain the native one-tick impact phase across arrows, charges, magic
  missiles, and Hork Spawn.

- Validated MonsterArrow mappings now consume their existing `projectile_speed`
  metadata for travel and `projectile_lifetime` when present; omitted values
  retain the native speed-one and distance-based lifetime fallbacks. Their
  optional `damage_type` selects the matching player-resistance channel, while
  omitted values retain physical arrows.

- Validated BloodStar mappings now consume their existing `projectile_speed`,
  `projectile_lifetime`, optional `damage_type`, and optional
  `projectile_damage` metadata before falling back to mapped-spell transport
  values and native monster damage, keeping older rows compatible.

- The shipped Succubus and Lazarus BloodStar rows now explicitly carry the
  native speed-16, lifetime-256 transport values, removing that fallback from
  the active base pack while preserving compatibility for older packs.

- The shipped Acid and AcidUnique mappings now explicitly carry native
  projectile speed 16; their validated lifetime and puddle terms remain
  authoritative, while older rows retain compatibility fallbacks.

- Reusable bounded grid navigation now lives in `Devilution.Engine` behind
  caller-owned walkability and occupancy predicates. The Diablo module retains
  its level and entity semantics while the deterministic breadth-first order
  and depth boundary are engine-owned and independently tested.

- Nova and Immolation controllers now consume their validated
  `nova_projectile_count` and `immolation_projectile_count` fields in native
  radial emission order; the shipped rows retain the native 36-projectile
  fan-out.

- Reflect retaliation now consumes validated minimum and maximum damage
  percentages from the Reflect row in `spells.tsv`; older spell schemas retain
  the native 20–30% compatibility roll.

- Golem summon health now consumes a validated per-effective-level Q6 base
  term from the Golem row in `spells.tsv`; older spell schemas retain the
  native 320 Q6 per-level health term.

- Golem summon health, damage, and armor now consume validated formula terms
  from the Golem spell row in `spells.tsv`; older spell schemas retain the
  native health 2/3 terms, damage 2/4/8 terms, and armor class 25.

- Melee Quick/Fast/Faster/Fastest attack timing now consumes validated
  first-frame and subsequent-frame tier metadata from `combat.tsv`; older
  combat schemas retain the native 1/0, 2/1, 3/2, and 4/2 compatibility
  defaults.

- Equipment Fast/Faster/Fastest Hit Recovery timing now consumes validated
  ordered tier metadata from `combat.tsv`; older combat schemas retain the
  native 1/2/3 compatibility defaults.

- Equipment-driven FastBlock timing now consumes validated
  `equipment_fast_block_frame_skip` combat metadata; older combat schemas
  retain the native two-frame compatibility default.

- Class FastBlock staff timing now consumes validated
  `fast_block_frame_skip` metadata from `classes.tsv`; older class schemas
  retain the native two-frame compatibility default.

- Player-versus-player block chance now consumes validated
  `pvp_block_level_scale` combat metadata; older combat schemas retain the
  native level-difference scale of two.

- Class NaturalResistance and HeavyHitThreshold now consume validated
  per-level resistance and threshold-bonus percentages from `classes.tsv`;
  older class schemas retain the native 100% and 25% compatibility defaults.

- Class IronSkin now consumes validated armor-divisor metadata from
  `classes.tsv`; older class schemas retain the native level-divided-by-four
  compatibility default.

- Class Critical Strike now consumes validated per-level chance and damage
  multiplier metadata from `classes.tsv`; older class schemas retain the native
  1% per character level and 2× compatibility defaults.

- Monster arrow and snake-charge controllers now dispatch from validated
  missile projectile behavior rather than requiring `GoatRanged`/`Snake`
  labels or `Arrow`/`Rhino` missile names; legacy `Targeted` arrow rows remain
  accepted, mapped arrow rows suppress adjacent melee fallback, and controller
  state stays server-owned.

- Blood Star dispatch now uses validated `BloodStar` missile behavior
  independent of the `Succubus`/`LazarusSuccubus` label when a missile catalog
  is active; the no-catalog legacy path retains native-label compatibility.

- Fat and GoatMelee special-attack selection now honors complete validated
  attack tuples independently of the runtime native-AI label; rows without
  migrated tuples retain their native compatibility paths.

- Fallen fear radius and countdown behavior now use complete validated tuning
  tuples independent of the row's native-AI label; rows without migrated tuning
  retain the native-name compatibility path, while persisted fear state also
  advances and remains snapshot/save compatible for alternate labels.

- Scavenger corpse-healing and search behavior now use validated tuning
  independent of the row's native-AI label; rows without migrated tuning retain
  the native-name compatibility path, while persisted corpse and healing state
  advances for any validated label and remains snapshot/save compatible. A
  standalone validated wounded-health threshold composes the native
  10/4/8/1/75 healing defaults instead of being ignored.

- Scavenger corpse selection now preserves native coin-selected square-scan
  order and chooses the first visible corpse, rather than nearest-corpse
  ordering; the selected identity remains persisted for deterministic pursuit.

- Sneak visibility and hit-retreat behavior now use complete validated tuning
  tuples independent of the row's native-AI label; rows without migrated tuning
  retain the native-name compatibility path, while hidden/retreat state also
  advances and remains snapshot/save compatible for alternate labels.

- Bat adjacent attack and retreat-duration behavior now use validated tuning
  independent of the row's native-AI label; rows without migrated tuning retain
  the native-name compatibility path, while retreat ticks/direction remain
  snapshot/save compatible and advance for any validated label carrying
  migrated Bat tuning. A standalone validated retreat duration composes the
  native 8/4 attack defaults instead of being ignored.

- Bat movement now preserves native `var1`/`var2` admission state: long-idle
  ticks use the `intelligence + 13` band, fresh movement modes use the
  `intelligence + 63` band, and both counters cross snapshots, saves, protocol,
  and canonical hashes. Ordinary Bat identities reach this movement boundary
  even when no optional charge-missile mapping is present.

- Snake lateral drift now preserves native `RandomWalk2` fallback order after
  a blocked drift candidate; the server checks only the original direction and
  its two lateral candidates, with no extra ±90° replacement.

- Gargoyle safety, wounded-health, and healing-formula behavior now use
  validated tuning independent of the row's native-AI label; rows without
  migrated tuning retain the native-name compatibility path, while the
  persisted special lock remains snapshot/save compatible for any validated
  label carrying migrated Gargoyle tuning. Standalone healing fields compose
  the native 2/1 safety boundary and 16/5/4 healing-formula defaults.

- Butcher pursuit now consults the active validated monster-type identity when
  a catalog is present, while complete alternate AI tuning retains precedence;
  direct legacy targets retain the native-name fallback.

- Mapped monster missile dispatch now resolves the active validated
  monster-type identity before the runtime-label compatibility fallback, so
  content can change identity without moving projectile ownership into the
  engine.

- Native `AiRangedAvoidance` adjacent melee now uses its server-owned
  `1000 * (intelligence + 6)` admission band (halved for Acid) before shared
  melee resolution, preserving the native failed-roll defer path. Failed or
  blocked ranged rolls now use deterministic lateral round-walk movement
  instead of generic pursuit.

- Native `AiRangedAvoidance` round-walk movement now follows the native
  candidate order: 90-degree turn, 45-degree turn, straight, then the
  opposite-side `RandomWalk` fallback.

- Native `AiRangedAvoidance` now selects its deterministic movement fallback
  by branch: the far-distance one-in-four (one-in-eight Acid) Move admission
  uses `RoundWalk`, while the ordinary failed ranged admission uses
  `RandomWalk`.

- Native `AiRangedAvoidance` `goalVar1`/`goalVar2` movement state is now
  server-owned and persisted through snapshots, optional protocol fields
  58-59, saves, and the canonical state hash.

- Native `AiRangedAvoidance` ordinary `RandomWalk` movement now preserves its
  `var1`/`var2`-compatible mode counter. Long-idle, fresh-mode, and middle-band
  admissions are server-owned and persist through snapshots, optional protocol
  fields 66-67, saves, and canonical hashes; far-distance `RoundWalk` goals
  remain on fields 58-59.

- Native `AiRangedAvoidance` now flips persisted `goalVar2` when its primary
  `RoundWalk` candidates are blocked, matching the native opposite-side
  `RandomWalk` fallback on the next authoritative state.

- Native Rhino `goalVar1`/`goalVar2` movement state is now server-owned and
  persisted through snapshots, optional protocol fields 60-61, saves, and the
  canonical state hash; Rhino's far-distance Move goal continues across ticks.

- Shared native `AiAvoidance` `goalVar1`/`goalVar2` movement state is now
  server-owned for GoatMelee and Gargoyle and persists through snapshots,
  optional protocol fields 62-63, saves, and the canonical state hash.

- Shared native `AiAvoidance` ordinary movement now preserves the native
  `var1`/`var2`-compatible mode counter for GoatMelee and Gargoyle. Long-idle,
  fresh-mode, and middle-band admissions are server-owned and persist through
  snapshots, optional protocol fields 64-65, saves, and canonical hashes.

- Native Skeleton/Fallen `AiDelay` is now a server-owned countdown after
  failed movement or adjacent attack admissions. The delay persists through
  snapshots, optional protocol field 68, saves, and canonical hashes, so the
  server does not reroll SkeletonAi while native delay mode is active.

- Native `SkeletonBowAi` close-range retreat now preserves its `var1`/`var2`
  movement counter. Long-idle and fresh-mode admissions are server-owned and
  persist through snapshots, optional protocol fields 69-70, saves, and
  canonical hashes; blocked arrow lanes remain server-owned no-ops.

- Native Fat/Overlord movement now preserves its `var1`/`var2`-compatible
  counter. Long-idle and fresh-mode `RandomWalk` admissions are server-owned
  and persist through snapshots, optional protocol fields 71-72, saves, and
  canonical hashes.

- Native Rhino ordinary movement now preserves its `var1`/`var2`-compatible
  counter and `AiDelay` countdown. Validated 33/2/50 long-idle/fresh-mode
  bands (with the native fallback) and failed admissions remain server-owned
  through snapshots, optional protocol fields 73-75, saves, and canonical
  hashes; far-distance Move goals remain in fields 60-61.

- Native Leoric/Skeleton King `AiDelay` is now a server-owned countdown after
  failed movement admissions. The countdown persists through snapshots,
  optional protocol field 76, saves, and canonical hashes; the server does not
  reroll LeoricAi while native delay mode is active.

- Native HorkDemonAi ordinary movement `AiDelay` is now a server-owned
  countdown after failed movement admissions. The countdown persists through
  snapshots, optional protocol field 77, saves, and canonical hashes; the
  server does not reroll HorkDemonAi while native delay mode is active.

- Native `AiRangedAvoidance` no-action ticks now preserve the native
  `AiDelay(GenerateRnd(10) + 5)` countdown. The server-owned delay persists
  through snapshots, optional protocol field 78, saves, and canonical hashes;
  successful movement and ranged admissions remain unaffected.

- Native Snake charge mappings now preserve both non-charge and adjacent
  attack `AiDelay` countdowns. The server-owned delay persists through
  snapshots, optional protocol field 79, saves, and canonical hashes; native
  lateral drift and admitted adjacent attacks remain server-owned.

- Native Counselor non-adjacent failed ranged/movement admissions now preserve
  the native `AiDelay` countdown, and successful mapped ranged actions now start
  the native post-action `GenerateRnd(10) + 5` delay. The server-owned delay
  persists through snapshots, optional protocol field 80, saves, and canonical
  hashes; movement and wounded retreat remain server-owned.

- Shared native `AiAvoidance` failed `RoundWalk` admissions now preserve the
  native `AiDelay(GenerateRnd(10) + 10)` countdown for GoatMelee and Gargoyle.
  The server-owned delay persists through snapshots, optional protocol field
  81, saves, and canonical hashes; the next tick decrements the delay without
  rerolling movement or attack admission.

- Native `AiRanged` post-attack delays now preserve the
  `AiDelay(GenerateRnd(20))` countdown for generic ranged mappings. The
  server-owned delay persists through snapshots, optional protocol field 82,
  saves, and canonical hashes; active projectiles no longer permit a second
  ranged admission while the native delay is pending.

- Native intermediate-distance `MegaAi` post-action delays now preserve the
  `AiDelay(GenerateRnd(10) + 5)` countdown in dedicated server-owned state,
  persisted through optional protocol field 83, snapshots, saves, and the
  canonical hash.

- Legacy native-AI fallback controllers now consult the active validated
  monster-type identity before the runtime-label compatibility path when no
  complete migrated tuning is present; alternate complete tuning retains
  precedence.

- SkeletonRanged retreat and clear-lane arrow admission now use complete
  validated tuning tuples independent of the row's native-AI label; rows
  without migrated tuning retain the native-name compatibility path, and the
  validated tuple suppresses adjacent melee fallback.

- Zombie action, random-direction, and movement-distance behavior now use
  complete validated tuning tuples independent of the row's native-AI label;
  rows without migrated tuning retain the native-name compatibility path.
  Successful action rolls in the intermediate distance band now also perform
  the native random walk instead of leaving the authoritative target idle.

- Snake charge mappings now retain the native non-charge drift branch: targets
  outside the charge boundary use deterministic lateral movement, while the
  existing generic navigation fallback remains available during native delay
  ticks.

- Fat/Overlord movement, adjacent attack, and special-band behavior now use
  complete validated tuning tuples independent of the row's native-AI label;
  rows without migrated tuning retain the native-name compatibility path.
  Admitted non-adjacent movement now uses the native `RandomWalk` candidate
  order before bounded navigation fallback.

- GoatMelee movement, adjacent attack, and wounded-special behavior now use
  complete validated tuning tuples independent of the row's native-AI label;
  rows without migrated tuning retain the native-name compatibility path.
  A standalone complete wounded-special tuple now composes the native 23/2
  attack-band defaults instead of being ignored. Non-adjacent movement now
  preserves the native one-in-four RoundWalk and ordinary RandomWalk branches
  through deterministic candidate order without bounded pursuit replacement.

- SkeletonMelee movement-delay and adjacent attack admission now use complete
  validated tuning tuples independent of the row's native-AI label; rows
  without migrated tuning retain the native-name compatibility path. Delay
  exits now use the native `RandomWalk` candidate order before bounded
  navigation fallback.

- Fallen normal-state AI now delegates to the same server-owned SkeletonAi
  movement and adjacent attack bands as native `FallenAi`; fear state retains
  precedence, and specialized validated controller tuples are not shadowed.

- Butcher movement now uses the native non-adjacent `RandomWalk` candidate order
  before bounded navigation fallback; validated identity precedence remains
  server-owned.

- Skeleton King non-adjacent movement now uses deterministic native
  `RoundWalk`/`RandomWalk` candidates after summon admission is evaluated;
  validated summon identity and server-owned summon precedence remain intact.

- Hork Demon non-adjacent movement now uses deterministic native
  `RoundWalk`/`RandomWalk` candidates after Hork Spawn admission is evaluated;
  validated spawn identity and server-owned summon precedence remain intact.

- Mega far-distance movement now delegates to the server-owned SkeletonAi
  `35 - 4 * intelligence` delay and deterministic `RandomWalk` boundary when
  Inferno is outside its validated maximum range.

- Mega's far-distance SkeletonAi delegation now persists the native
  `AiDelay(15 - 2 * intelligence + GenerateRnd(10))` countdown through the
  existing Skeleton delay state, snapshots, saves, and canonical hashes.

- Mega intermediate-distance Inferno misses now consume the native
  `2 * (5 * intelligence + 25)` RandomWalk fallback before generic pursuit;
  clear-lane and blocked-lane outcomes remain server-owned.

- Counselor wounded-adjacent retreat now enters a persisted four-attempt native
  `RandomWalk` state in the server before mapped ranged dispatch; retreat state
  is carried through snapshots, protocol saves, and the authoritative state
  hash, while the Godot client remains behavior-blind.

- Counselor mapped ranged rows now consume validated base and intelligence-scale
  admission terms, preserving the native 50/5 fallback and 30-percent
  `RoundWalk` branch; failed admissions defer on the server instead of
  generic-pursuing.

- Rhino charge admission is now selected by its complete validated tuning tuple
  independent of the row's native-AI label; rows without migrated tuning retain
  the native-name compatibility path.

- Rhino charge misses and blocked lanes now consume the native far-distance
  `RoundWalk` and ordinary `33 + 2 * intelligence` `RandomWalk` fallback before
  generic navigation.

- Mapped Gloom `BatCharge` misses and blocked lanes now retain the native
  `BatAi` movement branch: the server applies the standing-tick compatibility
  `63 + intelligence` admission and deterministic `RandomWalk` candidates
  before suppressing generic pursuit. Adjacent ticks remain available to the
  shared melee boundary.

- SkeletonRanged blocked arrow lanes now retain the native `SkeletonBowAi`
  defer at long range. The server does not replace a missing clear lane with
  generic pursuit; its validated close-range retreat branch remains evaluated
  before that no-op.

- GoatRanged blocked arrow lanes now retain the native `AiRanged` defer at long
  range. Its validated close-range avoidance branch remains evaluated first;
  a missing clear lane no longer delegates movement to generic navigation.

- Snake non-charge and adjacent attack `AiDelay` ticks now remain server-owned
  countdowns. The validated lateral drift and melee branches resume only after
  the native delay expires, and generic pursuit no longer replaces delayed
  ticks.

- Zombie action ticks now preserve the native blocked-walk no-op. When the
  validated action band succeeds but no native one-step candidate is available,
  the server does not replace that result with generic navigation.

- HorkDemon admitted movement now preserves the native blocked-candidate
  no-op. When both deterministic `RoundWalk` and `RandomWalk` orders are
  unavailable, the server leaves the monster stationary rather than navigating.

- Mega's validated far-distance Skeleton fallback now preserves its native
  blocked-`RandomWalk` no-op; no bounded generic path replaces an admitted but
  unavailable movement candidate.

- Skeleton King's validated `LeoricAi` movement now preserves its native
  blocked-`RandomWalk` no-op; no bounded generic path replaces an admitted but
  unavailable movement candidate.

- Fat/Overlord's validated movement now preserves its native blocked
  `RandomWalk` no-op; no bounded generic path replaces an admitted but
  unavailable movement candidate.

- SkeletonMelee's validated movement now preserves its native blocked
  `RandomWalk` no-op; no bounded generic path replaces an admitted but
  unavailable movement candidate.

- Butcher's validated movement now preserves its native blocked `RandomWalk`
  no-op; no bounded generic path replaces an admitted but unavailable movement
  candidate.

- GoatMelee's validated far-distance movement now preserves its native blocked
  `RoundWalk` no-op; no bounded generic path replaces an admitted but
  unavailable movement candidate.

- GoatMelee's ordinary admitted movement now preserves the native `RandomWalk`
  candidate order and blocked-tick no-op instead of invoking bounded generic
  pursuit.

- Gargoyle healthy non-adjacent ticks now preserve native `AiAvoidance`
  `RoundWalk`/`RandomWalk` candidate order and blocked-tick no-op after the
  server-owned healing branch.

- Gargoyle adjacent attacks now preserve the native `23 + 2 * intelligence`
  `AiAvoidance` admission before shared authoritative melee resolution.

- Scavenger corpse-seeking movement now preserves native `RandomWalk` candidate
  order and consumes the tick when all five native candidates are blocked; no
  eight-way replacement is used.

- Skeleton King summon admission is now selected by its complete validated
  tuning tuple independent of the row's native-AI label; rows without migrated
  tuning retain the native-name compatibility path, and summon candidates can
  carry the complete SkeletonMelee tuple under an alternate validated label.

- Hork Spawn admission is now selected by a complete validated tuning tuple,
  independent of the row's native-AI label; rows without migrated tuning retain
  the native-name compatibility path, while content can assign the controller
  to another validated AI identity.

- Hork Spawn now requires its summoned native monster identity in the validated
  `monster_missiles.tsv` mapping; the processor no longer embeds native ID 117,
  and malformed or stale mappings are rejected during content activation.

- Weapon classification now consults validated `Weapon`, `Sword`, `Axe`,
  `Mace`, `Bow`, and `Staff` tags for attack-animation selection, Monk flexible
  blocking, and loadout off-hand validation; metadata-free or ambiguous rows
  retain the legacy `ItemClass` fallback.

- PvP-ear native projection now comes from optional validated `combat.tsv`
  fields for the native item index and miscellaneous ID; omitted fields retain
  the compatibility values 23 and 44, while activation still rejects an ear
  definition that disagrees with the configured projection.

- Class Recharge admission now consults validated `Staff` item tags through the
  active catalog; direct legacy states without catalog metadata retain the
  native item-type path.

- Monk chest-armor scaling now consults validated `LightArmor`, `MediumArmor`,
  and `HeavyArmor` item tags through the active catalog; direct legacy states
  retain the native item-type path.

- Canonical Gold recognition and generated stack identity now consult the
  validated `Gold` item tag through the active catalog; metadata-free states
  retain the native type/index fallback.

- Flexible and fast block staff eligibility now consults validated `Staff`
  item tags through the active catalog; legacy states retain numeric fallback.

- Store recharge validation now accepts only explicit `ChargedItem` sources
  when source metadata is present; metadata-free legacy rows retain the native
  staff and miscellaneous-ID fallback.

- General store recharge now consults the active catalog's validated
  `ChargedItem` source metadata, including unambiguous miscellaneous-ID
  mappings, and its `Staff` tag—including unambiguous misc-ID tag mappings—
  before applying the native type/miscellaneous-ID fallback; conflicting item
  metadata therefore remains content-owned without breaking legacy rows.

- Player loadout validation now uses the active item definition's `Shield`
  subtype for off-hand eligibility and only falls back to native item type
  when no explicit equipment subtype is declared.

- Native player attack-animation selection now consumes validated equipment
  subtype tags for sword, axe, mace, bow, staff, and shield profiles; legacy
  snapshots and untagged rows retain numeric item-type fallback.

- Equipment damage profiles now consult active item content tags through the
  retained native item index; conflicting duplicate mappings are rejected and
  legacy item-type classification remains the compatibility fallback.

- Equipment subtype classification is now centralized in one content-first
  helper used by player attack profiles, block eligibility, Hellfire weapon
  decay, loadout validation, equipment damage, armor scaling, and monster
  weapon modifiers; metadata-free states retain their native item-type fallback.

- Gold identity is now centralized in the same validated item-classification
  helper for wallet topology, spending, generation, and death drops; legacy
  states retain the native type/index fallback.

- Belt eligibility is now centralized in the same content-first item
  classification seam for loadout validation, including unambiguous misc-ID
  tag mappings when the compatibility item index is unavailable; legacy rows
  retain the native equipment-location/item-class fallback.

- Two-handed equipment classification is now centralized for loadout topology
  and dual-wield admission; active rows use `TwoHanded` tags while legacy
  states retain the native equipment-location fallback. Unambiguous misc-ID
  tags cover compatibility states that lack a native item-index match.

- Loadout equipment-slot admission is now content-first for hand, two-handed,
  body-armor, helmet, ring, and amulet locations; untagged legacy rows retain
  the native `equip_location` mapping, while unambiguous misc-ID tags cover
  compatibility states that lack a native item-index match.

- Schema 1.146 adds normalized `Sword`, `Axe`, `Mace`, `Bow`, `Shield`,
  `Staff`, `Belt`, `OneHanded`, `TwoHanded`, `BodyArmor`, `Helmet`, `Ring`,
  and `Amulet` subtype tags to `items.tsv`; player and monster weapon
  modifiers consume those tags before falling back to native item type.

- Schema 1.147 adds optional `name` and `identified_name` item metadata. The
  authoritative catalog validates and projects these labels into the existing
  server-owned item state and protocol fields; legacy `itemdat.tsv` names map
  through the same seam.

- Schema 1.148 adds the validated `item_cursor_graphics.tsv` catalog and the
  optional normalized `cursor_graphic` item column. Normalized and legacy item
  loaders resolve the same content-owned symbols to native cursor IDs, while
  callers that omit the catalog retain compatibility fallback behavior.

- Schema 1.149 projects normalized and legacy unique-item names into generated
  item state, so unique presentation no longer inherits a base-item label.
  Unique names are validated at the catalog boundary and remain server-owned
  through snapshots and protocol projections.

- Schema 1.150 adds optional normalized unique-item `cursor_graphic` metadata.
  Normalized and legacy unique rows resolve shared cursor symbols through the
  active cursor catalog, while omitted symbols preserve the base-item cursor.

- Schema 1.151 adds optional normalized item `min_monster_level` metadata.
  The item catalog retains native random-drop eligibility thresholds from both
  normalized and legacy tables for server-owned drop planning.

- Schema 1.152 adds optional normalized item `drop_rate` metadata. The catalog
  retains native weighted random-drop rates, including zero-rate exclusions.

- Schema 1.153 adds optional normalized item `usable` metadata. Explicit
  native iUsable values now gate consumable source admission; absent metadata
  preserves the compatibility path.

- Schema 1.154 adds optional normalized item `min_armor_class` and
  `max_armor_class` metadata. When both are present, generated items use the
  native inclusive armor range; rows that omit them retain fixed armor values.

- Schema 1.155 adds optional normalized item `special_effects` metadata.
  Validated symbolic effects map to the same authoritative equipment flags as
  legacy `specialEffects`; numeric `flags` remain additive, and legacy unknown
  effect names retain their compatibility behavior.

- Schema 1.156 adds optional normalized item `hellfire_effects` metadata.
  Validated symbolic Hellfire damage/armor effects now project directly into
  generated item state; legacy unique and affix effect paths remain available.

- Schema 1.157 adds optional spell `teleport_search_radius` metadata.
  Teleport destination resolution now consumes the validated crawl radius in
  both its cast admission and movement-controller tick; direct legacy spell
  definitions retain the native five-tile default.

- Schema 1.158 adds optional spell `warp_search_radius` metadata. Warp
  destination resolution now consumes the validated crawl radius in both cast
  admission and world-transition application; direct legacy world-command
  callers retain the native five-tile default.

- Schema 1.159 adds optional Town Portal placement and lifetime metadata:
  `town_portal_search_radius` and `town_portal_lifetime`. Portal creation now
  consumes the validated crawl radius and expiry ticks; direct legacy
  world-command callers retain native five-tile, 100-tick defaults.

- Schema 1.160 adds optional monster magic-projectile accuracy metadata to
  `combat.tsv`. Monster-owned magic projectiles now use validated base, level,
  and distance terms for player-hit admission; omitted values retain the
  native 10/2/2/40/2 formula and shared 20/25/30 level-specific floors.

- Schema 1.161 adds optional Snake charge special-hit accuracy metadata to
  `combat.tsv`. Charge admission now uses the validated native base to-hit
  value with shared monster accuracy terms; omitted values retain the native
  500 compatibility value.

- Schema 1.162 adds optional Acid Puddle player-hit accuracy metadata to
  `combat.tsv`: `monster_acid_puddle_minimum_hit_chance` and
  `monster_acid_puddle_base_hit_chance`. Persistent puddle admission now uses
  validated level floors and base-hit terms; omitted values retain native
  10/40 compatibility values.

- Schema 1.163 adds optional `projectile_damage` metadata to
  `monster_missiles.tsv`. Mapped monster projectiles consume a validated fixed
  impact value when declared; omitted values retain behavior-specific/native
  damage derivation.

- Schema 1.164 adds optional `maximum_resistance` metadata to `combat.tsv`.
  Active player elemental resistance calculations now use the validated cap;
  direct legacy resistance callers retain the native 75-percent default.

- Schema 1.165 adds optional `maximum_attribute` metadata to `combat.tsv`.
  Active player equipment recalculation now uses the validated primary-attribute
  ceiling; omitted values retain the native 750 default for compatibility callers.

- Schema 1.166 adds optional `barbarian_weapon_strength_damage_divisor` metadata
  to `combat.tsv`. Active Barbarian axe/mace damage scaling now uses the
  validated divisor; omitted values retain the native 75 divisor for compatibility callers.

- Schema 1.167 adds optional `rogue_strength_dexterity_damage_divisor` metadata
  to `combat.tsv`. Active Rogue physical damage scaling now uses the validated
  divisor; omitted values retain the native 200 divisor for compatibility callers.

- Schema 1.168 adds optional `monk_staff_unarmed_damage_divisor` metadata to
  `combat.tsv`. Active Monk staff/unarmed physical damage scaling now uses the
  validated divisor; omitted values retain the native 150 divisor for compatibility callers.

- Schema 1.169 adds optional `monk_weapon_damage_divisor` metadata to
  `combat.tsv`. Active Monk non-staff weapon physical damage scaling now uses
  the validated divisor; omitted values retain the native 300 divisor for compatibility callers.

- Schema 1.170 adds optional `bard_sword_strength_dexterity_damage_divisor`
  metadata to `combat.tsv`. Active Bard sword physical damage scaling now uses
  the validated divisor; omitted values retain the native 150 divisor for compatibility callers.

- Schema 1.171 adds optional `bard_bow_strength_dexterity_damage_divisor`
  metadata to `combat.tsv`. Active Bard bow physical damage scaling now uses
  the validated divisor; omitted values retain the native 250 divisor for compatibility callers.

- Schema 1.172 adds optional `bard_default_strength_damage_divisor` metadata
  to `combat.tsv`. Active Bard fallback physical damage scaling now uses the
  validated divisor; omitted values retain the native 100 divisor for compatibility callers.

- Schema 1.173 adds optional `standard_strength_damage_divisor` metadata to
  `combat.tsv`. Active standard-class fallback physical damage scaling now uses
  the validated divisor; omitted values retain the native 100 divisor for compatibility callers.

- Schema 1.174 adds optional `barbarian_bow_strength_damage_divisor` metadata
  to `combat.tsv`. Active Barbarian bow physical damage scaling now uses the
  validated divisor; omitted values retain the native 300 divisor for compatibility callers.

- Schema 1.175 adds optional `barbarian_default_strength_damage_divisor`
  metadata to `combat.tsv`. Active Barbarian fallback physical damage scaling
  now uses the validated divisor; omitted values retain the native 100 divisor for compatibility callers.

- Schema 1.176 adds optional `barbarian_vitality_damage_divisor` metadata to
  `combat.tsv`. Active Barbarian weaponless physical damage scaling now uses
  the validated divisor; omitted values retain the native 100 divisor for compatibility callers.

- Schema 1.177 adds optional `thorns_damage_minimum` and
  `thorns_damage_maximum` metadata to `combat.tsv`. Active Thorns retaliation
  now uses the validated damage range; omitted values retain the native 1–3 range for compatibility callers.

- Schema 1.178 adds optional `hellfire_decay_damage_reduction` and
  `hellfire_decay_removal_threshold` metadata to `combat.tsv`. Active Hellfire
  Decay now uses the validated decrement and removal threshold; omitted values retain the native 5/-100 behavior for compatibility callers.

- Schema 1.179 adds optional `hellfire_devastation_chance_percent` and
  `hellfire_devastation_damage_multiplier` metadata to `combat.tsv`. Active
  Hellfire Devastation now uses the validated proc chance and multiplier; omitted values retain the native 5%/3× behavior for compatibility callers.

- Schema 1.180 adds optional `hellfire_jesters_roll_maximum`,
  `hellfire_jesters_expansion_threshold`, and
  `hellfire_jesters_expansion_multiplier` metadata to `combat.tsv`. Active
  Hellfire Jesters now uses the validated roll and upper-half expansion terms;
  omitted values retain the native 0–200/100/5 behavior for compatibility callers.

- Schema 1.181 adds optional `hellfire_peril_damage_multiplier` and
  `hellfire_doppelganger_chance_percent` metadata to `combat.tsv`. Active
  Hellfire Peril and Doppelganger now use validated native terms; omitted
  values retain the native 2× and 10% behavior for compatibility callers.

- Schema 1.184 adds optional `critical_strike_chance_per_level_percent`
  metadata to `classes.tsv`. Active class Critical Strike now uses the
  validated per-level proc chance; omitted values retain the native 1% per
  character level behavior.

- Schema 1.185 adds optional `iron_skin_level_divisor` metadata to
  `classes.tsv`. Active IronSkin armor now uses the validated class divisor;
  omitted values retain the native level-divided-by-four behavior.

- Schema 1.186 adds optional `natural_resistance_per_level_percent` and
  `heavy_hit_threshold_bonus_percent` metadata to `classes.tsv`. Active class
  defense now uses the validated percentages; omitted values retain the native
  100% and 25% behavior.

- Schema 1.187 adds optional `pvp_block_level_scale` metadata to `combat.tsv`.
  Active player-versus-player block chance now uses the validated scale;
  omitted values retain the native scale of two.

- Schema 1.188 adds optional `fast_block_frame_skip` metadata to `classes.tsv`.
  Active FastBlock staff timing now uses the validated class value; omitted
  values retain the native two-frame behavior.

- Schema 1.189 adds optional `equipment_fast_block_frame_skip` metadata to
  `combat.tsv`. Equipment FastBlock timing now uses the validated combat value;
  omitted values retain the native two-frame behavior.

- Schema 1.190 adds optional Fast/Faster/Fastest Hit Recovery skip metadata to
  `combat.tsv`. Active recovery timing now uses the validated ordered values;
  omitted values retain the native 1/2/3 behavior.

- Schema 1.191 adds optional Quick/Fast/Faster/Fastest melee attack-speed skip
  metadata to `combat.tsv`. Active melee timing now uses validated skips for
  both first-frame and subsequent-frame paths; omitted values retain the
  native 1/0, 2/1, 3/2, and 4/2 behavior.

- Schema 1.192 adds optional Golem summon formula metadata to `spells.tsv`:
  health level/mana terms, damage level/offset terms, and armor class. Active
  Golem summons now use validated row values; omitted values retain the native
  health 2/3, damage 2/4/8, and armor 25 behavior.

- Schema 1.193 adds optional `golem_hit_points_level_base_q6` metadata to the
  Golem row in `spells.tsv`; omitted values retain the native 320 Q6 per-level
  health term.

- Schema 1.202 adds an optional persistent-projectile player damage divisor to
  `spells.tsv`; omitted values retain the native divisor of two.

- Schema 1.203 adds optional direct player-magic hit tuning to `combat.tsv`;
  Charged Bolt, Firebolt, and Bone Spirit player-hit rolls retain native 2/5/95
  character-level penalty and chance bounds when omitted.

- Schema 1.204 adds an optional Bone Spirit player damage divisor to
  `spells.tsv`; omitted values retain the native one-third current-life rule.

- Schema 1.205 adds an optional monster regeneration level divisor to
  `combat.tsv`; omitted values retain the native `level / 2` recovery rule.

- Schema 1.206 adds an optional monster-player block level scale to
  `combat.tsv`; omitted values retain the native level-difference scale of two.

- Schema 1.207 adds an optional Acid puddle impact lifetime to
  `monster_missiles.tsv`; omitted values retain the native two-tick impact
  phase.

- Schema 1.208 adds an optional mapped monster projectile impact lifetime to
  `monster_missiles.tsv`. MonsterArrow, SnakeCharge, MonsterMagic, and
  BloodStar mappings consume the shared phase; omitted values retain the native
  one-tick impact phase, while other authoritative controllers retain their
  controller-specific lifecycle transitions.

- Schema 1.209 adds optional quest dialogue and completion transition variables
  to `quests.tsv` and `quest_states.tsv`; omitted values retain the existing
  no-transition behavior while shipped unique quests preserve their native
  terminal `_qvar1` states.

- Schema 1.210 adds optional follow-on quest activation and route-activation
  policy columns; Lazarus can now activate the server-owned Diablo quest while
  that quest remains suppressed from premature route entry.

- Schema 1.211 adds optional `diablo` monster-type and `completion_diablo`
  quest-state columns; validated boss targets now complete the server-owned
  Diablo quest through an explicit native identity boundary.

- Schema 1.212 adds optional `fire_resistance_hell`,
  `lightning_resistance_hell`, `magic_resistance_hell`, and
  `acid_resistance_hell` columns to `monster_types.tsv`. Hell materialization
  now selects those validated native `resistanceHell` projections while
  Normal/Nightmare retain the base resistance channels; omitted columns keep
  the existing resistance values for compatibility.

- Schema 1.213 adds optional native difficulty tuning columns to `combat.tsv`.
- Schema 1.214 adds external ranged-attack frame timing columns to `combat.tsv`, including the Hellfire zero-skip variant.
  Level, accuracy, armor, damage, experience, and initial-health formulas now
  consume validated pack values; omitted columns retain the native defaults,
  including the distinct Nightmare `2 * (base + 2)` and Hell `4 * base + 6`
  damage formulas.

- Schema 1.215 adds optional quest-completion reward item metadata to
  `quest_states.tsv`; the Hellfire Defiler's Cathedral Map now spawns at the
  authoritative quest death boundary rather than as a static monster drop.

- Schema 1.216 adds the validated `QuestSequence` world-object effect. Hellfire
  Na-Krul's three tome interactions now use persistent server-owned quest
  variables, reset on wrong order, and complete the quest on the native order.

- Schema 1.217 adds validated content-owned item combinations. The Hellfire
  Na-Krul note fragments now combine on authoritative pickup into the
  reconstructed note while preserving native item identities.

- Schema 1.218 adds validated item quest effects. The Hellfire Cathedral Map
  now activates Grave Matters and sets its native first quest variable when
  picked up by the authoritative player.

- Schema 1.219 adds the `QuestItemCompletion` object effect. The Hellfire grave
  object now consumes the Cathedral Map and completes Grave Matters through
  the server-owned object interaction boundary; object completions also honor
  the quest catalog's completion variables and follow-on activation metadata.

- Schema 1.220 adds quest-gated level triggers. The Hellfire crypt entrance is
  now a server-owned town warp that opens only after Grave Matters completes.
  Procedural crypt return triggers remain part of the native map-synthesis
  backlog until their generated tile identities are externalized.

- Schema 1.221 adds `world_rules.tsv` with the externalized town-level identity
  used by Town Portal and Warp.

- Schema 1.222 adds the Adria service-only store identity to `world_rules.tsv`;
  activation rejects zero and stock-vendor collisions.

- Schema 1.231 separates class capability symbols into the stable `ability`
  namespace and assigns Holy Bolt the stable `spell:holy_bolt` identity while
  legacy loadouts retain spell-namespace fallback.

- Schema 1.232 adds optional `quest_symbol` columns to quest type, state, and
  pool tables; activated rows resolve the `quest` namespace and reject
  numeric/symbol mismatches while older numeric-only rows retain fallback.

- Schema 1.233 extends optional quest symbols to warp gates, linked objects,
  item quest effects, and follow-on quest activation.

- Schema 1.234 adds optional `item_symbol` to `class_loadout_items.tsv` and
  validates the shipped stable `item` entries against the active item catalog.

- Schema 1.235 extends optional item symbols to `stores.tsv`,
  `unique_items.tsv`, quest reward columns, and `item_quest_effects.tsv`.

- Schema 1.241 extends stable unique symbols to optional store unique-item
  stock and monster unique-drop references. Numeric-only links remain
  compatible while active symbols are validated against the unique catalog.

- Schema 1.242 adds the stable `monster` namespace. Monster type rows and
  authored encounter, missile selector/spawn, unique-monster, and quest
  completion links may carry symbols cross-checked against retained numeric
  IDs; numeric-only rows remain compatible.

- Schema 1.243 adds the stable `class` namespace. Class-keyed loadout and
  animation rows may carry `class_symbol`, including the valid native class
  ID zero; numeric-only rows remain compatible.

- Schema 1.244 adds the stable `level` namespace to level definitions, warps,
  portals, and Town Portal world rules. Symbolic links are cross-checked
  against active geometry while numeric-only links remain compatible.

- Schema 1.245 extends stable level symbols to authored monster, object, quest,
  and combat restart links through `level_symbol` columns. Numeric-only rows
  remain compatible while links are checked against the composed world.

- Schema 1.251 adds normalized stable Hellfire quest-item definitions for Rune
  Bomb, Theodore, Auric Amulet, Brown Suit, and Grey Suit while preserving their
  native item-index and cursor projections.

- Schema 1.252 normalizes stable symbols, display names, and native cursor
  projections for the first five Hellfire unique base-item rows, including the
  shared Hellfire ring base used by multiple unique definitions.

- Schema 1.253 normalizes stable symbols, display names, and native cursor
  projections for the next five Hellfire unique base-item rows: Veil of Steel,
  Arkaine's Valor, Griswold's Edge, Bovine Plate, and The Butcher's Cleaver.

- Schema 1.254 normalizes stable symbols, display names, and native cursor
  projections for the next five Hellfire bow base-item rows: Hunter's Bow, Long
  Bow, Short Bow, Long War Bow, and Short Battle Bow.

- Schema 1.255 normalizes stable symbols, display names, and native cursor
  projections for the next five Hellfire sword base-item rows: Dagger, Sabre,
  Falchion, Broad Sword, and Long Sword.

- Schema 1.256 normalizes stable symbols, display names, and native cursor
  projections for the next five Hellfire sword base-item rows: Claymore, Broad
  Sword, Scimitar, Bastard Sword, and Two-Handed Sword. The duplicate Hellfire
  Broad Sword uses a distinct stable symbol while retaining its native
  projection.

- Schema 1.257 normalizes stable symbols, display names, native cursor
  projections, and `Axe` generation tags for the next five Hellfire axe
  base-item rows: Large Axe, Broad Axe, Battle Axe, Small Axe, and Great Axe.

- Schema 1.258 normalizes stable symbols, corrected native indices, display
  names, native cursor projections, and `Mace` generation tags for the next
  five Hellfire mace-family rows: Mace, Maul, Flail, Morning Star, and War
  Hammer.

- Schema 1.259 normalizes stable symbols, native indices, display names, native
  cursor projections, and `Staff` generation tags for the next five Hellfire
  staff rows: Long Staff, War Staff, Short Staff, Composite Staff, and Quarter
  Staff. Quarter Staff retains its native `SHORT_STAFF` cursor projection.

- Schema 1.260 normalizes stable symbols, native indices, display names, and
  native cursor projections for the next five Hellfire armor rows: Helm, Skull
  Cap, Great Helm, Crown, and Rags. Rags uses its native LightArmor/BodyArmor
  placement while the other four rows retain helmet placement.

- Schema 1.261 normalizes stable symbols, native indices, display names, and
  native cursor projections for the next five Hellfire armor rows: Studded
  Leather Armor, Cloak, Leather Armor, Robe, and Chain Mail. These rows use
  native body-armor placement; Chain Mail corrects the retained native index
  to `64` and the native `CHAIN_MAIL` cursor identity.

- Schema 1.262 normalizes stable symbols, native indices, display names, and
  native cursor projections for the next five Hellfire armor/shield rows:
  Breast Plate, Cape, Plate Mail, Full Plate Mail, and the duplicate Hellfire
  Buckler. Armor rows use native body-armor placement; the Hellfire Buckler
  uses native one-handed shield placement and a distinct stable symbol.

- Schema 1.263 normalizes stable symbols, native indices, display names, and
  native cursor projections for the next five Hellfire shield/amulet rows: Kite
  Shield, Small Shield, Large Shield, Gothic Shield, and Amulet. Shields use
  native one-handed placement; Amulet restores the native level-8 base row and
  cursor projection.

- Schema 1.264 normalizes the Hellfire Sorcerer starting staff overlay. The
  `hellfire_staff_charged_bolt` row retains its native short-staff identity and
  Hellfire Firebolt charged-item behavior while restoring native display and
  cursor projections plus the stable `firebolt` spell reference.

- Schema 1.265 externalizes the five native rune consumables: Rune of Fire,
  Rune of Lightning, Greater Rune of Fire, Greater Rune of Lightning, and Rune
  of Stone. Their stable item identities retain native indices and cursor
  graphics, miscellaneous IDs, drop levels, and values while resolving
  consumable spell sources through validated spell rows.

- Schema 1.266 externalizes 22 native spell-backed scroll rows with stable item
  identities, native indices, cursor graphics, scroll miscellaneous IDs, level
  gates, values, drop rates, and validated spell references. Legacy item-table
  parsing now preserves the native 21/22 scroll IDs; Identify scrolls remain a
  separate item-action migration because Identify is also a class ability.

- Schema 1.268 externalizes nine Hellfire quest and miscellaneous item rows
  (Magic Rock, Tavern Sign, Golden Elixir, Anvil of Fury, Black Mushroom, Brain,
  Fungal Tome, Spectral Elixir, and Blood Stone) with stable IDs, native cursor
  identities, item classes, miscellaneous IDs, level gates, and usability.

- Schema 1.269 externalizes the Hellfire full-potion and rejuvenation family,
  including fixed Full Healing and Full Mana rows plus random Healing, Full
  Healing, Mana, Full Mana, Rejuvenation, and Full Rejuvenation rows with stable
  IDs, native potion miscellaneous IDs, cursor identities, level gates, drop
  rates, and usability.

- Schema 1.270 externalizes the Hellfire oil and elixir consumables: Blacksmith
  Oil, Oil of Accuracy, Oil of Sharpness, Oil of Fortitude, and the Strength,
  Magic, Dexterity, and Vitality Elixirs, preserving native indices,
  miscellaneous IDs, cursor identities, level gates, values, drop rates, and
  usability.

- Schema 1.271 externalizes the base helmet, body-armor, and shield rows that
  share stable projections with Hellfire, preserving base native indices, armor
  ranges, durability, requirements, cursor identities, level gates, values,
  and equipment subtypes while retaining variant-specific overlays.

- Schema 1.272 externalizes the base standard weapon rows for swords, axes,
  maces, bows, and staves, preserving native damage ranges, durability,
  requirements, cursor identities, level gates, values, and weapon subtypes;
  shared symbols retain Hellfire overlay identity while base-only rows use new
  stable symbols.

- Schema 1.273 completes the remaining base armor variants: Cap, Full Helm,
  Quilted Armor, Hard Leather Armor, Ring Mail, Scale Mail, Splint Mail, Field
  Plate, Gothic Plate, Buckler, and Tower Shield, preserving native armor
  ranges, durability, requirements, cursor identities, level gates, values,
  and equipment subtypes.

- Schema 1.274 externalizes the remaining base potion, oil, and elixir rows at
  native indices 77–90 plus the native healing scroll at index 91, preserving
  native miscellaneous IDs, values, level gates, drop rates, cursor identities,
  usability, and the scroll's consumable spell source.

- Schema 1.275 externalizes the generic base ring and amulet rows at native
  indices 156–160 and the native Arena Potion at index 167, preserving native
  item types, miscellaneous IDs, level gates, values, cursor identities, drop
  rates, and usability.

- Schema 1.276 corrects the native compatibility projections for all normalized
  fixed and spell-backed scroll rows, retaining their actual native enum indices
  and removing the duplicate healing-scroll alias.

- Schema 1.277 externalizes the four native dynamic spell-book templates at
  indices 114–117, preserving level gates, drop weights, misc identity, and the
  blue-book cursor projection. Schema 1.278 completes dynamic spell assignment
  and book-cost mutation through the validated server-owned generator described
  below.

- Schema 1.279 adds validated display names to every directly castable spell
  row. Memorized, charged-item, and consumable-item projections now use the
  server-owned spell catalog name, falling back to spell-book metadata where
  applicable.

- Schema 1.280 externalizes the native Gargoyle adjacent-attack admission pair
  into validated monster-type content. Checked-in Hellfire Gargoyle rows carry
  the native values; older packs retain the compatibility fallback.

- Schema 1.281 externalizes the native Gargoyle avoidance-movement admission
  pair and makes the validated Gargoyle identity authoritative when its other
  tuning rows are present, while preserving older-pack fallback behavior.

- Schema 1.282 externalizes Snake's native six-step lateral drift pattern in
  the validated monster-missile mapping, retaining the native sequence when
  older mappings omit the optional column.

- Schema 1.283 externalizes Resurrection's ordered eight-cell initial
  placement probes in validated spell content, retaining the native probe
  order when older mappings omit the optional column.

- Schema 1.284 externalizes Charged Bolt's ordered 16-entry steering pattern
  in validated spell content, retaining the native sequence when older
  mappings omit the optional column.

- Schema 1.285 externalizes Fireball's ordered nine-cell impact offsets in
  validated spell content, retaining the native impact order when older
  mappings omit the optional column.

- Schema 1.286 externalizes Elemental's ordered nine-cell burst offsets in
  validated spell content, retaining the native burst order when older
  mappings omit the optional column.

- Schema 1.287 externalizes Flash's ordered bottom and top layer offsets in
  validated spell content, retaining the native layer orders when older
  mappings omit the optional columns.

- Schema 1.288 externalizes Guardian's ordered 48-cell firing arc in validated
  spell content, retaining the native firing order when older mappings omit
  the optional column.

- Schema 1.300 externalizes Snake's distant-delay admission chance through
  optional `SnakeCharge` missile fields, retaining the native
  35-minus-two-times-intelligence fallback for older mappings.

- Schema 1.301 externalizes Counselor's failed-ranged-admission movement
  chance through optional `counselor_move_chance_percent` monster-type data,
  retaining the native 30-percent fallback for older mappings.

- Schema 1.302 externalizes Mega's delegated Skeleton delay-admission chance
  through optional monster-type fields, retaining the native
  35-minus-four-times-intelligence fallback for older mappings.

- Schema 1.303 externalizes Mega's intermediate-distance movement admission
  through optional monster-type fields, retaining the native
  50-plus-ten-times-intelligence fallback and 100 percent cap for older
  mappings.

- Schema 1.304 externalizes SkeletonKing's RoundWalk admission modulo through
  optional monster-type data, retaining the native modulo-four fallback for
  older mappings.

- Schema 1.305 externalizes Hork Demon's far-distance RoundWalk admission
  modulo through optional monster-type data, retaining the native modulo-four
  fallback for older mappings.

- Schema 1.306 externalizes the shared AiAvoidance Move-goal distance and
  modulo gate through optional monster-type data, retaining the native
  minimum-four/modulo-four fallback for older mappings.

- Schema 1.307 externalizes Rhino's far-distance movement-goal distance and
  modulo gate through optional monster-type data, retaining the native
  minimum-five/modulo-four fallback for older mappings.

- Schema 1.308 externalizes AiRangedAvoidance's far-distance Move-goal modulo
  through optional missile data, retaining the native four-way/Acid eight-way
  fallback for older mappings.

- Schema 1.309 externalizes AiRangedAvoidance's ordinary RandomWalk movement
  band through optional missile data, retaining the native 50/10/30 fallback
  for older mappings.

- Schema 1.310 externalizes SkeletonRanged's fresh-mode movement bonus through
  optional monster-type data, retaining the native 50-percent fallback for
  older mappings.

- Schema 1.311 externalizes Snake's adjacent attack-admission chance through
  optional `SnakeCharge` missile data, retaining the native
  20-plus-intelligence fallback for older mappings.

- Schema 1.312 externalizes Bat's long-idle and fresh-mode movement bands
  through optional monster-type data, retaining the native 13/1/50 fallback
  for older mappings.

- Schema 1.313 externalizes Counselor's ranged-admission chance through
  optional monster-type data, retaining the native capped 50/5 fallback for
  older mappings.

- Schema 1.314 externalizes the shared AiAvoidance fresh-mode movement bonus
  through optional monster-type data, retaining the native 50-point fallback
  for older mappings.

- Schema 1.315 externalizes the Fat/Overlord fresh-mode movement bonus through
  optional monster-type data, retaining the native 50-point fallback for older
  mappings.

- Schema 1.316 externalizes Rhino's ordinary movement bands through optional
  monster-type data, retaining the native 33/2/50 fallback for older mappings.

- Schema 1.317 externalizes Familiar Lightning's native random-damage upper
  bound through optional missile data, retaining the native `GenerateRnd(10)`
  fallback for older mappings.

- Schema 1.318 externalizes AiRangedAvoidance's adjacent attack-admission base
  and intelligence scale through optional missile data, retaining the native
  6,000/1,000 fallback for older mappings.

- Schema 1.319 externalizes AiRangedAvoidance's adjacent attack-admission
  divisor through optional missile data, retaining the native Acid half-band
  for older mappings.

- Schema 1.320 externalizes Familiar's post-melee missile identity through an
  optional `post_melee_missile` field, retaining the legacy Lightning-label
  fallback for older mappings.

- Schema 1.321 externalizes mapped Bat and Snake charge projectile damage
  type through the existing optional `damage_type` field, retaining the
  native Physical fallback for older mappings.

- Schema 1.322 externalizes mapped Bat and Snake charge projectile spell
  identity through the existing optional `spell_id`/`spell_symbol` fields,
  retaining native spell identity `1` for older mappings.
- Schema 1.323 adds optional `procedural_levels.tsv` definitions. Activation
  materializes deterministic connected geometry from each row's generation seed
  combined with the server world seed, composes generated levels with static
  levels, and rejects invalid or duplicate definitions, including non-Boolean
  default-start and multiplayer-death-drop flags. Full native map synthesis and
  richer authored geometry remain follow-up work.
- Schema 1.324 adds optional `missile_blocked_percent` procedural-level
  metadata. Generated missile occlusion is deterministic and always includes
  movement-blocked cells, while omitted values retain the movement-density
  fallback.

- Schema 1.326 adds optional native towner turn-in metadata to
  `quests.tsv` and `quest_states.tsv`, including the accepting towner, required
  item, native `_qvar2` gate, unique reward, positions, and dialogue message.
  The original shipped use was the Hellfire Blacksmith Anvil turn-in; the same
  schema now also carries Little Girl and Jersey's Jersey.

- Schema 1.327 adds optional normal-item turn-in rewards to the same quest
  tables. The server validates the item catalog and generates the authored
  reward at its native level and position; Hellfire Little Girl consumes
  Theodore and creates the level-13 amulet through the shared turn-in command.

- Schema 1.328 adds optional unique-monster dialogue hand-ins to the same quest
  tables. The server validates the required item and authored relative reward,
  then consumes, completes, spawns, and emits completion dialogue atomically.
  Hellfire Lachdanan consumes Golden Elixir and spawns Veil of Steel one tile
  south of the encounter. Normal-item dialogue rewards now use the validated
  spell-book catalog when the authored item is a dynamic book template, so
  generated spell identity and read-book state remain server-owned.

- Schema 1.329 adds an optional one-time dialogue activation reward. Unlike a
  hand-in, the server generates the authored reward during the first valid
  conversation, keeps the quest active, and suppresses duplicate rewards on
  later conversations. Base Zhar now uses this boundary for its level-14
  dynamic spell-book reward one tile southeast of the encounter.

- Schema 1.330 adds the optional `turn_in_activation_only` towner mode. The
  server can now activate an inactive authored quest and create its initial
  normal item without completing the quest; Hellfire Farmer's Orchard uses it
  for the native Rune Bomb conversation before its existing Hive completion.

- Schema 1.331 adds the optional `turn_in_on_completion` towner stage and its
  authored `_qvar2` marker/reward metadata. Hellfire Farmer's Orchard uses it
  after Hive completion to generate Auric and emit the native completion
  dialogue through the server boundary.

- Schema 1.332 adds optional alternate-item towner interactions. The server
  consumes an authored alternate item and emits its dialogue without changing
  quest progress; Hellfire Jersey's Jersey uses this for the native Grey Suit
  tease before its Brown Suit hand-in.

- Schema 1.333 adds the activation-only item and `_qvar1` marker fields. Base
  Cain now consumes Staff of Lazarus and starts quest 3016 through the same
  `TurnInQuestRequested` boundary.

- Schema 1.334 adds the base Magic Rock item effect and completes the authored
  Griswold turn-in coverage. The staged `_qvar2` prompt now runs before item
  validation, then the server consumes Magic Rock and creates Empyrean Band at
  the native reward position.

- Schema 1.335 adds the base Ogden's Sign staged towner row and normalized
  Tavern Sign/Harlequin Crest content through `TurnInQuestRequested`.

- Schema 1.336 adds the base Anvil of Fury pickup effect, staged Griswold
  turn-in, normalized Anvil item, and Griswold's Edge reward content.

- Schema 1.337 adds the base Butcher activation, normalized The Butcher
  monster, and authored Dead Guy quest state.

- Schema 1.338 adds the base Skeleton King route quest state and normalized
  native Skeleton King monster definition. Schema 1.339 adds the authored
  route activation `_qvar1` marker used by the native Skeleton King level.

- Schema 1.340 adds completion-stage `_qvar1` gates and markers. Lazarus's
  Storyteller transition now uses those fields and logs the Diablo follow-on.

- Schema 1.341 adds non-consuming, item-gated `turn_in_dialogue_only` stages.
  Hellfire Grave Matters uses the new stage for Gillian's Cathedral Map
  conversation.

- Schema 1.342 adds variable-1-only completion-stage markers. Base Poisoned
  Water uses the new fields for Pepin's native completion conversation and
  Ring of Truth reward.

- Schema 1.343 adds active staged item turn-ins with `_qvar1` gates and
  progress-only markers. Base Black Mushroom now externalizes Pepin's Brain
  hand-in, Spectral Elixir reward, and native `_qvar1 = 7` transition while
  leaving the quest active for Adria's later conversation.

- Schema 1.344 adds normalized base Optic Amulet and Arkaine's Valor item and
  unique definitions. Their pickups now complete the authored Halls of the
  Blind (3009) and Valor (3010) quests through the shared item quest-effect
  catalog; Hellfire's existing Warlord mapping remains an overlay.

- Schema 1.345 adds an authored subsequent-dialogue `_qvar1` transition.
  Warlord's second conversation now advances from `QS_WARLORD_TALKING` to
  `QS_WARLORD_ATTACKING` through the shared unique-monster dialogue boundary.

- Schema 1.346 adds repeatable content-owned quest/towner interaction rows.
  Base Black Mushroom now externalizes Adria's Fungal Tome activation, mushroom
  hand-in, brain dialogue, and Spectral Elixir completion stages.

- Schema 1.348 adds server-owned per-player `visited_level_ids` and optional
  OR-gated quest/towner stages. Base King Leoric activation now matches native
  `_pLvlVisited[2] || _pLvlVisited[4]` behavior through the shared protocol.

- Schema 1.350 adds the Hellfire Grave Matters/Barmaid dialogue-only turn-in's
  server-owned `_pLvlVisited[21]` suppression, projected to native and Godot
  clients through `QuestSnapshot.turn_in_blocked_visited_level`.

- Schema 1.351 adds `require_unlogged` to authored quest/towner stages. Base
  Poisoned Water now externalizes Pepin's initial Caves/Poisoned Water
  visitation gate and the native active-but-unlogged fallback through the same
  server-owned state and protocol projection.

- Schema 1.349 moves the base Magic Rock and Anvil activation conversations
  into authored quest/towner rows, including their native visited-level OR-gates
  and variable-2 markers; the existing item hand-ins and unique rewards remain
  validated quest-catalog transitions.

- The Godot protocol client now validates and selects the first adjacent
  content-owned quest/towner stage with matching quest variables and item
  identity, and its HUD submits the corresponding server intent.

- Hellfire Magic Rock now uses the authored Griswold turn-in definition. The
  server owns its native first-conversation variable gate, required-item
  consumption, and Empyrean Band unique reward.

- Schema 1.278 adds validated metadata for all 23 native bookable spells and a
  deterministic server-owned generator for spell names, book costs, intelligence
  requirements, school-specific cursors, and eligible external templates.
  Random selection now rolls over the validated active native spell-table span
  and walks native spell IDs cyclically before mapping to stable IDs; explicit
  spell references remain direct. Reading/learning is server-owned through
  `ReadBookRequested`; single-player generation excludes native Resurrect and
  Heal Other while multiplayer generation retains both. Store rows plus
  static/procedural monster drops and quest-completion rewards now invoke the
  same game-mode-aware generator and retain their supplied native book-template
  identity.

- Schema 1.250 adds the stable `warp` namespace with optional `warp_symbol`
  static-route links, preserving numeric compatibility.

- Schema 1.249 completes the stable `portal` namespace with optional
  `portal_symbol` static-route links, preserving numeric compatibility.

- Schema 1.248 adds the stable `store` namespace and optional store symbols for
  vendor stock and Adria's service identity, preserving numeric compatibility.

- Schema 1.247 adds the stable `object` namespace and optional `object_symbol`
  world-object links, preserving numeric-only compatibility.

- Schema 1.246 adds the stable `set_level` namespace and optional
  `set_level_symbol` quest-type links, preserving native `SL_NONE` ID zero and
  numeric-only compatibility.

- Schema 1.240 adds the stable `unique` namespace and `unique_symbol` to
  shipped `unique_items.tsv` rows. Numeric-only unique definitions remain
  compatible for older external packs.

- Schema 1.239 adds optional `effect_item_symbol` to `objects.tsv` for
  `QuestItemCompletion` rows while preserving numeric effect amounts for all
  legacy and non-item effects.

- Schema 1.238 completes stable base-item symbols for shipped
  `unique_items.tsv` rows. Numeric-only external packs remain compatible while
  checked-in base and Hellfire rows are symbolically linked.

- Schema 1.237 completes stable symbols for every shipped normalized item
  definition. `items.tsv` retains optional-symbol compatibility for older
  third-party packs, while the checked-in base and Hellfire definitions and
  their `content_ids.tsv` entries are complete and one-to-one.

- Schema 1.236 extends optional item symbols to `item_combinations.tsv`,
  monster drop rows, and `combat.tsv`'s PvP-ear item reference.

- Schema 1.230 adds optional `spell_symbol` to direct spell rows in `stores.tsv`;
  rows resolve stable spell IDs and reject numeric/symbol mismatches while
  older rows retain numeric fallback.

- Schema 1.229 adds optional `jester_spell_symbols` to Jester rows in
  `spells.tsv`; dispatch entries resolve stable spell IDs and reject
  numeric/symbol mismatches while older rows retain numeric fallback.

- Schema 1.228 adds optional `spell_symbol` to `store_services.tsv`; spell-staff
  pricing rows resolve stable spell IDs and reject numeric/symbol mismatches
  while older rows retain numeric fallback.

- Schema 1.227 adds optional `spell_symbol` to `spells.tsv`; active rows resolve
  stable spell IDs and reject numeric/symbol mismatches while older rows retain
  numeric fallback.

- Schema 1.226 adds optional `spell_symbol` to `items.tsv`; active rows resolve
  stable spell IDs and reject numeric/symbol mismatches while older rows retain
  numeric fallback.

- Schema 1.225 adds optional `spell_symbol` to `monster_missiles.tsv`; active
  rows resolve stable spell IDs and reject numeric/symbol mismatches while older
  rows retain numeric fallback.

- Schema 1.224 adds optional class-loadout spell symbols for class abilities and
  starting memorized spells; activated packs cross-check those symbols against
  numeric compatibility IDs, while older loadout schemas retain their fallback.

- Schema 1.223 adds optional Town Portal destination coordinates and slot
  layout to `world_rules.tsv`; packs without those columns retain spell-row
  destination tuning.

- The native single-player Diablo defeat rank is now server-owned and crosses
  snapshots, protocol field 34, version-1 saves, and canonical state hashes;
  multiplayer completion does not mutate it. Native Normal/Nightmare/Hell
  difficulty is now selected by authoritative start configuration and
  persisted in snapshots, protocol field 15, version-1 saves, and hashes;
  the rank records difficulty + 1. The authoritative combat module now derives
  native Nightmare/Hell monster level (+15/+30), armor (+50/+80), attack
  accuracy (+85/+120), physical damage scaling, regeneration, and experience
  scaling (2/4 × base-plus-1000, with unique rewards doubled) at runtime
  without mutating persisted base monster data. Initial materialization now
  also applies native Nightmare/Hell 3x/4x health formulas with the
  Diablo/Hellfire single-player/multiplayer bonuses; effective Q6 health is
  persisted in snapshots while validated base monster definitions remain
  unchanged. Hell-specific resistance data is now externalized through the
  validated monster-type columns and selected only at Hell materialization.

- Native Diablo defeat now also sets a server-owned terminal victory flag,
  persists it through the transport-neutral snapshot and version-1 save
  boundary, includes it in canonical hashes, emits a typed protocol event, and
  rejects later player intents or autonomous gameplay ticks; cinematic
  playback and broader post-victory presentation remain client work.

- Schema 1.201 adds optional persistent-projectile player magic-hit tuning to
  `spells.tsv`; shared player-hit rolls retain native 2/5/95 character-level
  penalty and chance bounds when omitted.

- Schema 1.200 adds optional persistent-projectile magic-hit tuning to
  `spells.tsv`; shared Fire Wall, Fireball, Elemental, Nova, Apocalypse, and
  Lightning-segment monster hits retain native 2/5/95 defaults when omitted.

- Schema 1.199 adds optional Bone Spirit magic-hit and current-life damage
  tuning to `spells.tsv`; omitted values retain the native 2/5/95 hit terms
  and one-third current-life damage rule.

- Schema 1.198 adds optional Firebolt magic-hit tuning to `spells.tsv`;
  omitted values retain the native 2/5/95 monster-level penalty and
  hit-chance bounds.

- Schema 1.197 adds optional Holy Bolt magic-hit tuning to `spells.tsv`;
  omitted values retain the native 2/5/95 monster-level penalty and
  hit-chance bounds.

- Schema 1.196 adds optional Charged Bolt magic-hit tuning to `spells.tsv`;
  omitted values retain the native 2/5/95 monster-level penalty and
  hit-chance bounds.

- Schema 1.195 adds optional Town Portal destination slot metadata to
  `spells.tsv`; omitted values retain the native 57/40 destination origin,
  four slots, and two-tile spacing.

- Schema 1.194 adds optional `reflect_damage_percent_minimum` and
  `reflect_damage_percent_maximum` metadata to the Reflect row in `spells.tsv`;
  omitted values retain the native 20–30% reflected-damage roll.

- Schema 1.183 adds optional `critical_damage_multiplier` metadata to
  `classes.tsv`. Active class Critical Strike now uses the validated multiplier;
  omitted values retain the native 2× behavior for compatibility callers.

- Schema 1.182 adds optional `triple_demon_damage_multiplier` metadata to
  `combat.tsv`. Active Triple Demon Damage now uses the validated multiplier;
  omitted values retain the native 3× behavior for compatibility callers.

- Monster missile `spell_id` mappings are now authoritative for the Succubus
  Blood Star path and are cross-validated against the active spell catalog;
  older rows without a mapping retain the native inference fallback.

- Schema 1.145 externalizes special consumable spell behavior through
  `spells.tsv`; the checked-in Rune of Fire row declares `RuneFire`, while
  legacy spell tables retain miscellaneous-ID fallback.

- Schema 1.144 externalizes normalized item spell-source classification through
  `items.tsv`; generated state and protocol matching use the validated source
  kind and spell identity, including charged `SPELL` powers applied by unique
  items and magic affixes, while legacy item tables retain native-ID fallback.

- Cast-source resolution now consults explicit source metadata from the active
  item catalog when a legacy state lacks the projected field; native
  miscellaneous-ID inference remains the final compatibility fallback.

- Schema 1.143 externalizes semantic class-skill roles through validated
  `class_loadouts.tsv` content and carries them through authoritative snapshots
  and protocol projections, so Godot service presentation no longer infers
  Repair, Trap Disarm, Recharge, Identify, Search, or Rage from numeric IDs.

- Authoritative item-service and trap-disarm admission now checks the semantic
  class-skill role first, allowing content packs to assign different spell
  identities without changing gameplay authorization; legacy snapshots that
  lack a role retain their configured spell-ID fallback.

- Godot no longer keeps a numeric table for remote-player spell targeting. It
  supplies visible player identity plus the clicked tile, while the server
  resolves targetability from the active external spell behavior and preserves
  tile targeting for non-player spells.

- Item-backed spell-source kind is now projected by the server in
  `ItemStateSnapshot`; Godot no longer identifies staffs, scrolls, or runes by
  native miscellaneous-ID ranges.

- Schema 1.142 externalizes native Hellfire class-armor adjustments through
  validated `combat.tsv` content; older combat definitions retain the 40/20
  demon/undead compatibility values.

- Schema 1.141 externalizes native post-hit equipment resource restoration
  through validated `combat.tsv` content; older combat definitions retain the
  8/3/5/3/5 random divisor and life/mana compatibility values.

- Schema 1.140 externalizes native monster accuracy formula tuning through
  validated `combat.tsv` content; older combat definitions retain the 2/30/2
  level-scale, base-bonus, and ranged-distance compatibility values.

- Schema 1.139 externalizes native monster hit-chance floors through validated
  `combat.tsv` content; older combat definitions retain the 15/10 ordinary and
  20/25/30 level-specific compatibility values.

- Schema 1.138 externalizes the Warrior Repair ability's durability-loss
  denominator offset through validated `classes.tsv` content; older class
  definitions retain the native offset compatibility behavior.

- Schema 1.137 externalizes Charged Bolt's initial path random range through
  validated `spells.tsv` content; older definitions retain the native 1–15
  compatibility range.

- Schema 1.136 externalizes Berserk's magic-resistance admission frequency
  through validated `spells.tsv` content; older definitions retain the native
  1-in-2 compatibility roll.

- Schema 1.135 externalizes Reflect's no-explicit-level fallback through
  validated `spells.tsv` content; older definitions retain the native level-2
  compatibility value.

- Schema 1.134 externalizes Rage's minimum-life admission threshold through
  validated `spells.tsv` content; older definitions retain the native
  character-level 64-Q6 compatibility threshold.

- Schema 1.133 externalizes the Mana spell's three native restoration ranges
  through validated `spells.tsv` content; older definitions retain the native
  10/4/6 compatibility values.

- Schema 1.132 externalizes Gargoyle wounded-healing admission through
  validated `monster_types.tsv` rows; the active catalog feeds the native
  health threshold while older targets retain the 50-percent compatibility
  value.

- Bat post-hit retreat now uses the validated duration to identify its native
  initial retreat step, so external overlays can safely tune the duration
  without changing the deterministic phase order.

- Schema 1.131 externalizes Scavenger wounded-healing admission through
  validated `monster_types.tsv` rows; the active catalog feeds the native
  health threshold while older targets retain the 50-percent compatibility
  value.

- Schema 1.130 externalizes GoatMelee wounded-special admission through
  validated `monster_types.tsv` rows; the active catalog feeds the native
  health threshold and deterministic random-group gate while older targets
  retain the 50-percent/two-group compatibility values.

- Schema 1.129 externalizes Bat post-hit retreat duration through validated
  `monster_types.tsv` rows; the active catalog feeds server-owned retreat state
  while older targets retain the native compatibility duration.

- Schema 1.128 externalizes Scavenger corpse-eating duration, search radius,
  variant healing, and base-game target-health tuning through validated
  `monster_types.tsv` rows; the active catalog feeds server-owned healing while
  older targets retain the native compatibility values.

- Schema 1.127 externalizes the Gargoyle self-heal divisor multiplier,
  random-range modulus, and random offset through validated `monster_types.tsv`
  rows; the active catalog feeds server-owned healing while older targets retain
  the native compatibility formula.

- Schema 1.126 activates validated missile projectile speed/lifetime fields for
  Snake and Bat mapped-charge controllers; older rows retain the native
  18-speed, 256-tick compatibility trajectory.

- Schema 1.125 externalizes Zombie’s random-direction prepend chance base and
  intelligence scale through validated `monster_types.tsv` rows; older targets
  retain the native compatibility values.

- Schema 1.124 externalizes Fat/Overlord's adjacent attack and special-band
  chance bases and intelligence scales through validated `monster_types.tsv`
  rows; older targets retain the native compatibility values.

- Schema 1.123 externalizes GoatMelee's adjacent attack chance base and
  intelligence scale through validated `monster_types.tsv` rows; older targets
  retain the native compatibility values.

- Schema 1.122 externalizes SkeletonMelee's adjacent attack chance base and
  intelligence scale through validated `monster_types.tsv` rows; older targets
  retain the native compatibility values.

- Schema 1.121 externalizes Mega's monster-Inferno maximum distance, adjacent
  and ranged intelligence bands, far-distance offset, and adjacent choice
  modulo through validated `monster_missiles.tsv` content; older mappings retain
  native compatibility values.
- The native GoatRanged Arrow path now consumes schema 1.120's validated
  ranged-retreat distance and chance fields, keeping its dedicated dispatch
  aligned with the shared server-owned close-range boundary.

- Schema 1.120 externalizes generic `Ranged` close-range retreat distance and
  chance tuning through validated `monster_missiles.tsv` rows; the active
  catalog feeds server-owned AI, including the dedicated Succubus-family Blood
  Star path, while older mappings retain native values.

- Schema 1.119 externalizes generic `RangedAvoidance` distance and chance
  tuning through validated `monster_missiles.tsv` rows; the active catalog feeds
  server-owned AI while older mappings retain native compatibility values.

- Schema 1.118 extends validated `monster_missiles.tsv` charge-distance tuning
  to Snake `SnakeCharge` rows; the active catalog feeds server-owned AI while
  older mappings retain the native compatibility distance.

- Schema 1.117 externalizes Bat-family mapped-charge admission tuning through
  validated `monster_missiles.tsv` rows; the active catalog feeds server-owned
  AI while older mappings retain the native compatibility values.

- Schema 1.116 externalizes Rhino clear-lane charge admission tuning through
  validated `monster_types.tsv` rows; the active catalog feeds server-owned AI
  while older targets retain the native compatibility values.

- Schema 1.115 externalizes SkeletonRanged retreat and clear-lane arrow
  admission tuning through validated `monster_types.tsv` rows; the active
  catalog feeds server-owned AI while older targets retain the native
  compatibility values.

- Schema 1.114 externalizes the Gargoyle wounded retreat/self-heal safety
  boundary through validated `monster_types.tsv` rows; the active catalog feeds
  server-owned AI while older targets retain the native compatibility values.

- Schema 1.113 externalizes Hork Demon adjacent attack, Hork Spawn, and
  non-adjacent movement admission tuning through validated `monster_types.tsv`
  rows; the active catalog feeds server-owned AI while older targets retain the
  native compatibility values.

- Schema 1.112 externalizes Skeleton King summon-admission tuning through
  validated `monster_types.tsv` rows; the active catalog feeds server-owned AI
  while older targets retain the native compatibility values.

- Schema 1.111 externalizes Zombie long-range movement-distance tuning through
  validated `monster_types.tsv` rows; the active catalog feeds server-owned AI
  while older targets retain the native compatibility values.

- Schema 1.110 externalizes Zombie autonomous action-band tuning through
  validated `monster_types.tsv` rows; the active catalog feeds server-owned AI
  while older targets retain the native compatibility values.

- Schema 1.109 externalizes Fat/Overlord non-adjacent movement-band tuning
  through validated `monster_types.tsv` rows; the active catalog feeds
  server-owned AI while older targets retain the native compatibility values.

- Schema 1.108 externalizes GoatMelee non-adjacent movement-band tuning
  through validated `monster_types.tsv` rows; the active catalog feeds
  server-owned AI while older targets retain the native compatibility values.

- Schema 1.107 externalizes SkeletonMelee movement-delay tuning through
  validated `monster_types.tsv` rows; the active catalog feeds server-owned AI
  while older targets retain the native compatibility values.

- Schema 1.106 externalizes Bat adjacent attack-band tuning through validated
  `monster_types.tsv` rows; the active catalog feeds server-owned combat while
  older targets retain the native compatibility values.

- Schema 1.105 externalizes Sneak visibility-band and hit-retreat tuning
  through validated `monster_types.tsv` rows; the active catalog feeds
  server-owned AI while older targets retain the native compatibility values.

- Schema 1.104 externalizes Fallen fear radius and countdown tuning through
  validated `monster_types.tsv` rows; the active catalog feeds server-owned
  combat while older targets retain the native compatibility values.

- Schema 1.103 externalizes the native Monk unarmed minimum and maximum
  damage level divisors through validated class content, with compatibility
  defaults for older class schemas.

- Schema 1.102 externalizes native equipped-damage formula selection through
  validated class content and removes symbol checks from physical damage and
  Monk armor resolution. Legacy class tables retain their formula mapping at
  the loader boundary.

- Schema 1.101 externalizes two native class traits through validated
  `class_flags`: Monk staff fast-block timing and Barbarian hit-stun threshold
  scaling. Combat no longer selects those rules by class symbol, and older
  class overlays retain their previous behavior through compatibility defaults.

- Fallen fear is now a server-owned monster state: nearby Fallen receive the
  native `max(8 - level, 2)` retreat countdown and source-to-survivor direction
  at the death boundary, with deterministic walk fallback and snapshot,
  save, protocol, and hash coverage.
- Native GoatRanged and SkeletonRanged Arrow mappings now create explicit
  server-owned monster-arrow projectiles. Their resolved physical damage is
  captured at admission, travel is collision-checked per authoritative tick,
  and impact damage, player death, snapshots, saves, and protocol projection
  remain server-owned instead of applying ranged damage immediately.
- Native Sneak stealth is now server-owned: its intelligence-derived visibility
  band, hidden target exclusion, and nine-tick post-hit retreat persist through
  snapshots, saves, protocol, and canonical hashes.
- Native Scavenger corpse seeking and healing are now server-owned. Defeated
  non-minion monsters become persisted corpse tiles; Scavenger healing goals,
  deterministic navigation, native base/Hellfire healing, and Hellfire corpse
  consumption cross snapshots, saves, protocol, and canonical hashes.
- Native SkeletonKing summoning is now server-owned. A validated, level-aware
  SkeletonMelee content boundary creates deterministic summons on an available
  adjacent cell, including candidates selected by a complete behavior tuple
  under an alternate validated label; clients receive ordinary authoritative
  monster snapshots and never execute the summon AI.
- Native SkeletonKing non-adjacent movement now preserves LeoricAi's deterministic
  RoundWalk/RandomWalk candidate order after summon admission, before bounded
  navigation fallback.
- Native HorkDemon non-adjacent movement now preserves its far-distance
  RoundWalk/RandomWalk candidate order after Hork Spawn admission, before
  bounded navigation fallback.
- Native MegaAi now delegates to the server-owned SkeletonAi movement boundary
  at or beyond Inferno's maximum distance, preserving its native delay band
  before bounded navigation fallback.
- Native Bat adjacent attacks now use the `4 * intelligence + 8` admission
  band, then retain the native two-step retreat direction and countdown through
  snapshots, saves, protocol, and canonical hashes.
- Native Butcher pursuit now has an explicit server-owned AI boundary: the
  monster uses deterministic native `RandomWalk` candidates until adjacent,
  then uses the shared authoritative melee path.
- Native SkeletonMelee movement now uses the server-owned `35 - 4 * intelligence`
  admission band, delaying or using deterministic `RandomWalk` candidates
  before shared melee.
- Native Fallen normal-state movement and adjacent attack now use the shared
  SkeletonAi `35 - 4 * intelligence` and `20 + 2 * intelligence` bands;
  server-owned fear movement remains the higher-priority state.
- Native SkeletonRanged now has a server-owned four-tile retreat boundary and
  ranged-only idle path; generic melee is no longer admitted for validated bows.
- Native GoatMelee non-adjacent movement now uses a server-owned
  `28 + 2 * intelligence` avoidance admission before shared melee resolution;
  distance-four-or-greater admissions retain the native one-in-four
  RoundWalk branch through deterministic lateral movement.
- Native Fat/Overlord non-adjacent movement now uses a server-owned
  `20 + 4 * intelligence` admission and deterministic `RandomWalk` candidate
  order before shared melee resolution.
- Native Stone Curse now retains a defeated petrified target through its
  authoritative shatter phase; ordinary defeated monsters become persisted
  corpse tiles, and snapshots preserve the distinction between the two states.

- Handshake validation, bounded framing, retry/deduplication, reconnect tokens,
  acknowledgements, snapshots, events, and state hashes.
- Network snapshots now atomically capture every assigned participant. The
  receiver is first for local-client identity, remote players follow by entity
  ID, and canonical hashing sorts the full set so receiver-relative player
  order cannot create a deterministic mismatch.
- Player movement, occupancy, portals, resources, levels, status effects,
  combat, spells, projectiles, monsters, drops, and world-item pickup.
- Golem death-animation timing is externalized from the native 12-frame data;
  remaining terminal ticks are server-owned and deterministic through minion
  cleanup, snapshots, saves, hashes, and protocol projection.
- Etherealize status state is projected as a typed server-owned effect and
  surfaced by the Godot HUD without client-side immunity or timing logic.
- The checked-in Hellfire pack selects the native game variant through a
  validated `combat.tsv` overlay, adds native-derived dungeon level identities
  3, 4, 9, and 17 through 24 with normalized 40-by-40 bounds, and carries a
  native-derived encounter set plus the Undead Crown, Empyrean Band, Optic
  Amulet, Ring of Truth, Harlequin Crest, Veil of Steel, Arkaine's Valor, and
  Griswold's Edge, Bovine Plate, The Rift Bow, The Butcher's Cleaver, and The
  Needler, The Celestial Bow, Deadly Hunter, Bow of the Dead, The Blackoak Bow,
  and Flamedart, Fleshstinger, Windforce, Eaglehorn, Gonnagal's Dirk, The Defender,
  and Gryphon's Claw, Black Razor, Gibbous Moon, Ice Shank, The Executioner's Blade,
  The Bonesaw, Shadowhawk, Wizardspike, Lightsabre, The Falcon's Talon, Inferno, Doombringer, The Grandfather, The Mangler, Sharp Beak, BloodSlayer, The Celestial Axe, Wicked Axe, Stonecleaver, Aguinara's Hatchet, Hellslayer, Messerschmidt's Reaver, Crackrust, Hammer of Jholm, Civerb's Cudgel, The Celestial Star, Baranar's Star, Gnarled Root, The Cranium Basher, Schaefer's Hammer, Dreamflange, Staff of Shadows, Immolator, Storm Spire, Gleamsong, Thundercall, The Protector, Naj's Puzzler, Mindcry, Rod of Onan, Helm of Spirits, Thinking Cap, OverLord's Helm, Fool's Crest, Gotterdamerung, Royal Circlet, Torn Flesh of Souls, The Gladiator's Bane, The Rainbow Cloak, Leather of Aut, Wisdom's Wrap, Sparking Mail, Scavenger Carapace, Nightscape, Naj's Light Plate, Demonspike Coat, The Deflector, Split Skull Shield, Dragon's Breach, Blackoak Shield, Holy Defender, Stormshield, Bramble, Ring of Regha, The Bleeder, Constricting Ring, Ring of Engagement, Giant's Knuckle, Mercurial Ring, Xorine's Ring, Karik's Ring, Ring of Magma, Ring of the Mystics, Ring of Thunder, Amulet of Warding, Gnat Sting, Flambeau, Armor of Gloom, Blitzen, Thunderclap, Shirotachi, Eater of Souls, Diamondedge, Bone Chain Armor, Demon Plate Armor, Acolyte's Amulet, and Gladiator's Ring
  unique items, and Sorcerer Firebolt staff through Diablo activation and the live Godot protocol
  path, including charged-source validation, charge consumption, and target
  damage; blocked cell generation and remaining Hellfire world and catalog
  tables stay explicit follow-up content migrations.
- Monster type identity is now externalized through validated
  `monster_types.tsv` rows. Base and first-wave Hellfire encounter rows must
  resolve a stable/native type identity before activation. The first
  all one hundred thirty-eight variant-local native Hellfire type identities,
  through native ID 137, are now externalized with native demon, undead, and
  animal class capabilities plus the native knockback ability flag; encounter rows inherit
  validated type resistance, baseline combat, and immutable capability metadata
  unless they provide explicit instance overrides. Variant-local native IDs are
  tracked separately from stable IDs; reverse lookup rejects ambiguous IDs in
  composed overlays. Encounter behavior and the rest of the Hellfire catalog
  remain incremental; native acid resistance is now externalized as a typed
  acid damage channel and persisted through encounters, snapshots, saves,
  hashes, and protocol field 36.
- Those same type rows now carry validated native `MonsterAIID` dispatch
  identities in schema 1.79; identity crosses authoritative targets,
  snapshots, saves, protocol field 43, and canonical hashes while behavior
  dispatch remains server-owned and incremental.
- Usable, identified equipment with native `TripleDemonDamage` now applies the
  validated combat multiplier only against externally classified demons; the
  demon capability persists through snapshots, saves, hashes, and protocol
  field 37.
- Usable, identified equipment with native `Knockback` now moves a surviving,
  non-petrified physical target one valid unoccupied cell away from the
  attacker; the existing authoritative position snapshot/hash boundary owns
  the resulting movement.
- Hellfire `ACAgainstDemons` and `ACAgainstUndead` equipment flags now add the
  native 40/20 armor bonuses before authoritative autonomous monster damage;
  unusable and unidentified magical equipment remains excluded.
- Hellfire `Devastation` now performs the native 5% post-hit triple-damage
  roll on authoritative physical player attacks, consuming its RNG draw only
  after a successful hit and excluding unusable or unidentified magical items.
- Hellfire `Jesters` now applies native 0..200 damage scaling after the
  Devastation boundary in exact Q6 arithmetic; fractional damage remains
  authoritative through target damage and life/mana-on-hit effects.
- Hellfire `Peril` now applies its native pre-modifier self-damage, including
  usable equipment `PlusGetHit`, and applies the validated final physical-hit
  multiplier in Q6; Mana Shield, death handling, and emitted damage events
  remain server-owned.
- Hellfire `Doppelganger` now consumes its validated post-hit proc chance,
  excludes unique and Diablo targets, and spawns a full-health non-unique copy
  on the deterministic last available adjacent cell; the copy is
  snapshot-owned.
- Hellfire `Decay` now applies after an authoritative attack to eligible hand
  weapons in native order, subtracting five damage-bonus points and removing a
  weapon at -100; hit resolution is kept separate from post-armor damage so
  zero-damage hits still preserve native decay behavior.
- Hellfire `THORNS` is now server-owned: eligible usable equipment retaliates
  against a surviving monster after a successful autonomous hit using the
  native 1-3 Q6 roll, before player death handling and knockback.
- Hellfire `DRAINLIFE` is now server-owned: eligible usable equipment applies
  native 4-Q6 damage once per active non-town tick, including Mana Shield and
  native no-item/no-ear death semantics.
- Native `ABSHALFTRAP` is now server-owned at the armed-object boundary:
  eligible usable equipment halves trap damage before the player damage
  resolver, preserving exact Q6 event state.
- Native `FIREDAM` and `LIGHTDAM` equipment are now server-owned at the
  authoritative attack boundary: eligible usable equipment rolls inclusive
  elemental channels, applies target resistance, and emits exact Q6 damage
  events after a successful physical hit.
- Native class `CriticalStrike` is now authoritative for eligible player
  attacks: the level-based roll is consumed only after a successful hit and
  doubles physical damage before armor and later class/item effects. The shared
  action replay checkpoint was refreshed to this native result.
- Native class `IronSkin` is now authoritative in effective armor: the
  validated class flag adds character level divided by its validated divisor
  (legacy rows default to four) after dexterity and usable equipment armor are
  applied, across creation, transfers, and restore.
- Native Monk chest-armor bonuses are now content-owned in schema 1.74 and
  applied after dexterity and usable equipment armor: light armor adds two
  levels, ordinary medium armor adds one-half level, unique medium armor adds
  two levels, and unique heavy armor adds one-half level.
- Monster-type aggro radius is now validated in schema 1.75 and carried into
  procedural targets for autonomous server behavior.
- Autonomous monsters retain their direct native-compatible step order and now
  use a bounded deterministic path fallback when blocked geometry would
  otherwise cause equal-distance backtracking or an unrecoverable stall.
- Procedural monster targets can now inherit validated `monsters.tsv` drop
  policies through the item/unique catalogs; generated world-item IDs, seeds,
  values, and unique/Diablo encounter flags are deterministic and server-owned.
- Procedural target conversion now also preserves the validated monster
  Berserk-immunity capability used by authoritative spell targeting.
- Monster-type death-animation durations are now validated in schema 1.76 and
  inherited by static and procedural targets, preserving native Hellfire
  terminal timing while older packs retain the immediate-cleanup default.
- Monster-type experience rewards are now validated in schema 1.77, persisted
  through authoritative snapshots, and applied at the death boundary instead
  of using one global reward for every content-defined monster.
- Monster-type normal and special attack accuracy are now validated in schema
  1.78; Hellfire normal autonomous attacks use the native level, armor, and
  minimum-hit formula, with accuracy persisted through snapshots and protocol.
- Monster native `MonsterAIID` identities are now validated in schema 1.79 from
  the retained native Hellfire `monstdat.tsv` rows and persisted through
  authoritative targets, snapshots, saves, protocol field 43, and canonical
  hashes. AI-specific special behaviors remain incremental and are not inferred
  by clients.
- The native `Fat` AI normal and special melee bands are now server-owned:
  Toad Demon-family targets use the native `15 + 4 * intelligence` normal and
  `20 + 4 * intelligence` total boundaries with externalized accuracy and damage.
- The native `Rhino` AI charge is now server-owned for clear long-range lanes:
  the authoritative monster moves to the nearest clear cell before the player,
  applies its externalized special damage, and preserves charge knockback and
  blocked-lane fallback through snapshots.
- Native monster intelligence is now externalized in schema 1.80 from the
  retained Hellfire `monstdat.tsv` rows and persists through authoritative
  targets, snapshots, saves, protocol field 44, and canonical hashes. Rhino
  charge probability now uses the native `43 + 2 * intelligence` boundary.
- Native monster missile identities are now externalized in schema 1.81 through
  validated `monster_missiles.tsv` mappings. The active content pack supplies
  each native AI's retained missile ID and projectile behavior; GoatRanged and
  SkeletonRanged `Arrow` rows explicitly select the server-owned `MonsterArrow`
  behavior, while older `Targeted` rows remain compatible. Clients never
  dispatch these mappings.
- Validated ranged mappings now admit their native AI directly from the active
  content pack; the server no longer maintains a separate allow-list for which
  native identities may enter the migrated projectile path. Existing
  AI-specific probability and avoidance branches remain server-owned until
  their remaining semantics are externalized.
- Schema 1.82 now carries the mapped ranged-AI mode and avoidance probability;
  the migrated projectile path consumes those fields instead of identifying
  the `AiRangedAvoidance` identities in code. Native behavior remains
  server-owned, while its selection is content-owned and validated.
- Schema 1.83 adds optional monster-specific missile rows. Native Gloom
  (`Bat`, monster type 42) now consumes a validated `Rhino` mapping; the
  server owns its clear-lane, speed-18, movement-only charge and blocked-lane
  stop while the content row selects the controller.
- Schema 1.84 adds validated Counselor rows for native monster IDs 107, 108,
  109, 110, and 113. The server now owns the corresponding Firebolt, Charged
  Bolt, Lightning, and Fireball projectile paths, including monster-owned
  Charged Bolt player-hit resolution.
- Schema 1.85 adds a validated `Mega` `InfernoControl` mapping. The server now owns
  Mega's native distance/intelligence admission, speed-32 controller, three
  persistent segment emissions, and monster damage rolls through the existing
  authoritative fire collision path.
- Schema 1.86 validates the Hellfire Hork Demon native AI identity and routes its
  `43 + 2 * intelligence` Hork Spawn admission through a native-identity summon
  planner.
- Schema 1.87 externalizes the Hork Spawn missile mapping and preserves its native
  speed-8, nine-tick trajectory and expiry-time level-valid summon search.
- Schema 1.88 externalizes additive Hellfire initial quest-state rows through
  `quest_states.tsv`; route-level activation remains server-owned and duplicate
  quest identities are rejected during activation.
- Schema 1.89 externalizes the Hellfire Defiler encounter, its Cathedral Map
  reward, and its quest completion trigger through validated content rows.
- Schema 1.90 externalizes unique-monster identities through
  `unique_monsters.tsv`; the identity is persisted in snapshots/saves and
  projected through additive protocol fields so unique quest triggers remain
  exact rather than matching every base monster of the same type.
- Schema 1.91 externalizes the mapped monster projectile `spell_id` through
  `monster_missiles.tsv`; checked-in ranged rows now supply their authoritative
  spell identity, while legacy overlays retain the prior inference fallback.
- Schema 1.92 externalizes the Hork Spawn `spawn_monster_id` through
  `monster_missiles.tsv`; the Hellfire row supplies native monster ID 117, and
  current activation rejects Hork Spawn mappings that omit the projection.
- Schema 1.93 externalizes mapped missile `projectile_speed` and
  `projectile_lifetime`; migrated ranged rows now supply their authoritative
  movement/lifetime tuning, including Hork Spawn's native speed-8, nine-tick
  trajectory, while legacy rows retain server defaults.
- Schema 1.94 externalizes Acid's `projectile_lifetime_base` and
  `projectile_lifetime_intelligence_scale`; the shipped Acid mappings now own
  the native intelligence-scaled lifetime, while legacy rows retain the server
  formula fallback.
- Schema 1.95 externalizes Acid puddle duration through
  `puddle_duration_random_range`, `puddle_duration_base`, and
  `puddle_duration_intelligence_scale`; the shipped mappings now own the
  native puddle lifetime, while legacy rows retain the compatibility formula.
- Schema 1.96 externalizes Acid puddle damage through
  `puddle_damage_minimum_monster_level`, `puddle_damage_low`, and
  `puddle_damage_high`; shipped mappings now own the native threshold and
  values, while legacy rows retain compatibility defaults.
- Schema 1.97 externalizes mapped monster-missile `damage_type`; shipped
  damaging rows now declare their authoritative channel, while legacy rows
  retain spell/name inference during migration.
- Schema 1.98 externalizes the Mana spell class multiplier through
  `mana_spell_percent`; shipped classes now own Sorcerer 200-percent and
  Rogue/Bard 150-percent scaling, while legacy class rows default to 100
  percent.
- Schema 1.99 externalizes the Hellfire melee armor reduction through
  `melee_armor_reduction_divisor`; shipped classes now own the Barbarian
  divisor of 8, while legacy class rows retain the disabled-rule default.
- Schema 1.100 externalizes Jester's ten-way spell dispatch through
  `jester_spell_ids`; the shipped row owns the native table, while legacy rows
  retain the compatibility dispatch table.
- The native `FireBat` `AiRanged` boundary now consumes its validated Firebolt
  mapping and resolves server-owned projectile travel plus native
  monster-to-player magic hit, resistance, block, and Q6 damage rules.
- Native Familiar now consumes its monster-specific validated `Bat` Lightning
  mapping: the server preserves Bat movement at range and emits the native
  `GenerateRnd(10) + 1` lightning damage after an admitted adjacent hit.
  Projectile travel, lightning resistance, hit admission, and persistence stay
  server-owned; clients receive only the resulting `MonsterMagic` projection.
- Gharbad's post-dialogue combat now accepts the native `Gharbad` identity and
  delegates to the server-owned `AiAvoidance` boundary, including the native
  23 + 2 * intelligence adjacent-attack band and wounded special branch.
  Unique encounter placement and later multi-step dialogue remain incremental.
- Warlord's post-dialogue combat now accepts the native `Warlord` identity and
  delegates to the server-owned `SkeletonAi` boundary, preserving its native
  adjacent attack and non-adjacent delay/movement bands. Later dialogue-driven
  encounter state remains incremental.
- SnotSpil's post-banner combat now accepts the native `Snotspill` identity and
  delegates to the server-owned `FallenAi`/`SkeletonAi` boundary, including
  native Fallen fear state and combat timing. Banner quest activation and map
  transition behavior remain incremental.
- Zhar and Archbishop Lazarus now accept their native `Zhar` and `Lazarus`
  identities, use validated Fireball mappings, and delegate normal combat to
  the server-owned `CounselorAi` boundary. Later dialogue-driven quest and map
  transitions remain incremental.
- The base pack now supplies additive `quest_states.tsv` rows for the native
  Gharbad, Zhar, Warlord, Lazarus, and Diablo quest states. Server-owned
  completion uses exact unique identities, applies content-owned terminal
  native variables, activates Lazarus's declared follow-on Diablo quest, and
  completes Diablo through an explicit validated boss identity; encounter
  placement, dialogue, and rewards remain incremental.
- A shared `TalkToQuest` intent now validates a living adjacent unique target
  against the quest's configured completion identity, activates/logs the quest,
  applies content-owned minimum native dialogue variables monotonically, and
  emits a typed server-owned dialogue event with the content message ID;
  localized text, unique encounter placement, and later dialogue steps remain
  incremental.
- Native `LazarusSuccubus` minions now use the same validated Blood Star path;
  their retained `LazarusMinionAi` dispatch is represented server-side without
  exposing AI execution to clients.
- The native `Torchant` `AiRanged` boundary now consumes its validated Fireball
  mapping and resolves server-owned travel, impact crawl, and
  monster-to-player explosion damage.
- Native Lich-family flare identities (`Lich`/`OrangeFlare`,
  `ArchLich`/`YellowFlare`, `Psychorb`/`BlueFlare`, and
  `Necromorb`/`RedFlare`) now use the explicit server-owned `MonsterMagic`
  projectile behavior with authoritative travel and monster-to-player magic
  hit resolution; other retained missile identities remain incremental.
- Native `BoneDemon` `AiRangedAvoidance` now consumes its validated
  `BlueFlare2` `MonsterMagic` mapping; the server owns the clear-lane
  500-per-10,000 intelligence admission band and resulting magic hit.
- Native `Storm` `AiRangedAvoidance` now consumes its validated
  `ThinLightningControl` Lightning mapping; the server owns the speed-32
  controller, doubled monster damage, persistent segments, and magic hit.
- Native `Diablo` `DiabloApocalypse` now consumes its validated Apocalypse
  controller mapping; the server emits one physical boom per clear-lane player
  and owns the resulting monster-to-player damage.
- Native `Magma` `MagmaBall` now consumes its validated MonsterMagic mapping;
  the server owns its AiRangedAvoidance admission, speed-16 travel, fire
  resistance/block resolution, and monster-to-player damage.
- Native `Acid`/`AcidUnique` now consume validated Acid mappings; the server
  owns projectile travel, expiry splat, persistent puddle lifetime,
  acid/magic resistance, and per-tick monster-to-player damage.
- Native `GoatRanged` and `SkeletonRanged` now require validated `Arrow`
  identities from `monster_missiles.tsv` before their server-owned clear-lane
  ranged boundaries execute.
- Native player `MultipleArrows` is now represented by authoritative
  `WeaponArrow` projectiles. The server emits the first arrow at the ranged
  attack effect and two spread arrows after the native two-frame delay;
  captured physical Q6 damage, arrow elemental channels, and activation state
  survive protocol snapshots, save/restore, and canonical hashing. Ordinary
  bow attacks now use the same one-arrow travel and impact lifecycle; remaining
  native arrow-item flags remain incremental.
- Native `Snake` now consumes a validated `Rhino` mapping; the server owns its
  clear two-tile charge controller, monster movement, special physical damage,
  blocking, and Q6 event resolution.
- The native `SkeletonRanged` AI clear-lane arrow boundary is now server-owned:
  its `3 + 2 * intelligence` attack band, distance-aware accuracy, damage, and
  blocked-lane fallback resolve through the authoritative directed event path.
- The native `Gargoyle` wounded self-heal boundary is now server-owned: the
  deterministic native heal amount, Healing event, and distance-based special
  lock persist through snapshots, saves, protocol field 45, and canonical
  hashes. Wounded Gargoyles now retreat through a deterministic server-owned
  movement fallback before healing; native animation timing remains incremental.
- The native `GoatRanged` shared `AiRanged` boundary is now server-owned:
  clear-lane arrow damage, close-range avoidance probability, deterministic
  retreat selection, and blocked-lane fallback resolve through the authoritative
  movement and directed-event paths.
- The native `GoatMelee` adjacent `AiAvoidance` boundary is now server-owned:
  its `23 + 2 * intelligence` attack admission and wounded special branch use
  the authoritative physical-hit path and externalized special damage range.
- The native `SkeletonMelee` adjacent attack boundary is now server-owned:
  its `20 + 2 * intelligence` admission is resolved from stable tick/entity
  state before the existing authoritative physical-hit path.
- The native `Zombie` action boundary is now server-owned: its
  `10 + 2 * intelligence` admission and long-range movement threshold are
  resolved before authoritative navigation and adjacent combat.
- Native `ProcessMonsters` regeneration is now server-owned: externalized
  monster levels drive the native one-life or `level / 2` per-tick recovery in
  exact Q6 state before AI execution.
- Native `Succubus` `AiRanged` dispatch is now server-owned: its Blood Star
  projectile uses the retained native missile behavior, magic hit boundary,
  player resistance/block rules, and authoritative Q6 damage.
- Native class melee and ranged to-hit is now authoritative for initialized
  content players: the validated class catalog applies `level + dexterity / 2`
  or `level + dexterity`, respectively, plus each class's external base
  modifier. Legacy hand-built templates with uninitialized dexterity retain the
  global-hit compatibility fallback.
- Initialized physical hit resolution now subtracts target armor and clamps to
  the native 5–95 range. Base Diablo adds `PlusEnemyArmorClass` to to-hit;
  Hellfire applies its native armor shift/quarter rule, including the
  Barbarian melee reduction. Legacy uninitialized templates remain on the
  compatibility path.
- Initialized physical damage now derives its native min/max range from usable
  equipped items and applies class-specific strength/dexterity modifiers.
  Native unarmed, shield, Monk, Rogue, Bard, and Barbarian branches are
  represented in the server-owned profile; legacy uninitialized templates keep
  the global combat-damage fallback.
- Native undead weapon modifiers now apply at the authoritative damage
  boundary: swords deal half damage and maces deal one-and-a-half damage when
  the external monster capability marks the target undead.
- Native animal weapon modifiers now apply at the authoritative damage
  boundary: swords deal one-and-a-half damage and maces deal half damage when
  the external monster capability marks the target animal. Content schema 1.72
  externalizes the capability, which is inherited from validated
  `monster_types.tsv` rows and persists through the
  shared protocol, saves, snapshots, and canonical hashes.
- Native monster `MFLAG_KNOCKBACK` is now externalized through content schema
  1.73 and applied by the authoritative autonomous attack path; a successful
  hit moves the player one valid tile in the monster's facing direction and
  blocked destinations preserve the native position.
- Native `TrapSense` visibility is now recipient-scoped: only a TrapSense
  player receives `TrapArmed` in the network projection, while saves, hashes,
  and internal authoritative snapshots retain the complete armed state; each
  recipient's projected hash matches its visibility boundary.
- External unique and affix catalogs now project every native Hellfire
  damage/armor power identity into the authoritative effect flags,
  so these effects no longer depend on hard-coded content-side interpretation.
- Quest type identity is now externalized through validated `quest_types.tsv`
  rows covering the base and Hellfire native quest IDs. Quest-state rows must
  resolve a declared stable/native identity; quest behavior and remaining
  quest-state data remain incremental.
- Quest randomization pools and native set-level names/identities are now
  externalized through validated `quest_pools.tsv` and `set_level_names.tsv`
  catalogs; activation rejects pool references outside the stable quest catalog,
  and the module applies native deterministic pool exclusions to single-player
  quest state with the historical bad-seed compatibility discard. Excluded
  quests reject progression and linked object interaction and persist through
  snapshots, saves, protocol field 6, and canonical hashes; multiplayer
  activation leaves exclusions disabled. Quest state also preserves the native
  initial/active/done lifecycle and quest-log bit through protocol fields 7-8;
  optional `_qvar1`, `_qvar2`, and `_qmsg` values now flow through protocol
  fields 9-11, saves, snapshots, and compatibility-aware hashes; quest-specific
  behavior now includes an optional validated monster-completion trigger and
  route-level activation of initial quests using explicit session mode;
  linked objects now use validated quest-progress amounts with clamped
  authoritative advancement; Hellfire initial quest-state rows are now
  externalized through `quest_states.tsv` and merged additively with base
  state; the Hellfire Defiler completion trigger and Cathedral Map reward now
  use validated encounter/item rows and its exact unique identity; remaining quest behavior and state
  variables remain incremental.
- Native level-range generation rules are now externalized through validated
  `level_generation.tsv` metadata. Activation checks that every declared world
  level is covered, and the catalog owns deterministic encounter-count
  calculation used by the server-owned spawn planner. The optional
  `procedural_levels.tsv` table now supplies an initial deterministic connected
  layout path; full native map synthesis remains a separate migration.
- Reflect is now a content-selected immediate self effect. The server owns native
  level/character-scaled uint16 charges, consumes one charge per successful
  monster hit, and applies the seeded 20–30% physical return damage; charges
  persist through snapshots, saves, hashes, replay fixtures, and protocol field
  30, while Godot presents the active count and aura.
- Magi is now a content-selected immediate self effect. The server restores the
  caster's current mana to its maximum through the shared authoritative cast
  path, using stable spell ID 40 and native `SpellID` 38.
- Mana is now a content-selected immediate self effect. The server applies the
  native LCG restoration draws, effective-level and class multipliers, Q6
  clamping, and stable spell ID 41/native `SpellID` 37 mapping.
- Ring of Fire is now a content-selected one-tick controller. The server
  preserves native radius-three crawl ordering, solid/object and missile
  line-of-sight filtering, first-blocker termination, shared RNG draw order,
  and Fire Wall segment damage/lifetime behavior through the authoritative
  projectile and protocol state.
- Store stock, purchases, sales, services, equipment, belt, inventory placement,
  item generation, affixes, and unique items for the current shipped slice.
- Native item life/mana bonuses and Hellfire mana-to-life and life-to-mana
  effects apply their Q6 values and independently truncated conversions to newly
  created authoritative player templates and after every accepted equip/unequip
  transfer. The server preserves native resource debt and persists
  base/effective maxima through snapshots, saves, protocol projection, and
  conditional hashes.
- Native usable, identified equipment damage-percent, flat-damage, to-hit, and
  armor-piercing metadata now affects authoritative player attacks with the
  native hit/armor boundary; unequipped, unusable, and unidentified magical
  items remain excluded.
- Server-owned adjacent melee and line-of-sight ranged player targets now use
  the native shield/class block boundary at the authoritative attack effect
  tick and delayed `WeaponArrow` impact; block state, target life, and live
  protocol projection remain server-owned.
- Native usable, identified equipment `PlusGetHit` metadata now adjusts
  authoritative autonomous monster physical hits after hit/armor resolution,
  preserving the configured minimum damage; snapshot state remains server-owned.
- Native usable, identified equipment life/mana-on-hit flags now apply after
  successful authoritative physical attacks, preserving native random life
  steal, 3/5-percent precedence, Q6 resource units, and `NoMana` suppression;
  life gains use the existing authoritative healing event.
- Native equipped item armor and percentage armor bonuses now recompute the
  authoritative player's effective armor, including native dexterity armor and
  identification/requirement rules, across player creation, transfers, and
  snapshot restoration.
- Native usable, identified equipment strength, magic, dexterity, and vitality
  bonuses now recompute authoritative current attributes across player creation,
  transfers, and snapshot restoration.
- Native usable, identified equipment vitality and magic bonuses now contribute
  to authoritative life and mana maxima through each external class's Q6 item
  factors; flat resource bonuses and Hellfire conversions use the same native
  eligibility boundary.
- Native usable, identified equipment `ZeroResistance` flags now reset
  authoritative fire, lightning, and magic resistance after natural class
  resistance and before the native 0..75 clamp; clients consume the resulting
  server-owned damage outcomes.
- Native usable, identified equipment `NoMana` flags now reject ordinary
  memorized spell casts before source consumption while preserving free class
  abilities and charged or consumable sources; the shared damage resolver also
  retains the native Mana Shield bypass.
- The external class and loadout catalogs define all six native-compatible
  Diablo and Hellfire classes, a content-selected default, starting attributes,
  combat metadata, wallets, equipment, belt and inventory placements, class
  skills, and learned spells. Native and Godot clients can select an
  active-content class for a new session; the server creates independent state
  with native Q6 life/mana formulas and deterministic item seeds, while an
  in-memory or saved participant wins on reconnect. Class and spell identity
  flow through simulation state, protocol snapshots, saves, native projection,
  overlays, and cross-language canonical hashes.
- New sessions may also request a native-valid character name. The server owns
  it thereafter and preserves it through participant state, saves, protocol
  field 28, conditional canonical hashes, reconnect, and native/Godot
  projection. Lethal player damage now creates the native-compatible PvP ear
  from that name, class cursor, character level, and external item definition;
  equipment remains equipped and suppressed multiplayer levels omit the ear.
- Objects, declarative object effects, linked quest progress, and multi-level
  entity persistence.
- Versioned server-owned saves with validation and atomic replacement.
- Native and Godot protocol clients plus shared replay and live-server tests.
- A loopback reconnect soak now keeps a second participant connected while the
  first participant resumes through twelve consecutive cycles, preserving the
  resume token, entity identity, complete participant set, self-first ordering,
  and snapshot hash on every cycle.
- The live Godot protocol client now reconnects through eight v0.2 cycles
  against the launched authoritative server while a second client remains
  connected, preserving the resume token and self-first participant ordering.
- The generic host adapter now has an eight-generation replacement soak that
  migrates two sessions through every candidate and retires each prior target
  only after publication, preserving session state at every boundary.
- The Diablo content boundary now exposes stable-ID monster candidates for a
  requested dungeon level, with validated range filtering and deterministic
  ordering; map layout and spawn placement remain explicitly separate.
- The server-owned monster spawn planner now turns those candidates and
  validated level-density metadata into bounded walkable placements with
  stable seed replay. Activation can optionally replace static encounter rows
  with generated combat targets through the standalone server's procedural
  monster options; static rows remain the compatibility default, and native
  map synthesis remains a separate migration. Successful authoritative level
  transitions materialize each destination encounter set once with collision-
  safe reserved IDs and occupancy-aware placement.
- Diablo content activation now exposes the planner directly to the server
  adapter, preserving the module/data boundary for future generated worlds.
- Engine-neutral inventory-grid geometry, footprint validation, placement, and
  index compaction extracted into the dependency-free `Devilution.Domain`
  assembly with an independent test project.
- Canonical deterministic hashing moved into `Devilution.Domain`; generic TSV
  parsing, ordered content packs, manifest identity, and stable content IDs moved
  into the independent `Devilution.Data` assembly with native golden vectors.
- Diablo item/store/combat/monster/spell/world schemas and deterministic store
  rules plus protocol-independent authoritative player/store state moved into
  `Devilution.Diablo`, with the shipped base pack tested without referencing the
  server or protocol.
- Store, service, inventory-placement, and inventory/belt/equipment transfer
  commands now cross protocol-neutral intents and results; the server maps
  Protobuf commands and rejection codes without owning those state transitions.
- Authoritative world collections, walkability, line of sight, occupancy,
  initial-world validation, movement, portals, pickup, object effects, quest
  transitions, and their transient events now live in `Devilution.Diablo`.
  The server maps Protobuf commands, rejection codes, snapshots, and event
  messages around that module-owned boundary.
- Attack and cast intents, typed damage, experience and drop resolution,
  projectiles, status expiry, monster targeting, movement, attacks, and
  per-player transient event delivery now execute inside `Devilution.Diablo`.
  The host only supplies stable player order and authoritative tick numbers.
- Cast admission now verifies an authoritative class ability, learned spell,
  charged left-hand staff, or exact inventory/belt scroll or rune. Abilities are
  free, memorized spells consume mana, charged items lose one charge, and
  consumables are removed only after all target validation succeeds. Native and
  Godot clients send the selected source; source-zero compatibility commands can
  infer only an actually owned source in a stable order.
- The Godot presentation client enumerates those authoritative sources in a
  stable order, lets the player cycle them with `Q`/`E`, and sends the exact
  selected source and item reference. Selection is presentation state only; the
  server still owns admission, timing, targeting, and resource consumption.
- Content activation cross-validates every directly castable starting learned,
  charged-item, scroll, and rune source against `spells.tsv`. Class-skill IDs are
  kept distinct because repair, recharge, trap disarm, and similar native
  capabilities are cursor-driven interactions rather than ordinary casts.
- The base Sorcerer staff's native spell ID 30 is now an external typed Charged
  Bolt definition. One accepted cast consumes its source once and creates
  `4 + floor(effective spell level / 2)` server-owned bolts using the native
  Diablo LCG draw order, Magic-based damage, 16.16 isometric wandering cadence,
  magic hit roll, and lightning resistance/immunity behavior. Gameplay RNG and
  all path/velocity/phase fields persist through snapshots and saves.
- `Devilution.Engine` now owns transport-neutral module identity, dependency
  ordering, transactional typed capabilities, authoritative clock context, and
  ordered ruleset identity. `DiabloModule` registers its store rules and
  simulation factory, which constructs one aggregate over shared Diablo state.
- Generic Engine session coordination now owns deterministic state ordering,
  reverse participant lookup, explicit participant-state registration, and
  per-session transient event queues. Diablo
  owns stable player/world/store snapshot capture and restore validation; a
  dedicated Diablo adapter maps those projections to Protobuf, state hashes,
  events, and the compatible version-1 save envelope.
- The Engine runtime now serializes participant reads, writes, command steps,
  captures, and autonomous advancement; owns the last-applied tick; and routes
  module-directed events. Diablo supplies normalized player templates and
  lifecycle hooks. The Diablo adapter save codec explicitly dispatches format
  version 1 while retaining the existing JSON/base64/Protobuf representation.
- Data now composes ordered external packs with explicit composite-key upserts,
  exact ordered-column checks, deterministic row placement, and atomic candidate
  activation. The standalone server consumes the effective tables for every
  Diablo catalog, and live protocol tests prove overlays change authoritative
  store state and the content-selected default player class.
- Engine now exposes semantic API versions and deterministic module inspection.
  Duplicate IDs, missing or cyclic dependencies, and incompatible API ranges
  produce actionable diagnostics before registration; the host supplies the
  trusted candidate set explicitly.
- Modules may constrain each named dependency to an inclusive three-component
  semantic-version range. Preflight rejects non-semantic or out-of-range loaded
  versions before registration, verified package wrappers retain the declared
  requirements, and non-empty constraints extend the ruleset identity without
  changing legacy unconstrained hashes.
- Engine now offers all-or-nothing module activation without changing the
  compatibility `Load` API. A generic ruleset runtime composes Data, activates
  a complete module candidate set, computes its identity, and runs a
  game-specific validation callback before atomically publishing any of them.
  Failed content, compatibility, registration, identity, or target validation
  retains the exact previous activation with a stage-specific diagnostic. The
  standalone server uses this coordinated boundary for initial activation.
- Data now accepts versioned content-schema identities and inclusive semantic
  dependency ranges, hashes them when declared, and preserves every legacy
  content golden vector when omitted. The first-party Diablo module rejects a
  declared schema that does not match its contract.
- Host now owns a format-1 external module-package policy: explicit package
  roots, primary-DLL SHA-256 verification, named-type activation, verified code
  identities, and rejection of private managed/native dependencies. A
  process-level test proves the standalone host loads and reports a package;
  Engine remains unaware of files and reflection.
- `IDiabloContentBootstrap` now owns schema compatibility, all normalized and
  legacy table selection, catalog validation, service rules, default-class and
  default-level selection, and simulation construction. Server `Program`
  supplies generic composed content and player-start settings; a boundary test prevents TSV
  names and Diablo catalog constructors from returning to executable startup.
- A generic Server adapter catalog now contributes built-in module candidates
  and selects exactly one compatible combined command/tick/snapshot/event/save
  adapter. Only the built-in Diablo bridge resolves Diablo capabilities or
  constructs the protocol executor; executable `Program` contains no Diablo
  symbols, enforced by tests.
- Managed Protobuf generation now occurs once in `Devilution.Protocol` and is
  shared by Server and Godot. Generic transport, sessions, scheduling, package
  loading, snapshot hashing, and filesystem persistence live in
  `Devilution.Server.Host`, which has no Diablo dependency. Mapping, compatible
  saves, replay helpers, and the protocol executor live in
  `Devilution.Server.Diablo`; the .NET 10 executable contains only `Program.cs`.
  Exact assembly-reference and schema-ownership tests enforce this graph.

## Architectural debt

The process boundary is authoritative and the intended internal C# dependency
direction is now enforced through separate Domain, Data, Engine, Diablo,
Protocol, generic Host, Diablo adapter, and composition assemblies. All
gameplay transitions in the current vertical slice are module-owned and
composed by a registered `DiabloSimulation` factory. Engine owns generic
session/state coordination; Diablo owns transport-neutral state and validation;
the adapter alone owns Diablo/Protobuf mapping and compatible saves; Host owns
generic network and operational behavior. Remaining debt is behavioral and
extensibility work rather than a collapsed project boundary: the adapter still
retains the legacy-named protocol executor and replay helpers, format-1 packages
provide gameplay modules rather than host adapters, live ruleset replacement
  now exposes transactional candidate preparation, a host operation barrier,
  save-based session migration, handshake identity publication, generation
  leases, and explicit retired-target disposal; broader reload fixtures and
  authoritative content/gameplay coverage are still partial. Static content
  portal routes remain protocol-visible without perturbing legacy replay hashes;
  owned, expiring portal state is hash-visible, and the base-content replay
  golden vectors track the current manifest. Cast source ownership and immediate
resource consumption are now authoritative, and every directly castable base
loadout source resolves during activation, but the spell catalog still models
only the current healing, damage, area, status, and projectile slice. Charged
Bolt count, creation RNG order, Magic-based damage, wandering movement, magic
hit chance, Q6-before-resistance damage, fractional monster health, events,
persistence, separate missile geometry, swept tile checks, stationary-occupant
suppression, external object pass-through/breakability/destruction, and shared
C++/C# spawn, resolution, object-collision, and moving-occupancy checkpoints are
now covered. Moving monsters preserve current and future occupancy, normal
walkers collide only at their current tile, petrified walkers freeze and become
hittable at both tiles, and death cancels movement. This state crosses
snapshots, saves, hashes, native signed occupancy, and replay validation.
Firebolt now has a content-selected native behavior rather than the provisional
targeted-projectile path. The server derives class- and variant-adjusted Q6
mana, seeded magic/spell-level damage, 16.16 speed-26 movement, swept collision,
magic hit chance, quarter resistance damage, and impact expiry from schema 0.9
content. Protocol-neutral snapshots, version-1 saves, canonical hashes, parser
validation, and a shared three-checkpoint C++/C# replay preserve its exact
fractional-health and RNG boundary. The shared ranged Hellfire replay now binds
to the composed base-plus-Hellfire manifest and loads its effective overlaid
combat and class tables in the C# executor; the native replay runner passes the
same fixture and checkpoints.
Lightning now uses a content-selected invisible controller and persistent
stationary segments. Schema 0.10 externalizes its speed, controller lifetime,
per-update randomized Q6 damage, positive-resistance divisor, and segment
lifetime scaling. The server preserves same-tick segment processing, repeated
collision, magic-hit and petrification RNG, PvP damage, and the native rule that
a blocking object stops the controller without breaking it. Effective spell
level and all controller/segment state cross protocol field 29, snapshots,
saves, hashes, native projection, and a shared four-checkpoint C++/C# replay.
Flash now uses two content-selected, runtime-owned stationary layers centered on
the player. Schema 0.11 externalizes its 19-tick lifetime, independent
character-level damage rolls, effective-level scaling, post-scale bonus, magic
resistance divisor, and mana tuning. The server preserves the native six-cell
Bottom and three-cell Top ordering, repeated exact-Q6 collision, petrification
and hit RNG, non-expiring wall/object collision, breakable-object destruction,
PvP rules, and the invulnerability window from the first processed layer through
final removal. Layer state and magic resistance cross snapshots, saves, hashes,
protocol projection, strict replay parsing, and a shared four-checkpoint C++/C#
timeline.
Healing now uses a content-selected immediate self effect. Schema 0.12
externalizes the initial, per-character-level, and per-effective-spell-level
random rolls, Q6 scale, and class multiplier. The server preserves native mana,
RNG, and full-life cast consumption, caps only the applied healing, and carries
the exact applied Q6 amount through an additive event field without changing the
legacy native amount. A shared three-checkpoint C++/C# replay proves uncapped,
capped, and already-full casts from one continuous RNG stream.
Teleport now uses a content-selected invisible movement controller. Schema 0.13
activates its exact mana tuning and stationary two-tick lifetime. The server
preserves native radius-0-through-5 `DoCrawl` order, static, monster, object,
and participant occupancy, source-preserving fizzle, and destination
revalidation after the monster phase before moving the player. Controller and
player state cross snapshots, version-1 saves, existing protocol fields,
native/Godot presentation, and a shared four-checkpoint C++/C# replay.
Resurrection now uses a content-selected immediate player-target effect plus a
runtime-owned beam. Schema 0.14 externalizes its fixed-minimum mana, native
range, 16-tick beam lifetime, revived life, life scale, and radius-50 placement
limit. The server preserves native other-player admission, living-target beam
behavior, dead-player action cancellation, zero-mana and ten-life reset,
initial-offset and crawl placement, actor/object/static occupancy, and portal
avoidance during the fallback crawl. Complete participant and beam state cross
snapshots, compatible saves, existing protocol fields, Godot presentation,
native target mapping, and a shared five-checkpoint C++/C# replay. The native
adapter retains all remote participant projections for targeting but does not
yet construct full legacy remote-player presentation objects from them.
Mana Shield now uses a content-selected immediate self effect. Schema 0.19
externalizes its native damage-reduction base divisor, per-learned-level step,
and level cap. The first cast consumes its source; an active recast fizzles
without consumption. One module-owned resolver now applies the native Q6
damage-to-mana conversion and depletion reversal to monster, PvP projectile,
and damaging-object paths, including exact-depletion cleanup and NoMana-item
bypass. Active state crosses snapshots, compatible saves, additive protocol
field 29, conditional hashes, native/Godot presentation, and a shared six-
checkpoint C++/C# replay. Level-changing transitions and Resurrection clear it.
Stone Curse now uses a content-selected monster-target controller. Schema 0.20
externalizes its mana adjustment/floor, native crawl radius, duration base/cap/
scale, shatter lifetime, and per-monster immunity. The server preserves the
native no-target fizzle, consumes without refresh on an already-petrified
target, searches both cells of a walking monster, freezes pending movement,
releases a living monster at expiry, and retains petrification through the
dead-target shatter phase. Controller state, effective spell level, immunity,
and occupancy cross snapshots, compatible saves, additive monster field 22,
conditional hashes, native/Godot presentation, and a shared six-checkpoint
C++/C# replay.
Infravision now uses a content-selected immediate self status. Schema 0.21
externalizes its mana adjustment/floor, status identity, base duration, and
per-level duration-growth divisor. The server validates learned, ability,
scroll, and rune sources; consumes the exact accepted source; applies native
class mana rules only when appropriate; unions recast duration; owns expiry;
and clears the typed effect during level-changing portal and restart transitions
without removing generic statuses. Typed behavior crosses snapshots, compatible
saves, additive status field 4, conditional hashes, native/Godot presentation,
and a shared four-checkpoint C++/C# replay.
Phasing now uses a content-selected invisible movement controller. Schema 0.22
externalizes its mana adjustment/floor and minimum/maximum axis offsets. The
server ignores client target coordinates, enumerates the native 36 candidate
tiles against complete authoritative occupancy, consumes one RNG draw only
when candidates exist, and preserves facing. A successful selection creates a
two-tick controller whose first update revalidates through the shared Teleport
crawl; an empty set creates nothing and advances no RNG but still consumes the
accepted source. Controller state, RNG, resources, and resulting position cross
snapshots, compatible saves, existing protocol fields, conditional hashes,
native/Godot projection, and a shared four-checkpoint C++/C# replay.
Fireball now uses a content-selected native projectile and impact controller.
Schema 0.23 externalizes its mana floor, two-roll damage formula, per-level
damage growth, capped speed growth, fire-resistance divisor, and explosion
lifetime. The server preserves swept direct collision, native shifted 3x3
splash ordering, origin-to-cell solid blocking, object breakage, exact Q6
damage, and the 14-tick stationary explosion. Runtime phase, velocity, origin,
damage, RNG, exact health, and remaining lifetime cross snapshots, compatible
saves, existing protocol fields, native/Godot presentation, conditional hashes,
and a shared four-checkpoint C++/C# replay.
Heal Other now uses a content-selected immediate living-player effect. Schema
0.24 reuses Healing's native randomized Q6 formula and mana rules while
externalizing a separate per-class multiplier, including the Monk's distinct
300-percent result. The server validates the stable target ID, other-player,
living, same-level, and range rules before mutation, then owns source
consumption, RNG, cap, exact healing event, and participant snapshot update. A
shared timed three-checkpoint C++/C# multiplayer replay proves the transition.
Search now uses a content-selected immediate self status. Schema 0.25 makes the
Monk's external spell-46 ability executable and externalizes its base,
character-level, and spell-level duration terms. The server preserves native
additive recasts and the signed-int overflow guard, owns expiry and transition
cleanup, and projects typed behavior 2 to Godot item outlines and the native
automap flag. Compatible saves, conditional hashes, and a shared four-checkpoint
C++/C# replay carry the same state. The live Godot protocol suite now verifies
the Monk class loadout, explicit ability source, and authoritative status
projection with a positive duration.
The Bard's Identify skill is now a server-owned item action. Schema 0.26 binds
the loadout's spell-5 declaration to the stable `spell:identify` identity, and
an additive protocol source distinguishes it from paid vendor identification.
The server validates ability ownership and the exact equipment or inventory
item, charges no Gold for the skill, and remains the only writer of identified
state. Native and Godot clients provide selection and presentation only; the
live Godot protocol suite now verifies the equipment path end to end.
The Warrior's Repair skill is now a server-owned item action. Schema 0.27 binds
spell `26` to stable `spell:repair`; the server validates class-skill ownership
and the exact equipment or inventory item, then preserves native level-scaled
random repair, iterative maximum-durability loss, full-item no-op behavior, item
exhaustion, and RNG state. Source zero remains paid full vendor repair, while
native and Godot clients only select an item and render the returned snapshot.
The live Godot protocol suite now verifies the class-ability equipment path and
the fully-repaired native no-op outcome.
The Sorcerer's Staff Recharge skill is now server-owned under schema 0.28.
Stable `spell:recharge` identity `27` binds the live loadout to the item-service
policy, while `spells.tsv` externalizes native staff levels. The server validates
the exact staff, owns the single recharge-strength RNG roll, permanently reduces
maximum charges, and preserves full-item and exhaustion outcomes. Source zero
remains paid full vendor recharge; clients perform selection and presentation
only. The live Godot protocol suite now verifies the charged-cast to class-recharge
sequence and the resulting charge/max-charge projection.
Restart in Town is now a transport-neutral, server-owned terminal-death
transition. Schema 0.15 adds a validated town level, explicit default-start
level, destination tile, one-whole-point Q6 life reset, and transition duration.
The command is admitted only after the Death action has cleared, preserves
inventory, sets mana to zero, clears the nullable active-store state, applies
the destination immediately, and holds NewLevel for one tick before Stand.
Protocol field 27, action kind 6, both presentation clients, version-1 saves,
conditional hashes, and a six-checkpoint C++/C# dungeon-death-to-town replay
carry the same result. The native adapter invokes only the legacy town-loading
presentation after the accepted server snapshot; it does not resolve the
restart locally.
Ordinary combat-death gold handling is now server-owned under schema 0.16.
Validated content selects the divisor, projected stack cap, native Gold
identity, and search radius. Death cancels movement before radius-1-through-50
crawl placement, retains and drops independently truncated halves, assigns
nonzero seeds from the shared RNG, and exposes the results as ordinary world
items. When canonical Gold piles are present, death now follows the native
two-pass inventory order, reuses seeds for whole piles, creates one seed for a
partial split, compacts the grid, and retains the physical odd remainder behind
the truncated wallet cache. Topology-aware pickup tops off piles and uses native
last-row/column placement before recalculating the wallet. If only part of a
world Gold pile fits, the server removes the world entity, retains the original
seed and remaining value as the player's cursor-held item, and excludes that
remainder from the wallet. The held item can be dropped onto a valid nearby
tile relative to the player's reserved movement destination and is also dropped
before Gold when the player dies. Aggregate-only saves
retain the prior compatible balance path. Focused tests cover multi-pile order,
seed reuse, grid compaction, persistence/recovery, placement, malformed saves,
and the legacy full-grid fallback. Shared C++/C# replays hash both the death
topology and a full-inventory partial pickup followed by an identity-preserving
held-item drop.
Schema 0.17 preserves the native multiplayer exception for level 16 and
arena-equivalent maps. The host owns explicit single-player/multiplayer mode,
projects it in `ServerHello`, and passes it through the generic adapter boundary
to the Diablo simulation. Validated level content marks maps that suppress
multiplayer death consequences. On those maps Gold and RNG state remain
unchanged, while a cursor-held item still follows the native unconditional
death-drop path. A shared C++/C# fixture proves the rule without inferring mode
from connected participant count.
New content-backed participants now start with canonical Gold piles instead of
an aggregate-only wallet. The first pile occupies native cell 30, overflow uses
native Gold placement order, and deterministic session-derived nonzero seeds
remain distinct from starting-item seeds. Store purchases and services spend
nonmaximum piles before maximum piles without changing retained seeds;
canonical purchases receive a native-order inventory footprint. Sales top off
existing piles, allocate seeded piles only when needed, compact the sold-item
footprint, reject transactionally when proceeds do not fit, and preserve the
post-death physical/cache remainder. Direct transfers and swaps reject any
result that would place Gold in equipment or the belt. A five-checkpoint shared
C++/C# store replay proves purchase, sale, mana service, pile value/seed, grid,
wallet, and the one RNG draw used to create sale proceeds after an exact-balance
purchase.
Charged Bolt now also resolves player collision with native friendly/global
admission, hit, equipment/class blocking, lightning resistance, exact Q6
damage, and RNG order. Explicit friendly-mode commands are retry-safe, hostile
state crosses protocol, saves, native/Godot controls, and conditional hashes,
and a two-participant C++/C# replay verifies the collision timeline. Player
combat mode is now server-owned and persisted; only Stand and melee Attack may
block, class block/recovery frames and usable speed flags determine transient
durations, same-tick bolts observe the immediate mode change, and a shared
Hellfire replay verifies the expansion-only post-block RNG draw. The activated
module lifecycle now reserves player walk destinations, defers melee/ranged
damage and spell resource/effect resolution to native action frames, permits
attack/cast chaining only at native boundaries, and distinguishes base-game
from Hellfire bow-speed behavior. Current-action state crosses module snapshots,
protocol field 25, version-1 saves, conditional hashes, and native/Godot
projection. A shared C++/C# command timeline verifies deferred movement, melee
damage, attack-to-cast chaining, spell consumption/effects, and cleanup at seven
checkpoints. Two additional shared timelines carry a fast Bow as hashed initial
equipment and prove nonadjacent ranged admission plus the one-tick Diablo versus
Hellfire effect/ready-boundary difference. Focused module tests retain broader
melee speed-tier coverage. Lethal monster or Charged Bolt damage now cancels an
in-progress action, starts a content-timed Death action, and leaves the player
in terminal Death mode after animation cleanup. Portal use applies the
authoritative destination behind a one-tick NewLevel action barrier before
returning a living player to Stand. Both action kinds cross protocol field 25,
version-1 saves, conditional hashes, and native/Godot validation. Shared C++/C#
timelines prove autonomous monster approach, movement cancellation, the
content-owned death duration, terminal cleanup, one-stack Gold topology,
placement and RNG state, immediate portal destination, and transition cleanup.
Server-owned facing now follows accepted move, attack, and resolved cast
directions and crosses protocol field 27, saves, hashes, native projection, and
Godot validation. On ordinary monster-or-trap death, equipment is removed in
native Head-through-Chest slot order while facing rotates left once per slot;
each item uses its preferred adjacent tile before the shared crawl, preserving
seed and full state without RNG. PvP and special-level multiplayer deaths retain
equipment. A shared two-checkpoint C++/C# replay proves the rotated positions,
occupied-tile fallback, and identified-value world price. Lethal player damage
now retains equipment and creates one native-compatible ear on the shared death
crawl. The victim name deterministically supplies create-info and seed, the
victim level supplies value, the activated class supplies cursor graphic, and
schema-0.18 combat/item content supplies stable item identity. Runtime names
and cursor cross protocol, saves, both clients, and conditional hashes; a shared
C++/C# replay proves death, exact ear state, placement, and unchanged ear-drop
RNG. Remaining spell scaling and other class modifiers and targeting edge cases
remain migration work. Godot now presents the complete authoritative spell
source list, preserves source-key selection across snapshots, and allows direct
selection alongside Q/E cycling; the six cursor-driven class capabilities are
now server-owned and covered by protocol tests.

Content schema 0.8 moves block/recovery timing out of `classes.tsv` and imports
the complete native six-class animation matrix into validated
`class_animations.tsv`. Weapon-specific attack/action pairs and walk, death,
cast, recovery, block, and town frames now share one content-owned source for
the command-lifecycle work instead of remaining native-only presentation data.

Content schema 0.9 adds native spell-mana adjustment, minimum, unit scaling,
and Firebolt damage-scaling columns plus per-class base/Hellfire mana reduction.
The first activated consumer is Firebolt; the same validated fields are now
available as the remaining native spell behaviors are ported.

Content schema 0.10 adds Lightning's character-level random damage, Q6 damage
scale, and secondary-projectile lifetime fields. Validation reserves
`LightningSegment` for runtime creation and rejects incomplete controller
definitions before activation.

Content schema 0.11 adds Flash's repeated-roll count, character-level bonus,
per-spell-level effect scaling, and post-scale percentage fields. Validation
reserves `FlashBottom` and `FlashTop` for runtime creation and rejects incomplete
or non-Magic Flash definitions before activation.

Content schema 0.12 adds Healing's initial, character-level, and spell-level
random ranges and fixed additions plus its Q6 scale, while class rows add the
self-Healing percentage. Validation rejects incomplete Healing behavior and
healing-only tuning attached to any other behavior.

Content schema 0.13 adds the validated Teleport behavior and base spell row.
Teleport accepts only one stationary two-tick controller with no damage,
healing, status, or area effect; destination occupancy remains runtime state
rather than content data.

Content schema 0.14 adds validated Resurrection tuning and the base spell row.
Resurrection accepts no damage, healing, status, or area tuning, requires one
stationary beam with positive lifetime, and validates its life scale and search
radius. `ResurrectionBeam` remains runtime-owned and cannot be selected by a
content row.

Content schema 0.15 adds `default_start` level selection and validated
restart-in-town fields to `combat.tsv`. The base pack now distinguishes town
level 0 from default dungeon level 1 and validates that the configured restart
tile is walkable before activation.

Content schema 0.16 adds validated player-death gold divisor, projected
maximum-stack, native item type/index, and drop-search radius fields to
`combat.tsv`. The module uses the activated values for death, pickup, and save
validation while protocol clients continue to consume generic player and
world-item projections.

Content schema 0.17 adds the validated
`suppress_multiplayer_death_drops` level flag. The server applies it only when
explicit session mode is multiplayer, matching native level-16 and arena
behavior without baking map IDs into engine code.

Content schema 0.18 adds `combat.tsv`'s stable `pvp_ear_item_id`, each class's
validated `pvp_ear_cursor_graphic`, and the normalized native ear row in
`items.tsv`. Activation rejects an ear definition that does not project native
item index 23 and miscellaneous ID 44.

Content schema 0.19 adds the validated Mana Shield behavior and base spell row.
Validation requires an immediate self effect, a safe learned-level divisor, and
exactly one Mana Shield definition; other behaviors cannot carry its tuning.

Content schema 0.20 adds the validated Stone Curse behavior, base spell row,
and `monsters.tsv` immunity flag. Validation constrains its stationary
monster-target controller, duration arithmetic, shatter phase, and crawl radius;
other behaviors cannot carry Stone Curse tuning.

Content schema 0.21 adds the validated Infravision behavior and base spell row.
Validation constrains it to one immediate self status with zero magnitude and
safe duration-growth arithmetic; other behaviors cannot carry Infravision
tuning.

Content schema 0.22 adds the validated Phasing behavior and base spell row.
Validation constrains it to one stationary two-tick controller with bounded,
ordered axis-offset tuning; other behaviors cannot carry Phasing tuning.

Content schema 0.23 adds the validated Fireball behavior and base spell row.
Validation constrains its bounded random-damage work, level-scaled speed,
single fire projectile, radius-one impact, resistance divisor, and persistent
explosion; other behaviors cannot carry Fireball-only tuning.

Content schema 0.24 adds the validated Heal Other behavior, base spell row, and
`heal_other_spell_percent` class column. Validation requires one immediate
living-player effect with native random Q6 scaling and prevents unrelated
behaviors from carrying its healing tuning.

Content schema 0.25 adds the validated Search behavior and base spell row.
Validation requires one zero-magnitude immediate self status with bounded
character- and spell-level duration tuning; unrelated behaviors cannot carry
Search-only columns.

Content schema 0.26 adds the stable `spell:identify` identity required by the
Bard loadout. Activation resolves that symbol into the module's item-service
policy, so external content cannot silently assign the class skill by an
unvalidated host constant.

Content schema 0.27 adds stable `spell:repair` identity `26` for the Warrior
loadout. Activation binds it to the module's native repair policy; the service
uses existing authoritative durability, character-level, and RNG state rather
than client or host constants.

Content schema 0.28 adds stable `spell:recharge` identity `27` for the Sorcerer
loadout and the native `staff_level` spell field. Activation binds the skill to
the authoritative recharge policy, and the content value supplies the exact
character-level divisor used by its shared RNG sequence.

Content schema 0.29 adds the validated Holy Bolt behavior and base spell row,
plus the external `monsters.tsv` `undead` capability. Holy Bolt reproduces the
native level-scaled magic projectile and accepts only explicitly undead
monsters; the capability persists through protocol field 23, compatible saves,
conditional hashes, and future replay state.

Content schema 0.30 adds stable `spell:trap_disarm` identity `28` for the
Rogue's cursor-driven Trap Disarm capability. Armed object state is external,
server-owned, persisted, and cleared only by the authoritative additive
Disarm Trap command; its nondefault state uses a tagged canonical-hash section.
Network projection now exposes that state only to a TrapSense recipient. The
live Godot protocol suite verifies Rogue visibility, non-TrapSense concealment,
an external armed-object overlay, adjacency validation, and server-owned
disarm mutation.

Content schema 0.31 adds stable spell ID `36` for Hellfire Blood Star
(`SpellID` `35`).
Its external behavior owns the fixed magic damage formula, quarter-resistance
resolution, projectile lifetime/speed, and immediate physical self-damage;
server-owned casts retain those effects through the same source and persistence
boundaries as other authoritative spells.

Content schema 0.32 adds stable spell ID `38` for Bone Spirit (`SpellID` `36`). Its external
behavior owns the magic homing projectile, nearest-monster retarget, current-
life-third damage, resistance, seven-tick impact phase, and six-point physical
self-damage while preserving the shared authoritative source and persistence
boundaries.

Content schema 0.33 adds stable spell ID `6` for Fire Wall (`SpellID` `6`). Its external
behavior owns the seven-tick wall controller, perpendicular branch growth with
independent stopping, persistent typed segments, repeated shifted fire damage,
quarter resistance, native duration scaling, and all branch state needed for
deterministic protocol, save, hash, and replay continuation.

Content schema 0.34 adds stable spell ID `15` for Flame Wave (`SpellID` `15`). Its external
behavior owns the one-tick perpendicular wave placement controller and moving
fire segments, including native facing-direction travel, branch count scaling,
random character-level damage, missile blocking, resistance, lifetime, and
deterministic persisted projectile state.

Content schema 0.35 adds stable spell ID `21` for Inferno (`SpellID` `20`). Its external
behavior owns the speed-32 controller, native three-segment cap, stepwise
segment lifetime, character-level and two-value damage draws, repeated fire
collision, and deterministic persisted controller/segment state.

Content schema 0.36 adds stable spell ID `16` for Chain Lightning (`SpellID` `14`). Its external
behavior owns the one-tick native fan-out, crawl-radius target enumeration, and
Lightning-compatible bolt/segment state, including source spell level, damage,
resistance, lifetime, collision, and deterministic RNG ordering.

Content schema 0.39 adds stable spell ID `22` for Hellfire Rage (`SpellID` `22`). Its external
behavior uses typed controller value `34`: the server preserves the native
character-level active and cooldown durations, rejects recasts while either
phase exists, applies the Q6 self-damage floor at cooldown expiry, and carries
the controller phase and remaining ticks through snapshots and protocol. The
live Godot protocol suite now verifies the Barbarian class loadout, explicit
ability source, active controller projection, and server-owned life boundary.

Content schema 0.40 adds stable spell ID `31` for Elemental (`SpellID` `29`). Its external
behavior uses typed projectile value `35`: the server preserves the native
first-destination retarget, 14-tick nine-cell fire burst, fire resistance
divisor, and burst-origin line-of-sight state through snapshots and protocol.

Content schema 0.45 adds stable Reflect spell ID `43` (native `SpellID` `43`).
The server owns native level/character-scaled uint16 charges, seeded 20–30%
physical return damage on monster hits, and persisted protocol/save/hash state.

Content schema 0.46 adds stable Magi spell ID `40` (native `SpellID` `38`).
The server owns its immediate current-mana-to-maximum effect through the shared
validated cast-source path.

Content schema 0.47 adds stable Mana spell ID `41` (native `SpellID` `37`).
The server owns native LCG draws for base, character-level, and spell-level
restoration, including Sorcerer/Rogue/Bard multipliers and Q6 maximum clamping.

Content schema 0.56 adds Etherealize (stable spell ID `53`, native `SpellID`
`26`). The server owns its level-scoped status identity, transition cleanup,
protocol/snapshot/hash persistence, and monster-damage immunity.
Stable player loadouts, saves, and replay state now retain the authoritative
stable identity separately from the legacy native-ID ceiling. The live Godot
protocol suite verifies Etherealize selected through an external loadout,
memorized-source admission, and typed status projection.

Content schema 0.68 adds native acid resistance to `monster_types.tsv` and the
authoritative damage-channel, encounter, snapshot, save, hash, and protocol
paths. The four Hellfire acid creatures retain native 100-percent immunity.
Content schema 0.69 adds the native-derived Hellfire Griswold's Edge base item
and unique definition (`unique_id=1010`) with its one-handed sword identity,
fire-damage, hit, attack-speed, knockback, mana, and life-curse powers.
Content schema 0.70 adds the native-derived Hellfire Bovine Plate base item and
unique definition (`unique_id=1011`) with its heavy-armor identity, armor,
indestructibility, light, resistance, mana-curse, and spell-level powers.
Content schema 0.71 adds the native-derived Hellfire The Rift Bow unique
definition (`unique_id=1012`), reusing the validated Short Bow base item with
native arrow-velocity, damage, and dexterity-curse powers.
Content schema 0.72 adds the native-derived Hellfire The Butcher's Cleaver base
item and unique definition (`unique_id=1013`) with its two-handed axe identity,
strength, fixed-damage, and durability powers.
Content schema 0.73 adds the native-derived Hellfire The Needler unique
definition (`unique_id=1014`), reusing the validated Short Bow base item with
native hit, fixed-damage, and attack-speed powers.
Content schema 0.74 adds the native-derived Hellfire The Celestial Bow base
item and unique definition (`unique_id=1015`) with its long-bow identity,
minimum-strength, damage, and armor powers.
Content schema 0.75 adds the native-derived Hellfire Deadly Hunter base item
and unique definition (`unique_id=1016`) with its composite-bow identity,
triple-damage, hit, and magic-curse powers.
Content schema 0.76 adds the native-derived Hellfire Bow of the Dead unique
definition (`unique_id=1017`), reusing the validated Composite Bow base item
with native hit, attribute, curse, and durability powers.
Content schema 0.77 adds the native-derived Hellfire The Blackoak Bow unique
definition (`unique_id=1018`), reusing the validated Long Bow base item with
native attribute, damage, and light/vitality-curse powers.
Content schema 0.78 adds the native-derived Hellfire Flamedart base item and
unique definition (`unique_id=1019`) with its Hunter's Bow identity, fire-arrow,
hit, and fire-resistance powers.
Content schema 0.79 adds the native-derived Hellfire Fleshstinger unique
definition (`unique_id=1020`), reusing the validated Long Bow base item with
native dexterity, hit, damage, and durability powers.
Content schema 0.80 adds the native-derived Hellfire Windforce base item and
unique definition (`unique_id=1021`) with its War Bow identity, strength,
damage, and knockback powers.
Content schema 0.81 adds the native-derived Hellfire Eaglehorn base item and
unique definition (`unique_id=1022`) with its Battle Bow identity, dexterity,
hit, damage, and indestructibility powers.
Content schema 0.82 adds the native-derived Hellfire Gonnagal's Dirk base item
and unique definition (`unique_id=1023`) with its Dagger identity,
dexterity-curse, damage-modifier, attack-speed, and fire-resistance powers.
Content schema 0.83 adds the native-derived Hellfire The Defender base item and
unique definition (`unique_id=1024`) with its Sabre identity, armor, vitality,
and hit-curse powers.
Content schema 0.84 adds the native-derived Hellfire Gryphon's Claw base item
and unique definition (`unique_id=1025`) with its Falchion identity, damage,
magic-curse, and dexterity-curse powers.
Content schema 0.85 adds the native-derived Hellfire Black Razor unique
definition (`unique_id=1026`), reusing the validated Dagger base item with
native damage, vitality, and durability powers.
Content schema 0.86 adds the native-derived Hellfire Gibbous Moon base item and
unique definition (`unique_id=1027`) with its Broad Sword identity, attribute,
damage, mana, and light-curse powers.
Content schema 0.87 adds the native-derived Hellfire Ice Shank base item and
unique definition (`unique_id=1028`) with its Long Sword identity, fire
resistance, durability, and strength powers.
Content schema 1.68 adds the native-derived Hellfire Demon Plate Armor unique
definition (`unique_id=1109`), reusing the native Full Plate identity with fixed
armor and extra armor against demons.

Content schema 1.69 adds the native-derived Hellfire Acolyte's Amulet unique
definition (`unique_id=1110`), reusing the native Amulet identity and preserving
the player-dependent 50-percent mana-to-life conversion as item metadata.

Content schema 1.70 adds the native-derived Hellfire Gladiator's Ring unique
definition (`unique_id=1111`), reusing the native Ring identity and preserving
the player-dependent 40-percent life-to-mana conversion as item metadata.

Content schema 1.67 adds the native-derived Hellfire Bone Chain Armor unique
definition (`unique_id=1108`), reusing the native Chain Mail identity with
fixed armor and extra armor against undead.

Content schema 1.66 adds the native-derived Hellfire Diamondedge unique
definition (`unique_id=1107`), reusing the native Long Sword identity with
fixed durability, to-hit, damage, resistance, and fixed-armor powers.

Content schema 1.65 adds the native-derived Hellfire Eater of Souls unique
definition (`unique_id=1106`), reusing the native Two-Handed Sword identity
with indestructibility, life, life-steal, mana-steal, and drain-life powers.

Content schema 1.64 adds the native-derived Hellfire Shirotachi unique
definition (`unique_id=1105`), reusing the native Great Sword identity with
one-handed, fastest-attack, target-armor, and lightning-damage powers.

Content schema 1.63 adds the native-derived Hellfire Thunderclap unique
definition (`unique_id=1104`), reusing the native War Hammer identity with
charged-bolt hit damage, strength, resistance, light, and indestructible powers.

Content schema 1.62 adds the native-derived Hellfire Blitzen unique definition
(`unique_id=1103`), reusing the native Composite Bow identity with
lightning-arrow damage, zero base damage, and indestructible powers.

Content schema 1.61 adds the native-derived Hellfire Armor of Gloom unique
definition (`unique_id=1102`), reusing the native Full Plate identity with zero
strength requirement, fixed armor 225, all-resistance reset, and light curse.

Content schema 1.60 adds the native-derived Hellfire Flambeau unique definition
(`unique_id=1101`), reusing the native Composite Bow identity with Fireball-bow
damage, zero base damage, and indestructible powers.

Content schema 1.59 adds the native-derived Hellfire Gnat Sting unique
definition (`unique_id=1100`), reusing the native Hunter's Bow identity with
multi-arrow, damage, fast-attack, and indestructible powers.

Content schema 1.58 adds the native-derived Hellfire Amulet of Warding unique
definition (`unique_id=1099`), using the native AMULET identity with
all-resistance and life-curse powers.

Content schema 1.57 adds the native-derived Hellfire Ring of Thunder unique
definition (`unique_id=1098`), reusing the corrected Ring identity with
lightning resistance and fire/magic-resistance curses.

Content schema 1.56 adds the native-derived Hellfire Ring of the Mystics unique
definition (`unique_id=1097`), reusing the corrected Ring identity with magic
resistance and fire/lightning-resistance curses.

Content schema 1.55 adds the native-derived Hellfire Ring of Magma unique
definition (`unique_id=1096`), reusing the corrected Ring identity with fire
resistance and lightning/magic-resistance curses.

Content schema 1.54 adds the native-derived Hellfire Karik's Ring unique
definition (`unique_id=1095`), reusing the corrected Ring identity with
vitality and magic-curse powers.

Content schema 1.53 adds the native-derived Hellfire Xorine's Ring unique
definition (`unique_id=1094`), reusing the corrected Ring identity with magic
and strength-curse powers.

Content schema 1.52 adds the native-derived Hellfire Mercurial Ring unique
definition (`unique_id=1093`), reusing the corrected Ring identity with
dexterity and strength-curse powers.

Content schema 1.51 adds the native-derived Hellfire Giant's Knuckle unique
definition (`unique_id=1092`), reusing the corrected Ring identity with
strength and dexterity-curse powers.

Content schema 1.50 adds the native-derived Hellfire Ring of Engagement unique
definition (`unique_id=1091`), reusing the corrected Ring identity with
get-hit, thorns, armor, and target-armor powers.

Content schema 1.49 adds the native-derived Hellfire Constricting Ring unique
definition (`unique_id=1090`), reusing the corrected Ring identity with
all-resistance and life-drain powers.

Content schema 1.48 adds the native-derived Hellfire The Bleeder unique
definition (`unique_id=1089`), reusing the corrected Ring identity with magic-
resistance, mana, and life-curse powers.

Content schema 1.47 adds the native-derived Hellfire Ring of Regha unique
definition (`unique_id=1088`), reusing the corrected Ring identity with magic,
magic-resistance, light, strength-curse, and dexterity-curse powers.

Content schema 1.46 adds the native-derived Hellfire Bramble unique definition
(`unique_id=1087`), reusing its Ring identity with cursed attributes, damage
modifier, and mana powers.

Content schema 1.45 adds the native-derived Hellfire Stormshield base item and
unique definition (`unique_id=1086`) with its Gothic Shield identity, armor,
get-hit, strength, indestructible, fast-block, and lightning-resistance powers.

Content schema 1.44 adds the native-derived Hellfire Holy Defender base item
and unique definition (`unique_id=1085`) with its Large Shield identity, armor,
get-hit, fire-resistance, durability, and fast-block powers.

Content schema 1.43 adds the native-derived Hellfire Blackoak Shield base item
and unique definition (`unique_id=1084`) with its Small Shield identity, armor,
dexterity, vitality-curse, light-curse, and durability powers.

Content schema 1.42 adds the native-derived Hellfire Dragon's Breach base item
and unique definition (`unique_id=1083`) with its Kite Shield identity, armor,
fire-resistance, strength, magic-curse, and indestructible powers.

Content schema 1.41 adds the native-derived Hellfire Split Skull Shield unique
definition (`unique_id=1082`), reusing its Buckler identity with armor, life,
strength, light-curse, and durability powers.

Content schema 1.40 adds the native-derived Hellfire The Deflector base item
and unique definition (`unique_id=1081`) with its Buckler identity, armor,
resistance, damage-curse, and to-hit-curse powers.

Content schema 1.39 adds the native-derived Hellfire Demonspike Coat base item
and unique definition (`unique_id=1080`) with its Full Plate Mail identity,
armor, get-hit, strength, indestructible, and fire-resistance powers.

Content schema 1.38 adds the native-derived Hellfire Naj's Light Plate base
item and unique definition (`unique_id=1079`) with its Plate Mail identity,
no-strength, magic, mana, all-resistance, and spell-level powers.

Content schema 1.37 adds the native-derived Hellfire Nightscape base item and
unique definition (`unique_id=1078`) with its Cape identity, fast-recovery,
light-curse, armor, dexterity, and all-resistance powers.

Content schema 1.36 adds the native-derived Hellfire Scavenger Carapace base
item and unique definition (`unique_id=1077`) with its Breast Plate identity,
armor, get-hit, armor-curse, dexterity, and light-resistance powers.

Content schema 1.35 adds the native-derived Hellfire Sparking Mail base item
and unique definition (`unique_id=1076`) with its Chain Mail identity, armor,
and lightning-damage powers.
Content schema 1.34 adds the native-derived Hellfire Wisdom's Wrap base item
and unique definition (`unique_id=1075`) with its Robe identity, magic, mana,
light-resistance, armor, and get-hit powers.
Content schema 1.33 adds the native-derived Hellfire Leather of Aut base item
and unique definition (`unique_id=1074`) with its Leather Armor identity,
armor, strength, magic-curse, dexterity, and indestructible powers.
Content schema 1.32 adds the native-derived Hellfire The Rainbow Cloak base
item and unique definition (`unique_id=1073`) with its Cloak identity, armor,
attribute, resistance, life, and durability powers.
Content schema 1.31 adds the native-derived Hellfire The Gladiator's Bane
base item and unique definition (`unique_id=1072`) with its Studded Leather
Armor identity, armor, get-hit, durability, and cursed-attribute powers.
Content schema 1.30 adds the native-derived Hellfire Torn Flesh of Souls base
item and unique definition (`unique_id=1071`) with its Rags identity, armor,
vitality, get-hit, and indestructible powers.
Content schema 1.29 adds the native-derived Hellfire Royal Circlet base item
and unique definition (`unique_id=1070`) with its Crown identity, attribute,
mana, armor, and light powers.
Content schema 1.28 adds the native-derived Hellfire Gotterdamerung base item
and unique definition (`unique_id=1069`) with its Great Helm identity,
attribute, armor, get-hit, resistance, and light-curse powers.
Content schema 1.27 adds the native-derived Hellfire Fool's Crest unique
definition (`unique_id=1068`), reusing the validated Helm base item with
cursed attributes, life, get-hit, and thorns powers.
Content schema 1.26 adds the native-derived Hellfire OverLord's Helm unique
definition (`unique_id=1067`), reusing the validated Helm base item with
attribute, magic-curse, and durability powers.
Content schema 1.25 adds the native-derived Hellfire Thinking Cap base item
and unique definition (`unique_id=1066`) with its Skull Cap identity, mana,
spell-level, resistance, and durability powers.
Content schema 1.24 adds the native-derived Hellfire Helm of Spirits base item
and unique definition (`unique_id=1065`) with its Helm identity and life-steal
power.
Content schema 1.23 adds the native-derived Hellfire Rod of Onan unique
definition (`unique_id=1064`), reusing the validated War Staff base item with
charged-spell, damage, and attribute powers.
Content schema 1.22 adds the native-derived Hellfire Mindcry base item and
unique definition (`unique_id=1063`) with its Quarter Staff identity, magic,
charged-spell, all-resistance, and spell-level powers.
Content schema 1.21 adds the native-derived Hellfire Naj's Puzzler unique
definition (`unique_id=1062`), reusing the validated Long Staff base item with
magic, dexterity, all-resistance, charged-spell, and life-curse powers.
Content schema 1.20 adds the native-derived Hellfire The Protector unique
definition (`unique_id=1061`), reusing the validated Short Staff base item with
vitality, get-hit, armor, charged-spell, and thorns powers.
Content schema 1.19 adds the native-derived Hellfire Thundercall base item and
unique definition (`unique_id=1060`) with its Composite Staff identity, to-hit,
lightning-damage, charged-spell, lightning-resistance, and light powers.
Content schema 1.18 adds the native-derived Hellfire Gleamsong base item and
unique definition (`unique_id=1059`) with its Short Staff identity, mana,
strength-curse, vitality-curse, and charged-spell powers.
Content schema 1.17 adds the native-derived Hellfire Storm Spire base item and
unique definition (`unique_id=1058`) with its War Staff identity,
lightning-resistance, lightning-damage, strength, and magic-curse powers.
Content schema 1.16 adds the native-derived Hellfire Immolator unique
definition (`unique_id=1057`), reusing the validated Long Staff base item with
fire-resistance, fire-damage, mana, and vitality-curse powers.
Content schema 1.15 adds the native-derived Hellfire Staff of Shadows base
item and unique definition (`unique_id=1056`) with its Long Staff identity,
magic-curse, to-hit, damage, light-curse, and fast-attack powers.
Content schema 1.14 adds the native-derived Hellfire Dreamflange unique
definition (`unique_id=1055`), reusing the validated Mace base item with magic,
mana, magic-resistance, light, and spell-level powers.
Content schema 1.13 adds the native-derived Hellfire Schaefer's Hammer base
item and unique definition (`unique_id=1054`) with its War Hammer identity,
damage-curse, lightning-damage, life, to-hit, lightning-resistance, and light
powers.
Content schema 1.12 adds the native-derived Hellfire The Cranium Basher unique
definition (`unique_id=1053`), reusing the validated Maul base item with
damage-modifier, strength, indestructibility, mana-curse, and all-resistance
powers. It also corrects Hammer of Jholm's normalized native Maul index.
Content schema 1.11 adds the native-derived Hellfire Gnarled Root unique
definition (`unique_id=1052`), reusing the validated Spiked Club base item with
to-hit, damage, dexterity, magic, all-resistance, and armor-curse powers.
Content schema 1.10 adds the native-derived Hellfire Baranar's Star base item
and unique definition (`unique_id=1051`) with its Morning Star identity,
to-hit, damage, attack-speed, vitality, dexterity-curse, and durability powers.
Content schema 1.9 adds the native-derived Hellfire The Celestial Star base
item and unique definition (`unique_id=1050`) with its Flail identity,
no-strength, light, damage-modifier, and armor-curse powers. Content schema 1.8
adds the native-derived Hellfire Civerb's Cudgel unique
definition (`unique_id=1049`), reusing the validated Mace base item with demon
damage, dexterity-curse, and magic-curse powers. Content schema 1.7 adds the
native-derived Hellfire Hammer of Jholm base item
and unique definition (`unique_id=1048`) with its Maul identity, fixed damage,
indestructibility, strength, and to-hit powers. Content schema 1.6 adds the
native-derived Hellfire Crackrust base item and
unique definition (`unique_id=1047`) with its Mace identity, attributes,
indestructibility, all-resistance, damage, and spell-level powers. Content
schema 1.5 adds the native-derived Hellfire Messerschmidt's Reaver base
item and unique definition (`unique_id=1046`) with its Great Axe identity,
damage, damage-modifier, attribute, life-curse, and fire-damage powers. Content
schema 1.4 adds the native-derived Hellfire Hellslayer unique definition
(`unique_id=1045`), reusing the validated Battle Axe base item with strength,
vitality, damage, life, and mana-curse powers. Content schema 1.3 adds the
native-derived Hellfire Aguinara's Hatchet base item
and unique definition (`unique_id=1044`) with its Small Axe identity,
spell-level, magic, and magic-resistance powers. Content schema 1.2 adds the
native-derived Hellfire Stonecleaver unique definition
(`unique_id=1043`), reusing the validated Broad Axe base item with life, to-hit,
damage, and lightning-resistance powers. Content schema 1.1 adds the
native-derived Hellfire Wicked Axe unique definition
(`unique_id=1042`), reusing the validated Large Axe base item with to-hit,
dexterity, vitality-curse, hit-curse, and indestructibility powers. Content
schema 1.0 adds the native-derived Hellfire The Celestial Axe base item
and unique definition (`unique_id=1041`) with its Battle Axe identity, no-strength
requirement, to-hit, life, and strength-curse powers. Content schema 0.99 adds
the native-derived Hellfire BloodSlayer base item and
unique definition (`unique_id=1040`) with its Broad Axe identity, damage, demon
damage, all-attribute-curse, and spell-level-curse powers. Content schema 0.98
adds the native-derived Hellfire Sharp Beak unique
definition (`unique_id=1039`), reusing the validated Large Axe base item with
life, magic-curse, and mana-curse powers. Content schema 0.97 adds the
native-derived Hellfire The Mangler base item and
unique definition (`unique_id=1038`) with its Large Axe identity, damage,
dexterity-curse, magic-curse, and mana-curse powers. Content schema 0.96 adds
the native-derived Hellfire The Grandfather base item
and unique definition (`unique_id=1037`) with its Great Sword identity,
one-handed, attribute, to-hit, damage, and life powers. Content schema 0.95 adds
the native-derived Hellfire Doombringer base item and
unique definition (`unique_id=1036`) with its Bastard Sword identity, to-hit,
damage, all-attribute-curse, life-curse, and light-curse powers. Content schema
0.94 adds the native-derived Hellfire Inferno unique definition
(`unique_id=1035`), reusing the validated Long Sword base item with fire damage,
light, mana, and fire-resistance powers. Content schema 0.93 adds the
native-derived Hellfire The Falcon's Talon base
item and unique definition (`unique_id=1034`) with its Scimitar identity,
fast-attack, to-hit, damage-curse, and dexterity powers. Content schema 0.92
adds the native-derived Hellfire Lightsabre unique
definition (`unique_id=1033`), reusing the validated Sabre base item with
light, lightning-damage, to-hit, and lightning-resistance powers. Content schema
0.91 adds the native-derived Hellfire Wizardspike unique
definition (`unique_id=1032`), reusing the validated Dagger base item with
magic, mana, to-hit, and all-resistance powers. Content schema 0.90 adds the
native-derived Hellfire Shadowhawk base item and
unique definition (`unique_id=1031`) with its Broad Sword identity, light-curse,
life-steal, to-hit, and all-resistance powers. Content schema 0.89 adds the
native-derived Hellfire The Bonesaw base item and
unique definition (`unique_id=1030`) with its Claymore identity, damage-modifier,
strength, magic/dexterity-curse, life, and mana-curse powers. Content schema 0.88
adds the native-derived Hellfire The Executioner's Blade
unique definition (`unique_id=1029`), reusing the validated Falchion base item
with native damage, life-curse, light-curse, and durability powers.
Content schema 0.67 adds native monster fire, lightning, and magic resistance
percentages to `monster_types.tsv`; encounter rows inherit those values unless
they provide an explicit per-instance override. Content schema 0.66 adds
validated quest pools and native set-level names and
restores stable set-level identities in quest type rows. Single-player
activation now applies the deterministic pool exclusions to server-owned quest
state, while multiplayer activation preserves the full pool and skips quest
types marked `single_player_only`. Quest state also
preserves native initial/active/done and quest-log flags plus optional route
activation, `_qvar1`, `_qvar2`, `_qmsg`, monster-completion triggers, and linked
object progress amounts through the same
authoritative persistence boundary. Content schema 0.65
adds validated native level-range generation metadata in
`level_generation.tsv`, including monster density, multiplayer scaling, and
trap percentages. Content schema 0.64 adds validated native-compatible `quest_types.tsv`
definitions for all base and Hellfire native quest IDs; quest-state rows now
reject unknown quest identities. Content schema 0.63 adds validated
native-compatible `monster_types.tsv`
definitions for the base encounter and the first six Hellfire monster types;
encounter rows now reject unknown type identities. Content schema 0.61 adds
native-derived Hellfire Optic Amulet and Ring of Truth
base-item and unique definitions. Their item identities, values, native indexes,
and unique powers cross the same validated overlay and activation boundary as
the earlier Hellfire item slice.

Content schema 0.62 adds native-derived Hellfire Harlequin Crest, Veil of
Steel, and Arkaine's Valor item and unique definitions with their native
indices and authoritative armor, resistance, attribute, life, mana, and
recovery powers.

Content schema 0.60 completes validated `consumable_misc_id` spell mappings for
native rune sources. The base pack maps Rune of Lightning ID `48` to stable
Lightning Wall `50`, Greater Rune of Lightning ID `49` to Nova `18`, Greater
Rune of Fire ID `50` to Immolation `45`, and Rune of Stone ID `51` to Stone
Curse `8`. Rune of Fire ID `47` now uses a direct BigExplosion-style splash,
native level-zero damage, and radius-nine placement crawl. Loadout validation
and combat resolution use the active spell catalog instead of treating native
misc IDs as stable spell identities.

Content schema 0.59 extended the validated `consumable_misc_id` spell mapping
for the compatible Lightning, Nova, and Immolation rune behaviors.

Content schema 0.58 added the validated `consumable_misc_id` spell mapping and
legacy preservation of all five native rune IDs.

Content schema 0.57 externalizes Golem's native 12-frame death-animation
timing on spell row `52`. Remaining terminal ticks cross snapshots, saves,
hashes, and protocol fields 34-35; the server removes the minion after the
animation completes.

Content schema 0.55 adds Golem (stable spell ID `52`, native `SpellID` `21`).
The server validates targeted summon placement, replaces the caster's prior
owned minion, and persists owner identity plus native life, armor, and damage
state through snapshots, saves, hashes, and protocol field 33. Golems now use
server-owned deterministic nearest-monster targeting, movement, melee damage,
experience attribution, and owner/level lifecycle cleanup. Golem path planning
now routes around blocked cells with deterministic breadth-first selection, and
its idle phase follows the owner-facing native random-walk order through the
authoritative RNG. Its native 12-frame death animation is now externalized and
server-owned through terminal cleanup.

Content schema 0.54 adds Nova (stable spell ID `18`, native `SpellID` `18`). The
existing authoritative radial controller and persistent lightning-ball
projectiles are now available through the shipped validated content row.

Content schema 0.53 adds Lightning Wall (stable spell ID `50`, native `SpellID` `40`). It reuses the authoritative wall-growth controller and persistent
segments, with native lightning damage, duration, and resistance tuning in the
external spell row.

Content schema 0.52 adds Warp (stable spell ID `49`, native `SpellID` `42`).
The server selects the nearest normalized level trigger, crawls up to five
cells around its authored destination while excluding trigger and occupied
tiles, and applies the existing authoritative level-transition action.

Content schema 0.51 adds Berserk (stable spell ID `48`, native `SpellID` `44`).
The authoritative module crawls eligible monsters within radius five, applies
the native resistance coin flip, scales all four persisted damage channels in
native RNG order, and preserves berserked state through snapshots, saves,
protocol, and canonical hashes.

Content schema 0.48 adds stable Ring of Fire spell ID `44` (native `SpellID`
`45`). Its typed controller value `42` performs the native radius-three crawl,
stops at the first missile blocker, consumes the native pre-crawl RNG draws,
and emits persistent Fire Wall segments with the shared native damage/lifetime
tuning.

Content schema 0.44 adds stable Town Portal spell ID `39` (native `SpellID`
`7`). The server performs native five-cell placement, replaces the owner's
previous route, exposes a bidirectional dungeon/town portal, expires it after
100 ticks, and persists the route through snapshots, saves, hashes, and the
shared protocol.

Content schema 0.43 adds stable Telekinesis spell ID `33` (native `SpellID`
`33`). The server applies the native cursor operation to a visible item,
object, or monster through the existing authoritative cast and world rules;
remote pickup, object effects, and one-cell knockback are deterministic.

Content schema 0.42 adds explicit stable/native spell identity mapping through
the optional `native_spell_id` column. `spell_id` remains the authoritative
content identity; legacy item spell names resolve from native IDs through the
activated catalog, preserving intentional pairs such as Guardian `7`/`13`, Holy
Bolt `5`/`31`, and Bone Spirit `38`/`36`.

Content schema 0.41 adds stable spell ID `7` for native Guardian (`SpellID`
`13`). Its external
behavior uses typed controller value `36`: the server owns the valid spawn
search, native duration, six-tile scan cadence, Firebolt emission,
line-of-sight filtering, and deterministic RNG state through snapshots and
protocol.

Content schema 0.38 adds stable spell ID `25` for Apocalypse (`SpellID` `24`). Its external
behavior uses typed controller value `32` and boom value `33`: the server scans
the native eight-tile square row-major, skips player minions and blocked cells,
applies Hellfire missile line-of-sight filtering, and persists cursor/first-hit
state through snapshots and protocol. Content schema 0.37 adds stable spell ID
`18` for Nova (`SpellID` `18`). Its external behavior
owns the one-tick radial controller, native nine-point quarter-radius order,
four mirrored emissions, 36 continuing Lightning balls, damage scaling,
resistance, lifetime, collision, and deterministic projectile state.

## Near-term priorities

1. Expand authoritative combat and spell coverage beyond Firebolt, Holy Bolt,
   Blood Star, Bone Spirit, Fire Wall, Flame Wave, Inferno, Chain Lightning, Nova, Apocalypse, Rage, Elemental, Charged Bolt, Fireball, Lightning, Flash, Healing, Heal Other, Stone Curse,
   Infravision, Search, Phasing, Mana Shield, Magi, Mana, Ring of Fire, Teleport, Telekinesis, Town Portal, Resurrection, Jester, Berserk, Warp, Lightning Wall, and Golem,
   including remaining class modifiers, scaling, and targeting rules.
2. Expand external content coverage, including remaining Hellfire monster,
   quest behavior/state, native-parity procedural level generation, and item metadata and
   authoritative gameplay definitions.
3. Grow the Godot client from a protocol demonstration into a complete playable
   presentation client using production assets.
4. Add the missing replay, reconnect, multiplayer, and long-running soak cases.
5. Broaden host-controlled live ruleset replacement fixtures and safe-retirement
   soak coverage; generalize adapter selection when a second adapter exists.
6. Replace the remaining Lua capability destinations and remove Lua only after
   every removal gate is satisfied.

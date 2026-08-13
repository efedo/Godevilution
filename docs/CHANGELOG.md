# Godevilution Changelog

All notable changes to this fork are documented below.

## 2026-08-12

- Advance the Diablo content schema to 0.59 with external mappings for Rune of
  Lightning, Greater Rune of Lightning, Greater Rune of Fire, and Rune of Stone
  to existing authoritative Lightning Wall, Nova, Immolation, and Stone Curse
  behaviors. Rune of Fire remains queued for direct explosion behavior.

- Advance the Diablo content schema to 0.58 with external rune-source identity
  mapping. The base pack maps Rune of Stone miscellaneous ID `51` to stable
  Stone Curse spell ID `8`; legacy rune IDs are preserved and authoritative
  loadout/combat source resolution now consults the active spell catalog.

- Advance the Diablo content schema to 0.57 with externalized Golem
  death-animation timing. The native 12-frame sequence is server-owned and
  persists remaining terminal ticks through snapshots, saves, hashes, and
  protocol fields 34-35 before the expired minion is removed.

- Advance the Diablo content schema to 0.56 with server-authoritative
  Etherealize (stable spell ID 53, native `SpellID` 26), including its
  level-scoped status, transition cleanup, protocol/snapshot/hash persistence,
  and monster-damage immunity.
- Surface the authoritative Etherealize status in the Godot HUD and extend
  client-model regression coverage for its typed protocol projection.
- Add the checked-in Hellfire content pack, which selects the native game
  variant through a validated `combat.tsv` overlay while keeping remaining
  Hellfire world and catalog metadata separate for later migration slices.
- Expand the native-derived Hellfire encounter set and validate monster
  identity, stats, and faction through composed Diablo activation.
- Add the Hellfire Undead Crown as normalized base/unique content with
  deterministic catalog-generation coverage.
- Make the Hellfire Sorcerer starting loadout use its native Firebolt staff
  through a validated keyed class-loadout overlay, with live Godot protocol
  coverage for charged-source validation, charge consumption, and target damage.
- Extend live Godot protocol coverage through the Bard's server-owned Identify
  ability, proving class-service source validation and equipment-state mutation.
- Extend live Godot protocol coverage through the Sorcerer's server-owned Staff
  Recharge ability, including charged-cast consumption and native max-charge loss.
- Extend live Godot protocol coverage through the Warrior's server-owned Repair
  ability, including class authorization and fully-repaired equipment handling.
- Extend live Godot protocol coverage through the Rogue's server-owned Trap
  Disarm ability, including an external armed-object overlay, adjacency
  validation, and server-owned trap-state mutation.
- Extend live Godot protocol coverage through the Monk's server-owned Search
  ability, including explicit class-ability source validation and typed status
  projection with authoritative duration.
- Extend live Godot protocol coverage through the Barbarian's server-owned Rage
  ability, including explicit class-ability source validation and active
  controller projection with server-owned life state.
- Separate stable authoritative spell-ID bounds from the legacy native-ID
  ceiling, allowing external loadouts, saves, and replay state to carry stable
  Etherealize identity `53` (`native SpellID` `26`).
- Extend live Godot protocol coverage through Etherealize selected by an
  external memorized loadout, including source admission and typed status
  projection.
- Promote the shared ranged Hellfire replay fixture to the composed
  `devilution.base` plus `devilution.hellfire` manifest, so native and C# replay
  execution validate the effective Hellfire overlay rather than only a variant
  label.

- Advance the Diablo content schema to 0.55 with server-authoritative Golem
  (stable spell ID 52, native `SpellID` 21). Targeted summon placement,
  replacement of the caster's prior owned minion, native life/armor/damage
  state, owner identity, snapshot/save/hash/protocol persistence, and focused
  parity coverage are now in the C# module.
- Add deterministic Golem nearest-monster targeting, movement, melee damage,
  experience attribution, and owner/level lifecycle cleanup; native path
  planning now uses deterministic breadth-first routing around blocked cells and
  owner-facing idle wandering.
- Refresh the 22 base-content replay fixtures to the resulting manifest
  identity while preserving all existing replay checkpoints.
- Advance the Diablo content schema to 0.50 with server-authoritative Jester
  (stable spell ID 47, native `SpellID` 39), preserving native 10-way RNG
  dispatch into the existing authoritative spell paths without a second
  client-side rules implementation.
- Advance the Diablo content schema to 0.49 with server-authoritative
  Immolation (stable spell ID 45, native `SpellID` 41), preserving the native
  moving controller, 36-projectile radial burst, level-scaled speed, and
  FireballBow projectile impact path.
- Advance the Diablo content schema to 0.48 with server-authoritative Ring of
  Fire (stable spell ID 44, native `SpellID` 45), preserving native
  radius-three crawl ordering, first-blocker termination, RNG draw order, and
  Fire Wall segment lifecycle through the shared projectile protocol state.
- Refresh the 22 base-content replay fixtures to the resulting manifest
  identity while preserving all existing replay checkpoints.
- Advance the Diablo content schema to 0.47 with server-authoritative Mana
  (stable spell ID 41, native `SpellID` 37), preserving native LCG draws,
  Q6 clamping, effective-level scaling, and class multipliers.
- Refresh the 22 base-content replay fixtures to the manifest identity produced
  by the new Mana definition.
- Advance the Diablo content schema to 0.46 with server-authoritative Magi
  (stable spell ID 40, native `SpellID` 38), restoring current mana to its
  native maximum through the shared immediate-effect cast path.
- Advance the Diablo content schema to 0.45 with server-authoritative Reflect
  charges (spell ID 43, native `SpellID` 43), native level/character scaling,
  seeded 20–30% physical reflection, persisted protocol state, and Godot HUD
  presentation.
- Add same-instance Godot protocol reconnect, preserving the server-issued resume
  token and pending command sequence ledger while safely replacing the transport;
  cover the resumed handshake, snapshot, and retry path with a live transport test.
- Make the Godot presentation observe generation-safe transport closure and
  recover dropped connections with bounded exponential backoff without replacing
  its authoritative model.
- Complete leased-host shutdown ownership: server disposal now releases the
  operation barrier and drains retired adapters, and subsequent operations are
  rejected deterministically.
- Make the Host save contract explicitly expose complete session-state
  migration while retaining the legacy player-save method names; the Diablo
  adapter now documents its versioned snapshot as player-plus-world state.
- Add host-controlled ruleset replacement with transactional candidate
  preparation, save-based session migration, handshake identity publication,
  operation barriers, generation-safe retired-target disposal, and regression
  coverage for failed and concurrent migrations.
- Preserve legacy replay hashes for static catalog portals while hashing owned,
  expiring Town Portal state, and refresh all base-content replay golden vectors
  to the current manifest and authoritative snapshot projections.
- Add generic Engine ruleset generation leases and explicit retired-activation
  draining, keeping replaced targets alive until in-flight operations release
  them while leaving session migration and resource disposal to the host.
- Route the generic server adapter boundary through those leases so commands,
  ticks, snapshots, events, saves, and participant initialization cannot use a
  retired game target mid-operation.
- Expand the Godot cast cursor to enumerate and cycle every authoritative spell
  source—memorized spells, charged staff, consumables, and class ability—while
  preserving explicit server-owned source validation and resource consumption.
- Advance the Diablo content schema to 0.44 with server-authoritative Town
  Portal (spell ID 39, native `SpellID` 7), including native five-cell
  placement, owner replacement, bidirectional dungeon/town use, 100-tick
  expiry, and persisted protocol-visible portal state.
- Advance the Diablo content schema to 0.43 with server-authoritative
  Telekinesis (spell ID 33), covering visible item pickup, object activation and
  effects, one-cell monster knockback, line-of-sight validation, and deterministic
  source/resource handling.
- Advance the Diablo content schema to 0.42 with explicit stable/native spell
  identity mapping. `spells.tsv` now records `native_spell_id`, rejects duplicate
  native identities, and resolves legacy item spell names to stable content IDs
  before cast-source validation.
- Advance the Diablo content schema to 0.41 with the externally validated
  Guardian turret (spell ID 7), preserving its native spawn search, duration,
  arc scan, Firebolt emission, line-of-sight filtering, RNG ordering, and
  resumable controller state.
- Advance the Diablo content schema to 0.40 with the externally validated
  Elemental projectile (spell ID 31), preserving its native first-destination
  retarget, persistent nine-cell fire burst, deterministic damage scaling, and
  resumable projectile state.
- Advance the Diablo content schema to 0.39 with the externally validated
  Hellfire Rage controller (spell ID 22), preserving its native active and
  cooldown phases, character-level duration scaling, minimum-life self-damage,
  and deterministic persisted controller state.
- Advance the Diablo content schema to 0.38 with the externally validated
  Apocalypse row-major controller and persistent Physical boom behaviors (spell
  ID 25), preserving native damage rolls, blocked-cell/minion filtering,
  Hellfire line-of-sight, and deterministic cursor/boom state.
- Advance the Diablo content schema to 0.35 with the externally validated
  Inferno controller and stationary segment behaviors (spell ID 21), preserving
  native speed, three-segment emission, stepwise lifetime, fire damage rolls,
  repeated collision, and deterministic projectile state.
- Advance the Diablo content schema to 0.36 with the externally validated
  Chain Lightning fan-out controller and Lightning-compatible bolt behavior
  (spell ID 16), preserving native crawl targeting, damage, segment lifetime,
  resistance, collision, and deterministic source-level state.
- Advance the Diablo content schema to 0.37 with the externally validated Nova
  radial controller and continuing Lightning-ball behavior (spell ID 18),
  preserving native mirrored fan-out order, damage scaling, resistance,
  lifetime, collision, and deterministic projectile state.
- Advance the Diablo content schema to 0.34 with the externally validated
  Flame Wave controller and moving segment behaviors (spell ID 15), preserving
  native perpendicular placement, facing-direction travel, random fire damage,
  collision, lifetime, and deterministic projectile state.
- Advance the Diablo content schema to 0.33 with the externally validated
  Fire Wall controller and persistent segment behaviors (spell ID 6), preserving
  native perpendicular growth, branch stopping, repeated shifted fire damage,
  resistance, duration, and deterministic persisted wall state.
- Advance the Diablo content schema to 0.32 with the externally validated
  Bone Spirit spell (ID 38), preserving its native homing, current-life-third
  magic damage, resistance, impact lifetime, and physical self-damage rules.
- Advance the Diablo content schema to 0.31 with the externally validated
  Hellfire Blood Star spell (ID 36). Preserve its native fixed magic damage,
  quarter-resistance projectile behavior, 16-tile speed, 256-tick lifetime, and
  five-point physical self-damage through the authoritative C# module.
- Advance the Diablo content schema to 0.30 and add server-owned Holy Bolt and
  Rogue Trap Disarm capabilities.
  Externalize the undead monster capability, preserve native damage and speed
  scaling, pass non-undead monsters through, and carry the capability through
  protocol field 23, compatible saves, and conditional snapshot hashes.
- Refresh the 22 base-content replay fixtures to the current manifest identity
  while preserving their native-compatible snapshot checkpoints, and keep the
  projectile behavior values stable as Holy Bolt is appended to the catalog.
- Externalize the Rogue's Trap Disarm capability identity and armed-object
  state, then route an additive protocol command through the authoritative
  Diablo world module with snapshot, save, and hash coverage.
- Give the shipped base content an explicit versioned manifest and make the
  standalone host prefer it while retaining legacy loading for unmanifested
  external roots.
- Consume authoritative armed-trap state in the Godot presentation client,
  including typed Rogue Trap Disarm command routing and server-only visual
  state changes.
- Move player rejuvenation into the native debug-command registry while
  preserving `dev.player.stats.rejuvenate` as a Lua compatibility alias.
- Move player health and mana adjustment into native debug commands while
  preserving the Lua stat helpers as compatibility aliases.
- Move player attribute max/min helpers into native debug commands while
  preserving their Lua names as compatibility aliases.
- Move player level-up into a native debug command while preserving the Lua
  helper and its high-priority experience command sequence as a compatibility
  alias.
- Move player gold give/take into native debug commands while preserving exact
  inventory-stack behavior and Lua compatibility aliases.
- Move the player spell-level helper into a native debug command while
  preserving learned-spell filtering and the Lua compatibility alias.
- Move automap reveal and hide helpers into native debug commands while
  preserving the Lua compatibility aliases.
- Move level reset into the native debug command registry while preserving
  optional seed selection and the Lua compatibility alias.
- Move dungeon-level warp into the native debug command registry while
  preserving legacy transition modes and the Lua compatibility alias.
- Move quest-level warp into the native debug command registry while
  preserving set-level selection and the Lua compatibility alias.
- Move custom-map warp into the native debug command registry while
  preserving path, dungeon-type, spawn-bound, and transition validation.
- Move selected-item diagnostics into the native debug command registry while
  preserving Lua compatibility and network-pack validation.
- Move towner talk and visit helpers into native debug commands while
  preserving their town checks and Lua compatibility aliases.
- Move level `.dun` export into the native debug command registry while
  preserving the legacy binary layout and Lua compatibility alias.
- Move item and monster spawning helpers into native debug commands while
  preserving their generation, placement, count, and Lua compatibility behavior.
- Move quest activation and diagnostics into native debug commands while
  preserving the `dev.quests` Lua compatibility aliases.
- Move player TRN selection into the native debug-command registry and relabel
  the overlay as a general debug console. Preserve the Lua `dev.player.trn`
  functions as compatibility aliases.
- Move player arrow-effect selection and player inspection into the native
  debug-command registry. Preserve the existing Lua module names as aliases
  while routing the debug console through native handlers.
- Move display tile-data inspection and scroll-view diagnostics into the native
  debug-command registry. Preserve the existing Lua module names as aliases
  while routing the debug console through native handlers.
- Move level-seed inspection and automap search diagnostics into the native
  debug-command registry. Preserve the existing Lua module names as aliases
  while routing the debug console through native handlers.
- Add a deterministic native debug-command registry for core player and display
  toggles. Route those commands through the debug console while retaining the
  Lua modules as a compatibility frontend for the remaining developer REPL.
- Move native sound and table-data refreshes into `ModManager` after active
  mod scripts run. Lua remains a compatibility hook rather than the owner of
  archive, sound, or data reload orchestration.
- Move Hellfire activation from the Lua `init.lua` bootstrap into the native
  declarative mod manager. Preserve archive priorities, game-mode selection,
  and data reload behavior while allowing content-only mods to omit Lua.
- Advance the Diablo content schema to 0.28 and make the Sorcerer's declared
  Staff Recharge skill a server-owned item action. Externalize native staff
  levels, preserve the single recharge-strength RNG roll, permanent
  maximum-charge loss, full-item no-op and exhaustion behavior, retain
  source-zero paid vendor recharge, and route both clients without local
  gameplay mutation.
- Advance the Diablo content schema to 0.27 and make the Warrior's declared
  Repair skill a server-owned item action. Preserve native level-scaled random
  repair strength, permanent maximum-durability loss, no-op and item-exhaustion
  behavior, and RNG sequencing; reuse the additive item-service source while
  retaining source-zero paid vendor repair; and route both clients without
  local gameplay mutation.
- Advance the Diablo content schema to 0.26 and make the Bard's declared
  Identify skill a server-owned item action. Add an explicit additive service
  source to the protocol, validate the skill against authoritative loadout
  state, preserve zero-cost native identification, retain source-zero vendor
  compatibility, and route Godot and native inventory selection without local
  gameplay mutation.
- Advance the Diablo content schema to 0.25 and make the Monk's declared Search
  ability executable by the authoritative module. Externalize its native
  character- and spell-level duration terms, preserve additive recasts and the
  signed-overflow guard, server-owned expiry and transition cleanup, and carry
  typed behavior 2 through compatible saves, protocol snapshots, Godot item
  outlines, native automap highlighting, and a four-checkpoint C++/C# replay.
- Advance the Diablo content schema to 0.24 and add server-owned Heal Other.
  Externalize its distinct class multiplier, preserve native other-player
  admission, mana, randomized Q6 healing, target cap, full-life consumption,
  and Monk 300-percent rule, and reuse existing protocol participant state.
  Add Godot/native target mapping and a timed three-checkpoint shared C++/C#
  multiplayer replay.
- Advance the Diablo content schema to 0.23 and add server-owned Fireball.
  Preserve native two-roll and per-level damage scaling, capped speed growth,
  swept direct collision, ordered geometry-gated 3x3 splash, Q6 resistance and
  object rules, and the 14-tick impact. Carry complete runtime state through
  protocol, compatible saves, conditional hashes, native and Godot
  presentation, and a four-checkpoint shared C++/C# replay.
- Advance the Diablo content schema to 0.22 and add server-owned Phasing.
  Preserve native offset enumeration, participant and world occupancy, one-draw
  destination selection, exact learned/consumable source handling, unchanged
  facing, and the consuming no-candidate outcome. Carry its invisible two-tick
  controller through protocol, compatible saves, conditional hashes, native
  and Godot projection, and a four-checkpoint shared C++/C# replay.
- Advance the Diablo content schema to 0.21 and add server-owned Infravision.
  Preserve native class-adjusted learned mana, exact scroll/rune consumption,
  saturating per-level duration growth, nonshortening recasts, authoritative
  expiry, and transition cleanup. Carry typed status behavior through protocol
  field 4, saves, conditional hashes, native and Godot presentation, and a
  four-checkpoint shared C++/C# replay.
- Advance the Diablo content schema to 0.20 and add server-owned Stone Curse.
  Preserve native crawl targeting, future walking-cell selection, content-owned
  boss immunity, no-target fizzle, consuming non-refreshing recast, level-scaled
  duration, movement release, and dead-target shatter. Carry controller and
  immunity state through protocol field 22, saves, conditional hashes, native
  and Godot presentation, and a six-checkpoint shared C++/C# replay.
- Advance the Diablo content schema to 0.19 and add server-owned Mana Shield.
  Preserve the native active-cast fizzle, learned-level reduction cap, exact Q6
  mana absorption, reverse calculation when mana is insufficient, exact-
  depletion cleanup, NoMana-equipment bypass, and transition cleanup. Route all
  authoritative player-damage paths through one module resolver; carry active
  state through protocol field 29, saves, conditional hashes, both clients, and
  a six-checkpoint shared C++/C# replay.
- Add a coordinated generic ruleset runtime that composes candidate content,
  atomically activates the complete module set, computes identity, and validates
  a game-specific target before one publication. Preserve the prior activation
  with stage-specific diagnostics on composition, compatibility, registration,
  identity, or target failure; retain the compatibility `Load` API; and move
  standalone server startup onto the all-or-nothing path.
- Add inclusive semantic version requirements to gameplay-module dependencies.
  Reject non-semantic and out-of-range loaded dependency versions during Engine
  preflight, preserve requirements through verified external-package wrapping,
  include declared ranges in deterministic ruleset identity while retaining
  unconstrained legacy hashes, and cover compatibility diagnostics and package
  propagation in Engine and Server tests.
- Advance the Diablo content schema to 0.18 and make PvP ears server-owned.
  Persist native-valid character names from new-session handshake through
  saves, reconnect, protocol field 28, conditional hashes, and both clients;
  externalize the stable ear item and per-class cursor; carry victim-authored
  runtime item names/cursor in additive generic item fields; preserve PvP
  equipment and special-level suppression; consume no extra RNG; expose names
  through the Godot launcher; and verify the complete death/drop timeline in a
  shared C++/C# replay.
- Add server-owned native player facing and ordinary monster-or-trap equipped
  item death drops. Accepted movement, attacks, and resolved casts update the
  eight-way direction; death rotates left through Head-to-Chest slot order,
  tries each preferred adjacent tile before native crawl fallback, preserves
  item seed and complete state, and consumes no RNG. Retain equipment for PvP
  and special-level multiplayer suppression, carry facing through protocol
  field 27, saves, hashes, native and Godot projection, and verify the result in
  a two-checkpoint C++/C# replay.
- Advance the Diablo content schema to 0.17 and preserve native multiplayer
  death-drop suppression for level 16 and arena-equivalent maps. Make game mode
  explicit host configuration instead of participant-count inference, expose it
  additively in `ServerHello`, synchronize both presentation clients, validate
  level suppression in external content, preserve unconditional cursor-held
  item drops, and verify unchanged Gold and RNG state in shared C++/C# replay.
- Preserve native cursor-held Gold overflow as server-owned state. World-item
  pickup now fills canonical inventory piles in native order, places any
  remainder on the authoritative cursor without consuming an item seed, and
  supports an explicit nearby drop command that preserves the original world
  item identity. Carry held items through snapshots, saves, conditional hashes,
  native and Godot presentation, combat death, and a shared two-checkpoint
  C++/C# replay.
- Advance the Diablo content schema to 0.16 and make ordinary combat-death gold
  consequences server-owned. Externalize the divisor, projected maximum stack,
  native Gold identity, and drop radius; cancel movement before native crawl
  placement; consume the authoritative RNG for item seeds; validate and persist
  canonical Gold world items; add native non-full-first inventory removal,
  whole-stack seed reuse, partial-stack RNG, grid compaction, odd physical-pile
  retention, and topology-aware pickup; retain an aggregate compatibility path;
  and verify wallet, inventory, world-item, and RNG state in shared C++/C# replay
  coverage.
- Materialize content-backed starting Gold as stable session-seeded native
  inventory piles beginning at cell 30. Preserve those piles through native
  nonmaximum-first store spending, purchase placement, sale top-off/new-pile
  creation, post-death wallet divergence, and transactional capacity rejection;
  keep Gold inventory-only across direct transfers and swaps; and add a
  five-checkpoint shared C++/C# purchase/sale/refill replay.
- Advance the Diablo content schema to 0.15 and make restart-in-town a
  server-owned terminal-death command. Externalize the destination level and
  tile, native Q6 life reset, and one-tick transition duration; preserve zero
  mana, inventory, and the cleared store state; add protocol action kind 6 and
  command field 27; route Godot Escape and both native dead-player controls
  through the server; and verify dungeon death through town recovery in a
  shared six-checkpoint C++/C# replay.
- Advance the Diablo content schema to 0.14 and add server-owned Resurrection.
  Preserve native other-player targeting, fixed minimum mana, living-target beam
  behavior, dead-player resource reset, ten-life revival, initial placement and
  radius-50 crawl order, fallback portal avoidance, and the timed beam
  lifecycle. Carry complete participant and beam state through snapshots,
  saves, protocol projection, both clients, and a five-checkpoint shared C++/C#
  replay.
- Project every assigned participant in atomic server snapshots, keeping the
  receiving player first and sorting remote players by entity ID. Validate and
  render the full set in Godot, harden native identity projection, and extend
  canonical C++/C# replay hashes and multiplayer fixtures to include both
  players while remaining receiver-order independent.
- Advance the Diablo content schema to 0.13 and add exact server-owned
  Teleport. Preserve the native radius-0-through-5 `DoCrawl` order, participant,
  monster, object, and static-geometry occupancy, no-cost fizzle, source
  consumption on success, and delayed first-update movement through an
  invisible two-tick controller. Carry the controller through snapshots, the
  existing protocol/save fields, native and Godot presentation, and a shared
  four-checkpoint C++/C# replay.
- Advance the Diablo content schema to 0.12 and add exact server-owned Healing.
  Externalize the native character-class multiplier and all character/spell-level
  random rolls, preserve Q6 life caps plus full-life mana/RNG consumption, add an
  exact additive healing event field with legacy native-reader behavior, and
  verify uncapped, capped, and full-life casts in one shared C++/C# replay.
- Advance the Diablo content schema to 0.11 and add server-owned Flash parity.
  Two independently rolled 19-tick layers reproduce the native 3x3 offset split,
  repeated magic-hit and exact-Q6 resistance damage, blocker and breakable-object
  behavior, PvP admission, and the delayed invulnerability window. Preserve the
  runtime through snapshots, saves, protocol projection, hashes, and a shared
  four-checkpoint C++/C# replay with strict magic-resistance validation.
- Advance the Diablo content schema to 0.10 and add server-owned Lightning
  parity. A speed-32 invisible controller now consumes the native per-tick RNG
  draws, leaves persistent level-scaled damaging segments, stops without
  breaking missile-blocking objects, and preserves magic-hit, resistance, Q6,
  PvP, snapshot, save, hash, protocol, and replay behavior. Explicitly sequence
  the retained C++ RNG operands to preserve the observed Windows result across
  compilers, and add a four-checkpoint shared C++/C# resolution timeline.
- Advance the Diablo content schema to 0.9 and replace spell 1's provisional
  targeted projectile with server-owned Firebolt parity: external native mana
  and class/variant adjustments, Q6 consumption, seeded Magic/spell-level
  damage, speed-26 16.16 swept movement, magic hit chance, quarter resistance,
  impact expiry, persistence, and a three-checkpoint C++/C# replay.
- Add server-owned player death and portal-transition lifecycles. Lethal monster
  and Charged Bolt damage cancel the current action, enter Death for the
  content-defined animation, and retain the terminal mode after action cleanup;
  portal use applies its destination behind a one-tick NewLevel barrier. Add
  protocol action kinds 4-5, save/hash/native/Godot validation, and shared
  C++/C# death and transition timelines, including autonomous monster movement
  and attack replay data.
- Add an authoritative player-action lifecycle that reserves movement
  destinations, defers melee/ranged damage and spell resource/effect resolution
  to native class/equipment animation frames, enforces native chaining
  boundaries, and preserves base-game versus Hellfire bow-speed behavior.
  Preserve in-progress actions through module snapshots, protocol field 25,
  version-1 saves, conditional canonical hashes, native/Godot projection, and a
  shared C++/C# action-state hash golden. Add a shared command-timeline replay
  for deferred movement, melee damage, attack-to-cast chaining, spell resource
  consumption, effects, and cleanup, then enable the lifecycle in composed
  server sessions.
- Add shared Diablo and Hellfire fast-Bow command timelines whose initial
  equipment is hashed with the player state. Both runners derive ranged target
  admission, combat mode, and variant-specific frame skipping from that
  equipment, and the managed gate verifies the fixtures against the activated
  base manifest and Rogue animation row.
- Advance the Diablo content schema to 0.8 and extract the complete native
  six-class animation matrix into validated `class_animations.tsv`; remove
  duplicated block/recovery timing from `classes.tsv`, cross-check every class
  row, and wire the catalog into authoritative simulation and collision modes as
  the basis for command-driven movement, attack, cast, death, and transition
  lifecycles.
- Advance the Diablo content schema to 0.7; externalize class block/recovery
  frames and game variant, make native player combat modes authoritative across
  protocol, saves, hashes, and clients, enforce Stand/Attack-only blocking with
  immediate block/hit transitions, reproduce Hellfire's post-block RNG draw,
  and add a shared C++/C# Hellfire block replay.
- Advance the Diablo content schema to 0.6 and add server-owned Charged Bolt PvP parity for global/friendly admission, retry-safe hostile mode, native hit/block/damage RNG order, class/equipment blocking, lightning resistance, exact Q6 victim events, protocol/save/hash/native/Godot projection, and a shared two-participant C++/C# replay.
- Advance the Diablo content schema to 0.5 and add native-style Charged Bolt collision parity for separate missile geometry, swept intermediate tiles, stationary-occupant suppression, and external object pass-through, breakability, and destruction; preserve the new state through protocol field 28, saves, conditional hashes, native projection, and a shared C++/C# object-collision replay.
- Preserve native current/future monster occupancy and petrification across authoritative ticks, protocol fields 18-21, version-1 saves, conditional hashes, native signed occupancy, and a shared C++/C# Charged Bolt replay; validate adjacent movement, ignore normal future occupancy, guarantee petrified hits after the native RNG draw, and cancel movement on death.

## 2026-08-10

- Adopt Godevilution as the project name and replace the inherited root README with the current migration goal, repository map, verification commands, origin, and license notice.
- Consolidate current architecture, migration status, protocol, server, Godot, native-client, replay, Lua, source-layout, content, and build guidance; archive completed project-specific plans.
- Remove inherited DevilutionX policy, release, packaging, and platform manuals from the active documentation tree while preserving `LICENSE.md` unchanged.
- Correct protocol-v1 identity guidance and make the native launcher send the combined ruleset hash in the compatibility content field.
- Make the Godot harness build a missing authoritative server, and make live protocol tests build the current server once as an explicit dependency before launching it without rebuild races.
- Introduce the dependency-free .NET 8 `Devilution.Domain` kernel and its independent xUnit project, then move deterministic inventory-grid geometry and mutation rules out of `StoreSimulationExecutor`.
- Introduce the .NET 8 `Devilution.Data` runtime depending only on Domain; extract canonical hashing, TSV parsing, ordered content packs, manifest identity, and stable symbolic IDs into independently tested assemblies.
- Introduce the .NET 8 `Devilution.Diablo` module with no server/protocol dependency; move item, store, combat, monster, spell, and world catalogs, authoritative player state, store/inventory intents and transitions, and transport-independent rules into it with shipped-content coverage.
- Move authoritative world collections, navigation, occupancy, movement, portals, world-item pickup, object effects, quest transitions, and transport-neutral gameplay events into `Devilution.Diablo`; reduce the server to Protobuf/result/event adapters for those commands.
- Move attack and cast intents, typed damage, experience and drop resolution, projectiles, status expiry, monster targeting, movement, attacks, and directed tick events into `Devilution.Diablo`; leave only stable tick scheduling and session event routing in the server host.
- Introduce the .NET 8 `Devilution.Engine` composition layer with dependency-ordered modules, transactional typed capabilities, authoritative clock context, and ruleset identity; move the first-party Diablo module and aggregate simulation factory behind that boundary while retaining the legacy host store-rule adapter.
- Move deterministic session/state coordination and transient event queues into Engine; move player/world/store snapshot capture and restore validation into Diablo; isolate Protobuf mapping in Server while preserving snapshot hashes, version-1 saves, and replay behavior.
- Add a generic Engine simulation runtime for serialized participant access, exactly-once tick advancement, and directed events; provide Diablo player templates/lifecycle hooks; extract explicit version-1 save encoding from the protocol executor.
- Allow content and live-server tests to locate an explicitly configured Godevilution repository root and server artifact path when builds use isolated output directories.
- Add manifest-declared, dependency-ordered content packs with deterministic composite-key table overlays, exact schema validation, transactional activation, and live-server protocol coverage.
- Add semantic Engine API compatibility ranges and deterministic module preflight diagnostics for duplicate IDs, missing or cyclic dependencies, and incompatible candidates before registration.
- Add optional content schema identities, semantic dependency-version ranges, manifest-format validation, and compatibility hashing while preserving legacy native golden hashes.
- Add explicit Server-owned gameplay-module package loading with SHA-256 verification, named-type activation, single-assembly dependency enforcement, verified ruleset identities, and process-level startup coverage.
- Move Diablo schema validation, normalized/legacy table selection, catalog construction, default-level selection, store-rule activation, and simulation creation out of Server startup and behind an `IDiabloContentBootstrap` module capability.
- Add a generic authoritative game-host adapter/factory catalog combining command, tick, snapshot, event, and save services; move built-in Diablo capability and executor construction behind it so executable `Program` contains no Diablo symbols.
- Generate managed Protobuf types once in `Devilution.Protocol`; extract generic sessions, scheduling, module loading, snapshots, and persistence into `Devilution.Server.Host`; extract Diablo mapping, compatible saves, replay support, and the protocol executor into `Devilution.Server.Diablo`; reduce the executable to `Program.cs` and enforce the assembly graph in tests.
- Externalize all six native-compatible player classes, including Hellfire metadata, flags, attributes, combat values, and the content-selected default; carry class identity through authoritative state, protocol field 19, saves, overlays, native projection, and matching C++/C# state hashes.
- Add optional per-session class selection to the shared handshake; create independent class-derived players with native Q6 starting resources, preserve save/reconnect precedence, and send the selection from both Godot and native clients.
- Gate session persistence on completed participant initialization so fixed ticks cannot create fallback state while a requested class is being installed.
- Externalize native-compatible starting wallets, equipment, belt and inventory placements, class skills, learned spells, item indexes, and charge state; deterministically seed each new session's starting items and validate every class/loadout/item relationship at content activation.
- Carry class skills and learned spell levels through module state, additive protocol fields 20-21, version-1 saves, Godot/native projection, conditional canonical hashes, and matching C++/C# golden coverage.
- Restore cold saves through their saved-class template and register the participant only after successful validation, preserving non-default resources and loadouts without leaving partial session state after a rejected import.
- Make cast admission source-aware across the protocol, Diablo module, native adapter, and Godot client; verify owned abilities and learned spells, exact charged or consumable items, and consume mana, charges, or items only after complete cast validation.
- Advance the Diablo content schema to 0.3, cross-validate every directly castable starting source during activation, and externalize the Sorcerer staff's spell 30 as a four-projectile authoritative vertical slice with module and live-server coverage.
- Advance the Diablo content schema to 0.4 and replace spell 30's provisional targeted projectiles with server-owned Charged Bolt count scaling, native Diablo-LCG draw order, Magic damage, 16.16 wandering movement, hit/resistance handling, persisted RNG/projectile runtime, and a shared C++/C# replay checkpoint.
- Preserve exact native Q6 monster health and Q6-before-resistance Charged Bolt damage across module state, additive protocol fields, events, version-1 saves, native projection, and conditional hashes; add a shared C++/C# checkpoint-only resolution timeline through hit RNG and impact state.
- Activate the `authoritative-client` vcpkg manifest feature automatically when the server-backed native client is enabled, ensuring the authoritative build resolves Protobuf.

## 2026-07-31

- Complete the first Godot client vertical slice with a local server harness, live handshake/movement/cast smoke coverage, captured-session replay, retry/reconnect tests, SVG-backed world presentation, authoritative inventory/store/event panels, and RTT-aware movement prediction/correction.
- Add standalone server starting-life and starting-mana options so local authoritative gameplay can exercise validated spell commands.
- Add a CI authoritative replay-parity gate that runs the native C++ and C# fixture runners against the same state-hash checkpoints, including the previously uncopied transaction and movement fixtures.
- Add shipped-content smoke coverage for the base monster, spell, item, store, and generated-item catalogs, plus a shared base-store purchase/sale replay fixture with native and C# hash parity.
- Add Godot asset-backed level layouts and authoritative HUD controls for store services, inventory movement, mana refill, and event-log management.
- Make the Godot harness discover versioned WinGet Mono installations from PATH and document the explicit headless verification workaround.
- Complete the Godot protocol-boundary integration with selectable store stock, footprint-aware inventory moves, selected-item services, object/quest dialog interactions, command feedback, and tested C++-compatible headless server launches.
- Add adjacent Godot client hardening: F3 diagnostics, F4 high-contrast HUD text, optional event audio hooks, cast target previews, and configurable headless run duration.
- Close the shipped Phase 3/4 parity gate: normalized store item definitions now take precedence over legacy compatibility tables, live server stock verifies weapon/armor projections, shipped generation/pricing tables are covered, and native replay/item checks remain aligned with C#.
- Complete the remote store transition for the shipped vendor set: external Smith, Witch, Wirt, and Healer stock is projected into native text and visual store UIs, purchases and Smith/Witch sales use authoritative commands, and live C# coverage exercises every vendor open/purchase path.


- Promote server-backed gameplay to an explicit legacy/authoritative runtime
  mode, add local server discovery and launch tooling, persist reconnect resume
  tokens, and prevent authoritative sessions from falling back to local
  simulation after disconnect.
- Add detailed handshake identity-mismatch diagnostic dumps and an
  authoritative native/protocol CI smoke gate.

## 2026-07-30

- Restore saved authoritative monsters and world items with entity-identity and walkability validation.
- Add deterministic server-owned monster movement and attack ticks, including correctly sourced damage events and external attack/aggro tuning.
- Add a validated external item catalog and allow store stock to reference stable item definitions.
- Add external portal definitions, deterministic level/tag-filtered item affixes, and catalog-backed monster drops.
- Route opt-in native world-item pickup through authoritative projected entity IDs.
- Add authoritative object activation and quest progress snapshots, commands, persistence, and native routing.
- Add a shared C++/C# replay fixture for object activation and quest completion with matching state hashes.
- Add typed elemental/magic damage, area-targeted spells, unique-item catalog generation, and expanded monster combat tuning.
- Load the shipped legacy item, prefix, suffix, and unique TSV table shapes into stable authoritative catalogs, with deterministic aliases and expanded native power mappings.
- Harden authoritative startup and save import against blocked placements, cross-category entity collisions, duplicate item identities, invalid equipment/belt slots, and malformed multi-cell inventory footprints.
- Match native item armor rolls with the legacy Diablo LCG, advance autonomous monsters for every skipped authoritative tick, and reject damage spells through blocked line-of-sight cells.
- Add autonomous snapshot polling and suppress legacy gameplay processors while the native client is connected to the authoritative server.
- Reject overlapping live player, monster, world-item, and object placements during startup and save restoration.
- Route autonomous monster hit, critical, and armor resolution through the external authoritative combat rules.
- Match legacy unique-item power rolls with the native LCG for legacy tables and recalculate legacy affix pricing from value modifiers.
- Carry external object-to-quest links through authoritative snapshots and advance linked quest progress during object activation.
- Add a validated native world projection cache for authoritative monsters, world items, and objects.
- Verify deterministic multi-actor autonomous movement across skipped authoritative ticks.
- Share the native LCG stream across legacy base armor, monster-drop quality, and affix generation, including forced-good drops.
- Match native legacy modifier semantics for fixed-point life/mana, hit-recovery signs, resistance clamping, elemental flags/damage, and indestructible durability; restore object quest links during save import.
- Resolve server-backed monster targets by authoritative cell rather than sorted native cursor index, preventing attacks and spells from addressing the wrong entity.
- Add shared multi-level occupancy replay coverage and save restoration checks for entities that remain on their original level after portal transitions.
- Synchronize compatible authoritative world items, monsters, and objects into legacy presentation pools, including safe creation and removal of server-owned entries.
- Complete the Phase 4 Godot-start boundary with authoritative delayed projectiles, declarative object heal/damage/experience effects, healing events, native projectile projection, and a frozen protocol contract covering handshake, framing, retries, snapshots, events, and state hashes.
- Start the Godot 4 C# client with shared Protobuf transport, adaptive command retries, snapshot/event reconciliation, procedural entity presentation, input mapping, and connection/player HUD.
- Fix CMake 4.x deferred dependency resolution for configuration keywords and link the MSVC Protobuf import target instead of a DLL path.

## 2026-07-29

- Extend the authoritative gameplay boundary with catalog-driven monster snapshots, defeat drops, world-item pickup, damage spells, and native client projections for monsters and world items.
- Add stable content-ID catalogs and ruleset identity negotiation to the Protobuf handshake
- Load authoritative levels, blocked cells, spells, combat constants, and complete protocol item modifiers from external TSV content
- Add validated Protobuf-snapshot saves with atomic server-owned filesystem persistence and session restoration
- Add native movement, attack, cast, and portal command entry points plus shared C++/C# spell replay coverage


- Extend the authoritative gameplay boundary with catalog-driven monster snapshots, defeat drops, world-item pickup, damage spells, and native client projections for monsters and world items.

## 2026-07-28

- Add external `store_services.tsv` pricing data, including spell staff-cost overrides and legacy-compatible service formulas
- Add explicit authoritative inventory-to-belt/equipment transfer commands while retaining the legacy inventory-grid command
- Add the server-backed Adria mana-refill menu action and service-only store scope
- Make C# replay execution validate SHA-256 state checkpoints after every transaction command; extend native fixture coverage
- Execute the shared transaction replay natively, add shape-aware multi-cell inventory placement and swaps, and align C++/C# hashes for resources, belts, item footprints, and protocol item state
- Add the first authoritative gameplay boundary: bounded movement, life/mana maxima, character level, healing, adjacent combat damage, defeat experience, event-batch transport, native player projection, and generic vendor routing
- Add authoritative blocked-cell movement, level-aware portal transitions, status-effect snapshots/expiry, native event application, and replay-hash parity for level/status state
- Add a shared movement/combat replay fixture executed by both C# and native replay runners
- Configure the C# test project to build a self-contained Microsoft Testing Platform runner; direct execution avoids a missing machine-wide VSTest `testhost` package

## 2026-07-26

- Add authoritative belt snapshots and stable inventory, belt, and equipment
  references for server-backed service commands
- Add C# Adria mana-refill validation and deterministic refill pricing
- Match server-backed sale, repair, recharge, and identification rules more
  closely to legacy vendor pricing semantics
- Route the opt-in Smith visual store through acknowledged authoritative
  purchase, sale, repair, and refresh operations
- Add shared C++/C# transaction-parity replay coverage for purchase, sale, and
  mana refill

## 2026-07-22

- Add authoritative wallet, active-store, and inventory snapshots after
  handshake and command batches
- Add deterministic SHA-256 hashes for the authoritative snapshot projection
- Add baseline experience, life, and mana fields to the store player projection
- Add primary attributes, equipment slots, and inventory-grid snapshot fields
- Add core authoritative item-state fields to store inventory snapshots
- Add C# replay-fixture execution, authoritative clock abstraction, session
  entity-ID coverage, empty-batch validation, and command-delivery vector tests
- Reconcile the C# migration plan with implemented phase status, ownership,
  remaining exit gates, and the current critical path
- Add external content and versioned C# gameplay modules as first-class
  migration workstreams with per-slice ownership and verification gates
- Add external TSV store loading, module-owned store transactions, deterministic
  replay checkpoint parity, fixed-point/RNG/ID primitives, and gameplay ruleset
  identity coverage
- Add an executable JSON replay fixture baseline for player/store projection
- Rename experimental C++ client-facing authoritative types and files to server-backed/vendor terminology with deprecated compatibility aliases

- Add the initial transport-independent Protobuf protocol contract and compatibility rules
- Add adaptive client command acknowledgement retries with duplicate and late-command outcomes
- Add a shared retry/deduplication protocol test vector consumed by the C# server tests
- Add the initial C# authoritative command server with Protobuf bindings and deduplication tests
- Add bounded TCP Protobuf sessions with handshake validation and command routing
- Add the first C# authoritative store executor with retry-safe purchases and shared stock
- Add opt-in C++ Protobuf generation, bounded envelope framing, and a minimal
  server-backed client covering handshake, command acknowledgement, and snapshots
- Wire the C++ command-delivery tracker into queued authoritative sends,
  adaptive resubmissions, and acknowledgement resolution

- Fix native authoritative builds by generating Protobuf sources in the consuming
  CMake directory and exporting generated message data to C++ DLL tests
- Fix C++ authoritative connection includes and invalid-frame boundary coverage
- Add authoritative active-store stock snapshots and include vendor items in
  deterministic snapshot hashes and reconnect-resynchronization tests
- Add matching C++ and C# canonical content-manifest hashes with shared golden
  vectors and malformed-row validation
- Extend C++ and C# replay fixtures with structured content identity, retained
  checkpoints, store command payloads, and optional final snapshot validation
- Fix the native replay fixture test to avoid character-level validation before
  player experience tables are initialized
- Preserve native queued and in-flight authoritative commands, adaptive retry
  state, and the server resume token across reconnects
- Add opt-in server endpoint parsing, stable store-command construction,
  validated native vendor-stock projection, and reconnect-safe store UI state
- Add standalone C# server host startup with external store content, live ticks,
  native player/item projections, and server-backed inventory session commands
- Add a destination-explicit legacy store UI adapter that preserves stable
  authoritative vendor slots while applying validated snapshots
- Wire the opt-in server-backed runtime into game lifecycle and Smith-store
  opening, with content identity validation and local-mode preservation
- Apply authoritative player snapshots to native state, poll adaptive command
  retries, and route acknowledged Smith/inventory store transactions through
  the server-backed runtime without mutating locally before acknowledgement

## 2026-07-21

- Fix quest-pool initialization, item-generation reload state and legacy drop probabilities
- Restore test includes and test access to the refactored store and automap managers
- Extract active-mod and game-data reload ownership from Lua behind C++ managers and a typed event bus
- Add deterministic replay primitives for canonical state hashing and command ordering
- Add initial canonical player and store state projections for replay comparison

## 2026-07-20

- Remove all Packaging/ and Android-specific build artifacts
- Prune Android/UWP conditionals and references from CMake build system
- Consolidate Level identity fields into `LevelId`, remove 5 duplicate fields from Level
- Make `World::switchLevel` accept a `LevelId` instead of bare `LevelIndex`, centralizing identity setup
- Simplify all level-transition callers to construct `LevelId` in one call
- Externalize monster AI dispatch: replace `AiProc[]` array with `std::unordered_map` registry populated at startup
- Externalize AI-to-missile-type mapping: replace `GetMissileType()` switch with data lookup from `monster_missiles.tsv`
- Add `behavior` column to `objdat.tsv`, populate for all 109 object types
- Replace `OperateObject()` hardcoded `switch` with registry lookup via `ObjectBehaviorRegistry`
- Externalize item generation parameters: quality curve, affix allocation, drop tables into `item_generation.tsv`
- Create `ItemGenerationConfig` struct loaded from `item_generation.tsv` replacing hardcoded constants in `GetItemBLevel()`, `GetItemPowerPrefixAndSuffix()`, `RndItemForMonsterLevel()`, `RndAllItems()`, and `SpawnItem()`
- Externalize level generation parameters: monster density divisor, MP multiplier, and trap frequency into `level_generation.tsv`
- Create `LevelGenerationData` struct with level-range lookups for monster density, MP multiplier, and trap percent
- Use new data-driven lookups in `AddObjTraps()` and `InitMonsters()`
- Externalize quest pools and set level names into `quest_pools.tsv` and `set_level_names.tsv`
- Add `Object::data()` accessor returning `AllObjects[_otype]`
- Replace `QuestLevelNames[]` array with data-driven `SetLevelNames` lookup
- Add `onExpiryMissile` column to `misdat.tsv` replacing the switch in `ProcessGenericProjectile`
- Add `shortName` column to `towners.tsv`, replacing hardcoded `TownerShortNames` map
- Add TSV entries for TOWN_FARMER, TOWN_GIRL, TOWN_COWFARM (Hellfire NPCs)

## 2026-07-02

- Refactor `ui/automap` into `AutomapManager` class with singleton `CurrentAutomapManager`; move 8 globals + 13 functions; update ~18 caller files

## 2026-07-01

- Refactor engine/lighting into `LightManager` class with singleton `CurrentLightManager`; update ~22 caller files
- Move 12 theme/trigger/crypt scalar globals (`numthemes`, `armorFlag`, `weaponFlag`, `zharlib`, `trigflag`, `numtrigs`, `TWarpFrom`, `UberRow`, `UberCol`, `IsUberRoomOpened`, `IsUberLeverActivated`, `UberDiabloMonsterIndex`) into `Level` class with inline accessors
- Replace `Missile::var1`-`var7` with typed anonymous union providing named struct accessors per missile family (projectile, guardian, portal, rune, wall, chain, inferno, nova, firewall, lightning, golem); migrate ProcessGenericProjectile and ProcessGuardian/AddGuardian
- Refactor `game/stores/` into `StoreManager` class with singleton `CurrentStoreManager`; update ~30 caller files

## 2026-06-24

- Add file-level summaries to all source files missing them
- Remove Testing/ from tracking, add to .gitignore
- Move mpq/ to data/mpq/ and storm/ to network/storm/
- Restructure network/ directory (protocol/ and transport/ subfolders)
- Move validation/ files back to respective subject folders
- Move menus/, panel/, debug_overlay/ under ui/
- Remove Discord integration

## 2026-06-23

- Rename dvlnet/ to net_transport/
- Subdivide utils/ into string/, sdl/, endian/, image/, container/, file/
- Subdivide engine/ into audio/, gfx/, load/, math/ subdirectories
- Rename DiabloUI/ to menus/
- Collect validation files into Source/validation/
- Merge control/ and panels/ into panel/ directory
- Remove compatibility forwarders and update all includes to canonical paths
- Migrate remaining Source/ files to domain subdirectories
- Migrate game domains to Source/game/
- Update docs
- Fix portal migration verification
- Document portal source migration
- Move portals into game domain
- Add canonical portal domain interfaces
- Plan portal domain source migration
- Document source organization architecture

## 2026-06-22

- Replace gendung macro shims with accessors
- Plan gendung macro shim removal
- Require comments on level accessors
- Document gendung macro shim removal

## 2026-06-21

- Add positioned tile grid iteration
- Document tile grid iteration API
- Plan tile position range implementation
- Document tile position range design
- Modernize dungeon level tile storage
- Document unified dungeon megatile storage
- Document legacy dTransVal removal

## 2026-06-20

- Remove legacy dPiece storage
- Document legacy dPiece removal
- Move AI docs under docs/AI
- Move player helpers into Player
- line endings
- Add graphical debug piece selector
- Fix debug overlay windows and editor selection
- Expand debug overlay: add Inspector panel & toolbar
- Add built-in help system to debug Lua console

## 2026-06-19

- Add tile fallback and sync with legacy map data
- Refactor: add levelMicros() span for tile micro access
- Migrate dungeon flags and special data to Tile API
- Remove legacy dungeon entity arrays, use Tile API only
- Refactor tile access to use tileAt(Point) API
- Add object subsystem unit tests and test visibility
- Complete Tile state migration
- Update CMake, codecov, and level init in tests/benchmarks
- Add ImGui debug overlay console and refactor console history

## 2026-06-18

- Phase 4B COMPLETE: Update documentation
- Phase 4B-12,13,14: Final entity macro migrations
- Phase 4B-9,10,11: Migrate debug, towners, sync, inv to Tile API
- Phase 4B-8: Migrate cursor.cpp to Tile API entity accessors
- Phase 4B-7: Migrate missiles.cpp to Tile API entity accessors
- Phase 4B-6: Migrate loadsave.cpp to Tile API entity accessors
- Phase 4B-3,4,5: Migrate dead.cpp, items.cpp, objects.cpp to Tile API
- Phase 4B-1 & 4B-2: Migrate player.cpp and monster.cpp to Tile API entity accessors

## 2026-06-17

- Phase 4A lighting migration: Tile API conversion, tests, benchmarks
- Better agent instructions
- Migrate rendering hot-paths to Tile API, update docs

## 2026-06-16

- Add Tile class and Level integration (Phase 2)
- Migrate dItem to be level-specific, not global
- Refactor dItem to be level-specific, not global
- Minor items.h cleanup
- Refactor active entity bookkeeping to pool headers
- Refactor to use MonsterPoolAdapter and ItemPoolAdapter
- Introduce monster pool for modern entity management

## 2026-06-15

- Clean up player attribute migration
- Clarify packed player validation fields
- Group player damage bonuses
- Group player life and mana resources
- Normalize player header line endings
- Modernize object storage with DenseEntityPool
- Refactor object pool access via ObjectPoolAdapter

## 2026-06-14

- Modernize object pool with type-safe container
- Introduce ObjectManager facade for object operations
- Minor comment clean-up to objects.h
- Initial objects.h cleanup
- Add shared attribute value types
- Plan player attribute model migration
- Document player attribute model design

## 2026-06-13

- Refactor player logic into Player class methods
- Introduce World and Level state containers
- Introduce Bestiary and migrate Monster behavior
- Add CombatActor base type
- Refactor players and monsters onto Actor
- Introduce Bestiary class and related engine updates
- Introduce Actor base struct for Player, Monster
- Hellfire content now externalizes the native-derived dungeon level identities
  used by the fixture set through validated composed `levels.tsv` metadata;
  blocked-cell generation and remaining Hellfire catalogs remain explicit follow-ups.
- Hellfire content now also externalizes the Empyrean Band and its native
  attribute, light, recovery, and trap-resistance powers through keyed item and
  unique-item overlays.
- Add a three-cycle server reconnect soak that preserves a resumed participant's
  identity and complete self-first multiplayer snapshot while another session remains connected.

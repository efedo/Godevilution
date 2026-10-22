# Godevilution Changelog

Notable implementation milestones are summarized here. Current work and open
acceptance gates are tracked in [working status](migration-status.md); verification
is summarized in [test history](test-history.md). Protocol and architecture
contracts stay in their existing guides.

## 2026-10-21

- Fix the native Cathedral barrel useful-item test fixture: its canonical
  healing/mana potion rows now carry no spell, matching the native IDI rows
  required by useful-item selection since 2026-10-16. Production behavior is unchanged.
- Restore the authoritative-parity CI gate: the content-contract validator accepts
  live overlays that derive their schema from the base manifest (still rejecting
  stale literals), keeps the schema wording check in the content README only, and
  CI installs both the .NET 8 and .NET 10 runtimes the managed projects target.
- Fix two store consumable tests that used a dead default player (refused since
  `17b114a54`) and two launcher contract expectations stale after script refactors;
  move the Trap Disarm launcher test to the shelf with its launcher.
- Align the C++ replay model's seeded HorkDemon action roll with the managed
  native stream (one GenerateRnd(100) draw), restoring the autonomous multiplayer
  fixture that diverged when `60287c872` updated only the managed side.
- Make CI's Linux build and tests portable: pass the proto3-optional flag to protoc
  older than 3.15, run the standalone-host package test against the matching
  build configuration, and widen clock-gated host hang guards for loaded runners.
- Adopt the playable-first recovery plan in the work plan, status and inventory.
  Shelve all unfinished drafts on `wip/phase0-shelf` (one commit per group), move
  root clutter to `.local/` and ignore the runtime files that created it.

## 2026-10-20

- Add distinct C2 monster Arrow owners with native fixed-point flight, collision-
  time Q6 hit/block/RNG, lifetime and current/retained source facts. Preserve
  pending flight across detached floor admission and pause it off-floor.
- Add repeated native Arrow traces, scoped movement/player comparisons and owned
  processor/monster-impact checks. Existing C1/profile contracts remain unchanged;
  complete missile families, live chest/wall effects, travel and additive C2 cold
  restore remain open.

- Add C2 ranged release clocks for Skeleton/Goat and Magma/Acid/Deathspit, bound to
  exact floor, actor and missile identities. Preserve pending modes across detached
  floor admission; advance actual reserved-archer emergence before ranged admission.
- Compare nine repeated native controller traces and verify processor pause/resume,
  native Acid sound/damage RNG and stale-owner refusal. Native Arrow flight/impact, live
  chest/wall effects, family profiles, travel and additive cold restore remain open.

- Add class-bound C2 initial owners, source-bound door registries and detached
  multi-floor admission. Admit all 32 qualified Warrior/Sorcerer inputs and pass
  63 focused owner, command, item, phase and Cathedral continuation checks.
- Retain armed C2 door links and bridge four native elixir identities explicitly.
  Preserve old Cathedral semantics and refuse live C2 chest/armed-door effects
  until their owners exist; family profiles, travel and cold restore remain open.

- Add explicit internal item-family ownership for Catacombs pools and operation
  generation while preserving public Cathedral guards and existing save formats.
- Add C2 ordinary/TCHEST loot and intrinsic trap plans, disarmed-chest behavior,
  linked-wall flag preservation and complete native object classification. Compare
  30 prepared native operations; live floor/effect ownership and cold restore stay open.

- Extract immutable native population bindings behind the existing Cathedral API.
  Preserve C1 generation fingerprints, object lifecycles and retained-floor restore
  while preparing separate Catacombs owners; 25 focused C1 integration cases pass.

- Add distinct Catacombs actor admission, frozen Gargoyle wake/animation phases,
  active-order pack wake, bounded ranged-skeleton emergence admission and native
  Fat Special cursors. Preserve effective unique
  AI and refuse the new fields in existing cohort formats.
- Add source-bound L2 door operations, occupied-close and sync/frame behavior;
  preserve independent collision blockers. Keep floor owners, trapped-chest
  operations, ranged timing, family profiles, travel and cold restore open.

- Compose complete native Catacombs fresh encounters, items, themes and level-ready
  light/vision/automap state through shared internal phases and distinct C2 APIs.
- Preserve Cathedral contracts and derive class inputs/history independently;
  compare all 32 native cases, 44 controlled theme branches and saturated pools.
  Keep playable actor behavior, profile/travel integration and cold restore open.

- Add distinct Catacombs population/automap metadata and offline compilation,
  verified semantic-source hashes, L8 story bindings and unchanged Cathedral schemas.
- Extend shared initial phases through explicit internal family contexts: generated
  theme ordering/reservations, L2 doors/torches, L8 books, barrels and trapped chests.
  Compare all 32 native initial stages; retain separate version-1 reference bytes.

- Add pure seeded Catacombs L5–L8 generation and semantic TIL/SOL materialization,
  preserving retained retry seeds, theme ordering, entries and generated L8 stairs.
- Add repeated native geometry and complete prepared-class population references,
  with explicit hero/history inputs and test-only same-process restoration guards.
  Keep managed population, playable family activation, travel and cold restore open.

- Add the optional original Cathedral L1 object family: eight CEL sheets and ten
  native IDs, complete atlas preflight and exact current authoritative frames.
  Preserve generic controls, native centering and visible broken-barrel residue.
- Qualify all 48 source frames, published atlas pixels and production native draw
  anchors. Add ordinary Warrior chest before/after draw evidence and exact cold
  object/collision retention; keep other live lifecycle art and rendered parity open.
- Project all 11 original Town residents from the active authoritative catalog,
  with stable cow instances, strict wire validation and unchanged save/hash contracts.
- Import optional original human/cow CEL art into a content/Town-bound catalog;
  preserve every native pixel, direction, anchor and prepared frame-order/delay edge.
  Draw residents with whole-family atlas preflight and bounded cosmetic Town time.
- Verify complete resident anchor roaming through actual class controls, retain
  explicit fallback and keep native initialization RNG/cold animation phase open.
  Use ordinary destination waypoints and bounded post-draw clock/snapshot readiness
  before unchanged cold-state/receipt checks; export native-class timing diagnostics.

## 2026-10-19

- Add distinct format-5 Warrior/Sorcerer Town profiles and a strict v11 owner for
  semantic fine/coarse/special/protected geometry, native provenance and typed
  mutation replay. Preserve older profile/save bytes and shared simulation RNG.
- Bind canonical human/cow collision without changing Smith scenery, and qualify
  Town/Cathedral drawing, Smith/Pepin controls and exact cold retention through
  both headless class journeys. Keep default adoption and full rendered parity open.
- Draw optional identity-bound native scenes with one isometric camera/pointer
  transform, native diagonal traversal and special placement, bounded textures
  and whole-family atlas preflight. Keep explicit fallback and parity limits.
- Record actual Cathedral raster/entity draws in class journeys and explicit
  Town fallback on cold restore. Walk toward ranged targets until they are visible
  before ordinary clicks; retain combat, resource and exact-resume gates.
- Add a bounded optional client-only Town/Cathedral CEL/MIN/TIL/SOL importer,
  explicit palettes, content/level bindings and hashed PNG atlases. Preserve native
  MIN remapping, first-use frame types, floor/foliage masks and special anchors;
  compare every native piece/special and published pixel before client adoption.
- Add an MPQ-free Town geometry builder with separate fine/coarse channels,
  native warp policy, conditional Hive/Grave/fountain changes and exact generation
  RNG. Keep general DUN validation and existing runtime profiles unchanged.
- Add an isolated headless native Town/scene oracle and a 191-case managed
  composition comparison, including retained state and all native entry kinds.
- Add an optional client-only original-player CL2/PAL/TRN importer and validated
  native catalog. Preserve native run spill, opaque palette zero, original anchors
  and source-frame/playback-cycle differences in ordinary PNG atlases.
- Bind native class/equipment/facing/action art to admitted content and explicit
  Town/Cathedral levels, with bounded texture residency and observable fallback.
  Retain generic packs and add native pixel/atlas comparisons and class draw checks.
- Add explicit class-bound native Warrior/Sorcerer profiles, canonical preparation
  validation, starter-item bindings and class-safe admission/cohort restoration.
  Preserve legacy Rogue compiler bytes and profile/save identities.
- Add native birth-tuple comparisons and ordinary-control class journeys with
  Repair/Recharge, spell sources and exact cold checkpoints. Keep route travel
  active while a server-owned native walk path remains pending between actions.
- Consolidate Godot session return/rejoin, joined cancellation/scene cleanup,
  confirmed resume credentials and current-model HUD callbacks. Preserve pending
  command and bookmark refusal before returning to setup.
- Scope local native setup to admitted Rogue identity using client-only endpoint
  class hints; preserve remote/default choices, empty launcher-cleared hints and
  resume identity. Add focused regressions and headless menu/cold-resume checks.

## 2026-10-18

- Refresh the clean-session handoff for the renamed trees and completed local
  artifact cleanup (about 17.2 GiB); retain current inputs, build caches and
  unique sources while preserving gameplay WIP.
- Rename `godot/` to `godot-client/` and `Source/` to `legacy-native/`; update
  build, launcher, CI, test, translation and documentation paths. Keep runtime
  identities, session directories and existing gameplay work unchanged.
- Reconcile contributor, protocol, client, content and build documentation with
  current profile scope, versioned state and the synchronized plan/status/inventory.
  Remove superseded run dossiers from guides; keep concise outcomes in test history.
- Add a brief near-term work plan and require synchronized updates with working
  status and the numbered feature inventory at meaningful work changes.
- Expand the feature/gap inventory into stable numbered subitems with individual
  status, current scope and remaining work, including full graphical and release closure.
- Separate current working memory, numbered feature gaps, implementation history
  and scoped test history; remove obsolete archives and use Git for past versions.
- Add opt-in held entity attacks for the native-depth client. Repeats follow
  authoritative receipts and action readiness; release, focus loss and identity
  changes stop new inputs without rewriting queued commands or retries.
- Add native-depth buffered ranged follow-ups during movement and an active bow
  attack. Preserve the in-progress action, latest valid target and cold-resume
  state; legacy commands and saves retain their prior behavior.
- Add four-floor native Cathedral owners, content activation and cohort v10,
  including native destination walking and fresh-host retention. Focused native
  and managed coverage is in place; full L3/L4 combat acceptance remains open.
- Reduce clock-gated response work while preserving durable receipts, FIFO order,
  publication fences and exact retry behavior. Add negotiated physical clock
  samples, command-boundary snapshots and in-flight duplicate snapshot reuse.
- Cache immutable presentation geometry and encode checkpoints once, preserving
  canonical snapshot/save bytes and invalidating on door or barrel mutations.
- Match native TCP latency settings on both endpoints without changing framing,
  cancellation or reconnect semantics.

## 2026-10-14 to 2026-10-17

- Add native Cathedral chests, sarcophagi, barrels and linked arrow traps to the
  opt-in progression profile. Preserve native object mutation, loot, reserved
  actor release and trap timing through cohort cold restore.
- Complete the scoped native Rogue L1 Town/combat/earned-equipment route and the
  first L2 combat/XP continuation. Keep this profile separate from legacy launch
  defaults and older cohort formats.
- Add explicit Pepin healing purchases, native pinned shop stock, and atomic
  refusal of consumable use by dead players.
- Add clock-only timing probes and FIFO authority turns for periodic saves,
  response capture and command admission, retaining legacy transport behavior.

## 2026-10-12 to 2026-10-13

- Add native-derived movement, attack/cast timing and action-phase contracts
  while preserving default profile behavior. Full native tick/frame integration
  remains a separate parity requirement.
- Add authored Cathedral combat and loot content, actual-control equipment and
  Town service flows, and fresh-host retention checks for the opt-in profile.
- Add command-phase observations for acceptance and diagnostics without changing
  serialized gameplay state or using observations as simulation authority.

## 2026-10-07 to 2026-10-08

- Add validated native quest-pool selection and level-entry policy with explicit
  profile identity, preserving existing constructors, packs and save defaults.
- Harden ruleset-bound restore and host recovery; keep compatibility loaders and
  legacy sessions available while rejecting mismatched opt-in checkpoints.
- Add isolated headless performance and gameplay workloads with process ownership,
  bounded evidence and clean shutdown checks. These workloads do not certify
  broader release budgets or full campaign acceptance.

## 2026-10-04 to 2026-10-06

- Add ordered authoritative command ticks and actor-scoped execution while
  retaining legacy APIs and strict durable-before-acknowledgement behavior.
- Add native Cathedral object, trap-disarm, loot and combat characterization with
  explicit opt-in activation. Keep incomplete gameplay routes out of accepted
  completion claims.
- Improve Godot scene lifecycle, reconnect, menu and process cleanup, with
  isolated headless checks and explicit limits on rendered acceptance.

## 2026-09-29 to 2026-09-30

- Add the Godot 4 client vertical slice for authoritative movement, combat, loot,
  inventory, Town services, save/resume and server recovery.
- Add optional multiplayer sessions, reverse-order fresh-host resume and shared
  world state while preserving single-player and preview defaults.
- Introduce negotiated clock-gated command scheduling, correlated timing probes,
  immutable retries and server sequence floors. Keep admission rules and existing
  wire/save compatibility intact.
- Add explicit Cathedral level-one generation and content-owned entry metadata;
  authored replacements are validated and isolated from base content.
- Add bounded performance recording and improve checkpoint publication while
  retaining durability and failure fences. Measurements remain workload-specific.

## 2026-08-10 to 2026-08-13

- Establish the .NET 8 Domain, Data, Diablo, Engine and Server.Host boundaries.
  Move gameplay rules and state out of the executable host; keep Protobuf mapping
  and transport adapters at the server boundary.
- Add validated content packs, stable catalogs, canonical identity and semantic
  compatibility checks, with legacy manifests and native golden identities kept
  readable.
- Externalize native player classes, starting loadouts, spells, items, monsters,
  quests, level generation and object behavior into validated content catalogs.
- Expand server-owned combat, projectiles, status effects, player resources and
  monster AI with native-derived RNG, damage, timing and blocking behavior.
- Preserve `LICENSE.md` unchanged while replacing inherited project guidance with
  Godevilution-specific architecture, build and migration documentation.

## 2026-07-28 to 2026-07-31

- Build the first authoritative gameplay boundary for movement, resources, combat,
  experience, events, portals and vendor operations.
- Add server-owned world entities, monster movement and attacks, drops, object
  activation and quest progress, with native client projection and save/restore.
- Complete the initial Godot client vertical slice with Protobuf transport,
  acknowledged controls, snapshots, events, procedural presentation and HUD flows.
- Add shared C++/C# replay fixtures for movement, combat, stores and objects to
  keep deterministic state and content identity aligned.

## 2026-07-20 to 2026-07-26

- Establish authoritative sessions, bounded Protobuf transport, command retry and
  deduplication, durable saves, canonical state hashes and reconnect handling.
- Add external content catalogs and module-owned store transactions, inventory,
  equipment, belt and wallet state; preserve native item identity and pricing.
- Begin moving native gameplay data and behavior behind validated tables and
  registries, including monsters, portals, item generation, quests and objects.

## 2026-06-13 to 2026-07-02

- Introduce `World`, `Level`, `Player`, `Monster`, `Actor` and `Bestiary` state
  boundaries, moving global gameplay ownership into explicit domain objects.
- Replace legacy dungeon entity arrays and tile storage with level-owned pools and
  a unified Tile API; migrate lighting, automap, stores and related managers.
- Reorganize source domains and utilities, remove compatibility shims, and move
  active-mod and game-data reload ownership behind managers and typed events.

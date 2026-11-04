# Godevilution Changelog

Notable implementation milestones are summarized here. Current work and open
acceptance gates are tracked in [working status](migration-status.md); verification
is summarized in [test history](test-history.md). Protocol and architecture
contracts stay in their existing guides.

## 2026-11-04

- Debug command logs (P3, P4): Debug hosts and clients log commands, receipts and checkpoints on a
  background thread. `--replay-command-log` reproduces a later checkpoint byte for byte and
  `--compare-client-command-log` checks a client log against the host's.

## 2026-11-03

- Clock-gated command lead: the client's tick projection is pulled back by each snapshot to what
  the snapshot's age allows and stops 1 s past the last server evidence, and the round-trip lead
  stops at 500 ms, so a slowed server cannot leave a command waiting seconds ahead.
- Level-entry ticks: the server compiles its code fully optimized at first use, enters every
  floor once on a throwaway run at startup on a background thread, and the live warp no longer
  re-checks state that cannot have changed between prepare and commit. On a Release server at
  200 Hz, L2 entry went from 343 to about 120 ms and L5 from 570 to 186-242 ms.
- Acceptance bot no longer starves the client's main thread: a failed target search stops
  and yields instead of repeating on one snapshot (485 ms a pass to 25 ms), door retries wait
  for a frame, and above 20 Hz tick budgets cannot end a stage within 3 s of wall time.
- 200 Hz journeys: the server clock is paced by the tick rate. One read grants at most 50 ms
  of game time, and time spent executing ticks counts against the next read, so a stalled host
  slows the game instead of catching up in a burst. Live hosts accept a late gameplay command
  on the current tick for up to 2 s; store and stash commands keep their rescheduling window.
  The client corrects its clock backward from probe replies, polls snapshots proportionally
  faster above 20 Hz, and takes its command lead from the median probe rather than the largest,
  so one slow probe cannot schedule a command seconds ahead. The bot accepts a successful
  duplicate receipt from a retransmitted command.

## 2026-11-02

- Background checkpoint writes (batch P2): timed cohort checkpoints capture under the
  authority lock and write on their own thread, one at a time. A failed timed write
  is logged and retried next interval instead of fencing the host. Admission, the
  last disconnect, reload and shutdown wait for it and still publish synchronously.
  On the L8 cohort this moves 72 ms off the lock; 174 ms of capture stays (P5).

## 2026-11-01

- 60 Hz fight hitch (batch P1b): native vision rays looked up each cell in a sorted
  list of every wall and blocker on the level, for every monster on every tick. A
  hash set now answers that lookup, and monster visibility finds its level without
  copying the level list. L3 monster ticks over 15 ms went from 762 to 5 in a
  60 Hz journey, which now passes with resume on the Debug server.
- Timed cohort checkpoints (persistence batch P1): clock-gated cohort hosts advance
  every tick in memory and acknowledge commands and serve polls without writing. The
  cohort checkpoints every `--checkpoint-interval-seconds` (default 30), on clean
  shutdown and when the last player leaves; a crash loses play since the last one.
  Cohort format 2 keeps each participant's retired client sequence and its last 256
  receipts; format 1 saves are refused with the folder to delete. `ServerHello`
  carries `host_instance_id`; after a host restart the client reschedules pending
  commands the host never received from the restored tick.
- Tick-rate synchronization test: `ClockGatedPlayStaysSynchronizedAtTheHostTickRate`
  plays three clock-gated steps against a live host at 9, 20 and 41 Hz and checks the
  advertised rate, the host clock and that each step lands on its scheduled tick.
- Clock-gated command lead: the client schedules each command ahead by the largest
  timing-probe round trip of the last 10 s (at least 100 ms) instead of the largest
  ever seen, so one server stall (1.3 s while L5 generated) no longer delays every
  later input. Retransmission keeps the largest round trip, so slow acks are still
  waited for. The Warrior's L5 melee went from 36 to 10 ticks per swing at 20 Hz;
  a TooLate swing is retried.
- Depth journey speed-up: `test-godot-native-depth-progression.ps1 -TickRateHz` runs
  the server faster than real time (automated non-performance phases only).
  Acceptance waits count game ticks when the rate is not 20 Hz, with a wall-clock
  backstop, and the timing file records melee reactions. Draw and melee-receipt
  failures now report their state.

## 2026-10-31

- Catacombs ranged and missile monsters (batch 2.4b): on L5–L8 Skeleton and Goat
  archers, Acid Beast and Poison Spitter, Magma Demon and Blood Stone, and the
  Familiar run the native SkeletonBowAi, AiRanged, AiRangedAvoidance and BatAi with
  their Stand, Delay and ranged-attack modes. Their AI state (var1/var2, goal) lives on
  the actor and the ranged attack on its native animation cursor; Arrow, Acid,
  AcidSplat, AcidPuddle, MagmaBall, MagmaBallExplosion and Lightning are native
  missiles on their projectiles, with native CheckMissileCol, PlayerMHit and
  MonsterTrapHit. The new state is saved only in the Catacombs cohort format; saved C2
  ranged actions and Arrow flights still finish under their old owners. A seven-lane
  native ranged AI trace joins `tools/run-native-oracle-parity.sh`. The mapper,
  validator and client keep the AcidSplat and AcidPuddle identities; MagmaBallExplosion
  draws as generic monster magic until its art lands. Cohort capture no longer asks a
  native monster Arrow for a C2 flight owner.
- Depth journey timing: `test-godot-native-depth-progression.ps1` prints, per launcher
  run, the time outside the Warrior journey and the journey's wall time against server
  ticks by step kind (travel, fight, Town, checks), from a `<phase>-timing.json` the
  acceptance writes even on failure. The first L5–L8 runs were fully clock-bound (clock
  share 1.00). A Warrior that steps onto stairs while clearing a pack now walks back.
- Catacombs melee monsters (batch 2.4a): on L5–L8 Zombie, Fallen, Skeleton, Scavenger,
  Sneak (Stalker/Unseen), Goat melee, Fat, Bat, Gargoyle and Horned Demon fight with
  native AI: Sneak fade in/out and its Retreat goal, Delay timing, var1/var2 stand
  records, and the hit and attack sound draws. Fade and Sneak state are saved only in
  the Catacombs cohort format; older formats refuse them. Live Acid flights keep their
  native missile identity in the mapper, validator and client. A ten-lane native AI
  trace joins `tools/run-native-oracle-parity.sh`. The depth journey kills on every
  floor to L8 (trapped Catacombs doors are skipped until 2.5) and gains
  `-FirstFightDepth` for development runs. `warrior-l8` is now clvl 18 in Catacombs
  plate: in breast plate it ran out of potions against L6–L8 packs.

## 2026-10-29

- Render the Catacombs (batch 2.3): with the native packs, L5–L8 draw native L2 floors,
  walls and specials under `l2_1.pal`, the player's dungeon animations, and chests,
  L2 doors (42/43), traps and barrels from a new `native-object-catalog-catacombs.json`
  beside the Cathedral object catalog. Profiles carrying the Catacombs pack bind L5–L8 to
  the `catacombs` location; the scene pack gains a third family. Existing two-family
  scene packs and Cathedral object catalogs still load. The native scene capture also
  exports the L2 art. The depth journey requires native draws on every floor down to
  its target. Lighting, palette cycling and wall blending stay open for every level.

- Single-player Town warp (batch 2.2c): L5's warp-up (piece 558) returns to Town at
  (49,22) and sets the Town's `WarpUnlocks` Catacombs bit, as native `WM_DIABTWARPUP`;
  the Town is rebuilt with its warp opening (same closed-Hell choices and catalog
  fingerprint), and (49,21) then leads to L5 at any clvl. The unlock is saved in the
  existing Town checkpoint; older single-player saves without the warp pair restore
  with it closed. Warp gates (clvl, first entry, Town warp) now come from trusted
  content on restore, which also fixes 2.2b multiplayer restores losing the clvl 8 gate.
  The client can click native Catacombs doors (42/43). Live C2 Arrow flights no longer
  fail snapshot validation, and a floor with a killed Catacombs archer restores on a
  fresh host. The headless depth journey at `-TargetDepth 5` opens L5's doors, takes
  the warp-up and the Town warp, and resumes with the warp open.

## 2026-10-28

- Multiplayer Catacombs (batch 2.2b): with the Catacombs pack, native multiplayer runs
  L1–L8 with native multiplayer rules on L5–L8 (full monster life, 150% scatter,
  multiplayer shrine, item and spell pools). The multiplayer Town opens its warp tiles;
  the warp at (49,21) takes clvl 8+ players to L5 (entered first by `TownWarpDown`),
  lower levels are turned back one tile south, and L5's warp-up returns to (49,22).
  Floor entries may now skip levels; the optional cohort field `CatacombsEntry`
  records a Town-warp entry. `tools/run-native-oracle-parity.sh` adds multiplayer
  Catacombs theme-pass and chest captures; `test-godot-native-multiplayer.ps1
  -TargetLevel 5 -TestParticipant` sends two clvl 8+ Warriors through the warp.

- Cohort checkpoints off the poll path: clock-gated cohort polls serve the last durable
  tick, saving every 20 ticks or on a due command; format 13 writes geometry once (24→14 MB
  at L8). L8 poll median drops from about 1.6 s to 60 ms; older formats still load.

- Descend to L5–L8 (batch 2.2a, server): the Catacombs pack (1.1.0) adds the `l2.amp`
  automap; the native Town run continues by stairs to L8 under run identity
  `native-town-l1-l8-v1`, generating Catacombs floors on first entry for all three
  classes. Cohort format 12 saves their doors, Gargoyle phases, ranged actions and
  Arrows. 1.0.0 profiles keep the L1–L4 run and format 11. A stair crossed mid-path
  no longer changes level: like native CheckTriggers, it fires only once the walk
  stops there. The protocol envelope cap is 4 MiB, since snapshots carry every
  entered level's geometry (about 1.2 MB at L8).

- Catacombs in the profile (batch 2.1): setup builds the L5–L8 metadata (L2 geometry,
  levels 5–8 rules, L8 books) from the archive into the all-class profile as a fifth
  pack; the server exposes it as `NativeCatacombsMetadata`. Older profiles still load.

## 2026-10-27

- Combat processor split (batch 1.7): `DiabloCombatCommandProcessor.cs` (18,387 lines) moves
  whole members, in order and unchanged, into twelve partials for player attacks, spells,
  monster AI, missiles and player death; the main file keeps state and entry points.

- Shared per-level state (batch 1.6): ten level-keyed registries in `DiabloWorldState`
  (population owners, door levels and owners, monster phases, ranged mappings, floor
  entries) are one `LevelState` per level; both floor admissions share adoption and item merging.

- Two-player native smoke closed (batch 1.5b): both multiplayer phases wait until
  L1 draws both participants natively, and `test-godot-native-multiplayer.ps1
  -Windowed` runs the pair windowed, saving each client's capture of its peer.

- Two-player native smoke (batch 1.5b, headless): `tools/test-godot-native-multiplayer.ps1`
  runs `NativeMultiplayerJourney` (a Warrior and a Rogue each kill on shared L1) and
  `NativeMultiplayerResume` (both restore on a fresh host and take their next
  command). Native item occupancy now marks every participant on a level (native
  `dPlayer` index + 1) instead of refusing a second player, and barrel collision
  resolves the player by that index.

## 2026-10-26

- Multiplayer native Cathedral (batch 1.5a): the all-class native profile hosts
  multiplayer. The first participant's class generates the run; others pick any
  class. Cathedral generation follows native multiplayer rules: no monster life
  halving, 150% scatter counts, the multiplayer shrine list, and Resurrect/Heal
  Other in the spell, staff and item pools. Monster AI reseeds per tick as native
  `MonsterSeeds`. The native fresh-population oracle gains a multiplayer capture
  (`test-cathedral-fresh-population.ps1 -Multiplayer`), and the launcher accepts
  `-Multiplayer` with the native Town profile and the `NativeMultiplayerCreate`
  phase. Healer Resurrect stock and multiplayer Town warps stay open.

- Playtest controls (batch 1.4): left click on the floor walks instead of casting
  the class skill (the spell selector's fallback made every floor click a cast);
  holding the button or a WASD key keeps walking (WASD are now screen-relative);
  left click attacks monsters and right click casts the readied spell on empty
  tiles. G / Shift+G add DevilutionX's brightness tone curve, saved per user.
  World art samples nearest-pixel. The launcher reads the server log as one string
  so a partly written log no longer fails port detection.

- Native CI repair: format `legacy-native/` and `test/` with clang-format 18;
  build `replay_fixture.cpp` only into `replay_test` (3DS); fall back to
  `app_fatal` in the content manifest without exceptions (Switch); fix stale test
  includes, the `pack_test` rename, `path_test` matchers and SDL3 fills. Delete
  the workflows whose packaging `2fe67d708` removed (Linux cross, MinGW, Win9x,
  XP, MSVC, PS4, PS5, Xbox, Xbox One, Amiga, Vita, Miyoo, OpenDingux, RetroFW);
  Linux SDL1 and macOS now build without packaging. Six pre-existing native test
  failures (pack, writehero, Wirt purchase, timedemo) remain open.

## 2026-10-25

- Rendered slice (batch 1.4): the Warrior depth Journey checks native scene,
  player and resident draws in Town and on each of L1-L4, and drives the Hero,
  Items and Trade panels with real controls (class skill, weapon to the
  inventory and back, Smith buy and sell). `test-godot-native-depth-progression.ps1
  -Visible` runs it windowed with a capture per floor and panel; the launcher
  allows a window for that phase only. Route travel takes an optional caller
  interrupt.

- Cathedral L1-L4 by default (batch 1.3): the all-class profile's new
  `NativeDepthJourney`/`NativeDepthResume` phases take an ordinary-melee Warrior
  from Town to a target floor (one XP kill per floor, Town healer and Pepin's
  potions between floors) and back, then prove every floor retained on a fresh
  host. `test-godot-native-depth-progression.ps1` accepts the all-class profile
  with `-TargetDepth` and `-TestParticipant`; acceptance-only
  `--test-participant` starts a played hero (level, attributes, gear) from
  `tools/native-test-heroes/`. Remove the `run-godot-cathedral-playtest.ps1`,
  `-depths-` and `-doors-` launchers; their profiles stay available through
  `run-local-client.ps1` parameters.

## 2026-10-24

- One native profile for all supported classes (batch 1.2): native setup now
  builds a format-6 all-class Town profile whose class progression has one start
  row each for Warrior, Rogue and Sorcerer. The new all-class adapter activates
  one native run per class and keeps the run the first participant picks or a
  resumed cohort validates against; the setup screen offers all three. Add a
  headless Rogue Journey/Resume (`test-godot-native-classes.ps1 -Class`). Class-bound
  profiles, adapters and saves are unchanged.
- Fix the 1.1c default launch, which put saves outside the profile's `sessions`
  folder that the Town launcher requires: setup now keeps each profile at
  `profile-<content hash>` with its saves, reused while its content is unchanged.
- First-run native setup (batch 1.1c): `run-godot-client.ps1` now finds the
  player's `DIABDAT.MPQ` (remembered `-DiabloArchive`, `GODEVILUTION_DIABDAT`, the
  DevilutionX data folder, the GOG registry, usual install folders), builds the
  Warrior and Sorcerer Town profiles and all four art packs once per input
  fingerprint with the new `tools/Native.Setup`, and starts native Town; without
  an archive it explains and starts the authored content. The art importers take
  `--archive` (with the open `assets` layered in as native does) instead of an
  extracted tree. Fix two launcher profile checks that split CRLF rows wrongly on
  Windows PowerShell 5.1.
- Assign planning to Claude Opus 5.5 and supervision to Astra 5 (medium), with
  a fixed pool of two Sol 5.6 implementers and one Sol 5.6 reviewer at
  extra-high effort. Workers run focused checks only; the supervisor runs the
  broad suites once per batch.
- Add `Cathedral.Playtest --export-native-sources` (batch 1.1b): C# ports of the
  native item, spell, monster, object, unique-monster, level-rule, Cathedral SOL
  and cursor-footprint loaders build every native profile compiler input from the
  player's archive, the open assets and source constants, with no native engine.
  Warrior and Sorcerer profiles compiled from it are byte-identical to the
  native-built ones. Open-data tables are pinned in
  `test/fixtures/native-open-metadata` and checked in CI.

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
  build configuration.
- Report a poisoned clock-gated lane's failure when host cancellation ends the
  connection before its writer observes it, and make the observer-fault test fail
  only the command's publication so it no longer races a periodic checkpoint fault.
- Lengthen the Warrior block window in the live PvP shield-block test's content
  overlay so loaded runners cannot sample snapshots on either side of it.
- Render the Godot client with Forward+ on Vulkan by default and disable the
  OpenGL compatibility fallback; a client test pins the project settings.
- Regenerate the archive-free native oracle corpora in CI and run their managed
  parity comparisons; restore the Cathedral chest capture's dropped sixth lane.
- Add the worker guide (batch contracts, porting, verification, documentation
  budget, stop rules, reviewer checklist), a Phase 1 batch queue, and a current
  session handoff that no longer points at frozen Catacombs work.
- Switch the worker pool to two GPT-6.1 Sol implementers plus a separate GPT-6.1
  Sol reviewer that never reviews its own work.
- Run the Sol implementers at medium reasoning effort and the reviewer at
  extra-high effort.
- Fix the intermittent live PvP shield-block test: the legacy seeded hit roll
  depends only on `(tick ^ ids) mod 4` early in a game, so attempts that kept
  landing on one tick residue could all miss. Attempts now cycle residues, and
  failures report per-attempt state. The roll itself is an open defect
  (inventory 2.8). The live towner-travel helper now retries a move rejected
  `TooLate` after catching its model up to the host tick.
- Revise the work plan: mark Phase 0 done, move a rendered human-playable
  Town→L4 slice and a two-player smoke into Phase 1 ahead of the refactors,
  specify first-run setup end to end with automatic profile rebuilds, and record
  which proven Catacombs pieces Phase 2 reuses.
- Split Phase 1 batch 1.1 into the C# MPQ reader (1.1a), C# semantic table export
  (1.1b) and first-run setup (1.1c). Add `tools/Native.Archive`, a faithful C# port
  of the pinned `mpqfs` read side (crypto, sector streams, PKWARE explode), with a
  native `mpqfs` oracle run in CI against a synthetic archive and `spawn.mpq`.
  The redistributable `spawn.mpq` uses multi-method zlib sectors, so the reader
  decodes PKWARE and zlib sectors as `mpqfs` does; bzip2 sectors are refused.
- Poll the legacy snapshot-timing client test until its fake server observes the
  snapshot request instead of assuming the ACK arrives within 20 ms.
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

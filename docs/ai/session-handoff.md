# Clean-session handoff

Prepared 2026-10-22. Use `D:\Programming\Godevilution` and its existing working
tree. Fresh context does not mean a reset or a replacement checkout.

## Read first

1. [AGENTS.md](../../AGENTS.md)
2. [Worker guide](worker-guide.md): how to choose, size, port, verify, document
   and hand off a batch, and the patterns that previously sent work off track.
3. The current phase of the [work plan](../work-plan.md), including its batch
   queue.
4. [Working status](../migration-status.md) sections on limits and environment.

Search other documents only for the contract you need. Do not read the
histories end to end or repeat their tests to rebuild context.

## First task

Take the next unstarted batch in the current phase's queue in the
[work plan](../work-plan.md), write its batch contract as described in the
[worker guide](worker-guide.md#batch-contract), and start. Catacombs-only parity
work is frozen; unfinished earlier drafts are on branch `wip/phase0-shelf` and
are recovered piecemeal only when a batch needs them.

## Session goal

Complete Godevilution's migration to a server-authoritative C# engine and fully
playable Godot client, including supported Diablo and Hellfire progression,
native behavioral compatibility, full native graphical parity in Godot, shared
single-player/multiplayer infrastructure, optional client MPQs and retirement of
production C++/SDL and Lua dependencies. The server must never require MPQs.
Keep engine, validated external data, game modules, hosting and presentation
separate. The server owns gameplay, RNG, world state and persistence. The
[work plan](../work-plan.md) owns the order: Diablo single-player first.

## Operating policy

- Follow [working status](../migration-status.md#working-environment-and-authority)
  for environment, retained holds and authority. Preserve unrelated work, use
  CRLF (except shell scripts and Godot-written files), and keep `master` CI green.
- No reset, bulk commit, security changes or new archives. Push only on explicit
  user instruction. Git history holds past versions; do not create backup
  documentation or QA dumps.
- Use the [fixed worker policy](../completion-plan.md#worker-delegation).
  Claude Opus 5.5 plans and selects batches; Astra 5 at medium effort
  supervises the Sol 5.6 workers and owns integration, verification, concise
  documentation and cohesive commits. Every worker and reviewer follows the
  [worker guide](worker-guide.md).

## Batch 2.2b Multiplayer Catacombs (done)

- **Player-visible outcome:** in a native multiplayer game, players of clvl 8 or
  higher take the Town warp at (49,21) to L5; lower levels are turned back with
  native "I can't get there from here". L5-L8 generate and play with native
  multiplayer rules, and L5's warp-up stair returns to Town at (49,22).
- **Native source:** `triggers.cpp` IsWarpOpen, InitTownTriggers, InitL2Triggers
  (piece 558) and CheckTriggers (WM_DIABTOWNWARP clvl gates, WM_DIABTWARPUP);
  `town.cpp` CreateTown warp tiles and ENTRY_TWARPUP view positions;
  `monsters.cpp` InitMonsters (`GetMpMonsterMultiplierPercent`), InitMonster and
  PrepareUniqueMonst life; `items.cpp` and `objects.cpp` multiplayer branches
  (spell-book pools, unique reuse, shrine availability) for L5-L8.
- **Files and contracts owned:**
  - the multiplayer Town composes with its open warp tiles (Catacombs, Caves,
    Hell drawn open; only the Catacombs warp is wired, Caves and Hell are
    published unavailable until their families exist);
  - Town warp and L5 warp-up routes in the native progression, with the clvl 8
    gate and TownWarpDown first entry into L5;
  - the single-player gates in the Catacombs population, chest, object and
    actor owners accept multiplayer, bound to the game mode;
  - CI-runnable multiplayer variants of the Catacombs theme-pass and chest
    captures in `tools/run-native-oracle-parity.sh` with their consumers;
  - a native multiplayer journey/resume that reaches L5 by the Town warp, with
    clvl 8+ test heroes.
- **Exit check:** the multiplayer Catacombs oracles match; server tests for the
  warp gates, warp-up return and multiplayer L5-L8 restore;
  `tools/test-godot-native-multiplayer.ps1` journey and fresh-host resume with
  both players on L5; Diablo.Tests, Server.Tests, client protocol tests and the
  oracle consumers pass.
- **Out of scope:** the single-player Town warp unlock (`pTownWarps`, set by the
  L5 warp-up) because it needs a cohort field; it is 2.2c after the cohort-save
  work merges. Also rendering (2.3), monster AI and missiles (2.4), object
  effects (2.5), Caves and Hell, quests.
- **Size:** about five days; if the oracle variants alone exceed two, split the
  Town warp work into its own batch.
- **State:** done. With the Catacombs pack, multiplayer is an L1–L8 run with the
  multiplayer Town catalog (open warp tiles); only the Catacombs warp is wired,
  and Caves and Hell are not published as routes. The optional format-12 field
  `CatacombsEntry` records a `TownWarpDown` entry, and floor entries may skip
  levels. That field touches one line of `DiabloCohortCodec.IsCatacombsActorField`,
  which the cohort-save branch also edits. The two-client L5 smoke meets and
  resumes on L5 without fighting there (Catacombs AI is 2.4).

## Batch 2.2a Descend to L5-L8 (done)

2.2 is split along its playable seam: 2.2a single-player descent and resume,
2.2b the multiplayer rules, clvl 8 Town warp and two-client L5 smoke.

- **Player-visible outcome:** on the default profile any class takes the L4 down
  stair to L5 and on to L8 and back up; each floor generates on first entry,
  saves, and resumes on a fresh host. L5-L8 are not drawn natively yet (2.3).
- **Native source:** `diablo.cpp` LoadGameLevel/`CreateLevel` for DTYPE_CATACOMBS
  (seeds from `glSeedTbl`); `drlg_l2.cpp` stair entries (`ENTRY_MAIN`/`ENTRY_PREV`);
  `automap.cpp` InitAutomap (`l2.amp`); `items.cpp` elixir rows for L5-L8 drops.
- **Files and contracts owned:**
  - setup: the Catacombs pack gains the `l2.amp` automap tables and the elixir
    item semantics (another content identity; old saves stay in their kept profile);
  - `AuthoritativeCatacombsNativePopulationBuilder` admits Rogue;
  - the native progression becomes family-aware through depth 8 under a new run
    identity: Cathedral owners for 1-4, Catacombs owners for 5-8, generated
    L4-L8 stair routes, first-entry admission through the Catacombs admission;
  - an additive cohort format captures and restores Catacombs doors, monster
    phases, ranged actions and arrows from the saved first-entry contexts;
  - the client depth journey continues past L4 (no native draw check below L4)
    with depth-8 test heroes.
- **Compatibility:** saves in earlier formats and profiles keep loading; a
  profile without the new tables keeps its L1-L4 run identity.
- **Exit check:** unit tests for the family-aware progression, the Catacombs
  cohort round trip and old-format loads; the explicit archive reference tests;
  headless `test-godot-native-depth-progression.ps1 -TargetDepth 8` with a
  Warrior and a fresh-host resume on L8; a Rogue and a Sorcerer reach L5 in
  server tests; Diablo.Tests, Server.Tests, client protocol tests and the oracle
  consumers pass.
- **Out of scope:** multiplayer (2.2b), Town warps including the L5 warp-up
  (2.2b), rendering (2.3), monster AI and missiles beyond what is already
  committed (2.4), object effects (2.5), quests.
- **Size:** three to five days.
- **State:** server and setup work done. Format 12 (`DiabloNativeCatacombsCheckpoint`:
  doors, Gargoyle phases, ranged actions, Arrows) restores by regenerating each
  first entry and reapplying saved door states. The explicit
  `AuthoritativeCatacombsNativeProgressionTests` take all three classes
  L1→L8, save, restore cold byte-identically and climb back. A stair crossed
  mid-path now fires only when the walk stops on it; the envelope cap is 4 MiB.
  The headless `-TargetDepth 8` Journey passes; its fresh-host Resume needs
  `-ServerBuildConfiguration Release`. Open limit: the host saves the whole cohort
  on every snapshot poll, so snapshots arrive every 1–2 s; this also makes the
  Town held-WASD step flaky. It is being handled in its own session.

## Batch 2.1 Catacombs in the profile (done)

- **Player-visible outcome:** none yet; first-run setup puts the L5-L8 generation
  and population inputs into the default profile, so 2.2 can descend without new
  setup or an MPQ on the server.
- **Native source:** `LoadLevelSOLData`/`LoadMegaTiles` for DTYPE_CATACOMBS
  (`l2.til`, `l2.sol`, no SOL patches); `leveldat.cpp` rows for depths 5-8; the
  L8 story books (`TEXT_BOOK12/22/32`).
- **Files and contracts owned:** `NativeSourceExport` and its geometry/rule
  exporters build the Catacombs metadata tables from the archive; the all-class
  Town compiler writes them as a fifth schema-neutral pack
  (`native_catacombs_population`) bound by `contentManifestHash`; the Diablo
  content bootstrap loads it, when present, as `DiabloContentActivation.NativeCatacombsMetadata`;
  the launcher, art-profile reader and setup pass it through.
- **Compatibility:** profiles without the pack still activate. A new
  setup has a new content identity, so earlier saves stay in their kept profile
  folder and load from it explicitly (the 1.2 precedent); they do not migrate.
- **Exit check:** an explicit test matches the archive-built Catacombs tables
  byte for byte against the native-exported pack; unit tests cover the pack's
  optional loading and refusal of partial packs; `Native.Setup ensure` from a
  clean setup root produces the pack and a changed input rebuilds; the headless
  Town-L4 journey passes on the new profile; Diablo.Tests, Server.Tests, client
  protocol tests and the C2 oracle consumers pass.
- **Out of scope:** admission, descent, rendering, Catacombs art, quests.
- **Size:** a few hundred lines.
- **State (2026-10-28):** done. The archive-built tables match the native export byte
  for byte; setup rebuilt the existing setup root (not an empty one) into
  `profile-50bae5ff90ec1142` and reused it on rerun; the
  headless Town-L4 journey and resume pass on it. The pre-2.1 profile still activates
  (the host serves its Town), but its art packs were replaced, so its draw checks
  fail; old saves were not resumed. Suites and 132 oracle consumers pass. 2.2 is next.

## Batch 1.7 Split the combat processor (done)

- **Player-visible outcome:** none by design; combat code becomes reviewable
  per native module before Phase 2 ports into it.
- **Native source:** module boundaries only: `player.cpp`, `spells.cpp`,
  `missiles.cpp`, `monster.cpp`.
- **Files and contracts owned:** `DiabloCombatCommandProcessor.cs` (18,387 lines)
  moves whole members, in order, into partial files named for those modules. The
  main file keeps fields, the constructor and the command/tick entry points.
  No member body changes.
- **Exit check:** no combat file over about 2,000 lines; the moved text equals the
  original member text; Diablo.Tests, Server.Tests and client protocol tests pass.
- **Out of scope:** renames, reordering inside members, other large files.
- **Size:** under a day.
- **State (2026-10-27):** done. Twelve partials of 1,057-1,887 lines; the main file
  keeps 627. The concatenated member text equals the original apart from 13 blank
  lines at the cuts. Phase 1 is complete except the user's 1.4 playtest.

## Batch 1.6 Shared per-level state (done)

- **Player-visible outcome:** none by design; later floors extend one per-level
  state instead of adding a registry per feature.
- **Native source:** none (structural).
- **Files and contracts owned:** the level-keyed registries in the
  `DiabloWorldState` partials (population owners, C1/C2 door levels, C2 door
  owners, monster-phase orders and bindings, ranged missile catalogs, floor
  entries) become one `LevelState` per level, and both floor admissions adopt a
  detached floor's level states in one step. Entity-keyed state maps stay.
- **Exit check:** Diablo.Tests, Server.Tests and client protocol tests pass; the
  CI oracle comparisons pass; cohort and native save captures are byte-identical
  (existing checkpoint tests); the native depth journey and resume pass headless.
- **Out of scope:** the entity-keyed maps, cohort save formats, behavior changes.
- **Size:** one to two days.
- **State (2026-10-27):** done; net 3 fewer lines. Diablo.Tests (except the known
  worker-delegation document check), Server.Tests and client protocol tests pass;
  all 132 archive-free Cathedral/Catacombs oracle consumers pass on fresh captures.
  The MPQ reader capture needs `spawn.mpq`, absent locally; CI runs it. Entity-keyed
  maps (barrels, sarcophagi, traps, doors, phases, ranged actions) stay per entity.
  The headless Town-L4 journey (`warrior-l4`) and fresh-host resume pass. 1.7 is next.

## Batch 1.5b Two-player smoke (done)

- **Player-visible outcome:** two clients join one native multiplayer host,
  see each other and fight together on L1, then resume on a fresh host.
- **Builds on:** 1.5a `NativeMultiplayerCreate` (two headless clients meet on
  L1) and the authored `CooperativeJourney`/`CooperativeResume` pattern.
- **Files and contracts owned:** a native multiplayer journey/resume phase pair
  in `SessionAcceptance`, its launcher gate, and cold restore of
  `nativePopulationMultiplayer` with mixed-class participants.
- **Exit check:** scripted two-client journey with cold resume; both clients draw
  each other natively. Windowed runs need the user's approval; run headless first.
- **Out of scope:** Resurrect/Heal Other in play, Catacombs, Town warps.
- **State (2026-10-27):** done. The journey and resume pass headless and
  windowed (`tools/test-godot-native-multiplayer.ps1 [-Windowed]`); each client
  waits for L1 to draw both players natively and, windowed, saves a capture.
  One earlier windowed attempt timed out on that draw wait on both clients, before
  its failure reported draw state; two reruns passed. 1.6 is next.

## Batch 1.5a Multiplayer native Cathedral (done)

Done 2026-10-26: oracle, server theory and `NativeMultiplayerCreate` pass (see
test history). Open gaps, recorded as inventory 9.24: the Healer's multiplayer
Resurrect scroll (the profile has no random or third pinned Healer stock),
multiplayer Town warps (native opens them at clvl 8/13/17 into Catacombs and
beyond), and native delta re-entry, for which retained floors stand in. Monster
AI seeds use the server tick as native `sgdwGameLoops`.

1.4 stays open for the user's Town-L4-Town playtest. Batch 1.5 was split: the
native profile's Cathedral owners reject multiplayer ("Only ordinary
original-Diablo Cathedral SP Normal generation is supported"), so two players
first need the native multiplayer rules.

- **Player-visible outcome:** a multiplayer game on the native profile starts,
  generates Town and Cathedral L1-L4, and plays with native multiplayer rules.
- **Native source:** `monsters.cpp` InitMonster and PrepareUniqueMonst (no
  single-player life halving); `items.cpp` spell-book and oil pools,
  `UniqueItemFlags` reuse, Healer multiplayer pinned stock; `diablo.cpp`
  LoadGameLevel re-entry (multiplayer regenerates from the level seed); the
  monster AI random streams gated on single-player in the combat processor.
- **Files and contracts owned:** the single-player gates in the Cathedral
  population, item, monster-metadata and door owners and in combat AI; the
  native population oracle export (`test/cathedral_population_test.cpp`) gains
  a multiplayer variant; the launcher accepts `-Multiplayer` with the native
  profile.
- **Exit check:** multiplayer native oracles for L1-L4 match the C# generators;
  Diablo.Tests and Server.Tests pass; a headless two-client create on the
  native profile reaches L1.
- **Out of scope:** quests, Catacombs, multiplayer-only spells in play
  (Resurrect, Heal Other), the two-client fight and resume (1.5b).
- **Size:** several days; split again if the population oracle work alone
  exceeds that.

## Batch 1.4 Rendered slice (awaiting playtest)

- **Player-visible outcome:** the windowed native launch draws Town and
  Cathedral L1-L4 with native player, scene, resident and object art, and the
  Hero, Items and Trade panels work on the native profile with real controls.
- **Native source:** none ported; draws use the existing native art catalogs.
- **Files and contracts owned:** the Warrior depth journey gains per-floor
  native draw checks (Town and each Cathedral floor), PNG captures when visible,
  and panel steps (Items equip swap, Hero attributes and skill source, Smith
  buy and sell); the launcher and `test-godot-native-depth-progression.ps1`
  accept `-Visible` for the NativeDepth phases only; fixes for rendering or
  control defects those steps expose.
- **Exit check:** `test-godot-native-depth-progression.ps1 -TargetDepth 4
  -TestParticipant tools/native-test-heroes/warrior-l4.json -Visible` passes
  with a capture per floor and panel; the headless per-floor pairs still pass;
  the user plays Town-L4-Town through `run-godot-client.ps1` and reports it
  playable.
- **Out of scope:** native monster art, lighting, palette cycling, wall
  transparency, native-styled panels, Rogue/Sorcerer visible runs, looting.
- **Size:** a few hundred lines plus whatever fixes the visible run exposes.

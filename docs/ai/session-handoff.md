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

## Batch 2.4b Catacombs ranged and missile monsters (done)

- **Player-visible outcome:** on L5–L8 the ranged families fight as native: Skeleton
  archers (Burning Dead/Horror), Goat archers (four clans), Acid Beast and Poison
  Spitter (acid bolt, splat, puddle), Magma Demon and Blood Stone (magma ball), and
  the Familiar's lightning. The Warrior journey fights through them on every floor.
- **Native source:** `monsters.cpp` AiRanged, AiRangedAvoidance, SkeletonBowAi and
  BatAi's Familiar branch, StartRangedAttack/StartRangedSpecialAttack and the ranged
  mode updates; `missiles.cpp` AddArrow/ProcessArrow, AddAcid/ProcessGenericProjectile,
  ProcessAcidSplate, AddAcidPuddle/ProcessAcidPuddle, AddMagmaBall, AddLightning/
  ProcessLightning, and the monster-missile hit path.
- **Files and contracts owned:**
  - port the three ranged AI functions for L5–L8 native actors into the shared
    processor (`DiabloCombatCommandProcessor.MonsterAi*`), retiring the tuning-based
    ranged controllers for those actors; reuse the existing C2 release clocks and
    Arrow flight where they already match;
  - Acid, splat, puddle, magma ball and lightning flights in the shared projectile
    state; additive cohort fields only where new state must persist;
  - the shelved `wip/phase0-shelf` caster chains are read for reference, not merged;
  - `ThemeRoomGenerationTest.CaptureNativeCatacombsRangedAi` (archive-free, in
    `tools/run-native-oracle-parity.sh`) free-runs one lane per family as 2.4a does,
    recording actor, player life, RNG and missile count each tick, with its managed
    consumer.
- **Exit check:** the ranged lanes match every tick in CI; the headless
  `test-godot-native-depth-progression.ps1 -TargetDepth 8` with `warrior-l8` kills on
  every floor and resumes; Diablo, Server and client suites and both smokes pass.
- **Out of scope:** monster missile art and sounds beyond their RNG draws, lighting
  from missiles, Red Death/Storm (not spawned), uniques' AcidUnique branch unless an
  L5–L8 unique uses it, Caves families, multiplayer AI differences.
- **Size:** four to five days.
- **State:** `CaptureNativeCatacombsRangedAi` shares one lane runner with the melee
  capture and records seven lanes with their missiles; all seven match every tick
  (`AuthoritativeCatacombsRangedAiNativeOracleTests`) and the ten melee lanes still
  do. Decision taken: ranged modes live on the actor (`NativeAiVars` and the native
  cursor's `RangedMissile`/`RangedDamage`, in `DiabloCombatCommandProcessor.NativeRangedAi`)
  and missiles on their projectiles (`AuthoritativeNativeMonsterMissileState`, processed
  in `DiabloCombatCommandProcessor.NativeMonsterMissiles`). Native ProcessMissiles
  reaches a missile created during the pass in the same pass. Saved C2 ranged
  actions and Arrow flights still finish under their floor-owned controllers; new
  ones are no longer started for these families. Gloom keeps the legacy Bat path
  (its Rhino charge is untranslated). Verified with the `-FirstFightDepth 5` journey
  (kills on L5–L8, resume) and both smokes; the depth journey now writes
  `<phase>-timing.json` and showed a fully clock-bound run (clock share 1.00). The
  `-TickRateHz` passthrough exists; the command lead now uses the recent maximum
  probe round trip (melee 36 → 10 ticks at 20 Hz). Open: a 40 Hz journey froze its
  L6 snapshot view for about 23 minutes with no command pending; a traced 60 Hz run
  reproduced it: the client's poll task stops completing (likely a socket write blocked
  while the host's bounded per-connection response queue waits on its writer). At
  60 Hz the 20-tick cohort checkpoint also saturates the host (probe p90 325 ms).
  `DiabloSimulationSnapshot.cs` (3,400+ lines) still needs its zero-behaviour split.

## Batch 2.4a Catacombs monsters in play: melee families (done)

- **Player-visible outcome:** on L5–L8 the melee families fight as native: Sneak
  (Stalker/Unseen), Gargoyle (wake, perch, special), Goat melee (circling charge),
  Fat (Overlord/Mud Man), Bat (Blink/Gloom/Familiar swoop and retreat), plus the
  C1 families (Zombie, Fallen, Skeleton, Scavenger) at their L5–L8 variants. The
  Warrior kills one monster per floor on L5–L8 in the headless journey, as on L1–L4.
- **Native source:** `monster.cpp` MAI_Sneak, MAI_Garg, MAI_GoatMc (MAI_Round),
  MAI_Fat, MAI_Bat, and the shared walk/attack/hit/death paths they call
  (M_StartAttack, M_StartSpAttack, M_TryH2HHit, MonsterAttackPlayer).
- **Files and contracts owned:**
  - audit each family's existing managed AI (`DiabloCombatCommandProcessor.MonsterAi*`)
    against native, in native call and RNG order; port what differs, delete what
    no family uses;
  - lift the Catacombs runtime admission so every L5–L8 monster ticks through the
    shared processor with its native AI (today only Gargoyle phases and the ranged
    owners are Catacombs-specific);
  - one archive-free native per-tick AI trace per family (seeded floor, scripted
    player, N ticks of mode/position/RNG/HP) added to
    `tools/run-native-oracle-parity.sh`, with its managed consumer;
  - the depth journey's per-floor kill extends to L5–L8 (`NativeWarriorFoughtFloor`
    = 8), with resume checking the retained corpses.
- **Exit check:** the family traces match in CI; the headless
  `test-godot-native-depth-progression.ps1 -TargetDepth 8` with `warrior-l8` kills on
  every floor and resumes; Diablo, Server and client suites pass.
- **Out of scope (2.4b):** ranged and missile families: Skeleton and Goat archers'
  release/flight already exist and are only rechecked; Acid spit and puddles, Magma
  bolts, the shelved Magma/Acid chains, Familiar lightning. Monster art, lighting,
  uniques' quest behaviour (set levels), multiplayer AI differences beyond the
  existing per-monster seeds.
- **Size:** four to five days; 2.4b follows at a similar size.
- **State:** `ThemeRoomGenerationTest.CaptureNativeCatacombsMeleeAi` (registered in
  `tools/run-native-oracle-parity.sh`) free-runs ten lanes for 300 ticks of
  ProcessMonsters + ProcessMissiles against a passive, durable Warrior on a fully
  visible open floor (fresh-level ClearMVars state, live `pLvlLoad = 0`, placeholder
  sprites for the charge missile). `AuthoritativeCatacombsMeleeAiNativeOracleTests`
  admits each actor through the production Catacombs adapter with the shipped
  monster-type, monster-missile and combat tables and compares RNG, player life,
  tile and hit points every tick. Zombie, Fallen, Skeleton, Scavenger, Goat melee,
  Fat, Bat (Gloom), Gargoyle and Horned Demon match all 300 ticks. Ported for L5-L8
  native actors: Fallen/Scavenger delegate to SkeletonAi; Delay spends its first
  count on the starting update and reruns without the roll on expiry; the Warrior's
  hit cry, the normal-attack sound and the charge-collision hit sound draw their
  variants; walk completion records var1 = walk mode, var2 = 0 for Bat,
  AiAvoidance, Overlord and Rhino; AiAvoidance short-circuits FlipCoin(4) in its
  Move goal and round-walks without a roll. Native SneakAi is ported (fade in/out
  on the Special graphic, hidden flag, Retreat goal, var1/var2); the fade cursor and
  `NativeSneak` state persist only in the Catacombs cohort format. All ten lanes match.
- **State (2026-10-31):** done. The full headless journey with `warrior-l8` (now
  clvl 18 in Catacombs plate) kills on each of L1–L8 and resumes; the harness drinks
  through packs, keeps a potion reserve for the walk out and skips trapped doors,
  which the server refuses until 2.5. Next: 2.4b.

## Batch 2.3 Render the Catacombs (done)

- **Player-visible outcome:** with the native packs, L5–L8 draw with native
  Catacombs art: floors, walls and `l2s` specials under `l2_1.pal`, the player's
  dungeon animations, and the Catacombs objects (doors open and closed, plus the
  other object kinds the L5–L8 populations place). Parity matches the Cathedral's
  today; lighting, palette cycling and wall blending stay as open for every level.
- **Native source:** `diablo.cpp` LoadLvlGFX (`DTYPE_CATACOMBS`: `l2.cel`, `l2.til`,
  `l2s`); `dungeon_common.cpp` (`l2.min`, `l2.sol`); `palette.cpp` LoadRndLvlPal
  (fixed to palette 1, as `l1_1.pal` is for the Cathedral); `objdat`/`objects.cpp`
  for the L2 object sprites.
- **Files and contracts owned:**
  - `tools/Native.SceneArt`: a third `catacombs` family from `levels/l2data`; the
    catalog gains the family and existing packs still load;
  - profile level bindings: levels 5–8 map to `catacombs` for scene, player
    (dungeon art) and object packs;
  - `tools/Native.ObjectArt`: Catacombs object sprites under the L2 palette;
  - client `NativeSceneCatalog`, `NativeObjectCatalog` and `WorldPresentation`:
    a Catacombs location;
  - `test/native_scene_art_test.cpp` captures the L2 art for the existing decode
    comparison;
  - the depth journey checks native draws on every floor down to the target.
    Setup rebuilds the packs by itself, because its fingerprint covers the tools.
- **Exit check:** scene and object importer/catalog tests with the Catacombs family;
  the native L2 art capture matches its managed decode; a fresh setup builds the
  packs; `test-godot-native-depth-progression.ps1 -TargetDepth 8` with `warrior-l8`
  (headless) requires native scene, player and object draws on L5–L8 and resumes;
  Diablo, Server and client suites pass. A visible capture run only with approval.
- **Out of scope:** native lighting and light radius, random palettes 2–4, palette
  cycling and transparency blending (open for L1 too), monster art (no level has
  native monster art yet), set levels, Caves and Hell, Catacombs AI (2.4).
- **Size:** three to five days.
- **State (2026-10-29):** done headless; the visible capture awaits approval. Profiles
  with the Catacombs sidecar bind L5–L8 to `catacombs`; the player pack maps them to
  its shared dungeon animations. The scene pack gains a third family; the object pack
  writes `native-object-catalog-catacombs.json` beside the Cathedral catalog (byte
  identical to before) with chests, L2 doors (42/43), traps and barrels under the L2
  palette. Older two-family scene packs and Cathedral-only object packs still load.
  The door step now retries a failed threat clear too (monsters crowd L5's doors).

## Batch 2.2c Single-player Town warp (done)

- **Player-visible outcome:** in single-player, stepping on L5's warp-up (piece
  558) returns to Town at (49,22) and opens the Catacombs warp: Town is drawn with
  its opening and (49,21) leads to L5 (no clvl gate, native SP). The unlock
  persists across save and fresh-host resume.
- **Native source:** `players.cpp` StartNewLvl (`WM_DIABTWARPUP` sets
  `pTownWarps |= 1 << (levelType - 2)`); `triggers.cpp` IsWarpOpen,
  InitTownTriggers and InitL2Triggers; `town.cpp` CreateTown warp tiles under the
  unlock mask.
- **Files and contracts owned:**
  - the L5 warp-up route in single-player (today multiplayer only) and an
    unlock effect on it that sets the Town's `WarpUnlocks` Catacombs bit;
  - Town geometry rebuilt from the unlocked settings (opening tiles, same closed
    Hell choices) and the Town warp route published once unlocked;
  - the Town checkpoint and catalog accept single-player `WarpUnlocks` Catacombs
    (today `None` only). Existing format 13 field, no new cohort format; saves
    without the unlock restore unchanged;
  - a native Town oracle case for unlock mask 1 if the existing capture lacks it;
  - the headless depth journey gains a warp leg: L5 warp-up, Town warp back to
    L5, and a resume that checks the open warp.
- **Exit check:** server tests for the SP warp-up unlock, the warp-down entry and
  a cold restore with the unlock; Town geometry matches native for mask 1;
  `test-godot-native-depth-progression.ps1 -TargetDepth 5` with `warrior-l8` and
  the warp leg, plus its fresh-host resume; Diablo.Tests, Server.Tests and client
  protocol tests pass.
- **Out of scope:** Caves and Hell warps (no families yet), Hellfire clvl
  unlocks, the multiplayer per-player bit (multiplayer opens all warps),
  rendering L5 (2.3), quests.
- **Size:** two to three days.
- **State (2026-10-29):** done. Warps carry `RequiredTownWarp`/`UnlocksTownWarp`; the
  Town catalog's `WithWarpUnlocks` keeps its fingerprint, and the world state rebuilds
  Town on unlock and publishes only open warps. Restored warps take behaviour from
  trusted content by id when topology matches (this fixed the 2.2b multiplayer clvl
  gate on restore). The existing Town oracle (warps 0–7) covers mask 1. Found on the
  way and fixed: Catacombs doors were not clickable in the client, MonsterArrow
  `"Arrow"` failed three snapshot validators (server crash on L5), and a killed
  Catacombs archer blocked fresh-host restore. L5's monsters fight already; their
  native AI parity stays 2.4.

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
  levels. The cohort checkpoint-cost work (format 13) is merged; 2.2c can start.
  The two-client L5 smoke meets and
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
  Town held-WASD step flaky. Fixed by the merged checkpoint-cost work (2.2a+).

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

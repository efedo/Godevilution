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

## Current batch: 1.5b Two-player smoke

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
- **State (2026-10-27):** the headless journey and resume pass
  (`tools/test-godot-native-multiplayer.ps1`). Open: the windowed run in which
  both clients draw each other, which needs the user's approval.

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

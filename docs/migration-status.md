# Godevilution Working Status and Memory

As of 2026-10-21. The project is in Phase 0 of the
[recovery plan](work-plan.md). `master` has a clean working tree; unfinished
drafts are shelved on branch `wip/phase0-shelf` (see below).

This file contains current facts, unresolved limits and session memory. It is
revised in place. Git history preserves superseded versions; do not add archive
copies, per-run diaries or repeated verification transcripts here.

## Current delivery position and verification limits

The migration is incomplete. The C# authority, content, protocol and persistence
infrastructure are substantial, while full native campaigns and the complete
Godot presentation client remain unfinished. The server does not require MPQs.

- The native Original Diablo, single-player Normal Rogue route passes Town ->
  L1 earned equipment/Gold -> Pepin purchase/healing -> actual L2 combat -> Town,
  followed by exact fresh-host restore and retained two-floor revisit.
- Cathedral L1-L4 generation, lazy floor owners, destination walking and pending
  bow/cold-state support exist behind explicit profiles. Backend proof does not
  establish a completed four-floor application journey.
- Explicit Warrior/Sorcerer profiles pass canonical native preparation, generated
  L1 combat, Repair/Recharge and exact fresh-host player/items/world/RNG/cursor
  continuity. Their later-floor class campaigns remain unqualified.
- Native chests, sarcophagi, ordinary/explosive barrels and Arrow wall traps have
  scoped operation and retained-mutation evidence. Other required object families
  remain open in the inventory.
- Held entity attacks are committed. Focused lifecycle tests, a clean client
  build and three actual held-input pairs pass. Repeat cadence is snapshot/ACK
  paced. The latest L3 run then dies before a kill or living checkpoint; no L3
  fresh-host or complete four-floor pass is claimed.
- Authored Godot combat, equipment, services, portals and scoped multiplayer/
  recovery journeys have evidence. They do not establish equivalent native
  generated-world campaigns or all class/difficulty combinations.
- The reviewed session batch passes headless create/return/rejoin and exact
  fresh-host resume, including current-model event clearing, confirmed-token
  retention and joined scene cleanup. Legacy native setup offers Rogue; explicit
  Warrior/Sorcerer profiles offer their bound class. Remote/default setup retains
  its existing choices. Rendered flow remains open.
- Optional original-player and Town/Cathedral scene imports pass full native
  frame/piece/special and published-atlas comparisons. Both class pairs issue
  actual native Town/Cathedral draws through shared isometric input, including
  native Town specials and exact cold restore. Native animation cursors, complete
  actor/object/effect coverage and rendered graphical parity remain open.
- Native profile inputs (Cathedral population metadata, monster runtime, item
  footprints, Town sources) are now built in C# from the player's archive and the
  open assets; the resulting class profiles are byte-identical to native-built ones.
  `run-godot-client.ps1` builds them, with the art, on first launch and starts native Town.
  One all-class profile offers Warrior, Rogue and Sorcerer; class-bound profiles still load.
  Its headless Warrior journeys reach L1-L4 and back with fresh-host resume (batch 1.3);
  a windowed run draws Town and L1-L4 natively and uses the Hero, Items and Trade panels (batch 1.4);
  click and held WASD walking and the DevilutionX brightness option followed the first playtest.
  The profile also hosts multiplayer with native multiplayer generation rules; two headless
  clients of different classes meet, draw each other and fight on L1, and resume on a fresh host,
  headless and windowed (1.5a, 1.5b).
- The pure MPQ-free Town family matches all 191 native cases. Additive format-5
  Warrior/Sorcerer profiles now compile semantic Town inputs and canonical
  residents, retain separate 112x112 fine/special and 40x40 coarse/protected
  channels in v11, and validate detached mutation replay without advancing shared
  RNG. Both headless pairs pass Smith/Pepin controls and full checkpoint retention.
  This is Original Diablo single-player Normal with quests disabled; exact NPC
  initialization/animation phase, complete vendor stock and quest-enabled Town remain open.
- Original Town residents now have immutable 11-instance projection, optional
  native CEL/PAL import and isometric drawing. All 248 frames and prepared native
  frame-order/delay traces compare exactly; both headless class pairs cover all
  anchors and exact cold retention. Live animation remains cosmetic, with native
  initialization RNG and persisted/cold animation phase still open.
- Optional Cathedral L1 object art now covers eight CEL sheets and ten native
  IDs using current authoritative frames. Source/atlas/anchor comparisons, an
  ordinary Warrior chest opening and exact inactive L1 object/collision retention
  pass. Other live lifecycle art checks and cold L1 redraw remain open.
- The Godot client is still a presentation prototype. Full native graphical
  parity, complete asset coverage and final rendered playability are open.

The [numbered inventory](feature-gap-inventory.md) owns feature-level completion
status and graphical-parity criteria. [Test history](test-history.md) records the
relevant verification scope; [implementation history](CHANGELOG.md) records
meaningful delivered changes. There is no calibrated overall percentage.

## Milestone status

| Gate | Status | Current position / remaining exit requirement |
| --- | --- | --- |
| M0: Launch and acceptance | Partial | Scoped launch, transport, cohort recovery and shutdown checks pass. Normal-profile adoption, C++ native compatibility-client moved-position cold resume and broader platform/fault/budget acceptance remain open. |
| M1: Usable Godot loop | Partial | Scoped authored/native journeys, headless session flow and Warrior/Sorcerer L1 admission pass. Rendered session usability and broader class/death/quest/service campaigns remain incomplete. |
| M2: Native world | Partial | Ordinary Cathedral L1-L4 generation/owners and several object families exist. Remaining dungeon families, quest-enabled generation and broader placement/population behavior remain open. |
| M3: Base/Hellfire campaigns | Partial | Many gameplay systems and native comparisons exist. Complete supported campaigns, final encounters, classes/difficulties and campaign-wide multiplayer retention are unproved or incomplete. |
| M4: Release client and operation | Partial | Client-pack and packaging foundations exist. Full graphical parity, audio/input/accessibility, clean-install coverage and measured release performance/soaks remain open. |
| M5: Legacy retirement | Partial | Authoritative execution is Lua-free. Production C++/SDL retirement and physical Lua removal still depend on replacement and compatibility closure. |

The [completion plan](completion-plan.md) defines dependencies and exit criteria.
Passing a scoped test or adding a schema version does not close a milestone.

## Material limits and next decision

- Nothing a person can launch shows the native world. The default client draws
  authored content with SVG stand-ins. Native Town/Cathedral routes run only as
  headless scripted journeys on profiles compiled by hand through several tools
  from extracted retail files; on 2026-10-23 the local Warrior Town profile was
  already rejected as stale by the current launcher. Phase 1 of the
  [recovery plan](work-plan.md) automates that setup and delivers a rendered,
  human-playable Town→L4 slice.
- Legacy native profiles admit canonical Rogue; class-bound profiles admit
  Warrior/Sorcerer in single-player Normal. Remaining classes, modes,
  difficulties and full campaigns stay open. See item 1.
- Native generation beyond Cathedral and quest/set-level campaign integration
  remain substantial gaps. See items 5, 7 and 8.
- Committed Catacombs work covers generation, population, actors, doors, items,
  chests, ranged release clocks and Arrow flight behind internal C2 owners; no
  playable Catacombs profile is active. The in-progress Magma/Acid caster chains
  are shelved. Phase 2 queues Catacombs as batches 2.1–2.6 in the work plan; since
  2.1 the default profile carries the L5–L8 inputs, and since 2.2a the server run
  descends to L8 and saves it (format 12, written as compact format 13). The headless
  L8 journey and fresh-host resume pass on the Debug server. Since P1 the cohort
  advances in memory and checkpoints every 30 s, on clean shutdown and when the
  last player leaves; format 1 cohorts are refused. Since P1b the L5 journey and
  resume also pass at 60 Hz on the Debug server. Since P1d first entry to a level
  costs one tick of about 120 ms (Cathedral) to 240 ms (Catacombs) on Release, and
  still about 0.5 s on Debug. Since P2 timed checkpoints write on their own thread, and a failed
  write is logged and retried. Since P5 a timed checkpoint holds the lock about 9 ms on the
  Debug L8 cohort, and validates and encodes on that thread too. Since P3–P4 Debug builds keep command logs that the host can replay and
  compare (client against server). Since P1c the clock is
  paced by the tick rate, stalls slow the game rather than jump it, late gameplay
  commands are accepted for up to 2 s, and the L5 journey and resume pass at 200 Hz
  on Debug and Release servers.
  Since 2.2b native multiplayer also runs L1–L8 with native multiplayer rules, and
  clvl 8+ players reach L5 by the Town warp; two headless clients meet on L5 and
  resume there. Since 2.2c single-player L5's warp-up opens the Catacombs Town warp
  (kept in the save); the headless journey opens L5's doors, takes both warps and resumes.
  Since 2.3 setup also imports the L2 tiles, specials and objects under `l2_1.pal`,
  and the client draws L5–L8 natively (headless journey checks every floor).
  Since 2.4a the L5–L8 melee families fight with native AI (per-tick native traces)
  and the headless Warrior journey kills on every floor to L8. Since 2.4b the ranged
  and missile families (archers, Acid, Magma, Familiar) do too, with native Acid,
  splat, puddle, magma ball, explosion and lightning missiles. Since 2.5a trapped doors
  and chests fire native Arrow, Firebolt, FireArrow and Lightning traps on L1–L8, retained
  across resume, and the journey opens a chest, picks up a drop and (when seeded) fires a door.
  Since 2.5b every non-Hellfire shrine runs its native effect and message on L1–L8 and stays
  used across resume. Since 2.5c fountains, goat shrines, cauldrons, bookcases, book stands,
  decapitated bodies, armor stands and weapon racks work natively and burning crosses burn.
  Since 2.5d trapped L1–L4 sarcophagi fire their linked wall trap after opening.
- Since 2.6a the local player and camera move smoothly between tiles with bounded
  prediction, known actions render through their interval, the command lead adapts
  to measured latency (minimum 50 ms), terrain drawing is retained per level and the
  client is capped at 60 FPS with VSync. `GODEVILUTION_CLIENT_FRAME_CAPTURE` records
  per-second frame costs. Snapshots carry only the receiving player's level and reuse
  unchanged geometry and idle bodies by reference. `tools/run-auto-capture.ps1` drives a
  windowed Town capture: in Release, standing costs about 6% of one core and walking
  about 12%. The user re-playtest is pending.
  See [Arrow verification](test-history.md#2026-10-20--c2-arrow-flight-and-impact).
- CI now regenerates 24 archive-free native oracle corpora and runs their
  managed comparisons (2.2b added 48 multiplayer Catacombs theme and chest cases;
  2.4a the ten-lane melee AI trace; 2.4b the seven-lane ranged AI trace; 2.5a door and chest traps; 2.5b vanilla shrines; 2.5c theme item objects and theme effects; 2.5d trapped sarcophagi). Retail-archive corpora stay local by design (public
  repository), so those comparisons still skip in CI.
- Since T1 the default client, server and Diablo suites run in about 14, 37 and 30 s;
  149 `Category=Slow` live-host, launcher and matrix tests run in a separate CI step.
- Legacy per-participant cold import can overwrite shared world/RNG. Explicit
  cohort mode has scoped recovery proof; normal cohort adoption is separate.
- L3 player death is a failed bot scenario, not an established transport/combat
  bug. Do not change native rules merely to make a bot pass.
- A per-cell Town roaming trial reached `TooLate` after about 640 ms waiting for
  the authority lock. Host admission latency remains unresolved.
- A command the host refuses as `TooLate` is dropped, not retried (pre-existing); a
  bounded retry with a fresh sequence is proposed.
- Optional original player, Town, resident and Cathedral object art pass scoped
  native comparisons; see [test history](test-history.md). Live animation phase,
  lighting, palette cycling, transparency and rendered comparisons remain open.

## Shelved work

Branch `wip/phase0-shelf` holds the uncommitted drafts from before the reset, one
commit per group: Catacombs Magma/Acid caster chains with the shared vision
line-of-sight change; native weapon-completion GTest fixtures; Godot Bard
Identify, Monk Search, Trap Disarm and Warrior Repair previews, experimental
four-floor scenarios and held-ACK relay support; and their documentation. None
was reviewed for merge. Recover individual pieces when a phase needs them.

Root clutter from earlier runs (`16`, `Microsoft/`, `diablo.ini`,
`single_0.hsv`, the prompt notes, logs and temporary files) was moved to
`.local/root-cleanup-2026-10-21/`.

## Working environment and authority

Use the existing `D:\Programming\Godevilution` tree. Preserve backward
compatibility, CRLF and SDK pins. Source Godot SDK is 4.7.1; the locally qualified
isolated engine/stage is 4.7.2. Use the [Godot](ai/godot-build.md) and
[MSVC](ai/msvc-build.md) guides; initialize matching VS tools and verify cl/rc/mt
before native builds. Serialize shared builds and run commands/write outputs
under `D:\Programming`.

The repository trees are `godot-client/` and `legacy-native/` after rename
commit `aea715e63`; native compatibility work remains active. The rename build
and its three pre-existing launcher-contract mismatches are recorded in
[test history](test-history.md#2026-10-18--repository-directory-rename).

Cleanup reclaimed about 17.2 GiB of old runs, exports, reports and generated
build output. `.managed-test` was removed; `.tmp` retains loose source/script/
patch files. `.devcontainer`, current profile/environment inputs, reusable
Godot export templates and unique experimental sources were retained.
The retained native caches are `build/x64-Debug` and
`build/x64-Debug-authoritative-vcpkg`. Reconfigure `build/x64-Debug` for the new
source paths before its next build; the authoritative-vcpkg cache already
passed the rename build. Historical local run paths may no longer exist; use
the current input entry points below and existing source-based verification.

Continue headlessly. No computer-use tools, GUI, visible applications or renewed
overnight schedule are requested. Do not reset, bulk commit, expose tokens or
change security settings. Push only on explicit user instruction. Ordinary source work, including Main.cs and
Main.Session.cs, is authorized; there is no Main edit hold or per-patch approval
requirement and no game-save privacy approval check.

The separately retained user-directed holds are limited to R7-r2 proof4ACC
source/metadata/hash reads, normal cohort/profile adoption, ordered weapon-break
implementation and inactive native Gold changes beyond the previously approved
correction. Do not broaden them to ordinary gameplay work. If a conflict arises,
identify the exact human instruction rather than creating a new restriction.

The [worker policy](completion-plan.md#worker-delegation) has Claude Opus 5.5 plan and
orchestrate two Sol 6.1/max implementers and one separate Sol 6.1/max independent
reviewer, run through the Codex CLI. Create the pool once and reuse it through the
thread ids in `.local/sol-pool.json`. No model substitution, replacement per package or
nested delegation. The orchestrator owns final verification and selective cohesive
commits.

## Useful entry points

The passed two-floor profile is `.local/nd2`, with
`tools/test-godot-native-progression.ps1 -ProfileDirectory .local/nd2` as its
existing reproduction entry. Do not replay unchanged evidence by default.
The four-floor experimental profile is `.local/nr16-f4b1`; its matching local
build/oracle environment is
`.local/native-cathedral-run-20261016/native-walk-environment.ps1`.
Use the matching profile, engine and guide when an affected verification is needed.

The class-bound profiles are `.local/native-classes-20261019/warrior` and
`.local/native-classes-20261019/sorcerer`; use
`tools/test-godot-native-classes.ps1 -ProfileDirectory <class profile>`.
The accepted pairs used an isolated staged host/client to exclude unrelated
vision/preview WIP. See test history for the retained class-scoped sessions.

Current reports are linked from test history. Keep historical outputs and old
file versions out of active documentation; Git is the archive. Technical rules
belong in the existing architecture, protocol, replay and build documents.

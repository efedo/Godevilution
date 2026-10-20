# Godevilution Working Status and Memory

As of 2026-10-20. Gameplay source baseline: `3af2a51cc`.
The existing working tree contains substantial uncommitted work; it is not a
clean committed-source baseline. Use `git status` and the actual diff before
assigning ownership or staging a batch.

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

- Legacy native profiles retain canonical Rogue admission. Separate class-bound
  profiles admit canonical Warrior/Sorcerer in single-player Normal; remaining
  classes, wider modes/difficulties and full campaigns stay open. See item 1.
- Native generation beyond Cathedral and generated quest/set-level campaign
  integration remain substantial implementation gaps. See items 5, 7 and 8.
- Legacy per-participant cold import can overwrite shared world/RNG. Explicit
  cohort mode has scoped recovery proof; normal cohort adoption is separate.
- L3 player death is a failed scenario, not an established transport/combat bug.
  Do not rerun it unchanged or change native rules merely to make a bot pass.
- Current source has broader backend coverage than application evidence. Keep
  implementation, native parity, headless play and rendered play distinct.
- A per-cell Town roaming trial reached `TooLate` after about 640 ms waiting for
  the authority lock. Host admission latency remains unresolved; ordinary
  destination-waypoint acceptance does not qualify load or repair that stall.
- The session lifecycle/setup batch is reviewed and verified for headless flow.
  Class-preview and experimental acceptance packages remain separate WIP.

The detailed inventory, documentation split and [near-term work plan](work-plan.md)
are prepared. Session flow and Warrior/Sorcerer L1 admission are complete for their
headless boundaries. Optional player/scene art, shared isometric input and the
explicit native Town profile/checkpoint stage pass their scoped comparisons and
both class journey/restore pairs. Missing or corrupt atlases still refuse native
activation before pointer projection changes. Existing profile bytes and save
schemas retain their contracts; normal cohort/profile adoption remains held.
See [Town verification](test-history.md#2026-10-19--explicit-native-town-integration).

The original Town resident visual family passes its scoped native comparisons,
whole-family failure probes and both class journey/restore pairs. See
[resident verification](test-history.md#2026-10-20--original-town-resident-art).
Exact NPC initialization RNG and persisted animation phase remain open.
The optional Cathedral L1 object family now imports eight original CEL sheets
for ten native IDs, preflights all textures and draws the current authoritative
frame. Native source/atlas/metadata/owner-offset comparisons pass; the ordinary
Warrior pair observes a chest 4→6 transition and incidental sarcophagus/barrel
art, retaining all 58 supported L1 object protobufs and collision geometry before
cold input with exact sequence 40→41. See
[object verification](test-history.md#2026-10-20--original-cathedral-object-art).
Other live object lifecycle art coverage and cold L1 redraw remain open, alongside
later-level TCHEST aliases, scene phases, native light/palette/transparency and
matched rendered comparisons. The active primary batch is native Catacombs L4/L5
entry/return and the cohesive L5–L8 family. Native generation/population
characterization comes first, including retained attempt seeds, distinct L2
doors/trapped chests, required monster behavior and L5 Town-warp unlock/rebuild.
Use an additive opt-in family profile; existing Cathedral/save contracts remain
unchanged. Normal-profile adoption remains held.
The Catacombs pure generator/materializer passes 75 focused managed cases,
including all 66 supported native entry comparisons and four existing ordinary
coarse/transparency goldens. Both 75-case native generation captures repeat
byte-identically, and both separate same-process guard regressions pass. Source
L2 SOL is 559 bytes; the native 1379-slot zero tail and opaque flags are retained.
The 32-case actual-class native population reference repeats byte-identically
across 35 artifacts, including retained unique history and actual Fat, Rhino,
Gargoyle, Magma and Acid actors. Starter-item identity seeds and pre-entry hero
coordinates are explicit prepared inputs; birth-clock seed parity and preceding-
floor gameplay are outside this proof. Distinct semantic metadata, L2 automap
and initial population phases pass 55 focused cases and all 32 exact staged
native comparisons. Complete fresh encounters/items/themes and level-ready state
now match all 32 actual native cases, including both classes and managed retained-
history chains. Separate controlled L5/L8 theme seams pass all 44 cases; 16 focused
contracts and 183 Cathedral compatibility cases pass, with six explicit fixtures
intentionally not selected. The earlier metadata compatibility proof retains its
124 passes and unavailable symbolic-link case under sandbox privileges.
Distinct C2 actor admission preserves generated active order/reservations,
ranged-skeleton emergence records and separate frozen Gargoyle phases.
Seventy-six focused cases and all 32 generated admission comparisons pass.
Controlled native actor/pack and L2 door operation traces now cover Special
clocks, wake order, blocked close and sync geometry.
Existing cohort formats refuse C2 fields; no playable Catacombs profile is active.
Ranged timing/controller retention, TCHEST operations, per-floor ownership,
profile/travel integration and detached cold restore remain required family gates.
See [verification](test-history.md#2026-10-20--catacombs-actor-admission-and-l2-door-operations).
Keep the queue, this status and affected inventory entries synchronized. Do not
restart inventory construction or an unrelated test campaign.

## Existing working-tree packages

Inspect current diffs; this grouping is working memory, not staging authority.

- Class-control preview acceptance partials, launcher wrappers/shared script
  hunks and associated tests remain uncommitted. Session lifecycle, menu/HUD
  integration and profile-aware setup are now a separate reviewed batch.
- Experimental four-floor acceptance: entry continuation, Town itinerary v2,
  L3 encounter/held-pair and ground-input diagnostics, plus wrappers. These are
  separate experimental scenarios, not mandatory production dependencies.
- ACK-loss/retry changes in protocol-client tests and the acceptance relay.
- Native player-command fixtures, `CMake/Tests.cmake`,
  `AuthoritativeWorld.cs` and documentation changes.

Preserve unrelated untracked files, including `16`, `Microsoft/`, `diablo.ini`
and `single_0.hsv`. Do not bulk stage or include them in a documentation commit.

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

The [worker policy](completion-plan.md#worker-delegation) specifies two GPT-6
Luna/xhigh implementers and one GPT-6.1 Sol/medium independent reviewer. Create
one reusable pool in a clean session; do not assume old chat handles exist there.
No model substitution, replacement per package or nested delegation. Root owns
integration, necessary verification and selective cohesive commits.

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

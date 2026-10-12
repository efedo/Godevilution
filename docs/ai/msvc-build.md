# MSVC Build Notes for Agents

## Native loaded quest pools

Build the existing registered `quests_test` target using the matching MSVC x64
environment below. From that configured build directory, run:

```powershell
.\quests_test.exe --gtest_filter=QuestTest.LoadedPoolsPreserveCanonicalTsvOrder:QuestTest.LoadedPoolsMatchLiteralSeededSelection
.\quests_test.exe
.\quests_test.exe --gtest_shuffle --gtest_random_seed=417 --gtest_repeat=3
```

Require focused 2/2, integrated 4/4 and three shuffled four-test iterations,
without skips, plus frozen source hashes and bounded owned exits. The fixture
validates the complete loose build TSV before `LoadQuestPools`, then makes its
directory the sole asset override so it wins before archive lookup. It asserts
six pool slots, row order, eight literal seed exclusions, all 24 quest flags
and unchanged global RNG. Full quest/pool/override/assets-path/RNG state is
restored; the two existing empty-table fallback tests remain order-independent.
Loaded special seed 988045466 skips pool 1, while the fallback skips its second
hardcoded group. Canonical test DLL data annotations expose only the required
globals. This is native characterization; managed policy, save/ruleset
compatibility, world reconstruction and rendered acceptance remain separate.

## Prepared native Monk Search prerequisite

Build the existing registered `missiles_test` target using the checked MSVC x64
environment below. From that configured build directory, run:

```powershell
.\missiles_test.exe --gtest_filter=MissileData.ScopedTestLoadRestoresOriginalStorage:NativeMonkSearchTest.*
.\missiles_test.exe
.\missiles_test.exe --gtest_shuffle --gtest_random_seed=417 --gtest_repeat=3
```

Require focused 5/5, integrated 10/10 and three shuffled ten-test iterations,
without skips, plus frozen critical inputs and bounded owned exits. Loose
checked-in Hellfire tables feed actual `CastSpell`/`AddMissile`/`ProcessMissiles`.
At native default level one/effective spell level zero, Search starts at 247,
becomes 246 on the first processing pass, remains active at one and disappears
on the 247th pass. Recast extends the original missile; the strict signed-int
extension boundary discards the duplicate without overflow. Automap item
visibility toggles and unchanged Life/Mana/RNG are asserted.

A `BUILD_TESTING`-only metadata guard swaps and restores all four missile data
vectors, preserving their original storage and owned graphics. The fixture
restores player/spell/missile storage, asset path, tile/level, managers, audio
and RNG state; nested loads test metadata storage restoration. Callback pointers
are checked non-null and behaviorally because DLL import thunks make EXE/DLL
address equality unreliable. Fresh test processes do not load MPQs/overrides.
The added caller case loads all canonical class/Experience tables, checks the
Monk loadout and animation fields, then executes actual
`TestPlayerCheckNewPath` -> `StartSpell` -> `ProcessPlayers`/`DoSpell`.
The real effective-level-zero cast frame creates duration 247 before the paired
missile pass decrements it to 246. Its finite bound derives from loaded animation
frames/ticks; view position and delayed-sound globals are restored. Only those two
audio DATA declarations gain test-only DLL visibility, without runtime changes.
Canonical player tables remain loaded for the process under existing table-test
conventions; the fixture does not claim to restore their previous private storage.
Freeze all 31 class TSVs, Experience and the player/animation/audio dependencies
alongside original Search inputs. This prepared caller sequence does not run full
GameLogic, CheckPlrSpell/Town admission, native character creation, shared server
tick alignment or rendering.

## Prepared native Recharge edge prerequisites

Build the existing registered `items_test` target using the checked MSVC x64
environment below. From that configured build directory, run:

```powershell
.\items_test.exe --gtest_filter=ItemsTest.SorcererStaffRechargeFullyChargedStaffIsNoOpWithoutRandomDraw:ItemsTest.SorcererStaffRechargeCanExhaustItsLastMaximumChargeWithOneRandomDraw:ItemsTest.RechargeItemRejectsNonStaffAndNullSpellWithoutChangingItemOrRandom
.\items_test.exe
.\items_test.exe --gtest_shuffle --gtest_random_seed=417 --gtest_repeat=3
```

Require focused 3/3, integrated 10/10 and three shuffled ten-test iterations,
without skips, plus frozen critical inputs and bounded owned exits. Actual
`RechargeItem` leaves fully charged staff untouched without an RNG draw; a staff
with zero charges/one maximum charge loses that final capacity with exactly one
LCG transition. Nonstaff and null-spell inputs preserve the complete item's
semantic fields and RNG. The existing ordinary Recharge, Warrior Repair and
Bard Identify facts remain in the full target.

These cases use a prepared level-ten stack-local player distinct from MyPlayer;
the helper is class-independent. RNG is restored even on fatal assertions.
They do not exercise Sorcerer class admission, `DoRecharge`/inventory recalculation,
cursor/network behavior, native character creation or rendered acceptance.

## Prepared native Bard Identify prerequisite

Build the existing registered `items_test` target using the checked MSVC x64
environment below. From that configured build directory, run:

```powershell
.\items_test.exe --gtest_filter=ItemsTest.BardIdentifyChangesOnlyTheEquippedItemsIdentificationState
.\items_test.exe
.\items_test.exe --gtest_shuffle --gtest_random_seed=417 --gtest_repeat=3
```

Require focused 1/1 and the complete current target (now 10/10, including Recharge
edges), plus three shuffled full iterations without skips, bounded owned exits
and frozen source hashes. The earlier Bard-only qualification was 1/7/21. The
MPQ-free fixture invokes actual `CheckIdentify`/`CalcPlrInv` on a prepared
table-backed ordinary sword of a genuine nonlocal, inactive-level Bard. Only
identification changes; all item semantic/animation fields, collections,
inventory grid/count, attributes, derived values, resources and RNG are checked.
Scoped player storage/pointers/ID, lighting/vision, options, RNG and the active
headless Health/Mana redraw flags are restored. Other registered backbuffers,
native character creation, cursor/graphics, magical affixes, local stash/Gold
bookkeeping and full class/rendered parity remain outside this prerequisite.

## Native Trap Disarm branch oracle

Build the existing registered `objects_test` target using the matching checked
MSVC environment described below. From the configured build directory, run:

```powershell
.\objects_test.exe --gtest_filter=TryDisarmTest.*
.\objects_test.exe
.\objects_test.exe --gtest_shuffle --gtest_random_seed=417 --gtest_repeat=3
```

The six MPQ-free fixtures invoke actual `ACTION_DISARM` through the existing
`TestPlayerCheckNewPath` bridge. Nine armed attempts each consume exactly one
native LCG draw; an unarmed attempt consumes none. Literal expectations cover
Dexterity/depth thresholds, equality, values above 100, the signed negative RNG
edge, both linked wall-trap orientations, unrelated targets, unlinked doors,
trapped-chest fallback and ordinary chests. The existing predicate tests cover
all three trapped-chest identities.

Scoped empty object behaviors disable the subsequent `OperateObject` effect.
The fixture preserves original metadata/player vector storage, player pointers,
the complete object pool, tile references, level ID, RNG and light manager.
A genuine nonlocal player avoids cursor/view changes while exercising the
caller's position/vision bookkeeping. The pool's `DVL_API_FOR_TEST` annotation
uses the existing test-only DLL import/export policy. No production behavior,
normal profile, private save or held player/CMake draft is changed.

Qualification requires all six focused tests, the full 22-test object suite
and three complete shuffled iterations (66 executions), without skips, plus
clean owned exits and frozen source hashes. This characterizes prepared native
disarm behavior; managed probability/trap-graph parity, subsequent operation
effects, generation, projectile suppression and rendered acceptance remain open.

## Sprite-free animation timing proof

The additive registered `player_command_phase_test` characterizes the actual
native attack/ranged handler completion signal passed to `CheckNewPath`.
Build it with the matching checked MSVC environment as described below, then
run its four `NativePlayerCommandPhase` fixtures. Thirty-two MPQ-free headless
cases check literal melee/Diablo-ranged/Hellfire-ranged ready/completion skips,
pending/repeated cursor fields, one subsequent animation step and unchanged RNG.
The `BUILD_TESTING`-only wrapper calls the existing native handlers; it does not
simulate their return values or modify production behavior. Pair this with the
existing Engine `OrderedSimulationTickTests` and module
`DiabloSimulationTickCommandTests` after rebuilding their xUnit executables.
This proves ordinary callers, not whole `ProcessPlayers`, weapon-break completion,
typed input phases, live cursor restore/protocol or rendered client acceptance.

Seven additional `NativePlayerActionStart` fixtures cover the three walking
families, town running, a walk admitted by actual attack completion, idle Spell
and ready Spell repetition. Run `NativePlayerActionStart.*` (7 tests), then
`NativePlayerActionStart.*:NativePlayerCommandPhase.*` (11 tests), followed by
that combined filter with `--gtest_shuffle --gtest_random_seed=417`, 418 and 419.
The prepared 8-frame/5-action-frame fixture asserts all nine timing fields
immediately after admission and after two direct `processAnimation` calls:

| Admission | Current frames: start / step 1 / step 2 | Tick modifier | Prior skipped frames |
| --- | --- | --- | --- |
| Idle walk | -2 / -1 / 0 | 113 | 0 |
| Town run | 2 / 3 / 4 | 204 | 0 |
| Completion walk | -1 / 0 / 1 | 128 | 0 |
| Idle Spell | 0 / 1 / 2 | 170 | 0 |
| Ready Spell repeat | 0 / 1 / 2 | 256 | 2 |

All rows use 8 frames, one tick per frame, counter 0 and no petrification;
sequence ticks are -128/0/128, with 8 relevant walk frames or 4 Spell frames.
These direct animation steps isolate the end-update boundary; they do not run
the whole `ProcessPlayers` loop or a Spell effect. The module's three added
`DiabloSimulationTickCommandTests` use actual Engine callbacks and shipped
Warrior walking/casting/action-frame values 8/20/14. Their 39-case class covers
participant order, Move completion before Cast, ready Cast replacement and
second same-tick admission refusal. The differing metadata does not establish
native/managed tick parity; refusal is not transport duplicate-ledger coverage.

The registered `missiles_test` includes five full-player-loop fixtures. Run
`NativeMonkSearchTest.ProcessPlayersCompletesUnarmedAttackAndImmediatelyStartsNorthWalk`
(1 test), then `NativeMonkSearchTest.*:MissileData.ScopedTestLoadRestoresOriginalStorage`
(10 tests), with three shuffled combined runs at seeds 417/418/419. Logical label
100 is the first complete `ProcessPlayers` admission update; the fixtures do not
change a production tick counter.

Prepared Search timing 8/5 yields post-update Spell frames 1 through 7 at labels
100 through 106, actual Search creation at 105 and Stand at 107. Completion
resets the nine timing scalars to `8/4/1/0/false/0/0/0/128`. Duration is 247
before a separate missile pass after completion and 246 after it; missiles are
not processed on every intervening update. The earlier walking fixture checks
two updates, frames -1/0, occupancy and view prestate with a 3x3 scoped guard.
Canonical class tables remain loaded under the existing test convention; the
old canonical Search caller case remains unchanged.

The complete North-walk fixture follows labels 100 through 109. Prepared
walking frames 8 yield post-update frames -1 through 7 at 100 through 108;
arrival and Stand occur at 109. It checks all nine animation fields, occupancy,
position and view. A separate 5x5 scoped tile guard covers the destination's
empty pickup ring and restores full tiles and piece-zero properties before
base teardown, which restores the light/vision manager and view.

The prepared Search re-admission fixture adds thirteen post-update rows through
label 112. First creation and successor admission share label 105; the successor
uses repeated carry 2/modifier 256 instead of initial carry 0/modifier 170.
Second creation at 110 extends the active duration 247 to 494 and flags the new
entry for deletion. Stand is observed at 112, followed by one separate terminal
missile pass that removes the duplicate and decrements duration to 493.

The managed `DiabloSimulationTickCommandTests` class has 49 cases. Its 8/5 Cast
trace captures 32 observations, including eight actual post-`ExecuteTick` rows,
with one damage/mana effect at 105 and Stand at 107. The North-Move trace reads
ten post-return states at 100 through 109. Authored walking frames 8 yield
effect/ready/end 108: origin persists through 107, then position changes and
the action clears to Stand at 108; tick 109 remains Stand. Native Monk and
managed Warrior fixtures use distinct classes, positions and Cast effects.
The managed re-admission trace captures thirteen post-return rows and callback
state before/after replacement at 105. The first effect is already applied
before that callback; the successor applies at 110 and completes to Stand at
112, with exactly two damage events and mana 10/6/2. Original end 107 is stored
metadata that becomes counterfactual after replacement; actual 107 stays Spell.
The unarmed attack-completion fixture follows nine actual updates at 100-108:
its queued North walk receives a same-call `DoWalk` update, giving frames 0-7
and modifier 128 before Stand/arrival at 108. Its terminal attack frame follows
the action frame, with empty hands and no speed or elemental flags. The existing
5x5 guard and base teardown restore tiles, lighting and view.

The internal `DiabloCommandCallbackPhase` is transient and actor-reference-matched
under native timing. Added tests cover stable phase identity through rejection
and replacement, opt-out/outside/wrong-actor absence and exception/null-event
cleanup. Run the tick-command class and the seven selected lifecycle/cohort
regressions recorded in migration status. Persisted anchor and cursor adoption
remain open; prepared traces do not establish full world-tick parity.

The local checked MSVC build hit C1902 under `/ZI /FS` even after preserving and
removing generated symbol artifacts from the selected target. A fresh compiler
probe passed, and child-process `_CL_=/Z7` allowed the target to build and pass
the selected runs. Apply that override only after `VsDevCmd.bat` in the child
environment; keep CMake defaults unchanged. MSVC reports the expected D9025
debug-format override warning.

For the separate player-context metadata prerequisite, run:

```powershell
.\tools\test-player-animation-context.ps1
```

This zero-argument bounded command initializes/checks the matching MSVC x64
environment and builds the registered `player_animation_context_test`. Four
native fixtures and an independent capture repeat must pass, followed by the
exact existing-module xUnit v3 context family, including the explicit comparison.
Both bounded regular artifacts must be byte-identical; reports, stage exits,
logs, cleanup and reparse-point ancestors are checked. No retry follows failure.
The prior opt-in environment value and working directory are restored.

The native matrix contains 648 prepared class/weapon/armor context sequences,
1,944 town/dungeon transitions, all nine cached fields and all nine native timing
pairs per transition. Cold zeroes and signed town-retained values remain inputs;
managed continuation never receives native expected state. Actual spell dispatch
compares all 37 base and 52 Hellfire rows, including Null. The strict reader pins
the pre-input-only fingerprint. Native cursor/RNG remain unchanged. Public spell,
RNG, path and level settings are restored; hidden class caches are owned by the
dedicated test process. Configured bundle assets are respected with a source
text-resource fallback; no archives, server, client or Godot are opened.

Evidence remains under `%TEMP%/Godevilution-Player-Animation-Context-*`. This is
prepared effective graphics and metadata application, not actual equipment
admission, native initialization/death/RNG callers, command phases, cursor
persistence/protocol, asset availability or rendered Godot acceptance. Production
profiles, legacy save bytes/hashes and the server MPQ boundary are unchanged.

```powershell
.\tools\test-animation-timing.ps1
```

This zero-argument command initializes the matching MSVC x64 environment,
verifies `cl.exe`/`rc.exe`/`mt.exe`, and builds the existing registered
`animationinfo_test` target. It runs all eight AnimationInfo fixtures and repeats
the scalar capture independently, then builds/runs the full existing Domain
xUnit v3 project through its in-process runner, including the explicit comparison.
It requires no MPQs and launches no server, client or Godot.

The 50 input-bound traces cover 1,369 transitions. Every transition compares all
nine cursor scalars and all 129 input fractions for render-frame and raw native
progress queries. Assertion/division/overflow-invalid native progress queries
are recorded as refusals and never executed; the managed query must reject them.
Defined negative native progress intermediates retain their byte conversion.
This is scoped scalar qualification, not player-controller, initialization RNG,
gameplay-effect timing, protocol/persistence or Godot acceptance.

Exact completed reports, portable/evidence-guard families, two byte-identical
bounded regular artifacts, clean owned exits and retained logs are required.
Stages have deadlines, inherited child-only dialog suppression and bounded
cleanup attempts; these are not universal process-tree/dialog containment.
No automatic retry follows a failure. Evidence remains in the printed private
`%TEMP%/Godevilution-Animation-Timing-Oracle-*` directory; the environment and
working directory are restored. Existing source/configuration/saves are not
rewritten by the command.

The supplemental monster-runtime proof takes no arguments:

```powershell
.\tools\test-cathedral-monster-runtime.ps1
```

It initializes the matching MSVC environment, verifies `cl.exe`/`rc.exe`/`mt.exe`,
builds the registered oracle target, captures the complete 138-row runtime table
and 720 actual native monster-method cases twice, then runs the explicit managed
comparison. Reports, hashes, stage exits and cleanup evidence stay in a fresh
private `%TEMP%/Godevilution-Monster-Runtime-Oracle-*` directory. Excluded,
replaced or incomplete reports and non-identical captures fail the proof.
It needs no MPQ files and launches no server, client or Godot. This proves raw
metadata/calculation parity, not whole-world activation, AI or death-time loot.
The nineteen-table generation pack and earlier corpus contracts remain unchanged.

For the separate detached death-time loot prerequisite, run:

```powershell
.\tools\test-cathedral-monster-loot.ps1
```

This zero-argument bounded runner checks the matching MSVC tools and builds the
registered `cathedral_monster_loot_test`. It captures 68 cases twice (17 lanes
across depths 1-4), requires byte-identical complete corpora, then builds/runs
four exact xUnit v3 depth comparisons. The BUILD_TESTING bridge invokes actual
SpawnLoot after an explicit monster-item reseed, not full MonsterDeath. No MPQs,
server, client or Godot are opened. Evidence stays private under
`%TEMP%/Godevilution-Monster-Loot-Oracle-*`; failures stop without automatic retry,
retain diagnostics and restore the opt-in environment variable. All owned stage
exits, logs, bounded cleanup and exact report/artifact inventories are required.

Scope is original Diablo Cathedral, quest-disabled SP Normal. Full item128/
allocator127/history128 state, signed refs, zero seeds, minion/Berserk, weighted/
gold selectors before capacity, placement exhaustion and actual paired fresh/
exhausted unique history are compared. Only native pre-call metadata/state are
managed inputs. Run the portable loot/metadata and launcher guard families
separately, plus `test-theme-item-placement.ps1` for unchanged old-world behavior.
This is not live death handling, multiplayer, quest/Hellfire, populated-profile
or cold-restore acceptance; default/save/schema identities are unchanged.

Use the Visual Studio developer environment. Plain PowerShell may find `cl.exe`
or CMake but still miss standard MSVC headers such as `cstdint`, `array`, or
`Windows.h`.

The current working command prefix is:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && <command>'
```

Before configuring, building, or investigating native compilation failures,
verify the compiler and Windows SDK tools in that same developer environment:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && where cl.exe && where rc.exe && where mt.exe'
```

Use focused targets for a logical change and broader affected suites at the
integration gates in [the completion plan](../completion-plan.md#verification-policy).
Neither test discovery counts nor old build logs establish a current pass.

The Visual Studio bundled CMake used by this workspace is:

```bat
"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
```

Configure `x64-Debug` from a clean tree:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" -S . -B build\x64-Debug -G Ninja -DCMAKE_BUILD_TYPE=Debug -DBUILD_TESTING=ON'
```

Build the game:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" --build build\x64-Debug --target devilutionx'
```

Build a focused test target:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" --build build\x64-Debug --target debug_console_history_test && build\x64-Debug\debug_console_history_test.exe'
```

Run CTest after test binaries are built:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\ctest.exe" --test-dir build\x64-Debug --output-on-failure'
```

Avoid using `--target all` as a shortcut for the test suite unless benchmarks are
also intended; this target builds benchmark executables too. To build only the
registered GTest executables, use the target names listed in `CMake/Tests.cmake`
under `tests` and `standalone_tests`.

If CMake regeneration reports dubious repository ownership, use repository-local
safe-directory flags where possible:

```bat
git -c safe.directory=D:/Programming/Godevilution status --short
```

If vcpkg reports permission errors under `%LOCALAPPDATA%\vcpkg`, the command
must run with permission to access the user vcpkg registry cache.

To build the authoritative native client slice, configure a separate build
directory with the server-backed feature enabled. Keep the default build
unchanged because that feature requires Protobuf:

```bat
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 && "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" -S . -B build\x64-Debug-authoritative -G Ninja -DCMAKE_BUILD_TYPE=Debug -DBUILD_TESTING=ON -DDEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT=ON -DCMAKE_TOOLCHAIN_FILE="C:\Program Files\Microsoft Visual Studio\18\Community\VC\vcpkg\scripts\buildsystems\vcpkg.cmake"'
```

The server-backed option activates the `authoritative-client` vcpkg manifest
feature. A fresh configure can therefore take several minutes while vcpkg
builds Protobuf and its dependencies. Keep the toolchain argument present;
without manifest mode CMake cannot resolve the required Protobuf package on the
verified Windows setup.

The authoritative client expects the server's content and ruleset hashes.
The production launcher selects protocol `0.2.0` and passes the printed hashes
independently.
On Windows, `run-authoritative-client.ps1` starts a local server on a
free port, reads those hashes, persists a resume token, and launches the
client with matching arguments. A handshake identity mismatch writes a
timestamped diagnostic report to `authoritative-diagnostics` (or the
directory supplied with `--authoritative-diagnostics`). The report includes
the endpoint, working directory, expected identities, and server identities.
The root launcher takes no arguments:

```powershell
run-authoritative-client.ps1
```

The root launcher selects only a CMake directory configured with
`DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT=ON`, initializes the Visual Studio
developer environment, and incrementally rebuilds both native client and server
on every run. It also verifies the server's reported single-player mode and
clock-gated scheduling before
opening the client, so stale binaries and mode mismatches fail before native
level setup.

The root launcher pairs base plus the separate `native-start` entry pack with
explicit `--starting-level 0` and `--diablo`, even
when Hellfire is installed. A Hellfire acceptance run needs matching server
content; native installation discovery must not silently select another game.
Town entry `(75,68)` is validated module content, not a client teleport or
server MPQ lookup. Standalone/base/Godot defaults and base pack bytes are
unchanged; the root launcher rebuilds the module required by the optional table.
Only diagnostic runs fix seed 0; normal native seed selection is unchanged.
The paired native runtime advertises clock-gated support. Default low-level
native configurations and standalone host timing retain Immediate compatibility.
Within a gated connection, delayed/equal snapshots retain the native projected
tick and fractional elapsed age; only strictly ahead samples reanchor it.
Whole-millisecond truncation, floor projection, saturating arithmetic and the
negotiated rate are preserved; reconnect clears the old epoch. Immediate raw-tick
observation and explicit/frozen commands remain unchanged. Nine deterministic
clock checks share the existing `server_backed_client_test` target; broader
registered transport/presentation checks are recorded in migration status.
CTest runs each named fixture in a fresh process, so native presentation fixtures
must initialize their required player tables themselves. Neither estimator
tests nor a successful game build prove accepted native GUI movement/restore.
The feature-enabled CMake branch also defines the feature on separately compiled
gameplay object libraries, with protocol-generation ordering and dependencies;
enabling it only on the main library does not enable those existing guards.
The native transport remains synchronous: deliberate future-command ACK waits
are expected, with a default two-second absolute gated exchange deadline.
This bound is not an asynchronous rendering/responsiveness or release-budget pass.
Normal launches retain the user's native save/config locations. The server is
hidden; the client is interactive. Native logs and owned process IDs/start times
are retained beside the server logs. Cleanup owns only those process handles;
forcibly killing the PowerShell launcher itself can bypass its `finally` block.

For a reproducible, isolated manual native GUI check, run from an interactive
PowerShell session:

```powershell
./tools/run-native-acceptance.ps1
```

The wrapper defaults to a zero-argument fresh run using the same rebuild/handshake
launcher, with fresh
native save/config folders, windowed presentation, skipped intro, and a separate
server save root. Select Single Player, create a new hero, enter the world,
observe the authoritative state, exercise movement, and exit normally. Record
what actually happened; window discovery or exit code alone is not a GUI pass.
Logs remain under the printed `Godevilution-Native-Acceptance-*` temp directory.
Keep the resume-token file private. To reuse only an explicitly marked prior
test profile with a fresh host/client and separate logs, use the diagnostic
wrapper's resume option (the root launcher still takes no arguments):

```powershell
./tools/run-native-acceptance.ps1 -SessionDirectory 'C:\path\Godevilution-Native-Acceptance-profile' -Resume
```

Resume requires the matching version-2 profile marker with
`ContentProfile=godevilution.native-start@0.1.0`, private bookmark,
nonempty authoritative checkpoint and native save/config folders. The bookmark
must match the native single-binary-line format; empty first lines, CRLF and
extra tokens are rejected. Reparse-point ancestors are rejected, and an
exclusive profile lock is held through builds and owned-child cleanup. Validation
does not rewrite old files; resume logs get a unique `resume-*` subdirectory.
Select the existing test hero, inspect the restored authoritative state and
verify a newly accepted movement. Merely opening that hero is not a resume pass.
Terminal client outcomes are retained even on forced owned-child cleanup.

The wrapper restores both internal `GODEVILUTION_NATIVE_ACCEPTANCE_DIRECTORY`
and `GODEVILUTION_NATIVE_ACCEPTANCE_RESUME` settings. Fresh runs reject existing
target directories before any build or launch; resume never guesses a latest
profile or uses the user's normal saves. This does not establish normal-launch
server persistence or certify the test profile's gameplay state.
Earlier version-1 diagnostic profiles remain private and are rejected, not
migrated or relocated into the new content identity. Negotiated host identities
and checkpoint integrity remain separate strict checks.

On the verified desktop, a sandbox-launched client did not expose a targetable
window, while the same rebuilt client launched on the interactive desktop did.
Agents must request the appropriate launch permission and Computer Use app
approval; they must not treat a running process as visible UI or bypass denied
window access. See migration status for current GUI outcomes and the remaining
native gameplay/timing/presentation gap.

Pre-adaptation Immediate-mode GUI evidence reaches healthy town entry and fresh-host
restoration at `(75,68)`, with full-life HUD/state and normal menu exits. Recorded
movement clicks reject `TooLate`; player rendering is not established by the town
captures, so native create/move/save/resume acceptance remains open. The earlier
dead-origin Cathedral probe is retained; its missing-art failure remains
unresolved. Diagnostics identify the exact object/template without
relaxing graphics/frame admission or adding a speculative reload. Base entry
coordinates now have the optional validated contract. Timing/presentation source
review and affected native tests pass; accepted movement and moved-position GUI
restore remain required. See migration status for the latest verification.
Inspect legacy checkpoints only after confirmed owned-publisher exit or a verified
publication barrier. Delete sharing alone does not permit the Windows
`File.Move(overwrite:true)` publisher to replace a held canonical reader. The
qualified cohort POSIX-rename reader contract is separate. The retained earlier
access-denied phase and passing retry do not establish the historical lock owner
or a universal sharing-safe live-inspection contract.
The latest fresh GUI run was stopped behind a test-executable firewall prompt;
the affected TCP test listeners are now loopback-only and their rerun passes. Never
automate security prompts or enable public/private network permissions for the
loopback test. If a prompt appears, ask the user to cancel it before continuing;
do not count focused tests or world rendering as GUI gameplay acceptance.
Authoritative connection failures are retained in `native.log` and show an error
dialog that returns to the menu. A normal exit code alone remains insufficient.
Controlled Direct3D captures stayed black, while a child-process launch with
`SDL_RENDER_DRIVER=software` displayed the menus. Use that environment override
only for a bounded diagnostic and restore the caller's prior value afterward;
normal renderer defaults and the release-platform matrix are not certified.

## Optional Cathedral fine-geometry oracle

From the repository root, run the zero-argument Windows wrapper:

```powershell
./tools/test-cathedral-geometry.ps1
```

It selects an existing source-matching Debug MSVC tree with `BUILD_TESTING=ON`
and `UNPACKED_MPQS=OFF`, initializes its matching developer environment and
rebuilds the native `level_l1_test` and managed game-module tests. Native archive
discovery uses the existing GOG-aware path and requires TIL/SOL resolution from
the main Diablo archive, not spawn or higher-priority loose/mod assets.

One native GTest captures seeds 2588/743271966/0/1/0x80000000/0xFFFFFFFF with MAIN/PREV entry; twelve explicit
managed cases compare complete coarse/fine layers, RNG/entry/stairs, named SOL
flags and collision/light cells. Raw SOL is zero-extended only in memory and
its unnamed `0x40` bit is checked separately before semantic comparison.
Strict production SOL validation is unchanged; this is not a server importer.
The shared conservative vision projection is not exact native vision parity.

The wrapper requires the exact native result and twelve managed passes without
skips, hashes fourteen nonempty artifacts and retains logs plus `result.json` in a
fresh private `%TEMP%/Godevilution-Cathedral-Oracle-*` directory. Licensed raw
inputs and snapshots must never be committed or published. Without the opt-in
directory the new native test skips, and the twelve managed comparisons are
explicit; neither default-suite exclusion counts as original-asset evidence.
Unpacked-MPQ builds are not accepted by this original-archive wrapper.

To compare ordinary Cathedral dungeon depths 1-4 through the shared native L1
family, select the separate depth oracle explicitly:

```powershell
./tools/test-cathedral-geometry.ps1 -CathedralDepths
```

This path resets the selected depth seed, makes every quest unavailable and
selects original-Diablo stairs before each native capture. It requires 48 managed
comparisons (four depths, the same six seeds and MAIN/PREV) plus one completed
native capture test, with no skips. The separate depth format records 80x80
coarse transparency in addition to complete fine layers, RNG and stair/entry
metadata. Fifty nonempty private artifacts are hashed. The original zero-argument
level-1 format, twelve comparisons and fourteen artifacts remain unchanged.
The managed depth API consumes validated TIL/SOL catalogs, never MPQs. These
checks do not activate a multi-depth runtime profile or establish native
population, quest variants, campaign seed selection or complete Cathedral play.

To characterize native Cathedral door placement and mutation separately, use:

```powershell
./tools/test-cathedral-geometry.ps1 -CathedralDoors
```

This native-only mode is mutually exclusive with `-CathedralDepths`. Two GTests
capture ordinary depths 1-4, seeds 2588/743271966 and MAIN/PREV entry, plus one
depth-2 delta-open case. They call the native `AddL1Objs` scanner and public
sync/delta door operations, requiring real CLOSED/OPEN/CLOSED object transitions.
Every case retains complete 112x112 closed layers, changed open/reclosed cells,
native object fields, RNG observations and hashes. Non-restoring closes are
recorded; a fresh generated/scanned baseline isolates the next door when needed.
The test-local pool reset and scope cleanup match native legacy index ownership.

The wrapper requires both completed fixtures, validates the separate door schema
and compares all twenty mandatory artifacts by byte length and SHA-256 across
two fresh private captures before reporting success. Typed native integers,
booleans, transition/baseline consistency and the exact artifact set are checked;
skips, floating numeric evidence and hidden extra files are rejected. Each capture
retains raw
`l1.til`/`l1.sol`, metadata, sixteen ordinary cases and the delta case. Logs,
reports, hashes and `result.json` stay under the printed private oracle directory;
licensed inputs must not be committed or published. Direct fixtures require
`GODEVILUTION_CATHEDRAL_DOOR_ORACLE_DIRECTORY` and otherwise skip.

This wrapper remains native characterization; run the managed comparisons
separately against its private `first-capture/artifacts` directory after building
the game-module test project:

```powershell
$env:GODEVILUTION_CATHEDRAL_DOOR_ORACLE_DIRECTORY = 'C:\path\Godevilution-Cathedral-Door-Oracle-profile\first-capture\artifacts'
./server/tests/Devilution.Diablo.Tests/bin/Debug/net8.0/Devilution.Diablo.Tests.exe --filter-class '*AuthoritativeCathedralNativeDoorOracleTests' --explicit only --fail-skips on
```

Scope and restore the environment variable in an automated caller. All seventeen
explicit cases must pass without skips. They compare complete geometry and
native door fields/operations, not full population or physical-light parity.
The validated server API consumes catalogs, never MPQs. A default-suite explicit
exclusion is not private-asset evidence and these checks do not activate a
door-enabled playable profile.

Scanner RNG includes native L1 lights but is not production `InitObjects` or
encounter-placement RNG; physical lighting/vision state is not captured. It
does not establish that doors explain disconnected routes. Existing level-1
and depth oracle modes and server/client runtime behavior remain unchanged.

To characterize the complete native `InitObjects` phase separately, use:

```powershell
./tools/test-cathedral-geometry.ps1 -CathedralPopulation
```

This isolated mode must be selected alone. It uses the dedicated
`cathedral_population_test` target, ordinary original-Diablo depths 1-4,
seeds 2588/743271966 and MAIN/PREV entry. Real stairs, reservations, monster
definitions and theme selection precede the independently reset population
stream, then native `HoldThemeRooms`, `InitGolems` and `InitObjects` run in order.
The separate story-book seed and fixed single-player Normal-difficulty inputs
are recorded; explicit seeds do not establish campaign seed selection.
The headless fixture disables audio and calls the actual level initializer
through a `BUILD_TESTING`-only forwarder; normal gameplay is unchanged.

The runner requires a completed native fixture, complete typed artifacts and
byte-identical independent captures. Logs and derived original-asset evidence
remain in a fresh private temporary directory. Failed or timed-out stages stop
the run before another capture. The opt-in environment is restored afterward;
default geometry/depth/door modes and user saves remain unchanged.

This is a reference for the initial object phase, not managed population parity,
save-byte equivalence, final monster placement or the later `CreateThemeRooms`
phase. It neither activates a populated server profile nor adds server MPQ
access. Keep the entire printed evidence directory private.

The original mode preserves its version-1 reference: it does not load the
level-generation table, so trap percentages use the native zero fallback.
Its piece/transparency arrays are pre-object layers, not final door mutations.
Do not use this corpus alone to claim parity with the shipped prerequisites.

For the native/managed prerequisites and initial-object comparison, select:

```powershell
./tools/test-cathedral-geometry.ps1 -CathedralPopulationParity
```

This mutually exclusive mode loads the native level-generation table (trap
percentages 10/15), captures a version-2 pre-theme input sidecar, and compares
sixteen explicit managed cases with no skips. Independent managed bestiary and
theme selection feed held reservations into Golem/object initialization, after
resetting the separate population stream. Shared input grids are asserted
unchanged across native selection/reservation, and fresh native animation slots
are required. Both independent native captures must have identical seventeen
reference artifacts and one sidecar. The reader compares selected types,
animations, themes, reservations, RNG boundaries and complete initial objects,
requires successful wall traps, and never supplies expected selections or final
state to the composed generator. Retained version-1 sidecars remain readable for
the earlier, narrower initial-object-only comparison; the wrapper requires
version 2 and cannot report upstream parity from old evidence.
Builds and stages are bounded, failed stages stop, owned children are cleaned up,
and all opt-in environment variables are restored. Licensed evidence stays
outside the repository. This does not supply final encounters/theme objects,
physical lighting, a production content loader or playable
population/restore acceptance. Existing oracle modes and runtime defaults remain
unchanged.

For the composed prerequisites, initial objects and ordinary monster encounters,
select this mode alone:

```powershell
./tools/test-cathedral-geometry.ps1 -CathedralEncounters
```

It requires the version-2 prerequisites and a separate version-1 encounter
sidecar containing sixteen depths 1-4 cases (seeds 2588/743271966, MAIN/PREV,
quest-disabled original Diablo, fresh single-player Normal). Two fresh native
captures must be byte-identical across the original seventeen artifacts, the
pre-theme sidecar and the encounter sidecar. All earlier artifact bytes and
oracle modes are preserved. The runner selects exactly sixteen explicit
initial-object comparisons and sixteen explicit encounter-method comparisons,
with no exclusions; ordinary parser guards run separately.

The managed path derives fresh 200-slot clearing from the recorded pre-clear
seed and verifies its separate post-clear RNG boundary before the population
reseed. It compares all active/unused monster records, active IDs and target
count, signed occupancy, RNG, tile flags, transparency visibility and all 32
indexed light records/allocator history against native `InitMonsters`. Native
expected inactive slots, selections and final state are not generation inputs.
The raw `cathedral_population_monsters.tsv` and
`cathedral_population_objects.tsv` loader validates detached external tables
and a canonical fingerprint; it does not compile unique metadata or complete
runtime packs. Builds/captures/comparisons have parent-owned deadlines and
failure/cleanup checks; all three opt-in environment variables are restored
exactly, including absent versus empty values on PowerShell 7. The same helper
preserves earlier modes' caller state; regressions exercise Windows PowerShell
and available PowerShell 7 without starting an engine.
Keep the evidence and licensed source data outside the repository. This does
not cover `InitItems`, `CreateThemeRooms`, physical light intensity, campaign
seed selection, a populated runtime profile or ordinary Godot interaction/cold
restore acceptance. Runtime defaults and server MPQ independence are unchanged.

For the additional ordinary initial-item phase, select `-CathedralInitialItems` alone:

```powershell
./tools/test-cathedral-geometry.ps1 -CathedralInitialItems
```

It composes the same managed prerequisites, objects and encounters, then compares
sixteen explicit `InitItems` cases. Two fresh captures must retain byte-identical
17+1+1+1 artifacts; earlier modes and bytes remain separate and unchanged. The
additional `native-initial-items.json` contains external HEAL/MANA metadata and
complete pre/post 128-slot item pools, 127 active IDs, signed item refs, RNG and
unchanged world channels. The reader derives fresh item storage independently
and supplies actual managed upstream results, never native expected final state,
to the item generator. Native names and licensed geometry remain private.

Every public item scalar/string field and five public animation scalars are
compared. Sprite handles and private visual distribution internals are excluded;
portable tests cover the managed reset projection separately. Native partial
clearing, complete allocation, absolute item-seed reseeding, placement and the
limit-one AC draw remain distinct. A safe signed count-roll corner produces one
item; out-of-bounds signed placement candidates are explicitly rejected because
native `ItemPlace` has no corresponding safe retry contract. Raw byte strength
and dexterity requirements retain their native signed-byte conversion. Prior
slot fields must fit native widths and C-string capacity; invalid cursor array
indices fail closed. Names use UTF-8 truncation and first-NUL projection.

The separate `cathedral_population_uniques.tsv` loader validates all 100 source
rows and a canonical fingerprint, deriving quest suppression from source IDs.
The encounter reader exercises it without embedding captured tables in runtime
code. These loaders are not a complete pack compiler or profile activation.
All four opt-in environment variables are restored exactly, including absent
versus empty state. Existing owned-process deadlines, dialog suppression and
failure/cleanup checks apply. `CreateThemeRooms`, quest/campaign generation,
physical lighting and populated Godot interaction/cold restore remain open.
No server MPQ requirement, runtime default or user save is changed.

For detached ordinary Cathedral item setup, run this separate zero-argument wrapper:

```powershell
./tools/test-theme-item-generation.ps1
```

It builds the dedicated registered `theme_item_generation_test` target and the
existing module tests, initializing and checking the matching MSVC environment.
Two independent native captures must be byte-identical; all four native fixtures
and four explicit managed depth comparisons must complete without skips. Every
owned stage has a deadline, inherited dialog suppression, exit/log/cleanup gates
and retained private evidence outside the repository. The opt-in environment is
restored exactly. No engine client or user save is opened.

The 3,040-case corpus uses parsed raw item/affix/unique/spell rows, cursor animation
lengths and available English-context font-row widths. It compares complete
public item scalar/string fields, five animation scalars, unique history and RNG
after `SetupAllItems`, `TryRandomUniqueItem` and `SetupItem`; expected final state
is never an input. It requires no Diablo MPQ. Non-dropping cursor-255 placeholders
retain source ordinals but cannot be selected for ordinary generation. Keep all
captured names/metadata private. Its original four-fixture filter and corpus
remain separate from the additional world-item proof below.

For detached raw selection and complete world-item creation, run:

```powershell
./tools/test-theme-item-placement.ps1
```

This separate zero-argument wrapper rebuilds the same registered native target
and existing module test project with the same owned-process/MSVC safeguards.
All three `ThemeItemPlacementTest` fixtures and four exact explicit depth
comparisons must pass without exclusions; two fresh captures must be
byte-identical. The private bounded corpus contains 224 selector cases and 40
world cases across depths 1-4. Managed inputs come only from actual pre-call
world state, raw metadata and player context; expected selection/position/RNG
are never generator inputs. The comparison covers all 128 item slots, 127 IDs,
112x112 signed item refs, 128 history flags, RNG and unchanged other channels.

Native selection precedes capacity checking; full pools retain every item and
only selector consumption, including zero draws for typed Gold. Placement always
draws once before full-square k=2..49 fallback. Signed non-positive draws may
overwrite an in-bounds occupied tile;
exhaustion keeps an allocated item at (0,0) with no grid reference. Unsafe
out-of-bounds writes fail closed; those unsafe native calls are never executed.
The initial setup seed is distinct from the final seed after unique retries.
The fixture calls actual native functions in single-player Normal mode with
send-message/spawn disabled; delta/pregen flags still affect item setup.

Composed full-level `CreateThemeRooms`, runtime-pack/profile activation, other
game modes and Godot interaction/cold restore remain separate gates. Earlier oracles,
runtime defaults, saves and server MPQ independence are unchanged. Keep native
metadata/captures outside the repository; the world-item wrapper restores its
own opt-in environment variable exactly and never launches Godot.

For complete detached ordinary Cathedral theme rooms and zero-offset static
object lighting, run the separate zero-argument wrapper:

```powershell
./tools/test-theme-room-generation.ps1
```

It uses the same registered native target, checked MSVC environment and bounded,
dialog-suppressed owned-process harness. Eight exact native fixtures and five
explicit managed comparisons must pass without exclusions or runner errors;
both private corpora must be byte-identical across two fresh captures. Separate
room/static-light directories enforce one regular bounded artifact each
(128 MiB/8 MiB), and the process environment is restored exactly.

The room corpus contains 88 worlds: fourteen selected types, their ordered
sequence, full object/item/monster pools, failed fitting, loading and empty
passes at each depth 1-4. Compare all 127 object slots, 200 monster slots,
128 item slots, complete allocator IDs/history, all eleven 112x112 tile channels,
32 light records, theme scratch/flags and RNG. Actual raw object/item tables,
loaded bestiary animation metadata and pre-call state are inputs; expected final
state is never reused. The 40 separate lighting cases cover radii 0-15, corners
and loading copy behavior. Native edge fixtures characterize no-skeleton barrel
selection, blocked reservations, signed fallback and both multiplayer-only
shrine retries. Unsafe reached native reads/writes are rejected by managed
guards and are never invoked in the native fixture.

This proof remains original Diablo, Cathedral, single-player Normal,
quest-disabled and detached from real upstream level composition. Indexed
light allocation, pixel-offset lighting, vision, wider modes/campaigns and
populated Godot interaction/restore are not certified. No Godot or MPQ is opened;
earlier item corpora, launcher defaults and user saves remain unchanged.

For controlled upstream population through ordinary theme rooms, run:

```powershell
./tools/test-cathedral-full-population.ps1
```

This separate zero-argument wrapper first reruns the earlier InitItems route
with all 48 upstream comparisons and repeated 17+1+1+1 artifacts. It then builds
the same registered item-oracle target, captures one exact native full-population
fixture twice and requires sixteen exact explicit managed comparisons with no
skips/errors. Each fresh external directory contains one regular corpus bounded
at 128 MiB; both captures must match by byte length and SHA256. Licensed raw
metadata/evidence remains private outside the repository. Native original-asset
discovery opens the local Diablo archive; no server or Godot client is launched.
The prior eight-fixture detached room filter explicitly excludes this new opt-in
fixture, preserving earlier corpora and comparison scopes.

The matrix is depths 1-4, seeds 2588/743271966 and MAIN/PREV, quest-disabled
original Diablo SP Normal. Managed preceding phases supply all selections,
held flags, pools, scratch XY/variant, physical initial-object light and RNG.
Native after-items/final worlds are comparison-only. Every complete pool/tile
channel, light allocator/history, visibility entry and item-get reset record is
checked; no expected intermediate selection or state is a generator input.
Initial inactive monster IDs are explicitly prepared with count zero/no draws,
then independently reset by native pool clearing. Actual pre-phase stale
inactive light records remain inputs, matching native lighting initialization.

This is the controlled post-CreateLevel pre-clear seam, not full normal level
loading: the application clears monsters before geometry creation. Campaign
seeds, hero/store/quest lifecycle, retained pools and later SavePreLighting/
ProcessLightList remain outside acceptance. A separately identified populated
runtime profile still requires ordinary Godot interaction
and cold restore. Earlier oracle modes, default profiles and user saves are
unchanged. Shared owned-process deadlines, inherited crash-dialog suppression,
terminal exit/log/cleanup gates and exact environment restoration apply. Failed
stages stop immediately; uncertain cleanup must be resolved before retrying.

To verify complete external population metadata against a completed private
full-population capture, without rebuilding or recapturing native fixtures:

```powershell
./tools/test-cathedral-population-metadata.ps1 `
  -FullPopulationEvidenceDirectory "<completed private full-population evidence>" `
  -GeometryDirectory "<directory containing explicit geometry TSVs>"
```

This separate bounded wrapper checks clean retained provenance and reference
hashes, copies only the two explicit geometry TSVs into private evidence, and
exports raw metadata through a test-only adapter. It compiles all nineteen
required tables plus the schema manifest twice with the production CLI, requires
identical file bytes/metadata/manifest identities, then runs sixteen exact
explicit comparisons through reloaded packs and the sixteen original comparisons.
Geometry and all population catalogs/policies come from the loaded pack; native
expected intermediate/final state is comparison-only. No skips are accepted.
Licensed names, font metrics and geometry remain outside version control.

The compiler can also consume already prepared semantic metadata directly:

```powershell
dotnet run --project tools/Cathedral.Playtest --no-restore -- `
  --compile-population-metadata --metadata-directory "<nineteen semantic TSVs>" `
  --output-directory "<new pack directory>"
```

Version 1.0.0 is a distinct standalone metadata schema, not the gameplay schema
or a playable profile. Every table family is mandatory; no installation/MPQ
access, seed mapping, selected-population replay or runtime activation is added.
Separate populated-profile interaction/restore and application-order lifecycle
acceptance remain. Existing launchers and earlier native corpora are unchanged.

To verify the additive fresh-cold boundary in native load order:

```powershell
./tools/test-cathedral-fresh-population.ps1 `
  -MetadataPackDirectory "<private compiled population metadata pack>"
```

This bounded wrapper rebuilds the existing registered `theme_item_generation_test`
with verified MSVC tools, selects `CaptureFreshOrdinaryPopulation` twice into
fresh private roots, requires byte-identical captures, then builds/runs sixteen
exact managed comparisons and focused factory checks. The explicit native
fixture alone discovers original assets; the production factory loads only the
compiled MPQ-free metadata. Native expected worlds and selections are comparison-only.
All stages must exit cleanly with logs and confirmed cleanup; failed evidence
is retained. The wrapper restores its two opt-in environment variables exactly.

Scope is original Diablo, quest-disabled SP Normal, depths 1-4, seeds
2588/743271966, MAIN/PREV, cold-zero pools and a prepared living local Warrior
(Q6 life 6400/mana 3200, unarmed idle frames 10, light radius 8, prior future
75,68, changing=true). Actual `InitPlayer(..., false)` runs after themes, before
the separate population reseed; it does not load hotkeys. Complete pools,
signed grids, RNG, metadata/bestiary order, public animation, pending vision,
lighting and item-get reset state are compared. Existing controlled captures
remain separate and must retain their bytes. This is not campaign seed selection,
retained-level loading, post-theme level-ready processing, runtime installation,
Godot interaction or cold-restore acceptance.

To verify the additive cold post-theme loader handoff:

```powershell
./tools/test-cathedral-level-ready.ps1 `
  -MetadataPackDirectory "<private compiled population metadata pack>"
```

This separate bounded wrapper builds the same registered native target, selects
`CaptureFreshLevelReady` twice, requires exactly the byte-identical corpus and
256-row automap TSV, then builds/runs sixteen exact managed comparisons. It
restores both opt-in environment variables and retains failed evidence. Only
the native test fixture discovers archives; production consumes explicit
MPQ-free metadata. Run the portable `AuthoritativeCathedralLevelReadyGeneratorTests`,
`AuthoritativeCathedralAutomapMetadataTests` and `AuthoritativeCathedralNativeVisionTests`
separately, plus old fresh/controlled parity and unchanged-corpus checks.
These checks do not launch Godot or the server.

Scope extends the earlier ordinary cold policy through InitMissiles/InitCorpses,
SavePreLighting, player occupancy, monster lights/charge cleanup and actual
physical light/vision, stopping before join. Extra player scalars are supplied
after InitPlayer and themes, not before their resets. Full corpus state,
31 corpse definitions, all light/vision records/tails, automap exploration and
visible transparency are compared. Retained missiles/portals, nonzero active
light offsets, wider policies, live checkpoint/profile activation and campaign
loading remain separate gates. Preserve older capture bytes and save identities.

The C# xUnit project is an executable Microsoft Testing Platform runner. Run
it directly when `dotnet test` selects a machine-wide VSTest host whose
`testhost` package is unavailable:

```bat
dotnet build server/tests/Devilution.Server.Tests/Devilution.Server.Tests.csproj --no-restore
server\tests\Devilution.Server.Tests\bin\Debug\net10.0\Devilution.Server.Tests.exe --no-ansi
```

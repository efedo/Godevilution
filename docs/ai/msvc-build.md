# MSVC Build Notes for Agents

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
For live checkpoint inspection, use read-only `FileStream` handles sharing
`ReadWrite | Delete`; ordinary no-delete file sharing can block Windows atomic
checkpoint replacement. The retained first resume phase reported access denied;
the sharing-safe retry did not. This is not a durability or permissions change.
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
and both opt-in environment variables are restored. Licensed evidence stays
outside the repository. This does not supply final encounters/theme objects,
physical lighting, a production content loader or playable
population/restore acceptance. Existing oracle modes and runtime defaults remain
unchanged.

The C# xUnit project is an executable Microsoft Testing Platform runner. Run
it directly when `dotnet test` selects a machine-wide VSTest host whose
`testhost` package is unavailable:

```bat
dotnet build server/tests/Devilution.Server.Tests/Devilution.Server.Tests.csproj --no-restore
server\tests\Devilution.Server.Tests\bin\Debug\net10.0\Devilution.Server.Tests.exe --no-ansi
```

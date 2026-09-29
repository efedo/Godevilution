# Godot Build Notes

The presentation project targets Godot 4.7.1 Mono. Godot is not assumed to be
installed on every development machine, and repository tests that do not render
the client must remain runnable without it.

## Protocol and Solution Verification

From the repository root:

```powershell
dotnet run --project godot/Devilution.Client.Protocol.Tests/Devilution.Client.Protocol.Tests.csproj
dotnet build server/src/Devilution.Server/Devilution.Server.csproj
dotnet build godot/Devilution.Client/Devilution.Client.sln
```

The protocol-test project builds the current server once as an explicit,
build-only dependency, then the live tests launch that Debug server without
starting concurrent rebuilds.

For a focused class or method, invoke the xUnit v3 in-process runner directly.
This avoids requiring the VSTest testhost package cache:

```powershell
dotnet run --project godot/Devilution.Client.Protocol.Tests/Devilution.Client.Protocol.Tests.csproj --no-build -- --filter-class Devilution.Client.Protocol.Tests.AuthoritativeTargetSelectorTests --no-ansi
```

Replace the class name with the focused test class under investigation. Use
`--filter-method` with the fully qualified method name for one test method.
Build the affected project first; `--no-build` is valid only when its binaries
already include the change being checked. Run broader suites at integration
gates or when new changes/failures justify them, as described in the
[completion plan](../completion-plan.md#verification-policy).

## Finding Godot

Use a Godot 4.7.1 Mono console executable. When installed through WinGet it may
not immediately appear in an existing shell's `PATH`. Discover it rather than
hard-coding an installation directory:

```powershell
$godot = (Get-Command 'Godot_v*-stable_mono_win64_console.exe' -CommandType Application).Source
& $godot --headless --path godot/Devilution.Client --quit-after 5
```

The root launcher is the normal zero-argument entry point:

```powershell
powershell -ExecutionPolicy Bypass -File run-godot-client.ps1
```

It delegates to `godot/run-local-client.ps1`, which also checks the standard
per-user WinGet package directory. For headless diagnostics or an explicit
executable, invoke that underlying harness directly. Every launch incrementally
builds the server and Godot project and imports presentation assets. It starts
the server on a free port, validates reported mode and hashes, and supplies those
identities to the client. Only owned process handles are cleaned up, and changed
environment values are restored when the harness returns.

Normal launches retain authoritative saves under `.local/godot/save` and a
private `resume-token.txt` bookmark beside them. Both are ignored by Git. Keep
them together when backing up; do not share the token. The next launch defaults
the setup form to resume, while New character remains available. A directory
lock prevents simultaneous local writers. Logs are retained in the printed temp
directory. `-SessionDirectory` on the inner harness isolates diagnostic runs.
`-Headless -QuitAfterSeconds N` now measures real seconds, not Godot iterations.

## Application acceptance

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -Visible
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -Journey -Visible
```

The first two commands use separate server and Godot processes for create and resume, a
unique retained test save directory, the real `Main.tscn` setup form, mouse
submission, and movement key input. They require accepted movement, matching
character/position after fresh-host resume, and a new command sequence after
restart. A third Recovery phase keeps its Godot process alive while the launcher
terminates only its owned host. For at least 1.5 seconds with no server, real key
input must leave the authoritative snapshot, rendered position, and command
ledger unchanged, with explicit reconnect feedback. Only then does the launcher
restart the host on the same port/save root, revalidate its identity, and require
a fresh snapshot plus the next accepted movement sequence. Marker/process
evidence does not contain the resume credential. This checks a settled-command
outage, not every pending-command, multiplayer, or campaign failure scenario.
Missing evidence, wrong identities/mode, absent checkpoints, timeout,
or nonzero exits fail the run. No MPQs are required. The visible variant also
captures setup/world PNGs for visual inspection; the private token is not emitted
in JSON evidence. This is a narrow session journey, not campaign acceptance.
The visible run also captures disconnected and reconnected frames. The isolated
creation phase deliberately starts with a corrupt bookmark, then
holds its file open to verify persistent write-failure feedback and successful
retry after access is restored. The test uses town level 0 and seed 0 to exclude
combat knockback from its movement/save assertion; normal launch defaults are
unchanged. Keep the evidence directory private because it contains a resume
credential even though the JSON and screenshots do not reveal it.

Protocol tests exercise the model/transport without the Godot scene. A headless
startup verifies a different boundary and does not prove that a visible window
renders. The application harness exercises controls even headlessly, but its
visible variant and inspection of captured frames are separate evidence for
rendering. Native new-game, campaign, and broader multiplayer/pending-command
recovery gates remain distinct checks.

`-Journey` selects a separate fresh-session town-to-Cathedral scenario instead
of Create/Resume/Recovery; omit `-Visible` for headless execution. Both scenarios
exercise all five HUD tabs at 960x600, 1028x720, and 1280x720, checking bounds,
horizontal overflow, the exposed world area, and player visibility. Journey
also Ctrl-clicks the resized world, starts/cancels travel through the real buttons,
observes no further submissions after the in-flight step settles, restarts travel,
and requires the server's level change and target geometry. It uses the shipped
authored route, not test teleportation or client pathfinding. Visible runs retain
layout/tab PNGs; Journey additionally captures dungeon entry. Allow up to 165
seconds for this longer route. Combat/loot/services, return travel, multiplayer,
native generated maps, and campaign completion are not certified by Journey.

## Standalone host performance baseline

Run `tools/measure-authoritative-host.ps1` from PowerShell. It rebuilds Release
artifacts and explicitly selects the otherwise excluded real-time xUnit workload.
Run it without concurrent builds/tests. The one-player single-player case and
four-player multiplayer case each use a fresh standalone host, seed 0, authored
town level 0, real loopback TCP clients, and on-disk checkpoints. One Rogue moves
once per settled round (at least one second); all participants poll using the
normal 250ms snapshot interval and must observe each completed move. Five warm-up
moves precede `profile-start`; fifteen measured moves follow. The test fails on
rejection, missing movement, stale observer state, missing metrics, truncated
samples, bad identities, or nonzero host exit. It does not assert latency budgets.

The printed private evidence directory retains `host.json`, `workload.json`,
minimal final observations, and server saves. Host JSON contains exact metric
boundaries described in the [host guide](../../server/src/Devilution.Server.Host/README.md#opt-in-performance-evidence),
OS/runtime/architecture, participant count, and process memory observations.
Workload JSON records actual duration, command counts, and configuration. No
client session credential is emitted in these reports; keep the save directory
private. Failed/empty output is not a baseline. Record the verified revision and
machine in migration status with each accepted measurement.
Compare checkpoint changes on the same save volume and workload, with the new
per-session capture/publication phases enabled on both sides. Their timings
overlap the outer command/cycle measurements; do not add percentiles together.

For focused execution after rebuilding, set `GODEVILUTION_PERFORMANCE_DIRECTORY`
to an evidence parent and run:

```powershell
dotnet run --project godot/Devilution.Client.Protocol.Tests/Devilution.Client.Protocol.Tests.csproj --configuration Release --no-build -- --filter-method Devilution.Client.Protocol.Tests.LiveAuthoritativeServerTests.MeasureStandaloneHostBaseline --explicit only --no-ansi
```

This measures the standalone server, not Godot frame cost, full combat/campaign
load, WAN latency, native map generation, supported-platform acceptance, or a
release performance gate. Current measurements and remaining budgets belong in
migration status; do not reinterpret a passing workload as a performance pass.

# Godot Build Notes

The checked-in presentation project pins `Godot.NET.Sdk/4.7.1`. Godot is not assumed to be
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
build-only dependency, then the live tests launch that matching-configuration
server without starting concurrent rebuilds. Do not rebuild the same test
project while its executable is running; Windows locks the active test host.

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

Use a stable Godot 4.7.x Mono engine executable. The checked-in C# project keeps
its 4.7.1 SDK pin; local launch acceptance qualifies the selected engine separately.
The launcher discovers it through
`PATH` or WinGet and resolves a console executable to its direct engine sibling.
Use the isolated launcher for headless startup so imports and clients receive
writable AppData paths:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File godot/run-local-client.ps1 -Headless -QuitAfterSeconds 5
```

The root launcher is the normal zero-argument entry point:

```powershell
powershell -ExecutionPolicy Bypass -File run-godot-client.ps1
```

It delegates to `godot/run-local-client.ps1`, which also checks the standard
per-user WinGet package directory. For headless diagnostics or an explicit
executable, invoke that underlying harness directly. Every launch builds the
server, stages the current client sources in its own temporary directory,
imports that copy, then builds the imported project before starting clients. It starts
the server on a free port, validates reported mode and hashes, and supplies those
identities to the client. Only owned process handles are cleaned up, and changed
environment values are restored when the harness returns.

Both single-player and multiplayer Godot profiles explicitly require the host's
negotiated clock-gated scheduling mode. Future commands wait for physical server
time without moving peer snapshots ahead. Idle correlated snapshot probes measure
end-to-end freshness; deliberate ACK waits do not inflate command lead. Retries
wait for scheduled delay plus normal timeout and retain their original identities.
Native launchers and the standalone host's default remain immediate for legacy
compatibility. This is not a bound on arbitrary network/capture stalls or proof
of campaign/shared-world restart recovery.

Godot's editor may upgrade a C# project's SDK during import. Only the staged
copy may receive that change; the source project, existing upgrade backups,
and local edits are not restored, deleted, or rewritten. Generated caches and
build outputs are not copied. Staged project/solution references resolve to
the original protocol projects, and NuGet configuration is retained. The
launcher records engine/SDK identities and rejects unsupported engines or
unexpected project migrations before starting the host or clients. Separate
save profiles no longer share a client import/cache directory. Staging does
not certify arbitrary future Godot compatibility or serialize unrelated
manual builds of the shared server/protocol projects.

Every Godot child, including the editor asset importer, gets its own writable
`APPDATA` and `LOCALAPPDATA` directories beneath the printed launch-log directory.
These are child-process environment overrides, not changes to the user's profile,
ACLs, or registry. Godot 4.7.1 was reproduced crashing with `0xC0000005` during
restricted editor imports after denied writes to its normal settings/help cache;
the same import with isolated AppData exited cleanly. A custom project save path
alone does not redirect the editor's global settings. Authoritative saves and
resume bookmarks retain their existing explicit paths.

Automated imports and acceptance/headless clients suppress modal Windows crash
dialogs only for those child processes; the launching process's error mode is
restored immediately. Nonzero exits and fatal import-storage errors fail the
launch, with process identity/exit evidence and logs retained. No crash is a pass,
even if scene assertions already wrote successful evidence. Normal interactive
client error reporting is unchanged. This prevents unattended dialog accumulation;
it is not a substitute for fixing a recorded engine fault.

`LocalLauncherBehaviorTests` in the server test project exercises the actual
launcher helpers with harmless Windows PowerShell children, including isolated
storage and rejected process/import outcomes. It does not require Godot or
deliberately crash an application. Run actual scene acceptance separately to
verify the installed engine and client/server lifecycle.

Normal launches retain authoritative saves under `.local/godot/save` and a
private `resume-token.txt` bookmark beside them. Both are ignored by Git. Keep
them together when backing up; do not share the token. The next launch defaults
the setup form to resume, while New character remains available. A directory
lock prevents simultaneous local writers. Logs are retained in the printed temp
directory. `-SessionDirectory` on the inner harness isolates diagnostic runs.
`-Headless -QuitAfterSeconds N` now measures real seconds, not Godot iterations.

The additional zero-argument `run-godot-preview.ps1` selects the authored
`town-loop-preview` overlay, town start, and seed 0. Its separate
`.local/godot-preview` save/bookmark directory must not be mixed with the normal
base profile. The original root launcher and base content identity are unchanged.

The separate zero-argument `run-godot-portal-preview.ps1` adds the exact-pinned
`town-portal-preview` overlay after the original preview. It supplies a purchasable
50-gold Town Portal scroll without extra starting resources. Its distinct
`.local/godot-portal-preview` save/bookmark profile must not be mixed with either
existing profile. The inner harness uses `-PortalPreviewContent`; it cannot be
combined with `-PreviewContent` or `-Multiplayer`. Original preview bytes, stock,
content/ruleset hashes and the `-PreviewContent` acceptance path are preserved.

`run-godot-multiplayer.ps1` is the zero-argument two-window local multiplayer
entry point. Its explicit `-Multiplayer` inner-harness mode builds/imports once,
starts one multiplayer host, and supplies separate process-local environment
blocks and bookmarks to both clients. It uses `.local/godot-multiplayer/save`
and `player-1/resume-token.txt` / `player-2/resume-token.txt`, not either existing
single-player profile. Both windows must exit before normal host cleanup; any
nonzero client exit or acceptance timeout cleans up all tracked owned processes.

## Application acceptance

The multiplayer variant is separate from Journey/TownLoop. It runs two actual
Godot processes through new-character forms, moves both players using ordinary
keys, toggles friendly mode, and requires each client's snapshot and world
presentation to show the other's changes without changing its own state.
It checks two distinct private bookmarks and exactly two durable checkpoints.
A fresh host and two fresh clients then resume in reverse connection order,
verify both characters' state, and advance each player's own command sequence.
Per-phase process evidence distinguishes new processes from reused ones. Allow
75 seconds per phase plus builds. Both clients remain connected until both
finish observing the peer. This is a town/session check, not cooperative combat,
multiplayer host-loss recovery, stable cross-restart numeric entity IDs, or an
OS foreground/focus guarantee. Keep the entire evidence directory private.

Scene acceptance treats an accepted receipt or a successful duplicate receipt
as the same applied outcome. A duplicate retaining a rejection reason still
fails; command sequences, snapshot ticks, settled positions, and state changes
remain required. This does not relax admission or resubmit rejected commands.
Multiplayer inputs retain bounded `*-command-<sequence>.json` traces on success
or failure: injected key, last queued sequence, pending count, connection,
receipt fields, and observed local state/ticks. The initial sample and latest
observations are retained, with at most 128 samples and a dropped-sample count.
These files contain neither credentials nor raw snapshots. They distinguish
input that never queued from missing/rejected receipts and unsettled outcomes;
they do not measure OS focus or prove the cause of older runs without traces.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -Visible
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -Journey -Visible
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -TownLoop -Visible
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -Multiplayer -Visible
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

Create, Journey, and TownLoop also flush a real movement key into the input handler,
require one queued command, and deliberately stall the client main thread for
750ms before its next transport poll. The separate host is not paused. Movement
must still be accepted and settle at the expected authoritative tile; the
`*-input-stall.json` artifact records the pause, pre-input tick, and applied tick.
This covers unsent frame-stalled input, not arbitrary host/network delay after
transmission. Resume and Recovery continue to verify durable state and sequences.

Protocol tests exercise the model/transport without the Godot scene. A headless
startup verifies a different boundary and does not prove that a visible window
renders. The application harness exercises controls even headlessly, but its
visible variant and inspection of captured frames are separate evidence for
rendering. Native new-game, campaign, and broader multiplayer/pending-command
recovery gates remain distinct checks.

`-Journey` selects a separate fresh-session town-to-Cathedral combat/loot scenario
and fresh-host JourneyResume instead of Create/Resume/Recovery; omit `-Visible`
for headless execution. Both scenarios
exercise all five HUD tabs at 960x600, 1028x720, and 1280x720, checking bounds,
horizontal overflow, the exposed world area, and player visibility. Journey
also Ctrl-clicks the resized world, starts/cancels travel through the real buttons,
observes no further submissions after the in-flight step settles, restarts travel,
and requires the server's level change and target geometry. It then right-clicks
the authored encounter, uses only starting belt potions below half health when
needed, requires experience and loot, walks away, and clicks the
distant drop to exercise repeated server-owned approach and pickup. A fresh host
and client must retain identity/position, experience, loot, remaining belt item
identities (consumed potions stay consumed), and the defeated
encounter without duplicating the drop, then accept a fresh movement sequence.
It uses shipped authored content, not test teleportation or client pathfinding.
Visible runs retain layout/tab, dungeon, combat, and loot PNGs; a combat trace is
retained for failure diagnosis. Allow up to 165 seconds for Journey and 45 for
JourneyResume, plus builds. Services, return travel/portal, multiplayer, native
generated maps, and campaign completion are not certified by Journey.

`-TownLoop` now selects the separate portal preview in a fresh isolated directory.
It walks to projected Smith through World's declared-destination controls,
verifies cancel/restart, and buys the 50-gold scroll before the combat/loot journey,
unequips the starting bow, equips the earned sword, selects the consumed scroll
source under Hero, and left-clicks to cast. Clicking the server-owned portal
returns to its exact town endpoint, not the reverse stair. It then buys the
existing 25-gold Smith buckler after walking back to Smith, approaches Gillian,
stores bow/buckler, withdraws/redeposits the
buckler, and deposits ten then withdraws five gold. A fresh host/client verifies
equipped/stashed identities, remaining starting potions, consumed-source absence,
content/ruleset identity, experience, the active portal and 20 carried/5 stashed
gold. Walking back to the restored portal and the owner's town-to-dungeon
return must close it before a fresh movement sequence.
Actual tab/list/grid/world/button input submits ordinary
server intents; no teleport, state injection, or free resources is used.
Item assertions use server-provided names, including unidentified names.
Allow 240 seconds for TownLoop and 45 for TownLoopResume, plus builds. Omit
`-Visible` for headless; do not combine `-TownLoop` with `-Journey`. PNGs cover
cast, portal return, equipment, purchase, stash and restore. The original inner
`-PreviewContent -AcceptancePhase TownLoop`/`TownLoopResume` path retains reverse
stairs, 70 carried/5 stashed gold and legacy evidence compatibility. These checks
and focused admission tests cover native-role entry level/range/cursor checks,
but not stock/pricing, complete open-service lifecycle handling,
every placement exclusion,
portal animation/art, a complete M1 loop or campaign parity.

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

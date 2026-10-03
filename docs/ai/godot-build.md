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

Live town-travel failures now include the exact command sequence, requested and
receipt-applied ticks, status/rejection reason, and observed player/action state.
Retain complete runner output when investigating them. The observed model state
is not proof of server state at admission; do not assign a timing/action cause
from a later isolated pass or retry a rejected command to hide the failure.

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
time without moving peer snapshots ahead. Realtime input waits for a valid
current-connection timing probe before first dispatch; pending unsent input does
not prevent startup probes. Bounded calibration failure closes the transport and
retains unresolved input for a fresh reconnect epoch. Idle correlated probes
measure end-to-end freshness; deliberate ACK waits do not inflate command lead.
Retries wait for scheduled delay plus normal timeout and retain their original
identities. Within a connection, delayed snapshots (including newer raw ticks)
do not rewind the C# command estimate or repeatedly round its anchor forward.
The negotiated tick rate remains fixed until reconnect resets that anchor.
Immediate observation and explicit/frozen command ticks are unchanged. This
does not establish the cause of retained `TooLate` failures. The separately
verified native counterpart retains native floor/saturation rules; see
[MSVC guidance](msvc-build.md) and migration status for its evidence and GUI gap.
The paired native launcher also selects clock-gated scheduling; the standalone
host and default low-level native configuration remain Immediate for compatibility.
Native acceptance is recorded separately in migration status. This is not a bound
on arbitrary network/capture stalls or proof
of campaign/shared-world restart recovery.

Production world presentation installs the full geometry projection once per
model-accepted snapshot generation, including same-tick replacements and the
first admitted snapshot after reconnect. Omitted/empty geometry still replaces
the prior projection. Legacy mutable presentation calls remain eager; null/legacy
calls invalidate reuse. Camera, interpolation, visibility, prediction and drawing
continue each frame. This avoids repeated installation between admissions, not
per-snapshot validation/cloning or full-world drawing; measurements belong in
migration status.

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
client error reporting is unchanged. This mitigates unattended dialog accumulation;
it is not universal fault-dialog prevention or a fix for Godot's internal fault.
The retained comparison establishes the denied-write failure path, not the cause
of every historical dialog. Never change user-profile permissions or system-wide
error reporting to run these checks.

Owned-process termination uses a five-second bounded wait per cleanup attempt,
including importer startup failure and recovery-host shutdown. Failed termination
is recorded separately without inventing an exit time/code. Terminal native exits
are recorded before redirected logs are written; diagnostic-storage failures cannot
replace a nonzero engine exit as the primary error. A zero engine exit with failed
diagnostic storage still fails verification. These bounds cover direct owned handles,
not arbitrary OS hangs or independently launched engines/crash reporters.

`LocalLauncherBehaviorTests` in the server test project exercises the actual
launcher helpers with harmless Windows PowerShell children, including isolated
storage and rejected process/import outcomes. It does not require Godot or
deliberately crash an application. Run actual scene acceptance separately to
verify the installed engine and client/server lifecycle.
`MultiplayerRecoveryLauncherTests` additionally exercises the actual argument
guards, exact retained-process evidence and canonical final-owner receipt helper;
these helper tests complement, but do not replace, the application run below.

`CohortCliParticipantRecoveryTests` launches the matching-configuration production
server with shipped MPQ-free content and real TCP participants. It covers abrupt
and graceful restart, reverse-order resume, claimed-item non-resurrection, exact
duplicate/accepted receipts, live competing-lease refusal and malformed-terminal
preservation. Stopped canonical integrity, strict module/outer tick and owner
bindings are checked; RNG equality across different realtime ticks is not claimed.
The test-project build includes the server; no artifact override is needed.
An explicit `GODEVILUTION_SERVER_ASSEMBLY` must exist, with no binary fallback.
Select this class through the server project's xUnit `--filter-class` option.
Owned cleanup is bounded/reparse-checked; failed roots are retained.

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

The zero-argument `run-godot-lifecycle-preview.ps1` uses a distinct
`.local/godot-lifecycle-preview` save/bookmark profile and exact-pinned
`town-lifecycle-preview` overlay. Choose a Sorcerer in the normal setup form.
The authored encounters grant no starting resources or loot. Seed 5 leaves the
itemless Butcher quest available under both current production and native pool
orderings; it does not establish complete quest-pool parity. Existing normal,
town-loop, portal, multiplayer and generated-profile defaults are unchanged.

`run-godot-multiplayer.ps1` is the zero-argument two-window local multiplayer
entry point. Its explicit `-Multiplayer` inner-harness mode builds/imports once,
starts one multiplayer host, and supplies separate process-local environment
blocks and bookmarks to both clients. It uses `.local/godot-multiplayer/save`
and `player-1/resume-token.txt` / `player-2/resume-token.txt`, not either existing
single-player profile. Both windows must exit before normal host cleanup; any
nonzero client exit or acceptance timeout cleans up all tracked owned processes.

## Generated Cathedral playtest

The optional offline compiler builds an isolated ordinary original-Diablo
Cathedral level-1 profile from the explicit base pack and two semantic geometry
tables: `native_dun_piece_definitions.tsv` and `native_dun_tile_properties.tsv`.
It uses the existing strict catalogs and generated adapter. It does not discover
installations or read MPQs, and neither does the server. Supply legally available
semantic tables through a private directory; licensed geometry and generated
packs must not be checked in or distributed as sample assets.

From the repository root, replace the example private geometry directory:

```powershell
dotnet run --project tools/Cathedral.Playtest/Cathedral.Playtest.csproj -- --base-directory server/content/base --geometry-directory 'D:\Private\cathedral-geometry' --output-directory .local/cathedral-playtest --seed 2588
.\run-godot-cathedral-playtest.ps1
```

The output directory must not already exist, even if empty. Compilation validates
the complete pack before creating output, reloads and activates serialized content,
and publishes metadata only after matching its manifest. The seed defaults to
2588 and accepts any uint32, including zero; the CLI selects MAIN entry. A
different seed/profile requires a new output directory and separate saves. The
compiler is development tooling, not a production server dependency.

The zero-argument root launcher requires that compiled private profile. It
creates/resumes only `.local/cathedral-playtest/sessions/local`, keeping existing
base, preview and multiplayer saves unchanged. Pack bytes, profile metadata and
host manifest are checked before use; changed profiles, unmarked existing saves
and reparse-point storage reject without migrating or overwriting saves.

Create a character and use World / Routes / town to cathedral / Travel to walk
from Town `(75,68)` through the native entrance. Every movement step remains
server-chosen. Level 1 has native-generated fine geometry but an explicitly
authored diagnostic object, small encounter and loot. The zero-effect object
grants no resources and occupies its cell until activated. Other levels remain
authored prototypes. This is not native population, quests, campaign mapping,
full art, exact native vision or complete Cathedral/M2 acceptance.

Run the actual scene/control traversal and separate fresh-host resume against
the same compiled profile:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -CathedralProfileDirectory .local/cathedral-playtest
```

`-Visible` enables rendered captures; otherwise this is headless scene acceptance.
The wrapper uses a fresh child session, not the interactive save. `CathedralJourney`
walks into level 1, activates the object, fights and picks up distant loot, observes
continuous fine-cell traversal, cancels and settles its final acknowledged step,
then verifies an exact rejected wall input and a subsequent valid move.
Cancellation evidence requires an accepted final receipt and four unchanged
ticks with no new submissions. If an already queued step moves away from the
captured wall boundary, separately recorded, bounded ordinary Ctrl-click inputs
restore that exact checkpoint before collision testing; they do not count as
continuous-route traversal or relax the wall rejection assertion.
`CathedralResume` starts new host/client processes, compares position, geometry,
object, combat, inventory and sequence state, then walks through native Up stairs
back to Town. Compact route/collision/state evidence and all logs are retained.
Clean engine exits remain required in addition to scene assertions. Do not edit
checkpoints to manufacture entry, traversal or restore evidence.

## Generated Cathedral depths playtest

The separate four-depth compiler consumes the same private semantic TIL/SOL tables,
never MPQs. Supply four explicit uint32 seeds in depth order; this selects ordinary,
quest-disabled original-Diablo generation, not native campaign seed selection.
The following diagnostic inputs have connected routes with the local semantic
catalog used for verification; other inputs must pass validation independently:

```powershell
dotnet run --project tools/Cathedral.Playtest/Cathedral.Playtest.csproj -- --base-directory server/content/base --geometry-directory 'D:\Private\cathedral-geometry' --output-directory .local/cathedral-depths-playtest --depth-seeds '2588,2588,1,4294967295' --placement-policy none
.\run-godot-cathedral-depths-playtest.ps1
```

Output must be fresh. Compilation validates all four geometries, ordered identities,
eight reciprocal routes and surviving content references before atomic publication.
It checks stair connectivity using the server's eight-direction walkability graph.
Disconnected routes reject without changing SOL geometry or choosing replacement
seeds; native object/door placement is still missing. `none` produces no diagnostic
encounters/objects. The alternative/default `authored-diagnostic` policy adds
explicitly authored interactions and never claims native population.

The zero-argument launcher uses `.local/cathedral-depths-playtest/sessions/local`,
the separate `devilution.diablo.cathedral-depths.v1` adapter and strict v3 save identity.
All four seed values must be integer JSON numbers in uint32 range. Hash, session,
reparse and exclusive-lock guards preserve ordinary and existing L1 saves.
Do not overwrite or edit an existing compiled profile to change its seeds.
Town remains authored; this profile does not provide native Town MAIN placement.
Routes enter through the native Cathedral entrance `(25,29)` and return to
Town `(25,31)`. Down arrives at next.Main and Up at previous.Previous.
Floor four retains native Down geometry but deliberately has no Catacombs-5 route.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -CathedralDepthProfileDirectory .local/cathedral-depths-playtest -Visible
```

Omit `-Visible` for headless scene checks. `CathedralDepthJourney` walks through
floors 1-4 and back to two, checks exact geometry/stair arrivals, rejects an exact
blocked-cell input, then saves a subsequent valid move. `CathedralDepthResume`
requires fresh host/client processes, the same content/profile hashes and all four
geometry identities, then returns through floor one to Town. Every move uses
ordinary client controls and authoritative receipts; short native stair paths use
an endpoint-distance lower bound while long paths retain the existing eight-position
requirement. Assertions, captures and engine lifecycle records are retained in a
fresh child session. Failed evidence is never overwritten; rerun the full wrapper
to create a new pair rather than reusing a failed phase's evidence directory.

This verifies diagnostic traversal/restore, not native population, quests,
campaign mapping, broader topology, exact light parity, full art or M2 completion.

## Application acceptance

The multiplayer variant is separate from Journey/TownLoop. It runs two actual
Godot processes through new-character forms, moves both players using ordinary
keys, toggles friendly mode, and requires each client's snapshot and world
presentation to show the other's changes without changing its own state.
It checks two distinct private bookmarks and the selected persistence format:
two legacy participant checkpoints or one complete-cohort checkpoint containing
both stable participant bindings.
A fresh host and two fresh clients then resume in reverse connection order,
verify both characters' state, and advance each player's own command sequence.
Per-phase process evidence distinguishes new processes from reused ones. Allow
75 seconds per phase plus builds. Both clients remain connected until both
finish observing the peer. This town/session check alone does not certify host-loss
recovery, cooperative combat, legacy cross-restart numeric entity IDs or OS focus.
Keep the entire evidence directory private.

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
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -Lifecycle
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -Lifecycle -Visible
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -Multiplayer -Visible
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -PersistenceMode cohort
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -PersistenceMode cohort -Multiplayer
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -PersistenceMode cohort -Multiplayer -Visible
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -PersistenceMode cohort -MultiplayerRecovery -Visible
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -PersistenceMode cohort -MultiplayerPendingRecovery -Visible
```

`-MultiplayerRecovery` adds a separate town/session host-loss phase after creation
and reverse-order resume. It requires explicit cohort mode and cannot be mixed
with preview/generated-world journeys. Exactly two original clients remain alive
while only their owned host is terminated. Each must show reconnect feedback and
for at least two seconds retain its full snapshot fingerprint/revision, pending
and queued command state, receipt and local/peer rendering despite actual key
input. Both offline barriers precede same-port/root host restart. Both restored
rosters must retain the same entity bindings before either submits its next
command. New accepted or successful-duplicate receipts, mutual settled rendering,
unchanged local state after peer movement and exact accepted canonical owner
receipts are required. Rendering uses a bounded wait for interpolation, not an
immediate position check. Process evidence correlates independently captured
initial and retained client identities and terminal old-host exit before its
replacement. Remove `-Visible` for headless execution. This does not cover pending
commands at host loss, every failure, campaign play or hardware power loss; the
negotiated clock flag and accepted commands alone do not independently prove
reconnect-epoch calibration. Any assertion or nonzero engine exit fails the run.

`-MultiplayerPendingRecovery` selects a different third phase, never combined
with `-MultiplayerRecovery`. Two scene-only loopback relays withhold each owner's
original accepted reply and subsequent responses. Before terminating the host,
the launcher independently binds both private bookmarks to exact canonical
receipts and module state; it repeats those checks at the stopped boundary.
Both original clients retain exactly one pending command through two seconds of
offline controls. Automatic reconnect must resend the same parsed Command bytes
(sequence, requested tick and intent), receive the original successful Duplicate
receipt, restore the stable roster and accept the next new real control. Final
offline fingerprints are bound to their per-slot barriers. Terminal host evidence
uses a retained owned handle, a bounded terminal wait and a real native exit code;
missing evidence fails rather than synthesizing a result.

The actual F control assigns friendly mode; it is not a non-idempotent toggle.
`CohortCliPendingCommandRecoveryTests` supplies a production TCP item-drop outcome
that remains unread across restart, while `CohortPendingCommandRecoveryTests`
exercises the production C# transport's relative-movement retry. Relay length and
changed-command negatives execute the scene relay itself without crashing an
engine. These checks do not establish literal envelope-byte equality, durability
of uncommitted future scheduler intents, every outage, independent epoch timing
calibration or campaign/release readiness. Failed fixture roots/logs are retained;
successful owned cleanup is bounded and refuses reparse trees. New CLI commands
use a correlated probe through the following fresh snapshot and the negotiated
tick rate to select `ceil(RTT * tickRate) + 1` future lead; duplicate retries keep
their original requested tick. This does not bound later load. The C# transport
fixture has a linked 90-second deadline, stage diagnostics and bounded client
cleanup; only its explicitly marked original-host loss permits committed-frame
truncation/reset. Unexpected replacement relay faults still fail.

`-PersistenceMode cohort` explicitly opts into a fresh isolated save root; the
default remains `legacy` and existing normal-launch profiles are unchanged.
The launcher verifies the actual host persistence mode and canonical checkpoint
integrity, participant count and private bookmark bindings without emitting
credentials. Reads allow atomic replacement by the running server. Per-phase
process evidence proves fresh hosts/clients; Recovery proves a fresh host with
the same surviving client. Cohort multiplayer reverses admission order and
requires the same local and peer entity IDs as creation. These town/session
checks without either recovery switch do not cover multiplayer host loss; no
variant covers every fault, campaign or release.
For manual cohort play, the inner launcher also requires an explicit separate
`-SessionDirectory`; legacy files are never automatically imported or changed.

Canonical publication uses a fully flushed same-volume rename. On Windows,
handle-based POSIX replacement keeps shared/delete readers on the previous complete
file; unsupported systems fail closed. The supported replacement profile preserves
owner/group/DACL, creation time, supported basic attributes, named `$DATA` bytes and
None/LZNT1 compression. Stage access is verified before any payload. First files
inherit the dedicated root policy; no user/system ACL is changed. Encryption,
object IDs, sparse streams, conditional access and special access-policy metadata
are refused. Audit SACLs are outside this profile and are not copied; no auditing
privilege is enabled. This is not full ReplaceFile metadata equivalence or short-name
preservation. Real-file tests positively query object-ID/sparse setup and verify
that refusal preserves the canonical bytes/generation and metadata before any
observed payload boundary; they do not observe every transient stage operation.
Use a private dedicated root; normal-profile adoption requires approval.

Explicit cohort hosting creates `.authoritative-cohort.initializing` before seed
or staged publication, then commits `.authoritative-cohort.committed` after the
canonical checkpoint. A validated older canonical records committed history before
new staging work. Strict lowercase GUID-named files in `.authoritative-staging`
are preserved but never read/promoted on recovery. Unknown entries and malformed
markers refuse; a committed root missing its canonical cannot reset. Generic stores
and legacy persistence do not infer this host layout. The cohort seed factory remains
bound to the originating live lease and cannot regenerate a missing original seed
after canonical publication. Keep complete roots together when backing up.

Process-kill verification does not certify hardware power-loss/filesystem durability
or repair older unmarked failed roots. Preserve unexpected temporary files for
diagnosis; the launcher does not select, promote or delete them to repair a save.

`Lifecycle` uses real equipment/spell controls, mouse reselection and Q/E cycling,
itemless quest activation, staff casting, native-style class Recharge, a
mana-consuming Firebolt, observed monster-caused player death and Hero /
Restart in Town.
`LifecycleResume` requires fresh host/client processes, unchanged identities,
restored quest/resources, an exact new acknowledged Town step and the complete
unchanged death-dropped staff with no owned duplicate. Off-level items are already
in the complete snapshot; this check does not require another dungeon trip.
Bounded receipt/death/state evidence and six distinct visible milestone captures
are retained. Lifecycle cannot be mixed with the other diagnostic routes.
These authored checks do not close M1, native population or multiplayer recovery.

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

## Actual Godot client performance baseline

From the root, run the zero-argument `tools/measure-godot-client.ps1`. It builds
current Debug sources and runs separate rendered one-client single-player and
four-client multiplayer profiles against clock-gated hosts. Do not run concurrent
builds or application tests. Each profile uses base town content, seed 0, private
fresh saves/bookmarks and a controlled 60-FPS cap; normal launches are unchanged.

The real new-character form and input path prepare participants without state
injection. Five settled warm-up movements precede recording; fifteen measured
movements follow, paced at least one second apart. Every participant must observe
each settled leader move while its own state remains unchanged. Recording starts
after warm-up and stops before screenshots/exit. Marker barriers prevent skipped
rounds and early peer disconnection. Controller observations add low-frequency
test overhead; they are part of this workload, not normal play.

`performance-baseline.json` summarizes exact nearest-rank percentiles from each
client's bounded `frame-measurement/client-frame-evidence.json`. It separates
`ProcessCallbackMilliseconds` (the Main `_Process` body) from
`CallbackIntervalMilliseconds` (monotonic callback-start intervals). Neither is
GPU time or whole-frame completion. Actual engine, renderer/device, display,
viewport, pacing, Debug configuration, OS/runtime/architecture, participants and
public server identity accompany the measurements. Four windows may overlap;
focus/occlusion is uncontrolled and this is not four fully visible displays.
Own-process observations bracket capture creation/stop after workload warm-up;
CPU deltas include recorder/barrier overhead. `LastGcHeapSizeBytes` reports heap
size at the [last garbage collection](https://learn.microsoft.com/en-us/dotnet/api/system.gcmemoryinfo.heapsizebytes),
not live allocations; no collection is forced. Peak working set is a lifetime peak,
not leak/soak evidence. Keep the printed directory private: its saved sessions
and bookmarks contain credentials even though the frame reports do not.

Missing rounds, changed identity, empty/invalid/truncated samples, overwritten
output, nonzero client exits or missing checkpoints reject the baseline. Recording
is explicitly enabled only for this acceptance phase; inherited capture settings
are cleared/restored by the launcher. Headless diagnostics can use the inner
`-AcceptancePhase Performance -PerformanceParticipants 1` (or `4 -Multiplayer`)
with `-Headless` and a fresh absolute `-SessionDirectory`; they do not prove
rendered performance. No FPS/RAM release target, supported-platform matrix,
campaign workload or soak is certified by these local measurements.

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

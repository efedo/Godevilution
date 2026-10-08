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

Live fixtures resolve the server's evaluated MSBuild `TargetPath` and execute
`dotnet <server.dll>` directly, so the owned PID is the actual host rather than
a `dotnet run` parent. Configuration comes from the executing test assembly;
`GODEVILUTION_ARTIFACTS_PATH` selects the matching artifact tree, while
`GODEVILUTION_SERVER_ASSEMBLY` explicitly selects a prebuilt DLL. Missing selected
outputs refuse startup without a fallback build. One stdout parser handles both
identity and operator replies; stdout/stderr drains outlive caller cancellation.
Bounded teardown confirms host exit before deleting a registered GUID-owned
fixture subtree, rejects reparse traversal, and retains uncertain/failed roots.
Per-test cleanup reports teardown failures separately from body failures;
borrowed roots remain untouched after startup refusal.

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

## TCP cohort portal recovery

`LiveCohortRestoresOwnedTownPortalAndNonOwnerUseAfterProcessRestart` uses two real
protocol clients, explicit `cohort`/multiplayer/ClockGated hosting and the existing
town/portal preview stack. Evidence: `.local/cohort-town-portal-restart-79bb820d5c71458799cf60c5b8289754`.
An ordinary Warrior buys and consumes one scroll; owner source-to-town and guest
source-to-town/town-to-source use retain its route. Both actors remain alive and
settle in Town off both endpoints before a forced stop. Validated cohort loading
and module decoding check full projected portal fields, tick-derived opening
counter, exact last portal tick, two participant bindings and complete canonical
accepted ledgers/cursors.

Reverse-order cold rejoin checks each first wire snapshot before input: exact two
saved actors/positions/classes/names, settled actions, content/ruleset identity and
portal counter. Owner endpoint reposition, guest adjacent movement and owner
return then require persisted next client sequences and consecutive global
receipts. Owner return closes the route. A second stopped checkpoint retains
complete updated ledgers and no original or same-owner route.

The new fact plus existing two-participant and legacy portal restart controls pass
in clean and working lanes (six executions/nine zero-exit outer stages).
HEAD `afeada4ac` has 2,795 tracked paths; the clean lane adds only this partial and
replaces only the private fixture, retaining 2,794 original inputs. All 2,813
working inputs/protected20 match; drains/cleanup finish and exact outer identities
are absent. The fixture's optional persistence mode preserves default legacy
callers. Both builds retain the existing Dispatch.cs xUnit2031 warning. Failed r2
null-token and r3 dead-owner attempts remain retained. Fresh r5 qualifies the empty
creation token and corrected journey ordering. Hosts are forcibly stopped.
Rendered interaction, graceful shutdown, full world/resources/RNG and normal
profile adoption remain separate.

## TCP portal counter and cold restore

The focused `LiveServerRestoresTownPortalLifetimeAfterProcessRestart` fact uses
the existing explicit `town-loop-preview` and `town-portal-preview` content,
ordinary Warrior resources, real Smith purchase and a consumed Town Portal scroll.
Evidence: `.local/town-portal-removal-restart-4c14b6b64d8e4a78a8817507667b9899`.
It requires observed pre-clamp aging during accepted movement, full tick-derived
portal equality in the stopped checkpoint and first fresh-host wire snapshot,
source-to-town retention and owner-return closure. A second forced stop verifies
the closure and complete canonical portal-use receipts in the saved checkpoint.
A third fresh host must restore the same settled actor and absent original/owner
routes before input, then accept exactly the persisted next sequence and settle
ordinary movement at a safe adjacent cell actually visited earlier.

Portal identity, owner, endpoints and bidirectionality stay exact while open;
only the counter changes by `max(1, R0 - elapsedTicks)`. This is an opening counter,
not an expiry deadline. The extended fact and two existing restart facts pass in
clean and preserved working lanes: six executions, nine zero-exit outer stages,
complete drains/cleanup and recorded identities absent. The clean archive has
2,795 tracked paths with only this test replaced; its other 2,794 original inputs,
all 2,812 working inputs and twenty protected hashes match. Both builds retain the
existing Dispatch.cs xUnit2031 warning. Hosts are forcibly stopped. Rendered input,
graceful shutdown, native cast-pass alignment and full resources/world preservation
remain separate; this preview does not adopt normal profiles.

## Broader headless gameplay integration

The post-timing-change regression covers existing Journey, TownLoop and Lifecycle
Create/Resume in legacy mode, and two-client Cooperative in explicit cohort mode.
Evidence: `.local/m1-broader-headless-integrated-0d770258058d4edea14f6f2876d440d3`.
The reviewed preserved-tree run freezes all 1,019 runtime/content inputs, keeps
twenty protected hashes, and binds eight staged projects to client source hash
`c3afc5a213d7c286dfb6ffdc5fdc4dea5f2c5a5d2924205b110140b1095e24c7`.
All 26 engine exits are zero and 38 exact PID/start identities are absent.
Source SDK 4.7.1 remains unchanged; isolated SDK/engine are 4.7.2.

The first Journey verifier failed on duplicate reporting of the same session root
after its application wrapper passed. Retain that failure and original logs;
the corrected audit qualifies the completed run separately, while the other
three profiles are fresh. The 236 focused client/server unit executions are
reused evidence, not newly run. This is preserved working-tree integration, not
a pristine committed-source, rendered or native-parity qualification.
Scene assertions remain scoped: Journey does not compare complete inventory and
resources; TownLoop's positive/nonincreasing portal check does not prove elapsed
aging or expiry. Checkpoint metadata and zero outer exits do not establish
graceful host shutdown; host native terminal receipts are unavailable.

## Client presentation packs

`res://assets/presentation-pack.json` is the default client-only manifest. It
reuses the six existing SVG stand-ins without changing their appearance or
license. Set `GODEVILUTION_PRESENTATION_PACK` to another `res://` manifest for an
explicit selection; this is not authoritative content and never enters server
packs, protocol identities, saves or gameplay/RNG decisions. No MPQs are needed.

Schema 1 requires `schemaVersion`, a lowercase stable `packId`, and up to 256
`assets` with unique lowercase IDs. Each binding has `id` and `kind` (`texture`,
`audio`, or `localization`). Textures/audio require a path relative to the
manifest directory; localization requires inline `text` and no resource path.
Absolute, URI, parent/current-directory, control-character, backslash and
Windows trailing-dot/space aliases reject. Unknown/duplicate JSON properties,
unsupported schemas and any invalid binding refuse the whole catalog before use.
Documents are limited to 1 MB; IDs to 64, paths to 240 and text to 1024 characters.

Optional texture `frames` require `frameCount` (1-256), `frameWidth`/`frameHeight`
(1-4096), `columns`/`rows` (1-64), finite `framesPerSecond` (greater than zero,
at most 240), and explicit `loop`. Count must fit the atlas; actual texture size
must contain its declared grid. Delta-driven animation is visual only, freezes
offline, and resets after a new admitted reconnect epoch, level change or rewind.
Unknown bindings retain safe legacy lookup; unavailable/undersized declared
textures use procedural fallback rather than bypassing their declaration.
Unavailable optional audio stays silent. Feedback accepts optional
`feedback.damage.prefix`, `feedback.healing.prefix` and `feedback.other.prefix`
localization; default numeric feedback remains unchanged.

Schema 1 also accepts an optional `actors` array of at most 512 player-art rules.
Each rule has a unique stable `id`, an `assetId` referencing a declared texture,
and at least one selector: unsigned `classId`, `action`, `combatMode`, or `facing`.
Action names are `idle`, `move`, `attack`, `cast`, `death`, `level-transition`,
and `restart-town`; idle means no action or an unspecified action. Combat/facing
names are the existing protocol enum names, not numeric strings. Named selector values
are case-insensitive and accept hyphen/underscore spelling; IDs remain exact.
Empty or unknown selectors reject the entire pack. For example:

```json
"actors": [
  { "id": "rogue-idle-south", "assetId": "rogue.idle", "classId": 1,
    "action": "idle", "combatMode": "stand", "facing": "south" }
]
```

The unique matching rule with the most specified fields wins. Any pair of
equal-specificity rules whose selectors can overlap refuses the whole catalog,
even if they share an asset. Array order never breaks ties. Oversized rule arrays
refuse before construction and pairwise ambiguity checks. Unknown server enum
values or no matching rule retain the base/procedural player path; missing or
undersized selected art remains procedural rather than substituting base art.
Actual per-entity drawing records the selected rule and resource; observation
getters do not load resources. This extends player art selection only: its clock
is still global and visual-only, not native action-frame timing or gameplay.

HUD localization keys use `ui.hud.` followed by `tab.hero`, `tab.items`,
`tab.trade`, `tab.stash`, `tab.world`, `travel`, `cancel-travel`, `approach`,
`spell-sources`, `belt`, `equipment`, `oil-targets`, `store-stock`, `service-help`,
`stored-items`, `routes`, `destinations`, or `world-help`.
Missing entries retain English. Node names, server-supplied
labels, intent IDs and event wiring do not localize. Fallback guidance names the
displayed tab title. This is static HUD integration, not complete localization.

The base single-player acceptance driver observes actual resource/draw use,
default appearance, changing atlas regions, missing/undersized textures,
atomic unsafe-manifest refusal, offline frame freeze and admitted-epoch reset:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -PresentationPackProfile atlas -Visible
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -PresentationPackProfile semantic -Visible
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -PresentationPackProfile missing
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -PresentationPackProfile undersized
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -PresentationPackProfile invalid
```

Accepted profile names are normalized to lowercase before child selection.
Fixtures reuse the original SVGs and isolated acceptance saves, restore both
selection environment variables on exit, and cannot combine with other journey
profiles. Clean native exits remain mandatory. Pure catalog/resolver/clock tests
run in `PresentationAssetPackTests` and `SemanticPlayerPresentationTests` without
Godot. The semantic fixture checks authoritative Rogue idle/facing selection,
accepted south-west movement with missing-selected-art fallback, loaded idle
atlas freeze/reconnect reset, and actual localized/default HUD controls. This is
not full art, audible playback, complete localization, optional original-art
import or M4.

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

For an unattended visible diagnostic outside an acceptance phase, use the inner
harness's explicit `-Automated` mode with a separate session directory. This
enables the same child-only suppression, a real-seconds scene quit timer and a
parent deadline fifteen seconds later. `-QuitAfterSeconds` alone does not turn a
normal interactive launch into automation. The zero-argument root launcher stays
interactive. Agents must use `-Automated`, `-Headless` or an acceptance phase for
unattended runs; do not retry after a crash or uncertain cleanup.

```powershell
$automatedSession = Join-Path ([IO.Path]::GetTempPath()) ('Godevilution-VisibleCheck-' + [guid]::NewGuid().ToString('N'))
powershell -NoProfile -ExecutionPolicy Bypass -File godot/run-local-client.ps1 -Automated -SessionDirectory $automatedSession -QuitAfterSeconds 30
```

Owned-process termination uses a five-second bounded wait per cleanup attempt,
including importer startup failure and recovery-host shutdown. Failed termination
is recorded separately without inventing an exit time/code. Terminal native exits
are recorded before redirected logs are written; diagnostic-storage failures cannot
replace a nonzero engine exit as the primary error. A zero engine exit with failed
diagnostic storage still fails verification. These bounds cover direct owned handles,
not arbitrary OS hangs, descendants or independently launched engines/crash reporters.
Cleanup/evidence failures reject an otherwise successful launch; an existing engine
failure remains primary. This is not an OS process-tree containment or persistent
retry lock. Confirm any reported owned PID has stopped before another run; never
kill unrelated Godot or Windows error-reporting processes by name.

`LocalLauncherBehaviorTests` in the server test project exercises the actual
launcher helpers with harmless Windows PowerShell children, including isolated
storage and rejected process/import outcomes. A minimal non-crashing executable
also checks inherited suppression bits before host-shell startup can change them.
Policy tests cover manual, visible automated, headless and acceptance modes, plus
cleanup-error rejection. These tests do not require Godot or
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
seeds; this depth-only profile deliberately does not enable native doors or scanner
lights. `none` produces no diagnostic
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

## Generated Cathedral door playtest

Door mode is a separate offline compiler/profile, not an upgrade of existing L1,
depth-only, base or preview saves. It consumes explicit private semantic TIL/SOL
tables and four ordered seeds. Neither compilation nor the server reads MPQs.
Use a fresh output directory; never overwrite a compiled profile or its saves:

```powershell
dotnet run --project tools/Cathedral.Playtest/Cathedral.Playtest.csproj -- --base-directory server/content/base --geometry-directory 'D:\Private\cathedral-geometry' --output-directory .local/cathedral-doors-playtest --depth-seeds '2588,2588,1,4294967295' --cathedral-doors --placement-policy none
.\run-godot-cathedral-doors-playtest.ps1
```

The marker is `cathedral-doors-playtest.json`; the adapter is
`devilution.diablo.cathedral-doors.v1`. Its door-catalog identity, explicit left/right
type mappings, content/file hashes and distinct session identity fence the private
profile. The root wrapper uses only `.local/cathedral-doors-playtest/sessions/local`.
Changed metadata/content, conflicting or unmarked saves and reparse-point storage
refuse; no existing profile is migrated. Licensed geometry and generated packs
remain private and must not be committed or distributed as sample assets.

The server scans native doors and fixed-light footprints under the native pool
limit. Operable-door reachability checks use native transitions on detached
geometry, not wall repairs or replacement seeds. The published initial world
still has closed doors. `none` is the default; `authored-diagnostic` is explicitly
optional and must pass final production occupancy/pool validation. This is not
complete native object/light/monster population, population RNG or campaign mapping.

The portable `CathedralDoorCompilerPortableTests` class checks exact mixed
door/light capacity (127 accepted, 128 rejected) and safe adjacent versus blocked
authored item footprints through the actual compiler. Its synthetic candidates
are independently materialized before compilation; failed compilation must leave
no destination or staging publication and preserve the base tables. Run all six
facts through the existing module xUnit v3 runner after rebuilding. This is
diagnostic compiler coverage, not native layout or rendered acceptance.

The explicit `CathedralDoorRouteCompatibilityTests` fact uses private semantic
catalogs to pin baseline closed routes and the depth-2 seed `743271966` pair:
raw preparation remains nonmutating, ordinary compilation refuses the disconnected
route, and door compilation accepts the same seeds with all doors initially closed.
It compares initialized channels/caches and preserves raw discovery inputs; this
is integration coverage, not an independent native oracle. After rebuilding:

```powershell
$env:GODEVILUTION_CATHEDRAL_DOOR_PLAYTEST_GEOMETRY = 'D:\Private\cathedral-geometry'
.\server\tests\Devilution.Diablo.Tests\bin\Debug\net8.0\Devilution.Diablo.Tests.exe --filter-class Devilution.Diablo.Tests.CathedralDoorRouteCompatibilityTests --explicit only --fail-skips on
```

Absent private inputs produce an honest skip; `--fail-skips on` makes that
absence fail qualification. No licensed fixture is included in the repository.

Godot displays schematic CLOSED, OPEN and BLOCKED cues from validated typed server
state. Left-click operates a door; Ctrl-click walks through an open doorway.
Occupied closing must retain the open frame/geometry until the occupant leaves.
These cues are not original sprites or native loaded-art/frame qualification.
Native activation/GUI acceptance remains a separate gate.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -CathedralDoorProfileDirectory .local/cathedral-doors-playtest -Visible
```

Omit `-Visible` for headless scene checks. The wrapper uses a fresh child session.
`CathedralDoorJourney` enters L1 from Town, approaches a reachable closed door,
opens it through ordinary input, physically occupies its cell, verifies BLOCKED
and an unchanged occupied-close outcome, leaves, recloses and saves it open.
`CathedralDoorResume` starts a fresh host/client, checks the exact saved identity,
position and four-floor mutable geometry/typed-door hash before any input, then
requires the first new movement sequence to equal the saved sequence plus one.
It repeats occupied-close/clear/close through normal controls. Rendered checks
observe actual door drawing and retain milestone PNGs. Allow 300/180 seconds for
the scene phases plus launcher/build margins. Clean engine exits remain mandatory.
A successful journey establishes L1 door integration and cold restore, not
four-floor traversal, full native art/population, multiplayer door recovery, a
campaign or M2 completion. Current headless phases pass; rendered creation and
restored-open drawing pass, but first resumed movement rejects `TooLate`.
See [migration status](../migration-status.md#m2-isolated-cathedral-door-playtest-integration-2026-10-03)
for the retained failure. Do not relax admission or retry to hide that result.

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

`-Cooperative -PersistenceMode cohort` selects a separate two-client authored
combat/loot journey, never implicitly selected by ordinary multiplayer. Both
Rogues use the real setup form, route controls and safe tile input. The peer
approaches the projected stair in Town, cancels and remains stationary while
the first player enters and clears the shared landing. Each client must observe
the other's landed attack, the same defeated monster and drop, and exactly one
owner's inventory pickup. A fresh host and two fresh clients then resume in
reverse connection order, preserving both entity IDs/positions, defeated-world
state and unique loot ownership before new settled movement. The launcher checks
exact accepted canonical owner receipts, including the actual pickup command,
not an earlier approach step. Allow 300 seconds for CooperativeJourney and 240
for CooperativeResume, plus builds. Admission rejection, death, missing evidence,
nonzero engine exit or uncertain cleanup fails; never weaken gameplay or retry
to hide such a failure. Normal profiles, resources and legacy saves are unchanged.

The visible cooperative variant retains entry, defeated, loot and final frames
for each client, plus restored and final frames after restart. Per-entity texture
bindings are retained resource lookups, not timestamped proof of drawing on each
frame: inspect the paired PNGs to verify both avatars are actually visible.
Headless success is not rendering acceptance. This authored encounter does not
close M1, native animation/world parity, complete campaign or broader recovery.

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
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -PersistenceMode cohort -Cooperative
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -PersistenceMode cohort -Cooperative -Visible
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

The separate TCP equipped-staff cold-restore regression can be run from the
workspace after building `godot/Devilution.Client.Protocol.Tests` in Debug:

```powershell
.\godot\Devilution.Client.Protocol.Tests\bin\Debug\net8.0\Devilution.Client.Protocol.Tests.exe --filter-method Devilution.Client.Protocol.Tests.LiveAuthoritativeServerTests.LiveServerRestoresSorcererRechargeAfterProcessRestart --parallel none --fail-skips on --no-ansi
```

Require one passing fact without skips. Actual Sorcerer creation, Charged Bolt
and typed class Recharge use positive canonical receipts; the full equipped
staff changes 40/40 -> 39/40 -> 39/39. Stop the owned first host before reading
its checkpoint, then require a fresh host/client to restore the staff and four
resources before input and preserve them after the next accepted movement.
The canonical save omits two catalog-derived spell presentation annotations;
fresh wire snapshots restore them. Tokens stay in memory and failed test roots
are retained privately. Hosts are forcibly stopped by the existing harness.
This protocol regression complements Lifecycle's rendered controls/death-drop
journey; it does not replace that gate or qualify full inventory/world recovery.

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
evidence records the owned host's PID/start identity, confirmed native exit code,
terminal observation time and port before disposal or any reconnect wait. The
driver requires the stopped marker and replacement record to agree, and rejects
replacement timestamps before confirmed exit. `EndedUtc` is observed after the
bounded terminal wait, not the OS exit timestamp; forced host termination need
not return zero. Evidence does not contain the resume credential. This checks
a settled-command outage, not every pending-command, multiplayer, or campaign
failure scenario.
Missing evidence, wrong identities/mode, absent checkpoints, timeout,
or nonzero Godot engine/client stage exits fail the run. No MPQs are required.
The visible variant also
captures setup/world PNGs for visual inspection; the private token is not emitted
in JSON evidence. This is a narrow session journey, not campaign acceptance.
Managed packaged-restart fixtures inspect legacy checkpoints only after confirmed
owned-host exit. Windows `File.Move(overwrite:true)` can refuse publication even
with a delete-sharing reader; a different replacement API is not evidence for
that publisher. The test abruptly stops the host after an Accepted move, then
uses the production store to validate identity/state/receipt integrity and checks
the exact durable receipt and player state. No byte/tick shortcut, graceful flush
or corruption retry substitutes for this proof. Cancellation and missing/corrupt
inputs remain failures. Failed GUID-owned roots are retained; guarded cleanup
reports secondary failures without replacing the primary assertion.
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

Create additionally flushes two real friendly-mode key inputs without a frame or
transport poll between them. Both must be queued together and return separately
accepted, ordered receipts before save/resume proceeds. Both inputs read the
same snapshot and assign the same absolute mode; this is not a relative double
toggle or client-owned simulation. `create-input-burst.json` retains detached
results on success or failure. Normal play installs no acknowledgement observer.
The host admits separate envelopes before awaiting their ACKs, while retaining
whole-envelope FIFO, bounded responses, strict late admission and frozen retries.

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

## Monk Search content prerequisite

`server/content/monk-search-preview` is an exact-base optional overlay containing
only Level 1 start (20,20). It keeps the normal Hellfire Monk class 3 Search 46
loadout, effective spell level 0, resources and items. The adjacent (21,20) cell
is walkable, empty, outside authored monster aggro and not a warp trigger.
It adds no actors, grants, catalog overrides or normal-profile adoption.

`Devilution.Diablo.Tests.MonkSearchPreviewContentTests` passes 2/2 without skips
in both verified HEAD-derived source with the candidate overlay and the existing
working tree; evidence is
`.local/monk-search-prerequisite-72f3ed434ff14b2aa37b280fbc2983ec`.
This qualifies content only; the retained timing counterexample below was found
while preparing the actual-control journey. Native Hellfire Search is illegal
in Town and does not reveal Rogue traps.

## Timed-effect catch-up qualification

`AuthoritativeSimulationRuntime` keeps the original constructor's coalesced
`ParticipantAdvancePolicy.OncePerAdvance` contract. The additive policy constructor
allows `BeforeEachTick`: ordinal participant-local state advances before each
replayed world tick. Ordinary and restored Diablo adapters select it so effects
created inside catch-up age as they do during single-step advancement. No save
schema or host profile selection changes.

Clean and working Engine/module/server checks pass without failures/skips in
`.local/search-timing-fix-092f15ef838b4d4b9aef4e4b6e91a662` (1,070 executions,
including repeated focused checks; thirty clean owned terminal receipts).
`.local/search-shared-integrated-ce408ab9bec14c02a8597a6ef7e49198` records eight current-source hidden headless profiles:
Search, Bard Identify, session menu, Trap Disarm, Warrior Repair, default legacy,
default cohort and pending-command recovery. Sixty engine stages exit zero;
ninety exact recorded identities are absent; nineteen isolated project stages
share one client source hash with source SDK 4.7.1/staged SDK 4.7.2. The real Search
control, fresh-host residual status, next movement and observed expiry pass.
The initial diagnostic-helper compile failure and its source are retained;
removing only `static` corrected the build. The unaffected 236 focused checks
were explicitly reused for that modifier change, with no claim of fresh execution.

This is managed catch-up consistency and headless current-candidate evidence.
The separate prepared native prerequisite in `.local/monk-search-native-1e3041893a3a46f7955938b9fa2433d0` passes actual
cast/missile processing, exact 247-pass lifetime, recast/overflow boundaries,
automap item-visibility flags and scoped state restoration (4/9/27 executions).
See [MSVC build notes](msvc-build.md#prepared-native-monk-search-prerequisite).
A separate expanded-input caller qualification passes 5/10/30 executions:
canonical Monk loadout/animation data feeds actual StartSpell/ProcessPlayers/
DoSpell, observing 247 before the paired missile pass and 246 afterward.
Canonical player tables stay loaded per process; other scoped state is restored.
It does not run full GameLogic/CheckPlrSpell/Town admission or establish native/
server phase alignment. Rendered automap/application acceptance and graceful host exit remain
unqualified; host checkpoints were checked as metadata. Scene/launcher commits
await rendered acceptance. Existing approval holds apply.

## Opt-in command-timing diagnostics

Add `-CommandTimingTrace` to `tools/test-godot-session.ps1` for a fresh isolated
diagnostic journey, for example:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-godot-session.ps1 -CathedralDoorProfileDirectory .local/cathedral-doors-playtest -Visible -CommandTimingTrace
```

Normal zero-argument launchers do not enable this capture. The inner launcher
requires an explicit acceptance phase, clears/restores inherited trace settings,
and refuses to mix diagnostics with performance measurement. It enables the
host's `--command-timing-trace true` and bounded client recorder together.

Each phase writes `<phase>-client-command-timing.json` before scene quit, on
success or failure, using a new file without overwriting earlier evidence.
`AcceptancePassed: false`, a null report, or explicit record drops must remain
visible. A missing file after a native crash or forced timeout is incomplete
evidence, not an empty successful capture. Capture ends at the acceptance
epilogue, not transport shutdown. Host `COMMAND_TIMING_JSON:` lines are retained
in that phase's printed server stdout log, incrementally rather than only at
graceful host shutdown. Correlate phase/process/connection, client sequence and
timing-request IDs; never concatenate fresh-process resumes as one clock epoch.
`AcceptancePassed` describes the scene outcome only; terminal engine exits remain
separate. Host `PublisherComplete` describes publisher shutdown, while
`CaptureComplete` describes recorder/sink delivery accounting, not gameplay or
operation completion. Missing operation terminals remain incomplete intervals;
capped-marker counts are lower bounds, not final lifetime totals.

Client freeze/write, snapshot/probe/ACK observations and exact host
read/admission/ACK/snapshot boundaries contain scalar diagnostic metadata, not
command payloads or resume credentials. Server read completion means full
envelope parsing, not physical packet arrival. Stopwatch timestamps/frequency
can support same-machine Windows interval comparisons; unrelated process-relative
origins or cross-machine clocks cannot. Instrumentation adds overhead and cannot
certify release latency. It changes neither strict late admission nor frozen
retry identities. A passing diagnostic repeat does not explain or erase an
earlier uninstrumented failure. Keep the containing session directory private:
its saves/bookmarks still contain credentials.

## Actual Godot client performance baseline

Performance markers are published by closing a temporary file then moving it
to the final name. Readers permit shared read/write/delete access so the Windows
rename handle can coexist with a read; they retain legacy UTF8/BOM decoding and
strict JSON/marker validation without IO retries. The portable marker-reader
tests include a Windows DELETE-handle counterexample and explicitly skip that
case on other systems. This scoped regression does not replace a complete
one/four-client exported workload or rendered acceptance.

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

Performance captures bind the `client` and `client-protocol` runtime assemblies
against an independent file manifest taken after the fresh staged Debug build and
before client startup. `RuntimeAssemblies` records full identity, MVID, image SHA,
configuration, explicit `DebuggableAttribute` optimization state, file name and a
canonical image-path SHA. Absolute image paths stay out of the public report.
The qualified local Godot 4.7.2 runtime loads these application assemblies from
memory: `StagedImageWithMatchingMvid` identifies a verified staged image rather
than inventing an `Assembly.Location`. Identity/MVID matching does not establish
byte-for-byte integrity of the loaded in-memory image. Missing, extra, mistyped,
mismatched or optimized assembly evidence rejects this Debug baseline. Ordinary
profiles clear the new image-directory setting and perform no capture hashing.

Godot's optimized application configuration is `ExportRelease`. Independently
reviewed assembly-only probes now execute genuine standalone exports from both
preserved dirty sources and an archived committed source. Matched official 4.7.2
Mono Windows x64 templates and export presets exist only in disposable workspace
stages. The source SDK remains 4.7.1; isolated imports use SDK 4.7.2. Independent
pre-runtime bundle manifests bind physical runtime locations, identities, MVIDs
and file/path hashes for three optimized ExportRelease application images and
the exact optimized Release GodotSharp image. The committed repeat also verifies
all 143 archived build inputs before and after export. All owned stages exit
cleanly and are absent. These assembly-only probes instantiate no Main,
connection or save.

A separate disposable committed-source qualification now launches the actual
standalone exports through Main's new-character/input path and the same
one/four-participant Performance workload. Independent pre-runtime manifests
and private startup witnesses bind all four physical optimized images; all
rounds, observer barriers, terminal proofs and zero-loss frame captures pass.
The hosts are Debug and forcibly stopped, with direct PID/start-bound terminal
receipts. Checkpoint verification is metadata only; a retained temporary save
file prevents any claim of fully drained writes or cold durable restore.
This exported headless baseline does not change the Debug measurement command
above or ordinary launch profiles. Rendered Release measurements, native
machine-code optimization, soak, platform coverage and release budgets remain
open. See migration status for exact revision/evidence and retained failures.

A reviewed matching-Release-host repeat is retained at
`.local/godot-exported-performance-run-4e964a7e71bc4dabbd7eda234f9ef2eb`.
Its ignored private driver freezes fresh committed HEAD `8383fc7b4`, builds a
Release host, and patches only a disposable generated launcher. Both genuine
exported one/four-client Main profiles pass with explicit legacy persistence;
all four optimized physical client runtime images are bound per client.
Live native apphost path/PID/start and adjacent Release DLL metadata are checked
before/after each profile; this does not inspect loaded managed/in-memory bytes.
The normal launcher and historical Debug-host baseline are unchanged.

Five valid zero-drop captures and all round/observer/process barriers pass.
Both hosts are forcibly stopped; one temporary save remains in the four-player
profile. Checkpoint inspection is metadata only. Fully drained saves, cold
restore, graceful shutdown, rendered/GPU or whole-frame timing, soak, platform
matrix, native machine-code optimization and release budgets remain unqualified.
Migration status records exact evidence, metrics and retained failed attempts.

A separately reviewed exported single-client cold-recovery diagnostic is retained
at `.local/godot-exported-recovery-run-89c6bf748ab34f878146bdb8ddefbcea`.
It freezes committed HEAD `884193c6d` and instruments only disposable sources.
Genuine ExportRelease Main runs explicit Legacy Create and fresh Release-host
Resume. A first-wire observer precedes validation/model application; actual
Store.Load/codec inspection and byte-identical retained checkpoint copies bind
the settled player scalars and full accepted receipt ledger. Create ends at
cursor 3/(0,1); Resume restores it before input and accepts cursor 4/(0,2).
All 42 owned identities are absent, and this lane has no temporary canonical
save. Keep save bodies and credentials private; only scalar reports and hashes
are public evidence. Hosts still stop forcibly. Full-world/RNG/inventory parity,
rendered/native cold Resume, graceful shutdown, normal cohort adoption and
release/platform/soak acceptance remain open. See migration status for exact
evidence, retained failures and the narrower historical Performance scope.

The separate graceful exported recovery lane is retained at
`.local/godot-exported-recovery-graceful-run-44dac60a5d644d01989813f5a2cbe153`.
It freezes committed HEAD `be8118de7`, instruments disposable sources, and runs
headless ExportRelease Legacy Create followed by a fresh Release-host Resume.
The source-pinned driver performs no-engine generated-launcher preflight first.
The reviewed outer wrapper rejects evidence-root collisions and bounds preflight
to 120 seconds and the full run to 900 seconds. The host retains its native
handle, writes/flushes `quit` only after client exit, joins both output pumps,
flushes sinks and records actual native exit before disposal. A shared 30-second
quit deadline and strict 26-field receipt reject forced stops, task/drain faults,
changed streams, missing identities and terminal evidence collisions.

Both hosts exit naturally with code 0; original and retained canonical Store/
codec reports preserve receipts 1..3 then append 4 after cold Resume's first wire.
All 42 recorded identities are absent. Keep private saves and tokens withheld;
publish scalar reports/hashes only. Local helper/driver qualifications comprise
21 pure guards, 13 synthetic lifecycle cases and 32 terminal-validator cases.
A future run needs fresh source/archive/dependency pins and reviewed generated
source. This scoped pass leaves rendered/native moved-position Resume, full
world/RNG/inventory parity, normal cohort adoption and release/platform/soak
gates open. Migration status retains exact evidence and earlier failed attempts.

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
Run it without concurrent builds/tests. Immediate and ClockGated scheduling each
run one-player single-player and four-player multiplayer cases: four fresh
standalone hosts, seed 0, authored town level 0, real loopback TCP clients, and
on-disk checkpoints. Both the wrapper and explicit workload refuse a nonempty
host-assembly override, so Release test configuration cannot select a Debug host. One Rogue moves
once per settled round (at least one second); all participants poll using the
normal 250ms snapshot interval and must observe each completed move. Five warm-up
moves precede `profile-start`; fifteen measured moves follow. The test fails on
rejection, missing movement, stale observer state, missing metrics, truncated
samples, bad identities, or nonzero host exit. It does not assert latency budgets.

The printed private evidence directory retains `host.json`, `workload.json`,
minimal final observations, and server saves. Every participant must negotiate
the selected ServerHello scheduling mode. All thirteen declared metric summaries
must be complete with zero drops: Immediate has eleven populated summaries and
empty queue-admission/scheduled-checkpoint summaries; ClockGated has twelve
populated summaries and an empty immediate-admission/checkpoint summary. Empty
summaries require zero samples and null statistics. Missing/unknown metrics,
invalid summaries or unexpected scheduling fail.

ClockGated additionally records the moving client's scalar dispatch/receipt
timings. Every accepted sequence requires a successful first dispatch at a
positive requested tick, stable requested ticks across retransmissions, and an
Accepted/Unspecified result with a positive server receipt at that exact tick.
Timing capture must have zero dropped records. This optional recorder adds
measurement overhead to the ClockGated cases. Workload metadata retains these
bindings, every participant's negotiated mode, inapplicable metrics and the owned
host PID/clean-quit observation. It does not emit payloads or session credentials.

Host JSON contains exact metric
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

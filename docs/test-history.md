# Verification history

This is a curated record of accepted, scoped verification and material open
limits. It is not a release-readiness claim or a replacement for the current
[migration status](migration-status.md) and [feature inventory](feature-gap-inventory.md).
Commit IDs identify the source change; a pass applies only to the scope stated
here. Git history preserves older document versions; detailed assertions remain in
the referenced test sources. Do not copy raw outputs into historical archives.

## Client and host verification

### 2026-10-19 — native Town geometry prerequisite

The checked MSVC x64 `native_scene_art_test` passes 2/2 without skips. Its actual
native warp predicate covers 8,960 mode/bit/level/quest contexts. An isolated
private export runs production `CreateDungeon`/`CreateTown` over 191 cases:
160 gate/water/Hellfire combinations at two seeds, all eight entry kinds with
fresh/dirty channels, remaining warp entries, retained mutations and all four
water states. Every fine piece, special, SOL flag, transparency byte, coarse
mega/protected cell, bound, view and final RNG matches the managed builder.
All 22 focused xUnit cases pass without skips, including the explicit oracle,
exact fine-grid boundary, malformed inputs and detached state.

Native inputs are original base DIABDAT assets plus the bundled Town automap.
SOL loads into an explicitly zeroed 1,379-entry table; Town's partial sector4
DUN tail is ignored only by the bounded Town channel reader. General DUN/SOL
validation is unchanged. The fixture restores generator, lookup and blitter
state; canonical player tables remain loaded under the native table-test
convention. Original sources and captures stay private.

The same export captures every original Town/Cathedral MIN slot and full-piece
floor/content pass through production conversion/blitters, plus special CEL
sprites. Managed scene pixels and client binding are subsequent gates. Existing
profiles still have empty Town layers; profile/persistence integration, Hellfire
art, shared isometric client input and rendered parity remain open. Independent
review has no remaining findings for this prerequisite.

Evidence: `.local/native-scene-20261019/original-reference-v2`,
`native-capture-v2.log`, `native-capture-v2.xml` and `town-verified.log` under that
batch's `.local` root. Earlier failed captures are not acceptance evidence.

### 2026-10-19 — optional native player art

The checked MSVC native fixture passes 3/3, including opaque index zero and a
cross-row opaque-run regression. All 765 original base-class CL2 files (255 per
class; 78,016 directional frames) match native converted/blitted indices and
opacity. All 909 published Town/Cathedral atlas clips (96,672 cells) match native
pixels through the selected static palettes, including every padding pixel.
Both explicit comparisons pass without skips. Seven extra-source-frame cases
retain native animation-cycle metadata; 44 clips retain a wider native footprint.

The importer, existing protocol-test project and isolated SDK4.7.1 client build.
All 144 focused importer/catalog/selection/generic-pack cases pass without skips.
Synthetic atomic-publication checks use workspace-local TEMP because the sandbox
refuses directory rename in its default package temp directory. Original data and
generated art remain private. Both final native-pack Warrior/Sorcerer Journey/
Resume pairs pass with actual identity-bound Town/Cathedral standing, walking,
attack or lightning/fire clips, frame advance and unscaled bottom anchoring.
Repair/Recharge, exact pre-input cold state and next receipt/fresh host checks
also pass. Rendered scenes, complete native animation cursors and lighting parity
remain open. Independent review has no remaining findings.

Evidence: `.local/native-presentation-20261019/original-reference-footprint`,
`native-managed-pixel-comparison-final.log` and `focused-tests-final.log` under
that batch's `.local` root. Qualified sessions under the class profile roots:
Warrior `native-class-warrior-2e7ec8d2c70c40bd91768bf5dc3ff6ae`;
Sorcerer `native-class-sorcerer-c26b11b825a24677b82e148cda15c7f7`.
The isolated candidate excludes the preserved visibility/preview experiments;
headless draw evidence explicitly retains `RenderedParity=false`.

### 2026-10-19 — native class admission

Both canonical native class captures repeat byte-for-byte. Their two managed
full-world/RNG comparisons pass; the compatible Rogue fixture also passes.
The checked MSVC target, module/server projects and staged Godot client build.
All 43 focused C# cases pass: parser/factory/CLI, class-bound floor/restore,
route/settlement and launcher/evidence checks. Both staged positional contracts
pass. Legacy Rogue compilation matches all 82 retained profile files exactly.

The staged candidate's headless Warrior and Sorcerer Journey/Resume pairs pass
with Godot 4.7.2, excluding the preserved vision/preview experiments. They prove
generated kills/XP, natural buckler wear/Repair or staff charge/learned mana use
and Recharge, then exact pre-input player/items/grid/resources/world/RNG/cursor
restore, the next accepted receipt, distinct fresh hosts and terminal owned exits.
Independent review has no remaining findings. Full campaigns/rendering stay open.

Evidence: native `D:\Programming\Godevilution-evidence-native-classes-a0942ed598104a3fa7b39aaf373f743e`;
class profiles `.local/native-classes-20261019/{warrior,sorcerer}`;
Warrior session `native-class-warrior-0aeffeac50e54c4887ceafd519c31acf`;
Sorcerer session `native-class-sorcerer-2da1fdc7f0de433f8bc62dc9517dc986`.
Earlier working-tree bot diagnostics are not acceptance of the staged candidate.

### 2026-10-19 — session-flow consolidation

The server test project, protocol-test project and isolated Godot client build.
All 56 focused lifecycle/setup/boundary/model/launcher cases pass; the final
16-case setup run includes empty launcher-cleared hints, malformed metadata,
local/remote endpoint selection and resume bypass. Independent review has no
remaining static findings.

The actual headless `-PersistenceMode cohort -SessionMenu` Create/Resume pair
passes with Godot 4.7.2: ordinary controls, pending/bookmark refusal, profile-aware
setup, confirmed-token cancellation, joined scene exit, repeated current-model
event clearing, same-host rejoin and exact next-command fresh-host continuity.
Owned engine/client/host exits and typed parent evidence checks pass. The sandbox
blocked loopback sockets before identity; the approved unsandboxed run supplies
application evidence. The real form also exposed and corrected empty-hint refusal.

This is scoped session-flow acceptance in the existing working tree, not rendered
usability, a full campaign or normal cohort adoption. See the
[menu procedure](ai/godot-build.md#session-menu) and
[actual-control fixture](../godot-client/Devilution.Client/Acceptance/SessionAcceptance.Menu.cs).

### 2026-10-18 — repository directory rename

The Godot solution and protocol-test project build under `godot-client/` with
SDK 4.7.1 unchanged. Native authoritative `devilutionx`, `content_manifest_test`
and `cathedral_population_test` rebuild under `legacy-native/`; MSVC requires
the documented child-only `/Z7` workaround for the existing C1902 PDB error.
Client boundary tests pass 3/3, content-manifest tests 4/4 and the changed
repository-output path guard 1/1. Focused launcher/assembly checks pass 36/39;
the three existing positional-parameter/ProfileKind contract mismatches are
unchanged baseline/WIP drift, confirmed by independent comparison.

All 257 local Markdown links, 56 project references, 36 PowerShell parses and
three Python parses pass. Independent index comparison excludes gameplay WIP;
relocated WIP content is preserved. These are rename checks, not new gameplay
acceptance.

### 2026-10-18 — held entity attacks (`3af2a51cc`)

Negotiated native-depth clients can repeat ordinary entity attacks through the
Godot input path, with one outstanding action and server-ready pacing. The 15
focused lifecycle cases and clean committed-source client build pass. Headless
R12 produced three accepted held-attack pairs and then died on L3: no L3 kill,
living checkpoint, cold resume, or four-floor pass is established. See the
[native-depth acceptance wrapper](../tools/test-godot-native-depth-progression.ps1) and
[current delivery record](migration-status.md#current-delivery-position-and-verification-limits).

### 2026-10-18 — native-depth owners and resumable movement (`45c556a6c`)

The opt-in L1–L4 profile has lazy floor owners, versioned cohort state, and
native destination-walk continuation. Native timing/path checks, focused
managed compatibility cases, and a TCP cold-host mid-walk continuation pass.
These verify the backend and save boundary; the complete four-floor gameplay
journey remains unpassed. See [walk resume tests](../server/tests/Devilution.Server.Tests/NativeDepthWalkResumeTests.cs).

### 2026-10-18 — durable response and geometry costs (`8a1519984`, `365b3441f`, `132291d44`)

The opted-in host captures eligible command responses at the durable commit
boundary, reuses only exact in-flight duplicate snapshots, encodes checkpoint
payloads once, and reuses immutable geometry projections. Focused host/store,
cold-resume, fallback, and mutation cases pass. Passive-host measurements show
lower periodic publication cost after one-pass encoding; they are observational,
not a matched benchmark or a gameplay improvement claim. See
[cohort-store tests](../server/tests/Devilution.Server.Tests/AuthoritativeCohortStoreTests.cs)
and [clock-gated host tests](../server/tests/Devilution.Server.Tests/ClockGatedCohortSnapshotPollTests.cs).

### 2026-10-17 — clocked cohort fairness and client cadence (`0bc5cc5d1`, `909ab6d14`, `a766d0a41`)

Clock-only calibration is separate from full snapshots; command admission and
periodic work use fair authority turns; validated delivered snapshots inform
client polling cadence. Focused host/client regressions pass, including live
admission and retry boundaries. This does not guarantee every full-game command
will avoid rejection under combat load. See the relevant clock-gated host and
client protocol test classes: [host poll tests](../server/tests/Devilution.Server.Tests/ClockGatedCohortSnapshotPollTests.cs),
[clock-only client tests](../godot-client/Devilution.Client.Protocol.Tests/AuthoritativeClientClockOnlyProbeTests.cs),
and [client admission tests](../godot-client/Devilution.Client.Protocol.Tests/AuthoritativeClientSchedulingClockAdmissionTests.cs).

## Native Cathedral baselines

### 2026-10-16 — native Town/L1/L2 journey and cold resume (`17b114a54`)

Headless R24 earns and equips L1 gear, collects earned Gold, buys standard
healing potions, kills an L2 actor, saves in Town, and resumes through a fresh
host/client. The resumed player, RNG, cursor, and two-floor world match before
input; L1 gear and L1/L2 corpses remain on revisit, and the character returns
alive to Town. L2 produced no observed drop; no L2 loot is claimed. See the
[two-floor native route wrapper](../tools/test-godot-native-progression.ps1)
and [depth resume tests](../server/tests/Devilution.Server.Tests/NativeDepthWalkResumeTests.cs).

### 2026-10-14–15 — generated Cathedral object operations and retention

| Commit | Verified scope |
| --- | --- |
| `ef2935781` | Ordinary chest operation, loot/empty outcomes, repeat refusal, and fresh-host retention; native oracle and managed operation tests cover loading and live-animation branches. |
| `9ffa457a7` | Untrapped sarcophagus operation, loot/empty outcome, opened state, and cold continuation; the headless seed did not exercise linked-spawn emergence, which is covered by focused native/shared-owner tests. |
| `9ee01bd19` | Ordinary and explosive barrel interaction, collision/animation retention, and cold restore; the app run did not claim loot or blast-victim branches. |
| `6d756f268` | Actual Arrow-trap disarm interaction and door operation, one-shot firing/flight coverage, and retained cold state. |

Evidence is in the corresponding Cathedral operation/oracle test classes and
the [native-depth acceptance wrapper](../tools/test-godot-native-depth-progression.ps1).
These are scoped object families, not full native object parity.

## Authored and recovery verification

### 2026-10-13 — authored Cathedral loot, portal, and stash route (`ea5ecce98`, `778a5ae6b`)

The headless route buys and uses authored items, equips earned loot, opens its
server-owned portal, deposits/withdraws and re-equips stash items, and cold
resumes with the same authoritative state. A full-durability repair is refused
without changing the item or Gold. This is an authored progression slice, not
native generated population or campaign completion. See
[authored generated-depth guide](ai/godot-build.md#generated-cathedral-depths-playtest).

### 2026-10-07–12 — exported headless recovery and Performance (`265de3665`, `cff967af6`)

Committed-source exported Legacy runs verify graceful cold recovery and a
single-client Create/Resume workload, including the first admitted snapshot and
next command. They qualify those explicit exported workloads only; rendered
parity, distribution readiness, and the full platform/performance matrix remain
open. The explicit single-client PerformanceCreate/Resume pair at `c6c7f543b`
restores before input, admits the exact next move and exits both Release hosts
naturally. A separate exported one/four-client Performance lane has optimized
runtime-image and graceful Release-host shutdown coverage; it does not establish
cold restore of those Performance profiles, GPU/whole-frame costs or release
budgets. See [Godot build and export notes](ai/godot-build.md).

### 2026-10-07 — portal counter/ownership recovery (`afeada4ac`, `8383fc7b4`)

Focused TCP cases preserve tick-derived portal opening counters, canonical
command receipts, owner/guest use and owner-return closure across fresh hosts.
The two-client case rejoins in reverse order. Forced stops exercise restart;
these cases do not establish graceful shutdown, rendered controls or a full
native campaign. See [counter restart tests](../godot-client/Devilution.Client.Protocol.Tests/LiveAuthoritativeServerTests.TownPortalLifetimeRestart.cs)
and [cohort portal tests](../godot-client/Devilution.Client.Protocol.Tests/LiveAuthoritativeServerTests.CohortTownPortalRestart.cs).

### 2026-10-04 — two-client authored combat and cohort resume (`5e8389c03`)

A scoped two-client Godot/server journey exercises authored combat and loot,
then restores the cohort through a fresh host while preserving participant
identity and state. This verifies that scenario’s multiplayer boundary, not a
general multiplayer campaign or long-running soak. See the
[two-client recovery acceptance](../tools/test-godot-session.ps1).

### 2026-10-02 — complete-cohort recovery and Windows staging

`915a4f0b9` integrated complete simulation-cohort capture/restore;
`60f60c8a1` added Godot cohort recovery acceptance; `9da79c159` hardened
cohort initialization and Windows checkpoint staging. Focused tests cover
atomic publication, interrupted first publication, durable command receipts,
and fresh-process restoration. These results do not qualify every filesystem or power-loss scenario.
Relevant fixtures: [complete-cohort recovery](../server/tests/Devilution.Server.Tests/CompleteCohortRecoveryTests.cs)
and [publication failure coverage](../server/tests/Devilution.Server.Tests/CompleteCohortFailureTests.cs).

## Material open limits

- The four-floor native-depth gameplay gate is open. R12 verifies held input but
  ends in L3 death; it does not prove an L3 kill or living checkpoint. Do not
  combine backend/cold-walk tests with the passed R24 L1/L2 route into a claimed
  L1–L4 journey.
- Native seed-driven Catacombs/Caves/Hell and Hellfire Hive/Crypt generation and
  population are not established by the Cathedral generator or supplied-map
  loaders.
- Native initial Town stock/RNG parity, broad quest/set-level progression,
  remaining generated object families, and full multiplayer campaign behavior
  remain incomplete or unqualified.
- Full native/Godot rendered parity, normal-profile adoption, all-class campaign
  play, and release/platform qualification remain open. See the
  [completion plan](completion-plan.md) for acceptance gates.

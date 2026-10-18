# Verification history

This is a curated record of accepted, scoped verification and material open
limits. It is not a release-readiness claim or a replacement for the current
[migration status](migration-status.md) and [feature inventory](feature-gap-inventory.md).
Commit IDs identify the source change; a pass applies only to the scope stated
here. Git history preserves older document versions; detailed assertions remain in
the referenced test sources. Do not copy raw outputs into historical archives.

## Client and host verification

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
[clock-only client tests](../godot/Devilution.Client.Protocol.Tests/AuthoritativeClientClockOnlyProbeTests.cs),
and [client admission tests](../godot/Devilution.Client.Protocol.Tests/AuthoritativeClientSchedulingClockAdmissionTests.cs).

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
native campaign. See [counter restart tests](../godot/Devilution.Client.Protocol.Tests/LiveAuthoritativeServerTests.TownPortalLifetimeRestart.cs)
and [cohort portal tests](../godot/Devilution.Client.Protocol.Tests/LiveAuthoritativeServerTests.CohortTownPortalRestart.cs).

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

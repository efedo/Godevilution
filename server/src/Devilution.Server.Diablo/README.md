# Diablo Server Adapter

`Devilution.Server.Diablo` bridges the transport-neutral `Devilution.Diablo`
simulation to the generic authoritative host and shared Protobuf contract. It
owns command/result mapping, wire snapshots and events, compatible version-1
saves and strict generated-world version-2/version-3 saves, replay compatibility
helpers, and construction of the current protocol executor. Snapshot projection
rejects duplicate or zero entity IDs across the
server-owned entity collections; identity-neutral version-1 saves retain their
zero player ID until import rebinds it.

The separate `DiabloCohortCodec` encodes complete format-1 module checkpoints,
with additive formats for Cathedral doors (2), native monsters (3), item pools (4),
combat continuation (5), sarcophagi (6), barrels (7), traps (8), native Town/L1/L2
progression (9) and native L1-L4 depth progression (10). The opt-in native
profile retains bow flight and collision-time hit/damage resolution, frozen
future-tile aim, and melee, recovery and walking phases. Older cohort formats
retain their established encoding and legacy movement behavior.

The separate explicit `devilution.diablo.cathedral-native-progression.v1`
adapter admits the canonical Rogue Town/L1/L2 profile. Cohort v9 binds its run
identity, first-entry hero/unique-history contexts and initial skeleton phases.
Cold activation regenerates trusted floor owners before installing the complete
saved world, player and RNG. Wrong profiles and schema downconversion refuse;
legacy adapters and cohort formats keep their existing behavior.

The separate `devilution.diablo.cathedral-native-depth-progression.v1` profile
uses cohort v10 for lazy L1-L4 owners, native destination-path continuation and
pending ranged follow-ups. Backend support is not a completed four-floor
application journey.

Raw native door trap markers remain preserved and are projected when visible;
unsupported native door operations are refused before they can mutate door or
pool state. For opt-in native L1, public static collision geometry includes solid
raw object-reference footprints, matching authoritative occupancy; non-native
scanner-light projection is unchanged.

The scoped Godot native-world Rogue Town/L1/L2 journey, fresh-host restore and
retained two-floor revisit pass. See [test history](../../../docs/test-history.md#native-cathedral-baselines)
for accepted scope. Complete L1-L4 play, native compatibility-client GUI acceptance,
normal-profile adoption and broader campaign acceptance remain open.

The cohort codec requires every stored field, rejects unknown/duplicate members and malformed
Unicode, validates typed graphs and deterministic collection order, and preserves
nullable fields and raw values without constructor normalization. Fresh module
candidate validation is still required after decoding. Host integrity envelopes,
leases, generations and command ledgers remain separate. `StoreSimulationExecutor`
now exposes optional complete-cohort capture and fresh Prepare/Commit installation
through one leased active simulation/runtime holder. Trusted activation options
create the detached candidate; prepared metadata comes from actual recapture.
Restored runtimes admit explicitly bound participants only. The opt-in host uses
this codec during startup/publication; legacy constructors and saves remain unchanged.

Game rules and state do not belong here; they remain in `Devilution.Diablo`.
Generic transport, session, and filesystem behavior remains in
`Devilution.Server.Host`. The composition executable references this adapter
without naming Diablo types in `Program.cs`.

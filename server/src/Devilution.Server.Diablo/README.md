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
with additive format-2 Cathedral doors, format-3 native monster state, format-4
native item pools and format-5 native combat continuation. The opt-in native
profile retains bow flight and collision-time hit/damage resolution, frozen
future-tile aim, and melee, recovery and walking phases. Older cohort formats
retain their established encoding and legacy movement behavior.

Raw native door trap markers remain preserved and are projected when visible;
unsupported native door operations are refused before they can mutate door or
pool state. For the opt-in native L1, public static collision geometry also
includes solid raw object-reference footprints, matching authoritative
occupancy; non-native scanner-light projection is unchanged.

The scoped headless native Rogue L1 Journey and fresh-host Resume R3 pass with
a Town checkpoint at sequence 103 and first resumed accepted command 104.
Normal-profile adoption and broader campaign acceptance remain open.

It requires every stored field, rejects unknown/duplicate members and malformed
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

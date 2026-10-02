# Diablo Server Adapter

`Devilution.Server.Diablo` bridges the transport-neutral `Devilution.Diablo`
simulation to the generic authoritative host and shared Protobuf contract. It
owns command/result mapping, wire snapshots and events, compatible version-1
saves and strict generated-world version-2/version-3 saves, replay compatibility
helpers, and construction of the current protocol executor. Snapshot projection
rejects duplicate or zero entity IDs across the
server-owned entity collections; identity-neutral version-1 saves retain their
zero player ID until import rebinds it.

The separate `DiabloCohortCodec` encodes complete format-1 module checkpoints.
It requires every stored field, rejects unknown/duplicate members and malformed
Unicode, validates typed graphs and deterministic collection order, and preserves
nullable fields and raw values without constructor normalization. Fresh module
candidate validation is still required after decoding. Host integrity envelopes,
leases, generations and command ledgers remain separate. This codec is not yet
wired into production startup or publication and does not change legacy saves.

Game rules and state do not belong here; they remain in `Devilution.Diablo`.
Generic transport, session, and filesystem behavior remains in
`Devilution.Server.Host`. The composition executable references this adapter
without naming Diablo types in `Program.cs`.

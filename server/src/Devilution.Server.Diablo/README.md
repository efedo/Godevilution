# Diablo Server Adapter

`Devilution.Server.Diablo` bridges the transport-neutral `Devilution.Diablo`
simulation to the generic authoritative host and shared Protobuf contract. It
owns command/result mapping, wire snapshots and events, compatible version-1
saves, replay compatibility helpers, and construction of the current protocol
executor. Snapshot projection rejects duplicate or zero entity IDs across the
server-owned entity collections; identity-neutral version-1 saves retain their
zero player ID until import rebinds it.

Game rules and state do not belong here; they remain in `Devilution.Diablo`.
Generic transport, session, and filesystem behavior remains in
`Devilution.Server.Host`. The composition executable references this adapter
without naming Diablo types in `Program.cs`.

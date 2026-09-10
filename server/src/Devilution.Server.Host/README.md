# Generic Authoritative Host

`Devilution.Server.Host` owns protocol-facing infrastructure shared by any
authoritative game adapter: TCP sessions, command admission and deduplication,
wall-clock scheduling, snapshots and events, save-provider contracts,
filesystem persistence, adapter selection, per-operation ruleset-generation
leases, live ruleset replacement, complete session-state migration through the
save contract,
handshake identity publication, explicit verified module packages, and
hash-verified `game-host-adapter.json` package discovery. Adapter packages are
restricted to one assembly, canonical relative paths, shared Godevilution
contracts, and public parameterless factory types before they enter the
deterministic adapter catalog. The
module-package loader rejects malformed UTF-8 manifests, reparse-point
entries, and noncanonical assembly paths before hashing or loading an assembly.
Server-owned save envelopes, combined session checkpoints, command ledgers, and
world-seed files use strict UTF-8 I/O and fail closed on malformed text during
recovery. Combined session checkpoints are version 3 and carry canonical
SHA-256 hashes over serialized authoritative state and the command ledger;
version-1 and version-2 checkpoints remain readable and are upgraded on their
next write. The leased adapter wrapper uses a read/write operation barrier:
replacement waits
for in-flight operations, migrates initialized sessions before publication,
and disposes drained game targets through an injected host policy.
It exposes the active combined ruleset identity and the number of retired
generations still awaiting disposal so operators can verify replacement health
without inspecting game-specific state.
The wrapper is itself disposable; server shutdown drains any remaining retired
generation, disposes the active target, and releases its operation barrier.

It targets .NET 8 and depends on Protocol, Domain, Data, and Engine. It must not
reference `Devilution.Diablo` or any other game module. The .NET 10 executable
supplies concrete content, module candidates, and one compatible adapter.

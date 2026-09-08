# Generic Authoritative Host

`Devilution.Server.Host` owns protocol-facing infrastructure shared by any
authoritative game adapter: TCP sessions, command admission and deduplication,
wall-clock scheduling, snapshots and events, save-provider contracts,
filesystem persistence, adapter selection, per-operation ruleset-generation
leases, live ruleset replacement, complete session-state migration through the
save contract,
handshake identity publication, and explicit verified module packages. The
module-package loader rejects malformed UTF-8 manifests, reparse-point
entries, and noncanonical assembly paths before hashing or loading an assembly.
leased adapter wrapper uses a read/write operation barrier: replacement waits
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

# Generic Authoritative Host

`Devilution.Server.Host` owns protocol-facing infrastructure shared by any
authoritative game adapter: TCP sessions, command admission and deduplication,
wall-clock scheduling, snapshots and events, save-provider contracts,
filesystem persistence, adapter selection, per-operation ruleset-generation
leases, live ruleset replacement, complete session-state migration through the
save contract,
handshake identity publication, explicit verified module packages, and
hash-verified `game-host-adapter.json` package discovery. Adapter packages use
canonical relative paths and hash-verified entry assemblies plus optional
private dependency assemblies; every private reference must resolve inside the
declared package closure, while Godevilution contracts remain shared with the
host. Public parameterless factory types are validated before they enter the
deterministic adapter catalog. The
module-package loader rejects malformed UTF-8 manifests, reparse-point
entries, and noncanonical assembly paths before hashing or loading an assembly.
Server-owned save envelopes, combined session checkpoints, command ledgers, and
world-seed files use strict UTF-8 I/O and fail closed on malformed text during
recovery. Combined session checkpoints are version 4 and carry canonical
SHA-256 hashes over serialized authoritative state and the command ledger plus
the active ruleset identity; restart recovery rejects a current-format
checkpoint written for a different ruleset before importing gameplay state.
When multiple session ledgers are restored, receipt identities are also
validated globally so a duplicate server receipt cannot be admitted across
sessions.
Version-1 through version-3 checkpoints remain readable and are upgraded on
their next write. The leased adapter wrapper uses a read/write operation barrier:
replacement waits
for in-flight operations, migrates initialized sessions and prepares every
session export before publishing their new-identity checkpoints during
candidate preparation. This all-session export phase runs before activation or
handshake publication, and the batch persistence transaction restores earlier
session checkpoints if a later combined or compatibility write fails. A failed
candidate export or batch write therefore leaves the prior generation active;
successful replacement then disposes drained game targets through an injected
host policy.
It exposes the active combined ruleset identity and the number of retired
generations still awaiting disposal so operators can verify replacement health
without inspecting game-specific state.
The wrapper is itself disposable; server shutdown drains any remaining retired
generation, disposes the active target, and releases its operation barrier.

It targets .NET 8 and depends on Protocol, Domain, Data, and Engine. It must not
reference `Devilution.Diablo` or any other game module. The .NET 10 executable
supplies concrete content, module candidates, and one compatible adapter.

## Opt-in performance evidence

The standalone host accepts `--performance-output <new-file>`. It reserves a
new file before listening and never overwrites existing evidence. After warming
up the intended participants, issue the local operator command `profile-start`,
then `quit` to emit JSON. Repeated starts cannot reset a recording. A graceful
quit without a start writes `Status: NotStarted`; a killed process may leave an
incomplete file, which is not valid evidence. No recording is allocated by default.

Measurements describe completed operations, not a single summed tick cost:

- Periodic cycle: authoritative advance plus per-session checkpoints; it can
  advance multiple ticks and excludes the scheduler wait.
- Periodic advancement: `AdvanceTo` only, including its authoritative lock wait.
- Session checkpoint lock wait: acquiring the authoritative monitor for one
  `SaveSession` call; a command already holding it enters recursively. This is
  not the command's initial lock wait.
- Session capture: state export and completed-ledger cloning under that monitor.
- Combined checkpoint, compatibility state, and compatibility receipt publication:
  separate synchronous store calls, including validation, serialization, disk
  flush, and replacement when needed. These phases cover ordinary `SaveSession`
  calls, not ruleset-reload `SaveBatch`; phase sample counts need not match.
- Command admission/apply/checkpoint: includes authoritative lock wait, but not
  network publication or initial command-batch validation.
- Snapshot materialization/validation: includes lock wait and any advance.
- Snapshot write: serialization and socket write outside the authoritative lock.
- Snapshot envelope payload bytes: exact protobuf envelope size, excluding the
  four-byte transport prefix, IP/TCP overhead, and non-snapshot messages.

Each metric retains at most 20,000 samples with an explicit dropped count.
Mean, nearest-rank p50/p95, and maximum apply only to retained samples. Failed or
unfinished operations are not success samples. Metrics contain no session IDs,
credentials, commands, or game state. Final process working set/private bytes
and process-lifetime peak working set are observations, not leak/soak proof.
Final and startup identity hashes expose an identity change during profiling;
use a stable ruleset for comparable baselines. Timings overlap and must not be
added together. The reproducible workload is documented in
[Godot build notes](../../../docs/ai/godot-build.md#standalone-host-performance-baseline).

On Windows, an unchanged compatibility receipt file is compared byte-for-byte
under the store's per-path lock, then flushed to disk without replacement. The
open handle excludes writers and pathname replacement through comparison/flush.
No cached hash or receipt count substitutes for reading the actual file. Missing,
different, or corrupt mirrors use the original write-through atomic replacement;
an equality-path flush failure propagates. Other platforms retain unconditional
replacement until equivalent pathname protection is verified. Combined and raw
state checkpoints, compatibility formats, publication order, and durable-before-
command-acknowledgement behavior are unchanged.

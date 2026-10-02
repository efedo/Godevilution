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

## Complete-cohort recovery

`AuthoritativeCommandServer.CaptureCohortState` detaches every existing ledger
and its exact admission/receipt cursors under the command monitor.
`InitializeCohortState` prevalidates the whole set before one-shot installation
into a fresh server, without invoking gameplay. The legacy direct Immediate
API can produce a future-applied receipt before advancing its admission clock;
the command-only view preserves that intermediate state. Complete host
publication must first settle the module/world boundary. The existing TCP
Immediate path already advances to the greatest applied tick under the same
monitor before persistence; gated publication occurs when work is due.

The separate format-1 `AuthoritativeCohortStore` owns one canonical file and an
exclusive lifetime save-root lease. Its integrity envelope fences host identity,
complete roster bindings/ledgers, clocks, cursors and an opaque module payload.
Publication requires the next generation, forward-only history and stable
participants/receipts; zero exhaustion remains exact. Default identity changes
reject. A trusted, explicitly fenced reload publication can change content,
ruleset and module format only after matching the previous identity, while
preserving module ID, protocol, mode and tick rate. Caller-owned module/owner
validation is still required before publication.

The standalone executable explicitly selects this mode with
`--persistence-mode cohort --save-root <dedicated-directory>`. Legacy remains
the default; existing saves are never merged, reset or migrated implicitly.
Acquire the root lease before seed writes. Refuse unknown/legacy files in a
new cohort root and a missing original seed in an existing CLI cohort root.
Never fall back to per-session imports after canonical corruption.

The optional `IAuthoritativeGameCohortAdapter` supplies detached capture and
fresh, one-shot Prepare/Commit restoration. The leased facade checks the actual
adapter capability, owns immutable prepared bindings and serializes token
installation/disposal. Restore the complete module, offline roster, all ledgers,
exact cursors and clock before listening; raw resume tokens remain ephemeral.
Admission, snapshot materialization and durable publication share the command
barrier before Hello/ACK/snapshot exposure. Live mutation/publication failures
fence transports, admission, advancement, reload and shutdown republishing.
The optional synchronous scheduler failure callback cancels due peers and final
advancement before an executor fault can escape; legacy callback behavior and
constructor signatures remain unchanged.

Healthy shutdown quiesces owned work, publishes settled state, then releases
adapters and the root lease. Complete-candidate reload publishes the new identity
before activation; changed or uncertain durable outcomes fence the host, while
failed preparation may leave the old activation healthy only with verified old
durable state. Cleanup must not replace the primary failure. Normal-launch
adoption, broader fault/reload checks and application acceptance remain M0 work;
legacy per-session multiplayer overwrite remains a separate compatibility gap.

## Negotiated clock-gated scheduling

The standalone host accepts `--command-scheduling immediate|clock-gated`.
Immediate is the compatibility default; `AuthoritativeCommandServer` keeps its
existing direct API. Godot launchers explicitly select clock-gated timing in
both single-player and multiplayer. The policy applies to the whole host, not
individual participants. Clock-gated hosts require `ClientHello` capability
support before loading receipts, restoring state or initializing a participant.
The native paired launcher also selects clock-gated scheduling. The standalone
CLI and default low-level native configuration retain Immediate compatibility;
native GUI timing acceptance remains distinct from source and helper checks.

Two loopback characterization cases demonstrate that the legacy immediate host
can advance from physical tick 111 to a peer's requested tick 125, rejecting
another client's tick-120 input; without that peer, tick 120 is accepted. This
establishes a mechanism, not proof of the retained application's exact cause.

The host retains future intents until their due boundary, preserves batch wire
order, and drains due work before later admission, autonomous advancement,
handshake, event or snapshot publication. Physical time is sampled under the
authoritative monitor. Receipt allocation and strict late-command policy remain in
`AuthoritativeCommandServer`. A configured publication callback must checkpoint
state and receipts before successful completion; queued intents themselves are
not persisted. Connection loss cannot cancel received intents; host cancellation
ends unresolved work. Retained slots, including duplicate waiters, are bounded.
The default limit is 1,024 retained wire slots, including duplicate waiters.
Capacity refusal is an explicit protocol error, not `TooLate`, and allocates no
new receipt. A resumed hello includes both completed and pending sequence
high-water marks so a fresh process cannot reuse an already admitted identity.
Publication callbacks cannot re-enter submission, advancement or disposal;
explicit cancellation and read-only queries remain available.

Reload and legacy cold persisted restore drain due work, then refuse mutation while
any future command remains queued. In-memory reconnect is allowed; refused cold
restores do not cache an old-generation checkpoint. This fence does not solve
the legacy per-session shared-world checkpoint replacement problem. Cohort mode
uses complete pre-listen recovery rather than participant-world replacement.
Host shutdown cancels pending waits, closes owned transports and awaits handlers
before disposing the adapter. Background `Completion` faults promptly;
`DisposeAsync` completes cleanup before rethrowing retained failures. Isolated
session publication failures are reported and
receive no ACK. Arbitrary non-cooperative adapter callbacks are not bounded.

Godot idle snapshot requests carry an optional timing correlation; the host
replies only after the snapshot. This measures end-to-end state freshness, not
pure network RTT. The client excludes deliberate command waits from its timing
estimate and delays retry until remaining scheduled wait plus normal timeout.
Retries retain their original payload/tick/sequence. Godot requires a correlated,
unexpired current-connection sample before its first realtime dispatch. Measured
history cannot bound unseen latency spikes. Actual two-client restart acceptance
remains distinct from these rules.

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
- Periodic advancement: authoritative lock wait and advance; gated mode also
  includes due command execution and its checkpoints.
- Session checkpoint lock wait: acquiring the authoritative monitor for one
  `SaveSession` call; a command already holding it enters recursively. This is
  not the command's initial lock wait.
- Session capture: state export and completed-ledger cloning under that monitor.
- Combined checkpoint, compatibility state, and compatibility receipt publication:
  separate synchronous store calls, including validation, serialization, disk
  flush, and replacement when needed. These phases cover ordinary `SaveSession`
  calls, not ruleset-reload `SaveBatch`; phase sample counts need not match.
- Immediate command admission/apply/checkpoint: includes authoritative lock wait,
  but not network publication or initial command-batch validation.
- Gated queue admission: synchronous admission/drain cost, excluding deliberate
  future ACK wait. Scheduled checkpoint: result validation and owner-session
  publication, excluding command execution. These overlap the draining operation.
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

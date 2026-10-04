# Devilution.Engine

`Devilution.Engine` is the transport-neutral composition layer for the C#
authoritative engine. It targets .NET 8, references only `Devilution.Domain` and
`Devilution.Data`, and has no dependency on a game module, server host,
Protobuf, Godot, persistence, or operating-system services.

The engine currently owns:

- stable gameplay-module identity and deterministic dependency ordering;
- semantic Engine API compatibility ranges and pre-registration diagnostics;
- explicit host-supplied candidate inspection without reflection or filesystem
  loading inside Engine;
- typed capability registration with per-module transactional publication;
- validated content and authoritative-clock module context;
- ordered content-plus-module ruleset identity;
- transactional candidate preparation hooks so host-owned session state can be
  validated against a candidate before publication;
- generation leases and explicit retired-activation draining so replaced
  targets remain alive until in-flight session operations release them;
- capability lookup used by hosts to construct a game simulation;
- deterministic generic session/state coordination, reverse participant lookup,
  and per-session transient-event queues;
- bounded deterministic grid navigation with caller-supplied walkability and
  occupancy and goal predicates, plus a stable neighbor sequence, so game
  modules do not own reusable path-search logic;
- native-compatible bounded grid crawl ordering and overflow-safe nearest-cell
  search, exposed independently from Diablo's compatibility facade;
- the native-compatible deterministic legacy LCG, exposed as a reusable
  primitive with compatibility-shaped seed and bounded-roll aliases and used
  directly by Diablo authoritative callers; the module retains its
  source-compatible `DiabloRandom` and `AuthoritativeLegacyRandom` facades;
- native-compatible signed 26.6 fixed-point arithmetic, exposed as a reusable
  primitive and used directly by Diablo authoritative callers while the module
  retains its source-compatible `DiabloFixedPointQ6` facade;
- overflow-safe Chebyshev grid-distance metrics shared by bounded navigation
  and authoritative gameplay, while Diablo retains its compatibility entry
  point;
- deterministic overflow-safe Bresenham line traversal shared by movement and
  missile line-of-sight checks, with a bounded step limit for malformed inputs;
- native-compatible eight-way direction sectors, coordinate-to-facing
  selection, rotation, and adjacent-cell displacement, including the stable
  direction-indexed sequence consumed by Diablo combat;
- native alternating and turn-pair direction candidate ordering used by
  movement AI, preserving the native RandomWalk sweep sequences;
- native RoundWalk primary/fallback, straight-and-lateral, and clockwise
  direction candidate ordering used by Diablo movement controllers;
- native-compatible rotated-grid projectile velocity construction with the
  legacy fixed-point scaling contract;
- deterministic procedural grid generation with connected corridor topology,
  independently seeded missile occlusion and native vision blockers, stable
  cell ordering, and explicit required-walkable entry cells for authored spawn
  points;
- native-compatible radial projectile fan-out ordering with deterministic
  mirrored quarter-ring truncation;
- stable 32-bit mixing and aligned walkable-footprint selection for authored
  deterministic placement;
- native-compatible Fisher–Yates ordering with caller-owned bounded random
  sources for deterministic spawn and content selection;
- deterministic non-zero entity-ID allocation, including caller-owned
  collision skipping and stateless first-available lookup, exposed as a
  reusable primitive and used directly by the server host while the host
  retains its source-compatible allocator facade;
- serialized participant operations, exactly-once deterministic tick advancement,
  module-supplied lifecycle hooks, directed event routing, and complete ordered
  participant capture under the same runtime barrier.

The first-party `Devilution.Diablo` module depends on this assembly and
registers its store-rule and simulation-factory capabilities. The server host
loads modules and consumes those capabilities; it must not add transport or
Diablo-specific contracts to the engine registry.

`AuthoritativeSimulationRuntime.CaptureCohort` captures the exact runtime clock
and all existing session bindings without creating participants, advancing time
or draining events. Module callbacks must detach their checkpoint data inside
the barrier. `InitializeCohort` installs caller-created state and the exact clock
once into a fresh, unexposed runtime; it prevalidates every binding without
calling the lifecycle factory. It does not roll back mutable state in a live
module. `DeterministicEntityIdAllocator.NextId` and `RestoreNextId` preserve
allocation high-water marks, including zero exhaustion; modules must validate
restored cursors against their complete identity graph. These primitives alone
do not implement host checkpoint publication or multiplayer recovery.

`ExecuteTick` is an explicit ordered-command API, not a change to legacy
`Execute`/`AdvanceTo`. Its lifecycle capability supplies one synchronous command
slot per actor in ordinal session order. Commands retain per-actor input order;
results retain whole-batch input order. The module merges callback events at
their actual slot into one chronological directed-event stream. Semantic
deduplication and routing input preparation versus primary actions belong to
the host/module, not delegates or session IDs.

The API validates/detaches the batch before mutable hooks, catches up through
tick-1 using existing hooks, then creates new actors in first input appearance
order. Tick zero and closed ticks refuse commands. Participant lists are
read-only; foreign/repeated/omitted/out-of-order, cross-thread or expired
callbacks are refused. Nested runtime access is forbidden during execution.
A potentially mutating hook fault or caught contract violation poisons the
runtime: execution, capture and event publication then refuse reuse. This is
fail-stop behavior, not state/RNG rollback; only the clock diagnostic remains
readable. Rebuild from a known checkpoint. Default hosts do not select this API.

Hosts provide the trusted candidate set. `GameplayModuleRegistry.Inspect`
returns the deterministic load order and actionable diagnostics for duplicate
IDs, missing dependencies, dependency cycles, and incompatible Engine API
ranges. `Load` performs the same preflight before any module registration and
retains transactional capability publication for each compatible module.
Engine performs no assembly discovery, hashing, filesystem access, or
reflection. The standalone Server may turn explicitly selected, verified
packages into candidates before calling this API; other hosts can implement a
different installation policy without changing Engine.

Run the independent xUnit v3 suite from the repository root:

```powershell
dotnet run --project server/tests/Devilution.Engine.Tests/Devilution.Engine.Tests.csproj
```

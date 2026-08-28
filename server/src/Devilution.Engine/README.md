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
  occupancy predicates, so game modules do not own reusable path-search logic;
- the native-compatible deterministic legacy LCG, exposed as a reusable
  primitive while Diablo retains its source-compatible `DiabloRandom` facade;
- native-compatible signed 26.6 fixed-point arithmetic, exposed as a reusable
  primitive while Diablo retains its source-compatible `DiabloFixedPointQ6`
  facade;
- overflow-safe Chebyshev grid-distance metrics shared by bounded navigation
  and authoritative gameplay, while Diablo retains its compatibility entry
  point;
- deterministic overflow-safe Bresenham line traversal shared by movement and
  missile line-of-sight checks, with a bounded step limit for malformed inputs;
- deterministic non-zero entity-ID allocation, exposed as a reusable
  primitive while the server host retains its source-compatible allocator
  facade;
- serialized participant operations, exactly-once deterministic tick advancement,
  module-supplied lifecycle hooks, and directed event routing.

The first-party `Devilution.Diablo` module depends on this assembly and
registers its store-rule and simulation-factory capabilities. The server host
loads modules and consumes those capabilities; it must not add transport or
Diablo-specific contracts to the engine registry.

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

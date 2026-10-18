# Repository Organization

Godevilution contains three runtime surfaces plus shared contracts and tests.

| Path | Responsibility |
| --- | --- |
| `Source/` | Native DevilutionX-derived engine and optional server-backed client adapter |
| `server/src/Devilution.Domain/` | Dependency-free .NET 8 deterministic domain kernel |
| `server/src/Devilution.Data/` | .NET 8 validated content runtime depending only on Domain |
| `server/src/Devilution.Engine/` | .NET 8 transport-neutral module composition and capability runtime depending only on Domain and Data |
| `server/src/Devilution.Diablo/` | .NET 8 first-party game module depending only on Domain, Data, and Engine |
| `server/src/Devilution.Server.Host/` | .NET 8 generic protocol host, session lifecycle, module loader, and filesystem persistence |
| `server/src/Devilution.Server.Diablo/` | .NET 8 Diablo-to-host/Protobuf adapter, compatible saves, and replay support |
| `server/src/Devilution.Server/` | .NET 10 composition-only authoritative executable |
| `server/content/` | Validated external authoritative content packs |
| `godot/Devilution.Client/` | Godot 4.7.x Mono presentation project; source SDK pinned to 4.7.1 |
| `godot/Devilution.Client.Protocol/` | Engine-independent C# protocol transport and client model |
| `protocol/Devilution.Protocol/` | Single generated .NET 8 Protobuf binding assembly |
| `protocol/` | Canonical Protobuf schema and language-neutral delivery vectors |
| `test/fixtures/replay/` | Shared native/C# deterministic replay fixtures |
| `test/` | Native GTest suites and support |
| `docs/` | Current architecture, status, contributor, and migration documentation |

## Native Source

Native source is organized under `Source/` by architectural domain. Major
directories include `application`, `controls`, `data`, `engine`, `game`, `lua`,
`network`, `persistence`, `platform`, `qol`, `tables`, `ui`, and `utils`.
`Source/network/authoritative` contains the opt-in server-backed adapter, while
`Source/replay` and the domain projection directories support cross-language
parity. Native gameplay remains the compatibility baseline during migration.

Native gameplay headers under `Source/game` use `.hpp`; most other native
domains use `.h`. Includes are rooted at `Source`, for example
`#include "game/items/items.hpp"`.

## C# Server

The composition executable targets .NET 10 and references the .NET 8 Data,
Engine, generic Host, and Diablo adapter libraries. Its only source file is
`Program.cs`. `Devilution.Protocol` generates the C# schema once and has no
Godevilution assembly dependencies. `Devilution.Server.Host` depends only on
Protocol, Domain, Data, and Engine; `Devilution.Server.Diablo` depends on those
generic layers plus the transport-neutral Diablo module. Data depends only on
Domain; Domain has no project or package dependencies. Engine depends only on
Domain and Data and owns module identity, dependency ordering, typed
capabilities, authoritative clock context, ruleset identity, semantic API
compatibility and module dependency-version preflight, generic deterministic
session/state coordination, atomic candidate registration, coordinated ruleset
publication, serialized participant access, exactly-once tick advancement, and
event routing. Diablo depends only on Domain, Data, and
Engine and owns game-specific catalogs, player and world state,
transport-neutral gameplay events, and deterministic store,
inventory, movement, portal, pickup, object, and quest transitions without a
server, protocol, or presentation dependency. It also owns attack and cast
intents, damage and progression resolution, projectiles, status advancement,
deterministic autonomous monster ticks, and the aggregate constructed by its
registered simulation-factory capability. Diablo also captures and validates
protocol-neutral player/world/store snapshots. Generic hosting and filesystem
persistence live in `Devilution.Server.Host`; mapping, replay compatibility,
and the version-1 save codec live in `Devilution.Server.Diablo`. Deterministic
lifecycle remains in Engine, while Host supplies wall-clock scheduling. Data
now composes explicit
composite-key overlays transactionally, and Engine inspects host-supplied module
candidates before registration. Host now turns explicitly selected,
hash-verified single-assembly packages into those candidates without adding
filesystem or reflection dependencies to Engine. The generic adapter catalog
and combined command, tick, snapshot, event, and save contract keep the
executable composition root free of Diablo symbols. Assembly-boundary tests
prevent Protocol, Host, the Diablo adapter, and the executable from collapsing
back together. New work can broaden game-content schemas while keeping the
wire, current save, and replay contracts stable.

## Godot Client

The protocol library must remain independent of Godot so it can be tested with
ordinary .NET tooling. Godot scripts adapt snapshots and events into scenes,
input, rendering, audio, and UI; they must not become a second gameplay engine.

See `docs/csharp-authoritative-server-migration.md` for ownership rules and
`docs/migration-status.md` for current delivery scope and constraints. The short
execution order is in `docs/work-plan.md`, feature completion in
`docs/feature-gap-inventory.md`, and milestone gates in `docs/completion-plan.md`.
This layout is
not a list of architecture work that still needs to be implemented.

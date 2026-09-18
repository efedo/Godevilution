# C# Authoritative Server

`Devilution.Server` is the .NET 10 composition executable for authoritative
gameplay. Runtime responsibilities are physically separated into .NET 8
libraries:

- `Devilution.Server.Host` owns generic TCP sessions, command admission,
  clocks, adapter selection, module-package loading, snapshot hashing, and
  filesystem persistence without depending on the Diablo module.
- `Devilution.Server.Diablo` owns the Diablo-to-Protobuf adapter, compatible
  save codec and replay support, and the current protocol executor.
- `Devilution.Protocol` is the only managed project that generates bindings
  from `protocol/devilution.proto`; both Server and Godot reference it.
- `Devilution.Server` loads content and composes Engine, Host, and the selected
  built-in adapter. Its only C# source file is `Program.cs`.

The implementation already covers the migration's playable vertical slice:
stores and inventory, player resources and attributes, movement and occupancy,
combat and spells, monsters and drops, objects and quests, portals, world-item
pickup, player classes, death and restart-in-town transitions, exact Q6 monster
health, status expiry, snapshots,
replay fixtures, and
reconnectable sessions.
The exact spell subset currently includes content-driven native Firebolt mana,
damage, movement, hit, resistance, and impact behavior; Lightning's moving
controller, persistent segments, repeated collision, Q6 resistance, and PvP
rules; Flash's two repeated-collision layers, seeded scaling, blocker/object
rules, and native invulnerability window; Healing's native level rolls, Q6 class
modifiers, life cap, and full-life resource consumption; Heal Other's stable
living-player targeting, distinct class modifier, exact Q6 result, and valid
full-life consumption; Teleport's native
nearest-tile crawl, fizzle semantics, delayed authoritative movement, and
participant occupancy; Mana Shield's native damage-to-mana conversion,
depletion reversal, and active-cast fizzle; Stone Curse's native crawl,
content-owned immunity, timed petrification, non-refreshing recast, and shatter
phase; Infravision's native duration growth, exact consumable sources, typed
status lifecycle, and transition cleanup; the Monk's Search ability with
native additive duration, typed status lifecycle, and client-only item
highlighting; Phasing's native random-offset
selection, consuming no-candidate outcome, delayed movement, and unchanged
facing; Fireball's native two-roll level scaling, level-capped speed, swept
direct collision, geometry-gated 3x3 splash, and persistent explosion;
Resurrection's other-player
targeting, native placement, resource reset, and timed beam; and the broader Charged Bolt
creation/collision/PvP timelines. The external spell catalog now carries the
validated identities and behavior selections for the current direct-cast slice,
including the Hellfire spell controllers listed in the content reference. Full
legacy parity and edge-case coverage remain incremental work rather than a
client-side responsibility.
The Bard's cursor-driven Identify skill is modeled separately as a typed item
service: the server validates the content-selected class ability and exact
owned item, applies identification without a store or Gold cost, and preserves
the source-zero paid vendor path for older clients.
The Warrior's cursor-driven Repair skill uses the same service boundary but a
distinct stable ability identity. The server owns its native iterative random
repair, permanent maximum-durability loss, no-op and item-exhaustion outcomes,
while source zero retains full paid Smith repair.
The Sorcerer's cursor-driven Staff Recharge skill also uses that boundary. The
server validates the content-selected ability, exact staff, and external native
staff level; owns its single recharge-strength RNG roll and permanent
maximum-charge loss; and retains source-zero paid Witch recharge.
Terminal Death also admits an explicit restart command whose level, tile, Q6
life reset, and transition duration come from validated base content; the
server preserves inventory, zeroes mana, and clears store state.
Ordinary combat death also applies the content-selected Gold rule. Canonical
inventory piles follow native non-full-first removal, whole-stack seed reuse,
partial-stack RNG, grid compaction, and crawl placement; this preserves the
physical odd remainder behind Diablo's truncated wallet cache. Authoritative
pickup tops off and places those piles before recalculating the wallet, while a
partial pile remainder stays server-owned on the cursor with its original seed
and outside the wallet. Clients may request a nearby drop, and combat death
drops the held item before applying ordinary Gold loss. Snapshots, saves, and
replay hashes preserve the complete held item. A compatible aggregate fallback
remains for older state without Gold topology.
Accepted movement, attacks, and casts also maintain server-owned native facing.
Ordinary monster-or-trap death uses it to rotate through equipment slots and
place each retained-seed item on its preferred adjacent tile or the shared
crawl fallback without consuming RNG. PvP and marked multiplayer-level deaths
retain equipment. Protocol field 27, saves, both clients, and shared native/C#
replay checkpoints preserve this state and behavior.
Lethal player damage now creates a server-owned native-compatible ear unless
the active multiplayer level suppresses death drops. The persisted victim name
supplies its identity and runtime names, external class/item/combat content
supplies its cursor and stable definition, and the existing world-item protocol
shape carries the result to both clients without consuming additional RNG.
Explicit host-owned game mode and validated level metadata also preserve the
native multiplayer death exception for level 16 and arena-equivalent maps. On
marked levels multiplayer death retains Gold and RNG state while cursor-held
items still drop; single-player continues to use ordinary loss.
Normal content-backed participants start with deterministic canonical Gold at
native inventory cell 30. Store purchases and services spend physical piles in
native order, and sales top off or place seeded piles transactionally; a shared
C++/C# replay covers the complete purchase/sale/refill sequence.
Protocol-independent player and world state plus store, inventory, movement,
portal, pickup, object, quest, attack, cast, projectile, status, and autonomous
monster transitions now live behind a `DiabloSimulation` aggregate. The Diablo
module registers its rules, simulation factory, and content bootstrap through
the transport-neutral `Devilution.Engine` capability registry; the executor
translates Protobuf commands and results around that aggregate. Engine now owns generic deterministic
session/state coordination and transient event queues. Diablo captures and
validates transport-neutral snapshots used by both wire and save paths, while a
dedicated Diablo adapter preserves Protobuf snapshots, events, hashes, and the
version-1 save envelope. The generic Engine runtime serializes participant
operations and advances deterministic ticks exactly once; the host supplies the
wall-clock tick source. Protocol mapping and the explicit versioned save codec
live in `Devilution.Server.Diablo`; filesystem persistence and operational
hosting live in `Devilution.Server.Host`.
Network capture is atomic across all assigned participants. Each receiver sees
itself first and the remaining players in entity-ID order, while canonical
hashing sorts the complete player set so receiver-relative ordering does not
change the player portion of the hash. Receiver-scoped fields such as the
singular active-store view remain part of each session's projection.
The version-1 save codec remains an explicit single-player projection and does
not serialize unrelated live sessions into a player's save.
The module-owned content bootstrap receives the generic composed tables and
owns schema validation, normalized/legacy fallback selection, every Diablo
catalog loader, default-class and default-level selection, and simulation
construction. The executable startup no longer names TSV files or constructs
game catalogs.
Executable composition now selects an `IAuthoritativeGameHostAdapter` through a
generic adapter catalog. That combined boundary supplies command execution,
ticks, snapshots, events, and saves to the TCP host. The built-in Diablo adapter
alone resolves Diablo capabilities and constructs `StoreSimulationExecutor`;
`Program` contains no Diablo symbols or executor types.

## Run

From the repository root:

```powershell
dotnet run --project server/src/Devilution.Server/Devilution.Server.csproj
```

The default endpoint is `127.0.0.1:6113`, content is loaded from
`server/content/base`, and authoritative saves are written under `server/saves`.
Use `--help` for the complete option list. Common overrides are `--bind`,
`--port`, `--content-root`, `--save-root`, `--game-mode`, and (when multiple
factories are installed) `--game-adapter`. The standalone
host defaults to multiplayer; use `--game-mode single-player` for the same
protocol and simulation in a local single-player session. Normal starting life and mana are
derived from the class catalog's native Q6 formulas, while the initial wallet,
items, class skill, and learned spells come from the selected class loadout.
The wallet is backed by physical Gold piles from session creation onward.
`--starting-gold`, `--starting-life`, `--starting-mana`, and
`--starting-stat-points` remain optional development overrides; without them,
activated content owns the initial resources and attribute-point state.
The server generates and logs a fresh 32-bit gameplay RNG seed at startup. Use
`--random-seed <uint32>` to reproduce an authoritative run exactly; the active
module owns subsequent draws, and snapshots and saves persist the current state.
Use the repeatable `--content-overlay <directory>` option to compose ordered,
manifest-declared overlay packs over the base content.
Use the repeatable `--module-package <directory>` option to load explicitly
installed gameplay-module packages before Engine compatibility preflight.
By default exactly one compatible host-adapter factory must be available. Use
`--game-adapter <id>` to select a named compatible factory when a deployment
provides more than one; unknown or incompatible IDs fail before network startup.

At startup the server prints the content-manifest hash, combined ruleset
identity, and protocol schema. Production protocol `0.2.0` clients send the
content-manifest hash and ruleset identity in their corresponding handshake
fields. Protocol `0.1.0` remains an explicit compatibility mode and uses the
ruleset identity for both fields. Startup also prints the ordered active packs
and the compatibility diagnostic for every loaded gameplay module. Invalid
pack composition or module compatibility fails before the server starts
listening.

Startup publishes composed content, the complete module registry, ruleset
identity, and the selected validated game adapter through one coordinated
Engine activation. Candidate module registration is all-or-nothing on this
path, so a later module or Diablo content-validation failure cannot expose a
partial ruleset. The host also exposes a transactional replacement API that
exports and imports every initialized session under an operation barrier,
updates the handshake identity only after migration succeeds, and retires old
adapters after in-flight leases drain. The standalone executable also provides
an operator-facing reload command through its local console.

The save root is reusable across process restarts: a reconnect carrying the
same resume token restores the persisted session through the fresh adapter and
save provider before any new character selection is considered. The host also
publishes an atomic `<session>.session.json` checkpoint containing the exported
state and completed command receipts. Existing `<session>.json` and
`<session>.commands.json` files remain compatibility fallbacks, while a
retrying client sequence remains a duplicate after a process restart rather
than replaying gameplay.

When attached to an interactive console, the standalone executable accepts the
local operator commands `status`, `reload`, `help`, and `quit`. `reload` reads
the configured content and module-package roots again, validates the candidate,
and invokes the same transactional session migration barrier as the host API;
failed candidates leave the active ruleset unchanged. End-of-input does not
stop the server, which remains controlled by `quit` or Ctrl+C.

## Gameplay-module packages

Package format 1 is a directory containing one managed DLL and a
`gameplay-module.json` manifest:

```json
{
  "formatVersion": 1,
  "id": "example.rules",
  "version": "1.0.0",
  "assembly": "Example.Rules.dll",
  "assemblySha256": "<64 hexadecimal characters>",
  "moduleTypes": ["Example.Rules.ExampleGameplayModule"]
}
```

The operator explicitly selects each directory; the server does not scan for
or accept client-supplied code. It verifies the primary assembly hash, loads
only the named public parameterless module types, and uses the verified hash in
their ruleset identities. Format 1 permits references only to platform
assemblies and the shared Domain, Data, Engine, and Diablo contracts; private
managed and native dependencies are rejected so the declared binary remains
the complete package code identity.

Each module type declares its own Engine API range and named module
dependencies. A dependency may additionally require an inclusive
three-component semantic-version range. Engine checks missing, cyclic,
non-semantic, and out-of-range dependencies before any module registers a
capability. The verified package wrapper preserves these requirements, and
declared constraints participate in the ruleset hash.

These modules are fully trusted in-process server code. The manifest hash
detects accidental or unauthorized binary changes relative to the selected
manifest, but it is not a publisher signature, trust store, or security
sandbox. Administrators must install packages only from trusted sources.

## Test

```powershell
dotnet run --project server/tests/Devilution.Domain.Tests/Devilution.Domain.Tests.csproj
dotnet run --project server/tests/Devilution.Data.Tests/Devilution.Data.Tests.csproj
dotnet run --project server/tests/Devilution.Engine.Tests/Devilution.Engine.Tests.csproj
dotnet run --project server/tests/Devilution.Diablo.Tests/Devilution.Diablo.Tests.csproj
dotnet run --project server/tests/Devilution.Server.Tests/Devilution.Server.Tests.csproj
```

The independent domain, data, engine, Diablo-module, and server suites use xUnit
v3 and include deterministic primitives, validated content identity, typed
module capabilities, game schemas and rules, protocol, persistence, replay, and
shared native/C# parity coverage.

## Clients

- Use the root `run-godot-client.ps1` launcher for a complete local Godot
  session. The underlying `godot/run-local-client.ps1` harness remains available
  for diagnostic switches.
- Build the native adapter with
  `DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT=ON`, then run the root
  `run-authoritative-client.ps1` launcher.
- Use the launcher's `-Legacy` switch to run the unchanged local native path.

Both authoritative clients can request a stable class ID and native-valid
character name when creating a new session. The active game module validates
both; a resumed in-memory or saved participant retains its authoritative name,
class, and complete loadout state. A cold save import selects the saved class
template before validating and restoring identity, resources, items, and spell
knowledge.

Server-backed sessions do not read or write legacy client saves. See
`docs/csharp-authoritative-server-migration.md` for ownership boundaries and
`server/content/README.md` for the content-pack contract.

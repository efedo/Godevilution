# Devilution.Diablo

`Devilution.Diablo` is the first-party game module for authoritative Diablo and
Hellfire behavior. It targets .NET 8, references the Domain kernel, Data runtime,
and Engine capability contracts, and has no dependency on the server host,
Protobuf, Godot, or persistence.

The module currently owns:

- authoritative item state, item generation, affixes, unique items, stores,
  and service pricing;
- protocol-independent player attributes, owned inventory, equipment, belt,
  and read-only authoritative player projections;
- a validated player-class catalog with native-compatible IDs, base and
  Hellfire metadata, content-selected starting attributes, and exact native Q6
  starting-life and starting-mana formulas;
- validated per-class starting wallets, equipment, belt and inventory layouts,
  native class skills, learned spell levels, deterministic item seeds, and
  native item-index/charge compatibility metadata;
- protocol-independent store/inventory intents, results, mutable state, and
  deterministic transitions, including multi-cell placement and swaps;
- protocol-independent world state, navigation and occupancy rules, movement,
  portals, world-item pickup, object effects, quest transitions, and gameplay
  events;
- combat rules, damage types, monsters, drops, spells, projectiles, combat
  intents, status advancement, deterministic autonomous combat ticks, and
  content-selected player-death Gold loss/drop/pickup with native inventory
  ordering, seed reuse, grid compaction, cursor-held overflow/drop/death
  handling, aggregate-save compatibility, and explicit-mode multiplayer
  suppression selected by validated level metadata;
- canonical starting Gold plus native-order store spending, purchase placement,
  sale top-off/new-pile creation, inventory-only transfer enforcement, and
  transactional capacity rejection;
- a shared-state `DiabloSimulation` aggregate and engine-registered simulation
  factory;
- an engine-registered content bootstrap that validates the declared schema,
  selects normalized or legacy table variants, constructs every Diablo catalog,
  and returns activated simulation state;
- stable protocol-neutral player/world/store snapshots plus module-owned restore
  validation;
- normalized player templates and lifecycle hooks consumed by the generic Engine
  runtime, including independent per-session templates selected by stable class
  ID;
- levels, portals, objects and their effects, quests, statuses, and world
  entities;
- transport-independent Diablo store validation and pricing results.

`Devilution.Server` remains the composition and protocol adapter. It translates
Protobuf store, inventory, world, attack, and cast commands into module intents;
supplies host-ordered players at tick boundaries; maps module failures and
events into protocol messages; and resolves capabilities from the engine
registry. `DiabloModule` registers its rules, simulation factory, and content
bootstrap through the typed `Devilution.Engine` capability registry. Server
passes the bootstrap only generic `ComposedContent`; table names, fallback
selection, schema validation, and catalog construction remain here. The
protocol adapter must remain thin; Diablo state transitions, formulas, and
content schemas belong here.
The Engine-owned generic session coordinator now supplies deterministic player
ordering, reverse state lookup, serialized state access, exactly-once tick
advancement, and transient event queues. The Server keeps the versioned Protobuf
and JSON compatibility representations outside this module. A Server-owned
Diablo adapter bridges this module to those representations behind the generic
`IAuthoritativeGameHostAdapter`; the executable composition root does not
reference Diablo types.

When validated towner greetings are supplied, native-role store entry and
Gillian's stash require the declared NPC level, Chebyshev distance at most one,
and no held item. Reserved Adria/Cain IDs retain their town-only guard. Rejected
entry occurs before inventory-dimension hydration; accepted entry and the
metadata-free compatibility path retain hydration. Custom Generic stores are
unchanged. Ongoing item actions do not reapply the entry cursor/range check;
closing services remains possible anywhere. Full open-service lifecycle handling
across movement/transitions is still an integration gap.

The ordinary Cathedral level-1 geometry adapter expands the generator's
one-based coarse IDs through validated external TIL/SOL catalogs into native
112x112 fine coordinates at offset (16,16), with exterior mega 22 and local
Cathedral SOL corrections. Its immutable result retains the separate 40x40
coarse/seed/stair output and fine level rather than changing generic DUN or wire
geometry contracts. No MPQs are read. Native asset/oracle comparison, runtime
integration, exact vision parity and population remain open; the existing level
contract conservatively includes movement blockers in vision blockers.

Run the independent xUnit v3 suite from the repository root:

```powershell
dotnet run --project server/tests/Devilution.Diablo.Tests/Devilution.Diablo.Tests.csproj
```

The suite includes the checked-in base content pack and deterministic class,
loadout, item, store, combat, spell, projectile, autonomous-tick, status,
inventory, navigation, portal, pickup, object-effect, quest, and
world-validation coverage.

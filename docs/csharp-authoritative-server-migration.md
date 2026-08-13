# Godevilution Authoritative Architecture

## Purpose

Godevilution is migrating the inherited C++/SDL engine to a server-authoritative
C# engine and a Godot C# presentation client. The migration is incremental so
the native client remains usable for compatibility testing while authoritative
systems move behind a shared protocol.

This document defines the intended architecture and invariants. Current progress
is tracked separately in [migration-status.md](migration-status.md).

## Components

| Component | Responsibility |
| --- | --- |
| Domain kernel | Engine-neutral deterministic primitives, simulation state, commands, events, validation, and stable gameplay APIs |
| Data runtime | External content loading, validation, overlays, stable symbolic IDs, and content manifests |
| Engine composition | Module identity, dependency/version preflight, atomic candidate activation, coordinated ruleset publication, clocks, and generic simulation coordination |
| Gameplay modules | Game-specific authoritative rules and simulation factories exposed through engine capabilities |
| Server host | Sessions, transport, fixed ticks, persistence, module loading, and operational concerns |
| Game server adapter | Maps one transport-neutral gameplay module to host commands, snapshots, events, and saves |
| Godot client | Input, rendering, UI, audio, interpolation, bounded prediction, and presentation effects |
| Protocol package | Versioned commands, snapshots, events, errors, identifiers, and ruleset identity |

The C# runtime boundaries are now separate assemblies, while the inherited
native compatibility implementation remains a monolith during migration.
`Devilution.Domain` is the dependency-free .NET 8 kernel and owns canonical
hashing plus the first extracted inventory-grid rules. `Devilution.Data` is the
.NET 8 validated content runtime and depends only on the domain kernel.
`Devilution.Engine` is the .NET 8 transport-neutral composition runtime, depends
only on Domain and Data, and owns module identity, dependency ordering and
version constraints, typed capabilities, clock context, ruleset identity, and
the atomic content/module/target activation boundary.
`Devilution.Diablo` is the
.NET 8 first-party game module, depends only on Domain, Data, and Engine, and
owns the extracted item, store, combat, monster, spell, and world
schemas. It also owns authoritative player and world state, transport-neutral
gameplay events, and protocol-independent store, inventory, movement, portal,
pickup, object, and quest intents, results, and deterministic transitions.
It now also owns attack and cast intents, typed damage and progression,
projectiles, status advancement, monster decisions, and deterministic combat
ticks. `Devilution.Engine` also owns generic deterministic session/state
coordination, serialized participant access, exactly-once tick advancement, and
transient event queues. `Devilution.Diablo` owns stable transport-neutral
player/world/store snapshot capture and restore validation, including
authoritative player-class identity and starting attributes.
`Devilution.Protocol` is the single generated C# binding assembly.
`Devilution.Server.Host` maps generic host services to that contract and owns
the wall-clock scheduler, module loading, filesystem persistence, and sessions
without referencing Diablo. `Devilution.Server.Diablo` maps Diablo commands and
projections to Protobuf and preserves version-1 save bytes through an explicit
version-dispatching codec. The .NET 10 `Devilution.Server` executable contains
only `Program.cs` and composes these libraries. Data now composes
manifest-declared composite-key overlays transactionally. Engine now preflights
the host-supplied module candidates for deterministic order, dependencies, and
semantic dependency-version and Engine API compatibility before registration.
The Server host now
loads only explicit, hash-verified, single-assembly module packages and passes
their declared types to Engine. Content manifests can declare schema identities
and dependency-version constraints without changing legacy identities.
Diablo-specific catalog/bootstrap construction now occurs behind the module's
content-bootstrap capability; Server startup supplies only generic composed
content and player-start settings. Separating generic host lifecycle from the
Diablo-specific protocol adapter now occurs through a combined Server-side
adapter contract and deterministic factory catalog. The executable composition
root names no Diablo types. Exact assembly-reference tests enforce the separate
Protocol, Host, Diablo adapter, and executable dependency graph, while a schema
ownership test prevents duplicate managed Protobuf generation.

## Authority

The server owns:

- simulation time, tick ordering, random seeds, and random streams;
- accepted player and monster positions;
- combat, healing, experience, levels, spells, projectiles, and status effects;
- items, inventory, equipment, gold, stores, and item generation;
- objects, quests, portals, level occupancy, and persistent world state;
- saves, validation, migrations, active content, and gameplay modules.

Clients own:

- input collection and intent submission;
- rendering, animation, audio, menus, HUD, and accessibility;
- interpolation and explicitly bounded prediction;
- presentation-only diagnostics and effects.

A client sends intent such as movement, attack, cast, purchase, or interaction.
It never sends final positions, damage, item rolls, resource totals, or other
authoritative results.

## Protocol and hosting invariants

- `protocol/devilution.proto` is the canonical schema.
- The initial transport is reliable TCP with a four-byte little-endian frame
  length and a maximum one-MiB Protobuf envelope.
- The server advances at a fixed 20 Hz authoritative tick by default.
- Commands are ordered by requested tick and server receipt sequence.
- Client sequence numbers are reused for retries; the server deduplicates them.
- Snapshots are canonical state. Events are transient presentation deltas.
- Every network snapshot includes all assigned participants, with the receiver
  first on the wire and every player sorted by entity ID for canonical hashing.
- Single-player launches or connects to the same server used by multiplayer.
- An authoritative client never falls back to local simulation after a server
  failure.

## Determinism and compatibility

Gameplay-visible legacy behavior is preserved through fixed-point conventions,
integer truncation, deterministic ordering, legacy-compatible random streams,
stable IDs, shared replay fixtures, and canonical state hashes.

Protobuf changes are additive within `devilution.protocol.v1`. Existing field
numbers and meanings are immutable. Breaking changes require a new package and
schema version.

The inherited native implementation remains available until migrated behavior
has characterization and replay coverage. Dual execution is a testing tool; a
production session must have exactly one authoritative simulation.

## Content and gameplay modules

Definitions, tuning, localization keys, and relationships belong in validated
external content. Generic mechanics belong in the domain kernel. Diablo-specific
behavior that cannot be expressed clearly as data belongs in a versioned C#
gameplay module.

Gameplay modules are trusted server assemblies installed by the server
administrator. Clients cannot upload or select executable server code. The
ordered content packs and gameplay modules form one ruleset identity recorded
in handshakes, saves, diagnostics, and replay fixtures.

Content packs are composed in declared order. Replacing an existing table
requires an explicit composite-key `upsert` declaration with the same ordered
schema; failed composition leaves the previously active immutable content
unchanged. Engine accepts an explicit trusted candidate set from the host and
rejects duplicate IDs, missing or cyclic dependencies, and incompatible Engine
API or declared inclusive module-dependency version ranges before any module
registers capabilities. A constrained dependency must expose a three-component
semantic version. Declared requirements participate in ruleset identity and are
retained when the host substitutes a verified package assembly hash. Engine
deliberately does not scan the filesystem or load assemblies.

The coordinated ruleset runtime builds a candidate entirely off to the side:
Data composition, all-or-nothing module registration, deterministic identity,
and a caller-provided game-specific activation must all succeed before one
locked publication replaces the active tuple. Stage-specific reload failures
return the exact previous activation. Engine accepts a host-owned candidate
preparation callback, so session state can be validated against the candidate
before publication. The generic host provides a read/write operation barrier,
exports complete initialized session state from the old adapter, imports it into the
candidate, and updates the handshake identity only after migration succeeds.
Generation leases keep replaced activations alive until all in-flight
operations release them; the host drains and disposes those targets through its
injected disposal policy.
Server shutdown also disposes the leased wrapper itself, releasing its
operation barrier after draining any remaining retired generation.

Snapshot hashing treats static catalog portals as content rather than mutable
simulation state, preserving legacy replay vectors. Owned, expiring portals are
included in the canonical hash; base-content fixtures are refreshed whenever
the current manifest or authoritative projection changes.

The Diablo base pack's `classes.tsv` declares the six native-compatible class
IDs, base-game/Hellfire availability, flags, attribute bounds, combat modifiers,
Q6 life/mana scalars, and separate native self-Healing and Heal Other class
multipliers. It also
selects exactly one default class, making
starting class and attributes activated content rather than host special cases.
Overlays may replace class rows, but activation validates the complete composed
catalog transactionally before it can affect a session.

The companion `class_animations.tsv` mirrors every native per-class animation
timing, including each weapon/shield attack frame and action frame plus walk,
block, death, casting, recovery, and town frames. Activation cross-validates its
class IDs and native ranges before simulation construction. Block and recovery
consume this catalog. The authoritative player-action
lifecycle that reserves walk destinations and resolves melee, bow, and spell
effects at native class/equipment frames, including action chaining and
base-game/Hellfire bow-speed differences is enabled for activated content.
In-progress action state crosses module snapshots, protocol field 25, version-1
saves, conditional hashes, and native and Godot projection. A shared C++/C#
fixture verifies deferred movement, melee damage, attack-to-cast chaining,
spell consumption/effects, and cleanup at seven timeline checkpoints. Paired
shared fast-Bow fixtures carry hashed initial equipment and prove nonadjacent
ranged admission plus the base-game/Hellfire frame difference; focused module
tests retain the wider melee speed-tier matrix.

The same lifecycle now owns terminal player actions. Lethal monster and
Charged Bolt damage cancel the prior action and start an already-applied Death
action whose duration is twice the activated class `death_frames`, matching the
native two logic ticks per death frame. Cleanup removes the action while
retaining Death mode. A valid portal applies its authoritative destination
immediately, enters NewLevel through an already-applied one-tick transition
action, and returns a living player to Stand on the next tick. Death and
transition actions are validated by module snapshots, version-1 saves, protocol
mapping, canonical hashes, and both clients. Shared C++/C# fixtures cover the
same autonomous monster approach/death and portal timelines. Schema 0.16 makes
ordinary combat-death gold server-owned: it cancels movement before native crawl
placement, retains and drops independently truncated halves, consumes the
shared RNG for newly split stacks, and persists every result. Canonical
inventory-backed Gold follows the native two-pass order (non-full piles first),
reuses whole-stack seeds, compacts the inventory grid, and preserves the
physical odd remainder even while the cached wallet is truncated. Pickup tops
off those piles and uses native last-row/column placement, recalculating the
wallet from physical Gold. When a pile only partly fits, the server owns the
unplaced remainder as a cursor-held item with the original world seed. That
remainder is excluded from the wallet, can be dropped only onto a valid nearby
tile relative to the reserved movement destination, and is dropped before
ordinary Gold on death. Held state crosses module
snapshots, protocol field 26, version-1 saves, conditional hashes, and both
clients; an aggregate-only fallback keeps older saves and fixtures compatible.
The shared death fixture hashes the cached 50 wallet, the physical 51-gold
remainder, its inventory cell, the 50-gold world item, and the advanced RNG
state. A separate two-checkpoint replay fills the final ten Gold capacity,
retains 65 on the cursor without advancing RNG, and drops it with its original
seed. Content-backed class templates now create canonical
session-seeded Gold at native cell 30, and store purchase/service spending,
purchase placement, sale top-off/new-pile placement, capacity rejection, and
post-death cache divergence use that topology. Item transfers also keep Gold in
inventory by rejecting direct moves or swaps that would place it in equipment
or the belt. A separate shared C++/C# store fixture carries one pile through
purchase, sale, and mana refill at five checkpoints. Multiplayer level-16/arena
suppression is now selected by explicit host-owned game mode plus the validated
schema-0.17 `suppress_multiplayer_death_drops` level flag. The mode crosses
`ServerHello` and both clients, while the authoritative transition preserves
Gold and RNG state but still drops a cursor-held item. A shared C++/C# replay
proves the rule without participant-count inference. Ordinary monster-or-trap
death now also drops equipment in native slot order, rotating left from the
server-owned facing once per slot and trying the preferred adjacent tile before
the shared crawl. Item seeds and complete state are retained, no RNG is
consumed, PvP death retains equipment, and special-level multiplayer
suppression retains equipment with Gold. Facing is carried by protocol field
27, saves, conditional hashes, and both clients. A two-checkpoint C++/C# replay
proves slot rotation, occupied-tile fallback, and identified-value projection.
Schema 0.18 now completes the PvP-ear consequence for lethal player damage.
The requested character name becomes server-owned participant identity and
crosses protocol field 28, saves, reconnect, conditional hashes, and both
clients. The victim name supplies the native create-info/seed tuple, the victim
level supplies item value, `classes.tsv` supplies the native cursor, and
`combat.tsv` selects the normalized `items.tsv` ear definition. The resulting
runtime base/identified names and cursor use the generic additive item snapshot
fields, so clients do not reconstruct an ear. Equipment remains equipped,
special-level multiplayer suppression omits the ear, duplicate identity is
rejected, and placement uses the existing death crawl without consuming RNG.
A shared two-checkpoint C++/C# replay proves the complete transition.
Restart in Town is a distinct
terminal-death world intent: after the
timed Death action clears, the server applies the schema-0.15 destination,
restores one whole point of Q6 life, zeroes mana, clears store state, and holds
NewLevel for one tick.
The empty protocol command, action kind 6, saves, both clients, and a shared
C++/C# replay preserve the transition without giving either client authority.
Server-owned Resurrection is covered below.

`class_loadouts.tsv` and `class_loadout_items.tsv` separately declare each
class's initial wallet, class skill, optional learned spell, and exact starting
item placements. The item catalog supplies stable content IDs plus explicit
native compatibility indexes and charge state. Activation validates the class,
loadout, item, spell-bound, equipment, belt, and inventory relationships as one
candidate, then requires every directly castable starting learned, charged-item,
scroll, or rune source to resolve in the activated spell catalog. Class-skill
IDs remain distinct because several native skills select cursor-driven item or
object interactions instead of casting an ordinary spell. Fresh item seeds are
deterministic functions of session and content identity rather than client time
or local native state.

Cast admission is source-aware at the module boundary. The protocol adapter
maps the requested source and optional stable item reference into a
transport-neutral cast intent. Diablo validates class abilities, learned spell
levels, the charged staff in the native left-hand slot, and exact inventory or
belt scroll/rune references against authoritative state. It consumes mana,
charges, or the item only after all other cast validation succeeds. Legacy
source-zero commands remain additive-compatible by deterministic server-side
inference from owned state; they cannot cast an unowned spell.

The Godot presentation client enumerates the same authoritative source set in
stable order—memorized spells, the charged left-hand staff, inventory and belt
consumables, then the class ability—and cycles it with `Q`/`E`. It sends the
selected source and exact item reference; the selector never validates or
executes gameplay locally.

Spell definitions declare a bounded base projectile count and can select a
typed module-owned behavior with level-count scaling, random-damage,
resistance, impact, speed, and lifetime tuning. One accepted cast allocates the
resulting independently authoritative projectiles while charging mana, a staff
charge, or a consumable exactly once. The base Sorcerer staff's spell 30 now
uses the native Charged Bolt count, Diablo-LCG draw order, Magic-based damage,
16.16 isometric wandering cadence, magic hit roll, and lightning
resistance/immunity behavior. Its RNG and full projectile runtime are captured
in canonical snapshots and version-1 saves. The module now also separates
missile-blocking geometry from walkability, sweeps skipped intermediate tiles,
suppresses repeated stationary collisions against the same stable occupant, and
applies external object pass-through, breakability, and persistent destruction
flags. Those additions cross protocol, saves, hashes, native projection, and a
shared C++/C# replay. Moving monsters now retain separate current and adjacent
future occupancy; normal walkers collide only at the current tile, while
petrified walkers freeze and become hittable at either tile with the native
guaranteed-hit RNG consumption. Movement, petrification, death cancellation,
protocol, saves, hashes, native signed occupancy, and a shared replay are
covered. Player collision now also preserves the native global/friendly-mode
gate, hit roll, eligible equipment/class block roll, exact Q6 spell damage,
lightning-resistance override, and RNG order. Friendly mode is explicit
retry-safe authoritative state carried through protocol, saves, hashes, native
and Godot controls, and a shared two-participant replay. Animation-mode block
eligibility is now server-owned: only native Stand and melee Attack modes may
block, successful blocks and threshold hits install class-duration modes, and
usable fast-block/hit-recovery flags shorten them. `combat.tsv` selects Diablo
or Hellfire behavior; Hellfire consumes the extra blocked-missile rotation draw
in the native order. Protocol, saves, hashes, native/Godot projection, and a
shared Hellfire block replay preserve that state and RNG boundary.

The starting Sorcerer spell `1` now selects a distinct `Firebolt` behavior from
external content rather than the provisional targeted-projectile path. Content
schema 0.9 carries native mana adjustment, minimum, Q6 scale, randomized damage,
magic and spell-level scaling, speed, lifetime, impact, and resistance tuning.
Class rows carry base-game and Hellfire mana-reduction percentages. Cast
resolution consumes the resulting Q6 mana only after admission, creates damage
with the native Diablo-LCG order, moves in 16.16 isometric space through every
swept tile, rolls magic-to-hit, applies native quarter damage to resistant
monsters, and preserves its impact through snapshots and version-1 saves. A
three-checkpoint C++/C# replay proves spawn, impact, exact fractional health,
expiry, and RNG parity.

Sorcerer spell `3` now selects schema-0.10 `Lightning` behavior. An invisible
speed-32 controller consumes the two native damage draws per update in an
explicit order shared with retained C++, then leaves same-tick stationary
segments whose lifetime scales with effective spell level. Segments preserve
native repeated collision, magic-hit, petrification, positive-resistance, PvP,
and exact-Q6 rules. The controller stops at missile-blocking geometry without
breaking an object or creating a segment there. Effective spell level and the
complete controller/segment runtime cross protocol field 29, snapshots, saves,
conditional hashes, and a four-checkpoint C++/C# replay.

Spell `4` now selects schema-0.11 `Flash` behavior. Cast resolution ignores
client coordinates, consumes the native independent Bottom-then-Top damage
streams, and creates two 19-tick stationary layers whose ordered offsets
partition the player's 3x3 neighborhood. Each layer repeats magic-hit and exact
Q6 resistance damage without expiring on actors or walls, while still breaking
eligible objects. Live layer state derives the native first-update-through-final-
removal invulnerability window for monster and PvP damage. Runtime layer type,
damage, effective level, magic resistance, and RNG state cross snapshots, saves,
hashes, protocol projection, and a four-checkpoint C++/C# replay.

Spell `2` now selects schema-0.12 `Healing` behavior. The module consumes the
native initial, character-level, and effective-spell-level RNG draws, converts
the result to Q6, and applies the activated class multiplier before capping at
maximum life. A full-life cast remains accepted and consumes both mana and RNG.
The replay envelope now carries an optional initial maximum life, healing events
carry an additive exact-Q6 field while retaining the legacy raw amount, and a
three-checkpoint C++/C# replay proves uncapped, capped, and full-life parity.

Spell `23` now selects schema-0.13 `Teleport` behavior. Cast resolution checks
the requested tile and radii through `5` in native `DoCrawl` order against
static geometry, active monster positions, other authoritative participants,
and active objects. A failed search fizzles without consuming the cast source;
a successful search creates an invisible two-tick controller and consumes the
source. The first missile update, after monsters advance, repeats the search
and moves the player; the second removes the controller. The generic
participant-aware simulation boundary, snapshots, compatible saves, existing
projectile/player protocol fields, both clients, and a four-checkpoint shared
C++/C# replay preserve the timeline.

Spell `32` now selects schema-0.14 `Resurrection` behavior. Cast admission
requires another authoritative participant on the caster's active level and
within the native range; no line-of-sight check is added. A valid target always
consumes the native fixed-minimum mana and creates a 16-tick beam at the
target's pre-effect position, including when that player is already alive. A
dead target returns to Stand with zero mana and up to ten whole life at the
first available native initial offset or radius-50 crawl tile. Placement checks
static geometry, living participants, monsters, and objects; the fallback crawl
also avoids portal exits. Full
participant snapshots, existing projectile target/lifetime fields, compatible
saves, Godot projection, native target mapping, and a five-checkpoint shared
C++/C# replay preserve the result without granting either client gameplay
authority.

Spell `11` now selects schema-0.19 `ManaShield` behavior. The first valid cast
consumes its fixed-minimum source and activates player state; another cast while
active is accepted but fizzles before source consumption. Every authoritative
monster, PvP projectile, and damaging-object path uses one module-owned damage
resolver. It reproduces the native learned-level divisor and cap, exact Q6 mana
absorption, reverse calculation for an unpaid remainder, exact-depletion
cleanup, and NoMana-equipment bypass. The active flag crosses module snapshots,
version-1 saves, additive protocol field 29, conditional hashes, native and
Godot presentation, and a six-checkpoint shared C++/C# replay. Level-changing
transitions and Resurrection clear the effect server-side.

Spell `8` now selects schema-0.20 `StoneCurse` behavior. At the effect frame,
the module performs the native radius-five crawl over authoritative monster
occupancy, skips content-marked immunity, and preserves the native distinction
between a no-target fizzle and an already-petrified non-refreshing cast. A
stationary runtime controller owns the level-scaled duration and dead-target
shatter phase while the monster retains its pending movement for release.
Petrification, both occupied cells, immutable immunity, controller lifetime and
effective spell level cross module snapshots, version-1 saves, additive monster
field 22, conditional hashes, native and Godot presentation, and a six-
checkpoint shared C++/C# replay.

Spell `9` now selects schema-0.21 `Infravision` behavior. The module admits a
self-targeted learned, ability, scroll, or rune source, applies native
class-adjusted mana only to learned casts, computes the duration's repeated
one-eighth growth with saturation, and replaces a same-ID status with the longer
remaining duration. Status behavior crosses module snapshots, version-1 saves,
additive protocol field 4, conditional hashes, native and Godot presentation,
and a four-checkpoint shared C++/C# replay. Portal and restart transitions clear
the typed effect server-side without removing generic statuses.

Spell `46` now selects schema-0.25 `Search` behavior, making the external
Monk loadout's declared class ability executable. The module computes the
native `245 + 2 * character level + 10 * effective spell level` duration from
validated content, adds recast duration with the native signed-overflow guard,
and owns tick expiry and level-transition cleanup. Protocol status behavior
`2` drives only native automap and Godot item highlighting; clients do not
advance or combine the effect.

Schema 0.26 makes the Bard's content-declared spell-5 Identify skill executable
as a typed item-service source. The module validates the selected ability and
exact owned equipment or inventory item, reproduces native zero-cost
identification, and bypasses vendor-store and Gold rules only for that verified
source. Source-zero requests retain the existing paid vendor behavior. Native
and Godot clients select an item and submit intent; the server alone mutates its
identified state.

Schema 0.27 makes the Warrior's content-declared spell-26 Repair skill
executable through the same item-service source contract. The module preserves
the native iterative rule: every iteration adds character level plus one native
random roll, reduces maximum durability by at least one, and stops when the
repaired durability reaches the reduced maximum. Full items consume no RNG;
exhausting maximum durability removes the item. Source-zero requests retain
full paid vendor repair. Both clients submit an exact item and project the
returned item and RNG-owned result.

Schema 0.28 makes the Sorcerer's content-declared spell-27 Staff Recharge skill
executable through the item-service source contract and externalizes each
authoritative spell's native staff level. The module reproduces the native
single recharge-strength roll, decrements maximum charges before every
restoration step, clamps current charges to the reduced maximum, and preserves
full-item no-op and zero-maximum exhaustion behavior. Source-zero requests
retain full paid vendor recharge; both clients submit only item selection and
project the returned state.

Spell `10` now selects schema-0.22 `Phasing` behavior. At the effect frame, the
module ignores client target coordinates and enumerates the native 36 offset
candidates in row-major order against authoritative geometry, monsters,
objects, and living participants. A nonempty set consumes one native RNG draw;
the selected position becomes an invisible two-tick controller whose first
update revalidates occupancy through the shared Teleport crawl and moves the
player. An empty set consumes no RNG and creates no controller, but the
accepted cast still consumes its learned, charged, scroll, or rune source.
Neither admission nor resolution changes player facing. Controller state, RNG,
resources, and resulting coordinates cross module snapshots, version-1 saves,
existing protocol fields, native and Godot projection, conditional hashes, and
a four-checkpoint shared C++/C# replay.

Spell `12` now selects schema-0.23 `Fireball` behavior. Creation preserves the
native two-draw character-level damage formula, repeated effective-level
one-eighth growth, and speed growth capped at a 34-point bonus. The module
advances a swept 16.16 trajectory, resolves the direct collision, then enters a
14-tick stationary impact while checking the center and eight neighboring
cells in native shifted order. Solid origin-to-cell geometry gates each splash
cell; direct and shifted hits retain their distinct Q6 resistance behavior.
Complete projectile runtime, RNG, exact health, protocol projection,
version-1 saves, native/Godot presentation, conditional hashes, and a four-
checkpoint shared C++/C# replay preserve the transition without client-side
simulation.

Spell `34` now selects schema-0.24 `HealOther` behavior. The module resolves a
stable, living, same-level, in-range participant distinct from the caster
before consuming the selected source or advancing RNG. It then uses Healing's
native mana and randomized Q6 formula with the caster class's separately
externalized Heal Other percentage; notably, the Monk applies 300 percent here
but 200 percent to self-Healing. Full-life targets remain valid and consume the
same resources and draws. Existing target IDs, healing events, complete player
snapshots, saves, hashes, Godot/native target mapping, and a timed three-
checkpoint C++/C# multiplayer replay carry the result without a wire change.

Schema 0.15 also marks one explicit default-start level in `levels.tsv` and
adds restart level, tile, life amount, Q6 scale, and transition ticks to
`combat.tsv`. Activation cross-validates the restart destination against the
composed world. This keeps session creation on dungeon level 1 while making
terminal-death recovery to town level 0 entirely content-selected and
server-owned.

Schema 0.16 adds validated death-gold divisor, projected maximum-stack, native
item type/index, and crawl-radius fields to `combat.tsv`. These values govern
the module transition and save validation; clients receive only the resulting
player balance and ordinary world-item snapshot.

Schema 0.17 adds validated multiplayer death-drop suppression to `levels.tsv`.
The generic host supplies explicit single-player or multiplayer mode to the
selected adapter; the Diablo module combines it with level metadata rather than
depending on host types, participant count, or hard-coded map IDs.

On a new connection, the generic Host passes an optional archetype ID through
`IAuthoritativeParticipantInitializer`; it does not interpret Diablo classes.
The Diablo adapter maps that selection to the active class catalog, constructs
an independent player template, and derives starting life and mana with the
native Q6 class formulas. It then applies the activated class wallet, equipment,
belt, inventory, skill, and learned-spell definition. An absent selection uses
the activated content default. Host restoration runs first, so an existing
in-memory participant or server-owned save is never replaced by a reconnect-time
selection. Cold restoration constructs the saved class template and validates
the save before participant registration; a failed import cannot leave partial
session state behind.

After module registration, the host passes `ComposedContent` to the selected
Diablo bootstrap capability. The module validates its schema, selects normalized
or legacy tables, builds all catalogs and deterministic rules, chooses the
default class and level, and returns activated simulation state. Server code
does not interpret table names or construct game catalogs.

The executable then selects exactly one compatible
`IAuthoritativeGameHostAdapter`. This Server-side bridge combines command,
autonomous-tick, snapshot, event, and save services. Its built-in Diablo factory
resolves the module bootstrap and creates the protocol executor, leaving the
composition root independent of game types. Multiple or missing compatible
adapters fail before network startup.

The standalone host's package format is an installation policy, not an Engine
contract. Version 1 accepts one explicitly named managed DLL, verifies its
SHA-256, instantiates only manifest-listed public module types, and rejects
private managed or native dependencies. Packages are trusted in-process code;
the digest binds code identity but does not authenticate a publisher or provide
sandboxing.

The current v1 compatibility boundary places the combined ruleset hash in both
the `content_manifest_hash` and `ruleset_identity_hash` handshake fields. A
future breaking protocol version may give those fields distinct meanings.

Recent content slices

Schema 0.66 adds validated `quest_pools.tsv` and `set_level_names.tsv` catalogs.
Quest type rows now retain native `SL_*` identities, and activation validates
pool references against the stable quest catalog before exposing the metadata
to the server-owned module. Single-player activation now applies the native
deterministic pool exclusions to mutable quest state, preserves the historical
bad-seed discard behavior, rejects excluded quest progression and linked object
use, and carries the state through snapshots, saves, protocol field 6, and
canonical hashes. Quest rows and transitions also preserve the native
initial/active/done lifecycle and quest-log bit through protocol fields 7-8.
Optional quest columns now also preserve native `_qvar1`, `_qvar2`, and `_qmsg`
values through authoritative saves, snapshots, protocol fields 9-11, and
compatibility-aware state hashes.
Quest rows may also declare a `completion_monster_id`; the server applies that
trigger at the authoritative monster-death boundary and persists the resulting
progress and completion state.
External quest-type route levels now also activate `QUEST_INIT` rows when the
server enters the matching level, with explicit single-player/multiplayer mode
selection rather than participant-count inference.
Multiplayer activation leaves pool exclusions disabled and skips quest types
marked `single_player_only`.
Linked quest objects may declare a validated progress amount; authoritative
activation applies and clamps that amount instead of assuming one progress point.

Schema 0.68 adds native acid resistance to `monster_types.tsv` and the typed
authoritative Acid damage channel. The four Hellfire acid creatures preserve
their native 100-percent immunity through encounter inheritance, snapshots,
saves, hashes, and protocol field 36.

Schema 0.69 adds the native-derived Hellfire Griswold's Edge base item and
unique definition (`unique_id=1010`) with its one-handed sword identity,
fire-damage, hit, attack-speed, knockback, mana, and life-curse powers.

Schema 0.70 adds the native-derived Hellfire Bovine Plate base item and unique
definition (`unique_id=1011`) with its heavy-armor identity, armor,
indestructibility, light, resistance, mana-curse, and spell-level powers.

Schema 0.71 adds the native-derived Hellfire The Rift Bow unique definition
(`unique_id=1012`), reusing the validated Short Bow base item with native
arrow-velocity, damage, and dexterity-curse powers.

Schema 0.72 adds the native-derived Hellfire The Butcher's Cleaver base item
and unique definition (`unique_id=1013`) with its two-handed axe identity,
strength, fixed-damage, and durability powers.

Schema 0.73 adds the native-derived Hellfire The Needler unique definition
(`unique_id=1014`), reusing the validated Short Bow base item with native hit,
fixed-damage, and attack-speed powers.

Schema 0.74 adds the native-derived Hellfire The Celestial Bow base item and
unique definition (`unique_id=1015`) with its long-bow identity,
minimum-strength, damage, and armor powers.

Schema 0.75 adds the native-derived Hellfire Deadly Hunter base item and
unique definition (`unique_id=1016`) with its composite-bow identity,
triple-damage, hit, and magic-curse powers.

Schema 0.76 adds the native-derived Hellfire Bow of the Dead unique definition
(`unique_id=1017`), reusing the validated Composite Bow base item with native
hit, attribute, curse, and durability powers.

Schema 0.77 adds the native-derived Hellfire The Blackoak Bow unique definition
(`unique_id=1018`), reusing the validated Long Bow base item with native
attribute, damage, and light/vitality-curse powers.

Schema 0.78 adds the native-derived Hellfire Flamedart base item and unique
definition (`unique_id=1019`) with its Hunter's Bow identity, fire-arrow, hit,
and fire-resistance powers.

Schema 0.79 adds the native-derived Hellfire Fleshstinger unique definition
(`unique_id=1020`), reusing the validated Long Bow base item with native
dexterity, hit, damage, and durability powers.

Schema 0.80 adds the native-derived Hellfire Windforce base item and unique
definition (`unique_id=1021`) with its War Bow identity, strength, damage, and
knockback powers.

Schema 0.81 adds the native-derived Hellfire Eaglehorn base item and unique
definition (`unique_id=1022`) with its Battle Bow identity, dexterity, hit,
damage, and indestructibility powers.

Schema 0.82 adds the native-derived Hellfire Gonnagal's Dirk base item and
unique definition (`unique_id=1023`) with its Dagger identity,
dexterity-curse, damage-modifier, attack-speed, and fire-resistance powers.

Schema 0.83 adds the native-derived Hellfire The Defender base item and unique
definition (`unique_id=1024`) with its Sabre identity, armor, vitality, and
hit-curse powers.

Schema 0.84 adds the native-derived Hellfire Gryphon's Claw base item and
unique definition (`unique_id=1025`) with its Falchion identity, damage,
magic-curse, and dexterity-curse powers.

Schema 0.85 adds the native-derived Hellfire Black Razor unique definition
(`unique_id=1026`), reusing the validated Dagger base item with native damage,
vitality, and durability powers.

Schema 0.86 adds the native-derived Hellfire Gibbous Moon base item and unique
definition (`unique_id=1027`) with its Broad Sword identity, attribute, damage,
mana, and light-curse powers.

Schema 0.87 adds the native-derived Hellfire Ice Shank base item and unique
definition (`unique_id=1028`) with its Long Sword identity, fire resistance,
durability, and strength powers.

Schema 1.36 adds the native-derived Hellfire Scavenger Carapace base item
and unique definition (`unique_id=1077`) with its Breast Plate identity,
armor, get-hit, armor-curse, dexterity, and light-resistance powers.

Schema 1.35 adds the native-derived Hellfire Sparking Mail base item and
unique definition (`unique_id=1076`) with its Chain Mail identity, armor, and
lightning-damage powers.

Schema 1.34 adds the native-derived Hellfire Wisdom's Wrap base item and
unique definition (`unique_id=1075`) with its Robe identity, magic, mana,
light-resistance, armor, and get-hit powers.

Schema 1.33 adds the native-derived Hellfire Leather of Aut base item and
unique definition (`unique_id=1074`) with its Leather Armor identity, armor,
strength, magic-curse, dexterity, and indestructible powers.

Schema 1.32 adds the native-derived Hellfire The Rainbow Cloak base item and
unique definition (`unique_id=1073`) with its Cloak identity, armor, attribute,
resistance, life, and durability powers.

Schema 1.31 adds the native-derived Hellfire The Gladiator's Bane base item
and unique definition (`unique_id=1072`) with its Studded Leather Armor
identity, armor, get-hit, durability, and cursed-attribute powers.

Schema 1.30 adds the native-derived Hellfire Torn Flesh of Souls base item and
unique definition (`unique_id=1071`) with its Rags identity, armor, vitality,
get-hit, and indestructible powers.

Schema 1.29 adds the native-derived Hellfire Royal Circlet base item and
unique definition (`unique_id=1070`) with its Crown identity, attribute, mana,
armor, and light powers.

Schema 1.28 adds the native-derived Hellfire Gotterdamerung base item and
unique definition (`unique_id=1069`) with its Great Helm identity, attribute,
armor, get-hit, resistance, and light-curse powers.

Schema 1.27 adds the native-derived Hellfire Fool's Crest unique definition
(`unique_id=1068`), reusing the validated Helm base item with cursed
attributes, life, get-hit, and thorns powers.

Schema 1.26 adds the native-derived Hellfire OverLord's Helm unique definition
(`unique_id=1067`), reusing the validated Helm base item with attribute,
magic-curse, and durability powers.

Schema 1.25 adds the native-derived Hellfire Thinking Cap base item and unique
definition (`unique_id=1066`) with its Skull Cap identity, mana, spell-level,
resistance, and durability powers.

Schema 1.24 adds the native-derived Hellfire Helm of Spirits base item and
unique definition (`unique_id=1065`) with its Helm identity and life-steal
power.

Schema 1.23 adds the native-derived Hellfire Rod of Onan unique definition
(`unique_id=1064`), reusing the validated War Staff base item with
charged-spell, damage, and attribute powers.

Schema 1.22 adds the native-derived Hellfire Mindcry base item and unique
definition (`unique_id=1063`) with its Quarter Staff identity, magic,
charged-spell, all-resistance, and spell-level powers.

Schema 1.21 adds the native-derived Hellfire Naj's Puzzler unique definition
(`unique_id=1062`), reusing the validated Long Staff base item with magic,
dexterity, all-resistance, charged-spell, and life-curse powers.

Schema 1.20 adds the native-derived Hellfire The Protector unique definition
(`unique_id=1061`), reusing the validated Short Staff base item with vitality,
get-hit, armor, charged-spell, and thorns powers.

Schema 1.19 adds the native-derived Hellfire Thundercall base item and unique
definition (`unique_id=1060`) with its Composite Staff identity, to-hit,
lightning-damage, charged-spell, lightning-resistance, and light powers.

Schema 1.18 adds the native-derived Hellfire Gleamsong base item and unique
definition (`unique_id=1059`) with its Short Staff identity, mana,
strength-curse, vitality-curse, and charged-spell powers.

Schema 1.17 adds the native-derived Hellfire Storm Spire base item and unique
definition (`unique_id=1058`) with its War Staff identity,
lightning-resistance, lightning-damage, strength, and magic-curse powers.

Schema 1.16 adds the native-derived Hellfire Immolator unique definition
(`unique_id=1057`), reusing the validated Long Staff base item with
fire-resistance, fire-damage, mana, and vitality-curse powers.

Schema 1.15 adds the native-derived Hellfire Staff of Shadows base item and
unique definition (`unique_id=1056`) with its Long Staff identity,
magic-curse, to-hit, damage, light-curse, and fast-attack powers.

Schema 1.14 adds the native-derived Hellfire Dreamflange unique definition
(`unique_id=1055`), reusing the validated Mace base item with magic, mana,
magic-resistance, light, and spell-level powers.

Schema 1.13 adds the native-derived Hellfire Schaefer's Hammer base item and
unique definition (`unique_id=1054`) with its War Hammer identity, damage-curse,
lightning-damage, life, to-hit, lightning-resistance, and light powers.

Schema 1.12 adds the native-derived Hellfire The Cranium Basher unique
definition (`unique_id=1053`), reusing the validated Maul base item with
damage-modifier, strength, indestructibility, mana-curse, and all-resistance
powers. It also corrects Hammer of Jholm's normalized native Maul index.

Schema 1.11 adds the native-derived Hellfire Gnarled Root unique definition
(`unique_id=1052`), reusing the validated Spiked Club base item with to-hit,
damage, dexterity, magic, all-resistance, and armor-curse powers.

Schema 1.10 adds the native-derived Hellfire Baranar's Star base item and
unique definition (`unique_id=1051`) with its Morning Star identity, to-hit,
damage, attack-speed, vitality, dexterity-curse, and durability powers.

Schema 1.9 adds the native-derived Hellfire The Celestial Star base item and
unique definition (`unique_id=1050`) with its Flail identity, no-strength,
light, damage-modifier, and armor-curse powers.

Schema 1.8 adds the native-derived Hellfire Civerb's Cudgel unique definition
(`unique_id=1049`), reusing the validated Mace base item with demon damage,
dexterity-curse, and magic-curse powers.

Schema 1.7 adds the native-derived Hellfire Hammer of Jholm base item and
unique definition (`unique_id=1048`) with its Maul identity, fixed damage,
indestructibility, strength, and to-hit powers.

Schema 1.6 adds the native-derived Hellfire Crackrust base item and unique
definition (`unique_id=1047`) with its Mace identity, attributes,
indestructibility, all-resistance, damage, and spell-level powers.

Schema 1.5 adds the native-derived Hellfire Messerschmidt's Reaver base item
and unique definition (`unique_id=1046`) with its Great Axe identity, damage,
damage-modifier, attribute, life-curse, and fire-damage powers.

Schema 1.4 adds the native-derived Hellfire Hellslayer unique definition
(`unique_id=1045`), reusing the validated Battle Axe base item with strength,
vitality, damage, life, and mana-curse powers.

Schema 1.3 adds the native-derived Hellfire Aguinara's Hatchet base item and
unique definition (`unique_id=1044`) with its Small Axe identity, spell-level,
magic, and magic-resistance powers.

Schema 1.2 adds the native-derived Hellfire Stonecleaver unique definition
(`unique_id=1043`), reusing the validated Broad Axe base item with life,
to-hit, damage, and lightning-resistance powers.

Schema 1.1 adds the native-derived Hellfire Wicked Axe unique definition
(`unique_id=1042`), reusing the validated Large Axe base item with to-hit,
dexterity, vitality-curse, hit-curse, and indestructibility powers.

Schema 1.0 adds the native-derived Hellfire The Celestial Axe base item and
unique definition (`unique_id=1041`) with its Battle Axe identity, no-strength
requirement, to-hit, life, and strength-curse powers.

Schema 0.99 adds the native-derived Hellfire BloodSlayer base item and unique
definition (`unique_id=1040`) with its Broad Axe identity, damage, demon damage,
all-attribute-curse, and spell-level-curse powers.

Schema 0.98 adds the native-derived Hellfire Sharp Beak unique definition
(`unique_id=1039`), reusing the validated Large Axe base item with life,
magic-curse, and mana-curse powers.

Schema 0.97 adds the native-derived Hellfire The Mangler base item and unique
definition (`unique_id=1038`) with its Large Axe identity, damage,
dexterity-curse, magic-curse, and mana-curse powers.

Schema 0.96 adds the native-derived Hellfire The Grandfather base item and
unique definition (`unique_id=1037`) with its Great Sword identity, one-handed,
attribute, to-hit, damage, and life powers.

Schema 0.95 adds the native-derived Hellfire Doombringer base item and unique
definition (`unique_id=1036`) with its Bastard Sword identity, to-hit, damage,
all-attribute-curse, life-curse, and light-curse powers.

Schema 0.94 adds the native-derived Hellfire Inferno unique definition
(`unique_id=1035`), reusing the validated Long Sword base item with fire damage,
light, mana, and fire-resistance powers.

Schema 0.93 adds the native-derived Hellfire The Falcon's Talon base item and
unique definition (`unique_id=1034`) with its Scimitar identity, fast-attack,
to-hit, damage-curse, and dexterity powers.

Schema 0.92 adds the native-derived Hellfire Lightsabre unique definition
(`unique_id=1033`), reusing the validated Sabre base item with light,
lightning-damage, to-hit, and lightning-resistance powers.

Schema 0.91 adds the native-derived Hellfire Wizardspike unique definition
(`unique_id=1032`), reusing the validated Dagger base item with magic, mana,
to-hit, and all-resistance powers.

Schema 0.90 adds the native-derived Hellfire Shadowhawk base item and unique
definition (`unique_id=1031`) with its Broad Sword identity, light-curse,
life-steal, to-hit, and all-resistance powers.

Schema 0.89 adds the native-derived Hellfire The Bonesaw base item and unique
definition (`unique_id=1030`) with its Claymore identity, damage-modifier,
strength, magic/dexterity-curse, life, and mana-curse powers.

Schema 0.88 adds the native-derived Hellfire The Executioner's Blade unique
definition (`unique_id=1029`), reusing the validated Falchion base item with
native damage, life-curse, light-curse, and durability powers.

Schema 0.67 adds native monster resistance percentages to `monster_types.tsv`.
Encounter rows may override those values per instance; otherwise the validated
type metadata supplies the authoritative resistance projection.
The same type metadata now supplies baseline armor, damage channels, monster
level, and immutable capability flags when an encounter row omits them.
Native monster IDs remain variant-local compatibility metadata. The catalog
exposes reverse lookup only when an ID is unambiguous, preserving composed
base/Hellfire overlays whose variant-local IDs may overlap without moving TSV
parsing into the engine.

Schema 0.65 adds validated native level-range generation metadata through
`level_generation.tsv`. The Diablo activation exposes the native monster
density, multiplayer multiplier, and trap-percentage rules to the server-owned
module without embedding them in the host or presentation client.

The Hellfire monster catalog now also carries native IDs 6 through 11 with
validated baseline level, health, damage, and undead/capability metadata.

Schema 0.64 adds a validated native-compatible `quest_types.tsv` catalog for
all base and Hellfire quest identities. Quest-state rows must resolve a
declared stable quest identity while native quest IDs, routing levels, set-level
sources, and single-player availability remain external data.

Schema 0.63 adds a validated native-compatible `monster_types.tsv` catalog for
the base encounter and the first six Hellfire monster types. Encounter rows
must reference a declared stable monster type, while native IDs and baseline
health, damage, level, and immutable capabilities remain external data.

Schema 0.62 adds native-derived Hellfire Harlequin Crest, Veil of Steel, and
Arkaine's Valor item and unique definitions to the validated content overlay.
Their base-item identities and authoritative armor, resistance, attribute,
life, mana, and recovery powers are generated by the C# item catalog.

Schema 0.61 adds the native-derived Hellfire Optic Amulet and Ring of Truth
item and unique definitions to the validated content overlay. Their base-item
identities and unique powers are generated by the authoritative item catalog.

Schema 0.60 completes native rune-source identity mapping through the active
spell catalog. Rune of Lightning ID `48`, Greater Rune of Lightning ID `49`,
Greater Rune of Fire ID `50`, and Rune of Stone ID `51` resolve to the existing
stable Lightning Wall, Nova, Immolation, and Stone Curse behaviors. Rune of
Fire ID `47` now uses a direct BigExplosion-style authoritative splash with
native level-zero damage and radius-nine placement crawl.

Schema 0.57 externalizes Golem's native 12-frame death-animation timing on
spell row `52`. The server carries remaining terminal ticks through
snapshots, version-1 saves, canonical hashes, and protocol fields 34-35, then
removes the expired minion.

Schema 0.56 adds Etherealize (stable spell ID `53`, native `SpellID` `26`).
The server owns its level-scoped status identity, transition cleanup,
protocol/snapshot/hash persistence, and monster-damage immunity.

Schema 0.55 adds Golem (stable spell ID `52`, native `SpellID` `21`). The
server validates targeted summon placement, replaces the caster's prior owned
minion, and persists owner identity plus native life, armor, and damage state
through snapshots, saves, hashes, and protocol field 33. Golems use
server-owned deterministic nearest-monster targeting, movement, melee damage,
experience attribution, owner/level lifecycle cleanup, deterministic
breadth-first path planning around blocked cells, owner-facing native
random-walk ordering when idle, and externalized native death-animation timing
through terminal cleanup.

Schema 0.54 adds Nova (stable spell ID `18`, native `SpellID` `18`). The
existing authoritative radial controller and persistent lightning-ball
projectiles are now available through the shipped validated content row.

Schema 0.53 adds Lightning Wall (stable spell ID `50`, native `SpellID` `40`).
It reuses the server-owned wall-growth controller and persistent segment
machinery while external content supplies native lightning damage and lifetime
tuning.

Schema 0.52 adds Warp (stable spell ID `49`, native `SpellID` `42`). A
normalized external trigger table supplies native level destinations. The
server selects the nearest trigger, performs a five-cell destination crawl,
excludes occupied and trigger tiles, and applies the transition through the
existing server-owned level-action state.

Schema 0.51 adds Berserk (stable spell ID `48`, native `SpellID` `44`). The
server owns the radius-five target crawl, eligibility rules, resistance coin
flip, deterministic four-channel damage scaling, and persisted berserked
monster state. Mode-specific animation and light-radius presentation remain
client concerns in this bounded slice.

Schema 0.50 adds Jester (stable spell ID `47`, native `SpellID` `39`). The
server performs the native ten-way deterministic dispatch into the existing
authoritative Firebolt, Fireball, Fire Wall, Guardian, Chain Lightning, Town
Portal, Teleport, Apocalypse, or Stone Curse path; no client-side rules fork
is introduced.

Schema 0.49 adds Immolation (stable spell ID `45`, native `SpellID` `41`). The
server owns the moving controller, native 36-projectile radial burst, capped
spell-level speed bonus, and FireballBow movement/impact lifecycle through the
shared projectile state.

Schema 0.48 adds Ring of Fire (stable spell ID `44`, native `SpellID` `45`).
The server preserves the radius-three crawl order, first blocker termination,
native RNG advancement, and Fire Wall segment lifecycle.

Schema 0.47 adds Mana (stable spell ID `41`, native `SpellID` `37`). The server
owns the native LCG draws, Q6 clamping, effective-level scaling, and class
restoration multipliers.

Schema 0.46 adds Magi (stable spell ID `40`, native `SpellID` `38`) as a
server-owned immediate effect that restores current mana to its native maximum.

Schema 0.45 adds Reflect (stable spell ID `43`, native `SpellID` `43`) with
server-owned charge state, native level/character scaling, seeded reflection,
and protocol/snapshot persistence.

Schema 0.44 adds Town Portal (stable spell ID `39`, native `SpellID` `7`). The
server owns native five-cell placement, one active portal per caster, the fixed
town endpoint, bidirectional use, 100-tick expiry, and portal persistence in
snapshots, saves, hashes, and the shared protocol.

Schema 0.43 adds Telekinesis (stable and native spell ID `33`). The server
resolves the target through the shared cast intent, then authoritatively applies
remote item pickup, object activation/effects, or one-cell monster knockback.
Line-of-sight, ownership, inventory capacity, and target state are validated
before mana or item-source consumption.

Schema 0.42 makes the stable/native spell identity boundary explicit. The
authoritative `spell_id` remains the command and persistence identity while
`native_spell_id` records the source `SpellID`; legacy item spell names are
resolved through the activated catalog before cast-source validation.

Schema 0.40 adds Elemental (spell ID `31`). The server creates a typed moving
projectile, preserves the native first-destination retarget and 14-tick
nine-cell fire burst, and carries its current target, burst origin, phase, and
movement state through snapshots, saves, hashes, and the existing projectile
protocol fields.

Schema 0.41 adds Guardian (stable spell ID `7`, native `SpellID` `13`). The server creates a typed stationary
turret, preserves the native spawn search, duration, six-tile scan cadence,
Firebolt emission, line-of-sight filtering, and RNG ordering, and carries its
controller state through snapshots, saves, hashes, and the existing projectile
protocol fields.

Schema 0.39 adds Hellfire Rage (spell ID `22`). The server creates a typed
active/cooldown controller, preserves the native character-level lifetime
formula and minimum-life Q6 self-damage on cooldown expiry, rejects recasts
while either phase is active, and carries the phase and remaining ticks through
snapshots, saves, hashes, and the existing projectile protocol fields.

Schema 0.29 externalizes Holy Bolt as a typed projectile. Schema 0.30 adds the
Rogue Trap Disarm item capability, schema 0.31 adds Hellfire Blood Star, schema
0.32 adds Bone Spirit, and schema 0.33 adds Fire Wall. Each slice keeps its
native damage, resistance, collision, lifetime, and object-interaction rules
in the Diablo module while clients receive only authoritative projection.

Schema 0.34 adds Flame Wave (spell ID `15`). The server creates a one-tick
controller, derives the native perpendicular branch layout from the caster's
facing and target direction, and persists the resulting moving Fire segments
through existing projectile fields. Content supplies the controller lifetime,
branch count, effective-level divisor, and character-level damage bonus; no
new protocol field is required. Flame Wave branch placement, movement,
collision, breakable-object destruction, save validation, and replay manifest
identity remain server-owned.

Schema 0.35 adds Inferno (spell ID `21`). Its speed-32 controller emits at
most three stationary fire segments as it crosses new tiles. Segment lifetime,
native character-level and two-value damage draws, repeated collision, object
interaction, snapshot validation, and replay identity remain server-owned
through existing projectile state plus one content tuning column.

Schema 0.36 adds Chain Lightning (spell ID `16`). The server resolves its
one-tick target fan-out in native crawl order, then advances each bolt through
the existing Lightning controller and segment rules while retaining the chain
spell ID and effective level. Clients observe only the resulting authoritative
projectiles and damage events.

Schema 0.38 adds Apocalypse (spell ID `25`). The server resolves its native
row-major scan controller and persistent Physical boom entirely from external
spell tuning, including deterministic cursor progression, player-minion
exclusion, Hellfire line-of-sight filtering, and snapshot/protocol state.

Schema 0.37 adds Nova (spell ID `18`). The server resolves its one-tick native
radial controller in the nine-point quarter-radius order, emits four mirrored
balls per point, and advances each Lightning ball independently through 16.16
movement and continuing target collision. Clients observe only authoritative
projectiles and damage events.

## Lua transition

Lua remains a frozen native-client compatibility layer. New authoritative
behavior must be added through external content, the C# domain/module boundary,
or the protocol. Lua can be removed only after its data, gameplay, presentation,
and developer-tooling capabilities have replacements with parity coverage.

See [lua-capability-inventory.md](lua-capability-inventory.md) for the remaining
replacement gates.

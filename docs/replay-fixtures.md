# Deterministic Replay Fixtures

Replay fixtures are the cross-language behavioral baseline for the native
engine and C# authoritative server. They are UTF-8 JSON test artifacts, not the
runtime Protobuf transport.

A fixture records its format and protocol versions, content identity, tick rate,
RNG seed, initial state, authoritative command order, checkpoint hashes, and an
optional final hash. Commands are ordered by `(target_tick,
server_receipt_sequence)` and retain client sequences so retry and deduplication
behavior can be characterized.

An optional top-level `player_action_timing` object records the activated
walking, selected attack-profile, and casting frame boundaries used by a
deferred-action fixture. Fixtures that can kill a player may also declare the
optional native `death_frames` value. Its presence enables native-timed command
execution in both runners. The managed tests cross-check those fixture values
against the validated base `class_animations.tsv` row, preventing the replay
from becoming a second unverified timing definition.

Both runners initialize their authoritative gameplay RNG from `rng_seed`.
Canonical state includes the current nonzero RNG state, and behavior-specific
projectile runtime fields are hashed in stable entity order, so an incorrect
draw count or path state diverges at the first affected checkpoint.

Gameplay initial state may declare `ability_spell_id` and a unique
`memorized_spells` array of native-bounded spell IDs and nonzero levels. Both
runners use that ownership state when admitting casts, and include nonempty
spell knowledge in the same tagged canonical-hash extension used by snapshots.
An optional `equipment` array carries protocol-shaped slot, item seed, native
item index, type/class, value, identified value, flags, and usability state.
Both runners validate unique native slots and hash the resulting equipment.
Attack profile, ranged admission, combat mode, and native speed flags are then
derived from that state rather than declared as separate fixture outcomes.
Optional `facing_direction` uses the native zero-through-seven ordinal. Both
runners apply accepted action rotations, conditionally hash non-South facing,
and use the direction for equipped-item death placement.
An initial object may also declare `object_missile_passable`,
`object_breakable`, and `object_destroyed`; both runners validate the same flag
invariants and hash nondefault collision state. An initial combat target may
declare its current tile, adjacent future tile, movement state, and
petrification. It may also declare armor class, attack damage, and aggro range
for autonomous combat. Both runners reject inconsistent occupancy or negative
combat data, complete and begin movement on the same tick boundaries, and apply
the native rule that only a petrified walker is hittable at its future tile.
`initial_state.secondary_player` may declare a second stable participant for
multiplayer collision fixtures, including identity, entity ID, Q6 life,
position, level, and character level. The runners initialize it before the
timeline and tests assert its resulting life directly. Optional class, Magic,
Dexterity, friendly mode, block capability, combat-mode ordinal, and mode end
tick configure richer multiplayer fixtures. A top-level `game_variant` selects
strict `Diablo` or `Hellfire` rules for variant-sensitive draw order. A
top-level `game_mode` selects `SinglePlayer` or `Multiplayer`; legacy fixtures
default to single-player. Initial state may mirror validated level content with
`suppress_multiplayer_death_drops`, allowing mode-sensitive death parity without
participant-count heuristics in either runner.
Checkpoint hashes cover the complete participant projection. Protocol snapshots
put the receiving player first, but both runners sort every player by entity ID
before hashing, so receiver-relative wire order cannot alter a checkpoint. A
secondary player's resources, attributes, equipment, combat mode, and inventory
topology therefore expose multiplayer divergence directly.
An initial state with one canonical Gold pile may add
`inventory_filler_count`. Both runners place that many deterministic one-cell
non-Gold items into the remaining native inventory cells. This forces the same
capacity boundary without encoding presentation-only filler details in each
fixture.

## Rules

- Required identity metadata must match the runner; mismatches invalidate the
  fixture instead of producing a comparison.
- State hashes use the protocol's canonical field order and exclude localized
  strings, pointers, wall-clock values, and presentation state.
- Structured fixtures checkpoint every command tick and may add checkpoint-only
  ticks. Both runners advance autonomous simulation to those ticks before
  hashing, so projectile, AI, status, and other server-owned timelines do not
  require no-op client commands.
- Additive fixture metadata may be ignored. Changes to command meaning or
  canonical hash order require both runners and their parity vectors to change
  together.

## Current Coverage

Fixtures under `test/fixtures/replay` cover store transactions against synthetic
and checked-in content, movement and combat, healing, portals, world-item
pickup, status expiry, object-linked quest advancement, and multi-level world
occupancy. The native and C# runners preserve the same baseline resources,
attributes, equipment, belt, inventory footprints, stock, item state, level
state, class and spell knowledge, monsters, objects, quests, projectiles, and
world items in their canonical projections. Spell fixtures now reject unowned
casts and distinguish free class abilities from mana-consuming memorized spells;
protocol-level managed and native-client tests separately cover exact charged
and consumable item references.

`gameplay-player-action-lifecycle.json` follows a Warrior through an eight-frame
reserved walk, a deferred unarmed strike, chaining at the native ready boundary,
and a deferred memorized healing cast. Seven shared checkpoints prove action
payloads and modes before effects, committed position and melee damage at their
effect ticks, delayed mana/healing, and final cleanup.

`gameplay-player-action-ranged-diablo.json` and
`gameplay-player-action-ranged-hellfire.json` equip the same Rogue fast Bow and
attack a nonadjacent target. The Diablo timeline skips one native logic frame,
applying damage at tick 15 and admitting its chained move at tick 16; Hellfire
applies at tick 16 and admits the move at tick 17. Five checkpoints per fixture
prove the variant divergence and common final state. Focused module tests retain
additional faster/fastest melee-speed coverage.

`gameplay-player-death-lifecycle.json` starts a Warrior movement action while
an autonomous monster approaches, attacks on tick 2, and cancels that action.
The already-applied Death action remains through tick 40, clears on tick 41,
and leaves the player in Death mode. The initial 101-gold inventory pile keeps a
physical value of 51 while the native cached wallet becomes 50, and one newly
seeded 50-gold world item appears at the first native crawl tile. The split
advances the shared RNG exactly once. Both runners derive death duration and
Gold identity from checked base-content behavior and hash the wallet, physical
inventory pile and grid cell, world item, placement, and RNG state at the same
checkpoints.

`stores/gold-topology-parity.json` begins with one 100-gold pile at native cell
30. Purchase spends and removes that pile before placing the bought item at cell
31; sale removes the item and creates a newly seeded proceeds pile at native
cell 39; mana refill reduces it again. Five checkpoints prove the wallet,
physical pile, grid, store state, mana, and one exact RNG advance in both
runners.

`gameplay-gold-pickup-overflow.json` starts with 4,990 Gold, 39 occupied
inventory cells, and a 75-Gold world pile. Tick 1 tops the existing pile to
5,000, leaves 65 on the authoritative cursor with the original world seed, and
does not advance RNG. Tick 2 drops that held remainder onto the adjacent tile
as stable world entity `0x40000000`. Both checkpoints hash the inventory grid,
wallet, held item, world item, and RNG state in C++ and C#.

`gameplay-player-level-transition.json` uses a portal on tick 1. Both runners
apply level 2 and its destination immediately, expose an already-applied
NewLevel action through the one-tick barrier, and return the living player to
Stand at tick 2.

`gameplay-player-restart-town.json` starts the player and attacking monster on
dungeon level 1. The monster cancels movement by killing the player on tick 2;
terminal Death is reached on tick 41; and an explicit restart command on tick
42 applies town level 0, tile `(75,68)`, 64 Q6 life, zero mana, and action kind
6. Tick 43 proves the one-tick NewLevel barrier clears to Stand. The fixture's
`combat_target_level_id` keeps monster and player level identity explicit in
both runners and their canonical hashes.

`gameplay-charged-bolt.json` gives both runners seed `0x12345678`, Magic 35,
and learned spell level 2. Both must create five bolts with the
native Diablo-LCG path/damage/animation draw order, initial 16.16 velocity, and
the same post-cast RNG state. `gameplay-charged-bolt-resolution.json` adds a
resistant target and a tick-5 checkpoint with no intervening command. Both
runners must remove the same out-of-bounds bolt, resolve the same two hits after
identical RNG draws, enter the same impact phases, and retain `1696/64` target
health. `gameplay-charged-bolt-object-collision.json` places a breakable blocking
object on the shared path; both runners destroy it, remove the colliding bolt,
and retain the same three surviving projectiles without consuming hit RNG.
`gameplay-charged-bolt-moving-occupancy.json` places a petrified moving target's
future tile on the shared path; both runners consume the same guaranteed-hit
roll, damage the target there, and retain its frozen two-cell occupancy. Diablo
module tests also cover normal walkers passing through future-tile collision,
sub-point Q6 damage, and movement cancellation on death.
`gameplay-charged-bolt-pvp.json` places a second player on the shared path;
both runners consume the same native hit and damage draws, damage that player,
and reach identical complete two-participant checkpoints.
`gameplay-charged-bolt-hellfire-block.json` gives that participant a usable
shield and guaranteed block chance. Both runners consume hit, block, damage,
and Hellfire rotation-selection draws in order, enter Block immediately so
later bolts cannot block again, and reach the same RNG-backed checkpoints. A
seeded live Godot/server test proves the host seed reaches a charged-item cast.

`gameplay-firebolt-resolution.json` uses the activated base Firebolt row, a
level-3 Sorcerer with Magic 35, and seed `0x12345678`. Tick 1 proves native
class-adjusted Q6 mana, randomized damage, speed-26 16.16 velocity, and the
post-cast RNG state. Tick 2 proves swept collision, magic hit RNG, one-quarter
fire-resistance damage, exact `400/64` monster health, and impact rollback.
Tick 3 proves deterministic impact expiry. The native parser and managed loader
also reject fire resistance outside the authoritative `-100..100` range.

`gameplay-fireball-resolution.json` uses activated Fireball, a level-12
Sorcerer with Magic 35 and effective spell level 3, a petrified adjacent target,
and seed `0x12345678`. Tick 1 proves class-adjusted Q6 mana, two-draw native
damage and RNG order, level growth, and speed-22 16.16 velocity. Tick 3 proves
the direct hit, exact shifted center splash, and impact entry; ticks 16 and 17
prove the final active explosion tick, expiry, exact Q6 health, and final RNG
state. Both runners validate all four checkpoints.

`gameplay-lightning-resolution.json` uses activated Lightning, a level-12
Sorcerer with Magic 35 and effective spell level 3, a petrified
lightning-resistant target, and seed `0x12345678`. Ticks 1 and 2 prove native
mana, controller velocity, explicitly sequenced per-update damage RNG,
same-tick segment activation, exact Q6 resistance damage, and persistent
repeated collision. Ticks 8 and 9 prove segment expiry, target death, retained
controller/segment population, and the final RNG state. Both runners validate
all four checkpoints; invalid lightning resistance is rejected before replay.

`gameplay-flash-resolution.json` uses activated Flash, a level-12 Sorcerer with
Magic 35 and effective spell level 3, a petrified magic-resistant target in the
Top layer, and seed `0x12345678`. Ticks 1 and 2 prove native mana, independent
Bottom/Top creation damage, layer ordering, repeated magic-hit RNG, and exact Q6
resistance damage. Ticks 19 and 20 prove final-tick collision, simultaneous
layer expiry, stable post-expiry state, and the final RNG value. Both runners
validate all four checkpoints; invalid magic resistance is rejected before
replay.

`gameplay-healing-resolution.json` uses activated Healing, a level-5 Rogue with
effective spell level 3, an explicit larger maximum life, and seed `0x12345678`.
Ticks 1 and 2 prove native mana, character/spell-level RNG order, the Rogue's
half-again Q6 modifier, exact event amounts, and life-cap truncation. Tick 3
proves that an already-full cast remains accepted and consumes mana and RNG
without emitting zero healing. Both runners validate all three checkpoints;
both loaders reject an initial maximum below current life.

`gameplay-heal-other-resolution.json` uses activated Heal Other, a level-5 Monk
with effective spell level 3, a living second participant, and seed
`0x12345678`. Tick 1 proves admission and the native Monk casting lifecycle;
tick 14 proves class-adjusted mana, Healing-compatible RNG order, the distinct
300-percent modifier, exact Q6 target healing, and complete multiplayer state;
tick 18 proves action cleanup. Both runners validate all three checkpoints.

`gameplay-teleport-resolution.json` uses activated Teleport, a level-5 Rogue
with effective spell level 3, and a requested destination occupied by a second
player. The next native crawl candidates are occupied by a monster and an
object, forcing selection of `(9, 10)`. Ticks 1, 13, 14, and 16 prove deferred
cast timing, exact class-adjusted Q6 mana, radius ordering, participant-aware
occupancy, first-controller-update movement after the monster phase,
controller expiry, and action cleanup. Both runners validate all four
checkpoints.

`gameplay-phasing-resolution.json` uses activated Phasing, a level-5 Warrior
with effective spell level 3, East-facing initial state, and seed `0x12345678`.
The client-supplied target points south-east but cannot change facing or select
the destination. Ticks 1, 15, 16, and 20 prove deferred class-owned cast timing,
the native row-major 36-candidate list, one RNG draw selecting `(24, 15)`, exact
Q6 mana, first-update movement, controller expiry, and action cleanup. Both
runners validate all four checkpoints; focused module tests also prove that an
empty candidate set consumes mana without advancing RNG or creating a
controller.

`gameplay-resurrection-resolution.json` uses activated Resurrection, a level-5
Rogue, and a dead second participant with explicit maximum life. Occupied
initial offsets and a portal exit force the native placement search to choose
`(9, 10)`. Ticks 1, 13, 16, 27, and 28 prove deferred cast timing, exact
fixed-minimum Q6 mana, action cancellation, ten-life and zero-mana revival,
complete multiplayer hashing, beam creation at the old position, its final
active tick, expiry, and cast-action cleanup. Both runners validate all five
checkpoints.

`gameplay-mana-shield-resolution.json` uses activated Mana Shield, learned
spell level 1, and a deterministic adjacent monster attack. Ticks 0 through 5
prove initial fixed-minimum mana consumption, active-state hashing, an accepted
active-cast fizzle with no second charge, native Q6 damage reduction, mana
depletion, reverse reduction of the unpaid remainder, life damage, and shield
cleanup. Both runners validate all six checkpoints.

`gameplay-stone-curse-resolution.json` uses learned Stone Curse level 3 and a
walking monster whose future tile is reached through the native radius crawl.
Ticks 0, 1, 2, 144, 145, and 146 prove a source-preserving no-target fizzle,
future-cell selection, exact mana and duration, a consuming non-refreshing
recast, the last active controller tick, living release, and resumed movement.
Both runners validate all six checkpoints.

`gameplay-infravision-resolution.json` starts with two Infravision scrolls in
distinct belt slots. Casts at ticks 0 and 1 prove exact source consumption,
typed status hashing, and recast duration union. Ticks 1,584 and 1,585 prove the
last active tick and server-owned expiry. Both runners validate all four
checkpoints.

`gameplay-search-resolution.json` uses the level-5 Monk's external Search
ability. Casts at ticks 0 and 1 prove free ability admission, the native
255-tick term, typed status hashing, and additive recast after one elapsed tick.
Ticks 509 and 510 prove the last active tick and server-owned expiry. Both
runners validate all four checkpoints.

Notable shared fixtures include:

- `stores/basic-buy.json` for the versioned envelope and a minimal purchase;
- `stores/transaction-parity.json` for purchase, sale, and refill transitions;
- `stores/gold-topology-parity.json` for physical Gold across purchase, sale,
  inventory placement, and refill;
- `gameplay-gold-pickup-overflow.json` for partial canonical Gold pickup,
  cursor-held remainder identity, RNG stability, and explicit adjacent drop;
- `stores/base-content-purchase.json` for the checked-in base content pack;
- `gameplay-player-action-lifecycle.json` for native-timed movement, melee,
  attack-to-cast chaining, cast effects, and action cleanup;
- `gameplay-player-action-ranged-diablo.json` and
  `gameplay-player-action-ranged-hellfire.json` for equipment-derived Bow mode,
  nonadjacent targeting, speed flags, and variant-specific frame timing;
- `gameplay-player-death-lifecycle.json` for autonomous monster approach and
  attack, action cancellation, content-timed Death, native physical-stack and
  cached-wallet divergence for odd Gold, deterministic world-item
  placement/seed, and terminal cleanup;
- `gameplay-multiplayer-death-suppression.json` for explicit multiplayer mode,
  special-level Gold suppression, unchanged inventory, and zero death-drop RNG;
- `gameplay-player-equipped-death-drops.json` for native facing rotation across
  equipment slots, occupied preferred-tile fallback, retained item identity,
  identified-value world price, and zero death-drop RNG;
- `gameplay-player-level-transition.json` for immediate portal destination and
  the one-tick NewLevel action barrier;
- `gameplay-player-restart-town.json` for terminal-death admission,
  content-selected town recovery, resource reset, and action kind 6 cleanup;
- `gameplay-charged-bolt.json` for seeded native/C# projectile creation parity;
- `gameplay-charged-bolt-resolution.json` for checkpoint-only tick advancement,
  hit RNG, resistance, Q6 health, and impact lifecycle parity;
- `gameplay-charged-bolt-object-collision.json` for persistent breakable-object
  collision state and projectile removal parity;
- `gameplay-charged-bolt-moving-occupancy.json` for native current/future
  occupancy, petrified future-tile collision, and guaranteed-hit RNG parity;
- `gameplay-charged-bolt-pvp.json` for second-player collision, Q6 damage, and
  hit/damage RNG parity;
- `gameplay-charged-bolt-hellfire-block.json` for animation-mode block
  admission and the Hellfire-only blocked-missile RNG draw;
- `gameplay-firebolt-resolution.json` for native mana/damage scaling, swept
  speed-26 movement, fire resistance, fractional health, impact, and RNG parity;
- `gameplay-fireball-resolution.json` for two-roll damage, level-capped speed,
  swept direct collision, shifted splash, persistent impact, and RNG parity;
- `gameplay-lightning-resolution.json` for native controller/segment creation,
  same-tick repeated collision, level-scaled lifetime, Q6 resistance, death,
  and RNG parity;
- `gameplay-flash-resolution.json` for native dual-layer creation, repeated
  3x3 collision, magic resistance, final-tick expiry, and RNG parity;
- `gameplay-healing-resolution.json` for randomized class-scaled Q6 healing,
  life-cap truncation, and full-life mana/RNG consumption;
- `gameplay-teleport-resolution.json` for native crawl ordering, multiplayer
  occupancy, delayed movement, source consumption, and controller expiry;
- `gameplay-resurrection-resolution.json` for other-player admission, native
  revival placement and resources, complete participant hashing, and beam
  lifetime;
- `gameplay-mana-shield-resolution.json` for source consumption, active-cast
  fizzle, native Q6 mana absorption, depletion reversal, and state-hash parity;
- `gameplay-pvp-ear-death.json` for requested and persisted character names,
  lethal Charged Bolt PvP damage, equipment retention, native ear identity and
  cursor presentation, zero-extra-RNG placement, and complete C++/C# dynamic
  item hashing;
- `gameplay-multi-level-occupancy.json` for preserving entities across a portal
  transition and persistence round-trip.

## Verification

`.github/workflows/authoritative-parity.yml` runs the native `replay_test` and
the C# test suite as separate processes against the same fixtures. A checkpoint
failure identifies the first divergent authoritative tick. Locally, use the
normal native GTest build described in `docs/ai/msvc-build.md` and:

```powershell
dotnet run --project server/tests/Devilution.Server.Tests/Devilution.Server.Tests.csproj
```

Future fixtures should prioritize uncovered legacy parity rather than repeat
already-characterized transitions: seeded quest pools, mod/content reload
ordering, Hellfire activation, cursor-held pickup overflow, and broader
combat/event ordering.

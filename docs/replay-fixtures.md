# Deterministic Replay Fixtures

Replay fixtures are the cross-language behavioral baseline for the native
engine and C# authoritative server. They are UTF-8 JSON test artifacts, not the
runtime Protobuf transport.

A fixture records its format and protocol versions, content identity, tick rate,
RNG seed, initial state, authoritative command order, checkpoint hashes or
projection-only markers, and an
optional final hash. Commands are ordered by `(target_tick,
server_receipt_sequence)` and retain client sequences so retry and deduplication
behavior can be characterized. An optional command-level `player` routes a
command to a declared participant; omitted commands target the primary player,
and client sequences are unique per participant.

An optional top-level `player_action_timing` object records the activated
walking, selected attack-profile, and casting frame boundaries used by a
deferred-action fixture. Fixtures that can kill a player may also declare the
optional native `death_frames` value. Its presence enables native-timed command
execution in both runners. The managed tests cross-check those fixture values
against the validated base `class_animations.tsv` row, preventing the replay
from becoming a second unverified timing definition.

An optional `quest_selection` object records a native-compatible single-player
pool seed and stable `pool_id`/`quest_ids` entries. Both runners sort pools,
select the same unavailable quest identities without consuming gameplay RNG,
and reject subsequent advancement of an unavailable quest.

An optional top-level `initial_projectiles` array seeds server-owned projectiles
before the first simulation tick. It carries the stable entity/source/target
identities, level and travel coordinates, damage channel, behavior, speed,
remaining lifetime, and impact-phase metadata needed to replay a native
projectile lifecycle without moving AI admission into the fixture format.

Both runners initialize their authoritative gameplay RNG from `rng_seed`.
Canonical state includes the current nonzero RNG state, and behavior-specific
projectile runtime fields are hashed in stable entity order, so an incorrect
draw count or path state diverges at the first affected checkpoint.
This includes deferred native attack damage draws. Object effect, trap, and
quest-progress extensions, plus quest log/native metadata, use the same
optional tagged fields in both projections.

Gameplay initial state may declare `ability_spell_id` and a unique
`memorized_spells` array of native-bounded spell IDs and nonzero levels. Both
runners use that ownership state when admitting casts, and include nonempty
spell knowledge in the same tagged canonical-hash extension used by snapshots.
An optional `equipment` array carries protocol-shaped slot, item seed, native
item index, type/class, value, identified value, flags, and usability state.
Elemental replay cases may also carry `identified`, `magical`,
`fire_min_damage`, `fire_max_damage`, `lightning_min_damage`, and
`lightning_max_damage`; these fields are optional and default to the native
zero/false values. Melee attacks select `FireDamage`/`LightningDamage`, while
ranged attacks select `FireArrows`/`LightningArrows`, so arrow-only effects can
be checked independently. Both runners validate unique native slots and hash
the resulting equipment. Attack profile, ranged admission, combat mode, and
native speed flags are then derived from that state rather than declared as
separate fixture outcomes.

Fallen parity fixtures may declare `combat_target_fallen_stand_animation_frame`,
`combat_target_fallen_stand_animation_tick`, and
`combat_target_fallen_rally_ticks`. Both runners validate the native bounds and
advance the authored 11-frame/3-tick stand animation during simulation. At the
animation boundary they perform the native `FlipCoin(4)` rally admission,
apply the intelligence-scaled Q6 self-heal, and project the
`30 * intelligence + 105` rally timer. Non-default Fallen state is included in
the tagged `godevilution-monster-fallen-rally-v1` checkpoint extension. The
replay path models the primary target plus an optional bounded companion,
including one-tick pursuit beyond normal aggro range with native future
occupancy and rally-timer decrement. The companion form captures the
same-level, in-radius group-rally propagation boundary and its ordered
stand-animation/timer update;
`gameplay-fallen-rally.json` covers admission and
`gameplay-fallen-rally-pursuit.json` covers the pursuit tick. Multi-actor rally
propagation is covered by `gameplay-fallen-rally-companion.json`.
That fixture uses the additive `combat_target_companion_*` fields for one
same-level Fallen at the leader's rally radius; companion entity identity,
position, vitality, native AI, animation, and rally timer are validated and
hashed in entity order. The companion contract requires a nonzero distinct
leader, the same level, and the exact `Fallen` AI identity in both loaders;
malformed companion state is rejected before execution. The bounded shape is deliberate: larger pack
topologies remain live-world coverage until their replay state is specified.

The authoritative server represents every player bow shot as a `WeaponArrow`
projectile. A normal bow emits one arrow; native `MultipleArrows` emits the
first shot at the attack effect boundary and two spread shots with a two-tick
activation delay. Captured Q6 and elemental channels are part of the snapshot
hash, allowing replay and reconnect tests to observe travel, impact, and the
complete volley.

`gameplay-fire-wall-resolution.json` covers the player Fire Wall cast boundary
and persistent wall geometry. The controller expires after seven simulation
ticks while emitting deterministic center and two-branch segments; segment
damage and lifetime retain the native character/spell-level scaling and are
included in canonical projectile hashing. The managed runner loads the
validated base `spells.tsv` catalog and checks the active content-manifest hash
used by the native runner.

`gameplay-lightning-wall-resolution.json` covers the same controller geometry
for Lightning Wall, including its distinct lightning damage scale and long
persistent-segment lifetime. Segments retain the shared behavior identity but
project as Lightning Wall visuals from their damage channel.

`gameplay-ring-of-fire-resolution.json` covers the player Ring of Fire cast,
including the native radius-three crawl, deterministic Fire Wall segment
creation, persistent segment lifetime, and canonical projectile hashing.

`gameplay-nova-radial-fan-resolution.json` covers the player Nova cast,
including the one-tick controller, native 36-ball radial fan ordering,
first-tick movement, and canonical projectile hashing.

The Lightning Wall, Ring of Fire, Nova, and Flame Wave runners load the
validated base `spells.tsv` catalog and check the active `devilution.base`
manifest identity.

The Chain Lightning, Immolation, Elemental, and Guardian runners likewise load
the validated base `spells.tsv` catalog and check the active `devilution.base`
manifest identity.

`gameplay-elemental-resolution.json` covers the player Elemental cast,
including the initial fire projectile, native retargeting lifecycle, and
canonical projectile hashing.

`gameplay-guardian-resolution.json` covers the player Guardian cast, including
the one-tick turret admission, native 48-position firing arc, Firebolt
creation, controller GuardianState transition, and projectile lifetime. Its
timeline checkpoints intentionally omit snapshot hashes until the compact
native monster projection and full managed monster snapshot share the same
canonical shape.

`gameplay-rage-resolution.json` covers the Hellfire Rage cast, including the
active controller, duplicate-cast suppression, cooldown transition, bounded
Q6 self-damage, and expiry removal.

`gameplay-inferno-resolution.json` covers the player Inferno cast, including
the moving controller, deterministic three-segment emission, and persistent
segment lifetimes.

`gameplay-apocalypse-resolution.json` covers the player Apocalypse cast,
including row-major target scanning, physical boom emission, target impact,
and boom lifetime.

The Rage, Inferno, and Apocalypse runners load the validated base `spells.tsv`
rows and check the active `devilution.base` content-manifest identity.

`gameplay-magi-resolution.json` covers the player Magi cast, including the
native full-mana restoration boundary.

`gameplay-mana-resolution.json` covers the player Mana cast, including seeded
character-level and memorized-spell-level restoration rolls and the native
class multiplier.

The Magi, Mana, Reflect, Etherealize, and Berserk runners likewise load the
validated base `spells.tsv` catalog and check the active `devilution.base`
manifest identity.

The Warp, Golem, Jester, and Town Portal runners also load the validated base
`spells.tsv` catalog and check the active `devilution.base` manifest identity.

`gameplay-telekinesis-world-item.json` covers a memorized Telekinesis cast that
targets a server-owned world item, preserving exact target coordinates,
inventory first-fit placement, purchased-tick state, mana consumption, and the
canonical item hash across native and managed runners.

`gameplay-telekinesis-object.json` covers a memorized cast against an authored
world object, preserving exact target coordinates, object activation, an
authored experience effect, mana consumption, and canonical player/object state
hashing across native and managed runners.

`gameplay-telekinesis-monster-knockback.json` covers a memorized cast against a
living combat target, preserving exact target coordinates, one-cell knockback,
movement state, mana consumption, and canonical monster state hashing across
native and managed runners.

The rejection fixtures cover the inverse boundaries: `gameplay-telekinesis-
invalid-target.json` rejects an entity whose coordinates do not match,
`gameplay-telekinesis-insufficient-mana.json` rejects before consuming mana,
and `gameplay-telekinesis-unmemorized.json` rejects spell-source admission.
Each case verifies that the world item, player resources, and canonical hash
remain unchanged in both runners.

The managed Telekinesis runner loads the validated base `spells.tsv` catalog and
checks the active `devilution.base` content-manifest identity.

`gameplay-flame-wave-resolution.json` covers the player Flame Wave cast,
including the one-tick controller, deterministic center/branch geometry,
per-segment damage, same-tick movement, and canonical projectile hashing.

`gameplay-chain-lightning-resolution.json` covers the player Chain Lightning
cast, including the one-tick controller, deterministic native crawl fan-out,
Lightning bolt movement, impact segments, and canonical projectile hashing.

`gameplay-immolation-radial-fan-resolution.json` covers the player Immolation
cast, including the one-tick controller, deterministic radial fan ordering,
Fireball movement, and canonical projectile hashing.

`gameplay-spell-cast.json` covers the basic self-healing cast. The managed
runner loads its mana and healing values from the hash-checked
`test/content/replay/spell-cast` content pack; native execution continues to
validate the same replay contract and checkpoint.
Native GoatRanged and SkeletonRanged content rows explicitly select their
validated `Arrow`/`MonsterArrow` identities, while older `Targeted` rows remain
compatible. The authoritative module projects `MonsterArrow` missiles with
captured physical damage and per-tick player collision. Managed combat and
snapshot tests cover this lifecycle, and the compact cross-runner fixture now
covers seeded MonsterArrow travel, primary-player impact, impact-phase removal,
and secondary-participant routing. Companion fixtures cover autonomous
GoatRanged nearest-player selection, seeded GoatRanged close retreat and
post-shot delay, SkeletonRanged clear-lane admission, and seeded
Succubus/Lazarus BloodStar close retreat, post-shot delay, and
admission/travel/impact state, including secondary-player
block resolution, and Lich-family MonsterMagic admission, hit, miss,
nearest-participant selection, and secondary-participant impact resolution;
the Mega InfernoController moving fire-stream controller, segment lifetime,
recovery-delay lifecycle, and nearest-participant selection, including seeded
multiplayer AI-stream advancement. Mega fixtures load their spell and
native missile rows from
the hash-checked `test/content/replay/mega-inferno` content pack.
The seven Blood Star fixtures likewise load the Succubus and Lazarus Succubus
`BloodStar` mappings from the hash-checked `test/content/replay/blood-star`
content pack.
The nine Monster Magic fixtures likewise load their `OrangeFlare` mappings
from the hash-checked `test/content/replay/monster-magic` content pack.
The nine Monster Arrow fixtures load their GoatRanged and SkeletonRanged
`Arrow` mappings from the hash-checked `monster-arrow` and
`monster-arrow-admission` content packs.
The nine Hork Spawn fixtures load their `HorkDemon` mapping from the
hash-checked `monster-hork-spawn` content pack, including its summoned monster
type row and level-generation rule. Snake delay fixtures load the default
`SnakeCharge` mapping from `monster-snake-delay`, while the charge
lifecycle fixture loads its explicitly tuned mapping from
`monster-snake-charge`.
The autonomous GoatRanged fixture loads its explicit Arrow mapping from the
hash-checked `monster-arrow-autonomous` pack; its compact native projection
preserves the pack’s retreat thresholds and native defaults when omitted.
The four Counselor fixtures load their native Counselor type and timing bands
from the hash-checked `test/content/replay/counselor` content pack. The Zhar
and Lazarus fixtures exercise the corresponding native CounselorAi aliases.
The three GoatMelee fixtures load their native AiAvoidance and adjacent-attack
tuning from the hash-checked `test/content/replay/goat-melee` content pack.
The six Gargoyle fixtures load native safety, healing, avoidance, and
adjacent-attack tuning from the hash-checked `test/content/replay/gargoyle`
content pack. The multiplayer Gargoyle fixture also covers nearest-participant
selection through the autonomous avoidance branch.
The two SkeletonMelee delay fixtures load native chance bands and delay
formulas from the hash-checked `test/content/replay/skeleton-melee` content
pack.
The Bat retreat, Fat movement, and Zombie action fixture families likewise load
their
native AI tuning from the hash-checked `bat-retreat`, `fat-movement`, and
`zombie-action` content packs.
The Counselor target fixture, Rhino charge fixtures, the SkeletonMelee target
fixture, the Snake charge target fixture, and all three Scavenger corpse-healing
fixtures load
their native charge, movement, delay, and healing tuples from the hash-checked
`rhino-charge` and `scavenger-corpse` content packs.
The Sneak conceal/reveal fixtures, including the multiplayer target fixture,
load their complete stealth distance and
retreat tuple from the hash-checked `sneak` content pack.
The elemental equipment and ranged-arrow fixtures validate the canonical
`devilution.base` content manifest before replaying their equipment channels.
Optional `facing_direction` uses the native zero-through-seven ordinal. Both
runners apply accepted action rotations, conditionally hash non-South facing,
and use the direction for equipped-item death placement.
An initial object may also declare `object_missile_passable`,
`object_breakable`, and `object_destroyed`; both runners validate the same flag
invariants and hash nondefault collision state. Quest-linked object interaction
fixtures should declare `object_quest_id` explicitly so the replay models the
authored link required by authoritative command admission and quest progression.
An initial combat target may
declare its current tile, adjacent future tile, movement state, and
petrification. It may also declare armor class, attack damage, and aggro range
for autonomous combat. Both runners reject inconsistent occupancy or negative
combat data, complete and begin movement on the same tick boundaries, and apply
the native rule that only a petrified walker is hittable at its future tile.
Optional `combat_target_to_hit`, `combat_target_to_hit_special`, and paired
`combat_target_min_damage`/`combat_target_max_damage` fields expose native
monster accuracy and damage channels for autonomous special attacks. The
`gameplay-rhino-charge.json` fixture uses them to verify the server-owned charge
landing and knockback transition.
Native monster runtime seeds may be supplied as the paired
`combat_target_monster_item_seed` and `combat_target_monster_ai_seed` fields.
The multiplayer AI seed advances from the monster-owned stream at each
authoritative tick; generic melee then uses that stream for hit and block rolls.
The secondary-player native-AI-seed fixture verifies seed advancement, block
mode timing, and the canonical hash in both runners.
SkeletonMelee fixtures may carry
`combat_target_skeleton_melee_delay_ticks` to replay an admitted native delay
or preserve a failed adjacent-attack countdown across checkpoint ticks.
Snake fixtures may carry `combat_target_snake_delay_ticks` to replay an
admitted distant delay or preserve a failed adjacent-attack countdown across
checkpoint ticks. The paired Snake fixtures cover both native delay bands.
The `gameplay-snake-charge-lifecycle.json` fixture covers the mapped
`SnakeCharge` admission, projectile travel, player collision, and impact phase
through shared native and managed checkpoints.
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
When a deferred attack command arrives at the native ready boundary, both
runners treat it as a chained action and apply the native first-frame skip for
`QuickAttack`, `FastAttack`, `FasterAttack`, or `FastestAttack`; base-game
ranged skips remain disabled for Hellfire.
Checkpoint hashes cover the complete participant projection. Protocol snapshots
put the receiving player first, but both runners sort every player by entity ID
before hashing, so receiver-relative wire order cannot alter a checkpoint. A
secondary player's resources, attributes, equipment, combat mode, and inventory
topology therefore expose multiplayer divergence directly.
The optional `secondary_player.equipment` array uses the same validated
equipment schema as the primary player; when present it replaces the synthetic
block-capable shield used by older fixtures.
The optional `secondary_player.inventory_item` fields exercise an explicit
stable-reference `DropItem` from a secondary participant. The corresponding
fixture verifies participant routing, source removal, adjacent world-item
materialization, stable identity validation, and a shared checkpoint hash in
both runners.
The optional `secondary_player.held_item` fields seed the authoritative cursor
for pickup-replacement fixtures; both runners validate its identity and preserve
the dropped cursor item in the projected world state.
The companion `gameplay-multiplayer-secondary-item-pickup.json` fixture routes
`PickupWorldItem` from the secondary participant, proving authoritative world
item removal, inventory placement, purchased-tick propagation, and the shared
checkpoint hash in both runners.
The companion `gameplay-multiplayer-secondary-item-pickup-with-existing-item.json`
fixture repeats that route with secondary inventory cell 0 occupied. Both
runners must choose cell 1 through the same first-fit placement rule, preserving
the existing item and producing the same participant hash.
The `gameplay-multiplayer-secondary-item-pickup-overflow.json` fixture fills
all 40 secondary inventory cells and verifies that the accepted pickup moves to
the secondary authoritative cursor, then drops that held item back to an
adjacent tile while preserving the world-item identity.
The `gameplay-multiplayer-secondary-gold-pickup.json` fixture routes an adjacent
Gold pile through a secondary participant with an existing wallet balance. Both
runners add to that wallet directly, remove the world item, and produce the same
canonical participant hash without inventing an inventory Gold stack.
The companion `gameplay-multiplayer-secondary-gold-pickup-malformed.json`
fixture corrupts the Gold identity and requires both runners to reject the
secondary pickup as `Malformed` without changing the wallet, cursor, or world
item.
An initial state with one canonical Gold pile may add
`inventory_filler_count`. Both runners place that many deterministic one-cell
non-Gold items into the remaining native inventory cells. This forces the same
capacity boundary without encoding presentation-only filler details in each
fixture.

The optional `gold_stack_stable_item_id` gives that initial pile a stable
content identity. A `DropItem` command uses `location`, `inventory_index`, and
`stable_item_id` to submit the explicit `DropItemRequested` protocol intent;
the native and managed runners validate the identity, remove the inventory
source, and materialize the adjacent world item. `DropHeldItem` remains the
legacy cursor-only compatibility command. The
`gameplay-stable-item-drop.json` fixture covers this boundary without a
presentation-specific cursor state. A command may also declare
`expected_status` (`Accepted` or `Rejected`) and, for rejected commands,
`expected_reject_reason`; both runners validate the authoritative admission
outcome after receipt sequencing. The
`gameplay-stable-item-drop-retry.json` fixture uses this field to prove that a
second submission with the already-removed stable identity is rejected while
the first drop remains authoritative.

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
- Base-content fixtures must carry the exact SHA-256 produced by the checked-in
  content pack. Preserve existing checkpoint hashes when a change only updates
  manifest identity or adds a non-observable catalog entry; regenerate hashes
  only when canonical state actually changes, then verify both runners.
- A checkpoint label that is not a 64-character hexadecimal digest is a
  projection-only marker: runners still advance and execute that tick, but the
  marker deliberately does not compare canonical hashes.

## Parity status

The compact native replay projection now carries the same server-owned corpse
tile representation and per-tick monster regeneration as the C# checkpoints.
Ordinary-death and Firebolt, Fireball, Lightning, and Flash
 spell/projectile-timeline checkpoints pass in both runners. As of 2026-09-08,
the native replay gate contains 191 checked-in JSON fixtures and 210 registered
tests; the test count also includes parser and validation cases that do not
introduce another fixture file. Coverage includes seeded and autonomous MonsterArrow
source coverage, Hellfire HorkDemon Hork Spawn admission, player impact, and
miss-before-expiry, secondary-player Hork Spawn block and impact, current-tile
breakable-object removal, primary-player Hork Spawn block, multiplayer
HorkDemon target selection, SkeletonRanged MonsterArrow nearest-participant
selection, autonomous Succubus BloodStar admission/impact/block lanes, a
Succubus BloodStar multiplayer target-selection lane, ArchLich, Psychorb,
Necromorb, BoneDemon, and two-projectile multiplayer MonsterArrow participant
volley coverage, and Lich-family
MonsterMagic admission/hit/miss/block/nearest-participant/secondary-impact
lanes, plus the wounded Gargoyle self-heal/special-lock boundary and adjacent
Gargoyle attack admission against a secondary blocker, and healthy Gargoyle
avoidance-idle admission, and admitted avoidance RandomWalk destination and
occupancy, including an active object blocking the straight candidate, and the
far-distance Move-goal/RoundWalk state boundary across its first continuation
tick, GoatMelee, Gharbad, and Gargoyle nearest-participant autonomous movement, Fallen
rally nearest-participant pursuit, Butcher RandomWalk nearest-participant
pursuit, and Skeleton King RoundWalk nearest-participant pursuit with
decision-delay countdown, Gharbad GoatMelee/AiAvoidance alias movement, Warlord
and Snotspill SkeletonAi alias
admission/countdown,
SkeletonMelee movement-delay admission/countdown, and failed adjacent-
attack admission/countdown. Counselor, Zhar, and Lazarus nearest-participant
delay selection,
SkeletonMelee nearest-participant adjacent attack, Snake nearest-participant
charge routing, Rhino charge admission and nearest-participant selection,
special damage, and knockback, and Mega InfernoController nearest-participant
selection are also covered in both runners. Snake distant-delay and failed
adjacent-attack admission/countdown are also covered in both runners, as is
the mapped SnakeCharge travel, player collision, and impact phase. Sneak
conceal/reveal transitions and the Bat retreat movement/countdown lifecycle
are also covered in both runners. Scavenger corpse selection, movement, Q6
healing, countdown, and RNG consumption are also covered in both runners. The
Hellfire Scavenger max-health healing and corpse-removal lifecycle is also
covered in both runners. The dated full-suite baseline below records the last
complete run; on 2026-08-26 the native
`replay_test` executable was rebuilt and the new multiplayer HorkDemon,
SkeletonRanged MonsterArrow, Succubus BloodStar, ArchLich, Psychorb, Necromorb,
and BoneDemon MonsterMagic tests passed, including the new Lich block lane and
the two-projectile MonsterArrow volley, as did
the focused managed counterparts. The full suites remain
scheduled for the next periodic verification window. The secondary-portal,
secondary cursor-pickup, and secondary monster-death-drop fixtures are verified
in both runners.

## Current Coverage

Fixtures under `test/fixtures/replay` cover store transactions against synthetic
and checked-in content, movement and combat, healing, portals, world-item
pickup, status expiry, object-linked quest advancement, deterministic quest-pool
selection, a 1,000-tick autonomous regeneration soak, a multiplayer
autonomous-tick participant timeline with explicit secondary-session movement
commands, a secondary-session melee attack timeline, multi-level world
occupancy, a secondary-session portal transition, seeded MonsterArrow
travel/impact/removal, autonomous GoatRanged and
SkeletonRanged MonsterArrow admission, autonomous Succubus BloodStar admission,
travel, player impact, and secondary-player block, Hellfire HorkDemon Hork Spawn
admission, multiplayer target selection, decision-delay state,
miss-before-expiry lifecycle, secondary-player
block and impact, current-tile breakable-object removal, primary-player block,
a BoneDemon alias and Lich-family
MonsterMagic impact, miss, and secondary-participant impact, plus Mega
InfernoController lifecycle and nearest-participant admission, and a
multiplayer MonsterArrow
impact against the secondary participant, plus a two-projectile participant
volley whose managed counterpart also exercises both-participant save/restore
continuation, and a lethal secondary MonsterArrow lane that verifies
authoritative death, Gold, and equipment drops, plus generic monster melee
target selection against a closer secondary participant, plus bounded fallback
pathing around an occupied participant or object tile. The native and C# runners
also cover the wounded Gargoyle self-heal and retained special-lock boundary,
plus its adjacent attack-band admission, healthy non-adjacent avoidance
idle-band lifecycle, an admitted RandomWalk destination with participant and
active-object occupancy checks, and far-distance Move-goal admission with
persisted goal ticks, turn state, RoundWalk destination, and next-tick
continuation parity, plus SkeletonMelee movement-delay and failed adjacent-
attack admission/countdown lifecycles, Counselor, Zhar, and Lazarus
nearest-participant delay selection, Fallen rally nearest-participant pursuit,
Butcher RandomWalk
nearest-participant pursuit, Skeleton King RoundWalk nearest-participant
pursuit and decision-delay countdown, Gharbad GoatMelee/AiAvoidance alias
movement, Warlord and Snotspill SkeletonAi alias
admission/countdown, SkeletonMelee
nearest-participant adjacent attack, Snake
nearest-participant charge routing, Rhino charge admission,
nearest-
participant selection, special damage, relocation, and player knockback, and
Snake distant-delay and failed
adjacent-attack admission/countdown lifecycles, plus mapped SnakeCharge
travel, player collision, and impact-phase timing, plus Sneak conceal/reveal
transitions, Bat retreat movement/countdown timing, and Scavenger corpse
selection, movement, healing, countdown, and RNG timing.
The Hellfire Scavenger lane additionally verifies max-health-fraction healing
and removal of the consumed corpse.
Zombie action admission and long-range movement are also covered in both
runners. Fat/Overlord fresh movement admission, mode, idle counter, and
occupancy are also covered in both runners. GoatMelee AiAvoidance movement
admission, deterministic turn ordering, and occupancy are also covered in both
runners. GoatMelee adjacent attack admission, block timing, and seeded
AI-stream advancement are also covered in both runners. They are wired to
preserve the same
baseline resources,
attributes, equipment, belt, inventory footprints, stock, item state, level
state, class and spell knowledge, monsters, objects, quests, projectiles, and
world items in their canonical projections. Spell fixtures now reject unowned
casts and distinguish free class abilities from mana-consuming memorized spells;
protocol-level managed and native-client tests separately cover exact charged
and consumable item references.

The managed persistence gate also restores an active Lich MonsterMagic
projectile from a versioned save, checks the canonical snapshot hash and
trajectory state, and advances both source and replacement simulations through
the next tick for continuation parity.

`gameplay-player-action-lifecycle.json` follows a Warrior through an eight-frame
reserved walk, a deferred unarmed strike, chaining at the native ready boundary,
and a deferred memorized healing cast. Seven shared checkpoints prove action
payloads and modes before effects, committed position and melee damage at their
effect ticks, delayed mana/healing, and final cleanup.

`gameplay-player-action-ranged-diablo.json` and
`gameplay-player-action-ranged-hellfire.json` equip the same Rogue fast Bow and
attack a nonadjacent target. The Diablo timeline skips one native logic frame,
emits the arrow at tick 15, admits its chained move at tick 16, and resolves
impact at tick 17; Hellfire emits at tick 16, admits the move at tick 17, and
resolves impact at tick 18. Five checkpoints per fixture prove the variant
divergence and common final state. Their initialized Rogue
attributes use the native equipment-damage profile; because the compact replay
equipment shape omits base damage, the native unarmed one-damage fallback is
used against a one-hit-point target. Focused module tests retain additional
faster/fastest melee-speed coverage. The Hellfire fixture also carries the
Rogue class and Fast attack velocity modifier through the shared `Speed` and
`PathTicks` projectile state, so its C++ and C# checkpoint hashes cover the
fractional arrow cadence.

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

`gameplay-stable-item-drop.json` starts with a stable-identified Gold pile in
inventory cell 0. Both runners submit `DropItemRequested` with the stable
identity, remove the source pile, and materialize it at `(1,0)` as world entity
`0x40000000`. This keeps explicit stable-reference drops in the shared replay
baseline while preserving the separate legacy held-item fixture.

`gameplay-stable-belt-drop.json` and `gameplay-stable-equipment-drop.json`
exercise the same intent from belt slot 0 and equipment slot 0. Their stable
identities are validated before the slotted source is removed, keeping replay
coverage aligned with the protocol's three carried-item source locations.

`gameplay-multiplayer-secondary-item-pickup.json` starts an adjacent world item
and routes `PickupWorldItem` through the secondary participant. Both runners
remove the world entity, place the item in the secondary inventory at the
native first-fit cell, preserve its purchased tick, and validate the shared
checkpoint hash. Its second command retries the consumed entity and must be
rejected as `InvalidTarget` by both runners.

`gameplay-multiplayer-secondary-item-pickup-with-existing-item.json` repeats
that pickup with a stable item already occupying secondary cell 0. The shared
replay contract therefore checks first-fit placement at cell 1 instead of the
legacy native placement preference, while preserving both item identities.

`gameplay-multiplayer-secondary-item-pickup-overflow.json` fills the secondary
inventory, verifies that pickup remains accepted and projects the item as a
secondary held item, then submits `DropHeldItem` and verifies adjacent world
placement and the resulting checkpoint hash.

`gameplay-multiplayer-secondary-held-item-pickup-replacement.json` starts with
an occupied secondary cursor and an adjacent world item. Both runners drop the
cursor item at the secondary pickup origin, place the picked item in inventory,
and preserve the participant checkpoint hash.

`gameplay-multiplayer-secondary-held-item-pickup-rejection.json` moves an
occupied secondary cursor onto the world item tile before pickup. Both runners
record `NotAllowed` and preserve the cursor and world item without mutation.

`gameplay-multiplayer-secondary-gold-pickup.json` routes an adjacent Gold pile
through the secondary participant. The authoritative wallet is credited and
the world item is removed without creating an inventory stack; the native and
managed participant projections must match at the checkpoint.

`gameplay-multiplayer-secondary-gold-pickup-malformed.json` repeats the route
with an invalid identified value. Both runners require `Malformed` rejection
and preserve the secondary wallet, cursor, and world item.

`gameplay-multiplayer-secondary-gold-pickup-overflow.json` starts the secondary
wallet near the signed protocol limit and verifies that adding a valid pile is
rejected as `Malformed` without mutating participant or world state.

`gameplay-primary-held-item-pickup-rejection.json` starts with a cursor item and
a world item on the same tile. Both runners reject the pickup as `NotAllowed`,
record the rejected transition, and preserve both item states.

`gameplay-primary-held-item-pickup-replacement.json` starts with a cursor item
and an adjacent world item. Both runners drop the cursor item at the pickup
origin, place the world item in inventory, and preserve the same checkpoint.

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

The five standard Charged Bolt fixtures share the validated `test/content/replay/charged-bolt`
pack and canonical manifest identity; Hellfire rules remain a separate variant
overlay.
Hellfire Charged Bolt now uses the same validated `test/content/replay/charged-bolt`
pack. The status-expiry fixture uses the validated `test/content/replay/status-expiry`
pack for its spell row and canonical manifest identity.

Bone Spirit homing and the Hellfire Firebolt/Fireball block fixtures share the validated
`test/content/replay/projectile-variants` pack and canonical manifest identity.

`gameplay-firebolt-resolution.json` uses the activated base Firebolt row, a
level-3 Sorcerer with Magic 35, and seed `0x12345678`. Tick 1 proves native
class-adjusted Q6 mana, randomized damage, speed-26 16.16 velocity, and the
post-cast RNG state. Tick 2 proves swept collision, magic hit RNG, one-quarter
fire-resistance damage, exact `400/64` monster health, and impact rollback.
Tick 3 proves deterministic impact expiry. The native parser and managed loader
also reject fire resistance outside the authoritative `-100..100` range.

`gameplay-holy-bolt-resolution.json` uses the base Holy Bolt row against an authored undead target. The fixture proves the level-scaled projectile speed, physical damage roll, undead-only collision, class-adjusted Q6 mana, and impact expiry; the native and managed runners share the undead capability in their compact target state and canonical hash.

`gameplay-holy-bolt-living-target.json` places a living monster on the projectile path. Both runners verify Holy Bolt passes through without damage or impact termination while still consuming the cast mana and preserving the in-flight projectile state.

`gameplay-firebolt-pvp.json` uses the same level-3 Firebolt against a second
participant. The fixture proves class-adjusted Q6 mana, native player magic-hit
RNG, half-Q6 player damage, hit animation timing, projectile impact, and
removal parity. `gameplay-firebolt-hellfire-block.json` adds a shield-capable
participant and proves Hellfire block admission, block animation timing,
variant RNG, and retained impact parity.

`gameplay-fireball-resolution.json` uses activated Fireball, a level-12
Sorcerer with Magic 35 and effective spell level 3, a petrified adjacent target,
and seed `0x12345678`. Tick 1 proves class-adjusted Q6 mana, two-draw native
damage and RNG order, level growth, and speed-22 16.16 velocity. Tick 3 proves
the direct hit, exact shifted center splash, and impact entry; ticks 16 and 17
prove the final active explosion tick, expiry, exact Q6 health, and final RNG
state. Both runners validate all four checkpoints.

`gameplay-fireball-pvp.json` places a second participant on the first travel
tile of a level-3 Fireball cast in multiplayer mode. Tick 1 proves the native
player hit and Q6 damage boundary, direct-to-impact transition, and complete
participant projection; tick 17 proves the 14-tick impact lifetime and
projectile removal. Both runners validate the same two checkpoints.

`gameplay-fireball-hellfire-block.json` gives that participant a guaranteed
shield block against a level-3 Fireball. Tick 1 proves the Hellfire rotation
draw and retained impact phase; the same impact applies the non-blocked splash
to leave Q6 life at `1987`, and tick 17 proves removal. Both runners validate
the same two checkpoints.

`gameplay-bone-spirit-pvp.json` places a second participant on the swept path
of a level-2 Bone Spirit cast. The fixture proves native current-life damage,
including the non-arrow half-damage step, immediate self-damage, the single
player-hit RNG draw, impact entry, and seven-tick projectile removal. Both
runners validate the same two checkpoints and final participant projection.

`gameplay-bone-spirit-homing.json` places a petrified, magic-resistant monster
near a level-1 Bone Spirit path. The fixture proves native nearest-monster
retargeting, current-life-third damage with monster resistance, monster
regeneration, and the retained seven-tick impact phase. Both runners validate
the same spawn and post-hit checkpoints.
The Firebolt, Fireball, and Bone Spirit PvP runners load the validated base `spells.tsv` rows and check the active `devilution.base` content-manifest
identity.

`gameplay-multiplayer-secondary-attack.json` routes a melee attack from the
secondary participant to an adjacent monster. The fixture proves participant
identity routing, shared combat-target admission, and the native unarmed damage
boundary in both runners.

`gameplay-multiplayer-secondary-portal.json` routes a portal transition from
the secondary participant while the primary remains on its original level. The
fixture proves session-specific world-command routing, level/position
projection, and per-participant visited-level history in both runners. The
managed replay harness also captures both participants in its initial snapshot
and round-trips the resulting two-session state through persistence in reverse
restore order, covering reconnect reconstruction of the secondary history.

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

`gameplay-reflect-resolution.json` uses the external Reflect ability at
character level 3. Tick 1 proves the native default spell level, level-scaled
charge creation, and canonical Reflect-state hashing in both runners.

`gameplay-reflect-retaliation.json` starts with one Reflect charge and an
adjacent monster. Tick 1 proves charge consumption, the native 20-30 percent
retaliation roll, reflected monster damage, reduced incoming damage, and shared
gameplay RNG state.

`gameplay-etherealize-resolution.json` uses the Etherealize ability against an
adjacent monster. Ticks 0 and 1 prove immediate permanent status admission,
typed status hashing, and blocked autonomous monster damage while the status
remains active. Both runners validate both checkpoints.

`gameplay-berserk-resolution.json` uses a learned Berserk spell against an
eligible monster with four independent damage channels. Ticks 1 and 2 prove
native damage scaling, attack-damage replacement, canonical berserked-monster
hashing, and rejection of a duplicate cast. Both runners validate both
checkpoints.

`gameplay-warp-resolution.json` uses a learned Warp spell against a normalized
level trigger. Tick 1 proves deterministic destination selection, level
transition, visited-level history, exact mana consumption, and canonical state
parity between the native and managed runners.

`gameplay-golem-resolution.json` uses a learned Golem spell at character level
4. Tick 0 proves server-owned minion identity and ownership, native summon
stats and spawn-seed progression, death-animation timing, exact mana
consumption, and canonical monster-state parity between the native and managed
runners.

`gameplay-jester-firebolt-resolution.json` uses a learned Jester spell in the
bounded Firebolt dispatch lane. Tick 0 proves deterministic dispatch RNG,
nested Firebolt projectile creation, one-time mana consumption, and canonical
projectile-state parity between the native and managed runners.

`gameplay-town-portal-resolution.json` uses a learned Town Portal spell from a
dungeon level. Tick 0 proves dynamic portal endpoints, ownership, bidirectional
lifetime state, exact mana consumption, and canonical portal-state parity
between the native and managed runners.

`gameplay-counselor-delay.json` uses a healthy adjacent Counselor whose native
adjacent attack roll falls through to the server-owned decision delay. Ticks 1,
2, and 3 prove the initial RNG draw, delay creation, countdown, and canonical
monster-state parity between the native and managed runners.

`gameplay-counselor-retreat.json` starts a wounded adjacent Counselor with four
native retreat attempts. Ticks 1, 2, and 3 prove opposite-direction
RandomWalk movement, occupancy state, retreat countdown, and canonical
monster-state parity between the native and managed runners.

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
- `gameplay-stable-item-drop.json` for explicit stable-reference inventory
  drops through `DropItemRequested` and authoritative world-item creation;
- `gameplay-multiplayer-secondary-item-pickup.json` for secondary-participant
  world-item pickup, inventory placement, purchase-tick propagation, and
  checkpoint parity;
- `gameplay-multiplayer-secondary-item-pickup-with-existing-item.json` for
  occupied-inventory first-fit placement and participant checkpoint parity;
- `gameplay-multiplayer-secondary-item-pickup-overflow.json` for full-secondary
  inventory cursor overflow and participant checkpoint parity;
- `gameplay-multiplayer-secondary-held-item-pickup-replacement.json` for
  secondary cursor replacement and participant checkpoint parity;
- `gameplay-multiplayer-secondary-held-item-pickup-rejection.json` for
  occupied-tile secondary cursor rejection and state preservation;
- `gameplay-multiplayer-secondary-gold-pickup.json` for direct secondary wallet
  credit, world-pile removal, and participant checkpoint parity;
- `gameplay-multiplayer-secondary-gold-pickup-malformed.json` for malformed
  Gold rejection and secondary state preservation;
- `gameplay-multiplayer-secondary-gold-pickup-overflow.json` for wallet-overflow
  rejection and secondary state preservation;
- `gameplay-primary-held-item-pickup-rejection.json` for occupied-tile cursor
  rejection and transition bookkeeping;
- `gameplay-primary-held-item-pickup-replacement.json` for atomic cursor
  replacement, origin drop, and inventory placement;
- `gameplay-stable-item-drop-retry.json` for accepted-then-rejected stale
  stable-reference retry ordering and `InvalidTarget` rejection reason;
- `gameplay-stable-belt-drop-invalid-target.json` for `NotAllowed` rejection
  when a valid stable source targets a non-adjacent destination;
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
- `gameplay-multiplayer-secondary-monster-death-drops.json` for lethal seeded
  MonsterArrow impact against a secondary participant, death-mode transition,
  Gold halving and world-drop RNG, facing-ordered equipment drops, and shared
  C++/C# participant hashing;
- `gameplay-multiplayer-secondary-monster-melee.json` for nearest-participant
  generic monster melee selection, secondary damage, and primary-state
  preservation across the native and managed runners;
- `gameplay-multiplayer-secondary-monster-pathing.json` for bounded fallback
  movement around a dead primary participant and alternate first-step parity;
- `gameplay-multiplayer-secondary-monster-object-pathing.json` for preserving
  active world-object occupancy while selecting the same fallback first step;
- `gameplay-skeleton-melee-delay.json` and
  `gameplay-skeleton-melee-attack-delay.json` for native SkeletonMelee
  movement-delay and failed adjacent-attack admission/countdown parity;
- `gameplay-rhino-charge.json` for native charge admission, nearest adjacent
  landing, special damage, monster relocation, player knockback, and RNG parity;
- `gameplay-rhino-multiplayer-target.json` for nearest-participant charge
  admission, secondary damage and knockback, monster relocation, and farther-
  primary preservation;
- `gameplay-skeleton-melee-multiplayer-target.json` for nearest-participant
  adjacent attack admission, secondary damage, and farther-primary preservation;
- `gameplay-snake-multiplayer-target.json` for nearest-participant charge
  routing through projectile travel/removal and farther-primary preservation;
- `gameplay-counselor-multiplayer-target.json` for nearest-participant selection,
  entity-id tie-breaking, adjacent delay admission, and farther-primary
  preservation;
- `gameplay-fallen-rally-multiplayer-target.json` for nearest-participant
  Fallen rally pursuit, diagonal movement, countdown, and farther-primary
  preservation;
- `gameplay-butcher-multiplayer-target.json` for nearest-participant Butcher
  pursuit, active-object blocking of the straight step, native five-direction
  RandomWalk turn ordering, and farther-primary preservation;
- `gameplay-skeleton-king-multiplayer-target.json` for nearest-participant
  Skeleton King pursuit, native RoundWalk 90/45/straight ordering, delay
  admission/countdown, and farther-primary preservation;
- `gameplay-snotspill-multiplayer-target.json` and
  `gameplay-warlord-multiplayer-target.json` for explicit native SkeletonAi
  alias admission, nearest-participant selection, and shared delay countdown;
- `gameplay-mega-inferno-multiplayer-target.json` for nearest-participant
  InfernoController admission, secondary-target routing, and farther-primary
  preservation;
- `gameplay-scavenger-multicandidate-selection.json` for native coin-directed
  corpse scan order, first-candidate selection, deterministic movement, and
  selected-corpse checkpoint parity;
- `gameplay-bat-multiplayer-target.json` for Bat retreat movement selecting the
  nearest live participant while preserving the farther primary participant;
- `gameplay-sneak-multiplayer-target.json` for Sneak concealment target
  selection, nearest-participant distance admission, and participant-state
  preservation;
- `gameplay-zombie-multiplayer-target.json` for Zombie long-range action
  selecting the nearer participant while preserving the farther primary;
- `gameplay-fat-multiplayer-target.json` for Fat movement selecting the nearer
  participant while preserving its idle/mode and movement checkpoint state;
- `gameplay-goat-melee-movement.json` for zero-seed AiAvoidance movement
  admission, deterministic RandomWalk ordering, occupancy, and RNG stability;
- `gameplay-goat-melee-attack.json` for seeded adjacent attack admission,
  secondary-player block timing, and native AI-stream advancement;
- `gameplay-goat-melee-multiplayer-target.json` for seeded AiAvoidance movement
  selecting the nearest live participant, preserving the farther primary while
  the secondary participant drives the autonomous path;
- `gameplay-gargoyle-multiplayer-target.json` for seeded Gargoyle AiAvoidance
  movement selecting the nearest live participant, preserving the farther
  primary, and advancing the native AI stream through the avoidance branch;
- `gameplay-multiplayer-secondary-monster-native-ai-seed.json` for paired
  native monster runtime-seed advancement, secondary hit/block stream
  consumption, block timing, and cross-language canonical hashing;
- `gameplay-multiplayer-secondary-monster-native-ai-seed-impact.json` for
  successful seeded secondary melee, min/max damage draw consumption, and
  canonical RNG-state parity;
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
- `gameplay-monster-arrow-resolution.json` for seeded MonsterArrow travel,
  player impact damage, impact-phase lifetime, and cross-runner removal parity;
- `gameplay-monster-arrow-autonomous.json` for validated GoatRanged admission,
  stable nearest-player target selection, travel, impact damage, and lifecycle
  parity;
- `gameplay-monster-arrow-goat-ranged-native-ai-seed.json` for restored
  GoatRanged AI-stream advancement, secondary-player arrow routing, and
  stream-derived post-shot delay;
- `gameplay-monster-arrow-goat-ranged-close-native-ai-seed.json` for restored
  close-range retreat admission, RandomWalk direction draws, and movement
  checkpoint parity;
- `gameplay-monster-arrow-skeleton-ranged.json` for validated SkeletonRanged
  clear-lane admission, long-range travel, impact damage, and lifecycle parity;
- `gameplay-blood-star-autonomous.json` for validated Succubus BloodStar
  admission, native velocity/runtime state, and pre-impact lifecycle parity;
- `gameplay-blood-star-lazarus-succubus.json` for the LazarusSuccubus
  BloodStar alias mapping and admission parity;
- `gameplay-blood-star-impact.json` for deterministic monster-magic hit RNG,
  Q6 player damage, impact-phase retention, and removal-boundary parity;
- `gameplay-blood-star-secondary-block.json` for secondary-player target routing,
  shield-capable block admission, block animation timing, and RNG parity;
- `gameplay-blood-star-native-ai-seed.json` for restored BloodStar AI-stream
  advancement, secondary-player routing, and stream-derived post-shot delay;
- `gameplay-blood-star-close-native-ai-seed.json` for restored close-range
  retreat admission, RandomWalk direction draws, and movement parity;
- `gameplay-monster-magic-autonomous.json` for validated Lich-family
  MonsterMagic admission, hit RNG, Q6 damage, and impact-phase parity;
- `gameplay-monster-magic-miss.json` for a seeded MonsterMagic miss that
  preserves player life, projectile travel, and failed-collision identity;
- `gameplay-monster-magic-secondary-impact.json` for secondary-participant
  target selection, Q6 impact damage, and complete participant hashing;
- `gameplay-multiplayer-monster-arrow.json` for secondary-participant target
  routing, complete participant hashing, and multiplayer impact/removal parity;
- `gameplay-firebolt-resolution.json` for native mana/damage scaling, swept
  speed-26 movement, fire resistance, fractional health, impact, and RNG parity;
- `gameplay-fireball-resolution.json` for two-roll damage, level-capped speed,
  swept direct collision, shifted splash, persistent impact, and RNG parity;
- `gameplay-fireball-pvp.json` for direct player collision, impact splash,
  complete participant state, Q6 damage, and projectile-lifetime parity;
- `gameplay-fireball-hellfire-block.json` for Hellfire shield-block retention,
  variant RNG, impact splash, and projectile-lifetime parity;
- `gameplay-fire-wall-resolution.json` for seven-tick controller expiry,
  deterministic two-branch growth, scaled persistent segment state, validated
  base-content spell loading, and canonical projectile parity;
- `gameplay-lightning-wall-resolution.json` for shared wall geometry with
  lightning-specific damage, duration, presentation, and canonical parity;
- `gameplay-ring-of-fire-resolution.json` for native radius-three crawl,
  deterministic Fire Wall segment creation, persistent lifetime, and
  canonical projectile parity;
- `gameplay-nova-radial-fan-resolution.json` for the one-tick controller,
  native 36-ball radial ordering, first-tick movement, and canonical parity;
- `gameplay-elemental-resolution.json` for the initial fire projectile,
  native retargeting lifecycle, and canonical parity;
- `gameplay-guardian-resolution.json` for turret admission, deterministic
  native firing-arc selection, Firebolt creation, controller state, and
  projectile lifetime;
- `gameplay-rage-resolution.json` for active/cooldown timing, duplicate-cast
  suppression, bounded Q6 expiry damage, and controller removal;
- `gameplay-inferno-resolution.json` for moving-controller admission,
  deterministic three-segment emission, and persistent segment lifetimes;
- `gameplay-apocalypse-resolution.json` for row-major target scanning,
  physical boom emission, target impact, and boom lifetime;
- `gameplay-flame-wave-resolution.json` for the one-tick controller,
  deterministic center/branch geometry, per-segment damage, same-tick movement,
  and canonical parity;
- `gameplay-chain-lightning-resolution.json` for the one-tick controller,
  deterministic native crawl fan-out, Lightning bolt movement, impact segments,
  and canonical parity;
- `gameplay-immolation-radial-fan-resolution.json` for the one-tick controller,
  deterministic radial fan ordering, Fireball movement, and canonical parity;
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
- `gameplay-reflect-resolution.json` for default-level, character-level-scaled
  Reflect charge creation and canonical state hashing;
- `gameplay-reflect-retaliation.json` for charge consumption, native reflected
  monster damage, reduced incoming damage, and gameplay RNG parity;
- `gameplay-etherealize-resolution.json` for permanent status admission,
  typed status hashing, and autonomous monster-damage immunity;
- `gameplay-berserk-resolution.json` for eligible-target admission, four-channel
  damage scaling, recast rejection, and canonical monster state hashing;
- `gameplay-golem-resolution.json` for server-owned minion identity and
  ownership, native summon stats and spawn-seed progression, death-animation
  timing, mana consumption, and canonical monster state hashing;
- `gameplay-jester-firebolt-resolution.json` for deterministic dispatch RNG,
  nested Firebolt projectile creation, one-time mana consumption, and canonical
  projectile state hashing;
- `gameplay-town-portal-resolution.json` for dynamic portal endpoints,
  ownership, bidirectional lifetime state, mana consumption, and canonical
  portal state hashing;
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
already-characterized transitions: broader autonomous MonsterArrow and
MonsterMagic AI branches, additional melee/avoidance AI branches beyond the
current Counselor, SkeletonMelee, Snake, Sneak, Bat, Fat, Zombie, Rhino, Fallen,
Butcher, Skeleton King, Gharbad, Warlord, and Snotspill,
Scavenger, and Gargoyle admission lanes,
multi-actor target selection beyond the current Bat, Fat, GoatMelee, Gargoyle,
Counselor, Rhino, SkeletonMelee, Snake, Sneak, Zombie, Fallen, Butcher,
Skeleton King, Gharbad, Warlord, Snotspill, and projectile lanes,
mod/content reload
ordering, an end-to-end Hellfire replay beyond the live overlay and loadout
activation smoke tests, and broader combat/event ordering. Cursor-held pickup
overflow is already covered by `gameplay-gold-pickup-overflow.json`.

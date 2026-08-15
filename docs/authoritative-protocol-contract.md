# Authoritative Protocol Contract

This document defines the server/client boundary shared by the native adapter
and Godot client. `protocol/devilution.proto` is the canonical wire schema;
generated bindings and engine-specific models must not redefine it.

## Handshake and Identity

Every connection begins with `ClientHello`. The server accepts commands only
after returning `ServerHello`. Production clients use schema version `0.2.0`
and must match both required identity hashes; the v0.1.0 compatibility alias
remains available when explicitly selected. Otherwise the server sends
`ProtocolError` and closes the connection.

Protocol v1 uses a compatibility alias: the server's combined content-and-module
ruleset SHA-256 is carried in both `content_manifest_hash` and
`ruleset_identity_hash`. A client must send that combined value in
`content_manifest_hash` and should repeat it in `ruleset_identity_hash`. Schema
version `0.2.0` gives the fields independent meanings: `content_manifest_hash`
is the validated ordered content-manifest SHA-256, while
`ruleset_identity_hash` is the combined content-and-gameplay-module identity;
both are required for a v2 handshake. The v1 alias remains available for
native compatibility clients.

A successful handshake returns a server-issued `session_token`. Presenting a
known token on reconnect restores the authoritative entity identity and command
ledger. An unknown or omitted token starts a new session.

`ServerHello.game_mode` additively declares whether the shared authoritative
simulation uses single-player or multiplayer rules. It is server-owned process
configuration, not inferred from connected participant count. Both native and
Godot clients retain it; the native compatibility runtime synchronizes its
legacy multiplayer presentation flag to the declared value. For additive
compatibility with older servers, an unspecified value preserves the native
launch-time mode; current servers always declare a concrete value.

For a new session, `ClientHello.requested_character_class_id` may select a
stable class ID from the active module's validated catalog. If omitted, the
module uses its content-selected default. The field is only a creation hint:
in-memory state or a server-owned save restored by the resume token takes
precedence, and an ID absent from active content is rejected before gameplay.

`ClientHello.requested_character_name` is the corresponding optional
new-session name hint. The Diablo module validates it against native multiplayer
name rules before participant creation. It is not a rename command: an
in-memory participant or server-owned save restored by the resume token keeps
its existing character name.

Schema evolution is additive. Existing field numbers and meanings are never
reused, removed fields are reserved, and peers ignore unknown fields. A breaking
change requires a new package and protocol schema version.

## Transport

TCP envelopes consist of a four-byte unsigned little-endian payload length and
one serialized Protobuf `Envelope`. Payloads must be between one byte and 1 MiB.
Framing is transport infrastructure and may be replaced without changing the
gameplay contract.

## Command Delivery

Each command carries a session-scoped client sequence and requested simulation
tick. The server assigns receipt order, applies its late-command policy, and
deduplicates by session and client sequence. Clients retain unresolved commands
and retry after an adaptive acknowledgement deadline. A duplicate returns the
original receipt and applied tick without mutating state twice.

Acknowledgements are authoritative for accepted, rescheduled, rejected, and
duplicate outcomes. Commands express intent only: clients never supply the
resulting gold, health, damage, inventory placement, quest state, or world
mutation.

`CastRequested.source` identifies an ability, memorized spell, charged item, or
consumable item. Item-backed casts must also carry the exact authoritative
`PlayerItemReference`; non-item sources must not. The server verifies that the
spell belongs to the declared class ability or learned-spell state, or that the
referenced item is an eligible matching staff, scroll, or rune in its required
location. A valid ability costs no mana, a memorized spell consumes catalog
mana, a charged item decrements one charge, and a consumable is removed. Source
resources change only after the spell, target, range, line-of-sight, and
projectile checks have all succeeded.

For native-scaled definitions such as Firebolt, catalog mana is resolved from
effective spell level, character level where applicable, class/game-variant
reduction, minimum cost, and the Q6 unit scale before admission. The client
still sends only cast intent; it never computes or supplies the cost or damage.

Source zero is retained for older additive peers, but it is not an ownership
bypass. The server infers only from authoritative state in the deterministic
order ability, memorized spell, charged left-hand staff, inventory consumable,
then belt consumable. If no owned source matches, the cast is rejected.

`IdentifyItemRequested.source` field 3 is a separate item-service source, not a
spell-cast source. Zero preserves the paid vendor service and active-store
requirement. `CLASS_ABILITY` names the Bard's content-selected Identify skill;
the server verifies the player's authoritative `ability_spell_id`, resolves the
exact owned equipment or inventory item, and identifies it without spending
Gold. Unknown sources are malformed, and clients never set identified state
locally.

`RepairItemRequested.source` field 3 uses the same enum. Zero preserves paid
vendor repair, including its store, eligibility, full-restoration, and Gold
rules. `CLASS_ABILITY` names the Warrior's content-selected Repair skill. The
server validates that ability and exact owned item, advances the authoritative
native RNG for every repair iteration, and applies level-scaled repair strength
plus permanent maximum-durability loss. A fully repaired item is an accepted
no-op with no RNG draw, and a repair that exhausts maximum durability removes
the item. Clients never calculate or apply these changes.

`RechargeItemRequested.source` field 3 also uses the item-service enum. Zero
preserves paid vendor recharge, including store, eligibility, full restoration,
and Gold rules. `CLASS_ABILITY` names the Sorcerer's content-selected Staff
Recharge skill. The server validates the ability and exact owned staff, uses
the content-owned native staff level to make one authoritative recharge-strength
roll, then reduces maximum charges before each restoration step. A full staff
is an accepted no-op with no RNG draw; reaching zero maximum charges leaves the
staff exhausted. Clients never calculate or apply charge changes.

`SetFriendlyModeRequested.friendly_mode` is an explicit, retry-safe state
assignment. The server does not interpret repeated delivery as another toggle.
The following snapshot is the authority for the resulting mode.

`DropHeldItemRequested` is the additive command alternative at field 28. It
contains only a target tile. The Diablo module requires a server-owned held item
and an unoccupied, walkable tile no farther than one cell from the player, then
creates the world item with the same item seed, price, and complete state. A
retry cannot duplicate the item because normal command deduplication and the
following snapshot remain authoritative.

## State, Events, and Persistence

`Snapshot` is a complete projection for the receiving session. The server owns
simulation ticks, movement and occupancy, combat, spells and projectiles,
monsters, items, objects, quests, portals, stores, inventory, progression,
content rules, and save data. A client may interpolate presentation but must
reconcile to every snapshot.

The repeated player projection contains every participant with an assigned
entity ID. Its wire order is receiver-relative: the receiving participant is
first, followed by all other participants in ascending entity-ID order. IDs are
nonzero and unique. Clients use the first entry as local presentation state but
must validate the whole set. The canonical state hasher sorts all players by
entity ID, so receiver-relative player ordering cannot change the hash. The
snapshot remains a per-session projection: receiver-scoped fields such as the
singular active-store view may legitimately produce a different overall hash.

`EventBatch` contains presentation-friendly deltas such as damage, healing, and
experience. Events are not a second source of truth. A reconnect or dropped
event cannot invalidate the next snapshot.

`Snapshot.state_sha256` hashes authoritative fields in an explicit canonical
order. Stable-identity lists, including the complete participant set, are
sorted before hashing. Presentation-only and localized values are excluded.
Every newly authoritative field requires native and C# parity coverage before
clients depend on it.

`Snapshot.random_state` field 10 carries the module-owned Diablo gameplay RNG.
`Snapshot.portals` field 11 carries the complete authoritative portal route
projection. Each row includes both endpoints plus dynamic owner,
bidirectional-use, and remaining-tick metadata; Town Portal placement,
replacement, expiry, and travel remain server-owned. Older readers may ignore
the additive field, while current clients validate and render it without
simulating portal state.
`MonsterSnapshot.monster_level` field 15 supplies deterministic magic hit
resolution. `MonsterSnapshot.hit_points_q6` and `max_hit_points_q6` fields
16-17 carry exact native 26.6 monster health; fields 6-7 remain rounded-up
whole-point projections so a living fractional-health monster never appears
dead to an older reader. `DamageEvent.amount_q6` field 4 carries exact damage,
while field 3 remains its truncated whole-point presentation projection.
Legacy messages with no raw field are interpreted as whole points multiplied by
64. Fractional health adds the tagged
`godevilution-monster-hit-points-q6-v1` canonical-hash section; legacy and
whole-aligned snapshots retain their prior hashes.

`HealingEvent.amount_q6` field 3 carries the exact applied Q6 healing. The
pre-existing `amount` field 2 remains the same raw native-resource amount for
older readers and is populated alongside the new field. New native readers use
`amount_q6` when present and fall back to the legacy raw amount, preserving both
fractional healing and compatibility with earlier event batches.

`MonsterSnapshot.moving`, `future_position_x`, `future_position_y`, and
`petrified` fields 18 through 21 preserve the native two-cell walking model.
The current tile remains positive monster occupancy; the adjacent future tile
is projected as negative occupancy until movement completes. A normal walker
is collision-eligible only at its current tile. Petrification freezes movement,
makes both occupied tiles collision-eligible, and guarantees a missile hit
after consuming the native hit-roll RNG draw. Death cancels movement back to
the current tile. The server validates adjacency and duplicate occupancy,
persists this state in version-1 saves, and adds the tagged
`godevilution-monster-occupancy-v1` canonical-hash section whenever movement or
petrification is nondefault. Missing additive fields normalize to stationary
occupancy at the current tile.

`MonsterSnapshot.stone_curse_immune` field 22 carries an immutable external-
content capability needed by future authoritative casts and version-1 save
restore. True adds the tagged
`godevilution-monster-stone-curse-immunity-v1` hash section; false is the
additive legacy default. Clients may present the capability but never decide
whether a cast succeeds.

`MonsterSnapshot.is_unique`, `is_diablo`, and `berserk_immune` fields 25-27,
`min_damage`, `max_damage`, `min_damage_special`, and `max_damage_special`
fields 28-31, and `is_berserked` field 32 carry the server-owned Berserk
contract. The server alone selects an eligible target, performs the native
radius-five crawl and resistance coin flip, and scales each damage channel in
native RNG order. Nondefault values add the tagged
`godevilution-monster-berserk-v1` canonical-hash section; missing fields retain
legacy defaults.

`MonsterSnapshot.acid_resistance` field 36 carries the validated native acid
resistance percentage through snapshots, saves, and protocol projection.
Nonzero values add the tagged
`godevilution-monster-acid-resistance-v1` canonical-hash section; missing or
zero values retain the additive compatibility default.

`MonsterSnapshot.experience_reward` field 40 carries the validated native
experience reward used by the server-owned monster death boundary. Clients do
not calculate rewards from this field; they receive the resulting experience
event and authoritative player snapshot.

`MonsterSnapshot.to_hit` and `to_hit_special` fields 41-42 carry validated
native normal and special attack accuracy. Normal autonomous attacks apply the
native level-delta, armor, and minimum-hit formula on the server; clients never
resolve these rolls. Zero values retain the additive compatibility fallback.

`MonsterSnapshot.native_ai` field 43 carries the validated native `MonsterAIID`
dispatch identity. The server owns AI execution; clients do not dispatch
behavior from this metadata.

`MonsterSnapshot.intelligence` field 44 carries the validated native intelligence
value used by server-owned AI probability bands. Clients do not resolve AI rolls
from this metadata.

`MonsterSnapshot.gargoyle_special_active` field 45 carries the server-owned
Gargoyle special lock. When a wounded Gargoyle heals beyond its native
`intelligence + 2` safety boundary, the server applies the deterministic native
self-heal and holds this lock until the player closes that boundary; clients
only render the resulting Healing event and snapshot state.

`MonsterSnapshot` fields 46-47 carry server-owned Fallen fear countdown and
retreat direction. Fields 48-49 carry Sneak hidden state and retreat ticks;
hidden monsters are excluded from authoritative targeting. Fields 50-51 carry
Scavenger healing ticks and its selected corpse identity. Clients render these
values but never execute the corresponding AI.

`Snapshot.corpses` carries persisted defeated non-minion corpse tiles. Corpse
identity and position are server-owned and included in canonical hashing.

`MonsterSnapshot` fields 52-53 carry Bat retreat steps and the direction toward
the victim captured when the retreat began. The server applies the native
`4 * intelligence + 8` adjacent-attack admission and performs the two-step
retreat; clients render only the authoritative state.

`MonsterSnapshot.counselor_retreat_ticks` field 55 carries the remaining native
Counselor wounded-adjacent retreat attempts. The server applies the four
opposite-direction `RandomWalk` attempts before resuming mapped ranged
dispatch; clients render only the resulting movement and snapshot state.

For the `Mega` identity, Inferno admission and its intermediate-distance
`2 * (5 * intelligence + 25)` RandomWalk fallback are server-owned. A failed or
blocked mapped Inferno roll never delegates movement policy to the client.

Counselor mapped ranged rows use the native `5 * (intelligence + 10)` admission
and 30-percent `RoundWalk` fallback; clients receive only the resulting
projectile or authoritative movement snapshot.

The `SkeletonKing` identity drives a server-owned summon boundary. When its
validated single-player admission succeeds, the server selects a level-valid
`SkeletonMelee` definition, creates the summon on an available adjacent cell,
and projects it through the ordinary monster snapshot. Clients never create or
simulate the summon.

The server currently uses the `Fat` identity for the native Overlord normal
(`15 + 4 * intelligence`) and special (`20 + 4 * intelligence`) melee bands;
the resulting damage event and snapshot remain authoritative. Other AI-specific
special behavior is intentionally added incrementally.

The `Rhino` identity likewise drives a server-owned clear-lane charge using the
native `43 + 2 * intelligence` probability boundary. Its
resulting monster/player positions, damage event, and blocked-lane fallback are
ordinary authoritative snapshot state; clients do not simulate the charge.
Charge misses and blocked lanes retain the native far-distance `RoundWalk` or
ordinary `33 + 2 * intelligence` `RandomWalk` movement fallback on the server.

The validated Gloom `BatCharge` mapping likewise retains the native `BatAi`
movement branch after a charge miss or blocked lane. The server applies the
standing-tick compatibility `63 + intelligence` admission and deterministic
`RandomWalk` candidate order; clients receive only the resulting movement or
the unchanged authoritative snapshot.

The `SkeletonRanged` identity drives a server-owned clear-lane arrow boundary
using the native `3 + 2 * intelligence` attack band. Its resolved damage is an
ordinary directed gameplay event; clients do not perform the hit roll.
When the lane is blocked at long range, the server preserves the native
defer rather than delegating pursuit to the client or generic navigation.

The `GoatRanged` identity drives the shared native `AiRanged` boundary. On a
clear missile lane it emits the server-resolved arrow damage event; inside four
tiles it first applies the native `10 * (intelligence + 7)` avoidance band and
selects a deterministic retreat cell when one is available. At long range, a
blocked lane preserves the native defer rather than generic pursuit.

The `GoatMelee` identity drives the adjacent `AiAvoidance` admission boundary
of `23 + 2 * intelligence`. When its wounded special branch is selected, the
server uses the externalized special accuracy and damage range; clients receive
only the resulting authoritative damage event and snapshot.

The `SkeletonMelee` identity drives the native adjacent attack band of
`20 + 2 * intelligence`; an outside-band tick consumes no client-side action and
the next authoritative tick reevaluates the server-owned boundary.

The `Zombie` identity drives its native `10 + 2 * intelligence` action band. A
successful long-range action uses an authoritative movement step once the
distance reaches `2 * intelligence + 4`; outside-band ticks do not make the
client simulate a move or attack.

Every live monster also receives the native `ProcessMonsters` regeneration before
AI execution: level 1 monsters recover one life per tick, and higher levels
recover `level / 2`, capped at the externalized maximum. The server applies this
in exact Q6 state; clients render the resulting snapshot.

`ProjectileSnapshot` fields 14 through 27 carry behavior, phase, origin, 16.16
traveled distance and velocity, original direction, wandering path
position/cadence, speed, impact lifetime, and resistance tuning. Field 28 stores
the last stable occupant checked on a stationary tile, preventing a resumed slow
missile from rerolling collision against the same actor. Field 29 stores the
effective spell level captured at creation, so a resumed Lightning controller
or segment retains its content-scaled lifetime independently of later equipment
or learned-level changes. These fields are state,
not presentation hints: they are included in server saves and tagged
canonical-hash sections when nondefault. Older snapshots retain their existing
default behavior, while a resumed Charged Bolt continues with the same path,
collision suppression, exact target health, and next RNG draw. A resumed
Firebolt likewise retains its origin, 16.16 travel and velocity, native speed,
impact phase, resistance divisor, exact damage, and stationary-collision state.
A resumed Lightning cast retains its invisible controller, stationary damaging
segments, source spell level, remaining lifetimes, exact Q6 damage, and native
per-tick collision/RNG boundary.
A Teleport cast uses the existing projectile behavior value `9` as an invisible
two-tick controller. Its position and remaining lifetime are authoritative;
the resulting player position continues to use the existing player snapshot
coordinates. Version-1 peers therefore require no new wire field, and a
resumed save revalidates occupancy before the controller's first movement.

Native `Arrow` rows for GoatRanged and SkeletonRanged select behavior value
`59`, `MonsterArrow`; older `Targeted` rows are accepted as a compatibility
mapping. The server captures the resolved physical damage and target
coordinates, advances one authoritative tile per tick, removes blocked or
expired missiles, and applies player damage only on collision. A zero spell ID
is valid for this monster-owned behavior; snapshots, saves, hashes, and the
existing `ProjectileSnapshot` shape carry the complete resumable state.

A Phasing cast uses projectile behavior value `15` as the same shape of
invisible two-tick movement controller. The server selects its random
destination, consumes the cast source even when no candidate exists, and
revalidates occupancy before movement. Clients neither send a destination that
governs the selection nor consume RNG; they observe the controller through
existing projectile fields and the resulting authoritative player coordinates.
Version-1 protocol and save envelopes therefore need no additive field.

A Fireball cast uses projectile behavior value `16`. Phase `0` carries its
server-owned 16.16 travel state; phase `1` carries the stationary 14-tick
explosion after direct collision, blocking, or range expiry. Existing origin,
position, distance, velocity, speed, damage, resistance, impact-lifetime,
remaining-tick, and effective-spell-level fields are sufficient for exact
resume. The server alone applies the ordered 3x3 shifted splash and object
breakage. Godot and native clients render the orange travel/impact states but
never predict collision or damage, so protocol version 1 needs no new field.

A Flame Wave cast uses behavior value `24` for its one-tick controller and
behavior value `25` for each moving Fire segment. The controller stores the
authoritative target coordinates and caster-facing direction; on its update the
server places the center segment plus the native left/right branches. Segments
use the existing speed, direction, damage, resistance, remaining-lifetime,
position, and collision fields, so protocol version 1 and version-1 saves need
no additive field. Godot and native clients render the segments but never
choose branch layout, consume RNG, resolve collision, or apply damage.

An Inferno cast uses behavior value `26` for its speed-32 controller and
behavior value `27` for each stationary Fire segment. The controller's
position, traveled distance, remaining lifetime, and emission count are
authoritative; each segment retains its position, damage, lifetime, and
collision state in the existing projectile fields. Clients render the moving
stream and stationary segments but never emit segments, consume RNG, resolve
collision, or apply damage.

A Chain Lightning cast uses behavior value `28` for its one-tick fan-out and
value `29` for each moving Lightning-compatible bolt. The server enumerates the
native crawl radius, creates the target bolts, and retains the chain spell ID
and effective level through existing projectile fields.

A Guardian cast uses behavior value `36` for its stationary turret controller.
`ProjectileSnapshot.guardian_state` field 44 carries the native three-tick
post-shot animation countdown; all spawn, scan, line-of-sight, RNG, and
Firebolt emission decisions remain server-owned.

Lightning segments continue to use behavior `4`; clients never choose targets,
consume RNG, or resolve collision and damage.

A Nova cast uses behavior value `30` for its one-tick radial controller and
value `31` for each continuing Lightning ball. The server preserves the native
nine-point quarter-radius order and mirrored emissions, then owns ball movement,
target collision, resistance, lifetime, and RNG. Clients render the resulting
authoritative projectiles and never emit balls or apply damage.

An Apocalypse cast uses behavior value `32` for its row-major scan controller
and value `33` for each persistent Physical boom. The server owns cursor
progression, blocked-cell and player-minion filtering, Hellfire line-of-sight,
and first-hit persistence; clients only render the resulting state.

A Resurrection cast names another participant through the existing
`CastRequested.target_entity_id`. Its runtime beam uses projectile behavior
value `11` plus the existing target, position, and lifetime fields. Revival is
not a client event or prediction: updated player coordinates, life, mana,
combat mode, and action state arrive in the complete participant snapshot. A
living valid target still produces the authoritative beam without changing
that player's resources.

A Heal Other cast likewise names another participant through
`CastRequested.target_entity_id`; it creates no projectile. The server validates
that the target is living, distinct, same-level, and in range, then publishes
the exact applied amount through the existing Healing event and the target's
new life through the complete participant snapshot. A full-life valid target
still consumes mana and RNG but produces no zero-healing event. Protocol version
1 therefore needs no additive field.

Stone Curse uses projectile behavior value `13` as a stationary timed
controller. Existing target, position, remaining-tick, phase, impact-lifetime,
and effective-spell-level fields carry its complete runtime. The associated
monster `petrified` flag freezes presentation and movement; expiry or the
dead-target shatter transition is resolved only by the server.

`StatusEffectSnapshot.behavior` field 4 carries typed server-owned semantics for
player statuses. Value `1` is Infravision and value `2` is Search. Clients
use them only for presentation: native maps Search to the original automap item
highlight and Godot outlines world items. Duration, Infravision recast union,
Search additive recasts, expiry, and level-transition cleanup remain server
decisions. Nonzero behavior adds the tagged
`godevilution-status-effect-behavior-v1` canonical-hash section after the legacy
effect ID, duration, and magnitude, so an omitted value preserves prior hashes.
Unknown behavior values and nonzero Infravision or Search magnitude are invalid
complete snapshot state.

`ObjectSnapshot` fields 10 through 12 carry native missile pass-through,
breakability, and persistent destruction. Field 13 carries additive armed-trap
state for the Rogue Trap Disarm capability. The server validates impossible flag
combinations, excludes destroyed objects from occupancy and interaction, and
removes them from native presentation. Object and projectile collision
extensions use separate tagged canonical-hash sections so all-default legacy
snapshots preserve their prior hashes. Version-1 save envelopes remain
wire-compatible and preserve the additive raw fields in their Protobuf payload.

Player class identity is authoritative and persists through snapshots and
server-owned saves. `PlayerSnapshot.character_class_id` field 19 carries the
stable ID from the active module's validated class catalog. Shipped content
keeps ID `0` as Warrior, preserving the Protobuf default used by older peers and
saves. Native compatibility clients reject IDs outside their supported hero
class enum. Nonzero IDs are included in the canonical state hash and covered by
matching native/C# golden tests. For newly created Diablo players, the server
also derives maximum life and mana from the selected class's native-compatible
Q6 level and attribute formulas; command-line values remain explicit
development overrides rather than the normal creation path.

`PlayerSnapshot.character_name` field 28 carries server-owned character
identity. A nonempty value is validated before state creation, persisted in
version-1 saves, restored on reconnect, and projected by both clients. It adds
the tagged `godevilution-player-name-v1` canonical-hash section; empty remains
the additive default for older snapshots and fixtures.

`PlayerSnapshot.mana_shield_active` field 29 carries server-owned Mana Shield
state. True adds the tagged `godevilution-player-mana-shield-v1` canonical-hash
section and is preserved by version-1 saves and both clients; false remains the
additive default. Clients do not apply the effect. The Diablo module owns the
native learned-level damage reduction, Q6 mana absorption, reverse calculation
when mana is insufficient, exact-depletion cleanup, and transition cleanup.

Starting Gold is materialized as ordinary `PlayerSnapshot.inventory` and
`inventory_grid` state: the first pile uses native cell 30 and deterministic
session-owned nonzero seeds. The wallet remains the native cached total. Store
commands preserve that topology with nonmaximum-first spending, native purchase
placement, sale top-off/new-pile placement, and transactional capacity checks;
aggregate-only compatibility participants retain the older wallet path.

`PlayerSnapshot.ability_spell_id` field 20 carries the class skill and repeated
`memorized_spells` field 21 carries learned spell IDs and levels. The server
validates native bounds and uniqueness, saves this state, and sorts learned
spells for protocol and hash projection. Native compatibility clients rebuild
their ability and memorized-spell masks from the snapshot rather than retaining
local creation state. When either field contains state, canonical hashing adds
the tagged `godevilution-player-spells-v1` section; an entirely empty spell
projection adds nothing so older state hashes remain unchanged.

`PlayerSnapshot.hostile_mode` field 22 is the inverse of native friendly mode.
Its absent/false Protobuf default therefore preserves friendly behavior for
older peers and saves. A true value adds the tagged
`godevilution-player-hostile-mode-v1` canonical-hash section; friendly legacy
state retains its prior hash. Diablo content owns the global `friendly_fire`
rule. When it is disabled, a friendly caster does not collide with another
player, while hostile mode opts that caster into PvP; when it is enabled,
player collision is globally active. Charged Bolt then preserves native
hit-roll, eligible block-roll, and damage-roll order, emits exact Q6 damage to
the victim, derives lightning resistance and blocking from authoritative class
and equipped-item state, and treats positive resistance as overriding a block.

`PlayerSnapshot.facing_direction` field 27 mirrors native `Direction` values
South through SouthEast. South/zero is the additive legacy default. Accepted
move, attack, and cast intents update facing from their resolved direction;
rejected or same-position resolved intents do not invent a rotation. Facing
crosses module snapshots, version-1 saves, native and Godot projection, and the tagged
`godevilution-player-facing-v1` canonical-hash extension. The server also uses
it as the starting direction for native equipped-item death placement.

`PlayerSnapshot.combat_mode` field 23 mirrors the native `PLR_MODE` ordinal and
`combat_mode_end_tick` field 24 records the first inactive tick for a timed
mode. Stand with end tick zero is the additive legacy default. Only Stand and
melee Attack admit missile blocking; Block and GotHit durations use validated
per-class frame counts plus usable fast-block or hit-recovery item flags.
Nondefault mode state adds the tagged
`godevilution-player-combat-mode-v1` canonical-hash section and round-trips
through version-1 saves. Hellfire content also selects the native post-block
rotation RNG draw before Charged Bolt enters its common impact phase.

`PlayerSnapshot.current_action` field 25 is present while a native-timed move,
attack, ranged attack, cast, death, level transition, or restart-in-town remains
in progress. It carries the start, effect, ready, and end ticks; whether the
effect has already resolved; and the action's direction, target, or complete
cast request.
Movement reserves the adjacent destination until its end tick, and presentation
clients expose that reservation as the future or predicted position. Deferred
casts preserve their spell, source kind, source item, and target so save
restoration cannot silently substitute a different resource source.

Death action kind 4 is already applied at its start tick, requires zero life
and Death combat mode, records the killing source as its target, and uses two
server ticks per activated class death frame. On the current ordinary combat
death path, the server first cancels movement, applies the activated Gold
divisor, and creates deterministic entries in the existing repeated
`Snapshot.world_items` projection. Canonical Gold piles already carried by
`PlayerSnapshot.inventory` and `inventory_grid` follow native non-full-first
removal, whole-pile seed reuse, partial-pile RNG, and compaction. This can expose
a physical odd remainder that differs from the truncated `PlayerSnapshot.gold`
cache until authoritative pickup recalculates it. Aggregate-only compatibility
state continues to use the wallet field directly. World-item position follows
native crawl order and newly created seeds consume `Snapshot.random_state`.
These states use only the ordinary player/world-item protocol shapes and
version-1 save envelope; no client computes loss, placement, topology, seed, or
recovered balance. The action
clears at the first inactive tick while Death mode remains terminal.
Level-transition kind 5 is
already applied after a valid portal or Warp target changes level and position,
records that target, and holds NewLevel for the native transition lifetime
before a living player returns to Stand. Restart action kind 6 has no target and is admitted
only from terminal Death after the Death action clears. The server immediately
applies the content-selected town level and tile, restores the configured Q6
life capped by maximum life, zeroes mana, clears the active store, and holds
NewLevel for the configured barrier before Stand. `RestartTownRequested` is the
empty additive command alternative at field 27; neither client supplies the
resulting state. Players in Death, NewLevel, or Quit cannot begin a
native-timed world action, and NewLevel/Quit players are excluded from monster
and projectile targeting. Present action state adds the tagged
`godevilution-player-action-v1` canonical-hash section; an absent action adds
nothing and retains all earlier hashes.

`PlayerSnapshot.held_item` field 26 carries the item owned by the server but
presented on the local cursor. It is outside `inventory`, `inventory_grid`, and
the cached Gold total. A partial Gold pickup fills inventory in native order,
removes the original world entity, and preserves its seed on the held remainder
without consuming RNG. Death drops the held item before applying ordinary Gold
loss. Present held state adds the tagged `godevilution-player-held-item-v1`
canonical-hash section; absence preserves every earlier hash and save shape.

`ItemStateSnapshot.base_name`, `identified_name`, and optional
`native_cursor_graphic` fields 59 through 61 carry runtime-authored dynamic-item
presentation. If any dynamic field is present, both names and a native
byte-representable cursor must be present. Both clients validate and project
that complete identity; they never derive it from localized catalog data.
Present dynamic state adds the tagged
`godevilution-dynamic-item-presentation-v1` canonical-hash section, while an
ordinary item with all three fields absent retains its previous hash.

`ItemStateSnapshot.plus_mana` and `plus_hit_points` fields 33 and 34 carry
native Q6 item resource bonuses. Fields 62 and 63 preserve native Hellfire's player-dependent
resource conversions. The authoritative server applies both bonus values and
each percentage against the character's base Q6 resource when resolving
equipment stats; clients never infer a fixed bonus from the item name. Nonzero
conversion state adds the tagged
`godevilution-item-resource-conversion-v1` canonical-hash section; absent
conversion fields preserve legacy snapshot and replay hashes.
The `plus_damage` (percent), `plus_damage_modifier` (flat), `plus_to_hit`, and
`plus_enemy_armor_class` (armor-piercing) item fields are also authoritative
when usable equipment is resolved for player attacks; unusable and
unidentified magical items do not contribute them.
The `plus_get_hit` item field is likewise resolved by the server for
autonomous monster physical hits after hit/armor resolution, with the configured
minimum damage preserved; clients never apply the incoming adjustment locally.
Equipped item `armor_class` and `plus_armor_class` are likewise folded into
the server-owned effective player armor state with native dexterity and
identification/requirement rules; clients do not derive defensive statistics.
Equipped `plus_strength`, `plus_magic`, `plus_dexterity`, and `plus_vitality`
are likewise applied by the server to current attributes under native
usability and identification rules; base attributes remain the recalculation
source.
The active class's Q6 item-vitality and item-magic factors, flat item resource
bonuses, and Hellfire conversions are likewise applied by the server to life
and mana maxima; clients consume those authoritative values.
Usable identified equipment resistance bonuses are also resolved by the server;
an active native `ZeroResistance` flag resets fire, lightning, and magic
resistance after natural class resistance and before the native 0..75 clamp.
Clients never derive elemental resistance from item presentation.
The native `NoMana` flag is likewise server-owned: it rejects memorized spell
admission before mana or source mutation, but does not reject free class
abilities or charged/consumable sources; it also disables Mana Shield
absorption through the shared damage resolver.

`PlayerSnapshot.base_mana_maximum` and `base_life_maximum` fields 31 and 32
carry the unmodified native maxima used by Hellfire equipment conversions. A
zero value falls back to the effective maximum for snapshots written before
these fields existed. Accepted equipment transfers recompute effective maxima
and current resources while preserving native resource debt. When base and
effective maxima differ, canonical hashing adds the tagged
`godevilution-player-resource-base-v1` section; equal values preserve earlier
hashes.

After the unconditional held-item drop, an ordinary monster-or-trap death
applies Gold loss and then removes equipped items in native slot order from
Head through Chest. Direction rotates left once for every slot, including an
empty slot; each present item first tries that adjacent tile and then the
shared radius-1-through-50 crawl. Drops retain the item seed and complete state,
and project the identified value as world price without consuming RNG. PvP
death retains equipment, and validated special-level multiplayer suppression
retains both equipment and Gold while still dropping the held item.
An unsuppressed lethal player hit instead creates one server-owned PvP ear.
The victim's validated name supplies native create-info, seed, and runtime
names; character level supplies value; the activated class supplies the native
cursor; and external combat/item content supplies the stable ear definition.
The existing world-item projection carries the complete result, including a
valid zero seed for short names. Duplicate ear identity is suppressed and the
death crawl consumes no gameplay RNG.

New Diablo participants also receive the activated class loadout's wallet,
equipment, belt, inventory, class skill, and learned spell. Item instances use
deterministic server-derived seeds. Save restoration validates against the
saved class template before registering a cold participant, so reconnect-time
class hints and host defaults cannot replace persisted class state.

Server-backed clients do not load or save legacy client state. The server is
the sole persistence owner for an authenticated session.

## Compatibility Gate

A protocol change is complete only when the schema, generated clients, server,
state hasher, shared replay fixtures, and relevant documentation agree. The
language-neutral delivery vector and replay fixtures under `test/fixtures`
provide the cross-language boundary tests.

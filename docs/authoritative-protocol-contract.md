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

If a server-owned save exists for a token but fails validation during restore,
the handshake is rejected and the stored state is not replaced. Reconnecting
with that token retries the same save until an operator repairs or removes it.
Concurrent first connections presenting one token are serialized and share the
same authoritative session; only the first connection's creation hints apply.

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

Before acknowledgement results mutate retry or presentation state, native and
Godot clients enforce the same admission rules: no more than 256 results;
every status and rejection reason must be a defined protocol value; an
accepted or rescheduled result must carry an unspecified rejection reason;
and rejected or duplicate results may carry the server's rejection reason.
Malformed acknowledgement batches fail closed, including when they arrive
through the direct command-submit path.

`OpenStoreRequested` selects a validated vendor or service store for the
session. `CloseStoreRequested` clears that active-store selection and is
idempotent, including when no store is open. The resulting snapshot carries no
`active_store` and a player `active_store_id` of zero; clients must use that
snapshot rather than clearing vendor state optimistically. Store open and close
commands are non-critical for late-command scheduling, but all purchases and
paid item services still require the matching authoritative active store.

`OpenStashRequested` and `CloseStashRequested` establish the equivalent
server-owned stash session. They are idempotent and mutually exclusive with an
active vendor store. `PlayerSnapshot.stash_open` is the authoritative session
state; stash item transfers, stash gold commands, and item-service requests
targeting stash items are rejected while it is false. `DepositStashGoldRequested`
and `WithdrawStashGoldRequested` move only
the requested amount after validating the open session and available balance.
The existing stash-item/gold canonical-hash section remains unchanged for a
closed session; an open session adds a separate tagged session extension.

`CastRequested.source` identifies an ability, memorized spell, charged item, or
consumable item. Item-backed casts must also carry the exact authoritative
`PlayerItemReference`; non-item sources must not. The server verifies that the
spell belongs to the declared class ability or learned-spell state, or that the
referenced item is an eligible matching staff, scroll, or rune in its required
location. A valid ability costs no mana, a memorized spell consumes catalog
mana, a charged item decrements one charge, and a consumable is removed. Source
resources change only after the spell, target, range, line-of-sight, and
projectile checks have all succeeded.

`PlayerSnapshot.ability_targeting_kind`,
`PlayerSpellLevelSnapshot.targeting_kind`, and
`ItemStateSnapshot.spell_targeting_kind` are additive presentation hints for
stable target selection. `COMBAT_ENTITY` permits a client to include a visible
monster or living remote-player entity ID, while `PLAYER` is reserved for
player-targeted spells such as Heal Other and Resurrection. `TILE` and the
zero-valued legacy default submit only coordinates. `WORLD_ENTITY` permits a
Telekinesis client to name a visible world item, authored object, or living
monster. The server remains the authority for target type, visibility, range,
line of sight, and spell-specific admission; these fields never grant targeting
permission.

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

`MoveInventoryItemRequested.item` is an additive exact inventory reference.
When present, the server validates its stable identity before changing the
inventory grid; an absent or zero-valued reference retains positional
compatibility with older clients.

`MoveItemRequested` also accepts `PLAYER_ITEM_LOCATION_STASH`. A stash
reference carries a page and 10x10 grid cell in addition to its stable item
identity. The server validates page bounds, item footprint, occupancy, and
cross-location identity before applying an atomic transfer or swap. Every
`PlayerSnapshot` projects the validated stash items and stash gold; the same
state is included in the version-1 authoritative save envelope. Native stash
presentation remains gated until its compatibility bridge consumes this
projection.

`SetFriendlyModeRequested.friendly_mode` is an explicit, retry-safe state
assignment. The server does not interpret repeated delivery as another toggle.
The following snapshot is the authority for the resulting mode.

`DropHeldItemRequested` is the additive command alternative at field 28. It
contains only a target tile. The Diablo module requires a server-owned held item
and an unoccupied, walkable tile no farther than one cell from the player, then
creates the world item with the same item seed, price, and complete state. A
retry cannot duplicate the item because normal command deduplication and the
following snapshot remain authoritative.

`DropItemRequested` is the additive command at field 43. It carries a stable
`PlayerItemReference` plus a target tile, allowing protocol clients to drop an
inventory, belt, or equipment item without relying on a native cursor. The
server rejects stash references, occupied or non-adjacent destinations, stale
stable identities, and drops while another cursor item is held; accepted drops
remove the source item atomically and apply content-owned world-item anchor
quests. Godot preflights the selected carried reference before sending the
intent. The cursor-only field-28 command remains unchanged for native
compatibility.

`TalkToQuestRequested` is the additive command at field 30. It names a stable
quest ID and target entity ID; the server requires a living adjacent unique
encounter whose validated identity matches the quest's completion identity.
Accepted dialogue activates and logs the quest and emits a `DialogueEvent`;
the quest's content-owned native dialogue variables advance monotonically as
part of the same authoritative transition. A dialogue definition may instead
declare a required inventory item and authored relative reward; the server
consumes the item, completes the quest, spawns the reward, and emits the
completion message atomically. It may also declare a one-time activation
reward; the server spawns it during the first accepted conversation while
leaving the quest active and suppressing duplicate rewards. Clients do not
choose message text or mutate quest state.

`TurnInQuestRequested` is the additive command at field 34. It names a
content-owned quest and native towner; the server validates the quest, level,
adjacency, authored conversation variable, and geometry. A normal turn-in then
consumes the required item, completes the quest, and spawns an optional reward.
An explicit activation-only definition instead activates an inactive quest and
may create its authored initial item without completing it. Shipped uses include
the Hellfire Farmer, Blacksmith Anvil, Little Girl, and Jersey's Jersey paths.
Repeatable authored stages may be itemless when the native conversation has no
inventory requirement. A stage may also set `allow_completed` to expose a
server-owned post-completion dialogue; clients must select it only when the
projected quest is complete, while the server remains authoritative for the
state transition and emitted message.

## State, Events, and Persistence

`Snapshot` is a complete projection for the receiving session. The server owns
simulation ticks, movement and occupancy, combat, spells and projectiles,
monsters, items, objects, quests, portals, stores, inventory, progression,
content rules, and save data. A client may interpolate presentation but must
reconcile to every snapshot.

The repeated `level_geometry` projection contains the effective level bounds,
movement-blocked cells, missile-blocked cells, and optional native d-piece
transparency values after all active content-backed world mutations. The
additive `active_world_mutation_ids` field preserves those identities through
protocol-compatible saves. Both are server-owned state: clients do not author
map changes.

The additive `set_level_object_origins` field preserves runtime-generated
set-piece origins through snapshots and protocol-compatible saves. Each entry
identifies one validated set-level origin by set-level ID, runtime level, and
origin cell; the server uses it to materialize relative set-level object
placements. An absent or empty field retains compatibility with older saves.

The additive `materialized_procedural_level_ids` field preserves the server's
procedural encounter-materialization boundary through snapshots and
protocol-compatible saves. A level remains marked after its generated monsters
are defeated, so restart or reconnect cannot respawn that encounter. An absent
or empty field retains compatibility with older saves.

The repeated player projection contains every participant with an assigned
entity ID. Its wire order is receiver-relative: the receiving participant is
first, followed by all other participants in ascending entity-ID order. IDs are
nonzero and unique. Clients use the first entry as local presentation state but
must validate the whole set. The canonical state hasher sorts all players by
entity ID, so receiver-relative player ordering cannot change the hash. The
snapshot remains a per-session projection: receiver-scoped fields such as the
singular active-store view may legitimately produce a different overall hash.

`PlayerSnapshot.level_id` accepts the 25 ordinary native level IDs and the
reserved runtime identities for materialized set-level map variants. The
`visited_level_ids` projection remains the native `_pLvlVisited` state and may
therefore contain only ordinary level IDs; a player on a set-level variant does
not need its runtime identity repeated in that list. Native compatibility
clients decode the runtime identity to the corresponding `_setlevels` value,
while older peers that do not implement this additive boundary must not be
used for set-level sessions.

`EventBatch` contains presentation-friendly deltas such as damage, healing,
experience, and validated quest dialogue. `DialogueEvent` carries the source
player, unique encounter entity, quest identity, and content-owned message ID.
Optional field 5 carries server-owned text for the active content locale; an
empty value preserves ID-only compatibility for older packs and peers. It does
not carry mutable quest state. Events are not a second source of truth. A
reconnect or dropped event cannot invalidate the next snapshot. The generic
host, Godot client, and native compatibility client each accept at most 4096
events in one batch and reject unknown event kinds, negative damage/healing
amounts, negative dialogue message IDs, or dialogue text containing NUL or
carriage-return characters or exceeding 16 KiB of UTF-8 bytes; zero entity IDs
remain tolerated for compatibility with events that do not target the local
presentation. The server event-batch tick must equal the applied tick used for
the corresponding command response or snapshot boundary. Godot and the native
compatibility client validate event payloads even when a batch is stale or
temporarily quarantined, rather than silently hiding malformed data behind
their presentation tick filters. During a Godot reconnect epoch boundary,
event batches are ignored until the next validated snapshot is accepted, so
events from the prior transport stream cannot populate the new presentation
history.
Snapshots are validated before stale-tick filtering as well; an older snapshot
is discarded without replacing state, but malformed data cannot be hidden by
the presentation boundary.

Godot and the native compatibility client accept an omitted snapshot hash for
legacy peers, but reject any non-empty hash that is not exactly 64 hexadecimal
characters. The server remains responsible for computing and verifying the
canonical digest.

`QuestSnapshot` fields 9-13 preserve native quest variables and completion
identities. Optional proto3 field 14, `turn_in_towner_id`, carries the validated
content-owned towner ordinal for authored quest interactions. Fields 15-16 add a
one-shot completion-stage interaction and its native `_qvar2` marker. Fields
17-18 add an optional alternate item and dialogue identity for an item-first
interaction at the same towner; the server consumes that item without advancing
the quest. Their absence is preserved for older peers and means the native client
keeps its legacy local interaction; when present, the fields are included in the
canonical state hash and let the client route the quest without a hardcoded
quest/towner pairing. Field 19 marks an activation-only towner row. The native
client routes that row only while the quest is in its initial state, preventing a
repeat activation command after the server has consumed its optional item.
Optional field 20 carries the authored turn-in message identity, and field 21
marks a non-consuming, item-gated dialogue-only row. The native client routes
that row while the quest is active and the current `_qmsg` differs from the
authored message; the server then records the message and leaves inventory and
quest completion state unchanged. Optional fields 22-23 carry a native
`_qvar1` completion-stage gate and marker for quests whose one-shot conversation
does not use `_qvar2`.
Optional fields 24-26 carry an active staged item hand-in's `_qvar1` gate,
marker, and progress-only flag. The latter keeps the quest active after the
server consumes the authored item and creates its reward.
Optional field 27 carries a native `_pLvlVisited` level that suppresses a
dialogue-only turn-in after the player has visited that level. The server
checks this against its authoritative visitation set; native and Godot clients
must treat the field as an availability boundary rather than local history.

`Snapshot.quest_towner_interactions` is the repeatable authored-stage
projection. Its `item_id` may be zero for an itemless native conversation;
`consume_item` remains false in that case. Field 15, `allow_completed`, is an
explicit post-completion availability bit. A client must match that bit against
`QuestSnapshot.completed` before presenting or submitting the stage, and the
server rechecks the same gate during intent execution. Fields 16-17 carry an
optional OR-gate over the player's server-owned `visited_level_ids`, preserving
native `_pLvlVisited` checks without reintroducing client-side level history.
Activation stages may also carry a required `_qvar1` or `_qvar2` value while
the quest is already active; this represents native conversations whose
availability is gated by a progress marker rather than by the initial quest
state. The server and clients apply the same variable gate before presenting
or executing that stage. Field 18, `require_unlogged`, preserves native
branches that are available only while the quest-log bit is clear; the server
and clients reject the stage after the quest is logged.
Fields 19-20 carry an optional required item identity and presence polarity.
An item-present stage retains the existing item gate; an item-absent stage is
available only when the player does not carry that item. This supports native
conversation branches that must yield to a direct item hand-in, including the
Hellfire Little Girl opening and active dialogue.
Fields 21-22 mark completion stages that may run while a quest is still in its
initial state and may reuse the quest's declared turn-in reward. This preserves
native item hand-ins that are valid before the opening conversation.
Interaction kind `4` is `CompleteAfterQuest`: it is available only when
`allow_completed` is true and the quest is already complete. Its variable gates
and markers preserve one-shot completion conversations without reopening the
quest. Field 23, `use_quest_completion_reward`, tells the server to spawn the
quest's authored completion reward at its declared position after the stage
succeeds. The additive field is ignored by older peers, which retain the
legacy completion-stage route.

`PlayerSnapshot.visited_level_ids` is the canonical per-player visitation set.
The server marks a level when the player enters it through a warp, portal, or
restart-town transition, persists the set through the save snapshot, and
projects it to both native and Godot clients. IDs are unique and sorted in
canonical projections; an omitted legacy field means the current level only.

`Snapshot.state_sha256` hashes authoritative fields in an explicit canonical
order. Stable-identity lists, including the complete participant set, are
sorted before hashing. Presentation-only and localized values are excluded.
Every newly authoritative field requires native and C# parity coverage before
clients depend on it. A non-empty value is a 64-character ASCII hexadecimal
SHA-256 digest; the generic TCP host, Godot presentation boundary, and native
compatibility boundary reject malformed values while still accepting an omitted
value from legacy snapshots. Entity IDs are non-zero and unique across players,
monsters, world items, objects, projectiles, and corpses. Monster snapshots are
limited to DevilutionX's 200-entry active monster pool per level. Digest computation and
content verification remain server-owned. Snapshot difficulty must be a defined
protocol value; portal IDs and active world-mutation IDs are non-zero and unique,
and portal owner/lifetime fields must agree with bidirectional state. Player
character names are omitted or printable ASCII strings of at most 31 characters;
the managed protocol assembly exposes this wire-level shape as
`AuthoritativePlayerNameRules` for host and client boundary validation.

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

Monster-arrow impacts use the same Q6 boundary: the server converts the
configured physical impact to native fixed-point damage before player
resolution and populates `amount_q6` with the exact event value. Older clients
continue to receive the truncated whole-point `amount` projection.

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

`MonsterSnapshot.movement_started_tick` and `movement_effect_tick` fields 95
and 96 optionally carry the server-owned one-tick movement window. The server
stamps legacy unstamped movement when it captures a snapshot, and restores the
window through version-1 saves. The timing is derived presentation metadata
and is deliberately excluded from canonical gameplay hashing. Godot uses the
window only to interpolate presentation; native occupancy and all targeting
decisions remain discrete and server-owned. Both zero values preserve
compatibility with older snapshots. Every current wire boundary rejects timing
on a stationary monster or a non-increasing nonzero window.
Godot may evaluate movement windows at a fractional presentation tick advanced by the
engine-independent client protocol projection from the server-declared tick
rate between snapshots; this is presentation-only and does not alter
authoritative occupancy or targeting.
The presentation clock is reset when the local authoritative level identity
changes, so a prior-level timing window cannot influence the new level. If a
reconnect or resync delivers an older snapshot, the clock marks that advance
as a presentation rewind; Godot snaps cached players, monsters, and projectiles
to the rewound snapshot instead of easing backward from stale positions. The
rewind marker is one-shot and does not change authoritative simulation state.

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

`MonsterSnapshot.dialogue_message_id` field 85 carries the current authored
dialogue identity for a unique encounter. A zero value means that no authored
conversation is pending. The field is presentation metadata only: clients do
not advance quest variables or execute monster AI from it. Nonzero values are
included in the conditional `godevilution-monster-dialogue-message-v1`
canonical-hash section.

Optional `MonsterSnapshot.monster_item_seed` and `monster_ai_seed` fields 86-87
preserve the native `InitMonster` runtime seeds. The server uses the item seed
for authoritative monster-owned loot. In multiplayer, the server advances the
AI seed once per monster tick, matching native `ProcessMonsters` state
progression, and restores the stream before that monster's AI random draws;
the multiplayer Zombie controller now consumes its action and movement draws
from that stream. The multiplayer SkeletonMelee controller likewise consumes
its movement and adjacent-attack admission, native delay, and RandomWalk coin
draws. The multiplayer SkeletonRanged controller likewise consumes its native
movement/retreat admission and subsequent arrow attack roll, including the
far-distance movement draw. The multiplayer Fat/Overlord controller likewise
consumes its native movement or adjacent-attack admission and RandomWalk coin
draws. The multiplayer GoatMelee/AiAvoidance controller likewise consumes its
movement, Move-goal, RandomWalk, adjacent-attack, and wounded-special draws.
The multiplayer Gargoyle controller likewise consumes its healthy AiAvoidance
movement, adjacent attacks, wounded retreat RandomWalk, and healing divisor
draws; its native special lock remains server-owned. The multiplayer ButcherAi
controller likewise consumes its non-adjacent RandomWalk coin draws; adjacent
attacks retain their no-roll native admission.
Single-player and seeded multiplayer Fallen fear retreats consume the native
`RandomWalk` coin draws before committing movement; seed-zero multiplayer
targets retain the deterministic compatibility path.
Single-player Skeleton King consumes its native action roll for summon and
adjacent-attack admission, and summons consume the native type-selection and
`InitMonster` initialization draws before their item/AI seeds are projected;
multiplayer summoning remains server-owned. Single-player and seeded
multiplayer ordinary adjacent melee roll the effective native min/max damage
range from the authoritative stream, including validated special melee ranges;
seed-zero multiplayer targets retain the legacy projection. Single-player and
seeded multiplayer adjacent monster melee also consume native hit admission,
shield/class block admission, and damage rolls in native order, rolling the
range directly in Q6 and carrying the exact applied amount in
`DamageEvent.amount_q6`; seed-zero multiplayer targets retain the compatibility
controller. Pack leadership and GroupUnity are now server-owned in the
gameplay module and survive internal snapshots. Additive protocol projection
for leader identity, relation, and pack size, plus generated pack placement and
visibility-driven activation are shipped; broader last-position interpolation
and Fallen rally behavior remain follow-up work.
Older peers may omit both fields; when present, the pair is included in the conditional
`godevilution-monster-runtime-seeds-v1` canonical-hash section.

Optional `MonsterSnapshot` fields 91-94 carry the server-owned GroupUnity
projection: `leader_entity_id`, `leader_relation` (0 none, 1 leashed, 2
separated), `pack_size`, and `active_for_ticks`. The server validates leader
 references and relationship ranges; Godot and native compatibility clients
 preserve the fields for presentation but never run GroupUnity transitions.
 Their non-default values
are included in the conditional `godevilution-monster-group-unity-v1`
canonical-hash section, while all-default legacy snapshots retain prior hashes.

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

Optional `MonsterSnapshot` fields 88-90 carry the server-owned Fallen stand
animation frame, animation tick, and remaining rally attack timer. The server
advances the animation and performs the native `FlipCoin(4)` rally admission;
clients render the state and never simulate the rally or its pursuit.

`Snapshot.corpses` carries persisted defeated non-minion corpse tiles. Corpse
identity and position are server-owned and included in canonical hashing.

When a Scavenger enters its healing goal, the server performs the native
coin-selected square scan and persists the first visible corpse identity. It
does not replace native traversal order with nearest-corpse selection; clients
only render the resulting identity and movement.

`MonsterSnapshot` fields 52-53 carry Bat retreat steps and the direction toward
the victim captured when the retreat began. The server applies the native
`4 * intelligence + 8` adjacent-attack admission and performs the two-step
retreat; clients render only the authoritative state.

`MonsterSnapshot` fields 56-57 carry Bat's server-owned native `var2`/`var1`
movement counters. Long-idle ticks use the `intelligence + 13` admission, while
a fresh movement mode with `var2 == 0` uses `intelligence + 63`; clients render
the counters and never resolve these AI branches.
The movement boundary is also server-owned for ordinary Bat identities without
an optional charge-missile mapping; clients never substitute generic pursuit.

Ranged-avoidance monsters use a server-owned native `RoundWalk` fallback after
failed or blocked far-distance Move admissions. Ordinary failed ranged
admissions use the native `RandomWalk` fallback; clients receive only the
resulting movement or unchanged snapshot. For `RoundWalk`, the server checks
the 90-degree turn, 45-degree turn, and straight candidate before the
opposite-side random-walk fallback.
Optional `MonsterSnapshot` fields 58-59 carry the persisted native `goalVar1`
movement counter and `goalVar2` turn state so a resumed server continues the
same Move goal; a blocked primary `RoundWalk` flips `goalVar2` before the
opposite-side fallback, and older peers may omit these fields.

Optional `MonsterSnapshot` fields 60-61 carry the persisted Rhino `goalVar1`
movement counter and `goalVar2` turn state. The server continues Rhino's
far-distance Move goal across ticks and saves; clients never execute it.

Optional `MonsterSnapshot` fields 62-63 carry the shared persisted
`AiAvoidance` `goalVar1` movement counter and `goalVar2` turn state used by
GoatMelee and Gargoyle. The server continues their far-distance Move goals;
clients never execute them.

Optional `MonsterSnapshot` fields 64-65 carry the shared `AiAvoidance`
`var2`-compatible idle counter and `var1`-compatible movement value. The
server applies the native long-idle, validated fresh-mode bonus, and
middle-band ordinary movement admissions for `GoatMelee` and `Gargoyle`; older
mappings retain the native 50-point bonus, older peers may omit these fields,
and clients never execute the AI.

Optional `MonsterSnapshot` fields 66-67 carry the native
`AiRangedAvoidance` `var2`-compatible idle counter and `var1`-compatible
movement value. The server applies the native long-idle, fresh-mode, and
middle-band ordinary `RandomWalk` admissions; older peers may omit these
fields, while far-distance `RoundWalk` goal state remains in fields 58-59.

Optional `MonsterSnapshot.skeleton_melee_delay_ticks` field 68 carries the
remaining native Skeleton/Fallen `AiDelay` countdown. The server consumes the
countdown before the next movement or attack decision; older peers may omit
the field and clients never execute the delay policy.

Optional `MonsterSnapshot` fields 69-70 carry the native `SkeletonBowAi`
`var2`-compatible idle counter and `var1`-compatible movement value. The
server applies the long-idle and fresh-mode close-range retreat admissions;
older peers may omit these fields and clients never execute the AI.

Optional `MonsterSnapshot` fields 71-72 carry the native Fat/Overlord
`var2`-compatible idle counter and `var1`-compatible movement value. The
server applies the validated base and intelligence-scale long-idle admission
and fresh-mode bonus, retaining the native 20/4/50 fallback for older
mappings; older peers may omit these fields.

Optional `MonsterSnapshot` fields 73-74 carry the native Rhino
`var2`-compatible idle counter and `var1`-compatible movement value. Field 75
carries the remaining native Rhino `AiDelay` countdown. The server owns these
ordinary movement and delay decisions, applying the validated 33/2/50
long-idle and fresh-mode bands with the native fallback for older mappings;
older peers may omit the fields while Rhino far-distance Move goal state
remains in fields 60-61.

Optional `MonsterSnapshot.skeleton_king_delay_ticks` field 76 carries the
remaining native Leoric/Skeleton King `AiDelay` countdown. It is server-owned,
validated, persisted by version-1 saves, and included in the canonical hash;
older peers may omit it and clients never execute LeoricAi policy.

Optional `MonsterSnapshot.hork_demon_delay_ticks` field 77 carries the
remaining native HorkDemonAi `AiDelay` countdown after an ordinary movement
admission fails. It is server-owned, validated, persisted by version-1 saves,
and included in the canonical hash; older peers may omit it and clients never
execute HorkDemonAi policy.

Optional `MonsterSnapshot.direction` field 84 carries the native monster
direction (zero through seven). The server updates it from committed movement
and uses it for direction-sensitive native behavior such as Hork Spawn's
adjacent admission and summoned-monster facing; it is persisted and included
in the canonical hash, while clients only render the resulting state.

Optional `MonsterSnapshot.ranged_avoidance_delay_ticks` field 78 carries the
remaining native `AiRangedAvoidance` no-action `AiDelay` countdown. It is
server-owned, validated, persisted by version-1 saves, and included in the
canonical hash; older peers may omit it and clients never execute the delay
policy.

Snake lateral drift is likewise server-owned. When its computed drift tile is
blocked, the server applies the native three-candidate `RandomWalk2` fallback
from the original target direction and emits no extra navigation replacement.

`MonsterSnapshot.counselor_retreat_ticks` field 55 carries the remaining native
Counselor wounded-adjacent retreat attempts. The server applies the four
opposite-direction `RandomWalk` attempts before resuming mapped ranged
dispatch; clients render only the resulting movement and snapshot state.

For the `Mega` identity, Inferno admission and its intermediate-distance
`2 * (5 * intelligence + 25)` RandomWalk fallback are server-owned. In
multiplayer, seeded Mega targets consume the restored per-monster stream for
the native action and adjacent-choice rolls, intermediate movement, recovery
delay, and immediate segment collision; seed-zero targets retain the legacy
compatibility path. A failed or blocked mapped Inferno roll never delegates
movement policy to the client.

Counselor mapped ranged rows use validated base and intelligence-scale
admission terms, capped at 100, with the native 50/5 fallback for older
mappings, followed by the 30-percent `RoundWalk` fallback; clients receive
only the resulting projectile or authoritative movement snapshot. Seeded
multiplayer Counselor targets consume the restored per-monster stream for the
initial and secondary admission rolls, adjacent admission, retreat
`RandomWalk` ordering, and server-owned recovery delays; seed-zero targets
retain the compatibility controller.

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
Seeded multiplayer Rhino targets consume the restored per-monster stream for
the initial and adjacent attack admissions, conditional far-distance Move-goal
setup, native RoundWalk/RandomWalk fallback, and recovery delay; seed-zero
targets retain the compatibility path.

Validated `AiRangedAvoidance` mappings (including Magma, Storm, Diablo,
BoneDemon, and Acid families) use a server-owned ranged boundary. Seeded
multiplayer targets consume the restored per-monster stream for the initial
10,000-band admission, conditional Move-goal setup and direction, native
RandomWalk movement, and recovery delay; clients receive only authoritative
projectile or movement state.

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
selects a native `RandomWalk` retreat cell when one is available. Seeded
multiplayer targets consume the restored per-monster stream for that admission,
the two RandomWalk ordering coins, and the post-shot delay; long-range and
seed-zero branches retain the native no-roll compatibility path. A blocked
lane preserves the native defer rather than generic pursuit.

The `Succubus` and `LazarusSuccubus` identities use the same native `AiRanged`
admission around their validated `BloodStar` mappings. Seeded multiplayer
targets consume the restored per-monster stream for the close-range admission,
the two RandomWalk ordering coins, and post-shot delay; long-range and
seed-zero branches retain the native no-roll compatibility path. Clients
receive only the authoritative projectile, movement, and delay snapshot state.

Generic `AiRanged` identities, including FireBat, Torchant, the Lich family,
and AcidUnique, use the same server-owned close-range boundary around their
validated ranged mappings. Inside four tiles the server consumes the native
`10 * (intelligence + 7)` admission and, when admitted, the two `RandomWalk`
ordering coins toward the opposite target direction. A clear long-range lane
emits the authoritative projectile and consumes the native post-shot delay
draw. Seeded multiplayer targets use the restored per-monster stream for these
draws; long-range and seed-zero branches retain the native no-roll
compatibility path. Clients receive only authoritative projectile, movement,
and delay state.

Player-owned Golems remain server-owned combat targets. When native path
planning cannot produce a step, the server consumes the two native `RandomWalk`
ordering coins using the restored minion stream and then applies the native
clockwise eight-direction fallback sweep. Clients receive only the resulting
movement and direction state; they never path-plan or consume Golem RNG.
Golem spawn snapshots also carry the native item and AI seeds after the
server consumes the spawn-time standing-animation and health draws; reconnect
and restart therefore resume the same minion stream.

The validated Snake charge mapping preserves the native non-charge boundary:
failed movement and adjacent attack admissions carry a server-owned `AiDelay`
countdown, while admitted lateral drift and attacks are applied by the server
before any generic navigation.

Optional `MonsterSnapshot.snake_delay_ticks` field 79 carries the remaining
native SnakeAi delay. Older peers may omit it; clients never execute SnakeAi or
its delay policy.

Optional `MonsterSnapshot.counselor_delay_ticks` field 80 carries the remaining
native CounselorAi delay after failed admissions or a successful mapped ranged
action. It is server-owned, validated, persisted by version-1 saves, and
included in the canonical hash; older peers may omit it and clients never
execute CounselorAi delay policy.

Optional `MonsterSnapshot.avoidance_delay_ticks` field 81 carries the remaining
shared native `AiAvoidance` `AiDelay` countdown after a failed `RoundWalk`
admission. It is server-owned, validated, persisted by version-1 saves, and
included in the canonical hash; older peers may omit it and clients never
execute the delay policy.

Optional `MonsterSnapshot.ranged_delay_ticks` field 82 carries the remaining
native `AiRanged` post-attack `AiDelay` countdown. It is server-owned,
validated, persisted by version-1 saves, and included in the canonical hash;
older peers may omit it and clients never execute the delay policy.

Optional `MonsterSnapshot.mega_delay_ticks` field 83 carries the remaining
native intermediate-distance `MegaAi` post-action `AiDelay` countdown. It is
server-owned, validated, persisted by version-1 saves, and included in the
canonical hash; older peers may omit it and clients never execute the delay
policy.

The `Zombie` identity likewise consumes an admitted action even when its
native one-step movement is blocked; clients receive the unchanged
authoritative snapshot rather than a client- or navigation-generated move.

The `HorkDemon` identity also consumes an admitted movement branch when its
native `RoundWalk` and `RandomWalk` candidates are blocked; no generic path is
substituted on the authoritative tick.

For Mega beyond the Inferno boundary, the delegated Skeleton movement likewise
consumes the tick when its native `RandomWalk` candidates are blocked; clients
receive no navigation-generated replacement move.

For Skeleton King, an admitted native `LeoricAi` `RandomWalk` also consumes the
tick when all of its candidates are blocked; clients receive no generic
navigation replacement.

For Fat/Overlord, an admitted native `RandomWalk` likewise consumes the tick
when all candidates are blocked; clients receive no generic navigation step.

For SkeletonMelee, an admitted native `RandomWalk` also consumes the tick when
all candidates are blocked; clients receive no generic navigation step.

For Butcher, an admitted native `RandomWalk` likewise consumes the tick when
all candidates are blocked; clients receive no generic navigation step.

For GoatMelee, an admitted far-distance native `RoundWalk` also consumes the
tick when all candidates are blocked; clients receive no generic navigation
step.

For GoatMelee's ordinary admitted movement, the server likewise uses the
native `RandomWalk` candidate order and consumes the tick when those candidates
are blocked; clients receive no bounded pursuit replacement.

For a healthy Gargoyle outside melee range, the server applies the native
`AiAvoidance` `RoundWalk`/`RandomWalk` candidate order after healing is not
admitted; blocked candidates consume the tick without a generic navigation
replacement.

While a Scavenger is seeking a corpse, the server uses the native five-candidate
`RandomWalk` order and emits no movement replacement when those candidates are
blocked.

When a Gargoyle is adjacent, the server applies its native
`23 + 2 * intelligence` `AiAvoidance` attack admission before emitting the
authoritative melee result; a failed roll emits no client-side action.

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
`ProjectileSnapshot.behavior` uses the stable `AuthoritativeProjectileBehavior`
identities (0 through 59). The value remains authoritative simulation state
and a compatibility field; clients do not use it to make gameplay decisions.
Native and managed snapshot validators, resume hashing, and legacy presentation
fallbacks use the same named identity contract.
`ProjectileSnapshot.presentation_kind` field 53 is an additive server-owned
presentation family. Current servers populate it for every projectile so
presentation clients do not need to maintain the behavior-to-visual mapping;
older peers may omit it and clients retain their behavior/phase compatibility
fallback. Charged Bolt behavior `1` uses `Lightning`; Firebolt behavior `2` and
Fireball behavior `16` use `Fire` during travel and `FireImpact` during impact.
The protocol assembly exposes the maximum presentation-family identity and
native-visual identity alongside the behavior limit; managed validators and the
native bridge consume those shared limits rather than duplicating wire bounds.
The same protocol limit covers `StatusEffectSnapshot.behavior`; zero is the
unspecified compatibility value and the shipped identities are Infravision,
Search, and Etherealize.
Item snapshots use the shared maxima for presentation kind, spell source kind,
item action kind, and spell targeting kind; these bounds are protocol identities
and are not inferred from native item IDs.
Authored object snapshots likewise use shared bounds for object presentation,
effect, quest-transition, reward, reward-origin, projectile-source, and ordered
follow-up kinds; quest/towner interaction kinds use the same cross-client limit.
Event batches and bounded snapshot collections use shared caps for event count,
world mutations, quests, set-level object origins, and materialized procedural
levels so native projection and managed validation reject the same oversized
payloads.
Native set-level IDs and monster leader-relation values likewise use shared
compatibility maxima across the managed mapper, validators, and native bridge.
Every projected tile coordinate uses the shared native `0..255` wire range;
managed mapper/host/Godot validators and native snapshot/world projection call
the same named limit before applying level geometry bounds. This includes
authored towner greeting positions, so a future geometry or presentation path
cannot silently widen one boundary.
Projectile damage channels/phases and unique-monster dialogue states use the
same shared maxima across managed validation and native projection.
`ProjectileSnapshot.native_visual` field 56 carries the exact native
compatibility sprite identity for presentation-only rendering. A zero value
retains the behavior-based fallback for older servers; an unknown nonzero value
is rejected by the native presentation bridge rather than being interpreted as
gameplay state. Lightning Wall's shared `FireWallSegment` behavior uses the
explicit `LIGHTNING_WALL` identity and the Lightning presentation family when
its damage channel is Lightning; ordinary Fire Wall and Ring of Fire segments
continue to use the Fire Wall identity.
`ProjectileSnapshot.native_missile_id` field 57 carries the validated
content-owned native missile identity for native monster-magic projectiles.
It is limited to 128 UTF-8 bytes and must use the validated native identity
syntax. An empty value preserves the enum/behavior compatibility fallback, and
older peers ignore this additive field. When both `native_visual` and
`native_missile_id` are present, clients must use the server-owned
`native_visual`; the string is a compatibility fallback only when the enum is
unspecified. This keeps content identity out of the presentation decision for
current clients.
`ItemStateSnapshot.presentation_kind` is additive server-owned metadata for
world-item presentation. `Gold` and `Ear` identify the shipped currency and
dynamic PvP-ear families; zero or unknown values retain generic item rendering,
and clients do not infer these identities from native numeric fields.
`ObjectSnapshot.presentation_kind` is additive server-owned object metadata for
the shrine, quest, breakable, and revealed-trap visual families. Trap metadata
is emitted only when the existing trap-reveal boundary permits it; zero or
unknown values retain generic object rendering.

`ObjectSnapshot.quest_id` field 7 is the authored quest identity associated with
an interactive object. When present, the server validates the linked quest,
advances it atomically with object activation, and includes the identity in the
tagged `godevilution-object-quest-v1` canonical-hash section; clients never
infer or mutate this relationship locally.

`ObjectSnapshot` fields 17-20 carry the optional authored quest transition
kind, amount, and required `_qvar1`/`_qvar2` values. The server validates the
linked quest and applies the transition atomically with object activation;
clients must treat these fields as projection metadata and never mutate quest
state locally. Absent transition fields retain the legacy no-transition
meaning; required-variable values are interpreted only when a transition kind
is present.
`ObjectSnapshot` fields 21-25 carry an optional authored item reward kind,
stable item or unique identity, item level, and relative placement offsets.
The server validates the referenced catalog entry and walkable, unoccupied
placement before activation, generates the item with the authoritative item
catalog, and persists the resulting world item. Absent reward fields retain
the legacy no-reward meaning; clients render the resulting `ItemStateSnapshot`
and never generate rewards locally.
`ObjectSnapshot.projectile_source_kind` field 32 carries the authored projectile
origin. Zero preserves target-origin compatibility; the activating-player value
is currently valid for Phasing rows. Nonzero source state uses the tagged
`godevilution-object-projectile-source-v1` canonical-hash extension.
Optional `ProjectileSnapshot` fields 54-55 carry a dynamic MonsterArrow
physical damage range. When present, the server rolls that range at collision
time; zero fields retain the fixed `projectile_damage` payload behavior. The
range is persisted and included in canonical hashing so a resumed arrow keeps
the same native damage boundary.
A Teleport cast uses the existing projectile behavior value `9` as a
server-owned two-tick controller. Its position and remaining lifetime are
authoritative; the resulting player position continues to use the existing
player snapshot coordinates. The additive `Teleport` presentation family is a
client-only transition cue and does not authorize prediction or destination
selection. Version-1 peers therefore require no new wire field, and a resumed
save revalidates occupancy before the controller's first movement.

Native `Arrow` rows for GoatRanged and SkeletonRanged select behavior value
`59`, `MonsterArrow`; older `Targeted` rows are accepted as a compatibility
mapping. The server retains the launch admission result and target coordinates,
advances one authoritative tile per tick, destroys a breakable blocking object,
removes blocked or expired missiles, and rolls the monster's dynamic physical
damage range only on collision. A zero spell ID is valid for this monster-owned
behavior; snapshots, saves, hashes, and the additive range fields carry the
complete resumable state. Player-owned arrows follow the same native
breakable-object transition through the shared protocol-neutral simulation.
During travel, the server checks each traversed tile in native order and
resolves the first living monster or non-owner player encountered before the
declared endpoint target. At the final impact tick, a declared monster or
player target must still occupy its endpoint tile; leaving that path produces
an authoritative miss and projectile removal. The resulting impact and
projectile removal remain server-owned and resumable through the existing
fields.
For validated player arrows, `Speed` carries the native `AddArrow` pixel
velocity, including class-level and Hellfire Quick/Fast/Faster/Fastest bonuses;
`RandomArrowVelocity` begins from the native 16–47 roll. `PathTicks` carries
the fractional tile remainder and `RemainingTicks` is derived from that speed,
so cadence and collision path survive snapshots, saves, hashes, and protocol
projection without a new wire field. Legacy arrows retain the default 32-pixel
one-tile cadence. Hellfire PvP shield blocks retain the arrow's authoritative
impact phase and native rotation RNG draw before the projectile is retired.

Persistent Acid puddles check their occupied tile on every active tick. A
breakable missile-blocking object is destroyed through the same server-owned
collision transition, while the puddle restores its active lifetime and only
enters its existing impact phase at expiry.

Compatibility generic projectiles likewise destroy a breakable missile-blocking
object before their blocked travel is removed; clients observe only the
resulting authoritative object and projectile state.

Apocalypse booms retain their persistent lifetime when they collide with a
breakable missile-blocking object: the server destroys the object through the
native collision transition and continues publishing the boom state until its
authoritative expiry.

Hork Spawn uses the same current-tile collision boundary as native
`ProcessHorkSpawn`: before movement or expiry, the server checks a living
player on the missile tile with the non-arrow `PlayerMHit` rules. A successful
hit or block removes the missile and proceeds through the normal expiry summon
path; a miss leaves the controller available for its next tick. A breakable
missile-blocking object on the current tile is destroyed before that same
expiry/summon transition. A newly blocked destination remains authoritative
until the following tick's current-tile check, matching native movement. The
collision is authoritative gameplay state, not a client-side visual effect.

A Phasing cast uses projectile behavior value `15` as the same shape of
server-owned two-tick movement controller. The server selects its random
destination, consumes the cast source even when no candidate exists, and
revalidates occupancy before movement. The additive `Phasing` presentation
family is only a client transition cue; clients neither send a destination that
governs the selection nor consume RNG, and they observe the controller through
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

Nova Ball movement also honors the authoritative world-object boundary. A
non-passable object removes the ball on collision, except when the object is a
validated shrine on the ball's target tile; that native shrine interaction
keeps the ball alive for its remaining lifetime. A regular breakable blocker is
destroyed before the ball is removed. The optional shrine capability is carried
through snapshots, saves, protocol projections, and canonical hash state, while
clients only render the resulting projectile.

An Apocalypse cast uses behavior value `32` for its row-major scan controller
and value `33` for each persistent Physical boom. The server owns cursor
progression, blocked-cell and player-minion filtering, Hellfire line-of-sight,
and first-hit persistence; clients only render the resulting state. The
additive presentation projection classifies the controller as `Fire` and the
persistent boom as `FireImpact`, while legacy clients use the same behavior
fallback.

An Elemental cast uses behavior value `35` for its retargeting projectile and
nine-cell burst. The server owns destination selection, retargeting, line of
sight, collision, damage, and burst order; the additive presentation projection
uses `Fire` for both travel and burst phases, matching the native `firerun`
visual family.

A Guardian cast uses behavior value `36` for its stationary turret controller.
The server owns its duration, scan order, line-of-sight, firing RNG, and emitted
Firebolt projectiles; the additive presentation projection uses `Guardian` so
Godot can render the native turret state without simulating its behavior.

Flash uses native behaviors `5`, `6`, and `7` for its controller and two visual
layers. The server owns the repeated collision and damage timing; the additive
`Flash` presentation projection lets Godot render the native blue flash without
inferring spell behavior from a generic projectile.

Rage behavior `34` is a server-owned controller with no world sprite. The
additive `HIDDEN` presentation value tells current clients to suppress it;
older peers may ignore the value and retain their behavior-based compatibility
path.

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
player statuses. Value `1` is Infravision, value `2` is Search, and value `3` is
Etherealize. Clients use them only for presentation: native maps Search to the
original automap item highlight, native and Godot surface Etherealize as the
server-owned immunity presentation, and Godot outlines world items. Duration,
Infravision recast union, Search additive recasts, expiry, immunity, and
level-transition cleanup remain server decisions. Nonzero behavior adds the tagged
`godevilution-status-effect-behavior-v1` canonical-hash section after the legacy
effect ID, duration, and magnitude, so an omitted value preserves prior hashes.
Unknown behavior values and nonzero magnitude for any typed behavior are invalid
complete snapshot state.

`ObjectSnapshot` fields 10 through 12 carry native missile pass-through,
breakability, and persistent destruction. Field 13 carries additive armed-trap
state for the Rogue Trap Disarm capability. The server validates impossible flag
combinations, excludes destroyed objects from occupancy and interaction, and
removes them from native presentation. Object and projectile collision
extensions use separate tagged canonical-hash sections so all-default legacy
snapshots preserve their prior hashes. Version-1 save envelopes remain
wire-compatible and preserve the additive raw fields in their Protobuf payload.
Field 15 carries the optional native shrine capability used by Nova Ball's
target-tile collision rule; it is likewise persisted and included in the
canonical hash when enabled.
Field 40 carries the native occupancy-trigger distinction used by movement
prediction: a live object with `activate_on_occupancy` set is enterable and
activates on entry, while other live objects block the tile. The additive value
is included in the tagged `godevilution-object-occupancy-v1` hash section.

Player class identity is authoritative and persists through snapshots and
server-owned saves. `PlayerSnapshot.character_class_id` field 19 carries the
stable ID from the active module's validated class catalog. Shipped content
keeps ID `0` as Warrior, preserving the Protobuf default used by older peers and
saves. Native compatibility clients reject IDs outside their supported hero
class enum. Nonzero IDs are included in the canonical state hash and covered by
matching native/C# golden tests. For newly created Diablo players, the server
also derives maximum life and mana from the selected class's native-compatible
Q6 level and attribute formulas; command-line values remain explicit
development overrides rather than the normal creation path. The standalone
server may likewise select an authored starting level with `--starting-level`;
omitting it retains the active content pack's default.

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
`memorized_spells` field 21 carries learned spell IDs and levels. Each learned
spell may also carry additive server-owned `display_name` and targeting metadata
from the active validated spell catalog; older peers may ignore it. The server
validates native bounds and uniqueness, saves this state, and sorts learned
spells for protocol and hash projection. Native compatibility clients rebuild
their ability and memorized-spell masks from the snapshot rather than retaining
local creation state. When either field contains state, canonical hashing adds
the tagged `godevilution-player-spells-v1` section; an entirely empty spell
projection adds nothing so older state hashes remain unchanged.

Item-backed spell sources retain their existing `ItemStateSnapshot` identity and
source-kind fields. Additive `spell_display_name` and `spell_targeting_kind`
metadata carry the server-owned content projection; zero preserves compatibility
for older snapshots and non-spell items.

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
Firebolt uses the same server-owned player-target admission boundary, with its
native magic hit roll and Q6 damage retained through the resumable projectile
state; the checked-in two-participant replay exercises this path without client
damage or RNG decisions.
Fireball uses that same boundary for its direct travel collision and then
reuses the server-owned player-target rules during the ordered impact splash;
the complete participant snapshot carries any Q6 damage, death, and combat-mode
transition while the projectile remains resumable through its impact lifetime.
In Hellfire, a shield block is still a collision even though the projectile is
retained for its impact lifetime; the server consumes the native rotation draw
and publishes the retained phase before removal.

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
Every boundary rejects Stand with a nonzero end tick and Block/GotHit with a
zero end tick, even when no action is currently active.
Nondefault mode state adds the tagged
`godevilution-player-combat-mode-v1` canonical-hash section and round-trips
through version-1 saves. Hellfire content also selects the native post-block
rotation RNG draw before Charged Bolt enters its common impact phase.

`PlayerSnapshot.current_action` field 25 is present while a native-timed move,
attack, ranged attack, cast, death, level transition, or restart-in-town remains
in progress. It carries the start, effect, ready, and end ticks; whether the
effect has already resolved; and the action's direction, target, or complete
cast request.
The snapshot tick is part of that contract: `started_at_tick` must not be in the
future, `end_tick` must not be behind the snapshot, and `effect_applied` is false
before `effect_tick` and true at or after it. This is enforced at managed,
Godot, and native validation boundaries so presentation and save restore cannot
admit an impossible action timeline. The managed simulation, mapper, host, and
Godot protocol client consume the same domain timing-rule source, while the
native client keeps the equivalent boundary check for compatibility.
Movement reserves the adjacent destination until its end tick, and presentation
clients expose that reservation as the future or predicted position. Deferred
casts preserve their spell, source kind, source item, and target so save
restoration cannot silently substitute a different resource source.

Death action kind 4 is already applied at its start tick, requires zero life
and Death combat mode, records a nonzero killing source as its target, and uses two
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
`godevilution-player-action-v1` canonical-hash section; a nonzero cast-source
stable item identity adds the tagged
`godevilution-player-item-reference-stable-id-v1` field within that section.
An absent action or legacy zero stable identity adds nothing and retains all
earlier hashes.

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

`PlayerSnapshot.light_radius` field 41 carries the native-clamped player vision
radius derived from identified, usable equipped `+light` modifiers. The server
owns the calculation and clients use it only for presentation and visibility;
zero preserves compatibility with snapshots written before this field existed.
Non-default values are included in the canonical state hash under the tagged
`godevilution-player-light-radius-v1` section.

`PlayerSnapshot.base_mana_maximum` and `base_life_maximum` fields 31 and 32
carry the unmodified native maxima used by Hellfire equipment conversions. A
zero value falls back to the effective maximum for snapshots written before
these fields existed. Accepted equipment transfers recompute effective maxima
and current resources while preserving native resource debt. When base and
effective maxima differ, canonical hashing adds the tagged
`godevilution-player-resource-base-v1` section; equal values preserve earlier
hashes.

`PlayerSnapshot.visited_level_ids` field 35 carries the server-owned native
`_pLvlVisited` set. The server marks entries on authoritative level entry and
persists the set through the version-1 save codec. The current level is always
implicit for legacy snapshots; previously visited IDs are unique, sorted, and
included in the canonical hash when they add history beyond the current level.

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

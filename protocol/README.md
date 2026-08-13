# Protocol Package

`devilution.proto` is the canonical, transport-independent Protobuf schema for
the native client, C# authoritative server, and Godot C# client. The initial
transport is TCP: each serialized `Envelope` is prefixed by a four-byte
little-endian payload length. Empty payloads and payloads larger than 1 MiB are
rejected.

`protocol/Devilution.Protocol/Devilution.Protocol.csproj` is the only managed
binding generator. The C# server host, Diablo adapter, Godot transport, and
tests all reference that assembly, so a process contains one identity for every
generated wire type. The native build generates C++ bindings only when
configured with:

```powershell
cmake -S . -B build-authoritative -DDEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT=ON
```

That option activates the `authoritative-client` vcpkg manifest feature, which
provides Protobuf for the native adapter. The legacy network provider remains
the default when the option is off. See `docs/ai/msvc-build.md` for the verified
Windows toolchain command.

## Compatibility Rules

- Package `devilution.protocol.v1` and schema version `0.2.0` identify the
  current production contract; `0.1.0` remains the native compatibility alias.
- Schema version `0.2.0` retains the additive wire fields but gives
  `content_manifest_hash` and `ruleset_identity_hash` independent meanings:
  the former is the validated ordered content manifest SHA-256 and the latter
  is the combined content-and-gameplay-module identity. Both hashes are
  required in the `ClientHello` for this version.
- Existing field numbers and meanings are immutable. Removed fields must be
  reserved, and breaking changes require a new package and schema version.
- Additive fields and `oneof` alternatives must be safe for older peers to
  ignore. Required behavior is negotiated during the handshake.
- `client_sequence` is scoped to a resumable session. Retrying a command with
  the same sequence returns the recorded result without applying it twice.
- The server assigns `server_receipt_sequence` and `applied_tick`; command
  acknowledgements explicitly report acceptance, rescheduling, rejection, or
  duplication.
- Clients submit intent. The server owns all resulting resources, damage,
  placement, movement, world state, and persistence.
- `CastRequested.source` declares an ability, memorized spell, charged item, or
  consumable item. Item sources include an exact `PlayerItemReference`; the
  server validates ownership and consumes mana, one charge, or the item only
  after full cast validation. Source zero is compatibility inference from
  authoritative owned state, never permission to cast an unowned spell.
- `IdentifyItemRequested.source` distinguishes the Bard's zero-cost class skill
  from the existing paid vendor service. Value zero retains vendor behavior for
  older peers; class-ability requests must match the server-owned loadout and
  exact owned item reference.
- `RepairItemRequested.source` applies the same additive distinction to the
  Warrior's class skill. Value zero remains full paid vendor repair; the class
  source authorizes only the server's native random repair-and-degradation rule.
- `RechargeItemRequested.source` distinguishes the Sorcerer's Staff Recharge
  skill from paid vendor recharge. Value zero retains vendor behavior; the
  class source authorizes only the server-owned native charge-restoration and
  maximum-charge-loss rule for the exact selected staff.
- `ClientHello.requested_character_class_id` is an optional new-session
  selection from the active game module's validated catalog. When it is absent,
  the module selects its content default. Existing in-memory or restored save
  state takes precedence on resume, and an unknown requested ID is rejected.
- `ClientHello.requested_character_name` is an optional new-session creation
  hint. The Diablo module applies native multiplayer name validation before
  creating state. Resumed in-memory or saved state keeps its existing name;
  reconnect hints cannot rename a character.
- `SetFriendlyModeRequested.friendly_mode` sets explicit state rather than
  toggling it, so a retry is idempotent. `PlayerSnapshot.hostile_mode` is the
  inverse projection: its absent/false Protobuf default preserves native
  friendly mode for older peers.
- `RestartTownRequested` is the empty command intent at field 27. The server
  admits it only for terminal Death after the timed death action has cleared,
  then owns the content-selected level, position, Q6 life, zero mana, cleared
  store state, and transition timing.
- `DropHeldItemRequested` is command field 28 and supplies only an intended
  tile. The server validates distance, occupancy, and the held-item lifecycle;
  accepted drops preserve the item seed, price, and full state.
- A snapshot is the complete authoritative projection for its session. Events
  are transient presentation aids and never replace the following snapshot.
- `Snapshot.players` contains every assigned participant. The receiving
  participant is first so a presentation client can identify its local player;
  the remaining participants are sorted by stable entity ID. Player entity IDs
  must be nonzero and unique. Canonical hashing independently sorts the complete
  player set by entity ID, so receiver-specific wire order does not change the
  state hash.
- `PlayerSnapshot.character_class_id` is the stable ID from the active game
  module's validated class catalog. An absent field has Protobuf's zero value;
  shipped content reserves ID `0` for Warrior so older snapshots and peers keep
  their original meaning. Nonzero class IDs participate in canonical hashes.
- `PlayerSnapshot.character_name` field 28 is server-owned character identity.
  Nonempty names use the tagged `godevilution-player-name-v1` canonical-hash
  extension and cross saves, reconnect, native projection, and Godot
  presentation. Empty remains the additive legacy default.
- `PlayerSnapshot.mana_shield_active` field 29 is the server-owned persistent
  Mana Shield state. True adds the tagged `godevilution-player-mana-shield-v1`
  canonical-hash section and crosses version-1 saves plus native/Godot
  presentation; false is the additive default. The Diablo module alone converts
  incoming Q6 damage to mana, reverses the reduction on depletion, and clears
  the flag on exact depletion or level-changing transitions.
- `PlayerSnapshot.ability_spell_id` is the native-compatible class skill and
  `memorized_spells` carries stable spell IDs with nonzero levels. The list is
  canonicalized by spell ID. Native peers reject IDs above their supported
  spell range and rebuild their ability/learned-spell masks from each snapshot.
- Spell state extends the canonical player hash only when the ability is
  nonzero or the learned-spell list is nonempty. The tagged
  `godevilution-player-spells-v1` suffix preserves every pre-extension hash for
  legacy zero-state snapshots while giving C++ and C# one deterministic order.
- Hostile player state extends the canonical player hash only when
  `hostile_mode` is true, using the tagged
  `godevilution-player-hostile-mode-v1` suffix. Friendly legacy snapshots retain
  their existing hashes. Charged Bolt player hits use exact Q6 damage events;
  friendly-fire admission, hit, block, resistance, damage, and RNG order are
  server-owned.
- `PlayerSnapshot.combat_mode` field 23 mirrors native `PLR_MODE` ordinals and
  `combat_mode_end_tick` field 24 is the first tick on which a timed mode is no
  longer active. Stand/zero are additive defaults. Nondefault state uses the
  tagged `godevilution-player-combat-mode-v1` hash extension and is preserved
  by version-1 saves and both presentation clients.
- `PlayerSnapshot.current_action` field 25 carries an optional move, attack,
  ranged-attack, cast, death, level-transition, or restart-in-town lifecycle.
  Its timing boundaries, applied flag, direction or target, and complete cast intent allow
  saves and reconnecting clients to preserve an in-progress effect. A move
  reserves its adjacent destination, which native and Godot clients present as
  the future position. Death (kind 4) is already applied, requires zero life,
  and retains Death mode after its content-timed action clears. Level transition
  (kind 5) records its portal target after applying the destination and holds
  NewLevel for a one-tick barrier before Stand. Warp uses the same action kind
  with its normalized level-trigger target and native two-tick lifetime. Restart in Town (kind 6) has no
  target, arrives after its destination and resource reset are already applied,
  and uses the same content-selected NewLevel barrier.
  Present state uses the tagged `godevilution-player-action-v1` hash extension;
  an absent action preserves all earlier hashes.
- `PlayerSnapshot.held_item` field 26 is server-owned cursor state, outside the
  inventory grid and cached Gold total. Partial Gold pickup preserves the
  original world seed on the remainder; explicit drop and combat death return
  it to the world. Present held state uses the tagged
  `godevilution-player-held-item-v1` hash extension, while absence retains old
  hashes and save compatibility.
- `PlayerSnapshot.facing_direction` field 27 uses the native `Direction`
  ordinal. South/zero is the additive legacy default. The server changes facing
  only for accepted movement, attack, and cast intents, persists it in saves,
  and uses it for directional death-drop placement. Non-South state uses the
  tagged `godevilution-player-facing-v1` hash extension.
- `PlayerSnapshot.base_mana_maximum` and `base_life_maximum` fields 31-32 carry
  the unmodified native maxima used by Hellfire equipment conversions. Zero
  falls back to the effective maximum for older snapshots. When a conversion
  changes an effective maximum, the tagged
  `godevilution-player-resource-base-v1` hash extension carries both values;
  equal base/effective maxima preserve legacy hashes.
- `QuestSnapshot` fields 9-12 carry optional native quest synchronization values:
  `_qvar1`, `_qvar2`, `_qmsg`, and a validated monster identity whose defeat
  advances the quest. Zero values are additive defaults and preserve legacy
  hashes and peer compatibility.
- `ItemStateSnapshot.base_name`, `identified_name`, and optional
  `native_cursor_graphic` fields 59-61 carry runtime-authored presentation for
  dynamic items such as PvP ears. A dynamic identity is complete only when
  both names and the byte-representable cursor are present. Present state uses
  the tagged `godevilution-dynamic-item-presentation-v1` hash extension; absent
  fields retain existing item hashes. Lethal player damage supplies these
  fields from authoritative victim and content state, and clients project them
  without generating the ear locally.
- `ItemStateSnapshot.plus_hit_points` and `plus_mana` carry native Q6 item
  resource bonuses; fields 62-63 preserve native Hellfire's player-dependent
  conversions as item metadata. The server applies both bonus values and each
  percentage to the character's base Q6 resource when equipment stats are
  resolved; clients never infer a fixed bonus from the item name. Nonzero
  conversion state uses the tagged
  `godevilution-item-resource-conversion-v1` canonical-hash extension.
- Equipped item fields `plus_damage` (percent), `plus_damage_modifier` (flat),
  `plus_to_hit`, and `plus_enemy_armor_class` (armor piercing) are consumed by
  the authoritative player attack resolver only when the item is usable and
  identified when magical; clients never apply these combat bonuses locally.
- Equipped `plus_get_hit` is consumed by the authoritative autonomous monster
  physical-hit resolver under the same usability and identification boundary;
  clients never adjust incoming damage from item presentation.
- Native life/mana-on-hit equipment flags are resolved by the server after a
  successful physical attack, including deterministic random life steal and
  `NoMana` suppression; clients consume the resulting resource state and
  healing events rather than reimplementing item effects.
- Equipped item `armor_class` and `plus_armor_class` are folded into the
  server-owned player armor state with native dexterity and identification
  rules; clients never recompute defensive statistics from item names.
- Equipped `plus_strength`, `plus_magic`, `plus_dexterity`, and `plus_vitality`
  are applied by the server to current attributes under native usability and
  identification rules; clients consume the resulting authoritative state.
- The server applies each active class's Q6 item-vitality and item-magic factors,
  flat item resources, and Hellfire conversions to authoritative life and mana
  maxima; clients do not derive resource maxima from item metadata.
- `Snapshot.random_state`, `MonsterSnapshot.monster_level`, exact Q6 monster
  health fields 16-17, `DamageEvent.amount_q6`, `HealingEvent.amount_q6`,
  projectile runtime fields 14-27,
  `ProjectileSnapshot.last_collision_target_entity_id` field 28, and
  `ProjectileSnapshot.effective_spell_level` field 29 are additive
  authoritative state used by deterministic spell movement, collision
  suppression, replay, events, and persistence. A missing Q6 health field is
  reconstructed from its legacy whole-point field. Fractional values and a
  nonzero last collision target use tagged canonical-hash extensions, while old
  and whole-aligned snapshots keep their previous hashes. Firebolt uses these
  fields for its native speed-26 16.16 path, swept collision, exact resistant
  damage, impact phase, and deterministic resume point. Lightning uses field 29
  with the other runtime fields to resume its invisible controller and
  level-scaled persistent segments without consulting mutable player state.
  Teleport uses behavior value `9` with the same existing position and lifetime
  fields for an invisible two-tick controller; authoritative movement appears
  through the existing player coordinates, so no new protocol field is needed.
  Phasing uses behavior value `15` for another invisible two-tick movement
  controller. Its destination and RNG are server-owned, and a no-candidate cast
  can consume its source without producing a projectile. Clients observe only
  existing projectile fields and authoritative player coordinates.
  Fireball uses behavior value `16`; phase `0` is its server-owned 16.16 travel
  and phase `1` is its stationary 14-tick impact. Existing origin, movement,
  damage, resistance, lifetime, and spell-level fields preserve deterministic
  resume without adding a protocol field. Clients render the two phases but do
  not select splash targets or resolve damage.
  Blood Star uses behavior value `20` for its fixed native magic projectile;
  Bone Spirit uses behavior value `21` for its native homing projectile and
  seven-tick impact phase. Both remain server-owned, including resistance,
  current-life-third Bone Spirit damage, and their immediate self-damage.
  Fire Wall uses behavior values `22` and `23` for its server-owned wall
  controller and persistent segments. Additive projectile fields 30 through 38
  carry branch positions, directions, stop flags, and segment hit direction so
  clients can render restored walls without simulating growth or damage.
  Flame Wave uses behavior values `24` and `25` for its server-owned placement
  controller and moving fire segments. Existing projectile movement, damage,
  lifetime, collision, and direction fields carry the complete resumable state.
  Inferno uses behavior values `26` and `27` for its server-owned moving
  controller and stationary fire segments. Existing projectile position,
  damage, lifetime, and collision fields carry the complete resumable state.
  Chain Lightning uses behavior value `28` for its one-tick fan-out controller
  and value `29` for each Lightning-compatible moving bolt; resulting segments
  use the existing Lightning segment behavior and projectile fields.
  Nova uses behavior value `30` for its one-tick radial controller and value
  `31` for each continuing Lightning ball. Existing projectile fields carry the
  native mirrored fan-out, movement, damage, lifetime, and collision state.
  Apocalypse uses behavior value `32` for its row-major scan controller and
  value `33` for each persistent Physical boom. Existing projectile fields carry
  its cursor, filtering, lifetime, and first-hit state.
  Rage uses behavior value `34` for its persistent active/cooldown controller;
  existing projectile phase and remaining-tick fields carry its resumable state.
  Elemental uses behavior value `35`; existing target, phase, movement, and
  new burst-origin fields carry its retargeting and persistent fire-burst state.
  Guardian uses behavior value `36` for its stationary turret controller;
  `ProjectileSnapshot.guardian_state` field 44 carries its firing animation
  countdown through snapshots and the wire protocol.
  Resurrection uses the existing cast `target_entity_id` for the selected
  participant and runtime projectile behavior value `11` for its stationary
  beam. The beam's target, position, and lifetime use existing projectile
  fields, while revived position, life, mana, mode, and action state use the
  existing complete player snapshot.
  Heal Other also uses the existing cast `target_entity_id`. Its living,
  distinct, same-level, and range checks, randomized Q6 healing, class modifier,
  and resource consumption are server-owned; the result uses the existing
  Healing event and complete player snapshot without a new protocol field.
  Stone Curse uses behavior value `13` with the same target, position,
  remaining-tick, phase, impact-lifetime, and effective-spell-level fields for
  its stationary duration/shatter controller.
- `Snapshot.portals` field 11 carries server-owned static and dynamic portal
  endpoints. Dynamic Town Portal rows retain owner, bidirectional-use, and
  remaining-tick state; clients render these endpoints but never create,
  expire, or resolve a portal locally. The field is additive for readers that
  predate dynamic portal projection.
- `StatusEffectSnapshot.behavior` field 4 identifies typed server-owned status
  semantics without asking a client to infer them from an effect ID.
  `STATUS_EFFECT_BEHAVIOR_INFRAVISION` value `1` drives native and Godot
  presentation. `STATUS_EFFECT_BEHAVIOR_SEARCH` value `2` drives native
  automap and Godot world-item highlighting. The server alone owns both
  lifecycles, including Search's additive recasts and transition cleanup.
  `STATUS_EFFECT_BEHAVIOR_ETHEREALIZE` value `3` marks the level-scoped native
  monster-immunity status. A nondefault behavior uses the tagged
  `godevilution-status-effect-behavior-v1` canonical-hash extension; omitted
  behavior preserves legacy generic-status hashes. The server alone owns each
  status lifecycle and transition cleanup.
- `ObjectSnapshot` fields 10-12 carry missile pass-through, breakability, and
  persistent destruction, while field 13 carries additive armed-trap state for
  the Rogue Trap Disarm capability. Field 14 carries an optional linked-quest
  progress amount. Nondefault object collision state uses the tagged
  `godevilution-object-collision-v1` hash extension. Native projection removes
  activated or destroyed objects from interaction and presentation occupancy.
- `MonsterSnapshot` fields 18-21 carry native-compatible moving occupancy:
  movement state, the adjacent future tile, and petrification. Native
  projection marks the current tile as positive occupancy and the future tile
  as negative occupancy. Charged Bolt can hit a normal walker only at its
  current tile, while a petrified walker is hittable at either tile. Nondefault
  movement or petrification uses the tagged
  `godevilution-monster-occupancy-v1` hash extension.
- `MonsterSnapshot.stone_curse_immune` field 22 persists the external monster
  capability used by later server-owned casts. True uses the tagged
  `godevilution-monster-stone-curse-immunity-v1` hash extension; false remains
  the additive compatibility default.
- `MonsterSnapshot.is_undead` field 23 persists the external monster
  capability used by the server-owned Holy Bolt cast. True uses the tagged
  `godevilution-monster-undead-v1` hash extension; false remains the additive
  compatibility default.
- `MonsterSnapshot` fields 25-32 persist Berserk eligibility metadata, native
  physical damage channels, and active berserked state. Nondefault values use the
  tagged `godevilution-monster-berserk-v1` canonical-hash extension; old
  readers may ignore these additive fields.
- `MonsterSnapshot.owner_player_id` field 33 identifies the authoritative
  player that owns a summoned minion. It is zero for ordinary monsters and is
  retained through snapshot restore and canonical state identity.
- `MonsterSnapshot` fields 34-35 carry the externalized native death-animation
  duration and remaining terminal ticks. Golem's 12-frame death sequence uses
  these fields; the server alone decrements and expires the dead minion. The
  nondefault values use the tagged
  `godevilution-monster-death-animation-v1` canonical-hash extension.
- `MonsterSnapshot.acid_resistance` field 36 carries the validated native acid
  resistance percentage. Nonzero values use the tagged
  `godevilution-monster-acid-resistance-v1` canonical-hash extension; zero is
  the additive compatibility default.
- `MonsterSnapshot.is_demon` field 37 carries the validated native monster-class
  capability used by demon-specific equipment; clients do not infer faction
  from monster names.
- `MonsterSnapshot.is_animal` field 38 carries the validated native monster-class
  capability used by animal-specific weapon modifiers; clients do not infer
  class from monster names. True uses the tagged
  `godevilution-monster-animal-v1` canonical-hash extension; false remains the
  additive compatibility default.
- `MonsterSnapshot.has_knockback` field 39 carries the validated native monster
  ability that displaces a player after a successful autonomous hit. True uses
  the tagged `godevilution-monster-knockback-v1` canonical-hash extension.
- `MonsterSnapshot.experience_reward` field 40 carries the validated native
  reward used by the server-owned monster death boundary; older peers may
  ignore it while still receiving the ordinary experience event.
- `MonsterSnapshot.to_hit` and `to_hit_special` fields 41-42 carry validated
  native normal and special monster attack accuracy. The server applies normal
  accuracy at the autonomous attack boundary; clients never resolve monster
  hit rolls. Zero values preserve the additive compatibility fallback for
  older hand-built targets and peers.
- `MonsterSnapshot.native_ai` field 43 carries the validated native
  `MonsterAIID` dispatch identity. The server owns AI execution; clients treat
  this as state metadata and do not dispatch behavior from it.
- `MonsterSnapshot.intelligence` field 44 carries the validated native
  intelligence value used by server-owned AI probability bands. Clients do not
  resolve AI rolls from this metadata.
- `MonsterSnapshot.gargoyle_special_active` field 45 carries the server-owned
  Gargoyle distance-based special lock. Clients render the resulting Healing
  event and snapshot state; they do not execute Gargoyle healing or unlock it.
- GoatRanged attack and avoidance outcomes use ordinary authoritative monster
  snapshots and directed Damage events; no client-side AI or hit roll is needed.
- GoatMelee admission and special attacks likewise resolve on the server and
  arrive as ordinary authoritative Damage events.
- SkeletonMelee attack admission is server-owned and produces either an
  authoritative Damage event or no event for an outside-band tick.
- Zombie action admission and long-range movement are server-owned; clients
  render only the resulting monster snapshot and gameplay events.
- Fat normal and special melee bands are server-owned; clients receive only the
  resulting authoritative Damage event or unchanged state.
- Native monster regeneration is applied by the server before AI execution and
  appears through the authoritative monster health snapshot.

## Identity Note

Protocol v1 predates a separate ruleset field. The current server therefore
places the combined content-and-module ruleset SHA-256 in both
`content_manifest_hash` and `ruleset_identity_hash`. Current v1 clients must
send that combined hash in `content_manifest_hash`; they should also send it in
`ruleset_identity_hash`. In protocol v2, clients send the separately printed
raw content manifest hash and combined ruleset identity in their corresponding
fields. A client that supplies `ruleset_identity_hash` rejects a server
response with a different ruleset identity before accepting the initial
snapshot.

## Test Vectors

`test-vectors/command-delivery-retry.json` is the language-neutral delivery
vector for lost acknowledgements, safe retries, strict late rejection, and
lenient rescheduling. The C# tests consume it directly. Native protocol and
replay behavior is covered by the opt-in CMake test targets.

See `docs/authoritative-protocol-contract.md` for the complete boundary and
evolution rules.

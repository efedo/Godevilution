# Protocol Package

`devilution.proto` is the canonical, transport-independent Protobuf schema for
the native client, C# authoritative server, and Godot C# client. The initial
transport is TCP: each serialized `Envelope` is prefixed by a four-byte
little-endian payload length. Empty payloads and payloads larger than 1 MiB are
rejected.

Managed consumers use `AuthoritativeProtocolLimits` from the generated protocol
assembly for the shared 16 KiB dialogue-text and 128-byte identity boundaries;
the native bridge mirrors those limits in its C++ validator.

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
- Protocol fields 54 (monster `unique_identity`) and 13 (quest
  `completion_unique_identity`) are additive opaque server-owned identities;
  each identity is limited to 128 UTF-8 bytes, and monster identities are only
  valid for unique monsters. Content-owned `ProjectileSnapshot.native_missile_id`
  uses the same 128-byte identity limit. Clients may ignore these identities
  while preserving them through authoritative snapshots.
- `client_sequence` is scoped to a resumable session. Retrying a command with
  the same sequence returns the recorded result without applying it twice.
- Command batches and command acknowledgements are each limited to 256 entries
  by the shared protocol limits consumed by the host, Godot client, and native
  compatibility client.
- `DialogueEvent.text` carries server-owned localized presentation text. It is
  limited to 16 KiB of UTF-8 bytes and may not contain NUL or carriage-return
  characters; validators on the server, Godot client, and native bridge apply
  the same boundary. An empty text retains the legacy message-ID fallback.
- `OpenStoreRequested` selects a validated vendor or service store, while
  `CloseStoreRequested` clears the active store and is safe to repeat. Clients
  wait for the authoritative snapshot to remove `active_store` instead of
  clearing vendor state optimistically.
- `OpenStashRequested` and `CloseStashRequested` explicitly control the
  server-owned stash session and are safe to repeat. `MoveItemRequested` and
  the stash gold commands are rejected while the stash is closed. Repair,
  Recharge, and Identify requests that target a stash item are rejected while
  the stash is closed as well. A snapshot's `stash_open` flag is authoritative;
  stash and vendor sessions cannot overlap.
- `DepositStashGoldRequested` and `WithdrawStashGoldRequested` transfer only
  the requested amount between the server-owned wallet and stash gold. The
  server validates the open session and available balance before changing
  either total.
- `SplitGoldRequested` moves a validated amount from an inventory or belt Gold
  stack to `PlayerSnapshot.held_item`; the server rejects a second held stack and
  reduces the projected wallet by the split amount while retaining the held
  stack separately. `DropHeldItemRequested` then returns that
  server-owned cursor stack to the world.
- `SortInventoryRequested` atomically reorganizes carried inventory using the
  native size ordering and placement scan. A held cursor stack is rejected;
  clients wait for the authoritative snapshot before updating their layout.
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
- `PlayerItemReference.stable_item_id` is an additive stable content identity
  paired with the location and slot. The server uses it to reject a stale slot
  that now contains a different item; zero preserves older clients and
  positional legacy commands.
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
- `ReadBookRequested` consumes an exact owned book reference (or the legacy
  inventory index), validates the server-projected readable-book action and
  native intelligence requirement, then owns spell learning, level increases,
  native mana refund, and book consumption. Clients never mutate learned-spell
  state locally.
- `UseConsumableRequested` consumes an exact owned inventory or belt reference.
  `ItemActionKind` projects the complete validated action set, including
  attribute-all and all four oil actions; oil requests carry an additional
  target reference.
  An optional `target_item` carries the exact inventory or equipment item for
  cursor-based oils; the source and target are validated atomically.
  The server validates the content-owned potion, rejuvenation, or elixir
  action, applies native Q6 life/mana restoration, class multipliers, elixir
  stat/resource transitions, Hellfire full-resource effects, and `NoMana`
  suppression, or applies a validated Blacksmith, Accuracy, Sharpness, or
  Fortitude oil to the target, then consumes the source and returns the
  authoritative snapshot. The native client does not apply these effects
  locally.
- `TurnInQuestRequested` identifies a content-owned quest and native towner.
  The server validates the active quest, level, towner adjacency, native
  conversation variable, and required item before consuming the item,
  completing the quest, and spawning any validated unique reward.
- `TalkToTownerRequested` identifies a general native townsperson greeting.
  The server validates the content-owned towner position, level, adjacency,
  and empty cursor, then emits the native message identity as a dialogue event;
  store menus remain client presentation after the event is accepted.
- `MoveInventoryItemRequested.item` is an additive exact inventory reference.
  When present, the server rejects a stale slot before changing the inventory
  grid; an absent or zero-valued reference retains positional compatibility.
- `MoveItemRequested` also supports the server-owned stash. Stash references
  use `PlayerItemLocation.STASH`, a page, and a 10x10 grid cell; snapshots carry
  the validated stash item projection and stash gold. Older clients omit these
  additive fields and retain their previous inventory/belt/equipment behavior.
- `AutoEquipItemRequested` accepts an exact inventory item reference and owns
  the native automatic equipment selection and any atomic displacement back to
  inventory. An optional `equipment_slot` preserves the hand, ring, or other
  equipment slot selected by an explicit native drag; omission retains the
  automatic first-fit policy. The server rejects stale, incompatible, or
  over-capacity requests without partially changing the loadout.
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
- `ProjectileSnapshot.presentation_kind` is an additive server-owned visual
  family. `MONSTER_MAGIC`, `HORK_SPAWN`, `GUARDIAN`, and `FLASH` identify native
  monster/turret/spell visuals, while `HIDDEN` marks server-only controllers
  such as Rage. Godot uses these values for presentation; older peers may
  ignore them and fall back to the behavior identity.
- `ItemStateSnapshot.presentation_kind` is an additive server-owned world-item
  family. `GOLD` and `EAR` identify dynamic item visuals without making the
  client infer item identity from native numeric fields; zero or unknown values
  retain the generic item presentation.
- `ObjectSnapshot.presentation_kind` is additive server-owned object
  presentation metadata. `SHRINE`, `QUEST`, and `BREAKABLE` identify safe
  visual families; `TRAP` is emitted only when trap state is already revealed.
  `NATIVE_ARMOR_STAND` and `NATIVE_WEAPON_RACK` identify authored native
  presentation families without requiring clients to maintain native-ID tables.
  Zero or unknown values retain the generic object presentation.
- `ObjectSnapshot.native_object_id` uses the native object identity range
  (`0` through `OBJ_LAST`, currently `108`) when supplied; larger values are
  rejected by native, managed, and Godot validators.
- `LevelGeometrySnapshot.warp_triggers` is additive presentation metadata for
  authored level transitions. Each trigger carries its server-owned source
  cell, destination, stable symbol, quest gate, and current availability;
  a nonzero quest gate must reference a quest in the same snapshot; clients
  may render a marker but never choose or apply the transition.
- `LevelGeometrySnapshot.native_object_layer` is an additive row-major
  projection of server-owned raw DUN object-layer values (`0` through the
  current native `DUN_OBJECT_ID_LAST` value `146`). It is presentation metadata
  only; clients must not infer gameplay authority from it, and larger values
  are rejected by the native and managed validators. This range is distinct
  from `ObjectSnapshot.native_object_id`, which carries the converted
  authoritative object identity and remains bounded by `OBJ_LAST` (`108`).
- `LevelGeometrySnapshot.native_transparency` values must fit the native
  signed-byte transparency-region representation (`0` through `127`) for the
  compatibility client; out-of-range values are rejected before presentation.
- `LevelGeometrySnapshot.native_mega_tile_layer` is an additive row-major
  projection of composed native mega-tile IDs. Values must fit the native
  byte-sized mega-tile identity (`0` through `255`), and its dimensions are the
  ceiling of the level dimensions divided by two. Fine-grid dimensions cannot
  exceed the native `112x112` arrays, and a supplied coarse layer cannot exceed
  `40x40`; the native and managed protocol validators reject larger values.
  A `112x112` town projection without a coarse layer remains valid. This is
  presentation metadata only. When a server-owned world mutation replaces a
  native set-piece presentation, the updated coarse layer is delivered through
  the next authoritative level-geometry snapshot; clients must replace their
  cached layer rather than infer a local map transition.
- `Snapshot.set_level_object_origins` carries the server-owned origins for
  runtime-generated set-piece object groups. Entries are keyed by validated
  set-level ID and include the runtime level plus origin cell; relative
  `set_level_objects.tsv` placements are materialized from this state. The
  field is additive and absent/empty preserves older snapshot behavior.
- `Snapshot.materialized_procedural_level_ids` carries the server-owned set of
  levels whose procedural encounter population has already been created. It
  prevents a save/restart from respawning an encounter after its monsters have
  been defeated. The field is additive and absent/empty preserves older
  snapshot behavior.
- `Snapshot.players` contains every assigned participant. The receiving
  participant is first so a presentation client can identify its local player;
  the remaining participants are sorted by stable entity ID. Player entity IDs
  must be nonzero and unique. Canonical hashing independently sorts the complete
  player set by entity ID, so receiver-specific wire order does not change the
  state hash.
- Entity IDs for monsters, world items, objects, projectiles, and corpses share
  the same server-owned namespace as players and must also be nonzero and
  globally unique within a snapshot. Version-1 save snapshots may retain a
  zero player ID because the identity is rebound when the save is imported;
  live protocol snapshots must use the assigned nonzero ID.
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
  `memorized_spells` carries stable spell IDs with nonzero levels. Each learned
  spell may also carry an additive server-owned `display_name` from the active
  validated content pack; older peers can ignore it and continue using the ID.
  The list is canonicalized by spell ID. Native peers reject IDs above their
  supported spell range and rebuild their ability/learned-spell masks from each
  snapshot.
- `PlayerSnapshot.ability_kind` is the additive semantic class-skill role from
  validated loadout content. Godot uses it for typed presentation and service
  intents instead of inferring class behavior from numeric spell IDs; an
  unspecified value preserves compatibility for older snapshots.
- `ItemStateSnapshot.spell_source_kind` is the server-projected classification
  of an item-backed spell source (`CHARGED_ITEM` or `CONSUMABLE_ITEM`). Godot
  uses it to enumerate cast sources and does not infer staffs, scrolls, or
  runes from native miscellaneous IDs. `ItemStateSnapshot.spell_display_name`
  is additive server-owned presentation metadata for those sources; an empty
  value preserves compatibility for older snapshots and non-booked spells.
- `ItemStateSnapshot.stable_item_id` is the additive stable content identity
  carried with generated item state. It is authoritative for server catalog
  resolution when present; `item_index` remains the native compatibility
  projection. Zero preserves legacy snapshots and clients, and the field is
  intentionally outside the legacy replay hash until a versioned hash
  extension is introduced.
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
- `QuestSnapshot` fields 9-14 carry optional native quest synchronization values:
  `_qvar1`, `_qvar2`, `_qmsg`, a validated monster identity and unique identity
  whose defeat advances the quest, plus an optional content-owned towner identity
  for turn-in routing. The towner field is proto3-optional, so older snapshots
  omit it and retain legacy behavior; when present it is a validated native
  towner ordinal and participates in the canonical state hash.
- `QuestSnapshot` fields 15-23 carry additive authored towner routing metadata:
  completion-stage markers, alternate-item dialogue, activation-only state,
  an optional turn-in message plus non-consuming dialogue-only flag, and an
  optional native `_qvar1`-only completion-stage gate and marker. The latter
  lets a native client route an item-gated quest conversation without consuming
  the item or changing completion state; after the server records the message,
  the client returns to the normal local towner path.
- `QuestSnapshot` fields 24-26 carry an active staged item hand-in's optional
  `_qvar1` gate, marker, and progress-only flag. The server consumes the
  authored item and reward while retaining the quest's active/completion state;
  absent fields preserve the completing-turn-in behavior.
- `Snapshot.towner_greetings` carries validated content-owned native towner
  locations and greeting message identities. Godot clients use these entries
  to discover clicked towners and submit `TalkToTownerRequested`; the text
  itself may remain in the legacy localization catalog during migration.
- `QuestSnapshot.native_quest_id` field 28 optionally carries the validated
  native quest ordinal (`0` through `23`). It must be unique within a snapshot.
  Compatibility clients use it to hydrate legacy quest records without deriving
  an ordinal from a stable content ID; older snapshots omit it and retain their
  local compatibility state.
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
  Targeted Firebolt, Charged Bolt, and Bone Spirit casts may name a living
  remote player; the server validates the participant target before creating
  the same projectile lifecycle, and player impact resolution remains
  authoritative.
  Teleport uses behavior value `9` with the same existing position and lifetime
  fields for a server-owned two-tick controller; authoritative movement appears
  through the existing player coordinates, so no new protocol field is needed.
  Godot may render the additive `Teleport` transition cue, but never chooses or
  predicts the destination. Phasing uses behavior value `15` for another
  server-owned two-tick movement controller. Its destination and RNG are
  server-owned, and a no-candidate cast can consume its source without
  producing a projectile. Godot may render the additive `Phasing` transition
  cue while observing only the existing projectile fields and authoritative
  player coordinates.
  Fireball uses behavior value `16`; phase `0` is its server-owned 16.16 travel
  and phase `1` is its stationary 14-tick impact. Existing origin, movement,
  damage, resistance, lifetime, and spell-level fields preserve deterministic
  resume without adding a protocol field. Clients render the two phases but do
  not select splash targets or resolve damage.
  `ProjectileSnapshot.behavior` uses the stable `AuthoritativeProjectileBehavior`
  identities (0 through 59); this is simulation state carried for resume and
  legacy compatibility, not a client-owned gameplay decision. Current clients
  prefer the server-owned presentation fields below.
  `ProjectileSnapshot.presentation_kind` field 53 is an additive server-owned
  presentation family. Current servers populate it for every projectile so
  Godot does not need to maintain the behavior-to-visual mapping; older peers
  may omit it and clients retain their behavior/phase compatibility fallback.
  `ProjectileSnapshot.native_visual` field 56 is the corresponding exact
  native-compatibility sprite identity. The native client consumes this
  presentation-only metadata instead of inferring a sprite from gameplay
  behavior; zero retains the legacy behavior fallback and unknown nonzero
  values fail closed.
  `ProjectileSnapshot.native_missile_id` field 57 carries the validated
  content-owned native missile identity when a presentation client needs the
  source row rather than only its compatibility visual. Empty retains the
  legacy enum/behavior fallback, and the field is only valid for native
  monster-magic projectiles. When both fields are present, clients use the
  server-owned enum and consult the string only as a compatibility fallback
  for older snapshots that omit `native_visual`. Older peers ignore this
  additive field.
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
  Native `MultipleArrows` uses behavior value `58` for server-owned weapon
  arrows. The first arrow is emitted at the ranged attack effect and the two
  spread arrows carry a two-tick activation delay. Additive fields 46 through
  52 retain physical Q6 damage, captured arrow elemental channels, hit state,
  deferred Hellfire proc state, and activation delay; clients render the
  volley but never resolve its damage.
  Native GoatRanged and SkeletonRanged Arrow rows use behavior value `59` for
  server-owned `MonsterArrow` missiles. The server captures resolved physical
  damage, advances their travel, and applies player damage only on collision;
  zero spell identity is valid for this monster-owned lifecycle and no new
  wire field is required.
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
- `ObjectSnapshot.is_shrine` field 15 carries the server-owned shrine capability
  used by Nova Ball target-tile collision. Field 16 carries the additive typed
  object presentation family. Fields 17-20 carry authored quest-variable
  transition kinds, amounts, and required variable gates; clients submit the
  object entity but never apply these transitions locally. Fields 21-25 carry
  authored normal/unique item reward identity, item level, and relative spawn
  offset; clients must not construct or consume the reward from these metadata
  fields. Fields 26-27 carry server-owned projectile target offsets, and fields
  28-31 carry ordered quest-sequence follow-up kind, amount, and relative
  offsets. These fields are additive projection metadata; the server validates
  and applies the complete operation atomically.
- `ObjectSnapshot` fields 37-38 optionally select a published set-level origin
  for an item reward and carry its native set-level identity. Zero preserves
  legacy object-relative reward behavior; clients must treat both fields as
  server-owned projection metadata.
- `ObjectSnapshot.dialogue_message_id` field 39 carries an optional authored
  dialogue identity emitted when an object activates. It may be nonzero when
  `quest_id` is zero, allowing dialogue-only environmental objects to be
  discovered and routed without client-side message tables.
- `ObjectSnapshot.projectile_source_kind` field 32 carries the authored
  projectile origin. Zero preserves target-origin compatibility; the additive
  activating-player value is currently valid for Phasing rows. Nonzero source
  state uses the tagged `godevilution-object-projectile-source-v1` hash
  extension, and clients treat it as projection metadata rather than gameplay
  authority.
- `ObjectSnapshot.native_object_id` field 33 is optional compatibility metadata
  for native presentations. Stable `object_id` remains the authoritative
  server-owned identity; current presentation families are authored by the
  server's `object_presentations.tsv` sidecar and published in field 16.
  Older peers ignore the additive family field and may continue using the
  native identity fallback; zero keeps the existing numeric presentation path.
- `MonsterSnapshot` fields 18-21 carry native-compatible moving occupancy:
  movement state, the adjacent future tile, and petrification. Native
  projection marks the current tile as positive occupancy and the future tile
  as negative occupancy. Charged Bolt can hit a normal walker only at its
  current tile, while a petrified walker is hittable at either tile. Nondefault
  movement or petrification uses the tagged
  `godevilution-monster-occupancy-v1` hash extension.
- `MonsterSnapshot` fields 95-96 optionally carry the server tick at which a
  one-tick walk was admitted and the first tick at which it completes. Zero
  values preserve older snapshots. The timing is derived presentation metadata
  and is excluded from canonical gameplay hashing, so clients interpolate
  without changing deterministic state.
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
- `MonsterSnapshot` fields 46-47 carry server-owned Fallen fear countdown and
  retreat direction. Fields 48-49 carry Sneak hidden state and retreat ticks;
  hidden monsters are excluded from authoritative targeting. Fields 50-51
  carry Scavenger healing ticks and its selected corpse identity. These values
  are state metadata only; clients never execute the AI.
- Optional `MonsterSnapshot` fields 88-90 carry Fallen's standing-animation
  frame, animation tick, and remaining rally attack timer. The server performs
  the animation-boundary `FlipCoin(4)` admission, self-healing, and pursuit;
  clients render these values without simulating the behavior.
- `Snapshot.corpses` carries persisted defeated non-minion corpse tiles. Corpse
  identity and position are server-owned and included in canonical hashing.
- `MonsterSnapshot` fields 52-53 carry server-owned Bat retreat steps and its
  attack direction. The server owns Bat attack admission and retreat movement;
  clients render the resulting snapshot without executing the AI.
- `MonsterSnapshot` fields 56-57 carry the server-owned native Bat movement
  counters corresponding to `var2` and `var1`; clients render these values but
  never resolve Bat movement admissions.
- Optional `MonsterSnapshot` fields 58-59 carry the server-owned native
  `AiRangedAvoidance` `goalVar1` movement counter and `goalVar2` turn state.
  A blocked primary `RoundWalk` may flip `goalVar2` before its fallback.
  Older peers may omit the fields; clients render the values but never execute
  the ranged-avoidance goal.
- Optional `MonsterSnapshot` fields 60-61 carry the corresponding server-owned
  Rhino `goalVar1` movement counter and `goalVar2` turn state. Older peers may
  omit them; clients never execute Rhino movement policy.
- Optional `MonsterSnapshot` fields 62-63 carry the shared server-owned
  `AiAvoidance` `goalVar1` movement counter and `goalVar2` turn state used by
  GoatMelee and Gargoyle. Older peers may omit them; clients never execute the
  movement goal.
- Optional `MonsterSnapshot` fields 64-65 carry the shared server-owned
  `AiAvoidance` `var2`-compatible idle counter and `var1`-compatible movement
  value. Older peers may omit them; clients never execute ordinary movement
  admissions.
- Optional `MonsterSnapshot` fields 66-67 carry the server-owned native
  `AiRangedAvoidance` `var2`-compatible idle counter and `var1`-compatible
  movement value. Older peers may omit them; clients never execute ordinary
  `RandomWalk` admissions.
- Optional `MonsterSnapshot.skeleton_melee_delay_ticks` field 68 carries the
  server-owned native Skeleton/Fallen `AiDelay` countdown. Older peers may omit
  it; clients never execute the delay policy.
- Optional `MonsterSnapshot` fields 69-70 carry the server-owned native
  `SkeletonBowAi` `var2`-compatible idle counter and `var1`-compatible movement
  value. Older peers may omit them; clients never execute close-range retreat.
- Optional `MonsterSnapshot` fields 71-72 carry the server-owned native
  Fat/Overlord `var2`-compatible idle counter and `var1`-compatible movement
  value. Older peers may omit them; clients never execute Fat movement policy.
- Optional `MonsterSnapshot` fields 73-75 carry the server-owned native Rhino
  ordinary movement counters and `AiDelay` countdown. Older peers may omit
  them; clients never execute Rhino movement or delay policy.
- Optional `MonsterSnapshot.skeleton_king_delay_ticks` field 76 carries the
  server-owned native Leoric/Skeleton King `AiDelay` countdown. Older peers may
  omit it; clients never execute LeoricAi policy.
- Optional `MonsterSnapshot.hork_demon_delay_ticks` field 77 carries the
  server-owned native HorkDemonAi `AiDelay` countdown. Older peers may omit it;
  clients never execute HorkDemonAi policy.
- Optional `MonsterSnapshot.ranged_avoidance_delay_ticks` field 78 carries the
  server-owned native `AiRangedAvoidance` no-action delay countdown. Older
  peers may omit it; clients never execute the delay policy.
- Optional `MonsterSnapshot.snake_delay_ticks` field 79 carries the server-owned
  native SnakeAi delay countdown. Older peers may omit it; clients never
  execute SnakeAi policy.
- Optional `MonsterSnapshot.counselor_delay_ticks` field 80 carries the
  server-owned native CounselorAi delay countdown. Older peers may omit it;
  clients never execute CounselorAi delay policy.
- Optional `MonsterSnapshot.avoidance_delay_ticks` field 81 carries the
  server-owned shared `AiAvoidance` delay countdown after a blocked native
  `RoundWalk`. Older peers may omit it; clients never execute the delay policy.
- Optional `MonsterSnapshot.ranged_delay_ticks` field 82 carries the
  server-owned native `AiRanged` post-attack delay countdown. Older peers may
  omit it; clients never execute the delay policy.
- Optional `MonsterSnapshot.mega_delay_ticks` field 83 carries the server-owned
  native intermediate-distance `MegaAi` post-action delay countdown. Older
  peers may omit it; clients never execute the delay policy.
- `MonsterSnapshot.dialogue_message_id` field 85 carries the current authored
  dialogue identity for a unique encounter. It is zero when the target has no
  pending authored conversation; clients present the identity but never advance
  quest state or execute monster AI from it. The value is included in the
  conditional `godevilution-monster-dialogue-message-v1` canonical hash.
- `MonsterSnapshot.dialogue_state` field 97 carries the server-owned state of an
  authored unique-monster dialogue route: `1` means the target is waiting for
  the next conversation step and `2` means its authored dialogue is complete
  and normal combat may resume. `0` preserves the legacy fallback for older or
  unauthored routes. Non-zero states are valid only for unique monsters, and
  the state participates in the conditional `godevilution-monster-dialogue-
  state-v1` canonical hash.
- Optional `MonsterSnapshot.monster_item_seed` and `monster_ai_seed` fields
  86-87 preserve the two native `InitMonster` runtime seeds across snapshots.
  The item seed is the server-owned loot seed; the AI seed is retained for
  native multiplayer synchronization parity and advances once per authoritative
  multiplayer monster tick before that monster's AI random stream is restored.
  Seeded Fallen fear retreats consume the restored stream's two native
  `RandomWalk` coin draws; seed-zero targets retain the compatibility path.
  Older peers may omit both fields.
- Optional `MonsterSnapshot` fields 91-94 carry server-owned GroupUnity state:
  `leader_entity_id`, `leader_relation` (0 none, 1 leashed, 2 separated),
  `pack_size`, and `active_for_ticks`. The server validates leader references
  (including authored unique leaders) and owns all relationship transitions;
  older peers may omit these fields.
- The `SkeletonKing` identity drives a server-owned summon boundary. The server
  selects a level-valid `SkeletonMelee` definition and emits the resulting
  authoritative monster snapshot; clients never create or simulate summons.
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
- Succubus Blood Star missiles are created and resolved by the server; clients
  render the existing Blood Star projectile behavior and resulting directed
  damage events without simulating monster AI or magic rolls.

## Level Geometry Compatibility

`LevelGeometrySnapshot.native_monster_layer` is an additive, cell-aligned raw
DUN monster conversion layer. Values are bounded to the native conversion
range (0 through 157), and older peers may ignore the field.

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


MonsterMagic native visuals 29–35 identify the seven content-owned missile families and remain optional for older clients.

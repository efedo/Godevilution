# Godevilution Migration Status

Status date: 2026-08-14

## Current state

| Area | State | Remaining work |
| --- | --- | --- |
| Protocol and sessions | Operational vertical slice with one generated C# protocol assembly shared by Server and Godot, including self-first complete multiplayer snapshots, per-session class selection, class-skill/learned-spell state, explicit cast sources, resume precedence, a twelve-cycle server soak plus an eight-cycle live Godot-client reconnect soak while another participant remains connected, and protocol v0.2 independent content/ruleset handshake identities adopted by production launchers while preserving the v0.1 alias | Add broader reconnect/multiplayer fixtures, physical player-versus-player attack admission, and production-client soak coverage |
| Deterministic replay | Shared C++/C# gate hashing complete multiplayer participant state, with server-owned corpse tiles, native per-tick monster regeneration, refreshed ordinary-death and spell-timeline checkpoints, deterministic quest-pool selection/rejection, a 1,000-tick autonomous regeneration soak, a multiplayer autonomous-tick participant fixture with explicit secondary-session command routing, shared melee and ranged elemental-equipment fixtures covering native fire/lightning and arrow channels, resistance, and RNG, native/C# chained-action timing coverage for first-frame attack skips, live protocol coverage for the checked-in Hellfire encounter and Sorcerer loadout overlays, plus a composed-manifest Hellfire ranged replay | Expand broader multi-actor autonomous and reconnect replay coverage |
| Stores and inventory | C# remote for the shipped vendor slice | Expand content coverage and continue exact legacy edge-case characterization |
| World gameplay | C# remote vertical slice | Expand AI, level generation, quests, dialogs, towners, environmental behaviors, and multiplayer coverage |
| Persistence | Server-owned snapshot saves with an explicit version-1 compatibility codec and validated saved-class reconstruction for cold sessions | Add concrete migrations when formats change and broader recovery/soak testing |
| Native client | Explicit legacy or authoritative runtime mode | Continue removing compatibility-pool assumptions as Godot presentation matures |
| Godot client | Complete for protocol-boundary demonstrations, including typed Monk Search, Rogue Trap Disarm, and Etherealize presentation, live Etherealize, Barbarian Rage, Monk Search, Rogue Trap Disarm, Bard Identify, Warrior Repair, and Sorcerer Recharge service coverage, a stable authoritative spell-source selector, same-instance reconnect with bounded automatic retry, right-click authoritative monster attack intent routing, live entity-targeted attack action coverage, server-owned stealth target presentation, and authoritative player action indicators | Physical player-versus-player attack admission, production assets, broader prediction, menus, packaging, and complete play flow |
| Domain kernel | Independent dependency-free .NET 8 assembly | Extract additional game-neutral deterministic primitives only where reuse warrants it; keep Diablo state in its module |
| Data runtime | Independent .NET 8 assembly with ordered overlays, schema identities, dependency-version constraints, and transactional activation, depending only on Domain | Document future table extensions and broaden reload fixtures |
| Engine composition | Independent .NET 8 assembly with generic deterministic lifecycle, module compatibility preflight, transactional candidate preparation, atomic candidate registration, coordinated ruleset publication, and generation leases with explicit retired-target draining, depending only on Domain and Data | Keep filesystem/reflection outside Engine and broaden replacement fixtures |
| Diablo module | Independent .NET 8 assembly with content activation, transport-neutral simulation, and state capture/restore, depending only on Domain, Data, and Engine | Expand exact legacy gameplay/content coverage and register future capabilities through Engine |
| External content | Explicitly manifested base and Hellfire variant packs plus schema-aware, dependency-versioned composite-key overlays, including all six native-compatible classes, their complete native animation timings, complete initial loadouts, authoritative definitions for every directly castable starting source, the Rogue Trap Disarm capability identity, native level-range generation metadata, validated level-aware monster candidate selection, a bounded server-owned seed-stable spawn planner exposed through Diablo content activation and optionally consumable as generated combat targets with deterministic validated drops, unique/Diablo encounter flags, native death-animation durations, native experience rewards, native normal and special monster attack accuracy, native monster resistance metadata, the validated Hellfire Hork Demon AI identity, Hork Spawn missile mapping and server-owned speed-8 nine-tick expiry summon controller, and the Hellfire Blood Star, Bone Spirit, Fire Wall, Lightning Wall, Flame Wave, Inferno, Chain Lightning, Nova, Apocalypse, Rage, Elemental, Guardian, Town Portal, Reflect, Magi, Mana, Ring of Fire, Immolation, Jester, Berserk, Warp, Golem, and Etherealize spell behaviors | Externalize remaining cursor-driven class capabilities and native spells, unique Hellfire quest behavior and rewards, native map synthesis, and remaining item metadata; and extend the table reference as new schemas land |
| Gameplay modules | Typed dependency-ordered capabilities, inclusive semantic dependency-version and Engine API ranges, atomic activation, preflight diagnostics, a first-party Diablo factory, explicit hash-verified Server packages, and repeated eight-generation replacement/retirement coverage | Add further first-party capabilities |
| Host adapters | Independent .NET 8 generic Host and Diablo adapter assemblies with exact dependency tests; composition-only .NET 10 executable | Add configurable adapter selection if additional authoritative game adapters are introduced |
| Lua removal | Hellfire archive activation and the native post-bootstrap reload pipeline no longer require Lua; compatibility inventory established; authoritative data extensions use validated overlays; player/display diagnostics, item and monster spawning/diagnostics, towner interaction, level export, TRN selection, level reset/quest-warp/custom-map warp and seed inspection, automap reveal/hide and search, player rejuvenation, player health/mana adjustment, attribute max/min, level-up, player gold adjustment, spell-level adjustment, and quest diagnostics now use a deterministic native command registry | Replace remaining native debug/admin and compatibility script hooks, then remove the Lua REPL and presentation capabilities |

## Implemented authoritative systems

- Fallen fear is now a server-owned monster state: nearby Fallen receive the
  native `max(8 - level, 2)` retreat countdown and source-to-survivor direction
  at the death boundary, with deterministic walk fallback and snapshot,
  save, protocol, and hash coverage.
- Native GoatRanged and SkeletonRanged Arrow mappings now create explicit
  server-owned monster-arrow projectiles. Their resolved physical damage is
  captured at admission, travel is collision-checked per authoritative tick,
  and impact damage, player death, snapshots, saves, and protocol projection
  remain server-owned instead of applying ranged damage immediately.
- Native Sneak stealth is now server-owned: its intelligence-derived visibility
  band, hidden target exclusion, and nine-tick post-hit retreat persist through
  snapshots, saves, protocol, and canonical hashes.
- Native Scavenger corpse seeking and healing are now server-owned. Defeated
  non-minion monsters become persisted corpse tiles; Scavenger healing goals,
  deterministic navigation, native base/Hellfire healing, and Hellfire corpse
  consumption cross snapshots, saves, protocol, and canonical hashes.
- Native SkeletonKing summoning is now server-owned. A validated, level-aware
  SkeletonMelee content boundary creates deterministic summons on an available
  adjacent cell; clients receive ordinary authoritative monster snapshots and
  never execute the summon AI.
- Native Bat adjacent attacks now use the `4 * intelligence + 8` admission
  band, then retain the native two-step retreat direction and countdown through
  snapshots, saves, protocol, and canonical hashes.
- Native Butcher pursuit now has an explicit server-owned AI boundary: the
  monster follows deterministic navigation until adjacent, then uses the shared
  authoritative melee path.
- Native SkeletonMelee movement now uses the server-owned `35 - 4 * intelligence`
  admission band, delaying or navigating deterministically before shared melee.
- Native SkeletonRanged now has a server-owned four-tile retreat boundary and
  ranged-only idle path; generic melee is no longer admitted for validated bows.
- Native GoatMelee non-adjacent movement now uses a server-owned
  `28 + 2 * intelligence` avoidance admission before shared melee resolution.
- Native Fat/Overlord non-adjacent movement now uses a server-owned
  `20 + 4 * intelligence` admission before shared melee resolution.
- Native Stone Curse now retains a defeated petrified target through its
  authoritative shatter phase; ordinary defeated monsters become persisted
  corpse tiles, and snapshots preserve the distinction between the two states.

- Handshake validation, bounded framing, retry/deduplication, reconnect tokens,
  acknowledgements, snapshots, events, and state hashes.
- Network snapshots now atomically capture every assigned participant. The
  receiver is first for local-client identity, remote players follow by entity
  ID, and canonical hashing sorts the full set so receiver-relative player
  order cannot create a deterministic mismatch.
- Player movement, occupancy, portals, resources, levels, status effects,
  combat, spells, projectiles, monsters, drops, and world-item pickup.
- Golem death-animation timing is externalized from the native 12-frame data;
  remaining terminal ticks are server-owned and deterministic through minion
  cleanup, snapshots, saves, hashes, and protocol projection.
- Etherealize status state is projected as a typed server-owned effect and
  surfaced by the Godot HUD without client-side immunity or timing logic.
- The checked-in Hellfire pack selects the native game variant through a
  validated `combat.tsv` overlay, adds native-derived dungeon level identities
  3, 4, 9, and 17 through 24 with normalized 40-by-40 bounds, and carries a
  native-derived encounter set plus the Undead Crown, Empyrean Band, Optic
  Amulet, Ring of Truth, Harlequin Crest, Veil of Steel, Arkaine's Valor, and
  Griswold's Edge, Bovine Plate, The Rift Bow, The Butcher's Cleaver, and The
  Needler, The Celestial Bow, Deadly Hunter, Bow of the Dead, The Blackoak Bow,
  and Flamedart, Fleshstinger, Windforce, Eaglehorn, Gonnagal's Dirk, The Defender,
  and Gryphon's Claw, Black Razor, Gibbous Moon, Ice Shank, The Executioner's Blade,
  The Bonesaw, Shadowhawk, Wizardspike, Lightsabre, The Falcon's Talon, Inferno, Doombringer, The Grandfather, The Mangler, Sharp Beak, BloodSlayer, The Celestial Axe, Wicked Axe, Stonecleaver, Aguinara's Hatchet, Hellslayer, Messerschmidt's Reaver, Crackrust, Hammer of Jholm, Civerb's Cudgel, The Celestial Star, Baranar's Star, Gnarled Root, The Cranium Basher, Schaefer's Hammer, Dreamflange, Staff of Shadows, Immolator, Storm Spire, Gleamsong, Thundercall, The Protector, Naj's Puzzler, Mindcry, Rod of Onan, Helm of Spirits, Thinking Cap, OverLord's Helm, Fool's Crest, Gotterdamerung, Royal Circlet, Torn Flesh of Souls, The Gladiator's Bane, The Rainbow Cloak, Leather of Aut, Wisdom's Wrap, Sparking Mail, Scavenger Carapace, Nightscape, Naj's Light Plate, Demonspike Coat, The Deflector, Split Skull Shield, Dragon's Breach, Blackoak Shield, Holy Defender, Stormshield, Bramble, Ring of Regha, The Bleeder, Constricting Ring, Ring of Engagement, Giant's Knuckle, Mercurial Ring, Xorine's Ring, Karik's Ring, Ring of Magma, Ring of the Mystics, Ring of Thunder, Amulet of Warding, Gnat Sting, Flambeau, Armor of Gloom, Blitzen, Thunderclap, Shirotachi, Eater of Souls, Diamondedge, Bone Chain Armor, Demon Plate Armor, Acolyte's Amulet, and Gladiator's Ring
  unique items, and Sorcerer Firebolt staff through Diablo activation and the live Godot protocol
  path, including charged-source validation, charge consumption, and target
  damage; blocked cell generation and remaining Hellfire world and catalog
  tables stay explicit follow-up content migrations.
- Monster type identity is now externalized through validated
  `monster_types.tsv` rows. Base and first-wave Hellfire encounter rows must
  resolve a stable/native type identity before activation. The first
  all one hundred thirty-eight variant-local native Hellfire type identities,
  through native ID 137, are now externalized with native demon, undead, and
  animal class capabilities plus the native knockback ability flag; encounter rows inherit
  validated type resistance, baseline combat, and immutable capability metadata
  unless they provide explicit instance overrides. Variant-local native IDs are
  tracked separately from stable IDs; reverse lookup rejects ambiguous IDs in
  composed overlays. Encounter behavior and the rest of the Hellfire catalog
  remain incremental; native acid resistance is now externalized as a typed
  acid damage channel and persisted through encounters, snapshots, saves,
  hashes, and protocol field 36.
- Those same type rows now carry validated native `MonsterAIID` dispatch
  identities in schema 1.79; identity crosses authoritative targets,
  snapshots, saves, protocol field 43, and canonical hashes while behavior
  dispatch remains server-owned and incremental.
- Usable, identified equipment with native `TripleDemonDamage` now triples
  authoritative physical damage only against externally classified demons;
  the demon capability persists through snapshots, saves, hashes, and protocol
  field 37.
- Usable, identified equipment with native `Knockback` now moves a surviving,
  non-petrified physical target one valid unoccupied cell away from the
  attacker; the existing authoritative position snapshot/hash boundary owns
  the resulting movement.
- Hellfire `ACAgainstDemons` and `ACAgainstUndead` equipment flags now add the
  native 40/20 armor bonuses before authoritative autonomous monster damage;
  unusable and unidentified magical equipment remains excluded.
- Hellfire `Devastation` now performs the native 5% post-hit triple-damage
  roll on authoritative physical player attacks, consuming its RNG draw only
  after a successful hit and excluding unusable or unidentified magical items.
- Hellfire `Jesters` now applies native 0..200 damage scaling after the
  Devastation boundary in exact Q6 arithmetic; fractional damage remains
  authoritative through target damage and life/mana-on-hit effects.
- Hellfire `Peril` now applies its native pre-modifier self-damage, including
  usable equipment `PlusGetHit`, and doubles the final physical hit in Q6;
  Mana Shield, death handling, and emitted damage events remain server-owned.
- Hellfire `Doppelganger` now consumes its native post-hit 10% roll, excludes
  unique and Diablo targets, and spawns a full-health non-unique copy on the
  deterministic last available adjacent cell; the copy is snapshot-owned.
- Hellfire `Decay` now applies after an authoritative attack to eligible hand
  weapons in native order, subtracting five damage-bonus points and removing a
  weapon at -100; hit resolution is kept separate from post-armor damage so
  zero-damage hits still preserve native decay behavior.
- Hellfire `THORNS` is now server-owned: eligible usable equipment retaliates
  against a surviving monster after a successful autonomous hit using the
  native 1-3 Q6 roll, before player death handling and knockback.
- Hellfire `DRAINLIFE` is now server-owned: eligible usable equipment applies
  native 4-Q6 damage once per active non-town tick, including Mana Shield and
  native no-item/no-ear death semantics.
- Native `ABSHALFTRAP` is now server-owned at the armed-object boundary:
  eligible usable equipment halves trap damage before the player damage
  resolver, preserving exact Q6 event state.
- Native `FIREDAM` and `LIGHTDAM` equipment are now server-owned at the
  authoritative attack boundary: eligible usable equipment rolls inclusive
  elemental channels, applies target resistance, and emits exact Q6 damage
  events after a successful physical hit.
- Native class `CriticalStrike` is now authoritative for eligible player
  attacks: the level-based roll is consumed only after a successful hit and
  doubles physical damage before armor and later class/item effects. The shared
  action replay checkpoint was refreshed to this native result.
- Native class `IronSkin` is now authoritative in effective armor: the
  validated class flag adds character level divided by four after dexterity and
  usable equipment armor are applied, across creation, transfers, and restore.
- Native Monk chest-armor bonuses are now content-owned in schema 1.74 and
  applied after dexterity and usable equipment armor: light armor adds two
  levels, ordinary medium armor adds one-half level, unique medium armor adds
  two levels, and unique heavy armor adds one-half level.
- Monster-type aggro radius is now validated in schema 1.75 and carried into
  procedural targets for autonomous server behavior.
- Autonomous monsters retain their direct native-compatible step order and now
  use a bounded deterministic path fallback when blocked geometry would
  otherwise cause equal-distance backtracking or an unrecoverable stall.
- Procedural monster targets can now inherit validated `monsters.tsv` drop
  policies through the item/unique catalogs; generated world-item IDs, seeds,
  values, and unique/Diablo encounter flags are deterministic and server-owned.
- Procedural target conversion now also preserves the validated monster
  Berserk-immunity capability used by authoritative spell targeting.
- Monster-type death-animation durations are now validated in schema 1.76 and
  inherited by static and procedural targets, preserving native Hellfire
  terminal timing while older packs retain the immediate-cleanup default.
- Monster-type experience rewards are now validated in schema 1.77, persisted
  through authoritative snapshots, and applied at the death boundary instead
  of using one global reward for every content-defined monster.
- Monster-type normal and special attack accuracy are now validated in schema
  1.78; Hellfire normal autonomous attacks use the native level, armor, and
  minimum-hit formula, with accuracy persisted through snapshots and protocol.
- Monster native `MonsterAIID` identities are now validated in schema 1.79 from
  the retained native Hellfire `monstdat.tsv` rows and persisted through
  authoritative targets, snapshots, saves, protocol field 43, and canonical
  hashes. AI-specific special behaviors remain incremental and are not inferred
  by clients.
- The native `Fat` AI normal and special melee bands are now server-owned:
  Toad Demon-family targets use the native `15 + 4 * intelligence` normal and
  `20 + 4 * intelligence` total boundaries with externalized accuracy and damage.
- The native `Rhino` AI charge is now server-owned for clear long-range lanes:
  the authoritative monster moves to the nearest clear cell before the player,
  applies its externalized special damage, and preserves charge knockback and
  blocked-lane fallback through snapshots.
- Native monster intelligence is now externalized in schema 1.80 from the
  retained Hellfire `monstdat.tsv` rows and persists through authoritative
  targets, snapshots, saves, protocol field 44, and canonical hashes. Rhino
  charge probability now uses the native `43 + 2 * intelligence` boundary.
- Native monster missile identities are now externalized in schema 1.81 through
  validated `monster_missiles.tsv` mappings. The active content pack supplies
  each native AI's retained missile ID and projectile behavior; GoatRanged and
  SkeletonRanged `Arrow` rows now project as server-owned `MonsterArrow`
  behavior, while other unsupported controllers remain explicitly `Targeted`
  until their server-owned behavior is migrated. Clients never dispatch these
  mappings.
- Validated ranged mappings now admit their native AI directly from the active
  content pack; the server no longer maintains a separate allow-list for which
  native identities may enter the migrated projectile path. Existing
  AI-specific probability and avoidance branches remain server-owned until
  their remaining semantics are externalized.
- Schema 1.82 now carries the mapped ranged-AI mode and avoidance probability;
  the migrated projectile path consumes those fields instead of identifying
  the `AiRangedAvoidance` identities in code. Native behavior remains
  server-owned, while its selection is content-owned and validated.
- Schema 1.83 adds optional monster-specific missile rows. Native Gloom
  (`Bat`, monster type 42) now consumes a validated `Rhino` mapping; the
  server owns its clear-lane, speed-18, movement-only charge and blocked-lane
  stop while the content row selects the controller.
- Schema 1.84 adds validated Counselor rows for native monster IDs 107, 108,
  109, 110, and 113. The server now owns the corresponding Firebolt, Charged
  Bolt, Lightning, and Fireball projectile paths, including monster-owned
  Charged Bolt player-hit resolution.
- Schema 1.85 adds a validated `Mega` `InfernoControl` mapping. The server now owns
  Mega's native distance/intelligence admission, speed-32 controller, three
  persistent segment emissions, and monster damage rolls through the existing
  authoritative fire collision path.
- Schema 1.86 validates the Hellfire Hork Demon native AI identity and routes its
  `43 + 2 * intelligence` Hork Spawn admission through a native-identity summon
  planner.
- Schema 1.87 externalizes the Hork Spawn missile mapping and preserves its native
  speed-8, nine-tick trajectory and expiry-time level-valid summon search.
- Schema 1.88 externalizes additive Hellfire initial quest-state rows through
  `quest_states.tsv`; route-level activation remains server-owned and duplicate
  quest identities are rejected during activation.
- Schema 1.89 externalizes the Hellfire Defiler encounter, its Cathedral Map
  reward, and its quest completion trigger through validated content rows.
- Schema 1.90 externalizes unique-monster identities through
  `unique_monsters.tsv`; the identity is persisted in snapshots/saves and
  projected through additive protocol fields so unique quest triggers remain
  exact rather than matching every base monster of the same type.
- The native `FireBat` `AiRanged` boundary now consumes its validated Firebolt
  mapping and resolves server-owned projectile travel plus native
  monster-to-player magic hit, resistance, block, and Q6 damage rules.
- Native `LazarusSuccubus` minions now use the same validated Blood Star path;
  their retained `LazarusMinionAi` dispatch is represented server-side without
  exposing AI execution to clients.
- The native `Torchant` `AiRanged` boundary now consumes its validated Fireball
  mapping and resolves server-owned travel, impact crawl, and
  monster-to-player explosion damage.
- Native Lich-family flare identities (`Lich`/`OrangeFlare`,
  `ArchLich`/`YellowFlare`, `Psychorb`/`BlueFlare`, and
  `Necromorb`/`RedFlare`) now use the explicit server-owned `MonsterMagic`
  projectile behavior with authoritative travel and monster-to-player magic
  hit resolution; other retained missile identities remain incremental.
- Native `BoneDemon` `AiRangedAvoidance` now consumes its validated
  `BlueFlare2` `MonsterMagic` mapping; the server owns the clear-lane
  500-per-10,000 intelligence admission band and resulting magic hit.
- Native `Storm` `AiRangedAvoidance` now consumes its validated
  `ThinLightningControl` Lightning mapping; the server owns the speed-32
  controller, doubled monster damage, persistent segments, and magic hit.
- Native `Diablo` `DiabloApocalypse` now consumes its validated Apocalypse
  controller mapping; the server emits one physical boom per clear-lane player
  and owns the resulting monster-to-player damage.
- Native `Magma` `MagmaBall` now consumes its validated MonsterMagic mapping;
  the server owns its AiRangedAvoidance admission, speed-16 travel, fire
  resistance/block resolution, and monster-to-player damage.
- Native `Acid`/`AcidUnique` now consume validated Acid mappings; the server
  owns projectile travel, expiry splat, persistent puddle lifetime,
  acid/magic resistance, and per-tick monster-to-player damage.
- Native `GoatRanged` and `SkeletonRanged` now require validated `Arrow`
  identities from `monster_missiles.tsv` before their server-owned clear-lane
  ranged boundaries execute.
- Native player `MultipleArrows` is now represented by authoritative
  `WeaponArrow` projectiles. The server emits the first arrow at the ranged
  attack effect and two spread arrows after the native two-frame delay;
  captured physical Q6 damage, arrow elemental channels, and activation state
  survive protocol snapshots, save/restore, and canonical hashing. Ordinary
  bow attacks now use the same one-arrow travel and impact lifecycle; remaining
  native arrow-item flags remain incremental.
- Native `Snake` now consumes a validated `Rhino` mapping; the server owns its
  clear two-tile charge controller, monster movement, special physical damage,
  blocking, and Q6 event resolution.
- The native `SkeletonRanged` AI clear-lane arrow boundary is now server-owned:
  its `3 + 2 * intelligence` attack band, distance-aware accuracy, damage, and
  blocked-lane fallback resolve through the authoritative directed event path.
- The native `Gargoyle` wounded self-heal boundary is now server-owned: the
  deterministic native heal amount, Healing event, and distance-based special
  lock persist through snapshots, saves, protocol field 45, and canonical
  hashes. Wounded Gargoyles now retreat through a deterministic server-owned
  movement fallback before healing; native animation timing remains incremental.
- The native `GoatRanged` shared `AiRanged` boundary is now server-owned:
  clear-lane arrow damage, close-range avoidance probability, deterministic
  retreat selection, and blocked-lane fallback resolve through the authoritative
  movement and directed-event paths.
- The native `GoatMelee` adjacent `AiAvoidance` boundary is now server-owned:
  its `23 + 2 * intelligence` attack admission and wounded special branch use
  the authoritative physical-hit path and externalized special damage range.
- The native `SkeletonMelee` adjacent attack boundary is now server-owned:
  its `20 + 2 * intelligence` admission is resolved from stable tick/entity
  state before the existing authoritative physical-hit path.
- The native `Zombie` action boundary is now server-owned: its
  `10 + 2 * intelligence` admission and long-range movement threshold are
  resolved before authoritative navigation and adjacent combat.
- Native `ProcessMonsters` regeneration is now server-owned: externalized
  monster levels drive the native one-life or `level / 2` per-tick recovery in
  exact Q6 state before AI execution.
- Native `Succubus` `AiRanged` dispatch is now server-owned: its Blood Star
  projectile uses the retained native missile behavior, magic hit boundary,
  player resistance/block rules, and authoritative Q6 damage.
- Native class melee and ranged to-hit is now authoritative for initialized
  content players: the validated class catalog applies `level + dexterity / 2`
  or `level + dexterity`, respectively, plus each class's external base
  modifier. Legacy hand-built templates with uninitialized dexterity retain the
  global-hit compatibility fallback.
- Initialized physical hit resolution now subtracts target armor and clamps to
  the native 5–95 range. Base Diablo adds `PlusEnemyArmorClass` to to-hit;
  Hellfire applies its native armor shift/quarter rule, including the
  Barbarian melee reduction. Legacy uninitialized templates remain on the
  compatibility path.
- Initialized physical damage now derives its native min/max range from usable
  equipped items and applies class-specific strength/dexterity modifiers.
  Native unarmed, shield, Monk, Rogue, Bard, and Barbarian branches are
  represented in the server-owned profile; legacy uninitialized templates keep
  the global combat-damage fallback.
- Native undead weapon modifiers now apply at the authoritative damage
  boundary: swords deal half damage and maces deal one-and-a-half damage when
  the external monster capability marks the target undead.
- Native animal weapon modifiers now apply at the authoritative damage
  boundary: swords deal one-and-a-half damage and maces deal half damage when
  the external monster capability marks the target animal. Content schema 1.72
  externalizes the capability, which is inherited from validated
  `monster_types.tsv` rows and persists through the
  shared protocol, saves, snapshots, and canonical hashes.
- Native monster `MFLAG_KNOCKBACK` is now externalized through content schema
  1.73 and applied by the authoritative autonomous attack path; a successful
  hit moves the player one valid tile in the monster's facing direction and
  blocked destinations preserve the native position.
- Native `TrapSense` visibility is now recipient-scoped: only a TrapSense
  player receives `TrapArmed` in the network projection, while saves, hashes,
  and internal authoritative snapshots retain the complete armed state; each
  recipient's projected hash matches its visibility boundary.
- External unique and affix catalogs now project every native Hellfire
  damage/armor power identity into the authoritative effect flags,
  so these effects no longer depend on hard-coded content-side interpretation.
- Quest type identity is now externalized through validated `quest_types.tsv`
  rows covering the base and Hellfire native quest IDs. Quest-state rows must
  resolve a declared stable/native identity; quest behavior and remaining
  quest-state data remain incremental.
- Quest randomization pools and native set-level names/identities are now
  externalized through validated `quest_pools.tsv` and `set_level_names.tsv`
  catalogs; activation rejects pool references outside the stable quest catalog,
  and the module applies native deterministic pool exclusions to single-player
  quest state with the historical bad-seed compatibility discard. Excluded
  quests reject progression and linked object interaction and persist through
  snapshots, saves, protocol field 6, and canonical hashes; multiplayer
  activation leaves exclusions disabled. Quest state also preserves the native
  initial/active/done lifecycle and quest-log bit through protocol fields 7-8;
  optional `_qvar1`, `_qvar2`, and `_qmsg` values now flow through protocol
  fields 9-11, saves, snapshots, and compatibility-aware hashes; quest-specific
  behavior now includes an optional validated monster-completion trigger and
  route-level activation of initial quests using explicit session mode;
  linked objects now use validated quest-progress amounts with clamped
  authoritative advancement; Hellfire initial quest-state rows are now
  externalized through `quest_states.tsv` and merged additively with base
  state; the Hellfire Defiler completion trigger and Cathedral Map reward now
  use validated encounter/item rows and its exact unique identity; remaining quest behavior and state
  variables remain incremental.
- Native level-range generation rules are now externalized through validated
  `level_generation.tsv` metadata and exposed by Diablo activation; procedural
  map generation still remains a separate migration.
- Reflect is now a content-selected immediate self effect. The server owns native
  level/character-scaled uint16 charges, consumes one charge per successful
  monster hit, and applies the seeded 20–30% physical return damage; charges
  persist through snapshots, saves, hashes, replay fixtures, and protocol field
  30, while Godot presents the active count and aura.
- Magi is now a content-selected immediate self effect. The server restores the
  caster's current mana to its maximum through the shared authoritative cast
  path, using stable spell ID 40 and native `SpellID` 38.
- Mana is now a content-selected immediate self effect. The server applies the
  native LCG restoration draws, effective-level and class multipliers, Q6
  clamping, and stable spell ID 41/native `SpellID` 37 mapping.
- Ring of Fire is now a content-selected one-tick controller. The server
  preserves native radius-three crawl ordering, solid/object and missile
  line-of-sight filtering, first-blocker termination, shared RNG draw order,
  and Fire Wall segment damage/lifetime behavior through the authoritative
  projectile and protocol state.
- Store stock, purchases, sales, services, equipment, belt, inventory placement,
  item generation, affixes, and unique items for the current shipped slice.
- Native item life/mana bonuses and Hellfire mana-to-life and life-to-mana
  effects apply their Q6 values and independently truncated conversions to newly
  created authoritative player templates and after every accepted equip/unequip
  transfer. The server preserves native resource debt and persists
  base/effective maxima through snapshots, saves, protocol projection, and
  conditional hashes.
- Native usable, identified equipment damage-percent, flat-damage, to-hit, and
  armor-piercing metadata now affects authoritative player attacks with the
  native hit/armor boundary; unequipped, unusable, and unidentified magical
  items remain excluded.
- Native usable, identified equipment `PlusGetHit` metadata now adjusts
  authoritative autonomous monster physical hits after hit/armor resolution,
  preserving the configured minimum damage; snapshot state remains server-owned.
- Native usable, identified equipment life/mana-on-hit flags now apply after
  successful authoritative physical attacks, preserving native random life
  steal, 3/5-percent precedence, Q6 resource units, and `NoMana` suppression;
  life gains use the existing authoritative healing event.
- Native equipped item armor and percentage armor bonuses now recompute the
  authoritative player's effective armor, including native dexterity armor and
  identification/requirement rules, across player creation, transfers, and
  snapshot restoration.
- Native usable, identified equipment strength, magic, dexterity, and vitality
  bonuses now recompute authoritative current attributes across player creation,
  transfers, and snapshot restoration.
- Native usable, identified equipment vitality and magic bonuses now contribute
  to authoritative life and mana maxima through each external class's Q6 item
  factors; flat resource bonuses and Hellfire conversions use the same native
  eligibility boundary.
- Native usable, identified equipment `ZeroResistance` flags now reset
  authoritative fire, lightning, and magic resistance after natural class
  resistance and before the native 0..75 clamp; clients consume the resulting
  server-owned damage outcomes.
- Native usable, identified equipment `NoMana` flags now reject ordinary
  memorized spell casts before source consumption while preserving free class
  abilities and charged or consumable sources; the shared damage resolver also
  retains the native Mana Shield bypass.
- The external class and loadout catalogs define all six native-compatible
  Diablo and Hellfire classes, a content-selected default, starting attributes,
  combat metadata, wallets, equipment, belt and inventory placements, class
  skills, and learned spells. Native and Godot clients can select an
  active-content class for a new session; the server creates independent state
  with native Q6 life/mana formulas and deterministic item seeds, while an
  in-memory or saved participant wins on reconnect. Class and spell identity
  flow through simulation state, protocol snapshots, saves, native projection,
  overlays, and cross-language canonical hashes.
- New sessions may also request a native-valid character name. The server owns
  it thereafter and preserves it through participant state, saves, protocol
  field 28, conditional canonical hashes, reconnect, and native/Godot
  projection. Lethal player damage now creates the native-compatible PvP ear
  from that name, class cursor, character level, and external item definition;
  equipment remains equipped and suppressed multiplayer levels omit the ear.
- Objects, declarative object effects, linked quest progress, and multi-level
  entity persistence.
- Versioned server-owned saves with validation and atomic replacement.
- Native and Godot protocol clients plus shared replay and live-server tests.
- A loopback reconnect soak now keeps a second participant connected while the
  first participant resumes through twelve consecutive cycles, preserving the
  resume token, entity identity, complete participant set, self-first ordering,
  and snapshot hash on every cycle.
- The live Godot protocol client now reconnects through eight v0.2 cycles
  against the launched authoritative server while a second client remains
  connected, preserving the resume token and self-first participant ordering.
- The generic host adapter now has an eight-generation replacement soak that
  migrates two sessions through every candidate and retires each prior target
  only after publication, preserving session state at every boundary.
- The Diablo content boundary now exposes stable-ID monster candidates for a
  requested dungeon level, with validated range filtering and deterministic
  ordering; map layout and spawn placement remain explicitly separate.
- The server-owned monster spawn planner now turns those candidates and
  validated level-density metadata into bounded walkable placements with
  stable seed replay. Activation can optionally replace static encounter rows
  with generated combat targets through the standalone server's procedural
  monster options; static rows remain the compatibility default, and native
  map synthesis remains a separate migration. Successful authoritative level
  transitions materialize each destination encounter set once with collision-
  safe reserved IDs and occupancy-aware placement.
- Diablo content activation now exposes the planner directly to the server
  adapter, preserving the module/data boundary for future generated worlds.
- Engine-neutral inventory-grid geometry, footprint validation, placement, and
  index compaction extracted into the dependency-free `Devilution.Domain`
  assembly with an independent test project.
- Canonical deterministic hashing moved into `Devilution.Domain`; generic TSV
  parsing, ordered content packs, manifest identity, and stable content IDs moved
  into the independent `Devilution.Data` assembly with native golden vectors.
- Diablo item/store/combat/monster/spell/world schemas and deterministic store
  rules plus protocol-independent authoritative player/store state moved into
  `Devilution.Diablo`, with the shipped base pack tested without referencing the
  server or protocol.
- Store, service, inventory-placement, and inventory/belt/equipment transfer
  commands now cross protocol-neutral intents and results; the server maps
  Protobuf commands and rejection codes without owning those state transitions.
- Authoritative world collections, walkability, line of sight, occupancy,
  initial-world validation, movement, portals, pickup, object effects, quest
  transitions, and their transient events now live in `Devilution.Diablo`.
  The server maps Protobuf commands, rejection codes, snapshots, and event
  messages around that module-owned boundary.
- Attack and cast intents, typed damage, experience and drop resolution,
  projectiles, status expiry, monster targeting, movement, attacks, and
  per-player transient event delivery now execute inside `Devilution.Diablo`.
  The host only supplies stable player order and authoritative tick numbers.
- Cast admission now verifies an authoritative class ability, learned spell,
  charged left-hand staff, or exact inventory/belt scroll or rune. Abilities are
  free, memorized spells consume mana, charged items lose one charge, and
  consumables are removed only after all target validation succeeds. Native and
  Godot clients send the selected source; source-zero compatibility commands can
  infer only an actually owned source in a stable order.
- The Godot presentation client enumerates those authoritative sources in a
  stable order, lets the player cycle them with `Q`/`E`, and sends the exact
  selected source and item reference. Selection is presentation state only; the
  server still owns admission, timing, targeting, and resource consumption.
- Content activation cross-validates every directly castable starting learned,
  charged-item, scroll, and rune source against `spells.tsv`. Class-skill IDs are
  kept distinct because repair, recharge, trap disarm, and similar native
  capabilities are cursor-driven interactions rather than ordinary casts.
- The base Sorcerer staff's native spell ID 30 is now an external typed Charged
  Bolt definition. One accepted cast consumes its source once and creates
  `4 + floor(effective spell level / 2)` server-owned bolts using the native
  Diablo LCG draw order, Magic-based damage, 16.16 isometric wandering cadence,
  magic hit roll, and lightning resistance/immunity behavior. Gameplay RNG and
  all path/velocity/phase fields persist through snapshots and saves.
- `Devilution.Engine` now owns transport-neutral module identity, dependency
  ordering, transactional typed capabilities, authoritative clock context, and
  ordered ruleset identity. `DiabloModule` registers its store rules and
  simulation factory, which constructs one aggregate over shared Diablo state.
- Generic Engine session coordination now owns deterministic state ordering,
  reverse participant lookup, explicit participant-state registration, and
  per-session transient event queues. Diablo
  owns stable player/world/store snapshot capture and restore validation; a
  dedicated Diablo adapter maps those projections to Protobuf, state hashes,
  events, and the compatible version-1 save envelope.
- The Engine runtime now serializes participant reads, writes, command steps,
  captures, and autonomous advancement; owns the last-applied tick; and routes
  module-directed events. Diablo supplies normalized player templates and
  lifecycle hooks. The Diablo adapter save codec explicitly dispatches format
  version 1 while retaining the existing JSON/base64/Protobuf representation.
- Data now composes ordered external packs with explicit composite-key upserts,
  exact ordered-column checks, deterministic row placement, and atomic candidate
  activation. The standalone server consumes the effective tables for every
  Diablo catalog, and live protocol tests prove overlays change authoritative
  store state and the content-selected default player class.
- Engine now exposes semantic API versions and deterministic module inspection.
  Duplicate IDs, missing or cyclic dependencies, and incompatible API ranges
  produce actionable diagnostics before registration; the host supplies the
  trusted candidate set explicitly.
- Modules may constrain each named dependency to an inclusive three-component
  semantic-version range. Preflight rejects non-semantic or out-of-range loaded
  versions before registration, verified package wrappers retain the declared
  requirements, and non-empty constraints extend the ruleset identity without
  changing legacy unconstrained hashes.
- Engine now offers all-or-nothing module activation without changing the
  compatibility `Load` API. A generic ruleset runtime composes Data, activates
  a complete module candidate set, computes its identity, and runs a
  game-specific validation callback before atomically publishing any of them.
  Failed content, compatibility, registration, identity, or target validation
  retains the exact previous activation with a stage-specific diagnostic. The
  standalone server uses this coordinated boundary for initial activation.
- Data now accepts versioned content-schema identities and inclusive semantic
  dependency ranges, hashes them when declared, and preserves every legacy
  content golden vector when omitted. The first-party Diablo module rejects a
  declared schema that does not match its contract.
- Host now owns a format-1 external module-package policy: explicit package
  roots, primary-DLL SHA-256 verification, named-type activation, verified code
  identities, and rejection of private managed/native dependencies. A
  process-level test proves the standalone host loads and reports a package;
  Engine remains unaware of files and reflection.
- `IDiabloContentBootstrap` now owns schema compatibility, all normalized and
  legacy table selection, catalog validation, service rules, default-class and
  default-level selection, and simulation construction. Server `Program`
  supplies generic composed content and player-start settings; a boundary test prevents TSV
  names and Diablo catalog constructors from returning to executable startup.
- A generic Server adapter catalog now contributes built-in module candidates
  and selects exactly one compatible combined command/tick/snapshot/event/save
  adapter. Only the built-in Diablo bridge resolves Diablo capabilities or
  constructs the protocol executor; executable `Program` contains no Diablo
  symbols, enforced by tests.
- Managed Protobuf generation now occurs once in `Devilution.Protocol` and is
  shared by Server and Godot. Generic transport, sessions, scheduling, package
  loading, snapshot hashing, and filesystem persistence live in
  `Devilution.Server.Host`, which has no Diablo dependency. Mapping, compatible
  saves, replay helpers, and the protocol executor live in
  `Devilution.Server.Diablo`; the .NET 10 executable contains only `Program.cs`.
  Exact assembly-reference and schema-ownership tests enforce this graph.

## Architectural debt

The process boundary is authoritative and the intended internal C# dependency
direction is now enforced through separate Domain, Data, Engine, Diablo,
Protocol, generic Host, Diablo adapter, and composition assemblies. All
gameplay transitions in the current vertical slice are module-owned and
composed by a registered `DiabloSimulation` factory. Engine owns generic
session/state coordination; Diablo owns transport-neutral state and validation;
the adapter alone owns Diablo/Protobuf mapping and compatible saves; Host owns
generic network and operational behavior. Remaining debt is behavioral and
extensibility work rather than a collapsed project boundary: the adapter still
retains the legacy-named protocol executor and replay helpers, format-1 packages
provide gameplay modules rather than host adapters, live ruleset replacement
  now exposes transactional candidate preparation, a host operation barrier,
  save-based session migration, handshake identity publication, generation
  leases, and explicit retired-target disposal; broader reload fixtures and
  authoritative content/gameplay coverage are still partial. Static content
  portal routes remain protocol-visible without perturbing legacy replay hashes;
  owned, expiring portal state is hash-visible, and the base-content replay
  golden vectors track the current manifest. Cast source ownership and immediate
resource consumption are now authoritative, and every directly castable base
loadout source resolves during activation, but the spell catalog still models
only the current healing, damage, area, status, and projectile slice. Charged
Bolt count, creation RNG order, Magic-based damage, wandering movement, magic
hit chance, Q6-before-resistance damage, fractional monster health, events,
persistence, separate missile geometry, swept tile checks, stationary-occupant
suppression, external object pass-through/breakability/destruction, and shared
C++/C# spawn, resolution, object-collision, and moving-occupancy checkpoints are
now covered. Moving monsters preserve current and future occupancy, normal
walkers collide only at their current tile, petrified walkers freeze and become
hittable at both tiles, and death cancels movement. This state crosses
snapshots, saves, hashes, native signed occupancy, and replay validation.
Firebolt now has a content-selected native behavior rather than the provisional
targeted-projectile path. The server derives class- and variant-adjusted Q6
mana, seeded magic/spell-level damage, 16.16 speed-26 movement, swept collision,
magic hit chance, quarter resistance damage, and impact expiry from schema 0.9
content. Protocol-neutral snapshots, version-1 saves, canonical hashes, parser
validation, and a shared three-checkpoint C++/C# replay preserve its exact
fractional-health and RNG boundary. The shared ranged Hellfire replay now binds
to the composed base-plus-Hellfire manifest and loads its effective overlaid
combat and class tables in the C# executor; the native replay runner passes the
same fixture and checkpoints.
Lightning now uses a content-selected invisible controller and persistent
stationary segments. Schema 0.10 externalizes its speed, controller lifetime,
per-update randomized Q6 damage, positive-resistance divisor, and segment
lifetime scaling. The server preserves same-tick segment processing, repeated
collision, magic-hit and petrification RNG, PvP damage, and the native rule that
a blocking object stops the controller without breaking it. Effective spell
level and all controller/segment state cross protocol field 29, snapshots,
saves, hashes, native projection, and a shared four-checkpoint C++/C# replay.
Flash now uses two content-selected, runtime-owned stationary layers centered on
the player. Schema 0.11 externalizes its 19-tick lifetime, independent
character-level damage rolls, effective-level scaling, post-scale bonus, magic
resistance divisor, and mana tuning. The server preserves the native six-cell
Bottom and three-cell Top ordering, repeated exact-Q6 collision, petrification
and hit RNG, non-expiring wall/object collision, breakable-object destruction,
PvP rules, and the invulnerability window from the first processed layer through
final removal. Layer state and magic resistance cross snapshots, saves, hashes,
protocol projection, strict replay parsing, and a shared four-checkpoint C++/C#
timeline.
Healing now uses a content-selected immediate self effect. Schema 0.12
externalizes the initial, per-character-level, and per-effective-spell-level
random rolls, Q6 scale, and class multiplier. The server preserves native mana,
RNG, and full-life cast consumption, caps only the applied healing, and carries
the exact applied Q6 amount through an additive event field without changing the
legacy native amount. A shared three-checkpoint C++/C# replay proves uncapped,
capped, and already-full casts from one continuous RNG stream.
Teleport now uses a content-selected invisible movement controller. Schema 0.13
activates its exact mana tuning and stationary two-tick lifetime. The server
preserves native radius-0-through-5 `DoCrawl` order, static, monster, object,
and participant occupancy, source-preserving fizzle, and destination
revalidation after the monster phase before moving the player. Controller and
player state cross snapshots, version-1 saves, existing protocol fields,
native/Godot presentation, and a shared four-checkpoint C++/C# replay.
Resurrection now uses a content-selected immediate player-target effect plus a
runtime-owned beam. Schema 0.14 externalizes its fixed-minimum mana, native
range, 16-tick beam lifetime, revived life, life scale, and radius-50 placement
limit. The server preserves native other-player admission, living-target beam
behavior, dead-player action cancellation, zero-mana and ten-life reset,
initial-offset and crawl placement, actor/object/static occupancy, and portal
avoidance during the fallback crawl. Complete participant and beam state cross
snapshots, compatible saves, existing protocol fields, Godot presentation,
native target mapping, and a shared five-checkpoint C++/C# replay. The native
adapter retains all remote participant projections for targeting but does not
yet construct full legacy remote-player presentation objects from them.
Mana Shield now uses a content-selected immediate self effect. Schema 0.19
externalizes its native damage-reduction base divisor, per-learned-level step,
and level cap. The first cast consumes its source; an active recast fizzles
without consumption. One module-owned resolver now applies the native Q6
damage-to-mana conversion and depletion reversal to monster, PvP projectile,
and damaging-object paths, including exact-depletion cleanup and NoMana-item
bypass. Active state crosses snapshots, compatible saves, additive protocol
field 29, conditional hashes, native/Godot presentation, and a shared six-
checkpoint C++/C# replay. Level-changing transitions and Resurrection clear it.
Stone Curse now uses a content-selected monster-target controller. Schema 0.20
externalizes its mana adjustment/floor, native crawl radius, duration base/cap/
scale, shatter lifetime, and per-monster immunity. The server preserves the
native no-target fizzle, consumes without refresh on an already-petrified
target, searches both cells of a walking monster, freezes pending movement,
releases a living monster at expiry, and retains petrification through the
dead-target shatter phase. Controller state, effective spell level, immunity,
and occupancy cross snapshots, compatible saves, additive monster field 22,
conditional hashes, native/Godot presentation, and a shared six-checkpoint
C++/C# replay.
Infravision now uses a content-selected immediate self status. Schema 0.21
externalizes its mana adjustment/floor, status identity, base duration, and
per-level duration-growth divisor. The server validates learned, ability,
scroll, and rune sources; consumes the exact accepted source; applies native
class mana rules only when appropriate; unions recast duration; owns expiry;
and clears the typed effect during level-changing portal and restart transitions
without removing generic statuses. Typed behavior crosses snapshots, compatible
saves, additive status field 4, conditional hashes, native/Godot presentation,
and a shared four-checkpoint C++/C# replay.
Phasing now uses a content-selected invisible movement controller. Schema 0.22
externalizes its mana adjustment/floor and minimum/maximum axis offsets. The
server ignores client target coordinates, enumerates the native 36 candidate
tiles against complete authoritative occupancy, consumes one RNG draw only
when candidates exist, and preserves facing. A successful selection creates a
two-tick controller whose first update revalidates through the shared Teleport
crawl; an empty set creates nothing and advances no RNG but still consumes the
accepted source. Controller state, RNG, resources, and resulting position cross
snapshots, compatible saves, existing protocol fields, conditional hashes,
native/Godot projection, and a shared four-checkpoint C++/C# replay.
Fireball now uses a content-selected native projectile and impact controller.
Schema 0.23 externalizes its mana floor, two-roll damage formula, per-level
damage growth, capped speed growth, fire-resistance divisor, and explosion
lifetime. The server preserves swept direct collision, native shifted 3x3
splash ordering, origin-to-cell solid blocking, object breakage, exact Q6
damage, and the 14-tick stationary explosion. Runtime phase, velocity, origin,
damage, RNG, exact health, and remaining lifetime cross snapshots, compatible
saves, existing protocol fields, native/Godot presentation, conditional hashes,
and a shared four-checkpoint C++/C# replay.
Heal Other now uses a content-selected immediate living-player effect. Schema
0.24 reuses Healing's native randomized Q6 formula and mana rules while
externalizing a separate per-class multiplier, including the Monk's distinct
300-percent result. The server validates the stable target ID, other-player,
living, same-level, and range rules before mutation, then owns source
consumption, RNG, cap, exact healing event, and participant snapshot update. A
shared timed three-checkpoint C++/C# multiplayer replay proves the transition.
Search now uses a content-selected immediate self status. Schema 0.25 makes the
Monk's external spell-46 ability executable and externalizes its base,
character-level, and spell-level duration terms. The server preserves native
additive recasts and the signed-int overflow guard, owns expiry and transition
cleanup, and projects typed behavior 2 to Godot item outlines and the native
automap flag. Compatible saves, conditional hashes, and a shared four-checkpoint
C++/C# replay carry the same state. The live Godot protocol suite now verifies
the Monk class loadout, explicit ability source, and authoritative status
projection with a positive duration.
The Bard's Identify skill is now a server-owned item action. Schema 0.26 binds
the loadout's spell-5 declaration to the stable `spell:identify` identity, and
an additive protocol source distinguishes it from paid vendor identification.
The server validates ability ownership and the exact equipment or inventory
item, charges no Gold for the skill, and remains the only writer of identified
state. Native and Godot clients provide selection and presentation only; the
live Godot protocol suite now verifies the equipment path end to end.
The Warrior's Repair skill is now a server-owned item action. Schema 0.27 binds
spell `26` to stable `spell:repair`; the server validates class-skill ownership
and the exact equipment or inventory item, then preserves native level-scaled
random repair, iterative maximum-durability loss, full-item no-op behavior, item
exhaustion, and RNG state. Source zero remains paid full vendor repair, while
native and Godot clients only select an item and render the returned snapshot.
The live Godot protocol suite now verifies the class-ability equipment path and
the fully-repaired native no-op outcome.
The Sorcerer's Staff Recharge skill is now server-owned under schema 0.28.
Stable `spell:recharge` identity `27` binds the live loadout to the item-service
policy, while `spells.tsv` externalizes native staff levels. The server validates
the exact staff, owns the single recharge-strength RNG roll, permanently reduces
maximum charges, and preserves full-item and exhaustion outcomes. Source zero
remains paid full vendor recharge; clients perform selection and presentation
only. The live Godot protocol suite now verifies the charged-cast to class-recharge
sequence and the resulting charge/max-charge projection.
Restart in Town is now a transport-neutral, server-owned terminal-death
transition. Schema 0.15 adds a validated town level, explicit default-start
level, destination tile, one-whole-point Q6 life reset, and transition duration.
The command is admitted only after the Death action has cleared, preserves
inventory, sets mana to zero, clears the nullable active-store state, applies
the destination immediately, and holds NewLevel for one tick before Stand.
Protocol field 27, action kind 6, both presentation clients, version-1 saves,
conditional hashes, and a six-checkpoint C++/C# dungeon-death-to-town replay
carry the same result. The native adapter invokes only the legacy town-loading
presentation after the accepted server snapshot; it does not resolve the
restart locally.
Ordinary combat-death gold handling is now server-owned under schema 0.16.
Validated content selects the divisor, projected stack cap, native Gold
identity, and search radius. Death cancels movement before radius-1-through-50
crawl placement, retains and drops independently truncated halves, assigns
nonzero seeds from the shared RNG, and exposes the results as ordinary world
items. When canonical Gold piles are present, death now follows the native
two-pass inventory order, reuses seeds for whole piles, creates one seed for a
partial split, compacts the grid, and retains the physical odd remainder behind
the truncated wallet cache. Topology-aware pickup tops off piles and uses native
last-row/column placement before recalculating the wallet. If only part of a
world Gold pile fits, the server removes the world entity, retains the original
seed and remaining value as the player's cursor-held item, and excludes that
remainder from the wallet. The held item can be dropped onto a valid nearby
tile relative to the player's reserved movement destination and is also dropped
before Gold when the player dies. Aggregate-only saves
retain the prior compatible balance path. Focused tests cover multi-pile order,
seed reuse, grid compaction, persistence/recovery, placement, malformed saves,
and the legacy full-grid fallback. Shared C++/C# replays hash both the death
topology and a full-inventory partial pickup followed by an identity-preserving
held-item drop.
Schema 0.17 preserves the native multiplayer exception for level 16 and
arena-equivalent maps. The host owns explicit single-player/multiplayer mode,
projects it in `ServerHello`, and passes it through the generic adapter boundary
to the Diablo simulation. Validated level content marks maps that suppress
multiplayer death consequences. On those maps Gold and RNG state remain
unchanged, while a cursor-held item still follows the native unconditional
death-drop path. A shared C++/C# fixture proves the rule without inferring mode
from connected participant count.
New content-backed participants now start with canonical Gold piles instead of
an aggregate-only wallet. The first pile occupies native cell 30, overflow uses
native Gold placement order, and deterministic session-derived nonzero seeds
remain distinct from starting-item seeds. Store purchases and services spend
nonmaximum piles before maximum piles without changing retained seeds;
canonical purchases receive a native-order inventory footprint. Sales top off
existing piles, allocate seeded piles only when needed, compact the sold-item
footprint, reject transactionally when proceeds do not fit, and preserve the
post-death physical/cache remainder. Direct transfers and swaps reject any
result that would place Gold in equipment or the belt. A five-checkpoint shared
C++/C# store replay proves purchase, sale, mana service, pile value/seed, grid,
wallet, and the one RNG draw used to create sale proceeds after an exact-balance
purchase.
Charged Bolt now also resolves player collision with native friendly/global
admission, hit, equipment/class blocking, lightning resistance, exact Q6
damage, and RNG order. Explicit friendly-mode commands are retry-safe, hostile
state crosses protocol, saves, native/Godot controls, and conditional hashes,
and a two-participant C++/C# replay verifies the collision timeline. Player
combat mode is now server-owned and persisted; only Stand and melee Attack may
block, class block/recovery frames and usable speed flags determine transient
durations, same-tick bolts observe the immediate mode change, and a shared
Hellfire replay verifies the expansion-only post-block RNG draw. The activated
module lifecycle now reserves player walk destinations, defers melee/ranged
damage and spell resource/effect resolution to native action frames, permits
attack/cast chaining only at native boundaries, and distinguishes base-game
from Hellfire bow-speed behavior. Current-action state crosses module snapshots,
protocol field 25, version-1 saves, conditional hashes, and native/Godot
projection. A shared C++/C# command timeline verifies deferred movement, melee
damage, attack-to-cast chaining, spell consumption/effects, and cleanup at seven
checkpoints. Two additional shared timelines carry a fast Bow as hashed initial
equipment and prove nonadjacent ranged admission plus the one-tick Diablo versus
Hellfire effect/ready-boundary difference. Focused module tests retain broader
melee speed-tier coverage. Lethal monster or Charged Bolt damage now cancels an
in-progress action, starts a content-timed Death action, and leaves the player
in terminal Death mode after animation cleanup. Portal use applies the
authoritative destination behind a one-tick NewLevel action barrier before
returning a living player to Stand. Both action kinds cross protocol field 25,
version-1 saves, conditional hashes, and native/Godot validation. Shared C++/C#
timelines prove autonomous monster approach, movement cancellation, the
content-owned death duration, terminal cleanup, one-stack Gold topology,
placement and RNG state, immediate portal destination, and transition cleanup.
Server-owned facing now follows accepted move, attack, and resolved cast
directions and crosses protocol field 27, saves, hashes, native projection, and
Godot validation. On ordinary monster-or-trap death, equipment is removed in
native Head-through-Chest slot order while facing rotates left once per slot;
each item uses its preferred adjacent tile before the shared crawl, preserving
seed and full state without RNG. PvP and special-level multiplayer deaths retain
equipment. A shared two-checkpoint C++/C# replay proves the rotated positions,
occupied-tile fallback, and identified-value world price. Lethal player damage
now retains equipment and creates one native-compatible ear on the shared death
crawl. The victim name deterministically supplies create-info and seed, the
victim level supplies value, the activated class supplies cursor graphic, and
schema-0.18 combat/item content supplies stable item identity. Runtime names
and cursor cross protocol, saves, both clients, and conditional hashes; a shared
C++/C# replay proves death, exact ear state, placement, and unchanged ear-drop
RNG. Remaining spell scaling and other class modifiers, targeting edge cases,
cursor-driven class capabilities, and a complete Godot spell-selection UI
remain migration work.

Content schema 0.8 moves block/recovery timing out of `classes.tsv` and imports
the complete native six-class animation matrix into validated
`class_animations.tsv`. Weapon-specific attack/action pairs and walk, death,
cast, recovery, block, and town frames now share one content-owned source for
the command-lifecycle work instead of remaining native-only presentation data.

Content schema 0.9 adds native spell-mana adjustment, minimum, unit scaling,
and Firebolt damage-scaling columns plus per-class base/Hellfire mana reduction.
The first activated consumer is Firebolt; the same validated fields are now
available as the remaining native spell behaviors are ported.

Content schema 0.10 adds Lightning's character-level random damage, Q6 damage
scale, and secondary-projectile lifetime fields. Validation reserves
`LightningSegment` for runtime creation and rejects incomplete controller
definitions before activation.

Content schema 0.11 adds Flash's repeated-roll count, character-level bonus,
per-spell-level effect scaling, and post-scale percentage fields. Validation
reserves `FlashBottom` and `FlashTop` for runtime creation and rejects incomplete
or non-Magic Flash definitions before activation.

Content schema 0.12 adds Healing's initial, character-level, and spell-level
random ranges and fixed additions plus its Q6 scale, while class rows add the
self-Healing percentage. Validation rejects incomplete Healing behavior and
healing-only tuning attached to any other behavior.

Content schema 0.13 adds the validated Teleport behavior and base spell row.
Teleport accepts only one stationary two-tick controller with no damage,
healing, status, or area effect; destination occupancy remains runtime state
rather than content data.

Content schema 0.14 adds validated Resurrection tuning and the base spell row.
Resurrection accepts no damage, healing, status, or area tuning, requires one
stationary beam with positive lifetime, and validates its life scale and search
radius. `ResurrectionBeam` remains runtime-owned and cannot be selected by a
content row.

Content schema 0.15 adds `default_start` level selection and validated
restart-in-town fields to `combat.tsv`. The base pack now distinguishes town
level 0 from default dungeon level 1 and validates that the configured restart
tile is walkable before activation.

Content schema 0.16 adds validated player-death gold divisor, projected
maximum-stack, native item type/index, and drop-search radius fields to
`combat.tsv`. The module uses the activated values for death, pickup, and save
validation while protocol clients continue to consume generic player and
world-item projections.

Content schema 0.17 adds the validated
`suppress_multiplayer_death_drops` level flag. The server applies it only when
explicit session mode is multiplayer, matching native level-16 and arena
behavior without baking map IDs into engine code.

Content schema 0.18 adds `combat.tsv`'s stable `pvp_ear_item_id`, each class's
validated `pvp_ear_cursor_graphic`, and the normalized native ear row in
`items.tsv`. Activation rejects an ear definition that does not project native
item index 23 and miscellaneous ID 44.

Content schema 0.19 adds the validated Mana Shield behavior and base spell row.
Validation requires an immediate self effect, a safe learned-level divisor, and
exactly one Mana Shield definition; other behaviors cannot carry its tuning.

Content schema 0.20 adds the validated Stone Curse behavior, base spell row,
and `monsters.tsv` immunity flag. Validation constrains its stationary
monster-target controller, duration arithmetic, shatter phase, and crawl radius;
other behaviors cannot carry Stone Curse tuning.

Content schema 0.21 adds the validated Infravision behavior and base spell row.
Validation constrains it to one immediate self status with zero magnitude and
safe duration-growth arithmetic; other behaviors cannot carry Infravision
tuning.

Content schema 0.22 adds the validated Phasing behavior and base spell row.
Validation constrains it to one stationary two-tick controller with bounded,
ordered axis-offset tuning; other behaviors cannot carry Phasing tuning.

Content schema 0.23 adds the validated Fireball behavior and base spell row.
Validation constrains its bounded random-damage work, level-scaled speed,
single fire projectile, radius-one impact, resistance divisor, and persistent
explosion; other behaviors cannot carry Fireball-only tuning.

Content schema 0.24 adds the validated Heal Other behavior, base spell row, and
`heal_other_spell_percent` class column. Validation requires one immediate
living-player effect with native random Q6 scaling and prevents unrelated
behaviors from carrying its healing tuning.

Content schema 0.25 adds the validated Search behavior and base spell row.
Validation requires one zero-magnitude immediate self status with bounded
character- and spell-level duration tuning; unrelated behaviors cannot carry
Search-only columns.

Content schema 0.26 adds the stable `spell:identify` identity required by the
Bard loadout. Activation resolves that symbol into the module's item-service
policy, so external content cannot silently assign the class skill by an
unvalidated host constant.

Content schema 0.27 adds stable `spell:repair` identity `26` for the Warrior
loadout. Activation binds it to the module's native repair policy; the service
uses existing authoritative durability, character-level, and RNG state rather
than client or host constants.

Content schema 0.28 adds stable `spell:recharge` identity `27` for the Sorcerer
loadout and the native `staff_level` spell field. Activation binds the skill to
the authoritative recharge policy, and the content value supplies the exact
character-level divisor used by its shared RNG sequence.

Content schema 0.29 adds the validated Holy Bolt behavior and base spell row,
plus the external `monsters.tsv` `undead` capability. Holy Bolt reproduces the
native level-scaled magic projectile and accepts only explicitly undead
monsters; the capability persists through protocol field 23, compatible saves,
conditional hashes, and future replay state.

Content schema 0.30 adds stable `spell:trap_disarm` identity `28` for the
Rogue's cursor-driven Trap Disarm capability. Armed object state is external,
server-owned, persisted, and cleared only by the authoritative additive
Disarm Trap command; its nondefault state uses a tagged canonical-hash section.
Network projection now exposes that state only to a TrapSense recipient. The
live Godot protocol suite verifies Rogue visibility, non-TrapSense concealment,
an external armed-object overlay, adjacency validation, and server-owned
disarm mutation.

Content schema 0.31 adds stable spell ID `36` for Hellfire Blood Star
(`SpellID` `35`).
Its external behavior owns the fixed magic damage formula, quarter-resistance
resolution, projectile lifetime/speed, and immediate physical self-damage;
server-owned casts retain those effects through the same source and persistence
boundaries as other authoritative spells.

Content schema 0.32 adds stable spell ID `38` for Bone Spirit (`SpellID` `36`). Its external
behavior owns the magic homing projectile, nearest-monster retarget, current-
life-third damage, resistance, seven-tick impact phase, and six-point physical
self-damage while preserving the shared authoritative source and persistence
boundaries.

Content schema 0.33 adds stable spell ID `6` for Fire Wall (`SpellID` `6`). Its external
behavior owns the seven-tick wall controller, perpendicular branch growth with
independent stopping, persistent typed segments, repeated shifted fire damage,
quarter resistance, native duration scaling, and all branch state needed for
deterministic protocol, save, hash, and replay continuation.

Content schema 0.34 adds stable spell ID `15` for Flame Wave (`SpellID` `15`). Its external
behavior owns the one-tick perpendicular wave placement controller and moving
fire segments, including native facing-direction travel, branch count scaling,
random character-level damage, missile blocking, resistance, lifetime, and
deterministic persisted projectile state.

Content schema 0.35 adds stable spell ID `21` for Inferno (`SpellID` `20`). Its external
behavior owns the speed-32 controller, native three-segment cap, stepwise
segment lifetime, character-level and two-value damage draws, repeated fire
collision, and deterministic persisted controller/segment state.

Content schema 0.36 adds stable spell ID `16` for Chain Lightning (`SpellID` `14`). Its external
behavior owns the one-tick native fan-out, crawl-radius target enumeration, and
Lightning-compatible bolt/segment state, including source spell level, damage,
resistance, lifetime, collision, and deterministic RNG ordering.

Content schema 0.39 adds stable spell ID `22` for Hellfire Rage (`SpellID` `22`). Its external
behavior uses typed controller value `34`: the server preserves the native
character-level active and cooldown durations, rejects recasts while either
phase exists, applies the Q6 self-damage floor at cooldown expiry, and carries
the controller phase and remaining ticks through snapshots and protocol. The
live Godot protocol suite now verifies the Barbarian class loadout, explicit
ability source, active controller projection, and server-owned life boundary.

Content schema 0.40 adds stable spell ID `31` for Elemental (`SpellID` `29`). Its external
behavior uses typed projectile value `35`: the server preserves the native
first-destination retarget, 14-tick nine-cell fire burst, fire resistance
divisor, and burst-origin line-of-sight state through snapshots and protocol.

Content schema 0.45 adds stable Reflect spell ID `43` (native `SpellID` `43`).
The server owns native level/character-scaled uint16 charges, seeded 20–30%
physical return damage on monster hits, and persisted protocol/save/hash state.

Content schema 0.46 adds stable Magi spell ID `40` (native `SpellID` `38`).
The server owns its immediate current-mana-to-maximum effect through the shared
validated cast-source path.

Content schema 0.47 adds stable Mana spell ID `41` (native `SpellID` `37`).
The server owns native LCG draws for base, character-level, and spell-level
restoration, including Sorcerer/Rogue/Bard multipliers and Q6 maximum clamping.

Content schema 0.56 adds Etherealize (stable spell ID `53`, native `SpellID`
`26`). The server owns its level-scoped status identity, transition cleanup,
protocol/snapshot/hash persistence, and monster-damage immunity.
Stable player loadouts, saves, and replay state now retain the authoritative
stable identity separately from the legacy native-ID ceiling. The live Godot
protocol suite verifies Etherealize selected through an external loadout,
memorized-source admission, and typed status projection.

Content schema 0.68 adds native acid resistance to `monster_types.tsv` and the
authoritative damage-channel, encounter, snapshot, save, hash, and protocol
paths. The four Hellfire acid creatures retain native 100-percent immunity.
Content schema 0.69 adds the native-derived Hellfire Griswold's Edge base item
and unique definition (`unique_id=1010`) with its one-handed sword identity,
fire-damage, hit, attack-speed, knockback, mana, and life-curse powers.
Content schema 0.70 adds the native-derived Hellfire Bovine Plate base item and
unique definition (`unique_id=1011`) with its heavy-armor identity, armor,
indestructibility, light, resistance, mana-curse, and spell-level powers.
Content schema 0.71 adds the native-derived Hellfire The Rift Bow unique
definition (`unique_id=1012`), reusing the validated Short Bow base item with
native arrow-velocity, damage, and dexterity-curse powers.
Content schema 0.72 adds the native-derived Hellfire The Butcher's Cleaver base
item and unique definition (`unique_id=1013`) with its two-handed axe identity,
strength, fixed-damage, and durability powers.
Content schema 0.73 adds the native-derived Hellfire The Needler unique
definition (`unique_id=1014`), reusing the validated Short Bow base item with
native hit, fixed-damage, and attack-speed powers.
Content schema 0.74 adds the native-derived Hellfire The Celestial Bow base
item and unique definition (`unique_id=1015`) with its long-bow identity,
minimum-strength, damage, and armor powers.
Content schema 0.75 adds the native-derived Hellfire Deadly Hunter base item
and unique definition (`unique_id=1016`) with its composite-bow identity,
triple-damage, hit, and magic-curse powers.
Content schema 0.76 adds the native-derived Hellfire Bow of the Dead unique
definition (`unique_id=1017`), reusing the validated Composite Bow base item
with native hit, attribute, curse, and durability powers.
Content schema 0.77 adds the native-derived Hellfire The Blackoak Bow unique
definition (`unique_id=1018`), reusing the validated Long Bow base item with
native attribute, damage, and light/vitality-curse powers.
Content schema 0.78 adds the native-derived Hellfire Flamedart base item and
unique definition (`unique_id=1019`) with its Hunter's Bow identity, fire-arrow,
hit, and fire-resistance powers.
Content schema 0.79 adds the native-derived Hellfire Fleshstinger unique
definition (`unique_id=1020`), reusing the validated Long Bow base item with
native dexterity, hit, damage, and durability powers.
Content schema 0.80 adds the native-derived Hellfire Windforce base item and
unique definition (`unique_id=1021`) with its War Bow identity, strength,
damage, and knockback powers.
Content schema 0.81 adds the native-derived Hellfire Eaglehorn base item and
unique definition (`unique_id=1022`) with its Battle Bow identity, dexterity,
hit, damage, and indestructibility powers.
Content schema 0.82 adds the native-derived Hellfire Gonnagal's Dirk base item
and unique definition (`unique_id=1023`) with its Dagger identity,
dexterity-curse, damage-modifier, attack-speed, and fire-resistance powers.
Content schema 0.83 adds the native-derived Hellfire The Defender base item and
unique definition (`unique_id=1024`) with its Sabre identity, armor, vitality,
and hit-curse powers.
Content schema 0.84 adds the native-derived Hellfire Gryphon's Claw base item
and unique definition (`unique_id=1025`) with its Falchion identity, damage,
magic-curse, and dexterity-curse powers.
Content schema 0.85 adds the native-derived Hellfire Black Razor unique
definition (`unique_id=1026`), reusing the validated Dagger base item with
native damage, vitality, and durability powers.
Content schema 0.86 adds the native-derived Hellfire Gibbous Moon base item and
unique definition (`unique_id=1027`) with its Broad Sword identity, attribute,
damage, mana, and light-curse powers.
Content schema 0.87 adds the native-derived Hellfire Ice Shank base item and
unique definition (`unique_id=1028`) with its Long Sword identity, fire
resistance, durability, and strength powers.
Content schema 1.68 adds the native-derived Hellfire Demon Plate Armor unique
definition (`unique_id=1109`), reusing the native Full Plate identity with fixed
armor and extra armor against demons.

Content schema 1.69 adds the native-derived Hellfire Acolyte's Amulet unique
definition (`unique_id=1110`), reusing the native Amulet identity and preserving
the player-dependent 50-percent mana-to-life conversion as item metadata.

Content schema 1.70 adds the native-derived Hellfire Gladiator's Ring unique
definition (`unique_id=1111`), reusing the native Ring identity and preserving
the player-dependent 40-percent life-to-mana conversion as item metadata.

Content schema 1.67 adds the native-derived Hellfire Bone Chain Armor unique
definition (`unique_id=1108`), reusing the native Chain Mail identity with
fixed armor and extra armor against undead.

Content schema 1.66 adds the native-derived Hellfire Diamondedge unique
definition (`unique_id=1107`), reusing the native Long Sword identity with
fixed durability, to-hit, damage, resistance, and fixed-armor powers.

Content schema 1.65 adds the native-derived Hellfire Eater of Souls unique
definition (`unique_id=1106`), reusing the native Two-Handed Sword identity
with indestructibility, life, life-steal, mana-steal, and drain-life powers.

Content schema 1.64 adds the native-derived Hellfire Shirotachi unique
definition (`unique_id=1105`), reusing the native Great Sword identity with
one-handed, fastest-attack, target-armor, and lightning-damage powers.

Content schema 1.63 adds the native-derived Hellfire Thunderclap unique
definition (`unique_id=1104`), reusing the native War Hammer identity with
charged-bolt hit damage, strength, resistance, light, and indestructible powers.

Content schema 1.62 adds the native-derived Hellfire Blitzen unique definition
(`unique_id=1103`), reusing the native Composite Bow identity with
lightning-arrow damage, zero base damage, and indestructible powers.

Content schema 1.61 adds the native-derived Hellfire Armor of Gloom unique
definition (`unique_id=1102`), reusing the native Full Plate identity with zero
strength requirement, fixed armor 225, all-resistance reset, and light curse.

Content schema 1.60 adds the native-derived Hellfire Flambeau unique definition
(`unique_id=1101`), reusing the native Composite Bow identity with Fireball-bow
damage, zero base damage, and indestructible powers.

Content schema 1.59 adds the native-derived Hellfire Gnat Sting unique
definition (`unique_id=1100`), reusing the native Hunter's Bow identity with
multi-arrow, damage, fast-attack, and indestructible powers.

Content schema 1.58 adds the native-derived Hellfire Amulet of Warding unique
definition (`unique_id=1099`), using the native AMULET identity with
all-resistance and life-curse powers.

Content schema 1.57 adds the native-derived Hellfire Ring of Thunder unique
definition (`unique_id=1098`), reusing the corrected Ring identity with
lightning resistance and fire/magic-resistance curses.

Content schema 1.56 adds the native-derived Hellfire Ring of the Mystics unique
definition (`unique_id=1097`), reusing the corrected Ring identity with magic
resistance and fire/lightning-resistance curses.

Content schema 1.55 adds the native-derived Hellfire Ring of Magma unique
definition (`unique_id=1096`), reusing the corrected Ring identity with fire
resistance and lightning/magic-resistance curses.

Content schema 1.54 adds the native-derived Hellfire Karik's Ring unique
definition (`unique_id=1095`), reusing the corrected Ring identity with
vitality and magic-curse powers.

Content schema 1.53 adds the native-derived Hellfire Xorine's Ring unique
definition (`unique_id=1094`), reusing the corrected Ring identity with magic
and strength-curse powers.

Content schema 1.52 adds the native-derived Hellfire Mercurial Ring unique
definition (`unique_id=1093`), reusing the corrected Ring identity with
dexterity and strength-curse powers.

Content schema 1.51 adds the native-derived Hellfire Giant's Knuckle unique
definition (`unique_id=1092`), reusing the corrected Ring identity with
strength and dexterity-curse powers.

Content schema 1.50 adds the native-derived Hellfire Ring of Engagement unique
definition (`unique_id=1091`), reusing the corrected Ring identity with
get-hit, thorns, armor, and target-armor powers.

Content schema 1.49 adds the native-derived Hellfire Constricting Ring unique
definition (`unique_id=1090`), reusing the corrected Ring identity with
all-resistance and life-drain powers.

Content schema 1.48 adds the native-derived Hellfire The Bleeder unique
definition (`unique_id=1089`), reusing the corrected Ring identity with magic-
resistance, mana, and life-curse powers.

Content schema 1.47 adds the native-derived Hellfire Ring of Regha unique
definition (`unique_id=1088`), reusing the corrected Ring identity with magic,
magic-resistance, light, strength-curse, and dexterity-curse powers.

Content schema 1.46 adds the native-derived Hellfire Bramble unique definition
(`unique_id=1087`), reusing its Ring identity with cursed attributes, damage
modifier, and mana powers.

Content schema 1.45 adds the native-derived Hellfire Stormshield base item and
unique definition (`unique_id=1086`) with its Gothic Shield identity, armor,
get-hit, strength, indestructible, fast-block, and lightning-resistance powers.

Content schema 1.44 adds the native-derived Hellfire Holy Defender base item
and unique definition (`unique_id=1085`) with its Large Shield identity, armor,
get-hit, fire-resistance, durability, and fast-block powers.

Content schema 1.43 adds the native-derived Hellfire Blackoak Shield base item
and unique definition (`unique_id=1084`) with its Small Shield identity, armor,
dexterity, vitality-curse, light-curse, and durability powers.

Content schema 1.42 adds the native-derived Hellfire Dragon's Breach base item
and unique definition (`unique_id=1083`) with its Kite Shield identity, armor,
fire-resistance, strength, magic-curse, and indestructible powers.

Content schema 1.41 adds the native-derived Hellfire Split Skull Shield unique
definition (`unique_id=1082`), reusing its Buckler identity with armor, life,
strength, light-curse, and durability powers.

Content schema 1.40 adds the native-derived Hellfire The Deflector base item
and unique definition (`unique_id=1081`) with its Buckler identity, armor,
resistance, damage-curse, and to-hit-curse powers.

Content schema 1.39 adds the native-derived Hellfire Demonspike Coat base item
and unique definition (`unique_id=1080`) with its Full Plate Mail identity,
armor, get-hit, strength, indestructible, and fire-resistance powers.

Content schema 1.38 adds the native-derived Hellfire Naj's Light Plate base
item and unique definition (`unique_id=1079`) with its Plate Mail identity,
no-strength, magic, mana, all-resistance, and spell-level powers.

Content schema 1.37 adds the native-derived Hellfire Nightscape base item and
unique definition (`unique_id=1078`) with its Cape identity, fast-recovery,
light-curse, armor, dexterity, and all-resistance powers.

Content schema 1.36 adds the native-derived Hellfire Scavenger Carapace base
item and unique definition (`unique_id=1077`) with its Breast Plate identity,
armor, get-hit, armor-curse, dexterity, and light-resistance powers.

Content schema 1.35 adds the native-derived Hellfire Sparking Mail base item
and unique definition (`unique_id=1076`) with its Chain Mail identity, armor,
and lightning-damage powers.
Content schema 1.34 adds the native-derived Hellfire Wisdom's Wrap base item
and unique definition (`unique_id=1075`) with its Robe identity, magic, mana,
light-resistance, armor, and get-hit powers.
Content schema 1.33 adds the native-derived Hellfire Leather of Aut base item
and unique definition (`unique_id=1074`) with its Leather Armor identity,
armor, strength, magic-curse, dexterity, and indestructible powers.
Content schema 1.32 adds the native-derived Hellfire The Rainbow Cloak base
item and unique definition (`unique_id=1073`) with its Cloak identity, armor,
attribute, resistance, life, and durability powers.
Content schema 1.31 adds the native-derived Hellfire The Gladiator's Bane
base item and unique definition (`unique_id=1072`) with its Studded Leather
Armor identity, armor, get-hit, durability, and cursed-attribute powers.
Content schema 1.30 adds the native-derived Hellfire Torn Flesh of Souls base
item and unique definition (`unique_id=1071`) with its Rags identity, armor,
vitality, get-hit, and indestructible powers.
Content schema 1.29 adds the native-derived Hellfire Royal Circlet base item
and unique definition (`unique_id=1070`) with its Crown identity, attribute,
mana, armor, and light powers.
Content schema 1.28 adds the native-derived Hellfire Gotterdamerung base item
and unique definition (`unique_id=1069`) with its Great Helm identity,
attribute, armor, get-hit, resistance, and light-curse powers.
Content schema 1.27 adds the native-derived Hellfire Fool's Crest unique
definition (`unique_id=1068`), reusing the validated Helm base item with
cursed attributes, life, get-hit, and thorns powers.
Content schema 1.26 adds the native-derived Hellfire OverLord's Helm unique
definition (`unique_id=1067`), reusing the validated Helm base item with
attribute, magic-curse, and durability powers.
Content schema 1.25 adds the native-derived Hellfire Thinking Cap base item
and unique definition (`unique_id=1066`) with its Skull Cap identity, mana,
spell-level, resistance, and durability powers.
Content schema 1.24 adds the native-derived Hellfire Helm of Spirits base item
and unique definition (`unique_id=1065`) with its Helm identity and life-steal
power.
Content schema 1.23 adds the native-derived Hellfire Rod of Onan unique
definition (`unique_id=1064`), reusing the validated War Staff base item with
charged-spell, damage, and attribute powers.
Content schema 1.22 adds the native-derived Hellfire Mindcry base item and
unique definition (`unique_id=1063`) with its Quarter Staff identity, magic,
charged-spell, all-resistance, and spell-level powers.
Content schema 1.21 adds the native-derived Hellfire Naj's Puzzler unique
definition (`unique_id=1062`), reusing the validated Long Staff base item with
magic, dexterity, all-resistance, charged-spell, and life-curse powers.
Content schema 1.20 adds the native-derived Hellfire The Protector unique
definition (`unique_id=1061`), reusing the validated Short Staff base item with
vitality, get-hit, armor, charged-spell, and thorns powers.
Content schema 1.19 adds the native-derived Hellfire Thundercall base item and
unique definition (`unique_id=1060`) with its Composite Staff identity, to-hit,
lightning-damage, charged-spell, lightning-resistance, and light powers.
Content schema 1.18 adds the native-derived Hellfire Gleamsong base item and
unique definition (`unique_id=1059`) with its Short Staff identity, mana,
strength-curse, vitality-curse, and charged-spell powers.
Content schema 1.17 adds the native-derived Hellfire Storm Spire base item and
unique definition (`unique_id=1058`) with its War Staff identity,
lightning-resistance, lightning-damage, strength, and magic-curse powers.
Content schema 1.16 adds the native-derived Hellfire Immolator unique
definition (`unique_id=1057`), reusing the validated Long Staff base item with
fire-resistance, fire-damage, mana, and vitality-curse powers.
Content schema 1.15 adds the native-derived Hellfire Staff of Shadows base
item and unique definition (`unique_id=1056`) with its Long Staff identity,
magic-curse, to-hit, damage, light-curse, and fast-attack powers.
Content schema 1.14 adds the native-derived Hellfire Dreamflange unique
definition (`unique_id=1055`), reusing the validated Mace base item with magic,
mana, magic-resistance, light, and spell-level powers.
Content schema 1.13 adds the native-derived Hellfire Schaefer's Hammer base
item and unique definition (`unique_id=1054`) with its War Hammer identity,
damage-curse, lightning-damage, life, to-hit, lightning-resistance, and light
powers.
Content schema 1.12 adds the native-derived Hellfire The Cranium Basher unique
definition (`unique_id=1053`), reusing the validated Maul base item with
damage-modifier, strength, indestructibility, mana-curse, and all-resistance
powers. It also corrects Hammer of Jholm's normalized native Maul index.
Content schema 1.11 adds the native-derived Hellfire Gnarled Root unique
definition (`unique_id=1052`), reusing the validated Spiked Club base item with
to-hit, damage, dexterity, magic, all-resistance, and armor-curse powers.
Content schema 1.10 adds the native-derived Hellfire Baranar's Star base item
and unique definition (`unique_id=1051`) with its Morning Star identity,
to-hit, damage, attack-speed, vitality, dexterity-curse, and durability powers.
Content schema 1.9 adds the native-derived Hellfire The Celestial Star base
item and unique definition (`unique_id=1050`) with its Flail identity,
no-strength, light, damage-modifier, and armor-curse powers. Content schema 1.8
adds the native-derived Hellfire Civerb's Cudgel unique
definition (`unique_id=1049`), reusing the validated Mace base item with demon
damage, dexterity-curse, and magic-curse powers. Content schema 1.7 adds the
native-derived Hellfire Hammer of Jholm base item
and unique definition (`unique_id=1048`) with its Maul identity, fixed damage,
indestructibility, strength, and to-hit powers. Content schema 1.6 adds the
native-derived Hellfire Crackrust base item and
unique definition (`unique_id=1047`) with its Mace identity, attributes,
indestructibility, all-resistance, damage, and spell-level powers. Content
schema 1.5 adds the native-derived Hellfire Messerschmidt's Reaver base
item and unique definition (`unique_id=1046`) with its Great Axe identity,
damage, damage-modifier, attribute, life-curse, and fire-damage powers. Content
schema 1.4 adds the native-derived Hellfire Hellslayer unique definition
(`unique_id=1045`), reusing the validated Battle Axe base item with strength,
vitality, damage, life, and mana-curse powers. Content schema 1.3 adds the
native-derived Hellfire Aguinara's Hatchet base item
and unique definition (`unique_id=1044`) with its Small Axe identity,
spell-level, magic, and magic-resistance powers. Content schema 1.2 adds the
native-derived Hellfire Stonecleaver unique definition
(`unique_id=1043`), reusing the validated Broad Axe base item with life, to-hit,
damage, and lightning-resistance powers. Content schema 1.1 adds the
native-derived Hellfire Wicked Axe unique definition
(`unique_id=1042`), reusing the validated Large Axe base item with to-hit,
dexterity, vitality-curse, hit-curse, and indestructibility powers. Content
schema 1.0 adds the native-derived Hellfire The Celestial Axe base item
and unique definition (`unique_id=1041`) with its Battle Axe identity, no-strength
requirement, to-hit, life, and strength-curse powers. Content schema 0.99 adds
the native-derived Hellfire BloodSlayer base item and
unique definition (`unique_id=1040`) with its Broad Axe identity, damage, demon
damage, all-attribute-curse, and spell-level-curse powers. Content schema 0.98
adds the native-derived Hellfire Sharp Beak unique
definition (`unique_id=1039`), reusing the validated Large Axe base item with
life, magic-curse, and mana-curse powers. Content schema 0.97 adds the
native-derived Hellfire The Mangler base item and
unique definition (`unique_id=1038`) with its Large Axe identity, damage,
dexterity-curse, magic-curse, and mana-curse powers. Content schema 0.96 adds
the native-derived Hellfire The Grandfather base item
and unique definition (`unique_id=1037`) with its Great Sword identity,
one-handed, attribute, to-hit, damage, and life powers. Content schema 0.95 adds
the native-derived Hellfire Doombringer base item and
unique definition (`unique_id=1036`) with its Bastard Sword identity, to-hit,
damage, all-attribute-curse, life-curse, and light-curse powers. Content schema
0.94 adds the native-derived Hellfire Inferno unique definition
(`unique_id=1035`), reusing the validated Long Sword base item with fire damage,
light, mana, and fire-resistance powers. Content schema 0.93 adds the
native-derived Hellfire The Falcon's Talon base
item and unique definition (`unique_id=1034`) with its Scimitar identity,
fast-attack, to-hit, damage-curse, and dexterity powers. Content schema 0.92
adds the native-derived Hellfire Lightsabre unique
definition (`unique_id=1033`), reusing the validated Sabre base item with
light, lightning-damage, to-hit, and lightning-resistance powers. Content schema
0.91 adds the native-derived Hellfire Wizardspike unique
definition (`unique_id=1032`), reusing the validated Dagger base item with
magic, mana, to-hit, and all-resistance powers. Content schema 0.90 adds the
native-derived Hellfire Shadowhawk base item and
unique definition (`unique_id=1031`) with its Broad Sword identity, light-curse,
life-steal, to-hit, and all-resistance powers. Content schema 0.89 adds the
native-derived Hellfire The Bonesaw base item and
unique definition (`unique_id=1030`) with its Claymore identity, damage-modifier,
strength, magic/dexterity-curse, life, and mana-curse powers. Content schema 0.88
adds the native-derived Hellfire The Executioner's Blade
unique definition (`unique_id=1029`), reusing the validated Falchion base item
with native damage, life-curse, light-curse, and durability powers.
Content schema 0.67 adds native monster fire, lightning, and magic resistance
percentages to `monster_types.tsv`; encounter rows inherit those values unless
they provide an explicit per-instance override. Content schema 0.66 adds
validated quest pools and native set-level names and
restores stable set-level identities in quest type rows. Single-player
activation now applies the deterministic pool exclusions to server-owned quest
state, while multiplayer activation preserves the full pool and skips quest
types marked `single_player_only`. Quest state also
preserves native initial/active/done and quest-log flags plus optional route
activation, `_qvar1`, `_qvar2`, `_qmsg`, monster-completion triggers, and linked
object progress amounts through the same
authoritative persistence boundary. Content schema 0.65
adds validated native level-range generation metadata in
`level_generation.tsv`, including monster density, multiplayer scaling, and
trap percentages. Content schema 0.64 adds validated native-compatible `quest_types.tsv`
definitions for all base and Hellfire native quest IDs; quest-state rows now
reject unknown quest identities. Content schema 0.63 adds validated
native-compatible `monster_types.tsv`
definitions for the base encounter and the first six Hellfire monster types;
encounter rows now reject unknown type identities. Content schema 0.61 adds
native-derived Hellfire Optic Amulet and Ring of Truth
base-item and unique definitions. Their item identities, values, native indexes,
and unique powers cross the same validated overlay and activation boundary as
the earlier Hellfire item slice.

Content schema 0.62 adds native-derived Hellfire Harlequin Crest, Veil of
Steel, and Arkaine's Valor item and unique definitions with their native
indices and authoritative armor, resistance, attribute, life, mana, and
recovery powers.

Content schema 0.60 completes validated `consumable_misc_id` spell mappings for
native rune sources. The base pack maps Rune of Lightning ID `48` to stable
Lightning Wall `50`, Greater Rune of Lightning ID `49` to Nova `18`, Greater
Rune of Fire ID `50` to Immolation `45`, and Rune of Stone ID `51` to Stone
Curse `8`. Rune of Fire ID `47` now uses a direct BigExplosion-style splash,
native level-zero damage, and radius-nine placement crawl. Loadout validation
and combat resolution use the active spell catalog instead of treating native
misc IDs as stable spell identities.

Content schema 0.59 extended the validated `consumable_misc_id` spell mapping
for the compatible Lightning, Nova, and Immolation rune behaviors.

Content schema 0.58 added the validated `consumable_misc_id` spell mapping and
legacy preservation of all five native rune IDs.

Content schema 0.57 externalizes Golem's native 12-frame death-animation
timing on spell row `52`. Remaining terminal ticks cross snapshots, saves,
hashes, and protocol fields 34-35; the server removes the minion after the
animation completes.

Content schema 0.55 adds Golem (stable spell ID `52`, native `SpellID` `21`).
The server validates targeted summon placement, replaces the caster's prior
owned minion, and persists owner identity plus native life, armor, and damage
state through snapshots, saves, hashes, and protocol field 33. Golems now use
server-owned deterministic nearest-monster targeting, movement, melee damage,
experience attribution, and owner/level lifecycle cleanup. Golem path planning
now routes around blocked cells with deterministic breadth-first selection, and
its idle phase follows the owner-facing native random-walk order through the
authoritative RNG. Its native 12-frame death animation is now externalized and
server-owned through terminal cleanup.

Content schema 0.54 adds Nova (stable spell ID `18`, native `SpellID` `18`). The
existing authoritative radial controller and persistent lightning-ball
projectiles are now available through the shipped validated content row.

Content schema 0.53 adds Lightning Wall (stable spell ID `50`, native `SpellID` `40`). It reuses the authoritative wall-growth controller and persistent
segments, with native lightning damage, duration, and resistance tuning in the
external spell row.

Content schema 0.52 adds Warp (stable spell ID `49`, native `SpellID` `42`).
The server selects the nearest normalized level trigger, crawls up to five
cells around its authored destination while excluding trigger and occupied
tiles, and applies the existing authoritative level-transition action.

Content schema 0.51 adds Berserk (stable spell ID `48`, native `SpellID` `44`).
The authoritative module crawls eligible monsters within radius five, applies
the native resistance coin flip, scales all four persisted damage channels in
native RNG order, and preserves berserked state through snapshots, saves,
protocol, and canonical hashes.

Content schema 0.48 adds stable Ring of Fire spell ID `44` (native `SpellID`
`45`). Its typed controller value `42` performs the native radius-three crawl,
stops at the first missile blocker, consumes the native pre-crawl RNG draws,
and emits persistent Fire Wall segments with the shared native damage/lifetime
tuning.

Content schema 0.44 adds stable Town Portal spell ID `39` (native `SpellID`
`7`). The server performs native five-cell placement, replaces the owner's
previous route, exposes a bidirectional dungeon/town portal, expires it after
100 ticks, and persists the route through snapshots, saves, hashes, and the
shared protocol.

Content schema 0.43 adds stable Telekinesis spell ID `33` (native `SpellID`
`33`). The server applies the native cursor operation to a visible item,
object, or monster through the existing authoritative cast and world rules;
remote pickup, object effects, and one-cell knockback are deterministic.

Content schema 0.42 adds explicit stable/native spell identity mapping through
the optional `native_spell_id` column. `spell_id` remains the authoritative
content identity; legacy item spell names resolve from native IDs through the
activated catalog, preserving intentional pairs such as Guardian `7`/`13`, Holy
Bolt `5`/`31`, and Bone Spirit `38`/`36`.

Content schema 0.41 adds stable spell ID `7` for native Guardian (`SpellID`
`13`). Its external
behavior uses typed controller value `36`: the server owns the valid spawn
search, native duration, six-tile scan cadence, Firebolt emission,
line-of-sight filtering, and deterministic RNG state through snapshots and
protocol.

Content schema 0.38 adds stable spell ID `25` for Apocalypse (`SpellID` `24`). Its external
behavior uses typed controller value `32` and boom value `33`: the server scans
the native eight-tile square row-major, skips player minions and blocked cells,
applies Hellfire missile line-of-sight filtering, and persists cursor/first-hit
state through snapshots and protocol. Content schema 0.37 adds stable spell ID
`18` for Nova (`SpellID` `18`). Its external behavior
owns the one-tick radial controller, native nine-point quarter-radius order,
four mirrored emissions, 36 continuing Lightning balls, damage scaling,
resistance, lifetime, collision, and deterministic projectile state.

## Near-term priorities

1. Expand authoritative combat and spell coverage beyond Firebolt, Holy Bolt,
   Blood Star, Bone Spirit, Fire Wall, Flame Wave, Inferno, Chain Lightning, Nova, Apocalypse, Rage, Elemental, Charged Bolt, Fireball, Lightning, Flash, Healing, Heal Other, Stone Curse,
   Infravision, Search, Phasing, Mana Shield, Magi, Mana, Ring of Fire, Teleport, Telekinesis, Town Portal, Resurrection, Jester, Berserk, Warp, Lightning Wall, and Golem,
   including remaining class modifiers, scaling, and targeting rules.
2. Expand external content coverage, including remaining Hellfire monster,
   quest behavior/state, procedural level generation, and item metadata and
   authoritative gameplay definitions.
3. Grow the Godot client from a protocol demonstration into a complete playable
   presentation client using production assets.
4. Add the missing replay, reconnect, multiplayer, and long-running soak cases.
5. Broaden host-controlled live ruleset replacement fixtures and safe-retirement
   soak coverage; generalize adapter selection when a second adapter exists.
6. Replace the remaining Lua capability destinations and remove Lua only after
   every removal gate is satisfied.

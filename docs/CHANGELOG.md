# Godevilution Changelog

All notable changes to this fork are documented below.

- Added the generic host-adapter package boundary. Repeatable
  `--game-adapter-package` roots now load hash-verified `game-host-adapter.json`
  packages, reject traversal/reparse-point paths and undeclared private
  references, validate public factory types, and merge them with the built-in
  catalog under strict duplicate-ID and compatibility selection rules. Package
  manifests may now carry a bounded, hash-verified private dependency closure,
  allowing production adapters to ship game-specific assemblies without
  duplicating shared host contracts.

- Added the first production package entry point in
  `Devilution.Server.Diablo.Package`. Explicit factory selection now scopes
  built-in module candidates to the selected adapter, allowing the packaged
  Diablo adapter to run without colliding with the built-in factory; focused
  coverage loads its private Diablo dependency closure in isolation. The
  adapter loader now shares `Google.Protobuf` with the host so isolated package
  code cannot create incompatible generated-message type identities.

- Added live Godot protocol coverage for the selected packaged Diablo adapter:
  the server starts through the package boundary, the client receives its
  initial authoritative snapshot, reconnects, and verifies resume-token and
  entity continuity. A companion process-restart fixture reuses the package
  and save root and verifies persisted entity and class identity.

- Added optional `sharedContracts` SHA-256 declarations to adapter manifests.
  The host validates declared shared Data, Engine, Host, Protocol, and
  Protobuf identities before loading a package, giving release deployments an
  early, fail-closed ABI mismatch check while preserving older manifests.

- Added `tools/package-diablo-adapter.ps1` to build and stage the production
  Diablo adapter entry assembly, private dependency closure, and verified
  manifest without overwriting a non-empty output directory.

- Extended packaged-adapter recovery coverage to roll the same adapter ID from
  version 0.1.0 to 0.2.0 across a server restart while retaining its durable
  session state.

- Added the staged-package release gate and rollback coverage. The verifier
  checks canonical paths and every declared entry/private/shared-contract hash;
  the loader suite now proves that a candidate with a changed private closure
  fails closed while the previous package remains loadable.

- Advanced combined authoritative session checkpoints to format 3 with
  canonical SHA-256 hashes of serialized server state and the command ledger.
  Version-1 and version-2 checkpoints remain readable for migration and are
  upgraded when the next synchronized checkpoint is written; tampered or
  incomplete current-format state or receipts now fail closed.

- Corrected the migration-status and architecture references for procedural
  level geometry: native-vision blockers and multiple authored required-walkable
  entry cells are now called out explicitly, and ruleset replacement pressure
  coverage is recorded as an existing boundary with broader multi-session soaks
  retained as future work. The near-term priority list now matches the current
  test coverage and second-adapter roadmap.

- Advanced the active content schema to `1.462.0`, retaining `1.461.0` as
  the previous additive contract. Procedural level rows can now author
  multiple required-walkable `x,y` cells, with strict duplicate/bounds
  validation and collision/vision clearing across generated geometry layers.
  Refreshed the checked-in replay fixture manifest hashes without changing
  their gameplay checkpoints.

- Added combat regression coverage for compatibility-template fallback armor
  damage and block/hit recovery RNG boundaries, including empty-equipment
  recovery paths.

- Revalidated timed authoritative player movement at its effect tick against
  current level geometry and participant occupancy, preventing delayed movement
  from entering a cell claimed by another participant or a world mutation.
  Snapshot restore now preserves in-flight moves whose destination changed while
  the action was pending, allowing the effect-time guard to cancel them safely.
  Protocol and Godot client-model validation now retain those pending actions
  when their destination is in bounds but currently blocked, leaving the final
  geometry decision to the authoritative effect tick; the native compatibility
  validator now follows the same boundary.

- Kept the native monster-pool count behind an exported adapter function so
  shared-library compatibility tests can link the same pool boundary they
  exercise in the client.

- Aligned compatibility-template combat and replay RNG boundaries with the
  native runner: metadata-free monster armor keeps its legacy damage reduction,
  petrified Bone Spirit targets skip hit rolls, and block/hit recovery skips
  durability rolls when no eligible equipment exists. Movement, Bone Spirit,
  and Fireball replay checkpoints now pass in both runners without fixture
  hash changes.

- Aligned managed PvP physical attacks with native `PlrHitPlr` and
  `Plr2PlrMHit`: player targets no longer inherit monster armor-piercing or
  post-armor damage rules, ranged PvP admission applies the native distance
  penalty, non-Rogue class damage modifiers are halved for arrows, and
  initialized monster-target arrows apply the native distance penalty.

- Aligned player attacks with native Gargoyle wake-up behavior: an active
  special flag is consumed by the waking hit before damage or equipment
  effects are applied. Other petrified monsters use guaranteed-hit physical
  admission while Stone Curse remains active, without enabling knockback.

- Added native class-driven Cleave resolution for Monk staffs, Bard dual
  swords, and Barbarian axe/two-handed melee, including adjacent hit penalty
  and quarter damage. Cell-targeted melee attacks now resolve both native
  Cleave lanes even when the primary cell is empty or contains a breakable
  object, preserving one shared hit/durability boundary.

- Extended Cleave parity to apply Hellfire fire and lightning weapon channels
  to each admitted side target, including empty-cell and breakable-object cell
  attacks, with native damage ordering and resistance handling.

- Completed native PvP Cleave ordering: a melee attack whose primary target is
  another player still evaluates both monster side lanes, including when the
  primary player blocks, and consumes one shared durability boundary.

- Corrected Hellfire Peril ordering so attacker self-damage includes the
  native post-percentage flat weapon modifier while remaining before the class
  damage modifier and final target-damage multiplier.

- Aligned Gargoyle wake-up admission with native melee timing: the waking hit
  suppresses damage and equipment effects but still participates in the shared
  successful-hit durability boundary.

- Preserved native Gargoyle wake-up timing for bow attacks: the special flag is
  consumed at arrow impact rather than launch, suppressing physical, elemental,
  Peril, knockback, and other attack effects on the waking arrow.

- Aligned petrified-monster admission with native `PlrHitMonst` and
  `MonsterMHit`: player physical and projectile attacks now use guaranteed-hit
  admission while Stone Curse remains active, without enabling knockback movement.

- Completed the native `DamageWeapon` parity slice: successful melee attacks
  now use the 1-in-30 durability roll, each emitted ranged arrow uses the
  1-in-40 roll, hand/shield fallback ordering matches native behavior,
  indestructible items are preserved, and broken equipment is removed with
  authoritative resource recalculation. Hellfire `Decay` removal retains its
  native early return before ordinary durability loss. Compatibility weapons
  without durability metadata no longer consume a spurious replay RNG roll.

- Refreshed six class-physical replay checkpoints whose native and managed
  runners now agree after the armor-admission and damage-boundary corrections.

- Refreshed the two Hellfire ranged Peril profile checkpoints and assertions
  after both runners incorporated the native class damage modifier before the
  final Peril multiplier.

- Refreshed the remaining ranged-class replay profiles for Hell difficulty,
  target armor, shifted Hellfire armor, and Multiple Arrows so native and
  managed projectile damage, hit seeds, and snapshot hashes agree.

- Added native incoming durability handling at timed recovery boundaries:
  block expiry applies the 1-in-10 shield/staff parry roll, hit recovery
  applies the 3-in-4 helmet/chest armor roll, and broken equipment is removed
  with authoritative resource recalculation.

- Aligned melee cell attacks with native `PlrHitObj`: adjacent authored
  breakable objects are destroyed authoritatively, and successful object hits
  consume the same native weapon-durability boundary as monster hits.

- Cell-targeted attacks now update authoritative player facing before either
  immediate resolution or timed animation admission.

- Advanced the active content schema to `1.461.0`, retaining `1.460.0` as
  the previous additive contract for compatible packs.

- Refreshed the canonical Hellfire replay fixtures to the `1.461.0` composed
  manifest identity; replay checkpoints and gameplay vectors remain unchanged.

- Hardened procedural dungeon entry points: authored start cells are passed
  into deterministic generation as required-walkable inputs, clearing both
  movement and missile blockers while preserving seeded layout determinism.

- Added schema 1.461 support for an optional procedural native-vision density.
  Generated movement walls remain vision blockers, while separately seeded
  BlockLight-style cells can now be projected to server and Godot clients
  without changing movement.

- Hardened the shared geometry contract: snapshots with an explicit native
  vision layer are rejected unless every movement-blocked cell is also a
  vision blocker, keeping server and Godot validation aligned.

- Preserved that movement/vision invariant when authoritative world mutations
  add collision cells to levels that already carry an explicit vision layer.

- Completed the remaining compact native replay checkpoint alignment slice:
  store fixtures now include validated cursor graphics, zero-seed Rhino charges
  use their deterministic compatibility damage stream, Reflect retaliation no
  longer consumes an extra native draw, and the equipped-death checkpoint now
  records the canonical managed/native state.

- Aligned compact native replay with managed monster activation visibility:
  replay now refreshes tracked visible targets and decrements their activation window when
  players die or leave range. The restart-town fixture now declares its active
  window, uses the canonical primary entity identity, and shares refreshed
  checkpoints across both runners.

- Aligned the compact native replay harness with the managed native melee RNG
  draw, refreshed the Hellfire fixtures to the composed manifest hash, and
  updated the death and Mana Shield checkpoints to the resulting stream.

- Routed the base store replay through the validated item-cursor catalog and
  refreshed its purchase/sale checkpoints for complete native item projection.

## Unreleased

- Refreshed all checked-in base replay fixtures to the canonical content-pack
  manifest hash produced by the normalized source identities.

- Added an explicit native-vision blocker override so synthetic and imported
  geometry can keep movement collision separate from line-of-sight data while
  preserving the legacy movement-blocker fallback when omitted.

- Corrected the multiplayer Mega Inferno RNG expectation so the adjacent
  controller test only accounts for rolls consumed on its actual tick.

- Corrected the charged-bolt save/restore expectation to retain the monster's
  post-impact Q6 hit points instead of resetting to full health.

- Corrected the critical-strike combat regression test to reflect native RNG
  consumption: Warrior/Barbarian critical paths roll, while Rogue attacks do
  not consume a critical-strike draw.

- Scoped the compact Snake-charge replay exception to an explicit validated
  missile-pack flag. Ordinary Snake charges now damage players regardless of
  their entity ID, while the replay fixture retains its native checkpoint
  boundary.

- Updated shipped catalog test fixtures to pass canonical pack-relative table
  identities while continuing to read their contents from the repository,
  matching the validated external-content boundary.

- Aligned Godot combat, Telekinesis, and unique-monster dialogue target
  selection with the projected native vision boundary used for monster
  rendering. Hidden, dead, sneak-hidden, and out-of-radius monsters now fail
  closed at the interaction boundary.

- Applied the projected native vision boundary to native position-based
  monster interaction lookups and exposed a reusable C++ point-visibility
  helper backed by the existing native ray table. Snapshots without vision
  geometry retain the legacy compatibility path.

- Applied the viewer-aware projected vision query to native monster
  presentation slot claiming, removing blocked monsters from the compatibility
  pool instead of leaving render/interact ghosts behind.

- Corrected authoritative monster activation to honor additive native vision
  blockers even when no transparency layer is present, preserving server/client
  parity for metadata-only geometry snapshots.

- Projected the authoritative native DUN monster layer through the shared
  protocol, including server, native, and Godot validation and wire coverage.

- Preserved additive native vision-blocked cells in the C++ authoritative
  projection and world-query bridge, with fallback to movement blockers for
  older snapshots.

- Fixed Godot monster visibility to honor native vision blockers on geometry
  snapshots that omit transparency data.

- Added `replace_collision_geometry` to `native_dun_fragments.tsv`. Validated
  fragment collision bundles can now replace, rather than merely add to, the
  base movement/missile geometry within their footprint. This advances the
  active content schema to `1.460.0` while retaining `1.459.0` as the previous
  additive contract.

- Added `native_layer_asset_path` to `world_mutations.tsv`. Mutations can now
  project a validated DUN mega-tile, transparency, object, and monster layer
  atomically, with optional `.til`/`.sol` collision assets completing the same
  map replacement. This advances the active content schema to `1.459.0` while
  retaining `1.458.0` as the previous additive contract.

- Added validated `tile_asset_path`/`sol_asset_path` pairs to
  `world_mutations.tsv`. Together with `mega_tile_asset_path`, a mutation now
  materializes native `.til`/`.sol` d-piece geometry and replaces movement and
  missile collision across the authored map footprint. This advances the
  active content schema to `1.458.0` while retaining `1.457.0` as the previous
  additive contract.

- Added `mega_tile_asset_path` to `world_mutations.tsv`. Mutations can now
  expand a complete validated native `.dun` mega-tile layer at load time,
  keeping post-activation map presentation in external licensed assets. The
  Gateway final-stage mutation now anchors `blood2.dun` alongside its mapped
  monster layer. This advances the active content schema to `1.457.0` while
  retaining `1.456.0` as the previous additive contract.

- Added the server-owned Gateway pedestal progression: each staged interaction
  consumes a Blood Stone, the final stage activates the validated blood2.dun
  monster layer, and Arkaine's Valor is emitted as an authoritative reward.
  Added `object_item_gates.tsv` and `monster_layer_rects` to schema `1.454.0`.

- Added validated set-level destinations to `warps.tsv`. Native pre-map and
  active-map variants now resolve through the same authoritative level
  transition path once their geometry is materialized; ordinary warp rows remain
  compatible. This advances the active content schema to `1.455.0`.

- Extended validated set-level destinations to static `portals.tsv` routes.
  Materialized pre-map and active-map variants now use the same authoritative
  portal occupancy and level-transition path. This advances the active content
  schema to `1.456.0`.

- Corrected the Gateway fragment to use the native pre-pedestal blood1.dun
  layer while retaining the validated blood2.dun post-pedestal asset for the
  server-owned final-stage monster-layer activation.

- Externalized the Halls of the Blind and Gateway of Blood rooms as
  availability-gated native-DUN fragments with explicit Cathedral/Catacombs
  anchors and server-owned book-template placements. This advances the active
  content schema to `1.453.0`; their licensed post-activation map mutations
  remain deferred.

- Externalized the Hellfire Warlord room as a validated native-DUN fragment
  with an explicit Hell anchor for the native level-13 encounter and an
  availability gate. This advances the active content schema to `1.452.0`.
  Its raw knight layer
  and steel-tome object placement now flow through the server-owned content
  boundary; native random-chamber selection and licensed collision replacement
  remain deferred.

- Snapshot restore rollback now captures the live player's action tick when an
  older incoming snapshot is rejected, preserving the original validation
  error instead of masking it with a rollback `AggregateException`.

- Added optional stable `monster_symbol` cross-checks to
  `native_dun_monsters.tsv` and populated both shipped packs. Native DUN layer
  mappings now validate their authoritative monster identity against the active
  stable namespace while retaining numeric-only compatibility rows.

- Externalized the native Halls of the Blind optic-amulet and Gateway of Blood
  Blood Stone book rewards through validated object templates. Blood Stone is
  now present in the base item catalog; placement and map replacement remain
  gated on authored native-DUN geometry.

- Expanded the validated object-reward offset envelope to +/-32 so native
  Gateway-of-Blood placement at (9,17) remains valid through domain and
  protocol projection.

- Corrected set-level-anchored mega-tile mutations to translate authored
  assignments from the published origin, preserving the future set-piece map
  replacement contract.

- Added schema `1.450.0`'s optional `procedural_quest_piece_origins.tsv` table.
  It publishes deterministic ordinary-level quest-piece origins independently
  of fixed native set-level IDs and persists them through saves and snapshots.

- Extended `world_mutations.tsv` with mutually exclusive procedural quest-origin
  anchors, allowing ordinary-level geometry overlays to consume published
  quest-piece origins without reusing fixed native set-level identities.

- Extended native DUN fragments and fragment-object placements with the same
  procedural quest-origin anchors, including deferred publication and template
  materialization after generated level geometry becomes available.

- Kept the new shared native-geometry catalog additive at the end of the
  `DiabloContentActivation` constructor so existing positional callers remain
  source-compatible while activation exposes the validated catalog.

- Added the optional validated `native_dun_piece_definitions.tsv` and
  `native_dun_tile_properties.tsv` tables. Ordinary native DUN levels, deferred
  fragments, and set-level maps can now materialize shared d-piece collision
  geometry without shipping licensed `.til`/`.sol` bytes; binary pairs remain
  authoritative when declared, and incomplete table pairs fail activation.

- Deferred native-DUN fragments now retain the shared external geometry catalog
  through simulation construction, later set-level-origin publication, and
  snapshot restore instead of falling back to raw layers after bootstrap.

- Diablo content activation now retains the validated shared native geometry
  catalog alongside the other content-owned catalogs, keeping the external
  collision contract visible to server hosts after bootstrap.

- Godot now preserves authoritative native DUN object-layer values in its level
  layout and renders deterministic provenance outlines. These outlines are
  presentation-only; object commands continue to require server-owned entity
  projections.

- Godot now renders a deterministic diagnostic overlay for complete
  authoritative native d-piece and transparency layers, while retaining the
  server-owned collision projection and floor fallback until licensed native
  tile art is available.

- Added a viewport transform shared by the Godot world presenter and input
  hit-testing. The authoritative tile world now centers small maps, clamps
  large maps around the local player, and adapts to the actual viewport instead
  of relying on a fixed 1280×720 origin.

- Added optional content-owned `start_x`/`start_y` entry positions to ordinary
  and procedural levels. The authoritative host now gives new participants the
  validated position for their selected level, while omitted columns preserve
  the legacy `(0, 0)` default.

- Corrected compact native replay routing so player-owned Firebolt missiles stay
  in the shared magic-projectile resolver instead of entering the autonomous
  monster-magic lane; Firebolt resolution, PvP, Hellfire block, and Guardian
  admission now match the managed checkpoints and RNG stream.
- Replay fixture loaders now reject checkpoint labels longer than 64
  characters in both managed and compact native runners, preventing malformed
  hashes from being silently treated as symbolic checkpoints.
- Corrected the first two Acid puddle replay checkpoints, which had been stored
  as malformed 65-character hashes; the managed and compact native runners now
  validate the same canonical tick-1 and tick-2 state hashes.
- Added primary MonsterMagic impact coverage for usable equipment GetHit and
  Mana Shield absorption across the managed and compact native runners.
- Added primary MonsterMagic resistance coverage proving usable magic
  resistance is applied before Mana Shield absorption in both runners.
- Added primary MonsterMagic natural-resistance coverage proving the validated
  Barbarian class contribution is applied before Mana Shield in both runners.
- Added primary MonsterMagic block coverage proving usable shields consume the
  block boundary before damage in both runners.
- Added Hellfire Monk MonsterMagic block coverage proving FlexibleBlock admits
  unarmed primary-player blocking and applies the class base block chance in
  both runners.
- Added Hellfire Monk MonsterMagic coverage for a usable one-handed weapon,
  extending the replay envelope with native equip-location classification so
  both runners admit one-handed FlexibleBlock without admitting two-handed
  weapons.
- Added the inverse Hellfire Monk MonsterMagic fixture, proving a usable
  two-handed weapon receives the normal hit boundary and does not enter the
  FlexibleBlock animation in either runner.
- Aligned compact native replay block chances with every validated class
  profile (Warrior, Rogue, Sorcerer, Monk, Bard, and Barbarian), with a
  low-Dexterity Warrior fixture proving the non-Monk base chance path.
- Corrected managed MonsterMagic replay setup to retain authored mana maxima,
  Mana Shield state, memorized spells, spell content, and equipment
  `equip_location`; the in-process xUnit runner now verifies the full
  MonsterMagic fixture family against native checkpoints.
- Extended Hellfire Monk MonsterMagic FlexibleBlock coverage to an unarmed
  secondary participant in multiplayer, including class base block chance.
- Added Hellfire Monk Hork Spawn coverage proving the shared class-aware block
  boundary applies to the primary player without equipment.
- Hork Spawn replay setup now loads the validated player-class catalog and
  initial Dexterity, so managed class-aware block checks use fixture state.
- Extended secondary-player FlexibleBlock/base-chance handling to Bone Spirit,
  Charged Bolt, and Fireball replay impacts, with Hellfire Monk Bone Spirit
  coverage preserving life and variant RNG parity.
- Completed the class-aware Monk replay lanes by preserving authored level,
  Hellfire rules, and native base/current attribute semantics in the managed
  Bone Spirit and Hork Spawn harnesses; added missing schema metadata to the
  remaining secondary-Monk fixtures.
- Replay helper factories now construct their combat rules and difficulty from
  fixture metadata, keeping the autonomous MonsterMagic, MonsterArrow, Blood
  Star, Inferno, Hork Spawn, Snake, stealth, and melee lanes on the authored
  Diablo/Hellfire and Normal/Nightmare/Hell rulesets.
- Added Hellfire Monk secondary-participant MonsterMelee coverage, applying the
  same class-aware block admission and preserving the native AI seed stream.
- Added primary Acid impact coverage proving usable GetHit and acid resistance
  are applied before Mana Shield absorption in both runners.
- Added persistent Acid puddle impact coverage proving active-tick player
  collision applies usable GetHit and acid resistance before Mana Shield in
  both runners.
- Added persistent Acid puddle object-collision coverage proving breakable
  missile blockers are destroyed without shortening the puddle impact phase.
- Added multiplayer Acid puddle coverage proving secondary-player usable
  GetHit and Acid resistance are applied on persistent active ticks.
- Compact native MonsterMagic impacts now apply the validated primary-player
  GetHit/Mana Shield boundary and secondary-player GetHit adjustment.
- Added primary SkeletonMelee replay coverage for reflection and Mana Shield
  ordering, with shared C# and native checkpoints.
- Compact native replay now applies reflection and Mana Shield to the
  post-GetHit damage boundary and preserves multiplayer RNG consumption.
- Added Rhino charge boundary coverage proving the dedicated impact path does
  not apply generic Thorns retaliation while preserving damage and movement.
- Restricted compact native Thorns application to Skeleton-family special melee
  attacks, matching the managed Rhino charge path.
- Added Rhino charge replay coverage for primary-player usable-equipment
  `PlusGetHit`, preserving charge movement and the shared damage checkpoint.
- Compact native replay now applies the validated incoming GetHit adjustment in
  the Rhino special-charge damage branch.
- Added SkeletonMelee special-attack replay coverage for combined usable
  equipment GetHit and Thorns effects on a secondary participant.
- Compact native replay now applies incoming equipment effects in the special
  SkeletonMelee/Snotspill/Warlord attack branch with matching RNG consumption.
- Added initialized GoatMelee replay coverage for usable-equipment Thorns
  retaliation, including target HP reduction and post-hit RNG checkpoint parity.
- Compact native replay now applies validated Thorns retaliation for primary and
  secondary monster-hit victims before the player death boundary.
- Added initialized GoatMelee replay coverage for authored usable-equipment
  `PlusGetHit`, including a shared checkpoint with the secondary player at 95 life.
- Compact native replay now applies validated usable-equipment `PlusGetHit` to
  generic primary and secondary monster-hit damage before reflection and mana shield.
- Added initialized physical replay coverage for usable equipment Knockback,
  including non-lethal target relocation and shared canonical checkpoints.
- Compact native replay now applies validated equipment Knockback after melee
  and ranged physical hits, respecting petrification, bounds, and occupancy.
- Added a ranged-impact replay fixture for native StealLife and StealMana
  restoration, including life/mana caps and shared post-impact checkpoints.
- Compact native replay now applies validated usable-equipment resource effects
  after melee and ranged physical hits, matching the authoritative C# rules.
- Added initialized Monk, Bard, and Barbarian ranged class-profile fixtures,
  including cross-runner damage and RNG checkpoint coverage.
- Corrected the managed initialized-class gate to require positive Strength and
  Dexterity, matching native fallback behavior for zero-authored attributes.
- Corrected Monk replay classification for occupied unclassified hand slots and
  aligned the compact native unarmed range with externalized level divisors.
- Added a composed Hellfire ranged-class replay fixture covering shifted armor
  admission and unchanged post-hit target armor reduction in both runners.
- Added initialized Warrior ranged replay coverage for Critical Strike and
  Triple Demon Damage ordering across managed and native runners.
- Added ranged elemental-arrow impact coverage, including fire resistance and
  projectile removal at the authoritative collision tick.
- Corrected native ranged miss snapshots so failed admissions do not carry
  elemental channels or consume an elemental damage roll.
- Added initialized Multiple Arrows coverage for per-arrow hit seeds and
  deterministic damage-range RNG ordering.
- Corrected Hellfire ranged misses so post-hit effect rolls are skipped when
  no damage is admitted, preserving native RNG state.
- Corrected initialized class Peril payloads so self-damage uses pre-armor
  attack damage while projectile damage remains post-armor.
- Added unique- and Diablo-target Hellfire ranged fixtures so both replay
  runners skip ineligible Doppelganger rolls without consuming RNG.
- Extended Hellfire Peril replay fixtures to carry authored `PlusGetHit`
  self-damage for both melee and ranged class profiles.
- Added initialized Hell ranged replay coverage for difficulty armor during
  hit admission and projectile damage resolution.
- Player-minion owner references now resolve to a present player on the same
  level across mapper, host, Godot, and native snapshot boundaries.
- Projectile destination coordinates now use the projected level geometry at
  mapper, host, Godot, and native snapshot boundaries.
- Cell-targeted player attacks now preserve their explicit target-cell contract
  and projected geometry across domain, mapper, host, Godot, and native action
  validation.
- Cast-action target coordinates now remain within native and projected level
  geometry for tile-targeted casts across domain, mapper, host, Godot, and native
  snapshot validation.
- Cast source references now enforce native inventory, belt, equipment, and stash
  slot/page bounds and require item-backed source kinds to carry a reference.
- Timed action snapshots now reject stale action-level target coordinates except
  for explicit cell-targeted attacks, keeping attack and cast target channels
  distinct across managed and native boundaries.
- Timed action snapshots now bind action progress to the snapshot tick: actions
  must have started, must not have expired, and `EffectApplied` must agree with
  whether the effect tick has been reached across managed and native boundaries.
- Timed action ordering and snapshot-boundary checks now use one shared domain
  contract across Diablo simulation, mapper, host validation, and Godot protocol
  projection; the client links only that pure rule source and does not depend on
  the server domain assembly.
- Death actions now require a nonzero killing/source entity reference at managed
  and native snapshot boundaries, matching the authoritative death lifecycle.
- Native snapshot validation now enforces standalone player combat-mode lifetime
  rules, matching the managed host and Godot validators.
- Native tile-coordinate bounds are now named once in the shared protocol
  contract and consumed by managed mapper/host/Godot validation and native
  snapshot/world projection, including authored towner greetings.
- The replay contract now carries explicit player strength, dexterity, and
  vitality plus authored physical weapon ranges, covering Rogue class weapon
  damage at a shared native/managed checkpoint; native replay no longer relies
  on the armor-only melee fallback when class attributes are authored.
- Shared class-damage replay now covers Monk staff, Bard sword, and Barbarian
  axe profiles, including Barbarian's class critical-strike RNG draw; ranged
  replay additionally covers Monk weapon, Bard bow, and Barbarian bow profiles.
- The class-damage replay matrix now also covers explicit Warrior and Sorcerer
  profiles; class-0 authored attributes use the native formula while omitted
  attributes retain the legacy compact fallback.
- Initialized physical replay now cross-checks class to-hit, deterministic hit
  seeds, armor reduction, minimum damage, and an authored miss against C#.
- Replay equipment now carries validated `plus_enemy_armor_class`; native and
  managed initialized attacks agree on armor-piercing admission and checkpoint.
- Replay fixtures now carry validated Normal/Nightmare/Hell difficulty; native
  and managed Hell target armor and initial-health modifiers share a checkpoint.
- Initialized physical replay now carries validated percentage, flat, and
  to-hit weapon modifiers through native and managed checkpoints.
- Initialized physical replay now applies the native sword penalty and mace
  bonus against authored undead targets in both runners.
- The same replay boundary now covers the inverse sword/mace adjustments for
  authored animal targets.
- Initialized physical replay now carries demon targets and the validated
  post-armor `TripleDemonDamage` multiplier through both runners.
- Barbarian physical replay now covers native melee armor reduction for
  armor-piercing hit admission while preserving post-hit damage armor.
- Hellfire physical replay now carries authored damage-armor flags and covers
  the deterministic post-hit `Devastation` multiplier in both runners.
- Hellfire armor-piercing replay now covers the native shifted-armor hit
  admission rule in both runners.
- Hellfire physical replay now covers slot-ordered post-attack weapon `Decay`
  mutation while preserving the attack's pre-decay damage.
- Hellfire ranged replay now applies `Decay` once per emitted arrow before
  projectile resolution and refreshes the ranged fixture's content identity.
- Hellfire physical replay now covers the native post-armor `Jesters` roll and
  upper-half expansion in both runners.
- Hellfire physical replay now covers `Peril` pre-modifier self-damage and its
  final target-damage multiplier in both runners.
- Hellfire ranged replay now covers `Jesters` Q6 scaling before elemental
  channel resolution in both runners.
- Hellfire ranged replay now covers the post-hit `Devastation` multiplier in
  both runners.
- Hellfire ranged replay now carries `Peril` self-damage and its post-armor
  multiplier into projectile snapshots in both runners.
- Ranged Hellfire replay now applies deferred `Peril` self-damage at arrow
  impact and checks the shared target/removal checkpoint.
- Hellfire ranged replay now carries the deterministic `Doppelganger` proc
  flag into projectile snapshots in both runners.
- The ranged replay gate now covers the three-arrow `MultipleArrows` volley,
  including native spread positions and activation delays.
- Initialized ranged replay now covers the authored Rogue bow damage profile
  and shared damage-range RNG draw.
- Initialized ranged replay now covers class hit admission and target armor
  reduction for authored Rogue bow damage.

- Monster item and AI runtime seeds now travel as an all-or-nothing pair,
  preserving deterministic native initialization at mapper, host, Godot, and
  native snapshot boundaries.

- Native monster AI identity, directional state, and Scavenger corpse
  references now validate consistently at managed mapper/host/Godot and
  native snapshot boundaries.

- Native monster AI timers and counters now use the shared compatibility
  bounds at managed mapper/host/Godot and native snapshot boundaries,
  rejecting malformed state before presentation or persistence.

- Native monster movement modes and turn-state fields now use shared `-1..3`
  and `0..1` compatibility bounds across managed/native snapshot validation,
  Diablo state restoration, replay loading, and protocol aliases.

- Native monster movement counters now use one shared compatibility bound
  across Diablo ticking/restoration, snapshot validation, protocol aliases,
  and native envelope assertions.

- Native monster scalar compatibility bounds for experience, accuracy,
  intelligence, directions, pack/activity state, Fallen animation/rally state,
  and resistances now have one shared source consumed by Diablo construction,
  snapshot validation, catalog validation, replay/protocol aliases, and native
  envelope assertions.

- Native monster AI timer, short-counter, retreat, and death-animation bounds
  now have one shared compatibility source consumed by Diablo state/catalog
  validation, replay fixture validation, the protocol contract, and native
  envelope assertions.

- Native progression bounds now have one shared `Devilution.Shared` source for
  stable/native spell IDs, spell levels, memorized slots, character levels,
  Diablo victory ranks, and Reflect charges; protocol and Diablo validation
  consume the same contract.

- Native spell identity compatibility now has one shared `0..51` limit across
  the protocol/domain contract, Diablo content catalogs, replay validation, and
  native compatibility tests.

- Replay parity fixtures now use the active base-content manifest identity.
  Native and managed replay checkpoints also agree on multiplayer Snake,
  Fallen-rally, and Mega Inferno targeting, including native RNG and impact
  lifecycle boundaries; malformed companion fixtures now report the same
  validation precedence in both parsers.

- The 1 MiB authoritative envelope cap is now a named shared protocol limit
  consumed by the managed server, Godot client, and native compatibility codec.
- Native player/entity capacities and level geometry bounds are now named shared
  protocol limits consumed by managed validation, Godot validation, Diablo
  mapping, and the native compatibility layer.
- Native scalar compatibility bounds for monster state, player progression,
  spell levels, visited-level history, light radius, and quest/towner payloads
  now use the same named limits across managed validation and native projection.
- Server, Diablo mapping, and native session boundaries now validate authored
  towner greetings consistently, including count, identity, message, and level
  geometry constraints before publication or presentation.
- Player status-effect counts and native inventory, equipment, belt, and grid
  dimensions now use shared protocol limits across managed, Godot, and native
  snapshot boundaries; native projection rejects malformed status collections.
- Remaining native monster-state bounds for Fallen rally timing, resistance,
  directional presentation, and identity payloads now use the shared protocol
  contract across the mapper, managed/Godot validators, and native projection.
- Native-compatible stash page, cell, and 10x10 geometry limits now use the
  shared protocol contract in managed and native projection/runtime paths.
- Stable item-reference commands now reject slots and stash pages outside the
  shared native inventory, belt, equipment, and stash topology limits.
- Native world projection and presentation now consume the shared monster-pack,
  activation-tick, projectile-damage-type, and projectile-phase bounds.
- Quest/towner byte fields, optional-value sentinels, and authored reward
  offsets now use named shared protocol limits across managed, Godot, and native
  snapshot boundaries.
- Authored object projectile and quest-follow-up offsets now use named shared
  native object-offset limits across all snapshot validation boundaries.
- Replay fixture validation now consumes the shared native inventory, equipment,
  belt, monster, character-level, and resistance compatibility limits instead
  of maintaining duplicate scalar bounds.
- Native movement-intent direction bounds now use named shared `-1..1` limits
  across managed, Godot, and native protocol validation and command creation.
- Server-owned save envelopes, combined session checkpoints, command ledgers,
  and world-seed files now use strict UTF-8 file I/O; malformed bytes and
  invalid UTF-16 fail closed instead of being silently replaced during
  persistence recovery.
- Version-one Diablo save envelopes now require canonical Base64 snapshot
  text, rejecting whitespace or alternate encodings before Protobuf restore.
- Combined session checkpoints are now published before legacy save and command
  receipt mirrors, so a compatibility-write failure cannot leave the primary
  server-owned recovery document stale.
- Hardened verified gameplay-module packages: manifests now require strict
  UTF-8 decoding and package roots reject reparse-point entries before assembly
  hashing or reflection loading; assembly paths must also be canonical and
  traversal-free.
- Hardened external content-pack resource paths: tables, overlays, and binary
  assets now share canonical relative-path validation and reject traversal,
  absolute paths, and separator aliases before composition or hashing.
- Content-pack manifests and TSV tables now use strict UTF-8 decoding and
  reject malformed text instead of silently replacing invalid byte sequences.
- Centralized event-batch and authoritative snapshot collection limits across
  the protocol assembly, managed validators, Godot client validation, and
  native snapshot projection.
- Centralized native set-level and monster leader-relation compatibility bounds
  across managed validation, the Diablo protocol mapper, and native projection.
- Centralized projectile damage/phase and unique-monster dialogue-state bounds
  across managed validation and native snapshot projection.
- Centralized the named projectile presentation-family and native-visual bounds
  alongside the behavior identity limit. Managed server/Godot validation and
  native snapshot projection now consume the same maximums instead of raw
  presentation literals.
- Centralized the status-effect behavior bound across the protocol assembly,
  managed validators, and native player projection; the compatibility bridge no
  longer repeats the raw maximum.
- Centralized item presentation, spell-source, item-action, and spell-targeting
  bounds across the protocol assembly, managed item validation, and native item
  projection, keeping server-owned item semantics aligned at the wire edge.
- Centralized authored object presentation and quest-interaction metadata bounds
  across the protocol assembly, managed validators, and native snapshot
  projection, covering object effects, transitions, rewards, follow-ups, and
  quest/towner interaction kinds.
- Applied the named projectile behavior identities across the server snapshot
  validator/hasher and native projection/presentation validators, removing the
  remaining raw behavior-number switches from the compatibility boundary.
- Added the stable `AuthoritativeProjectileBehavior` identity enum to the
  dependency-free protocol assembly and replaced managed/native projectile
  behavior bounds with the shared protocol limit. Godot's legacy presentation
  fallback now uses named protocol identities instead of raw numeric literals.
- Unified projectile presentation precedence across the Godot and native
  clients: the server-owned `native_visual` enum is authoritative, while
  `native_missile_id` remains a legacy fallback only when that enum is absent.
  This keeps current presentation decisions independent of raw content IDs.
- Unified dialogue-event validation across the server, Godot client, and
  native compatibility bridge: server-owned text is now bounded to 16 KiB of
  UTF-8 and rejects NUL/carriage-return payloads before presentation.
- Applied the shared 128-byte identity boundary to native missile identities
  at server, Godot, and native snapshot validation.
- Centralized the managed dialogue and opaque-identity limits in the protocol
  assembly so server and Godot validation consume one contract.
- Extended native compatibility presentation to consume server-owned localized
  dialogue text when supplied by `DialogueEvent`, retaining the legacy message
  catalog only for ID-only peers. Vendor/service greetings can now cross the
  server/content boundary without local text lookup.
- Hardened the Godot reconnect epoch boundary: transient event batches are now
  ignored until the first validated snapshot of the new server epoch, preventing
  delayed events from the prior transport stream from entering presentation
  history while preserving command-result retry state.
- Made Godot reconnect/resync presentation rewind-safe. An older authoritative
  snapshot now emits a one-advance clock marker so cached players, monsters,
  and projectiles snap to the resync state instead of easing backward from
  stale positions; authoritative simulation state is unchanged.
- Added Hellfire multiplayer Acid and AcidUnique secondary-participant impact
  fixtures. Native and managed replay now agree on shifted Q6 impact damage,
  splat/puddle expiry, primary-player preservation, and the authored alias
  dispatch; the native compact runner now consumes the same Acid hit and
  damage RNG boundary as the server.
- Extended cross-runner MonsterMagic impact coverage to BoneDemon, ArchLich,
  Psychorb, and Necromorb. Each authored alias now verifies the 27-tick
  secondary-participant Q6 impact, preserved primary death state, and retained
  impact-phase projectile snapshot.
- Added four cross-runner multiplayer MonsterMagic target-selection fixtures
  for BoneDemon, ArchLich, Psychorb, and Necromorb. Native and managed replay
  now verify each authored alias routes its OrangeFlare projectile to the
  nearer secondary participant while preserving the farther primary state.
- Added cross-runner Acid lifecycle coverage. Native and managed replay now
  agree on validated Acid expiry, splat conversion, puddle placement and
  persistence, and puddle expiry; seeded projectile IDs are reserved so
  replacement projectiles cannot reuse a released identity.
- Added cross-runner AcidUnique alias coverage so the authored identity uses
  the validated Acid lifecycle instead of generic combat fallback.
- Preserved the authoritative monster activation window in the native replay
  state hash, restoring cross-runner parity for multiplayer death suppression.
- Added LazarusSuccubus multiplayer Blood Star target-selection coverage; both
  runners now preserve the authored alias while routing the projectile to the
  nearer secondary participant.
- Added a validated FireBat Firebolt missile mapping and multiplayer replay
  coverage for nearest-secondary target selection and projectile travel parity.
- Added FireBat Firebolt and Magma MagmaBall multiplayer secondary-impact
  fixtures. Native and managed replay now agree on the two-tick impact/removal
  lifecycle, shifted Q6 damage, and preservation of the primary player's dead
  state for both authored monster projectile families.
- Added the Torchant Fireball multiplayer secondary-impact fixture and aligned
  the compact native replay with the managed distance-based monster-magic hit
  admission, Q6 damage, collision marker, and impact/removal lifecycle.
- Added Torchant Fireball secondary-miss coverage and aligned native duplicate
  zero-distance collision suppression with the managed impact lifecycle.
- Added a validated Torchant Fireball missile mapping and multiplayer replay
  coverage for nearest-secondary target selection and projectile travel parity.
- Added a validated Magma MagmaBall mapping and Hellfire multiplayer replay
  coverage for nearest-secondary target selection and native initial travel.
- Added a validated Storm ThinLightning mapping and Hellfire multiplayer replay
  coverage for controller/segment admission and nearest-secondary selection.

- Added replay coverage for the quest-only Lachdanan and unregistered FireMan
  native identities. Native and managed runners now fail closed for both
  identities instead of allowing generic combat or movement to run.

- Added shared replay coverage for the Zhar and Lazarus native CounselorAi
  aliases. Native and managed runners now preserve each authored identity,
  select the nearer secondary participant, and agree on Counselor delay
  checkpoints.

- Added a Gharbad multi-participant replay fixture through the shared
  GoatMelee/AiAvoidance compatibility path. Native and managed runners now
  preserve the authored Gharbad identity, select the nearest live participant,
  and agree on deterministic movement checkpoints; external Gharbad rows also
  accept validated avoidance-delay tuning.

- Added shared replay coverage for the Warlord and Snotspill native SkeletonAi
  aliases. Native and managed runners now preserve nearest-participant
  selection, shared movement-delay admission, and countdown state for both
  authored identities.

- Extended the Skeleton King multi-participant replay fixture through
  Leoric's compatibility decision-delay lifecycle. Both runners now verify
  nearest-secondary selection, farther-primary preservation, the native
  90/45/straight RoundWalk ordering, delay admission, and countdown state.

- Added a Butcher multi-participant replay fixture and aligned the compact
  native projection with the managed RandomWalk turn ordering. Both runners
  now verify nearest-secondary target selection, farther-primary preservation,
  and an active object blocking the straight pursuit candidate.

- Added a Fallen rally multi-participant replay fixture and aligned the compact
  native projection with the managed nearest-participant pursuit decision.
  Both runners now preserve the farther primary while hashing the diagonal
  secondary-target movement and rally countdown together.

- Added a Mega Inferno multi-participant replay fixture. Native and managed
  runners now verify nearest-participant InfernoController admission,
  secondary-target routing, persistent segment state, and farther-primary
  preservation against the shared checkpoint hash.

- Added shared replay coverage for Fallen stand-animation and rally-admission
  state. Native and managed runners now parse, validate, advance the authored
  frame/tick boundary, apply the native Q6 self-heal and rally timer, and hash
  the resulting state together. The replay gate now also covers one-tick
  pursuit beyond normal aggro range and bounded same-level companion
  propagation. Native and managed loaders reject the same malformed companion
  leader, identity, level, AI, vitality, intelligence, animation, and timer
  state; broader multi-actor selection remains follow-up replay work.

- Added a seeded GoatMelee multi-participant replay fixture. Native and managed
  runners now verify nearest-live-participant selection for autonomous
  AiAvoidance movement while preserving the farther participant's state.

- Added a seeded Gargoyle multi-participant replay fixture. Native and managed
  runners now verify nearest-live-participant selection through Gargoyle's
  autonomous avoidance branch, including authoritative movement and AI-stream
  checkpoint parity.

- Added a Scavenger multi-candidate replay fixture and aligned the compact
  native replay controller with the managed/native coin-directed corpse scan.
  Both runners now preserve first-candidate selection, movement, selected-corpse
  identity, and canonical checkpoints.

- Added a Bat multi-participant replay fixture. Native and managed runners now
  verify nearest-live-participant selection for the retreat controller while
  preserving the farther primary participant and movement checkpoints.

- Added multiplayer target-selection fixtures for Zombie and Fat. Native and
  managed runners now cover the nearer secondary participant, farther-primary
  preservation, and branch-specific movement state for both autonomous lanes.

- Added a Sneak multiplayer target-selection fixture. Native and managed runners
  now preserve preferred-distance concealment admission against a nearer
  secondary participant while retaining the farther primary state.

- Added a Rhino multiplayer target-selection fixture. Native and managed runners
  now verify nearest-participant charge admission, secondary damage and
  knockback, monster relocation, and farther-primary preservation.

- Added a SkeletonMelee multiplayer target-selection fixture. Native and managed
  runners now verify nearest-participant adjacent attack admission, secondary
  damage, and farther-primary preservation.

- Added a Snake multiplayer target-selection fixture. Native and managed runners
  now verify nearest-participant charge routing through the full projectile
  lifecycle while preserving the farther primary participant.

- Added a Counselor multiplayer target-selection fixture and aligned the native
  replay projection with nearest-player selection, including deterministic
  entity-id tie-breaking and the authored adjacent delay state.

- Replay object-interaction fixtures now declare their authored quest link
  explicitly, keeping object-quest timelines valid after inert-object command
  admission was hardened; native and managed runners now apply the same linked
  quest progression and duplicate-advance rejection.

- Snapshot hydration and canonical state hashing now preserve authored object
  dialogue identities when an existing object is updated in place, keeping
  dialogue-only interactions available across replacement-session restores.

- Server object command admission now requires an authored interaction. Direct
  commands cannot activate presentation-only objects, while dialogue-only
  objects remain valid authoritative targets.

- Projected authored object snapshots now carry an additive
  `dialogue_message_id`. Dialogue-only environmental objects can be discovered
  and routed by native and Godot clients even when they are not linked to a
  quest; invalid identities are rejected at the server, native, and Godot
  boundaries.

- Authoritative unique-monster dialogue snapshots now carry an additive,
  validated state projection. Staged Gharbad/Warlord routes explicitly move
  from `Inquiring` to `Combat`, allowing the native compatibility client to
  render server-owned encounter transitions without consulting legacy quest
  tables. Unauthored routes retain the unspecified legacy fallback.

- Move item-action dialogue identities into validated content metadata. Na-Krul
  notes now declare their `TEXT_BOOK9` message ID in `items.tsv`; the server
  resolves it through the active item definition and retains the native `322`
  fallback for older packs. Advance the Diablo content schema to `1.449.0`;
  `1.448.0` remains accepted as the previous additive contract.

- Externalized Na-Krul note reading as the authored `ReadQuestNote` item action. Connected clients now submit the existing stable item-use command; the server emits the native `TEXT_BOOK9` dialogue without consuming the note, and native/Godot clients present the same text. Advance the Diablo content schema to `1.448.0`; `1.447.0` remains accepted as the previous additive contract.

- Routed the native Cathedral Map action through the authoritative item-command
  boundary. Base and Hellfire content now declare `OpenCathedralMap`; the server
  keeps the item owned and emits a recipient-scoped `MapOpenedEvent`, while
  native and Godot clients present the transient map interaction. Advance the
  Diablo content schema to `1.447.0`; `1.446.0` remains accepted as the previous
  additive contract.

- Added the optional `golem_monster_symbol` spell-content field. The shipped
  Golem row now names stable monster identity `golem_native_109` (stable ID
  111, native selector 109), while numeric-only rows remain compatible. Advance
  the Diablo content schema to `1.446.0`; `1.445.0` remains accepted as the
  previous additive contract.
- Corrected authored unique placement and pack materialization to resolve stable
  monster IDs through validated content before calling native-ID summon logic;
  stable IDs that differ from native IDs now materialize consistently in the
  server and Godot live protocol paths.
- Corrected authored quest-object monster follow-ups and Hork Spawn payloads to
  resolve stable monster IDs before native summon creation; fixed shipped Hork
  Spawn content that previously pointed at Arachnon’s stable identity.
- Externalized native AiAvoidance RoundWalk chance base and intelligence scale
  into validated monster content. Shipped GoatMelee and Gargoyle rows retain
  the native `16`/`5` tuning, while older packs use the compatibility fallback.
  Advance the Diablo content schema to `1.445.0`; `1.444.0` remains accepted
  as the previous additive contract.
- Added generic-host command-acknowledgement validation before TCP delivery,
  matching the Godot client result-count, status, and reject-reason contract.
- Converged generic host admission with native and Godot player combat-mode
  lifetime validation for Stand, Block, and GotHit states.
- Converged generic host admission with native and Godot authored object
  effect, quest-transition, reward, projectile, presentation, and breakage
  validation.
- Converged generic host admission with native and Godot monster occupancy,
  movement timing, and Fallen animation/rally validation.
- Converged generic host admission with native and Godot player resource,
  attribute, visited-level, spell, status-effect, and stash validation.
- Converged generic host admission with native and Godot player-item topology,
  identity, price, footprint, and active-store validation.
- Converged generic host admission with native and Godot world-item price,
  identity, and item-state validation.
- Enforced DevilutionX's `MaxMonsters` limit of 200 active monsters per level
  across authoritative runtime validation, save restore, server and host
  protocol admission, Godot validation, and native snapshot projection.
- Completed generic host pool validation for DevilutionX's `MAXOBJECTS` and
  `MAXITEMS` per-level limits, matching the server, Godot, and native snapshot
  boundaries.
- Enforced DevilutionX's `MAX_PLRS` four-player pool across participant
  projection, host admission, Godot admission, and native player projection.
- Enforced DevilutionX's `MAXTRIGGERS` seven-warp-trigger-per-level pool across
  server projection, host admission, Godot geometry admission, and native
  geometry projection.
- Converged generic host admission with the server, Godot, and native quest
  state and quest/towner interaction validation contracts.
- Converged generic host admission with native and Godot level-geometry
  dimensions, cell-layer, duplicate-level, and warp-target validation.
- Converged generic host admission with native and Godot entity-position,
  set-level-origin, and procedural-level identity validation.
- Converged generic host admission with native and Godot projected player-action
  timing, movement, attack, cast, death, and transition validation.
- Converged generic host admission with native and Godot monster vitality,
  combat-range, movement, and pack-leader validation.
- Converged generic host admission with native and Godot projectile source,
  lifetime, damage, behavior, presentation, direction, and missile-identity
  validation.
- Enforced the native four-route portal pool in authoritative world-state
  construction, static catalog loading, Town Portal creation, and save restore;
  runtime state can no longer grow beyond the projection boundary.
- Reconciled native signed-transparency wire-byte validation so values 0–255
  are preserved consistently by the C# world, Godot, and native boundaries.
- Enforced DevilutionX's `MAXPORTAL` limit across authoritative server
  projection, host validation, Godot admission, and native client projection;
  snapshots with more than four portal routes now fail closed.
- Fail closed on ambiguous native monster summon selectors instead of falling
  through to a potentially unrelated numeric stable-ID row; older selectors
  still use the compatibility fallback when no native mapping exists.
- Apply that ambiguity guard consistently to authored native-monster quest
  follow-ups during content validation and command admission.
- Bound native monster-type and DUN mapping identities to DevilutionX's
  200-entry monster table while retaining the separate raw DUN layer bound.
- Bound external native `.til`/`.sol` d-piece identities to DevilutionX's
  `MAXTILES` range (0–1378) before collision materialization.
- Enforced the same native d-piece bound on projected level layers and authored
  `piece_layer_rects` mutations.
- Enforced native transparency and mega-tile value ranges on authored mutation
  assignments before publication.
- Canonicalized native DUN transparency words to their low-byte signed-`int8_t`
  representation across the server, protocol, and Godot validation paths.
- Mirrored the native `MAXTILES` d-piece bound in Godot snapshot validation.
- Bound procedural level dimensions to the native 112x112 fine-grid contract
  during content validation instead of allowing late materialization failures.
- Bound native DUN monster materialization to DevilutionX's 200-monster pool per
  level, rejecting oversized external layers before unbounded state creation.
- Bound native DUN object materialization to DevilutionX's 127-object pool per
  level, rejecting oversized external layers before unbounded state creation.
- Enforced the same native object-pool capacity on server, Godot, and native
  snapshot boundaries, rejecting more than 127 projected objects on one level.
- Enforced that native object-pool capacity while materializing set-level
  placements and native-DUN fragment template objects, including existing
  authored occupants on the target level.
- Fixed native object presentation resynchronization so same-position
  activation and destruction transitions clear stale tile occupancy while
  retained-visible activated objects are rebound to their authoritative tile.
- Enforced DevilutionX's native `MAXITEMS` world-item pool across server,
  Godot, and native snapshot boundaries, rejecting more than 127 active items
  on one level before compatibility presentation.

- Guard the shipped monster catalogs against identity gaps: the composed
  Hellfire variant now verifies stable monster IDs 1–140 and native IDs 0–137,
  keeping external monster content complete as overlays evolve.

- Add an explicit `coverage_mode` contract to `monster_spawn_policies.tsv`.
  The shipped base table declares complete coverage and activation rejects
  missing active monster identities; omitted declarations remain partial for
  backward-compatible custom packs.

- Advance the Diablo content schema to `1.444.0` for spawn-policy coverage
  metadata; `1.443.0` remains accepted as the previous additive schema.

- Refresh the migration-status priorities so they describe remaining combat/AI,
  dungeon-generation, content-coverage, Godot, replay, operations, and Lua
  work instead of listing already-migrated spell behaviors as future work.

- Validate explicit unique-monster `monster_id` selectors against the active
  monster-type catalog during content activation, while preserving deferred
  native-AI-only selectors for generated and set-piece encounters.

- Keep legacy Golem/Diablo procedural-admission compatibility in the validated
  spawn-policy catalog rather than in the planner, so the planner remains
  content-driven while older packs continue to load.

- Resolve legacy summon identities through the validated native-AI catalog,
  avoiding numeric monster IDs in combat code while rejecting ambiguous
  content-pack mappings.

- Declare an explicit ordinary/summon/boss/quest spawn role for every shipped
  monster in the base content pack, with completeness validation that keeps
  ordinary procedural admission content-owned while preserving partial-pack
  compatibility fallbacks.

- Move the ordinary procedural-monster ceiling into validated
  `level_generation.tsv` metadata. Custom packs may select a bounded capacity;
  rows that omit it retain the native 190-slot compatibility default.

- Advance the Diablo content schema to `1.443.0` for the ordinary-spawn
  capacity field; `1.442.0` remains accepted as the previous additive schema.

- Add a documentation/content identity regression guard so the active
  Godevilution README, migration status, changelog, content guide, and shipped
  manifests stay aligned on the project name and current schema.

- Externalize the Golem summon target through the optional `golem_monster_id`
  spell field and validate it against active monster types, while retaining the
  native compatibility fallback for older packs.

- Externalize procedural monster admission roles in the validated
  `monster_spawn_policies.tsv` table. The shipped base pack marks Skeleton
  King, The Butcher, and Snotspill as quest encounters, Golem as a summon, and
  The Dark Lord as a boss; older packs without the table retain the
  ordinary-spawn compatibility fallback.

- Complete the base monster identity catalog with the native Golem and The Dark
  Lord rows, stable IDs, and available DUN mappings; ordinary procedural
  generation now excludes summon and boss identities.

- Expand the base content pack with native identities 112–137, covering the
  late Hellboar, Psychorb, HorkDemon, Necromorb, Torchant, FireBat, Lich,
  BoneDemon, ArchLich, and Na-Krul families with their stable IDs and available
  DUN mappings.

- Expand the base content pack with native identities 89–111: Snake and
  Skeleton late-game families, Succubus variants, and Counselor through
  Arch-Litch Malignus, including their stable IDs and DUN mappings.

- Expand the base content pack with the validated native 68–88 Storm, FireMan,
  Gargoyle, and Mega families, including stable identities, AI metadata, and
  native DUN mappings.

- Expand the base content pack with the validated midgame Acid, Fat,
  SkeletonMelee, Magma, and Rhino families (native IDs 46–49 and 52–67),
  including stable identities, AI metadata, and native DUN mappings.

- Expand the base content pack with the validated native 28–45 families:
  Invisible Lord, Sneak, Goat, and Bat variants through Night Clan. Their
  stable identities, native AI metadata, and available DUN mappings are now
  available to production server generation; raw DUN identity 31 remains
  reserved by the authored Banner fragment.

- Expand the base content pack with the next validated early-undead progression:
  Carver, Plague Eater, Shadow Beast, Bone Gasher, Skeleton, Corpse Bow, Burning
  Dead, Horror, and the Skeleton Captain variants. Their native AI metadata and
  DUN mappings are now available to production server generation; native Carver
  remains stable-ID-only because raw DUN identity 14 is authored Snotspill.

- Expand the base content pack with the canonical Ghoul, Rotting Carcass,
  Black Death, Fallen One, Carver, Devil Kin, and Dark One identities. Their
  validated native AI metadata and DUN monster mappings are now available to
  production server level generation; the generic fallback and authored
  encounter identities remain compatible.

- Treat the first snapshot received after a reconnect as a new server epoch.
  The Godot protocol model now accepts a lower server tick after process or
  session recovery while clearing transient events and prediction; the
  authoritative snapshot and command-result ledger remain available until the
  replacement snapshot arrives.

- Consolidate authoritative player-status presentation in the Godot client.
  HUD and world rendering now consume one typed projection for Mana Shield,
  Reflect charges, and timed status families.

- Complete typed status consumption in the Godot client. Search highlighting,
  player overlays, and HUD summaries now reuse one active-status projection;
  zero-duration effects are ignored at the presentation boundary.

- Expose exact server-owned status durations and permanent status state in a
  dedicated Godot HUD line without adding client-side expiry logic.

- Preserve server-authored missile-only level collision in the Godot layout
  projection and render it as a distinct occlusion cue; walk-blocked cells
  remain visually and semantically separate.

- Teach the Godot presentation client to consume server-authored object
  lifecycle frames. Activated authored books now render an open-book cue when
  the server keeps their native object visible; older snapshots retain the
  existing generic placeholder behavior.

- Externalize the native activated-book presentation lifecycle for the Halls of
  the Blind, Gateway of Blood, and Warlord steel-tome templates. The base pack
  now keeps each one visible and selects its native open-book frame after the
  server-owned quest transition; set-piece placement, rewards, and map
  resynchronization remain explicitly deferred until their licensed geometry
  is available.

- Extract the Bat retreat, Fat movement, and Zombie action replay fixtures into
  validated content packs, including their native AI chance, movement, and
  retreat tuning.

- Extract the Rhino charge and Diablo/Hellfire Scavenger corpse replay fixtures
  into validated content packs, including native charge, movement, delay, and
  healing tuning.

- Extract the Sneak conceal/reveal replay fixtures into a validated content pack,
  including native stealth distance and retreat tuning.

- Canonicalize the elemental equipment and ranged-arrow replay fixtures to the
  validated `devilution.base` content manifest and enforce that identity in the
  managed replay tests.

- Extract the SkeletonMelee movement-delay and failed-adjacent-attack replay
  fixtures into the validated `test/content/replay/skeleton-melee` pack,
  including native chance bands and delay formulas.

- Extract the Gargoyle healing, movement, avoidance, and adjacent-attack replay
  fixtures into the validated `test/content/replay/gargoyle` pack, including
  native safety, healing, and AiAvoidance tuning.

- Extract the GoatMelee movement and adjacent-attack replay fixtures into the
  validated `test/content/replay/goat-melee` pack, including native
  AiAvoidance and wounded-special tuning.

- Extract the Counselor delay and retreat replay fixtures into the validated
  `test/content/replay/counselor` pack and activate their external monster
  type/timing metadata through the compatibility executor.

- Extract the basic healing spell replay fixture into the validated
  `test/content/replay/spell-cast` pack and bind the managed runner to its
  external spell row.

- Move the Hork Spawn replay planner’s level-generation rule into the
  validated `monster-hork-spawn` pack, completing externalization of its
  missile, summoned-monster, and encounter-generation metadata.

- Move the Hork Spawn summoned-monster type row into the validated
  `monster-hork-spawn` pack, so the replay helper no longer constructs spawn
  metadata inline.

- Extract the autonomous GoatRanged Arrow replay fixture into the validated
  `monster-arrow-autonomous` pack and carry its compact native retreat tuning
  through the replay contract so native and managed admission remain identical.

- Extract the nine Hork Spawn and three Snake Charge replay fixtures into
  validated external missile packs, preserving native/C# admission, delay,
  travel, impact, blocking, and summon-controller parity.

- Extract the nine Monster Arrow replay fixtures’ GoatRanged and
  SkeletonRanged mappings into validated shared and admission content packs,
  preserving native/C# projectile parity and manifest validation.

- Extract the nine Monster Magic replay fixtures’ Lich-family and orb
  `OrangeFlare` mappings into the validated `test/content/replay/monster-magic`
  pack, preserving native/C# projectile parity and manifest validation.

- Extract the seven Blood Star replay fixtures’ Succubus and Lazarus Succubus
  missile mappings into the validated `test/content/replay/blood-star` pack,
  preserving native/C# projectile parity and manifest validation.

- Extract the shared Mega Inferno replay spell and native missile rows into the
  validated `test/content/replay/mega-inferno` pack, preserving both native
  controller lifecycles and managed manifest validation.

- Extract the Hellfire Charged Bolt and status-expiry replay rows into validated external replay packs, preserving native/C# checkpoint parity.

- Extract the Bone Spirit homing and Hellfire Firebolt/Fireball fixtures into the validated `test/content/replay/projectile-variants` pack; align the native homing lifetime assertion with the shared replay checkpoint.

- Extract the five standard Charged Bolt replay fixtures into the validated `test/content/replay/charged-bolt` pack and bind their manifests to its canonical identity.
- Bind the Rage, Inferno, and Apocalypse replay runners to the validated base `spells.tsv` catalog, preserving native controller, segment, target-scan, and checkpoint parity.

- Move the Firebolt, Fireball, and Bone Spirit PvP replay fixtures to the validated base `spells.tsv` catalog and active content-manifest identity, preserving native player-hit, current-life, resistance, and checkpoint parity.

- Move the Chain Lightning, Immolation, Elemental, and Guardian replay
  fixtures to the validated base `spells.tsv` catalog and active content-manifest
  identity, preserving native controller, retargeting, and turret checkpoints.

- Move the Lightning Wall, Ring of Fire, Nova, and Flame Wave replay fixtures
  to the validated base `spells.tsv` catalog and active content-manifest
  identity, preserving native controller and projectile checkpoints.

- Move the Magi, Mana, Reflect, Etherealize, and Berserk replay fixtures
  to the validated base `spells.tsv` catalog and active content-manifest
  identity, preserving restoration, retaliation, status, and mutation parity.

- Move the Warp, Golem, Jester, and Town Portal replay fixtures to the
  validated base `spells.tsv` catalog and active content-manifest identity,
  preserving native transition, summon, dispatch, and portal checkpoints.

- Move all six Telekinesis replay fixtures to the validated base `spells.tsv`
  catalog and active content-manifest identity, preserving native pickup,
  object, knockback, and rejection checkpoints in the managed runner.

- Rebind all checked-in base-content replay fixtures to the active content
  manifest and move Fire Wall replay coverage to the validated `spells.tsv`
  catalog, keeping native and managed controller checkpoints aligned.

- Add shared Holy Bolt replay coverage. Native and managed runners now preserve
  undead-only collision, level-scaled projectile speed, physical damage, mana
  consumption, impact expiry, and canonical state hashing.

- Extend Holy Bolt replay coverage with living-target pass-through. Native and
  managed runners now prove non-undead targets remain unharmed while the
  projectile continues through the shared timeline.

- Add shared Telekinesis replay coverage. Native and managed runners now
  preserve server-owned world-item pickup, object activation, monster knockback,
  authored object experience effects, target validation, memorized-spell
  admission, mana consumption, and canonical state hashing. Rejection fixtures
  cover invalid coordinates, insufficient mana, and unmemorized spell sources
  while proving failed casts preserve world state and resources.

- Add shared Counselor AI replay coverage. Native and managed runners now
  preserve server-owned adjacent decision delay, wounded retreat movement, and
  retreat countdown state with canonical checkpoint parity.

- Add shared Town Portal replay coverage. Native and managed runners now
  preserve dynamic portal endpoints, ownership, bidirectional lifetime state,
  mana consumption, and canonical portal hashing.

- Add shared Jester replay coverage. Native and managed runners now preserve
  deterministic dispatch RNG, nested Firebolt projectile creation, one-time
  mana consumption, and canonical projectile state hashing.

- Add shared Golem replay coverage. Native and managed runners now preserve
  server-owned minion identity, owner, native summon stats and seeds, death
  animation timing, mana consumption, and canonical monster state hashing.

- Add shared Warp replay coverage. Native and managed runners now preserve
  normalized level-trigger resolution, deterministic destination selection,
  level-visit history, mana consumption, and canonical level-transition state.

- Add shared Berserk replay coverage. Native and managed runners now preserve
  eligible-target admission, native four-channel damage scaling, and recast
  rejection with canonical monster state hashing.

- Add shared Etherealize replay coverage. Native and managed runners now
  preserve permanent status hashing and monster-damage immunity across ticks.

- Add shared Reflect replay coverage. Native and managed runners now preserve
  level-scaled charge creation, canonical charge state, charge consumption,
  reflected monster damage, and the aligned gameplay RNG stream.

- Formalize projection-only replay checkpoint markers. Timeline checkpoints
  may now omit a canonical hash while both runners still advance and verify
  the covered lifecycle boundary.

- Add shared Guardian replay coverage. Native and managed runners now
  preserve the one-tick turret admission, deterministic 48-position firing
  arc, Firebolt creation, controller state, and projectile lifetime.

- Add shared Rage replay coverage. Native and managed runners now preserve
  active/cooldown timing, duplicate-cast suppression, and the bounded Q6
  self-damage applied when the Rage controller expires.

- Add shared Inferno replay coverage. Native and managed runners now preserve
  the moving controller, deterministic three-segment emission, and persistent
  segment lifetimes.

- Add shared Apocalypse replay coverage. Native and managed runners now
  preserve row-major target scanning, physical boom emission, target impact,
  and boom lifetime.

- Add shared Magi and Mana replay coverage. Native and managed runners now
  preserve full-mana restoration and seeded, class-scaled mana restoration.

- Add shared Elemental replay coverage. Native and managed runners now
  preserve the retargeting projectile lifecycle, deterministic fire burst
  timing, and canonical projectile checkpoints.

- Add shared Immolation replay coverage. Native and managed runners now
  preserve the one-tick controller, deterministic radial fan ordering,
  Fireball movement, and canonical projectile checkpoints.

- Add shared Chain Lightning replay coverage. Native and managed runners now
  preserve the one-tick controller, deterministic native crawl fan-out,
  Lightning bolt movement, impact segments, and canonical checkpoints.

- Add shared Flame Wave replay coverage. Native and managed runners now preserve
  the one-tick controller, deterministic center/branch geometry, per-segment
  damage, same-tick movement, and canonical projectile checkpoints.

- Add shared Nova replay coverage. Native and managed runners now preserve
  the one-tick controller, native 36-ball radial fan ordering, first-tick
  movement, and canonical projectile checkpoints.

- Add shared Ring of Fire replay coverage. Native and managed runners now
  preserve the deterministic radius-three crawl, persistent Fire Wall segment
  creation, and canonical projectile checkpoints.

- Extend shared wall replay coverage to Lightning Wall. Native and managed
  runners now preserve the shared controller geometry with lightning-specific
  segment damage, duration, presentation identity, and canonical checkpoints.

- Add shared Fire Wall replay coverage. Native and managed runners now preserve
  the seven-tick controller, deterministic two-branch segment growth, scaled
  segment damage/lifetime, and canonical projectile checkpoints.

- Add shared Mega InfernoController replay coverage. Native and managed
  runners now preserve the three-step moving fire-stream controller, segment
  lifetime, and Mega recovery delay through canonical checkpoints.

- Extend Mega InfernoController replay coverage to the native AI stream.
  Seeded multiplayer execution now preserves monster-seed advancement and
  stream-driven recovery delay across both runners.

- Add seeded Succubus/Lazarus BloodStar replay coverage. Native and managed
  runners now consume the restored AI stream for close-range retreat RandomWalk
  draws and post-shot ranged-delay state, preserving canonical checkpoints.

- Add seeded GoatRanged replay coverage. Native and managed runners now
  consume the restored AI stream for close-range retreat RandomWalk draws and
  post-shot ranged-delay state, preserving canonical checkpoints.

- Add seeded SkeletonRanged replay coverage. Native and managed runners now
  consume the same native AI stream for arrow admission and preserve the
  updated monster seed through the canonical checkpoint.

- Add a shared SkeletonRanged close-retreat replay fixture. Native and managed
  runners now preserve nearest-participant selection, deterministic retreat
  destinations, movement counters, and same-tick arrow suppression.

- Extend the shared GoatRanged replay contract with persisted ranged-delay
  state. Native and managed runners now agree on delayed admission and the
  following secondary-targeted arrow checkpoint.

- Add a shared GoatRanged close-retreat replay fixture. Native and managed
  runners now preserve nearest-participant selection, deterministic retreat
  destinations, and the no-projectile retreat boundary.

- Add a shared GoatRanged multiplayer target-selection replay fixture. Native
  and managed runners now preserve nearest-participant selection, projectile
  admission, and canonical checkpoint parity without generic melee pursuit.

- Restore native/C# canonical checkpoint parity for multiplayer autonomous
  target-selection fixtures by letting the native replay executor hydrate the
  complete secondary participant state from the shared fixture.

- Add shared Sneak conceal/reveal and Bat retreat replay fixtures. Native and
  managed runners now preserve stealth visibility transitions, retreat
  movement, countdown state, and canonical checkpoint parity.

- Add a shared Scavenger corpse lifecycle replay fixture. Native and managed
  runners now preserve corpse selection, movement, Q6 healing, countdown state,
  gameplay RNG consumption, and canonical checkpoint parity.

- Extend Scavenger replay parity through the Hellfire healing variant. Both
  runners now verify max-health-fraction healing and removal of the consumed
  corpse when the monster reaches full health.

- Add a shared Zombie autonomous-AI replay fixture. Native and managed runners
  now preserve the action admission band, deterministic long-range walk, and
  current/future occupancy across the tick boundary.

- Add a shared Fat/Overlord movement replay fixture. Native and managed runners
  now preserve the fresh movement admission band, movement mode, idle counter,
  and current/future occupancy across consecutive ticks.

- Add a shared GoatMelee AiAvoidance movement replay fixture. Native and managed
  runners now preserve the compatibility movement band, turn ordering, and
  avoidance counter state without advancing the gameplay RNG.

- Add a shared GoatMelee adjacent-attack replay fixture. Native and managed
  runners now preserve seeded attack admission, block timing, and AI-stream
  advancement for the melee attack boundary.

- Add cross-language BloodStar admission coverage for the LazarusSuccubus
  native alias, preserving the shipped alias-to-missile mapping in replay.

- Add a shared SnakeCharge replay fixture covering native admission, projectile
  travel, player collision, impact timing, and canonical checkpoint parity.
- Add shared Snake autonomous-AI delay fixtures for the distant-delay and
  failed adjacent-attack admission bands. Native and managed replay runners
  now carry the server-owned Snake delay state through canonical checkpoints.

- Add a shared Rhino charge replay fixture. Native and managed runners now
  preserve charge admission, the nearest adjacent destination, special damage,
  monster relocation, player knockback, and unchanged single-player RNG.
- Add shared SkeletonMelee replay fixtures for native movement-delay admission,
  failed adjacent-attack admission, and their countdown lifecycles. Both
  runners preserve the server-owned delay state and unchanged global RNG across
  these autonomous boundaries.
- Extend the shared Gargoyle avoidance Move-goal fixture through its second
  autonomous tick. Native and managed runners now verify movement completion,
  next-tile RoundWalk selection, persisted goal state, per-monster seed advance,
  and the preserved global RNG state.
- Add cross-language coverage for a primary cursor pickup attempted on the
  player's occupied tile. Native and managed runners now record the
  `NotAllowed` rejection while preserving the held and world items.
- Add cross-language coverage for accepted primary cursor replacement pickup.
  Both runners now verify atomic origin drop, inventory placement, and the
  shared checkpoint hash.
- Extend the shared replay schema and runners to cover accepted secondary
  cursor replacement pickup, including authoritative origin-drop placement and
  participant checkpoint parity.
- Add secondary occupied-tile cursor rejection coverage and preserve native/C#
  facing-state parity when secondary movement precedes the rejected pickup.
- Extend replay participants with validated secondary equipment and add a
  cross-language lethal MonsterArrow fixture. Native and managed runners now
  agree on secondary death mode, Gold halving, deterministic world-drop seeds,
  facing-ordered equipment placement, and the resulting canonical hash.
- Align generic autonomous monster melee with the managed participant model.
  Native and C# runners now select the nearest live participant with stable
  entity-ID tie breaking, including secondary-player damage and death state.
- Match the managed bounded breadth-first fallback for generic monster crawl.
  Native replay now preserves both participants and active blocking objects as
  occupancy blockers and selects the same alternate first step when direct
  approach tiles are blocked.
- Carry paired native monster runtime seeds through replay fixtures. The
  multiplayer AI seed now advances on the authoritative tick boundary, and
  generic secondary-player melee matches native hit/block stream consumption,
  block timing, and canonical state hashing.
- Extend seeded secondary melee coverage through successful impact. Both
  runners now consume the native min/max damage draw and preserve the shared
  post-impact RNG state and participant hash.
- Add the Gargoyle wounded self-healing replay boundary. Native and managed
  runners now consume the same deterministic divisor draw, apply Q6 healing,
  retain the native special lock, and preserve the canonical checkpoint across
  the lock's following tick.
- Extend Gargoyle replay parity through adjacent attack admission. Native and
  managed runners now consume the extra attack-band draw before hit/block
  resolution, including secondary-player block timing and the multiplayer
  monster RNG stream.
- Add Gargoyle avoidance-idle replay parity. Native and managed runners now
  consume the healthy non-adjacent action draw and preserve the authoritative
  avoidance counter when no movement is admitted.
- Extend Gargoyle avoidance replay parity through admitted RandomWalk. Native
  and managed runners now preserve the candidate coin draws, destination,
  direction, occupancy checks, and canonical checkpoint hash.
- Honor active object occupancy in the Gargoyle RandomWalk replay boundary.
  The shared fixture now proves the straight candidate is rejected before the
  next deterministic destination is selected.
- Add Gargoyle far-distance Move-goal replay parity. Native and managed runners
  now preserve the admission coin, turn initialization, persisted goal tick,
  RoundWalk destination, and canonical checkpoint hash.
- Guard the legacy native client loop against opt-in authoritative-runtime
  references when server-backed client support is disabled, restoring the
  default replay-test build.

- Keep authoritative store presentation out of the legacy Lua event bus. The
  native `StoreOpened` callback remains available in compatibility mode behind
  the centralized Lua lifecycle policy.
- Keep authoritative data reload, game-start, and frame-complete lifecycle
  emissions out of the legacy Lua event bus while preserving those callbacks in
  compatibility mode.

- Add the cross-language `gameplay-stable-item-drop.json` replay fixture.
  Native and managed runners now parse and execute explicit
  `DropItemRequested` inventory references with stable identity validation,
  source removal, and authoritative world-item materialization. The legacy
  `DropHeldItem` replay path remains covered separately.

- Add multiplayer secondary-participant stable-reference drop coverage. The
  shared fixture initializes one secondary inventory item and verifies
  participant routing, stable identity validation, source removal, and
  adjacent world-item materialization in both replay runners.

- Add multiplayer secondary-participant world-item pickup coverage. The shared
  fixture verifies authoritative item removal, inventory placement, and
  canonical checkpoint parity through the same protocol command.

- Add occupied-inventory secondary-participant pickup coverage. A companion
  fixture verifies that native and managed runners preserve an existing item
  and choose the same first-fit cell for the picked world item.

- Extend secondary-participant pickup parity with an accepted-then-stale retry.
  Native and managed replay runners now agree on the authoritative
  `InvalidTarget` rejection reason after the world entity is consumed.

- Add secondary-participant inventory-overflow pickup coverage. A full
  secondary inventory now accepts a world-item pickup into the authoritative
  secondary cursor, with native and managed checkpoint parity.

- Complete the secondary cursor lifecycle in replay coverage. The overflow
  fixture now drops the held item through `DropHeldItem` and verifies adjacent
  authoritative world-item placement in both runners.

- Add secondary-participant Gold pickup parity coverage. The shared fixture
  credits Gold directly to the secondary wallet, removes the world pile, and
  verifies the canonical participant hash in native and managed replay.

- Add malformed secondary-participant Gold pickup coverage. Native and managed
  runners now reject an invalid Gold identity as `Malformed` while preserving
  the secondary wallet, cursor, and world item.

- Extend secondary Gold pickup parity to accumulate into an existing wallet
  balance, proving that the participant's starting Gold survives the pickup.

- Add secondary Gold wallet-overflow parity coverage. A valid pile that would
  exceed the signed protocol Gold range is rejected as `Malformed` by both
  replay runners without mutating authoritative state.

- Correct replay rejection bookkeeping for malformed primary Gold pickups so
  the rejected transition is recorded and checked like secondary commands.

- Extend stable-reference replay coverage to belt and equipment sources with
  dedicated fixtures and native/managed tests. All three carried-item source
  locations now exercise the same explicit drop intent in parity fixtures.

- Add optional replay command `expected_status` validation for accepted and
  rejected outcomes. The stale stable-item retry fixture now proves native and
  managed runners agree that a consumed source identity cannot be submitted a
  second time.

- Extend replay command expectations with `expected_reject_reason`, beginning
  with cross-language `InvalidTarget` parity for stale stable-item retries.

- Add a cross-language `NotAllowed` stable-belt drop lane so replay parity
  distinguishes source identity failures from non-adjacent destination checks.

- Add the additive `DropItemRequested` protocol intent for stable-reference
  inventory, belt, and equipment-to-world drops. The server keeps the native
  cursor-only `DropHeldItemRequested` compatibility path while validating the
  new source reference atomically, including content-owned Cornerstone anchor
  completion and stale-reference rejection. Godot now preflights the selected
  carried item and routes world clicks through the same intent. A live
  Godot/server fixture now verifies an inventory source is removed and
  materialized as an authoritative world item, while a retry with the stale
  reference is rejected.

- Add a live two-participant Godot/server Lachdanan fixture for shared-safe
  staged dialogue. The fixture composes the shipped Hellfire pack, validates
  an explicit Lachdanan target binding, and confirms both multiplayer
  snapshots and talk commands remain on the item-absent branch until a player
  supplies the Golden Elixir.

- Route native authoritative inventory, belt, and equipment cursor drops
  through the stable-reference `DropItemRequested` intent. Legacy cursor-only
  drops remain unchanged for compatibility mode, and stash selections fail
  closed instead of being misrepresented as held items.

- Make initial staged-dialogue projection safe for shared multiplayer targets.
  Item-gated conversations now advertise the item-absent branch until the
  interacting player submits a validated item-present hand-in, preventing a
  snapshot from exposing another participant's reward branch.

- Project the current authored dialogue message onto staged unique-monster
  targets as soon as the server materializes or restores them. Godot can now
  resolve and submit the dialogue target directly from the initial
  authoritative snapshot, before any local interaction or legacy fallback is
  needed.

- Make staged unique-monster dialogue own combat admission while a matching
  authored stage is pending. Server combat now reads the same dialogue catalog
  as `TalkToQuestRequested`, and the live Godot fixture proves direct attacks
  are rejected even when the legacy completion marker is absent; older packs
  retain the marker fallback.

- Move Snotspill's initial unique-monster conversation into the authored
  staged-dialogue catalog. The server and Godot live fixture now verify the
  event, quest-variable advance, and projected next-message identity under
  schema `1.443.0`, while `1.442.0` remains accepted as the previous additive
  contract.

- Complete the quest-transition object follow-up slice across the protocol
  boundary. Server and Godot validation now accept authored map, projectile,
  and encounter follow-ups on quest-transition objects, with a live Godot
  fixture proving the command acknowledgement, quest completion, and projected
  world-mutation geometry.

- Extend authored object follow-ups to quest-transition objects. Books, levers,
  and similar set-piece controls can now apply a validated server-owned map,
  projectile, or encounter follow-up after their quest transition succeeds;
  schema `1.443.0` is current with `1.442.0` retained for additive
  compatibility.

- Externalize the Halls of the Blind, Gateway of Blood, and Warlord steel-tome
  activation dialogue through authored object metadata. The server now records
  each native quest message and emits it as a server-owned dialogue event;
  native-DUN placement and map mutations remain deferred until their licensed
  geometry is available. schema `1.443.0` is current with `1.442.0` retained
  for additive compatibility.

- Externalize Hellfire Lachdanan's opening and Golden Elixir hand-in through
  paired item-presence dialogue stages. The server now owns the Veil of Steel
  unique reward, placement validation, quest completion, and target defeat;
  schema `1.443.0` is current with `1.442.0` retained for additive
  compatibility.

- Move Zhar's activation conversation and level-14 book reward into the
  authored unique-monster dialogue catalog. The new `NormalItem` stage keeps
  reward placement and quest advancement server-owned; schema `1.443.0` is
  current with `1.442.0` retained for additive compatibility.

- Externalize the Lazarus storyteller opening through authored quest/towner
  stages. Single-player Staff of Lazarus activation and multiplayer unlogged
  dialogue now preserve the native message and progression marker under the
  server-owned path; those rows remain compatible through current schema
  `1.443.0`, with `1.442.0` retained as the previous additive contract.

- Extend authored quest/towner rewards to `Advance` stages and make item
  consumption atomic with reward placement validation. The base Black Mushroom
  healer hand-in now consumes the Brain, advances its native marker, and spawns
  the declared Spectral Elixir entirely through the server-owned path;
  those rows were introduced under schema `1.436.0` and remain compatible
  through the current `1.443.0` content contract.

- Externalize the remaining shipped base quest/towner item stages for Magic
  Rock, Anvil of Fury, Ogden's Sign, and the Butcher. Their server-owned
  activation/completion rows preserve native gates, messages, item consumption,
  and authored rewards; those rows were introduced under schema `1.435.0` and
  remain compatible through the current `1.443.0` content contract.

- Add the additive `CompleteAfterQuest` interaction kind and completion-reward
  flag. Lazarus, Poisoned Water, and Farmer's Orchard completion conversations
  now use validated content rows with server-owned variable gates, follow-on
  activation, reward placement, and one-shot behavior across C#, Godot, and
  native compatibility boundaries.

- Expand the content-owned quest/towner pipeline across shipped Hellfire
  interactions: Grave Matters dialogue, Farmer's Orchard activation (including
  its Rune Bomb reward), and Jersey's Jersey's Grey Suit alternate and Brown
  Suit completion stages now use validated interaction rows instead of the
  legacy turn-in fallback.

- Add an atomic per-session checkpoint containing the exported authoritative
  state and completed command receipts. Fresh hosts prefer this checkpoint and
  retain the separate save/ledger files as compatibility fallbacks, closing
  the process-crash window between state and idempotency publication.

- Serialize authoritative state and command-ledger exports under the command
  server's synchronization boundary. Concurrent commands and autonomous ticks
  can no longer produce a save whose snapshot and durable receipt ledger were
  captured from different authoritative states.

- Expand fresh-host persistence coverage into a four-cycle, multi-command soak:
  every previously accepted client sequence is replayed after each restart,
  receipt identities remain stable, and the next sequence is accepted exactly
  once by the replacement adapter.

- Persist per-session command receipts beside authoritative saves and hydrate
  them before reconnect processing. Retried client sequences now remain
  idempotent across a fresh host process, while receipt allocation resumes
  after the highest persisted server receipt.

- Preserve native Little Girl ordering when Theodore is already in the player's
  inventory: the authored completion stage may run during initial quest state,
  reuses the declared amulet reward, and is projected consistently to Godot and
  the native compatibility client.

- Add live standalone-server coverage for the Hellfire Little Girl's
  pre-activation hand-in metadata, including the Theodore item gate and
  quest-reward reuse flags.

- Externalize the Hellfire Little Girl's opening and active dialogue through
  authored quest/towner stages. New required-item presence metadata keeps those
  itemless branches from intercepting the server-owned Theodore hand-in while
  preserving the native visited-level gate and dialogue messages while keeping
  the existing `1.434.0` content identity backward compatible.

- Add a validated `--starting-level` authoritative-server override for new
  participants. Development and live-client fixtures can now select an
  authored content level explicitly, avoiding accidental interaction with
  the default dungeon encounter while preserving the content-defined default
  when the option is omitted.

- Discard transient simulation events when importing a saved session or
  synchronizing the runtime to a restored tick. Reconnect and live ruleset
  replacement no longer deliver combat or dialogue events from before the
  restored snapshot.

- Restore set-level-anchored native DUN fragments after origins are hydrated,
  including their persisted behavior objects. Snapshot restore now treats an
  already-present matching fragment object as idempotent while still rejecting
  conflicting entity state.

- Make dynamic set-piece origin publication and snapshot rollback transactional;
  failed deferred geometry or object materialization no longer leaves partial
  origins, objects, fragments, or projected mutation state behind. Snapshot
  restore now rolls back the complete captured module state after any late
  validation or materialization failure.

- Add validated set-level-origin anchors to native DUN fragments and their
  behavior-object placements. Rows now defer until a generated set-piece origin
  is published, then compose atomically with existing entity, unique-encounter,
  and coordinate anchor modes; the Diablo content schema is now `1.434.0`
  (schema `1.434.0`)
  while retaining `1.433.0` as the previous additive contract.
- Add deferred set-level-origin anchors to authored world mutations, allowing
  geometry and native layer changes to follow published generated origins.
- Add validated set-piece-origin metadata to object item rewards. Rewards can
  now resolve their drop coordinate from a published set-level origin while
  retaining legacy object-relative behavior across snapshots and protocol.
- Package the existing native Skeleton King and Chamber of Bone transparency
  `.dun` binaries in the authoritative base content pack, with shipped-content
  dimension checks. The Lazarus transparency mapping remains metadata-only
  until its shared `vile1.dun` map bundle is available.
- Author the native transparency layers for Skeleton King's Lair, the Chamber
  of Bone, and Archbishop Lazarus' Lair in `set_level_maps.tsv`. Transparency
  binaries may now be validated and retained while their deferred native map
  and `.til`/`.sol` geometry inputs are still unavailable; complete bundles
  continue to project the layer onto authoritative set-level geometry.
- Add template-only authoritative behavior rows for the native Halls of the
  Blind book, Gateway of Blood book, and Warlord steel tome. The rows carry
  stable object identities and initial quest transitions so future native-DUN
  placement can reuse one server contract; exact generated placement, rewards,
  and map replacement remain deferred until their native geometry inputs land.
- Run spawned live authoritative servers from the same build configuration as
  the Godot protocol test assembly, preventing stale Debug binaries from
  making Release boundary tests intermittently miss gameplay projections.
- Refresh all live Godot overlay fixtures to the active `1.434.0` content
  schema; previous additive-schema compatibility remains covered by the
  module and Data tests. The content-pack guide continues to document the
  exact-schema composition rule.
- Refresh the native Lua-boundary characterization for the centralized
  `ShouldInitializeLegacyLua` lifecycle policy, covering both guarded startup
  and shutdown while retaining the authoritative debug-console exclusion.
- Make multiplayer replay initial snapshots complete before command execution
  and add a secondary-portal persistence round-trip that restores both
  participants and visited-level history in reverse reconnect order.
- Initialize the generic entity-pool free-list at construction, make repeated
  releases harmless, and correctly pack dense-pool elements after deletion;
  restore the referenced entity-pool contract and modernization guides with
  focused native regression tests.
- Make Godot local movement prediction collision-aware using only authoritative
  geometry, monster occupancy, and remote-player occupancy already present in
  the latest snapshot; unknown collision data remains non-blocking for
  compatibility.
- Route secondary-participant portal transitions through the shared replay
  fixture gate, preserving the primary session while updating the secondary
  level and position in both native and managed runners.
- Extend the repository-level content-contract check to derive the active and
  previous additive schema from the Diablo module, validate first-party
  manifests and inline Godot overlay fixtures, and catch stale contract text
  in the content and migration docs for schema `1.434.0`.
- Require active spell content to provide the native projectile ordering
  patterns used by authoritative Charged Bolt, Fireball, Elemental, Flash,
  Guardian, and Resurrection handlers; direct compatibility catalogs retain
  embedded native defaults.
- Require active native missile mappings to provide both the external missile
  namespace and sprite catalog so missing reference validation fails closed.
- Require the active native missile namespace to retain all 68 native `misdat`
  identities during production activation; direct compatibility catalogs remain
  permissive until explicitly validated.
- Require the active native missile sprite catalog to retain all 46 base
  `missile_sprites.tsv` identities while permitting Hellfire overlay additions.
- Restore omitted randomized armor values from the authoritative item seed
  across snapshot, store, world-item, and equipment paths; compatibility states
  without a seed remain unresolved instead of receiving a guessed roll.
- Route authoritative item, spawn, quest, spell-book, and unique-item random
  consumers through the engine LCG primitive, retaining Diablo random wrappers
  and overloads only as source-compatible shims and adding sequence-parity
  coverage.
- Extend live snapshot identity propagation to both clients, including
  reconnecting with refreshed identity, fail-closed rejection of incomplete
  paired metadata, and focused handshake coverage.
- Add additive snapshot content/ruleset identity metadata. The generic host
  stamps live snapshots after ruleset replacement and the Godot protocol client
  updates its retained identity, without changing canonical gameplay hashes or
  save bytes.
- Enforce the documented complete native spell namespace during production
  content activation, while retaining partial catalogs for compatibility-only
  callers.
- Expose active ruleset identity and retired-generation counts through the
  generic host and operator `status` command, making live replacement and safe
  retirement observable without coupling the host to Diablo.
- Add optional validated `object_presentations.tsv` sidecars so content packs
  can author activated-object visibility and native animation frames without
  changing the legacy `objects.tsv` header or breaking older overlays.
- Bump the Diablo content schema to `1.431.0`; `1.430.0` remains accepted as
  the immediately previous additive contract for existing manifests.
- Share the engine-owned native direction displacement sequence and sector
  selection with Diablo combat, removing a duplicate compatibility table while
  preserving direction indices and extreme-coordinate behavior.
- Extract native rotated-grid projectile velocity construction into the
  engine-neutral assembly while preserving Diablo's fixed-point scaling.
- Extract deterministic procedural level lattice/hash generation into the
  engine-neutral assembly while retaining Diablo-owned level identity and
  world construction.
- Extract native radial projectile fan-out ordering into the engine-neutral
  assembly while preserving mirrored quarter-ring truncation.
- Extract stable 32-bit mixing and aligned walkable-footprint selection into
  the engine-neutral assembly; Diablo set-piece origins retain their existing
  row-major candidate order and seed selection.
- Extract native alternating and turn-pair direction candidate ordering into
  the engine-neutral assembly; Diablo movement AI retains its RandomWalk,
  fear, stealth, counselor, and minion fallback sequences.
- Extract native-compatible Fisher–Yates ordering into the engine-neutral
  assembly; Diablo monster placement preserves its random stream and uses the
  shared stable hash for runtime seeds.
- Complete the direction-ordering migration by moving native RoundWalk,
  Snake lateral, clockwise fallback, and adjacent-step mapping behind the
  engine-neutral direction primitive.
- Generalize engine-owned bounded grid navigation to caller-selected goal cells
  and expose its stable neighbor order; Diablo player-minion pursuit now uses
  the shared pathfinder instead of a private breadth-first implementation.
- Move native-compatible inventory placement ordering into the engine-neutral
  domain inventory layout; Diablo store sorting, purchases, and equipment
  displacement now share the same deterministic footprint search, including
  first-fit handling for validated external footprints.
- Route authoritative Diablo world and combat crawl searches through the
  engine-owned deterministic crawl primitive; the Diablo crawl type remains
  only as a source-compatible facade for existing callers.
- Extend the engine-owned entity-ID allocator with deterministic collision
  skipping and migrate Diablo combat/world allocation ranges to it, preserving
  native range partitioning and non-zero exhaustion behavior.
- Migrate native DUN monster hydration and generated unique-monster pack
  materialization to the shared engine allocator, preserving authored IDs,
  generated drop reservations, and deterministic collision handling.
- Complete the authoritative distance migration: Diablo targeting, movement,
  AI, projectile, snapshot, and world validation now call the engine metrics
  primitive directly while removing the redundant combat-target facade.
- Complete the authoritative fixed-point migration: Diablo hit-point, damage,
  healing, and snapshot paths now call the engine Q6 primitive directly;
  `DiabloFixedPointQ6` remains only as a source-compatible facade for existing
  callers.
- Extract native-compatible bounded grid crawl ordering into the engine-neutral
  assembly; Diablo retains its source-compatible crawl facade while teleport,
  portal, resurrection, drop, and multi-target searches share overflow-safe
  nearest-cell behavior.
- Extract native-compatible eight-way direction math into the engine-neutral
  assembly; Diablo retains its public direction enum while authoritative
  callers use the engine primitive directly for overflow-safe coordinate-to-
  facing selection, rotation, and adjacent-cell displacement.
- Preserve authoritative object presentation lifecycle metadata across content,
  snapshots, saves, hashes, protocol projections, native compatibility, and
  Godot visibility. Objects remain one-shot server interactions while authored
  templates may stay visible and select an activated animation frame; omitted
  metadata retains legacy hide behavior.
- Extract deterministic overflow-safe Bresenham line traversal into the
  engine-neutral assembly; authoritative movement and missile line-of-sight
  checks now share one endpoint and interior-cell contract, while malformed
  extreme-coordinate requests fail closed before an unbounded walk.
- Extract overflow-safe Chebyshev grid-distance metrics into the
  engine-neutral assembly; bounded navigation and Diablo's authoritative
  targeting callers now share the same native-compatible extreme-coordinate
  behavior while the Diablo entry point remains source-compatible.
- Extract native-compatible signed 26.6 fixed-point arithmetic into the
  engine-neutral assembly; the Diablo module retains its source-compatible
  `DiabloFixedPointQ6` facade while other authoritative modules can reuse the
  same truncation, ceiling, and checked whole-value conversion rules.
- Extract stable non-zero entity-ID allocation into the engine-neutral
  assembly; the server host retains its source-compatible allocator facade
  while authoritative runtimes can share the deterministic primitive.
- Extract the native-compatible deterministic LCG into the engine-neutral
  assembly; the Diablo module retains its source-compatible `DiabloRandom`
  facade while other authoritative modules can reuse the same parity primitive.
  Diablo combat, world, store, simulation, spawn, and quest code now consumes
  `DeterministicLegacyRandom` directly.
- Route Server host ID allocation and Diablo protocol/replay projections through
  engine-owned deterministic primitives directly; the existing host and Diablo
  facades remain available for compatibility callers and fixtures.
- Refresh the multiplayer death-suppression replay checkpoint to the current
  canonical snapshot hash; all 76 replay fixture tests now validate cleanly.
- Extend widened authoritative coordinate arithmetic through monster aggro,
  retreat, scavenger/fallen radius, minion, projectile, and native-vision
  targeting paths; malformed extreme coordinates no longer wrap during AI
  selection or projectile area resolution.
- Guard one-cell player movement and Telekinesis knockback offsets against
  integer-boundary wraparound, rejecting or accepting without mutation when a
  malformed state cannot represent the requested destination.
- Centralize authoritative Chebyshev range checks on widened coordinate
  arithmetic across monster unity/spawn validation, world interactions, and
  combat cast/target admission; extreme wrapped coordinates now fail closed
  without mutating server state.
- Clarify Hellfire migration status: the monster-type catalog is complete,
  while encounter placement, world behavior, map synthesis, and unported data
  remain follow-up work.
- Reject out-of-level raw damage-cast coordinates before distance arithmetic,
  so malformed client spell targets fail closed during authoritative admission.
- Extend the raw cast-coordinate boundary to Town Portal and Golem range checks
  and spawn searches, covering special casts outside the shared damage-target
  path.
- Widen Godot monster movement deltas during snapshot validation so malformed
  coordinates fail as invalid snapshots instead of overflowing first.
- Correct the shipped Etherealize native spell identity from `ItemRepair` (26)
  to the native `Etherealize` ID (25), including catalog fixtures.
- Cross-check every shipped external spell behavior row against the native spell
  identity namespace during content verification.
- Widen Godot interaction, quest/towner selection, visibility, and monster
  interpolation coordinate arithmetic so extreme malformed snapshots cannot
  overflow or throw at the presentation protocol boundary.
- Make engine-neutral bounded navigation skip integer-boundary neighbor
  overflow and widen adjacency checks, keeping caller-owned grid searches
  deterministic and fail-closed for extreme coordinates.
- Make shared bounded crawl searches skip overflowing candidate coordinates,
  keeping raw client target cells fail-closed across authoritative placement
  and target-search paths.
- Reject out-of-level held-item drop targets before adjacency arithmetic, so
  malformed client coordinates cannot overflow the authoritative range check.
- Make authoritative projected and fallback line-of-sight checks fail closed
  for out-of-level endpoints before coordinate arithmetic, preventing malformed
  targets from overflowing deltas or forcing an unbounded grid traversal.
- Make authoritative warp-distance lookup overflow-safe for extreme caller
  coordinates.
- Reject duplicate authored warp source triggers so content cannot define
  ambiguous level transitions that differ only by warp ID.
- Reject partial declared set-level native map bundles instead of silently
  ignoring supplied `.dun` assets when `.til`/`.sol` data is absent.
- Apply external quest route activation when an authoritative player enters a
  materialized active set-level runtime map, preserving native set-level entry
  markers without treating pre-map variants as visited gameplay levels.
- Enforce the same active set-level quest-entry rule during simulation ticks and
  snapshot restoration, closing lifecycle paths that bypass world commands.
- Share regular and materialized set-level runtime-ID rules across the protocol,
  generic host, Diablo mapper, Godot model, and native compatibility boundary;
  native presentation now decodes runtime IDs to `_setlevels`, while visited
  flags remain limited to ordinary native levels.
- Keep the same regular-only visited-level invariant in the Diablo player state
  and snapshot capture/restore paths, so set-level runtime identities survive
  persistence without being mis-recorded as `_pLvlVisited` entries.
- Clarify that geometry-backed visibility activation is shipped, while broader
  activation timing, interpolation, rally behavior, and soak coverage remain
  follow-up work.
- Reject duplicate visited-level IDs at the generic host boundary, matching
  the existing Godot and native compatibility validation.
- Bound active world-mutation metadata to 4,096 entries consistently across
  the generic host, Godot, and native compatibility boundaries.
- Reject non-native player level IDs before native compatibility application
  narrows them into the legacy 8-bit level field.
- Reject authoritative player snapshots with undefined direction, combat-mode,
  ability-kind, or ability-targeting enum values at the generic host boundary.
- Reject player snapshots whose current level ID is outside the native 25-level
  range, including when no visited-level list is supplied.
- Reject server handshakes that declare an unspecified or unknown game mode
  before the Godot client enters a connected state.
- Validate command acknowledgements before the Godot transport or model mutates
  pending-command and presentation state; malformed acknowledgements now fail
  closed without dropping retryable commands.
- Preserve rejection reasons carried by valid duplicate command acknowledgements.
- Reject legacy whole-unit damage values that would overflow native Q6
  conversion across the C# host, Godot, and native event validators.
- Reject authoritative Diablo victory ranks above the native byte range across
  C#, Godot, generic-host, and native compatibility boundaries.
- Validate requested character names at the generic handshake boundary using
  the shared printable wire-shape rule before participant initialization.
- Make the Godot client reject an error or unexpected payload in the required
  initial-snapshot handshake frame instead of entering a false connected state.
- Reject authoritative victory-event Diablo ranks above the native byte range
  across the C# mapper, generic host, Godot, and native compatibility validators.
- Reject oversized authoritative command batches at the generic host boundary
  before they reach command admission or game execution.
- Make the Godot transport own cloned queued commands, preventing caller
  mutation from changing commands retained for send and retry.
- Make the Godot protocol model own cloned validated snapshots and event
  history entries, preventing post-apply mutation through transport objects.
- Reconcile native `_pLvlChanging` state from authoritative `PM_NEWLVL`
  snapshots for local and remote player presentation, preventing stale level
  transition gates from leaking across snapshots.
- Rebind native compatibility-pool item and object mappings when a level reload
  reuses a slot, preventing stale entity identities from deleting newly
  projected presentation state; add focused item/object reuse coverage.
- Remove unclaimed native compatibility-pool world items and objects after
  authoritative snapshot application so omitted legacy level entities cannot
  remain as connected-client ghosts; add focused item/object pool regression
  coverage.
- Advance Godot monster and remote-player presentation on a bounded fractional
  server-tick clock between snapshots, preserving discrete authoritative
  occupancy while timed walks finish smoothly at render cadence.
- Keep the fractional presentation clock in the engine-independent Godot
  protocol projection layer with direct reset, rate, and horizon coverage.
- Cover a remote-player walk across a coarse snapshot boundary by composing
  the presentation clock with the authoritative action-tick projection.
- Advance native remote-player presentation animations from the connected game
  loop without re-entering legacy gameplay simulation.
- Avoid double-advancing idle remote-player animations when a snapshot follows
  presentation ticks that were already rendered locally.
- Drive the local native player's authoritative action animation from the
  server-backed presentation loop while keeping `ProcessPlayers` disabled.
- Reset local native presentation animation state when the authoritative level
  changes.
- Refresh the local native sprite family and animation data when authoritative
  equipment or class state changes, without recalculating gameplay locally.
- Rebuild native stash scroll-source state on every authoritative player
  snapshot so closed or changed stashes cannot leave stale spell sources.
- Preserve inventory and belt scroll sources while reconciling stash-derived
  scroll availability.
- Reconcile the native local block-animation admission flag from authoritative
  combat modes so stale legacy state cannot survive a server-owned transition.
- Use the native `PM_BLOCK` enum for remote-player block presentation and verify
  the flag clears when the authoritative action returns to standing.
- Recompute native remote-player life and mana percentages from each authoritative
  snapshot so HUD and party presentation do not retain stale resource bars.
- Rebuild remote-player staff and scroll spell masks from authoritative item state,
  clearing stale spell entries when equipment or inventory changes.
- Rebuild the local authoritative player's staff-spell mask from server-owned
  equipment while preserving legacy spell selection behavior offline.
- Reset the Godot presentation clock across authoritative local-level changes
  so timed movement from a prior level cannot bleed into the new view.
- Validate authoritative monster movement timing at the generic host boundary,
  matching the Godot, Diablo adapter, and native compatibility checks.
- Carry authoritative monster movement start/effect ticks through snapshots,
  saves, the shared protocol, Godot interpolation, and native compatibility
  projection while retaining zero-valued legacy snapshot compatibility.
- Share the native `DoVision` ray table between the authoritative server and
  Godot protocol client, including radius trimming and diagonal corner rules.
- Align native authoritative projection tests with the fail-closed boundary for
  invalid projectile visuals and out-of-bounds moving destinations.
- Apply the authoritative geometry-backed monster vision boundary to Godot
  presentation, including radius, line-of-sight, and diagonal corner checks.
- Honor native DUN `BlockLight` piece metadata in geometry-backed monster
  visibility, including diagonal corner occlusion.
- Carry derived native vision-blocked cells through authoritative level geometry
  so Godot can reproduce DUN `BlockLight` visibility without native piece metadata.
- Project native equipment-derived player light radius through the shared player
  snapshot and use it consistently for server activation and Godot presentation.
- Preserve native diagonal corner occlusion when geometry-backed monster
  visibility follows the server's equipment-derived light-radius boundary.
- Extend geometry-backed monster visibility activation with the native default
  light radius and equipment modifiers, while preserving geometry-free
  compatibility behavior and documenting the remaining exact-ray follow-up.
- Add the first server-side visibility activation gate for geometry-backed
  worlds: occluded monsters remain inactive when connected players are present,
  while geometry-free compatibility fixtures retain their legacy behavior.
- Preserve explicit native SkeletonAi compatibility identities (`SkeletonMelee`,
  `Snotspill`, and `Warlord`) when a generic monster catalog row is present,
  while retaining catalog-selected precedence for complete controllers such as
  Gargoyle.
- Align generated unique-pack placement with native `PlaceGroup` retry and
  transparency-region rules while retaining deterministic server occupancy and
  entity allocation.
- Align generated native unique-pack minions with `PlaceGroup` initialization:
  minion health is restored to the native doubled maximum and minions inherit
  the leader's intelligence for authoritative AI decisions.
- Reject malformed directional projectile metadata at the native authoritative
  snapshot boundary before it reaches presentation code.
- Reject the same malformed directional metadata in the C# server adapter
  before protocol serialization.
- Apply the same directional projectile metadata validation at the Godot
  protocol boundary.
- Reset Godot projectile interpolation across authoritative level, phase, and
  presentation-identity changes.
- Extend the native protocol-free world projection guard to reject malformed
  projectile lifecycle, visual-ID, and directional metadata.
- Reject malformed or misplaced native missile identities in the same native
  protocol-free projection path.
- Enforce native tile-coordinate and supplied fine-grid geometry bounds in the
  native protocol-free world projection before compatibility presentation.
- Align native protocol-free projectile validation with the shared arrow
  spell-less exception and damage-type range.
- Enforce native monster group-unity leader invariants in the protocol-free
  world projection before compatibility presentation.
- Externalize native 1–50 character experience thresholds, wire active
  simulations to server-owned progression data, and add source parity coverage.
- Externalize all 68 native `misdat.tsv` missile identities, retain five
  explicit monster-color compatibility aliases, and validate every active
  monster missile mapping against the shared namespace.
- Externalize all 46 native `missile_sprites.tsv` definitions, including
  dimensions and 16-direction frame timing, for presentation-owned rendering.
- Externalize the 13 Hellfire-only native missile sprite rows, including
  colored flares, Hork Spawn, Rune, and Reflect, through the validated overlay.
- Validate every non-alias native missile graphic against the active sprite
  catalog during content activation.
- Restrict native missile-sprite flags to the validated `MonsterOwned` and
  `NotAnimated` source vocabulary.
- Carry validated content-owned native missile identities through the shared
  projectile protocol, with Godot presentation precedence and legacy fallback.
- Carry the same native missile identity through the native compatibility
  projection, with fail-closed validation and enum/behavior fallback.
- Advance authoritative monster walk animations in the connected native loop
  without re-enabling legacy monster simulation.
- Advance authoritative missile sprite animations in the connected native loop
  without re-enabling legacy projectile simulation, collision, or lifetime
  mutation.
- Add exhaustive source coverage for all 36 native spell-data identities in
  the 52-entry normalized native spell namespace.
- Add exhaustive parity coverage for the normalized item-generation rules and
  their native `item_generation.tsv` source row.
- Add exhaustive parity coverage for all five normalized level-generation rules
  and their native `level_generation.tsv` source rows.
- Add exhaustive parity coverage for the six normalized class attribute tables,
  including direct native values and Q6-converted life/mana tuning.
- Add a native-source parity guard for all six class starting-loadout metadata
  rows, covering ability identity, starting spell, spell level, and gold.
- Add exhaustive parity coverage for all 27 exposed animation fields across
  the six normalized base classes and their native animation tables.
- Guard the retained 168-row legacy `itemdat.tsv` compatibility fallback
  field-for-field against the inherited native item table.
- Restore native standing-animation frame and timing metadata for all 26
  Hellfire-only monster types and guard the complete 138-row variant catalog
  against `monstdat.tsv` core metadata.
- Add exhaustive Hellfire affix parity coverage against the native variant
  prefix and suffix tables, guarding all 86 prefixes and 98 suffixes after
  base-pack composition and overlay replacement.
- Add exhaustive base-content affix parity coverage against the inherited native
  prefix and suffix tables, guarding every field and row in the external pack.
- Restore The Undead Crown's native `SETAC 8–8` unique power in the Hellfire
  overlay and guard the complete native unique-power projection against drift.
- Restore the 19 remaining native Hellfire unique cursor projections through
  the validated shared cursor-symbol catalog.
- Guard the complete 52-entry native spell namespace, including unused legacy
  identities, service capabilities, and rune aliases that intentionally do not
  create additional cast-behavior rows.
- Characterize the complete Hellfire unique catalog's native blank numeric
  power fields: normalized flag-style rows use explicit `0,0` values while
  retaining all populated native ranges.
- Correct the external Hellfire monster catalog's native armor-class values
  and Fallen-family special-damage ranges against `monstdat.tsv`, covering
  the 25 affected armor rows and four non-damaging special attacks.
- Correct the Hellfire `piercing` suffix range to its native `1–1` target-
  accuracy value in the external overlay.
- Externalize Hellfire's native `The Grizzly` unique item with its validated
  five-power definition through the unique-item overlay.
- Externalize Hellfire's native affix eligibility rows, including staff-capable
  prefixes and suffixes, through deterministic composite-key overlays.
- Externalize Hellfire's native `piercing`, `puncturing`, and `bashing`
  target-accuracy suffix ranges through the validated item-suffix overlay.
- Externalize Hellfire's `devastation`, `decay`, and `peril` weapon/staff/bow
  suffixes through the validated item-suffix overlay.
- Externalize Hellfire's distinct `Doppelganger's` weapon/staff prefix through
  the validated item-prefix overlay.
- Externalize Hellfire's distinct `Jester's` weapon prefix through the
  validated item-prefix overlay.
- Externalize Hellfire's native `Crystalline` weapon prefix through the
  validated Hellfire content overlay.
- Add native Hellfire `CRYSTALLINE` damage and durability semantics to
  authoritative affix and unique-item generation.
- Extend normalized and legacy unique-item power handling with native
  `CHARGES` multiplier semantics while preserving depleted current charges.
- Extend normalized and legacy unique-item power handling with native
  `DUR_CURSE` minimum-durability semantics and focused coverage.
- Validate and bound Godot authoritative event batches while retaining
  compatibility with zero-ID untargeted events.
- Apply the same event-batch validation to generic TCP host output before
  adapter events cross the shared protocol boundary.
- Apply matching event-batch validation before native compatibility-client
  event application.
- Add a loopback TCP regression test proving malformed adapter events are
  rejected as protocol errors before wire serialization.
- Align native snapshot ingress with Godot by rejecting malformed non-empty
  state-hash strings while retaining omitted-hash compatibility.
- Validate non-empty authoritative snapshot hashes at the Godot protocol
  boundary while preserving legacy snapshots that omit the additive hash.
- Validate non-empty authoritative snapshot hashes at the generic TCP host
  boundary before adapter snapshots are serialized, preserving omitted-hash
  compatibility.
- Reject zero or duplicate projected entity IDs at the generic TCP host
  boundary, matching the Godot and native compatibility clients.
- Validate snapshot difficulty, portal-route state, and active world-mutation
  identities at the generic TCP host boundary.
- Align native snapshot projection with Godot by rejecting malformed player-name
  shapes while retaining omitted-name compatibility.
- Align generic TCP host snapshot emission with the shared player-name shape,
  rejecting malformed names before wire serialization.
- Centralize the optional authoritative player-name wire shape in the shared
  protocol assembly for host and Godot boundaries while preserving the Diablo
  module's independent native validation.
- Make authoritative save and world-seed writes durable before atomic
  replacement, isolate concurrent temporary files, and clean up failed writes;
  add focused coverage for temporary-artifact cleanup.
- Make world-seed initialization first-writer-wins across concurrent store
  instances and add a concurrent initialization regression test.
- Extend TCP restart recovery coverage across four fresh host/adapter cycles,
  preserving the resume token, player identity, character name, and canonical
  snapshot hash while confirming each retired adapter is disposed.
- Coordinate save-store instances by canonical path and add concurrent
  coverage proving atomic replacement leaves one complete session payload and
  no temporary artifacts.
- Persist initialized or restored TCP sessions immediately after handshake so
  idle participants remain recoverable across process restart.
- Add live Godot coverage that restarts the server process and restores two
  participants through resume tokens, preserving identity and moved position.
- Give validated monster controller tuning precedence over legacy AI labels,
  restoring the authored Gargoyle, Skeleton King, and Rhino behavior paths.
- Preserve authored unique-monster health percentages when materializing
  single-player summons, avoiding a second native health-halving pass.
- Harden the protocol-free native world projection boundary by rejecting
  zero/duplicate monster identities, invalid native directions, and
  non-adjacent authoritative movement targets before publishing state; reject
  duplicate nonzero identities across projected entity collections as well.
- Carry the authoritative snapshot tick through the native world projection
  and ignore older complete world projections at the presentation boundary,
  preventing delayed server state from regressing native entity visuals while
  preserving unversioned compatibility callers.
- Map authoritative monster movement directions to the native renderer's
  movement modes and walk animation, clear stale movement modes when snapshots
  become stationary, and share the direction classification with native walks.
- Use authoritative monster future positions for Godot presentation
  interpolation while retaining the occupied-tile fallback for stationary or
  legacy snapshots.
- Align native snapshot validation with the authored unique-pack contract by
  accepting leashed members whose leader is a unique encounter.
- Add bounded Godot monster interpolation reset detection for level changes and
  large authoritative relocations while preserving one-tile movement smoothing.
- Allow validated overlays to introduce optional tables that are absent from
  earlier packs, enabling external pack-only contracts such as
  `unique_monster_packs.tsv` without changing the base table set.
- Project monster snapshots now carry additive group-unity state (leader
  identity/relation, pack size, and active ticks) through the shared protocol.
  Server mapping, canonical hashing, Godot validation, and the native
  compatibility projection preserve the state while default values remain
  compatible with legacy snapshots.
- Add the optional `unique_monster_packs.tsv` content contract for generated
  unique encounters. Validated independent and leashed packs now materialize
  deterministic minion identities and reserved-cell-aware positions through
  the authoritative spawn planner.
- Preserve authored unique-monster pack relationships across simulation ticks
  and reserve generated monster loot IDs so pack materialization cannot collide
  with the leader's drop or another authoritative entity.
- Preserve native `FollowTheLeader` activation timing for authoritative leashed
  packs, including both leader-to-member and member-to-leader propagation and
  snapshot round-trip coverage.
- Enforce native leashed-member movement admission alongside leader admission:
  a member's candidate cell must remain strictly within four tiles of its
  leader, with focused boundary coverage for three- and four-tile moves.
- Enforce native leader movement admission for authoritative leashed packs:
  leaders cannot retain a movement that leaves the validated pack-size count
  or any member outside the strict four-tile envelope; focused coverage covers
  both distance and count drift.
- Add authoritative monster pack leader/relation state with validated
  leashed/separated transitions, native line-of-sight separation, strict
  four-tile reunion, snapshot preservation, and focused autonomous-tick
  coverage; generated pack placement and active-tick leader following remain
  explicit follow-up seams.
- Record the current 1,828/1,839 Debug Diablo-suite verification result and
  enumerate its remaining characterized failures so the historical release
  baseline is not mistaken for current green coverage.
- Treat explicit runtime AI identities as authoritative for all unique
  encounters while preserving alternate-AI catalog precedence for non-unique
  compatibility targets.
- Prefer explicit runtime AI identities when resolving native monster missiles,
  preventing aliased unique encounters from inheriting generic projectiles or
  movement branches; add catalog-backed Counselor alias coverage.
- Preserve explicit Warlord, Snotspill, and Fallen native AI identities when a
  unique encounter inherits a generic monster-type catalog row; add a
  catalog-backed Warlord SkeletonAi regression test.
- Preserve Counselor recovery-delay and RNG handling for seeded Zhar and
  Lazarus Fireball aliases, with focused coverage for both native identities.

- Added an explicit Lightning Wall projectile visual identity so shared
  FireWallSegment behavior is rendered as lightning when its authoritative
  damage channel is Lightning, preserving native and Godot presentation parity.
- Preserved the same Lightning Wall distinction in the native behavior fallback
  used when an older server omits the additive
`native_visual` field.
- Added the native behavior fallback for Nova Ball so older snapshots that omit
  the additive visual identity still render the server-owned projectile.
- Updated Godot's behavior fallback to distinguish Lightning Wall segments by
  their authoritative damage channel when older snapshots omit presentation metadata.
- Corrected the replay-gate documentation to report the current 81 checked-in
  JSON fixtures and 107 native registered tests, including parser and validation
  cases that do not add a fixture file.
- Added a shared multiplayer MonsterArrow volley replay fixture proving two
  simultaneous server-owned projectiles route deterministic impacts to the
  primary and secondary participants in both runners.
- Added managed save/restore coverage proving both participant saves preserve
  the volley trajectory, canonical multiplayer hash, and deterministic removal
  after replacement-runtime continuation.
- Added a real TCP reconnect fixture combining server-owned store state with a
  second participant, preserving complete participant ordering and the
  canonical snapshot hash after resume.
- Strengthened that reconnect fixture to verify requested character names are
  retained for both participants through the live authoritative initializer.
- Added live Godot protocol coverage for two participants, a server-owned store
  purchase, and reconnect state/name preservation through the client boundary.
- Added live Godot coverage for an externally declared memorized Reflect spell,
  including authoritative charge creation and mana consumption.
- Added live Godot coverage for an externally declared memorized Golem spell,
  including server-owned minion creation and owner identity projection.
- Added live Godot coverage for externally declared memorized Immolation and
  Nova casts, while keeping native 36-projectile fan-out and presentation
  assertions in deterministic Diablo combat tests rather than relying on
  transient live snapshots.
- Stabilized live Heal Other and Resurrection fixtures by giving dungeon
  participants enough Q6 life to survive until their intended interaction.
- Added live Godot coverage for an externally declared memorized Teleport,
  confirming server-owned destination resolution, movement, and mana use.
- Added live Godot reconnect coverage proving a queued store purchase retries
  after reconnect and is applied once by the authoritative server.
- Added a distinct Godot presentation family for server-owned player minions,
  rendering their authoritative ownership cue separately from hostile monsters.
- Added focused content-runtime coverage rejecting rooted, drive-qualified, and
  directory-traversing binary asset paths in external packs.
- Content-pack directory loading now rejects symbolic-link and junction entries
  before importing external tables or native binary assets.
- Pack roots themselves are now checked for symbolic links and junctions before
  any directory contents are enumerated.
- Table and native-asset paths now use case-insensitive identity during pack
  validation and composition, matching native Windows path semantics.
- Updated live Godot combat fixtures to select the authored base monster by
  authoritative entity identity when generated unique encounters are also
  projected, and refreshed the charged-source RNG checkpoint for that
  expanded encounter state.
- Added live Godot coverage for an externally declared memorized Telekinesis
  spell, confirming server-owned target knockback and mana consumption.
- Added live Godot coverage for an externally declared memorized Ring of Fire
  spell, proving that server-owned fire-wall segments and presentation metadata
  cross the protocol boundary while generated geometry can occlude blocked cells.
- Added live Godot coverage for an externally declared memorized Stone Curse
  spell, proving that tile-targeted petrification and mana consumption remain
  authoritative across the protocol boundary.
- Added live Godot coverage for an externally declared memorized Phasing spell,
  proving that server-owned candidate selection, relocation, and mana use cross
  the protocol boundary.
- Added live Godot coverage for an externally declared memorized Mana Shield
  spell, confirming server-owned activation and mana consumption.
- Added live Godot coverage for an externally declared memorized Berserk spell,
  confirming server-owned target mutation and mana consumption.
- Refreshed Godot live content-overlay fixtures to the shipped schema 1.430.0
  and current stores-table columns so external-pack tests remain runnable after
  schema evolution.
- Added a shared Necromorb MonsterMagic replay fixture proving the alternate
  Lich-family missile alias admission and canonical projectile state in both
  runners.
- Added a shared Psychorb MonsterMagic replay fixture proving the alternate
  Lich-family missile alias admission and canonical projectile state in both
  runners.
- Added a shared ArchLich MonsterMagic replay fixture proving the alternate
  Lich-family missile alias admission and canonical projectile state in both
  runners.
- Added a shared BoneDemon MonsterMagic replay fixture proving the native
  Lich-family alias admission and canonical projectile state in both runners.
- Added a shared Lich MonsterMagic secondary-player block replay fixture that
  preserves native damage prevention, block mode, and timing in both runners.
- Added a shared multiplayer SkeletonRanged MonsterArrow replay fixture that
  proves the native and C# runners choose the nearest participant and preserve
  the canonical target identity.
- Added a shared multiplayer Lich MonsterMagic replay fixture that proves the
  native and C# runners choose the nearest participant and preserve the
  canonical target identity.
- Added a shared multiplayer Succubus BloodStar replay fixture that proves the
  native and C# runners choose the nearest participant and preserve the
  canonical target identity.
- Added focused save/restore coverage for Hork Spawn expiry materialization,
  retaining the spawned monster identity, position, hit points, and consumed
  projectile.
- Fixed authoritative save exports to retain the current simulation tick and
  save imports to resume the runtime clock at that tick; active Lich
  MonsterMagic projectile trajectory and post-restore continuation are now
  covered by a focused test.
- Added focused two-participant session-save recovery coverage, preserving
  self-first entity ordering and the canonical multiplayer snapshot hash after
  importing both participant saves into a replacement simulation.
- Added focused autonomous multi-monster save/restore coverage, preserving
  ordered monster identities, canonical snapshot state, and deterministic
  continuation on the next authoritative tick.
- Reconciled the replay-fixture backlog with current coverage, removing the
  already-tested HorkDemon delay/summon and MonsterMagic hit/block priorities
  and retaining the remaining AI, multi-actor, reload, Hellfire, and ordering
  gaps.
- Removed cursor-held pickup overflow from the replay backlog because the
  shared `gameplay-gold-pickup-overflow.json` fixture already covers it.
- Added a shared multiplayer HorkDemon replay fixture that proves the native
  and C# runners choose the nearest participant and preserve the canonical
  target identity.
- Godot and native snapshot boundaries now reject duplicate materialized
  procedural level IDs, matching the server's additive field-19 validation.
- Procedural encounter materialization is now persisted through additive
  snapshot/protocol field 19, so defeated generated encounters do not respawn
  after save/restart; canonical snapshot hashing includes the state.
- Snapshot restoration now rebuilds transient procedural-spawn tracking and
  the generated monster identity cursor from the restored world, so replaying
  an earlier snapshot can materialize a later level encounter again with the
  same authoritative identities.
- Snapshot restoration now republishes deferred native-DUN fragments after
  restored unique encounters and fragment objects are hydrated, preserving
  anchored geometry across process restart and replacement simulations.
- Deferred native-DUN fragments now apply after native monster-layer targets
  materialize, so unique encounters created from mapped DUN monster cells can
  publish their anchored geometry and objects in the same simulation activation.
- Native DUN fragments now validate every target level during content
  activation, including unique-anchored fragments whose encounter anchor is
  still deferred; impossible level identities fail closed early.
- Set-level and native-DUN object template references now validate during
  content activation, even when their geometry remains deferred; unknown or
  mismatched templates fail closed before a later map publication.
- Ordinary-level native DUN object mappings now fail closed during content
  activation when their target level is absent from the composed world;
  deferred set-level mappings remain eligible for later geometry publication.
- Complete native set-level map bundles now parse and cross-validate during
  content preflight even before a world is available; malformed geometry and
  invalid active-map spawn points fail closed rather than remaining deferred.
- Ordinary native DUN levels now parse and cross-validate their complete
  optional `.til`/`.sol` collision bundle during content activation, so malformed
  level geometry fails closed before publication.
- Deferred native DUN fragments now parse and cross-validate their optional
  `.til`/`.sol` collision bundle during content activation, before a unique
  encounter can publish the fragment; malformed deferred geometry fails closed
  at pack load.
- Authoritative monster-arrow impacts now convert physical damage into native
  Q6 before player resolution and publish the exact applied value through
  `DamageEvent.amount_q6`; legacy whole-life fixtures retain their prior
  compatibility projection.
- Single-player Fallen fear retreats now consume the native `RandomWalk` coin
  draws before choosing a retreat cell; seed-zero multiplayer targets retain
  the deterministic compatibility sweep.
- Fallen targets now retain native stand-animation frame/tick state and use
  the animation-boundary `FlipCoin(4)` admission for their healing rally;
  nearby Fallen receive the server-owned attack timer and can pursue players
  beyond their normal aggro range through native `RandomWalk` ordering.
- Initialized single-player SkeletonMelee/Fallen targets now consume the global
  native stream for SkeletonAi action, adjacent attack admission/delay, and
  RandomWalk draws; zero-seed fixtures and legacy multiplayer snapshots retain
  the compatibility path.
- Initialized single-player GoatMelee/AiAvoidance targets now consume the
  global native action, Move-goal, adjacent attack/special, RandomWalk, and
  delay draws under the same compatibility rule.
- Initialized single-player Fat/Overlord targets now consume the global native
  movement, adjacent attack, and RandomWalk draws under the same compatibility
  rule.
- Initialized single-player HorkDemon targets now consume the global native
  spawn/attack and movement admission, recovery delay, RoundWalk, and
  RandomWalk draws under the same compatibility rule.
- Initialized single-player SkeletonRanged targets now consume the global
  native retreat/movement and arrow-admission draws under the same
  compatibility rule.
- Initialized single-player GoatRanged targets now consume the global
  close-range retreat, RandomWalk fallback, and post-shot delay draws under
  the same compatibility rule.
- Initialized single-player Succubus/Lazarus Blood Star targets now consume
  the global close-range retreat, RandomWalk fallback, and post-shot delay
  draws under the same compatibility rule.
- Initialized single-player Rhino targets now consume the global native
  charge, movement, recovery-delay, and adjacent-admission draws under the
  same compatibility rule.
- Initialized single-player Scavenger targets now consume the global native
  corpse-scan direction and corpse-targeted RandomWalk draws under the same
  compatibility rule.
- Initialized single-player Counselor targets now consume the global native
  ranged/movement admissions, retreat RandomWalk, adjacent admission, and
  server-owned recovery-delay draws under the same compatibility rule.
- Initialized single-player Sneak targets now consume the global native
  action, retreat RandomWalk, and adjacent-admission draws under the same
  compatibility rule.
- Initialized single-player Bat targets now consume the global native charge,
  retreat, movement, RandomWalk, and adjacent-admission draws for validated
  Bat controllers under the same compatibility rule.
- Initialized single-player Gargoyle targets now consume the global native
  healing, retreat, AiAvoidance movement, RandomWalk, and adjacent-admission
  draws under the same compatibility rule.
- Initialized single-player Zombie targets now consume the global native action,
  random-direction, and direct movement draws under the same compatibility
  rule.
- Initialized single-player Mega targets now consume the global native delegated
  Skeleton fallback and Inferno admission/recovery draws under the same
  compatibility rule.
- Initialized single-player Snake targets now consume the global native action,
  delay, and drift draws under the same compatibility rule.
- Initialized single-player Bat targets now consume the global native movement
  fallback and RandomWalk draws under the same compatibility rule.
- Initialized single-player Skeleton King targets now consume the global native
  movement, RoundWalk, RandomWalk, and delay draws under the same compatibility
  rule; their existing native action-roll behavior remains unchanged.
- Initialized single-player ranged-avoidance targets now consume the global
  native admission, movement, and projectile/recovery draws under the same
  compatibility rule.
- Initialized single-player generic ranged targets now consume the global
  native retreat and post-shot delay draws under the same compatibility rule.
- Initialized single-player Butcher targets now consume the global native
  non-adjacent RandomWalk draws under the same compatibility rule.
- Authoritative monster targets now retain the native `InitMonster` item-drop
  and AI seeds through snapshots, saves, and protocol fields 86-87; procedural
  loot uses the retained item seed instead of deriving a second seed at death.
  Multiplayer monster ticks now advance the retained AI seed once per tick,
  restore it before that monster's AI random draws, and match native
  `ProcessMonsters` state progression.
- Multiplayer Zombie AI now consumes that restored stream for its action roll,
  random-direction admission, and direct movement path; legacy seed-zero
  targets retain the compatibility controller.
- Multiplayer SkeletonMelee movement and adjacent attack admission now consume
  that restored stream, including native delay rolls and RandomWalk coin draws;
  legacy seed-zero targets retain the compatibility controller.
- Multiplayer SkeletonRanged now consumes the restored stream for its native
  movement/retreat admission and subsequent arrow attack roll, including the
  far-distance movement draw; legacy seed-zero targets retain compatibility.
- Multiplayer Fat/Overlord AI now consumes the restored stream for its native
  movement or adjacent-attack admission, including RandomWalk coin draws;
  legacy seed-zero targets retain compatibility.
- Multiplayer GoatMelee/AiAvoidance now consumes the restored stream for its
  movement, Move-goal, RandomWalk, adjacent-attack, and wounded-special draws;
  legacy seed-zero targets retain compatibility.
- Multiplayer Gargoyle now consumes the restored stream for healthy
  AiAvoidance movement, adjacent attacks, wounded retreat RandomWalk, and
  healing divisor rolls; the native special lock remains server-owned.
- Multiplayer ButcherAi now consumes the restored stream for its native
  non-adjacent RandomWalk coin draws; adjacent attacks retain their no-roll
  native admission and seed-zero compatibility.
- Multiplayer Sneak now consumes the restored stream for its native action
  roll, retreat RandomWalk, and adjacent-attack admission; seed-zero targets
  retain the compatibility controller.
- Multiplayer Skeleton King now consumes the restored stream for its native
  action and movement-admission rolls, delay range, RoundWalk, RandomWalk, and
  adjacent-attack admission; multiplayer summoning remains server-owned and
  seed-zero targets retain the compatibility controller.
- Single-player Skeleton King summons now consume the live native stream for
  skeleton-type selection, stand-animation timing, initial health, and the
  retained item/AI seeds before entering authoritative simulation.
- Single-player Skeleton King summon and adjacent-attack admission now reuse
  the native first action roll instead of a separate compatibility roll.
- Single-player and seeded multiplayer ordinary adjacent monster melee now roll
  the effective native min/max damage range from the authoritative stream;
  validated special melee damage uses the same stream for its special range.
  Seed-zero multiplayer targets retain the legacy damage projection. Those same
  native-stream paths now consume monster hit admission, shield/class block
  admission, and damage in native order; damage is rolled in native Q6 and
  carried exactly in `DamageEvent.amount_q6`; seed-zero multiplayer targets
  retain the compatibility path.
- Multiplayer Bat now consumes the restored stream for native adjacent-attack
  admission, two-step retreat, movement, and mapped-charge action/RandomWalk
  draws; mapped charge misses reuse the same action roll without double-consuming
  the stream.
- Multiplayer Snake now consumes the restored stream for distant and adjacent
  action admission, native delay rolls, and RandomWalk2 fallback draws; mapped
  charge ticks retain their native no-roll branch and seed-zero compatibility.
- Multiplayer HorkDemon now consumes the restored stream for spawn/attack and
  movement admission, recovery delay, RoundWalk, and RandomWalk draws; seeded
  targets retain native controller ordering while seed-zero targets retain the
  compatibility path.
- Multiplayer Scavenger now consumes the restored stream for corpse-scan
  direction and corpse-targeted RandomWalk draws; seed-zero targets retain the
  compatibility path.
- Multiplayer Mega now consumes the restored stream for its far-distance
  Skeleton fallback and mapped Inferno admission, including the native action,
  adjacent-choice, intermediate-movement, recovery-delay, and collision draws;
  seed-zero targets retain the compatibility path.
- Multiplayer GoatRanged now consumes the restored stream for its close-range
  AiRanged admission, native RandomWalk fallback, and post-shot delay; long
  range and seed-zero compatibility branches retain their native no-roll path.
- Multiplayer Succubus/Lazarus Blood Star now consumes the restored stream for
  its close-range AiRanged admission, native RandomWalk fallback, and post-shot
  delay; long-range and seed-zero compatibility branches retain their native
  no-roll path.
- Multiplayer Rhino charge, movement, and adjacent attack admission now consume
  the restored stream for initial admission, far-distance Move-goal setup,
  RoundWalk/RandomWalk fallback, and recovery delay; seed-zero targets retain
  the compatibility path.
- Seeded multiplayer AiRangedAvoidance mappings now consume the restored stream
  for their initial 10,000-band admission, Move-goal setup/direction,
  RandomWalk movement, and recovery delay; seed-zero targets retain the
  compatibility path.
- Seeded multiplayer generic AiRanged mappings (FireBat, Torchant, Lich-family,
  and AcidUnique) now consume the restored stream for their close-range
  admission, native RandomWalk fallback, and post-shot delay; long-range and
  seed-zero compatibility branches retain the native no-roll path.
- Player-owned Golem movement now preserves native blocked-path `RandomWalk`
  and the clockwise eight-direction fallback before giving up; seeded
  multiplayer minions consume the restored stream for the two RandomWalk
  ordering coins.
- Authoritative Golem casts now retain the native spawn-time item and AI seeds
  and advance the same animation/health seed draws before the minion enters
  multiplayer simulation.
- Multiplayer Counselor now consumes the restored stream for its initial and
  secondary ranged/movement admissions, adjacent admission, retreat RandomWalk
  coins, and server-owned recovery delays; seed-zero targets retain the
  compatibility controller.
- Content schema `1.430.0` externalizes native standing-animation frame and
  tick metadata used to preserve the two `InitMonster` RNG draws before
  procedural health and damage initialization.
- Procedural monster scatter now enforces DevilutionX's native `MaxMonsters -
  10` capacity (190), preserving ten slots for quest and runtime summons while
  retaining caller-provided lower limits.
- Procedural monster placements now preserve DevilutionX's native deterministic
  0-to-7 placement direction through authoritative combat targets.
- Skeleton King runtime summons now preserve the native direction toward the
  victim through authoritative combat targets instead of defaulting to zero.
- Planner-created Gargoyles now start with the native special lock active,
  preserving their initial stone-state AI admission.
- Planner-created monster targets now apply native single-player health
  initialization (half health with a one-health floor); multiplayer retains full
  generated health.
- Content schema `1.429.0` externalizes the validated native `MonstConvTbl`
  mapping through
`native_dun_monsters.tsv` for base and Hellfire packs. The
  authoritative module now selects stable monster identities and materializes
  mapped cells in native row-major order when geometry is available.
- Native DUN geometry materialization now preserves the parsed transparency
  layer in authoritative levels instead of dropping native `dTransVal` values
  when external `.til`/`.sol` geometry is composed.
- Raw native DUN monster layers now survive authoritative level and fragment
  composition, including world-mutation rebuilds; unsupported or unmapped
  conversion entries remain inert until their content is externalized.
- Deferred native-DUN fragments now refresh effective world geometry and
  reconcile authored object mappings after their anchor appears, so mapped
  set-piece object cells become authoritative instead of remaining inert.
- Raw DUN object-layer conversion indices now preserve the native
  `ObjTypeConv` range through the C++ bridge, authoritative C# geometry, and
  Godot validation; converted object presentation identities retain the
  separate `OBJ_LAST` bound. Warlord's native object-layer value `109` can now
  cross the compatibility protocol without being mistaken for an object
  snapshot identity.
- Authored object-layer mutation values now fail closed during content parsing
  when they exceed the raw DUN compatibility range, instead of waiting for
  later projected-geometry reconstruction to reject them.
- Godot and the authoritative combat processor now support spell consumables in
  an open stash as stable cast sources; closed-stash sources fail closed without
  consuming the item.
- The connected native spell list now exposes validated open-stash scroll and
  rune sources and emits their stable stash references through the cast protocol;
  offline spell selection remains unchanged.
- Cursor-triggered native scroll and rune casts now resolve the same open-stash
  stable references when no inventory or belt slot is selected.
- Connected native stash books, potions, and stash-sourced oils now use open-session
  stable item references as authoritative sources; closed-stash sources fail
  closed without consuming or mutating local state.
- Connected native oil use now routes inventory-sourced oils onto open stash
  targets through the authoritative stable-reference command; closed stash
  targets remain rejected without consuming the source item.
- Native controller interaction now fails closed when a highlighted monster is
  missing from the authoritative projection; stale monster highlights no longer
  fall through to positional attacks.
- Connected native spell submission now fails closed when a visible player or
  combat-entity cursor target is missing from the authoritative projection;
  it no longer silently falls back to a tile cast, including scroll and rune
  consumables.
- Native geometry presentation now independently rejects out-of-range DUN
  object-layer IDs when applying directly constructed projections.
- Authoritative world-item pickup now preserves native cursor behavior: full
  inventories project the picked item as held state, and a held cursor item is
  atomically dropped at the authoritative player tile before replacement;
  native replay pickup follows the same rules.
- Telekinesis now validates that a held cursor item can be dropped before the
  cast consumes mana or its source; failed replacement attempts leave both
  authoritative items and cast resources unchanged.
- Telekinesis object targets now validate their authored mutation, reward, and
  spell-projectile requirements before cast resources are consumed.
- Authored object rewards now validate world-item placement against every
  authoritative participant, preventing multiplayer rewards from spawning on a
  different player's tile.
- Authored quest-sequence monster follow-ups now validate their spawn tile
  against every authoritative participant before changing quest state.
- Staged unique-monster dialogue rewards now validate the complete participant
  set and existing world-item occupancy before advancing the quest stage.
- Non-staged unique-monster dialogue rewards now use that same participant-aware
  placement boundary before consuming the dialogue item or advancing the quest.
- Towner turn-in rewards now use all authoritative participants for entity-ID
  allocation and reject occupied reward tiles before mutating quest state.
- Monster-defeat quest rewards now search deterministically for an available
  tile when the target position is occupied by another participant or item.
- Native monster drops now use the same participant-aware open-tile placement
  boundary before entering authoritative world state.
- Spell catalog activation now rejects cyclic Jester dispatch tables before
  recursive authoritative cast resolution can exhaust the process stack.
- Quest activation validation now rejects self-referential completion targets
  while retaining valid follow-on quest routes.
- Extended the focused TCP reconnect regression to four active participants,
  preserving complete self-first snapshots and canonical state hashes while
  the resumed participant rejoins three connected peers.
- Refreshed TCP store snapshot fixtures to use valid empty inventory grids under
  the current authoritative topology validation.
- Added a focused three-participant TCP reconnect regression covering complete
  self-first snapshots and canonical state hashes.
- World-item anchor completion now requires the target quest to be active;
  inactive content cannot be completed by dropping an item on its anchor.
- Added the validated Lachdanan native-AI identity and kept his dialogue-only
  encounter passive in authoritative combat ticks, matching the native
  `LachdananAi` behavior outside quest-dialogue handling.
- FireMan encounters now fail closed to passive behavior because the native
  registry has no FireMan AI; the server no longer invents generic melee
  behavior for that shipped Hellfire identity.
- LazarusSuccubus now honors the native single-player Vile Betrayer quest gate:
  its Blood Star attack remains suppressed through variable 5, resumes at 6,
  and remains active in multiplayer.
- Extended the validated text d-piece property loader to preserve the complete
  native `.sol` flag set, while retaining compatibility with older rows that
  provide only `solid` and `block_missile`.
- Added schema 1.428's optional `piece_property_overrides` world-mutation
  column. Mutations can now externalize validated native `.sol`-style
  collision flags for d-pieces introduced by a set-piece transition; the
  Hellfire Na-Krul room-opening mutation now preserves its walk and missile
  geometry contract when the surrounding binary collision bundle is absent.
- Server and Godot protocol projections now validate generated set-piece
  origins against supplied authoritative level geometry, while geometry-free
  legacy snapshots retain their compatibility behavior.
- The native compatibility client now applies the same generated-origin
  validation before snapshot projection, including duplicate identity checks
  and geometry-free legacy compatibility.
- Added schema 1.427's optional `procedural_set_piece_origins.tsv` catalog.
  Generated set-piece origins now derive deterministically from aligned
  walkable authoritative geometry and feed relative set-level object
  materialization, while unavailable target levels remain deferred.
- Retained procedural-origin rules in the simulation so hosts can publish
  deferred origins after generated geometry arrives; level-transition intents
  invoke the same pass, and repeated publication is deterministic without
  duplicating relative objects.
- Added authoritative world-state add/replace operations for late-generated
  levels; effective mutation and native-layer geometry is rebuilt before those
  levels can feed deferred origin publication.
- Added schema 1.426's optional set-level object placement mode. Existing rows
  remain absolute; future procedural set-piece rows can use signed offsets from
  a runtime-supplied origin and stay deferred until its geometry is available.
- Connected relative set-level placements to the authoritative simulation:
  initial origins and later level-entry origin publication now materialize
  validated objects with deterministic reserved identities.
- Persisted runtime-generated set-piece origins through transport-neutral
  snapshots and version-1 saves, including canonical state-hash coverage.
- Clarified that un-authored native object behavior and other local interaction
  fallbacks are offline-only; connected sessions require authoritative object
  projections and fail closed otherwise.
- Inventory placement now retains native ordering for known footprints and
  falls back deterministically for other validated external-content footprints
  permitted by the authoritative 10x4 grid contract.
- Authoritative purchases now maintain a supplied inventory grid even for
  wallet-only compatibility sessions without Gold item stacks; Gold topology
  detection no longer suppresses ordinary item placement.
- Aligned authoritative store-simulation fixtures with strict protocol
  topology, native item identity, cursor metadata, and unique-seed validation;
  the full store executor class now passes without weakening those boundaries.
- Generated authoritative items now keep DevilutionX-compatible 16-bit
  creation flags: legacy-sized fixture seeds retain their value, while larger
  stable item seeds use canonical no-flag state instead of invalid 32-bit data.
- Godot malformed-monster protocol coverage now initializes positive vitality
  with the explicit authoritative `Alive` state, so combat-state mutation checks
  reach combat validation instead of failing during fixture setup.
- Godot now filters cached item/projectile positions and floating-feedback
  anchors by active-level visibility, preventing off-level snapshot entries from
  leaking into the current presentation.
- Corrected Godot portal presentation and endpoint range validation to treat
  level zero as the real native town level, matching server-owned exact endpoint
  admission rather than the generic entity visibility wildcard.
- Godot now replaces its authoritative level-geometry cache per complete
  snapshot and clears it on disconnect, preventing stale blocked cells or warp
  markers from leaking across sessions or geometry-less legacy snapshots.
- Godot portal presentation and click routing now share exact server-owned
  source and bidirectional destination level identities.
- Godot world presentation now retains and draws server-owned persistent corpse
  tiles with the shared authoritative level-visibility contract; corpse lifecycle
  remains server-controlled.
- Smith/Witch vendor admission now treats non-empty normalized generation tags
  as authoritative for `Misc` and `Quest` classification instead of allowing a
  conflicting legacy item class to override authored content.
- Native compatibility item projection now applies authoritative Gold/Ear
  presentation identities even when legacy item fields conflict.
- Item classification now treats semantic generation tags case-insensitively
  across normalized definitions and custom content overlays.
- Item presentation now resolves the content-declared `Ear` generation tag
  before applying the legacy PvP-ear identity fallback, so normalized item
  metadata owns the wire presentation kind whenever an active catalog is
  available.
- Vendor `Misc` tags now resolve through unambiguous `misc_id` mappings and
  override conflicting legacy item-classification fields for Smith/Witch
  admission.
- Vendor `NoSale` and `Quest` tags now resolve through unambiguous `misc_id`
  mappings when native item identity is unavailable, preventing legacy vendor
  classification from overriding normalized content metadata.
- Recharge admission now suppresses the native staff misc-ID fallback when
  unambiguous normalized item tags identify a misc-ID-only row as non-staff;
  older untagged mappings retain compatibility behavior.
- Random unique-monster drop admission now honors normalized `Book` generation
  tags even when the spell-book catalog is omitted; tagged non-books no longer
  inherit the native `misc_id=25` book fallback.
- Restricted the legacy Smith/Witch oil misc-ID and Staff of Lazarus item-index
  sale fallbacks to catalog-less compatibility calls; normalized item metadata
  now fully owns vendor admission whenever an authoritative catalog is present.
- Added validated quest-type world-item anchors; a matching single-player
  drop completes the quest while the dropped item remains server-owned and
  persistent in world state, with snapshot round-trip coverage for the
  completed quest and retained item; activation now rejects anchors outside or
  on blocked cells in their authored quest level and rejects duplicate anchors
  across quest states.
- Shipped Hellfire's native `cornerstone.dun` as a validated coordinate-anchored
  level-21 fragment at `(14,14)`, with content activation coverage for its
  mega-tile layer and matching Cornerstone quest metadata.
- Added validated deterministic coordinate anchors to native DUN fragments;
  fragment object placements can inherit a coordinate anchor, extending the
  content/runtime contract toward procedural set-piece placement without
  creating a placeholder runtime object.
- Guarded the native doppelgänger-spawn and monster attribute-reduction
  helpers with the connected-session simulation policy, preventing direct
  callers from mutating server-owned monster or player state.
- Guarded direct native monster damage application with the same policy, so
  connected missile or protocol callers cannot mutate server-owned hit points
  or trigger legacy death/drop handling.
- Guarded native monster hit attribution and gargoyle wake-up mutation with
  the connected-session simulation policy; connected presentation cannot
  update server-owned attacker or special-attack state.
- Guarded native monster knockback and petrification mutators with the same
  policy; authoritative clients retain only snapshot-driven presentation for
  those states.
- Guarded the native player-versus-monster, player-versus-player, and
  breakable-object hit helpers with the connected-session simulation policy,
  closing direct combat mutation seams below the action loop.
- Guarded native player missile/lifecycle cleanup with the connected-session
  simulation policy, preventing presentation-only level transitions from
  mutating server-owned golems or Stone Curse state.
- Applied the connected-session player-simulation boundary to the native
  per-action execution helpers (`DoWalk`, attacks, block, spell, hit, and
  death), so direct calls cannot advance legacy gameplay beside the server.
- Added an explicit connected-session guard to the native player action-start
  helpers for walking, melee attack, ranged attack, and spell execution; direct
  callers can no longer start legacy player simulation alongside the server.
- Made the native `CastSpell` and resurrection-beam helpers explicitly
  offline-only in server-backed sessions, closing their direct-call seams
  instead of relying on the missile factory's connected-mode no-op.
- Added focused compatibility coverage for the connected legacy spell
  simulation policy.
- Aligned native monster presentation with Godot's alive-only authoritative
  visibility rule: dead projected monsters no longer retain stale compatibility
  pool slots or animation state.
- Preserved authoritative monster stealth state through the native projection;
  hidden monsters now remain absent from native presentation and interaction
  lookup, matching Godot's `SneakHidden` behavior.
- Centralized the native level-visibility wildcard and applied it to remote
  player targeting, so projected level `0` players follow the same active-level
  visibility rule as Godot and other native authoritative entities.
- Preserved authoritative `TrapArmed` object state through native projection;
  trap-only objects now remain eligible for server-owned disarm interaction and
  update the native compatibility trap flag.
- Applied authoritative object missile-passability and breakable/broken state
  to native compatibility objects, preventing stale collision metadata after a
  snapshot replacement.
- Centralized Godot's authoritative monster presentation visibility rule so
  dead or `SneakHidden` monsters are removed from cached presentation
  positions and cannot receive action-indicator target lines.
- Aligned Godot object presentation with native object-pool removal: activated
  and destroyed objects no longer retain cached positions or remain rendered.
- Aligned Godot quest/towner validation with the native duplicate-level
  geometry boundary. Duplicate level identities now fail with the controlled
  invalid-snapshot error before interaction bounds are evaluated, rather than
  escaping through a dictionary-construction exception.
- Aligned dynamic item identity validation across server, native, and Godot
  boundaries: runtime-authored names now require the byte-representable native
  cursor graphic as well as both base and identified names.
- Aligned native item scalar representability across server, native, and Godot
  boundaries, including narrowed enum, damage, bonus, requirement, and
  Hellfire-flag fields that must fit their native storage types.
- Aligned native player-item topology validation with the managed boundaries:
  inventory-grid references and footprints, equipment slots, belt slots, and
  collection limits now fail closed consistently.
- Aligned native vendor projection with managed active-store validation by
  rejecting stock entries that lack a valid native item identity.
- Aligned native byte-length identity limits for unique monsters and quest
  completion identities with the managed UTF-8 protocol validators.
- Centralized the same UTF-8 byte boundary in the managed domain so authored
  unique-monster, quest, world-mutation, and native-DUN identities fail during
  content/runtime validation instead of only at protocol projection.
- Aligned the native player UI with the managed legacy inventory-grid contract:
  omitted zero-length grids are accepted and stale native cells are cleared.
- Cleared stale native character names when an accepted legacy player snapshot
  omits the optional character identity.
- Aligned remote native player hydration with the local snapshot path: omitted
  legacy inventory grids now clear stale equipment, belt, inventory, grid, and
  held-item presentation state before applying the next snapshot.
- Preserved authoritative native monster directions through the C++ projection
  so compatibility presentation no longer retains a stale pool direction.
- Aligned native and Godot monster snapshot validation so `Alive` must match
  effective Q6 hit points, preventing contradictory dead/alive presentation
  state at either client boundary.
- Aligned Godot monster vitality validation with native Q6 fallback semantics:
  effective maximum hit points now bound effective current hit points even when
  both values arrive through legacy whole-point fields.
- Aligned Godot visited-level validation with the native 25-level range so
  out-of-range authored level IDs cannot enter client state.
- Aligned Godot session metadata validation with native projection: unknown
  difficulties and zero or duplicate world-mutation IDs now fail closed.
- Restored Godot compatibility with legacy stationary monster snapshots whose
  omitted future coordinates default to `(0, 0)` and normalize to the current
  tile, matching the native projection.
- Aligned warp-trigger reference validation across the server mapper, native
  snapshot bridge, and Godot protocol model: source levels must exist and
  nonzero quest gates must reference a quest in the same snapshot.
- Aligned optional native quest-ordinal validation across server, native, and
  Godot snapshots with the compatibility range (`0` through `23`) and unique
  identity requirement.
- Aligned projected monster unique-identity validation with native behavior:
  identities are limited to 128 characters and cannot be attached to ordinary
  monsters.
- Applied validated authoritative level mega-tile, d-piece, and transparency
  layers in the native compatibility presentation bridge. Native DUN object
  layers remain inert until an authored object mapping exists, so behavior and
  encounter-state ownership cannot be reconstructed locally from presentation
  data.
- Allowed native town-sized geometry projections without an optional coarse
  mega-tile layer while retaining the 40×40 coarse-layer bound when that layer
  is present.
- Tightened native level-geometry protocol validation so transparency regions
  and mega-tile identities cannot exceed the native compatibility client's
  signed-byte and byte-sized presentation ranges across the C++ bridge,
  authoritative C# level model, and Godot protocol model.
- Applied the native 112×112 fine-grid and optional 40×40 coarse-grid geometry
  limits consistently to authoritative C# levels and Godot snapshots.
- Applied the same fine/coarse geometry limits at the C++ snapshot validation
  boundary, while retaining coarse-layer-free 112×112 town snapshots.
- Aligned converted native object identity validation across the C++ bridge,
  authoritative C# levels, and Godot snapshots with the native `OBJ_LAST`
  range (`0` through `108`), while retaining the broader d-piece value range.
- Applied the same native object identity bound to object snapshots and server
  object state, preventing values above `OBJ_LAST` from crossing the protocol.
- Enforced the native object identity bound during native-DUN mapping load and
  mutable authoritative-object state updates, rather than deferring rejection
  until snapshot projection.
- Externalized the Snotspill Banner sign chest as an authored server object. Its
  quest-variable gate and normal-item reward now activate through the shared
  authoritative object path instead of relying on the native local pickup. The
  authored row now also declares the native sign-chest presentation identity.
- Added additive native presentation identity metadata for authored objects. The
  Hellfire Na-Krul tomes and final lever now project through their native book
  and lever visuals while stable authored object IDs remain server-owned. The
  Chamber of Bone and Vile Betrayer templates now likewise declare their native
  book and center-circle presentation identities.
- Externalized Hellfire Na-Krul's final lever as an anchor-relative authored
  fragment object. The server now gates it on the three-tome sequence and owns
  the final quest completion; the quest no longer completes prematurely when
  the third tome is read.
- Hardened authored occupancy-trigger persistence: an unactivated trigger tile
  remains enterable during snapshot restore, and every secondary authored world
  mutation follow-up is validated before content activation.
- Externalized the Vile Betrayer center-circle trigger as a validated occupancy
  object: both linked books must activate before the circle applies its deferred
  replacement, advances native `_qvar1` to 4, and launches activating-player
  Phasing. Occupancy-trigger objects remain enterable and persist through
  authoritative snapshots.
- Externalized the two fixed Chamber of Bone completion books as validated
  set-level behavior templates: the server now completes the authored quest,
  grants one Guardian level, and starts the native player-origin Guardian at
  the exact book-relative target offset. Set-level quest progress gates resolve
  their native route identity without changing ordinary quest-level behavior.
- Externalized the two fixed Vile Betrayer book replacement rectangles as
  validated set-level world mutations. Their authored WorldMutation effects
  apply the native pre-map replacement when the optional map bundle is loaded,
  then launch the activating-player Phasing projectile in the same operation;
  native object resynchronization and encounter presentation remain seams.
- Externalized the two fixed Vile Betrayer book placements as validated
  set-level behavior templates: authoritative activation now owns the native
  activating-player Phasing projectiles and exact target offsets, while the
  native mega-tile replacement and encounter-state presentation remain
  documented compatibility seams.
- Added validated native mega-tile copy rectangles to world mutations, allowing
  a server-owned target level to apply a replacement layer and matching d-piece
  collision state from a companion source level without hard-coded tile data.
- Restricted the Godot HUD's generic object, trap, and quest commands to
  visible server-authored targets, using deterministic entity/quest ordering
  instead of arbitrary snapshot entries.
- Canonicalized Godot world-item and authored-object target selection, plus
  clicked item and portal hit-testing, by entity ID so client interaction
  resolution matches the native projection order.
- Canonicalized native monster, world-item, and object projection ordering by
  entity ID so overlapping interaction lookups match Godot's deterministic
  selector order, with focused native regression coverage.
- Centralized the authoritative level-visibility wildcard in one shared Godot
  protocol projection used by targeting, authored-object/dialogue routing, and
  world presentation, with focused invariant coverage.

- Preserve native level-`0` wildcard visibility when Godot routes authored
  object clicks, including ordinary operation and trap-disarm interactions.
- Match native Telekinesis target precedence in Godot by preferring authored
  objects over world items and excluding server-authored dialogue monsters
  from world-spell targets.
- Align Godot world and authored-dialogue target selection with native
  level-`0` compatibility wildcards while retaining exact-level filtering for
  ordinary projections.
- Align Godot monster targeting with native authoritative occupancy by
  resolving moving monsters on both their current origin and server-owned
  future destination for combat, Telekinesis, and authored dialogue.
- Centralize and characterize the native Lua lifecycle gate so authoritative
  startup and shutdown cannot create or tear down an embedded Lua runtime;
  explicit legacy launches retain the existing compatibility lifecycle.
- Mark Godot's server-authored unique-monster dialogue targets with a gold
  presentation marker, keeping discoverability derived from snapshot state.
- Route Godot clicks on visible authored unique monsters through
  `TalkToQuestRequested` by matching the server-projected dialogue identity to
  the active quest; the server still owns validation and state changes.
- Project authoritative unique-monster dialogue identities into the native
  compatibility monster state so talk affordances follow server-owned quest
  stages; invalid native speech IDs fail closed.
- Add validated anchored native mega-tile assignments and explicit
  transparency-layer creation to authoritative world mutations; the base
  Snotspill hand-in now applies its final Banner map/transparency transition
  through the server-owned quest dialogue follow-up.
- Route the first Snotspill Banner dialogue through an anchored server-owned
  opening transparency mutation while retaining native object/map
  resynchronization and encounter presentation as compatibility seams.
- Advance the active Diablo content schema to `1.419.0`, retaining additive
  compatibility with schema `1.418.0`.
- Externalize the initial Snotspill Banner room through a validated Hell
  placement, unique-anchored `banner1.dun` fragment, and native monster type
  metadata while retaining later Banner mutations as a compatibility seam.
- Gate the authored Snotspill placement on the active Ogden's Sign quest so
  single-player quest-pool exclusions preserve native encounter availability.
- Allow native-DUN fragment object placements to follow generated unique
  encounters after materialization, while retaining fixed-anchor compatibility.
- Materialize the authored Snotspill Banner sign chest by cloning a validated
  behavior-only object template at its unique encounter-relative position;
  unavailable quest-gated encounters produce no orphan chest.
- Apply unique-anchored fragment behavior objects when deferred encounters are
  generated on level entry, keeping geometry and object state in one server-owned
  lifecycle.
- Validate native-DUN fragment behavior-template references during content
  activation so unavailable encounters cannot hide malformed pack data.
- Externalize a validated Storyteller service-only store identity for paid Cain
  identification, preserving the previous additive content schema while the
  connected native menu is routed through the authoritative session.
- Route the connected native Cain identification menu through the Storyteller
  service session, closing it when the menu is left while retaining offline
  behavior.
- Close the connected Storyteller service on both Cain's explicit leave option
  and the native Escape path.
- Externalize the shipped Staff of Lazarus vendor-sale exception through the
  normalized `NoSale` item tag while retaining the legacy index fallback.
- Resolve normalized oil vendor-sale filters from content-owned item action
  kinds while retaining the native misc-ID fallback for older states.
- Resolve normalized quest-sale admission from item classification and tags,
  retaining the native quest-index range only for catalog-free legacy states.
- Strengthen shipped spell-book activation coverage so every production book
  explicitly declares single-player admission metadata.
- Constrain authoritative Repair and Recharge admission to declared Smith and
  Witch vendor roles, reject vendor sale/identification/service mutations
  through Adria, and retain Generic compatibility for legacy custom-store
  fixtures.
- Require the authoritative Adria service to be opened and used on the
  configured world-rule town level.
- Advance the active Diablo content schema to `1.418.0` for the validated
  Storyteller service identity, while retaining `1.417.0` compatibility for
  existing manifest-based packs.
- Externalize native vendor roles in store content and enforce Smith, Witch, Wirt, and Healer sale admission on the authoritative server, with focused native-filter parity coverage.
- Add validated authoritative server host and port entry to the Godot character setup flow while preserving environment defaults and resume behavior.
- Add an authoritative `SplitGoldRequested` transition for connected native gold-stack splitting; the server owns the source remainder and held cursor stack.
- Route connected native inventory sorting through an authoritative `SortInventoryRequested` transition that preserves native ordering and placement.
- Exclude script-backed default and unpacked `init.lua` mods from authoritative native startup while retaining legacy discovery.
- Keep legacy `/lua` debug chat hotkeys out of authoritative console entry while
  preserving their legacy-mode behavior.
- Reuse the aggregate projection validation boundary when importing saved world entities, rejecting malformed world-item, object, quest, monster, projectile, portal, and corpse state.
- Reuse the live player projection validation boundary when importing saved snapshots, rejecting malformed resources, item topology, actions, spell state, and item identity.
- Reject imported saves whose player, active-store, and stash-session state contradicts the authoritative session contract.
- Verify the protocol state hash when importing version-1 saves, rejecting tampered snapshots while retaining hashless legacy compatibility.
- Reject invalid world-mutation identities and duplicate active-store slots when importing protocol snapshots for authoritative saves, matching the live projection boundary.
- Align save round-trip fixtures with authoritative stash/vendor exclusivity and quest-linked object validation.
- Validate participant-template equipment/belt slots, item dimensions, and inventory footprints before runtime creation.
- Validate participant-template scalar resources, ability IDs, reflect charges, and combat-mode timing before runtime creation.
- Validate participant-template attribute references and non-negative base/current values before runtime creation.
- Reject negative participant attribute values during authoritative save restore.
- Reject saved player gold, stash gold, and stat points that cannot cross the shared protocol boundary.
- Reject the same unrepresentable player resources before live server-to-client protocol projection.
- Reject live authoritative player projections whose life, mana, base maxima,
  Reflect charges, or character level cannot cross the native/Godot snapshot
  boundary, matching client-side resource validation.
- Reject live authoritative player projections with negative primary attributes,
  matching the save and Godot snapshot validation boundaries.
- Reject live authoritative player projections with invalid ability, facing, or
  combat-mode enum values before casting them into the wire schema.
- Reject live authoritative player projections with malformed action timing or
  action-kind state before serializing the native/Godot action snapshot.
- Reject live authoritative player projections with invalid or duplicate
  memorized spells and status effects before serialization.
- Reject live authoritative player projections with oversized, duplicate, or
  current-level-missing visited-level state before serialization.
- Reject live authoritative snapshots whose active-store and stash-session
  state cannot be represented consistently by the shared client projection.
- Reject malformed live authoritative character names before native/Godot
  projection, including names outside the native printable length rules.
- Reject live authoritative item projections with impossible value, damage,
  armor, charge, durability, elemental, footprint, or semantic enum state.
- Reject live item projections with partial or oversized presentation names,
  embedded NULs, or out-of-range native cursor graphics.
- Reject unrepresentable live item prices across player, active-store, and
  world-item projections before they cross the shared protocol.
- Reject live item projections that have neither a nonzero item seed nor a
  complete presentation identity, matching the client identity boundary.
- Reject live player item collections with duplicate references, invalid slots,
  oversized counts, or malformed inventory-grid references.
- Reject live inventory grids whose cell occupancy does not exactly match each
  projected item's native footprint.
- Reject live stash pages with out-of-range cells/pages or overlapping item
  footprints before projection.
- Reject active-store projections containing duplicate stock slots.
- Reject world-item projections with coordinates outside the native tile range
  or supplied authoritative level geometry.
- Reject authored object projections with coordinates outside the native tile
  range or supplied authoritative level geometry.
- Reject projected projectile states with invalid identity, lifetime, damage,
  enum, coordinate, or spell/behavior combinations.
- Reject projected player, moving-monster future, corpse, and portal endpoint
  coordinates outside the native tile range or supplied level geometry.
- Reject projected monster vitality, combat, dialogue, ownership, death-timing,
  and movement-occupancy states outside the shared native protocol range.
- Reject projected object effect, quest-transition, reward, follow-up, and
  projectile metadata that cannot be represented by the shared client contract.
- Reject projected quest identity/progress metadata and portal identity,
  ownership, and lifetime states that cannot cross the shared client contract.
- Reject projected warp targets with duplicate identities, invalid geometry,
  unknown quest gates, or non-printable stable symbols.
- Reject projected quest/towner interactions with invalid identity, quest or
  level links, native IDs, message IDs, coordinates, or variable ranges.
- Reject projected active world-mutation collections with zero, duplicate, or
  oversized identities before serialization.
- Reject projected player combat modes whose end-tick state violates the
  native Stand, Block, or GotHit timing contract.
- Reject null inventory or stash entries before player-item projection can
  dereference malformed authoritative collections.
- Validate world-mutation identities before publishing a restored mutation set, preserving the prior projection when a save lists an unknown or duplicate mutation.
- Roll back the prior world-mutation projection when later authoritative snapshot validation rejects a save.
- Reject duplicate participant-template item identities across inventory, equipment, belt, stash, and held-item state.
- Validate participant-template stable item IDs, seeds, names, and cursor graphics before runtime creation.
- Preserve legacy startup inventory-grid placeholders and defer malformed stash-footprint rejection to save restore.
- Keep connected native level loading from resetting server-owned charging
  monsters; legacy charger cleanup remains offline-only.
- Keep connected native level loading from resynchronizing stale legacy portal
  records; authoritative portal projection remains the connected source.
- Preserve the server-selected player tile during connected native level
  loading; the legacy nearby-tile placement fallback remains offline-only.
- Keep connected native player initialization from replacing the authoritative
  transition tile with the legacy view-position fallback during level loading.
- Guard connected native dungeon-message playback during level loading so
  legacy Defiler quest mutation and quest resynchronization commands cannot
  bypass authoritative level projections.
- Guard direct connected identify, repair, recharge, and oil cursor callbacks;
  stable-reference item-service commands remain the connected mutation path.
- Guard direct connected town-opening helpers before legacy hive or grave
  commands can mutate local town state; authoritative object operations remain
  the connected route.
- Route connected native object commands through the authoritative-only object
  projection, keeping presentation-only objects out of OperateObject,
  DisarmTrap, and object-targeted Telekinesis submissions.
- Keep server-owned presentation missiles from clearing legacy missile and
  wall tile flags; only locally simulated missiles update that bookkeeping.
- Guard native object synchronization helpers at their public entry points so
  direct connected delta, operate, close, and break callbacks fail closed.
- Guard direct connected object interaction facades, including break, trap, and
  Na-Krul room callbacks, while preserving authoritative operate submission.
- Guard connected native towner dialogue before legacy quest or store mutation,
  while preserving authoritative dialogue and menu submission.
- Guard connected native monster dialogue before legacy quest handoffs, while
  preserving authoritative quest-dialogue submission.
- Guard direct connected trap-disarm callbacks before legacy trap-state
  mutation; authoritative disarm intent remains unchanged.
- Guard the native portal delta-state setter so connected legacy callbacks
  cannot publish local portal state outside authoritative snapshots.
- Guard native portal level and position transition helpers before connected
  sessions can consume stale legacy portal state.
- Guard direct native death-drop helpers for item respawn, gold splitting, and
  top-gold overflow while connected sessions remain server-owned.
- Guard direct native equipment-damage helpers before connected weapon decay,
  shield/staff durability loss, or armor damage.
- Guard direct connected player damage/death and monster damage callbacks before
  native combat state or death lifecycle mutation.
- Guard direct connected native resource, progression, attribute, and hit-point
  mutation helpers while preserving the authoritative snapshot projection path.
- Guard connected native missile creation and direct spell/healer effect
  callbacks before legacy projectile or player-state mutation.
- Guard connected native inventory/equipment stat-recalculation helpers so
  derived player state remains snapshot-owned.
- Guard direct connected native inventory, belt, and equipment removal and
  automatic-placement helpers before legacy local item mutation.
- Preserve non-persisting automatic-placement probes for connected vendor and
  inventory capacity checks while rejecting legacy writes.
- Guard direct connected equipment replacement, cursor swaps, gold placement,
  inventory synchronization, and staff-charge consumption before legacy item
  state mutation.
- Guard direct connected native store purchase, sale, repair, recharge,
  identify, currency, and automatic-placement mutations while preserving
  read-only vendor room checks.
- Route connected native manual and automatic world-item pickup through the
  authoritative projected entity command and guard direct legacy pickup and
  cleanup helpers.
- Guard direct connected legacy floor-item synchronization while preserving
  the dedicated authoritative world-presentation allocation path.
- Guard direct legacy delta-load helpers for spawned monsters, monster state,
  objects, and world items in connected sessions.
- Route native stair, town-warp, and quest-entrance trigger checks through an
  explicit connected-session legacy-world policy, with compatibility coverage
  proving that connected clients cannot emit legacy level-transition commands.
- Add a 64-generation ruleset replacement pressure fixture proving that
  pinned generations remain retired until every lease is released.
- Extend the generic host adapter replacement soak from eight to 64 generations,
  preserving migrated session state and disposing each prior target after publication.
- Route connected native visual-store openings through authoritative vendor stock
  for Smith, Witch, Healer, and Wirt; keep the unsupported premium tab fail-closed.
- Set-level native geometry now has an explicit asset preflight before level
  materialization; partial bundles fail before any runtime geometry is published.
- Set-level map definitions now require `.til` and `.sol` collision assets as
  an explicit pair; metadata-only rows remain valid when neither is supplied.
- Ordinary native-DUN level activation now preflights every referenced `.dun`
  and optional `.til`/`.sol` asset before publishing the composed world, matching
  the fragment catalog's activation boundary.
- Revalidate authoritative portal IDs during world validation so direct catalog
  callers cannot publish zero or duplicate portal identities.
- Reject zero or duplicate portal IDs when constructing live authoritative world
  state, closing the same identity gap for direct runtime callers.
- Reject contradictory static-versus-dynamic portal ownership and lifetime
  metadata during live authoritative world construction.
- Bound direct initial world-state collections to the same 4,096-entry
  per-category limit enforced during authoritative save restoration.
- Revalidate mutable portal dictionary keys and lifecycle metadata at the live
  initial-position gate before publishing a session.
- Revalidate mutable world-entity dictionary keys, including projectiles, against
  their server-owned entity identities before publishing a session.
- Reject invalid mutable projectile lifetime, enum, and endpoint state at the
  live initial-position gate.
- Revalidate mutable quest dictionary keys against their server-owned quest
  identities before publishing a session.
- Reject oversized participant-template inventory, equipment, belt,
  memorized-spell, inventory-grid, and stash collections before runtime
  creation.
- Reject duplicate equipment and belt slot assignments in participant templates
  before runtime creation.
- Validate participant-template inventory-grid references and memorized-spell
  IDs/levels, including duplicate spell identities.
- The server boundary suite now rejects Lua assets or manifest references in
  the active base and Hellfire content packs, while legacy compatibility files
  remain outside those packs.
- Server boundary tests now explicitly enforce that the Diablo game module
  depends only on Domain, Data, and Engine.
- Production content activation now rejects unique and affix power symbols
  without an authoritative C# handler, while direct catalogs retain their
  compatibility behavior.
- Externalize native d-piece mutations through `piece_layer_rects`, preserving
  the Hellfire Na-Krul `ObjSetMicro` room-opening layer in authoritative state,
  snapshots, and the content schema `1.415.0`.
- Retain native `.til`/`.sol` collision provenance through fragment composition
  so authoritative d-piece mutations recompute native-owned movement and missile
  cells without disturbing authored geometry.
- Extend ordinary native-DUN level rows with optional validated `.til`/`.sol`
  collision assets, advancing the Diablo content schema to `1.416.0` while
  preserving metadata-only behavior for legacy rows.
- Publish ordinary native-DUN level replacements only after the complete
  catalog validates, preventing partial world composition on activation errors.
- Apply native-DUN fragments through the same staged publication boundary while
  preserving deterministic composition of multiple fragments per level.
- Clarify that the native Lua REPL, quick-message frontend, and compatibility
  aliases are legacy-mode only; authoritative server-backed mode remains
  native-diagnostics-only.
- Unique-item and affix powers with matching ordinary effect symbols now resolve through
  the validated
`native_item_effects.tsv` namespace, retaining native flag
  fallbacks for direct catalogs without the namespace; the remaining
  level-dependent StealMana, StealLife, FastAttack, FastRecover, and FastBlock
  flags and elemental arrow/damage channel mappings are now covered by the same
  catalog.
- Hellfire unique-item and affix powers now resolve through the
  validated
`native_item_hellfire_effects.tsv` namespace, retaining native flag
  fallbacks for direct catalogs without the namespace.
- Legacy charged-staff initialization now resolves the `STAFF` miscellaneous
  identity through validated
`native_item_misc.tsv` content while retaining
  the native fallback for older callers.
- Spell-book rows can now author single-player admission through the validated
  `single_player_excluded` field, advancing the Diablo content schema to
  `1.414.0` while retaining native behavior and ID fallbacks for older rows.
- Spell-book rows can now author their native cursor projection through the
  validated `item_cursor_graphics.tsv` catalog, advancing the Diablo content
  schema to `1.413.0` while retaining the native magic-school fallback.
- Legacy item-type generation tags now resolve through validated external

`native_item_tags.tsv` data during content activation, advancing the Diablo
  content schema to `1.412.0` while retaining direct-loader compatibility
  fallback.
- Normalized Hellfire item-effect symbols now resolve through validated external

`native_item_hellfire_effects.tsv` data during content activation, advancing
  the Diablo content schema to `1.411.0` while retaining direct-loader
  compatibility fallback.
- Legacy `itemdat.tsv` and normalized item special-effect symbols now resolve
  through validated external
`native_item_effects.tsv` data during content
  activation, advancing the Diablo content schema to `1.410.0` while retaining
  direct-loader compatibility fallback.
- Legacy `itemdat.tsv` type, class, and equipment-location symbols now resolve
  through validated external
`native_item_topology.tsv` data during production
  activation, advancing the Diablo content schema to `1.409.0` while retaining
  direct-loader compatibility fallback.
- Legacy `itemdat.tsv` miscellaneous symbols now resolve through a validated
  external
`native_item_misc.tsv` namespace during production content
  activation, preserving the compatibility fallback for older direct callers
  and advancing the Diablo content schema to `1.408.0`.
- Legacy `itemdat.tsv` spell names now resolve through a validated external

`native_spells.tsv` namespace during production content activation. The table
  preserves all 52 native ordinals, rejects duplicate or out-of-range entries,
  and advances the Diablo content schema to `1.407.0`; older direct callers
  retain the compatibility fallback.
- Connected native missile processing now retains authoritative presentation
  missiles for animation but discards stale legacy missiles before their
  native gameplay callbacks can run; offline sessions retain legacy missiles.
- Connected native towner processing now fails closed at its public helper so
  local quest-derived towner state cannot bypass server-owned presentation;
  offline sessions retain legacy towner animation and lifecycle behavior.
- Connected native item processing now fails closed at its public helper so
  legacy item animation and multiplayer tile cleanup cannot mutate
  server-owned world-item state; offline sessions retain the legacy path.
- Connected native quest ticking now fails closed at its public helper so legacy
  portal creation, quest-variable transitions, and local level changes cannot
  bypass server-owned world progression; offline sessions retain the legacy
  path.
- Connected native quest completion, set-piece drawing, and legacy multiplayer
  quest synchronization helpers now fail closed as well; authoritative quest
  state and world mutations remain server-owned.
- Connected native monster spawn, golem, and death lifecycle helpers now fail
  closed at their public entry points too; server-owned monster snapshots
  cannot be bypassed by a direct legacy callback.
- Connected native legacy level-transition helpers now fail closed at their
  public entry points; authoritative movement and restart presentation continue
  through the dedicated server-backed runtime path.
- Public native simulation helpers now fail closed while connected so
  movement, combat, lifecycle, trap, door, and other world mutation cannot
  bypass the server-owned game loop; offline sessions retain the legacy
  simulation.
- Connected native Telekinesis now fails closed at the public legacy command
  helper; authoritative target interaction remains server-owned.
- Connected native automatic pickup now fails closed at the legacy item-command
  boundary; authoritative world-item pickup remains server-owned.
- Connected native unique-monster conversations no longer mutate the local
  monster mode before submitting an authoritative dialogue command; legacy
  mode changes remain available offline.
- Connected native Ctrl-click stash-to-inventory transfers now submit an
  authoritative stable-reference move instead of mutating the local inventory
  and stash directly.
- Connected native inventory actions now fail closed before the legacy item-use
  fallback can remove or mutate unsupported local items; scroll and rune uses
  retain only their immediate authoritative cast-intent route.
- Native, Godot, and saved-state validation now require zero magnitude for all
  typed status behaviors, including Etherealize, keeping malformed authoritative
  snapshots fail-closed across clients and persistence.
- Connected native level-transition presentation no longer clears or
  rebroadcasts server-owned Mana Shield and Reflect state after an authoritative
  restart or transition; disconnected sessions retain the legacy lifecycle.
- Native authoritative snapshots now accept protocol-defined Etherealize status
  effects and project the native Etherealize spell flag for local and remote
  player presentation.
- Connected native sessions no longer mutate or broadcast legacy Reflect
  charges; authoritative snapshots own the connected status effect.
- Connected native sessions no longer create or remove Mana Shield through
  legacy local lifecycle commands; shield state is server-owned and projected
  from authoritative snapshots.
- Connected native sessions no longer apply the legacy periodic `DRAINLIFE`
  equipment tick; the authoritative server owns that damage and its death
  consequences.
- Connected native death presentation no longer emits legacy death commands or
  synthesizes local gold, equipment, held-item, or ear drops; the authoritative
  server owns those consequences.
- Native object operation now fails closed when an authoritative session has
  no projected entity at the selected object position, preventing fallback to
  legacy local object behavior.
- Native debug item and monster spawning now fails closed in authoritative
  mode instead of mutating local inventory or monster pools.
- Native debug level reset, dungeon warp, quest warp, and custom-map warp
  commands now fail closed in authoritative mode instead of changing local
  level, seed, or visited-state data.
- Native debug quest activation now fails closed in authoritative mode;
  read-only quest diagnostics remain available against projected state.
- Native player-mutating debug commands now fail closed in authoritative mode,
  including god mode, arrow effects, resource adjustments, attributes, level,
  gold, and spell-level changes.
- The native debug towner-travel command now fails closed in authoritative
  mode instead of teleporting the local player through legacy spell logic.
- The native debug towner command now fails closed in authoritative mode,
  preventing direct legacy quest and store mutation through debug entry points.
- Connected native unique-monster conversations now fail closed when no
  authored server quest dialogue is available instead of entering legacy quest
  mutation handlers.
- Connected native towner clicks now enter supported store menus through a
  presentation-only path and fail closed for unsupported towners, preventing
  legacy quest handlers from mutating local state when no server interaction
  is available.
- The authoritative quest catalog extension preserves the existing
  `DiabloSimulation` positional constructor argument order for compatibility.
- Quest content activation now rejects duplicate native quest ordinals before
  they can reach protocol snapshot projection.
- Authoritative quest snapshots now optionally carry validated native quest
  ordinals, allowing connected native clients to hydrate legacy quest state
  from server-owned data while older snapshots retain compatibility behavior.
- Native authoritative object projection now prefers a compatibility object at
  the projected server position before falling back to type matching, reducing
  presentation misassociation when legacy levels contain duplicate object types.
- Connected single-player transition progress no longer writes legacy per-level
  save files before server-owned moves; multiplayer delta saves remain protected by
  the existing delta-state policy.
- Connected single-player level transitions now bypass legacy visited-level
  `LoadLevel()` restoration in dungeon, town, and set-level branches, keeping
  the server-backed runtime on authoritative state.
- Native monster level setup no longer writes the legacy Zhar-unavailable quest
  state while connected; monster presentation remains reconstructed from the
  authoritative snapshot.
- Connected native set-piece object initialization no longer rewrites Blind, Blood,
  or Warlord quest-message state or loads their legacy map fragments; authored
  server state remains authoritative for those connected sessions.
- Native authoritative world presentation now removes unclaimed local monster
  slots after each snapshot, preventing legacy level-load monsters from remaining
  visible or occupying tiles outside server-owned state.
- Connected native level loads no longer generate legacy random or quest items.
  Authoritative world-item projections remain the only connected-session source of
  world-item state, and projected Cornerstone items cannot complete the legacy
  local quest.

- Native authoritative snapshot application now preflights every projected
  entity, session, geometry, quest, and vendor state before committing any
  session mutation; malformed snapshots therefore cannot leave partial player
  or world state behind.
- Native authoritative stash item use now routes spell books, supported
  consumables, and oils through stable server-owned item intents; unsupported
  stash actions fail closed instead of invoking legacy local gameplay.
- Native authoritative held Rune Bomb and Cathedral Map interactions now route
  through projected server-owned dungeon objects; missing projections fail
  closed instead of opening local set pieces.
- Native authoritative inventory Cathedral Maps now also fail closed when no
  projected grave interaction is available, preventing legacy DoomFlag changes.
- Native authoritative unique-monster quest lookup now includes eligible
  activation-stage quests, so conversations such as Zhar and Warlord cannot
  fall through to local quest activation or reward mutation.
- Native authoritative unique-monster conversations now fail closed when the
  native target has no authoritative monster projection, preventing stale
  local targets from reaching legacy quest mutation.
- Native authoritative path-based trap disarm now submits the server-owned
  disarm intent before the legacy action loop, preventing local trap RNG and
  state mutation from preceding authoritative validation.
- Native authoritative clients no longer run the legacy Mana Shield depletion
  loop or emit CMD_REMSHIELD; server-projected status state remains the only
  connected-client source of truth.
- Connected native local save/load entry points and legacy persistence writers
  now fail closed because server-backed sessions own persistence; legacy local
  binary saves remain offline-only.
- Connected classic store dialogs now fail closed for premium Griswold
  purchases because no authoritative command exists; offline clients retain
  the legacy premium purchase path.
- Connected native `/arena` and `/arenapot` chat commands now fail closed
  because their legacy level/item mutations have no authoritative command;
  offline multiplayer retains the legacy commands.
- Connected native clients now ignore legacy item protocol messages;
  authoritative world-item and inventory commands remain server-owned.
- Connected native clients now ignore legacy `CMD_ADD*` attribute messages;
  character-panel allocation remains server-owned.
- Connected native clients now ignore legacy level and portal transition
  messages; server-owned movement, portals, and level state remain authoritative.
- Connected native clients now ignore legacy combat, damage, resurrection,
  shield, reflection, and monster-spawn messages; the server owns those effects.
- Connected native clients now ignore legacy quest, spell-level, object,
  set-piece, and friendly-mode synchronization messages; the server owns them.
- Connected native level loads now bypass legacy quest resynchronization that
  mutates local quest, object, and monster state; authoritative projections own
  the connected world.
- Connected native return-level loading no longer mutates the legacy Betrayer
  quest marker; the server owns quest progression in connected sessions.
- Connected native set-piece and crypt level loading no longer mutates legacy
  quest markers or Na-Krul room geometry; authoritative world projections own
  those connected-state changes.
- Connected native crypt loading no longer restores a legacy Cornerstone item
  from local Hellfire options; item state remains server-owned.
- Connected native clients now ignore legacy player-info, level, and remote
  attribute synchronization messages; authoritative snapshots own presentation.
- Connected native clients now ignore legacy movement, targeting, attack, spell,
  talk, and object-action messages; typed server intents own simulation.
- Connected native clients now ignore legacy monster-delta synchronization and
  debug-experience mutation messages; authoritative snapshots own progression.
- Connected native clients now bypass legacy delta state helpers for monsters,
  items, objects, portals, level save/load, and legacy delta transport; the
  authoritative session remains the only connected-client source of truth.
- Native authoritative snapshot application now rejects player, moving-monster,
  world-item, object, projectile, corpse, and portal positions outside native
  coordinate limits or supplied level geometry, matching the Godot boundary.
- Native authoritative snapshots now enforce one nonzero entity-ID namespace
  across players, monsters, world items, objects, projectiles, and corpses.
- Native authoritative snapshot application now rejects malformed player action
  timing, combat-mode combinations, casts, deaths, transitions, and blocked moves.
- Native authoritative monster snapshots now reject invalid combat ranges,
  resistances, hit chances, death timelines, ownership, and dialogue identity.
- Native authoritative projectile snapshots now reject invalid damage channels,
  lifecycle ranges, behavior/phase values, and typed presentation identities.
- Native authoritative sessions now retain sorted server-owned corpse positions
  without attempting to synthesize missing native corpse visual metadata.
- Native authoritative sessions now retain server-owned RNG, victory, difficulty,
  and active world-mutation metadata without enabling local simulation.
- Native authoritative sessions now retain sorted effective level geometry,
  native layers, blocked cells, and warp markers for presentation consumers.
- Native world projections now carry authoritative level geometry and expose
  fail-closed blocked-cell and missile-blocked queries.
- Native authoritative event application now consumes typed victory events for
  the local participant's server-owned Diablo-kill rank.
- The native debug console now stays native-only in authoritative mode: it
  skips script prelude loading, autocomplete, module help, and interpreter
  fallback while exposing the registered server-compatible command list.
- Compatibility item-state hydration now restores omitted authored display
  names and native cursor graphics by stable/native identity or unambiguous
  `misc_id`, while preserving explicit unique-item presentation overrides.
- The same identity hydration now restores omitted immutable item type,
  equipment classification, requirements, native index, and authored effect
  flags without replacing nonzero runtime or affix state.
- The Godot HUD now selects the server-owned identified item name only when
  authoritative state marks the item identified, with a compatibility fallback
  for older snapshots that omit the base name.
- The Godot protocol boundary now rejects oversized ordinary inventory,
  belt, equipment, or held-item footprints instead of silently clamping them.
- The same boundary now rejects duplicate nonzero player item seeds so stable
  inventory, belt, equipment, stash, and held-item references remain unambiguous.
- The Godot protocol boundary now rejects impossible core item ranges while
  retaining signed affix modifiers and omitted zero-valued legacy defaults.
- The Godot protocol boundary now cross-validates active vendor state, rejecting
  inconsistent session IDs, duplicate stock slots, unsupported prices, and
  invalid stock item identities before presentation consumes them.
- Godot now rejects malformed native inventory topology, including invalid or
  duplicate belt/equipment slots and inconsistent inventory-grid footprints.
- Live Godot authoritative-server overlay fixtures now track the shipped
  content schema so the complete focused live class starts against compatible
  base and overlay packs.
- Godot now rejects out-of-range, duplicate, or malformed server-owned ability
  and memorized-spell projections before spell presentation consumes them.
- Godot now rejects player gold, stash gold, stat points, Reflect charges, and
  character levels outside the native-compatible projection ranges.
- Godot now rejects malformed authoritative projectile identity, effect ranges,
  lifecycle codes, and presentation metadata before combat presentation.
- Godot now rejects malformed authoritative monster combat ranges, resistances,
  hit chances, death timelines, and bounded native scalar state.
- Godot now rejects malformed monster occupancy projections whose future cell is
  not adjacent while moving or does not match the current cell while stationary.
- Godot now rejects zero-identity objects, undefined object effect/presentation
  kinds, and contradictory breakable/collision state before interaction routing.
- Godot now rejects unrepresentable world-item prices, undefined typed item
  metadata, and NUL-containing item spell display names.
- Godot now rejects projected entity coordinates outside the native tile range,
  including legacy snapshots that omit level geometry.
- Godot now rejects projected player movement actions whose reserved destination
  is outside the supplied level bounds or occupies a server-declared blocked cell,
  while retaining compatibility for snapshots without level geometry.
- Godot now rejects non-empty visited-level projections that omit the player’s
  current level, matching the server-owned quest-gate state invariant.
- Godot now rejects partially populated or negative player attribute projections
  before the presentation HUD consumes authoritative character state.
- Godot now rejects undefined selected-ability targeting enums while preserving
  the additive unspecified default for older snapshots.
- The native authoritative projection now rejects undefined ability, memorized
  spell, and item targeting enums before converting them to native state.
- Native and Godot authoritative item projections now reject unsigned prices
  that cannot be represented by the legacy native integer price field.
- Native authoritative projection and UI validation now reject duplicate
  nonzero player item seeds across inventory, equipment, belt, stash, and held state.
- Native and Godot player-item projections now reject incomplete item identity
  metadata before converting or applying inventory, equipment, or belt state.
- Native item conversion now rejects invalid damage, armor, charge, durability,
  typed metadata, and NUL-containing spell-display ranges before narrowing.
- Native authoritative object projection now rejects invalid effect and
  presentation kinds, quest-transition and reward metadata, projectile offsets
  and sources, and quest-sequence follow-ups before native conversion.
- Native authoritative portal projection now rejects non-native endpoint
  coordinates and inconsistent owned-portal lifetime or owner state.
- Native authoritative quest-state validation now rejects duplicate or missing
  quest identities, impossible progress, unavailable progress, and invalid
  native quest metadata before interaction gates consume the snapshot.
- Native authoritative quest/towner interaction validation now rejects invalid
  IDs, quest references, messages, ranges, and geometry positions before routing.
- Native authoritative geometry validation now rejects malformed dimensions,
  cells, layers, duplicate levels, and warp-trigger routes before projection.
- The authoritative Diablo save boundary now rejects the same out-of-range item
  prices before restoring player, world, or active-store state.
- The native authoritative projection now rejects non-empty visited-level sets
  that omit the player’s current level, matching the server and Godot boundary.
- Use content-owned `Book` item tags for spell-book template generation,
  random-drop eligibility, and `ReadBook` admission; use authored `Gold`
  classification for unique-monster drop filtering, retaining native fallbacks
  for older packs.
- Recharge admission now treats active non-empty item tags as authoritative,
  preventing legacy staff misc IDs from overriding authored non-staff metadata.
- Consumable action magnitudes now resolve through stable or native item
  catalog identity before using the legacy Spectral Elixir fallback.
- Consumable and book action admission now resolves authored item actions by
  stable or native identity when serialized state omits the action kind.
- `ReadBook` now resolves authored spell identity and minimum-magic requirements
  by stable or native item identity when compatibility state omits those fields.
- Recharge validation and pricing now resolve authored maximum charges and spell
  identity through stable or native item metadata when state omits those fields.
- Sharpness Oil now hydrates omitted target weapon damage from authored item
  metadata before applying its native increment.
- Store sale, repair, and recharge prices now resolve omitted item values from
  unambiguous authored metadata, including compatibility states keyed by
  `misc_id`.
- Equipped physical damage now resolves omitted weapon ranges from authored
  item metadata before calculating the server-owned player damage profile.
- Equipped fixed armor now resolves omitted armor values from authored item
  metadata; randomized armor ranges remain unresolved unless state supplies the
  rolled value.
- `ReadBook` now resolves omitted spell and minimum-magic fields from
  unambiguous authored book metadata keyed by `misc_id`.
- Store intent processing now hydrates omitted inventory footprints from
  unambiguous authored item dimensions before relocation and stash moves.
- World-item pickup now resolves omitted/default inventory footprints from
  authored item dimensions before placement.
- Snapshot capture and restore now hydrate omitted/default inventory footprints
  from authored item dimensions before persistence validation and world use.
- World-item gold pickup and snapshot restore now hydrate omitted gold values
  from the authoritative world-item price while preserving mismatch rejection.
- Snapshot restore now hydrates omitted authored item values, base damage,
  durability, maximum charges, spell sources, and item actions across inventory,
  equipment, belt, stash, held, world-item, and store state without changing
  stable or native item identity.
- Live world-item pickup now materializes the same authored metadata before
  mutating player state.
- Death-gold classification now treats active non-empty item tags as authoritative
  instead of allowing a conflicting native type/index to override them.
- Belt classification now treats active non-empty item tags as authoritative
  instead of allowing conflicting native placement metadata to override them.
- Two-handed and equipment-slot classification now treats active non-empty slot
  tags as authoritative before using native equipment-location fallback.
- Staff classification now treats active non-empty staff/non-staff tags as
  authoritative before using the native item-type fallback.
- Weapon classification now treats active non-empty weapon/non-weapon tags as
  authoritative before using the native item-class fallback.
- Weapon and armor subtype classification now treats active non-empty subtype
  tags as authoritative before using native item-type fallback.
- Weapon and armor subtype state classification now also resolves unambiguous
  content tags by misc ID when native item-index lookup is unavailable.
- Store armor-target validation now treats active armor tags as authoritative,
  including misc-ID resolution when native item-index lookup is unavailable.
- Oil-target validation now treats active `Misc`, `Gold`, and `Quest` tags as
  authoritative before falling back to native item-class values.
- Auto-equip hand selection now compares content-first weapon classification,
  preserving native `ItemClass` behavior only for tagless compatibility states.
- `ReadBook` admission now resolves authored `Book` tags by unambiguous `misc_id`
  when a compatibility state has no stable or native item identity.
- Recharge capacity and spell pricing now resolve unambiguous charged-item
  metadata by `misc_id` when compatibility state omits native item identity.
- Consumable action kind and amount now resolve unambiguous authored metadata by
  `misc_id`, with ambiguous mappings rejected rather than guessed.
- Arena-only admission now resolves authored `arena_only` metadata by `misc_id`
  when compatibility state lacks stable/native item identity.
- Gold classification now resolves authored `Gold` and `Currency` tags by
  `misc_id` before applying the native type/index fallback.
- Item-backed spell-source classification now fills omitted spell and charge
  fields from unambiguous charged/consumable metadata keyed by `misc_id`.
- Authored `usable=false` consumable metadata is now enforced by `misc_id` when
  native/stable item identity is unavailable.
- Targetless consumable actions now enforce authored `usable=false` metadata
  before applying potion, elixir, or oil effects.
- Consumable Identify admission now resolves authored action metadata by stable,
  native, or `misc_id` identity and enforces authored usability.
- Sorcerer Staff Recharge execution now resolves omitted spell and charge
  metadata through the active charged-item catalog, including `misc_id`.
- Vendor Recharge now writes resolved authored maximum charges back into item
  state when compatibility state omitted that field.
- Repair validation, pricing, and mutation now resolve authored maximum
  durability through stable, native, or `misc_id` identity.
- Blacksmith and Fortitude oil mutations now hydrate omitted target durability
  from authored item metadata before applying native effects.

- Externalize Arena Potion admission through the optional `arena_only` Boolean
  field in `items.tsv`, preserving the native misc-ID compatibility fallback for
  older packs and advancing the Diablo content schema to 1.406.

- Externalize the native Multiple Arrows perpendicular spread distance through
  the optional `multiple_arrows_spread_distance` field in `combat.tsv`,
  preserving the one-cell compatibility fallback for older packs and advancing
  the Diablo content schema to 1.405.

- Externalize the native Multiple Arrows follow-up delay through the optional
  `multiple_arrows_delay_ticks` field in `combat.tsv`, preserving the two-tick
  compatibility fallback for older packs and advancing the Diablo content
  schema to 1.404.

- Externalize the native Multiple Arrows count through the optional
  `multiple_arrows_count` field in `combat.tsv`, preserving the three-arrow
  compatibility fallback for older packs and advancing the Diablo content
  schema to 1.403.

- Externalize Skeleton King summon candidate selection through the optional
  `skeleton_summon_candidate` field in `monster_types.tsv`, preserving the
  native-AI and complete-tuning compatibility fallback for older packs and
  advancing the Diablo content schema to 1.402.

- Externalize the native class-specific ranged-arrow speed level divisor
  through `classes.tsv`, preserving symbol-based compatibility for older class
  tables and advancing the Diablo content schema to 1.401.

- Declare the native fifteen-point Counselor Charged Bolt monster payload in
  shipped `monster_missiles.tsv` content while retaining the legacy fallback.

- Externalize the native Counselor Charged Bolt projectile count through
  `monster_missiles.tsv`, preserving the three-projectile fallback for older
  packs and advancing the Diablo content schema to 1.400.

- Externalize the native MagmaBall initial launch advance through
  `monster_missiles.tsv`, preserving the three-quanta fallback for older packs
  and advancing the Diablo content schema to 1.399.

- Externalize native spell-book template item-level eligibility through
  `items.tsv`, preserving the legacy fallback for older packs and advancing the
  Diablo content schema to 1.398.

- Externalize the native unique-monster experience multiplier through
  `combat.tsv`, preserving the native two-times fallback for older packs and
  advancing the Diablo content schema to 1.397.

- Externalize the native monster Flash damage multiplier through `spells.tsv`,
  preserving the level-times-two fallback for older packs and advancing the
  Diablo content schema to 1.396.

- Externalize Counselor's adjacent Flash-admission chance band through
  `monster_types.tsv`, preserving the native 20/2 fallback for older packs and
  advancing the Diablo content schema to 1.395.

- Reject cross-category world entity-ID collisions during authoritative world
  construction, matching the save/snapshot identity invariant before runtime
  commands can observe ambiguous targets.

- Centralize runtime entity-ID occupancy checks across world-item, generated
  monster, and authored-projectile allocators so corpses and portals cannot be
  skipped by a server-owned allocation path.

- Add the optional `unique_monster_placements.tsv` contract for validated,
  content-owned placement of generated unique encounters. Placements resolve
  unique identity selectors, monster types, level geometry, stable entity IDs,
  reserved entity IDs from existing monsters and authored world objects,
  reserved warp/portal/object cells, and deterministic spawn seeds before
  materializing through the authoritative planner; shipped packs declare the
  empty table until native set-piece coordinates are authored.

- Defer unique-anchored native DUN fragments until the matching generated
  encounter is materialized; fixed-object fragments still apply during content
  activation, and deferred fragments now compose into the same authoritative
  level state on initial or entered-level spawn.

- Preserve Snotspill's authored intermediate banner dialogue identity through
  the staged server dialogue contract; omitted next-message values remain
  backward compatible with next-stage lookup.

- Allow authored world mutations to follow one validated server-owned unique
  encounter by identity, removing the fixed-object-anchor limitation for future
  generated set-piece transitions while preserving existing object anchors.

- Extend native DUN fragment composition with validated unique-encounter anchors;
  generated anchored fragments are now applied after encounter planning while
  existing fixed-object fragments remain unchanged.

- Refresh the migration verification baseline to record the current Diablo
  module result: 1,452 of 1,452 focused module tests pass.

- Align Na-Krul's unique-spawn regression coverage with the native weakening
  rule: both current and maximum health are reduced to 50 percent.

- Externalize the native Lachdanan dialogue completion boundary. The Hellfire
  quest row now declares that a successful Golden Elixir hand-in defeats the
  unique target after its Veil of Steel reward, preserving the target's dead
  state through authoritative snapshots and saves.

- Extend authoritative unique-encounter dialogue projection to legacy
  item-gated hand-ins such as Lachdanan. Initial and completion message
  identities now remain on the target across snapshots instead of being
  presentation-only event data.

- Preserve the current staged unique-monster dialogue identity in the
  authoritative monster snapshot and protocol. Accepted dialogue stages now
  advance the target to the next authored message (or clear it at the terminal
  stage), allowing reconnecting clients to present the same encounter state.

- Add `quest_sequence_variants.tsv` for complete content-declared native
  ordered-quest permutations. Hellfire Na-Krul tome slots now select one of
  the six native layouts with the authoritative Diablo RNG before simulation
  state is created.

- Add
`native_dun_fragment_objects.tsv` for validated anchor-relative
  placement of existing authored objects. The Hellfire Na-Krul tome rows now
  follow the encounter anchor instead of duplicating absolute coordinates.

- Add optional validated `.til`/`.sol` collision composition to native DUN
  fragments. External tile definitions now expand fragment mega-tiles into
  anchor-relative solid and missile-blocked cells without hard-coded gameplay
  assumptions; the checked-in Hellfire fragment remains presentation-only until
  licensed `l5.til`/`l5.sol` assets are supplied.

- Externalize the Hellfire Na-Krul completion's four native room-opening
  object-layer assignments as a validated mutation anchored to the authored
  encounter object. The server applies the room mutation together with the
  deterministic Na-Krul spawn, persists it through active mutation state, and
  projects the resulting native object layer through the authoritative geometry
  protocol.
- Compose the Hellfire `uberroom.dun` set-piece as a validated native fragment
  relative to the Na-Krul encounter anchor, retaining its native mega-tile
  layer and projecting the resulting tile data through level geometry.

- Externalize the Hellfire Na-Krul tome completion's server-owned native
  monster materialization. The validated content pack now binds the Na-Krul
  identity, applies its native health/armor/resistance weakening transition,
  scales the spawned target through the selected difficulty, and preserves a
  deterministic entity identity; dynamic room-opening geometry remains a
  documented follow-up seam.

- Add an optional standalone-server starting-stat-points override and live
  Godot coverage for allocating a server-owned Vitality point through the
  shared protocol and reconciling the resulting resources from a snapshot.

- Present the server-selected Normal, Nightmare, or Hell difficulty in the
  Godot HUD and add live Hell projection coverage.

- Add live Hellfire coverage proving that the server-selected difficulty also
  scales authoritative monster hit points before they reach the Godot client.

- Enforce the authoritative stash session for Repair, Recharge, and Identify
  commands that target stash items; crafted closed-session requests now fail
  with `NotAllowed` instead of bypassing the presentation boundary.

- Add live Godot stash-session coverage for authoritative open/close control,
  wallet/stash-gold reconciliation, stable equipment-to-stash moves, and
  rejection of stash moves after the session closes.

- Add live Godot protocol coverage for moving an equipment item into an open
  authoritative stash and identifying it there through a stable page/cell
  reference, plus Warrior Repair and Sorcerer Recharge stash targets.

- Allow Godot's Warrior Repair, Sorcerer Recharge, and Bard Identify service
  controls to target stable-reference items in an open authoritative stash;
  stale or closed-session targets remain disabled.

- Add explicit server-owned stash open/close session intents plus validated
  stash-gold deposit and withdrawal commands. Item transfers and gold changes
  now fail closed while the stash session is closed, and vendor/stash sessions
  cannot overlap.

- Add Godot HUD stash session controls and paged 10x10 authoritative stash-grid
  presentation, including server-reconciled stash gold, stable-reference item
  moves, and page switching.

- Add server-owned 100-page stash state with native-compatible 10x10 page
  grids, stable page/cell references, overlap validation, inventory↔stash
  transfers, snapshot projection, and version-1 save round-tripping.

- Add the native authoritative stash projection and session boundary. Barmaid
  entry and close now use server-owned stash session commands; validated page
  grids, session state, and stash gold are projected into the legacy renderer;
  native gold deposit/withdraw uses authoritative commands; item cursor transfers
  now use the stable stash bridge. Normal stash drags, shift-click automatic
  withdrawals, and Ctrl-click inventory-to-stash placement are reconciled from
  authoritative snapshots.

- Extend native Identify, Repair, and Staff Recharge class-ability cursors to
  submit authoritative stash page/cell references and apply server snapshots.

- Expand Godot authoritative walk-projection coverage across all eight native
  adjacent directions and the start, midpoint, effect, and post-effect tick
  boundaries.

- Use the same authoritative action-tick walk projection for the Godot local
  player after bounded prediction has reconciled, preventing a snap to the
  occupied tile while the server-owned movement effect is still pending.

- Drive Godot remote-player walk presentation from the authoritative
  `started_at_tick`/`effect_tick` interval, while leaving discrete reserved
  tiles unchanged for gameplay and target selection.

- Project native spell-source target metadata through authoritative player
  snapshots and use it to prevent tile/world casts from submitting accidental
  creature entity IDs; older snapshots retain the legacy selection behavior.

- Route Godot spell casts at visible living monsters through stable
  authoritative entity IDs, preserving server-owned target validation and
  coordinate fallback for older clients.

- Route Godot Telekinesis casts at visible items, authored objects, and living
  monsters through the same server-projected world-entity targeting boundary.

- Add an idempotent server-owned `CloseStoreRequested` intent and Godot HUD
  control so vendor and Adria service sessions can end without relying on a
  town restart or stale active-store state.

- Route native authoritative visual-store closure and the Adria refill path
  through the same server-owned close intent, so native and Godot clients no
  longer leave an active vendor selected after the presentation closes.

- Refresh shipped-content assertions for the composed procedural level catalog
  and current Hellfire level set, keeping the full Diablo test suite aligned
  with validated external content.

- Restrict Godot world-object clicks to server-authored interactions. Ordinary
  presentation-only objects no longer submit `OperateObjectRequested`, while
  authored effects, quest links/transitions, rewards, follow-ups, and revealed
  traps continue to resolve by authoritative entity ID.

- Cross-validate Godot authored object quest links against the current quest
  snapshot so unknown quest references are rejected before interaction routing.

- Validate Godot quest identity uniqueness and core progress/variable bounds
  before quest, towner, and authored-object selectors consume the catalog.

- Route Godot clicks for distant world items, authored objects, quest/towner
  stages, and portals through authoritative target-cell movement before
  submitting the range-constrained interaction intent; the original intent is
  automatically submitted after the authoritative position reaches range.

- Validate Godot projected level blocked-cell indices, duplicate cells,
  transparency lengths, and native transparency bounds before movement or
  warp presentation consumes the geometry.

- Reject Godot projected entities whose positions fall outside supplied level
  geometry, including moving-monster future positions, while retaining the
  compatibility fallback when a level has no geometry projection.

- Reject Godot projected portal endpoints outside supplied level geometry while
  retaining the compatibility fallback when a portal level has no geometry
  projection.

- Expose authoritative equipment rows in the Godot HUD, route service actions
  through stable equipment references, and submit explicit inventory/equipment
  transfers through the existing server-owned item protocol.

- Add snapshot-driven Godot attribute display and one-point level-up controls
  backed by the existing server-owned allocation command.

- Preserve server-owned Reflect charges when applying authoritative native
  player snapshots, with validation against the native 16-bit range.

- Apply server-owned visited-level state to the authoritative native player's
  `_pLvlVisited` flags, rejecting invalid or duplicate level IDs.

- Preserve authoritative Diablo rank, Reflect charges, and visited levels in
  native remote-player presentation state.

- Reject inconsistent authoritative player/vendor active-store identities at
  the native session boundary.

- Stop projecting a second native walk destination after the authoritative
  movement effect has already advanced the player position.

- Align Godot walk-position projection with the authoritative effect-applied
  flag so reconciled snapshots do not schedule a second client-side step.

- Reconcile a matching pending Godot move command against the server action
  before applying later queued movement predictions.

- Filter retained native remote-player presentation by authoritative level
  before drawing while preserving the level-zero compatibility wildcard.

- Reset Godot entity-position interpolation when an authoritative level
  identity changes, preventing cross-level movement trails.

- Reset Godot player presentation when an explicit transition action or a
  same-level non-walk relocation arrives, while retaining smooth interpolation
  for ordinary one-tile authoritative walks.

- Bound Godot local movement prediction to 16 unacknowledged commands so
  disconnected or delayed acknowledgements cannot grow the prediction queue.

- Clear pending Godot movement prediction when the authoritative local player
  identity or level changes, preventing old-level inputs from crossing levels.

- Reset Godot presentation prediction when reconnecting while retaining the
  server-side pending-command retry ledger, and clear the prior event history
  so stale dialogue and victory feedback cannot survive into the new stream.

- Ignore older Godot snapshots after reconnect or queued delivery while still
  applying acknowledgements carried by the same model message.

- Preserve Godot event batches that arrive immediately before their snapshot;
  stale event ticks are ignored and command-result presentation stays monotonic.

- Bound Godot's retained authoritative event history to 256 entries and expose
  a monotonic sequence window so feedback remains synchronized when old entries
  roll off during long-running sessions.

- Reject stale native remote-player snapshots before they can regress the
  compatibility presentation's position, action, or level state.

- Ignore stale native local-player snapshots and their older event batches
  during polling, preserving the last authoritative local projection.

- Harden the native authoritative player-state projection with a monotonic
  server-tick guard. Direct adapter calls now reject delayed snapshots without
  replacing the last accepted state, matching the session-level ordering rule.

- Route native authoritative object activation for every projected server-owned
  object effect, including world mutations, spell effects, quest links, and
  authored rewards; ordinary objects without authoritative behavior retain
  their legacy local path.

- Validate authored object transition, reward, projectile, and follow-up
  metadata at the Godot protocol boundary before presentation or interaction
  routing can consume it.


- Preserve the server-owned Diablo kill rank when applying authoritative native
  player snapshots, keeping victory progression aligned with C# state and saves.

- Move the optional clock presentation to the Godot HUD with a tested,
  client-only `HH:mm:ss` formatter while retaining the legacy native script.

- Project native server-backed healing feedback directly into the compatibility
  client's floating-number renderer, completing the local damage/healing/
  experience presentation seam while retaining legacy Lua mods outside the
  authoritative path.

- Align native replay item, monster-direction, and historical-level hashing
  with authoritative C# snapshots, preserve checked-in base item names in the
  native store fixture, and add native Firebolt player-collision parity for
  ordinary and Hellfire shield-block paths. Skip the petrified-target Bone
  Spirit hit roll to preserve native RNG order, keep generic native monster
  pursuit out of autonomous ranged lanes, and align Succubus/Lich projectile
  admission, damage, block, hit-recovery, and canonical monster-range state.
  The rebuilt native replay runner now passes all 96 tests.

- Add optional `--game-adapter` host selection. The generic adapter catalog now
  selects a named compatible factory when configured, while preserving default
  ambiguity rejection and failing closed for unknown or incompatible IDs.

- Add end-to-end TCP restart recovery coverage: a saved session now survives
  host disposal, restores through a fresh adapter and save root, and retains
  the persisted character state over a new requested identity.

- Route the compatibility system tick wrapper through the shared native timer,
  preserving demo/replay timing while removing its direct SDL dependency.

- Route native server-backed input, polling, portal, store, quest, and ability
  request timestamps through the same shared timer, keeping replay-aware time
  consistent across the compatibility adapter.

- Move native server-backed local damage and experience floating-number
  presentation into the compatibility renderer, so those authoritative event
  projections no longer require a Lua presentation hook.

- Extend the end-to-end TCP ruleset-replacement soak to 32 generations for a
  connected session. The session migrates through the live host barrier,
  continues operating on every replacement, retires each prior target, and
  resumes with the final published independent ruleset identity. Two connected
  sessions remain covered through the eight-generation migration and resume
  boundary.

- Add a shared Hellfire HorkDemon Hork Spawn replay fixture covering
  server-owned summon admission, native travel timing, and C++/C# checkpoint
  parity.

- Add shared Hellfire HorkDemon decision-delay replay coverage, including
  persisted delay state and the native first-tick decrement in both runners.

- Add shared Hellfire Hork Spawn player-impact replay coverage for native hit
  RNG, one-point Q6 damage, GotHit timing, and projectile removal.

- Preserve the native Hork Spawn projectile through a seeded player-hit miss
  until its remaining travel lifetime expires, with a shared C++/C# checkpoint.

- Add multiplayer Hork Spawn secondary-player block parity, including native
  block RNG, Block timing, and projectile removal ordering.

- Add multiplayer Hork Spawn secondary-player impact parity, including Q6
  damage, GotHit timing, and projectile removal ordering.

- Add Hork Spawn current-tile breakable-object collision parity, including
  object destruction and projectile removal ordering.

- Add primary-player Hork Spawn shield-block parity through normal replay
  equipment seeding, including native block admission and timing.

- Route Godot world clicks through authoritative portals, objects, traps, and
  authored quest/towner stages before falling back to spell casting or movement;
  trap clicks fail closed when the local player lacks Trap Disarm.

- Continue authoritative quest/towner stage selection past out-of-range active
  stages, and add live Godot protocol coverage for an authored itemless,
  completed-quest dialogue stage submitted through `TurnInQuestRequested`.

- Add live Godot protocol coverage for server-owned Holy Bolt admission and
  undead-only damage using an external class, spell, level, and monster overlay.

- Harden Godot snapshot validation so authored quest/towner interactions must
  reference a quest present in the same server-owned snapshot.

- Extend that fail-closed validation to require authored interaction positions
  to fit the projected level geometry whenever geometry is present.

- Add a server-driven Godot victory banner for typed Diablo defeat events and
  terminal victory snapshots, preserving the multiplayer/single-player level
  identity distinction without adding client gameplay state.

- Make Godot authored warp markers actionable at the input boundary: available
  markers continue through server-owned movement, while unavailable quest-gated
  markers provide client feedback without submitting a futile transition.

- Add live Godot protocol coverage that follows a validated authored progression
  warp through server-owned movement and level transition using a content overlay,
  then reconnects and verifies the destination state is retained.

- Harden authored warp-trigger projection validation at the Godot protocol boundary:
  destination level identities and coordinates must resolve to declared level
  geometry before presentation consumes a route, with malformed-snapshot coverage.

- Project authored level triggers through additive level-geometry snapshots and render
  available or quest-gated markers in the Godot presentation client.

- Add validated base progression warps from town through ordinary levels 16,
  preserving Hellfire's quest-gated dungeon warps. Bump the Diablo content
  schema to 1.383.

- Verify every shipped ordinary progression warp through the authoritative
  level-trigger execution path.

- Ship deterministic ordinary-level generation rows for base levels 3 through
  16, with explicit variant `levels.tsv` rows taking precedence. Bump the
  Diablo content schema to 1.382.

- Allow
`native_dun_objects.tsv` rows to carry an optional stable
  `object_symbol` cross-check while retaining numeric object-ID compatibility.
  Bump the Diablo content schema to 1.381.

- Add the validated
`native_dun_levels.tsv` catalog. Ordinary runtime levels
  can now acquire their native DUN object layers from immutable content assets,
  preserving authored collision geometry while enabling explicit object
  materialization. Bump the Diablo content schema to 1.380.

- Extend
`native_dun_objects.tsv` mappings to ordinary runtime levels as well
  as set-level variants, with separate validated identity ranges. Bump the
  Diablo content schema to 1.379.

- Reconcile mapped native DUN object entities when active world mutations change
  projected object-layer cells. Unchanged mapped objects preserve authoritative
  state, stale entities are removed, and newly mapped cells are materialized
  deterministically. Bump the Diablo content schema to 1.378.

- Extend authored world mutations with validated `object_layer_rects`
  assignments. Native object-layer values now persist in effective level
  geometry for explicit object mapping and rematerialization. Bump the Diablo
  content schema to 1.377.

- Extend authored world mutations with validated `transparency_rects`
  assignments. Native transparency values now persist through active mutation
  state and project through level geometry, enabling effects such as the native
  Halls of the Blind transparency change. Bump the Diablo content schema to
  1.376.

- Add deterministic rectangle authoring to `world_mutations.tsv`. Optional
  `geometry_rect_add` and `geometry_rect_remove` values expand into validated
  movement and missile-blocking overlays while retaining the existing explicit
  cell-list columns. Bump the Diablo content schema to 1.375.

- Add optional authored projectile origins. `objects.tsv` rows can select
  `ActivatingPlayer` for Phasing projectiles while retaining target-origin
  compatibility by default; the source is validated and carried through
  snapshots, saves, protocol projection, and canonical hashes. Bump the Diablo
  content schema to 1.374.

- Add the validated `GrantSpellLevelAndSpawnProjectile` object effect so one
  authored interaction can grant a spell level and spawn its server-owned
  Phasing or Guardian projectile atomically. Bump the Diablo content schema to
  1.372.

- Allow a direct `WorldMutation` object to apply a validated cell overlay and
  then spawn one authored Phasing or Guardian projectile through its existing
  follow-up metadata. This covers the native set-piece interaction shape
  without hard-coding map data. Bump the Diablo content schema to 1.373.

- Allow `world_mutations.tsv` to target generated native set-level variants by
  stable set-level identity and active/pre-map selection; validate those cell
  overlays after set-level geometry materialization. Bump the Diablo content
  schema to 1.371.

- Allow `template_only=1` object rows to omit fake level and placement fields;
  ordinary world-object rows remain strict. Bump the Diablo content schema to
  1.370.

- Add validated `template_only` object rows. Behavior-only entries in
  `objects.tsv` can now serve as set-level/native-DUN templates without being
  materialized into ordinary world levels. Bump the Diablo content schema to
  1.369.

- Preserve native DUN object layers through authoritative geometry and add the
  optional
`native_dun_objects.tsv` mapping table. Explicit mappings now
  materialize deterministic server-owned objects, optionally cloning validated
  `objects.tsv` behavior templates, while unmapped native values remain inert.
  Bump the Diablo content schema to 1.368.

- Add optional validated set-level object behavior templates. A
  `set_level_objects.tsv` placement can now clone a matching `objects.tsv`
  entity, carrying server-owned quest, spell, mutation, reward, and
  presentation behavior without hard-coded set-piece logic; legacy bare
  placements remain compatible. Bump the Diablo content schema to 1.367.

- Add presentation-only native remote-player sprites from authoritative
  snapshots without inserting remote players into the native simulation pool;
  consume authoritative walk/attack/block/hit/spell/death modes for native
  graphic selection, select spell families from authoritative action IDs,
  project weapon/shield/armor sprite variants from the authoritative equipment
  list, position action animations from authoritative snapshot ticks, draw
  same-level friendly remote projections on the native automap and remote
  party-panel portraits, route party-panel hover/right-click inspection by
  authoritative entity ID, project inspection stats/spells/item layout, project
  authoritative one-tile walk destinations into native future positions for
  renderer interpolation without local movement simulation, reject invalid
  remote walk destinations, and add coverage for retaining, removing, timing,
  movement, and mapping the projected state.

- Add an in-client Godot character setup form. When no resume token or complete
  class/name identity is supplied, the client now collects the identity and
  sends it through the existing authoritative handshake.

- Share authoritative one-tile player-walk destination projection between
  Godot local prediction and remote presentation, keeping remote movement
  presentation-only while it interpolates toward the server-reserved tile.

- Prune Godot presentation positions when authoritative entities disappear or
  the session disconnects, preventing stale world visuals from surviving
  snapshot reconciliation.

- Resolve Godot combat and player-target clicks against a remote player's
  authoritative reserved walk destination as well as its occupied tile, while
  continuing to submit only the stable server entity ID.

- Add an additive `NovaBall` native projectile visual identity so the native
  compatibility renderer can present server-owned Nova projectiles without
  inferring the visual from gameplay behavior numbers; cover the mapping in
  C# protocol and native presentation tests.

- Add an additive `NovaBall` projectile presentation family to the shared
  protocol and Godot renderer. Godot now preserves the server-selected Nova
  identity while older snapshots continue to use behavior-based fallback.

- Make connected native controller interaction fail closed when a stale local
  object highlight has no matching authoritative entity; add routing coverage
  so the path cannot fall through to a legacy object command.

- Correct the server-owned attribute-allocation regression fixtures to submit
  valid client sequences and requested ticks; the full server suite now passes
  all 381 tests.

- Refresh the migration verification baseline: the Release Diablo suite now
  passes all 1,388 tests after rebuilding its current content/schema assembly.

- Record the complete managed Release verification baseline: Domain 11, Data
  26, Engine 43, Diablo 1,388, Server 412, and Godot protocol 145 tests pass.

- Expand server-side native projectile visual parity coverage to every
  supported visual family and explicitly verify non-sprite behaviors remain
  unspecified.

- Materialize the first supported authoritative projectile families in the
  native compatibility renderer. Their position and lifetime come from the
  server, and presentation-only missile objects skip local process, collision,
  and damage logic. The bridge now also covers Lightning segments, Flash layers,
  Resurrection beam, Flame Wave, Inferno, Apocalypse impact, Elemental, player
  arrows, Acid, Hork Spawn, monster Charged Bolt, and monster Arrow families,
  while unsupported or content-missing missile rows remain projection-only.

- Add the server-owned
`native_visual` projectile presentation identity. The
  native compatibility client now consumes exact sprite metadata rather than
  inferring visuals from gameplay behavior values; zero-valued metadata retains
  older-server fallback behavior and unknown explicit values fail closed.

- Handle connected Town Portal entry directly from projected portal state when
  no legacy native portal missile exists; disconnected sessions retain local
  portal behavior.

- Project authoritative portal snapshots into the native compatibility session
  and route connected Town Portal entry through `UsePortal`; disconnected
  sessions retain legacy `CMD_WARP` behavior.

- Prevent connected native trigger polling from emitting legacy level-change
  commands after the server has already applied a projected warp transition.

- Route the remaining connected native mouse held-item drop path through the
  authoritative drop intent; disconnected sessions retain `CMD_PUTITEM`.

- Match native level-up resource behavior in the authoritative progression
  path: class life/mana growth, full refills, `NoMana` suppression, and stat
  point awards now update server-owned state and snapshots.

- Add server-owned character attribute allocation through the shared protocol.
  Connected native character-panel allocation now spends projected stat points
  through an authoritative intent; disconnected sessions retain legacy
  `CMD_ADD*` behavior.

- Route connected native stash-close drops through the authoritative held-item
  drop intent when an adjacent world cell is available; legacy `CMD_PUTITEM`
  remains the disconnected compatibility path.

- Route connected native dungeon talker interactions from mouse and controller
  inputs through authoritative quest dialogue, and fail closed when controller
  item/object projections are missing instead of falling through to legacy
  commands. Controller quest-target movement now submits authoritative target
  cells; disconnected sessions retain the original compatibility paths.

- Route connected native controller monster, remote-player, and object
  interactions through authoritative entity IDs, including fail-closed handling
  when a projected entity is unavailable; legacy controller commands remain the
  disconnected compatibility path.

- Route connected native targeted scroll and rune cursor casts through the
  authoritative cast protocol with their stable consumable source reference;
  legacy spell commands remain available outside server-backed sessions.

- Route connected native direct scroll and rune item use through the same
  authoritative cast path, including non-keyboard controls and exact source
  references; legacy item spell commands remain available in compatibility mode.

- Route connected native Resurrection and Heal Other cursor actions through
  explicit authoritative player-targeted cast sources; legacy target commands
  remain available when the server-backed runtime is disconnected.

- Route connected native town-mouse and controller towner interactions through
  the authoritative towner boundary before retaining local store/dialogue
  presentation fallback.

- Add an additive target-cell movement intent backed by the shared deterministic
  grid-navigation primitive. Connected native cursor walking and repeated
  dungeon walking now submit server-owned target cells; the legacy directional
  and auto-path commands remain available for compatibility paths.

- Extend the Godot presentation client's left-click flow to submit the same
  target-cell movement intent when no authoritative cast or item interaction
  applies.

- Route connected native mouse entity-targeted monster/player attacks and
  ordinary object operations through authoritative entity references, including
  repeated actions. Add an additive cell-targeted attack intent that preserves
  native `CMD_SATTACKXY`/`CMD_RATTACKXY` direction and path semantics; connected
  native location-only and repeated attacks now use it, while legacy mode keeps
  the original commands. Adjacent primary native world-item clicks now submit
  the projected world-item entity through the authoritative pickup intent in
  both town and dungeon modes; distant clicks retain native auto-walk
  compatibility. Trap Disarm continues to submit the existing authoritative
  `DisarmTrapRequested` intent.

- Route connected native Telekinesis through the authoritative cast protocol for
  projected world items, objects, and monsters while retaining the legacy
  fallback outside server-backed sessions.

- Move native mod-option archive and table-data reload orchestration into
  `ModManager`; authoritative startup now reloads selected content without Lua,
  while legacy mode retains the existing Lua compatibility callback.

- Add live Godot protocol coverage for consuming a belt potion through its
  stable location and item identity; the running server accepts the request
  and removes exactly the referenced belt entry.

- Extend the Godot HUD's authoritative item-source flow to belt items. Books,
  targetless consumables, and oil sources now submit stable belt references;
  inventory-only vendor services retain their existing restrictions.

- Enforce native Arena Potion availability on active arena set-level runtime IDs;
  non-arena use is rejected server-side while legacy full-rejuvenation behavior
  remains unchanged.

- Add a Godot oil-target surface that lists authoritative equipment and
  inventory references. The HUD now submits the selected oil and target as one
  `UseConsumableRequested` command; the server remains authoritative for
  admissibility, mutations, and atomic source consumption.

- Extend the shared item-action protocol projection through the full
  server-owned action set. The Godot HUD can now submit one-item potion,
  rejuvenation, and elixir use through `UseConsumableRequested`; oil actions
  remain explicitly target-driven for the follow-up cursor flow.

- Refresh deterministic replay fixture content identities after schema 1.366
  so base and Hellfire replay checks validate the current manifested packs.

- Live protocol coverage now preserves unmatched co-delivered messages and
  chooses deterministic hit ticks for the shared-session melee fixture.

- Extend validated content packs to carry native `.dun`, `.til`, and `.sol`
  binaries alongside TSV tables. Asset bytes now participate in the manifest
  identity, identical inherited assets compose deterministically, conflicting
  assets reject composition, and the Diablo native geometry loader can consume
  the composed asset set directly.

- Complete native set-level bundles now materialize both pre-map and active-map
  variants during Diablo activation as server-owned levels in a reserved
  runtime-ID range. Legacy packs without the binary companions remain
  metadata-only, while partial bundles reject activation.

- Complete native set-level bundles now also materialize their validated active
  map object placements as server-owned interactive objects with deterministic
  entity identities. Legacy packs retain the metadata-only placement catalog.

- Complete native set-level bundles now validate their declared active-map spawn
  point against the materialized collision geometry before activation succeeds.

- Schema 1.366 adds an optional native `transparency_asset` to set-level maps.
  Declared transparency layers participate in complete-bundle validation and
  must match the active map dimensions, then remain attached to authoritative
  level geometry; older packs remain compatible.

- The Diablo module now has a validated `.dun` geometry materializer. It
  expands native mega-tile IDs through external `.til` d-piece definitions
  and external `.sol` properties into authoritative movement and missile
  geometry; missing native inputs reject activation. Binary `.til` and `.sol`
  loaders preserve the native little-endian records and supported tile flags,
  with strict length and unknown-bit validation. A composed native geometry
  loader now validates and materializes the `.dun`/`.til`/`.sol` bundle as one
  server-side boundary.

- The Diablo module now validates and preserves native `.dun` set-piece assets,
  including mega tiles, the reserved layer, monsters, objects, and transparency
  values. Collision interpretation and authoritative geometry materialization
  remain an explicit later boundary.

- Schema 1.364 externalizes native set-level map asset selection and entry
  points. Skeleton King's Lair, the Chamber of Bone, and Archbishop Lazarus'
  Lair now declare their pre-map/active-map assets and native spawn positions
  in server content; binary map synthesis and transitions remain separate.

- Schema 1.365 adds optional native `.til` and `.sol` asset paths to
  `set_level_maps.tsv`. Current set-level rows now carry the dungeon-type
  geometry inputs shared by their pre-map and active-map variants; older packs
  remain compatible until those paths are supplied.

- Schema 1.363 adds the validated `set_level_objects.tsv` catalog. Native
  fixed-object placements and template footprints for Skeleton King's Lair,
  the Chamber of Bone, and Archbishop Lazarus' Lair now live in the active
  server content contract; binary set-level map synthesis remains separate.

- Schema 1.362 adds validated staged-dialogue follow-ups. An accepted
  unique-monster conversation stage may apply one content-backed world-cell
  mutation after all quest, item, and reward validation succeeds.

- Schema 1.361 adds validated quest-sequence follow-up effects. A correctly
  ordered authored sequence step may apply one world-cell mutation or spawn a
  server-owned Phasing/Guardian controller with relative target offsets; the
  metadata persists through saves, snapshots, protocol projection, and hashes.

- Extend authored `SpawnSpellProjectile` objects with validated relative target
  offsets. The offsets persist through saves, snapshots, protocol projection,
  and canonical hashing, allowing native book coordinates to be expressed by
  content while retaining server-owned projectile construction.

- Add the validated `SpawnSpellProjectile` object effect. Authored object rows
  can now spawn a server-owned Phasing or Guardian controller from a stable
  spell identity at the object's position; unsupported projectile behaviors
  are rejected during content validation, and the existing projectile snapshot
  path carries the resulting state without client-side spell decisions.

- Add the validated `GrantSpellLevel` object effect. An authored book/lever
  row can now grant one level of an active catalog spell through
  `OperateObjectRequested`; optional `effect_spell_symbol` links resolve
  through the stable spell namespace, while the server validates the spell,
  persists the memorized-spell state, and projects it through the existing
  player snapshot.
  Set-level placement and native projectile/map effects remain separate seams.

- Add validated authored object rewards. Object rows can now generate a
  deterministic normal or unique item at a validated relative position when
  activated; reward metadata persists and projects through snapshots, while
  native set-level placement and spell effects remain later migration seams.

- Extend authored object effects with validated quest transitions. Object rows
  can now activate or complete a linked quest, set native `_qvar1`/`_qvar2`,
  and enforce required variable gates atomically with world mutations through
  `OperateObjectRequested`; set-level placement and spell effects remain later
  migration seams.

- Add validated content-backed world-cell mutations. Authored `WorldMutation`
  object effects now apply deterministic movement and missile-occlusion overlays
  on the server; active mutation IDs persist in snapshots, and the existing
  level-geometry projection exposes the effective result to connected clients.
  Native book/lever set-piece placement and spell effects remain a separate
  compatibility seam until their authored definitions are available.

- Project authored quest messages from content-owned `QuestSequence` object
  interactions through the authoritative dialogue event path; book/lever
  placement and set-piece mutations remain separate migration seams.

- Add schema 1.358 staged unique-monster dialogue rewards. Gharbad's second
  authored stage now generates a deterministic native-quality random drop on
  the authoritative server, with content-declared placement and item level.

- Render typed authoritative dialogue events in the Godot interaction panel
  using server-owned text with quest/message identity fallback; the protocol
  client no longer embeds native text tables.

- Add schema 1.357 dialogue message content. Authored Gharbad, Snotspill, and
  Warlord messages now load from `dialogue_messages.tsv` and flow through the
  authoritative event protocol, with ID-only compatibility when a pack omits
  the optional catalog.

- Add schema 1.356 staged Warlord dialogue content. The two deterministic
  `_qvar1` transitions for Warlord's authored message now use
  `quest_dialogue_stages.tsv` through `TalkToQuestRequested`, preserving the
  native combat gate at variable `3`.

- Add schema 1.355 item-gated staged dialogue. Snotspill's banner conversation
  and Tavern Sign hand-in now use the shared Ogden's Sign quest through the
  server-owned `TalkToQuestRequested` path, including item consumption and
  quest completion; the Banner set-piece map mutation remains a native fallback.

- Add schema 1.354 staged unique-monster dialogue content. Base Gharbad's four
  user-visible messages and monotonic quest-variable transitions now run through
  `quest_dialogue_stages.tsv` and the server-owned `TalkToQuestRequested` path;
  its random item reward and encounter-state presentation remain native
  compatibility fallbacks.

- Apply validated unique-monster selectors during procedural encounter
  materialization. Static rows remain strict, while generated selector rows
  bind exactly one matching unique target when its level is spawned.

- Add schema 1.353 base unique-monster selector declarations for Gharbad, Zhar,
  Warlord, and Lazarus. Static activation now defers unmatched selector rows,
  allowing those identities to be declared before their procedural encounters
  exist while retaining strict ambiguity checks.

- Enforce authored staged unique-monster dialogue combat gates. Targets remain
  non-combatant while their quest's `dialogue_completion_variable1` marker is
  pending; the Warlord path now rejects direct attacks and pauses autonomous
  AI until native marker `3`, then resumes its validated Skeleton-AI branch.

- Add schema 1.352 selector-based unique-monster identity bindings. Generated
  unique encounters can now use validated monster/native-AI selectors instead
  of requiring a static runtime entity ID; the Hellfire Defiler row exercises
  the new form while retaining strict single-target validation.

- Add schema 1.351 authored unlogged Pepin stages. Poisoned Water activation
  now preserves both native branches: the initial conversation requires a
  visited Caves/Poisoned Water level, while an active unlogged quest can still
  surface the same dialogue without local history.

- Add schema 1.350 authored Grave Matters visitation suppression. Hellfire
  Barmaid dialogue now honors the native `_pLvlVisited[21]` boundary through
  server-owned quest state and the shared native/Godot protocol.

- Add schema 1.349 authored Blacksmith visitation gates. Magic Rock and Anvil
  activation now use external quest/towner stages with server-owned
  `_pLvlVisited[4] || _pLvlVisited[5]` and `_pLvlVisited[9] ||
  _pLvlVisited[10]` checks; their existing item hand-ins and rewards remain
  validated content-owned turn-ins.

- Add schema 1.348 authored level-visitation gates. Server-owned player
  snapshots now persist native-compatible visited dungeon levels, and the base
  King Leoric activation stage is selected from visited Catacombs or Caves.

- Add schema 1.347 itemless and post-completion authored quest/towner stages.
  Ogden's King Leoric follow-up dialogue now advances the completed quest's
  native `_qvar1`/`_qvar2` markers through `TurnInQuestRequested`, and the
  protocol exposes the explicit `allow_completed` availability boundary.

- Expose content-owned quest/towner interaction stages to the Godot client.
  The protocol client validates and selects an adjacent server-declared stage,
  and the HUD submits the corresponding `TurnInQuestRequested` intent.

- Add schema 1.346 repeatable quest/towner interaction definitions. Base Black
  Mushroom now externalizes Adria's Fungal Tome activation, mushroom hand-in,
  brain dialogue, and Spectral Elixir completion stages through the same
  authoritative server boundary, including belt-item consumption.

- Add schema 1.345 authored unique-monster dialogue transitions. Warlord's
  follow-up conversation now advances native `_qvar1` from `QS_WARLORD_TALKING`
  to `QS_WARLORD_ATTACKING` through the shared `TalkToQuestRequested` boundary.

- Add schema 1.341 non-consuming towner dialogue stages. Hellfire Grave Matters
  now routes Gillian's Cathedral Map conversation through the server, preserving
  the native message while leaving the map and quest completion state unchanged.

- Add schema 1.342 variable-1-only completion-stage quest metadata. Base
  Poisoned Water now routes Pepin's completion dialogue and Ring of Truth reward
  through the authoritative server without mutating native `_qvar2`.

- Add schema 1.343 active staged item turn-in metadata. Base Black Mushroom now
  routes Pepin's Brain hand-in through the authoritative server, advances native
  `_qvar1 = 7`, and generates the Spectral Elixir without completing the quest.

- Add schema 1.344 base quest-reward pickup content. Optic Amulet and Arkaine's
  Valor now have normalized base item/unique definitions and complete their
  authored Halls of the Blind and Valor quests through server-owned pickup
  transitions.

- Add schema 1.340 completion-stage quest markers. Lazarus's authored
  Storyteller interaction now writes native `_qvar1 = 8` and logs the Diablo
  follow-on quest after the native completion gate.

- Add schema 1.339 route activation markers. The base Skeleton King route now
  preserves the native `_qvar1 = 1` write when the authored quest activates.

- Add schema 1.338 base Skeleton King quest content. The route-level quest
  activation and normalized native Skeleton King completion monster now use
  authored server-owned data.

- Add schema 1.337 base Butcher quest content. The Dead Guy activation and
  normalized The Butcher completion monster now use authored server-owned data.

- Add schema 1.328 unique-monster dialogue hand-ins. Hellfire Lachdanan now
  consumes Golden Elixir and spawns the authored Veil of Steel reward through
  the shared `TalkToQuestRequested` boundary; the server owns completion,
  relative reward placement, and completion dialogue.

- Make authored dialogue normal-item rewards use the validated spell-book
  catalog when their item identity is a dynamic book template. The server now
  generates the same deterministic spell identity, name, cost, and read-book
  action as other authoritative book drops.

- Add schema 1.329 one-time dialogue activation rewards. Base Zhar now starts
  its authored quest conversation and spawns the level-14 dynamic spell book
  exactly once through `TalkToQuestRequested`; the server owns the activation
  guard, reward placement, and generated book state.

- Externalize Hellfire Jersey's Jersey quest 3024 through the authored towner
  boundary. Complete Nut now consumes Brown Suit and spawns the validated Bovine
  Plate reward with native conversation gating and completion dialogue.

- Add schema 1.330 activation-only towner interactions. Hellfire Farmer's
  Orchard now activates through the authored Farmer row and creates its initial
  Rune Bomb on the server; the existing Hive object remains the completion path.

- Add schema 1.331 completion-stage towner interactions. Hellfire Farmer's
  post-Hive conversation now advances its authored marker, creates Auric, and
  emits completion dialogue through the server boundary.

- Add schema 1.332 alternate-item towner interactions. Hellfire Jersey's Jersey
  now consumes Grey Suit and emits its native tease dialogue without changing
  quest state before the Brown Suit completion path.

- Add schema 1.333 activation-only item and marker support. Base Cain now
  consumes Staff of Lazarus, writes the native quest marker, and starts the
  authored Lazarus conversation through `TurnInQuestRequested`.

- Add schema 1.334 base Magic Rock quest content. Griswold's staged conversation
  now advances the authored gate before requiring the item, then consumes Magic
  Rock and spawns the normalized level-5 Empyrean Band through the server.

- Add schema 1.335 base Ogden's Sign quest content. The staged Tavern
  conversation now advances its native gate before consuming Tavern Sign and
  spawning the normalized level-4 Harlequin Crest through the server.

- Add schema 1.336 base Anvil of Fury quest content. The staged Blacksmith
  conversation now advances its native gate before consuming Anvil and
  spawning the normalized Griswold's Edge through the server.

- Extend content-owned towner turn-ins with validated normal-item rewards.
  Schema 1.327 adds `turn_in_reward_item_id`/`turn_in_reward_item_symbol`; the
  server generates the authored item at the native reward level and position.
  Hellfire Little Girl now consumes Theodore and creates the level-13 amulet
  reward through `TurnInQuestRequested`.

- Route Hellfire Magic Rock turn-in through the same authored Griswold boundary.
  The server now owns the native first-conversation gate, Magic Rock
  consumption, and Empyrean Band reward.

- Route content-declared quest-object effects through
  `OperateObjectRequested`. The native client now identifies active
  `QuestSequence` and `QuestItemCompletion` objects from authoritative
  snapshots, while the server owns ordering, quest state, item consumption,
  and activation reconciliation. The Hellfire Na-Krul tome sequence is the
  first shipped definition; unmodeled objects retain legacy behavior.

- Route authored unique-monster quest conversations through
  `TalkToQuestRequested`. Native snapshots now carry the optional opaque unique
  identity, the client selects the active matching quest, and the server owns
  target identity, live-state, level, adjacency, activation, and dialogue
  validation. The Hellfire Defiler path is the first shipped definition; legacy
  monster conversations remain compatible outside the authoritative path.

- Add schema 1.326 content-owned towner turn-ins. Quest snapshots now carry
  optional validated towner metadata, so the native authoritative client can
  route any active authored turn-in without a hardcoded quest/towner pairing.
  The first shipped Hellfire Blacksmith Anvil path validates the towner, native
  `_qvar2` conversation gate, required item, and adjacency on the server, then
  consumes the Anvil, completes quest 3011, and spawns Griswold's Edge through
  `TurnInQuestRequested` while projecting the server-authored dialogue.

- Route native healing, mana, rejuvenation, and arena potions through the
  server-owned `UseConsumableRequested` intent. External content now classifies
  their actions; the Diablo module applies native Q6 restoration, class
  multipliers, `NoMana` suppression, deterministic RNG, and item consumption.

- Route native Strength, Magic, Dexterity, and Vitality elixirs through the
  same server-owned intent. External content classifies each stat transition;
  the Diablo module owns native stat ceilings, Q6 resource growth, Hellfire
  full-resource restoration, `NoMana` suppression, persistence, and item
  consumption.

- Route native Blacksmith, Accuracy, Sharpness, and Fortitude oils through the
  server-owned `UseConsumableRequested` intent with an additive target item
  reference. External content classifies each oil; the Diablo module owns the
  native target restrictions, durability/damage/to-hit mutations, deterministic
  random ranges, atomic source consumption, and equipment recalculation.

- Route Hellfire Spectral Elixir through `UseConsumableRequested`. External
  content classifies the +3-all-attributes transition; the Diablo module owns
  native attribute ceilings, life/mana resource growth, persistence, and item
  consumption.

- Externalize item-action magnitudes in the validated item catalog. Spectral
  Elixir now carries its native three-point magnitude in content, while legacy
  states retain the compatible fallback.

- Route connected native Cathedral Map use through the authoritative grave
  object. The server now validates the projected object and consumes the map
  through the existing quest-item completion path instead of local quest state.

- Route connected native Hellfire Rune Bomb use through content-owned Hive
  entrance objects. The server now consumes the bomb, completes Farmer's
  Orchard, and gates the normalized town-to-Nest warp; legacy mode retains the
  native local path.

- Externalize Hellfire Magic Rock pickup as a validated item-to-quest effect.
  Connected authoritative pickup now activates and logs the Magic Rock quest
  through server-owned state instead of relying on native local mutation.

- Externalize Hellfire Anvil of Fury pickup as a validated item-to-quest effect.
  Connected authoritative pickup now activates and logs the Anvil quest through
  server-owned state while legacy pickup remains compatible.

- Externalize Hellfire Optic Amulet and Arkaine's Valor pickup completion as
  validated item-to-quest effects. The server completes only an already-active
  quest, matching the native quest-item boundary.

- Externalize Hellfire Black Mushroom pickup progression as a conditional
  item-to-quest effect. The server advances the quest only from the native
  spawned-mushroom state.

- Preserve the native equipment slot selected by an authoritative inventory
  drag through an optional `AutoEquipItemRequested.equipment_slot`; the server
  now owns explicit hand selection, ring replacement, and atomic two-handed
  displacement while automatic placement keeps its first-fit behavior.

- Enforce content-defined equipment slot topology for authoritative item moves,
  including multi-cell body armor transfers; belt placement retains the native
  one-cell footprint rule while catalog-less compatibility simulations remain
  supported.

- Route native automatic equipment-to-inventory, belt-to-inventory, and
  inventory-to-belt placement through stable-reference authoritative transfers,
  and add `AutoEquipItemRequested` for atomic inventory-to-equipment swaps that
  return displaced equipment through native inventory placement order.

- Route normal native authoritative inventory-cursor placement through the
  existing stable-reference `MoveItemRequested` transfer, keeping cursor
  selection as presentation state and reconciling rejected transfers from the
  server snapshot. Automatic shift-click placement remains a follow-up seam.

- Add a live single-player protocol-client check that relocates an inventory
  item by stable identity and confirms a stale reference is rejected without
  mutating the authoritative grid.

- Carry an additive stable item reference on inventory-move intents. Native and
  Godot clients now send the projected identity, and the C# module rejects
  stale item slots before changing the authoritative inventory grid.

- Resolve native indexed item-service convenience calls through the latest
  authoritative inventory snapshot, carrying stable item IDs and rejecting
  missing slots before submission.

- Harden external TSV validation with one strict Boolean parser across class,
  quest, quest-type, level, procedural-level, object, warp, item, monster,
  monster-missile, affix, store, and combat catalogs; malformed Boolean values
  now reject the complete content candidate.

- Ensure an unstarted authoritative TCP host still disposes its leased active
  adapter, closing the server-owned resource lifecycle on construction failure
  and early shutdown paths.

- Roll back authoritative TCP host startup when port binding fails, allowing a
  caller to release the port and retry without leaving a half-started host.

- Preserve failed authoritative save imports as failures: a reconnect retries
  the same stored state instead of silently creating and saving a fresh session.

- Serialize concurrent first connections for one resume token so they share a
  single authoritative session and participant initialization.

- Make Godot client handshakes transactional: rejected or incomplete attempts
  now close their TCP transport before a reconnect or retry.

- Refresh live Godot overlay fixtures to the active `devilution.diablo@1.325.0`
  schema so the protocol-client integration suite matches shipped content.

- Route native Counselor Flash projectiles through the validated stable `flash`
  spell identity, retaining numeric ID `4` for legacy hand-built simulations.

- Let active item catalogs resolve charged and consumable spell sources by
  content-owned miscellaneous IDs when legacy state lacks a native item index;
  ambiguous or older metadata continues to use the native fallback.

- Apply the same content-owned miscellaneous-ID source resolution to store
  recharge validation, so normalized charged-item metadata is honored even
  when saved state has no native item-index match.

- Let active item catalogs resolve semantic `Staff` tags by unambiguous misc ID
  when native item-index state is unavailable, preserving native type fallback
  for older or ambiguous rows.

- Extend the same unambiguous misc-ID tag resolution to belt eligibility, so
  server inventory placement can recognize content-defined belt items without a
  native item-index match.

- Extend unambiguous misc-ID tag resolution to equipment-slot projection, so
  ring and amulet content remains authoritative without a native item-index
  match while legacy location fallback remains available.

- Extend the semantic misc-ID fallback to weapon and two-handed admission for
  unambiguous staff/weapon rows, retaining native item-class and location rules
  for legacy or ambiguous mappings.

- Carry each generated item's stable content ID through authoritative state,
  version-1 saves, and the shared protocol. State-based catalog resolution now
  prefers that identity before the native item-index compatibility projection;
  zero-valued legacy state and existing replay hashes remain compatible.

- Use stable item identity for authoritative quest effects, item combinations,
  quest-item completion, and PvP-ear deduplication, retaining native-index
  matching for legacy state without a stable ID.

- Validate nonzero stable item IDs against the active item catalog during
  snapshot restoration, while preserving zero-valued legacy saves and
  catalog-less compatibility simulations.

- Carry stable item IDs on protocol item references. Godot now sends them for
  service and cast-source requests, and the server rejects stale slot bindings
  while retaining zero-valued positional compatibility.

- Add executable Godot presentation-boundary checks so the protocol client
  cannot acquire server dependencies and the Godot project remains limited to
  the shared protocol and presentation protocol assemblies.

- Keep the native authoritative client out of the legacy Lua lifecycle: its
  server-backed mode no longer initializes or exposes the Lua REPL, while the
  explicit legacy mode remains unchanged.

- Carry stable item identities through the native authoritative projection and
  use them for native service, store, and cast-source commands, retaining zero
  as the compatibility value for older snapshots.

- Route native authoritative spell-book use through the existing
  `ReadBookRequested` intent so book learning, mana gain, and item removal stay
  server-owned while the legacy runtime retains its local path.

- Move the legacy Rune of Fire miscellaneous-ID translation into spell-catalog
  activation so combat consumes the validated semantic `RuneFire` behavior.

- Derive single-player dynamic-book exclusions from validated `Resurrection` and
  `HealOther` spell behavior, retaining native-ID fallback for older content
  packs whose spell metadata predates semantic behavior fields.

- Persist the server/world RNG seed in the authoritative save root so
  deterministic procedural geometry survives process restarts; an explicit
  `--random-seed` must match an existing persisted seed.

- Add schema 1.324's optional `missile_blocked_percent` procedural-level
  field, preserving separate deterministic missile occlusion while retaining
  `blocked_percent` as the compatibility fallback.

- Add optional schema 1.323 `procedural_levels.tsv` definitions for
  deterministic, connected server-owned level geometry. Generated levels are
  composed additively with static maps using row and world seeds; native map
  synthesis remains a later parity stage, and invalid default-start or
  multiplayer-death-drop flag values are rejected during activation.

- Add a local standalone-server operator console with status, transactional
  ruleset reload, help, and graceful quit commands.

- Add additive server-owned object presentation families for shrines, quests,
  breakables, and revealed traps, with generic compatibility fallback.

- Add protocol presentation value `FLASH` and project native Flash behaviors
  `5`, `6`, and `7` through the server and Godot client as a dedicated family.

- Add protocol presentation value `HIDDEN` so Rage controller behavior `34` is
  suppressed by current Godot clients instead of rendered as a generic sprite.

- Clarify that Teleport and Phasing transition families are presentation-only
  cues while movement destinations, occupancy, and RNG remain server-owned.

- Add additive world-item presentation metadata for server-owned Gold and PvP
  ear rendering, with generic fallback for older or unknown item state.

- Preserve native Fireball collision with missile-blocking tiles and objects,
  including breakable-object destruction before the authoritative splash.

- Preserve native breakable-object destruction for both player and monster
  arrows when an arrow collides with a missile-blocking object.

- Preserve native player-arrow path collision by resolving the first living
  combatant encountered on each traversed tile before the declared endpoint.

- Preserve native player-arrow misses when the declared endpoint target leaves
  the arrow's spatial path before impact.

- Preserve Hellfire `RandomArrowVelocity` timing with server-owned 16–47 pixel
  speeds and resumable fractional travel state.

- Preserve native player-arrow class and Hellfire attack-speed velocity bonuses
  through resumable `Speed`/`PathTicks` state and the shared replay gate.

- Preserve Hellfire blocked player-arrow impact state and native rotation RNG
  consumption through the authoritative projectile lifecycle.

- Add a shared deterministic Firebolt player-versus-player replay covering
  server-owned hit resolution, damage, and projectile checkpoint state.

- Preserve Hellfire Firebolt shield blocks as authoritative impact collisions,
  including the native variant RNG draw and resumable removal timing.

- Add a shared deterministic Fireball player-versus-player replay covering
  direct collision, impact splash, Q6 damage, participant projection, and removal
  timing across the native and C# runners.

- Preserve Hellfire Fireball shield-block retention and variant rotation RNG,
  while allowing the retained impact splash to resolve against the participant.

- Preserve native Bone Spirit player damage by applying the configured
  current-life divisor followed by the non-arrow half-damage step; add a shared
  C++/C# replay covering self-damage, hit RNG, impact lifetime, and removal.

- Preserve native Bone Spirit nearest-monster homing and resistant-monster
  damage, including monster regeneration and the retained impact phase, with a
  shared C++/C# replay fixture.

- Route secondary replay-participant melee attacks through the same
  authoritative combat boundary as the primary participant, with shared C++/C#
  checkpoint coverage for identity routing and unarmed damage.

- Add seeded MonsterArrow replay coverage for native travel, player impact,
  impact-phase lifetime, and canonical C++/C# checkpoint parity.

- Route seeded MonsterArrow impacts to a secondary multiplayer participant in
  the shared replay projection, preserving complete participant-state hashes.

- Add autonomous GoatRanged and SkeletonRanged MonsterArrow replay coverage:
  validated admission, stable target selection, long-range travel, impact, and
  removal now share canonical checkpoints between the C# server and native
  compatibility runner.

- Add autonomous Succubus BloodStar replay coverage for validated admission,
  native velocity/lifetime state, and pre-impact travel parity between the C#
  server and native compatibility runner.
- Add Succubus BloodStar impact replay coverage for deterministic monster-magic
  hit RNG, Q6 player damage, impact-phase retention, and removal-boundary parity.
- Add secondary-player BloodStar block replay coverage for shield admission,
  block-animation timing, and deterministic hit/block RNG parity.
- Add Lich-family MonsterMagic replay coverage for validated admission, native
  travel, deterministic hit RNG, Q6 damage, and impact-phase parity.
- Add a seeded Lich MonsterMagic miss replay covering preserved player state,
  projectile travel, and failed-collision identity parity.
- Add multiplayer Lich MonsterMagic replay coverage for secondary-participant
  target selection, impact damage, and complete participant-state hashing.
- Add an additive `MonsterMagic` projectile presentation family to the shared
  protocol and Godot client, preserving server-owned visual classification.
- Add an additive `HorkSpawn` projectile presentation family to the shared
  protocol and Godot client for server-owned summon-controller visuals.
- Classify Apocalypse behavior `32` as `Fire` and persistent behavior `33` as
  `FireImpact` in the server-owned projectile presentation projection, with
  matching legacy fallback coverage.
- Classify Firebolt behavior `2` as `Fire` during travel and `FireImpact` during
  impact, matching the existing Fireball presentation contract.
- Classify Charged Bolt behavior `1` as the server-owned `Lightning` family,
  including behavior-based fallback for older peers.
- Classify Elemental behavior `35` as the server-owned `Fire` family across
  travel and burst phases, matching its native `firerun` visual.
- Add protocol presentation value `GUARDIAN` and project Guardian behavior `36`
  through the server and Godot client as a dedicated turret family.

- Preserve native breakable-object destruction for persistent Acid puddles
  without prematurely ending their lifetime.

- Preserve native breakable-object destruction for compatibility generic
  projectiles when a missile-blocking tile ends their travel.

- Preserve native breakable-object destruction for persistent Apocalypse booms
  without shortening their remaining lifetime.

- Preserve native breakable-object destruction for regular Nova Ball blockers
  while retaining the validated shrine exception.

- Preserve native Hork Spawn current-tile breakable-object destruction before
  its expiry summon path.

- Preserve native Hork Spawn timing by checking a newly blocked destination on
  the following tick instead of expiring immediately.

- Preserve Nova Ball's native shrine collision exception through validated
  world-object metadata and the authoritative snapshot, protocol, save, and
  hash boundaries.

- Clarify current architecture and content documentation that schema 1.278
  completed dynamic spell-book assignment and book-cost behavior.

- Preserve native adjacent Counselor Flash attacks in the authoritative server,
  including both stationary layers, monster-level magic damage, and the native
  post-attack delay.

- Preserve native `AiRanged` adjacent arrow admission for validated
  `MonsterArrow` mappings, preventing a clear close-range shot from becoming a
  no-op or falling through to melee.

- Preserve Counselor's native three-projectile Charged Bolt attack and fixed
  15-point monster payload, while retaining validated damage overrides.
- Classify server-owned Counselor `MonsterChargedBolt` projectiles as
  Lightning for Godot presentation, including behavior-based fallback.

- Preserve Counselor's initial native admission RNG draw and capture mapped
  Firebolt/MonsterMagic damage from the monster range at missile launch, while
  retaining fixed payload overrides and Fireball's per-impact roll.

- Preserve native monster direction through authoritative snapshots, saves,
  protocol projection, and canonical hashing; Hork Spawn now uses the monster
  direction for its admission cell and summoned-monster facing.

- Roll dynamic MonsterArrow physical damage at impact from the authoritative
  monster range, retaining fixed `projectile_damage` overrides and resumable
  range state through snapshots and saves.

- Preserve Hork Spawn's native current-tile player collision before movement or
  expiry, including non-arrow hit/block RNG, one-point minimum damage, and the
  subsequent summon path.

- Preserve native per-impact monster Fireball damage rolls for mapped direct
  and splash collisions, while retaining fixed `projectile_damage` overrides.

- Preserve the native three-velocity-quanta launch offset for mapped
  `MagmaBall` projectiles.

- Preserve Storm/ThinLightning's native per-tick doubled monster damage roll
  when creating authoritative lightning segments.

- Activate the validated Hellfire Familiar post-melee Lightning mapping in
  `monster_missiles.tsv`, including its native transport, impact lifetime, and
  `GenerateRnd(10)` damage bound.

- Honor existing validated `projectile_damage` metadata for Familiar
  post-melee Lightning mappings, preserving the configured random-damage
  fallback when older mappings omit a fixed value.

- Apply mapped Snake charge `damage_type` through the authoritative player
  resistance path, preserving native Physical damage for older rows.

- Honor existing validated `projectile_damage` metadata for Snake charge
  collisions, preserving the native special-damage roll when older mappings
  omit a fixed value.

- MonsterArrow mappings now retain their configured
  `projectile_impact_lifetime` after a player impact, while omitted values
  retain the native one-tick compatibility phase.

- Server-owned `MonsterMagic` and `BloodStar` mappings now retain their
  configured `projectile_impact_lifetime` after a player impact; player-owned
  spell projectiles and native expiry behavior remain unchanged.

- `SnakeCharge` mappings now retain their configured
  `projectile_impact_lifetime` after a successful player impact; blocked,
  missed, and expired charges retain native removal behavior.

- MonsterArrow mappings now advance up to their validated `projectile_speed`
  tiles per simulation tick, while speed-one rows retain the native fallback.

- Mega `InfernoController` mappings now carry validated `projectile_damage`
  into every emitted segment, while omitted values retain the native random
  monster-damage roll.

- MonsterArrow mappings now honor validated `projectile_damage` after ranged
  hit admission, while omitted values retain native damage derivation.

- Add schema 1.322 mapped-charge spell identities to `monster_missiles.tsv`,
  preserving the native spell identity `1` when older rows omit the optional
  reference.

- Add schema 1.321 validated mapped-charge damage types to
  `monster_missiles.tsv`, preserving the native Physical fallback when older
  rows omit the optional value.

- Add schema 1.320 validated Familiar post-melee missile identity to
  `monster_missiles.tsv`, preserving the legacy Lightning-label fallback for
  older mappings.

- Add schema 1.319 validated AiRangedAvoidance adjacent attack-band divisor to
  `monster_missiles.tsv`, preserving the native Acid half-band for older
  mappings.

- Add schema 1.318 validated AiRangedAvoidance adjacent attack-band base and
  intelligence-scale fields to `monster_missiles.tsv`, preserving the native
  6,000/1,000-per-intelligence fallback for older mappings.

- Add schema 1.317 validated Familiar Lightning random-damage bounds to
  `monster_missiles.tsv`, preserving the native `GenerateRnd(10)` fallback for
  older mappings.

- Apply the validated Rhino far-distance movement-goal gate consistently to
  charge-miss and blocked-lane fallback branches, retaining the native 5/4
  fallback for older mappings.

- Add schema 1.316 validated Rhino ordinary movement bands to
  `monster_types.tsv`, preserving the native 33/2/50 fallback for older
  mappings.

- Add schema 1.315 validated Fat/Overlord fresh-mode movement bonus to
  `monster_types.tsv`, preserving the native 50-point fallback for older
  mappings.

- Add schema 1.314 validated AiAvoidance fresh-mode movement bonus to
  `monster_types.tsv`, preserving the native 50-point fallback for older
  mappings.

- Add schema 1.313 validated Counselor ranged-admission chance fields to
  `monster_types.tsv`, preserving the native capped 50/5 fallback for older
  mappings.

- Add schema 1.312 validated Bat movement-band fields to
  `monster_types.tsv`, preserving the native 13/1/50 fallback for older
  mappings.

- Add schema 1.311 validated Snake adjacent attack-admission chance fields to
  `monster_missiles.tsv`, preserving the native 20-plus-intelligence fallback
  for older mappings.

- Add schema 1.310 validated SkeletonRanged fresh-mode movement bonus to
  `monster_types.tsv`, preserving the native 50-percent fallback for older
  mappings.

- Add schema 1.309 validated AiRangedAvoidance ordinary movement-band fields to
  `monster_missiles.tsv`, preserving the native 50/10/30 fallback for older
  mappings.

- Add schema 1.308 validated AiRangedAvoidance movement-goal modulo to
  `monster_missiles.tsv`, preserving the native four-way/Acid eight-way
  fallback for older mappings.

- Add schema 1.307 validated Rhino movement-goal gate fields to
  `monster_types.tsv`, preserving the native minimum-five/modulo-four fallback
  for older mappings.

- Add schema 1.306 validated shared AiAvoidance Move-goal gate fields to
  `monster_types.tsv`, preserving the native minimum-four/modulo-four fallback
  for older mappings.

- Add schema 1.305 validated Hork Demon RoundWalk admission modulo to
  `monster_types.tsv`, preserving the native modulo-four fallback for older
  mappings.

- Add schema 1.304 validated SkeletonKing RoundWalk admission modulo to
  `monster_types.tsv`, preserving the native modulo-four fallback for older
  mappings.

- Add schema 1.303 validated Mega intermediate movement chance fields to
  `monster_types.tsv`, preserving the native intelligence-scaled band and cap
  for older mappings.

- Add schema 1.302 validated Mega delegated delay-admission chance fields to
  `monster_types.tsv`, preserving the native intelligence-scaled band for older
  mappings.

- Add schema 1.301 validated Counselor failed-ranged-admission movement chance
  metadata to `monster_types.tsv`, preserving the native 30-percent fallback
  for older mappings.

- Add schema 1.300 validated Snake distant-delay admission chance fields to
  `monster_missiles.tsv`, preserving the native intelligence-scaled band for
  older mappings.

- Add an authoritative-path dependency gate that rejects Lua/sol2 references
  from server and Godot C# source/project files while retaining the native
  compatibility layer.

- Add schema 1.299 validated SkeletonKing movement-admission chance tuning to
  `monster_types.tsv`, preserving the native intelligence band for older
  mappings.

- Add schema 1.298 separate Counselor failed-admission and successful-attack
  delay tuning to `monster_types.tsv`, preserving both native formulas for
  older mappings.

- Add schema 1.297 validated AiAvoidance RoundWalk-failure delay tuning to
  `monster_types.tsv`, preserving the native base-plus-exclusive-random delay
  for older mappings.

- Add schema 1.296 validated SkeletonMelee movement and failed-adjacent-
  attack delay bands to `monster_types.tsv`, preserving the native
  intelligence reduction and exclusive random ranges for older mappings.

- Add schema 1.295 validated Rhino recovery-delay tuning to
  `monster_types.tsv`, preserving the native base-plus-exclusive-random delay
  for older mappings.

- Add schema 1.294 validated SkeletonKing movement-delay tuning to
  `monster_types.tsv`, preserving the native base-plus-exclusive-random delay
  for older mappings.

- Add schema 1.293 validated Mega post-action delay tuning to the
  `InfernoController` mapping in `monster_missiles.tsv`, preserving the
  native base-plus-exclusive-random delay for older mappings.

- Add schema 1.292 validated Snake adjacent and distant delay-band tuning to
  `monster_missiles.tsv`, preserving native intelligence reduction and
  exclusive random ranges for older mappings.

- Add schema 1.291 validated HorkDemon recovery-delay tuning to the
  `HorkSpawn` row in `monster_missiles.tsv`, preserving the native
  base-plus-exclusive-random delay for older mappings.

- Add schema 1.290 validated RangedAvoidance recovery-delay tuning to
  `monster_missiles.tsv`, preserving the native base-plus-exclusive-random
  delay for older mappings that omit the optional columns.

- Add schema 1.289 validated post-attack ranged-delay tuning to
  `monster_missiles.tsv`, preserving the native base-plus-exclusive-random
  delay for older mappings that omit the optional columns.

- Refresh all 22 checked-in base and Hellfire replay fixture manifest hashes to
  the active content identities, restoring the full server replay gate.

- Dispose the active authoritative adapter during host shutdown, completing
  safe lifecycle ownership alongside retired-generation disposal.

- Reject impossible authoritative player-resource and monster-vitality bounds
  at the Godot protocol boundary before presentation consumes snapshots.

- Enforce one nonzero entity-ID namespace across authoritative players,
  monsters, items, objects, projectiles, and corpses, while preserving the
  zero-player identity compatibility needed by version-1 saves.

- Add typed Godot combat feedback for authoritative damage, healing, and
  experience events, replacing Lua-owned floating-number presentation for the
  server-backed client.

- Add additive server-owned projectile presentation metadata so Godot no longer
  needs the canonical behavior-to-visual mapping for current peers.

- Add schema 1.279 validated display names to every directly castable spell row,
  projecting them through memorized, charged-item, and consumable-item sources.

- Add schema 1.280 Gargoyle adjacent-attack tuning to validated monster-type
  content, preserving the native fallback for older packs.

- Add schema 1.281 Gargoyle avoidance-movement tuning to validated monster-type
  content and keep the content-defined Gargoyle identity active when healing
  metadata is also present.

- Add schema 1.282 Snake drift-pattern metadata to validated monster-missile
  content, preserving the native fallback for older mappings.

- Add schema 1.283 Resurrection initial-placement metadata to validated spell
  content, preserving the native probe order for older mappings.

- Add schema 1.284 Charged Bolt steering-pattern metadata to validated spell
  content, preserving the native fallback for older mappings.

- Add schema 1.285 Fireball explosion-offset metadata to validated spell
  content, preserving the native impact order for older mappings.

- Add schema 1.286 Elemental burst-offset metadata to validated spell content,
  preserving the native burst order for older mappings.

- Add schema 1.287 Flash bottom/top layer offset metadata to validated spell
  content, preserving the native layer orders for older mappings.

- Add schema 1.288 Guardian arc-offset metadata to validated spell content,
  preserving the native firing order for older mappings.

- Add additive server-owned spell display names to memorized and item-backed
  spell snapshots, sourced from validated spell-book content so Godot can
  present cast sources without maintaining a client-side spell-ID table.

- Add schema 1.267 Identify item-action metadata and server-authoritative
  consumable scroll handling, while keeping the Bard class ability distinct.

- Add schema 1.268 stable Hellfire quest and miscellaneous item identities for
  Magic Rock, Tavern Sign, Golden Elixir, Anvil of Fury, Black Mushroom, Brain,
  Fungal Tome, Spectral Elixir, and Blood Stone.

- Add schema 1.269 stable Hellfire fixed/random Full Healing, Full Mana, Mana,
  Healing, Rejuvenation, and Full Rejuvenation potion identities.

- Add schema 1.270 stable Hellfire Oil of Blacksmith, Oil of Accuracy, Oil of
  Sharpness, Oil of Fortitude, and Strength, Magic, Dexterity, and Vitality
  Elixir identities.

- Add schema 1.271 stable base helmet, body-armor, and shield identities with
  native armor ranges, durability, requirements, cursor projections, and
  Hellfire-compatible overlays.

- Add schema 1.272 stable base standard weapon identities for swords, axes,
  maces, bows, and staves, preserving native damage ranges, durability,
  requirements, cursor projections, and Hellfire-compatible overlays.

- Add schema 1.273 stable identities for the remaining base armor variants:
  Cap, Full Helm, Quilted Armor, Hard Leather Armor, Ring Mail, Scale Mail,
  Splint Mail, Field Plate, Gothic Plate, Buckler, and Tower Shield.

- Add schema 1.274 stable identities for the remaining base potion, oil, and
  elixir rows (native indices 77–90) and the native healing scroll at index 91,
  preserving miscellaneous IDs, values, level gates, drop rates, cursor
  projections, usability, and the scroll's consumable spell source.

- Add schema 1.275 stable identities for the generic base ring and amulet rows
  (native indices 156–160) and the native Arena Potion at index 167, preserving
  item types, miscellaneous IDs, level gates, values, cursor projections, drop
  rates, and usability.

- Add schema 1.276 native-compatibility corrections for all normalized fixed
  and spell-backed scroll rows, retaining their actual native enum indices and
  removing the duplicate healing-scroll alias.

- Add schema 1.277 stable identities for the four native dynamic spell-book
  templates at indices 114–117, preserving level gates, drop weights, misc
  identity, and the blue-book cursor projection while leaving dynamic spell
  assignment for a follow-up behavior migration.

- Add schema 1.278 validated metadata for all 23 native bookable spells and a
  deterministic server-owned generator that applies spell names, book costs,
  intelligence requirements, and magic-school cursor projections.

- Add the shared `ReadBookRequested` command with server-owned native
  intelligence gating, learned-spell level mutation, mana refund, and book
  consumption. Store rows and static/procedural monster drops now invoke the
  same deterministic dynamic-book generator before emitting authoritative item
  state, including quest-completion rewards.

- Refine schema 1.278 dynamic-book selection to roll over the validated active
  native spell-table span and walk native spell IDs cyclically before mapping to
  stable content IDs; explicit spell references remain direct and deterministic.
  Thread the active game mode through stores, static/procedural drops, and quest
  rewards so single-player excludes native Resurrect and Heal Other while
  multiplayer retains both. Store, drop, and quest-reward paths now preserve
  the supplied native book-template identity instead of substituting another
  eligible template.

- Align the base-pack quest-state and warp overlays with the current stable
  identity schemas, and keep base-only monster missile rows numeric-only when
  their symbols belong to the Hellfire catalog.

- Normalize the first five Hellfire unique base-item projections with stable
  symbols, native display names, and cursor identities, including the shared
  Hellfire ring base.

- Normalize the next five Hellfire unique base-item projections: Veil of Steel,
  Arkaine's Valor, Griswold's Edge, Bovine Plate, and The Butcher's Cleaver.

- Normalize the next five Hellfire bow base-item projections: Hunter's Bow,
  Long Bow, Short Bow, Long War Bow, and Short Battle Bow.

- Normalize the next five Hellfire sword base-item projections: Dagger, Sabre,
  Falchion, Broad Sword, and Long Sword.

- Normalize the next five Hellfire sword base-item projections: Claymore, Broad
  Sword, Scimitar, Bastard Sword, and Two-Handed Sword, including a distinct
  symbol for the duplicate Hellfire Broad Sword projection.

- Normalize the next five Hellfire axe base-item projections: Large Axe, Broad
  Axe, Battle Axe, Small Axe, and Great Axe, including their native `Axe`
  generation tags.

- Normalize the next five Hellfire mace-family projections: Mace, Maul, Flail,
  Morning Star, and War Hammer, including corrected native indices and `Mace`
  generation tags.

- Normalize the next five Hellfire staff projections: Long Staff, War Staff,
  Short Staff, Composite Staff, and Quarter Staff, retaining Quarter Staff's
  native `SHORT_STAFF` cursor projection.

- Normalize the next five Hellfire armor projections: Helm, Skull Cap, Great
  Helm, Crown, and Rags, correcting Rags to native LightArmor/BodyArmor
  placement.

- Normalize the next five Hellfire armor projections: Studded Leather Armor,
  Cloak, Leather Armor, Robe, and Chain Mail, including native body-armor
  placement and the corrected Chain Mail native index and cursor identity.

- Normalize the next five Hellfire armor/shield projections: Breast Plate,
  Cape, Plate Mail, Full Plate Mail, and the duplicate Hellfire Buckler,
  including native body-armor/one-handed placement and stable unique-base
  references for unique rows 1077–1082.

- Normalize the next five Hellfire shield/amulet projections: Kite Shield,
  Small Shield, Large Shield, Gothic Shield, and Amulet, including native
  one-handed shield placement and the corrected level-8 Amulet base row.

- Normalize the Hellfire Sorcerer starting staff overlay with native short-staff
  presentation and a stable `firebolt` spell reference while preserving its
  Hellfire charged-item behavior.

- Externalize the five native rune consumables with stable item identities,
  native cursors, drop metadata, and validated consumable spell mappings.

- Externalize 22 native spell-backed scroll rows with stable item identities,
  native indices, cursor projections, level/value/drop metadata, and validated
  spell mappings; correct legacy `SCROLL`/`SCROLLT` parsing to native IDs 21/22
  while retaining Identify scrolls for their dedicated item-action migration.

- Refresh migration status to record all six cursor-driven class capabilities as
  server-owned and covered, leaving native spell, quest, map, and client work as
  the remaining external-content priorities.

- Normalize the Hellfire Rune Bomb, Theodore, Auric Amulet, Brown Suit, and
  Grey Suit quest items with stable symbols, native item indices, and cursor
  projections while retaining the legacy item-table fallback.

- Add the stable `warp` namespace and optional `warp_symbol` links to static
  level-trigger routes, with activation-time checks and numeric fallback.

- Complete the stable `portal` namespace with optional `portal_symbol` links on
  static portal rows, activation-time mismatch checks, and numeric fallback.

- Add the stable `store` namespace and optional store symbols for vendor stock
  and Adria's service identity, with activation-time validation and numeric
  compatibility for older packs.

- Add the stable `object` namespace and optional `object_symbol` links to
  world-object content, with activation-time numeric/symbol validation and
  numeric-only compatibility for older packs.

- Extend stable quest symbols to warp gates, linked objects, item quest effects,
  and follow-on quest activation while retaining numeric compatibility.

- Add optional stable `quest_symbol` identities to quest types, quest state
  rows, and single-player quest pools, with activation-time mismatch checks and
  numeric-only compatibility for older content packs.

- Separate class capability IDs into the stable `ability` namespace and assign
  Holy Bolt its explicit `spell:holy_bolt` identity, while retaining legacy
  spell-namespace fallback for older loadouts.

- Externalize direct store spell references through optional `spell_symbol`
  values in `stores.tsv`; activated rows reject numeric/symbol mismatches while
  numeric fallback remains available.

- Externalize the Jester ten-way dispatch list through optional
  `jester_spell_symbols` values in `spells.tsv`; activated rows reject
  numeric/symbol mismatches while numeric fallback remains available.

- Externalize spell-staff pricing references through optional `spell_symbol`
  values in `store_services.tsv`; activated rows reject numeric/symbol
  mismatches while numeric fallback remains available.

- Externalize every shipped spell-row identity through optional
  `spell_symbol`; active rows resolve stable spell IDs and reject mismatches
  while numeric fallback remains available.

- Externalize normalized item spell-source references through optional
  `spell_symbol`; active rows resolve stable spell IDs and reject mismatches
  while numeric fallback remains available.

- Externalize mapped monster-missile spell references through optional
  `spell_symbol`; active rows resolve stable spell IDs and reject mismatches
  while numeric fallback remains available.

- Externalize class-skill and starting-spell references through optional stable
  spell symbols in `class_loadouts.tsv`; activated packs cross-check symbols
  against numeric compatibility IDs while older loadout schemas remain valid.

- Externalize Town Portal destination slot geometry through validated
  `world_rules.tsv`; active packs own placement while packs without the new
  columns retain legacy spell-row tuning. Activated geometry is also checked
  against town-level bounds and walkability for every destination slot.

- Externalize Adria's service-only store identity through validated
  `world_rules.tsv`. The authoritative store processor now uses the active
  content value, rejects collisions with stock vendor IDs, and retains native
  store `10` when no world-rules table is supplied.

- Externalize the authoritative town level identity through validated
  `world_rules.tsv`. Town Portal and Warp now use the active content value,
  activation rejects identities absent from the externalized world, and packs
  without the table retain the native level-0 compatibility default.

- Validate optional `monster_missiles.tsv` `monster_id` selectors against the
  active monster-type catalog during content activation, preventing
  monster-specific projectile rows from becoming silently unreachable.

- Invoke combat-content validation during activation and reject unknown
  `combat.tsv` `pvp_ear_item_id` references before a session can construct a
  player-kill ear.

- Validate `content_ids.tsv` spell-kind entries against the active spell
  catalog during activation, preventing stable ability symbols from resolving
  to unreachable spell definitions.

- Validate stable `level` and `portal` identities from `content_ids.tsv` against
  their active world and portal catalogs while retaining legacy behavior when
  those catalogs are absent.

- Externalize native Nightmare/Hell level, accuracy, armor, damage, experience,
  and initial-health modifiers through validated `combat.tsv` difficulty
  columns, preserving the native distinct Nightmare and Hell damage formulas
  while retaining legacy defaults for older packs.

- Externalize native ranged-attack frame timing through validated `combat.tsv`
  columns, including Hellfire's zero-skip timing while retaining legacy defaults.

- Externalize Hellfire quest-completion reward metadata. The Defiler's Cathedral
  Map is now generated by the authoritative server at the native quest death
  boundary instead of being encoded as a static monster drop.

- Externalize the Hellfire Na-Krul tome sequence through validated world-object
  rows and persistent authoritative quest variables, including native wrong-order
  reset behavior.

- Externalize the Hellfire Na-Krul note combination through a validated item
  combination table. The server now consumes the three note fragments and
  produces the reconstructed note on pickup.

- Externalize the Hellfire Cathedral Map pickup transition through a validated
  item quest-effect table. Picking up the map now activates Grave Matters and
  preserves the native first quest variable on the authoritative server.

- Externalize Cathedral Map use at the Hellfire grave object. The existing
  authoritative object command now consumes the required map and completes
  Grave Matters server-side, including content-defined completion variables and
  follow-on quest activation.

- Externalize the Hellfire crypt entrance as a quest-gated server warp. The
  native town trigger remains closed until Grave Matters is completed, then
  uses the authored crypt destination and validated destination crawl.

- Validate quest catalogs and identities referenced by level-trigger gates
  during content activation, so malformed external packs fail before
  publication.

- Validate quest identities referenced by authored world objects during content
  activation, including rejection when a quest catalog is missing.

- Validate follow-on quest identities from primary `quests.tsv` rows during
  activation, closing the path where malformed non-overlay quest data could
  activate an unknown quest at runtime.

- Validate static portal levels and endpoint coordinates against the active
  world during content activation, so malformed routes fail before publication.

- Reject blocked portal and level-trigger endpoints during world activation, so
  authored transitions always land on usable movement cells.

- Validate authored object levels and positions against active world bounds,
  preventing malformed object placements from surviving content activation.

- Validate explicit quest monster completion IDs against active monster types,
  while preserving the base pack's native implicit Diablo-boss completion path.

- Validate authored monster levels and positions against active world bounds,
  preventing malformed static encounters from entering the authoritative world.

- Validate normalized item spell identities against the active spell catalog,
  preventing charged or consumable items from referencing inert spells.

- Validate normalized unique-item base references against the active item
  catalog, preventing late generation failures from malformed content.

- Validate non-zero store spell-staff pricing identities against the active
  spell catalog while preserving the native zero-cost fallback row.

- Validate direct store-stock spell identities against the active spell catalog,
  preventing malformed stock metadata from silently using a zero-spell fallback.

- Validate monster-missile summon identities against active monster types,
  preventing server-owned summon projectiles from creating unknown monsters.
- Preserve native Familiar Bat follow-up lightning: validated monster-specific
  content now emits a server-owned `MonsterMagic` projectile with captured
  `GenerateRnd(10) + 1` damage after an adjacent attack, while ranged ticks
  retain Bat movement and all travel, resistance, and persistence remain
  authoritative.

- Restore Gharbad's native post-dialogue combat boundary: the authoritative
  server now accepts the `Gharbad` AI identity and preserves its AiAvoidance
  attack admission and wounded special branch; dialogue and unique encounter
  activation remain incremental.

- Restore Warlord's native post-dialogue combat boundary: the authoritative
  server now accepts the `Warlord` AI identity and delegates normal combat to
  the server-owned SkeletonAi timing and movement boundary; quest activation
  and unique encounter state remain incremental.

- Restore SnotSpil's native post-banner combat boundary: the authoritative
  server now accepts the `Snotspill` AI identity and delegates combat and Fallen
  fear state through the server-owned FallenAi/SkeletonAi boundary; banner
  activation and map transition behavior remain incremental.

- Restore Zhar and Archbishop Lazarus's native combat boundary: validated
  `Zhar` and `Lazarus` Fireball mappings now delegate normal combat through the
  server-owned CounselorAi controller; dialogue-driven quest and map
  transitions remain incremental.

- Externalize four native unique defeat quest states through the base
  `quest_states.tsv` catalog: Gharbad, Zhar, Warlord, and Lazarus now carry
  exact unique-identity completion triggers and terminal native quest-variable
  transitions; encounter placement, dialogue, and rewards remain incremental.

- Add the validated Lazarus-to-Diablo quest handoff: completing the Lazarus
  quest activates the server-owned Diablo quest, while route activation remains
  suppressed until that handoff occurs.

- Externalize the Diablo boss completion boundary: validated monster types can
  declare the native Diablo identity, and quest rows can complete against that
  identity without relying on an AI label or static encounter placement.

- Persist the native single-player Diablo defeat rank through authoritative
  player snapshots, protocol, version-1 saves, and canonical state hashes;
  multiplayer quest completion leaves this single-player hero state unchanged.

- Add the server-owned Diablo victory boundary: a defeated Diablo now sets
  terminal simulation state and emits a typed `VictoryEvent` with the boss
  identity and single-player hero rank for protocol clients.

- Enforce the native post-victory terminal boundary: once Diablo is defeated,
  later player intents are rejected server-side and autonomous simulation ticks
  no longer advance gameplay state.

- Externalize native Normal/Nightmare/Hell difficulty through authoritative
  start configuration, protocol snapshots, version-1 saves, and canonical
  hashes; the Diablo defeat rank now records difficulty + 1 for single-player.

- Apply native Nightmare/Hell monster level, armor, accuracy, physical damage,
  regeneration, experience, and initial health modifiers in the authoritative
  combat module. Nightmare/Hell health uses the native 3x/4x formulas and
  Diablo/Hellfire single-player/multiplayer bonuses; effective Q6 health is
  persisted in snapshots while base content definitions remain unchanged.
  Normal behavior remains unchanged and unique rewards retain the native
  double-experience rule; Hell-specific resistance data is covered by the
  validated monster-type projection below.

- Externalize native Hell-specific monster resistances through validated
  `monster_types.tsv` columns. Hell materialization now selects the native
  `resistanceHell` fire, lightning, magic, and acid channels while
  Normal/Nightmare retain the ordinary resistance projection; omitted columns
  preserve compatibility behavior.

- Add the first server-authoritative quest dialogue boundary: `TalkToQuest`
  validates an adjacent living unique encounter, activates and logs the quest,
  advances content-owned native dialogue variables monotonically, and emits a
  typed protocol dialogue event carrying only stable identities and the
  content-owned message ID.

- Refresh the 22 tracked replay fixtures to the current base and Hellfire
  content-manifest identities, restoring the complete server replay gate.

- Restore the native `AiRangedAvoidance` round-walk fallback for failed or
  blocked ranged rolls, preventing generic pursuit from replacing native
  lateral movement.

- Preserve the native `AiRangedAvoidance` `RoundWalk` candidate order:
  deterministic fallbacks now test the 90-degree turn, 45-degree turn, and
  straight direction before the opposite-side `RandomWalk` candidates.

- Preserve the native `AiRangedAvoidance` movement branch split: far-distance
  Move admissions use `RoundWalk`, while ordinary failed ranged rolls use
  `RandomWalk` before any generic navigation fallback.

- Persist native `AiRangedAvoidance` `goalVar1`/`goalVar2` movement state through
  authoritative snapshots, optional protocol fields 58-59, saves, and the
  canonical state hash; resumed Move goals continue server-side.

- Preserve native `AiRangedAvoidance` ordinary movement admissions: the server
  now carries its `var1`/`var2`-compatible mode counter, applies the long-idle,
  fresh-mode, and middle-band `RandomWalk` probabilities, and persists that
  state through optional protocol fields 66-67, snapshots, saves, and the
  canonical state hash.

- Preserve the native `AiRangedAvoidance` `goalVar2` mutation when all primary
  `RoundWalk` candidates are blocked; the server flips the turn state before
  its opposite-side `RandomWalk` fallback.

- Persist native Rhino `goalVar1`/`goalVar2` movement state through
  authoritative snapshots, optional protocol fields 60-61, saves, and the
  canonical state hash; resumed Rhino Move goals remain server-owned.

- Persist shared native `AiAvoidance` `goalVar1`/`goalVar2` movement state for
  GoatMelee and Gargoyle through authoritative snapshots, optional protocol
  fields 62-63, saves, and the canonical state hash.

- Preserve shared native `AiAvoidance` ordinary movement admissions: the
  server now carries the native `var1`/`var2`-compatible mode counter, applies
  the long-idle, fresh-mode, and middle-band movement probabilities, and
  persists that state through optional protocol fields 64-65, snapshots,
  saves, and the canonical state hash.

- Preserve native Skeleton/Fallen `AiDelay` state: failed movement and adjacent
  attack admissions now start a server-owned delay countdown, which persists
  through optional protocol field 68, snapshots, saves, and the canonical
  state hash instead of rerolling on the next tick.

- Preserve native `SkeletonBowAi` movement admissions: the server now carries
  its `var1`/`var2`-compatible counter, applies the long-idle and fresh-mode
  close-range retreat bands, and persists that state through optional protocol
  fields 69-70, snapshots, saves, and the canonical state hash.

- Preserve native Fat/Overlord movement admissions: the server now carries the
  `var1`/`var2`-compatible counter, applies the long-idle `20 + 4*intelligence`
  and fresh-mode `70 + 4*intelligence` bands, and persists that state through
  optional protocol fields 71-72, snapshots, saves, and the canonical hash.

- Preserve native Rhino ordinary movement state: the server now carries its
  `var1`/`var2`-compatible counter, applies validated 33/2/50 long-idle and
  fresh-mode bands with the native fallback, starts the native delay countdown
  on failed admissions, and persists movement and delay state through optional
  protocol fields 73-75.

- Preserve native Leoric/Skeleton King `AiDelay` state: failed movement
  admissions now start a server-owned countdown, which persists through
  optional protocol field 76, snapshots, saves, and the canonical state hash
  instead of rerolling on the next tick.

- Preserve native HorkDemonAi `AiDelay` state: failed ordinary movement
  admissions now start a server-owned countdown, which persists through
  optional protocol field 77, snapshots, saves, and the canonical state hash
  instead of rerolling on the next tick.

- Preserve native `AiRangedAvoidance` no-action delay state: failed attack or
  movement decisions now start the native `GenerateRnd(10) + 5` countdown,
  persisted through optional protocol field 78, snapshots, saves, and the
  canonical state hash.

- Preserve native Snake `AiDelay` state: failed non-charge movement and
  adjacent attack admissions now start server-owned countdowns, persisted
  through optional protocol field 79, snapshots, saves, and the canonical
  state hash.

- Preserve native Counselor `AiDelay` state: failed non-adjacent ranged and
  movement admissions and successful mapped ranged actions now start the
  server-owned countdown, persisted through optional protocol field 80,
  snapshots, saves, and the canonical state hash.

- Preserve shared native `AiAvoidance` `AiDelay` state: failed GoatMelee and
  Gargoyle `RoundWalk` admissions now start the native
  `GenerateRnd(10) + 10` countdown, persisted through optional protocol field
  81, snapshots, saves, and the canonical state hash.

- Preserve native `AiRanged` post-attack delay state: generic ranged mappings
  now start the native `GenerateRnd(20)` countdown after firing, persisted
  through optional protocol field 82, snapshots, saves, and the canonical
  state hash.

- Preserve native intermediate-distance `MegaAi` delay state: Inferno and
  movement decisions now start the native `GenerateRnd(10) + 5` countdown,
  persisted through optional protocol field 83, snapshots, saves, and the
  canonical state hash.

- Preserve the native `AiRangedAvoidance` adjacent melee band, including Acid's
  half-rate branch, instead of allowing every adjacent tick through shared
  melee resolution.

- Restore the native `AiAvoidance` far-distance GoatMelee RoundWalk branch:
  validated movement admissions now use a deterministic lateral turn order for
  one-in-four rolls at distance four or greater before native RandomWalk.

- Restore GoatMelee's ordinary native `AiAvoidance` RandomWalk branch: admitted
  non-round movement now uses deterministic immediate candidates instead of
  bounded pursuit.

- Restore Gargoyle's healthy non-adjacent native `AiAvoidance` movement:
  deterministic `RoundWalk`/`RandomWalk` candidates now run after its healing
  branch instead of falling through to bounded pursuit.

- Preserve Gargoyle's native adjacent `AiAvoidance` admission: the server now
  applies the `23 + 2 * intelligence` attack band before shared melee damage.

- Preserve Scavenger's native corpse-seeking `RandomWalk`: deterministic
  five-candidate movement now defers when those candidates are blocked instead
  of scanning the full neighboring ring.

- Preserve Scavenger's native coin-selected corpse scan: the server searches
  the configured square in native traversal order and keeps the first visible
  corpse instead of choosing the nearest tile.

- Preserve Bat's native movement admissions: server-owned `var1`/`var2`
  movement state now distinguishes the long-idle `intelligence + 13` band from
  the fresh-mode `intelligence + 63` band and persists through snapshots;
  ordinary Bat movement no longer requires a charge-missile mapping.

- Preserve Snake's native blocked-drift fallback: after the lateral candidate
  is unavailable, deterministic `RandomWalk2` now checks only straight, left,
  and right from the original target direction instead of adding ±90° moves.

- Restore the native `OverlordAi` Fat movement walk: validated `20 + 4 *
  intelligence` admissions now use the shared deterministic `RandomWalk`
  candidate order before bounded navigation fallback.

- Restore the native `SkeletonAi` movement walk: validated delay-band exits
  now use the shared deterministic `RandomWalk` candidate order before bounded
  navigation fallback.

- Map native `FallenAi` normal-state delegation to the server-owned SkeletonAi
  movement and adjacent attack bands while preserving fear-state precedence and
  specialized validated controller identities.

- Restore the native `ButcherAi` non-adjacent `RandomWalk` branch before shared
  melee resolution, with bounded navigation retained only for blocked cells.

- Restore native `LeoricAi` non-adjacent movement after summon admission:
  Skeleton King now uses deterministic `RoundWalk`/`RandomWalk` candidates before
  bounded navigation, while validated summons retain precedence.

- Restore native `HorkDemonAi` far-distance movement after summon admission:
  Hork Demons now use deterministic `RoundWalk`/`RandomWalk` candidates before
  bounded navigation, while validated Hork Spawn projectiles retain precedence.

- Restore native `MegaAi` far-distance fallback: Mega now delegates to the
  server-owned Skeleton movement boundary when Inferno is outside its validated
  maximum range.

- Preserve Mega's delegated SkeletonAi delay: far-distance delay-band misses
  now retain the native `15 - 2 * intelligence + GenerateRnd(10)` countdown
  through the existing Skeleton delay state, snapshots, saves, and canonical
  hashes.

- Preserve the native Mega intermediate-distance fallback: a missed or
  blocked mapped Inferno admission now consumes the native
  `2 * (5 * intelligence + 25)` RandomWalk band before generic pursuit.

- Restore Rhino’s native movement fallback after charge misses or blocked
  lanes: far targets now use deterministic `RoundWalk` admissions, while the
  ordinary `33 + 2 * intelligence` band uses `RandomWalk` before generic
  navigation.

- Restore native `BatAi` movement after a mapped Gloom charge misses or is
  blocked: the server now applies the deterministic move-mode
  `63 + intelligence` admission and native `RandomWalk` candidate order,
  suppressing generic pursuit on deferred ticks.

- Preserve the native `SkeletonBowAi` blocked-lane defer: a long-range arrow
  with no clear missile lane now leaves the SkeletonRanged monster stationary
  instead of falling through to generic pursuit.

- Preserve the native `AiRanged` blocked-lane defer for GoatRanged: after its
  close-range avoidance branch, a long-range arrow with no clear missile lane
  now leaves the monster stationary instead of entering generic pursuit.

- Preserve the native Snake `AiDelay` branch: failed non-charge movement and
  adjacent attack admissions now consume the authoritative tick without
  falling through to generic pursuit, while admitted lateral drift and attacks
  remain server-owned.

- Preserve the native Zombie blocked-walk result: a successful action whose
  one-step destination is unavailable now leaves the monster stationary rather
  than invoking generic navigation.

- Preserve the native HorkDemon blocked-movement result: admitted
  `RoundWalk`/`RandomWalk` branches now consume the tick when every native
  candidate is blocked instead of invoking generic navigation.

- Preserve Mega's native Skeleton fallback when its far-distance `RandomWalk`
  candidates are blocked: the admitted tick now defers instead of invoking
  bounded generic navigation.

- Preserve Skeleton King's native `LeoricAi` movement when its admitted
  `RandomWalk` candidates are blocked: the tick now defers instead of invoking
  bounded generic navigation.

- Preserve Fat/Overlord's native blocked `RandomWalk` result: an admitted
  movement tick now defers instead of invoking bounded generic navigation.

- Preserve SkeletonMelee's native blocked `RandomWalk` result: an admitted
  movement tick now defers instead of invoking bounded generic navigation.

- Preserve Butcher's native blocked `RandomWalk` result: an admitted movement
  tick now defers instead of invoking bounded generic navigation.

- Preserve GoatMelee's native blocked `RoundWalk` result: an admitted
  far-distance movement tick now defers instead of invoking bounded generic
  navigation.

- Restore native `CounselorAi` wounded-adjacent retreat: Counselors now enter a
  server-owned four-attempt `RandomWalk` state before mapped ranged dispatch,
  with retreat state preserved through snapshots, protocol saves, and state
  hashing.

- Restore Counselor’s native ranged admission: mapped Firebolt, Charged Bolt,
  Fireball, Lightning, and magic rows now use validated base and intelligence-
  scale terms, preserving the native 50/5 fallback and 30-percent `RoundWalk`
  branch before generic pursuit.

- Honor standalone validated Gargoyle healing tuning by composing omitted
  safety-boundary and healing-formula terms from the native 2/1 and 16/5/4
  defaults.

- Honor standalone validated Scavenger wounded-health tuning by composing
  omitted healing terms from the native 10/4/8/1/75 defaults.

- Honor standalone validated Bat retreat-duration tuning by composing omitted
  attack-band terms from the native 8/4 defaults.

- Honor standalone validated GoatMelee wounded-special tuning by composing
  omitted attack-band terms from the native 23/2 defaults.

- Restore native Snake non-charge movement: mapped Snake controllers now use a
  deterministic drift branch outside the charge boundary while preserving the
  existing generic navigation fallback for native delay ticks.

- Restore native Zombie intermediate-range movement: successful action rolls
  at distances outside melee but below the long-range pursuit boundary now use
  a deterministic server-owned random walk instead of idling.

- Fix partial external Gargoyle healing tuning to retain the native random
  divisor offset of 4 when `gargoyle_heal_divisor_random_offset` is omitted,
  avoiding a zero-divisor path.

- Add a shared client-side status presentation classifier; Godot now renders
  server-owned Infravision, Search, and Etherealize state as typed player
  overlays while leaving status timing and admission authoritative.

- Render authoritative Teleport and Phasing movement controllers in Godot with
  typed presentation families instead of filtering those server-owned
  transitions from the world view.

- Add a shared client-side projectile presentation classifier for authoritative
  behavior IDs; Godot now renders distinct Fire, Lightning, Holy, Blood Star,
  Bone Spirit, Acid, arrow, beam, and impact families with a generic fallback.

- Use validated 
ova_projectile_count` and `immolation_projectile_count`
  metadata for the deterministic radial fan-out prefix; checked-in content
  retains the native 36-projectile Nova and Immolation behavior.

- Use existing validated `projectile_speed`, `projectile_lifetime`, and
  optional `damage_type` and `projectile_damage` metadata for mapped BloodStar
  projectiles before falling back to spell transport values and native monster
  damage, preserving older rows.

- Use existing validated `projectile_speed` metadata for MonsterArrow travel;
  existing `projectile_lifetime` metadata now overrides arrow lifetime when
  present; legacy rows retain the native speed-one and distance-based lifetime
  fallbacks. Configured `damage_type` now selects the matching resistance
  channel, while omitted values retain physical arrows.

- Add schema 1.208 optional mapped monster projectile impact lifetime to
  `monster_missiles.tsv`; monster arrows, Snake charges, and server-owned magic
  missiles now use validated impact timing, retaining the native one-tick
  default, while Hork Spawn keeps its controller-specific expiry lifecycle.

- Add schema 1.207 optional Acid puddle impact lifetime to
  `monster_missiles.tsv`; validated Acid mappings now own the persistent
  puddle impact phase, retaining the native two-tick lifetime when omitted.

- Add schema 1.206 optional monster-player block level scale to `combat.tsv`;
  blockable monster attacks now use validated level-difference tuning, retaining
  the native scale of two when omitted.

- Add schema 1.205 optional monster regeneration level divisor to `combat.tsv`;
  higher-level per-tick recovery now uses validated content, retaining the
  native `level / 2` rule when omitted.

- Add schema 1.204 optional Bone Spirit player damage divisor to `spells.tsv`;
  direct player damage now uses validated content, retaining the native
  one-third current-life rule when omitted.

- Add schema 1.203 optional direct player-magic hit tuning to `combat.tsv`;
  Charged Bolt, Firebolt, and Bone Spirit player-hit rolls now use validated
  character-level penalty and chance bounds, retaining native 2/5/95 defaults
  when omitted.

- Add schema 1.202 optional persistent-projectile player damage divisor to
  `spells.tsv`; shared player damage now uses validated content, retaining the
  native divisor of two when omitted.

- Add schema 1.201 optional persistent-projectile player magic-hit tuning to
  `spells.tsv`; shared player-hit rolls now use validated character-level
  penalty and chance bounds, retaining native 2/5/95 defaults when omitted.

- Add schema 1.200 optional persistent-projectile magic-hit tuning to
  `spells.tsv`. Shared Fire Wall, Fireball, Elemental, Nova, Apocalypse, and
  Lightning-segment monster hits now use validated terms; omitted values retain
  the native 2/5/95 compatibility bounds.

- Add schema 1.199 optional Bone Spirit magic-hit and current-life damage
  tuning to `spells.tsv`; omitted values retain the native 2/5/95 hit terms
  and one-third current-life damage rule.

- Add schema 1.198 optional Firebolt magic-hit tuning to `spells.tsv`.
  Firebolt now uses validated monster-level penalty and hit-chance bounds;
  omitted values retain the native 2/5/95 compatibility terms.

- Add schema 1.197 optional Holy Bolt magic-hit tuning to `spells.tsv`.
  Holy Bolt now uses validated monster-level penalty and hit-chance bounds;
  omitted values retain the native 2/5/95 compatibility terms.

- Add schema 1.196 optional Charged Bolt magic-hit tuning to `spells.tsv`.
  The server now uses validated monster-level penalty and hit-chance bounds;
  omitted values retain the native 2/5/95 compatibility terms.

- Add schema 1.195 optional Town Portal destination slot metadata to
  `spells.tsv`. Server-owned portals now use validated destination origin,
  slot count, and spacing; omitted values retain the native 57/40, four-slot,
  two-tile layout.

- Add schema 1.194 optional Reflect damage-percentage bounds to `spells.tsv`.
  Monster retaliation now uses validated Reflect-row bounds; omitted values
  retain the native 20–30% compatibility roll.

- Add schema 1.193 optional `spells.tsv` Golem per-level Q6 health metadata.
  Golem health now uses a validated per-level base term; omitted values retain
  the native 320 Q6 compatibility default.

- Add schema 1.192 optional `spells.tsv` Golem summon formula metadata.
  Golem health, damage, and armor now use validated spell-row terms; omitted
  values retain the native 2/3, 2/4/8, and 25 compatibility defaults.

- Add schema 1.191 optional `combat.tsv` melee attack-speed tier metadata.
  Quick/Fast/Faster/Fastest melee timing now uses validated skips for both
  first-frame and subsequent-frame paths; omitted values retain the native
  1/0, 2/1, 3/2, and 4/2 compatibility defaults.

- Add schema 1.190 optional `combat.tsv` hit-recovery tier metadata. Active
  Fast/Faster/Fastest Hit Recovery flags now use validated skip values, while
  omitted values retain the native ordered 1/2/3 behavior.

- Add schema 1.189 optional `combat.tsv`
  `equipment_fast_block_frame_skip` metadata. Equipment FastBlock timing now
  uses the validated combat value, while omitted values retain the native
  two-frame compatibility default.

- Add schema 1.188 optional `classes.tsv` `fast_block_frame_skip` metadata.
  Active FastBlock staff timing now uses the validated class skip value, while
  omitted values retain the native two-frame compatibility default.

- Add schema 1.187 optional `combat.tsv` `pvp_block_level_scale` metadata.
  Player-versus-player block chance now uses the validated level-difference
  scale, while omitted values retain the native scale of two.

- Add schema 1.186 optional `classes.tsv` natural-resistance and Heavy Hit
  threshold tuning metadata. Active class defense now uses validated per-level
  resistance and threshold bonus percentages, while omitted values retain the
  native 100% and 25% behavior.

- Add schema 1.185 optional `classes.tsv` `iron_skin_level_divisor` metadata.
  Active IronSkin armor now uses the validated class divisor, while omitted
  values retain the native level-divided-by-four behavior.

- Add schema 1.184 optional `classes.tsv`
  `critical_strike_chance_per_level_percent` metadata. Active class Critical
  Strike now uses the validated per-level proc chance; omitted values retain
  the native 1% per character level behavior.

- Add schema 1.183 optional `classes.tsv` `critical_damage_multiplier`
  metadata. Active class Critical Strike now uses the validated multiplier,
  while omitted values retain the native 2× behavior for compatibility callers.

- Add schema 1.182 optional `combat.tsv`
  `triple_demon_damage_multiplier` metadata. Active Triple Demon Damage now
  uses the validated multiplier, while direct legacy combat rules retain the
  native 3× behavior.

- Add schema 1.181 optional `combat.tsv`
  `hellfire_peril_damage_multiplier` and
  `hellfire_doppelganger_chance_percent` metadata. Active Hellfire Peril and
  Doppelganger now use validated native terms, while direct legacy combat rules
  retain the native 2× and 10% behavior.

- Add schema 1.180 optional `combat.tsv`
  `hellfire_jesters_roll_maximum`, `hellfire_jesters_expansion_threshold`,
  and `hellfire_jesters_expansion_multiplier` metadata. Active Hellfire
  Jesters now uses the validated roll and upper-half expansion terms, while
  direct legacy combat rules retain the native 0–200/100/5 behavior.

- Add schema 1.179 optional `combat.tsv`
  `hellfire_devastation_chance_percent` and
  `hellfire_devastation_damage_multiplier` metadata. Active Hellfire
  Devastation now uses the validated proc chance and multiplier, while direct
  legacy combat rules retain the native 5%/3× behavior.

- Add schema 1.178 optional `combat.tsv`
  `hellfire_decay_damage_reduction` and `hellfire_decay_removal_threshold`
  metadata. Active Hellfire Decay now uses the validated decrement and removal
  threshold, while direct legacy combat rules retain the native 5/-100 behavior.

- Add schema 1.177 optional `combat.tsv` `thorns_damage_minimum` and
  `thorns_damage_maximum` metadata. Active Thorns retaliation now uses the
  validated damage range, while direct legacy combat rules retain the native 1–3 range.

- Add schema 1.176 optional `combat.tsv`
  `barbarian_vitality_damage_divisor` metadata. Active Barbarian weaponless
  physical damage scaling now uses the validated divisor, while direct legacy
  damage callers retain the native 100 divisor.

- Add schema 1.175 optional `combat.tsv`
  `barbarian_default_strength_damage_divisor` metadata. Active Barbarian
  fallback physical damage scaling now uses the validated divisor, while
  direct legacy damage callers retain the native 100 divisor.
- Add schema 1.174 optional `combat.tsv`
  `barbarian_bow_strength_damage_divisor` metadata. Active Barbarian bow
  physical damage scaling now uses the validated divisor, while direct legacy
  damage callers retain the native 300 divisor.
- Add schema 1.173 optional `combat.tsv`
  `standard_strength_damage_divisor` metadata. Active standard-class fallback
  physical damage scaling now uses the validated divisor, while direct legacy
  damage callers retain the native 100 divisor.
- Add schema 1.172 optional `combat.tsv`
  `bard_default_strength_damage_divisor` metadata. Active Bard fallback
  physical damage scaling now uses the validated divisor, while direct legacy
  damage callers retain the native 100 divisor.
- Add schema 1.171 optional `combat.tsv`
  `bard_bow_strength_dexterity_damage_divisor` metadata. Active Bard bow
  physical damage scaling now uses the validated divisor, while direct legacy
  damage callers retain the native 250 divisor.
- Add schema 1.170 optional `combat.tsv`
  `bard_sword_strength_dexterity_damage_divisor` metadata. Active Bard sword
  physical damage scaling now uses the validated divisor, while direct legacy
  damage callers retain the native 150 divisor.
- Add schema 1.169 optional `combat.tsv` `monk_weapon_damage_divisor`
  metadata. Active Monk non-staff weapon physical damage scaling now uses the
  validated divisor, while direct legacy damage callers retain the native 300 divisor.
- Add schema 1.168 optional `combat.tsv` `monk_staff_unarmed_damage_divisor`
  metadata. Active Monk staff/unarmed physical damage scaling now uses the
  validated divisor, while direct legacy damage callers retain the native 150 divisor.
- Add schema 1.167 optional `combat.tsv`
  `rogue_strength_dexterity_damage_divisor` metadata. Active Rogue physical
  damage scaling now uses the validated divisor, while direct legacy damage
  callers retain the native 200 divisor.
- Add schema 1.166 optional `combat.tsv`
  `barbarian_weapon_strength_damage_divisor` metadata. Active Barbarian
  axe/mace damage scaling now uses the validated divisor, while direct legacy
  damage callers retain the native 75 divisor.
- Add schema 1.165 optional `combat.tsv` `maximum_attribute` metadata. Active
  player equipment recalculation now uses the validated primary-attribute
  ceiling, while direct legacy state and rules callers retain the native 750
  default.
- Add schema 1.164 optional `combat.tsv` `maximum_resistance` metadata. Active
  player elemental resistance now uses the validated cap, while direct legacy
  resistance callers retain the native 75-percent default.
- Add schema 1.163 optional `monster_missiles.tsv` `projectile_damage`
  metadata. Mapped monster projectiles now consume content-owned fixed impact
  damage when declared, while omitted values retain native damage derivation.
- Add schema 1.162 optional `combat.tsv` Acid Puddle player-hit accuracy
  metadata. Persistent puddles now use validated level-floor and base-hit
  terms while older definitions retain the native 10/40 compatibility values.
- Add schema 1.161 optional `combat.tsv` Snake charge special-hit accuracy
  metadata. Charge admission now uses validated content for its native base
  to-hit value while older definitions retain the 500 compatibility value.
- Add schema 1.160 optional `combat.tsv` monster magic-projectile accuracy
  metadata. Monster-owned magic projectiles now use validated base, level, and
  distance terms for player-hit admission while legacy definitions retain the
  native 10/2/2/40/2 formula.
- Add schema 1.159 optional `spells.tsv` Town Portal placement and lifetime
  metadata. Authoritative portal creation now consumes the validated crawl
  radius and expiry ticks while direct legacy world-command callers retain
  five-tile, 100-tick defaults.
- Add schema 1.158 optional `spells.tsv` `warp_search_radius` metadata.
  Authoritative Warp resolution now consumes the validated destination crawl
  radius while direct legacy world-command callers retain the five-tile default.
- Add schema 1.157 optional `spells.tsv` `teleport_search_radius` metadata.
  Authoritative Teleport resolution now consumes the validated native crawl
  radius while direct legacy spell definitions retain the five-tile default.
- Add schema 1.156 optional normalized item `hellfire_effects` metadata. The
  validated symbolic effects project native Hellfire damage/armor flags into
  generated item state while preserving legacy unique and affix compatibility.
- Add schema 1.147 optional item presentation metadata. Normalized and legacy
  item definitions now project validated base and identified names through the
  authoritative item state and existing protocol fields, with legacy tables
  retaining their native-name fallback.
- Add schema 1.148's validated `item_cursor_graphics.tsv` catalog and optional
  normalized `cursor_graphic` item column. Normalized and legacy item loaders
  now share content-owned cursor symbols and native graphic IDs, while direct
  legacy callers retain their compatibility fallback when no catalog is loaded.
- Add schema 1.149 unique-item presentation projection. Names already declared
  by normalized `unique_items.tsv` and legacy `unique_itemdat.tsv` now become
  the generated item's authoritative base and identified names.
- Add schema 1.150 optional unique-item `cursor_graphic` metadata. Normalized
  and legacy unique rows now resolve shared cursor symbols through the active
  catalog, while omitted symbols preserve the base item's cursor.
- Add schema 1.151 optional item `min_monster_level` metadata. Normalized and
  legacy item catalogs now retain native random-drop eligibility thresholds.
- Add schema 1.152 optional item `drop_rate` metadata. Normalized and legacy
  item catalogs now retain native weighted random-drop rates, including zero
  values that exclude an item from selection.
- Add schema 1.153 optional item `usable` metadata. The authoritative
  consumable source gate now honors explicit native iUsable values while
  retaining compatibility fallback when the field is absent.
- Add schema 1.154 optional normalized item `min_armor_class` and
  `max_armor_class` metadata. Normalized item generation now preserves native
  armor ranges when declared and keeps fixed-armor fallback when omitted.
- Add schema 1.155 optional normalized item `special_effects` metadata.
  Symbolic native equipment effects now map to authoritative flags, while
  numeric `flags` and legacy unknown-effect compatibility remain supported.
- Select monster arrow and snake-charge controllers from validated missile
  projectile behavior rather than requiring `GoatRanged`/`Snake` labels or
  `Arrow`/`Rhino` missile names; mapped arrow rows also suppress adjacent
  melee fallback, and legacy `Targeted` arrow rows remain accepted.
- Select Blood Star dispatch from validated `BloodStar` missile behavior
  rather than requiring `Succubus`/`LazarusSuccubus` labels; no-catalog legacy
  rows retain the native-label fallback.
- Remove the remaining `Fat` and `GoatMelee` label gates from validated
  special-attack selection; complete attack tuples now select those branches
  independently of the runtime native-AI label.
- Select Fallen fear radius and countdown behavior from complete validated
  tuning tuples rather than requiring the `Fallen` native-AI name; legacy rows
  without tuning retain native-name compatibility defaults, and persisted fear
  state advances for any validated label carrying the tuple.
- Select Scavenger corpse-healing and search behavior from validated tuning
  independently of the row's `Scavenger` native-AI name; legacy rows without
  migrated tuning retain native-name compatibility defaults, and persisted
  healing/corpse state advances for any validated label carrying the tuple.
- Select Sneak visibility and hit-retreat behavior from complete validated
  tuning tuples rather than requiring the `Sneak` native-AI name; legacy rows
  without tuning retain native-name compatibility defaults, and persisted
  stealth state advances for any validated label carrying the tuple.
- Select Bat adjacent attack and retreat-duration behavior from validated
  tuning regardless of the row's `Bat` native-AI name; legacy rows without
  migrated tuning retain native-name compatibility defaults, and persisted
  retreat ticks/direction advance for any validated label carrying the tuple.
- Select Gargoyle safety, wounded-health, and healing-formula behavior from
  validated tuning regardless of the row's `Gargoyle` native-AI name; legacy
  rows without migrated tuning retain native-name compatibility defaults, and
  the persisted special lock is accepted for any validated label carrying the
  tuple.
- Select SkeletonRanged retreat and clear-lane arrow admission from complete
  validated tuning tuples rather than requiring the `SkeletonRanged` native-AI
  name; legacy rows without tuning retain native-name compatibility defaults,
  and validated ranged tuples suppress adjacent melee fallback.
- Select Butcher pursuit from the active monster-type identity when a validated
  catalog is present unless a complete alternate AI tuple takes precedence;
  direct legacy targets retain the native-label fallback.
- Resolve mapped monster missiles through the active monster-type identity
  before falling back to the runtime label, preserving legacy mappings while
  allowing content-owned identity changes.
- Resolve legacy native-AI fallback controllers through the active monster-type
  identity before consulting runtime labels; complete migrated AI tuning retains
  precedence, and direct targets preserve their compatibility path.
- Select Zombie action, random-direction, and movement-distance behavior from
  complete validated tuning tuples rather than requiring the `Zombie` native-AI
  name; legacy rows without tuning retain native-name compatibility defaults.
- Select Fat/Overlord movement, adjacent attack, and special-band behavior
  from complete validated tuning tuples rather than requiring the `Fat`
  native-AI name; legacy rows without tuning retain native-name compatibility
  defaults.
- Select GoatMelee movement, adjacent attack, and wounded-special behavior from
  complete validated tuning tuples rather than requiring the `GoatMelee`
  native-AI name; legacy rows without tuning retain native-name compatibility
  defaults.
- Select SkeletonMelee movement-delay and adjacent attack admission from
  complete validated tuning tuples rather than requiring the `SkeletonMelee`
  native-AI name; legacy rows without tuning retain native-name compatibility
  defaults.
- Select Rhino charge admission from a complete validated tuning tuple rather
  than requiring the `Rhino` native-AI name; legacy rows without tuning retain
  the native-name compatibility defaults.
- Select Skeleton King summon admission from a complete validated tuning tuple
  rather than requiring the `SkeletonKing` native-AI name; legacy rows without
  tuning retain the native-name compatibility defaults, and summon candidates
  can carry the complete SkeletonMelee tuple under an alternate label.
- Select the migrated Hork Spawn controller from a complete validated Hork
  tuning tuple instead of requiring the `HorkDemon` native-AI name; legacy
  rows without tuning retain the native-name compatibility defaults.
- Require validated `monster_missiles.tsv` Hork Spawn mappings to declare their
  summoned native monster identity, removing the processor's remaining native
  ID fallback while preserving the checked-in Hellfire projection.
- Project validated level dimensions and blocked-cell geometry in additive
  live snapshots so Godot can render the active server world; legacy client
  assets remain only as a fallback and saves/hashes omit static geometry.
- Move the PvP-ear native item-index and miscellaneous-ID projection into
  validated `combat.tsv` content, retaining the native 23/44 defaults for
  legacy rows and covering custom projections with loader and replay tests.
- Classify two-handed equipment from normalized `TwoHanded` tags before
  falling back to native equipment-location metadata for legacy content.
- Validate loadout equipment slots from semantic hand, body-armor, helmet,
  ring, and amulet tags before falling back to native location values.
- Resolve cast-source kind and spell identity from active item catalog metadata
  before reading legacy state fields or native miscellaneous IDs.
- Validate starting belt items from the normalized `Belt` tag before falling
  back to native equipment-location/item-class metadata for legacy content.
- Make the validated `level_generation.tsv` catalog own deterministic
  encounter-count calculation and reject active worlds with uncovered levels;
  preserve static map geometry while procedural synthesis remains future work.
- Make general store recharge validation consult validated `Staff` tags from
  the active item catalog before legacy item-type/miscellaneous-ID fallback.
- Centralize content-first equipment subtype classification for player combat,
  block eligibility, Hellfire weapon decay, loadouts, armor scaling, and monster
  weapon modifiers while retaining native item-type fallback for legacy state.
- Route Gold identity through the shared validated item-classification seam for
  wallet and death-drop topology, retaining native type/index fallback.
- Centralize content-first weapon classification for attack-animation selection,
  Monk flexible blocking, and loadout off-hand validation; conflicting legacy
  item classes now follow validated `Weapon`/subtype tags with numeric fallback.
- Resolve Monk flexible/fast staff blocking from validated `Staff` item tags
  when active content is present, retaining native item-type fallback.
- Use the validated `Gold` item tag for canonical Gold recognition and stack
  generation, while preserving numeric type/index fallback for legacy content.
- Use validated `LightArmor`, `MediumArmor`, and `HeavyArmor` item tags for Monk
  chest-armor scaling, with native item-type fallback for legacy states.
- Make the Sorcerer Recharge class ability resolve Staff eligibility from the
  active item catalog, with numeric item-type fallback for legacy callers.
- Make store recharge validation honor explicit item spell-source metadata,
  while preserving native type/miscellaneous fallback for legacy rows.
- Validate loadout off-hand shield eligibility from semantic item tags, so
  active content can override conflicting native item types while legacy rows
  retain the numeric fallback.
- Select native player attack-animation profiles from validated equipment
  subtype tags, including shield variants, while retaining numeric item-type
  fallback for legacy or untagged content.
- Resolve equipped-item combat categories from validated content tags when the
  active catalog has an unambiguous native-index mapping, while retaining the
  legacy numeric item-type fallback for older or ambiguous rows.
- Add schema 1.146's normalized equipment subtype tags and use them for the
  server's player damage and undead/animal weapon modifiers, preserving the
  native item-type fallback for legacy rows.
- Honor `monster_missiles.tsv` spell identities for Succubus-family Blood Star
  projectiles and validate configured missile spell references against the active
  spell catalog, while preserving legacy inference for older rows.
- Add schema 1.145's optional `consumable_behavior` spell metadata, so Rune of
  Fire special handling is selected by validated content while older tables
  retain the native miscellaneous-ID fallback.
- Preserve charged-source classification when normalized unique items or magic
  affixes add a `SPELL` power after base item generation.
- Add schema 1.144's explicit normalized item spell-source kind, so current
  content no longer derives charged or consumable source identity from native
  miscellaneous IDs while legacy tables retain their compatibility fallback.
- Add schema 1.143's semantic class-skill roles, carrying validated loadout
  capability identities through authoritative snapshots and the protocol so
  Godot no longer hard-codes class-service spell IDs.
- Authorize class item services and trap disarm through the semantic class
  capability role, while retaining the configured spell-ID fallback for legacy
  snapshots and direct compatibility callers.
- Remove Godot's fixed remote-player spell-ID table; the server now resolves
  player targeting from active external spell behavior and preserves tile casts
  for non-player spells.
- Project item-backed spell-source kind through `ItemStateSnapshot`, removing
  Godot's native miscellaneous-ID checks for staffs, scrolls, and runes.
- Add schema 1.142's optional Hellfire class-armor bonuses, moving the native
  demon and undead adjustments into validated combat content while preserving
  compatibility values.
- Add schema 1.141's optional equipment attack-effect tuning, moving the
  native random life-steal divisor and 3/5-percent life/mana channels into
  validated combat content while preserving compatibility values.
- Add schema 1.140's optional monster accuracy formula tuning, moving the
  native level scale, base bonus, and ranged distance penalty into validated
  combat content while preserving compatibility values for older definitions.
- Add schema 1.139's optional monster hit-chance floors, moving the native
  ordinary and level-specific accuracy minimums into validated combat content
  while preserving the compatibility values for older definitions.
- Add schema 1.138's optional Warrior Repair durability level offset, moving
  the native durability-loss denominator tuning into validated class content
  while preserving the legacy offset for older definitions.
- Add schema 1.137's optional Charged Bolt initial path random range, moving
  the native projectile-state roll into validated spell content while
  preserving the legacy 1–15 compatibility range.
- Add schema 1.136's optional Berserk magic-resistance frequency, moving the
  native resistance admission roll into validated spell content while
  preserving the legacy 1-in-2 compatibility roll.
- Add schema 1.135's optional Reflect default spell level, moving the native
  no-explicit-level charge fallback into validated spell content while
  preserving the legacy level-2 behavior.
- Add schema 1.134's optional Rage admission life threshold, moving the native
  cast safety boundary into validated spell content while preserving the
  legacy character-level Q6 fallback.
- Add schema 1.133's optional Mana restoration random-range tuning, moving the
  native initial, character-level, and spell-level rolls into validated spell
  content while preserving legacy behavior.
- Add schema 1.132's optional Gargoyle wounded-health tuning, moving the native
  self-healing admission threshold into validated monster type content while
  preserving legacy target behavior.
- Make Bat retreat phase selection honor each validated row's configured
  retreat duration, preserving the native first-step direction for overlays
  that use a duration other than two ticks.
- Add schema 1.131's optional Scavenger wounded-health tuning, moving the
  native corpse-seeking threshold into validated monster type content while
  preserving legacy target behavior.
- Add schema 1.130's optional GoatMelee wounded-special tuning, moving the
  native health threshold and deterministic random-group gate into validated
  monster type content while preserving legacy target behavior.
- Add schema 1.129's optional Bat retreat-duration tuning, moving the native
  post-hit retreat length into validated monster type content while preserving
  legacy target behavior.
- Add schema 1.128's optional Scavenger healing/search tuning, moving corpse
  duration, search radius, variant healing, and target-health rules into
  validated monster type content while preserving legacy target behavior.
- Add schema 1.127's optional Gargoyle healing-formula tuning, moving the native
  divisor multiplier, random-range modulus, and random offset into validated
  monster type content while preserving legacy target behavior.
- Add schema 1.121's optional monster-Inferno admission tuning, moving Mega's
  distance, intelligence bands, and adjacent choice modulo into validated
  missile content while preserving legacy target behavior.
- Add schema 1.122's optional SkeletonMelee adjacent attack-band tuning,
  moving its native `20 + 2 * intelligence` admission into validated monster
  type content while preserving legacy target behavior.
- Add schema 1.123's optional GoatMelee adjacent attack-band tuning, moving its
  native `23 + 2 * intelligence` admission into validated monster type content
  while preserving legacy target behavior.
- Add schema 1.124's optional Fat attack and special-band tuning, moving the
  native `20 + 4 * intelligence` admission and `15 + 4 * intelligence` special
  lower bound into validated monster type content while preserving legacy
  target behavior.
- Add schema 1.125's optional Zombie random-direction chance tuning, moving its
  native `20 + 2 * intelligence` movement-prepend chance into validated monster
  type content while preserving legacy target behavior.
- Add schema 1.126's mapped-charge trajectory activation, routing Snake and Bat
  through validated missile projectile speed/lifetime fields while preserving
  legacy 18-speed/256-tick defaults for older rows.
- Route the GoatRanged Arrow path through schema 1.120's validated
  ranged-retreat tuning so its dedicated dispatch shares the server-owned
  generic close-range boundary.
- Route the Succubus-family Blood Star path through schema 1.120's validated
  ranged-retreat tuning so its dedicated dispatch shares the server-owned
  generic close-range boundary.
- Add schema 1.120's optional ranged-retreat distance/chance tuning, moving the
  generic native close-range retreat boundary into validated missile content
  while preserving legacy target behavior.
- Add schema 1.119's optional ranged-avoidance distance/chance tuning, moving
  generic native distance and 10,000-scale intelligence chance into validated
  missile content while preserving legacy target behavior.
- Extend schema 1.118's mapped-charge tuning to Snake `SnakeCharge` rows,
  moving the native clear two-tile admission into validated missile content
  while preserving legacy target behavior.
- Add schema 1.117's optional mapped-charge admission tuning, moving the native
  Bat-family charge distance and intelligence-scaled chance into validated
  missile content while preserving legacy target behavior.
- Add schema 1.116's optional Rhino charge-admission tuning, moving the native
  clear-lane charge distance and intelligence-scaled chance into validated
  monster content while preserving legacy target behavior.
- Add schema 1.115's optional SkeletonRanged admission tuning, moving the
  native retreat and clear-lane arrow chance bands into validated monster
  content while preserving legacy target behavior.
- Add schema 1.114's optional Gargoyle safety-boundary tuning, moving the
  native wounded retreat/self-heal distance into validated monster content
  while preserving legacy target behavior.
- Add schema 1.113's optional Hork Demon admission tuning, moving its native
  adjacent attack, Hork Spawn, and non-adjacent movement bands into validated
  monster content while preserving legacy target behavior.
- Add schema 1.112's optional Skeleton King summon-admission tuning, moving the
  native minimum-distance and intelligence-scaled chance into validated monster
  content while preserving legacy target behavior.
- Add schema 1.111's optional Zombie movement-distance tuning, moving the
  native long-range movement threshold into validated monster content while
  preserving legacy target behavior.
- Add schema 1.110's optional Zombie action-band tuning, moving the native
  autonomous action admission chance into validated monster content while
  preserving legacy target behavior.
- Add schema 1.109's optional Fat movement-band tuning, moving the native
  Fat/Overlord non-adjacent movement admission chance into validated monster
  content while preserving legacy target behavior.
- Add schema 1.108's optional GoatMelee movement-band tuning, moving the
  native non-adjacent movement admission chance into validated monster content
  while preserving legacy target behavior.
- Add schema 1.107's optional SkeletonMelee delay-band tuning, moving the
  native movement admission chance into validated monster content while
  preserving legacy target behavior.
- Add schema 1.106's optional Bat attack-band tuning, moving the native
  adjacent attack chance into validated monster content while preserving
  legacy target behavior.
- Add schema 1.105's optional Sneak monster tuning, moving the native
  visibility band and hit-retreat timing into validated monster content while
  preserving legacy target behavior.
- Add schema 1.104's optional Fallen fear monster tuning, moving the native
  fear radius and countdown formula into validated monster content while
  preserving legacy target behavior.
- Add schema 1.103's validated Monk unarmed damage level-divisor columns,
  removing the remaining fixed base-damage divisors from the equipment rule.
- Add schema 1.102's validated `physical_damage_formula` class column, moving
  native equipped-damage formula selection out of symbol-specific equipment
  rules while preserving legacy loader inference.
- Add schema 1.101's validated `FastBlock` and `HeavyHitThreshold` class
  flags, moving Monk staff-block timing and Barbarian hit-stun threshold
  selection out of symbol-specific combat checks while preserving legacy
  class-row defaults.
- Clarify the `monster_missiles.tsv` migration contract: checked-in rows use
  explicit migrated projectile behaviors, while legacy `Targeted` values are
  accepted only as compatibility spellings for older overlays.
- Add Godot right-click combat targeting: the client resolves visible monster
  and living remote-player snapshots to `AttackRequested` intents while the
  server retains all combat admission, timing, hit, damage, and action-state
  ownership; live protocol coverage verifies entity-targeted action state.
  Server-owned SneakHidden monsters are excluded from Godot rendering and target
  selection, and Godot renders authoritative player action indicators without
  simulating them.
- Add the initial server-owned adjacent melee player-versus-player path:
  friendly-mode admission, native animation timing, deterministic physical Q6
  damage, fire/lightning equipment channels with target resistance, equipment
  knockback, native shield/class blocking, lethal player death/PvP-ear handling,
  and managed plus live multiplayer regression coverage. The same `WeaponArrow`
  lifecycle now admits line-of-sight ranged player targets and resolves
  physical/elemental impact plus shield/class blocking.
- Preserve the native timed player-block boundary: player-target melee impacts
  now resolve against the authoritative effect tick; delayed `WeaponArrow`
  impacts receive the same target-side block boundary, and the live protocol
  suite verifies melee shield blocking through a validated content overlay
  without client-side combat simulation.
- Admit targeted Firebolt, Charged Bolt, and Bone Spirit casts against living
  remote players through the same authoritative target validation used by
  ranged attacks. Friendly-fire, level, range, and missile line-of-sight rules
  remain server-owned; existing player-impact resistance, block, damage, and
  death handling are reused, with managed acceptance/rejection and live Godot
  protocol coverage.
- Extend the Godot cast cursor to attach visible same-level remote-player IDs
  for Firebolt, Charged Bolt, and Bone Spirit, while retaining the existing
  Resurrection and Heal Other target rules; the presentation client still
  performs no gameplay admission.
- Extend managed authoritative regression coverage through the Charged Bolt and
  Bone Spirit player-impact paths, including native resource consumption and
  distinct projectile collision lifecycles.
- Add live Godot protocol coverage for a Charged Bolt remote-player cast using
  a validated class-loadout overlay, proving content activation and multiplayer
  impact resolution through the shared protocol.
- Add the matching live Godot protocol coverage for Bone Spirit, completing
  end-to-end player-target coverage for all three supported projectile spells.
- Verify the same generated protocol in live single-player mode, including the
  mode handshake, single-participant snapshot, and authoritative Firebolt cast.
- Add live process-restart persistence coverage: a resumed protocol session now
  restores server-owned movement and class state from the deterministic save
  identity derived from its resume token.
- Add live Godot protocol coverage for Heal Other targeting a remote player
  through a validated memorized-spell loadout; healing remains server-owned and
  the client only supplies the selected entity intent.
- Add live Godot protocol coverage for Resurrection targeting a dead remote
  player, including authoritative death, revive placement, and beam lifecycle
  through one shared protocol.
- Make the checked-in GoatRanged and SkeletonRanged content rows select the
  authoritative `MonsterArrow` behavior directly, while retaining compatibility
  with older `Targeted` overlays.
- Native GoatRanged and SkeletonRanged Arrow mappings now use explicit
  server-owned monster-arrow projectiles with delayed collision and impact
  resolution, including snapshot/save/protocol coverage.
- Add schema 1.85's validated Mega InfernoControl mapping, preserving native
  distance/intelligence admission and monster-owned persistent segments.
- Add schema 1.86's validated Hork Demon identity and server-owned Hork Spawn
  admission through native monster-table identity; missile trajectory remains
  an explicit follow-up controller migration.
- Add schema 1.87's validated Hork Spawn missile mapping, native speed-8
  nine-tick trajectory, and expiry summon controller.
- Add schema 1.88's additive Hellfire `quest_states.tsv` catalog, merging the
  native initial quest lifecycle into the shared authoritative quest state.
- Add schema 1.89's external Hellfire Defiler encounter, Cathedral Map reward,
  and quest completion trigger through validated monster and item content.
- Add schema 1.90's validated unique-monster catalog and opaque protocol/save
  identity, binding Defiler quest completion to the native unique identity.
- Add schema 1.91's optional monster-missile `spell_id`, so mapped monster
  projectile spell identities are supplied by validated content while older
  overlays retain the compatibility inference path.
- Add schema 1.92's optional monster-missile `spawn_monster_id`, moving the
  Hork Spawn summon target into validated Hellfire content while preserving
  compatibility for older rows and snapshots.
- Use schema 1.93's optional missile projectile speed and lifetime fields for
  migrated ranged rows, moving Hork Spawn's native speed-8, nine-tick tuning
  into validated content while preserving legacy defaults.
- Add schema 1.94's optional intelligence-scaled missile lifetime terms, moving
  Acid's native lifetime formula into validated content while preserving the
  compatibility formula for older rows.
- Add schema 1.95's optional Acid puddle-duration terms, moving the native
  random-plus-intelligence puddle lifetime into validated content while
  preserving the legacy formula fallback.
- Add schema 1.96's optional Acid puddle-damage threshold and values, moving
  the native level-based damage rule into validated content while preserving
  compatibility defaults.
- Add schema 1.97's optional monster-missile `damage_type` identity, moving
  checked-in ranged damage channels into validated content while preserving
  legacy spell/name inference for older rows.
- Add schema 1.98's optional `mana_spell_percent` class tuning, moving the
  native Mana spell class multiplier into validated content while preserving
  the 100-percent compatibility default.
- Add schema 1.99's optional `melee_armor_reduction_divisor` class tuning,
  moving the native Hellfire Barbarian melee armor rule into validated content
  while preserving the disabled-rule compatibility default.
- Add schema 1.100's optional `jester_spell_ids` spell tuning, moving Jester's
  ten-way native dispatch table into validated content while preserving the
  compatibility table for older rows.
- Apply externalized Hellfire `THORNS` equipment through the authoritative
  monster-hit boundary, preserving the native 1-3 damage retaliation order.
- Apply externalized Hellfire `DRAINLIFE` equipment once per active non-town
  tick through the server-owned Q6 damage and death boundary.
- Apply externalized `ABSHALFTRAP` equipment to armed-object damage through the
  server-owned Q6 interaction path.
- Apply externalized `FIREDAM` and `LIGHTDAM` equipment channels after a
  successful authoritative physical attack, including native eligibility,
  resistance, deterministic rolls, and Q6 event state.
- Add a shared elemental-equipment replay fixture, carrying native fire and
  lightning item fields through both runners and matching the post-hit
  resistance and RNG checkpoint.
- Route ranged attacks through native `FireArrows` and `LightningArrows`
  channels, while retaining `FireDamage` and `LightningDamage` for melee;
  add a shared arrow-only replay checkpoint.
- Preserve native first-frame attack skips when a player chains a melee or
  base-game ranged attack, including `QuickAttack`, `FastAttack`,
  `FasterAttack`, and `FastestAttack` timing.
- Add the server-owned `WeaponArrow` projectile boundary for native
  `MultipleArrows`: one arrow fires on the attack effect frame and two spread
  arrows activate two ticks later, with physical Q6 damage, elemental channels,
  and deferred impact state persisted through protocol snapshots and saves.
- Route ordinary bow attacks through the same server-owned `WeaponArrow`
  lifecycle, preserving captured physical/elemental state through tile travel,
  impact resolution, replay checkpoints, and native/C# hash parity.
- Add server-owned Fallen fear state and protocol persistence, triggering the
  native nearby-death retreat duration and direction deterministically.
- Add server-owned Sneak visibility and post-hit retreat state, preserving
  native distance bands and preventing hidden monsters from being targeted.
- Add server-owned corpse tiles and Scavenger healing/navigation, with native
  Hellfire corpse consumption and persisted healing goals.
- Add the server-owned SkeletonKing summon boundary, selecting level-valid
  SkeletonMelee content and creating deterministic authoritative summons; the
  selected candidate may now carry the complete behavior tuple under another
  validated native label.
- Add server-owned Bat attack admission and a persisted two-step native retreat
  after adjacent attacks, including protocol, save, and canonical-hash state.
- Add the server-owned Butcher pursuit boundary, routing its native close-range
  chase through deterministic navigation before shared melee resolution.
- Add the server-owned SkeletonMelee movement admission band, preserving the
  native intelligence-based delay before deterministic navigation.
- Make validated SkeletonRanged monsters retreat inside the native four-tile
  boundary and remain ranged-only instead of falling through to melee.
- Add the server-owned GoatMelee non-adjacent movement admission, preserving
  its native intelligence-based avoidance band before shared melee.
- Add the server-owned Fat/Overlord non-adjacent movement admission, preserving
  its native intelligence-based pursuit before shared melee and special attacks.
- Preserve native Stone Curse shatter state for defeated petrified monsters and
  expose corpse conversion consistently in authoritative snapshots.
- Align the compact native replay projection with server-owned corpse tiles and
  refresh all ordinary-death and spell-timeline replay checkpoints; the native
  replay gate now passes all 68 tests alongside the managed checkpoints.
- Add a 1,000-tick autonomous regeneration soak fixture, proving long-running
  native/C# checkpoint convergence without gameplay RNG drift.
- Add a multiplayer autonomous-tick replay fixture, hashing a second participant
  alongside monster movement, attacks, and a late movement command.
- Route replay fixture commands to an explicit participant session, preserving
  per-participant client sequences and native/C# hashes for a secondary move.
- Add a shared deterministic quest-pool selection replay fixture, including
  unavailable-quest rejection and matching native/C# checkpoint hashes.
- Add schema 1.84 Counselor missile mappings for all native intelligence
  variants, including the server-owned monster Charged Bolt controller.
- Route wounded Gargoyles through the native retreat boundary before their
  server-owned self-heal, preserving the intelligence-based safety radius and
  deterministic movement fallback.
- Add schema 1.83 monster-specific `monster_missiles.tsv` rows so Gloom can
  use its native Rhino movement charge without hardcoded monster-type checks.
- Add schema 1.82 ranged-AI mode and avoidance-probability fields to
  `monster_missiles.tsv`, moving the remaining mapped `AiRanged` versus
  `AiRangedAvoidance` selection into validated content.
- Make validated `monster_missiles.tsv` rows authoritative for mapped ranged
  admission, removing the server's native-AI allow-list while retaining each
  migrated behavior's native parity branch.
- Add schema 1.81 `monster_missiles.tsv` mappings so native monster AI missile
  identities and migrated projectile behaviors are validated by the active
  content pack; unsupported controllers remain explicitly `Targeted`.
- Route the native `FireBat` `AiRanged` behavior through its validated Firebolt
  mapping, server-owned projectile travel, and native monster-to-player magic
  hit, resistance, block, and Q6 damage rules.
- Extend the server-owned Blood Star dispatch to native `LazarusSuccubus`
  minions, which retain the native `LazarusMinionAi` -> `AiRanged` path.
- Route the native `Torchant` `AiRanged` behavior through its validated
  Fireball mapping, including server-owned travel, impact crawl, and
  monster-to-player explosion damage.
- Add the explicit `MonsterMagic` projectile behavior and route the native Lich
  family (`Lich`, `ArchLich`, `Psychorb`, and `Necromorb`) through their
  retained flare identities with server-owned travel and magic hit resolution.
- Route the native `BoneDemon` `AiRangedAvoidance` clear-lane admission through
  its validated `BlueFlare2` `MonsterMagic` mapping and server-owned projectile
  travel and magic hit resolution.
- Route the native `Storm` `ThinLightningControl` through the validated
  Lightning mapping, preserving its speed-32 controller, doubled monster
  damage, persistent segments, and server-owned monster-to-player hit path.
- Route native `Diablo` `DiabloApocalypse` through a validated Apocalypse
  controller that emits physical booms for clear-lane players and resolves
  their server-owned monster-to-player damage.
- Route native `Magma` `MagmaBall` through the validated server-owned fire
  projectile path, preserving its avoidance admission, speed, and hit rules.
- Route native `Acid`/`AcidUnique` `Acid` through server-owned Acid, splat,
  and persistent puddle stages with native resistance and hit rules.
- Validate native `Arrow` identities for `GoatRanged` and `SkeletonRanged`
  through the active server monster-missile catalog before their authoritative
  ranged attack boundaries execute.
- Route native `Snake` Rhino charges through a validated server-owned charge
  controller with native movement, special damage, blocking, and Q6 hit rules.
- Add schema 1.79 native monster AI identities, validated from the native
  `monstdat.tsv` catalog and carried through authoritative state, snapshots,
  protocol field 43, replay fixtures, and canonical hashes.
- Add schema 1.80 native monster intelligence, validated from the native
  `monstdat.tsv` catalog and carried through authoritative state, snapshots,
  protocol field 44, replay fixtures, and canonical hashes; Rhino charge
  probability now follows the native intelligence boundary.
- Route the native `SkeletonRanged` clear-lane arrow boundary through the
  authoritative accuracy, damage, and directed-event path, retaining blocked
  lane fallback.
- Route the native `Gargoyle` wounded self-heal through the authoritative
  Healing event path, persisting its distance-based special lock in protocol
  field 45, snapshots, saves, and canonical hashes.
- Route the native `GoatRanged` shared `AiRanged` boundary through the
  authoritative arrow damage, close-range avoidance, and movement paths.
- Route the native `GoatMelee` adjacent attack-admission and wounded special
  branch through the authoritative physical-hit path.
- Route the native `SkeletonMelee` adjacent attack admission through the
  authoritative physical-hit path.
- Route the native `Zombie` action band and long-range movement boundary through
  authoritative server simulation.
- Align the native `Fat` normal and special melee bands with Overlord's
  `15 + 4 * intelligence` and `20 + 4 * intelligence` boundaries.
- Route native live-monster regeneration through externalized level metadata and
  authoritative Q6 health state before AI execution.
- Route native `Succubus` `AiRanged` attacks through server-owned Blood Star
  projectiles with magic hit, block, resistance, and Q6 damage resolution.

## 2026-08-13

- Extend the authoritative reconnect soak to twelve consecutive resume cycles
  while a second participant remains connected, checking participant ordering
  and canonical snapshot hashes on every resumed session.
- Add an eight-cycle live Godot-client reconnect soak against the v0.2
  authoritative server, verifying the resumed session and the still-connected
  participant's self-first snapshots.
- Add an eight-generation host ruleset replacement soak that migrates two
  sessions and verifies safe retirement of every prior adapter.
- Add a validated Diablo monster-candidate query by dungeon-level range, keeping
  procedural map layout and spawn placement outside the current data contract.
- Add a bounded server-owned monster spawn planner using validated density
  metadata and seed-stable walkable placements; native map synthesis remains
  separate.
- Expose the spawn planner through validated Diablo content activation so the
  server adapter can consume it without bypassing module/data boundaries.
- Convert planned placements into authoritative combat targets and add an
  opt-in standalone-server path (`--procedural-monsters`, plus an optional
  level override); static encounter rows remain the compatibility default.
- Materialize the same deterministic encounter policy when players enter new
  levels, with one-time level tracking and collision-safe reserved entity IDs.
- Filter generated placements against authoritative occupancy, including route
  endpoints, objects, items, monsters, portals, and players.
- Add validated monster-type `aggro_range` metadata in schema 1.75 and carry it
  into generated targets so autonomous server behavior is not passive by
  default.
- Add bounded deterministic monster pathing around blocked geometry while
  preserving the existing direct-step order and server-owned occupancy rules.
- Carry validated monster drop policies into deterministic procedural targets,
  including generated item identity, seed, price, and unique/Diablo encounter
  metadata.
- Preserve validated monster Berserk immunity when converting procedural
  placements into authoritative combat targets.
- Externalize native monster death-animation durations through schema 1.76,
  derive the checked-in Hellfire values from native frame/rate metadata, and
  carry them into static and procedural authoritative targets.
- Externalize native per-monster experience rewards through schema 1.77 and
  award the validated value at the server-owned death boundary.
- Externalize native monster normal and special attack accuracy through schema
  1.78 and apply the native normal-attack level, armor, and minimum-hit formula
  at the authoritative autonomous attack boundary, retaining the legacy global
  fallback for hand-built targets.
- Externalize native monster `MonsterAIID` dispatch identities through schema
  1.79, validating them from native Hellfire data and carrying them through
  authoritative state, protocol, saves, replay fixtures, and canonical hashes.
- Route the native `Fat` AI special-melee band through the authoritative combat
  boundary using the externalized special accuracy and damage range.
- Route the native `Rhino` AI clear-lane charge through authoritative movement,
  special damage, knockback, and blocked-lane fallback state.
- Adopt protocol v0.2 as the production default for the C# server, Godot
  launcher, and native authoritative launcher, while retaining explicit v0.1
  compatibility selection.
- Add protocol v0.2 independent content-manifest and gameplay-ruleset
  handshake identities while preserving the v0.1 combined-hash alias.
- Enforce the supplied ruleset identity during the Godot client handshake, so
  a client cannot accept a server with incompatible gameplay modules.
- Align native replay canonical hashing with the C# snapshot contract for
  projectile controller runtime state, object/quest extensions, and deferred
  attack RNG consumption; the native replay gate now passes all 68 tests.
- Scope armed-trap visibility to the requesting player's native `TrapSense`
  class flag; authoritative saves and internal state retain the full trap state.
- Apply validated class base modifiers to native physical to-hit: initialized
  content players use `level + dexterity / 2` for melee or `level + dexterity`
  for ranged attacks, with the class's external base modifier and a preserved
  global-hit fallback for legacy templates without class attributes.
- Resolve initialized physical hits against target armor using native 5–95
  clamping, base-game armor-piercing addition, Hellfire armor scaling, and the
  Barbarian melee armor reduction; legacy uninitialized templates retain their
  compatibility hit path.
- Derive initialized physical damage from usable equipped native min/max
  damage and class-specific strength/dexterity modifiers, including native
  unarmed and shield fallbacks; legacy templates retain global base damage.
- Apply native sword and mace damage modifiers against externally classified
  undead targets at the authoritative physical damage boundary.
- Apply native sword and mace damage modifiers against externally classified
  animal targets at the authoritative physical damage boundary, carrying the
  validated class capability through protocol, saves, snapshots, and hashes.
- Externalize native monster `MFLAG_KNOCKBACK` through the content schema and
  apply its server-owned one-tile player displacement after successful monster
  hits, preserving blocked-destination behavior through snapshots and hashes.
- Apply the native class `CriticalStrike` rule to eligible authoritative player
  attacks: a level-based post-hit roll doubles physical damage before armor and
  downstream class/item effects, with the native RNG draw order preserved.
- Apply the native class `IronSkin` armor bonus from validated class flags,
  adding character level divided by four after equipment-derived armor.
- Externalize the native Monk chest-armor bonuses in schema 1.74: light,
  ordinary medium, unique medium, and unique heavy armor now contribute their
  native level-scaled effective armor through the authoritative stat boundary.
- Preserve Q6 physical attack damage through authoritative hit, knockback, and
  on-hit resource handling, and apply native Hellfire `Jesters` scaling.
- Apply native Hellfire `Peril`: self-damage uses pre-modifier attack damage
  plus `PlusGetHit`, while the final physical hit is doubled in Q6; Mana Shield
  and death handling remain authoritative.
- Apply native Hellfire `Doppelganger` spawning after its authoritative 10%
  post-hit roll, including unique/Diablo exclusions and deterministic adjacent
  placement for a full-health non-unique copy.
- Apply native Hellfire `Decay` at the authoritative attack boundary, reducing
  eligible hand weapons' damage bonus by five in native order and removing
  each at the native -100 threshold.
- Complete validated external item-power mappings for all native Hellfire
  damage/armor effects: `Devastation`, `Decay`, `Peril`, `Jesters`,
  `Doppelganger`, `ACAgainstDemons`, and `ACAgainstUndead`.
- Apply Hellfire `Devastation` as a native 5% post-hit triple-damage roll,
  preserving authoritative RNG ordering and equipment eligibility.
- Apply native Hellfire `ACAgainstDemons` and `ACAgainstUndead` equipment
  armor bonuses to authoritative autonomous monster hits.
- Externalize native monster demon-class capability through schema 1.71 and
  apply usable identified `TripleDemonDamage` equipment at the authoritative
  physical attack boundary.
- Apply native usable identified `Knockback` equipment after successful
  non-lethal physical hits, preserving petrified and blocked-destination rules.
- Apply native usable identified life/mana-on-hit equipment effects after
  successful authoritative physical attacks, including deterministic random
  life steal and `NoMana` suppression for mana steal.
- Apply usable identified equipment `PlusGetHit` metadata to authoritative
  autonomous monster physical hits, preserving the configured minimum damage.
- Honor usable identified equipment with the native `NoMana` flag when
  admitting memorized spell casts; free class abilities and charged or
  consumable sources remain unaffected, while Mana Shield keeps its existing
  native bypass behavior.
- Apply native `ZeroResistance` item flags to authoritative elemental
  resistance after natural class resistance and before the native 0..75 clamp.
- Apply usable identified equipment damage-percent, flat-damage, to-hit, and
  armor-piercing metadata to authoritative player attacks.
- Recompute effective player armor from equipped item armor, percentage armor,
  native dexterity, and identification/requirement rules across authoritative
  creation, transfers, and snapshot restoration.
- Apply native usable identified equipment strength, magic, dexterity, and
  vitality bonuses to authoritative current attributes.
- Apply class-scaled usable identified equipment vitality and magic to
  authoritative life and mana maxima, preserving native resource eligibility.

## 2026-08-13

- Apply externalized native item life/mana bonuses alongside Hellfire resource
  conversions during player creation and live equipment transfers.

## 2026-08-13

- Make Hellfire mana-to-life and life-to-mana conversions server-owned across
  live equipment transfers, preserving native resource debt and persisting
  base/effective maxima through snapshots, saves, protocol fields 31-32, and
  conditional canonical hashes.

## 2026-08-13

- Externalize Hellfire Gladiator's Ring (`unique_id=1111`) through schema 1.70,
  reusing native Ring with its player-dependent 40-percent life-to-mana
  conversion preserved as authoritative item metadata.

## 2026-08-13

- Externalize Hellfire Acolyte's Amulet (`unique_id=1110`) through schema 1.69,
  reusing native Amulet with its player-dependent 50-percent mana-to-life
  conversion preserved as authoritative item metadata.

## 2026-08-13

- Externalize Hellfire Demon Plate Armor (`unique_id=1109`) through schema 1.68,
  reusing native Full Plate with fixed armor and extra armor against demons.

## 2026-08-13

- Externalize Hellfire Bone Chain Armor (`unique_id=1108`) through schema 1.67,
  reusing native Chain Mail with fixed armor and extra armor against undead.

## 2026-08-13

- Externalize Hellfire Diamondedge (`unique_id=1107`) through schema 1.66,
  reusing native Long Sword with fixed durability, to-hit, damage, resistance,
  and fixed-armor powers.

## 2026-08-13

- Externalize Hellfire Eater of Souls (`unique_id=1106`) through schema 1.65,
  reusing native Two-Handed Sword with indestructibility, life, life-steal,
  mana-steal, and drain-life powers.

## 2026-08-13

- Externalize Hellfire Shirotachi (`unique_id=1105`) through schema 1.64,
  reusing native Great Sword with one-handed, fastest-attack, target-armor,
  and lightning-damage powers.

## 2026-08-13

- Externalize Hellfire Thunderclap (`unique_id=1104`) through schema 1.63,
  reusing native War Hammer with charged-bolt hit damage, strength, resistance,
  light, and indestructible powers.

## 2026-08-13

- Externalize Hellfire Blitzen (`unique_id=1103`) through schema 1.62,
  reusing native Composite Bow with lightning-arrow damage, zero base damage,
  and indestructible powers.

## 2026-08-13

- Externalize Hellfire Armor of Gloom (`unique_id=1102`) through schema 1.61,
  reusing native Full Plate with zero strength requirement, fixed armor 225,
  all-resistance reset, and light curse.

## 2026-08-13

- Externalize Hellfire Flambeau (`unique_id=1101`) through schema 1.60,
  reusing the native Composite Bow base and adding Fireball-bow damage,
  zero base damage, and indestructible powers.

## 2026-08-13

- Externalize Hellfire Gnat Sting (`unique_id=1100`) through schema 1.59,
  reusing the native Hunter's Bow base with multi-arrow, damage, fast-attack,
  and indestructible powers.

## 2026-08-13

- Externalize Hellfire Amulet of Warding (`unique_id=1099`) through schema
  1.58, adding the native AMULET base identity with all-resistance and
  life-curse powers.

## 2026-08-13

- Externalize Hellfire Ring of Thunder (`unique_id=1098`) through schema
  1.57, reusing the corrected native Ring base item with lightning resistance
  and fire/magic-resistance curses.

## 2026-08-13

- Externalize Hellfire Ring of the Mystics (`unique_id=1097`) through schema
  1.56, reusing the corrected native Ring base item with magic resistance and
  fire/lightning-resistance curses.

## 2026-08-13

- Externalize Hellfire Ring of Magma (`unique_id=1096`) through schema
  1.55, reusing the corrected native Ring base item with fire resistance and
  lightning/magic-resistance curses.

## 2026-08-13

- Externalize Hellfire Karik's Ring (`unique_id=1095`) through schema
  1.54, reusing the corrected native Ring base item with vitality and
  magic-curse powers.

## 2026-08-13

- Externalize Hellfire Xorine's Ring (`unique_id=1094`) through schema
  1.53, reusing the corrected native Ring base item with magic and
  strength-curse powers.

## 2026-08-13

- Externalize Hellfire Mercurial Ring (`unique_id=1093`) through schema
  1.52, reusing the corrected native Ring base item with dexterity and
  strength-curse powers.

## 2026-08-13

- Externalize Hellfire Giant's Knuckle (`unique_id=1092`) through schema
  1.51, reusing the corrected native Ring base item with strength and
  dexterity-curse powers.

## 2026-08-13

- Externalize Hellfire Ring of Engagement (`unique_id=1091`) through schema
  1.50, reusing the corrected native Ring base item with get-hit, thorns,
  armor, and target-armor powers.

## 2026-08-13

- Externalize Hellfire Constricting Ring (`unique_id=1090`) through schema
  1.49, reusing the corrected native Ring base item with all-resistance and
  life-drain powers.

## 2026-08-13

- Externalize Hellfire The Bleeder (`unique_id=1089`) through schema 1.48,
  reusing the corrected native Ring base item with magic-resistance, mana,
  and life-curse powers.

## 2026-08-13

- Externalize Hellfire Ring of Regha (`unique_id=1088`) through schema 1.47,
  reusing the corrected native Ring base item with magic, magic-resistance,
  light, strength-curse, and dexterity-curse powers.

## 2026-08-13

- Externalize Hellfire Bramble (`unique_id=1087`) through schema 1.46,
  reusing the native Ring base item with cursed attributes, damage modifier,
  and mana powers.

## 2026-08-13

- Externalize Hellfire Stormshield (`unique_id=1086`) through schema 1.45,
  adding its native Gothic Shield base item with armor, get-hit,
  strength, indestructible, fast-block, and lightning-resistance powers.

## 2026-08-13

- Externalize Hellfire Holy Defender (`unique_id=1085`) through schema 1.44,
  adding its native Large Shield base item with armor, get-hit,
  fire-resistance, durability, and fast-block powers.

## 2026-08-13

- Externalize Hellfire Blackoak Shield (`unique_id=1084`) through schema 1.43,
  adding its native Small Shield base item with armor, dexterity,
  vitality-curse, light-curse, and durability powers.

## 2026-08-13

- Externalize Hellfire Dragon's Breach (`unique_id=1083`) through schema 1.42,
  adding its native Kite Shield base item with armor, fire-resistance,
  strength, magic-curse, and indestructible powers.

## 2026-08-13

- Externalize Hellfire Split Skull Shield (`unique_id=1082`) through schema
  1.41, reusing the native Buckler base item with armor, life, strength,
  light-curse, and durability powers.

## 2026-08-13

- Externalize Hellfire The Deflector (`unique_id=1081`) through schema 1.40,
  adding its native Buckler base item with armor, resistance, damage-curse,
  and to-hit-curse powers.

## 2026-08-13

- Externalize Hellfire Demonspike Coat (`unique_id=1080`) through schema
  1.39, adding its native Full Plate Mail base item with armor, get-hit,
  strength, indestructible, and fire-resistance powers.

## 2026-08-13

- Externalize Hellfire Naj's Light Plate (`unique_id=1079`) through schema
  1.38, adding its native Plate Mail base item with no-strength, magic, mana,
  all-resistance, and spell-level powers.

## 2026-08-13

- Externalize Hellfire Nightscape (`unique_id=1078`) through schema 1.37,
  adding its native Cape base item with fast-recovery, light-curse, armor,
  dexterity, and all-resistance powers.

## 2026-08-13

- Externalize Hellfire Scavenger Carapace (`unique_id=1077`) through schema
  1.36, adding its native Breast Plate base item with armor, get-hit,
  armor-curse, dexterity, and light-resistance powers.

## 2026-08-13

- Externalize Hellfire Sparking Mail (`unique_id=1076`) through schema 1.35,
  adding its native Chain Mail base item with armor and lightning-damage
  powers.

## 2026-08-13

- Externalize Hellfire Wisdom's Wrap (`unique_id=1075`) through schema 1.34,
  adding its native Robe base item with magic, mana, light-resistance, armor,
  and get-hit powers.

## 2026-08-13

- Externalize Hellfire Leather of Aut (`unique_id=1074`) through schema 1.33,
  adding its native Leather Armor base item with armor, strength,
  magic-curse, dexterity, and indestructible powers.

## 2026-08-13

- Externalize Hellfire The Rainbow Cloak (`unique_id=1073`) through schema
  1.32, adding its native Cloak base item with armor, attribute, resistance,
  life, and durability powers.

## 2026-08-13

- Externalize Hellfire The Gladiator's Bane (`unique_id=1072`) through schema
  1.31, adding its native Studded Leather Armor base item with armor, get-hit,
  durability, and cursed-attribute powers.

## 2026-08-13

- Externalize Hellfire Torn Flesh of Souls (`unique_id=1071`) through schema
  1.30, adding its native Rags base item with armor, vitality, get-hit, and
  indestructible powers.

## 2026-08-13

- Externalize Hellfire Royal Circlet (`unique_id=1070`) through schema 1.29,
  adding its native Crown base item with attribute, mana, armor, and light
  powers.

## 2026-08-13

- Externalize Hellfire Gotterdamerung (`unique_id=1069`) through schema 1.28,
  adding its native Great Helm base item with attribute, armor, get-hit,
  resistance, and light-curse powers.

## 2026-08-13

- Externalize Hellfire Fool's Crest (`unique_id=1068`) through schema 1.27,
  reusing the native Helm base item with cursed attributes, life, get-hit,
  and thorns powers.

## 2026-08-13

- Externalize Hellfire OverLord's Helm (`unique_id=1067`) through schema 1.26,
  reusing the native Helm base item with attribute, magic-curse, and
  durability powers.

## 2026-08-13

- Externalize Hellfire Thinking Cap (`unique_id=1066`) through schema 1.25,
  adding its native Skull Cap base item with mana, spell-level, resistance,
  and durability powers.

## 2026-08-13

- Externalize Hellfire Helm of Spirits (`unique_id=1065`) through schema 1.24,
  adding its native Helm base item and life-steal power.

## 2026-08-13

- Externalize Hellfire Rod of Onan (`unique_id=1064`) through schema 1.23,
  reusing the validated War Staff base item with charged-spell, damage, and
  attribute powers.

## 2026-08-13

- Externalize Hellfire Mindcry (`unique_id=1063`) through schema 1.22,
  adding its native Quarter Staff base item with magic, charged-spell,
  all-resistance, and spell-level powers.

## 2026-08-13

- Externalize Hellfire Naj's Puzzler (`unique_id=1062`) through schema 1.21,
  reusing the validated Long Staff base item with magic, dexterity,
  all-resistance, charged-spell, and life-curse powers.

## 2026-08-13

- Externalize Hellfire The Protector (`unique_id=1061`) through schema 1.20,
  reusing the validated Short Staff base item with vitality, get-hit, armor,
  charged-spell, and thorns powers.

## 2026-08-13

- Externalize Hellfire Thundercall (`unique_id=1060`) through schema 1.19,
  adding its native Composite Staff base item with to-hit, lightning-damage,
  charged-spell, lightning-resistance, and light powers.

## 2026-08-13

- Externalize Hellfire Gleamsong (`unique_id=1059`) through schema 1.18,
  adding its native Short Staff base item with mana, strength-curse,
  vitality-curse, and charged-spell powers.

## 2026-08-13

- Externalize Hellfire Storm Spire (`unique_id=1058`) through schema 1.17,
  adding its native War Staff base item with lightning-resistance,
  lightning-damage, strength, and magic-curse powers.

## 2026-08-13

- Externalize Hellfire Immolator (`unique_id=1057`) through schema 1.16,
  reusing the validated Long Staff base item with fire-resistance, fire-damage,
  mana, and vitality-curse powers.

## 2026-08-13

- Externalize Hellfire Staff of Shadows (`unique_id=1056`) through schema 1.15,
  adding its native Long Staff base item with magic-curse, to-hit, damage,
  light-curse, and fast-attack powers.

## 2026-08-13

- Externalize Hellfire Dreamflange (`unique_id=1055`) through schema 1.14,
  reusing the validated Mace base item with magic, mana, magic-resistance,
  light, and spell-level powers.

## 2026-08-13

- Externalize Hellfire Schaefer's Hammer (`unique_id=1054`) through schema
  1.13, adding its native War Hammer base item with damage-curse, lightning
  damage, life, to-hit, lightning-resistance, and light powers.

## 2026-08-13

- Externalize Hellfire The Cranium Basher (`unique_id=1053`) through schema
  1.12, reusing the validated Maul base item with damage-modifier, strength,
  indestructibility, mana-curse, and all-resistance powers; correct the native
  Maul index on Hammer of Jholm's normalized base row.

- Externalize Hellfire Gnarled Root (`unique_id=1052`) through schema 1.11,
  reusing the validated Spiked Club base item with to-hit, damage, dexterity,
  magic, all-resistance, and armor-curse powers.

- Externalize Hellfire Baranar's Star (`unique_id=1051`) through schema 1.10,
  adding its native Morning Star base item with to-hit, damage, attack-speed,
  vitality, dexterity-curse, and durability powers.

- Externalize Hellfire The Celestial Star (`unique_id=1050`) through schema
  1.9, adding its native Flail base item with no-strength, light,
  damage-modifier, and armor-curse powers.

- Externalize Hellfire Civerb's Cudgel (`unique_id=1049`) through schema 1.8,
  reusing the validated Mace base item with demon damage, dexterity-curse,
  and magic-curse powers.

- Externalize Hellfire Hammer of Jholm (`unique_id=1048`) through schema 1.7,
  adding its native Maul base item with fixed damage, indestructibility,
  strength, and to-hit powers.

- Externalize Hellfire Crackrust (`unique_id=1047`) through schema 1.6,
  adding its native Mace base item with attributes, indestructibility,
  all-resistance, damage, and spell-level powers.

- Externalize Hellfire Messerschmidt's Reaver (`unique_id=1046`) through schema
  1.5, adding its native Great Axe base item with damage, damage-modifier,
  attribute, life-curse, and fire-damage powers.

- Externalize Hellfire Hellslayer (`unique_id=1045`) through schema 1.4,
  reusing the validated Battle Axe base item with strength, vitality, damage,
  life, and mana-curse powers.

- Externalize Hellfire Aguinara's Hatchet (`unique_id=1044`) through schema 1.3,
  adding its native Small Axe base item with spell-level, magic, and
  magic-resistance powers.

- Externalize Hellfire Stonecleaver (`unique_id=1043`) through schema 1.2,
  reusing the validated Broad Axe base item with life, to-hit, damage,
  and lightning-resistance powers.

- Externalize Hellfire Wicked Axe (`unique_id=1042`) through schema 1.1,
  reusing the validated Large Axe base item with to-hit, dexterity,
  vitality-curse, hit-curse, and indestructibility powers.

- Externalize Hellfire The Celestial Axe (`unique_id=1041`) through schema 1.0,
  including its native Battle Axe base item with no-strength requirement,
  to-hit, life, and strength-curse powers.

- Externalize Hellfire BloodSlayer (`unique_id=1040`) through schema 0.99,
  including its native Broad Axe base item with damage, demon damage,
  all-attribute-curse, and spell-level-curse powers.

- Externalize Hellfire Sharp Beak (`unique_id=1039`) through schema 0.98,
  reusing the validated Large Axe base item with life, magic-curse, and
  mana-curse powers.

- Externalize Hellfire The Mangler (`unique_id=1038`) through schema 0.97,
  including its native Large Axe base item with damage, dexterity-curse,
  magic-curse, and mana-curse powers.

- Externalize Hellfire The Grandfather (`unique_id=1037`) through schema 0.96,
  including its native Great Sword base item with one-handed, attribute,
  to-hit, damage, and life powers.

- Externalize Hellfire Doombringer (`unique_id=1036`) through schema 0.95,
  including its native Bastard Sword base item with to-hit, damage,
  all-attribute-curse, life-curse, and light-curse powers.

- Externalize Hellfire Inferno (`unique_id=1035`) through schema 0.94,
  reusing the validated Long Sword base item with fire damage, light, mana,
  and fire-resistance powers.

- Externalize Hellfire The Falcon's Talon (`unique_id=1034`) through schema
  0.93, including its native Scimitar base item with fast-attack, to-hit,
  damage-curse, and dexterity powers.

- Externalize Hellfire Lightsabre (`unique_id=1033`) through schema 0.92,
  reusing the validated Sabre base item with light, lightning-damage, to-hit,
  and lightning-resistance powers.

- Externalize Hellfire Wizardspike (`unique_id=1032`) through schema 0.91,
  reusing the validated Dagger base item with magic, mana, to-hit, and
  all-resistance powers.

- Externalize Hellfire Shadowhawk (`unique_id=1031`) through schema 0.90,
  including its native Broad Sword base item with light-curse, life-steal,
  to-hit, and all-resistance powers.

- Externalize Hellfire The Bonesaw (`unique_id=1030`) through schema 0.89,
  including its native Claymore base item with damage-modifier, strength,
  magic/dexterity-curse, life, and mana-curse powers.

- Externalize Hellfire The Executioner's Blade (`unique_id=1029`) through
  schema 0.88, reusing the native Falchion base item with damage, life-curse,
  light-curse, and durability powers.

- Externalize Hellfire Ice Shank (`unique_id=1028`) through schema 0.87,
  including its native Long Sword base item with fire-resistance, durability,
  and strength powers.

- Externalize Hellfire Gibbous Moon (`unique_id=1027`) through schema 0.86,
  including its native Broad Sword base item with attribute, damage, mana, and
  light-curse powers.

- Externalize Hellfire Black Razor (`unique_id=1026`) through schema 0.85,
  reusing the native Dagger base item with damage, vitality, and durability
  powers.

- Externalize Hellfire Gryphon's Claw (`unique_id=1025`) through schema 0.84,
  including its native Falchion base item with damage, magic-curse, and
  dexterity-curse powers.

- Externalize Hellfire The Defender (`unique_id=1024`) through schema 0.83,
  including its native Sabre base item and armor, vitality, and hit-curse
  powers.

- Externalize Hellfire Gonnagal's Dirk (`unique_id=1023`) through schema 0.82,
  including its native Dagger base item and dexterity-curse, damage-modifier,
  attack-speed, and fire-resistance powers.

- Externalize Hellfire Eaglehorn (`unique_id=1022`) through schema 0.81,
  including its native Battle Bow base item and dexterity, hit, damage, and
  indestructibility powers.

- Externalize Hellfire Windforce (`unique_id=1021`) through schema 0.80,
  including its native War Bow base item and strength, damage, and knockback
  powers.

- Externalize Hellfire Fleshstinger (`unique_id=1020`) through schema 0.79,
  reusing the validated Long Bow base item with native dexterity, hit, damage,
  and durability powers.

- Externalize Hellfire Flamedart (`unique_id=1019`) through schema 0.78,
  including its native Hunter's Bow base item and fire-arrow, hit, and
  fire-resistance powers.

- Externalize Hellfire The Blackoak Bow (`unique_id=1018`) through schema 0.77,
  reusing the validated Long Bow base item with native attribute, damage, and
  light/vitality-curse powers.

- Externalize Hellfire Bow of the Dead (`unique_id=1017`) through schema 0.76,
  reusing the validated Composite Bow base item with native hit, attribute,
  curse, and durability powers.

- Externalize Hellfire Deadly Hunter (`unique_id=1016`) through schema 0.75,
  including its native composite-bow base item and triple-damage, hit, and
  magic-curse powers.

- Externalize Hellfire The Celestial Bow (`unique_id=1015`) through schema
  0.74, including its native long-bow base item and minimum-strength,
  damage, and armor powers.

- Externalize Hellfire The Needler (`unique_id=1014`) through schema 0.73,
  reusing the validated Short Bow base item with native hit, fixed-damage, and
  attack-speed powers.

- Externalize Hellfire The Butcher's Cleaver (`unique_id=1013`) through schema
  0.72, including its native two-handed axe base item and strength,
  fixed-damage, and durability powers.

- Externalize Hellfire The Rift Bow (`unique_id=1012`) through schema 0.71,
  reusing the validated Short Bow base item with native arrow-velocity,
  damage, and dexterity-curse powers.

- Externalize Hellfire Bovine Plate (`unique_id=1011`) through schema 0.70,
  including its native heavy-armor base item and armor, indestructibility,
  light, resistance, mana-curse, and spell-level powers.

- Externalize Hellfire Griswold's Edge (`unique_id=1010`) through schema 0.69,
  including its native one-handed base item and fire-damage, hit, attack-speed,
  knockback, mana, and life-curse powers.

- Externalize native Hellfire acid immunity through schema 0.68, add the
  authoritative Acid damage channel, and carry acid resistance through
  encounter inheritance, snapshots, saves, hashes, protocol field 36, and
  replay validation.

- Externalize native monster resistance percentages through validated monster
  type metadata; encounter rows inherit type resistances unless they provide
  an explicit instance override, with the content schema advanced to 0.67.

- Apply validated monster-type baseline damage, level, armor, and capability
  metadata when encounter rows omit per-instance values, while preserving
  explicit encounter overrides.

- Track native monster IDs separately from stable type IDs and expose reverse
  lookup only for unambiguous identities, preserving composed base/Hellfire
  overlays whose variant-local IDs may overlap.

- Extend the Hellfire monster-type catalog with the next twelve native type
  identities (native IDs 12–23), including validated baseline combat,
  resistance, and undead capability metadata.

- Extend the Hellfire monster-type catalog through native ID 35 with twelve
  more validated identities, including special damage channels and native
  demon/undead resistance baselines.

- Extend the Hellfire monster-type catalog through native ID 47 with twelve
  more validated identities; at that point the unsupported native acid
  resistance remained an explicit future schema extension rather than being
  silently discarded.

- Extend the Hellfire monster-type catalog through native ID 59 with twelve
  more validated identities, preserving boss, demon, undead, resistance, and
  special-damage baselines supported by the current schema.

- Extend the Hellfire monster-type catalog through native ID 71 with twelve
  more validated elemental, boss, and demon identities, including their
  supported special-damage and resistance baselines.

- Extend the Hellfire monster-type catalog through native ID 83 with twelve
  more validated fire, storm, and demon identities, including supported
  special-damage and resistance baselines.

- Extend the Hellfire monster-type catalog through native ID 95 with twelve
  more validated demon identities and their supported resistance baselines.

- Extend the Hellfire monster-type catalog through native ID 107 with twelve
  more validated undead and demon identities, preserving their supported
  resistance baselines.

- Extend the Hellfire monster-type catalog through native ID 119 with twelve
  more validated caster, boss, and creature identities and supported
  resistance baselines.

- Extend the Hellfire monster-type catalog through native ID 131 with twelve
  more validated spider, demon, and undead identities, including supported
  special-damage baselines.

- Complete the Hellfire native monster-type identity catalog through native ID
  137, including the final Bone Demon, Arch Lich, Biclops, Flesh Thing, Reaper,
  and Na-Krul rows with supported special-damage metadata.

- Apply native single-player quest-pool exclusions to server-owned quest state
  during content activation. Unavailable quests reject progression and linked
  object interaction, and their state persists through snapshots, saves,
  protocol field 6, and canonical hashes.

- Preserve the native quest initial/active/done lifecycle and quest-log bit in
  external quest rows, server transitions, snapshots, saves, protocol fields
  7-8, and canonical hashes; refresh the affected deterministic replay
  checkpoints.

- Preserve native quest `_qvar1`, `_qvar2`, and `_qmsg` state through optional
  content columns, authoritative saves, snapshots, protocol fields 9-11, and
  compatibility-aware hashes.

- Add an optional data-driven quest monster-completion trigger. Matching native
  monster defeats now advance the server-owned quest, update quest-log state,
  and complete the quest at its validated progress limit.

- Activate initial quest rows from external native route levels when an
  authoritative player enters the configured level, using explicit session
  mode to select single-player or multiplayer routing.

- Honor `single_player_only` when activating externally routed quests in
  multiplayer sessions.

- Externalize linked-object quest progress amounts. Object activation now uses
  validated `quest_progress` data and clamps progress at the quest requirement,
  with the rule preserved through snapshots, saves, protocol field 14, and
  compatibility-aware hashes.

## 2026-08-12

- Advance the Diablo content schema to 0.66 with validated quest pools and
  native set-level identities/names, including quest routing metadata for the
  base and Hellfire quest catalogs.

- Add the module-owned deterministic quest-pool selector, including the native
  bad-seed compatibility discard, without reintroducing quest constants or Lua
  state into the server.

- Advance the Diablo content schema to 0.65 with validated native level-range
  generation metadata in `level_generation.tsv`.

- Extend the validated Hellfire `monster_types.tsv` catalog with native IDs 6
  through 11, covering Devil Kin, Dark One, Skeleton, Corpse Axe, Burning Dead,
  and Horror.

- Advance the Diablo content schema to 0.64 with a validated native-compatible
  `quest_types.tsv` catalog for all base and Hellfire quest identities and
  quest-state identity checks.

- Advance the Diablo content schema to 0.63 with a validated native-compatible
  `monster_types.tsv` catalog and encounter identity checks for the base and
  first six Hellfire monster types.

- Advance the Diablo content schema to 0.62 with native-derived Hellfire
  Harlequin Crest, Veil of Steel, and Arkaine's Valor item and unique
  definitions.

- Advance the Diablo content schema to 0.61 with native-derived Hellfire
  Optic Amulet and Ring of Truth item and unique definitions, including their
  base-item identities and authoritative unique powers.

- Advance the Diablo content schema to 0.59 with external mappings for Rune of
  Lightning, Greater Rune of Lightning, Greater Rune of Fire, and Rune of Stone
  to existing authoritative Lightning Wall, Nova, Immolation, and Stone Curse
  behaviors.

- Advance the Diablo content schema to 0.60 with the direct Rune of Fire
  BigExplosion-style authoritative splash, native level-zero damage, and
  radius-nine placement crawl.

- Advance the Diablo content schema to 0.58 with external rune-source identity
  mapping. The base pack maps Rune of Stone miscellaneous ID `51` to stable
  Stone Curse spell ID `8`; legacy rune IDs are preserved and authoritative
  loadout/combat source resolution now consults the active spell catalog.

- Advance the Diablo content schema to 0.57 with externalized Golem
  death-animation timing. The native 12-frame sequence is server-owned and
  persists remaining terminal ticks through snapshots, saves, hashes, and
  protocol fields 34-35 before the expired minion is removed.

- Advance the Diablo content schema to 0.56 with server-authoritative
  Etherealize (stable spell ID 53, native `SpellID` 26), including its
  level-scoped status, transition cleanup, protocol/snapshot/hash persistence,
  and monster-damage immunity.
- Surface the authoritative Etherealize status in the Godot HUD and extend
  client-model regression coverage for its typed protocol projection.
- Add the checked-in Hellfire content pack, which selects the native game
  variant through a validated `combat.tsv` overlay while keeping remaining
  Hellfire world and catalog metadata separate for later migration slices.
- Expand the native-derived Hellfire encounter set and validate monster
  identity, stats, and faction through composed Diablo activation.
- Add the Hellfire Undead Crown as normalized base/unique content with
  deterministic catalog-generation coverage.
- Make the Hellfire Sorcerer starting loadout use its native Firebolt staff
  through a validated keyed class-loadout overlay, with live Godot protocol
  coverage for charged-source validation, charge consumption, and target damage.
- Extend live Godot protocol coverage through the Bard's server-owned Identify
  ability, proving class-service source validation and equipment-state mutation.
- Extend live Godot protocol coverage through the Sorcerer's server-owned Staff
  Recharge ability, including charged-cast consumption and native max-charge loss.
- Extend live Godot protocol coverage through the Warrior's server-owned Repair
  ability, including class authorization and fully-repaired equipment handling.
- Extend live Godot protocol coverage through the Rogue's server-owned Trap
  Disarm ability, including an external armed-object overlay, adjacency
  validation, and server-owned trap-state mutation.
- Extend live Godot protocol coverage through the Monk's server-owned Search
  ability, including explicit class-ability source validation and typed status
  projection with authoritative duration.
- Extend live Godot protocol coverage through the Barbarian's server-owned Rage
  ability, including explicit class-ability source validation and active
  controller projection with server-owned life state.
- Separate stable authoritative spell-ID bounds from the legacy native-ID
  ceiling, allowing external loadouts, saves, and replay state to carry stable
  Etherealize identity `53` (
`native SpellID` `26`).
- Extend live Godot protocol coverage through Etherealize selected by an
  external memorized loadout, including source admission and typed status
  projection.
- Promote the shared ranged Hellfire replay fixture to the composed
  `devilution.base` plus `devilution.hellfire` manifest, so native and C# replay
  execution validate the effective Hellfire overlay rather than only a variant
  label.

- Advance the Diablo content schema to 0.55 with server-authoritative Golem
  (stable spell ID 52, native `SpellID` 21). Targeted summon placement,
  replacement of the caster's prior owned minion, native life/armor/damage
  state, owner identity, snapshot/save/hash/protocol persistence, and focused
  parity coverage are now in the C# module.
- Add deterministic Golem nearest-monster targeting, movement, melee damage,
  experience attribution, and owner/level lifecycle cleanup; native path
  planning now uses deterministic breadth-first routing around blocked cells and
  owner-facing idle wandering.
- Refresh the 22 base-content replay fixtures to the resulting manifest
  identity while preserving all existing replay checkpoints.
- Advance the Diablo content schema to 0.50 with server-authoritative Jester
  (stable spell ID 47, native `SpellID` 39), preserving native 10-way RNG
  dispatch into the existing authoritative spell paths without a second
  client-side rules implementation.
- Advance the Diablo content schema to 0.49 with server-authoritative
  Immolation (stable spell ID 45, native `SpellID` 41), preserving the native
  moving controller, 36-projectile radial burst, level-scaled speed, and
  FireballBow projectile impact path.
- Advance the Diablo content schema to 0.48 with server-authoritative Ring of
  Fire (stable spell ID 44, native `SpellID` 45), preserving native
  radius-three crawl ordering, first-blocker termination, RNG draw order, and
  Fire Wall segment lifecycle through the shared projectile protocol state.
- Refresh the 22 base-content replay fixtures to the resulting manifest
  identity while preserving all existing replay checkpoints.
- Advance the Diablo content schema to 0.47 with server-authoritative Mana
  (stable spell ID 41, native `SpellID` 37), preserving native LCG draws,
  Q6 clamping, effective-level scaling, and class multipliers.
- Refresh the 22 base-content replay fixtures to the manifest identity produced
  by the new Mana definition.
- Advance the Diablo content schema to 0.46 with server-authoritative Magi
  (stable spell ID 40, native `SpellID` 38), restoring current mana to its
  native maximum through the shared immediate-effect cast path.
- Advance the Diablo content schema to 0.45 with server-authoritative Reflect
  charges (spell ID 43, native `SpellID` 43), native level/character scaling,
  seeded 20–30% physical reflection, persisted protocol state, and Godot HUD
  presentation.
- Add same-instance Godot protocol reconnect, preserving the server-issued resume
  token and pending command sequence ledger while safely replacing the transport;
  cover the resumed handshake, snapshot, and retry path with a live transport test.
- Make the Godot presentation observe generation-safe transport closure and
  recover dropped connections with bounded exponential backoff without replacing
  its authoritative model.
- Complete leased-host shutdown ownership: server disposal now releases the
  operation barrier and drains retired adapters, and subsequent operations are
  rejected deterministically.
- Make the Host save contract explicitly expose complete session-state
  migration while retaining the legacy player-save method names; the Diablo
  adapter now documents its versioned snapshot as player-plus-world state.
- Add host-controlled ruleset replacement with transactional candidate
  preparation, save-based session migration, handshake identity publication,
  operation barriers, generation-safe retired-target disposal, and regression
  coverage for failed and concurrent migrations.
- Preserve legacy replay hashes for static catalog portals while hashing owned,
  expiring Town Portal state, and refresh all base-content replay golden vectors
  to the current manifest and authoritative snapshot projections.
- Add generic Engine ruleset generation leases and explicit retired-activation
  draining, keeping replaced targets alive until in-flight operations release
  them while leaving session migration and resource disposal to the host.
- Route the generic server adapter boundary through those leases so commands,
  ticks, snapshots, events, saves, and participant initialization cannot use a
  retired game target mid-operation.
- Expand the Godot cast cursor to enumerate and cycle every authoritative spell
  source—memorized spells, charged staff, consumables, and class ability—while
  preserving explicit server-owned source validation and resource consumption.
- Advance the Diablo content schema to 0.44 with server-authoritative Town
  Portal (spell ID 39, native `SpellID` 7), including native five-cell
  placement, owner replacement, bidirectional dungeon/town use, 100-tick
  expiry, and persisted protocol-visible portal state.
- Advance the Diablo content schema to 0.43 with server-authoritative
  Telekinesis (spell ID 33), covering visible item pickup, object activation and
  effects, one-cell monster knockback, line-of-sight validation, and deterministic
  source/resource handling.
- Advance the Diablo content schema to 0.42 with explicit stable/native spell
  identity mapping. `spells.tsv` now records
`native_spell_id`, rejects duplicate
  native identities, and resolves legacy item spell names to stable content IDs
  before cast-source validation.
- Advance the Diablo content schema to 0.41 with the externally validated
  Guardian turret (spell ID 7), preserving its native spawn search, duration,
  arc scan, Firebolt emission, line-of-sight filtering, RNG ordering, and
  resumable controller state.
- Advance the Diablo content schema to 0.40 with the externally validated
  Elemental projectile (spell ID 31), preserving its native first-destination
  retarget, persistent nine-cell fire burst, deterministic damage scaling, and
  resumable projectile state.
- Advance the Diablo content schema to 0.39 with the externally validated
  Hellfire Rage controller (spell ID 22), preserving its native active and
  cooldown phases, character-level duration scaling, minimum-life self-damage,
  and deterministic persisted controller state.
- Advance the Diablo content schema to 0.38 with the externally validated
  Apocalypse row-major controller and persistent Physical boom behaviors (spell
  ID 25), preserving native damage rolls, blocked-cell/minion filtering,
  Hellfire line-of-sight, and deterministic cursor/boom state.
- Advance the Diablo content schema to 0.35 with the externally validated
  Inferno controller and stationary segment behaviors (spell ID 21), preserving
  native speed, three-segment emission, stepwise lifetime, fire damage rolls,
  repeated collision, and deterministic projectile state.
- Advance the Diablo content schema to 0.36 with the externally validated
  Chain Lightning fan-out controller and Lightning-compatible bolt behavior
  (spell ID 16), preserving native crawl targeting, damage, segment lifetime,
  resistance, collision, and deterministic source-level state.
- Advance the Diablo content schema to 0.37 with the externally validated Nova
  radial controller and continuing Lightning-ball behavior (spell ID 18),
  preserving native mirrored fan-out order, damage scaling, resistance,
  lifetime, collision, and deterministic projectile state.
- Advance the Diablo content schema to 0.34 with the externally validated
  Flame Wave controller and moving segment behaviors (spell ID 15), preserving
  native perpendicular placement, facing-direction travel, random fire damage,
  collision, lifetime, and deterministic projectile state.
- Advance the Diablo content schema to 0.33 with the externally validated
  Fire Wall controller and persistent segment behaviors (spell ID 6), preserving
  native perpendicular growth, branch stopping, repeated shifted fire damage,
  resistance, duration, and deterministic persisted wall state.
- Advance the Diablo content schema to 0.32 with the externally validated
  Bone Spirit spell (ID 38), preserving its native homing, current-life-third
  magic damage, resistance, impact lifetime, and physical self-damage rules.
- Advance the Diablo content schema to 0.31 with the externally validated
  Hellfire Blood Star spell (ID 36). Preserve its native fixed magic damage,
  quarter-resistance projectile behavior, 16-tile speed, 256-tick lifetime, and
  five-point physical self-damage through the authoritative C# module.
- Advance the Diablo content schema to 0.30 and add server-owned Holy Bolt and
  Rogue Trap Disarm capabilities.
  Externalize the undead monster capability, preserve native damage and speed
  scaling, pass non-undead monsters through, and carry the capability through
  protocol field 23, compatible saves, and conditional snapshot hashes.
- Refresh the 22 base-content replay fixtures to the current manifest identity
  while preserving their native-compatible snapshot checkpoints, and keep the
  projectile behavior values stable as Holy Bolt is appended to the catalog.
- Externalize the Rogue's Trap Disarm capability identity and armed-object
  state, then route an additive protocol command through the authoritative
  Diablo world module with snapshot, save, and hash coverage.
- Give the shipped base content an explicit versioned manifest and make the
  standalone host prefer it while retaining legacy loading for unmanifested
  external roots.
- Consume authoritative armed-trap state in the Godot presentation client,
  including typed Rogue Trap Disarm command routing and server-only visual
  state changes.
- Move player rejuvenation into the native debug-command registry while
  preserving `dev.player.stats.rejuvenate` as a Lua compatibility alias.
- Move player health and mana adjustment into native debug commands while
  preserving the Lua stat helpers as compatibility aliases.
- Move player attribute max/min helpers into native debug commands while
  preserving their Lua names as compatibility aliases.
- Move player level-up into a native debug command while preserving the Lua
  helper and its high-priority experience command sequence as a compatibility
  alias.
- Move player gold give/take into native debug commands while preserving exact
  inventory-stack behavior and Lua compatibility aliases.
- Move the player spell-level helper into a native debug command while
  preserving learned-spell filtering and the Lua compatibility alias.
- Move automap reveal and hide helpers into native debug commands while
  preserving the Lua compatibility aliases.
- Move level reset into the native debug command registry while preserving
  optional seed selection and the Lua compatibility alias.
- Move dungeon-level warp into the native debug command registry while
  preserving legacy transition modes and the Lua compatibility alias.
- Move quest-level warp into the native debug command registry while
  preserving set-level selection and the Lua compatibility alias.
- Move custom-map warp into the native debug command registry while
  preserving path, dungeon-type, spawn-bound, and transition validation.
- Move selected-item diagnostics into the native debug command registry while
  preserving Lua compatibility and network-pack validation.
- Move towner talk and visit helpers into native debug commands while
  preserving their town checks and Lua compatibility aliases.
- Move level `.dun` export into the native debug command registry while
  preserving the legacy binary layout and Lua compatibility alias.
- Move item and monster spawning helpers into native debug commands while
  preserving their generation, placement, count, and Lua compatibility behavior.
- Move quest activation and diagnostics into native debug commands while
  preserving the `dev.quests` Lua compatibility aliases.
- Move player TRN selection into the native debug-command registry and relabel
  the overlay as a general debug console. Preserve the Lua `dev.player.trn`
  functions as compatibility aliases.
- Move player arrow-effect selection and player inspection into the native
  debug-command registry. Preserve the existing Lua module names as aliases
  while routing the debug console through native handlers.
- Move display tile-data inspection and scroll-view diagnostics into the native
  debug-command registry. Preserve the existing Lua module names as aliases
  while routing the debug console through native handlers.
- Move level-seed inspection and automap search diagnostics into the native
  debug-command registry. Preserve the existing Lua module names as aliases
  while routing the debug console through native handlers.
- Add a deterministic native debug-command registry for core player and display
  toggles. Route those commands through the debug console while retaining the
  Lua modules as a compatibility frontend for the remaining developer REPL.
- Move native sound and table-data refreshes into `ModManager` after active
  mod scripts run. Lua remains a compatibility hook rather than the owner of
  archive, sound, or data reload orchestration.
- Move Hellfire activation from the Lua `init.lua` bootstrap into the native
  declarative mod manager. Preserve archive priorities, game-mode selection,
  and data reload behavior while allowing content-only mods to omit Lua.
- Advance the Diablo content schema to 0.28 and make the Sorcerer's declared
  Staff Recharge skill a server-owned item action. Externalize native staff
  levels, preserve the single recharge-strength RNG roll, permanent
  maximum-charge loss, full-item no-op and exhaustion behavior, retain
  source-zero paid vendor recharge, and route both clients without local
  gameplay mutation.
- Advance the Diablo content schema to 0.27 and make the Warrior's declared
  Repair skill a server-owned item action. Preserve native level-scaled random
  repair strength, permanent maximum-durability loss, no-op and item-exhaustion
  behavior, and RNG sequencing; reuse the additive item-service source while
  retaining source-zero paid vendor repair; and route both clients without
  local gameplay mutation.
- Advance the Diablo content schema to 0.26 and make the Bard's declared
  Identify skill a server-owned item action. Add an explicit additive service
  source to the protocol, validate the skill against authoritative loadout
  state, preserve zero-cost native identification, retain source-zero vendor
  compatibility, and route Godot and native inventory selection without local
  gameplay mutation.
- Advance the Diablo content schema to 0.25 and make the Monk's declared Search
  ability executable by the authoritative module. Externalize its native
  character- and spell-level duration terms, preserve additive recasts and the
  signed-overflow guard, server-owned expiry and transition cleanup, and carry
  typed behavior 2 through compatible saves, protocol snapshots, Godot item
  outlines, native automap highlighting, and a four-checkpoint C++/C# replay.
- Advance the Diablo content schema to 0.24 and add server-owned Heal Other.
  Externalize its distinct class multiplier, preserve native other-player
  admission, mana, randomized Q6 healing, target cap, full-life consumption,
  and Monk 300-percent rule, and reuse existing protocol participant state.
  Add Godot/native target mapping and a timed three-checkpoint shared C++/C#
  multiplayer replay.
- Advance the Diablo content schema to 0.23 and add server-owned Fireball.
  Preserve native two-roll and per-level damage scaling, capped speed growth,
  swept direct collision, ordered geometry-gated 3x3 splash, Q6 resistance and
  object rules, and the 14-tick impact. Carry complete runtime state through
  protocol, compatible saves, conditional hashes, native and Godot
  presentation, and a four-checkpoint shared C++/C# replay.
- Advance the Diablo content schema to 0.22 and add server-owned Phasing.
  Preserve native offset enumeration, participant and world occupancy, one-draw
  destination selection, exact learned/consumable source handling, unchanged
  facing, and the consuming no-candidate outcome. Carry its invisible two-tick
  controller through protocol, compatible saves, conditional hashes, native
  and Godot projection, and a four-checkpoint shared C++/C# replay.
- Advance the Diablo content schema to 0.21 and add server-owned Infravision.
  Preserve native class-adjusted learned mana, exact scroll/rune consumption,
  saturating per-level duration growth, nonshortening recasts, authoritative
  expiry, and transition cleanup. Carry typed status behavior through protocol
  field 4, saves, conditional hashes, native and Godot presentation, and a
  four-checkpoint shared C++/C# replay.
- Advance the Diablo content schema to 0.20 and add server-owned Stone Curse.
  Preserve native crawl targeting, future walking-cell selection, content-owned
  boss immunity, no-target fizzle, consuming non-refreshing recast, level-scaled
  duration, movement release, and dead-target shatter. Carry controller and
  immunity state through protocol field 22, saves, conditional hashes, native
  and Godot presentation, and a six-checkpoint shared C++/C# replay.
- Advance the Diablo content schema to 0.19 and add server-owned Mana Shield.
  Preserve the native active-cast fizzle, learned-level reduction cap, exact Q6
  mana absorption, reverse calculation when mana is insufficient, exact-
  depletion cleanup, NoMana-equipment bypass, and transition cleanup. Route all
  authoritative player-damage paths through one module resolver; carry active
  state through protocol field 29, saves, conditional hashes, both clients, and
  a six-checkpoint shared C++/C# replay.
- Add a coordinated generic ruleset runtime that composes candidate content,
  atomically activates the complete module set, computes identity, and validates
  a game-specific target before one publication. Preserve the prior activation
  with stage-specific diagnostics on composition, compatibility, registration,
  identity, or target failure; retain the compatibility `Load` API; and move
  standalone server startup onto the all-or-nothing path.
- Add inclusive semantic version requirements to gameplay-module dependencies.
  Reject non-semantic and out-of-range loaded dependency versions during Engine
  preflight, preserve requirements through verified external-package wrapping,
  include declared ranges in deterministic ruleset identity while retaining
  unconstrained legacy hashes, and cover compatibility diagnostics and package
  propagation in Engine and Server tests.
- Advance the Diablo content schema to 0.18 and make PvP ears server-owned.
  Persist native-valid character names from new-session handshake through
  saves, reconnect, protocol field 28, conditional hashes, and both clients;
  externalize the stable ear item and per-class cursor; carry victim-authored
  runtime item names/cursor in additive generic item fields; preserve PvP
  equipment and special-level suppression; consume no extra RNG; expose names
  through the Godot launcher; and verify the complete death/drop timeline in a
  shared C++/C# replay.
- Add server-owned native player facing and ordinary monster-or-trap equipped
  item death drops. Accepted movement, attacks, and resolved casts update the
  eight-way direction; death rotates left through Head-to-Chest slot order,
  tries each preferred adjacent tile before native crawl fallback, preserves
  item seed and complete state, and consumes no RNG. Retain equipment for PvP
  and special-level multiplayer suppression, carry facing through protocol
  field 27, saves, hashes, native and Godot projection, and verify the result in
  a two-checkpoint C++/C# replay.
- Advance the Diablo content schema to 0.17 and preserve native multiplayer
  death-drop suppression for level 16 and arena-equivalent maps. Make game mode
  explicit host configuration instead of participant-count inference, expose it
  additively in `ServerHello`, synchronize both presentation clients, validate
  level suppression in external content, preserve unconditional cursor-held
  item drops, and verify unchanged Gold and RNG state in shared C++/C# replay.
- Preserve native cursor-held Gold overflow as server-owned state. World-item
  pickup now fills canonical inventory piles in native order, places any
  remainder on the authoritative cursor without consuming an item seed, and
  supports an explicit nearby drop command that preserves the original world
  item identity. Carry held items through snapshots, saves, conditional hashes,
  native and Godot presentation, combat death, and a shared two-checkpoint
  C++/C# replay.
- Advance the Diablo content schema to 0.16 and make ordinary combat-death gold
  consequences server-owned. Externalize the divisor, projected maximum stack,
  native Gold identity, and drop radius; cancel movement before native crawl
  placement; consume the authoritative RNG for item seeds; validate and persist
  canonical Gold world items; add native non-full-first inventory removal,
  whole-stack seed reuse, partial-stack RNG, grid compaction, odd physical-pile
  retention, and topology-aware pickup; retain an aggregate compatibility path;
  and verify wallet, inventory, world-item, and RNG state in shared C++/C# replay
  coverage.
- Materialize content-backed starting Gold as stable session-seeded native
  inventory piles beginning at cell 30. Preserve those piles through native
  nonmaximum-first store spending, purchase placement, sale top-off/new-pile
  creation, post-death wallet divergence, and transactional capacity rejection;
  keep Gold inventory-only across direct transfers and swaps; and add a
  five-checkpoint shared C++/C# purchase/sale/refill replay.
- Advance the Diablo content schema to 0.15 and make restart-in-town a
  server-owned terminal-death command. Externalize the destination level and
  tile, native Q6 life reset, and one-tick transition duration; preserve zero
  mana, inventory, and the cleared store state; add protocol action kind 6 and
  command field 27; route Godot Escape and both native dead-player controls
  through the server; and verify dungeon death through town recovery in a
  shared six-checkpoint C++/C# replay.
- Advance the Diablo content schema to 0.14 and add server-owned Resurrection.
  Preserve native other-player targeting, fixed minimum mana, living-target beam
  behavior, dead-player resource reset, ten-life revival, initial placement and
  radius-50 crawl order, fallback portal avoidance, and the timed beam
  lifecycle. Carry complete participant and beam state through snapshots,
  saves, protocol projection, both clients, and a five-checkpoint shared C++/C#
  replay.
- Project every assigned participant in atomic server snapshots, keeping the
  receiving player first and sorting remote players by entity ID. Validate and
  render the full set in Godot, harden native identity projection, and extend
  canonical C++/C# replay hashes and multiplayer fixtures to include both
  players while remaining receiver-order independent.
- Advance the Diablo content schema to 0.13 and add exact server-owned
  Teleport. Preserve the native radius-0-through-5 `DoCrawl` order, participant,
  monster, object, and static-geometry occupancy, no-cost fizzle, source
  consumption on success, and delayed first-update movement through an
  invisible two-tick controller. Carry the controller through snapshots, the
  existing protocol/save fields, native and Godot presentation, and a shared
  four-checkpoint C++/C# replay.
- Advance the Diablo content schema to 0.12 and add exact server-owned Healing.
  Externalize the native character-class multiplier and all character/spell-level
  random rolls, preserve Q6 life caps plus full-life mana/RNG consumption, add an
  exact additive healing event field with legacy native-reader behavior, and
  verify uncapped, capped, and full-life casts in one shared C++/C# replay.
- Advance the Diablo content schema to 0.11 and add server-owned Flash parity.
  Two independently rolled 19-tick layers reproduce the native 3x3 offset split,
  repeated magic-hit and exact-Q6 resistance damage, blocker and breakable-object
  behavior, PvP admission, and the delayed invulnerability window. Preserve the
  runtime through snapshots, saves, protocol projection, hashes, and a shared
  four-checkpoint C++/C# replay with strict magic-resistance validation.
- Advance the Diablo content schema to 0.10 and add server-owned Lightning
  parity. A speed-32 invisible controller now consumes the native per-tick RNG
  draws, leaves persistent level-scaled damaging segments, stops without
  breaking missile-blocking objects, and preserves magic-hit, resistance, Q6,
  PvP, snapshot, save, hash, protocol, and replay behavior. Explicitly sequence
  the retained C++ RNG operands to preserve the observed Windows result across
  compilers, and add a four-checkpoint shared C++/C# resolution timeline.
- Advance the Diablo content schema to 0.9 and replace spell 1's provisional
  targeted projectile with server-owned Firebolt parity: external native mana
  and class/variant adjustments, Q6 consumption, seeded Magic/spell-level
  damage, speed-26 16.16 swept movement, magic hit chance, quarter resistance,
  impact expiry, persistence, and a three-checkpoint C++/C# replay.
- Add server-owned player death and portal-transition lifecycles. Lethal monster
  and Charged Bolt damage cancel the current action, enter Death for the
  content-defined animation, and retain the terminal mode after action cleanup;
  portal use applies its destination behind a one-tick NewLevel barrier. Add
  protocol action kinds 4-5, save/hash/native/Godot validation, and shared
  C++/C# death and transition timelines, including autonomous monster movement
  and attack replay data.
- Add an authoritative player-action lifecycle that reserves movement
  destinations, defers melee/ranged damage and spell resource/effect resolution
  to native class/equipment animation frames, enforces native chaining
  boundaries, and preserves base-game versus Hellfire bow-speed behavior.
  Preserve in-progress actions through module snapshots, protocol field 25,
  version-1 saves, conditional canonical hashes, native/Godot projection, and a
  shared C++/C# action-state hash golden. Add a shared command-timeline replay
  for deferred movement, melee damage, attack-to-cast chaining, spell resource
  consumption, effects, and cleanup, then enable the lifecycle in composed
  server sessions.
- Add shared Diablo and Hellfire fast-Bow command timelines whose initial
  equipment is hashed with the player state. Both runners derive ranged target
  admission, combat mode, and variant-specific frame skipping from that
  equipment, and the managed gate verifies the fixtures against the activated
  base manifest and Rogue animation row.
- Advance the Diablo content schema to 0.8 and extract the complete native
  six-class animation matrix into validated `class_animations.tsv`; remove
  duplicated block/recovery timing from `classes.tsv`, cross-check every class
  row, and wire the catalog into authoritative simulation and collision modes as
  the basis for command-driven movement, attack, cast, death, and transition
  lifecycles.
- Advance the Diablo content schema to 0.7; externalize class block/recovery
  frames and game variant, make native player combat modes authoritative across
  protocol, saves, hashes, and clients, enforce Stand/Attack-only blocking with
  immediate block/hit transitions, reproduce Hellfire's post-block RNG draw,
  and add a shared C++/C# Hellfire block replay.
- Advance the Diablo content schema to 0.6 and add server-owned Charged Bolt PvP parity for global/friendly admission, retry-safe hostile mode, native hit/block/damage RNG order, class/equipment blocking, lightning resistance, exact Q6 victim events, protocol/save/hash/native/Godot projection, and a shared two-participant C++/C# replay.
- Advance the Diablo content schema to 0.5 and add native-style Charged Bolt collision parity for separate missile geometry, swept intermediate tiles, stationary-occupant suppression, and external object pass-through, breakability, and destruction; preserve the new state through protocol field 28, saves, conditional hashes, native projection, and a shared C++/C# object-collision replay.
- Preserve native current/future monster occupancy and petrification across authoritative ticks, protocol fields 18-21, version-1 saves, conditional hashes, native signed occupancy, and a shared C++/C# Charged Bolt replay; validate adjacent movement, ignore normal future occupancy, guarantee petrified hits after the native RNG draw, and cancel movement on death.

## 2026-08-10

- Adopt Godevilution as the project name and replace the inherited root README with the current migration goal, repository map, verification commands, origin, and license notice.
- Consolidate current architecture, migration status, protocol, server, Godot, native-client, replay, Lua, source-layout, content, and build guidance; archive completed project-specific plans.
- Remove inherited DevilutionX policy, release, packaging, and platform manuals from the active documentation tree while preserving `LICENSE.md` unchanged.
- Correct protocol-v1 identity guidance and make the native launcher send the combined ruleset hash in the compatibility content field.
- Make the Godot harness build a missing authoritative server, and make live protocol tests build the current server once as an explicit dependency before launching it without rebuild races.
- Introduce the dependency-free .NET 8 `Devilution.Domain` kernel and its independent xUnit project, then move deterministic inventory-grid geometry and mutation rules out of `StoreSimulationExecutor`.
- Introduce the .NET 8 `Devilution.Data` runtime depending only on Domain; extract canonical hashing, TSV parsing, ordered content packs, manifest identity, and stable symbolic IDs into independently tested assemblies.
- Introduce the .NET 8 `Devilution.Diablo` module with no server/protocol dependency; move item, store, combat, monster, spell, and world catalogs, authoritative player state, store/inventory intents and transitions, and transport-independent rules into it with shipped-content coverage.
- Move authoritative world collections, navigation, occupancy, movement, portals, world-item pickup, object effects, quest transitions, and transport-neutral gameplay events into `Devilution.Diablo`; reduce the server to Protobuf/result/event adapters for those commands.
- Move attack and cast intents, typed damage, experience and drop resolution, projectiles, status expiry, monster targeting, movement, attacks, and directed tick events into `Devilution.Diablo`; leave only stable tick scheduling and session event routing in the server host.
- Introduce the .NET 8 `Devilution.Engine` composition layer with dependency-ordered modules, transactional typed capabilities, authoritative clock context, and ruleset identity; move the first-party Diablo module and aggregate simulation factory behind that boundary while retaining the legacy host store-rule adapter.
- Move deterministic session/state coordination and transient event queues into Engine; move player/world/store snapshot capture and restore validation into Diablo; isolate Protobuf mapping in Server while preserving snapshot hashes, version-1 saves, and replay behavior.
- Add a generic Engine simulation runtime for serialized participant access, exactly-once tick advancement, and directed events; provide Diablo player templates/lifecycle hooks; extract explicit version-1 save encoding from the protocol executor.
- Allow content and live-server tests to locate an explicitly configured Godevilution repository root and server artifact path when builds use isolated output directories.
- Add manifest-declared, dependency-ordered content packs with deterministic composite-key table overlays, exact schema validation, transactional activation, and live-server protocol coverage.
- Add semantic Engine API compatibility ranges and deterministic module preflight diagnostics for duplicate IDs, missing or cyclic dependencies, and incompatible candidates before registration.
- Add optional content schema identities, semantic dependency-version ranges, manifest-format validation, and compatibility hashing while preserving legacy native golden hashes.
- Add explicit Server-owned gameplay-module package loading with SHA-256 verification, named-type activation, single-assembly dependency enforcement, verified ruleset identities, and process-level startup coverage.
- Move Diablo schema validation, normalized/legacy table selection, catalog construction, default-level selection, store-rule activation, and simulation creation out of Server startup and behind an `IDiabloContentBootstrap` module capability.
- Add a generic authoritative game-host adapter/factory catalog combining command, tick, snapshot, event, and save services; move built-in Diablo capability and executor construction behind it so executable `Program` contains no Diablo symbols.
- Generate managed Protobuf types once in `Devilution.Protocol`; extract generic sessions, scheduling, module loading, snapshots, and persistence into `Devilution.Server.Host`; extract Diablo mapping, compatible saves, replay support, and the protocol executor into `Devilution.Server.Diablo`; reduce the executable to `Program.cs` and enforce the assembly graph in tests.
- Externalize all six native-compatible player classes, including Hellfire metadata, flags, attributes, combat values, and the content-selected default; carry class identity through authoritative state, protocol field 19, saves, overlays, native projection, and matching C++/C# state hashes.
- Add optional per-session class selection to the shared handshake; create independent class-derived players with native Q6 starting resources, preserve save/reconnect precedence, and send the selection from both Godot and native clients.
- Gate session persistence on completed participant initialization so fixed ticks cannot create fallback state while a requested class is being installed.
- Externalize native-compatible starting wallets, equipment, belt and inventory placements, class skills, learned spells, item indexes, and charge state; deterministically seed each new session's starting items and validate every class/loadout/item relationship at content activation.
- Carry class skills and learned spell levels through module state, additive protocol fields 20-21, version-1 saves, Godot/native projection, conditional canonical hashes, and matching C++/C# golden coverage.
- Restore cold saves through their saved-class template and register the participant only after successful validation, preserving non-default resources and loadouts without leaving partial session state after a rejected import.
- Make cast admission source-aware across the protocol, Diablo module, native adapter, and Godot client; verify owned abilities and learned spells, exact charged or consumable items, and consume mana, charges, or items only after complete cast validation.
- Advance the Diablo content schema to 0.3, cross-validate every directly castable starting source during activation, and externalize the Sorcerer staff's spell 30 as a four-projectile authoritative vertical slice with module and live-server coverage.
- Advance the Diablo content schema to 0.4 and replace spell 30's provisional targeted projectiles with server-owned Charged Bolt count scaling, native Diablo-LCG draw order, Magic damage, 16.16 wandering movement, hit/resistance handling, persisted RNG/projectile runtime, and a shared C++/C# replay checkpoint.
- Preserve exact native Q6 monster health and Q6-before-resistance Charged Bolt damage across module state, additive protocol fields, events, version-1 saves, native projection, and conditional hashes; add a shared C++/C# checkpoint-only resolution timeline through hit RNG and impact state.
- Activate the `authoritative-client` vcpkg manifest feature automatically when the server-backed native client is enabled, ensuring the authoritative build resolves Protobuf.

## 2026-07-31

- Complete the first Godot client vertical slice with a local server harness, live handshake/movement/cast smoke coverage, captured-session replay, retry/reconnect tests, SVG-backed world presentation, authoritative inventory/store/event panels, and RTT-aware movement prediction/correction.
- Add standalone server starting-life and starting-mana options so local authoritative gameplay can exercise validated spell commands.
- Add a CI authoritative replay-parity gate that runs the native C++ and C# fixture runners against the same state-hash checkpoints, including the previously uncopied transaction and movement fixtures.
- Add shipped-content smoke coverage for the base monster, spell, item, store, and generated-item catalogs, plus a shared base-store purchase/sale replay fixture with native and C# hash parity.
- Add Godot asset-backed level layouts and authoritative HUD controls for store services, inventory movement, mana refill, and event-log management.
- Make the Godot harness discover versioned WinGet Mono installations from PATH and document the explicit headless verification workaround.
- Complete the Godot protocol-boundary integration with selectable store stock, footprint-aware inventory moves, selected-item services, object/quest dialog interactions, command feedback, and tested C++-compatible headless server launches.
- Add adjacent Godot client hardening: F3 diagnostics, F4 high-contrast HUD text, optional event audio hooks, cast target previews, and configurable headless run duration.
- Close the shipped Phase 3/4 parity gate: normalized store item definitions now take precedence over legacy compatibility tables, live server stock verifies weapon/armor projections, shipped generation/pricing tables are covered, and native replay/item checks remain aligned with C#.
- Complete the remote store transition for the shipped vendor set: external Smith, Witch, Wirt, and Healer stock is projected into native text and visual store UIs, purchases and Smith/Witch sales use authoritative commands, and live C# coverage exercises every vendor open/purchase path.


- Promote server-backed gameplay to an explicit legacy/authoritative runtime
  mode, add local server discovery and launch tooling, persist reconnect resume
  tokens, and prevent authoritative sessions from falling back to local
  simulation after disconnect.
- Add detailed handshake identity-mismatch diagnostic dumps and an
  authoritative native/protocol CI smoke gate.

## 2026-07-30

- Restore saved authoritative monsters and world items with entity-identity and walkability validation.
- Add deterministic server-owned monster movement and attack ticks, including correctly sourced damage events and external attack/aggro tuning.
- Add a validated external item catalog and allow store stock to reference stable item definitions.
- Add external portal definitions, deterministic level/tag-filtered item affixes, and catalog-backed monster drops.
- Route opt-in native world-item pickup through authoritative projected entity IDs.
- Add authoritative object activation and quest progress snapshots, commands, persistence, and native routing.
- Add a shared C++/C# replay fixture for object activation and quest completion with matching state hashes.
- Add typed elemental/magic damage, area-targeted spells, unique-item catalog generation, and expanded monster combat tuning.
- Load the shipped legacy item, prefix, suffix, and unique TSV table shapes into stable authoritative catalogs, with deterministic aliases and expanded native power mappings.
- Harden authoritative startup and save import against blocked placements, cross-category entity collisions, duplicate item identities, invalid equipment/belt slots, and malformed multi-cell inventory footprints.
- Match native item armor rolls with the legacy Diablo LCG, advance autonomous monsters for every skipped authoritative tick, and reject damage spells through blocked line-of-sight cells.
- Add autonomous snapshot polling and suppress legacy gameplay processors while the native client is connected to the authoritative server.
- Reject overlapping live player, monster, world-item, and object placements during startup and save restoration.
- Route autonomous monster hit, critical, and armor resolution through the external authoritative combat rules.
- Match legacy unique-item power rolls with the native LCG for legacy tables and recalculate legacy affix pricing from value modifiers.
- Carry external object-to-quest links through authoritative snapshots and advance linked quest progress during object activation.
- Add a validated native world projection cache for authoritative monsters, world items, and objects.
- Verify deterministic multi-actor autonomous movement across skipped authoritative ticks.
- Share the native LCG stream across legacy base armor, monster-drop quality, and affix generation, including forced-good drops.
- Match native legacy modifier semantics for fixed-point life/mana, hit-recovery signs, resistance clamping, elemental flags/damage, and indestructible durability; restore object quest links during save import.
- Resolve server-backed monster targets by authoritative cell rather than sorted native cursor index, preventing attacks and spells from addressing the wrong entity.
- Add shared multi-level occupancy replay coverage and save restoration checks for entities that remain on their original level after portal transitions.
- Synchronize compatible authoritative world items, monsters, and objects into legacy presentation pools, including safe creation and removal of server-owned entries.
- Complete the Phase 4 Godot-start boundary with authoritative delayed projectiles, declarative object heal/damage/experience effects, healing events, native projectile projection, and a frozen protocol contract covering handshake, framing, retries, snapshots, events, and state hashes.
- Start the Godot 4 C# client with shared Protobuf transport, adaptive command retries, snapshot/event reconciliation, procedural entity presentation, input mapping, and connection/player HUD.
- Fix CMake 4.x deferred dependency resolution for configuration keywords and link the MSVC Protobuf import target instead of a DLL path.

## 2026-07-29

- Extend the authoritative gameplay boundary with catalog-driven monster snapshots, defeat drops, world-item pickup, damage spells, and native client projections for monsters and world items.
- Add stable content-ID catalogs and ruleset identity negotiation to the Protobuf handshake
- Load authoritative levels, blocked cells, spells, combat constants, and complete protocol item modifiers from external TSV content
- Add validated Protobuf-snapshot saves with atomic server-owned filesystem persistence and session restoration
- Add native movement, attack, cast, and portal command entry points plus shared C++/C# spell replay coverage


- Extend the authoritative gameplay boundary with catalog-driven monster snapshots, defeat drops, world-item pickup, damage spells, and native client projections for monsters and world items.

## 2026-07-28

- Add external `store_services.tsv` pricing data, including spell staff-cost overrides and legacy-compatible service formulas
- Add explicit authoritative inventory-to-belt/equipment transfer commands while retaining the legacy inventory-grid command
- Add the server-backed Adria mana-refill menu action and service-only store scope
- Make C# replay execution validate SHA-256 state checkpoints after every transaction command; extend native fixture coverage
- Execute the shared transaction replay natively, add shape-aware multi-cell inventory placement and swaps, and align C++/C# hashes for resources, belts, item footprints, and protocol item state
- Add the first authoritative gameplay boundary: bounded movement, life/mana maxima, character level, healing, adjacent combat damage, defeat experience, event-batch transport, native player projection, and generic vendor routing
- Add authoritative blocked-cell movement, level-aware portal transitions, status-effect snapshots/expiry, native event application, and replay-hash parity for level/status state
- Add a shared movement/combat replay fixture executed by both C# and native replay runners
- Configure the C# test project to build a self-contained Microsoft Testing Platform runner; direct execution avoids a missing machine-wide VSTest `testhost` package

## 2026-07-26

- Add authoritative belt snapshots and stable inventory, belt, and equipment
  references for server-backed service commands
- Add C# Adria mana-refill validation and deterministic refill pricing
- Match server-backed sale, repair, recharge, and identification rules more
  closely to legacy vendor pricing semantics
- Route the opt-in Smith visual store through acknowledged authoritative
  purchase, sale, repair, and refresh operations
- Add shared C++/C# transaction-parity replay coverage for purchase, sale, and
  mana refill

## 2026-07-22

- Add authoritative wallet, active-store, and inventory snapshots after
  handshake and command batches
- Add deterministic SHA-256 hashes for the authoritative snapshot projection
- Add baseline experience, life, and mana fields to the store player projection
- Add primary attributes, equipment slots, and inventory-grid snapshot fields
- Add core authoritative item-state fields to store inventory snapshots
- Add C# replay-fixture execution, authoritative clock abstraction, session
  entity-ID coverage, empty-batch validation, and command-delivery vector tests
- Reconcile the C# migration plan with implemented phase status, ownership,
  remaining exit gates, and the current critical path
- Add external content and versioned C# gameplay modules as first-class
  migration workstreams with per-slice ownership and verification gates
- Add external TSV store loading, module-owned store transactions, deterministic
  replay checkpoint parity, fixed-point/RNG/ID primitives, and gameplay ruleset
  identity coverage
- Add an executable JSON replay fixture baseline for player/store projection
- Rename experimental C++ client-facing authoritative types and files to server-backed/vendor terminology with deprecated compatibility aliases

- Add the initial transport-independent Protobuf protocol contract and compatibility rules
- Add adaptive client command acknowledgement retries with duplicate and late-command outcomes
- Add a shared retry/deduplication protocol test vector consumed by the C# server tests
- Add the initial C# authoritative command server with Protobuf bindings and deduplication tests
- Add bounded TCP Protobuf sessions with handshake validation and command routing
- Add the first C# authoritative store executor with retry-safe purchases and shared stock
- Add opt-in C++ Protobuf generation, bounded envelope framing, and a minimal
  server-backed client covering handshake, command acknowledgement, and snapshots
- Wire the C++ command-delivery tracker into queued authoritative sends,
  adaptive resubmissions, and acknowledgement resolution

- Fix native authoritative builds by generating Protobuf sources in the consuming
  CMake directory and exporting generated message data to C++ DLL tests
- Fix C++ authoritative connection includes and invalid-frame boundary coverage
- Add authoritative active-store stock snapshots and include vendor items in
  deterministic snapshot hashes and reconnect-resynchronization tests
- Add matching C++ and C# canonical content-manifest hashes with shared golden
  vectors and malformed-row validation
- Extend C++ and C# replay fixtures with structured content identity, retained
  checkpoints, store command payloads, and optional final snapshot validation
- Fix the native replay fixture test to avoid character-level validation before
  player experience tables are initialized
- Preserve native queued and in-flight authoritative commands, adaptive retry
  state, and the server resume token across reconnects
- Add opt-in server endpoint parsing, stable store-command construction,
  validated native vendor-stock projection, and reconnect-safe store UI state
- Add standalone C# server host startup with external store content, live ticks,
  native player/item projections, and server-backed inventory session commands
- Add a destination-explicit legacy store UI adapter that preserves stable
  authoritative vendor slots while applying validated snapshots
- Wire the opt-in server-backed runtime into game lifecycle and Smith-store
  opening, with content identity validation and local-mode preservation
- Apply authoritative player snapshots to native state, poll adaptive command
  retries, and route acknowledged Smith/inventory store transactions through
  the server-backed runtime without mutating locally before acknowledgement

## 2026-07-21

- Fix quest-pool initialization, item-generation reload state and legacy drop probabilities
- Restore test includes and test access to the refactored store and automap managers
- Extract active-mod and game-data reload ownership from Lua behind C++ managers and a typed event bus
- Add deterministic replay primitives for canonical state hashing and command ordering
- Add initial canonical player and store state projections for replay comparison

## 2026-07-20

- Remove all Packaging/ and Android-specific build artifacts
- Prune Android/UWP conditionals and references from CMake build system
- Consolidate Level identity fields into `LevelId`, remove 5 duplicate fields from Level
- Make `World::switchLevel` accept a `LevelId` instead of bare `LevelIndex`, centralizing identity setup
- Simplify all level-transition callers to construct `LevelId` in one call
- Externalize monster AI dispatch: replace `AiProc[]` array with `std::unordered_map` registry populated at startup
- Externalize AI-to-missile-type mapping: replace `GetMissileType()` switch with data lookup from `monster_missiles.tsv`
- Add `behavior` column to `objdat.tsv`, populate for all 109 object types
- Replace `OperateObject()` hardcoded `switch` with registry lookup via `ObjectBehaviorRegistry`
- Externalize item generation parameters: quality curve, affix allocation, drop tables into `item_generation.tsv`
- Create `ItemGenerationConfig` struct loaded from `item_generation.tsv` replacing hardcoded constants in `GetItemBLevel()`, `GetItemPowerPrefixAndSuffix()`, `RndItemForMonsterLevel()`, `RndAllItems()`, and `SpawnItem()`
- Externalize level generation parameters: monster density divisor, MP multiplier, and trap frequency into `level_generation.tsv`
- Create `LevelGenerationData` struct with level-range lookups for monster density, MP multiplier, and trap percent
- Use new data-driven lookups in `AddObjTraps()` and `InitMonsters()`
- Externalize quest pools and set level names into `quest_pools.tsv` and `set_level_names.tsv`
- Add `Object::data()` accessor returning `AllObjects[_otype]`
- Replace `QuestLevelNames[]` array with data-driven `SetLevelNames` lookup
- Add `onExpiryMissile` column to `misdat.tsv` replacing the switch in `ProcessGenericProjectile`
- Add `shortName` column to `towners.tsv`, replacing hardcoded `TownerShortNames` map
- Add TSV entries for TOWN_FARMER, TOWN_GIRL, TOWN_COWFARM (Hellfire NPCs)

## 2026-07-02

- Refactor `ui/automap` into `AutomapManager` class with singleton `CurrentAutomapManager`; move 8 globals + 13 functions; update ~18 caller files

## 2026-07-01

- Refactor engine/lighting into `LightManager` class with singleton `CurrentLightManager`; update ~22 caller files
- Move 12 theme/trigger/crypt scalar globals (
umthemes`, `armorFlag`, `weaponFlag`, `zharlib`, `trigflag`, 
umtrigs`, `TWarpFrom`, `UberRow`, `UberCol`, `IsUberRoomOpened`, `IsUberLeverActivated`, `UberDiabloMonsterIndex`) into `Level` class with inline accessors
- Replace `Missile::var1`-`var7` with typed anonymous union providing named struct accessors per missile family (projectile, guardian, portal, rune, wall, chain, inferno, nova, firewall, lightning, golem); migrate ProcessGenericProjectile and ProcessGuardian/AddGuardian
- Refactor `game/stores/` into `StoreManager` class with singleton `CurrentStoreManager`; update ~30 caller files

## 2026-06-24

- Add file-level summaries to all source files missing them
- Remove Testing/ from tracking, add to .gitignore
- Move mpq/ to data/mpq/ and storm/ to network/storm/
- Restructure network/ directory (protocol/ and transport/ subfolders)
- Move validation/ files back to respective subject folders
- Move menus/, panel/, debug_overlay/ under ui/
- Remove Discord integration

## 2026-06-23

- Rename dvlnet/ to net_transport/
- Subdivide utils/ into string/, sdl/, endian/, image/, container/, file/
- Subdivide engine/ into audio/, gfx/, load/, math/ subdirectories
- Rename DiabloUI/ to menus/
- Collect validation files into Source/validation/
- Merge control/ and panels/ into panel/ directory
- Remove compatibility forwarders and update all includes to canonical paths
- Migrate remaining Source/ files to domain subdirectories
- Migrate game domains to Source/game/
- Update docs
- Fix portal migration verification
- Document portal source migration
- Move portals into game domain
- Add canonical portal domain interfaces
- Plan portal domain source migration
- Document source organization architecture

## 2026-06-22

- Replace gendung macro shims with accessors
- Plan gendung macro shim removal
- Require comments on level accessors
- Document gendung macro shim removal

## 2026-06-21

- Add positioned tile grid iteration
- Document tile grid iteration API
- Plan tile position range implementation
- Document tile position range design
- Modernize dungeon level tile storage
- Document unified dungeon megatile storage
- Document legacy dTransVal removal

## 2026-06-20

- Remove legacy dPiece storage
- Document legacy dPiece removal
- Move AI docs under docs/AI
- Move player helpers into Player
- line endings
- Add graphical debug piece selector
- Fix debug overlay windows and editor selection
- Expand debug overlay: add Inspector panel & toolbar
- Add built-in help system to debug Lua console

## 2026-06-19

- Add tile fallback and sync with legacy map data
- Refactor: add levelMicros() span for tile micro access
- Migrate dungeon flags and special data to Tile API
- Remove legacy dungeon entity arrays, use Tile API only
- Refactor tile access to use tileAt(Point) API
- Add object subsystem unit tests and test visibility
- Complete Tile state migration
- Update CMake, codecov, and level init in tests/benchmarks
- Add ImGui debug overlay console and refactor console history

## 2026-06-18

- Phase 4B COMPLETE: Update documentation
- Phase 4B-12,13,14: Final entity macro migrations
- Phase 4B-9,10,11: Migrate debug, towners, sync, inv to Tile API
- Phase 4B-8: Migrate cursor.cpp to Tile API entity accessors
- Phase 4B-7: Migrate missiles.cpp to Tile API entity accessors
- Phase 4B-6: Migrate loadsave.cpp to Tile API entity accessors
- Phase 4B-3,4,5: Migrate dead.cpp, items.cpp, objects.cpp to Tile API
- Phase 4B-1 & 4B-2: Migrate player.cpp and monster.cpp to Tile API entity accessors

## 2026-06-17

- Phase 4A lighting migration: Tile API conversion, tests, benchmarks
- Better agent instructions
- Migrate rendering hot-paths to Tile API, update docs

## 2026-06-16

- Add Tile class and Level integration (Phase 2)
- Migrate dItem to be level-specific, not global
- Refactor dItem to be level-specific, not global
- Minor items.h cleanup
- Refactor active entity bookkeeping to pool headers
- Refactor to use MonsterPoolAdapter and ItemPoolAdapter
- Introduce monster pool for modern entity management

## 2026-06-15

- Clean up player attribute migration
- Clarify packed player validation fields
- Group player damage bonuses
- Group player life and mana resources
- Normalize player header line endings
- Modernize object storage with DenseEntityPool
- Refactor object pool access via ObjectPoolAdapter

## 2026-06-14

- Modernize object pool with type-safe container
- Introduce ObjectManager facade for object operations
- Minor comment clean-up to objects.h
- Initial objects.h cleanup
- Add shared attribute value types
- Plan player attribute model migration
- Document player attribute model design

## 2026-06-13

- Refactor player logic into Player class methods
- Introduce World and Level state containers
- Introduce Bestiary and migrate Monster behavior
- Add CombatActor base type
- Refactor players and monsters onto Actor
- Introduce Bestiary class and related engine updates
- Introduce Actor base struct for Player, Monster
- Hellfire content now externalizes the native-derived dungeon level identities
  used by the fixture set through validated composed `levels.tsv` metadata;
  blocked-cell generation and remaining Hellfire catalogs remain explicit follow-ups.
- Hellfire content now also externalizes the Empyrean Band and its native
  attribute, light, recovery, and trap-resistance powers through keyed item and
  unique-item overlays.
- Add a three-cycle server reconnect soak that preserves a resumed participant's
  identity and complete self-first multiplayer snapshot while another session remains connected.

- Preserved content-owned MonsterMagic missile identities through authoritative snapshots, save/restore, and protocol projection so native clients render exact Lightning, flare, and Magma Ball families.

- Added Godot presentation mapping for the seven additive MonsterMagic native visual identities.

- Hardened snapshot restore so MonsterMagic visual identities must resolve through active validated content.

- Hardened protocol projection to reject unknown or misplaced MonsterMagic visual identities.

- Added Godot-model validation coverage for out-of-range native projectile visuals.

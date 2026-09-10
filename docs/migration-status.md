# Godevilution Migration Status

Status date: 2026-09-10

The active content schema is now `1.462.0` (with `1.461.0` retained as
the previous additive contract). Optional `procedural_quest_piece_origins.tsv`
rows publish deterministic ordinary-level quest geometry origins. Their
identities are intentionally separate from the fixed native set-level origin
namespace, and published locations now survive authoritative snapshots,
saves, and protocol projection. The Warlord room and the Halls/Gateway
Catacombs quest rooms now use the separate native-DUN path; remaining licensed
native assets and authored quest placement rows remain deferred.

The checked-in base and Hellfire replay fixtures now use the canonical
content-pack manifest hashes produced by the normalized source identities and
active schema, keeping replay admission aligned with the validated packs.

Native player-versus-player physical attacks now use the native combat
boundary: armor-piercing bonuses are ignored for player targets, target armor
is applied only to admission (not damage), and ranged player attacks apply
the native distance penalty and non-Rogue class damage modifiers are halved
for arrows; initialized monster-target arrows now apply the same native
distance admission penalty. Monster attacks retain their existing
armor-adjusted damage and equipment-piercing rules.

Player-versus-monster physical attacks now use the same native armor boundary:
target armor affects hit admission, while successful melee and weapon-arrow
damage is applied directly without a second armor reduction. The compact native
replay runner and managed processor share this ordering.

Legacy weapon durability remains authoritative for hydrated equipment. Replay
compatibility weapons that omit durability metadata are inert placeholders and
do not consume the native durability RNG boundary.

The class-physical replay matrix is current for armor, armor-piercing, to-hit,
damage-modifier, Barbarian, and Hellfire variants; both runners agree at each
refreshed checkpoint.

Hellfire ranged Peril profiles now also agree on the class-modified projectile
 payload, Peril self-damage, multiplier, and deterministic snapshot hash.

The remaining ranged-class replay profiles now agree across both runners for
Hell difficulty, target armor admission, shifted Hellfire armor, and
Multiple Arrows hit-seed payloads.

Compatibility-template combat now preserves the native fallback armor damage
boundary. Petrified Bone Spirit impacts do not consume a hit roll, and block or
hit recovery does not consume durability RNG when no eligible equipment is
hydrated. The movement, Bone Spirit, and Fireball replay fixture families now
agree at every checked-in checkpoint in the managed and native runners.

Timed authoritative player movement now revalidates both effective level
walkability and participant occupancy when its animation reaches the movement
effect tick. A participant or world mutation that claims the destination during
the animation therefore cancels the step instead of allowing the server state to
overlap or enter newly blocked geometry.
Snapshot capture and restore now preserve that in-flight boundary: a future move
cell is not treated as occupied state, and a saved action may resume against
changed geometry before the same effect-time revalidation cancels it.
Protocol snapshot validators now preserve that boundary as well: pending moves
must remain within the published level bounds, but a destination that is
currently blocked is accepted for effect-time server revalidation rather than
being rejected during managed transport, Godot client-model, or native
compatibility validation.

Player hits now also honor the native Gargoyle wake-up boundary: an active
Gargoyle special flag is consumed by the first physical hit without applying
that hit's damage or equipment effects. Other petrified monsters use the
native guaranteed-hit admission boundary while Stone Curse remains active;
knockback and movement remain suppressed.

Class-driven Cleave now resolves the native adjacent side targets for Monk
staffs, Bard dual swords, and Barbarian axe/two-handed melee, applying the
native adjacent hit penalty and quarter-damage boundary. Cell-targeted melee
attacks now run those same two side lanes when their primary cell is empty or
contains a breakable object, and apply one shared native hit/Decay/durability
decision across the object and side-target results.

Cleave side targets now retain the native Hellfire elemental-channel admission
and fire-then-lightning damage ordering for both entity- and cell-targeted
attacks.

PvP melee now also evaluates those side lanes after a player primary target,
including the native blocked-primary path, through the same shared durability
decision.

Hellfire Peril self-damage now includes the native post-percentage flat weapon
modifier while remaining before class damage and the final target multiplier.

Gargoyle wake-up attacks now preserve the native admitted-hit durability
boundary while still suppressing physical, elemental, knockback, and resource
effects on the waking hit.

Monster-target arrows now defer the Gargoyle wake-up check until their native
impact boundary, so launch-time projectile state does not consume or bypass the
server-owned special lock.

Petrified monsters now use the native guaranteed-hit boundary for player melee,
bow, and magic projectile attacks; Stone Curse still suppresses movement and
regeneration-side transitions rather than combat damage.

The compact native replay runner now mirrors the managed monster activation
window for fixtures that carry an active target: visible targets wake for the
native 255-tick window, while targets outside live-player vision count down
deterministically. The restart-town fixture records that window explicitly and
now passes shared checkpoints in both runners through death, recovery, and town
transition.

The compact replay parity slice now also covers validated store cursor graphics,
zero-seed Rhino charge damage, Reflect retaliation draw consumption, and
equipped-death drop checkpoints. These paths now produce the same canonical
state in the native and managed runners.

Procedural level generation now accepts authored entry cells as deterministic
required-walkable inputs. The generator clears both movement and missile
collision at those cells, and engine/Diablo regression coverage rejects
out-of-grid requirements while proving seeded start positions remain usable.

Schema 1.462 extends those procedural rows with optional semicolon-separated
`required_walkable_cells` coordinates. Multiple authored entry cells now clear
movement, missile, and explicit native-vision blockers while preserving the
single configured start cell and older row shapes.

Schema 1.461 adds an optional independently seeded native
vision-blocker density. Movement collision remains authoritative for walking,
while the projected vision layer can diverge for BlockLight-style geometry;
older rows retain the previous movement-derived vision behavior.

Server and Godot snapshot validation now reject explicit vision layers that
omit movement-blocked cells, preserving the same movement-is-vision invariant
at both ends of the protocol.
The authoritative level projection also reapplies that invariant after
server-owned world mutations add or replace collision geometry.

Godot combat, Telekinesis, and unique-monster dialogue target selection now
uses the same projected native vision boundary as monster rendering. Hidden,
dead, sneak-hidden, and out-of-radius monsters therefore cannot be selected
through a blocked or unlit tile.

The native authoritative runtime now applies the same boundary to
position-based projected-monster interaction lookups. Its shared C++ vision
helper reuses the native ray table, while snapshots without vision geometry
retain the legacy compatibility path.

Native compatibility presentation now consumes that same viewer-aware query
before claiming monster-pool slots, so blocked projected monsters cannot
survive as native rendering or interaction ghosts.

Authoritative monster activation now also honors additive native vision
blockers when a snapshot omits the transparency layer, keeping server AI
visibility aligned with both presentation clients for metadata-only geometry.

Authoritative direct-target spell and Telekinesis validation now uses the same
native vision boundary whenever projected geometry is available. BlockLight-
only native pieces therefore cannot be bypassed by a client action even when
they do not contribute movement collision or a transparency layer.
The query also applies the player's equipment-derived light radius, preserving
the native distinction between target range and what the player can see.

Godot world-item, authored-object, corpse, and portal presentation and
selection now reuse that same projected vision predicate. Hidden world
entities no longer appear as client-side interaction candidates that the
server would reject.

Remote-player world rendering, player-target selection, and action indicators
now use the same projected native vision boundary while retaining party-panel
and other out-of-world authoritative information.

Typed trap, quest/towner, and greeting interaction candidates now also fail
closed outside projected native vision, so HUD actions match world rendering
and server-side interaction admission.

Monster AI line-of-sight consumers now use the projected native vision ray
table whenever geometry is available, preventing ranged and avoidance paths
from reasoning through BlockLight-only tiles while preserving legacy fallback
behavior for geometry-free worlds.

Schema 1.454 adds server-owned object item gates and native monster-layer
mutation assignments. The Gateway pedestal now consumes one Blood Stone per
stage, applies the validated blood2.dun encounter layer after the final stage,
and awards Arkaine's Valor through the authoritative object interaction path.

Schema 1.455 adds validated `warps.tsv` destinations for materialized native
set-level pre-map and active-map variants. Set-level geometry is published
before warp loading, so the same server-owned level-transition path now accepts
authored set-piece destinations while ordinary warp rows remain compatible.

Schema 1.456 extends the same validated destination model to static
`portals.tsv` routes. Set-level portal endpoints use the materialized pre-map or
active-map runtime identity and share the ordinary portal occupancy and
transition path.

Schema 1.457 lets a world mutation load its complete native mega-tile layer
from a validated external `.dun` asset. The Gateway transition now anchors
`blood2.dun` at the final pedestal position, so post-activation presentation
uses the licensed native tile layer while the existing monster-layer mapping
remains authoritative.

Schema 1.458 extends those mutation assets with validated `.til` and `.sol`
inputs. A mutation can now materialize the native d-piece layer and replace
movement/missile collision across its `.dun` footprint, while preserving the
server-owned projection and rejecting mixed inline collision overlays.

Schema 1.459 adds `native_layer_asset_path`, allowing a mutation to project
the validated DUN mega-tile, transparency, object, and monster layers as one
atomic authored overlay. It can be paired with the collision bundle so the
server owns the complete map replacement without hand-authored layer rows.

Schema 1.460 adds `replace_collision_geometry` to native DUN fragments. A
fragment with a validated `.til`/`.sol` bundle can now clear its authored
footprint before applying native collision provenance, matching the replacement
semantics used by world mutations.

Native DUN geometry definitions can now be supplied as a validated external
TSV pair (`native_dun_piece_definitions.tsv` and
`native_dun_tile_properties.tsv`). Ordinary levels, deferred fragments, and
set-level maps may use that shared expansion when licensed `.til`/`.sol` bytes
are unavailable; declared binary pairs remain authoritative. This advances
the content boundary without claiming that any missing licensed tile data has
been recreated.

The Hellfire Warlord room is now published as an availability-gated native DUN
fragment at an explicit validated anchor on runtime Hell (the native level-13
encounter). Its raw monster layer and
template-only steel tome are activated only while the Warlord quest is not
unavailable, matching the native `IsAvailable` gate without inferring placement
from legacy object IDs. Native random chamber selection and licensed collision
replacement remain deferred.

The Halls of the Blind and Gateway of Blood rooms now use availability-gated
native DUN fragments at explicit validated anchors on runtime Cathedral and
Catacombs. The Gateway fragment deliberately uses the native pre-pedestal
blood1.dun asset; the server-owned three-stage pedestal consumes Blood Stones,
applies the validated blood2.dun monster layer and anchored mega-tile layer,
and awards Arkaine's Valor. Their book templates inherit the fragment anchors,
while native random-chamber selection and licensed collision replacement remain
deferred.

The shared geometry catalog is retained by the simulation for deferred
set-piece publication and snapshot restore, so anchored fragments use the same
validated collision definitions whether they materialize during bootstrap or
after their runtime origin becomes available.
The validated catalog is also retained on the content-activation result for
server hosts that inspect or coordinate deferred geometry publication.

World mutations can now anchor their authored rectangles, transparency, d-piece,
and mega-tile assignments to a published ordinary-level quest-piece origin via
`anchor_procedural_origin_id` or its stable symbol. This keeps fixed native
set-level anchors separate while allowing the same server-owned overlay path to
drive future quest geometry transitions.

Native DUN fragments and their authored object placements use the same separate
origin namespace. Their assets remain validated at activation, while geometry
and template objects defer until the generated ordinary-level origin is
published.

Native DUN monster mappings now also accept an optional stable `monster_symbol`
cross-check. Numeric-only rows remain compatible, while shipped base and
Hellfire mappings validate their authoritative monster identity against the
active stable namespace before materialization.

Snapshot rejection rollback now remains valid when the live player has an
in-progress action newer than the incoming snapshot. The server preserves the
original validation failure and the live action state instead of surfacing a
rollback `AggregateException`.

The Godot client now uses a shared authoritative viewport transform for world
rendering and mouse hit-testing. Small levels are centered, large levels clamp
to their edges while following the local player, and the transform uses the
actual viewport dimensions rather than fixed 1280×720 coordinates. This keeps
the presentation client aligned with server geometry as map sizes vary.

Ordinary and procedural level definitions now carry optional content-owned
`start_x`/`start_y` entry positions. Content activation validates that each
position is in bounds and walkable, and the authoritative host applies the
selected level's position to newly created participants. Packs that omit the
columns retain the legacy `(0, 0)` default so existing fixtures remain
compatible.

Managed and compact native replay parsers now reject checkpoint labels longer
than the 64-character SHA-256 boundary while continuing to support shorter
symbolic labels used by compatibility fixtures.

Compact native replay now routes player-owned Firebolt projectiles exclusively
 through the shared magic-projectile resolver. This prevents the autonomous
 monster-magic lane from consuming player missiles, preserving Firebolt
 resolution, PvP, Hellfire block, Guardian admission, impact timing, and RNG
 parity with the managed authoritative runner.

The primary Acid puddle fixture now carries valid 64-character tick-1 and
tick-2 checkpoint hashes. Both replay runners validate those intermediate
states instead of silently treating the malformed values as non-hash labels.

The class-aware Monk replay lanes now preserve authored level IDs, Hellfire
combat rules, and native base/current attribute semantics in the managed Bone
Spirit and Hork Spawn harnesses. The two remaining secondary-Monk fixtures now
carry the required protocol schema metadata, and the focused six-test managed
lane matches native checkpoints.

Reusable managed replay executors now also carry each fixture's declared game
variant and difficulty into the simulation, preventing autonomous combat lanes
from silently falling back to Diablo/Normal defaults.

Autonomous MonsterMagic impacts now share the managed primary-player GetHit and
Mana Shield boundary in compact native replay, while secondary impacts retain
their Q6 GetHit adjustment. The new Lich fixtures verify projectile impact,
equipment resistance, primary block admission, mana absorption, life
preservation, and both canonical checkpoints.

Acid projectile impacts now share the managed equipment GetHit, acid resistance,
and Mana Shield boundary in compact native replay. The primary Acid fixture
also verifies the resulting puddle lifecycle and canonical checkpoints.

Primary SkeletonMelee replay now shares the managed reflection and Mana Shield
ordering in compact native replay. The cross-runner fixture covers the
post-GetHit damage, charge consumption, target HP, mana, and multiplayer RNG
checkpoint.

The Rhino special-charge path now remains distinct from generic Thorns
retaliation: both runners preserve the authored charge damage and movement but
do not consume a Thorns roll for a Rhino victim carrying that equipment.

Rhino charge replay now applies the primary player's validated incoming GetHit
equipment adjustment at the special-charge impact boundary in both runners,
while preserving the native charge movement and knockback state.

Special SkeletonMelee-family monster attacks now use the same validated
incoming-equipment GetHit and Thorns boundary as generic monster hits. A shared
multiplayer checkpoint covers secondary-player damage, monster retaliation, and
the compatibility RNG stream.

Initialized monster-hit replay now applies usable-equipment Thorns retaliation
against the attacking monster in both runners. The shared GoatMelee checkpoint
covers target HP reduction and the additional deterministic retaliation roll.

Initialized monster-hit replay now applies the target player's validated usable
equipment `PlusGetHit` bonus in both runners. Generic primary and secondary
monster attacks share a checkpoint that includes the adjusted incoming damage,
with native reflection and mana-shield ordering preserved for primary targets.

Initialized physical replay now applies usable equipment Knockback in both
runners. A surviving, non-petrified target moves one valid unoccupied cell
away from the attacker, with bounds, secondary-player, and object occupancy
guards reflected in the shared checkpoint.

Ranged physical replay now applies validated usable-equipment StealLife and
StealMana restoration at the impact boundary in both runners. The new fixture
covers life/mana caps and a shared post-impact checkpoint, including the
server-owned projectile removal transition.

Initialized ranged class replay now cross-checks Monk, Bard, and Barbarian
damage profiles in both runners, including the Monk weapon, Bard bow, and
Barbarian bow branches with their class-specific RNG checkpoints.

Initialized class replay now retains the native compact fallback when authored
Strength is zero, matching the native positive-attribute gate and preserving
the unadvanced RNG state for the fallback ranged attack.

Monk unarmed replay now recognizes an unclassified occupied hand slot as an
unarmed profile and uses the externalized level/2-to-level damage range in both
runners, matching the native item and class rules.

The replay parity boundary now covers initialized Rogue ranged attacks under
Hellfire armor piercing: hit admission applies the shifted armor rule while
successful projectile damage remains direct, with matching C# and native
checkpoints.

Initialized Warrior ranged attacks now also apply the native Critical Strike
roll and Triple Demon Damage multiplier before projectile capture, with matching
managed/native checkpoint coverage.

Ranged elemental arrows now also cross the impact boundary in both runners;
their server-owned fire channel applies target resistance before projectile
removal and the resulting checkpoint is shared with native replay.

Ranged miss replay now also clears elemental channels before projectile capture,
keeping failed admissions and their RNG consumption identical across C# and
native runners.

Initialized Multiple Arrows replay now salts each arrow's hit admission with its
volley index, preserving per-arrow outcomes and shared damage RNG ordering in
both runners.

Hellfire ranged miss replay now skips the post-hit Devastation, Doppelganger,
and Jesters rolls when no damage was admitted, preserving the native RNG state
at the shared projectile checkpoint.

Initialized class Peril replay now carries the pre-modifier attack payload for
self-damage while preserving direct projectile damage and the Hellfire
multiplier in both runners.

Hellfire ranged replay now carries unique and Diablo target eligibility into
the compact native projection, so Doppelganger rolls are skipped consistently
for excluded targets while ordinary targets retain the proc path.

Hellfire Peril replay now carries authored equipment `PlusGetHit` into the
attacker self-damage payload for both melee and ranged class profiles, with
matching native and managed checkpoints.

Initialized ranged class replay now applies Nightmare/Hell monster armor to
both hit admission and projectile damage before capture, matching the managed
and native checkpoints.

Native progression compatibility now uses one shared `Devilution.Shared`
contract for stable/native spell IDs, spell levels, memorized slots, character
levels, Diablo victory ranks, and Reflect charges. The protocol and Diablo
validation paths consume the same source; native spell identity remains the
shared `0..51` limit used by replay and boundary tests.

Timed player-action ordering is now one shared domain contract consumed by the
Diablo simulation, mapper, host snapshot validator, and Godot protocol client.
The client links the pure rule source directly, preserving the dependency
boundary between presentation transport and the server domain assembly.

Native monster AI timer, short-counter, retreat, and death-animation bounds now
also consume one shared compatibility source across Diablo state/catalog
validation, replay fixture loading, the protocol contract, and native envelope
assertions.

Native monster movement counters now use that shared compatibility source
across Diablo ticking/restoration, snapshot validation, protocol aliases, and
native envelope assertions.

Native monster movement modes and turn-state fields now use shared `-1..3`
and `0..1` compatibility bounds across managed/native snapshot validation,
Diablo state restoration, replay loading, and protocol aliases.

Native monster AI timers and counters now consume the existing shared timer,
short-counter, retreat, and movement bounds at managed mapper/host/Godot and
native snapshot boundaries, rejecting malformed state before presentation or
persistence.

Native monster AI identity, directional state, and Scavenger corpse references
now validate consistently at managed mapper/host/Godot and native snapshot
boundaries, rejecting malformed cross-entity state before presentation or
persistence.

Monster item and AI runtime seeds now validate as an all-or-nothing pair at the
mapper, host, Godot, and native snapshot boundaries, preserving deterministic
native initialization during the migration.

Player-minion owner references now resolve to a present player on the same
level at mapper, host, Godot, and native snapshot boundaries, rejecting stale
or cross-level server-owned ownership before presentation or persistence.

Projectile destination coordinates now validate against their projected level
geometry at mapper, host, Godot, and native snapshot boundaries, matching save
restore validation and rejecting malformed travel endpoints before presentation
or persistence.

Cell-targeted player attacks now remain distinct from entity-targeted attacks at
the domain, mapper, host, Godot, and native boundaries. Their explicit target
coordinates must remain within projected geometry, preserving the native
animation-time attack path without admitting malformed action state.
Cast actions now apply the same coordinate contract to tile-targeted spell
intents: native tile ranges are always enforced, and targetless casts must stay
inside projected level geometry while stable entity-targeted casts retain their
entity identity path.
Cast source references now use the same native slot/page bounds as command
admission, and charged/consumable source kinds cannot cross the action boundary
without an item reference; zero stable IDs remain compatible with legacy state.
Timed action snapshots also keep the legacy action-level target coordinates clear
for entity attacks, casts, movement, and terminal transitions; only explicit
cell-targeted attacks carry coordinates in that channel.
Action progress now uses the same snapshot tick contract at every boundary:
active actions must have started but not ended, and `EffectApplied` must be false
before `EffectTick` and true at or after it. This keeps presentation and save
restore from accepting a timeline that the authoritative tick loop could not
have produced.
Death actions also require the nonzero source entity recorded by the server;
managed and native snapshot validators reject an unreferenced death timeline.
Native compatibility validation now also rejects impossible standalone combat
mode lifetimes before action projection, matching the managed boundaries.
Native tile-coordinate bounds are now one shared wire contract across managed
mapper/host/Godot validation and native snapshot/world projection, including
authored towner greeting coordinates; boundary tests retain the exact
0..255-compatible range.

The remaining native monster scalar bounds for experience, accuracy,
intelligence, directions, pack/activity state, Fallen animation/rally state,
and resistances now consume that same compatibility source across Diablo
construction, snapshot/catalog validation, replay/protocol aliases, and native
envelope assertions.

Replay parity maintenance now keeps base-content fixture manifests current
and aligns the managed replay executor with native multiplayer Snake, Fallen,
and Mega Inferno checkpoint hashes, including deterministic RNG consumption and
the native compact impact boundaries. Native and managed malformed companion
fixtures now report the same validation precedence.

The replay gate now also carries explicit player strength, dexterity, and
vitality plus authored physical weapon ranges, covering all six native classes
at shared native/managed checkpoints. Warrior and Sorcerer now exercise the
standard formula, while Monk, Rogue, Bard, and Barbarian cover their class
profiles; both critical-strike classes preserve their class RNG draw. Legacy
compact attack fixtures retain their armor-only fallback until they author the
full class-attribute contract. Authored armor-piercing and Hell difficulty
target modifiers now cross the native/managed replay gate. Authored percentage,
flat, and to-hit weapon modifiers are also covered; remaining weapon and
hit-resolution edge cases are still follow-up work. Sword/mace target-class
damage adjustment now also crosses the replay gate for undead targets, with
both sword and mace branches covered. Animal target-class adjustment is now
covered by the matching inverse sword/mace fixtures.

The 1 MiB authoritative envelope cap is now a named shared protocol limit
consumed by the managed server, Godot client, and native compatibility codec.

Native player/entity capacities and level geometry bounds are also named in the
shared protocol contract. Managed validation, Godot validation, Diablo mapping,
and native compatibility code now consume the same values for player, monster,
object, item, portal, warp-trigger, and level-dimension boundaries.

Native scalar compatibility bounds for monster state, player progression, spell
levels, visited-level history, light radius, and quest/towner payloads are now
named in that contract as well. Managed validators and native projection use
the same maxima instead of repeating raw literals.

Authored towner greetings now pass the same count, identity, message, and level
geometry validation at the server snapshot boundary, Diablo mapper boundary,
Godot client boundary, and native session boundary.

Player status-effect collection caps and native inventory, equipment, belt, and
grid topology limits are now named in the shared protocol contract. Managed,
Godot, and native snapshot boundaries consume those limits, and native
projection now fails closed on malformed status-effect collections.

Fallen-rally timing, monster resistance, directional presentation, and native
identity bounds now also consume named shared limits at the Diablo mapper,
managed/Godot validation, and native projection boundaries.

Native-compatible stash page, cell, and 10x10 geometry limits now consume the
same named protocol contract in the C# host/Godot models and native snapshot,
runtime, and UI projection paths; the dependency-free Diablo domain retains
its independent stash-layout helper to preserve assembly boundaries.

Stable item-reference command construction and server mapping now reject
inventory, belt, equipment, and stash slots outside those native topology
limits before a gameplay intent is created.

Native world projection and presentation now consume the same named bounds for
monster pack state and projectile damage/phase metadata as the snapshot and
managed validation paths.

Quest/towner byte fields, optional-value sentinels, and authored reward
offsets now use named shared protocol limits across managed validation, Diablo
mapping, Godot validation, and native snapshot projection.

Authored object projectile and quest-follow-up offsets now use the same named
native object-offset limits across managed validation, Diablo mapping, Godot
validation, and native snapshot projection.

Replay fixture validation now consumes the shared native inventory, equipment,
belt, monster, character-level, and resistance compatibility limits, keeping
the cross-language replay gate aligned with snapshot validation.

Native movement-intent direction bounds now use named shared `-1..1` limits at
managed, Godot, native validation, and native command-construction boundaries.

Initialized physical replay now carries validated authored armor-piercing
bonuses and Hell difficulty target modifiers through both runners. Percentage,
flat, and to-hit weapon modifiers now cross the same gate; other
weapon/hit-resolution edge cases remain follow-up native-parity work. The
initialized undead sword and mace paths now include the native target-class
modifier, as do the corresponding animal paths. The initialized demon path now
also includes the validated post-hit `TripleDemonDamage` multiplier. The
Barbarian path now covers the native melee armor-reduction divisor when
armor-piercing equipment is present. Hellfire physical replay now carries
authored damage-armor flags and the deterministic post-hit `Devastation` proc.
Hellfire armor-piercing replay now also uses the native shifted-armor hit
formula rather than the Diablo additive-piercing formula.
Hellfire replay now also applies slot-ordered post-attack `Decay` mutation to
authored weapons.
Ranged Hellfire replay now applies the same mutation once per emitted arrow,
before projectile resolution, with the refreshed Hellfire manifest identity
checked by both runners.
Hellfire physical replay now also applies the native post-hit `Jesters`
roll and upper-half expansion at the shared checkpoint.
Hellfire physical replay now also applies `Peril` self-damage from the
pre-modifier attack and its final target-damage multiplier.
Ranged Hellfire replay now applies the native `Jesters` Q6 scaling before
elemental channel resolution.
Ranged Hellfire replay now also applies the validated post-hit `Devastation`
multiplier before projectile snapshotting.
Native player attacks now carry the remaining `DamageWeapon` boundary into the
authoritative runtime: successful melee hits use the native 1-in-30 roll,
weapon volleys use the native 1-in-40 roll per emitted arrow, hand slots are
processed in native order, indestructible equipment is preserved, and broken
weapons or shields are removed with equipment-resource recalculation. Hellfire
`Decay` removal suppresses the subsequent ordinary durability roll, matching
the native early return.
Timed player recovery now carries the native incoming durability boundary:
block expiry uses the 1-in-10 parry roll for left-hand shield/staff and
right-hand shield damage, while hit recovery uses the 3-in-4 armor roll with
native helmet/chest selection. Indestructible equipment is preserved and
zero-durability items are removed with authoritative resource recalculation.
Melee cell attacks now also honor the native `PlrHitObj` boundary: the first
adjacent authored breakable object is destroyed through the authoritative
world state, and the same Hellfire-decay/ordinary weapon-durability ordering
is applied as a successful melee hit. Ranged cell attacks retain projectile
collision handling for breakable blockers.
Both immediate and timed cell attacks now update the server-owned facing before
resolving the native attack path.
Ranged Hellfire replay now carries the validated `Peril` self-damage payload
and post-hit multiplier into the projectile snapshot.
The ranged `Peril` impact replay now applies that deferred self-damage at the
native arrow collision tick and preserves the shared target/removal checkpoint.
Ranged Hellfire replay now carries the validated `Doppelganger` proc flag into
the projectile snapshot.

All server-owned save envelopes, combined session checkpoints, command
receipts, and world-seed files now use strict UTF-8 persistence I/O. Malformed
bytes and invalid UTF-16 are rejected before a recovery path can treat the
content as authoritative.

The version-one Diablo save codec also requires canonical Base64 snapshot text,
so whitespace and alternate encodings cannot produce multiple persisted forms
of the same authoritative state.

Session persistence now publishes the combined checkpoint before writing the
legacy save and command-receipt mirrors. A failure in a compatibility mirror
therefore leaves the latest authoritative recovery document available.

External content-pack tables, overlays, and binary assets now share canonical
relative-path validation, so traversal and path aliases cannot enter content
composition or manifest identity.

Content-pack manifests and TSV tables now reject malformed UTF-8 before
composition, keeping external pack validation deterministic across hosts.

Verified gameplay-module packages now reject malformed UTF-8 manifests,
reparse-point entries, and noncanonical assembly paths before assembly hashing
or reflection loading.

The named projectile behavior contract now reaches the server snapshot hasher
and native compatibility projection/presentation switches, so resume hashing
and legacy rendering no longer depend on unexplained raw behavior literals.

Projectile presentation-family and native-visual bounds now use named shared
protocol limits in managed validation and native snapshot projection, keeping
all three additive presentation identities aligned at the wire boundary.

Status-effect behavior identities now use the same shared-limit boundary in
server/Godot validation and native player projection, preserving typed Search,
Infravision, and Etherealize state across the compatibility bridge.

Item presentation, spell-source, item-action, and spell-targeting identities
now consume shared protocol maxima in managed validation and native projection,
so external item semantics cannot drift between clients.

Authored object presentation and quest-interaction metadata now consume shared
protocol maxima in managed validation and native projection, keeping external
object effects, transitions, rewards, follow-ups, and towner stages aligned.

Event batches, world mutations, quests, set-level origins, and materialized
procedural levels now consume shared collection caps in managed validation and
native snapshot projection.

Native set-level IDs and monster leader-relation values now consume shared
compatibility maxima in managed validation, Diablo mapping, and native
projection.

Projectile damage/phase and unique-monster dialogue-state values now consume
shared protocol maxima in managed validation and native projection.

Projectile behavior values now have one named protocol identity enum shared by
managed validation and legacy Godot presentation fallback; native bounds mirror
the same maximum rather than repeating an unexplained literal.

Projectile presentation now uses the typed server-owned `native_visual` enum
as the authoritative identity in both Godot and native clients. The validated
content-owned `native_missile_id` remains available only as a compatibility
fallback for older snapshots that omit the enum, keeping current presentation
decisions independent of raw content strings.

Godot presentation now treats an older authoritative snapshot as a bounded
rewind event. The presentation clock exposes a one-advance rewind marker, and
the world view snaps cached players, monsters, and projectiles to the resync
state before resuming normal interpolation. This keeps reconnect/resync
presentation from easing backward through stale positions while leaving the
authoritative tick and simulation unchanged.

Godot reconnect epoch handling now also ignores transient event batches until
the first validated snapshot of the new epoch arrives. Events resume normally
after that snapshot, while command-result state remains available for retry and
feedback.

The native compatibility bridge now consumes server-owned localized dialogue
text when a `DialogueEvent` carries it, while retaining message-ID lookup for
older ID-only peers. General vendor/service greeting presentation therefore no
longer requires the local native text catalog when the active content pack
provides the message.

The same dialogue boundary is now validated consistently at every protocol
edge: server, Godot, and native clients reject NUL/carriage-return text and
payloads above 16 KiB of UTF-8 bytes before presentation.

Content-owned native missile identities now use the same 128-byte UTF-8 limit
as other opaque protocol identities at every snapshot boundary, preventing an
oversized presentation identity from reaching native or Godot projection.
Managed validators now consume these limits from the protocol assembly rather
than maintaining separate server/Godot constants.

The replay gate now covers the validated Acid projectile lifecycle across the
native and managed runners. A seeded initial Acid missile expires into its
splat, shifts into the authored one-tile puddle, enters impact, and expires
without admitting a duplicate controller; projectile identity allocation also
reserves externally seeded IDs so replacement projectiles remain collision-free.
The authored AcidUnique identity now exercises the same lifecycle through its
validated alias mapping and fails closed before generic combat dispatch.
Hellfire multiplayer Acid and AcidUnique fixtures now route an authored Acid
missile to the secondary participant, apply shifted Q6 damage, and preserve
the primary player's terminal state through the shared splat/puddle expiry.
The native fixture runner also carries the authoritative monster activation
window used by multiplayer death suppression.

FireBat now has a validated external Firebolt missile mapping and a two-
participant replay lane. Native and managed runners select the nearer
secondary participant and preserve the monster-owned Firebolt travel state.

FireBat Firebolt and Magma MagmaBall now also have seeded multiplayer
secondary-impact fixtures. Native and managed runners agree on the impact
damage, projectile removal, and preserved primary-player terminal state. The
Torchant Fireball now has the same seeded secondary-impact coverage. Native and
managed runners agree on the authoritative distance-based hit admission, full
Q6 damage, impact-phase collision marker, projectile removal, and preserved
primary-player terminal state.
The companion miss fixture now proves the same distance-based admission on both
the direct and shifted checks, suppresses duplicate zero-distance collision
rolls, and retains the impact-phase projectile through the next tick.

Torchant now has a validated external Fireball missile mapping and a two-
participant replay lane. Native and managed runners select the nearer
secondary participant and preserve the monster-owned Fireball travel state.

Magma now has a validated external MagmaBall mapping and a two-participant
Hellfire replay lane. Native and managed runners select the nearer secondary
participant and preserve the monster-owned initial travel advance.

Storm now has a validated external ThinLightning mapping and a two-participant
Hellfire replay lane. Native and managed runners preserve the monster-owned
controller/segment lifecycle while selecting the nearer secondary participant.

The shared replay envelope now carries Fallen stand-animation frame/tick and
rally-timer state. Native and managed runners advance the same 11-frame/3-tick
animation, perform the animation-boundary `FlipCoin(4)` admission, apply the
native Q6 self-heal, and project the rally timer. Canonical hashes include the
additive `godevilution-monster-fallen-rally-v1` state. The cross-runner
stand-animation, rally-admission, one-tick pursuit, and bounded companion-
propagation fixtures close the current Fallen checkpoint gap. The live
controller remains authoritative for larger monster groups; the replay
contract intentionally keeps this extension bounded while parity coverage
expands incrementally. Native and managed fixture loaders now reject the same
missing-leader, duplicate-identity, cross-level, non-Fallen-AI, vitality,
intelligence, animation, and rally-timer companion states before execution.

Fallen rally pursuit now has two-participant replay coverage. Native and
managed runners select the nearer secondary participant for the rally attack
goal, preserve the farther primary, and agree on the diagonal movement and
countdown checkpoint.

Butcher pursuit now has two-participant replay coverage. Native and managed
runners select the nearer secondary participant, preserve the farther primary,
and agree on the seeded RandomWalk turn ordering when an active object blocks
the straight candidate.

Skeleton King pursuit now has two-participant replay coverage. Native and
managed runners select the nearer secondary participant, preserve the farther
primary, agree on the compatibility RoundWalk 90/45/straight candidate
ordering, and preserve the native decision-delay admission/countdown.

Warlord and Snotspill now share the same two-participant SkeletonAi replay
coverage. Native and managed runners preserve their explicit authored native-AI
identities while agreeing on movement-delay admission and countdown state.

Gharbad now has two-participant replay coverage through the shared GoatMelee/
AiAvoidance compatibility path. Native and managed runners preserve the
authored Gharbad identity, select the nearest live participant, and agree on
the deterministic movement checkpoints. External Gharbad rows also accept the
same validated avoidance-delay tuning as GoatMelee and Gargoyle.

Zhar and Lazarus now share two-participant replay coverage through the native
CounselorAi compatibility path. Native and managed runners preserve their
authored identities, route the nearer secondary participant, and agree on the
delay/countdown state.

Lachdanan and FireMan now have explicit non-combat replay coverage. Native and
managed runners preserve their quest-only or unregistered AI identities and
fail closed before generic combat or movement dispatch.

The deterministic replay gate now also includes a seeded GoatMelee
multi-participant movement fixture. Both runners select the nearest live
participant for the autonomous path while preserving the farther participant's
state, extending multi-actor coverage beyond projectile-only lanes.

The same gate now covers a distinct Gargoyle AiAvoidance multi-participant
fixture. Native and managed runners select the nearest live participant,
preserve the farther participant, and advance the Gargoyle AI stream through
the authored avoidance movement branch.

The replay gate also covers Bat retreat target selection with two live
participants. Native and managed runners select the nearer secondary player,
preserve the farther primary player, and retain the same retreat movement
state and canonical checkpoints.

The same multi-participant boundary now covers Zombie long-range action and Fat
movement. Both runners select the nearer secondary player, preserve the farther
primary player, and retain each AI branch's authoritative movement state.

Sneak concealment now has the same two-participant replay coverage. The nearer
secondary keeps the monster inside the native preferred-distance band, while
the farther primary would trigger concealment; both runners preserve the
nearest-target decision and canonical state.

Rhino charge now has the same two-participant replay coverage. The nearer
secondary receives the native special damage and knockback while the farther
primary remains unchanged; both runners preserve the charge landing and
canonical checkpoint.

SkeletonMelee now has the same two-participant replay coverage. The nearer
secondary receives the native adjacent attack while the farther primary remains
unchanged; both runners preserve the branch-specific checkpoint.

Snake charge now has the same two-participant replay coverage. The nearer
secondary owns the charge projectile and its full travel/removal timeline while
the farther primary remains unchanged; both runners preserve the final state.

Counselor now has two-participant replay coverage. The native and managed paths
select the nearest eligible player with entity-id tie-breaking, preserve the
farther primary, and agree on the adjacent delay checkpoint.

Mega Inferno now has two-participant replay coverage. The native and managed
paths select the nearer secondary participant for InfernoController admission,
preserve the farther primary, and agree on the controller, segment, and delay
checkpoint state.

The shared object-quest replay fixture now carries an explicit authored quest
link. Native and managed runners apply the same authored-interaction admission,
quest progression, canonical object identity hashing, and duplicate-advance
rejection, preserving deterministic parity across the migration boundary.

Authored unique-monster dialogue snapshots now carry an additive validated
`dialogue_state` contract. Warlord and Gharbad compatibility clients use the
server-owned inquiring/combat state instead of inferring terminal progression
from native AI or quest tables; legacy peers retain the zero-valued fallback.

General town vendor greetings now have an additive `TalkToTownerRequested`
command. The base content pack supplies native towner positions and message
identities; the server validates adjacency and cursor state, emits the dialogue
event, and leaves store-menu presentation to the client.
The same greeting metadata is now included in authoritative snapshots so the
Godot client can discover and route these interactions without native positions.

Authored object snapshots now also carry an optional dialogue identity. Native
and Godot object selectors treat a nonzero `dialogue_message_id` as an
authoritative interaction even when `quest_id` is zero, enabling dialogue-only
environmental objects without client-side message tables.
The server now applies the same authored-interaction predicate at command
admission, so presentation-only objects cannot be activated by crafted intents.
Snapshot replacement also reapplies the authored dialogue identity to existing
object instances, and canonical snapshot hashing includes that identity,
preserving dialogue-only interactions across reconnect and save hydration.

The `ReadQuestNote` action now resolves its dialogue identity from the authored
item definition (`item_action_message_id`) instead of embedding the native
message number in the server action branch. Older packs remain compatible via
the native `TEXT_BOOK9`/322 fallback. The Diablo content schema is now
`1.449.0`; `1.448.0` remains accepted as the previous additive contract.

Na-Krul note reading is now an authored `ReadQuestNote` item action. Connected
clients submit the stable note reference, the server emits the content-backed
`TEXT_BOOK9` dialogue without consuming the note, and native/Godot clients
present the same server-owned text. The Diablo content schema is now `1.449.0`;
`1.448.0` remains accepted as the previous additive contract.

The Cathedral Map is now an authored `OpenCathedralMap` item action. Connected
clients submit the stable item reference, the server validates ownership and
emits a recipient-scoped `MapOpenedEvent`, and native/Godot clients present the
map without consuming the item or mutating gameplay state. The Diablo content
schema is now `1.449.0`; `1.448.0` remains accepted as the previous additive
contract.

Authoritative monster snapshots now enforce DevilutionX's 200-entry active
monster pool per level at runtime, save, server, host, Godot, and native
projection boundaries. Projectiles remain dynamically allocated in the native
runtime; their 125-entry save payload bound is a persistence-format limit, not
an active projectile pool contract.

The generic host now also rejects over-capacity native object and world-item
groups per level, completing the transport-side pool checks already enforced by
the server mapper and client projections.

Authoritative participant snapshots now enforce the native four-player pool at
the server mapper, generic host, Godot, and native projection boundaries.

Projected level geometry now enforces the native seven warp-trigger slots per
level across server, host, Godot, and native validation boundaries.

Generic host admission now rejects malformed quest state and quest/towner
interaction references before snapshots reach connected clients, matching the
server, Godot, and native validation contracts.

Generic host admission now also rejects malformed level dimensions, cell layers,
duplicate geometry identities, and invalid warp targets before transmission.

Generic host admission now also rejects entity positions outside projected
geometry and malformed set-level origins or procedural-level identity lists.

Generic host admission now also rejects malformed projected player-action timing
and semantic shapes, including blocked movement and invalid cast sources.

Generic host admission now also rejects malformed monster vitality, combat
ranges, occupancy, movement timing, Fallen animation/rally state, and
pack-leader relationships before transmission.

Generic host admission now also rejects malformed authored object effect,
quest-transition, reward, projectile, presentation, and breakage metadata
before transmission.

Generic host admission now also rejects contradictory player combat-mode
lifetimes, matching native and Godot snapshot admission.

The generic TCP host now validates outgoing command acknowledgements for result
count, status, and reject-reason consistency before transmission.

Native AiAvoidance RoundWalk chance tuning is now validated monster content. The
shipped GoatMelee and Gargoyle rows carry the native `16` chance base and `5`
intelligence scale; older or custom packs that omit the optional pair retain
the native-compatible fallback.

Authored unique placements and their generated pack members now resolve stable
monster IDs through the validated catalog before invoking native-ID summon
logic. This preserves content identity when stable and native IDs differ.

Authored quest-object monster follow-ups and Hork Spawn payloads now use the
same stable-ID boundary. Their content rows identify the stable monster first;
the server resolves the corresponding native selector only at summon creation,
so stable/native ID differences cannot silently summon the wrong monster.

Generic host admission now also rejects malformed world-item prices, native
identity metadata, and item-state ranges before transmission.

Generic host admission now also rejects malformed player inventory, equipment,
belt, stash, held-item, and active-store projections before transmission.

Generic host admission now also rejects malformed player resources, attributes,
visited levels, memorized spells, status effects, and stash footprints before
transmission.

Generic host admission now also rejects malformed projectile source/lifetime,
damage, behavior, presentation, direction, and native missile identity state.

Godot HUD and world rendering now share a typed projection of authoritative
player statuses, including Mana Shield, Reflect charges, and timed status
families, while preserving server-owned durations and values.

All Godot status consumers now use the reusable active-status projection,
including Search-driven world-item highlighting; zero-duration effects cannot
create presentation state.

The Godot HUD now renders exact projected status durations (or permanent state)
on a dedicated line; it never advances or expires those values locally.

The Godot protocol model now marks reconnect as a server-epoch boundary. After
`ResetTransientPresentation()`, the next validated snapshot is admitted even
when its tick is lower than the retained snapshot, allowing process-restart
recovery without stale-state lockout. Transient events and movement prediction
are cleared while the command-result ledger remains available until replacement
acknowledgements arrive.

The base content pack now includes the canonical early undead and Fallen
families (Ghoul through Dark One) with validated native AI metadata and DUN
monster mappings. Server-owned level generation can select those stable
identities without test-only overlays; the generic compatibility monster and
authored Skeleton King, Butcher, and Snotspill encounters remain unchanged.

The base pack now also carries canonical Skeleton-family and Scavenger rows
(Skeleton through Scavenger), including their native AI metadata and DUN
monster mappings. Together with the later family blocks, the composed Hellfire
catalog now covers every native monster identity from 0 through 137; the
complete set is guarded by shipped-content regression coverage.

The next early-undead progression is now available as well: Carver, Plague
Eater, Shadow Beast, Bone Gasher, the ranged Skeleton family, and the Skeleton
Captain variants. Native Carver is retained as a stable identity without a DUN
mapping because raw identity 14 is reserved by the authored Snotspill encounter.

The base pack now also carries native monster identities 28–45, covering the
Invisible Lord/Sneak, Goat, and Bat families through Night Clan. Their validated
AI selectors and available native DUN mappings are available to production
generation; raw DUN identity 31 remains reserved by the authored Banner fragment.

The base pack now continues with the midgame Acid, Fat, SkeletonMelee, Magma,
and Rhino families (native IDs 46–49 and 52–67). Their stable identities,
validated AI selectors, and native DUN mappings are available to production
generation while the authored Skeleton King and Butcher identities remain intact.

The next production family block now covers native identities 68–88: Storm and
FireMan variants, Devil Kin Brute, Gargoyle variants, and Mega-family elites.
Their validated AI selectors and native DUN mappings are available to production
generation.

The base pack now continues through native identities 89–111, covering the
late Snake and Skeleton families, Succubus variants, and the Counselor family
through Advocate and Arch-Litch Malignus. Their stable IDs, AI selectors, and
native DUN mappings are available to production generation.

The next Hellfire block now covers native identities 112–137, from Hellboar and
Psychorb through HorkDemon, Necromorb, Torchant, FireBat, Lich, BoneDemon,
ArchLich, and Na-Krul. Their stable IDs and available native DUN mappings are
available to production generation; native identity 133 has no source DUN
conversion row and remains stable-ID-only.

The base pack also declares the native Golem and The Dark Lord identities
(stable IDs 111 and 112) with their available DUN mappings. Every shipped
monster now has an explicit role in the content-owned
`monster_spawn_policies.tsv` table: ordinary identities may scatter, while the
authored Skeleton King, Butcher, and Snotspill encounters, Golem summon, and
The Dark Lord boss remain excluded from ordinary generation. The table now
declares `coverage_mode=Complete`, so activation rejects any incomplete active
monster catalog; omitted coverage declarations remain `Partial` for older and
custom packs, which retain the legacy identity fallback.

The Golem spell now carries its content-owned summon monster identity and
activation validates that reference against `monster_types.tsv`; older spell
rows without the field continue to use the native compatibility fallback.

Explicit unique-monster selectors with a `monster_id` are now validated against
the active `monster_types.tsv` catalog during activation. Native-AI-only
selectors remain deferred so generated or later set-piece encounters can still
materialize them without requiring a fabricated type row.

Legacy special-identity admission fallback now lives in the spawn-policy
catalog, leaving the planner itself free of embedded Golem/Diablo identity
knowledge while preserving older-pack behavior.

The monster-type catalog now provides unambiguous native-AI lookup for legacy
compatibility paths. The Golem summon uses that lookup instead of hard-coding
native monster ID 109; packs with duplicate native-AI labels fail closed rather
than selecting an arbitrary identity.

Native monster summon selectors now also fail closed when a composed catalog
maps one native monster ID to multiple stable identities. Numeric stable-ID
fallback remains available only when the native selector is absent, preserving
legacy compatibility without allowing variant overlap to select the wrong row.
The same resolver now guards authored `SpawnNativeMonster` follow-ups during
content validation and command admission, so an ambiguous selector cannot
advance quest state before failing at materialization.

Native monster-table identities are now bounded to DevilutionX's 200-entry
compatibility table (0–199) in both `monster_types.tsv` and native DUN mappings.
Raw DUN conversion-layer identities retain their separate 1–157 bound.

Native `.til` d-piece references and `.sol`/TSV d-piece properties are now
bounded to DevilutionX's `MAXTILES` range (0–1378), preventing external
collision bundles, level projections, or authored mutations from carrying
unrepresentable piece identities. Authored transparency and mega-tile mutation
values now enforce their native byte widths too; signed transparency values are
carried as their low-byte wire representation for parity with the native DUN
loader.
The Godot protocol model now enforces the same `MAXTILES` d-piece bound on
authoritative `native_piece_layer` snapshots.

Authoritative level geometry now also carries the cell-aligned native DUN
monster layer through the shared protocol, with the native, server, and Godot
validators enforcing its 0–157 compatibility range.

Procedural level definitions now enforce the native 112x112 fine-grid ceiling
at content validation time, preventing rows that would only fail later during
world materialization.

The ordinary procedural-monster ceiling is now content-owned through the
optional `ordinary_monster_capacity` level-generation field. Values are bounded
to the native 200-slot range, while omitted fields preserve the default 190-slot
ordinary limit and keep older packs loadable.

The Godot geometry projection now preserves server-authored missile-only
collision cells separately from walk-blocked cells and renders a deterministic
presentation cue for that occlusion layer.

The shared replay contract now covers player Telekinesis world-item pickup,
object activation, and monster knockback. Native and managed runners preserve
target validation, inventory placement, authored object mutation, one-cell
knockback, authored object experience effects, memorized-spell admission, mana
consumption, and canonical state hashing. Rejection fixtures also preserve the
world item and player resources for invalid targets, insufficient mana, and
unmemorized spell sources.

The six replay fixtures now load Telekinesis from the validated base `spells.tsv`
catalog instead of a synthetic test-only spell definition.

The Magi, Mana, Reflect, Etherealize, and Berserk fixtures now load their
spell behavior from the validated base `spells.tsv` catalog instead of synthetic
test-only rows.

The Firebolt, Fireball, and Bone Spirit PvP fixtures now load their
spell behavior from the validated base `spells.tsv` catalog instead of synthetic
test-only rows.

The Rage, Inferno, and Apocalypse fixtures now load their spell behavior from
the validated base `spells.tsv` catalog instead of synthetic test-only rows.

The five standard Charged Bolt fixtures now load their spell behavior from the
`test/content/replay/charged-bolt` validated pack; the managed runner validates
the pack manifest hash before constructing the authoritative catalog.

The Bone Spirit homing and Hellfire Firebolt/Fireball fixtures now load their
spell behavior from the validated `test/content/replay/projectile-variants` pack; the
managed runner validates its canonical manifest hash.
Hellfire Charged Bolt now reuses the validated `test/content/replay/charged-bolt`
pack, and the status-expiry fixture now loads its row from the validated
`test/content/replay/status-expiry` pack; both manifests are hash-checked before
catalog activation.

The three Mega Inferno replay fixtures now share the validated
`test/content/replay/mega-inferno` pack, which externalizes both the Inferno
controller spell row and its native `Mega`/`InfernoControl` missile mapping.
The managed runner validates the composite pack hash before loading either
catalog.

The eight Blood Star replay fixtures now share the validated
`test/content/replay/blood-star` pack, which externalizes the Succubus and
Lazarus Succubus `BloodStar` missile mappings. The managed runner validates the
pack hash before loading the native-AI projectile catalog.

The seventeen Monster Magic replay fixtures now share the validated
`test/content/replay/monster-magic` pack, which externalizes the `OrangeFlare`
mapping for Lich-family and orb native AIs. The managed runner validates the
pack hash before loading the native-AI projectile catalog.

The eleven Monster Arrow replay fixtures now load validated GoatRanged and
SkeletonRanged mappings from the shared `test/content/replay/monster-arrow`
pack and the admission-specific `test/content/replay/monster-arrow-admission`
pack. Both native and managed runners validate their manifest hashes before
loading the projectile catalog.

The nine Hork Spawn fixtures now load their validated `HorkDemon` mapping from
`test/content/replay/monster-hork-spawn`. The two Snake delay fixtures and the
Snake Charge lifecycle fixture likewise load separate validated
`monster-snake-delay` and `monster-snake-charge` packs, preserving the native
default and explicitly tuned charge configurations.

The Hork Spawn pack now also supplies the summoned monster’s validated
`monster_types.tsv` row; the managed spawn planner consumes that external
metadata instead of an inline test definition.

Its `level_generation.tsv` rule is externalized as well, so the Hork replay
planner no longer constructs encounter-generation metadata inline.

The autonomous GoatRanged Arrow fixture now loads the validated
`test/content/replay/monster-arrow-autonomous` pack. Its compact native replay
projection accepts the same explicit retreat thresholds as managed content,
while omitted thresholds retain the native 4/70/10 defaults.

The basic healing spell fixture now loads spell ID 1 from the validated
`test/content/replay/spell-cast` pack instead of the simulation’s synthetic
fallback catalog.

The Counselor delay and retreat fixtures now load their validated native
Counselor type and AI timing bands from the shared
`test/content/replay/counselor` pack instead of relying on compatibility
fallbacks.

The GoatMelee movement and adjacent-attack fixtures now load their validated
native AiAvoidance and wounded-special tuning from the shared
`test/content/replay/goat-melee` pack instead of relying on compatibility
fallbacks.

The five Gargoyle healing, movement, avoidance, and adjacent-attack fixtures
now load their validated native safety, healing, and AiAvoidance tuning from
the shared `test/content/replay/gargoyle` pack instead of relying on
compatibility fallbacks.

The two SkeletonMelee delay fixtures now load their validated native chance
bands and movement/attack delay formulas from the shared
`test/content/replay/skeleton-melee` pack instead of relying on compatibility
fallbacks.

The Bat retreat, Fat movement, and Zombie action fixtures now load their
validated native AI tuning from the shared `bat-retreat`, `fat-movement`, and
`zombie-action` packs instead of relying on compatibility fallbacks. Their
replay state now carries explicit stable monster IDs so content lookup is
server-authoritative and deterministic.

The Rhino charge and Diablo/Hellfire Scavenger corpse fixtures now load their
validated charge, movement, delay, and healing tuning from the shared
`rhino-charge` and `scavenger-corpse` packs instead of relying on compatibility
fallbacks. Their replay states also carry explicit stable monster IDs.

The Sneak conceal/reveal fixtures now load their validated stealth distance and
retreat tuning from the shared `sneak` pack instead of relying on compatibility
fallbacks. Both replay states carry an explicit stable monster ID.

The elemental equipment and ranged-arrow replay fixtures now identify the
canonical `devilution.base` manifest and assert its current validated hash
before executing their shared equipment-channel behavior.

The shared replay contract now covers the native Counselor AI delay and
retreat lanes. Native and managed runners preserve server-owned adjacent
decision delay, wounded retreat movement, retreat countdown state, and
canonical monster hashing.

The shared replay contract now covers player Town Portal casts. Native and
managed runners preserve dynamic portal endpoints, ownership, bidirectional
lifetime state, mana consumption, and canonical portal hashing.

The shared replay contract now covers the Jester dispatch lane. Native and
managed runners preserve deterministic dispatch RNG, nested Firebolt projectile
creation, one-time mana consumption, and canonical projectile state hashing.

The shared replay contract now covers player Golem summons. Native and managed
runners preserve server-owned minion identity and ownership, native summon
stats and spawn seeds, death-animation timing, mana consumption, and canonical
monster state hashing.

The shared replay contract now covers player Berserk casts. Native and managed
runners preserve eligible-target admission, native four-channel damage scaling,
recast rejection, and canonical berserked-monster state across ticks.

The shared replay contract now covers player Warp transitions. Native and
managed runners preserve normalized trigger resolution, deterministic
destination selection, visited-level history, mana consumption, and canonical
level-transition state.

The Warp, Golem, Jester, and Town Portal fixtures now load their spell behavior
from the validated base `spells.tsv` catalog instead of synthetic test-only rows.

The shared replay contract now covers player Etherealize casts. Native and
managed runners preserve the permanent status, canonical status hashing, and
monster-damage immunity across authoritative ticks.

The shared replay contract now covers player Reflect casts and retaliation.
Native and managed runners preserve level-scaled charges, canonical Reflect
state, charge consumption, reflected monster damage, and gameplay RNG parity.

The shared replay contract now covers player Guardian casts. Native and
managed runners preserve turret admission, the deterministic native firing
arc, Firebolt creation, controller state, and projectile lifetime. The compact
fixture currently uses timeline checkpoints without snapshot hashes because
the native replay projection intentionally remains narrower than the full
managed monster snapshot; the replay contract now accepts those projection-
only checkpoints explicitly in both runners.

The shared replay contract now covers the Hellfire Rage controller. Native
and managed runners preserve active/cooldown timing, duplicate-cast
suppression, and the bounded Q6 self-damage on expiry.

The shared replay contract now covers player Inferno casts. Native and managed
runners preserve the moving controller, deterministic three-segment emission,
and persistent segment lifetimes.

The shared replay contract now covers player Apocalypse casts. Native and
managed runners preserve row-major target scanning, physical boom emission,
target impact, and boom lifetime.

The shared replay contract now covers player Magi and Mana casts. Native and
managed runners preserve full-mana restoration and seeded, class-scaled mana
restoration.

The shared replay contract now covers player Elemental casts. Native and
managed runners preserve the retargeting projectile lifecycle, fire-burst
timing, and canonical checkpoints.

The shared replay contract now covers player Immolation casts. Native and
managed runners preserve the one-tick controller, deterministic radial fan
ordering, Fireball movement, and canonical checkpoints.

The shared replay contract now covers player Chain Lightning casts. Native and
managed runners preserve the one-tick controller, deterministic native crawl
fan-out, Lightning bolt movement, impact segments, and canonical checkpoints.

The shared replay contract now covers player Flame Wave casts. Native and
managed runners preserve the one-tick controller, deterministic center/branch
geometry, per-segment damage, same-tick movement, and canonical checkpoints.

The shared replay contract now covers player Nova casts. Native and managed
runners preserve the one-tick controller, native 36-ball radial fan ordering,
first-tick movement, and canonical projectile checkpoints.

The shared replay contract now covers player Lightning Wall casts. Native and
managed runners reuse the Fire Wall geometry controller while preserving the
native lightning damage, duration, and presentation identity.

The shared replay contract now covers player Ring of Fire casts. Native and
managed runners preserve the native radius-three crawl, deterministic segment
creation, persistent Fire Wall state, and canonical checkpoints.

The Lightning Wall, Ring of Fire, Nova, and Flame Wave fixtures now load their
spell behavior from the validated base `spells.tsv` catalog instead of synthetic
test-only rows.

The Chain Lightning, Immolation, Elemental, and Guardian fixtures now load
their spell behavior from the validated base `spells.tsv` catalog instead of
synthetic test-only rows.

The shared replay contract now covers player Fire Wall casts. Native and
managed runners preserve the seven-tick wall controller, deterministic branch
growth, scaled persistent segment state, and canonical checkpoints.

The shared replay contract now covers seeded Succubus/Lazarus BloodStar
close retreat and long-range admission. Native and managed runners consume
the restored AI stream for retreat direction, RandomWalk fallback, and
post-shot delay, then agree on canonical checkpoints.

The shared replay contract now covers Mega InfernoController admission and
the three-step moving fire-stream lifecycle. Native and managed runners
preserve segment lifetime, target selection, and Mega recovery delay through
the same canonical checkpoint.

The Mega replay lane also covers seeded multiplayer execution, including
native AI-stream advancement and stream-driven recovery delay.

The shared replay contract now covers seeded GoatRanged close retreat and
long-range arrow admission. Native and managed runners consume the restored
AI stream for retreat direction, RandomWalk fallback, and post-shot delay,
then agree on the canonical checkpoints.

The shared replay contract now covers seeded SkeletonRanged arrow admission.
Native and managed runners consume the same native AI stream, preserve the
updated monster seed, and agree on the canonical projectile checkpoint.

The shared replay contract now covers SkeletonRanged close-range retreat.
Native and managed runners preserve nearest-participant selection,
deterministic retreat destinations, movement counters, and suppression of
same-tick MonsterArrow admission.

The shared replay contract now carries GoatRanged ranged-delay state through
the native and managed projections, verifying delayed admission before the
subsequent secondary-targeted MonsterArrow checkpoint.

The shared replay contract now covers GoatRanged close-range retreat. Native
and managed runners preserve nearest-participant selection, deterministic
retreat destinations, and suppression of same-tick arrow admission.

The shared replay contract now covers GoatRanged multiplayer target
selection. Native and managed runners preserve nearest-participant ordering,
MonsterArrow admission, and canonical checkpoint parity while keeping ranged
monsters out of the generic melee-pursuit branch.

Multiplayer autonomous target-selection replay fixtures now hydrate secondary
participants from the shared fixture on both runners, restoring canonical
checkpoint parity for HorkDemon, Succubus, Lich, and SkeletonRanged lanes.

The shared replay contract now covers Snake autonomous-AI delay admissions:
native and managed runners carry Snake's distant-delay and failed
adjacent-attack countdowns through the same canonical replay checkpoints.

The shared replay contract now also covers the server-owned SnakeCharge
projectile lifecycle: admission, travel, player collision, impact timing, and
canonical checkpoint parity are exercised by both runners.

The shared replay contract now covers native Sneak conceal/reveal transitions
and the Bat retreat lifecycle. Native and managed runners preserve visibility,
retreat movement, countdown state, and canonical checkpoint parity.

The shared replay contract now covers the server-owned Scavenger corpse goal.
Native and managed runners preserve corpse selection, movement, Q6 healing,
countdown state, and the native gameplay-RNG draw across the lifecycle.

The Scavenger replay lane now also covers Hellfire's max-health-fraction
healing and corpse removal when the monster reaches full health.

The Scavenger replay lane now also covers native coin-directed selection among
multiple visible corpses. The compact native replay controller and managed
runner preserve the same scan order, selected corpse identity, movement, and
canonical checkpoints.

The shared replay contract now covers the server-owned ZombieAi action band
and deterministic long-range walk, including current/future occupancy parity
across the following tick.

The shared replay contract now covers the server-owned Fat/Overlord movement
band, including its fresh movement mode, idle counter, and occupancy state.

The shared replay contract now covers the server-owned GoatMelee AiAvoidance
movement band, deterministic RandomWalk turn ordering, and occupancy state.

The shared replay contract now covers the server-owned GoatMelee adjacent
attack boundary, including seeded admission, block timing, and AI-stream state.

The shared replay contract now covers the shipped LazarusSuccubus BloodStar
alias, confirming its authoritative projectile mapping in both runners.

The shared protocol now admits an additive `DropItemRequested` intent carrying
the same stable item references used by inventory and stash transfers. The
authoritative server validates and removes the referenced carried item only
after checking the adjacent destination, while retaining the native
cursor-only `DropHeldItemRequested` compatibility command. This closes the
Godot protocol gap for item-placement interactions and exercises the
content-owned Cornerstone anchor path. Godot inventory selection now routes
world clicks through the same stable-reference intent, while native cursor
input remains backward compatible. A live Godot/server fixture now verifies
inventory-source removal, authoritative world-item projection, and stale
reference rejection over the transport boundary. The native authoritative
client now submits the same stable-reference intent for carried cursor drops;
legacy mode and stash selections retain fail-closed compatibility behavior.

The shared replay contract now covers the server-owned Rhino charge boundary.
Native and managed runners agree on charge admission, nearest adjacent landing,
special damage, monster relocation, player knockback, and unchanged
single-player RNG in one authoritative tick.

The shared replay contract now includes a stable-reference inventory-drop
fixture. Native and managed runners both validate the stable identity, remove
the inventory source, and create the authoritative adjacent world item,
extending parity coverage for the protocol path behind the native and Godot
clients.

The replay contract now also carries validated equipment for secondary
participants. A shared lethal MonsterArrow fixture drives the secondary player
through the authoritative monster-death path, including Gold halving,
facing-ordered equipment world drops, and canonical participant hashing in both
runners. Native secondary projectile impacts now enter death mode and apply the
same drop suppression and item-seed rules as the managed simulation.

Generic autonomous monster melee now selects the nearest live participant in
both runners, with stable entity-ID tie breaking. The shared secondary-melee
fixture verifies that a closer secondary participant is attacked while the
primary remains untouched. The same native crawl now has the managed bounded
breadth-first fallback, preserving participant and active-object occupancy when
direct approach tiles are blocked.

Replay fixtures now carry paired native monster item/AI runtime seeds. The
multiplayer AI stream advances once per authoritative multiplayer tick, and
generic secondary melee consumes that stream for hit and block rolls. The
cross-language native-AI-seed fixture verifies the resulting block mode,
timing, seed advancement, and canonical checkpoint hash.
The companion impact fixture also verifies successful min/max damage draw
consumption and post-impact RNG-state parity. The default native client build
now keeps those opt-in runtime references behind the server-backed feature
guard, allowing the replay target to build without that feature enabled.

The shared replay gate now covers the wounded Gargoyle self-heal boundary.
Both runners consume the same native divisor draw, apply the resulting Q6
healing, retain the special lock while the player remains outside the native
safety distance, and preserve the lock across the next autonomous tick.
The companion adjacent-attack fixture now verifies Gargoyle's native
intelligence-scaled attack admission before hit/block resolution, including
secondary-player block timing and multiplayer AI-stream advancement.
The healthy non-adjacent avoidance fixtures also verify the native action draw,
persisted avoidance counter when no walk is admitted, and the admitted
RandomWalk candidate draws, destination, and participant/object occupancy
state. The far-distance Move-goal fixture also preserves its admission coin,
turn initialization, persisted goal tick, RoundWalk destination, and the
next-tick continuation boundary. Companion SkeletonMelee fixtures now cover
native movement-delay admission, failed adjacent-attack admission, and both
countdown lifecycles across their following ticks.

Multiplayer replay coverage now also routes an explicit stable-reference drop
from a secondary participant. The new fixture validates secondary inventory
source removal, adjacent world-item projection, and a shared canonical
checkpoint hash in both runners.

The companion multiplayer replay lane now routes `PickupWorldItem` from the
secondary participant. Native and managed runners agree on world-item removal,
inventory placement, purchased-tick propagation, and the resulting canonical
checkpoint hash.

Dedicated replay fixtures now cover stable-reference drops from inventory,
belt, and equipment sources, matching the three carried-item locations exposed
by the protocol boundary. Command-level expected statuses are now part of the
cross-language replay contract; the accepted-then-rejected stale inventory
drop fixture proves both runners preserve authoritative source removal while
rejecting a retry that references the consumed stable identity, including the
protocol's `InvalidTarget` reason. A companion belt-source lane verifies that
valid identity with an illegal destination is rejected as `NotAllowed` without
removing the source.

Initial staged-dialogue projection now chooses a shared-safe message for
item-gated encounters. When a conversation has item-present and item-absent
branches, snapshots advertise the item-absent branch; the server still selects
the interacting player's exact branch at `TalkToQuestRequested`. This keeps
multiplayer target presentation deterministic without leaking item-dependent
reward state. The Lachdanan boundary is covered by catalog and simulation
tests, plus a live two-participant Godot/server fixture that composes the
shipped Hellfire pack and validates the same branch through both protocol
snapshots. Native map geometry remains separately deferred.

Snotspill's initial unique-monster conversation now enters through the authored
staged-dialogue catalog instead of the legacy quest-dialogue fallback. The
live Godot fixture verifies the server event, quest-variable advance, next
projected message identity, and rejection of direct combat while the authored
stage is pending. The same initial snapshot now projects the authored message
identity, allowing Godot's target selector to resolve the dialogue before the
first interaction. Snapshot restore reapplies the same projection after quest
state hydration, including saves created before the projection was introduced.
The active content schema is now `1.449.0`, with `1.448.0` accepted as the
previous additive contract.

The Golem spell now names its summon target with the stable
`golem_monster_symbol` field. The shipped row resolves
`golem_native_109` to stable monster ID 111 and native selector 109; numeric-only
spell rows remain accepted for older packs.

The authored object follow-up boundary is now covered end to end through the
shared protocol. A live Godot fixture activates a quest-transition object,
verifies its quest state and activation acknowledgement, and observes the
server-persisted world-mutation cell in the projected level geometry. Both
protocol mappers and the Godot model accept world-mutation, projectile, and
encounter follow-ups on quest-transition objects; schema `1.449.0` remains
current with `1.448.0` accepted for additive compatibility.

The Halls of the Blind, Gateway of Blood, and Warlord steel-tome object
templates now carry authored activation-message identities. Server object
operations record the message on the linked quest and emit the dialogue event,
removing native quest speech from the authoritative path. Their native-DUN
placement and map mutations remain deferred until the licensed geometry is
available. Quest-transition objects can now also apply an authored follow-up
mutation or encounter after the transition succeeds, providing the server-side
hook needed by those future set-piece replacements without fabricating geometry.
The base pack also carries presentation metadata for all three templates: the
objects remain visible after activation and select the native open-book frame.
The content schema is now `1.449.0`; the previous additive `1.448.0` schema
remains accepted for pack compatibility.

The deterministic replay gate now includes an occupied-inventory
secondary-participant world-item pickup fixture. Native and managed runners
preserve the existing item, choose the same first-fit destination cell, and
match the resulting canonical participant hash.

The same replay lane now covers a full secondary inventory and cursor lifecycle.
Both runners accept the pickup into the secondary held-item projection, then
route `DropHeldItem` to adjacent authoritative world placement with matching
entity identity and canonical participant hash.

The secondary multiplayer replay lane now also covers Gold pickup. Native and
managed runners add an adjacent Gold pile directly to an existing secondary
wallet, remove the world entity, and match the canonical participant hash
without creating a presentation-only inventory stack.

The same lane now rejects malformed secondary Gold identities as `Malformed`.
Both runners preserve the wallet, cursor, and world item on rejection, keeping
invalid content fail-closed across the protocol boundary.

The lane also rejects a valid secondary Gold pickup that would exceed the
signed protocol Gold range, preserving participant and world state in both
runners.

Replay rejection bookkeeping now records malformed primary Gold pickups as
well, keeping accepted/rejected command timelines complete for both
participants.

Primary cursor pickup now has an explicit occupied-tile rejection lane. Native
and managed runners record `NotAllowed` consistently and preserve both the held
cursor item and the world item.

The complementary accepted cursor-replacement lane now also has a shared
checkpoint. Both runners drop the held item at the pickup origin and place the
adjacent world item in inventory with matching entity and identity state.

The shared replay schema now carries an optional secondary cursor item. Native
and managed multiplayer runners use it to verify accepted secondary pickup
replacement, including authoritative origin-drop placement and participant
checkpoint parity.

The secondary cursor boundary now also rejects pickup on an occupied origin as
`NotAllowed`, preserving both items. Secondary movement updates the native
facing projection so movement-then-rejection checkpoints remain cross-language
identical.

Hellfire Lachdanan's opening and Golden Elixir hand-in now use paired authored
unique-monster dialogue stages. The server selects the item-present or
item-absent branch, preserves native messages 81/83 and `_qvar1/_qvar2`,
validates the Veil of Steel unique reward placement, consumes the Elixir, and
applies the native dialogue defeat boundary. The legacy dialogue item/reward
fields are empty in the shipped row. The stage rows remain compatible with the
current `1.449.0` contract; `1.448.0` remains accepted as the previous additive
schema.

Zhar's activation conversation now uses an authored unique-monster dialogue
stage with a validated normal-item reward. The server checks the target tile
before advancing `_qvar1` from 0 to 2, emits native message 148, and generates
the level-14 book from content; the legacy dialogue reward fields are empty in
the shipped quest row. The stage row remains compatible with the current
`1.449.0` contract; `1.448.0` remains accepted as the previous additive schema.

The Lazarus storyteller opening now uses authored quest/towner stages. The
single-player Staff of Lazarus hand-in consumes its item and advances native
`_qvar1` to 2, while the multiplayer unlogged opening dialogue records the
same native message without an item; the existing completion stage remains
authored. Those rows were introduced under schema `1.438.0` and remain
compatible with the current `1.449.0` pack contract, which also accepts
`1.448.0` as its previous additive schema.

The base Black Mushroom healer hand-in now uses an authored `Advance` stage.
The server validates the Spectral Elixir placement before consuming the Brain,
then advances native `_qvar1` from 5 to 6, emits message 124, and creates the
declared reward. Those rows were introduced under schema `1.436.0` and remain
compatible with the current `1.449.0` pack contract, which also accepts
`1.448.0` as its previous additive schema.

The remaining shipped base item hand-ins now use authored quest/towner rows:
Magic Rock and Anvil of Fury completion, Ogden's Sign activation and return,
and the Butcher activation preserve their native town coordinates, variable
gates, messages, item consumption, and quest-declared rewards. Those rows were
introduced under schema `1.435.0` and remain compatible with the current
`1.449.0` pack contract. Legacy quest-state turn-in fields remain
available to older packs and direct compatibility callers.

The generic host now persists each initialized session's completed command
receipt ledger beside its authoritative save and restores it before handshake
processing. A client retry therefore remains a duplicate across a fresh host
process, preserving server-owned exactly-once command admission through the
reconnect boundary.

The fresh-host recovery fixture now replays every accepted command through four
replacement host/adapter cycles. Each durable receipt retains its original
server sequence, while the next client sequence is executed once by the new
adapter and then remains a duplicate on later restarts.

State and receipt exports now run under the command server's synchronization
boundary. Concurrent command handlers and autonomous ticks cannot capture a
snapshot and command ledger from different authoritative moments.

The host now publishes an atomic `<session>.session.json` checkpoint containing
both the authoritative state and completed command receipts. Cold hosts prefer
that checkpoint and fall back to the legacy separate files when it is absent,
closing the crash window between the two compatibility publications.

The content-owned quest/towner interaction catalog now covers the shipped
Hellfire Grave Matters dialogue, Farmer's Orchard activation and Rune Bomb
reward, and Jersey's Jersey alternate Grey Suit dialogue plus Brown Suit
completion. Activation stages may reuse their quest-declared normal-item reward;
legacy quest-state turn-in handling remains available for older packs and direct
compatibility callers.

The additive `CompleteAfterQuest` interaction kind now externalizes the shipped
Poisoned Water, Lazarus, and Farmer's Orchard post-completion conversations.
Completion rows carry their native variable gates and markers, optionally spawn
the quest's completion reward, and activate follow-on quests under the same
server-owned one-shot boundary. Older peers retain the legacy completion-stage
fallback when the additive field is absent.

Hellfire Little Girl now uses authored quest/towner stages for its opening,
active dialogue, and pre-activation Theodore hand-in branches. The itemless
stages require Theodore to be absent, while the completion stage is explicitly
allowed during initial quest state and reuses the quest-declared amulet reward;
the authoritative visited-level gate and native messages 283/284/285 are
preserved. A live Godot protocol fixture now verifies that the standalone
Hellfire server projects the pre-activation completion metadata and reward
contract unchanged.

The base content pack now includes template-only authoritative behavior rows
for the Halls of the Blind book, Gateway of Blood book, and Warlord steel tome.
Their native identities, initial quest-variable transitions, and Halls
optic-amulet/Gateway Blood Stone rewards are validated by the module-owned
shipped-content test. Native-DUN placement and Gateway's post-activation
mega-tile projection are now active; licensed collision replacement remains
deferred pending the native geometry bundle.

Native DUN fragments and fragment-object placements now accept validated
set-level-origin anchors. The server defers those rows until the corresponding
generated origin is published, then composes the fragment and its behavior
objects atomically; existing entity, unique-encounter, and coordinate anchors
remain backward compatible.

World mutations now accept the same validated set-level-origin anchor. Geometry
and native layer changes defer until the referenced generated origin is
published, then rebuild through the authoritative world projection.

Dynamic origin publication and snapshot rollback now restore origins, generated
objects, native fragments, and projected geometry together when deferred
materialization or validation fails. Snapshot restore also checkpoints the
complete module state before applying a save and restores that checkpoint after
late validation or materialization failures.

Snapshot restore now hydrates saved set-level origins before replaying deferred
native DUN fragments. This rebuilds set-level fragment geometry on a replacement
session even when the origin was not preloaded, and accepts the matching
persisted behavior objects without duplicating their entity IDs; conflicting
object state still fails closed.

Save and ruleset-import boundaries now discard queued transient simulation events
before resuming the restored tick. Events are not persisted, so reconnects and
live replacement cannot deliver stale combat or dialogue notifications from the
pre-restore timeline.

Godot live protocol tests now launch the authoritative server from the same
Debug or Release output selected for the test assembly, avoiding stale
configuration binaries during boundary verification.

Live Godot overlay fixtures now declare the active `1.449.0` content schema,
matching the shipped base pack. Manifest composition continues to reject a
mixed current/previous schema set before module activation; the Diablo module
still accepts the previous additive `1.448.0` schema when a complete manifest
uses it.

Godot local movement prediction now stops at authoritative blocked cells and
known live monster or remote-player occupancy while retaining bounded queued
input for cells whose collision state is not yet projected. This keeps the
presentation responsive without allowing the client to speculate through
server-known blockers.

Godot now consumes complete authoritative native d-piece layers as a
deterministic diagnostic overlay. The overlay distinguishes ordinary native
geometry, walk-blocked cells, missile-only blockers, and transparency regions
without moving any collision or gameplay rule into the client; the existing
floor presentation remains the fallback until licensed native tile art is
available.

The native compatibility client now retains the server's content and ruleset
identity from complete authoritative snapshots and reuses refreshed values on
reconnect, so live ruleset replacement is visible to both presentation clients
while malformed paired metadata fails closed at the transport boundaries.

The multiplayer death-suppression replay checkpoint now matches the canonical
snapshot hash produced after the accumulated additive snapshot fields; the
complete replay fixture class validates all 79 cases, including a secondary
participant portal transition and visited-level projection.

The engine-neutral entity pool now initializes sparse free-lists before any
game-specific reset hook, rejects duplicate releases, and correctly packs
dense-policy storage after deletion. The native compatibility adapters retain
their legacy bookkeeping contract, documented in the entity-pool guides, while
focused GTest coverage protects the container behavior used by future
presentation-pool extraction.

The shared secondary-portal replay now captures a complete initial participant
set and verifies reverse-order save restoration, preserving the secondary
participant's visited-level history across a reconnect-style reconstruction.

The native Lua compatibility boundary characterization now tracks the
centralized lifecycle policy directly: both startup and shutdown remain guarded
for authoritative mode, while the debug-console registration retains its
explicit native-only mode comparison.

The generic host now exposes the active combined ruleset identity and retired
generation count through the live server status surface. Operators can verify
that a replacement was published and that old generations were drained without
reaching into game-specific adapter state.

Production Diablo activation now rejects a present `native_spells.tsv` table
that omits any native compatibility ID from 0 through 51; partial catalogs
remain available only to explicit compatibility callers.

Production Diablo activation now also requires externally authored ordering
patterns for every native projectile behavior that consumes one (Charged Bolt,
Fireball, Elemental, Flash, Guardian, and Resurrection). Compatibility callers
retain the embedded native defaults when constructing catalogs directly.

Production activation now requires the complete external native missile
namespace (all 68 native `misdat.tsv` identities) and its sprite catalog as a
pair whenever native missile or monster-missile mappings are present; partial
namespaces, partial base sprite catalogs, and missing tables no longer silently
skip reference validation. Hellfire overlays may add their 13 variant sprite
identities on top of the required 46 base definitions.

Persisted item states that carry an item seed but omit a randomized armor value
now restore the native inclusive armor roll consistently through item metadata
hydration, snapshots, stores, world-item pickup, and equipment-stat
calculation. Seedless compatibility states remain unresolved until their
authoritative generator supplies the rolled value.

Overflow-safe Chebyshev grid-distance metrics now live in the engine-neutral
assembly and are reused by bounded navigation and Diablo's authoritative
targeting paths; the Diablo compatibility entry point preserves the same
extreme-coordinate behavior.

Deterministic overflow-safe Bresenham line traversal now also lives in the
engine-neutral assembly. Authoritative movement and missile line-of-sight
checks share its endpoint-exclusion and interior-blocking contract, and a
bounded step limit rejects malformed requests before an unbounded walk.

Native-compatible signed 26.6 fixed-point arithmetic now lives in the
engine-neutral assembly as a reusable primitive. `DiabloFixedPointQ6` remains
a source-compatible facade so Diablo hit-point and damage callers preserve the
same checked whole-value conversion and signed truncation/ceiling semantics.
Authoritative Diablo callers now use `DeterministicFixedPointQ6` directly for
hit-point, damage, healing, and snapshot arithmetic; the facade is retained
only for existing compatibility consumers.

The native-compatible deterministic LCG now lives in the engine-neutral
assembly as a reusable primitive. `Devilution.Diablo.DiabloRandom` remains a
source-compatible facade, preserving existing gameplay and replay callers
while keeping deterministic infrastructure outside the game module.
The engine primitive now also owns the compatibility-shaped bounded-roll and
spawn-seed aliases. Authoritative item, world, store, simulation, spawn, quest,
spell-book, and unique-item code types and constructs it directly; both Diablo
facades are retained only for compatibility consumers and existing fixtures.

Stable non-zero entity-ID allocation now follows the same boundary: its
implementation lives in `Devilution.Engine`, and the TCP host now allocates
directly from that primitive while retaining its source-compatible
`StableEntityIdAllocator` facade for existing callers.

The Server Diablo protocol mapper and replay fixture now likewise consume the
engine fixed-point and legacy-random primitives directly; their Diablo-facing
wire and fixture contracts remain unchanged.

Monster aggro and retreat selection, scavenger/fallen radius checks, minion
targeting, projectile retargeting and area resolution, and native light-radius
vision now use the shared widened Chebyshev arithmetic. One-cell player movement
and Telekinesis knockback also reject coordinate-boundary wraparound before
consulting geometry or mutating authoritative state.

Authoritative monster unity and spawn spacing, world interaction adjacency,
and combat cast/target range gates now share widened Chebyshev distance
arithmetic. Extreme integer-boundary coordinates therefore fail closed
without wrapped adjacency or state mutation.

Damage-targeted casts now reject raw coordinates outside the active level
before distance arithmetic, keeping malformed client spell targets fail-closed
instead of allowing integer-overflow exceptions during authoritative admission.

Town Portal and Golem casts apply the same active-level coordinate gate before
their range checks or spawn searches, covering special casts that do not enter
the damage-target resolution branch.

Godot snapshot validation now widens monster movement deltas before checking
occupancy, so malformed coordinates are rejected as invalid snapshots rather
than surfacing an integer-overflow exception before the coordinate gate.

The shipped Etherealize content row now resolves to native spell identity 25
(`Etherealize`) instead of 26 (`ItemRepair`), keeping external spell identity
mapping aligned with the inherited native catalog.
Shipped-content verification now cross-checks every external spell behavior row
against that native identity namespace.

Godot interaction-range and quest/towner selection helpers now widen raw
coordinate deltas before taking absolute values, so extreme malformed snapshots
cannot overflow or throw while presentation commands are being selected.
Monster visibility distance checks and timed walk interpolation likewise widen
coordinate arithmetic before squaring or interpolating, preserving stable
fail-closed presentation behavior at the protocol boundary.

The engine-neutral bounded navigation helper now skips neighbor offsets that
would cross the integer coordinate boundary and widens target-distance checks,
so caller-owned grid predicates cannot observe wrapped cells during a bounded
search.

Shared bounded crawl searches now skip candidate coordinates that overflow the
integer domain, so raw client target cells fail closed across teleport, stone
curse, wall, portal, and berserk placement paths instead of faulting a session.

Held-item drop intents now reject out-of-level target coordinates before
computing adjacency distance, keeping the direct client target path fail-closed
for malformed coordinates in both projected and fallback worlds.

Authoritative generic and fallback line-of-sight checks now reject endpoints
outside the validated level before calculating coordinate deltas, preventing
malformed remote targets from overflowing arithmetic or causing an unbounded
grid walk in either projected or compatibility geometry.

Warp trigger lookup now widens coordinate deltas before squaring, preventing
32-bit wraparound from changing the selected authoritative route for malformed
caller coordinates.

Authoritative world registration now rejects duplicate source warp trigger
coordinates instead of silently selecting one route by numeric ID; authored
level transitions therefore remain unambiguous at content activation time.

Set-level map validation now fails closed when any declared native binary is
present without the complete `.dun`/`.til`/`.sol` bundle (or declared
transparency layer), while rows with no supplied binaries remain metadata-only.

Set-level quest entry state is now enforced across world commands, simulation
ticks, store/combat entry, and snapshot restoration, so a player already on a
materialized active set-level cannot bypass native QUEST_INIT activation.

The generic host now rejects duplicate visited-level IDs before snapshot
serialization, aligning the authoritative boundary with the existing Godot
and native compatibility validators and preventing ambiguous quest-gate state.

Character progression now consumes a validated external `experience.tsv`
catalog in activated content, preserving all 50 native cumulative thresholds;
programmatic compatibility callers retain their legacy fallback until they
provide an active catalog.

The native `misdat.tsv` missile identity namespace is now externalized as
validated `native_missiles.tsv` content. Monster missile mappings must resolve
through those 68 native identities plus five explicitly declared monster-color
compatibility aliases during activation, preserving source-owned visual, sound,
handler, movement, and expiry-link metadata for both server and presentation
bridges.

The native `missile_sprites.tsv` contract is now externalized as validated
`native_missile_sprites.tsv` content, preserving all 46 base sprite definitions
and the 13 Hellfire-only overlay definitions, including dimensions, names,
direction counts, flags, and 16-direction frame timing for presentation
clients. Every non-alias native missile graphic must now resolve through that
active sprite catalog during content activation, and sprite flags are checked
against the native vocabulary.

Content-owned native missile identities now also cross the shared protocol as
an additive field for monster-magic projectiles. Godot prefers the validated
identity when selecting presentation, while empty values preserve the enum and
behavior fallbacks required by older snapshots.

Entering a materialized active set-level now applies the same external
route-activation metadata as its native `LoadSetMap` counterpart. This keeps
set-level quest activation and route variable markers server-owned while
leaving pre-map variants presentation-only and preserving multiplayer quest
availability gates.

The native compatibility projection now retains and prefers that identity as
well; malformed or misplaced values fail closed before presentation, while
empty values retain the legacy native enum/behavior fallback. The connected
native presentation loop also advances projected remote-player animations
without invoking legacy gameplay simulation, while snapshot catch-up avoids
double-advancing time already consumed by the render loop. The same loop now
drives the local player's server-declared action animation without re-entering
legacy `ProcessPlayers` mutation, and resets that presentation state across
authoritative level changes.
Authoritative local equipment and class changes now also refresh the native
sprite family and animation data through a presentation-only path; inventory
and gameplay statistics remain snapshot-owned.
Authoritative local combat-mode snapshots now also reconcile the native block
animation admission flag, preventing stale legacy block state from surviving
an action transition.
Remote-player presentation uses the same native `PM_BLOCK` identity rather than
an independent numeric literal, and its regression coverage verifies block state
clears on the next authoritative standing snapshot.
Remote-player snapshots also recompute the native derived life and mana
percentages, keeping HUD and party resource presentation aligned with the server.
Remote-player item hydration also rebuilds staff-charge and scroll spell masks,
so native inspection cannot retain spell entries after authoritative item changes.
The local authoritative snapshot path likewise rebuilds staff-spell availability
from server-owned equipment without invoking legacy gameplay recalculation.
The same snapshot path now rebuilds the native stash scroll-source mask, so
closing or changing an authoritative stash cannot leave stale spell sources.
Inventory and belt scroll sources remain intact during that reconciliation.
The native world presentation bridge now removes unclaimed compatibility-pool
world-item and object slots after each authoritative snapshot, clearing their
tile references so omitted legacy level entities cannot remain as connected
client ghosts. Focused coverage verifies that claimed item/object slots remain
while stale compatibility slots are removed.
Presentation mappings now also rebind safely when a native level reload reuses
a compatibility-pool slot for a different authoritative entity, preventing an
old entity mapping from deleting the newly projected presentation state.
Authoritative local and remote player projections now also derive the native
`_pLvlChanging` presentation gate from the server-declared `PM_NEWLVL` mode,
so lighting, automap, and input gates cannot retain stale transition state
across server snapshots.
The connected native loop now advances projected walk and missile sprite
animations each game tick, so authoritative current/future tiles and active
projectile visuals continue to animate without running legacy AI, movement,
combat, collision, lifetime, or regeneration logic.
Native snapshot ingress also rejects out-of-range direction metadata for
directional projectile presentations before it reaches the compatibility
renderer.
The C# server adapter rejects the same malformed directional metadata before
it serializes a protocol snapshot.
The Godot protocol client applies the same directional metadata invariant,
keeping both presentation boundaries fail-closed and behaviorally aligned.
The Godot protocol model now owns cloned validated snapshots and event history
entries, preventing mutable transport objects from changing state after the
authoritative boundary has accepted them.
The Godot transport likewise owns cloned queued commands, so caller mutation
cannot change a command retained for send or retry after it crosses the client
boundary.
The Godot handshake now also requires an authoritative snapshot immediately
after `ServerHello`; an error or unexpected payload fails closed before the
client reports itself connected.
Victory-event Diablo ranks now use the same native byte-range guard as player
snapshot ranks across the C# mapper, generic host, Godot model, and native
compatibility validator, preventing an oversized event from being silently
narrowed during presentation.
Legacy whole-unit damage values are now bounded before native Q6 conversion;
the C# host, Godot model, and native compatibility validator reject values
that would overflow the signed fixed-point range when no exact Q6 amount is
provided.
Godot command acknowledgements are now validated before transport removal of
pending commands or model presentation updates; malformed result status and
reason combinations fail closed without losing retryable commands.
Duplicate acknowledgements retain the server's original rejection reason, so
the validator preserves that valid retry outcome while rejecting other
status/reason mismatches.
The Godot handshake now also rejects unspecified or unknown server game modes,
so the client cannot enter a connected session without a valid single-player
or multiplayer execution mode.
Current player level IDs now share one protocol rule: the native 25 regular
levels plus the reserved runtime IDs for materialized set-level variants are
accepted by the Diablo mapper, generic host, Godot model, and native
projection boundaries. Regular `_pLvlVisited` entries remain limited to the
25 ordinary levels, and set-level runtime IDs are decoded to the native
`_setlevels` enum before compatibility presentation.
The generic host also rejects undefined player direction, combat-mode,
ability-kind, and ability-targeting enum values before wire serialization.
The native player application boundary now decodes reserved runtime set-level
IDs to `_setlevels` before applying them, while still rejecting unsupported
identities before narrowing ordinary IDs into the legacy 8-bit field.
The Diablo simulation state and snapshot codec now apply the same rule during
capture and restore, preserving set-level runtime identities without placing
them in the regular `_pLvlVisited` collection.
Active world-mutation metadata now has the same 4,096-entry bound at the host,
Godot, and native session-metadata boundaries.
Godot projectile presentation now snaps on authoritative level, phase, or
presentation-identity changes instead of interpolating across discontinuities.

The normalized native spell namespace now checks all 36 `spelldat.tsv` spell
identities and their ordinal native IDs, while retaining the 16 compatibility
and rune-alias entries outside the native spell-data table.

The normalized item-generation catalog now checks all eleven fields against its
native `item_generation.tsv` source row.

The normalized level-generation catalog now checks all five external rules
against the native `level_generation.tsv` ranges and tuning values.

The normalized base class catalog now checks all six native `attributes.tsv`
tables for direct stat, block, and accuracy values plus Q6-converted life and
mana tuning; migration-specific class flags remain intentionally separate.

The six external class-loadout metadata rows are now checked directly against
their native `starting_loadout.tsv` sources for ability identity, starting
spell, spell level, and gold; intentional item aliases remain covered by the
runtime loadout and stable-ID tests.

The external base prefix and suffix catalogs are now guarded exhaustively
against the inherited native tables, including row ordering and every field;
future content edits therefore fail focused shipped-content verification when
they drift from the native source.

The composed Hellfire prefix and suffix catalogs now receive the same
field-for-field guard against the native variant tables: all 86 prefixes and
98 suffixes must remain present under their validated composite keys after
overlay replacement.

The retained legacy `itemdat.tsv` fallback is also guarded field-for-field
against the inherited native table, preserving all 168 rows while normalized
item definitions continue to take precedence in active packs.

The normalized base class animation catalog is likewise guarded against each
native class animation table, covering all 27 exposed timing fields for all
six classes.

The external Hellfire monster catalog now matches native `monstdat.tsv` core
metadata for all 138 variant rows, including the 26 Hellfire-only standing
animation frame/timing pairs, previously divergent armor values, and the four
Fallen-family special attacks as non-damaging ranges. Focused shipped-content
coverage guards these native combat and presentation metadata values.

The complete external Hellfire unique catalog retains all 110 native
definitions. Native blank numeric cells on flag-style powers are intentionally
normalized to explicit `0,0` values; populated ranges remain source-faithful,
with focused coverage guarding both forms. The Undead Crown also restores its
native `SETAC 8–8` power, and the shipped test compares every native unique
definition's level, value, cursor, and power rows against the external catalog;
the remaining 19 native cursor projections are now explicit in the overlay.

The Godot protocol model now validates the shape of non-empty authoritative
snapshot hashes (64 hexadecimal characters) while retaining compatibility with
legacy snapshots that omit the additive hash field. Digest computation and
verification remain server-owned. The generic TCP host now validates the same
shape before serializing adapter snapshots, so malformed non-empty hashes fail
closed before reaching either presentation client. It also rejects zero or
duplicate entity IDs across the projected entity collections, matching the
Godot and native compatibility boundaries. It also validates difficulty,
portal-route state, and active world-mutation identities before emission. Native
snapshot projection now applies the same printable, 31-character player-name
shape accepted by Godot and the Diablo mapper; the shared protocol assembly now
owns that wire-level shape rule for host and Godot boundaries, while the Diablo
module retains independent native validation to preserve its dependency
boundary. The generic host rejects malformed names before wire serialization,
and the Godot setup form applies the same 31-character printable-ASCII limit
before sending a new-session request.
The generic host also rejects command batches larger than the bounded transport
limit before command admission, preventing oversized client payloads from
reaching authoritative execution.
The generic handshake now applies the same shared character-name shape rule to
optional requested names before participant initialization, keeping malformed
identity input outside the game module.
The authoritative Diablo victory rank now applies the native byte-sized range
at C# projection, generic-host, Godot, and native compatibility boundaries,
preventing malformed values from being narrowed into presentation state.

Godot event-batch ingress now rejects unknown event kinds, impossible signed
damage/healing values, invalid dialogue message/text payloads, and batches
larger than 4096 entries while retaining zero-ID compatibility for untargeted
events.

The generic TCP host applies the same event-batch validation to adapter output
before writing it to the shared protocol, and the native compatibility client
applies the same checks before event-driven resource/presentation updates;
malformed module or wire events therefore fail closed at every current boundary.
Loopback host coverage now drives a malformed adapter batch through the live
handshake and command path, confirming the host returns an invalid-message
error instead of serializing the event batch.

Authoritative save and world-seed writes now use unique temporary files with
`WriteThrough` and an explicit disk flush before atomic replacement. Failed
writes clean up their temporary file without changing the previous committed
save, and focused store tests verify that normal writes leave no temporary
artifacts. This strengthens the server-owned recovery boundary without
changing the version-1 save envelope.

World-seed initialization now coordinates all store instances sharing a path
and uses first-writer-wins atomic creation, so concurrent server starts cannot
replace a previously selected deterministic world seed.

Fresh TCP recovery coverage now repeats the same saved session through four
replacement host/adapter cycles, preserving the resume token, player identity,
character name, and canonical snapshot hash while confirming each retired
adapter is disposed.

Concurrent save-store instances now coordinate by canonical save path, and
focused coverage proves that atomic replacement publishes one complete session
payload and leaves no temporary artifacts after the writers finish.

The TCP host now persists each successfully initialized or restored session
immediately after its handshake, so a participant that has not issued a
gameplay command is still recoverable after a process restart.

Godot live protocol coverage now restarts the production server process with
two clients, restores both saved participants through their resume tokens, and
verifies self-first ordering, class/name identity, and moved authoritative
position state.

Generated unique placements now accept an optional validated
`unique_monster_packs.tsv` contract. The server materializes deterministic
independent or leashed minions through the authoritative spawn planner,
assigns generated monster and loot entity IDs without colliding with existing
targets or drops, avoids reserved cells, and preserves the native strict
four-tile envelope for leashed members. Generated minions now also restore the
native doubled maximum health and inherit the leader's intelligence used by
authoritative AI decisions. Placement now follows the native bounded retry and
transparency-region rules when native transparency data is available. Unique
leaders retain their pack relationships across authoritative ticks.
Visibility-driven activation is shipped for geometry-backed worlds; broader
activation timing and cross-snapshot prediction/soak coverage remain
follow-ups.

The authoritative monster AI selector now preserves explicit native
SkeletonAi-compatible `SkeletonMelee`, `Snotspill`, and `Warlord` identities
when a generic catalog row is supplied, while complete catalog controllers
retain precedence for intentionally content-selected identities such as
Gargoyle.

Geometry-backed server worlds now apply the first visibility activation gate:
occluded monsters with no active tick remain inactive while connected players
are present, using the authoritative walkability layer and shared native
ray-table light-radius line-of-sight when native transparency is supplied.
The ray pass also rejects native-style diagonal corner occlusion.
Native DUN piece metadata marked `BlockLight` now participates in the same
vision blocker path, rather than relying only on movement collision.
Player light radius is now derived from identified usable equipment using the
native baseline and 2–15 clamp, projected through `PlayerSnapshot.light_radius`,
and consumed by both server activation/targeting and Godot presentation.
Geometry-free compatibility fixtures and no-player simulation ticks retain
their legacy behavior; broader activation timing remains follow-up work.

The generic Data composer now permits a validated overlay to introduce an
optional table absent from earlier packs. This keeps pack-only contracts
external to the base table set while retaining strict key and schema checks.

Monster group-unity state now crosses the shared snapshot boundary through
additive leader identity/relation, pack-size, and active-tick fields. The
server projects and hashes these fields, while Godot and the native
compatibility client validate and retain the state for presentation. Default
values preserve legacy snapshot hashes. Godot monster presentation now moves
toward the server's authoritative future tile. Native authoritative monster
presentation now maps server-declared movement directions to the native
movement modes and walk animation consumed by the renderer, while stationary
snapshots clear stale movement modes; the connected loop advances that walk
animation without running legacy simulation. The bounded fractional
presentation clock now advances timed monster and remote-player walks between
snapshots; broader cross-snapshot prediction and soak coverage remain follow-up
work. The native
compatibility validator now accepts
authored unique leaders in the same pack relationship contract.
Godot also snaps monster presentation on level changes or large authoritative
relocations, preventing stale interpolation from carrying entities across
discontinuous server state. The native world projection now carries the
authoritative snapshot tick into presentation and ignores older complete world
projections, preventing delayed server state from regressing native entities;
legacy callers that do not provide a tick remain accepted.

Authoritative monster snapshots now optionally carry a server-owned movement
start/effect tick window. Godot interpolates a one-tick walk from the current
tile toward the reserved future tile, while native occupancy and targeting stay
discrete; zero-valued fields preserve older snapshots and saves.

The protocol-free native world projection path now repeats the structural
monster invariants needed by compatibility presentation: non-zero unique
identities, native direction range, one-tile movement destinations, and the
validated group-unity leader contract. It also
rejects duplicate nonzero identities across projected entity collections while
retaining zero-ID placeholder compatibility for items and objects. This keeps
legacy callers safe even when they bypass the richer protocol-session
validator; projectile lifecycle, visual-ID, and directional metadata ranges now
receive the same fail-closed treatment. Full protocol validation remains the
authoritative ingress check, and malformed or misplaced native missile
identities are rejected before compatibility presentation. Projected entity
coordinates must remain in the native tile range even without geometry; when
authoritative level geometry is supplied, monster, item, object, and projectile
positions are also checked against its fine-grid bounds before publication.
The same boundary now preserves the protocol's spell-less WeaponArrow and
MonsterArrow exception and enforces the shared physical-through-acid damage
type range.

Explicit runtime AI identities now also select their native missile mapping
before a generic monster-type catalog row. Unique encounters therefore cannot
inherit a generic projectile or movement branch merely because their base type
remains generic; non-unique alternate-AI catalog rows retain their compatibility
precedence.

Explicit native AI identities on unique encounters now take precedence over a
generic monster-type catalog row. Warlord, Snotspill, and Fallen therefore
retain their server-owned Skeleton/Fallen combat branches when their encounter
inherits baseline type metadata, rather than falling through to generic
movement.

Zhar and Lazarus now retain the native Counselor recovery-delay and RNG branch
when their validated Fireball mappings are dispatched, rather than being
treated as generic ranged monsters because their runtime identities are
aliases.

Lightning Wall segments now carry an explicit server-selected native visual and
lightning presentation family when their shared wall-segment behavior uses the
Lightning damage channel; Fire Wall and Ring of Fire segments retain fire
presentation. Native and Godot bridges both consume the new identity, and both
retain behavior/damage-channel fallbacks for older snapshots that omit additive
presentation metadata.

Live Godot combat fixtures now select authored base targets by authoritative
entity identity, so generated unique encounters can be projected without
making target selection ambiguous; the charged-source checkpoint tracks the
current expanded encounter state.

Live Godot coverage now also exercises an externally declared memorized
Telekinesis spell, proving that target knockback and mana consumption remain
server-owned while the client observes the resulting snapshot.

Live Godot coverage now also exercises an externally declared memorized Mana
Shield spell, proving that activation and mana consumption remain server-owned
while the client observes the authoritative state.

Live Godot coverage now also exercises an externally declared memorized Berserk
spell, proving that target mutation and mana consumption remain server-owned
while the client observes the authoritative state.

Live Godot coverage now also exercises an externally declared memorized Ring of
Fire spell, proving that server-owned fire-wall segments and presentation
metadata cross the protocol boundary while generated geometry can occlude
blocked cells.

Live Godot coverage now also exercises an externally declared memorized Stone
Curse spell, proving that tile-targeted petrification and mana consumption
remain authoritative across the protocol boundary.

Live Godot coverage now also exercises an externally declared memorized Phasing
spell, proving that server-owned candidate selection, relocation, and mana use
cross the protocol boundary.

The shared replay gate now covers a two-projectile multiplayer MonsterArrow
participant volley, multiplayer HorkDemon, SkeletonRanged
MonsterArrow, Succubus BloodStar, ArchLich, Psychorb, Necromorb, and BoneDemon
MonsterMagic alias admission, and Lich MonsterMagic target selection, plus
Lich MonsterMagic secondary-player block resolution: native and C# runners
choose the nearest participant, preserve the canonical target identity, and retain
the native block mode/timing.

Godot and native snapshot boundaries now reject duplicate materialized
procedural level IDs, matching the server's additive field-19 validation.

Procedural encounter materialization is now persisted through additive
snapshot/protocol field 19, so defeated generated encounters do not respawn
after save/restart; canonical snapshot hashing includes the state.

Hork Spawn expiry materialization is also covered through save/restore, retaining
the spawned monster identity, position, hit points, and consumed projectile.

Active Lich MonsterMagic projectiles are now covered through save/restore as
well: the saved authoritative tick, projectile trajectory, target identity,
phase, and remaining lifetime survive import, and the replacement runtime
continues from that tick without replaying earlier world ticks.

The shared multiplayer MonsterArrow volley now also survives importing both
participant saves: trajectory, participant ordering, canonical hash, impact
phase, and final removal remain deterministic after replacement-runtime
continuation.

Real TCP reconnect coverage now combines a purchased server-owned store state
with a second live participant, preserving the complete participant set,
self-first ordering, and canonical snapshot hash when the purchaser resumes.
The same fixture verifies requested character names for both participants at
the live authoritative boundary.

The Godot protocol client now exercises the same two-participant store purchase
and reconnect path, confirming that the presentation-side client preserves the
authoritative participant set, names, inventory, and gold after resume.

Godot live content-overlay coverage also exercises an externally declared
memorized Reflect spell: the server validates the loadout, consumes authoritative
mana, and creates the native level-scaled Reflect charge. Existing overlay
fixtures now target shipped schema 1.430.0 and the current stores-table shape.

The same live Godot boundary now exercises an externally declared memorized
Golem spell, proving that the server creates an alive player-owned minion and
projects its owner identity through the protocol for presentation.

It also exercises externally declared memorized Immolation: the authoritative
server accepts the cast and projects the selected spell identity through the
shared protocol. The Diablo combat tests retain the native 36-projectile fire
fan-out and presentation-metadata assertions without depending on a transient
live snapshot.

Live Godot coverage now also exercises externally declared memorized Nova,
confirming that the authoritative server accepts the external spell loadout and
projects the selected spell identity. The Diablo combat tests retain the
server-owned 36-ball radial fan-out and `NovaBall` presentation-metadata
assertions without depending on a transient live snapshot.

The same live boundary now exercises an externally declared memorized Teleport,
confirming that destination resolution, movement, and mana consumption remain
server-owned while the Godot client observes the resulting snapshot.

The live Godot client also now queues a store purchase, reconnects before
submission, and verifies that the pending command is retried through the same
resume session and applied once by the authoritative server.

Godot now resolves alive `is_player_minion` snapshots to a dedicated
presentation family and renders them with an owned green cue; dialogue metadata
cannot misclassify a server-owned minion as a hostile or interactive monster.

The validated data boundary now has regression coverage for rooted,
drive-qualified, and directory-traversing native asset paths, keeping external
packs constrained to their declared relative content tree.

Directory-pack loading also rejects reparse-point entries before reading a pack,
so symbolic links and junctions cannot widen the external content boundary.
The root directory is covered by the same fail-closed check.
Pack table and native-asset identities are also compared case-insensitively, so
Linux-authored packs cannot contain path collisions that behave differently on
the native Windows client.

Per-participant session saves are also covered across a two-player recovery:
importing both saved participants into one replacement simulation preserves
self-first entity ordering and the canonical multiplayer snapshot hash.

Autonomous multi-monster save/restore now preserves ordered monster identities,
canonical snapshot state, and deterministic continuation on the next
authoritative tick.

Snapshot restoration now rebuilds transient procedural-spawn tracking and the
generated monster identity cursor from the restored world, so replaying an
earlier snapshot can materialize a later level encounter again with the same
authoritative identities.

Snapshot restoration now republishes deferred native-DUN fragments after
restored unique encounters and fragment objects are hydrated, preserving
anchored geometry across process restart and replacement simulations.

Authoritative monster targets now retain the native `InitMonster` item-drop and
AI seeds through snapshots, saves, and additive protocol fields 86-87.
Procedural monster loot uses the retained item seed, preserving the server-owned
drop identity across reconnect and restart boundaries. Multiplayer monster ticks
advance the retained AI seed once per tick, matching native `ProcessMonsters`
state progression, and restore each monster's stream before its AI random draws.
Multiplayer Zombie AI now consumes the stream for its action and movement draws;
Multiplayer SkeletonMelee now consumes the stream for movement and adjacent
attack admission, native delay rolls, and RandomWalk coin draws.
Multiplayer SkeletonRanged now consumes the stream for its native movement and
retreat admission plus the subsequent arrow attack roll, including the
far-distance movement draw. Multiplayer Fat/Overlord AI now consumes the
stream for its native movement or adjacent-attack admission, including
RandomWalk coin draws. Multiplayer GoatMelee/AiAvoidance now consumes the
stream for its movement, Move-goal, RandomWalk, adjacent-attack, and
wounded-special draws. Multiplayer Gargoyle now consumes the stream for
healthy AiAvoidance movement,
adjacent attacks, wounded retreat RandomWalk, and healing divisor rolls; its
native special lock remains server-owned. Multiplayer ButcherAi now consumes
the stream for its non-adjacent RandomWalk coin draws; adjacent attacks retain
their no-roll native admission. Multiplayer Sneak now consumes the stream for
its native action roll, retreat RandomWalk, and adjacent-attack admission;
seed-zero targets retain the compatibility path. Multiplayer Skeleton King now
consumes the stream for its native action and movement-admission rolls, delay
range, RoundWalk, RandomWalk, and adjacent-attack admission; multiplayer
summoning remains server-owned. Single-player Skeleton King now consumes its
native action roll for summon and adjacent-attack admission; summons also use
the live stream for skeleton-type selection, stand-animation timing, initial
health, and the retained item/AI seeds before entering authoritative simulation.
Initialized single-player SkeletonMelee/Fallen targets now consume the global
native stream for SkeletonAi action, adjacent attack admission/delay, and
RandomWalk draws; zero-seed fixtures and legacy multiplayer snapshots retain
the compatibility controller.
Initialized single-player GoatMelee/AiAvoidance targets now consume the global
native action, Move-goal, adjacent attack/special, RandomWalk, and delay draws
under the same rule.
Initialized single-player Fat/Overlord targets now consume the global native
movement, adjacent attack, and RandomWalk draws under the same compatibility
rule.
Initialized single-player HorkDemon targets now consume the global native
spawn/attack and movement admission, recovery delay, RoundWalk, and RandomWalk
draws under the same compatibility rule.
Initialized single-player SkeletonRanged targets now consume the global native
retreat/movement and arrow-admission draws under the same compatibility rule.
Initialized single-player GoatRanged targets now consume the global native
close-range retreat, RandomWalk fallback, and post-shot delay draws under the
same compatibility rule.
Initialized single-player Succubus/Lazarus Blood Star targets now consume the
global native close-range retreat, RandomWalk fallback, and post-shot delay
draws under the same compatibility rule.
Initialized single-player Rhino targets now consume the global native charge,
movement, recovery-delay, and adjacent-admission draws under the same
compatibility rule.
Initialized single-player Scavenger targets now consume the global native
corpse-scan direction and corpse-targeted RandomWalk draws under the same
compatibility rule.
Initialized single-player Counselor targets now consume the global native
ranged/movement admissions, retreat RandomWalk, adjacent admission, and
server-owned recovery-delay draws under the same compatibility rule.
Initialized single-player Sneak targets now consume the global native action,
retreat RandomWalk, and adjacent-admission draws under the same compatibility
rule.
Initialized single-player Bat targets now consume the global native charge,
retreat, movement, RandomWalk, and adjacent-admission draws for validated Bat
controllers under the same compatibility rule.
Initialized single-player Gargoyle targets now consume the global native
healing, retreat, AiAvoidance movement, RandomWalk, and adjacent-admission
draws under the same compatibility rule.
Initialized single-player Zombie targets now consume the global native action,
random-direction, and direct movement draws under the same compatibility rule.
Initialized single-player Mega targets now consume the global native delegated
Skeleton fallback and Inferno admission/recovery draws under the same
compatibility rule.
Initialized single-player Snake targets now consume the global native action,
delay, and drift draws under the same compatibility rule.
Initialized single-player Bat targets now consume the global native movement
fallback and RandomWalk draws under the same compatibility rule.
Initialized single-player Skeleton King targets now consume the global native
movement, RoundWalk, RandomWalk, and delay draws under the same compatibility
rule; their existing native action-roll behavior remains unchanged.
Initialized single-player ranged-avoidance targets now consume the global
native admission, movement, and projectile/recovery draws under the same
compatibility rule.
Initialized single-player generic ranged targets now consume the global native
retreat and post-shot delay draws under the same compatibility rule.
Initialized single-player Butcher targets now consume the global native
non-adjacent RandomWalk draws under the same compatibility rule.
Single-player and seeded multiplayer ordinary adjacent melee now roll the
effective native min/max damage range from the authoritative stream, including
validated special melee ranges; seed-zero multiplayer targets retain the legacy
damage projection. Single-player and seeded multiplayer adjacent monster melee
also consume native hit admission, shield/class block admission, and damage
rolls in native order, with the damage range rolled directly in native Q6 and
the exact applied amount projected through `DamageEvent.amount_q6`; seed-zero
multiplayer targets retain the compatibility controller. Monster-arrow impacts
now cross the same native Q6 boundary before player
damage resolution and carry the exact applied amount in `DamageEvent.amount_q6`;
legacy whole-life fixtures retain their compatibility projection. Authoritative
monster rows now carry validated leader identity, leashed/separated relation,
and pack-size state; the server applies the native line-of-sight separation and
strictly-under-four-tile reunion rule during autonomous ticks and preserves the
relationship through snapshots. Native active-tick FollowTheLeader propagation
now runs for leashed members and survives snapshots. Geometry-backed
visibility-driven activation is shipped; broader activation timing and
cross-snapshot position prediction remain follow-up work. Post-tick authoritative
movement-admission passes now prevent either a leader or a leashed member from
moving beyond the strict four-tile pack envelope.
Multiplayer Bat now consumes
the stream for native adjacent
attack admission, two-step retreat, movement, and mapped-charge action/RandomWalk
draws; mapped charge misses reuse the same action roll without double-consuming
the stream.
Multiplayer Snake now consumes the stream for distant and adjacent action
admission, native delay rolls, and RandomWalk2 fallback draws; mapped charge
ticks retain their native no-roll branch and seed-zero compatibility.
Multiplayer HorkDemon now consumes the stream for spawn/attack and movement
admission, recovery delay, RoundWalk, and RandomWalk draws; seed-zero targets
retain the compatibility path.
Multiplayer Scavenger now consumes the stream for corpse-scan direction and
corpse-targeted RandomWalk draws; seed-zero targets retain the compatibility
path.
Multiplayer Mega now consumes the stream for its far-distance Skeleton fallback
and mapped Inferno admission, including native action, adjacent-choice,
intermediate-movement, recovery-delay, and collision draws; seed-zero targets
retain the compatibility path.
Multiplayer GoatRanged now consumes the stream for its close-range AiRanged
admission, native RandomWalk fallback, and post-shot delay; long-range and
seed-zero branches retain their native no-roll compatibility path.
Multiplayer Succubus/Lazarus Blood Star now consumes the stream for its
close-range AiRanged admission, native RandomWalk fallback, and post-shot
delay; long-range and seed-zero branches retain their native no-roll
compatibility path.
Multiplayer Rhino charge, movement, and adjacent attack admission now consume
the stream for initial admission, far-distance Move-goal setup,
RoundWalk/RandomWalk fallback, and recovery delay; seed-zero targets retain
the compatibility path.
Seeded multiplayer AiRangedAvoidance mappings now consume the stream for their
initial 10,000-band admission, Move-goal setup/direction, RandomWalk movement,
and recovery delay; seed-zero targets retain the compatibility path.
Seeded multiplayer generic AiRanged mappings (FireBat, Torchant, Lich-family,
and AcidUnique) now consume the stream for their close-range admission,
native RandomWalk fallback, and post-shot delay; long-range and seed-zero
branches retain the native no-roll compatibility path.
Player-owned Golem movement now preserves the native blocked-path
RandomWalk and clockwise eight-direction fallback before giving up; seeded
multiplayer minions consume the restored stream for the two RandomWalk coins.
Authoritative Golem casts now retain the native spawn-time item and AI seeds
and advance the same animation/health seed draws before the minion enters
multiplayer simulation.
Multiplayer Counselor now consumes the stream for its initial and secondary
ranged/movement admissions, adjacent admission, retreat RandomWalk coins, and
server-owned recovery delays; seed-zero targets retain the compatibility
controller.
Single-player and seeded multiplayer Fallen fear retreats now consume the
native RandomWalk coin draws before selecting their retreat cell; seed-zero
multiplayer targets retain the deterministic compatibility sweep.
Fallen targets now retain native standing-animation frame/tick state through
snapshots, saves, and additive protocol fields 88-90. The server performs the
animation-boundary `FlipCoin(4)` rally admission, applies native self-healing,
and gives nearby Fallen a server-owned attack timer that pursues players beyond
their normal aggro range with native `RandomWalk` ordering.

Content schema `1.430.0` now carries validated native standing-animation frame
and tick metadata for shipped monster types. Procedural spawning consumes the
same two stand-animation RNG draws that native `InitMonster` performs before
rolling initial health and damage.

At this migration stage, ordinary procedural monster population preserved the
native default scatter capacity: generation was capped at 190 active monsters,
leaving the native ten reserved slots available for quest and runtime summons.
The current `level_generation.tsv` contract keeps that value as the compatibility
default while allowing validated packs to override it through
`ordinary_monster_capacity`. Caller-provided lower limits and the existing
density/multiplayer rules remain unchanged.

Procedural monster placements now also carry the native pre-initialization
0-to-7 placement direction through authoritative targets instead of defaulting
all generated monsters to direction zero.

Skeleton King runtime summons now retain the native direction toward the
victim when their authoritative targets are created, preserving the
`SpawnMonster(newPosition, direction, typeIndex)` initialization contract.

Planner-created Gargoyles now begin with the native special lock active, so
their first authoritative AI ticks preserve DevilutionX's initial stone-state
admission until the player closes the safety boundary.

Planner-created monster targets now apply native `InitMonster` single-player
health initialization: current and maximum health are halved with a one-health
floor, while multiplayer retains full generated health.

The inventory contract now preserves native placement order for the shipped
footprints and uses a deterministic first-fit fallback for any other validated
external footprint within the native 10x4 grid. Connected native object
interactions likewise fail closed when an object has no authoritative
projection; legacy object behavior remains an offline compatibility path.

The native-compatible inventory placement scan now lives in the engine-neutral
domain layout instead of the Diablo store processor. Sorting, purchases, and
equipment-displacement planning therefore share one deterministic placement
primitive while retaining the same dimension-specific scan order and external
footprint fallback.

Authoritative Diablo world and combat paths now call the engine-owned bounded
crawl primitive directly for portal, warp, teleport, resurrection, projectile,
and multi-target searches. `DiabloCrawl` remains available only as a
source-compatible facade for older module callers.

The engine entity-ID allocator now owns collision skipping and identity-space
exhaustion handling. Diablo combat and world processors retain their native
world-item, monster, projectile, and authored-projectile ranges while routing
all allocations through that shared deterministic primitive.

Native DUN monster hydration and generated unique-monster pack materialization
now use the same engine allocator for their runtime IDs. Authored IDs remain
unchanged, while generated pack starts continue to reserve occupied monster
and drop identities before native-compatible pack creation.

All authoritative Diablo distance consumers now call the engine-owned widened
Chebyshev metric directly, including combat targeting, movement and AI,
projectile resolution, world interactions, and snapshot validation. The old
combat-target distance facade has been removed; compatibility behavior remains
in the engine primitive itself.

Connected native spell targeting now also fails closed when a visible player or
combat-entity cursor target has no authoritative entity projection, preventing
presentation-only cursor state from becoming an unintended tile cast for both
memorized/ability spells and scroll/rune consumables.

Connected native oil use now also routes inventory-sourced oils onto open stash
items through the authoritative stable-reference command; closed stash targets
remain rejected without consuming the source item.

Connected native stash books, potions, and stash-sourced oils now use open-session
stable item references as authoritative sources; a closed stash is rejected before
the source can be consumed or mutate player state.

Connected native spell selection now includes validated scroll and rune sources
from an open authoritative stash, and casts carry the same stable page/cell/item
reference used by the server and Godot client. Offline spell selection remains
inventory/belt-only for compatibility.

Cursor-triggered native scroll and rune casts now use the same open-stash
stable-reference fallback when the selected source has no inventory or belt
slot.

Godot's authoritative cast-source selector now includes spell consumables in an
open stash, carrying page/cell/stable identity through the same cast protocol;
the server rejects those sources when the stash session is closed.

The authoritative simulation now accepts and publishes generated set-piece
origins, materializing relative `set_level_objects.tsv` rows at level-entry
time with deterministic reserved identities and world-bound validation. These
origins now cross transport-neutral snapshots and version-1 saves with
canonical state hashing. The validated procedural-origin catalog is retained
after activation, so a host can publish deferred origins when generated level
geometry arrives, and level-transition intents invoke the same pass; repeated
publication revalidates without duplicating objects.
Producing the actual Blind, Blood, and Warlord origins from native-compatible
level generation remains follow-up work.

The server protocol mapper and Godot client model now validate published
generated origins against supplied authoritative level geometry before emitting
or accepting a live snapshot; older geometry-free snapshots retain the
compatibility path.

The native compatibility client now enforces the same generated-origin
validation before applying a snapshot, including duplicate set-level identity
checks and the geometry-free legacy path.

Native snapshot projection now also applies Godot's non-empty state-hash shape
check: omitted hashes remain compatible with legacy peers, while supplied
values must be exactly 64 hexadecimal characters before projection.

The native presentation bridge independently revalidates DUN object-layer IDs
before applying directly constructed geometry projections, so malformed
compatibility data fails closed even when it bypasses protocol validation.

Authoritative world-item pickup now preserves the native cursor contract: an
overflow item is projected as the server-owned held item, and picking up while
holding another item atomically drops the existing cursor item at the player's
authoritative tile before replacing it. The native replay projection follows
the same overflow and cursor-replacement rules.

Telekinesis preflights that cursor replacement, including walkability,
occupancy, and world-item collision at the authoritative drop tile, before
consuming mana or a memorized source. A rejected replacement therefore leaves
the held item, target world item, and cast resources unchanged.

Telekinesis object targets now run the same authored world-mutation, reward,
and spell-projectile validation during preflight. Invalid object projections
therefore fail before mana or the memorized source is consumed, preserving the
single authoritative cast transaction.

Staged unique-monster dialogue rewards now use the complete participant set
for world-item placement validation and entity allocation. A blocked reward
leaves quest progress and world state unchanged until its tile is available.

The non-staged unique-monster dialogue reward path now uses the same boundary,
including normal and spell-book rewards, before consuming a required dialogue
item or changing quest state.

Towner turn-in rewards now allocate world-item identities against the complete
participant set and preflight other-participant and existing-item occupancy
before completion, activation, or ordinary item-consuming mutations. The
interacting player's own reward tile remains compatible with native behavior.

Monster-defeat quest rewards now use the active participant set and existing
world-item state when selecting their native target tile. If that tile is
occupied, a deterministic nearby open tile is selected without consuming the
reward seed until placement is available.

Configured monster drops now use the same deterministic placement search before
their immutable item state is inserted into the world, so combat death drops
cannot materialize beneath another participant or an existing item.

Validated spell activation now rejects cyclic Jester dispatch graphs, keeping
recursive dispatch finite before an external content pack can reach combat.

Quest activation validation now rejects self-referential completion targets;
valid cross-quest activation routes remain supported.

The generic TCP host now has focused four-participant reconnect coverage,
preserving complete self-first snapshots and canonical state hashes while a
second, third, and fourth participant remain connected; the resumed
participant rejoins three active peers without changing the canonical hash.

World-item anchor completion now requires an active quest, preventing an
inactive content quest from being completed by an authoritative item drop.

Deferred native-DUN fragments now refresh the effective world projection after
their geometry is published and reconcile explicit object mappings afterward.
Mappings may therefore target a layer introduced by a fixed or unique-anchored
fragment without creating a presentation-only or inert set-piece object.

Ordinary-level native DUN object mappings now validate their target level during
content activation and fail closed when the composed world does not contain it;
deferred set-level mappings remain eligible for later geometry publication.

Set-level and native-DUN object behavior templates now validate during content
activation before deferred geometry is loaded, including unknown-template and
object-identity mismatches.

Native DUN fragments now validate their target levels before activation even
when unique encounter anchors defer world projection.

Native-DUN monster materialization now reruns deferred unique-fragment
publication after mapped monster targets exist, preserving same-activation
set-piece geometry for native monster-layer encounters.

Native DUN geometry materialization now carries the asset's transparency layer
into the authoritative level alongside its collision, object, mega-tile, and
d-piece layers, preserving the native `dTransVal` data for protocol projection
and later presentation parity.

The raw native DUN monster layer is now retained through authoritative level
materialization, ordinary-level composition, deferred fragment composition, and
world-mutation rebuilds. Content schema 1.429.0 now externalizes each valid
`MonstConvTbl` index with a native identity and stable monster selector;
authoritative simulation materializes mapped cells in native row-major order
after geometry is available, while unsupported or unmapped entries remain
inert.
Mapped native DUN monster cells now also fail closed when one level would
exceed DevilutionX's 200-monster active pool.

Authored object reward placement now uses the complete authoritative participant
set for occupancy validation and entity allocation. Multiplayer object rewards
therefore cannot be materialized underneath another participant.

Authored quest-sequence monster follow-ups now use that same participant set for
spawn validation. A blocked multiplayer spawn leaves the quest step and world
unchanged until its target tile is authoritatively available.

The external geometry contract now preserves all native `.sol` d-piece flags
when a text property table is used: legacy `solid`/`block_missile` rows remain
valid, while optional lighting, transparency, and trap flags are validated and
retained for the same authoritative geometry model.

The authoritative monster catalog now accepts Lachdanan's native AI identity,
and server ticks keep the dialogue-only encounter passive outside the existing
server-owned quest-dialogue path.

The shipped Hellfire FireMan identity now fails closed to passive behavior,
matching DevilutionX's unregistered native AI instead of entering generic
authoritative melee until its native behavior is explicitly externalized.

LazarusSuccubus now preserves the native single-player Vile Betrayer gate:
server-owned Blood Star combat is suppressed through `_qvar1=5`, resumes at
`_qvar1=6`, and remains active in multiplayer as in `LazarusMinionAi`.

Late-generated level additions and replacements now pass through
`DiabloWorldState`, which rebuilds effective mutation/native-layer geometry
before deferred procedural origins are published; direct simulation consumers
no longer need to mutate the projected-level cache themselves.

Connected native spell and resurrection-beam helpers now fail closed at their
public entry points, so direct callers cannot rely on the missile factory's
connected-mode no-op to preserve server ownership. Offline sessions retain the
legacy spell path.
Native doppelgänger spawning and monster-driven player-attribute reduction now
also fail closed in connected sessions; these public helpers cannot mutate
server-owned monster or player state when reached outside the legacy loop.
Direct native monster damage application now fails closed as well, preventing
connected missile or protocol callers from changing server-owned hit points or
entering legacy death/drop handling.
Native monster hit attribution and gargoyle wake-up mutation now fail closed
under the same boundary; connected presentation cannot update server-owned
attacker or special-attack state.
Native monster knockback and petrification mutators now use the same boundary;
connected presentation continues to use snapshot-driven animation/state
projection rather than local simulation.
The native player action-start helpers now apply the same explicit boundary for
walking, melee attack, ranged attack, and spell execution; connected sessions
cannot start a second local player simulation through a direct caller.
The corresponding per-action execution helpers now fail closed as well,
covering movement completion, combat impacts, blocking, spell execution, hit
recovery, and death animation state.
The lower-level native player combat helpers now apply the same guard before
damage, knockback, resource-steal, and breakable-object mutation can occur.
The native player level-transition cleanup helper now also fails closed in
connected sessions, so presentation-only restart/transition cleanup cannot
kill server-owned golems or restore server-owned Stone Curse state locally.

Native authoritative monster presentation now applies the same alive-only
visibility rule as Godot. Dead projected monsters are omitted before pool
hydration, so stale compatibility slots are removed rather than rendered.
The native projection now also preserves server-owned `SneakHidden` state;
hidden monsters are omitted from native presentation and interaction lookup,
matching Godot's visibility and targeting filters.
Native entity visibility now uses one level-0 wildcard predicate across world
lookups and remote-player targeting, matching the Godot level-visibility rule.
Native object projection now also preserves server-owned `TrapArmed` state and
recognizes trap-only objects as authoritative disarm targets, matching Godot.
Native compatibility objects now also receive authoritative missile-passability
and breakable/broken state, preventing stale collision metadata on resync.
Godot now applies one authoritative monster presentation visibility predicate
when hydrating cached positions, drawing monsters, and rendering action-target
indicators; dead or `SneakHidden` monsters cannot leak through a presentation
path even when they remain present in the complete snapshot.
When projected native transparency is available, Godot also applies the
server-aligned equipment-derived light-radius vision boundary and diagonal
corner occlusion to monster drawing; geometry-free snapshots retain the
compatibility predicate.
Authoritative level geometry now projects derived native vision-blocked cells,
including DUN `BlockLight` pieces, through the shared protocol. Godot consumes
that projection for presentation visibility while older snapshots fall back to
their movement-blocked-cell behavior.
Godot object presentation now follows the native removal boundary as well;
activated or destroyed objects are removed from cached positions and are not
drawn while their authoritative state remains available for validation and
resynchronization.

Godot quest/towner snapshot validation now rejects duplicate level-geometry
identities through its normal invalid-snapshot boundary before evaluating
interaction bounds, matching the native fail-closed path.

Dynamic item presentation identity now has the same native compatibility
requirements in the server mapper and Godot model as in the C++ bridge: both
runtime names and a byte-representable cursor graphic are required together.
The same boundaries now reject item scalar values that cannot fit the native
item storage types before they reach compatibility presentation.
Native player-item projection now also enforces the managed inventory-grid,
footprint, equipment-slot, belt-slot, and collection topology checks.
Native vendor projection now rejects seedless stock without complete dynamic
presentation identity, matching the server and Godot active-store boundaries.
Unique-monster and quest-completion identity limits now count UTF-8 bytes in
the managed validators, matching the native protocol representation.
The shared Domain limit is also applied before activation to authored unique
monsters, placements, quest state, world-mutation anchors, and native-DUN
fragment anchors.
The native player UI now accepts the same omitted zero-length legacy inventory
grid as the server and Godot paths and clears stale cells before hydration.
It also clears the native character name when an accepted legacy snapshot
omits that optional identity, preventing prior-session names from persisting.
Remote native player presentation follows the same rule: omitted legacy grids
clear stale equipment, belt, inventory, grid, and held-item state before the
next projected remote player is presented.
Native monster projections now retain and apply the server-owned direction
before compatibility presentation, avoiding stale direction state in reused
native monster slots.
Native and Godot snapshot validators now require each monster's `Alive` flag to
match effective Q6 hit points, including the legacy whole-point fallback, so a
contradictory dead/alive state cannot cross either client boundary.
Godot monster vitality validation now also compares effective current and
maximum Q6 hit points after the same fallback, rejecting legacy snapshots that
would be refused by native projection.
Godot visited-level validation now rejects IDs outside the ordinary native
25-level `_pLvlVisited` range while accepting the reserved runtime set-level
identities used for the current player level, matching native projection.
Godot session metadata validation now rejects unknown difficulties and zero or
duplicate active world-mutation identities, matching native projection.
Godot now accepts the same omitted stationary monster destination used by the
native projection, normalizing legacy `(0, 0)` future coordinates to the
current tile instead of rejecting an older additive snapshot.

Raw native DUN object-layer values now use the same `ObjTypeConv` index bound
(`0` through `DUN_OBJECT_ID_LAST`, currently `146`) in the C++ snapshot bridge,
authoritative C# levels, and Godot protocol validation. Converted object
presentation identities remain independently bounded by `OBJ_LAST` (`108`),
while d-piece presentation values remain independently validated as 16-bit
identities. Unmapped native object-layer cells remain inert until an explicit
authored mapping exists. Authored object-layer mutation rectangles now enforce
the same raw-DUN bound during content activation.
Native-DUN object materialization also enforces DevilutionX's `MAXOBJECTS` pool
capacity of `127` active objects per level, counting existing authored objects
before mapped cells are added.
Set-level placements and native-DUN fragment template clones apply that same
per-level bound during materialization, including authored occupants already
present on the target level.
The server protocol mapper, Godot client validator, and native snapshot bridge
now enforce that same per-level object-pool boundary before presentation, so an
oversized authoritative object collection fails closed instead of silently
dropping objects when the native presentation pool is exhausted.
Warp-trigger projection now rejects missing source levels on the server and
unknown quest-gate identities in the server, native, and Godot validators, so
all three protocol boundaries enforce the same authored-route references.
Native quest ordinals in projected quest snapshots now share the `0` through
`23` range and uniqueness checks across server, native, and Godot validation.
Projected monster unique identities now enforce the native 128 UTF-8-byte
limit and unique-monster ownership across the engine, server, native, and
Godot boundaries.
Object snapshot identities now use that same bound before native or Godot
presentation consumes them.
Native-DUN mapping activation and mutable authoritative-object updates enforce
the bound as well, so invalid identities cannot persist in server state.

The native compatibility presentation bridge now applies supplied authoritative
mega-tile, d-piece, and transparency layers to the active level after validating
their native dimensions and value ranges. This closes the map-layer portion of
the snapshot resynchronization seam; native DUN object-layer materialization and
object encounter-state presentation remain server-authored compatibility work.
Town-sized 112×112 geometry without a coarse mega-tile layer now remains a
valid presentation projection; the native 40×40 coarse limit applies only when
that optional layer is supplied.
The authoritative C# level model and Godot protocol model enforce the same
112×112 fine-grid, optional 40×40 coarse-grid, signed-byte transparency, and
byte-sized mega-tile ranges before those layers cross the shared protocol.
The Hellfire Na-Krul fragment now materializes its native final lever as an
anchor-relative authored behavior template. The server gates the lever on the
three-tome sequence and owns final quest completion; the third tome no longer
closes the quest prematurely. Its three authored tomes and final lever now also
declare native presentation identities for the compatibility client. Native
room reconstruction and collision/tile definition parity remain compatibility
seams.
The Snotspill Banner sign chest now uses the authored object path: its server
owned quest-variable gate generates the Banner item, so connected clients no
longer depend on the native local sign-chest pickup. It also declares the
native sign-chest presentation identity explicitly; native Banner map and
object resynchronization remain compatibility seams.
Native object presentation resynchronization now clears tile occupancy when an
authoritative activation or destruction hides an object at its existing
position, while preserving retained-visible activated objects on their new
authoritative position.
World-item snapshots now enforce DevilutionX's native `MAXITEMS` capacity of
127 active items per level across server, Godot, and native validation, closing
the corresponding fixed-pool overflow seam before presentation.
The Vile Betrayer center circle now materializes as an authored occupancy
trigger. After both linked books activate, a player entering the circle applies
the deferred center replacement, advances native `_qvar1` to 4, and receives the
server-owned player-origin Phasing transition. Occupancy-trigger behavior and
linked-object gates are reusable content fields; unactivated trigger tiles remain
enterable across snapshot restore, and secondary authored mutation follow-ups are
validated before activation. Native object resynchronization and encounter-
progress presentation remain compatibility seams.
The fixed Chamber of Bone completion books now clone validated authored
templates: the server owns quest completion, one Guardian level, and the native
player-origin Guardian target offset. Set-level quest progress gates resolve the
native set-level route identity while ordinary quest-level routing remains
unchanged. Their authored templates now also declare the native book and center-
circle presentation identities. The base Vile Betrayer fixed book placements now clone validated authored
templates: the server owns each activation, its native Phasing projectile
source/target transition, and the two authored replacement rectangles once the
optional native map bundle is materialized. Native object-resynchronization and
encounter-progress presentation remain explicit compatibility seams.
World mutations can now copy a validated native mega-tile rectangle and its
corresponding d-piece collision cells from a companion source level into the
active target level. This provides the generic server/data boundary needed for
Vile's pre-map replacement layer without embedding native tile values in the
engine; set-level mutations remain deferred when the licensed native map bundle
is not present.
Authoritative world mutations now support anchored native mega-tile
assignments and explicit creation of missing native transparency layers. The
base Snotspill Banner dialogue now applies the native opening transparency
rectangle through an anchored server-owned follow-up, while the hand-in stage
applies the final mega-tile/transparency transition. Native object/map
resynchronization and encounter-state presentation remain compatibility seams.
Authoritative unique-monster dialogue identities now also cross the native
projection into compatibility monster talk state; native speech/audio timing
and Banner object/map resynchronization remain seams.
Godot now resolves the same projected unique-monster dialogue identity against
the active quest and submits `TalkToQuestRequested`; the server remains the
authority for range, stage, inventory, rewards, and progression.
Godot's presentation now marks those same authoritative dialogue targets with
a gold marker without embedding native text or quest progression locally.
The native Lua lifecycle boundary is now centralized and characterized: an
authoritative launch never initializes or shuts down embedded Lua, while
explicit legacy launches retain the compatibility runtime.
Godot's shared monster position projection now treats a moving monster's
current and future cells as occupied, matching native target resolution for
combat, Telekinesis, and authored dialogue without client-side movement.
Godot target selection also preserves the native level-`0` visibility wildcard
for projected world entities and authored dialogue quests.
Godot world-spell targeting now follows native object/item/monster precedence
and rejects authored dialogue monsters as Telekinesis-style targets.
Normal Godot authored-object routing now also accepts native level-`0`
compatibility projections instead of requiring an exact level match.
The level-visibility wildcard is now centralized in one shared Godot protocol
projection used by targeting, authored-object/dialogue routing, and world
presentation, with focused invariant coverage.
Native monster, world-item, and object projections now use the same entity-ID
ordering as Godot selectors before presentation and interaction lookup, so
overlapping projected cells resolve deterministically across clients.
Godot world-item and authored-object target selection now applies that same
entity-ID ordering, including clicked item and portal hit-testing.
The Godot HUD's generic object, trap, and quest commands now use the same
visible authoritative-target filters instead of selecting arbitrary snapshot
entries, preserving fail-closed server ownership outside world click routing.

Authoritative store catalogs now carry content-declared native vendor roles;
server sale admission applies the native Smith and Witch buy filters and
rejects Wirt and Healer sales before generic pricing, while unknown legacy
store symbols remain generic for compatibility.

Authoritative vendor services now require the declared native role: Repair is
admitted only through Smith, Recharge only through Witch, and Adria service
sessions reject vendor sale, identification, and service mutations. Legacy
Generic catalogs remain compatible for custom-service fixtures.

The Adria service identity is also constrained to the active world-rule town
level for both opening and mana refill; server clients cannot carry the service
into dungeon levels.

World rules now externalize a separate Storyteller service-only store identity.
Paid Cain identification is admitted only through that town-level service (or
legacy Generic fixtures), preventing connected native clients from using a
different vendor role as an identification authority.

Connected native Cain identification now opens and closes that authoritative
Storyteller session around the legacy identification menu; offline Cain remains
unchanged.

The shipped Staff of Lazarus vendor-sale exception is now declared by the
normalized `NoSale` item tag. Authoritative Smith and Witch admission resolves
that content metadata first; the native item-index check remains only as a
compatibility fallback for older direct catalogs without normalized items.
Normalized oil action kinds likewise drive the Smith/Witch oil filter, with the
native misc-ID range retained only for older compatibility states.
Normalized item classification also owns quest-sale admission; the legacy
quest-index range is now consulted only when no item catalog is available.

Random unique-monster drop admission now honors normalized `Book` generation
tags even when the spell-book catalog is omitted; tagged non-book rows no
longer inherit the native `misc_id=25` book fallback. Untagged legacy rows
retain the compatibility fallback.

Recharge admission now suppresses the native staff misc-ID fallback when
unambiguous normalized item tags identify a misc-ID-only row as non-staff;
older untagged mappings retain compatibility behavior.

Vendor `NoSale` and `Quest` tags now resolve through unambiguous `misc_id`
mappings when native item identity is unavailable, preventing legacy vendor
classification from overriding normalized content metadata.

Vendor `Misc` tags now resolve through unambiguous `misc_id` mappings and
override conflicting legacy item-classification fields for Smith/Witch
admission.

The shipped spell-book catalog now requires every production book row to carry
explicit `single_player_excluded` metadata; native Resurrection/Heal Other
inference remains only a compatibility fallback for older packs.

The Godot character setup flow now accepts a validated authoritative server
host and port; environment-provided endpoints and resume-token launches remain
unchanged.

Connected native gold splitting now submits a server-owned `SplitGoldRequested` intent; the authoritative snapshot projects the source remainder and held cursor stack while legacy offline splitting remains unchanged.

Connected native inventory sorting now submits a server-owned `SortInventoryRequested` intent; the server applies the native size ordering and placement scan atomically while legacy offline sorting remains unchanged.

Connected native object-command routes now resolve only projected objects with
authoritative behavior. Ordinary presentation-only objects remain available for
rendering but fail closed before OperateObject, DisarmTrap, or object-targeted
Telekinesis commands are submitted.

Connected native town-opening helpers now also fail closed before legacy hive or
grave commands can mutate local town state; authoritative object operations
remain the connected route.

Connected native identify, repair, recharge, and oil cursor callbacks now also
fail closed at their direct entry points; stable-reference item-service
commands remain the connected mutation path.

Connected native dungeon-message playback now also fails closed during level
loading, preventing legacy Defiler quest mutation and quest resynchronization
commands from bypassing authoritative level projections.

Connected native level loading now preserves the server-selected player tile;
the legacy nearby-tile placement fallback is offline-only, so a transition
cannot rewrite authoritative movement state during presentation setup.

Connected native player initialization also skips its legacy view-position
placement fallback during level loading, preventing initialization from
rewriting the authoritative transition tile before snapshot projection.

Connected native level loading also skips legacy charger cleanup, preserving
server-owned monster combat modes while still retaining the cleanup for
offline level loads.

Connected native level loading also skips legacy portal resynchronization, so
stale native portal records cannot create presentation missiles outside the
authoritative portal projection.

Connected native missile presentation no longer clears legacy missile and wall
tile flags for server-owned projectiles; only locally simulated missiles update
that native gameplay bookkeeping.

Connected native object synchronization helpers now fail closed at their public
entry points as well as at protocol dispatch, preventing direct legacy delta,
operate, close, and break callbacks from mutating server-owned object state.

Authoritative debug chat hotkeys now ignore legacy `/lua` entries before they
reach console initialization; legacy mode retains the existing script hotkey
behavior.

Authoritative startup now excludes script-backed default and unpacked `init.lua` mods; legacy mode retains their discovery and reload behavior.

Connected native object interaction facades now also fail closed for direct
operate, break, trap, and Na-Krul room callbacks; local authoritative operate
requests still route through the server projection.

Connected native towner dialogue now fails closed before legacy talk handlers
can mutate local quest or store state; local authoritative dialogue and menu
requests retain their server-backed route.

Connected native monster dialogue now also fails closed before legacy quest
handoffs can mutate local inventory, quest, or monster state; authoritative
local dialogue retains the server-backed route.

Connected native trap-disarm callbacks now preserve cursor presentation but
fail closed before legacy trap flags can mutate; authoritative disarm intent
remains the only connected-session state transition.

Connected native portal delta projection now also fails closed at the portal
state setter, so a direct legacy delta callback cannot publish local portal
state outside the authoritative snapshot path.

Connected native portal transition helpers now fail closed before legacy portal
state can select a level or warp position; authoritative movement remains the
only connected-session transition source.

Connected native death-drop helpers now also fail closed at their direct entry
points, covering item respawn, gold splitting, and top-gold overflow while the
server owns inventory and world-item consequences.

Connected native equipment-damage helpers now fail closed before weapon decay,
shield/staff durability loss, or armor damage can mutate local inventory; the
server remains the sole connected combat authority.

Connected native player damage and death callbacks now also fail closed at their
direct entry points, while monster damage callbacks do the same; authoritative
combat and death presentation remain server-owned and offline sessions retain
the legacy lifecycle.

Connected native resource restoration, experience/level advancement, and direct
attribute or hit-point mutation helpers now fail closed as well. Authoritative
snapshot projection retains its dedicated level and field-assignment paths;
offline sessions retain the legacy player-state lifecycle.

Connected native missile creation now fails closed at `AddMissile` before any
legacy effect initializer can mutate player or world state. Direct native
resurrection, Heal Other, spell-consumption, and healer callbacks likewise stay
offline-only; authoritative projectiles, healing, resurrection, and resources
continue through snapshot and event projection.

Connected native inventory and equipment stat-recalculation helpers now also
fail closed. Authoritative item stat flags, resources, and derived player state
remain snapshot-owned while legacy recalculation remains available offline.

Connected native inventory, belt, and equipment removal helpers now also fail
closed at their direct entry points. Legacy automatic placement helpers use the
same boundary, while stable-reference inventory, belt, stash, and equipment
commands remain the connected mutation path.
Non-persisting automatic-placement probes remain available for capacity checks;
only connected legacy writes fail closed.

Connected native equipment replacement, inventory/belt cursor swaps, gold
placement, and staff-charge consumption helpers now fail closed at their direct
entry points as well. Authoritative item snapshots and item commands remain the
only connected source for those mutations.

Connected native store purchase, sale, repair, recharge, identify, currency,
and automatic-store-placement entry points now fail closed before legacy vendor
state can diverge from authoritative store commands. Read-only room checks
remain available to preserve connected vendor UI behavior.

Connected native manual and automatic world-item pickup actions now resolve the
projected world-item entity and submit `PickupWorldItemRequested`; legacy pickup
commands and direct inventory/world cleanup helpers remain offline-only.
Legacy floor-item synchronization helpers likewise return without creating
local world items while connected; authoritative world presentation still uses
its dedicated projection allocation path.

Native legacy delta-load helpers now also fail closed at their direct entry
points, covering spawned monsters, monster state, objects, and world items in
addition to the existing guarded delta dispatch.

Engine verification now includes a 64-generation ruleset replacement pressure
fixture that holds every prior generation under lease and drains them only
after all leases release.

Set-level native geometry now preflights partial binary bundles before
materialization, preserving metadata-only behavior when no bundle assets are
present.

Set-level map definitions now reject incomplete `.til`/`.sol` collision pairs,
while legacy metadata-only rows remain valid when neither asset is present.

Ordinary native-DUN level activation now validates every referenced `.dun` and
optional `.til`/`.sol` asset before publishing any level replacement, matching
the staged fragment activation boundary.

Authoritative portal catalogs now revalidate zero and duplicate portal IDs at
the world-validation boundary, including direct callers that do not originate
from TSV loading.

Runtime world construction now applies the same zero-and-duplicate portal
identity boundary before portal state can be published into a live session.
It also rejects contradictory static-versus-dynamic ownership and lifetime
metadata at that boundary.
Initial world-state collections now share the 4,096-entry per-category bound
used by save restoration, preventing oversized direct activation inputs.
The runtime initial-position gate also revalidates mutable portal dictionary
keys and metadata before a session can publish its starting state.
The same gate now checks all mutable entity-map keys, including projectiles,
against their server-owned entity identities.
It also rejects mutable projectiles with invalid lifetime, enum, or endpoint
state before the authoritative session starts.
Quest dictionary keys are likewise checked against their server-owned quest
identities before startup publication.
Participant templates now reject oversized native-compatible inventory,
equipment, belt, inventory-grid, memorized-spell, and stash collections before
runtime creation.
They also reject duplicate equipment and belt slot assignments.
Inventory-grid references and memorized-spell IDs/levels are validated at the
same participant-template boundary.

Legacy item-table spell names now resolve through the validated external

`native_spells.tsv` namespace during content activation; the compatibility
fallback remains available to older direct callers.
Legacy item-table miscellaneous symbols now resolve through the validated
external
`native_item_misc.tsv` namespace as well.
Legacy charged-staff initialization now resolves the `STAFF` miscellaneous
identity through that namespace, retaining the native fallback for older
callers.
Normalized unique-item and Hellfire affix powers now resolve through the validated
Hellfire item-effect namespace, retaining native flag fallbacks for direct
catalogs without that namespace.
Unique-item and affix powers with matching ordinary effect symbols now resolve through
the validated
`native_item_effects.tsv` namespace as well, including the
level-dependent StealMana, StealLife, FastAttack, FastRecover, and FastBlock
flags; direct catalogs retain native flag fallbacks without that namespace,
including elemental arrow and damage-channel powers.
Production content activation now rejects unique and affix power symbols without
an authoritative C# handler; direct catalogs remain permissive for compatibility.
Legacy item-table type, class, and equipment-location symbols now resolve
through the validated external
`native_item_topology.tsv` namespace as well.
Legacy item-table special-effect symbols now resolve through the validated
external
`native_item_effects.tsv` namespace as well.
Normalized Hellfire item-effect symbols now resolve through the validated
external
`native_item_hellfire_effects.tsv` namespace as well.
Legacy item-type generation tags now resolve through the validated external

`native_item_tags.tsv` namespace as well.
Spell-book cursor projections now resolve through the validated item cursor
catalog when authored by `cursor_symbol`.
Spell-book single-player exclusions now resolve through authored
`single_player_excluded` metadata when supplied, retaining native fallbacks for
older rows.
The Diablo content schema is now `1.434.0` for these table contracts; the
module continues to accept the immediately previous additive `1.433.0` schema
for existing manifest-based packs. Schema 1.431 adds the optional validated
`object_presentations.tsv` sidecar, keyed by authored object entity, so
activation visibility and animation frames can evolve without changing the
legacy `objects.tsv` header. Schema 1.433 adds set-piece-origin metadata for
authored object item rewards;
the server resolves these offsets from published origins and preserves them
through saves and protocol snapshots. Schema 1.428 adds the optional
`piece_property_overrides` column to `world_mutations.tsv`, allowing a
mutation to externalize validated native `.sol`-style collision flags for
d-pieces introduced by a set-piece transition. Schema 1.427 adds the optional
`procedural_set_piece_origins.tsv` table. Its validated set-level rules select
deterministic aligned walkable footprints from authoritative generated level
geometry and feed the existing relative-object materialization path; missing
target levels remain deferred. Schema 1.426 adds the optional `placement_mode`
column to `set_level_objects.tsv`: absolute rows preserve existing
coordinates, while `set_level_origin` rows use signed offsets from a
runtime-generated set-piece origin and remain deferred until that origin and
its geometry are available. Schema 1.425 adds paired world-item anchor
coordinates to quest types; the server completes a matching single-player
quest when an item is dropped on that level cell while retaining the item in
authoritative world state. Hellfire now also ships the native `cornerstone.dun`
fragment at level-21 coordinate `(14,14)`, so the Cornerstone map layer and its
item-placement quest anchor share one validated content contract. Activation
also rejects anchors outside or on blocked cells and rejects duplicate anchor
coordinates across quest states. Schema 1.424
adds deterministic coordinate
anchors to native DUN fragments and lets fragment object placements inherit
that anchor; procedural set-piece placement can now be authored without
inventing a runtime object solely to carry coordinates. Schema 1.423 adds authored object reward
columns; the Snotspill Banner sign chest uses them with its server-owned
quest-variable gate.

The authoritative object projection now carries additive presentation lifecycle
metadata. Optional `hide_when_activated`, `animation_frame`, and
`activated_animation_frame` values preserve legacy hide behavior by default,
allow authored native objects to remain visible after their one-shot server
operation, and select the post-activation frame across save/restore, hashing,
the protocol, native compatibility presentation, and Godot visibility. Existing
packs omit these columns and retain the previous behavior.

Content packs can now supply the same values through optional validated
`object_presentations.tsv` sidecars keyed by authored object entity. Activation
applies sidecar metadata to both ordinary object rows and behavior-only templates
before set-level or native-DUN placements clone them, so existing `objects.tsv`
overlay headers remain compatible.

Native-compatible eight-way direction sectors and coordinate-to-facing selection
are now implemented by the engine-neutral deterministic grid primitive; the
Diablo module keeps its existing public direction enum for source compatibility,
and authoritative combat, world movement, and monster placement now consume the
engine-owned displacement sequence and sector selection while extreme coordinate
differences are evaluated without integer wraparound.

The bounded native crawl order is likewise engine-neutral: Diablo's existing
`DiabloCrawl` facade delegates nearest-cell searches and displacement enumeration
to the shared primitive, including its 50-cell radius limit and overflow-safe
candidate handling.

Native rotated-grid projectile velocity construction is now also owned by the
engine-neutral assembly. Diablo retains its private source-compatible helper,
but all projectile callers share the same fixed-point scaling and mirrored
trajectory behavior.

The procedural level catalog now delegates its connected lattice sampling and
independent missile-occlusion hash layers to an engine-neutral generator;
Diablo still owns the external level definitions and authoritative world
instances, while other modules can reuse the deterministic layout primitive.

Native radial projectile fan-out ordering is likewise engine-owned. Diablo's
Nova and related controllers retain their source-compatible helper while using
the shared mirrored quarter-ring sequence and count truncation.

Deterministic authored set-piece placement now uses engine-owned 32-bit mixing
and aligned walkable-footprint selection. Diablo still owns set-level identity,
content validation, and world-origin records, while the reusable candidate
ordering and seed selection remain outside the game module.

Native movement-AI direction sweeps now use engine-owned alternating and
turn-pair candidate ordering. The Diablo module retains AI-specific admission,
occupancy, and movement state while sharing the exact RandomWalk-compatible
direction sequences across fear, stealth, counselor, and minion fallbacks.

Monster spawn planning now delegates its native descending Fisher–Yates order
to the engine-neutral deterministic ordering primitive. Diablo retains the
legacy random-stream facade and spawn-specific selection rules, while runtime
seed mixing is shared with other authoritative deterministic systems.

The remaining Diablo RoundWalk, Snake lateral, clockwise fallback, and moving
monster adjacent-step paths now use engine-owned direction ordering and
rotation. Diablo retains only source-compatible facades around those generic
primitives; no combat controller owns a duplicate direction ring algorithm.

Player-minion pursuit now delegates its goal-set breadth-first search and
neighbor ordering to the engine navigation primitive. Diablo retains goal
construction, occupancy policy, and minion identity while reusable path search
logic is no longer duplicated in the game module.

Complete native set-level map bundles now parse and validate both map variants,
their optional transparency layer, and the active-map spawn point during
content preflight, even when world activation is deferred.

Native `SelectChamber()` parity remains intentionally deferred: the native
`FirstRoom()` pass owns `VerticalLayout` and `HasChamber1/2/3`, while the
authoritative content packs do not yet contain the corresponding room-synthesis
inputs or licensed `.til`/`.sol` collision assets. Current Cornerstone and
`uberroom.dun` rows therefore use explicit validated anchors; no server path
claims to reproduce native random chamber selection until those inputs can be
externalized together.

Schema 1.421 adds optional occupancy-trigger and linked-object-gate fields to
`objects.tsv`. The server applies an authored object's normal validated effect
when a player enters its tile, after any declared linked quest objects have
activated; omitted fields preserve ordinary interaction behavior.

Schema 1.419 adds optional anchored native mega-tile assignments and explicit
transparency-layer creation to `world_mutations.tsv`. The assignments are
validated against the targeted level's mega dimensions and projected through
the authoritative level geometry; the transparency option preserves authored
set-piece transitions on metadata-only base levels.

Schema 1.418 adds optional `storyteller_store_id` and
`storyteller_store_symbol` columns to `world_rules.tsv`. The service-only
identity is validated separately from Adria and from stock-vendor identities;
older rows retain the native Storyteller default.

Schema 1.417 adds the optional `store_role` column to `stores.tsv`. Explicit
roles validate native vendor identities for authoritative sale admission;
legacy rows infer only known Smith/Witch/Wirt/Healer symbols and retain the
generic role for custom stores.

Schema 1.416 extends ordinary
`native_dun_levels.tsv` rows with optional
validated `.til`/`.sol` collision assets. Legacy rows remain object/mega-tile
metadata-only; rows with the pair now retain effective d-piece collision
provenance for later authoritative mutations.
Native DUN level activation prepares every replacement before publishing any
of them, so malformed later assets cannot leave a partially composed world.
Native DUN fragment activation now uses the same staged publication boundary,
while retaining ordered composition for multiple non-overlapping fragments on
one level.

The native authoritative client remains Lua-free at runtime: its console,
quick-message handling, and startup path expose only native diagnostics. Lua
REPL and compatibility aliases remain available only for explicit legacy mode.

Native `.til`/`.sol` fragment collision provenance now remains attached to
authoritative d-piece layers, so later `piece_layer_rects` mutations update
native-owned movement and missile cells without changing authored geometry.
Deferred fragments now parse and cross-validate their complete `.dun`/`.til`/
`.sol` bundle during content activation, before a unique encounter can publish
the fragment; malformed collision inputs therefore fail closed at pack load.
Ordinary native DUN levels now apply the same cross-asset validation to their
optional collision bundle before level replacements are published.
The historical `uberroom.dun` fragment entry denotes its native layer payload;
effective d-piece collision behavior still depends on the optional validated
`.til`/`.sol` definitions.

Connected native missile processing now keeps server-projected missiles on the
presentation path while discarding stale legacy missiles before their native
gameplay callbacks can run; offline sessions retain the legacy projectile
simulation.
Connected native towner processing now fails closed at its public helper so
local quest-derived towner state cannot bypass server-owned presentation;
offline sessions retain legacy towner animation and lifecycle behavior.
Connected native item processing now fails closed at its public helper so
legacy item animation and multiplayer tile cleanup cannot mutate server-owned
world-item state; offline sessions retain the legacy path.
Connected native quest ticking now fails closed at its public helper so legacy
portal creation, quest-variable transitions, and local level changes cannot
bypass server-owned world progression; offline sessions retain the legacy path.
Connected native trigger checks now use the shared legacy-world policy and fail
closed before stair, town-warp, or quest-entrance transitions can emit legacy
level-change commands; authoritative movement remains server-owned.
Connected native quest completion, set-piece drawing, and legacy multiplayer
quest synchronization helpers now fail closed as well; authoritative quest
state and world mutations remain server-owned.
Connected native monster spawn, golem, and death lifecycle helpers now fail
closed at their public entry points too; server-owned monster snapshots cannot
be bypassed by a direct legacy callback.
Connected native legacy level-transition helpers now fail closed at their
public entry points; authoritative movement and restart presentation continue
through the dedicated server-backed runtime path.
Public native simulation helpers now fail closed while connected, closing the
direct-call seams around the existing authoritative game-loop gate; offline
sessions retain the legacy movement, combat, lifecycle, trap, door, and other
world simulation.
Connected native Telekinesis now fails closed at its public legacy command
helper; authoritative target interaction remains server-owned even if a future
caller bypasses the current cursor-routing branch.
Connected native automatic pickup now fails closed at its legacy command
boundary; authoritative world-item pickup remains server-owned even if a
future caller reaches the legacy helper.
Connected native unique-monster conversations now avoid the legacy local
monster-mode mutation before authoritative dialogue routing; offline
conversations retain the native mode change.
Connected native Ctrl-click stash-to-inventory transfers now submit an
authoritative stable-reference move; the legacy local transfer remains offline-
only so the server owns both inventory and stash state.
Connected native inventory actions now fail closed before unsupported legacy
item-use fallbacks can remove or mutate local state; scroll and rune items retain
only their immediate authoritative cast-intent route.
Connected native gold splitting now submits the authoritative
`SplitGoldRequested` intent; disconnected sessions retain the legacy dialog.
Native player projections now accept protocol behavior `3` (Etherealize) status
effects and hydrate the native `SpellFlag::Etherealize` bit for local and remote
presentation; valid server snapshots no longer fail closed on this effect.
Connected native level-transition presentation also preserves server-owned Mana
Shield and Reflect state instead of clearing or rebroadcasting it from the legacy
`InitLevelChange` path; offline transitions retain the legacy lifecycle.
Connected native sessions now bypass the remaining legacy delta-state helpers
for monster, item, object, portal, level save/load, and delta transport paths;
authoritative snapshots remain the only connected-client source of truth.
Legacy quest resynchronization during level loads is likewise disabled for
connected sessions so quest-driven local world mutations cannot compete with
authoritative projections.
The return-level Betrayer marker also remains offline-only for the same reason.
Set-piece quest activation and Na-Krul room reconstruction are also offline-only
during connected level loads.
Legacy Cornerstone item restoration from local Hellfire options is offline-only
as well.
Connected native level loads now skip legacy random and quest-item generation.
Authoritative world-item projections remain the only connected-session source of
world-item state, and projected Cornerstone items no longer trigger the legacy
local quest-completion side effect.
The native authoritative world bridge now removes unclaimed local monster
presentation slots after each snapshot, so legacy level-load monsters cannot
remain visible or occupy tiles after server state is applied.
Connected native set-piece object initialization now leaves Blind, Blood, and
Warlord quest-message and map-fragment mutations offline-only; connected
sessions retain only presentation object setup until authored replacements
exist. The Snotspill Banner sign chest is the first connected set-piece object
to use the server-owned reward path.
Native monster level setup now also leaves the legacy Zhar-unavailable fallback
quest-state write offline-only; connected monster presentation is reconciled from
the authoritative snapshot.
Connected single-player level transitions now bypass legacy visited-level
`LoadLevel()` restoration in dungeon, town, and set-level branches; local flags no
longer select a second gameplay state while the server-backed runtime is active.
Connected single-player transition progress no longer writes legacy per-level save
files before a server-owned move; multiplayer delta saves remain protected by the
existing delta-state policy.
Native authoritative object projection now prefers a local compatibility object
at the server-projected position before falling back to object-type matching;
ordinary local objects remain presentation-only until their authored replacements
are available.
Connected native clients now hydrate legacy quest availability, completion,
log, variables, and speech fields from optional server-projected native quest
ordinals; snapshots without the additive mapping retain compatibility behavior.
Quest content activation also rejects duplicate native ordinal mappings before
they can make a connected snapshot ambiguous.
Connected native towner clicks now bypass legacy quest checks when opening
supported store menus and fail closed when no authored server interaction or
safe presentation menu exists.
Connected native unique-monster conversations now also fail closed when a
projected monster has no authored server dialogue, preventing legacy quest
activation, item consumption, and reward mutation.
The native debug towner entry point also fails closed in authoritative mode;
legacy debug interaction remains available only to offline sessions.
Native debug towner travel likewise remains offline-only so it cannot mutate
authoritative movement through a local teleport spell.
Native player-mutating debug commands (god mode, arrow effects, resource,
attribute, level, gold, and spell-level adjustments) are likewise offline-only;
their deterministic policy is covered by the native compatibility suite.
Native debug quest activation is also offline-only; connected quest diagnostics
remain read-only over the server-projected quest state.
Native debug level reset and dungeon, quest, and custom-map warps are likewise
offline-only so connected transitions remain server-owned.
Native debug item and monster spawning is also offline-only; connected world
items and monsters remain projections of server state.
Native object operation now also fails closed when the selected local object
has no matching authoritative projection instead of invoking legacy behavior.
Connected native death presentation now suppresses legacy death messaging and
local death-drop synthesis; gold, equipment, held-item, and ear consequences
remain server-owned and arrive through authoritative projections.
Connected native player processing also suppresses the legacy periodic
`DRAINLIFE` equipment damage tick; the server-owned combat tick remains the
sole source of that damage and any resulting death.
Connected native Mana Shield creation and removal likewise remain offline-only;
connected shield state is applied from authoritative snapshots without legacy
`CMD_SETSHIELD` or `CMD_REMSHIELD` lifecycle commands.
Connected native Reflect charge mutation and legacy `CMD_SETREFLECT` broadcasts
are likewise offline-only; connected charges remain server-owned projections.
Protocol item presentation now resolves the active catalog's `Ear` generation
tag before applying the legacy PvP-ear identity fallback, keeping normalized
content authoritative for the wire presentation kind while preserving older
catalog-less snapshots.
Smith/Witch vendor admission likewise treats non-empty normalized generation
tags as authoritative for `Misc` and `Quest` classification; legacy item-class
fallback remains available only for definitions without classification tags.
Native compatibility item projection now applies the server-owned Gold/Ear
presentation identity to native item fields before presentation, so conflicting
legacy item type, class, misc ID, or index values cannot change the projected
item family.
Semantic item classification now applies the same case-insensitive tag contract
to normalized definitions and custom content overlays, including misc-ID
resolution and equipment-slot classification.
Godot world presentation now retains and draws the server-owned persistent corpse
tiles from complete snapshots, using the shared level-visibility rule and keeping
corpse lifecycle entirely authoritative.
Godot portal presentation, click routing, and interaction-range validation now
share exact server-owned source and bidirectional destination level identities;
level zero remains the real native town level rather than a visibility wildcard
for portal admission.
Godot authoritative level geometry is now replaced rather than merged: omitted
levels and geometry-less legacy snapshots cannot retain prior-session blocked
cells or warp markers, while static layouts remain a presentation fallback.
Godot cached item and projectile positions now use the active-level visibility
boundary, and feedback anchors refuse off-level entities instead of presenting
transient labels from another projected level.

## Current state

Authoritative server startup now accepts an optional `--starting-level` override
for newly created participants. This is a development and fixture control only;
when omitted, the active content pack's validated default level remains in use.

| Area | State | Remaining work |
| --- | --- | --- |
| Protocol and sessions | Operational vertical slice with one generated C# protocol assembly shared by Server and Godot, including self-first complete multiplayer snapshots, additive live authoritative level-geometry projections, live authored progression-warp transition through server-owned movement with same-session resume retention, live authored quest/towner dialogue submission through the Godot protocol model, per-session class selection, class-skill/learned-spell state, explicit cast sources, resume precedence, live single-player and multiplayer handshakes/cast slices, a twelve-cycle server soak plus an eight-cycle live Godot-client reconnect soak while another participant remains connected, live Godot two-participant process-restart restoration, protocol v0.2 independent content/ruleset handshake identities adopted by production launchers while preserving the v0.1 alias, server-owned adjacent melee plus line-of-sight ranged player-versus-player admission with native timing, physical/elemental damage, target resistance, shield/class blocking, equipment knockback, lethal death/ear handling, targeted Firebolt/Charged Bolt/Bone Spirit admission for living remote players, live Firebolt, Charged Bolt, Bone Spirit, Heal Other, and Resurrection player-target coverage, live two-client coverage including effect-tick shield resolution, typed Diablo victory events for client presentation, exact additive native `NovaBall` projectile visual identity, exact additive Godot `NovaBall` presentation identity with behavior fallback for older snapshots, and Normal/Nightmare/Hell difficulty projection in authoritative snapshots | Add broader reconnect/multiplayer fixtures and production-client soak coverage |
| Deterministic replay | Shared C++/C# gate hashing complete multiplayer participant state, with server-owned corpse tiles, native per-tick monster regeneration, refreshed ordinary-death and spell-timeline checkpoints, deterministic quest-pool selection/rejection, a 1,000-tick autonomous regeneration soak, multiplayer autonomous-tick participant fixtures with explicit secondary-session movement, melee-attack, and portal-transition routing, shared melee and ranged elemental-equipment fixtures covering native fire/lightning and arrow channels, resistance, and RNG, native/C# chained-action timing coverage for first-frame attack skips, seeded and autonomous GoatRanged and SkeletonRanged MonsterArrow admission/travel/impact/removal parity including secondary-participant routing, Hellfire HorkDemon Hork Spawn admission, nearest-participant target selection, and decision-delay parity, autonomous Succubus BloodStar admission, nearest-participant target selection, travel/player-impact parity with deterministic monster-magic hit/block RNG, Q6 damage, and block-animation state, plus Lich-family MonsterMagic admission/travel/player-impact parity for deterministic hits, misses, and secondary-participant impacts, live protocol coverage for the checked-in Hellfire encounter and Sorcerer loadout overlays, plus composed-manifest Hellfire ranged replay and shared Firebolt, Fireball, Bone Spirit player-target, and Bone Spirit homing/resistant-monster replays | Expand broader multi-actor autonomous and reconnect replay coverage |
| Stores and inventory | C# remote for vendor actions, item services, spell-book, potion, rejuvenation, elixir (including Hellfire Spectral Elixir), and Blacksmith/Accuracy/Sharpness/Fortitude oil consumption, authoritative inventory relocation, and server-owned stash pages (100 native-compatible 10x10 grids) with stable page/cell item references, persistence, overlap validation, inventory↔stash transfers, explicit open/close session control, and authoritative stash-gold deposit/withdraw commands; stash-targeted Repair, Recharge, and Identify commands now require the server-owned stash session; stable item references remain carried by native and Godot clients; content-backed moves enforce native equipment-slot topology and belt footprint rules, normal native equipment drags preserve the selected slot through atomic `AutoEquipItemRequested`, all native automatic equipment, inventory, belt, potion, rejuvenation, elixir, and oil paths use stable server-owned intents, the server enforces active-arena-only Arena Potion admission, and the Godot HUD submits inventory or belt sources as stable references for books, targetless consumables, two-item oils, Cathedral Maps, and authored quest notes through the same protocol; native authoritative stash entry projects validated page grids, session state, and server-reconciled gold, and native normal, automatic, and Ctrl-click stash transfers now submit stable page/cell moves; non-consuming item dialogues resolve their message identities from validated content | Add broader live-client coverage and exact legacy edge-case characterization |
| World gameplay | C# remote vertical slice with content-owned quest item transitions, generic authored towner turn-ins (including item-consuming activation-only, non-consuming dialogue-only, completion-stage, alternate-item, and normal-item rewards), repeatable content-owned quest/towner stages (including all four Black Mushroom/Adria stages and Ogden's post-completion King Leoric dialogue), explicit itemless and completed-quest interaction availability, server-owned per-player visited-level state for native `_pLvlVisited` gates, unique-monster quest dialogue and authored follow-up variable transitions, activation rewards, and hand-ins, content-declared quest-object effects with validated quest activation/completion, native variable transitions, and one-level spell grants, validated content-backed world-cell mutation overlays persisted with the session and projected through effective level geometry, and the Hellfire Na-Krul tome completion's deterministic server-owned unique spawn, native weakening transition, anchor-relative four-value native d-piece room-opening mutation, and validated `uberroom.dun` native mega-tile/d-piece fragment; the native authoritative client submits `TurnInQuestRequested`, `TalkToQuestRequested`, and `OperateObjectRequested` and projects server dialogue/events (first shipped base Skeleton King route/completion and Butcher activation/completion, Hellfire Farmer activation/post-Hive reward, base Cain/Lazarus staff hand-in and Lazarus Storyteller completion stage, Hellfire Grave Matters/Barmaid dialogue, base and Hellfire Magic Rock and Anvil/Blacksmith, base Ogden's Sign, Little Girl, Jersey's Jersey, Defiler, Zhar, Warlord, Lachdanan, and Na-Krul paths) | Expand AI, level generation, remaining quests, dialogs, towners, environmental behaviors, complete native Na-Krul collision/tile-definition parity beyond the projected fragment layer, then externalize the remaining set-piece mutation, book/lever, placement, projectile, and map behaviors not yet covered by the Chamber of Bone and Vile Betrayer templates; expand multiplayer coverage |
The optional shared native geometry tables and their deferred-fragment
publication path are now implemented, including set-level-origin activation and
snapshot restore. Remaining world-gameplay work concerns native parity beyond
those validated geometry contracts.

| Persistence | Server-owned snapshot saves with an explicit version-1 player-save compatibility codec, integrity-hash verification, fail-closed player/session/world-state import validation, validated saved-class reconstruction for cold sessions, persisted per-player native-compatible `visited_level_ids`, immediate post-handshake session persistence, live process-restart restoration through a fresh TCP host/adapter with resume-token precedence, an atomic combined per-session checkpoint with a version-3 canonical SHA-256 state and command-ledger integrity envelope, version-1 and version-2 checkpoint compatibility with next-write upgrade, compatibility fallback to separate files, a four-cycle fresh-host/adapter recovery soak that preserves canonical snapshot hashes and replays every accepted command while retaining durable receipt identities and exactly-once replacement execution, command-synchronized state/receipt exports, concurrent multi-instance atomic-save publication coverage, and a durable save-root world seed that keeps generated geometry stable across restarts | Add broader recovery/soak testing and future migrations for additional persisted metadata |
| Native client | Explicit legacy or authoritative runtime mode; connected native character-panel attribute allocation now submits server-owned stat-point intents and applies projected stat points/resources, with legacy `CMD_ADD*` retained only when disconnected; connected native stash-close drops now submit the authoritative held-item drop intent, native mouse/controller dungeon talker interactions now submit authoritative quest dialogue, controller item/object projections fail closed when their entity IDs are unavailable, and controller quest-target movement submits authoritative target cells; connected controller monster, remote-player, and object interactions now resolve projected targets to authoritative entity IDs with fail-closed handling for missing projections, including stale highlighted objects that no longer have a projected entity; normal item-cursor inventory/belt placement uses stable-reference transfers, normal equipment drags carry the selected slot through atomic `AutoEquipItemRequested`, automatic inventory/equipment/belt relocation remains server-owned, connected native spell casts now honor server-projected tile, player, combat-entity, and world-entity targeting roles while preserving legacy behavior for older snapshots, and content-declared towner/unique-monster/quest-object interactions submit `TurnInQuestRequested`/`TalkToQuestRequested`/`OperateObjectRequested` with server dialogue/event projection, including repeatable Black Mushroom/Adria stages, server-owned `visited_level_ids` for native `_pLvlVisited` gates, and Ogden's itemless post-completion King Leoric dialogue (first shipped by the base Skeleton King route/completion and Butcher activation/completion, Hellfire Farmer activation/post-Hive reward, base Cain/Lazarus staff hand-in and Lazarus Storyteller completion stage, base and Hellfire Magic Rock and Anvil/Blacksmith, Ogden's Sign, Little Girl, Jersey's Jersey, Defiler, Zhar, Warlord, Lachdanan, and Na-Krul paths); presentation-only remote-player sprites now draw from authoritative snapshots without entering native simulation pools, including action-tick animation timing, automap arrows, party-panel portraits, party-panel hover/right-click entity routing, and projected inspection stats/spells/item layout | Continue removing compatibility-pool assumptions as Godot presentation matures; broaden interpolation soak coverage and finish legacy interaction parity |
| Godot client | Complete for protocol-boundary demonstrations, including typed Monk Search, Rogue Trap Disarm, and Etherealize presentation, live Etherealize, Barbarian Rage, Monk Search, Rogue Trap Disarm, Bard Identify, Warrior Repair, and Sorcerer Recharge service coverage, stable-reference Repair/Recharge/Identify targeting for items in an open authoritative stash, a stable authoritative spell-source selector with server-owned memorized and item-backed spell display names, validated selection and submission of adjacent content-owned quest/towner stages, server-authored unique-monster dialogue target selection and `TalkToQuestRequested` routing, fail-closed cross-validation of authored interaction quest references, contextual world-click routing for portals, authored objects, revealed traps, and authored quest/towner stages, ordinary presentation-only objects excluded from command routing, server-driven victory presentation from typed Diablo defeat state, same-instance reconnect with bounded automatic retry, right-click authoritative monster and remote-player attack intent routing, behavior-driven cast-cursor remote-player targeting without client spell-ID tables, stable authoritative monster entity targeting for spell casts, stable world-entity targeting for Godot Telekinesis casts, live entity-targeted monster, player attack, Firebolt, Holy Bolt against an authored undead target, Heal Other, Resurrection, server-owned attribute-allocation action coverage, and authoritative difficulty presentation with live Hell projection coverage, server-owned stealth target presentation, authoritative player action indicators, typed projectile presentation families, additive authored warp-trigger markers with destination geometry validation and unavailable-route feedback, an in-client class/name setup form that submits the initial authoritative identity, all-direction action-tick walk interpolation boundary coverage, authoritative stash session/gold controls with paged 10x10 grid presentation and server-routed stable-reference moves, and server-authored activated-object lifecycle-frame presentation | Production assets, broader prediction, menus, packaging, and complete play flow |
| Domain kernel | Independent dependency-free .NET 8 assembly | Extract additional game-neutral deterministic primitives only where reuse warrants it; keep Diablo state in its module |
| Data runtime | Independent .NET 8 assembly with ordered overlays, schema identities, dependency-version constraints, strict TSV Boolean parsing, immutable native `.dun`/`.til`/`.sol` asset composition and hashing, and transactional activation, depending only on Domain | Document future table extensions and broaden reload fixtures |
| Engine composition | Independent .NET 8 assembly with generic deterministic lifecycle, module compatibility preflight, transactional candidate preparation, atomic candidate registration, coordinated ruleset publication, generation leases with explicit retired-target draining, reusable bounded grid navigation, native-compatible bounded grid crawl ordering and overflow-safe nearest-cell search, native-compatible eight-way direction sectors and coordinate-to-facing selection, overflow-safe Chebyshev grid-distance metrics, deterministic bounded overflow-safe Bresenham line traversal, native-compatible deterministic legacy LCG, native-compatible signed 26.6 fixed-point arithmetic, and deterministic non-zero entity-ID allocation, depending only on Domain and Data | Keep filesystem/reflection outside Engine and broaden replacement fixtures |
| Diablo module | Independent .NET 8 assembly with content activation, transport-neutral simulation, and state capture/restore, depending only on Domain, Data, and Engine; participant templates and save restore fail closed on invalid scalar resources/actions, attribute state, item identity metadata and duplicates, equipment/belt slots, and malformed item dimensions or inventory footprints while retaining legacy character-level normalization; save restore remains the authoritative stash placement validation boundary | Expand exact legacy gameplay/content coverage and register future capabilities through Engine |
| External content | Explicitly manifested base and Hellfire variant packs plus schema-aware, dependency-versioned composite-key overlays, including the validated complete native
`native_spells.tsv` namespace, native
`native_missiles.tsv` namespace, native
`native_missile_sprites.tsv` timing metadata, native
`native_item_misc.tsv` namespace, and native
`native_item_topology.tsv`/
`native_item_effects.tsv`/
`native_item_hellfire_effects.tsv`/
`native_item_tags.tsv` namespaces for item compatibility, normalized Hellfire effects, and legacy generation tags, all six native-compatible classes, their complete native animation timings, complete initial loadouts, authoritative definitions for every directly castable starting source, all six validated cursor-driven class capability identities (Repair, Trap Disarm, Recharge, Identify, Search, and Rage), native level-range generation metadata, optional `procedural_levels.tsv` definitions with deterministic connected movement geometry, independently seeded missile occlusion, and optional independently seeded native-vision blockers from row and world seeds; multiple authored required-walkable entry cells clear all three effective blockers, validated level-aware monster candidate selection, a bounded server-owned seed-stable spawn planner exposed through Diablo content activation and optionally consumable as generated combat targets with deterministic validated drops, unique/Diablo encounter flags, native death-animation durations, native experience rewards, native normal, special, and magic-projectile monster attack accuracy, native monster resistance metadata, the validated Hellfire Hork Demon AI identity, Hork Spawn missile mapping and server-owned speed-8 nine-tick expiry summon controller, the validated item presentation-name projection, shared item cursor-symbol catalog, unique-item cursor projection, item random-drop minimum-level metadata, weighted drop-rate metadata, explicit usable-item metadata for consumable source admission, normalized item armor-range metadata, symbolic normalized native special-effect metadata, 22 native spell-backed scroll consumable rows, explicit Identify item-action metadata and authoritative consumable-source handling, validated item-action magnitudes, nine Hellfire quest and miscellaneous item identities, the Hellfire full-potion and rejuvenation family, Hellfire oil and elixir consumables, complete normalized base armor, standard weapon, generic ring/amulet, and Arena Potion rows, corrected native scroll projections, four normalized dynamic spell-book templates, validated metadata and deterministic generation for all 23 native bookable spells, authoritative `ReadBookRequested` learning/consumption behavior, integrated store/static-drop/procedural-drop/quest-reward spell-book generation, explicit native DUN object-layer mappings with deterministic object materialization, and the Hellfire Blood Star, Bone Spirit, Fire Wall, Lightning Wall, Flame Wave, Inferno, Chain Lightning, Nova, Apocalypse, Rage, Elemental, Guardian, Town Portal, Reflect, Magi, Mana, Ring of Fire, Immolation, Jester, Berserk, Warp, Golem, and Etherealize spell behaviors | Expand generated geometry toward native map synthesis, externalize remaining native spells, unique Hellfire quest behavior and rewards, and remaining item metadata; ship authored native DUN object mappings for remaining set-piece objects and extend the table reference as new schemas land |
| Gameplay modules | Typed dependency-ordered capabilities, inclusive semantic dependency-version and Engine API ranges, atomic activation, preflight diagnostics, a first-party Diablo factory, explicit hash-verified Server packages, and repeated 64-generation replacement/retirement coverage | Add further first-party capabilities |
| Host adapters | Independent .NET 8 generic Host and Diablo adapter assemblies with exact dependency tests; composition-only .NET 10 executable; optional configured factory-ID selection with strict compatibility validation; explicit hash-verified `game-host-adapter.json` packages with canonical entry/dependency paths, bounded private dependency closures, shared-contract identity, factory-type validation, duplicate package-ID rejection, repeatable `--game-adapter-package` discovery, the first production `Devilution.Server.Diablo.Package` entry point exercised through its isolated private dependency closure, live Godot selected-package smoke/reconnect coverage, selected-package process-restart persistence coverage, optional shared-contract SHA-256 compatibility checks with mismatch rejection, and reproducible PowerShell artifact staging | Exercise versioned rollout/upgrade coverage |
| Lua removal | Hellfire archive activation and the native post-bootstrap reload pipeline no longer require Lua; compatibility inventory established; authoritative data extensions use validated overlays; player/display diagnostics, item and monster spawning/diagnostics, towner interaction, level export, TRN selection, level reset/quest-warp/custom-map warp and seed inspection, automap reveal/hide and search, player rejuvenation, player health/mana adjustment, attribute max/min, level-up, player gold adjustment, spell-level adjustment, and quest diagnostics now use a deterministic native command registry; server boundary tests reject Lua/sol2 references from the authoritative C# source and project path and reject Lua assets or manifest references in the active base and Hellfire packs | Replace remaining native debug/admin and compatibility script hooks, then remove the Lua REPL and presentation capabilities |

## Release verification baseline

On 2026-08-21, the current Release artifacts passed the managed boundary and
client suites without failures: Domain 11 tests, Data 26, Engine 43, Diablo
1,452, Server 437, and Godot protocol 223. The rebuilt native replay runner
passed 96 of 96 tests and the managed replay fixture class passed without
failures. These counts are a dated verification baseline, not a substitute for
rebuilding the projects before a future run.

The Diablo module suite was rerun after the Na-Krul parity-characterization
correction and passed 1,452 of 1,452 tests; the other counts above remain from
the original Release baseline.

On 2026-08-27, the focused native `ServerBackedPlayerSnapshot.*` suite passed
107 of 107 tests, including the compatibility-pool world-item/object cleanup
regression.

On 2026-08-26, the current Debug Diablo module suite ran 1,828 of 1,839
tests successfully. Three failures were stale characterization expectations:
the lethal gold-stack seed now follows the monster attack's preceding native
RNG draw, a full inventory intentionally uses the held cursor for pickup, and
authored Na-Krul health must not receive single-player halving twice. The
remaining eight failures were controller-dispatch precedence cases where
validated Gargoyle, Skeleton King, or Rhino tuning was bypassed by a legacy AI
label. On 2026-08-27, after correcting that precedence, the full Debug Diablo
module suite passed 1,856 of 1,856 tests.

The live Godot protocol suite also verifies that a Hell difficulty selected by
the server changes the authoritative Hellfire monster snapshot (including its
scaled hit points), not only the client's difficulty label.

## Implemented authoritative systems

- Snapshot restore now hydrates omitted authored item values, base damage,
  durability, maximum charges, spell sources, and item actions across inventory,
  equipment, belt, stash, held, world-item, and store state without changing
  stable or native item identity; zero current charges remain preserved as a
  legitimate depleted-item state.
- Live world-item pickup now materializes the same authored metadata before
  mutating player state.
- The native debug console now stays script-free in authoritative mode, using
  only the registered native command list and retaining the legacy console
  behavior for explicit compatibility launches.
- Authoritative store presentation now skips the legacy `StoreOpened` Lua event
  callback; explicit compatibility launches continue to deliver it for legacy
  mods.
- Authoritative data reload, game-start, and frame-complete event-bus emissions
  now skip legacy Lua callbacks; explicit compatibility launches continue to
  deliver those lifecycle events for legacy mods.
- Snapshot and live world-item hydration now also restores omitted authored
  presentation names and native cursor graphics without replacing explicit
  unique-item overrides.
- Compatibility item hydration also restores omitted immutable classification,
  attribute requirements, native index, and authored effect flags whenever
  stable/native identity resolves, preserving nonzero runtime state.
- Godot item descriptions now honor the authoritative identified flag and
  identified-name projection while retaining a safe fallback for older state.
- Godot now rejects ordinary player-item footprints outside the native 10×4
  grid before presentation layout can silently relocate or clamp them.
- Godot also rejects duplicate nonzero player item seeds across inventory,
  belt, equipment, stash, and held-item projections.
- Godot now rejects impossible core item value, damage, armor, charge,
  durability, and elemental-damage ranges before presentation consumes them.
- Godot now cross-validates the active vendor session and rejects duplicate
  stock slots, prices outside the native range, and invalid stock identities.
- Godot now validates native inventory-grid references and belt/equipment slot
  topology while retaining omitted zero-length legacy grids.
- The live Godot authoritative-server fixture manifests now use the shipped
  `devilution.diablo@1.421.0` schema, keeping overlay startup coverage aligned
  with the active base and Hellfire packs.
- Godot now validates stable spell IDs, native spell levels, targeting enums,
  display-name NULs, duplicate memorized IDs, and the native memorized-spell cap.
- Godot now validates native-compatible gold, stash-gold, stat-point, Reflect,
  resource-base, and character-level ranges while retaining zero defaults.
- Godot now validates projectile source/lifetime identity, damage channels,
  dynamic damage ranges, lifecycle codes, and typed visual identities.
- Godot now validates monster damage ranges, resistances, hit chances,
  death-timer relationships, ownership, and bounded native combat metadata.
- Godot now validates adjacent monster movement destinations and stationary
  future-cell retention before interpolation consumes the snapshot.
- Godot now validates object identity, typed effect/presentation kinds, and
  breakable/missile-passability/destroyed-state relationships.
- Godot now validates signed-native world-item prices and typed item
  presentation/source/action/targeting metadata before pickup or HUD routing.
- Godot now validates native tile-coordinate representability for all projected
  entities even when optional level geometry is absent.
- Godot now validates projected player movement destinations against supplied
  level bounds and blocked cells, while retaining older geometry-free snapshots.
- Godot now validates that non-empty visited-level projections include the
  player’s current level before authored quest gates consume them.
- Godot now validates complete, non-negative player attribute projections when
  the additive attribute message is present.
- Godot now validates the selected ability targeting enum while retaining its
  compatibility-safe unspecified default.
- Native authoritative projection and UI validation now reject undefined
  ability, memorized-spell, and item targeting enums before native conversion.
- Native and Godot authoritative item projections now reject unsigned prices
  outside the legacy native integer range before applying player or world state.
- Native authoritative projection and UI validation now reject duplicate
  nonzero player item seeds across inventory, equipment, belt, stash, and held state.
- Native and Godot player-item projections now reject incomplete item identity
  metadata before converting or applying inventory, equipment, or belt state.
- Native item conversion now rejects invalid damage, armor, charge, durability,
  typed metadata, and NUL-containing spell-display ranges before narrowing.
- Native authoritative object projection now mirrors the Godot object boundary,
  rejecting invalid effect and presentation kinds, quest-transition and reward
  metadata, projectile offsets and sources, and quest-sequence follow-ups.
- Native authoritative portal projection now rejects non-native endpoint
  coordinates and inconsistent owned-portal lifetime or owner state.
- Native, server, and Godot authoritative boundaries now enforce the native
  `MAXPORTAL` array capacity, so no side can admit more than four routes.
- The authoritative world and save paths now enforce the same capacity before
  projection, and Town Portal creation fails closed when all native slots are
  occupied.
- Native transparency snapshots now accept the full low-byte wire range on all
  three clients, while values above 255 fail closed before presentation.
- Native authoritative quest-state validation now rejects duplicate or missing
  quest identities, impossible progress, unavailable progress, and invalid
  native quest metadata before interaction gates consume the snapshot.
- Native authoritative quest/towner interaction validation now rejects invalid
  IDs, quest references, messages, ranges, and geometry positions before routing.
- Native authoritative geometry validation now rejects malformed dimensions,
  cells, layers, duplicate levels, and warp-trigger routes before projection.
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
- Native authoritative snapshot application now commits projected player,
  world, metadata, quest, geometry, and vendor state transactionally, so a
  malformed later projection cannot leave earlier session state partially
  applied.
- Native authoritative stash item use now routes spell books, supported
  consumables, and oils through stable server-owned item intents; unsupported
  stash actions fail closed instead of invoking legacy local gameplay.
- Native authoritative held Rune Bomb and Cathedral Map interactions now route
  through projected server-owned dungeon objects; missing projections fail
  closed instead of opening local set pieces.
- Native authoritative inventory Cathedral Maps now route standalone map use
  through `UseConsumable`; when no projected grave interaction is available,
  the server emits `MapOpenedEvent` and the client presents the map without a
  local DoomFlag mutation.
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
- Connected native inventory sorting now submits the authoritative
  `SortInventoryRequested` intent; disconnected clients retain the legacy sort.
- Connected native visual-store openings now hydrate basic Smith, Witch, Healer,
  and Wirt stock from the server before rendering; the unsupported premium tab
  remains fail-closed instead of exposing stale legacy stock.
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
- Connected native clients now ignore legacy player-info, level, and remote
  attribute synchronization messages; authoritative snapshots own presentation.
- Connected native clients now ignore legacy movement, targeting, attack, spell,
  talk, and object-action messages; typed server intents own simulation.
- Connected native clients now ignore legacy monster-delta synchronization and
  debug-experience mutation messages; authoritative snapshots own progression.
- The authoritative Diablo save boundary now rejects the same out-of-range item
  prices before restoring player, world, or active-store state.
- Native authoritative projection now validates that non-empty visited-level
  state includes the player’s current level before applying native quest gates.

- Schema 1.395 externalizes Counselor adjacent Flash-admission chance bands
  through `monster_types.tsv`; shipped Counselor-family rows carry native 20/2
  tuning and older packs retain the compatibility fallback.

- Schema 1.396 externalizes the native monster Flash damage multiplier through
  `spells.tsv`; the shipped Flash row carries the native level-times-two rule
  while older packs retain the compatibility fallback.

- Schema 1.397 externalizes the native unique-monster experience multiplier
  through `combat.tsv`; the shipped combat row carries the native two-times
  reward rule while older mappings retain the compatibility fallback.
- Schema 1.398 externalizes native spell-book template item-level eligibility
  through `items.tsv`; shipped templates carry explicit thresholds while older
  mappings retain the native compatibility fallback.
- Schema 1.399 externalizes the native MagmaBall initial launch advance through
  `monster_missiles.tsv`; the shipped row carries three velocity quanta while
  older mappings retain the compatibility fallback.
- Schema 1.400 externalizes the native Counselor Charged Bolt projectile count
  through `monster_missiles.tsv`; the shipped row carries three projectiles
  while older mappings retain the compatibility fallback.
- The shipped Counselor Charged Bolt missile row now declares its native
  fifteen-point payload through the existing validated `projectile_damage`
  field; older rows retain the server fallback.
- Schema 1.401 externalizes the native class-specific ranged-arrow speed bonus
  through `classes.tsv`; shipped Rogue, Warrior, and Bard rows declare their
  native level divisors while older class tables retain the compatibility
  fallback.
- Schema 1.402 externalizes Skeleton King summon candidate selection through
  `monster_types.tsv`; shipped rows declare eligibility while older mappings
  retain the native-AI and complete-tuning compatibility fallback.
- Schema 1.403 externalizes the native Multiple Arrows count through
  `combat.tsv`; the shipped combat row declares three arrows while older
  mappings retain the native compatibility fallback.
- Schema 1.404 externalizes the native Multiple Arrows follow-up delay through
  `combat.tsv`; the shipped combat row declares two ticks while older mappings
  retain the native compatibility fallback.
- Schema 1.405 externalizes the native Multiple Arrows perpendicular spread
  distance through `combat.tsv`; the shipped combat row declares one cell while
  older mappings retain the native compatibility fallback.
- Schema 1.406 externalizes Arena Potion admission through the optional
  `arena_only` item field; the shipped row declares arena-only behavior while
  older item mappings retain the native misc-ID fallback.
- Spell-book template generation and `ReadBook` admission now honor the active
  content definition's `Book` item tag; mappings without item tags retain the
  native misc-ID fallback.
- Unique-monster random-drop filtering now honors authored `Gold` and `Book`
  item tags when active catalogs are present, while tag-less mappings retain
  the native item-type and misc-ID fallbacks.
- Recharge admission now honors active item spell-source metadata and non-empty
  staff tags before considering legacy staff misc IDs; tag-less mappings retain
  the native fallback.
- Consumable action magnitudes now resolve through stable or native item
  catalog identity, preserving the legacy Spectral Elixir amount only when no
  content definition can be resolved.
- Consumable and book action admission now resolves authored action kinds by
  stable or native item identity when compatibility state omits that field.
- `ReadBook` now resolves authored spell identity and minimum-magic requirements
  by stable or native item identity when compatibility state omits those fields.
- Recharge validation and pricing now resolve authored maximum charges and spell
  identity through stable or native item metadata when compatibility state omits
  those fields.
- Death-gold classification now honors active non-empty item tags before the
  native type/index fallback.
- Belt classification now honors active non-empty item tags before the native
  equip-location/item-class fallback.
- Two-handed and equipment-slot classification now honors active non-empty slot
  tags before the native equipment-location fallback.
- Staff classification now honors active non-empty staff/non-staff tags before
  the native item-type fallback.
- Weapon classification now honors active non-empty weapon/non-weapon tags
  before the native item-class fallback.
- Weapon and armor subtype classification now honors active non-empty subtype
  tags before the native item-type fallback.
- Weapon and armor subtype state classification also resolves unambiguous
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
- Sharpness Oil now hydrates omitted target weapon damage from authored item
  metadata before applying its native increment.
- Store sale, repair, and recharge prices now resolve omitted item values from
  unambiguous authored metadata, including compatibility states keyed by
  `misc_id`.
- Equipped physical damage now resolves omitted weapon ranges from authored
  item metadata before calculating the server-owned player damage profile.
- Equipped fixed armor now resolves omitted armor values from authored item
  metadata, and seeded randomized armor ranges restore their native inclusive
  roll; seedless compatibility states remain unresolved.
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

- Staged unique-monster dialogue now retains the next authored message identity
  on the server-owned monster state and protocol snapshot. Gharbad and Warlord
  conversations therefore preserve their current dialogue target across
  snapshot/reconnect boundaries; terminal stages clear the identity while the
  existing native AI compatibility behavior remains unchanged.

- Legacy unique-monster item hand-ins now retain their authored initial and
  completion dialogue identities on the same server-owned target state. The
  Hellfire Lachdanan row also declares the native post-hand-in defeat boundary;
  the server consumes the Golden Elixir, creates Veil of Steel, and carries the
  dead target through authoritative snapshots and saves.

- Hellfire Na-Krul tome objects now use the validated

`native_dun_fragment_objects.tsv` contract to follow the encounter anchor;
  their behavior remains authored once in `objects.tsv` while placement is
  externalized with the room fragment. The same contract now accepts a unique
  encounter identity, so generated set-piece objects can be positioned after
  encounter materialization without client-owned coordinates.

- Hellfire Na-Krul tome slots now use the validated
  `quest_sequence_variants.tsv` contract. Activation selects one of the six
  native step permutations with the same deterministic RNG and applies it to
  the anchored authored objects before the simulation is created.

- Schema 1.394 adds optional `unique_monster_placements.tsv` rows for validated,
  content-owned generated unique placement. The contract checks identity and
  monster selectors, level geometry, collision cells, stable entity IDs, and
  deterministic seeds before materializing targets through the authoritative
  spawn planner. Optional quest gates are evaluated after single-player pool
  selection; the shipped base table places Snotspill for the initial Banner
  fragment only while Ogden's Sign remains available.

- Schema 1.393 adds optional authored next-message identities to staged unique-
  monster dialogue. Snotspill now preserves the native intermediate banner
  prompt after its opening conversation instead of jumping directly to hand-in.

- Unique-anchored native DUN fragments now follow the encounter lifecycle:
  fixed-object fragments apply during activation, while generated unique
  encounters apply their validated fragments when materialized on level entry.

- Native-DUN fragment object placements can now clone behavior-only
  `objects.tsv` templates. The authored Snotspill Banner sign chest therefore
  materializes at its unique encounter-relative coordinate; an unavailable
  quest-gated Snotspill target leaves no orphan chest.

- Deferred unique-anchored fragments now materialize their anchored behavior
  objects in the same simulation boundary as the fragment geometry, including
  procedural level-entry encounters rather than only activation-time targets.

- Native-DUN fragment behavior-template references are now validated during
  activation, before quest or encounter availability can defer their use.

- The initial Snotspill Banner room is now externalized as a unique-anchored
  `banner1.dun` fragment plus an explicit Hell placement. Later Banner quest
  opening state changes and resynchronization remain native, while the final
  item-gated mega-tile/transparency transition is now an authored mutation
  follow-up.

- Native server-backed input, polling, portal, store, quest, and ability
  requests now timestamp the compatibility boundary with the shared
  demo-aware timer; the adapter no longer reintroduces direct SDL timing for
  authoritative commands.

- Godot spell casts now resolve visible living monsters to stable authoritative
  entity IDs before submission, while preserving dead-player targeting for
  Resurrection and other player-targeted casts. Coordinate targeting remains
  the server-owned fallback for older or presentation-only clients.

- The native server-backed client now projects local authoritative damage,
  healing, and experience events directly into its floating-number renderer;
  legacy Lua floating-number mods remain available in legacy mode.

- Native authoritative player snapshots now retain the server-owned Diablo kill
  rank in the native player state, preserving victory progression across the
  compatibility projection and subsequent saves.

- Native authoritative player snapshots now retain server-owned Reflect charges
  in the native `wReflections` state, preserving spell defense and renderer
  parity while rejecting values outside the native 16-bit range.

- Native authoritative local-player snapshots now apply the server-owned visited
  level set to `_pLvlVisited`, with native-range and duplicate validation so
  legacy quest and dungeon-entry gates cannot read stale local state.

- Native remote-player presentation now retains authoritative Diablo rank,
  Reflect charges, and visited levels as well, keeping aura and inspection
  state aligned without adding remote players to simulation pools.

- The native session boundary now cross-validates the player `active_store_id`
  against the matching authoritative vendor snapshot, rejecting inconsistent
  reconnect or poll state before it reaches the store UI.

- The shared protocol now carries an idempotent `CloseStoreRequested` intent;
  the Diablo module clears the server-owned active store and the Godot HUD waits
  for the resulting empty vendor snapshot instead of closing optimistically.

- The native authoritative client now submits that same close intent when the
  visual vendor panel closes and after the Adria mana service completes, then
  reconciles the empty vendor snapshot before continuing local presentation.

- The authoritative protocol now carries idempotent stash open/close intents
  plus stash-gold deposit/withdraw commands. Stash item moves and gold changes
  require the server-owned stash session, and vendor/stash sessions are
  mutually exclusive; Godot and the native client expose the authoritative
  `stash_open` state, page grid, and server-reconciled gold.

- Native authoritative stash entry now validates and projects every server-owned
  page/cell footprint into the legacy 10×10 renderer, routes session open/close
  through the server, and routes stash gold deposit/withdraw through stable
  authoritative commands. Native normal stash drags, shift-click automatic
  withdrawals, and Ctrl-click inventory-to-stash placement now use stable
  page/cell move commands and server-reconciled snapshots.

- Native Identify, Repair, and Staff Recharge class-ability cursors now resolve
  projected stash items to stable page/cell references and submit the same
  authoritative service intents used for inventory and equipment.

- Godot now submits those stash session and gold commands from its HUD and
  presents server-reconciled paged 10x10 stash grids. Grid clicks submit only
  stable item references and destinations; the server remains authoritative for
  footprint validation, swaps, and placement.

- Native local and remote player projections now use a walk action's future
  tile only until the authoritative movement effect is applied; post-effect
  snapshots no longer schedule a second client-side step.

- The Godot authoritative player-position projection now follows the same
  action lifecycle: it reserves a walk destination only before the server's
  movement effect tick, preventing a second client-side step after reconciliation.

- Godot local prediction now reconciles one matching pending move sequence
  against the authoritative move action, both before and after its effect tick,
  while retaining later queued inputs for bounded prediction.

- Godot world-position caches now reset when an entity's authoritative level
  identity changes; same-level snapshots retain smooth interpolation.

- Godot player presentation now also snaps for explicit level-transition or
  restart-town actions and same-level relocations larger than one tile, while
  ordinary one-tile authoritative walks continue to interpolate.

- Godot remote-player walk presentation now derives its fractional position
  from the authoritative `started_at_tick`/`effect_tick` interval. The
  presentation path clamps at the effect tick and never changes the discrete
  reserved position used by gameplay or target selection.

- Godot local presentation uses bounded input prediction while a move is
  unacknowledged, then uses that same authoritative interval after
  reconciliation instead of snapping to the occupied tile.

- Godot local movement prediction now retains at most 16 unacknowledged
  commands; additional input remains server-authoritative until snapshots catch
  up.

- Godot clears pending local movement prediction when the authoritative player
  identity or level changes, so transitions cannot replay old-level inputs.

- Godot clears transient presentation prediction and event history when
  reconnect begins; the client still retains the authoritative snapshot and
  transport command ledger for server-side retry.

- Godot ignores snapshots older than its current authoritative tick while still
  processing acknowledgements and other message payloads.

- Godot retains event batches delivered before their matching snapshot and
  ignores older event ticks; command-result presentation cannot regress.

- Godot bounds retained authoritative event history to 256 entries for
  long-running sessions and advances a presentation sequence cursor when
  entries roll off.

- Native remote-player presentation now rejects snapshots older than its last
  applied server tick, preserving authoritative position and action state
  during delayed or reordered delivery.

- Native authoritative session polling now ignores snapshots older than the
  local player's last applied server tick and filters event batches older than
  that projection before native feedback is applied.

- The native authoritative player-state owner now also rejects a snapshot older
  than its retained server tick, so delayed packets cannot regress state when a
  lower-level adapter applies a projection outside the normal session poll.

- Native remote-player drawing now checks the projected player's authoritative
  level before rendering, while preserving level `0`'s compatibility wildcard;
  snapshots retained for inspection on other explicit levels cannot appear at
  matching coordinates in the active dungeon.


- The Godot HUD now owns the legacy clock presentation with a tested invariant
  `HH:mm:ss` formatter; the optional native legacy clock script remains
  available for compatibility mode.

- Native remote-player presentation now selects walk, attack, ranged, block,
  hit, spell, and death sprite families from the authoritative combat mode
  without inserting those players into native simulation pools, and projects
  authoritative spell families plus weapon/shield/armor slots into native
  sprite variants, and positions action animations from authoritative snapshot
  and action ticks. One-tile authoritative walk destinations now populate the
  native future position so the existing renderer can interpolate remote walks
  without local movement simulation. Same-level friendly remote projections
  also draw on the native automap and party panel, with party-panel hover and
  right-click inspection routed by authoritative entity ID; correction buffering
  and remaining legacy interaction parity remain follow-up seams.

- Connected native Town Portal collision now resolves the projected portal ID
  for the local authoritative level and submits `UsePortal`; disconnected
  sessions retain legacy `CMD_WARP` compatibility behavior. Portal projection
  is sorted and duplicate-checked before entering the native session state.

- Native authoritative object interaction now routes every projected object
  with a server-owned effect, quest link, transition, reward, or follow-up
  through `OperateObject`; objects without authored authoritative behavior keep
  the legacy local interaction path.

- Godot now rejects malformed authored object transition, reward, projectile,
  and follow-up metadata at the protocol boundary, preserving fail-closed
  presentation and interaction behavior for external content.

- Godot world-object hit-testing now selects only active server-authored
  interactions. Ordinary presentation-only objects cannot accidentally submit
  an authoritative operation; authored effects, quest links/transitions,
  rewards, follow-ups, and revealed traps still route by projected entity ID.

- Godot rejects authored object links to quests absent from the same snapshot,
  keeping quest-object interaction selection fail-closed at the protocol edge.

- Godot validates quest identity uniqueness and core progress/variable bounds
  before exposing quest, towner, or authored-object interactions.

- Godot world clicks now use server-owned target-cell movement before pickup,
  authored-object, quest/towner, or portal commands when the current player
  position does not satisfy the server interaction range, then submit the
  original entity-addressed intent once the authoritative position arrives.

- Godot rejects projected level geometry with out-of-range or duplicate
  collision cells and mismatched native transparency layers.

- Godot rejects projected players and world entities outside a supplied level
  geometry, including moving-monster future cells, while allowing legacy
  snapshots that omit geometry for that level.

- Godot rejects projected portal endpoints outside supplied level geometry,
  while allowing legacy portal snapshots that omit geometry for either level.

- Godot now presents authoritative equipment rows, routes sell/repair/recharge/
  identify through stable equipment references, and submits explicit
  inventory/equipment transfers without local topology decisions.

- Godot now displays server-owned primary attributes and stat points and sends
  one-point allocation intents for Strength, Magic, Dexterity, and Vitality;
  resulting values remain snapshot-authoritative.

- Level geometry snapshots now carry validated authored warp-trigger metadata.
  Godot renders available and quest-gated markers from the server projection;
  clients never select or apply the transition locally.

- The native authoritative world projection now carries sorted, validated
  server projectile snapshots through the same boundary as monsters, items,
  and objects. The native presentation bridge materializes the first supported
  spell, monster, area, impact, layered-effect, and player-arrow families
  (including Lightning segments, Flash layers, Resurrection beam, Flame Wave,
  Inferno, Apocalypse impact, and Elemental) as presentation-only missile
  sprites when the active native content pack defines them, applying
  authoritative position and
  lifetime while skipping local process, collision, and damage logic. The
  bridge fails closed for unsupported families or content-missing missile rows;
  those projectiles remain projection-only until their native presentation
  mapping is available.
  The server also supplies an additive exact native visual identity, so the
  compatibility client no longer needs to infer supported sprites from
  gameplay behavior numbers; zero-valued metadata preserves older-server
  fallback behavior and unknown explicit identities fail closed.
  Snapshot-driven collision also submits the same intent when the authoritative
  portal has no locally-created legacy missile.

- The standalone authoritative executable now exposes a local operator console
  with `status`, `reload`, `help`, and `quit`; `reload` re-reads validated
  content/module candidates and uses the existing transactional session
  migration barrier, while failed candidates leave the active ruleset intact.

- Object snapshots now project additive server-owned shrine, quest, breakable,
  and reveal-gated trap presentation families, while older or unknown metadata
  retains the generic Godot object path.

- Godot's interaction panel now renders typed server-owned dialogue events as
  quest/message identities without importing native text tables; localized text
  remains a later presentation/content concern.

- Dynamic spell-book selection now derives single-player exclusions from the
  validated `Resurrection` and `HealOther` spell behaviors, while retaining the
  native-ID fallback needed by older content packs and multiplayer availability.

- Generated authoritative item state now carries the stable content item ID
  through protocol-backed saves. Catalog resolution prefers that ID before the
  retained native item index; zero-valued legacy state and the legacy replay
  hash remain compatible.

- Server-owned item quest effects, combinations, quest-item completion, and
  PvP-ear deduplication now use that stable identity before the native index,
  so shared native projections cannot select the wrong content definition.

- Snapshot restoration rejects unknown nonzero stable item IDs when an active
  item catalog is present; legacy zero-valued IDs and catalog-less simulations
  remain compatible.

- `PlayerItemReference` now carries an additive stable item ID. Godot populates
  it from authoritative snapshots for item services and spell sources, while
  the server treats zero as legacy positional addressing.

- Inventory relocation now carries the same additive `PlayerItemReference`.
  Native and Godot indexed moves resolve the latest authoritative item before
  submission, and the C# module rejects stale identities without changing the
  inventory grid; the legacy index-only fields remain compatible.

- The Godot presentation project and protocol client now have executable
  dependency-boundary coverage: the protocol client may reference only the
  shared protocol assembly, and the Godot project may reference only those two
  presentation/protocol projects, never the server or Diablo module.

- The native client now skips Lua initialization, shutdown, and debug-console
  registration in explicit authoritative server-backed mode. Legacy mode keeps
  the existing Lua compatibility lifecycle.

- The native authoritative adapter now carries stable item identities through
  projected inventory, equipment, belt, and cast-source state. Native service,
  store, and spell-source commands resolve references from the authoritative
  snapshot instead of relying on positional slots alone.

- Native authoritative startup now registers a `ModManager` archive/data reload
  callback and activates the selected mod archives without initializing Lua.
  Legacy mode still replaces that callback with the Lua compatibility hook so
  existing scripted mod ordering remains unchanged.

- Connected native Telekinesis now submits the projected world-item, object, or
  monster entity through the authoritative cast path; the legacy cursor helper
  remains active for disconnected and legacy sessions.

- The shared protocol now carries an additive target-cell movement intent. The
  Diablo module resolves one deterministic server-owned path step through the
  Engine navigation primitive, and connected native cursor walking plus
  repeated dungeon walking use it; legacy directional and auto-path commands
  remain available for compatibility paths.

- Godot's left-click presentation flow now submits the same target-cell
  movement intent when no authoritative cast or item interaction applies;
  server-side path resolution remains authoritative.

- Connected native targeted scroll and rune cursor casts now submit the
  authoritative cast intent with the exact stable consumable source reference;
  legacy spell commands remain available outside server-backed sessions.

- Connected native direct scroll and rune item use now submits the same
  authoritative cast intent for non-keyboard and immediate-use paths, retaining
  the exact stable source reference and the legacy fallback outside connected
  server-backed mode.

- Connected native Resurrection and Heal Other cursor actions now submit
  explicit authoritative player-targeted casts with memorized-spell or
  consumable source identity, while the legacy target commands remain the
  disconnected compatibility path.

- Connected native mouse and controller town-towner interactions now enter
  `TalkToTowner`, allowing authored quest turn-ins to submit server-owned
  intents while preserving local presentation for non-authored store/dialogue
  behavior.

- Connected native mouse entity-targeted monster/player attacks and ordinary
  object operations now submit projected authoritative entity IDs, and repeated
  cursor actions use the same server-owned intents. Adjacent primary native
  world-item clicks now submit the projected world-item entity through the
  authoritative pickup intent in both town and dungeon modes; distant clicks
  retain native auto-walk compatibility. Connected native Trap Disarm now
  submits the existing `DisarmTrapRequested` command. Connected native
  location-only and repeated attacks submit the additive cell-targeted attack
  intent, which preserves native directional melee and ranged path semantics;
  legacy mode retains the original commands.

- Native authoritative inventory use now submits spell-book consumption through
  `ReadBookRequested`; the legacy client keeps its local book path only outside
  the connected server-backed mode.

- The connected native mouse held-item drop path now submits the authoritative
  drop intent; `CMD_PUTITEM` remains the disconnected compatibility path.

- Connected native trigger polling now defers level transitions entirely to
  server-owned movement/warp resolution instead of emitting legacy `CMD_NEWLVL`.

- Authoritative experience level-ups now apply native class life/mana growth,
  level-up refills, `NoMana` suppression, and server-owned stat-point awards;
  the projected native character panel receives the resulting maxima and
  resources from the snapshot.

- The shared protocol now projects every validated item action, including
  attribute-all and all four oil actions. The Godot HUD submits targetless
  potion, rejuvenation, and elixir actions as stable-reference
  `UseConsumableRequested` commands, and its oil-target list submits a second
  stable equipment or inventory reference; server validation remains the final
  authority for target restrictions.

- Native authoritative potion, rejuvenation, elixir, and supported oil use now submits
  `UseConsumableRequested` with a stable inventory or belt reference. The Diablo
  module owns native Q6 life/mana restoration, class multipliers, deterministic
  RNG, elixir stat ceilings, Q6 resource growth, Hellfire full-resource effects,
  `NoMana` suppression, persistence, and item consumption. Oil selection retains
  a presentation-only cursor until the server receives the exact source and
  target references; the server owns target restrictions, mutations, and
  atomic source consumption. Remaining miscellaneous cursor actions are still
  explicit follow-up seams.

- Arena Potion use now preserves the native set-level restriction: the
  server accepts the full-rejuvenation action only on active arena runtime
  levels and rejects the same item elsewhere without consuming it.

- Godot now exposes authoritative belt source selection for books, targetless
  consumables, and oil sources; each request carries the belt slot and stable
  item identity, while vendor service actions remain inventory-only.

- Live single-player protocol coverage now consumes a starting belt potion by
  its stable reference and verifies that only the addressed belt entry is
  removed.

- Connected native Cathedral Map use now submits the projected grave object
  through `OperateObjectRequested`; the server validates adjacency, quest state,
  and the stable map definition before consuming it.

- Connected native Hellfire Rune Bomb use now submits a projected Hive entrance
  object through `OperateObjectRequested`; the server validates the stable item,
  consumes the bomb, completes Farmer's Orchard, and exposes the gated Nest warp.

- Hellfire Magic Rock pickup is now an external `item_quest_effects.tsv`
  transition. The authoritative pickup path activates and logs quest 3001 and
  advances its native first variable; legacy native pickup remains compatible.

- Hellfire Anvil of Fury pickup now uses the same external item-to-quest
  transition catalog. The authoritative pickup path activates and logs quest
  3011; legacy native pickup remains compatible.

- Hellfire Optic Amulet and Arkaine's Valor pickup now complete their active
  content-defined quests through the same catalog (quests 3009 and 3012).
  Inactive quests retain the native no-completion behavior.

- Hellfire Black Mushroom pickup now advances quest 3002 from
  QS_MUSHSPAWNED to QS_MUSHPICKED through the content-owned
  required_variable1 precondition; unexpected quest states remain unchanged.

- Content-declared towner turn-ins are now generic. The server
  validates the towner, level, adjacency, native `_qvar2` conversation gate,
  and required Anvil item; it then consumes the item, completes quest 3011, and
  spawns Griswold's Edge through `TurnInQuestRequested`. The base and Hellfire
  Anvil/Blacksmith paths use the same definition. Legacy native behavior
  remains compatible outside authoritative mode; the connected native client
  now selects the active quest from the content-declared towner metadata and
  projects the returned dialogue event.

- Content-declared unique-monster conversations are now generic. The native
  adapter projects each monster's opaque unique identity, selects the active
  authored quest, and submits `TalkToQuestRequested`; the server validates the
  target identity, live state, level, and adjacency before activating the quest
  and returning its dialogue event. Dialogue definitions may additionally
  declare an inventory hand-in and relative reward; Hellfire Defiler remains
  the first conversation-only definition and Lachdanan is the first hand-in
  definition. Authored `dialogue_completion_variable1` now also gates unique
  monster combat: the Warlord remains non-combatant while its staged dialogue
  is pending, and direct attacks are rejected until the native terminal marker
  is reached. Staged routes now read that pending state directly from
  `quest_dialogue_stages.tsv`, while the completion marker remains the fallback
  for older packs. Legacy hardcoded monster conversations remain fallback
  behavior until their content actions are externalized.

- Staged unique-monster dialogue may now declare a deterministic
  `RandomUniqueMonsterDrop` reward. The server selects the native unique-drop
  family from validated item metadata, generates the item with the active
  quality rules, and projects it in the world snapshot; base Gharbad's second
  stage is the first shipped use.

- Content-declared quest-object effects are now generic. The native adapter
  routes active `QuestSequence` and `QuestItemCompletion` objects through
  `OperateObjectRequested`; the server owns effect ordering, quest state,
  required-item consumption, activation reconciliation, and any linked
  authored quest dialogue event. The Hellfire Na-Krul tome sequence is the
  first shipped definition; authored sequence follow-ups can now apply
  validated map mutations or server-owned Phasing/Guardian projectiles after
  a correct step. Complete native set-level bundles now materialize their
  active-map object anchors; unavailable bundles retain the legacy fallback.

- Authored `WorldMutation` object effects now load a validated
  `world_mutations.tsv` catalog. The server applies each referenced cell
  overlay once, persists active mutation IDs in transport-independent saves,
  and sends effective movement/missile geometry through the existing
  `LevelGeometrySnapshot`; native book/lever placement is supplied by the
  validated set-level catalog when its bundle is available.

- Schema 1.375 extends `world_mutations.tsv` with optional deterministic
  geometry rectangles. Each rectangle expands into both movement and
  missile-blocking cells after the target level is resolved, enabling authored
  native-style map regions without hard-coded map logic; explicit cell lists
  remain backward compatible.

- Schema 1.376 extends the same mutation catalog with validated
  `transparency_rects` assignments. Active values are applied to native
  transparency layers and projected through level geometry; overlapping
  assignments and levels without a transparency layer reject activation.

- Schema 1.378 reconciles explicit native-DUN object mappings after active
  object-layer mutations. Unchanged mapped entities preserve authoritative
  state, stale cells are removed, and newly mapped cells are materialized with
  deterministic reserved IDs.

- Schema 1.379 extends explicit native-DUN object mappings to ordinary runtime
  levels used by quest and composite-map paths. Set-level mappings retain their
  existing identity contract, while ordinary-level mappings use a separate
  reserved entity range and require an explicit level identity.

- Schema 1.380 adds
`native_dun_levels.tsv`. Each ordinary runtime level may
  explicitly reference one immutable native `.dun` asset; its object layer is
  attached to the existing level geometry without inferring collision behavior.
  Effective d-piece geometry remains supplied by the composed level or a
  validated external `.til`/`.sol` bundle. Dimensions, level identities, and
  asset paths are validated before activation.

- Schema 1.381 adds optional stable `object_symbol` cross-checks to

`native_dun_objects.tsv`. Numeric `object_id` rows remain backward-compatible;
  when a symbol is supplied, it must resolve to the same authored object ID.

- Schema 1.382 ships deterministic `procedural_levels.tsv` rows for ordinary
  base-dungeon levels 3 through 16. Explicit `levels.tsv` rows supplied by a
  variant pack take precedence for overlapping identities; generated rows fill
  only levels absent from the active explicit world.

- Schema 1.383 ships stable base progression warps from town through ordinary
  level 16. Hellfire overlays retain its quest-gated dungeon warps, and the
  composed world validates all 18 triggers against generated and explicit
  level geometry.

- Schema 1.377 extends authored mutations with validated `object_layer_rects`
  assignments. Effective native object-layer values now survive map mutation
  projection and remain available to the explicit native-DUN object mapping
  catalog.

- Authored object rows now also support validated quest transitions. An object
  may activate or complete its linked quest, set `_qvar1` or `_qvar2`, and gate
  the transition on prior variable values; the transition is applied in the
  same accepted `OperateObjectRequested` command as any geometry overlay.

- Authored object rows may also generate one validated normal or unique item
  reward at a relative offset. The server owns catalog validation, deterministic
  generation, placement, persistence, and snapshot projection; set-level
  anchors are materialized from complete native bundles, while their later map
  mutation effects remain a separate seam.

- Authored object rows now support the validated `GrantSpellLevel` effect.
  `OperateObjectRequested` resolves the spell through the active catalog and
  increments the player’s memorized level with native level caps. An optional
  `effect_spell_symbol` cross-checks the stable spell namespace; set-level
  anchors are materialized from complete native bundles, while their later map
  mutation effects remain a separate seam.

- Schema 1.359 adds the validated `SpawnSpellProjectile` object effect.
  Authored rows can spawn a server-owned Phasing or Guardian controller at the
  object position through the existing projectile snapshot/tick path; the
  active spell catalog rejects unsupported behaviors. Exact native target
  offsets, set-level placement, and map mutations remain later seams.

- Schema 1.360 adds optional `projectile_target_offset_x` and
  `projectile_target_offset_y` columns to authored objects. The server carries
  these validated offsets through saves, protocol snapshots, and canonical
  hashes before constructing the Phasing or Guardian controller, so native
  book-relative targets no longer require client-side coordinates.

- Schema 1.361 adds validated quest-sequence follow-up metadata. Correctly
  ordered `QuestSequence` steps may apply one content-backed world mutation or
  spawn a server-owned Phasing/Guardian controller at authored relative
  offsets; wrong-order resets do not apply follow-ups, and the state is carried
  through saves, snapshots, protocol projection, and canonical hashes.

- Schema 1.362 adds validated staged-dialogue follow-ups. An accepted
  unique-monster conversation stage may apply one content-backed world-cell
  mutation after the quest transition, item consumption, and authored reward
  validation; invalid mutation references reject before any state changes.

- Schema 1.363 adds `set_level_objects.tsv`, which externalizes the native
  fixed-object positions and template footprints for the three quest set-level
  maps that currently use hardcoded `InitializeLoadedObject` calls. The
  validated catalog is carried by Diablo activation and simulation metadata.
  When the corresponding active native bundle is present, activation now
  materializes those placements as server-owned interactive objects; older
  packs retain the metadata-only catalog. Set-level transitions remain
  separate.

- Schema 1.364 adds `set_level_maps.tsv`, which externalizes the native
  pre-map/active-map asset paths, dungeon type, and entry spawn positions for
  Skeleton King's Lair, the Chamber of Bone, and Archbishop Lazarus' Lair.
  Activation validates the stable set-level links and carries the catalog in
  simulation metadata; complete native bundles are now materialized during
  activation and reject an out-of-bounds or blocked active-map spawn point,
  while routing set-level transitions remains separate.

- Schema 1.365 adds optional `til_asset` and `sol_asset` paths to
  `set_level_maps.tsv`. Current rows identify the native dungeon-type geometry
  inputs shared by each pre-map and active-map pair; older packs remain
  compatible when the optional paths are absent, while composed materialization
  requires them.

- Schema 1.366 adds the optional native `transparency_asset` path to
  `set_level_maps.tsv`. When declared, activation composes and validates the
  transparency `.dun` layer against the active-map dimensions and retains its
  d-piece values on the authoritative level geometry; a transparency layer
  may also arrive before deferred map and `.til`/`.sol` geometry, in which
  case it is shape-validated and retained as metadata until the complete
  bundle is available. The base pack now includes the native Skeleton King
  and Chamber of Bone transparency binaries; the Lazarus row remains deferred
  because native reuses its `vile1.dun` pre-map as the transparency source.
  Older packs remain compatible without it.

- Schema 1.367 adds the optional `template_entity_id` link to
  `set_level_objects.tsv`. A validated set-level placement can now clone the
  complete behavior of an `objects.tsv` entity with the matching native object
  ID, including quest transitions, spell effects, world mutations, rewards,
  and presentation flags, while retaining deterministic reserved placement
  identities. Existing bare placements remain backward compatible. This
  removes the server-side structural blocker for externalizing book/lever
  behaviors; exact native placement and map-effect rows remain content work.

- Schema 1.368 adds the optional
`native_dun_objects.tsv` mapping table. It
  explicitly maps native DUN object-layer values for each set-level map variant
  to authoritative object identities and optional behavior templates. Complete
  native geometry now retains the object layer through level projection and
  materializes every mapped cell with deterministic reserved entity IDs;
  unmapped values remain inert instead of being guessed as gameplay objects.

- Schema 1.369 adds the optional `template_only` column to `objects.tsv`.
  Behavior-only rows are validated and available to set-level/native-DUN
  mappings without entering ordinary world state, removing the phantom-object
  blocker for authoring the remaining book/lever behaviors.

- Schema 1.370 lets `template_only=1` object rows omit level and placement
  fields. Template definitions now carry behavior without a fake world
  coordinate, while ordinary object rows retain strict placement validation.

- Schema 1.371 lets `world_mutations.tsv` target a native set-level variant by
  stable set-level identity and active/pre-map selection. Validation now runs
  after set-level geometry materialization, enabling authored set-piece cell
  overlays while retaining ordinary level targets.

- World mutations may now use an optional `anchor_unique_identity` instead of a
  fixed object entity. The server resolves exactly one matching unique encounter
  when projecting an active mutation, so generated quest targets can provide a
  deterministic origin for future set-piece transitions; fixed-object anchors
  remain backward compatible.

- Native DUN fragments now accept the same optional unique-identity anchor. The
  content bootstrap applies those fragments after generated encounter planning,
  allowing a future authored quest fragment to follow a generated unique target
  without checking proprietary map assets into the repository.

- Schema 1.372 adds the composite
  `GrantSpellLevelAndSpawnProjectile` object effect. One validated server-owned
  interaction can now grant a spell level and create its Phasing or Guardian
  projectile atomically, covering the multi-effect shape needed by authored
  set-piece books.

- Schema 1.373 allows a direct `WorldMutation` object to use the existing
  follow-up metadata for one validated Phasing or Guardian projectile. The
  server applies the cell overlay and projectile in one accepted operation,
  extending the authored set-piece boundary without inventing native tile
  data; exact map replacement and resynchronization remain separate work.

- Schema 1.374 adds the optional authored `projectile_source` field. The
  compatibility default preserves target-origin behavior, while Phasing rows
  may start from the activating player's tile. Source identity is validated and
  carried through snapshots, saves, protocol projection, and canonical hashes.

- The Diablo module now has a strict native `.dun` parser that preserves the
  mega-tile layer and all four d-piece layers (reserved, monsters, objects, and
  transparency) from checked-in native fixtures. It deliberately does not
  infer walkability or object behavior from native IDs; collision interpretation
  remains explicit, and object-layer behavior requires the validated

`native_dun_objects.tsv` mapping.

- The geometry boundary now has a validated materializer contract: native
  little-endian `.til` mega-tile records and `.sol` d-piece property bytes can
  be loaded without presentation dependencies, then expanded into authoritative
  blocked and missile-blocked cells with missing mappings rejected. The shipped
  packs do not yet include the native `.til`/`.sol` assets, so the shipped
  set-level rows remain metadata-only. A complete bundle now materializes both
  pre-map and active-map variants during Diablo activation as authoritative
  levels in the reserved `0x80000000` runtime range, and its active-map object
  placements become server-owned interactive objects in a reserved entity
  range; partial bundles reject activation. The engine exposes a single
  validated bundle loader for the `.dun`/`.til`/`.sol` composition, keeping
  asset acquisition separate from geometry interpretation.

- Content packs now carry relative native `.dun`, `.til`, and `.sol` assets as
  immutable binary entries alongside TSV tables. Asset bytes participate in the
  manifest hash; identical inherited assets compose safely, conflicting bytes
  reject activation, and the Diablo geometry loader consumes the composed asset
  set without filesystem access in the engine or gameplay module.

- Content-owned towner turn-ins now support normal item rewards as well as
  unique rewards. The server validates and generates the authored reward item
  at the native reward level and position; Hellfire Little Girl consumes
  Theodore and creates the level-13 amulet path through `TurnInQuestRequested`.

- Hellfire Jersey's Jersey now uses the same authored towner boundary. Complete
  Nut's stable towner identity, Brown Suit hand-in, native conversation gate,
  Bovine Plate reward, and dialogue IDs are content-owned; the server consumes
  the suit, completes quest 3024, and spawns the unique reward at the native
  southeast position.

- Hellfire Jersey's Jersey Grey Suit tease now uses an authored alternate
  towner item interaction. The server consumes Grey Suit, emits the native tease
  dialogue, and leaves quest 3024 unchanged; the authoritative client no longer
  rejects or falls through to the local Cow Farmer branch for that item.

- Hellfire Farmer's Orchard now uses an explicit activation-only towner row.
  Complete Nut's counterpart, the Farmer, activates quest 3018, advances the
  native `_qvar1` gate, and creates the initial Rune Bomb through the same
  `TurnInQuestRequested` boundary; the existing Hive object remains the
  server-owned completion path.

- Hellfire Farmer's post-Hive reward now uses a second authored towner stage.
  Completion exposes a one-shot Farmer interaction through the protocol,
  advances the native `_qvar2` marker, generates the Auric Amulet, and projects
  the native completion dialogue; the connected client no longer falls back to
  local reward spawning.

- Base Cain's Lazarus hand-in now uses an item-consuming activation-only
  towner row. The server consumes Staff of Lazarus, writes native `_qvar1 = 2`,
  emits the activation dialogue, and exposes the quest to the authored Lazarus
  unique-monster conversation; the native client stops routing repeat Cain
  interactions after activation.

- Base Magic Rock pickup and Griswold's staged hand-in now use authored content.
  The pickup effect activates quest 3001; the first Blacksmith interaction
  advances `_qvar2` and emits the native prompt without consuming Magic Rock,
  while the second consumes it and spawns Empyrean Band at the native position.

- Base Anvil of Fury pickup and Griswold's staged hand-in now use authored
  content. The pickup effect activates quest 3011; the first Blacksmith
  interaction advances `_qvar2` and emits the native prompt without consuming
  Anvil, while the second consumes it and spawns Griswold's Edge at the native
  position.

- Base Butcher activation now uses an authored Dead Guy interaction, with normalized native The Butcher monster content supplying the completion target.

- Base Skeleton King now uses an authored route-activated quest state, with
  normalized native Skeleton King monster content supplying the completion
  target for native monster identity 50. Schema 1.339 also externalizes its
  native route `_qvar1 = 1` activation marker.

- Base Lazarus now uses an authored completion-stage Storyteller interaction:
  the server validates native `_qvar1 = 7`, writes `_qvar1 = 8`, emits the
  native completion dialogue, and logs the authored Diablo follow-on quest.

- Hellfire Grave Matters now uses an authored non-consuming Barmaid dialogue
  stage. The server requires the Cathedral Map, records native `_qmsg = 274`,
  emits Gillian's dialogue, and preserves the map and quest completion state.

- Base Poisoned Water now uses an authored Pepin completion-stage interaction.
  Its native `_qvar1`-only marker is carried through the protocol, the server
  writes `_qvar1 = 2` without changing `_qvar2`, emits `TEXT_POISON5`, and
  generates the authored Ring of Truth reward at the native position. Schema
  1.351 also externalizes Pepin's initial Caves/Poisoned Water visitation gate
  and active-but-unlogged fallback through `require_unlogged` stages.

- Base Ogden's Sign now uses authored quest state 3008. The first Tavern
  interaction advances `_qvar2` and emits the native prompt without consuming
  Tavern Sign; the second consumes it and spawns Harlequin Crest at the native
  reward position.

- Schema 1.353 extends the generated unique-encounter boundary to the base
  pack. Gharbad, Zhar, Warlord, and Lazarus now have validated native-AI
  selector declarations; static activation defers unmatched selectors, and
  the planner binds them only when the matching unique encounter materializes.

- Schema 1.354 adds `quest_dialogue_stages.tsv`, a reusable server-owned
  contract for user-visible unique-monster dialogue messages and monotonic
  `_qvar1`/`_qvar2` transitions. Base Gharbad now routes all four authored
  dialogue stages through `TalkToQuestRequested`; its random item reward is
  externalized in schema 1.358, while encounter-state and presentation
  transitions remain explicit native fallbacks.

- Schema 1.355 extends `quest_dialogue_stages.tsv` with validated item gates
  and optional quest completion. The shared Ogden's Sign/Snotspill quest now
  routes Snotspill's banner dialogue and Tavern Sign hand-in through
  `TalkToQuestRequested`; its final Banner mega-tile/transparency transition
  now follows the accepted hand-in through `world_mutations.tsv`.

- Schema 1.356 moves Warlord's two authored dialogue transitions into the
  staged dialogue catalog. Messages and monotonic `_qvar1` transitions through
  values `2` and `3` are server-owned; the native Warlord AI/presentation
  fallback remains until encounter-state projection is externalized.

- Schema 1.357 moves the text for authored Gharbad, Snotspill, and Warlord
  messages into validated `dialogue_messages.tsv` content. The server projects
  that text on `DialogueEvent.text`; clients retain the numeric identity
  fallback when a legacy pack has no message catalog.

- Schema 1.358 adds a validated staged-dialogue reward contract. Base Gharbad's
  second authored stage now generates one deterministic native-quality random
  unique-monster drop from the active item catalog and places it at the
  authored relative position; his encounter-state and presentation transitions
  remain a separate native fallback.

- Schema 1.352 removes the remaining static-entity dependency from authored
  unique-monster identities. `unique_monsters.tsv` can now select exactly one
  unique encounter by validated monster ID and native AI, preserving the
  identity across generated placement and the existing snapshot/protocol
  boundary. The spawn planner now applies selector rows as generated unique
  encounters materialize, while unrelated levels defer their selectors. The
  Hellfire Defiler definition exercises this path.

- Base Magic Rock and Anvil of Fury activation conversations now use authored
  quest/towner rows. Their server-owned visited-level OR-gates reproduce the
  native Blacksmith checks, while the existing validated item hand-ins and
  Empyrean Band/Griswold's Edge rewards remain in the quest catalog.

- Nova Ball now preserves the native shrine collision exception: regular
  missile-blocking objects terminate a ball, while a validated shrine on its
  target tile lets the ball continue. The optional object capability is carried
  through snapshots, protocol, saves, and canonical hashing; regular breakable
  blockers are destroyed before the ball is removed. The current Release Diablo
  suite passes all 1,406 tests and the Release server suite passes all 412 tests.

- Fireball now stops on native missile-blocking geometry, destroys a hit
  breakable object, and starts its authoritative splash at the blocker tile;
  the behavior is covered by regular and breakable-object tests.

- Player- and monster-owned arrows now preserve native breakable-object
  destruction when they collide with a missile-blocking object before removal.

- Player-owned arrows now resolve the first living combatant encountered on
  each traversed tile, matching native `ProcessArrow` path collision instead
  of requiring the declared endpoint target to remain the first occupant.

- Player-owned arrows now require the declared endpoint target to still occupy
  the endpoint tile at impact; targets that leave the native travel path are
  missed rather than resolved by stale entity identity.

- Hellfire `RandomArrowVelocity` is now server-owned: validated equipped bows
  capture native 16–47 pixel speed, persist the fractional travel remainder,
  and retain deterministic multi-tile collision timing through snapshots.

- Player-owned arrows now carry the native `AddArrow` class and Hellfire
  attack-speed modifiers in their persisted pixel velocity. The compact replay
  fixture and both C++/C# runners retain the same fractional cadence and
  checkpoint hashes.

- Hellfire player-versus-player arrows now retain the native blocked-missile
  impact phase and consume the native rotation RNG draw instead of disappearing
  immediately on a shield block.

- The shared C++/C# replay gate now includes a deterministic two-participant
  Firebolt cast, covering server-owned spell damage, player hit admission, and
  the resumable projectile checkpoint lifecycle.

- Hellfire Firebolt shield blocks now preserve the native player block result,
  consume the variant rotation draw, and enter the authoritative impact phase;
  a shared replay checkpoints spawn, impact, and removal.

- Fireball now resolves direct and impact-phase player collisions through the
  same authoritative player-target boundary as native `Plr2PlrMHit`; the
  shared replay covers nonlethal Q6 damage, splash timing, and projectile
  removal in both C++ and C#.

- Hellfire Fireball shield blocks now retain the native impact phase, consume
  the variant rotation draw, and still resolve the later splash against the
  living participant through the shared C++/C# replay.

- Bone Spirit player hits now retain the native current-life divisor followed
  by the non-arrow half-damage step; a shared replay covers self-damage, hit
  RNG, impact lifetime, and complete participant-state parity.

- Bone Spirit now preserves native nearest-monster homing and resistant-monster
  damage, including monster regeneration and the retained impact phase; a
  shared replay covers the C++/C# checkpoint parity.

- Secondary replay participants now route deterministic melee attacks through
  the same authoritative combat boundary as the primary participant; a shared
  replay covers participant identity, target admission, and unarmed damage.

- Seeded MonsterArrow projectiles now advance through native travel, player
  impact damage, and the retained impact phase in both replay runners; a shared
  fixture checks the three-tick lifecycle and canonical checkpoint hashes. The
  managed replay suite passes locally; native GTest source coverage is added but
  awaits a local C++ toolchain run.

- Multiplayer MonsterArrow replay state now routes a seeded impact to the
  secondary participant and hashes both participants, preserving the one
  server-owned projectile lifecycle across session identities.

- Autonomous GoatRanged replay admission now selects the nearest eligible
  participant with stable entity-ID tie-breaking, emits a server-owned
  MonsterArrow from validated missile metadata, and shares travel/impact/removal
  checkpoints with the native compact runner.

- Autonomous SkeletonRanged replay admission now preserves its clear-lane
  attack chance and long-range MonsterArrow travel/impact/removal checkpoints
  across the managed and native compact runners.

- Autonomous Succubus replay admission now emits the validated BloodStar
  missile with native velocity, lifetime, runtime-state, deterministic hit/block
  RNG, Q6 damage, impact-phase, and secondary-player block-animation checkpoints
  in both compact runners.

- Lich-family replay admission now emits validated MonsterMagic projectiles with
  native speed, deterministic hit RNG, Q6 damage, and impact-phase checkpoints
  in both compact runners.

- Hellfire HorkDemon Hork Spawn admission now has a shared C++/C# replay
  checkpoint covering validated summon identity, native speed-8 travel, and
  the first lifecycle-tick decrement. Its persisted native decision-delay
  state also has a shared first-tick decrement checkpoint; the compatibility
  adapter accepts an optional spawn planner so the admission and delay cases
  exercise the same authoritative branch, while a player-impact checkpoint
  verifies native hit RNG, Q6 damage, GotHit timing, and projectile removal;
  a seeded miss checkpoint preserves the projectile through its remaining
  travel tick before expiry; a multiplayer checkpoint covers secondary-player
  block admission, Block timing, and Hork Spawn removal ordering; a secondary
  impact checkpoint covers non-blocking Q6 damage, GotHit timing, and removal;
  a current-tile checkpoint covers breakable-object destruction and removal;
  a shield-seeded primary-player checkpoint covers native block admission and
  Block timing.

- Lich-family MonsterMagic misses now preserve the living player's state and
  the projectile's travel phase while retaining the failed occupant identity;
  the shared replay checkpoints this native/C# collision bookkeeping.

- Lich-family MonsterMagic admission now selects and impacts a living
  secondary participant when the primary is unavailable, preserving target
  identity, Q6 damage, and complete multiplayer checkpoint state.

- The shared protocol now projects an explicit `MonsterMagic` projectile
  presentation family, and Godot renders that server-owned family without
  inferring gameplay behavior from a generic fallback.

- The shared protocol and Godot client now classify server-owned `HorkSpawn`
  summon controllers explicitly, while older peers retain behavior fallback.

- Apocalypse behavior `32` now projects the server-owned fire controller as
  `Fire`, while persistent behavior `33` booms use the larger `FireImpact`
  family; older peers retain the same behavior fallback.

- Firebolt behavior `2` now shares the server-owned `Fire`/`FireImpact`
  projection with Fireball behavior `16`, including the legacy fallback.

- Charged Bolt behavior `1` now projects the server-owned `Lightning` family,
  including the legacy behavior fallback.

- Elemental behavior `35` now projects the server-owned `Fire` family across
  travel and burst phases, including the legacy behavior fallback.

- Guardian behavior `36` now projects an additive server-owned `Guardian`
  turret family, while older peers retain behavior-based fallback.

- Flash behaviors `5`, `6`, and `7` now project an additive server-owned
  `Flash` family for the native two-layer visual, while older peers retain
  behavior-based fallback.

- Rage behavior `34` now projects an additive server-owned `HIDDEN` family so
  current Godot clients suppress the invisible controller instead of drawing a
  generic projectile; older peers retain behavior-based fallback.

- World-item snapshots now project additive server-owned `GOLD` and `EAR`
  families for currency and dynamic PvP-ear presentation, while zero or unknown
  metadata retains the generic client path.

- Persistent Acid puddles now preserve native breakable-object destruction on
  their occupied tile without ending the puddle lifetime.

- Compatibility generic projectiles now preserve native breakable-object
  destruction when a missile-blocking tile ends their travel.

- Apocalypse booms now preserve native breakable-object destruction while
  retaining their remaining persistent lifetime.

- The Godot world presentation now renders server-owned monster health bars
  from authoritative `MonsterSnapshot` vitality, preferring exact Q6 values and
  falling back to rounded legacy fields without moving combat state into the
  client.

- Godot also renders local and remote player life/mana bars from authoritative
  `PlayerSnapshot` resources; the client clamps presentation values but never
  predicts or mutates those resources.

- Godot's engine-independent protocol projection now resolves an in-progress
  authoritative one-tile walk to its reserved destination for remote-player
  interpolation. Local prediction and remote presentation share that resolver;
  neither path advances server-owned gameplay state.

- Godot world presentation now prunes screen positions for entities absent from
  the latest authoritative snapshot and clears them on disconnect, preventing
  stale entity visuals or feedback anchors from surviving reconciliation.

- Godot combat and player-target selection now uses the same authoritative
  reserved-walk position as remote-player presentation, so a visible moving
  participant remains targetable by stable entity ID before movement resolves.

- Player, monster, world-item, object, projectile, and corpse IDs now share a
  validated server-owned namespace at the mapper and Godot boundaries. Entity
  allocators skip occupied portal/corpse/player IDs, while identity-neutral
  version-1 saves preserve their zero player ID until import.

- All 38 checked-in replay fixtures now bind the active
  content-manifest identities; the full 326-test server assembly passes again.

- Host shutdown now disposes the active authoritative adapter as well as
  drained retired generations, closing the ruleset lifecycle ownership gap.

- Ranged monster missile post-attack delays are now validated content fields
  (`ranged_delay_base` and `ranged_delay_random_range`) with native-compatible
  fallback behavior for older packs; the shipped base and Hellfire mappings
  carry the active values and replay identities have been refreshed.

- RangedAvoidance recovery delays are now validated missile-content fields
  (`ranged_avoidance_delay_base` and `ranged_avoidance_delay_random_range`),
  retaining the native fallback for older packs and carrying the active values
  in the shipped base mappings.

- Hellfire HorkDemon recovery delays are now validated `HorkSpawn` missile
  fields (`hork_demon_delay_base` and `hork_demon_delay_random_range`), with
  native-compatible fallback behavior for older packs.

- Native monster direction is now authoritative state carried through internal
  snapshots, the versioned protocol, saves, and canonical hashing. Hork Spawn
  uses the monster's current direction for its adjacent admission and carries
  that direction into the summoned monster instead of deriving either value
  from the victim.

- MonsterArrow projectiles now retain dynamic monster damage bounds through
  the authoritative snapshot, save, protocol, and hash boundaries and roll
  physical damage at impact, while fixed `projectile_damage` mappings remain
  unchanged.

- Hork Spawn now checks its current tile against living players before each
  movement/expiry decision, preserving native non-arrow `PlayerMHit` hit,
  block, minimum-damage, and RNG behavior before the summon path continues.

- Hork Spawn now also breaks a breakable missile-blocking object on its current
  tile before taking the native expiry/summon path.

- Hork Spawn now retains a newly blocked destination for the following tick,
  matching native movement before the current-tile expiry check.

- Snake adjacent and distant delay bands are now validated `SnakeCharge`
  missile fields, preserving native intelligence reduction and deterministic
  zero-clamped timing while retaining fallback behavior for older packs.

- Snake distant-delay admission chance is now validated `SnakeCharge` missile
  content (`snake_distant_delay_chance_base` and
  `snake_distant_delay_chance_intelligence_scale`), preserving the native
  35-minus-two-times-intelligence band with fallback behavior for older packs.

- Snake adjacent attack admission chance is now validated `SnakeCharge` missile
  content (`snake_adjacent_attack_chance_base` and
  `snake_adjacent_attack_chance_intelligence_scale`), preserving the native
  20-plus-intelligence band with fallback behavior for older packs.

- Bat long-idle and fresh-mode movement admission is now validated through
  `bat_movement_chance_base`, `bat_movement_chance_intelligence_scale`, and
  `bat_movement_fresh_chance_bonus` monster-type fields, preserving the native
  13/1/50 bands with fallback behavior for older packs.

- Counselor ranged-admission chance is now validated through
  `counselor_attack_chance_base` and
  `counselor_attack_chance_intelligence_scale` monster-type fields, preserving
  the native capped 50/5 band with fallback behavior for older packs.

- Shared AiAvoidance fresh-mode movement admission is now validated through
  the `ai_avoidance_movement_fresh_chance_bonus` monster-type field, preserving
  the native 50-point bonus with fallback behavior for older packs.

- Fat/Overlord fresh-mode movement admission is now validated through the
  `fat_movement_fresh_chance_bonus` monster-type field, preserving the native
  50-point bonus with fallback behavior for older packs.

- Rhino ordinary movement admission is now validated through the
  `rhino_movement_chance_base`, `rhino_movement_chance_intelligence_scale`, and
  `rhino_movement_fresh_chance_bonus` monster-type fields, preserving the native
  33/2/50 bands with fallback behavior for older packs.

- Rhino far-distance Move-goal admission now uses the same validated
  `rhino_movement_goal_minimum_distance` and
  `rhino_movement_goal_chance_modulo` gate in charge-miss and blocked-lane
  fallback branches, preserving the native 5/4 fallback for older packs.

- Familiar Lightning damage now uses the optional
  `projectile_damage_random_range` missile field for its native
  `GenerateRnd` upper bound, preserving the native range of ten for older
  mappings.

- Familiar post-melee Lightning now honors the existing optional
  `projectile_damage` missile field before applying its random-damage range,
  preserving the legacy random roll when older mappings omit a fixed value.

- The Hellfire pack now activates Familiar's validated `Bat`/monster-43
  post-melee Lightning mapping, so the shipped composition owns its native
  transport, eight-tick impact phase, and `GenerateRnd(10)` damage bound.

- AiRangedAvoidance adjacent attack admission now uses optional missile fields
  for its validated base, intelligence scale, and divisor, preserving the
  native 6,000/1,000-per-intelligence band and Acid half-band for older
  mappings.

- Familiar post-melee Lightning dispatch now uses an explicit validated
  `post_melee_missile` mapping flag, while older mappings retain the
  `MonsterMagic`/`Lightning` compatibility tuple.

- Mapped Bat and Snake charge projectiles now consume validated missile
  `damage_type` content, while older mappings retain the native Physical
  fallback.

- Mapped Bat and Snake charge projectiles now consume their validated missile
  `spell_id`, while older mappings retain the native spell identity `1`.

- Snake charge collision damage now applies the mapped missile `damage_type`
  through player resistance, while older mappings retain native Physical
  damage behavior.

- Snake charge collisions now honor the existing optional `projectile_damage`
  missile field before applying resistance, while older mappings retain the
  native special-damage range.

- MonsterArrow impacts now enter the validated `projectile_impact_lifetime`
  phase before removal, retaining the native one-tick fallback for older rows.

- Server-owned `MonsterMagic` and `BloodStar` impacts now enter the validated
  `projectile_impact_lifetime` phase, while player-owned spell projectiles and
  native expiry behavior remain unchanged.

- `SnakeCharge` impacts now enter the validated `projectile_impact_lifetime`
  phase after a successful player collision, while blocked, missed, and
  expired charges retain native removal behavior.

- MonsterArrow travel now consumes the validated `projectile_speed` as its
  per-tick tile-step count, while omitted values retain the native speed-one
  fallback and lifetime behavior.

- Mega Inferno segments now consume the mapped `projectile_damage` override,
  while older mappings retain the native per-segment monster-damage roll.

- MonsterArrow impacts now consume mapped `projectile_damage` after the native
  ranged hit roll, while older mappings retain native damage derivation.

- Validated `MonsterArrow` mappings now admit a clear native `AiRanged` lane at
  adjacent distance, preserving the native arrow attack instead of producing a
  no-op; mapped ranged-only monsters still suppress adjacent melee fallback.

- Mega post-action delays are now validated `InfernoController` missile
  fields (`mega_delay_base` and `mega_delay_random_range`), retaining
  native-compatible fallback behavior for older packs.

- SkeletonKing movement delays are now validated monster-type fields
  (`skeleton_king_delay_base` and `skeleton_king_delay_random_range`),
  retaining native-compatible fallback behavior for older packs.

- Rhino recovery delays are now validated monster-type fields
  (`rhino_delay_base` and `rhino_delay_random_range`), retaining
  native-compatible fallback behavior for older packs.

- SkeletonMelee movement and failed-adjacent-attack delay bands are now
  validated monster-type fields, preserving native intelligence reduction
  and deterministic exclusive random ranges for older packs.

- Shared AiAvoidance failed-RoundWalk delays are now validated monster-type
  fields for GoatMelee and Gargoyle, retaining the native exclusive random
  range through compatibility fallback for older packs.

- Counselor failed-ranged-admission and successful-attack delays are now
  separate validated monster-type fields, preserving their distinct native
  intelligence-scaled and fixed-base formulas.

- Counselor failed-ranged-admission movement chance is now a validated
  `counselor_move_chance_percent` monster-type field, preserving the native
  30-percent branch with compatibility fallback for older packs.

- Healthy Counselor-family monsters now preserve native adjacent Flash
  attacks: the server emits both stationary Flash layers with
  `2 * monster_level` magic damage and keeps the native Counselor delay,
  instead of falling through to generic melee.

- Counselor Charged Bolt mappings now emit the native three independent
  `MonsterChargedBolt` projectiles with their native fixed 15-point payload;
  validated `projectile_damage` values remain authoritative overrides.

- Counselor `MonsterChargedBolt` projectiles now retain their typed Lightning
  presentation in the server-to-Godot mapping instead of falling back to a
  generic projectile visual.

- Counselor's native admission now preserves its initial `GenerateRnd(100)`
  boundary, and mapped Firebolt/MonsterMagic projectiles capture the inclusive
  monster damage roll at launch, while fixed payload overrides consume no roll.

- Mapped monster Fireball collisions now roll the native monster damage range
  at each direct or splash impact, while validated `projectile_damage` values
  remain fixed overrides.

- Mapped `MagmaBall` projectiles now preserve the native three-velocity-quanta
  launch offset before their first authoritative simulation tick.

- Storm/ThinLightning controller ticks now roll `2 * RandomIntBetween` from
  the monster damage range before creating each lightning segment.

- Mega delegated Skeleton movement-delay admission is now validated
  `mega_delay_chance_base` and `mega_delay_chance_intelligence_scale`
  monster-type data, preserving the native 35-minus-four-times-intelligence
  band with compatibility fallback for older packs.

- Mega intermediate-distance RandomWalk admission is now validated
  `mega_intermediate_movement_chance_base` and
  `mega_intermediate_movement_chance_intelligence_scale` monster-type data,
  preserving the native 50-plus-ten-times-intelligence band with its 100
  percent cap and compatibility fallback for older packs.

- SkeletonKing RoundWalk admission is now a validated
  `skeleton_king_round_walk_chance_modulo` monster-type field, preserving the
  native modulo-four branch with compatibility fallback for older packs.

- Hork Demon far-distance RoundWalk admission is now a validated
  `hork_demon_round_walk_chance_modulo` monster-type field, preserving the
  native modulo-four branch with compatibility fallback for older packs.

- Shared AiAvoidance Move-goal admission is now validated through
  `ai_avoidance_movement_minimum_distance` and
  `ai_avoidance_movement_chance_modulo` monster-type fields, preserving the
  native minimum-four/modulo-four gate with compatibility fallback for older
  packs.

- Rhino far-distance movement-goal admission is now validated through
  `rhino_movement_goal_minimum_distance` and
  `rhino_movement_goal_chance_modulo` monster-type fields, preserving the
  native minimum-five/modulo-four gate with compatibility fallback for older
  packs.

- AiRangedAvoidance far-distance Move-goal admission is now validated through
  the `ranged_avoidance_movement_chance_modulo` missile field, preserving the
  native modulo-four gate (Acid modulo-eight variant) with compatibility
  fallback for older packs.

- AiRangedAvoidance ordinary RandomWalk admission is now validated through
  `ranged_avoidance_movement_chance_base`,
  `ranged_avoidance_movement_chance_intelligence_scale`, and
  `ranged_avoidance_movement_fresh_chance_bonus` missile fields, preserving the
  native 50/10/30 movement band with compatibility fallback for older packs.

- SkeletonRanged fresh-mode movement admission is now validated through the
  `skeleton_ranged_movement_fresh_chance_bonus` monster-type field, preserving
  the native 50-percent bonus with compatibility fallback for older packs.

- SkeletonKing movement-admission chance is now a validated monster-type
  field, preserving the native intelligence-scaled movement band.

- The client protocol model rejects impossible player life/mana and monster
  vitality bounds before publishing a snapshot to presentation; visual
  clamping remains defensive and never replaces authoritative validation.

- Combat content activation now validates the configured PvP-ear item against
  the active item catalog before constructing sessions, rejecting unknown or
  mismatched native ear projections at the data boundary.

- Stable spell and ability symbols in `content_ids.tsv` are now
  cross-validated against their active catalogs before IDs are published to a
  session.

- World-object rows now optionally resolve stable `object_symbol` values
  through `content_ids.tsv`; activation validates those identities against the
  composed object catalog while numeric-only rows remain compatible.

- Store stock and the Adria service identity now optionally resolve stable
  `store_symbol`/`adria_store_symbol` values; activation validates the store
  namespace against stock and the reserved service identity while numeric-only
  rows remain compatible.

- Static portal rows now optionally resolve stable `portal_symbol` values;
  activation cross-checks them against the retained portal IDs while numeric
  routes remain compatible.

- Static warp rows now optionally resolve stable `warp_symbol` values;
  activation cross-checks them against the retained warp IDs while numeric
  routes remain compatible.

- Quest types, quest state rows, and single-player quest pools now optionally
  resolve stable `quest_symbol` values through `content_ids.tsv`; activated
  rows reject numeric/symbol mismatches while numeric-only legacy rows remain
  compatible.

- Warp gates, linked objects, item quest effects, and follow-on quest activation
  now resolve optional stable quest symbols through the same namespace, with
  activation-time mismatch checks and numeric-only fallback.

- Starting loadout item rows now resolve optional `item_symbol` values through
  the stable item namespace. Activated packs cross-check numeric and symbolic
  identities, validate the active item entries, and retain numeric-only fallback.

- Store stock, unique-item base references, quest completion rewards, and item
  quest effects now resolve optional stable item symbols with numeric-only
  fallback and activation-time mismatch checks.

- Item combinations, catalog-backed monster drops, and the PvP-ear combat rule
  now resolve optional stable item symbols with the same validation guarantees.

- Every shipped base and Hellfire normalized item definition now carries a
  unique stable `item_symbol`; the active item namespace is checked for full
  definition coverage while numeric IDs remain the compatibility projection.

- Shipped normalized unique-item rows now carry stable `base_item_symbol`
  links. Activation cross-checks those symbols against retained numeric base
  IDs and the active item catalog, while numeric-only external packs remain
  compatible.

- Quest-item completion objects now accept `effect_item_symbol` links through
  the stable item namespace. Activation cross-checks the symbol against the
  retained numeric `effect_amount`; non-item object effects remain numeric.

- Unique-item definitions now carry stable `unique_symbol` identities in the
  `unique` namespace. Activation cross-checks each symbol against its numeric
  ID and validates the complete namespace before publication.

- Optional store unique-item stock and monster unique-drop references now
  resolve `unique_item_symbol`/`drop_unique_item_symbol` through the stable
  unique namespace, preserving numeric-only compatibility.

- Class loadouts now resolve `ability_spell_symbol` through the stable ability
  namespace and `starting_spell_symbol` through the spell namespace. Activated
  packs reject symbol/numeric mismatches, while legacy symbols fall back to the
  old spell namespace.

- Monster missile mappings now optionally resolve `spell_symbol` through the
  stable spell namespace. Activated rows reject symbol/numeric mismatches,
  while older rows retain numeric-ID fallback.

- Normalized item spell-source mappings now optionally resolve `spell_symbol`
  through the stable spell namespace. Activated rows reject symbol/numeric
  mismatches, while older rows retain numeric-ID fallback.

- Shipped spell definitions now optionally resolve `spell_symbol` through the
  stable spell namespace. Activated rows reject symbol/numeric mismatches,
  while older rows retain numeric-ID fallback.

- Spell-staff pricing rows now optionally resolve `spell_symbol` through the
  stable spell namespace. Activated rows reject symbol/numeric mismatches,
  while older pricing tables retain numeric-ID fallback.

- Jester dispatch lists now optionally resolve `jester_spell_symbols` through
  the stable spell namespace. Activated lists reject symbol/numeric mismatches,
  while older spell rows retain numeric-ID fallback.

- Direct store spell rows now optionally resolve `spell_symbol` through the
  stable spell namespace. Activated rows reject symbol/numeric mismatches,
  while older store tables retain numeric-ID fallback.

- Stable level and portal symbols are now cross-validated against active world
  and portal catalogs during content activation, without removing legacy
  no-catalog fallbacks.

- The town level identity used by Town Portal and Warp is now externalized in
  validated `world_rules.tsv` content. Active packs must point to a level in
  the externalized world; callers without world-rule content retain native
  level `0` compatibility.

- Adria's service-only store identity is now externalized in the same
  validated `world_rules.tsv` contract. Activation rejects collisions with
  stock vendor IDs, while compatibility callers retain native store `10`.

- Town Portal destination coordinates and slot layout are now optionally owned
  by validated `world_rules.tsv` content. Packs without the geometry columns
  retain their existing spell-row destination behavior. When present, every
  configured slot is checked against the activated town level for bounds and
  walkability before content activation completes.

- The Godot client now classifies server-owned projectile behavior IDs through a
  shared protocol-side presentation mapper and renders distinct elemental,
  spell, arrow, beam, impact, and movement-transition families without moving
  simulation logic into the client. Teleport and Phasing controllers remain
  server-owned while their authoritative positions are now visible to players.

- Projectile snapshots now carry additive server-owned `presentation_kind`
  metadata. Godot prefers that projection and retains behavior/phase mapping only
  as the compatibility fallback for older peers.

- Godot now converts authoritative damage, healing, and experience events into
  typed transient floating feedback positioned from authoritative entity state;
  the native Lua floating-number mods remain compatibility-only.

- The Godot client also maps authoritative status-effect identities to typed
  overlays: Infravision, Search, and Etherealize now have distinct player
  presentation while the server retains all timing, stacking, and admission
  rules.

- Partial external Gargoyle AI tuning now retains the native healing divisor
  offset when its optional random-offset column is omitted; validated content
  cannot reach the previous zero-divisor path.

- Acid puddle impact duration now consumes validated `puddle_impact_lifetime`
  metadata from `monster_missiles.tsv`; omitted values retain the native
  two-tick impact phase.

- Mapped monster projectile impact duration now consumes validated
  `projectile_impact_lifetime` metadata from `monster_missiles.tsv`; omitted
  values retain the native one-tick impact phase across arrows, charges, magic
  missiles, and Hork Spawn.

- Validated MonsterArrow mappings now consume their existing `projectile_speed`
  metadata for travel and `projectile_lifetime` when present; omitted values
  retain the native speed-one and distance-based lifetime fallbacks. Their
  optional `damage_type` selects the matching player-resistance channel, while
  omitted values retain physical arrows.

- Validated BloodStar mappings now consume their existing `projectile_speed`,
  `projectile_lifetime`, optional `damage_type`, and optional
  `projectile_damage` metadata before falling back to mapped-spell transport
  values and native monster damage, keeping older rows compatible.

- The shipped Succubus and Lazarus BloodStar rows now explicitly carry the
  native speed-16, lifetime-256 transport values, removing that fallback from
  the active base pack while preserving compatibility for older packs.

- The shipped Acid and AcidUnique mappings now explicitly carry native
  projectile speed 16; their validated lifetime and puddle terms remain
  authoritative, while older rows retain compatibility fallbacks.

- Reusable bounded grid navigation now lives in `Devilution.Engine` behind
  caller-owned walkability and occupancy predicates. The Diablo module retains
  its level and entity semantics while the deterministic breadth-first order
  and depth boundary are engine-owned and independently tested.

- Nova and Immolation controllers now consume their validated
  
ova_projectile_count` and `immolation_projectile_count` fields in native
  radial emission order; the shipped rows retain the native 36-projectile
  fan-out.

- Reflect retaliation now consumes validated minimum and maximum damage
  percentages from the Reflect row in `spells.tsv`; older spell schemas retain
  the native 20–30% compatibility roll.

- Golem summon health now consumes a validated per-effective-level Q6 base
  term from the Golem row in `spells.tsv`; older spell schemas retain the
  native 320 Q6 per-level health term.

- Golem summon health, damage, and armor now consume validated formula terms
  from the Golem spell row in `spells.tsv`; older spell schemas retain the
  native health 2/3 terms, damage 2/4/8 terms, and armor class 25.

- Melee Quick/Fast/Faster/Fastest attack timing now consumes validated
  first-frame and subsequent-frame tier metadata from `combat.tsv`; older
  combat schemas retain the native 1/0, 2/1, 3/2, and 4/2 compatibility
  defaults.

- Equipment Fast/Faster/Fastest Hit Recovery timing now consumes validated
  ordered tier metadata from `combat.tsv`; older combat schemas retain the
  native 1/2/3 compatibility defaults.

- Equipment-driven FastBlock timing now consumes validated
  `equipment_fast_block_frame_skip` combat metadata; older combat schemas
  retain the native two-frame compatibility default.

- Class FastBlock staff timing now consumes validated
  `fast_block_frame_skip` metadata from `classes.tsv`; older class schemas
  retain the native two-frame compatibility default.

- Player-versus-player block chance now consumes validated
  `pvp_block_level_scale` combat metadata; older combat schemas retain the
  native level-difference scale of two.

- Class NaturalResistance and HeavyHitThreshold now consume validated
  per-level resistance and threshold-bonus percentages from `classes.tsv`;
  older class schemas retain the native 100% and 25% compatibility defaults.

- Class IronSkin now consumes validated armor-divisor metadata from
  `classes.tsv`; older class schemas retain the native level-divided-by-four
  compatibility default.

- Class Critical Strike now consumes validated per-level chance and damage
  multiplier metadata from `classes.tsv`; older class schemas retain the native
  1% per character level and 2× compatibility defaults.

- Monster arrow and snake-charge controllers now dispatch from validated
  missile projectile behavior rather than requiring `GoatRanged`/`Snake`
  labels or `Arrow`/`Rhino` missile names; legacy `Targeted` arrow rows remain
  accepted, mapped arrow rows suppress adjacent melee fallback, and controller
  state stays server-owned.

- Blood Star dispatch now uses validated `BloodStar` missile behavior
  independent of the `Succubus`/`LazarusSuccubus` label when a missile catalog
  is active; the no-catalog legacy path retains native-label compatibility.

- Fat and GoatMelee special-attack selection now honors complete validated
  attack tuples independently of the runtime native-AI label; rows without
  migrated tuples retain their native compatibility paths.

- Fallen fear radius and countdown behavior now use complete validated tuning
  tuples independent of the row's native-AI label; rows without migrated tuning
  retain the native-name compatibility path, while persisted fear state also
  advances and remains snapshot/save compatible for alternate labels.

- Scavenger corpse-healing and search behavior now use validated tuning
  independent of the row's native-AI label; rows without migrated tuning retain
  the native-name compatibility path, while persisted corpse and healing state
  advances for any validated label and remains snapshot/save compatible. A
  standalone validated wounded-health threshold composes the native
  10/4/8/1/75 healing defaults instead of being ignored.

- Scavenger corpse selection now preserves native coin-selected square-scan
  order and chooses the first visible corpse, rather than nearest-corpse
  ordering; the selected identity remains persisted for deterministic pursuit.

- Sneak visibility and hit-retreat behavior now use complete validated tuning
  tuples independent of the row's native-AI label; rows without migrated tuning
  retain the native-name compatibility path, while hidden/retreat state also
  advances and remains snapshot/save compatible for alternate labels.

- Bat adjacent attack and retreat-duration behavior now use validated tuning
  independent of the row's native-AI label; rows without migrated tuning retain
  the native-name compatibility path, while retreat ticks/direction remain
  snapshot/save compatible and advance for any validated label carrying
  migrated Bat tuning. A standalone validated retreat duration composes the
  native 8/4 attack defaults instead of being ignored.

- Bat movement now preserves native `var1`/`var2` admission state: long-idle
  ticks use the `intelligence + 13` band, fresh movement modes use the
  `intelligence + 63` band, and both counters cross snapshots, saves, protocol,
  and canonical hashes. Ordinary Bat identities reach this movement boundary
  even when no optional charge-missile mapping is present.

- Snake lateral drift now preserves native `RandomWalk2` fallback order after
  a blocked drift candidate; the server checks only the original direction and
  its two lateral candidates, with no extra ±90° replacement.

- Gargoyle safety, wounded-health, and healing-formula behavior now use
  validated tuning independent of the row's native-AI label; rows without
  migrated tuning retain the native-name compatibility path, while the
  persisted special lock remains snapshot/save compatible for any validated
  label carrying migrated Gargoyle tuning. Standalone healing fields compose
  the native 2/1 safety boundary and 16/5/4 healing-formula defaults.

- Butcher pursuit now consults the active validated monster-type identity when
  a catalog is present, while complete alternate AI tuning retains precedence;
  direct legacy targets retain the native-name fallback.

- Mapped monster missile dispatch now resolves the active validated
  monster-type identity before the runtime-label compatibility fallback, so
  content can change identity without moving projectile ownership into the
  engine.

- Native `AiRangedAvoidance` adjacent melee now uses its server-owned
  `1000 * (intelligence + 6)` admission band (halved for Acid) before shared
  melee resolution, preserving the native failed-roll defer path. Failed or
  blocked ranged rolls now use deterministic lateral round-walk movement
  instead of generic pursuit.

- Native `AiRangedAvoidance` round-walk movement now follows the native
  candidate order: 90-degree turn, 45-degree turn, straight, then the
  opposite-side `RandomWalk` fallback.

- Native `AiRangedAvoidance` now selects its deterministic movement fallback
  by branch: the far-distance one-in-four (one-in-eight Acid) Move admission
  uses `RoundWalk`, while the ordinary failed ranged admission uses
  `RandomWalk`.

- Native `AiRangedAvoidance` `goalVar1`/`goalVar2` movement state is now
  server-owned and persisted through snapshots, optional protocol fields
  58-59, saves, and the canonical state hash.

- Native `AiRangedAvoidance` ordinary `RandomWalk` movement now preserves its
  `var1`/`var2`-compatible mode counter. Long-idle, fresh-mode, and middle-band
  admissions are server-owned and persist through snapshots, optional protocol
  fields 66-67, saves, and canonical hashes; far-distance `RoundWalk` goals
  remain on fields 58-59.

- Native `AiRangedAvoidance` now flips persisted `goalVar2` when its primary
  `RoundWalk` candidates are blocked, matching the native opposite-side
  `RandomWalk` fallback on the next authoritative state.

- Native Rhino `goalVar1`/`goalVar2` movement state is now server-owned and
  persisted through snapshots, optional protocol fields 60-61, saves, and the
  canonical state hash; Rhino's far-distance Move goal continues across ticks.

- Shared native `AiAvoidance` `goalVar1`/`goalVar2` movement state is now
  server-owned for GoatMelee and Gargoyle and persists through snapshots,
  optional protocol fields 62-63, saves, and the canonical state hash.

- Shared native `AiAvoidance` ordinary movement now preserves the native
  `var1`/`var2`-compatible mode counter for GoatMelee and Gargoyle. Long-idle,
  fresh-mode, and middle-band admissions are server-owned and persist through
  snapshots, optional protocol fields 64-65, saves, and canonical hashes.

- Native Skeleton/Fallen `AiDelay` is now a server-owned countdown after
  failed movement or adjacent attack admissions. The delay persists through
  snapshots, optional protocol field 68, saves, and canonical hashes, so the
  server does not reroll SkeletonAi while native delay mode is active.

- Native `SkeletonBowAi` close-range retreat now preserves its `var1`/`var2`
  movement counter. Long-idle and fresh-mode admissions are server-owned and
  persist through snapshots, optional protocol fields 69-70, saves, and
  canonical hashes; blocked arrow lanes remain server-owned no-ops.

- Native Fat/Overlord movement now preserves its `var1`/`var2`-compatible
  counter. Long-idle and fresh-mode `RandomWalk` admissions are server-owned
  and persist through snapshots, optional protocol fields 71-72, saves, and
  canonical hashes.

- Native Rhino ordinary movement now preserves its `var1`/`var2`-compatible
  counter and `AiDelay` countdown. Validated 33/2/50 long-idle/fresh-mode
  bands (with the native fallback) and failed admissions remain server-owned
  through snapshots, optional protocol fields 73-75, saves, and canonical
  hashes; far-distance Move goals remain in fields 60-61.

- Native Leoric/Skeleton King `AiDelay` is now a server-owned countdown after
  failed movement admissions. The countdown persists through snapshots,
  optional protocol field 76, saves, and canonical hashes; the server does not
  reroll LeoricAi while native delay mode is active.

- Native HorkDemonAi ordinary movement `AiDelay` is now a server-owned
  countdown after failed movement admissions. The countdown persists through
  snapshots, optional protocol field 77, saves, and canonical hashes; the
  server does not reroll HorkDemonAi while native delay mode is active.

- Native `AiRangedAvoidance` no-action ticks now preserve the native
  `AiDelay(GenerateRnd(10) + 5)` countdown. The server-owned delay persists
  through snapshots, optional protocol field 78, saves, and canonical hashes;
  successful movement and ranged admissions remain unaffected.

- Native Snake charge mappings now preserve both non-charge and adjacent
  attack `AiDelay` countdowns. The server-owned delay persists through
  snapshots, optional protocol field 79, saves, and canonical hashes; native
  lateral drift and admitted adjacent attacks remain server-owned.

- Native Counselor non-adjacent failed ranged/movement admissions now preserve
  the native `AiDelay` countdown, and successful mapped ranged actions now start
  the native post-action `GenerateRnd(10) + 5` delay. The server-owned delay
  persists through snapshots, optional protocol field 80, saves, and canonical
  hashes; movement and wounded retreat remain server-owned.

- Shared native `AiAvoidance` failed `RoundWalk` admissions now preserve the
  native `AiDelay(GenerateRnd(10) + 10)` countdown for GoatMelee and Gargoyle.
  The server-owned delay persists through snapshots, optional protocol field
  81, saves, and canonical hashes; the next tick decrements the delay without
  rerolling movement or attack admission.

- Native `AiRanged` post-attack delays now preserve the
  `AiDelay(GenerateRnd(20))` countdown for generic ranged mappings. The
  server-owned delay persists through snapshots, optional protocol field 82,
  saves, and canonical hashes; active projectiles no longer permit a second
  ranged admission while the native delay is pending.

- Native intermediate-distance `MegaAi` post-action delays now preserve the
  `AiDelay(GenerateRnd(10) + 5)` countdown in dedicated server-owned state,
  persisted through optional protocol field 83, snapshots, saves, and the
  canonical hash.

- Legacy native-AI fallback controllers now consult the active validated
  monster-type identity before the runtime-label compatibility path when no
  complete migrated tuning is present; alternate complete tuning retains
  precedence.

- SkeletonRanged retreat and clear-lane arrow admission now use complete
  validated tuning tuples independent of the row's native-AI label; rows
  without migrated tuning retain the native-name compatibility path, and the
  validated tuple suppresses adjacent melee fallback.

- Zombie action, random-direction, and movement-distance behavior now use
  complete validated tuning tuples independent of the row's native-AI label;
  rows without migrated tuning retain the native-name compatibility path.
  Successful action rolls in the intermediate distance band now also perform
  the native random walk instead of leaving the authoritative target idle.

- Snake charge mappings now retain the native non-charge drift branch: targets
  outside the charge boundary use deterministic lateral movement, while the
  existing generic navigation fallback remains available during native delay
  ticks.

- Fat/Overlord movement, adjacent attack, and special-band behavior now use
  complete validated tuning tuples independent of the row's native-AI label;
  rows without migrated tuning retain the native-name compatibility path.
  Admitted non-adjacent movement now uses the native `RandomWalk` candidate
  order before bounded navigation fallback.

- GoatMelee movement, adjacent attack, and wounded-special behavior now use
  complete validated tuning tuples independent of the row's native-AI label;
  rows without migrated tuning retain the native-name compatibility path.
  A standalone complete wounded-special tuple now composes the native 23/2
  attack-band defaults instead of being ignored. Non-adjacent movement now
  preserves the native one-in-four RoundWalk and ordinary RandomWalk branches
  through deterministic candidate order without bounded pursuit replacement.

- SkeletonMelee movement-delay and adjacent attack admission now use complete
  validated tuning tuples independent of the row's native-AI label; rows
  without migrated tuning retain the native-name compatibility path. Delay
  exits now use the native `RandomWalk` candidate order before bounded
  navigation fallback.

- Fallen normal-state AI now delegates to the same server-owned SkeletonAi
  movement and adjacent attack bands as native `FallenAi`; fear state retains
  precedence, and specialized validated controller tuples are not shadowed.

- Butcher movement now uses the native non-adjacent `RandomWalk` candidate order
  before bounded navigation fallback; validated identity precedence remains
  server-owned.

- Skeleton King non-adjacent movement now uses deterministic native
  `RoundWalk`/`RandomWalk` candidates after summon admission is evaluated;
  validated summon identity and server-owned summon precedence remain intact.

- Hork Demon non-adjacent movement now uses deterministic native
  `RoundWalk`/`RandomWalk` candidates after Hork Spawn admission is evaluated;
  validated spawn identity and server-owned summon precedence remain intact.

- Mega far-distance movement now delegates to the server-owned SkeletonAi
  `35 - 4 * intelligence` delay and deterministic `RandomWalk` boundary when
  Inferno is outside its validated maximum range.

- Mega's far-distance SkeletonAi delegation now persists the native
  `AiDelay(15 - 2 * intelligence + GenerateRnd(10))` countdown through the
  existing Skeleton delay state, snapshots, saves, and canonical hashes.

- Mega intermediate-distance Inferno misses now consume the native
  `2 * (5 * intelligence + 25)` RandomWalk fallback before generic pursuit;
  clear-lane and blocked-lane outcomes remain server-owned.

- Counselor wounded-adjacent retreat now enters a persisted four-attempt native
  `RandomWalk` state in the server before mapped ranged dispatch; retreat state
  is carried through snapshots, protocol saves, and the authoritative state
  hash, while the Godot client remains behavior-blind.

- Counselor mapped ranged rows now consume validated base and intelligence-scale
  admission terms, preserving the native 50/5 fallback and 30-percent
  `RoundWalk` branch; failed admissions defer on the server instead of
  generic-pursuing.

- Rhino charge admission is now selected by its complete validated tuning tuple
  independent of the row's native-AI label; rows without migrated tuning retain
  the native-name compatibility path.

- Rhino charge misses and blocked lanes now consume the native far-distance
  `RoundWalk` and ordinary `33 + 2 * intelligence` `RandomWalk` fallback before
  generic navigation.

- Mapped Gloom `BatCharge` misses and blocked lanes now retain the native
  `BatAi` movement branch: the server applies the standing-tick compatibility
  `63 + intelligence` admission and deterministic `RandomWalk` candidates
  before suppressing generic pursuit. Adjacent ticks remain available to the
  shared melee boundary.

- SkeletonRanged blocked arrow lanes now retain the native `SkeletonBowAi`
  defer at long range. The server does not replace a missing clear lane with
  generic pursuit; its validated close-range retreat branch remains evaluated
  before that no-op.

- GoatRanged blocked arrow lanes now retain the native `AiRanged` defer at long
  range. Its validated close-range avoidance branch remains evaluated first;
  a missing clear lane no longer delegates movement to generic navigation.

- Snake non-charge and adjacent attack `AiDelay` ticks now remain server-owned
  countdowns. The validated lateral drift and melee branches resume only after
  the native delay expires, and generic pursuit no longer replaces delayed
  ticks.

- Zombie action ticks now preserve the native blocked-walk no-op. When the
  validated action band succeeds but no native one-step candidate is available,
  the server does not replace that result with generic navigation.

- HorkDemon admitted movement now preserves the native blocked-candidate
  no-op. When both deterministic `RoundWalk` and `RandomWalk` orders are
  unavailable, the server leaves the monster stationary rather than navigating.

- Mega's validated far-distance Skeleton fallback now preserves its native
  blocked-`RandomWalk` no-op; no bounded generic path replaces an admitted but
  unavailable movement candidate.

- Skeleton King's validated `LeoricAi` movement now preserves its native
  blocked-`RandomWalk` no-op; no bounded generic path replaces an admitted but
  unavailable movement candidate.

- Fat/Overlord's validated movement now preserves its native blocked
  `RandomWalk` no-op; no bounded generic path replaces an admitted but
  unavailable movement candidate.

- SkeletonMelee's validated movement now preserves its native blocked
  `RandomWalk` no-op; no bounded generic path replaces an admitted but
  unavailable movement candidate.

- Butcher's validated movement now preserves its native blocked `RandomWalk`
  no-op; no bounded generic path replaces an admitted but unavailable movement
  candidate.

- GoatMelee's validated far-distance movement now preserves its native blocked
  `RoundWalk` no-op; no bounded generic path replaces an admitted but
  unavailable movement candidate.

- GoatMelee's ordinary admitted movement now preserves the native `RandomWalk`
  candidate order and blocked-tick no-op instead of invoking bounded generic
  pursuit.

- Gargoyle healthy non-adjacent ticks now preserve native `AiAvoidance`
  `RoundWalk`/`RandomWalk` candidate order and blocked-tick no-op after the
  server-owned healing branch.

- Gargoyle adjacent attacks now preserve the native `23 + 2 * intelligence`
  `AiAvoidance` admission before shared authoritative melee resolution.

- Scavenger corpse-seeking movement now preserves native `RandomWalk` candidate
  order and consumes the tick when all five native candidates are blocked; no
  eight-way replacement is used.

- Skeleton King summon admission is now selected by its complete validated
  tuning tuple independent of the row's native-AI label; rows without migrated
  tuning retain the native-name compatibility path, and summon candidates can
  carry the complete SkeletonMelee tuple under an alternate validated label.

- Hork Spawn admission is now selected by a complete validated tuning tuple,
  independent of the row's native-AI label; rows without migrated tuning retain
  the native-name compatibility path, while content can assign the controller
  to another validated AI identity.

- Hork Spawn now requires its summoned native monster identity in the validated
  `monster_missiles.tsv` mapping; the processor no longer embeds native ID 117,
  and malformed or stale mappings are rejected during content activation.

- Weapon classification now consults validated `Weapon`, `Sword`, `Axe`,
  `Mace`, `Bow`, and `Staff` tags for attack-animation selection, Monk flexible
  blocking, and loadout off-hand validation; metadata-free or ambiguous rows
  retain the legacy `ItemClass` fallback.

- PvP-ear native projection now comes from optional validated `combat.tsv`
  fields for the native item index and miscellaneous ID; omitted fields retain
  the compatibility values 23 and 44, while activation still rejects an ear
  definition that disagrees with the configured projection.

- Class Recharge admission now consults validated `Staff` item tags through the
  active catalog; direct legacy states without catalog metadata retain the
  native item-type path.

- Monk chest-armor scaling now consults validated `LightArmor`, `MediumArmor`,
  and `HeavyArmor` item tags through the active catalog; direct legacy states
  retain the native item-type path.

- Canonical Gold recognition and generated stack identity now consult the
  validated `Gold` item tag through the active catalog; metadata-free states
  retain the native type/index fallback.

- Flexible and fast block staff eligibility now consults validated `Staff`
  item tags through the active catalog; legacy states retain numeric fallback.

- Store recharge validation now accepts only explicit `ChargedItem` sources
  when source metadata is present; metadata-free legacy rows retain the native
  staff and miscellaneous-ID fallback.

- General store recharge now consults the active catalog's validated
  `ChargedItem` source metadata, including unambiguous miscellaneous-ID
  mappings, and its `Staff` tag—including unambiguous misc-ID tag mappings—
  before applying the native type/miscellaneous-ID fallback; conflicting item
  metadata therefore remains content-owned without breaking legacy rows.

- Player loadout validation now uses the active item definition's `Shield`
  subtype for off-hand eligibility and only falls back to native item type
  when no explicit equipment subtype is declared.

- Native player attack-animation selection now consumes validated equipment
  subtype tags for sword, axe, mace, bow, staff, and shield profiles; legacy
  snapshots and untagged rows retain numeric item-type fallback.

- Equipment damage profiles now consult active item content tags through the
  retained native item index; conflicting duplicate mappings are rejected and
  legacy item-type classification remains the compatibility fallback.

- Equipment subtype classification is now centralized in one content-first
  helper used by player attack profiles, block eligibility, Hellfire weapon
  decay, loadout validation, equipment damage, armor scaling, and monster
  weapon modifiers; metadata-free states retain their native item-type fallback.

- Gold identity is now centralized in the same validated item-classification
  helper for wallet topology, spending, generation, and death drops; legacy
  states retain the native type/index fallback.

- Belt eligibility is now centralized in the same content-first item
  classification seam for loadout validation, including unambiguous misc-ID
  tag mappings when the compatibility item index is unavailable; legacy rows
  retain the native equipment-location/item-class fallback.

- Two-handed equipment classification is now centralized for loadout topology
  and dual-wield admission; active rows use `TwoHanded` tags while legacy
  states retain the native equipment-location fallback. Unambiguous misc-ID
  tags cover compatibility states that lack a native item-index match.

- Loadout equipment-slot admission is now content-first for hand, two-handed,
  body-armor, helmet, ring, and amulet locations; untagged legacy rows retain
  the native `equip_location` mapping, while unambiguous misc-ID tags cover
  compatibility states that lack a native item-index match.

- Schema 1.146 adds normalized `Sword`, `Axe`, `Mace`, `Bow`, `Shield`,
  `Staff`, `Belt`, `OneHanded`, `TwoHanded`, `BodyArmor`, `Helmet`, `Ring`,
  and `Amulet` subtype tags to `items.tsv`; player and monster weapon
  modifiers consume those tags before falling back to native item type.

- Schema 1.147 adds optional 
ame` and `identified_name` item metadata. The
  authoritative catalog validates and projects these labels into the existing
  server-owned item state and protocol fields; legacy `itemdat.tsv` names map
  through the same seam.

- Schema 1.148 adds the validated `item_cursor_graphics.tsv` catalog and the
  optional normalized `cursor_graphic` item column. Normalized and legacy item
  loaders resolve the same content-owned symbols to native cursor IDs, while
  callers that omit the catalog retain compatibility fallback behavior.

- Schema 1.149 projects normalized and legacy unique-item names into generated
  item state, so unique presentation no longer inherits a base-item label.
  Unique names are validated at the catalog boundary and remain server-owned
  through snapshots and protocol projections.

- Schema 1.150 adds optional normalized unique-item `cursor_graphic` metadata.
  Normalized and legacy unique rows resolve shared cursor symbols through the
  active cursor catalog, while omitted symbols preserve the base-item cursor.

- Schema 1.151 adds optional normalized item `min_monster_level` metadata.
  The item catalog retains native random-drop eligibility thresholds from both
  normalized and legacy tables for server-owned drop planning.

- Schema 1.152 adds optional normalized item `drop_rate` metadata. The catalog
  retains native weighted random-drop rates, including zero-rate exclusions.

- Schema 1.153 adds optional normalized item `usable` metadata. Explicit
  native iUsable values now gate consumable source admission; absent metadata
  preserves the compatibility path.

- Schema 1.154 adds optional normalized item `min_armor_class` and
  `max_armor_class` metadata. When both are present, generated items use the
  native inclusive armor range; rows that omit them retain fixed armor values.

- Schema 1.155 adds optional normalized item `special_effects` metadata.
  Validated symbolic effects map to the same authoritative equipment flags as
  legacy `specialEffects`; numeric `flags` remain additive, and legacy unknown
  effect names retain their compatibility behavior.

- Schema 1.156 adds optional normalized item `hellfire_effects` metadata.
  Validated symbolic Hellfire damage/armor effects now project directly into
  generated item state; legacy unique and affix effect paths remain available.

- Schema 1.157 adds optional spell `teleport_search_radius` metadata.
  Teleport destination resolution now consumes the validated crawl radius in
  both its cast admission and movement-controller tick; direct legacy spell
  definitions retain the native five-tile default.

- Schema 1.158 adds optional spell `warp_search_radius` metadata. Warp
  destination resolution now consumes the validated crawl radius in both cast
  admission and world-transition application; direct legacy world-command
  callers retain the native five-tile default.

- Schema 1.159 adds optional Town Portal placement and lifetime metadata:
  `town_portal_search_radius` and `town_portal_lifetime`. Portal creation now
  consumes the validated crawl radius and expiry ticks; direct legacy
  world-command callers retain native five-tile, 100-tick defaults.

- Schema 1.160 adds optional monster magic-projectile accuracy metadata to
  `combat.tsv`. Monster-owned magic projectiles now use validated base, level,
  and distance terms for player-hit admission; omitted values retain the
  native 10/2/2/40/2 formula and shared 20/25/30 level-specific floors.

- Schema 1.161 adds optional Snake charge special-hit accuracy metadata to
  `combat.tsv`. Charge admission now uses the validated native base to-hit
  value with shared monster accuracy terms; omitted values retain the native
  500 compatibility value.

- Schema 1.162 adds optional Acid Puddle player-hit accuracy metadata to
  `combat.tsv`: `monster_acid_puddle_minimum_hit_chance` and
  `monster_acid_puddle_base_hit_chance`. Persistent puddle admission now uses
  validated level floors and base-hit terms; omitted values retain native
  10/40 compatibility values.

- Schema 1.163 adds optional `projectile_damage` metadata to
  `monster_missiles.tsv`. Mapped monster projectiles consume a validated fixed
  impact value when declared; omitted values retain behavior-specific/native
  damage derivation.

- Schema 1.164 adds optional `maximum_resistance` metadata to `combat.tsv`.
  Active player elemental resistance calculations now use the validated cap;
  direct legacy resistance callers retain the native 75-percent default.

- Schema 1.165 adds optional `maximum_attribute` metadata to `combat.tsv`.
  Active player equipment recalculation now uses the validated primary-attribute
  ceiling; omitted values retain the native 750 default for compatibility callers.

- Schema 1.166 adds optional `barbarian_weapon_strength_damage_divisor` metadata
  to `combat.tsv`. Active Barbarian axe/mace damage scaling now uses the
  validated divisor; omitted values retain the native 75 divisor for compatibility callers.

- Schema 1.167 adds optional `rogue_strength_dexterity_damage_divisor` metadata
  to `combat.tsv`. Active Rogue physical damage scaling now uses the validated
  divisor; omitted values retain the native 200 divisor for compatibility callers.

- Schema 1.168 adds optional `monk_staff_unarmed_damage_divisor` metadata to
  `combat.tsv`. Active Monk staff/unarmed physical damage scaling now uses the
  validated divisor; omitted values retain the native 150 divisor for compatibility callers.

- Schema 1.169 adds optional `monk_weapon_damage_divisor` metadata to
  `combat.tsv`. Active Monk non-staff weapon physical damage scaling now uses
  the validated divisor; omitted values retain the native 300 divisor for compatibility callers.

- Schema 1.170 adds optional `bard_sword_strength_dexterity_damage_divisor`
  metadata to `combat.tsv`. Active Bard sword physical damage scaling now uses
  the validated divisor; omitted values retain the native 150 divisor for compatibility callers.

- Schema 1.171 adds optional `bard_bow_strength_dexterity_damage_divisor`
  metadata to `combat.tsv`. Active Bard bow physical damage scaling now uses
  the validated divisor; omitted values retain the native 250 divisor for compatibility callers.

- Schema 1.172 adds optional `bard_default_strength_damage_divisor` metadata
  to `combat.tsv`. Active Bard fallback physical damage scaling now uses the
  validated divisor; omitted values retain the native 100 divisor for compatibility callers.

- Schema 1.173 adds optional `standard_strength_damage_divisor` metadata to
  `combat.tsv`. Active standard-class fallback physical damage scaling now uses
  the validated divisor; omitted values retain the native 100 divisor for compatibility callers.

- Schema 1.174 adds optional `barbarian_bow_strength_damage_divisor` metadata
  to `combat.tsv`. Active Barbarian bow physical damage scaling now uses the
  validated divisor; omitted values retain the native 300 divisor for compatibility callers.

- Schema 1.175 adds optional `barbarian_default_strength_damage_divisor`
  metadata to `combat.tsv`. Active Barbarian fallback physical damage scaling
  now uses the validated divisor; omitted values retain the native 100 divisor for compatibility callers.

- Schema 1.176 adds optional `barbarian_vitality_damage_divisor` metadata to
  `combat.tsv`. Active Barbarian weaponless physical damage scaling now uses
  the validated divisor; omitted values retain the native 100 divisor for compatibility callers.

- Schema 1.177 adds optional `thorns_damage_minimum` and
  `thorns_damage_maximum` metadata to `combat.tsv`. Active Thorns retaliation
  now uses the validated damage range; omitted values retain the native 1–3 range for compatibility callers.

- Schema 1.178 adds optional `hellfire_decay_damage_reduction` and
  `hellfire_decay_removal_threshold` metadata to `combat.tsv`. Active Hellfire
  Decay now uses the validated decrement and removal threshold; omitted values retain the native 5/-100 behavior for compatibility callers.

- Schema 1.179 adds optional `hellfire_devastation_chance_percent` and
  `hellfire_devastation_damage_multiplier` metadata to `combat.tsv`. Active
  Hellfire Devastation now uses the validated proc chance and multiplier; omitted values retain the native 5%/3× behavior for compatibility callers.

- Schema 1.180 adds optional `hellfire_jesters_roll_maximum`,
  `hellfire_jesters_expansion_threshold`, and
  `hellfire_jesters_expansion_multiplier` metadata to `combat.tsv`. Active
  Hellfire Jesters now uses the validated roll and upper-half expansion terms;
  omitted values retain the native 0–200/100/5 behavior for compatibility callers.

- Schema 1.181 adds optional `hellfire_peril_damage_multiplier` and
  `hellfire_doppelganger_chance_percent` metadata to `combat.tsv`. Active
  Hellfire Peril and Doppelganger now use validated native terms; omitted
  values retain the native 2× and 10% behavior for compatibility callers.

- Schema 1.184 adds optional `critical_strike_chance_per_level_percent`
  metadata to `classes.tsv`. Active class Critical Strike now uses the
  validated per-level proc chance; omitted values retain the native 1% per
  character level behavior.

- Schema 1.185 adds optional `iron_skin_level_divisor` metadata to
  `classes.tsv`. Active IronSkin armor now uses the validated class divisor;
  omitted values retain the native level-divided-by-four behavior.

- Schema 1.186 adds optional 
atural_resistance_per_level_percent` and
  `heavy_hit_threshold_bonus_percent` metadata to `classes.tsv`. Active class
  defense now uses the validated percentages; omitted values retain the native
  100% and 25% behavior.

- Schema 1.187 adds optional `pvp_block_level_scale` metadata to `combat.tsv`.
  Active player-versus-player block chance now uses the validated scale;
  omitted values retain the native scale of two.

- Schema 1.188 adds optional `fast_block_frame_skip` metadata to `classes.tsv`.
  Active FastBlock staff timing now uses the validated class value; omitted
  values retain the native two-frame behavior.

- Schema 1.189 adds optional `equipment_fast_block_frame_skip` metadata to
  `combat.tsv`. Equipment FastBlock timing now uses the validated combat value;
  omitted values retain the native two-frame behavior.

- Schema 1.190 adds optional Fast/Faster/Fastest Hit Recovery skip metadata to
  `combat.tsv`. Active recovery timing now uses the validated ordered values;
  omitted values retain the native 1/2/3 behavior.

- Schema 1.191 adds optional Quick/Fast/Faster/Fastest melee attack-speed skip
  metadata to `combat.tsv`. Active melee timing now uses validated skips for
  both first-frame and subsequent-frame paths; omitted values retain the
  native 1/0, 2/1, 3/2, and 4/2 behavior.

- Schema 1.192 adds optional Golem summon formula metadata to `spells.tsv`:
  health level/mana terms, damage level/offset terms, and armor class. Active
  Golem summons now use validated row values; omitted values retain the native
  health 2/3, damage 2/4/8, and armor 25 behavior.

- Schema 1.193 adds optional `golem_hit_points_level_base_q6` metadata to the
  Golem row in `spells.tsv`; omitted values retain the native 320 Q6 per-level
  health term.

- Schema 1.202 adds an optional persistent-projectile player damage divisor to
  `spells.tsv`; omitted values retain the native divisor of two.

- Schema 1.203 adds optional direct player-magic hit tuning to `combat.tsv`;
  Charged Bolt, Firebolt, and Bone Spirit player-hit rolls retain native 2/5/95
  character-level penalty and chance bounds when omitted.

- Schema 1.204 adds an optional Bone Spirit player damage divisor to
  `spells.tsv`; omitted values retain the native one-third current-life rule.

- Schema 1.205 adds an optional monster regeneration level divisor to
  `combat.tsv`; omitted values retain the native `level / 2` recovery rule.

- Schema 1.206 adds an optional monster-player block level scale to
  `combat.tsv`; omitted values retain the native level-difference scale of two.

- Schema 1.207 adds an optional Acid puddle impact lifetime to
  `monster_missiles.tsv`; omitted values retain the native two-tick impact
  phase.

- Schema 1.208 adds an optional mapped monster projectile impact lifetime to
  `monster_missiles.tsv`. MonsterArrow, SnakeCharge, MonsterMagic, and
  BloodStar mappings consume the shared phase; omitted values retain the native
  one-tick impact phase, while other authoritative controllers retain their
  controller-specific lifecycle transitions.

- Schema 1.209 adds optional quest dialogue and completion transition variables
  to `quests.tsv` and `quest_states.tsv`; omitted values retain the existing
  no-transition behavior while shipped unique quests preserve their native
  terminal `_qvar1` states.

- Schema 1.210 adds optional follow-on quest activation and route-activation
  policy columns; Lazarus can now activate the server-owned Diablo quest while
  that quest remains suppressed from premature route entry.

- Schema 1.211 adds optional `diablo` monster-type and `completion_diablo`
  quest-state columns; validated boss targets now complete the server-owned
  Diablo quest through an explicit native identity boundary.

- Schema 1.212 adds optional `fire_resistance_hell`,
  `lightning_resistance_hell`, `magic_resistance_hell`, and
  `acid_resistance_hell` columns to `monster_types.tsv`. Hell materialization
  now selects those validated native `resistanceHell` projections while
  Normal/Nightmare retain the base resistance channels; omitted columns keep
  the existing resistance values for compatibility.

- Schema 1.213 adds optional native difficulty tuning columns to `combat.tsv`.
- Schema 1.214 adds external ranged-attack frame timing columns to `combat.tsv`, including the Hellfire zero-skip variant.
  Level, accuracy, armor, damage, experience, and initial-health formulas now
  consume validated pack values; omitted columns retain the native defaults,
  including the distinct Nightmare `2 * (base + 2)` and Hell `4 * base + 6`
  damage formulas.

- Schema 1.215 adds optional quest-completion reward item metadata to
  `quest_states.tsv`; the Hellfire Defiler's Cathedral Map now spawns at the
  authoritative quest death boundary rather than as a static monster drop.

- Schema 1.216 adds the validated `QuestSequence` world-object effect. Hellfire
  Na-Krul's three tome interactions now use persistent server-owned quest
  variables, reset on wrong order, and complete the quest on the native order.

- Schema 1.217 adds validated content-owned item combinations. The Hellfire
  Na-Krul note fragments now combine on authoritative pickup into the
  reconstructed note while preserving native item identities.

- Schema 1.218 adds validated item quest effects. The Hellfire Cathedral Map
  now activates Grave Matters and sets its native first quest variable when
  picked up by the authoritative player.

- Schema 1.219 adds the `QuestItemCompletion` object effect. The Hellfire grave
  object now consumes the Cathedral Map and completes Grave Matters through
  the server-owned object interaction boundary; object completions also honor
  the quest catalog's completion variables and follow-on activation metadata.

- Schema 1.220 adds quest-gated level triggers. The Hellfire crypt entrance is
  now a server-owned town warp that opens only after Grave Matters completes.
  Procedural crypt return triggers remain part of the native map-synthesis
  backlog until their generated tile identities are externalized.

- Schema 1.221 adds `world_rules.tsv` with the externalized town-level identity
  used by Town Portal and Warp.

- Schema 1.222 adds the Adria service-only store identity to `world_rules.tsv`;
  activation rejects zero and stock-vendor collisions.

- Schema 1.231 separates class capability symbols into the stable `ability`
  namespace and assigns Holy Bolt the stable `spell:holy_bolt` identity while
  legacy loadouts retain spell-namespace fallback.

- Schema 1.232 adds optional `quest_symbol` columns to quest type, state, and
  pool tables; activated rows resolve the `quest` namespace and reject
  numeric/symbol mismatches while older numeric-only rows retain fallback.

- Schema 1.233 extends optional quest symbols to warp gates, linked objects,
  item quest effects, and follow-on quest activation.

- Schema 1.234 adds optional `item_symbol` to `class_loadout_items.tsv` and
  validates the shipped stable `item` entries against the active item catalog.

- Schema 1.235 extends optional item symbols to `stores.tsv`,
  `unique_items.tsv`, quest reward columns, and `item_quest_effects.tsv`.

- Schema 1.241 extends stable unique symbols to optional store unique-item
  stock and monster unique-drop references. Numeric-only links remain
  compatible while active symbols are validated against the unique catalog.

- Schema 1.242 adds the stable `monster` namespace. Monster type rows and
  authored encounter, missile selector/spawn, unique-monster, and quest
  completion links may carry symbols cross-checked against retained numeric
  IDs; numeric-only rows remain compatible.

- Schema 1.243 adds the stable `class` namespace. Class-keyed loadout and
  animation rows may carry `class_symbol`, including the valid native class
  ID zero; numeric-only rows remain compatible.

- Schema 1.244 adds the stable `level` namespace to level definitions, warps,
  portals, and Town Portal world rules. Symbolic links are cross-checked
  against active geometry while numeric-only links remain compatible.

- Schema 1.245 extends stable level symbols to authored monster, object, quest,
  and combat restart links through `level_symbol` columns. Numeric-only rows
  remain compatible while links are checked against the composed world.

- Schema 1.251 adds normalized stable Hellfire quest-item definitions for Rune
  Bomb, Theodore, Auric Amulet, Brown Suit, and Grey Suit while preserving their
  native item-index and cursor projections.

- Schema 1.252 normalizes stable symbols, display names, and native cursor
  projections for the first five Hellfire unique base-item rows, including the
  shared Hellfire ring base used by multiple unique definitions.

- Schema 1.253 normalizes stable symbols, display names, and native cursor
  projections for the next five Hellfire unique base-item rows: Veil of Steel,
  Arkaine's Valor, Griswold's Edge, Bovine Plate, and The Butcher's Cleaver.

- Schema 1.254 normalizes stable symbols, display names, and native cursor
  projections for the next five Hellfire bow base-item rows: Hunter's Bow, Long
  Bow, Short Bow, Long War Bow, and Short Battle Bow.

- Schema 1.255 normalizes stable symbols, display names, and native cursor
  projections for the next five Hellfire sword base-item rows: Dagger, Sabre,
  Falchion, Broad Sword, and Long Sword.

- Schema 1.256 normalizes stable symbols, display names, and native cursor
  projections for the next five Hellfire sword base-item rows: Claymore, Broad
  Sword, Scimitar, Bastard Sword, and Two-Handed Sword. The duplicate Hellfire
  Broad Sword uses a distinct stable symbol while retaining its native
  projection.

- Schema 1.257 normalizes stable symbols, display names, native cursor
  projections, and `Axe` generation tags for the next five Hellfire axe
  base-item rows: Large Axe, Broad Axe, Battle Axe, Small Axe, and Great Axe.

- Schema 1.258 normalizes stable symbols, corrected native indices, display
  names, native cursor projections, and `Mace` generation tags for the next
  five Hellfire mace-family rows: Mace, Maul, Flail, Morning Star, and War
  Hammer.

- Schema 1.259 normalizes stable symbols, native indices, display names, native
  cursor projections, and `Staff` generation tags for the next five Hellfire
  staff rows: Long Staff, War Staff, Short Staff, Composite Staff, and Quarter
  Staff. Quarter Staff retains its native `SHORT_STAFF` cursor projection.

- Schema 1.260 normalizes stable symbols, native indices, display names, and
  native cursor projections for the next five Hellfire armor rows: Helm, Skull
  Cap, Great Helm, Crown, and Rags. Rags uses its native LightArmor/BodyArmor
  placement while the other four rows retain helmet placement.

- Schema 1.261 normalizes stable symbols, native indices, display names, and
  native cursor projections for the next five Hellfire armor rows: Studded
  Leather Armor, Cloak, Leather Armor, Robe, and Chain Mail. These rows use
  native body-armor placement; Chain Mail corrects the retained native index
  to `64` and the native `CHAIN_MAIL` cursor identity.

- Schema 1.262 normalizes stable symbols, native indices, display names, and
  native cursor projections for the next five Hellfire armor/shield rows:
  Breast Plate, Cape, Plate Mail, Full Plate Mail, and the duplicate Hellfire
  Buckler. Armor rows use native body-armor placement; the Hellfire Buckler
  uses native one-handed shield placement and a distinct stable symbol.

- Schema 1.263 normalizes stable symbols, native indices, display names, and
  native cursor projections for the next five Hellfire shield/amulet rows: Kite
  Shield, Small Shield, Large Shield, Gothic Shield, and Amulet. Shields use
  native one-handed placement; Amulet restores the native level-8 base row and
  cursor projection.

- Schema 1.264 normalizes the Hellfire Sorcerer starting staff overlay. The
  `hellfire_staff_charged_bolt` row retains its native short-staff identity and
  Hellfire Firebolt charged-item behavior while restoring native display and
  cursor projections plus the stable `firebolt` spell reference.

- Schema 1.265 externalizes the five native rune consumables: Rune of Fire,
  Rune of Lightning, Greater Rune of Fire, Greater Rune of Lightning, and Rune
  of Stone. Their stable item identities retain native indices and cursor
  graphics, miscellaneous IDs, drop levels, and values while resolving
  consumable spell sources through validated spell rows.

- Schema 1.266 externalizes 22 native spell-backed scroll rows with stable item
  identities, native indices, cursor graphics, scroll miscellaneous IDs, level
  gates, values, drop rates, and validated spell references. Legacy item-table
  parsing now preserves the native 21/22 scroll IDs; Identify scrolls remain a
  separate item-action migration because Identify is also a class ability.

- Schema 1.268 externalizes nine Hellfire quest and miscellaneous item rows
  (Magic Rock, Tavern Sign, Golden Elixir, Anvil of Fury, Black Mushroom, Brain,
  Fungal Tome, Spectral Elixir, and Blood Stone) with stable IDs, native cursor
  identities, item classes, miscellaneous IDs, level gates, and usability.

- Schema 1.269 externalizes the Hellfire full-potion and rejuvenation family,
  including fixed Full Healing and Full Mana rows plus random Healing, Full
  Healing, Mana, Full Mana, Rejuvenation, and Full Rejuvenation rows with stable
  IDs, native potion miscellaneous IDs, cursor identities, level gates, drop
  rates, and usability.

- Schema 1.270 externalizes the Hellfire oil and elixir consumables: Blacksmith
  Oil, Oil of Accuracy, Oil of Sharpness, Oil of Fortitude, and the Strength,
  Magic, Dexterity, and Vitality Elixirs, preserving native indices,
  miscellaneous IDs, cursor identities, level gates, values, drop rates, and
  usability.

- Schema 1.271 externalizes the base helmet, body-armor, and shield rows that
  share stable projections with Hellfire, preserving base native indices, armor
  ranges, durability, requirements, cursor identities, level gates, values,
  and equipment subtypes while retaining variant-specific overlays.

- Schema 1.272 externalizes the base standard weapon rows for swords, axes,
  maces, bows, and staves, preserving native damage ranges, durability,
  requirements, cursor identities, level gates, values, and weapon subtypes;
  shared symbols retain Hellfire overlay identity while base-only rows use new
  stable symbols.

- Schema 1.273 completes the remaining base armor variants: Cap, Full Helm,
  Quilted Armor, Hard Leather Armor, Ring Mail, Scale Mail, Splint Mail, Field
  Plate, Gothic Plate, Buckler, and Tower Shield, preserving native armor
  ranges, durability, requirements, cursor identities, level gates, values,
  and equipment subtypes.

- Schema 1.274 externalizes the remaining base potion, oil, and elixir rows at
  native indices 77–90 plus the native healing scroll at index 91, preserving
  native miscellaneous IDs, values, level gates, drop rates, cursor identities,
  usability, and the scroll's consumable spell source.

- Schema 1.275 externalizes the generic base ring and amulet rows at native
  indices 156–160 and the native Arena Potion at index 167, preserving native
  item types, miscellaneous IDs, level gates, values, cursor identities, drop
  rates, and usability.

- Schema 1.276 corrects the native compatibility projections for all normalized
  fixed and spell-backed scroll rows, retaining their actual native enum indices
  and removing the duplicate healing-scroll alias.

- Schema 1.277 externalizes the four native dynamic spell-book templates at
  indices 114–117, preserving level gates, drop weights, misc identity, and the
  blue-book cursor projection. Schema 1.278 completes dynamic spell assignment
  and book-cost mutation through the validated server-owned generator described
  below.

- Schema 1.279 adds validated display names to every directly castable spell
  row. Memorized, charged-item, and consumable-item projections now use the
  server-owned spell catalog name, falling back to spell-book metadata where
  applicable.

- Schema 1.280 externalizes the native Gargoyle adjacent-attack admission pair
  into validated monster-type content. Checked-in Hellfire Gargoyle rows carry
  the native values; older packs retain the compatibility fallback.

- Schema 1.281 externalizes the native Gargoyle avoidance-movement admission
  pair and makes the validated Gargoyle identity authoritative when its other
  tuning rows are present, while preserving older-pack fallback behavior.

- Schema 1.282 externalizes Snake's native six-step lateral drift pattern in
  the validated monster-missile mapping, retaining the native sequence when
  older mappings omit the optional column.

- Schema 1.283 externalizes Resurrection's ordered eight-cell initial
  placement probes in validated spell content, retaining the native probe
  order when older mappings omit the optional column.

- Schema 1.284 externalizes Charged Bolt's ordered 16-entry steering pattern
  in validated spell content, retaining the native sequence when older
  mappings omit the optional column.

- Schema 1.285 externalizes Fireball's ordered nine-cell impact offsets in
  validated spell content, retaining the native impact order when older
  mappings omit the optional column.

- Schema 1.286 externalizes Elemental's ordered nine-cell burst offsets in
  validated spell content, retaining the native burst order when older
  mappings omit the optional column.

- Schema 1.287 externalizes Flash's ordered bottom and top layer offsets in
  validated spell content, retaining the native layer orders when older
  mappings omit the optional columns.

- Schema 1.288 externalizes Guardian's ordered 48-cell firing arc in validated
  spell content, retaining the native firing order when older mappings omit
  the optional column.

- Schema 1.300 externalizes Snake's distant-delay admission chance through
  optional `SnakeCharge` missile fields, retaining the native
  35-minus-two-times-intelligence fallback for older mappings.

- Schema 1.301 externalizes Counselor's failed-ranged-admission movement
  chance through optional `counselor_move_chance_percent` monster-type data,
  retaining the native 30-percent fallback for older mappings.

- Schema 1.302 externalizes Mega's delegated Skeleton delay-admission chance
  through optional monster-type fields, retaining the native
  35-minus-four-times-intelligence fallback for older mappings.

- Schema 1.303 externalizes Mega's intermediate-distance movement admission
  through optional monster-type fields, retaining the native
  50-plus-ten-times-intelligence fallback and 100 percent cap for older
  mappings.

- Schema 1.304 externalizes SkeletonKing's RoundWalk admission modulo through
  optional monster-type data, retaining the native modulo-four fallback for
  older mappings.

- Schema 1.305 externalizes Hork Demon's far-distance RoundWalk admission
  modulo through optional monster-type data, retaining the native modulo-four
  fallback for older mappings.

- Schema 1.306 externalizes the shared AiAvoidance Move-goal distance and
  modulo gate through optional monster-type data, retaining the native
  minimum-four/modulo-four fallback for older mappings.

- Schema 1.307 externalizes Rhino's far-distance movement-goal distance and
  modulo gate through optional monster-type data, retaining the native
  minimum-five/modulo-four fallback for older mappings.

- Schema 1.308 externalizes AiRangedAvoidance's far-distance Move-goal modulo
  through optional missile data, retaining the native four-way/Acid eight-way
  fallback for older mappings.

- Schema 1.309 externalizes AiRangedAvoidance's ordinary RandomWalk movement
  band through optional missile data, retaining the native 50/10/30 fallback
  for older mappings.

- Schema 1.310 externalizes SkeletonRanged's fresh-mode movement bonus through
  optional monster-type data, retaining the native 50-percent fallback for
  older mappings.

- Schema 1.311 externalizes Snake's adjacent attack-admission chance through
  optional `SnakeCharge` missile data, retaining the native
  20-plus-intelligence fallback for older mappings.

- Schema 1.312 externalizes Bat's long-idle and fresh-mode movement bands
  through optional monster-type data, retaining the native 13/1/50 fallback
  for older mappings.

- Schema 1.313 externalizes Counselor's ranged-admission chance through
  optional monster-type data, retaining the native capped 50/5 fallback for
  older mappings.

- Schema 1.314 externalizes the shared AiAvoidance fresh-mode movement bonus
  through optional monster-type data, retaining the native 50-point fallback
  for older mappings.

- Schema 1.315 externalizes the Fat/Overlord fresh-mode movement bonus through
  optional monster-type data, retaining the native 50-point fallback for older
  mappings.

- Schema 1.316 externalizes Rhino's ordinary movement bands through optional
  monster-type data, retaining the native 33/2/50 fallback for older mappings.

- Schema 1.317 externalizes Familiar Lightning's native random-damage upper
  bound through optional missile data, retaining the native `GenerateRnd(10)`
  fallback for older mappings.

- Schema 1.318 externalizes AiRangedAvoidance's adjacent attack-admission base
  and intelligence scale through optional missile data, retaining the native
  6,000/1,000 fallback for older mappings.

- Schema 1.319 externalizes AiRangedAvoidance's adjacent attack-admission
  divisor through optional missile data, retaining the native Acid half-band
  for older mappings.

- Schema 1.320 externalizes Familiar's post-melee missile identity through an
  optional `post_melee_missile` field, retaining the legacy Lightning-label
  fallback for older mappings.

- Schema 1.321 externalizes mapped Bat and Snake charge projectile damage
  type through the existing optional `damage_type` field, retaining the
  native Physical fallback for older mappings.

- Schema 1.322 externalizes mapped Bat and Snake charge projectile spell
  identity through the existing optional `spell_id`/`spell_symbol` fields,
  retaining native spell identity `1` for older mappings.
- Schema 1.323 adds optional `procedural_levels.tsv` definitions. Activation
  materializes deterministic connected geometry from each row's generation seed
  combined with the server world seed, composes generated levels with static
  levels, and rejects invalid or duplicate definitions, including non-Boolean
  default-start and multiplayer-death-drop flags. Full native map synthesis and
  richer authored geometry remain follow-up work.
- Schema 1.324 adds optional `missile_blocked_percent` procedural-level
  metadata. Generated missile occlusion is deterministic and always includes
  movement-blocked cells, while omitted values retain the movement-density
  fallback.

- Schema 1.326 adds optional native towner turn-in metadata to
  `quests.tsv` and `quest_states.tsv`, including the accepting towner, required
  item, native `_qvar2` gate, unique reward, positions, and dialogue message.
  The original shipped use was the Hellfire Blacksmith Anvil turn-in; the same
  schema now also carries Little Girl and Jersey's Jersey.

- Schema 1.327 adds optional normal-item turn-in rewards to the same quest
  tables. The server validates the item catalog and generates the authored
  reward at its native level and position; Hellfire Little Girl consumes
  Theodore and creates the level-13 amulet through the shared turn-in command.

- Schema 1.328 adds optional unique-monster dialogue hand-ins to the same quest
  tables. The server validates the required item and authored relative reward,
  then consumes, completes, spawns, and emits completion dialogue atomically.
  Hellfire Lachdanan consumes Golden Elixir and spawns Veil of Steel one tile
  south of the encounter. Normal-item dialogue rewards now use the validated
  spell-book catalog when the authored item is a dynamic book template, so
  generated spell identity and read-book state remain server-owned.

- Schema 1.329 adds an optional one-time dialogue activation reward. Unlike a
  hand-in, the server generates the authored reward during the first valid
  conversation, keeps the quest active, and suppresses duplicate rewards on
  later conversations. Base Zhar now uses this boundary for its level-14
  dynamic spell-book reward one tile southeast of the encounter.

- Schema 1.330 adds the optional `turn_in_activation_only` towner mode. The
  server can now activate an inactive authored quest and create its initial
  normal item without completing the quest; Hellfire Farmer's Orchard uses it
  for the native Rune Bomb conversation before its existing Hive completion.

- Schema 1.331 adds the optional `turn_in_on_completion` towner stage and its
  authored `_qvar2` marker/reward metadata. Hellfire Farmer's Orchard uses it
  after Hive completion to generate Auric and emit the native completion
  dialogue through the server boundary.

- Schema 1.332 adds optional alternate-item towner interactions. The server
  consumes an authored alternate item and emits its dialogue without changing
  quest progress; Hellfire Jersey's Jersey uses this for the native Grey Suit
  tease before its Brown Suit hand-in.

- Schema 1.333 adds the activation-only item and `_qvar1` marker fields. Base
  Cain now consumes Staff of Lazarus and starts quest 3016 through the same
  `TurnInQuestRequested` boundary.

- Schema 1.334 adds the base Magic Rock item effect and completes the authored
  Griswold turn-in coverage. The staged `_qvar2` prompt now runs before item
  validation, then the server consumes Magic Rock and creates Empyrean Band at
  the native reward position.

- Schema 1.335 adds the base Ogden's Sign staged towner row and normalized
  Tavern Sign/Harlequin Crest content through `TurnInQuestRequested`.

- Schema 1.336 adds the base Anvil of Fury pickup effect, staged Griswold
  turn-in, normalized Anvil item, and Griswold's Edge reward content.

- Schema 1.337 adds the base Butcher activation, normalized The Butcher
  monster, and authored Dead Guy quest state.

- Schema 1.338 adds the base Skeleton King route quest state and normalized
  native Skeleton King monster definition. Schema 1.339 adds the authored
  route activation `_qvar1` marker used by the native Skeleton King level.

- Schema 1.340 adds completion-stage `_qvar1` gates and markers. Lazarus's
  Storyteller transition now uses those fields and logs the Diablo follow-on.

- Schema 1.341 adds non-consuming, item-gated `turn_in_dialogue_only` stages.
  Hellfire Grave Matters uses the new stage for Gillian's Cathedral Map
  conversation.

- Schema 1.342 adds variable-1-only completion-stage markers. Base Poisoned
  Water uses the new fields for Pepin's native completion conversation and
  Ring of Truth reward.

- Schema 1.343 adds active staged item turn-ins with `_qvar1` gates and
  progress-only markers. Base Black Mushroom now externalizes Pepin's Brain
  hand-in, Spectral Elixir reward, and native `_qvar1 = 7` transition while
  leaving the quest active for Adria's later conversation.

- Schema 1.344 adds normalized base Optic Amulet and Arkaine's Valor item and
  unique definitions. Their pickups now complete the authored Halls of the
  Blind (3009) and Valor (3010) quests through the shared item quest-effect
  catalog; Hellfire's existing Warlord mapping remains an overlay.

- Schema 1.345 adds an authored subsequent-dialogue `_qvar1` transition.
  Warlord's second conversation now advances from `QS_WARLORD_TALKING` to
  `QS_WARLORD_ATTACKING` through the shared unique-monster dialogue boundary.

- Schema 1.346 adds repeatable content-owned quest/towner interaction rows.
  Base Black Mushroom now externalizes Adria's Fungal Tome activation, mushroom
  hand-in, brain dialogue, and Spectral Elixir completion stages.

- Schema 1.348 adds server-owned per-player `visited_level_ids` and optional
  OR-gated quest/towner stages. Base King Leoric activation now matches native
  `_pLvlVisited[2] || _pLvlVisited[4]` behavior through the shared protocol.

- Schema 1.350 adds the Hellfire Grave Matters/Barmaid dialogue-only turn-in's
  server-owned `_pLvlVisited[21]` suppression, projected to native and Godot
  clients through `QuestSnapshot.turn_in_blocked_visited_level`.

- Schema 1.351 adds `require_unlogged` to authored quest/towner stages. Base
  Poisoned Water now externalizes Pepin's initial Caves/Poisoned Water
  visitation gate and the native active-but-unlogged fallback through the same
  server-owned state and protocol projection.

- Schema 1.349 moves the base Magic Rock and Anvil activation conversations
  into authored quest/towner rows, including their native visited-level OR-gates
  and variable-2 markers; the existing item hand-ins and unique rewards remain
  validated quest-catalog transitions.

- The Godot protocol client now validates and selects the first adjacent
  content-owned quest/towner stage with matching quest variables and item
  identity, and its HUD submits the corresponding server intent.

- Hellfire Magic Rock now uses the authored Griswold turn-in definition. The
  server owns its native first-conversation variable gate, required-item
  consumption, and Empyrean Band unique reward.

- Schema 1.278 adds validated metadata for all 23 native bookable spells and a
  deterministic server-owned generator for spell names, book costs, intelligence
  requirements, school-specific cursors, and eligible external templates.
  Random selection now rolls over the validated active native spell-table span
  and walks native spell IDs cyclically before mapping to stable IDs; explicit
  spell references remain direct. Reading/learning is server-owned through
  `ReadBookRequested`; single-player generation excludes native Resurrect and
  Heal Other while multiplayer generation retains both. Store rows plus
  static/procedural monster drops and quest-completion rewards now invoke the
  same game-mode-aware generator and retain their supplied native book-template
  identity.

- Schema 1.250 adds the stable `warp` namespace with optional `warp_symbol`
  static-route links, preserving numeric compatibility.

- Schema 1.249 completes the stable `portal` namespace with optional
  `portal_symbol` static-route links, preserving numeric compatibility.

- Schema 1.248 adds the stable `store` namespace and optional store symbols for
  vendor stock and Adria's service identity, preserving numeric compatibility.

- Schema 1.247 adds the stable `object` namespace and optional `object_symbol`
  world-object links, preserving numeric-only compatibility.

- Schema 1.246 adds the stable `set_level` namespace and optional
  `set_level_symbol` quest-type links, preserving native `SL_NONE` ID zero and
  numeric-only compatibility.

- Schema 1.240 adds the stable `unique` namespace and `unique_symbol` to
  shipped `unique_items.tsv` rows. Numeric-only unique definitions remain
  compatible for older external packs.

- Schema 1.239 adds optional `effect_item_symbol` to `objects.tsv` for
  `QuestItemCompletion` rows while preserving numeric effect amounts for all
  legacy and non-item effects.

- Schema 1.238 completes stable base-item symbols for shipped
  `unique_items.tsv` rows. Numeric-only external packs remain compatible while
  checked-in base and Hellfire rows are symbolically linked.

- Schema 1.237 completes stable symbols for every shipped normalized item
  definition. `items.tsv` retains optional-symbol compatibility for older
  third-party packs, while the checked-in base and Hellfire definitions and
  their `content_ids.tsv` entries are complete and one-to-one.

- Schema 1.236 extends optional item symbols to `item_combinations.tsv`,
  monster drop rows, and `combat.tsv`'s PvP-ear item reference.

- Schema 1.230 adds optional `spell_symbol` to direct spell rows in `stores.tsv`;
  rows resolve stable spell IDs and reject numeric/symbol mismatches while
  older rows retain numeric fallback.

- Schema 1.229 adds optional `jester_spell_symbols` to Jester rows in
  `spells.tsv`; dispatch entries resolve stable spell IDs and reject
  numeric/symbol mismatches while older rows retain numeric fallback.

- Schema 1.228 adds optional `spell_symbol` to `store_services.tsv`; spell-staff
  pricing rows resolve stable spell IDs and reject numeric/symbol mismatches
  while older rows retain numeric fallback.

- Schema 1.227 adds optional `spell_symbol` to `spells.tsv`; active rows resolve
  stable spell IDs and reject numeric/symbol mismatches while older rows retain
  numeric fallback.

- Schema 1.226 adds optional `spell_symbol` to `items.tsv`; active rows resolve
  stable spell IDs and reject numeric/symbol mismatches while older rows retain
  numeric fallback.

- Schema 1.225 adds optional `spell_symbol` to `monster_missiles.tsv`; active
  rows resolve stable spell IDs and reject numeric/symbol mismatches while older
  rows retain numeric fallback.

- Schema 1.224 adds optional class-loadout spell symbols for class abilities and
  starting memorized spells; activated packs cross-check those symbols against
  numeric compatibility IDs, while older loadout schemas retain their fallback.

- Schema 1.223 adds optional Town Portal destination coordinates and slot
  layout to `world_rules.tsv`; packs without those columns retain spell-row
  destination tuning.

- The native single-player Diablo defeat rank is now server-owned and crosses
  snapshots, protocol field 34, version-1 saves, and canonical state hashes;
  multiplayer completion does not mutate it. Native Normal/Nightmare/Hell
  difficulty is now selected by authoritative start configuration and
  persisted in snapshots, protocol field 15, version-1 saves, and hashes;
  the rank records difficulty + 1. The authoritative combat module now derives
  native Nightmare/Hell monster level (+15/+30), armor (+50/+80), attack
  accuracy (+85/+120), physical damage scaling, regeneration, and experience
  scaling (2/4 × base-plus-1000, with unique rewards doubled) at runtime
  without mutating persisted base monster data. Initial materialization now
  also applies native Nightmare/Hell 3x/4x health formulas with the
  Diablo/Hellfire single-player/multiplayer bonuses; effective Q6 health is
  persisted in snapshots while validated base monster definitions remain
  unchanged. Hell-specific resistance data is now externalized through the
  validated monster-type columns and selected only at Hell materialization.

- Native Diablo defeat now also sets a server-owned terminal victory flag,
  persists it through the transport-neutral snapshot and version-1 save
  boundary, includes it in canonical hashes, emits a typed protocol event, and
  rejects later player intents or autonomous gameplay ticks; cinematic
  playback and broader post-victory presentation remain client work.

- Schema 1.201 adds optional persistent-projectile player magic-hit tuning to
  `spells.tsv`; shared player-hit rolls retain native 2/5/95 character-level
  penalty and chance bounds when omitted.

- Schema 1.200 adds optional persistent-projectile magic-hit tuning to
  `spells.tsv`; shared Fire Wall, Fireball, Elemental, Nova, Apocalypse, and
  Lightning-segment monster hits retain native 2/5/95 defaults when omitted.

- Schema 1.199 adds optional Bone Spirit magic-hit and current-life damage
  tuning to `spells.tsv`; omitted values retain the native 2/5/95 hit terms
  and one-third current-life damage rule.

- Schema 1.198 adds optional Firebolt magic-hit tuning to `spells.tsv`;
  omitted values retain the native 2/5/95 monster-level penalty and
  hit-chance bounds.

- Schema 1.197 adds optional Holy Bolt magic-hit tuning to `spells.tsv`;
  omitted values retain the native 2/5/95 monster-level penalty and
  hit-chance bounds.

- Schema 1.196 adds optional Charged Bolt magic-hit tuning to `spells.tsv`;
  omitted values retain the native 2/5/95 monster-level penalty and
  hit-chance bounds.

- Schema 1.195 adds optional Town Portal destination slot metadata to
  `spells.tsv`; omitted values retain the native 57/40 destination origin,
  four slots, and two-tile spacing.

- Schema 1.194 adds optional `reflect_damage_percent_minimum` and
  `reflect_damage_percent_maximum` metadata to the Reflect row in `spells.tsv`;
  omitted values retain the native 20–30% reflected-damage roll.

- Schema 1.183 adds optional `critical_damage_multiplier` metadata to
  `classes.tsv`. Active class Critical Strike now uses the validated multiplier;
  omitted values retain the native 2× behavior for compatibility callers.

- Schema 1.182 adds optional `triple_demon_damage_multiplier` metadata to
  `combat.tsv`. Active Triple Demon Damage now uses the validated multiplier;
  omitted values retain the native 3× behavior for compatibility callers.

- Monster missile `spell_id` mappings are now authoritative for the Succubus
  Blood Star path and are cross-validated against the active spell catalog;
  older rows without a mapping retain the native inference fallback.

- Schema 1.145 externalizes special consumable spell behavior through
  `spells.tsv`; the checked-in Rune of Fire row declares `RuneFire`, while
  legacy spell tables retain miscellaneous-ID fallback.

- Schema 1.144 externalizes normalized item spell-source classification through
  `items.tsv`; generated state and protocol matching use the validated source
  kind and spell identity, including charged `SPELL` powers applied by unique
  items and magic affixes, while legacy item tables retain native-ID fallback.

- Cast-source resolution now consults explicit source metadata from the active
  item catalog when a legacy state lacks the projected field; native
  miscellaneous-ID inference remains the final compatibility fallback.

- Schema 1.143 externalizes semantic class-skill roles through validated
  `class_loadouts.tsv` content and carries them through authoritative snapshots
  and protocol projections, so Godot service presentation no longer infers
  Repair, Trap Disarm, Recharge, Identify, Search, or Rage from numeric IDs.

- Authoritative item-service and trap-disarm admission now checks the semantic
  class-skill role first, allowing content packs to assign different spell
  identities without changing gameplay authorization; legacy snapshots that
  lack a role retain their configured spell-ID fallback.

- Godot no longer keeps a numeric table for remote-player spell targeting. It
  supplies visible player identity plus the clicked tile, while the server
  resolves targetability from the active external spell behavior and preserves
  tile targeting for non-player spells.

- Item-backed spell-source kind is now projected by the server in
  `ItemStateSnapshot`; Godot no longer identifies staffs, scrolls, or runes by
  native miscellaneous-ID ranges.

- Schema 1.142 externalizes native Hellfire class-armor adjustments through
  validated `combat.tsv` content; older combat definitions retain the 40/20
  demon/undead compatibility values.

- Schema 1.141 externalizes native post-hit equipment resource restoration
  through validated `combat.tsv` content; older combat definitions retain the
  8/3/5/3/5 random divisor and life/mana compatibility values.

- Schema 1.140 externalizes native monster accuracy formula tuning through
  validated `combat.tsv` content; older combat definitions retain the 2/30/2
  level-scale, base-bonus, and ranged-distance compatibility values.

- Schema 1.139 externalizes native monster hit-chance floors through validated
  `combat.tsv` content; older combat definitions retain the 15/10 ordinary and
  20/25/30 level-specific compatibility values.

- Schema 1.138 externalizes the Warrior Repair ability's durability-loss
  denominator offset through validated `classes.tsv` content; older class
  definitions retain the native offset compatibility behavior.

- Schema 1.137 externalizes Charged Bolt's initial path random range through
  validated `spells.tsv` content; older definitions retain the native 1–15
  compatibility range.

- Schema 1.136 externalizes Berserk's magic-resistance admission frequency
  through validated `spells.tsv` content; older definitions retain the native
  1-in-2 compatibility roll.

- Schema 1.135 externalizes Reflect's no-explicit-level fallback through
  validated `spells.tsv` content; older definitions retain the native level-2
  compatibility value.

- Schema 1.134 externalizes Rage's minimum-life admission threshold through
  validated `spells.tsv` content; older definitions retain the native
  character-level 64-Q6 compatibility threshold.

- Schema 1.133 externalizes the Mana spell's three native restoration ranges
  through validated `spells.tsv` content; older definitions retain the native
  10/4/6 compatibility values.

- Schema 1.132 externalizes Gargoyle wounded-healing admission through
  validated `monster_types.tsv` rows; the active catalog feeds the native
  health threshold while older targets retain the 50-percent compatibility
  value.

- Bat post-hit retreat now uses the validated duration to identify its native
  initial retreat step, so external overlays can safely tune the duration
  without changing the deterministic phase order.

- Schema 1.131 externalizes Scavenger wounded-healing admission through
  validated `monster_types.tsv` rows; the active catalog feeds the native
  health threshold while older targets retain the 50-percent compatibility
  value.

- Schema 1.130 externalizes GoatMelee wounded-special admission through
  validated `monster_types.tsv` rows; the active catalog feeds the native
  health threshold and deterministic random-group gate while older targets
  retain the 50-percent/two-group compatibility values.

- Schema 1.129 externalizes Bat post-hit retreat duration through validated
  `monster_types.tsv` rows; the active catalog feeds server-owned retreat state
  while older targets retain the native compatibility duration.

- Schema 1.128 externalizes Scavenger corpse-eating duration, search radius,
  variant healing, and base-game target-health tuning through validated
  `monster_types.tsv` rows; the active catalog feeds server-owned healing while
  older targets retain the native compatibility values.

- Schema 1.127 externalizes the Gargoyle self-heal divisor multiplier,
  random-range modulus, and random offset through validated `monster_types.tsv`
  rows; the active catalog feeds server-owned healing while older targets retain
  the native compatibility formula.

- Schema 1.126 activates validated missile projectile speed/lifetime fields for
  Snake and Bat mapped-charge controllers; older rows retain the native
  18-speed, 256-tick compatibility trajectory.

- Schema 1.125 externalizes Zombie’s random-direction prepend chance base and
  intelligence scale through validated `monster_types.tsv` rows; older targets
  retain the native compatibility values.

- Schema 1.124 externalizes Fat/Overlord's adjacent attack and special-band
  chance bases and intelligence scales through validated `monster_types.tsv`
  rows; older targets retain the native compatibility values.

- Schema 1.123 externalizes GoatMelee's adjacent attack chance base and
  intelligence scale through validated `monster_types.tsv` rows; older targets
  retain the native compatibility values.

- Schema 1.122 externalizes SkeletonMelee's adjacent attack chance base and
  intelligence scale through validated `monster_types.tsv` rows; older targets
  retain the native compatibility values.

- Schema 1.121 externalizes Mega's monster-Inferno maximum distance, adjacent
  and ranged intelligence bands, far-distance offset, and adjacent choice
  modulo through validated `monster_missiles.tsv` content; older mappings retain
  native compatibility values.
- The native GoatRanged Arrow path now consumes schema 1.120's validated
  ranged-retreat distance and chance fields, keeping its dedicated dispatch
  aligned with the shared server-owned close-range boundary.

- Schema 1.120 externalizes generic `Ranged` close-range retreat distance and
  chance tuning through validated `monster_missiles.tsv` rows; the active
  catalog feeds server-owned AI, including the dedicated Succubus-family Blood
  Star path, while older mappings retain native values.

- Schema 1.119 externalizes generic `RangedAvoidance` distance and chance
  tuning through validated `monster_missiles.tsv` rows; the active catalog feeds
  server-owned AI while older mappings retain native compatibility values.

- Schema 1.118 extends validated `monster_missiles.tsv` charge-distance tuning
  to Snake `SnakeCharge` rows; the active catalog feeds server-owned AI while
  older mappings retain the native compatibility distance.

- Schema 1.117 externalizes Bat-family mapped-charge admission tuning through
  validated `monster_missiles.tsv` rows; the active catalog feeds server-owned
  AI while older mappings retain the native compatibility values.

- Schema 1.116 externalizes Rhino clear-lane charge admission tuning through
  validated `monster_types.tsv` rows; the active catalog feeds server-owned AI
  while older targets retain the native compatibility values.

- Schema 1.115 externalizes SkeletonRanged retreat and clear-lane arrow
  admission tuning through validated `monster_types.tsv` rows; the active
  catalog feeds server-owned AI while older targets retain the native
  compatibility values.

- Schema 1.114 externalizes the Gargoyle wounded retreat/self-heal safety
  boundary through validated `monster_types.tsv` rows; the active catalog feeds
  server-owned AI while older targets retain the native compatibility values.

- Schema 1.113 externalizes Hork Demon adjacent attack, Hork Spawn, and
  non-adjacent movement admission tuning through validated `monster_types.tsv`
  rows; the active catalog feeds server-owned AI while older targets retain the
  native compatibility values.

- Schema 1.112 externalizes Skeleton King summon-admission tuning through
  validated `monster_types.tsv` rows; the active catalog feeds server-owned AI
  while older targets retain the native compatibility values.

- Schema 1.111 externalizes Zombie long-range movement-distance tuning through
  validated `monster_types.tsv` rows; the active catalog feeds server-owned AI
  while older targets retain the native compatibility values.

- Schema 1.110 externalizes Zombie autonomous action-band tuning through
  validated `monster_types.tsv` rows; the active catalog feeds server-owned AI
  while older targets retain the native compatibility values.

- Schema 1.109 externalizes Fat/Overlord non-adjacent movement-band tuning
  through validated `monster_types.tsv` rows; the active catalog feeds
  server-owned AI while older targets retain the native compatibility values.

- Schema 1.108 externalizes GoatMelee non-adjacent movement-band tuning
  through validated `monster_types.tsv` rows; the active catalog feeds
  server-owned AI while older targets retain the native compatibility values.

- Schema 1.107 externalizes SkeletonMelee movement-delay tuning through
  validated `monster_types.tsv` rows; the active catalog feeds server-owned AI
  while older targets retain the native compatibility values.

- Schema 1.106 externalizes Bat adjacent attack-band tuning through validated
  `monster_types.tsv` rows; the active catalog feeds server-owned combat while
  older targets retain the native compatibility values.

- Schema 1.105 externalizes Sneak visibility-band and hit-retreat tuning
  through validated `monster_types.tsv` rows; the active catalog feeds
  server-owned AI while older targets retain the native compatibility values.

- Schema 1.104 externalizes Fallen fear radius and countdown tuning through
  validated `monster_types.tsv` rows; the active catalog feeds server-owned
  combat while older targets retain the native compatibility values.

- Schema 1.103 externalizes the native Monk unarmed minimum and maximum
  damage level divisors through validated class content, with compatibility
  defaults for older class schemas.

- Schema 1.102 externalizes native equipped-damage formula selection through
  validated class content and removes symbol checks from physical damage and
  Monk armor resolution. Legacy class tables retain their formula mapping at
  the loader boundary.

- Schema 1.101 externalizes two native class traits through validated
  `class_flags`: Monk staff fast-block timing and Barbarian hit-stun threshold
  scaling. Combat no longer selects those rules by class symbol, and older
  class overlays retain their previous behavior through compatibility defaults.

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
  adjacent cell, including candidates selected by a complete behavior tuple
  under an alternate validated label; clients receive ordinary authoritative
  monster snapshots and never execute the summon AI.
- Native SkeletonKing non-adjacent movement now preserves LeoricAi's deterministic
  RoundWalk/RandomWalk candidate order after summon admission, before bounded
  navigation fallback.
- Native HorkDemon non-adjacent movement now preserves its far-distance
  RoundWalk/RandomWalk candidate order after Hork Spawn admission, before
  bounded navigation fallback.
- Native MegaAi now delegates to the server-owned SkeletonAi movement boundary
  at or beyond Inferno's maximum distance, preserving its native delay band
  before bounded navigation fallback.
- Native Bat adjacent attacks now use the `4 * intelligence + 8` admission
  band, then retain the native two-step retreat direction and countdown through
  snapshots, saves, protocol, and canonical hashes.
- Native Butcher pursuit now has an explicit server-owned AI boundary: the
  monster uses deterministic native `RandomWalk` candidates until adjacent,
  then uses the shared authoritative melee path.
- Native SkeletonMelee movement now uses the server-owned `35 - 4 * intelligence`
  admission band, delaying or using deterministic `RandomWalk` candidates
  before shared melee.
- Native Fallen normal-state movement and adjacent attack now use the shared
  SkeletonAi `35 - 4 * intelligence` and `20 + 2 * intelligence` bands;
  server-owned fear movement remains the higher-priority state.
- Native SkeletonRanged now has a server-owned four-tile retreat boundary and
  ranged-only idle path; generic melee is no longer admitted for validated bows.
- Native GoatMelee non-adjacent movement now uses a server-owned
  `28 + 2 * intelligence` avoidance admission before shared melee resolution;
  distance-four-or-greater admissions retain the native one-in-four
  RoundWalk branch through deterministic lateral movement.
- Native Fat/Overlord non-adjacent movement now uses a server-owned
  `20 + 4 * intelligence` admission and deterministic `RandomWalk` candidate
  order before shared melee resolution.
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
- Usable, identified equipment with native `TripleDemonDamage` now applies the
  validated combat multiplier only against externally classified demons; the
  demon capability persists through snapshots, saves, hashes, and protocol
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
  usable equipment `PlusGetHit`, and applies the validated final physical-hit
  multiplier in Q6; Mana Shield, death handling, and emitted damage events
  remain server-owned.
- Hellfire `Doppelganger` now consumes its validated post-hit proc chance,
  excludes unique and Diablo targets, and spawns a full-health non-unique copy
  on the deterministic last available adjacent cell; the copy is
  snapshot-owned.
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
  validated class flag adds character level divided by its validated divisor
  (legacy rows default to four) after dexterity and usable equipment armor are
  applied, across creation, transfers, and restore.
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
  SkeletonRanged `Arrow` rows explicitly select the server-owned `MonsterArrow`
  behavior, while older `Targeted` rows remain compatible. Clients never
  dispatch these mappings.
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
- Schema 1.91 externalizes the mapped monster projectile `spell_id` through
  `monster_missiles.tsv`; checked-in ranged rows now supply their authoritative
  spell identity, while legacy overlays retain the prior inference fallback.
- Schema 1.92 externalizes the Hork Spawn `spawn_monster_id` through
  `monster_missiles.tsv`; the Hellfire row supplies stable monster ID 119
  (native selector 117), and current activation rejects Hork Spawn mappings
  that omit the projection.
- Schema 1.93 externalizes mapped missile `projectile_speed` and
  `projectile_lifetime`; migrated ranged rows now supply their authoritative
  movement/lifetime tuning, including Hork Spawn's native speed-8, nine-tick
  trajectory, while legacy rows retain server defaults.
- Schema 1.94 externalizes Acid's `projectile_lifetime_base` and
  `projectile_lifetime_intelligence_scale`; the shipped Acid mappings now own
  the native intelligence-scaled lifetime, while legacy rows retain the server
  formula fallback.
- Schema 1.95 externalizes Acid puddle duration through
  `puddle_duration_random_range`, `puddle_duration_base`, and
  `puddle_duration_intelligence_scale`; the shipped mappings now own the
  native puddle lifetime, while legacy rows retain the compatibility formula.
- Schema 1.96 externalizes Acid puddle damage through
  `puddle_damage_minimum_monster_level`, `puddle_damage_low`, and
  `puddle_damage_high`; shipped mappings now own the native threshold and
  values, while legacy rows retain compatibility defaults.
- Schema 1.97 externalizes mapped monster-missile `damage_type`; shipped
  damaging rows now declare their authoritative channel, while legacy rows
  retain spell/name inference during migration.
- Schema 1.98 externalizes the Mana spell class multiplier through
  `mana_spell_percent`; shipped classes now own Sorcerer 200-percent and
  Rogue/Bard 150-percent scaling, while legacy class rows default to 100
  percent.
- Schema 1.99 externalizes the Hellfire melee armor reduction through
  `melee_armor_reduction_divisor`; shipped classes now own the Barbarian
  divisor of 8, while legacy class rows retain the disabled-rule default.
- Schema 1.100 externalizes Jester's ten-way spell dispatch through
  `jester_spell_ids`; the shipped row owns the native table, while legacy rows
  retain the compatibility dispatch table.
- The native `FireBat` `AiRanged` boundary now consumes its validated Firebolt
  mapping and resolves server-owned projectile travel plus native
  monster-to-player magic hit, resistance, block, and Q6 damage rules.
- Native Familiar now consumes its monster-specific validated `Bat` Lightning
  mapping: the server preserves Bat movement at range and emits the native
  `GenerateRnd(10) + 1` lightning damage after an admitted adjacent hit.
  Projectile travel, lightning resistance, hit admission, and persistence stay
  server-owned; clients receive only the resulting `MonsterMagic` projection.
- Gharbad's post-dialogue combat now accepts the native `Gharbad` identity and
  delegates to the server-owned `AiAvoidance` boundary, including the native
  23 + 2 * intelligence adjacent-attack band and wounded special branch.
  Unique encounter placement and later multi-step dialogue remain incremental.
- Warlord's post-dialogue combat now accepts the native `Warlord` identity and
  delegates to the server-owned `SkeletonAi` boundary, preserving its native
  adjacent attack and non-adjacent delay/movement bands. Later dialogue-driven
  encounter state remains incremental.
- SnotSpil's post-banner combat now accepts the native `Snotspill` identity and
  delegates to the server-owned `FallenAi`/`SkeletonAi` boundary, including
  native Fallen fear state and combat timing. Banner quest activation and map
  transition behavior remain incremental.
- Zhar and Archbishop Lazarus now accept their native `Zhar` and `Lazarus`
  identities, use validated Fireball mappings, and delegate normal combat to
  the server-owned `CounselorAi` boundary. Later dialogue-driven quest and map
  transitions remain incremental.
- The base pack now supplies additive `quest_states.tsv` rows for the native
  Gharbad, Zhar, Warlord, Lazarus, and Diablo quest states. Server-owned
  completion uses exact unique identities, applies content-owned terminal
  native variables, activates Lazarus's declared follow-on Diablo quest, and
  completes Diablo through an explicit validated boss identity; encounter
  placement, dialogue, and rewards remain incremental.
- A shared `TalkToQuest` intent now validates a living adjacent unique target
  against the quest's configured completion identity, activates/logs the quest,
  applies content-owned minimum native dialogue variables monotonically, and
  emits a typed server-owned dialogue event with the content message ID;
  localized text, unique encounter placement, and later dialogue steps remain
  incremental.
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
- The shared replay gate now checks the three-arrow `MultipleArrows` volley,
  including spread positions and activation delays in both runners.
- Initialized ranged replay now applies the authored Rogue bow damage profile,
  including class attribute scaling and deterministic range selection.
- Initialized ranged replay now applies class hit admission and target armor
  reduction at the shared native/C# checkpoint.
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
  `level_generation.tsv` metadata. Activation checks that every declared world
  level is covered, and the catalog owns deterministic encounter-count
  calculation used by the server-owned spawn planner. The optional
  `procedural_levels.tsv` table now supplies an initial deterministic connected
  layout path; full native map synthesis remains a separate migration.
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
- Unique-item `DUR_CURSE` now uses the native percentage reduction and clamps
  both current and maximum durability to the native minimum of one.
- Unique-item `CHARGES` now multiplies maximum charges using the native power
  parameter while retaining zero current charges for depleted items.
- Native Hellfire `CRYSTALLINE` item powers now add their fixed-plus-scaled
  damage and apply the native percentage durability reduction in both affixes
  and unique-item definitions.
- The Hellfire pack now ships the native `Crystalline` weapon prefix through
  an explicit validated `item_prefixes.tsv` overlay.
- The same overlay now ships the distinct Hellfire `Jester's` weapon prefix;
  duplicate-name legacy variants remain intentionally unmerged.
- The overlay also ships the distinct Hellfire `Doppelganger's` weapon/staff
  prefix; duplicate-name legacy variants remain intentionally unmerged.
- The Hellfire overlay now replaces the native `piercing` (`1–1`),
  `puncturing` (`2–2`), and `bashing` (`3–3`) target-accuracy suffix ranges
  with their Hellfire values while retaining the shared validated suffix
  schema.
- Hellfire's staff-capable legacy affix eligibility rows now replace their base
  counterparts through deterministic composite-key overlays, avoiding duplicate
  candidates during authoritative generation.
- The Hellfire overlay now also ships the native `devastation`, `decay`, and
  `peril` weapon/staff/bow suffixes with their authoritative effect mappings.
- The Hellfire overlay now ships the previously missing native `The Grizzly`
  unique item with its five validated powers and cursor identity.
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
- Server-owned adjacent melee and line-of-sight ranged player targets now use
  the native shield/class block boundary at the authoritative attack effect
  tick and delayed `WeaponArrow` impact; block state, target life, and live
  protocol projection remain server-owned.
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
- An end-to-end TCP restart fixture now writes a session save, disposes the
  first host, starts a fresh host and adapter over the same save root, and
  restores the saved participant through its resume token while ignoring a new
  character selection.
- Native and Godot protocol clients plus shared replay and live-server tests.
- A loopback reconnect soak now keeps a second participant connected while the
  first participant resumes through twelve consecutive cycles, preserving the
  resume token, entity identity, complete participant set, self-first ordering,
  and snapshot hash on every cycle.
- The live Godot protocol client now reconnects through eight v0.2 cycles
  against the launched authoritative server while a second client remains
  connected, preserving the resume token and self-first participant ordering.
- The generic host adapter now has a 64-generation replacement soak that
  migrates two sessions through every candidate and retires each prior target
  only after publication, preserving session state at every boundary. The live
  TCP host now keeps one connected session operating across 32 replacement
  generations and two connected sessions operating across eight generations;
  both paths resume with the final published ruleset identity.
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
  adapter. Default startup rejects ambiguity; an optional `--game-adapter`
  factory ID selects one named compatible adapter and fails closed for unknown
  or incompatible IDs. Only the built-in Diablo bridge resolves Diablo
  capabilities or constructs the protocol executor; executable `Program`
  contains no Diablo symbols, enforced by tests.
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
provide gameplay modules rather than host adapters, and live ruleset replacement
  now exposes transactional candidate preparation, a host operation barrier,
  save-based session migration, handshake identity publication, generation
  leases, and explicit retired-target disposal. A 32-generation single-session
  and eight-generation two-session end-to-end TCP soak now proves active-session
  migration, continued operation on every replacement, retired-target disposal,
  and resume under the final published identity; the existing 64-generation
  adapter loop and live 32/8-generation TCP loops cover the current
  retirement-pressure boundary, while broader production-like multi-session
  pressure and authoritative content/gameplay coverage remain partial. Static
  content
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
adapter retains remote participant projections for targeting and constructs
presentation-only current-level sprites with authoritative action timing,
automap arrows, party-panel portraits, and party-panel entity routing;
interpolation and legacy interaction parity remain separate migrations.
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
RNG. Remaining spell scaling and other class modifiers and targeting edge cases
remain migration work. Godot now presents the complete authoritative spell
source list, preserves source-key selection across snapshots, and allows direct
selection alongside Q/E cycling; the six cursor-driven class capabilities are
now server-owned and covered by protocol tests.

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
the optional
`native_spell_id` column. `spell_id` remains the authoritative
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

Persistent Acid puddles now share the managed active-tick player collision
boundary in compact native replay, including the 40% hit roll, usable
equipment effects, acid resistance, Mana Shield absorption, and lifecycle
checkpoints.

Persistent Acid puddles now also destroy an occupied breakable missile blocker
in compact native replay while preserving the managed impact-phase lifetime.

The shared Acid puddle boundary now covers multiplayer secondary participants,
including usable GetHit and Acid resistance before Q6 damage on active ticks.

MonsterMagic player impacts now include the validated Barbarian natural-
resistance class contribution before Mana Shield in the compact native replay,
matching the external class catalog used by the managed server.

Hellfire Monk MonsterMagic impacts now use the validated FlexibleBlock and base
block chance rules for unarmed primary-player targets in compact native replay,
matching the managed class catalog and preserving the shared replay checkpoints.

The same MonsterMagic boundary now covers a usable one-handed Hellfire Monk
weapon. Replay equipment carries the native equip-location classification, so
managed and native runners agree that one-handed weapons may block while
two-handed weapons remain excluded.

The paired two-handed Monk fixture now verifies the negative boundary as well:
the same classification reaches the managed and native runners, which retain
the ordinary MonsterMagic hit and GotHit timing instead of admitting a block.

Compact native replay now mirrors the complete validated class base-block
chance table instead of treating non-Monk classes as zero. A low-Dexterity
Warrior MonsterMagic fixture exercises the 30-percent base chance and shares
its block checkpoint with the managed class catalog.

Managed MonsterMagic replay setup now retains the authored mana maximum, Mana
Shield state, memorized spells, spell catalog, and equipment equip-location.
The in-process xUnit runner executes the complete MonsterMagic fixture family
against the same native checkpoints; the legacy VSTest testhost path remains
an optional wrapper rather than the only managed verification route.

The same class-aware block boundary now applies to unarmed Hellfire Monk
secondary participants, while retaining the explicit legacy block-capable
secondary path used by existing shield fixtures.

Hork Spawn player impacts now reuse the same class-aware FlexibleBlock and base
block chance boundary for primary and secondary participants, with a shared
Hellfire Monk primary fixture covering the previously shield-only native path.
Its replay setup now also supplies the validated class catalog and fixture
Dexterity to the managed runner, keeping the class decision data-driven.

The compact native replay now applies the same secondary-player class-aware
FlexibleBlock/base-chance boundary across Bone Spirit, Charged Bolt, and
Fireball player impacts; the Hellfire Monk Bone Spirit fixture covers the
unarmed branch and its variant RNG consumption.

Seeded native MonsterMelee impacts now use the same secondary-player
FlexibleBlock/base-chance boundary, with an unarmed Hellfire Monk fixture
covering class admission while preserving the monster AI seed transition.

## Near-term priorities

1. Close the remaining native-parity combat and AI edge cases, including class
   modifiers, scaling, targeting, and timing that are not yet covered by replay
   fixtures.
2. Advance native-parity dungeon generation with authoritative geometry,
   collision definitions, set-piece composition, and encounter/object placement;
   unavailable licensed assets remain deferred rather than being fabricated.
3. Complete external content coverage for the remaining Hellfire quest, world,
   and environmental behavior, remaining spell definitions or edge behavior,
   item metadata, and native object mappings.
4. Grow the Godot client from a protocol demonstration into a complete playable
   presentation client using production assets.
5. Add the missing replay, reconnect, multiplayer, and long-running soak cases.
6. Keep the current ruleset replacement and safe-retirement pressure coverage
   green, extend it to broader production-like multi-session soaks, and
   generalize adapter selection when a second production adapter exists.
7. Replace the remaining Lua capability destinations and remove Lua only after
   every removal gate is satisfied.

MonsterMagic projectiles now retain validated content missile identities through simulation snapshots, save/restore, and protocol projection. Native presentation maps the seven supported families exactly; unknown identities remain fail-closed.


Godot presentation now consumes the additive native MonsterMagic visual identities, rendering distinct Lightning, flare, and Magma Ball cues while retaining the generic behavior fallback for older snapshots.

Snapshot restore now validates content-owned MonsterMagic missile identities against the active external monster-missile catalog and rejects unknown or misplaced identities.

The protocol mapper now rejects unknown or misplaced content-owned MonsterMagic identities instead of silently degrading them.

Godot snapshot validation now explicitly rejects native projectile visual IDs outside the additive 0–35 protocol range, matching native projection.

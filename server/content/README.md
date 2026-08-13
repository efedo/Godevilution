# Authoritative Content Packs

Each directory under `server/content` is a server-owned, validated gameplay
content pack. The default `base` pack is loaded by the standalone server and is
included in its content manifest and combined ruleset identity.
It declares its identity and `devilution.diablo@1.6.0` schema in
`base/content-pack.json`; the standalone host falls back to legacy command-line
identity only for external roots that do not yet provide a manifest.
The checked-in `hellfire` pack depends on `base` and overlays the validated
`combat.tsv` variant selector, so operators can activate the native Hellfire
ruleset with `--content-overlay server/content/hellfire` while remaining
Hellfire tables are migrated incrementally.

Generic TSV parsing, ordered pack loading, deterministic overlays, transactional
composition, manifest hashing, and stable IDs are owned by the
engine-independent `Devilution.Data` assembly. Diablo-specific table schemas
and deterministic catalog rules are owned by `Devilution.Diablo`; the server
only composes the selected content and module.

The normalized TSV files define stable IDs and authoritative rules for player
classes, animation timing, initial loadouts, levels, items, affixes, stores and
services, spells, combat, monsters, portals, objects, and quests. Legacy `itemdat.tsv` and
`unique_itemdat.tsv` are compatibility fallbacks; normalized `items.tsv` and
`unique_items.tsv` take precedence so stable IDs are not replaced by source-row
positions.

`classes.tsv` defines the native-compatible class IDs `0` through `5`, their
base-game or Hellfire availability, exactly one active default, class flags,
base and maximum attributes, combat modifiers, and life/mana fixed-point
scalars. `spell_mana_reduction_percent` and its Hellfire counterpart externalize
the native class adjustment applied before each spell's minimum mana cost.
`self_healing_spell_percent` externalizes the native Warrior/Monk/Barbarian
double and Rogue/Bard half-again self-Healing modifiers.
`heal_other_spell_percent` separately externalizes Heal Other's class modifier;
the Monk uses 300 percent for another player while retaining 200 percent for
self-Healing.
`pvp_ear_cursor_graphic` supplies the native inventory graphic used when that
class dies to another player. Activation bounds it to the native byte-sized
cursor range.
Fixed-point values use Q6 units, so one whole point is represented as 64.
An overlay may select a different default class, but the composed table is
accepted only when it still contains exactly one default and satisfies the
complete class schema.

`class_animations.tsv` is keyed by `class_id` and mirrors every native
class-animation timing: unarmed, shield, sword, bow, axe, mace, and staff attack
frame/action-frame pairs plus idle, walking, blocking, death, casting, recovery,
and town frames. Attack action frames retain the native one-based convention;
`casting_action_frame` retains the native zero-based frame index. Activation
requires exactly one valid row for every active class, bounds values to native
storage and walking-table limits, and rejects action frames outside their
animation. Block, hit-recovery, walking, weapon-profile attack, and casting
lifecycles consume this catalog in activated server sessions. Lethal damage also
uses `death_frames` at two server ticks per frame, cancels the current action,
and retains Death mode after the timed action clears. In-progress actions cross
module snapshots, protocol field 25, version-1 saves, conditional hashes, and
native/Godot projection. The shared player-action replay verifies deferred
walk, melee, and cast boundaries. Shared fast-Bow timelines derive ranged mode
and speed flags from hashed equipment and prove the base-game versus Hellfire
frame difference; a shared death timeline cross-checks the base animation row;
focused module tests retain broader speed-tier coverage.

The `FlexibleBlock` class flag externalizes the Monk's native alternative block
equipment rules. In addition to an ordinary usable shield, that class may block
with a usable staff, while unarmed, or while holding exactly one usable
one-handed weapon. Activation rejects unknown class flags.

`combat.tsv` declares the shared deterministic combat constants, the native
`Diablo` or `Hellfire` game variant, the global `friendly_fire` switch, and the
restart-in-town level, position, whole-life amount, Q6 scale, and NewLevel
duration. It also declares the player-death gold divisor, maximum projected
stack, native compatibility item type/index, and radius-50 drop search.
`pvp_ear_item_id` selects the normalized item definition used for a player-kill
ear; activation requires that definition to project native `IDI_EAR` index 23
and miscellaneous ID 44. Activation also requires a walkable restart
destination and validates all death-drop bounds. For canonical
inventory-backed Gold, the Diablo module applies the
native two-pass stack-removal order, preserves whole-stack seeds, generates a
new seed for a partial drop, and divides the cached balance independently of
the remaining physical stack total. The base maximum stack is 5000. Aggregate
wallet-only states retain the earlier compatibility fallback. The restart
rules restore 64 Q6 life, set mana to zero in simulation, and hold the
transition for one tick. With
`friendly_fire=0`, a player in friendly mode does
not collide with another player; explicit hostile mode opts that caster into
PvP. With `friendly_fire=1`, player collision is globally active, matching the
native override. Friendly/hostile mode remains authoritative player state and
is not a client-side filter.

The checked-in Hellfire overlay replaces the base combat row by its stable
`base_attack_damage` key and selects `game_variant=Hellfire`. Variant selection
therefore remains external validated content rather than executable startup or
client state. It also adds native-derived level identities 3, 4, 9, and 17
through 24 at the native 40-by-40 dungeon bounds, so level references can cross
the same composed-world validation boundary. These rows intentionally provide
only the current normalized geometry metadata; blocked-cell generation and the
remaining Hellfire quest behavior/state, monster, and item catalogs remain
separate migrations.
The initial overlay also adds a native-derived Hellfire encounter set
(`monster_id` values `2` through `7`) to prove that encounter rows cross the
same composition and activation boundary; the complete Hellfire encounter
population is still pending. Schema 0.63 adds the validated `monster_types.tsv`
catalog for the base encounter and all one hundred thirty-eight native Hellfire types,
including stable/native IDs, baseline health, damage, level, and immutable
capability flags. Schema 0.67 adds native fire, lightning, and magic resistance
percentages; encounter rows inherit those values unless they provide an
explicit per-instance override. Encounter rows now reject unknown type
identities when the table is present. Baseline armor, damage channels, monster
level, and immutable capability flags also inherit from the type when omitted
from an encounter row. Schema 0.68 adds native acid resistance as the
`acid_resistance` percentage, preserving the native acid immunity of the four
Hellfire acid creatures without silently mapping it to another channel.
Native monster IDs are variant-local compatibility metadata; reverse lookup is
available only for unambiguous IDs in a composed catalog, without making the
engine parse TSV.
It also adds the normalized Undead Crown base item and unique definition
(`unique_id=1003`); unique overlays use `(unique_id, power)` because each power
is a separate normalized row. The Empyrean Band (`unique_id=1004`) and its
ring base item are also externalized with native attribute, light, recovery,
and trap-resistance powers; the remaining Hellfire item catalog is still
incremental.
The Optic Amulet (`unique_id=1005`) and Ring of Truth (`unique_id=1006`) now
cross the same boundary with their native base-item identities and unique
light, resistance, get-hit, attribute, and life powers. Their normalized
native item indexes remain `11` and `12` respectively.
Harlequin Crest (`unique_id=1007`), Veil of Steel (`unique_id=1008`), and
Arkaine's Valor (`unique_id=1009`) extend that slice with native helm and armor
identities and their authoritative armor, resistance, attribute, life, mana,
and recovery powers. Griswold's Edge (`unique_id=1010`) adds its native
one-handed sword base item and fire-damage, hit, attack-speed, knockback, mana,
and life-curse powers. Bovine Plate (`unique_id=1011`) adds its native
heavy-armor base item and armor, indestructibility, light, resistance,
mana-curse, and spell-level powers. The Rift Bow (`unique_id=1012`) reuses the
validated Short Bow base item with native arrow-velocity, damage, and
dexterity-curse powers. The Butcher's Cleaver (`unique_id=1013`) adds its
native two-handed axe base item with strength, fixed-damage, and durability
powers. The Needler (`unique_id=1014`) reuses the validated Short Bow base item
with native hit, fixed-damage, and attack-speed powers. The Celestial Bow
(`unique_id=1015`) adds its native long-bow base item with minimum-strength,
damage, and armor powers. Deadly Hunter (`unique_id=1016`) adds its native
composite-bow base item with triple-damage, hit, and magic-curse powers. Bow of
the Dead (`unique_id=1017`) reuses the validated Composite Bow base item with
native hit, attribute, curse, and durability powers. The Blackoak Bow
(`unique_id=1018`) reuses the validated Long Bow base item with native
attribute, damage, and light/vitality-curse powers. Flamedart
(`unique_id=1019`) adds its native Hunter's Bow base item with fire-arrow, hit,
and fire-resistance powers. Fleshstinger (`unique_id=1020`) reuses the
validated Long Bow base item with native dexterity, hit, damage, and durability
powers. Windforce (`unique_id=1021`) adds its native War Bow base item with
strength, damage, and knockback powers.
Eaglehorn (`unique_id=1022`) adds its native Battle Bow base item with
dexterity, hit, damage, and indestructibility powers.
Gonnagal's Dirk (`unique_id=1023`) adds its native Dagger base item with
dexterity-curse, damage-modifier, attack-speed, and fire-resistance powers.
The Defender (`unique_id=1024`) adds its native Sabre base item with armor,
vitality, and hit-curse powers.
Gryphon's Claw (`unique_id=1025`) adds its native Falchion base item with
damage, magic-curse, and dexterity-curse powers.
Black Razor (`unique_id=1026`) reuses the validated Dagger base item with
damage, vitality, and durability powers.
Gibbous Moon (`unique_id=1027`) adds its native Broad Sword base item with
attribute, damage, mana, and light-curse powers.
Ice Shank (`unique_id=1028`) adds its native Long Sword base item with
fire-resistance, durability, and strength powers.
The Executioner's Blade (`unique_id=1029`) reuses the validated Falchion base
item with damage, life-curse, light-curse, and durability powers.
The Bonesaw (`unique_id=1030`) adds its native Claymore base item with
damage-modifier, strength, magic/dexterity-curse, life, and mana-curse powers.
Shadowhawk (`unique_id=1031`) adds its native Broad Sword base item with
light-curse, life-steal, to-hit, and all-resistance powers.
Wizardspike (`unique_id=1032`) reuses the validated Dagger base item with
magic, mana, to-hit, and all-resistance powers.
Lightsabre (`unique_id=1033`) reuses the validated Sabre base item with light,
lightning-damage, to-hit, and lightning-resistance powers.
The Falcon's Talon (`unique_id=1034`) adds its native Scimitar base item with
fast-attack, to-hit, damage-curse, and dexterity powers.
Inferno (`unique_id=1035`) reuses the validated Long Sword base item with fire
damage, light, mana, and fire-resistance powers.
Doombringer (`unique_id=1036`) adds its native Bastard Sword base item with
to-hit, damage, all-attribute-curse, life-curse, and light-curse powers.
The Grandfather (`unique_id=1037`) adds its native Great Sword base item with
one-handed, attribute, to-hit, damage, and life powers.
The Mangler (`unique_id=1038`) adds its native Large Axe base item with damage,
dexterity-curse, magic-curse, and mana-curse powers.
Sharp Beak (`unique_id=1039`) reuses the validated Large Axe base item with
life, magic-curse, and mana-curse powers.
BloodSlayer (`unique_id=1040`) adds its native Broad Axe base item with damage,
demon damage, all-attribute-curse, and spell-level-curse powers.
The Celestial Axe (`unique_id=1041`) adds its native Battle Axe base item with
no-strength requirement, to-hit, life, and strength-curse powers.
Wicked Axe (`unique_id=1042`) reuses the validated Large Axe base item with
to-hit, dexterity, vitality-curse, hit-curse, and indestructibility powers.
Stonecleaver (`unique_id=1043`) reuses the validated Broad Axe base item with
life, to-hit, damage, and lightning-resistance powers.
Aguinara's Hatchet (`unique_id=1044`) adds its native Small Axe base item with
spell-level, magic, and magic-resistance powers.
Hellslayer (`unique_id=1045`) reuses the validated Battle Axe base item with
strength, vitality, damage, life, and mana-curse powers.
Messerschmidt's Reaver (`unique_id=1046`) adds its native Great Axe base item
with damage, damage-modifier, attribute, life-curse, and fire-damage powers.
Crackrust (`unique_id=1047`) adds its native Mace base item with attributes,
indestructibility, all-resistance, damage, and spell-level powers.
Schema 0.64 adds the validated `quest_types.tsv` catalog for all base and
Hellfire native quest IDs. Quest-state rows must resolve a declared stable
quest identity; native IDs, routing levels, set-level sources, and
single-player availability remain external metadata.

Schema 0.65 adds `level_generation.tsv`, which externalizes the native
level-range monster-density divisor, multiplayer monster multiplier, and trap
percentage. The catalog is validated and exposed by Diablo activation; actual
procedural map generation remains a separate migration.

Schema 0.66 adds `quest_pools.tsv` and `set_level_names.tsv`. Quest types now
retain native set-level identities (`SL_*`) rather than an unresolved sentinel;
pool entries are stable quest IDs and are validated against `quest_types.tsv`
during activation.
The module's quest-pool selector preserves native seed ordering and the known
bad-seed compatibility discard. Single-player activation applies those
exclusions to server-owned quest state; excluded quests cannot advance or
activate linked objects, native lifecycle/log flags are retained, and the state
is retained by snapshots and saves.
Initial quest rows activate from their validated native route level when an
authoritative player enters that level; explicit session mode selects the
single-player or multiplayer route, and multiplayer skips quest types marked
`single_player_only`.

The Hellfire Sorcerer loadout replaces the base Charged Bolt staff with the
native Firebolt staff through a keyed `class_loadout_items.tsv` overlay.

`class_loadouts.tsv` starting Gold is materialized as canonical inventory state:
the first pile uses native cell 30, values above the configured maximum split
into additional native-order cells, and each session receives stable nonzero
pile seeds. Those piles remain authoritative through store and death rules.

For normal new-session creation, the Diablo module derives maximum life as
`life_adjustment_q6 + life_per_level_q6 * level + life_per_vitality_q6 * vitality`
and maximum mana with the corresponding mana, level, and magic columns. The
optional host `--starting-life` and `--starting-mana` values are explicit Q6
development overrides. They are not content defaults.

`class_loadouts.tsv` defines one complete starting wallet, stable class-skill
ID, and optional learned spell/level for every active class. Its companion
`class_loadout_items.tsv` gives each starting item a contiguous per-class order,
stable `item_id`, exact `Inventory`, `Equipment`, or `Belt` location, and native
slot or inventory-grid anchor. Activation rejects missing classes, unknown
items, invalid stable spell bounds, unusable equipment, duplicate slots,
two-handed/off-hand conflicts, belt-ineligible items, overlapping inventory
footprints, more than five starting items, and any directly castable starting
memorized, charged-item, scroll, or rune source whose spell is absent from the
activated spell catalog. Native class-skill IDs are not required to be ordinary
spell definitions because several select cursor-driven item or object
interactions such as repair, recharge, or trap disarm. New-session item seeds
are derived deterministically from the session, class, order, and item ID; they
are server-owned and unique within the loadout.

`ability_spell_id` and `starting_spell_id` use the stable authoritative
`spells.tsv` identity, not the legacy native `SpellID` ceiling. This allows a
content pack to expose stable rows such as Etherealize `53` while native
compatibility fields continue to carry their separate `native_spell_id` values.

Schema 0.64 adds the native-compatible `quest_types.tsv` catalog and makes
external quest-state rows reject unknown quest identities. Schema 0.63 adds the
native-compatible `monster_types.tsv` catalog and makes
external encounter rows reject unknown monster type identities. Schema 0.62
adds native-derived Hellfire Harlequin Crest, Veil of Steel, and
Arkaine's Valor item and unique definitions. Schema 0.61 adds the Hellfire
Optic Amulet and Ring of Truth item and unique definitions. Schema 0.60 extends
the optional `consumable_misc_id` spell column. The base
pack maps Rune of Lightning (`48`) to Lightning Wall (`50`), Greater Rune of
Lightning (`49`) to Nova (`18`), Greater Rune of Fire (`50`) to Immolation
(`45`), and Rune of Stone (`51`) to Stone Curse (`8`) without reusing native
IDs as authoritative identities. Rune of Fire (`47`) uses a direct
BigExplosion-style authoritative splash with native level-zero damage and
radius-nine placement crawl. Legacy `itemdat.tsv` parsing preserves all five
rune miscellaneous IDs so fallback packs use the same validated source path.

Schema 0.57 adds Golem death-animation timing to stable spell row `52` (native
`SpellID` `21`). The native 12-frame death sequence is externalized as
`golem_death_animation_ticks`; the server carries its remaining terminal ticks
through snapshots, saves, hashes, and protocol fields 34-35, then removes the
expired minion after the animation.

Schema 0.55 adds stable Golem spell row `52` (native `SpellID` `21`). The
server validates the native targeted summon, replaces only the caster's prior
owned minion, and persists the owner identity and native armor, life, and
damage formulas through snapshots, saves, hashes, and protocol field 33. The
authoritative module also drives deterministic nearest-monster targeting,
movement with blocked-cell path planning and owner-facing idle wandering, melee
damage, experience attribution, and owner/level cleanup.

Schema 0.56 adds stable Etherealize spell row `53` (native `SpellID` `26`).
The server owns its level-scoped status identity, protocol/snapshot/hash
projection, transition cleanup, and monster-damage immunity.

Schema 0.54 adds stable Nova spell row `18` (native `SpellID` `18`). The
server exposes the existing one-tick radial controller and 36 persistent
lightning balls through the validated external row.

Schema 0.53 adds stable Lightning Wall spell row `50` (native `SpellID` `40`).
The server reuses the authoritative wall-growth controller and persistent
segments with native lightning damage, duration, and resistance tuning.

Schema 0.52 adds stable Warp spell row `49` (native `SpellID` `42`). The
server resolves the nearest normalized level trigger, crawls around the native
destination while excluding trigger tiles and occupied cells, and applies the
level transition through the shared player action state.

Schema 0.51 adds stable Berserk spell row `48` (native `SpellID` `44`). The
server owns the radius-five target crawl, eligibility rules, resistance coin
flip, four-channel damage scaling, and persisted berserked state.

Schema 0.50 adds stable Jester spell row `47` (native `SpellID` `39`). The
server owns native ten-way deterministic dispatch into the existing typed spell
paths, so single-player and multiplayer use the same authoritative result.

Schema 0.49 adds stable Immolation spell row `45` (native `SpellID` `41`). The
server owns the moving controller, native 36-projectile radial burst,
spell-level speed cap, and FireballBow projectile impact lifecycle.

Schema 0.44 adds stable Town Portal spell row `39` (native `SpellID` `7`).
The typed behavior performs native five-cell portal placement, replaces the
owner's prior portal, exposes a bidirectional dungeon/town route, and expires
after 100 authoritative ticks. Portal state is server-owned through snapshots,
saves, hashes, and the shared protocol.

Schema 0.43 adds stable Telekinesis spell row `33` (native `SpellID` `33`).
The typed behavior applies the native cursor operation authoritatively to a
visible world item, object, or non-talkative monster; item pickup, object
effects, and one-cell knockback remain server-owned and deterministic.

Schema 0.42 adds the optional `native_spell_id` column to `spells.tsv`.
`spell_id` remains the stable authoritative content identity used by commands,
snapshots, saves, and overlays; `native_spell_id` records the corresponding
Devilution `SpellID` for compatibility imports. The catalog rejects duplicate
native identities and legacy `itemdat.tsv` spell names resolve through this
mapping before cast-source validation. This keeps deliberately different
stable/native pairs explicit, including Guardian `7`/`13`, Holy Bolt `5`/`31`,
Inferno `21`/`20`, Chain Lightning `16`/`14`, Elemental `31`/`29`, Blood Star
`36`/`35`, Bone Spirit `38`/`36`, and Apocalypse `25`/`24`.

Schema 0.26 requires stable `spell:identify` ID `5` for the Bard's declared
class skill. Identify remains a cursor-driven item service rather than a row in
`spells.tsv`: the module resolves that stable identity at activation, validates
it against the live loadout for each request, and identifies an exact owned
equipment or inventory item without a store or Gold cost. Source-zero requests
retain the paid vendor service.

Schema 0.27 requires stable `spell:repair` ID `26` for the Warrior's declared
class skill. Repair is also a cursor-driven item service rather than a
`spells.tsv` row. The module validates the live loadout and exact owned item,
then applies native character-level random repair strength and permanent
maximum-durability loss through the shared server RNG. Full items are accepted
without a draw; reaching zero maximum durability removes the item. Source zero
retains paid, full vendor repair.

Schema 0.28 requires stable `spell:recharge` ID `27` for the Sorcerer's declared
class skill and adds `staff_level` to `spells.tsv`. Positive staff levels are
the native divisor for a staff carrying that spell; `-1` marks a spell that is
not available on a staff. The class service validates the live loadout and exact
owned staff, makes one native recharge-strength RNG roll, permanently reduces
maximum charges before restoration, and accepts a full staff without a draw.
Source zero retains paid, full vendor recharge.

Schema 0.29 adds Holy Bolt behavior and the optional `undead` monster
capability. The behavior requires one moving physical-damage missile and uses the
external capability to reject non-undead monster collisions.

Schema 0.30 adds the stable `spell:trap_disarm` identity `28` for the Rogue's
cursor-driven Trap Disarm class capability. Armed object state is validated from
`objects.tsv`, server-owned, persisted, hashed through an additive extension,
and cleared only by an admitted authoritative Disarm Trap intent.

Schema 0.31 adds stable spell row (`36`) for the native Hellfire Blood Star
(`SpellID` `35`) and its typed
`BloodStar` projectile behavior. The server preserves the native fixed magic
damage formula, quarter magic resistance, 16-tile speed, 256-tick lifetime,
and five-point physical self-damage cost as validated content tuning.

Schema 0.32 adds stable spell row (`38`) for the native Bone Spirit
(`SpellID` `36`) and its typed
`BoneSpirit` projectile behavior. The server preserves the native magic
homing projectile, one-third-current-life damage, quarter magic resistance,
seven-tick impact phase, and six-point physical self-damage cost.

Schema 0.33 adds stable spell row (`6`) for native Fire Wall (`SpellID` `6`)
and its typed controller
and segment behaviors. The controller grows a center segment and perpendicular
branches for seven ticks, stopping each branch independently when blocked. Each
segment persists for `(10 + 10 * effective spell level + character level) * 16`
ticks, rolls `GenerateRnd(10) + GenerateRnd(10) + 2 + character level`, scales
the result by eight, and applies shifted fire damage on every authoritative tick
with quarter resistance for monsters. Controller branches, segment duration,
damage, and hit directions are persisted and hashed so restored sessions resume
deterministically.

Schema 0.34 adds stable spell row (`15`) for native Flame Wave (`SpellID` `15`)
and its typed controller
and segment behaviors. The one-tick controller places a center segment and
perpendicular branches using the native `(effective spell level / 2) + 2`
branch count. Each moving segment rolls `GenerateRnd(10) + character level + 1`,
travels at speed `16` for `255` ticks in the caster's facing direction, applies
fire collision and quarter resistance, and stops at missile-blocking geometry.

Schema 0.35 adds stable spell row (`21`) for native Inferno (`SpellID` `20`)
and its typed controller
and segment behaviors. The controller travels at speed `32`, emits no more than
three stationary segments, and increments each segment lifetime by one tick.
Each segment rolls the native character-level and two-value damage formula and
repeats fire collision until expiry.

Schema 0.36 adds stable spell row (`16`) for native Chain Lightning
(`SpellID` `14`) and its typed
fan-out controller and bolt behaviors. The controller emits a target bolt plus
the native crawl-radius monster bolts; each bolt reuses the Lightning movement,
damage, segment lifetime, and collision rules with the chain spell's source
level retained in its authoritative state.

Schema 0.40 adds stable spell row (`31`) for native Elemental (`SpellID` `29`)
and its typed
retargeting projectile behavior. The projectile preserves the native first
destination, retargets once to the nearest eligible monster, then applies its
14-tick nine-cell fire burst with native line-of-sight and resistance rules.

Schema 0.41 adds stable spell row (`7`) for native Guardian (`SpellID` `13`)
and its typed turret
controller behavior. The server finds a valid five-tile spawn, persists the
native duration and firing cadence, scans the six-tile guardian arc in native
order, and emits Firebolt-compatible projectiles with deterministic RNG and
line-of-sight rules.

Schema 0.39 adds stable spell row (`22`) for native Hellfire Rage (`SpellID` `22`)
and its typed
active/cooldown controller behavior. The controller preserves the native
character-level lifetime formula, rejects recasts while active or cooling down,
and applies its Q6 self-damage at cooldown expiry without allowing the cast to
kill the player. Phase and remaining lifetime persist through snapshots and the
existing projectile protocol fields.

Schema 0.38 adds stable spell row (`25`) for native Apocalypse (`SpellID` `24`)
and its typed row-major
controller and boom behaviors. The controller scans the native eight-tile square
in stable row-major order, excludes player minions and blocked cells, applies
Hellfire missile line-of-sight filtering, and persists its cursor and boom
first-hit state through snapshots and the protocol.

Schema 0.37 adds stable spell row (`18`) for native Nova (`SpellID` `18`) and
its typed one-tick radial
controller and ball behaviors. The controller preserves the native nine-point
quarter-radius order and four mirrored emissions (36 balls total). Each ball
travels at speed `16` for `255` ticks, uses the Lightning damage type and
quarter-resistance rule, and can pass through a hit target while continuing to
its endpoint. Ball damage is generated once as
`ScaleSpellEffect((GenerateRndSum(6, 5) + character level + 5) / 2,
effective spell level) * 64`.

Normalized `items.tsv` includes a stable content `item_id` and an explicit
`native_item_index` used only for compatibility projection. `charges` and
`max_charges` preserve charged-item state. The content ID remains the key for
catalog references and overlays; native row/index values must not become
authoritative identities. The base pack assigns stable ID `24` to the native
ear definition. Its victim-derived name and cursor are runtime state carried by
the generic item snapshot rather than localized catalog text.

Spell casting also interprets native compatibility item metadata. A charged
cast must name the matching staff or unique item equipped in native left-hand
slot `4`; an accepted cast decrements `charges`. A consumable cast must name the
matching scroll or rune in inventory or belt; an accepted cast removes that
exact item. The shipped Sorcerer staff therefore uses native miscellaneous type
`23` (staff). These compatibility values are validated and consumed by the
Diablo module rather than trusted from a client.

`monsters.tsv` keeps encounter state and immutable gameplay capabilities outside
the engine. Its optional `stone_curse_immune`, `undead`, `player_minion`,
`unique`, `diablo`, and `berserk_immune` columns accept `0` or `1`; immune
monsters are skipped by Stone Curse's native crawl, while undead
monsters are the only monster targets accepted by Holy Bolt. These flags persist
in snapshots and saves so a restored session cannot change future targeting
rules. Berserk also accepts optional `min_damage`, `max_damage`,
`min_damage_special`, and `max_damage_special` channels. Its schema `0.51.0`
row owns the radius-five crawl and 120% plus one-to-nine percent scaling; the
server persists the active state and all channels.

`warps.tsv` is the normalized level-trigger projection used by Warp. Each row
binds a stable trigger to a source level and an authored destination level and
tile. Activation validates both levels and all coordinates; runtime selection
is nearest-trigger first, then stable warp ID, with the native five-cell
destination crawl applied by the server.

The current base `spells.tsv` implements spell IDs `1` through `6`, Stone Curse
spell ID `8`, Infravision spell ID `9`, Phasing spell ID `10`, Mana Shield spell
ID `11`, Fireball spell ID `12`, Teleport spell ID `23`, the Sorcerer's
charged-staff spell ID `30`, Resurrection spell ID `32`, and Heal Other spell
ID `34`, Fire Wall spell ID `6`, Flame Wave spell ID `15`, Inferno spell ID `21`, Chain Lightning spell ID `16`, Nova spell ID `18`, Apocalypse spell ID `25`, Rage spell ID `22`, Elemental spell ID `31`, Hellfire Blood Star spell ID `36`, Bone Spirit spell ID `38`, plus the
Monk's Search ability spell ID `46`, Telekinesis spell ID `33`, Berserk spell
ID `48`, Warp spell ID `49`, Lightning Wall spell ID `50`, and Golem spell ID
`52`.
`projectile_count` supplies the base
count, and `projectile_count_level_divisor` adds the floor of effective spell
level divided by that value. `projectile_behavior` selects a typed module-owned
transition; its behavior-specific damage, resistance, impact, speed, and
lifetime values remain external data rather than host policy.

Spell `1` uses `Firebolt` behavior. Its mana columns reproduce the native
post-first-level adjustment, Firebolt-specific adjustment divisor, minimum,
class/variant reduction, and Q6 resource scale. Damage is generated once at
creation as `GenerateRnd(10) + current magic / 8 + effective spell level + 1`.
The server then advances the bolt at native speed `26` through 16.16 isometric
space, checks every swept tile, performs the native magic hit roll, applies
one-quarter damage to a fire-resistant monster, and retains a one-tick impact
phase. Those runtime fields and RNG state cross snapshots, saves, hashes, and
the shared native/C# replay.

Spell `5` uses `HolyBolt` behavior. Its external row reproduces the native
mana, speed growth, and damage `GenerateRnd(10) + character level + 9`.
Collision uses the native magic hit roll but accepts only monsters marked
`undead=1`; non-undead monsters are passed through without consuming a hit
or damage event. The explicit capability crosses authoritative snapshots,
saves, protocol field 23, conditional hashes, and future replay state.

Hellfire spell `36` uses `BloodStar` behavior. Its external row preserves
the native damage `current magic / 2 + 3 * effective spell level - current
magic / 8`, fixed magic damage type, quarter positive magic resistance,
speed `16`, and lifetime `256`. Casting also applies five whole points of
physical self-damage through the same server-owned Mana Shield and death-drop
path as other player damage. The projectile and self-damage event remain
authoritative while the legacy native client retains its compatibility path.

Spell `38` uses `BoneSpirit` behavior. Its projectile travels at native speed
`16` for `256` ticks, retargets the nearest monster within the native radius
after reaching its initial destination, deals one-third of the victim's current
life before resistance, and remains in a seven-tick impact phase after a hit.
Casting applies six whole points of physical self-damage through the same
server-owned resource and death path.

Spell `6` uses `FireWallController` behavior to place the native seven-tick
controller, which grows a center segment and perpendicular branches. It emits
`FireWallSegment` behavior with native duration and damage tuning; segments
remain stationary and apply shifted fire damage every authoritative tick until
their duration expires. Branch state is server-owned and persisted.

Spell `15` uses `FlameWaveController` behavior to place the native perpendicular
wave. Its runtime `FlameWaveSegment` projectiles move in the caster's facing
direction, resolve fire collision on swept tiles, and retain their movement,
damage, lifetime, and collision state through authoritative snapshots and saves.

Spell `21` uses `InfernoController` behavior to move at native speed toward the
requested tile and emit at most three stationary `InfernoSegment` projectiles.
The segment lifetimes increase by one tick per emitted segment, while each
segment uses the native character-level and two-value fire damage rolls and
repeats collision checks until expiry. Controller and segment state are
server-owned and snapshot/save validated.

Spell `2` uses `Healing` behavior. It rolls once from `0..9`, once from `0..3`
per character level, and once from `0..5` per effective spell level, adding one
to every draw before converting the result to Q6. The active class row then
applies its healing percentage. Casting at maximum life remains valid and still
consumes the native RNG stream and mana; only the applied amount is capped.
Exact healing crosses the additive protocol Q6 field, and a three-cast shared
native/C# replay proves uncapped, capped, and full-life behavior.

Spell `34` uses `HealOther` behavior and the same native mana and randomized
Q6 base-healing columns as Healing. It requires a different living player on
the caster's level within range, resolves the selected stable entity ID before
consuming anything, and applies the caster class's separate Heal Other
percentage. A valid full-life target still consumes mana and the complete RNG
sequence but emits no zero-healing event. Exact applied healing and the updated
target travel through existing events and complete participant snapshots; a
shared timed C++/C# multiplayer replay proves the Monk's 300-percent rule.

Spell `3` uses `Lightning` behavior. One accepted cast creates the native
speed-32 invisible controller, which consumes character-level and two-value
damage draws on every update in an explicit compatibility order. Each tile the
controller reaches receives a stationary segment with Q6 damage
`(GenerateRnd(character level) + GenerateRnd(2) + 2) * 64` and lifetime
`6 + floor(effective spell level / 2)`. Newly created segments participate in
the same authoritative tick, can damage the same occupant repeatedly while
alive, apply the native magic hit roll and one-quarter positive resistance, and
retain the source spell level needed for deterministic resume. A
missile-blocking object stops the controller without being broken and without
receiving a segment. Protocol, saves, hashes, and the shared native/C# replay
carry the complete controller and segment runtime.

Spell `4` uses `Flash` behavior. It ignores client coordinates and creates the
native Bottom and Top layers at the player in that order. Each layer consumes
`character level + 1` independent `GenerateRnd(20)` draws, adds character level
plus one, applies `damage += damage / 8` once per effective spell level, and
then adds half again. The six Bottom offsets and three Top offsets partition the
surrounding 3x3 area. Both layers repeat magic-hit and exact-Q6 collision for 19
ticks, divide positive monster resistance by four, break eligible objects
without expiring, and persist across static missile blockers. Their lifetime
also derives the native invulnerability window: it begins after the first
missile update and ends when the final layer is removed. Layer type, damage,
effective level, lifetime, and RNG state cross protocol snapshots, saves,
canonical hashes, and the shared native/C# replay.

Spell `8` uses `StoneCurse` behavior. At effect time the server searches the
requested tile through radius `stone_curse_search_radius` in native crawl order,
including either occupied tile of a walking monster and skipping content-marked
immune monsters. No valid target is an accepted fizzle that consumes nothing;
an already-petrified target consumes the source without stacking or refreshing.
A new curse freezes the target without discarding its pending movement and
creates a stationary controller for
`min(effective level + stone_curse_base_duration,
stone_curse_maximum_duration) * stone_curse_duration_scale` ticks. Living
expiry restores movement on the next monster phase. Death switches the
controller to the `projectile_impact_lifetime` shatter phase. Controller,
petrification, occupancy, immunity, and source level cross protocol, saves,
hashes, both clients, and the shared native/C# replay.

Spell `9` uses `Infravision` behavior as an immediate self status. Its duration
starts at 1,584 ticks and applies `duration += duration / 8` once per effective
spell level with saturating arithmetic. Recasting replaces the same status ID
with the longer remaining duration. Learned casts apply the content-selected
mana adjustment and class modifier; matching scrolls and runes consume their
exact inventory or belt source without requiring mana. The typed status crosses
snapshots, saves, conditional hashes, both clients, and the shared native/C#
replay. Level-changing portal and restart transitions remove Infravision on the
server while retaining unrelated status effects.

Spell `46` uses `Search` behavior as an immediate self status. Its new schema
columns compute `status_duration + character level *
search_character_level_duration_multiplier + effective spell level *
search_spell_level_duration_multiplier`; the base pack therefore reproduces
`245 + 2 * character level + 10 * effective spell level`. Recasts add the new
duration to the remaining duration unless the native signed-int addition would
reach or exceed `INT_MAX`, in which case the old duration is retained.
Abilities consume no mana. The server owns duration, recasts, expiry, and
level-transition cleanup; typed protocol behavior `2` lets native and Godot
clients highlight items without simulating Search.

Spell `10` uses `Phasing` behavior. It ignores client target coordinates and
enumerates every walkable, unoccupied tile whose absolute X and Y offsets are
both between `phasing_minimum_axis_offset` and
`phasing_maximum_axis_offset`, inclusive, in native row-major order. A cast
with candidates consumes exactly one RNG draw to choose a destination, consumes
its source, and creates an invisible two-tick controller. Its first update
revalidates the chosen tile through the same radius-0-through-5 crawl used by
Teleport and moves the authoritative player; its second removes the controller.
If no initial candidate exists, the accepted cast still consumes its source but
does not advance RNG or create a controller. Phasing never changes facing.
Controller state, RNG, player position, and resources cross snapshots,
compatible saves, protocol projection, both clients, and the shared native/C#
replay.

Spell `11` uses `ManaShield` behavior. Its first valid cast consumes the source
and activates a server-owned player flag; casting again while active is an
accepted fizzle that consumes nothing. Incoming Q6 damage uses the learned base
spell level, capped by `mana_shield_damage_reduction_maximum_level`, to reduce
the native divisor by `mana_shield_damage_reduction_level_step`. Mana absorbs
the adjusted damage first. If mana is insufficient, the server reverses the
reduction on the remainder with native integer truncation, clears the shield,
and applies the result to life. Exact mana depletion also clears the shield.
Usable identified equipment with the native NoMana flag bypasses absorption.
The active flag crosses snapshots, saves, protocol field 29, conditional hashes,
native/Godot presentation, and the shared native/C# replay.

Spell `12` uses `Fireball` behavior. Creation consumes two independent
`GenerateRnd(10)` draws, computes native base damage as twice character level
plus both draws, then adds four and repeats one-eighth growth for each effective
spell level. Speed starts at 16 and gains two per effective level up to a
34-point bonus. The server advances its 16.16 path through every swept tile;
the first direct hit, blocking tile, or range expiry creates the native center-
plus-eight-neighbor splash in fixed order, subject to origin-to-cell solid
geometry. The direct collision and shifted splash retain their distinct native
Q6 and resistance rules. The resulting stationary explosion persists for 14
ticks. Damage, velocity, origin, phase, lifetime, RNG, and exact health cross
snapshots, compatible saves, protocol projection, both clients, and the shared
native/C# replay.

Spell `23` uses `Teleport` behavior. Cast resolution searches the requested
tile and radii `1` through `5` in the exact native `DoCrawl` order, rejecting
static geometry, active monsters, other living players, and active objects.
If no tile is available, the spell fizzles without consuming its source. A
successful cast consumes the source and creates an invisible two-tick
controller. On its first update, after the monster phase, the controller
revalidates the destination with the same crawl and moves the authoritative
player; its second update removes it. The controller and resulting player
position cross snapshots, version-1 saves, protocol projection, and a shared
native/C# replay.

Spell `32` uses `Resurrection` behavior. A cast must target another player on
the caster's active level and within native range. Once admitted, it consumes
the source and creates a stationary 16-tick beam at the target's old position,
even if that target is alive. A dead player returns to Stand with zero mana and
`resurrection_life * resurrection_life_scale` Q6 life, capped by maximum life.
Placement tries the native eight initial offsets before the radius crawl up to
`resurrection_search_radius`, rejecting static blockers, living participants,
monsters, and objects. The fallback crawl also rejects portal exits. The beam
behavior is runtime-owned and cannot be selected directly by content.

Spell `30` uses `ChargedBolt` behavior. One accepted cast consumes its source
once and creates `4 + floor(effective spell level / 2)` independently tracked
bolts. Each bolt uses the server-owned Diablo LCG for its path, animation draw,
and `GenerateRnd(current magic / 4) + 1` damage, then follows the native
16.16-isometric wandering cadence for 256 ticks. Effective spell level combines
the learned level with eligible equipped spell-level additions regardless of
whether the cast source is mana, a staff charge, or a consumable.

Player collisions preserve the native Charged Bolt roll sequence: hit first,
an eligible block roll second, and damage last even when blocked. Spell damage
uses exact Q6 units, lightning resistance is derived from usable identified
equipment plus class rules and capped at 75 percent, and positive resistance
overrides blocking. Only authoritative `Stand` and melee `Attack` modes admit a
block. A successful block immediately enters the class-duration `Block` mode;
an unresisted threshold hit enters class-duration `GotHit`, and item recovery
flags shorten those durations exactly as in native code. Hellfire additionally
consumes the blocked-missile rotation-selection draw before Charged Bolt enters
its common impact phase.

`levels.tsv` keeps movement geometry in `blocked_cells` and native missile
geometry in `missile_blocked_cells`. The latter defaults to `blocked_cells` when
the column is absent, preserving older packs, but an explicitly empty value
means the level has no static missile-blocking cells. Both columns contain
comma-separated row-major cell indexes and are bounds-validated. Use `none`
instead of a blank cell when an on-disk table intentionally declares no blocked
cells.

The optional `default_start` column accepts `0` or `1` and may select exactly
one level for new sessions. It is independent from the restart destination, so
the base pack starts on dungeon level 1 while retaining town level 0 for
restart-in-town.

The optional `suppress_multiplayer_death_drops` column also accepts `0` or `1`.
It marks native level-16 and arena-equivalent maps where multiplayer deaths do
not lose Gold or produce normal equipment/ear consequences. Single-player
death rules are unchanged, and a cursor-held item remains an unconditional
death drop.

`objects.tsv` declares missile behavior independently from movement occupancy.
`missile_passable=1` mirrors a native object whose `_oMissFlag` lets missiles
through. A non-passable object stops a bolt; `breakable=1` additionally makes
that collision set persistent `destroyed` state. A destroyed or activated
object no longer occupies its cell or accepts interaction. Activation rejects a
breakable pass-through object and any destroyed object that is not breakable.

## Table schema reference

The following table is the authoritative ownership and key reference for the
base pack. Column order is part of the validated schema; overlays must retain
the complete target header even when they upsert only one row.

| Table | Stable row key | Module-owned meaning |
| --- | --- | --- |
| `classes.tsv` | `class_id` | Native-compatible class identity, availability, flags, attributes, combat modifiers, and Q6 resource formulas. |
| `class_animations.tsv` | `class_id` | Native action and animation frame timings for each class. |
| `class_loadouts.tsv` | `class_id` | Starting Gold, class-skill identity, and optional learned spell/level. |
| `class_loadout_items.tsv` | `class_id`, `item_order` | Deterministic starting equipment, belt, and inventory placement. |
| `combat.tsv` | singleton | Variant, combat defaults, restart rules, death-gold rules, and PvP-ear identity. |
| `content_ids.tsv` | `kind`, `symbol` | Bidirectional stable symbolic IDs; class capabilities such as `spell:trap_disarm` are declared here. |
| `levels.tsv` | `level_id` | Walkability, missile geometry, default-start selection, and multiplayer death-drop policy. |
| `level_generation.tsv` | `minimum_level`, `maximum_level` | Native level-range monster density, multiplayer scaling, and trap-generation metadata. |
| `warps.tsv` | `warp_id` | Normalized level-trigger source and destination projections for the server-owned Warp spell. |
| `portals.tsv` | `portal_id` | Source and destination level endpoints. |
| `monsters.tsv` | `entity_id` | Encounter state, resistances, drops, and immutable capabilities such as `undead`. |
| `monster_types.tsv` | `monster_id` | Native-compatible monster type identity and validated baseline level, health, damage, resistance, and capability metadata. |
| `objects.tsv` | `entity_id` | World-object placement, effects, linked-quest progress amounts, missile collision, breakage, and optional `trap_armed` state. |
| `quests.tsv` | `quest_id` | Level-scoped progress requirements, native initial/completed state, quest-log state, optional `_qvar1`/`_qvar2`/`_qmsg` values, optional `completion_monster_id`, and optional single-player pool exclusion state. |
| `quest_types.tsv` | `quest_id` | Native-compatible quest identity, routing levels, set-level source, and single-player availability. |
| `quest_pools.tsv` | `pool_id`, `quest_id` | Native single-player quest-randomization pools using stable quest identities. |
| `set_level_names.tsv` | `set_level_id` | Native set-level identities and presentation names, including arena names. |
| `spells.tsv` | `spell_id` | Directly castable spell resources, stable/native identity mapping, typed projectile behavior, scaling, status, and damage rules. |
| `items.tsv` | `item_id` | Normalized item identity, native compatibility metadata, placement, charges, and generation inputs. |
| `item_affixes.tsv` | `name`, `is_suffix` | Normalized prefix/suffix definitions and deterministic affix effects. |
| `item_generation.tsv` | singleton | Item-generation bounds, quality rolls, and native-compatible random selection rules. |
| `unique_items.tsv` | `unique_id` | Normalized unique-item definitions and fixed effects. |
| `stores.tsv` | `store_id`, `store_slot` | Deterministic vendor inventory and item-generation references. |
| `store_services.tsv` | `key`, `spell_id` | Service pricing and spell-staff costs; omitted keys use module defaults. |
| `itemdat.tsv`, `item_prefixes.tsv`, `item_suffixes.tsv` | source-row compatibility | Legacy fallback tables used only when normalized tables are absent. |
| `unique_itemdat.tsv` | source-row compatibility | Legacy unique-item fallback used only when `unique_items.tsv` is absent. |

The Data runtime owns parsing, ordered composition, overlay keys, and manifest
identity. The Diablo module owns the meaning and cross-table validation of these
rows. A pack cannot add an unrecognized column, replace a table implicitly, or
assign a class capability that the active module does not validate.

## Overlay packs

Pass each overlay directory with a repeatable `--content-overlay` argument.
Every overlay directory contains `content-pack.json`, its declared TSV files,
and no implicit table replacements. For example:

```json
{
  "formatVersion": 1,
  "id": "example.balance",
  "version": "1.0.0",
  "dependencies": ["devilution.base"],
  "schema": {
    "id": "devilution.diablo",
      "version": "1.6.0"
  },
  "dependencyConstraints": [
    {
      "id": "devilution.base",
      "minimumVersion": "0.1.0",
      "maximumVersion": "0.1.0"
    }
  ],
  "overlays": [
    {
      "table": "stores.tsv",
      "keys": ["store_id", "store_slot"],
      "mode": "upsert"
    }
  ]
}
```

The server composes the base pack followed by overlay arguments in command-line
order. A dependency must name an earlier pack. Reusing a table path requires an
overlay declaration, key columns must exist, and the overlay must use exactly
the target table's ordered columns. Composite keys identify rows. Existing rows
are replaced in place; new rows append in overlay order. Duplicate keys,
implicit conflicts, missing targets, schema mismatches, and invalid dependencies
reject the complete candidate before it becomes active.

Manifest format 1 accepts an optional versioned schema identity and inclusive
three-component semantic-version constraints for declared dependencies. All
declared schemas in one ordered manifest must match, every constraint must name
a declared dependency, and the loaded dependency version must satisfy its
range. The first-party server rejects a declared schema other than
`devilution.diablo@1.6.0`. Manifests without compatibility metadata retain their
legacy identity and remain loadable during migration.

Useful overlay keys are `class_id` for `classes.tsv`, `class_animations.tsv`,
and `class_loadouts.tsv`, `class_id` plus `item_order` for
`class_loadout_items.tsv`, and `item_id` for normalized `items.tsv`. Class,
loadout, and item overlays are composed first and then validated together; a
cross-table mismatch rejects the candidate without changing active content.

Pack identity, dependency metadata, overlay declarations, and table contents
contribute to the content manifest and combined ruleset hashes. Clients must use
the server's printed ruleset identity, not calculate an identity from an
individual pack.

Changes to authoritative content can change deterministic results. Update or
add loader tests and shared replay fixtures whenever a content change affects
state transitions or canonical hashes. Client presentation data may mirror
layout or labels, but it must not become an independent gameplay definition.

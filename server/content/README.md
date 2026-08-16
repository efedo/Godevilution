# Authoritative Content Packs

Each directory under `server/content` is a server-owned, validated gameplay
content pack. The default `base` pack is loaded by the standalone server and is
included in its content manifest and combined ruleset identity.
It declares its identity and `devilution.diablo@1.231.0` schema in
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
classes, animation timing, initial loadouts, levels, world rules, items, affixes, stores and
services, spells, combat, monsters, portals, objects, and quests. Legacy `itemdat.tsv` and
`unique_itemdat.tsv` are compatibility fallbacks; normalized `items.tsv` and
`unique_items.tsv` take precedence so stable IDs are not replaced by source-row
positions.

`classes.tsv` defines the native-compatible class IDs `0` through `5`, their
base-game or Hellfire availability, exactly one active default, class flags,
base and maximum attributes, combat modifiers, and life/mana fixed-point
scalars. `spell_mana_reduction_percent` and its Hellfire counterpart externalize
the native class adjustment applied before each spell's minimum mana cost.
`critical_damage_multiplier` externalizes the native class Critical Strike
damage multiplier; omitted values retain the native 2× behavior.
`critical_strike_chance_per_level_percent` externalizes the native Critical
Strike chance added per character level; omitted values retain the native 1%
per-level behavior.
`iron_skin_level_divisor` externalizes the native IronSkin armor bonus divisor;
omitted values retain the native divisor of four.
`natural_resistance_per_level_percent` externalizes the native Barbarian
resistance added per character level; omitted values retain the native 100%
per-level contribution. `heavy_hit_threshold_bonus_percent` externalizes the
native HeavyHitThreshold stun threshold bonus; omitted values retain the native
25% bonus.
`pvp_block_level_scale` externalizes the native player-versus-player block
chance level-difference scale; omitted values retain the native scale of two.
`fast_block_frame_skip` externalizes the native FastBlock staff timing value;
omitted values retain the native two-frame compatibility default.
`equipment_fast_block_frame_skip` externalizes the native equipment FastBlock
timing value; omitted values retain the native two-frame compatibility default.
The `fast_hit_recovery_frame_skip`, `faster_hit_recovery_frame_skip`, and
`fastest_hit_recovery_frame_skip` columns externalize the native equipment hit-
recovery tier skips; omitted values retain the ordered native 1/2/3 defaults.
The `quick_attack_frame_skip_with_first_frame` through
`fastest_attack_frame_skip_without_first_frame` columns externalize the native
Quick/Fast/Faster/Fastest melee attack-speed skips for both animation timing
paths; omitted values retain the native 1/0, 2/1, 3/2, and 4/2 defaults.
The Golem spell row's `golem_hit_points_level_multiplier`,
`golem_hit_points_mana_divisor`, `golem_damage_level_multiplier`,
`golem_minimum_damage_level_offset`, `golem_maximum_damage_level_offset`, and
`golem_armor_class` columns externalize its summon health, damage, and armor
formula terms; omitted values retain the native 2/3, 2/4/8, and 25 defaults.
`golem_hit_points_level_base_q6` externalizes the native Q6 health contribution
per effective spell level; omitted values retain the native 320-unit
compatibility default.
`base_melee_to_hit` and `base_ranged_to_hit` are active native physical combat
metadata: initialized player attacks add the class base modifier to level and
dexterity (`dexterity / 2` for melee, full dexterity for ranged) before shared
equipment bonuses and deterministic hit resolution.
That resolution subtracts target armor and clamps to native 5–95%; base Diablo
adds `plus_enemy_armor_class` to to-hit, while Hellfire applies its native
armor-shift/quarter rule and Barbarian melee reduction.
`self_healing_spell_percent` externalizes the native Warrior/Monk/Barbarian
double and Rogue/Bard half-again self-Healing modifiers.
`heal_other_spell_percent` separately externalizes Heal Other's class modifier;
the Monk uses 300 percent for another player while retaining 200 percent for
self-Healing.
`mana_spell_percent` externalizes the native Mana spell multiplier: Sorcerer
uses 200 percent, Rogue/Bard use 150 percent, and the remaining classes use
100 percent. Legacy class rows default to 100 percent during migration.
`melee_armor_reduction_divisor` externalizes the Hellfire Barbarian melee
target-armor reduction divisor; the shipped Barbarian row uses 8 and other
classes use zero. Legacy class rows retain the disabled-rule default.
The five `monk_*_armor_*` columns externalize the native Monk chest-armor
bonuses: light armor, ordinary medium armor, unique medium armor, and unique
heavy armor use their declared level multipliers over the shared divisor.
The `IronSkin` class flag adds the native character-level armor bonus using
`iron_skin_level_divisor` after dexterity and usable equipment armor are
calculated; legacy rows retain the divisor of four. The
`CriticalStrike` flag likewise selects its native combat rule; both behaviors
remain server-owned after content activation.
The `TrapSense` class flag controls recipient-scoped armed-object visibility:
only a TrapSense player receives `trap_armed` in the network projection. The
authoritative world, saves, and internal snapshots retain the complete
trap state, while each projected hash matches its recipient boundary.
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
one-handed weapon. The `FastBlock` flag separately declares the native staff
fast-block timing, with `fast_block_frame_skip` supplying its validated value,
and `HeavyHitThreshold` declares the native Barbarian hit-stun threshold
multiplier. Activation rejects unknown class flags. Legacy class rows without
the new flags retain those native traits through the loader's compatibility
mapping.

The `physical_damage_formula` class column selects the validated native
equipped-damage formula (`Standard`, `Rogue`, `Monk`, `Bard`, or `Barbarian`).
Legacy class tables without this column infer the historical formula at the
loader boundary so the combat engine remains independent of class symbols.
The `monk_unarmed_minimum_damage_level_divisor` and
`monk_unarmed_maximum_damage_level_divisor` columns externalize the native
level divisors used by Monk unarmed base damage; older class schemas retain
the native `2` and `1` defaults.
The optional `repair_ability_durability_level_offset` column externalizes the
Warrior Repair ability's native durability-loss denominator offset; the
shipped Warrior row uses `9`, while older class schemas retain the legacy
offset.

The optional `fallen_fear_radius`, `fallen_fear_level_offset`, and
`fallen_fear_minimum_ticks` monster-type columns externalize the Fallen fear
admission radius and countdown formula. Legacy monster rows retain the native
radius-four, offset-eight, minimum-two compatibility values. A complete positive
tuple selects the behavior regardless of the row's `native_ai` label; rows
without migrated tuning retain the native-name compatibility path. The
persisted fear timer and retreat direction are accepted and advanced for any
validated label carrying the complete tuple.
The optional `sneak_distance_base`, `sneak_distance_intelligence_scale`,
`sneak_retreat_release_distance`, and `sneak_retreat_ticks` columns externalize
the Sneak visibility band and hit-retreat timing. Legacy monster rows retain
the native base-five, intelligence-scale-one, release-three, nine-tick values.
A complete positive tuple selects the behavior regardless of the row's
`native_ai` label; rows without migrated tuning retain the native-name
compatibility path.
Persisted hidden state and retreat ticks are likewise accepted and advanced
for any validated label carrying the complete tuple.
The optional `bat_attack_chance_base` and
`bat_attack_chance_intelligence_scale` columns externalize the Bat adjacent
attack admission band. Legacy monster rows retain the native base-eight,
intelligence-scale-four compatibility values. A complete positive tuple selects
the behavior regardless of the row's `native_ai` label; rows without migrated
tuning retain the native-name compatibility path. The optional
`bat_retreat_duration_ticks` column may be supplied independently; when it is
present without the attack pair, the runtime composes the native 8/4 attack
defaults and applies the configured retreat duration. Persisted retreat ticks
and direction are likewise accepted and advanced for any validated label
carrying migrated Bat tuning.
The optional `goat_melee_special_health_threshold_percent` and
`goat_melee_special_chance_group_period` columns externalize the GoatMelee
wounded-special health threshold and deterministic random-group period.
Legacy monster rows retain the native 50-percent threshold and two-group
compatibility gate. A complete positive special tuple selects the behavior
regardless of the row's `native_ai` label and composes the native 23/2 attack
band when those optional attack columns are omitted. Omitted special values
retain the native 50-percent threshold and two-group compatibility defaults.
The optional `scavenger_healing_health_threshold_percent` column externalizes
the Scavenger wounded-health admission threshold for corpse seeking. Legacy
monster rows retain the native 50-percent compatibility threshold. The
validated threshold is selected independently of the row's `native_ai` label;
when supplied without the healing tuple, the runtime composes the native
10/4/8/1/75 healing defaults.
The optional `mana_random_range`, `mana_character_level_random_range`, and
`mana_spell_level_random_range` columns externalize the three native Mana
restoration rolls. Legacy Mana rows retain the native 10, 4, and 6 ranges.
The optional `rage_minimum_life_q6_per_character_level` column externalizes
Rage's native minimum-life admission threshold. Legacy Rage rows retain the
native 64-Q6-per-character-level compatibility threshold.
The optional `reflect_default_spell_level` column externalizes Reflect's
no-explicit-level charge fallback. Legacy Reflect rows retain native level 2.
The optional `reflect_damage_percent_minimum` and
`reflect_damage_percent_maximum` columns externalize Reflect's inclusive
retaliation-damage roll. Legacy Reflect rows retain the native 20–30 percent
compatibility range.
The optional `town_portal_destination_position_x`,
`town_portal_destination_position_y`, `town_portal_destination_slot_count`,
and `town_portal_destination_slot_spacing` columns externalize the native
Town Portal destination slot layout. Legacy Town Portal rows retain the
57/40 origin, four slots, and two-tile spacing.
The optional `charged_bolt_hit_monster_level_scale`,
`charged_bolt_minimum_hit_chance`, and `charged_bolt_maximum_hit_chance`
columns externalize Charged Bolt's native monster magic-hit roll. Legacy
Charged Bolt rows retain the 2/5/95 penalty and chance bounds.
The optional `holy_bolt_hit_monster_level_scale`,
`holy_bolt_minimum_hit_chance`, and `holy_bolt_maximum_hit_chance` columns
externalize Holy Bolt's native monster magic-hit roll. Legacy Holy Bolt rows
retain the 2/5/95 penalty and chance bounds.
The optional `firebolt_hit_monster_level_scale`,
`firebolt_minimum_hit_chance`, and `firebolt_maximum_hit_chance` columns
externalize Firebolt's native monster magic-hit roll. Legacy Firebolt rows
retain the 2/5/95 penalty and chance bounds.
The optional `bone_spirit_hit_monster_level_scale`,
`bone_spirit_minimum_hit_chance`, `bone_spirit_maximum_hit_chance`, and
`bone_spirit_damage_hit_points_divisor` columns externalize Bone Spirit's
monster magic-hit roll and current-life damage rule. Legacy Bone Spirit rows
retain the 2/5/95 terms and one-third current-life damage.
The optional `persistent_projectile_hit_monster_level_scale`,
`persistent_projectile_minimum_hit_chance`, and
`persistent_projectile_maximum_hit_chance` columns externalize the shared
monster magic-hit roll used by persistent Fire Wall, Fireball, Elemental,
Nova, Apocalypse, and Lightning-segment projectiles. Legacy rows retain the
2/5/95 bounds.
The optional `persistent_projectile_hit_character_level_scale`,
`persistent_projectile_player_minimum_hit_chance`, and
`persistent_projectile_player_maximum_hit_chance` columns externalize the
matching player-hit roll. Legacy rows retain the native 2/5/95 bounds.
The optional `persistent_projectile_player_damage_divisor` column externalizes
the shared player damage divisor; legacy rows retain the native divisor of two.
The optional `player_magic_hit_character_level_scale`,
`player_magic_minimum_hit_chance`, and `player_magic_maximum_hit_chance`
columns in `combat.tsv` externalize the direct Charged Bolt, Firebolt, and Bone
Spirit player-hit roll; legacy combat rows retain the native 2/5/95 terms.
The optional `bone_spirit_player_damage_hit_points_divisor` column externalizes
Bone Spirit's current-life player damage divisor; legacy rows retain the native
one-third damage rule.
The optional `monster_regeneration_level_divisor` column externalizes the
higher-level native monster regeneration divisor; legacy combat rows retain
the native `level / 2` recovery rule.
The optional `monster_player_block_level_scale` column externalizes the
level-difference term used when monster attacks can be blocked; legacy combat
rows retain the native scale of two.
The optional `berserk_resistance_frequency` column externalizes Berserk's
magic-resistance admission roll. Legacy Berserk rows retain the native
1-in-2 compatibility roll.
The optional `charged_bolt_path_random_range` column externalizes Charged
Bolt's initial path-state random roll. Legacy Charged Bolt rows retain the
native 1–15 compatibility range.
The optional `skeleton_melee_delay_chance_base` and
`skeleton_melee_delay_chance_intelligence_scale` columns externalize the
SkeletonMelee movement-delay band. Legacy monster rows retain the native
base-thirty-five, intelligence-scale-four compatibility values. A complete
positive tuple selects the behavior regardless of the row's `native_ai` label;
rows without migrated tuning retain the native-name compatibility path.
The optional `goat_melee_movement_chance_base` and
`goat_melee_movement_chance_intelligence_scale` columns externalize the
GoatMelee non-adjacent movement admission band. Legacy monster rows retain the
native base-twenty-eight, intelligence-scale-two compatibility values. A
complete positive tuple selects the behavior regardless of the row's `native_ai`
label; rows without migrated tuning retain the native-name compatibility path.
The optional `fat_movement_chance_base` and
`fat_movement_chance_intelligence_scale` columns externalize the Fat/Overlord
non-adjacent movement admission band. Legacy monster rows retain the native
base-twenty, intelligence-scale-four compatibility values. A complete positive
tuple selects the behavior regardless of the row's `native_ai` label; rows
without migrated tuning retain the native-name compatibility path.
The optional `fat_attack_chance_base`, `fat_attack_chance_intelligence_scale`,
`fat_special_chance_base`, and `fat_special_chance_intelligence_scale` columns
externalize the Fat/Overlord adjacent attack and special lower bands. Legacy
monster rows retain the native base-twenty/base-fifteen, intelligence-scale-four
compatibility values. Complete positive tuples select the behavior regardless of
the row's `native_ai` label.
The optional `zombie_action_chance_base` and
`zombie_action_chance_intelligence_scale` columns externalize the Zombie
autonomous action admission band. Legacy monster rows retain the native
base-ten, intelligence-scale-two compatibility values. A complete positive
tuple selects the behavior regardless of the row's `native_ai` label; rows
without migrated tuning retain the native-name compatibility path.
The optional `zombie_random_direction_chance_base` and
`zombie_random_direction_chance_intelligence_scale` columns externalize the
Zombie random-direction prepend chance during long-range movement. Legacy
monster rows retain the native base-twenty, intelligence-scale-two values. A
complete positive tuple selects the behavior regardless of the row's
`native_ai` label.
The optional `zombie_movement_distance_minimum`,
`zombie_movement_distance_base`, and
`zombie_movement_distance_intelligence_scale` columns externalize the Zombie
long-range movement threshold. Legacy monster rows retain the native minimum-
two, base-four, intelligence-scale-two compatibility values. A complete
positive tuple selects the behavior regardless of the row's `native_ai` label.
When an action roll succeeds outside melee but below this threshold, the
server performs the native intermediate-range random walk before returning to
the normal adjacent attack path.
The optional `skeleton_king_summon_minimum_distance`,
`skeleton_king_summon_chance_base`, and
`skeleton_king_summon_chance_intelligence_scale` columns externalize the
Skeleton King summon admission boundary. When all three values are present and
positive, the tuple selects the server-owned summon controller regardless of
the row's `native_ai` label; rows without migrated tuning retain the native-name
compatibility defaults. Legacy monster rows retain the native
minimum-three, base-thirty-five, intelligence-scale-four compatibility values.
Skeleton King summon candidates are likewise selected from complete positive
SkeletonMelee delay and attack tuples, while rows labeled `SkeletonMelee`
remain accepted as the legacy fallback.
The optional `hork_demon_attack_chance_base`,
`hork_demon_attack_chance_intelligence_scale`,
`hork_demon_spawn_minimum_distance`, `hork_demon_spawn_chance_base`,
`hork_demon_spawn_chance_intelligence_scale`, `hork_demon_movement_chance_base`,
and `hork_demon_movement_chance_intelligence_scale` columns externalize the
Hork Demon adjacent attack, Hork Spawn, and non-adjacent movement admissions.
When all seven tuning values are present and positive, the tuple selects the
server-owned Hork Spawn controller regardless of the row's `native_ai` label;
rows without migrated tuning retain the native-name compatibility defaults.
Legacy monster rows retain the native base-twenty-eight, minimum-three,
base-forty-three, and base-thirty-three compatibility values with
intelligence-scale-two chance bands.
The optional `gargoyle_safe_distance_base` and
`gargoyle_safe_distance_intelligence_scale` columns externalize the Gargoyle
wounded retreat/self-heal safety boundary. Legacy monster rows retain the
native base-two, intelligence-scale-one compatibility values. A complete
positive tuple selects the behavior regardless of the row's `native_ai` label;
rows without migrated tuning retain the native-name compatibility path.
The optional `gargoyle_healing_health_threshold_percent` column externalizes
the Gargoyle wounded-health admission threshold for self-healing. Legacy
monster rows retain the native 50-percent compatibility threshold. The
validated threshold is selected independently of the row's `native_ai` label;
when supplied without the safe-distance pair, the runtime composes the native
2/1 safety boundary and 16/5/4 healing-formula defaults. The optional
`gargoyle_heal_divisor_multiplier`, `gargoyle_heal_divisor_random_range`, and
`gargoyle_heal_divisor_random_offset` columns follow the same composition rule.
The active catalog's validated `native_ai` identity also selects the server-owned
Butcher pursuit boundary when no complete alternate AI tuple takes precedence;
direct legacy targets without a catalog retain the runtime-label fallback.
The optional `skeleton_ranged_retreat_minimum_distance`,
`skeleton_ranged_retreat_chance_base`,
`skeleton_ranged_retreat_chance_intelligence_scale`,
`skeleton_ranged_attack_chance_base`, and
`skeleton_ranged_attack_chance_intelligence_scale` columns externalize the
SkeletonRanged retreat and clear-lane arrow admission bands. Legacy monster
rows retain the native minimum-four, retreat base-thirteen, and attack
base-three compatibility values with intelligence-scale-two chance bands. A
complete positive tuple selects the behavior regardless of the row's
`native_ai` label; rows without migrated tuning retain the native-name
compatibility path. The validated tuple also preserves ranged-only admission
when no projectile is created, preventing adjacent melee fallback.
The optional `rhino_charge_minimum_distance`,
`rhino_charge_chance_base`, and
`rhino_charge_chance_intelligence_scale` columns externalize the Rhino clear-
lane charge admission boundary. When all three values are present and positive,
the tuple selects the server-owned charge controller regardless of the row's
`native_ai` label; rows without migrated tuning retain the native-name
compatibility defaults. Legacy monster rows retain the native minimum-five,
base-forty-three, intelligence-scale-two compatibility values.

`combat.tsv` declares the shared deterministic combat constants, the native
`Diablo` or `Hellfire` game variant, the global `friendly_fire` switch, and the
restart-in-town level, position, whole-life amount, Q6 scale, and NewLevel
duration. It also declares the player-death gold divisor, maximum projected
stack, native compatibility item type/index, and radius-50 drop search.
The `monster_melee_minimum_hit_chance` and
`monster_ranged_minimum_hit_chance` columns externalize the native ordinary
monster attack floors, while `monster_minimum_hit_chance_level_14`,
`monster_minimum_hit_chance_level_15`, and
`monster_minimum_hit_chance_level_16` externalize the level-specific floors
shared by both attack modes. Legacy combat rows retain the native 15/10 and
20/25/30 compatibility values.
The `monster_to_hit_level_scale`, `monster_to_hit_base_bonus`, and
`monster_ranged_distance_penalty` columns externalize the remaining native
monster accuracy formula terms; legacy rows retain the 2/30/2 compatibility
values.
The `monster_magic_minimum_hit_chance`,
`monster_magic_to_hit_monster_level_scale`,
`monster_magic_to_hit_player_level_scale`,
`monster_magic_to_hit_base_bonus`, and `monster_magic_distance_penalty`
columns externalize the native monster-owned magic-projectile admission
formula; legacy rows retain the 10/2/2/40/2 compatibility values. The
level-specific floors remain shared with ordinary monster attacks.
The `random_steal_life_divisor`, `steal_life_3_percent`,
`steal_life_5_percent`, `steal_mana_3_percent`, and
`steal_mana_5_percent` columns externalize post-hit equipment resource gains;
legacy rows retain the native 8/3/5/3/5 compatibility values.
The `hellfire_armor_bonus_against_demons` and
`hellfire_armor_bonus_against_undead` columns externalize the native Hellfire
class-armor adjustments; legacy rows retain the 40/20 compatibility values.
`pvp_ear_item_id` selects the normalized item definition used for a player-kill
ear. The optional `pvp_ear_native_item_index` and `pvp_ear_misc_id` columns
select the native item-index and miscellaneous-ID projection, defaulting to
23 and 44 for legacy rows; activation requires the normalized ear definition to
 match those configured values and rejects an unknown `pvp_ear_item_id` before
 runtime construction. Activation also requires a walkable restart
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
The Diablo module now exposes a stable-ID query for validated monster types
available at a requested dungeon level; procedural map layout and spawn
placement remain separate migrations. Its server-owned spawn planner can now
turn those candidates and validated level-density metadata into bounded,
walkable, seed-stable placements. Diablo content activation exposes that
planner to the server adapter without bypassing the validated content boundary.
Passing `--procedural-monsters <n>` to the standalone server replaces static
encounter rows with at most `n` generated targets on the default starting level;
`--procedural-monster-level <id>` selects another validated level. The default
still consumes static `monsters.tsv` rows. Procedural targets inherit a
validated per-monster drop policy when the active item and unique catalogs are
available; generated world-item IDs, seeds, values, and unique flags remain
server-owned and seed-stable, and procedural targets retain validated `unique`
and `diablo` encounter flags. Native map synthesis remains separate.
When procedural mode is active, a successful server-authoritative level
transition materializes the same seed-stable policy for the destination level
once, using reserved entity IDs that coexist with dynamic combat entities.
Static route endpoints and inactive objects are reserved during activation;
live players, items, objects, monsters, portals, and warp endpoints are also
reserved during runtime transitions, so generated targets do not overlap an
authoritative occupant.
Schema 1.71 adds the native `demon` monster-class capability. The server
validates it from external type rows and applies `TripleDemonDamage` only at
the authoritative physical attack boundary; clients never infer the class
from display names.
Schema 1.72 adds the native `animal` monster-class capability. The server
validates it from external type rows and applies the native inverse sword/mace
modifiers at the authoritative physical attack boundary.
Schema 1.73 adds the native monster `knockback` ability flag. The server
validates it from external type rows and displaces a player one valid tile
after a successful autonomous monster hit.
Schema 1.74 adds the native Monk chest-armor tuning columns. The server
applies the declared level-scaled light, medium, unique-medium, and
unique-heavy bonuses after dexterity and usable equipment armor.
Schema 1.75 adds the optional `aggro_range` monster-type column. Generated
encounters carry this validated radius into autonomous server behavior; older
packs default to the compatibility radius of six.
Schema 1.76 adds the optional `death_animation_ticks` monster-type column.
The checked-in Hellfire rows carry native death-frame duration derived from
the active `monstdat` frame and rate values; older packs retain an immediate
cleanup default of zero.
Schema 1.77 adds the optional `experience_reward` monster-type column. The
checked-in Hellfire rows carry native `monstdat` experience values, while the
base compatibility row retains the existing ruleset reward when omitted.
Schema 1.78 adds optional `to_hit` and `to_hit_special` monster-type columns.
The checked-in Hellfire rows carry native normal and special attack accuracy;
authoritative normal attacks apply the native level, armor, and minimum-hit
formula, while older rows retain the global-hit compatibility fallback.
Schema 1.79 adds the optional `native_ai` monster-type column. The checked-in
Hellfire rows copy the native `MonsterAIID` identity from `monstdat.tsv`; the
server validates and persists the identity while clients do not dispatch AI.
The first behavior using that identity is the native `Fat` Overlord normal and
special melee bands, which consume validated admission, accuracy, and damage
columns for the Toad Demon family regardless of the row's `native_ai` label. The
`Rhino` identity now drives the server-owned
clear-lane charge using the same special columns; remaining AI-specific behavior
is still incremental.
Schema 1.80 adds the optional `intelligence` monster-type column. The checked-in
Hellfire rows copy native `monstdat.tsv` intelligence values; authoritative AI
probability bands consume the validated value while clients continue to treat it
as server-owned metadata. The Rhino charge now uses the native `43 + 2 *
intelligence` boundary.
Schema 1.81 adds the validated `monster_missiles.tsv` table. Each native AI
identity maps to its retained native missile ID and authoritative projectile
behavior. The mapping remains content-owned and validated; checked-in rows use
the most specific migrated behavior, while older overlays may retain
`Targeted` as a compatibility spelling until their projectile controllers are
migrated. Mapped dispatch first resolves the active monster-type `native_ai`
identity and then retains the runtime-label lookup as a legacy fallback.
When a complete migrated AI-tuning tuple is absent, legacy controller defaults
use the active monster-type identity before retaining the direct-target
runtime-label fallback; complete alternate tuning retains precedence.
Schema 1.82 adds optional `attack_mode` and
`avoidance_probability_percent` columns to `monster_missiles.tsv`. The active
content pack now selects the native `AiRanged` or `AiRangedAvoidance` branch
and its probability scaling; omitted values retain the `Ranged`/100-percent
compatibility defaults. The server also preserves the native adjacent melee
fallback band for `AiRangedAvoidance` (`1000 * (intelligence + 6)` per 10,000,
halved for Acid) instead of admitting every shared melee tick. Failed or
blocked ranged rolls use a deterministic lateral round-walk fallback rather
than generic pursuit toward the player.
Schema 1.83 adds the optional `monster_id` discriminator to
`monster_missiles.tsv`. A monster-specific row takes precedence over its
native-AI wildcard, allowing shared native AI identities such as `Bat` to
retain content-specific controllers without engine-side type checks.
The checked-in Familiar row uses that discriminator to select the native Bat
Lightning follow-up after an adjacent hit; the server captures its
`GenerateRnd(10) + 1` damage and emits a `MonsterMagic` projectile while
unrelated Bat rows retain their mapped-charge behavior.
Schema 1.84 adds five native Counselor missile rows for monster IDs 107, 108,
109, 110, and 113. They select Firebolt, Charged Bolt, Lightning, or Fireball
from validated content; the server owns the monster-specific Charged Bolt
controller and its player hit boundary.
Schema 1.85 adds the native `Mega` `InfernoControl` mapping. The server preserves
Mega's distance- and intelligence-based special-attack admission, speed-32
Inferno controller, three-segment limit, and monster damage roll through the
same server-owned persistent collision path used by player Inferno. The current
Inferno admission values are supplied by the validated missile row.
The `SkeletonRanged` identity now consumes the native `3 + 2 * intelligence`
attack band for a clear-lane arrow hit; the server owns the resulting accuracy,
damage event, and blocked-lane movement fallback. Complete positive tuples
select the retreat and attack behavior regardless of the row's `native_ai`
label.
The `SkeletonMelee` identity now consumes the validated adjacent attack band
(`20 + 2 * intelligence` in the checked-in rows) while older rows retain the
native compatibility formula. A complete positive attack tuple selects the
behavior regardless of the row's `native_ai` label.
The `Gargoyle` identity now owns the native wounded self-heal boundary: once the
monster is beyond `intelligence + 2` tiles, the server applies the deterministic
`max_hp / (16 * (roll + 4))` heal and persists the special lock until the player
closes that boundary. Validated safety, threshold, and healing-formula tuning
is selected independently of the row's `native_ai` label; clients do not
execute this behavior.
Validated `MonsterArrow` missile mappings use the shared native `AiRanged`
boundary: clear missile lanes produce authoritative arrow damage, while the
mapping row supplies the native close-range retreat distance and
`10 * (intelligence + 7)` avoidance band for deterministic retreat-cell
selection. The controller is selected by projectile behavior, so valid content
may use a different `native_ai` label or missile name; legacy `Targeted` rows
remain compatible. Their validated `projectile_speed` now controls arrow travel;
their validated `projectile_lifetime` controls the server-owned lifetime when
present, and omitted values retain the native speed-one and distance-based
lifetime fallbacks. The optional validated `damage_type` is carried into the
impact path and applies the target's matching resistance; omitted values retain
the native physical arrow behavior. Mapped arrow rows also keep ranged-only
monsters from falling through to adjacent melee when the content label is
changed.
Validated `SnakeCharge` missile mappings select the native two-tile charge
controller and server-owned collision path independently of the `Snake` label
or `Rhino` missile name. Outside the charge boundary, the same mapping now
selects the server-owned deterministic Snake drift branch; native delay ticks
retain the shared generic navigation fallback for compatibility.
The `GoatMelee` identity now consumes the validated adjacent attack band
(`23 + 2 * intelligence` in the checked-in rows) and wounded special branch,
with special accuracy and damage resolved by the server. Complete positive
tuples select these behaviors regardless of the row's `native_ai` label.
The `SkeletonMelee` identity now uses its native adjacent `20 + 2 * intelligence`
attack admission boundary; clients never perform the monster attack roll.
The `Zombie` identity now uses its native `10 + 2 * intelligence` action band,
validated random-direction prepend chance, and long-range
`2 * intelligence + 4` movement boundary; movement remains
server-owned and falls back to the shared authoritative navigation when needed.
Native monster regeneration is also server-owned: each live monster recovers
one life at level 1 or `level / 2` at higher levels per simulation tick, capped
at maximum life and preserved in exact Q6 state.
Validated `BloodStar` missile mappings dispatch through the existing
authoritative magic-projectile path and consume the validated ranged-retreat
tuning independently of the `Succubus`/`LazarusSuccubus` label; player hit,
block, resistance, and damage resolution remain server-owned. The optional
`spell_id` mapping is used directly for the projectile identity and is
cross-validated against the active spell catalog. When no missile catalog is
active, legacy Succubus-family labels retain the native stable-ID fallback.
When present, the mapping's `projectile_speed`, `projectile_lifetime`, and
`damage_type` fields override the corresponding spell transport values, and
`projectile_damage` supplies a fixed impact value; omitted fields retain the
native spell-backed transport and monster-damage defaults.
Usable identified equipment with native `Knockback` now applies the same
authoritative physical-hit boundary: surviving non-petrified targets move one
valid unoccupied cell away from the attacker, while blocked destinations and
misses leave positions unchanged.
Hellfire `ACAgainstDemons` and `ACAgainstUndead` item powers add 40 and 20
armor respectively before autonomous monster hit resolution when the target
monster carries the corresponding validated demon or undead capability.
Hellfire `Devastation` consumes one native RNG draw after a successful
physical hit and triples damage when the result is below 5; the server keeps
that roll authoritative and excludes unusable or unidentified magical items.
Hellfire `Jesters` then consumes its native 0..200 roll and applies the
100..200 fivefold expansion in Q6 units, preserving fractional damage through
the authoritative target and on-hit resource boundaries.
Hellfire `Peril` applies pre-modifier self-damage from the base attack and
eligible `PlusGetHit` equipment, then applies the validated final physical-hit
multiplier in Q6; the server owns Mana Shield conversion, death handling, and
both damage events. Hellfire `Doppelganger` consumes its validated post-hit
proc chance, excludes unique and Diablo targets, and creates a full-health
non-unique copy on the last available adjacent cell; the spawned monster
remains in authoritative world snapshots and hashes.
Hellfire `Decay` reduces eligible hand weapons' damage bonus by five in native
order after an authoritative attack and removes a weapon at the native -100
threshold; the server preserves the separate hit roll even when post-armor
damage is zero.
Hellfire `THORNS` retaliates after a successful autonomous monster hit with a
server-owned native 1-3 physical damage roll against the surviving monster;
the reaction occurs before player death handling and blocked knockback.
Hellfire `DRAINLIFE` applies native 4-Q6 physical damage once per active
non-town tick; the server routes it through Mana Shield and the environment
death boundary, which does not drop equipment or a PvP ear.
Native `ABSHALFTRAP` halves armed-object damage before the server-owned player
damage resolver and preserves the exact Q6 amount for protocol events.
Native `FIREDAM` and `LIGHTDAM` equipment rolls its validated elemental ranges
after a successful physical hit, applies target resistance, and preserves the
exact Q6 damage events through the server-owned combat boundary.
The `CriticalStrike` class flag enables the native class rule for eligible
player attacks: after a successful hit, the server consumes one level-based
roll and doubles physical damage before armor and downstream class/item
effects. Class flags therefore select the behavior while combat and RNG remain
server-authoritative.
Validated unique and affix rows can now express all native Hellfire
damage/armor powers—`DEVASTATION`, `DECAY`, `PERIL`, `JESTERS`,
`DOPPELGANGER`, `ACDEMON`, and `ACUNDEAD`—which resolve to the same
authoritative effect flags used by combat.
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
Hammer of Jholm (`unique_id=1048`) adds its native Maul base item with fixed
damage, indestructibility, strength, and to-hit powers.
Civerb's Cudgel (`unique_id=1049`) reuses the validated Mace base item with
demon damage, dexterity-curse, and magic-curse powers.
The Celestial Star (`unique_id=1050`) adds its native Flail base item with
no-strength, light, damage-modifier, and armor-curse powers.
Baranar's Star (`unique_id=1051`) adds its native Morning Star base item with
to-hit, damage, attack-speed, vitality, dexterity-curse, and durability powers.
Gnarled Root (`unique_id=1052`) reuses the validated Spiked Club base item with
to-hit, damage, dexterity, magic, all-resistance, and armor-curse powers.
The Cranium Basher (`unique_id=1053`) reuses the validated Maul base item with
damage-modifier, strength, indestructibility, mana-curse, and all-resistance
powers. The Hammer of Jholm base row now carries the corrected native Maul
index.
Schaefer's Hammer (`unique_id=1054`) adds its native War Hammer base item with
damage-curse, lightning-damage, life, to-hit, lightning-resistance, and light
powers.
Dreamflange (`unique_id=1055`) reuses the validated Mace base item with magic,
mana, magic-resistance, light, and spell-level powers.
Staff of Shadows (`unique_id=1056`) adds its native Long Staff base item with
magic-curse, to-hit, damage, light-curse, and fast-attack powers.
Immolator (`unique_id=1057`) reuses the validated Long Staff base item with
fire-resistance, fire-damage, mana, and vitality-curse powers.
Storm Spire (`unique_id=1058`) adds its native War Staff base item with
lightning-resistance, lightning-damage, strength, and magic-curse powers.
Gleamsong (`unique_id=1059`) adds its native Short Staff base item with mana,
strength-curse, vitality-curse, and charged-spell powers.
Thundercall (`unique_id=1060`) adds its native Composite Staff base item with
to-hit, lightning-damage, charged-spell, lightning-resistance, and light powers.
The Protector (`unique_id=1061`) reuses the validated Short Staff base item with
vitality, get-hit, armor, charged-spell, and thorns powers.
Naj's Puzzler (`unique_id=1062`) reuses the validated Long Staff base item with
magic, dexterity, all-resistance, charged-spell, and life-curse powers.
Mindcry (`unique_id=1063`) adds its native Quarter Staff base item with magic,
charged-spell, all-resistance, and spell-level powers.
Rod of Onan (`unique_id=1064`) reuses the validated War Staff base item with
charged-spell, damage, and attribute powers.
Helm of Spirits (`unique_id=1065`) adds its native Helm base item with a
life-steal power.
Thinking Cap (`unique_id=1066`) adds its native Skull Cap base item with mana,
spell-level, resistance, and durability powers.
OverLord's Helm (`unique_id=1067`) reuses the native Helm base item with
attribute, magic-curse, and durability powers.
Fool's Crest (`unique_id=1068`) reuses the native Helm base item with cursed
attributes, life, get-hit, and thorns powers.
Gotterdamerung (`unique_id=1069`) adds its native Great Helm base item with
attribute, armor, get-hit, resistance, and light-curse powers.
Royal Circlet (`unique_id=1070`) adds its native Crown base item with
attribute, mana, armor, and light powers.
Torn Flesh of Souls (`unique_id=1071`) adds its native Rags base item with
armor, vitality, get-hit, and indestructible powers.
The Gladiator's Bane (`unique_id=1072`) adds its native Studded Leather Armor
base item with armor, get-hit, durability, and cursed-attribute powers.
The Rainbow Cloak (`unique_id=1073`) adds its native Cloak base item with
armor, attribute, resistance, life, and durability powers.
Leather of Aut (`unique_id=1074`) adds its native Leather Armor base item with
armor, strength, magic-curse, dexterity, and indestructible powers.
Wisdom's Wrap (`unique_id=1075`) adds its native Robe base item with magic,
mana, light-resistance, armor, and get-hit powers.
Sparking Mail (`unique_id=1076`) adds its native Chain Mail base item with
armor and lightning-damage powers.
Scavenger Carapace (`unique_id=1077`) adds its native Breast Plate base
item with armor, get-hit, armor-curse, dexterity, and light-resistance powers.
Nightscape (`unique_id=1078`) adds its native Cape base item with fast-recovery,
light-curse, armor, dexterity, and all-resistance powers.
Naj's Light Plate (`unique_id=1079`) adds its native Plate Mail base item with
no-strength, magic, mana, all-resistance, and spell-level powers.
Demonspike Coat (`unique_id=1080`) adds its native Full Plate Mail base item
with armor, get-hit, strength, indestructible, and fire-resistance powers.
The Deflector (`unique_id=1081`) adds its native Buckler base item with armor,
resistance, damage-curse, and to-hit-curse powers.
Split Skull Shield (`unique_id=1082`) reuses the native Buckler base item with
armor, life, strength, light-curse, and durability powers.
Dragon's Breach (`unique_id=1083`) adds its native Kite Shield base item with
armor, fire-resistance, strength, magic-curse, and indestructible powers.
Blackoak Shield (`unique_id=1084`) adds its native Small Shield base item with
armor, dexterity, vitality-curse, light-curse, and durability powers.
Holy Defender (`unique_id=1085`) adds its native Large Shield base item with
armor, get-hit, fire-resistance, durability, and fast-block powers.
Stormshield (`unique_id=1086`) adds its native Gothic Shield base item with
armor, get-hit, strength, indestructible, fast-block, and lightning-resistance powers.
Bramble (`unique_id=1087`) reuses the native Ring base item with cursed
attributes, damage modifier, and mana powers.
Ring of Regha (`unique_id=1088`) reuses the corrected native Ring base item
with magic, magic-resistance, light, strength-curse, and dexterity-curse powers.
The Bleeder (`unique_id=1089`) reuses the corrected native Ring base item with
magic-resistance, mana, and life-curse powers.
Constricting Ring (`unique_id=1090`) reuses the corrected native Ring base item
with all-resistance and life-drain powers.
Ring of Engagement (`unique_id=1091`) reuses the corrected native Ring base
item with get-hit, thorns, armor, and target-armor powers.
Giant's Knuckle (`unique_id=1092`) reuses the corrected native Ring base item
with strength and dexterity-curse powers.
Mercurial Ring (`unique_id=1093`) reuses the corrected native Ring base item
with dexterity and strength-curse powers.
Xorine's Ring (`unique_id=1094`) reuses the corrected native Ring base item
with magic and strength-curse powers.
Karik's Ring (`unique_id=1095`) reuses the corrected native Ring base item
with vitality and magic-curse powers.
Ring of Magma (`unique_id=1096`) reuses the corrected native Ring base item
with fire resistance and lightning/magic-resistance curses.
Ring of the Mystics (`unique_id=1097`) reuses the corrected native Ring base
item with magic resistance and fire/lightning-resistance curses.
Ring of Thunder (`unique_id=1098`) reuses the corrected native Ring base item
with lightning resistance and fire/magic-resistance curses.
Amulet of Warding (`unique_id=1099`) uses normalized item `171`, preserving
the native AMULET item index `160`, with all-resistance and life-curse powers.
Gnat Sting (`unique_id=1100`) reuses normalized Hunter's Bow item `124`,
preserving native item index `145`, with multi-arrow, damage, fast-attack, and
indestructible powers.
Flambeau (`unique_id=1101`) reuses normalized Composite Bow item `123`,
preserving native item index `147`, with Fireball-bow damage, zero base damage,
and indestructible powers.
Armor of Gloom (`unique_id=1102`) reuses normalized Full Plate item `165`,
preserving native item index `71`, with zero strength requirement, fixed armor
225, all-resistance reset, and light curse.
Blitzen (`unique_id=1103`) reuses normalized Composite Bow item `123`,
preserving native item index `147`, with native lightning-arrow damage, zero base
damage, and indestructible powers.
Thunderclap (`unique_id=1104`) reuses normalized War Hammer item `146`,
preserving native item index `138`, with charged-bolt hit damage, strength,
lightning resistance, light, and indestructible powers.
Shirotachi (`unique_id=1105`) reuses normalized Great Sword item `136`,
preserving native item index `130`, with one-handed, fastest-attack,
target-armor, and lightning-damage powers.
Eater of Souls (`unique_id=1106`) reuses normalized Two-Handed Sword item `135`,
preserving native item index `129`, with indestructibility, life, life-steal,
mana-steal, and drain-life powers.
Diamondedge (`unique_id=1107`) reuses normalized Long Sword item `131`,
preserving native item index `126`, with fixed durability, to-hit, damage,
lightning resistance, and fixed-armor powers.
Bone Chain Armor (`unique_id=1108`) reuses normalized Chain Mail item `157`,
preserving native item index `62`, with fixed armor and native extra armor
against undead.
Demon Plate Armor (`unique_id=1109`) reuses normalized Full Plate item `165`,
preserving native item index `71`, with fixed armor and native extra armor
against demons.
Acolyte's Amulet (`unique_id=1110`) reuses normalized Amulet item `171`,
preserving native item index `160`, with the native 50-percent mana-to-life
conversion represented as dynamic equipment metadata.
Gladiator's Ring (`unique_id=1111`) reuses normalized Ring item `113`,
preserving native item index `157`, with the native 40-percent life-to-mana
conversion represented as dynamic equipment metadata.
When a new authoritative player template is created, equipped items apply their
native Q6 life/mana bonuses and conversion portions independently. The same
resolver runs after every accepted equipment transfer, preserving native
resource debt when an item is equipped or removed; base maxima and effective
maxima persist through snapshots, saves, and protocol projection.
Usable, identified equipped items also contribute their native `plus_damage`
percent, `plus_damage_modifier` flat damage, `plus_to_hit`, and
`plus_enemy_armor_class` armor-piercing values to authoritative player attacks;
the server excludes unequipped, unusable, and unidentified magical items from
those bonuses.
Initialized players also derive the physical min/max range from usable
equipped item damage, then apply the native class strength/dexterity modifier.
When no usable item damage is present, the server uses native unarmed, shield,
and Monk level-scaled fallbacks; compatibility templates retain global base
damage.
Against externally classified undead targets, the server applies the native
50% sword penalty or 50% mace bonus after the critical-damage boundary.
Equipped item `armor_class` and native percentage armor metadata likewise
recompute effective player armor together with native dexterity armor during
player creation, equipment transfers, and snapshot restoration.
Usable, identified equipped items also apply their native `plus_strength`,
`plus_magic`, `plus_dexterity`, and `plus_vitality` bonuses to authoritative
current attributes; base attributes remain the persisted source for recalculation.
The class table's Q6 `item_life_per_vitality` and `item_mana_per_magic` factors
then convert usable, identified equipped vitality and magic into authoritative
life and mana maxima. Flat item resources and Hellfire conversions use the same
native requirement and identification rules.
Usable, identified equipped `plus_get_hit` is likewise server-owned for
autonomous monster physical hits; it is applied after the current hit/armor
resolution and cannot reduce a successful hit below the configured minimum.
Native life/mana-on-hit flags are also server-owned at the same equipment
eligibility boundary. Successful physical attacks apply random life steal and
the native 3/5-percent life/mana rates in Q6 units; `NoMana` suppresses mana
steal, and the server emits the existing healing event for applied life gain.
Schema 0.64 adds the validated `quest_types.tsv` catalog for all base and
Hellfire native quest IDs. Quest-state rows may resolve a declared stable
quest identity; native IDs, routing levels, set-level sources, and
single-player availability remain external metadata.

Schema 0.65 adds `level_generation.tsv`, which externalizes the native
level-range monster-density divisor, multiplayer monster multiplier, and trap
percentage. The catalog is validated at activation, must cover every declared
world level when present, and owns deterministic encounter-count calculation;
actual procedural map synthesis remains a separate migration.

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

Schema 1.232 adds optional `quest_symbol` columns to `quest_types.tsv`,
`quests.tsv`, `quest_states.tsv`, and `quest_pools.tsv`. Active rows resolve
through the `quest` namespace in `content_ids.tsv` and reject numeric/symbol
mismatches; older numeric-only rows remain compatible.

Schema 1.250 adds the stable `warp` namespace. Static level-trigger rows may
carry `warp_symbol` values resolved through `content_ids.tsv`; numeric-only
routes remain compatible while active symbols are cross-checked.

Schema 1.251 adds normalized stable definitions for the Hellfire quest items
Rune Bomb, Theodore, Auric Amulet, Brown Suit, and Grey Suit. Their native item
indices and cursor projections remain explicit compatibility metadata, while
older packs may continue to use the legacy `itemdat.tsv` fallback.

Schema 1.252 normalizes the first five Hellfire unique base-item projections:
The Undead Crown, the shared Hellfire ring base, Optic Amulet, Ring of Truth,
and Harlequin Crest. Their stable item symbols, display names, and native
cursor projections now cross the same validated item boundary.

Schema 1.253 normalizes the next five Hellfire unique base-item projections:
Veil of Steel, Arkaine's Valor, Griswold's Edge, Bovine Plate, and The
Butcher's Cleaver. Their stable item symbols, native display names, and cursor
projections are validated alongside their unique base-item links.

Schema 1.254 normalizes the next five Hellfire bow base-item projections:
Hunter's Bow, Long Bow, Short Bow, Long War Bow, and Short Battle Bow. Their
stable item symbols, native display names, and cursor projections are validated
for all unique definitions that reuse those bases.

Schema 1.255 normalizes the next five Hellfire sword base-item projections:
Dagger, Sabre, Falchion, Broad Sword, and Long Sword. Their stable item symbols,
native display names, and cursor projections are validated for all unique
definitions that reuse those bases.

Schema 1.256 normalizes the next five Hellfire sword base-item projections:
Claymore, Broad Sword, Scimitar, Bastard Sword, and Two-Handed Sword. The
duplicate Hellfire Broad Sword receives its own stable symbol while retaining
the native display and cursor projection.

Schema 1.257 normalizes the next five Hellfire axe base-item projections:
Large Axe, Broad Axe, Battle Axe, Small Axe, and Great Axe. Their stable item
symbols, native display names, cursor projections, and `Axe` generation tags
are validated against the native table.

Schema 1.258 normalizes the next five Hellfire mace-family projections: Mace,
Maul, Flail, Morning Star, and War Hammer. Their stable symbols, corrected
native indices, display names, cursor projections, and `Mace` generation tags
are validated against native item and unique-item definitions.

Schema 1.259 normalizes the next five Hellfire staff projections: Long Staff,
War Staff, Short Staff, Composite Staff, and Quarter Staff. Their stable symbols,
native indices, display names, cursor projections, and `Staff` generation tags
are validated against the native table; Quarter Staff retains its native
SHORT_STAFF cursor projection.

Schema 1.260 normalizes the next five Hellfire armor projections: Helm, Skull
Cap, Great Helm, Crown, and Rags. Helmet rows retain helmet placement while
Rags is corrected to its native LightArmor/BodyArmor placement and cursor.

Schema 1.249 adds the stable `portal` namespace. Static portal rows may carry
`portal_symbol` values resolved through `content_ids.tsv`; numeric-only routes
remain compatible while active symbols are cross-checked.

Schema 1.248 adds the stable `store` namespace. Store stock rows and the Adria
service identity may carry stable symbols resolved through `content_ids.tsv`;
numeric-only vendor rows remain compatible while active symbols are cross-checked.

Schema 1.247 adds the stable `object` namespace. World-object rows may carry
`object_symbol` values resolved through `content_ids.tsv`; numeric-only object
rows remain compatible while active symbols are cross-checked.

Schema 1.246 adds the stable `set_level` namespace. Set-level definitions
retain their native symbols and quest-type rows may carry `set_level_symbol`;
the valid native `SL_NONE` ID zero is preserved while numeric-only rows remain
compatible.

Schema 1.245 extends the stable `level` namespace to authored monster,
object, quest, and combat restart links through `level_symbol` columns.
Numeric-only level references remain compatible while active symbols are
cross-checked against the composed world.

Schema 1.244 adds the stable `level` namespace. `levels.tsv`, warps, portals,
and Town Portal world rules may carry level symbols; activation cross-checks
them against the retained numeric level IDs and geometry. Numeric-only level
links remain compatible.

Schema 1.243 adds the stable `class` namespace. Class definitions and
class-keyed loadout and animation rows may carry `class_symbol`; native class
ID zero remains valid and symbolic references are cross-checked at activation.

Schema 1.242 adds the stable `monster` namespace. Monster type rows carry
`monster_symbol`, and authored encounter, missile selector/spawn, unique
monster, and quest-completion links may use corresponding symbolic columns.
Activation cross-checks symbols against retained numeric IDs and the active
monster catalog; numeric-only rows remain compatible.

Schema 1.241 extends stable unique identities to optional catalog-backed
store stock and monster-drop references through `unique_item_symbol` and
`drop_unique_item_symbol`. Numeric-only references remain compatible while
active symbols are cross-checked against the unique catalog.

Schema 1.240 adds the stable `unique` namespace. Shipped `unique_items.tsv`
rows carry a `unique_symbol`, and activation cross-checks those symbols against
retained numeric IDs before publishing the unique catalog. Numeric-only rows
remain compatible for older external packs.

Schema 1.239 adds `effect_item_symbol` to `objects.tsv` for
`QuestItemCompletion` rows. The symbol resolves through the stable `item`
namespace and is cross-checked against `effect_amount`; sequence, healing,
damage, and experience effects continue to use their numeric amounts.

Schema 1.238 completes stable item links in shipped unique-item definitions.
Every `unique_items.tsv` row now carries a `base_item_symbol` that resolves to
the retained numeric `base_item_id`; older external packs may omit the
optional column and remain numeric-only.

Schema 1.237 completes the shipped normalized item namespace. Every base and
Hellfire `items.tsv` definition now carries an `item_symbol`, and every symbol
is declared exactly once in the corresponding `content_ids.tsv` overlay. The
numeric `item_id` remains the compatibility projection; older third-party
packs may omit the optional column and continue using numeric-only rows.

Schema 1.236 extends the stable `item` namespace to item combinations,
catalog-backed monster drops, and the PvP-ear combat rule. Numeric-only links
remain compatible while active rows reject symbol/numeric mismatches.

Schema 1.235 extends the stable `item` namespace to store stock, unique-item
base references, quest completion rewards, and item quest effects. Each optional
symbol is cross-checked against its retained numeric ID; numeric-only rows remain
compatible.

Schema 1.234 adds the stable `item` namespace to the shipped base and Hellfire
packs. `class_loadout_items.tsv` accepts an optional `item_symbol`; activation
resolves it through `content_ids.tsv`, rejects numeric/symbol mismatches, and
validates every active stable item entry against `items.tsv`. Numeric-only rows
remain compatible.

Schema 1.233 extends the same optional `quest` symbols to warp gates, linked
objects, item quest effects, and follow-on quest activation. Each authored link
rejects numeric/symbol mismatches while retaining numeric-only compatibility.

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
ID, optional stable `ability_spell_symbol`, semantic `ability_kind`, and optional
learned spell/level with `starting_spell_symbol` for every active class. Its companion
`class_loadout_items.tsv` gives each starting item a contiguous per-class order,
stable `item_id` plus optional `item_symbol`, exact `Inventory`, `Equipment`, or
`Belt` location, and native slot or inventory-grid anchor. Activation rejects
missing classes, unknown
items, invalid stable spell bounds, unusable equipment, duplicate slots,
two-handed/off-hand conflicts, belt-ineligible items, overlapping inventory
footprints, more than five starting items, and any directly castable starting
memorized, charged-item, scroll, or rune source whose spell is absent from the
activated spell catalog. Native class-skill IDs are not required to be ordinary
spell definitions because several select cursor-driven item or object
interactions such as repair, recharge, or trap disarm. New-session item seeds
are derived deterministically from the session, class, order, and item ID; they
are server-owned and unique within the loadout.

`ability_spell_symbol` resolves through the active `content_ids.tsv` `ability`
namespace, with a legacy fallback to `spell`; `starting_spell_symbol` resolves
through the `spell` namespace. Activated packs cross-check each symbol against
its numeric compatibility column; older tables without the symbol columns
retain numeric-ID behavior. `ability_kind` identifies the content-owned semantic role of a class skill
(`Repair`, `TrapDisarm`, `Recharge`, `Search`, `Identify`, or `Rage`) so
presentation clients do not infer behavior from numeric spell IDs. The server
still validates and admits the stable `ability_spell_id` independently.
`item_symbol` resolves through the active `content_ids.tsv` `item` namespace and
is cross-checked against `item_id`; loadout tables without that column retain
numeric-only behavior.
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
server owns the moving controller and the validated `immolation_projectile_count`
radial burst (the shipped row retains 36 projectiles), spell-level speed cap,
and FireballBow projectile impact lifecycle.

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
controller and ball behaviors. The validated `nova_projectile_count` controls
the deterministic prefix of the native nine-point quarter-radius order and
four mirrored emissions; the shipped row retains 36 balls. Each ball
travels at speed `16` for `255` ticks, uses the Lightning damage type and
quarter-resistance rule, and can pass through a hit target while continuing to
its endpoint. Ball damage is generated once as
`ScaleSpellEffect((GenerateRndSum(6, 5) + character level + 5) / 2,
effective spell level) * 64`.

Normalized `items.tsv` includes a stable content `item_id` and an explicit
`native_item_index` used only for compatibility projection. `item_tags` also
declares semantic item categories (`Weapon`, `Sword`, `Axe`, `Mace`, `Bow`,
`Shield`, `Staff`, `Belt`, `OneHanded`, `TwoHanded`, `BodyArmor`, `Helmet`,
`Ring`, `Amulet`, `LightArmor`, `MediumArmor`, `HeavyArmor`, and `Gold`) for
active rows; combat and loadout validation use these tags before the native numeric
fallback. `charges` and
`max_charges` preserve charged-item state. `spell_source_kind` explicitly
classifies an item as `None`, `ChargedItem`, or `ConsumableItem`; source
matching uses the normalized `spell_id` and no longer depends on native
miscellaneous-ID ranges for current packs. The content ID remains the key for
catalog references and overlays; native row/index values must not become
authoritative identities. Loadout validation, native player attack timing,
weapon-class damage, armor subtype scaling, and block eligibility consume these
subtype tags through the shared authoritative item-classification seam at the
server boundary. The
base pack assigns stable ID `24` to the native
ear definition. Its victim-derived name and cursor are runtime state carried by
the generic item snapshot rather than localized catalog text.
Normalized unique-item and magic-affix `SPELL` powers also preserve this
classification when they add a charged spell after base-item generation.

Spell casting uses the validated source kind and spell identity from the item
state or, for legacy states, the active item catalog. A charged cast must name
the matching source equipped in native left-hand slot `4`; an
accepted cast decrements `charges`. A consumable cast must name the matching
source in inventory or belt; an accepted cast removes that exact item. Legacy
tables without `spell_source_kind` retain the native miscellaneous-ID fallback
until they are normalized. Store recharge likewise honors explicit
`ChargedItem`/non-charged source metadata from state or active item content
before applying legacy fallback, and
uses the active item's validated `Staff` tag before the native type and
miscellaneous-ID fallback when source metadata is absent.
The Sorcerer Recharge class ability additionally requires the validated `Staff`
subtype when an item catalog is active. These values are validated and consumed
by the Diablo module rather than trusted from a client.

`monsters.tsv` keeps encounter state and immutable gameplay capabilities outside
the engine. Its optional `stone_curse_immune`, `undead`, `animal`, `knockback`, `player_minion`,
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
destination crawl applied by the server. Optional quest-gate columns keep
native entrances such as the Hellfire crypt closed until their linked quest is
completed; authored quest-linked objects and triggers reject missing quest
catalogs and unknown quest identities before publishing the candidate ruleset.

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
`consumable_behavior` optionally selects special handling for consumable spell
casts (`Standard` or `RuneFire`). The checked-in Rune of Fire row declares
`RuneFire`; older tables without the column retain the native miscellaneous-ID
fallback.

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
Usable identified equipment with the native `NoMana` flag bypasses absorption
and rejects ordinary memorized spell casts before mana is consumed. Free class
abilities and charged or consumable sources remain valid.
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
equipment plus class rules and capped at 75 percent. A usable identified item
with the native `ZeroResistance` flag resets fire, lightning, and magic
resistance after natural class resistance and before that cap. Positive
resistance overrides blocking. Only authoritative `Stand` and melee `Attack`
modes admit a block. A successful block immediately enters the class-duration
`Block` mode; an unresisted threshold hit enters class-duration `GotHit`, and
item recovery flags shorten those durations exactly as in native code.
Hellfire additionally consumes the blocked-missile rotation-selection draw
before Charged Bolt enters its common impact phase.

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
| `classes.tsv` | `class_id` | Native-compatible class identity, availability, flags, attributes, combat modifiers, Q6 resource formulas, Mana spell scaling, melee armor reduction, Monk chest-armor tuning, class-ability tuning, Critical Strike chance/multiplier, IronSkin armor divisor, class defense tuning, and FastBlock timing. |
| `class_animations.tsv` | `class_id` | Native action and animation frame timings for each class. |
| `class_loadouts.tsv` | `class_id` | Starting Gold, numeric compatibility and symbolic class-skill identity, semantic role, and optional learned spell/level. |
| `class_loadout_items.tsv` | `class_id`, `item_order` | Deterministic starting equipment, belt, and inventory placement with optional stable `item_symbol` cross-checks. |
| `combat.tsv` | singleton | Variant, combat defaults, primary-attribute and resistance ceilings, native difficulty level/accuracy/armor/damage/experience/health tuning, Monk staff/unarmed/weapon, Bard sword/bow/fallback, standard fallback, Barbarian bow/fallback/axe/mace/vitality scaling, Thorns retaliation, Hellfire Decay, Devastation, Jesters, Peril, Doppelganger, Triple Demon Damage, PvP block tuning, equipment FastBlock timing, hit-recovery tiers, melee attack-speed tiers, Rogue damage scaling, monster hit-chance floors and accuracy formula tuning, post-hit equipment restoration, Hellfire class armor, restart rules, death-gold rules, and PvP-ear identity/projection with optional `pvp_ear_item_symbol`. |
| `content_ids.tsv` | `kind`, `symbol` | Bidirectional stable symbolic IDs; item, unique-item, quest, class capabilities such as `ability:trap_disarm`, world objects, stores, set levels, spells, levels, and portals are declared separately from runtime projections and validated against their active catalogs during activation. |
| `item_combinations.tsv` | `result_item_id`, `required_item_ids` | Validated server-owned item-combination recipes with optional result and input item symbols. |
| `levels.tsv` | `level_id` | Walkability, missile geometry, default-start selection, and multiplayer death-drop policy. |
| `world_rules.tsv` | singleton | External town-level identity, optional stable Adria service-store identity, and optional Town Portal destination coordinates/slot layout; activation validates the level, requires every configured destination slot to be in-bounds and walkable, and rejects zero or stock-vendor service IDs. |
| `level_generation.tsv` | `minimum_level`, `maximum_level` | Native level-range monster density, multiplayer scaling, and trap-generation metadata. |
| `warps.tsv` | `warp_id` | Normalized level-trigger source and destination projections for the server-owned Warp spell, with optional stable `warp_symbol`, `required_quest_id`/`required_quest_symbol`, and `required_quest_completed` gates; endpoints must be in bounds and walkable. |
| `portals.tsv` | `portal_id` | Static source and destination level endpoints with optional stable `portal_symbol`, validated against the composed world geometry and walkability during activation. |
| `monsters.tsv` | `entity_id` | Encounter state, resistances, drops, and immutable capabilities such as `undead`, `animal`, and `knockback`; optional `drop_item_symbol` and `drop_unique_item_symbol` cross-check catalog-backed drops, and authored levels and positions are validated against active world bounds. |
| `unique_monsters.tsv` | `entity_id` | Native unique-monster identity bound to a matching unique encounter and preserved by server snapshots. |
| `monster_types.tsv` | `monster_id` | Native-compatible monster type identity and validated baseline level, health, damage, accuracy, intelligence, Normal resistance, optional Hell-specific resistance, capability, Diablo-boss identity, aggro, death-animation, experience, native AI-dispatch/admission metadata, and optional monster AI tuning. |
| `monster_missiles.tsv` | `native_ai`, `monster_id` | Validated native AI-to-missile identity, projectile behavior, ranged-AI branch, avoidance probability, optional monster-specific controller, optional `monster_id` selector (validated against active monster types), mapped `spell_id` with optional stable `spell_symbol` cross-check, optional `spawn_monster_id` (validated against active monster types), explicit `damage_type`, projectile speed/lifetime/impact-lifetime, intelligence-scaled lifetime terms, optional Acid puddle-duration/damage/impact-lifetime terms, optional mapped-charge admission tuning, optional ranged-avoidance distance/chance tuning, optional ranged-retreat distance/chance tuning, and optional monster-Inferno admission tuning; checked-in `Arrow` rows select `MonsterArrow`, while legacy `Targeted` rows remain compatible. |
| `objects.tsv` | `entity_id` | World-object placement (validated against active level bounds), optional stable `object_symbol` and `quest_symbol` links, effects (including ordered `QuestSequence` and `QuestItemCompletion` interactions), linked-quest progress amounts, missile collision, breakage, and optional `trap_armed` state. `QuestItemCompletion` stores the required normalized item ID in `effect_amount` with optional `effect_item_symbol` cross-checking; other effects retain numeric amounts. |
| `quests.tsv` | `quest_id` | Level-scoped progress requirements, optional stable `quest_symbol`, native initial/completed state, quest-log state, optional `_qvar1`/`_qvar2`/`_qmsg` values, optional dialogue/completion transition variables, optional follow-on quest activation with `completion_activate_quest_symbol` (validated against the composed quest catalog), optional `completion_reward_item_symbol` (validated against the active item catalog), optional `completion_monster_id` (validated against active monster types), optional native Diablo-boss completion, and optional single-player pool exclusion state. |
| `quest_states.tsv` | `quest_id` | Additive variant-specific quest state rows merged with the base quest catalog; optional `quest_symbol`, `completion_activate_quest_symbol`, and `completion_reward_item_symbol` resolve through `content_ids.tsv`, and the table uses the same native lifecycle, progress, variable, message, dialogue/completion-transition, follow-on activation, completion-trigger, and optional completion-reward item fields. |
| `quest_types.tsv` | `quest_id` | Native-compatible quest identity with optional stable `quest_symbol`, routing levels, set-level source, and single-player availability. |
| `quest_pools.tsv` | `pool_id`, `quest_id` | Native single-player quest-randomization pools with optional stable `quest_symbol` cross-checks. |
| `set_level_names.tsv` | `set_level_id` | Native set-level identities and presentation names, including arena names. |
| `spells.tsv` | `spell_id` | Directly castable spell resources with optional stable `spell_symbol` cross-check, stable/native identity mapping, typed projectile behavior, scaling, status, damage rules, optional Jester dispatch tables with `jester_spell_symbols`, and Golem summon formula terms. |
| `items.tsv` | `item_id` | Normalized item identity with optional stable `item_symbol` (required by shipped base and Hellfire definitions and validated against `content_ids.tsv`), explicit spell-source kind and spell identity with optional stable `spell_symbol` cross-check (validated against active `spells.tsv`), validated presentation names and optional cursor symbol, compatibility projection, placement, charges, armor-range metadata, symbolic ordinary and Hellfire special-effect metadata, drop-level metadata, weighted drop rate, usable-item metadata, and generation inputs. |
| `item_quest_effects.tsv` | `item_id`, `quest_id`, `effect_kind` | Validated server-owned quest transitions applied when an item is picked up, with optional `item_symbol` and `quest_symbol`, including native quest activation, completion, and `_qvar1` updates. |
| `item_cursor_graphics.tsv` | `symbol` | Validated legacy item cursor symbols and their native byte-sized graphic IDs, shared by normalized and legacy item loaders. |
| `item_affixes.tsv` | `name`, `is_suffix` | Normalized prefix/suffix definitions and deterministic affix effects. |
| `item_generation.tsv` | singleton | Item-generation bounds, quality rolls, and native-compatible random selection rules. |
| `unique_items.tsv` | `unique_id` | Normalized unique-item definitions with optional stable `unique_symbol`, base-item references and optional `base_item_symbol` validated against the active item catalog (both symbols required by shipped base and Hellfire rows), authoritative presentation names, optional cursor symbols, and fixed effects. |
| `stores.tsv` | `store_id`, `store_slot` | Deterministic vendor inventory and item-generation references, with optional stable `store_symbol`, `item_symbol`, `unique_item_symbol`, and `spell_symbol` cross-checks validated against active store, item, unique-item, and spell catalogs. |
| `store_services.tsv` | `key`, `spell_id`, `spell_symbol` | Service pricing and spell-staff costs, with optional stable `spell_symbol` cross-checks and non-zero spell identities validated against active `spells.tsv`; omitted keys use module defaults. |
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
      "version": "1.231.0"
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
`devilution.diablo@1.231.0`. Manifests without compatibility metadata retain their
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

Schema 1.81 adds the validated `monster_missiles.tsv` table. Each native AI
identity maps to its retained native missile ID and authoritative projectile
behavior. The checked-in `Arrow` rows explicitly select the server-owned
`MonsterArrow` lifecycle; older overlays that declare `Targeted` for those rows
remain accepted as a compatibility spelling. Schema 1.82 additionally externalizes the
native ranged-AI branch and avoidance probability columns. Schema 1.83 adds
monster-specific mapping rows. Schema 1.84 adds the Counselor missile rows
and the monster-owned Charged Bolt behavior. The checked-in base pack also
admits native `Zhar` and `Lazarus` aliases through validated Fireball rows;
their runtime controller remains the server-owned Counselor boundary. Schema
1.85 adds Mega's
monster-owned Inferno controller and persistent segment path. Schema 1.86
externalizes the Hork Demon native AI identity and its validated Hork Spawn
summon admission. Schema 1.87 adds the validated Hork Spawn missile mapping,
speed-8 nine-tick trajectory, and expiry summon controller. Schema 1.88 adds
the additive Hellfire `quest_states.tsv` catalog, merging native initial quest
state with the shared authoritative quest catalog. The same table contract
now carries four base unique-defeat rows for Gharbad, Zhar, Warlord, and
Lazarus. Schema 1.89 adds the
Hellfire Defiler encounter and its validated Cathedral Map reward. Schema 1.90
adds `unique_monsters.tsv` and preserves unique identities across the protocol
and server-owned save boundary. Schema 1.91 adds the optional `spell_id` to
`monster_missiles.tsv`, moving mapped monster projectile spell identities into
validated content while preserving legacy inference for older overlays. Schema
1.92 adds the optional `spawn_monster_id` to `monster_missiles.tsv`; Hork Spawn
now selects its summoned native monster from the Hellfire row. Current
activation requires Hork Spawn mappings to provide that projection, so stale
rows without it are rejected instead of falling back to a processor-native ID.
Schema 1.93 adds optional
`projectile_speed` and `projectile_lifetime` columns; the Hellfire Hork Spawn
row supplies its native speed `8` and nine-tick lifetime, and migrated ranged
rows may override their server-compatible movement and lifetime values. Schema
1.94 adds optional `projectile_lifetime_base` and
`projectile_lifetime_intelligence_scale` columns for Acid's
intelligence-scaled lifetime formula. Schema 1.95 adds optional
`puddle_duration_random_range`, `puddle_duration_base`, and
`puddle_duration_intelligence_scale` columns for Acid's native puddle lifetime.
Schema 1.96 adds optional `puddle_damage_minimum_monster_level`,
`puddle_damage_low`, and `puddle_damage_high` columns for Acid's native puddle
damage threshold and values.
Schema 1.97 adds the optional `damage_type` column for mapped monster missiles;
checked-in damaging rows now declare their authoritative channel, while legacy
rows retain spell/name inference during migration.
Schema 1.98 adds the optional `mana_spell_percent` class column; checked-in
classes now own the native Mana spell multiplier, while legacy class rows retain
the 100-percent compatibility default.
Schema 1.99 adds the optional `melee_armor_reduction_divisor` class column;
checked-in classes now own the native Hellfire melee armor reduction rule, while
legacy class rows retain the disabled-rule compatibility default.
Schema 1.100 adds the optional `jester_spell_ids` spell column; the shipped
Jester row now owns its ten-way native dispatch table, while legacy rows retain
the compatibility table.
Schema 1.101 adds the `FastBlock` and `HeavyHitThreshold` values to the
validated `class_flags` vocabulary; the shipped Monk and Barbarian rows now
declare those native combat traits, while older rows remain valid without them.
Schema 1.102 adds the `physical_damage_formula` class column; shipped class
rows now select their native equipped-damage formula through validated content,
while legacy tables retain symbol-based loader inference for compatibility.
Schema 1.103 adds the two Monk unarmed damage level-divisor columns; legacy
1.102 and older class schemas retain the native defaults.
Schema 1.104 adds the optional Fallen fear tuning columns to `monster_types.tsv`;
checked-in Fallen rows own their radius and countdown values while older rows
retain the compatibility formula. Complete positive tuples are selected
independently of the `native_ai` label.
Schema 1.105 adds the optional Sneak tuning columns to `monster_types.tsv`;
checked-in Sneak rows own their visibility and retreat values while older rows
retain the compatibility formula. Complete positive tuples are selected
independently of the `native_ai` label.
Schema 1.106 adds the optional Bat attack-band columns to `monster_types.tsv`;
checked-in Bat rows own their adjacent attack chance while older rows retain
the compatibility formula. Complete positive tuples are selected independently
of the `native_ai` label.
Schema 1.107 adds the optional SkeletonMelee delay-band columns to
`monster_types.tsv`; checked-in SkeletonMelee rows own their movement delay
chance while older rows retain the compatibility formula.
Schema 1.108 adds the optional GoatMelee movement-band columns to
`monster_types.tsv`; checked-in GoatMelee rows own their movement admission
chance while older rows retain the compatibility formula.
Schema 1.109 adds the optional Fat movement-band columns to `monster_types.tsv`;
checked-in Fat rows own their movement admission chance while older rows retain
the compatibility formula. Complete positive tuples are selected independently
of the `native_ai` label.
Schema 1.110 adds the optional Zombie action-band columns to `monster_types.tsv`;
checked-in Zombie rows own their action admission chance while older rows retain
the compatibility formula. Complete positive tuples are selected independently
of the `native_ai` label.
Schema 1.111 adds the optional Zombie movement-distance columns to
`monster_types.tsv`; checked-in Zombie rows own their long-range threshold
while older rows retain the compatibility formula. Complete positive tuples are
selected independently of the `native_ai` label.
Schema 1.112 adds the optional Skeleton King summon-admission columns to
`monster_types.tsv`; checked-in SkeletonKing rows own their summon boundary
while older rows retain the compatibility formula.
Schema 1.113 adds the optional Hork Demon admission columns to
`monster_types.tsv`; checked-in HorkDemon rows own their adjacent attack,
Hork Spawn, and movement chance bands while older rows retain the compatibility
formula.
Schema 1.114 adds the optional Gargoyle safety-boundary columns to
`monster_types.tsv`; checked-in Gargoyle rows own their wounded retreat and
self-heal boundary while older rows retain the compatibility formula. Complete
positive tuples are selected independently of the `native_ai` label. Persisted
special-lock state is likewise accepted and advanced for any validated label
carrying the complete tuple.
Schema 1.115 adds the optional SkeletonRanged admission columns to
`monster_types.tsv`; checked-in SkeletonRanged rows own their retreat and
clear-lane arrow chance bands while older rows retain the compatibility formula.
Complete positive tuples are selected independently of the `native_ai` label.
Schema 1.116 adds the optional Rhino charge-admission columns to
`monster_types.tsv`; checked-in Rhino rows own their clear-lane charge boundary
while older rows retain the compatibility formula.
Schema 1.117 adds the optional mapped-charge admission columns to
`monster_missiles.tsv`; checked-in Bat `BatCharge` rows own their charge
boundary while older rows retain the compatibility formula.
Schema 1.118 extends the mapped-charge distance column to `SnakeCharge` rows;
the checked-in Snake `Rhino` row owns its clear two-tile admission while older
rows retain the native compatibility distance.
Schema 1.119 adds optional ranged-avoidance distance and chance columns to
`monster_missiles.tsv`; checked-in `RangedAvoidance` rows own their clear-lane
distance and 10,000-scale intelligence chance boundary while older rows retain
the native compatibility values.
Schema 1.120 adds optional ranged-retreat distance and chance columns to
`monster_missiles.tsv`; checked-in `Ranged` rows own their close-range retreat
boundary (`70 + 10 * intelligence` inside four tiles) while older rows retain
the native compatibility values.
Schema 1.121 adds optional monster-Inferno admission columns to
`monster_missiles.tsv`; the checked-in `Mega` row owns its special maximum
distance, adjacent/ranged intelligence bands, far-distance offset, and adjacent
choice modulo while older rows retain the native compatibility values.
Schema 1.122 adds optional SkeletonMelee adjacent attack-band columns to
`monster_types.tsv`; checked-in SkeletonMelee rows own their `20 + 2 *
intelligence` admission while older rows retain the native compatibility values.
Complete positive tuples are selected independently of the `native_ai` label.
Schema 1.123 adds optional GoatMelee adjacent attack-band columns to
`monster_types.tsv`; checked-in GoatMelee rows own their `23 + 2 * intelligence`
admission while older rows retain the native compatibility values. Complete
positive tuples are selected independently of the `native_ai` label.
Schema 1.124 adds optional Fat/Overlord adjacent attack and special-band columns
to `monster_types.tsv`; checked-in Fat rows own their `20 + 4 * intelligence`
admission and `15 + 4 * intelligence` special lower bound while older rows
retain the native compatibility values. Complete positive tuples are selected
independently of the `native_ai` label.
Schema 1.125 adds optional Zombie random-direction chance columns to
`monster_types.tsv`; checked-in Zombie rows own their `20 + 2 * intelligence`
movement-prepend chance while older rows retain the native compatibility values.
Complete positive tuples are selected independently of the `native_ai` label.
The active Snake `Rhino` and Bat `Rhino` missile rows also supply the mapped
charge projectile speed and lifetime through the existing validated projectile
columns; older rows retain the native speed-18, lifetime-256 defaults.
Schema 1.126 records this activation of the existing projectile columns for
mapped charges.
Schema 1.127 adds optional Gargoyle healing-formula columns to
`monster_types.tsv`; checked-in Gargoyle rows own the native divisor multiplier,
random-range modulus, and random offset (`16 * (seed % 5 + 4)`) while older rows
retain the compatibility formula. An omitted random offset retains the native
offset of four. Complete positive tuples are selected
independently of the `native_ai` label; persisted special-lock state is
accepted for any validated label carrying the tuple.
Schema 1.128 adds optional Scavenger healing/search columns to
`monster_types.tsv`; checked-in Scavenger rows own the corpse-eating duration,
search radius, Hellfire maximum-health divisor, base-game healing amount, and
base-game target-health percentage while older rows retain the compatibility
values. Complete positive tuples are selected independently of the `native_ai`
label. Persisted healing timers and corpse identities are likewise accepted
and advanced for any validated label carrying the complete tuple.
Schema 1.129 adds the optional Bat retreat-duration column to
`monster_types.tsv`; checked-in Bat rows own the native two-tick post-hit retreat
while older rows retain the compatibility duration. The validated duration is
selected independently of the `native_ai` label, and persisted retreat state is
accepted for any validated label carrying the tuple.
Schema 1.130 adds optional GoatMelee wounded-special columns to
`monster_types.tsv`; checked-in GoatMelee rows own the native 50-percent
health threshold and two-group deterministic gate while older rows retain the
compatibility values. Complete positive tuples are selected independently of
the `native_ai` label.
Schema 1.131 adds the optional Scavenger wounded-health threshold column to
`monster_types.tsv`; checked-in Scavenger rows own the native 50-percent corpse
seeking threshold while older rows retain the compatibility value. The
validated threshold is selected independently of the `native_ai` label.
Schema 1.132 adds the optional Gargoyle wounded-health threshold column to
`monster_types.tsv`; checked-in Gargoyle rows own the native 50-percent
self-healing threshold while older rows retain the compatibility value. The
validated threshold is selected independently of the `native_ai` label.
Schema 1.133 adds the optional Mana restoration random-range columns to
`spells.tsv`; the checked-in Mana row owns the native 10, 4, and 6 ranges while
older rows retain the compatibility values.
Schema 1.134 adds the optional Rage minimum-life admission threshold to
`spells.tsv`; the checked-in Rage row owns the native 64-Q6-per-level boundary
while older rows retain the compatibility value.
Schema 1.135 adds the optional Reflect default spell level to `spells.tsv`; the
checked-in Reflect row owns the native level-2 fallback while older rows retain
the compatibility value.
Schema 1.136 adds the optional Berserk magic-resistance frequency to
`spells.tsv`; the checked-in Berserk row owns the native 1-in-2 roll while older
rows retain the compatibility value.
Schema 1.137 adds the optional Charged Bolt initial path random range to
`spells.tsv`; the checked-in Charged Bolt row owns the native 1–15 range while
older rows retain the compatibility value.

Schema 1.138 adds the optional `repair_ability_durability_level_offset`
class column to `classes.tsv`; the checked-in Warrior row owns the native
offset `9` while older class schemas retain the compatibility offset.
Schema 1.139 adds the optional monster hit-chance floor columns to
`combat.tsv`; checked-in combat rows own the native ordinary and level-specific
floors while older rows retain the compatibility values.
Schema 1.140 adds the optional monster accuracy formula columns to
`combat.tsv`; checked-in combat rows own the native level, base, and ranged
distance terms while older rows retain the compatibility values.
Schema 1.141 adds the optional equipment attack-effect tuning columns to
`combat.tsv`; checked-in combat rows own the native random life-steal divisor
and 3/5-percent life/mana channels while older rows retain the compatibility
values.
Schema 1.142 adds the optional Hellfire class-armor bonus columns to
`combat.tsv`; checked-in combat rows own the native demon/undead adjustments
while older rows retain the compatibility values.
Schema 1.143 adds the optional semantic `ability_kind` column to
`class_loadouts.tsv`; checked-in loadouts identify each native class skill for
typed client presentation while legacy loadout tables retain their numeric-ID
compatibility behavior.

Schema 1.144 adds the optional `spell_source_kind` column to normalized
`items.tsv`; checked-in item rows explicitly classify charged and consumable
sources, while legacy item tables retain native miscellaneous-ID fallback until
they are normalized.
Schema 1.145 adds the optional `consumable_behavior` column to `spells.tsv`;
the checked-in Rune of Fire row selects `RuneFire`, while older spell tables
retain miscellaneous-ID fallback.
Schema 1.146 adds the normalized equipment subtype tags to `items.tsv`; the
checked-in base and Hellfire rows declare their weapon, bow, shield, staff,
hand, helmet, body-armor, ring, amulet, TwoHanded, and Gold categories; base
rows also declare belt and armor categories, while older rows retain native
item-type compatibility behavior.
The authoritative server centralizes these tag-first subtype decisions for
combat, armor, loadout, and monster rules; belt validation likewise uses the
validated `Belt` tag before the legacy equipment-location/item-class fallback.
Two-handed equipment validation likewise uses the validated `TwoHanded` tag
before the legacy `equip_location` fallback. Direct legacy states still use
their native metadata fallback. Loadout slot admission uses the semantic hand,
helmet, body-armor, ring, and amulet tags before the same location fallback.
Gold identity
likewise uses the validated `Gold` or `Currency` tag through this seam before the
native type/index fallback.

Schema 1.147 adds optional `name` and `identified_name` presentation metadata
to `items.tsv`. Generated item state carries these validated labels through the
existing server-owned item snapshot and protocol fields; an omitted identified
name uses the base name. Legacy `itemdat.tsv` rows map their native `name` and
`shortName` columns into the same fields.

Schema 1.148 adds `item_cursor_graphics.tsv` and the optional `cursor_graphic`
column to `items.tsv`. The catalog validates the native byte-sized graphic IDs
and resolves shared symbols for both normalized and legacy item tables; direct
callers that do not load the optional catalog retain the native compatibility
fallback.

Schema 1.149 projects the existing `name` field from `unique_items.tsv` and
`unique_itemdat.tsv` into both base and identified item-state names when a
unique item is generated. This keeps unique presentation server-owned without
adding a client-side name table.

Schema 1.150 adds the optional `cursor_graphic` column to `unique_items.tsv`.
Normalized and legacy unique rows resolve declared symbols through the shared
`item_cursor_graphics.tsv` catalog; an omitted symbol preserves the generated
base item's cursor, and direct callers without the optional catalog retain
their compatibility fallback.

Schema 1.151 adds the optional `min_monster_level` column to `items.tsv`.
Normalized and legacy item definitions now retain the native minimum monster
level used by random drop selection; omitted values remain zero for direct
compatibility callers.

Schema 1.152 adds the optional `drop_rate` column to `items.tsv`. The catalog
retains the native byte-sized weighted drop rate, including zero values that
exclude an item from random selection, for server-owned drop planning.

Schema 1.153 adds the optional `usable` column to `items.tsv`. When present,
the catalog applies the native iUsable flag to consumable item source admission;
legacy callers without the optional metadata retain their compatibility path.

Schema 1.154 adds the optional `min_armor_class` and `max_armor_class` columns
to `items.tsv`. When both columns are present, item generation rolls the
inclusive native armor range; omitted columns preserve the normalized fixed
`armor_class` fallback.

Schema 1.155 adds the optional `special_effects` column to `items.tsv`. Its
comma-separated native effect names map to authoritative equipment flags and
are validated during activation. Numeric `flags` remain additive, while legacy
`specialEffects` parsing keeps its compatibility fallback for unknown names.

Schema 1.156 adds the optional `hellfire_effects` column to `items.tsv`. Its
validated symbolic names map to native Hellfire damage/armor effects including
Devastation, Decay, Peril, Jesters, Doppelganger, and class-specific armor
bonuses. Legacy unique and affix effect paths remain supported.

Schema 1.157 adds the optional `teleport_search_radius` column to `spells.tsv`.
Teleport uses this validated crawl radius for both destination admission and
the authoritative movement-controller tick; omitted values retain five tiles.

Schema 1.158 adds the optional `warp_search_radius` column to `spells.tsv`.
Warp uses this validated crawl radius for both cast admission and world-
transition application; omitted values retain five tiles for direct legacy
world-command callers.

Schema 1.159 adds the optional `town_portal_search_radius` and
`town_portal_lifetime` columns to `spells.tsv`. Town Portal uses the validated
crawl radius and expiry ticks; omitted values retain five tiles and 100 ticks
for direct legacy world-command callers.

Schema 1.160 adds optional monster magic-projectile accuracy columns to
`combat.tsv`. Monster-owned magic projectiles now use validated base, level,
and distance terms for player-hit admission; omitted values retain the native
10/2/2/40/2 compatibility formula.

Schema 1.161 adds the optional `monster_charge_base_to_hit` column to
`combat.tsv`. Snake charge special-hit admission now uses the validated native
base to-hit value with the shared monster level, base-bonus, armor, and
level-floor terms; omitted values retain the native 500 compatibility value.

Schema 1.162 adds optional `monster_acid_puddle_minimum_hit_chance` and
`monster_acid_puddle_base_hit_chance` columns to `combat.tsv`. Persistent Acid
Puddle player-hit admission now uses validated level floors and base-hit terms;
omitted values retain the native 10/40 compatibility values.

Schema 1.163 adds the optional `projectile_damage` column to
`monster_missiles.tsv`. Mapped monster projectiles use the validated fixed
impact value when present; omitted values retain behavior-specific/native
damage derivation. The checked-in Diablo Apocalypse mapping declares its native
40-point impact explicitly.

Schema 1.164 adds the optional `maximum_resistance` column to `combat.tsv`.
Active player elemental resistance calculations use the validated cap; omitted
values retain the native 75-percent default for compatibility callers.

Schema 1.165 adds the optional `maximum_attribute` column to `combat.tsv`.
Active player equipment recalculation now uses the validated primary-attribute
ceiling; omitted values retain the native 750 default for compatibility callers.

Schema 1.166 adds the optional `barbarian_weapon_strength_damage_divisor`
column to `combat.tsv`. Active Barbarian axe/mace damage scaling now uses the
validated divisor; omitted values retain the native 75 divisor for compatibility callers.

Schema 1.167 adds the optional `rogue_strength_dexterity_damage_divisor`
column to `combat.tsv`. Active Rogue physical damage scaling now uses the
validated divisor; omitted values retain the native 200 divisor for compatibility callers.

Schema 1.168 adds the optional `monk_staff_unarmed_damage_divisor` column to
`combat.tsv`. Active Monk staff/unarmed physical damage scaling now uses the
validated divisor; omitted values retain the native 150 divisor for compatibility callers.

Schema 1.169 adds the optional `monk_weapon_damage_divisor` column to
`combat.tsv`. Active Monk non-staff weapon physical damage scaling now uses the
validated divisor; omitted values retain the native 300 divisor for compatibility callers.

Schema 1.170 adds the optional `bard_sword_strength_dexterity_damage_divisor`
column to `combat.tsv`. Active Bard sword physical damage scaling now uses the
validated divisor; omitted values retain the native 150 divisor for compatibility callers.

Schema 1.171 adds the optional `bard_bow_strength_dexterity_damage_divisor`
column to `combat.tsv`. Active Bard bow physical damage scaling now uses the
validated divisor; omitted values retain the native 250 divisor for compatibility callers.

Schema 1.172 adds the optional `bard_default_strength_damage_divisor` column
to `combat.tsv`. Active Bard fallback physical damage scaling now uses the
validated divisor; omitted values retain the native 100 divisor for compatibility callers.

Schema 1.173 adds the optional `standard_strength_damage_divisor` column to
`combat.tsv`. Active standard-class fallback physical damage scaling now uses
the validated divisor; omitted values retain the native 100 divisor for compatibility callers.

Schema 1.174 adds the optional `barbarian_bow_strength_damage_divisor` column
to `combat.tsv`. Active Barbarian bow physical damage scaling now uses the
validated divisor; omitted values retain the native 300 divisor for compatibility callers.

Schema 1.175 adds the optional `barbarian_default_strength_damage_divisor`
column to `combat.tsv`. Active Barbarian fallback physical damage scaling now
uses the validated divisor; omitted values retain the native 100 divisor for compatibility callers.

Schema 1.176 adds the optional `barbarian_vitality_damage_divisor` column to
`combat.tsv`. Active Barbarian weaponless physical damage scaling now uses the
validated divisor; omitted values retain the native 100 divisor for compatibility callers.

Schema 1.177 adds the optional `thorns_damage_minimum` and
`thorns_damage_maximum` columns to `combat.tsv`. Active Thorns retaliation now
uses the validated damage range; omitted values retain the native 1–3 range for compatibility callers.

Schema 1.178 adds the optional `hellfire_decay_damage_reduction` and
`hellfire_decay_removal_threshold` columns to `combat.tsv`. Active Hellfire
Decay now uses the validated decrement and removal threshold; omitted values retain the native 5/-100 behavior for compatibility callers.

Schema 1.179 adds the optional `hellfire_devastation_chance_percent` and
`hellfire_devastation_damage_multiplier` columns to `combat.tsv`. Active
Hellfire Devastation now uses the validated proc chance and multiplier; omitted values retain the native 5%/3× behavior for compatibility callers.

Schema 1.184 adds the optional `critical_strike_chance_per_level_percent`
column to `classes.tsv`. Active class Critical Strike now uses the validated
per-level proc chance; omitted values retain the native 1% per character level
behavior.

Schema 1.185 adds the optional `iron_skin_level_divisor` column to
`classes.tsv`. Active IronSkin armor now uses the validated class divisor;
omitted values retain the native level-divided-by-four behavior.

Schema 1.186 adds the optional `natural_resistance_per_level_percent` and
`heavy_hit_threshold_bonus_percent` columns to `classes.tsv`. Active class
defense now uses the validated percentages; omitted values retain the native
100% and 25% behavior.

Schema 1.187 adds the optional `pvp_block_level_scale` column to `combat.tsv`.
Active player-versus-player block chance now uses the validated scale; omitted
values retain the native scale of two.

Schema 1.188 adds the optional `fast_block_frame_skip` column to `classes.tsv`.
Active FastBlock staff timing now uses the validated class value; omitted values
retain the native two-frame behavior.

Schema 1.189 adds the optional `equipment_fast_block_frame_skip` column to
`combat.tsv`. Equipment FastBlock timing now uses the validated combat value;
omitted values retain the native two-frame behavior.

Schema 1.190 adds the optional hit-recovery tier skip columns to `combat.tsv`.
Active Fast/Faster/Fastest Hit Recovery timing now uses the validated ordered
values; omitted values retain the native 1/2/3 behavior.

Schema 1.191 adds the optional Quick/Fast/Faster/Fastest melee attack-speed
skip columns to `combat.tsv`. Active melee timing now uses validated skips for
both first-frame and subsequent-frame paths; omitted values retain the native
1/0, 2/1, 3/2, and 4/2 behavior.

Schema 1.192 adds optional Golem summon formula columns to `spells.tsv`.
The Golem row externalizes health level/mana terms, damage level/offset terms,
and armor class; omitted values retain the native health 2/3, damage 2/4/8,
and armor 25 behavior.

Schema 1.193 adds the optional `golem_hit_points_level_base_q6` column to the
Golem row in `spells.tsv`; omitted values retain the native 320 Q6 per-level
health term.

Schema 1.194 adds the optional `reflect_damage_percent_minimum` and
`reflect_damage_percent_maximum` columns to the Reflect row in `spells.tsv`;
omitted values retain the native 20–30 percent reflected-damage roll.

Schema 1.195 adds the optional Town Portal destination slot columns to
`spells.tsv`; omitted values retain the native 57/40 destination origin,
four slots, and two-tile spacing.

Schema 1.196 adds the optional Charged Bolt magic-hit tuning columns to
`spells.tsv`; omitted values retain the native 2/5/95 penalty and chance
bounds.

Schema 1.197 adds the optional Holy Bolt magic-hit tuning columns to
`spells.tsv`; omitted values retain the native 2/5/95 penalty and chance
bounds.

Schema 1.198 adds the optional Firebolt magic-hit tuning columns to
`spells.tsv`; omitted values retain the native 2/5/95 penalty and chance
bounds.

Schema 1.199 adds the optional Bone Spirit magic-hit and current-life damage
tuning columns to `spells.tsv`; omitted values retain the native 2/5/95 hit
terms and one-third current-life damage rule.

Schema 1.200 adds the optional persistent-projectile magic-hit tuning columns
to `spells.tsv`; omitted values retain the native 2/5/95 bounds.

Schema 1.201 adds the optional persistent-projectile player magic-hit tuning
columns to `spells.tsv`; omitted values retain the native 2/5/95 bounds.

Schema 1.202 adds the optional persistent-projectile player damage divisor to
`spells.tsv`; omitted values retain the native divisor of two.

Schema 1.203 adds optional direct player-magic hit tuning to `combat.tsv`;
omitted values retain the native 2/5/95 character-level penalty and chance
bounds.

Schema 1.204 adds the optional Bone Spirit player damage divisor to `spells.tsv`;
omitted values retain the native one-third current-life rule.

Schema 1.205 adds the optional monster regeneration level divisor to
`combat.tsv`; omitted values retain the native `level / 2` recovery rule.

Schema 1.206 adds the optional monster-player block level scale to
`combat.tsv`; omitted values retain the native level-difference scale of two.

Schema 1.207 adds the optional Acid puddle impact lifetime to
`monster_missiles.tsv`; omitted values retain the native two-tick impact phase.

Schema 1.208 adds the optional mapped monster projectile impact lifetime to
`monster_missiles.tsv`; omitted values retain the native one-tick impact phase.

Schema 1.212 adds optional Hell-specific resistance columns to
`monster_types.tsv`; omitted values retain the ordinary resistance channels,
and active Hell materialization selects the validated native `resistanceHell`
projection.

Schema 1.213 adds optional native difficulty tuning columns to `combat.tsv`;
omitted values retain the native Nightmare/Hell level, accuracy, armor, damage,
experience, and initial-health formulas, including Nightmare `2 * (base + 2)`
and Hell `4 * base + 6` monster damage.

Schema 1.183 adds the optional `critical_damage_multiplier` column to
`classes.tsv`. Active class Critical Strike now uses the validated multiplier;
omitted values retain the native 2× behavior for compatibility callers.

Schema 1.182 adds the optional `triple_demon_damage_multiplier` column to
`combat.tsv`. Active Triple Demon Damage now uses the validated multiplier;
omitted values retain the native 3× behavior for compatibility callers.

Schema 1.181 adds the optional `hellfire_peril_damage_multiplier` and
`hellfire_doppelganger_chance_percent` columns to `combat.tsv`. Active
Hellfire Peril and Doppelganger now use validated native terms; omitted values
retain the native 2× and 10% behavior for compatibility callers.

Schema 1.180 adds the optional `hellfire_jesters_roll_maximum`,
`hellfire_jesters_expansion_threshold`, and
`hellfire_jesters_expansion_multiplier` columns to `combat.tsv`. Active
Hellfire Jesters now uses the validated roll and upper-half expansion terms;
omitted values retain the native 0–200/100/5 behavior for compatibility callers.

Changes to authoritative content can change deterministic results. Update or
add loader tests and shared replay fixtures whenever a content change affects
state transitions or canonical hashes. Client presentation data may mirror
layout or labels, but it must not become an independent gameplay definition.
Schema 1.214 adds optional ranged-attack frame timing columns to `combat.tsv`.
The base-game defaults preserve the native first-frame and Fast Attack skips;
the Hellfire pack sets all three columns to zero to preserve its native timing.
Schema 1.215 adds optional `completion_reward_item_id` and
`completion_reward_item_level` columns to `quest_states.tsv`. Completion rewards
are generated by the authoritative server at the quest's native monster-defeat
position; the Hellfire Defiler's Cathedral Map is the first migrated reward.
Schema 1.216 adds the `QuestSequence` `effect_kind` for ordered quest objects;
the Hellfire Na-Krul tome rows use it to preserve native sequence state.
Schema 1.217 adds `item_combinations.tsv`, a validated server-owned mapping
from required normalized item IDs to a generated result item. The base pack
uses it for the Hellfire Na-Krul note fragments; combinations run after an
authoritative pickup and never depend on client-side inventory logic.
Schema 1.218 adds `item_quest_effects.tsv`, a validated server-owned mapping
from normalized item identities to quest transitions at pickup. The Hellfire
Cathedral Map activates Grave Matters and sets its native first quest variable.
Schema 1.219 adds the `QuestItemCompletion` object effect. The Hellfire grave
object consumes the server-owned Cathedral Map and completes Grave Matters
through the existing authoritative object-interaction path.
Schema 1.220 adds optional quest gates to `warps.tsv`; the Hellfire crypt
entrance is now a server-owned trigger that remains closed until Grave Matters
is completed.
Schema 1.231 separates class capabilities into the stable `ability` namespace
and assigns the shipped Holy Bolt row the stable `spell:holy_bolt` symbol;
legacy class-loadout symbols still fall back to the `spell` namespace.
Schema 1.232 adds optional `quest_symbol` columns to quest type, state, and
pool tables. Activated rows resolve the `quest` namespace and reject numeric
and symbolic mismatches while older numeric-only rows remain compatible.
Schema 1.233 extends optional quest symbols to warp gates, linked objects, item
quest effects, and follow-on quest activation.
Schema 1.230 adds optional `spell_symbol` to direct spell rows in `stores.tsv`.
Activated rows resolve the stable spell namespace and reject numeric and
symbolic mismatches; older rows retain numeric-ID fallback.
Schema 1.229 adds optional `jester_spell_symbols` to `spells.tsv`. Jester
dispatch entries resolve through the stable spell namespace and reject numeric
and symbolic mismatches; older rows retain numeric-ID fallback.
Schema 1.228 adds optional `spell_symbol` to `store_services.tsv`. Spell-staff
pricing rows resolve the symbol through the stable spell namespace and reject
numeric and symbolic mismatches; older rows retain numeric-ID fallback.
Schema 1.227 adds optional `spell_symbol` to `spells.tsv`. Active spell rows
resolve the symbol through the stable spell namespace and reject numeric and
symbolic mismatches; older rows retain numeric-ID fallback.
Schema 1.226 adds optional `spell_symbol` to `items.tsv`. Active item rows
resolve the symbol through the stable spell namespace and reject numeric and
symbolic mismatches; older rows retain numeric-ID fallback.
Schema 1.225 adds optional `spell_symbol` to `monster_missiles.tsv`. Active
rows resolve the symbol through the stable spell namespace and reject numeric
and symbolic mismatches; older rows retain numeric-ID fallback.
Schema 1.224 adds optional `ability_spell_symbol` and `starting_spell_symbol`
columns to `class_loadouts.tsv`. Active packs resolve capability symbols through
the stable ability namespace and starting-spell symbols through the spell
namespace, rejecting numeric/symbol mismatches; older loadout schemas
retain numeric-ID compatibility. Schema 1.223 adds Town Portal destination geometry columns to
`world_rules.tsv`; active packs now own the destination slot layout used by
the authoritative cast path, and activation validates every slot against the
town level's bounds and walkability. Schema 1.222 adds the `adria_store_id` column to
the same table. It
externalizes the service-only store identity used by Adria's mana action;
activation rejects zero or stock-vendor collisions. The checked-in base row
retains native store `10`, while packs without the table retain the legacy
default. Schema 1.221 introduced the table and its externalized town-level
identity used by Town Portal and Warp admission. Packs without the new geometry
columns retain the spell-row destination tuning for compatibility.

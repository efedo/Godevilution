# Godevilution Changelog

All notable changes to this fork are documented below.

## 2026-08-13

- Extend the authoritative reconnect soak to twelve consecutive resume cycles
  while a second participant remains connected, checking participant ordering
  and canonical snapshot hashes on every resumed session.
- Add an eight-cycle live Godot-client reconnect soak against the v0.2
  authoritative server, verifying the resumed session and the still-connected
  participant's self-first snapshots.
- Adopt protocol v0.2 as the production default for the C# server, Godot
  launcher, and native authoritative launcher, while retaining explicit v0.1
  compatibility selection.
- Add protocol v0.2 independent content-manifest and gameplay-ruleset
  handshake identities while preserving the v0.1 combined-hash alias.
- Enforce the supplied ruleset identity during the Godot client handshake, so
  a client cannot accept a server with incompatible gameplay modules.
- Align native replay canonical hashing with the C# snapshot contract for
  projectile controller runtime state, object/quest extensions, and deferred
  attack RNG consumption; the native replay gate now passes all 65 tests.
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
  Etherealize identity `53` (`native SpellID` `26`).
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
  identity mapping. `spells.tsv` now records `native_spell_id`, rejects duplicate
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
- Move 12 theme/trigger/crypt scalar globals (`numthemes`, `armorFlag`, `weaponFlag`, `zharlib`, `trigflag`, `numtrigs`, `TWarpFrom`, `UberRow`, `UberCol`, `IsUberRoomOpened`, `IsUberLeverActivated`, `UberDiabloMonsterIndex`) into `Level` class with inline accessors
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

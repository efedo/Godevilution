# Player class data

There is one folder per class.

### attributes.tsv

 Attribute         | Description
------------------:|--------------------------------------
 `baseStr`         | Class Starting Strength Stat, uint8_t
 `baseMag`         | Class Starting Magic Stat, uint8_t
 `baseDex`         | Class Starting Dexterity Stat, uint8_t
 `baseVit`         | Class Starting Vitality Stat, uint8_t
 `maxStr`          | Class Maximum Strength Stat, uint8_t
 `maxMag`          | Class Maximum Magic Stat, uint8_t
 `maxDex`          | Class Maximum Dexterity Stat, uint8_t
 `maxVit`          | Class Maximum Vitality Stat, uint8_t
 `blockBonus`      | Class Block Bonus, %
 `adjLife`         | Class Life Adjustment, decimal
 `adjMana`         | Class Mana Adjustment, decimal
 `lvlLife`         | Life gained on level up, decimal
 `lvlMana`         | Mana gained on level up, decimal
 `chrLife`         | Life from base Vitality, decimal
 `chrMana`         | Mana from base Magic, decimal
 `itmLife`         | Life from item bonus Vitality, decimal
 `itmMana`         | Mana from item bonus Magic, decimal
 `baseMagicToHit`  | Starting chance to hit with spells/scrolls, %
 `baseMeleeToHit`  | Starting chance to hit with melee weapons/fists, %
 `baseRangedToHit` | Starting chance to hit with ranged weapons, %

### animations.tsv

Animation frame counts are native compatibility data. The authoritative C#
content pack mirrors the complete six-class table in `class_animations.tsv`,
including weapon-specific attack/action pairs plus walk, block, death, cast,
recovery, and town timing. The C# simulation consumes block/recovery timing now;
the remaining values are validated inputs for command-driven action lifecycles
as those transitions move off the native engine.

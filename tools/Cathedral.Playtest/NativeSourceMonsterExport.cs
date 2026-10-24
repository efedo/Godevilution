using Devilution.Data;
using Devilution.Diablo;

namespace Godevilution.Tools;

/**
 * Cathedral monster, unique-monster and object rows from open txtdata,
 * ported from legacy-native/tables/monstdat.cpp and objdat.cpp.
 * The name tables at the end are copied from the native enums.
 */
public static class NativeSourceMonsterExport
{
    // UniqueMonstersData rows reserved for quests (monsters.cpp IsQuestOnlyUnique).
    private static readonly int[] QuestOnlyUniques = [0, 2, 3, 7, 8];

    /** LoadMonstDat: rows fill their _monster_id slot; the remaining default slots stay Never/Invalid. */
    public static IReadOnlyList<AuthoritativeCathedralRawMonsterDefinition> Monsters(string textDataRoot)
    {
        var slots = new AuthoritativeCathedralRawMonsterDefinition?[MonsterIds.Length];
        foreach (var row in NativeSourceRuleExport.Load(textDataRoot, "monsters", "monstdat.tsv").Rows) {
            var id = Index(MonsterIds, row["_monster_id"], "_monster_id");
            slots[id] = new(id, (AuthoritativeCathedralMonsterAvailability)Lookup(Availabilities, row["availability"], "availability"),
                row.RequiredInt32("image"), row.RequiredInt32("minDunLvl"), row.RequiredInt32("maxDunLvl"),
                IsSkeleton(id), row.RequiredInt32("hitPointsMinimum"), row.RequiredInt32("hitPointsMaximum"),
                Index(AiIds, row["ai"], "ai"), row.RequiredInt32("intelligence"),
                (uint)Flags(MonsterFlags, row["abilityFlags"], "abilityFlags"),
                row.RequiredInt32("minDamage"), row.RequiredInt32("maxDamage"),
                row.RequiredInt32("minDamageSpecial"), row.RequiredInt32("maxDamageSpecial"),
                row.RequiredInt32("armorClass"), Flags(Resistances, row["resistance"], "resistance"),
                row.RequiredInt32("reducePlayerStrength"), row.RequiredInt32("reducePlayerMagic"),
                row.RequiredInt32("reducePlayerDexterity"), row.RequiredInt32("reducePlayerVitality"),
                row.RequiredInt32("reducePlayerMaxHP"), row.RequiredInt32("reducePlayerMaxMana"),
                row.RequiredBoolean("hasSpecial"), row.RequiredInt32("width"),
                Ints(row["frames[6]"]), Ints(row["rate[6]"]));
        }
        return slots.Select((slot, id) => slot ?? new(id, AuthoritativeCathedralMonsterAvailability.Never, 0, 0, 0,
            IsSkeleton(id), 0, 0, -1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 0, new int[6], new int[6])).ToArray();
    }

    /** MonstersData fields the runtime sidecar keeps; default slots are empty and zero. */
    public static AuthoritativeCathedralMonsterRuntimeCatalog Runtime(string textDataRoot)
    {
        var slots = new AuthoritativeCathedralMonsterRuntimeDefinition?[MonsterIds.Length];
        foreach (var row in NativeSourceRuleExport.Load(textDataRoot, "monsters", "monstdat.tsv").Rows) {
            var id = Index(MonsterIds, row["_monster_id"], "_monster_id");
            slots[id] = new(id, StableSymbol(id), row["name"], row.RequiredInt32("level"),
                row.RequiredInt32("toHit"), row.RequiredInt32("animFrameNum"),
                row.RequiredInt32("toHitSpecial"), row.RequiredInt32("animFrameNumSpecial"),
                Index(MonsterClasses, row["monsterClass"], "monsterClass"),
                Flags(SelectionRegions, row["selectionRegion"], "selectionRegion"),
                Lookup(Treasures, row["treasure"], "treasure"), row.RequiredInt32("exp"),
                Flags(Resistances, row["resistanceHell"], "resistanceHell"));
        }
        return new(slots.Select((slot, id) => slot ?? new(id, StableSymbol(id), string.Empty, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
    }

    private static string StableSymbol(int id) => $"NativeMonster_{id:000}";

    public static IReadOnlyList<AuthoritativeCathedralUniqueDefinition> UniqueMonsters(string textDataRoot)
    {
        var speech = NativeSourceRuleExport.Load(textDataRoot, "text", "textdat.tsv");
        var speechIds = speech.Rows.Select((row, index) => (Id: row.Values[speech.Columns[0]], Index: index))
            .ToDictionary(row => row.Id, row => row.Index, StringComparer.Ordinal);
        return NativeSourceRuleExport.Load(textDataRoot, "monsters", "unique_monstdat.tsv").Rows.Select((row, index) =>
            new AuthoritativeCathedralUniqueDefinition(index, Index(MonsterIds, row["type"], "type"),
                row.RequiredInt32("level"), row.RequiredInt32("maxHp"), Index(AiIds, row["ai"], "ai"),
                row.RequiredInt32("intelligence"), row.RequiredInt32("minDamage"), row.RequiredInt32("maxDamage"),
                Flags(Resistances, row["resistance"], "resistance"),
                row.RequiredInt32("reducePlayerStrength"), row.RequiredInt32("reducePlayerMagic"),
                row.RequiredInt32("reducePlayerDexterity"), row.RequiredInt32("reducePlayerVitality"),
                row.RequiredInt32("reducePlayerMaxHP"), row.RequiredInt32("reducePlayerMaxMana"),
                Enum.Parse<AuthoritativeCathedralUniquePack>(row["monsterPack"]),
                row.RequiredInt32("customToHit"), row.RequiredInt32("customArmorClass"),
                // ParseSpeechId: empty is TEXT_NONE; ids follow textdat.tsv row order.
                row["talkMessage"].Length == 0 ? -1 : Lookup(speechIds, row["talkMessage"], "talkMessage"),
                QuestOnlyUniques.Contains(index))).ToArray();
    }

    public static IReadOnlyList<AuthoritativeCathedralPopulationObjectDefinition> Objects(string textDataRoot)
        => NativeSourceRuleExport.Load(textDataRoot, "objects", "objdat.tsv").Rows.Select((row, index) =>
            new AuthoritativeCathedralPopulationObjectDefinition(index,
                (byte)Flags(ObjectFlags, row["flags"], "flags"),
                checked((byte)row.RequiredInt32("animDelay")), checked((byte)row.RequiredInt32("animLen")),
                checked((byte)row.RequiredInt32("animWidth")),
                Flags(SelectionRegions, row["selectionRegion"], "selectionRegion"))).ToArray();

    // monsters.cpp SkeletonTypes.
    private static bool IsSkeleton(int id)
        => MonsterIds[id] is "MT_WSKELAX" or "MT_TSKELAX" or "MT_RSKELAX" or "MT_XSKELAX"
            or "MT_WSKELBW" or "MT_TSKELBW" or "MT_RSKELBW" or "MT_XSKELBW"
            or "MT_WSKELSD" or "MT_TSKELSD" or "MT_RSKELSD" or "MT_XSKELSD";

    /** RecordReader::readIntArray: comma-separated, exactly six values. */
    private static int[] Ints(string value)
    {
        var values = value.Split(',').Select(int.Parse).ToArray();
        return values.Length == 6 ? values : throw new InvalidDataException($"Expected six values in '{value}'.");
    }

    private static int Index(string[] names, string value, string column)
    {
        var index = Array.IndexOf(names, value);
        return index >= 0 ? index : throw new InvalidDataException($"Unknown {column} value '{value}'.");
    }

    private static int Lookup(IReadOnlyDictionary<string, int> names, string value, string column)
        => names.TryGetValue(value, out var result) ? result
            : throw new InvalidDataException($"Unknown {column} value '{value}'.");

    private static int Flags(IReadOnlyDictionary<string, int> names, string value, string column)
        => value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Aggregate(0, (flags, name) => flags | Lookup(names, name, column));

    private static readonly Dictionary<string, int> Availabilities = new(StringComparer.Ordinal) {
        ["Never"] = 0, ["Always"] = 1, ["Retail"] = 2,
    };

    private static readonly Dictionary<string, int> MonsterFlags = new(StringComparer.Ordinal) {
        ["HIDDEN"] = 1 << 0, ["LOCK_ANIMATION"] = 1 << 1, ["ALLOW_SPECIAL"] = 1 << 2, ["TARGETS_MONSTER"] = 1 << 4,
        ["GOLEM"] = 1 << 5, ["QUEST_COMPLETE"] = 1 << 6, ["KNOCKBACK"] = 1 << 7, ["SEARCH"] = 1 << 8,
        ["CAN_OPEN_DOOR"] = 1 << 9, ["NO_ENEMY"] = 1 << 10, ["BERSERK"] = 1 << 11, ["NOLIFESTEAL"] = 1 << 12,
    };

    private static readonly Dictionary<string, int> Resistances = new(StringComparer.Ordinal) {
        ["RESIST_MAGIC"] = 1 << 0, ["RESIST_FIRE"] = 1 << 1, ["RESIST_LIGHTNING"] = 1 << 2, ["IMMUNE_MAGIC"] = 1 << 3,
        ["IMMUNE_FIRE"] = 1 << 4, ["IMMUNE_LIGHTNING"] = 1 << 5, ["IMMUNE_ACID"] = 1 << 7,
    };

    private static readonly string[] MonsterClasses = ["Undead", "Demon", "Animal"];

    // ParseMonsterTreasure: T_NODROP, or T_UNIQ plus a _unique_items index.
    private static readonly Dictionary<string, int> Treasures = new(StringComparer.Ordinal) {
        [""] = 0, ["None"] = 0x4000, ["Uniq(CLEAVER)"] = 0x8000 + 0, ["Uniq(SKCROWN)"] = 0x8000 + 1,
    };

    private static readonly Dictionary<string, int> ObjectFlags = new(StringComparer.Ordinal) {
        ["Animated"] = 1 << 0, ["Solid"] = 1 << 1, ["MissilesPassThrough"] = 1 << 2, ["Light"] = 1 << 3,
        ["Trap"] = 1 << 4, ["Breakable"] = 1 << 5,
    };

    private static readonly Dictionary<string, int> SelectionRegions = new(StringComparer.Ordinal) {
        ["Bottom"] = 1 << 0, ["Middle"] = 1 << 1, ["Top"] = 1 << 2,
    };

    // tables/monstdat.h _monster_id (NUM_DEFAULT_MTYPES slots) and MonsterAIID.
    private static readonly string[] MonsterIds = [
        "MT_NZOMBIE", "MT_BZOMBIE", "MT_GZOMBIE", "MT_YZOMBIE", "MT_RFALLSP", "MT_DFALLSP", "MT_YFALLSP", "MT_BFALLSP",
        "MT_WSKELAX", "MT_TSKELAX", "MT_RSKELAX", "MT_XSKELAX", "MT_RFALLSD", "MT_DFALLSD", "MT_YFALLSD", "MT_BFALLSD",
        "MT_NSCAV", "MT_BSCAV", "MT_WSCAV", "MT_YSCAV", "MT_WSKELBW", "MT_TSKELBW", "MT_RSKELBW", "MT_XSKELBW",
        "MT_WSKELSD", "MT_TSKELSD", "MT_RSKELSD", "MT_XSKELSD", "MT_INVILORD", "MT_SNEAK", "MT_STALKER", "MT_UNSEEN",
        "MT_ILLWEAV", "MT_LRDSAYTR", "MT_NGOATMC", "MT_BGOATMC", "MT_RGOATMC", "MT_GGOATMC", "MT_FIEND", "MT_BLINK",
        "MT_GLOOM", "MT_FAMILIAR", "MT_NGOATBW", "MT_BGOATBW", "MT_RGOATBW", "MT_GGOATBW", "MT_NACID", "MT_RACID",
        "MT_BACID", "MT_XACID", "MT_SKING", "MT_CLEAVER", "MT_FAT", "MT_MUDMAN", "MT_TOAD", "MT_FLAYED",
        "MT_WYRM", "MT_CAVSLUG", "MT_DVLWYRM", "MT_DEVOUR", "MT_NMAGMA", "MT_YMAGMA", "MT_BMAGMA", "MT_WMAGMA",
        "MT_HORNED", "MT_MUDRUN", "MT_FROSTC", "MT_OBLORD", "MT_BONEDMN", "MT_REDDTH", "MT_LTCHDMN", "MT_UDEDBLRG",
        "MT_INCIN", "MT_FLAMLRD", "MT_DOOMFIRE", "MT_HELLBURN", "MT_STORM", "MT_RSTORM", "MT_STORML", "MT_MAEL",
        "MT_BIGFALL", "MT_WINGED", "MT_GARGOYLE", "MT_BLOODCLW", "MT_DEATHW", "MT_MEGA", "MT_GUARD", "MT_VTEXLRD",
        "MT_BALROG", "MT_NSNAKE", "MT_RSNAKE", "MT_BSNAKE", "MT_GSNAKE", "MT_NBLACK", "MT_RTBLACK", "MT_BTBLACK",
        "MT_RBLACK", "MT_UNRAV", "MT_HOLOWONE", "MT_PAINMSTR", "MT_REALWEAV", "MT_SUCCUBUS", "MT_SNOWWICH", "MT_HLSPWN",
        "MT_SOLBRNR", "MT_COUNSLR", "MT_MAGISTR", "MT_CABALIST", "MT_ADVOCATE", "MT_GOLEM", "MT_DIABLO", "MT_DARKMAGE",
        "MT_HELLBOAR", "MT_STINGER", "MT_PSYCHORB", "MT_ARACHNON", "MT_FELLTWIN", "MT_HORKSPWN", "MT_VENMTAIL", "MT_NECRMORB",
        "MT_SPIDLORD", "MT_LASHWORM", "MT_TORCHANT", "MT_HORKDMN", "MT_DEFILER", "MT_GRAVEDIG", "MT_TOMBRAT", "MT_FIREBAT",
        "MT_SKLWING", "MT_LICH", "MT_CRYPTDMN", "MT_HELLBAT", "MT_BONEDEMN", "MT_ARCHLICH", "MT_BICLOPS", "MT_FLESTHNG",
        "MT_REAPER", "MT_NAKRUL",
    ];
    private static readonly string[] AiIds = [
        "Zombie", "Fat", "SkeletonMelee", "SkeletonRanged", "Scavenger", "Rhino", "GoatMelee", "GoatRanged",
        "Fallen", "Magma", "SkeletonKing", "Bat", "Gargoyle", "Butcher", "Succubus", "Sneak",
        "Storm", "FireMan", "Gharbad", "Acid", "AcidUnique", "Golem", "Zhar", "Snotspill",
        "Snake", "Counselor", "Mega", "Diablo", "Lazarus", "LazarusSuccubus", "Lachdanan", "Warlord",
        "FireBat", "Torchant", "HorkDemon", "Lich", "ArchLich", "Psychorb", "Necromorb", "BoneDemon",
    ];
}

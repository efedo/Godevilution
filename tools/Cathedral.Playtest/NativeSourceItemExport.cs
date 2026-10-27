using Devilution.Data;
using Devilution.Diablo;

namespace Godevilution.Tools;

/**
 * Cathedral item catalog from open txtdata and the bundled 12px font,
 * ported from legacy-native/tables/itemdat.cpp, spelldat.cpp and items.cpp.
 * The name tables at the end are copied from the native enums.
 */
public static class NativeSourceItemExport
{
    private const int NativeDefaultUniqueBaseItemCount = 70; // NUM_DEFAULT_UITYPES
    private const int NativeMaximumUniqueBaseItemCount = 127; // NUM_MAX_UITYPES
    private const int PlaceholderCursor = byte.MaxValue; // ICURS_DEFAULT

    public static AuthoritativeCathedralThemeItemCatalog Read(string assetsRoot)
    {
        var textData = Path.Combine(assetsRoot, "txtdata");
        var animations = CursorAnimations();
        var uniqueBaseItems = new Dictionary<string, int>(UniqueBaseItems, StringComparer.Ordinal);
        var items = NativeSourceRuleExport.Load(textData, "items", "itemdat.tsv").Rows
            .Select((row, index) => Item(row, index, uniqueBaseItems, animations)).ToArray();
        var uniques = NativeSourceRuleExport.Load(textData, "items", "unique_itemdat.tsv").Rows
            .Select((row, index) => Unique(row, index, uniqueBaseItems)).ToArray();
        var glyphs = GlyphWidths(Path.Combine(assetsRoot, "fonts"));
        var generation = NativeSourceRuleExport.Load(textData, "items", "item_generation.tsv").Rows[0];
        return new(items, Affixes(textData, "item_prefixes.tsv"), Affixes(textData, "item_suffixes.tsv"), uniques,
            Spells(textData), animations, glyphs,
            new(CursorGraphics["BOOK_RED"], CursorGraphics["BOOK_BLUE"], CursorGraphics["BOOK_GREY"],
                CursorGraphics["GOLD_SMALL"], CursorGraphics["GOLD_MEDIUM"], CursorGraphics["GOLD_LARGE"]),
            AlwaysMagicMisc(generation), Rules(generation), glyphs['?']);
    }

    /** items.cpp IsItemAvailable for retail (non-spawn) Diablo without the test-bard option. */
    public static bool IsAvailable(int index)
        => index != 22 // IDI_MAPOFDOOM
            && index != 32 // IDI_LGTFORGE
            && index is < 35 or > 47 // IDI_OIL..IDI_GREYSUIT (Hellfire)
            && index is < 83 or > 86 // oils
            && index != 92 // Scroll of Search
            && index is < 161 or > 165 // runes
            && index != 5; // IDI_SORCERER

    private static AuthoritativeCathedralNativeItemDefinition Item(TsvRow row, int index,
        Dictionary<string, int> uniqueBaseItems,
        IReadOnlyDictionary<int, AuthoritativeCathedralNativeAnimationDefinition> animations)
    {
        var dropRate = row.RequiredInt32("dropRate");
        var cursor = Cursor(row["cursorGraphic"]);
        // Non-dropping placeholder rows have no sprite.
        var animation = cursor == PlaceholderCursor && dropRate == 0 ? new AuthoritativeCathedralNativeAnimationDefinition(0)
            : animations.TryGetValue(cursor, out var value) ? value
            : throw new InvalidDataException($"itemdat.tsv row {index} has no cursor animation for {cursor}.");
        return new(index, UniqueBaseItem(row["uniqueBaseItem"], uniqueBaseItems, add: true), dropRate,
            Lookup(ItemClasses, row["class"], "class"), Lookup(EquipTypes, row["equipType"], "equipType"), cursor,
            Lookup(ItemTypes, row["itemType"], "itemType"), row["name"], row["shortName"],
            row.RequiredInt32("minMonsterLevel"), row.RequiredInt32("durability"),
            row.RequiredInt32("minDamage"), row.RequiredInt32("maxDamage"),
            row.RequiredInt32("minArmor"), row.RequiredInt32("maxArmor"),
            row.RequiredInt32("minStrength"), row.RequiredInt32("minMagic"), row.RequiredInt32("minDexterity"),
            unchecked((uint)Flags(SpecialEffects, row["specialEffects"], "specialEffects")),
            Lookup(MiscIds, row["miscId"], "miscId"), Lookup(SpellIds, row["spell"], "spell"),
            row.RequiredBoolean("usable"), row.RequiredInt32("value"),
            animation.FrameCount, animation.TicksPerFrame, IsAvailable(index));
    }

    private static AuthoritativeCathedralNativeUniqueDefinition Unique(TsvRow row, int index,
        Dictionary<string, int> uniqueBaseItems)
    {
        // LoadUniqueItemDatFromFile: powers stop at the first empty name; unused slots stay IPL_INVALID.
        var powers = new AuthoritativeCathedralNativeItemPower[6];
        var count = 0;
        for (; count < powers.Length && row[$"power{count}"].Length != 0; count++)
            powers[count] = Power(row, $"power{count}");
        for (var slot = count; slot < powers.Length; slot++)
            powers[slot] = new(EffectTypes["INVALID"]);
        return new(index, row["name"], Cursor(row["cursorGraphic"]),
            UniqueBaseItem(row["uniqueBaseItem"], uniqueBaseItems, add: false),
            row.RequiredInt32("minLevel"), row.RequiredInt32("value"), powers);
    }

    private static AuthoritativeCathedralNativeAffixDefinition[] Affixes(string textData, string file)
        => NativeSourceRuleExport.Load(textData, "items", file).Rows.Select(row => new AuthoritativeCathedralNativeAffixDefinition(
            row["name"], Power(row, "power"), row.RequiredInt32("minLevel"),
            Flags(AffixItemTypes, row["itemTypes"], "itemTypes"), Lookup(Alignments, row["alignment"], "alignment"),
            row.RequiredInt32("chance"), row.RequiredBoolean("useful"),
            row.RequiredInt32("minVal"), row.RequiredInt32("maxVal"), row.RequiredInt32("multVal"))).ToArray();

    private static AuthoritativeCathedralNativeItemPower Power(TsvRow row, string field)
        => new(Lookup(EffectTypes, row[field], field),
            row.OptionalInt32($"{field}.value1"), row.OptionalInt32($"{field}.value2"));

    /** LoadSpellData: a null spell precedes the file rows; costs are stored in tens. */
    private static AuthoritativeCathedralNativeSpellDefinition[] Spells(string textData)
        => NativeSourceRuleExport.Load(textData, "spells", "spelldat.tsv").Rows
            .Select((row, index) => new AuthoritativeCathedralNativeSpellDefinition(index + 1, row["name"],
                row.RequiredInt32("bookLevel"), row.RequiredInt32("staffLevel"), row.RequiredInt32("minIntelligence"),
                row.RequiredInt32("bookCost10") * 10, row.RequiredInt32("staffCost10") * 10,
                Flags(SpellFlags, row["flags"], "flags") & 0b11,
                row.RequiredInt32("staffMin"), row.RequiredInt32("staffMax")))
            .Prepend(new(0, string.Empty, 0, 0, 0, 0, 0, 0, 40, 80))
            .ToArray();

    private static AuthoritativeItemGenerationRules Rules(TsvRow row)
        => new(row.RequiredInt32("magicChanceBase"), row.RequiredInt32("magicChancePerLevel"),
            row.RequiredInt32("uniqueChanceNormal"), row.RequiredInt32("uniqueChanceUnique"),
            row.RequiredInt32("bonusLevelsUnique"), row.RequiredInt32("prefixPercent"),
            row.RequiredInt32("suffixPercent"), row.RequiredInt32("onlygoodChance"),
            row.RequiredInt32("noDropPercent"), row.RequiredInt32("goldPercent"));

    /** LoadItemGenerationConfig upper-cases each name and rejects unknown ones. */
    private static HashSet<int> AlwaysMagicMisc(TsvRow row)
        => row["alwaysMagicMisc"].Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(name => Lookup(MiscIds, name.ToUpperInvariant(), "alwaysMagicMisc")).ToHashSet();

    /** items.cpp ItemCAnimTbl/ItemAnimLs, as GetItemAnimationLengthForTesting reads them. */
    public static IReadOnlyDictionary<int, AuthoritativeCathedralNativeAnimationDefinition> CursorAnimations()
        => CursorAnimationIndex.Select((animation, cursor) => (Cursor: cursor, Animation: (int)animation))
            .Where(row => row.Animation >= 0 && row.Animation < DropAnimationLengths.Length)
            .ToDictionary(row => row.Cursor,
                row => new AuthoritativeCathedralNativeAnimationDefinition(DropAnimationLengths[row.Animation]));

    /** 12px font pages the native build ships (CMake/Assets.cmake); other bundled pages are unused. */
    public static IReadOnlyList<int> ShippedFontPages { get; } = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x1f4, 0x1f6, 0x1f9, 0x20, 0x26, 0xe0];

    /** Widths of each shipped 256-glyph 12px font page; codepoint = page * 256 + glyph. */
    public static Dictionary<int, int> GlyphWidths(string fontsDirectory)
    {
        var glyphs = new Dictionary<int, int>();
        foreach (var page in ShippedFontPages) {
            var path = Path.Combine(fontsDirectory, $"12-{page:x2}.clx");
            var widths = ClxWidths(File.ReadAllBytes(path));
            if (widths.Length != 256)
                throw new InvalidDataException($"{Path.GetFileName(path)} must contain 256 glyphs.");
            for (var glyph = 0; glyph < widths.Length; glyph++)
                glyphs.Add(page * 256 + glyph, widths[glyph]);
        }
        if (!glyphs.ContainsKey('?'))
            throw new InvalidDataException("The 12px font page 0 is required.");
        return glyphs;
    }

    /** ClxSpriteList: uint32 count, count+1 uint32 offsets; each sprite starts uint16 header size, uint16 width. */
    public static int[] ClxWidths(ReadOnlySpan<byte> clx)
    {
        if (clx.Length < 8)
            throw new InvalidDataException("CLX data is truncated.");
        var count = BitConverter.ToUInt32(clx[..4]);
        if (count > (uint)(clx.Length - 8) / 4 || BitConverter.ToUInt32(clx.Slice(4 + (int)count * 4, 4)) != clx.Length)
            throw new InvalidDataException("CLX data is not a single sprite list.");
        var widths = new int[count];
        for (var index = 0; index < count; index++) {
            var offset = BitConverter.ToUInt32(clx.Slice(4 + index * 4, 4));
            if (offset > clx.Length - 6)
                throw new InvalidDataException("CLX sprite offset is out of range.");
            widths[index] = BitConverter.ToUInt16(clx.Slice((int)offset + 2, 2));
        }
        return widths;
    }

    /** ParseItemCursorGraphic: names, empty for ICURS_DEFAULT, or a frame number. */
    private static int Cursor(string value)
        => value.Length == 0 ? PlaceholderCursor
            : CursorGraphics.TryGetValue(value, out var cursor) ? cursor
            : byte.TryParse(value, out var number) ? number
            : throw new InvalidDataException($"Unknown cursor graphic '{value}'.");

    /** ParseOrAddUniqueBaseItem gives unknown itemdat names the next free id; unique_itemdat only looks up. */
    private static int UniqueBaseItem(string value, Dictionary<string, int> known, bool add)
    {
        if (known.TryGetValue(value, out var id))
            return id;
        var next = NativeDefaultUniqueBaseItemCount + known.Count - UniqueBaseItems.Count;
        if (!add || next >= NativeMaximumUniqueBaseItemCount)
            throw new InvalidDataException($"Unknown unique base item '{value}'.");
        known.Add(value, next);
        return next;
    }

    private static int Lookup(IReadOnlyDictionary<string, int> names, string value, string column)
        => names.TryGetValue(value, out var result) ? result
            : throw new InvalidDataException($"Unknown {column} value '{value}'.");

    private static int Flags(IReadOnlyDictionary<string, int> names, string value, string column)
        => value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Aggregate(0, (flags, name) => flags | Lookup(names, name, column));

    private static readonly Dictionary<string, int> ItemClasses = new(StringComparer.Ordinal) {
        ["None"] = 0, ["Weapon"] = 1, ["Armor"] = 2, ["Misc"] = 3, ["Gold"] = 4, ["Quest"] = 5,
    };

    private static readonly Dictionary<string, int> EquipTypes = new(StringComparer.Ordinal) {
        ["None"] = 0, ["One-handed"] = 1, ["Two-handed"] = 2, ["Armor"] = 3, ["Helm"] = 4,
        ["Ring"] = 5, ["Amulet"] = 6, ["Unequippable"] = 7, ["Belt"] = 8,
    };

    private static readonly Dictionary<string, int> ItemTypes = new(StringComparer.Ordinal) {
        ["Misc"] = 0, ["Sword"] = 1, ["Axe"] = 2, ["Bow"] = 3, ["Mace"] = 4, ["Shield"] = 5, ["LightArmor"] = 6,
        ["Helm"] = 7, ["MediumArmor"] = 8, ["HeavyArmor"] = 9, ["Staff"] = 10, ["Gold"] = 11, ["Ring"] = 12,
        ["Amulet"] = 13, ["None"] = -1,
    };

    private static readonly Dictionary<string, int> AffixItemTypes = new(StringComparer.Ordinal) {
        ["Misc"] = 1, ["Bow"] = 2, ["Staff"] = 4, ["Weapon"] = 8, ["Shield"] = 16, ["Armor"] = 32,
    };

    private static readonly Dictionary<string, int> Alignments = new(StringComparer.Ordinal) {
        ["Any"] = 0, ["Evil"] = 1, ["Good"] = 2,
    };

    // SpellDataFlags; the low two bits hold MagicType.
    private static readonly Dictionary<string, int> SpellFlags = new(StringComparer.Ordinal) {
        ["Fire"] = 0, ["Lightning"] = 1, ["Magic"] = 2, ["Targeted"] = 4, ["AllowedInTown"] = 8,
    };

    private static readonly Dictionary<string, int> CursorGraphics = new(StringComparer.Ordinal) {
        ["POTION_OF_FULL_MANA"] = 0,
        ["SCROLL_OF"] = 1,
        ["GOLD_SMALL"] = 4,
        ["GOLD_MEDIUM"] = 5,
        ["GOLD_LARGE"] = 6,
        ["THE_BLEEDER"] = 8,
        ["BRAMBLE"] = 9,
        ["RING_OF_TRUTH"] = 10,
        ["RING_OF_REGHA"] = 11,
        ["RING"] = 12,
        ["RING_OF_ENGAGEMENT"] = 13,
        ["CONSTRICTING_RING"] = 14,
        ["SPECTRAL_ELIXIR"] = 15,
        ["ARENA_POTION"] = 16,
        ["GOLDEN_ELIXIR"] = 17,
        ["EMPYREAN_BAND"] = 18,
        ["EAR_SORCERER"] = 19,
        ["EAR_WARRIOR"] = 20,
        ["EAR_ROGUE"] = 21,
        ["BLOOD_STONE"] = 25,
        ["OIL"] = 30,
        ["ELIXIR_OF_VITALITY"] = 31,
        ["POTION_OF_HEALING"] = 32,
        ["POTION_OF_FULL_REJUVENATION"] = 33,
        ["ELIXIR_OF_MAGIC"] = 34,
        ["POTION_OF_FULL_HEALING"] = 35,
        ["ELIXIR_OF_DEXTERITY"] = 36,
        ["POTION_OF_REJUVENATION"] = 37,
        ["ELIXIR_OF_STRENGTH"] = 38,
        ["POTION_OF_MANA"] = 39,
        ["BRAIN"] = 40,
        ["OPTIC_AMULET"] = 44,
        ["AMULET"] = 45,
        ["WIZARDSPIKE"] = 50,
        ["DAGGER"] = 51,
        ["BLACK_RAZOR"] = 53,
        ["GONNAGALS_DIRK"] = 54,
        ["BLADE"] = 56,
        ["BASTARD_SWORD"] = 57,
        ["THE_EXECUTIONERS_BLADE"] = 58,
        ["MACE"] = 59,
        ["LONG_SWORD"] = 60,
        ["BROAD_SWORD"] = 61,
        ["FALCHION"] = 62,
        ["MORNING_STAR"] = 63,
        ["SHORT_SWORD"] = 64,
        ["CLAYMORE"] = 65,
        ["CLUB"] = 66,
        ["SABRE"] = 67,
        ["GRYPHONS_CLAW"] = 68,
        ["SPIKED_CLUB"] = 70,
        ["SCIMITAR"] = 72,
        ["FULL_HELM"] = 75,
        ["MAGIC_ROCK"] = 76,
        ["HELM_OF_SPIRITS"] = 77,
        ["THE_UNDEAD_CROWN"] = 78,
        ["ROYAL_CIRCLET"] = 79,
        ["FOOLS_CREST"] = 80,
        ["HARLEQUIN_CREST"] = 81,
        ["HELM"] = 82,
        ["BUCKLER"] = 83,
        ["VEIL_OF_STEEL"] = 85,
        ["BOOK_GREY"] = 86,
        ["BOOK_RED"] = 87,
        ["BOOK_BLUE"] = 88,
        ["BLACK_MUSHROOM"] = 89,
        ["SKULL_CAP"] = 90,
        ["CAP"] = 91,
        ["TORN_FLESH_OF_SOULS"] = 92,
        ["THINKING_CAP"] = 93,
        ["CROWN"] = 95,
        ["MAP_OF_THE_STARS"] = 96,
        ["FUNGAL_TOME"] = 97,
        ["GREAT_HELM"] = 98,
        ["OVERLORDS_HELM"] = 99,
        ["BATTLE_AXE"] = 101,
        ["HUNTERS_BOW"] = 102,
        ["FIELD_PLATE"] = 103,
        ["STONECLEAVER"] = 104,
        ["SMALL_SHIELD"] = 105,
        ["CLEAVER"] = 106,
        ["STUDDED_LEATHER_ARMOR"] = 107,
        ["DEADLY_HUNTER"] = 108,
        ["SHORT_STAFF"] = 109,
        ["TWO_HANDED_SWORD"] = 110,
        ["CHAIN_MAIL"] = 111,
        ["SMALL_AXE"] = 112,
        ["KITE_SHIELD"] = 113,
        ["SCALE_MAIL"] = 114,
        ["SPLIT_SKULL_SHIELD"] = 116,
        ["DRAGONS_BREACH"] = 117,
        ["SHORT_BOW"] = 118,
        ["LONG_BATTLE_BOW"] = 119,
        ["LONG_WAR_BOW"] = 120,
        ["WAR_HAMMER"] = 121,
        ["MAUL"] = 122,
        ["LONG_STAFF"] = 123,
        ["WAR_STAFF"] = 124,
        ["TAVERN_SIGN"] = 126,
        ["HARD_LEATHER_ARMOR"] = 127,
        ["RAGS"] = 128,
        ["QUILTED_ARMOR"] = 129,
        ["FLAIL"] = 131,
        ["TOWER_SHIELD"] = 132,
        ["COMPOSITE_BOW"] = 133,
        ["GREAT_SWORD"] = 134,
        ["LEATHER_ARMOR"] = 135,
        ["SPLINT_MAIL"] = 136,
        ["ROBE"] = 137,
        ["THE_RAINBOW_CLOAK"] = 138,
        ["ANVIL_OF_FURY"] = 140,
        ["BROAD_AXE"] = 141,
        ["LARGE_AXE"] = 142,
        ["GREAT_AXE"] = 143,
        ["AXE"] = 144,
        ["BLACKOAK_SHIELD"] = 146,
        ["LARGE_SHIELD"] = 147,
        ["GOTHIC_SHIELD"] = 148,
        ["CLOAK"] = 149,
        ["CAPE"] = 150,
        ["FULL_PLATE_MAIL"] = 151,
        ["GOTHIC_PLATE"] = 152,
        ["BREAST_PLATE"] = 153,
        ["RING_MAIL"] = 154,
        ["STAFF_OF_LAZARUS"] = 155,
        ["ARKAINES_VALOR"] = 157,
        ["THE_NEEDLER"] = 158,
        ["NAJS_LIGHT_PLATE"] = 159,
        ["THE_GRIZZLY"] = 160,
        ["THE_GRANDFATHER"] = 161,
        ["THE_PROTECTOR"] = 162,
        ["MESSERSCHMIDTS_REAVER"] = 163,
        ["WINDFORCE"] = 164,
        ["SHORT_WAR_BOW"] = 165,
        ["COMPOSITE_STAFF"] = 166,
        ["SHORT_BATTLE_BOW"] = 167,
        ["XORINES_RING"] = 168,
        ["AMULET_OF_WARDING"] = 170,
        ["KARIKS_RING"] = 173,
        ["MERCURIAL_RING"] = 176,
        ["RING_OF_THUNDER"] = 177,
        ["GIANTS_KNUCKLE"] = 179,
        ["AURIC_AMULET"] = 180,
        ["RING_OF_THE_MYSTICS"] = 181,
        ["ACOLYTES_AMULET"] = 183,
        ["RING_OF_MAGMA"] = 184,
        ["GLADIATORS_RING"] = 186,
        ["RUNE_BOMB"] = 187,
        ["THEODORE"] = 188,
        ["TORN_NOTE_1"] = 189,
        ["TORN_NOTE_2"] = 190,
        ["TORN_NOTE_3"] = 191,
        ["RECONSTRUCTED_NOTE"] = 192,
        ["RUNE_OF_FIRE"] = 193,
        ["GREATER_RUNE_OF_FIRE"] = 194,
        ["RUNE_OF_LIGHTNING"] = 195,
        ["GREATER_RUNE_OF_LIGHTNING"] = 196,
        ["RUNE_OF_STONE"] = 197,
        ["GREY_SUIT"] = 198,
        ["BROWN_SUIT"] = 199,
        ["EATER_OF_SOULS"] = 200,
        ["ARMOR_OF_GLOOM"] = 203,
        ["BONE_CHAIN_ARMOR"] = 204,
        ["THUNDERCLAP"] = 205,
        ["DIAMONDEDGE"] = 206,
        ["FLAMBEAU"] = 209,
        ["GNAT_STING"] = 210,
        ["BLITZEN"] = 219,
        ["DEMON_PLATE_ARMOR"] = 225,
        ["BOVINE"] = 226,
        ["DEFAULT"] = 255,
    };
    private static readonly Dictionary<string, int> MiscIds = new(StringComparer.Ordinal) {
        ["NONE"] = 0,
        ["USEFIRST"] = 1,
        ["FULLHEAL"] = 2,
        ["HEAL"] = 3,
        ["0x4"] = 4,
        ["0x5"] = 5,
        ["MANA"] = 6,
        ["FULLMANA"] = 7,
        ["0x8"] = 8,
        ["0x9"] = 9,
        ["ELIXSTR"] = 10,
        ["ELIXMAG"] = 11,
        ["ELIXDEX"] = 12,
        ["ELIXVIT"] = 13,
        ["0xE"] = 14,
        ["0xF"] = 15,
        ["0x10"] = 16,
        ["0x11"] = 17,
        ["REJUV"] = 18,
        ["FULLREJUV"] = 19,
        ["USELAST"] = 20,
        ["SCROLL"] = 21,
        ["SCROLLT"] = 22,
        ["STAFF"] = 23,
        ["BOOK"] = 24,
        ["RING"] = 25,
        ["AMULET"] = 26,
        ["UNIQUE"] = 27,
        ["0x1C"] = 28,
        ["OILFIRST"] = 29,
        ["OILOF"] = 30,
        ["OILACC"] = 31,
        ["OILMAST"] = 32,
        ["OILSHARP"] = 33,
        ["OILDEATH"] = 34,
        ["OILSKILL"] = 35,
        ["OILBSMTH"] = 36,
        ["OILFORT"] = 37,
        ["OILPERM"] = 38,
        ["OILHARD"] = 39,
        ["OILIMP"] = 40,
        ["OILLAST"] = 41,
        ["MAPOFDOOM"] = 42,
        ["EAR"] = 43,
        ["SPECELIX"] = 44,
        ["0x2D"] = 45,
        ["RUNEFIRST"] = 46,
        ["RUNEF"] = 47,
        ["RUNEL"] = 48,
        ["GR_RUNEL"] = 49,
        ["GR_RUNEF"] = 50,
        ["RUNES"] = 51,
        ["RUNELAST"] = 52,
        ["AURIC"] = 53,
        ["NOTE"] = 54,
        ["ARENAPOT"] = 55,
        ["INVALID"] = -1,
    };
    private static readonly Dictionary<string, int> EffectTypes = new(StringComparer.Ordinal) {
        ["TOHIT"] = 0,
        ["TOHIT_CURSE"] = 1,
        ["DAMP"] = 2,
        ["DAMP_CURSE"] = 3,
        ["TOHIT_DAMP"] = 4,
        ["TOHIT_DAMP_CURSE"] = 5,
        ["ACP"] = 6,
        ["ACP_CURSE"] = 7,
        ["FIRERES"] = 8,
        ["LIGHTRES"] = 9,
        ["MAGICRES"] = 10,
        ["ALLRES"] = 11,
        ["SPLLVLADD"] = 14,
        ["CHARGES"] = 15,
        ["FIREDAM"] = 16,
        ["LIGHTDAM"] = 17,
        ["STR"] = 19,
        ["STR_CURSE"] = 20,
        ["MAG"] = 21,
        ["MAG_CURSE"] = 22,
        ["DEX"] = 23,
        ["DEX_CURSE"] = 24,
        ["VIT"] = 25,
        ["VIT_CURSE"] = 26,
        ["ATTRIBS"] = 27,
        ["ATTRIBS_CURSE"] = 28,
        ["GETHIT_CURSE"] = 29,
        ["GETHIT"] = 30,
        ["LIFE"] = 31,
        ["LIFE_CURSE"] = 32,
        ["MANA"] = 33,
        ["MANA_CURSE"] = 34,
        ["DUR"] = 35,
        ["DUR_CURSE"] = 36,
        ["INDESTRUCTIBLE"] = 37,
        ["LIGHT"] = 38,
        ["LIGHT_CURSE"] = 39,
        ["MULT_ARROWS"] = 41,
        ["FIRE_ARROWS"] = 42,
        ["LIGHT_ARROWS"] = 43,
        ["THORNS"] = 45,
        ["NOMANA"] = 46,
        ["FIREBALL"] = 50,
        ["ABSHALFTRAP"] = 52,
        ["KNOCKBACK"] = 53,
        ["STEALMANA"] = 55,
        ["STEALLIFE"] = 56,
        ["TARGAC"] = 57,
        ["FASTATTACK"] = 58,
        ["FASTRECOVER"] = 59,
        ["FASTBLOCK"] = 60,
        ["DAMMOD"] = 61,
        ["RNDARROWVEL"] = 62,
        ["SETDAM"] = 63,
        ["SETDUR"] = 64,
        ["NOMINSTR"] = 65,
        ["SPELL"] = 66,
        ["ONEHAND"] = 68,
        ["3XDAMVDEM"] = 69,
        ["ALLRESZERO"] = 70,
        ["DRAINLIFE"] = 72,
        ["RNDSTEALLIFE"] = 73,
        ["SETAC"] = 75,
        ["ADDACLIFE"] = 76,
        ["ADDMANAAC"] = 77,
        ["AC_CURSE"] = 79,
        ["LASTDIABLO"] = 79,
        ["FIRERES_CURSE"] = 80,
        ["LIGHTRES_CURSE"] = 81,
        ["MAGICRES_CURSE"] = 82,
        ["DEVASTATION"] = 84,
        ["DECAY"] = 85,
        ["PERIL"] = 86,
        ["JESTERS"] = 87,
        ["CRYSTALLINE"] = 88,
        ["DOPPELGANGER"] = 89,
        ["ACDEMON"] = 90,
        ["ACUNDEAD"] = 91,
        ["MANATOLIFE"] = 92,
        ["LIFETOMANA"] = 93,
        ["INVALID"] = -1,
    };
    private static readonly Dictionary<string, int> UniqueBaseItems = new(StringComparer.Ordinal) {
        ["NONE"] = 0,
        ["SHORTBOW"] = 1,
        ["LONGBOW"] = 2,
        ["HUNTBOW"] = 3,
        ["COMPBOW"] = 4,
        ["WARBOW"] = 5,
        ["BATTLEBOW"] = 6,
        ["DAGGER"] = 7,
        ["FALCHION"] = 8,
        ["CLAYMORE"] = 9,
        ["BROADSWR"] = 10,
        ["SABRE"] = 11,
        ["SCIMITAR"] = 12,
        ["LONGSWR"] = 13,
        ["BASTARDSWR"] = 14,
        ["TWOHANDSWR"] = 15,
        ["GREATSWR"] = 16,
        ["CLEAVER"] = 17,
        ["LARGEAXE"] = 18,
        ["BROADAXE"] = 19,
        ["SMALLAXE"] = 20,
        ["BATTLEAXE"] = 21,
        ["GREATAXE"] = 22,
        ["MACE"] = 23,
        ["MORNSTAR"] = 24,
        ["SPIKCLUB"] = 25,
        ["MAUL"] = 26,
        ["WARHAMMER"] = 27,
        ["FLAIL"] = 28,
        ["LONGSTAFF"] = 29,
        ["SHORTSTAFF"] = 30,
        ["COMPSTAFF"] = 31,
        ["QUARSTAFF"] = 32,
        ["WARSTAFF"] = 33,
        ["SKULLCAP"] = 34,
        ["HELM"] = 35,
        ["GREATHELM"] = 36,
        ["CROWN"] = 37,
        ["38"] = 38,
        ["RAGS"] = 39,
        ["STUDARMOR"] = 40,
        ["CLOAK"] = 41,
        ["ROBE"] = 42,
        ["CHAINMAIL"] = 43,
        ["LEATHARMOR"] = 44,
        ["BREASTPLATE"] = 45,
        ["CAPE"] = 46,
        ["PLATEMAIL"] = 47,
        ["FULLPLATE"] = 48,
        ["BUCKLER"] = 49,
        ["SMALLSHIELD"] = 50,
        ["LARGESHIELD"] = 51,
        ["KITESHIELD"] = 52,
        ["GOTHSHIELD"] = 53,
        ["RING"] = 54,
        ["55"] = 55,
        ["AMULET"] = 56,
        ["SKCROWN"] = 57,
        ["INFRARING"] = 58,
        ["OPTAMULET"] = 59,
        ["TRING"] = 60,
        ["HARCREST"] = 61,
        ["MAPOFDOOM"] = 62,
        ["ELIXIR"] = 63,
        ["ARMOFVAL"] = 64,
        ["STEELVEIL"] = 65,
        ["GRISWOLD"] = 66,
        ["LGTFORGE"] = 67,
        ["LAZSTAFF"] = 68,
        ["BOVINE"] = 69,
        ["INVALID"] = -1,
    };
    private static readonly Dictionary<string, int> SpecialEffects = new(StringComparer.Ordinal) {
        ["None"] = 0,
        ["RandomStealLife"] = 2,
        ["RandomArrowVelocity"] = 4,
        ["FireArrows"] = 8,
        ["FireDamage"] = 16,
        ["LightningDamage"] = 32,
        ["DrainLife"] = 64,
        ["MultipleArrows"] = 512,
        ["Knockback"] = 2048,
        ["StealMana3"] = 8192,
        ["StealMana5"] = 16384,
        ["StealLife3"] = 32768,
        ["StealLife5"] = 65536,
        ["QuickAttack"] = 131072,
        ["FastAttack"] = 262144,
        ["FasterAttack"] = 524288,
        ["FastestAttack"] = 1048576,
        ["FastHitRecovery"] = 2097152,
        ["FasterHitRecovery"] = 4194304,
        ["FastestHitRecovery"] = 8388608,
        ["FastBlock"] = 16777216,
        ["LightningArrows"] = 33554432,
        ["Thorns"] = 67108864,
        ["NoMana"] = 134217728,
        ["HalfTrapDamage"] = 268435456,
        ["TripleDemonDamage"] = 1073741824,
        ["ZeroResistance"] = int.MinValue,
    };
    private static readonly Dictionary<string, int> SpellIds = new(StringComparer.Ordinal) {
        ["Null"] = 0,
        ["FIRST"] = 0,
        ["Firebolt"] = 1,
        ["Healing"] = 2,
        ["Lightning"] = 3,
        ["Flash"] = 4,
        ["Identify"] = 5,
        ["FireWall"] = 6,
        ["TownPortal"] = 7,
        ["StoneCurse"] = 8,
        ["Infravision"] = 9,
        ["Phasing"] = 10,
        ["ManaShield"] = 11,
        ["Fireball"] = 12,
        ["Guardian"] = 13,
        ["ChainLightning"] = 14,
        ["FlameWave"] = 15,
        ["DoomSerpents"] = 16,
        ["BloodRitual"] = 17,
        ["Nova"] = 18,
        ["Invisibility"] = 19,
        ["Inferno"] = 20,
        ["Golem"] = 21,
        ["Rage"] = 22,
        ["Teleport"] = 23,
        ["Apocalypse"] = 24,
        ["Etherealize"] = 25,
        ["ItemRepair"] = 26,
        ["StaffRecharge"] = 27,
        ["TrapDisarm"] = 28,
        ["Elemental"] = 29,
        ["ChargedBolt"] = 30,
        ["HolyBolt"] = 31,
        ["Resurrect"] = 32,
        ["Telekinesis"] = 33,
        ["HealOther"] = 34,
        ["BloodStar"] = 35,
        ["BoneSpirit"] = 36,
        ["LastDiablo"] = 36,
        ["Mana"] = 37,
        ["Magi"] = 38,
        ["Jester"] = 39,
        ["LightningWall"] = 40,
        ["Immolation"] = 41,
        ["Warp"] = 42,
        ["Reflect"] = 43,
        ["Berserk"] = 44,
        ["RingOfFire"] = 45,
        ["Search"] = 46,
        ["RuneOfFire"] = 47,
        ["RuneOfLight"] = 48,
        ["RuneOfNova"] = 49,
        ["RuneOfImmolation"] = 50,
        ["RuneOfStone"] = 51,
        ["LAST"] = 51,
        ["Invalid"] = -1,
    };
    // items.cpp ItemCAnimTbl (229 cursors)
    private static readonly sbyte[] CursorAnimationIndex = [20, 16, 16, 16, 4, 4, 4, 12, 12, 12, 12, 12, 12, 12, 12, 21, 21, 25, 12, 28, 28, 28, 38, 38, 38, 32, 38, 38, 38, 24, 24, 26, 2, 25, 22, 23, 24, 21, 27, 27, 29, 0, 0, 0, 12, 12, 12, 12, 12, 0, 8, 8, 0, 8, 8, 8, 8, 8, 8, 6, 8, 8, 8, 6, 8, 8, 6, 8, 8, 6, 6, 6, 8, 8, 8, 5, 9, 13, 13, 13, 5, 5, 5, 15, 5, 5, 18, 18, 18, 30, 5, 5, 14, 5, 14, 13, 16, 18, 5, 5, 7, 1, 3, 17, 1, 15, 10, 14, 3, 11, 8, 0, 1, 7, 0, 7, 15, 7, 3, 3, 3, 6, 6, 11, 11, 11, 31, 14, 14, 14, 6, 6, 7, 3, 8, 14, 0, 14, 14, 0, 33, 1, 1, 1, 1, 1, 7, 7, 7, 14, 14, 17, 17, 17, 0, 34, 1, 0, 3, 17, 8, 8, 6, 1, 3, 3, 11, 3, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 35, 39, 36, 36, 36, 37, 38, 38, 38, 38, 38, 41, 42, 8, 8, 8, 17, 0, 6, 8, 11, 11, 3, 3, 1, 6, 6, 6, 1, 8, 6, 11, 3, 6, 8, 1, 6, 6, 17, 40, 0, 0];
    // items.cpp ItemAnimLs (43 drop animations)
    private static readonly sbyte[] DropAnimationLengths = [15, 13, 16, 13, 10, 13, 13, 13, 13, 20, 13, 13, 13, 13, 13, 13, 13, 13, 13, 1, 16, 16, 16, 16, 16, 16, 16, 16, 13, 12, 12, 13, 13, 13, 8, 10, 16, 16, 10, 10, 15, 15, 15];
}

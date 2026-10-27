using System.Buffers;
using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Text;

using Devilution.Engine;

namespace Devilution.Diablo;

/** Bounded policy accepted by the detached ordinary Cathedral item generator. */
public sealed record AuthoritativeCathedralThemeItemPolicy(
    int Depth,
    DiabloGameMode GameMode = DiabloGameMode.SinglePlayer,
    DiabloDifficulty Difficulty = DiabloDifficulty.Normal,
    bool Hellfire = false,
    bool Spawn = false,
    bool QuestEnabled = false,
    bool SetLevel = false,
    bool LegacyLevelItemGenerationAllowed = true,
    int CurrentDungeonLevel = 0);

/** Player values read by native item powers and SetupItem. */
public sealed record AuthoritativeCathedralThemeItemPlayerContext(
    bool PlayerPresent = true,
    bool LevelLoading = true,
    int MaximumManaBase = 0,
    int MaximumLifeBase = 0);

/** Direct SetupAllItems input. The item state is intentionally allowed to be partial. */
public sealed record AuthoritativeCathedralThemeItemRequest(
    uint ItemSeed,
    int NativeItemIndex,
    int Level,
    int UniqueChance,
    bool OnlyGood,
    bool PreGenerate,
    int UniqueOffset = 0,
    bool ForceNotUnique = false,
    AuthoritativeCathedralInitialItemState? InputItem = null,
    bool ResetBeforeGetItemAttrs = false,
    AuthoritativeCathedralThemeItemPlayerContext? Player = null,
    IReadOnlySet<int>? UniqueItemFlags = null);

/** Detached selector input. Placement and item-pool mutation are deliberately excluded. */
public sealed record AuthoritativeCathedralThemeItemSelectionRequest(
    uint RandomState,
    int CurrentLevel,
    bool OnlyGood,
    int RequestedItemType = -1,
    int RequestedMiscId = -1,
    AuthoritativeCathedralInitialItemState? InputItem = null,
    bool ResetBeforeGetItemAttrs = false,
    AuthoritativeCathedralThemeItemPlayerContext? Player = null,
    IReadOnlySet<int>? UniqueItemFlags = null);

/** Result of detached SetupAllItems/unique continuation and optional public ground-scalar setup. */
public sealed record AuthoritativeCathedralThemeItemResult(
    AuthoritativeCathedralInitialItemState Item,
    uint ItemSeed,
    uint FinalRandomState,
    int SelectedNativeItemIndex,
    bool Cleared,
    IReadOnlySet<int> UniqueItemFlags);

/** Native ItemData power payload, retaining the original integer effect identity. */
public sealed record AuthoritativeCathedralNativeItemPower(
    int Type,
    int Param1 = 0,
    int Param2 = 0);

/** Raw ItemData row required by the original SetupAllItems path. */
public sealed record AuthoritativeCathedralNativeItemDefinition(
    int NativeItemIndex,
    int NativeItemId,
    int DropRate,
    int ItemClass,
    int EquipLocation,
    int CursorGraphic,
    int ItemType,
    string Name,
    string ShortName,
    int MinimumMonsterLevel,
    int Durability,
    int MinimumDamage,
    int MaximumDamage,
    int MinimumArmorClass,
    int MaximumArmorClass,
    int MinimumStrength,
    int MinimumMagic,
    int MinimumDexterity,
    uint Flags,
    int MiscId,
    int SpellId,
    bool Usable,
    int Value,
    int AnimationFrameCount,
    int AnimationTicksPerFrame = 1,
    bool Available = true);

/** Raw native prefix/suffix row. Item-type flags and table order are significant. */
public sealed record AuthoritativeCathedralNativeAffixDefinition(
    string Name,
    AuthoritativeCathedralNativeItemPower Power,
    int MinimumLevel,
    int ItemTypeFlags,
    int Alignment,
    int Chance,
    bool Useful,
    int MinimumValue,
    int MaximumValue,
    int ValueMultiplier);

/** Raw UniqueItem row. Powers are applied in this supplied order. */
public sealed record AuthoritativeCathedralNativeUniqueDefinition(
    int NativeUniqueId,
    string Name,
    int CursorGraphic,
    int NativeBaseItemId,
    int MinimumLevel,
    int Value,
    IReadOnlyList<AuthoritativeCathedralNativeItemPower> Powers);

/** Raw SpellData fields consumed by book and staff generation. */
public sealed record AuthoritativeCathedralNativeSpellDefinition(
    int NativeSpellId,
    string Name,
    int BookLevel,
    int StaffLevel,
    int MinimumMagic,
    int BookCost,
    int StaffCost,
    int MagicType,
    int StaffMinimumCharges,
    int StaffMaximumCharges,
    bool AvailableInBook = true,
    bool AvailableInStaff = true);

/** Public AnimationInfo values obtained from a cursor's externally supplied sprite metadata. */
public sealed record AuthoritativeCathedralNativeAnimationDefinition(
    int FrameCount,
    int TicksPerFrame = 1);

/** Cursor identities used by native book and gold helpers. */
public sealed record AuthoritativeCathedralNativeCursorDefinition(
    int BookRed,
    int BookBlue,
    int BookGrey,
    int GoldSmall,
    int GoldMedium,
    int GoldLarge);

/**
 * Complete raw metadata boundary for ordinary Cathedral item generation.
 * No renderer, MPQ, or normalized drop catalog dependency is used.
 * Detached convenience rule defaults are not a validated production content pack.
 */
public sealed class AuthoritativeCathedralThemeItemCatalog
{
    private readonly IReadOnlyList<AuthoritativeCathedralNativeItemDefinition> items;
    private readonly IReadOnlyList<AuthoritativeCathedralNativeAffixDefinition> prefixes;
    private readonly IReadOnlyList<AuthoritativeCathedralNativeAffixDefinition> suffixes;
    private readonly IReadOnlyList<AuthoritativeCathedralNativeUniqueDefinition> uniques;
    private readonly IReadOnlyList<AuthoritativeCathedralNativeSpellDefinition> spells;
    private readonly IReadOnlyDictionary<int, AuthoritativeCathedralNativeAnimationDefinition> cursorAnimations;
    private readonly IReadOnlyDictionary<int, int> glyphWidths;
    private readonly IReadOnlySet<int> alwaysMagicMiscIds;

    public AuthoritativeCathedralThemeItemCatalog(
        IEnumerable<AuthoritativeCathedralNativeItemDefinition> items,
        IEnumerable<AuthoritativeCathedralNativeAffixDefinition> prefixes,
        IEnumerable<AuthoritativeCathedralNativeAffixDefinition> suffixes,
        IEnumerable<AuthoritativeCathedralNativeUniqueDefinition> uniques,
        IEnumerable<AuthoritativeCathedralNativeSpellDefinition> spells,
        IReadOnlyDictionary<int, AuthoritativeCathedralNativeAnimationDefinition> cursorAnimations,
        IReadOnlyDictionary<int, int> glyphWidths,
        AuthoritativeCathedralNativeCursorDefinition cursors,
        IReadOnlySet<int>? alwaysMagicMiscIds = null,
        AuthoritativeItemGenerationRules? rules = null,
        int replacementGlyphWidth = -1)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(prefixes);
        ArgumentNullException.ThrowIfNull(suffixes);
        ArgumentNullException.ThrowIfNull(uniques);
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(cursorAnimations);
        ArgumentNullException.ThrowIfNull(glyphWidths);
        ArgumentNullException.ThrowIfNull(cursors);

        this.items = new ReadOnlyCollection<AuthoritativeCathedralNativeItemDefinition>(items.ToArray());
        this.prefixes = new ReadOnlyCollection<AuthoritativeCathedralNativeAffixDefinition>(prefixes.ToArray());
        this.suffixes = new ReadOnlyCollection<AuthoritativeCathedralNativeAffixDefinition>(suffixes.ToArray());
        var uniqueRows = uniques.ToArray();
        if (uniqueRows.Any(unique => unique.Powers is null))
            throw new InvalidDataException("Native unique metadata must provide a power list.");
        this.uniques = new ReadOnlyCollection<AuthoritativeCathedralNativeUniqueDefinition>(
            uniqueRows.Select(unique => unique with {
                Powers = new ReadOnlyCollection<AuthoritativeCathedralNativeItemPower>(unique.Powers.ToArray()),
            }).ToArray());
        this.spells = new ReadOnlyCollection<AuthoritativeCathedralNativeSpellDefinition>(spells.ToArray());
        this.cursorAnimations = new ReadOnlyDictionary<int, AuthoritativeCathedralNativeAnimationDefinition>(
            new Dictionary<int, AuthoritativeCathedralNativeAnimationDefinition>(cursorAnimations));
        this.glyphWidths = new ReadOnlyDictionary<int, int>(new Dictionary<int, int>(glyphWidths));
        var alwaysMagicValues = alwaysMagicMiscIds is null ? Array.Empty<int>() : alwaysMagicMiscIds.ToArray();
        this.alwaysMagicMiscIds = alwaysMagicValues.ToFrozenSet();
        var sourceRules = rules ?? AuthoritativeItemGenerationRules.Default;
        if (sourceRules.AlwaysMagicMisc is null)
            throw new InvalidDataException("Native item-generation rules must provide an AlwaysMagicMisc set.");
        Rules = sourceRules with {
            AlwaysMagicMisc = sourceRules.AlwaysMagicMisc.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
        };
        Cursors = cursors;
        ReplacementGlyphWidth = replacementGlyphWidth;
        Validate();
    }

    public IReadOnlyList<AuthoritativeCathedralNativeItemDefinition> Items => items;
    public IReadOnlyList<AuthoritativeCathedralNativeAffixDefinition> Prefixes => prefixes;
    public IReadOnlyList<AuthoritativeCathedralNativeAffixDefinition> Suffixes => suffixes;
    public IReadOnlyList<AuthoritativeCathedralNativeUniqueDefinition> Uniques => uniques;
    public IReadOnlyList<AuthoritativeCathedralNativeSpellDefinition> Spells => spells;
    public IReadOnlyDictionary<int, AuthoritativeCathedralNativeAnimationDefinition> CursorAnimations => cursorAnimations;
    public IReadOnlyDictionary<int, int> GlyphWidths => glyphWidths;
    public IReadOnlySet<int> AlwaysMagicMiscIds => alwaysMagicMiscIds;
    public AuthoritativeItemGenerationRules Rules { get; }
    public AuthoritativeCathedralNativeCursorDefinition Cursors { get; }
    public int ReplacementGlyphWidth { get; }

    private void Validate()
    {
        if (items.Count == 0 || spells.Count < 2)
            throw new InvalidDataException("The native item catalog requires item and spell rows.");
        if (items.Any(item => item.NativeItemIndex < 0 || item.NativeItemIndex > short.MaxValue
                || item.NativeItemId is < sbyte.MinValue or > sbyte.MaxValue
                || item.DropRate is < 0 or > byte.MaxValue
                || item.ItemClass is < byte.MinValue or > byte.MaxValue
                || item.EquipLocation is < sbyte.MinValue or > sbyte.MaxValue
                || item.CursorGraphic is < 0 or > byte.MaxValue
                || item.CursorGraphic == byte.MaxValue && (item.DropRate != 0 || item.AnimationFrameCount != 0)
                || item.ItemType is < sbyte.MinValue or > sbyte.MaxValue
                || item.MinimumMonsterLevel is < 0 or > byte.MaxValue
                || item.Durability is < 0 or > byte.MaxValue
                || item.MinimumDamage is < 0 or > byte.MaxValue
                || item.MaximumDamage < item.MinimumDamage || item.MaximumDamage > byte.MaxValue
                || item.MinimumArmorClass is < 0 or > byte.MaxValue
                || item.MaximumArmorClass < item.MinimumArmorClass || item.MaximumArmorClass > byte.MaxValue
                || item.MinimumStrength is < 0 or > byte.MaxValue
                || item.MinimumMagic is < 0 or > byte.MaxValue
                || item.MinimumDexterity is < 0 or > byte.MaxValue
                || item.MiscId is < sbyte.MinValue or > sbyte.MaxValue
                || item.SpellId is < sbyte.MinValue or > sbyte.MaxValue
                || item.Value is < 0 or > ushort.MaxValue
                || item.AnimationFrameCount < 0 || item.AnimationFrameCount > sbyte.MaxValue
                || item.CursorGraphic != byte.MaxValue && item.AnimationFrameCount == 0
                || item.AnimationTicksPerFrame != 1
                || !IsUtf8Text(item.Name) || !IsUtf8Text(item.ShortName)))
            throw new InvalidDataException("Native item metadata contains an invalid field width or name.");
        if (items.Select(item => item.NativeItemIndex).Distinct().Count() != items.Count
            || items.Select((item, index) => item.NativeItemIndex == index).Any(isInOrder => !isInOrder))
            throw new InvalidDataException("Native item indices must be unique and retain table order.");
        if (spells.Select((spell, index) => spell.NativeSpellId == index).Any(valid => !valid))
            throw new InvalidDataException("Native spells must retain contiguous SpellID order.");
        if (spells.Any(spell => spell.NativeSpellId is < 0 or > sbyte.MaxValue
                || !IsUtf8Text(spell.Name)
                || spell.BookLevel is < -1 or > sbyte.MaxValue
                || spell.StaffLevel is < -1 or > sbyte.MaxValue
                || spell.MinimumMagic is < 0 or > byte.MaxValue
                || spell.BookCost is < 0 or > 655350 || spell.BookCost % 10 != 0
                || spell.StaffCost is < 0 or > 2550 || spell.StaffCost % 10 != 0
                || spell.MagicType is < 0 or > 2
                || spell.StaffMinimumCharges is < 0 or > byte.MaxValue
                || spell.StaffMaximumCharges < spell.StaffMinimumCharges
                || spell.StaffMaximumCharges > byte.MaxValue))
            throw new InvalidDataException("Native spell metadata contains an invalid field.");
        ValidateAffixes(prefixes);
        ValidateAffixes(suffixes);
        if (uniques.Any(unique => unique.NativeUniqueId is < 0 or > 127
                || !IsUtf8Text(unique.Name)
                || unique.CursorGraphic < 0 || unique.CursorGraphic > 228 && unique.CursorGraphic != 255
                || unique.NativeBaseItemId is < sbyte.MinValue or > sbyte.MaxValue
                || unique.MinimumLevel is < 0 or > sbyte.MaxValue || unique.Value < 0
                || unique.Powers is null || unique.Powers.Count > 6))
            throw new InvalidDataException("Native unique metadata contains an invalid field.");
        if (uniques.Select(unique => unique.NativeUniqueId).Distinct().Count() != uniques.Count
            || uniques.Select((unique, index) => unique.NativeUniqueId == index).Any(isInOrder => !isInOrder))
            throw new InvalidDataException("Native unique IDs must be distinct and retain table order.");
        foreach (var unique in uniques)
            if (unique.Powers.Any(power => !IsValidPower(power)))
                throw new InvalidDataException("Native unique power metadata contains an invalid field.");
        if (cursorAnimations.Any(pair => pair.Key is < 0 or > 228
                || pair.Value is null || pair.Value.FrameCount is < 1 or > sbyte.MaxValue
                || pair.Value.TicksPerFrame != 1))
            throw new InvalidDataException("Cursor animation metadata contains an invalid field.");
        if (glyphWidths.Count == 0 || ReplacementGlyphWidth < 0)
            throw new InvalidDataException("Raw GameFont12 glyph widths and replacement width are required.");
        if (glyphWidths.Any(pair => pair.Key is < 0 or > 0x10FFFF || pair.Value is < 0 or > ushort.MaxValue))
            throw new InvalidDataException("Glyph widths contain an invalid Unicode codepoint or width.");
        if (!glyphWidths.ContainsKey('?'))
            throw new InvalidDataException("Raw glyph metadata must include the native replacement glyph.");
        if (ReplacementGlyphWidth != glyphWidths['?'])
            throw new InvalidDataException("The replacement width must come from the supplied '?' glyph row.");
        foreach (var row in glyphWidths.Keys.Select(codepoint => codepoint >> 8).Distinct())
            for (var offset = 0; offset < 256; offset++)
                if (!glyphWidths.ContainsKey((row << 8) | offset))
                    throw new InvalidDataException($"Raw GameFont12 glyph row {row} is incomplete.");
        if (new[] { Cursors.BookRed, Cursors.BookBlue, Cursors.BookGrey, Cursors.GoldSmall,
                Cursors.GoldMedium, Cursors.GoldLarge }.Any(cursor => cursor is < 0 or > byte.MaxValue))
            throw new InvalidDataException("Native cursor metadata contains an invalid cursor identity.");
        if (Rules.MagicChanceBase < 0 || Rules.MagicChancePerLevel < 0
            || Rules.UniqueChanceNormal is < 0 or > 100 || Rules.UniqueChanceUnique is < 0 or > 100
            || Rules.BonusLevelsUnique < 0 || Rules.PrefixPercent is < 0 or > 100
            || Rules.SuffixPercent is < 0 or > 100 || Rules.OnlyGoodChance is < 0 or > 100
            || Rules.GoldPercent is < 0 or > 100 || Rules.NoDropPercent is < 0 or > 100)
            throw new InvalidDataException("Native item-generation probabilities are outside their bounds.");
        if (cursorAnimations.Count != 0) {
            foreach (var item in items)
                if (item.CursorGraphic != byte.MaxValue && !cursorAnimations.ContainsKey(item.CursorGraphic))
                    throw new InvalidDataException($"Missing animation metadata for cursor {item.CursorGraphic}.");
            foreach (var spell in spells)
                if (spell.AvailableInBook && spell.BookLevel >= 0 && !cursorAnimations.ContainsKey(Cursors.BookRed))
                    throw new InvalidDataException("Book cursor animation metadata is missing.");
        }
    }

    private static void ValidateAffixes(IEnumerable<AuthoritativeCathedralNativeAffixDefinition> values)
    {
        foreach (var affix in values) {
            if (!IsUtf8Text(affix.Name) || affix.MinimumLevel is < 0 or > sbyte.MaxValue || affix.ItemTypeFlags is < 0 or > 63
                || affix.Alignment is < 0 or > 2 || affix.Chance is < 0 or > byte.MaxValue
                || affix.MinimumValue > affix.MaximumValue
                || !IsValidPower(affix.Power))
                throw new InvalidDataException("Native affix metadata contains an invalid field.");
        }
    }

    private static bool IsValidPower(AuthoritativeCathedralNativeItemPower power)
        => power.Type is >= -1 and <= sbyte.MaxValue;

    private static bool IsUtf8Text(string value)
    {
        if (value is null || value.Contains('\0'))
            return false;
        try {
            _ = new UTF8Encoding(false, true).GetBytes(value);
            return true;
        } catch (EncoderFallbackException) {
            return false;
        }
    }
}

/**
 * Detached ordinary Cathedral SetupAllItems/unique continuation for depths 1-4.
 *
 * The optional ground path only prepares public scalar state. No CreateThemeRooms or world
 * creation is implemented here.
 */
public static class AuthoritativeCathedralThemeItemGenerator
{
    private const int ItemTypeMisc = 0;
    private const int ItemTypeSword = 1;
    private const int ItemTypeAxe = 2;
    private const int ItemTypeBow = 3;
    private const int ItemTypeMace = 4;
    private const int ItemTypeShield = 5;
    private const int ItemTypeLightArmor = 6;
    private const int ItemTypeHelm = 7;
    private const int ItemTypeMediumArmor = 8;
    private const int ItemTypeHeavyArmor = 9;
    private const int ItemTypeStaff = 10;
    private const int ItemTypeGold = 11;
    private const int ItemTypeRing = 12;
    private const int ItemTypeAmulet = 13;

    // item_misc_id values from tables/itemdat.h: IMISC_BOOK=24, IMISC_RING=25,
    // IMISC_AMULET=26, and IMISC_UNIQUE=27.
    private const int MiscBook = 24;
    private const int MiscHeal = 3;
    private const int MiscMana = 6;
    private const int MiscUnique = 27;
    private const int EquipUnequipable = 7;
    private const int QualityNormal = 0;
    private const int QualityMagic = 1;
    private const int QualityUnique = 2;
    private const int SelectionNone = 0;
    private const int SelectionBottom = 1;
    private const ushort CreateInfoOnlyGood = 1 << 6;
    private const ushort CreateInfoUper15 = 1 << 7;
    private const ushort CreateInfoUper1 = 1 << 8;
    private const ushort CreateInfoUseful = CreateInfoUper15 | CreateInfoUper1;
    private const ushort CreateInfoUnique = 1 << 9;
    private const ushort CreateInfoPregen = 1 << 15;
    private const int AffixMisc = 1;
    private const int AffixBow = 2;
    private const int AffixStaff = 4;
    private const int AffixWeapon = 8;
    private const int AffixShield = 16;
    private const int AffixArmor = 32;
    private const int GoeAny = 0;
    private const int GoeEvil = 1;
    private const int GoeGood = 2;
    private const int GoldSmallLimit = 1000;
    private const int GoldMediumLimit = 2500;
    private const int NativeHealingPotionCursor = 32; // Canonical IDI_HEAL ItemData row.
    private const int NativeManaPotionCursor = 39; // Canonical IDI_MANA ItemData row.
    private const int GoldMaxLimit = 5000;
    private const int ItemNameBytes = 64;
    private const int ZeroWidthSpace = 0x200B;

    // item_effect_type values from tables/itemdat.h. Keeping these numeric
    // identities avoids introducing a second managed enum with different data ownership.
    private const int PowerToHit = 0;
    private const int PowerToHitCurse = 1;
    private const int PowerDamp = 2;
    private const int PowerDampCurse = 3;
    private const int PowerToHitDamp = 4;
    private const int PowerToHitDampCurse = 5;
    private const int PowerAcp = 6;
    private const int PowerAcpCurse = 7;
    private const int PowerFireRes = 8;
    private const int PowerLightRes = 9;
    private const int PowerMagicRes = 10;
    private const int PowerAllRes = 11;
    private const int PowerSpellLevel = 14;
    private const int PowerCharges = 15;
    private const int PowerFireDamage = 16;
    private const int PowerLightningDamage = 17;
    private const int PowerStrength = 19;
    private const int PowerStrengthCurse = 20;
    private const int PowerMagic = 21;
    private const int PowerMagicCurse = 22;
    private const int PowerDexterity = 23;
    private const int PowerDexterityCurse = 24;
    private const int PowerVitality = 25;
    private const int PowerVitalityCurse = 26;
    private const int PowerAttributes = 27;
    private const int PowerAttributesCurse = 28;
    private const int PowerGetHitCurse = 29;
    private const int PowerGetHit = 30;
    private const int PowerLife = 31;
    private const int PowerLifeCurse = 32;
    private const int PowerMana = 33;
    private const int PowerManaCurse = 34;
    private const int PowerDurability = 35;
    private const int PowerDurabilityCurse = 36;
    private const int PowerIndestructible = 37;
    private const int PowerLight = 38;
    private const int PowerLightCurse = 39;
    private const int PowerMultipleArrows = 41;
    private const int PowerFireArrows = 42;
    private const int PowerLightningArrows = 43;
    private const int PowerThorns = 45;
    private const int PowerNoMana = 46;
    private const int PowerFireball = 50;
    private const int PowerHalfTrap = 52;
    private const int PowerKnockback = 53;
    private const int PowerStealMana = 55;
    private const int PowerStealLife = 56;
    private const int PowerTargetArmor = 57;
    private const int PowerFastAttack = 58;
    private const int PowerFastRecover = 59;
    private const int PowerFastBlock = 60;
    private const int PowerDamageModifier = 61;
    private const int PowerRandomArrowVelocity = 62;
    private const int PowerSetDamage = 63;
    private const int PowerSetDurability = 64;
    private const int PowerNoMinimumStrength = 65;
    private const int PowerSpell = 66;
    private const int PowerOneHand = 68;
    private const int PowerTripleDemonDamage = 69;
    private const int PowerAllResZero = 70;
    private const int PowerDrainLife = 72;
    private const int PowerRandomStealLife = 73;
    private const int PowerSetArmor = 75;
    private const int PowerAddArmorLife = 76;
    private const int PowerAddManaArmor = 77;
    private const int PowerArmorCurse = 79;
    private const int PowerFireResCurse = 80;
    private const int PowerLightResCurse = 81;
    private const int PowerMagicResCurse = 82;
    private const int PowerDevastation = 84;
    private const int PowerDecay = 85;
    private const int PowerPeril = 86;
    private const int PowerJesters = 87;
    private const int PowerCrystalline = 88;
    private const int PowerDoppelganger = 89;
    private const int PowerAgainstDemons = 90;
    private const int PowerAgainstUndead = 91;
    private const int PowerManaToLife = 92;
    private const int PowerLifeToMana = 93;

    /** Generates SetupAllItems, unique continuation, and final public ground-scalar state. */
    public static AuthoritativeCathedralThemeItemResult Generate(
        AuthoritativeCathedralThemeItemRequest request,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalog);
        ValidatePolicy(policy);
        return Generate(request, catalog, policy, AuthoritativeNativeDungeonFamilyContext.Cathedral(policy.Depth));
    }

    internal static AuthoritativeCathedralThemeItemResult Generate(
        AuthoritativeCathedralThemeItemRequest request,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy,
        AuthoritativeNativeDungeonFamilyContext familyContext)
        => GenerateCore(request, catalog, policy, true, familyContext);

    /** Generates ordinary Cathedral SetupAllUseful contents, including L2 portal rolls. */
    internal static AuthoritativeCathedralThemeItemResult GenerateUseful(
        AuthoritativeCathedralInitialItemState inputItem,
        uint itemSeed,
        int currentLevel,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy,
        AuthoritativeCathedralThemeItemPlayerContext? player,
        IReadOnlySet<int>? uniqueItemFlags)
    {
        ArgumentNullException.ThrowIfNull(inputItem);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(policy);
        ValidatePolicy(policy);
        return GenerateUseful(inputItem, itemSeed, currentLevel, catalog, policy, player,
            uniqueItemFlags, AuthoritativeNativeDungeonFamilyContext.Cathedral(policy.Depth));
    }

    internal static AuthoritativeCathedralThemeItemResult GenerateUseful(
        AuthoritativeCathedralInitialItemState inputItem,
        uint itemSeed,
        int currentLevel,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy,
        AuthoritativeCathedralThemeItemPlayerContext? player,
        IReadOnlySet<int>? uniqueItemFlags,
        AuthoritativeNativeDungeonFamilyContext familyContext)
    {
        ArgumentNullException.ThrowIfNull(inputItem);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(policy);
        ValidatePolicy(policy, familyContext);
        if (currentLevel != policy.Depth || policy.SetLevel)
            throw new InvalidDataException("Native useful contents require the matching ordinary dungeon depth.");
        ValidateInputState(inputItem);
        ValidateUniqueFlags(uniqueItemFlags);

        // Native SetupAllUseful chooses IDI_MANA/IDI_HEAL by canonical item
        // index; later droppable rows duplicate their misc/name/cursor fields.
        var mana = FindUsefulItem(
            catalog,
            AuthoritativeCathedralInitialItemGenerator.NativeManaPotionId,
            "Potion of Mana",
            MiscMana,
            NativeManaPotionCursor);
        var healing = FindUsefulItem(
            catalog,
            AuthoritativeCathedralInitialItemGenerator.NativeHealingPotionId,
            "Potion of Healing",
            MiscHeal,
            NativeHealingPotionCursor);
        var flags = CopyFlags(uniqueItemFlags);
        var random = new AuthoritativeLegacyRandom(itemSeed);

        // Native PickRandomlyAmong({ IDI_MANA, IDI_HEAL }) clamps a negative
        // GenerateRnd result to the first entry, Mana.
        var selection = Math.Max(random.GenerateRnd(2), 0) == 0 ? mana : healing;
        // Original Diablo consumes this draw only deeper than L1; negative is not FlipCoin success.
        if (currentLevel > 1 && random.GenerateRnd(3) == 0)
            selection = FindUsefulItem(catalog, 27, "Scroll of Town Portal", 21, 1,
                minimumMonsterLevel: 4, spellId: 7);
        var item = GetItemAttrs(CloneItem(inputItem with { Seed = itemSeed }),
            selection.NativeItemIndex, currentLevel, random, catalog, policy) with {
                Seed = itemSeed,
                CreateInfo = checked((ushort)(currentLevel | CreateInfoUseful)),
            };
        item = SetupItem(item, selection.NativeItemIndex, catalog, player, policy);
        return ToResult(new GenerationState {
            Item = item,
            ItemSeed = itemSeed,
            RandomState = random.State,
            ItemIndex = selection.NativeItemIndex,
        }, flags);
    }

    /** Generates SetupAllItems followed by TryRandomUniqueItem, without ground-scalar mutation. */
    public static AuthoritativeCathedralThemeItemResult GenerateSetupAndUniqueContinuation(
        AuthoritativeCathedralThemeItemRequest request,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalog);
        ValidatePolicy(policy);
        return GenerateSetupAndUniqueContinuation(request, catalog, policy,
            AuthoritativeNativeDungeonFamilyContext.Cathedral(policy.Depth));
    }

    internal static AuthoritativeCathedralThemeItemResult GenerateSetupAndUniqueContinuation(
        AuthoritativeCathedralThemeItemRequest request,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy,
        AuthoritativeNativeDungeonFamilyContext familyContext)
        => GenerateCore(request, catalog, policy, false, familyContext);

    private static AuthoritativeCathedralThemeItemResult GenerateCore(
        AuthoritativeCathedralThemeItemRequest request,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy,
        bool applyGroundAnimation,
        AuthoritativeNativeDungeonFamilyContext familyContext)
    {
        ValidateRequest(request, catalog, policy, familyContext);
        var flags = CopyFlags(request.UniqueItemFlags);
        var generation = SetupCore(request, catalog, policy, familyContext);
        if (!generation.Cleared)
            generation = TryRandomUnique(generation, request, catalog, policy, flags, familyContext);
        if (applyGroundAnimation)
            generation.Item = SetupItem(generation.Item, generation.ItemIndex, catalog, request.Player, policy);
        return ToResult(generation, flags);
    }

    /** Performs only the detached native SetupAllItems phase. */
    public static AuthoritativeCathedralThemeItemResult SetupAllItems(
        AuthoritativeCathedralThemeItemRequest request,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalog);
        ValidatePolicy(policy);
        return SetupAllItems(request, catalog, policy,
            AuthoritativeNativeDungeonFamilyContext.Cathedral(policy.Depth));
    }

    internal static AuthoritativeCathedralThemeItemResult SetupAllItems(
        AuthoritativeCathedralThemeItemRequest request,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy,
        AuthoritativeNativeDungeonFamilyContext familyContext)
    {
        ValidateRequest(request, catalog, policy, familyContext);
        return ToResult(SetupCore(request, catalog, policy, familyContext), CopyFlags(request.UniqueItemFlags));
    }

    /** Selects and generates preparatory state without pool allocation or placement; selector parity is not independently validated. */
    public static AuthoritativeCathedralThemeItemResult CreateRandomItem(
        AuthoritativeCathedralThemeItemSelectionRequest request,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalog);
        ValidatePolicy(policy);
        return CreateRandomItem(request, catalog, policy,
            AuthoritativeNativeDungeonFamilyContext.Cathedral(policy.Depth));
    }

    internal static AuthoritativeCathedralThemeItemResult CreateRandomItem(
        AuthoritativeCathedralThemeItemSelectionRequest request,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy,
        AuthoritativeNativeDungeonFamilyContext familyContext)
    {
        ValidateSelectionRequest(request, catalog, policy, familyContext);
        var random = new AuthoritativeLegacyRandom(request.RandomState);
        var itemIndex = SelectRandomItem(random, request.OnlyGood, request.CurrentLevel, catalog);
        var itemSeed = unchecked((uint)random.AdvanceRndSeed());
        var itemRequest = new AuthoritativeCathedralThemeItemRequest(
            itemSeed,
            itemIndex,
            checked(request.CurrentLevel * 2),
            catalog.Rules.UniqueChanceNormal,
            request.OnlyGood,
            false,
            InputItem: request.InputItem,
            ResetBeforeGetItemAttrs: request.ResetBeforeGetItemAttrs,
            Player: request.Player,
            UniqueItemFlags: request.UniqueItemFlags);
        var result = GenerateSetupAndUniqueContinuation(itemRequest, catalog, policy, familyContext);
        return result;
    }

    /** Selects and generates typed preparatory state without pool allocation or placement; selector parity is not independently validated. */
    public static AuthoritativeCathedralThemeItemResult CreateTypeItem(
        AuthoritativeCathedralThemeItemSelectionRequest request,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalog);
        ValidatePolicy(policy);
        return CreateTypeItem(request, catalog, policy,
            AuthoritativeNativeDungeonFamilyContext.Cathedral(policy.Depth));
    }

    internal static AuthoritativeCathedralThemeItemResult CreateTypeItem(
        AuthoritativeCathedralThemeItemSelectionRequest request,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy,
        AuthoritativeNativeDungeonFamilyContext familyContext)
    {
        ValidateSelectionRequest(request, catalog, policy, familyContext);
        ValidateRequestedItemType(request.RequestedItemType);
        var random = new AuthoritativeLegacyRandom(request.RandomState);
        var itemIndex = request.RequestedItemType == ItemTypeGold
            ? FindGoldItem(catalog)
            : SelectTypeItem(random, request.RequestedItemType, request.RequestedMiscId,
                request.CurrentLevel, request.OnlyGood, catalog);
        var itemSeed = unchecked((uint)random.AdvanceRndSeed());
        var itemRequest = new AuthoritativeCathedralThemeItemRequest(
            itemSeed,
            itemIndex,
            checked(request.CurrentLevel * 2),
            catalog.Rules.UniqueChanceNormal,
            request.OnlyGood,
            false,
            InputItem: request.InputItem,
            ResetBeforeGetItemAttrs: request.ResetBeforeGetItemAttrs,
            Player: request.Player,
            UniqueItemFlags: request.UniqueItemFlags);
        return GenerateSetupAndUniqueContinuation(itemRequest, catalog, policy, familyContext);
    }

    // The world wrapper must expose the selector boundary without performing
    // AdvanceRndSeed, allocation, placement, or item generation. Keep this
    // accessor beside the native selector implementation so the wrapper does
    // not duplicate the drop-table predicates or RNG order.
    internal static AuthoritativeCathedralThemeItemWorldSelectionResult SelectRandomItemForWorld(
        uint randomState,
        int currentLevel,
        bool onlyGood,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ValidatePolicy(policy);
        return SelectRandomItemForWorld(randomState, currentLevel, onlyGood, catalog, policy,
            AuthoritativeNativeDungeonFamilyContext.Cathedral(policy.Depth));
    }

    internal static AuthoritativeCathedralThemeItemWorldSelectionResult SelectRandomItemForWorld(
        uint randomState,
        int currentLevel,
        bool onlyGood,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy,
        AuthoritativeNativeDungeonFamilyContext familyContext)
    {
        ValidateSelectionRequest(new AuthoritativeCathedralThemeItemSelectionRequest(
            randomState, currentLevel, onlyGood), catalog, policy, familyContext);
        var random = new AuthoritativeLegacyRandom(randomState);
        var itemIndex = SelectRandomItem(random, onlyGood, currentLevel, catalog);
        return new(itemIndex, random.State);
    }

    internal static AuthoritativeCathedralThemeItemWorldSelectionResult SelectTypeItemForWorld(
        uint randomState,
        int currentLevel,
        bool onlyGood,
        int itemType,
        int miscId,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ValidatePolicy(policy);
        return SelectTypeItemForWorld(randomState, currentLevel, onlyGood, itemType, miscId, catalog, policy,
            AuthoritativeNativeDungeonFamilyContext.Cathedral(policy.Depth));
    }

    internal static AuthoritativeCathedralThemeItemWorldSelectionResult SelectTypeItemForWorld(
        uint randomState,
        int currentLevel,
        bool onlyGood,
        int itemType,
        int miscId,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy,
        AuthoritativeNativeDungeonFamilyContext familyContext)
    {
        ValidateSelectionRequest(new AuthoritativeCathedralThemeItemSelectionRequest(
            randomState, currentLevel, onlyGood, itemType, miscId), catalog, policy, familyContext);
        ValidateRequestedItemType(itemType);
        var random = new AuthoritativeLegacyRandom(randomState);
        var itemIndex = itemType == ItemTypeGold
            ? FindGoldItem(catalog)
            : SelectTypeItem(random, itemType, miscId, currentLevel, onlyGood, catalog);
        return new(itemIndex, random.State);
    }

    private sealed class GenerationState
    {
        public AuthoritativeCathedralInitialItemState Item = new();
        public uint ItemSeed;
        public uint RandomState;
        public int ItemIndex;
        public bool Cleared;
    }

    private static GenerationState SetupCore(
        AuthoritativeCathedralThemeItemRequest request,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy,
        AuthoritativeNativeDungeonFamilyContext familyContext)
    {
        familyContext.RequireMatch(policy.Depth);
        var item = request.ResetBeforeGetItemAttrs
            ? new AuthoritativeCathedralInitialItemState()
            : CloneItem(request.InputItem ?? new AuthoritativeCathedralInitialItemState());
        var random = new AuthoritativeLegacyRandom(request.ItemSeed);
        item = GetItemAttrs(item, request.NativeItemIndex, request.Level / 2, random, catalog, policy);
        item = item with {
            Seed = request.ItemSeed,
            CreateInfo = checked((ushort)request.Level),
        };
        if (request.PreGenerate)
            item = item with { CreateInfo = (ushort)(item.CreateInfo | CreateInfoPregen) };
        if (request.OnlyGood)
            item = item with { CreateInfo = (ushort)(item.CreateInfo | CreateInfoOnlyGood) };
        if (request.UniqueChance == catalog.Rules.UniqueChanceUnique)
            item = item with { CreateInfo = (ushort)(item.CreateInfo | CreateInfoUper15) };
        else if (request.UniqueChance == catalog.Rules.UniqueChanceNormal)
            item = item with { CreateInfo = (ushort)(item.CreateInfo | CreateInfoUper1) };

        var result = new GenerationState {
            Item = item,
            ItemSeed = request.ItemSeed,
            RandomState = random.State,
            ItemIndex = request.NativeItemIndex,
        };
        if (item.MiscId == MiscUnique) {
            if (item.EquipLocation != EquipUnequipable) {
                var uniqueId = unchecked((int)request.ItemSeed);
                var unique = catalog.Uniques.FirstOrDefault(candidate => candidate.NativeUniqueId == uniqueId);
                if (request.ItemSeed > 109 || unique is null
                    || FindItem(catalog, request.NativeItemIndex).NativeItemId != unique.NativeBaseItemId) {
                    result.Item = item with { ItemType = ItemTypeNone };
                    result.Cleared = true;
                    return result;
                }
                result.Item = ApplyUnique(item, unique, random, catalog, request.Player);
            }
            result.RandomState = random.State;
            return result;
        }

        var baseLevel = GetItemBLevel(request.Level, item.MiscId, request.OnlyGood,
            request.UniqueChance == catalog.Rules.UniqueChanceUnique, random, catalog);
        if (baseLevel != -1) {
            var unique = request.ForceNotUnique
                ? null
                : CheckUnique(item, baseLevel, request.UniqueChance, request.UniqueOffset, random, catalog);
            if (request.ForceNotUnique)
                random.Discard(1);
            result.Item = unique is null
                ? GetItemBonus(item, baseLevel / 2, baseLevel, request.OnlyGood, random, catalog, request.Player)
                : ApplyUnique(item, unique, random, catalog, request.Player);
        }
        if (result.Item.Magical != QualityUnique)
            result.Item = ItemRndDur(result.Item, random);
        result.RandomState = random.State;
        return result;
    }

    private static AuthoritativeCathedralInitialItemState GetItemAttrs(
        AuthoritativeCathedralInitialItemState item,
        int itemIndex,
        int level,
        AuthoritativeLegacyRandom random,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy)
    {
        var baseItem = FindItem(catalog, itemIndex);
        var armor = checked(baseItem.MinimumArmorClass
            + GenerateRnd(random, checked(baseItem.MaximumArmorClass - baseItem.MinimumArmorClass + 1), "GetItemAttrs AC"));
        item = item with {
            ItemType = baseItem.ItemType,
            CursorGraphic = baseItem.CursorGraphic,
            BaseName = CopyNativeName(baseItem.Name),
            IdentifiedName = CopyNativeName(baseItem.Name),
            EquipLocation = baseItem.EquipLocation,
            ItemClass = baseItem.ItemClass,
            MinDamage = baseItem.MinimumDamage,
            MaxDamage = baseItem.MaximumDamage,
            ArmorClass = armor,
            Flags = baseItem.Flags,
            MiscId = baseItem.MiscId,
            SpellId = baseItem.SpellId,
            Magical = QualityNormal,
            Value = baseItem.Value,
            IdentifiedValue = baseItem.Value,
            Durability = baseItem.Durability,
            MaxDurability = baseItem.Durability,
            MinimumStrength = unchecked((sbyte)baseItem.MinimumStrength),
            MinimumMagic = baseItem.MinimumMagic,
            MinimumDexterity = unchecked((sbyte)baseItem.MinimumDexterity),
            ItemIndex = itemIndex,
            PrefixPower = -1,
            SuffixPower = -1,
        };

        if (baseItem.MiscId == MiscBook)
            item = GetBookSpell(item, level, random, catalog, policy);
        if (baseItem.ItemType == ItemTypeGold)
            item = GetGold(item, policy.CurrentDungeonLevel == 0 ? policy.Depth : policy.CurrentDungeonLevel,
                random, catalog);
        return item;
    }

    private static AuthoritativeCathedralInitialItemState GetGold(
        AuthoritativeCathedralInitialItemState item,
        int level,
        AuthoritativeLegacyRandom random,
        AuthoritativeCathedralThemeItemCatalog catalog)
    {
        var roll = checked(5 * level + GenerateRnd(random, checked(10 * level), "GetItemAttrs gold"));
        var value = Math.Min(roll, GoldMaxLimit);
        var cursor = value >= GoldMediumLimit
            ? catalog.Cursors.GoldLarge
            : value <= GoldSmallLimit ? catalog.Cursors.GoldSmall : catalog.Cursors.GoldMedium;
        return item with { Value = value, CursorGraphic = cursor };
    }

    private static AuthoritativeCathedralInitialItemState GetBookSpell(
        AuthoritativeCathedralInitialItemState item,
        int level,
        AuthoritativeLegacyRandom random,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy)
    {
        if (level == 0)
            level = 1;
        var roll = GenerateRnd(random, catalog.Spells.Count, "GetBookSpell spell") + 1;
        var spell = SelectSpell(roll, level, false, catalog);
        var name = CopyNativeName(item.BaseName);
        var identifiedName = CopyNativeName(item.IdentifiedName);
        name = CopyNativeName(name + spell.Name);
        identifiedName = CopyNativeName(identifiedName + spell.Name);
        var cursor = spell.MagicType switch {
            0 => catalog.Cursors.BookRed,
            1 => catalog.Cursors.BookBlue,
            _ => catalog.Cursors.BookGrey,
        };
        return item with {
            BaseName = name,
            IdentifiedName = identifiedName,
            SpellId = spell.NativeSpellId,
            MinimumMagic = spell.MinimumMagic,
            Value = checked(item.Value + spell.BookCost),
            IdentifiedValue = checked(item.IdentifiedValue + spell.BookCost),
            CursorGraphic = cursor,
        };
    }

    private static AuthoritativeCathedralNativeSpellDefinition SelectSpell(
        int roll,
        int level,
        bool staff,
        AuthoritativeCathedralThemeItemCatalog catalog)
    {
        if (roll <= 0)
            return staff ? catalog.Spells[0] : catalog.Spells[1];
        var remaining = roll;
        var spellId = 1;
        var maxIterations = checked(catalog.Spells.Count * catalog.Spells.Count + 1);
        for (var iteration = 0; iteration < maxIterations; iteration++) {
            var spell = catalog.Spells[spellId];
            var available = staff ? spell.AvailableInStaff && spell.StaffLevel >= 0
                : spell.AvailableInBook && spell.BookLevel >= 0;
            var spellLevel = staff ? spell.StaffLevel : spell.BookLevel;
            var selected = available && level >= spellLevel;
            if (selected)
                remaining--;
            if (selected && remaining <= 0)
                return spell;
            spellId++;
            if (spellId == 32)
                spellId = 33;
            if (spellId == 34)
                spellId = 35;
            if (spellId >= catalog.Spells.Count)
                spellId = 1;
        }
        throw new InvalidDataException("Native spell selection exceeded its bounded safety limit.");
    }

    private static int GetSpellLevel(
        AuthoritativeCathedralNativeSpellDefinition spell,
        bool staff)
        => staff ? spell.StaffLevel : spell.BookLevel;

    private static AuthoritativeCathedralInitialItemState GetItemBonus(
        AuthoritativeCathedralInitialItemState item,
        int minimumLevel,
        int maximumLevel,
        bool onlyGood,
        AuthoritativeLegacyRandom random,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPlayerContext? player)
    {
        minimumLevel = Math.Min(minimumLevel, 25);
        var affixType = item.ItemType switch {
            ItemTypeSword or ItemTypeAxe or ItemTypeMace => AffixWeapon,
            ItemTypeBow => AffixBow,
            ItemTypeShield => AffixShield,
            ItemTypeLightArmor or ItemTypeHelm or ItemTypeMediumArmor or ItemTypeHeavyArmor => AffixArmor,
            ItemTypeStaff => AffixStaff,
            ItemTypeRing or ItemTypeAmulet => AffixMisc,
            _ => 0,
        };
        if (item.ItemType == ItemTypeStaff)
            return GetStaffSpellOrPower(item, maximumLevel, onlyGood, random, catalog, player);
        if (affixType == 0)
            return item;
        return GetItemPower(item, minimumLevel, maximumLevel, affixType, onlyGood, random, catalog, player);
    }

    private static AuthoritativeCathedralInitialItemState GetStaffSpellOrPower(
        AuthoritativeCathedralInitialItemState item,
        int level,
        bool onlyGood,
        AuthoritativeLegacyRandom random,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPlayerContext? player)
    {
        if (GenerateRnd(random, 4, "GetStaffSpell branch") == 0)
            return GetItemPower(item, level / 2, level, AffixStaff, onlyGood, random, catalog, player);

        var spellLevel = level / 2;
        if (spellLevel == 0)
            spellLevel = 1;
        var roll = GenerateRnd(random, catalog.Spells.Count, "GetStaffSpell spell") + 1;
        var spell = SelectSpell(roll, spellLevel, true, catalog);
        var charges = checked(spell.StaffMinimumCharges
            + GenerateRnd(random, checked(spell.StaffMaximumCharges - spell.StaffMinimumCharges + 1), "GetStaffSpell charges"));
        var value = checked(item.Value + charges * spell.StaffCost / 5);
        item = item with {
            SpellId = spell.NativeSpellId,
            Charges = charges,
            MaxCharges = charges,
            MinimumMagic = spell.MinimumMagic,
            Value = value,
            IdentifiedValue = value,
        };
        return GetStaffPower(item, level, onlyGood, random, catalog, player);
    }

    private static AuthoritativeCathedralInitialItemState GetStaffPower(
        AuthoritativeCathedralInitialItemState item,
        int maximumLevel,
        bool onlyGood,
        AuthoritativeLegacyRandom random,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPlayerContext? player)
    {
        AuthoritativeCathedralNativeAffixDefinition? prefix = null;
        var staffPrefixRoll = GenerateRnd(random, 10, "GetStaffPrefix branch");
        if (staffPrefixRoll == 0 || onlyGood)
            prefix = SelectAffix(catalog.Prefixes, AffixStaff, 0, maximumLevel, onlyGood, GoeAny, false, random);
        if (prefix is not null) {
            item = ApplyAffix(item, prefix, true, random, catalog, player);
            item = item with { Magical = QualityMagic, PrefixPower = prefix.Power.Type };
        }
        var spell = catalog.Spells.FirstOrDefault(value => value.NativeSpellId == item.SpellId);
        if (spell is null)
            throw new InvalidDataException($"Staff spell {item.SpellId} is absent from the raw spell catalog.");
        var baseItem = FindItem(catalog, item.ItemIndex);
        var baseName = $"{baseItem.Name} of {spell.Name}";
        if (!StringInPanel(baseName, catalog))
            baseName = $"{baseItem.ShortName} of {spell.Name}";
        item = item with { BaseName = CopyNativeName(baseName) };
        if (prefix is null) {
            item = item with { IdentifiedName = item.BaseName };
        } else {
            var magicalName = $"{prefix.Name} {baseItem.Name} of {spell.Name}";
            if (!StringInPanel(magicalName, catalog))
                magicalName = $"{prefix.Name} {baseItem.ShortName} of {spell.Name}";
            item = item with { IdentifiedName = CopyNativeName(magicalName) };
        }
        return RecalculateValue(item);
    }

    private static AuthoritativeCathedralInitialItemState GetItemPower(
        AuthoritativeCathedralInitialItemState item,
        int minimumLevel,
        int maximumLevel,
        int affixType,
        bool onlyGood,
        AuthoritativeLegacyRandom random,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPlayerContext? player)
    {
        var allocatePrefix = RollAffixChance(random, catalog.Rules.PrefixPercent, "GetItemPower prefix allocation");
        var allocateSuffix = RollAffixChance(random, catalog.Rules.SuffixPercent, "GetItemPower suffix allocation");
        if (!allocatePrefix && !allocateSuffix) {
            if (GenerateRnd(random, 2, "GetItemPower fallback allocation") == 0)
                allocatePrefix = true;
            else
                allocateSuffix = true;
        }
        var effectiveOnlyGood = onlyGood;
        if (!effectiveOnlyGood)
            effectiveOnlyGood = RollAffixChance(random, catalog.Rules.OnlyGoodChance, "GetItemPower onlygood");
        var goe = GoeAny;
        AuthoritativeCathedralNativeAffixDefinition? prefix = null;
        AuthoritativeCathedralNativeAffixDefinition? suffix = null;
        if (allocatePrefix)
            prefix = SelectAffix(catalog.Prefixes, affixType, minimumLevel, maximumLevel,
                effectiveOnlyGood, goe, true, random);
        if (prefix is not null) {
            goe = prefix.Alignment;
            item = ApplyAffix(item, prefix, true, random, catalog, player);
            item = item with { Magical = QualityMagic, PrefixPower = prefix.Power.Type };
        }
        if (allocateSuffix)
            suffix = SelectAffix(catalog.Suffixes, affixType, minimumLevel, maximumLevel,
                effectiveOnlyGood, goe, true, random);
        if (suffix is not null) {
            item = ApplyAffix(item, suffix, false, random, catalog, player);
            item = item with { Magical = QualityMagic, SuffixPower = suffix.Power.Type };
        }
        var name = MakeMagicName(item.BaseName, prefix, suffix);
        item = item with { IdentifiedName = CopyNativeName(name) };
        if (!StringInPanel(item.IdentifiedName, catalog))
            item = item with {
                IdentifiedName = CopyNativeName(MakeMagicName(FindItem(catalog, item.ItemIndex).ShortName, prefix, suffix)),
            };
        if (prefix is not null || suffix is not null)
            item = RecalculateValue(item);
        return item;
    }

    private static AuthoritativeCathedralNativeAffixDefinition? SelectAffix(
        IReadOnlyList<AuthoritativeCathedralNativeAffixDefinition> source,
        int itemType,
        int minimumLevel,
        int maximumLevel,
        bool onlyGood,
        int goe,
        bool excludeStaffCharges,
        AuthoritativeLegacyRandom random)
    {
        var weighted = new List<AuthoritativeCathedralNativeAffixDefinition>();
        foreach (var affix in source) {
            if ((affix.ItemTypeFlags & itemType) == 0 || affix.MinimumLevel < minimumLevel
                || affix.MinimumLevel > maximumLevel || onlyGood && !affix.Useful
                || (goe == GoeGood && affix.Alignment == GoeEvil)
                || (goe == GoeEvil && affix.Alignment == GoeGood)
                || excludeStaffCharges && itemType == AffixStaff && affix.Power.Type == PowerCharges)
                continue;
            for (var index = 0; index < affix.Chance; index++)
                weighted.Add(affix);
        }
        if (weighted.Count == 0)
            return null;
        if (weighted.Count > 256)
            throw new InvalidDataException("Native SelectAffix exceeded its bounded eligible-affix storage.");
        var selected = GenerateRnd(random, weighted.Count, "SelectAffix");
        if (selected < 0 || selected >= weighted.Count)
            throw new InvalidDataException("Native SelectAffix produced an unsafe signed index.");
        return weighted[selected];
    }

    private static AuthoritativeCathedralNativeUniqueDefinition? CheckUnique(
        AuthoritativeCathedralInitialItemState item,
        int level,
        int chance,
        int uniqueOffset,
        AuthoritativeLegacyRandom random,
        AuthoritativeCathedralThemeItemCatalog catalog)
    {
        if (GenerateRnd(random, 100, "CheckUnique chance") > chance)
            return null;
        var baseItemId = FindItem(catalog, item.ItemIndex).NativeItemId;
        var valid = catalog.Uniques
            .Where(unique => unique.NativeBaseItemId == baseItemId && level >= unique.MinimumLevel)
            .ToArray();
        if (valid.Length == 0)
            return null;
        random.Discard(1);
        if (uniqueOffset < 0 || uniqueOffset >= valid.Length)
            return null;
        return valid[valid.Length - 1 - uniqueOffset];
    }

    private static int GetItemBLevel(
        int level,
        int miscId,
        bool onlyGood,
        bool unique15,
        AuthoritativeLegacyRandom random,
        AuthoritativeCathedralThemeItemCatalog catalog)
    {
        var alwaysMagic = catalog.AlwaysMagicMiscIds.Contains(miscId);
        var baseMagic = GenerateRnd(random, 100, "GetItemBLevel base") <= catalog.Rules.MagicChanceBase;
        var levelMagic = !baseMagic
            && GenerateRnd(random, 100, "GetItemBLevel level") <= level * catalog.Rules.MagicChancePerLevel;
        if (baseMagic || levelMagic || onlyGood || alwaysMagic)
            return unique15 ? checked(level + catalog.Rules.BonusLevelsUnique) : level;
        if (unique15)
            return checked(level + catalog.Rules.BonusLevelsUnique);
        return -1;
    }

    private static AuthoritativeCathedralInitialItemState ItemRndDur(
        AuthoritativeCathedralInitialItemState item,
        AuthoritativeLegacyRandom random)
    {
        if (item.Durability > 0 && item.Durability != byte.MaxValue)
            item = item with {
                Durability = checked(GenerateRnd(random, item.MaxDurability / 2, "ItemRndDur")
                    + item.MaxDurability / 4 + 1),
            };
        return item;
    }

    private sealed class MutablePowerState
    {
        public int PlusDamage;
        public int PlusToHit;
        public int PlusArmorClass;
        public int ArmorClass;
        public int PlusStrength;
        public int PlusMagic;
        public int PlusDexterity;
        public int PlusVitality;
        public int PlusFireResistance;
        public int PlusLightningResistance;
        public int PlusMagicResistance;
        public int PlusMana;
        public int PlusHitPoints;
        public int PlusDamageModifier;
        public int PlusGetHit;
        public int PlusLight;
        public int SpellLevelAdd;
        public int SpellId;
        public int Charges;
        public int MaxCharges;
        public int Durability;
        public int MaxDurability;
        public int FireMinDamage;
        public int FireMaxDamage;
        public int LightningMinDamage;
        public int LightningMaxDamage;
        public int PlusEnemyArmorClass;
        public int MinimumStrength;
        public int EquipLocation;
        public uint Flags;
        public int HellfireDamageArmorFlags;

        public MutablePowerState(AuthoritativeCathedralInitialItemState item)
        {
            PlusDamage = item.PlusDamage;
            PlusToHit = item.PlusToHit;
            PlusArmorClass = item.PlusArmorClass;
            ArmorClass = item.ArmorClass;
            PlusStrength = item.PlusStrength;
            PlusMagic = item.PlusMagic;
            PlusDexterity = item.PlusDexterity;
            PlusVitality = item.PlusVitality;
            PlusFireResistance = item.PlusFireResistance;
            PlusLightningResistance = item.PlusLightningResistance;
            PlusMagicResistance = item.PlusMagicResistance;
            PlusMana = item.PlusMana;
            PlusHitPoints = item.PlusHitPoints;
            PlusDamageModifier = item.PlusDamageModifier;
            PlusGetHit = item.PlusGetHit;
            PlusLight = item.PlusLight;
            SpellLevelAdd = item.SpellLevelAdd;
            SpellId = item.SpellId;
            Charges = item.Charges;
            MaxCharges = item.MaxCharges;
            Durability = item.Durability;
            MaxDurability = item.MaxDurability;
            FireMinDamage = item.FireMinDamage;
            FireMaxDamage = item.FireMaxDamage;
            LightningMinDamage = item.LightningMinDamage;
            LightningMaxDamage = item.LightningMaxDamage;
            PlusEnemyArmorClass = item.PlusEnemyArmorClass;
            MinimumStrength = item.MinimumStrength;
            EquipLocation = item.EquipLocation;
            Flags = item.Flags;
            HellfireDamageArmorFlags = item.HellfireDamageArmorFlags;
        }

        public AuthoritativeCathedralInitialItemState Apply(AuthoritativeCathedralInitialItemState item)
            => item with {
                PlusDamage = PlusDamage, PlusToHit = PlusToHit, PlusArmorClass = PlusArmorClass,
                ArmorClass = ArmorClass, PlusStrength = PlusStrength, PlusMagic = PlusMagic,
                PlusDexterity = PlusDexterity, PlusVitality = PlusVitality,
                PlusFireResistance = PlusFireResistance, PlusLightningResistance = PlusLightningResistance,
                PlusMagicResistance = PlusMagicResistance, PlusMana = PlusMana,
                PlusHitPoints = PlusHitPoints, PlusDamageModifier = PlusDamageModifier,
                PlusGetHit = PlusGetHit, PlusLight = PlusLight, SpellLevelAdd = SpellLevelAdd,
                SpellId = SpellId, Charges = Charges, MaxCharges = MaxCharges,
                Durability = Durability, MaxDurability = MaxDurability,
                FireMinDamage = FireMinDamage, FireMaxDamage = FireMaxDamage,
                LightningMinDamage = LightningMinDamage, LightningMaxDamage = LightningMaxDamage,
                PlusEnemyArmorClass = PlusEnemyArmorClass, MinimumStrength = MinimumStrength,
                EquipLocation = EquipLocation, Flags = Flags,
                HellfireDamageArmorFlags = HellfireDamageArmorFlags,
            };
    }

    /** Apply the native storage widths after a power writes the scalar Item fields. */
    private static AuthoritativeCathedralInitialItemState NarrowNativeStorage(
        AuthoritativeCathedralInitialItemState item)
    {
        var animation = item.Animation with {
            TicksPerFrame = unchecked((sbyte)item.Animation.TicksPerFrame),
            TickCounterOfCurrentFrame = unchecked((sbyte)item.Animation.TickCounterOfCurrentFrame),
            NumberOfFrames = unchecked((sbyte)item.Animation.NumberOfFrames),
            CurrentFrame = unchecked((sbyte)item.Animation.CurrentFrame),
        };
        return item with {
            Animation = animation,
            ItemType = unchecked((sbyte)item.ItemType),
            EquipLocation = unchecked((sbyte)item.EquipLocation),
            ItemClass = unchecked((byte)item.ItemClass),
            CursorGraphic = unchecked((byte)item.CursorGraphic),
            MinDamage = unchecked((byte)item.MinDamage),
            MaxDamage = unchecked((byte)item.MaxDamage),
            ArmorClass = unchecked((short)item.ArmorClass),
            MiscId = unchecked((sbyte)item.MiscId),
            SpellId = unchecked((sbyte)item.SpellId),
            ItemIndex = unchecked((short)item.ItemIndex),
            Magical = unchecked((byte)item.Magical),
            SelectionRegion = unchecked((byte)item.SelectionRegion),
            PlusDamage = unchecked((short)item.PlusDamage),
            PlusToHit = unchecked((short)item.PlusToHit),
            PlusArmorClass = unchecked((short)item.PlusArmorClass),
            PlusStrength = unchecked((short)item.PlusStrength),
            PlusMagic = unchecked((short)item.PlusMagic),
            PlusDexterity = unchecked((short)item.PlusDexterity),
            PlusVitality = unchecked((short)item.PlusVitality),
            PlusFireResistance = unchecked((short)item.PlusFireResistance),
            PlusLightningResistance = unchecked((short)item.PlusLightningResistance),
            PlusMagicResistance = unchecked((short)item.PlusMagicResistance),
            PlusMana = unchecked((short)item.PlusMana),
            PlusHitPoints = unchecked((short)item.PlusHitPoints),
            PlusDamageModifier = unchecked((short)item.PlusDamageModifier),
            PlusGetHit = unchecked((short)item.PlusGetHit),
            PlusLight = unchecked((short)item.PlusLight),
            SpellLevelAdd = unchecked((sbyte)item.SpellLevelAdd),
            FireMinDamage = unchecked((short)item.FireMinDamage),
            FireMaxDamage = unchecked((short)item.FireMaxDamage),
            LightningMinDamage = unchecked((short)item.LightningMinDamage),
            LightningMaxDamage = unchecked((short)item.LightningMaxDamage),
            PlusEnemyArmorClass = unchecked((short)item.PlusEnemyArmorClass),
            PrefixPower = unchecked((sbyte)item.PrefixPower),
            SuffixPower = unchecked((sbyte)item.SuffixPower),
            MinimumStrength = unchecked((sbyte)item.MinimumStrength),
            MinimumMagic = unchecked((byte)item.MinimumMagic),
            MinimumDexterity = unchecked((sbyte)item.MinimumDexterity),
            HellfireDamageArmorFlags = unchecked((byte)item.HellfireDamageArmorFlags),
        };
    }

    private static AuthoritativeCathedralInitialItemState ApplyAffix(
        AuthoritativeCathedralInitialItemState item,
        AuthoritativeCathedralNativeAffixDefinition affix,
        bool prefix,
        AuthoritativeLegacyRandom random,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPlayerContext? player)
    {
        var saved = SaveItemPower(item, affix.Power, random, player);
        item = NarrowNativeStorage(saved.Item);
        var value = saved.Value;
        value = PLVal(value, affix.Power.Param1, affix.Power.Param2, affix.MinimumValue, affix.MaximumValue);
        item = item.ValueAdd1 != 0 || item.ValueMultiply1 != 0
            ? item with { ValueAdd2 = value, ValueMultiply2 = affix.ValueMultiplier }
            : item with { ValueAdd1 = value, ValueMultiply1 = affix.ValueMultiplier };
        return item;
    }

    private static (AuthoritativeCathedralInitialItemState Item, int Value) SaveItemPower(
        AuthoritativeCathedralInitialItemState item,
        AuthoritativeCathedralNativeItemPower power,
        AuthoritativeLegacyRandom random,
        AuthoritativeCathedralThemeItemPlayerContext? player)
    {
        var value = RndPl(power.Param1, power.Param2, random);
        var result = new MutablePowerState(item);
        switch (power.Type) {
        case PowerToHit: result.PlusToHit += value; break;
        case PowerToHitCurse: result.PlusToHit -= value; break;
        case PowerDamp: result.PlusDamage += value; break;
        case PowerDampCurse: result.PlusDamage -= value; break;
        case PowerDoppelganger:
            result.HellfireDamageArmorFlags |= 1 << 4;
            goto case PowerToHitDamp;
        case PowerToHitDamp:
            value = RndPl(power.Param1, power.Param2, random);
            result.PlusDamage += value;
            result.PlusToHit += CalculateToHitBonus(power.Param1, random);
            break;
        case PowerToHitDampCurse:
            result.PlusDamage -= value;
            result.PlusToHit += CalculateToHitBonus(-power.Param1, random);
            break;
        case PowerAcp: result.PlusArmorClass += value; break;
        case PowerAcpCurse: result.PlusArmorClass -= value; break;
        case PowerSetArmor: result.ArmorClass = value; break;
        case PowerArmorCurse: result.ArmorClass -= value; break;
        case PowerFireRes: result.PlusFireResistance += value; break;
        case PowerLightRes: result.PlusLightningResistance += value; break;
        case PowerMagicRes: result.PlusMagicResistance += value; break;
        case PowerAllRes:
            result.PlusFireResistance = Math.Max(result.PlusFireResistance + value, 0);
            result.PlusLightningResistance = Math.Max(result.PlusLightningResistance + value, 0);
            result.PlusMagicResistance = Math.Max(result.PlusMagicResistance + value, 0);
            break;
        case PowerSpellLevel: result.SpellLevelAdd = value; break;
        case PowerCharges:
            result.Charges = checked(result.Charges * power.Param1);
            result.MaxCharges = result.Charges;
            break;
        case PowerSpell:
            result.SpellId = power.Param1;
            result.Charges = power.Param2;
            result.MaxCharges = power.Param2;
            break;
        case PowerFireDamage:
            result.Flags |= 1U << 4;
            result.Flags &= ~(1U << 5);
            result.FireMinDamage = power.Param1;
            result.FireMaxDamage = power.Param2;
            result.LightningMinDamage = 0;
            result.LightningMaxDamage = 0;
            break;
        case PowerLightningDamage:
            result.Flags |= 1U << 5;
            result.Flags &= ~(1U << 4);
            result.LightningMinDamage = power.Param1;
            result.LightningMaxDamage = power.Param2;
            result.FireMinDamage = 0;
            result.FireMaxDamage = 0;
            break;
        case PowerStrength: result.PlusStrength += value; break;
        case PowerStrengthCurse: result.PlusStrength -= value; break;
        case PowerMagic: result.PlusMagic += value; break;
        case PowerMagicCurse: result.PlusMagic -= value; break;
        case PowerDexterity: result.PlusDexterity += value; break;
        case PowerDexterityCurse: result.PlusDexterity -= value; break;
        case PowerVitality: result.PlusVitality += value; break;
        case PowerVitalityCurse: result.PlusVitality -= value; break;
        case PowerAttributes:
            result.PlusStrength += value; result.PlusMagic += value; result.PlusDexterity += value; result.PlusVitality += value;
            break;
        case PowerAttributesCurse:
            result.PlusStrength -= value; result.PlusMagic -= value; result.PlusDexterity -= value; result.PlusVitality -= value;
            break;
        case PowerGetHitCurse: result.PlusGetHit += value; break;
        case PowerGetHit: result.PlusGetHit -= value; break;
        case PowerLife: result.PlusHitPoints += value << 6; break;
        case PowerLifeCurse: result.PlusHitPoints -= value << 6; break;
        case PowerMana: result.PlusMana += value << 6; break;
        case PowerManaCurse: result.PlusMana -= value << 6; break;
        case PowerDurability: {
            var bonus = value * result.MaxDurability / 100;
            result.Durability += bonus;
            result.MaxDurability += bonus;
        } break;
        case PowerCrystalline:
            result.PlusDamage += 140 + value * 2;
            goto case PowerDurabilityCurse;
        case PowerDurabilityCurse:
            result.MaxDurability = Math.Max(
                (int)unchecked((byte)(result.MaxDurability - value * result.MaxDurability / 100)), 1);
            result.Durability = result.MaxDurability;
            break;
        case PowerIndestructible: result.Durability = byte.MaxValue; result.MaxDurability = byte.MaxValue; break;
        case PowerLight: result.PlusLight += power.Param1; break;
        case PowerLightCurse: result.PlusLight -= power.Param1; break;
        case PowerMultipleArrows: result.Flags |= 1U << 9; break;
        case PowerFireArrows:
            result.Flags |= (1U << 3) | (1U << 4); result.Flags &= ~(1U << 25);
            result.FireMinDamage = power.Param1; result.FireMaxDamage = power.Param2; result.LightningMinDamage = 0; result.LightningMaxDamage = 0;
            break;
        case PowerLightningArrows:
            result.Flags |= (1U << 25) | (1U << 5); result.Flags &= ~(1U << 3);
            result.LightningMinDamage = power.Param1; result.LightningMaxDamage = power.Param2; result.FireMinDamage = 0; result.FireMaxDamage = 0;
            break;
        case PowerFireball:
            result.Flags |= (1U << 25) | (1U << 3); result.FireMinDamage = power.Param1; result.FireMaxDamage = power.Param2;
            result.LightningMinDamage = 0; result.LightningMaxDamage = 0;
            break;
        case PowerThorns: result.Flags |= 1U << 26; break;
        case PowerNoMana: result.Flags |= 1U << 27; break;
        case PowerHalfTrap: result.Flags |= 1U << 28; break;
        case PowerKnockback: result.Flags |= 1U << 11; break;
        case PowerStealMana:
            if (power.Param1 == 3) result.Flags |= 1U << 13;
            if (power.Param1 == 5) result.Flags |= 1U << 14;
            break;
        case PowerStealLife:
            if (power.Param1 == 3) result.Flags |= 1U << 15;
            if (power.Param1 == 5) result.Flags |= 1U << 16;
            break;
        case PowerTargetArmor: result.PlusEnemyArmorClass += value; break;
        case PowerFastAttack:
            result.Flags |= power.Param1 switch { 1 => 1U << 17, 2 => 1U << 18, 3 => 1U << 19, 4 => 1U << 20, _ => 0 };
            break;
        case PowerFastRecover:
            result.Flags |= power.Param1 switch { 1 => 1U << 21, 2 => 1U << 22, 3 => 1U << 23, _ => 0 };
            break;
        case PowerFastBlock: result.Flags |= 1U << 24; break;
        case PowerDamageModifier: result.PlusDamageModifier += value; break;
        case PowerRandomArrowVelocity: result.Flags |= 1U << 2; break;
        case PowerSetDamage: item = item with { MinDamage = power.Param1, MaxDamage = power.Param2 }; break;
        case PowerSetDurability: result.Durability = power.Param1; result.MaxDurability = power.Param1; break;
        case PowerNoMinimumStrength: result.MinimumStrength = 0; break;
        case PowerOneHand: result.EquipLocation = 1; break;
        case PowerTripleDemonDamage: result.Flags |= 1U << 30; break;
        case PowerAllResZero: result.Flags |= 1U << 31; break;
        case PowerDrainLife: result.Flags |= 1U << 6; break;
        case PowerRandomStealLife: result.Flags |= 1U << 1; break;
        case PowerAddArmorLife:
            result.Flags |= (1U << 25) | (1U << 3); result.FireMinDamage = power.Param1; result.FireMaxDamage = power.Param2; result.LightningMinDamage = 1; result.LightningMaxDamage = 0;
            break;
        case PowerAddManaArmor:
            result.Flags |= (1U << 5) | (1U << 4); result.FireMinDamage = power.Param1; result.FireMaxDamage = power.Param2; result.LightningMinDamage = 2; result.LightningMaxDamage = 0;
            break;
        case PowerFireResCurse: result.PlusFireResistance -= value; break;
        case PowerLightResCurse: result.PlusLightningResistance -= value; break;
        case PowerMagicResCurse: result.PlusMagicResistance -= value; break;
        case PowerDevastation: result.HellfireDamageArmorFlags |= 1; break;
        case PowerDecay: result.HellfireDamageArmorFlags |= 2; result.PlusDamage += value; break;
        case PowerPeril: result.HellfireDamageArmorFlags |= 4; break;
        case PowerJesters: result.HellfireDamageArmorFlags |= 8; break;
        case PowerAgainstDemons: result.HellfireDamageArmorFlags |= 32; break;
        case PowerAgainstUndead: result.HellfireDamageArmorFlags |= 64; break;
        case PowerManaToLife:
            if (player is null)
                throw new InvalidDataException("Native mana-to-life item power requires player base mana metadata.");
            result.PlusMana -= ((player?.MaximumManaBase ?? 0) >> 6) * 50 / 100 << 6;
            result.PlusHitPoints += ((player?.MaximumManaBase ?? 0) >> 6) * 50 / 100 << 6;
            break;
        case PowerLifeToMana:
            if (player is null)
                throw new InvalidDataException("Native life-to-mana item power requires player base life metadata.");
            result.PlusHitPoints -= ((player?.MaximumLifeBase ?? 0) >> 6) * 40 / 100 << 6;
            result.PlusMana += ((player?.MaximumLifeBase ?? 0) >> 6) * 40 / 100 << 6;
            break;
        case -1:
            break;
        default:
            throw new InvalidDataException($"Native item power {power.Type} is unsupported by the ordinary Diablo generator.");
        }
        return (NarrowNativeStorage(result.Apply(item)), value);
    }

    private static AuthoritativeCathedralInitialItemState ApplyUnique(
        AuthoritativeCathedralInitialItemState item,
        AuthoritativeCathedralNativeUniqueDefinition unique,
        AuthoritativeLegacyRandom random,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPlayerContext? player)
    {
        foreach (var power in unique.Powers) {
            if (power.Type == -1)
                break;
            item = SaveItemPower(item, power, random, player).Item;
        }
        return item with {
            Seed = item.MiscId == MiscUnique ? unchecked((uint)unique.NativeUniqueId) : item.Seed,
            IdentifiedName = CopyNativeName(unique.Name),
            CursorGraphic = unique.CursorGraphic == byte.MaxValue ? item.CursorGraphic : unique.CursorGraphic,
            IdentifiedValue = unique.Value,
            UniqueId = unique.NativeUniqueId,
            Magical = QualityUnique,
            CreateInfo = (ushort)(item.CreateInfo | CreateInfoUnique),
        };
    }

    private static GenerationState TryRandomUnique(
        GenerationState generation,
        AuthoritativeCathedralThemeItemRequest request,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy,
        HashSet<int> uniqueFlags,
        AuthoritativeNativeDungeonFamilyContext familyContext)
    {
        if ((generation.Item.CreateInfo & CreateInfoUnique) == 0 || generation.Item.MiscId == MiscUnique)
            return generation;
        var random = new AuthoritativeLegacyRandom(generation.ItemSeed);
        random.Discard(1);
        var baseLevel = GetItemBLevel(request.Level, generation.Item.MiscId, request.OnlyGood,
            request.UniqueChance == catalog.Rules.UniqueChanceUnique, random, catalog);
        var baseItemId = FindItem(catalog, generation.ItemIndex).NativeItemId;
        var valid = catalog.Uniques.Where(unique => unique.NativeBaseItemId == baseItemId && baseLevel >= unique.MinimumLevel).ToArray();
        if (valid.Length == 0)
            throw new InvalidDataException("Native TryRandomUniqueItem found no valid unique for a flagged item.");
        var available = valid.Where(unique => policy.GameMode == DiabloGameMode.Multiplayer
                || !uniqueFlags.Contains(unique.NativeUniqueId)).ToArray();
        if (available.Length == 0) {
            var level = request.Level;
            var chance = catalog.Rules.UniqueChanceNormal;
            if (request.UniqueChance == catalog.Rules.UniqueChanceUnique) {
                level = checked(level + catalog.Rules.BonusLevelsUnique);
            }
            return RegenerateUntilNonUnique(generation, request, catalog, policy, level, chance, uniqueFlags,
                familyContext);
        }
        var selectedIndex = Math.Max(GenerateRnd(random, available.Length, "TryRandomUniqueItem unique"), 0);
        if (selectedIndex >= available.Length)
            throw new InvalidDataException("Native TryRandomUniqueItem produced an unsafe unique index.");
        var selected = available[selectedIndex];
        if (generation.Item.UniqueId == selected.NativeUniqueId) {
            if (policy.GameMode != DiabloGameMode.Multiplayer)
                uniqueFlags.Add(selected.NativeUniqueId);
            generation.RandomState = random.State;
            return generation;
        }
        var selectedValidIndex = Array.FindIndex(valid, unique => unique.NativeUniqueId == selected.NativeUniqueId);
        if (selectedValidIndex < 0)
            throw new InvalidDataException("Native TryRandomUniqueItem selected a unique absent from the valid list.");
        var uidOffset = valid.Length - selectedValidIndex - 1;
        var canReverse = true;
        for (var index = selectedValidIndex + 1; index < valid.Length; index++)
            if (valid[index].MinimumLevel <= selected.MinimumLevel)
                canReverse = false;
        if (canReverse) {
            var targetLevel = 1;
            var chance = catalog.Rules.UniqueChanceNormal;
            if (selected.MinimumLevel - catalog.Rules.BonusLevelsUnique > 0) {
                chance = catalog.Rules.UniqueChanceUnique;
                targetLevel = selected.MinimumLevel - catalog.Rules.BonusLevelsUnique;
            } else {
                targetLevel = selected.MinimumLevel;
            }
            generation = RegenerateUntilUnique(generation, request, catalog, policy, selected.NativeUniqueId,
                targetLevel, chance, uniqueFlags, familyContext);
        } else {
            var oldPositionX = generation.Item.PositionX;
            var oldPositionY = generation.Item.PositionY;
            var newRequest = request with { UniqueOffset = uidOffset, ResetBeforeGetItemAttrs = true };
            generation = SetupCore(newRequest, catalog, policy, familyContext);
            generation.Item = generation.Item with { PositionX = oldPositionX, PositionY = oldPositionY };
            generation.Item = generation.Item with { Buff = generation.Item.Buff | ((uint)(uidOffset << 1) & 0x1EU) };
        }
        if (policy.GameMode != DiabloGameMode.Multiplayer)
            uniqueFlags.Add(selected.NativeUniqueId);
        return generation;
    }

    private static GenerationState RegenerateUntilNonUnique(
        GenerationState generation,
        AuthoritativeCathedralThemeItemRequest request,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy,
        int level,
        int chance,
        HashSet<int> flags,
        AuthoritativeNativeDungeonFamilyContext familyContext)
    {
        var itemGenerator = new AuthoritativeLegacyRandom(generation.ItemSeed);
        for (var attempt = 0; attempt < 10000; attempt++) {
            var seed = unchecked((uint)itemGenerator.AdvanceRndSeed());
            var oldPositionX = generation.Item.PositionX;
            var oldPositionY = generation.Item.PositionY;
            var regenerated = request with { ItemSeed = seed, Level = level, UniqueChance = chance,
                UniqueOffset = 0, ForceNotUnique = false, ResetBeforeGetItemAttrs = true,
                UniqueItemFlags = flags };
            generation = SetupCore(regenerated, catalog, policy, familyContext);
            generation.Item = generation.Item with { PositionX = oldPositionX, PositionY = oldPositionY };
            if (generation.Item.Magical != QualityUnique)
                return generation;
        }
        throw new InvalidDataException("Native TryRandomUniqueItem exceeded the bounded non-unique retry limit.");
    }

    private static GenerationState RegenerateUntilUnique(
        GenerationState generation,
        AuthoritativeCathedralThemeItemRequest request,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy,
        int uniqueId,
        int level,
        int chance,
        HashSet<int> flags,
        AuthoritativeNativeDungeonFamilyContext familyContext)
    {
        var itemGenerator = new AuthoritativeLegacyRandom(generation.ItemSeed);
        for (var attempt = 0; attempt < 10000; attempt++) {
            var seed = unchecked((uint)itemGenerator.AdvanceRndSeed());
            var oldPositionX = generation.Item.PositionX;
            var oldPositionY = generation.Item.PositionY;
            var regenerated = request with { ItemSeed = seed, Level = level, UniqueChance = chance,
                UniqueOffset = 0, ForceNotUnique = false, OnlyGood = true,
                ResetBeforeGetItemAttrs = true, UniqueItemFlags = flags };
            generation = SetupCore(regenerated, catalog, policy, familyContext);
            generation.Item = generation.Item with { PositionX = oldPositionX, PositionY = oldPositionY };
            if (generation.Item.UniqueId == uniqueId)
                return generation;
        }
        throw new InvalidDataException("Native TryRandomUniqueItem exceeded the bounded unique retry limit.");
    }

    private static AuthoritativeCathedralInitialItemState SetupItem(
        AuthoritativeCathedralInitialItemState item,
        int itemIndex,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPlayerContext? player,
        AuthoritativeCathedralThemeItemPolicy policy)
    {
        var cursor = item.CursorGraphic;
        if (!catalog.CursorAnimations.TryGetValue(cursor, out var animation))
            throw new InvalidDataException($"Missing animation metadata for generated cursor {cursor}.");
        var showAnimation = player?.PlayerPresent == true && player.LevelLoading == false;
        var frame = showAnimation ? 0 : animation.FrameCount - 1;
        return item with {
            Animation = new AuthoritativeCathedralInitialItemAnimation {
                TicksPerFrame = animation.TicksPerFrame,
                TickCounterOfCurrentFrame = 0,
                NumberOfFrames = animation.FrameCount,
                CurrentFrame = frame,
                IsPetrified = false,
            },
            AnimFlag = showAnimation,
            SelectionRegion = showAnimation ? SelectionNone : SelectionBottom,
            PostDraw = false,
            Request = false,
            Identified = false,
        };
    }

    private static int SelectRandomItem(
        AuthoritativeLegacyRandom random,
        bool onlyGood,
        int currentLevel,
        AuthoritativeCathedralThemeItemCatalog catalog)
    {
        if (!onlyGood && GenerateRnd(random, 100, "RndAllItems gold") >= 100 - catalog.Rules.GoldPercent)
            return FindGoldItem(catalog);
        return SelectDroppable(random, currentLevel, onlyGood, -1, -1, catalog, onlyGood);
    }

    private static int SelectTypeItem(
        AuthoritativeLegacyRandom random,
        int itemType,
        int miscId,
        int currentLevel,
        bool onlyGood,
        AuthoritativeCathedralThemeItemCatalog catalog)
        => SelectDroppable(random, currentLevel, onlyGood, itemType, miscId, catalog, false);

    private static int SelectDroppable(
        AuthoritativeLegacyRandom random,
        int currentLevel,
        bool onlyGood,
        int itemType,
        int miscId,
        AuthoritativeCathedralThemeItemCatalog catalog,
        bool allowGoodBookBypass)
    {
        var maximumLevel = checked(currentLevel * 2);
        var candidates = catalog.Items.Where(item => item.Available && item.DropRate > 0
                && (allowGoodBookBypass && onlyGood && item.ItemType == ItemTypeMisc && item.MiscId == MiscBook
                    || item.MinimumMonsterLevel <= maximumLevel)
                && (item.SpellId is not 32 and not 34)
                && (itemType < 0
                    ? onlyGood
                        ? item.ItemType != ItemTypeGold && (item.ItemType != ItemTypeMisc || item.MiscId == MiscBook)
                        : true
                    : item.ItemType == itemType && (miscId == -1 || item.MiscId == miscId)))
            .ToArray();
        if (candidates.Length == 0)
            throw new InvalidDataException("Native item selection has no eligible row.");
        var total = checked(candidates.Length);
        var selected = random.RandomIntLessThan(total);
        if (selected < 0 || selected >= candidates.Length)
            throw new InvalidDataException("Native item selection produced an unsafe signed index.");
        return candidates[selected].NativeItemIndex;
    }

    private static int FindGoldItem(AuthoritativeCathedralThemeItemCatalog catalog)
    {
        var gold = catalog.Items.SingleOrDefault(item => item.NativeItemIndex == 0);
        if (gold is null || gold.ItemType != ItemTypeGold || !gold.Available
            || gold.CursorGraphic == byte.MaxValue)
            throw new InvalidDataException(
                "Native gold selection requires an available Gold ItemData row at IDI_GOLD index 0.");
        return 0;
    }

    private static AuthoritativeCathedralNativeItemDefinition FindUsefulItem(
        AuthoritativeCathedralThemeItemCatalog catalog,
        int nativeItemIndex,
        string expectedName,
        int expectedMiscId,
        int cursorGraphic,
        int minimumMonsterLevel = 0,
        int spellId = 0)
    {
        var item = catalog.Items.SingleOrDefault(value => value.NativeItemIndex == nativeItemIndex);
        if (item is null
            || item.NativeItemId != 0
            || item.ItemType != ItemTypeMisc
            || item.MiscId != expectedMiscId
            || item.CursorGraphic != cursorGraphic
            || item.SpellId != spellId
            || !string.Equals(item.Name, expectedName, StringComparison.Ordinal)
            || !string.IsNullOrEmpty(item.ShortName)
            || !item.Available
            || !item.Usable
            || item.DropRate != 0
            || item.MinimumMonsterLevel != minimumMonsterLevel)
            throw new InvalidDataException(
                $"Native chest useful contents require the canonical available item row at IDI index {nativeItemIndex}.");
        return item;
    }

    private static void ValidateRequestedItemType(int itemType)
    {
        if (itemType < 0)
            throw new InvalidDataException("Native typed item selection cannot use ItemType::None.");
    }

    private static string MakeMagicName(
        string baseName,
        AuthoritativeCathedralNativeAffixDefinition? prefix,
        AuthoritativeCathedralNativeAffixDefinition? suffix)
    {
        if (prefix is not null && suffix is not null)
            return $"{prefix.Name} {baseName} of {suffix.Name}";
        if (prefix is not null)
            return $"{prefix.Name} {baseName}";
        if (suffix is not null)
            return $"{baseName} of {suffix.Name}";
        return baseName;
    }

    private static int RndPl(int minimum, int maximum, AuthoritativeLegacyRandom random)
        => checked(minimum + GenerateRnd(random, checked(maximum - minimum + 1), "RndPL"));

    private static int CalculateToHitBonus(int level, AuthoritativeLegacyRandom random)
        => level switch {
            -50 => -RndPl(6, 10, random),
            -25 => -RndPl(1, 5, random),
            20 => RndPl(1, 5, random),
            36 => RndPl(6, 10, random),
            51 => RndPl(11, 15, random),
            66 => RndPl(16, 20, random),
            81 => RndPl(21, 30, random),
            96 => RndPl(31, 40, random),
            111 => RndPl(41, 50, random),
            126 => RndPl(51, 75, random),
            151 => RndPl(76, 100, random),
            _ => throw new InvalidDataException($"Unknown native to-hit bonus level {level}."),
        };

    private static int PLVal(int value, int powerMinimum, int powerMaximum, int minimum, int maximum)
    {
        if (powerMinimum == powerMaximum || minimum == maximum)
            return minimum;
        return checked(minimum + (maximum - minimum) * (100 * (value - powerMinimum) / (powerMaximum - powerMinimum)) / 100);
    }

    private static AuthoritativeCathedralInitialItemState RecalculateValue(AuthoritativeCathedralInitialItemState item)
    {
        var multiplier = item.ValueMultiply1 + item.ValueMultiply2;
        var value = multiplier > 0
            ? unchecked(multiplier * item.Value)
            : multiplier < 0 ? item.Value / multiplier : 0;
        value = unchecked(item.ValueAdd1 + item.ValueAdd2 + value);
        return item with { IdentifiedValue = Math.Max(value, 1) };
    }

    private static string CopyNativeName(string value)
    {
        if (value is null)
            throw new InvalidDataException("Native item names cannot be null.");
        var terminator = value.IndexOf('\0');
        if (terminator >= 0)
            value = value[..terminator];
        byte[] bytes;
        try {
            bytes = new UTF8Encoding(false, true).GetBytes(value);
        } catch (EncoderFallbackException exception) {
            throw new InvalidDataException("Native item name is not valid UTF-8.", exception);
        }
        var length = Math.Min(bytes.Length, ItemNameBytes - 1);
        while (length > 0 && length < bytes.Length && (bytes[length] & 0xC0) == 0x80)
            length--;
        return new UTF8Encoding(false, true).GetString(bytes, 0, length);
    }

    private static bool StringInPanel(string value, AuthoritativeCathedralThemeItemCatalog catalog)
    {
        var bytes = new UTF8Encoding(false, true).GetBytes(value);
        var width = 0;
        var offset = 0;
        while (offset < bytes.Length) {
            if (System.Text.Rune.DecodeFromUtf8(bytes.AsSpan(offset), out var rune, out var consumed)
                != OperationStatus.Done || consumed <= 0)
                break;
            offset += consumed;
            if (rune.Value == '\uFFFD')
                break;
            if (rune.Value == ZeroWidthSpace)
                continue;
            if (rune.Value == '\n')
                break;
            var glyphWidth = catalog.GlyphWidths.TryGetValue(rune.Value, out var known)
                ? known
                : catalog.ReplacementGlyphWidth;
            width = checked(width + glyphWidth + 2);
        }
        return (width == 0 ? 0 : width - 2) < 254;
    }

    /// <summary>
    /// Rolls a configured affix chance with the vanilla dice for the shipped values, as native
    /// RollAffixChance does: one in four for 25 and two in three for 66.
    /// </summary>
    private static bool RollAffixChance(AuthoritativeLegacyRandom random, int percent, string operation)
        => percent switch {
            25 => GenerateRnd(random, 4, operation) == 0,
            66 => GenerateRnd(random, 3, operation) != 0,
            _ => GenerateRnd(random, 100, operation) < percent,
        };

    private static int GenerateRnd(AuthoritativeLegacyRandom random, int limit, string operation)
    {
        if (limit <= 0)
            return 0;
        return random.GenerateRnd(limit);
    }

    private static AuthoritativeCathedralNativeItemDefinition FindItem(
        AuthoritativeCathedralThemeItemCatalog catalog,
        int nativeItemIndex)
        => catalog.Items.SingleOrDefault(item => item.NativeItemIndex == nativeItemIndex)
            ?? throw new InvalidDataException($"Native item index {nativeItemIndex} is absent from the raw catalog.");

    private static void ValidateRequest(
        AuthoritativeCathedralThemeItemRequest request,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy,
        AuthoritativeNativeDungeonFamilyContext familyContext)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalog);
        ValidatePolicy(policy, familyContext);
        if (request.NativeItemIndex < 0 || !catalog.Items.Any(item => item.NativeItemIndex == request.NativeItemIndex))
            throw new InvalidDataException("The requested native item index is absent from the raw catalog.");
        if (FindItem(catalog, request.NativeItemIndex).CursorGraphic == byte.MaxValue)
            throw new InvalidDataException("Native placeholder item rows cannot be generated as ordinary items.");
        if (request.Level is < 0 or > 63 || request.UniqueChance is < 0 or > 100)
            throw new InvalidDataException("Native SetupAllItems scalar bounds are invalid.");
        ValidateInputState(request.InputItem);
        ValidateUniqueFlags(request.UniqueItemFlags);
    }

    private static void ValidateSelectionRequest(
        AuthoritativeCathedralThemeItemSelectionRequest request,
        AuthoritativeCathedralThemeItemCatalog catalog,
        AuthoritativeCathedralThemeItemPolicy policy,
        AuthoritativeNativeDungeonFamilyContext familyContext)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalog);
        ValidatePolicy(policy, familyContext);
        if (request.CurrentLevel != policy.Depth)
            throw new InvalidDataException(familyContext.IsCatacombs
                ? "Catacombs item selection requires the matching current depth."
                : "Cathedral item selection requires current level 1 through 4.");
        ValidateInputState(request.InputItem);
        ValidateUniqueFlags(request.UniqueItemFlags);
    }

    private static void ValidatePolicy(AuthoritativeCathedralThemeItemPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.Depth is < 1 or > 4 || policy.CurrentDungeonLevel is < 0 or > 4
            || policy.GameMode != DiabloGameMode.SinglePlayer || policy.Difficulty != DiabloDifficulty.Normal
            || policy.Hellfire || policy.Spawn || policy.QuestEnabled || policy.SetLevel
            || !policy.LegacyLevelItemGenerationAllowed)
            throw new InvalidDataException("Only ordinary original-Diablo Cathedral SP Normal generation is supported.");
        if (policy.CurrentDungeonLevel != 0 && policy.CurrentDungeonLevel != policy.Depth)
            throw new InvalidDataException("Current dungeon level must match the requested Cathedral depth.");
    }

    private static void ValidatePolicy(
        AuthoritativeCathedralThemeItemPolicy policy,
        AuthoritativeNativeDungeonFamilyContext familyContext)
    {
        ArgumentNullException.ThrowIfNull(policy);
        familyContext.RequireMatch(policy.Depth);
        var maximumDepth = familyContext.IsCatacombs ? 8 : 4;
        var minimumDepth = familyContext.IsCatacombs ? 5 : 1;
        if (policy.Depth < minimumDepth || policy.Depth > maximumDepth
            || policy.CurrentDungeonLevel < 0 || policy.CurrentDungeonLevel > maximumDepth
            || policy.GameMode != DiabloGameMode.SinglePlayer || policy.Difficulty != DiabloDifficulty.Normal
            || policy.Hellfire || policy.Spawn || policy.QuestEnabled || policy.SetLevel
            || !policy.LegacyLevelItemGenerationAllowed)
            throw new InvalidDataException(familyContext.IsCatacombs
                ? "Only ordinary original-Diablo Catacombs SP Normal generation is supported."
                : "Only ordinary original-Diablo Cathedral SP Normal generation is supported.");
        if (policy.CurrentDungeonLevel != 0 && policy.CurrentDungeonLevel != policy.Depth)
            throw new InvalidDataException(familyContext.IsCatacombs
                ? "Current dungeon level must match the requested Catacombs depth."
                : "Current dungeon level must match the requested Cathedral depth.");
    }

    private static HashSet<int> CopyFlags(IReadOnlySet<int>? flags)
        => flags is null ? [] : flags.ToHashSet();

    private static void ValidateInputState(AuthoritativeCathedralInitialItemState? item)
    {
        if (item is null)
            return;
        AuthoritativeCathedralInitialItemGenerator.ValidateItemProjection(item);
        if (item.UniqueId is < 0 or > 127)
            throw new InvalidDataException("Native item unique IDs must address IDs 0 through 127.");
    }

    private static void ValidateUniqueFlags(IReadOnlySet<int>? flags)
    {
        if (flags is not null && flags.Any(uniqueId => uniqueId is < 0 or > 127))
            throw new InvalidDataException("Native unique history flags must address IDs 0 through 127.");
    }

    private static AuthoritativeCathedralInitialItemState CloneItem(AuthoritativeCathedralInitialItemState item)
        => item with { Animation = item.Animation with { } };

    private static AuthoritativeCathedralThemeItemResult ToResult(GenerationState generation, HashSet<int> flags)
    {
        var item = CloneItem(generation.Item);
        AuthoritativeCathedralInitialItemGenerator.ValidateItemProjection(item);
        return new(item, generation.ItemSeed, generation.RandomState, generation.ItemIndex,
            generation.Cleared, new ReadOnlySet<int>(flags));
    }

    private const int ItemTypeNone = -1;

    private sealed class ReadOnlySet<T> : ReadOnlyCollection<T>, IReadOnlySet<T>
    {
        private readonly HashSet<T> values;

        public ReadOnlySet(IEnumerable<T> values)
            : base(values.ToArray())
        {
            this.values = new HashSet<T>(this);
        }

        public new bool Contains(T item) => values.Contains(item);
        public bool IsProperSubsetOf(IEnumerable<T> other) => values.IsProperSubsetOf(other);
        public bool IsProperSupersetOf(IEnumerable<T> other) => values.IsProperSupersetOf(other);
        public bool IsSubsetOf(IEnumerable<T> other) => values.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<T> other) => values.IsSupersetOf(other);
        public bool Overlaps(IEnumerable<T> other) => values.Overlaps(other);
        public bool SetEquals(IEnumerable<T> other) => values.SetEquals(other);
    }
}

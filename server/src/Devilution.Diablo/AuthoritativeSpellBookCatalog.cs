using Devilution.Data;
using Devilution.Engine;
using Devilution.Shared;

namespace Devilution.Diablo;

/** Native spell school used to select a spell-book cursor projection. */
public enum AuthoritativeSpellBookMagicType
{
    Magic,
    Fire,
    Lightning,
}

/** External metadata for one spell that can be generated as a book. */
public sealed record AuthoritativeSpellBookDefinition(
    uint SpellId,
    string DisplayName,
    int BookLevel,
    int BookCost,
    AuthoritativeSpellBookMagicType MagicType,
    int MinimumMagic)
{
    /** Native cursor graphic selected from the spell's external cursor symbol. */
    public int NativeCursorGraphic { get; init; } = MagicType switch {
        AuthoritativeSpellBookMagicType.Magic => 86,
        AuthoritativeSpellBookMagicType.Fire => 87,
        AuthoritativeSpellBookMagicType.Lightning => 88,
        _ => throw new InvalidOperationException(),
    };

    /** Optional content-owned single-player book exclusion. */
    public bool? SinglePlayerExcluded { get; init; }
}

/** Validated external spell-book metadata and deterministic item projection. */
public sealed class AuthoritativeSpellBookCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "spell_id",
        "spell_symbol",
        "display_name",
        "book_level",
        "book_cost",
        "magic_type",
        "minimum_magic",
        "cursor_symbol",
        "single_player_excluded",
    ];

    private const int BookMiscId = 25;
    private const uint FirstNativeSpellId = 1;
    private const uint MaximumNativeSpellId = NativeCompatibilityLimits.MaximumNativeSpellId;
    private const uint NativeResurrectSpellId = 32;
    private const uint NativeHealOtherSpellId = 34;
    private readonly Dictionary<uint, AuthoritativeSpellBookDefinition> definitions;
    private IReadOnlySet<uint> singlePlayerExcludedSpellIds = new HashSet<uint>();
    private IReadOnlyDictionary<uint, AuthoritativeSpellBookDefinition> nativeDefinitions
        = new Dictionary<uint, AuthoritativeSpellBookDefinition>();
    private IReadOnlyDictionary<uint, uint> nativeSpellIdsByStableId
        = new Dictionary<uint, uint>();
    private uint nativeSpellTableSize;

    public AuthoritativeSpellBookCatalog(
        IEnumerable<AuthoritativeSpellBookDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        this.definitions = new Dictionary<uint, AuthoritativeSpellBookDefinition>();
        foreach (var definition in definitions) {
            if (definition.SpellId == 0
                || string.IsNullOrWhiteSpace(definition.DisplayName)
                || definition.DisplayName.Contains('\0')
                || definition.BookLevel <= 0
                || definition.BookCost < 0
                || definition.MinimumMagic < 0
                || definition.NativeCursorGraphic is < 0 or > byte.MaxValue
                || !Enum.IsDefined(definition.MagicType)) {
                throw new InvalidDataException("Spell-book definitions contain invalid bounds or presentation data.");
            }
            if (!this.definitions.TryAdd(definition.SpellId, definition))
                throw new InvalidDataException(
                    $"Spell {definition.SpellId} has more than one spell-book definition.");
        }
        if (this.definitions.Count == 0)
            throw new InvalidDataException("The spell-book catalog cannot be empty.");
    }

    public IReadOnlyList<AuthoritativeSpellBookDefinition> Definitions
        => definitions.Values.OrderBy(definition => definition.SpellId).ToArray();

    /** Rejects partial spell-book metadata at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production spell-book table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows)
            foreach (var column in ProductionColumns)
                _ = row.Required(column);
    }

    public bool TryGet(uint spellId, out AuthoritativeSpellBookDefinition definition)
        => definitions.TryGetValue(spellId, out definition!);

    /** Returns whether an item definition is one of the native book templates. */
    public bool IsBookTemplate(AuthoritativeItemCatalog items, uint itemId)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items.TryGet(itemId, out var definition)
            && IsBookTemplate(definition);
    }

    /** Validates spell references and discovers the native book templates. */
    public void Validate(
        AuthoritativeSpellCatalog spells,
        AuthoritativeItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(items);
        var templates = GetBookTemplates(items);
        if (templates.Count == 0)
            throw new InvalidDataException(
                "Spell-book metadata requires at least one item tagged Book.");
        singlePlayerExcludedSpellIds = spells.Definitions
            .Where(spell => spell.ProjectileBehavior is
                AuthoritativeProjectileBehavior.Resurrection
                or AuthoritativeProjectileBehavior.HealOther)
            .Select(spell => spell.SpellId)
            .ToHashSet();
        var validatedNativeDefinitions = new Dictionary<uint, AuthoritativeSpellBookDefinition>();
        var validatedNativeSpellIds = new Dictionary<uint, uint>();
        var maximumNativeSpellId = FirstNativeSpellId;
        var nativeSelectionSupported = true;
        foreach (var spell in spells.Definitions) {
            if (spell.NativeSpellId != 0)
                maximumNativeSpellId = Math.Max(maximumNativeSpellId, spell.NativeSpellId);
        }
        foreach (var definition in Definitions) {
            if (!spells.TryGet(definition.SpellId, out var spell))
                throw new InvalidDataException(
                    $"Spell-book definition references unknown spell {definition.SpellId}.");
            var nativeSpellId = spell.NativeSpellId == 0
                ? definition.SpellId
                : spell.NativeSpellId;
            if (nativeSpellId is < FirstNativeSpellId or > MaximumNativeSpellId
                || !validatedNativeDefinitions.TryAdd(nativeSpellId, definition)) {
                // Packs authored before native spell IDs were externalized
                // retain the stable-order fallback below.
                nativeSelectionSupported = false;
            } else {
                validatedNativeSpellIds[definition.SpellId] = nativeSpellId;
            }
            maximumNativeSpellId = Math.Max(maximumNativeSpellId, nativeSpellId);
        }
        if (nativeSelectionSupported) {
            nativeDefinitions = validatedNativeDefinitions;
            nativeSpellIdsByStableId = validatedNativeSpellIds;
            nativeSpellTableSize = checked(maximumNativeSpellId + 1);
        } else {
            nativeDefinitions = new Dictionary<uint, AuthoritativeSpellBookDefinition>();
            nativeSpellIdsByStableId = new Dictionary<uint, uint>();
            nativeSpellTableSize = 0;
        }
    }

    /**
     * Generates one server-owned dynamic book using native level eligibility,
     * spell presentation, intelligence requirement, cost, and cursor school.
     */
    public AuthoritativeItemState Generate(
        AuthoritativeItemCatalog items,
        uint spellId,
        uint itemSeed,
        int itemLevel = 1)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (itemSeed == 0)
            throw new ArgumentOutOfRangeException(nameof(itemSeed));
        if (itemLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(itemLevel));
        if (!TryGet(spellId, out var book))
            throw new InvalidDataException(
                $"Spell {spellId} is not available as a dynamic book.");

        var eligible = GetEligibleBookTemplates(items, itemLevel);
        if (eligible.Count == 0)
            throw new InvalidDataException(
                $"No spell-book template is eligible at item level {itemLevel}.");

        var template = eligible[new DeterministicLegacyRandom(itemSeed).Next(eligible.Count)];
        return GenerateFromTemplate(items, spellId, itemSeed, book, template);
    }

    private static AuthoritativeItemState GenerateFromTemplate(
        AuthoritativeItemCatalog items,
        uint spellId,
        uint itemSeed,
        AuthoritativeSpellBookDefinition book,
        (uint ItemId, AuthoritativeItemDefinition Definition) template)
    {
        var state = items.Generate(template.ItemId, itemSeed);
        var value = checked(state.Value + book.BookCost);
        var name = $"Book of {book.DisplayName}";
        return state with {
            Value = value,
            IdentifiedValue = checked(state.IdentifiedValue + book.BookCost),
            SpellId = checked((int)spellId),
            ItemActionKind = DiabloItemActionKind.ReadBook,
            MinimumMagic = book.MinimumMagic,
            BaseName = name,
            IdentifiedName = name,
            NativeCursorGraphic = book.NativeCursorGraphic,
        };
    }

    /** Generates a deterministic native-style book by selecting an eligible spell. */
    public AuthoritativeItemState GenerateRandom(
        AuthoritativeItemCatalog items,
        uint itemSeed,
        int itemLevel = 1,
        DiabloGameMode gameMode = DiabloGameMode.SinglePlayer,
        uint? templateItemId = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (itemSeed == 0)
            throw new ArgumentOutOfRangeException(nameof(itemSeed));
        if (itemLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(itemLevel));
        if (!Enum.IsDefined(gameMode))
            throw new ArgumentOutOfRangeException(nameof(gameMode));

        var maximumNativeLevel = Math.Max(1, itemLevel);
        var eligible = Definitions
            .Where(definition => definition.BookLevel <= maximumNativeLevel
                && IsAvailableInGameMode(definition, gameMode))
            .ToArray();
        if (eligible.Length == 0)
            throw new InvalidDataException(
                $"No spell is eligible for a book at item level {itemLevel}.");

        var spell = SelectRandomSpell(eligible, itemSeed);
        if (templateItemId is uint requestedTemplateItemId)
        {
            var template = GetEligibleBookTemplates(items, itemLevel)
                .SingleOrDefault(candidate => candidate.ItemId == requestedTemplateItemId);
            if (template.ItemId == 0)
                throw new InvalidDataException(
                    $"Spell-book template {requestedTemplateItemId} is not eligible at item level {itemLevel}.");
            return GenerateFromTemplate(items, spell.SpellId, itemSeed, spell, template);
        }
        return Generate(items, spell.SpellId, itemSeed, itemLevel);
    }

    private static IReadOnlyList<(uint ItemId, AuthoritativeItemDefinition Definition)> GetEligibleBookTemplates(
        AuthoritativeItemCatalog items,
        int itemLevel)
    {
        var maximumItemLevel = Math.Max(1, itemLevel);
        return GetBookTemplates(items)
            .Where(template => template.Definition.BookTemplateMinimumItemLevel >= 0
                ? template.Definition.BookTemplateMinimumItemLevel <= maximumItemLevel
                : template.Definition.MinimumMonsterLevel <= checked(maximumItemLevel * 2))
            .ToArray();
    }

    private bool IsAvailableInGameMode(
        AuthoritativeSpellBookDefinition definition,
        DiabloGameMode gameMode)
    {
        if (gameMode == DiabloGameMode.Multiplayer)
            return true;
        if (definition.SinglePlayerExcluded is { } singlePlayerExcluded)
            return !singlePlayerExcluded;
        if (singlePlayerExcludedSpellIds.Contains(definition.SpellId))
            return false;
        return !nativeSpellIdsByStableId.TryGetValue(definition.SpellId, out var nativeSpellId)
            || nativeSpellId is not NativeResurrectSpellId and not NativeHealOtherSpellId;
    }

    private AuthoritativeSpellBookDefinition SelectRandomSpell(
        IReadOnlyList<AuthoritativeSpellBookDefinition> eligible,
        uint itemSeed)
    {
        if (nativeDefinitions.Count == 0)
            return eligible[new DeterministicLegacyRandom(itemSeed).Next(eligible.Count)];

        var remaining = new DeterministicLegacyRandom(itemSeed).Next(checked((int)nativeSpellTableSize)) + 1;
        var nativeSpellId = FirstNativeSpellId;
        while (true) {
            if (nativeDefinitions.TryGetValue(nativeSpellId, out var definition)
                && eligible.Contains(definition)) {
                remaining--;
                if (remaining == 0)
                    return definition;
            }
            nativeSpellId++;
            if (nativeSpellId >= nativeSpellTableSize)
                nativeSpellId = FirstNativeSpellId;
        }
    }

    public static AuthoritativeSpellBookCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null,
        AuthoritativeItemCursorCatalog? cursorCatalog = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeSpellBookCatalog(table.Rows.Select(row => {
            var magicType = ParseMagicType(row);
            var definition = new AuthoritativeSpellBookDefinition(
                ResolveSpellId(row, stableIds),
                row.Required("display_name"),
                row.RequiredInt32("book_level"),
                row.RequiredInt32("book_cost"),
                magicType,
                row.RequiredInt32("minimum_magic")) {
                SinglePlayerExcluded = ParseOptionalSinglePlayerExcluded(row),
            };
            if (cursorCatalog is not null
                && row.TryGet("cursor_symbol", out var cursorSymbol)
                && !string.IsNullOrWhiteSpace(cursorSymbol)) {
                definition = definition with {
                    NativeCursorGraphic = cursorCatalog.Resolve(cursorSymbol),
                };
            }
            return definition;
        }));
    }

    private static uint ResolveSpellId(TsvRow row, StableContentIdCatalog? stableIds)
    {
        var numericId = row.RequiredUInt32("spell_id");
        if (!row.TryGet("spell_symbol", out var symbol)
            || string.IsNullOrWhiteSpace(symbol))
            return numericId;
        if (stableIds is null) {
            if (numericId == 0)
                throw new InvalidDataException(
                    $"Spell-book row {row.LineNumber} requires stable IDs to resolve '{symbol}'.");
            return numericId;
        }
        uint resolvedId;
        try {
            resolvedId = stableIds.Resolve("spell", symbol);
        } catch (KeyNotFoundException exception) {
            throw new InvalidDataException(
                $"Spell-book row {row.LineNumber} references unknown spell symbol '{symbol}'.",
                exception);
        }
        if (numericId != 0 && numericId != resolvedId)
            throw new InvalidDataException(
                $"Spell-book row {row.LineNumber} maps spell {numericId} to symbol '{symbol}', "
                + $"which resolves to {resolvedId}.");
        return resolvedId;
    }

    private static AuthoritativeSpellBookMagicType ParseMagicType(TsvRow row)
    {
        var value = row.Required("magic_type");
        if (Enum.TryParse<AuthoritativeSpellBookMagicType>(value, true, out var result)
            && Enum.IsDefined(result))
            return result;
        throw new InvalidDataException(
            $"Spell-book row {row.LineNumber} contains unknown magic type '{value}'.");
    }

    private static bool? ParseOptionalSinglePlayerExcluded(TsvRow row)
    {
        if (!row.TryGet("single_player_excluded", out var value)
            || string.IsNullOrWhiteSpace(value))
            return null;
        return row.OptionalBoolean("single_player_excluded");
    }

    private static bool IsBookTemplate(AuthoritativeItemDefinition definition)
        => definition.GenerationTags.Count > 0
            ? definition.GenerationTags.Contains("Book", StringComparer.OrdinalIgnoreCase)
            : definition.MiscId == BookMiscId;

    private static IReadOnlyList<(uint ItemId, AuthoritativeItemDefinition Definition)> GetBookTemplates(
        AuthoritativeItemCatalog items)
    {
        return items.Definitions
            .Where(IsBookTemplate)
            .OrderBy(definition => definition.MinimumMonsterLevel)
            .ThenBy(definition => definition.ItemId)
            .Select(definition => (definition.ItemId, definition))
            .ToArray();
    }
}

using Devilution.Data;

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
    /** Native cursor graphic selected from the spell's magic school. */
    public int NativeCursorGraphic => MagicType switch {
        AuthoritativeSpellBookMagicType.Magic => 86,
        AuthoritativeSpellBookMagicType.Fire => 87,
        AuthoritativeSpellBookMagicType.Lightning => 88,
        _ => throw new InvalidOperationException(),
    };
}

/** Validated external spell-book metadata and deterministic item projection. */
public sealed class AuthoritativeSpellBookCatalog
{
    private const int BookMiscId = 25;
    private readonly Dictionary<uint, AuthoritativeSpellBookDefinition> definitions;

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

    public bool TryGet(uint spellId, out AuthoritativeSpellBookDefinition definition)
        => definitions.TryGetValue(spellId, out definition!);

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
        foreach (var definition in Definitions) {
            if (!spells.TryGet(definition.SpellId, out _))
                throw new InvalidDataException(
                    $"Spell-book definition references unknown spell {definition.SpellId}.");
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

        var templates = GetBookTemplates(items);
        var maximumNativeLevel = Math.Max(1, itemLevel) * 2;
        var eligible = templates
            .Where(template => template.Definition.MinimumMonsterLevel <= maximumNativeLevel)
            .ToArray();
        if (eligible.Length == 0)
            throw new InvalidDataException(
                $"No spell-book template is eligible at item level {itemLevel}.");

        var template = eligible[new AuthoritativeLegacyRandom(itemSeed).Next(eligible.Length)];
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

    public static AuthoritativeSpellBookCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeSpellBookCatalog(table.Rows.Select(row => new AuthoritativeSpellBookDefinition(
            ResolveSpellId(row, stableIds),
            row.Required("display_name"),
            row.RequiredInt32("book_level"),
            row.RequiredInt32("book_cost"),
            ParseMagicType(row),
            row.RequiredInt32("minimum_magic"))));
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

    private static IReadOnlyList<(uint ItemId, AuthoritativeItemDefinition Definition)> GetBookTemplates(
        AuthoritativeItemCatalog items)
    {
        return items.Definitions
            .Where(definition => definition.MiscId == BookMiscId
                && definition.GenerationTags.Contains("Book", StringComparer.OrdinalIgnoreCase))
            .OrderBy(definition => definition.MinimumMonsterLevel)
            .ThenBy(definition => definition.ItemId)
            .Select(definition => (definition.ItemId, definition))
            .ToArray();
    }
}

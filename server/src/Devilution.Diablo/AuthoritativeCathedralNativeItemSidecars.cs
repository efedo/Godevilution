using Devilution.Data;

namespace Devilution.Diablo;

/** Verified cursor dimensions captured from the matching native item sprites. */
internal sealed record AuthoritativeCathedralNativeItemFootprint(
    int NativeItemIndex,
    int NativeCursorId,
    int PixelWidth,
    int PixelHeight,
    int InventoryWidth,
    int InventoryHeight);

/** Closed native-to-stable item semantics used only by the opt-in Cathedral profile. */
internal sealed record AuthoritativeCathedralNativeItemSemantic(
    int NativeItemIndex,
    string StableItemSymbol,
    int NativeCursorId,
    int InventoryWidth,
    int InventoryHeight,
    int MiscId,
    string SpellSourcePolicy,
    DiabloItemActionKind ItemActionKind);

internal sealed class AuthoritativeCathedralNativeItemSidecars
{
    private static readonly string[] FootprintColumns = [
        "native_item_index",
        "native_cursor_id",
        "pixel_width",
        "pixel_height",
        "inventory_width",
        "inventory_height",
    ];

    private static readonly string[] SemanticColumns = [
        "native_item_index",
        "stable_item_symbol",
        "native_cursor_id",
        "inventory_width",
        "inventory_height",
        "misc_id",
        "spell_source_policy",
        "item_action_kind",
    ];

    private AuthoritativeCathedralNativeItemSidecars(
        IReadOnlyDictionary<int, AuthoritativeCathedralNativeItemFootprint> footprints,
        IReadOnlyDictionary<int, AuthoritativeCathedralNativeItemSemantic> semantics)
    {
        Footprints = footprints;
        Semantics = semantics;
    }

    public IReadOnlyDictionary<int, AuthoritativeCathedralNativeItemFootprint> Footprints { get; }

    public IReadOnlyDictionary<int, AuthoritativeCathedralNativeItemSemantic> Semantics { get; }

    public static AuthoritativeCathedralNativeItemSidecars Load(TsvTable footprintsTable, TsvTable semanticsTable)
    {
        ArgumentNullException.ThrowIfNull(footprintsTable);
        ArgumentNullException.ThrowIfNull(semanticsTable);
        var footprints = LoadFootprints(footprintsTable);
        var semantics = ParseSemantics(semanticsTable);
        if (semantics.Keys.Any(index => !footprints.ContainsKey(index)))
            throw new InvalidDataException("Native Cathedral item semantics may select only captured cursor footprints.");
        return new(footprints, semantics);
    }

    internal static IReadOnlyDictionary<int, AuthoritativeCathedralNativeItemFootprint> LoadFootprints(TsvTable table)
    {
        if (!string.Equals(table.SourcePath, "native-rogue-l1-inventory.tsv", StringComparison.Ordinal)
            || !table.Columns.SequenceEqual(FootprintColumns, StringComparer.Ordinal))
            throw new InvalidDataException("Native item footprint table must use its exact six-column schema.");
        var result = new Dictionary<int, AuthoritativeCathedralNativeItemFootprint>();
        foreach (var row in table.Rows) {
            var footprint = new AuthoritativeCathedralNativeItemFootprint(
                row.RequiredInt32("native_item_index"),
                row.RequiredInt32("native_cursor_id"),
                row.RequiredInt32("pixel_width"),
                row.RequiredInt32("pixel_height"),
                row.RequiredInt32("inventory_width"),
                row.RequiredInt32("inventory_height"));
            if (footprint.NativeItemIndex < 0 || footprint.NativeCursorId <= 0
                || footprint.PixelWidth <= 0 || footprint.PixelHeight <= 0
                || footprint.PixelWidth > ushort.MaxValue || footprint.PixelHeight > ushort.MaxValue
                || footprint.InventoryWidth <= 0 || footprint.InventoryHeight <= 0
                || footprint.InventoryWidth != footprint.PixelWidth / 28
                || footprint.InventoryHeight != footprint.PixelHeight / 28
                || !result.TryAdd(footprint.NativeItemIndex, footprint))
                throw new InvalidDataException("Native item footprint rows must be unique and match floor-divided cursor dimensions.");
        }
        if (result.Count == 0)
            throw new InvalidDataException("Native item footprint table cannot be empty.");
        return new System.Collections.ObjectModel.ReadOnlyDictionary<int, AuthoritativeCathedralNativeItemFootprint>(result);
    }

    private static IReadOnlyDictionary<int, AuthoritativeCathedralNativeItemSemantic> ParseSemantics(TsvTable table)
    {
        if (!string.Equals(table.SourcePath, "native_item_semantics.tsv", StringComparison.Ordinal)
            || !table.Columns.SequenceEqual(SemanticColumns, StringComparer.Ordinal))
            throw new InvalidDataException("Native item semantics table must use its exact eight-column schema.");
        var result = new Dictionary<int, AuthoritativeCathedralNativeItemSemantic>();
        foreach (var row in table.Rows) {
            var actionName = row.Required("item_action_kind");
            if (!Enum.TryParse<DiabloItemActionKind>(actionName, ignoreCase: false, out var action)
                || !Enum.IsDefined(action) || !string.Equals(actionName, action.ToString(), StringComparison.Ordinal))
                throw new InvalidDataException("Native item semantics contain an unknown item action.");
            var semantic = new AuthoritativeCathedralNativeItemSemantic(
                row.RequiredInt32("native_item_index"),
                row.Required("stable_item_symbol"),
                row.RequiredInt32("native_cursor_id"),
                row.RequiredInt32("inventory_width"),
                row.RequiredInt32("inventory_height"),
                row.RequiredInt32("misc_id"),
                row.Required("spell_source_policy"),
                action);
            if (semantic.NativeItemIndex < 0 || semantic.NativeCursorId <= 0 || semantic.MiscId < 0
                || semantic.InventoryWidth <= 0 || semantic.InventoryHeight <= 0
                || semantic.SpellSourcePolicy is not ("catalog" or "native-staff-dynamic")
                || semantic.StableItemSymbol.Any(char.IsWhiteSpace)
                || !result.TryAdd(semantic.NativeItemIndex, semantic))
                throw new InvalidDataException("Native item semantic rows must be unique and use supported closed values.");
        }
        if (result.Count == 0)
            throw new InvalidDataException("Native item semantics table cannot be empty.");
        return new System.Collections.ObjectModel.ReadOnlyDictionary<int, AuthoritativeCathedralNativeItemSemantic>(result);
    }
}

using System.Text;
using Devilution.Data;

namespace Devilution.Diablo;

/** C2-only semantic bridge for the four native attribute elixirs. */
internal static class AuthoritativeCatacombsNativeItemBindings
{
    private sealed record ElixirIdentity(
        int NativeIndex,
        int NativeMiscId,
        int StableMiscId,
        uint StableItemId,
        string StableSymbol,
        DiabloItemActionKind Action);

    private static readonly ElixirIdentity[] Elixirs = [
        new(87, 10, 11, 2190, "elixir_of_strength_standard", DiabloItemActionKind.IncreaseStrength),
        new(88, 11, 12, 2191, "elixir_of_magic_standard", DiabloItemActionKind.IncreaseMagic),
        new(89, 12, 13, 2192, "elixir_of_dexterity_standard", DiabloItemActionKind.IncreaseDexterity),
        new(90, 13, 14, 2193, "elixir_of_vitality_standard", DiabloItemActionKind.IncreaseVitality),
    ];

    internal static TsvTable AppendElixirSemantics(
        TsvTable cathedralSemantics,
        AuthoritativeCathedralThemeItemCatalog nativeItems,
        AuthoritativeItemCatalog stableItems,
        IReadOnlyDictionary<int, AuthoritativeCathedralNativeItemFootprint> footprints)
    {
        ArgumentNullException.ThrowIfNull(cathedralSemantics);
        ArgumentNullException.ThrowIfNull(nativeItems);
        ArgumentNullException.ThrowIfNull(stableItems);
        ArgumentNullException.ThrowIfNull(footprints);
        if (!string.Equals(cathedralSemantics.SourcePath, "native_item_semantics.tsv", StringComparison.Ordinal)
            || !cathedralSemantics.Columns.SequenceEqual([
                "native_item_index", "stable_item_symbol", "native_cursor_id", "inventory_width",
                "inventory_height", "misc_id", "spell_source_policy", "item_action_kind"], StringComparer.Ordinal))
            throw new InvalidDataException("The Catacombs elixir extension requires the canonical native item semantic table.");

        var output = new StringBuilder(cathedralSemantics.ToCanonicalTsv());
        foreach (var identity in Elixirs) {
            if (cathedralSemantics.Rows.Any(row => row.RequiredInt32("native_item_index") == identity.NativeIndex))
                throw new InvalidDataException("Catacombs elixir semantics cannot replace an existing item identity.");
            var native = nativeItems.Items.SingleOrDefault(value => value.NativeItemIndex == identity.NativeIndex)
                ?? throw new InvalidDataException($"Native Catacombs elixir index {identity.NativeIndex} has no raw item row.");
            if (!footprints.TryGetValue(identity.NativeIndex, out var footprint)
                || footprint.NativeCursorId != checked(native.CursorGraphic + 12))
                throw new InvalidDataException($"Native Catacombs elixir index {identity.NativeIndex} has no matching cursor footprint.");
            var candidates = stableItems.Definitions.Where(value => MatchesElixir(native, value, stableSpellId: 0)
                && value.ItemId == identity.StableItemId
                && string.Equals(value.StableSymbol, identity.StableSymbol, StringComparison.Ordinal))
                .ToArray();
            if (candidates.Length != 1)
                throw new InvalidDataException($"Catacombs elixir index {identity.NativeIndex} must match one canonical stable item.");

            output.Append(identity.NativeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\t')
                .Append(identity.StableSymbol).Append('\t')
                .Append(footprint.NativeCursorId.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\t')
                .Append(footprint.InventoryWidth.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\t')
                .Append(footprint.InventoryHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\t')
                .Append(identity.StableMiscId.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("\tcatalog\t")
                .Append(identity.Action).Append('\n');
        }
        return TsvTable.Parse(cathedralSemantics.SourcePath, output.ToString());
    }

    internal static bool MatchesElixir(
        AuthoritativeCathedralNativeItemDefinition native,
        AuthoritativeItemDefinition stable,
        int stableSpellId)
    {
        ArgumentNullException.ThrowIfNull(native);
        ArgumentNullException.ThrowIfNull(stable);
        var identity = Elixirs.SingleOrDefault(value => value.NativeIndex == native.NativeItemIndex);
        if (identity is null || native.MiscId != identity.NativeMiscId || native.SpellId != 0
            || stable.NativeItemIndex != identity.NativeIndex || stable.ItemId != identity.StableItemId
            || !string.Equals(stable.StableSymbol, identity.StableSymbol, StringComparison.Ordinal)
            || stable.MiscId != identity.StableMiscId || stable.ItemActionKind != identity.Action)
            return false;

        // The source MiscId is one lower than the stable consumable action ID.
        // Normalize only this candidate during the established full native-field
        // comparison; the stable catalog and C1 matching behavior stay intact.
        return AuthoritativeCathedralNativePopulationBuilder.MatchesNativeItemDefinition(
            native, stable with { MiscId = native.MiscId }, stableSpellId,
            includeNativeDepthItems: true);
    }
}

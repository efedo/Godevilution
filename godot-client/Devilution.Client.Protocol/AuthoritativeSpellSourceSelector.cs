using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/**
 * Presentation-only view of the cast sources currently owned by the local
 * player. The server remains authoritative for admission, source ownership,
 * resource consumption, target validation, and every spell effect. Targeting
 * kind is an additive server-projected hint used only to choose an entity ID.
 */
public sealed record AuthoritativeSpellSource(
    string Key,
    string DisplayName,
    uint SpellId,
    uint Level,
    CastSourceKind Kind,
    PlayerItemReference? SourceItem,
    SpellTargetingKind TargetingKind = SpellTargetingKind.Unspecified);

public static class AuthoritativeSpellSourceSelector
{
    /** Returns stable, native-like source precedence for the local snapshot. */
    public static IReadOnlyList<AuthoritativeSpellSource> GetSources(Snapshot? snapshot)
    {
        var player = snapshot?.Players.FirstOrDefault();
        if (player is null)
            return [];

        var sources = new List<AuthoritativeSpellSource>();
        foreach (var spell in player.MemorizedSpells
                     .Where(spell => spell.SpellId != 0 && spell.Level != 0)
                     .OrderBy(spell => spell.SpellId)
                     .ThenBy(spell => spell.Level)) {
            sources.Add(new AuthoritativeSpellSource(
                $"memorized:{spell.SpellId}:{spell.Level}",
                string.IsNullOrWhiteSpace(spell.DisplayName)
                    ? $"Spell {spell.SpellId} (level {spell.Level})"
                    : $"{spell.DisplayName} (level {spell.Level})",
                spell.SpellId,
                spell.Level,
                CastSourceKind.MemorizedSpell,
                null,
                spell.TargetingKind));
        }

        foreach (var staff in player.Equipment
                     .Where(item => item.Slot == 4
                         && item.State is {
                             SpellId: > 0,
                             Charges: > 0,
                             SpellSourceKind: ItemSpellSourceKind.ChargedItem,
                         })
                     .OrderBy(item => item.ItemSeed)
                     .ThenBy(item => item.Slot)) {
            AddItemSource(
                sources,
                $"equipment:{staff.Slot}:{staff.ItemSeed}",
                FormatItemSpellName(staff.State!, $"staff, {staff.State.Charges} charges"),
                (uint)staff.State.SpellId,
                CastSourceKind.ChargedItem,
                new PlayerItemReference {
                    Location = PlayerItemLocation.Equipment,
                    Slot = staff.Slot,
                    StableItemId = staff.State.StableItemId,
                },
                staff.State.SpellTargetingKind);
        }

        for (var index = 0; index < player.Inventory.Count; index++) {
            var item = player.Inventory[index];
            if (!IsSpellConsumable(item.State))
                continue;
            AddItemSource(
                sources,
                $"inventory:{index}",
                FormatItemSpellName(item.State!, "inventory"),
                (uint)item.State.SpellId,
                CastSourceKind.ConsumableItem,
                new PlayerItemReference {
                    Location = PlayerItemLocation.Inventory,
                    Slot = (uint)index,
                    StableItemId = item.State.StableItemId,
                },
                item.State.SpellTargetingKind);
        }

        foreach (var item in player.Belt
                     .Where(item => IsSpellConsumable(item.State))
                     .OrderBy(item => item.Slot)) {
            AddItemSource(
                sources,
                $"belt:{item.Slot}",
                FormatItemSpellName(item.State!, "belt"),
                (uint)item.State.SpellId,
                CastSourceKind.ConsumableItem,
                new PlayerItemReference {
                    Location = PlayerItemLocation.Belt,
                    Slot = item.Slot,
                    StableItemId = item.State.StableItemId,
                },
                item.State.SpellTargetingKind);
        }

        if (player.StashOpen) {
            foreach (var item in player.Stash
                         .Where(item => item.Item is not null
                             && IsSpellConsumable(item.Item.State))
                         .OrderBy(item => item.Page)
                         .ThenBy(item => item.Cell)) {
                var state = item.Item!.State!;
                AddItemSource(
                    sources,
                    $"stash:{item.Page}:{item.Cell}",
                    FormatItemSpellName(state, "stash"),
                    (uint)state.SpellId,
                    CastSourceKind.ConsumableItem,
                    new PlayerItemReference {
                        Location = PlayerItemLocation.Stash,
                        Page = item.Page,
                        Slot = item.Cell,
                        StableItemId = state.StableItemId,
                    },
                    state.SpellTargetingKind);
            }
        }

        if (player.AbilitySpellId != 0) {
            var abilityName = player.AbilityKind == PlayerAbilityKind.Unspecified
                ? $"Ability {player.AbilitySpellId}"
                : player.AbilityKind.ToString();
            sources.Add(new AuthoritativeSpellSource(
                $"ability:{player.AbilitySpellId}",
                $"Ability {abilityName}",
                player.AbilitySpellId,
                1,
                CastSourceKind.Ability,
                null,
                player.AbilityTargetingKind));
        }

        return sources;
    }

    /** Keeps the selected source stable across snapshots, falling back safely. */
    public static AuthoritativeSpellSource? Select(
        IReadOnlyList<AuthoritativeSpellSource> sources,
        string? selectedKey)
    {
        if (sources.Count == 0)
            return null;
        return sources.FirstOrDefault(source => source.Key == selectedKey) ?? sources[0];
    }

    /** Cycles with wraparound while preserving the current source when possible. */
    public static AuthoritativeSpellSource? Cycle(
        IReadOnlyList<AuthoritativeSpellSource> sources,
        string? selectedKey,
        int direction)
    {
        if (sources.Count == 0)
            return null;
        if (direction == 0)
            return Select(sources, selectedKey);

        var index = Array.FindIndex(
            sources.ToArray(),
            source => source.Key == selectedKey);
        if (index < 0)
            index = direction > 0 ? -1 : 0;
        var next = (index + Math.Sign(direction) + sources.Count) % sources.Count;
        return sources[next];
    }

    private static void AddItemSource(
        ICollection<AuthoritativeSpellSource> sources,
        string key,
        string displayName,
        uint spellId,
        CastSourceKind kind,
        PlayerItemReference sourceItem,
        SpellTargetingKind targetingKind)
    {
        sources.Add(new AuthoritativeSpellSource(
            key,
            displayName,
            spellId,
            1,
            kind,
            sourceItem,
            targetingKind));
    }

    private static bool IsSpellConsumable(ItemStateSnapshot? state)
    {
        return state is { SpellId: > 0, SpellSourceKind: ItemSpellSourceKind.ConsumableItem };
    }

    private static string FormatItemSpellName(ItemStateSnapshot state, string source)
    {
        var name = string.IsNullOrWhiteSpace(state.SpellDisplayName)
            ? $"Spell {state.SpellId}"
            : state.SpellDisplayName;
        return $"{name} ({source})";
    }
}

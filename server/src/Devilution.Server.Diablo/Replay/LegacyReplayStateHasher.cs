using Devilution.Domain.Hashing;
using Devilution.Server.Stores;

namespace Devilution.Server.Replay;

/** Mirrors the field order of C++ AppendReplayPlayerState and StoreState. */
public static class LegacyReplayStateHasher
{
    public static string Compute(ReplayFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        var hasher = new CanonicalStateHasher();
        AppendPlayerState(hasher, fixture.InitialState);
        AppendStoreState(hasher, fixture.LegacyStoreState);
        return hasher.HexDigest();
    }

    public static string Compute(ReplayInitialState player, ReplayLegacyStoreState store)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(store);
        var hasher = new CanonicalStateHasher();
        AppendPlayerState(hasher, player);
        AppendStoreState(hasher, store);
        return hasher.HexDigest();
    }

    private static void AppendPlayerState(CanonicalStateHasher hasher, ReplayInitialState player)
    {
        hasher.AppendUInt8(0);
        hasher.AppendString(player.Player);
        hasher.AppendInt32(player.CharacterClass);
        hasher.AppendUInt8(player.CharacterLevel);
        hasher.AppendUInt32(player.Experience);
        hasher.AppendInt32(unchecked((int)player.Gold));

        AppendAttribute(hasher, 0, 0);
        AppendAttribute(hasher, 0, 0);
        AppendAttribute(hasher, 0, 0);
        AppendAttribute(hasher, 0, 0);
        AppendVitalResource(hasher, 0, player.Life, player.Life, 0);
        AppendVitalResource(hasher, 0, player.Mana, player.Mana, 0);
        hasher.AppendUInt32(0);
        hasher.AppendUInt8(0);
        hasher.AppendUInt8(0);
        hasher.AppendInt32(0);

        AppendItemRange(hasher, 7, LegacyReplayItemState.Empty);
        AppendItemRange(hasher, 40, LegacyReplayItemState.Empty);
        AppendItemRange(hasher, 8, LegacyReplayItemState.Empty);
        AppendItem(hasher, LegacyReplayItemState.Empty);
        for (var index = 0; index < 40; index++)
            hasher.AppendInt32(0);
    }

    private static void AppendStoreState(CanonicalStateHasher hasher, ReplayLegacyStoreState store)
    {
        hasher.AppendInt32(store.ActiveStore);
        hasher.AppendInt32(store.PremiumItemCount);
        hasher.AppendInt32(store.PremiumItemLevel);
        AppendItemRange(hasher, 0, LegacyReplayItemState.Empty);
        hasher.AppendUInt64((ulong)store.PremiumItemSeeds.Count);
        foreach (var seed in store.PremiumItemSeeds)
            AppendItem(hasher, LegacyReplayItemState.Empty with { Seed = seed });
        AppendItemRange(hasher, 0, LegacyReplayItemState.Empty);
        AppendItemRange(hasher, 0, LegacyReplayItemState.Empty);
        AppendItem(hasher, LegacyReplayItemState.Empty);
    }

    private static void AppendAttribute(CanonicalStateHasher hasher, int @base, int current)
    {
        hasher.AppendInt32(@base);
        hasher.AppendInt32(current);
    }

    private static void AppendVitalResource(CanonicalStateHasher hasher, int @base, int current, int maximum, int maximumBase)
    {
        hasher.AppendInt32(@base);
        hasher.AppendInt32(current);
        hasher.AppendInt32(maximum);
        hasher.AppendInt32(maximumBase);
    }

    private static void AppendItemRange(CanonicalStateHasher hasher, int count, LegacyReplayItemState item)
    {
        hasher.AppendUInt64((ulong)count);
        for (var index = 0; index < count; index++)
            AppendItem(hasher, item);
    }

    private static void AppendItem(CanonicalStateHasher hasher, LegacyReplayItemState item)
    {
        var state = item.State;
        hasher.AppendUInt32(item.Seed);
        hasher.AppendUInt32(state.CreateInfo);
        hasher.AppendInt32(state.ItemType);
        hasher.AppendInt32(state.PositionX);
        hasher.AppendInt32(state.PositionY);
        hasher.AppendBool(state.Deleted);
        hasher.AppendBool(state.Identified);
        hasher.AppendInt32(state.Magical);
        hasher.AppendInt32(state.EquipLocation);
        hasher.AppendInt32(state.ItemClass);
        hasher.AppendInt32(state.Value);
        hasher.AppendInt32(state.IdentifiedValue);
        hasher.AppendInt32(state.MinDamage);
        hasher.AppendInt32(state.MaxDamage);
        hasher.AppendInt32(state.ArmorClass);
        hasher.AppendInt32(unchecked((int)state.Flags));
        hasher.AppendInt32(state.MiscId);
        hasher.AppendInt32(state.SpellId);
        hasher.AppendInt32(state.ItemIndex);
        hasher.AppendInt32(state.Charges);
        hasher.AppendInt32(state.MaxCharges);
        hasher.AppendInt32(state.Durability);
        hasher.AppendInt32(state.MaxDurability);
        hasher.AppendInt32(state.PlusDamage);
        hasher.AppendInt32(state.PlusToHit);
        hasher.AppendInt32(state.PlusArmorClass);
        hasher.AppendInt32(state.PlusStrength);
        hasher.AppendInt32(state.PlusMagic);
        hasher.AppendInt32(state.PlusDexterity);
        hasher.AppendInt32(state.PlusVitality);
        hasher.AppendInt32(state.PlusFireResistance);
        hasher.AppendInt32(state.PlusLightningResistance);
        hasher.AppendInt32(state.PlusMagicResistance);
        hasher.AppendInt32(state.PlusMana);
        hasher.AppendInt32(state.PlusHitPoints);
        hasher.AppendInt32(state.PlusDamageModifier);
        hasher.AppendInt32(state.PlusGetHit);
        hasher.AppendInt32(state.PlusLight);
        hasher.AppendInt32(state.SpellLevelAdd);
        hasher.AppendInt32(state.UniqueId);
        hasher.AppendInt32(state.FireMinDamage);
        hasher.AppendInt32(state.FireMaxDamage);
        hasher.AppendInt32(state.LightningMinDamage);
        hasher.AppendInt32(state.LightningMaxDamage);
        hasher.AppendInt32(state.PlusEnemyArmorClass);
        hasher.AppendInt32(state.PrefixPower);
        hasher.AppendInt32(state.SuffixPower);
        hasher.AppendInt32(state.ValueAdd1);
        hasher.AppendInt32(state.ValueMultiply1);
        hasher.AppendInt32(state.ValueAdd2);
        hasher.AppendInt32(state.ValueMultiply2);
        hasher.AppendInt32(state.MinimumStrength);
        hasher.AppendInt32(state.MinimumMagic);
        hasher.AppendInt32(state.MinimumDexterity);
        hasher.AppendBool(state.StatFlag);
        hasher.AppendInt32(state.HellfireDamageArmorFlags);
        hasher.AppendUInt32(state.Buff);
        if (state.ManaToLifePercent != 0 || state.LifeToManaPercent != 0) {
            hasher.AppendString("godevilution-item-resource-conversion-v1");
            hasher.AppendInt32(state.ManaToLifePercent);
            hasher.AppendInt32(state.LifeToManaPercent);
        }
    }

    private sealed record LegacyReplayItemState(uint Seed, AuthoritativeItemState State)
    {
        public static LegacyReplayItemState Empty { get; } = new(0, AuthoritativeItemState.Empty);
    }
}

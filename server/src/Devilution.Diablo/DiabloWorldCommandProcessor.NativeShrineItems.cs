using Devilution.Engine;

namespace Devilution.Diablo;

public sealed partial class DiabloWorldCommandProcessor
{
    /** Native objects.cpp OperateShrineHidden. Empty equipment slots still participate in rejection sampling. */
    private static void OperateShrineHidden(DeterministicLegacyRandom rng, AuthoritativePlayerState player)
    {
        if (player.Equipment.Any(item => item.Slot >= 7))
            throw new InvalidDataException("Hidden shrine equipment is outside native InvBody.");
        for (var index = 0; index < player.Equipment.Count; index++) {
            var item = player.Equipment[index];
            if (item.State.ItemType != -1 && item.State.MaxDurability is not (0 or 255))
                player.Equipment[index] = item with { State = item.State with {
                    Durability = Math.Min(item.State.Durability + 10, item.State.MaxDurability + 10),
                    MaxDurability = item.State.MaxDurability + 10,
                } };
        }
        while (player.Equipment.Any(item => item.State.ItemType != -1 && item.State.MaxDurability is not (0 or 255))) {
            var slot = (uint)rng.GenerateRnd(7);
            var index = player.Equipment.FindIndex(item => item.Slot == slot);
            if (index < 0) continue;
            var item = player.Equipment[index];
            if (item.State.ItemType == -1 || item.State.MaxDurability is 0 or 255) continue;
            player.Equipment[index] = item with { State = item.State with {
                Durability = Math.Max(1, item.State.Durability - 20),
                MaxDurability = Math.Max(1, item.State.MaxDurability - 20),
            } };
            break;
        }
    }

    /** Native objects.cpp OperateShrineGloomy. */
    private static void OperateShrineGloomy(AuthoritativePlayerState player)
    {
        TransformPlayerItems(player, state => state.ItemType switch {
            1 or 2 or 3 or 4 or 10 => state with { MaxDamage = Math.Max(state.MinDamage, state.MaxDamage - 1) },
            5 or 6 or 7 or 8 or 9 => state with { ArmorClass = state.ArmorClass + 2 },
            _ => state,
        });
        player.RecalculateEquipmentResources();
    }

    /** Native objects.cpp OperateShrineWeird. */
    private static void OperateShrineWeird(AuthoritativePlayerState player)
    {
        for (var index = 0; index < player.Equipment.Count; index++) {
            var item = player.Equipment[index];
            if (item.Slot is 4 or 5 && item.State.ItemType is not (-1 or 5))
                player.Equipment[index] = item with { State = item.State with { MaxDamage = item.State.MaxDamage + 1 } };
        }
        TransformInventoryItems(player, state => state.ItemType is 1 or 2 or 3 or 4 or 10
            ? state with { MaxDamage = state.MaxDamage + 1 } : state);
        player.RecalculateEquipmentResources();
    }

    /** Native objects.cpp OperateShrineStone. */
    private static void OperateShrineStone(AuthoritativePlayerState player)
    {
        TransformPlayerItems(player, state => state.ItemType == 10 ? state with { Charges = state.MaxCharges } : state);
        player.RecalculateEquipmentResources();
    }

    /** Native objects.cpp OperateShrineReligious. */
    private static void OperateShrineReligious(AuthoritativePlayerState player)
        => TransformPlayerItems(player, state => state with { Durability = state.MaxDurability });

    /** Native objects.cpp OperateShrineGlimmering. */
    private static void OperateShrineGlimmering(AuthoritativePlayerState player)
    {
        TransformPlayerItems(player, state => state.Magical != 0 && !state.Identified ? state with { Identified = true } : state);
        player.RecalculateEquipmentResources();
    }

    /** Native objects.cpp OperateShrineEldritch. InitializeItem consumes no RNG and preserves the old seed. */
    private void OperateShrineEldritch(AuthoritativePlayerState player)
    {
        AuthoritativeItemState Convert(AuthoritativeItemState state) => state.ItemType != 0 ? state : state.MiscId switch {
            3 or 6 => InitializeShrineItem(18), // IMISC_HEAL/MANA -> IMISC_REJUV
            2 or 7 => InitializeShrineItem(19), // IMISC_FULLHEAL/FULLMANA -> IMISC_FULLREJUV
            _ => state,
        };
        TransformInventoryItems(player, Convert);
        for (var index = 0; index < player.Belt.Count; index++)
            player.Belt[index] = player.Belt[index] with { State = Convert(player.Belt[index].State) };
    }

    /** Native objects.cpp OperateShrineDivine. World items use the global stream, not the shrine generator. */
    private void OperateShrineDivine(AuthoritativePlayerState player, AuthoritativeWorldObject shrine,
        IReadOnlyList<AuthoritativePlayerState> participants)
    {
        var lowLevel = world.NativePopulationForLevel(shrine.LevelId)!.Depth < 4;
        world.CreateNativeShrineItem(shrine, lowLevel ? 7 : 19, participants,
            () => AllocateWorldItemEntityId(participants), random);
        world.CreateNativeShrineItem(shrine, lowLevel ? 2 : 19, participants,
            () => AllocateWorldItemEntityId(participants), random);
        player.Mana = player.ManaMaximum;
        player.Life = player.LifeMaximum;
    }

    /** Native objects.cpp OperateShrineSpiritual. InvGrid is native row-major, gold seed uses global AdvanceRndSeed. */
    private void OperateShrineSpiritual(DeterministicLegacyRandom rng, AuthoritativePlayerState player)
    {
        var levelType = NativeShrineLevelType(player.LevelId);
        for (var cell = 0; cell < player.InventoryGrid.Count; cell++) {
            if (player.InventoryGrid[cell] != -1) continue; // Native zero is the managed empty sentinel -1.
            var value = 5 * levelType + rng.GenerateRnd(10 * levelType);
            var seed = random.AdvanceSeed();
            // Zero is reserved by shared inventory identities; uint.MaxValue is outside native abs32 seed results.
            player.Inventory.Add(DiabloGoldInventory.CreateStack(seed == 0 ? uint.MaxValue : seed, (uint)value, combatRules, items));
            player.InventoryGrid[cell] = player.Inventory.Count - 1;
            player.Gold = checked(player.Gold + (uint)value);
        }
    }

    /** Native items.cpp InitializeItem for Eldritch potion conversions. */
    private AuthoritativeItemState InitializeShrineItem(int miscId)
    {
        var definition = items!.Definitions.FirstOrDefault(item => item.ItemType == 0 && item.MiscId == miscId)
            ?? throw new InvalidDataException($"Native shrine item {miscId} is absent.");
        return new AuthoritativeItemState(0, definition.ItemType, 0, 0, false, false, 0,
            definition.EquipLocation, definition.ItemClass, definition.Value, definition.Value,
            definition.MinDamage, definition.MaxDamage, Math.Max(0, definition.MinArmorClass), 0,
            definition.MiscId, definition.SpellId, definition.NativeItemIndex, 0, 0,
            definition.Durability, definition.Durability) {
            StableItemId = definition.ItemId,
            SpellSourceKind = definition.SpellSourceKind,
            ItemActionKind = definition.ItemActionKind,
            MinimumStrength = definition.MinimumStrength,
            MinimumMagic = definition.MinimumMagic,
            MinimumDexterity = definition.MinimumDexterity,
            StatFlag = true,
            InventoryWidth = definition.InventoryWidth,
            InventoryHeight = definition.InventoryHeight,
            BaseName = definition.Name,
            IdentifiedName = definition.Name,
            NativeCursorGraphic = definition.NativeCursorGraphic,
        };
    }

    private void RefreshNativeItemStatFlags(AuthoritativePlayerState player)
    {
        AuthoritativeItemState Refresh(AuthoritativeItemState state) {
            var minimumMagic = state.MinimumMagic;
            if (state.MiscId == 24 && state.SpellId > 0
                && spellBooks?.TryGet((uint)state.SpellId, out var book) == true) {
                minimumMagic = book.MinimumMagic;
                var level = player.MemorizedSpells.FirstOrDefault(entry => entry.SpellId == (uint)state.SpellId)?.Level ?? 0;
                for (var index = 0; index < level; index++) {
                    minimumMagic += minimumMagic / 5;
                    if (minimumMagic + minimumMagic / 5 > 255) { minimumMagic = 255; break; }
                }
            }
            return state with {
                MinimumMagic = minimumMagic,
                StatFlag = player.Attributes.Strength.Current >= state.MinimumStrength
                    && player.Attributes.Magic.Current >= minimumMagic
                    && player.Attributes.Dexterity.Current >= state.MinimumDexterity,
            };
        }
        TransformInventoryItems(player, Refresh);
        for (var index = 0; index < player.Belt.Count; index++)
            player.Belt[index] = player.Belt[index] with { State = Refresh(player.Belt[index].State) };
        if (!player.StashOpen) return;
        for (var index = 0; index < player.Stash.Count; index++) {
            var item = player.Stash[index];
            player.Stash[index] = item with { State = Refresh(item.State) };
        }
    }

    // Preserve native container order: InvBody, InvList, SpdList. Cursor/stash are excluded.
    private static void TransformPlayerItems(AuthoritativePlayerState player, Func<AuthoritativeItemState, AuthoritativeItemState> transform)
    {
        foreach (var index in Enumerable.Range(0, player.Equipment.Count).OrderBy(index => player.Equipment[index].Slot))
            player.Equipment[index] = player.Equipment[index] with { State = transform(player.Equipment[index].State) };
        TransformInventoryItems(player, transform);
        foreach (var index in Enumerable.Range(0, player.Belt.Count).OrderBy(index => player.Belt[index].Slot))
            player.Belt[index] = player.Belt[index] with { State = transform(player.Belt[index].State) };
    }

    private static void TransformInventoryItems(AuthoritativePlayerState player, Func<AuthoritativeItemState, AuthoritativeItemState> transform)
    {
        for (var index = 0; index < player.Inventory.Count; index++)
            player.Inventory[index] = player.Inventory[index] with { State = transform(player.Inventory[index].State) };
    }
}

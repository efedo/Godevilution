namespace Devilution.Diablo;

public sealed partial class AuthoritativePlayerState
{
    /** Native items.cpp CalcSelfItems. Admitted carried items already passed the native item provenance boundary. */
    private void CalcSelfItems()
    {
        var strength = 0; var magic = 0; var dexterity = 0;
        foreach (var index in Enumerable.Range(0, Equipment.Count).OrderBy(index => Equipment[index].Slot)) {
            var item = Equipment[index];
            Equipment[index] = item with { State = item.State with { StatFlag = true } };
            if (!item.State.Identified) continue;
            strength += item.State.PlusStrength; magic += item.State.PlusMagic; dexterity += item.State.PlusDexterity;
        }
        bool changed;
        do {
            var currentStrength = Math.Max(0, strength + Attributes.Strength.Base);
            var currentMagic = Math.Max(0, magic + Attributes.Magic.Base);
            var currentDexterity = Math.Max(0, dexterity + Attributes.Dexterity.Base);
            changed = false;
            foreach (var index in Enumerable.Range(0, Equipment.Count).OrderBy(index => Equipment[index].Slot)) {
                var item = Equipment[index];
                if (!item.State.StatFlag) continue;
                if (currentStrength >= item.State.MinimumStrength && currentMagic >= item.State.MinimumMagic
                    && currentDexterity >= item.State.MinimumDexterity) continue;
                changed = true;
                Equipment[index] = item with { State = item.State with { StatFlag = false } };
                if (!item.State.Identified) continue;
                strength -= item.State.PlusStrength; magic -= item.State.PlusMagic; dexterity -= item.State.PlusDexterity;
            }
        } while (changed);
    }

    /** Native items.cpp CalcPlrInv updates InvList, SpdList, the open stash and HoldItem after equipment stats. */
    private void RefreshNativeCarryStatFlags()
    {
        AuthoritativeItemState Refresh(AuthoritativeItemState state) => state with {
            StatFlag = Attributes.Strength.Current >= state.MinimumStrength
                && Attributes.Magic.Current >= state.MinimumMagic && Attributes.Dexterity.Current >= state.MinimumDexterity,
        };
        for (var index = 0; index < Inventory.Count; index++) Inventory[index] = Inventory[index] with { State = Refresh(Inventory[index].State) };
        for (var index = 0; index < Belt.Count; index++) Belt[index] = Belt[index] with { State = Refresh(Belt[index].State) };
        if (HeldItem is not null) HeldItem = HeldItem with { State = Refresh(HeldItem.State) };
        if (StashOpen) for (var index = 0; index < Stash.Count; index++) Stash[index] = Stash[index] with { State = Refresh(Stash[index].State) };
    }
}

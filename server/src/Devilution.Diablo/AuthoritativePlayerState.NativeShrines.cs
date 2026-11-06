namespace Devilution.Diablo;

public sealed partial class AuthoritativePlayerState
{
    /** Native players.cpp modifyStrength/modifyMagic/modifyDexterity/modifyVitality. */
    internal void ModifyNativeAttribute(DiabloPlayerAttribute attribute, int amount)
    {
        var current = attribute switch {
            DiabloPlayerAttribute.Strength => Attributes.Strength,
            DiabloPlayerAttribute.Magic => Attributes.Magic,
            DiabloPlayerAttribute.Dexterity => Attributes.Dexterity,
            DiabloPlayerAttribute.Vitality => Attributes.Vitality,
            _ => throw new ArgumentOutOfRangeException(nameof(attribute)),
        };
        var maximum = attribute switch {
            DiabloPlayerAttribute.Strength => PlayerClass?.MaximumStrength,
            DiabloPlayerAttribute.Magic => PlayerClass?.MaximumMagic,
            DiabloPlayerAttribute.Dexterity => PlayerClass?.MaximumDexterity,
            DiabloPlayerAttribute.Vitality => PlayerClass?.MaximumVitality,
            _ => null,
        } ?? MaximumAttribute;
        amount = Math.Clamp(amount, -current.Base, maximum - current.Base);
        var updated = new PlayerAttributeState(current.Base + amount, current.Current + amount);
        Attributes = attribute switch {
            DiabloPlayerAttribute.Strength => Attributes with { Strength = updated },
            DiabloPlayerAttribute.Magic => Attributes with { Magic = updated },
            DiabloPlayerAttribute.Dexterity => Attributes with { Dexterity = updated },
            DiabloPlayerAttribute.Vitality => Attributes with { Vitality = updated },
            _ => Attributes,
        };
        if (attribute == DiabloPlayerAttribute.Magic) {
            var delta = checked(amount * (PlayerClass?.ManaPerMagicQ6 ?? 0));
            BaseManaMaximum += delta;
            ManaMaximum += delta;
            if (!AuthoritativeEquipmentManaRules.HasNoManaItem(Equipment)) Mana += delta;
        } else if (attribute == DiabloPlayerAttribute.Vitality) {
            var delta = checked(amount * (PlayerClass?.LifePerVitalityQ6 ?? 0));
            BaseLifeMaximum += delta;
            LifeMaximum += delta;
            Life += delta;
        }
        RecalculateEquipmentResources();
    }

    internal void RestoreNativeMana(int current, int maximum)
    {
        Mana = current;
        ManaMaximum = maximum;
    }

    /** Native objects.cpp OperateShrineCostOfWisdom: current mana is allowed to become negative. */
    internal void ApplyNativeWisdomPenalty()
    {
        BaseManaMaximum = Math.Max(0, BaseManaMaximum);
        var penalty = BaseManaMaximum / 10;
        BaseManaMaximum -= penalty;
        ManaMaximum -= penalty;
        Mana -= penalty;
    }
}

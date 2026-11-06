using Devilution.Client.Protocol;
using Devilution.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client.Acceptance;

/** Observable theme effects; random shrines remain covered by their native comparison and shrine journey. */
internal static class NativeThemeObjectJourneyEvidence
{
    internal static bool IsLoot(uint type) => NativeDepthResumeEvidence.IsThemeItemObject(type);
    internal static bool IsReusable(uint type) => NativeDepthResumeEvidence.IsReusableThemeObject(type);

    internal static bool CanDemonstrate(ObjectSnapshot value, PlayerSnapshot player)
        => AuthoritativeNativeThemeObjectState.IsValidInteraction(value)
            && (IsLoot(value.NativeObjectId) || value.NativeObjectId switch {
                66 => player.Life < player.LifeMaximum,
                76 => Mana(player) < ManaMaximum(player),
                81 => !player.StatusEffects.Any(effect => effect.Behavior == StatusEffectBehavior.Infravision),
                82 => Attributes(player).Zip(new[] {250, 50, 60, 100}, (amount, maximum) => amount > 0 && amount < maximum).All(value => value),
                _ => false,
            });

    internal static List<(int X, int Y)>? FindApproach(Snapshot snapshot, LevelGeometrySnapshot geometry, ObjectSnapshot value)
        => NativeObjectApproachPath.Find(snapshot, geometry, value, allowClosedDoors: true, ignoreActors: true);

    internal static uint UsedFrame(ObjectSnapshot value) => value.NativeObjectId switch {
        61 or 64 => checked(value.AnimationFrame + 2),
        62 or 63 => checked(value.AnimationFrame - 2),
        77 or 89 or 90 or 92 => checked(value.AnimationFrame + 1),
        _ => value.AnimationFrame,
    };

    internal static bool EffectApplied(uint type, PlayerSnapshot before, PlayerSnapshot after)
        => type switch {
            66 => after.Life == Math.Min((long)before.Life + 64, before.LifeMaximum),
            76 => Mana(after) == Math.Min(Mana(before) + 64, ManaMaximum(before)),
            81 => after.StatusEffects.Any(effect => effect.Behavior == StatusEffectBehavior.Infravision && effect.RemainingTicks > 0),
            82 => TearsTransferred(before, after),
            _ => false,
        };

    internal static void VerifyDrop(WorldItemSnapshot item, uint level)
    {
        if (item.LevelId != level || item.State is null || item.State.StableItemId <= 0
            || (item.State.CreateInfo & 0x3f) != 2 * level)
            throw new InvalidOperationException("The native theme object drop has the wrong identity, floor or item level.");
    }

    internal static void VerifyRetained(ObjectSnapshot? value, uint level, uint type, uint frame)
    {
        var reusable = IsReusable(type);
        if (!NativeDepthResumeEvidence.IsDemonstrableThemeObject(type) || value is null || value.LevelId != level || value.NativeObjectId != type || value.Destroyed
            || value.AnimationFrame != frame || value.Activated == reusable
            || value.NativeThemeObjectInteractable != reusable)
            throw new InvalidOperationException("The native theme object's identity, frame or use state did not survive retention or fresh-host resume.");
    }

    private static long Mana(PlayerSnapshot value) => value.HasNativeMana ? value.NativeMana : value.Mana;
    private static long ManaMaximum(PlayerSnapshot value) => value.HasNativeManaMaximum ? value.NativeManaMaximum : value.ManaMaximum;

    private static bool TearsTransferred(PlayerSnapshot before, PlayerSnapshot after)
    {
        var delta = Attributes(after).Zip(Attributes(before), (current, old) => current - old).Order().ToArray();
        return delta.SequenceEqual(new[] {-1, 0, 0, 1});
    }

    private static int[] Attributes(PlayerSnapshot value)
        => [(int)value.Attributes.Strength.Base, (int)value.Attributes.Magic.Base,
            (int)value.Attributes.Dexterity.Base, (int)value.Attributes.Vitality.Base];
}

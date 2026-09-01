using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Presentation family selected from a server-owned status-effect identity. */
public enum StatusPresentationKind
{
    None,
    ManaShield,
    Reflect,
    Infravision,
    Search,
    Etherealize,
}

/** One server-owned player status projected for presentation. */
public sealed record StatusPresentationBadge(
    StatusPresentationKind Kind,
    string Label,
    uint RemainingTicks = 0,
    uint Charges = 0);

/** Maps authoritative status identities to client-only visual families. */
public static class StatusPresentationStyle
{
    public static StatusPresentationKind Resolve(StatusEffectBehavior behavior)
    {
        return behavior switch {
            StatusEffectBehavior.Infravision => StatusPresentationKind.Infravision,
            StatusEffectBehavior.Search => StatusPresentationKind.Search,
            StatusEffectBehavior.Etherealize => StatusPresentationKind.Etherealize,
            _ => StatusPresentationKind.None,
        };
    }

    /** Resolves all displayable player statuses in a stable presentation order. */
    public static IReadOnlyList<StatusPresentationBadge> Resolve(PlayerSnapshot player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var badges = new List<StatusPresentationBadge>();
        if (player.ManaShieldActive)
            badges.Add(new(StatusPresentationKind.ManaShield, "Mana Shield"));
        if (player.ReflectCharges > 0)
            badges.Add(new(StatusPresentationKind.Reflect, "Reflect", Charges: player.ReflectCharges));

        var timed = player.StatusEffects
            .Select(effect => new {
                Kind = Resolve(effect.Behavior),
                effect.RemainingTicks,
            })
            .Where(effect => effect.Kind != StatusPresentationKind.None)
            .GroupBy(effect => effect.Kind)
            .Select(group => new StatusPresentationBadge(
                group.Key,
                FormatLabel(group.Key),
                group.Max(effect => effect.RemainingTicks)))
            .OrderBy(badge => badge.Kind);
        badges.AddRange(timed);
        return badges;
    }

    /** Formats a stable compact status summary for the HUD. */
    public static string Format(IReadOnlyList<StatusPresentationBadge> badges)
    {
        ArgumentNullException.ThrowIfNull(badges);
        return string.Join(
            "   ",
            badges.Select(badge => badge.Kind == StatusPresentationKind.Reflect
                ? $"{badge.Label} x{badge.Charges}"
                : badge.Label));
    }

    private static string FormatLabel(StatusPresentationKind kind)
    {
        return kind switch {
            StatusPresentationKind.Infravision => "Infravision",
            StatusPresentationKind.Search => "Search",
            StatusPresentationKind.Etherealize => "Etherealize",
            _ => kind.ToString(),
        };
    }
}

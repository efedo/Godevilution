using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Maps authoritative monster vitality into a presentation-only health bar. */
public readonly record struct MonsterHealthBarProjection(
    bool Visible,
    float Ratio)
{
    public static MonsterHealthBarProjection Resolve(MonsterSnapshot monster)
    {
        ArgumentNullException.ThrowIfNull(monster);
        if (!monster.Alive)
            return new MonsterHealthBarProjection(false, 0);

        var maximum = monster.MaxHitPointsQ6 > 0
            ? monster.MaxHitPointsQ6
            : monster.MaxHitPoints;
        if (maximum <= 0)
            return new MonsterHealthBarProjection(false, 0);

        var current = monster.MaxHitPointsQ6 > 0
            ? monster.HitPointsQ6
            : monster.HitPoints;
        var ratio = Math.Clamp((double)current / maximum, 0, 1);
        return new MonsterHealthBarProjection(true, (float)ratio);
    }
}

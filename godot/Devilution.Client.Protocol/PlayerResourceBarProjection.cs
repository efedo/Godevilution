using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Maps authoritative player life and mana into presentation-only bars. */
public readonly record struct PlayerResourceBarProjection(
    bool LifeVisible,
    float LifeRatio,
    bool ManaVisible,
    float ManaRatio)
{
    public static PlayerResourceBarProjection Resolve(PlayerSnapshot player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var alive = player.Life > 0;
        var life = ResolveBar(alive, player.Life, player.LifeMaximum);
        var mana = ResolveBar(alive, player.Mana, player.ManaMaximum);
        return new PlayerResourceBarProjection(
            life.Visible,
            life.Ratio,
            mana.Visible,
            mana.Ratio);
    }

    private static (bool Visible, float Ratio) ResolveBar(
        bool alive,
        int current,
        int maximum)
    {
        if (!alive || maximum <= 0)
            return (false, 0);

        var ratio = Math.Clamp((double)current / maximum, 0, 1);
        return (true, (float)ratio);
    }
}

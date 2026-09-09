using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** Selects server-declared general towner greetings for presentation input. */
public static class AuthoritativeTownerGreetingSelector
{
    public static TownerGreetingSnapshot? SelectAt(Snapshot? snapshot, int x, int y)
    {
        var player = snapshot?.Players.FirstOrDefault();
        if (player is null || player.HeldItem is not null)
            return null;

        var geometry = snapshot!.LevelGeometry.FirstOrDefault(level => level.LevelId == player.LevelId);
        var lightRadius = player.LightRadius is >= 2 and <= 15 ? player.LightRadius : 10;
        return snapshot.TownerGreetings
            .Where(greeting => greeting.TownerId is >= 0 and <= byte.MaxValue
                && greeting.LevelId == player.LevelId
                && greeting.PositionX == x
                && greeting.PositionY == y
                && AuthoritativeNativeVisionVisibility.IsVisible(
                    greeting.LevelId,
                    greeting.PositionX,
                    greeting.PositionY,
                    player.LevelId,
                    geometry,
                    player.PositionX,
                    player.PositionY,
                    lightRadius))
            .OrderBy(greeting => greeting.TownerId)
            .FirstOrDefault();
    }
}

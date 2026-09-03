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

        return snapshot!.TownerGreetings
            .Where(greeting => greeting.TownerId is >= 0 and <= byte.MaxValue
                && greeting.LevelId == player.LevelId
                && greeting.PositionX == x
                && greeting.PositionY == y)
            .OrderBy(greeting => greeting.TownerId)
            .FirstOrDefault();
    }
}

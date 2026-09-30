using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

/** A declared destination, not a client path or permission to perform gameplay. */
public sealed record AuthoritativeWorldDestination(
    string Key, string Label, uint LevelId, int X, int Y, uint Id, bool IsPortal)
{
    public Command CreateInteraction() => IsPortal
        ? new Command { UsePortalRequested = new UsePortalRequested { PortalId = Id } }
        : new Command { TalkToTownerRequested = new TalkToTownerRequested { TownerId = Id } };
}

/** Lists already-projected local-level destinations independently of viewport visibility. */
public static class AuthoritativeWorldDestinationSelector
{
    public static IReadOnlyList<AuthoritativeWorldDestination> GetDestinations(Snapshot? snapshot)
    {
        var player = snapshot?.Players.FirstOrDefault();
        if (player is not { Life: > 0 })
            return [];
        var destinations = new List<AuthoritativeWorldDestination>();
        if (player.HeldItem is null) {
            foreach (var greeting in snapshot!.TownerGreetings
                         .Where(greeting => greeting.LevelId == player.LevelId
                             && greeting.TownerId is >= 0 and <= byte.MaxValue)
                         .OrderBy(greeting => greeting.TownerId)) {
                var id = checked((uint)greeting.TownerId);
                destinations.Add(new AuthoritativeWorldDestination(
                    $"towner:{id}", $"Towner {id} — ({greeting.PositionX}, {greeting.PositionY})",
                    player.LevelId, greeting.PositionX, greeting.PositionY, id, false));
            }
        }
        foreach (var portal in snapshot!.Portals.OrderBy(portal => portal.PortalId)) {
            if (AuthoritativePortalPresentation.Resolve(portal, player.LevelId) is not { } endpoint)
                continue;
            var otherLevel = portal.SourceLevelId == player.LevelId
                ? portal.DestinationLevelId : portal.SourceLevelId;
            destinations.Add(new AuthoritativeWorldDestination(
                $"portal:{portal.PortalId}", $"Portal {portal.PortalId} → level {otherLevel}",
                player.LevelId, endpoint.X, endpoint.Y, portal.PortalId, true));
        }
        return destinations.AsReadOnly();
    }

    /** Re-checks identity, level and endpoint before every acknowledged approach step. */
    public static bool IsPresent(Snapshot? snapshot, int x, int y, Command interaction)
    {
        var key = interaction.IntentCase switch {
            Command.IntentOneofCase.TalkToTownerRequested => $"towner:{interaction.TalkToTownerRequested.TownerId}",
            Command.IntentOneofCase.UsePortalRequested => $"portal:{interaction.UsePortalRequested.PortalId}",
            _ => null,
        };
        return key is not null && GetDestinations(snapshot).Any(destination =>
            destination.Key == key && destination.X == x && destination.Y == y);
    }
}

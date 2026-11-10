using Devilution.Protocol;
using Devilution.Protocol.V1;

namespace Devilution.Client.Protocol;

public sealed partial class AuthoritativeClientModel
{
    private bool cachedLevelScopedSnapshots;

    private static void ValidateLevelProjection(Snapshot snapshot, bool scoped, HashSet<uint> entityIds)
    {
        if (!scoped) {
            if (snapshot.PlayerRoster.Count != 0)
                throw new InvalidDataException("An unnegotiated snapshot contains a level-scoped player roster.");
            return;
        }
        var local = snapshot.Players[0];
        if (snapshot.PlayerRoster.Count is < 1 or > NativeMaximumPlayerCount
            || snapshot.PlayerRoster[0].EntityId != local.EntityId
            || snapshot.PlayerRoster[0].LevelId != local.LevelId)
            throw new InvalidDataException("A level-scoped snapshot has no matching receiving player roster entry.");
        var roster = new Dictionary<uint, uint>();
        foreach (var entry in snapshot.PlayerRoster) {
            if (entry.EntityId == 0 || !AuthoritativeLevelIdRules.IsSupported(entry.LevelId)
                || !roster.TryAdd(entry.EntityId, entry.LevelId)
                || entry.LevelId != local.LevelId && entityIds.Contains(entry.EntityId))
                throw new InvalidDataException("A level-scoped snapshot contains an invalid player roster.");
        }
        foreach (var player in snapshot.Players)
            if (player.LevelId != local.LevelId || !roster.TryGetValue(player.EntityId, out var level) || level != player.LevelId)
                throw new InvalidDataException("A level-scoped snapshot contains an off-level or unlisted player.");
        foreach (var entry in snapshot.PlayerRoster)
            if (entry.LevelId == local.LevelId && !snapshot.Players.Any(player => player.EntityId == entry.EntityId))
                throw new InvalidDataException("A level-scoped snapshot omitted an on-level player.");
        if (snapshot.Monsters.Any(value => value.LevelId != local.LevelId)
            || snapshot.Objects.Any(value => value.LevelId != local.LevelId)
            || snapshot.WorldItems.Any(value => value.LevelId != local.LevelId)
            || snapshot.Projectiles.Any(value => value.LevelId != local.LevelId)
            || snapshot.Corpses.Any(value => value.LevelId != local.LevelId)
            || snapshot.QuestTownerInteractions.Any(value => value.LevelId != local.LevelId)
            || snapshot.TownerGreetings.Any(value => value.LevelId != local.LevelId)
            || snapshot.NativeTownResidents.Any(value => value.LevelId != local.LevelId)
            || snapshot.SetLevelObjectOrigins.Any(value => value.LevelId != local.LevelId)
            || snapshot.ProceduralQuestPieceOrigins.Any(value => value.LevelId != local.LevelId)
            || snapshot.Portals.Any(value => value.SourceLevelId != local.LevelId
                && (!value.Bidirectional || value.DestinationLevelId != local.LevelId)))
            throw new InvalidDataException("A level-scoped snapshot contains off-level entities or interactions.");
    }
}

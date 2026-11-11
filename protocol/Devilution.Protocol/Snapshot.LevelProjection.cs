using Devilution.Shared;
using Google.Protobuf.Collections;

namespace Devilution.Protocol.V1;

public sealed partial class Snapshot
{
    /** Cohort membership is global; Players contains only actors in the receiving view when scoped. */
    public int ParticipantCount => playerRoster_.Count == 0 ? players_.Count : playerRoster_.Count;

    /** Read-only connection view. Geometry/topology, quests and progression remain global metadata. */
    public Snapshot ForReceivingLevel()
    {
        if (players_.Count == 0) throw new InvalidDataException("A level-scoped snapshot requires its receiving player first.");
        return new(this, players_[0].LevelId);
    }

    private Snapshot(Snapshot other, uint level) : this(other, other.levelGeometry_, cloneBody: false)
    {
        playerRoster_ = new();
        if (other.playerRoster_.Count != 0) playerRoster_.AddRange(other.playerRoster_);
        else foreach (var player in players_)
            playerRoster_.Add(new PlayerRosterEntry { EntityId = player.EntityId, LevelId = player.LevelId });
        players_ = AtLevel(players_, level, static value => value.LevelId);
        monsters_ = AtLevel(monsters_, level, static value => value.LevelId);
        worldItems_ = AtLevel(worldItems_, level, static value => value.LevelId);
        objects_ = AtLevel(objects_, level, static value => value.LevelId);
        projectiles_ = AtLevel(projectiles_, level, static value => value.LevelId);
        corpses_ = AtLevel(corpses_, level, static value => value.LevelId);
        questTownerInteractions_ = AtLevel(questTownerInteractions_, level, static value => value.LevelId, legacyZeroWildcard: false);
        townerGreetings_ = AtLevel(townerGreetings_, level, static value => value.LevelId, legacyZeroWildcard: false);
        nativeTownResidents_ = AtLevel(nativeTownResidents_, level, static value => value.LevelId, legacyZeroWildcard: false);
        setLevelObjectOrigins_ = AtLevel(setLevelObjectOrigins_, level, static value => value.LevelId, legacyZeroWildcard: false);
        proceduralQuestPieceOrigins_ = AtLevel(proceduralQuestPieceOrigins_, level, static value => value.LevelId, legacyZeroWildcard: false);
        portals_ = new();
        foreach (var portal in other.portals_)
            if (LevelMembership.IsOnLevel(portal.SourceLevelId, level, legacyZeroWildcard: false)
                || portal.Bidirectional && LevelMembership.IsOnLevel(portal.DestinationLevelId, level, legacyZeroWildcard: false))
                portals_.Add(portal);
    }

    private static RepeatedField<T> AtLevel<T>(RepeatedField<T> source, uint level, Func<T, uint> levelId, bool legacyZeroWildcard = true)
    {
        var result = new RepeatedField<T>();
        foreach (var value in source)
            if (LevelMembership.IsOnLevel(levelId(value), level, legacyZeroWildcard)) result.Add(value);
        return result;
    }
}

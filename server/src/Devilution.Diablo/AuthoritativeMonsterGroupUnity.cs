namespace Devilution.Diablo;

/** Native relationship state used by a monster pack's GroupUnity pass. */
public enum AuthoritativeMonsterLeaderRelation
{
    None,
    Leashed,
    Separated,
}

/** Applies the server-owned portion of native GroupUnity deterministically. */
public static class AuthoritativeMonsterGroupUnity
{
    /** Native reunion admits a separated member strictly below four tiles. */
    public const int ReunionDistance = 4;

    /** Native isDirOK gate for a leader carrying a leashed pack. */
    public static bool CanLeaderMove(
        AuthoritativeCombatTarget leader,
        IReadOnlyDictionary<uint, AuthoritativeCombatTarget> monsters)
    {
        ArgumentNullException.ThrowIfNull(leader);
        ArgumentNullException.ThrowIfNull(monsters);
        if (leader.PackSize == 0)
            return true;

        var leashedMembers = monsters.Values
            .Where(monster => monster.LeaderEntityId == leader.EntityId
                && monster.LeaderRelation == AuthoritativeMonsterLeaderRelation.Leashed)
            .ToArray();
        if (leashedMembers.Length != leader.PackSize)
            return false;

        var futureX = leader.IsMoving ? leader.FuturePositionX : leader.PositionX;
        var futureY = leader.IsMoving ? leader.FuturePositionY : leader.PositionY;
        return leashedMembers.All(member =>
            member.LevelId == leader.LevelId
            && AuthoritativeCombatTarget.ChebyshevDistance(
                member.PositionX,
                member.PositionY,
                futureX,
                futureY) < ReunionDistance);
    }

    /** Native isDirOK gate for a leashed member's candidate movement cell. */
    public static bool CanLeashedMemberMove(
        AuthoritativeCombatTarget member,
        IReadOnlyDictionary<uint, AuthoritativeCombatTarget> monsters)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(monsters);
        if (member.LeaderRelation != AuthoritativeMonsterLeaderRelation.Leashed)
            return true;
        if (member.LeaderEntityId == 0
            || member.LeaderEntityId == member.EntityId
            || !monsters.TryGetValue(member.LeaderEntityId, out var leader)
            || leader.HitPoints <= 0
            || leader.LevelId != member.LevelId) {
            return false;
        }

        var leaderX = leader.IsMoving ? leader.FuturePositionX : leader.PositionX;
        var leaderY = leader.IsMoving ? leader.FuturePositionY : leader.PositionY;
        var memberX = member.IsMoving ? member.FuturePositionX : member.PositionX;
        var memberY = member.IsMoving ? member.FuturePositionY : member.PositionY;
        return AuthoritativeCombatTarget.ChebyshevDistance(
            memberX,
            memberY,
            leaderX,
            leaderY) < ReunionDistance;
    }

    /** Native FollowTheLeader activation timing for a leashed member. */
    public static bool ApplyFollowTheLeader(
        AuthoritativeCombatTarget member,
        IReadOnlyDictionary<uint, AuthoritativeCombatTarget> monsters)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(monsters);
        if (member.LeaderRelation != AuthoritativeMonsterLeaderRelation.Leashed
            || member.LeaderEntityId == 0
            || member.LeaderEntityId == member.EntityId
            || !monsters.TryGetValue(member.LeaderEntityId, out var leader))
            return false;

        if (leader.ActiveForTicks > member.ActiveForTicks)
            member.RestoreActiveForTicks(leader.ActiveForTicks - 1);
        return true;
    }

    /**
     * Updates one member against its authoritative leader. A missing, dead,
     * cross-level, or self-referential leader breaks the relationship instead
     * of allowing stale state to influence later simulation ticks.
     */
    public static bool Apply(
        AuthoritativeCombatTarget monster,
        IReadOnlyDictionary<uint, AuthoritativeCombatTarget> monsters,
        Func<uint, int, int, int, int, bool> hasLineOfSight)
    {
        ArgumentNullException.ThrowIfNull(monster);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(hasLineOfSight);

        if (monster.LeaderRelation == AuthoritativeMonsterLeaderRelation.None)
            return false;

        if (monster.IsUnique
            || monster.LeaderEntityId == 0
            || monster.LeaderEntityId == monster.EntityId
            || !monsters.TryGetValue(monster.LeaderEntityId, out var leader)
            || leader.HitPoints <= 0
            || leader.LevelId != monster.LevelId) {
            monster.BreakLeaderRelation();
            return true;
        }

        var leaderX = leader.IsMoving ? leader.FuturePositionX : leader.PositionX;
        var leaderY = leader.IsMoving ? leader.FuturePositionY : leader.PositionY;
        var lineIsClear = hasLineOfSight(
            monster.LevelId,
            monster.PositionX,
            monster.PositionY,
            leaderX,
            leaderY);
        if (lineIsClear) {
            if (monster.LeaderRelation == AuthoritativeMonsterLeaderRelation.Separated
                && AuthoritativeCombatTarget.ChebyshevDistance(
                    monster.PositionX,
                    monster.PositionY,
                    leaderX,
                    leaderY) < ReunionDistance) {
                leader.IncrementPackSize();
                monster.SetLeaderRelation(AuthoritativeMonsterLeaderRelation.Leashed);
            }
        } else if (monster.LeaderRelation == AuthoritativeMonsterLeaderRelation.Leashed) {
            leader.DecrementPackSize();
            monster.SetLeaderRelation(AuthoritativeMonsterLeaderRelation.Separated);
        }

        if (monster.LeaderRelation == AuthoritativeMonsterLeaderRelation.Leashed
            && monster.ActiveForTicks > leader.ActiveForTicks) {
            leader.RestoreActiveForTicks(monster.ActiveForTicks - 1);
        }

        return true;
    }
}

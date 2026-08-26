using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeMonsterGroupUnityTests
{
    [Fact]
    public void ReunitesSeparatedMemberWithinFourTiles()
    {
        var leader = new AuthoritativeCombatTarget(1, 5, 5, 10, packSize: 2);
        var member = new AuthoritativeCombatTarget(
            2,
            5,
            7,
            10,
            leaderEntityId: leader.EntityId,
            leaderRelation: AuthoritativeMonsterLeaderRelation.Separated);
        var world = new DiabloWorldState(combatTargets: [leader, member]);

        Assert.True(AuthoritativeMonsterGroupUnity.Apply(
            member,
            world.CombatTargets,
            world.HasLineOfSight));

        Assert.Equal(AuthoritativeMonsterLeaderRelation.Leashed, member.LeaderRelation);
        Assert.Equal(3, leader.PackSize);
    }

    [Fact]
    public void SeparatesLeashedMemberWhenLineOfSightIsBlocked()
    {
        var leader = new AuthoritativeCombatTarget(1, 5, 5, 10, packSize: 1);
        var member = new AuthoritativeCombatTarget(
            2,
            5,
            7,
            10,
            leaderEntityId: leader.EntityId,
            leaderRelation: AuthoritativeMonsterLeaderRelation.Leashed);
        var world = new DiabloWorldState(
            combatTargets: [leader, member],
            fallbackBlockedCells: [6 * 40 + 5]);

        Assert.True(AuthoritativeMonsterGroupUnity.Apply(
            member,
            world.CombatTargets,
            world.HasLineOfSight));

        Assert.Equal(AuthoritativeMonsterLeaderRelation.Separated, member.LeaderRelation);
        Assert.Equal(0, leader.PackSize);
    }

    [Fact]
    public void MissingLeaderBreaksStaleRelationship()
    {
        var member = new AuthoritativeCombatTarget(
            2,
            5,
            7,
            10,
            leaderEntityId: 1,
            leaderRelation: AuthoritativeMonsterLeaderRelation.Leashed);
        var world = new DiabloWorldState(combatTargets: [member]);

        Assert.True(AuthoritativeMonsterGroupUnity.Apply(
            member,
            world.CombatTargets,
            world.HasLineOfSight));

        Assert.Equal(0U, member.LeaderEntityId);
        Assert.Equal(AuthoritativeMonsterLeaderRelation.None, member.LeaderRelation);
    }

    [Fact]
    public void CatalogLoadsOptionalPackRelationshipFields()
    {
        var catalog = AuthoritativeMonsterCatalog.LoadTsv(
            "monsters.tsv",
            "entity_id\tmonster_id\tmonster_symbol\tposition_x\tposition_y\thit_points\tmax_hit_points\tleader_entity_id\tleader_relation\tpack_size\n"
                + "2\t1\tmember\t2\t2\t10\t10\t1\tleashed\t1\n");

        var member = Assert.Single(catalog.Targets);
        Assert.Equal(
            (1U, AuthoritativeMonsterLeaderRelation.Leashed, 1),
            (member.LeaderEntityId, member.LeaderRelation, member.PackSize));
    }

    [Fact]
    public void LeaderMovementAdmissionRequiresEveryLeashedMemberWithinFourTiles()
    {
        var leader = new AuthoritativeCombatTarget(1, 5, 5, 10, packSize: 2);
        var nearby = new AuthoritativeCombatTarget(
            2,
            5,
            8,
            10,
            leaderEntityId: leader.EntityId,
            leaderRelation: AuthoritativeMonsterLeaderRelation.Leashed);
        var distant = new AuthoritativeCombatTarget(
            3,
            5,
            10,
            10,
            leaderEntityId: leader.EntityId,
            leaderRelation: AuthoritativeMonsterLeaderRelation.Leashed);
        var world = new DiabloWorldState(combatTargets: [leader, nearby, distant]);

        Assert.False(AuthoritativeMonsterGroupUnity.CanLeaderMove(
            leader,
            world.CombatTargets));
    }

    [Fact]
    public void LeaderMovementAdmissionRejectsPackSizeDrift()
    {
        var leader = new AuthoritativeCombatTarget(1, 5, 5, 10, packSize: 2);
        var member = new AuthoritativeCombatTarget(
            2,
            5,
            6,
            10,
            leaderEntityId: leader.EntityId,
            leaderRelation: AuthoritativeMonsterLeaderRelation.Leashed);
        var world = new DiabloWorldState(combatTargets: [leader, member]);

        Assert.False(AuthoritativeMonsterGroupUnity.CanLeaderMove(
            leader,
            world.CombatTargets));
    }

    [Fact]
    public void LeashedMemberMovementAdmissionRequiresStrictFourTileEnvelope()
    {
        var leader = new AuthoritativeCombatTarget(1, 5, 5, 10, packSize: 1);
        var member = new AuthoritativeCombatTarget(
            2,
            5,
            8,
            10,
            moving: true,
            futurePositionX: 5,
            futurePositionY: 9,
            leaderEntityId: leader.EntityId,
            leaderRelation: AuthoritativeMonsterLeaderRelation.Leashed);
        var world = new DiabloWorldState(combatTargets: [leader, member]);

        Assert.False(AuthoritativeMonsterGroupUnity.CanLeashedMemberMove(
            member,
            world.CombatTargets));
    }

    [Fact]
    public void LeashedMemberMovementAdmissionAllowsThreeTileEnvelope()
    {
        var leader = new AuthoritativeCombatTarget(1, 5, 5, 10, packSize: 1);
        var member = new AuthoritativeCombatTarget(
            2,
            5,
            7,
            10,
            moving: true,
            futurePositionX: 5,
            futurePositionY: 8,
            leaderEntityId: leader.EntityId,
            leaderRelation: AuthoritativeMonsterLeaderRelation.Leashed);
        var world = new DiabloWorldState(combatTargets: [leader, member]);

        Assert.True(AuthoritativeMonsterGroupUnity.CanLeashedMemberMove(
            member,
            world.CombatTargets));
    }

    [Fact]
    public void FollowTheLeaderPropagatesActivationFromLeaderToMember()
    {
        var leader = new AuthoritativeCombatTarget(
            1,
            5,
            5,
            10,
            packSize: 1,
            activeForTicks: 5);
        var member = new AuthoritativeCombatTarget(
            2,
            5,
            6,
            10,
            leaderEntityId: leader.EntityId,
            leaderRelation: AuthoritativeMonsterLeaderRelation.Leashed);
        var world = new DiabloWorldState(combatTargets: [leader, member]);

        Assert.True(AuthoritativeMonsterGroupUnity.ApplyFollowTheLeader(
            member,
            world.CombatTargets));

        Assert.Equal(4, member.ActiveForTicks);
    }

    [Fact]
    public void GroupUnityPropagatesActivationFromMemberToLeader()
    {
        var leader = new AuthoritativeCombatTarget(
            1,
            5,
            5,
            10,
            packSize: 1,
            activeForTicks: 1);
        var member = new AuthoritativeCombatTarget(
            2,
            5,
            6,
            10,
            leaderEntityId: leader.EntityId,
            leaderRelation: AuthoritativeMonsterLeaderRelation.Leashed,
            activeForTicks: 5);
        var world = new DiabloWorldState(combatTargets: [leader, member]);

        Assert.True(AuthoritativeMonsterGroupUnity.Apply(
            member,
            world.CombatTargets,
            world.HasLineOfSight));

        Assert.Equal(4, leader.ActiveForTicks);
    }

    [Fact]
    public void SimulationRunsFollowTheLeaderBeforeMonsterAi()
    {
        var leader = new AuthoritativeCombatTarget(
            1,
            5,
            5,
            10,
            packSize: 1,
            activeForTicks: 5);
        var member = new AuthoritativeCombatTarget(
            2,
            5,
            6,
            10,
            leaderEntityId: leader.EntityId,
            leaderRelation: AuthoritativeMonsterLeaderRelation.Leashed);
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules) {
                CombatTargets = [leader, member],
            });

        simulation.AdvanceTick(1, []);

        Assert.Equal(4, member.ActiveForTicks);
    }

    [Fact]
    public void SimulationRunsGroupUnityForAuthoritativeMembers()
    {
        var leader = new AuthoritativeCombatTarget(1, 5, 5, 10, packSize: 2);
        var member = new AuthoritativeCombatTarget(
            2,
            5,
            7,
            10,
            leaderEntityId: leader.EntityId,
            leaderRelation: AuthoritativeMonsterLeaderRelation.Separated);
        var simulation = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules) {
                CombatTargets = [leader, member],
            });

        simulation.AdvanceTick(1, []);

        Assert.Equal(AuthoritativeMonsterLeaderRelation.Leashed, member.LeaderRelation);
        Assert.Equal(3, leader.PackSize);
    }

    [Fact]
    public void SnapshotRoundTripsPackRelationshipState()
    {
        var leader = new AuthoritativeCombatTarget(
            1,
            5,
            5,
            10,
            packSize: 2,
            activeForTicks: 7);
        var member = new AuthoritativeCombatTarget(
            2,
            5,
            7,
            10,
            leaderEntityId: leader.EntityId,
            leaderRelation: AuthoritativeMonsterLeaderRelation.Leashed,
            activeForTicks: 3);
        var source = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(
                new StoreCatalog(),
                DiabloModule.Instance.Rules) {
                CombatTargets = [leader, member],
            });
        var player = CreatePlayer();
        var snapshot = source.CaptureSnapshot(player, 1);
        var destination = DiabloSimulationFactory.Instance.Create(
            new DiabloSimulationOptions(new StoreCatalog(), DiabloModule.Instance.Rules));

        destination.RestoreSnapshot(CreatePlayer(), snapshot);

        var restoredLeader = destination.WorldState.CombatTargets[leader.EntityId];
        var restoredMember = destination.WorldState.CombatTargets[member.EntityId];
        Assert.Equal(2, restoredLeader.PackSize);
        Assert.Equal(7, restoredLeader.ActiveForTicks);
        Assert.Equal(3, restoredMember.ActiveForTicks);
        Assert.Equal(
            (leader.EntityId, AuthoritativeMonsterLeaderRelation.Leashed),
            (restoredMember.LeaderEntityId, restoredMember.LeaderRelation));
    }

    private static AuthoritativePlayerState CreatePlayer()
        => new(
            0,
            0,
            100,
            0,
            0,
            PlayerAttributesState.Zero,
            [],
            Enumerable.Repeat(-1, 40).ToArray(),
            [],
            1,
            1,
            100,
            1,
            0,
            0);
}

using Devilution.Client.Acceptance;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeCombatApproachTargetTests
{
    [Fact]
    public void DetouringAwayFromOneMonsterDoesNotSwitchToAnotherAndWalkBack()
    {
        var first = Monster(11, 9, 3);
        var second = Monster(12, 3, 5);
        var monsters = new[] { first, second };
        var approach = new NativeCombatApproachTarget();
        var position = (X: 6, Y: 6);
        long Distance(MonsterSnapshot value) => Math.Max(Math.Abs(position.X - value.PositionX), Math.Abs(position.Y - value.PositionY));
        var chosen = approach.Candidates(monsters, Distance).First();
        Assert.Equal(first.EntityId, chosen.EntityId);
        approach.Follow(chosen.EntityId);
        // A wall forces the first step away from the chosen target. Reselecting
        // by distance here would choose the other monster and reverse the walk.
        position = (6, 7);
        Assert.True(Distance(second) < Distance(first));
        Assert.Equal(first.EntityId, approach.Candidates(monsters, Distance).First().EntityId);
        // Continue the detour rather than just detecting and abandoning the cycle.
        foreach (var step in new[] {(6, 8), (7, 9), (8, 9), (9, 9), (9, 8), (9, 7), (9, 6), (9, 5), (9, 4)}) {
            position = step;
            Assert.Equal(first.EntityId, approach.Candidates(monsters, Distance).First().EntityId);
        }
        Assert.Equal(1, Distance(first));
    }

    [Fact]
    public void ClearingASpecificThreatNeverGoesLookingForUnrelatedMonsters()
    {
        var requested = Monster(12, 9, 3);
        var nearby = Monster(11, 6, 7);
        var approach = new NativeCombatApproachTarget(requested.EntityId);
        Assert.Same(requested, Assert.Single(approach.Candidates([nearby, requested], value => value.EntityId == nearby.EntityId ? 1 : 5)));
        // If that threat dies or has no route, no replacement hunt is required for its recovery call.
        Assert.Empty(approach.Candidates([nearby], _ => 1));
    }

    [Fact]
    public void ADisappearedOrUnreachableChosenTargetAllowsAnotherReachableCandidate()
    {
        var first = Monster(11, 6, 7); var alternative = Monster(12, 9, 3);
        var approach = new NativeCombatApproachTarget();
        approach.Follow(first.EntityId);
        Assert.Same(alternative, Assert.Single(approach.Candidates([alternative], _ => 5)));
        // The caller still tests each candidate's route; retaining a target does not hide alternatives.
        var reachable = approach.Candidates([alternative, first], _ => 1).First(value => value.EntityId != first.EntityId);
        approach.Follow(reachable.EntityId);
        Assert.Same(alternative, approach.Candidates([first, alternative], value => value.EntityId == first.EntityId ? 0 : 5).First());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnImmediatelyAttackableInterceptorCanBeDefendedAgainstDuringADetour(bool specificThreat)
    {
        var chosen = Monster(11, 9, 3); var interceptor = Monster(12, 6, 7);
        var approach = new NativeCombatApproachTarget(specificThreat ? chosen.EntityId : null);
        approach.Follow(chosen.EntityId);
        var targets = approach.Candidates([chosen, interceptor], value => value.EntityId == interceptor.EntityId ? 1 : 5,
            value => value.EntityId == interceptor.EntityId).ToArray();
        Assert.Same(interceptor, targets[0]);
        Assert.Same(chosen, targets[1]);
        // Defense does not replace the target of the pending approach once the interceptor is gone.
        Assert.Same(chosen, approach.Candidates([chosen], _ => 5, _ => false).First());
    }

    [Fact]
    public void InitialSearchStillOrdersByDistanceThenIdentityAndKeepsItsCandidateBound()
    {
        var targets = Enumerable.Range(1, 20).Select(id => Monster((uint)id, id, 0)).Reverse().ToArray();
        var selected = new NativeCombatApproachTarget().Candidates(targets, value => value.EntityId % 3).ToArray();
        Assert.Equal(12, selected.Length);
        Assert.Equal(targets.OrderBy(value => value.EntityId % 3).ThenBy(value => value.EntityId).Take(12), selected);
    }

    [Fact]
    public void MissingShooterFallsBackOnceAndNewObservationsCannotRearmTheDiversion()
    {
        var attack = new NativeCombatAttackTarget(12);
        attack.PreferRanged(99, currentIsRanged: false);
        Assert.Equal(99U, attack.Current);
        Assert.True(attack.FallBack());
        for (uint observation = 0; observation < 20; observation++) {
            attack.PreferRanged(99 + observation, currentIsRanged: false);
            Assert.Equal(12U, attack.Current);
            Assert.False(attack.FallBack());
        }
    }

    [Fact]
    public async Task FailureOfTheOriginalTargetStillReturnsNullAndAllSearchesAreBounded()
    {
        var attack = new NativeCombatAttackTarget(12);
        attack.PreferRanged(99, currentIsRanged: false);
        var attempts = new List<uint>();
        Task<MonsterSnapshot> Unreachable(uint id) {
            attempts.Add(id);
            throw new InvalidOperationException("No route.");
        }
        Assert.Null(await attack.ApproachAsync(Unreachable, (_, _) => true, () => true));
        attack.PreferRanged(100, currentIsRanged: false);
        Assert.Null(await attack.ApproachAsync(Unreachable, (_, _) => true, () => true));
        Assert.Equal(new uint[] { 99, 12, 12 }, attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeathWrongFloorAndNonApproachFailuresNeverFallBack(bool otherFailure)
    {
        var attack = new NativeCombatAttackTarget(12);
        attack.PreferRanged(99, currentIsRanged: false);
        Exception failure = otherFailure ? new FormatException("Invalid snapshot.") : new InvalidOperationException("Character died or left the floor.");
        var attempts = 0;
        var observed = await Record.ExceptionAsync(() => attack.ApproachAsync(_ => {
            attempts++; throw failure;
        }, (_, _) => true, () => otherFailure));
        Assert.Same(failure, observed);
        Assert.Equal(1, attempts);
        Assert.Equal(99U, attack.Current);
    }

    [Fact]
    public async Task SuccessfulCounterFireAndAdjacentInterceptorsKeepTheChosenTarget()
    {
        var attack = new NativeCombatAttackTarget(12);
        var shooter = Monster(99, 9, 3); var interceptor = Monster(100, 6, 7);
        attack.PreferRanged(shooter.EntityId, currentIsRanged: false);
        Assert.Same(shooter, await attack.ApproachAsync(id => Task.FromResult(shooter), (monster, id) => monster.EntityId == id, () => true));
        Assert.Same(interceptor, await attack.ApproachAsync(_ => Task.FromResult(interceptor), (_, _) => true, () => true));
        attack.PreferRanged(101, currentIsRanged: true);
        Assert.Equal(shooter.EntityId, attack.Current);
    }

    private static MonsterSnapshot Monster(uint id, int x, int y)
        => new() { EntityId = id, LevelId = 5, PositionX = x, PositionY = y, Alive = true };
}

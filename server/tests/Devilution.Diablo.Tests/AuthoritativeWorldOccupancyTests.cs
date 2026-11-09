using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeWorldOccupancyTests
{
    [Theory]
    [InlineData(0U, true, false, false, true)]
    [InlineData(1U, true, false, false, true)]
    [InlineData(2U, true, false, false, false)]
    [InlineData(1U, false, false, false, false)]
    [InlineData(1U, true, true, false, true)]
    [InlineData(1U, true, false, true, false)]
    public void MonsterOccupancyPreservesLegacyLevelsDeathReservationsAndExclusion(
        uint level, bool alive, bool future, bool excluded, bool expected)
    {
        var monster = new AuthoritativeCombatTarget(2, 3, 4, alive ? 1 : 0, maxHitPoints: 1,
            levelId: level, moving: future, futurePositionX: future ? 4 : 3, futurePositionY: 4);
        var world = new DiabloWorldState(combatTargets: [monster]);
        Assert.Equal(expected, world.IsOccupied(1, future ? 4 : 3, 4, [], excluded ? 2U : 0U));
    }

    [Theory]
    [InlineData(false, false, false, 1U, true)]
    [InlineData(true, false, false, 1U, false)]
    [InlineData(false, true, false, 1U, false)]
    [InlineData(false, false, true, 1U, false)]
    [InlineData(false, false, false, 2U, false)]
    public void OnlyAnIntactUnoperatedSolidObjectOnTheSelectedLevelOccupiesTheCell(
        bool activated, bool destroyed, bool occupancyTrigger, uint objectLevel, bool expected)
    {
        var value = new AuthoritativeWorldObject(2, 1, objectLevel, 3, 4,
            activated: activated, destroyed: destroyed, breakable: destroyed,
            activateOnOccupancy: occupancyTrigger, questId: occupancyTrigger ? 1U : 0U);
        var world = new DiabloWorldState(objects: [value], quests: [new AuthoritativeQuestState(1, 1, 1)]);
        Assert.Equal(expected, world.IsOccupied(1, 3, 4, []));
        Assert.False(world.IsOccupied(1, 3, 4, [], value.EntityId));
    }

    [Fact]
    public void RepeatedNavigationQueriesWithOffLevelEntitiesAllocateNoMemory()
    {
        var world = new DiabloWorldState();
        for (uint i = 1; i <= 300; i++)
            world.Objects.Add(i, new AuthoritativeWorldObject(i, 1, 1, (int)(i % 30), (int)(i / 30)));
        for (uint i = 400; i < 500; i++)
            world.CombatTargets.Add(i, new AuthoritativeCombatTarget(i, 2, 2, 1, levelId: 1));
        var player = DiabloPlayerTemplate.CreateDefault(0, 64, 0, 0).CreatePlayer();
        player.EntityId = 600;
        player.LevelId = 0;
        IReadOnlyList<AuthoritativePlayerState> players = new[] { player };
        for (var i = 0; i < 64; i++) Assert.False(world.IsOccupied(0, 3, 4, players, player.EntityId));
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1024; i++) world.IsOccupied(0, 3, 4, players, player.EntityId);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        player.PositionX = 3; player.PositionY = 4;
        Assert.True(world.IsOccupied(0, 3, 4, players));
    }
}

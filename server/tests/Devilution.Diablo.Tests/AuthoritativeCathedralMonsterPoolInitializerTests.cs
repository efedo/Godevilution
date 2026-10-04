using Devilution.Diablo;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class AuthoritativeCathedralMonsterPoolInitializerTests
{
    [Theory]
    [InlineData(0U, 1977929968U, new[] { 0, 5, 2, 2, 7, 1, 1, 7 }, new[] { 0, 0, 0, 4, 6, 3, 7, 6 })]
    [InlineData(1U, 2221433265U, new[] { 2, 1, 7, 3, 4, 4, 3, 3 }, new[] { 4, 7, 2, 5, 0, 1, 1, 4 })]
    [InlineData(0x80000000U, 4125413616U, new[] { 7, 2, 5, 5, 0, 6, 6, 0 }, new[] { 7, 7, 7, 3, 1, 4, 0, 1 })]
    [InlineData(0xFFFFFFFFU, 1734426671U, new[] { 2, 6, 4, 0, 6, 7, 2, 4 }, new[] { 5, 6, 1, 3, 3, 2, 7, 2 })]
    public void FreshClearConsumesTwoDrawsPerSlotAndPreservesNativeDefaultFields(
        uint seed, uint expectedFinalState, int[] firstDirections, int[] lastDirections)
    {
        // Literal LCG transcripts include both signed-seed halves. The private
        // native oracle separately compares every slot for generated levels.
        var result = AuthoritativeCathedralMonsterPoolInitializer.CreateFresh(seed);
        Assert.Equal(expectedFinalState, result.FinalRandomState);
        Assert.Equal(200, result.Monsters.Count);
        Assert.Equal(firstDirections, result.Monsters.Take(8).Select(monster => monster.Direction));
        Assert.Equal(lastDirections, result.Monsters.TakeLast(8).Select(monster => monster.Direction));
        foreach (var monster in result.Monsters) {
            Assert.InRange(monster.Direction, 0, 7);
            Assert.Equal(new AuthoritativeCathedralPopulationMonsterState {
                Slot = monster.Slot, Type = 109, Direction = monster.Direction,
                Flags = 1024, LightId = -1,
            }, monster);
        }
        Assert.Equal(Enumerable.Range(0, 200), result.Monsters.Select(monster => monster.Slot));
    }

    [Fact]
    public void FreshPoolsAreDetachedImmutableAndDoNotShareLifecycleState()
    {
        var first = AuthoritativeCathedralMonsterPoolInitializer.CreateFresh(2588);
        var second = AuthoritativeCathedralMonsterPoolInitializer.CreateFresh(2588);
        Assert.Equal(first.Monsters, second.Monsters);
        Assert.NotSame(first.Monsters, second.Monsters);
        Assert.NotSame(first.Monsters[0], second.Monsters[0]);
        var slots = Assert.IsAssignableFrom<IList<AuthoritativeCathedralPopulationMonsterState>>(first.Monsters);
        Assert.Throws<NotSupportedException>(() => slots[0] = slots[0] with { Flags = 0 });
        var activePrefix = first.Monsters.ToArray();
        activePrefix[0] = activePrefix[0] with { Tile = new(1, 0), Flags = 32 };
        Assert.Equal(new AuthoritativeCathedralPopulationPoint(0, 0), first.Monsters[0].Tile);
        Assert.Equal(1024U, second.Monsters[0].Flags);
    }
}

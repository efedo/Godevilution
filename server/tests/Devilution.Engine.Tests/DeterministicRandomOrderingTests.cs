using Devilution.Engine;
using Xunit;

namespace Devilution.Engine.Tests;

public sealed class DeterministicRandomOrderingTests
{
    [Fact]
    public void ShuffleUsesNativeDescendingFisherYatesOrder()
    {
        var values = new[] { 'a', 'b', 'c', 'd' };
        var draws = new Queue<int>([0, 1, 0]);

        DeterministicRandomOrdering.Shuffle(values, _ => draws.Dequeue());

        Assert.Equal(['c', 'd', 'b', 'a'], values);
        Assert.Empty(draws);
    }

    [Fact]
    public void ShuffleLeavesSingletonsAndEmptyCollectionsUntouched()
    {
        var singleton = new[] { 7 };
        var empty = Array.Empty<int>();
        var calls = 0;

        DeterministicRandomOrdering.Shuffle(singleton, _ => calls++);
        DeterministicRandomOrdering.Shuffle(empty, _ => calls++);

        Assert.Equal([7], singleton);
        Assert.Empty(empty);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void ShuffleRejectsAnOutOfRangeRandomIndex()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DeterministicRandomOrdering.Shuffle(
                new[] { 1, 2 },
                _ => 2));
    }
}

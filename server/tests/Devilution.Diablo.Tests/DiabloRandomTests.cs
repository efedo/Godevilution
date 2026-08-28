using Devilution.Diablo;
using Devilution.Engine;
using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class DiabloRandomTests
{
    [Fact]
    public void CompatibilityFacadeMatchesEngineSequenceAndState()
    {
        var facade = new DiabloRandom(0x12345678);
        var engine = new DeterministicLegacyRandom(0x12345678);

        Assert.Equal(engine.NextUInt32(), facade.NextUInt32());
        Assert.Equal(engine.GenerateRnd(40_000), facade.GenerateRnd(40_000));
        Assert.Equal(engine.RandomIntBetween(-10, 25), facade.RandomIntBetween(-10, 25));
        Assert.Equal(engine.State, facade.State);
    }

    [Fact]
    public void CompatibilityFacadeRestoresEngineStateIdentically()
    {
        var facade = new DiabloRandom(7);
        var engine = new DeterministicLegacyRandom(7);
        var restoredState = engine.NextUInt32();
        _ = facade.NextUInt32();

        facade.Restore(restoredState);
        engine.Restore(restoredState);

        Assert.Equal(engine.NextUInt32(), facade.NextUInt32());
        Assert.Equal(engine.RandomIntLessThan(100), facade.RandomIntLessThan(100));
    }
}

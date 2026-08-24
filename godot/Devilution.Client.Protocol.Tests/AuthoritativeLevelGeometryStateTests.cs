using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeLevelGeometryStateTests
{
    [Fact]
    public void ReplacesPriorProjectionInsteadOfRetainingOmittedLevels()
    {
        var state = new AuthoritativeLevelGeometryState();
        state.Replace([
            new LevelGeometrySnapshot { LevelId = 1, Width = 10, Height = 10 },
            new LevelGeometrySnapshot { LevelId = 2, Width = 12, Height = 12 },
        ]);

        state.Replace([
            new LevelGeometrySnapshot { LevelId = 2, Width = 20, Height = 20 },
        ]);

        Assert.Single(state.Definitions);
        Assert.Equal(20, state.Definitions[2].Width);
        Assert.DoesNotContain(1U, state.Definitions.Keys);
    }

    [Fact]
    public void EmptyProjectionClearsPriorGeometry()
    {
        var state = new AuthoritativeLevelGeometryState();
        state.Replace([
            new LevelGeometrySnapshot { LevelId = 1, Width = 10, Height = 10 },
        ]);

        state.Replace([]);

        Assert.Empty(state.Definitions);
    }

    [Fact]
    public void SkipsNonPositivePresentationDimensions()
    {
        var state = new AuthoritativeLevelGeometryState();
        state.Replace([
            new LevelGeometrySnapshot { LevelId = 1, Width = 0, Height = 10 },
            new LevelGeometrySnapshot { LevelId = 2, Width = 10, Height = -1 },
        ]);

        Assert.Empty(state.Definitions);
    }
}

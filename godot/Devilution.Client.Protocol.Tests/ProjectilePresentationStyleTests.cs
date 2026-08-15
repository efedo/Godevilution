using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class ProjectilePresentationStyleTests
{
    [Theory]
    [InlineData(16, 0, ProjectilePresentationKind.Fire)]
    [InlineData(16, 1, ProjectilePresentationKind.FireImpact)]
    [InlineData(20, 0, ProjectilePresentationKind.BloodStar)]
    [InlineData(21, 0, ProjectilePresentationKind.BoneSpirit)]
    [InlineData(31, 0, ProjectilePresentationKind.Lightning)]
    [InlineData(51, 0, ProjectilePresentationKind.Acid)]
    [InlineData(59, 0, ProjectilePresentationKind.Arrow)]
    public void ResolvesAuthoritativeProjectileFamilies(
        int behavior,
        int phase,
        ProjectilePresentationKind expected)
    {
        Assert.Equal(expected, ProjectilePresentationStyle.Resolve(behavior, phase));
    }

    [Theory]
    [InlineData(11, ProjectilePresentationKind.Beam)]
    [InlineData(13, ProjectilePresentationKind.StoneCurse)]
    [InlineData(999, ProjectilePresentationKind.Generic)]
    public void KeepsSpecialAndUnknownBehaviorsDeterministic(
        int behavior,
        ProjectilePresentationKind expected)
    {
        Assert.Equal(expected, ProjectilePresentationStyle.Resolve(behavior, 0));
    }
}

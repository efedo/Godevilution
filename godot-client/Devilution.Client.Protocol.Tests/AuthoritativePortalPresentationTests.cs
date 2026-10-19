using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativePortalPresentationTests
{
    [Fact]
    public void ResolvesTownSourceLevelZeroOnTownOnly()
    {
        var endpoint = AuthoritativePortalPresentation.Resolve(
            new PortalSnapshot {
                SourceLevelId = 0,
                SourcePositionX = 4,
                SourcePositionY = 5,
                DestinationLevelId = 7,
                Bidirectional = true,
            },
            0);

        Assert.Equal(new PortalEndpoint(4, 5), endpoint);
    }

    [Fact]
    public void DoesNotTreatTownSourceAsWildcard()
    {
        var endpoint = AuthoritativePortalPresentation.Resolve(
            new PortalSnapshot {
                SourceLevelId = 0,
                SourcePositionX = 4,
                SourcePositionY = 5,
                DestinationLevelId = 7,
                Bidirectional = true,
            },
            3);

        Assert.Null(endpoint);
    }

    [Fact]
    public void ResolvesBidirectionalTownDestinationOnTownOnly()
    {
        var endpoint = AuthoritativePortalPresentation.Resolve(
            new PortalSnapshot {
                SourceLevelId = 7,
                DestinationLevelId = 0,
                DestinationPositionX = 8,
                DestinationPositionY = 9,
                Bidirectional = true,
            },
            0);

        Assert.Equal(new PortalEndpoint(8, 9), endpoint);
    }

    [Fact]
    public void DoesNotExposeOneWayDestinationOnAnotherLevel()
    {
        var endpoint = AuthoritativePortalPresentation.Resolve(
            new PortalSnapshot {
                SourceLevelId = 7,
                DestinationLevelId = 3,
                DestinationPositionX = 8,
                DestinationPositionY = 9,
                Bidirectional = false,
            },
            3);

        Assert.Null(endpoint);
    }

    [Fact]
    public void PrefersSourceWhenBothEndpointsAreVisible()
    {
        var endpoint = AuthoritativePortalPresentation.Resolve(
            new PortalSnapshot {
                SourceLevelId = 3,
                SourcePositionX = 1,
                SourcePositionY = 2,
                DestinationLevelId = 3,
                DestinationPositionX = 8,
                DestinationPositionY = 9,
                Bidirectional = true,
            },
            3);

        Assert.Equal(new PortalEndpoint(1, 2), endpoint);
    }

    [Fact]
    public void HidesPortalBehindProjectedVisionBlockers()
    {
        var geometry = new LevelGeometrySnapshot { LevelId = 3, Width = 5, Height = 1 };
        geometry.NativeVisionBlockedCells.Add(1);

        Assert.Null(AuthoritativePortalPresentation.Resolve(
            new PortalSnapshot {
                PortalId = 4,
                SourceLevelId = 3,
                SourcePositionX = 4,
                SourcePositionY = 0,
            },
            3,
            geometry,
            0,
            0,
            10));
    }
}

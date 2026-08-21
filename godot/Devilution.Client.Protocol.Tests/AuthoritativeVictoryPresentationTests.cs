using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativeVictoryPresentationTests
{
    [Fact]
    public void FormatsSinglePlayerKillLevel()
    {
        var text = AuthoritativeVictoryPresentation.Format(new VictoryEvent {
            DiabloKillLevel = 16,
        });

        Assert.Equal("Victory — Diablo defeated on level 16", text);
    }

    [Fact]
    public void OmitsAbsentMultiplayerRankLevel()
    {
        var text = AuthoritativeVictoryPresentation.Format(new VictoryEvent());

        Assert.Equal("Victory — Diablo defeated", text);
    }
}

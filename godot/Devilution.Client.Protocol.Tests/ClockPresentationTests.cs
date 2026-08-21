using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class ClockPresentationTests
{
    [Fact]
    public void FormatsLocalTimeUsingTheLegacyTwentyFourHourShape()
    {
        var value = ClockPresentation.Format(new DateTimeOffset(2026, 8, 21, 7, 5, 9, TimeSpan.Zero));

        Assert.Equal("07:05:09", value);
    }
}

using Devilution.Server.Host;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class ServerHostWarmupOptionsTests
{
    [Fact]
    public void ProductionDefaultsKeepWarmupAndTwentyHertz()
    {
        Assert.True(ServerHostOptions.TryParse([], out var options, out var error), error);
        Assert.False(options.SkipFloorEntryWarmup);
        Assert.Equal(20U, options.TickRateHz);
        Assert.False(ServerHostOptions.Defaults.SkipFloorEntryWarmup);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void WarmupOptOutIsExplicitAndDoesNotMutateDefaults(string value, bool expected)
    {
        Assert.True(ServerHostOptions.TryParse(["--skip-floor-entry-warmup", value], out var options, out var error), error);
        Assert.Equal(expected, options.SkipFloorEntryWarmup);
        Assert.False(ServerHostOptions.Defaults.SkipFloorEntryWarmup);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("maybe")]
    [InlineData("")]
    public void InvalidWarmupSettingsAreRefused(string value)
    {
        Assert.False(ServerHostOptions.TryParse(["--skip-floor-entry-warmup", value], out _, out var error));
        Assert.Contains("warm-up setting", error, StringComparison.Ordinal);
    }
}

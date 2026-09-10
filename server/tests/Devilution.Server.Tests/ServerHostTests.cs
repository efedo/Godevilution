using System.Net;
using Devilution.Protocol;
using Devilution.Server.Host;
using Devilution.Server.Simulation;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class ServerHostTests
{
    [Fact]
    public void DefaultsAreLoopbackAndTheExpectedDevelopmentPort()
    {
        Assert.Equal(IPAddress.Loopback, ServerHostOptions.Defaults.BindAddress);
        Assert.Equal(6113, ServerHostOptions.Defaults.Port);
        Assert.Equal(20U, ServerHostOptions.Defaults.TickRateHz);
        Assert.Equal(
            ProtocolSchemaVersions.IndependentContentAndRulesetIdentity,
            ServerHostOptions.Defaults.ProtocolSchemaVersion);
        Assert.Null(ServerHostOptions.Defaults.StartingGold);
        Assert.Null(ServerHostOptions.Defaults.StartingLife);
        Assert.Null(ServerHostOptions.Defaults.StartingMana);
        Assert.Null(ServerHostOptions.Defaults.StartingStatPoints);
        Assert.Null(ServerHostOptions.Defaults.StartingLevelId);
        Assert.Null(ServerHostOptions.Defaults.RandomSeed);
        Assert.Equal(AuthoritativeGameMode.Multiplayer, ServerHostOptions.Defaults.GameMode);
        Assert.Equal(AuthoritativeGameDifficulty.Normal, ServerHostOptions.Defaults.Difficulty);
        Assert.Null(ServerHostOptions.Defaults.ProceduralMonsterLimit);
        Assert.Null(ServerHostOptions.Defaults.ProceduralMonsterLevelId);
        Assert.Null(ServerHostOptions.Defaults.GameAdapterId);
        Assert.Empty(ServerHostOptions.Defaults.GameHostAdapterPackageRoots);
    }

    [Fact]
    public void ParsesHostAndContentOverrides()
    {
        var parsed = ServerHostOptions.TryParse([
            "--bind", "0.0.0.0",
            "--port", "6120",
            "--content-root", "content/custom",
            "--content-id", "modded",
            "--content-version", "2.0.0",
            "--content-overlay", "content/mod-a",
            "--content-overlay", "content/mod-b",
            "--module-package", "modules/rules-a",
            "--module-package", "modules/rules-b",
            "--game-adapter-package", "adapters/example",
            "--game-adapter", "devilution.diablo",
            "--build-id", "test-server",
            "--protocol-version", "0.2.0",
            "--tick-rate", "30",
            "--starting-gold", "900",
            "--starting-life", "4480",
            "--starting-mana", "640",
            "--starting-stat-points", "3",
            "--starting-level", "0",
            "--game-mode", "single-player",
            "--difficulty", "hell",
            "--random-seed", "305419896",
            "--procedural-monsters", "12",
            "--procedural-monster-level", "24"],
            out var options,
            out var error);

        Assert.True(parsed, error);
        Assert.Equal(IPAddress.Any, options.BindAddress);
        Assert.Equal(6120, options.Port);
        Assert.Equal("content/custom", options.ContentRoot);
        Assert.Equal("modded", options.ContentId);
        Assert.Equal("2.0.0", options.ContentVersion);
        Assert.Equal(["content/mod-a", "content/mod-b"], options.ContentOverlayRoots);
        Assert.Equal(["modules/rules-a", "modules/rules-b"], options.GameplayModulePackageRoots);
        Assert.Equal(["adapters/example"], options.GameHostAdapterPackageRoots);
        Assert.Equal("devilution.diablo", options.GameAdapterId);
        Assert.Equal(30U, options.TickRateHz);
        Assert.Equal(900U, options.StartingGold!.Value);
        Assert.Equal(4480, options.StartingLife);
        Assert.Equal(640, options.StartingMana);
        Assert.Equal(3U, options.StartingStatPoints);
        Assert.Equal(0U, options.StartingLevelId);
        Assert.Equal(AuthoritativeGameMode.SinglePlayer, options.GameMode);
        Assert.Equal(AuthoritativeGameDifficulty.Hell, options.Difficulty);
        Assert.Equal(305419896U, options.RandomSeed);
        Assert.Equal(12, options.ProceduralMonsterLimit);
        Assert.Equal(24U, options.ProceduralMonsterLevelId);
    }

    [Theory]
    [InlineData("--port", "-1")]
    [InlineData("--port", "65536")]
    [InlineData("--tick-rate", "0")]
    [InlineData("--bind", "not-an-address")]
    [InlineData("--starting-life", "-1")]
    [InlineData("--starting-mana", "-1")]
    [InlineData("--starting-stat-points", "not-a-number")]
    [InlineData("--starting-level", "not-a-level")]
    [InlineData("--random-seed", "4294967296")]
    [InlineData("--procedural-monsters", "-1")]
    [InlineData("--procedural-monster-level", "not-a-level")]
    [InlineData("--game-adapter", " ")]
    [InlineData("--game-mode", "cooperative")]
    [InlineData("--difficulty", "torment")]
    [InlineData("--unknown", "value")]
    public void RejectsInvalidOptions(string name, string value)
    {
        Assert.False(ServerHostOptions.TryParse([name, value], out _, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void RejectsAProceduralLevelWithoutAnEncounterLimit()
    {
        Assert.False(ServerHostOptions.TryParse(
            ["--procedural-monster-level", "24"],
            out _,
            out var error));
        Assert.Contains("--procedural-monsters", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RealtimeClockIsMonotonic()
    {
        var clock = new RealtimeAuthoritativeClock(1000);
        var first = clock.CurrentTick;
        await Task.Delay(5, TestContext.Current.CancellationToken);
        Assert.True(clock.CurrentTick >= first);
    }
}

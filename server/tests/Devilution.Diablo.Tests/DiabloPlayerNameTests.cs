using Xunit;

namespace Devilution.Diablo.Tests;

public sealed class DiabloPlayerNameTests
{
    [Fact]
    public void EncodesTheNativeSixBytePvpEarIdentity()
    {
        var identity = DiabloPlayerName.EncodeEarIdentity("Dead-RogueDM");

        Assert.Equal(17509U, identity.CreateInfo);
        Assert.Equal(1633955154U, identity.Seed);
    }

    [Fact]
    public void PreservesTheNativeZeroSeedForShortNames()
    {
        var identity = DiabloPlayerName.EncodeEarIdentity("A");

        Assert.Equal(0x4100U, identity.CreateInfo);
        Assert.Equal(0U, identity.Seed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("slash/name")]
    [InlineData("line\nbreak")]
    [InlineData("nonlatin-É")]
    [InlineData("fuck")]
    public void RejectsNamesOutsideTheNativeMultiplayerBoundary(string name)
    {
        Assert.False(DiabloPlayerName.IsValid(name));
        Assert.Throws<InvalidDataException>(() =>
            DiabloPlayerName.RequireValid(name, nameof(name)));
    }

    [Fact]
    public void AcceptsTheMaximumNullTerminatedCompatibilityName()
    {
        Assert.True(DiabloPlayerName.IsValid(new string('A', DiabloPlayerName.MaximumLength)));
        Assert.False(DiabloPlayerName.IsValid(new string('A', DiabloPlayerName.MaximumLength + 1)));
    }
}

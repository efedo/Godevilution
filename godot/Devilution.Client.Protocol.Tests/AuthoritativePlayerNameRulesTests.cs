using Devilution.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class AuthoritativePlayerNameRulesTests
{
    [Fact]
    public void AcceptsEmptyAndMaximumPrintableNames()
    {
        Assert.True(AuthoritativePlayerNameRules.IsValidShape(string.Empty));
        Assert.True(AuthoritativePlayerNameRules.IsValidShape(new string('A', 31)));
    }

    [Theory]
    [InlineData("bad name")]
    [InlineData("bad\nname")]
    [InlineData("bad\0name")]
    public void RejectsNonPrintableNames(string name)
    {
        Assert.False(AuthoritativePlayerNameRules.IsValidShape(name));
    }

    [Fact]
    public void RejectsNamesLongerThanWireLimit()
    {
        Assert.False(AuthoritativePlayerNameRules.IsValidShape(new string('A', 32)));
    }
}

using Devilution.Server.Host;
using Xunit;

namespace Devilution.Server.Tests;

public sealed class ServerOperatorCommandTests
{
    [Theory]
    [InlineData(null, ServerOperatorCommandKind.Empty)]
    [InlineData("", ServerOperatorCommandKind.Empty)]
    [InlineData("  ", ServerOperatorCommandKind.Empty)]
    [InlineData("help", ServerOperatorCommandKind.Help)]
    [InlineData("?", ServerOperatorCommandKind.Help)]
    [InlineData(" STATUS ", ServerOperatorCommandKind.Status)]
    [InlineData("reload", ServerOperatorCommandKind.Reload)]
    [InlineData("profile-start", ServerOperatorCommandKind.ProfileStart)]
    [InlineData("exit", ServerOperatorCommandKind.Quit)]
    [InlineData("stop", ServerOperatorCommandKind.Quit)]
    [InlineData("quit", ServerOperatorCommandKind.Quit)]
    [InlineData("reload now", ServerOperatorCommandKind.Unknown)]
    [InlineData("unknown", ServerOperatorCommandKind.Unknown)]
    public void ParsesTheLocalOperatorCommandSurface(
        string? input,
        ServerOperatorCommandKind expected)
    {
        Assert.Equal(expected, ServerOperatorCommand.Parse(input));
    }
}

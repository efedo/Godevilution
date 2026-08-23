using Devilution.Client.Protocol;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class ClientConnectionOptionsTests
{
    [Fact]
    public void AcceptsNormalHostnameAndPort()
    {
        Assert.True(ClientConnectionOptions.TryValidateEndpoint("localhost", 6113, out var error));
        Assert.Empty(error);
    }

    [Theory]
    [InlineData(null, 6113)]
    [InlineData("", 6113)]
    [InlineData("localhost", 0)]
    [InlineData("localhost", 65536)]
    public void RejectsInvalidEndpointValues(string? host, int port)
    {
        Assert.False(ClientConnectionOptions.TryValidateEndpoint(host, port, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void RejectsOversizedHostnames()
    {
        var host = new string('x', 254);

        Assert.False(ClientConnectionOptions.TryValidateEndpoint(host, 6113, out var error));
        Assert.Equal("The server host is too long.", error);
    }
}

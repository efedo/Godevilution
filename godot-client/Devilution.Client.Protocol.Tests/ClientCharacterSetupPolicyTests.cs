using Devilution.Client;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class ClientCharacterSetupPolicyTests
{
    [Fact]
    public void MissingCapabilityRetainsAllSixClassesForTheConfiguredEndpoint()
    {
        var choices = ClassesFor("127.0.0.1", 6113, null);

        Assert.Equal(new uint[] { 0, 1, 2, 3, 4, 5 }, Ids(choices));
    }

    [Fact]
    public void EmptyLauncherClearedCapabilityRetainsAllSixClassesForTheConfiguredEndpoint()
    {
        var choices = ClassesFor("127.0.0.1", 6113, string.Empty);

        Assert.Equal(new uint[] { 0, 1, 2, 3, 4, 5 }, Ids(choices));
    }

    [Fact]
    public void NativeCapabilityOffersOnlyRogueAndFallsBackWhenTheRequestedClassIsUnsupported()
    {
        var choices = ClassesFor("127.0.0.1", 6113, "1");

        Assert.Equal(new uint[] { 1 }, Ids(choices));
        Assert.Equal(1U, ClientCharacterSetupPolicy.SelectClassId(choices, requestedClassId: 0));
        Assert.Equal(1U, ClientCharacterSetupPolicy.SelectClassId(choices, requestedClassId: null));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData(",1")]
    [InlineData("1,")]
    [InlineData("1,,2")]
    [InlineData("1,1")]
    [InlineData("6")]
    [InlineData("4294967295")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("one")]
    public void RejectsMalformedDuplicateOrUnknownClassMetadata(string metadata)
    {
        Assert.Throws<InvalidDataException>(() => ClassesFor(
            "127.0.0.1", 6113, metadata));
    }

    [Fact]
    public void RemoteEndpointGetsGenericChoicesAndReturningToConfiguredEndpointRestoresTheProfileLimit()
    {
        var remote = ClientCharacterSetupPolicy.GetAvailableClasses(
            "remote.example", 6113, "127.0.0.1", 6113, "invalid");
        var otherPort = ClientCharacterSetupPolicy.GetAvailableClasses(
            "127.0.0.1", 6114, "127.0.0.1", 6113, "invalid");
        var localAgain = ClassesFor("127.0.0.1", 6113, "1");

        Assert.Equal(new uint[] { 0, 1, 2, 3, 4, 5 }, Ids(remote));
        Assert.Equal(new uint[] { 0, 1, 2, 3, 4, 5 }, Ids(otherPort));
        Assert.Equal(new uint[] { 1 }, Ids(localAgain));
    }

    [Fact]
    public void ResumeDoesNotNeedOrParseClassCapabilities()
    {
        var choices = ClientCharacterSetupPolicy.GetAvailableClasses(
            "127.0.0.1", 6113, "127.0.0.1", 6113, "invalid", resumeSession: true);

        Assert.Equal(new uint[] { 0, 1, 2, 3, 4, 5 }, Ids(choices));
    }

    [Fact]
    public void RequestedClassIsKeptWhenItIsAvailableAndOtherwiseUsesFirstAvailableChoice()
    {
        var choices = ClassesFor("127.0.0.1", 6113, null);

        Assert.Equal(2U, ClientCharacterSetupPolicy.SelectClassId(choices, requestedClassId: 2));
        Assert.Equal(0U, ClientCharacterSetupPolicy.SelectClassId(choices, requestedClassId: 9));
        Assert.Equal(0U, ClientCharacterSetupPolicy.SelectClassId(choices, requestedClassId: null));
    }

    private static IReadOnlyList<CharacterClassChoice> ClassesFor(
        string host,
        int port,
        string? supportedClassIds)
        => ClientCharacterSetupPolicy.GetAvailableClasses(
            host, port, "127.0.0.1", 6113, supportedClassIds);

    private static uint[] Ids(IReadOnlyList<CharacterClassChoice> choices)
        => choices.Select(choice => choice.Id).ToArray();
}

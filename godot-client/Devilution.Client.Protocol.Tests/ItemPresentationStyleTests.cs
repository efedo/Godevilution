using Devilution.Protocol.V1;
using Devilution.Client.Protocol;
using Xunit;
using ProtocolItemPresentationKind = Devilution.Protocol.V1.ItemPresentationKind;

namespace Devilution.Client.Protocol.Tests;

public sealed class ItemPresentationStyleTests
{
    [Theory]
    [InlineData(ProtocolItemPresentationKind.Unspecified, ItemPresentationKind.Generic)]
    [InlineData(ProtocolItemPresentationKind.Generic, ItemPresentationKind.Generic)]
    [InlineData(ProtocolItemPresentationKind.Gold, ItemPresentationKind.Gold)]
    [InlineData(ProtocolItemPresentationKind.Ear, ItemPresentationKind.Ear)]
    public void ResolvesServerOwnedItemPresentationFamilies(
        ProtocolItemPresentationKind wireKind,
        ItemPresentationKind expected)
    {
        var state = new ItemStateSnapshot { PresentationKind = wireKind };

        Assert.Equal(expected, ItemPresentationStyle.Resolve(state));
    }

    [Fact]
    public void ResolvesIdentifiedNameOnlyWhenTheServerMarksTheItemIdentified()
    {
        var state = new ItemStateSnapshot {
            BaseName = "Unidentified Sword",
            IdentifiedName = "Sword of Dawn",
            Identified = true,
        };

        Assert.Equal("Sword of Dawn", ItemPresentationStyle.ResolveName(state));

        state.Identified = false;
        Assert.Equal("Unidentified Sword", ItemPresentationStyle.ResolveName(state));
    }

    [Fact]
    public void ResolvesIdentifiedNameAsCompatibilityFallbackWhenBaseNameIsMissing()
    {
        var state = new ItemStateSnapshot {
            IdentifiedName = "Server Item",
            Identified = false,
        };

        Assert.Equal("Server Item", ItemPresentationStyle.ResolveName(state));
    }
}

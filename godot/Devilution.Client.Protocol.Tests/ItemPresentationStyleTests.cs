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
}

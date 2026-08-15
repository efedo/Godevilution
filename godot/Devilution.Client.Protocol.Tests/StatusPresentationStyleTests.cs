using Devilution.Client.Protocol;
using Devilution.Protocol.V1;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class StatusPresentationStyleTests
{
    [Theory]
    [InlineData(StatusEffectBehavior.Infravision, StatusPresentationKind.Infravision)]
    [InlineData(StatusEffectBehavior.Search, StatusPresentationKind.Search)]
    [InlineData(StatusEffectBehavior.Etherealize, StatusPresentationKind.Etherealize)]
    [InlineData(StatusEffectBehavior.Unspecified, StatusPresentationKind.None)]
    public void ResolvesAuthoritativeStatusFamilies(
        StatusEffectBehavior behavior,
        StatusPresentationKind expected)
    {
        Assert.Equal(expected, StatusPresentationStyle.Resolve(behavior));
    }
}

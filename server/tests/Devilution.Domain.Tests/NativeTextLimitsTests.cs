using Devilution.Domain.Protocol;
using Xunit;

namespace Devilution.Domain.Tests;

public sealed class NativeTextLimitsTests
{
    [Fact]
    public void IdentityLimitUsesUtf8BytesAtTheNativeBoundary()
    {
        Assert.True(NativeTextLimits.IsIdentityWithinLimit(new string('é', 64)));
        Assert.False(NativeTextLimits.IsIdentityWithinLimit(new string('é', 65)));
        Assert.True(NativeTextLimits.IsIdentityWithinLimit(
            string.Concat(Enumerable.Repeat("💡", 32))));
        Assert.False(NativeTextLimits.IsIdentityWithinLimit(
            string.Concat(Enumerable.Repeat("💡", 33))));
    }
}

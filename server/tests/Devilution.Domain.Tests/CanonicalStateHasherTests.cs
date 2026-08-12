using Devilution.Domain.Hashing;
using Xunit;

namespace Devilution.Domain.Tests;

public sealed class CanonicalStateHasherTests
{
    [Fact]
    public void PrimitivesMatchLittleEndianLengthPrefixedGoldenVector()
    {
        var hasher = new CanonicalStateHasher();
        hasher.AppendBool(true);
        hasher.AppendUInt8(7);
        hasher.AppendInt32(-42);
        hasher.AppendUInt32(9001);
        hasher.AppendUInt64(123456789);
        hasher.AppendString("griswold");

        Assert.Equal("b23c2ac66840117b646028471a19b9e5928967f875de54c461e9a4a95edf7a1b", hasher.HexDigest());
    }
}

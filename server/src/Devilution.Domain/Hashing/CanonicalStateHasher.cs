using System.Security.Cryptography;
using System.Text;

namespace Devilution.Domain.Hashing;

/** Builds the byte stream used by the cross-language authoritative state hash. */
public sealed class CanonicalStateHasher
{
    private readonly List<byte> bytes = [];

    public void AppendBool(bool value) => AppendUInt8(value ? (byte)1 : (byte)0);

    public void AppendUInt8(byte value) => bytes.Add(value);

    public void AppendInt32(int value) => AppendUInt32(unchecked((uint)value));

    public void AppendUInt32(uint value)
    {
        for (var index = 0; index < sizeof(uint); index++)
            bytes.Add((byte)(value >> (index * 8)));
    }

    public void AppendUInt64(ulong value)
    {
        for (var index = 0; index < sizeof(ulong); index++)
            bytes.Add((byte)(value >> (index * 8)));
    }

    public void AppendString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var encoded = Encoding.UTF8.GetBytes(value);
        AppendUInt64((ulong)encoded.Length);
        bytes.AddRange(encoded);
    }

    public string HexDigest()
    {
        var digest = SHA256.HashData(bytes.ToArray());
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}

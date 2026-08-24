using System.Text;

namespace Devilution.Domain.Protocol;

/** Shared byte-length limits for text carried by native-compatible protocol fields. */
public static class NativeTextLimits
{
    public const int MaximumIdentityBytes = 128;

    public static bool IsIdentityWithinLimit(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Encoding.UTF8.GetByteCount(value) <= MaximumIdentityBytes;
    }
}

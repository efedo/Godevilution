using System.Text;

namespace Devilution.Protocol;

/** Shared byte-level limits for server-owned text and opaque protocol identities. */
public static class AuthoritativeProtocolLimits
{
    public const int MaximumDialogueTextBytes = 16 * 1024;
    public const int MaximumIdentityBytes = 128;

    public static bool IsDialogueTextValid(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return !value.Contains('\0')
            && !value.Contains('\r')
            && Encoding.UTF8.GetByteCount(value) <= MaximumDialogueTextBytes;
    }

    public static bool IsIdentityWithinLimit(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Encoding.UTF8.GetByteCount(value) <= MaximumIdentityBytes;
    }
}

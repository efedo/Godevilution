using System.Text;
using Devilution.Protocol.V1;

namespace Devilution.Protocol;

/** Shared byte-level limits for server-owned text and opaque protocol identities. */
public static class AuthoritativeProtocolLimits
{
    public const int MaximumDialogueTextBytes = 16 * 1024;
    public const int MaximumIdentityBytes = 128;
    public const int MaximumProjectileBehavior =
        (int)AuthoritativeProjectileBehavior.MonsterArrow;
    public const int MaximumProjectilePresentationKind =
        (int)ProjectilePresentationKind.NovaBall;
    public const int MaximumNativeProjectileVisual =
        (int)NativeProjectileVisual.MonsterMagmaBall;

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

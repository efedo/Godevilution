using System;

namespace Devilution.Client.Acceptance
{
    /** Scalar acceptance policy shared with the launcher via Windows PowerShell Add-Type (C# 5). */
    public static class NativeDepthResumeEvidence
    {
        public static bool RequiresChestEvidence(bool hasExplicitCatacombsSeed)
        {
            return !hasExplicitCatacombsSeed;
        }

        public static bool IsThemeItemObject(uint type)
        {
            return type == 61 || type == 62 || type == 63 || type == 64 || type == 67
                || type == 77 || type == 89 || type == 90 || type == 92;
        }

        public static bool IsReusableThemeObject(uint type)
        {
            return type == 66 || type == 76;
        }

        public static bool IsDemonstrableThemeObject(uint type)
        {
            return IsThemeItemObject(type) || IsReusableThemeObject(type) || type == 81 || type == 82;
        }

        public static void VerifyThemeObject(bool required, bool hasRecord)
        {
            if (required && !hasRecord)
                throw new InvalidOperationException("The resume checkpoint has no required theme object evidence; complete the theme object journey before resuming.");
        }

        public static void Verify(bool requireShrine, bool hasShrine, bool requireTrappedDoor,
            uint doorEntity, uint trapSource, ulong firingTick)
        {
            if (requireShrine && !hasShrine)
                throw new InvalidOperationException("The resume checkpoint has no required shrine evidence; complete the shrine journey before resuming.");
            if (requireTrappedDoor && (doorEntity == 0 || trapSource == 0 || firingTick == 0))
                throw new InvalidOperationException("The resume checkpoint has no required trapped door firing evidence; complete the seeded door journey before resuming.");
        }
    }
}

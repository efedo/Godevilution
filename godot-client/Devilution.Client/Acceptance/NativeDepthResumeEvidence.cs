using System;

namespace Devilution.Client.Acceptance
{
    /** Scalar acceptance policy shared with the launcher via Windows PowerShell Add-Type (C# 5). */
    public static class NativeDepthResumeEvidence
    {
        public const uint TrappedSarcophagusLevel = 4;
        public const int TrappedSarcophagusX = 33;
        public const int TrappedSarcophagusY = 62;

        public static void VerifyTrappedSarcophagus(bool required, bool hasRecord, uint level, uint entity,
            uint source, ulong openingTick, ulong firingTick, bool hasOutcome)
        {
            if (!required && !hasRecord) return;
            var failure = string.Empty;
            if (!hasRecord) failure = "hasRecord=false";
            else if (level < 1 || level > 4) failure = "level=" + level + " (expected 1..4)";
            else if (entity == 0) failure = "entity=0";
            else if (source == 0 || entity == source) failure = "source=" + source + " (expected a nonzero wall trap distinct from entity=" + entity + ")";
            else if (openingTick == 0) failure = "openingTick=0";
            else if (firingTick <= openingTick) failure = "firingTick=" + firingTick + " (must be newer than openingTick=" + openingTick + ")";
            else if (!hasOutcome) failure = "hasOutcome=false (no attributed loot, released skeleton or corpse)";
            if (failure.Length != 0)
                throw new InvalidOperationException("The trapped sarcophagus resume checkpoint is invalid: " + failure + ".");
        }

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

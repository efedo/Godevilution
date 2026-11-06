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

namespace Devilution.Protocol;

/** Shared wire-level identity rules for regular and materialized set-level maps. */
public static class AuthoritativeLevelIdRules
{
    public const uint NativeLevelCount = 25;
    public const uint RuntimeSetLevelIdBase = 0x8000_0000;
    public const uint RuntimeSetLevelVariantCount = 18;

    public static bool IsRegularLevel(uint levelId)
        => levelId < NativeLevelCount;

    public static bool IsRuntimeSetLevel(uint levelId)
        => levelId >= RuntimeSetLevelIdBase
            && levelId < RuntimeSetLevelIdBase + RuntimeSetLevelVariantCount;

    public static bool IsSupported(uint levelId)
        => IsRegularLevel(levelId) || IsRuntimeSetLevel(levelId);
}

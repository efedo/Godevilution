using Devilution.Data;

namespace Devilution.Diablo;

/** External world identities shared by authoritative level-transition rules. */
public sealed record AuthoritativeWorldRules(uint TownLevelId)
{
    /** Native-compatible fallback used by legacy callers without world rules. */
    public static AuthoritativeWorldRules NativeDefaults { get; } = new(
        AuthoritativePortalRules.TownLevelId);

    public static AuthoritativeWorldRules LoadTsv(
        string sourcePath,
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var row = table.Rows.Single();
        return new AuthoritativeWorldRules(
            row.OptionalUInt32("town_level_id", AuthoritativePortalRules.TownLevelId));
    }

    /** Ensures the configured town identity exists in the activated geometry. */
    public void ValidateWorld(AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.ContainsLevel(TownLevelId)) {
            throw new InvalidDataException(
                $"World rules reference unknown town level {TownLevelId}.");
        }
    }
}

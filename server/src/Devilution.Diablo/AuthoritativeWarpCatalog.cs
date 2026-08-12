namespace Devilution.Diablo;

using Devilution.Data;

/** Normalized level-trigger projection consumed by the server-owned Warp spell. */
public sealed class AuthoritativeWarpTarget
{
    public AuthoritativeWarpTarget(
        uint warpId,
        uint sourceLevelId,
        int triggerX,
        int triggerY,
        uint targetLevelId,
        int targetX,
        int targetY)
    {
        if (warpId == 0)
            throw new ArgumentOutOfRangeException(nameof(warpId));
        WarpId = warpId;
        SourceLevelId = sourceLevelId;
        TriggerX = triggerX;
        TriggerY = triggerY;
        TargetLevelId = targetLevelId;
        TargetX = targetX;
        TargetY = targetY;
    }

    public uint WarpId { get; }

    public uint SourceLevelId { get; }

    public int TriggerX { get; }

    public int TriggerY { get; }

    public uint TargetLevelId { get; }

    public int TargetX { get; }

    public int TargetY { get; }
}

/** Loads normalized native trigger destinations from an external content table. */
public static class AuthoritativeWarpCatalog
{
    public static IReadOnlyList<AuthoritativeWarpTarget> LoadTsv(
        string sourcePath,
        string contents,
        AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var table = TsvTable.Parse(sourcePath, contents);
        var targets = new List<AuthoritativeWarpTarget>();
        foreach (var row in table.Rows) {
            var target = new AuthoritativeWarpTarget(
                row.RequiredUInt32("warp_id"),
                row.RequiredUInt32("source_level_id"),
                row.RequiredInt32("trigger_x"),
                row.RequiredInt32("trigger_y"),
                row.RequiredUInt32("target_level_id"),
                row.RequiredInt32("target_x"),
                row.RequiredInt32("target_y"));
            if (!world.ContainsLevel(target.SourceLevelId)
                || !world.ContainsLevel(target.TargetLevelId)) {
                throw new InvalidDataException(
                    $"Warp {target.WarpId} references an unknown source or target level.");
            }
            if (!world.ContainsPosition(
                    target.SourceLevelId,
                    target.TriggerX,
                    target.TriggerY)
                || !world.ContainsPosition(
                    target.TargetLevelId,
                    target.TargetX,
                    target.TargetY)) {
                throw new InvalidDataException(
                    $"Warp {target.WarpId} contains a position outside its level.");
            }
            targets.Add(target);
        }
        return targets;
    }
}

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
        int targetY,
        uint requiredQuestId = 0,
        bool requiredQuestCompleted = false)
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
        if (requiredQuestId == 0 && requiredQuestCompleted)
            throw new ArgumentException(
                "A quest-gated warp must identify its required quest.",
                nameof(requiredQuestCompleted));
        RequiredQuestId = requiredQuestId;
        RequiredQuestCompleted = requiredQuestCompleted;
    }

    public uint WarpId { get; }

    public uint SourceLevelId { get; }

    public int TriggerX { get; }

    public int TriggerY { get; }

    public uint TargetLevelId { get; }

    public int TargetX { get; }

    public int TargetY { get; }

    /** Optional server-owned quest gate for native level-trigger availability. */
    public uint RequiredQuestId { get; }

    /** Whether the required quest must be completed rather than active. */
    public bool RequiredQuestCompleted { get; }

    /** Stable content symbol retained alongside the numeric warp identity. */
    public string StableSymbol { get; init; } = string.Empty;
}

/** Loads normalized native trigger destinations from an external content table. */
public static class AuthoritativeWarpCatalog
{
    /** Validates quest identities referenced by content-owned level triggers. */
    public static void ValidateQuestRequirements(
        IEnumerable<AuthoritativeWarpTarget> targets,
        IEnumerable<AuthoritativeQuestState> quests)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(quests);
        var questIds = quests.Select(quest => quest.QuestId).ToHashSet();
        foreach (var target in targets.Where(target => target.RequiredQuestId != 0)) {
            if (!questIds.Contains(target.RequiredQuestId))
                throw new InvalidDataException(
                    $"Warp {target.WarpId} references unknown quest {target.RequiredQuestId}.");
        }
    }

    public static IReadOnlyList<AuthoritativeWarpTarget> LoadTsv(
        string sourcePath,
        string contents,
        AuthoritativeWorld world,
        StableContentIdCatalog? stableIds = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        var table = TsvTable.Parse(sourcePath, contents);
        var targets = new List<AuthoritativeWarpTarget>();
        foreach (var row in table.Rows) {
            var target = new AuthoritativeWarpTarget(
                AuthoritativeWarpIdentity.Resolve(row, stableIds),
                AuthoritativeLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "source_level_id",
                    "source_level_symbol"),
                row.RequiredInt32("trigger_x"),
                row.RequiredInt32("trigger_y"),
                AuthoritativeLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "target_level_id",
                    "target_level_symbol"),
                row.RequiredInt32("target_x"),
                row.RequiredInt32("target_y"),
                AuthoritativeQuestIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "required_quest_id",
                    "required_quest_symbol"),
                row.OptionalInt32("required_quest_completed") != 0) {
                StableSymbol = AuthoritativeWarpIdentity.ReadSymbol(row),
            };
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

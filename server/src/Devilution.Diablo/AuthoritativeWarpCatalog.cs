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
        bool requiredQuestCompleted = false,
        int targetSetLevelId = -1,
        bool targetSetLevelActiveMap = true)
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
        if (targetSetLevelId is < -1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(targetSetLevelId));
        TargetSetLevelId = targetSetLevelId;
        TargetSetLevelActiveMap = targetSetLevelActiveMap;
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

    /** Optional native set-level identity represented by TargetLevelId. */
    public int TargetSetLevelId { get; }

    /** Selects the active or pre-map variant for a set-level destination. */
    public bool TargetSetLevelActiveMap { get; }

    /** Stable set-level symbol retained alongside the numeric destination. */
    public string TargetSetLevelSymbol { get; init; } = string.Empty;

    /** Stable content symbol retained alongside the numeric warp identity. */
    public string StableSymbol { get; init; } = string.Empty;
}

/** Loads normalized native trigger destinations from an external content table. */
public static class AuthoritativeWarpCatalog
{
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "warp_id", "warp_symbol", "source_level_id", "source_level_symbol",
        "trigger_x", "trigger_y", "target_level_id", "target_level_symbol",
        "target_x", "target_y", "required_quest_id", "required_quest_symbol",
        "required_quest_completed", "target_set_level_id", "target_set_level_symbol",
        "target_set_level_active_map",
    ];

    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production warp table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.RequiredUInt32("warp_id");
            _ = row.RequiredUInt32("source_level_id");
            _ = row.RequiredInt32("trigger_x");
            _ = row.RequiredInt32("trigger_y");
            _ = row.RequiredInt32("target_x");
            _ = row.RequiredInt32("target_y");
        }
    }

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
        StableContentIdCatalog? stableIds = null,
        AuthoritativeSetLevelMapCatalog? setLevelMaps = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        var table = TsvTable.Parse(sourcePath, contents);
        var targets = new List<AuthoritativeWarpTarget>();
        foreach (var row in table.Rows) {
            var hasTargetSetLevel = row.TryGet("target_set_level_id", out var targetSetLevelText)
                && !string.IsNullOrWhiteSpace(targetSetLevelText)
                && !string.Equals(targetSetLevelText.Trim(), "none", StringComparison.OrdinalIgnoreCase)
                || row.TryGet("target_set_level_symbol", out var targetSetLevelSymbolText)
                && !string.IsNullOrWhiteSpace(targetSetLevelSymbolText);
            var targetSetLevelId = hasTargetSetLevel
                ? AuthoritativeSetLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "target_set_level_id",
                    "target_set_level_symbol")
                : -1;
            var targetSetLevelActiveMap = row.OptionalBoolean(
                "target_set_level_active_map",
                defaultValue: true);
            var hasOrdinaryTargetLevel = row.TryGet("target_level_id", out var targetLevelText)
                && !string.IsNullOrWhiteSpace(targetLevelText)
                || row.TryGet("target_level_symbol", out var targetLevelSymbolText)
                && !string.IsNullOrWhiteSpace(targetLevelSymbolText);
            if (hasTargetSetLevel == hasOrdinaryTargetLevel)
                throw new InvalidDataException(
                    $"Warp row {row.LineNumber} must target exactly one ordinary level or set-level variant.");
            if (hasTargetSetLevel
                && (setLevelMaps is null || !setLevelMaps.TryGet(targetSetLevelId, out _))) {
                throw new InvalidDataException(
                    $"Warp row {row.LineNumber} references unavailable set level {targetSetLevelId}.");
            }
            var targetLevelId = hasTargetSetLevel
                ? AuthoritativeSetLevelMapCatalog.GetRuntimeLevelId(
                    targetSetLevelId,
                    targetSetLevelActiveMap)
                : AuthoritativeLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "target_level_id",
                    "target_level_symbol");
            var target = new AuthoritativeWarpTarget(
                AuthoritativeWarpIdentity.Resolve(row, stableIds),
                AuthoritativeLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "source_level_id",
                    "source_level_symbol"),
                row.RequiredInt32("trigger_x"),
                row.RequiredInt32("trigger_y"),
                targetLevelId,
                row.RequiredInt32("target_x"),
                row.RequiredInt32("target_y"),
                AuthoritativeQuestIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "required_quest_id",
                    "required_quest_symbol"),
                row.OptionalBoolean("required_quest_completed"),
                targetSetLevelId,
                targetSetLevelActiveMap) {
                StableSymbol = AuthoritativeWarpIdentity.ReadSymbol(row),
                TargetSetLevelSymbol = row.TryGet("target_set_level_symbol", out var setLevelSymbol)
                    ? setLevelSymbol.Trim()
                    : string.Empty,
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

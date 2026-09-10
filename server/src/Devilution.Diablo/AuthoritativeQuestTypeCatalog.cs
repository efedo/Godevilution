using Devilution.Data;

namespace Devilution.Diablo;

/** Native-compatible quest metadata supplied by validated content. */
public sealed record AuthoritativeQuestTypeDefinition(
    uint QuestId,
    uint NativeQuestId,
    string Name,
    int SinglePlayerLevel,
    int MultiplayerLevel,
    string LevelType,
    int SetLevel,
    bool SinglePlayerOnly)
{
    /** Optional stable symbol resolved from content_ids.tsv. */
    public string StableSymbol { get; init; } = string.Empty;

    /** Optional level-cell anchor that completes the quest when an item is dropped there. */
    public int? WorldItemAnchorPositionX { get; init; }

    public int? WorldItemAnchorPositionY { get; init; }

    public AuthoritativeQuestTypeDefinition Validate()
    {
        var hasX = WorldItemAnchorPositionX is not null;
        var hasY = WorldItemAnchorPositionY is not null;
        if (hasX != hasY
            || WorldItemAnchorPositionX is < 0 or > 512
            || WorldItemAnchorPositionY is < 0 or > 512) {
            throw new InvalidDataException(
                $"Quest type {QuestId} contains an incomplete or invalid world-item anchor.");
        }
        return this;
    }
}

/** Validated quest metadata used to admit external quest state rows. */
public sealed class AuthoritativeQuestTypeCatalog
{
    /** Highest native quest ordinal accepted by the compatibility protocol. */
    public const uint MaximumNativeQuestId = 23;
    private static readonly IReadOnlyList<string> ProductionColumns = [
        "quest_id",
        "quest_symbol",
        "native_quest_id",
        "name",
        "single_player_level",
        "multiplayer_level",
        "level_type",
        "set_level",
        "set_level_symbol",
        "single_player_only",
        "world_item_anchor_position_x",
        "world_item_anchor_position_y",
    ];

    private readonly Dictionary<uint, AuthoritativeQuestTypeDefinition> definitions;

    public AuthoritativeQuestTypeCatalog(
        IEnumerable<AuthoritativeQuestTypeDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        this.definitions = new Dictionary<uint, AuthoritativeQuestTypeDefinition>();
        var nativeQuestIds = new HashSet<uint>();
        foreach (var definition in definitions.Select(definition => definition.Validate())) {
            if (definition.QuestId == 0
                || definition.NativeQuestId > MaximumNativeQuestId
                || string.IsNullOrWhiteSpace(definition.Name)
                || definition.SinglePlayerLevel < -1
                || definition.MultiplayerLevel < -1
                || definition.SetLevel is < 0 or > 8
                || !nativeQuestIds.Add(definition.NativeQuestId)
                || !this.definitions.TryAdd(definition.QuestId, definition)) {
                throw new InvalidDataException("Quest type definitions contain invalid or duplicate identities.");
            }
        }
        if (this.definitions.Count == 0)
            throw new InvalidDataException("The quest type catalog cannot be empty.");
    }

    public IReadOnlyList<AuthoritativeQuestTypeDefinition> Definitions
        => definitions.Values.OrderBy(definition => definition.QuestId).ToArray();

    public bool TryGet(uint questId, out AuthoritativeQuestTypeDefinition definition)
        => definitions.TryGetValue(questId, out definition!);

    /** Rejects item-placement anchors that cannot exist on their authored quest level. */
    public void ValidateWorldItemAnchors(
        IEnumerable<AuthoritativeQuestState> quests,
        AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(quests);
        ArgumentNullException.ThrowIfNull(world);
        var anchors = new HashSet<(uint LevelId, int PositionX, int PositionY)>();
        foreach (var quest in quests) {
            if (!TryGet(quest.QuestId, out var definition)
                || definition.WorldItemAnchorPositionX is not { } positionX
                || definition.WorldItemAnchorPositionY is not { } positionY) {
                continue;
            }
            if (!world.ContainsPosition(quest.LevelId, positionX, positionY)
                || !world.IsWalkable(quest.LevelId, positionX, positionY)) {
                throw new InvalidDataException(
                    $"Quest type {quest.QuestId} world-item anchor ({positionX},{positionY}) "
                    + $"is outside or blocked on quest level {quest.LevelId}.");
            }
            if (!anchors.Add((quest.LevelId, positionX, positionY))) {
                throw new InvalidDataException(
                    $"Quest type {quest.QuestId} world-item anchor ({positionX},{positionY}) "
                    + $"duplicates another quest anchor on level {quest.LevelId}.");
            }
        }
    }

    /** Rejects partial quest-type metadata at the current production boundary. */
    public static void ValidateExternalizedProduction(TsvTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Columns.SequenceEqual(ProductionColumns, StringComparer.Ordinal))
            throw new InvalidDataException(
                $"Production quest-type table '{table.SourcePath}' must use the complete ordered schema.");
        foreach (var row in table.Rows) {
            _ = row.Required("quest_id");
            _ = row.Required("native_quest_id");
            _ = row.Required("name");
            _ = row.Required("single_player_level");
            _ = row.Required("multiplayer_level");
        }
    }

    public static AuthoritativeQuestTypeCatalog LoadTsv(
        string sourcePath,
        string contents,
        AuthoritativeSetLevelNameCatalog? setLevelNames = null,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        var catalog = new AuthoritativeQuestTypeCatalog(table.Rows.Select(row =>
            new AuthoritativeQuestTypeDefinition(
                AuthoritativeQuestIdentity.Resolve(row, stableIds),
                row.RequiredUInt32("native_quest_id"),
                row.Required("name"),
                row.RequiredInt32("single_player_level"),
                row.RequiredInt32("multiplayer_level"),
                OptionalText(row, "level_type"),
                AuthoritativeSetLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "set_level",
                    "set_level_symbol"),
                row.OptionalBoolean("single_player_only")) {
                StableSymbol = AuthoritativeQuestIdentity.ReadSymbol(row),
                WorldItemAnchorPositionX = OptionalCoordinate(row, "world_item_anchor_position_x"),
                WorldItemAnchorPositionY = OptionalCoordinate(row, "world_item_anchor_position_y"),
            }));
        if (setLevelNames is not null
            && catalog.Definitions.Any(definition =>
                !setLevelNames.TryGet(definition.SetLevel, out _))) {
            throw new InvalidDataException(
                $"Quest type table '{sourcePath}' references an unknown set-level identity.");
        }
        return catalog;
    }

    private static string OptionalText(TsvRow row, string column)
        => row.TryGet(column, out var value) ? value : string.Empty;

    private static int? OptionalCoordinate(TsvRow row, string column)
    {
        return row.TryGet(column, out var value)
            && !string.IsNullOrWhiteSpace(value)
            ? row.OptionalInt32(column)
            : null;
    }
}

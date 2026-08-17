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
}

/** Validated quest metadata used to admit external quest state rows. */
public sealed class AuthoritativeQuestTypeCatalog
{
    private readonly Dictionary<uint, AuthoritativeQuestTypeDefinition> definitions;

    public AuthoritativeQuestTypeCatalog(
        IEnumerable<AuthoritativeQuestTypeDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        this.definitions = new Dictionary<uint, AuthoritativeQuestTypeDefinition>();
        foreach (var definition in definitions) {
            if (definition.QuestId == 0
                || definition.NativeQuestId > 23
                || string.IsNullOrWhiteSpace(definition.Name)
                || definition.SinglePlayerLevel < -1
                || definition.MultiplayerLevel < -1
                || definition.SetLevel is < 0 or > 8
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
}

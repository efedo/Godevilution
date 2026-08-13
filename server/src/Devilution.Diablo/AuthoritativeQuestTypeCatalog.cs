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
    bool SinglePlayerOnly);

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
                || definition.SetLevel < -1
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
        string contents)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeQuestTypeCatalog(table.Rows.Select(row =>
            new AuthoritativeQuestTypeDefinition(
                row.RequiredUInt32("quest_id"),
                row.RequiredUInt32("native_quest_id"),
                row.Required("name"),
                row.RequiredInt32("single_player_level"),
                row.RequiredInt32("multiplayer_level"),
                OptionalText(row, "level_type"),
                row.OptionalInt32("set_level", -1),
                ParseFlag(row, "single_player_only"))));
    }

    private static bool ParseFlag(TsvRow row, string column)
    {
        var value = row.OptionalInt32(column);
        if (value is not (0 or 1))
            throw new InvalidDataException(
                $"Quest type row {row.LineNumber} has invalid {column} value {value}.");
        return value == 1;
    }

    private static string OptionalText(TsvRow row, string column)
        => row.TryGet(column, out var value) ? value : string.Empty;
}

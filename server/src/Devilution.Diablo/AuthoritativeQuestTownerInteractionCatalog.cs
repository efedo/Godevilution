using Devilution.Data;

namespace Devilution.Diablo;

/** Server-owned action applied by a content-declared quest/towner interaction. */
public enum AuthoritativeQuestTownerInteractionKind
{
    Activate,
    Advance,
    Complete,
    Dialogue,
}

/** One validated interaction stage for a quest and native townsperson. */
public sealed record AuthoritativeQuestTownerInteractionDefinition(
    uint InteractionId,
    uint QuestId,
    int TownerId,
    uint LevelId,
    int PositionX,
    int PositionY,
    uint ItemId,
    AuthoritativeQuestTownerInteractionKind Kind,
    int RequiredVariable1 = -1,
    int RequiredVariable2 = -1,
    int Variable1 = -1,
    int Variable2 = -1,
    int MessageId = 0,
    bool ConsumeItem = true,
    bool AllowCompleted = false);

/** Validated repeatable quest/towner interactions supplied by external content. */
public sealed class AuthoritativeQuestTownerInteractionCatalog
{
    private readonly IReadOnlyList<AuthoritativeQuestTownerInteractionDefinition> definitions = [];

    public AuthoritativeQuestTownerInteractionCatalog(
        IEnumerable<AuthoritativeQuestTownerInteractionDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions.ToArray();
        if (values.Any(definition =>
                definition.InteractionId == 0
                || definition.QuestId == 0
                || definition.TownerId is < 0 or > byte.MaxValue
                || definition.LevelId > ushort.MaxValue
                || definition.PositionX < 0
                || definition.PositionY < 0
                || !Enum.IsDefined(definition.Kind)
                || definition.RequiredVariable1 is < -1 or > byte.MaxValue
                || definition.RequiredVariable2 is < -1 or > byte.MaxValue
                || definition.Variable1 is < -1 or > byte.MaxValue
                || definition.Variable2 is < -1 or > byte.MaxValue
                || definition.MessageId is < 0 or > short.MaxValue)) {
            throw new InvalidDataException(
                "Quest/towner interactions contain invalid values.");
        }
        if (values.GroupBy(definition => definition.InteractionId)
                .Any(group => group.Count() != 1)) {
            throw new InvalidDataException(
                "Quest/towner interactions contain duplicate interaction IDs.");
        }
        foreach (var definition in values) {
            switch (definition.Kind) {
            case AuthoritativeQuestTownerInteractionKind.Activate:
                if (definition.ConsumeItem && definition.ItemId == 0
                    || !definition.ConsumeItem && definition.ItemId != 0
                    || definition.RequiredVariable1 >= 0
                    || definition.RequiredVariable2 >= 0
                    || definition.Variable1 < 0
                    || definition.MessageId == 0
                    || definition.AllowCompleted) {
                    throw new InvalidDataException(
                        $"Quest/towner activation {definition.InteractionId} is incomplete.");
                }
                break;
            case AuthoritativeQuestTownerInteractionKind.Advance:
                if (!definition.ConsumeItem
                    || definition.ItemId == 0
                    || (definition.RequiredVariable1 < 0 && definition.RequiredVariable2 < 0)
                    || (definition.Variable1 < 0 && definition.Variable2 < 0)
                    || definition.MessageId == 0
                    || definition.AllowCompleted) {
                    throw new InvalidDataException(
                        $"Quest/towner advancement {definition.InteractionId} is incomplete.");
                }
                break;
            case AuthoritativeQuestTownerInteractionKind.Complete:
                if (!definition.ConsumeItem
                    || definition.ItemId == 0
                    || (definition.RequiredVariable1 < 0 && definition.RequiredVariable2 < 0)
                    || definition.MessageId == 0
                    || definition.AllowCompleted) {
                    throw new InvalidDataException(
                        $"Quest/towner completion {definition.InteractionId} is incomplete.");
                }
                break;
            case AuthoritativeQuestTownerInteractionKind.Dialogue:
                if (definition.ConsumeItem && definition.ItemId == 0
                    || definition.MessageId == 0) {
                    throw new InvalidDataException(
                        $"Quest/towner dialogue {definition.InteractionId} requires a message.");
                }
                break;
            default:
                throw new InvalidDataException(
                    $"Quest/towner interaction {definition.InteractionId} has an unsupported kind.");
            }
        }
        this.definitions = values
            .OrderBy(definition => definition.InteractionId)
            .ToArray();
    }

    public IReadOnlyList<AuthoritativeQuestTownerInteractionDefinition> Definitions
        => definitions;

    public void ValidateItems(AuthoritativeItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var definition in definitions) {
            if (definition.ItemId == 0)
                continue;
            if (!items.TryGet(definition.ItemId, out _))
                throw new InvalidDataException(
                    $"Quest/towner interaction {definition.InteractionId} references unknown item {definition.ItemId}.");
        }
    }

    public void ValidateQuests(IEnumerable<AuthoritativeQuestState> quests)
    {
        ArgumentNullException.ThrowIfNull(quests);
        var questIds = quests.Select(quest => quest.QuestId).ToHashSet();
        foreach (var definition in definitions) {
            if (!questIds.Contains(definition.QuestId))
                throw new InvalidDataException(
                    $"Quest/towner interaction {definition.InteractionId} references unknown quest {definition.QuestId}.");
        }
    }

    public void ValidateWorld(AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (var definition in definitions) {
            if (!world.ContainsLevel(definition.LevelId)
                || !world.IsWalkable(
                    definition.LevelId,
                    definition.PositionX,
                    definition.PositionY)) {
                throw new InvalidDataException(
                    $"Quest/towner interaction {definition.InteractionId} contains an invalid position.");
            }
        }
    }

    public static AuthoritativeQuestTownerInteractionCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeQuestTownerInteractionCatalog(table.Rows.Select(row =>
            new AuthoritativeQuestTownerInteractionDefinition(
                row.RequiredUInt32("interaction_id"),
                AuthoritativeQuestIdentity.Resolve(
                    row,
                    stableIds,
                    "quest_id",
                    "quest_symbol"),
                row.RequiredInt32("towner_id"),
                AuthoritativeLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "level_id",
                    "level_symbol"),
                row.RequiredInt32("position_x"),
                row.RequiredInt32("position_y"),
                AuthoritativeItemIdentity.Resolve(
                    row,
                    stableIds,
                    "item_id",
                    "item_symbol"),
                ParseKind(row.Required("kind"), sourcePath, row.LineNumber),
                row.OptionalInt32("required_variable1", -1),
                row.OptionalInt32("required_variable2", -1),
                row.OptionalInt32("variable1", -1),
                row.OptionalInt32("variable2", -1),
                row.OptionalInt32("message_id"),
                row.OptionalBoolean("consume_item", true),
                row.OptionalBoolean("allow_completed", false))));
    }

    private static AuthoritativeQuestTownerInteractionKind ParseKind(
        string value,
        string sourcePath,
        int lineNumber)
    {
        if (Enum.TryParse<AuthoritativeQuestTownerInteractionKind>(
                value,
                ignoreCase: true,
                out var kind)
            && Enum.IsDefined(kind)) {
            return kind;
        }
        throw new InvalidDataException(
            $"Quest/towner interaction table '{sourcePath}' has an invalid kind '{value}' on line {lineNumber}.");
    }
}

using Devilution.Data;

namespace Devilution.Diablo;

/** Content-owned location and native message identity for a general towner greeting. */
public sealed record AuthoritativeTownerGreetingDefinition(
    int TownerId,
    uint LevelId,
    int PositionX,
    int PositionY,
    int MessageId);

/** Validated greeting metadata used before a connected vendor menu opens. */
public sealed class AuthoritativeTownerGreetingCatalog
{
    private readonly IReadOnlyDictionary<int, AuthoritativeTownerGreetingDefinition> definitions;

    public AuthoritativeTownerGreetingCatalog(
        IEnumerable<AuthoritativeTownerGreetingDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var values = definitions.ToArray();
        if (values.Any(definition =>
                definition.TownerId < 0
                || definition.TownerId > byte.MaxValue
                || definition.LevelId > ushort.MaxValue
                || definition.PositionX < 0
                || definition.PositionY < 0
                || definition.MessageId <= 0
                || definition.MessageId > short.MaxValue)) {
            throw new InvalidDataException("Towner greetings contain invalid values.");
        }
        if (values.GroupBy(definition => definition.TownerId)
                .Any(group => group.Count() != 1)) {
            throw new InvalidDataException("Towner greetings contain duplicate towner IDs.");
        }
        this.definitions = values.ToDictionary(definition => definition.TownerId);
    }

    public IReadOnlyCollection<AuthoritativeTownerGreetingDefinition> Definitions
        => definitions.Values.ToArray();

    public bool TryGet(int townerId, out AuthoritativeTownerGreetingDefinition definition)
        => definitions.TryGetValue(townerId, out definition!);

    public void ValidateWorld(AuthoritativeWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (var definition in definitions.Values) {
            if (!world.ContainsLevel(definition.LevelId)
                || !world.IsWalkable(
                    definition.LevelId,
                    definition.PositionX,
                    definition.PositionY)) {
                throw new InvalidDataException(
                    $"Towner {definition.TownerId} contains an invalid greeting position.");
            }
        }
    }

    public static AuthoritativeTownerGreetingCatalog LoadTsv(
        string sourcePath,
        string contents,
        StableContentIdCatalog? stableIds = null)
    {
        var table = TsvTable.Parse(sourcePath, contents);
        return new AuthoritativeTownerGreetingCatalog(table.Rows.Select(row =>
            new AuthoritativeTownerGreetingDefinition(
                row.RequiredInt32("towner_id"),
                AuthoritativeLevelIdentity.ResolveOptional(
                    row,
                    stableIds,
                    "level_id",
                    "level_symbol"),
                row.RequiredInt32("position_x"),
                row.RequiredInt32("position_y"),
                row.RequiredInt32("message_id"))));
    }
}

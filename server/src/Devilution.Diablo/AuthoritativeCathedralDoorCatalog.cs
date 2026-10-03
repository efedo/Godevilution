using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Devilution.Data;

namespace Devilution.Diablo;

/** Explicit content identities for the native-scanned Cathedral door types. */
public sealed record AuthoritativeCathedralDoorDefinition(
    uint LevelId, uint LeftObjectId, uint RightObjectId);

/** Opt-in mappings; the engine scans pieces, never a seed-specific object list. */
public sealed class AuthoritativeCathedralDoorCatalog
{
    public const string TableName = "native_cathedral_doors.tsv";
    public const string ProfileId = "original-diablo.cathedral-doors";
    public const int Revision = 1;
    private static readonly string[] Columns = [
        "level_id", "level_symbol", "left_object_id", "left_object_symbol",
        "right_object_id", "right_object_symbol",
    ];

    public AuthoritativeCathedralDoorCatalog(
        IEnumerable<AuthoritativeCathedralDoorDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var entries = definitions.OrderBy(entry => entry.LevelId).ToArray();
        if (entries.Length is < 1 or > 4
            || entries.Any(entry => entry.LevelId is < 1 or > 4
                || entry.LeftObjectId == 0 || entry.RightObjectId == 0
                || entry.LeftObjectId == entry.RightObjectId)
            || entries.Select(entry => entry.LevelId).Distinct().Count() != entries.Length)
            throw new InvalidDataException("Cathedral door mappings contain invalid or duplicate identities.");
        Definitions = Array.AsReadOnly(entries);
        var canonical = string.Join("\n", entries.Select(entry => string.Create(
            CultureInfo.InvariantCulture,
            $"{entry.LevelId}:{entry.LeftObjectId}:{entry.RightObjectId}")));
        Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public IReadOnlyList<AuthoritativeCathedralDoorDefinition> Definitions { get; }
    public string Fingerprint { get; }

    public static AuthoritativeCathedralDoorCatalog Load(
        string contents, StableContentIdCatalog stableIds)
    {
        ArgumentNullException.ThrowIfNull(stableIds);
        var table = TsvTable.Parse(TableName, contents);
        if (!table.Columns.SequenceEqual(Columns, StringComparer.Ordinal)
            || table.Rows.Count != 4)
            throw new InvalidDataException($"{TableName} requires its exact header and four ordered depth rows.");
        var entries = new List<AuthoritativeCathedralDoorDefinition>();
        foreach (var row in table.Rows) {
            uint Resolve(string category, string idColumn, string symbolColumn)
            {
                if (!uint.TryParse(row.Required(idColumn), NumberStyles.None,
                        CultureInfo.InvariantCulture, out var id)
                    || id == 0
                    || !stableIds.TryResolve(category, row.Required(symbolColumn), out var resolved)
                    || resolved != id)
                    throw new InvalidDataException($"{TableName} row {row.LineNumber} has an invalid {idColumn} identity.");
                return id;
            }
            var levelId = Resolve("level", "level_id", "level_symbol");
            if (levelId != entries.Count + 1)
                throw new InvalidDataException($"{TableName} must list depths one through four in order.");
            entries.Add(new AuthoritativeCathedralDoorDefinition(levelId,
                Resolve("object", "left_object_id", "left_object_symbol"),
                Resolve("object", "right_object_id", "right_object_symbol")));
        }
        return new AuthoritativeCathedralDoorCatalog(entries);
    }
}

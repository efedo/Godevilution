using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Devilution.Data;

/** A parsed row from an external, tab-delimited content table. */
public sealed record TsvRow(int LineNumber, IReadOnlyDictionary<string, string> Values)
{
    public string this[string column]
    {
        get
        {
            if (!Values.TryGetValue(column, out var value))
                throw new InvalidDataException($"Column '{column}' is not present on TSV row {LineNumber}.");
            return value;
        }
    }

    public bool TryGet(string column, out string value) => Values.TryGetValue(column, out value!);

    public string Required(string column)
    {
        var value = this[column];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"Column '{column}' is empty on TSV row {LineNumber}.");
        return value;
    }

    public int RequiredInt32(string column)
    {
        var value = Required(column);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            throw new InvalidDataException($"Column '{column}' on TSV row {LineNumber} must be a 32-bit integer.");
        return result;
    }

    public uint RequiredUInt32(string column)
    {
        var value = Required(column);
        if (!uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            throw new InvalidDataException(
                $"Column '{column}' on TSV row {LineNumber} must be an unsigned 32-bit integer.");
        return result;
    }

    public int OptionalInt32(string column, int defaultValue = 0)
    {
        if (!TryGet(column, out var value) || string.IsNullOrWhiteSpace(value))
            return defaultValue;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            throw new InvalidDataException($"Column '{column}' on TSV row {LineNumber} must be a 32-bit integer.");
        return result;
    }

    public uint OptionalUInt32(string column, uint defaultValue = 0)
    {
        if (!TryGet(column, out var value) || string.IsNullOrWhiteSpace(value))
            return defaultValue;
        if (!uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            throw new InvalidDataException(
                $"Column '{column}' on TSV row {LineNumber} must be an unsigned 32-bit integer.");
        return result;
    }

    public bool RequiredBoolean(string column)
        => ParseBoolean(Required(column), column);

    public bool OptionalBoolean(string column, bool defaultValue = false)
    {
        if (!TryGet(column, out var value) || string.IsNullOrWhiteSpace(value))
            return defaultValue;
        return ParseBoolean(value, column);
    }

    public bool? OptionalNullableBoolean(string column)
    {
        if (!TryGet(column, out var value) || string.IsNullOrWhiteSpace(value))
            return null;
        return ParseBoolean(value, column);
    }

    private bool ParseBoolean(string value, string column)
    {
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || value == "1")
            return true;
        if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
            || value == "0")
            return false;
        throw new InvalidDataException(
            $"Column '{column}' on TSV row {LineNumber} must be Boolean (true/false or 1/0).");
    }
}

/** A validated TSV table whose row and overlay ordering is preserved. */
public sealed record TsvTable(
    string SourcePath,
    IReadOnlyList<string> Columns,
    IReadOnlyList<TsvRow> Rows)
{
    /** Produces a canonical TSV representation for game-specific schema loaders. */
    public string ToCanonicalTsv()
    {
        var output = new StringBuilder();
        output.AppendJoin('\t', Columns).Append('\n');
        foreach (var row in Rows) {
            for (var index = 0; index < Columns.Count; index++) {
                if (index != 0)
                    output.Append('\t');
                output.Append(row[Columns[index]]);
            }
            output.Append('\n');
        }
        return output.ToString();
    }

    public static TsvTable Parse(string sourcePath, string contents)
    {
        sourcePath = ContentPath.NormalizeRelative(sourcePath, "table");
        ArgumentNullException.ThrowIfNull(contents);

        var lines = contents.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var columns = default(IReadOnlyList<string>);
        var rows = new List<TsvRow>();

        for (var index = 0; index < lines.Length; index++) {
            var line = lines[index];
            if (index == 0)
                line = line.TrimStart('\uFEFF');
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                continue;

            var cells = line.Split('\t', StringSplitOptions.None);
            if (columns is null) {
                var header = cells.Select(cell => cell.Trim()).ToArray();
                if (header.Length == 0 || header.Any(string.IsNullOrWhiteSpace))
                    throw new InvalidDataException(
                        $"TSV table '{sourcePath}' has an empty column name on line {index + 1}.");
                if (header.Distinct(StringComparer.Ordinal).Count() != header.Length)
                    throw new InvalidDataException($"TSV table '{sourcePath}' has duplicate column names.");
                columns = header;
                continue;
            }

            if (cells.Length != columns.Count)
                throw new InvalidDataException(
                    $"TSV table '{sourcePath}' has {cells.Length} cells but expected {columns.Count} on line {index + 1}.");

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var column = 0; column < columns.Count; column++)
                values.Add(columns[column], cells[column]);
            rows.Add(new TsvRow(index + 1, new ReadOnlyDictionary<string, string>(values)));
        }

        if (columns is null)
            throw new InvalidDataException($"TSV table '{sourcePath}' does not contain a header row.");
        return new TsvTable(sourcePath, columns, rows);
    }
}

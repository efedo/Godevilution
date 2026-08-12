using System.Collections.ObjectModel;

namespace Devilution.Data;

/** Immutable effective table set produced from one validated ordered manifest. */
public sealed class ComposedContent
{
    private readonly IReadOnlyDictionary<string, TsvTable> tablesByPath;

    private ComposedContent(ContentManifest manifest, IReadOnlyList<TsvTable> tables)
    {
        Manifest = manifest;
        Tables = tables;
        tablesByPath = new ReadOnlyDictionary<string, TsvTable>(
            tables.ToDictionary(table => table.SourcePath, StringComparer.Ordinal));
    }

    public ContentManifest Manifest { get; }

    public IReadOnlyList<TsvTable> Tables { get; }

    public bool TryGetTable(string tablePath, out TsvTable table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tablePath);
        return tablesByPath.TryGetValue(tablePath.Replace('\\', '/'), out table!);
    }

    public TsvTable GetRequiredTable(string tablePath)
    {
        return TryGetTable(tablePath, out var table)
            ? table
            : throw new InvalidDataException(
                $"Active content does not provide required table '{tablePath}'.");
    }

    public static ComposedContent Compose(ContentManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var tableOrder = new List<string>();
        var effectiveTables = new Dictionary<string, TsvTable>(StringComparer.Ordinal);

        foreach (var pack in manifest.Packs) {
            var overlays = pack.Overlays.ToDictionary(
                overlay => overlay.TablePath,
                StringComparer.Ordinal);
            foreach (var table in pack.Tables) {
                if (!effectiveTables.TryGetValue(table.SourcePath, out var existing)) {
                    if (overlays.ContainsKey(table.SourcePath)) {
                        throw CompositionError(
                            pack.Id,
                            table.SourcePath,
                            "declares an overlay but no earlier pack provides the target table");
                    }
                    effectiveTables.Add(table.SourcePath, table);
                    tableOrder.Add(table.SourcePath);
                    continue;
                }

                if (!overlays.TryGetValue(table.SourcePath, out var overlay)) {
                    throw CompositionError(
                        pack.Id,
                        table.SourcePath,
                        "conflicts with an earlier table but has no overlay declaration");
                }
                effectiveTables[table.SourcePath] = ApplyOverlay(
                    pack.Id,
                    existing,
                    table,
                    overlay);
            }
        }

        return new ComposedContent(
            manifest,
            tableOrder.Select(path => effectiveTables[path]).ToArray());
    }

    private static TsvTable ApplyOverlay(
        string packId,
        TsvTable existing,
        TsvTable overlayTable,
        ContentTableOverlay overlay)
    {
        if (!existing.Columns.SequenceEqual(overlayTable.Columns, StringComparer.Ordinal)) {
            throw CompositionError(
                packId,
                overlay.TablePath,
                "does not use the same ordered columns as the target table");
        }
        var missingKeys = overlay.KeyColumns
            .Where(column => !existing.Columns.Contains(column, StringComparer.Ordinal))
            .ToArray();
        if (missingKeys.Length != 0) {
            throw CompositionError(
                packId,
                overlay.TablePath,
                $"references missing key columns: {string.Join(", ", missingKeys)}");
        }
        if (overlay.Mode != ContentTableOverlayMode.Upsert) {
            throw CompositionError(
                packId,
                overlay.TablePath,
                $"uses unsupported overlay mode '{overlay.Mode}'");
        }

        var rows = existing.Rows.ToList();
        var rowByKey = BuildKeyIndex(packId, existing, overlay.KeyColumns, rows);
        var overlayKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in overlayTable.Rows) {
            var key = CreateRowKey(packId, overlay.TablePath, overlay.KeyColumns, row);
            if (!overlayKeys.Add(key)) {
                throw CompositionError(
                    packId,
                    overlay.TablePath,
                    $"contains duplicate overlay key {DescribeKey(overlay.KeyColumns, row)}");
            }
            if (rowByKey.TryGetValue(key, out var index))
                rows[index] = row;
            else {
                rowByKey.Add(key, rows.Count);
                rows.Add(row);
            }
        }
        return new TsvTable(existing.SourcePath, existing.Columns.ToArray(), rows.ToArray());
    }

    private static Dictionary<string, int> BuildKeyIndex(
        string packId,
        TsvTable table,
        IReadOnlyList<string> keyColumns,
        IReadOnlyList<TsvRow> rows)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < rows.Count; index++) {
            var row = rows[index];
            var key = CreateRowKey(packId, table.SourcePath, keyColumns, row);
            if (!result.TryAdd(key, index)) {
                throw CompositionError(
                    packId,
                    table.SourcePath,
                    $"target table contains duplicate key {DescribeKey(keyColumns, row)}");
            }
        }
        return result;
    }

    private static string CreateRowKey(
        string packId,
        string tablePath,
        IReadOnlyList<string> keyColumns,
        TsvRow row)
    {
        var values = new string[keyColumns.Count];
        for (var index = 0; index < keyColumns.Count; index++) {
            var column = keyColumns[index];
            if (!row.TryGet(column, out var value) || string.IsNullOrWhiteSpace(value)) {
                throw CompositionError(
                    packId,
                    tablePath,
                    $"has an empty key column '{column}' on TSV row {row.LineNumber}");
            }
            values[index] = $"{value.Length}:{value}";
        }
        return string.Join('|', values);
    }

    private static string DescribeKey(IReadOnlyList<string> keyColumns, TsvRow row)
    {
        return string.Join(
            ", ",
            keyColumns.Select(column => $"{column}='{row[column]}'"));
    }

    private static InvalidDataException CompositionError(
        string packId,
        string tablePath,
        string detail)
    {
        return new InvalidDataException(
            $"Content pack '{packId}', table '{tablePath}' {detail}.");
    }
}

public sealed record ContentDiagnostic(string Code, string Message);

public sealed record ContentReloadResult(
    bool Succeeded,
    ComposedContent Active,
    IReadOnlyList<ContentDiagnostic> Diagnostics);

/** Atomically replaces active content only after complete composition succeeds. */
public sealed class ContentRuntime
{
    private readonly object synchronization = new();
    private ComposedContent active;

    public ContentRuntime(ContentManifest initialManifest)
    {
        active = ComposedContent.Compose(initialManifest);
    }

    public ComposedContent Active
    {
        get {
            lock (synchronization)
                return active;
        }
    }

    public ContentReloadResult TryReload(ContentManifest candidateManifest)
    {
        ArgumentNullException.ThrowIfNull(candidateManifest);
        ComposedContent candidate;
        try {
            candidate = ComposedContent.Compose(candidateManifest);
        } catch (Exception exception)
            when (exception is ArgumentException or InvalidDataException) {
            return new ContentReloadResult(
                false,
                Active,
                [new ContentDiagnostic("content-composition-failed", exception.Message)]);
        }

        lock (synchronization)
            active = candidate;
        return new ContentReloadResult(true, candidate, []);
    }
}

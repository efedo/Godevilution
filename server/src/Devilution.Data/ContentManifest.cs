using Devilution.Domain.Hashing;

namespace Devilution.Data;

public enum ContentTableOverlayMode
{
    Upsert,
}

/** Declarative row-identity and merge policy for one overlaid table. */
public sealed record ContentTableOverlay
{
    public ContentTableOverlay(
        string tablePath,
        IReadOnlyList<string> keyColumns,
        ContentTableOverlayMode mode = ContentTableOverlayMode.Upsert)
    {
        if (string.IsNullOrWhiteSpace(tablePath))
            throw new ArgumentException("An overlay table path is required.", nameof(tablePath));
        ArgumentNullException.ThrowIfNull(keyColumns);
        var keys = keyColumns.ToArray();
        if (keys.Length == 0 || keys.Any(string.IsNullOrWhiteSpace)) {
            throw new ArgumentException(
                "An overlay requires at least one named key column.",
                nameof(keyColumns));
        }
        if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length) {
            throw new ArgumentException(
                "Overlay key columns must be unique.",
                nameof(keyColumns));
        }
        TablePath = ContentPath.NormalizeRelative(tablePath, "overlay table");
        KeyColumns = keys;
        Mode = mode;
    }

    public string TablePath { get; }

    public IReadOnlyList<string> KeyColumns { get; }

    public ContentTableOverlayMode Mode { get; }
}

/** One ordered external content pack and its validated tables. */
public sealed record ContentPack
{
    public ContentPack(string id, string version, IReadOnlyList<TsvTable> tables)
        : this(id, version, tables, [], [], null, [], [])
    {
    }

    public ContentPack(
        string id,
        string version,
        IReadOnlyList<TsvTable> tables,
        IReadOnlyList<string>? dependencies = null,
        IReadOnlyList<ContentTableOverlay>? overlays = null,
        ContentSchemaIdentity? schema = null,
        IReadOnlyList<ContentPackDependencyConstraint>? dependencyConstraints = null,
        IReadOnlyList<ContentAsset>? assets = null)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Content pack IDs and versions are required.");
        ArgumentNullException.ThrowIfNull(tables);
        var tableList = tables.ToArray();
        if (tableList.Any(table => table is null))
            throw new ArgumentException("Content pack tables cannot be null.", nameof(tables));
        if (tableList.Any(table => ContentPath.NormalizeRelative(table.SourcePath, "table") != table.SourcePath))
            throw new ArgumentException(
                "Content table source paths must use canonical relative paths.",
                nameof(tables));
        if (tableList.Select(table => table.SourcePath).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            != tableList.Length) {
            throw new ArgumentException(
                "Content table paths must be unique within a pack.",
                nameof(tables));
        }
        var assetList = assets?.ToArray() ?? [];
        if (assetList.Any(asset => asset is null)
            || assetList.Select(asset => asset.SourcePath).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != assetList.Length
            || assetList.Any(asset => tableList.Any(table =>
                string.Equals(table.SourcePath, asset.SourcePath, StringComparison.OrdinalIgnoreCase)))) {
            throw new ArgumentException(
                "Content asset paths must be non-null, unique, and distinct from table paths.",
                nameof(assets));
        }
        var dependencyIds = dependencies?.ToArray() ?? [];
        if (dependencyIds.Any(string.IsNullOrWhiteSpace)
            || dependencyIds.Distinct(StringComparer.Ordinal).Count() != dependencyIds.Length
            || dependencyIds.Contains(id, StringComparer.Ordinal)) {
            throw new ArgumentException(
                "Content pack dependencies must be named, unique, and cannot reference the pack itself.",
                nameof(dependencies));
        }
        var overlayList = overlays?.ToArray() ?? [];
        if (overlayList.Any(overlay => overlay is null)
            || overlayList.Select(overlay => overlay.TablePath)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() != overlayList.Length) {
            throw new ArgumentException(
                "Content table overlay paths must be non-null and unique.",
                nameof(overlays));
        }
        var tablePaths = tableList.Select(table => table.SourcePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (overlayList.Any(overlay => !tablePaths.Contains(overlay.TablePath))) {
            throw new ArgumentException(
                "Every overlay declaration must reference a table supplied by its pack.",
                nameof(overlays));
        }
        var constraints = dependencyConstraints?.ToArray() ?? [];
        if (constraints.Any(constraint => constraint is null)
            || constraints.Select(constraint => constraint.DependencyId)
                    .Distinct(StringComparer.Ordinal).Count() != constraints.Length) {
            throw new ArgumentException(
                "Content dependency constraints must be non-null and unique by dependency ID.",
                nameof(dependencyConstraints));
        }
        var unknownConstraints = constraints
            .Where(constraint => !dependencyIds.Contains(
                constraint.DependencyId,
                StringComparer.Ordinal))
            .Select(constraint => constraint.DependencyId)
            .ToArray();
        if (unknownConstraints.Length != 0) {
            throw new ArgumentException(
                $"Content dependency constraints must reference declared dependencies: {string.Join(", ", unknownConstraints)}.",
                nameof(dependencyConstraints));
        }
        Id = id;
        Version = version;
        Tables = tableList;
        Assets = assetList;
        Dependencies = dependencyIds;
        Overlays = overlayList;
        Schema = schema;
        DependencyConstraints = constraints;
    }

    public string Id { get; }

    public string Version { get; }

    public IReadOnlyList<TsvTable> Tables { get; }

    public IReadOnlyList<ContentAsset> Assets { get; }

    public IReadOnlyList<string> Dependencies { get; }

    public IReadOnlyList<ContentTableOverlay> Overlays { get; }

    public ContentSchemaIdentity? Schema { get; }

    public IReadOnlyList<ContentPackDependencyConstraint> DependencyConstraints { get; }
}

/** Deterministic identity for the ordered content set active on a server. */
public sealed class ContentManifest
{
    public ContentManifest(string id, string version, IEnumerable<ContentPack> packs)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A content manifest ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("A content manifest version is required.", nameof(version));
        ArgumentNullException.ThrowIfNull(packs);

        Id = id;
        Version = version;
        Packs = packs.ToArray();
        if (Packs.Count == 0)
            throw new ArgumentException("A content manifest must contain at least one pack.", nameof(packs));
        if (Packs.Any(pack => pack is null))
            throw new ArgumentException("Content packs cannot be null.", nameof(packs));
        if (Packs.Select(pack => pack.Id).Distinct(StringComparer.Ordinal).Count() != Packs.Count)
            throw new ArgumentException("Content pack IDs must be unique.", nameof(packs));
        var loadedPacks = new Dictionary<string, ContentPack>(StringComparer.Ordinal);
        foreach (var pack in Packs) {
            var unavailable = pack.Dependencies
                .Where(dependency => !loadedPacks.ContainsKey(dependency))
                .ToArray();
            if (unavailable.Length != 0) {
                throw new ArgumentException(
                    $"Content pack '{pack.Id}' has unavailable or out-of-order dependencies: {string.Join(", ", unavailable)}.",
                    nameof(packs));
            }
            foreach (var constraint in pack.DependencyConstraints) {
                var dependency = loadedPacks[constraint.DependencyId];
                if (!ContentSemanticVersion.TryParse(dependency.Version, out var dependencyVersion)) {
                    throw new ArgumentException(
                        $"Content pack '{pack.Id}' constrains '{dependency.Id}', but version "
                        + $"'{dependency.Version}' is not a three-component semantic version.",
                        nameof(packs));
                }
                if (!constraint.Supports(dependencyVersion)) {
                    throw new ArgumentException(
                        $"Content pack '{pack.Id}' requires '{dependency.Id}' version "
                        + $"{constraint.Minimum} through {constraint.Maximum}, but {dependencyVersion} is loaded.",
                        nameof(packs));
                }
            }
            loadedPacks.Add(pack.Id, pack);
        }
        var schemas = Packs
            .Select(pack => pack.Schema)
            .Where(schema => schema is not null)
            .Distinct()
            .ToArray();
        if (schemas.Length > 1) {
            throw new ArgumentException(
                $"Content packs declare incompatible schemas: {string.Join(", ", schemas.AsEnumerable())}.",
                nameof(packs));
        }
        Schema = schemas.SingleOrDefault();
        Sha256 = ComputeHash();
    }

    public string Id { get; }

    public string Version { get; }

    public IReadOnlyList<ContentPack> Packs { get; }

    public ContentSchemaIdentity? Schema { get; }

    public string Sha256 { get; }

    private string ComputeHash()
    {
        var hasher = new CanonicalStateHasher();
        hasher.AppendString(Id);
        hasher.AppendString(Version);
        hasher.AppendUInt64((ulong)Packs.Count);
        foreach (var pack in Packs) {
            hasher.AppendString(pack.Id);
            hasher.AppendString(pack.Version);
            hasher.AppendUInt64((ulong)pack.Tables.Count);
            foreach (var table in pack.Tables) {
                hasher.AppendString(table.SourcePath);
                hasher.AppendUInt64((ulong)table.Columns.Count);
                foreach (var column in table.Columns)
                    hasher.AppendString(column);
                hasher.AppendUInt64((ulong)table.Rows.Count);
                foreach (var row in table.Rows) {
                    hasher.AppendUInt64((ulong)row.LineNumber);
                    foreach (var column in table.Columns)
                        hasher.AppendString(row[column]);
                }
            }
            if (pack.Dependencies.Count != 0 || pack.Overlays.Count != 0) {
                hasher.AppendString("godevilution-content-pack-metadata-v1");
                hasher.AppendUInt64((ulong)pack.Dependencies.Count);
                foreach (var dependency in pack.Dependencies)
                    hasher.AppendString(dependency);
                hasher.AppendUInt64((ulong)pack.Overlays.Count);
                foreach (var overlay in pack.Overlays) {
                    hasher.AppendString(overlay.TablePath);
                    hasher.AppendUInt64((ulong)overlay.Mode);
                    hasher.AppendUInt64((ulong)overlay.KeyColumns.Count);
                    foreach (var keyColumn in overlay.KeyColumns)
                        hasher.AppendString(keyColumn);
                }
            }
            if (pack.Schema is not null || pack.DependencyConstraints.Count != 0) {
                hasher.AppendString("godevilution-content-pack-compatibility-v1");
                hasher.AppendBool(pack.Schema is not null);
                if (pack.Schema is not null) {
                    hasher.AppendString(pack.Schema.Id);
                    hasher.AppendString(pack.Schema.Version.ToString());
                }
                hasher.AppendUInt64((ulong)pack.DependencyConstraints.Count);
                foreach (var constraint in pack.DependencyConstraints) {
                    hasher.AppendString(constraint.DependencyId);
                    hasher.AppendString(constraint.Minimum.ToString());
                    hasher.AppendString(constraint.Maximum.ToString());
                }
            }
            if (pack.Assets.Count != 0) {
                hasher.AppendString("godevilution-content-pack-assets-v1");
                hasher.AppendUInt64((ulong)pack.Assets.Count);
                foreach (var asset in pack.Assets.OrderBy(
                    asset => asset.SourcePath,
                    StringComparer.Ordinal)) {
                    hasher.AppendString(asset.SourcePath);
                    hasher.AppendUInt64((ulong)asset.Contents.Length);
                    foreach (var value in asset.Contents.Span)
                        hasher.AppendUInt8(value);
                }
            }
        }
        return hasher.HexDigest();
    }
}

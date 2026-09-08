using System.Text;
using System.Text.Json;

namespace Devilution.Data;

/** Loads an ordered directory of UTF-8 TSV and native binary assets into a content pack. */
public static class ContentPackLoader
{
    public const string ManifestFileName = "content-pack.json";
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static ContentPack LoadDirectory(string id, string version, string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        var root = Path.GetFullPath(rootDirectory);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Content pack directory '{rootDirectory}' does not exist.");

        RejectReparsePoints(root, rootDirectory);

        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => new {
                FullPath = path,
                RelativePath = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'),
            })
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();
        var tables = files
            .Where(file => string.Equals(Path.GetExtension(file.RelativePath), ".tsv", StringComparison.OrdinalIgnoreCase))
            .Select(file => TsvTable.Parse(file.RelativePath, ReadUtf8Text(file.FullPath, "table")))
            .ToArray();
        var assets = files
            .Where(file => IsNativeBinaryAsset(file.RelativePath))
            .Select(file => new ContentAsset(file.RelativePath, File.ReadAllBytes(file.FullPath)))
            .ToArray();
        if (tables.Length == 0 && assets.Length == 0)
            throw new InvalidDataException(
                $"Content pack directory '{rootDirectory}' contains no supported tables or native assets.");
        return new ContentPack(id, version, tables, [], [], assets: assets);
    }

    /** Loads pack identity, compatibility, dependencies, and overlays from JSON. */
    public static ContentPack LoadManifestDirectory(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        var root = Path.GetFullPath(rootDirectory);
        var manifestPath = Path.Combine(root, ManifestFileName);
        if (!File.Exists(manifestPath)) {
            throw new FileNotFoundException(
                $"Content pack directory '{rootDirectory}' does not contain {ManifestFileName}.",
                manifestPath);
        }

        ContentPackDocument? document;
        try {
            document = JsonSerializer.Deserialize<ContentPackDocument>(
                ReadUtf8Text(manifestPath, "manifest"),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        } catch (JsonException exception) {
            throw new InvalidDataException(
                $"Content pack manifest '{manifestPath}' is not valid JSON.",
                exception);
        }
        if (document is null
            || string.IsNullOrWhiteSpace(document.Id)
            || string.IsNullOrWhiteSpace(document.Version)) {
            throw new InvalidDataException(
                $"Content pack manifest '{manifestPath}' requires id and version.");
        }
        if (document.FormatVersion is not null and not 1) {
            throw new InvalidDataException(
                $"Content pack manifest '{manifestPath}' uses unsupported formatVersion {document.FormatVersion}.");
        }

        var loaded = LoadDirectory(document.Id, document.Version, root);
        var overlays = (document.Overlays ?? []).Select(overlay => {
            if (overlay is null || string.IsNullOrWhiteSpace(overlay.Table)) {
                throw new InvalidDataException(
                    $"Content pack manifest '{manifestPath}' contains an unnamed overlay.");
            }
            if (!string.IsNullOrWhiteSpace(overlay.Mode)
                && !string.Equals(overlay.Mode, "upsert", StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException(
                    $"Content pack manifest '{manifestPath}' uses unsupported overlay mode '{overlay.Mode}'.");
            }
            return new ContentTableOverlay(
                overlay.Table,
                overlay.Keys ?? [],
                ContentTableOverlayMode.Upsert);
        }).ToArray();
        ContentSchemaIdentity? schema = null;
        if (document.Schema is not null) {
            if (string.IsNullOrWhiteSpace(document.Schema.Id)
                || string.IsNullOrWhiteSpace(document.Schema.Version)) {
                throw new InvalidDataException(
                    $"Content pack manifest '{manifestPath}' has an incomplete schema identity.");
            }
            try {
                schema = new ContentSchemaIdentity(
                    document.Schema.Id,
                    document.Schema.Version);
            } catch (ArgumentException exception) {
                throw new InvalidDataException(
                    $"Content pack manifest '{manifestPath}' has an invalid schema identity.",
                    exception);
            }
        }
        var constraints = (document.DependencyConstraints ?? []).Select(constraint => {
            if (constraint is null
                || string.IsNullOrWhiteSpace(constraint.Id)
                || string.IsNullOrWhiteSpace(constraint.MinimumVersion)
                || string.IsNullOrWhiteSpace(constraint.MaximumVersion)) {
                throw new InvalidDataException(
                    $"Content pack manifest '{manifestPath}' has an incomplete dependency constraint.");
            }
            try {
                return new ContentPackDependencyConstraint(
                    constraint.Id,
                    constraint.MinimumVersion,
                    constraint.MaximumVersion);
            } catch (ArgumentException exception) {
                throw new InvalidDataException(
                    $"Content pack manifest '{manifestPath}' has an invalid constraint for '{constraint.Id}'.",
                    exception);
            }
        }).ToArray();
        return new ContentPack(
            loaded.Id,
            loaded.Version,
            loaded.Tables,
            document.Dependencies,
            overlays,
            schema,
            constraints,
            loaded.Assets);
    }

    private sealed record ContentPackDocument(
        int? FormatVersion,
        string? Id,
        string? Version,
        IReadOnlyList<string>? Dependencies,
        IReadOnlyList<ContentOverlayDocument?>? Overlays,
        ContentSchemaDocument? Schema,
        IReadOnlyList<ContentDependencyConstraintDocument?>? DependencyConstraints);

    private static bool IsNativeBinaryAsset(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".dun", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".til", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".sol", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadUtf8Text(string path, string resourceName)
    {
        try {
            return StrictUtf8.GetString(File.ReadAllBytes(path));
        } catch (DecoderFallbackException exception) {
            throw new InvalidDataException(
                $"Content pack {resourceName} '{path}' is not valid UTF-8.",
                exception);
        }
    }

    private static void RejectReparsePoints(string directory, string displayPath)
    {
        var directoryAttributes = File.GetAttributes(directory);
        if ((directoryAttributes & FileAttributes.ReparsePoint) != 0) {
            throw new InvalidDataException(
                $"Content pack directory '{displayPath}' is a reparse-point entry.");
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(directory)) {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0) {
                throw new InvalidDataException(
                    $"Content pack directory '{displayPath}' contains a reparse-point entry '{entry}'.");
            }

            if ((attributes & FileAttributes.Directory) != 0)
                RejectReparsePoints(entry, displayPath);
        }
    }

    private sealed record ContentOverlayDocument(
        string? Table,
        IReadOnlyList<string>? Keys,
        string? Mode);

    private sealed record ContentSchemaDocument(string? Id, string? Version);

    private sealed record ContentDependencyConstraintDocument(
        string? Id,
        string? MinimumVersion,
        string? MaximumVersion);
}

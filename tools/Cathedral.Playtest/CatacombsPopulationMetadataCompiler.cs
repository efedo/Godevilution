using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Devilution.Data;
using Devilution.Diablo;

namespace Godevilution.Tools;

/** Receipt for an offline Catacombs population metadata pack. */
public sealed record CatacombsPopulationMetadataCompilation(
    string OutputDirectory,
    string PackId,
    string PackVersion,
    string MetadataFingerprint,
    string ManifestHash,
    string FileTreeHash,
    int TableCount);

/**
 * Compiles a detached Catacombs metadata directory into a deterministic pack.
 * The compiler reads only the explicitly supplied flat TSV directory.
 */
public static class CatacombsPopulationMetadataCompiler
{
    public const string PackId = AuthoritativeCatacombsPopulationMetadata.CatalogId;
    public const string PackVersion = AuthoritativeCatacombsPopulationMetadata.SchemaVersion;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static CatacombsPopulationMetadataCompilation CompileDirectory(
        string inputDirectory, string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        var input = Path.GetFullPath(inputDirectory);
        var output = Path.GetFullPath(outputDirectory);
        if (Overlaps(input, output))
            throw new InvalidDataException("Catacombs metadata input and output paths must not overlap.");
        RejectReparseAncestors(input);
        RequireFiles(input, includeManifest: false);
        var source = ContentPackLoader.LoadDirectory(PackId, PackVersion, input);
        return Compile(source.Tables, output);
    }

    public static CatacombsPopulationMetadataCompilation Compile(
        IEnumerable<TsvTable> tables, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        var metadata = AuthoritativeCatacombsPopulationMetadata.Load(new ContentPack(
            PackId, PackVersion, tables.ToArray(),
            schema: new ContentSchemaIdentity(
                AuthoritativeCatacombsPopulationMetadata.SchemaId,
                AuthoritativeCatacombsPopulationMetadata.SchemaVersion)));
        var output = Path.GetFullPath(outputDirectory);
        RejectReparseAncestors(output);
        if (Path.Exists(output))
            throw new IOException($"Catacombs metadata output must be a new directory: {output}");
        var staging = output + ".catacombs-metadata-staging-" + Guid.NewGuid().ToString("N");
        RejectReparseAncestors(staging);
        if (Path.Exists(staging))
            throw new IOException("The owned Catacombs metadata staging directory already exists.");
        Directory.CreateDirectory(staging);
        try {
            var pack = metadata.ToContentPack();
            foreach (var table in pack.Tables)
                File.WriteAllText(Path.Combine(staging, table.SourcePath),
                    ToCrLf(table.ToCanonicalTsv()), Utf8);
            var manifest = new {
                formatVersion = 1,
                id = pack.Id,
                version = pack.Version,
                schema = new { id = pack.Schema!.Id, version = pack.Schema.Version.ToString() },
            };
            File.WriteAllText(Path.Combine(staging, ContentPackLoader.ManifestFileName),
                ToCrLf(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + "\n"),
                Utf8);
            var reloaded = LoadDirectory(staging);
            if (reloaded.Fingerprint != metadata.Fingerprint || reloaded.ManifestHash != metadata.ManifestHash)
                throw new InvalidDataException("Written Catacombs metadata changed during reload.");
            var treeHash = ComputeFileTreeHash(staging);
            RejectReparseAncestors(output);
            RejectReparseAncestors(staging);
            if (Path.Exists(output))
                throw new IOException("Catacombs metadata publication target appeared during compilation.");
            Directory.Move(staging, output);
            return new(output, reloaded.PackId, reloaded.PackVersion,
                reloaded.Fingerprint, reloaded.ManifestHash, treeHash, pack.Tables.Count);
        } catch (Exception failure) {
            try {
                if (Directory.Exists(staging)) {
                    RejectReparseAncestors(staging);
                    var allowed = AuthoritativeCatacombsPopulationMetadata.TableNames
                        .Append(ContentPackLoader.ManifestFileName).ToHashSet(StringComparer.Ordinal);
                    foreach (var entry in Directory.EnumerateFileSystemEntries(staging))
                        if ((File.GetAttributes(entry) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0
                            || !allowed.Contains(Path.GetFileName(entry)))
                            throw new InvalidDataException("Catacombs metadata cleanup encountered an unexpected entry.");
                    Directory.Delete(staging, recursive: true);
                }
            } catch (Exception cleanup) {
                failure.Data["RetainedCatacombsMetadataStagingDirectory"] = staging;
                failure.Data["CatacombsMetadataCleanupError"] = cleanup.Message;
            }
            throw;
        }
    }

    public static AuthoritativeCatacombsPopulationMetadata LoadDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var root = Path.GetFullPath(directory);
        RejectReparseAncestors(root);
        RequireFiles(root, includeManifest: true);
        ValidateManifest(Path.Combine(root, ContentPackLoader.ManifestFileName));
        return AuthoritativeCatacombsPopulationMetadata.Load(
            ContentPackLoader.LoadManifestDirectory(root));
    }

    private static void RequireFiles(string root, bool includeManifest)
    {
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException(root);
        var expected = AuthoritativeCatacombsPopulationMetadata.TableNames
            .Concat(includeManifest ? [ContentPackLoader.ManifestFileName] : Array.Empty<string>())
            .Order(StringComparer.Ordinal).ToArray();
        var entries = Directory.EnumerateFileSystemEntries(root).Order(StringComparer.Ordinal).ToArray();
        foreach (var entry in entries)
            if ((File.GetAttributes(entry) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                throw new InvalidDataException("Catacombs metadata must contain only regular flat-schema files.");
        if (!entries.Select(Path.GetFileName).SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidDataException("Catacombs metadata directory has missing, unknown, or noncanonical files.");
    }

    private static void ValidateManifest(string path)
    {
        JsonDocument document;
        try {
            document = JsonDocument.Parse(Utf8.GetString(File.ReadAllBytes(path)));
        } catch (Exception exception) when (exception is JsonException or DecoderFallbackException) {
            throw new InvalidDataException("Catacombs metadata manifest must be valid UTF-8 JSON.", exception);
        }
        using var ownedDocument = document;
        var root = document.RootElement;
        RequireProperties(root, ["formatVersion", "id", "version", "schema"]);
        var schema = root.GetProperty("schema");
        RequireProperties(schema, ["id", "version"]);
        if (root.GetProperty("formatVersion").GetRawText() != "1"
            || root.GetProperty("id").ValueKind != JsonValueKind.String
            || root.GetProperty("id").GetString() != PackId
            || root.GetProperty("version").ValueKind != JsonValueKind.String
            || root.GetProperty("version").GetString() != PackVersion
            || schema.GetProperty("id").ValueKind != JsonValueKind.String
            || schema.GetProperty("id").GetString() != AuthoritativeCatacombsPopulationMetadata.SchemaId
            || schema.GetProperty("version").ValueKind != JsonValueKind.String
            || schema.GetProperty("version").GetString() != AuthoritativeCatacombsPopulationMetadata.SchemaVersion)
            throw new InvalidDataException("Catacombs metadata manifest has an unsupported schema or scalar type.");
    }

    private static void RequireProperties(JsonElement value, string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object
            || !value.EnumerateObject().Select(property => property.Name)
                .Order(StringComparer.Ordinal).SequenceEqual(names.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidDataException("Catacombs metadata manifest has missing, duplicate, or unknown properties.");
    }

    private static bool Overlaps(string first, string second)
        => first.Equals(second, StringComparison.OrdinalIgnoreCase)
            || first.StartsWith(second.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || second.StartsWith(first.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);

    private static void RejectReparseAncestors(string path)
    {
        for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Catacombs metadata paths must not traverse reparse points.");
    }

    private static string ComputeFileTreeHash(string root)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in Directory.EnumerateFiles(root).Order(StringComparer.Ordinal)) {
            var name = Path.GetFileName(path);
            var fileHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            hash.AppendData(Utf8.GetBytes($"{name.Length}:{name}:{fileHash}\n"));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string ToCrLf(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", "\r\n", StringComparison.Ordinal);
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Devilution.Data;
using Devilution.Diablo;

namespace Godevilution.Tools;

/** Receipt for a complete metadata pack; deliberately not a playable profile. */
public sealed record CathedralPopulationMetadataCompilation(
    string OutputDirectory, string PackId, string PackVersion,
    string MetadataFingerprint, string ManifestHash, string FileTreeHash, int TableCount);

/**
 * Explicit offline TSV compiler. No installation discovery, MPQs, native outputs,
 * seeds or gameplay activation. Fresh publication revalidates the written pack.
 */
public static class CathedralPopulationMetadataCompiler
{
    public const string PackId = "godevilution.cathedral-population-metadata";
    public const string PackVersion = "1.0.0";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static CathedralPopulationMetadataCompilation CompileDirectory(string inputDirectory, string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        var input = Path.GetFullPath(inputDirectory);
        var output = Path.GetFullPath(outputDirectory);
        if (Overlaps(input, output))
            throw new InvalidDataException("Metadata inputs and publication paths must not overlap.");
        RejectReparseAncestors(input);
        RequireFiles(input, includeManifest: false);
        // The flat exact file set is established before the generic loader reads it.
        var source = ContentPackLoader.LoadDirectory(PackId, PackVersion, input);
        return Compile(source.Tables, output);
    }

    public static CathedralPopulationMetadataCompilation Compile(IEnumerable<TsvTable> tables, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        var metadata = AuthoritativeCathedralPopulationMetadata.Load(new ContentPack(PackId, PackVersion,
            tables.ToArray(), schema: new ContentSchemaIdentity(
                AuthoritativeCathedralPopulationMetadata.SchemaId, AuthoritativeCathedralPopulationMetadata.SchemaVersion)));
        var output = Path.GetFullPath(outputDirectory);
        RejectReparseAncestors(output);
        if (Path.Exists(output))
            throw new IOException($"Population metadata output must be a new directory: {output}");
        var staging = output + ".metadata-staging-" + Guid.NewGuid().ToString("N");
        RejectReparseAncestors(staging);
        if (Path.Exists(staging))
            throw new IOException("The owned metadata staging directory already exists.");
        Directory.CreateDirectory(staging);
        try {
            var pack = metadata.ToContentPack();
            foreach (var table in pack.Tables)
                File.WriteAllText(Path.Combine(staging, table.SourcePath), ToCrLf(table.ToCanonicalTsv()), Utf8);
            var manifest = new {
                formatVersion = 1, id = pack.Id, version = pack.Version,
                schema = new { id = pack.Schema!.Id, version = pack.Schema.Version.ToString() },
            };
            File.WriteAllText(Path.Combine(staging, ContentPackLoader.ManifestFileName),
                ToCrLf(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + "\n"), Utf8);
            var reloaded = LoadDirectory(staging);
            if (reloaded.Fingerprint != metadata.Fingerprint || reloaded.ManifestHash != metadata.ManifestHash)
                throw new InvalidDataException("Written population metadata changed during reload.");
            var treeHash = ComputeFileTreeHash(staging);
            RejectReparseAncestors(output);
            RejectReparseAncestors(staging);
            if (Path.Exists(output))
                throw new IOException("Population metadata publication target appeared during compilation.");
            Directory.Move(staging, output);
            return new(output, reloaded.PackId, reloaded.PackVersion, reloaded.Fingerprint,
                reloaded.ManifestHash, treeHash, pack.Tables.Count);
        } catch (Exception failure) {
            // Only the exact GUID-owned, flat staging tree may be removed. Retain
            // uncertain trees and preserve the original compilation error.
            try {
                if (Directory.Exists(staging)) {
                    RejectReparseAncestors(staging);
                    var allowed = AuthoritativeCathedralPopulationMetadata.TableNames
                        .Append(ContentPackLoader.ManifestFileName).ToHashSet(StringComparer.Ordinal);
                    foreach (var entry in Directory.EnumerateFileSystemEntries(staging))
                        if ((File.GetAttributes(entry) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0
                            || !allowed.Contains(Path.GetFileName(entry)))
                            throw new InvalidDataException("Metadata staging cleanup encountered an unexpected entry.");
                    Directory.Delete(staging, recursive: true);
                }
            } catch (Exception cleanup) {
                failure.Data["RetainedMetadataStagingDirectory"] = staging;
                failure.Data["MetadataCleanupError"] = cleanup.Message;
            }
            throw;
        }
    }

    public static AuthoritativeCathedralPopulationMetadata LoadDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var root = Path.GetFullPath(directory);
        RejectReparseAncestors(root);
        RequireFiles(root, includeManifest: true);
        ValidateManifest(Path.Combine(root, ContentPackLoader.ManifestFileName));
        return AuthoritativeCathedralPopulationMetadata.Load(ContentPackLoader.LoadManifestDirectory(root));
    }

    private static void RequireFiles(string root, bool includeManifest)
    {
        var expected = AuthoritativeCathedralPopulationMetadata.TableNames
            .Concat(includeManifest ? [ContentPackLoader.ManifestFileName] : Array.Empty<string>())
            .Order(StringComparer.Ordinal).ToArray();
        var entries = Directory.EnumerateFileSystemEntries(root).Order(StringComparer.Ordinal).ToArray();
        foreach (var entry in entries)
            if ((File.GetAttributes(entry) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                throw new InvalidDataException("Population metadata must contain only regular flat-schema files.");
        if (!entries.Select(Path.GetFileName).SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidDataException("Population metadata directory has missing, unknown or noncanonical files.");
    }

    private static void ValidateManifest(string path)
    {
        JsonDocument document;
        try {
            document = JsonDocument.Parse(Utf8.GetString(File.ReadAllBytes(path)));
        } catch (Exception exception) when (exception is JsonException or DecoderFallbackException) {
            throw new InvalidDataException("Population metadata manifest must be valid UTF-8 JSON.", exception);
        }
        using var ownedDocument = document;
        var root = document.RootElement;
        RequireProperties(root, ["formatVersion", "id", "version", "schema"]);
        var schema = root.GetProperty("schema");
        RequireProperties(schema, ["id", "version"]);
        if (root.GetProperty("formatVersion").GetRawText() != "1"
            || root.GetProperty("id").ValueKind != JsonValueKind.String
            || root.GetProperty("version").ValueKind != JsonValueKind.String
            || schema.GetProperty("id").ValueKind != JsonValueKind.String
            || schema.GetProperty("id").GetString() != AuthoritativeCathedralPopulationMetadata.SchemaId
            || schema.GetProperty("version").ValueKind != JsonValueKind.String
            || schema.GetProperty("version").GetString() != AuthoritativeCathedralPopulationMetadata.SchemaVersion)
            throw new InvalidDataException("Population metadata manifest has an unsupported schema or scalar type.");
    }

    private static void RequireProperties(JsonElement value, string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.EnumerateObject().Select(property => property.Name)
                .Order(StringComparer.Ordinal).SequenceEqual(names.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidDataException("Population metadata manifest has missing, duplicate or unknown properties.");
    }

    private static bool Overlaps(string first, string second)
        => first.Equals(second, StringComparison.OrdinalIgnoreCase)
            || first.StartsWith(second.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || second.StartsWith(first.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static void RejectReparseAncestors(string path)
    {
        for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Population metadata paths must not traverse reparse points.");
    }

    private static string ComputeFileTreeHash(string root)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in Directory.EnumerateFiles(root).Order(StringComparer.Ordinal)) {
            var name = Path.GetFileName(path);
            hash.AppendData(Utf8.GetBytes($"{name.Length}:{name}:{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}\n"));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string ToCrLf(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace("\n", "\r\n", StringComparison.Ordinal);
}

using System.Buffers.Binary;
using System.Security.Cryptography;
using Devilution.Client.PresentationAssets;
using Devilution.Protocol.V1;
using Godot;
using FileAccess = Godot.FileAccess;

namespace Devilution.Client;

internal sealed record NativeTownerPackObservation(string? CatalogPath, bool IsLoaded, string? PackId,
    string? ContentManifestHash, string? TownCatalogFingerprint, bool IdentityMatched, bool AtlasesReady,
    string? Diagnostic, IReadOnlyList<string> LoadedAtlasPaths, IReadOnlyList<string> MissingAtlasPaths);

/** Optional resident art activates as a complete family after content, roster and atlas validation. */
internal sealed class NativeTownerAssets
{
    public const string ManifestEnvironmentVariable = "GODEVILUTION_NATIVE_TOWNER_PACK";
    private readonly string? manifestPath;
    private readonly NativeTownerCatalog? catalog;
    private readonly Dictionary<int, Texture2D> textures = [];
    private readonly List<string> loadedPaths = [];
    private readonly List<string> missingPaths = [];
    private string? contentHash;
    private string? fingerprint;
    private string? diagnostic;
    private bool identityMatched;
    private bool preflightAttempted;
    private bool atlasesReady;

    public NativeTownerAssets()
    {
        var configured = System.Environment.GetEnvironmentVariable(ManifestEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configured)) {
            diagnostic = "Native Town resident art is disabled; using resident markers.";
            return;
        }
        if (!PresentationAssetPath.TryNormalizeResourcePath(configured.Trim(), out var path, out var error)) {
            diagnostic = $"Native Town resident catalog path refused: {error}.";
            return;
        }
        manifestPath = path;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file is null || file.GetLength() > NativeTownerCatalog.MaximumDocumentCharacters) {
            diagnostic = "Native Town resident catalog is missing or exceeds its bounded size.";
            return;
        }
        if (!NativeTownerCatalog.TryParse(file.GetAsText(), out catalog, out var errors))
            diagnostic = string.Join(" ", errors);
    }

    public NativeTownerCatalog? Catalog => catalog;

    public bool Bind(string? admittedHash, string townFingerprint, IEnumerable<NativeTownResidentSnapshot> rows)
    {
        contentHash = admittedHash;
        fingerprint = string.IsNullOrEmpty(townFingerprint) ? null : townFingerprint;
        var residents = rows.ToArray();
        identityMatched = residents.Length == 11 && residents.All(row => row.LevelId == 0)
            && catalog is not null && admittedHash is not null && fingerprint is not null
            && catalog.SupportsBinding(admittedHash, fingerprint)
            && catalog.ResidentsMatches(residents.Select(row => (row.ResidentSlot, row.TownerId,
                row.PositionX, row.PositionY, row.NativeDirection)).ToArray());
        if (!identityMatched) {
            if (catalog is not null)
                diagnostic = "Native Town resident art does not match the admitted content, Town fingerprint and complete authoritative roster.";
            return false;
        }
        if (!preflightAttempted) {
            preflightAttempted = true;
            atlasesReady = Preflight();
        }
        if (atlasesReady)
            diagnostic = null;
        return atlasesReady;
    }

    public bool TryGetTexture(int atlasId, out Texture2D texture)
        => textures.TryGetValue(atlasId, out texture!);

    public NativeTownerPackObservation ObservePack()
        => new(manifestPath, catalog is not null, catalog?.PackId, contentHash, fingerprint,
            identityMatched, atlasesReady, diagnostic, loadedPaths.ToArray(), missingPaths.ToArray());

    private bool Preflight()
    {
        foreach (var atlas in catalog!.Atlases) {
            if (!PresentationAssetPath.TryCombineResourcePath(manifestPath!, atlas.Path, out var path, out var error)) {
                diagnostic = $"Native Town resident atlas path refused: {error}.";
                textures.Clear();
                return false;
            }
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file is null || file.GetLength() is <= 0 or > NativeTownerCatalog.MaximumAtlasFileBytes) {
                diagnostic = $"Native Town resident atlas '{path}' is missing or exceeds its bounded size.";
                missingPaths.Add(path);
                textures.Clear();
                return false;
            }
            var bytes = file.GetBuffer(checked((long)file.GetLength())).ToArray();
            if (Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != atlas.Sha256
                || bytes.Length < 33 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
                || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)) != 13
                || !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8)
                || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)) != atlas.Width
                || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4)) != atlas.Height) {
                diagnostic = $"Native Town resident atlas '{path}' hash or bounded PNG dimensions differ from its declaration.";
                missingPaths.Add(path);
                textures.Clear();
                return false;
            }
            using var image = new Image();
            if (image.LoadPngFromBuffer(bytes) != Error.Ok || image.GetWidth() != atlas.Width || image.GetHeight() != atlas.Height) {
                diagnostic = $"Native Town resident atlas '{path}' could not be decoded.";
                missingPaths.Add(path);
                textures.Clear();
                return false;
            }
            textures.Add(atlas.Id, ImageTexture.CreateFromImage(image));
            loadedPaths.Add(path);
        }
        return true;
    }
}

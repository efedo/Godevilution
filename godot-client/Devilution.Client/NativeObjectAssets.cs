using System.Buffers.Binary;
using System.Security.Cryptography;
using Devilution.Client.PresentationAssets;
using Godot;
using Environment = System.Environment;
using FileAccess = Godot.FileAccess;

namespace Devilution.Client;

internal sealed record NativeObjectPackObservation(
    string? CatalogPath,
    bool IsLoaded,
    string? PackId,
    string? ContentManifestHash,
    uint LevelId,
    bool IdentityMatched,
    bool AtlasesReady,
    string? Diagnostic,
    IReadOnlyList<string> LoadedAtlasPaths,
    IReadOnlyList<string> MissingAtlasPaths);

/**
 * Loads the optional Cathedral object family and, since batch 2.3, its Catacombs sibling catalog. Each family
 * activates only for its exact admitted content and level binding.
 */
internal sealed class NativeObjectAssets
{
    public const string ManifestEnvironmentVariable = "GODEVILUTION_NATIVE_OBJECT_PACK";
    /** The Catacombs catalog sits beside the configured Cathedral catalog; packs before 2.3 lack it. */
    public const string CatacombsCatalogFileName = "native-object-catalog-catacombs.json";

    private readonly List<Family> families = [];
    private readonly string? catalogPath;
    private readonly string? loadDiagnostic;
    private Family? active;
    private string? contentManifestHash;
    private uint levelId;
    private string? diagnostic;

    public NativeObjectAssets()
    {
        var configuredPath = Environment.GetEnvironmentVariable(ManifestEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configuredPath)) {
            loadDiagnostic = "Native object art is disabled; using the existing object presentation.";
            return;
        }
        if (!PresentationAssetPath.TryNormalizeResourcePath(configuredPath.Trim(), out var normalizedPath, out var pathError)) {
            loadDiagnostic = $"Native object pack refused: {pathError}.";
            return;
        }
        catalogPath = normalizedPath;
        if (!FileAccess.FileExists(normalizedPath)) {
            loadDiagnostic = "Native object catalog was not found; using the existing object presentation.";
            return;
        }
        if (!TryLoadCatalog(normalizedPath, out var cathedral, out loadDiagnostic))
            return;
        families.Add(new Family(normalizedPath, cathedral!));
        if (PresentationAssetPath.TryCombineResourcePath(normalizedPath, CatacombsCatalogFileName, out var siblingPath, out _)
            && FileAccess.FileExists(siblingPath)) {
            if (TryLoadCatalog(siblingPath, out var catacombs, out var siblingDiagnostic)
                && catacombs!.Location == NativeObjectCatalog.CatacombsLocation)
                families.Add(new Family(siblingPath, catacombs));
            else
                loadDiagnostic = siblingDiagnostic ?? "Native Catacombs object catalog does not describe the Catacombs family.";
        }
    }

    /** The bound family's catalog, or the Cathedral catalog while no family is bound. */
    public NativeObjectCatalog? Catalog => (active ?? families.FirstOrDefault())?.Catalog;

    public bool Bind(string? admittedContentHash, uint currentLevelId)
    {
        contentManifestHash = admittedContentHash;
        levelId = currentLevelId;
        active = families.FirstOrDefault(family => family.Catalog.SupportsBinding(admittedContentHash, currentLevelId));
        if (active is null) {
            diagnostic = families.Count == 0
                ? loadDiagnostic
                : admittedContentHash is null
                    ? "Native object art is inactive because no admitted content identity was supplied."
                    : $"Native object art is not bound to admitted content '{admittedContentHash}' at level {currentLevelId}.";
            return false;
        }

        active.Preflight();
        diagnostic = active.AtlasesReady ? null : active.PreflightDiagnostic;
        return active.AtlasesReady;
    }

    public bool TryGetTexture(int atlasId, out Texture2D texture)
    {
        if (active is { AtlasesReady: true } family && family.Textures.TryGetValue(atlasId, out texture!))
            return true;
        texture = null!;
        return false;
    }

    public NativeObjectPackObservation ObservePack()
    {
        var family = active ?? families.FirstOrDefault();
        return new(family?.CatalogPath ?? catalogPath, family is not null, family?.Catalog.PackId, contentManifestHash, levelId,
            active is not null, active?.AtlasesReady == true, diagnostic ?? loadDiagnostic,
            family?.LoadedAtlasPaths.ToArray() ?? [], family?.MissingAtlasPaths.ToArray() ?? []);
    }

    private static bool TryLoadCatalog(string path, out NativeObjectCatalog? catalog, out string? failure)
    {
        catalog = null;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file is null) {
            failure = "Native object catalog could not be opened; using the existing object presentation.";
            return false;
        }
        if (file.GetLength() > NativeObjectCatalog.MaximumDocumentCharacters) {
            failure = $"Native object catalog exceeds the {NativeObjectCatalog.MaximumDocumentCharacters} character size limit.";
            return false;
        }
        if (!NativeObjectCatalog.TryParse(file.GetAsText(), out catalog, out var errors)) {
            failure = string.Join(" ", errors);
            return false;
        }
        failure = null;
        return true;
    }

    /** One loaded catalog with its lazily preflighted atlas textures. */
    private sealed class Family(string catalogPath, NativeObjectCatalog catalog)
    {
        public string CatalogPath { get; } = catalogPath;
        public NativeObjectCatalog Catalog { get; } = catalog;
        public Dictionary<int, Texture2D> Textures { get; } = [];
        public List<string> LoadedAtlasPaths { get; } = [];
        public List<string> MissingAtlasPaths { get; } = [];
        public bool AtlasesReady { get; private set; }
        public string? PreflightDiagnostic { get; private set; }
        private bool attempted;

        public void Preflight()
        {
            if (attempted)
                return;
            attempted = true;
            AtlasesReady = PreflightAtlases();
        }

        private bool PreflightAtlases()
        {
            if (Catalog.Atlases.Count != Catalog.Sprites.Count) {
                PreflightDiagnostic = "Native object pack must provide one atlas per source sprite.";
                return false;
            }

            var pendingTextures = new Dictionary<int, Texture2D>();
            foreach (var atlas in Catalog.Atlases) {
                if (!PresentationAssetPath.TryCombineResourcePath(CatalogPath, atlas.Path, out var path, out var pathError)) {
                    PreflightDiagnostic = $"Native object atlas path refused: {pathError}.";
                    return false;
                }

                using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
                if (file is null || file.GetLength() is <= 0 or > NativeObjectCatalog.MaximumAtlasFileBytes) {
                    PreflightDiagnostic = $"Native object atlas '{path}' is missing or exceeds its bounded file size.";
                    MissingAtlasPaths.Add(path);
                    return false;
                }

                var bytes = file.GetBuffer(checked((long)file.GetLength())).ToArray();
                if (!HasExpectedPng(bytes, atlas)) {
                    PreflightDiagnostic = $"Native object atlas '{path}' hash or bounded PNG dimensions differ from its declaration.";
                    MissingAtlasPaths.Add(path);
                    return false;
                }

                using var image = new Image();
                if (image.LoadPngFromBuffer(bytes) != Error.Ok
                    || image.GetWidth() != atlas.Width
                    || image.GetHeight() != atlas.Height) {
                    PreflightDiagnostic = $"Native object atlas '{path}' could not be decoded at its declared dimensions.";
                    MissingAtlasPaths.Add(path);
                    return false;
                }

                try {
                    var texture = ImageTexture.CreateFromImage(image);
                    if (texture is null) {
                        PreflightDiagnostic = $"Native object atlas '{path}' could not be uploaded.";
                        MissingAtlasPaths.Add(path);
                        return false;
                    }
                    pendingTextures.Add(atlas.Id, texture);
                } catch (Exception exception) {
                    PreflightDiagnostic = $"Native object atlas '{path}' could not be uploaded: {exception.Message}";
                    MissingAtlasPaths.Add(path);
                    return false;
                }
                LoadedAtlasPaths.Add(path);
            }

            foreach (var entry in pendingTextures)
                Textures.Add(entry.Key, entry.Value);
            return true;
        }
    }

    private static bool HasExpectedPng(byte[] bytes, NativeObjectAtlas atlas)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() == atlas.Sha256
            && bytes.Length >= 33
            && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)) == 13
            && bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8)
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)) == atlas.Width
            && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4)) == atlas.Height;
}

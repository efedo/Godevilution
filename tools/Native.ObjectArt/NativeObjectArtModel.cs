namespace Devilution.Tools.NativeObjectArt;

public sealed record NativeObjectProfileBinding(string ContentManifestHash, uint LevelId, string Location);

public sealed record NativeObjectSourceHash(string Path, string Sha256);

public sealed record NativeObjectAtlas(int Id, string Path, int Width, int Height, string Sha256);

public sealed record NativeObjectFrameRegion(int NativeFrame, int AtlasId, int X, int Y, int Width, int Height);

public sealed record NativeObjectSpriteMetadata(
    int SpriteId,
    string SourcePath,
    int NativeWidth,
    int SourceFrameCount,
    IReadOnlyList<NativeObjectFrameRegion> Frames);

public sealed record NativeObjectDefinitionMetadata(
    uint NativeObjectId,
    int SpriteId,
    int AnimDelay,
    int AnimLen,
    int AnimWidth,
    bool Animated);

public sealed record NativeObjectArtCatalog(
    int SchemaVersion,
    string PackId,
    IReadOnlyList<NativeObjectProfileBinding> ProfileBindings,
    IReadOnlyList<NativeObjectSourceHash> SourceHashes,
    IReadOnlyList<NativeObjectAtlas> Atlases,
    IReadOnlyList<NativeObjectSpriteMetadata> Sprites,
    IReadOnlyList<NativeObjectDefinitionMetadata> Objects);

/** SourceRoot is an extracted tree; with Archive set it is ignored and files come from the MPQ. */
public sealed record NativeObjectImportOptions(
    string SourceRoot,
    string TextDataRoot,
    IReadOnlyList<string> ProfileDirectories,
    string OutputRoot,
    Godevilution.Tools.Archive.NativeArchiveSource? Archive = null);

public sealed record NativeObjectImportResult(string OutputDirectory, string PackId, int SpriteCount, int ObjectCount);

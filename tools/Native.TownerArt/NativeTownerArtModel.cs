namespace Devilution.Tools.NativeTownerArt;

public sealed record NativeTownerProfileBinding(string ContentManifestHash, string TownCatalogFingerprint, uint LevelId, string Location);

public sealed record NativeTownerSourceHash(string Path, string Sha256);

public sealed record NativeTownerAtlas(int Id, string Path, int Width, int Height, string Sha256);

public sealed record NativeTownerFrameRegion(int Group, int Frame, int AtlasId, int X, int Y, int Width, int Height);

public sealed record NativeTownerSpriteMetadata(
    int SpriteId,
    int AlignmentWidth,
    int DirectionGroupCount,
    int SourceFramesPerDirection,
    int AnimationFrames,
    int TicksPerFrame,
    IReadOnlyList<int> AnimationOrder,
    IReadOnlyList<NativeTownerFrameRegion> Frames);

public sealed record NativeTownerResidentMetadata(
    int ResidentSlot,
    int TownerId,
    int PositionX,
    int PositionY,
    int NativeDirection,
    int SpriteId);

public sealed record NativeTownerSpriteSourceMetadata(
    int SpriteId,
    string SourcePath,
    int AlignmentWidth,
    int AnimationFrames,
    int TicksPerFrame,
    IReadOnlyList<int> AnimationOrder);

public sealed record NativeTownerArtCatalog(
    int SchemaVersion,
    string PackId,
    IReadOnlyList<NativeTownerProfileBinding> ProfileBindings,
    IReadOnlyList<NativeTownerSourceHash> SourceHashes,
    IReadOnlyList<NativeTownerAtlas> Atlases,
    IReadOnlyList<NativeTownerSpriteMetadata> Sprites,
    IReadOnlyList<NativeTownerResidentMetadata> Residents);

public sealed record NativeTownerAtlasPixels(byte[] Png, int Width, int Height);

public sealed record NativeTownerImportOptions(
    string SourceRoot,
    string TextDataRoot,
    IReadOnlyList<string> ProfileDirectories,
    string OutputRoot);

public sealed record NativeTownerImportResult(string OutputDirectory, string PackId, int SpriteCount, int ResidentCount);

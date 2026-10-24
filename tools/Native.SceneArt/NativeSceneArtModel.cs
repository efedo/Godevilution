using System.Security.Cryptography;

namespace Devilution.Tools.NativeSceneArt;

/** The six native d-piece pixel encodings stored in MIN words. */
public enum NativeSceneTileType : byte
{
    Square = 0,
    TransparentSquare = 1,
    LeftTriangle = 2,
    RightTriangle = 3,
    LeftTrapezoid = 4,
    RightTrapezoid = 5,
}

/** A row-major, top-down indexed raster. Opacity is separate so palette index zero can be opaque. */
public sealed record NativeSceneRaster(int Width, int Height, byte[] Indices, byte[] Opacity)
{
    public void Validate(string description)
    {
        if (Width is < 1 or > NativeSceneArtDecoder.MaximumRasterDimension
            || Height is < 1 or > NativeSceneArtDecoder.MaximumRasterDimension
            || checked((long)Width * Height) > NativeSceneArtDecoder.MaximumRasterPixels
            || Indices.Length != checked(Width * Height)
            || Opacity.Length != Indices.Length
            || Opacity.Any(value => value is not 0 and not byte.MaxValue))
            throw new InvalidDataException($"{description} is outside the native scene raster bounds.");
    }

    public static NativeSceneRaster Clear(int width, int height)
        => new(width, height, new byte[checked(width * height)], new byte[checked(width * height)]);
}

/** Metadata for one referenced native CEL frame after SetDungeonMicros compacts the source frame table. */
public sealed record NativeSceneFrame(
    ushort SourceFrameId,
    ushort AdjustedFrameId,
    uint FirstUsePieceId,
    byte FirstUseSlotIndex,
    NativeSceneTileType SourceType,
    NativeSceneTileType StoredType,
    bool IsFloorTransform,
    NativeSceneRaster StoredRaster,
    NativeSceneRaster? FoliageRaster);

/** One rendered micro-slot. Source and remapped words stay distinct from effective rendering roles. */
public sealed record NativeSceneSlot(
    byte SlotIndex,
    ushort SourceRawWord,
    ushort AdjustedRawWord,
    ushort SourceFrameId,
    ushort AdjustedFrameId,
    NativeSceneTileType SourceType,
    NativeSceneTileType FirstUseFrameType,
    NativeSceneTileType DrawType,
    bool IsFloorBase,
    bool DrawFoliage,
    NativeSceneRaster? FloorRaster,
    NativeSceneRaster? ContentRaster);

public sealed record NativeScenePiece(
    uint PieceId,
    byte Properties,
    bool IsFloorPiece,
    IReadOnlyList<NativeSceneSlot> Slots);

public sealed record NativeSceneMegaTile(uint MegaTileId, IReadOnlyList<ushort> PieceIds);

/** A decoded Town or Cathedral source family before palette baking or catalog publication. */
public sealed record NativeSceneDecodedFamily(
    string Location,
    byte MicroTileLength,
    IReadOnlyList<NativeScenePiece> Pieces,
    IReadOnlyList<NativeSceneMegaTile> MegaTiles,
    IReadOnlyList<NativeSceneFrame> Frames,
    IReadOnlyList<NativeSceneRaster> Specials,
    byte[] PaletteRgb);

public sealed record NativeSceneSourceHash(string Path, string Sha256)
{
    public static NativeSceneSourceHash FromBytes(string path, ReadOnlySpan<byte> bytes)
        => new(path.Replace('\\', '/'), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
}

public sealed record NativeSceneArtLevelBinding(string ContentManifestHash, uint LevelId, string Location);

public sealed record NativeSceneRasterReference(int AtlasId, int X, int Y, int Width, int Height);

/** SourceRoot is an extracted tree; with Archive set it is ignored and files come from the MPQ. */
public sealed record NativeSceneArtImportOptions(
    string SourceRoot,
    string OutputRoot,
    string TownPaletteRelativePath,
    string CathedralPaletteRelativePath,
    IReadOnlyList<string> ProfileDirectories,
    Godevilution.Tools.Archive.NativeArchiveSource? Archive = null);

public sealed record NativeSceneArtImportResult(string OutputDirectory, string PackId, int PieceCount, int FrameCount, int SpecialCount);

public sealed record NativeSceneArtCatalog(
    int SchemaVersion,
    string PackId,
    IReadOnlyList<string> ContentManifestHashes,
    IReadOnlyList<NativeSceneArtLevelBinding> LevelBindings,
    IReadOnlyList<NativeSceneArtFamilyCatalog> Families);

public sealed record NativeSceneArtFamilyCatalog(
    string Location,
    byte MicroTileLength,
    int PieceCount,
    IReadOnlyList<NativeSceneSourceHash> SourceHashes,
    IReadOnlyList<NativeSceneArtAtlas> Atlases,
    IReadOnlyList<NativeSceneMegaTile> MegaTiles,
    IReadOnlyList<NativeSceneArtFrame> Frames,
    IReadOnlyList<NativeSceneArtDPiece> DPieces,
    IReadOnlyList<NativeSceneArtSlot> MinSlots,
    IReadOnlyList<NativeSceneArtSpecial> Specials,
    string DrawPhase);

public sealed record NativeSceneArtAtlas(
    int Id,
    string Kind,
    string Path,
    int Width,
    int Height,
    int CellWidth,
    int CellHeight,
    int CellCount,
    string Sha256);

public sealed record NativeSceneArtFrame(
    ushort SourceFrameId,
    ushort AdjustedFrameId,
    uint FirstUsePieceId,
    byte FirstUseSlotIndex,
    string SourceType,
    string StoredType,
    bool IsFloorTransform,
    NativeSceneRasterReference StoredRaster,
    NativeSceneRasterReference? FoliageRaster);

public sealed record NativeSceneArtDPiece(
    uint PieceId,
    byte SolFlags,
    string FloorClass);

public sealed record NativeSceneArtSlot(
    uint PieceId,
    byte Slot,
    ushort SourceWord,
    ushort RemappedWord,
    ushort SourceFrameId,
    ushort AdjustedFrameId,
    string SourceType,
    string FirstUseType,
    string EffectiveType,
    string Role,
    NativeSceneRasterReference? FloorRaster,
    NativeSceneRasterReference? Raster);

public sealed record NativeSceneArtSpecial(
    uint SpecialId,
    NativeSceneRasterReference Raster,
    string Anchor,
    int AnchorX,
    int AnchorY,
    string DrawPhase,
    int SourceTileOffsetX,
    int SourceTileOffsetY);

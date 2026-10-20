using System.Buffers.Binary;

namespace Devilution.Diablo;

/** The header and mega-tile channel consumed by Town; native Town ignores other DUN layers. */
public sealed class AuthoritativeTownTileMap
{
    public const int MaximumSourceBytes = 1024 * 1024;
    public const int MaximumMegaDimension = AuthoritativeTownGeometry.FineWidth / 2;

    private AuthoritativeTownTileMap(string sourcePath, int width, int height, ushort[] tiles)
    {
        SourcePath = sourcePath;
        MegaWidth = width;
        MegaHeight = height;
        TileLayer = Array.AsReadOnly(tiles);
    }

    public string SourcePath { get; }
    public int MegaWidth { get; }
    public int MegaHeight { get; }
    public int TileWidth => MegaWidth * 2;
    public int TileHeight => MegaHeight * 2;
    public IReadOnlyList<ushort> TileLayer { get; }

    /** Accepts the native sector4s partial tail without weakening the general DUN loader. */
    public static AuthoritativeTownTileMap Load(string sourcePath, ReadOnlyMemory<byte> contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var data = contents.Span;
        if (data.Length < 4 || data.Length > MaximumSourceBytes || data.Length % 2 != 0)
            throw new InvalidDataException($"Town DUN '{sourcePath}' has an invalid byte length.");
        var width = BinaryPrimitives.ReadUInt16LittleEndian(data);
        var height = BinaryPrimitives.ReadUInt16LittleEndian(data[2..]);
        if (width is < 1 or > MaximumMegaDimension || height is < 1 or > MaximumMegaDimension)
            throw new InvalidDataException($"Town DUN '{sourcePath}' has out-of-range dimensions.");
        var count = checked(width * height);
        if (data.Length < checked(4 + count * 2))
            throw new InvalidDataException($"Town DUN '{sourcePath}' has a truncated mega-tile channel.");
        var tiles = new ushort[count];
        for (var cell = 0; cell < count; cell++)
            tiles[cell] = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(4 + cell * 2, 2));
        return new AuthoritativeTownTileMap(sourcePath, width, height, tiles);
    }

    /** Existing fully validated DUN callers can supply the same consumed channel. */
    public static implicit operator AuthoritativeTownTileMap(AuthoritativeDunMapAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return new AuthoritativeTownTileMap(asset.SourcePath, asset.MegaWidth, asset.MegaHeight, asset.TileLayer.ToArray());
    }
}

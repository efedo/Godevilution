using System.Buffers.Binary;
using System.IO.Compression;

namespace Devilution.Tools.NativeSceneArt;

/** Writes deterministic RGBA PNG atlases using only the .NET base class library. */
internal static class NativeSceneArtPng
{
    public const int MaximumAtlasDimension = 8192;
    public const long MaximumAtlasPixels = 4L * 1024 * 1024;
    public const int MaximumPngBytes = 128 * 1024 * 1024;

    public static byte[] EncodeAtlas(
        IReadOnlyList<NativeSceneRaster> cells,
        int columns,
        int cellWidth,
        int cellHeight,
        int paletteOffset,
        ReadOnlySpan<byte> paletteRgb,
        bool bottomAlignCells = false)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Count is < 1 or > 4096 || columns is < 1 or > 256
            || columns > cells.Count || cellWidth is < 1 or > MaximumAtlasDimension
            || cellHeight is < 1 or > MaximumAtlasDimension || paletteOffset < 0
            || paletteRgb.Length != NativeSceneArtDecoder.PaletteByteCount)
            throw new InvalidDataException("Scene atlas parameters are outside the supported bounds.");
        var rows = checked((cells.Count + columns - 1) / columns);
        var width = checked(columns * cellWidth);
        var height = checked(rows * cellHeight);
        if (width > MaximumAtlasDimension || height > MaximumAtlasDimension
            || checked((long)width * height) > MaximumAtlasPixels)
            throw new InvalidDataException("Generated scene atlas exceeds its dimension or pixel limit.");

        var stride = checked(width * 4 + 1);
        var rawLength = checked(stride * height);
        if (rawLength > MaximumPngBytes)
            throw new InvalidDataException("Generated scene PNG exceeds its raw image data limit.");
        var rgba = new byte[rawLength];
        for (var y = 0; y < height; y++)
            rgba[y * stride] = 0;

        for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++) {
            var cell = cells[cellIndex];
            cell.Validate($"scene atlas cell {cellIndex}");
            if (cell.Width > cellWidth || cell.Height > cellHeight)
                throw new InvalidDataException($"Scene atlas cell {cellIndex} exceeds its storage cell.");
            var cellX = cellIndex % columns * cellWidth;
            var cellY = cellIndex / columns * cellHeight + (bottomAlignCells ? cellHeight - cell.Height : 0);
            for (var sourceY = 0; sourceY < cell.Height; sourceY++) {
                var targetRow = checked((cellY + sourceY) * stride + 1 + cellX * 4);
                for (var sourceX = 0; sourceX < cell.Width; sourceX++) {
                    var sourcePixel = sourceY * cell.Width + sourceX;
                    if (cell.Opacity[sourcePixel] == 0)
                        continue;
                    var colorIndex = checked((paletteOffset + cell.Indices[sourcePixel]) * 3);
                    var targetPixel = targetRow + sourceX * 4;
                    rgba[targetPixel] = paletteRgb[colorIndex];
                    rgba[targetPixel + 1] = paletteRgb[colorIndex + 1];
                    rgba[targetPixel + 2] = paletteRgb[colorIndex + 2];
                    rgba[targetPixel + 3] = byte.MaxValue;
                }
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(rgba);
        if (compressed.Length > MaximumPngBytes)
            throw new InvalidDataException("Generated scene PNG exceeds its compressed output limit.");

        using var png = new MemoryStream(checked((int)compressed.Length + 64));
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, checked((uint)width));
        BinaryPrimitives.WriteUInt32BigEndian(header[4..], checked((uint)height));
        header[8] = 8;
        header[9] = 6;
        header[10] = 0;
        header[11] = 0;
        header[12] = 0;
        WriteChunk(png, "IHDR"u8, header);
        WriteChunk(png, "IDAT"u8, compressed.GetBuffer().AsSpan(0, checked((int)compressed.Length)));
        WriteChunk(png, "IEND"u8, ReadOnlySpan<byte>.Empty);
        if (png.Length > MaximumPngBytes)
            throw new InvalidDataException("Generated scene PNG exceeds its output size limit.");
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)data.Length));
        stream.Write(length);
        stream.Write(type);
        stream.Write(data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, ComputeCrc(type, data));
        stream.Write(crcBytes);
    }

    private static uint ComputeCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in type)
            crc = UpdateCrc(crc, value);
        foreach (var value in data)
            crc = UpdateCrc(crc, value);
        return ~crc;
    }

    private static uint UpdateCrc(uint crc, byte value)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++)
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320U : crc >> 1;
        return crc;
    }
}

using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using Devilution.Tools.NativePlayerArt;

namespace Devilution.Tools.NativeTownerArt;

/** Writes Town palette-indexed CEL frames to deterministic RGBA atlases, bottom-aligned in each cell. */
public static class NativeTownerArtPng
{
    public const int MaximumAtlasDimension = 8192;
    public const long MaximumAtlasPixels = 4L * 1024 * 1024;
    public const long MaximumAggregateAtlasPixels = 16L * 1024 * 1024;
    public const int MaximumPngBytes = 128 * 1024 * 1024;

    public static NativeTownerAtlasPixels EncodeAtlas(NativeTownerCelSheet sheet, NativePlayerPalette palette)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(palette);
        palette.Validate();
        if (sheet.Directions.Count is < 1 or > NativeTownerCelDecoder.MaximumGroupCount)
            throw new InvalidDataException("Native towner atlas must contain one or eight direction groups.");
        var frameCount = sheet.Directions[0].Count;
        if (frameCount is < 1 or > NativeTownerCelDecoder.MaximumFramesPerGroup
            || sheet.Directions.Any(group => group.Count != frameCount))
            throw new InvalidDataException("Native towner atlas direction groups must have one common nonempty frame count.");
        var cellWidth = sheet.Directions.SelectMany(group => group).Max(frame => frame.Width);
        var cellHeight = sheet.Directions.SelectMany(group => group).Max(frame => frame.Height);
        foreach (var frame in sheet.Directions.SelectMany(group => group))
            frame.Validate("Native towner atlas frame");
        var atlasWidth = checked(cellWidth * frameCount);
        var atlasHeight = checked(cellHeight * sheet.Directions.Count);
        var atlasPixels = checked((long)atlasWidth * atlasHeight);
        if (atlasWidth > MaximumAtlasDimension || atlasHeight > MaximumAtlasDimension || atlasPixels > MaximumAtlasPixels)
            throw new InvalidDataException("Generated native towner atlas exceeds its dimension or pixel limit.");

        var scanlineBytes = checked(atlasWidth * 4 + 1);
        var rawLength = checked(scanlineBytes * atlasHeight);
        if (rawLength > MaximumPngBytes)
            throw new InvalidDataException("Generated native towner atlas exceeds its raw PNG limit.");
        var rgba = new byte[rawLength];
        for (var y = 0; y < atlasHeight; y++)
            rgba[y * scanlineBytes] = 0;

        for (var group = 0; group < sheet.Directions.Count; group++) {
            for (var frameIndex = 0; frameIndex < frameCount; frameIndex++) {
                var frame = sheet.Directions[group][frameIndex];
                var topPadding = cellHeight - frame.Height;
                var targetX = checked(frameIndex * cellWidth);
                var targetY = checked(group * cellHeight + topPadding);
                for (var y = 0; y < frame.Height; y++) {
                    for (var x = 0; x < frame.Width; x++) {
                        var sourcePixel = checked(y * frame.Width + x);
                        if (frame.Opacity[sourcePixel] == 0)
                            continue;
                        var paletteOffset = frame.Indices[sourcePixel] * NativePlayerPalette.BytesPerColor;
                        var targetPixel = checked((targetY + y) * scanlineBytes + 1 + (targetX + x) * 4);
                        rgba[targetPixel] = palette.Rgb[paletteOffset];
                        rgba[targetPixel + 1] = palette.Rgb[paletteOffset + 1];
                        rgba[targetPixel + 2] = palette.Rgb[paletteOffset + 2];
                        rgba[targetPixel + 3] = byte.MaxValue;
                    }
                }
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(rgba);
        if (compressed.Length > MaximumPngBytes)
            throw new InvalidDataException("Generated native towner PNG exceeds its compressed output limit.");

        using var png = new MemoryStream(checked((int)compressed.Length + 64));
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        Span<byte> ihdr = stackalloc byte[13];
        ihdr.Clear();
        BinaryPrimitives.WriteUInt32BigEndian(ihdr, checked((uint)atlasWidth));
        BinaryPrimitives.WriteUInt32BigEndian(ihdr[4..], checked((uint)atlasHeight));
        ihdr[8] = 8;
        ihdr[9] = 6;
        WriteChunk(png, "IHDR"u8, ihdr);
        WriteChunk(png, "IDAT"u8, compressed.GetBuffer().AsSpan(0, checked((int)compressed.Length)));
        WriteChunk(png, "IEND"u8, ReadOnlySpan<byte>.Empty);
        if (png.Length > MaximumPngBytes)
            throw new InvalidDataException("Generated native towner PNG exceeds its output limit.");
        return new NativeTownerAtlasPixels(png.ToArray(), atlasWidth, atlasHeight);
    }

    public static string Sha256Hex(ReadOnlySpan<byte> bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)data.Length));
        output.Write(length);
        output.Write(type);
        output.Write(data);
        Span<byte> checksum = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(checksum, ComputeCrc(type, data));
        output.Write(checksum);
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

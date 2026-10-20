using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Devilution.Tools.NativePlayerArt;

public sealed record NativePlayerPalette(byte[] Rgb)
{
    public const int ColorCount = 256;
    public const int BytesPerColor = 3;

    public static NativePlayerPalette Parse(ReadOnlySpan<byte> data, string description)
    {
        if (data.Length != ColorCount * BytesPerColor)
            throw new InvalidDataException($"{description} must contain exactly 768 RGB bytes; found {data.Length}.");
        return new NativePlayerPalette(data.ToArray());
    }

    public void Validate()
    {
        if (Rgb.Length != ColorCount * BytesPerColor)
            throw new InvalidDataException("Palette must contain exactly 256 RGB colors.");
    }
}

/** Writes native indexed frames as a bottom-aligned RGBA atlas without external image packages. */
public static class NativePlayerArtPng
{
    public const int MaximumAtlasDimension = 8192;
    public const long MaximumAtlasPixels = 4L * 1024 * 1024;
    public const int MaximumPngBytes = 128 * 1024 * 1024;

    public static byte[] EncodeAtlas(
        NativeCl2Sheet sheet,
        NativePlayerPalette palette,
        ReadOnlyMemory<byte>? graphicTranslation = null,
        ReadOnlyMemory<byte>? classTranslation = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(palette);
        palette.Validate();
        var (framesPerDirection, frameWidth, _) = ValidateSheet(sheet);
        var frameHeight = sheet.Directions.SelectMany(direction => direction).Max(frame => frame.Height);
        var atlasWidth = checked(frameWidth * framesPerDirection);
        var atlasHeight = checked(frameHeight * NativeCl2SpriteSheetDecoder.DirectionCount);
        var atlasPixels = checked((long)atlasWidth * atlasHeight);
        if (atlasWidth > MaximumAtlasDimension || atlasHeight > MaximumAtlasDimension || atlasPixels > MaximumAtlasPixels)
            throw new InvalidDataException("Generated PNG atlas exceeds its dimension or pixel limit.");
        ValidateTranslation(graphicTranslation, "graphic");
        ValidateTranslation(classTranslation, "class");

        var scanlineBytes = checked(atlasWidth * 4 + 1);
        var rawLength = checked(scanlineBytes * atlasHeight);
        if (rawLength > MaximumPngBytes)
            throw new InvalidDataException("Generated PNG exceeds its raw image data limit.");
        var rgba = new byte[rawLength];
        for (var y = 0; y < atlasHeight; y++) {
            var rowStart = y * scanlineBytes;
            rgba[rowStart] = 0;
            var direction = y / frameHeight;
            var yInDirection = y % frameHeight;
            for (var frameIndex = 0; frameIndex < framesPerDirection; frameIndex++) {
                var frame = sheet.Directions[direction][frameIndex];
                var topPadding = frameHeight - frame.Height;
                var sourceTopY = yInDirection - topPadding;
                if (sourceTopY < 0 || sourceTopY >= frame.Height)
                    continue;
                var sourceY = frame.Height - 1 - sourceTopY;
                var sourceRow = checked(sourceY * frame.Width);
                var targetStart = checked(rowStart + 1 + frameIndex * frameWidth * 4);
                for (var x = 0; x < frame.Width; x++) {
                    var sourcePixel = sourceRow + x;
                    if (frame.Opacity[sourcePixel] == 0)
                        continue;

                    var paletteIndex = frame.Indices[sourcePixel];
                    if (graphicTranslation.HasValue)
                        paletteIndex = graphicTranslation.Value.Span[paletteIndex];
                    if (classTranslation.HasValue)
                        paletteIndex = classTranslation.Value.Span[paletteIndex];
                    var paletteOffset = paletteIndex * NativePlayerPalette.BytesPerColor;
                    var targetPixel = targetStart + x * 4;
                    rgba[targetPixel] = palette.Rgb[paletteOffset];
                    rgba[targetPixel + 1] = palette.Rgb[paletteOffset + 1];
                    rgba[targetPixel + 2] = palette.Rgb[paletteOffset + 2];
                    rgba[targetPixel + 3] = byte.MaxValue;
                }
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(rgba);
        if (compressed.Length > MaximumPngBytes)
            throw new InvalidDataException("Generated PNG exceeds its compressed output limit.");

        using var png = new MemoryStream(checked((int)compressed.Length + 64));
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr, checked((uint)atlasWidth));
        BinaryPrimitives.WriteUInt32BigEndian(ihdr[4..], checked((uint)atlasHeight));
        ihdr[8] = 8;
        ihdr[9] = 6;
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;
        WriteChunk(png, "IHDR"u8, ihdr);
        WriteChunk(png, "IDAT"u8, compressed.GetBuffer().AsSpan(0, checked((int)compressed.Length)));
        WriteChunk(png, "IEND"u8, ReadOnlySpan<byte>.Empty);
        if (png.Length > MaximumPngBytes)
            throw new InvalidDataException("Generated PNG exceeds its output limit.");
        return png.ToArray();
    }

    public static byte[] Sha256(ReadOnlySpan<byte> bytes) => SHA256.HashData(bytes);

    private static (int FramesPerDirection, int CellWidth, int AnchorWidth) ValidateSheet(NativeCl2Sheet sheet)
    {
        if (sheet.Directions.Count != NativeCl2SpriteSheetDecoder.DirectionCount)
            throw new InvalidDataException("Native player atlas must contain eight direction rows.");
        var framesPerDirection = sheet.Directions[0].Count;
        if (framesPerDirection is < 1 or > NativeCl2SpriteSheetDecoder.MaximumFrameCountPerDirection)
            throw new InvalidDataException("Native player atlas has an invalid frame count.");
        var cellWidth = sheet.Directions.SelectMany(direction => direction).Max(frame => frame.Width);
        var anchorWidth = sheet.Directions[0][0].AnchorWidth;
        foreach (var direction in sheet.Directions) {
            if (direction.Count != framesPerDirection)
                throw new InvalidDataException("Native player direction groups have different frame counts.");
            foreach (var frame in direction) {
                var pixels = checked(frame.Width * frame.Height);
                if (frame.Width is < 1 or > NativeCl2SpriteSheetDecoder.MaximumFrameStorageWidth
                    || frame.AnchorWidth < 1 || frame.AnchorWidth > frame.Width
                    || frame.AnchorWidth != anchorWidth
                    || frame.Height is < 1 or > NativeCl2SpriteSheetDecoder.MaximumFrameHeight
                    || frame.Indices.Length != pixels || frame.Opacity.Length != pixels)
                    throw new InvalidDataException("Native player frame dimensions or pixel buffers are inconsistent.");
            }
        }
        return (framesPerDirection, cellWidth, anchorWidth);
    }

    private static void ValidateTranslation(ReadOnlyMemory<byte>? translation, string name)
    {
        if (translation.HasValue && translation.Value.Length != NativePlayerPalette.ColorCount)
            throw new InvalidDataException($"The {name} translation table must contain exactly 256 bytes.");
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        if (type.Length != 4 || data.Length > MaximumPngBytes)
            throw new InvalidDataException("PNG chunk has an invalid type or length.");
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)data.Length));
        output.Write(length);
        output.Write(type);
        output.Write(data);
        var crc = new Crc32();
        crc.Append(type);
        crc.Append(data);
        Span<byte> checksum = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(checksum, crc.GetCurrent());
        output.Write(checksum);
    }

    private sealed class Crc32
    {
        private uint value = uint.MaxValue;

        public void Append(ReadOnlySpan<byte> bytes)
        {
            foreach (var current in bytes) {
                value ^= current;
                for (var bit = 0; bit < 8; bit++)
                    value = (value & 1) != 0 ? (value >> 1) ^ 0xEDB88320U : value >> 1;
            }
        }

        public uint GetCurrent() => ~value;
    }
}

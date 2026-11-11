using System.Buffers.Binary;

namespace Devilution.Client.PresentationAssets;

/** Optional private PNG chunk: palette-index provenance for native cursor selection. Old PNG readers ignore it. */
public static class NativeTownerPickingData
{
    public static ReadOnlySpan<byte> ChunkName => "npIx"u8;
    private const int HeaderSize = 9;
    private const int MaximumPixels = 4 * 1024 * 1024;

    public static byte[] Create(int width, int height)
    {
        var pixels = PixelCount(width, height);
        var data = new byte[HeaderSize + (pixels + 7) / 8];
        data[0] = 1;
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(1), width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(5), height);
        return data;
    }

    public static void SetSelectable(Span<byte> data, int pixel)
        => data[HeaderSize + pixel / 8] |= (byte)(1 << (pixel % 8));

    public static bool IsSelectable(ReadOnlySpan<byte> data, int pixel)
        => (data[HeaderSize + pixel / 8] & (1 << (pixel % 8))) != 0;

    public static void Validate(ReadOnlySpan<byte> data, int width, int height)
    {
        var pixels = PixelCount(width, height);
        if (data.Length != HeaderSize + (pixels + 7) / 8 || data[0] != 1
            || BinaryPrimitives.ReadInt32BigEndian(data[1..]) != width
            || BinaryPrimitives.ReadInt32BigEndian(data[5..]) != height)
            throw new InvalidDataException("Native resident picking metadata has invalid dimensions or version.");
    }

    /** Missing data is a legacy atlas: retain native tile selection instead of guessing palette indices from RGB. */
    public static bool TryReadPng(ReadOnlySpan<byte> png, int width, int height, out ReadOnlySpan<byte> data)
    {
        data = default;
        if (png.Length < 8 || !png[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            throw new InvalidDataException("Native resident picking requires a PNG atlas.");
        var offset = 8;
        while (offset < png.Length) {
            if (png.Length - offset < 12) throw new InvalidDataException("Truncated resident PNG chunk.");
            var size = BinaryPrimitives.ReadUInt32BigEndian(png[offset..]);
            if (size > png.Length - offset - 12) throw new InvalidDataException("Truncated resident PNG chunk.");
            var type = png.Slice(offset + 4, 4);
            if (type.SequenceEqual(ChunkName)) {
                if (!data.IsEmpty) throw new InvalidDataException("Duplicate resident picking metadata.");
                data = png.Slice(offset + 8, (int)size);
                Validate(data, width, height);
            }
            offset += (int)size + 12;
            if (type.SequenceEqual("IEND"u8)) break;
        }
        return !data.IsEmpty;
    }

    private static int PixelCount(int width, int height)
    {
        if (width <= 0 || height <= 0 || (long)width * height > MaximumPixels)
            throw new InvalidDataException("Native resident picking metadata exceeds the atlas bounds.");
        return width * height;
    }
}

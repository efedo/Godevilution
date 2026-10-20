using System.Buffers.Binary;
using System.IO.Compression;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

/** Bounded independent PNG reader shared by native art reference comparisons. */
internal static class NativeArtReferencePng
{
    private const int MaximumPngBytes = 128 * 1024 * 1024;

    internal static (int Width, int Height, byte[] Rgba) Read(byte[] png, int maximumDimension, long maximumPixels, string description)
    {
        Assert.InRange(png.Length, 8, MaximumPngBytes);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png.AsSpan(0, 8).ToArray());
        var offset = 8;
        var width = 0;
        var height = 0;
        var sawHeader = false;
        var sawEnd = false;
        using var compressed = new MemoryStream();
        while (offset < png.Length) {
            var length = ReadBigEndianUInt32(png, ref offset, $"{description} chunk length");
            Assert.InRange(length, 0u, (uint)MaximumPngBytes);
            Assert.True(length <= (uint)(png.Length - offset - 8), $"{description} chunk exceeds the PNG file.");
            var type = System.Text.Encoding.ASCII.GetString(png, offset, 4);
            offset += 4;
            var data = png.AsSpan(offset, checked((int)length));
            offset += checked((int)length);
            var recordedCrc = ReadBigEndianUInt32(png, ref offset, $"{description} chunk CRC");
            Assert.Equal(recordedCrc, ComputeCrc32(png.AsSpan(offset - checked((int)length) - 8, checked((int)length) + 4)));

            if (!sawHeader) {
                Assert.Equal("IHDR", type);
                Assert.Equal(13, data.Length);
                width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data));
                height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data[4..]));
                Assert.Equal(new byte[] { 8, 6, 0, 0, 0 }, data[8..].ToArray());
                Assert.InRange(width, 1, maximumDimension);
                Assert.InRange(height, 1, maximumDimension);
                Assert.InRange((long)width * height, 1, maximumPixels);
                sawHeader = true;
            } else if (type == "IDAT") {
                compressed.Write(data);
            } else if (type == "IEND") {
                Assert.Empty(data.ToArray());
                sawEnd = true;
                Assert.Equal(png.Length, offset);
                break;
            }
        }
        Assert.True(sawHeader && sawEnd, $"{description} has no complete PNG structure.");

        compressed.Position = 0;
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        var stride = checked(width * 4);
        var filtered = new byte[checked(height * (stride + 1))];
        zlib.ReadExactly(filtered);
        Assert.Equal(-1, zlib.ReadByte());
        var rgba = new byte[checked(width * height * 4)];
        for (var y = 0; y < height; y++) {
            var sourceStart = y * (stride + 1);
            var filter = filtered[sourceStart];
            var targetRow = rgba.AsSpan(y * stride, stride);
            var sourceRow = filtered.AsSpan(sourceStart + 1, stride);
            var previousRow = y == 0 ? Span<byte>.Empty : rgba.AsSpan((y - 1) * stride, stride);
            UnfilterRow(filter, sourceRow, targetRow, previousRow);
        }
        return (width, height, rgba);
    }

    private static void UnfilterRow(byte filter, ReadOnlySpan<byte> source, Span<byte> target, ReadOnlySpan<byte> previous)
    {
        Assert.InRange(filter, (byte)0, (byte)4);
        for (var index = 0; index < source.Length; index++) {
            var left = index >= 4 ? target[index - 4] : (byte)0;
            var above = previous.IsEmpty ? (byte)0 : previous[index];
            var upperLeft = previous.IsEmpty || index < 4 ? (byte)0 : previous[index - 4];
            var predictor = filter switch {
                0 => 0,
                1 => left,
                2 => above,
                3 => (left + above) / 2,
                4 => Paeth(left, above, upperLeft),
                _ => throw new InvalidDataException("PNG filter is invalid."),
            };
            target[index] = unchecked((byte)(source[index] + predictor));
        }
    }

    private static int Paeth(byte left, byte above, byte upperLeft)
    {
        var estimate = left + above - upperLeft;
        var leftDistance = Math.Abs(estimate - left);
        var aboveDistance = Math.Abs(estimate - above);
        var upperLeftDistance = Math.Abs(estimate - upperLeft);
        return leftDistance <= aboveDistance && leftDistance <= upperLeftDistance
            ? left
            : aboveDistance <= upperLeftDistance ? above : upperLeft;
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> typeAndData)
    {
        var crc = uint.MaxValue;
        foreach (var value in typeAndData) {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320U : crc >> 1;
        }
        return ~crc;
    }

    private static uint ReadBigEndianUInt32(byte[] bytes, ref int offset, string description)
    {
        Assert.True(offset >= 0 && offset <= bytes.Length - sizeof(uint), $"PNG is truncated at {description}.");
        var value = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, sizeof(uint)));
        offset += sizeof(uint);
        return value;
    }

}

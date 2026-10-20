using System.Buffers.Binary;

namespace Devilution.Tools.NativeTownerArt;

/** One bottom-aligned indexed frame as drawn by the native CEL-to-CLX path. */
public sealed record NativeTownerCelFrame(int Width, int Height, byte[] Indices, byte[] Opacity)
{
    public void Validate(string description)
    {
        var pixelCount = checked(Width * Height);
        if (Width is < 1 or > NativeTownerCelDecoder.MaximumFrameWidth
            || Height is < 1 or > NativeTownerCelDecoder.MaximumFrameHeight
            || pixelCount > NativeTownerCelDecoder.MaximumPixelsPerFrame
            || Indices.Length != pixelCount
            || Opacity.Length != pixelCount
            || Opacity.Any(value => value is not 0 and not byte.MaxValue))
            throw new InvalidDataException($"{description} is outside the native towner frame bounds.");
    }
}

/** CEL groups in native direction order. Human towner files contain one group; cow.cel has eight. */
public sealed record NativeTownerCelSheet(IReadOnlyList<IReadOnlyList<NativeTownerCelFrame>> Directions);

/** Bounded decoder for the original CEL group tables and row RLE used by LoadCel/LoadCelSheet. */
public static class NativeTownerCelDecoder
{
    public const int MaximumInputBytes = 64 * 1024 * 1024;
    public const int MaximumGroupCount = 8;
    public const int MaximumFramesPerGroup = 64;
    public const int MaximumFrameWidth = 128;
    public const int MaximumFrameHeight = 1024;
    public const int MaximumPixelsPerFrame = 128 * 1024;
    public const long MaximumPixelsPerSheet = 16L * 1024 * 1024;

    public static NativeTownerCelSheet Decode(
        ReadOnlySpan<byte> data,
        int width,
        int expectedGroupCount,
        int expectedFramesPerGroup)
    {
        if (data.Length is < 8 or > MaximumInputBytes)
            throw new InvalidDataException("Native towner CEL file size is outside the supported bounds.");
        if (width is < 1 or > MaximumFrameWidth)
            throw new InvalidDataException("Native towner CEL width is outside the supported bounds.");
        if (expectedGroupCount is < 1 or > MaximumGroupCount
            || expectedFramesPerGroup is < 0 or > MaximumFramesPerGroup)
            throw new InvalidDataException("Native towner CEL frame metadata is outside the supported bounds.");

        var first = ReadUInt32(data, 0, "CEL header");
        var isSingleGroup = IsSingleGroup(data, first);
        int[] groupStarts;
        if (isSingleGroup) {
            if (expectedGroupCount != 1)
                throw new InvalidDataException("Native towner CEL file has one group; the selected sprite requires eight.");
            groupStarts = [0, data.Length];
        } else {
            if (first is < sizeof(uint) or > MaximumGroupCount * sizeof(uint) || first % sizeof(uint) != 0)
                throw new InvalidDataException("Native towner CEL has an invalid group directory.");
            var groupCount = checked((int)(first / sizeof(uint)));
            if (groupCount != expectedGroupCount || first > data.Length)
                throw new InvalidDataException($"Native towner CEL has {groupCount} groups; expected {expectedGroupCount}.");
            groupStarts = new int[groupCount + 1];
            for (var group = 0; group < groupCount; group++) {
                var offset = ReadUInt32(data, group * sizeof(uint), "CEL group directory");
                if (offset > int.MaxValue || offset > data.Length)
                    throw new InvalidDataException($"Native towner CEL group {group} has an out-of-range offset.");
                groupStarts[group] = (int)offset;
            }
            groupStarts[groupCount] = data.Length;
            if (groupStarts[0] != (int)first)
                throw new InvalidDataException("Native towner CEL group data does not immediately follow its directory.");
            for (var group = 0; group < groupCount; group++) {
                if (groupStarts[group] >= groupStarts[group + 1])
                    throw new InvalidDataException("Native towner CEL group offsets must be strictly increasing.");
            }
        }

        var directions = new List<IReadOnlyList<NativeTownerCelFrame>>(expectedGroupCount);
        long totalPixels = 0;
        for (var group = 0; group < expectedGroupCount; group++) {
            var groupStart = groupStarts[group];
            var groupEnd = groupStarts[group + 1];
            var frameCount = ReadGroupFrameCount(data, groupStart, groupEnd, group);
            if (expectedFramesPerGroup != 0 && frameCount != expectedFramesPerGroup)
                throw new InvalidDataException($"Native towner CEL group {group} has {frameCount} source frames; expected {expectedFramesPerGroup}.");

            var tableBytes = checked((frameCount + 2) * sizeof(uint));
            var offsets = new int[frameCount + 1];
            var previous = tableBytes;
            for (var frame = 0; frame <= frameCount; frame++) {
                var rawOffset = ReadUInt32(data, groupStart + (frame + 1) * sizeof(uint), $"CEL group {group} frame table");
                if (rawOffset > int.MaxValue || rawOffset < tableBytes || rawOffset > groupEnd - groupStart)
                    throw new InvalidDataException($"Native towner CEL group {group} frame {frame} has an invalid offset.");
                offsets[frame] = (int)rawOffset;
                if (offsets[frame] <= previous && frame != 0)
                    throw new InvalidDataException($"Native towner CEL group {group} frame offsets must be strictly increasing.");
                previous = offsets[frame];
            }
            if (offsets[0] < tableBytes || offsets[^1] != groupEnd - groupStart)
                throw new InvalidDataException($"Native towner CEL group {group} frame data does not match its group boundary.");

            var frames = new List<NativeTownerCelFrame>(frameCount);
            for (var frame = 0; frame < frameCount; frame++) {
                var start = checked(groupStart + offsets[frame]);
                var end = checked(groupStart + offsets[frame + 1]);
                if (end <= start)
                    throw new InvalidDataException($"Native towner CEL group {group} frame {frame} is empty.");
                var decoded = DecodeFrame(data.Slice(start, end - start), width, $"group {group} frame {frame}");
                totalPixels = checked(totalPixels + (long)decoded.Width * decoded.Height);
                if (totalPixels > MaximumPixelsPerSheet)
                    throw new InvalidDataException($"Native towner CEL sheet exceeds {MaximumPixelsPerSheet} decoded pixels.");
                frames.Add(decoded);
            }
            directions.Add(frames.AsReadOnly());
        }
        return new NativeTownerCelSheet(directions.AsReadOnly());
    }

    private static bool IsSingleGroup(ReadOnlySpan<byte> data, uint first)
    {
        if (first is < 1 or > MaximumFramesPerGroup)
            return false;
        var terminalPosition = ((long)first + 1) * sizeof(uint);
        return terminalPosition + sizeof(uint) <= data.Length
            && ReadUInt32(data, checked((int)terminalPosition), "CEL frame directory") == data.Length;
    }

    private static int ReadGroupFrameCount(ReadOnlySpan<byte> data, int groupStart, int groupEnd, int group)
    {
        if (groupStart < 0 || groupEnd - groupStart < 3 * sizeof(uint))
            throw new InvalidDataException($"Native towner CEL group {group} header is truncated.");
        var count = ReadUInt32(data, groupStart, $"CEL group {group} header");
        if (count is < 1 or > MaximumFramesPerGroup)
            throw new InvalidDataException($"Native towner CEL group {group} frame count is outside 1..{MaximumFramesPerGroup}.");
        var tableBytes = checked(((int)count + 2) * sizeof(uint));
        if (tableBytes > groupEnd - groupStart)
            throw new InvalidDataException($"Native towner CEL group {group} frame table is truncated.");
        return checked((int)count);
    }

    private static NativeTownerCelFrame DecodeFrame(ReadOnlySpan<byte> frame, int width, string description)
    {
        if (frame.Length >= 10 && BinaryPrimitives.ReadUInt16LittleEndian(frame) == 10)
            frame = frame[10..];
        var rows = new List<(byte[] Indices, byte[] Opacity)>();
        var cursor = 0;
        while (cursor < frame.Length) {
            if (rows.Count >= MaximumFrameHeight)
                throw new InvalidDataException($"Native towner CEL {description} exceeds {MaximumFrameHeight} rows.");
            var indices = new byte[width];
            var opacity = new byte[width];
            var x = 0;
            while (x < width) {
                if (cursor >= frame.Length)
                    throw new InvalidDataException($"Native towner CEL {description} ends inside an RLE row.");
                var control = frame[cursor++];
                if (control == 0)
                    throw new InvalidDataException($"Native towner CEL {description} contains a zero-length run.");
                var run = control >= 0x80 ? 256 - control : control;
                if (run > width - x)
                    throw new InvalidDataException($"Native towner CEL {description} run exceeds width {width}.");
                if (control < 0x80) {
                    if (cursor > frame.Length - run)
                        throw new InvalidDataException($"Native towner CEL {description} ends inside an opaque run.");
                    frame.Slice(cursor, run).CopyTo(indices.AsSpan(x, run));
                    opacity.AsSpan(x, run).Fill(byte.MaxValue);
                    cursor += run;
                }
                x += run;
            }
            rows.Add((indices, opacity));
        }
        if (rows.Count == 0)
            throw new InvalidDataException($"Native towner CEL {description} contains no rows.");
        var pixelCount = checked(width * rows.Count);
        if (pixelCount > MaximumPixelsPerFrame)
            throw new InvalidDataException($"Native towner CEL {description} exceeds the per-frame pixel limit.");
        var resultIndices = new byte[pixelCount];
        var resultOpacity = new byte[pixelCount];
        for (var sourceRow = 0; sourceRow < rows.Count; sourceRow++) {
            var targetStart = checked((rows.Count - sourceRow - 1) * width);
            rows[sourceRow].Indices.CopyTo(resultIndices, targetStart);
            rows[sourceRow].Opacity.CopyTo(resultOpacity, targetStart);
        }
        var result = new NativeTownerCelFrame(width, rows.Count, resultIndices, resultOpacity);
        result.Validate($"Native towner CEL {description}");
        return result;
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset, string description)
    {
        if (offset < 0 || offset > data.Length - sizeof(uint))
            throw new InvalidDataException($"{description} is truncated.");
        return BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, sizeof(uint)));
    }
}

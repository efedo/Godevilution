using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Devilution.Tools.NativePlayerArt;

/** A decoded indexed sprite frame. Width includes native right-edge run spill; AnchorWidth is the source CL2 width. */
public sealed record NativeCl2Frame
{
    public NativeCl2Frame(int width, int height, byte[] indices, byte[] opacity)
        : this(width, height, indices, opacity, width)
    {
    }

    public NativeCl2Frame(int width, int height, byte[] indices, byte[] opacity, int anchorWidth)
    {
        Width = width;
        Height = height;
        Indices = indices;
        Opacity = opacity;
        AnchorWidth = anchorWidth;
    }

    public int Width { get; init; }
    public int Height { get; init; }
    public byte[] Indices { get; init; }
    public byte[] Opacity { get; init; }
    public int AnchorWidth { get; init; }

    public bool IsOpaque(int x, int y) => Opacity[checked(y * Width + x)] != 0;
}

/** The native eight-direction CL2 group in enum order: S, SW, W, NW, N, NE, E, SE. */
public sealed record NativeCl2Sheet(IReadOnlyList<IReadOnlyList<NativeCl2Frame>> Directions);

/** Header and frame boundaries from one CL2 direction group. Offsets are relative to GroupStart. */
internal sealed record NativeCl2GroupHeader(int GroupStart, int HeaderEnd, int[] FrameOffsets);

/** Bounded decoder for the CL2 sheet layout consumed by DevilutionX's LoadCl2Sheet. */
public static class NativeCl2SpriteSheetDecoder
{
    public const int DirectionCount = 8;
    public const int MaximumInputBytes = 64 * 1024 * 1024;
    public const int MaximumFrameCountPerDirection = 64;
    public const int MaximumFrameWidth = 512;
    public const int MaximumNativeRightSpillPixels = 64;
    public const int MaximumFrameStorageWidth = MaximumFrameWidth + MaximumNativeRightSpillPixels;
    public const int MaximumFrameHeight = 2048;
    public const long MaximumDecodedPixelsPerSheet = 32L * 1024 * 1024;

    public static NativeCl2Sheet Decode(
        ReadOnlySpan<byte> data,
        int frameWidth,
        int expectedFramesPerDirection,
        bool allowAdditionalFrames = false)
    {
        if (data.Length < DirectionCount * sizeof(uint) || data.Length > MaximumInputBytes)
            throw new InvalidDataException("CL2 sheet size is outside the supported bounds.");
        if (frameWidth is < 1 or > MaximumFrameWidth)
            throw new InvalidDataException($"CL2 frame width {frameWidth} is outside the supported bounds.");
        if (expectedFramesPerDirection is < 1 or > MaximumFrameCountPerDirection)
            throw new InvalidDataException("Expected CL2 frame count is outside the supported bounds.");

        var directoryBytes = ReadUInt32(data, 0);
        if (directoryBytes != DirectionCount * sizeof(uint))
            throw new InvalidDataException("CL2 player sheet must contain exactly eight ordered direction groups.");

        var groupOffsets = new int[DirectionCount + 1];
        for (var direction = 0; direction < DirectionCount; direction++) {
            var offset = ReadUInt32(data, direction * sizeof(uint));
            if (offset > int.MaxValue || offset < directoryBytes || offset >= data.Length)
                throw new InvalidDataException($"CL2 direction {direction} has an invalid group offset.");
            groupOffsets[direction] = (int)offset;
        }
        groupOffsets[DirectionCount] = data.Length;
        if (groupOffsets[0] != directoryBytes)
            throw new InvalidDataException("CL2 direction data must immediately follow its offset table.");
        for (var direction = 1; direction <= DirectionCount; direction++) {
            if (groupOffsets[direction] <= groupOffsets[direction - 1])
                throw new InvalidDataException("CL2 direction offsets must be strictly increasing.");
        }

        var groups = new List<NativeCl2GroupHeader>(DirectionCount);
        for (var direction = 0; direction < DirectionCount; direction++) {
            var groupStart = groupOffsets[direction];
            var nextGroupStart = groupOffsets[direction + 1];
            groups.Add(ReadGroupHeader(data, groupStart, nextGroupStart, expectedFramesPerDirection, allowAdditionalFrames));
        }

        var actualFrameCount = groups[0].FrameOffsets.Length - 1;
        if (groups.Any(group => group.FrameOffsets.Length - 1 != actualFrameCount))
            throw new InvalidDataException("CL2 direction groups must contain the same source frame count.");

        ValidateNonOverlappingRanges(data.Length, directoryBytes, groups);

        var directions = new List<IReadOnlyList<NativeCl2Frame>>(DirectionCount);
        long totalPixels = 0;
        for (var direction = 0; direction < DirectionCount; direction++) {
            var frames = DecodeGroup(data, groups[direction], frameWidth, ref totalPixels);
            directions.Add(frames);
        }
        return new NativeCl2Sheet(directions);
    }

    private static NativeCl2GroupHeader ReadGroupHeader(
        ReadOnlySpan<byte> data,
        int groupStart,
        int nextGroupStart,
        int expectedFrames,
        bool allowAdditionalFrames)
    {
        if (groupStart < 0 || groupStart > data.Length - sizeof(uint) * 3)
            throw new InvalidDataException("CL2 direction group header is truncated.");

        var rawFrameCount = ReadUInt32(data, groupStart);
        var hasExpectedFrames = allowAdditionalFrames ? rawFrameCount >= expectedFrames : rawFrameCount == expectedFrames;
        if (!hasExpectedFrames || rawFrameCount is < 1 or > MaximumFrameCountPerDirection)
            throw new InvalidDataException($"CL2 direction contains {rawFrameCount} frames; expected {(allowAdditionalFrames ? "at least " : string.Empty)}{expectedFrames}.");
        var frameCount = checked((int)rawFrameCount);

        var offsetsBytes = checked((frameCount + 2) * sizeof(uint));
        var headerEnd = checked(groupStart + offsetsBytes);
        if (headerEnd > nextGroupStart)
            throw new InvalidDataException("CL2 direction frame-offset table is truncated.");

        var frameOffsets = new int[frameCount + 1];
        for (var frame = 0; frame <= frameCount; frame++) {
            var offset = ReadUInt32(data, groupStart + sizeof(uint) * (frame + 1));
            if (offset > int.MaxValue || offset < offsetsBytes || offset > data.Length - groupStart)
                throw new InvalidDataException($"CL2 frame {frame} has an invalid offset.");
            frameOffsets[frame] = (int)offset;
        }
        if (frameOffsets[0] < offsetsBytes)
            throw new InvalidDataException("CL2 frame data overlaps its direction header.");
        for (var frame = 1; frame < frameOffsets.Length; frame++) {
            if (frameOffsets[frame] <= frameOffsets[frame - 1])
                throw new InvalidDataException("CL2 frame offsets must be strictly increasing.");
        }

        return new NativeCl2GroupHeader(groupStart, headerEnd, frameOffsets);
    }

    private static void ValidateNonOverlappingRanges(int dataLength, uint directoryBytes, IReadOnlyList<NativeCl2GroupHeader> groups)
    {
        var ranges = new List<(int Start, int End, string Description)>(1 + groups.Count * 2) {
            (0, checked((int)directoryBytes), "CL2 direction directory"),
        };
        foreach (var group in groups) {
            ranges.Add((group.GroupStart, group.HeaderEnd, "CL2 direction header"));
            for (var frame = 0; frame + 1 < group.FrameOffsets.Length; frame++) {
                var start = checked(group.GroupStart + group.FrameOffsets[frame]);
                var end = checked(group.GroupStart + group.FrameOffsets[frame + 1]);
                if (start < group.HeaderEnd || end > dataLength || end <= start)
                    throw new InvalidDataException($"CL2 frame {frame} has an invalid byte range.");
                ranges.Add((start, end, "CL2 frame data"));
            }
        }

        ranges.Sort((left, right) => left.Start.CompareTo(right.Start));
        for (var range = 1; range < ranges.Count; range++) {
            if (ranges[range].Start < ranges[range - 1].End)
                throw new InvalidDataException($"CL2 {ranges[range].Description} overlaps {ranges[range - 1].Description}.");
        }
    }

    private static IReadOnlyList<NativeCl2Frame> DecodeGroup(
        ReadOnlySpan<byte> data,
        NativeCl2GroupHeader group,
        int frameWidth,
        ref long totalPixels)
    {
        var frameCount = group.FrameOffsets.Length - 1;
        var frames = new List<NativeCl2Frame>(frameCount);
        for (var frame = 0; frame < frameCount; frame++) {
            var frameStart = checked(group.GroupStart + group.FrameOffsets[frame]);
            var frameEnd = checked(group.GroupStart + group.FrameOffsets[frame + 1]);
            var decoded = DecodeFrame(data[frameStart..frameEnd], frameWidth, ref totalPixels);
            frames.Add(decoded);
        }
        return frames;
    }

    private static NativeCl2Frame DecodeFrame(ReadOnlySpan<byte> frame, int width, ref long totalPixels)
    {
        if (frame.Length < sizeof(ushort))
            throw new InvalidDataException("CL2 frame header is truncated.");
        var pixelDataOffset = BinaryPrimitives.ReadUInt16LittleEndian(frame);
        if (pixelDataOffset < sizeof(ushort) || pixelDataOffset > frame.Length)
            throw new InvalidDataException("CL2 frame pixel-data offset is invalid.");

        var stream = frame[pixelDataOffset..];
        if (stream.Length == 0)
            throw new InvalidDataException("CL2 frame has no encoded pixels.");

        var clx = new List<byte>(Math.Min(stream.Length, 16 * 1024));
        var pendingOpaquePixels = new List<byte>(Math.Min(width * 16, 4096));
        var pendingTransparentPixels = 0;
        var sourceOffset = 0;
        var xOffset = 0;
        var height = 0;
        long decodedPixels = 0;
        while (sourceOffset < stream.Length) {
            var remainingWidth = width - xOffset;
            while (remainingWidth > 0) {
                if (sourceOffset >= stream.Length)
                    throw new InvalidDataException("CL2 frame ends before its final source row is complete.");

                var control = stream[sourceOffset++];
                if (control < 0x80) {
                    FlushOpaquePixels(pendingOpaquePixels, clx);
                    pendingTransparentPixels = checked(pendingTransparentPixels + control);
                    decodedPixels = checked(decodedPixels + control);
                    remainingWidth -= control;
                } else if (control <= 0xBE) {
                    var runLength = 0xBF - control;
                    if (sourceOffset >= stream.Length)
                        throw new InvalidDataException("CL2 opaque-fill run is missing its palette index.");
                    FlushTransparentPixels(pendingTransparentPixels, clx);
                    pendingTransparentPixels = 0;
                    var index = stream[sourceOffset++];
                    AppendOpaquePixels(pendingOpaquePixels, index, runLength);
                    decodedPixels = checked(decodedPixels + runLength);
                    remainingWidth -= runLength;
                } else {
                    var runLength = 256 - control;
                    if (runLength > stream.Length - sourceOffset)
                        throw new InvalidDataException("CL2 literal run exceeds the encoded frame data.");
                    FlushTransparentPixels(pendingTransparentPixels, clx);
                    pendingTransparentPixels = 0;
                    if (runLength > MaximumFrameWidth * MaximumFrameHeight - pendingOpaquePixels.Count)
                        throw new InvalidDataException("CL2 opaque run exceeds the decoded frame limit.");
                    for (var pixel = 0; pixel < runLength; pixel++)
                        pendingOpaquePixels.Add(stream[sourceOffset++]);
                    decodedPixels = checked(decodedPixels + runLength);
                    remainingWidth -= runLength;
                }

                if (decodedPixels > MaximumFrameWidth * (long)MaximumFrameHeight)
                    throw new InvalidDataException("CL2 frame exceeds the decoded pixel limit.");
            }

            var skip = GetSkipSize(remainingWidth, width);
            xOffset = skip.XOffset;
            height = checked(height + skip.WholeLines);
            if (height is < 1 or > MaximumFrameHeight)
                throw new InvalidDataException($"CL2 frame height {height} is outside the supported bounds.");
        }

        FlushOpaquePixels(pendingOpaquePixels, clx);
        FlushTransparentPixels(pendingTransparentPixels, clx);
        var rendered = RenderClxFrame(clx, width, height);
        totalPixels = checked(totalPixels + (long)rendered.Width * height);
        if (totalPixels > MaximumDecodedPixelsPerSheet)
            throw new InvalidDataException("CL2 sheet exceeds the decoded pixel limit.");
        return rendered;
    }

    private static void AppendOpaquePixels(List<byte> pending, byte color, int count)
    {
        if (count < 0 || count > MaximumFrameWidth * MaximumFrameHeight - pending.Count)
            throw new InvalidDataException("CL2 opaque runs exceed the decoded frame limit.");
        for (var index = 0; index < count; index++)
            pending.Add(color);
    }

    private static void FlushOpaquePixels(List<byte> pending, List<byte> clx)
    {
        if (pending.Count == 0)
            return;
        AppendClxPixelsOrFillRun(CollectionsMarshal.AsSpan(pending), clx);
        pending.Clear();
    }

    private static void FlushTransparentPixels(int pending, List<byte> clx)
    {
        while (pending >= 0x7F) {
            AppendClxByte(clx, 0x7F);
            pending -= 0x7F;
        }
        if (pending != 0)
            AppendClxByte(clx, checked((byte)pending));
    }

    private static void AppendClxPixelsOrFillRun(ReadOnlySpan<byte> pixels, List<byte> clx)
    {
        var segmentStart = 0;
        var colorRunStart = 0;
        var colorRunLength = 1;
        for (var index = 1; index < pixels.Length; index++) {
            if (pixels[index] == pixels[index - 1]) {
                colorRunLength++;
                continue;
            }

            if (colorRunLength >= 3) {
                AppendClxPixelsRun(pixels, segmentStart, colorRunStart - segmentStart, clx);
                AppendClxFillRun(pixels[colorRunStart], colorRunLength, clx);
                segmentStart = index;
            }
            colorRunStart = index;
            colorRunLength = 1;
        }

        if (colorRunLength >= 2) {
            AppendClxPixelsRun(pixels, segmentStart, colorRunStart - segmentStart, clx);
            AppendClxFillRun(pixels[colorRunStart], colorRunLength, clx);
        } else {
            AppendClxPixelsRun(pixels, segmentStart, pixels.Length - segmentStart, clx);
        }
    }

    private static void AppendClxPixelsRun(ReadOnlySpan<byte> pixels, int offset, int count, List<byte> clx)
    {
        while (count >= 0x41) {
            AppendClxByte(clx, 0xBF);
            AppendClxBytes(clx, pixels.Slice(offset, 0x41));
            offset += 0x41;
            count -= 0x41;
        }
        if (count == 0)
            return;
        AppendClxByte(clx, checked((byte)(256 - count)));
        AppendClxBytes(clx, pixels.Slice(offset, count));
    }

    private static void AppendClxFillRun(byte color, int count, List<byte> clx)
    {
        while (count >= 0x3F) {
            AppendClxByte(clx, 0x80);
            AppendClxByte(clx, color);
            count -= 0x3F;
        }
        if (count == 0)
            return;
        AppendClxByte(clx, checked((byte)(0xBF - count)));
        AppendClxByte(clx, color);
    }

    private static void AppendClxBytes(List<byte> clx, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaximumClxFrameBytes - clx.Count)
            throw new InvalidDataException("Converted CLX frame exceeds its bounded encoded size.");
        foreach (var value in bytes)
            clx.Add(value);
    }

    private static void AppendClxByte(List<byte> clx, byte value)
    {
        if (clx.Count >= MaximumClxFrameBytes)
            throw new InvalidDataException("Converted CLX frame exceeds its bounded encoded size.");
        clx.Add(value);
    }

    private static NativeCl2Frame RenderClxFrame(List<byte> clx, int nominalWidth, int height)
    {
        var pitch = checked(nominalWidth + MaximumNativeRightSpillPixels);
        var bufferLength = checked(pitch * height);
        var renderedIndices = new byte[bufferLength];
        var renderedOpacity = new byte[bufferLength];
        var destination = checked((height - 1) * pitch);
        var sourceOffset = 0;
        var xOffset = 0;
        while (sourceOffset < clx.Count && destination >= 0) {
            var remainingWidth = nominalWidth - xOffset;
            destination = checked(destination + xOffset);
            while (remainingWidth > 0) {
                if (sourceOffset >= clx.Count)
                    throw new InvalidDataException("Converted CLX frame ends before its final row is complete.");
                var control = clx[sourceOffset++];
                if (control < 0x80) {
                    destination = checked(destination + control);
                    remainingWidth -= control;
                } else if (control <= 0xBE) {
                    var runLength = 0xBF - control;
                    if (sourceOffset >= clx.Count)
                        throw new InvalidDataException("Converted CLX fill run is missing its palette index.");
                    var color = clx[sourceOffset++];
                    WriteOpaqueRun(renderedIndices, renderedOpacity, destination, pitch, runLength, color);
                    destination = checked(destination + runLength);
                    remainingWidth -= runLength;
                } else {
                    var runLength = 256 - control;
                    if (runLength > clx.Count - sourceOffset)
                        throw new InvalidDataException("Converted CLX literal run exceeds frame data.");
                    WriteOpaqueRun(renderedIndices, renderedOpacity, destination, pitch, clx, sourceOffset, runLength);
                    sourceOffset += runLength;
                    destination = checked(destination + runLength);
                    remainingWidth -= runLength;
                }
            }

            var skip = GetSkipSize(remainingWidth, nominalWidth);
            xOffset = skip.XOffset;
            destination = checked(destination - skip.WholeLines * pitch - (nominalWidth - remainingWidth));
        }

        var storedWidth = nominalWidth;
        for (var row = 0; row < height; row++) {
            var rowStart = row * pitch;
            for (var column = nominalWidth; column < pitch; column++) {
                if (renderedOpacity[rowStart + column] != 0)
                    storedWidth = Math.Max(storedWidth, column + 1);
            }
        }
        if (storedWidth > nominalWidth + MaximumNativeRightSpillPixels)
            throw new InvalidDataException("Native CLX drawing exceeded its bounded right-edge spill.");

        var indices = new byte[checked(storedWidth * height)];
        var opacity = new byte[indices.Length];
        for (var bottomUpRow = 0; bottomUpRow < height; bottomUpRow++) {
            var sourceRow = checked((height - 1 - bottomUpRow) * pitch);
            var targetRow = checked(bottomUpRow * storedWidth);
            renderedIndices.AsSpan(sourceRow, storedWidth).CopyTo(indices.AsSpan(targetRow, storedWidth));
            renderedOpacity.AsSpan(sourceRow, storedWidth).CopyTo(opacity.AsSpan(targetRow, storedWidth));
        }
        return new NativeCl2Frame(storedWidth, height, indices, opacity, nominalWidth);
    }

    private static void WriteOpaqueRun(byte[] indices, byte[] opacity, int destination, int pitch, int count, byte color)
    {
        var firstRow = destination / pitch;
        for (var index = 0; index < count; index++) {
            var position = checked(destination + index);
            if (position < 0 || position >= indices.Length || position / pitch != firstRow)
                throw new InvalidDataException("Native CLX drawing exceeded the padded right-edge pitch.");
            indices[position] = color;
            opacity[position] = byte.MaxValue;
        }
    }

    private static void WriteOpaqueRun(byte[] indices, byte[] opacity, int destination, int pitch, List<byte> clx, int offset, int count)
    {
        var firstRow = destination / pitch;
        for (var index = 0; index < count; index++) {
            var position = checked(destination + index);
            if (position < 0 || position >= indices.Length || position / pitch != firstRow)
                throw new InvalidDataException("Native CLX drawing exceeded the padded right-edge pitch.");
            indices[position] = clx[offset + index];
            opacity[position] = byte.MaxValue;
        }
    }

    private static (int WholeLines, int XOffset) GetSkipSize(int remainingWidth, int width)
    {
        if (remainingWidth > 0)
            throw new InvalidOperationException("A CLX skip is only valid after a line is complete.");
        if (remainingWidth == 0)
            return (1, 0);
        var overrun = checked(-remainingWidth);
        return (checked(overrun / width + 1), overrun % width);
    }

    private const int MaximumClxFrameBytes = MaximumFrameWidth * MaximumFrameHeight * 4;

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset)
    {
        if (offset < 0 || offset > data.Length - sizeof(uint))
            throw new InvalidDataException("CL2 offset table is truncated.");
        return BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, sizeof(uint)));
    }
}

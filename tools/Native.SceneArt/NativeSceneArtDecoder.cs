using System.Buffers.Binary;

namespace Devilution.Tools.NativeSceneArt;

/** Strict decoder for the original Town and Cathedral CEL/MIN/TIL/SOL source formats. */
public static class NativeSceneArtDecoder
{
    public const int NativePieceCount = 1379;
    public const int MaximumRasterDimension = 8192;
    public const long MaximumRasterPixels = 4L * 1024 * 1024;
    public const int MaximumCelFileBytes = 128 * 1024 * 1024;
    public const int MaximumCelFrames = 4096;
    public const int MaximumSpecialSprites = 4096;
    public const long MaximumSpecialPixels = 16L * 1024 * 1024;
    public const int PaletteByteCount = 256 * 3;
    public const int FrameWidth = 32;
    public const int FrameHeight = 32;
    public const int FloorFoliageFrameOffset = 512;
    public const byte SolidFlag = 1 << 0;
    public const byte BlockMissileFlag = 1 << 2;

    private const ushort FrameMask = 0x0FFF;
    private const ushort TypeMask = 0x7000;
    private const int TrianglePackedBytes = 512;
    private const int TrapezoidPackedBytes = 784;

    public static NativeSceneDecodedFamily DecodeFamily(
        string location,
        ReadOnlyMemory<byte> cel,
        ReadOnlyMemory<byte> min,
        ReadOnlyMemory<byte> til,
        ReadOnlyMemory<byte> sol,
        ReadOnlyMemory<byte> palette)
    {
        if (location is not "town" and not "cathedral")
            throw new ArgumentException("Native scene location must be 'town' or 'cathedral'.", nameof(location));

        ValidateInputLength(cel.Length, MaximumCelFileBytes, "Dungeon CEL");
        var microTileLength = checked((byte)(location == "town" ? 16 : 10));
        var tileProperties = ParseTileProperties(location, sol.Span);
        var celFrames = ParseSingleCelGroup(cel.Span, "Dungeon CEL");
        if (celFrames.Count is < 1 or > MaximumCelFrames)
            throw new InvalidDataException($"Dungeon CEL frame count must be in 1..{MaximumCelFrames}.");

        var pieceCount = min.Length / checked(microTileLength * 2);
        if (pieceCount is < 1 or > NativePieceCount
            || min.Length != checked(pieceCount * microTileLength * 2))
            throw new InvalidDataException($"Native MIN data must contain one through {NativePieceCount} complete pieces.");

        var paletteBytes = palette.ToArray();
        if (paletteBytes.Length != PaletteByteCount)
            throw new InvalidDataException($"Native PAL data must contain exactly {PaletteByteCount} RGB bytes.");

        var rawPieces = ParseMin(min.Span, microTileLength, pieceCount, celFrames.Count);
        var firstUses = FindFirstUses(rawPieces, tileProperties, celFrames.Count);
        var orderedFrameIds = firstUses.Keys.Order().ToArray();
        var adjustedIds = orderedFrameIds
            .Select((sourceFrameId, index) => (sourceFrameId, adjustedFrameId: checked((ushort)(index + 1))))
            .ToDictionary(pair => pair.sourceFrameId, pair => pair.adjustedFrameId);

        var frames = new List<NativeSceneFrame>(orderedFrameIds.Length);
        var frameBytesBySourceId = new Dictionary<ushort, byte[]>(orderedFrameIds.Length);
        foreach (var sourceFrameId in orderedFrameIds) {
            var firstUse = firstUses[sourceFrameId];
            var sourceBytes = celFrames[sourceFrameId - 1];
            var isFloorTransform = firstUse.IsFloor && firstUse.Type == NativeSceneTileType.TransparentSquare;
            var storedType = isFloorTransform
                ? firstUse.SlotIndex == 0 ? NativeSceneTileType.LeftTriangle : NativeSceneTileType.RightTriangle
                : firstUse.Type;
            var storedBytes = ReencodeSourceFrame(sourceBytes, firstUse.Type, isFloorTransform, storedType);
            frameBytesBySourceId.Add(sourceFrameId, storedBytes);
            var storedRaster = DecodeFramePixels(storedBytes, 0, storedType, sourceFrameId, allowTrailingData: true);
            var foliageRaster = isFloorTransform
                ? DecodeTransparentSquare(storedBytes, FloorFoliageFrameOffset, 16, sourceFrameId, allowTrailingData: true, nonzeroOnly: false)
                : null;
            frames.Add(new NativeSceneFrame(
                sourceFrameId,
                adjustedIds[sourceFrameId],
                firstUse.PieceId,
                firstUse.SlotIndex,
                firstUse.Type,
                storedType,
                isFloorTransform,
                storedRaster,
                foliageRaster));
        }

        var pieces = new List<NativeScenePiece>(pieceCount);
        for (var pieceIndex = 0; pieceIndex < pieceCount; pieceIndex++) {
            var properties = tileProperties[pieceIndex];
            var isFloorPiece = IsFloor(properties);
            var slots = new List<NativeSceneSlot>(microTileLength);
            for (byte slotIndex = 0; slotIndex < microTileLength; slotIndex++) {
                var sourceRawWord = rawPieces[pieceIndex][slotIndex];
                if (sourceRawWord == 0) {
                    slots.Add(new NativeSceneSlot(slotIndex, 0, 0, 0, 0,
                        NativeSceneTileType.Square, NativeSceneTileType.Square, NativeSceneTileType.Square,
                        false, false, null, null));
                    continue;
                }

                var sourceFrameId = checked((ushort)(sourceRawWord & FrameMask));
                var sourceType = ReadTileType(sourceRawWord, $"MIN piece {pieceIndex} slot {slotIndex}");
                var adjustedFrameId = adjustedIds[sourceFrameId];
                var adjustment = sourceFrameId - adjustedFrameId;
                var adjustedRawWord = checked((ushort)(sourceRawWord - adjustment));
                var firstUse = firstUses[sourceFrameId];
                var isFloorBase = isFloorPiece && slotIndex < 2;
                var drawType = isFloorBase
                    ? slotIndex == 0 ? NativeSceneTileType.LeftTriangle : NativeSceneTileType.RightTriangle
                    : sourceType;
                var drawsFoliage = isFloorBase && sourceType == NativeSceneTileType.TransparentSquare;
                NativeSceneRaster? floorRaster = null;
                NativeSceneRaster? contentRaster = null;
                if (isFloorBase) {
                    floorRaster = DecodeFramePixels(frameBytesBySourceId[sourceFrameId], 0,
                        slotIndex == 0 ? NativeSceneTileType.LeftTriangle : NativeSceneTileType.RightTriangle,
                        sourceFrameId, allowTrailingData: true);
                    if (drawsFoliage) {
                        contentRaster = DecodeTransparentSquare(frameBytesBySourceId[sourceFrameId],
                            FloorFoliageFrameOffset, 16, sourceFrameId, allowTrailingData: true, nonzeroOnly: false);
                    }
                } else {
                    contentRaster = DecodeFramePixels(frameBytesBySourceId[sourceFrameId], 0, sourceType,
                        sourceFrameId, allowTrailingData: true);
                }

                slots.Add(new NativeSceneSlot(
                    slotIndex,
                    sourceRawWord,
                    adjustedRawWord,
                    sourceFrameId,
                    adjustedFrameId,
                    sourceType,
                    firstUse.Type,
                    drawType,
                    isFloorBase,
                    drawsFoliage,
                    floorRaster,
                    contentRaster));
            }
            pieces.Add(new NativeScenePiece(checked((uint)pieceIndex), properties, isFloorPiece, slots));
        }

        var megaTiles = ParseMegaTiles(til.Span, pieceCount);
        return new NativeSceneDecodedFamily(location, microTileLength, pieces, megaTiles, frames, [], paletteBytes);
    }

    /** Decodes the special CEL sheet used for arches and Town trees, with the native 64-pixel width. */
    public static IReadOnlyList<NativeSceneRaster> DecodeSpecials(ReadOnlyMemory<byte> cel)
    {
        ValidateInputLength(cel.Length, MaximumCelFileBytes, "Special CEL");
        var groups = ParseCelGroups(cel.Span, "Special CEL", MaximumSpecialSprites);
        var frames = groups.SelectMany(group => group).ToArray();
        if (frames.Length is < 1 or > MaximumSpecialSprites)
            throw new InvalidDataException($"Special CEL must contain 1..{MaximumSpecialSprites} frames.");

        long totalPixels = 0;
        var result = new List<NativeSceneRaster>(frames.Length);
        for (var index = 0; index < frames.Length; index++) {
            var raster = DecodeCelRleFrame(frames[index], 64, $"special frame {index}");
            totalPixels = checked(totalPixels + (long)raster.Width * raster.Height);
            if (totalPixels > MaximumSpecialPixels)
                throw new InvalidDataException($"Special CEL exceeds the {MaximumSpecialPixels} decoded-pixel limit.");
            result.Add(raster);
        }
        return result;
    }

    public static bool IsFloor(byte properties)
        => (properties & (SolidFlag | BlockMissileFlag)) == 0;

    private static List<byte[]> ParseSingleCelGroup(ReadOnlySpan<byte> data, string description)
    {
        var groups = ParseCelGroups(data, description);
        if (groups.Count != 1)
            throw new InvalidDataException($"{description} must contain exactly one frame group.");
        return groups[0];
    }

    private static IReadOnlyList<List<byte[]>> ParseCelGroups(
        ReadOnlySpan<byte> data,
        string description,
        int maximumTotalFrameCount = MaximumCelFrames)
    {
        if (data.Length < 8)
            throw new InvalidDataException($"{description} is too short for a CEL header.");

        var first = ReadUInt32(data, 0, description);
        if (first is > 0 and <= MaximumCelFrames) {
            if (checked((int)first) > maximumTotalFrameCount)
                throw new InvalidDataException($"{description} exceeds its {maximumTotalFrameCount}-frame limit.");
            if (TryParseCelGroup(data, 0, data.Length, checked((int)first), description, out var singleGroup))
                return [singleGroup];
        }

        if (first < 4 || first % 4 != 0 || first > 256)
            throw new InvalidDataException($"{description} has an invalid CEL group header.");
        var groupCount = checked((int)(first / 4));
        if (groupCount > maximumTotalFrameCount || checked((int)first) > data.Length)
            throw new InvalidDataException($"{description} has an invalid group count or truncated offset table.");
        var groups = new List<List<byte[]>>(groupCount);
        var cursor = checked((int)first);
        var totalFrames = 0;
        for (var groupIndex = 0; groupIndex < groupCount; groupIndex++) {
            var groupOffset = ReadUInt32(data, groupIndex * sizeof(uint), description);
            if (groupOffset != cursor)
                throw new InvalidDataException($"{description} group {groupIndex} offset does not match its sequential boundary.");
            if (cursor >= data.Length)
                throw new InvalidDataException($"{description} group {groupIndex} is missing.");
            if (cursor + 4 > data.Length)
                throw new InvalidDataException($"{description} group {groupIndex} has a truncated header.");
            var frameCount = ReadUInt32(data, cursor, description);
            if (frameCount is < 1 or > MaximumCelFrames)
                throw new InvalidDataException($"{description} group {groupIndex} has an invalid frame table.");
            if (checked((int)frameCount) > maximumTotalFrameCount - totalFrames)
                throw new InvalidDataException($"{description} exceeds its {maximumTotalFrameCount}-frame limit.");
            totalFrames = checked(totalFrames + (int)frameCount);
            var terminalOffset = checked((int)ReadUInt32(data, cursor + checked((int)(frameCount + 1) * 4), description));
            if (terminalOffset < checked((int)(frameCount + 2) * 4)
                || terminalOffset > data.Length - cursor
                || !TryParseCelGroup(data.Slice(cursor, terminalOffset), 0, terminalOffset,
                    checked((int)frameCount), $"{description} group {groupIndex}", out var group))
                throw new InvalidDataException($"{description} group {groupIndex} has an invalid frame table.");
            cursor = checked(cursor + terminalOffset);
            groups.Add(group);
        }
        if (cursor != data.Length)
            throw new InvalidDataException($"{description} contains trailing bytes after its CEL groups.");
        return groups;
    }

    private static bool TryParseCelGroup(
        ReadOnlySpan<byte> data,
        int groupOffset,
        int groupLength,
        int frameCount,
        string description,
        out List<byte[]> frames)
    {
        frames = [];
        if (frameCount is < 1 or > MaximumCelFrames)
            return false;
        var tableBytes = checked((frameCount + 2) * 4);
        if (tableBytes > groupLength)
            return false;
        var group = data.Slice(groupOffset, groupLength);
        var terminalOffset = ReadUInt32(group, checked((frameCount + 1) * 4), description);
        if (terminalOffset != groupLength)
            return false;

        var offsets = new uint[frameCount + 1];
        var previous = checked((uint)tableBytes);
        for (var frame = 0; frame < frameCount; frame++) {
            var offset = ReadUInt32(group, checked((frame + 1) * 4), description);
            var next = ReadUInt32(group, checked((frame + 2) * 4), description);
            if (offset < previous || next <= offset || next > terminalOffset)
                return false;
            offsets[frame] = offset;
            previous = next;
        }
        offsets[frameCount] = terminalOffset;

        frames = new List<byte[]>(frameCount);
        for (var frame = 0; frame < frameCount; frame++) {
            var start = checked((int)offsets[frame]);
            var length = checked((int)(offsets[frame + 1] - offsets[frame]));
            frames.Add(group.Slice(start, length).ToArray());
        }
        return true;
    }

    private static byte[] ParseTileProperties(string location, ReadOnlySpan<byte> sol)
    {
        if (sol.Length is < 1 or > NativePieceCount)
            throw new InvalidDataException($"Native {location} SOL data must contain 1..{NativePieceCount} bytes.");
        var result = new byte[NativePieceCount];
        sol.CopyTo(result);

        if (location == "cathedral") {
            foreach (var pieceId in new[] { 9, 15, 16, 20, 21, 51, 56, 58, 61, 63, 65, 72, 208, 247, 253, 257, 323, 450 })
                result[pieceId] |= (byte)((1 << 1) | BlockMissileFlag);
            result[27] |= BlockMissileFlag;
            result[28] |= BlockMissileFlag;
            result[403] |= 1 << 1;
            result[24] |= 1 << 1;
        }
        return result;
    }

    private static ushort[][] ParseMin(ReadOnlySpan<byte> min, byte microTileLength, int pieceCount, int celFrameCount)
    {
        var expectedLength = checked(pieceCount * microTileLength * 2);
        if (min.Length != expectedLength)
            throw new InvalidDataException($"Native MIN data length {min.Length} does not match {pieceCount} pieces × {microTileLength} slots.");
        var pieces = new ushort[pieceCount][];
        for (var piece = 0; piece < pieceCount; piece++) {
            var slots = new ushort[microTileLength];
            var pieceOffset = checked(piece * microTileLength * 2);
            for (var slot = 0; slot < microTileLength; slot++) {
                var diskIndex = microTileLength - 2 + (slot & 1) - (slot & 0xE);
                var word = BinaryPrimitives.ReadUInt16LittleEndian(min.Slice(pieceOffset + diskIndex * 2, 2));
                if (word != 0) {
                    _ = ReadTileType(word, $"MIN piece {piece} slot {slot}");
                    var frameId = word & FrameMask;
                    if (frameId == 0 || frameId > celFrameCount)
                        throw new InvalidDataException($"MIN piece {piece} slot {slot} references invalid one-based CEL frame {frameId}.");
                }
                slots[slot] = word;
            }
            pieces[piece] = slots;
        }
        return pieces;
    }

    private static Dictionary<ushort, FirstUse> FindFirstUses(ushort[][] pieces, byte[] properties, int celFrameCount)
    {
        var result = new Dictionary<ushort, FirstUse>();
        for (var pieceIndex = 0; pieceIndex < pieces.Length; pieceIndex++) {
            var isFloor = IsFloor(properties[pieceIndex]);
            for (var slotIndex = 0; slotIndex < pieces[pieceIndex].Length; slotIndex++) {
                var rawWord = pieces[pieceIndex][slotIndex];
                if (rawWord == 0)
                    continue;
                var sourceFrameId = checked((ushort)(rawWord & FrameMask));
                if (sourceFrameId > celFrameCount)
                    throw new InvalidDataException($"MIN piece {pieceIndex} references CEL frame {sourceFrameId}, beyond {celFrameCount} frames.");
                if (!result.ContainsKey(sourceFrameId)) {
                    result.Add(sourceFrameId, new FirstUse(
                        checked((uint)pieceIndex),
                        checked((byte)slotIndex),
                        ReadTileType(rawWord, $"MIN piece {pieceIndex} slot {slotIndex}"),
                        isFloor && slotIndex < 2));
                }
            }
        }
        return result;
    }

    private static IReadOnlyList<NativeSceneMegaTile> ParseMegaTiles(ReadOnlySpan<byte> til, int pieceCount)
    {
        if (til.Length == 0 || til.Length % 8 != 0 || til.Length / 8 > 65_535)
            throw new InvalidDataException("Native TIL data must contain one through 65,535 complete four-piece mega tiles.");
        var count = til.Length / 8;
        var result = new List<NativeSceneMegaTile>(count);
        for (var megaTile = 0; megaTile < count; megaTile++) {
            var pieceIds = new ushort[4];
            for (var part = 0; part < 4; part++) {
                var pieceId = BinaryPrimitives.ReadUInt16LittleEndian(til.Slice(megaTile * 8 + part * 2, 2));
                if (pieceId >= pieceCount)
                    throw new InvalidDataException($"Native TIL mega tile {megaTile} references missing d-piece {pieceId}.");
                pieceIds[part] = pieceId;
            }
            result.Add(new NativeSceneMegaTile(checked((uint)megaTile), pieceIds));
        }
        return result;
    }

    private static byte[] ReencodeSourceFrame(
        ReadOnlySpan<byte> source,
        NativeSceneTileType sourceType,
        bool isFloorTransform,
        NativeSceneTileType storedType)
    {
        if (isFloorTransform) {
            var sourceRaster = DecodeTransparentSquare(source, 0, FrameHeight, 0, allowTrailingData: false, nonzeroOnly: false);
            var pixels = sourceRaster.Indices.ToArray();
            var packedTriangle = PackTriangle(pixels, storedType);
            var foliagePixels = ExtractFoliage(pixels, storedType);
            var foliageBytes = EncodeFoliageRle(foliagePixels);
            return [.. packedTriangle, .. foliageBytes];
        }

        switch (sourceType) {
        case NativeSceneTileType.Square:
            RequireExactSize(source, FrameWidth * FrameHeight, "square");
            return source.ToArray();
        case NativeSceneTileType.TransparentSquare:
            _ = DecodeTransparentSquare(source, 0, FrameHeight, 0, allowTrailingData: false, nonzeroOnly: false);
            return source.ToArray();
        case NativeSceneTileType.LeftTriangle:
            return ReencodeTriangle(source, left: true);
        case NativeSceneTileType.RightTriangle:
            return ReencodeTriangle(source, left: false);
        case NativeSceneTileType.LeftTrapezoid:
            return ReencodeTrapezoid(source, left: true);
        case NativeSceneTileType.RightTrapezoid:
            return ReencodeTrapezoid(source, left: false);
        default:
            throw new InvalidDataException($"Unsupported native source tile type {(byte)sourceType}.");
        }
    }

    private static byte[] ReencodeTriangle(ReadOnlySpan<byte> source, bool left)
    {
        const int rightTriangleConsumedBytes = 542;
        const int canonicalTriangleInputBytes = 544;
        if (left) {
            RequireExactSize(source, canonicalTriangleInputBytes, "left triangle");
        } else if (source.Length is not rightTriangleConsumedBytes and not canonicalTriangleInputBytes) {
            throw new InvalidDataException($"Native right triangle frame has {source.Length} bytes; expected 542 or 544.");
        }
        var output = new byte[TrianglePackedBytes];
        var sourceOffset = 0;
        var outputOffset = 0;
        if (left) {
            var width = 0;
            for (var row = 0; row < 8; row++) {
                sourceOffset += 2;
                width += 2;
                CopySpan(source, ref sourceOffset, output, ref outputOffset, width);
                width += 2;
                CopySpan(source, ref sourceOffset, output, ref outputOffset, width);
            }
            width = FrameWidth;
            for (var row = 0; row < 7; row++) {
                sourceOffset += 2;
                width -= 2;
                CopySpan(source, ref sourceOffset, output, ref outputOffset, width);
                width -= 2;
                CopySpan(source, ref sourceOffset, output, ref outputOffset, width);
            }
            sourceOffset += 2;
            width -= 2;
            CopySpan(source, ref sourceOffset, output, ref outputOffset, width);
        } else {
            var width = 0;
            for (var row = 0; row < 8; row++) {
                width += 2;
                CopySpan(source, ref sourceOffset, output, ref outputOffset, width);
                sourceOffset += 2;
                width += 2;
                CopySpan(source, ref sourceOffset, output, ref outputOffset, width);
            }
            width = FrameWidth;
            for (var row = 0; row < 7; row++) {
                width -= 2;
                CopySpan(source, ref sourceOffset, output, ref outputOffset, width);
                sourceOffset += 2;
                width -= 2;
                CopySpan(source, ref sourceOffset, output, ref outputOffset, width);
            }
            width -= 2;
            CopySpan(source, ref sourceOffset, output, ref outputOffset, width);
        }
        var expectedConsumedBytes = left ? canonicalTriangleInputBytes : rightTriangleConsumedBytes;
        if (outputOffset != output.Length || sourceOffset != expectedConsumedBytes)
            throw new InvalidDataException("Native triangle frame does not match its padded row encoding.");
        return output;
    }

    private static byte[] ReencodeTrapezoid(ReadOnlySpan<byte> source, bool left)
    {
        const int lowerTriangleInputSize = 288;
        RequireExactSize(source, lowerTriangleInputSize + FrameWidth * 16,
            left ? "left trapezoid" : "right trapezoid");
        var output = new byte[TrapezoidPackedBytes];
        var sourceOffset = 0;
        var outputOffset = 0;
        if (left) {
            var width = 0;
            for (var row = 0; row < 8; row++) {
                sourceOffset += 2;
                width += 2;
                CopySpan(source, ref sourceOffset, output, ref outputOffset, width);
                width += 2;
                CopySpan(source, ref sourceOffset, output, ref outputOffset, width);
            }
        } else {
            var width = 0;
            for (var row = 0; row < 8; row++) {
                width += 2;
                CopySpan(source, ref sourceOffset, output, ref outputOffset, width);
                sourceOffset += 2;
                width += 2;
                CopySpan(source, ref sourceOffset, output, ref outputOffset, width);
            }
        }
        CopySpan(source, ref sourceOffset, output, ref outputOffset, FrameWidth * 16);
        if (outputOffset != output.Length || sourceOffset != source.Length)
            throw new InvalidDataException("Native trapezoid frame does not match its padded row encoding.");
        return output;
    }

    private static NativeSceneRaster DecodeFramePixels(
        ReadOnlySpan<byte> data,
        int offset,
        NativeSceneTileType type,
        ushort frameId,
        bool allowTrailingData)
    {
        if (offset < 0 || offset > data.Length)
            throw new InvalidDataException($"Native CEL frame {frameId} has an invalid pixel offset.");
        var source = data[offset..];
        var indices = new byte[FrameWidth * FrameHeight];
        var opacity = new byte[indices.Length];
        switch (type) {
        case NativeSceneTileType.Square:
            RequireMinimumSize(source, indices.Length, $"square frame {frameId}", allowTrailingData);
            for (var bottomRow = 0; bottomRow < FrameHeight; bottomRow++) {
                var topDownRow = FrameHeight - 1 - bottomRow;
                var src = bottomRow * FrameWidth;
                var dst = topDownRow * FrameWidth;
                source.Slice(src, FrameWidth).CopyTo(indices.AsSpan(dst, FrameWidth));
                opacity.AsSpan(dst, FrameWidth).Fill(byte.MaxValue);
            }
            break;
        case NativeSceneTileType.TransparentSquare:
            return DecodeTransparentSquare(source, 0, FrameHeight, frameId, allowTrailingData, nonzeroOnly: false);
        case NativeSceneTileType.LeftTriangle:
        case NativeSceneTileType.RightTriangle:
            RequireMinimumSize(source, TrianglePackedBytes, $"triangle frame {frameId}", allowTrailingData);
            DecodePackedTriangle(source[..TrianglePackedBytes], type == NativeSceneTileType.LeftTriangle, indices, opacity);
            break;
        case NativeSceneTileType.LeftTrapezoid:
        case NativeSceneTileType.RightTrapezoid:
            RequireMinimumSize(source, TrapezoidPackedBytes, $"trapezoid frame {frameId}", allowTrailingData);
            DecodePackedTrapezoid(source[..TrapezoidPackedBytes], type == NativeSceneTileType.LeftTrapezoid, indices, opacity);
            break;
        default:
            throw new InvalidDataException($"Native CEL frame {frameId} uses unsupported tile type {(byte)type}.");
        }
        var raster = new NativeSceneRaster(FrameWidth, FrameHeight, indices, opacity);
        raster.Validate($"Native CEL frame {frameId}");
        return raster;
    }

    private static NativeSceneRaster DecodeTransparentSquare(
        ReadOnlySpan<byte> data,
        int offset,
        int height,
        ushort frameId,
        bool allowTrailingData,
        bool nonzeroOnly)
    {
        if (offset < 0 || offset > data.Length || height is < 1 or > FrameHeight)
            throw new InvalidDataException($"Native transparent-square frame {frameId} has invalid dimensions or offset.");
        var indices = new byte[FrameWidth * height];
        var opacity = new byte[indices.Length];
        var source = data[offset..];
        var cursor = 0;
        for (var bottomRow = 0; bottomRow < height; bottomRow++) {
            var x = 0;
            var targetRow = (height - 1 - bottomRow) * FrameWidth;
            while (x < FrameWidth) {
                if (cursor >= source.Length)
                    throw new InvalidDataException($"Native transparent-square frame {frameId} ends inside row {bottomRow}.");
                var control = unchecked((sbyte)source[cursor++]);
                if (control == 0)
                    throw new InvalidDataException($"Native transparent-square frame {frameId} contains a zero-length run.");
                var runLength = Math.Abs((int)control);
                if (runLength > FrameWidth - x)
                    throw new InvalidDataException($"Native transparent-square frame {frameId} run exceeds row {bottomRow}.");
                if (control > 0) {
                    if (cursor + runLength > source.Length)
                        throw new InvalidDataException($"Native transparent-square frame {frameId} ends inside an opaque run.");
                    source.Slice(cursor, runLength).CopyTo(indices.AsSpan(targetRow + x, runLength));
                    if (nonzeroOnly) {
                        for (var pixel = 0; pixel < runLength; pixel++) {
                            if (source[cursor + pixel] != 0)
                                opacity[targetRow + x + pixel] = byte.MaxValue;
                        }
                    } else {
                        opacity.AsSpan(targetRow + x, runLength).Fill(byte.MaxValue);
                    }
                    cursor += runLength;
                }
                x += runLength;
            }
        }
        if (!allowTrailingData && source.Length - cursor > 3)
            throw new InvalidDataException($"Native transparent-square frame {frameId} contains more than three alignment bytes.");
        var raster = new NativeSceneRaster(FrameWidth, height, indices, opacity);
        raster.Validate($"Native transparent-square frame {frameId}");
        return raster;
    }

    private static NativeSceneRaster DecodeCelRleFrame(ReadOnlySpan<byte> frame, int width, string description)
    {
        var source = frame;
        if (source.Length >= 10 && BinaryPrimitives.ReadUInt16LittleEndian(source) == 10)
            source = source[10..];
        var rows = new List<byte[]>();
        var opacityRows = new List<byte[]>();
        var cursor = 0;
        while (cursor < source.Length) {
            if (rows.Count >= MaximumRasterDimension)
                throw new InvalidDataException($"{description} exceeds the maximum CEL frame height.");
            var row = new byte[width];
            var rowOpacity = new byte[width];
            var x = 0;
            while (x < width) {
                if (cursor >= source.Length)
                    throw new InvalidDataException($"{description} ends inside an RLE row.");
                var control = source[cursor++];
                if (control == 0)
                    throw new InvalidDataException($"{description} contains a zero-length RLE run.");
                var run = control >= 0x80 ? 256 - control : control;
                if (run > width - x)
                    throw new InvalidDataException($"{description} RLE run exceeds row width {width}.");
                if (control < 0x80) {
                    if (cursor + run > source.Length)
                        throw new InvalidDataException($"{description} ends inside an opaque RLE run.");
                    source.Slice(cursor, run).CopyTo(row.AsSpan(x, run));
                    rowOpacity.AsSpan(x, run).Fill(byte.MaxValue);
                    cursor += run;
                }
                x += run;
            }
            rows.Add(row);
            opacityRows.Add(rowOpacity);
        }
        if (rows.Count == 0)
            throw new InvalidDataException($"{description} has no CEL rows.");
        var pixelCount = checked((long)width * rows.Count);
        if (width > MaximumRasterDimension || pixelCount > MaximumRasterPixels)
            throw new InvalidDataException($"{description} exceeds the decoded raster limit.");
        var indices = new byte[checked(width * rows.Count)];
        var opacity = new byte[indices.Length];
        for (var sourceRow = 0; sourceRow < rows.Count; sourceRow++) {
            var targetY = rows.Count - 1 - sourceRow;
            rows[sourceRow].CopyTo(indices, targetY * width);
            opacityRows[sourceRow].CopyTo(opacity, targetY * width);
        }
        var raster = new NativeSceneRaster(width, rows.Count, indices, opacity);
        raster.Validate(description);
        return raster;
    }

    private static byte[] PackTriangle(byte[] surfaceTopDown, NativeSceneTileType type)
    {
        var left = type == NativeSceneTileType.LeftTriangle;
        var output = new byte[TrianglePackedBytes];
        var cursor = 0;
        for (var bottomRow = 0; bottomRow < 31; bottomRow++) {
            var width = bottomRow < 16 ? 2 + bottomRow * 2 : 32 - (bottomRow - 15) * 2;
            var x = left ? FrameWidth - width : 0;
            var topDownY = FrameHeight - 1 - bottomRow;
            surfaceTopDown.AsSpan(topDownY * FrameWidth + x, width).CopyTo(output.AsSpan(cursor, width));
            cursor += width;
        }
        if (cursor != output.Length)
            throw new InvalidDataException("Generated floor triangle has an invalid packed size.");
        return output;
    }

    private static byte[] ExtractFoliage(byte[] surfaceTopDown, NativeSceneTileType type)
    {
        var left = type == NativeSceneTileType.LeftTriangle;
        var result = surfaceTopDown.ToArray();
        for (var bottomRow = 0; bottomRow < 31; bottomRow++) {
            var width = bottomRow < 16 ? 2 + bottomRow * 2 : 32 - (bottomRow - 15) * 2;
            var x = left ? FrameWidth - width : 0;
            var y = FrameHeight - 1 - bottomRow;
            result.AsSpan(y * FrameWidth + x, width).Clear();
        }
        var foliage = new byte[FrameWidth * 16];
        for (var y = 0; y < 16; y++)
            result.AsSpan(y * FrameWidth, FrameWidth).CopyTo(foliage.AsSpan(y * FrameWidth, FrameWidth));
        return foliage;
    }

    private static byte[] EncodeFoliageRle(byte[] foliageTopDown)
    {
        var result = new List<byte>(FrameWidth * 16);
        for (var y = 15; y >= 0; y--) {
            var x = 0;
            while (x < FrameWidth) {
                var isOpaque = foliageTopDown[y * FrameWidth + x] != 0;
                var runStart = x;
                while (x < FrameWidth && (foliageTopDown[y * FrameWidth + x] != 0) == isOpaque)
                    x++;
                var runLength = x - runStart;
                result.Add(isOpaque ? checked((byte)runLength) : unchecked((byte)-runLength));
                if (isOpaque) {
                    var terminatedByTransparentPixel = x < FrameWidth;
                    var literalStart = terminatedByTransparentPixel ? runStart + 1 : runStart;
                    var literalEnd = terminatedByTransparentPixel ? x + 1 : x;
                    for (var pixel = literalStart; pixel < literalEnd; pixel++)
                        result.Add(foliageTopDown[y * FrameWidth + pixel]);
                }
            }
        }
        return [.. result];
    }

    private static void DecodePackedTriangle(ReadOnlySpan<byte> packed, bool left, byte[] indices, byte[] opacity)
    {
        var cursor = 0;
        for (var bottomRow = 0; bottomRow < 31; bottomRow++) {
            var width = bottomRow < 16 ? 2 + bottomRow * 2 : 32 - (bottomRow - 15) * 2;
            var x = left ? FrameWidth - width : 0;
            var y = FrameHeight - 1 - bottomRow;
            packed.Slice(cursor, width).CopyTo(indices.AsSpan(y * FrameWidth + x, width));
            opacity.AsSpan(y * FrameWidth + x, width).Fill(byte.MaxValue);
            cursor += width;
        }
        if (cursor != packed.Length)
            throw new InvalidDataException("Packed triangle frame length does not match its row geometry.");
    }

    private static void DecodePackedTrapezoid(ReadOnlySpan<byte> packed, bool left, byte[] indices, byte[] opacity)
    {
        const int lowerTriangleLength = 272;
        var cursor = 0;
        for (var bottomRow = 0; bottomRow < 16; bottomRow++) {
            var width = 2 + bottomRow * 2;
            var x = left ? FrameWidth - width : 0;
            var y = FrameHeight - 1 - bottomRow;
            packed.Slice(cursor, width).CopyTo(indices.AsSpan(y * FrameWidth + x, width));
            opacity.AsSpan(y * FrameWidth + x, width).Fill(byte.MaxValue);
            cursor += width;
        }
        if (cursor != lowerTriangleLength)
            throw new InvalidDataException("Packed trapezoid lower triangle length is invalid.");
        // The native trapezoid's top half is a full-width 32x16 rectangle after its packed lower triangle.
        for (var bottomRow = 16; bottomRow < 32; bottomRow++) {
            var y = FrameHeight - 1 - bottomRow;
            packed.Slice(cursor, FrameWidth).CopyTo(indices.AsSpan(y * FrameWidth, FrameWidth));
            opacity.AsSpan(y * FrameWidth, FrameWidth).Fill(byte.MaxValue);
            cursor += FrameWidth;
        }
        if (cursor != packed.Length)
            throw new InvalidDataException("Packed trapezoid frame length does not match its row geometry.");
    }

    private static NativeSceneTileType ReadTileType(ushort rawWord, string description)
    {
        var rawType = (rawWord & TypeMask) >> 12;
        if (rawType > (ushort)NativeSceneTileType.RightTrapezoid)
            throw new InvalidDataException($"{description} has unsupported tile type {rawType}.");
        return (NativeSceneTileType)rawType;
    }

    private static void CopySpan(ReadOnlySpan<byte> source, ref int sourceOffset, Span<byte> target, ref int targetOffset, int length)
    {
        if (length < 0 || sourceOffset < 0 || targetOffset < 0
            || sourceOffset + length > source.Length || targetOffset + length > target.Length)
            throw new InvalidDataException("Native padded tile frame is truncated.");
        source.Slice(sourceOffset, length).CopyTo(target[targetOffset..]);
        sourceOffset += length;
        targetOffset += length;
    }

    private static void RequireExactSize(ReadOnlySpan<byte> data, int expected, string description)
    {
        if (data.Length != expected)
            throw new InvalidDataException($"Native {description} frame has {data.Length} bytes; expected {expected}.");
    }

    private static void RequireMinimumSize(ReadOnlySpan<byte> data, int expected, string description, bool allowTrailingData)
    {
        if (allowTrailingData ? data.Length < expected : data.Length != expected)
            throw new InvalidDataException($"Native {description} frame is truncated or has unexpected trailing bytes.");
    }

    private static void ValidateInputLength(int actual, int maximum, string description)
    {
        if (actual is < 1 || actual > maximum)
            throw new InvalidDataException($"{description} input is outside 1..{maximum} bytes.");
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset, string description)
    {
        if (offset < 0 || offset + 4 > data.Length)
            throw new InvalidDataException($"{description} has a truncated 32-bit field.");
        return BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));
    }

    private sealed record FirstUse(uint PieceId, byte SlotIndex, NativeSceneTileType Type, bool IsFloor);
}

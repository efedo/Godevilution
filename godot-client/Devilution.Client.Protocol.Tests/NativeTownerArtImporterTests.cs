using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Devilution.Tools.NativePlayerArt;
using Devilution.Tools.NativeTownerArt;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeTownerArtImporterTests
{
    [Fact]
    public void ReadsExactlyTheEightBaseTownHumansAndThreeCowInstances()
    {
        var roster = NativeTownerArtCompiler.ReadBaseTownRoster(Path.Combine(FindRepositoryRoot(), "assets", "txtdata"));
        var actual = roster.Select(row => (
            row.ResidentSlot, row.TownerId, row.PositionX, row.PositionY, row.NativeDirection, row.SpriteId));
        var expected = new[] {
            (0, 0, 62, 63, 1, 0),
            (1, 1, 55, 79, 7, 1),
            (2, 3, 55, 62, 1, 2),
            (3, 4, 62, 71, 0, 3),
            (4, 5, 71, 84, 0, 4),
            (5, 6, 80, 20, 0, 5),
            (6, 7, 43, 66, 0, 6),
            (7, 8, 11, 53, 0, 7),
            (8, 9, 58, 16, 1, 8),
            (9, 9, 56, 14, 3, 8),
            (10, 9, 59, 20, 4, 8),
        };

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ReadsCanonicalAnimationControlsAndRejectsEditedTownerTable()
    {
        var repositoryTextData = Path.Combine(FindRepositoryRoot(), "assets", "txtdata");
        var metadata = NativeTownerArtCompiler.ReadBaseTownSpriteMetadata(repositoryTextData);
        Assert.Equal(new[] {
            (0, "towners/smith/smithn.cel", 96, 16, 3),
            (1, "towners/healer/healer.cel", 96, 20, 6),
            (2, "towners/twnf/twnfn.cel", 96, 16, 3),
            (3, "towners/strytell/strytell.cel", 96, 25, 3),
            (4, "towners/drunk/twndrunk.cel", 96, 18, 3),
            (5, "towners/townwmn1/witch.cel", 96, 19, 6),
            (6, "towners/townwmn1/wmnn.cel", 96, 18, 6),
            (7, "towners/townboy/pegkid1.cel", 96, 20, 6),
            (8, "towners/animals/cow.cel", 128, 12, 3),
        }, metadata.Select(row => (row.SpriteId, row.SourcePath, row.AlignmentWidth, row.AnimationFrames, row.TicksPerFrame)));
        Assert.All(metadata.Take(6), sprite => Assert.NotEmpty(sprite.AnimationOrder));
        Assert.All(metadata.Skip(6), sprite => Assert.Empty(sprite.AnimationOrder));
        Assert.All(metadata.SelectMany(sprite => sprite.AnimationOrder), frame => Assert.InRange(frame, 0, 24));

        var temporaryRoot = Path.Combine(Path.GetTempPath(), "godevilution-native-towner-metadata-" + Guid.NewGuid().ToString("N"));
        var alteredTable = File.ReadAllText(Path.Combine(repositoryTextData, "towners", "towners.tsv"), new UTF8Encoding(false))
            .Replace("TOWN_SMITH", "TOWN_WITCH", StringComparison.Ordinal);
        var alteredPath = Path.Combine(temporaryRoot, "towners", "towners.tsv");
        Directory.CreateDirectory(Path.GetDirectoryName(alteredPath)!);
        File.WriteAllText(alteredPath, alteredTable, new UTF8Encoding(false));
        try {
            Assert.Throws<InvalidDataException>(() => NativeTownerArtCompiler.ReadBaseTownSpriteMetadata(temporaryRoot));
        } finally {
            if (Directory.Exists(temporaryRoot)) {
                Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetDirectoryName(Path.GetFullPath(temporaryRoot)), StringComparer.OrdinalIgnoreCase);
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void DecodesCelRowsBottomUpAndKeepsOpaquePaletteZeroSeparateFromTransparency()
    {
        // Native CEL rows are bottom-up: bottom [opaque 0, opaque 5, transparent, transparent],
        // then top [transparent, opaque 7, opaque 8, opaque 9].
        var cel = BuildSingleGroup([[2, 0, 5, 0xFE, 0xFF, 3, 7, 8, 9]]);
        var frame = Assert.Single(Assert.Single(NativeTownerCelDecoder.Decode(cel, 4, 1, 1).Directions));

        Assert.Equal(4, frame.Width);
        Assert.Equal(2, frame.Height);
        Assert.Equal(new byte[] { 0, 7, 8, 9, 0, 5, 0, 0 }, frame.Indices);
        Assert.Equal(new byte[] { 0, 255, 255, 255, 255, 255, 0, 0 }, frame.Opacity);
    }

    [Fact]
    public void ParsesAllEightCowGroupsAndRejectsMismatchedGroupOffsets()
    {
        var sourceFrame = new byte[] { 2, 0, 1 };
        var groups = Enumerable.Range(0, 8).Select(_ => new[] { sourceFrame }).ToArray();
        var cel = BuildGroups(groups);
        var sheet = NativeTownerCelDecoder.Decode(cel, 2, 8, 1);

        Assert.Equal(8, sheet.Directions.Count);
        for (var direction = 0; direction < 8; direction++) {
            var frame = Assert.Single(sheet.Directions[direction]);
            Assert.Equal(new byte[] { 0, 1 }, frame.Indices);
            Assert.Equal(new byte[] { 255, 255 }, frame.Opacity);
        }

        var malformed = cel.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(malformed.AsSpan(sizeof(uint), sizeof(uint)), uint.MaxValue);
        Assert.Throws<InvalidDataException>(() => NativeTownerCelDecoder.Decode(malformed, 2, 8, 1));
        Assert.Throws<InvalidDataException>(() => NativeTownerCelDecoder.Decode(cel, 2, 1, 1));
    }

    [Fact]
    public void RejectsZeroLengthRunsTruncatedPayloadsAndRunsPastDeclaredWidth()
    {
        foreach (var frame in new[] {
                     new byte[] { 0, 1, 2 },
                     new byte[] { 2, 1 },
                     new byte[] { 3, 1, 2, 3 },
                     new byte[] { 0xFD },
                 }) {
            var cel = BuildSingleGroup([frame]);
            Assert.Throws<InvalidDataException>(() => NativeTownerCelDecoder.Decode(cel, 2, 1, 1));
        }
    }

    [Fact]
    public void EncodesDeterministicBottomAlignedAtlasAndTransparentPadding()
    {
        var shortFrame = new NativeTownerCelFrame(2, 1, [0, 5], [255, 0]);
        var tallFrame = new NativeTownerCelFrame(2, 2, [1, 2, 3, 4], [255, 255, 255, 255]);
        var sheet = new NativeTownerCelSheet([[shortFrame, tallFrame]]);
        var paletteBytes = new byte[768];
        for (var color = 0; color < 256; color++) {
            paletteBytes[color * 3] = checked((byte)color);
            paletteBytes[color * 3 + 1] = checked((byte)(255 - color));
            paletteBytes[color * 3 + 2] = checked((byte)(color / 2));
        }
        var palette = NativePlayerPalette.Parse(paletteBytes, "synthetic Town palette");

        var first = NativeTownerArtPng.EncodeAtlas(sheet, palette);
        var second = NativeTownerArtPng.EncodeAtlas(sheet, palette);
        Assert.Equal(first.Png, second.Png);
        var decoded = DecodePng(first.Png);
        Assert.Equal((4, 2), (decoded.Width, decoded.Height));
        Assert.Equal(new byte[] {
            0, 0, 0, 0, 0, 0, 0, 0,
            1, 254, 0, 255, 2, 253, 1, 255,
            0, 255, 0, 255, 0, 0, 0, 0,
            3, 252, 1, 255, 4, 251, 2, 255,
        }, decoded.Rgba);
    }

    [Fact]
    public void EnforcesAggregateAtlasPixelLimitBeforePublishingEachAtlas()
    {
        const int perAtlasPixels = 4 * 1024 * 1024;
        var total = 0L;
        for (var atlas = 0; atlas < 4; atlas++)
            total = NativeTownerArtCompiler.AddAtlasPixelsToAggregate(total, perAtlasPixels, 1);
        Assert.Equal(NativeTownerArtPng.MaximumAggregateAtlasPixels, total);
        Assert.Throws<InvalidDataException>(() => NativeTownerArtCompiler.AddAtlasPixelsToAggregate(total, 1, 1));
        Assert.Throws<InvalidDataException>(() => NativeTownerArtCompiler.AddAtlasPixelsToAggregate(-1, 1, 1));
    }

    [Fact(Explicit = true)]
    public void RejectsAggregateAtlasPixelLimitAndRemovesStagingOutput()
    {
        const string profileRootVariable = "GODEVILUTION_NATIVE_TOWN_PROFILE_ROOT";
        var profileRoot = Environment.GetEnvironmentVariable(profileRootVariable);
        Assert.False(string.IsNullOrWhiteSpace(profileRoot), $"Set {profileRootVariable} to a verified format-5 profile root.");
        var profileDirectory = Path.Combine(profileRoot!, "warrior");
        Assert.True(Directory.Exists(profileDirectory), $"The verified Warrior profile does not exist: {profileDirectory}");

        var repositoryRoot = FindRepositoryRoot();
        var textDataRoot = Path.Combine(repositoryRoot, "assets", "txtdata");
        var sprites = NativeTownerArtCompiler.ReadBaseTownSpriteMetadata(textDataRoot);
        var temporaryRoot = Path.Combine(Path.GetTempPath(), "godevilution-native-towner-pixel-limit-" + Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(temporaryRoot, "source");
        var outputRoot = Path.Combine(temporaryRoot, "published");
        try {
            Directory.CreateDirectory(sourceRoot);
            var palettePath = Path.Combine(sourceRoot, NativeTownerArtCompiler.TownPalettePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(palettePath)!);
            File.WriteAllBytes(palettePath, new byte[NativePlayerPalette.ColorCount * NativePlayerPalette.BytesPerColor]);

            foreach (var sprite in sprites) {
                var sourcePath = Path.Combine(sourceRoot, sprite.SourcePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
                if (sprite.SpriteId == 8) {
                    var groups = Enumerable.Range(0, 8)
                        .Select(_ => Enumerable.Range(0, sprite.AnimationFrames)
                            .Select(_ => BuildTransparentFrame(sprite.AlignmentWidth, 320)).ToArray())
                        .ToArray();
                    File.WriteAllBytes(sourcePath, BuildGroups(groups));
                } else {
                    var frames = Enumerable.Range(0, sprite.AnimationFrames)
                        .Select(_ => BuildTransparentFrame(sprite.AlignmentWidth, 1024)).ToArray();
                    File.WriteAllBytes(sourcePath, BuildSingleGroup(frames));
                }
            }

            var exception = Assert.Throws<InvalidDataException>(() => NativeTownerArtCompiler.Import(new NativeTownerImportOptions(
                sourceRoot,
                textDataRoot,
                [profileDirectory],
                outputRoot)));
            Assert.Contains("pixel family limit", exception.Message, StringComparison.Ordinal);
            Assert.False(Directory.Exists(outputRoot), "The rejected atlas family must not be published.");
            Assert.DoesNotContain(Directory.EnumerateDirectories(temporaryRoot),
                path => Path.GetFileName(path).StartsWith(".published.staging-", StringComparison.Ordinal));
        } finally {
            if (Directory.Exists(temporaryRoot)) {
                Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetDirectoryName(Path.GetFullPath(temporaryRoot)), StringComparer.OrdinalIgnoreCase);
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static byte[] BuildSingleGroup(IReadOnlyList<byte[]> frames)
        => BuildGroup(frames);

    private static byte[] BuildTransparentFrame(int width, int height)
    {
        var row = new byte[height];
        Array.Fill(row, checked((byte)(256 - width)));
        return row;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) {
            if (File.Exists(Path.Combine(directory.FullName, "assets", "txtdata", "towners", "towners.tsv")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate the Godevilution repository root for the native Town fixture.");
    }

    private static byte[] BuildGroups(IReadOnlyList<byte[][]> groups)
    {
        var directoryBytes = checked(groups.Count * sizeof(uint));
        var groupData = groups.Select(BuildGroup).ToArray();
        var totalBytes = checked(directoryBytes + groupData.Sum(group => group.Length));
        var result = new byte[totalBytes];
        var cursor = directoryBytes;
        for (var group = 0; group < groupData.Length; group++) {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(group * sizeof(uint), sizeof(uint)), checked((uint)cursor));
            groupData[group].CopyTo(result, cursor);
            cursor += groupData[group].Length;
        }
        return result;
    }

    private static byte[] BuildGroup(IReadOnlyList<byte[]> frames)
    {
        var headerBytes = checked((frames.Count + 2) * sizeof(uint));
        var totalBytes = checked(headerBytes + frames.Sum(frame => frame.Length));
        var result = new byte[totalBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(result, checked((uint)frames.Count));
        var cursor = headerBytes;
        for (var frame = 0; frame < frames.Count; frame++) {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan((frame + 1) * sizeof(uint), sizeof(uint)), checked((uint)cursor));
            frames[frame].CopyTo(result, cursor);
            cursor += frames[frame].Length;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan((frames.Count + 1) * sizeof(uint), sizeof(uint)), checked((uint)cursor));
        return result;
    }

    private static (int Width, int Height, byte[] Rgba) DecodePng(byte[] png)
    {
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        var width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4)));
        var height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4)));
        using var compressed = new MemoryStream();
        var cursor = 8;
        while (cursor < png.Length) {
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(cursor, 4)));
            var type = System.Text.Encoding.ASCII.GetString(png, cursor + 4, 4);
            if (type == "IDAT")
                compressed.Write(png, cursor + 8, length);
            cursor += checked(12 + length);
            if (type == "IEND")
                break;
        }
        compressed.Position = 0;
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        var filtered = raw.ToArray();
        var rowBytes = checked(width * 4);
        Assert.Equal(checked(height * (rowBytes + 1)), filtered.Length);
        var rgba = new byte[checked(width * height * 4)];
        for (var row = 0; row < height; row++) {
            Assert.Equal((byte)0, filtered[row * (rowBytes + 1)]);
            filtered.AsSpan(row * (rowBytes + 1) + 1, rowBytes).CopyTo(rgba.AsSpan(row * rowBytes, rowBytes));
        }
        return (width, height, rgba);
    }
}

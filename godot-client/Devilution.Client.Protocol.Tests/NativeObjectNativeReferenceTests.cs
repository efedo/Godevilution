using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Devilution.Client.PresentationAssets;
using Devilution.Tools.NativeObjectArt;
using Devilution.Tools.NativePlayerArt;
using Devilution.Tools.NativeTownerArt;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

/** Explicit source/atlas qualification against native CEL loading and raster output. */
public sealed class NativeObjectNativeReferenceTests
{
    private const string ReferenceVariable = "GODEVILUTION_NATIVE_OBJECT_ART_REFERENCE_DIRECTORY";
    private const string PackVariable = "GODEVILUTION_NATIVE_OBJECT_ART_PACK_DIRECTORY";
    private const int MaximumReferenceBytes = 256 * 1024 * 1024;
    private static readonly (string Stem, int Width, int MinimumFrames)[] ExpectedSources = [
        ("l1doors", 64, 4), ("chest1", 96, 6), ("chest2", 96, 6), ("chest3", 96, 6),
        ("sarc", 128, 5), ("traphole", 64, 2), ("barrel", 96, 9), ("barrelex", 96, 10),
    ];

    [Fact(Explicit = true)]
    public void MatchesEveryNativeSourceFrameIndexOpacityAndActualWidth()
    {
        var referenceRoot = RequireDirectory(ReferenceVariable);
        var packRoot = RequireDirectory(PackVariable);
        var reference = ReadReference(referenceRoot);
        var catalog = ReadCatalog(packRoot);
        var compared = 0;
        foreach (var expected in ExpectedSources) {
            var native = reference[expected.Stem];
            var sourcePath = $"objects/{expected.Stem}.cel";
            var sprite = Assert.Single(catalog.Sprites, row => row.SourcePath == sourcePath);
            var bytes = ReadBoundedFile(SafeFile(Path.Combine(referenceRoot, "source"), sourcePath),
                NativeObjectArtCompiler.MaximumSourceBytes);
            Assert.Equal(Assert.Single(catalog.SourceHashes, row => row.Path == sourcePath).Sha256, Sha256(bytes));
            Assert.Equal(expected.Width, sprite.NativeWidth);
            Assert.Equal(native.Count, sprite.SourceFrameCount);
            Assert.InRange(native.Count, expected.MinimumFrames, NativeTownerCelDecoder.MaximumFramesPerGroup);
            var decoded = Assert.Single(NativeTownerCelDecoder.Decode(bytes, expected.Width, 1, 0).Directions);
            Assert.Equal(native.Count, decoded.Count);
            for (var index = 0; index < native.Count; index++) {
                var actual = decoded[index];
                var frame = native[index];
                Assert.Equal(index + 1, frame.NativeFrame);
                Assert.Equal(expected.Width, frame.NativeWidth);
                Assert.Equal((frame.Width, frame.Height), (actual.Width, actual.Height));
                var pairs = new byte[checked(actual.Indices.Length * 2)];
                for (var pixel = 0; pixel < actual.Indices.Length; pixel++) {
                    pairs[pixel * 2] = actual.Indices[pixel];
                    pairs[pixel * 2 + 1] = actual.Opacity[pixel];
                }
                Assert.Equal(frame.IndexOpacity, pairs);
                compared++;
            }
        }
        Assert.Equal(reference.Values.Sum(frames => frames.Count), compared);
    }

    [Fact(Explicit = true)]
    public void MatchesEveryPublishedAtlasPixelPaddingHashAndSourceBinding()
    {
        var referenceRoot = RequireDirectory(ReferenceVariable);
        var packRoot = RequireDirectory(PackVariable);
        var reference = ReadReference(referenceRoot);
        var catalog = ReadCatalog(packRoot);
        var paletteBytes = ReadBoundedFile(SafeFile(Path.Combine(referenceRoot, "source"),
            NativeObjectArtCompiler.CathedralPalettePath), 768);
        var palette = NativePlayerPalette.Parse(paletteBytes, "native L1 object reference palette");
        Assert.Equal(Assert.Single(catalog.SourceHashes,
            row => row.Path == NativeObjectArtCompiler.CathedralPalettePath).Sha256, Sha256(paletteBytes));
        Assert.Equal(1, catalog.SchemaVersion);
        Assert.Equal(8, catalog.Sprites.Count);
        Assert.Equal(8, catalog.Atlases.Count);
        Assert.Equal(new uint[] { 1, 2, 5, 6, 7, 48, 53, 54, 57, 58 },
            catalog.Objects.Select(row => row.NativeObjectId).Order());
        Assert.NotEmpty(catalog.ProfileBindings);
        Assert.All(catalog.ProfileBindings, binding => {
            Assert.Equal(1U, binding.LevelId);
            Assert.Equal("cathedral", binding.Location);
        });
        foreach (var sprite in catalog.Sprites) {
            var stem = Path.GetFileNameWithoutExtension(sprite.SourcePath);
            var native = reference[stem];
            Assert.Equal(native.Count, sprite.SourceFrameCount);
            Assert.Equal(Enumerable.Range(1, native.Count), sprite.Frames.Select(frame => frame.NativeFrame).Order());
            var atlas = Assert.Single(catalog.Atlases, row => row.Id == sprite.Frames[0].AtlasId);
            Assert.All(sprite.Frames, frame => Assert.Equal(atlas.Id, frame.AtlasId));
            var expected = new byte[checked(atlas.Width * atlas.Height * 4)];
            var occupied = new bool[checked(atlas.Width * atlas.Height)];
            foreach (var region in sprite.Frames) {
                var frame = native[region.NativeFrame - 1];
                Assert.Equal((frame.Width, frame.Height), (region.Width, region.Height));
                Assert.Equal(sprite.NativeWidth, frame.NativeWidth);
                Assert.InRange(region.X, 0, atlas.Width - region.Width);
                Assert.InRange(region.Y, 0, atlas.Height - region.Height);
                for (var y = 0; y < frame.Height; y++) {
                    for (var x = 0; x < frame.Width; x++) {
                        var sourcePixel = y * frame.Width + x;
                        var targetPixel = (region.Y + y) * atlas.Width + region.X + x;
                        Assert.False(occupied[targetPixel]);
                        occupied[targetPixel] = true;
                        if (frame.IndexOpacity[sourcePixel * 2 + 1] == 0)
                            continue;
                        var paletteIndex = frame.IndexOpacity[sourcePixel * 2];
                        palette.Rgb.AsSpan(paletteIndex * 3, 3).CopyTo(expected.AsSpan(targetPixel * 4, 3));
                        expected[targetPixel * 4 + 3] = byte.MaxValue;
                    }
                }
            }
            var png = ReadBoundedFile(SafeFile(packRoot, atlas.Path), NativeObjectArtCompiler.MaximumAtlasFileBytes);
            Assert.Equal(atlas.Sha256, Sha256(png));
            var decoded = NativeArtReferencePng.Read(png, NativeObjectCatalog.MaximumAtlasDimension, NativeObjectCatalog.MaximumAtlasPixels, atlas.Path);
            Assert.Equal((atlas.Width, atlas.Height), (decoded.Width, decoded.Height));
            Assert.Equal(expected, decoded.Rgba);
        }
        var actualPngs = Directory.EnumerateFiles(packRoot, "*.png", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(packRoot, path).Replace('\\', '/')).Order(StringComparer.Ordinal);
        Assert.Equal(catalog.Atlases.Select(row => row.Path).Order(StringComparer.Ordinal), actualPngs);
        var expectedSourcePaths = ExpectedSources.Select(row => $"objects/{row.Stem}.cel")
            .Append(NativeObjectArtCompiler.CathedralPalettePath)
            .Append($"assets/txtdata/{NativeObjectArtCompiler.ObjectMetadataPath}").Order(StringComparer.Ordinal);
        Assert.Equal(expectedSourcePaths, catalog.SourceHashes.Select(row => row.Path).Order(StringComparer.Ordinal));
        var metadata = FindRepositoryFile("assets/txtdata/objects/objdat.tsv");
        var metadataHash = Sha256(ReadBoundedFile(metadata, 1024 * 1024));
        Assert.Equal(NativeObjectArtCompiler.CanonicalObjectMetadataSha256, metadataHash);
        Assert.Equal(metadataHash, Assert.Single(catalog.SourceHashes,
            row => row.Path == $"assets/txtdata/{NativeObjectArtCompiler.ObjectMetadataPath}").Sha256);
    }

    [Fact(Explicit = true)]
    public void MatchesNativeMetadataAndEveryProductionDrawAnchorAndOpaqueBounds()
    {
        var root = RequireDirectory(ReferenceVariable);
        var reference = ReadReference(root);
        var catalog = ReadCatalog(RequireDirectory(PackVariable));
        var expected = new (uint Id, string Stem, int Width, int Delay, int Length)[] {
            (1, "l1doors", 64, 1, 0), (2, "l1doors", 64, 2, 0),
            (5, "chest1", 96, 1, 0), (6, "chest2", 96, 1, 0), (7, "chest3", 96, 1, 0),
            (48, "sarc", 128, 1, 5), (53, "traphole", 64, 1, 0), (54, "traphole", 64, 2, 0),
            (57, "barrel", 96, 1, 9), (58, "barrelex", 96, 1, 10),
        };
        var metadata = ReadTsv(root, "native-object-art-index.tsv",
            "native_object_id\tstem\tanim_width\tanim_delay\tanim_length\tanimated\tinitial_frame\talternate_frame\topened_frame\talternate_opened_frame\tfinal_frame\tsource_frame_count", 12);
        Assert.Equal(expected.Length, metadata.Length);
        for (var index = 0; index < expected.Length; index++) {
            var row = metadata[index]; var spec = expected[index];
            Assert.Equal(spec.Id.ToString(), row[0]); Assert.Equal(spec.Stem, row[1]);
            Assert.Equal(spec.Width.ToString(), row[2]); Assert.Equal(spec.Delay.ToString(), row[3]);
            Assert.Equal(spec.Length.ToString(), row[4]); Assert.Equal("0", row[5]);
            Assert.Equal(spec.Delay.ToString(), row[6]);
            var chest = spec.Id is >= 5 and <= 7; var door = spec.Id is 1 or 2;
            Assert.Equal(chest ? "4" : "", row[7]);
            Assert.Equal(chest || door ? (spec.Delay + 2).ToString() : "", row[8]);
            Assert.Equal(chest ? "6" : "", row[9]);
            Assert.Equal(spec.Length > 0 ? spec.Length.ToString() : "", row[10]);
            Assert.Equal(reference[spec.Stem].Count.ToString(), row[11]);
            var definition = Assert.Single(catalog.Objects, item => item.NativeObjectId == spec.Id);
            var sprite = Assert.Single(catalog.Sprites, item => item.SpriteId == definition.SpriteId);
            Assert.Equal($"objects/{row[1]}.cel", sprite.SourcePath);
            Assert.Equal((spec.Delay, spec.Length, spec.Width, false),
                (definition.AnimDelay, definition.AnimLen, definition.AnimWidth, definition.Animated));
        }
        var draws = ReadTsv(root, "native-object-art-draws.tsv",
            "native_object_id\tstate\tframe_1based\tprobe\ttile_dx\ttile_dy\ttarget_x\ttarget_y\tdraw_anchor_x\tdraw_anchor_y\ttop_left_x\ttop_left_y\tnominal_width\theight\topaque_min_x\topaque_min_y\topaque_max_x_exclusive\topaque_max_y_exclusive", 18);
        Assert.InRange(draws.Length, 1, 4096);
        foreach (var row in draws) {
            var id = uint.Parse(row[0]); var spec = Assert.Single(expected, item => item.Id == id);
            var number = int.Parse(row[2]);
            Assert.InRange(number, 1, reference[spec.Stem].Count);
            var source = reference[spec.Stem][number - 1];
            var probe = row[3]; Assert.Contains(probe, new[] { "owner", "occupied-ne" });
            var dx = int.Parse(row[4]); var dy = int.Parse(row[5]);
            Assert.Equal(probe == "owner" ? (0, 0) : (0, -1), (dx, dy));
            var frame = new NativeObjectFrame(0, 0, 0, source.Width, source.Height, source.NativeWidth, (uint)number);
            var bounds = NativeObjectPlacement.ResolveBounds(int.Parse(row[6]), int.Parse(row[7]), frame, dx, dy);
            Assert.Equal((double)int.Parse(row[8]), bounds.X);
            Assert.Equal((double)int.Parse(row[9]), bounds.Y + source.Height - 1);
            Assert.Equal((double)int.Parse(row[10]), bounds.X); Assert.Equal((double)int.Parse(row[11]), bounds.Y);
            Assert.Equal((source.NativeWidth, source.Height), (int.Parse(row[12]), int.Parse(row[13])));
            var opaque = Enumerable.Range(0, source.Width * source.Height)
                .Where(pixel => source.IndexOpacity[pixel * 2 + 1] == byte.MaxValue).ToArray();
            var expectedOpaque = opaque.Length == 0 ? new[] { -1, -1, -1, -1 } : new[] {
                (int)bounds.X + opaque.Min(pixel => pixel % source.Width),
                (int)bounds.Y + opaque.Min(pixel => pixel / source.Width),
                (int)bounds.X + opaque.Max(pixel => pixel % source.Width) + 1,
                (int)bounds.Y + opaque.Max(pixel => pixel / source.Width) + 1,
            };
            Assert.Equal(expectedOpaque, row.Skip(14).Select(int.Parse));
        }
        foreach (var spec in expected) {
            var rows = draws.Where(row => uint.Parse(row[0]) == spec.Id).ToArray();
            Assert.Equal(reference[spec.Stem].Count * 2, rows.Count(row => row[1] == "source-frame"));
            foreach (var frame in Enumerable.Range(1, reference[spec.Stem].Count))
                Assert.Equal(new[] { "occupied-ne", "owner" }, rows.Where(row => row[1] == "source-frame" && int.Parse(row[2]) == frame)
                    .Select(row => row[3]).Order(StringComparer.Ordinal));
            Assert.Contains(rows, row => row[1] != "source-frame");
        }
        foreach (var state in new[] { "door-closed", "door-open", "door-blocked-close-refused", "door-reclosed",
            "chest-closed", "chest-closed-alternate", "chest-open", "chest-open-alternate", "sarcophagus-active-frame",
            "static-trap-armed", "static-trap-triggered", "barrel-break-animation-frame", "barrel-broken-final" })
            Assert.Contains(draws, row => row[1] == state);
    }

    private static string[][] ReadTsv(string root, string path, string header, int columns)
    {
        var text = new UTF8Encoding(false, true).GetString(ReadBoundedFile(SafeFile(root, path), 1024 * 1024));
        Assert.DoesNotContain("\n", text.Replace("\r\n", ""));
        var lines = text.Split("\r\n", StringSplitOptions.None);
        Assert.Equal(header, lines[0]); Assert.Equal("", lines[^1]);
        var rows = lines.Skip(1).SkipLast(1).Select(line => line.Split('\t')).ToArray();
        Assert.All(rows, row => Assert.Equal(columns, row.Length));
        return rows;
    }

    private static NativeObjectArtCatalog ReadCatalog(string root)
    {
        var bytes = ReadBoundedFile(SafeFile(root, NativeObjectArtCompiler.CatalogFileName), NativeObjectArtCompiler.MaximumCatalogBytes);
        var json = new UTF8Encoding(false, true).GetString(bytes);
        Assert.True(NativeObjectCatalog.TryParse(json, out var parsed, out var errors), string.Join("; ", errors));
        Assert.NotNull(parsed);
        return JsonSerializer.Deserialize<NativeObjectArtCatalog>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static Dictionary<string, List<ReferenceFrame>> ReadReference(string root)
    {
        var bytes = ReadBoundedFile(SafeFile(root, "native-object-art-reference.bin"), MaximumReferenceBytes);
        Assert.Equal("DVLNOAR1", Encoding.ASCII.GetString(bytes, 0, 8));
        var offset = 8;
        Assert.Equal(8U, ReadNumber(bytes, ref offset));
        var result = new Dictionary<string, List<ReferenceFrame>>(StringComparer.Ordinal);
        foreach (var expected in ExpectedSources) {
            var nameLength = checked((int)ReadNumber(bytes, ref offset));
            Assert.InRange(nameLength, 1, 32);
            Assert.InRange(offset, 0, bytes.Length - nameLength);
            var name = new UTF8Encoding(false, true).GetString(bytes, offset, nameLength);
            offset += nameLength;
            Assert.Equal(expected.Stem, name);
            var count = checked((int)ReadNumber(bytes, ref offset));
            Assert.InRange(count, expected.MinimumFrames, NativeTownerCelDecoder.MaximumFramesPerGroup);
            var frames = new List<ReferenceFrame>(count);
            for (var index = 0; index < count; index++) {
                var number = checked((int)ReadNumber(bytes, ref offset));
                var nativeWidth = checked((int)ReadNumber(bytes, ref offset));
                var width = checked((int)ReadNumber(bytes, ref offset));
                var height = checked((int)ReadNumber(bytes, ref offset));
                Assert.Equal(index + 1, number);
                Assert.Equal(expected.Width, nativeWidth);
                Assert.InRange(width, nativeWidth, NativeTownerCelDecoder.MaximumFrameWidth);
                Assert.InRange(height, 1, NativeTownerCelDecoder.MaximumFrameHeight);
                var size = checked(width * height * 2);
                Assert.InRange(offset, 0, bytes.Length - size);
                var pairs = bytes.AsSpan(offset, size).ToArray();
                offset += size;
                for (var pixel = 0; pixel < width * height; pixel++) {
                    Assert.True(pairs[pixel * 2 + 1] is 0 or byte.MaxValue);
                    if (pairs[pixel * 2 + 1] == 0)
                        Assert.Equal((byte)0, pairs[pixel * 2]);
                }
                frames.Add(new ReferenceFrame(number, nativeWidth, width, height, pairs));
            }
            Assert.True(result.TryAdd(name, frames));
        }
        Assert.Equal(bytes.Length, offset);
        return result;
    }

    private static uint ReadNumber(byte[] bytes, ref int offset)
    {
        Assert.InRange(offset, 0, bytes.Length - sizeof(uint));
        var value = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)));
        offset += sizeof(uint);
        return value;
    }

    private static byte[] ReadBoundedFile(string path, long maximum)
    {
        var info = new FileInfo(path);
        Assert.True(info.Exists, $"Required native comparison input is missing: {path}");
        Assert.InRange(info.Length, 1, maximum);
        return File.ReadAllBytes(path);
    }

    private static string SafeFile(string root, string relative)
    {
        var segments = relative.Replace('\\', '/').Split('/');
        Assert.False(Path.IsPathRooted(relative));
        Assert.DoesNotContain(':', relative);
        Assert.DoesNotContain(segments, part => part.Length == 0 || part is "." or "..");
        var full = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
        Assert.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar, full,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        return full;
    }

    private static string RequireDirectory(string variable)
    {
        var path = Environment.GetEnvironmentVariable(variable);
        Assert.False(string.IsNullOrWhiteSpace(path), $"Set {variable} for the explicit native comparison.");
        Assert.True(Directory.Exists(path));
        return Path.GetFullPath(path!);
    }

    private static string FindRepositoryFile(string relative)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) {
            var path = Path.Combine(directory.FullName, relative);
            if (File.Exists(path))
                return path;
        }
        throw new FileNotFoundException($"Repository comparison input is missing: {relative}");
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private sealed record ReferenceFrame(int NativeFrame, int NativeWidth, int Width, int Height, byte[] IndexOpacity);
}

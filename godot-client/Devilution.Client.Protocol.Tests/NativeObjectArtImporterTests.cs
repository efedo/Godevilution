using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Devilution.Client.PresentationAssets;
using Devilution.Data;
using Devilution.Tools.NativeObjectArt;
using Devilution.Tools.NativePlayerArt;
using Devilution.Tools.NativeTownerArt;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

public sealed class NativeObjectArtImporterTests
{
    [Fact]
    public void ReadsExactlyTheCanonicalTenL1ObjectsAndEightSharedSpriteFamilies()
    {
        var textData = Path.Combine(FindRepositoryRoot(), "assets", "txtdata");
        var objects = NativeObjectArtCompiler.ReadBaseObjectDefinitions(textData);
        Assert.Equal(new uint[] { 1, 2, 5, 6, 7, 48, 53, 54, 57, 58 }, objects.Select(item => item.NativeObjectId));
        Assert.Equal(new[] {
            (1U, 0, 1, 0, 64, false),
            (2U, 0, 2, 0, 64, false),
            (5U, 1, 1, 0, 96, false),
            (6U, 2, 1, 0, 96, false),
            (7U, 3, 1, 0, 96, false),
            (48U, 4, 1, 5, 128, false),
            (53U, 5, 1, 0, 64, false),
            (54U, 5, 2, 0, 64, false),
            (57U, 6, 1, 9, 96, false),
            (58U, 7, 1, 10, 96, false),
        }, objects.Select(item => (item.NativeObjectId, item.SpriteId, item.AnimDelay, item.AnimLen, item.AnimWidth, item.Animated)));
        Assert.Equal(new[] {
            (0, "objects/l1doors.cel", 64),
            (1, "objects/chest1.cel", 96),
            (2, "objects/chest2.cel", 96),
            (3, "objects/chest3.cel", 96),
            (4, "objects/sarc.cel", 128),
            (5, "objects/traphole.cel", 64),
            (6, "objects/barrel.cel", 96),
            (7, "objects/barrelex.cel", 96),
        }, NativeObjectArtCompiler.ReadBaseSpriteDefinitions(textData));
    }

    [Fact]
    public void RejectsEditedCanonicalObjectMetadataAndPreservesOpaquePaletteZeroInDecodedFrames()
    {
        var root = Path.Combine(Path.GetTempPath(), "godevilution-native-object-metadata-" + Guid.NewGuid().ToString("N"));
        var metadataPath = Path.Combine(root, "objects", "objdat.tsv");
        Directory.CreateDirectory(Path.GetDirectoryName(metadataPath)!);
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "assets", "txtdata", "objects", "objdat.tsv"), new UTF8Encoding(false));
        File.WriteAllText(metadataPath, source.Replace("OBJ_L1LDOOR", "OBJ_L1RDOOR", StringComparison.Ordinal), new UTF8Encoding(false));
        try {
            var exception = Assert.Throws<InvalidDataException>(() => NativeObjectArtCompiler.ReadBaseObjectDefinitions(root));
            Assert.Contains("canonical", exception.Message, StringComparison.Ordinal);
        } finally {
            DeleteOwnedTemporaryDirectory(root);
        }

        var frame = Assert.Single(Assert.Single(NativeTownerCelDecoder.Decode(BuildSingleGroup([[1, 0, 193]]), 64, 1, 1).Directions));
        Assert.Equal((byte)0, frame.Indices[0]);
        Assert.Equal(byte.MaxValue, frame.Opacity[0]);
        Assert.Equal((byte)0, frame.Opacity[1]);
    }

    [Fact]
    public void RejectsCorruptNativeCelHeaderAndFrameOffsets()
    {
        var valid = BuildCel(64, 4);
        var badCount = valid.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(badCount, uint.MaxValue);
        Assert.Throws<InvalidDataException>(() => NativeTownerCelDecoder.Decode(badCount, 64, 1, 0));

        var badFirstOffset = valid.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(badFirstOffset.AsSpan(sizeof(uint), sizeof(uint)), 0);
        Assert.Throws<InvalidDataException>(() => NativeTownerCelDecoder.Decode(badFirstOffset, 64, 1, 0));
    }

    [Fact]
    public void ImportsSyntheticOriginalFilesDeterministicallyAndRemovesStagingAfterMalformedCel()
    {
        using var profile = new NativeObjectProfileFixture();
        var sourceRoot = Path.Combine(profile.Root, "source");
        var outputA = Path.Combine(profile.Root, "output-a");
        var outputB = Path.Combine(profile.Root, "output-b");
        var outputFailed = Path.Combine(profile.Root, "output-failed");
        WriteSyntheticOriginalFiles(sourceRoot);

        var optionsA = new NativeObjectImportOptions(sourceRoot, profile.TextDataRoot, [profile.ProfileDirectory], outputA);
        var optionsB = new NativeObjectImportOptions(sourceRoot, profile.TextDataRoot, [profile.ProfileDirectory], outputB);
        var first = NativeObjectArtCompiler.Import(optionsA);
        var second = NativeObjectArtCompiler.Import(optionsB);
        Assert.Equal(8, first.SpriteCount);
        Assert.Equal(10, first.ObjectCount);
        Assert.Equal(first.PackId, second.PackId);
        Assert.Equal(File.ReadAllBytes(Path.Combine(outputA, NativeObjectArtCompiler.CatalogFileName)),
            File.ReadAllBytes(Path.Combine(outputB, NativeObjectArtCompiler.CatalogFileName)));
        foreach (var path in Directory.EnumerateFiles(Path.Combine(outputA, "sprites"), "*.png"))
            Assert.Equal(File.ReadAllBytes(path), File.ReadAllBytes(Path.Combine(outputB, "sprites", Path.GetFileName(path))));
        var doorPng = DecodePng(File.ReadAllBytes(Path.Combine(outputA, "sprites", "0-l1doors.png")));
        Assert.Equal((320, 1), (doorPng.Width, doorPng.Height));
        Assert.Equal(new byte[] { 0, 255, 0, 255 }, doorPng.Rgba[..4]);
        Assert.Equal((byte)0, doorPng.Rgba[7]);
        Assert.Equal(new byte[] { 7, 248, 3, 255 }, doorPng.Rgba[(128 * 4)..(128 * 4 + 4)]);

        var jsonBytes = File.ReadAllBytes(Path.Combine(outputA, NativeObjectArtCompiler.CatalogFileName));
        var jsonText = Encoding.UTF8.GetString(jsonBytes);
        Assert.DoesNotContain("\n", jsonText.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.True(NativeObjectCatalog.TryParse(jsonText, out var catalog, out var catalogErrors), string.Join(" ", catalogErrors));
        Assert.NotNull(catalog);
        Assert.Single(catalog.ProfileBindings);
        Assert.Equal(1U, catalog.ProfileBindings[0].LevelId);
        Assert.Equal("cathedral", catalog.ProfileBindings[0].Location);
        var malformedPath = Path.Combine(sourceRoot, "objects", "l1doors.cel");
        File.WriteAllBytes(malformedPath, BuildCel(64, 3));
        var exception = Assert.Throws<InvalidDataException>(() => NativeObjectArtCompiler.Import(
            new NativeObjectImportOptions(sourceRoot, profile.TextDataRoot, [profile.ProfileDirectory], outputFailed)));
        Assert.Contains("l1doors.cel", exception.Message, StringComparison.Ordinal);
        Assert.Contains("at least 4 source frames", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(outputFailed));
        Assert.DoesNotContain(Directory.EnumerateDirectories(profile.Root), path =>
            Path.GetFileName(path).StartsWith(".output-failed.staging-", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsEveryLifecycleSourceBelowItsMinimumFrameCount()
    {
        using var profile = new NativeObjectProfileFixture();
        var sourceRoot = Path.Combine(profile.Root, "truncated-source");
        var sprites = NativeObjectArtCompiler.ReadBaseSpriteDefinitions(Path.Combine(FindRepositoryRoot(), "assets", "txtdata"));
        var minimumFrames = new[] { 4, 6, 6, 6, 5, 2, 9, 10 };

        foreach (var sprite in sprites) {
            WriteSyntheticOriginalFiles(sourceRoot);
            var minimum = minimumFrames[sprite.SpriteId];
            var sourcePath = Path.Combine(sourceRoot, sprite.SourcePath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllBytes(sourcePath, BuildCel(sprite.NativeWidth, minimum - 1));
            var output = Path.Combine(profile.Root, $"truncated-output-{sprite.SpriteId}");

            var exception = Assert.Throws<InvalidDataException>(() => NativeObjectArtCompiler.Import(
                new NativeObjectImportOptions(sourceRoot, profile.TextDataRoot, [profile.ProfileDirectory], output)));
            Assert.Contains(Path.GetFileName(sourcePath), exception.Message, StringComparison.Ordinal);
            Assert.Contains($"at least {minimum} source frames", exception.Message, StringComparison.Ordinal);
            Assert.False(Directory.Exists(output));
            Assert.DoesNotContain(Directory.EnumerateDirectories(profile.Root), path =>
                Path.GetFileName(path).StartsWith($".truncated-output-{sprite.SpriteId}.staging-", StringComparison.Ordinal));
        }
    }

    private static void WriteSyntheticOriginalFiles(string sourceRoot)
    {
        var palettePath = Path.Combine(sourceRoot, NativeObjectArtCompiler.CathedralPalettePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(palettePath)!);
        var palette = new byte[NativePlayerPalette.ColorCount * NativePlayerPalette.BytesPerColor];
        for (var index = 0; index < NativePlayerPalette.ColorCount; index++) {
            palette[index * 3] = checked((byte)index);
            palette[index * 3 + 1] = checked((byte)(255 - index));
            palette[index * 3 + 2] = checked((byte)(index / 2));
        }
        File.WriteAllBytes(palettePath, palette);
        foreach (var sprite in NativeObjectArtCompiler.ReadBaseSpriteDefinitions(Path.Combine(FindRepositoryRoot(), "assets", "txtdata"))) {
            var path = Path.Combine(sourceRoot, sprite.SourcePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var minimumFrames = new[] { 4, 6, 6, 6, 5, 2, 9, 10 };
            var sourceFrames = minimumFrames[sprite.SpriteId] + (sprite.SpriteId == 0 ? 1 : 0);
            File.WriteAllBytes(path, BuildCel(sprite.NativeWidth, sourceFrames));
        }
    }

    private static byte[] BuildCel(int width, int frameCount)
    {
        var frames = Enumerable.Range(0, frameCount)
            .Select(frame => frame switch {
                0 => new byte[] { 1, 0, checked((byte)(256 - (width - 1))) },
                2 => new byte[] { 1, 7, checked((byte)(256 - (width - 1))) },
                _ => new byte[] { checked((byte)(256 - width)) },
            })
            .ToArray();
        var headerBytes = checked((frameCount + 2) * sizeof(uint));
        var length = checked(headerBytes + frames.Sum(frame => frame.Length));
        var cel = new byte[length];
        BinaryPrimitives.WriteUInt32LittleEndian(cel, checked((uint)frameCount));
        var cursor = headerBytes;
        for (var frame = 0; frame < frameCount; frame++) {
            BinaryPrimitives.WriteUInt32LittleEndian(cel.AsSpan((frame + 1) * sizeof(uint)), checked((uint)cursor));
            frames[frame].CopyTo(cel, cursor);
            cursor += frames[frame].Length;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(cel.AsSpan((frameCount + 1) * sizeof(uint)), checked((uint)cursor));
        return cel;
    }

    private static byte[] BuildSingleGroup(IReadOnlyList<byte[]> frames)
    {
        var headerBytes = checked((frames.Count + 2) * sizeof(uint));
        var result = new byte[checked(headerBytes + frames.Sum(frame => frame.Length))];
        BinaryPrimitives.WriteUInt32LittleEndian(result, checked((uint)frames.Count));
        var cursor = headerBytes;
        for (var index = 0; index < frames.Count; index++) {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan((index + 1) * sizeof(uint)), checked((uint)cursor));
            frames[index].CopyTo(result, cursor);
            cursor += frames[index].Length;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan((frames.Count + 1) * sizeof(uint)), checked((uint)cursor));
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
            var type = Encoding.ASCII.GetString(png, cursor + 4, 4);
            if (type == "IDAT")
                compressed.Write(png, cursor + 8, length);
            cursor = checked(cursor + 12 + length);
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

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) {
            if (File.Exists(Path.Combine(directory.FullName, "assets", "txtdata", "objects", "objdat.tsv")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate repository assets for the native object test fixture.");
    }

    private static void DeleteOwnedTemporaryDirectory(string root)
    {
        if (!Directory.Exists(root))
            return;
        var full = Path.GetFullPath(root);
        Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetDirectoryName(full), StringComparer.OrdinalIgnoreCase);
        Directory.Delete(full, recursive: true);
    }

    private sealed class NativeObjectProfileFixture : IDisposable
    {
        public NativeObjectProfileFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "godevilution-native-object-profile-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            TextDataRoot = Path.Combine(FindRepositoryRoot(), "assets", "txtdata");
            ProfileDirectory = Root;
            WritePack("content", "godevilution.cathedral-doors-playtest", "0.1.0",
                "level_id\tlevel_symbol\r\n0\ttown\r\n1\tcathedral\r\n2\tcathedral_depth_2\r\n3\tcathedral_depth_3\r\n4\tcathedral_depth_4\r\n",
                "levels.tsv");
            WritePack("native_cathedral_population", "godevilution.cathedral-native-population-metadata", "1.0.0",
                "key\tvalue\r\ntest\tpopulation\r\n");
            WritePack("native_cathedral_runtime", "godevilution.cathedral-native-runtime", "1.0.0",
                "class_id\tclass_symbol\thero_base_life_maximum\thero_base_mana_maximum\thero_light_radius\thero_idle_frames\r\n0\twarrior\t4480\t640\t10\t10\r\n",
                "native_cathedral_runtime/native-character-start.tsv");
            var content = ContentPackLoader.LoadManifestDirectory(Path.Combine(Root, "content"));
            var population = ContentPackLoader.LoadManifestDirectory(Path.Combine(Root, "native_cathedral_population"));
            var runtime = ContentPackLoader.LoadManifestDirectory(Path.Combine(Root, "native_cathedral_runtime"));
            var manifestHash = new ContentManifest(content.Id, content.Version, [content, population, runtime]).Sha256;
            var profile = new {
                formatVersion = 4,
                adapterId = NativePlayerArtProfiles.ClassProgressionAdapterId,
                contentDirectory = "content",
                contentId = content.Id,
                contentVersion = content.Version,
                contentManifestHash = manifestHash,
                populationDirectory = "native_cathedral_population",
                populationPackId = population.Id,
                populationPackVersion = population.Version,
                runtimeDirectory = "native_cathedral_runtime",
                runtimePackId = runtime.Id,
                runtimePackVersion = runtime.Version,
                nativeDepth = 4,
                heroClassSymbol = "warrior",
            };
            File.WriteAllText(Path.Combine(Root, "cathedral-native-class-progression-playtest.json"),
                JsonSerializer.Serialize(profile), new UTF8Encoding(false));
        }

        public string Root { get; }
        public string TextDataRoot { get; }
        public string ProfileDirectory { get; }

        public void Dispose()
        {
            DeleteOwnedTemporaryDirectory(Root);
        }

        private void WritePack(string directoryName, string id, string version, string table, string tableFileName = "data.tsv")
        {
            var root = Path.Combine(Root, directoryName);
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "content-pack.json"),
                JsonSerializer.Serialize(new { formatVersion = 1, id, version }), new UTF8Encoding(false));
            var tablePath = Path.Combine(root, tableFileName.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(tablePath)!);
            File.WriteAllText(tablePath, table, new UTF8Encoding(false));
        }
    }
}

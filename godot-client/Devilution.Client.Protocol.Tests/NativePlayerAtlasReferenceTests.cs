using System.Buffers.Binary;
using System.IO.Compression;
using Devilution.Client.PresentationAssets;
using Xunit;

namespace Devilution.Client.Protocol.Tests;

/** Checks the published PNG cells, including all padding, against native rendered pixels. */
public sealed class NativePlayerAtlasReferenceTests
{
    [Fact(Explicit = true)]
    public void OptionallyMatchesPublishedAtlasesToEveryNativeReferencePixel()
    {
        var referenceRoot = RequireDirectory("GODEVILUTION_NATIVE_PLAYER_ART_REFERENCE_DIRECTORY");
        var packRoot = RequireDirectory("GODEVILUTION_NATIVE_PLAYER_ART_PACK_DIRECTORY");
        Assert.True(NativePlayerCatalog.TryParse(
            File.ReadAllText(Path.Combine(packRoot, "native-player-catalog.json")), out var catalog, out var diagnostics),
            string.Join("; ", diagnostics));
        Assert.NotNull(catalog);
        Assert.NotEmpty(catalog.Clips);
        var inventory = File.ReadAllLines(Path.Combine(referenceRoot, "native-reference.tsv"))
            .Skip(1).Select(line => line.Split('\t')).ToDictionary(fields => fields[1], StringComparer.Ordinal);
        Assert.Equal(765, inventory.Count);
        var townPalette = File.ReadAllBytes(Path.Combine(referenceRoot, "levels/towndata/town.pal"));
        var cathedralPalette = File.ReadAllBytes(Path.Combine(referenceRoot, "levels/l1data/l1_1.pal"));
        Assert.Equal(768, townPalette.Length);
        Assert.Equal(768, cathedralPalette.Length);
        var additionalSourceFrames = new Dictionary<string, (int Source, int Cycle)>(StringComparer.Ordinal) {
            ["plrgfx/warrior/wln/wlnlm.cl2"] = (21, 20),
            ["plrgfx/warrior/wmd/wmdlm.cl2"] = (21, 20),
            ["plrgfx/warrior/whm/whmat.cl2"] = (17, 16),
            ["plrgfx/rogue/rmt/rmtat.cl2"] = (18, 16),
            ["plrgfx/rogue/rhu/rhuht.cl2"] = (8, 7),
            ["plrgfx/rogue/rhu/rhuqm.cl2"] = (17, 16),
            ["plrgfx/rogue/rht/rhtat.cl2"] = (18, 16),
        };
        var comparedFrames = 0;
        var seenSources = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, clip) in catalog.Clips) {
            var relativeSource = SourcePath(key);
            seenSources.Add(relativeSource);
            Assert.True(inventory.TryGetValue(relativeSource, out var facts), relativeSource);
            Assert.Equal(int.Parse(facts![2], System.Globalization.CultureInfo.InvariantCulture), clip.EffectiveAnchorWidth);
            Assert.Equal(int.Parse(facts[4], System.Globalization.CultureInfo.InvariantCulture), clip.FramesPerDirection);
            var expectedCounts = additionalSourceFrames.GetValueOrDefault(relativeSource, (clip.FramesPerDirection, clip.FramesPerDirection));
            Assert.Equal((expectedCounts.Item1, expectedCounts.Item2), (clip.FramesPerDirection, clip.EffectiveAnimationFrames));
            var png = ReadRgbaPng(Path.Combine(packRoot, clip.Path));
            Assert.Equal((clip.FrameWidth * clip.FramesPerDirection, clip.FrameHeight * 8), (png.Width, png.Height));
            var palette = key.Location == NativePlayerLocation.Town ? townPalette : cathedralPalette;
            using var reference = new BinaryReader(File.OpenRead(Path.Combine(referenceRoot, relativeSource + ".native.bin")));
            Assert.Equal(8u, reference.ReadUInt32());
            var maximumWidth = 0;
            var maximumHeight = 0;
            for (var direction = 0; direction < 8; direction++) {
                Assert.Equal((uint)clip.FramesPerDirection, reference.ReadUInt32());
                for (var frame = 0; frame < clip.FramesPerDirection; frame++) {
                    var width = checked((int)reference.ReadUInt32());
                    var height = checked((int)reference.ReadUInt32());
                    Assert.InRange(width, clip.EffectiveAnchorWidth, clip.FrameWidth);
                    Assert.InRange(height, 1, clip.FrameHeight);
                    maximumWidth = Math.Max(maximumWidth, width);
                    maximumHeight = Math.Max(maximumHeight, height);
                    var native = reference.ReadBytes(checked(width * height * 2));
                    Assert.Equal(width * height * 2, native.Length);
                    CompareCell(png.Pixels, png.Width, clip, direction, frame, width, height, native, palette, relativeSource);
                    comparedFrames++;
                }
            }
            Assert.Equal(reference.BaseStream.Length, reference.BaseStream.Position);
            Assert.Equal((maximumWidth, maximumHeight), (clip.FrameWidth, clip.FrameHeight));
        }
        Assert.Equal(inventory.Keys.Order(StringComparer.Ordinal), seenSources.Order(StringComparer.Ordinal));
        Assert.True(comparedFrames > 78016, "Town/Cathedral palette variants must compare all admitted catalog cells.");
    }

    private static void CompareCell(byte[] rgba, int atlasWidth, NativePlayerClip clip, int direction, int frame,
        int nativeWidth, int nativeHeight, byte[] native, byte[] palette, string source)
    {
        var topPadding = clip.FrameHeight - nativeHeight;
        for (var y = 0; y < clip.FrameHeight; y++) {
            for (var x = 0; x < clip.FrameWidth; x++) {
                var target = ((direction * clip.FrameHeight + y) * atlasWidth + frame * clip.FrameWidth + x) * 4;
                var sourcePixel = y >= topPadding && x < nativeWidth ? ((y - topPadding) * nativeWidth + x) * 2 : -1;
                var opaque = sourcePixel >= 0 && native[sourcePixel + 1] != 0;
                var color = opaque ? native[sourcePixel] * 3 : 0;
                var red = opaque ? palette[color] : 0;
                var green = opaque ? palette[color + 1] : 0;
                var blue = opaque ? palette[color + 2] : 0;
                if (rgba[target] != red || rgba[target + 1] != green || rgba[target + 2] != blue
                    || rgba[target + 3] != (opaque ? 255 : 0))
                    Assert.Fail($"Published atlas mismatch {source}, direction {direction}, frame {frame}, x {x}, y {y}.");
            }
        }
    }

    private static string SourcePath(NativePlayerClipKey key)
    {
        var (classPath, classCharacter) = key.ClassId switch {
            0 => ("warrior", 'w'), 1 => ("rogue", 'r'), 2 => ("sorceror", 's'),
            _ => throw new InvalidDataException("The original reference covers only base classes."),
        };
        var armor = key.Armor switch {
            NativePlayerArmor.Light => 'l', NativePlayerArmor.Medium => 'm', NativePlayerArmor.Heavy => 'h',
            _ => throw new InvalidDataException("Unknown armor."),
        };
        var weapon = key.Weapon switch {
            NativePlayerWeapon.Unarmed => 'n', NativePlayerWeapon.UnarmedShield => 'u',
            NativePlayerWeapon.Sword => 's', NativePlayerWeapon.SwordShield => 'd', NativePlayerWeapon.Bow => 'b',
            NativePlayerWeapon.Axe => 'a', NativePlayerWeapon.Mace => 'm', NativePlayerWeapon.MaceShield => 'h',
            NativePlayerWeapon.Staff => 't', _ => throw new InvalidDataException("Unknown weapon."),
        };
        var suffix = key.Graphic switch {
            NativePlayerGraphic.TownStand => "st", NativePlayerGraphic.TownWalk => "wl",
            NativePlayerGraphic.Stand => "as", NativePlayerGraphic.Walk => "aw", NativePlayerGraphic.Attack => "at",
            NativePlayerGraphic.Hit => "ht", NativePlayerGraphic.Block => "bl", NativePlayerGraphic.Lightning => "lm",
            NativePlayerGraphic.Fire => "fm", NativePlayerGraphic.Magic => "qm", NativePlayerGraphic.Death => "dt",
            _ => throw new InvalidDataException("Unknown graphic."),
        };
        var prefix = string.Concat(classCharacter, armor, weapon);
        return $"plrgfx/{classPath}/{prefix}/{prefix}{suffix}.cl2";
    }

    private static (int Width, int Height, byte[] Pixels) ReadRgbaPng(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, reader.ReadBytes(8));
        using var compressed = new MemoryStream();
        var width = 0;
        var height = 0;
        while (reader.BaseStream.Position < reader.BaseStream.Length) {
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(reader.ReadBytes(4)));
            var type = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));
            Assert.InRange(length, 0, 128 * 1024 * 1024);
            var data = reader.ReadBytes(length);
            Assert.Equal(length, data.Length);
            Assert.Equal(4, reader.ReadBytes(4).Length); // CRC behavior is verified by the synthetic PNG tests.
            if (type == "IHDR") {
                Assert.Equal(13, data.Length);
                width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data));
                height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4)));
                Assert.Equal(new byte[] { 8, 6, 0, 0, 0 }, data.AsSpan(8).ToArray());
                Assert.InRange(width, 1, NativePlayerCatalog.MaximumAtlasDimension);
                Assert.InRange(height, 1, NativePlayerCatalog.MaximumAtlasDimension);
                Assert.InRange((long)width * height, 1, NativePlayerCatalog.MaximumAtlasPixels);
            } else if (type == "IDAT") {
                compressed.Write(data);
            }
        }
        Assert.True(width > 0 && height > 0);
        compressed.Position = 0;
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        var stride = checked(width * 4 + 1);
        var filtered = new byte[checked(stride * height)];
        zlib.ReadExactly(filtered);
        Assert.Equal(-1, zlib.ReadByte());
        var pixels = new byte[checked(width * height * 4)];
        for (var y = 0; y < height; y++) {
            Assert.Equal((byte)0, filtered[y * stride]);
            filtered.AsSpan(y * stride + 1, width * 4).CopyTo(pixels.AsSpan(y * width * 4));
        }
        return (width, height, pixels);
    }

    private static string RequireDirectory(string variable)
    {
        var path = Environment.GetEnvironmentVariable(variable);
        Assert.False(string.IsNullOrWhiteSpace(path), $"Set {variable} to the explicit private artifact directory.");
        Assert.True(Directory.Exists(path), $"Missing {variable} directory.");
        return Path.GetFullPath(path!);
    }
}
